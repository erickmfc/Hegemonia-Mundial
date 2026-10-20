using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Captura o custo do Update real de quinze BrainMaster com IA03 ativa em uma
/// cena controlada. O teste cria somente objetos temporários e não salva cena.
/// </summary>
public sealed class IA03RuntimePerformancePlayModeTests
{
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const int BrainCount = 15;
    private const int WarmupFrames = 90;
    private const int MeasuredFrames = 180;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator RuntimeRealComQuinzeBrainMastersRegistraFrameCpuMemoriaEGc()
    {
        AsyncOperation load;
#if UNITY_EDITOR
        load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(ValidationScenePath, LoadSceneMode.Single);
#endif
        Assert.That(load, Is.Not.Null);
        while (!load.isDone) yield return null;
        yield return null;

        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");
        var brains = new List<GameObject>(BrainCount);
        var profiles = new List<ScriptableObject>(BrainCount);
        var existingBrainBehaviours = FindBrainBehaviours(brainType);
        var existingBrainEnabledStates = new List<bool>(existingBrainBehaviours.Count);
        for (int index = 0; index < existingBrainBehaviours.Count; index++)
            existingBrainEnabledStates.Add(existingBrainBehaviours[index].enabled);

        try
        {
            SetBrainEnabled(existingBrainBehaviours, false);
            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;
            var zeroBrains = new WindowMetrics();
            yield return CaptureWindow(zeroBrains, BuildSchedulerProbe(brainType));
            Debug.Log(FormatMetrics("zero_scene_brains", CountOperationalBrains(brainType), zeroBrains));

            SetBrainEnabled(existingBrainBehaviours, existingBrainEnabledStates);
            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;
            int baselineBrainCount = CountOperationalBrains(brainType);
            var baseline = new WindowMetrics();
            yield return CaptureWindow(baseline, BuildSchedulerProbe(brainType));
            Debug.Log(FormatMetrics("baseline_scene_brains", baselineBrainCount, baseline));
            Debug.Log(FormatSchedulerBreakdown("baseline_scene_brains", brainType));

            for (int index = 0; index < BrainCount; index++)
            {
                GameObject brainObject = new GameObject("IA03_RuntimePerf_Team_" + (1001 + index));
                brainObject.SetActive(false);
                brains.Add(brainObject);

                Component brain = brainObject.AddComponent(brainType);
                SetMember(brain, "TeamId", 1001 + index);

                Component strategist = brainObject.AddComponent(strategistType);
                ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
                profiles.Add(profile);
                SetMember(profile, "intervaloDecisaoSegundos", 5f);
                SetMember(strategist, "perfilPais", profile);
                SetMember(strategist, "ativo", true);
                SetMember(strategist, "brain", brain);
                brainObject.SetActive(true);
            }

            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;

            var withIA03 = new WindowMetrics();
            yield return CaptureWindow(withIA03, BuildSchedulerProbe(brainType));
            Debug.Log(FormatMetrics("scene_plus_15_ia03", CountOperationalBrains(brainType), withIA03));
            Debug.Log(FormatSchedulerBreakdown("scene_plus_15_ia03", brainType));

            yield return CaptureSuppressedModuleWindow(brainType, "scene_plus_15_without_build", "IA_BuildDirector");
            yield return CaptureSuppressedModuleWindow(brainType, "scene_plus_15_without_tactical", "IA_TacticalDirector");
            yield return CaptureSuppressedModuleWindow(
                brainType,
                "scene_plus_15_without_build_tactical",
                "IA_BuildDirector",
                "IA_TacticalDirector");

            var moduleWindow = new Dictionary<string, ModuleWindowAggregate>(StringComparer.Ordinal);
            yield return CaptureModuleWindow(brainType, 120, moduleWindow);
            Debug.Log(FormatModuleWindow(moduleWindow));
            yield return CaptureBuildDirectorProfiling(brainType, 180);

            Assert.That(brains.Count, Is.EqualTo(BrainCount));
            for (int index = 0; index < brains.Count; index++)
            {
                Assert.That(brains[index], Is.Not.Null);
                Assert.That(brains[index].activeInHierarchy, Is.True);
            }
        }
        finally
        {
            SetBrainEnabled(existingBrainBehaviours, existingBrainEnabledStates);
            for (int index = 0; index < brains.Count; index++)
            {
                if (brains[index] != null) UnityEngine.Object.Destroy(brains[index]);
            }
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] != null) UnityEngine.Object.Destroy(profiles[index]);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeCapturaMarcadoresDoFrameSpikeSemBrain()
    {
        AsyncOperation load;
#if UNITY_EDITOR
        load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(ValidationScenePath, LoadSceneMode.Single);
#endif
        Assert.That(load, Is.Not.Null);
        while (!load.isDone) yield return null;
        yield return null;

        Type brainType = ResolveType("IA_BrainMaster");
        List<Behaviour> existingBrains = FindBrainBehaviours(brainType);
        var previousStates = new List<bool>(existingBrains.Count);
        for (int index = 0; index < existingBrains.Count; index++)
            previousStates.Add(existingBrains[index] != null && existingBrains[index].enabled);

        var markerNames = new[]
        {
            "Main Thread",
            "PlayerLoop",
            "BehaviourUpdate",
            "ScriptRunBehaviourUpdate",
            "Physics.Simulate",
            "Camera.Render",
            "RenderLoop.Draw",
            "Gfx.WaitForPresentOnGfxThread",
            "GC.Alloc",
            "GC Allocated In Frame"
        };
        var recorders = new List<SpikeMarkerRecorder>(markerNames.Length);
        try
        {
            SetBrainEnabled(existingBrains, false);
            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;

            for (int index = 0; index < markerNames.Length; index++)
            {
                try
                {
                    ProfilerCategory category = markerNames[index].Contains("GC")
                        ? ProfilerCategory.Memory
                        : ProfilerCategory.Internal;
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, markerNames[index], 15);
                    if (recorder.Valid)
                        recorders.Add(new SpikeMarkerRecorder(markerNames[index], recorder));
                    else
                        recorder.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[IA03PERF][SPIKE] marcador indisponivel: "
                                     + markerNames[index] + " | " + exception.Message);
                }
            }

            int capturedSpikes = 0;
            var stopwatch = new Stopwatch();
            for (int frame = 0; frame < MeasuredFrames; frame++)
            {
                stopwatch.Restart();
                yield return null;
                stopwatch.Stop();
                float frameMs = (float)stopwatch.Elapsed.TotalMilliseconds;
                if (frameMs <= 50f)
                    continue;

                capturedSpikes++;
                var line = new StringBuilder("[IA03PERF][SPIKE] frame=")
                    .Append(frame)
                    .Append("; elapsedMs=")
                    .Append(frameMs.ToString("0.000"));
                for (int index = 0; index < recorders.Count; index++)
                {
                    SpikeMarkerRecorder marker = recorders[index];
                    long value = marker.Recorder.LastValue;
                    double valueMs = value / 1000000d;
                    line.Append("; ").Append(marker.Name).Append("=")
                        .Append(valueMs.ToString("0.000"));
                }
                Debug.Log(line.ToString());
            }

