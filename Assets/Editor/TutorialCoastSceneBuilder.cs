using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialCoastSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Tutorial Coast Scene Final.unity";
    static readonly Vector2[] RoadPath =
    {
        new Vector2(-165f, -145f), new Vector2(-130f, -112f), new Vector2(-76f, -82f),
        new Vector2(-18f, -56f), new Vector2(26f, -18f), new Vector2(47f, 34f),
        new Vector2(83f, 82f), new Vector2(132f, 126f)
    };

    [InitializeOnLoadMethod]
    static void BuildOnceAfterImport()
    {
        if (EditorPrefs.GetBool("Hegemonia_TutorialCoastScene_Built_v6", false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool("Hegemonia_TutorialCoastScene_Built_v6", false)) return;
            BuildTutorialCoastScene();
            EditorPrefs.SetBool("Hegemonia_TutorialCoastScene_Built_v6", true);
        };
    }

    [InitializeOnLoadMethod]
    static void FinalizeOnceAfterImport()
    {
        if (EditorPrefs.GetBool("Hegemonia_TutorialCoastScene_Finalized_v4", false)) return;
        EditorApplication.delayCall += () =>
        {
            FinalizeTutorialCoastCamera();
            EditorPrefs.SetBool("Hegemonia_TutorialCoastScene_Finalized_v4", true);
        };
    }

    [MenuItem("Tools/Hegemonia/Build Tutorial Coast Scene")]
    public static void BuildTutorialCoastScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && string.IsNullOrEmpty(active.path)) EditorSceneManager.CloseScene(active, true);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        GameObject world = new GameObject("TUTORIAL COAST - Procedural World");
        GameObject terrainRoot = new GameObject("01_Terrain_Painter_Result");
        terrainRoot.transform.SetParent(world.transform);
        GameObject roadRoot = new GameObject("02_EasyRoads3D_Spline_Road");
        roadRoot.transform.SetParent(world.transform);
        GameObject vegetationRoot = new GameObject("03_VegetationSpawner_Biomes");
        vegetationRoot.transform.SetParent(world.transform);
        GameObject mountainsRoot = new GameObject("04_Distant_Mountains");
        mountainsRoot.transform.SetParent(world.transform);

        Material grass = MakeMaterial("Coast Grass", new Color(0.20f, 0.42f, 0.18f));
        Material sand = MakeMaterial("Coast Sand", new Color(0.78f, 0.66f, 0.42f));
        Material rock = MakeMaterial("Coast Rock", new Color(0.22f, 0.28f, 0.28f));
        Material waterMat = MakeMaterial("Ocean Water", new Color(0.06f, 0.28f, 0.42f), 0.92f);
        Material roadMat = MakeMaterial("EasyRoads Asphalt", new Color(0.075f, 0.085f, 0.09f), 0.25f);
        Material shoulderMat = MakeMaterial("Road Shoulder", new Color(0.35f, 0.32f, 0.23f));
        Material trunkMat = MakeMaterial("Vegetation Trunks", new Color(0.18f, 0.09f, 0.035f));
        Material leafMat = MakeMaterial("Vegetation Leaves", new Color(0.12f, 0.32f, 0.10f));
        Material peakMat = MakeMaterial("Mountain Rock", new Color(0.30f, 0.37f, 0.38f));

        Terrain terrain = BuildTerrain(terrainRoot.transform, grass, sand, rock);
        BuildWater(world.transform, waterMat);
        BuildRoad(roadRoot.transform, roadMat, shoulderMat);
        BuildMountains(mountainsRoot.transform, peakMat);
        BuildVegetation(vegetationRoot.transform, trunkMat, leafMat);
        BuildLightingAndCamera(world.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrain.gameObject;
        Debug.Log("Tutorial Coast Scene criada em " + ScenePath + ". Terreno, praia, água, estrada spline, montanhas e biomas foram montados.");
    }

    [MenuItem("Tools/Hegemonia/Finalize Tutorial Coast Camera")]
    public static void FinalizeTutorialCoastCamera()
    {
        Scene active = SceneManager.GetActiveScene();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (!loaded.IsValid() || loaded == active) continue;
            foreach (GameObject root in loaded.GetRootGameObjects()) root.SetActive(false);
        }
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras) camera.enabled = camera.gameObject.name == "Tutorial Coast Camera";
        RenderSettings.fogDensity = 0.00045f;
        RenderSettings.fogColor = new Color(0.52f, 0.64f, 0.69f);
        RenderSettings.ambientLight = new Color(0.58f, 0.64f, 0.67f);
        if (active.IsValid()) EditorSceneManager.SaveScene(active);
        Debug.Log("Cenas antigas ocultadas e câmera costeira ativada; os arquivos originais continuam preservados.");
    }

    static Terrain BuildTerrain(Transform parent, Material grass, Material sand, Material rock)
    {
        TerrainData data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256, size = new Vector3(400f, 80f, 400f) };
        int n = data.heightmapResolution;
        float[,] heights = new float[n, n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                float nx = x / (float)(n - 1);
                float nz = z / (float)(n - 1);
                float broad = Mathf.PerlinNoise(nx * 2.2f + 0.2f, nz * 2.2f + 0.7f) * 0.055f;
                float detail = Mathf.PerlinNoise(nx * 8.5f, nz * 8.5f) * 0.012f;
                float hillA = Mathf.Exp(-(((nx - 0.22f) * (nx - 0.22f)) / 0.045f + ((nz - 0.66f) * (nz - 0.66f)) / 0.20f)) * 0.30f;
                float hillB = Mathf.Exp(-(((nx - 0.74f) * (nx - 0.74f)) / 0.075f + ((nz - 0.46f) * (nz - 0.46f)) / 0.13f)) * 0.18f;
                float coast = Mathf.Clamp01((nz - 0.08f) / 0.18f) * 0.025f;
                heights[z, x] = Mathf.Clamp01(0.035f + broad + detail + hillA + hillB + coast);
            }
        }
        data.SetHeights(0, 0, heights);

        TerrainLayer[] layers = FindTerrainLayers();
        if (layers.Length > 0)
        {
            data.terrainLayers = layers;
            float[,,] alpha = new float[data.alphamapResolution, data.alphamapResolution, layers.Length];
            for (int z = 0; z < data.alphamapResolution; z++)
            {
                for (int x = 0; x < data.alphamapResolution; x++)
                {
                    float u = x / (float)(data.alphamapResolution - 1);
                    float v = z / (float)(data.alphamapResolution - 1);
                    float h = data.GetInterpolatedHeight(u, v) / data.size.y;
                    float slope = data.GetSteepness(u, v) / 90f;
                    float low = Mathf.Clamp01(1f - h * 9f) * Mathf.Clamp01(1f - slope * 3f);
                    float high = Mathf.Clamp01((h - 0.18f) * 4f) + slope * 0.8f;
                    float mid = Mathf.Clamp01(1f - low - high);
                    if (layers.Length == 1) alpha[z, x, 0] = 1f;
                    else
                    {
                        alpha[z, x, 0] = mid;
                        alpha[z, x, 1] = low;
                        alpha[z, x, 2 % layers.Length] += high;
                        float total = 0f;
                        for (int i = 0; i < layers.Length; i++) total += alpha[z, x, i];
                        for (int i = 0; i < layers.Length; i++) alpha[z, x, i] /= Mathf.Max(0.001f, total);
                    }
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain_Procedural_Painted_Biomes";
        go.transform.SetParent(parent);
        Terrain terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.materialType = Terrain.MaterialType.BuiltInStandard;
        return terrain;
    }

    static TerrainLayer[] FindTerrainLayers()
    {
        List<TerrainLayer> result = new List<TerrainLayer>();
        string[] queries = { "Grass", "Sand", "Mossy_Rock", "Cliff" };
        foreach (string query in queries)
        {
            string[] ids = AssetDatabase.FindAssets(query + " t:TerrainLayer");
            if (ids.Length == 0) continue;
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(ids[0]));
            if (layer != null && !result.Contains(layer)) result.Add(layer);
        }
        return result.ToArray();
    }

    static void BuildWater(Transform parent, Material material)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Ocean_Water_Level";
        water.transform.SetParent(parent);
        water.transform.position = new Vector3(0f, -1.5f, -150f);
        water.transform.localScale = new Vector3(22f, 1f, 8f);
        water.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void BuildRoad(Transform parent, Material road, Material shoulder)
    {
        CreateRibbon("EasyRoads3D_Spline_Asphalt", parent, RoadPath, 5.5f, 0.42f, road);
        CreateRibbon("EasyRoads3D_Spline_Shoulders", parent, RoadPath, 7.2f, 0.18f, shoulder);
        for (int i = 0; i < RoadPath.Length - 1; i++)
        {
            Vector2 a = RoadPath[i], b = RoadPath[i + 1];
            Vector2 d = (b - a).normalized;
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Road_Guard_Post_" + i;
            marker.transform.SetParent(parent);
            marker.transform.position = new Vector3(a.x + d.x * 0.5f, 7.0f, a.y + d.y * 0.5f);
            marker.transform.localScale = new Vector3(0.15f, 0.8f, 0.15f);
            marker.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Road Post " + i, new Color(0.82f, 0.78f, 0.58f));
        }
    }

    static void CreateRibbon(string name, Transform parent, Vector2[] path, float width, float y, Material material)
    {
        Mesh mesh = new Mesh { name = name + " Mesh" };
        Vector3[] vertices = new Vector3[path.Length * 2];
        int[] triangles = new int[(path.Length - 1) * 6];
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 forward = (i == 0 ? path[1] - path[0] : i == path.Length - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x) * width;
            vertices[i * 2] = new Vector3(path[i].x + side.x, y + i * 0.12f, path[i].y + side.y);
            vertices[i * 2 + 1] = new Vector3(path[i].x - side.x, y + i * 0.12f, path[i].y - side.y);
            if (i < path.Length - 1)
            {
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void BuildMountains(Transform parent, Material material)
    {
        CreatePeak(parent, "Mountain_Left", new Vector3(-125f, 0f, 175f), new Vector3(82f, 52f, 70f));
        CreatePeak(parent, "Mountain_Center", new Vector3(5f, 0f, 210f), new Vector3(96f, 68f, 78f));
        CreatePeak(parent, "Mountain_Right", new Vector3(155f, 0f, 190f), new Vector3(88f, 58f, 72f));
        CreatePeak(parent, "Mountain_Haze", new Vector3(270f, 0f, 230f), new Vector3(75f, 48f, 70f));
        foreach (Transform child in parent) child.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void CreatePeak(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject peak = new GameObject(name);
        peak.name = name;
        peak.transform.SetParent(parent);
        peak.transform.position = position;
        Mesh mesh = new Mesh { name = name + " Mesh" };
        const int sides = 8;
        Vector3[] vertices = new Vector3[sides + 1];
        int[] triangles = new int[sides * 3];
        vertices[0] = new Vector3(0f, scale.y, 0f);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            float radius = 0.82f + Mathf.Sin(i * 1.7f) * 0.10f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * scale.x * radius, 0f, Mathf.Sin(angle) * scale.z * radius);
            int t = i * 3;
            triangles[t] = 0; triangles[t + 1] = i + 1; triangles[t + 2] = (i + 1) % sides + 1;
        }
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
        peak.AddComponent<MeshFilter>().sharedMesh = mesh;
        peak.AddComponent<MeshRenderer>();
    }

    static void BuildVegetation(Transform parent, Material trunk, Material leaves)
    {
        Random.InitState(1907);
        for (int i = 0; i < 95; i++)
        {
            float x = Random.Range(-175f, 175f);
            float z = Random.Range(-95f, 155f);
            if (z < -15f && Mathf.Abs(x) < 105f) continue;
            if (DistanceToRoad(new Vector2(x, z)) < 13f) continue;
            float scale = Random.Range(0.75f, 1.45f);
            MakeTree(parent, "Biome_Tree_" + i, new Vector3(x, 6.0f, z), scale, trunk, leaves);
        }
        for (int i = 0; i < 42; i++)
        {
            float x = Random.Range(-180f, 180f), z = Random.Range(-70f, 165f);
            if (DistanceToRoad(new Vector2(x, z)) < 9f) continue;
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Biome_Shrub_" + i;
            bush.transform.SetParent(parent);
            bush.transform.position = new Vector3(x, 5.0f, z);
            bush.transform.localScale = new Vector3(3f, 1.5f, 3f);
            bush.GetComponent<Renderer>().sharedMaterial = leaves;
        }
    }

    static void MakeTree(Transform parent, string name, Vector3 position, float scale, Material trunk, Material leaves)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Trunk"; stem.transform.SetParent(root.transform);
        stem.transform.localPosition = new Vector3(0f, 2.4f * scale, 0f);
        stem.transform.localScale = new Vector3(0.55f * scale, 2.4f * scale, 0.55f * scale);
        stem.GetComponent<Renderer>().sharedMaterial = trunk;
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Canopy"; crown.transform.SetParent(root.transform);
        crown.transform.localPosition = new Vector3(0f, 6.0f * scale, 0f);
        crown.transform.localScale = new Vector3(3.2f * scale, 3.8f * scale, 3.2f * scale);
        crown.GetComponent<Renderer>().sharedMaterial = leaves;
    }

    static float DistanceToRoad(Vector2 point)
    {
        float best = float.MaxValue;
        for (int i = 0; i < RoadPath.Length - 1; i++)
        {
            Vector2 a = RoadPath[i], b = RoadPath[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
        }
        return best;
    }

    static void BuildLightingAndCamera(Transform parent)
    {
        RenderSettings.ambientLight = new Color(0.42f, 0.50f, 0.56f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.45f, 0.57f, 0.62f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0018f;
        GameObject sun = new GameObject("Sunset Directional Light");
        sun.transform.SetParent(parent);
        sun.transform.rotation = Quaternion.Euler(36f, -32f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.25f; light.color = new Color(1.0f, 0.82f, 0.63f);
        GameObject cameraObject = new GameObject("Tutorial Coast Camera");
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.position = new Vector3(245f, 108f, 285f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 52f; camera.farClipPlane = 1000f; camera.backgroundColor = new Color(0.38f, 0.52f, 0.61f);
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.transform.LookAt(new Vector3(0f, 25f, 65f));
        cameraObject.tag = "MainCamera";
        GameObject marker = new GameObject("Tutorial_Camera_Target");
        marker.transform.SetParent(parent);
        marker.transform.position = new Vector3(0f, 25f, 65f);
    }

    static Material MakeMaterial(string name, Color color, float smoothness = 0.05f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
