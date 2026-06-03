using System.Collections.Generic;
using Hegemonia.AI.DEUSA;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public class IA_BrainMaster : MonoBehaviour
    {
        public enum IA_BootstrapStage
        {
            Disabled = 0,
            BuildPrefeitura = 1,
            BuildAeroporto = 2,
            BuildVehicleFactory = 3,
            BuildSupportHangar = 4,
            BuildTent = 5,
            AnalyzeTerrain = 6,
            ProduceGroundUnits = 7,
            HoldGroundUnits = 8,
            ProduceAircraft = 9,
            BuildShipyard = 10,
            HoldShipyard = 11,
            BuildPier = 12,
            ProduceOilTanker = 13,
            ProduceShip = 14,
            HoldShipLaunch = 15,
            InvasionPrep = 16,
            Completed = 17,
            MobilizeBase = 18
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
        public string CurrencyName = "Moeda IA";
        public string CurrencySymbol = "IA$";
        public PerfilPaisIA NationProfile = PerfilPaisIA.Neutro;
        public ModoInicialPaisIA InitialNationMode = ModoInicialPaisIA.Crescimento;

        [Header("National Personality")]
        [Range(0f, 1f)] public float DiplomacyWeight = 0.50f;
        [Range(0f, 1f)] public float TradeWeight = 0.55f;
        [Range(0f, 1f)] public float IndustryWeight = 0.50f;
        [Range(0f, 1f)] public float MilitarismWeight = 0.45f;
        [Range(0f, 1f)] public float AggressionWeight = 0.35f;
        [Range(0f, 1f)] public float ExternalDependencyWeight = 0.45f;
        [Range(0f, 1f)] public float SelfSufficiencyWeight = 0.45f;
        [Range(0f, 1f)] public float EconomicRiskWeight = 0.35f;
        [Range(0f, 1f)] public float StockControlWeight = 0.55f;
        [Range(0f, 1f)] public float AllyLoyaltyWeight = 0.55f;
        [Range(0f, 1f)] public float RivalHatredWeight = 0.45f;

        [Header("Economy")]
        public int InitialCredits = 30000;
        public int IncomePerSecond = 60;
        public int Credits;

        [Header("Stability")]
        public IA_IntegrationMode IntegrationMode = IA_IntegrationMode.Hybrid;
        public bool DisableLegacyAIWhenFull = true;
        public int MaxCommandsPerFrame = 4;
        public bool UseScriptedBootstrap = true;
        [Tooltip("Tempo minimo em segundos que a IA usa para estruturar base e produzir tropas sem atacar.")]
        public float BootstrapMobilizationSeconds = 300f;

        [Header("Debug")]
        public bool EnableVerboseLogs = false;
        public bool EnableBootstrapConsoleTrace = false;
        [Header("Manual Build")]
        public bool UseManualBuildPoints = true;
        [TextArea(3, 12)] public string RuntimeSummary = string.Empty;
        [TextArea(3, 12)] public string BootstrapStatus = string.Empty;
        [TextArea(2, 8)] public string BootstrapLastError = string.Empty;
        [TextArea(4, 18)] public string NavalDiagnosticSummary = string.Empty;
        [TextArea(2, 8)] public string CombatPressureSummary = string.Empty;

        [Header("Imperial AI")]
        public IA_StrategicPhase StrategicPhase = IA_StrategicPhase.Abertura;
        public string ActiveImperialPlan = "abertura";
        public string ImperialLastFailure = string.Empty;
        public int TargetFleet = 4;
        public int TargetAircraft = 6;
        public int TargetOilTankers = 2;
        public int TargetPlatforms = 2;
        public int TargetPiers = 2;
        public int TargetShipyards = 1;
        public int TargetCoastalDefenseShips = 3;
        public int TargetRadars = 1;
        public int TargetCiws = 1;
        public int PlayerFleetEstimate;
        public int PlayerAircraftEstimate;
        public bool WeakEmpireRecoveryActive;
        public string ActiveStrategicTarget = string.Empty;
        [TextArea(3, 10)] public string ImperialPlanSummary = string.Empty;

        public IA_BootstrapStage BootstrapStage { get; private set; }

        public IA_Context Context { get; private set; }
        public IA_DeusaPoliticaNacional PoliticaDeusaAtual
        {
            get { return _deusaBrain != null ? _deusaBrain.PoliticaNacional : null; }
        }

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
        private IA_NationalDecisionState _nationalDecisionState;
        private IA_GrandStrategy _grandStrategy;
        private IA_EconomyDirector _economyDirector;
        private IA_LawDirector _lawDirector;
        private IA_DiplomacyDirector _diplomacyDirector;
        private IA_MarketDirector _marketDirector;
        private IA_LogisticsDirector _logisticsDirector;
        private IA_WarDirector _warDirector;
        private IA_SyncNetwork _syncNetwork;
        private IA_BuildDirector _buildDirector;
        private IA_ProductionDirector _productionDirector;
        private IA_SquadDirector _squadDirector;
        private IA_TacticalDirector _tacticalDirector;
        private IA_NavalDirector _navalDirector;
        private IA_AirDirector _airDirector;
        private IA_DefenseDirector _defenseDirector;
        private IA_TaskForceCoordinator _taskForceCoordinator;
        private IA_DeusaBrain _deusaBrain;
        private readonly List<IdentidadeUnidade> _backendUnitBuffer = new List<IdentidadeUnidade>(128);

        private float _incomeTimer;
        private float _nextRuntimeSummaryTime;
        private readonly List<MonoBehaviour> _disabledLegacy = new List<MonoBehaviour>();
        private bool _legacyPolicyApplied;
        private int _legacyPolicyAppliedTeamId = -1;
        private IA_IntegrationMode _legacyPolicyAppliedMode = IA_IntegrationMode.ShadowReadOnly;
        private float _nextLegacyPolicyScanUnscaledTime = -1f;
        private float _schedulerPhaseOffset;
        private bool _modulesRegistered;
        private static int _activeBrainCount;
        private float _bootstrapStartTime;
        private float _bootstrapStageStartTime;
        private float _nextImperialPlanUpdateTime;
        private bool _imperialReport10;
        private bool _imperialReport20;
        private bool _imperialReport30;
        // Slot atribuido pelo coordenador global — determina a ordem de execucao entre IAs
        private int _coordinatorSlot = 0;
        private readonly System.Diagnostics.Stopwatch _updateWatch = new System.Diagnostics.Stopwatch();
        private float _nextObserverQueueLogTime = -1f;

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

            IA_GlobalBrainCoordinator coordinator = IA_GlobalBrainCoordinator.Instance;
            if (Context != null && _worldState != null)
            {
                SyncNationStateWithGovernment();
                Context.CombatPressure = _worldState.CombatPressure;
                Context.ForceSnapshot = _worldState.ForceSnapshot;
                Context.PerformanceGovernorState = coordinator.GetGovernorStateSnapshot();
                Context.BattleDecision = coordinator.BuildBattleDecision();
                Context.EngagementBudget = coordinator.BuildEngagementBudget();
                Context.TransportPlan = Context.TransportPlan ?? new IA_TransportPlan();
            }

            UpdateImperialPlanState();

            ConfigureSchedulerBudget();
            if (_debugMonitor != null) _debugMonitor.VerboseLogs = EnableVerboseLogs;

            if (_scheduler == null)
            {
                return;
            }

            // Consulta o coordenador global para saber o budget disponivel neste frame
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
                                 + " | Dificuldade=" + GameDifficultyManager.PerfilAtual.Codigo
                                 + BuildNationalSummary()
                                 + (_deusaBrain != null
                                     ? " | DEUSA=" + _deusaBrain.EstagioAtual + (_deusaBrain.ModoObservadorAtivo ? "(Obs)" : string.Empty)
                                     : string.Empty)
                                 + (_deusaBrain != null && _deusaBrain.ModoObservadorAtivo ? " | ObsScope=" + _deusaBrain.EscopoObservador : string.Empty)
                                 + " | Imperial=" + StrategicPhase + " " + ActiveImperialPlan
                                 + " | Bootstrap=" + BootstrapStage
                                 + " | BootstrapStatus=" + BootstrapStatus
                                 + " | Governor=" + (Context != null && Context.PerformanceGovernorState != null
                                     ? Context.PerformanceGovernorState.Band.ToString()
                                     : "n/d")
                                 + (string.IsNullOrEmpty(BootstrapLastError) ? string.Empty : " | BootstrapError=" + BootstrapLastError);
                NavalDiagnosticSummary = IA_NavalBuildDiagnostics.GetInspectorSummary(this);
                CombatPressureSummary = BuildCombatPressureSummary();
                _nextRuntimeSummaryTime = Time.unscaledTime + 0.6f;
            }
        }

        private void UpdateImperialPlanState()
        {
            if (Context == null || _worldState == null || Time.time < _nextImperialPlanUpdateTime)
            {
                return;
            }

            _nextImperialPlanUpdateTime = Time.time + 4f;
            IA_ForceSnapshot snapshot = Context.ForceSnapshot ?? _worldState.ForceSnapshot;
            if (snapshot == null)
            {
                return;
            }

            CountPlayerForces(out PlayerFleetEstimate, out PlayerAircraftEstimate);

            float elapsed = Time.timeSinceLevelLoad;
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            int basePlatforms = elapsed >= 1800f ? 3 : (elapsed >= 900f ? 2 : 1);
            int baseTankers = elapsed >= 1800f ? 3 : (elapsed >= 900f ? 2 : 1);
            int baseCoastalDefense = elapsed >= 900f ? 3 : 2;
            TargetShipyards = perfil.Dificuldade == DificuldadeJogo.Imperial && elapsed >= 1800f ? 2 : 1;
            TargetPiers = perfil.Dificuldade == DificuldadeJogo.Imperial && elapsed >= 1800f ? 2 : 1;
            TargetPlatforms = perfil.AjustarMeta(basePlatforms, 1);
            TargetOilTankers = perfil.AjustarMeta(baseTankers, 1);
            TargetCoastalDefenseShips = perfil.AjustarMeta(baseCoastalDefense, 1);
            TargetRadars = perfil.AjustarMeta(1, 1);
            TargetCiws = elapsed >= 900f ? perfil.AjustarMeta(1, 0) : 0;

            int baseFleet = elapsed < 300f ? 4 : (elapsed < 600f ? 10 : (elapsed < 1200f ? 18 : 30));
            int baseAir = elapsed < 300f ? 4 : (elapsed < 600f ? 10 : (elapsed < 1200f ? 16 : 24));
            int metaFrotaPorJogador = perfil.AjustarMetaContraJogador(PlayerFleetEstimate, 1.22f, TargetCoastalDefenseShips + 1);
            int metaArPorJogador = perfil.AjustarMetaContraJogador(PlayerAircraftEstimate, 1.28f, 2);
            TargetFleet = Mathf.Max(perfil.AjustarMeta(baseFleet, 1), metaFrotaPorJogador, TargetCoastalDefenseShips + 2);
            TargetAircraft = Mathf.Max(perfil.AjustarMeta(baseAir, 1), metaArPorJogador);

            bool oilGap = snapshot.PlatformCount < TargetPlatforms
                          || snapshot.PierCount < TargetPiers
                          || snapshot.ShipyardCount < TargetShipyards
                          || snapshot.OilTankers < TargetOilTankers;
            bool defenseGap = snapshot.NavalUnits < TargetCoastalDefenseShips
                              || snapshot.FixedWingAircraft < 2
                              || snapshot.RadarCount < TargetRadars
                              || (TargetCiws > 0 && CountOwnByHintFast("ciws", "phalanx", "antia") < TargetCiws);
            bool forceGap = snapshot.NavalUnits < TargetFleet || snapshot.FixedWingAircraft < TargetAircraft;
            WeakEmpireRecoveryActive = elapsed >= 900f
                                       && (snapshot.PlatformCount < 1
                                           || snapshot.PierCount < 1
                                           || snapshot.OilTankers < 1
                                           || snapshot.NavalUnits < Mathf.Min(6, TargetFleet)
                                           || snapshot.FixedWingAircraft < Mathf.Min(6, TargetAircraft)
                                           || snapshot.RadarCount < TargetRadars
                                           || (TargetCiws > 0 && CountOwnByHintFast("ciws", "phalanx", "antia") < TargetCiws));

            if (elapsed < 180f)
            {
                StrategicPhase = IA_StrategicPhase.Abertura;
                ActiveImperialPlan = "abrir base e cadeia militar";
            }
            else if (oilGap)
            {
                StrategicPhase = IA_StrategicPhase.LogisticaPetroleo;
                ActiveImperialPlan = "fechar plataforma-pier-petroleiro";
            }
            else if (defenseGap)
            {
                StrategicPhase = IA_StrategicPhase.DefesaCosteira;
                ActiveImperialPlan = "patrulha costeira e cobertura aerea";
            }
            else if (elapsed < 900f || forceGap)
            {
                StrategicPhase = IA_StrategicPhase.Expansao;
                ActiveImperialPlan = "crescer acima do jogador";
            }
            else if (elapsed < 1800f)
            {
                StrategicPhase = IA_StrategicPhase.PressaoEconomica;
                ActiveImperialPlan = "raides contra petroleo, pier e aeroporto";
            }
            else
            {
                StrategicPhase = IA_StrategicPhase.Dominacao;
                ActiveImperialPlan = "enfraquecer economia e finalizar prefeitura";
            }

            if (WeakEmpireRecoveryActive)
            {
                ActiveImperialPlan = "recuperacao imperial: " + ActiveImperialPlan;
            }

            ImperialPlanSummary = "fase=" + StrategicPhase
                                  + " | dificuldade=" + perfil.Codigo
                                  + " | plano=" + ActiveImperialPlan
                                  + " | alvo estrategico=" + (string.IsNullOrWhiteSpace(ActiveStrategicTarget) ? "n/d" : ActiveStrategicTarget)
                                  + " | alvos frota/ar/petroleiro/plataforma/radar/ciws="
                                  + TargetFleet + "/" + TargetAircraft + "/" + TargetOilTankers + "/" + TargetPlatforms + "/" + TargetRadars + "/" + TargetCiws
                                  + " | atual frota/ar/petroleiro/plataforma/radar="
                                  + snapshot.NavalUnits + "/" + snapshot.FixedWingAircraft + "/" + snapshot.OilTankers + "/" + snapshot.PlatformCount + "/" + snapshot.RadarCount
                                  + " | jogador frota/ar=" + PlayerFleetEstimate + "/" + PlayerAircraftEstimate;

            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_fase_" + TeamId, StrategicPhase.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_dificuldade_" + TeamId, perfil.Codigo);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_metas_" + TeamId, ImperialPlanSummary);
            RegistrarRelatorioImperialPorTempo(elapsed, snapshot);
        }

        private void CountPlayerForces(out int naval, out int aircraft)
        {
            naval = 0;
            aircraft = 0;
            _backendUnitBuffer.Clear();
            RegistroEntidadesJogo.FillUnidades(_backendUnitBuffer);

            for (int i = 0; i < _backendUnitBuffer.Count; i++)
            {
                IdentidadeUnidade unidade = _backendUnitBuffer[i];
                if (unidade == null || unidade.teamID != 1 || !unidade.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (unidade.tipoUnidade == TipoUnidade.Naval)
                {
                    string nome = IA_Text.Normalize(unidade.name);
                    if (!nome.Contains("petroleiro") && !nome.Contains("petrolifero") && !nome.Contains("tanker"))
                    {
                        naval++;
                    }
                }
                else if (unidade.tipoUnidade == TipoUnidade.Aereo)
                {
                    aircraft++;
                }
            }
        }

        private int CountOwnByHintFast(params string[] hints)
        {
            return _worldState != null ? _worldState.CountOwnByHint(hints) : 0;
        }

        public void ReportStrategicTarget(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            ActiveStrategicTarget = summary;
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_alvo_estrategico_" + TeamId, ActiveStrategicTarget);
        }

        public void DefinirDiretrizSuprema(string ordemLLM, int alvoTeamId)
        {
            if (ordemLLM == "invasao_anfibia_combinada")
            {
                StrategicPhase = IA_StrategicPhase.Dominacao;
                ActiveImperialPlan = "invasao_anfibia_combinada";
                ReportStrategicTarget("Força-Tarefa Combinada contra Nação " + alvoTeamId);
                
                // Força a produção intensa de navios e helicópteros
                TargetFleet = Mathf.Max(TargetFleet, 10);
                TargetAircraft = Mathf.Max(TargetAircraft, 10);
                
                if (_taskForceCoordinator != null)
                {
                    _taskForceCoordinator.SetInvasionTarget(alvoTeamId);
                }
            }
        }

        private void RegistrarRelatorioImperialPorTempo(float elapsed, IA_ForceSnapshot snapshot)
        {
            if (!_imperialReport10 && elapsed >= 600f)
            {
                _imperialReport10 = true;
                RegistrarMarcoImperial("10min", snapshot);
            }

            if (!_imperialReport20 && elapsed >= 1200f)
            {
                _imperialReport20 = true;
                RegistrarMarcoImperial("20min", snapshot);
            }

            if (!_imperialReport30 && elapsed >= 1800f)
            {
                _imperialReport30 = true;
                RegistrarMarcoImperial("30min", snapshot);
            }
        }

        private void RegistrarMarcoImperial(string marco, IA_ForceSnapshot snapshot)
        {
            string resumo = marco
                            + " team=" + TeamId
                            + " dificuldade=" + GameDifficultyManager.PerfilAtual.Codigo
                            + " fase=" + StrategicPhase
                            + " frota=" + snapshot.NavalUnits + "/" + TargetFleet
                            + " ar=" + snapshot.FixedWingAircraft + "/" + TargetAircraft
                            + " petroleiros=" + snapshot.OilTankers + "/" + TargetOilTankers
                            + " plataformas=" + snapshot.PlatformCount + "/" + TargetPlatforms
                            + " piers=" + snapshot.PierCount + "/" + TargetPiers
                            + " radar=" + snapshot.RadarCount + "/" + TargetRadars
                            + " ciws=" + CountOwnByHintFast("ciws", "phalanx", "antia") + "/" + TargetCiws
                            + " recuperacao=" + WeakEmpireRecoveryActive;
            DiagnosticoDesempenhoJogo.RegistrarEvento("IA_Imperial", resumo);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_relatorio_" + marco + "_" + TeamId, resumo);
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

        // Ponte de compatibilidade usada pelo IA_CommandExecutor (NovaIA).
        public bool TryQueueBuild(int teamId, string itemKey, Vector3 worldPoint, int priority)
        {
            if (teamId != TeamId || !EnsureRuntimeOperational(false) || _commandQueue == null)
            {
                return false;
            }

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Build,
                Priority = Mathf.Clamp(priority, 1, 1000),
                DedupKey = "compat:build:" + IA_Text.Normalize(itemKey),
                CooldownSeconds = 0.45f,
                Payload = new IA_BuildOrderData
                {
                    ItemKey = itemKey,
                    Position = ResolveBackendPoint(worldPoint),
                    Rotation = Quaternion.identity,
                    Zone = ResolveBuildZone(itemKey),
                    ForceManualPlacement = false,
                    ManualPointLabel = string.Empty
                }
            };

            string reason;
            return _commandQueue.Enqueue(request, Time.time, out reason);
        }

        public bool QueueBuild(string itemKey, Vector3 worldPoint, int priority)
        {
            return TryQueueBuild(TeamId, itemKey, worldPoint, priority);
        }

        public bool IA_QueueBuild(string itemKey, Vector3 worldPoint, int priority)
        {
            return TryQueueBuild(TeamId, itemKey, worldPoint, priority);
        }

        public bool TryQueueProduction(int teamId, string itemKey, int priority)
        {
            if (teamId != TeamId || !EnsureRuntimeOperational(false) || _commandQueue == null)
            {
                return false;
            }

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Produce,
                Priority = Mathf.Clamp(priority, 1, 1000),
                DedupKey = "compat:produce:" + IA_Text.Normalize(itemKey),
                CooldownSeconds = 0.35f,
                Payload = new IA_ProduceOrderData
                {
                    ItemKey = itemKey,
                    Quantity = 1
                }
            };

            string reason;
            return _commandQueue.Enqueue(request, Time.time, out reason);
        }

        public bool QueueProduction(string itemKey, int priority)
        {
            return TryQueueProduction(TeamId, itemKey, priority);
        }

        public bool IA_QueueProduction(string itemKey, int priority)
        {
            return TryQueueProduction(TeamId, itemKey, priority);
        }

        public bool TryIssueMovePackage(int teamId, string packageTag, Vector3 worldPoint, int priority)
        {
            if (teamId != TeamId || !EnsureRuntimeOperational(false) || _commandQueue == null)
            {
                return false;
            }

            IA_MoveOrderData move = new IA_MoveOrderData
            {
                Destination = ResolveBackendPoint(worldPoint)
            };
            FillTeamUnitsForBackend(move.Units);
            if (move.Units.Count == 0)
            {
                return false;
            }

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Move,
                Priority = Mathf.Clamp(priority, 1, 1000),
                DedupKey = "compat:move:" + IA_Text.Normalize(packageTag),
                CooldownSeconds = 0.2f,
                Payload = move
            };

            string reason;
            return _commandQueue.Enqueue(request, Time.time, out reason);
        }

        public bool IssueMovePackage(string packageTag, Vector3 worldPoint, int priority)
        {
            return TryIssueMovePackage(TeamId, packageTag, worldPoint, priority);
        }

        public bool IA_IssueMovePackage(string packageTag, Vector3 worldPoint, int priority)
        {
            return TryIssueMovePackage(TeamId, packageTag, worldPoint, priority);
        }

        public bool TryIssueAttack(int teamId, string attackTag, Vector3 worldPoint, int priority)
        {
            if (teamId != TeamId || !EnsureRuntimeOperational(false) || _commandQueue == null)
            {
                return false;
            }

            IA_AttackOrderData attack = new IA_AttackOrderData
            {
                TargetPosition = ResolveBackendPoint(worldPoint),
                Target = null
            };
            FillTeamUnitsForBackend(attack.Units);
            if (attack.Units.Count == 0)
            {
                return false;
            }

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Attack,
                Priority = Mathf.Clamp(priority, 1, 1000),
                DedupKey = "compat:attack:" + IA_Text.Normalize(attackTag),
                CooldownSeconds = 0.2f,
                Payload = attack
            };

            string reason;
            return _commandQueue.Enqueue(request, Time.time, out reason);
        }

        public bool IssueAttack(string attackTag, Vector3 worldPoint, int priority)
        {
            return TryIssueAttack(TeamId, attackTag, worldPoint, priority);
        }

        public bool IA_IssueAttack(string attackTag, Vector3 worldPoint, int priority)
        {
            return TryIssueAttack(TeamId, attackTag, worldPoint, priority);
        }

        public bool TryIssueUnload(int teamId, string tag, Vector3 worldPoint, int priority)
        {
            // No pipeline atual, unload usa a mesma trilha de deslocamento tático.
            return TryIssueMovePackage(teamId, tag, worldPoint, priority);
        }

        public bool IssueUnload(string tag, Vector3 worldPoint, int priority)
        {
            return TryIssueUnload(TeamId, tag, worldPoint, priority);
        }

        public bool IA_IssueUnload(string tag, Vector3 worldPoint, int priority)
        {
            return TryIssueUnload(TeamId, tag, worldPoint, priority);
        }

        private void TickEconomy(float deltaTime)
        {
            _incomeTimer += deltaTime;
            while (_incomeTimer >= 1f)
            {
                Credits += Mathf.Max(0, Mathf.RoundToInt(IncomePerSecond * GameDifficultyManager.PerfilAtual.MultiplicadorEconomiaIA));
                _incomeTimer -= 1f;
            }
        }

        private IA_ZoneType ResolveBuildZone(string itemKey)
        {
            string key = IA_Text.Normalize(itemKey);
            if (key.Contains("estaleiro") || key.Contains("pier") || key.Contains("plataforma"))
            {
                return IA_ZoneType.Naval;
            }
            if (key.Contains("aeroporto") || key.Contains("heliporto"))
            {
                return IA_ZoneType.Air;
            }
            if (key.Contains("torreta") || key.Contains("radar") || key.Contains("ciws") || key.Contains("muro") || key.Contains("missil") || key.Contains("ares") || key.Contains("antiaereo"))
            {
                return IA_ZoneType.Defense;
            }
            if (key.Contains("prefeitura") || key.Contains("quartel general") || key.Contains("quartel_general") || key == "hq")
            {
                return IA_ZoneType.Core;
            }
            if (key.Contains("armazem"))
            {
                return IA_ZoneType.Economy;
            }
            if (key.Contains("fabrica") || key.Contains("quartel") || key.Contains("tenda"))
            {
                return IA_ZoneType.Military;
            }

            return IA_ZoneType.Core;
        }

        private Vector3 ResolveBackendPoint(Vector3 candidate)
        {
            if (candidate.sqrMagnitude > 1f)
            {
                return candidate;
            }

            if (_worldState != null)
            {
                Vector3 center = _worldState.BaseCenter;
                if (center.sqrMagnitude > 1f)
                {
                    return center;
                }
            }

            return transform.position;
        }

        private void FillTeamUnitsForBackend(List<GameObject> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            RegistroEntidadesJogo.FillUnidades(_backendUnitBuffer);
            for (int i = 0; i < _backendUnitBuffer.Count; i++)
            {
                IdentidadeUnidade unit = _backendUnitBuffer[i];
                if (unit == null || unit.teamID != TeamId || unit.tipoUnidade == TipoUnidade.Estrutura)
                {
                    continue;
                }

                destination.Add(unit.gameObject);
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
            _scheduler.Register(_grandStrategy, now, 0.18f);
            if (_deusaBrain != null)
            {
                _scheduler.Register(_deusaBrain, now, 0.182f);
            }
            _scheduler.Register(_economyDirector, now, 0.185f);
            _scheduler.Register(_syncNetwork, now, 0.20f);
            _scheduler.Register(_marketDirector, now, 0.205f);
            _scheduler.Register(_diplomacyDirector, now, 0.215f);
            _scheduler.Register(_lawDirector, now, 0.225f);
            _scheduler.Register(_logisticsDirector, now, 0.235f);
            _scheduler.Register(_warDirector, now, 0.245f);
            _scheduler.Register(_buildDirector, now, 0.19f);
            _scheduler.Register(_productionDirector, now, 0.22f);
            _scheduler.Register(_squadDirector, now, 0.25f);
            _scheduler.Register(_tacticalDirector, now, 0.28f);
            _scheduler.Register(_navalDirector, now, 0.31f);
            _scheduler.Register(_airDirector, now, 0.34f);
            _scheduler.Register(_defenseDirector, now, 0.37f);
            
            _taskForceCoordinator = new IA_TaskForceCoordinator(Context);
            _scheduler.Register(_taskForceCoordinator, now, 0.40f);
            
            _scheduler.Register(_debugMonitor, now, 0.45f);
            _modulesRegistered = true;
        }

        private void ProcessCommandQueue(float now)
        {
            if (ShouldBlockCommandQueueByDeusaObserver())
            {
                DrainCommandQueueBlockedByDeusa(now);
                return;
            }

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

        private bool ShouldBlockCommandQueueByDeusaObserver()
        {
            return _deusaBrain != null && _deusaBrain.BloquearFilaBrainMasterEmObservador;
        }

        private void DrainCommandQueueBlockedByDeusa(float now)
        {
            if (_commandQueue == null)
            {
                return;
            }

            int maxCommands = Mathf.Max(1, ResolveCommandBudget());
            int blocked = 0;
            while (blocked < maxCommands)
            {
                IA_CommandRequest request;
                if (!_commandQueue.TryDequeue(now, out request))
                {
                    break;
                }

                const string reason = "bloqueado pelo modo observador da DEUSA";
                _commandQueue.Complete(request, false, now, reason);
                TraceCommandExecution(request, false, reason);
                blocked++;
            }

            if (blocked > 0 || Time.unscaledTime >= _nextObserverQueueLogTime)
            {
                Debug.Log(
                    "[DEUSA][Team " + TeamId + "] modo observador bloqueou "
                    + blocked + " comando(s) da fila do BrainMaster"
                    + (_deusaBrain != null ? " | escopo=" + _deusaBrain.EscopoObservador : string.Empty),
                    this);
                _nextObserverQueueLogTime = Time.unscaledTime + 5f;
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

            IA_BattleGovernorDecision decision = Context != null ? Context.BattleDecision : null;
            bool queuePressured = _commandQueue != null && _commandQueue.PendingCount > 4;
            if ((decision == null || decision.Band == IA_PerformanceGovernorBand.Saudavel)
                && pressure.Estado == EstadoCargaIA.Normal
                && !queuePressured)
            {
                return true;
            }

            IA_CommandLane lane = ClassifyCommandLane(request);
            switch (lane)
            {
                case IA_CommandLane.BuildHeavy:
                    return decision == null || decision.AllowHeavyBuild;
                case IA_CommandLane.BuildLight:
                    if ((decision != null && !decision.AllowBuild)
                        || pressure.Estado == EstadoCargaIA.Saturado
                        || queuePressured)
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
                    if (decision != null && !decision.AllowProduce)
                    {
                        return false;
                    }

                    int maxProduction = decision != null
                        ? Mathf.Clamp(decision.MaxProductionCommandsPerCycle, 1, 2)
                        : 1;
                    if (productionExecuted >= maxProduction)
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
                    int tacticalLimit = decision != null && decision.Band == IA_PerformanceGovernorBand.Critico ? 1 : 2;
                    if (tacticalExecuted >= tacticalLimit)
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

        public float GetBootstrapMobilizationSeconds()
        {
            return Mathf.Clamp(BootstrapMobilizationSeconds, 60f, 600f);
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
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            int maxCommands = perfil.AjustarComandos(Mathf.Clamp(MaxCommandsPerFrame, 1, 10));
            IA_BattleGovernorDecision decision = Context != null ? Context.BattleDecision : null;
            if (IsBootstrapActive)
            {
                return Mathf.Clamp(perfil.AjustarComandos(2), 2, 3);
            }

            if (decision != null)
            {
                switch (decision.Band)
                {
                    case IA_PerformanceGovernorBand.Critico:
                        maxCommands = Mathf.Min(maxCommands, 1);
                        break;
                    case IA_PerformanceGovernorBand.Pressao:
                        maxCommands = Mathf.Min(maxCommands, 2);
                        break;
                }
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
            IA_BattleGovernorDecision decision = Context != null ? Context.BattleDecision : null;
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

            if (decision != null)
            {
                if (decision.Band == IA_PerformanceGovernorBand.Critico)
                {
                    _scheduler.MaxModulesPerFrame = Mathf.Min(_scheduler.MaxModulesPerFrame, 2);
                }
                else if (decision.Band == IA_PerformanceGovernorBand.Pressao)
                {
                    _scheduler.MaxModulesPerFrame = Mathf.Min(_scheduler.MaxModulesPerFrame, 3);
                }
            }

            // Backoff: quanto mais IAs, mais espaçado cada modulo roda
            int count = Mathf.Max(1, coordinator.ActiveCount);
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            float multiplicadorBackoff = perfil.Dificuldade == DificuldadeJogo.Facil
                ? 1.25f
                : (perfil.Dificuldade == DificuldadeJogo.Imperial ? 0.85f : 1f);
            float backoffBase = bootstrapActive
                ? Mathf.Clamp(0.06f + (count - 1) * 0.035f, 0.06f, 0.20f)
                : Mathf.Clamp(0.05f + (count - 1) * 0.025f, 0.05f, 0.15f);
            _scheduler.MinBackoffSeconds = Mathf.Clamp(backoffBase * multiplicadorBackoff, 0.04f, 0.25f);
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
                ForceSnapshot = _worldState.ForceSnapshot,
                PerformanceGovernorState = IA_GlobalBrainCoordinator.Instance.GetGovernorStateSnapshot(),
                EngagementBudget = IA_GlobalBrainCoordinator.Instance.BuildEngagementBudget(),
                TransportPlan = new IA_TransportPlan(),
                BattleDecision = IA_GlobalBrainCoordinator.Instance.BuildBattleDecision()
            };

            _deusaBrain = GetComponent<IA_DeusaBrain>();
            if (_deusaBrain == null)
            {
                _deusaBrain = gameObject.AddComponent<IA_DeusaBrain>();
            }

            Context.Deusa = _deusaBrain;

            _semanticMapPlanner = new IA_SemanticMapPlanner(Context);
            _zonePlanner = new IA_ZonePlanner(Context);
            _urbanBuildValidator = new IA_UrbanBuildValidator(Context);
            _lotPlanner = new IA_LotPlanner(Context);
            _constructionPlanner = new IA_ConstructionPlanner(Context);
            _nationalDecisionState = new IA_NationalDecisionState();
            _grandStrategy = new IA_GrandStrategy(Context, _nationalDecisionState);
            _economyDirector = new IA_EconomyDirector(Context, _nationalDecisionState);
            _lawDirector = new IA_LawDirector(Context);
            _diplomacyDirector = new IA_DiplomacyDirector(Context, _nationalDecisionState);
            _marketDirector = new IA_MarketDirector(Context, _nationalDecisionState);
            _logisticsDirector = new IA_LogisticsDirector(Context);
            _warDirector = new IA_WarDirector(Context, _nationalDecisionState);
            _syncNetwork = new IA_SyncNetwork(Context, _nationalDecisionState);
            _squadDirector = new IA_SquadDirector(Context);
            _buildDirector = new IA_BuildDirector(Context);
            _productionDirector = new IA_ProductionDirector(Context);
            _tacticalDirector = new IA_TacticalDirector(Context);
            _navalDirector = new IA_NavalDirector(Context);
            _airDirector = new IA_AirDirector(Context);
            _defenseDirector = new IA_DefenseDirector(Context);

            Context.SquadDirector = _squadDirector;
            Context.BuildDirector = _buildDirector;
            Context.NationalDecisionState = _nationalDecisionState;
            Context.GrandStrategy = _grandStrategy;
            Context.EconomyDirector = _economyDirector;
            Context.LawDirector = _lawDirector;
            Context.DiplomacyDirector = _diplomacyDirector;
            Context.MarketDirector = _marketDirector;
            Context.LogisticsDirector = _logisticsDirector;
            Context.WarDirector = _warDirector;
            Context.SyncNetwork = _syncNetwork;
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
            if (_deusaBrain != null)
            {
                _deusaBrain.BindRuntime(this, Context);
            }

            SyncNationStateWithGovernment();
        }

        private void SyncNationStateWithGovernment()
        {
            SistemaGovernoMundial.GarantirInstancia();
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            gov.GarantirPaisIA(TeamId, NationName, CurrencyName, CurrencySymbol, NationProfile, InitialNationMode);
            DadosPaisGoverno pais = gov.ObterPais(TeamId);
            if (pais == null)
            {
                return;
            }

            pais.pesoDiplomacia = DiplomacyWeight;
            pais.pesoComercio = TradeWeight;
            pais.pesoIndustria = IndustryWeight;
            pais.pesoMilitarismo = MilitarismWeight;
            pais.pesoAgressividade = AggressionWeight;
            pais.pesoDependenciaExterna = ExternalDependencyWeight;
            pais.pesoAutossuficiencia = SelfSufficiencyWeight;
            pais.pesoRiscoEconomico = EconomicRiskWeight;
            pais.pesoControleEstoque = StockControlWeight;
            pais.pesoLealdadeAliados = AllyLoyaltyWeight;
            pais.pesoOdioRivais = RivalHatredWeight;
        }

        private string BuildNationalSummary()
        {
            if (_nationalDecisionState == null)
            {
                return string.Empty;
            }

            return " | PlanoPais=" + _nationalDecisionState.StrategicPlan
                   + " | Need=" + _nationalDecisionState.CriticalNeed
                   + " | Surplus=" + _nationalDecisionState.BestSurplus
                   + " | QV=" + _nationalDecisionState.QualityOfLife.ToString("0")
                   + " | DeficitEco=" + _nationalDecisionState.MainEconomicDeficit
                   + " | ProdEco=" + _nationalDecisionState.MainProduction
                   + " | PressPop=" + _nationalDecisionState.PopulationPressure.ToString("0.00")
                   + " | Propostas=" + _nationalDecisionState.PendingProposals
                   + " | Ofertas=" + _nationalDecisionState.ActiveOffers
                   + " | BloqMercado=" + _nationalDecisionState.BlockedDecisions;
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
                   + " | governor=" + (Context != null && Context.PerformanceGovernorState != null
                       ? Context.PerformanceGovernorState.Band.ToString()
                       : "n/d")
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
                _legacyPolicyApplied = false;
                _legacyPolicyAppliedTeamId = -1;
                _legacyPolicyAppliedMode = IntegrationMode;
                _nextLegacyPolicyScanUnscaledTime = -1f;
                return;
            }

            bool needsScan = !_legacyPolicyApplied
                             || _legacyPolicyAppliedTeamId != TeamId
                             || _legacyPolicyAppliedMode != IntegrationMode
                             || Time.unscaledTime >= _nextLegacyPolicyScanUnscaledTime;

            if (!needsScan)
            {
                return;
            }

            DisableLegacyComponent<IA_Suprema>();
            DisableLegacyComponent<IA_Dominadora>();
            DisableLegacyComponent<IA_Comandante>();

            _legacyPolicyApplied = true;
            _legacyPolicyAppliedTeamId = TeamId;
            _legacyPolicyAppliedMode = IntegrationMode;
            _nextLegacyPolicyScanUnscaledTime = Time.unscaledTime + 10f;
        }

        private void DisableLegacyComponent<T>() where T : MonoBehaviour
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null
                    || component.gameObject == gameObject
                    || !ComponentBelongsToSameTeam(component))
                {
                    continue;
                }

                if (!component.enabled)
                {
                    continue;
                }

                component.enabled = false;
                if (!_disabledLegacy.Contains(component))
                {
                    _disabledLegacy.Add(component);
                }
            }
        }

        private bool ComponentBelongsToSameTeam(MonoBehaviour component)
        {
            if (component == null)
            {
                return false;
            }

            IA_Suprema suprema = component as IA_Suprema;
            if (suprema != null)
            {
                return suprema.teamID == TeamId;
            }

            IA_Dominadora dominadora = component as IA_Dominadora;
            if (dominadora != null)
            {
                return dominadora.teamID == TeamId;
            }

            IA_Comandante comandante = component as IA_Comandante;
            if (comandante != null)
            {
                return comandante.TeamID == TeamId;
            }

            return false;
        }
    }
}
