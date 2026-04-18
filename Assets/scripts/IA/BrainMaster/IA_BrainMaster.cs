using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public class IA_BrainMaster : MonoBehaviour
    {
        public enum IA_BootstrapStage
        {
            Disabled,
            BuildPrefeitura,
            BuildAeroporto,
            BuildVehicleFactory,
            BuildSupportHangar,
            BuildTent,
            AnalyzeTerrain,
            ProduceGroundUnits,
            HoldGroundUnits,
            ProduceAircraft,
            BuildShipyard,
            HoldShipyard,
            ProduceShip,
            HoldShipLaunch,
            Completed
        }

        public enum IA_IntegrationMode
        {
            ShadowReadOnly,
            Hybrid,
            Full
        }

        [Header("Identity")]
        public int TeamId = 2;
        public string NationName = "BrainMaster";

        [Header("Economy")]
        public int InitialCredits = 30000;
        public int IncomePerSecond = 60;
        public int Credits;

        [Header("Stability")]
        public IA_IntegrationMode IntegrationMode = IA_IntegrationMode.Hybrid;
        public bool DisableLegacyAIWhenFull = true;
        public int MaxCommandsPerFrame = 4;
        public bool UseScriptedBootstrap = true;

        [Header("Debug")]
        public bool EnableVerboseLogs = false;
        public bool EnableBootstrapConsoleTrace = true;
        [Header("Manual Build")]
        public bool UseManualBuildPoints = true;
        [TextArea(3, 12)] public string RuntimeSummary = string.Empty;
        [TextArea(3, 12)] public string BootstrapStatus = string.Empty;
        [TextArea(2, 8)] public string BootstrapLastError = string.Empty;
        [TextArea(4, 18)] public string NavalDiagnosticSummary = string.Empty;
        [TextArea(2, 8)] public string CombatPressureSummary = string.Empty;

        public IA_BootstrapStage BootstrapStage { get; private set; }

        public IA_Context Context { get; private set; }

        private IA_CommandQueue _commandQueue;
        private IA_BackendBridge _backendBridge;
        private IA_PerformanceScheduler _scheduler;
        private IA_DebugMonitor _debugMonitor;
        private IA_WorldState _worldState;
        private IA_MapAnalyzer _mapAnalyzer;
        private IA_PlayerProfileMemory _profileMemory;
        private IA_ThreatAnalyzer _threatAnalyzer;
        private IA_SemanticMapPlanner _semanticMapPlanner;
        private IA_ZonePlanner _zonePlanner;
        private IA_LotPlanner _lotPlanner;
        private IA_UrbanBuildValidator _urbanBuildValidator;
        private IA_ConstructionPlanner _constructionPlanner;
        private IA_BuildDirector _buildDirector;
        private IA_ProductionDirector _productionDirector;
        private IA_SquadDirector _squadDirector;
        private IA_TacticalDirector _tacticalDirector;
        private IA_NavalDirector _navalDirector;
        private IA_AirDirector _airDirector;
        private IA_DefenseDirector _defenseDirector;

        private float _incomeTimer;
        private float _nextRuntimeSummaryTime;
        private readonly List<MonoBehaviour> _disabledLegacy = new List<MonoBehaviour>();
        private float _schedulerPhaseOffset;
        private bool _modulesRegistered;
        private static int _activeBrainCount;
        private float _bootstrapStartTime;
        private float _bootstrapStageStartTime;
        // Slot atribuido pelo coordenador global — determina a ordem de execucao entre IAs
        private int _coordinatorSlot = 0;
        private readonly System.Diagnostics.Stopwatch _updateWatch = new System.Diagnostics.Stopwatch();

        private enum IA_CommandLane
        {
            Tactical,
            Naval,
            Air,
            Production,
            BuildLight,
            BuildHeavy,
            Other
        }

        private void OnEnable()
        {
            _activeBrainCount++;
            _coordinatorSlot = IA_GlobalBrainCoordinator.Instance.Register(TeamId);
        }

        private void OnDisable()
        {
            _activeBrainCount = Mathf.Max(0, _activeBrainCount - 1);
            IA_GlobalBrainCoordinator.Instance.Unregister(TeamId);
        }

        private void Awake()
        {
            Credits = Mathf.Max(0, InitialCredits);
            EnsureRuntimeGraph(false, false);
        }

        private void Start()
        {
            EnsureRuntimeOperational(true);
        }

        private void Update()
        {
            TickEconomy(Time.deltaTime);
            if (!EnsureRuntimeOperational(false))
            {
                return;
            }

            ConfigureSchedulerBudget();
            if (_debugMonitor != null) _debugMonitor.VerboseLogs = EnableVerboseLogs;
            if (Context != null && _worldState != null)
            {
                Context.CombatPressure = _worldState.CombatPressure;
            }

            if (_scheduler == null)
            {
                return;
            }

            // Consulta o coordenador global para saber o budget disponivel neste frame
            IA_GlobalBrainCoordinator coordinator = IA_GlobalBrainCoordinator.Instance;
            float frameBudget = coordinator.GetBudgetForBrain(_coordinatorSlot);
            bool canRunHeavy = coordinator.CanRunHeavyModules(_coordinatorSlot);

            // Ajusta o budget do scheduler para o que o coordenador permite
            if (frameBudget > 0f)
            {
                _scheduler.GlobalFrameBudgetMs = frameBudget;
                _scheduler.HeavyModulesAllowed = canRunHeavy;

                _updateWatch.Restart();
                _scheduler.Tick(Time.time, Time.deltaTime);
                _updateWatch.Stop();

                coordinator.ReportFrameCost(_coordinatorSlot, (float)_updateWatch.Elapsed.TotalMilliseconds, canRunHeavy);
            }

            ProcessCommandQueue(Time.time);

            if (Time.unscaledTime >= _nextRuntimeSummaryTime)
            {
                RuntimeSummary = (_debugMonitor != null ? _debugMonitor.LastSummary : "monitor indisponivel")
                                 + " | Credits=" + Credits
                                 + " | Bootstrap=" + BootstrapStage
                                 + " | BootstrapStatus=" + BootstrapStatus
                                 + (string.IsNullOrEmpty(BootstrapLastError) ? string.Empty : " | BootstrapError=" + BootstrapLastError);
                NavalDiagnosticSummary = IA_NavalBuildDiagnostics.GetInspectorSummary(this);
                CombatPressureSummary = BuildCombatPressureSummary();
                _nextRuntimeSummaryTime = Time.unscaledTime + 0.6f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            IA_NavalBuildDiagnostics.DrawGizmos(this);
        }

        public bool TrySpend(int amount)
        {
            int cost = Mathf.Max(0, amount);
            if (Credits < cost)
            {
                return false;
            }

            Credits -= cost;
            return true;
        }

        public void Refund(int amount)
        {
            Credits += Mathf.Max(0, amount);
        }

        private void TickEconomy(float deltaTime)
        {
            _incomeTimer += deltaTime;
            while (_incomeTimer >= 1f)
            {
                Credits += Mathf.Max(0, IncomePerSecond);
                _incomeTimer -= 1f;
            }
        }

        private void RegisterModules()
        {
            if (_scheduler == null)
            {
                return;
            }

            float now = Time.time;
            _scheduler.Register(_worldState, now, 0.05f);
            _scheduler.Register(_mapAnalyzer, now, 0.07f);
            _scheduler.Register(_profileMemory, now, 0.09f);
            _scheduler.Register(_threatAnalyzer, now, 0.11f);
            _scheduler.Register(_semanticMapPlanner, now, 0.125f);
            _scheduler.Register(_zonePlanner, now, 0.14f);
            _scheduler.Register(_lotPlanner, now, 0.155f);
            _scheduler.Register(_constructionPlanner, now, 0.17f);
            _scheduler.Register(_buildDirector, now, 0.19f);
            _scheduler.Register(_productionDirector, now, 0.22f);
            _scheduler.Register(_squadDirector, now, 0.25f);
            _scheduler.Register(_tacticalDirector, now, 0.28f);
            _scheduler.Register(_navalDirector, now, 0.31f);
            _scheduler.Register(_airDirector, now, 0.34f);
            _scheduler.Register(_defenseDirector, now, 0.37f);
            _scheduler.Register(_debugMonitor, now, 0.45f);
            _modulesRegistered = true;
        }

        private void ProcessCommandQueue(float now)
        {
            if (IntegrationMode == IA_IntegrationMode.ShadowReadOnly)
            {
                return;
            }

            if (_commandQueue == null || _backendBridge == null || _backendBridge.CommandService == null || Context == null)
            {
                return;
            }

            int maxCommands = ResolveCommandBudget();
            IA_CombatPressure pressure = Context.CombatPressure;
            int tacticalExecuted = 0;
            int navalExecuted = 0;
            int airExecuted = 0;
            int productionExecuted = 0;
            int buildExecuted = 0;
            int executed = 0;
            while (executed < maxCommands)
            {
                IA_CommandRequest request;
                if (!_commandQueue.TryDequeue(now, out request))
                {
                    break;
                }

                if (!IsCommandAllowedInMode(request.Type))
                {
                    _commandQueue.Complete(request, false, now, "bloqueado pelo modo de integracao");
                    continue;
                }

                if (!IsCommandAllowedByBootstrap(request))
                {
                    _commandQueue.Complete(request, false, now, "bloqueado pelo bootstrap");
                    TraceCommandExecution(request, false, "bloqueado pelo bootstrap");
                    continue;
                }

                if (!TryConsumeCommandQuota(
                        request,
                        pressure,
                        ref tacticalExecuted,
                        ref navalExecuted,
                        ref airExecuted,
                        ref productionExecuted,
                        ref buildExecuted))
                {
                    _commandQueue.Complete(request, false, now, "adiado por quota de combate");
                    TraceCommandExecution(request, false, "adiado por quota de combate");
                    continue;
                }

                string message;
                bool success = _backendBridge.CommandService.Execute(request, Context, out message);
                _commandQueue.Complete(request, success, now, message);
                TraceCommandExecution(request, success, message);
                executed++;
            }
        }

        private bool IsCommandAllowedInMode(IA_CommandType type)
        {
            if (IntegrationMode == IA_IntegrationMode.Full)
            {
                return true;
            }

            if (IntegrationMode == IA_IntegrationMode.Hybrid)
            {
                return type == IA_CommandType.Build
                       || type == IA_CommandType.Produce
                       || type == IA_CommandType.Move
                       || type == IA_CommandType.Attack
                       || type == IA_CommandType.Patrol
                       || type == IA_CommandType.Ability;
            }

            return false;
        }

        private bool TryConsumeCommandQuota(
            IA_CommandRequest request,
            IA_CombatPressure pressure,
            ref int tacticalExecuted,
            ref int navalExecuted,
            ref int airExecuted,
            ref int productionExecuted,
            ref int buildExecuted)
        {
            if (request != null
                && IsBootstrapActive
                && (request.Type == IA_CommandType.Build || request.Type == IA_CommandType.Produce))
            {
                return true;
            }

            if (request == null || pressure == null)
            {
                return true;
            }

            bool queuePressured = _commandQueue != null && _commandQueue.PendingCount > 4;
            if (pressure.Estado == EstadoCargaIA.Normal && !queuePressured)
            {
                return true;
            }

            IA_CommandLane lane = ClassifyCommandLane(request);
            switch (lane)
            {
                case IA_CommandLane.BuildHeavy:
                    return false;
                case IA_CommandLane.BuildLight:
                    if (pressure.Estado == EstadoCargaIA.Saturado || queuePressured)
                    {
                        return false;
                    }

                    if (buildExecuted >= 1)
                    {
                        return false;
                    }

                    buildExecuted++;
                    return true;
                case IA_CommandLane.Production:
                    if (productionExecuted >= 1)
                    {
                        return false;
                    }

                    productionExecuted++;
                    return true;
                case IA_CommandLane.Naval:
                    if (navalExecuted >= 1)
                    {
                        return false;
                    }

                    navalExecuted++;
                    return true;
                case IA_CommandLane.Air:
                    if (airExecuted >= 1)
                    {
                        return false;
                    }

                    airExecuted++;
                    return true;
                case IA_CommandLane.Tactical:
                    if (tacticalExecuted >= 2)
                    {
                        return false;
                    }

                    tacticalExecuted++;
                    return true;
                default:
                    return true;
            }
        }

        private static IA_CommandLane ClassifyCommandLane(IA_CommandRequest request)
        {
            if (request == null)
            {
                return IA_CommandLane.Other;
            }

            if (request.Type == IA_CommandType.Produce)
            {
                return IA_CommandLane.Production;
            }

            if (request.Type == IA_CommandType.Build)
            {
                IA_BuildOrderData build = request.Payload as IA_BuildOrderData;
                string buildKey = IA_Text.Normalize(build != null ? build.ItemKey : request.DedupKey);
                return IsHeavyBuildItem(buildKey) ? IA_CommandLane.BuildHeavy : IA_CommandLane.BuildLight;
            }

            string dedup = IA_Text.Normalize(request.DedupKey);
            if (dedup.Contains("naval")
                || dedup.Contains("fleet")
                || dedup.Contains("submarine")
                || dedup.Contains("carrier")
                || dedup.Contains("amphibious"))
            {
                return IA_CommandLane.Naval;
            }

            if (dedup.Contains("air")
                || dedup.Contains("fighter")
                || dedup.Contains("helic")
                || dedup.Contains("caca"))
            {
                return IA_CommandLane.Air;
            }

            if (request.Type == IA_CommandType.Move
                || request.Type == IA_CommandType.Attack
                || request.Type == IA_CommandType.Patrol
                || request.Type == IA_CommandType.Ability)
            {
                return IA_CommandLane.Tactical;
            }

            return IA_CommandLane.Other;
        }

        private static bool IsHeavyBuildItem(string normalizedItemKey)
        {
            return !string.IsNullOrEmpty(normalizedItemKey)
                   && (normalizedItemKey.Contains("estaleiro")
                       || normalizedItemKey.Contains("pier")
                       || normalizedItemKey.Contains("plataforma"));
        }

        public bool IsBootstrapActive
        {
            get
            {
                return UseScriptedBootstrap
                       && BootstrapStage != IA_BootstrapStage.Disabled
                       && BootstrapStage != IA_BootstrapStage.Completed;
            }
        }

        public float GetBootstrapElapsed(float now)
        {
            return Mathf.Max(0f, now - _bootstrapStartTime);
        }

        public float GetBootstrapStageElapsed(float now)
        {
            return Mathf.Max(0f, now - _bootstrapStageStartTime);
        }

        public void SetBootstrapStage(IA_BootstrapStage stage, string status)
        {
            if (!UseScriptedBootstrap)
            {
                BootstrapStage = IA_BootstrapStage.Completed;
                BootstrapStatus = "bootstrap desativado";
                return;
            }

            string normalizedStatus = status ?? string.Empty;
            bool stageChanged = BootstrapStage != stage;
            bool statusChanged = BootstrapStatus != normalizedStatus;
            if (stageChanged)
            {
                BootstrapStage = stage;
                _bootstrapStageStartTime = Time.time;
            }

            BootstrapStatus = normalizedStatus;
            if (stageChanged)
            {
                TraceBootstrapStep("fase -> " + stage + (string.IsNullOrEmpty(normalizedStatus) ? string.Empty : " | " + normalizedStatus));
            }
            else if (statusChanged)
            {
                TraceBootstrapStep(normalizedStatus);
            }

            if (stage == IA_BootstrapStage.Completed)
            {
                BootstrapLastError = string.Empty;
            }
        }

        public void SetBootstrapStatus(string status)
        {
            string normalizedStatus = status ?? string.Empty;
            if (BootstrapStatus == normalizedStatus)
            {
                return;
            }

            BootstrapStatus = normalizedStatus;
            TraceBootstrapStep(normalizedStatus);
        }

        public void ReportBootstrapError(string error)
        {
            string normalizedError = error ?? string.Empty;
            if (BootstrapLastError == normalizedError)
            {
                return;
            }

            BootstrapLastError = normalizedError;
            if (!string.IsNullOrEmpty(normalizedError))
            {
                TraceBootstrapStep("erro -> " + normalizedError, true);
            }
        }

        private int ResolveCommandBudget()
        {
            int activeBrains = Mathf.Max(1, _activeBrainCount);
            int maxCommands = Mathf.Clamp(MaxCommandsPerFrame, 1, 10);
            if (IsBootstrapActive)
            {
                return 1;
            }

            if (activeBrains <= 1)
            {
                IA_CombatPressure pressure = _worldState != null ? _worldState.CombatPressure : null;
                if (pressure != null && pressure.Estado == EstadoCargaIA.Saturado)
                {
                    return Mathf.Clamp(maxCommands - 2, 1, maxCommands);
                }

                if (pressure != null && pressure.Estado == EstadoCargaIA.EmCombate)
                {
                    return Mathf.Clamp(maxCommands - 1, 1, maxCommands);
                }

                return maxCommands;
            }

            int scaled = maxCommands - Mathf.Min(3, activeBrains - 1);
            if (activeBrains >= 3)
            {
                scaled = Mathf.Min(scaled, 2);
            }

            return Mathf.Clamp(scaled, 1, maxCommands);
        }

        private void ConfigureSchedulerBudget()
        {
            if (_scheduler == null)
            {
                return;
            }

            bool bootstrapActive = IsBootstrapActive;
            _scheduler.PhaseOffsetSeconds = _schedulerPhaseOffset;

            // Delega ao coordenador global o calculo de budget e modulos por frame
            IA_GlobalBrainCoordinator coordinator = IA_GlobalBrainCoordinator.Instance;
            _scheduler.GlobalFrameBudgetMs = coordinator.ComputePerBrainBudgetMs(bootstrapActive);
            _scheduler.MaxModulesPerFrame = coordinator.ComputeMaxModulesPerFrame(bootstrapActive);
            IA_CombatPressure pressure = _worldState != null ? _worldState.CombatPressure : null;
            if (pressure != null)
            {
                if (pressure.Estado == EstadoCargaIA.Saturado)
                {
                    _scheduler.MaxModulesPerFrame = Mathf.Min(_scheduler.MaxModulesPerFrame, 3);
                }
                else if (pressure.Estado == EstadoCargaIA.EmCombate)
                {
                    _scheduler.MaxModulesPerFrame = Mathf.Min(_scheduler.MaxModulesPerFrame, 4);
                }
            }

            // Backoff: quanto mais IAs, mais espaçado cada modulo roda
            int count = Mathf.Max(1, coordinator.ActiveCount);
            _scheduler.MinBackoffSeconds = bootstrapActive
                ? Mathf.Clamp(0.06f + (count - 1) * 0.035f, 0.06f, 0.20f)
                : Mathf.Clamp(0.05f + (count - 1) * 0.025f, 0.05f, 0.15f);
        }

        private bool HasRuntimeGraph()
        {
            return _commandQueue != null
                   && _backendBridge != null
                   && _worldState != null
                   && _mapAnalyzer != null
                   && _profileMemory != null
                   && _threatAnalyzer != null
                   && _scheduler != null
                   && Context != null
                   && _semanticMapPlanner != null
                   && _zonePlanner != null
                   && _urbanBuildValidator != null
                   && _lotPlanner != null
                   && _constructionPlanner != null
                   && _squadDirector != null
                   && _buildDirector != null
                   && _productionDirector != null
                   && _tacticalDirector != null
                   && _navalDirector != null
                   && _airDirector != null
                   && _defenseDirector != null
                   && _debugMonitor != null;
        }

        private void RebuildRuntimeGraph()
        {
            _modulesRegistered = false;
            _commandQueue = new IA_CommandQueue();
            _backendBridge = new IA_BackendBridge(TeamId);
            _worldState = new IA_WorldState(TeamId);
            _worldState.SetFallbackCenter(transform.position);
            _mapAnalyzer = new IA_MapAnalyzer(_worldState);
            _profileMemory = new IA_PlayerProfileMemory(_worldState);
            _threatAnalyzer = new IA_ThreatAnalyzer(_worldState, _mapAnalyzer);
            _scheduler = new IA_PerformanceScheduler();
            _schedulerPhaseOffset = ComputeSchedulerPhaseOffset();
            _scheduler.PhaseOffsetSeconds = _schedulerPhaseOffset;
            ConfigureSchedulerBudget();

            Context = new IA_Context
            {
                Brain = this,
                WorldState = _worldState,
                MapAnalyzer = _mapAnalyzer,
                PlayerProfileMemory = _profileMemory,
                ThreatAnalyzer = _threatAnalyzer,
                CommandQueue = _commandQueue,
                Backend = _backendBridge,
                Scheduler = _scheduler,
                CombatPressure = _worldState.CombatPressure,
                ForceSnapshot = _worldState.ForceSnapshot
            };

            _semanticMapPlanner = new IA_SemanticMapPlanner(Context);
            _zonePlanner = new IA_ZonePlanner(Context);
            _urbanBuildValidator = new IA_UrbanBuildValidator(Context);
            _lotPlanner = new IA_LotPlanner(Context);
            _constructionPlanner = new IA_ConstructionPlanner(Context);
            _squadDirector = new IA_SquadDirector(Context);
            _buildDirector = new IA_BuildDirector(Context);
            _productionDirector = new IA_ProductionDirector(Context);
            _tacticalDirector = new IA_TacticalDirector(Context);
            _navalDirector = new IA_NavalDirector(Context);
            _airDirector = new IA_AirDirector(Context);
            _defenseDirector = new IA_DefenseDirector(Context);

            Context.SquadDirector = _squadDirector;
            Context.BuildDirector = _buildDirector;
            Context.SemanticMapPlanner = _semanticMapPlanner;
            Context.ZonePlanner = _zonePlanner;
            Context.LotPlanner = _lotPlanner;
            Context.UrbanBuildValidator = _urbanBuildValidator;
            Context.ConstructionPlanner = _constructionPlanner;

            _debugMonitor = new IA_DebugMonitor(this, _worldState, _commandQueue, _scheduler)
            {
                VerboseLogs = EnableVerboseLogs
            };
            Context.DebugMonitor = _debugMonitor;
        }

        private string BuildCombatPressureSummary()
        {
            IA_CombatPressure pressure = _worldState != null ? _worldState.CombatPressure : null;
            if (pressure == null)
            {
                return "combat pressure indisponivel";
            }

            return "Estado=" + pressure.Estado
                   + " | enemy=" + pressure.EnemyVisible
                   + " | naval=" + pressure.NavalUnitsActive
                   + " | air=" + pressure.AirUnitsActive
                   + " | recente=" + pressure.RecentCombatSeconds.ToString("0.0") + "s"
                   + " | misseis=" + pressure.ActiveMissiles
                   + " | projeteis=" + pressure.ActiveProjectiles
                   + (_buildDirector != null && _buildDirector.CombatNavalBuildLocked
                       ? " | navalBuildLock=" + _buildDirector.CombatNavalBuildLockReason
                       : string.Empty);
        }

        private bool EnsureRuntimeGraph(bool refreshCatalog, bool registerModules)
        {
            if (HasRuntimeGraph())
            {
                return false;
            }

            RebuildRuntimeGraph();

            if (refreshCatalog && _backendBridge != null)
            {
                _backendBridge.RefreshCatalog();
            }

            if (registerModules)
            {
                RegisterModules();
            }

            BootstrapLastError = "runtime da IA recomposto apos referencias nulas";
            RuntimeSummary = "BrainMaster recomposto em runtime.";
            return true;
        }

        private bool EnsureRuntimeOperational(bool initializeBootstrapIfNeeded)
        {
            bool rebuilt = EnsureRuntimeGraph(false, false);
            if (!HasRuntimeGraph())
            {
                BootstrapLastError = "runtime critico indisponivel";
                return false;
            }

            if (rebuilt && _backendBridge != null)
            {
                _backendBridge.RefreshCatalog();
            }

            if (!_modulesRegistered)
            {
                RegisterModules();
            }

            bool bootstrapAindaNaoInicializado = BootstrapStage == IA_BootstrapStage.Disabled
                                                 && string.IsNullOrEmpty(BootstrapStatus)
                                                 && string.IsNullOrEmpty(BootstrapLastError);

            if (initializeBootstrapIfNeeded || bootstrapAindaNaoInicializado)
            {
                InitializeBootstrap();
            }

            ApplyIntegrationPolicy();
            return true;
        }

        private float ComputeSchedulerPhaseOffset()
        {
            int seed = Mathf.Abs((TeamId * 31) + (GetInstanceID() * 17));
            return 0.03f * (seed % 11);
        }

        public void TraceBootstrapStep(string message, bool warning = false)
        {
            if (!EnableBootstrapConsoleTrace || string.IsNullOrEmpty(message))
            {
                return;
            }

            string prefix = "[IA_Bootstrap][Team " + TeamId + "][" + BootstrapStage + "][t="
                            + GetBootstrapElapsed(Time.time).ToString("0.0") + "s] ";
            if (warning)
            {
                Debug.LogWarning(prefix + message, this);
            }
            else
            {
                Debug.Log(prefix + message, this);
            }
        }

        private void TraceCommandExecution(IA_CommandRequest request, bool success, string message)
        {
            if (!EnableBootstrapConsoleTrace || request == null)
            {
                return;
            }

            if (request.Type != IA_CommandType.Build && request.Type != IA_CommandType.Produce)
            {
                return;
            }

            if (!IsBootstrapActive && !EnableVerboseLogs)
            {
                return;
            }

            string descriptor = DescribeCommand(request);
            string result = success ? "OK" : "FALHA";
            string text = "cmd " + request.Type + " -> " + descriptor + " => " + result;
            if (!string.IsNullOrEmpty(message))
            {
                text += " | " + message;
            }

            if (success)
            {
                Debug.Log("[IA_Command][Team " + TeamId + "] " + text, this);
            }
            else
            {
                Debug.LogWarning("[IA_Command][Team " + TeamId + "] " + text, this);
            }
        }

        private static string DescribeCommand(IA_CommandRequest request)
        {
            if (request == null)
            {
                return "desconhecido";
            }

            IA_BuildOrderData buildData = request.Payload as IA_BuildOrderData;
            if (buildData != null)
            {
                return buildData.ItemKey + " @ " + buildData.Position;
            }

            IA_ProduceOrderData produceData = request.Payload as IA_ProduceOrderData;
            if (produceData != null)
            {
                return produceData.ItemKey + " x" + Mathf.Max(1, produceData.Quantity);
            }

            return string.IsNullOrEmpty(request.DedupKey) ? request.Type.ToString() : request.DedupKey;
        }

        private void InitializeBootstrap()
        {
            _bootstrapStartTime = Time.time;
            _bootstrapStageStartTime = _bootstrapStartTime;

            if (!UseScriptedBootstrap)
            {
                BootstrapStage = IA_BootstrapStage.Completed;
                BootstrapStatus = "bootstrap desativado";
                BootstrapLastError = string.Empty;
                return;
            }

            BootstrapLastError = string.Empty;
            SetBootstrapStage(IA_BootstrapStage.BuildPrefeitura, "aguardando t=5s para prefeitura");
        }

        private bool IsCommandAllowedByBootstrap(IA_CommandRequest request)
        {
            if (!IsBootstrapActive || request == null)
            {
                return true;
            }

            switch (request.Type)
            {
                case IA_CommandType.Build:
                case IA_CommandType.Produce:
                    return true;
                case IA_CommandType.Move:
                case IA_CommandType.Attack:
                case IA_CommandType.Patrol:
                case IA_CommandType.Ability:
                    return false;
                default:
                    return true;
            }
        }

        private void ApplyIntegrationPolicy()
        {
            if (IntegrationMode != IA_IntegrationMode.Full || !DisableLegacyAIWhenFull)
            {
                return;
            }

            DisableLegacyComponent<IA_Suprema>();
            DisableLegacyComponent<IA_Dominadora>();
            DisableLegacyComponent<IA_Comandante>();
        }

        private void DisableLegacyComponent<T>() where T : MonoBehaviour
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null || component.gameObject == gameObject)
                {
                    continue;
                }

                component.enabled = false;
                _disabledLegacy.Add(component);
            }
        }
    }
}
