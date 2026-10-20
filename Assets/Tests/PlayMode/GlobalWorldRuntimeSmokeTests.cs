using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GlobalWorldRuntimeSmokeTests
{
    private const string ScenePath = "Assets/Scenes/GlobalMapRTS.unity";
    private string previousActiveScenePath;

    [UnitySetUp]
    public IEnumerator CaptureOriginalEditorScene()
    {
        previousActiveScenePath = SceneManager.GetActiveScene().path;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator CloseGlobalMapScene()
    {
        // ComercioLocal cria um agregador nacional persistente ao ser
        // habilitado. Remova todas as instâncias do Play Mode depois das
        // asserções, sem ceder um frame em que a cena possa recriá-las.
        Type commerceType = FindProjectType("SistemaComercioNacional");
        if (commerceType != null && EditorApplication.isPlaying)
        {
            UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(commerceType);
            for (int i = instances.Length - 1; i >= 0; i--)
            {
                Component commerce = instances[i] as Component;
                if (commerce == null || commerce.gameObject == null || !commerce.gameObject.scene.IsValid())
                    continue;

                UnityEngine.Object.DestroyImmediate(commerce.gameObject);
            }
        }

        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(previousActiveScenePath))
        {
            Scene previous = SceneManager.GetSceneByPath(previousActiveScenePath);
            if (!previous.IsValid() || !previous.isLoaded)
            {
                AsyncOperation restore = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    previousActiveScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                if (restore != null)
                {
                    while (!restore.isDone)
                        yield return null;
                }
                previous = SceneManager.GetSceneByPath(previousActiveScenePath);
            }

            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    [UnityTest]
    public IEnumerator ExistingOceanRulesAndTerrainStreamingRunTogether()
    {
        Scene scene = EditorSceneManager.LoadSceneInPlayMode(
            ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        if (!scene.IsValid())
            scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = SceneManager.GetSceneByName("GlobalMapRTS");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            string loadedScenes = string.Empty;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                loadedScenes += " [" + loadedScene.name + ", " + loadedScene.path + ", loaded=" + loadedScene.isLoaded + "]";
            }
            Debug.Log("[GlobalMapSmoke] SceneManager.sceneCount=" + SceneManager.sceneCount + loadedScenes);
        }
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "A cena global deve continuar carregada em Play Mode.");
        SceneManager.SetActiveScene(scene);
        GameObject worldRoot = FindRoot(scene, "GlobalMap_World");
        Assert.That(worldRoot, Is.Not.Null);

        Type streamerType = FindProjectType("GlobalTerrainStreamer");
        Type worldType = FindProjectType("GlobalWorldDefinition");
        Type oceanType = FindProjectType("OceanAdvanced");
        Type navalPlacementType = FindProjectType("NavalPlacementResolver");
        Component streamer = FindInScene(worldRoot, streamerType);
        Assert.That(streamer, Is.Not.Null);
        Assert.That(CountInScene(scene, oceanType), Is.EqualTo(1), "A cena deve usar somente o OceanAdvanced existente.");

        Camera camera = worldRoot.GetComponentInChildren<Camera>(true);
        Assert.That(camera, Is.Not.Null);
        camera.transform.position = new Vector3(0f, 250000f, 0f);
        yield return null;

        Component globalTerrain = GetField(streamer, "globalTerrain") as Component;
        Assert.That(globalTerrain, Is.Not.Null);
        Assert.That(globalTerrain.gameObject.activeInHierarchy, Is.True,
            "O Terrain-base deve ser ativado pelo streamer após o inicializador de superfícies.");

        FieldInfo worldField = streamer.GetType().GetField("world");
        object world = worldField.GetValue(streamer);
        Assert.That(world, Is.Not.Null);
        MethodInfo heightAtWorld = worldType.GetMethod("HeightAtWorld", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo coastDistanceAtWorld = worldType.GetMethod("SignedCoastDistanceAtWorld", BindingFlags.Instance | BindingFlags.Public);
        Vector2 worldSize = (Vector2)worldType.GetField("worldSize").GetValue(world);
        float footprintHeight = (float)worldType.GetField("mapFootprintHeight").GetValue(world);
        float minX = -worldSize.x * 0.5f;
        float minZ = -footprintHeight * 0.5f;
        Vector3 islandProbe = Vector3.zero;
        float deepestInlandDistance = 0f;
        Vector3 forestProbe = Vector3.zero;
        float maximumForestWeight = 0f;
        MethodInfo forestWeightAtWorld = worldType.GetMethod("ForestWeightAtWorld", BindingFlags.Instance | BindingFlags.Public);
        const int landProbeGridX = 512;
        const int landProbeGridZ = 288;
        for (int z = 0; z <= landProbeGridZ; z++)
        {
            for (int x = 0; x <= landProbeGridX; x++)
            {
                float worldX = minX + worldSize.x * x / landProbeGridX;
                float worldZ = minZ + footprintHeight * z / landProbeGridZ;
                float coastDistance = (float)coastDistanceAtWorld.Invoke(world, new object[] { worldX, worldZ });
                if (coastDistance > deepestInlandDistance)
                {
                    deepestInlandDistance = coastDistance;
                    islandProbe = new Vector3(worldX, 0f, worldZ);
                }

                if (coastDistance >= 1000f)
                {
                    float forestWeight = (float)forestWeightAtWorld.Invoke(world, new object[] { worldX, worldZ });
                    if (forestWeight > maximumForestWeight)
                    {
                        maximumForestWeight = forestWeight;
                        forestProbe = new Vector3(worldX, 0f, worldZ);
                    }
                }
            }
        }
        Assert.That(deepestInlandDistance, Is.GreaterThan(1000f), "A máscara precisa conter terra interior suficiente para verificação naval.");
        Assert.That(maximumForestWeight, Is.GreaterThan(0.38f),
            "A referência de biomas deve fornecer ao menos uma região capaz de gerar árvores. max="
            + maximumForestWeight.ToString("F3") + " probe=" + forestProbe);
        Debug.Log("[GlobalMapSmoke] terrainLandProbe=" + islandProbe + " forestProbe=" + forestProbe
            + " forestWeight=" + maximumForestWeight.ToString("F3"));
        float landHeight = (float)heightAtWorld.Invoke(world, new object[] { islandProbe.x, islandProbe.z });
        Assert.That(landHeight, Is.GreaterThanOrEqualTo(4f), "A costa deve ficar acima da amplitude do oceano.");

        MethodInfo waterHeightMethod = oceanType.GetMethod("GetWaterHeight", BindingFlags.Public | BindingFlags.Static);
        float sampledWave = (float)waterHeightMethod.Invoke(null, new object[] { new Vector3(0f, 0f, 0f) });
        Assert.That(sampledWave, Is.InRange(-2.61f, 2.61f), "A onda precisa continuar usando a amplitude naval existente.");

        Physics.SyncTransforms();
        MethodInfo navalWaterClassifier = navalPlacementType.GetMethod(
            "IsWaterAtPosition", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(Vector3), typeof(float) }, null);
        Assert.That(navalWaterClassifier, Is.Not.Null, "A cena deve continuar consultando as regras navais existentes.");
        bool openSeaIsWater = (bool)navalWaterClassifier.Invoke(null, new object[] { new Vector3(0f, 0f, 0f), 0f });
        bool islandIsWater = (bool)navalWaterClassifier.Invoke(null, new object[] { islandProbe, 0f });
        Assert.That(openSeaIsWater, Is.True, "O oceano existente deve classificar o mar aberto como água para navios.");
        Assert.That(islandIsWater, Is.False,
            "O terreno gerado deve impedir que a mesma regra naval classifique a ilha como água. "
            + DescribeNavalHits(globalTerrain as Terrain, islandProbe));

        string[] streamingMarkerNames =
        {
            "GlobalTerrainStreamer.BuildDesiredSet",
            "GlobalTerrainStreamer.ApplyVisibleTiles",
            "GlobalTerrainStreamer.RemoveStaleTiles",
            "GlobalTerrainStreamer.InitializeTerrainData",
            "GlobalTerrainStreamer.GenerateHeightRows",
            "GlobalTerrainStreamer.ApplyHeightRows",
            "GlobalTerrainStreamer.SyncHeightmap",
            "GlobalTerrainStreamer.GenerateAlphamapRows",
            "GlobalTerrainStreamer.ApplyAlphamapRows",
            "GlobalTerrainStreamer.GenerateVegetationRow",
            "GlobalTerrainStreamer.BuildVegetationInstances",
            "GlobalTerrainStreamer.ApplyVegetationInstances",
            "GlobalTerrainStreamer.CreateTerrainObject",
            "GlobalTerrainStreamer.ConfigureTerrainObject",
            "GlobalTerrainStreamer.PrepareNavMeshUpdate",
            "GlobalTerrainStreamer.ScheduleNavMeshUpdate",
            "GlobalTerrainStreamer.SwapNavMeshData",
            "GlobalTerrainStreamer.ValidateNavMesh"
        };
        ProfilerRecorder[] streamingMarkerRecorders = new ProfilerRecorder[streamingMarkerNames.Length];
        long[] streamingMarkerPeakNanoseconds = new long[streamingMarkerNames.Length];
        for (int i = 0; i < streamingMarkerNames.Length; i++)
            streamingMarkerRecorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, streamingMarkerNames[i], 64);
        ProfilerRecorder streamingMainThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "Main Thread", 64);
        ProfilerRecorder gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory, "GC Allocated In Frame", 64);
        ProfilerRecorder gcCollectRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts, "GC.Collect", 64);
        ProfilerRecorder waitForTargetFpsRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "WaitForTargetFPS", 64);
        ProfilerRecorder renderWaitRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", 64);
        long streamingMainThreadPeakNanoseconds = 0;
        int streamingFrameCount = 0;
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>> streamingOutliers =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>>(8);
        int gcCollectionCount0Before = GC.CollectionCount(0);
        int gcCollectionCount1Before = GC.CollectionCount(1);
        int gcCollectionCount2Before = GC.CollectionCount(2);

        camera.transform.position = new Vector3(forestProbe.x, 8000f, forestProbe.z);
        float deadline = Time.realtimeSinceStartup + 180f;
        PropertyInfo navMeshDurationProperty = streamerType.GetProperty("LastLocalNavMeshBuildMilliseconds");
        float navMeshWaitStarted = -1f;
        float navMeshWaitDeadline = deadline;
        int navMeshWaitFrames = 0;
        PropertyInfo activeTilesProperty = streamer.GetType().GetProperty("ActiveTileCount");
        PropertyInfo desiredTilesProperty = streamer.GetType().GetProperty("DesiredTileCount");
        PropertyInfo activeTreeCountProperty = streamer.GetType().GetProperty("ActiveTreeCount");
        int activeTiles = 0;
        int desiredTiles = 0;
        System.Collections.Generic.List<float> streamingFrameMilliseconds = new System.Collections.Generic.List<float>(2048);
        try
        {
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                streamingFrameCount++;
                float currentStreamingFrameMs = Time.unscaledDeltaTime * 1000f;
                // The first sample includes time spent in the smoke setup before recording began.
                // Keep profiler data for that frame, but exclude its delta from the streaming FPS sample.
                if (streamingFrameCount > 1)
                    streamingFrameMilliseconds.Add(currentStreamingFrameMs);
                float currentMainThreadMs = streamingMainThreadRecorder.Valid
                    ? streamingMainThreadRecorder.LastValue / 1000000f
                    : 0f;
                if (streamingMainThreadRecorder.Valid)
                    streamingMainThreadPeakNanoseconds = Math.Max(
                        streamingMainThreadPeakNanoseconds, streamingMainThreadRecorder.LastValue);
                for (int i = 0; i < streamingMarkerRecorders.Length; i++)
                {
                    ProfilerRecorder recorder = streamingMarkerRecorders[i];
                    if (recorder.Valid)
                        streamingMarkerPeakNanoseconds[i] = Math.Max(
                            streamingMarkerPeakNanoseconds[i], recorder.LastValue);
                }

                float outlierScore = Mathf.Max(streamingFrameCount > 1 ? currentStreamingFrameMs : 0f,
                    currentMainThreadMs);
                if (outlierScore >= 40f)
                {
                    StringBuilder spike = new StringBuilder(768);
                    spike.Append("frame=").Append(streamingFrameCount)
                        .Append(streamingFrameCount == 1 ? "(setup-sample)" : string.Empty)
                        .Append(" delta=").Append(streamingFrameCount > 1
                            ? currentStreamingFrameMs.ToString("F2") + "ms"
                            : "excluded")
                        .Append(" mainThread=").Append(currentMainThreadMs.ToString("F2")).Append("ms")
                        .Append(" gcAlloc=").Append(gcAllocatedInFrameRecorder.Valid
                            ? gcAllocatedInFrameRecorder.LastValue.ToString() + "B"
                            : "n/a")
                        .Append(" gcCollect=").Append(gcCollectRecorder.Valid
                            ? (gcCollectRecorder.LastValue / 1000000f).ToString("F2") + "ms"
                            : "n/a")
                        .Append(" waitTargetFps=").Append(waitForTargetFpsRecorder.Valid
                            ? (waitForTargetFpsRecorder.LastValue / 1000000f).ToString("F2") + "ms"
                            : "n/a")
                        .Append(" renderWait=").Append(renderWaitRecorder.Valid
                            ? (renderWaitRecorder.LastValue / 1000000f).ToString("F2") + "ms"
                            : "n/a")
                        .Append(" gcCollectionsSinceStart=")
                        .Append(GC.CollectionCount(0) - gcCollectionCount0Before).Append('/')
                        .Append(GC.CollectionCount(1) - gcCollectionCount1Before).Append('/')
                        .Append(GC.CollectionCount(2) - gcCollectionCount2Before)
                        .Append(" mono=").Append(Megabytes(Profiler.GetMonoUsedSizeLong()))
                        .Append(" markers=[");
                    for (int markerIndex = 0; markerIndex < streamingMarkerRecorders.Length; markerIndex++)
                    {
                        if (markerIndex > 0) spike.Append(';');
                        ProfilerRecorder marker = streamingMarkerRecorders[markerIndex];
                        spike.Append(streamingMarkerNames[markerIndex].Replace("GlobalTerrainStreamer.", string.Empty))
                            .Append('=')
                            .Append(marker.Valid ? (marker.LastValue / 1000000f).ToString("F2") : "n/a")
                            .Append("ms");
                    }
                    spike.Append(']');
                    if (streamingOutliers.Count < 8)
                    {
                        streamingOutliers.Add(new System.Collections.Generic.KeyValuePair<float, string>(
                            outlierScore, spike.ToString()));
                    }
                    else
                    {
                        int smallestIndex = 0;
                        for (int outlierIndex = 1; outlierIndex < streamingOutliers.Count; outlierIndex++)
                            if (streamingOutliers[outlierIndex].Key < streamingOutliers[smallestIndex].Key)
                                smallestIndex = outlierIndex;
                        if (outlierScore > streamingOutliers[smallestIndex].Key)
                            streamingOutliers[smallestIndex] = new System.Collections.Generic.KeyValuePair<float, string>(
                                outlierScore, spike.ToString());
                    }
                }

                activeTiles = (int)activeTilesProperty.GetValue(streamer);
                desiredTiles = (int)desiredTilesProperty.GetValue(streamer);
                int currentNavMeshMs = (int)navMeshDurationProperty.GetValue(streamer);
                if (desiredTiles > 0 && activeTiles == desiredTiles)
                {
                    if (currentNavMeshMs > 0)
                        break;
                    if (navMeshWaitStarted < 0f)
                    {
                        navMeshWaitStarted = Time.realtimeSinceStartup;
                        navMeshWaitDeadline = navMeshWaitStarted + 20f;
                    }
                    navMeshWaitFrames++;
                    if (Time.realtimeSinceStartup >= navMeshWaitDeadline)
                        break;
                }
            }
        }
        finally
        {
            if (streamingMainThreadRecorder.Valid)
            {
                streamingMainThreadPeakNanoseconds = Math.Max(
                    streamingMainThreadPeakNanoseconds, streamingMainThreadRecorder.LastValue);
                streamingMainThreadRecorder.Dispose();
            }
            if (gcAllocatedInFrameRecorder.Valid) gcAllocatedInFrameRecorder.Dispose();
            if (gcCollectRecorder.Valid) gcCollectRecorder.Dispose();
            if (waitForTargetFpsRecorder.Valid) waitForTargetFpsRecorder.Dispose();
            if (renderWaitRecorder.Valid) renderWaitRecorder.Dispose();
            for (int i = 0; i < streamingMarkerRecorders.Length; i++)
            {
                ProfilerRecorder recorder = streamingMarkerRecorders[i];
                if (recorder.Valid)
                {
                    streamingMarkerPeakNanoseconds[i] = Math.Max(
                        streamingMarkerPeakNanoseconds[i], recorder.LastValue);
                    recorder.Dispose();
                }
            }
        }
        StringBuilder streamingMarkerSummary = new StringBuilder(512);
        for (int i = 0; i < streamingMarkerNames.Length; i++)
        {
            if (i > 0) streamingMarkerSummary.Append(';');
            string shortName = streamingMarkerNames[i].Replace("GlobalTerrainStreamer.", string.Empty);
            streamingMarkerSummary.Append(shortName).Append('=')
                .Append((streamingMarkerPeakNanoseconds[i] / 1000000f).ToString("F2"))
                .Append("ms");
        }
        Debug.Log("[GlobalMapStreamingProfile] frames=" + streamingFrameCount
            + " CPU-main-peak=" + (streamingMainThreadPeakNanoseconds / 1000000f).ToString("F2") + "ms"
            + " markers=" + streamingMarkerSummary);
        streamingOutliers.Sort((a, b) => b.Key.CompareTo(a.Key));
        for (int i = 0; i < streamingOutliers.Count; i++)
            Debug.Log("[GlobalMapStreamingOutlier] rank=" + (i + 1) + " " + streamingOutliers[i].Value);
        Assert.That(desiredTiles, Is.GreaterThan(0), "O ponto de câmera precisa intersectar uma ilha da fase.png.");
        Assert.That(activeTiles, Is.EqualTo(desiredTiles), "Todos os tiles de alta resolução desejados devem carregar.");
        int activeTrees = (int)activeTreeCountProperty.GetValue(streamer);
        Assert.That(activeTrees, Is.GreaterThan(0),
            "Os protótipos de Terrain devem instanciar árvores renderizáveis nos tiles florestais. "
            + "forestWeight=" + maximumForestWeight.ToString("F2") + " probe=" + forestProbe);

        const int performanceFrames = 180;
        float[] frameMilliseconds = new float[performanceFrames];
        long peakMainThreadNanoseconds = 0;
        ProfilerRecorder mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 32);
        try
        {
            for (int i = 0; i < performanceFrames; i++)
            {
                yield return null;
                frameMilliseconds[i] = Time.unscaledDeltaTime * 1000f;
                if (mainThreadRecorder.Valid)
                    peakMainThreadNanoseconds = Math.Max(peakMainThreadNanoseconds, mainThreadRecorder.LastValue);
            }
        }
        finally
        {
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
        }

        Array.Sort(frameMilliseconds);
        float averageFrameMilliseconds = 0f;
        for (int i = 0; i < frameMilliseconds.Length; i++)
            averageFrameMilliseconds += frameMilliseconds[i];
        averageFrameMilliseconds /= frameMilliseconds.Length;
        float p95FrameMilliseconds = frameMilliseconds[(int)(frameMilliseconds.Length * 0.95f)];
        streamingFrameMilliseconds.Sort();
        float streamingP95FrameMilliseconds = streamingFrameMilliseconds.Count > 0
            ? streamingFrameMilliseconds[(int)(streamingFrameMilliseconds.Count * 0.95f)]
            : 0f;
        float streamingMaxFrameMilliseconds = streamingFrameMilliseconds.Count > 0
            ? streamingFrameMilliseconds[streamingFrameMilliseconds.Count - 1]
            : 0f;
        int sceneObjectCount = CountSceneObjects(scene);
        float maximumTreeDistance = 0f;
        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
            if (terrains[i] != null && terrains[i].gameObject.scene == scene)
                maximumTreeDistance = Mathf.Max(maximumTreeDistance, terrains[i].treeDistance);
        Assert.That(maximumTreeDistance, Is.LessThanOrEqualTo(500f),
            "Árvores locais devem ser ocultadas antes de ultrapassar 0,5 km da câmera.");
        int generationMilliseconds = (int)streamerType.GetProperty("LastGenerationMilliseconds").GetValue(streamer);
        int navMeshMilliseconds = (int)navMeshDurationProperty.GetValue(streamer);
        BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo navMeshSurfaceField = streamerType.GetField("localNavMeshSurface", privateInstance);
        FieldInfo runtimeNavMeshDataField = streamerType.GetField("runtimeLocalNavMeshData", privateInstance);
        object navMeshSurface = navMeshSurfaceField != null ? navMeshSurfaceField.GetValue(streamer) : null;
        bool navMeshSurfaceEnabled = navMeshSurface != null
            && (bool)navMeshSurface.GetType().GetProperty("enabled").GetValue(navMeshSurface);
        PropertyInfo navMeshDataProperty = navMeshSurface != null
            ? navMeshSurface.GetType().GetProperty("navMeshData")
            : null;
        bool navMeshDataAssigned = navMeshDataProperty != null && navMeshDataProperty.GetValue(navMeshSurface) != null;
        bool runtimeNavMeshDataCreated = runtimeNavMeshDataField != null
            && runtimeNavMeshDataField.GetValue(streamer) != null;
        Debug.Log("[GlobalMapNavMeshDiagnostic] completed=" + (navMeshMilliseconds > 0)
            + " waited=" + (navMeshWaitStarted >= 0f
                ? (Time.realtimeSinceStartup - navMeshWaitStarted).ToString("F2") + "s"
                : "0.00s")
            + " waitFrames=" + navMeshWaitFrames
            + " buildMs=" + navMeshMilliseconds
            + " surface=" + (navMeshSurface != null)
            + " surfaceEnabled=" + navMeshSurfaceEnabled
            + " dataAssigned=" + navMeshDataAssigned
            + " runtimeData=" + runtimeNavMeshDataCreated);
        Debug.Log("[GlobalMapPerfSmoke] loadedTiles=" + activeTiles + "/" + desiredTiles
            + " trees=" + activeTrees
            + " treeDistanceMax=" + maximumTreeDistance.ToString("F0") + "m"
            + " terrainGeneration=" + generationMilliseconds + "ms"
            + " localNavMesh=" + navMeshMilliseconds + "ms"
            + " streamFrames(p95/max)=" + streamingP95FrameMilliseconds.ToString("F2") + "/"
            + streamingMaxFrameMilliseconds.ToString("F2") + "ms"
            + " FPS(avg)=" + (1000f / Mathf.Max(0.001f, averageFrameMilliseconds)).ToString("F1")
            + " frameMs(avg/p95)=" + averageFrameMilliseconds.ToString("F2") + "/" + p95FrameMilliseconds.ToString("F2")
            + " CPU-main-peak=" + (peakMainThreadNanoseconds / 1000000f).ToString("F2") + " ms"
            + " Unity/Mono/GPU-memory=" + Megabytes(Profiler.GetTotalAllocatedMemoryLong()) + "/"
            + Megabytes(Profiler.GetMonoUsedSizeLong()) + "/" + Megabytes(Profiler.GetAllocatedMemoryForGraphicsDriver())
            + " GameObjects=" + sceneObjectCount);

        Assert.That(globalTerrain.gameObject.GetComponent<Collider>(), Is.Not.Null,
            "O terreno-base continua com o colisor físico enquanto o visual usa tiles detalhados.");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name) return roots[i];
        return null;
    }

    private static Component FindInScene(GameObject root, Type type)
    {
        if (root == null || type == null) return null;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && type.IsInstanceOfType(components[i])) return components[i];
        return null;
    }

    private static int CountInScene(Scene scene, Type type)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int count = 0;
        for (int i = 0; i < roots.Length; i++)
            count += CountInHierarchy(roots[i], type);
        return count;
    }

    private static int CountInHierarchy(GameObject gameObject, Type type)
    {
        int count = 0;
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && type.IsInstanceOfType(components[i])) count++;
        for (int i = 0; i < gameObject.transform.childCount; i++)
            count += CountInHierarchy(gameObject.transform.GetChild(i).gameObject, type);
        return count;
    }

    private static int CountSceneObjects(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int count = 0;
        for (int i = 0; i < roots.Length; i++)
            count += CountObjectTree(roots[i]);
        return count;
    }

    private static int CountObjectTree(GameObject gameObject)
    {
        int count = gameObject.activeInHierarchy ? 1 : 0;
        for (int i = 0; i < gameObject.transform.childCount; i++)
            count += CountObjectTree(gameObject.transform.GetChild(i).gameObject);
        return count;
    }

    private static string DescribeNavalHits(Terrain terrain, Vector3 position)
    {
        string details = " probe=" + position + " terrain=" + (terrain != null);
        if (terrain != null)
        {
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            details += " terrainY=" + terrain.transform.position.y.ToString("F1")
                + " sampleY=" + terrain.SampleHeight(position).ToString("F1")
                + " collider=" + (terrainCollider != null && terrainCollider.enabled);
        }

        Type navalPlacementType = FindProjectType("NavalPlacementResolver");
        MethodInfo buildRayMask = navalPlacementType.GetMethod("BuildRayMask", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo looksLikeWater = navalPlacementType.GetMethod("LooksLikeWater", BindingFlags.Static | BindingFlags.NonPublic);
        int mask = (int)buildRayMask.Invoke(null, null);
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(position.x, 1000f, position.z),
            Vector3.down,
            3000f,
            mask,
            QueryTriggerInteraction.Collide);
        details += " rayHits=" + hits.Length;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            bool water = (bool)looksLikeWater.Invoke(null, new object[] { collider });
            details += " {" + collider.name + ", layer=" + LayerMask.LayerToName(collider.gameObject.layer)
                + ", y=" + hits[i].point.y.ToString("F1") + ", water=" + water + "}";
        }

        return details;
    }

    private static Type FindProjectType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName, false);
            if (type != null) return type;
        }
        return null;
    }

    private static object GetField(Component component, string fieldName)
    {
        return component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public).GetValue(component);
    }

    private static string Megabytes(long bytes)
    {
        return (bytes / (1024f * 1024f)).ToString("F0") + " MB";
    }
}
