using System;
using System.Collections.Generic;
using Hegemonia.AI.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

namespace Hegemonia.AI.IA01
{
    [DefaultExecutionOrder(-940)]
    public sealed class IA01Manager : MonoBehaviour
    {
        private static IA01Manager instance;

        [Header("Auto Bind")]
        [SerializeField] private bool autoBindSceneControllers = true;
        [SerializeField] private bool autoResolveIdentityCollisions = true;
        [SerializeField] private bool autoSpawnMissingControllersFromSave = true;
        [SerializeField] private bool autoSpawnFromGovernment = false;

        [Header("Runtime")]
        [SerializeField] private float frameBudgetMilliseconds = 1.5f;
        [SerializeField] private float serviceRefreshInterval = 1f;
        [SerializeField] private int matchSeed = 1;

        [Header("Debug")]
        [SerializeField] private bool logSummary;
        [TextArea(3, 12)]
        [SerializeField] private string runtimeSummary = string.Empty;

        private readonly List<IA01Controller> controllers = new List<IA01Controller>(8);
        private readonly IA01Scheduler scheduler = new IA01Scheduler();
        private readonly IA01Telemetry telemetry = new IA01Telemetry();
        private readonly IA01WorldRegistry worldRegistry = new IA01WorldRegistry();
        private readonly IA01ServiceDiagnostics serviceDiagnostics = new IA01ServiceDiagnostics();
        private readonly List<SaveIA01NationState> saveBuffer = new List<SaveIA01NationState>(8);
        private readonly List<IA01Controller> executionBuffer = new List<IA01Controller>(8);
        private readonly StringBuilder summaryBuilder = new StringBuilder(512);

        private float nextServiceRefreshAt;
        private IA01SchedulerPlan lastPlan = new IA01SchedulerPlan();

        public static IA01Manager Instancia
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

#if UNITY_2023_1_OR_NEWER
                instance = UnityEngine.Object.FindFirstObjectByType<IA01Manager>();
#else
                instance = UnityEngine.Object.FindObjectOfType<IA01Manager>();
#endif
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("IA01Manager_Runtime");
                    instance = go.AddComponent<IA01Manager>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        public static bool TryGetInstance(out IA01Manager manager)
        {
            manager = instance;
            if (manager != null)
            {
                return true;
            }

#if UNITY_2023_1_OR_NEWER
            manager = UnityEngine.Object.FindFirstObjectByType<IA01Manager>();
#else
            manager = UnityEngine.Object.FindObjectOfType<IA01Manager>();
#endif
            if (manager != null)
            {
                instance = manager;
                return true;
            }

            return false;
        }

        public IReadOnlyList<IA01Controller> Controllers => controllers;
        public IA01EventBus EventBus { get; } = new IA01EventBus(256);
        public IA01WorldRegistry WorldRegistry => worldRegistry;
        public IA01Telemetry Telemetry => telemetry;
        public IA01ServiceDiagnosticsSnapshot ServiceSnapshot => serviceDiagnostics.Snapshot;
        public IA01SchedulerPlan LastPlan => lastPlan;
        public string RuntimeSummary => runtimeSummary;
        public int MatchSeed => matchSeed;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshServiceDiagnostics(true);
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
            RefreshServiceDiagnostics(true);
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
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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
            ExecuteTick(Time.unscaledTime, Time.unscaledDeltaTime * 1000f, frameBudgetMilliseconds);
        }

        public void RegisterController(IA01Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!controllers.Contains(controller))
            {
                controllers.Add(controller);
            }

            controller.EnsureBootstrap(false);
            controller.AttachManager(this);
            if (autoResolveIdentityCollisions)
            {
                ResolveIdentityCollisionIfNeeded(controller);
            }

