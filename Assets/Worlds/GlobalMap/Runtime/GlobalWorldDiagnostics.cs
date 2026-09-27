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

/// <summary>Optional local overlay for profiling the map scene.</summary>
public sealed class GlobalWorldDiagnostics : MonoBehaviour
{
    public GlobalTerrainStreamer streamer;
    public bool visible = true;
    [Min(0.1f)] public float sampleInterval = 1f;
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
    private bool benchmarkRecording;
    private float benchmarkStartedAt;
    private int benchmarkMemorySampleFrame;
    private int activeUnits;
    private Process currentProcess;

    private struct FrameSample
    {
        public float frameMilliseconds;
        public float elapsedSeconds;
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
        public int tileGenerationMilliseconds;
    }

    private void OnEnable()
    {
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
            processWorkingSetBytes = currentProcess != null ? currentProcess.WorkingSet64 : -1;
            activeGameObjects = CountActiveSceneObjects(out activeUnits);
            benchmarkMemorySampleFrame = Time.frameCount;
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
        GUI.Box(new Rect(12f, 12f, 330f, 262f), "GLOBAL MAP · PERFORMANCE");
        GUI.Label(new Rect(24f, 42f, 305f, 22f), "FPS: " + smoothedFps.ToString("F1") + "    altitude: " + altitude);
        GUI.Label(new Rect(24f, 64f, 305f, 22f), "terrain tiles: " + streamer.ActiveTileCount + "    trees: " + streamer.ActiveTreeCount);
        GUI.Label(new Rect(24f, 86f, 305f, 22f), "active GameObjects: " + activeGameObjects);
        GUI.Label(new Rect(24f, 108f, 305f, 22f), "tile generation: " + streamer.LastGenerationMilliseconds + " ms (last)");
        GUI.Label(new Rect(24f, 130f, 305f, 22f), "CPU main/render: " + mainThread + " / " + renderThread);
        GUI.Label(new Rect(24f, 152f, 305f, 22f), "GPU frame: " + ToMilliseconds(gpuRecorder));
        GUI.Label(new Rect(24f, 174f, 305f, 22f), "draw / triangles / batches: "
            + ToCount(drawCallsRecorder) + " / " + ToCount(trianglesRecorder) + " / " + ToCount(batchesRecorder));
        GUI.Label(new Rect(24f, 196f, 305f, 22f), "Unity / Mono memory: " + ToMegabytes(allocatedBytes) + " / " + ToMegabytes(managedBytes));
        GUI.Label(new Rect(24f, 218f, 305f, 22f), "graphics driver memory: " + ToMegabytes(graphicsBytes));
        GUI.Label(new Rect(24f, 240f, 305f, 22f), benchmarkRecording
            ? "REC  " + benchmarkLabel + "  " + (Time.unscaledTime - benchmarkStartedAt).ToString("F0") + "s · F9 parar"
            : "F9 iniciar/parar CSV · " + benchmarkLabel);
    }

    private void StartBenchmark()
    {
        benchmarkSamples.Clear();
        benchmarkRecording = true;
        benchmarkStartedAt = Time.unscaledTime;
        nextSampleAt = 0f;
        Debug.Log("[GlobalWorldDiagnostics] Benchmark iniciado: " + benchmarkLabel);
    }

    private void StopBenchmark()
    {
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
        if (benchmarkSamples.Count == benchmarkSamples.Capacity)
        {
            StopBenchmark();
            Debug.LogWarning("[GlobalWorldDiagnostics] Capacidade de amostras atingida; captura encerrada.");
            return;
        }

        bool updatedMemorySample = Time.frameCount == benchmarkMemorySampleFrame;
        benchmarkSamples.Add(new FrameSample
        {
            frameMilliseconds = Time.unscaledDeltaTime * 1000f,
            elapsedSeconds = Time.unscaledTime - benchmarkStartedAt,
            mainThreadNanoseconds = mainThreadRecorder.Valid ? mainThreadRecorder.LastValue : -1,
            renderThreadNanoseconds = renderThreadRecorder.Valid ? renderThreadRecorder.LastValue : -1,
            gpuNanoseconds = gpuRecorder.Valid ? gpuRecorder.LastValue : -1,
            drawCalls = drawCallsRecorder.Valid ? drawCallsRecorder.LastValue : -1,
            triangles = trianglesRecorder.Valid ? trianglesRecorder.LastValue : -1,
            batches = batchesRecorder.Valid ? batchesRecorder.LastValue : -1,
            gcAllocatedBytes = gcAllocatedRecorder.Valid ? gcAllocatedRecorder.LastValue : -1,
            unityMemoryBytes = allocatedBytes,
            monoMemoryBytes = managedBytes,
            graphicsMemoryBytes = graphicsBytes,
            processWorkingSetBytes = this.processWorkingSetBytes,
            terrainTiles = streamer != null ? streamer.ActiveTileCount : -1,
            trees = streamer != null ? streamer.ActiveTreeCount : -1,
            activeObjects = updatedMemorySample ? activeGameObjects : -1,
            units = updatedMemorySample ? activeUnits : -1,
            tileGenerationMilliseconds = streamer != null ? streamer.LastGenerationMilliseconds : -1
        });
    }

