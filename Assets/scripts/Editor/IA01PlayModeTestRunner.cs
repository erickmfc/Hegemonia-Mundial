using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.EditorTools
{
    [InitializeOnLoad]
    public static class IA01PlayModeTestRunner
    {
        private const string PendingKey = "IA01.Validation.Pending";
        private const string CompletedKey = "IA01.Validation.Completed";
        private const string ExitCodeKey = "IA01.Validation.ExitCode";
        private const string ResultPathKey = "IA01.Validation.ResultPath";
        private const string TracePathKey = "IA01.Validation.TracePath";
        private const string MenuSceneName = "Menu cena";
        private const string SaveFileName = "save_partida.json";
        private const double OverallTimeoutSeconds = 180.0;
        private const double CapitalObservationSeconds = 12.0;
        private const double HudWarmupSeconds = 1.25;

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        private static Type GovernmentSystemType => ResolveType("SistemaGovernoMundial");

        private static bool s_updateHooked;
        private static bool s_running;
        private static bool s_finished;
        private static bool s_exitQueued;
        private static int s_exitCode;
        private static int s_stage;
        private static double s_stageStartedAt;
        private static double s_overallStartedAt;
        private static string s_resultPath;
        private static string s_tracePath;
        private static readonly StringBuilder s_trace = new StringBuilder(8192);
        private static readonly List<string> s_transitions = new List<string>(32);

        private static object s_manager;
        private static object s_controller;
        private static object s_runtime;
        private static object s_diagnostic;
        private static object s_menuController;
        private static object s_country;
        private static bool s_sawWaitingConfirmation;
        private static int s_maxPendingCommands;
        private static string s_capitalSource = string.Empty;
        private static string s_capitalItemId = string.Empty;
        private static string s_capitalPrefab = string.Empty;
        private static string s_capitalFailure = string.Empty;
        private static string s_reportStatus = string.Empty;

        private enum Stage
        {
            Start = 0,
            LoadMenu = 1,
            WaitMenu = 2,
            StartCampaign = 3,
            WaitWorld = 4,
            ObserveCapital = 5,
            CaptureHud = 6,
            Finish = 7
        }

        private struct PerfSummary
        {
            public float FpsMedio;
            public float FpsMinimo;
            public float CpuMainMs;
            public float GpuMs;
            public int GcGen0;
            public int GcGen1;
            public int GcGen2;
        }

        static IA01PlayModeTestRunner()
        {
            EnsureUpdateHook();
        }

        public static void Run()
        {
            if (SessionState.GetBool(PendingKey, false) || SessionState.GetBool(CompletedKey, false) || s_running)
            {
                Debug.LogWarning("IA01 validation already requested or finished.");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            s_resultPath = Path.Combine(projectRoot, "IA01_PlayModeValidation_report.txt");
            s_tracePath = Path.Combine(projectRoot, "IA01_PlayModeValidation_trace.log");

            DeleteFileIfExists(s_resultPath);
            DeleteFileIfExists(s_tracePath);
            DeleteSaveFile();

            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(CompletedKey, false);
            SessionState.SetInt(ExitCodeKey, 0);
            SessionState.SetString(ResultPathKey, s_resultPath);
            SessionState.SetString(TracePathKey, s_tracePath);

            ResetRuntimeState();
            EnsureUpdateHook();

            TraceLine("IA01 validation requested.");
            EditorApplication.isPlaying = true;
        }

        private static void EnsureUpdateHook()
        {
            if (s_updateHooked)
            {
                return;
            }

            EditorApplication.update += Tick;
            s_updateHooked = true;
        }

        private static void Tick()
        {
            try
            {
                if (SessionState.GetBool(CompletedKey, false) && !EditorApplication.isPlaying)
                {
                    if (!s_exitQueued)
                    {
                        s_exitQueued = true;
                        TraceLine("Validation completed. Exiting editor with code " + s_exitCode + ".");
                        EditorApplication.Exit(s_exitCode);
                    }

                    return;
                }

                if (!SessionState.GetBool(PendingKey, false) && !s_running)
                {
                    return;
                }

                if (!EditorApplication.isPlaying)
                {
                    return;
                }

                if (!s_running)
                {
                    StartRun();
                }

                if (s_finished)
                {
                    return;
                }

                Advance();
            }
            catch (Exception ex)
            {
                Fail("Unexpected exception: " + ex);
            }
        }

        private static void StartRun()
        {
            s_running = true;
            SessionState.SetBool(PendingKey, false);
            s_stage = (int)Stage.Start;
            s_stageStartedAt = EditorApplication.timeSinceStartup;
            s_overallStartedAt = s_stageStartedAt;
            TraceLine("Entered Play Mode.");
        }

        private static void Advance()
        {
            if (EditorApplication.timeSinceStartup - s_overallStartedAt > OverallTimeoutSeconds)
            {
                Fail("Overall timeout reached.");
                return;
            }

            Stage current = (Stage)s_stage;
            if (ElapsedStageTime() > 60.0 && current != Stage.ObserveCapital && current != Stage.CaptureHud)
            {
                Fail("Stage timeout at " + current + ".");
                return;
            }

            switch (current)
            {
                case Stage.Start:
                    SetStage(Stage.LoadMenu, "Preparing fresh campaign.");
                    break;

                case Stage.LoadMenu:
                    if (SceneManager.GetActiveScene().name != MenuSceneName)
                    {
                        TraceLine("Loading menu scene.");
                        SceneManager.LoadScene(MenuSceneName);
                        return;
                    }

                    SetStage(Stage.WaitMenu, "Menu scene active.");
                    break;

                case Stage.WaitMenu:
                    s_menuController = FindMenuController();
                    if (s_menuController == null)
                    {
                        return;
                    }

                    TraceLine("Menu controller found: " + s_menuController.GetType().Name + ".");
                    SetStage(Stage.StartCampaign, "Invoking new campaign.");
                    break;

                case Stage.StartCampaign:
                    if (s_menuController == null)
                    {
                        s_menuController = FindMenuController();
                        if (s_menuController == null)
                        {
                            Fail("MenuInicialController disappeared before campaign start.");
                            return;
                        }
                    }

                    TraceLine("Calling Btn_NovaCampanha().");
                    InvokeInstance(s_menuController, "Btn_NovaCampanha");
                    SetStage(Stage.WaitWorld, "Campaign requested.");
                    break;

                case Stage.WaitWorld:
                    s_manager = FindManager();
                    if (s_manager == null)
                    {
                        return;
                    }

                    s_controller = ResolveController(s_manager);
                    s_runtime = GetRuntime(s_controller);
                    s_country = GetCountry(GetIntMember(s_controller, "TeamId")) ?? GetFirstCountry();
                    if (s_controller == null || s_runtime == null || s_country == null)
                    {
                        return;
                    }

                    AssertSafeProfileDefaults(s_controller);
                    TraceLine("IA01 manager/controller/runtime resolved.");
                    SetStage(Stage.ObserveCapital, "World ready.");
                    break;

                case Stage.ObserveCapital:
                    if (!ObserveCapitalFlow())
                    {
                        return;
                    }

                    SetStage(Stage.CaptureHud, "Capital flow validated.");
                    break;

                case Stage.CaptureHud:
                    if (!CaptureHudSnapshot())
                    {
                        return;
                    }

                    SetStage(Stage.Finish, "HUD snapshot captured.");
                    break;

                case Stage.Finish:
                    FinishSuccess();
                    break;
            }
        }

        private static bool ObserveCapitalFlow()
        {
            object buildDirector = GetMemberValue(s_runtime, "BuildDirector");
            object cityPlanner = GetMemberValue(s_runtime, "CityPlanner");
            object governor = GetMemberValue(s_runtime, "ConstructionGovernor");
            object capital = cityPlanner != null ? GetMemberValue(cityPlanner, "Capital") : null;
            string state = GetStringMember(s_runtime, "ConstructionStateStatus");
            string mode = GetStringMember(governor, "ConstructionMode");
            string treasury = GetStringMember(s_runtime, "TreasuryStatus");
            string emergencyReserve = GetStringMember(s_runtime, "EmergencyReserveStatus");
            string availableFunds = GetStringMember(s_runtime, "AvailableConstructionFundsStatus");
            string currentNeed = GetStringMember(s_runtime, "CurrentNeedStatus");
            string needScore = GetStringMember(s_runtime, "NeedScoreStatus");
            string objective = GetStringMember(s_runtime, "NextObjectiveStatus");
            string constructionCommand = GetStringMember(s_runtime, "ConstructionCommandStatus");
            string command = GetStringMember(buildDirector, "ActiveConstructionCommand");
            string blockReason = GetStringMember(buildDirector, "BlockReasonStatus");
            string failureCount = GetStringMember(buildDirector, "FailureCountStatus");
            s_capitalSource = GetStringMember(s_runtime, "CapitalSourceStatus");
            s_capitalItemId = GetStringMember(s_runtime, "CapitalItemIdStatus");
            s_capitalPrefab = GetStringMember(s_runtime, "CapitalPrefabStatus");
            s_capitalFailure = GetStringMember(s_runtime, "CapitalDiagnosticStatus");
            AddTransition(state);
            s_sawWaitingConfirmation |= string.Equals(state, "WaitingConfirmation", StringComparison.Ordinal);
            s_maxPendingCommands = Mathf.Max(s_maxPendingCommands, GetIntMember(buildDirector, "PendingCommandCount"));

            if (capital != null
                && GetIntMember(GetMemberValue(s_runtime, "ConstructionGovernor"), "BuildingsTotal") >= 1
                && string.Equals(state, "Idle", StringComparison.Ordinal)
                && GetIntMember(buildDirector, "PendingCommandCount") == 0
                && s_sawWaitingConfirmation)
            {
                TraceLine("Capital confirmed and planner is idle.");
                return true;
            }

            if (ElapsedStageTime() > CapitalObservationSeconds)
            {
                TraceLine(
                    "Capital flow stalled | mode=" + mode
                    + " state=" + state
                    + " treasury=" + treasury
                    + " reserve=" + emergencyReserve
                    + " available=" + availableFunds
                    + " need=" + currentNeed
                    + " score=" + needScore
                    + " objective=" + objective
                    + " constructionCommand=" + constructionCommand
                    + " command=" + command
                    + " block=" + blockReason
                    + " failures=" + failureCount
                    + " capitalSource=" + s_capitalSource
                    + " capitalItemId=" + s_capitalItemId
                    + " capitalPrefab=" + s_capitalPrefab
                    + " capitalDiagnostic=" + s_capitalFailure);
                Fail(
                    "Capital flow did not stabilize. state=" + state
                    + " capital=" + (capital != null)
                    + " buildings=" + GetIntMember(GetMemberValue(s_runtime, "ConstructionGovernor"), "BuildingsTotal")
                    + " pending=" + GetIntMember(buildDirector, "PendingCommandCount")
                    + " sawWaitingConfirmation=" + s_sawWaitingConfirmation
                    + " mode=" + mode
                    + " treasury=" + treasury
                    + " reserve=" + emergencyReserve
                    + " available=" + availableFunds
                    + " need=" + currentNeed
                    + " score=" + needScore
                    + " objective=" + objective
                    + " constructionCommand=" + constructionCommand
                    + " command=" + command
                    + " block=" + blockReason
                    + " failures=" + failureCount
                    + " capitalSource=" + s_capitalSource
                    + " capitalItemId=" + s_capitalItemId
                    + " capitalPrefab=" + s_capitalPrefab
                    + " capitalDiagnostic=" + s_capitalFailure);
            }

            return false;
        }

        private static bool CaptureHudSnapshot()
        {
            if (s_diagnostic == null)
            {
                s_diagnostic = FindDiagnostic();
                if (s_diagnostic == null)
                {
                    return false;
                }

                TraceLine("Diagnostic overlay found.");
            }

            if (ElapsedStageTime() < HudWarmupSeconds)
            {
                return false;
            }

            object governor = GetMemberValue(s_runtime, "ConstructionGovernor");
            s_capitalSource = GetStringMember(s_runtime, "CapitalSourceStatus");
            s_capitalItemId = GetStringMember(s_runtime, "CapitalItemIdStatus");
            s_capitalPrefab = GetStringMember(s_runtime, "CapitalPrefabStatus");
            s_capitalFailure = GetStringMember(s_runtime, "CapitalDiagnosticStatus");

            string[] requiredHudKeys =
            {
                "ia01_construction_mode",
                "ia01_construction_state",
                "ia01_construction_freeze_reason",
                "ia01_next_unfreeze_condition",
                "ia01_active_command",
                "ia01_pending_structure",
                "ia01_confirmation_deadline",
                "ia01_treasury",
                "ia01_emergency_reserve",
                "ia01_available_construction_funds",
                "ia01_buildings_total",
                "ia01_buildings_by_strategic_role",
                "ia01_current_need",
                "ia01_need_score",
                "ia01_current_lot",
                "ia01_catalog_intent_queries",
                "ia01_catalog_index_builds",
                "ia01_catalog_candidates",
                "ia01_physics_checks",
                "ia01_last_construction_completed_at"
            };

            for (int i = 0; i < requiredHudKeys.Length; i++)
            {
                string value = GetMetricText(s_diagnostic, requiredHudKeys[i]);
                if (string.IsNullOrWhiteSpace(value))
                {
                    Fail("HUD did not publish metric " + requiredHudKeys[i] + ".");
                    return false;
                }
            }

            PerfSummary perf = ReadPerfSummary(s_diagnostic);
            TraceLine(
                "Perf snapshot: fpsMedio=" + perf.FpsMedio.ToString("0.00", CultureInfo.InvariantCulture)
                + " fpsMin=" + perf.FpsMinimo.ToString("0.00", CultureInfo.InvariantCulture)
                + " cpuMainMs=" + perf.CpuMainMs.ToString("0.00", CultureInfo.InvariantCulture)
                + " gpuMs=" + perf.GpuMs.ToString("0.00", CultureInfo.InvariantCulture)
                + " gc0=" + perf.GcGen0
                + " gc1=" + perf.GcGen1
                + " gc2=" + perf.GcGen2
                + " ia01.frame=" + GetMetricTimeText(s_diagnostic, "ia01.frame")
                + " ia01.slice=" + GetMetricTimeText(s_diagnostic, "ia01.slice." + GetIntMember(s_controller, "NationId"))
                + " sliceCount=" + GetMetricCount(s_diagnostic, "ia01.slice.count"));

            TraceLine(
                "Capital source=" + s_capitalSource
                + " itemId=" + s_capitalItemId
                + " prefab=" + s_capitalPrefab
                + " failure=" + s_capitalFailure
                + " team=" + GetIntMember(s_controller, "TeamId")
                + " buildings=" + GetIntMember(GetMemberValue(s_runtime, "ConstructionGovernor"), "BuildingsTotal")
                + " pending=" + GetIntMember(GetMemberValue(s_runtime, "BuildDirector"), "PendingCommandCount"));

            return true;
        }

        private static void FinishSuccess()
        {
            if (s_finished)
            {
                return;
            }

            s_finished = true;
            s_exitCode = 0;
            s_reportStatus = "PASS";
            SessionState.SetBool(CompletedKey, true);
            SessionState.SetInt(ExitCodeKey, s_exitCode);
            WriteReport();
            TraceLine("Validation passed.");
            EditorApplication.isPlaying = false;
        }

        private static void Fail(string message)
        {
            if (s_finished)
            {
                return;
            }

            s_finished = true;
            s_exitCode = 1;
            s_reportStatus = "FAIL: " + message;
            SessionState.SetBool(CompletedKey, true);
            SessionState.SetInt(ExitCodeKey, s_exitCode);
            TraceLine("Validation failed: " + message);
            WriteReport();
            EditorApplication.isPlaying = false;
        }

        private static void SetStage(Stage nextStage, string reason)
        {
            s_stage = (int)nextStage;
            s_stageStartedAt = EditorApplication.timeSinceStartup;
            TraceLine("Stage -> " + nextStage + " | " + reason);
        }

        private static double ElapsedStageTime()
        {
            return EditorApplication.timeSinceStartup - s_stageStartedAt;
        }

        private static void ResetRuntimeState()
        {
            s_running = false;
            s_finished = false;
            s_exitQueued = false;
            s_exitCode = 0;
            s_stage = 0;
            s_stageStartedAt = 0.0;
            s_overallStartedAt = 0.0;
            s_trace.Length = 0;
            s_transitions.Clear();
            s_manager = null;
            s_controller = null;
            s_runtime = null;
            s_diagnostic = null;
            s_menuController = null;
            s_country = null;
            s_sawWaitingConfirmation = false;
            s_maxPendingCommands = 0;
            s_capitalSource = string.Empty;
            s_capitalItemId = string.Empty;
            s_capitalPrefab = string.Empty;
            s_capitalFailure = string.Empty;
            s_reportStatus = string.Empty;
        }

        private static void AddTransition(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return;
            }

            if (s_transitions.Count == 0 || !string.Equals(s_transitions[s_transitions.Count - 1], state, StringComparison.Ordinal))
            {
                s_transitions.Add(state);
            }
        }

        private static void AssertSafeProfileDefaults(object controllerObject)
        {
            if (controllerObject == null)
            {
                Fail("Controller is null while checking profile defaults.");
                return;
            }

            object profile = GetMemberValue(controllerObject, "Profile");
            object settings = profile != null ? GetMemberValue(profile, "ConstructionGovernor") : null;
            if (settings == null)
            {
                Fail("Construction governor profile settings were not found.");
                return;
            }

            if (GetIntMember(settings, "EmergencyReserve") <= 0
                || GetIntMember(settings, "MinimumConstructionReserve") <= 0
                || GetFloatMember(settings, "MaximumConstructionBudgetPercent") <= 0f
                || GetFloatMember(settings, "MaximumMaintenancePercent") <= 0f
                || GetFloatMember(settings, "MinimumAcceptableFps") <= 0f
                || GetFloatMember(settings, "MaxIaFrameBudgetMs") <= 0f
                || GetFloatMember(settings, "MaxBuildPlannerBudgetMs") <= 0f
                || GetIntMember(settings, "MaxCandidatesPerSlice") <= 0
                || GetIntMember(settings, "MaxPhysicsChecksPerSlice") <= 0)
            {
                Fail("Profile defaults are not safe for older assets.");
            }
        }

        private static object ResolveController(object managerObject)
        {
            if (managerObject == null)
            {
                return null;
            }

            object controllerObject = InvokeInstance(managerObject, "FindControllerByTeamId", 1);
            if (controllerObject != null)
            {
                return controllerObject;
            }

            object controllers = GetMemberValue(managerObject, "Controllers");
            if (controllers != null)
            {
                foreach (object candidate in EnumerateCollection(controllers))
                {
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object countryObject = GetCountry(1) ?? GetFirstCountry();
            if (countryObject != null)
            {
                controllerObject = InvokeInstance(managerObject, "CreateControllerFromGovernment", countryObject);
            }

            return controllerObject;
        }

        private static object GetRuntime(object controllerObject)
        {
            if (controllerObject == null)
            {
                return null;
            }

            FieldInfo field = controllerObject.GetType().GetField("nationRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(controllerObject) : null;
        }

        private static object FindManager()
        {
            Type managerType = ResolveType("Hegemonia.AI.IA01.IA01Manager");
            object managerObject = FindFirstObjectOfType(managerType);
            if (managerObject == null)
            {
                managerObject = GetStaticMemberValue(managerType, "Instancia");
            }

            return managerObject;
        }

        private static object FindMenuController()
        {
            return FindFirstObjectOfType(ResolveType("MenuInicialController"));
        }

        private static object FindDiagnostic()
        {
            return FindFirstObjectOfType(ResolveType("DiagnosticoDesempenhoJogo"));
        }

        private static object GetCountry(int teamId)
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
            if (system == null)
            {
                return null;
            }

            object countryObject = InvokeInstance(system, "ObterPais", teamId);
            if (countryObject != null)
            {
                return countryObject;
            }

            object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
            if (countries != null)
            {
                foreach (object candidate in EnumerateCollection(countries))
                {
                    if (candidate != null && GetIntMember(candidate, "teamId") == teamId)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static object GetFirstCountry()
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
            if (system == null)
            {
                return null;
            }

            object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
            if (countries == null)
            {
                return null;
            }

            foreach (object countryObject in EnumerateCollection(countries))
            {
                if (countryObject != null)
                {
                    return countryObject;
                }
            }

            return null;
        }

        private static object GetFirstCountryExcept(int teamId)
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
            if (system == null)
            {
                return null;
            }

            object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
            if (countries == null)
            {
                return null;
            }

            foreach (object countryObject in EnumerateCollection(countries))
            {
                if (countryObject != null && GetIntMember(countryObject, "teamId") != teamId)
                {
                    return countryObject;
                }
            }

            return null;
        }

        private static int GetNextFreeTeamId()
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
            int maxTeamId = 1;
            if (system != null)
            {
                object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
                if (countries != null)
                {
                    foreach (object countryObject in EnumerateCollection(countries))
                    {
                        if (countryObject != null)
                        {
                            maxTeamId = Mathf.Max(maxTeamId, GetIntMember(countryObject, "teamId"));
                        }
                    }
                }
            }

            return maxTeamId + 1;
        }

        private static void WriteReport()
        {
            string path = SessionState.GetString(ResultPathKey, s_resultPath);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var report = new StringBuilder(16384);
            report.AppendLine("IA01 PLAY MODE VALIDATION");
            report.AppendLine("Status: " + (string.IsNullOrWhiteSpace(s_reportStatus) ? "UNKNOWN" : s_reportStatus));
            report.AppendLine("ExitCode: " + s_exitCode);
            report.AppendLine("ElapsedSeconds: " + (EditorApplication.timeSinceStartup - s_overallStartedAt).ToString("0.00", CultureInfo.InvariantCulture));
            report.AppendLine("Scene: " + SceneManager.GetActiveScene().name);
            report.AppendLine("CapitalSource: " + s_capitalSource);
            report.AppendLine("CapitalItemId: " + s_capitalItemId);
            report.AppendLine("CapitalPrefab: " + s_capitalPrefab);
            report.AppendLine("CapitalFailure: " + s_capitalFailure);
            report.AppendLine("Transitions: " + string.Join(" -> ", s_transitions.ToArray()));
            report.AppendLine("WaitingConfirmation: " + s_sawWaitingConfirmation);
            report.AppendLine("MaxPendingCommands: " + s_maxPendingCommands);
            report.AppendLine("TraceFile: " + s_tracePath);
            report.AppendLine();
            report.AppendLine(s_trace.ToString());

            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        }

        private static void TraceLine(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message;
            s_trace.AppendLine(line);
            File.AppendAllText(GetTracePath(), line + Environment.NewLine, new UTF8Encoding(false));
            Debug.Log(line);
        }

        private static string GetTracePath()
        {
            string path = SessionState.GetString(TracePathKey, s_tracePath);
            if (string.IsNullOrWhiteSpace(path))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                path = Path.Combine(projectRoot, "IA01_PlayModeValidation_trace.log");
                SessionState.SetString(TracePathKey, path);
            }

            return path;
        }

        private static void DeleteSaveFile()
        {
            DeleteFileIfExists(Path.Combine(Application.persistentDataPath, SaveFileName));
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static object FindFirstObjectOfType(Type targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
            object fallback = null;
            for (int i = 0; i < objects.Length; i++)
            {
                UnityEngine.Object candidate = objects[i];
                if (candidate == null || !targetType.IsInstanceOfType(candidate))
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (candidate is Component component && component.gameObject.scene.IsValid())
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private static IEnumerable EnumerateCollection(object collection)
        {
            if (collection is IEnumerable enumerable)
            {
                return enumerable;
            }

            throw new InvalidOperationException("Object is not enumerable: " + collection.GetType().Name);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(instance, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            if (type == null)
            {
                return null;
            }

            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                PropertyInfo property = current.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(null, null);
                }

                current = current.BaseType;
            }

            return null;
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null)
            {
                return;
            }

            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return;
                }

                PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return;
                }

                type = type.BaseType;
            }
        }

        private static object InvokeInstance(object instance, string methodName, params object[] args)
        {
            if (instance == null)
            {
                return null;
            }

            MethodInfo method = FindMethod(instance.GetType(), methodName, false, args);
            if (method == null)
            {
                return null;
            }

            return method.Invoke(instance, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo method = FindMethod(type, methodName, true, args);
            if (method == null)
            {
                return null;
            }

            return method.Invoke(null, args);
        }

        private static MethodInfo FindMethod(Type type, string methodName, bool isStatic, params object[] args)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo[] methods = type.GetMethods(flags);
            int argCount = args != null ? args.Length : 0;

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != argCount)
                {
                    continue;
                }

                bool match = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    object arg = args[p];
                    if (arg == null)
                    {
                        if (parameters[p].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[p].ParameterType) == null)
                        {
                            match = false;
                            break;
                        }
                    }
                    else if (!parameters[p].ParameterType.IsAssignableFrom(arg.GetType()))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return method;
                }
            }

            return null;
        }

        private static string GetStringMember(object instance, string memberName)
        {
            object value = GetMemberValue(instance, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static int GetIntMember(object instance, string memberName)
        {
            object value = GetMemberValue(instance, memberName);
            return value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static float GetFloatMember(object instance, string memberName)
        {
            object value = GetMemberValue(instance, memberName);
            return value != null ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0f;
        }

        private static string GetMetricText(object diagnosticObject, string key)
        {
            return InvokePrivateMetric<string>(diagnosticObject, "ObterTextoMetrica", key);
        }

        private static string GetMetricTimeText(object diagnosticObject, string key)
        {
            float value = InvokePrivateMetric<float>(diagnosticObject, "ObterTempoMetrica", key);
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static int GetMetricCount(object diagnosticObject, string key)
        {
            return InvokePrivateMetric<int>(diagnosticObject, "ObterContadorMetrica", key);
        }

        private static T InvokePrivateMetric<T>(object diagnosticObject, string methodName, string key)
        {
            if (diagnosticObject == null)
            {
                return default(T);
            }

            MethodInfo method = diagnosticObject.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return default(T);
            }

            object value = method.Invoke(diagnosticObject, new object[] { key });
            if (value is T typed)
            {
                return typed;
            }

            if (value == null)
            {
                return default(T);
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static PerfSummary ReadPerfSummary(object diagnosticObject)
        {
            if (diagnosticObject == null)
            {
                return default(PerfSummary);
            }

            FieldInfo field = diagnosticObject.GetType().GetField("_ultimoResumo", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return default(PerfSummary);
            }

            object summary = field.GetValue(diagnosticObject);
            if (summary == null)
            {
                return default(PerfSummary);
            }

            Type summaryType = summary.GetType();
            return new PerfSummary
            {
                FpsMedio = Convert.ToSingle(summaryType.GetField("FpsMedio", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                FpsMinimo = Convert.ToSingle(summaryType.GetField("FpsMinimo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                CpuMainMs = Convert.ToSingle(summaryType.GetField("CpuMainMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                GpuMs = Convert.ToSingle(summaryType.GetField("GpuMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                GcGen0 = Convert.ToInt32(summaryType.GetField("GcGen0", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                GcGen1 = Convert.ToInt32(summaryType.GetField("GcGen1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture),
                GcGen2 = Convert.ToInt32(summaryType.GetField("GcGen2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary), CultureInfo.InvariantCulture)
            };
        }

        private static Type ResolveType(string typeName)
        {
            lock (TypeCache)
            {
                if (TypeCache.TryGetValue(typeName, out Type cached))
                {
                    return cached;
                }

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null)
                    {
                        continue;
                    }

                    try
                    {
                        Type type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            TypeCache[typeName] = type;
                            return type;
                        }

                        Type[] assemblyTypes = assembly.GetTypes();
                        for (int i = 0; i < assemblyTypes.Length; i++)
                        {
                            Type candidate = assemblyTypes[i];
                            if (candidate == null)
                            {
                                continue;
                            }

                            if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                                || string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                            {
                                TypeCache[typeName] = candidate;
                                return candidate;
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        if (ex.Types != null)
                        {
                            for (int i = 0; i < ex.Types.Length; i++)
                            {
                                Type candidate = ex.Types[i];
                                if (candidate == null)
                                {
                                    continue;
                                }

                                if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                                    || string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                                {
                                    TypeCache[typeName] = candidate;
                                    return candidate;
                                }
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException("Could not resolve type " + typeName + ".");
        }

        private static bool ContainsIgnoreCase(string text, string fragment)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(fragment)
                   && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
