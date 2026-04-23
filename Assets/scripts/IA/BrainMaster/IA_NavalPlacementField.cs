using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public static class IA_NavalPlacementField
    {
        public enum NavalPlacementKind
        {
            Shipyard,
            Pier,
            Platform
        }

        [Flags]
        public enum CellFlags : byte
        {
            None = 0,
            Water = 1 << 0,
            Shore = 1 << 1,
            DeepWater = 1 << 2,
            Blocked = 1 << 3
        }

        public struct CandidatePoint
        {
            public Vector3 Position;
            public float Score;
            public int CellX;
            public int CellY;
        }

        private struct CellData
        {
            public bool Resolved;
            public CellFlags Flags;
            public float Score;
            public float ValidUntil;
            public float BlacklistUntil;
            public string BlacklistReason;
        }

        private struct RegionCache
        {
            public bool Resolved;
            public float ValidUntil;
            public List<CandidatePoint> ShipyardCandidates;
            public List<CandidatePoint> PierCandidates;
            public List<CandidatePoint> PlatformCandidates;
        }

        private static readonly Dictionary<long, CellData> _cellCache = new Dictionary<long, CellData>(4096);
        private static readonly Dictionary<long, RegionCache> _regionCache = new Dictionary<long, RegionCache>(512);
        private static readonly List<CandidatePoint> _scratchCandidates = new List<CandidatePoint>(256);

        private static int _lastBudgetFrame = -1;
        private static float _frameBudgetMsUsed;
        private static float _frameBudgetMs = 3.5f;

        public static float CellSize = 48f;
        public static float RegionSize = 384f;
        public static float CellTTL = 20f;
        public static float RegionTTL = 12f;
        public static LayerMask WaterLayerMask = 0;
        public static LayerMask BlockerLayerMask = 0;

        public static void Configure(LayerMask waterMask, LayerMask blockerMask, float cellSize = 48f, float regionSize = 384f, float frameBudgetMs = 3.5f)
        {
            WaterLayerMask = waterMask;
            BlockerLayerMask = blockerMask;
            CellSize = Mathf.Max(16f, cellSize);
            RegionSize = Mathf.Max(CellSize * 4f, regionSize);
            _frameBudgetMs = Mathf.Max(0.5f, frameBudgetMs);
        }

        public static void BeginFrame()
        {
            if (_lastBudgetFrame == Time.frameCount)
            {
                return;
            }

            _lastBudgetFrame = Time.frameCount;
            _frameBudgetMsUsed = 0f;
        }

        public static bool TryReserveBudget(float estimatedMs)
        {
            BeginFrame();
            if (_frameBudgetMsUsed + estimatedMs > _frameBudgetMs)
            {
                return false;
            }

            _frameBudgetMsUsed += estimatedMs;
            return true;
        }

        public static void InvalidateAll()
        {
            _cellCache.Clear();
            _regionCache.Clear();
        }

        public static void InvalidateAround(Vector3 worldPos, float radius)
        {
            int minX = WorldToCellX(worldPos.x - radius);
            int maxX = WorldToCellX(worldPos.x + radius);
            int minY = WorldToCellY(worldPos.z - radius);
            int maxY = WorldToCellY(worldPos.z + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    _cellCache.Remove(CellKey(x, y));
                }
            }

            int regionMinX = WorldToRegionX(worldPos.x - radius);
            int regionMaxX = WorldToRegionX(worldPos.x + radius);
            int regionMinY = WorldToRegionY(worldPos.z - radius);
            int regionMaxY = WorldToRegionY(worldPos.z + radius);

            for (int y = regionMinY; y <= regionMaxY; y++)
            {
                for (int x = regionMinX; x <= regionMaxX; x++)
                {
                    _regionCache.Remove(RegionKey(x, y));
                }
            }
        }

        public static void MarkCellTemporarilyBlocked(Vector3 worldPos, string reason, float ttl = 30f)
        {
            int cx = WorldToCellX(worldPos.x);
            int cy = WorldToCellY(worldPos.z);
            long key = CellKey(cx, cy);

            CellData cell;
            if (!_cellCache.TryGetValue(key, out cell))
            {
                cell = new CellData();
            }

            cell.BlacklistUntil = Mathf.Max(cell.BlacklistUntil, Time.time + Mathf.Max(5f, ttl));
            cell.BlacklistReason = reason ?? string.Empty;
            cell.ValidUntil = 0f;
            _cellCache[key] = cell;

            int rx = WorldToRegionX(worldPos.x);
            int ry = WorldToRegionY(worldPos.z);
            _regionCache.Remove(RegionKey(rx, ry));
        }

        public static bool TryGetBestCandidate(
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            NavalPlacementKind kind,
            Func<Vector3, bool> cheapValidator,
            out Vector3 bestPoint)
        {
            bestPoint = anchor;

            int anchorRegionX = WorldToRegionX(anchor.x);
            int anchorRegionY = WorldToRegionY(anchor.z);

            _scratchCandidates.Clear();

            int regionRadius = Mathf.Max(1, Mathf.CeilToInt(maxRadius / RegionSize));
            for (int ry = anchorRegionY - regionRadius; ry <= anchorRegionY + regionRadius; ry++)
            {
                for (int rx = anchorRegionX - regionRadius; rx <= anchorRegionX + regionRadius; rx++)
                {
                    if (!TryAppendRegionCandidates(rx, ry, kind, _scratchCandidates))
                    {
                        continue;
                    }
                }
            }

            if (_scratchCandidates.Count == 0)
            {
                return false;
            }

            Vector3 flatAnchor = Flatten(anchor);
            CandidatePoint best = default;
            bool found = false;
            float bestScore = float.MinValue;

            for (int i = 0; i < _scratchCandidates.Count; i++)
            {
                CandidatePoint c = _scratchCandidates[i];
                float distance = Vector3.Distance(flatAnchor, Flatten(c.Position));
                if (distance < minRadius || distance > maxRadius)
                {
                    continue;
                }

                long key = CellKey(c.CellX, c.CellY);
                CellData cell;
                if (_cellCache.TryGetValue(key, out cell) && cell.BlacklistUntil > Time.time)
                {
                    continue;
                }

                if (cheapValidator != null && !cheapValidator(c.Position))
                {
                    continue;
                }

                float distScore = 1f - Mathf.Clamp01(distance / Mathf.Max(maxRadius, 1f));
                float finalScore = c.Score + (distScore * 0.35f);

                if (!found || finalScore > bestScore)
                {
                    best = c;
                    bestScore = finalScore;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            bestPoint = best.Position;
            return true;
        }

        private static bool TryAppendRegionCandidates(int regionX, int regionY, NavalPlacementKind kind, List<CandidatePoint> target)
        {
            long key = RegionKey(regionX, regionY);
            RegionCache cache;
            if (_regionCache.TryGetValue(key, out cache) && cache.Resolved && cache.ValidUntil > Time.time)
            {
                AppendByKind(cache, kind, target);
                return true;
            }

            if (!TryReserveBudget(0.45f))
            {
                return false;
            }

            cache = BuildRegion(regionX, regionY);
            _regionCache[key] = cache;
            AppendByKind(cache, kind, target);
            return true;
        }

        private static void AppendByKind(RegionCache cache, NavalPlacementKind kind, List<CandidatePoint> target)
        {
            List<CandidatePoint> source = null;
            switch (kind)
            {
                case NavalPlacementKind.Shipyard:
                    source = cache.ShipyardCandidates;
                    break;
                case NavalPlacementKind.Pier:
                    source = cache.PierCandidates;
                    break;
                case NavalPlacementKind.Platform:
                    source = cache.PlatformCandidates;
                    break;
            }

            if (source == null || source.Count == 0)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private static RegionCache BuildRegion(int regionX, int regionY)
        {
            RegionCache cache = new RegionCache
            {
                Resolved = true,
                ValidUntil = Time.time + RegionTTL,
                ShipyardCandidates = new List<CandidatePoint>(12),
                PierCandidates = new List<CandidatePoint>(12),
                PlatformCandidates = new List<CandidatePoint>(12)
            };

            float baseX = regionX * RegionSize;
            float baseY = regionY * RegionSize;

            int cellsPerSide = Mathf.Max(4, Mathf.RoundToInt(RegionSize / CellSize));

            for (int localY = 0; localY < cellsPerSide; localY++)
            {
                for (int localX = 0; localX < cellsPerSide; localX++)
                {
                    int cx = WorldToCellX(baseX + (localX * CellSize));
                    int cy = WorldToCellY(baseY + (localY * CellSize));

                    CellData cell = ResolveCell(cx, cy);
                    if (!cell.Resolved)
                    {
                        continue;
                    }

                    if ((cell.Flags & CellFlags.Blocked) != 0)
                    {
                        continue;
                    }

                    Vector3 pos = CellCenter(cx, cy);

                    if ((cell.Flags & CellFlags.Shore) != 0)
                    {
                        float shoreBias = 0.9f + cell.Score;
                        cache.ShipyardCandidates.Add(new CandidatePoint
                        {
                            Position = pos,
                            Score = shoreBias,
                            CellX = cx,
                            CellY = cy
                        });

                        cache.PierCandidates.Add(new CandidatePoint
                        {
                            Position = pos,
                            Score = shoreBias + 0.1f,
                            CellX = cx,
                            CellY = cy
                        });
                    }

                    if ((cell.Flags & CellFlags.DeepWater) != 0)
                    {
                        cache.PlatformCandidates.Add(new CandidatePoint
                        {
                            Position = pos,
                            Score = 1.1f + cell.Score,
                            CellX = cx,
                            CellY = cy
                        });

                        cache.ShipyardCandidates.Add(new CandidatePoint
                        {
                            Position = pos,
                            Score = 0.45f + cell.Score,
                            CellX = cx,
                            CellY = cy
                        });
                    }
                }
            }

            SortAndTrim(cache.ShipyardCandidates, 20);
            SortAndTrim(cache.PierCandidates, 20);
            SortAndTrim(cache.PlatformCandidates, 20);

            return cache;
        }

        private static CellData ResolveCell(int cx, int cy)
        {
            long key = CellKey(cx, cy);

            CellData cell;
            if (_cellCache.TryGetValue(key, out cell) && cell.ValidUntil > Time.time)
            {
                return cell;
            }

            cell = new CellData
            {
                Resolved = true,
                ValidUntil = Time.time + CellTTL,
                Flags = CellFlags.None,
                Score = 0f
            };

            Vector3 center = CellCenter(cx, cy);
            bool isWater = IsWater(center);
            bool isBlocked = IsBlocked(center);

            if (isBlocked)
            {
                cell.Flags |= CellFlags.Blocked;
            }

            if (isWater)
            {
                cell.Flags |= CellFlags.Water;

                int waterNeighbors = CountWaterNeighbors(cx, cy);
                if (waterNeighbors >= 2 && waterNeighbors <= 6)
                {
                    cell.Flags |= CellFlags.Shore;
                    cell.Score += 0.35f;
                }

                if (waterNeighbors >= 7)
                {
                    cell.Flags |= CellFlags.DeepWater;
                    cell.Score += 0.55f;
                }
            }

            _cellCache[key] = cell;
            return cell;
        }

        private static int CountWaterNeighbors(int cx, int cy)
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

                    Vector3 p = CellCenter(cx + ox, cy + oy);
                    if (IsWater(p))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsWater(Vector3 point)
        {
            if (WaterLayerMask.value == 0)
            {
                return false;
            }

            Vector3 origin = point + Vector3.up * 100f;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 300f, WaterLayerMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return false;
        }

        private static bool IsBlocked(Vector3 point)
        {
            if (BlockerLayerMask.value == 0)
            {
                return false;
            }

            return Physics.CheckSphere(point + Vector3.up * 1.5f, Mathf.Max(4f, CellSize * 0.30f), BlockerLayerMask, QueryTriggerInteraction.Ignore);
        }

        private static void SortAndTrim(List<CandidatePoint> list, int maxCount)
        {
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (list.Count > maxCount)
            {
                list.RemoveRange(maxCount, list.Count - maxCount);
            }
        }

        private static int WorldToCellX(float x)
        {
            return Mathf.FloorToInt(x / CellSize);
        }

        private static int WorldToCellY(float y)
        {
            return Mathf.FloorToInt(y / CellSize);
        }

        private static int WorldToRegionX(float x)
        {
            return Mathf.FloorToInt(x / RegionSize);
        }

        private static int WorldToRegionY(float y)
        {
            return Mathf.FloorToInt(y / RegionSize);
        }

        private static Vector3 CellCenter(int cx, int cy)
        {
            return new Vector3((cx + 0.5f) * CellSize, 0f, (cy + 0.5f) * CellSize);
        }

        private static long CellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private static long RegionKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private static Vector3 Flatten(Vector3 p)
        {
            p.y = 0f;
            return p;
        }
    }
}
