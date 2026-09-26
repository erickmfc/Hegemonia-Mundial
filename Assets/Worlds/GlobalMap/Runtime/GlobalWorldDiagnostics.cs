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

    private float nextSampleAt;
    private float smoothedFps;
    private long managedBytes;
    private long allocatedBytes;
    private long graphicsBytes;
    private int activeGameObjects;
    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gpuRecorder;
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder batchesRecorder;
    private readonly List<GameObject> sceneRoots = new List<GameObject>(16);

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
    }

    private void OnDisable()
    {
        if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
        if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
        if (gpuRecorder.Valid) gpuRecorder.Dispose();
        if (drawCallsRecorder.Valid) drawCallsRecorder.Dispose();
        if (trianglesRecorder.Valid) trianglesRecorder.Dispose();
        if (batchesRecorder.Valid) batchesRecorder.Dispose();
    }

    private void Update()
    {
        if (Time.unscaledDeltaTime > 0f)
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f);
            smoothedFps = Mathf.Lerp(smoothedFps <= 0f ? 1f / Time.unscaledDeltaTime : smoothedFps, 1f / Time.unscaledDeltaTime, blend);
        }

        if (Time.unscaledTime < nextSampleAt)
            return;

        nextSampleAt = Time.unscaledTime + sampleInterval;
        managedBytes = Profiler.GetMonoUsedSizeLong();
        allocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
        graphicsBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
        activeGameObjects = CountActiveSceneObjects();
    }

    private void OnGUI()
    {
        if (!visible || streamer == null)
            return;

        Camera camera = Camera.main;
        string mainThread = mainThreadRecorder.Valid ? (mainThreadRecorder.LastValue / 1000000f).ToString("F2") + " ms" : "n/a";
        string renderThread = renderThreadRecorder.Valid ? (renderThreadRecorder.LastValue / 1000000f).ToString("F2") + " ms" : "n/a";
        string altitude = camera != null ? (camera.transform.position.y / 1000f).ToString("F1") + " km" : "n/a";
        GUI.Box(new Rect(12f, 12f, 330f, 238f), "GLOBAL MAP · PERFORMANCE");
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

    private void CountChildren(Transform parent, ref int count)
    {
        count++;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                CountChildren(child, ref count);
        }
    }

    private int CountActiveSceneObjects()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        sceneRoots.Clear();
        activeScene.GetRootGameObjects(sceneRoots);
        int count = 0;
        for (int i = 0; i < sceneRoots.Count; i++)
        {
            GameObject root = sceneRoots[i];
            if (root != null && root.activeInHierarchy)
                CountChildren(root.transform, ref count);
        }
        return count;
    }
}
