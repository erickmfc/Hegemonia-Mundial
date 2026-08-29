using System;
using System.Collections.Generic;
using Hegemonia.AI.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using Hegemonia.RTS;
using Hegemonia.AI.Sovereign;

namespace Hegemonia.AI.IA02
{
    [DefaultExecutionOrder(-940)]
    public sealed class IA02Manager : MonoBehaviour
    {
        private static IA02Manager instance;

        [Header("Auto Bind")]
        [SerializeField] private bool autoBindSceneControllers = true;
        [SerializeField] private bool autoResolveIdentityCollisions = true;
        [SerializeField] private bool autoSpawnMissingControllersFromSave = true;
        [SerializeField] private bool autoSpawnFromGovernment = false;

        [Header("Runtime")]
        [SerializeField] private float frameBudgetMilliseconds = 1.5f;
        [SerializeField] private float serviceRefreshInterval = 1f;
        [SerializeField] private float summaryRefreshInterval = 0.25f;
        [SerializeField] private int matchSeed = 1;
        [Header("Agendamento global")]
        [SerializeField] private bool usarOrquestradorGlobal = true;
        [SerializeField, Min(0.25f)] private float frequenciaEstrategicaGlobal = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool logSummary;
        [TextArea(3, 12)]
        [SerializeField] private string runtimeSummary = string.Empty;

        private readonly List<IA02Controller> controllers = new List<IA02Controller>(8);
        private readonly IA02Scheduler scheduler = new IA02Scheduler();
        private readonly IA02Telemetry telemetry = new IA02Telemetry();
        private readonly IA02WorldRegistry worldRegistry = new IA02WorldRegistry();
        private readonly IA02ServiceDiagnostics serviceDiagnostics = new IA02ServiceDiagnostics();
        private readonly List<SaveIA02NationState> saveBuffer = new List<SaveIA02NationState>(8);
        private readonly List<IA02Controller> executionBuffer = new List<IA02Controller>(8);
        private readonly StringBuilder summaryBuilder = new StringBuilder(512);
        private readonly Dictionary<int, ProductionAuthorityClaim> productionAuthorityClaims = new Dictionary<int, ProductionAuthorityClaim>(8);

        private const int IA02ProductionAuthorityPriority = 400;

        private struct ProductionAuthorityClaim
        {
            public int TeamId;
            public string OwnerKey;
        }

        private float nextServiceRefreshAt;
        private float nextRuntimeSummaryAt;
        private float nextSliceRecordRefreshAt;
        private IA02SchedulerPlan lastPlan = new IA02SchedulerPlan();
        private bool worldReady;
        private string worldReadyReason = "aguardando inicialização do mundo";
        private string lastWorldReadyLogReason = string.Empty;
        private const string GlobalTaskId = "ia/ia02/manager";
        private bool registradoNoOrquestrador;

        public static IA02Manager Instancia
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

#if UNITY_2023_1_OR_NEWER
                instance = UnityEngine.Object.FindFirstObjectByType<IA02Manager>();
#else
                instance = UnityEngine.Object.FindObjectOfType<IA02Manager>();
#endif
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("IA02Manager_Runtime");
                    instance = go.AddComponent<IA02Manager>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        public static bool TryGetInstance(out IA02Manager manager)
        {
            manager = instance;
            if (manager != null)
            {
                return true;
            }

#if UNITY_2023_1_OR_NEWER
            manager = UnityEngine.Object.FindFirstObjectByType<IA02Manager>();
#else
            manager = UnityEngine.Object.FindObjectOfType<IA02Manager>();
#endif
            if (manager != null)
            {
                instance = manager;
                return true;
            }

            return false;
        }

