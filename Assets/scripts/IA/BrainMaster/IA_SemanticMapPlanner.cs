using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_SemanticMapPlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly Dictionary<Vector2Int, IA_SemanticCell> _cells = new Dictionary<Vector2Int, IA_SemanticCell>();

        public int ScanRadiusInCells = 14;
        public string LastSummary { get; private set; }

        public IA_SemanticMapPlanner(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_SemanticMapPlanner"; }
        }

        public float Interval
        {
            get { return 5.5f; }
        }

        public float BudgetMs
        {
            get { return 0.40f; }
        }

        public IEnumerable<IA_SemanticCell> Cells
        {
            get { return _cells.Values; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (_context == null || _context.MapAnalyzer == null || _context.WorldState == null)
            {
                return;
            }

            RebuildGrid();
        }

        public bool TryGetCell(Vector3 worldPosition, out IA_SemanticCell cell)
        {
            return _cells.TryGetValue(ToIndex(worldPosition), out cell);
        }

        public void SetSector(Vector2Int index, IA_UrbanSectorType sector)
        {
            IA_SemanticCell cell;
            if (_cells.TryGetValue(index, out cell) && cell != null)
            {
                cell.Sector = sector;
            }
        }

        private void RebuildGrid()
        {
            _cells.Clear();

            Vector3 center = ResolveReferenceCenter();
            float cellSize = Mathf.Max(8f, _context.MapAnalyzer.CellSize);
            int radius = Mathf.Clamp(ScanRadiusInCells, 6, 28);
            int occupiedCount = 0;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    Vector3 position = center + new Vector3(x * cellSize, 0f, z * cellSize);
                    IA_MapCell mapCell = _context.MapAnalyzer.SampleCell(position);
                    if (mapCell == null)
                    {
                        continue;
                    }

                    IA_SemanticCell semanticCell = BuildCell(mapCell, center, cellSize);
                    if (semanticCell.Occupied)
                    {
                        occupiedCount++;
                    }

                    _cells[semanticCell.Index] = semanticCell;
                }
            }

            LastSummary = "cells=" + _cells.Count + " occupied=" + occupiedCount + " center=" + center;
        }

        private IA_SemanticCell BuildCell(IA_MapCell mapCell, Vector3 baseCenter, float cellSize)
        {
            Vector3 position = mapCell.Center;
            bool occupied = IsInsideOwnFootprint(position, 10f);
            bool reserved = occupied || IsInsideOwnFootprint(position, 22f);
            bool forbidden = mapCell.Terrain == IA_TerrainType.Water
                             || mapCell.Slope > 30f
                             || mapCell.ObstacleDensity > 0.80f;

            return new IA_SemanticCell
            {
                Index = ToIndex(position),
                Center = position,
                Terrain = mapCell.Terrain,
                LegacyZone = mapCell.Zone,
                Sector = IA_UrbanSectorType.None,
                Buildable = mapCell.BuildableLand && mapCell.Terrain != IA_TerrainType.Water,
                Occupied = occupied,
                Reserved = reserved,
                Forbidden = forbidden,
                Height = mapCell.Height,
                Slope = mapCell.Slope,
                Threat = _context.ThreatAnalyzer != null
                    ? _context.ThreatAnalyzer.EvaluateThreat(position, mapCell.Terrain == IA_TerrainType.Water ? IA_Domain.Naval : IA_Domain.Land)
                    : 0f,
                BaseDistance = Vector3.Distance(Flatten(position), Flatten(baseCenter)),
                CoastDistance = EstimateCoastDistance(position, cellSize),
                Clearance = EstimateClearance(position)
            };
        }

        private float EstimateCoastDistance(Vector3 position, float step)
        {
            IA_MapCell current = _context.MapAnalyzer.SampleCell(position);
            if (current != null && (current.Terrain == IA_TerrainType.Coast || current.Terrain == IA_TerrainType.Water))
            {
                return 0f;
            }

            float maxDistance = step * 6f;
            for (int ring = 1; ring <= 6; ring++)
            {
                float radius = step * ring;
                for (int i = 0; i < 8; i++)
                {
                    float angle = ((360f / 8f) * i) * Mathf.Deg2Rad;
                    Vector3 probe = position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell sample = _context.MapAnalyzer.SampleCell(probe);
                    if (sample != null && (sample.Terrain == IA_TerrainType.Coast || sample.Terrain == IA_TerrainType.Water))
                    {
                        return radius;
                    }
                }
            }

            return maxDistance;
        }

        private float EstimateClearance(Vector3 position)
        {
            float best = 220f;
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(Flatten(structure.transform.position), Flatten(position));
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private bool IsInsideOwnFootprint(Vector3 position, float extraPadding)
        {
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                Vector2 halfExtents = _context.MapAnalyzer.EstimateFootprint(structure, 10f);
                float paddingX = halfExtents.x + extraPadding;
                float paddingZ = halfExtents.y + extraPadding;
                Vector3 center = structure.transform.position;

                if (Mathf.Abs(position.x - center.x) <= paddingX && Mathf.Abs(position.z - center.z) <= paddingZ)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 ResolveReferenceCenter()
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

        private Vector2Int ToIndex(Vector3 position)
        {
            float size = Mathf.Max(8f, _context.MapAnalyzer != null ? _context.MapAnalyzer.CellSize : 24f);
            return new Vector2Int(Mathf.RoundToInt(position.x / size), Mathf.RoundToInt(position.z / size));
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