            Debug.Log("[IA03PERF][SPIKE_SUMMARY] markers=" + recorders.Count
                      + "; spikesOver50Ms=" + capturedSpikes
                      + "; brainsDisabled=" + existingBrains.Count);
        }
        finally
        {
            for (int index = 0; index < recorders.Count; index++)
                recorders[index].Recorder.Dispose();
            SetBrainEnabled(existingBrains, previousStates);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimePerfilCadaCondicaoRecarregaCenaLimpa()
    {
        ResetFreshProfileOutput();
        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");

        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_zero_scene_brains",
            disableExistingBrains: true,
            extraBrainCount: 0,
            disableBootstrapForExtraBrains: false);
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_baseline_scene_brains",
            disableExistingBrains: false,
            extraBrainCount: 0,
            disableBootstrapForExtraBrains: false);
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_scene_plus_15_ia03",
            disableExistingBrains: false,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: false);
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_scene_plus_15_without_build",
            disableExistingBrains: false,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: false,
            "IA_BuildDirector");
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_scene_plus_15_without_tactical",
            disableExistingBrains: false,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: false,
            "IA_TacticalDirector");
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_scene_plus_15_without_build_tactical",
            disableExistingBrains: false,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: false,
            "IA_BuildDirector",
            "IA_TacticalDirector");
    }

    [UnityTest]
    public IEnumerator RuntimePerfilTacticoSemBootstrapRecarregaCenaLimpa()
    {
        ResetFreshProfileOutput();
        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");

        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_no_bootstrap_scene_plus_15_ia03",
            disableExistingBrains: true,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: true);
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_no_bootstrap_scene_plus_15_without_build",
            disableExistingBrains: true,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: true,
            "IA_BuildDirector");
        yield return CaptureFreshSceneProfile(
            brainType,
            strategistType,
            profileType,
            "fresh_no_bootstrap_scene_plus_15_without_tactical",
            disableExistingBrains: true,
            extraBrainCount: BrainCount,
            disableBootstrapForExtraBrains: true,
            "IA_TacticalDirector");
    }

    [UnityTest]
    public IEnumerator RuntimeTraceSchedulerPorJanelaControlada()
    {
        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");
        bool previousModuleTrace = GetModuleTraceEnabled();
        string tracePath = string.Empty;
        CloseRuntimeTraceSession();
        SetModuleTraceEnabled(true);
        try
        {
            yield return CaptureFreshSceneProfile(
                brainType,
                strategistType,
                profileType,
                "trace_no_bootstrap_scene_plus_15",
                disableExistingBrains: true,
                extraBrainCount: BrainCount,
                disableBootstrapForExtraBrains: true);
            tracePath = GetRuntimeTracePath();
        }
        finally
        {
            SetModuleTraceEnabled(previousModuleTrace);
            CloseRuntimeTraceSession();
        }

        WriteModuleTraceSummary(tracePath);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeABTraceNegocioIsolaCustoDeEscrita()
    {
        ResetFreshProfileOutput();
        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");
        bool previousBusinessTrace = GetBusinessTraceEnabled();
        bool previousModuleTrace = GetModuleTraceEnabled();
        string traceOnPath = string.Empty;
        string traceOffPath = string.Empty;
        CloseRuntimeTraceSession();
        SetModuleTraceEnabled(false);
        try
        {
            SetBusinessTraceEnabled(true);
            yield return CaptureFreshSceneProfileWindow(
                brainType,
                strategistType,
                profileType,
                "ab_trace_on_scene_plus_15",
                disableExistingBrains: true,
                extraBrainCount: BrainCount,
                disableBootstrapForExtraBrains: false,
                warmupFrames: 0,
                measuredFrames: 60);
            traceOnPath = GetRuntimeTracePath();

            CloseRuntimeTraceSession();
            SetBusinessTraceEnabled(false);
            yield return CaptureFreshSceneProfileWindow(
                brainType,
                strategistType,
                profileType,
                "ab_trace_off_scene_plus_15",
                disableExistingBrains: true,
                extraBrainCount: BrainCount,
                disableBootstrapForExtraBrains: false,
                warmupFrames: 0,
                measuredFrames: 60);
            traceOffPath = GetRuntimeTracePath();
        }
        finally
        {
            SetBusinessTraceEnabled(previousBusinessTrace);
            SetModuleTraceEnabled(previousModuleTrace);
            CloseRuntimeTraceSession();
        }

        Debug.Log("[IA03PERF][TRACE_AB] traceOnPath=" + traceOnPath + " | traceOffPath=" + traceOffPath);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeABTraceNegocioMedeChamadaDeBootstrap()
    {
        yield return LoadValidationScene();
        Type brainType = ResolveType("IA_BrainMaster");
        List<Behaviour> brains = FindBrainBehaviours(brainType);
        Assert.That(brains.Count, Is.GreaterThan(0), "A cena de validacao nao possui BrainMaster.");
        for (int frame = 0; frame < 5; frame++)
            yield return null;

        bool previousBusinessTrace = GetBusinessTraceEnabled();
        string onDuration;
        string offDuration;
        CloseRuntimeTraceSession();
        try
        {
            SetBusinessTraceEnabled(true);
            Stopwatch onWatch = Stopwatch.StartNew();
            for (int repeat = 0; repeat < 40; repeat++)
            {
                for (int index = 0; index < brains.Count; index++)
                {
                    Component brain = brains[index];
                    if (brain == null)
                        continue;
                    MethodInfo setStatus = brainType.GetMethod("SetBootstrapStatus", Members);
                    if (setStatus != null)
                        setStatus.Invoke(brain, new object[] { "trace-ab-on-" + repeat + "-" + index });
                }
            }
            onWatch.Stop();
            onDuration = onWatch.Elapsed.TotalMilliseconds.ToString("0.000");

            CloseRuntimeTraceSession();
            SetBusinessTraceEnabled(false);
            Stopwatch offWatch = Stopwatch.StartNew();
            for (int repeat = 0; repeat < 40; repeat++)
            {
                for (int index = 0; index < brains.Count; index++)
                {
                    Component brain = brains[index];
                    if (brain == null)
                        continue;
                    MethodInfo setStatus = brainType.GetMethod("SetBootstrapStatus", Members);
                    if (setStatus != null)
                        setStatus.Invoke(brain, new object[] { "trace-ab-off-" + repeat + "-" + index });
                }
            }
            offWatch.Stop();
            offDuration = offWatch.Elapsed.TotalMilliseconds.ToString("0.000");
        }
        finally
        {
            SetBusinessTraceEnabled(previousBusinessTrace);
            CloseRuntimeTraceSession();
        }

        Debug.Log("[IA03PERF][TRACE_AB_CALL] brains=" + brains.Count
            + "; repeats=40; traceOnMs=" + onDuration
            + "; traceOffMs=" + offDuration);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeCatalogRefreshProfilingStartup()
    {
        yield return LoadValidationScene();

        Type brainType = ResolveType("IA_BrainMaster");
        Type strategistType = ResolveType("IA03EstrategaNacional");
        Type profileType = ResolveType("PerfilPaisSO");
        bool previousCatalogDetail = GetCatalogDetailEnabled();
        SetCatalogDetailEnabled(true);

        Component runtimeDiagnostic = CreateRuntimeDiagnostic();
        var temporaryBrains = new List<GameObject>(BrainCount);
        var profiles = new List<ScriptableObject>(BrainCount);
        List<Behaviour> existingBrains = FindBrainBehaviours(brainType);
        var originalEnabledStates = new List<bool>(existingBrains.Count);
        for (int index = 0; index < existingBrains.Count; index++)
            originalEnabledStates.Add(existingBrains[index].enabled);

        try
        {
            SetBrainEnabled(existingBrains, false);
            ResetDiagnosticAccumulators();
            Type backendType = ResolveType("IA_BackendBridge");
            ConstructorInfo backendConstructor = backendType.GetConstructor(new[] { typeof(int) });
            MethodInfo refreshCatalog = backendType.GetMethod("RefreshCatalog", Members);
            Assert.That(backendConstructor, Is.Not.Null);
            Assert.That(refreshCatalog, Is.Not.Null);
            var directBridges = new List<object>(BrainCount);
            for (int index = 0; index < BrainCount; index++)
            {
                object bridge = backendConstructor.Invoke(new object[] { 2301 + index });
                directBridges.Add(bridge);
                refreshCatalog.Invoke(bridge, null);
            }
            Debug.Log("[IA03PERF][CATALOG_DIRECT] " + ReadDiagnosticMetrics());

            for (int index = 0; index < BrainCount; index++)
            {
                GameObject brainObject = new GameObject("IA03_RuntimePerf_Catalog_Team_" + (2301 + index));
                brainObject.SetActive(false);
                temporaryBrains.Add(brainObject);

                Component brain = brainObject.AddComponent(brainType);
                SetMember(brain, "TeamId", 2301 + index);
                Component strategist = brainObject.AddComponent(strategistType);
                ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
                profiles.Add(profile);
                SetMember(profile, "intervaloDecisaoSegundos", 5f);
                SetMember(strategist, "perfilPais", profile);
                SetMember(strategist, "ativo", true);
                SetMember(strategist, "brain", brain);
                brainObject.SetActive(true);
            }

            for (int frame = 0; frame < 4; frame++)
                yield return null;

            Debug.Log("[IA03PERF][CATALOG] " + ReadDiagnosticMetrics());
            Assert.That(CountOperationalBrains(brainType), Is.EqualTo(BrainCount));
        }
        finally
        {
            SetBrainEnabled(existingBrains, originalEnabledStates);
            for (int index = 0; index < temporaryBrains.Count; index++)
            {
                if (temporaryBrains[index] != null)
                    UnityEngine.Object.Destroy(temporaryBrains[index]);
            }
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] != null)
                    UnityEngine.Object.Destroy(profiles[index]);
            }
            if (runtimeDiagnostic != null)
            {
                MethodInfo setCaptureMode = runtimeDiagnostic.GetType().GetMethod("SetCaptureMode", Members);
                if (setCaptureMode != null)
                    setCaptureMode.Invoke(runtimeDiagnostic, new object[] { false, false });
                UnityEngine.Object.Destroy(runtimeDiagnostic.gameObject);
            }
            SetCatalogDetailEnabled(previousCatalogDetail);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeSharedCatalogRegistrationProfiling()
    {
        yield return LoadValidationScene();

        Type backendType = ResolveType("IA_BackendBridge");
        ConstructorInfo backendConstructor = backendType.GetConstructor(new[] { typeof(int) });
        MethodInfo refreshCatalog = backendType.GetMethod("RefreshCatalog", Members);
        PropertyInfo catalogProperty = backendType.GetProperty("Catalog", Members);
        Type sharedCatalogType = ResolveType("CatalogoProdutoCompartilhado");
        MethodInfo registerMethod = null;
        MethodInfo[] sharedMethods = sharedCatalogType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int index = 0; index < sharedMethods.Length; index++)
        {
            if (sharedMethods[index].Name == "RegistrarConstrucoes"
                && sharedMethods[index].GetParameters().Length == 1)
            {
                registerMethod = sharedMethods[index];
                break;
            }
        }

        Assert.That(backendConstructor, Is.Not.Null);
        Assert.That(refreshCatalog, Is.Not.Null);
        Assert.That(catalogProperty, Is.Not.Null);
        Assert.That(registerMethod, Is.Not.Null);

        object bridge = backendConstructor.Invoke(new object[] { 2401 });
        refreshCatalog.Invoke(bridge, null);
        object fichas = catalogProperty.GetValue(bridge, null);
        Assert.That(fichas, Is.Not.Null);

        var durations = new List<double>(15);
        for (int repeat = 0; repeat < 15; repeat++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            registerMethod.Invoke(null, new[] { fichas });
            watch.Stop();
            durations.Add(watch.Elapsed.TotalMilliseconds);
        }

        double first = durations[0];
        double repeatedTotal = 0d;
        double repeatedPeak = 0d;
        for (int index = 1; index < durations.Count; index++)
        {
            repeatedTotal += durations[index];
            repeatedPeak = Math.Max(repeatedPeak, durations[index]);
        }

        Debug.Log("[IA03PERF][CATALOG_SHARED_DIRECT] repeats=" + durations.Count
            + "; firstMs=" + first.ToString("0.000")
            + "; repeatedMeanMs=" + (repeatedTotal / Math.Max(1, durations.Count - 1)).ToString("0.000")
            + "; repeatedPeakMs=" + repeatedPeak.ToString("0.000"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeSharedCatalogReprocessesChangedConstruction()
    {
        yield return LoadValidationScene();

        Type fichaType = ResolveType("DadosConstrucao");
        Type capabilityType = ResolveType("IA_ConstructionCapability");
        Type categoriaType = fichaType.GetNestedType("CategoriaItem", BindingFlags.Public);
        Type catalogType = ResolveType("CatalogoProdutoCompartilhado");
        MethodInfo registrar = catalogType.GetMethod("RegistrarConstrucoes", BindingFlags.Public | BindingFlags.Static);
        MethodInfo obter = catalogType.GetMethod("TentarObter", BindingFlags.Public | BindingFlags.Static);
        Assert.That(registrar, Is.Not.Null);
        Assert.That(obter, Is.Not.Null);

        ScriptableObject ficha = ScriptableObject.CreateInstance(fichaType);
        try
        {
            SetMember(ficha, "NomeItem", "IA03 Cache Mutation Fixture");
            SetMember(ficha, "categoria", Enum.Parse(categoriaType, "Exercito"));
            SetMember(ficha, "Capacidades", Enum.Parse(capabilityType, "Unit, Military"));
            PropertyInfo receitaProperty = fichaType.GetProperty("ReceitaProducao", Members);
            Assert.That(receitaProperty, Is.Not.Null);
            object receita = receitaProperty.GetValue(ficha, null);
            SetMember(receita, "configurada", false);
            SetMember(receita, "diasNecessarios", 1);

            Array fichas = Array.CreateInstance(fichaType, 1);
            fichas.SetValue(ficha, 0);
            registrar.Invoke(null, new object[] { fichas });

            object[] args = { "ia03_cache_mutation_fixture", null };
            Assert.That(Convert.ToBoolean(obter.Invoke(null, args)), Is.True);
            object antes = args[1];
            Assert.That(ReadMember(antes, antes.GetType(), "permiteProducaoAutomatica"), Is.EqualTo(false));

            SetMember(receita, "configurada", true);
            registrar.Invoke(null, new object[] { fichas });

            args = new object[] { "ia03_cache_mutation_fixture", null };
            Assert.That(Convert.ToBoolean(obter.Invoke(null, args)), Is.True);
            object depois = args[1];
            Assert.That(ReadMember(depois, depois.GetType(), "permiteProducaoAutomatica"), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ficha);
        }

        yield return null;
    }

    private static Type ResolveRuntimeTraceType()
    {
        return ResolveType("IA_RuntimeTextTrace");
    }

    private static bool GetModuleTraceEnabled()
    {
        PropertyInfo property = ResolveRuntimeTraceType().GetProperty(
            "ModuleTraceEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        return Convert.ToBoolean(property.GetValue(null, null));
    }

    private static bool GetBusinessTraceEnabled()
    {
        PropertyInfo property = ResolveRuntimeTraceType().GetProperty(
            "BusinessTraceEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        return Convert.ToBoolean(property.GetValue(null, null));
    }

    private static bool GetCatalogDetailEnabled()
    {
        PropertyInfo property = ResolveType("IA_BackendBridge").GetProperty(
            "DetailedCatalogTimingEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        return Convert.ToBoolean(property.GetValue(null, null));
    }

    private static void SetBusinessTraceEnabled(bool enabled)
    {
        PropertyInfo property = ResolveRuntimeTraceType().GetProperty(
            "BusinessTraceEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, enabled, null);
    }

    private static void SetCatalogDetailEnabled(bool enabled)
    {
        PropertyInfo property = ResolveType("IA_BackendBridge").GetProperty(
            "DetailedCatalogTimingEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, enabled, null);
    }

    private static void SetModuleTraceEnabled(bool enabled)
    {
        PropertyInfo property = ResolveRuntimeTraceType().GetProperty(
            "ModuleTraceEnabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, enabled, null);
    }

    private static string GetRuntimeTracePath()
    {
        PropertyInfo property = ResolveRuntimeTraceType().GetProperty(
            "CurrentPath",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        return Convert.ToString(property.GetValue(null, null));
    }

    private static void CloseRuntimeTraceSession()
    {
        MethodInfo method = ResolveRuntimeTraceType().GetMethod(
            "CloseSession",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(null, null);
    }

    private static void WriteModuleTraceSummary(string tracePath)
    {
        string outputPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "ValidationResults",
            "ia03-scheduler-trace-latest.txt");
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var modules = new[]
        {
            "IA_BuildDirector",
            "IA_TacticalDirector",
            "IA_WorldState",
            "IA_ProductionDirector",
            "IA_NavalDirector"
        };
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var selectedLines = new List<string>(1024);
        for (int index = 0; index < modules.Length; index++)
            counts[modules[index]] = 0;

        if (!string.IsNullOrEmpty(tracePath) && File.Exists(tracePath))
        {
            string[] lines = File.ReadAllLines(tracePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                {
                    string module = modules[moduleIndex];
                    if (!line.Contains("| " + module + " | MODULE |"))
                        continue;

                    counts[module]++;
                    if (selectedLines.Count < 4096)
                        selectedLines.Add(line);
                    break;
                }
            }
        }

        var output = new StringBuilder(4096);
        output.AppendLine("[IA03TRACE] source=" + (tracePath ?? string.Empty));
        for (int index = 0; index < modules.Length; index++)
        {
            string module = modules[index];
            output.AppendLine("[IA03TRACE][COUNT] module=" + module + "; events=" + counts[module]);
        }
        for (int index = 0; index < selectedLines.Count; index++)
            output.AppendLine(selectedLines[index]);
        File.WriteAllText(outputPath, output.ToString());
    }

    private static string FreshProfileOutputPath
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "ValidationResults", "ia03-runtime-fresh-profile-latest.txt");
        }
    }

    private static void ResetFreshProfileOutput()
    {
        try
        {
            string path = FreshProfileOutputPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, string.Empty);
        }
        catch (IOException exception)
        {
            Debug.LogWarning("[IA03PERF] Nao foi possivel limpar o arquivo de perfil: " + exception.Message);
        }
    }

    private static void AppendFreshProfileOutput(
        string label,
        int brains,
        WindowMetrics metrics,
        string diagnosticMetrics,
        string schedulerMetrics,
        int warmupFrames = WarmupFrames,
        int measuredFrames = MeasuredFrames)
    {
        try
        {
            string path = FreshProfileOutputPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(
                path,
                FormatMetrics(label, brains, metrics, warmupFrames, measuredFrames) + Environment.NewLine);
            if (!string.IsNullOrEmpty(diagnosticMetrics))
            {
                File.AppendAllText(
                    path,
                    "[IA03PERF][DIAGNOSTICS] label=" + label + " | " + diagnosticMetrics + Environment.NewLine);
            }
            if (!string.IsNullOrEmpty(schedulerMetrics))
            {
                File.AppendAllText(
                    path,
                    "[IA03PERF][SCHEDULER] label=" + label + " | " + schedulerMetrics + Environment.NewLine);
            }
            if (!string.IsNullOrEmpty(metrics.peakFrameSchedulerSnapshot)
                || !string.IsNullOrEmpty(metrics.peakGcSchedulerSnapshot))
            {
                File.AppendAllText(
                    path,
                    "[IA03PERF][HITCH] label=" + label
                    + "; peakFrameMs=" + metrics.peakFrameMs.ToString("0.000")
                    + "; peakGcBytes=" + metrics.peakFrameProfilerGcAllocBytes
                    + "; schedulerAtPeakFrame=" + metrics.peakFrameSchedulerSnapshot
                    + "; schedulerAtPeakGc=" + metrics.peakGcSchedulerSnapshot
                    + Environment.NewLine);
            }
        }
        catch (IOException exception)
        {
            Debug.LogWarning("[IA03PERF] Nao foi possivel salvar o perfil " + label + ": " + exception.Message);
        }
    }

    private static void ResetDiagnosticAccumulators()
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null || behaviour.GetType().Name != "DiagnosticoDesempenhoJogo")
                continue;

            MethodInfo resetMethod = behaviour.GetType().GetMethod("ReiniciarAcumuladores", Members);
            if (resetMethod != null)
                resetMethod.Invoke(behaviour, new object[] { Time.unscaledTime });
        }
    }

    private static string ReadDiagnosticMetrics()
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var values = new List<string>();
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null || behaviour.GetType().Name != "DiagnosticoDesempenhoJogo")
                continue;

            FieldInfo metricsField = behaviour.GetType().GetField("_metricasTempoAcumuladas", Members);
            IDictionary dictionary = metricsField != null ? metricsField.GetValue(behaviour) as IDictionary : null;
            if (dictionary == null)
                continue;

            foreach (DictionaryEntry entry in dictionary)
            {
                string key = Convert.ToString(entry.Key);
                if (!IsRelevantDiagnosticMetric(key))
                    continue;

                float value = Convert.ToSingle(entry.Value);
                values.Add(key + "=" + value.ToString("0.000"));
            }
        }

        values.Sort(StringComparer.Ordinal);
        return string.Join("; ", values);
    }

    private static string ReadTacticalSchedulerMetrics(Type brainType)
    {
        FieldInfo schedulerField = brainType.GetField("_scheduler", Members);
        if (schedulerField == null)
            return "schedulerField=missing";

        int brainCount = 0;
        int tacticalSlots = 0;
        int runs = 0;
        int overBudget = 0;
        float peakMs = 0f;
        float lastMaxMs = 0f;
        float nextMin = float.MaxValue;
        float nextMax = float.MinValue;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            MonoBehaviour behaviour = behaviours[behaviourIndex];
            if (behaviour == null
                || behaviour.GetType() != brainType
                || !behaviour.isActiveAndEnabled)
                continue;

            object scheduler = schedulerField.GetValue(behaviour);
            if (scheduler == null)
                continue;
            MethodInfo getSnapshot = scheduler.GetType().GetMethod("GetSnapshot", Members);
            if (getSnapshot == null)
                continue;
            System.Collections.IEnumerable snapshot = getSnapshot.Invoke(scheduler, null)
                as System.Collections.IEnumerable;
            if (snapshot == null)
                continue;

            foreach (object slot in snapshot)
            {
                if (slot == null)
                    continue;
                Type slotType = slot.GetType();
                string name = Convert.ToString(ReadMember(slot, slotType, "Name"));
                if (!string.Equals(name, "IA_TacticalDirector", StringComparison.Ordinal))
                    continue;

                brainCount++;
                tacticalSlots++;
                runs += Convert.ToInt32(ReadMember(slot, slotType, "RunCount"));
                overBudget += Convert.ToInt32(ReadMember(slot, slotType, "OverBudgetCount"));
                peakMs = Mathf.Max(peakMs, Convert.ToSingle(ReadMember(slot, slotType, "PeakCostMs")));
                lastMaxMs = Mathf.Max(lastMaxMs, Convert.ToSingle(ReadMember(slot, slotType, "LastCostMs")));
                float nextTick = Convert.ToSingle(ReadMember(slot, slotType, "NextTick"));
                nextMin = Mathf.Min(nextMin, nextTick);
                nextMax = Mathf.Max(nextMax, nextTick);
                break;
            }
        }

        if (tacticalSlots == 0)
            return "brains=0; tacticalSlots=0";
        return "brains=" + brainCount
            + "; tacticalSlots=" + tacticalSlots
            + "; runs=" + runs
            + "; peakMs=" + peakMs.ToString("0.000")
            + "; lastMaxMs=" + lastMaxMs.ToString("0.000")
            + "; overBudget=" + overBudget
            + "; nextMin=" + nextMin.ToString("0.000")
            + "; nextMax=" + nextMax.ToString("0.000");
    }

    private static SchedulerProbe BuildSchedulerProbe(Type brainType)
    {
        var probe = new SchedulerProbe();
        FieldInfo schedulerField = brainType.GetField("_scheduler", Members);
        if (schedulerField == null)
            return probe;

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            MonoBehaviour behaviour = behaviours[behaviourIndex];
            if (behaviour == null
                || behaviour.GetType() != brainType
                || !behaviour.isActiveAndEnabled)
                continue;

            object scheduler = schedulerField.GetValue(behaviour);
            if (scheduler == null)
                continue;
            FieldInfo slotsField = scheduler.GetType().GetField("_slots", Members);
            if (slotsField == null)
                continue;
            System.Collections.IEnumerable slots = slotsField.GetValue(scheduler)
                as System.Collections.IEnumerable;
            if (slots == null)
                continue;

            probe.ActiveBrains++;
            foreach (object slot in slots)
            {
                if (slot == null)
                    continue;
                Type slotType = slot.GetType();
                string name = Convert.ToString(ReadMember(slot, slotType, "Name"));
                if (!IsHitchProbeModule(name))
                    continue;

                probe.Slots.Add(new SchedulerSlotProbe
                {
                    Slot = slot,
                    Name = name,
                    RunCountField = slotType.GetField("RunCount", Members),
                    LastCostMsField = slotType.GetField("LastCostMs", Members),
                    PeakCostMsField = slotType.GetField("PeakCostMs", Members)
                });
            }
        }

        return probe;
    }

    private static bool IsHitchProbeModule(string name)
    {
        return string.Equals(name, "IA_BuildDirector", StringComparison.Ordinal)
               || string.Equals(name, "IA_TacticalDirector", StringComparison.Ordinal)
               || string.Equals(name, "IA_WorldState", StringComparison.Ordinal)
               || string.Equals(name, "IA_ProductionDirector", StringComparison.Ordinal)
               || string.Equals(name, "IA_NavalDirector", StringComparison.Ordinal);
    }

    private static string ReadHitchSchedulerSnapshot(SchedulerProbe probe)
    {
        int buildRuns = 0;
        int tacticalRuns = 0;
        int worldRuns = 0;
        int productionRuns = 0;
        int navalRuns = 0;
        float buildLastMaxMs = 0f;
        float tacticalLastMaxMs = 0f;
        float worldLastMaxMs = 0f;
        float productionLastMaxMs = 0f;
        float navalLastMaxMs = 0f;
        float buildPeakMaxMs = 0f;
        float tacticalPeakMaxMs = 0f;
        float worldPeakMaxMs = 0f;
        float productionPeakMaxMs = 0f;
        float navalPeakMaxMs = 0f;
        if (probe == null)
            return "activeBrains=0; schedulerProbe=null";

        for (int index = 0; index < probe.Slots.Count; index++)
        {
            SchedulerSlotProbe slot = probe.Slots[index];
            if (slot == null || slot.Slot == null)
                continue;
            int runs = slot.RunCountField != null
                ? Convert.ToInt32(slot.RunCountField.GetValue(slot.Slot))
                : 0;
            float lastMs = slot.LastCostMsField != null
                ? Convert.ToSingle(slot.LastCostMsField.GetValue(slot.Slot))
                : 0f;
            float peakMs = slot.PeakCostMsField != null
                ? Convert.ToSingle(slot.PeakCostMsField.GetValue(slot.Slot))
                : 0f;
            if (string.Equals(slot.Name, "IA_BuildDirector", StringComparison.Ordinal))
            {
                buildRuns += runs;
                buildLastMaxMs = Mathf.Max(buildLastMaxMs, lastMs);
                buildPeakMaxMs = Mathf.Max(buildPeakMaxMs, peakMs);
            }
            else if (string.Equals(slot.Name, "IA_TacticalDirector", StringComparison.Ordinal))
            {
                tacticalRuns += runs;
                tacticalLastMaxMs = Mathf.Max(tacticalLastMaxMs, lastMs);
                tacticalPeakMaxMs = Mathf.Max(tacticalPeakMaxMs, peakMs);
            }
            else if (string.Equals(slot.Name, "IA_WorldState", StringComparison.Ordinal))
            {
                worldRuns += runs;
                worldLastMaxMs = Mathf.Max(worldLastMaxMs, lastMs);
                worldPeakMaxMs = Mathf.Max(worldPeakMaxMs, peakMs);
            }
            else if (string.Equals(slot.Name, "IA_ProductionDirector", StringComparison.Ordinal))
            {
                productionRuns += runs;
                productionLastMaxMs = Mathf.Max(productionLastMaxMs, lastMs);
                productionPeakMaxMs = Mathf.Max(productionPeakMaxMs, peakMs);
            }
            else if (string.Equals(slot.Name, "IA_NavalDirector", StringComparison.Ordinal))
            {
                navalRuns += runs;
                navalLastMaxMs = Mathf.Max(navalLastMaxMs, lastMs);
                navalPeakMaxMs = Mathf.Max(navalPeakMaxMs, peakMs);
            }
        }

        return "activeBrains=" + probe.ActiveBrains
            + "; buildRuns=" + buildRuns
            + "; buildLastMaxMs=" + buildLastMaxMs.ToString("0.000")
            + "; buildPeakMaxMs=" + buildPeakMaxMs.ToString("0.000")
            + "; tacticalRuns=" + tacticalRuns
            + "; tacticalLastMaxMs=" + tacticalLastMaxMs.ToString("0.000")
            + "; tacticalPeakMaxMs=" + tacticalPeakMaxMs.ToString("0.000")
            + "; worldRuns=" + worldRuns
            + "; worldLastMaxMs=" + worldLastMaxMs.ToString("0.000")
            + "; worldPeakMaxMs=" + worldPeakMaxMs.ToString("0.000")
            + "; productionRuns=" + productionRuns
            + "; productionLastMaxMs=" + productionLastMaxMs.ToString("0.000")
            + "; productionPeakMaxMs=" + productionPeakMaxMs.ToString("0.000")
            + "; navalRuns=" + navalRuns
            + "; navalLastMaxMs=" + navalLastMaxMs.ToString("0.000")
            + "; navalPeakMaxMs=" + navalPeakMaxMs.ToString("0.000");
    }

    private static bool IsRelevantDiagnosticMetric(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (key.StartsWith("ia_backend_catalog_", StringComparison.Ordinal))
            return true;
        if (key.StartsWith("tactical_", StringComparison.Ordinal))
            return true;

        switch (key)
        {
            case "targeting_ms":
            case "sensor_update_ms":
            case "coast_scan_ms":
            case "naval_candidate_ms":
            case "build_execute_ms":
            case "world_refresh_ms":
                return true;
            default:
                return false;
        }
    }

    [UnityTest]
    public IEnumerator FallbackFundacionalRespeitaCooldownDeCincoSegundos()
    {
        AsyncOperation load;
#if UNITY_EDITOR
        load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(ValidationScenePath, LoadSceneMode.Single);
#endif
        Assert.That(load, Is.Not.Null);
        while (!load.isDone) yield return null;
        yield return null;

        Type brainType = ResolveType("IA_BrainMaster");
        FieldInfo buildDirectorField = brainType.GetField("_buildDirector", Members);
        Assert.That(buildDirectorField, Is.Not.Null);

        Type buildDirectorType = ResolveType("IA_BuildDirector");
        FieldInfo cooldownField = buildDirectorType.GetField("_nextFoundationAttemptTime", Members);
        Assert.That(cooldownField, Is.Not.Null);

        var firstDeadlineByBrain = new Dictionary<int, float>();
        var violations = new List<string>();
        const int maxFrames = 420;
        int framesAfterFirstObservation = 0;
        for (int frame = 0; frame < maxFrames; frame++)
        {
            yield return null;
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || behaviour.GetType() != brainType)
                    continue;

                object buildDirector = buildDirectorField.GetValue(behaviour);
                if (buildDirector == null)
                    continue;

                float deadline = Convert.ToSingle(cooldownField.GetValue(buildDirector));
                if (deadline <= 0f)
                    continue;

                int brainId = behaviour.GetInstanceID();
                if (!firstDeadlineByBrain.TryGetValue(brainId, out float firstDeadline))
                {
                    Assert.That(deadline - Time.time, Is.GreaterThanOrEqualTo(4.5f),
                        "O fallback fundacional deve criar cooldown de aproximadamente 5 s.");
                    firstDeadlineByBrain.Add(brainId, deadline);
                    continue;
                }

                if (Time.time < firstDeadline - 0.15f
                    && Mathf.Abs(deadline - firstDeadline) > 0.02f)
                {
                    violations.Add("brain=" + brainId + " deadline mudou antes do prazo: "
                        + firstDeadline.ToString("0.000") + " -> " + deadline.ToString("0.000"));
                }
            }

            if (firstDeadlineByBrain.Count > 0)
                framesAfterFirstObservation++;
            if (framesAfterFirstObservation >= 60)
                break;
        }

        Assert.That(firstDeadlineByBrain.Count, Is.GreaterThan(0),
            "Nenhum BrainMaster executou o fallback fundacional durante a janela.");
        Assert.That(violations, Is.Empty, string.Join("; ", violations));
    }

    [UnityTest]
    public IEnumerator DiagnosticoLocalizaScriptsAusentesNaCenaDeValidacao()
    {
        AsyncOperation load;
#if UNITY_EDITOR
        load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(ValidationScenePath, LoadSceneMode.Single);
#endif
        Assert.That(load, Is.Not.Null);
        while (!load.isDone) yield return null;
        yield return null;

        for (int frame = 0; frame < 120; frame++)
            yield return null;

        int missingCount = 0;
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            GameObject gameObject = objects[objectIndex];
            if (gameObject == null)
                continue;

            Component[] components = gameObject.GetComponents<Component>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] != null)
                    continue;

                missingCount++;
                Debug.LogWarning("[IA03MISSING_SCRIPT] path=" + GetHierarchyPath(gameObject)
                    + " | componentIndex=" + componentIndex);
            }
        }

        Debug.Log("[IA03MISSING_SCRIPT_SUMMARY] scene=" + ValidationScenePath
            + " | count=" + missingCount);

        yield return null;
    }

    private sealed class SchedulerSlotProbe
    {
        public object Slot;
        public string Name;
        public FieldInfo RunCountField;
        public FieldInfo LastCostMsField;
        public FieldInfo PeakCostMsField;
    }

    private sealed class SchedulerProbe
    {
        public int ActiveBrains;
        public readonly List<SchedulerSlotProbe> Slots = new List<SchedulerSlotProbe>();
    }

    private sealed class WindowMetrics
    {
        public float meanFrameMs;
        public float p95FrameMs;
        public float peakFrameMs;
        public double mainThreadMeanMs = -1d;
        public long gcThreadBytes;
        public long monoDeltaBytes;
        public long unityDeltaBytes;
        public int framesOver50Ms;
        public int framesOver100Ms;
        public long peakFrameGcAllocBytes;
        public long peakFrameMonoDeltaBytes;
        public long peakFrameProfilerGcAllocBytes = -1L;
        public bool profilerGcRecorderValid;
        public string peakFrameSchedulerSnapshot = string.Empty;
        public string peakGcSchedulerSnapshot = string.Empty;
    }

    private sealed class SpikeMarkerRecorder
    {
        public readonly string Name;
        public readonly ProfilerRecorder Recorder;

        public SpikeMarkerRecorder(string name, ProfilerRecorder recorder)
        {
            Name = name;
            Recorder = recorder;
        }
    }

    private static IEnumerator CaptureWindow(
        WindowMetrics metrics,
        SchedulerProbe schedulerProbe = null,
        int frameCount = MeasuredFrames)
    {
        ProfilerRecorder mainThreadRecorder = default;
        bool recorderStarted = false;
        ProfilerRecorder gcAllocRecorder = default;
        bool gcAllocRecorderStarted = false;
        try
        {
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            recorderStarted = mainThreadRecorder.Valid;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[IA03PERF][RUNTIME] Main Thread recorder indisponivel: " + exception.Message);
        }

        try
        {
            gcAllocRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                15);
            gcAllocRecorderStarted = gcAllocRecorder.Valid;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[IA03PERF][RUNTIME] GC Alloc recorder indisponivel: " + exception.Message);
        }
        metrics.profilerGcRecorderValid = gcAllocRecorderStarted;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long gcBefore = GC.GetAllocatedBytesForCurrentThread();
        long managedBefore = Profiler.GetMonoUsedSizeLong();
        long unityBefore = Profiler.GetTotalAllocatedMemoryLong();
        int capturedFrames = Mathf.Max(1, frameCount);
        var frameTimes = new List<float>(capturedFrames);
        double mainThreadMsTotal = 0d;
        var stopwatch = new Stopwatch();

        for (int frame = 0; frame < capturedFrames; frame++)
        {
            long frameGcBefore = GC.GetAllocatedBytesForCurrentThread();
            long frameMonoBefore = Profiler.GetMonoUsedSizeLong();
            stopwatch.Restart();
            yield return null;
            stopwatch.Stop();
            float frameMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            long frameGcDelta = GC.GetAllocatedBytesForCurrentThread() - frameGcBefore;
            long frameMonoDelta = Profiler.GetMonoUsedSizeLong() - frameMonoBefore;
            long frameProfilerGcDelta = gcAllocRecorderStarted ? gcAllocRecorder.LastValue : -1L;
            bool newFramePeak = frameMs >= metrics.peakFrameMs;
            bool newGcPeak = frameProfilerGcDelta >= metrics.peakFrameProfilerGcAllocBytes;
            bool hitchCandidate = frameMs > 50f
                                  || frameProfilerGcDelta >= 64 * 1024L
                                  || newFramePeak
                                  || newGcPeak;
            string hitchSchedulerSnapshot = hitchCandidate && schedulerProbe != null
                ? ReadHitchSchedulerSnapshot(schedulerProbe)
                : string.Empty;
            frameTimes.Add(frameMs);
            if (frameMs > 50f)
                metrics.framesOver50Ms++;
            if (frameMs > 100f)
                metrics.framesOver100Ms++;
            if (newFramePeak)
            {
                metrics.peakFrameMs = frameMs;
                metrics.peakFrameGcAllocBytes = frameGcDelta;
                metrics.peakFrameMonoDeltaBytes = frameMonoDelta;
                metrics.peakFrameProfilerGcAllocBytes = frameProfilerGcDelta;
                metrics.peakFrameSchedulerSnapshot = hitchSchedulerSnapshot;
            }
            if (newGcPeak && !string.IsNullOrEmpty(hitchSchedulerSnapshot))
            {
                metrics.peakGcSchedulerSnapshot = hitchSchedulerSnapshot;
            }
            if (mainThreadRecorder.Valid)
                mainThreadMsTotal += mainThreadRecorder.LastValue / 1000000d;
        }

        long gcAfter = GC.GetAllocatedBytesForCurrentThread();
        long managedAfter = Profiler.GetMonoUsedSizeLong();
        long unityAfter = Profiler.GetTotalAllocatedMemoryLong();
        frameTimes.Sort();
        float meanMs = 0f;
        for (int i = 0; i < frameTimes.Count; i++) meanMs += frameTimes[i];
        metrics.meanFrameMs = meanMs / Mathf.Max(1, frameTimes.Count);
        metrics.p95FrameMs = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Count * 0.95f) - 1, 0, frameTimes.Count - 1)];
        metrics.peakFrameMs = frameTimes[frameTimes.Count - 1];
        metrics.mainThreadMeanMs = recorderStarted ? mainThreadMsTotal / capturedFrames : -1d;
        metrics.gcThreadBytes = gcAfter - gcBefore;
        metrics.monoDeltaBytes = managedAfter - managedBefore;
        metrics.unityDeltaBytes = unityAfter - unityBefore;
        if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
        if (gcAllocRecorder.Valid) gcAllocRecorder.Dispose();
    }

    private static List<Behaviour> FindBrainBehaviours(Type brainType)
    {
        var result = new List<Behaviour>();
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType() == brainType)
                result.Add(behaviour);
        }
        return result;
    }

    private static void SetBrainEnabled(List<Behaviour> brains, bool enabled)
    {
        for (int i = 0; i < brains.Count; i++)
        {
            if (brains[i] != null)
                brains[i].enabled = enabled;
        }
    }

    private static void SetBrainEnabled(List<Behaviour> brains, List<bool> enabledStates)
    {
        for (int i = 0; i < brains.Count && i < enabledStates.Count; i++)
        {
            if (brains[i] != null)
                brains[i].enabled = enabledStates[i];
        }
    }

    private static IEnumerator CaptureFreshSceneProfileWindow(
        Type brainType,
        Type strategistType,
        Type profileType,
        string label,
        bool disableExistingBrains,
        int extraBrainCount,
        bool disableBootstrapForExtraBrains,
        int warmupFrames,
        int measuredFrames)
    {
        yield return LoadValidationScene();

        Component runtimeDiagnostic = CreateRuntimeDiagnostic();
        var temporaryBrains = new List<GameObject>(Mathf.Max(0, extraBrainCount));
        var profiles = new List<ScriptableObject>(Mathf.Max(0, extraBrainCount));
        List<Behaviour> existingBrains = FindBrainBehaviours(brainType);
        var originalEnabledStates = new List<bool>(existingBrains.Count);
        for (int index = 0; index < existingBrains.Count; index++)
            originalEnabledStates.Add(existingBrains[index].enabled);

        try
        {
            if (disableExistingBrains)
                SetBrainEnabled(existingBrains, false);

            for (int index = 0; index < extraBrainCount; index++)
            {
                GameObject brainObject = new GameObject("IA03_RuntimePerf_AB_Team_" + (2101 + index));
                brainObject.SetActive(false);
                temporaryBrains.Add(brainObject);

                Component brain = brainObject.AddComponent(brainType);
                SetMember(brain, "TeamId", 2101 + index);
                if (disableBootstrapForExtraBrains)
                    SetMember(brain, "UseScriptedBootstrap", false);

                Component strategist = brainObject.AddComponent(strategistType);
                ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
                profiles.Add(profile);
                SetMember(profile, "intervaloDecisaoSegundos", 5f);
                SetMember(strategist, "perfilPais", profile);
                SetMember(strategist, "ativo", true);
                SetMember(strategist, "brain", brain);
                brainObject.SetActive(true);
            }

            for (int frame = 0; frame < 2; frame++)
                yield return null;

            SchedulerProbe schedulerProbe = BuildSchedulerProbe(brainType);
            ResetDiagnosticAccumulators();
            for (int frame = 0; frame < Mathf.Max(0, warmupFrames); frame++)
                yield return null;

            var metrics = new WindowMetrics();
            yield return CaptureWindow(metrics, schedulerProbe, measuredFrames);
            int operationalBrains = CountOperationalBrains(brainType);
            AppendFreshProfileOutput(
                label,
                operationalBrains,
                metrics,
                ReadDiagnosticMetrics(),
                ReadTacticalSchedulerMetrics(brainType),
                warmupFrames,
                measuredFrames);
            Debug.Log(FormatMetrics(label, operationalBrains, metrics, warmupFrames, measuredFrames));
        }
        finally
        {
            SetBrainEnabled(existingBrains, originalEnabledStates);
            for (int index = 0; index < temporaryBrains.Count; index++)
            {
                if (temporaryBrains[index] != null)
                    UnityEngine.Object.Destroy(temporaryBrains[index]);
            }
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] != null)
                    UnityEngine.Object.Destroy(profiles[index]);
            }

            if (runtimeDiagnostic != null)
            {
                MethodInfo setCaptureMode = runtimeDiagnostic.GetType().GetMethod("SetCaptureMode", Members);
                if (setCaptureMode != null)
                    setCaptureMode.Invoke(runtimeDiagnostic, new object[] { false, false });
                UnityEngine.Object.Destroy(runtimeDiagnostic.gameObject);
            }
        }

        yield return null;
    }

    private static IEnumerator CaptureFreshSceneProfile(
        Type brainType,
        Type strategistType,
        Type profileType,
        string label,
        bool disableExistingBrains,
        int extraBrainCount,
        bool disableBootstrapForExtraBrains,
        params string[] suppressedModuleNames)
    {
        yield return LoadValidationScene();

        Component runtimeDiagnostic = CreateRuntimeDiagnostic();

        var temporaryBrains = new List<GameObject>(Mathf.Max(0, extraBrainCount));
        var profiles = new List<ScriptableObject>(Mathf.Max(0, extraBrainCount));
        List<Behaviour> existingBrains = FindBrainBehaviours(brainType);
        var originalEnabledStates = new List<bool>(existingBrains.Count);
        for (int index = 0; index < existingBrains.Count; index++)
            originalEnabledStates.Add(existingBrains[index].enabled);

        List<SuppressedModuleState> suppressedModules = null;
        try
        {
            if (disableExistingBrains)
                SetBrainEnabled(existingBrains, false);

            for (int index = 0; index < extraBrainCount; index++)
            {
                GameObject brainObject = new GameObject("IA03_RuntimePerf_Fresh_Team_" + (2001 + index));
                brainObject.SetActive(false);
                temporaryBrains.Add(brainObject);

                Component brain = brainObject.AddComponent(brainType);
                SetMember(brain, "TeamId", 2001 + index);
                if (disableBootstrapForExtraBrains)
                    SetMember(brain, "UseScriptedBootstrap", false);

                Component strategist = brainObject.AddComponent(strategistType);
                ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
                profiles.Add(profile);
                SetMember(profile, "intervaloDecisaoSegundos", 5f);
                SetMember(strategist, "perfilPais", profile);
                SetMember(strategist, "ativo", true);
                SetMember(strategist, "brain", brain);
                brainObject.SetActive(true);
            }

            // Deixa cada BrainMaster criar o scheduler e registrar os módulos antes da supressão.
            for (int frame = 0; frame < 2; frame++) yield return null;
            if (suppressedModuleNames != null && suppressedModuleNames.Length > 0)
            {
                suppressedModules = SuppressSchedulerModules(brainType, suppressedModuleNames);
                Assert.That(suppressedModules.Count, Is.GreaterThan(0),
                    "Nenhum slot foi encontrado para o perfil isolado: " + label);
            }

            SchedulerProbe schedulerProbe = BuildSchedulerProbe(brainType);
            ResetDiagnosticAccumulators();
            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;
            var metrics = new WindowMetrics();
            yield return CaptureWindow(metrics, schedulerProbe);
            int operationalBrains = CountOperationalBrains(brainType);
            AppendFreshProfileOutput(
                label,
                operationalBrains,
                metrics,
                ReadDiagnosticMetrics(),
                ReadTacticalSchedulerMetrics(brainType));
            Debug.Log(FormatMetrics(label, operationalBrains, metrics));
        }
        finally
        {
            RestoreSchedulerModules(suppressedModules);
            SetBrainEnabled(existingBrains, originalEnabledStates);
            for (int index = 0; index < temporaryBrains.Count; index++)
            {
                if (temporaryBrains[index] != null)
                    UnityEngine.Object.Destroy(temporaryBrains[index]);
            }
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] != null)
                    UnityEngine.Object.Destroy(profiles[index]);
            }

            if (runtimeDiagnostic != null)
            {
                MethodInfo setCaptureMode = runtimeDiagnostic.GetType().GetMethod("SetCaptureMode", Members);
                if (setCaptureMode != null)
                    setCaptureMode.Invoke(runtimeDiagnostic, new object[] { false, false });
                UnityEngine.Object.Destroy(runtimeDiagnostic.gameObject);
            }
        }

        yield return null;
    }

    private static Component CreateRuntimeDiagnostic()
    {
        Type diagnosticType = ResolveType("DiagnosticoDesempenhoJogo");
        Assert.That(diagnosticType, Is.Not.Null, "DiagnosticoDesempenhoJogo nao foi encontrado.");

        GameObject host = new GameObject("IA03_RuntimePerf_Diagnostic");
        host.SetActive(false);
        Component diagnostic = host.AddComponent(diagnosticType);
        SetMember(diagnostic, "persistirEntreCenas", false);
        host.SetActive(true);

        MethodInfo setCaptureMode = diagnosticType.GetMethod("SetCaptureMode", Members);
        Assert.That(setCaptureMode, Is.Not.Null, "SetCaptureMode nao foi encontrado no diagnostico.");
        setCaptureMode.Invoke(diagnostic, new object[] { true, false });
        return diagnostic;
    }

    private static IEnumerator LoadValidationScene()
    {
        AsyncOperation load;
#if UNITY_EDITOR
        load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        load = SceneManager.LoadSceneAsync(ValidationScenePath, LoadSceneMode.Single);
#endif
        Assert.That(load, Is.Not.Null);
        while (!load.isDone) yield return null;
        yield return null;
    }

    private static int CountOperationalBrains(Type brainType)
    {
        int count = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null
                && behaviour.GetType() == brainType
                && behaviour.enabled
                && behaviour.gameObject.activeInHierarchy)
            {
                count++;
            }
        }
        return count;
    }

    private static string FormatMetrics(
        string label,
        int brains,
        WindowMetrics metrics,
        int warmupFrames = WarmupFrames,
        int measuredFrames = MeasuredFrames)
    {
        return string.Format(
            "[IA03PERF][RUNTIME] label={0}; brains={1}; warmupFrames={2}; measuredFrames={3}; meanFrameMs={4:0.000}; p95FrameMs={5:0.000}; peakFrameMs={6:0.000}; mainThreadMs={7:0.000}; gcThreadBytes={8}; monoDeltaBytes={9}; unityDeltaBytes={10}; framesOver50Ms={11}; framesOver100Ms={12}; peakFrameGcAllocBytes={13}; peakFrameMonoDeltaBytes={14}; profilerGcRecorderValid={15}; peakFrameProfilerGcAllocBytes={16}; peakFrameSchedulerSnapshot={17}; peakGcSchedulerSnapshot={18}",
            label,
            brains,
            warmupFrames,
            measuredFrames,
            metrics.meanFrameMs,
            metrics.p95FrameMs,
            metrics.peakFrameMs,
            metrics.mainThreadMeanMs,
            metrics.gcThreadBytes,
            metrics.monoDeltaBytes,
            metrics.unityDeltaBytes,
            metrics.framesOver50Ms,
            metrics.framesOver100Ms,
            metrics.peakFrameGcAllocBytes,
            metrics.peakFrameMonoDeltaBytes,
            metrics.profilerGcRecorderValid,
            metrics.peakFrameProfilerGcAllocBytes,
            CompactSchedulerSnapshot(metrics.peakFrameSchedulerSnapshot),
            CompactSchedulerSnapshot(metrics.peakGcSchedulerSnapshot));
    }

    private static string CompactSchedulerSnapshot(string snapshot)
    {
        return string.IsNullOrEmpty(snapshot)
            ? "<none>"
            : snapshot.Replace(";", ",");
    }

    private sealed class ModuleAggregate
    {
        public string Name;
        public int Brains;
        public int Runs;
        public int OverBudget;
        public float PeakMs;
        public float LastMaxMs;
    }

    private static string FormatSchedulerBreakdown(string label, Type brainType)
    {
        var aggregates = new Dictionary<string, ModuleAggregate>(StringComparer.Ordinal);
        FieldInfo schedulerField = brainType.GetField("_scheduler", Members);
        if (schedulerField == null)
            return "[IA03PERF][MODULE] label=" + label + "; schedulerField=missing";

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            MonoBehaviour behaviour = behaviours[behaviourIndex];
            if (behaviour == null || behaviour.GetType() != brainType) continue;
            object scheduler = schedulerField.GetValue(behaviour);
            if (scheduler == null) continue;
            MethodInfo getSnapshot = scheduler.GetType().GetMethod("GetSnapshot", Members);
            if (getSnapshot == null) continue;
            object snapshotObject = getSnapshot.Invoke(scheduler, null);
            System.Collections.IEnumerable snapshot = snapshotObject as System.Collections.IEnumerable;
            if (snapshot == null) continue;

            var seenForBrain = new HashSet<string>(StringComparer.Ordinal);
            foreach (object slot in snapshot)
            {
                if (slot == null) continue;
                Type slotType = slot.GetType();
                string name = Convert.ToString(ReadMember(slot, slotType, "Name"));
                if (string.IsNullOrEmpty(name)) continue;
                if (!aggregates.TryGetValue(name, out ModuleAggregate aggregate))
                {
                    aggregate = new ModuleAggregate { Name = name };
                    aggregates.Add(name, aggregate);
                }

                if (seenForBrain.Add(name)) aggregate.Brains++;
                aggregate.Runs += Convert.ToInt32(ReadMember(slot, slotType, "RunCount"));
                aggregate.OverBudget += Convert.ToInt32(ReadMember(slot, slotType, "OverBudgetCount"));
                aggregate.PeakMs = Mathf.Max(aggregate.PeakMs,
                    Convert.ToSingle(ReadMember(slot, slotType, "PeakCostMs")));
                aggregate.LastMaxMs = Mathf.Max(aggregate.LastMaxMs,
                    Convert.ToSingle(ReadMember(slot, slotType, "LastCostMs")));
            }
        }

        var ordered = new List<ModuleAggregate>(aggregates.Values);
        ordered.Sort((left, right) => right.PeakMs.CompareTo(left.PeakMs));
        var builder = new StringBuilder(ordered.Count * 120);
        builder.Append("[IA03PERF][MODULE] label=").Append(label)
            .Append("; modules=").Append(ordered.Count);
        for (int index = 0; index < ordered.Count; index++)
        {
            ModuleAggregate aggregate = ordered[index];
            builder.Append(" | ").Append(aggregate.Name)
                .Append(" brains=").Append(aggregate.Brains)
                .Append(" runs=").Append(aggregate.Runs)
                .Append(" peakMs=").Append(aggregate.PeakMs.ToString("0.000"))
                .Append(" lastMaxMs=").Append(aggregate.LastMaxMs.ToString("0.000"))
                .Append(" overBudget=").Append(aggregate.OverBudget);
        }
        return builder.ToString();
    }

    private sealed class ModuleWindowAggregate
    {
        public string Name;
        public int Runs;
        public int OverBudget;
        public double TotalCostMs;
        public float PeakCostMs;
    }

    private sealed class SuppressedModuleState
    {
        public object Slot;
        public FieldInfo NextTickField;
        public FieldInfo SliceInProgressField;
        public float NextTick;
        public bool SliceInProgress;
    }

    private static List<SuppressedModuleState> SuppressSchedulerModules(
        Type brainType,
        params string[] moduleNames)
    {
        var suppressed = new List<SuppressedModuleState>();
        var names = new HashSet<string>(moduleNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        FieldInfo schedulerField = brainType.GetField("_scheduler", Members);
        if (schedulerField == null)
            return suppressed;

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            MonoBehaviour behaviour = behaviours[behaviourIndex];
            if (behaviour == null || behaviour.GetType() != brainType)
                continue;

            object scheduler = schedulerField.GetValue(behaviour);
            if (scheduler == null)
                continue;

            FieldInfo slotsField = scheduler.GetType().GetField("_slots", Members);
            if (slotsField == null)
                continue;

            System.Collections.IEnumerable slots = slotsField.GetValue(scheduler) as System.Collections.IEnumerable;
            if (slots == null)
                continue;

            foreach (object slot in slots)
            {
                if (slot == null)
                    continue;

                Type slotType = slot.GetType();
                object module = ReadMember(slot, slotType, "Module");
                string moduleName = module != null
                    ? Convert.ToString(ReadMember(module, module.GetType(), "Name"))
                    : string.Empty;
                if (!names.Contains(moduleName))
                    continue;

                FieldInfo nextTickField = slotType.GetField("NextTick", Members);
                FieldInfo sliceInProgressField = slotType.GetField("SliceInProgress", Members);
                if (nextTickField == null || sliceInProgressField == null)
                    continue;

                suppressed.Add(new SuppressedModuleState
                {
                    Slot = slot,
                    NextTickField = nextTickField,
                    SliceInProgressField = sliceInProgressField,
                    NextTick = Convert.ToSingle(nextTickField.GetValue(slot)),
                    SliceInProgress = Convert.ToBoolean(sliceInProgressField.GetValue(slot))
                });
                nextTickField.SetValue(slot, Time.time + 3600f);
                sliceInProgressField.SetValue(slot, false);
            }
        }

        return suppressed;
    }

    private static void RestoreSchedulerModules(List<SuppressedModuleState> suppressed)
    {
        if (suppressed == null)
            return;

        for (int index = 0; index < suppressed.Count; index++)
        {
            SuppressedModuleState state = suppressed[index];
            if (state == null || state.Slot == null)
                continue;

            state.NextTickField.SetValue(state.Slot, state.NextTick);
            state.SliceInProgressField.SetValue(state.Slot, state.SliceInProgress);
        }
    }

    private static IEnumerator CaptureSuppressedModuleWindow(
        Type brainType,
        string label,
        params string[] moduleNames)
    {
        List<SuppressedModuleState> suppressed = SuppressSchedulerModules(brainType, moduleNames);
        try
        {
            Assert.That(suppressed.Count, Is.GreaterThan(0),
                "Nenhum slot foi encontrado para o perfil isolado: " + label);
            for (int frame = 0; frame < WarmupFrames; frame++) yield return null;
            var metrics = new WindowMetrics();
            yield return CaptureWindow(metrics, BuildSchedulerProbe(brainType));
            Debug.Log(FormatMetrics(label, CountOperationalBrains(brainType), metrics));
        }
        finally
        {
            RestoreSchedulerModules(suppressed);
        }
    }

    private static IEnumerator CaptureModuleWindow(
        Type brainType,
        int frames,
        Dictionary<string, ModuleWindowAggregate> aggregates)
    {
        FieldInfo schedulerField = brainType.GetField("_scheduler", Members);
        var previousRuns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int frame = 0; frame < frames; frame++)
        {
            yield return null;
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || behaviour.GetType() != brainType) continue;
                object scheduler = schedulerField != null ? schedulerField.GetValue(behaviour) : null;
                if (scheduler == null) continue;
                MethodInfo getSnapshot = scheduler.GetType().GetMethod("GetSnapshot", Members);
                if (getSnapshot == null) continue;
                System.Collections.IEnumerable snapshot = getSnapshot.Invoke(scheduler, null) as System.Collections.IEnumerable;
                if (snapshot == null) continue;

                int brainId = behaviour.GetInstanceID();
                foreach (object slot in snapshot)
                {
                    if (slot == null) continue;
                    Type slotType = slot.GetType();
                    string name = Convert.ToString(ReadMember(slot, slotType, "Name"));
                    if (string.IsNullOrEmpty(name)) continue;
                    string key = brainId.ToString() + "|" + name;
                    int runCount = Convert.ToInt32(ReadMember(slot, slotType, "RunCount"));
                    if (!previousRuns.TryGetValue(key, out int previousRunCount))
                        previousRunCount = runCount;
                    int deltaRuns = Mathf.Max(0, runCount - previousRunCount);
                    previousRuns[key] = runCount;
                    if (deltaRuns <= 0) continue;

                    if (!aggregates.TryGetValue(name, out ModuleWindowAggregate aggregate))
                    {
                        aggregate = new ModuleWindowAggregate { Name = name };
                        aggregates.Add(name, aggregate);
                    }

                    float lastCostMs = Convert.ToSingle(ReadMember(slot, slotType, "LastCostMs"));
                    int overBudget = Convert.ToInt32(ReadMember(slot, slotType, "OverBudgetCount"));
                    aggregate.Runs += deltaRuns;
                    aggregate.TotalCostMs += lastCostMs * deltaRuns;
                    aggregate.PeakCostMs = Mathf.Max(aggregate.PeakCostMs, lastCostMs);
                    aggregate.OverBudget = Mathf.Max(aggregate.OverBudget, overBudget);
                }
            }
        }
    }

    private static string FormatModuleWindow(Dictionary<string, ModuleWindowAggregate> aggregates)
    {
        var ordered = new List<ModuleWindowAggregate>(aggregates.Values);
        ordered.Sort((left, right) => right.TotalCostMs.CompareTo(left.TotalCostMs));
        var builder = new StringBuilder(ordered.Count * 120);
        builder.Append("[IA03PERF][MODULE_WINDOW] frames=120; modules=").Append(ordered.Count);
        for (int index = 0; index < ordered.Count; index++)
        {
            ModuleWindowAggregate aggregate = ordered[index];
            double meanCost = aggregate.Runs > 0 ? aggregate.TotalCostMs / aggregate.Runs : 0d;
            builder.Append(" | ").Append(aggregate.Name)
                .Append(" runs=").Append(aggregate.Runs)
                .Append(" totalMs=").Append(aggregate.TotalCostMs.ToString("0.000"))
                .Append(" meanMs=").Append(meanCost.ToString("0.000"))
                .Append(" peakMs=").Append(aggregate.PeakCostMs.ToString("0.000"))
                .Append(" overBudget=").Append(aggregate.OverBudget);
        }
        return builder.ToString();
    }

    private static IEnumerator CaptureBuildDirectorProfiling(Type brainType, int frames)
    {
        FieldInfo buildDirectorField = brainType.GetField("_buildDirector", Members);
        PropertyInfo summaryProperty = null;
        var lastByBrain = new Dictionary<int, string>();
        var summaries = new HashSet<string>(StringComparer.Ordinal);

        for (int frame = 0; frame < frames; frame++)
        {
            yield return null;
            if (buildDirectorField == null)
                continue;

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || behaviour.GetType() != brainType)
                    continue;

                object buildDirector = buildDirectorField.GetValue(behaviour);
                if (buildDirector == null)
                    continue;

                if (summaryProperty == null)
                {
                    summaryProperty = buildDirector.GetType().GetProperty("LastProfilingSummary", Members);
                    if (summaryProperty == null)
                        break;
                }

                string summary = Convert.ToString(summaryProperty.GetValue(buildDirector, null));
                if (string.IsNullOrEmpty(summary))
                    continue;

                int brainId = behaviour.GetInstanceID();
                if (lastByBrain.TryGetValue(brainId, out string previous) && string.Equals(previous, summary, StringComparison.Ordinal))
                    continue;

                lastByBrain[brainId] = summary;
                if (summaries.Add(summary))
                {
                    FieldInfo teamField = brainType.GetField("TeamId", Members);
                    object team = teamField != null ? teamField.GetValue(behaviour) : null;
                    Debug.Log("[IA03PERF][BUILD_PROFILE] team=" + Convert.ToString(team) + " | " + summary);
                }
            }
        }

        Debug.Log("[IA03PERF][BUILD_PROFILE_SUMMARY] frames=" + frames + "; unique=" + summaries.Count);
    }

    private static object ReadMember(object target, Type type, string name)
    {
        FieldInfo field = type.GetField(name, Members);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, Members);
        return property != null ? property.GetValue(target, null) : null;
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        if (gameObject == null)
            return "<null>";

        var path = new StringBuilder(gameObject.name);
        Transform current = gameObject.transform.parent;
        while (current != null)
        {
            path.Insert(0, current.name + "/");
            current = current.parent;
        }

        return path.ToString();
    }

    private static Type ResolveType(string simpleName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type direct = assemblies[assemblyIndex].GetType(simpleName, false);
            if (direct != null) return direct;
            Type[] types;
            try { types = assemblies[assemblyIndex].GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                if (types[typeIndex] != null && types[typeIndex].Name == simpleName)
                    return types[typeIndex];
            }
        }
        Assert.Fail("Tipo não encontrado: " + simpleName);
        return null;
    }

    private static void SetMember(object target, string memberName, object value)
    {
        Assert.That(target, Is.Not.Null);
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, Members);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, Members);
        Assert.That(property, Is.Not.Null, "Membro não encontrado: " + memberName);
        property.SetValue(target, value, null);
    }
}
