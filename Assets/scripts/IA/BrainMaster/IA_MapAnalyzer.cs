using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_MapAnalyzer : IIAUpdateModule
    {
        private struct TerrainQueryCacheEntry
        {
            public Vector3 Result;
            public float ValidUntil;
        }

        private struct LandRouteCacheEntry
        {
            public bool Result;
            public float ValidUntil;
        }

        private const int MaxCachedCells = 4096;
        private readonly IA_WorldState _world;
        private readonly Dictionary<Vector2Int, IA_MapCell> _cells = new Dictionary<Vector2Int, IA_MapCell>();
        private readonly Dictionary<int, Vector2> _footprintCache = new Dictionary<int, Vector2>();
        private readonly Dictionary<string, TerrainQueryCacheEntry> _terrainQueryCache = new Dictionary<string, TerrainQueryCacheEntry>();
        private readonly Dictionary<string, LandRouteCacheEntry> _landRouteCache = new Dictionary<string, LandRouteCacheEntry>();
        private readonly Collider[] _obstacleBuffer = new Collider[96];
        private readonly NavMeshPath _sharedPath = new NavMeshPath();
        private bool _cachedHasNavMeshData = true;
        private float _nextNavMeshPresenceCheckTime;

        public float CellSize = 24f;

        public IA_MapAnalyzer(IA_WorldState world)
        {
            _world = world;
        }

        public string Name
        {
            get { return "IA_MapAnalyzer"; }
        }

        public float Interval
        {
            get { return 1.75f; }
        }

        public float BudgetMs
        {
            get { return 0.55f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (_world == null)
            {
                return;
            }

            Vector3 center = _world.BaseCenter;
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 pos = center + new Vector3(x * CellSize, 0f, z * CellSize);
                    SampleCell(pos);
                }
            }

            if (_cells.Count > MaxCachedCells)
            {
                _cells.Clear();
            }
        }

        public IA_MapCell SampleCell(Vector3 worldPosition)
        {
            Vector2Int index = ToIndex(worldPosition);
            IA_MapCell cell;
            if (_cells.TryGetValue(index, out cell))
            {
                return cell;
            }

            cell = BuildCell(worldPosition);
            _cells[index] = cell;
            return cell;
        }

        public bool IsZoneCompatible(IA_ZoneType zone, IA_TerrainType terrain, bool naval)
        {
            if (naval)
            {
                return terrain == IA_TerrainType.Water || terrain == IA_TerrainType.Coast;
            }

            switch (zone)
            {
                case IA_ZoneType.Naval:
                    return terrain == IA_TerrainType.Coast;
                case IA_ZoneType.Air:
                case IA_ZoneType.Core:
                case IA_ZoneType.Economy:
                    return terrain != IA_TerrainType.Water && terrain != IA_TerrainType.Coast;
                case IA_ZoneType.Military:
                case IA_ZoneType.Frontline:
                    return terrain != IA_TerrainType.Water && terrain != IA_TerrainType.Coast;
                case IA_ZoneType.Defense:
                case IA_ZoneType.Coast:
                    return terrain != IA_TerrainType.Water;
                default:
                    return terrain != IA_TerrainType.Water;
            }
        }

        public Vector2 EstimateFootprint(GameObject prefab, float fallback)
        {
            if (prefab == null)
            {
                return new Vector2(fallback, fallback);
            }

            int key = prefab.GetInstanceID();
            Vector2 cached;
            if (_footprintCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(prefab.transform.position, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector2 result = hasBounds
                ? new Vector2(Mathf.Max(2f, bounds.extents.x), Mathf.Max(2f, bounds.extents.z))
                : new Vector2(fallback, fallback);

            _footprintCache[key] = result;
            return result;
        }

        public bool WouldBlockRoute(Vector3 position, Vector2 halfExtents, Vector3 baseCenter)
        {
            if (baseCenter == Vector3.zero)
            {
                return false;
            }

            if (!HasAnyNavMeshData(Time.time, baseCenter))
            {
                return false;
            }

            if (Vector3.Distance(Flatten(position), Flatten(baseCenter)) > 180f)
            {
                return false;
            }

            if (Mathf.Max(halfExtents.x, halfExtents.y) <= 6f)
            {
                return false;
            }

            float radius = Mathf.Max(halfExtents.x, halfExtents.y) + 8f;
            Vector3 probeA = position + new Vector3(radius, 0f, 0f);
            Vector3 probeC = position + new Vector3(0f, 0f, radius);
            return !HasPath(baseCenter, probeA) && !HasPath(baseCenter, probeC);
        }

        private bool HasAnyNavMeshData(float now, Vector3 baseCenter)
        {
            if (now < _nextNavMeshPresenceCheckTime)
            {
                return _cachedHasNavMeshData;
            }

            Vector3 center = baseCenter;
            if (center == Vector3.zero && _world != null)
            {
                center = _world.BaseCenter;
            }

            bool found = false;
            NavMeshHit hit;
            const float sampleRadius = 60f;
            const float probeDistance = 80f;

            if (center != Vector3.zero && NavMesh.SamplePosition(center, out hit, sampleRadius, NavMesh.AllAreas))
            {
                found = true;
            }
            else
            {
                Vector3 probe = center;
                probe.x += probeDistance;
                if (NavMesh.SamplePosition(probe, out hit, sampleRadius, NavMesh.AllAreas))
                {
                    found = true;
                }
                else
                {
                    probe = center;
                    probe.x -= probeDistance;
                    if (NavMesh.SamplePosition(probe, out hit, sampleRadius, NavMesh.AllAreas))
                    {
                        found = true;
                    }
                    else
                    {
                        probe = center;
                        probe.z += probeDistance;
                        if (NavMesh.SamplePosition(probe, out hit, sampleRadius, NavMesh.AllAreas))
                        {
                            found = true;
                        }
                        else
                        {
                            probe = center;
                            probe.z -= probeDistance;
                            if (NavMesh.SamplePosition(probe, out hit, sampleRadius, NavMesh.AllAreas))
                            {
                                found = true;
                            }
                        }
                    }
                }
            }

            _cachedHasNavMeshData = found;
            _nextNavMeshPresenceCheckTime = now + 5f;
            return _cachedHasNavMeshData;
        }

        public Vector3 FindPointInTerrain(
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            int samples)
        {
            float now = Time.time;
            string queryKey = BuildTerrainQueryKey(anchor, desiredTerrain, minRadius, maxRadius, samples);
            TerrainQueryCacheEntry cached;
            if (_terrainQueryCache.TryGetValue(queryKey, out cached) && cached.ValidUntil > now)
            {
                return cached.Result;
            }

            Vector3 result = anchor;
            int count = Mathf.Clamp(samples, 8, 18);
            for (int ring = 0; ring < 4; ring++)
            {
                float radius = Mathf.Lerp(minRadius, maxRadius, ring / 3f);
                for (int i = 0; i < count; i++)
                {
                    float angle = (360f / count) * i * Mathf.Deg2Rad;
                    Vector3 test = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell cell = SampleCell(test);
                    if (desiredTerrain == IA_TerrainType.Water)
                    {
                        if (cell.Terrain == IA_TerrainType.Water || cell.Terrain == IA_TerrainType.Coast)
                        {
                            result = cell.Center;
                            CacheTerrainQueryResult(queryKey, result, now, true);
                            return result;
                        }
                    }
                    else if (desiredTerrain == IA_TerrainType.Land)
                    {
                        if (cell.Terrain != IA_TerrainType.Water && cell.BuildableLand)
                        {
                            result = cell.Center;
                            CacheTerrainQueryResult(queryKey, result, now, true);
                            return result;
                        }
                    }
                    else
                    {
                        if (cell.Terrain == desiredTerrain && cell.BuildableLand)
                        {
                            result = cell.Center;
                            CacheTerrainQueryResult(queryKey, result, now, true);
                            return result;
                        }
                    }
                }
            }

            CacheTerrainQueryResult(queryKey, anchor, now, false);
            return anchor;
        }

        public bool HasLandRouteCached(Vector3 from, Vector3 to, float ttlSeconds)
        {
            float now = Time.time;
            string key = BuildLandRouteCacheKey(from, to);
            LandRouteCacheEntry cached;
            if (_landRouteCache.TryGetValue(key, out cached) && cached.ValidUntil > now)
            {
                return cached.Result;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool result = HasPath(from, to);
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("pathfinding_ms", elapsedMs);
            }

            _landRouteCache[key] = new LandRouteCacheEntry
            {
                Result = result,
                ValidUntil = now + Mathf.Clamp(ttlSeconds, 2f, 20f)
            };
            return result;
        }

        private string BuildTerrainQueryKey(Vector3 anchor, IA_TerrainType desiredTerrain, float minRadius, float maxRadius, int samples)
        {
            int cellX = Mathf.RoundToInt(anchor.x / Mathf.Max(1f, CellSize * 2f));
            int cellZ = Mathf.RoundToInt(anchor.z / Mathf.Max(1f, CellSize * 2f));
            int minBucket = Mathf.RoundToInt(minRadius / 20f);
            int maxBucket = Mathf.RoundToInt(maxRadius / 20f);
            int sampleBucket = Mathf.Clamp(samples, 8, 32);
            return desiredTerrain + ":" + cellX + ":" + cellZ + ":" + minBucket + ":" + maxBucket + ":" + sampleBucket;
        }

        private void CacheTerrainQueryResult(string key, Vector3 result, float now, bool success)
        {
            if (_terrainQueryCache.Count > 256)
            {
                _terrainQueryCache.Clear();
            }

            _terrainQueryCache[key] = new TerrainQueryCacheEntry
            {
                Result = result,
                ValidUntil = now + (success ? 12f : 4f)
            };
        }

        private IA_MapCell BuildCell(Vector3 position)
        {
            float height;
            IA_TerrainType terrain = DetectTerrain(position, out height);

            float slope = ComputeSlope(position, height);
            int hitCount = Physics.OverlapSphereNonAlloc(new Vector3(position.x, height + 1f, position.z), 9f, _obstacleBuffer, ~0, QueryTriggerInteraction.Ignore);
            float obstacleDensity = Mathf.Clamp01(hitCount / 14f);

            IA_ZoneType zone = InferZone(position, terrain);
            bool buildableLand = terrain != IA_TerrainType.Water && slope < 28f && obstacleDensity < 0.55f;
            bool buildableWater = terrain == IA_TerrainType.Water || terrain == IA_TerrainType.Coast;

            return new IA_MapCell
            {
                Center = new Vector3(position.x, height, position.z),
                Terrain = terrain,
                Height = height,
                Slope = slope,
                BuildableLand = buildableLand,
                BuildableWater = buildableWater,
                ObstacleDensity = obstacleDensity,
                Zone = zone
            };
        }

        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[16];

        private IA_TerrainType DetectTerrain(Vector3 position, out float height)
        {
            ClassificacaoSuperficieMapa superficieMarcada;
            if (RegistroSuperficieMapa.TryClassify(position, out superficieMarcada, out height))
            {
                if (superficieMarcada == ClassificacaoSuperficieMapa.Agua)
                {
                    return IA_TerrainType.Water;
                }

                if (superficieMarcada == ClassificacaoSuperficieMapa.Costa)
                {
                    return IA_TerrainType.Coast;
                }

                if (superficieMarcada == ClassificacaoSuperficieMapa.Chao)
                {
                    int urbanHitsMarcado = CountUrbanObjects(position, height);
                    if (urbanHitsMarcado >= 6)
                    {
                        return IA_TerrainType.City;
                    }

                    if (CountObstacles(position, height) >= 8)
                    {
                        return IA_TerrainType.Choke;
                    }

                    return IA_TerrainType.Open;
                }
            }

            height = position.y;
            int hitCount = Physics.RaycastNonAlloc(new Vector3(position.x, 1200f, position.z), Vector3.down, _raycastBuffer, 2500f, ~0, QueryTriggerInteraction.Collide);
            if (hitCount == 0)
            {
                return IA_TerrainType.Unknown;
            }

            // Ordenação manual simples (Insertion Sort) para evitar alocação de Array.Sort/Lambda
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit temp = _raycastBuffer[i];
                int j = i - 1;
                while (j >= 0 && _raycastBuffer[j].distance > temp.distance)
                {
                    _raycastBuffer[j + 1] = _raycastBuffer[j];
                    j--;
                }
                _raycastBuffer[j + 1] = temp;
            }

            bool seenWater = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _raycastBuffer[i].collider;
                if (col == null) continue;

                // Tenta pegar o marcador sem alocar (GetComponentInParent é moderadamente custoso se chamado muito)
                MarcadorSuperficieMapa marcador = col.GetComponent<MarcadorSuperficieMapa>();
                if (marcador == null) marcador = col.GetComponentInParent<MarcadorSuperficieMapa>();

                if (marcador != null)
                {
                    float alturaMarcador;
                    if (!marcador.TrySampleSurfaceHeight(position, out alturaMarcador))
                    {
                        continue;
                    }

                    height = alturaMarcador;
                    if (marcador.TipoSuperficie == TipoSuperficieMapa.Agua)
                    {
                        seenWater = true;
                        continue;
                    }

                    if (seenWater)
                    {
                        return IA_TerrainType.Coast;
                    }

                    int urbanHitsMarcador = CountUrbanObjects(position, height);
                    if (urbanHitsMarcador >= 6) return IA_TerrainType.City;
                    if (CountObstacles(position, height) >= 8) return IA_TerrainType.Choke;

                    return IA_TerrainType.Open;
                }

                string n = IA_Text.Normalize(col.name);
                bool water = col.gameObject.layer == 4 || n.Contains("agua") || n.Contains("water") || n.Contains("ocean") || n.Contains("mar");
                if (water)
                {
                    seenWater = true;
                    height = _raycastBuffer[i].point.y;
                    continue;
                }

                if (n.Contains("bip") || n.Contains("bone"))
                {
                    continue;
                }

                height = _raycastBuffer[i].point.y;
                if (seenWater)
                {
                    return IA_TerrainType.Coast;
                }

                int urbanHits = CountUrbanObjects(position, height);
                if (urbanHits >= 6) return IA_TerrainType.City;
                if (CountObstacles(position, height) >= 8) return IA_TerrainType.Choke;

                return IA_TerrainType.Open;
            }

            if (seenWater)
            {
                return IA_TerrainType.Water;
            }

            return IA_TerrainType.Unknown;
        }

        private float ComputeSlope(Vector3 position, float height)
        {
            float sample = 3f;
            float h0 = height;
            float hx = SampleHeight(position + new Vector3(sample, 0f, 0f));
            float hz = SampleHeight(position + new Vector3(0f, 0f, sample));
            float gradient = Mathf.Abs(hx - h0) + Mathf.Abs(hz - h0);
            return Mathf.Atan(gradient / Mathf.Max(0.01f, sample)) * Mathf.Rad2Deg;
        }

        private float SampleHeight(Vector3 position)
        {
            float alturaMarcada;
            if (RegistroSuperficieMapa.TryGetAltura(position, TipoSuperficieMapa.Chao, out alturaMarcada))
            {
                return alturaMarcada;
            }

            RaycastHit hit;
            if (Physics.Raycast(new Vector3(position.x, 1000f, position.z), Vector3.down, out hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return position.y;
        }

        private int CountUrbanObjects(Vector3 position, float y)
        {
            int count = 0;
            int total = Physics.OverlapSphereNonAlloc(new Vector3(position.x, y + 1f, position.z), 18f, _obstacleBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < total; i++)
            {
                Collider col = _obstacleBuffer[i];
                if (col == null)
                {
                    continue;
                }

                if (col.GetComponentInParent<MarcadorSuperficieMapa>() != null)
                {
                    continue;
                }

                string n = IA_Text.Normalize(col.name);
                if (n.Contains("predio") || n.Contains("building") || n.Contains("casa") || n.Contains("urb"))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountObstacles(Vector3 position, float y)
        {
            int count = 0;
            int total = Physics.OverlapSphereNonAlloc(new Vector3(position.x, y + 1f, position.z), 14f, _obstacleBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < total; i++)
            {
                Collider col = _obstacleBuffer[i];
                if (col == null || col.isTrigger)
                {
                    continue;
                }

                if (col.GetComponentInParent<MarcadorSuperficieMapa>() != null)
                {
                    continue;
                }

                string n = IA_Text.Normalize(col.name);
                if (n.Contains("terrain") || n.Contains("terra") || n.Contains("agua") || n.Contains("water"))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private IA_ZoneType InferZone(Vector3 position, IA_TerrainType terrain)
        {
            float distBase = Vector3.Distance(Flatten(position), Flatten(_world.BaseCenter));
            if (distBase <= 65f)
            {
                return IA_ZoneType.Core;
            }

            if (terrain == IA_TerrainType.Water || terrain == IA_TerrainType.Coast)
            {
                return IA_ZoneType.Coast;
            }

            if (terrain == IA_TerrainType.City)
            {
                return IA_ZoneType.Defense;
            }

            if (distBase <= 150f)
            {
                return IA_ZoneType.Economy;
            }

            if (distBase <= 280f)
            {
                return IA_ZoneType.Military;
            }

            return IA_ZoneType.Frontline;
        }

        private bool HasPath(Vector3 from, Vector3 to)
        {
            bool calculated = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _sharedPath);
            return calculated && _sharedPath.status == NavMeshPathStatus.PathComplete;
        }

        private string BuildLandRouteCacheKey(Vector3 from, Vector3 to)
        {
            int fromX = Mathf.RoundToInt(from.x / 40f);
            int fromZ = Mathf.RoundToInt(from.z / 40f);
            int toX = Mathf.RoundToInt(to.x / 40f);
            int toZ = Mathf.RoundToInt(to.z / 40f);
            return fromX + ":" + fromZ + ">" + toX + ":" + toZ;
        }

        private Vector2Int ToIndex(Vector3 worldPosition)
        {
            float size = Mathf.Max(1f, CellSize);
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x / size), Mathf.RoundToInt(worldPosition.z / size));
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
