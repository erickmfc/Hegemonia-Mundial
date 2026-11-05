using System;
using System.Globalization;
using System.IO;
using Process = System.Diagnostics.Process;
using System.Text;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.IA03;

/// <summary>Optional local overlay for profiling the map scene.</summary>
public sealed class GlobalWorldDiagnostics : MonoBehaviour
{
    private static int ia03BuildCount;
    private static int ia03PurchaseCount;
    private static int ia03CommandAttempts;
    private static int ia03BuildFailures;
    private static int ia03PurchaseFailures;
    private static int ia03PlanningFailures;
    private static int ia03UniquePlanningFailureCauses;
    private static int ia03RepeatedPlanningFailures;
    private static int ia03ErrorCount;
    private static long ia03LastUpdateAllocatedBytes;
    private static long ia03PeakUpdateAllocatedBytes;
    private static long ia03UpdateSampleCount;
    private static double ia03AverageUpdateAllocatedBytes;
    private static double ia03LastUpdateCpuMilliseconds;
    private static double ia03PeakUpdateCpuMilliseconds;
    private static double ia03AverageUpdateCpuMilliseconds;
    private static readonly Dictionary<string, int> ia03BuildsByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> ia03PurchasesByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ia03PlanningFailureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string ia03LastAction = "aguardando ordens";
    private static string ia03LastError = "sem falhas registradas";
    private static string ia03LastErrorSource = "n/d";
    private static string ia03LastRetryCondition = "n/d";

    public static void RegisterIA03Attempt(string action, string item, int attempt)
    {
        ia03CommandAttempts++;
        ia03LastAction = action + " " + item + " (tentativa " + Mathf.Max(1, attempt) + ")";
    }

    public static void RegisterIA03BuildResult(string item, bool success, string reason)
    {
        if (success)
        {
            ia03BuildCount++;
            IncrementIA03ItemCount(ia03BuildsByItem, item, 1);
            ia03LastAction = "construiu " + item;
            return;
        }

        ia03BuildFailures++;
        RegisterIA03Error(
            "IA_BackendBridge/BuildService",
            "build " + item + ": " + reason,
            "corrija o motivo da BuildService; a fila tentará novamente conforme cooldown e orçamento.");
    }

    public static void RegisterIA03PurchaseResult(string item, int quantity, bool success, string reason)
    {
        if (success)
        {
            int purchased = Mathf.Max(0, quantity);
            ia03PurchaseCount += purchased;
            IncrementIA03ItemCount(ia03PurchasesByItem, item, purchased);
            ia03LastAction = "comprou " + item + " x" + purchased;
            return;
        }

        ia03PurchaseFailures++;
        RegisterIA03Error(
            "IA_BackendBridge/Production",
            "compra " + item + ": " + reason,
            "corrija o motivo da produção; a fila tentará novamente conforme cooldown e orçamento.");
    }

    private static void RegisterIA03Error(string source, string reason, string retryCondition)
    {
        ia03ErrorCount++;
        ia03LastErrorSource = string.IsNullOrWhiteSpace(source) ? "n/d" : source;
        ia03LastError = string.IsNullOrWhiteSpace(reason) ? "motivo nao informado" : reason;
        ia03LastRetryCondition = string.IsNullOrWhiteSpace(retryCondition) ? "n/d" : retryCondition;
    }

    public static void RegisterIA03PlanningFailure(string item, string reason)
    {
        ia03PlanningFailures++;
        string key = (item ?? string.Empty).Trim() + "|" + (reason ?? string.Empty).Trim();
        if (ia03PlanningFailureKeys.Add(key))
            ia03UniquePlanningFailureCauses++;
        else
            ia03RepeatedPlanningFailures++;
        RegisterIA03Error(
            "IA_BuildDirector",
            "planejamento " + item + ": " + reason,
            ResolveIA03PlanningRetryCondition(item, reason));
    }

    public static void RegisterIA03UpdateSample(long allocatedBytes, double cpuMilliseconds)
    {
        ia03LastUpdateAllocatedBytes = Math.Max(0L, allocatedBytes);
        ia03PeakUpdateAllocatedBytes = Math.Max(ia03PeakUpdateAllocatedBytes, ia03LastUpdateAllocatedBytes);
        ia03LastUpdateCpuMilliseconds = Math.Max(0d, cpuMilliseconds);
        ia03PeakUpdateCpuMilliseconds = Math.Max(ia03PeakUpdateCpuMilliseconds, ia03LastUpdateCpuMilliseconds);
        ia03UpdateSampleCount++;
        ia03AverageUpdateAllocatedBytes +=
            (ia03LastUpdateAllocatedBytes - ia03AverageUpdateAllocatedBytes) / ia03UpdateSampleCount;
        ia03AverageUpdateCpuMilliseconds +=
            (ia03LastUpdateCpuMilliseconds - ia03AverageUpdateCpuMilliseconds) / ia03UpdateSampleCount;
    }