            RefreshControllerRecords(controller);
            telemetry.RegisterController(controller);
            controller.ApplyServiceDiagnostics(ServiceSnapshot);
        }

        public void UnregisterController(IA01Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            controllers.Remove(controller);
            scheduler.Unregister(controller.InstanceId);
            telemetry.UnregisterController(controller.InstanceId);
            worldRegistry.Remove(controller.UniqueEntityId);
            controller.DetachManager(this);
        }

        public int ExecuteTick(float now, float frameMs, float frameBudgetOverrideMs = -1f)
        {
            if (autoBindSceneControllers && controllers.Count == 0)
            {
                BindSceneControllers();
            }

            RefreshServiceDiagnostics(false, now);

            float budgetMs = frameBudgetOverrideMs > 0f ? frameBudgetOverrideMs : frameBudgetMilliseconds;
            IA01SchedulerPlan plan = scheduler.BuildPlan(controllers, now, budgetMs);
            lastPlan = plan;

            executionBuffer.Clear();
            for (int i = 0; i < plan.Slices.Count; i++)
            {
                IA01ScheduledSlice slice = plan.Slices[i];
                if (slice == null || slice.Controller == null)
                {
                    continue;
                }

                executionBuffer.Add(slice.Controller);
                IA01WorkResult result = slice.Controller.ExecuteSlice(slice.Budget);
                scheduler.ReportExecution(slice.Controller, result, now);
                RefreshControllerRecords(slice.Controller);

                int registryEntries = worldRegistry.CountByNation(slice.Controller.NationId);
                telemetry.RecordSlice(slice.Controller, result, registryEntries, slice.Controller.LastDirtyCount);
            }

            telemetry.RecordFrame(frameMs);
            runtimeSummary = BuildRuntimeSummary(plan);

            if (logSummary)
            {
                Debug.Log("[IA01Manager] " + runtimeSummary);
            }

            return executionBuffer.Count;
        }

        public List<SaveIA01NationState> CaptureSaveStates()
        {
            saveBuffer.Clear();
            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                saveBuffer.Add(controller.CaptureSaveState());
            }

            saveBuffer.Sort(CompareSaveStates);
            return new List<SaveIA01NationState>(saveBuffer);
        }

        public void RestoreSaveStates(IReadOnlyList<SaveIA01NationState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                SaveIA01NationState state = states[i];
                if (state == null)
                {
                    continue;
                }

                IA01Controller controller = FindMatchingController(state);
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

            runtimeSummary = BuildRuntimeSummary(lastPlan);
        }

        public IA01Controller CreateControllerFromGovernment(global::DadosPaisGoverno country)
        {
            if (country == null)
            {
                return null;
            }

            GameObject go = new GameObject("IA01 " + (string.IsNullOrWhiteSpace(country.nomePais) ? country.teamId.ToString() : country.nomePais));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.transform.SetParent(transform, false);
            IA01Controller controller = go.AddComponent<IA01Controller>();
            controller.ConfigureFromGovernment(country, matchSeed, ServiceSnapshot.DifficultyCode);
            RegisterController(controller);
            return controller;
        }

        public IA01Controller FindControllerByNationId(int nationIdValue)
        {
            if (nationIdValue <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller != null && controller.NationId == nationIdValue)
                {
                    return controller;
                }
            }

            return null;
        }

        public IA01Controller FindControllerByInstanceId(int instanceId)
        {
            if (instanceId <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller != null && controller.InstanceId == instanceId)
                {
                    return controller;
                }
            }

            return null;
        }

        public IA01Controller FindControllerByTeamId(int teamIdValue)
        {
            if (teamIdValue <= 0)
            {
                return null;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller != null && controller.TeamId == teamIdValue)
                {
                    return controller;
                }
            }

            return null;
        }

        private void BindSceneControllers()
        {
            IA01Controller[] found = UnityEngine.Object.FindObjectsByType<IA01Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                RegisterController(found[i]);
            }
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
                IA01Controller controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.ApplyServiceDiagnostics(ServiceSnapshot);
                RefreshControllerRecords(controller);
            }
        }

        private void RefreshControllerRecords(IA01Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            worldRegistry.Register(worldRegistry.CreateRecordFromController(controller));
        }

        private void ResolveIdentityCollisionIfNeeded(IA01Controller controller)
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
                IA01Controller other = controllers[i];
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
                IA01Controller controller = controllers[i];
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

        private IA01Controller FindMatchingController(SaveIA01NationState state)
        {
            if (state == null)
            {
                return null;
            }

            IA01Controller controller = FindControllerByInstanceId(state.instanceId);
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

        private IA01Controller SpawnControllerFromSave(SaveIA01NationState state)
        {
            GameObject go = new GameObject(BuildSaveControllerName(state));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            go.transform.SetParent(transform, false);
            go.SetActive(false);
            IA01Controller controller = go.AddComponent<IA01Controller>();
            controller.RestoreFromSaveState(state);
            go.SetActive(true);
            return controller;
        }

        private static string BuildSaveControllerName(SaveIA01NationState state)
        {
            if (state == null)
            {
                return "IA01 Runtime";
            }

            if (!string.IsNullOrWhiteSpace(state.nationName))
            {
                return "IA01 " + state.nationName;
            }

            return "IA01 " + state.teamId;
        }

        private string BuildRuntimeSummary(IA01SchedulerPlan plan)
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
            summaryBuilder.Append(" service=").Append(ServiceSnapshot.Report ?? string.Empty);
            return summaryBuilder.ToString();
        }

        private static int CompareSaveStates(SaveIA01NationState left, SaveIA01NationState right)
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