    private void WriteBenchmarkCsv(string path)
    {
        StringBuilder csv = new StringBuilder(benchmarkSamples.Count * 150);
        csv.AppendLine("label,elapsed_seconds,frame_ms,fps,main_thread_ms,render_thread_ms,gpu_ms,draw_calls,triangles,batches,gc_alloc_bytes,unity_memory_bytes,mono_memory_bytes,graphics_memory_bytes,process_working_set_bytes,terrain_tiles,trees,active_gameobjects,units,tile_generation_ms");
        for (int i = 0; i < benchmarkSamples.Count; i++)
        {
            FrameSample s = benchmarkSamples[i];
            csv.Append(Csv(benchmarkLabel)).Append(',')
                .Append(F(s.elapsedSeconds, 3)).Append(',')
                .Append(F(s.frameMilliseconds, 4)).Append(',').Append(F(s.frameMilliseconds > 0f ? 1000f / s.frameMilliseconds : 0f, 3)).Append(',')
                .Append(NsToMs(s.mainThreadNanoseconds)).Append(',').Append(NsToMs(s.renderThreadNanoseconds)).Append(',').Append(NsToMs(s.gpuNanoseconds)).Append(',')
                .Append(s.drawCalls).Append(',').Append(s.triangles).Append(',').Append(s.batches).Append(',').Append(s.gcAllocatedBytes).Append(',')
                .Append(s.unityMemoryBytes).Append(',').Append(s.monoMemoryBytes).Append(',').Append(s.graphicsMemoryBytes).Append(',').Append(s.processWorkingSetBytes).Append(',')
                .Append(s.terrainTiles).Append(',').Append(s.trees).Append(',').Append(s.activeObjects).Append(',').Append(s.units).Append(',')
                .Append(s.tileGenerationMilliseconds).AppendLine();
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
    }

    private string BuildBenchmarkSummary()
    {
        float[] frameTimes = new float[benchmarkSamples.Count];
        double totalMilliseconds = 0d;
        for (int i = 0; i < benchmarkSamples.Count; i++)
        {
            frameTimes[i] = benchmarkSamples[i].frameMilliseconds;
            totalMilliseconds += frameTimes[i];
        }
        Array.Sort(frameTimes);
        float averageFrameMs = (float)(totalMilliseconds / benchmarkSamples.Count);
        float p95 = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Length * 0.95f) - 1, 0, frameTimes.Length - 1)];
        float p99 = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Length * 0.99f) - 1, 0, frameTimes.Length - 1)];
        return "frames=" + benchmarkSamples.Count + ", avgFPS=" + (averageFrameMs > 0f ? 1000f / averageFrameMs : 0f).ToString("F1", CultureInfo.InvariantCulture)
            + ", P95=" + p95.ToString("F2", CultureInfo.InvariantCulture) + "ms, 1%low=" + (p99 > 0f ? 1000f / p99 : 0f).ToString("F1", CultureInfo.InvariantCulture);
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

    private void CountChildren(Transform parent, ref int count, ref int units)
    {
        count++;
        if (parent.TryGetComponent(out IdentidadeUnidade identidadeUnidade) && identidadeUnidade.enabled)
            units++;
        else if (parent.TryGetComponent(out IdentidadeNaval identidadeNaval) && identidadeNaval.enabled)
            units++;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                CountChildren(child, ref count, ref units);
        }
    }

    private int CountActiveSceneObjects(out int units)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        sceneRoots.Clear();
        activeScene.GetRootGameObjects(sceneRoots);
        int count = 0;
        units = 0;
        for (int i = 0; i < sceneRoots.Count; i++)
        {
            GameObject root = sceneRoots[i];
            if (root != null && root.activeInHierarchy)
                CountChildren(root.transform, ref count, ref units);
        }
        return count;
    }
}
