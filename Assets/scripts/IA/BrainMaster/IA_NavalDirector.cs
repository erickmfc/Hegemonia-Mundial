using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_NavalDirector : IIAUpdateModule
    {
        private const float ForcedNavalStrikeStartSeconds = 60f;
        private readonly IA_Context _context;
        private readonly List<IA_EnemyObservation> _enemyMemoryBuffer = new List<IA_EnemyObservation>(64);
        private readonly List<GameObject> _escortActiveBuffer = new List<GameObject>(8);
        private readonly List<GameObject> _heavyActiveBuffer = new List<GameObject>(8);
        private readonly List<GameObject> _subActiveBuffer = new List<GameObject>(4);
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
            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _escortActiveBuffer.Clear();
            _heavyActiveBuffer.Clear();
            _subActiveBuffer.Clear();
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
            long targetStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Transform navalTarget = GetVisibleNavalTarget();
            Vector3 pressureTarget = ResolvePressureTarget(baseCenter, now);
            float targetMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - targetStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (targetMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sensor_update_ms", targetMs);
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("targeting_ms", targetMs);
            }
            Vector3 objective = navalTarget != null ? navalTarget.position : pressureTarget;
            Vector3 assemblyCenter = ResolveAssemblyPoint(baseCenter, objective);
            Vector3 escortStage = ResolveStagePoint(assemblyCenter, objective, -180f, 140f);
            Vector3 heavyStage = ResolveStagePoint(assemblyCenter, objective, 0f, 170f);
            Vector3 subStage = ResolveStagePoint(assemblyCenter, objective, 180f, 260f);

            IA_SquadData escort = _context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort);
            IA_SquadData heavy = _context.SquadDirector.GetSquad(IA_SquadRole.NavalHeavy);
            IA_SquadData submarine = _context.SquadDirector.GetSquad(IA_SquadRole.Submarine);
            bool holdFormation = !ShouldLaunchNavalStrike(navalTarget, escort, heavy, submarine, assemblyCenter);

            DispatchEscort(navalTarget, pressureTarget, escortStage, holdFormation);
            DispatchHeavy(navalTarget, pressureTarget, heavyStage, holdFormation);
            DispatchSubmarine(navalTarget, pressureTarget, subStage, holdFormation);
            int activeTaskforces = (_escortActiveBuffer.Count + _heavyActiveBuffer.Count + _subActiveBuffer.Count) > 0 ? 1 : 0;
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("active_naval_taskforces", activeTaskforces);
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("naval_unit_update_ms", elapsedMs);
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("formation_update_ms", elapsedMs * 0.45f);
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

        private void DispatchEscort(Transform target, Vector3 pressureTarget, Vector3 stagePoint, bool holdFormation)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort);
            if (!HasUnits(squad))
            {
                return;
            }

            if (!TrySelectNavalUnits(squad.Units, _escortActiveBuffer, 0.45f))
            {
                return;
            }

            if (holdFormation || target == null)
            {
                QueueMove("naval_escort_stage", _escortActiveBuffer, stagePoint != Vector3.zero ? stagePoint : pressureTarget, 78, 3.2f);
            }
            else
            {
                Vector3 coastPatrol = ResolveAttackPoint(stagePoint, target.position, -150f, 260f);
                QueueAttack("naval_escort", _escortActiveBuffer, target, coastPatrol, 80, 4.2f);
            }
        }

        private void DispatchHeavy(Transform target, Vector3 pressureTarget, Vector3 stagePoint, bool holdFormation)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.NavalHeavy);
            if (!HasUnits(squad))
            {
                return;
            }

            if (!TrySelectNavalUnits(squad.Units, _heavyActiveBuffer, 0.40f))
            {
                return;
            }

            if (holdFormation || target == null)
            {
                QueueMove("naval_heavy_stage", _heavyActiveBuffer, stagePoint != Vector3.zero ? stagePoint : pressureTarget, 84, 3.4f);
            }
            else
            {
                Vector3 attackAxis = ResolveAttackPoint(stagePoint, target.position, 0f, 320f);
                QueueAttack("naval_heavy", _heavyActiveBuffer, target, attackAxis, 88, 3.8f);
            }
        }

        private void DispatchSubmarine(Transform target, Vector3 pressureTarget, Vector3 stagePoint, bool holdFormation)
        {
            IA_SquadData squad = _context.SquadDirector.GetSquad(IA_SquadRole.Submarine);
            if (!HasUnits(squad))
            {
                return;
            }

            if (!TrySelectNavalUnits(squad.Units, _subActiveBuffer, 0.20f))
            {
                return;
            }

            if (holdFormation || target == null)
            {
                QueueMove("submarine_stage", _subActiveBuffer, stagePoint != Vector3.zero ? stagePoint : pressureTarget, 83, 4.0f);
            }
            else
            {
                Vector3 flankWater = ResolveAttackPoint(stagePoint, target.position, 220f, 340f);
                QueueAttack("submarine", _subActiveBuffer, target, flankWater, 90, 5.2f);
            }
        }

        private Vector3 ResolvePressureTarget(Vector3 baseCenter, float now)
        {
            Vector3 hiddenEnemyAnchor;
            if (now >= ForcedNavalStrikeStartSeconds && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out hiddenEnemyAnchor))
            {
                return hiddenEnemyAnchor;
            }

            _context.WorldState.FillEnemyMemory(_enemyMemoryBuffer, 300f);
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

        private bool ShouldLaunchNavalStrike(
            Transform target,
            IA_SquadData escort,
            IA_SquadData heavy,
            IA_SquadData submarine,
            Vector3 assemblyCenter)
        {
            if (target == null)
            {
                return false;
            }

            int escortCount = CountUnits(escort);
            int heavyCount = CountUnits(heavy);
            int subCount = CountUnits(submarine);
            int combatCount = escortCount + heavyCount + subCount;
            if (combatCount < 3)
            {
                return false;
            }

            if (heavyCount + subCount <= 0)
            {
                return false;
            }

            int assembled = CountUnitsNear(escort, assemblyCenter, 420f)
                           + CountUnitsNear(heavy, assemblyCenter, 420f)
                           + CountUnitsNear(submarine, assemblyCenter, 460f);
            int required = Mathf.Clamp(combatCount - 1, 2, combatCount);
            return assembled >= required;
        }

        private Vector3 ResolveAssemblyPoint(Vector3 baseCenter, Vector3 objective)
        {
            Vector3 anchor = objective != Vector3.zero
                ? Vector3.Lerp(baseCenter, objective, 0.45f)
                : baseCenter;
            return _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Water, 70f, 260f, 24);
        }

        private Vector3 ResolveStagePoint(Vector3 assemblyCenter, Vector3 objective, float lateralOffset, float backOffset)
        {
            Vector3 axis = Flatten(objective - assemblyCenter);
            if (axis.sqrMagnitude < 0.01f)
            {
                axis = Vector3.forward;
            }

            axis.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, axis).normalized;
            Vector3 approximate = assemblyCenter + (lateral * lateralOffset) - (axis * backOffset);
            return _context.MapAnalyzer.FindPointInTerrain(approximate, IA_TerrainType.Water, 20f, 120f, 22);
        }

        private Vector3 ResolveAttackPoint(Vector3 stagePoint, Vector3 targetPosition, float lateralOffset, float backOffset)
        {
            Vector3 axis = Flatten(targetPosition - stagePoint);
            if (axis.sqrMagnitude < 0.01f)
            {
                axis = Vector3.forward;
            }

            axis.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, axis).normalized;
            Vector3 approximate = targetPosition + (lateral * lateralOffset) - (axis * backOffset);
            return _context.MapAnalyzer.FindPointInTerrain(approximate, IA_TerrainType.Water, 20f, 140f, 24);
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

        private static int CountUnits(IA_SquadData squad)
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

        private static int CountUnitsNear(IA_SquadData squad, Vector3 point, float radius)
        {
            if (!HasUnits(squad))
            {
                return 0;
            }

            int count = 0;
            float radiusSq = radius * radius;
            for (int i = 0; i < squad.Units.Count; i++)
            {
                GameObject unit = squad.Units[i];
                if (unit == null)
                {
                    continue;
                }

                Vector3 delta = Flatten(unit.transform.position) - Flatten(point);
                if (delta.sqrMagnitude <= radiusSq)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TrySelectNavalUnits(List<GameObject> source, List<GameObject> destination, float budgetShare)
        {
            destination.Clear();
            if (source == null || source.Count == 0)
            {
                return false;
            }

            IA_BattleGovernorDecision decision = _context != null ? _context.BattleDecision : null;
            int limit = source.Count;
            if (decision != null)
            {
                limit = Mathf.Max(1, Mathf.CeilToInt(decision.MaxNavalAttackers * Mathf.Clamp01(budgetShare)));
            }

            for (int i = 0; i < source.Count && destination.Count < limit; i++)
            {
                GameObject unit = source[i];
                if (unit != null)
                {
                    destination.Add(unit);
                }
            }

            return destination.Count > 0;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
