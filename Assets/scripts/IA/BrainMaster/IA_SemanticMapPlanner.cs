using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_SemanticMapPlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly Dictionary<Vector2Int, IA_SemanticCell> _cells = new Dictionary<Vector2Int, IA_SemanticCell>();

        // --- Reconstrucao incremental limitada por tempo por tick ---
        private int _rebuildCellCursor = int.MinValue;
        private int _rebuildRadius = 0;
        private Vector3 _rebuildCenter = Vector3.zero;
        private float _rebuildCellSize = 24f;
        private int _occupiedCount = 0;

        // Distância à costa depende do terreno, não da quantidade de estruturas.
        private readonly Dictionary<Vector2Int, float> _coastDistanceCache = new Dictionary<Vector2Int, float>();

        public int ScanRadiusInCells = 12; // Reduzido de 14 para 12 → 25x25=625 células em vez de 29x29=841
        public string LastSummary { get; private set; }

        public IA_SemanticMapPlanner(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_SemanticMapPlanner"; }
        }

        // Intervalo reduzido: reconstrução completa ocorre em fatias, entao pode rodar mais rápido
        public float Interval
        {
            get { return 0.35f; }
        }

        // Limite aplicado dentro do laço; uma única célula pode excedê-lo, mas
        // o planejador não processa mais uma coluna inteira no mesmo frame.
        public float BudgetMs
        {
            get { return 3.50f; }
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

            // Inicia uma varredura após a anterior ou quando o mapa mudou de área.
            Vector3 center = ResolveReferenceCenter();
            float cellSize = Mathf.Max(8f, _context.MapAnalyzer.CellSize);
            int radius = Mathf.Clamp(ScanRadiusInCells, 6, 22);
            int cellsPerAxis = radius * 2 + 1;
            int totalCells = cellsPerAxis * cellsPerAxis;
            bool mapRegionChanged = _rebuildCellCursor != int.MinValue
                && (Mathf.Abs(center.x - _rebuildCenter.x) > cellSize * 3f
                    || Mathf.Abs(center.z - _rebuildCenter.z) > cellSize * 3f
                    || Mathf.Abs(cellSize - _rebuildCellSize) > 0.01f
                    || radius != _rebuildRadius);

            bool needsReset = _rebuildCellCursor == int.MinValue
                              || _rebuildCellCursor >= totalCells
                              || mapRegionChanged;

            if (needsReset)
            {
                _rebuildCenter = center;
                _rebuildCellSize = cellSize;
                _rebuildRadius = radius;
                _rebuildCellCursor = 0;
                if (mapRegionChanged)
                {
                    _cells.Clear();
                    _coastDistanceCache.Clear();
                }

                _occupiedCount = 0;
            }

            // Preserva as células da última varredura e publica atualizações
            // incrementais sem bloquear o frame para processar uma coluna inteira.
            float cs = _rebuildCellSize;
            Vector3 rc = _rebuildCenter;
            int rr = _rebuildRadius;
            long sliceStart = Stopwatch.GetTimestamp();
            while (_rebuildCellCursor < totalCells)
            {
                int x = _rebuildCellCursor / cellsPerAxis - rr;
                int z = _rebuildCellCursor % cellsPerAxis - rr;
                Vector3 position = rc + new Vector3(x * cs, 0f, z * cs);
                IA_MapCell mapCell = _context.MapAnalyzer.SampleCell(position);
                if (mapCell != null)
                {
                    IA_SemanticCell semanticCell = BuildCell(mapCell, rc, cs);
                    if (semanticCell.Occupied)
                    {
                        _occupiedCount++;
                    }

                    _cells[semanticCell.Index] = semanticCell;
                }

                _rebuildCellCursor++;
                double elapsedMs = (Stopwatch.GetTimestamp() - sliceStart) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs >= Mathf.Max(0.5f, BudgetMs))
                {
                    break;
                }
            }

            if (_rebuildCellCursor >= totalCells)
            {
                // Varredura completa; o próximo tick começa outra atualização
                // sem descartar o mapa semântico já publicado.
                LastSummary = "cells=" + _cells.Count + " occupied=" + _occupiedCount + " center=" + rc;
                _rebuildCellCursor = int.MinValue;
            }
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
                // Threat removido da construcao de celula — avaliado sob demanda por quem precisar
                Threat = 0f,
                BaseDistance = Vector3.Distance(Flatten(position), Flatten(baseCenter)),
                CoastDistance = EstimateCoastDistanceCached(position, cellSize),
                Clearance = EstimateClearance(position)
            };
        }

        // Cache de distância à costa — evita recomputar 48 SampleCell por célula a cada varredura
        private float EstimateCoastDistanceCached(Vector3 position, float step)
        {
            Vector2Int key = ToIndex(position);
            float cached;
            if (_coastDistanceCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            float value = EstimateCoastDistance(position, step);
            _coastDistanceCache[key] = value;
            return value;
        }

        private float EstimateCoastDistance(Vector3 position, float step)
        {
            IA_MapCell current = _context.MapAnalyzer.SampleCell(position);
            if (current != null && (current.Terrain == IA_TerrainType.Coast || current.Terrain == IA_TerrainType.Water))
            {
                return 0f;
            }

            // Reduzido de 6 aneis para 4, mas com 6 probes (era 8) — 24 vs 48 SampleCell
            float maxDistance = step * 4f;
            for (int ring = 1; ring <= 4; ring++)
            {
                float radius = step * ring;
                for (int i = 0; i < 6; i++)
                {
                    float angle = ((360f / 6f) * i) * Mathf.Deg2Rad;
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
            // Limitado a 12 estruturas mais próximas para não escalar O(n) com bases grandes
            int limit = Mathf.Min(_context.WorldState.OwnStructures.Count, 12);
            for (int i = 0; i < limit; i++)
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
