using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_WorldState : IIAUpdateModule
    {
        private const int MaxMobileVisibilityProviders = 24;
        private const int MaxStructureVisibilityProviders = 8;
        private const float GlobalRegistryCleanupInterval = 12f;

        private struct EntityRuntimeCacheEntry
        {
            public string NormalizedName;
            public IA_Domain Domain;
            public bool IsStructure;
            public bool IsTransport;
            public bool IsOilTanker;
            public bool IsGroundTransport;
            public bool IsHoverTransport;
            public bool IsNavalTransport;
            public bool IsSubmarine;
            public bool IsInfantry;
            public bool IsTank;
            public bool IsArtillery;
            public bool IsHelicopter;
            public bool IsFixedWing;
            public bool IsRadar;
            public bool IsHighValueMobile;
            public float VisionRadius;
        }

        private struct RegistrySnapshotEntry
        {
            public IdentidadeUnidade Identity;
            public GameObject GameObject;
            public Transform Transform;
            public int TeamId;
            public int InstanceId;
            public EntityRuntimeCacheEntry Cache;
        }

        private readonly int _teamId;
        private readonly List<IdentidadeUnidade> _globalIdentityCache = new List<IdentidadeUnidade>(256);
        private readonly List<RegistrySnapshotEntry> _registrySnapshot = new List<RegistrySnapshotEntry>(256);
        private readonly Dictionary<int, IA_EnemyObservation> _enemyMemoryById = new Dictionary<int, IA_EnemyObservation>(256);
        private readonly IA_ForceSnapshot _forceSnapshot = new IA_ForceSnapshot();
        private readonly List<int> _staleEnemyIds = new List<int>(64);

        private Transform _baseProbe;
        private Vector3 _fallbackCenter;
        private bool _forceRefresh;
        private float _nextGlobalScanTime;
        private float _nextVisibleRefreshTime;
        private float _nextCleanupTime;
        private float _lastCombatSeenTime = -999f;
        private float _nextRegistryCleanupTime;

        private int _lastSeenRegistryVersion = -1;
        private bool _snapshotDirty = true;

        private static readonly HashSet<IdentidadeUnidade> _globalRegistry = new HashSet<IdentidadeUnidade>();
        private static readonly List<IdentidadeUnidade> _registryScratch = new List<IdentidadeUnidade>(512);
        private static readonly Dictionary<int, EntityRuntimeCacheEntry> _entityRuntimeCache = new Dictionary<int, EntityRuntimeCacheEntry>(1024);
        private static readonly Dictionary<int, bool> _isStructureCache = new Dictionary<int, bool>(1024);

        private static int _registryVersion = 1;
        private static float _nextFindFallbackAllowedTime = -999f;

        public readonly List<GameObject> OwnUnits = new List<GameObject>(128);
        public readonly List<GameObject> OwnStructures = new List<GameObject>(128);
        public readonly List<GameObject> OwnCombatUnits = new List<GameObject>(128);
        public readonly List<IA_VisibilityProvider> VisibilityProviders = new List<IA_VisibilityProvider>(48);
        public readonly List<IA_EnemyObservation> VisibleEnemies = new List<IA_EnemyObservation>(128);

        public Vector3 BaseCenter { get; private set; }
        public float LastScanTime { get; private set; }
        public IA_CombatPressure CombatPressure { get; private set; }

        public IA_ForceSnapshot ForceSnapshot
        {
            get { return _forceSnapshot; }
        }

        public IA_WorldState(int teamId)
        {
            _teamId = teamId;
            _fallbackCenter = Vector3.zero;
            CombatPressure = new IA_CombatPressure();
        }

        public string Name
        {
            get { return "IA_WorldState"; }
        }

        public float Interval
        {
            get { return 0.80f; }
        }

        public float BudgetMs
        {
            get { return 0.60f; }
        }

        public static void Register(IdentidadeUnidade id)
        {
            if (id == null)
            {
                return;
            }

            if (_globalRegistry.Add(id))
            {
                _registryVersion++;
            }
        }

        public static void Unregister(IdentidadeUnidade id)
        {
            if (id == null)
            {
                return;
            }

            if (_globalRegistry.Remove(id))
            {
                int instanceId = id.GetInstanceID();
                _entityRuntimeCache.Remove(instanceId);
                _isStructureCache.Remove(instanceId);
                _registryVersion++;
            }
        }

        public static void NotifyEntityChanged(IdentidadeUnidade id)
        {
            if (id == null)
            {
                return;
            }

            int instanceId = id.GetInstanceID();
            _entityRuntimeCache.Remove(instanceId);
            _isStructureCache.Remove(instanceId);
            _registryVersion++;
        }

        public static void InvalidateStructureCache(int instanceId)
        {
            _isStructureCache.Remove(instanceId);
            _entityRuntimeCache.Remove(instanceId);
            _registryVersion++;
        }

        public void Tick(float now, float deltaTime)
        {
            if (_forceRefresh || now >= _nextGlobalScanTime || _snapshotDirty || _lastSeenRegistryVersion != _registryVersion)
            {
                long refreshStart = System.Diagnostics.Stopwatch.GetTimestamp();
                RefreshOwnedAndGlobalCache(now);
                RegistrarMetricaTempo(
                    "world_refresh_ms",
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - refreshStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));

                _nextGlobalScanTime = now + 8f;
                _nextVisibleRefreshTime = 0f;
                _forceRefresh = false;
            }

            if (_forceRefresh || now >= _nextVisibleRefreshTime)
            {
                long visibleStart = System.Diagnostics.Stopwatch.GetTimestamp();
                RefreshVisibleEnemies(now);
                UpdateCombatPressure(now);
                RegistrarMetricaTempo(
                    "visible_enemy_ms",
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - visibleStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));

                _nextVisibleRefreshTime = now + ResolveVisibleRefreshInterval();
            }

            if (now >= _nextCleanupTime)
            {
                CleanupMemory(now, 120f);
                _nextCleanupTime = now + 30f;
            }

            if (now >= _nextRegistryCleanupTime)
            {
                CleanupDeadRegistryEntries();
                _nextRegistryCleanupTime = now + GlobalRegistryCleanupInterval;
            }
        }

        public void MarkDirty()
        {
            _forceRefresh = true;
            _snapshotDirty = true;
        }

        public void SetFallbackCenter(Vector3 center)
        {
            _fallbackCenter = center;
        }

        public List<IA_EnemyObservation> GetEnemyMemory(float maxAgeSeconds)
        {
            var output = new List<IA_EnemyObservation>(_enemyMemoryById.Count);
            FillEnemyMemory(output, maxAgeSeconds);
            return output;
        }

        public void FillEnemyMemory(List<IA_EnemyObservation> destination, float maxAgeSeconds)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            float now = Time.time;

            foreach (var pair in _enemyMemoryById)
            {
                IA_EnemyObservation obs = pair.Value;
                if (obs != null && now - obs.LastSeenTime <= maxAgeSeconds)
                {
                    destination.Add(obs);
                }
            }
        }

        public Transform GetNearestVisibleEnemy(Vector3 fromPosition, IA_Domain preferredDomain)
        {
            float bestDistance = float.MaxValue;
            Transform best = null;
            Vector3 flatFrom = Flatten(fromPosition);

            for (int i = 0; i < VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = VisibleEnemies[i];
                if (obs == null || obs.Transform == null)
                {
                    continue;
                }

                float scoreDistance = Vector3.Distance(flatFrom, Flatten(obs.Position));
                if (preferredDomain != obs.Domain)
                {
                    scoreDistance *= 1.15f;
                }

                if (scoreDistance < bestDistance)
                {
                    bestDistance = scoreDistance;
                    best = obs.Transform;
                }
            }

            return best;
        }

        public bool TryGetEnemyStrategicAnchor(Vector3 fromPosition, out Vector3 position)
        {
            position = Vector3.zero;
            float bestScore = float.MinValue;
            Vector3 flatFrom = Flatten(fromPosition);

            for (int i = 0; i < _registrySnapshot.Count; i++)
            {
                RegistrySnapshotEntry snap = _registrySnapshot[i];
                if (snap.Identity == null || snap.GameObject == null || !snap.GameObject.activeInHierarchy)
                {
                    continue;
                }

                if (snap.TeamId == 0 || snap.TeamId == _teamId)
                {
                    continue;
                }

                string name = snap.Cache.NormalizedName;
                float distance = Vector3.Distance(flatFrom, Flatten(snap.Transform.position));

                float score = snap.Cache.IsStructure ? 120f : 35f;
                if (snap.Cache.Domain == IA_Domain.Land)
                {
                    score += 10f;
                }

                if (name.Contains("plataforma") || name.Contains("petroleiro") || name.Contains("petrolifero") || name.Contains("tanker"))
                {
                    score += 170f;
                }
                else if (name.Contains("pier") || name.Contains("estaleiro"))
                {
                    score += 150f;
                }
                else if (name.Contains("aeroporto") || name.Contains("airport"))
                {
                    score += 135f;
                }
                else if (name.Contains("fabrica") || name.Contains("construtor"))
                {
                    score += 110f;
                }
                else if (name.Contains("prefeitura") || name.Contains("capital") || name.Contains("governo"))
                {
                    score += 95f;
                }
                else if (name.Contains("quartel general") || name.Contains("quartel_general") || name.Contains("hq"))
                {
                    score += 120f;
                }

                score -= distance * 0.015f;
                if (score > bestScore)
                {
                    bestScore = score;
                    position = snap.Transform.position;
                }
            }

            return bestScore > float.MinValue;
        }

        public int FillEnemyStrategicTargets(List<IA_StrategicTargetData> output, Vector3 fromPosition, int maxTargets)
        {
            if (output == null)
            {
                return 0;
            }

            output.Clear();
            if (maxTargets <= 0)
            {
                return 0;
            }

            Vector3 flatFrom = Flatten(fromPosition);
            for (int i = 0; i < _registrySnapshot.Count; i++)
            {
                RegistrySnapshotEntry snap = _registrySnapshot[i];
                if (snap.Identity == null || snap.GameObject == null || snap.Transform == null || !snap.GameObject.activeInHierarchy)
                {
                    continue;
                }

                if (snap.TeamId == 0 || snap.TeamId == _teamId)
                {
                    continue;
                }

                IA_StrategicTargetKind kind = ResolveStrategicTargetKind(snap.Cache, snap.GameObject);
                if (kind == IA_StrategicTargetKind.None)
                {
                    continue;
                }

                float distance = Vector3.Distance(flatFrom, Flatten(snap.Transform.position));
                float score = ScoreStrategicTarget(kind, snap.Cache, distance);
                IA_StrategicTargetData target = new IA_StrategicTargetData
                {
                    Kind = kind,
                    Transform = snap.Transform,
                    Position = snap.Transform.position,
                    Score = score,
                    Label = snap.Cache.NormalizedName
                };

                InsertStrategicTarget(output, target, maxTargets);
            }

            return output.Count;
        }

        private static IA_StrategicTargetKind ResolveStrategicTargetKind(EntityRuntimeCacheEntry cache, GameObject obj)
        {
            string name = cache.NormalizedName;
            if (name.Contains("plataforma"))
            {
                return IA_StrategicTargetKind.OilPlatform;
            }

            if (cache.IsOilTanker || name.Contains("petroleiro") || name.Contains("petrolifero") || name.Contains("tanker"))
            {
                return IA_StrategicTargetKind.OilTanker;
            }

            if (name.Contains("pier"))
            {
                return IA_StrategicTargetKind.Pier;
            }

            if (name.Contains("estaleiro"))
            {
                return IA_StrategicTargetKind.Shipyard;
            }

            if (name.Contains("aeroporto") || name.Contains("airport") || name.Contains("base aerea") || name.Contains("pista"))
            {
                return IA_StrategicTargetKind.Airport;
            }

            ControleAviao aircraft = obj != null ? obj.GetComponent<ControleAviao>() : null;
            if (aircraft != null && aircraft.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                return IA_StrategicTargetKind.ReadyAircraft;
            }

            if (!cache.IsStructure && (cache.IsFixedWing || cache.Domain == IA_Domain.Air))
            {
                return IA_StrategicTargetKind.ReadyAircraft;
            }

            if (!cache.IsStructure && cache.Domain == IA_Domain.Naval && !cache.IsOilTanker && !cache.IsNavalTransport)
            {
                return IA_StrategicTargetKind.NavalPatrol;
            }

            if (name.Contains("fabrica") || name.Contains("construtor"))
            {
                return IA_StrategicTargetKind.Factory;
            }

            if (name.Contains("prefeitura") || name.Contains("capital") || name.Contains("governo"))
            {
                return IA_StrategicTargetKind.CityHall;
            }

            return IA_StrategicTargetKind.None;
        }

        private static float ScoreStrategicTarget(IA_StrategicTargetKind kind, EntityRuntimeCacheEntry cache, float distance)
        {
            float score;
            switch (kind)
            {
                case IA_StrategicTargetKind.OilPlatform:
                    score = 330f;
                    break;
                case IA_StrategicTargetKind.OilTanker:
                    score = 315f;
                    break;
                case IA_StrategicTargetKind.Pier:
                    score = 285f;
                    break;
                case IA_StrategicTargetKind.Shipyard:
                    score = 275f;
                    break;
                case IA_StrategicTargetKind.Airport:
                    score = 260f;
                    break;
                case IA_StrategicTargetKind.ReadyAircraft:
                    score = 235f;
                    break;
                case IA_StrategicTargetKind.NavalPatrol:
                    score = 220f;
                    break;
                case IA_StrategicTargetKind.Factory:
                    score = 170f;
                    break;
                case IA_StrategicTargetKind.CityHall:
                    score = 135f;
                    break;
                default:
                    score = 0f;
                    break;
            }

            if (cache.IsStructure)
            {
                score += 35f;
            }

            return score - distance * 0.012f;
        }

        private static void InsertStrategicTarget(List<IA_StrategicTargetData> output, IA_StrategicTargetData target, int maxTargets)
        {
            for (int i = 0; i < output.Count; i++)
            {
                if (output[i] != null && output[i].Kind == target.Kind)
                {
                    if (target.Score <= output[i].Score)
                    {
                        return;
                    }

                    output.RemoveAt(i);
                    break;
                }
            }

            int index = 0;
            while (index < output.Count && output[index] != null && output[index].Score >= target.Score)
            {
                index++;
            }

            output.Insert(index, target);
            if (output.Count > maxTargets)
            {
                output.RemoveAt(output.Count - 1);
            }
        }

        public int CountOwnByHint(params string[] hints)
        {
            if (hints == null || hints.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < OwnUnits.Count; i++)
            {
                GameObject unit = OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = GetEntityCache(unit).NormalizedName;
                bool matched = false;

                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && name.Contains(hint))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshOwnedAndGlobalCache(float now)
        {
            RebuildRegistrySnapshotIfNeeded(now);

            OwnUnits.Clear();
            OwnStructures.Clear();
            OwnCombatUnits.Clear();
            VisibilityProviders.Clear();
            ResetForceSnapshot();

            int mobileVisibilityBudget = MaxMobileVisibilityProviders;
            int structureVisibilityBudget = MaxStructureVisibilityProviders;

            for (int i = 0; i < _registrySnapshot.Count; i++)
            {
                RegistrySnapshotEntry snap = _registrySnapshot[i];
                if (snap.Identity == null || snap.GameObject == null || !snap.GameObject.activeInHierarchy)
                {
                    continue;
                }

                if (snap.TeamId != _teamId)
                {
                    continue;
                }

                if (snap.Cache.IsStructure)
                {
                    OwnStructures.Add(snap.GameObject);
                    AccumulateStructureSnapshot(snap.Cache);
                }
                else
                {
                    OwnUnits.Add(snap.GameObject);
                    AccumulateUnitSnapshot(snap.Cache);
                    if (!snap.Cache.IsTransport)
                    {
                        OwnCombatUnits.Add(snap.GameObject);
                    }
                }

                if (!ShouldRegisterVisibilityProvider(snap.Cache, snap.Cache.IsStructure, ref mobileVisibilityBudget, ref structureVisibilityBudget))
                {
                    continue;
                }

                VisibilityProviders.Add(new IA_VisibilityProvider
                {
                    Source = snap.Transform,
                    Radius = snap.Cache.VisionRadius
                });
            }

            BaseCenter = ComputeBaseCenter();

            if (_baseProbe == null)
            {
                _baseProbe = CreateVirtualBaseProbe();
            }

            _baseProbe.position = BaseCenter;
            VisibilityProviders.Add(new IA_VisibilityProvider
            {
                Source = _baseProbe,
                Radius = 260f
            });

            LastScanTime = now;
        }

        private void RebuildRegistrySnapshotIfNeeded(float now)
        {
            if (!_snapshotDirty && _lastSeenRegistryVersion == _registryVersion)
            {
                return;
            }

            _registrySnapshot.Clear();
            _globalIdentityCache.Clear();

            if (_globalRegistry.Count == 0 && now >= _nextFindFallbackAllowedTime)
            {
                IdentidadeUnidade[] identities = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
                for (int i = 0; i < identities.Length; i++)
                {
                    Register(identities[i]);
                }

                _nextFindFallbackAllowedTime = now + 20f;
            }

            _registryScratch.Clear();
            foreach (IdentidadeUnidade id in _globalRegistry)
            {
                _registryScratch.Add(id);
            }

            for (int i = _registryScratch.Count - 1; i >= 0; i--)
            {
                IdentidadeUnidade id = _registryScratch[i];
                if (id == null)
                {
                    continue;
                }

                GameObject go = id.gameObject;
                if (go == null)
                {
                    continue;
                }

                EntityRuntimeCacheEntry cache = GetEntityCache(go);

                _globalIdentityCache.Add(id);
                _registrySnapshot.Add(new RegistrySnapshotEntry
                {
                    Identity = id,
                    GameObject = go,
                    Transform = id.transform,
                    TeamId = id.teamID,
                    InstanceId = id.GetInstanceID(),
                    Cache = cache
                });
            }

            _lastSeenRegistryVersion = _registryVersion;
            _snapshotDirty = false;
        }

        private void RefreshVisibleEnemies(float now)
        {
            VisibleEnemies.Clear();
            if (VisibilityProviders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _registrySnapshot.Count; i++)
            {
                RegistrySnapshotEntry snap = _registrySnapshot[i];
                if (snap.Identity == null || snap.GameObject == null || !snap.GameObject.activeInHierarchy)
                {
                    continue;
                }

                if (snap.TeamId == 0 || snap.TeamId == _teamId)
                {
                    continue;
                }

                if (!IsVisible(snap.Transform.position))
                {
                    continue;
                }

                IA_EnemyObservation obs;
                if (!_enemyMemoryById.TryGetValue(snap.InstanceId, out obs))
                {
                    obs = new IA_EnemyObservation
                    {
                        InstanceId = snap.InstanceId
                    };
                    _enemyMemoryById.Add(snap.InstanceId, obs);
                }

                obs.Transform = snap.Transform;
                obs.Position = snap.Transform.position;
                obs.UnitName = snap.Identity.name;
                obs.Domain = snap.Cache.Domain;
                obs.IsStructure = snap.Cache.IsStructure;
                obs.ThreatScore = EstimateThreat(obs.UnitName, obs.Domain, obs.IsStructure);
                obs.LastSeenTime = now;

                VisibleEnemies.Add(obs);

                if (obs.IsStructure)
                {
                    _forceSnapshot.VisibleEnemyStructures++;
                }
            }
        }

        private void CleanupMemory(float now, float maxAge)
        {
            _staleEnemyIds.Clear();

            foreach (var pair in _enemyMemoryById)
            {
                IA_EnemyObservation obs = pair.Value;
                if (obs == null || obs.Transform == null || now - obs.LastSeenTime > maxAge)
                {
                    _staleEnemyIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleEnemyIds.Count; i++)
            {
                _enemyMemoryById.Remove(_staleEnemyIds[i]);
            }
        }

        private void CleanupDeadRegistryEntries()
        {
            if (_globalRegistry.Count == 0)
            {
                return;
            }

            _registryScratch.Clear();
            foreach (IdentidadeUnidade id in _globalRegistry)
            {
                if (id == null || id.gameObject == null)
                {
                    _registryScratch.Add(id);
                }
            }

            if (_registryScratch.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _registryScratch.Count; i++)
            {
                _globalRegistry.Remove(_registryScratch[i]);
            }

            _registryVersion++;
            _snapshotDirty = true;
        }

        private void UpdateCombatPressure(float now)
        {
            if (CombatPressure == null)
            {
                CombatPressure = new IA_CombatPressure();
            }

            int navalUnits = _forceSnapshot.NavalUnits + _forceSnapshot.Submarines;
            int airUnits = _forceSnapshot.AirUnits;
            bool enemyVisible = VisibleEnemies.Count > 0;
            int activeMissiles = IA_CombatTelemetry.ActiveMissiles;
            int activeProjectiles = IA_CombatTelemetry.ActiveProjectiles;

            if (enemyVisible && (navalUnits > 0 || airUnits > 0 || activeMissiles > 0 || activeProjectiles > 0))
            {
                _lastCombatSeenTime = now;
            }

            float recentCombatSeconds = _lastCombatSeenTime <= 0f
                ? 999f
                : Mathf.Max(0f, now - _lastCombatSeenTime);

            EstadoCargaIA estado = EstadoCargaIA.Normal;
            bool mixedFleet = navalUnits >= 3 && airUnits >= 3;

            if ((enemyVisible && mixedFleet)
                || activeMissiles >= 16
                || activeProjectiles >= 90
                || (recentCombatSeconds <= 10f && (activeMissiles >= 8 || activeProjectiles >= 48)))
            {
                estado = EstadoCargaIA.Saturado;
            }
            else if (enemyVisible
                     || recentCombatSeconds <= 30f
                     || activeMissiles >= 4
                     || activeProjectiles >= 24
                     || navalUnits >= 4
                     || airUnits >= 4)
            {
                estado = EstadoCargaIA.EmCombate;
            }

            CombatPressure.Estado = estado;
            CombatPressure.EnemyVisible = enemyVisible;
            CombatPressure.NavalUnitsActive = navalUnits;
            CombatPressure.AirUnitsActive = airUnits;
            CombatPressure.RecentCombatSeconds = recentCombatSeconds;
            CombatPressure.ActiveMissiles = activeMissiles;
            CombatPressure.ActiveProjectiles = activeProjectiles;
            CombatPressure.LastUpdatedTime = now;

            _forceSnapshot.LastUpdatedTime = now;
            _forceSnapshot.VisibleEnemies = VisibleEnemies.Count;
            _forceSnapshot.EnemyVisible = enemyVisible;
            _forceSnapshot.ActiveMissiles = activeMissiles;
            _forceSnapshot.ActiveProjectiles = activeProjectiles;
            _forceSnapshot.RecentCombatSeconds = recentCombatSeconds;
        }

        private bool IsVisible(Vector3 position)
        {
            Vector3 flatTarget = Flatten(position);

            for (int i = 0; i < VisibilityProviders.Count; i++)
            {
                IA_VisibilityProvider provider = VisibilityProviders[i];
                if (provider == null || provider.Source == null)
                {
                    continue;
                }

                float radius = Mathf.Max(20f, provider.Radius);
                Vector3 delta = Flatten(provider.Source.position) - flatTarget;
                if (delta.sqrMagnitude <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldRegisterVisibilityProvider(EntityRuntimeCacheEntry entry, bool structure, ref int mobileBudget, ref int structureBudget)
        {
            if (entry.IsRadar)
            {
                return true;
            }

            if (structure)
            {
                if (structureBudget <= 0)
                {
                    return false;
                }

                structureBudget--;
                return true;
            }

            if (entry.IsHighValueMobile)
            {
                if (mobileBudget <= 0)
                {
                    return false;
                }

                mobileBudget--;
                return true;
            }

            if (mobileBudget <= 0)
            {
                return false;
            }

            mobileBudget--;
            return true;
        }

        private Vector3 ComputeBaseCenter()
        {
            if (OwnStructures.Count == 0)
            {
                if (OwnUnits.Count > 0 && OwnUnits[0] != null)
                {
                    return OwnUnits[0].transform.position;
                }

                return _fallbackCenter;
            }

            Vector3 preferred;
            if (TryFindStructureAnchor(out preferred, "prefeitura", "governo", "capital", "quartel general", "quartel_general", "hq"))
            {
                return ComputeClusterCenter(preferred, 380f);
            }

            if (TryFindStructureAnchor(out preferred, "tenda", "barraca", "construtor", "fabrica", "armazem", "radar"))
            {
                return ComputeClusterCenter(preferred, 340f);
            }

            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                sum += structure.transform.position;
                count++;
            }

            if (count == 0)
            {
                return _fallbackCenter;
            }

            return sum / count;
        }

        private bool TryFindStructureAnchor(out Vector3 position, params string[] hints)
        {
            position = Vector3.zero;
            Vector3 reference = GetAnchorReference();
            float best = float.MaxValue;
            bool found = false;
            Vector3 flatReference = Flatten(reference);

            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string n = GetEntityCache(structure).NormalizedName;
                bool match = false;

                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && n.Contains(hint))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                float dist = (Flatten(structure.transform.position) - flatReference).sqrMagnitude;
                if (!found || dist < best)
                {
                    best = dist;
                    position = structure.transform.position;
                    found = true;
                }
            }

            return found;
        }

        private Vector3 ComputeClusterCenter(Vector3 anchor, float radius)
        {
            float radiusSqr = radius * radius;
            Vector3 flatAnchor = Flatten(anchor);
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                Vector3 pos = structure.transform.position;
                if ((Flatten(pos) - flatAnchor).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                sum += pos;
                count++;
            }

            if (count == 0)
            {
                return anchor;
            }

            return sum / count;
        }

        private Vector3 GetAnchorReference()
        {
            if (_fallbackCenter != Vector3.zero)
            {
                return _fallbackCenter;
            }

            if (OwnUnits.Count > 0 && OwnUnits[0] != null)
            {
                return OwnUnits[0].transform.position;
            }

            if (OwnStructures.Count > 0 && OwnStructures[0] != null)
            {
                return OwnStructures[0].transform.position;
            }

            return Vector3.zero;
        }

        private Transform CreateVirtualBaseProbe()
        {
            GameObject holder = new GameObject("IA_WorldState_Probe");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = BaseCenter;
            return holder.transform;
        }

        private void ResetForceSnapshot()
        {
            _forceSnapshot.LastUpdatedTime = Time.time;
            _forceSnapshot.TotalOwnUnits = 0;
            _forceSnapshot.TotalOwnStructures = 0;
            _forceSnapshot.TotalCombatUnits = 0;
            _forceSnapshot.InfantryUnits = 0;
            _forceSnapshot.TankUnits = 0;
            _forceSnapshot.ArtilleryUnits = 0;
            _forceSnapshot.Helicopters = 0;
            _forceSnapshot.FixedWingAircraft = 0;
            _forceSnapshot.ReadyAircraft = 0;
            _forceSnapshot.AirUnits = 0;
            _forceSnapshot.NavalUnits = 0;
            _forceSnapshot.Submarines = 0;
            _forceSnapshot.OilTankers = 0;
            _forceSnapshot.CoastalDefenseShips = 0;
            _forceSnapshot.AttackFleetShips = 0;
            _forceSnapshot.GroundTransports = 0;
            _forceSnapshot.HoverTransports = 0;
            _forceSnapshot.NavalTransports = 0;
            _forceSnapshot.VisibleEnemies = 0;
            _forceSnapshot.VisibleEnemyStructures = 0;
            _forceSnapshot.BarracksCount = 0;
            _forceSnapshot.FactoryCount = 0;
            _forceSnapshot.AirportCount = 0;
            _forceSnapshot.HeliportCount = 0;
            _forceSnapshot.ShipyardCount = 0;
            _forceSnapshot.PierCount = 0;
            _forceSnapshot.PlatformCount = 0;
            _forceSnapshot.WarehouseCount = 0;
            _forceSnapshot.RadarCount = 0;
            _forceSnapshot.ActiveMissiles = 0;
            _forceSnapshot.ActiveProjectiles = 0;
            _forceSnapshot.EnemyVisible = false;
        }

        private void AccumulateUnitSnapshot(EntityRuntimeCacheEntry entry)
        {
            _forceSnapshot.TotalOwnUnits++;

            if (!entry.IsTransport)
            {
                _forceSnapshot.TotalCombatUnits++;
            }

            if (entry.IsOilTanker)
            {
                _forceSnapshot.OilTankers++;
            }

            if (entry.Domain == IA_Domain.Naval && !entry.IsSubmarine && !entry.IsOilTanker && !entry.IsNavalTransport)
            {
                _forceSnapshot.NavalUnits++;
                if (entry.IsHighValueMobile)
                {
                    _forceSnapshot.AttackFleetShips++;
                }
                else
                {
                    _forceSnapshot.CoastalDefenseShips++;
                }
            }
            else if (entry.Domain == IA_Domain.Air)
            {
                _forceSnapshot.AirUnits++;
            }

            if (entry.IsSubmarine)
            {
                _forceSnapshot.Submarines++;
            }

            if (entry.IsInfantry)
            {
                _forceSnapshot.InfantryUnits++;
            }

            if (entry.IsTank)
            {
                _forceSnapshot.TankUnits++;
            }

            if (entry.IsArtillery)
            {
                _forceSnapshot.ArtilleryUnits++;
            }

            if (entry.IsHelicopter)
            {
                _forceSnapshot.Helicopters++;
            }

            if (entry.IsFixedWing)
            {
                _forceSnapshot.FixedWingAircraft++;
                _forceSnapshot.ReadyAircraft++;
            }

            if (entry.IsNavalTransport)
            {
                _forceSnapshot.NavalTransports++;
            }

            if (entry.IsGroundTransport)
            {
                _forceSnapshot.GroundTransports++;
            }

            if (entry.IsHoverTransport)
            {
                _forceSnapshot.HoverTransports++;
            }



            if (entry.IsNavalTransport)
            {
                _forceSnapshot.NavalTransports++;
            }
        }

        private void AccumulateStructureSnapshot(EntityRuntimeCacheEntry entry)
        {
            string n = entry.NormalizedName;
            _forceSnapshot.TotalOwnStructures++;

            if (n.Contains("tenda") || n.Contains("barraca") || n.Contains("quartel"))
            {
                _forceSnapshot.BarracksCount++;
            }

            if (n.Contains("construtor de veiculos") || n.Contains("construtor") || n.Contains("fabrica"))
            {
                _forceSnapshot.FactoryCount++;
            }

            if (n.Contains("aeroporto") || n.Contains("airport") || n.Contains("base aerea") || n.Contains("pista"))
            {
                _forceSnapshot.AirportCount++;
            }

            if (n.Contains("heliporto") || n.Contains("hangar"))
            {
                _forceSnapshot.HeliportCount++;
            }

            if (n.Contains("estaleiro"))
            {
                _forceSnapshot.ShipyardCount++;
            }

            if (n.Contains("pier"))
            {
                _forceSnapshot.PierCount++;
            }

            if (n.Contains("plataforma"))
            {
                _forceSnapshot.PlatformCount++;
            }

            if (n.Contains("armazem") || n.Contains("warehouse"))
            {
                _forceSnapshot.WarehouseCount++;
            }

            if (entry.IsRadar)
            {
                _forceSnapshot.RadarCount++;
            }
        }

        private EntityRuntimeCacheEntry GetEntityCache(GameObject obj)
        {
            if (obj == null)
            {
                return default(EntityRuntimeCacheEntry);
            }

            int id = obj.GetInstanceID();
            EntityRuntimeCacheEntry entry;
            if (_entityRuntimeCache.TryGetValue(id, out entry))
            {
                return entry;
            }

            string n = IA_Text.Normalize(obj.name);
            bool hasAircraft = obj.GetComponent<ControleAviao>() != null || obj.GetComponent<ControleAviaoCaca>() != null;
            bool hasHelicopter = obj.GetComponent<Helicoptero>() != null;
            bool hasSubmarine = obj.GetComponent<ControleSubmarino>() != null || n.Contains("leviathan") || n.Contains("wraith") || n.Contains("mako");
            bool isOilTanker = obj.GetComponent<NavioPetroleiro>() != null || n.Contains("petroleiro") || n.Contains("petrolifero") || n.Contains("tanker");
            bool isNavalTransportComp = obj.GetComponent<NavioTransporteTropas>() != null;
            bool hasNaval = obj.GetComponent<ControleNavioRealista>() != null || isOilTanker || isNavalTransportComp || n.Contains("navio") || n.Contains("corveta") || n.Contains("destroy") || n.Contains("ironclad") || n.Contains("sovereign") || n.Contains("vindicator") || n.Contains("arrowhead");
            bool hasAgent = obj.GetComponent<NavMeshAgent>() != null;
            bool mobileByScript = !hasAgent
                                  && (hasAircraft
                                      || hasHelicopter
                                      || hasNaval
                                      || hasSubmarine
                                      || obj.GetComponent<ControleUnidade>() != null);

            bool explicitStructure = n.Contains("prefeitura")
                                     || n.Contains("quartel")
                                     || n.Contains("fabrica")
                                     || n.Contains("refinaria")
                                     || n.Contains("torre")
                                     || n.Contains("radar")
                                     || n.Contains("muro")
                                     || n.Contains("estaleiro")
                                     || n.Contains("pier")
                                     || n.Contains("plataforma")
                                     || n.Contains("aeroporto")
                                     || n.Contains("heliporto")
                                     || n.Contains("armazem");

            IdentidadeUnidade idComp = obj.GetComponent<IdentidadeUnidade>();
            bool isAereoFromComp = idComp != null && idComp.tipoUnidade == TipoUnidade.Aereo;
            bool isDrone = n.Contains("drone") || n.Contains("vap");
            bool isFixedWingByName = IsFixedWingAircraftName(n);

            bool isStructure = explicitStructure || (!hasAgent && !mobileByScript);
            IA_Domain domain = IA_Domain.Land;

            if (isAereoFromComp || hasAircraft || hasHelicopter || n.Contains("heli") || n.Contains("aviao") || isFixedWingByName || isDrone)
            {
                domain = IA_Domain.Air;
            }
            else if (hasNaval || hasSubmarine || n.Contains("sub"))
            {
                domain = IA_Domain.Naval;
            }

            bool isHover = n.Contains("hover") || n.Contains("houver");
            bool isTransport = isOilTanker
                               || n.Contains("transporte")
                               || n.Contains("truck")
                               || n.Contains("caminhao")
                               || isHover
                               || n.Contains("liberty");

            entry = new EntityRuntimeCacheEntry
            {
                NormalizedName = n,
                Domain = domain,
                IsStructure = isStructure,
                IsTransport = isTransport,
                IsOilTanker = isOilTanker,
                IsGroundTransport = (n.Contains("truck") || n.Contains("caminhao") || n.Contains("transporte")) && domain == IA_Domain.Land,
                IsHoverTransport = isHover,
                IsNavalTransport = domain == IA_Domain.Naval && (isOilTanker || isNavalTransportComp || n.Contains("liberty") || n.Contains("transporte") || isHover || n.Contains("ww")),
                IsSubmarine = hasSubmarine || n.Contains("sub"),
                IsInfantry = n.Contains("sold") || n.Contains("rifle") || n.Contains("infan"),
                IsTank = n.Contains("tank") || n.Contains("mbt") || n.Contains("south") || n.Contains("arthur") || n.Contains("c1"),
                IsArtillery = n.Contains("artilh") || n.Contains("hack") || n.Contains("mlrs") || n.Contains("lancador"),
                IsHelicopter = hasHelicopter || n.Contains("heli") || n.Contains("ray") || n.Contains("vans"),
                IsFixedWing = isAereoFromComp || hasAircraft || isFixedWingByName || n.Contains("jet") || n.Contains("aviao") || isDrone,
                IsRadar = n.Contains("radar"),
                IsHighValueMobile = isAereoFromComp || hasAircraft || isFixedWingByName || hasHelicopter || hasNaval || hasSubmarine || isDrone,
                VisionRadius = ResolveVisionRadius(n, isStructure, hasAircraft || isAereoFromComp || isFixedWingByName || isDrone, hasHelicopter, hasNaval || hasSubmarine)
            };

            _entityRuntimeCache[id] = entry;
            return entry;
        }

        private static bool IsFixedWingAircraftName(string normalizedName)
        {
            return normalizedName.Contains("fa1")
                   || normalizedName.Contains("g15")
                   || normalizedName.Contains("a_20")
                   || normalizedName.Contains("a20")
                   || normalizedName.Contains("b260")
                   || normalizedName.Contains("b-260")
                   || normalizedName.Contains("supra")
                   || normalizedName.Contains("su11")
                   || normalizedName.Contains("g_18m")
                   || normalizedName.Contains("g18m")
                   || normalizedName.Contains("super tuk")
                   || normalizedName.Contains("supertuk");
        }

        private static float ResolveVisionRadius(string normalizedName, bool structure, bool hasAircraft, bool hasHelicopter, bool hasNaval)
        {
            if (normalizedName.Contains("radar"))
            {
                return 260f;
            }

            if (structure)
            {
                return 110f;
            }

            if (hasAircraft)
            {
                return 230f;
            }

            if (hasHelicopter)
            {
                return 200f;
            }

            if (hasNaval)
            {
                return 210f;
            }

            return 190f;
        }

        private static float EstimateThreat(string unitName, IA_Domain domain, bool structure)
        {
            float value = structure ? 35f : 15f;
            string name = IA_Text.Normalize(unitName);

            if (name.Contains("tank") || name.Contains("mbt") || name.Contains("south"))
            {
                value += 30f;
            }

            if (name.Contains("artilh") || name.Contains("hack"))
            {
                value += 25f;
            }

            if (name.Contains("destroy") || name.Contains("ironclad") || name.Contains("vindicator"))
            {
                value += 40f;
            }

            if (name.Contains("sub"))
            {
                value += 35f;
            }

            if (name.Contains("fa1") || name.Contains("caca") || name.Contains("vap") || name.Contains("drone"))
            {
                value += 28f;
            }

            if (domain == IA_Domain.Air)
            {
                value += 8f;
            }
            else if (domain == IA_Domain.Naval)
            {
                value += 12f;
            }

            return value;
        }

        private static bool IsStructure(GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            int id = obj.GetInstanceID();
            bool result;
            if (_isStructureCache.TryGetValue(id, out result))
            {
                return result;
            }

            string n = IA_Text.Normalize(obj.name);
            bool hasAgent = obj.GetComponent<NavMeshAgent>() != null;
            bool mobileByScript = !hasAgent && (
                                  obj.GetComponent<ControleAviao>() != null
                                  || obj.GetComponent<ControleAviaoCaca>() != null
                                  || obj.GetComponent<Helicoptero>() != null
                                  || obj.GetComponent<ControleNavioRealista>() != null
                                  || obj.GetComponent<ControleSubmarino>() != null
                                  || obj.GetComponent<ControleUnidade>() != null);

            bool explicitStructure = n.Contains("prefeitura")
                                     || n.Contains("quartel")
                                     || n.Contains("fabrica")
                                     || n.Contains("refinaria")
                                     || n.Contains("torre")
                                     || n.Contains("radar")
                                     || n.Contains("muro")
                                     || n.Contains("estaleiro")
                                     || n.Contains("pier")
                                     || n.Contains("plataforma")
                                     || n.Contains("aeroporto")
                                     || n.Contains("heliporto");

            result = explicitStructure || (!hasAgent && !mobileByScript);
            _isStructureCache[id] = result;
            return result;
        }

        private static bool IsTransport(GameObject obj)
        {
            string n = IA_Text.Normalize(obj.name);
            return n.Contains("transporte")
                   || n.Contains("truck")
                   || n.Contains("caminhao")
                   || n.Contains("hover");
        }

        private static void RegistrarMetricaTempo(string nome, float valor)
        {
            if (valor > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(nome, valor);
            }
        }

        private float ResolveVisibleRefreshInterval()
        {
            IA_CombatPressure pressure = CombatPressure;
            if (pressure == null)
            {
                return 0.45f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 0.25f;
                case EstadoCargaIA.EmCombate:
                    return 0.35f;
                default:
                    return 0.60f;
            }
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
