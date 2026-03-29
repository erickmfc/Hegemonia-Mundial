using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_DefenseDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private float _nextDecisionTime;

        public IA_DefenseDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_DefenseDirector"; }
        }

        public float Interval
        {
            get { return 1.80f; }
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

            _nextDecisionTime = now + 1.45f;
            if (_context.CommandQueue.PendingCount > 6)
            {
                return;
            }

            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
            float localThreat = _context.ThreatAnalyzer.EvaluateThreat(baseCenter, IA_Domain.Land);

            if (localThreat < 45f
                && !counter.ReinforceCoast
                && !counter.ReinforceCenter
                && !counter.AntiRush
                && counter.AirWeight < 0.25f)
            {
                return;
            }

            if (localThreat > 55f)
            {
                QueueDefensiveBuild("torreta", baseCenter, IA_TerrainType.Choke, 35f, 140f, 93, 10f);
            }

            if (counter.AirWeight > 0.32f || localThreat > 70f)
            {
                QueueDefensiveBuild("ciws", baseCenter, IA_TerrainType.City, 30f, 130f, 92, 10f);
            }

            if (counter.ReinforceCoast)
            {
                QueueDefensiveBuild("radar", baseCenter, IA_TerrainType.Coast, 80f, 260f, 82, 16f);
                QueueDefensiveBuild("torreta", baseCenter, IA_TerrainType.Coast, 90f, 300f, 80, 12f);
            }

            if (counter.ReinforceCenter)
            {
                QueueDefensiveBuild("muro", baseCenter, IA_TerrainType.Choke, 30f, 150f, 76, 7f);
                QueueDefensiveBuild("hack", baseCenter, IA_TerrainType.Open, 60f, 180f, 74, 12f);
            }

            if (counter.AntiRush || localThreat > 85f)
            {
                QueueDefensiveBuild("lancador missil", baseCenter, IA_TerrainType.Open, 90f, 240f, 70, 35f);
            }
        }

        private void QueueDefensiveBuild(
            string itemKey,
            Vector3 anchor,
            IA_TerrainType terrain,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown)
        {
            Vector3 candidate = _context.MapAnalyzer.FindPointInTerrain(anchor, terrain, minRadius, maxRadius, 12);
            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = itemKey,
                Position = candidate,
                Rotation = Quaternion.identity,
                Zone = IA_ZoneType.Defense
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Build,
                Priority = priority,
                DedupKey = "defense_build:" + IA_Text.Normalize(itemKey),
                CooldownSeconds = cooldown,
                Payload = payload
            };

            string reason;
            _context.CommandQueue.Enqueue(request, Time.time, out reason);
        }
    }
}
