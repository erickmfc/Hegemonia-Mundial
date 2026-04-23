// ARQUIVO 3: IA_SpatialGrid.cs
// ============================================================================
using System;
using UnityEngine;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-855)]
    public sealed class IA_SpatialGrid : MonoBehaviour
    {
        public enum CellType : byte
        {
            Unknown = 0,
            Land = 1,
            Water = 2,
            Coast = 3,
            Blocked = 4
        }

        [Serializable]
        public struct GridCell
        {
            public Vector3 center;
            public CellType type;
            public byte heightBand;
            public float score;
            public float validUntil;
        }

        private Vector3 _origin;
        private int _width;
        private int _height;
        private float _cellSize;
        private LayerMask _waterMask;
        private LayerMask _blockerMask;
        private LayerMask _landMask;
        private GridCell[,] _cells;
        private readonly Collider[] _blockerHits = new Collider[16];
        private int _nextColumn;
        private bool _configured;

        public void Configure(Vector3 origin, int width, int height, float cellSize, LayerMask waterMask, LayerMask blockerMask, LayerMask landMask)
        {
            _origin = origin;
            _width = Mathf.Max(16, width);
            _height = Mathf.Max(16, height);
            _cellSize = Mathf.Max(8f, cellSize);
            _waterMask = waterMask;
            _blockerMask = blockerMask;
            _landMask = landMask;
            _cells = new GridCell[_width, _height];
            _nextColumn = 0;
            _configured = true;
        }

        public void TickGrid(float now, int columnsPerTick, IA_MasterController.RuntimeSeverity severity)
        {
            if (!_configured || _cells == null)
            {
                return;
            }

            int columns = Mathf.Max(1, columnsPerTick);
            if (severity == IA_MasterController.RuntimeSeverity.Emergency)
            {
                columns = 1;
            }
            else if (severity == IA_MasterController.RuntimeSeverity.Throttled)
            {
                columns = Mathf.Max(1, columns - 1);
            }

            for (int c = 0; c < columns; c++)
            {
                if (_nextColumn >= _width)
                {
                    _nextColumn = 0;
                }

                for (int y = 0; y < _height; y++)
                {
                    _cells[_nextColumn, y] = ResolveCell(_nextColumn, y, now);
                }

                _nextColumn++;
            }
        }

        public bool IsLikelyAcrossOcean(Vector3 from, Vector3 to, float minimumDistance)
        {
            float dist = Vector3.Distance(Flatten(from), Flatten(to));
            if (dist < minimumDistance)
            {
                return false;
            }

            int steps = Mathf.Clamp(Mathf.RoundToInt(dist / Mathf.Max(24f, _cellSize)), 6, 64);
            int waterHits = 0;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(from, to, t);
                GridCell cell;
                if (!TryGetCell(p, out cell))
                {
                    continue;
                }
                if (cell.type == CellType.Water || cell.type == CellType.Coast)
                {
                    waterHits++;
                }
            }

            return waterHits > steps * 0.35f;
        }

        public Vector3 FindBestNavalStagingPoint(Vector3 from, Vector3 toward)
        {
            return FindBestCellNearLine(from, toward, CellType.Coast, 1600f, 1.15f);
        }

        public Vector3 FindBestLandingPoint(Vector3 nearTarget, float radius)
        {
            return FindBestCellByType(nearTarget, radius, CellType.Coast, true);
        }

        public Vector3 FindBestEconomicCoast(Vector3 from)
        {
            return FindBestCellByType(from, 1800f, CellType.Coast, false);
        }

        public Vector3 FindScoutPoint(Vector3 baseCenter, Vector3 enemyHint)
        {
            if (enemyHint != Vector3.zero)
            {
                return FindBestCellNearLine(baseCenter, enemyHint, CellType.Land, 2200f, 1.0f);
            }

            return FindBestCellByType(baseCenter, 1800f, CellType.Land, false);
        }

        public bool TryGetCell(Vector3 world, out GridCell cell)
        {
            int x, y;
            if (!WorldToCell(world, out x, out y))
            {
                cell = default;
                return false;
            }

            cell = _cells[x, y];
            return true;
        }

        private GridCell ResolveCell(int x, int y, float now)
        {
            Vector3 center = CellCenter(x, y);
            GridCell cell = new GridCell
            {
                center = center,
                type = CellType.Unknown,
                heightBand = 0,
                score = 0f,
                validUntil = now + 18f
            };

            bool blocked = IsBlocked(center);
            if (blocked)
            {
                cell.type = CellType.Blocked;
                cell.score = -1f;
                return cell;
            }

            bool water = IsWater(center);
            if (water)
            {
                int waterNeighbors = CountNeighborType(x, y, CellType.Water, true);
                cell.type = (waterNeighbors >= 2 && waterNeighbors <= 6) ? CellType.Coast : CellType.Water;
                cell.score = cell.type == CellType.Coast ? 0.8f : 0.55f;
            }
            else
            {
                bool nearWater = HasAdjacentWater(x, y);
                cell.type = nearWater ? CellType.Coast : CellType.Land;
                cell.score = nearWater ? 0.9f : 1.0f;
            }

            cell.heightBand = ResolveHeightBand(center);
            return cell;
        }

        private Vector3 FindBestCellByType(Vector3 around, float radius, CellType type, bool preferNearest)
        {
            int cx, cy;
            if (!WorldToCell(around, out cx, out cy))
            {
                return around;
            }

            int r = Mathf.Max(1, Mathf.CeilToInt(radius / _cellSize));
            float bestScore = float.MinValue;
            Vector3 best = around;

            for (int oy = -r; oy <= r; oy++)
            {
                for (int ox = -r; ox <= r; ox++)
                {
                    int x = cx + ox;
                    int y = cy + oy;
                    if (!Inside(x, y))
                    {
                        continue;
                    }

                    GridCell cell = _cells[x, y];
                    if (cell.type != type)
                    {
                        continue;
                    }

                    float d = Vector3.Distance(Flatten(around), Flatten(cell.center));
                    if (d > radius)
                    {
                        continue;
                    }

                    float score = cell.score;
                    score += preferNearest ? (1f - Mathf.Clamp01(d / Mathf.Max(radius, 1f))) : (Mathf.Clamp01(d / Mathf.Max(radius, 1f)) * 0.2f);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cell.center;
                    }
                }
            }

            return best;
        }

        private Vector3 FindBestCellNearLine(Vector3 from, Vector3 to, CellType desired, float searchRadius, float lineBias)
        {
            int steps = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(Flatten(from), Flatten(to)) / Mathf.Max(_cellSize, 24f)), 8, 48);
            Vector3 best = from;
            float bestScore = float.MinValue;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(from, to, t);
                Vector3 candidate = FindBestCellByType(p, searchRadius * 0.18f, desired, true);
                GridCell cell;
                if (!TryGetCell(candidate, out cell))
                {
                    continue;
                }

                float along = Vector3.Distance(Flatten(from), Flatten(candidate));
                float score = cell.score + (along / Mathf.Max(searchRadius, 1f)) * lineBias;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private int CountNeighborType(int x, int y, CellType type, bool dynamicWaterCheck)
        {
            int count = 0;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }
                    int nx = x + ox;
                    int ny = y + oy;
                    if (!Inside(nx, ny))
                    {
                        continue;
                    }

                    if (dynamicWaterCheck)
                    {
                        if (IsWater(CellCenter(nx, ny)))
                        {
                            count++;
                        }
                    }
                    else if (_cells[nx, ny].type == type)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private bool HasAdjacentWater(int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }
                    int nx = x + ox;
                    int ny = y + oy;
                    if (!Inside(nx, ny))
                    {
                        continue;
                    }
                    if (IsWater(CellCenter(nx, ny)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool WorldToCell(Vector3 world, out int x, out int y)
        {
            Vector3 local = world - _origin;
            x = Mathf.FloorToInt(local.x / _cellSize);
            y = Mathf.FloorToInt(local.z / _cellSize);
            return Inside(x, y);
        }

        private bool Inside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        private Vector3 CellCenter(int x, int y)
        {
            return _origin + new Vector3((x + 0.5f) * _cellSize, 0f, (y + 0.5f) * _cellSize);
        }

        private bool IsWater(Vector3 point)
        {
            if (_waterMask.value == 0)
            {
                return false;
            }
            RaycastHit hit;
            return Physics.Raycast(point + Vector3.up * 220f, Vector3.down, out hit, 440f, _waterMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsBlocked(Vector3 point)
        {
            if (_blockerMask.value == 0)
            {
                return false;
            }
            return Physics.OverlapSphereNonAlloc(point + Vector3.up * 1.5f, Mathf.Max(5f, _cellSize * 0.25f), _blockerHits, _blockerMask, QueryTriggerInteraction.Ignore) > 0;
        }

        private byte ResolveHeightBand(Vector3 point)
        {
            RaycastHit hit;
            if (!Physics.Raycast(point + Vector3.up * 220f, Vector3.down, out hit, 440f, _landMask, QueryTriggerInteraction.Ignore))
            {
                return 0;
            }

            float y = hit.point.y;
            if (y < 5f) return 0;
            if (y < 15f) return 1;
            if (y < 30f) return 2;
            if (y < 60f) return 3;
            return 4;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}