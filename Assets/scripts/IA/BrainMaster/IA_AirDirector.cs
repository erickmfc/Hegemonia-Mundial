using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_AirDirector : IIAUpdateModule
    {
        private const float ForcedAirStrikeStartSeconds = 60f;
        private readonly IA_Context _context;
        private readonly List<IA_EnemyObservation> _enemyMemoryBuffer = new List<IA_EnemyObservation>(64);
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
            if (_context.Brain != null && _context.Brain.IsBootstrapActive)
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
            Transform airEnemy = GetVisibleAirEnemy();
            Transform groundEnemy = _context.WorldState.GetNearestVisibleEnemy(baseCenter, IA_Domain.Land);
            Vector3 pressureTarget = ResolvePressureTarget(baseCenter, now);

            DispatchAirIntercept(baseCenter, airEnemy, groundEnemy, pressureTarget);
            DispatchAirTransport(baseCenter, groundEnemy, pressureTarget, now);
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

            Transform target = airEnemy != null ? airEnemy : fallbackEnemy;
            Vector3 patrol = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 120f, 330f, 20);
            if (target != null)
            {
                QueueAttack("air_intercept", squad.Units, target, patrol, 87, 2.9f);
            }
            else
            {
                Vector3 fallback = pressureTarget != Vector3.zero ? pressureTarget : patrol;
                QueueMove("air_intercept", squad.Units, fallback + Vector3.up * 20f, 80, 3.1f);
            }
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

            Vector3 insertion = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.City, 90f, 260f, 22);
            if (target != null)
            {
                insertion = target.position + Vector3.up * 6f;
            }
            else if (now >= ForcedAirStrikeStartSeconds && pressureTarget != Vector3.zero)
            {
                insertion = pressureTarget + Vector3.up * 6f;
            }

            QueueMove("air_transport", squad.Units, insertion, 78, 4.2f);

            // Trigger tactical drop behavior when close enough.
            for (int i = 0; i < squad.Units.Count; i++)
            {
                GameObject unit = squad.Units[i];
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

                    IA_CommandRequest request = new IA_CommandRequest
                    {
                        Type = IA_CommandType.Ability,
                        Priority = 83,
                        DedupKey = "ability:air_transport_drop",
                        CooldownSeconds = 6f,
                        Payload = ability
                    };

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
