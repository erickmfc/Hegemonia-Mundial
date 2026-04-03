using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_TacticalDirector : IIAUpdateModule
    {
        private const float ForcedAssaultStartSeconds = 60f;
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private float _nextAssaultWaveTime;
        private float _lastEnemySeenTime = -999f;
        private float _exploreAngleDeg;
        private Vector3 _lastStrategicObjective;

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
            if (_context.Brain != null && _context.Brain.IsBootstrapActive)
            {
                return;
            }

            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + 0.60f;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            Transform priorityEnemy = _context.WorldState.GetNearestVisibleEnemy(baseCenter, IA_Domain.Land);
            if (priorityEnemy != null)
            {
                _lastEnemySeenTime = now;
            }
            Vector3 strategicObjective = ResolveStrategicObjective(baseCenter, priorityEnemy, now);
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
            if (visibleEnemy != null)
            {
                QueueAttack("armored", squad.Units, visibleEnemy, openApproach, 89, 3.2f);
            }
            else
            {
                Vector3 pressurePoint = BlendObjective(baseCenter, strategicObjective, 0.78f, 210f, 520f);
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
            Vector3 loadRally = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 35f, 100f, 18);
            List<GameObject> loaded = new List<GameObject>();
            List<GameObject> empty = new List<GameObject>();
            SplitAmphibiousUnits(squad.Units, loaded, empty);

            if (empty.Count > 0)
            {
                QueueMove("amphibious_load", empty, loadRally, 72, 5.4f);

                for (int i = 0; i < empty.Count && i < 2; i++)
                {
                    GameObject transport = empty[i];
                    if (transport == null || DistanceFlat(transport.transform.position, loadRally) > 85f)
                    {
                        continue;
                    }

                    QueueAbility(
                        "amphibious_board_" + transport.GetInstanceID(),
                        transport,
                        "IniciarEmbarque",
                        transport.transform.position,
                        null,
                        74,
                        9f);
                }
            }

            if (loaded.Count == 0)
            {
                return;
            }

            Vector3 pressureCoast = Vector3.Lerp(coast, strategicObjective, 0.45f);
            if (pressureCoast == Vector3.zero)
            {
                pressureCoast = coast;
            }

            if (visibleEnemy != null && CountNavalSupportUnits() >= 2)
            {
                QueueAttack("amphibious", loaded, visibleEnemy, coast, 80, 5.2f);
            }
            else
            {
                QueueMove("amphibious_stage", loaded, pressureCoast, 74, 4.8f);
            }
        }

        private void DispatchGroundLogistics(Vector3 baseCenter)
        {
            List<GameObject> emptyTransports = new List<GameObject>();
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
                    emptyTransports.Add(unit);
                }
            }

            if (emptyTransports.Count == 0)
            {
                return;
            }

            Vector3 rally = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 35f, 110f, 18);
            QueueMove("logistics_idle", emptyTransports, rally, 64, 5.6f);

            if (_context.Brain == null || _context.Brain.IntegrationMode != IA_BrainMaster.IA_IntegrationMode.Full)
            {
                return;
            }

            for (int i = 0; i < emptyTransports.Count && i < 2; i++)
            {
                GameObject transport = emptyTransports[i];
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

            bool forcedAssault = now >= ForcedAssaultStartSeconds;
            Vector3 forcedTarget = strategicObjective;
            bool hasForcedTarget = forcedAssault && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out forcedTarget);

            List<GameObject> assaultUnits = CollectGroundAssaultUnits(forcedAssault ? 40 : 24);
            if (assaultUnits.Count < (forcedAssault ? 2 : 3))
            {
                return;
            }

            if (visibleEnemy != null)
            {
                QueueAttack("assault_wave", assaultUnits, visibleEnemy, strategicObjective, 93, 4.2f);
                _nextAssaultWaveTime = now + 4.5f;
                return;
            }

            Vector3 fallbackTarget = hasForcedTarget ? forcedTarget : strategicObjective;
            if (DistanceFlat(baseCenter, fallbackTarget) < 170f)
            {
                fallbackTarget = ResolveExplorationObjective(baseCenter, now, true);
            }

            QueueMove("assault_wave", assaultUnits, fallbackTarget, forcedAssault ? 96 : 90, forcedAssault ? 2.2f : 3.2f);
            _nextAssaultWaveTime = now + (forcedAssault ? 3.0f : 5.5f);
        }

        private Vector3 ResolveStrategicObjective(Vector3 baseCenter, Transform visibleEnemy, float now)
        {
            if (visibleEnemy != null)
            {
                _lastStrategicObjective = visibleEnemy.position;
                return _lastStrategicObjective;
            }

            List<IA_EnemyObservation> memory = _context.WorldState.GetEnemyMemory(220f);
            IA_EnemyObservation best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < memory.Count; i++)
            {
                IA_EnemyObservation obs = memory[i];
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
            List<GameObject> selected = new List<GameObject>();
            int max = Mathf.Clamp(limit, 6, 48);

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

                selected.Add(unit);
                if (selected.Count >= max)
                {
                    break;
                }
            }

            return selected;
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
                return;
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
            var output = new List<GameObject>();
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
