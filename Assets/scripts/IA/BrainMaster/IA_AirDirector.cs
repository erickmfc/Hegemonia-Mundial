using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_AirDirector : IIAUpdateModule
    {
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
            get { return 1.25f; }
        }

        public float BudgetMs
        {
            get { return 0.35f; }
        }

        public void Tick(float now, float deltaTime)
        {
            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _activeAirUnitsBuffer.Clear();
            _activeAirTransportBuffer.Clear();
            if (_context.Brain != null && _context.Brain.IsBootstrapActive && (int)_context.Brain.BootstrapStage < (int)IA_BrainMaster.IA_BootstrapStage.ProduceAircraft)
            {
                return;
            }

            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + ResolveDecisionDelay();
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            long sensorStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Transform airEnemy = GetVisibleAirEnemy();
            Transform groundEnemy = _context.WorldState.GetNearestVisibleEnemy(baseCenter, IA_Domain.Land);
            Vector3 pressureTarget = ResolvePressureTarget(baseCenter, now);
            float sensorMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - sensorStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (sensorMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sensor_update_ms", sensorMs);
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("targeting_ms", sensorMs);
            }

            DispatchAirIntercept(baseCenter, airEnemy, groundEnemy, pressureTarget);
            DispatchStrategicBombers(baseCenter, now);
            DispatchAirTransport(baseCenter, groundEnemy, pressureTarget, now);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica(
                "active_air_wings",
                (_activeAirUnitsBuffer.Count + _activeAirTransportBuffer.Count + _activeBombersBuffer.Count) > 0 ? 1 : 0);
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("air_unit_update_ms", elapsedMs);
            }
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
            Vector3 patrol = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 120f, 330f, 20);
            if (target != null)
            {
                QueueAttack("air_intercept", _activeAirUnitsBuffer, target, patrol, 87, 2.9f);
            }
            else
            {
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

            Vector3 insertion = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.City, 90f, 260f, 22);
            if (!canProjectDrop)
            {
                insertion = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 40f, 120f, 18);
            }
            else if (target != null)
            {
                insertion = target.position + Vector3.up * 6f;
            }
            else if (now >= ForcedAirStrikeStartSeconds && pressureTarget != Vector3.zero)
            {
                insertion = pressureTarget + Vector3.up * 6f;
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

                float d = Vector3.Distance(unit.transform.position, insertion);
                if (d <= 28f)
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
            float best = float.MaxValue;
            Transform selected = null;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            for (int i = 0; i < _context.WorldState.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _context.WorldState.VisibleEnemies[i];
                if (obs == null || obs.Transform == null || obs.Domain != IA_Domain.Air)
                {
                    continue;
                }

                float distance = Vector3.Distance(baseCenter, obs.Position);
                if (distance < best)
                {
                    best = distance;
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
            _readyAircraftByAirport.Clear();
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

        private void DispatchStrategicBombers(Vector3 baseCenter, float now)
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
                Vector3 fallbackTarget = ResolvePressureTarget(baseCenter, now);
                if (fallbackTarget != Vector3.zero)
                {
                    QueueAttack("strategic_bombing_fallback", _activeBombersBuffer, null, fallbackTarget + Vector3.up * 20f, 75, 10.0f);
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