        public IReadOnlyList<IA02Controller> Controllers => controllers;
        public IA02EventBus EventBus { get; } = new IA02EventBus(256);
        public IA02WorldRegistry WorldRegistry => worldRegistry;
        public IA02Telemetry Telemetry => telemetry;
        public IA02ServiceDiagnosticsSnapshot ServiceSnapshot => serviceDiagnostics.Snapshot;
        public IA02SchedulerPlan LastPlan => lastPlan;
        public string RuntimeSummary => runtimeSummary;
        public int MatchSeed => matchSeed;
        public bool WorldReady => worldReady;
        public string WorldReadyReason => worldReadyReason;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // O manager persistente da partida continua sendo a autoridade
                // durante a troca de cenas. Desative o duplicado imediatamente
                // para que ele não execute ticks nem seja escolhido por buscas
                // de cena antes do Destroy realmente sair do frame.
                enabled = false;
                // Em algumas cenas o manager local compartilha o root com o
                // controller e o IA02CityLayout. Remover o GameObject inteiro
                // destruiria a infraestrutura válida da campanha.
                bool ownsSceneInfrastructure = GetComponentInChildren<IA02Controller>(true) != null
                    || GetComponentInChildren<IA02CityLayout>(true) != null;
                if (ownsSceneInfrastructure)
                {
                    Destroy(this);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshServiceDiagnostics(true);
            if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
            {
                return;
            }
            if (autoBindSceneControllers)
            {
                BindSceneControllers();
            }
            if (autoSpawnFromGovernment)
            {
                SpawnConfiguredGovernmentControllers();
            }
        }

        private void OnEnable()
        {
            RegistrarNoOrquestradorGlobal();
            RefreshServiceDiagnostics(true);
            if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
            {
                return;
            }
            if (autoBindSceneControllers)
            {
                BindSceneControllers();
            }
            if (autoSpawnFromGovernment)
            {
                SpawnConfiguredGovernmentControllers();
            }
        }

        private void OnDisable()
        {
            RemoverDoOrquestradorGlobal();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            controllers.Clear();
            executionBuffer.Clear();
            ReleaseAllProductionAuthorities();
            RemoverDoOrquestradorGlobal();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            worldReady = false;
            worldReadyReason = "cena alterada; aguardando novo layout";
            lastWorldReadyLogReason = string.Empty;
            if (ConfiguracaoCenasJogo.EhCenaDeMenu(scene.name))
            {
                return;
            }
            if (autoBindSceneControllers)
            {
                BindSceneControllers();
            }
            if (autoSpawnFromGovernment)
            {
                SpawnConfiguredGovernmentControllers();
            }

            RefreshServiceDiagnostics(true);
        }

        private void Update()
        {
            if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
            {
                return;
            }

            if (registradoNoOrquestrador)
            {
                return;
            }

            ExecuteTick(Time.unscaledTime, Time.unscaledDeltaTime * 1000f, frameBudgetMilliseconds);
        }

        private void RegistrarNoOrquestradorGlobal()
        {
            if (!Application.isPlaying || !usarOrquestradorGlobal || registradoNoOrquestrador)
            {
                return;
            }

            OrquestradorGlobalSimulacao global = OrquestradorGlobalSimulacao.Instancia;
            if (global == null || !global.HabilitarEstrategica)
            {
                return;
            }

            registradoNoOrquestrador = global.Registrar(
                GlobalTaskId,
                0,
                CamadaSimulacao.Estrategica,
                Mathf.Max(0.25f, frequenciaEstrategicaGlobal),
                1.25f,
                ExecutarTickGlobal,
                Time.unscaledTime);
        }

        private void RemoverDoOrquestradorGlobal()
        {
            if (!registradoNoOrquestrador)
            {
                return;
            }

            OrquestradorGlobalSimulacao.Instancia?.Remover(GlobalTaskId);
            registradoNoOrquestrador = false;
        }

