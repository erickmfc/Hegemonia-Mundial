using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_AirDirector : IIAIncrementalUpdateModule
    {
        private enum DecisionPhase
        {
            Idle,
            FindAirEnemy,
            FindGroundEnemy,
            ResolvePressure,
            DispatchIntercept,
            DispatchBombers,
            DispatchTransport,
            Finish
        }

        private const float ForcedAirStrikeStartSeconds = 35f;
        private readonly IA_Context _context;
        private readonly List<IA_EnemyObservation> _enemyMemoryBuffer = new List<IA_EnemyObservation>(64);
        private readonly List<IA_StrategicTargetData> _strategicTargetsBuffer = new List<IA_StrategicTargetData>(6);
        private readonly List<GameObject> _activeAirUnitsBuffer = new List<GameObject>(12);
        private readonly List<GameObject> _activeAirTransportBuffer = new List<GameObject>(8);
        private readonly List<GameObject> _airRaidA = new List<GameObject>(4);
        private readonly List<GameObject> _airRaidB = new List<GameObject>(4);
        private readonly List<GameObject> _airRaidC = new List<GameObject>(4);
        private readonly Dictionary<GerenciadorAeroporto, int> _readyAircraftByAirport = new Dictionary<GerenciadorAeroporto, int>(8);
        private readonly List<GameObject> _activeBombersBuffer = new List<GameObject>(8);
        private float _nextDecisionTime;
        private DecisionPhase _decisionPhase;
        private Vector3 _decisionBaseCenter;
        private Transform _decisionAirEnemy;
        private Transform _decisionGroundEnemy;
        private Vector3 _decisionPressureTarget;
        private long _decisionTickStart;
        private long _decisionSensorStart;
        private bool _readyAircraftReserveValid;

        public IA_AirDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_AirDirector"; }
        }

        public float Interval
        {
            // O tutorial continua usando todas as aeronaves; apenas evita
            // recalcular a mesma decisao de esquadra em intervalos curtos.
            get { return SceneManager.GetActiveScene().name == "Md Historia" ? 3f : 1.25f; }
        }

        public float BudgetMs
        {
            get { return 0.35f; }
        }

        public void Tick(float now, float deltaTime)
        {
            // Compatibilidade para qualquer chamador legado fora do scheduler.
            // O scheduler usa TickSlice e distribui as mesmas etapas entre frames.
            int safety = 0;
            while (safety++ < 16 && !TickSlice(now, deltaTime, 100f))
            {
            }
        }

        public bool TickSlice(float now, float deltaTime, float budgetMs)
        {
            if (_decisionPhase == DecisionPhase.Idle)
            {
                _activeAirUnitsBuffer.Clear();
                _activeAirTransportBuffer.Clear();
                _activeBombersBuffer.Clear();
                _readyAircraftReserveValid = false;

                if (_context == null || _context.Brain == null || _context.WorldState == null)
                {
                    return true;
                }

                if (_context.Brain.IsBootstrapActive
                    && (int)_context.Brain.BootstrapStage < (int)IA_BrainMaster.IA_BootstrapStage.ProduceAircraft)
                {
                    return true;
                }

                if (now < _nextDecisionTime)
                {
                    return true;
                }

                _nextDecisionTime = now + ResolveDecisionDelay();
                _decisionBaseCenter = _context.WorldState.BaseCenter;
                if (_decisionBaseCenter == Vector3.zero)
                {
                    _decisionBaseCenter = _context.Brain.transform.position;
                }

                _decisionAirEnemy = null;
                _decisionGroundEnemy = null;
                _decisionPressureTarget = Vector3.zero;
                _decisionTickStart = System.Diagnostics.Stopwatch.GetTimestamp();
                _decisionSensorStart = _decisionTickStart;
                _decisionPhase = DecisionPhase.FindAirEnemy;
                return false;
            }

            // Uma fatia é uma operação curta e indivisível. O próximo frame continua
            // na fase seguinte; nenhuma ordem já emitida é descartada.
            switch (_decisionPhase)
            {
                case DecisionPhase.FindAirEnemy:
                    _decisionAirEnemy = GetVisibleAirEnemy();
                    _decisionPhase = DecisionPhase.FindGroundEnemy;
                    break;
                case DecisionPhase.FindGroundEnemy:
                    _decisionGroundEnemy = _context.WorldState.GetNearestVisibleEnemy(_decisionBaseCenter, IA_Domain.Land);
                    _decisionPhase = DecisionPhase.ResolvePressure;
                    break;
                case DecisionPhase.ResolvePressure:
                {
                    _decisionPressureTarget = ResolvePressureTarget(_decisionBaseCenter, now);
                    float sensorMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - _decisionSensorStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    if (sensorMs > 0f)
                    {
                        DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sensor_update_ms", sensorMs);
                        DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("targeting_ms", sensorMs);
                    }
                    _decisionPhase = DecisionPhase.DispatchIntercept;
                    break;
                }
                case DecisionPhase.DispatchIntercept:
                    DispatchAirIntercept(_decisionBaseCenter, _decisionAirEnemy, _decisionGroundEnemy, _decisionPressureTarget);
                    _decisionPhase = DecisionPhase.DispatchBombers;
                    break;
                case DecisionPhase.DispatchBombers:
                    DispatchStrategicBombers(_decisionBaseCenter, _decisionPressureTarget);
                    _decisionPhase = DecisionPhase.DispatchTransport;
                    break;
                case DecisionPhase.DispatchTransport:
                    DispatchAirTransport(_decisionBaseCenter, _decisionGroundEnemy, _decisionPressureTarget, now);
                    _decisionPhase = DecisionPhase.Finish;
                    break;
                case DecisionPhase.Finish:
                {
                    DiagnosticoDesempenhoJogo.DefinirContadorMetrica(
                        "active_air_wings",
                        (_activeAirUnitsBuffer.Count + _activeAirTransportBuffer.Count + _activeBombersBuffer.Count) > 0 ? 1 : 0);
                    float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - _decisionTickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    if (elapsedMs > 0f)
                    {
                        DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("air_unit_update_ms", elapsedMs);
                    }
                    _decisionPhase = DecisionPhase.Idle;
                    return true;
                }
            }

            return false;
        }

        private float ResolveDecisionDelay()
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 1.05f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 1.55f;
                case EstadoCargaIA.EmCombate:
                    return 1.20f;
                default:
                    return 1.05f;
            }
        }

        private void DispatchAirIntercept(Vector3 baseCenter, Transform airEnemy, Transform fallbackEnemy, Vector3 pressureTarget)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.AirIntercept);
            if (!HasUnits(squad))
            {
                return;
            }

            bool includeBombers = false; // Bombardeiros são gerenciados separadamente em DispatchStrategicBombers
            if (!TrySelectActiveAirUnits(squad.Units, _activeAirUnitsBuffer, false, includeBombers))
            {
                return;
            }

            if (TryDispatchEconomicAirRaids(baseCenter, _activeAirUnitsBuffer))
            {
                return;
            }

            Transform target = airEnemy != null ? airEnemy : fallbackEnemy;
            if (target != null)
            {
                // Com alvo confirmado, a patrulha de fallback nao participa da
                // ordem. Evite uma amostragem de terreno cara nesse caminho.
                QueueAttack("air_intercept", _activeAirUnitsBuffer, target, target.position, 87, 2.9f);
            }
            else
            {
                Vector3 patrol = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 120f, 330f, 20);
                Vector3 fallback = pressureTarget != Vector3.zero ? pressureTarget : patrol;
                QueueAttack("air_intercept", _activeAirUnitsBuffer, null, fallback + Vector3.up * 20f, 80, 3.1f);
            }
        }

        private bool TryDispatchEconomicAirRaids(Vector3 baseCenter, List<GameObject> source)
        {
            IA_BrainMaster brain = _context != null ? _context.Brain : null;
            // Threshold reduzido para 3 avioes para permitir raids mais cedo
            if (brain == null || brain.StrategicPhase < IA_StrategicPhase.Expansao || source == null || source.Count < 2)
            {
                return false;
            }

            int targetCount = _context.WorldState.FillEnemyStrategicTargets(_strategicTargetsBuffer, baseCenter, 6);
            if (targetCount < 2)
            {
                return false;
            }

            _airRaidA.Clear();
            _airRaidB.Clear();
            _airRaidC.Clear();

            int groups = Mathf.Min(3, Mathf.Min(targetCount, Mathf.Max(2, source.Count / 2)));
            for (int i = 0; i < source.Count; i++)
            {
                GameObject unit = source[i];
                if (unit == null)
                {
                    continue;
                }

                int bucket = i % groups;
                if (bucket == 0)
                {
                    _airRaidA.Add(unit);
                }
                else if (bucket == 1)
                {
                    _airRaidB.Add(unit);
                }
                else
                {
                    _airRaidC.Add(unit);
                }
            }

            int launched = 0;
            launched += QueueEconomicRaidIfReady("oil_or_port", _airRaidA, _strategicTargetsBuffer[0], 92, 5.0f) ? 1 : 0;
            launched += QueueEconomicRaidIfReady("air_or_shipyard", _airRaidB, _strategicTargetsBuffer[1], 89, 5.4f) ? 1 : 0;
            if (groups >= 3 && _strategicTargetsBuffer.Count > 2)
            {
                launched += QueueEconomicRaidIfReady("third_axis", _airRaidC, _strategicTargetsBuffer[2], 86, 5.8f) ? 1 : 0;
            }

            if (launched > 0)
            {
                brain.ReportStrategicTarget("ataque aereo multi-alvo x" + launched + " alvo0=" + _strategicTargetsBuffer[0].Kind);
            }

            return launched > 0;
        }

        private bool QueueEconomicRaidIfReady(string key, List<GameObject> units, IA_StrategicTargetData target, int priority, float cooldown)
        {
            if (units == null || units.Count == 0 || target == null || target.Transform == null)
            {
                return false;
            }

            QueueAttack("air_raid_" + key + "_" + target.Kind, units, target.Transform, target.Position + Vector3.up * 20f, priority, cooldown);
            return true;
        }

        private Vector3 ResolvePressureTarget(Vector3 baseCenter, float now)
        {
            Vector3 hiddenEnemyAnchor;
            if (now >= ForcedAirStrikeStartSeconds && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out hiddenEnemyAnchor))
            {
                return hiddenEnemyAnchor;
            }

            _context.WorldState.FillEnemyMemory(_enemyMemoryBuffer, 240f);
            IA_EnemyObservation best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < _enemyMemoryBuffer.Count; i++)
            {
                IA_EnemyObservation obs = _enemyMemoryBuffer[i];
                if (obs == null)
                {
                    continue;
                }

                float age = Mathf.Max(0f, now - obs.LastSeenTime);
                float score = obs.ThreatScore + (obs.IsStructure ? 35f : 0f) - (age * 0.25f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = obs;
                }
            }

            if (best != null)
            {
                return best.Position;
            }

            return _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 260f, 900f, 26);
        }

        private void DispatchAirTransport(Vector3 baseCenter, Transform target, Vector3 pressureTarget, float now)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.AirTacticalTransport);
            if (!HasUnits(squad))
            {
                return;
            }

            if (!TrySelectActiveAirUnits(squad.Units, _activeAirTransportBuffer, true, true))
            {
                return;
            }

            IA_TransportPlan plan = _context != null ? _context.TransportPlan : null;
            bool canProjectDrop = plan == null || plan.HasLandRoute || plan.Ready;

            Vector3 insertion;
            if (target != null)
            {
                insertion = target.position + Vector3.up * 6f;
            }
            else if (now >= ForcedAirStrikeStartSeconds && pressureTarget != Vector3.zero)
            {
                insertion = pressureTarget + Vector3.up * 6f;
            }
            else if (!canProjectDrop)
            {
                insertion = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 40f, 120f, 18);
            }
            else
            {
                insertion = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.City, 90f, 260f, 22);
            }

            QueueMove("air_transport", _activeAirTransportBuffer, insertion, 78, 4.2f);

            if (!canProjectDrop)
            {
                return;
            }

            // Trigger tactical drop behavior when close enough.
            for (int i = 0; i < _activeAirTransportBuffer.Count; i++)
            {
                GameObject unit = _activeAirTransportBuffer[i];
                if (unit == null)
                {
                    continue;
                }

                float dSquared = (unit.transform.position - insertion).sqrMagnitude;
                if (dSquared <= 784f)
                {
                    IA_AbilityOrderData ability = new IA_AbilityOrderData
                    {
                        Caster = unit,
                        AbilityKey = "OrdemPousoOuDesembarque",
                        TargetPosition = insertion,
                        Target = target
                    };

                    IA_CommandRequest request = IA_CommandFactory.Create(
                        IA_CommandType.Ability,
                        "IA_AirDirector",
                        "air",
                        "desembarque tatico",
                        83,
                        "air",
                        "ability:air_transport_drop",
                        6f,
                        ability);

                    string reason;
                    _context.CommandQueue.Enqueue(request, Time.time, out reason);
                    break;
                }
            }
        }

        private Transform GetVisibleAirEnemy()
        {
            float bestSquared = float.MaxValue;
            Transform selected = null;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            for (int i = 0; i < _context.WorldState.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _context.WorldState.VisibleEnemies[i];
                if (obs == null || obs.Transform == null || obs.Domain != IA_Domain.Air)
                {
                    continue;
                }

                float distanceSquared = (baseCenter - obs.Position).sqrMagnitude;
                if (distanceSquared < bestSquared)
                {
                    bestSquared = distanceSquared;
                    selected = obs.Transform;
                }
            }

            return selected;
        }

        private void QueueMove(string key, List<GameObject> units, Vector3 destination, int priority, float cooldown)
        {
            IA_MoveOrderData payload = new IA_MoveOrderData
            {
                Units = CloneUnits(units),
                Destination = destination
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Move,
                "IA_AirDirector",
                "air",
                "reposicionamento aereo",
                priority,
                "air",
                "move:" + key,
                cooldown,
                payload);

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }

        private void QueueAttack(string key, List<GameObject> units, Transform target, Vector3 targetPosition, int priority, float cooldown)
        {
            IA_AttackOrderData payload = new IA_AttackOrderData
            {
                Units = CloneUnits(units),
                Target = target,
                TargetPosition = targetPosition
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Attack,
                "IA_AirDirector",
                "air",
                "ataque aereo",
                priority,
                "air",
                "attack:" + key,
                cooldown,
                payload);

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }

        private static List<GameObject> CloneUnits(List<GameObject> source)
        {
            var output = source != null ? new List<GameObject>(source.Count) : new List<GameObject>();
            if (source == null)
            {
                return output;
            }

            for (int i = 0; i < source.Count; i++)
            {
                GameObject unit = source[i];
                if (unit != null)
                {
                    output.Add(unit);
                }
            }

            return output;
        }

        private static bool HasUnits(IA_SquadData squad)
        {
            return squad != null && squad.Units != null && squad.Units.Count > 0;
        }

        private bool TrySelectActiveAirUnits(List<GameObject> source, List<GameObject> destination, bool transportWing, bool includeBombers)
        {
            destination.Clear();
            if (source == null || source.Count == 0)
            {
                return false;
            }

            RebuildReadyAircraftReserve();
            IA_BattleGovernorDecision decision = _context != null ? _context.BattleDecision : null;
            int hardLimit = decision != null
                ? Mathf.Max(1, transportWing ? Mathf.Max(1, decision.MaxAirAttackers / 2) : decision.MaxAirAttackers)
                : source.Count;
            int limit = transportWing
                ? Mathf.Min(2, hardLimit)
                : ResolveAirPackageSize(Mathf.Min(hardLimit, source.Count));
            IA_BrainMaster brain = _context != null ? _context.Brain : null;
            bool pressurePhase = brain != null && brain.StrategicPhase >= IA_StrategicPhase.PressaoEconomica;
            if (!transportWing && pressurePhase && source.Count >= 6)
            {
                limit = Mathf.Min(Mathf.Max(limit, 6), Mathf.Min(hardLimit, source.Count));
            }

            for (int i = 0; i < source.Count && destination.Count < limit; i++)
            {
                GameObject unit = source[i];
                if (unit == null)
                {
                    continue;
                }

                if (!includeBombers && IsBomber(unit))
                {
                    continue;
                }

                ControleAviao aircraft = unit.GetComponent<ControleAviao>();
                if (aircraft != null && !TrySpendReadyAircraftForLaunch(aircraft))
                {
                    continue;
                }

                destination.Add(unit);
            }

            return destination.Count > 0;
        }

        private void RebuildReadyAircraftReserve()
        {
            if (_readyAircraftReserveValid)
            {
                return;
            }

            _readyAircraftByAirport.Clear();
            _readyAircraftReserveValid = true;
            if (_context == null || _context.WorldState == null || _context.WorldState.OwnUnits == null)
            {
                return;
            }

            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                ControleAviao aircraft = unit.GetComponent<ControleAviao>();
                if (aircraft == null
                    || aircraft.aeroportoOrigem == null
                    || aircraft.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    continue;
                }

                int count;
                _readyAircraftByAirport.TryGetValue(aircraft.aeroportoOrigem, out count);
                _readyAircraftByAirport[aircraft.aeroportoOrigem] = count + 1;
            }
        }

        private bool TrySpendReadyAircraftForLaunch(ControleAviao aircraft)
        {
            if (aircraft == null
                || aircraft.aeroportoOrigem == null
                || aircraft.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                return true;
            }

            int readyCount;
            if (!_readyAircraftByAirport.TryGetValue(aircraft.aeroportoOrigem, out readyCount))
            {
                return true;
            }

            // Permite lancar mesmo com 1 aviao disponivel — reserva 0 no patio
            if (readyCount <= 0)
            {
                return false;
            }

            _readyAircraftByAirport[aircraft.aeroportoOrigem] = readyCount - 1;
            return true;
        }

        private static int ResolveAirPackageSize(int max)
        {
            if (max <= 1)
            {
                return max;
            }

            if (max >= 6)
            {
                return Mathf.Min(max, Random.Range(4, 7));
            }

            if (Random.value < 0.45f)
            {
                return Random.Range(2, Mathf.Min(4, max) + 1);
            }

            return 1;
        }

        private static bool IsBomber(GameObject unit)
        {
            if (unit == null)
            {
                return false;
            }

            string normalizedName = IA_Text.Normalize(unit.name);
            return unit.GetComponent<AviaoBombardeiro>() != null
                   || normalizedName.Contains("b260")
                   || normalizedName.Contains("bomb");
        }

        private void DispatchStrategicBombers(Vector3 baseCenter, Vector3 pressureTarget)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.AirIntercept);
            if (!HasUnits(squad))
            {
                return;
            }

            _activeBombersBuffer.Clear();
            for (int i = 0; i < squad.Units.Count; i++)
            {
                GameObject unit = squad.Units[i];
                if (unit == null) continue;

                if (IsBomber(unit))
                {
                    ControleAviao aircraft = unit.GetComponent<ControleAviao>();
                    if (aircraft != null && (aircraft.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio || aircraft.estadoAtual == ControleAviao.EstadoAviao.EmMissao))
                    {
                        _activeBombersBuffer.Add(unit);
                    }
                }
            }

            if (_activeBombersBuffer.Count == 0)
            {
                return;
            }

            Transform target = FindBestBombingTarget(baseCenter);
            if (target != null)
            {
                QueueAttack("strategic_bombing", _activeBombersBuffer, target, target.position, 95, 8.0f);
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("units_committed_air", _activeBombersBuffer.Count);
            }
            else
            {
                if (pressureTarget != Vector3.zero)
                {
                    QueueAttack("strategic_bombing_fallback", _activeBombersBuffer, null, pressureTarget + Vector3.up * 20f, 75, 10.0f);
                    DiagnosticoDesempenhoJogo.DefinirContadorMetrica("units_committed_air", _activeBombersBuffer.Count);
                }
            }
        }

        private Transform FindBestBombingTarget(Vector3 baseCenter)
        {
            _context.WorldState.FillEnemyMemory(_enemyMemoryBuffer, 300f);
            Transform best = null;
            float bestPriority = float.MinValue;

            for (int i = 0; i < _enemyMemoryBuffer.Count; i++)
            {
                IA_EnemyObservation obs = _enemyMemoryBuffer[i];
                if (obs == null || obs.Transform == null || !obs.IsStructure)
                {
                    continue;
                }

                string name = IA_Text.Normalize(obs.UnitName);
                float priority = 0f;

                if (name.Contains("prefeitura") || name.Contains("capital") || name.Contains("governo") || name.Contains("cityhall"))
                {
                    priority = 500f;
                }
                else if (name.Contains("fabrica") || name.Contains("construtor") || name.Contains("factory") || name.Contains("centro de construcao") || name.Contains("centro_construcao"))
                {
                    priority = 400f;
                }
                else if (name.Contains("usina") || name.Contains("gerador") || name.Contains("solar") || name.Contains("power"))
                {
                    priority = 300f;
                }
                else if (name.Contains("aeroporto") || name.Contains("airport"))
                {
                    priority = 250f;
                }

                priority -= Vector3.Distance(baseCenter, obs.Position) * 0.05f;

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = obs.Transform;
                }
            }

            return best;
        }
    }
}