    private static string ResolveIA03PlanningRetryCondition(string item, string reason)
    {
        string context = (item ?? string.Empty) + " " + (reason ?? string.Empty);
        if (context.IndexOf("creaty", StringComparison.OrdinalIgnoreCase) >= 0
            || context.IndexOf("aeroporto militar", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Creaty manual ativo e disponível, Role=ConstrucaoAeroportoMilitar, item alvo correto e área em terra firme.";
        }

        return "remova o bloqueio indicado; nova tentativa segue o cooldown e o próximo ciclo do BuildDirector.";
    }

    private static void IncrementIA03ItemCount(Dictionary<string, int> counts, string item, int amount)
    {
        if (amount <= 0) return;
        string key = string.IsNullOrWhiteSpace(item) ? "item sem nome" : item.Trim();
        int current;
        counts.TryGetValue(key, out current);
        counts[key] = current + amount;
    }

    public GlobalTerrainStreamer streamer;
    public bool visible = true;
    [Min(0.1f)] public float sampleInterval = 1f;
    [Min(1f)] public float activeObjectScanInterval = 5f;
    [Min(1)] public int activeObjectScanBudgetPerFrame = 256;
    [Tooltip("Nome do perfil/distância do teste, por exemplo 5km-floresta-camara-movendo.")]
    public string benchmarkLabel = "250km-parado";
    public KeyCode benchmarkKey = KeyCode.F9;

    private float nextSampleAt;
    private float smoothedFps;
    private long managedBytes;
    private long allocatedBytes;
    private long graphicsBytes;
    private long processWorkingSetBytes;
    private int activeGameObjects;
    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gpuRecorder;
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder batchesRecorder;
    private ProfilerRecorder gcAllocatedRecorder;
    private readonly List<GameObject> sceneRoots = new List<GameObject>(16);
    private readonly List<FrameSample> benchmarkSamples = new List<FrameSample>(36000);
    private readonly List<float> benchmarkWindowFrameMilliseconds = new List<float>(256);
    private const int BenchmarkFrameHistogramMaxMs = 30000;
    private const int BenchmarkFrameHistogramOverflowIndex = BenchmarkFrameHistogramMaxMs + 1;
    private readonly long[] benchmarkFrameMillisecondsHistogram = new long[BenchmarkFrameHistogramOverflowIndex + 1];
    private bool benchmarkRecording;
    private float benchmarkStartedAt;
    private float nextBenchmarkSampleAt;
    private float benchmarkMemorySampleAt = float.NegativeInfinity;
    private int activeUnits;
    private int activeNavalUnits;
    private int activeProjectiles;
    private int activeParticleSystems;
    private int activeTrailRenderers;
    private int benchmarkWindowFrameCount;
    private double benchmarkWindowFrameMillisecondsTotal;
    private float benchmarkWindowWorstFrameMilliseconds;
    private double benchmarkWindowMainThreadNanosecondsTotal;
    private double benchmarkWindowRenderThreadNanosecondsTotal;
    private double benchmarkWindowGpuNanosecondsTotal;
    private int benchmarkWindowMainThreadSamples;
    private int benchmarkWindowRenderThreadSamples;
    private int benchmarkWindowGpuSamples;
    private long benchmarkWindowGcAllocatedBytes;
    private int benchmarkWindowGcAllocatedSamples;
    private long benchmarkWindowDrawCalls = -1;
    private long benchmarkWindowTriangles = -1;
    private long benchmarkWindowBatches = -1;
    private long benchmarkFrameCount;
    private double benchmarkTotalFrameMilliseconds;
    private Process currentProcess;
    private readonly List<Transform> activeObjectScan = new List<Transform>(4096);
    private int activeObjectScanCursor;
    private int activeObjectScanCount;
    private int activeObjectScanUnits;
    private int activeObjectScanNavalUnits;
    private int activeObjectScanProjectiles;
    private int activeObjectScanParticleSystems;
    private int activeObjectScanTrailRenderers;
    private float nextActiveObjectScanAt;
    private bool activeObjectScanInProgress;
    private float nextIA03RefreshAt;
    private IA_BrainMaster cachedIA03Brain;
    private IA03EstrategaNacional cachedIA03Strategist;
    private IA_ForceSnapshot cachedIA03Force;
    private int cachedIA03Queued;
    private int cachedIA03AwaitingConfirmation;
    private float cachedIA03OldestPendingWaitSeconds;
    private string cachedIA03State = "indisponivel";
    private IA_CommandRecord cachedIA03LatestOrder;
    private readonly List<string> cachedIA03HistoryHeaders = new List<string>(128);
    private readonly List<string> cachedIA03HistoryDetails = new List<string>(128);
    private readonly List<float> cachedIA03HistoryRowHeights = new List<float>(128);
    private float cachedIA03HistoryContentHeight;
    private string cachedIA03NewestHistoryKey = string.Empty;
    private GUIStyle ia03CompactLabelStyle;
    private GUIStyle ia03HistoryLabelStyle;
    private Vector2 ia03BuildCountsScrollPosition;
    private Vector2 ia03PurchaseCountsScrollPosition;
    private Vector2 ia03CommandHistoryScrollPosition;

    private struct FrameSample
    {
        public float frameMilliseconds;
        public float p95FrameMilliseconds;
        public float worstFrameMilliseconds;
        public float elapsedSeconds;
        public float timeScale;
        public long mainThreadNanoseconds;
        public long renderThreadNanoseconds;
        public long gpuNanoseconds;
        public long drawCalls;
        public long triangles;
        public long batches;
        public long gcAllocatedBytes;
        public long unityMemoryBytes;
        public long monoMemoryBytes;
        public long graphicsMemoryBytes;
        public long processWorkingSetBytes;
        public int terrainTiles;
        public int trees;
        public int activeObjects;
        public int units;
        public int navalUnits;
        public int projectiles;
        public int particleSystems;
        public int trailRenderers;
        public int tileGenerationMilliseconds;
    }

    private void OnEnable()
    {
        activeObjectScanInProgress = false;
        activeObjectScan.Clear();
        activeObjectScanCursor = 0;
        nextActiveObjectScanAt = 0f;
        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Thread", 15);
        List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>(64);
        ProfilerRecorderHandle.GetAvailable(handles);
        gpuRecorder = StartRecorder(handles, "GPU Frame Time");
        drawCallsRecorder = StartRecorder(handles, "Draw Calls Count");
        trianglesRecorder = StartRecorder(handles, "Triangles Count");
        batchesRecorder = StartRecorder(handles, "Batches Count");
        gcAllocatedRecorder = StartRecorder(handles, "GC Allocated In Frame");
        currentProcess = Process.GetCurrentProcess();
    }

    private void OnDisable()
    {
        if (benchmarkRecording) StopBenchmark();
        activeObjectScanInProgress = false;
        activeObjectScan.Clear();
        activeObjectScanCursor = 0;
        if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
        if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
        if (gpuRecorder.Valid) gpuRecorder.Dispose();
        if (drawCallsRecorder.Valid) drawCallsRecorder.Dispose();
        if (trianglesRecorder.Valid) trianglesRecorder.Dispose();
        if (batchesRecorder.Valid) batchesRecorder.Dispose();
        if (gcAllocatedRecorder.Valid) gcAllocatedRecorder.Dispose();
        if (currentProcess != null) currentProcess.Dispose();
    }

    private void Update()
    {
        if (visible || benchmarkRecording)
        {
            if (!activeObjectScanInProgress && Time.unscaledTime >= nextActiveObjectScanAt)
                BeginActiveObjectScan();
            if (activeObjectScanInProgress)
                AdvanceActiveObjectScan();
        }

        if (Input.GetKeyDown(benchmarkKey))
        {
            if (benchmarkRecording) StopBenchmark();
            else StartBenchmark();
        }

        if (Time.unscaledDeltaTime > 0f)
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f);
            smoothedFps = Mathf.Lerp(smoothedFps <= 0f ? 1f / Time.unscaledDeltaTime : smoothedFps, 1f / Time.unscaledDeltaTime, blend);
        }

