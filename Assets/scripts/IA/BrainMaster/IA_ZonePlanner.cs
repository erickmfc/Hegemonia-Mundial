using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ZonePlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly Dictionary<IA_UrbanSectorType, Vector3> _anchors = new Dictionary<IA_UrbanSectorType, Vector3>();
        private readonly List<IA_ManualBuildPoint> _manualPointsBuffer = new List<IA_ManualBuildPoint>(64);

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

            float inicio = Time.realtimeSinceStartup;
            RebuildZones();
            float duracaoMs = (Time.realtimeSinceStartup - inicio) * 1000f;
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("zone_planner_ms", duracaoMs);
            if (duracaoMs > BudgetMs)
            {
                DiagnosticoDesempenhoJogo.RegistrarEvento("IA_ZonePlanner", $"Tick excedeu budget: {duracaoMs:0.00}ms (budget {BudgetMs:0.00}ms)");
            }
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

            Vector3 baseFlat = Flatten(baseCenter);
            Vector3 bestNaval = Vector3.zero;
            Vector3 bestAir = Vector3.zero;
            Vector3 bestMilitary = Vector3.zero;
            Vector3 bestCivil = Vector3.zero;
            Vector3 bestIndustrial = Vector3.zero;
            Vector3 bestLogistics = Vector3.zero;
            float bestNavalScore = float.MinValue;
            float bestAirScore = float.MinValue;
            float bestMilitaryScore = float.MinValue;
            float bestCivilScore = float.MinValue;
            float bestIndustrialScore = float.MinValue;
            float bestLogisticsScore = float.MinValue;

            Vector3 preferredFront = frontlineDirection;
            preferredFront.y = 0f;
            preferredFront = preferredFront.sqrMagnitude < 0.01f ? Vector3.forward : preferredFront.normalized;

            Vector3 preferredBack = -preferredFront;
            Vector3 preferredCivil = civilDirection;
            preferredCivil.y = 0f;
            preferredCivil = preferredCivil.sqrMagnitude < 0.01f ? Vector3.right : preferredCivil.normalized;

            Vector3 preferredIndustrial = industrialDirection;
            preferredIndustrial.y = 0f;
            preferredIndustrial = preferredIndustrial.sqrMagnitude < 0.01f ? Vector3.left : preferredIndustrial.normalized;

            const float navalMinSqr = 80f * 80f;
            const float navalMaxSqr = 320f * 320f;
            const float airMinSqr = 220f * 220f;
            const float airMaxSqr = 460f * 460f;
            const float militaryMinSqr = 140f * 140f;
            const float militaryMaxSqr = 320f * 320f;
            const float civilMinSqr = 120f * 120f;
            const float civilMaxSqr = 260f * 260f;
            const float industrialMinSqr = 120f * 120f;
            const float industrialMaxSqr = 300f * 300f;
            const float logisticsMinSqr = 90f * 90f;
            const float logisticsMaxSqr = 220f * 220f;

            foreach (IA_SemanticCell cell in _context.SemanticMapPlanner.Cells)
            {
                if (cell == null || cell.Forbidden)
                {
                    continue;
                }

                Vector3 cellFlat = Flatten(cell.Center);
                Vector3 offset = cellFlat - baseFlat;
                float sqrDist = offset.sqrMagnitude;
                if (sqrDist < 0.01f)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(sqrDist);
                Vector3 dir = offset / distance;

                float distancePenalty = distance * 0.18f;
                float baseClearance = Mathf.Clamp(cell.Clearance, 0f, 90f) * 0.35f;
                float threatPenalty = cell.Threat * 0.20f;

                bool navalCandidate = cell.Terrain == IA_TerrainType.Coast || cell.Terrain == IA_TerrainType.Water;
                bool buildCandidate = cell.Buildable && !cell.Occupied && !cell.Reserved;

                if (navalCandidate && sqrDist >= navalMinSqr && sqrDist <= navalMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredFront, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += Mathf.Max(0f, 110f - cell.CoastDistance);
                    if (score > bestNavalScore)
                    {
                        bestNavalScore = score;
                        bestNaval = cell.Center;
                    }
                }

                if (buildCandidate && sqrDist >= airMinSqr && sqrDist <= airMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredBack, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += Mathf.Max(0f, 18f - cell.Slope) * 5f;
                    score += Mathf.Clamp(cell.Clearance, 0f, 140f) * 0.45f;
                    if (score > bestAirScore)
                    {
                        bestAirScore = score;
                        bestAir = cell.Center;
                    }
                }

                if (buildCandidate && sqrDist >= militaryMinSqr && sqrDist <= militaryMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredFront, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += cell.Threat * 0.10f;
                    if (score > bestMilitaryScore)
                    {
                        bestMilitaryScore = score;
                        bestMilitary = cell.Center;
                    }
                }

                if (buildCandidate && sqrDist >= civilMinSqr && sqrDist <= civilMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredCivil, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += Mathf.Clamp(cell.Clearance, 0f, 70f) * 0.20f;
                    if (score > bestCivilScore)
                    {
                        bestCivilScore = score;
                        bestCivil = cell.Center;
                    }
                }

                if (buildCandidate && sqrDist >= industrialMinSqr && sqrDist <= industrialMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredIndustrial, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += Mathf.Clamp(cell.Clearance, 0f, 90f) * 0.30f;
                    if (score > bestIndustrialScore)
                    {
                        bestIndustrialScore = score;
                        bestIndustrial = cell.Center;
                    }
                }

                if (buildCandidate && sqrDist >= logisticsMinSqr && sqrDist <= logisticsMaxSqr)
                {
                    float alignment = Vector3.Dot(preferredIndustrial, dir);
                    float score = (alignment * 90f) - distancePenalty + baseClearance - threatPenalty;
                    score += Mathf.Clamp(cell.Clearance, 0f, 90f) * 0.30f;
                    if (score > bestLogisticsScore)
                    {
                        bestLogisticsScore = score;
                        bestLogistics = cell.Center;
                    }
                }
            }

            _anchors[IA_UrbanSectorType.Naval] = bestNaval;
            _anchors[IA_UrbanSectorType.Airfield] = bestAir;
            _anchors[IA_UrbanSectorType.Military] = bestMilitary;
            _anchors[IA_UrbanSectorType.Civil] = bestCivil;
            _anchors[IA_UrbanSectorType.Industrial] = bestIndustrial;
            _anchors[IA_UrbanSectorType.Logistics] = bestLogistics;

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

            _manualPointsBuffer.Clear();
            _context.Brain.GetComponentsInChildren(true, _manualPointsBuffer);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _manualPointsBuffer.Count; i++)
            {
                IA_ManualBuildPoint point = _manualPointsBuffer[i];
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
