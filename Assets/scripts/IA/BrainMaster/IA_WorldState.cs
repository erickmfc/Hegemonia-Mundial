using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_WorldState : IIAUpdateModule
    {
        private const int MaxMobileVisibilityProviders = 24;
        private const int MaxStructureVisibilityProviders = 8;

        private struct EntityRuntimeCacheEntry
        {
            public string NormalizedName;
            public IA_Domain Domain;
            public bool IsStructure;
            public bool IsTransport;
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
        private readonly int _teamId;
        private readonly List<IdentidadeUnidade> _globalIdentityCache = new List<IdentidadeUnidade>();
        private readonly Dictionary<int, IA_EnemyObservation> _enemyMemoryById = new Dictionary<int, IA_EnemyObservation>();
        private readonly IA_ForceSnapshot _forceSnapshot = new IA_ForceSnapshot();
        private readonly List<int> _staleEnemyIds = new List<int>(64);
        private Transform _baseProbe;
        private Vector3 _fallbackCenter;
        private bool _forceRefresh;
        private float _nextGlobalScanTime;
        private float _nextVisibleRefreshTime;
        private float _nextCleanupTime;
        private float _lastCombatSeenTime = -999f;

        // Registro estático para evitar FindObjectsByType (causa de travamentos)
        private static readonly HashSet<IdentidadeUnidade> _globalRegistry = new HashSet<IdentidadeUnidade>();
        private static readonly object _registryLock = new object();
        private static readonly Dictionary<int, EntityRuntimeCacheEntry> _entityRuntimeCache = new Dictionary<int, EntityRuntimeCacheEntry>();

        public static void Register(IdentidadeUnidade id)
        {
            if (id != null) _globalRegistry.Add(id);
        }

        public static void Unregister(IdentidadeUnidade id)
        {
            if (id != null) _globalRegistry.Remove(id);
        }

        public readonly List<GameObject> OwnUnits = new List<GameObject>();
        public readonly List<GameObject> OwnStructures = new List<GameObject>();
        public readonly List<GameObject> OwnCombatUnits = new List<GameObject>();
        public readonly List<IA_VisibilityProvider> VisibilityProviders = new List<IA_VisibilityProvider>();
        public readonly List<IA_EnemyObservation> VisibleEnemies = new List<IA_EnemyObservation>();

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

        public void Tick(float now, float deltaTime)
        {
            if (_forceRefresh || now >= _nextGlobalScanTime)
            {
                long refreshStart = System.Diagnostics.Stopwatch.GetTimestamp();
                RefreshOwnedAndGlobalCache(now);
                RegistrarMetricaTempo(
                    "world_refresh_ms",
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - refreshStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                _nextGlobalScanTime = now + 8f; // Aumentado: registro estático mantém dados frescos
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

            // Cleanup movido para intervalo maior — não precisa ocorrer todo Tick
            if (now >= _nextCleanupTime)
            {
                CleanupMemory(now, 120f);
                _nextCleanupTime = now + 30f;
            }
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

        public void MarkDirty()
        {
            _forceRefresh = true;
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
            for (int i = 0; i < VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = VisibleEnemies[i];
                if (obs == null || obs.Transform == null)
                {
                    continue;
                }

                float scoreDistance = Vector3.Distance(Flatten(fromPosition), Flatten(obs.Position));
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

            for (int i = 0; i < _globalIdentityCache.Count; i++)
            {
                IdentidadeUnidade id = _globalIdentityCache[i];
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID == 0 || id.teamID == _teamId)
                {
                    continue;
                }

                GameObject obj = id.gameObject;
                EntityRuntimeCacheEntry entry = GetEntityCache(obj);
                bool isStructure = entry.IsStructure;
                IA_Domain domain = entry.Domain;
                string name = entry.NormalizedName;
                float distance = Vector3.Distance(Flatten(fromPosition), Flatten(obj.transform.position));

                float score = isStructure ? 120f : 35f;
                if (domain == IA_Domain.Land)
                {
                    score += 10f;
                }

                if (name.Contains("prefeitura") || name.Contains("capital") || name.Contains("governo"))
                {
                    score += 180f;
                }
                else if (name.Contains("quartel general") || name.Contains("quartel_general") || name.Contains("hq"))
                {
                    score += 120f;
                }
                else if (name.Contains("aeroporto") || name.Contains("airport") || name.Contains("fabrica") || name.Contains("construtor") || name.Contains("estaleiro") || name.Contains("pier"))
                {
                    score += 70f;
                }

                score -= distance * 0.015f;
                if (score > bestScore)
                {
                    bestScore = score;
                    position = obj.transform.position;
                }
            }

            return bestScore > float.MinValue;
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

                EntityRuntimeCacheEntry entry = GetEntityCache(unit);
                string name = entry.NormalizedName;
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
            OwnUnits.Clear();
            OwnStructures.Clear();
            OwnCombatUnits.Clear();
            VisibilityProviders.Clear();
            _globalIdentityCache.Clear();
            ResetForceSnapshot();
            int mobileVisibilityBudget = MaxMobileVisibilityProviders;
            int structureVisibilityBudget = MaxStructureVisibilityProviders;

            // Usa o registro estático em vez de FindObjectsByType (elimina o maior hitch)
            foreach (IdentidadeUnidade id in _globalRegistry)
            {
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _globalIdentityCache.Add(id);

                if (id.teamID != _teamId)
                {
                    continue;
                }

                EntityRuntimeCacheEntry entry = GetEntityCache(id.gameObject);
                bool structure = entry.IsStructure;
                if (structure)
                {
                    OwnStructures.Add(id.gameObject);
                    AccumulateStructureSnapshot(entry);
                }
                else
                {
                    OwnUnits.Add(id.gameObject);
                    AccumulateUnitSnapshot(entry);
                    if (!entry.IsTransport)
                    {
                        OwnCombatUnits.Add(id.gameObject);
                    }
                }

                if (!ShouldRegisterVisibilityProvider(entry, structure, ref mobileVisibilityBudget, ref structureVisibilityBudget))
                {
                    continue;
                }

                VisibilityProviders.Add(new IA_VisibilityProvider
                {
                    Source = id.transform,
                    Radius = entry.VisionRadius
                });
            }

            // Fallback: se o registro estiver vazio (primeiros frames), usa FindObjectsByType uma vez
            if (_globalIdentityCache.Count == 0)
            {
                IdentidadeUnidade[] identities = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
                for (int i = 0; i < identities.Length; i++)
                {
                    _globalRegistry.Add(identities[i]);
                    _globalIdentityCache.Add(identities[i]);
                }
            }

            BaseCenter = ComputeBaseCenter();
            if (_baseProbe == null)
            {
                _baseProbe = CreateVirtualBaseProbe();
            }
            _baseProbe.position = BaseCenter;
            VisibilityProviders.Add(new IA_VisibilityProvider { Source = _baseProbe, Radius = 260f });

            LastScanTime = now;
        }

        private void RefreshVisibleEnemies(float now)
        {
            VisibleEnemies.Clear();
            if (VisibilityProviders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _globalIdentityCache.Count; i++)
            {
                IdentidadeUnidade id = _globalIdentityCache[i];
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID == 0 || id.teamID == _teamId)
                {
                    continue;
                }

                if (!IsVisible(id.transform.position))
                {
                    continue;
                }

                int instanceId = id.GetInstanceID();
                IA_EnemyObservation obs;
                if (!_enemyMemoryById.TryGetValue(instanceId, out obs))
                {
                    obs = new IA_EnemyObservation
                    {
                        InstanceId = instanceId
                    };
                    _enemyMemoryById.Add(instanceId, obs);
                }

                obs.Transform = id.transform;
                obs.Position = id.transform.position;
                obs.UnitName = id.name;
                EntityRuntimeCacheEntry entry = GetEntityCache(id.gameObject);
                obs.Domain = entry.Domain;
                obs.IsStructure = entry.IsStructure;
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
                if (now - pair.Value.LastSeenTime > maxAge)
                {
                    _staleEnemyIds.Add(pair.Key);
                }
            }

            if (_staleEnemyIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _staleEnemyIds.Count; i++)
            {
                _enemyMemoryById.Remove(_staleEnemyIds[i]);
            }
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

                float dist = (Flatten(structure.transform.position) - Flatten(reference)).sqrMagnitude;
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
                if ((Flatten(pos) - Flatten(anchor)).sqrMagnitude > radiusSqr)
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

        private static IA_Domain ClassifyDomain(GameObject obj)
        {
            string n = IA_Text.Normalize(obj.name);
            if (obj.GetComponent<ControleAviao>() != null || obj.GetComponent<ControleAviaoCaca>() != null || obj.GetComponent<Helicoptero>() != null || n.Contains("heli") || n.Contains("aviao") || n.Contains("fa1"))
            {
                return IA_Domain.Air;
            }

            if (obj.GetComponent<ControleNavioRealista>() != null || obj.GetComponent<ControleSubmarino>() != null || n.Contains("navio") || n.Contains("sub") || n.Contains("corveta"))
            {
                return IA_Domain.Naval;
            }

            return IA_Domain.Land;
        }

        // Cache de resultados IsStructure para evitar múltiplos GetComponent por objeto
        private static readonly Dictionary<int, bool> _isStructureCache = new Dictionary<int, bool>();

        public static void InvalidateStructureCache(int instanceId)
        {
            _isStructureCache.Remove(instanceId);
            _entityRuntimeCache.Remove(instanceId);
        }

        private static bool IsStructure(GameObject obj)
        {
            if (obj == null) return false;

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
            _forceSnapshot.AirUnits = 0;
            _forceSnapshot.NavalUnits = 0;
            _forceSnapshot.Submarines = 0;
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

            if (entry.Domain == IA_Domain.Naval && !entry.IsSubmarine)
            {
                _forceSnapshot.NavalUnits++;
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
            bool hasNaval = obj.GetComponent<ControleNavioRealista>() != null || n.Contains("navio") || n.Contains("corveta") || n.Contains("destroy") || n.Contains("ironclad") || n.Contains("sovereign") || n.Contains("vindicator") || n.Contains("arrowhead");
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

            bool isStructure = explicitStructure || (!hasAgent && !mobileByScript);
            IA_Domain domain = IA_Domain.Land;
            if (isAereoFromComp || hasAircraft || hasHelicopter || n.Contains("heli") || n.Contains("aviao") || n.Contains("fa1") || n.Contains("g15") || n.Contains("a_20") || n.Contains("super tuk") || isDrone)
            {
                domain = IA_Domain.Air;
            }
            else if (hasNaval || hasSubmarine || n.Contains("sub"))
            {
                domain = IA_Domain.Naval;
            }

            bool isHover = n.Contains("hover") || n.Contains("houver");
            bool isTransport = n.Contains("transporte")
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
                IsGroundTransport = (n.Contains("truck") || n.Contains("caminhao") || n.Contains("transporte")) && domain == IA_Domain.Land,
                IsHoverTransport = isHover,
                IsNavalTransport = domain == IA_Domain.Naval && (n.Contains("liberty") || n.Contains("transporte") || isHover || n.Contains("ww")),
                IsSubmarine = hasSubmarine || n.Contains("sub"),
                IsInfantry = n.Contains("sold") || n.Contains("rifle") || n.Contains("infan"),
                IsTank = n.Contains("tank") || n.Contains("mbt") || n.Contains("south") || n.Contains("arthur") || n.Contains("c1"),
                IsArtillery = n.Contains("artilh") || n.Contains("hack") || n.Contains("mlrs") || n.Contains("lancador"),
                IsHelicopter = hasHelicopter || n.Contains("heli") || n.Contains("ray") || n.Contains("vans"),
                IsFixedWing = isAereoFromComp || hasAircraft || n.Contains("fa1") || n.Contains("jet") || n.Contains("aviao") || n.Contains("g15") || n.Contains("a_20") || n.Contains("super tuk") || n.Contains("supertuk") || isDrone,
                IsRadar = n.Contains("radar"),
                IsHighValueMobile = isAereoFromComp || hasAircraft || hasHelicopter || hasNaval || hasSubmarine || isDrone,
                VisionRadius = ResolveVisionRadius(n, isStructure, hasAircraft || isAereoFromComp || isDrone, hasHelicopter, hasNaval || hasSubmarine)
            };

            _entityRuntimeCache[id] = entry;
            return entry;
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

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
