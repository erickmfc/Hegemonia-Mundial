using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_LotPlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private float _lastCacheResetTime;

        public string LastSummary { get; private set; }

        public IA_LotPlanner(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_LotPlanner"; }
        }

        public float Interval
        {
            get { return 4.0f; }
        }

        public float BudgetMs
        {
            get { return 0.25f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (now - _lastCacheResetTime >= 12f)
            {
                LastSummary = "zone-aware lot search ready";
                _lastCacheResetTime = now;
            }
        }

        public bool TryFindBestLot(string itemKey, IA_ZoneType zone, GameObject prefab, out IA_LotCandidate bestLot)
        {
            bestLot = null;

            if (_context == null || _context.SemanticMapPlanner == null || _context.ZonePlanner == null)
            {
                return false;
            }

            IA_UrbanSectorType targetSector = _context.ZonePlanner.ResolveSector(zone);
            Vector2 halfExtents = _context.MapAnalyzer != null
                ? _context.MapAnalyzer.EstimateFootprint(prefab, 12f)
                : new Vector2(12f, 12f);

            float bestScore = float.MinValue;
            foreach (IA_SemanticCell cell in _context.SemanticMapPlanner.Cells)
            {
                if (cell == null || cell.Forbidden || !cell.Buildable || cell.Occupied || cell.Reserved)
                {
                    continue;
                }

                if (!IsSectorCandidate(targetSector, cell.Sector))
                {
                    continue;
                }

                Quaternion rotation = ResolveRotation(itemKey, zone, cell.Center);
                float score = ScoreLot(cell, zone, targetSector);
                string validationMessage = string.Empty;
                bool valid = _context.UrbanBuildValidator == null
                    || _context.UrbanBuildValidator.ValidateLot(itemKey, zone, cell.Center, rotation, out validationMessage);

                if (!valid)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLot = new IA_LotCandidate
                    {
                        ItemKey = itemKey,
                        Zone = zone,
                        Sector = cell.Sector,
                        Position = cell.Center,
                        Rotation = rotation,
                        HalfExtents = halfExtents,
                        Score = score,
                        Valid = true,
                        ValidationMessage = validationMessage
                    };
                }
            }

            if (bestLot != null)
            {
                LastSummary = "best lot " + itemKey + " -> " + bestLot.Position + " score=" + bestLot.Score.ToString("F1");
                return true;
            }

            LastSummary = "no valid lot for " + itemKey;
            return false;
        }

        private Quaternion ResolveRotation(string itemKey, IA_ZoneType zone, Vector3 position)
        {
            string normalized = IA_Text.Normalize(itemKey);
            if ((zone == IA_ZoneType.Naval || normalized.Contains("estaleiro") || normalized.Contains("pier"))
                && NavalPlacementResolver.TryResolveWaterDirection(position, Vector3.forward, 20f, 180f, out var waterDirection, out var waterPoint, out var seaLevel))
            {
                if (waterDirection.sqrMagnitude >= 0.01f)
                {
                    return Quaternion.LookRotation(waterDirection.normalized, Vector3.up);
                }
            }

            if (_context.WorldState != null && _context.WorldState.TryGetEnemyStrategicAnchor(position, out var enemyAnchor))
            {
                Vector3 forward = enemyAnchor - position;
                forward.y = 0f;
                if (forward.sqrMagnitude >= 0.01f)
                {
                    return Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }

            return Quaternion.identity;
        }

        private float ScoreLot(IA_SemanticCell cell, IA_ZoneType zone, IA_UrbanSectorType targetSector)
        {
            float score = 100f;
            score -= cell.Threat * 0.18f;
            score += Mathf.Clamp(cell.Clearance, 0f, 120f) * 0.25f;

            if (cell.Sector == targetSector)
            {
                score += 30f;
            }

            switch (zone)
            {
                case IA_ZoneType.Core:
                    score += Mathf.Max(0f, 120f - cell.BaseDistance) * 0.25f;
                    break;
                case IA_ZoneType.Economy:
                    score += Mathf.Max(0f, 180f - Mathf.Abs(cell.BaseDistance - 180f)) * 0.15f;
                    break;
                case IA_ZoneType.Military:
                case IA_ZoneType.Defense:
                case IA_ZoneType.Frontline:
                    score += cell.Threat * 0.10f;
                    score += Mathf.Max(0f, cell.BaseDistance - 90f) * 0.05f;
                    break;
                case IA_ZoneType.Air:
                    score += Mathf.Max(0f, 12f - cell.Slope) * 4f;
                    score += Mathf.Clamp(cell.Clearance, 0f, 160f) * 0.35f;
                    score -= Mathf.Max(0f, 120f - cell.CoastDistance) * 0.20f;
                    break;
                case IA_ZoneType.Naval:
                case IA_ZoneType.Coast:
                    score += Mathf.Max(0f, 110f - cell.CoastDistance) * 0.65f;
                    break;
            }

            return score;
        }

        private static bool IsSectorCandidate(IA_UrbanSectorType targetSector, IA_UrbanSectorType sector)
        {
            if (sector == IA_UrbanSectorType.None || sector == IA_UrbanSectorType.Buffer)
            {
                return true;
            }

            if (sector == targetSector)
            {
                return true;
            }

            if (targetSector == IA_UrbanSectorType.Industrial && sector == IA_UrbanSectorType.Logistics)
            {
                return true;
            }

            if (targetSector == IA_UrbanSectorType.Military && sector == IA_UrbanSectorType.Logistics)
            {
                return true;
            }

            return false;
        }
    }
}
