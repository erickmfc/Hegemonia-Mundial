using System;
using System.Collections;
using System.Reflection;
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
        if (EditorApplication.isPlaying)
            yield return new ExitPlayMode();

        Scene previous = string.IsNullOrEmpty(previousActiveScenePath)
            ? default
            : SceneManager.GetSceneByPath(previousActiveScenePath);
        if (previous.IsValid() && previous.isLoaded)
            SceneManager.SetActiveScene(previous);
    }

    [UnityTest]
    public IEnumerator ExistingOceanRulesAndTerrainStreamingRunTogether()
    {
        yield return new EnterPlayMode();

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

        camera.transform.position = new Vector3(forestProbe.x, 8000f, forestProbe.z);
        PropertyInfo activeTilesProperty = streamer.GetType().GetProperty("ActiveTileCount");
        PropertyInfo desiredTilesProperty = streamer.GetType().GetProperty("DesiredTileCount");
        PropertyInfo activeTreeCountProperty = streamer.GetType().GetProperty("ActiveTreeCount");
        float deadline = Time.realtimeSinceStartup + 180f;
        int activeTiles = 0;
        int desiredTiles = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            activeTiles = (int)activeTilesProperty.GetValue(streamer);
            desiredTiles = (int)desiredTilesProperty.GetValue(streamer);
            if (desiredTiles > 0 && activeTiles == desiredTiles)
                break;
        }
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
        int sceneObjectCount = CountSceneObjects(scene);
        Debug.Log("[GlobalMapPerfSmoke] loadedTiles=" + activeTiles + "/" + desiredTiles
            + " trees=" + activeTrees
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
