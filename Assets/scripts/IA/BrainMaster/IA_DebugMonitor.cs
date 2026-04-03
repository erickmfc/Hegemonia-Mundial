using System.Text;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_DebugMonitor : IIAUpdateModule
    {
        private const float VerboseLogIntervalSeconds = 20f;
        private readonly IA_BrainMaster _brain;
        private readonly IA_WorldState _world;
        private readonly IA_CommandQueue _queue;
        private readonly IA_PerformanceScheduler _scheduler;
        private float _nextVerboseLogTime;

        public bool VerboseLogs;
        public string LastSummary = string.Empty;

        public IA_DebugMonitor(IA_BrainMaster brain, IA_WorldState world, IA_CommandQueue queue, IA_PerformanceScheduler scheduler)
        {
            _brain = brain;
            _world = world;
            _queue = queue;
            _scheduler = scheduler;
        }

        public string Name
        {
            get { return "IA_DebugMonitor"; }
        }

        public float Interval
        {
            get { return 3.50f; }
        }

        public float BudgetMs
        {
            get { return 0.45f; }
        }

        public void Tick(float now, float deltaTime)
        {
            var sb = new StringBuilder(512);
            sb.Append("AI=").Append(_brain != null ? _brain.name : "null");
            sb.Append(" | Team=").Append(_brain != null ? _brain.TeamId : 0);
            sb.Append(" | Own=").Append(_world.OwnUnits.Count);
            sb.Append(" | Combat=").Append(_world.OwnCombatUnits.Count);
            sb.Append(" | Struct=").Append(_world.OwnStructures.Count);
            sb.Append(" | EnemyVisible=").Append(_world.VisibleEnemies.Count);
            sb.Append(" | Queue=").Append(_queue.PendingCount);
            sb.Append(" | Cooldowns=").Append(_queue.CooldownCount);

            var snapshots = _scheduler.GetSnapshot();
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snap = snapshots[i];
                sb.Append(" | ").Append(snap.Name).Append(":")
                  .Append(snap.LastCostMs.ToString("0.00")).Append("ms");
            }

            string buildProfile = _brain != null
                && _brain.Context != null
                && _brain.Context.BuildDirector != null
                ? _brain.Context.BuildDirector.LastProfilingSummary
                : string.Empty;
            if (!string.IsNullOrEmpty(buildProfile))
            {
                sb.Append(" | BuildProfile=").Append(buildProfile);
            }

            LastSummary = sb.ToString();

            if (VerboseLogs && now >= _nextVerboseLogTime)
            {
                _nextVerboseLogTime = now + VerboseLogIntervalSeconds;
                if (!Application.isEditor)
                {
                    Debug.Log("[IA_DebugMonitor] " + LastSummary);
                }
            }
        }
    }
}
