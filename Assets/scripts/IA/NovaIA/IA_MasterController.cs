// ARQUIVO 1: IA_MasterController.cs
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using Hegemonia.AI.BrainMaster;
using UnityEngine.AI;
using UnityEngine;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-850)]
    public sealed class IA_MasterController : MonoBehaviour
    {
        public enum RuntimeProfile
        {
            Auto = 0,
            Potato = 1,
            Low = 2,
            Medium = 3,
            High = 4,
            Ultra = 5
        }

        public enum RuntimeQualityPreset
        {
            Eco = 0,
            Balanced = 1,
            Aggressive = 2
        }

        public enum RuntimeSeverity
        {
            Stable = 0,
            Watch = 1,
            Throttled = 2,
            Emergency = 3
        }

        public enum GlobalStrategyMode
        {
            Balanced = 0,
            Defensive = 1,
            Aggressive = 2,
            Economic = 3,
            Expeditionary = 4
        }

        public enum ObjectiveType
        {
            None = 0,
            BuildCore = 1,
            BuildNaval = 2,
            BuildAir = 3,
            DefendBase = 4,
            ExpandEconomy = 5,
            RaidLogistics = 6,
            BlindEnemy = 7,
            SecureOil = 8,
            PrepareExpedition = 9,
            AssaultBeachhead = 10,
            PushFrontline = 11,
            SupportAlly = 12,
            RefitForPerformance = 13
        }

        [Serializable]
        public struct RuntimeTuning
        {
            [Range(20, 240)] public int targetFps;
            [Range(20, 240)] public int minimumSafeFps;
            [Range(5, 120)] public int hitchMs;
            [Range(0.05f, 2f)] public float worldRefreshInterval;
            [Range(0.05f, 2f)] public float visibilityRefreshInterval;
            [Range(0.10f, 3f)] public float strategyRefreshInterval;
            [Range(0.10f, 3f)] public float commandFlushInterval;
            [Range(0.05f, 5f)] public float heavyThinkInterval;
            [Range(0.20f, 6f)] public float gridRefreshInterval;
            [Range(0.10f, 1f)] public float cpuBudgetMs;
            [Range(0.05f, 2f)] public float worldScanMultiplier;
            [Range(4, 64)] public int maxVisibleProviders;
            [Range(1, 12)] public int maxHeavyBrainsPerFrame;
            [Range(8, 256)] public int maxCommandsPerFlush;
            [Range(1, 12)] public int gridColumnsPerTick;
            [Range(0.10f, 3f)] public float stressDecaySpeed;
            [Range(0.10f, 1f)] public float emergencyDropFactor;
        }

        [Serializable]
        public struct AttackWeights
        {
            [Range(0f, 5f)] public float radar;
            [Range(0f, 5f)] public float airfield;
            [Range(0f, 5f)] public float shipyard;
            [Range(0f, 5f)] public float oil;
            [Range(0f, 5f)] public float logistics;
            [Range(0f, 5f)] public float hq;
            [Range(0f, 5f)] public float antiBlindPenalty;
        }

        [Serializable]
        public struct TeamMessage
        {
            public int senderTeam;
            public int targetTeam;
            public string tag;
            public Vector3 worldPoint;
            public float time;
        }

        [Serializable]
        public struct StrategicBlackboard
        {
            public int teamId;
            public bool underPressure;
            public bool needsNaval;
            public bool needsAir;
            public bool lowOil;
            public bool lowLogistics;
            public bool enemyAcrossOcean;
            public int allyCount;
            public int enemyCount;
            public Vector3 baseCenter;
            public Vector3 enemyAnchor;
            public float lastUpdated;
        }

        private sealed class SharedRuntimeState
        {
            public int frameIndex = -1;
            public float frameBudgetUsedMs;
            public int heavyBrainsUsed;
            public int rrCursor;
            public RuntimeSeverity worstSeverity = RuntimeSeverity.Stable;
        }

        private static readonly List<IA_MasterController> _controllers = new List<IA_MasterController>(8);
        private static readonly Dictionary<int, StrategicBlackboard> _sharedBlackboards = new Dictionary<int, StrategicBlackboard>(16);
        private static readonly List<TeamMessage> _messages = new List<TeamMessage>(64);
        private static readonly SharedRuntimeState _sharedRuntime = new SharedRuntimeState();

        [Header("Identidade")]
        [SerializeField] private int _teamId = 2;
        [SerializeField] private int[] _alliedTeams = Array.Empty<int>();
        [SerializeField] private GlobalStrategyMode _strategyMode = GlobalStrategyMode.Balanced;
        [SerializeField] private bool _isHumanTeam = false;

        [Header("Perfil de Execução")]
        [SerializeField] private RuntimeProfile _runtimeProfile = RuntimeProfile.Auto;
        [SerializeField] private RuntimeQualityPreset _qualityPreset = RuntimeQualityPreset.Balanced;
        [SerializeField] private float _targetMinFps = 55f;
        [SerializeField] private bool _manageApplicationTargetFrameRate = true;
        [SerializeField] private bool _autoDegradeUnderStress = true;
        [SerializeField] private bool _showDiagnosticsOverlay = true;
        [SerializeField] private RuntimeTuning _potatoTuning = CreatePotatoTuning();
        [SerializeField] private RuntimeTuning _lowTuning = CreateLowTuning();
        [SerializeField] private RuntimeTuning _mediumTuning = CreateMediumTuning();
        [SerializeField] private RuntimeTuning _highTuning = CreateHighTuning();
        [SerializeField] private RuntimeTuning _ultraTuning = CreateUltraTuning();

        [Header("Integração")]
        [SerializeField] private MonoBehaviour _backendBridge;
        [SerializeField] private IA_WorldStateCache _worldStateCache;
        [SerializeField] private IA_SpatialGrid _spatialGrid;
        [SerializeField] private IA_CommandExecutor _commandExecutor;

        [Header("Mapa / Grid")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private int _gridWidth = 128;
        [SerializeField] private int _gridHeight = 128;
        [SerializeField] private float _gridCellSize = 48f;
        [SerializeField] private LayerMask _waterMask;
        [SerializeField] private LayerMask _blockerMask;
        [SerializeField] private LayerMask _landMask = ~0;

        [Header("Estratégia Militar")]
        [SerializeField] private AttackWeights _attackWeights = CreateDefaultAttackWeights();
        [SerializeField] private bool _preferBlindEnemyFirst = true;
        [SerializeField] private bool _allowExpeditionaryWar = true;
        [SerializeField] private bool _spreadAttacks = true;
        [SerializeField] private float _minimumExpeditionDistance = 900f;
        [SerializeField] private float _beachheadRadius = 240f;

        private RuntimeTuning _activeTuning;
        private RuntimeSeverity _runtimeSeverity;
        private float _nextWorldRefreshTime;
        private float _nextVisibilityRefreshTime;
        private float _nextStrategyRefreshTime;
        private float _nextCommandFlushTime;
        private float _nextHeavyThinkTime;
        private float _nextGridRefreshTime;
        private float _smoothedFps = 60f;
        private float _smoothedFrameMs = 16.6f;
        private float _stressScore;
        private int _slotIndex = -1;
        private ObjectiveType _currentObjective;
        private Vector3 _currentObjectivePoint;
        private float _currentObjectiveUntil;
        private string _lastDecision = string.Empty;
        private int _lastFlushCommandCount;
        private float _lastDecisionLatencyMs;
        private long _telemetryCmdQueued;
        private long _telemetryCmdExecuted;
        private long _telemetryCmdFailed;
        private int _telemetryCmdPending;
        private float _telemetryBuildSuccessRate = 1f;
        private readonly List<IdentidadeUnidade> _unitBuffer = new List<IdentidadeUnidade>(128);
        private readonly List<Fabrica> _factoryBuffer = new List<Fabrica>(16);
        private readonly List<Estaleiro> _shipyardBuffer = new List<Estaleiro>(8);
        private readonly List<PierMarinha> _pierBuffer = new List<PierMarinha>(8);
        private readonly List<GerenciadorAeroporto> _airportBuffer = new List<GerenciadorAeroporto>(8);
        private readonly List<Heliporto> _heliportBuffer = new List<Heliporto>(8);
        private readonly List<DadosConstrucao> _catalogBuffer = new List<DadosConstrucao>(256);
        private readonly Collider[] _spacingBuffer = new Collider[64];
        [Header("Posicionamento Seguro")]
        [SerializeField] private float _minStructureSpacing = 24f;
        [SerializeField] private float _minUnitSpacing = 6f;

        public int TeamId => _teamId;
        public RuntimeSeverity Severity => _runtimeSeverity;
        public bool IsHumanTeam => _isHumanTeam;
        public StrategicBlackboard Blackboard => BuildBlackboard();

        private void Reset()
        {
            _worldStateCache = GetComponent<IA_WorldStateCache>();
            _spatialGrid = GetComponent<IA_SpatialGrid>();
            _commandExecutor = GetComponent<IA_CommandExecutor>();
        }

        private void Awake()
        {
            EnsureDependencies();
            EnsureBackendBridge();
            RegisterController();
            ResolveActiveProfile(true);
            ConfigureChildren();
        }

        private void OnEnable()
        {
            RegisterController();
            IA_RuntimeCoordinator.Instance.Register(GetInstanceID(), _teamId);
        }

        private void OnDisable()
        {
            IA_RuntimeCoordinator.Instance.Unregister(GetInstanceID());
            UnregisterController();
        }

        private void OnDestroy()
        {
            IA_RuntimeCoordinator.Instance.Unregister(GetInstanceID());
            UnregisterController();
        }

        private void Update()
        {
            if (_isHumanTeam)
            {
                return;
            }

            EnsureBackendBridge();
            ResolveSharedFrame();
            UpdateRuntimeHealth(Time.unscaledDeltaTime);
            _runtimeSeverity = IA_RuntimeCoordinator.Instance.ResolveSeverity(GetInstanceID(), _runtimeSeverity, _smoothedFps, Mathf.Max(_targetMinFps, _activeTuning.minimumSafeFps));
            ResolveActiveProfile(false);
            PublishBlackboard();

            if (Time.time >= _nextWorldRefreshTime)
            {
                _worldStateCache.RefreshOwnedAndGlobalCache(Time.time, _activeTuning.maxVisibleProviders);
                _nextWorldRefreshTime = Time.time + _activeTuning.worldRefreshInterval;
            }

            if (Time.time >= _nextVisibilityRefreshTime)
            {
                _worldStateCache.RefreshVisibleEnemies(Time.time);
                _worldStateCache.UpdateCombatPressure(Time.time);
                _nextVisibilityRefreshTime = Time.time + _activeTuning.visibilityRefreshInterval;
            }

            if (Time.time >= _nextGridRefreshTime)
            {
                if (IA_RuntimeCoordinator.Instance.ShouldRunGrid(GetInstanceID(), Time.frameCount, _runtimeSeverity))
                {
                    _spatialGrid.TickGrid(Time.time, _activeTuning.gridColumnsPerTick, _runtimeSeverity);
                }
                _nextGridRefreshTime = Time.time + _activeTuning.gridRefreshInterval;
            }

            bool canRunHeavy = CanRunHeavyLogicThisFrame() && IA_RuntimeCoordinator.Instance.ShouldRunHeavy(GetInstanceID(), Time.frameCount);
            if (canRunHeavy && Time.time >= _nextHeavyThinkTime)
            {
                float decisionStart = Time.realtimeSinceStartup * 1000f;
                RunHeavyStrategyPass();
                _lastDecisionLatencyMs = (Time.realtimeSinceStartup * 1000f) - decisionStart;
                _nextHeavyThinkTime = Time.time + _activeTuning.heavyThinkInterval;
            }

            if (Time.time >= _nextStrategyRefreshTime)
            {
                RunStrategyPass();
                _nextStrategyRefreshTime = Time.time + _activeTuning.strategyRefreshInterval;
            }

            if (Time.time >= _nextCommandFlushTime)
            {
                IA_RuntimeCoordinator coordinator = IA_RuntimeCoordinator.Instance;
                coordinator.TargetMinFps = Mathf.Max(30f, _targetMinFps);
                int commandCap = coordinator.ResolveCommandCap(_activeTuning.maxCommandsPerFlush);
                float budgetScale = coordinator.ResolveBudgetScale(_smoothedFps);
                float budgetMs = Mathf.Max(0.08f, _activeTuning.cpuBudgetMs * budgetScale);

                _lastFlushCommandCount = _commandExecutor.Flush(commandCap, budgetMs, _backendBridge);
                _telemetryCmdQueued = _commandExecutor.TotalQueued;
                _telemetryCmdExecuted = _commandExecutor.TotalExecuted;
                _telemetryCmdFailed = _commandExecutor.TotalFailed;
                _telemetryCmdPending = _commandExecutor.PendingCount;
                _telemetryBuildSuccessRate = _commandExecutor.BuildSuccessRate;
                _nextCommandFlushTime = Time.time + _activeTuning.commandFlushInterval;
            }
        }

        private void EnsureDependencies()
        {
            if (_worldStateCache == null)
            {
                _worldStateCache = GetComponent<IA_WorldStateCache>();
                if (_worldStateCache == null)
                {
                    _worldStateCache = gameObject.AddComponent<IA_WorldStateCache>();
                }
            }

            if (_spatialGrid == null)
            {
                _spatialGrid = GetComponent<IA_SpatialGrid>();
                if (_spatialGrid == null)
                {
                    _spatialGrid = gameObject.AddComponent<IA_SpatialGrid>();
                }
            }

            if (_commandExecutor == null)
            {
                _commandExecutor = GetComponent<IA_CommandExecutor>();
                if (_commandExecutor == null)
                {
                    _commandExecutor = gameObject.AddComponent<IA_CommandExecutor>();
                }
            }
        }

        private void EnsureBackendBridge()
        {
            if (HasBackendContract(_backendBridge))
            {
                return;
            }

            MonoBehaviour[] candidates = GetComponents<MonoBehaviour>();
            for (int i = 0; i < candidates.Length; i++)
            {
                MonoBehaviour candidate = candidates[i];
                if (candidate == null || candidate == this)
                {
                    continue;
                }

                if (HasBackendContract(candidate))
                {
                    _backendBridge = candidate;
                    return;
                }
            }

            _backendBridge = this;
        }

        private static bool HasBackendContract(MonoBehaviour candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            Type type = candidate.GetType();
            return type.GetMethod("TryQueueBuild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
                && type.GetMethod("TryQueueProduction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        private void ConfigureChildren()
        {
            _worldStateCache.Configure(_teamId, _alliedTeams);
            _spatialGrid.Configure(_gridOrigin, _gridWidth, _gridHeight, _gridCellSize, _waterMask, _blockerMask, _landMask);
            _commandExecutor.Configure(_teamId);
        }

        // Fallback backend contract para IA_CommandExecutor quando não há bridge externa válida.
        public bool TryQueueBuild(int teamId, string itemKey, Vector3 worldPoint, int priority)
        {
            if (teamId != _teamId)
            {
                return false;
            }

            DadosConstrucao data;
            if (!TryResolveCatalogItem(itemKey, out data))
            {
                return false;
            }

            if (RequiresCityHall(data) && !HasTeamCityHall())
            {
                return false;
            }

            Vector3 spawnPoint;
            Quaternion rotation;
            if (!TryResolveBuildPose(data, worldPoint, out spawnPoint, out rotation))
            {
                return false;
            }

            if (IsStructureCrowded(spawnPoint, _minStructureSpacing))
            {
                Vector3 relaxedPoint;
                if (!TryFindNearbyClearPoint(spawnPoint, Mathf.Max(12f, _minStructureSpacing), out relaxedPoint)
                    || IsStructureCrowded(relaxedPoint, _minStructureSpacing))
                {
                    return false;
                }

                spawnPoint = relaxedPoint;
            }

            GameObject created = null;
            Construtor construtor = Construtor.Instancia != null ? Construtor.Instancia : FindFirstObjectByType<Construtor>();
            if (construtor != null && data.prefabDaUnidade != null)
            {
                created = construtor.ConstruirEstruturaIA(data.prefabDaUnidade, spawnPoint, rotation);
            }

            if (created == null && data.prefabDaUnidade != null)
            {
                created = Instantiate(data.prefabDaUnidade, spawnPoint, rotation);
            }

            if (created == null)
            {
                return false;
            }

            EnsureTeamIdentity(created);
            return true;
        }

        public bool QueueBuild(string itemKey, Vector3 worldPoint, int priority)
        {
            return TryQueueBuild(_teamId, itemKey, worldPoint, priority);
        }

        public bool IA_QueueBuild(string itemKey, Vector3 worldPoint, int priority)
        {
            return TryQueueBuild(_teamId, itemKey, worldPoint, priority);
        }

        public bool TryQueueProduction(int teamId, string itemKey, int priority)
        {
            if (teamId != _teamId)
            {
                return false;
            }

            DadosConstrucao data;
            if (!TryResolveCatalogItem(itemKey, out data))
            {
                return false;
            }

            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            GameObject produced;
            bool producedByInfrastructure = TryProduceViaInfrastructure(data, out produced);
            if (!producedByInfrastructure)
            {
                Vector3 spawnPoint;
                if (IsNavalItem(data))
                {
                    if (!TryResolveNavalUnitSpawnPoint(ResolveUnitSpawnPoint(), out spawnPoint))
                    {
                        return false;
                    }
                }
                else
                {
                    spawnPoint = ResolveUnitSpawnPoint();
                }

                if (IsUnitCrowded(spawnPoint, _minUnitSpacing))
                {
                    Vector3 relaxedPoint;
                    if (TryFindNearbyClearPoint(spawnPoint, Mathf.Max(4f, _minUnitSpacing), out relaxedPoint))
                    {
                        spawnPoint = relaxedPoint;
                    }
                }

                produced = Instantiate(data.prefabDaUnidade, spawnPoint, Quaternion.identity);
            }

            if (!producedByInfrastructure && produced == null)
            {
                return false;
            }

            if (produced != null)
            {
                EnsureTeamIdentity(produced);
            }
            return true;
        }

        public bool QueueProduction(string itemKey, int priority)
        {
            return TryQueueProduction(_teamId, itemKey, priority);
        }

        public bool IA_QueueProduction(string itemKey, int priority)
        {
            return TryQueueProduction(_teamId, itemKey, priority);
        }

        public bool TryIssueMovePackage(int teamId, string packageTag, Vector3 worldPoint, int priority)
        {
            if (teamId != _teamId)
            {
                return false;
            }

            return MoveTeamUnits(worldPoint);
        }

        public bool IssueMovePackage(string packageTag, Vector3 worldPoint, int priority)
        {
            return TryIssueMovePackage(_teamId, packageTag, worldPoint, priority);
        }

        public bool IA_IssueMovePackage(string packageTag, Vector3 worldPoint, int priority)
        {
            return TryIssueMovePackage(_teamId, packageTag, worldPoint, priority);
        }

        public bool TryIssueAttack(int teamId, string attackTag, Vector3 worldPoint, int priority)
        {
            if (teamId != _teamId)
            {
                return false;
            }

            return MoveTeamUnits(worldPoint);
        }

        public bool IssueAttack(string attackTag, Vector3 worldPoint, int priority)
        {
            return TryIssueAttack(_teamId, attackTag, worldPoint, priority);
        }

        public bool IA_IssueAttack(string attackTag, Vector3 worldPoint, int priority)
        {
            return TryIssueAttack(_teamId, attackTag, worldPoint, priority);
        }

        public bool TryIssueUnload(int teamId, string tag, Vector3 worldPoint, int priority)
        {
            if (teamId != _teamId)
            {
                return false;
            }

            return MoveTeamUnits(worldPoint);
        }

        public bool IssueUnload(string tag, Vector3 worldPoint, int priority)
        {
            return TryIssueUnload(_teamId, tag, worldPoint, priority);
        }

        public bool IA_IssueUnload(string tag, Vector3 worldPoint, int priority)
        {
            return TryIssueUnload(_teamId, tag, worldPoint, priority);
        }

        private bool TryProduceViaInfrastructure(DadosConstrucao data, out GameObject produced)
        {
            produced = null;
            string normalized = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);

            if (data.categoria == DadosConstrucao.CategoriaItem.Marinha || normalized.Contains("navio") || normalized.Contains("sub"))
            {
                RegistroEntidadesJogo.FillEstaleiros(_shipyardBuffer);
                for (int i = 0; i < _shipyardBuffer.Count; i++)
                {
                    Estaleiro shipyard = _shipyardBuffer[i];
                    if (shipyard == null || !BelongsToTeam(shipyard.gameObject))
                    {
                        continue;
                    }

                    if (shipyard.ConstruirUnidade(data.prefabDaUnidade))
                    {
                        return true;
                    }
                }

                RegistroEntidadesJogo.FillPiers(_pierBuffer);
                for (int i = 0; i < _pierBuffer.Count; i++)
                {
                    PierMarinha pier = _pierBuffer[i];
                    if (pier == null || !BelongsToTeam(pier.gameObject))
                    {
                        continue;
                    }

                    if (pier.ConstruirNavio(data.prefabDaUnidade))
                    {
                        return true;
                    }
                }
            }

            if (data.categoria == DadosConstrucao.CategoriaItem.Aeronautica || normalized.Contains("aviao") || normalized.Contains("caca"))
            {
                RegistroEntidadesJogo.FillAeroportos(_airportBuffer);
                for (int i = 0; i < _airportBuffer.Count; i++)
                {
                    GerenciadorAeroporto airport = _airportBuffer[i];
                    if (airport == null || !BelongsToTeam(airport.gameObject))
                    {
                        continue;
                    }

                    airport.ComprarAviao(data.prefabDaUnidade);
                    return true;
                }

                RegistroEntidadesJogo.FillHeliportos(_heliportBuffer);
                for (int i = 0; i < _heliportBuffer.Count; i++)
                {
                    Heliporto heliport = _heliportBuffer[i];
                    if (heliport == null || !BelongsToTeam(heliport.gameObject) || !heliport.TemEspacoParaPousar())
                    {
                        continue;
                    }

                    produced = Instantiate(data.prefabDaUnidade, heliport.ObterPontoDePousoMundial(), heliport.transform.rotation);
                    return produced != null;
                }
            }

            RegistroEntidadesJogo.FillFabricas(_factoryBuffer);
            for (int i = 0; i < _factoryBuffer.Count; i++)
            {
                Fabrica factory = _factoryBuffer[i];
                if (factory == null || !BelongsToTeam(factory.gameObject))
                {
                    continue;
                }

                GameObject unit = factory.ProduzirUnidade(data.prefabDaUnidade);
                if (unit != null)
                {
                    produced = unit;
                    return true;
                }
            }

            return false;
        }

        private bool MoveTeamUnits(Vector3 worldPoint)
        {
            RegistroEntidadesJogo.FillUnidades(_unitBuffer);
            int moved = 0;
            for (int i = 0; i < _unitBuffer.Count; i++)
            {
                IdentidadeUnidade unit = _unitBuffer[i];
                if (unit == null || unit.teamID != _teamId || unit.tipoUnidade == TipoUnidade.Estrutura)
                {
                    continue;
                }

                GameObject go = unit.gameObject;
                ControleUnidade controle = go.GetComponent<ControleUnidade>();
                if (controle != null)
                {
                    controle.EmitirOrdemMover(worldPoint);
                    moved++;
                    continue;
                }

                go.SendMessage("MoverParaPonto", worldPoint, SendMessageOptions.DontRequireReceiver);
                NavMeshAgent nav = go.GetComponent<NavMeshAgent>();
                if (nav != null && nav.enabled && nav.isOnNavMesh)
                {
                    // Transicao: nao force SetDestination aqui. Se a unidade nao tem facade, preferimos revelar isso.
                    nav.isStopped = false;
                }

                moved++;
            }

            return moved > 0;
        }

        private bool TryResolveCatalogItem(string itemKey, out DadosConstrucao data)
        {
            data = null;
            string key = IA_Text.Normalize(itemKey);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            _catalogBuffer.Clear();
            if (MenuConstrucao.catalogoGlobal != null)
            {
                _catalogBuffer.AddRange(MenuConstrucao.catalogoGlobal);
            }

            DadosConstrucao[] fallback = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
            for (int i = 0; i < fallback.Length; i++)
            {
                DadosConstrucao candidate = fallback[i];
                if (candidate != null && !_catalogBuffer.Contains(candidate))
                {
                    _catalogBuffer.Add(candidate);
                }
            }

            for (int i = 0; i < _catalogBuffer.Count; i++)
            {
                DadosConstrucao candidate = _catalogBuffer[i];
                if (candidate == null || candidate.prefabDaUnidade == null)
                {
                    continue;
                }

                string byName = IA_Text.Normalize(candidate.nomeItem);
                string byAsset = IA_Text.Normalize(candidate.name);
                string byPrefab = IA_Text.Normalize(candidate.prefabDaUnidade.name);
                if (byName == key || byAsset == key || byPrefab == key
                    || byName.Contains(key) || key.Contains(byName)
                    || byPrefab.Contains(key) || key.Contains(byPrefab))
                {
                    data = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveBuildPose(DadosConstrucao data, Vector3 requestedPoint, out Vector3 point, out Quaternion rotation)
        {
            point = requestedPoint;
            rotation = Quaternion.identity;

            if (point.sqrMagnitude <= 1f)
            {
                point = ResolveUnitSpawnPoint();
            }

            if (IsNavalBuildItem(data))
            {
                if (RequiresCoastalStructure(data))
                {
                    NavalPlacementResolver.StructurePose pose;
                    if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, point, rotation, out pose))
                    {
                        return false;
                    }

                    point = pose.Position;
                    rotation = pose.Rotation;
                    return true;
                }

                Vector3 waterPoint;
                if (!TryResolveNavalUnitSpawnPoint(point, out waterPoint))
                {
                    return false;
                }

                point = waterPoint;
                return true;
            }

            if (Mathf.Abs(point.y) <= 0.01f)
            {
                if (Physics.Raycast(new Vector3(point.x, point.y + 500f, point.z), Vector3.down, out RaycastHit hit, 1200f, _landMask))
                {
                    point = hit.point;
                }
            }

            return true;
        }

        private bool TryResolveNavalUnitSpawnPoint(Vector3 anchor, out Vector3 spawnPoint)
        {
            float seaLevel;
            string reason;
            if (!NavalPlacementResolver.TryResolveWaterSpawn(anchor, Vector3.forward, 0f, 60f, out spawnPoint, out seaLevel, out reason))
            {
                return false;
            }

            if (_waterMask.value != 0)
            {
                Vector3 rayStart = new Vector3(spawnPoint.x, seaLevel + 120f, spawnPoint.z);
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 260f, _waterMask))
                {
                    return false;
                }

                spawnPoint = hit.point;
            }

            return true;
        }

        private Vector3 ResolveUnitSpawnPoint()
        {
            Vector3 baseCenter = _worldStateCache != null ? _worldStateCache.Snapshot.BaseCenter : Vector3.zero;
            if (baseCenter.sqrMagnitude > 1f)
            {
                return baseCenter + new Vector3(UnityEngine.Random.Range(-20f, 20f), 0f, UnityEngine.Random.Range(-20f, 20f));
            }

            RegistroEntidadesJogo.FillUnidades(_unitBuffer);
            for (int i = 0; i < _unitBuffer.Count; i++)
            {
                IdentidadeUnidade unit = _unitBuffer[i];
                if (unit != null && unit.teamID == _teamId)
                {
                    return unit.transform.position + new Vector3(8f, 0f, 8f);
                }
            }

            return transform.position + new Vector3(12f, 0f, 12f);
        }

        private void EnsureTeamIdentity(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            IdentidadeUnidade identity = instance.GetComponent<IdentidadeUnidade>();
            if (identity == null)
            {
                identity = instance.AddComponent<IdentidadeUnidade>();
            }

            identity.teamID = _teamId;
            instance.SetActive(true);
        }

        private bool IsNavalItem(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);
            return data.categoria == DadosConstrucao.CategoriaItem.Marinha
                   || n.Contains("navio")
                   || n.Contains("sub")
                   || n.Contains("carrier")
                   || n.Contains("porta avioes");
        }

        private bool IsNavalBuildItem(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);
            return n.Contains("estaleiro")
                   || n.Contains("pier")
                   || n.Contains("plataforma");
        }

        private bool RequiresCoastalStructure(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);
            return n.Contains("estaleiro") || n.Contains("pier");
        }

        private bool IsCityHallItem(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);
            return n.Contains("prefeitura")
                   || n.Contains("governo")
                   || n.Contains("capital")
                   || n.Contains("complexo");
        }

        private bool RequiresCityHall(DadosConstrucao data)
        {
            return !IsCityHallItem(data);
        }

        private bool HasTeamCityHall()
        {
            if (_worldStateCache != null && _worldStateCache.Snapshot.CityHallCount > 0)
            {
                return true;
            }

            RegistroEntidadesJogo.FillUnidades(_unitBuffer);
            for (int i = 0; i < _unitBuffer.Count; i++)
            {
                IdentidadeUnidade id = _unitBuffer[i];
                if (id == null || id.teamID != _teamId)
                {
                    continue;
                }

                string n = IA_Text.Normalize(id.name);
                if (n.Contains("prefeitura") || n.Contains("governo") || n.Contains("capital") || n.Contains("complexo"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsStructureCrowded(Vector3 point, float radius)
        {
            int mask = _blockerMask.value != 0 ? _blockerMask.value : ~0;
            int count = Physics.OverlapSphereNonAlloc(point, Mathf.Max(1f, radius), _spacingBuffer, mask);
            for (int i = 0; i < count; i++)
            {
                Collider col = _spacingBuffer[i];
                if (col == null)
                {
                    continue;
                }

                IdentidadeUnidade id = col.GetComponentInParent<IdentidadeUnidade>();
                if (id != null && id.tipoUnidade == TipoUnidade.Estrutura)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsUnitCrowded(Vector3 point, float radius)
        {
            int mask = _blockerMask.value != 0 ? _blockerMask.value : ~0;
            return Physics.CheckSphere(point, Mathf.Max(0.75f, radius), mask);
        }

        private bool TryFindNearbyClearPoint(Vector3 anchor, float spacing, out Vector3 clearPoint)
        {
            clearPoint = anchor;
            float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
            for (int ring = 1; ring <= 4; ring++)
            {
                float radius = spacing * ring;
                int steps = 8 + (ring * 4);
                for (int i = 0; i < steps; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / steps;
                    Vector3 candidate = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    candidate.y = Mathf.Max(candidate.y, seaLevel);
                    if (!IsStructureCrowded(candidate, spacing) && !IsUnitCrowded(candidate, Mathf.Min(spacing, _minUnitSpacing)))
                    {
                        clearPoint = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool BelongsToTeam(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            IdentidadeUnidade id = go.GetComponent<IdentidadeUnidade>();
            if (id == null)
            {
                id = go.GetComponentInParent<IdentidadeUnidade>();
            }

            return id != null && id.teamID == _teamId;
        }

        private void RegisterController()
        {
            if (_controllers.Contains(this))
            {
                _slotIndex = _controllers.IndexOf(this);
                return;
            }

            _controllers.Add(this);
            _slotIndex = _controllers.Count - 1;
        }

        private void UnregisterController()
        {
            _controllers.Remove(this);
            if (_sharedBlackboards.ContainsKey(_teamId))
            {
                _sharedBlackboards.Remove(_teamId);
            }
        }

        private void ResolveSharedFrame()
        {
            if (_sharedRuntime.frameIndex == Time.frameCount)
            {
                return;
            }

            _sharedRuntime.frameIndex = Time.frameCount;
            _sharedRuntime.frameBudgetUsedMs = 0f;
            _sharedRuntime.heavyBrainsUsed = 0;
            _sharedRuntime.rrCursor++;
            _sharedRuntime.worstSeverity = RuntimeSeverity.Stable;
        }

        private void UpdateRuntimeHealth(float deltaTime)
        {
            float fps = 1f / Mathf.Max(0.0001f, deltaTime);
            float frameMs = deltaTime * 1000f;

            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.08f);
            _smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs, frameMs, 0.08f);

            bool hitch = frameMs >= _activeTuning.hitchMs;
            float lowFpsFactor = _smoothedFps < _activeTuning.minimumSafeFps ? 1f : 0f;
            float hitchFactor = hitch ? 1f : 0f;

            float targetStress = Mathf.Clamp01((lowFpsFactor * 0.7f) + (hitchFactor * 0.6f));
            _stressScore = Mathf.MoveTowards(_stressScore, targetStress, Time.unscaledDeltaTime * _activeTuning.stressDecaySpeed);

            if (_stressScore >= 0.90f)
            {
                _runtimeSeverity = RuntimeSeverity.Emergency;
            }
            else if (_stressScore >= 0.55f)
            {
                _runtimeSeverity = RuntimeSeverity.Throttled;
            }
            else if (_stressScore >= 0.22f)
            {
                _runtimeSeverity = RuntimeSeverity.Watch;
            }
            else
            {
                _runtimeSeverity = RuntimeSeverity.Stable;
            }

            if (_runtimeSeverity > _sharedRuntime.worstSeverity)
            {
                _sharedRuntime.worstSeverity = _runtimeSeverity;
            }
        }

        private void ResolveActiveProfile(bool force)
        {
            RuntimeProfile effectiveProfile = _runtimeProfile;
            if (_runtimeProfile == RuntimeProfile.Auto)
            {
                effectiveProfile = ResolveAutoProfile();
            }

            RuntimeTuning resolved = GetTuning(effectiveProfile);
            ApplyQualityPreset(ref resolved);
            if (_autoDegradeUnderStress)
            {
                ApplySeverityOverrides(ref resolved);
            }

            _activeTuning = resolved;

            if (_manageApplicationTargetFrameRate)
            {
                Application.targetFrameRate = _activeTuning.targetFps;
            }
        }

        private void ApplyQualityPreset(ref RuntimeTuning tuning)
        {
            switch (_qualityPreset)
            {
                case RuntimeQualityPreset.Eco:
                    tuning.cpuBudgetMs *= 0.75f;
                    tuning.worldRefreshInterval *= 1.20f;
                    tuning.visibilityRefreshInterval *= 1.20f;
                    tuning.strategyRefreshInterval *= 1.15f;
                    tuning.heavyThinkInterval *= 1.25f;
                    tuning.maxCommandsPerFlush = Mathf.Max(6, Mathf.RoundToInt(tuning.maxCommandsPerFlush * 0.70f));
                    tuning.gridColumnsPerTick = Mathf.Max(1, tuning.gridColumnsPerTick - 1);
                    tuning.targetFps = Mathf.Max(55, tuning.targetFps);
                    break;
                case RuntimeQualityPreset.Aggressive:
                    tuning.cpuBudgetMs *= 1.20f;
                    tuning.strategyRefreshInterval *= 0.85f;
                    tuning.heavyThinkInterval *= 0.90f;
                    tuning.maxCommandsPerFlush = Mathf.Min(64, Mathf.RoundToInt(tuning.maxCommandsPerFlush * 1.20f));
                    break;
            }
        }

        private RuntimeProfile ResolveAutoProfile()
        {
            int mem = SystemInfo.systemMemorySize;
            int vram = SystemInfo.graphicsMemorySize;
            int cores = SystemInfo.processorCount;

            int score = 0;
            if (mem >= 8000) score += 2;
            else if (mem >= 4000) score += 1;

            if (vram >= 6000) score += 2;
            else if (vram >= 2048) score += 1;

            if (cores >= 8) score += 2;
            else if (cores >= 4) score += 1;

            if (score <= 1) return RuntimeProfile.Potato;
            if (score <= 2) return RuntimeProfile.Low;
            if (score <= 4) return RuntimeProfile.Medium;
            if (score <= 5) return RuntimeProfile.High;
            return RuntimeProfile.Ultra;
        }

        private void ApplySeverityOverrides(ref RuntimeTuning tuning)
        {
            switch (_runtimeSeverity)
            {
                case RuntimeSeverity.Watch:
                    tuning.cpuBudgetMs *= 0.85f;
                    tuning.maxCommandsPerFlush = Mathf.Max(8, Mathf.RoundToInt(tuning.maxCommandsPerFlush * 0.85f));
                    tuning.gridColumnsPerTick = Mathf.Max(1, tuning.gridColumnsPerTick - 1);
                    break;
                case RuntimeSeverity.Throttled:
                    tuning.cpuBudgetMs *= 0.65f;
                    tuning.worldRefreshInterval *= 1.15f;
                    tuning.visibilityRefreshInterval *= 1.20f;
                    tuning.strategyRefreshInterval *= 1.25f;
                    tuning.commandFlushInterval *= 1.10f;
                    tuning.heavyThinkInterval *= 1.35f;
                    tuning.gridRefreshInterval *= 1.25f;
                    tuning.maxCommandsPerFlush = Mathf.Max(6, Mathf.RoundToInt(tuning.maxCommandsPerFlush * 0.70f));
                    tuning.gridColumnsPerTick = Mathf.Max(1, Mathf.RoundToInt(tuning.gridColumnsPerTick * 0.60f));
                    tuning.maxHeavyBrainsPerFrame = Mathf.Max(1, tuning.maxHeavyBrainsPerFrame - 1);
                    break;
                case RuntimeSeverity.Emergency:
                    tuning.cpuBudgetMs *= Mathf.Clamp(_activeTuning.emergencyDropFactor, 0.10f, 0.50f);
                    tuning.worldRefreshInterval *= 1.40f;
                    tuning.visibilityRefreshInterval *= 1.55f;
                    tuning.strategyRefreshInterval *= 1.65f;
                    tuning.commandFlushInterval *= 1.30f;
                    tuning.heavyThinkInterval *= 2.10f;
                    tuning.gridRefreshInterval *= 1.70f;
                    tuning.maxCommandsPerFlush = Mathf.Max(4, Mathf.RoundToInt(tuning.maxCommandsPerFlush * 0.45f));
                    tuning.gridColumnsPerTick = 1;
                    tuning.maxHeavyBrainsPerFrame = 1;
                    break;
            }
        }

        private RuntimeTuning GetTuning(RuntimeProfile profile)
        {
            switch (profile)
            {
                case RuntimeProfile.Potato: return _potatoTuning;
                case RuntimeProfile.Low: return _lowTuning;
                case RuntimeProfile.Medium: return _mediumTuning;
                case RuntimeProfile.High: return _highTuning;
                case RuntimeProfile.Ultra: return _ultraTuning;
                default: return _mediumTuning;
            }
        }

        private bool CanRunHeavyLogicThisFrame()
        {
            if (_controllers.Count == 0)
            {
                return true;
            }

            if (_sharedRuntime.heavyBrainsUsed >= _activeTuning.maxHeavyBrainsPerFrame)
            {
                return false;
            }

            int turn = _sharedRuntime.rrCursor % _controllers.Count;
            int myIndex = Mathf.Clamp(_controllers.IndexOf(this), 0, Mathf.Max(0, _controllers.Count - 1));
            bool allowed = myIndex == turn;
            if (allowed)
            {
                _sharedRuntime.heavyBrainsUsed++;
            }

            return allowed;
        }

        private void RunHeavyStrategyPass()
        {
            var snapshot = _worldStateCache.Snapshot;
            bool enemyAcrossOcean = false;
            Vector3 enemyAnchor;
            if (_worldStateCache.TryGetEnemyStrategicAnchor(out enemyAnchor))
            {
                enemyAcrossOcean = _allowExpeditionaryWar && _spatialGrid.IsLikelyAcrossOcean(snapshot.BaseCenter, enemyAnchor, _minimumExpeditionDistance);
            }

            if (enemyAcrossOcean && snapshot.NavalTransportCount <= 0)
            {
                QueueBuildNavalTransport();
                _currentObjective = ObjectiveType.PrepareExpedition;
                _currentObjectivePoint = snapshot.BaseCenter;
                _currentObjectiveUntil = Time.time + 20f;
                _lastDecision = "Preparar expedição naval";
                return;
            }

            if (enemyAcrossOcean && snapshot.CarrierCount <= 0 && snapshot.AirUnits > 6)
            {
                QueueBuildCarrierSupport();
                _currentObjective = ObjectiveType.BuildNaval;
                _currentObjectivePoint = snapshot.BaseCenter;
                _currentObjectiveUntil = Time.time + 20f;
                _lastDecision = "Preparar cobertura aérea embarcada";
                return;
            }

            if (snapshot.UnderThreat)
            {
                _currentObjective = ObjectiveType.DefendBase;
                _currentObjectivePoint = snapshot.BaseCenter;
                QueueDefensiveCommands();
                _lastDecision = "Defesa prioritária";
                return;
            }

            if (snapshot.FactoryCount <= 0 || snapshot.BarracksCount <= 0)
            {
                _currentObjective = ObjectiveType.BuildCore;
                _currentObjectivePoint = snapshot.BaseCenter;
                QueueCoreBuilds(snapshot);
                _lastDecision = "Fechar infraestrutura crítica";
                return;
            }

            if (snapshot.LowOil && snapshot.PlatformCount <= 0)
            {
                _currentObjective = ObjectiveType.SecureOil;
                _currentObjectivePoint = _spatialGrid.FindBestEconomicCoast(snapshot.BaseCenter);
                QueueOilExpansion();
                _lastDecision = "Expandir petróleo";
                return;
            }

            if (_preferBlindEnemyFirst && TryQueueBlindEnemyRaid(enemyAnchor))
            {
                _currentObjective = ObjectiveType.BlindEnemy;
                _currentObjectivePoint = enemyAnchor;
                _currentObjectiveUntil = Time.time + 24f;
                _lastDecision = "Cegar o inimigo";
                return;
            }

            if (TryQueueStrategicRaid(enemyAnchor))
            {
                _currentObjective = ObjectiveType.RaidLogistics;
                _currentObjectivePoint = enemyAnchor;
                _currentObjectiveUntil = Time.time + 24f;
                _lastDecision = "Raid estratégico";
                return;
            }

            _currentObjective = ObjectiveType.ExpandEconomy;
            _currentObjectivePoint = snapshot.BaseCenter;
            QueueEconomicGrowth(snapshot);
            _lastDecision = "Crescimento controlado";
        }

        private void RunStrategyPass()
        {
            var snapshot = _worldStateCache.Snapshot;
            if (_runtimeSeverity == RuntimeSeverity.Emergency)
            {
                _commandExecutor.ClearNonCritical();
                _currentObjective = ObjectiveType.RefitForPerformance;
                _lastDecision = "Modo emergência: reduzir carga";
                return;
            }

            if (snapshot.EnemyVisibleCount > 0)
            {
                Vector3 hotspot = _worldStateCache.GetPrimaryThreatPoint();
                if (snapshot.NavalUnits > 0 || snapshot.AirUnits > 0)
                {
                    QueuePressureAttacks(hotspot);
                }
                else
                {
                    QueueGroundDefense(hotspot);
                }
                return;
            }

            if (Time.time > _currentObjectiveUntil)
            {
                _currentObjective = ObjectiveType.None;
            }

            if (_currentObjective == ObjectiveType.None && snapshot.CanExpand)
            {
                QueueScoutAndExpand(snapshot);
            }
        }

        private void QueueCoreBuilds(IA_WorldStateCache.WorldSnapshot snapshot)
        {
            string core = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Core, "Prefeitura");
            string barracks = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Barracks, "quartel");
            string factory = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Factory, "fabrica");
            string warehouse = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Warehouse, "armazem");

            if (snapshot.CityHallCount <= 0)
            {
                _commandExecutor.QueueBuild(core, snapshot.BaseCenter, 1000, true);
            }
            if (snapshot.BarracksCount <= 0)
            {
                _commandExecutor.QueueBuild(barracks, snapshot.BaseCenter, 950, true);
            }
            if (snapshot.FactoryCount <= 0)
            {
                _commandExecutor.QueueBuild(factory, snapshot.BaseCenter, 940, true);
            }
            if (snapshot.WarehouseCount <= 0)
            {
                _commandExecutor.QueueBuild(warehouse, snapshot.BaseCenter, 930, false);
            }
        }

        private void QueueBuildNavalTransport()
        {
            string navalTransport = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.NavalTransport, "Navio Transporte");
            string shipyard = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Shipyard, "Estaleiro Naval");
            _commandExecutor.QueueProduction(navalTransport, 920, true);
            _commandExecutor.QueueBuild(shipyard, _worldStateCache.Snapshot.BaseCenter, 910, false);
        }

        private void QueueBuildCarrierSupport()
        {
            string carrier = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Carrier, "Porta Avioes");
            string fighter = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Fighter, "Caca");
            _commandExecutor.QueueProduction(carrier, 905, true);
            _commandExecutor.QueueProduction(fighter, 900, false);
        }

        private void QueueDefensiveCommands()
        {
            Vector3 p = _worldStateCache.Snapshot.BaseCenter;
            string radar = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Radar, "radar");
            string ciws = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Ciws, "CIWS");
            string turret = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Turret, "torreta");
            _commandExecutor.QueueBuild(radar, p, 870, false);
            _commandExecutor.QueueBuild(ciws, p, 860, false);
            _commandExecutor.QueueBuild(turret, p, 850, false);
        }

        private void QueueOilExpansion()
        {
            Vector3 coast = _spatialGrid.FindBestEconomicCoast(_worldStateCache.Snapshot.BaseCenter);
            string platform = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Platform, "PLataforma");
            string oilShip = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.OilShip, "Navio Petrolifero");
            _commandExecutor.QueueBuild(platform, coast, 840, false);
            _commandExecutor.QueueProduction(oilShip, 830, false);
        }

        private bool TryQueueBlindEnemyRaid(Vector3 enemyAnchor)
        {
            IA_WorldStateCache.TargetInfo target;
            if (!_worldStateCache.TryGetBestEnemyTarget(_attackWeights.radar, _attackWeights.airfield, _attackWeights.shipyard, _attackWeights.oil, _attackWeights.logistics, _attackWeights.hq, true, out target))
            {
                return false;
            }

            QueueAttackPackage(target.Position, target.Name, target.Domain, true);
            return true;
        }

        private bool TryQueueStrategicRaid(Vector3 enemyAnchor)
        {
            IA_WorldStateCache.TargetInfo target;
            if (!_worldStateCache.TryGetBestEnemyTarget(_attackWeights.radar, _attackWeights.airfield, _attackWeights.shipyard, _attackWeights.oil, _attackWeights.logistics, _attackWeights.hq, false, out target))
            {
                return false;
            }

            QueueAttackPackage(target.Position, target.Name, target.Domain, false);
            return true;
        }

        private void QueueAttackPackage(Vector3 targetPos, string targetName, IA_WorldStateCache.DomainHint domain, bool disableSensorsFirst)
        {
            bool acrossOcean = _spatialGrid.IsLikelyAcrossOcean(_worldStateCache.Snapshot.BaseCenter, targetPos, _minimumExpeditionDistance);
            if (acrossOcean)
            {
                Vector3 staging = _spatialGrid.FindBestNavalStagingPoint(_worldStateCache.Snapshot.BaseCenter, targetPos);
                Vector3 beach = _spatialGrid.FindBestLandingPoint(targetPos, _beachheadRadius);

                _commandExecutor.QueueMovePackage("naval_strike", staging, 820, true);
                _commandExecutor.QueueUnloadPackage("landing", beach, 810, true);
            }

            if (disableSensorsFirst)
            {
                _commandExecutor.QueueAttack("air_precision", targetPos, 800, true);
                _commandExecutor.QueueAttack("naval_long_range", targetPos, 790, false);
                return;
            }

            if (_spreadAttacks)
            {
                Vector3 flank = targetPos + new Vector3(180f, 0f, -120f);
                _commandExecutor.QueueAttack("air_precision", targetPos, 780, true);
                _commandExecutor.QueueAttack("land_push", flank, 770, false);
                _commandExecutor.QueueAttack("naval_pressure", targetPos, 760, false);
            }
            else
            {
                _commandExecutor.QueueAttack("full_focus", targetPos, 780, true);
            }
        }

        private void QueuePressureAttacks(Vector3 hotspot)
        {
            _commandExecutor.QueueAttack("pressure_air", hotspot, 730, false);
            _commandExecutor.QueueAttack("pressure_land", hotspot, 720, false);
        }

        private void QueueGroundDefense(Vector3 hotspot)
        {
            string turret = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Turret, "torreta");
            _commandExecutor.QueueMovePackage("ground_defense", hotspot, 710, false);
            _commandExecutor.QueueBuild(turret, hotspot, 700, false);
        }

        private void QueueEconomicGrowth(IA_WorldStateCache.WorldSnapshot snapshot)
        {
            string warehouse = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Warehouse, "armazem");
            string factory = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Factory, "fabrica");
            string airport = IA_CatalogRoleResolver.ResolveOrFallback(IA_CatalogRole.Airport, "aeroporto");

            if (snapshot.WarehouseCount <= 1)
            {
                _commandExecutor.QueueBuild(warehouse, snapshot.BaseCenter, 650, false);
            }
            if (snapshot.FactoryCount < 2)
            {
                _commandExecutor.QueueBuild(factory, snapshot.BaseCenter, 640, false);
            }
            if (snapshot.AirportCount <= 0)
            {
                _commandExecutor.QueueBuild(airport, snapshot.BaseCenter, 630, false);
            }
        }

        private void QueueScoutAndExpand(IA_WorldStateCache.WorldSnapshot snapshot)
        {
            Vector3 scoutPoint = _spatialGrid.FindScoutPoint(snapshot.BaseCenter, snapshot.LastKnownEnemyAnchor);
            _commandExecutor.QueueMovePackage("scout", scoutPoint, 610, false);
        }

        private StrategicBlackboard BuildBlackboard()
        {
            var s = _worldStateCache.Snapshot;
            StrategicBlackboard board = new StrategicBlackboard
            {
                teamId = _teamId,
                underPressure = s.UnderThreat,
                needsNaval = s.EnemyAcrossOcean && (s.NavalTransportCount <= 0 || s.ShipyardCount <= 0),
                needsAir = s.AirUnits <= 2 && s.AirportCount > 0,
                lowOil = s.LowOil,
                lowLogistics = s.LowLogistics,
                enemyAcrossOcean = s.EnemyAcrossOcean,
                allyCount = CountAlliesAlive(),
                enemyCount = CountEnemiesAlive(),
                baseCenter = s.BaseCenter,
                enemyAnchor = s.LastKnownEnemyAnchor,
                lastUpdated = Time.time
            };
            return board;
        }

        private void PublishBlackboard()
        {
            _sharedBlackboards[_teamId] = BuildBlackboard();
        }

        private int CountAlliesAlive()
        {
            int count = 0;
            for (int i = 0; i < _controllers.Count; i++)
            {
                if (_controllers[i] == null || _controllers[i] == this)
                {
                    continue;
                }
                if (IsAlliedWith(_controllers[i]._teamId))
                {
                    count++;
                }
            }
            return count;
        }

        private int CountEnemiesAlive()
        {
            int count = 0;
            for (int i = 0; i < _controllers.Count; i++)
            {
                if (_controllers[i] == null || _controllers[i] == this)
                {
                    continue;
                }
                if (!IsAlliedWith(_controllers[i]._teamId))
                {
                    count++;
                }
            }
            return count;
        }

        private bool IsAlliedWith(int otherTeam)
        {
            if (otherTeam == _teamId)
            {
                return true;
            }

            for (int i = 0; i < _alliedTeams.Length; i++)
            {
                if (_alliedTeams[i] == otherTeam)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnGUI()
        {
            if (!_showDiagnosticsOverlay || _isHumanTeam)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f + (_slotIndex * 144f), 500f, 140f), GUI.skin.box);
            GUILayout.Label($"IA Team {_teamId} | Perfil: {_runtimeProfile}/{_qualityPreset} | Severidade: {_runtimeSeverity}");
            GUILayout.Label($"FPS={_smoothedFps:0.0} | Frame={_smoothedFrameMs:0.0}ms | Stress={_stressScore:0.00} | CmdFlush={_lastFlushCommandCount}");
            GUILayout.Label($"Objetivo={_currentObjective} | Decisão={_lastDecision}");
            GUILayout.Label($"World={_activeTuning.worldRefreshInterval:0.00}s | Vis={_activeTuning.visibilityRefreshInterval:0.00}s | Heavy={_activeTuning.heavyThinkInterval:0.00}s | GridCols={_activeTuning.gridColumnsPerTick}");
            GUILayout.Label($"Queued={_telemetryCmdQueued} | Pending={_telemetryCmdPending} | Ok={_telemetryCmdExecuted} | Fail={_telemetryCmdFailed} | BuildRate={_telemetryBuildSuccessRate:0.00} | DecideMs={_lastDecisionLatencyMs:0.0}");
            GUILayout.EndArea();
        }

        private static RuntimeTuning CreatePotatoTuning()
        {
            return new RuntimeTuning
            {
                targetFps = 45,
                minimumSafeFps = 28,
                hitchMs = 45,
                worldRefreshInterval = 1.10f,
                visibilityRefreshInterval = 0.90f,
                strategyRefreshInterval = 1.30f,
                commandFlushInterval = 0.28f,
                heavyThinkInterval = 2.40f,
                gridRefreshInterval = 0.45f,
                cpuBudgetMs = 0.25f,
                worldScanMultiplier = 1.6f,
                maxVisibleProviders = 10,
                maxHeavyBrainsPerFrame = 1,
                maxCommandsPerFlush = 10,
                gridColumnsPerTick = 1,
                stressDecaySpeed = 0.75f,
                emergencyDropFactor = 0.22f
            };
        }

        private static RuntimeTuning CreateLowTuning()
        {
            return new RuntimeTuning
            {
                targetFps = 60,
                minimumSafeFps = 35,
                hitchMs = 40,
                worldRefreshInterval = 0.85f,
                visibilityRefreshInterval = 0.65f,
                strategyRefreshInterval = 1.00f,
                commandFlushInterval = 0.24f,
                heavyThinkInterval = 1.85f,
                gridRefreshInterval = 0.35f,
                cpuBudgetMs = 0.32f,
                worldScanMultiplier = 1.3f,
                maxVisibleProviders = 14,
                maxHeavyBrainsPerFrame = 1,
                maxCommandsPerFlush = 14,
                gridColumnsPerTick = 2,
                stressDecaySpeed = 0.95f,
                emergencyDropFactor = 0.25f
            };
        }

        private static RuntimeTuning CreateMediumTuning()
        {
            return new RuntimeTuning
            {
                targetFps = 60,
                minimumSafeFps = 40,
                hitchMs = 35,
                worldRefreshInterval = 0.65f,
                visibilityRefreshInterval = 0.42f,
                strategyRefreshInterval = 0.75f,
                commandFlushInterval = 0.20f,
                heavyThinkInterval = 1.35f,
                gridRefreshInterval = 0.28f,
                cpuBudgetMs = 0.45f,
                worldScanMultiplier = 1.0f,
                maxVisibleProviders = 18,
                maxHeavyBrainsPerFrame = 1,
                maxCommandsPerFlush = 20,
                gridColumnsPerTick = 3,
                stressDecaySpeed = 1.10f,
                emergencyDropFactor = 0.28f
            };
        }

        private static RuntimeTuning CreateHighTuning()
        {
            return new RuntimeTuning
            {
                targetFps = 90,
                minimumSafeFps = 48,
                hitchMs = 30,
                worldRefreshInterval = 0.48f,
                visibilityRefreshInterval = 0.30f,
                strategyRefreshInterval = 0.55f,
                commandFlushInterval = 0.16f,
                heavyThinkInterval = 1.00f,
                gridRefreshInterval = 0.22f,
                cpuBudgetMs = 0.60f,
                worldScanMultiplier = 0.9f,
                maxVisibleProviders = 24,
                maxHeavyBrainsPerFrame = 2,
                maxCommandsPerFlush = 26,
                gridColumnsPerTick = 4,
                stressDecaySpeed = 1.25f,
                emergencyDropFactor = 0.32f
            };
        }

        private static RuntimeTuning CreateUltraTuning()
        {
            return new RuntimeTuning
            {
                targetFps = 120,
                minimumSafeFps = 58,
                hitchMs = 26,
                worldRefreshInterval = 0.38f,
                visibilityRefreshInterval = 0.22f,
                strategyRefreshInterval = 0.42f,
                commandFlushInterval = 0.14f,
                heavyThinkInterval = 0.85f,
                gridRefreshInterval = 0.16f,
                cpuBudgetMs = 0.75f,
                worldScanMultiplier = 0.8f,
                maxVisibleProviders = 30,
                maxHeavyBrainsPerFrame = 2,
                maxCommandsPerFlush = 32,
                gridColumnsPerTick = 5,
                stressDecaySpeed = 1.40f,
                emergencyDropFactor = 0.35f
            };
        }

        private static AttackWeights CreateDefaultAttackWeights()
        {
            return new AttackWeights
            {
                radar = 1.8f,
                airfield = 1.5f,
                shipyard = 1.25f,
                oil = 1.7f,
                logistics = 1.2f,
                hq = 1.0f,
                antiBlindPenalty = 0.4f
            };
        }
    }
}