        private bool ExecutarTickGlobal(float agora)
        {
            if (!isActiveAndEnabled || ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
            {
                return true;
            }

            ExecuteTick(agora, Time.unscaledDeltaTime * 1000f, frameBudgetMilliseconds);
            return true;
        }

        public void RegisterController(IA02Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!controllers.Contains(controller))
            {
                controllers.Add(controller);
            }

            worldReady = false;

            controller.EnsureBootstrap(false);
            controller.AttachManager(this);
            if (autoResolveIdentityCollisions)
            {
                ResolveIdentityCollisionIfNeeded(controller);
            }

            ClaimProductionAuthority(controller);

            RefreshControllerRecords(controller);
            telemetry.RegisterController(controller);
            controller.ApplyServiceDiagnostics(ServiceSnapshot);
        }

        public void UnregisterController(IA02Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            controllers.Remove(controller);
            ReleaseProductionAuthority(controller);
            worldReady = false;
            scheduler.Unregister(controller.InstanceId);
            telemetry.UnregisterController(controller.InstanceId);
            worldRegistry.Remove(controller.UniqueEntityId);
            controller.DetachManager(this);
        }

        public bool HasProductionAuthority(IA02Controller controller)
        {
            if (controller == null)
            {
                return false;
            }

            ProductionAuthorityClaim claim;
            if (!productionAuthorityClaims.TryGetValue(controller.GetInstanceID(), out claim))
            {
                return false;
            }

            return AIControlAuthority.CanIssue(claim.TeamId, claim.OwnerKey);
        }

        private void ClaimProductionAuthority(IA02Controller controller)
        {
            if (controller == null || controller.TeamId <= 1)
            {
                return;
            }

            int instanceId = controller.GetInstanceID();
            string ownerKey = "IA02Production:" + controller.TeamId + ":" + instanceId;
            AIControlAuthority.Claim(controller.TeamId, ownerKey, IA02ProductionAuthorityPriority);
            productionAuthorityClaims[instanceId] = new ProductionAuthorityClaim
            {
                TeamId = controller.TeamId,
                OwnerKey = ownerKey
            };
        }

        private void ReleaseProductionAuthority(IA02Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            int instanceId = controller.GetInstanceID();
            ProductionAuthorityClaim claim;
            if (!productionAuthorityClaims.TryGetValue(instanceId, out claim))
            {
                return;
            }

            AIControlAuthority.Release(claim.TeamId, claim.OwnerKey);
            productionAuthorityClaims.Remove(instanceId);
        }

        private void ReleaseAllProductionAuthorities()
        {
            foreach (ProductionAuthorityClaim claim in productionAuthorityClaims.Values)
            {
                AIControlAuthority.Release(claim.TeamId, claim.OwnerKey);
            }

            productionAuthorityClaims.Clear();
        }

        public int ExecuteTick(float now, float frameMs, float frameBudgetOverrideMs = -1f)
        {
            bool measureTick = DiagnosticoDesempenhoJogo.CapturaAtiva;
            float tickStartedAt = measureTick ? Time.realtimeSinceStartup : 0f;
            PruneInvalidControllers();
            if (autoBindSceneControllers && controllers.Count == 0)
            {
                BindSceneControllers();
            }

            RefreshServiceDiagnostics(false, now);

            if (!TryPrepareWorld(out string notReadyReason))
            {
                ReportWorldNotReady(notReadyReason);
                RefreshRuntimeSummaryIfDue(null, now);
                telemetry.RecordFrame(frameMs);
                ReportTickCost(measureTick, tickStartedAt);
                return 0;
            }

            float budgetMs = frameBudgetOverrideMs > 0f ? frameBudgetOverrideMs : frameBudgetMilliseconds;
            IA02SchedulerPlan plan = scheduler.BuildPlan(controllers, now, budgetMs);
            lastPlan = plan;

            float executionStartedAt = Time.realtimeSinceStartup;
            executionBuffer.Clear();
            for (int i = 0; i < plan.Slices.Count; i++)
            {
                if ((Time.realtimeSinceStartup - executionStartedAt) * 1000f >= budgetMs)
                {
                    break;
                }

                IA02ScheduledSlice slice = plan.Slices[i];
                if (slice == null || slice.Controller == null)
                {
                    continue;
                }

                executionBuffer.Add(slice.Controller);
                IA02WorkResult result = slice.Controller.ExecuteSlice(slice.Budget);
                scheduler.ReportExecution(slice.Controller, result, now);
                // O registro de cada controller nao precisa acompanhar todo
                // slice. Atualize no maximo um registro por janela curta e
                // deixe a varredura completa para RefreshServiceDiagnostics.
                if (Time.unscaledTime >= nextSliceRecordRefreshAt)
                {
                    RefreshControllerRecords(slice.Controller);
                    nextSliceRecordRefreshAt = Time.unscaledTime + 0.10f;
                }

                int registryEntries = worldRegistry.CountByNation(slice.Controller.NationId);
                telemetry.RecordSlice(slice.Controller, result, registryEntries, slice.Controller.LastDirtyCount);
            }

            telemetry.RecordFrame(frameMs);
            bool summaryRefreshed = RefreshRuntimeSummaryIfDue(plan, now);

            if (logSummary && summaryRefreshed)
            {
                Debug.Log("[IA02Manager] " + runtimeSummary);
            }

            ReportTickCost(measureTick, tickStartedAt);
            return executionBuffer.Count;
        }

