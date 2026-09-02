using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DefaultExecutionOrder(-930)]
    public sealed class IA02Controller : MonoBehaviour, IIA02Module
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
        [SerializeField] private IA02NationProfile profileAsset;
        [SerializeField] private bool createRuntimeProfileWhenMissing = true;

        [Header("Modo")]
        [SerializeField] private IA02ExecutionMode executionModeOverride = IA02ExecutionMode.Full;
        [SerializeField] private IA02NationMode nationModeOverride = IA02NationMode.Normal;
        [SerializeField] private IA02NationStage stageOverride = IA02NationStage.Initialization;
        [SerializeField] private IA02NationPosture postureOverride = IA02NationPosture.Development;

        [Header("Fundacao")]
        [SerializeField] private ComplexoGovernamental prefeituraAnchor;
        [SerializeField] private DadosConstrucao capitalBlueprint;
        [SerializeField] private IA02ConstructionAnchors constructionAnchors;

        [Header("Plano de construcao")]
        [SerializeField] private IA02BuildPlan buildPlan;
        [SerializeField] private IA02CityLayout cityLayout;
        [SerializeField] private List<DadosConstrucao> fichasDeConstrucao = new List<DadosConstrucao>();
        [Header("Catálogo militar permitido")]
        [Tooltip("Fichas militares que esta IA pode produzir. Se vazio, usa somente a allowlist padrão interna.")]
        [SerializeField] private List<DadosConstrucao> fichasMilitaresPermitidas = new List<DadosConstrucao>();
        [SerializeField] private GameObject fighterPrefab;
        [SerializeField] private bool useScriptedOpening = true;
        [SerializeField] private bool usePreparedSlots = true;
        [SerializeField] private bool allowAutonomousExpansion = true;
        [SerializeField] private bool enablePlanningAdvisor = true;
        [Tooltip("Raio de seguranca para impedir que um layout/slot duplicado no lado do jogador seja usado pela IA.")]
        [SerializeField, Min(500f)] private float maxConstructionDistanceFromController = 4200f;

        [Header("Progressao militar")]
        [Tooltip("Quando ativo, a reserva militar compra primeiro o menor escalao disponivel e sobe conforme a economia melhora.")]
        [SerializeField] private bool progressiveMilitaryCatalog = true;
        [Tooltip("Permite que uma economia forte/guerra avance mais rapidamente para A e S sem alterar a fila oficial.")]
        [SerializeField] private bool allowMilitaryTierAdvancement = true;

        [Header("Suporte estratégico opcional")]
        [Tooltip("Fica desativado por padrão até os lançadores balísticos e o presidente existirem no jogo.")]
        [SerializeField] private IA02StrategicOptions strategicOptions = new IA02StrategicOptions();

        [Header("Runtime")]
        [SerializeField] private bool autoRegisterWithManager = true;
        [SerializeField] private bool autoApplyGovernmentSnapshot = true;
        [SerializeField] private float fallbackCadenceSeconds = 0.65f;
        [Header("Cadencia de construcao")]
        [Tooltip("Depois da prefeitura, a IA espera este intervalo real entre obras confirmadas. Evita que a abertura concentre instanciacao, fisica e catalogo no mesmo frame.")]
        [SerializeField, Min(0f)] private float nonCapitalConstructionIntervalSeconds = 5f;
        [TextArea(3, 12)] [SerializeField] private string runtimeSummary = string.Empty;

        private readonly Stopwatch sliceStopwatch = new Stopwatch();
        private readonly StringBuilder summary = new StringBuilder(512);
        private readonly IA02GameStateBridge gameStateBridge = new IA02GameStateBridge();
        private readonly HashSet<IA02IntentType> loggedAnchorResolutions = new HashSet<IA02IntentType>();
        private IA02RuntimeContext context;
        private IA02NationProfile runtimeProfile;
        private IA02EventBus sharedEventBus;
        private IA02Manager attachedManager;
        private IA02NationRuntime nationRuntime;
        private string uniqueEntityId = string.Empty;
        private string lastGovernmentFingerprint = string.Empty;
        private string lastServiceFingerprint = string.Empty;
        private int subscribedNationId;
        private int pendingEventCount;
        private int lastDirtyCount;
        private string lastExecutionMessage = string.Empty;
        private IA02WorkResult lastExecutionResult;
        private bool restoredFromSave;
        private float nextStandaloneTick;
        [NonSerialized] private IA02StrategicSupport strategicSupport;

        public string ModuleId => UniqueEntityId;
        public bool IsDirty => context != null && context.IsDirty;
        public bool IsEnabled => isActiveAndEnabled;
        public IA02RuntimeContext Context => context;
        // Exposto somente para coordenadores internos que precisam distinguir uma
        // prefeitura realmente registrada de um marcador de construcao pendente.
        public IA02NationRuntime Runtime => nationRuntime;
        public IA02NationProfile Profile => runtimeProfile != null ? runtimeProfile : profileAsset;
        public IA02EventBus EventBus => sharedEventBus;
        public IA02Manager Manager => attachedManager;
        public bool HasProductionAuthority => attachedManager != null && attachedManager.HasProductionAuthority(this);
        public string UniqueEntityId => uniqueEntityId;
        public int InstanceId => GetInstanceID();
        public int NationId => context != null ? context.NationId : ResolveNationId();
        public int TeamId => context != null ? context.TeamId : ResolveTeamId();
        public string NationName => context != null ? context.NationName : ResolveNationName();
        public string PresidentName => context != null ? context.PresidentName : ResolvePresidentName();
        public IA02ExecutionMode ExecutionMode => context != null ? context.ExecutionMode : executionModeOverride;
        public IA02NationMode NationMode => context != null ? context.NationMode : nationModeOverride;
        public IA02NationStage CurrentStage => context != null ? context.CurrentStage : stageOverride;
        public IA02NationPosture CurrentPosture => context != null ? context.CurrentPosture : postureOverride;
        public string RuntimeSummary => runtimeSummary;
        public string LastExecutionMessage => lastExecutionMessage;
        public IA02WorkResult LastExecutionResult => lastExecutionResult;
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
        public bool HasConfirmedCapital => nationRuntime != null
            && nationRuntime.CityPlanner != null
            && nationRuntime.CityPlanner.Capital != null;
        public ComplexoGovernamental PrefeituraAnchor => prefeituraAnchor;
        public DadosConstrucao CapitalBlueprint => capitalBlueprint;
        public IA02ConstructionAnchors ConstructionAnchors => constructionAnchors != null ? constructionAnchors : GetComponentInChildren<IA02ConstructionAnchors>(true);
        public IA02BuildPlan BuildPlan => buildPlan;
        public IA02CityLayout CityLayout => cityLayout;
        public GameObject FighterPrefab => fighterPrefab;
        public IReadOnlyList<DadosConstrucao> FichasDeConstrucao => fichasDeConstrucao;
        public IReadOnlyList<DadosConstrucao> FichasMilitaresPermitidas => fichasMilitaresPermitidas;
        public bool UseScriptedOpening => useScriptedOpening;
        public bool UsePreparedSlots => usePreparedSlots;
        public bool AllowAutonomousExpansion => allowAutonomousExpansion;
        public bool EnablePlanningAdvisor => enablePlanningAdvisor;
        public bool ProgressiveMilitaryCatalog => progressiveMilitaryCatalog;
        public bool AllowMilitaryTierAdvancement => allowMilitaryTierAdvancement;
        public IA02StrategicOptions StrategicOptions => strategicOptions;
        public IA02StrategicSupport StrategicSupport => strategicSupport;
        public IA02BuildSlot CapitalSlot => cityLayout != null ? cityLayout.CapitalSlot : null;

        /// <summary>
        /// Controllers criados em runtime a partir do governo nao possuem um
        /// layout serializado na cena. Eles usam o planejador autonomo em um
        /// ponto seguro do proprio controller; a campanha canonica continua
        /// usando slots preparados normalmente.
        /// </summary>
        public void ConfigureForAutonomousRuntime()
        {
            usePreparedSlots = false;
            useScriptedOpening = false;
            allowAutonomousExpansion = true;
        }
        public float NonCapitalConstructionIntervalSeconds => Mathf.Max(0f, nonCapitalConstructionIntervalSeconds);

        /// <summary>
        /// A IA só pode executar quando a identidade, o governo e o layout
        /// oficial já estiverem disponíveis. Este método não cria estruturas;
        /// apenas prepara e valida os dados necessários para o primeiro slice.
        /// </summary>
        public bool IsWorldReady(out string reason)
        {
            reason = string.Empty;
            if (!isActiveAndEnabled)
            {
                reason = "controller inativo";
                return false;
            }

            EnsureBootstrap(false);
            if (NationId <= 0 || TeamId <= 0)
            {
                reason = "identidade da nação ainda não resolvida";
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            if (Application.isPlaying && government == null)
            {
                reason = "governo mundial ainda não inicializado";
                return false;
            }

            if (Application.isPlaying && government.ObterPais(TeamId) == null)
            {
                reason = "país da IA ainda não registrado: team=" + TeamId;
                return false;
            }

            IA02CityLayout layout = CityLayout;
            if (layout != null)
            {
                if (!layout.EnsureRuntimeReady())
                {
                    reason = "layout sem slots preparados: " + layout.LayoutId;
                    return false;
                }
                if (layout.OwnerTeamId != TeamId || layout.OwnerNationId != NationId)
                    layout.ConfigureOwner(TeamId, NationId);
            }
            else if (Application.isPlaying && UsePreparedSlots)
            {
                reason = "layout preparado ausente";
                return false;
            }

            return true;
        }

        public bool TryResolveConstructionAnchor(IA02IntentType intent, out Vector3 position)
        {
            position = Vector3.zero;
            Quaternion ignored;
            return TryResolveConstructionAnchor(intent, out position, out ignored);
        }

        /// <summary>
        /// Valida uma posição de estrutura contra o envelope do layout oficial
        /// desta nação. É usado na migração de saves antigos, cujo layout podia
        /// estar em coordenadas diferentes, antes de restaurar uma construção.
        /// Unidades móveis não passam por esta validação.
        /// </summary>
        public bool IsPositionInsidePreparedTerritory(Vector3 position, float margin = 220f)
        {
            IA02CityLayout layout = CityLayout;
            if (layout == null)
            {
                return !UsePreparedSlots;
            }
            if (layout.OwnerTeamId > 0 && layout.OwnerTeamId != TeamId) return false;

            // O layout oficial fica ancorado no controlador da nação. Se um
            // prefab/slot for duplicado na área do jogador, o envelope dos
            // slots sozinho não basta para detectar o erro: bloqueie também
            // qualquer create muito distante da raiz IA02.
            float maxDistance = Mathf.Max(500f, maxConstructionDistanceFromController);
            Vector3 fromController = position - transform.position;
            fromController.y = 0f;
            if (fromController.sqrMagnitude > maxDistance * maxDistance) return false;

            IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
            bool foundOwnedSlot = false;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            for (int i = 0; i < slots.Length; i++)
            {
                IA02BuildSlot slot = slots[i];
                if (slot == null || (slot.OwnerTeamId > 0 && slot.OwnerTeamId != TeamId)) continue;
                Transform point = slot.BuildingPoint != null ? slot.BuildingPoint : slot.transform;
                if (point == null) continue;

                foundOwnedSlot = true;
                Vector3 slotPosition = point.position;
                minX = Mathf.Min(minX, slotPosition.x);
                maxX = Mathf.Max(maxX, slotPosition.x);
                minZ = Mathf.Min(minZ, slotPosition.z);
                maxZ = Mathf.Max(maxZ, slotPosition.z);
            }

            if (!foundOwnedSlot) return false;
            float safeMargin = Mathf.Max(50f, margin);
            bool insidePreparedEnvelope = position.x >= minX - safeMargin && position.x <= maxX + safeMargin
                && position.z >= minZ - safeMargin && position.z <= maxZ + safeMargin;
            return insidePreparedEnvelope && !IsInsidePlayerTerritory(position);
        }

        private static bool IsInsidePlayerTerritory(Vector3 position)
        {
            int playerTeam = SistemaGovernoMundial.Instancia != null
                ? Mathf.Max(1, SistemaGovernoMundial.Instancia.teamJogador)
                : 1;
            MarcadorTerritorio[] markers = UnityEngine.Object.FindObjectsByType<MarcadorTerritorio>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                MarcadorTerritorio marker = markers[i];
                if (marker == null) continue;
                IdentidadeUnidade identity = marker.GetComponent<IdentidadeUnidade>();
                int teamId = identity != null ? identity.teamID : marker.teamID;
                if (teamId != playerTeam) continue;
                float radius = Mathf.Max(0f, marker.raioDeDominio);
                Vector3 delta = position - marker.transform.position;
                if (Mathf.Abs(delta.x) <= radius && Mathf.Abs(delta.z) <= radius) return true;
            }
            return false;
        }

        public bool TryResolveConstructionAnchor(IA02IntentType intent, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            // Os creates "IA02 Local - ..." sao os pontos oficiais do pais.
            // Nem todas as cenas possuem o componente opcional
            // IA02ConstructionAnchors, entao resolvemos o slot pelo mesmo id
            // usado no plano de construcao. Isso impede a IA de procurar um lote
            // livre ou construir em outra regiao quando o create ja existe.
            string slotId = ResolveLocalSlotId(intent);
            if (intent == IA02IntentType.BuildOffshorePlatform
                && TryResolvePlatformSlot(out position, out rotation, out string platformSlotId))
            {
                if (loggedAnchorResolutions.Add(intent))
                {
                    UnityEngine.Debug.Log("[IA02 Anchor] " + intent + " -> " + platformSlotId + " pos=" + position.ToString("F2"));
                }
                return true;
            }
            IA02BuildSlot slot;
            IA02CityLayout resolvedLayout = ResolveCityLayoutForSlot(slotId);
            if (!string.IsNullOrWhiteSpace(slotId) && resolvedLayout != null && resolvedLayout.TryGetSlot(slotId, out slot) && IsOwnedLayoutSlot(resolvedLayout, slot))
            {
                Transform point = slot.BuildingPoint != null ? slot.BuildingPoint : slot.transform;
                position = point.position;
                rotation = point.rotation;
                if (loggedAnchorResolutions.Add(intent))
                {
                    UnityEngine.Debug.Log("[IA02 Anchor] " + intent + " -> " + slot.name + " (" + slot.SlotId + ") pos=" + position.ToString("F2"));
                }
                return true;
            }

            // Se existe um slot preparado para este intento, nunca caia em uma
            // ancora legada ou em um lote livre de outra regiao. Uma falha de
            // planejamento e segura; uma construcao fora do territorio mistura
            // as faccoes e corrompe a partida.
            if (!string.IsNullOrWhiteSpace(slotId) && (resolvedLayout != null || HasPreparedSlotInScene(slotId)))
            {
                if (loggedAnchorResolutions.Add(intent))
                {
                    UnityEngine.Debug.LogWarning("[IA02 Anchor] Slot rejeitado por pertencer a outro territorio: " + slotId + " | team=" + TeamId);
                }
                return false;
            }

            // Compatibilidade com cenas antigas: somente usa as referencias
            // legadas se o create oficial nao existir. Essas referencias podem
            // estar preenchidas no prefab, mas nunca devem sobrescrever um
            // create diferente configurado na cena.
            IA02ConstructionAnchors anchors = ConstructionAnchors;
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
                        UnityEngine.Debug.Log("[IA02 Anchor] " + intent + " -> legado (slot " + slotId + " indisponivel) pos=" + position.ToString("F2"));
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
            string[] ids = { "ia02.local.plataforma.a", "ia02.local.plataforma.b", "ia02.local.plataforma.c" };
            int start = Mathf.Abs((matchSeed * 31) + TeamId) % ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                string candidateId = ids[(start + i) % ids.Length];
                IA02CityLayout layout = ResolveCityLayoutForSlot(candidateId);
                if (layout == null || !layout.TryGetSlot(candidateId, out IA02BuildSlot slot) || !IsOwnedLayoutSlot(layout, slot))
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

        private IA02CityLayout ResolveCityLayoutForSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return null;
            IA02CityLayout candidate = cityLayout;
            IA02BuildSlot ignored;
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored) && IsOwnedLayoutSlot(candidate, ignored)) return candidate;

            candidate = GetComponentInChildren<IA02CityLayout>(true);
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored) && IsOwnedLayoutSlot(candidate, ignored)) return candidate;
            candidate = GetComponentInParent<IA02CityLayout>(true);
            if (candidate != null && candidate.TryGetSlot(slotId, out ignored) && IsOwnedLayoutSlot(candidate, ignored)) return candidate;

            // Algumas cenas antigas mantêm o layout como irmao do controlador.
            // Escolhe o layout que realmente possui o create solicitado e que
            // esta mais proximo desta IA, evitando pegar o create de outro pais.
            IA02CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA02CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < layouts.Length; i++)
            {
                IA02CityLayout layout = layouts[i];
                if (layout == null || !layout.TryGetSlot(slotId, out ignored) || !IsOwnedLayoutSlot(layout, ignored)) continue;
                float distance = (layout.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    candidate = layout;
                }
            }
            return candidate;
        }

        private bool IsOwnedLayoutSlot(IA02CityLayout layout, IA02BuildSlot slot)
        {
            if (layout == null || slot == null) return false;
            int ownerTeam = TeamId;
            if (ownerTeam <= 0) return false;
            if (layout.OwnerTeamId > 0 && layout.OwnerTeamId != ownerTeam) return false;
            if (slot.OwnerTeamId > 0 && slot.OwnerTeamId != ownerTeam) return false;
            return true;
        }

        private bool HasPreparedSlotInScene(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return false;
            IA02CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA02CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < layouts.Length; i++)
            {
                IA02CityLayout layout = layouts[i];
                if (layout != null && layout.TryGetSlot(slotId, out IA02BuildSlot slot) && slot != null) return true;
            }
            return false;
        }

        private static string ResolveLocalSlotId(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.EstablishCapital: return "ia02.local.prefeitura_01";
                case IA02IntentType.BuildEnergy: return "ia02.local.energia_01";
                case IA02IntentType.BuildFoodProduction: return "ia02.local.fazenda_01";
                case IA02IntentType.BuildStorage: return "ia02.local.armazem_01";
                case IA02IntentType.BuildVehicleConstructor: return "ia02.local.construtor_veiculos";
                case IA02IntentType.BuildMilitaryAirport: return "ia02.local.aeroporto_militar";
                case IA02IntentType.BuildCommercialAirport: return "ia02.local.aeroporto_comercial";
                case IA02IntentType.BuildShipyard: return "ia02.local.estaleiro";
                case IA02IntentType.BuildPier: return "ia02.local.pier";
                case IA02IntentType.BuildOffshorePlatform: return "ia02.local.plataforma.a";
                case IA02IntentType.BuildMilitaryTent: return "ia02.local.tenda";
                case IA02IntentType.BuildStarterHouse: return "ia02.local.casa";
                case IA02IntentType.BuildMediumApartment: return "ia02.local.apartamento_medio";
                case IA02IntentType.BuildHighApartment: return "ia02.local.apartamento_alto";
                default: return string.Empty;
            }
        }

        private void Awake()
        {
            // O manager é persistente. O controller ligado à cena não é:
            // mantê-lo vivo deixava um layout antigo apontando para a cena
            // destruída e duplicava a IA ao voltar do menu para a campanha.
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
                if (!IsWorldReady(out _)) return;
                ExecuteSlice(IA02WorkBudget.Create(3f, 4, 2, true, false));
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

        public void Initialize(IA02RuntimeContext suppliedContext)
        {
            context = suppliedContext ?? context;
            EnsureBootstrap(false);
        }

        public void MarkDirty(IA02DirtyReason reason)
        {
            EnsureBootstrap(false);
            context?.MarkDirty(reason);
        }

        public IA02WorkResult ExecuteSlice(IA02WorkBudget budget)
        {
            if (!IsEnabled || budget.MaxOperations <= 0 && budget.MaxEvents <= 0)
            {
                return lastExecutionResult = IA02WorkResult.Empty("disabled_or_empty_budget");
            }

            if (attachedManager == null && autoRegisterWithManager)
            {
                RegisterWithManager();
            }

            if (!HasProductionAuthority)
            {
                lastExecutionMessage = "IA02 ProductionAuthority bloqueada por outro sistema.";
                return lastExecutionResult = IA02WorkResult.Empty("production_authority_not_granted");
            }

            string worldNotReadyReason;
            if (!IsWorldReady(out worldNotReadyReason))
            {
                lastExecutionMessage = "IA02 WorldNotReady: " + worldNotReadyReason;
                return lastExecutionResult = IA02WorkResult.Empty("world_not_ready:" + worldNotReadyReason);
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

            bool runtimeDeferred = false;
            if (nationRuntime != null && operations < budget.MaxOperations)
            {
                nationRuntime.Economy.Refresh(country);
                float runtimeBudgetMs = Mathf.Max(0.10f, budget.MaxMilliseconds - (float)sliceStopwatch.Elapsed.TotalMilliseconds);
                int runtimeOperations = nationRuntime.Execute(Time.unscaledTime, budget.MaxOperations - operations, restoredFromSave, runtimeBudgetMs);
                runtimeDeferred = nationRuntime.LastExecuteDeferred;
                if (!runtimeDeferred)
                {
                    restoredFromSave = false;
                }
                operations += runtimeOperations;
                changed |= runtimeOperations > 0;
            }

            // O suporte estratégico é opcional e só trabalha quando alguma
            // integração futura foi explicitamente ativada no Inspector.
            if (!runtimeDeferred && strategicSupport != null
                && (strategicSupport.Options.BallisticEnabled
                    || strategicSupport.Options.EnableStrategicLeaderIntegration
                    || strategicSupport.Options.EnableCountryTransferOperation)
                && operations < budget.MaxOperations)
            {
                operations += strategicSupport.ProcessSlice(Time.unscaledTime, budget.MaxOperations - operations);
            }

            if (!runtimeDeferred && operations < budget.MaxOperations)
            {
                operations += gameStateBridge.Refresh(context, budget.MaxOperations - operations);
                changed |= gameStateBridge.LastChangedResources > 0;
            }

            if (!runtimeDeferred)
            {
                operations += context.AdvanceMaintenance(Time.unscaledTime);
            }
            List<IA02DirtyReason> dirty = runtimeDeferred ? null : context.ConsumeDirtyReasons();
            lastDirtyCount = dirty != null ? dirty.Count : 0;
            changed |= dirty != null && dirty.Count > 0;
            context.SetMetric("ia02.last_slice_ms", sliceStopwatch.Elapsed.TotalMilliseconds);
            context.SetMetric("ia02.last_operations", operations);
            context.SetMetric("ia02.last_events", events);
            lastExecutionMessage = runtimeDeferred
                ? "budget_deferred nation=" + NationId
                : "slice nation=" + NationId + " dirty=" + lastDirtyCount + " cadence=" + (Profile != null ? Profile.ResolveCadence(ExecutionMode, CurrentStage, NationMode) : fallbackCadenceSeconds).ToString("0.000", CultureInfo.InvariantCulture);
            lastExecutionResult = IA02WorkResult.From(!runtimeDeferred, changed, operations, events, (float)sliceStopwatch.Elapsed.TotalMilliseconds, lastExecutionMessage, runtimeDeferred);
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

        public void SetProfile(IA02NationProfile newProfile)
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

        public void ConfigureIdentity(int newNationId, int newTeamId, string newNationName)
        {
            ConfigureIdentity(newNationId, newTeamId, newNationName, null, null, null, null, null);
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
            runtimeProfile = IA02NationProfile.CreateRuntimeFromGovernment(country, matchSeed, difficultyProfileOverride);
            nationRuntime = null;
            EnsureBootstrap(false);
        }

        public SaveIA02NationState CaptureSaveState()
        {
            EnsureBootstrap(false);
            SaveIA02NationState state = context.CaptureSaveState();
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
            state.profileSnapshot = Profile != null ? Profile.CaptureSnapshot() : new IA02NationProfileSnapshot();
            state.foundationSequenceStep = nationRuntime != null ? nationRuntime.FoundationSequenceStatus : string.Empty;
            state.foundationSkippedSteps = nationRuntime != null && nationRuntime.CityPlanner != null
                ? nationRuntime.CityPlanner.CaptureUnavailableSequenceSteps()
                : new System.Collections.Generic.List<string>();
            state.foundationFundingGranted = nationRuntime != null && nationRuntime.FoundationFundingGranted;
            state.buildPlanState = nationRuntime != null && nationRuntime.BuildPlanRuntime != null
                ? nationRuntime.BuildPlanRuntime.CaptureSaveState()
                : new SaveIA02BuildPlanState();
            return state;
        }

        public void RestoreFromSaveState(SaveIA02NationState state)
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
            runtimeProfile = ScriptableObject.CreateInstance<IA02NationProfile>();
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

        public void ApplyServiceDiagnostics(IA02ServiceDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null) return;
            string fingerprint = (snapshot.Report ?? string.Empty) + "|" + snapshot.DifficultyCode;
            if (fingerprint == lastServiceFingerprint) return;
            EnsureBootstrap(false);
            context.ApplyServiceSnapshot(snapshot);
            lastServiceFingerprint = fingerprint;
        }

        public void AttachManager(IA02Manager manager)
        {
            attachedManager = manager;
            SetSharedEventBus(manager != null ? manager.EventBus : null);
        }

        public void DetachManager(IA02Manager manager)
        {
            if (manager != null && attachedManager != manager) return;
            attachedManager = null;
            SetSharedEventBus(null);
        }

        public void SetSharedEventBus(IA02EventBus bus)
        {
            if (sharedEventBus == bus) return;
            if (sharedEventBus != null && subscribedNationId > 0) sharedEventBus.UnsubscribeNation(subscribedNationId, HandleRuntimeEvent);
            sharedEventBus = bus;
            subscribedNationId = 0;
            RefreshEventBusSubscription();
        }

        public int PublishEvent(string topic, string message, object payload = null, IA02EventSeverity severity = IA02EventSeverity.Info)
        {
            if (sharedEventBus == null) return 0;
            return sharedEventBus.Publish(new IA02RuntimeEvent { NationId = NationId, TeamId = TeamId, SourceInstanceId = InstanceId, Topic = topic ?? string.Empty, Message = message ?? string.Empty, Payload = payload, Severity = severity, TimeStamp = Time.unscaledTime });
        }

        public IA02BallisticThreatRecord RegisterBallisticImpact(Vector3 impactPosition, Vector3 predictedTargetPosition, Vector3 arrivalDirection, Vector3 probableLaunchArea, IA02BallisticMissileType missileType, IA02BallisticWarheadType warheadType, int launchCount, float damage, string infrastructureHit, int suspectedCountryId = 0, int confirmedCountryId = 0, Vector3 knownLaunchPosition = default(Vector3), bool launchPositionKnown = false, float authorshipConfidence = 0f)
        {
            EnsureBootstrap(false);
            return strategicSupport != null
                ? strategicSupport.RegisterBallisticImpact(impactPosition, predictedTargetPosition, arrivalDirection, probableLaunchArea, missileType, warheadType, launchCount, damage, infrastructureHit, suspectedCountryId, confirmedCountryId, knownLaunchPosition, launchPositionKnown, authorshipConfidence, Time.unscaledTime)
                : null;
        }

        public bool RegisterLeaderEvent(IA02LeaderEventType eventType, string leaderId, Vector3 position, float confidence, int relatedCountryId = 0, string regionId = null, string buildingId = null, string vehicleId = null)
        {
            EnsureBootstrap(false);
            return strategicSupport != null && strategicSupport.RegisterLeaderEvent(eventType, leaderId, position, confidence, Time.unscaledTime, relatedCountryId, regionId, buildingId, vehicleId);
        }

        public bool BeginCountryTransfer(int winnerCountryId, int defeatedCountryId, int regions, int cities, int structures, int resources, int units)
        {
            EnsureBootstrap(false);
            return strategicSupport != null && strategicSupport.BeginCountryTransfer(winnerCountryId, defeatedCountryId, regions, cities, structures, resources, units);
        }

        public IA02WorldEntityRecord BuildWorldRecord()
        {
            EnsureBootstrap(false);
            return new IA02WorldEntityRecord { EntityId = UniqueEntityId, InstanceId = InstanceId, NationId = NationId, TeamId = TeamId, DisplayName = NationName, Kind = IA02WorldEntityKind.Controller, Domain = IA02WorldDomain.Command, Category = "controller", RegionKey = "nation:" + NationId, Position = transform.position, Operational = isActiveAndEnabled, Version = context.Version, State = context.BuildDebugSummary(), Source = "IA02Controller" };
        }

        public void EnsureBootstrap(bool forceProfileRebuild)
        {
            if (context == null) context = new IA02RuntimeContext();
            if (strategicOptions == null) strategicOptions = new IA02StrategicOptions();
            if (runtimeProfile == null || forceProfileRebuild)
            {
                runtimeProfile = profileAsset != null ? profileAsset.CloneForRuntime(context.GetIdentitySnapshot()) : (createRuntimeProfileWhenMissing ? ScriptableObject.CreateInstance<IA02NationProfile>() : null);
                if (runtimeProfile != null) runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            DadosPaisGoverno country = null;
            if (autoApplyGovernmentSnapshot && SistemaGovernoMundial.Instancia != null) country = SistemaGovernoMundial.Instancia.ObterPais(ResolveTeamId());
            if (country != null && runtimeProfile != null) runtimeProfile.ApplyGovernmentBias(country, difficultyProfileOverride);
            IA02NationIdentity identity = runtimeProfile != null
                ? runtimeProfile.BuildIdentity(InstanceId, ResolveNationId(), ResolveTeamId(), matchSeed, executionModeOverride, nationModeOverride, stageOverride, postureOverride, nationNameOverride, presidentNameOverride, currencyNameOverride, currencySymbolOverride, countryProfileOverride, difficultyProfileOverride)
                : new IA02NationIdentity { InstanceId = InstanceId, NationId = ResolveNationId(), TeamId = ResolveTeamId(), NationName = ResolveNationName(), PresidentName = ResolvePresidentName(), CurrencyName = "Credit", CurrencySymbol = "$", CountryProfile = "Neutral", DifficultyProfile = "normal", RandomSeed = matchSeed + ResolveNationId(), ExecutionMode = executionModeOverride, NationMode = nationModeOverride, CurrentStage = stageOverride, CurrentPosture = postureOverride };
            context.ApplyIdentity(identity);
            if (cityLayout == null) cityLayout = GetComponentInChildren<IA02CityLayout>(true);
            if (constructionAnchors == null) constructionAnchors = GetComponentInChildren<IA02ConstructionAnchors>(true);
            cityLayout?.ConfigureOwner(identity.TeamId, identity.NationId);
            // EnsureBootstrap e chamado tambem pelas verificacoes de prontidao
            // do scheduler. Reaplicar o mesmo snapshot a cada slice marcava o
            // contexto como dirty continuamente e mantinha a IA em loop de
            // trabalho, mesmo quando o pais nao havia mudado.
            if (country != null)
            {
                string governmentFingerprint = BuildGovernmentFingerprint(country);
                if (!string.Equals(governmentFingerprint, lastGovernmentFingerprint, StringComparison.Ordinal))
                {
                    context.ApplyGovernmentSnapshot(country);
                    lastGovernmentFingerprint = governmentFingerprint;
                }
            }
            uniqueEntityId = "ia02:" + identity.NationId + ":" + identity.TeamId + ":" + InstanceId;
            if (nationRuntime == null) nationRuntime = new IA02NationRuntime(this, context, runtimeProfile);
            if (strategicSupport == null) strategicSupport = new IA02StrategicSupport(identity.NationId, strategicOptions);
            RefreshEventBusSubscription();
            // O resumo completo ja e renovado ao final de cada slice. Evite
            // criar uma nova string em toda chamada de EnsureBootstrap, que
            // tambem acontece nas verificacoes de prontidao do scheduler.
            if (string.IsNullOrEmpty(runtimeSummary))
            {
                runtimeSummary = BuildRuntimeSummary();
            }
        }

        private void RegisterWithManager()
        {
            IA02Manager manager = IA02Manager.Instancia;
            if (manager != null) manager.RegisterController(this);
        }

        private void RefreshEventBusSubscription()
        {
            if (sharedEventBus == null || NationId <= 0 || subscribedNationId == NationId) return;
            if (subscribedNationId > 0) sharedEventBus.UnsubscribeNation(subscribedNationId, HandleRuntimeEvent);
            subscribedNationId = NationId;
            sharedEventBus.SubscribeNation(subscribedNationId, HandleRuntimeEvent);
        }

        private void HandleRuntimeEvent(IA02RuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null || runtimeEvent.NationId > 0 && runtimeEvent.NationId != NationId) return;
            pendingEventCount++;
            if (runtimeEvent.Severity >= IA02EventSeverity.Warning) context.MarkDirty(IA02DirtyReason.ExternalEvent);
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
