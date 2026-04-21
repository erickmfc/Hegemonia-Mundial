using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ZonePlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly Dictionary<IA_UrbanSectorType, Vector3> _anchors = new Dictionary<IA_UrbanSectorType, Vector3>();

        public string LastSummary { get; private set; }

        public IA_ZonePlanner(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_ZonePlanner"; }
        }

        public float Interval
        {
            get { return 7.5f; }
        }

        public float BudgetMs
        {
            get { return 0.35f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (_context == null || _context.SemanticMapPlanner == null)
            {
                return;
            }

            RebuildZones();
        }

        public IA_UrbanSectorType ResolveSector(IA_ZoneType zone)
        {
            switch (zone)
            {
                case IA_ZoneType.Core:
                    return IA_UrbanSectorType.Command;
                case IA_ZoneType.Economy:
                    return IA_UrbanSectorType.Industrial;
                case IA_ZoneType.Military:
                case IA_ZoneType.Defense:
                case IA_ZoneType.Frontline:
                    return IA_UrbanSectorType.Military;
                case IA_ZoneType.Air:
                    return IA_UrbanSectorType.Airfield;
                case IA_ZoneType.Naval:
                case IA_ZoneType.Coast:
                    return IA_UrbanSectorType.Naval;
                default:
                    return IA_UrbanSectorType.Logistics;
            }
        }

        public bool TryGetAnchor(IA_ZoneType zone, out Vector3 anchor)
        {
            return TryGetAnchor(ResolveSector(zone), out anchor);
        }

        public bool TryGetAnchor(IA_UrbanSectorType sector, out Vector3 anchor)
        {
            return _anchors.TryGetValue(sector, out anchor) && anchor != Vector3.zero;
        }

        private void RebuildZones()
        {
            _anchors.Clear();

            Vector3 baseCenter = ResolveBaseCenter();
            Vector3 enemyAnchor = Vector3.zero;
            bool hasEnemyAnchor = _context.WorldState != null
                                  && _context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out enemyAnchor);

            Vector3 frontlineDirection = hasEnemyAnchor ? Flatten(enemyAnchor - baseCenter) : Vector3.forward;
            if (frontlineDirection.sqrMagnitude < 0.01f)
            {
                frontlineDirection = Vector3.forward;
            }

            frontlineDirection.Normalize();
            Vector3 civilDirection = Quaternion.Euler(0f, -90f, 0f) * frontlineDirection;
            Vector3 industrialDirection = Quaternion.Euler(0f, 90f, 0f) * frontlineDirection;

            _anchors[IA_UrbanSectorType.Command] = baseCenter;
            _anchors[IA_UrbanSectorType.Naval] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Naval, frontlineDirection, 80f, 320f);
            _anchors[IA_UrbanSectorType.Airfield] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Airfield, -frontlineDirection, 220f, 460f);
            _anchors[IA_UrbanSectorType.Military] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Military, frontlineDirection, 140f, 320f);
            _anchors[IA_UrbanSectorType.Civil] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Civil, civilDirection, 120f, 260f);
            _anchors[IA_UrbanSectorType.Industrial] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Industrial, industrialDirection, 120f, 300f);
            _anchors[IA_UrbanSectorType.Logistics] = SelectBestAnchor(baseCenter, IA_UrbanSectorType.Logistics, industrialDirection, 90f, 220f);

            Vector3 manualAnchor;
            if (TryResolveManualAnchor(baseCenter, out manualAnchor, IA_ManualBuildPoint.OperationalRole.EstacionamentoNaval, IA_ManualBuildPoint.OperationalRole.PatrulhaNaval))
            {
                _anchors[IA_UrbanSectorType.Naval] = manualAnchor;
            }

            if (TryResolveManualAnchor(baseCenter, out manualAnchor, IA_ManualBuildPoint.OperationalRole.SortidaAerea, IA_ManualBuildPoint.OperationalRole.PatrulhaAerea, IA_ManualBuildPoint.OperationalRole.ReconAereo, IA_ManualBuildPoint.OperationalRole.AtaqueAereo))
            {
                _anchors[IA_UrbanSectorType.Airfield] = manualAnchor;
            }

            if (TryResolveManualAnchor(baseCenter, out manualAnchor, IA_ManualBuildPoint.OperationalRole.MobilizacaoTerrestre))
            {
                _anchors[IA_UrbanSectorType.Military] = manualAnchor;
            }

            if (TryResolveManualAnchor(baseCenter, out manualAnchor, IA_ManualBuildPoint.OperationalRole.TransporteTerrestre))
            {
                _anchors[IA_UrbanSectorType.Logistics] = manualAnchor;
            }

            foreach (IA_SemanticCell cell in _context.SemanticMapPlanner.Cells)
            {
                if (cell == null)
                {
                    continue;
                }

                IA_UrbanSectorType sector = ClassifyCell(cell, baseCenter, frontlineDirection, civilDirection, industrialDirection);
                _context.SemanticMapPlanner.SetSector(cell.Index, sector);
            }

            LastSummary =
                "command=" + baseCenter
                + " civil=" + ResolveAnchorSummary(IA_UrbanSectorType.Civil)
                + " industrial=" + ResolveAnchorSummary(IA_UrbanSectorType.Industrial)
                + " military=" + ResolveAnchorSummary(IA_UrbanSectorType.Military)
                + " air=" + ResolveAnchorSummary(IA_UrbanSectorType.Airfield)
                + " naval=" + ResolveAnchorSummary(IA_UrbanSectorType.Naval);
        }

        private IA_UrbanSectorType ClassifyCell(
            IA_SemanticCell cell,
            Vector3 baseCenter,
            Vector3 frontlineDirection,
            Vector3 civilDirection,
            Vector3 industrialDirection)
        {
            if (cell.Forbidden)
            {
                return IA_UrbanSectorType.Forbidden;
            }

            if (cell.Terrain == IA_TerrainType.Water)
            {
                return IA_UrbanSectorType.Naval;
            }

            if (cell.BaseDistance <= 100f)
            {
                return IA_UrbanSectorType.Command;
            }

            if (cell.CoastDistance <= Mathf.Max(18f, _context.MapAnalyzer.CellSize * 1.5f))
            {
                return IA_UrbanSectorType.Naval;
            }

            Vector3 offset = Flatten(cell.Center - baseCenter);
            if (offset.sqrMagnitude < 0.01f)
            {
                return IA_UrbanSectorType.Logistics;
            }

            offset.Normalize();

            if (TryGetAnchor(IA_UrbanSectorType.Airfield, out var airAnchor)
                && Vector3.Distance(Flatten(cell.Center), Flatten(airAnchor)) <= 130f
                && cell.Slope <= 8f
                && cell.Clearance >= 45f)
            {
                return IA_UrbanSectorType.Airfield;
            }

            if (Vector3.Dot(offset, frontlineDirection) >= 0.30f || cell.Threat >= 85f)
            {
                return IA_UrbanSectorType.Military;
            }

            if (Vector3.Dot(offset, civilDirection) >= 0.18f)
            {
                return IA_UrbanSectorType.Civil;
            }

            if (Vector3.Dot(offset, industrialDirection) >= 0.18f)
            {
                return IA_UrbanSectorType.Industrial;
            }

            return IA_UrbanSectorType.Logistics;
        }

        private Vector3 SelectBestAnchor(Vector3 baseCenter, IA_UrbanSectorType sector, Vector3 preferredDirection, float minRadius, float maxRadius)
        {
            preferredDirection.y = 0f;
            if (preferredDirection.sqrMagnitude < 0.01f)
            {
                preferredDirection = Vector3.forward;
            }

            preferredDirection.Normalize();

            Vector3 bestAnchor = Vector3.zero;
            float bestScore = float.MinValue;
            foreach (IA_SemanticCell cell in _context.SemanticMapPlanner.Cells)
            {
                if (cell == null || cell.Forbidden)
                {
                    continue;
                }

                float distance = Vector3.Distance(Flatten(cell.Center), Flatten(baseCenter));
                if (distance < minRadius || distance > maxRadius)
                {
                    continue;
                }

                if (sector == IA_UrbanSectorType.Naval)
                {
                    if (cell.Terrain != IA_TerrainType.Coast && cell.Terrain != IA_TerrainType.Water)
                    {
                        continue;
                    }
                }
                else if (!cell.Buildable || cell.Occupied || cell.Reserved)
                {
                    continue;
                }

                Vector3 direction = Flatten(cell.Center - baseCenter);
                if (direction.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                direction.Normalize();
                float alignment = Vector3.Dot(preferredDirection, direction);
                float score = alignment * 90f;
                score -= distance * 0.18f;
                score += Mathf.Clamp(cell.Clearance, 0f, 90f) * 0.35f;
                score -= cell.Threat * 0.20f;

                switch (sector)
                {
                    case IA_UrbanSectorType.Naval:
                        score += Mathf.Max(0f, 110f - cell.CoastDistance);
                        break;
                    case IA_UrbanSectorType.Airfield:
                        score += Mathf.Max(0f, 18f - cell.Slope) * 5f;
                        score += Mathf.Clamp(cell.Clearance, 0f, 140f) * 0.45f;
                        break;
                    case IA_UrbanSectorType.Civil:
                        score += Mathf.Clamp(cell.Clearance, 0f, 70f) * 0.20f;
                        break;
                    case IA_UrbanSectorType.Industrial:
                    case IA_UrbanSectorType.Logistics:
                        score += Mathf.Clamp(cell.Clearance, 0f, 90f) * 0.30f;
                        break;
                    case IA_UrbanSectorType.Military:
                        score += cell.Threat * 0.10f;
                        break;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAnchor = cell.Center;
                }
            }

            return bestAnchor;
        }

        private Vector3 ResolveBaseCenter()
        {
            if (_context.WorldState != null && _context.WorldState.BaseCenter != Vector3.zero)
            {
                return _context.WorldState.BaseCenter;
            }

            if (_context.Brain != null)
            {
                return _context.Brain.transform.position;
            }

            return Vector3.zero;
        }

        private string ResolveAnchorSummary(IA_UrbanSectorType sector)
        {
            Vector3 anchor;
            return TryGetAnchor(sector, out anchor) ? anchor.ToString() : "none";
        }

        private bool TryResolveManualAnchor(Vector3 baseCenter, out Vector3 anchor, params IA_ManualBuildPoint.OperationalRole[] roles)
        {
            anchor = Vector3.zero;
            if (_context == null || _context.Brain == null || !_context.Brain.UseManualBuildPoints || roles == null || roles.Length == 0)
            {
                return false;
            }

            IA_ManualBuildPoint[] manualPoints = _context.Brain.GetComponentsInChildren<IA_ManualBuildPoint>(true);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < manualPoints.Length; i++)
            {
                IA_ManualBuildPoint point = manualPoints[i];
                if (point == null || !point.IsUsableAsAnchor(_context.Brain))
                {
                    continue;
                }

                if (!MatchesRole(point.ManualRole, roles))
                {
                    continue;
                }

                float distance = Vector3.Distance(Flatten(point.transform.position), Flatten(baseCenter));
                if (found && distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                anchor = point.transform.position;
                found = true;
            }

            return found;
        }

        private static bool MatchesRole(IA_ManualBuildPoint.OperationalRole role, IA_ManualBuildPoint.OperationalRole[] roles)
        {
            for (int i = 0; i < roles.Length; i++)
            {
                if (role == roles[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
