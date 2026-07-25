using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DefaultExecutionOrder(-930)]
    public sealed class IA01Controller : MonoBehaviour, IIA01Module
    {
        [Header("Identidade")]
        [SerializeField] private int nationId;
        [SerializeField] private int teamId;
        [SerializeField] private int matchSeed = 1;
        [SerializeField] private string nationNameOverride = string.Empty;
        [SerializeField] private string presidentNameOverride = string.Empty;
        [SerializeField] private string currencyNameOverride = string.Empty;
        [SerializeField] private string currencySymbolOverride = string.Empty;
        [SerializeField] private string countryProfileOverride = string.Empty;
        [SerializeField] private string difficultyProfileOverride = string.Empty;

        [Header("Perfil")]
        [SerializeField] private IA01NationProfile profileAsset;
        [SerializeField] private bool createRuntimeProfileWhenMissing = true;

        [Header("Modo")]
        [SerializeField] private IA01ExecutionMode executionModeOverride = IA01ExecutionMode.Full;
        [SerializeField] private IA01NationMode nationModeOverride = IA01NationMode.Normal;
        [SerializeField] private IA01NationStage stageOverride = IA01NationStage.Initialization;
        [SerializeField] private IA01NationPosture postureOverride = IA01NationPosture.Development;

        [Header("Fundacao")]
        [SerializeField] private ComplexoGovernamental prefeituraAnchor;
        [SerializeField] private DadosConstrucao capitalBlueprint;
        [SerializeField] private IA01ConstructionAnchors constructionAnchors;

        [Header("Plano de construcao")]
        [SerializeField] private IA01BuildPlan buildPlan;
        [SerializeField] private IA01CityLayout cityLayout;
        [SerializeField] private List<DadosConstrucao> fichasDeConstrucao = new List<DadosConstrucao>();
        [SerializeField] private GameObject fighterPrefab;
        [SerializeField] private bool useScriptedOpening = true;
        [SerializeField] private bool usePreparedSlots = true;
        [SerializeField] private bool allowAutonomousExpansion = true;
        [SerializeField] private bool enablePlanningAdvisor = true;

        [Header("Progressao militar")]
        [Tooltip("Quando ativo, a reserva militar compra primeiro o menor escalao disponivel e sobe conforme a economia melhora.")]
        [SerializeField] private bool progressiveMilitaryCatalog = true;
        [Tooltip("Permite que uma economia forte/guerra avance mais rapidamente para A e S sem alterar a fila oficial.")]
        [SerializeField] private bool allowMilitaryTierAdvancement = true;

        [Header("Suporte estratégico opcional")]
        [Tooltip("Fica desativado por padrão até os lançadores balísticos e o presidente existirem no jogo.")]
        [SerializeField] private IA01StrategicOptions strategicOptions = new IA01StrategicOptions();

        [Header("Runtime")]
        [SerializeField] private bool autoRegisterWithManager = true;
        [SerializeField] private bool autoApplyGovernmentSnapshot = true;
        [SerializeField] private float fallbackCadenceSeconds = 0.65f;
        [TextArea(3, 12)] [SerializeField] private string runtimeSummary = string.Empty;

        private readonly Stopwatch sliceStopwatch = new Stopwatch();
        private readonly StringBuilder summary = new StringBuilder(512);
        private readonly IA01GameStateBridge gameStateBridge = new IA01GameStateBridge();
        private readonly HashSet<IA01IntentType> loggedAnchorResolutions = new HashSet<IA01IntentType>();
        private IA01RuntimeContext context;
        private IA01NationProfile runtimeProfile;
        private IA01EventBus sharedEventBus;
        private IA01Manager attachedManager;
        private IA01NationRuntime nationRuntime;
        private string uniqueEntityId = string.Empty;
        private string lastGovernmentFingerprint = string.Empty;
        private string lastServiceFingerprint = string.Empty;
        private int subscribedNationId;
        private int pendingEventCount;
        private int lastDirtyCount;
        private string lastExecutionMessage = string.Empty;
        private IA01WorkResult lastExecutionResult;
        private bool restoredFromSave;
        private float nextStandaloneTick;
        [NonSerialized] private IA01StrategicSupport strategicSupport;

        public string ModuleId => UniqueEntityId;
        public bool IsDirty => context != null && context.IsDirty;
        public bool IsEnabled => isActiveAndEnabled;
        public IA01RuntimeContext Context => context;
        public IA01NationProfile Profile => runtimeProfile != null ? runtimeProfile : profileAsset;
        public IA01EventBus EventBus => sharedEventBus;
        public IA01Manager Manager => attachedManager;
        public string UniqueEntityId => uniqueEntityId;
        public int InstanceId => GetInstanceID();
        public int NationId => context != null ? context.NationId : ResolveNationId();
        public int TeamId => context != null ? context.TeamId : ResolveTeamId();
        public string NationName => context != null ? context.NationName : ResolveNationName();
        public string PresidentName => context != null ? context.PresidentName : ResolvePresidentName();
        public IA01ExecutionMode ExecutionMode => context != null ? context.ExecutionMode : executionModeOverride;
        public IA01NationMode NationMode => context != null ? context.NationMode : nationModeOverride;
        public IA01NationStage CurrentStage => context != null ? context.CurrentStage : stageOverride;
        public IA01NationPosture CurrentPosture => context != null ? context.CurrentPosture : postureOverride;
        public string RuntimeSummary => runtimeSummary;
        public string LastExecutionMessage => lastExecutionMessage;
        public IA01WorkResult LastExecutionResult => lastExecutionResult;
        public int LastDirtyCount => lastDirtyCount;
        public int PendingEventCount => pendingEventCount;
        public string ConstructionStatus => nationRuntime != null ? nationRuntime.ConstructionStatus : "Runtime aguardando inicializacao.";
        public string CombatStatus => nationRuntime != null ? nationRuntime.CombatStatus : "Runtime aguardando inicializacao.";
        public int WarEscalationLevel => nationRuntime != null ? nationRuntime.WarEscalationLevel : 0;
        public string MilitaryStatus => nationRuntime != null ? nationRuntime.MilitaryStatus : "Reserva militar aguardando inicializacao.";
        public string PlanningStatus => nationRuntime != null ? nationRuntime.PlanningStatus : "Planejador aguardando inicializacao.";
        public string MarketStatus => nationRuntime != null ? nationRuntime.MarketStatus : "Mercado aguardando inicializacao.";
        public string EconomicStateStatus => nationRuntime != null && nationRuntime.EconomicModel != null ? nationRuntime.EconomicModel.Status : "Economia aguardando inicializacao.";
        public string ProgressionStatus => nationRuntime != null ? nationRuntime.ProgressionStatus : "Runtime aguardando inicializacao.";
        public string NextObjectiveStatus => nationRuntime != null ? nationRuntime.NextObjectiveStatus : "Runtime aguardando inicializacao.";
        public bool FoundationFundingGranted => nationRuntime != null && nationRuntime.FoundationFundingGranted;
        public ComplexoGovernamental PrefeituraAnchor => prefeituraAnchor;
        public DadosConstrucao CapitalBlueprint => capitalBlueprint;
        public IA01ConstructionAnchors ConstructionAnchors => constructionAnchors != null ? constructionAnchors : GetComponentInChildren<IA01ConstructionAnchors>(true);
        public IA01BuildPlan BuildPlan => buildPlan;
        public IA01CityLayout CityLayout => cityLayout;
        public GameObject FighterPrefab => fighterPrefab;
        public IReadOnlyList<DadosConstrucao> FichasDeConstrucao => fichasDeConstrucao;
        public bool UseScriptedOpening => useScriptedOpening;
        public bool UsePreparedSlots => usePreparedSlots;
        public bool AllowAutonomousExpansion => allowAutonomousExpansion;
        public bool EnablePlanningAdvisor => enablePlanningAdvisor;
        public bool ProgressiveMilitaryCatalog => progressiveMilitaryCatalog;
        public bool AllowMilitaryTierAdvancement => allowMilitaryTierAdvancement;
        public IA01StrategicOptions StrategicOptions => strategicOptions;
        public IA01StrategicSupport StrategicSupport => strategicSupport;
        public IA01BuildSlot CapitalSlot => cityLayout != null ? cityLayout.CapitalSlot : null;

        public bool TryResolveConstructionAnchor(IA01IntentType intent, out Vector3 position)
        {
            position = Vector3.zero;
            Quaternion ignored;
            return TryResolveConstructionAnchor(intent, out position, out ignored);
        }

        public bool TryResolveConstructionAnchor(IA01IntentType intent, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            // Os creates "IA01 Local - ..." sao os pontos oficiais do pais.
            // Nem todas as cenas possuem o componente opcional
            // IA01ConstructionAnchors, entao resolvemos o slot pelo mesmo id
            // usado no plano de construcao. Isso impede a IA de procurar um lote
            // livre ou construir em outra regiao quando o create ja existe.
            string slotId = ResolveLocalSlotId(intent);
            if (intent == IA01IntentType.BuildMilitaryTent)
            {
                // Se o novo create de quartel foi colocado pelo criador, ele
                // passa a ser o ponto oficial da infantaria. Sem esse create,
                // o slot antigo da tenda permanece como fallback.
                IA01CityLayout quartelLayout = ResolveCityLayoutForSlot("ia01.local.quartel");
                IA01BuildSlot quartelSlot;
                if (quartelLayout != null && quartelLayout.TryGetSlot("ia01.local.quartel", out quartelSlot) && quartelSlot != null)
                {
                    slotId = "ia01.local.quartel";
                }
            }
            if (intent == IA01IntentType.BuildOffshorePlatform
                && TryResolvePlatformSlot(out position, out rotation, out string platformSlotId))
            {
                if (loggedAnchorResolutions.Add(intent))
                {
                    UnityEngine.Debug.Log("[IA01 Anchor] " + intent + " -> " + platformSlotId + " pos=" + position.ToString("F2"));
                }
                return true;
            }
            IA01BuildSlot slot;
            IA01CityLayout resolvedLayout = ResolveCityLayoutForSlot(slotId);
            if (!string.IsNullOrWhiteSpace(slotId) && resolvedLayout != null && resolvedLayout.TryGetSlot(slotId, out slot) && slot != null)
            {
                Transform point = slot.BuildingPoint != null ? slot.BuildingPoint : slot.transform;
                position = point.position;
                rotation = point.rotation;
                if (loggedAnchorResolutions.Add(intent))
                {
                    UnityEngine.Debug.Log("[IA01 Anchor] " + intent + " -> " + slot.name + " (" + slot.SlotId + ") pos=" + position.ToString("F2"));
                }
                return true;
            }

            // Compatibilidade com cenas antigas: somente usa as referencias
            // legadas se o create oficial nao existir. Essas referencias podem
            // estar preenchidas no prefab, mas nunca devem sobrescrever um
            // create diferente configurado na cena.
            IA01ConstructionAnchors anchors = ConstructionAnchors;
            if (anchors != null)
            {
                Vector3 resolvedPosition;
                Quaternion resolvedRotation;
                if (anchors.TryResolve(intent, out resolvedPosition, out resolvedRotation))
                {
                    position = resolvedPosition;
                    rotation = resolvedRotation;
                    if (loggedAnchorResolutions.Add(intent))
                    {
                        UnityEngine.Debug.Log("[IA01 Anchor] " + intent + " -> legado (slot " + slotId + " indisponivel) pos=" + position.ToString("F2"));
                    }
                    return true;
                }
            }

            return false;
        }

        private bool TryResolvePlatformSlot(out Vector3 position, out Quaternion rotation, out string slotId)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            slotId = string.Empty;
            string[] ids = { "ia01.local.plataforma.a", "ia01.local.plataforma.b", "ia01.local.plataforma.c" };
            int start = Mathf.Abs((matchSeed * 31) + TeamId) % ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                string candidateId = ids[(start + i) % ids.Length];
                IA01CityLayout layout = ResolveCityLayoutForSlot(candidateId);
                if (layout == null || !layout.TryGetSlot(candidateId, out IA01BuildSlot slot) || slot == null)
                {
                    continue;
                }

                Transform point = slot.BuildingPoint != null ? slot.BuildingPoint : slot.transform;
                position = point.position;
                rotation = point.rotation;
                slotId = slot.name + " (" + slot.SlotId + ")";
                return true;
            }
            return false;
        }

        private IA01CityLayout ResolveCityLayoutForSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return null;
            IA01CityLayout candidate = cityLayout;
            IA01BuildSlot ignored;
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored)) return candidate;

            candidate = GetComponentInChildren<IA01CityLayout>(true);
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored)) return candidate;
            candidate = GetComponentInParent<IA01CityLayout>(true);
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored)) return candidate;

            // Algumas cenas antigas mantêm o layout como irmao do controlador.
            // Escolhe o layout que realmente possui o create solicitado e que
            // esta mais proximo desta IA, evitando pegar o create de outro pais.
            IA01CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA01CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < layouts.Length; i++)
            {
                IA01CityLayout layout = layouts[i];
                if (layout == null || !layout.TryGetSlot(slotId, out ignored)) continue;
                float distance = (layout.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    candidate = layout;
                }
            }
            return candidate;
        }

        private static string ResolveLocalSlotId(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.EstablishCapital: return "prefeitura_01";
                case IA01IntentType.BuildEnergy: return "energia_01";
                case IA01IntentType.BuildFoodProduction: return "fazenda_01";
                case IA01IntentType.BuildStorage: return "armazem_01";
                case IA01IntentType.BuildVehicleConstructor: return "ia01.local.construtor_veiculos";
                case IA01IntentType.BuildMilitaryAirport: return "ia01.local.aeroporto_militar";
                case IA01IntentType.BuildCommercialAirport: return "ia01.local.aeroporto_comercial";
                case IA01IntentType.BuildShipyard: return "ia01.local.estaleiro";
                case IA01IntentType.BuildPier: return "ia01.local.pier";
                case IA01IntentType.BuildOffshorePlatform: return "ia01.local.plataforma.a";
                case IA01IntentType.BuildMilitaryTent: return "ia01.local.tenda";
                case IA01IntentType.BuildStarterHouse: return "ia01.local.casa";
                case IA01IntentType.BuildMediumApartment: return "ia01.local.apartamento_medio";
                case IA01IntentType.BuildHighApartment: return "ia01.local.apartamento_alto";
                default: return string.Empty;
            }
        }

        private void Awake()
        {
            // Unity só aceita DontDestroyOnLoad em objetos raiz. Prefabs de
            // país podem ser filhos de um bootstrap da cena, então preserva
            // a instância sem gerar aviso nem mover hierarquia indevidamente.
            if (Application.isPlaying && transform.parent == null) DontDestroyOnLoad(gameObject);
            EnsureBootstrap(false);
        }

        // Controllers can be created after the manager (or in a scene that has no
        // manager object at all). Keep a small safety tick here so the nation still
        // reaches its production directors instead of remaining permanently idle.
        private void Update()
        {
            if (!Application.isPlaying || !IsEnabled)
            {
                return;
            }

            if (attachedManager == null && autoRegisterWithManager)
            {
                RegisterWithManager();
            }

            if (attachedManager == null && Time.unscaledTime >= nextStandaloneTick)
            {
                nextStandaloneTick = Time.unscaledTime + Mathf.Max(0.25f, fallbackCadenceSeconds);
                ExecuteSlice(IA01WorkBudget.Create(3f, 4, 2, true, false));
            }
        }

        private void OnEnable()
        {
            EnsureBootstrap(false);
            SistemaDeDanos.OnDanoGlobal += HandleGlobalDamage;
            if (Application.isPlaying && autoRegisterWithManager) RegisterWithManager();
        }

        private void OnDisable()
        {
            SistemaDeDanos.OnDanoGlobal -= HandleGlobalDamage;
            Shutdown();
        }

        private void OnDestroy()
        {
            SistemaDeDanos.OnDanoGlobal -= HandleGlobalDamage;
            Shutdown();
        }

        private void HandleGlobalDamage(SistemaDeDanos victimDamage, GameObject aggressor, float damage)
        {
            if (!Application.isPlaying || victimDamage == null || aggressor == null) return;

            IdentidadeUnidade victimIdentity = SistemaDeDanos.ResolverIdentidade(victimDamage);
            IdentidadeUnidade aggressorIdentity = SistemaDeDanos.ResolverIdentidade(aggressor.transform);
            if (victimIdentity == null || aggressorIdentity == null
                || victimIdentity.teamID != TeamId || aggressorIdentity.teamID <= 0
                || aggressorIdentity.teamID == TeamId) return;

            EnsureBootstrap(false);
            nationRuntime?.RegisterHostileAggression(aggressorIdentity.teamID, aggressor.transform.position, damage);
        }

        public void Initialize(IA01RuntimeContext suppliedContext)
        {
            context = suppliedContext ?? context;
            EnsureBootstrap(false);
        }

        public void MarkDirty(IA01DirtyReason reason)
        {
            EnsureBootstrap(false);
            context?.MarkDirty(reason);
        }

        public IA01WorkResult ExecuteSlice(IA01WorkBudget budget)
        {
            if (!IsEnabled || budget.MaxOperations <= 0 && budget.MaxEvents <= 0)
            {
                return lastExecutionResult = IA01WorkResult.Empty("disabled_or_empty_budget");
            }

            EnsureBootstrap(false);
            sliceStopwatch.Restart();
            int operations = 0;
            int events = pendingEventCount;
            pendingEventCount = 0;
            bool changed = false;
            DadosPaisGoverno country = null;
            if (autoApplyGovernmentSnapshot)
            {
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                country = government != null ? government.ObterPais(TeamId) : null;
                string fingerprint = BuildGovernmentFingerprint(country);
                if (!string.Equals(fingerprint, lastGovernmentFingerprint, StringComparison.Ordinal))
                {
                    context.ApplyGovernmentSnapshot(country);
                    lastGovernmentFingerprint = fingerprint;
                    changed = true;
                    operations++;
                }
            }

            if (nationRuntime != null && operations < budget.MaxOperations)
            {
                nationRuntime.Economy.Refresh(country);
                int runtimeOperations = nationRuntime.Execute(Time.unscaledTime, budget.MaxOperations - operations, restoredFromSave);
                restoredFromSave = false;
                operations += runtimeOperations;
                changed |= runtimeOperations > 0;
            }

            // O suporte estratégico é opcional e só trabalha quando alguma
            // integração futura foi explicitamente ativada no Inspector.
            if (strategicSupport != null
                && (strategicSupport.Options.BallisticEnabled
                    || strategicSupport.Options.EnableStrategicLeaderIntegration
                    || strategicSupport.Options.EnableCountryTransferOperation)
                && operations < budget.MaxOperations)
            {
                operations += strategicSupport.ProcessSlice(Time.unscaledTime, budget.MaxOperations - operations);
            }

            if (operations < budget.MaxOperations)
            {
                operations += gameStateBridge.Refresh(context, budget.MaxOperations - operations);
                changed |= gameStateBridge.LastChangedResources > 0;
            }

            operations += context.AdvanceMaintenance(Time.unscaledTime);
            List<IA01DirtyReason> dirty = context.ConsumeDirtyReasons();
            lastDirtyCount = dirty.Count;
            changed |= dirty.Count > 0;
            context.SetMetric("ia01.last_slice_ms", sliceStopwatch.Elapsed.TotalMilliseconds);
            context.SetMetric("ia01.last_operations", operations);
            context.SetMetric("ia01.last_events", events);
            lastExecutionMessage = "slice nation=" + NationId + " dirty=" + dirty.Count + " cadence=" + (Profile != null ? Profile.ResolveCadence(ExecutionMode, CurrentStage, NationMode) : fallbackCadenceSeconds).ToString("0.000", CultureInfo.InvariantCulture);
            lastExecutionResult = IA01WorkResult.From(true, changed, operations, events, (float)sliceStopwatch.Elapsed.TotalMilliseconds, lastExecutionMessage);
            runtimeSummary = BuildRuntimeSummary();
            return lastExecutionResult;
        }

        public void Shutdown()
        {
            if (attachedManager != null) attachedManager.UnregisterController(this);
            if (sharedEventBus != null && subscribedNationId > 0) sharedEventBus.UnsubscribeNation(subscribedNationId, HandleRuntimeEvent);
            attachedManager = null;
            sharedEventBus = null;
            subscribedNationId = 0;
            nationRuntime = null;
            strategicSupport = null;
            pendingEventCount = 0;
        }

        public void SetProfile(IA01NationProfile newProfile)
        {
            profileAsset = newProfile;
            runtimeProfile = null;
            nationRuntime = null;
            EnsureBootstrap(false);
        }

        public void SetMatchSeed(int newMatchSeed)
        {
            matchSeed = newMatchSeed;
            nationRuntime = null;
            EnsureBootstrap(true);
        }

        public void ConfigureIdentity(int newNationId, int newTeamId, string newNationName = null, string newPresidentName = null, string newCurrencyName = null, string newCurrencySymbol = null, string newCountryProfile = null, string newDifficultyProfile = null)
        {
            if (newNationId > 0) nationId = newNationId;
            if (newTeamId > 0) teamId = newTeamId;
            if (newNationName != null) nationNameOverride = newNationName;
            if (newPresidentName != null) presidentNameOverride = newPresidentName;
            if (newCurrencyName != null) currencyNameOverride = newCurrencyName;
            if (newCurrencySymbol != null) currencySymbolOverride = newCurrencySymbol;
            if (newCountryProfile != null) countryProfileOverride = newCountryProfile;
            if (newDifficultyProfile != null) difficultyProfileOverride = newDifficultyProfile;
            nationRuntime = null;
            EnsureBootstrap(false);
            RefreshEventBusSubscription();
        }

        public void ConfigureFromGovernment(DadosPaisGoverno country, int newMatchSeed, string difficultyCode = null)
        {
            if (country == null) return;
            nationId = teamId = country.teamId;
            matchSeed = newMatchSeed;
            nationNameOverride = country.nomePais ?? string.Empty;
            presidentNameOverride = country.nomePresidente ?? string.Empty;
            currencyNameOverride = country.nomeMoeda ?? string.Empty;
            currencySymbolOverride = country.simboloMoeda ?? string.Empty;
            countryProfileOverride = country.perfilIA.ToString();
            difficultyProfileOverride = difficultyCode ?? difficultyProfileOverride;
            restoredFromSave = false;
            runtimeProfile = IA01NationProfile.CreateRuntimeFromGovernment(country, matchSeed, difficultyProfileOverride);
            nationRuntime = null;
            EnsureBootstrap(false);
        }

        public SaveIA01NationState CaptureSaveState()
        {
            EnsureBootstrap(false);
            SaveIA01NationState state = context.CaptureSaveState();
            state.instanceId = InstanceId;
            state.nationId = NationId;
            state.teamId = TeamId;
            state.nationName = NationName;
            state.presidentName = PresidentName;
            state.currencyName = context.CurrencyName;
            state.currencySymbol = context.CurrencySymbol;
            state.countryProfile = context.CountryProfile;
            state.difficultyProfile = context.DifficultyProfile;
            state.randomSeed = context.RandomSeed;
            state.executionMode = ExecutionMode;
            state.nationMode = NationMode;
            state.currentStage = CurrentStage;
            state.currentPosture = CurrentPosture;
            state.profileSnapshot = Profile != null ? Profile.CaptureSnapshot() : new IA01NationProfileSnapshot();
            state.foundationSequenceStep = nationRuntime != null ? nationRuntime.FoundationSequenceStatus : string.Empty;
            state.foundationSkippedSteps = nationRuntime != null && nationRuntime.CityPlanner != null
                ? nationRuntime.CityPlanner.CaptureUnavailableSequenceSteps()
                : new System.Collections.Generic.List<string>();
            state.foundationFundingGranted = nationRuntime != null && nationRuntime.FoundationFundingGranted;
            state.buildPlanState = nationRuntime != null && nationRuntime.BuildPlanRuntime != null
                ? nationRuntime.BuildPlanRuntime.CaptureSaveState()
                : new SaveIA01BuildPlanState();
            return state;
        }

        public void RestoreFromSaveState(SaveIA01NationState state)
        {
            if (state == null) return;
            nationId = state.nationId;
            teamId = state.teamId;
            matchSeed = state.randomSeed - Mathf.Max(1, state.nationId);
            nationNameOverride = state.nationName ?? string.Empty;
            presidentNameOverride = state.presidentName ?? string.Empty;
            currencyNameOverride = state.currencyName ?? string.Empty;
            currencySymbolOverride = state.currencySymbol ?? string.Empty;
            countryProfileOverride = state.countryProfile ?? string.Empty;
            difficultyProfileOverride = state.difficultyProfile ?? string.Empty;
            runtimeProfile = ScriptableObject.CreateInstance<IA01NationProfile>();
            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            if (state.profileSnapshot != null) runtimeProfile.ApplySnapshot(state.profileSnapshot);
            nationRuntime = null;
            EnsureBootstrap(false);
            context.RestoreFromSaveState(state);
            context.SetExecutionMode(state.executionMode);
            context.SetNationMode(state.nationMode);
            context.SetCurrentStage(state.currentStage);
            context.SetCurrentPosture(state.currentPosture);
            if (nationRuntime != null)
            {
                nationRuntime.RestoreFoundationState(state);
            }
            restoredFromSave = true;
            RefreshEventBusSubscription();
        }

        public void ApplyServiceDiagnostics(IA01ServiceDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null) return;
            string fingerprint = (snapshot.Report ?? string.Empty) + "|" + snapshot.DifficultyCode;
            if (fingerprint == lastServiceFingerprint) return;
            EnsureBootstrap(false);
            context.ApplyServiceSnapshot(snapshot);
            lastServiceFingerprint = fingerprint;
        }

        public void AttachManager(IA01Manager manager)
        {
            attachedManager = manager;
            SetSharedEventBus(manager != null ? manager.EventBus : null);
        }

        public void DetachManager(IA01Manager manager)
        {
            if (manager != null && attachedManager != manager) return;
            attachedManager = null;
            SetSharedEventBus(null);
        }

        public void SetSharedEventBus(IA01EventBus bus)
        {
            if (sharedEventBus == bus) return;
            if (sharedEventBus != null && subscribedNationId > 0) sharedEventBus.UnsubscribeNation(subscribedNationId, HandleRuntimeEvent);
            sharedEventBus = bus;
            subscribedNationId = 0;
            RefreshEventBusSubscription();
        }

        public int PublishEvent(string topic, string message, object payload = null, IA01EventSeverity severity = IA01EventSeverity.Info)
        {
            if (sharedEventBus == null) return 0;
            return sharedEventBus.Publish(new IA01RuntimeEvent { NationId = NationId, TeamId = TeamId, SourceInstanceId = InstanceId, Topic = topic ?? string.Empty, Message = message ?? string.Empty, Payload = payload, Severity = severity, TimeStamp = Time.unscaledTime });
        }

        public IA01BallisticThreatRecord RegisterBallisticImpact(Vector3 impactPosition, Vector3 predictedTargetPosition, Vector3 arrivalDirection, Vector3 probableLaunchArea, IA01BallisticMissileType missileType, IA01BallisticWarheadType warheadType, int launchCount, float damage, string infrastructureHit, int suspectedCountryId = 0, int confirmedCountryId = 0, Vector3 knownLaunchPosition = default(Vector3), bool launchPositionKnown = false, float authorshipConfidence = 0f)
        {
            EnsureBootstrap(false);
            return strategicSupport != null
                ? strategicSupport.RegisterBallisticImpact(impactPosition, predictedTargetPosition, arrivalDirection, probableLaunchArea, missileType, warheadType, launchCount, damage, infrastructureHit, suspectedCountryId, confirmedCountryId, knownLaunchPosition, launchPositionKnown, authorshipConfidence, Time.unscaledTime)
                : null;
        }

        public bool RegisterLeaderEvent(IA01LeaderEventType eventType, string leaderId, Vector3 position, float confidence, int relatedCountryId = 0, string regionId = null, string buildingId = null, string vehicleId = null)
        {
            EnsureBootstrap(false);
            return strategicSupport != null && strategicSupport.RegisterLeaderEvent(eventType, leaderId, position, confidence, Time.unscaledTime, relatedCountryId, regionId, buildingId, vehicleId);
        }

        public bool BeginCountryTransfer(int winnerCountryId, int defeatedCountryId, int regions, int cities, int structures, int resources, int units)
        {
            EnsureBootstrap(false);
            return strategicSupport != null && strategicSupport.BeginCountryTransfer(winnerCountryId, defeatedCountryId, regions, cities, structures, resources, units);
        }

        public IA01WorldEntityRecord BuildWorldRecord()
        {
            EnsureBootstrap(false);
            return new IA01WorldEntityRecord { EntityId = UniqueEntityId, InstanceId = InstanceId, NationId = NationId, TeamId = TeamId, DisplayName = NationName, Kind = IA01WorldEntityKind.Controller, Domain = IA01WorldDomain.Command, Category = "controller", RegionKey = "nation:" + NationId, Position = transform.position, Operational = isActiveAndEnabled, Version = context.Version, State = context.BuildDebugSummary(), Source = "IA01Controller" };
        }

        public void EnsureBootstrap(bool forceProfileRebuild)
        {
            if (context == null) context = new IA01RuntimeContext();
            if (strategicOptions == null) strategicOptions = new IA01StrategicOptions();
            if (runtimeProfile == null || forceProfileRebuild)
            {
                runtimeProfile = profileAsset != null ? profileAsset.CloneForRuntime(context.GetIdentitySnapshot()) : (createRuntimeProfileWhenMissing ? ScriptableObject.CreateInstance<IA01NationProfile>() : null);
                if (runtimeProfile != null) runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            DadosPaisGoverno country = null;
            if (autoApplyGovernmentSnapshot && SistemaGovernoMundial.Instancia != null) country = SistemaGovernoMundial.Instancia.ObterPais(ResolveTeamId());
            if (country != null && runtimeProfile != null) runtimeProfile.ApplyGovernmentBias(country, difficultyProfileOverride);
            IA01NationIdentity identity = runtimeProfile != null
                ? runtimeProfile.BuildIdentity(InstanceId, ResolveNationId(), ResolveTeamId(), matchSeed, executionModeOverride, nationModeOverride, stageOverride, postureOverride, nationNameOverride, presidentNameOverride, currencyNameOverride, currencySymbolOverride, countryProfileOverride, difficultyProfileOverride)
                : new IA01NationIdentity { InstanceId = InstanceId, NationId = ResolveNationId(), TeamId = ResolveTeamId(), NationName = ResolveNationName(), PresidentName = ResolvePresidentName(), CurrencyName = "Credit", CurrencySymbol = "$", CountryProfile = "Neutral", DifficultyProfile = "normal", RandomSeed = matchSeed + ResolveNationId(), ExecutionMode = executionModeOverride, NationMode = nationModeOverride, CurrentStage = stageOverride, CurrentPosture = postureOverride };
            context.ApplyIdentity(identity);
            if (cityLayout == null) cityLayout = GetComponentInChildren<IA01CityLayout>(true);
            if (constructionAnchors == null) constructionAnchors = GetComponentInChildren<IA01ConstructionAnchors>(true);
            cityLayout?.ConfigureOwner(identity.TeamId, identity.NationId);
            if (country != null) context.ApplyGovernmentSnapshot(country);
            uniqueEntityId = "ia01:" + identity.NationId + ":" + identity.TeamId + ":" + InstanceId;
            if (nationRuntime == null) nationRuntime = new IA01NationRuntime(this, context, runtimeProfile);
            if (strategicSupport == null) strategicSupport = new IA01StrategicSupport(identity.NationId, strategicOptions);
            RefreshEventBusSubscription();
            runtimeSummary = BuildRuntimeSummary();
        }

        private void RegisterWithManager()
        {
            IA01Manager manager = IA01Manager.Instancia;
            if (manager != null) manager.RegisterController(this);
        }

        private void RefreshEventBusSubscription()
        {
            if (sharedEventBus == null || NationId <= 0 || subscribedNationId == NationId) return;
            if (subscribedNationId > 0) sharedEventBus.UnsubscribeNation(subscribedNationId, HandleRuntimeEvent);
            subscribedNationId = NationId;
            sharedEventBus.SubscribeNation(subscribedNationId, HandleRuntimeEvent);
        }

        private void HandleRuntimeEvent(IA01RuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null || runtimeEvent.NationId > 0 && runtimeEvent.NationId != NationId) return;
            pendingEventCount++;
            if (runtimeEvent.Severity >= IA01EventSeverity.Warning) context.MarkDirty(IA01DirtyReason.ExternalEvent);
        }

        private string BuildGovernmentFingerprint(DadosPaisGoverno country)
        {
            return country == null ? "none" : country.teamId + "|" + country.saldo + "|" + country.emGuerra + "|" + country.rivalTeamId + "|" + country.energia + "|" + country.comida;
        }

        private string BuildRuntimeSummary()
        {
            summary.Clear();
            summary.Append("id=").Append(UniqueEntityId)
                .Append(" nation=").Append(NationName)
                .Append(" teamId=").Append(TeamId)
                .Append(" dirty=").Append(context != null ? context.DirtyCount : 0)
                .Append(" progression=").Append(ProgressionStatus)
                .Append(" objective=").Append(NextObjectiveStatus)
                .Append(" construction=").Append(ConstructionStatus)
                .Append(" combat=").Append(CombatStatus)
                .Append(" military=").Append(MilitaryStatus)
                .Append(" planning=").Append(PlanningStatus)
                .Append(" market=").Append(MarketStatus)
                .Append(" economy=").Append(EconomicStateStatus)
                .Append(" strategic=").Append(strategicSupport != null ? strategicSupport.Status : "n/d");
            return summary.ToString();
        }

        private int ResolveNationId() => nationId > 0 ? nationId : (profileAsset != null && profileAsset.NationIdHint > 0 ? profileAsset.NationIdHint : Mathf.Max(1, InstanceId));
        private int ResolveTeamId() => teamId > 0 ? teamId : (profileAsset != null && profileAsset.TeamIdHint > 0 ? profileAsset.TeamIdHint : ResolveNationId());
        private string ResolveNationName() => !string.IsNullOrWhiteSpace(nationNameOverride) ? nationNameOverride : "Nation " + ResolveNationId();
        private string ResolvePresidentName() => !string.IsNullOrWhiteSpace(presidentNameOverride) ? presidentNameOverride : "President " + ResolveNationId();
    }
}