        if (Time.unscaledTime >= nextSampleAt)
        {
            nextSampleAt = Time.unscaledTime + sampleInterval;
            managedBytes = Profiler.GetMonoUsedSizeLong();
            allocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            graphicsBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long workingSetBytes = currentProcess != null ? currentProcess.WorkingSet64 : 0L;
            processWorkingSetBytes = workingSetBytes > 0L ? workingSetBytes : -1L;
            benchmarkMemorySampleAt = Time.unscaledTime;
        }

        if (benchmarkRecording) CaptureBenchmarkFrame();
    }

    private void OnGUI()
    {
        if (!visible || streamer == null || benchmarkRecording)
            return;

        Camera camera = Camera.main;
        string mainThread = mainThreadRecorder.Valid ? (mainThreadRecorder.LastValue / 1000000f).ToString("F2") + " ms" : "n/a";
        string renderThread = renderThreadRecorder.Valid ? (renderThreadRecorder.LastValue / 1000000f).ToString("F2") + " ms" : "n/a";
        string altitude = camera != null ? (camera.transform.position.y / 1000f).ToString("F1") + " km" : "n/a";
        const float panelWidth = 440f;
        const float panelHeight = 700f;
        float panelX = Mathf.Max(12f, Screen.width - panelWidth - 12f);
        float panelY = MiniMapa.OverlayVisivel
            ? 12f
            : Mathf.Max(12f, Screen.height - panelHeight - 178f);
        GUI.BeginGroup(new Rect(panelX, panelY, panelWidth, panelHeight));
        GUI.Box(new Rect(0f, 0f, panelWidth, panelHeight), "GLOBAL MAP · PERFORMANCE");
        GUI.Label(new Rect(12f, 30f, 305f, 22f), "FPS: " + smoothedFps.ToString("F1") + "    altitude: " + altitude);
        GUI.Label(new Rect(12f, 52f, 305f, 22f), "terrain tiles: " + streamer.ActiveTileCount + "    trees: " + streamer.ActiveTreeCount);
        GUI.Label(new Rect(12f, 74f, 305f, 22f), "active GameObjects: " + activeGameObjects);
        GUI.Label(new Rect(12f, 96f, 305f, 22f), "last tile CPU work: " + streamer.LastGenerationMilliseconds + " ms total");
        GUI.Label(new Rect(12f, 118f, 305f, 22f), "CPU main/render: " + mainThread + " / " + renderThread);
        GUI.Label(new Rect(12f, 140f, 305f, 22f), "GPU frame: " + ToMilliseconds(gpuRecorder));
        GUI.Label(new Rect(12f, 162f, 305f, 22f), "draw / triangles / batches: "
            + ToCount(drawCallsRecorder) + " / " + ToCount(trianglesRecorder) + " / " + ToCount(batchesRecorder));
        GUI.Label(new Rect(12f, 184f, 305f, 22f), "process Unity / Mono memory: " + ToMegabytes(allocatedBytes) + " / " + ToMegabytes(managedBytes));
        GUI.Label(new Rect(12f, 206f, 305f, 22f), "graphics driver memory: " + ToMegabytes(graphicsBytes));
        GUI.Label(new Rect(12f, 228f, 305f, 22f), benchmarkRecording
            ? "REC  " + benchmarkLabel + "  " + (Time.unscaledTime - benchmarkStartedAt).ToString("F0") + "s · F9 parar"
            : "F9 iniciar/parar CSV · " + benchmarkLabel);
        DrawIA03Diagnostics();
        GUI.EndGroup();
    }

    private void DrawIA03Diagnostics()
    {
        if (ia03CompactLabelStyle == null)
        {
            ia03CompactLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                clipping = TextClipping.Clip
            };
        }
        if (ia03HistoryLabelStyle == null)
        {
            ia03HistoryLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }

        if (Time.unscaledTime >= nextIA03RefreshAt)
        {
            nextIA03RefreshAt = Time.unscaledTime + 1f;
            cachedIA03Brain = null;
            IA_BrainMaster[] brains = FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] != null && brains[i].TeamId == 2)
                {
                    cachedIA03Brain = brains[i];
                    break;
                }
            }

            cachedIA03Strategist = cachedIA03Brain != null
                ? cachedIA03Brain.GetComponent<IA03EstrategaNacional>()
                : null;
            cachedIA03Force = cachedIA03Brain != null && cachedIA03Brain.Context != null
                && cachedIA03Brain.Context.WorldState != null
                ? cachedIA03Brain.Context.WorldState.ForceSnapshot
                : null;
            IA_CommandQueue queue = cachedIA03Brain != null && cachedIA03Brain.Context != null
                ? cachedIA03Brain.Context.CommandQueue
                : null;
            cachedIA03Queued = queue != null ? queue.PendingCount : 0;
            cachedIA03AwaitingConfirmation = queue != null ? queue.AwaitingConfirmationCount : 0;
            cachedIA03OldestPendingWaitSeconds = queue != null
                ? queue.GetOldestPendingWaitSeconds(Time.time)
                : 0f;
            cachedIA03State = cachedIA03Strategist != null
                ? cachedIA03Strategist.EstadoNacional + "/" + cachedIA03Strategist.EstadoDaMissao
                : "indisponivel";
            List<IA_CommandRecord> history = queue != null ? queue.GetRecentHistory(128) : null;
            RefreshIA03CommandHistory(history);
            cachedIA03LatestOrder = GetLatestIA03CommandRecord(history);
        }

        GUI.Label(new Rect(12f, 250f, 415f, 20f), "IA03 total: builds " + ia03BuildCount
            + " | compras " + ia03PurchaseCount + " | efetivo "
            + (cachedIA03Force != null ? cachedIA03Force.TotalOwnUnits : 0) + " un / "
            + (cachedIA03Force != null ? cachedIA03Force.TotalOwnStructures : 0) + " estruturas");
        GUI.Label(new Rect(12f, 270f, 415f, 18f), "construiu (" + ia03BuildsByItem.Count + " tipos) · role para ver todos", ia03CompactLabelStyle);
        DrawIA03ItemCountsList(new Rect(12f, 288f, 415f, 36f), ia03BuildsByItem, ref ia03BuildCountsScrollPosition);
        GUI.Label(new Rect(12f, 326f, 415f, 18f), "comprou (" + ia03PurchasesByItem.Count + " tipos) · role para ver todos", ia03CompactLabelStyle);
        DrawIA03ItemCountsList(new Rect(12f, 344f, 415f, 36f), ia03PurchasesByItem, ref ia03PurchaseCountsScrollPosition);
        GUI.Label(new Rect(12f, 384f, 415f, 20f), "tentativas B/P " + ia03CommandAttempts
            + " | falhas construção/compra " + ia03BuildFailures + "/" + ia03PurchaseFailures,
            ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 404f, 415f, 20f), "pendentes " + cachedIA03Queued
            + " | confirma " + cachedIA03AwaitingConfirmation
            + " | espera mais antiga " + cachedIA03OldestPendingWaitSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s", ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 424f, 415f, 20f), "bloqueios " + ia03PlanningFailures
            + " | causas " + ia03UniquePlanningFailureCauses
            + " | repeticoes " + ia03RepeatedPlanningFailures + " | erros " + ia03ErrorCount, ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 444f, 415f, 20f), "IA03 " + cachedIA03State + " | ultima acao: "
            + TruncateDiagnosticText(ia03LastAction, 44), ia03CompactLabelStyle);

        GUI.Label(new Rect(12f, 464f, 415f, 20f), FormatLatestIA03Order(cachedIA03LatestOrder), ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 484f, 415f, 36f), "ultima falha [" + ia03LastErrorSource + "]: "
            + ia03LastError, ia03HistoryLabelStyle);
        GUI.Label(new Rect(12f, 522f, 415f, 36f), "condicao para nova tentativa: "
            + ia03LastRetryCondition, ia03HistoryLabelStyle);
        GUI.Label(new Rect(12f, 560f, 415f, 20f), "aloc IA03/Update atual/media/pico: "
            + ToKilobytes(ia03LastUpdateAllocatedBytes) + "/"
            + ToKilobytes((long)ia03AverageUpdateAllocatedBytes) + "/"
            + ToKilobytes(ia03PeakUpdateAllocatedBytes) + " KB", ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 580f, 415f, 20f), "tempo IA03/Update atual/media/pico: "
            + FormatDiagnosticMilliseconds(ia03LastUpdateCpuMilliseconds) + "/"
            + FormatDiagnosticMilliseconds(ia03AverageUpdateCpuMilliseconds) + "/"
            + FormatDiagnosticMilliseconds(ia03PeakUpdateCpuMilliseconds) + " ms", ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 600f, 415f, 20f), "Mono usada pelo processo (nao atribuivel a IA03): "
            + ToMegabytes(managedBytes) + " MB", ia03CompactLabelStyle);
        GUI.Label(new Rect(12f, 620f, 415f, 18f), "historico de ordens (mais antigas → recentes; limite 128 eventos)", ia03CompactLabelStyle);
        DrawIA03CommandHistory(new Rect(12f, 638f, 415f, 48f));
    }

    private void RefreshIA03CommandHistory(List<IA_CommandRecord> history)
    {
        const float viewportHeight = 48f;
        const float contentWidth = 399f;
        float previousMaxScroll = Mathf.Max(0f, cachedIA03HistoryContentHeight - viewportHeight);
        bool wasAtBottom = ia03CommandHistoryScrollPosition.y >= Mathf.Max(0f, previousMaxScroll - 20f);
        string newestKey = history != null && history.Count > 0
            ? BuildIA03HistoryKey(history[0])
            : string.Empty;
        bool hasNewHistory = !string.Equals(newestKey, cachedIA03NewestHistoryKey, StringComparison.Ordinal);

        cachedIA03HistoryHeaders.Clear();
        cachedIA03HistoryDetails.Clear();
        cachedIA03HistoryRowHeights.Clear();
        cachedIA03HistoryContentHeight = 0f;

        if (history != null)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                IA_CommandRecord record = history[i];
                if (record == null) continue;

                string header = FormatIA03HistoryHeader(record);
                string detail = FormatIA03HistoryDetail(record);
                float detailHeight = Mathf.Max(18f, ia03HistoryLabelStyle.CalcHeight(new GUIContent(detail), contentWidth));
                float rowHeight = 18f + detailHeight + 2f;
                cachedIA03HistoryHeaders.Add(header);
                cachedIA03HistoryDetails.Add(detail);
                cachedIA03HistoryRowHeights.Add(rowHeight);
                cachedIA03HistoryContentHeight += rowHeight;
            }
        }

        cachedIA03NewestHistoryKey = newestKey;
        if (hasNewHistory && wasAtBottom)
        {
            ia03CommandHistoryScrollPosition.y = Mathf.Max(0f, cachedIA03HistoryContentHeight - viewportHeight);
        }
        else
        {
            ia03CommandHistoryScrollPosition.y = Mathf.Min(
                ia03CommandHistoryScrollPosition.y,
                Mathf.Max(0f, cachedIA03HistoryContentHeight - viewportHeight));
        }
    }

    private void DrawIA03CommandHistory(Rect viewport)
    {
        float contentWidth = Mathf.Max(1f, viewport.width - 16f);
        float contentHeight = Mathf.Max(viewport.height, cachedIA03HistoryContentHeight);
        ia03CommandHistoryScrollPosition = GUI.BeginScrollView(
            viewport,
            ia03CommandHistoryScrollPosition,
            new Rect(0f, 0f, contentWidth, contentHeight),
            false,
            cachedIA03HistoryContentHeight > viewport.height);

        if (cachedIA03HistoryHeaders.Count == 0)
        {
            GUI.Label(new Rect(0f, 0f, contentWidth, 18f), "nenhuma ordem registrada", ia03HistoryLabelStyle);
        }
        else
        {
            float y = 0f;
            for (int i = 0; i < cachedIA03HistoryHeaders.Count; i++)
            {
                float rowHeight = cachedIA03HistoryRowHeights[i];
                GUI.Label(new Rect(0f, y, contentWidth, 18f), cachedIA03HistoryHeaders[i], ia03HistoryLabelStyle);
                GUI.Label(new Rect(0f, y + 18f, contentWidth, rowHeight - 18f), cachedIA03HistoryDetails[i], ia03HistoryLabelStyle);
                y += rowHeight;
            }
        }

        GUI.EndScrollView();
    }

    private static string BuildIA03HistoryKey(IA_CommandRecord record)
    {
        if (record == null) return string.Empty;
        return (record.Id ?? string.Empty) + "|" + record.Timestamp.ToString("F3", CultureInfo.InvariantCulture)
            + "|" + record.Status + "|" + (record.Message ?? string.Empty);
    }

    private static string FormatIA03HistoryHeader(IA_CommandRecord record)
    {
        string item = TruncateDiagnosticText(record.ItemKey, 30);
        string type = record.Type == IA_CommandType.Build ? "construcao"
            : record.Type == IA_CommandType.Produce ? "compra/produce"
            : record.Type.ToString().ToLowerInvariant();
        float elapsed = Mathf.Max(0f, record.Timestamp - record.CreatedAt);
        string header = "t+" + elapsed.ToString("F1", CultureInfo.InvariantCulture) + "s | " + type + " " + item
            + " | " + FormatIA03CommandStatus(record.Status) + " | x" + record.AttemptCount;

        if (record.AttemptCount > 0 && record.FirstAttemptTime >= record.CreatedAt)
            header += " | inicio " + (record.FirstAttemptTime - record.CreatedAt).ToString("F1", CultureInfo.InvariantCulture) + "s";
        if (record.CompletedAt > 0f && record.CompletedAt >= record.CreatedAt)
            header += " | concluiu " + (record.CompletedAt - record.CreatedAt).ToString("F1", CultureInfo.InvariantCulture) + "s";
        return header;
    }

    private static string FormatIA03HistoryDetail(IA_CommandRecord record)
    {
        return "origem=" + (record.Origin ?? "n/d")
            + " | motivo=" + (record.Reason ?? "n/d")
            + " | retorno=" + (record.Message ?? "n/d");
    }

    private void DrawIA03ItemCountsList(Rect viewport, Dictionary<string, int> counts, ref Vector2 scrollPosition)
    {
        const float rowHeight = 18f;
        float contentHeight = Mathf.Max(viewport.height, counts.Count * rowHeight);
        float contentWidth = Mathf.Max(1f, viewport.width - 16f);
        scrollPosition = GUI.BeginScrollView(
            viewport,
            scrollPosition,
            new Rect(0f, 0f, contentWidth, contentHeight),
            false,
            counts.Count * rowHeight > viewport.height);

        if (counts.Count == 0)
        {
            GUI.Label(new Rect(0f, 0f, contentWidth, rowHeight), "nenhum", ia03CompactLabelStyle);
        }
        else
        {
            int row = 0;
            foreach (KeyValuePair<string, int> entry in counts)
            {
                GUI.Label(
                    new Rect(0f, row * rowHeight, contentWidth, rowHeight),
                    TruncateDiagnosticText(entry.Key, 64) + " x" + entry.Value,
                    ia03CompactLabelStyle);
                row++;
            }
        }

        GUI.EndScrollView();
    }

    private static IA_CommandRecord GetLatestIA03CommandRecord(List<IA_CommandRecord> history)
    {
        if (history == null) return null;
        for (int i = 0; i < history.Count; i++)
        {
            IA_CommandRecord record = history[i];
            if (record == null || (record.Type != IA_CommandType.Build && record.Type != IA_CommandType.Produce))
                continue;

            if (record.Status == IA_CommandStatus.CoolingDown)
            {
                for (int j = i + 1; j < history.Count; j++)
                {
                    IA_CommandRecord previous = history[j];
                    if (previous == null || !string.Equals(previous.Id, record.Id, StringComparison.Ordinal))
                        continue;
                    if (previous.Status == IA_CommandStatus.Success || previous.Status == IA_CommandStatus.Failed)
                        return previous;
                }
            }

            return record;
        }

        return null;
    }

    private static string FormatLatestIA03Order(IA_CommandRecord record)
    {
        if (record == null) return "ordem recente: nenhuma construcao/compra";

        float latency = record.AttemptCount > 0 && record.FirstAttemptTime >= record.CreatedAt
            ? Mathf.Max(0f, record.FirstAttemptTime - record.CreatedAt)
            : -1f;
        string latencyText = latency >= 0f ? " | iniciou " + latency.ToString("F1", CultureInfo.InvariantCulture) + "s" : string.Empty;
        string completionText = record.CompletedAt > 0f && record.CompletedAt >= record.CreatedAt
            ? " | concluiu " + (record.CompletedAt - record.CreatedAt).ToString("F1", CultureInfo.InvariantCulture) + "s"
            : string.Empty;
        return "ordem: " + (record.Type == IA_CommandType.Build ? "build " : "compra ")
            + TruncateDiagnosticText(record.ItemKey, 18)
            + " | " + FormatIA03CommandStatus(record.Status)
            + " | x" + record.AttemptCount + latencyText + completionText;
    }

    private static string FormatDiagnosticMilliseconds(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string FormatIA03CommandStatus(IA_CommandStatus status)
    {
        switch (status)
        {
            case IA_CommandStatus.Queued: return "na fila";
            case IA_CommandStatus.Running: return "executando";
            case IA_CommandStatus.AwaitingConfirmation: return "confirma";
            case IA_CommandStatus.Success: return "concluida";
            case IA_CommandStatus.Failed: return "falhou";
            case IA_CommandStatus.CoolingDown: return "cooldown";
            case IA_CommandStatus.Cancelled: return "cancelada";
            case IA_CommandStatus.Requested: return "solicitada";
            default: return status.ToString();
        }
    }

    private static string TruncateDiagnosticText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "n/d";
        string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength - 1) + "…";
    }

    private void StartBenchmark()
    {
        benchmarkSamples.Clear();
        Array.Clear(benchmarkFrameMillisecondsHistogram, 0, benchmarkFrameMillisecondsHistogram.Length);
        benchmarkFrameCount = 0;
        benchmarkTotalFrameMilliseconds = 0d;
        ResetBenchmarkWindow();
        benchmarkRecording = true;
        benchmarkStartedAt = Time.unscaledTime;
        nextSampleAt = benchmarkStartedAt;
        nextBenchmarkSampleAt = benchmarkStartedAt + Mathf.Max(0.1f, sampleInterval);
        Debug.Log("[GlobalWorldDiagnostics] Benchmark iniciado: " + benchmarkLabel);
    }

    private void StopBenchmark()
    {
        if (benchmarkRecording && benchmarkWindowFrameCount > 0 && benchmarkSamples.Count < benchmarkSamples.Capacity)
            FinalizeBenchmarkSample();

        benchmarkRecording = false;
        if (benchmarkSamples.Count == 0)
        {
            Debug.LogWarning("[GlobalWorldDiagnostics] Benchmark sem frames; nenhum CSV foi salvo.");
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "GlobalMapPerformance");
        Directory.CreateDirectory(directory);
        string safeLabel = SanitizeFileName(string.IsNullOrWhiteSpace(benchmarkLabel) ? "benchmark" : benchmarkLabel);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, safeLabel + "-" + timestamp + ".csv");
        WriteBenchmarkCsv(path);
        Debug.Log("[GlobalWorldDiagnostics] Benchmark salvo: " + path + " | " + BuildBenchmarkSummary());
    }

    private void CaptureBenchmarkFrame()
    {
        float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
        benchmarkWindowFrameMilliseconds.Add(frameMilliseconds);
        benchmarkWindowFrameCount++;
        benchmarkWindowFrameMillisecondsTotal += frameMilliseconds;
        benchmarkWindowWorstFrameMilliseconds = Mathf.Max(benchmarkWindowWorstFrameMilliseconds, frameMilliseconds);
        benchmarkFrameCount++;
        benchmarkTotalFrameMilliseconds += frameMilliseconds;
        int histogramBucket = frameMilliseconds >= BenchmarkFrameHistogramMaxMs
            ? BenchmarkFrameHistogramOverflowIndex
            : Mathf.Clamp(Mathf.CeilToInt(frameMilliseconds), 0, BenchmarkFrameHistogramMaxMs);
        benchmarkFrameMillisecondsHistogram[histogramBucket]++;

        if (mainThreadRecorder.Valid)
        {
            benchmarkWindowMainThreadNanosecondsTotal += mainThreadRecorder.LastValue;
            benchmarkWindowMainThreadSamples++;
        }
        if (renderThreadRecorder.Valid)
        {
            benchmarkWindowRenderThreadNanosecondsTotal += renderThreadRecorder.LastValue;
            benchmarkWindowRenderThreadSamples++;
        }
        if (gpuRecorder.Valid && gpuRecorder.LastValue > 0)
        {
            benchmarkWindowGpuNanosecondsTotal += gpuRecorder.LastValue;
            benchmarkWindowGpuSamples++;
        }
        if (gcAllocatedRecorder.Valid)
        {
            benchmarkWindowGcAllocatedBytes += Math.Max(0L, gcAllocatedRecorder.LastValue);
            benchmarkWindowGcAllocatedSamples++;
        }
        if (drawCallsRecorder.Valid) benchmarkWindowDrawCalls = drawCallsRecorder.LastValue;
        if (trianglesRecorder.Valid) benchmarkWindowTriangles = trianglesRecorder.LastValue;
        if (batchesRecorder.Valid) benchmarkWindowBatches = batchesRecorder.LastValue;

        if (Time.unscaledTime >= nextBenchmarkSampleAt)
        {
            if (benchmarkSamples.Count >= benchmarkSamples.Capacity)
            {
                StopBenchmark();
                Debug.LogWarning("[GlobalWorldDiagnostics] Limite de intervalos atingido; captura encerrada.");
                return;
            }

            FinalizeBenchmarkSample();
            AdvanceBenchmarkSampleDeadline(Time.unscaledTime);
        }
    }

    private void AdvanceBenchmarkSampleDeadline(float currentTime)
    {
        float interval = Mathf.Max(0.1f, sampleInterval);
        do
        {
            nextBenchmarkSampleAt += interval;
        }
        while (nextBenchmarkSampleAt <= currentTime);
    }

    private void FinalizeBenchmarkSample()
    {
        if (benchmarkWindowFrameCount <= 0)
            return;

        float elapsedSeconds = Time.unscaledTime - benchmarkStartedAt;
        if (elapsedSeconds < 0f)
            return;

        float memorySampleAge = Time.unscaledTime - benchmarkMemorySampleAt;
        bool hasRecentMemorySample = memorySampleAge >= 0f
            && memorySampleAge <= Mathf.Max(0.25f, sampleInterval * 1.5f);

        benchmarkWindowFrameMilliseconds.Sort();
        int p95Index = Mathf.Clamp(
            Mathf.CeilToInt(benchmarkWindowFrameMilliseconds.Count * 0.95f) - 1,
            0,
            benchmarkWindowFrameMilliseconds.Count - 1);
        FrameSample sample = new FrameSample
        {
            frameMilliseconds = (float)(benchmarkWindowFrameMillisecondsTotal / benchmarkWindowFrameCount),
            p95FrameMilliseconds = benchmarkWindowFrameMilliseconds[p95Index],
            worstFrameMilliseconds = benchmarkWindowWorstFrameMilliseconds,
            elapsedSeconds = elapsedSeconds,
            timeScale = Time.timeScale,
            mainThreadNanoseconds = benchmarkWindowMainThreadSamples > 0
                ? (long)(benchmarkWindowMainThreadNanosecondsTotal / benchmarkWindowMainThreadSamples) : -1,
            renderThreadNanoseconds = benchmarkWindowRenderThreadSamples > 0
                ? (long)(benchmarkWindowRenderThreadNanosecondsTotal / benchmarkWindowRenderThreadSamples) : -1,
            gpuNanoseconds = benchmarkWindowGpuSamples > 0
                ? (long)(benchmarkWindowGpuNanosecondsTotal / benchmarkWindowGpuSamples) : -1,
            drawCalls = benchmarkWindowDrawCalls,
            triangles = benchmarkWindowTriangles,
            batches = benchmarkWindowBatches,
            gcAllocatedBytes = benchmarkWindowGcAllocatedSamples > 0 ? benchmarkWindowGcAllocatedBytes : -1,
            unityMemoryBytes = allocatedBytes,
            monoMemoryBytes = managedBytes,
            graphicsMemoryBytes = graphicsBytes,
            processWorkingSetBytes = processWorkingSetBytes,
            terrainTiles = streamer != null ? streamer.ActiveTileCount : -1,
            trees = streamer != null ? streamer.ActiveTreeCount : -1,
            activeObjects = hasRecentMemorySample ? activeGameObjects : -1,
            units = hasRecentMemorySample ? activeUnits : -1,
            navalUnits = hasRecentMemorySample ? activeNavalUnits : -1,
            projectiles = hasRecentMemorySample ? activeProjectiles : -1,
            particleSystems = hasRecentMemorySample ? activeParticleSystems : -1,
            trailRenderers = hasRecentMemorySample ? activeTrailRenderers : -1,
            tileGenerationMilliseconds = streamer != null ? streamer.LastGenerationMilliseconds : -1
        };
        benchmarkSamples.Add(sample);
        ResetBenchmarkWindow();
    }

    private void ResetBenchmarkWindow()
    {
        benchmarkWindowFrameMilliseconds.Clear();
        benchmarkWindowFrameCount = 0;
        benchmarkWindowFrameMillisecondsTotal = 0d;
        benchmarkWindowWorstFrameMilliseconds = 0f;
        benchmarkWindowMainThreadNanosecondsTotal = 0d;
        benchmarkWindowRenderThreadNanosecondsTotal = 0d;
        benchmarkWindowGpuNanosecondsTotal = 0d;
        benchmarkWindowMainThreadSamples = 0;
        benchmarkWindowRenderThreadSamples = 0;
        benchmarkWindowGpuSamples = 0;
        benchmarkWindowGcAllocatedBytes = 0L;
        benchmarkWindowGcAllocatedSamples = 0;
        benchmarkWindowDrawCalls = -1;
        benchmarkWindowTriangles = -1;
        benchmarkWindowBatches = -1;
    }

    private void WriteBenchmarkCsv(string path)
    {
        StringBuilder csv = new StringBuilder(benchmarkSamples.Count * 150);
        csv.AppendLine("label,elapsed_seconds,time_scale,frame_ms_avg,frame_p95_ms,frame_worst_ms,fps,main_thread_ms_avg,render_thread_ms_avg,gpu_ms_avg,draw_calls,triangles,batches,gc_alloc_bytes_interval,unity_memory_bytes,mono_memory_bytes,graphics_memory_bytes,process_working_set_bytes,terrain_tiles,trees,active_gameobjects,units,naval_units,projectiles,particle_systems,trail_renderers,tile_generation_ms");
        for (int i = 0; i < benchmarkSamples.Count; i++)
        {
            FrameSample s = benchmarkSamples[i];
            csv.Append(Csv(benchmarkLabel)).Append(',')
                .Append(F(s.elapsedSeconds, 3)).Append(',')
                .Append(F(s.timeScale, 2)).Append(',')
                .Append(F(s.frameMilliseconds, 4)).Append(',')
                .Append(F(s.p95FrameMilliseconds, 4)).Append(',')
                .Append(F(s.worstFrameMilliseconds, 4)).Append(',')
                .Append(F(s.frameMilliseconds > 0f ? 1000f / s.frameMilliseconds : 0f, 3)).Append(',')
                .Append(NsToMs(s.mainThreadNanoseconds)).Append(',').Append(NsToMs(s.renderThreadNanoseconds)).Append(',').Append(NsToMs(s.gpuNanoseconds)).Append(',')
                .Append(s.drawCalls).Append(',').Append(s.triangles).Append(',').Append(s.batches).Append(',').Append(s.gcAllocatedBytes).Append(',')
                .Append(s.unityMemoryBytes).Append(',').Append(s.monoMemoryBytes).Append(',').Append(s.graphicsMemoryBytes).Append(',').Append(s.processWorkingSetBytes).Append(',')
                .Append(s.terrainTiles).Append(',').Append(s.trees).Append(',').Append(s.activeObjects).Append(',').Append(s.units).Append(',')
                .Append(s.navalUnits).Append(',').Append(s.projectiles).Append(',').Append(s.particleSystems).Append(',').Append(s.trailRenderers).Append(',')
                .Append(s.tileGenerationMilliseconds).AppendLine();
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
    }

    private string BuildBenchmarkSummary()
    {
        float averageFrameMs = benchmarkFrameCount > 0 ? (float)(benchmarkTotalFrameMilliseconds / benchmarkFrameCount) : 0f;
        float p95 = GetHistogramPercentile(0.95f);
        float p99 = GetHistogramPercentile(0.99f);
        return "samples=" + benchmarkSamples.Count + ", frames=" + benchmarkFrameCount + ", avgFPS=" + (averageFrameMs > 0f ? 1000f / averageFrameMs : 0f).ToString("F1", CultureInfo.InvariantCulture)
            + ", P95=" + p95.ToString("F2", CultureInfo.InvariantCulture) + "ms, 1%low=" + (p99 > 0f ? 1000f / p99 : 0f).ToString("F1", CultureInfo.InvariantCulture);
    }

    private float GetHistogramPercentile(float percentile)
    {
        if (benchmarkFrameCount <= 0)
            return 0f;

        long target = Math.Max(1L, (long)Math.Ceiling(benchmarkFrameCount * percentile));
        long cumulative = 0L;
        for (int i = 0; i < benchmarkFrameMillisecondsHistogram.Length; i++)
        {
            cumulative += benchmarkFrameMillisecondsHistogram[i];
            if (cumulative >= target)
                return i == BenchmarkFrameHistogramOverflowIndex ? BenchmarkFrameHistogramMaxMs : i;
        }
        return BenchmarkFrameHistogramMaxMs;
    }

    private static string NsToMs(long nanoseconds) => nanoseconds < 0 ? "-1" : F(nanoseconds / 1000000f, 3);
    private static string F(float value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++) value = value.Replace(invalid[i], '_');
        return value.Trim();
    }

    private static string ToMegabytes(long bytes) => (bytes / (1024f * 1024f)).ToString("F0") + " MB";
    private static string ToKilobytes(long bytes) => (bytes / 1024f).ToString("F1");

    private static string ToMilliseconds(ProfilerRecorder recorder)
    {
        return recorder.Valid ? (recorder.LastValue / 1000000f).ToString("F2") + " ms" : "n/a";
    }

    private static string ToCount(ProfilerRecorder recorder)
    {
        return recorder.Valid ? recorder.LastValue.ToString("N0") : "n/a";
    }

    private static ProfilerRecorder StartRecorder(List<ProfilerRecorderHandle> handles, string desiredName)
    {
        for (int i = 0; i < handles.Count; i++)
        {
            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handles[i]);
            if (string.Equals(description.Name.ToString(), desiredName, System.StringComparison.OrdinalIgnoreCase))
                return new ProfilerRecorder(handles[i], 15);
        }
        return default;
    }

    private void BeginActiveObjectScan()
    {
        activeObjectScan.Clear();
        sceneRoots.Clear();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            activeScene.GetRootGameObjects(sceneRoots);

        for (int i = 0; i < sceneRoots.Count; i++)
        {
            GameObject root = sceneRoots[i];
            if (root != null && root.activeInHierarchy)
                activeObjectScan.Add(root.transform);
        }

        activeObjectScanCursor = 0;
        activeObjectScanCount = 0;
        activeObjectScanUnits = 0;
        activeObjectScanNavalUnits = 0;
        activeObjectScanProjectiles = 0;
        activeObjectScanParticleSystems = 0;
        activeObjectScanTrailRenderers = 0;
        activeObjectScanInProgress = true;
    }

    private void AdvanceActiveObjectScan()
    {
        int budget = Mathf.Max(1, activeObjectScanBudgetPerFrame);
        int processed = 0;
        while (activeObjectScanCursor < activeObjectScan.Count && processed < budget)
        {
            Transform current = activeObjectScan[activeObjectScanCursor++];
            processed++;
            if (current == null || !current.gameObject.activeInHierarchy)
                continue;

            activeObjectScanCount++;
            if (current.TryGetComponent(out IdentidadeUnidade identidadeUnidade) && identidadeUnidade.enabled)
                activeObjectScanUnits++;
            else if (current.TryGetComponent(out IdentidadeNaval identidadeNaval) && identidadeNaval.enabled)
                activeObjectScanUnits++;
            if (current.TryGetComponent(out IdentidadeNaval navalIdentity) && navalIdentity.enabled)
                activeObjectScanNavalUnits++;
            if (current.TryGetComponent(out Projetil projectile) && projectile.enabled)
                activeObjectScanProjectiles++;
            if (current.TryGetComponent(out ParticleSystem particleSystem) && particleSystem.IsAlive(true))
                activeObjectScanParticleSystems++;
            if (current.TryGetComponent(out TrailRenderer trailRenderer) && trailRenderer.enabled && trailRenderer.positionCount > 0)
                activeObjectScanTrailRenderers++;

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                    activeObjectScan.Add(child);
            }
        }

        if (activeObjectScanCursor < activeObjectScan.Count)
            return;

        activeGameObjects = activeObjectScanCount;
        activeUnits = activeObjectScanUnits;
        activeNavalUnits = activeObjectScanNavalUnits;
        activeProjectiles = activeObjectScanProjectiles;
        activeParticleSystems = activeObjectScanParticleSystems;
        activeTrailRenderers = activeObjectScanTrailRenderers;
        activeObjectScanInProgress = false;
        nextActiveObjectScanAt = Time.unscaledTime + Mathf.Max(1f, activeObjectScanInterval);
        activeObjectScan.Clear();
    }
}