        public List<SaveIA02NationState> CaptureSaveStates()
        {
            saveBuffer.Clear();
            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                saveBuffer.Add(controller.CaptureSaveState());
            }

            saveBuffer.Sort(CompareSaveStates);
            return new List<SaveIA02NationState>(saveBuffer);
        }

        public void RestoreSaveStates(IReadOnlyList<SaveIA02NationState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                SaveIA02NationState state = states[i];
                if (state == null)
                {
                    continue;
                }

                IA02Controller controller = FindMatchingController(state);
                if (controller == null && autoSpawnMissingControllersFromSave)
                {
                    controller = SpawnControllerFromSave(state);
                }

                if (controller == null)
                {
                    continue;
                }

                controller.RestoreFromSaveState(state);
                RegisterController(controller);
            }

            RefreshRuntimeSummaryIfDue(lastPlan, Time.unscaledTime, true);
        }

        public IA02Controller CreateControllerFromGovernment(global::DadosPaisGoverno country)
        {
            if (country == null)
            {
                return null;
            }

            GameObject go = new GameObject("IA02 " + (string.IsNullOrWhiteSpace(country.nomePais) ? country.teamId.ToString() : country.nomePais));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.transform.SetParent(transform, false);
            IA02Controller controller = go.AddComponent<IA02Controller>();
            controller.ConfigureFromGovernment(country, matchSeed, ServiceSnapshot.DifficultyCode);
            controller.ConfigureForAutonomousRuntime();
            RegisterController(controller);
            return controller;
        }

        public IA02Controller FindControllerByNationId(int nationIdValue)
        {
            if (nationIdValue <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller != null && controller.NationId == nationIdValue)
                {
                    return controller;
                }
            }

            return null;
        }

        public IA02Controller FindControllerByInstanceId(int instanceId)
        {
            if (instanceId <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller != null && controller.InstanceId == instanceId)
                {
                    return controller;
                }
            }

            return null;
        }

        public IA02Controller FindControllerByTeamId(int teamIdValue)
        {
            if (teamIdValue <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller != null && controller.TeamId == teamIdValue)
                {
                    return controller;
                }
            }

            return null;
        }

        private void BindSceneControllers()
        {
            IA02Controller[] found = UnityEngine.Object.FindObjectsByType<IA02Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                RegisterController(found[i]);
            }
        }

        private void PruneInvalidControllers()
        {
            for (int i = controllers.Count - 1; i >= 0; i--)
            {
                if (controllers[i] == null)
                {
                    controllers.RemoveAt(i);
                }
            }
        }

        private bool TryPrepareWorld(out string reason)
        {
            worldReady = false;
            reason = string.Empty;

            global::SistemaGovernoMundial government = global::SistemaGovernoMundial.Instancia;
            if (Application.isPlaying && government == null)
            {
                reason = "governo mundial ainda não inicializado";
                worldReadyReason = reason;
                return false;
            }

            if (Application.isPlaying && (government.Paises == null || government.Paises.Count == 0))
            {
                reason = "lista de países ainda não carregada";
                worldReadyReason = reason;
                return false;
            }

            if (controllers.Count == 0)
            {
                reason = "nenhum controller IA02 registrado na cena";
                worldReadyReason = reason;
                return false;
            }

            bool activeControllerFound = false;
            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller == null) continue;
                if (!controller.isActiveAndEnabled) continue;
                activeControllerFound = true;
                if (!controller.IsWorldReady(out string controllerReason))
                {
                    reason = "team=" + controller.TeamId + ": " + controllerReason;
                    worldReadyReason = reason;
                    return false;
                }
            }

            if (!activeControllerFound)
            {
                reason = "nenhum controller IA02 ativo na cena";
                worldReadyReason = reason;
                return false;
            }

            worldReady = true;
            worldReadyReason = "ok";
            if (lastWorldReadyLogReason != "ready")
            {
                Debug.Log("[IA02 WorldReady] layout, identidade e governo prontos; execução liberada.");
                lastWorldReadyLogReason = "ready";
            }
            return true;
        }

        private void ReportWorldNotReady(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) reason = "motivo não informado";
            if (lastWorldReadyLogReason == reason) return;
            Debug.LogWarning("[IA02 WorldNotReady] " + reason + ". Nenhuma construção ou slice foi executado.");
            lastWorldReadyLogReason = reason;
        }

        private void SpawnConfiguredGovernmentControllers()
        {
            global::SistemaGovernoMundial governo = global::SistemaGovernoMundial.Instancia;
            if (governo == null || governo.Paises == null)
            {
                return;
            }

            for (int i = 0; i < governo.Paises.Count; i++)
            {
                global::DadosPaisGoverno country = governo.Paises[i];
                if (country == null || country.teamId <= 0)
                {
                    continue;
                }

                if (FindControllerByTeamId(country.teamId) != null)
                {
                    continue;
                }

                CreateControllerFromGovernment(country);
            }
        }

        private void RefreshServiceDiagnostics(bool force, float now = 0f)
        {
            if (!force && now < nextServiceRefreshAt)
            {
                return;
            }

            serviceDiagnostics.Refresh();
            telemetry.SetServiceReport(ServiceSnapshot.Report);
            nextServiceRefreshAt = now + Mathf.Max(0.1f, serviceRefreshInterval);

            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.ApplyServiceDiagnostics(ServiceSnapshot);
                RefreshControllerRecords(controller);
            }
        }

        private void RefreshControllerRecords(IA02Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            worldRegistry.Register(worldRegistry.CreateRecordFromController(controller));
        }

        private void ResolveIdentityCollisionIfNeeded(IA02Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            int nationIdValue = controller.NationId;
            int teamIdValue = controller.TeamId;
            if (nationIdValue <= 0 && teamIdValue <= 0)
            {
                nationIdValue = AllocateUniqueNationId();
                teamIdValue = nationIdValue;
                controller.ConfigureIdentity(nationIdValue, teamIdValue);
                return;
            }

            bool collision = false;
            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller other = controllers[i];
                if (other == null || other == controller)
                {
                    continue;
                }

                if (other.NationId == nationIdValue || other.TeamId == teamIdValue)
                {
                    collision = true;
                    break;
                }
            }

            if (!collision)
            {
                return;
            }

            int uniqueNationId = AllocateUniqueNationId();
            controller.ConfigureIdentity(uniqueNationId, uniqueNationId);
        }

        private int AllocateUniqueNationId()
        {
            int candidate = Mathf.Max(1000, matchSeed + 1000);
            for (int i = 0; i < controllers.Count; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                candidate = Mathf.Max(candidate, controller.NationId + 1);
                candidate = Mathf.Max(candidate, controller.TeamId + 1);
            }

            global::SistemaGovernoMundial governo = global::SistemaGovernoMundial.Instancia;
            if (governo != null && governo.Paises != null)
            {
                for (int i = 0; i < governo.Paises.Count; i++)
                {
                    global::DadosPaisGoverno country = governo.Paises[i];
                    if (country != null)
                    {
                        candidate = Mathf.Max(candidate, country.teamId + 1);
                    }
                }
            }

            return candidate;
        }

        private IA02Controller FindMatchingController(SaveIA02NationState state)
        {
            if (state == null)
            {
                return null;
            }

            IA02Controller controller = FindControllerByInstanceId(state.instanceId);
            if (controller != null)
            {
                return controller;
            }

            controller = FindControllerByNationId(state.nationId);
            if (controller != null)
            {
                return controller;
            }

            return FindControllerByTeamId(state.teamId);
        }

        private IA02Controller SpawnControllerFromSave(SaveIA02NationState state)
        {
            GameObject go = new GameObject(BuildSaveControllerName(state));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.transform.SetParent(transform, false);
            go.SetActive(false);
            IA02Controller controller = go.AddComponent<IA02Controller>();
            controller.RestoreFromSaveState(state);
            go.SetActive(true);
            return controller;
        }

        private static string BuildSaveControllerName(SaveIA02NationState state)
        {
            if (state == null)
            {
                return "IA02 Runtime";
            }

            if (!string.IsNullOrWhiteSpace(state.nationName))
            {
                return "IA02 " + state.nationName;
            }

            return "IA02 " + state.teamId;
        }

        private string BuildRuntimeSummary(IA02SchedulerPlan plan)
        {
            summaryBuilder.Clear();
            summaryBuilder.Append("controllers=").Append(controllers.Count);
            summaryBuilder.Append(" scheduled=").Append(plan != null ? plan.ScheduledCount : 0);
            summaryBuilder.Append(" ready=").Append(plan != null ? plan.ReadyCount : 0);
            summaryBuilder.Append(" budgetMs=").Append(plan != null ? plan.FrameBudgetMs.ToString("0.000") : frameBudgetMilliseconds.ToString("0.000"));
            summaryBuilder.Append(" frameMs=").Append(telemetry.LastFrameMs.ToString("0.000"));
            summaryBuilder.Append(" avgMs=").Append(telemetry.AverageFrameMs.ToString("0.000"));
            summaryBuilder.Append(" slices=").Append(telemetry.SliceCount);
            summaryBuilder.Append(" events=").Append(telemetry.EventCount);
            summaryBuilder.Append(" worldReady=").Append(worldReady ? "true" : "false");
            if (!worldReady) summaryBuilder.Append(" worldReason=").Append(worldReadyReason ?? string.Empty);
            summaryBuilder.Append(" service=").Append(ServiceSnapshot.Report ?? string.Empty);
            return summaryBuilder.ToString();
        }

        private bool RefreshRuntimeSummaryIfDue(IA02SchedulerPlan plan, float now, bool force = false)
        {
            if (!force && now < nextRuntimeSummaryAt)
            {
                return false;
            }

            runtimeSummary = BuildRuntimeSummary(plan);
            nextRuntimeSummaryAt = now + Mathf.Max(0.1f, summaryRefreshInterval);
            return true;
        }

        private static void ReportTickCost(bool measuring, float startedAt)
        {
            if (measuring)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(
                    "ia02_manager_ms",
                    (Time.realtimeSinceStartup - startedAt) * 1000f);
            }
        }

        private static int CompareSaveStates(SaveIA02NationState left, SaveIA02NationState right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int byNation = left.nationId.CompareTo(right.nationId);
            if (byNation != 0)
            {
                return byNation;
            }

            int byTeam = left.teamId.CompareTo(right.teamId);
            if (byTeam != 0)
            {
                return byTeam;
            }

            return left.instanceId.CompareTo(right.instanceId);
        }
    }
}
