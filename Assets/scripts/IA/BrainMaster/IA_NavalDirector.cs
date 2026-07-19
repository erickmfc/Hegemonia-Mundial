using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_NavalDirector : IIAUpdateModule
    {
        private const float ForcedNavalStrikeStartSeconds = 40f;
        private readonly IA_Context _context;
        private readonly List<IA_EnemyObservation> _enemyMemoryBuffer = new List<IA_EnemyObservation>(64);
        private readonly List<IA_StrategicTargetData> _strategicTargetsBuffer = new List<IA_StrategicTargetData>(6);
        private readonly List<GameObject> _escortActiveBuffer = new List<GameObject>(8);
        private readonly List<GameObject> _heavyActiveBuffer = new List<GameObject>(8);
        private readonly List<GameObject> _subActiveBuffer = new List<GameObject>(4);
        private readonly List<GameObject> _coastalGuardBuffer = new List<GameObject>(2);
        private Vector3 _coastalGuardPoint;
        private float _nextCoastalGuardRefreshTime;
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
            _coastalGuardBuffer.Clear();
            if (_context.Brain != null && _context.Brain.IsBootstrapActive && (int)_context.Brain.BootstrapStage < (int)IA_BrainMaster.IA_BootstrapStage.ProduceShip)
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
            IA_BrainMaster brain = _context != null ? _context.Brain : null;
            bool pressurePhase = brain != null && brain.StrategicPhase >= IA_StrategicPhase.PressaoEconomica;
            int strategicTargetCount = pressurePhase
                ? _context.WorldState.FillEnemyStrategicTargets(_strategicTargetsBuffer, baseCenter, 4)
                : 0;
            Transform escortTarget = navalTarget;
            Vector3 escortPressureTarget = pressureTarget;
            Transform heavyTarget = navalTarget;
            Vector3 heavyPressureTarget = pressureTarget;
            Transform subTarget = navalTarget;
            Vector3 subPressureTarget = pressureTarget;

            if (strategicTargetCount > 0)
            {
                IA_StrategicTargetData primary = _strategicTargetsBuffer[0];
                heavyTarget = primary.Transform;
                heavyPressureTarget = primary.Position;
                pressureTarget = primary.Position;
                if (strategicTargetCount > 1)
                {
                    IA_StrategicTargetData secondary = _strategicTargetsBuffer[1];
                    subTarget = secondary.Transform;
                    subPressureTarget = secondary.Position;
                }

                if (strategicTargetCount > 2)
                {
                    IA_StrategicTargetData tertiary = _strategicTargetsBuffer[2];
                    escortTarget = tertiary.Transform;
                    escortPressureTarget = tertiary.Position;
                }
                else
                {
                    escortTarget = primary.Transform;
                    escortPressureTarget = primary.Position;
                }

                if (brain != null)
                {
                    brain.ReportStrategicTarget("naval multi-alvo x" + strategicTargetCount + " principal=" + primary.Kind);
                }
            }

            float targetMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - targetStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (targetMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sensor_update_ms", targetMs);
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("targeting_ms", targetMs);
            }
            Vector3 objective = heavyTarget != null ? heavyTarget.position : pressureTarget;
            Vector3 assemblyCenter = ResolveAssemblyPoint(baseCenter, objective);
            Vector3 escortStage = ResolveStagePoint(assemblyCenter, objective, -320f, 240f);
            Vector3 heavyStage = ResolveStagePoint(assemblyCenter, objective, 0f, 360f);
            Vector3 subStage = ResolveStagePoint(assemblyCenter, objective, 340f, 520f);

            IA_SquadData escort = _context.SquadDirector.GetSquad(IA_SquadRole.NavalEscort);
            IA_SquadData heavy = _context.SquadDirector.GetSquad(IA_SquadRole.NavalHeavy);
            IA_SquadData submarine = _context.SquadDirector.GetSquad(IA_SquadRole.Submarine);
            bool holdFormation = !ShouldLaunchNavalStrike(heavyTarget, escort, heavy, submarine, assemblyCenter);

            DispatchEscort(escortTarget, escortPressureTarget, escortStage, holdFormation, baseCenter, now);
            DispatchHeavy(heavyTarget, heavyPressureTarget, heavyStage, holdFormation);
            DispatchSubmarine(subTarget, subPressureTarget, subStage, holdFormation);
            int activeTaskforces = 0;
            if (_coastalGuardBuffer.Count > 0) activeTaskforces++;
            if (_escortActiveBuffer.Count > 0) activeTaskforces++;
            if (_heavyActiveBuffer.Count > 0) activeTaskforces++;
            if (_subActiveBuffer.Count > 0) activeTaskforces++;
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

        private void DispatchEscort(Transform target, Vector3 pressureTarget, Vector3 stagePoint, bool holdFormation, Vector3 baseCenter, float now)
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

            if (_escortActiveBuffer.Count > 1)
            {
                _coastalGuardBuffer.Add(_escortActiveBuffer[0]);
                _escortActiveBuffer.RemoveAt(0);
                QueueMove("naval_coastal_guard", _coastalGuardBuffer, ResolveCoastalGuardPoint(baseCenter, now), 86, 5.0f);
            }

            if (_escortActiveBuffer.Count == 0)
            {
                return;
            }

            if (holdFormation || target == null)
            {
                QueueMove("naval_escort_stage", _escortActiveBuffer, stagePoint != Vector3.zero ? stagePoint : pressureTarget, 78, 3.2f);
            }
            else
            {
                Vector3 coastPatrol = ResolveAttackPoint(stagePoint, target.position, -260f, 420f);
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
                Vector3 attackAxis = ResolveAttackPoint(stagePoint, target.position, 0f, 520f);
                QueueAttack("naval_heavy", _heavyActiveBuffer, target, attackAxis, 88, 3.8f);
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("units_committed_naval", _heavyActiveBuffer.Count);
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
                Vector3 flankWater = ResolveAttackPoint(stagePoint, target.position, 360f, 620f);
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
            int minCombat = _context.Brain != null && (int)_context.Brain.StrategicPhase >= (int)IA_StrategicPhase.PressaoEconomica ? 4 : 3;
            if (combatCount < minCombat)
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
            int required = Mathf.Clamp(combatCount - 2, 2, combatCount);
            bool ready = assembled >= required;
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("naval_strike_ready", ready ? 1 : 0);
            return ready;
        }

        private Vector3 ResolveAssemblyPoint(Vector3 baseCenter, Vector3 objective)
        {
            Vector3 anchor = objective != Vector3.zero
                ? Vector3.Lerp(baseCenter, objective, 0.45f)
                : baseCenter;
            return _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Water, 70f, 260f, 24);
        }

        private Vector3 ResolveCoastalGuardPoint(Vector3 baseCenter, float now)
        {
            if (_coastalGuardPoint != Vector3.zero && now < _nextCoastalGuardRefreshTime)
            {
                return _coastalGuardPoint;
            }

            Vector3 anchor;
            if (!TryFindOwnStructureAnchor(out anchor, "plataforma", "pier", "estaleiro"))
            {
                anchor = baseCenter;
            }

            _coastalGuardPoint = _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Water, 120f, 320f, 26);
            _nextCoastalGuardRefreshTime = now + 28f;
            return _coastalGuardPoint;
        }

        private bool TryFindOwnStructureAnchor(out Vector3 anchor, params string[] hints)
        {
            anchor = Vector3.zero;
            if (_context == null || _context.WorldState == null || _context.WorldState.OwnStructures == null)
            {
                return false;
            }

            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(structure.name);
                for (int h = 0; h < hints.Length; h++)
                {
                    if (!string.IsNullOrEmpty(hints[h]) && name.Contains(hints[h]))
                    {
                        anchor = structure.transform.position;
                        return true;
                    }
                }
            }

            return false;
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

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Move,
                "IA_NavalDirector",
                "naval",
                "reposicionamento naval",
                priority,
                "naval",
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
                "IA_NavalDirector",
                "naval",
                "ataque naval",
                priority,
                "naval",
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
