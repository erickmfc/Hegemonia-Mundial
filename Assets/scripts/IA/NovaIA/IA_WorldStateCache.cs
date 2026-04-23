// ARQUIVO 2: IA_WorldStateCache.cs
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-860)]
    public sealed class IA_WorldStateCache : MonoBehaviour
    {
        public enum DomainHint
        {
            Land = 0,
            Naval = 1,
            Air = 2
        }

        [Serializable]
        public struct TargetInfo
        {
            public string Name;
            public Vector3 Position;
            public DomainHint Domain;
            public bool IsStructure;
            public float Score;
        }

        [Serializable]
        public struct EnemyMemory
        {
            public int InstanceId;
            public string Name;
            public Vector3 Position;
            public float LastSeenTime;
            public DomainHint Domain;
            public bool IsStructure;
            public float Threat;
        }

        [Serializable]
        public struct WorldSnapshot
        {
            public Vector3 BaseCenter;
            public Vector3 LastKnownEnemyAnchor;
            public bool UnderThreat;
            public bool CanExpand;
            public bool LowOil;
            public bool LowLogistics;
            public bool EnemyAcrossOcean;
            public int EnemyVisibleCount;
            public int CityHallCount;
            public int BarracksCount;
            public int FactoryCount;
            public int WarehouseCount;
            public int ShipyardCount;
            public int PierCount;
            public int PlatformCount;
            public int AirportCount;
            public int CarrierCount;
            public int NavalTransportCount;
            public int NavalUnits;
            public int AirUnits;
            public int GroundUnits;
            public float LastUpdated;
        }

        [Serializable]
        private struct RuntimeCacheEntry
        {
            public string NormalizedName;
            public DomainHint Domain;
            public bool IsStructure;
            public bool IsRadar;
            public bool IsTransport;
            public bool IsCarrier;
            public bool IsNavalTransport;
            public bool IsOil;
            public bool IsLogistics;
            public float VisionRadius;
        }

        private struct RegistryEntry
        {
            public IdentidadeUnidade identity;
            public GameObject gameObject;
            public Transform transform;
            public int teamId;
            public RuntimeCacheEntry cache;
        }

        private static readonly HashSet<IdentidadeUnidade> _globalRegistry = new HashSet<IdentidadeUnidade>();
        private static readonly List<IdentidadeUnidade> _scratchRegistry = new List<IdentidadeUnidade>(512);
        private static readonly Dictionary<int, RuntimeCacheEntry> _runtimeCache = new Dictionary<int, RuntimeCacheEntry>(1024);
        private static int _registryVersion = 1;

        private readonly List<RegistryEntry> _globalEntries = new List<RegistryEntry>(256);
        private readonly List<GameObject> _ownUnits = new List<GameObject>(128);
        private readonly List<GameObject> _ownStructures = new List<GameObject>(128);
        private readonly List<Transform> _visibilityProviders = new List<Transform>(64);
        private readonly List<float> _visibilityRadii = new List<float>(64);
        private readonly List<EnemyMemory> _visibleEnemies = new List<EnemyMemory>(128);
        private readonly Dictionary<int, EnemyMemory> _enemyMemory = new Dictionary<int, EnemyMemory>(256);
        private readonly List<int> _staleKeys = new List<int>(64);

        private WorldSnapshot _snapshot;
        private int _teamId;
        private int[] _alliedTeams = Array.Empty<int>();
        private int _lastSeenRegistryVersion = -1;

        public WorldSnapshot Snapshot => _snapshot;
        public IReadOnlyList<EnemyMemory> VisibleEnemies => _visibleEnemies;
        public IReadOnlyList<GameObject> OwnUnits => _ownUnits;
        public IReadOnlyList<GameObject> OwnStructures => _ownStructures;

        public static void Register(IdentidadeUnidade identity)
        {
            if (identity == null)
            {
                return;
            }
            if (_globalRegistry.Add(identity))
            {
                _registryVersion++;
            }
        }

        public static void Unregister(IdentidadeUnidade identity)
        {
            if (identity == null)
            {
                return;
            }
            if (_globalRegistry.Remove(identity))
            {
                _runtimeCache.Remove(identity.GetInstanceID());
                _registryVersion++;
            }
        }

        public static void NotifyChanged(IdentidadeUnidade identity)
        {
            if (identity == null)
            {
                return;
            }
            _runtimeCache.Remove(identity.GetInstanceID());
            _registryVersion++;
        }

        public void Configure(int teamId, int[] alliedTeams)
        {
            _teamId = teamId;
            _alliedTeams = alliedTeams ?? Array.Empty<int>();
        }

        public void RefreshOwnedAndGlobalCache(float now, int maxVisibleProviders)
        {
            RebuildGlobalEntriesIfNeeded();

            _ownUnits.Clear();
            _ownStructures.Clear();
            _visibilityProviders.Clear();
            _visibilityRadii.Clear();
            ResetSnapshot(now);

            int providersBudget = Mathf.Max(4, maxVisibleProviders);
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < _globalEntries.Count; i++)
            {
                RegistryEntry entry = _globalEntries[i];
                if (entry.identity == null || entry.gameObject == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (entry.teamId != _teamId)
                {
                    continue;
                }

                if (entry.cache.IsStructure)
                {
                    _ownStructures.Add(entry.gameObject);
                    AccumulateStructure(entry.cache);
                }
                else
                {
                    _ownUnits.Add(entry.gameObject);
                    AccumulateUnit(entry.cache);
                }

                sum += entry.transform.position;
                count++;

                if (providersBudget > 0 && (entry.cache.IsRadar || !entry.cache.IsStructure))
                {
                    _visibilityProviders.Add(entry.transform);
                    _visibilityRadii.Add(entry.cache.VisionRadius);
                    providersBudget--;
                }
            }

            if (count > 0)
            {
                _snapshot.BaseCenter = sum / count;
            }
            else
            {
                _snapshot.BaseCenter = transform.position;
            }

            _snapshot.LastUpdated = now;
        }

        public void RefreshVisibleEnemies(float now)
        {
            _visibleEnemies.Clear();
            if (_visibilityProviders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _globalEntries.Count; i++)
            {
                RegistryEntry entry = _globalEntries[i];
                if (entry.identity == null || entry.gameObject == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (entry.teamId == 0 || entry.teamId == _teamId || IsAllied(entry.teamId))
                {
                    continue;
                }

                if (!IsVisible(entry.transform.position))
                {
                    continue;
                }

                EnemyMemory mem = new EnemyMemory
                {
                    InstanceId = entry.identity.GetInstanceID(),
                    Name = entry.gameObject.name,
                    Position = entry.transform.position,
                    LastSeenTime = now,
                    Domain = entry.cache.Domain,
                    IsStructure = entry.cache.IsStructure,
                    Threat = EstimateThreat(entry.cache)
                };

                _visibleEnemies.Add(mem);
                _enemyMemory[mem.InstanceId] = mem;
            }

            _snapshot.EnemyVisibleCount = _visibleEnemies.Count;
            if (_visibleEnemies.Count > 0)
            {
                _snapshot.LastKnownEnemyAnchor = _visibleEnemies[0].Position;
            }
        }

        public void UpdateCombatPressure(float now)
        {
            bool underThreat = false;
            Vector3 baseCenter = _snapshot.BaseCenter;
            for (int i = 0; i < _visibleEnemies.Count; i++)
            {
                float d = Vector3.Distance(Flatten(baseCenter), Flatten(_visibleEnemies[i].Position));
                if (d <= 480f)
                {
                    underThreat = true;
                    break;
                }
            }

            _snapshot.UnderThreat = underThreat;
            _snapshot.CanExpand = !underThreat && _snapshot.FactoryCount >= 1 && _snapshot.BarracksCount >= 1;
            _snapshot.LowOil = _snapshot.PlatformCount <= 0;
            _snapshot.LowLogistics = _snapshot.WarehouseCount <= 0 || _snapshot.NavalTransportCount <= 0;
            CleanupMemory(now, 120f);
        }

        public bool TryGetEnemyStrategicAnchor(out Vector3 position)
        {
            position = _snapshot.LastKnownEnemyAnchor;
            float bestScore = float.MinValue;
            bool found = false;

            foreach (var pair in _enemyMemory)
            {
                EnemyMemory mem = pair.Value;
                float score = mem.Threat + (mem.IsStructure ? 25f : 5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    position = mem.Position;
                    found = true;
                }
            }

            return found;
        }

        public bool TryGetBestEnemyTarget(
            float radarWeight,
            float airfieldWeight,
            float shipyardWeight,
            float oilWeight,
            float logisticsWeight,
            float hqWeight,
            bool preferBlindTarget,
            out TargetInfo target)
        {
            target = default;
            float best = float.MinValue;
            bool found = false;

            foreach (var pair in _enemyMemory)
            {
                EnemyMemory mem = pair.Value;
                string n = Normalize(mem.Name);
                float score = mem.Threat;

                if (n.Contains("radar")) score += radarWeight * 20f;
                if (n.Contains("aeroporto") || n.Contains("air") || n.Contains("pista")) score += airfieldWeight * 20f;
                if (n.Contains("estaleiro") || n.Contains("pier")) score += shipyardWeight * 18f;
                if (n.Contains("petro") || n.Contains("plataforma")) score += oilWeight * 22f;
                if (n.Contains("armazem") || n.Contains("log") || n.Contains("transporte")) score += logisticsWeight * 16f;
                if (n.Contains("prefeitura") || n.Contains("capital") || n.Contains("hq") || n.Contains("quartel general")) score += hqWeight * 18f;

                if (preferBlindTarget)
                {
                    bool strategicBlind = n.Contains("radar") || n.Contains("aeroporto") || n.Contains("pista");
                    if (!strategicBlind)
                    {
                        score -= 8f;
                    }
                }

                if (score > best)
                {
                    best = score;
                    target = new TargetInfo
                    {
                        Name = mem.Name,
                        Position = mem.Position,
                        Domain = mem.Domain,
                        IsStructure = mem.IsStructure,
                        Score = score
                    };
                    found = true;
                }
            }

            return found;
        }

        public Vector3 GetPrimaryThreatPoint()
        {
            if (_visibleEnemies.Count > 0)
            {
                return _visibleEnemies[0].Position;
            }
            return _snapshot.BaseCenter;
        }

        private void RebuildGlobalEntriesIfNeeded()
        {
            if (_lastSeenRegistryVersion == _registryVersion)
            {
                return;
            }

            _globalEntries.Clear();
            _scratchRegistry.Clear();
            foreach (IdentidadeUnidade id in _globalRegistry)
            {
                _scratchRegistry.Add(id);
            }

            for (int i = _scratchRegistry.Count - 1; i >= 0; i--)
            {
                IdentidadeUnidade identity = _scratchRegistry[i];
                if (identity == null || identity.gameObject == null)
                {
                    continue;
                }

                GameObject go = identity.gameObject;
                RuntimeCacheEntry cache = GetRuntimeCache(go);
                _globalEntries.Add(new RegistryEntry
                {
                    identity = identity,
                    gameObject = go,
                    transform = identity.transform,
                    teamId = identity.teamID,
                    cache = cache
                });
            }

            _lastSeenRegistryVersion = _registryVersion;
        }

        private void ResetSnapshot(float now)
        {
            _snapshot.LastUpdated = now;
            _snapshot.EnemyVisibleCount = 0;
            _snapshot.CityHallCount = 0;
            _snapshot.BarracksCount = 0;
            _snapshot.FactoryCount = 0;
            _snapshot.WarehouseCount = 0;
            _snapshot.ShipyardCount = 0;
            _snapshot.PierCount = 0;
            _snapshot.PlatformCount = 0;
            _snapshot.AirportCount = 0;
            _snapshot.CarrierCount = 0;
            _snapshot.NavalTransportCount = 0;
            _snapshot.NavalUnits = 0;
            _snapshot.AirUnits = 0;
            _snapshot.GroundUnits = 0;
            _snapshot.UnderThreat = false;
            _snapshot.CanExpand = false;
            _snapshot.LowOil = false;
            _snapshot.LowLogistics = false;
            _snapshot.EnemyAcrossOcean = false;
        }

        private void AccumulateStructure(RuntimeCacheEntry cache)
        {
            string n = cache.NormalizedName;
            if (n.Contains("prefeitura") || n.Contains("capital")) _snapshot.CityHallCount++;
            if (n.Contains("quartel") || n.Contains("tenda")) _snapshot.BarracksCount++;
            if (n.Contains("fabrica") || n.Contains("construtor")) _snapshot.FactoryCount++;
            if (n.Contains("armazem") || n.Contains("warehouse")) _snapshot.WarehouseCount++;
            if (n.Contains("estaleiro")) _snapshot.ShipyardCount++;
            if (n.Contains("pier")) _snapshot.PierCount++;
            if (n.Contains("plataforma")) _snapshot.PlatformCount++;
            if (n.Contains("aeroporto") || n.Contains("pista")) _snapshot.AirportCount++;
        }

        private void AccumulateUnit(RuntimeCacheEntry cache)
        {
            switch (cache.Domain)
            {
                case DomainHint.Air: _snapshot.AirUnits++; break;
                case DomainHint.Naval: _snapshot.NavalUnits++; break;
                default: _snapshot.GroundUnits++; break;
            }

            if (cache.IsCarrier)
            {
                _snapshot.CarrierCount++;
            }
            if (cache.IsNavalTransport)
            {
                _snapshot.NavalTransportCount++;
            }
        }

        private bool IsVisible(Vector3 point)
        {
            Vector3 flat = Flatten(point);
            for (int i = 0; i < _visibilityProviders.Count; i++)
            {
                Transform t = _visibilityProviders[i];
                if (t == null)
                {
                    continue;
                }
                float r = _visibilityRadii[i];
                Vector3 d = Flatten(t.position) - flat;
                if (d.sqrMagnitude <= r * r)
                {
                    return true;
                }
            }
            return false;
        }

        private void CleanupMemory(float now, float maxAge)
        {
            _staleKeys.Clear();
            foreach (var pair in _enemyMemory)
            {
                if (now - pair.Value.LastSeenTime > maxAge)
                {
                    _staleKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                _enemyMemory.Remove(_staleKeys[i]);
            }
        }

        private bool IsAllied(int teamId)
        {
            for (int i = 0; i < _alliedTeams.Length; i++)
            {
                if (_alliedTeams[i] == teamId)
                {
                    return true;
                }
            }
            return false;
        }

        private RuntimeCacheEntry GetRuntimeCache(GameObject go)
        {
            int id = go.GetInstanceID();
            RuntimeCacheEntry cache;
            if (_runtimeCache.TryGetValue(id, out cache))
            {
                return cache;
            }

            string n = Normalize(go.name);
            bool hasAgent = go.GetComponent<NavMeshAgent>() != null;
            bool isAircraft = n.Contains("aviao") || n.Contains("caca") || n.Contains("jet") || n.Contains("drone") || n.Contains("heli");
            bool isNaval = n.Contains("navio") || n.Contains("sub") || n.Contains("corveta") || n.Contains("destroy") || n.Contains("porta");
            bool isStructure = n.Contains("prefeitura")
                               || n.Contains("quartel")
                               || n.Contains("fabrica")
                               || n.Contains("radar")
                               || n.Contains("muro")
                               || n.Contains("estaleiro")
                               || n.Contains("pier")
                               || n.Contains("plataforma")
                               || n.Contains("aeroporto")
                               || n.Contains("heliporto")
                               || n.Contains("armazem")
                               || (!hasAgent && !isAircraft && !isNaval);

            DomainHint domain = DomainHint.Land;
            if (isAircraft) domain = DomainHint.Air;
            else if (isNaval) domain = DomainHint.Naval;

            cache = new RuntimeCacheEntry
            {
                NormalizedName = n,
                Domain = domain,
                IsStructure = isStructure,
                IsRadar = n.Contains("radar"),
                IsTransport = n.Contains("transporte") || n.Contains("truck") || n.Contains("caminhao") || n.Contains("hover"),
                IsCarrier = n.Contains("porta") && n.Contains("avio"),
                IsNavalTransport = domain == DomainHint.Naval && (n.Contains("transporte") || n.Contains("liberty") || n.Contains("hover")),
                IsOil = n.Contains("petro") || n.Contains("plataforma"),
                IsLogistics = n.Contains("armazem") || n.Contains("truck") || n.Contains("transporte"),
                VisionRadius = ResolveVisionRadius(n, isStructure, domain)
            };

            _runtimeCache[id] = cache;
            return cache;
        }

        private static float EstimateThreat(RuntimeCacheEntry cache)
        {
            float score = cache.IsStructure ? 20f : 10f;
            if (cache.IsRadar) score += 18f;
            if (cache.IsCarrier) score += 35f;
            if (cache.IsNavalTransport) score += 15f;
            if (cache.IsOil) score += 22f;
            if (cache.IsLogistics) score += 16f;
            switch (cache.Domain)
            {
                case DomainHint.Air: score += 12f; break;
                case DomainHint.Naval: score += 16f; break;
            }
            return score;
        }

        private static float ResolveVisionRadius(string normalizedName, bool isStructure, DomainHint domain)
        {
            if (normalizedName.Contains("radar")) return 260f;
            if (isStructure) return 110f;
            if (domain == DomainHint.Air) return 230f;
            if (domain == DomainHint.Naval) return 210f;
            return 170f;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        [DisallowMultipleComponent]
        public sealed class RegistryHook : MonoBehaviour
        {
            private IdentidadeUnidade _identity;

            private void Awake()
            {
                _identity = GetComponent<IdentidadeUnidade>();
                if (_identity != null)
                {
                    Register(_identity);
                }
            }

            private void OnEnable()
            {
                if (_identity == null)
                {
                    _identity = GetComponent<IdentidadeUnidade>();
                }
                if (_identity != null)
                {
                    Register(_identity);
                }
            }

            private void OnDisable()
            {
                if (_identity != null)
                {
                    Unregister(_identity);
                }
            }

            private void OnDestroy()
            {
                if (_identity != null)
                {
                    Unregister(_identity);
                }
            }
        }
    }
}
