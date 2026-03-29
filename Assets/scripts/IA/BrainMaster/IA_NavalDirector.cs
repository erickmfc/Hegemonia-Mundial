using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_NavalDirector : IIAUpdateModule
    {
        private const float ForcedNavalStrikeStartSeconds = 60f;
        private readonly IA_Context _context;
        private float _nextDecisionTime;

        public IA_NavalDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_NavalDirector"; }
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

            _nextDecisionTime = now + 1.05f;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            Transform navalTarget = GetVisibleNavalTarget();
            Vector3 pressureTarget = ResolvePressureTarget(baseCenter, now);

            DispatchEscort(baseCenter, navalTarget, pressureTarget);
            DispatchHeavy(baseCenter, navalTarget, pressureTarget);
            DispatchSubmarine(baseCenter, navalTarget, pressureTarget);
        }

        private void DispatchEscort(Vector3 baseCenter, Transform target, Vector3 pressureTarget)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 coastPatrol = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Coast, 110f, 320f, 20);
            if (target != null)
            {
                QueueAttack("naval_escort", squad.Units, target, coastPatrol, 76, 4.2f);
            }
            else
            {
                QueueMove("naval_escort", squad.Units, pressureTarget != Vector3.zero ? pressureTarget : coastPatrol, 76, 3.8f);
            }
        }

        private void DispatchHeavy(Vector3 baseCenter, Transform target, Vector3 pressureTarget)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.NavalHeavy);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 attackAxis = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Water, 170f, 450f, 26);
            if (target != null)
            {
                QueueAttack("naval_heavy", squad.Units, target, attackAxis, 85, 3.8f);
            }
            else
            {
                QueueMove("naval_heavy", squad.Units, pressureTarget != Vector3.zero ? pressureTarget : attackAxis, 81, 3.8f);
            }
        }

        private void DispatchSubmarine(Vector3 baseCenter, Transform target, Vector3 pressureTarget)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.Submarine);
            if (!HasUnits(squad))
            {
                return;
            }

            Vector3 flankWater = _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Water, 200f, 500f, 32);
            if (target != null)
            {
                QueueAttack("submarine", squad.Units, target, flankWater, 88, 5.2f);
            }
            else
            {
                QueueMove("submarine", squad.Units, pressureTarget != Vector3.zero ? pressureTarget : flankWater, 82, 4.2f);
            }
        }

        private Vector3 ResolvePressureTarget(Vector3 baseCenter, float now)
        {
            Vector3 hiddenEnemyAnchor;
            if (now >= ForcedNavalStrikeStartSeconds && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out hiddenEnemyAnchor))
            {
                return hiddenEnemyAnchor;
            }

            List<IA_EnemyObservation> memory = _context.WorldState.GetEnemyMemory(300f);
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
                float score = ((obs.Domain == IA_Domain.Naval) ? 30f : 0f)
                    + obs.ThreatScore
                    + (obs.IsStructure ? 24f : 0f)
                    - (age * 0.28f);
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

            return _context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Water, 220f, 1200f, 28);
        }

        private Transform GetVisibleNavalTarget()
        {
            float best = float.MaxValue;
            Transform selected = null;
            Vector3 baseCenter = _context.WorldState.BaseCenter;
            for (int i = 0; i < _context.WorldState.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _context.WorldState.VisibleEnemies[i];
                if (obs == null || obs.Transform == null || obs.Domain != IA_Domain.Naval)
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
