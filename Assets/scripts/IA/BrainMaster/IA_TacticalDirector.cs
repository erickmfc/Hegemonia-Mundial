using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_TacticalDirector : IIAUpdateModule
    {
        private const float ForcedAssaultStartSeconds = 60f;
        private readonly IA_Context _context;
        private readonly List<GameObject> _loadedAmphibiousBuffer = new List<GameObject>(16);
        private readonly List<GameObject> _emptyAmphibiousBuffer = new List<GameObject>(16);
        private readonly List<GameObject> _emptyGroundTransportBuffer = new List<GameObject>(16);
        private readonly List<GameObject> _assaultUnitsBuffer = new List<GameObject>(48);
        private readonly List<GameObject> _activeGroundUnitsBuffer = new List<GameObject>(32);
        private readonly List<GameObject> _boardingUnitsBuffer = new List<GameObject>(24);
        private readonly List<IA_EnemyObservation> _enemyMemoryBuffer = new List<IA_EnemyObservation>(64);
        private float _nextDecisionTime;
        private float _nextAssaultWaveTime;
        private float _lastEnemySeenTime = -999f;
        private float _exploreAngleDeg;
        private Vector3 _lastStrategicObjective;
        private int _landAttackersCommittedThisTick;
        private int _landPointsCommittedThisTick;
        private int _activeLandFrontsThisTick;

        public IA_TacticalDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_TacticalDirector"; }
        }

        public float Interval
        {
            get { return 1.00f; }
        }

        public float BudgetMs
        {
            get { return 0.45f; }
        }

        public void Tick(float now, float deltaTime)
        {
            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_context.Brain != null && _context.Brain.IsBootstrapActive)
            {
                return;
            }

            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + ResolveDecisionDelay();
            _landAttackersCommittedThisTick = 0;
            _landPointsCommittedThisTick = 0;
            _activeLandFrontsThisTick = 0;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            long sensorStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Transform priorityEnemy = _context.WorldState.GetNearestVisibleEnemy(baseCenter, IA_Domain.Land);
            if (priorityEnemy != null)
            {
                _lastEnemySeenTime = now;
            }
            Vector3 strategicObjective = ResolveStrategicObjective(baseCenter, priorityEnemy, now);
            float sensorMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - sensorStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (sensorMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sensor_update_ms", sensorMs);
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("targeting_ms", sensorMs);
            }
            Vector3 hiddenEnemyAnchor;
            if (priorityEnemy == null
                && now >= ForcedAssaultStartSeconds
                && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out hiddenEnemyAnchor))
            {
                strategicObjective = hiddenEnemyAnchor;
                _lastStrategicObjective = strategicObjective;
            }
            Vector3 threatSector = _context.ThreatAnalyzer.GetHighestThreatSectorCenter();

            DispatchRecon(baseCenter, priorityEnemy, strategicObjective);
            DispatchLocalDefense(baseCenter, threatSector, priorityEnemy);
            DispatchBorderPatrol(baseCenter, strategicObjective);
            DispatchArmoredAssault(baseCenter, priorityEnemy, strategicObjective);
            DispatchAmphibious(baseCenter, priorityEnemy, strategicObjective);
            DispatchGroundLogistics(baseCenter);
            DispatchOffensiveWave(now, baseCenter, priorityEnemy, strategicObjective);

            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("active_land_fronts", _activeLandFrontsThisTick);
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("land_unit_update_ms", elapsedMs);
            }
        }

        private float ResolveDecisionDelay()
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 0.60f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 1.40f;
                case EstadoCargaIA.EmCombate:
                    return 0.95f;
                default:
                    return 0.60f;
            }
        }

        private void DispatchRecon(Vector3 baseCenter, Transform visibleEnemy, Vector3 strategicObjective)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.Recon);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 target = visibleEnemy != null
                ? visibleEnemy.position
                : BlendObjective(baseCenter, strategicObjective, 0.60f, 80f, 220f);

            QueueMove("recon", squad.Units, target, 74, 3.5f);
        }

        private void DispatchLocalDefense(Vector3 baseCenter, Vector3 threatSector, Transform visibleEnemy)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.LocalDefense);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 target = threatSector != Vector3.zero
                ? threatSector
                : _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.City, 20f, 90f, 20);

            if (visibleEnemy != null && Vector3.Distance(baseCenter, visibleEnemy.position) < 180f)
            {
                QueueAttack("defense_local", squad.Units, visibleEnemy, target, 92, 2.8f);
                return;
            }

            QueueMove("defense_local", squad.Units, target, 82, 3.8f);
        }

        private void DispatchBorderPatrol(Vector3 baseCenter, Vector3 strategicObjective)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.BorderPatrol);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 pointA = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 120f, 260f, 24);
            Vector3 pointB = BlendObjective(baseCenter, strategicObjective, 0.72f, 140f, 280f);
            IA_PatrolOrderData payload = new IA_PatrolOrderData
            {
                Units = CloneUnits(squad.Units),
                PointA = pointA,
                PointB = pointB
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Patrol,
                Priority = 68,
                DedupKey = "patrol:border",
                CooldownSeconds = 5f,
                Payload = payload
            };

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }

        private void DispatchArmoredAssault(Vector3 baseCenter, Transform visibleEnemy, Vector3 strategicObjective)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.ArmoredAssault);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 openApproach = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 150f, 320f, 26);
            if (!TrySelectActiveGroundUnits(squad.Units, _activeGroundUnitsBuffer, 10))
            {
                return;
            }

            if (visibleEnemy != null)
            {
                QueueAttack("armored", _activeGroundUnitsBuffer, visibleEnemy, openApproach, 89, 3.2f);
                _activeLandFrontsThisTick = Mathf.Max(_activeLandFrontsThisTick, 1);
            }
            else
            {
                Vector3 pressurePoint = CanProjectGroundOffense()
                    ? BlendObjective(baseCenter, strategicObjective, 0.78f, 210f, 520f)
                    : _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 40f, 140f, 18);
                QueueMove("armored", squad.Units, pressurePoint, 84, 3.6f);
            }
        }

        private void DispatchAmphibious(Vector3 baseCenter, Transform visibleEnemy, Vector3 strategicObjective)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.Amphibious);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 coast = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Coast, 120f, 360f, 26);
            if (coast == Vector3.zero)
            {
                coast = baseCenter;
            }

            Vector3 loadRally = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 35f, 100f, 18);
            if (loadRally == Vector3.zero)
            {
                loadRally = baseCenter;
            }

            Vector3 landingPoint = ResolveEnemyLandingPoint(baseCenter, strategicObjective);
            SplitAmphibiousUnits(squad.Units, _loadedAmphibiousBuffer, _emptyAmphibiousBuffer);

            if (_emptyAmphibiousBuffer.Count > 0)
            {
                QueueMove("amphibious_load", _emptyAmphibiousBuffer, loadRally, 76, 4.2f);
                CollectBoardingUnits(loadRally, Mathf.Clamp(_emptyAmphibiousBuffer.Count * 6, 6, 18), _boardingUnitsBuffer);
                if (_boardingUnitsBuffer.Count > 0)
                {
                    QueueMove("amphibious_boarding_units", _boardingUnitsBuffer, loadRally, 79, 3.6f);
                }

                for (int i = 0; i < _emptyAmphibiousBuffer.Count && i < 3; i++)
                {
                    GameObject transport = _emptyAmphibiousBuffer[i];
                    if (transport == null || DistanceFlat(transport.transform.position, loadRally) > 95f)
                    {
                        continue;
                    }

                    QueueAbility(
                        "amphibious_board_" + transport.GetInstanceID(),
                        transport,
                        "IniciarEmbarque",
                        transport.transform.position,
                        null,
                        82,
                        6f);
                }
            }

            if (_loadedAmphibiousBuffer.Count == 0)
            {
                return;
            }

            if (landingPoint == Vector3.zero)
            {
                landingPoint = Vector3.Lerp(coast, strategicObjective, 0.65f);
            }

            if (landingPoint == Vector3.zero || DistanceFlat(landingPoint, baseCenter) < 120f)
            {
                landingPoint = strategicObjective != Vector3.zero ? strategicObjective : coast;
            }

            QueueMove("amphibious_landing", _loadedAmphibiousBuffer, landingPoint, 84, 4.0f);

            for (int i = 0; i < _loadedAmphibiousBuffer.Count && i < 3; i++)
            {
                GameObject transport = _loadedAmphibiousBuffer[i];
                if (transport == null || DistanceFlat(transport.transform.position, landingPoint) > 115f)
                {
                    continue;
                }

                QueueAbility(
                    "amphibious_unload_" + transport.GetInstanceID(),
                    transport,
                    "DesembarcarTudo",
                    landingPoint,
                    visibleEnemy,
                    88,
                    8f);
                _activeLandFrontsThisTick = Mathf.Max(_activeLandFrontsThisTick, 1);
            }
        }

        private void DispatchGroundLogistics(Vector3 baseCenter)
        {
            _emptyGroundTransportBuffer.Clear();
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                TransporteTerrestre transport = unit.GetComponent<TransporteTerrestre>();
                if (transport == null)
                {
                    continue;
                }

                if (transport.QuantidadePassageiros <= 0)
                {
                    _emptyGroundTransportBuffer.Add(unit);
                }
            }

            if (_emptyGroundTransportBuffer.Count == 0)
            {
                return;
            }

            Vector3 rally = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 35f, 110f, 18);
            QueueMove("logistics_idle", _emptyGroundTransportBuffer, rally, 64, 5.6f);

            if (_context.Brain == null || _context.Brain.IntegrationMode != IA_BrainMaster.IA_IntegrationMode.Full)
            {
                return;
            }

            for (int i = 0; i < _emptyGroundTransportBuffer.Count && i < 2; i++)
            {
                GameObject transport = _emptyGroundTransportBuffer[i];
                if (transport == null || Vector3.Distance(transport.transform.position, baseCenter) > 80f)
                {
                    continue;
                }

                QueueAbility(
                    "logistics_load_" + transport.GetInstanceID(),
                    transport,
                    "TentarEmbarcar",
                    transport.transform.position,
                    null,
                    66,
                    8f);
            }
        }

        private void DispatchOffensiveWave(float now, Vector3 baseCenter, Transform visibleEnemy, Vector3 strategicObjective)
        {
            if (now < _nextAssaultWaveTime)
            {
                return;
            }

            IA_BattleGovernorDecision decision = GetBattleDecision();
            if (decision != null && decision.MaxActiveFronts <= 0)
            {
                return;
            }

            if (!CanProjectGroundOffense())
            {
                return;
            }

            bool forcedAssault = now >= ForcedAssaultStartSeconds;
            Vector3 forcedTarget = strategicObjective;
            bool hasForcedTarget = forcedAssault && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out forcedTarget);

            int maxAttackers = decision != null
                ? Mathf.Max(4, decision.MaxLandAttackers)
                : (forcedAssault ? 40 : 24);
            List<GameObject> assaultUnits = CollectGroundAssaultUnits(Mathf.Min(forcedAssault ? 40 : 24, maxAttackers));
            if (assaultUnits.Count < (forcedAssault ? 2 : 3))
            {
                return;
            }

            if (visibleEnemy != null)
            {
                QueueAttack("assault_wave", assaultUnits, visibleEnemy, strategicObjective, 93, 4.2f);
                _nextAssaultWaveTime = now + 4.5f;
                _activeLandFrontsThisTick = Mathf.Max(_activeLandFrontsThisTick, 1);
                return;
            }

            Vector3 fallbackTarget = hasForcedTarget ? forcedTarget : strategicObjective;
            if (DistanceFlat(baseCenter, fallbackTarget) < 170f)
            {
                fallbackTarget = ResolveExplorationObjective(baseCenter, now, true);
            }

            QueueMove("assault_wave", assaultUnits, fallbackTarget, forcedAssault ? 96 : 90, forcedAssault ? 2.2f : 3.2f);
            _nextAssaultWaveTime = now + (forcedAssault ? 3.0f : 5.5f);
            _activeLandFrontsThisTick = Mathf.Max(_activeLandFrontsThisTick, 1);
        }

        private Vector3 ResolveStrategicObjective(Vector3 baseCenter, Transform visibleEnemy, float now)
        {
            if (visibleEnemy != null)
            {
                _lastStrategicObjective = visibleEnemy.position;
                return _lastStrategicObjective;
            }

            _context.WorldState.FillEnemyMemory(_enemyMemoryBuffer, 220f);
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
                float distance = DistanceFlat(baseCenter, obs.Position);
                float score = obs.ThreatScore
                              + (obs.IsStructure ? 45f : 0f)
                              + Mathf.Clamp(distance * 0.04f, 0f, 36f)
                              - Mathf.Clamp(age * 0.22f, 0f, 42f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = obs;
                }
            }

            if (best != null)
            {
                _lastStrategicObjective = best.Position;
                return _lastStrategicObjective;
            }

            Vector3 longRange = FindLongRangeObjective(baseCenter, now);
            if (DistanceFlat(baseCenter, longRange) >= 120f)
            {
                _lastStrategicObjective = longRange;
            }

            Vector3 resolved = _lastStrategicObjective != Vector3.zero ? _lastStrategicObjective : longRange;
            if (DistanceFlat(baseCenter, resolved) < 180f || now - _lastEnemySeenTime > 18f)
            {
                resolved = ResolveExplorationObjective(baseCenter, now, true);
                _lastStrategicObjective = resolved;
            }

            return resolved;
        }

        private Vector3 FindLongRangeObjective(Vector3 baseCenter, float now)
        {
            Vector3 best = baseCenter;
            float bestScore = float.MinValue;
            float phase = (now * 21f) % 360f;
            const int slices = 14;

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = Mathf.Lerp(360f, 1250f, ring / 2f);
                for (int i = 0; i < slices; i++)
                {
                    float angle = (phase + ((360f / slices) * i)) * Mathf.Deg2Rad;
                    Vector3 probe = baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                    if (cell == null || cell.Terrain == IA_TerrainType.Water || !cell.BuildableLand)
                    {
                        continue;
                    }

                    float score = DistanceFlat(baseCenter, cell.Center);
                    if (cell.Terrain == IA_TerrainType.Open)
                    {
                        score += 22f;
                    }
                    else if (cell.Terrain == IA_TerrainType.City)
                    {
                        score += 11f;
                    }
                    else if (cell.Terrain == IA_TerrainType.Choke)
                    {
                        score -= 6f;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cell.Center;
                    }
                }
            }

            if (bestScore == float.MinValue)
            {
                best = ResolveExplorationObjective(baseCenter, now, true);
            }

            return best;
        }

        private List<GameObject> CollectGroundAssaultUnits(int limit)
        {
            _assaultUnitsBuffer.Clear();
            int max = Mathf.Clamp(limit, 6, 48);
            int maxPoints = _context != null && _context.EngagementBudget != null
                ? Mathf.Max(4, _context.EngagementBudget.LandPoints)
                : max * 2;
            int remainingUnits = Mathf.Max(0, max - _landAttackersCommittedThisTick);
            int remainingPoints = Mathf.Max(0, maxPoints - _landPointsCommittedThisTick);
            int initialPoints = remainingPoints;
            if (remainingUnits <= 0 || remainingPoints <= 0)
            {
                return _assaultUnitsBuffer;
            }

            for (int i = 0; i < _context.WorldState.OwnCombatUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnCombatUnits[i];
                if (unit == null || !unit.activeInHierarchy)
                {
                    continue;
                }

                if (unit.GetComponent<ControleNavioRealista>() != null
                    || unit.GetComponent<ControleSubmarino>() != null
                    || unit.GetComponent<ControleAviao>() != null
                    || unit.GetComponent<ControleAviaoCaca>() != null
                    || unit.GetComponent<Helicoptero>() != null)
                {
                    continue;
                }

                string n = IA_Text.Normalize(unit.name);
                bool groundTransport = unit.GetComponent<TransporteTerrestre>() != null
                                       || n.Contains("truck")
                                       || n.Contains("caminhao")
                                       || (n.Contains("transporte")
                                           && !n.Contains("aereo")
                                           && !n.Contains("air")
                                           && !n.Contains("heli")
                                           && !n.Contains("ray")
                                           && !n.Contains("vans"));
                if (groundTransport)
                {
                    continue;
                }

                int cost = IA_BattleGovernorUtils.GetEngagementCost(unit);
                if (cost > remainingPoints)
                {
                    continue;
                }

                _assaultUnitsBuffer.Add(unit);
                remainingPoints -= cost;
                if (_assaultUnitsBuffer.Count >= remainingUnits)
                {
                    break;
                }
            }

            _landAttackersCommittedThisTick += _assaultUnitsBuffer.Count;
            _landPointsCommittedThisTick += Mathf.Max(0, initialPoints - remainingPoints);
            return _assaultUnitsBuffer;
        }

        private IA_BattleGovernorDecision GetBattleDecision()
        {
            return _context != null ? _context.BattleDecision : null;
        }

        private bool CanProjectGroundOffense()
        {
            IA_TransportPlan plan = _context != null ? _context.TransportPlan : null;
            return plan == null || plan.HasLandRoute || plan.Ready;
        }

        private Vector3 ResolveEnemyLandingPoint(Vector3 baseCenter, Vector3 strategicObjective)
        {
            Vector3 anchor = strategicObjective;
            if (anchor == Vector3.zero || DistanceFlat(baseCenter, anchor) < 180f)
            {
                Vector3 enemyAnchor;
                if (_context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out enemyAnchor))
                {
                    anchor = enemyAnchor;
                }
            }

            if (anchor == Vector3.zero)
            {
                return Vector3.zero;
            }

            Vector3 coast = _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Coast, 40f, 320f, 32);
            if (coast == Vector3.zero || DistanceFlat(coast, baseCenter) < 140f)
            {
                coast = _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Land, 60f, 280f, 28);
            }

            return coast;
        }

        private void CollectBoardingUnits(Vector3 loadRally, int limit, List<GameObject> output)
        {
            output.Clear();
            if (_context == null || _context.WorldState == null || _context.WorldState.OwnCombatUnits == null || limit <= 0)
            {
                return;
            }

            for (int i = 0; i < _context.WorldState.OwnCombatUnits.Count && output.Count < limit; i++)
            {
                GameObject unit = _context.WorldState.OwnCombatUnits[i];
                if (unit == null || !unit.activeInHierarchy)
                {
                    continue;
                }

                if (DistanceFlat(unit.transform.position, loadRally) > 650f || !IsBoardingCandidate(unit))
                {
                    continue;
                }

                output.Add(unit);
            }
        }

        private static bool IsBoardingCandidate(GameObject unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.GetComponent<HovercraftTransporte>() != null
                || unit.GetComponent<TransporteTerrestre>() != null
                || unit.GetComponent<TransporteAnfibio>() != null
                || unit.GetComponent<ControleNavioRealista>() != null
                || unit.GetComponent<ControleSubmarino>() != null
                || unit.GetComponent<ControleAviao>() != null
                || unit.GetComponent<ControleAviaoCaca>() != null
                || unit.GetComponent<Helicoptero>() != null)
            {
                return false;
            }

            string n = IA_Text.Normalize(unit.name);
            if (n.Contains("transporte")
                || n.Contains("truck")
                || n.Contains("caminhao")
                || n.Contains("hover")
                || n.Contains("houver"))
            {
                return false;
            }

            if (n.Contains("soldado")
                || n.Contains("infant")
                || n.Contains("rifle")
                || n.Contains("sniper")
                || n.Contains("tank")
                || n.Contains("mbt")
                || n.Contains("arthur")
                || n.Contains("south")
                || n.Contains("c1")
                || n.Contains("hack")
                || n.Contains("artilh"))
            {
                return true;
            }

            return unit.GetComponent<NavMeshAgent>() != null && unit.GetComponent<SistemaDeDanos>() != null;
        }

        private bool TrySelectActiveGroundUnits(List<GameObject> source, List<GameObject> destination, int requestedLimit)
        {
            destination.Clear();
            if (source == null || source.Count == 0)
            {
                return false;
            }

            IA_BattleGovernorDecision decision = GetBattleDecision();
            int limit = decision != null
                ? Mathf.Min(requestedLimit, Mathf.Max(1, decision.MaxLandAttackers - _landAttackersCommittedThisTick))
                : requestedLimit;
            int remainingPoints = _context != null && _context.EngagementBudget != null
                ? Mathf.Max(0, _context.EngagementBudget.LandPoints - _landPointsCommittedThisTick)
                : limit * 2;
            int initialPoints = remainingPoints;
            if (limit <= 0 || remainingPoints <= 0)
            {
                return false;
            }

            for (int i = 0; i < source.Count; i++)
            {
                GameObject unit = source[i];
                if (unit == null)
                {
                    continue;
                }

                int cost = IA_BattleGovernorUtils.GetEngagementCost(unit);
                if (cost > remainingPoints)
                {
                    continue;
                }

                destination.Add(unit);
                remainingPoints -= cost;
                if (destination.Count >= limit)
                {
                    break;
                }
            }

            _landAttackersCommittedThisTick += destination.Count;
            _landPointsCommittedThisTick += Mathf.Max(0, initialPoints - remainingPoints);
            return destination.Count > 0;
        }

        private Vector3 BlendObjective(Vector3 baseCenter, Vector3 strategicObjective, float t, float fallbackMinRadius, float fallbackMaxRadius)
        {
            if (strategicObjective == Vector3.zero || DistanceFlat(baseCenter, strategicObjective) < 90f)
            {
                return ResolveExplorationObjective(baseCenter, Time.time, false, fallbackMinRadius, fallbackMaxRadius);
            }

            return Vector3.Lerp(baseCenter, strategicObjective, Mathf.Clamp01(t));
        }

        private Vector3 ResolveExplorationObjective(Vector3 baseCenter, float now, bool preferFar, float minRadius = 220f, float maxRadius = 1200f)
        {
            float start = _exploreAngleDeg;
            _exploreAngleDeg = (_exploreAngleDeg + 27f + (now * 0.03f)) % 360f;

            float min = Mathf.Max(120f, minRadius);
            float max = Mathf.Max(min + 80f, maxRadius);
            if (!preferFar)
            {
                max = Mathf.Min(max, 900f);
            }

            Vector3 best = baseCenter;
            float bestScore = float.MinValue;
            const int slices = 16;
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = Mathf.Lerp(min, max, ring / 2f);
                for (int i = 0; i < slices; i++)
                {
                    float angleDeg = start + ((360f / slices) * i) + (ring * 11f);
                    float angle = angleDeg * Mathf.Deg2Rad;
                    Vector3 probe = baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                    if (cell == null)
                    {
                        continue;
                    }

                    if (cell.Terrain == IA_TerrainType.Water || cell.Terrain == IA_TerrainType.Unknown)
                    {
                        continue;
                    }

                    float dist = DistanceFlat(baseCenter, cell.Center);
                    if (dist < 140f)
                    {
                        continue;
                    }

                    float score = dist;
                    if (cell.Terrain == IA_TerrainType.Open)
                    {
                        score += 24f;
                    }
                    else if (cell.Terrain == IA_TerrainType.City)
                    {
                        score += 14f;
                    }
                    else if (cell.Terrain == IA_TerrainType.Choke)
                    {
                        score -= 7f;
                    }

                    score -= cell.ObstacleDensity * 28f;
                    if (preferFar)
                    {
                        score += dist * 0.12f;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cell.Center;
                    }
                }
            }

            if (bestScore == float.MinValue)
            {
                float fallbackAngle = (_exploreAngleDeg + 45f) * Mathf.Deg2Rad;
                float fallbackRadius = preferFar ? 900f : 420f;
                best = baseCenter + new Vector3(Mathf.Cos(fallbackAngle) * fallbackRadius, 0f, Mathf.Sin(fallbackAngle) * fallbackRadius);
            }

            return best;
        }

        private static float DistanceFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private int CountNavalSupportUnits()
        {
            return CountSquadUnits(_context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort))
                   + CountSquadUnits(_context.SquadDirector.GetSquad(IA_SquadRole.NavalHeavy))
                   + CountSquadUnits(_context.SquadDirector.GetSquad(IA_SquadRole.Submarine));
        }

        private static int CountSquadUnits(IA_SquadData squad)
        {
            if (!HasUnits(squad))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < squad.Units.Count; i++)
            {
                if (squad.Units[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void SplitAmphibiousUnits(List<GameObject> source, List<GameObject> loaded, List<GameObject> empty)
        {
            if (source == null)
            {
                if (loaded != null) loaded.Clear();
                if (empty != null) empty.Clear();
                return;
            }

            if (loaded != null)
            {
                loaded.Clear();
            }

            if (empty != null)
            {
                empty.Clear();
            }

            for (int i = 0; i < source.Count; i++)
            {
                GameObject unit = source[i];
                if (unit == null)
                {
                    continue;
                }

                HovercraftTransporte hover = unit.GetComponent<HovercraftTransporte>();
                if (hover == null)
                {
                    continue;
                }

                if (hover.TemCarga())
                {
                    loaded.Add(unit);
                }
                else
                {
                    empty.Add(unit);
                }
            }
        }

        private void QueueMove(string key, List<GameObject> units, Vector3 destination, int priority, float cooldown)
        {
            IA_MoveOrderData payload = new IA_MoveOrderData
            {
                Units = CloneUnits(units),
                Destination = destination
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Move,
                Priority = priority,
                DedupKey = "move:" + key,
                CooldownSeconds = cooldown,
                Payload = payload
            };

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

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Attack,
                Priority = priority,
                DedupKey = "attack:" + key,
                CooldownSeconds = cooldown,
                Payload = payload
            };

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }

        private void QueueAbility(string key, GameObject caster, string abilityKey, Vector3 targetPosition, Transform target, int priority, float cooldown)
        {
            if (caster == null)
            {
                return;
            }

            IA_AbilityOrderData payload = new IA_AbilityOrderData
            {
                Caster = caster,
                AbilityKey = abilityKey,
                TargetPosition = targetPosition,
                Target = target
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Ability,
                Priority = priority,
                DedupKey = "ability:" + key,
                CooldownSeconds = cooldown,
                Payload = payload
            };

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
    }
}
