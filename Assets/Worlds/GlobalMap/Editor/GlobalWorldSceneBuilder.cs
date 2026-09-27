using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Builds the isolated global-map scene from the official references.</summary>
public static class GlobalWorldSceneBuilder
{
    private const string RootFolder = "Assets/Worlds/GlobalMap";
    private const string DataFolder = RootFolder + "/Data";
    private const string ScenePath = "Assets/Scenes/GlobalMapRTS.unity";
    private const string DefinitionPath = DataFolder + "/GlobalWorldDefinition.asset";
    private const string TerrainDataPath = DataFolder + "/GlobalMap_CoarseTerrain_VisualGate.asset";
    private const string MacroBiomeTexturePath = DataFolder + "/GlobalMap_MacroBiomes.png";
    private const string MacroBiomeLayerPath = DataFolder + "/GlobalMap_MacroBiomes.terrainlayer";
    private const string ForestGroundTexturePath = DataFolder + "/GlobalMap_ForestGround_512m.png";
    private const string ForestGroundNormalPath = DataFolder + "/GlobalMap_ForestGround_Normal.png";

    private const string AuthorityTexturePath = RootFolder + "/References/fase.png";
    private const string AppearanceTexturePath = RootFolder + "/References/referencia_biomas_vista_superior.png";
    private const string SeaPrefabPath = "Assets/Mar_Feito/Models/Sea.prefab";
    private const string SeaSurfaceMaterialPath = "Assets/Mar_Feito/Models/Sea/Sea.mat";
    private const string SeaSimulationMaterialPath = "Assets/Mar_Feito/Models/Sea/Mar_Novo.mat";

    [MenuItem("Hegemonia/World/Build Global Map RTS Scene")]
    public static void BuildGlobalWorldScene()
    {
        try
        {
            EnsureFolder("Assets/Worlds");
            EnsureFolder(RootFolder);
            EnsureFolder(DataFolder);
            EnsureFolder("Assets/Scenes");

            ImportReference(AuthorityTexturePath, true);
            ImportReference(AppearanceTexturePath, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GlobalWorldDefinition world = BuildDefinition();
            world.ResetRuntimeData();
            world.InitializeRuntimeData();
            ValidateReferenceMask(world);
            ValidateTreePrefabs(world);

            TerrainData globalTerrainData = BuildGlobalTerrainData(world);
            Scene scene = PrepareSceneForBuild();
            BuildSceneObjects(scene, world, globalTerrainData);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unity não conseguiu salvar a cena " + ScenePath);

            Debug.Log("[GlobalMap] Cena RTS construída: " + ScenePath
                + ". Um Sea.prefab, um OceanAdvanced, mar médio em Y=" + world.seaLevel.ToString("F1") + ".");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Hegemonia/World/Rebake Global Terrain Data (Safe)")]
    public static void RebakeGlobalTerrainDataSafe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia do Play Mode antes de rebakar os dados do Terrain.");
        if (!File.Exists(ScenePath)
            || AssetDatabase.LoadAssetAtPath<GlobalWorldDefinition>(DefinitionPath) == null)
            throw new FileNotFoundException("A cena e a definição existentes devem estar presentes antes do rebake.");

        try
        {
            // Keep the previously modified TerrainData asset available for
            // recovery. Build the refreshed data under a new path, then update
            // only the existing Terrain and TerrainCollider references.
            GlobalWorldDefinition world = BuildDefinition();
            world.ResetRuntimeData();
            world.InitializeRuntimeData();
            ValidateReferenceMask(world);
            ValidateTreePrefabs(world);
            TerrainData data = BuildGlobalTerrainData(world);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TerrainDataPath, ImportAssetOptions.ForceSynchronousImport);
            data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
                throw new InvalidOperationException("O Unity não conseguiu reabrir o novo TerrainData após salvá-lo.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            List<Terrain> terrains = GetSceneComponents<Terrain>(scene);
            if (terrains.Count != 1)
                throw new InvalidOperationException("A cena deve conter exatamente um Terrain ao atualizar seus dados.");
            Terrain terrain = terrains[0];
            terrain.terrainData = data;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
                collider.terrainData = data;
            EditorUtility.SetDirty(terrain);
            if (collider != null)
                EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unity não conseguiu salvar a cena após atualizar a referência do TerrainData.");

            Debug.Log("[GlobalMap] Rebake seguro concluído: " + data.heightmapResolution
                + " amostras de altura; terreno global macro e " + world.terrainLayers.Length
                + " camadas locais atualizados. Hierarquia e oceano preservados.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Hegemonia/World/Validate Global Map RTS Scene")]
    public static void ValidateGlobalWorldScene()
    {
        if (!File.Exists(ScenePath))
            throw new FileNotFoundException("A cena global ainda não foi construída.", ScenePath);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        List<Terrain> terrains = GetSceneComponents<Terrain>(scene);
        List<GlobalTerrainStreamer> streamers = GetSceneComponents<GlobalTerrainStreamer>(scene);
        List<OceanAdvanced> oceans = GetSceneComponents<OceanAdvanced>(scene);
        List<MeshCollider> seaColliders = GetSceneComponents<MeshCollider>(scene);
        List<CameraController> cameraControllers = GetSceneComponents<CameraController>(scene);
        List<MapaGeralController> mapControllers = GetSceneComponents<MapaGeralController>(scene);

        if (terrains.Count != 1)
            throw new InvalidOperationException("Esperado um Terrain global; encontrados: " + terrains.Count);
        if (streamers.Count != 1 || streamers[0].globalTerrain != terrains[0])
            throw new InvalidOperationException("Streamer ausente ou não ligado ao Terrain global.");
        if (oceans.Count != 1)
            throw new InvalidOperationException("A cena deve conter exatamente um OceanAdvanced; encontrados: " + oceans.Count);
        if (seaColliders.Count != 1 || seaColliders[0].GetComponent<OceanAdvanced>() == null)
            throw new InvalidOperationException("Esperado o colisor do Sea.prefab no único oceano existente.");
        if (cameraControllers.Count != 1 || cameraControllers[0].alturaMaximaZoom < 280000f)
            throw new InvalidOperationException("A câmera da cena global está ausente ou sem faixa de zoom global.");
        if (mapControllers.Count != 1)
            throw new InvalidOperationException("A cena global deve ter exatamente um MapaGeralController para o atalho M.");

        GlobalWorldDefinition world = streamers[0].world;
        OceanAdvanced ocean = oceans[0];
        Terrain terrain = terrains[0];
        if (world == null || terrain.terrainData == null)
            throw new InvalidOperationException("A definição ou os dados do terreno não estão atribuídos.");
        if (Mathf.Abs(world.seaLevel) > 0.001f)
            throw new InvalidOperationException("O nível médio do mar deve continuar em Y=0.");
        if (Mathf.Abs(terrain.transform.position.y - world.terrainBaseY) > 0.01f)
            throw new InvalidOperationException("A origem do Terrain não corresponde à faixa vertical da definição.");
        if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length != 1
            || terrain.terrainData.terrainLayers[0] == null
            || terrain.terrainData.terrainLayers[0].name != "GlobalMap_MacroBiomes")
            throw new InvalidOperationException("A camada única de macrobiomas não está no Terrain global.");
        if (ocean.ocean != AssetDatabase.LoadAssetAtPath<Material>(SeaSimulationMaterialPath))
            throw new InvalidOperationException("OceanAdvanced não está usando o material de simulação do projeto.");
        if (ocean.sun == null || seaColliders[0].sharedMesh == null)
            throw new InvalidOperationException("A luz ou a malha do Sea.prefab não está atribuída.");
        if (Mathf.Abs(seaColliders[0].bounds.center.y - world.seaLevel) > 0.05f
            || seaColliders[0].bounds.size.y > 0.1f)
            throw new InvalidOperationException("A superfície física do Sea.prefab não está no nível médio do mar.");

        world.InitializeRuntimeData();
        ValidateReferenceMask(world);
        ValidateTreePrefabs(world);
        Debug.Log("[GlobalMap] Validação aprovada: Terrain 512×288 km, seaLevel=0, máscara das referências, "
            + "um Sea.prefab/OceanAdvanced e câmera/streaming conectados.");
    }

    private static GlobalWorldDefinition BuildDefinition()
    {
        GlobalWorldDefinition world = AssetDatabase.LoadAssetAtPath<GlobalWorldDefinition>(DefinitionPath);
        if (world == null)
        {
            world = ScriptableObject.CreateInstance<GlobalWorldDefinition>();
            world.name = "GlobalWorldDefinition";
            AssetDatabase.CreateAsset(world, DefinitionPath);
        }

        world.authorityMap = AssetDatabase.LoadAssetAtPath<Texture2D>(AuthorityTexturePath);
        world.appearanceMap = AssetDatabase.LoadAssetAtPath<Texture2D>(AppearanceTexturePath);
        world.worldSize = new Vector2(512000f, 512000f);
        world.mapFootprintHeight = 288000f;
        world.terrainTileSize = 8000f;
        world.globalHeightResolution = 2049;
        world.localHeightResolution = 513;
        world.alphamapResolution = 512;
        world.localAlphamapResolution = 512;
        world.seaLevel = 0f;
        world.minimumLandClearance = 4f;
        world.terrainBaseY = -200f;
        // Reserve room above the tallest combined inland/ridge relief so
        // Terrain height normalization does not clamp mountain crests.
        world.terrainVerticalSize = 1800f;
        world.beachWidth = 1600f;
        world.coastalElevationBlendWidth = 14000f;
        world.maximumMountainHeight = 1250f;
        world.terrainLayers = new[]
        {
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Grass_2_DiffuseGrass_2_Normal2023054508611406.terrainlayer",
                DataFolder + "/GlobalMap_GrassGreen.terrainlayer", "GlobalMap_GrassGreen", new Vector2(48f, 48f)),
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Grass_1_DiffuseGrass_1_Normal2023054508611406.terrainlayer",
                DataFolder + "/GlobalMap_Grass.terrainlayer", "GlobalMap_Grass", new Vector2(54f, 54f)),
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Dirt_1_DiffuseDirt_1_Normal2023054508611406.terrainlayer",
                DataFolder + "/GlobalMap_Dirt.terrainlayer", "GlobalMap_Dirt", new Vector2(58f, 58f)),
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Sand_DiffuseSand_Normal2023054508611406.terrainlayer",
                DataFolder + "/GlobalMap_Sand.terrainlayer", "GlobalMap_Sand", new Vector2(72f, 72f)),
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Cliff_1_DiffuseCliff_1_Normaleda51f90c7f37482.terrainlayer",
                DataFolder + "/GlobalMap_Rock.terrainlayer", "GlobalMap_Rock", new Vector2(72f, 72f)),
            CreateGlobalTerrainLayer(
                "Assets/_TerrainAutoUpgrade/layer_Grass_1_DiffuseGrass_1_Normal2023054508611406.terrainlayer",
                DataFolder + "/GlobalMap_Forest.terrainlayer", "GlobalMap_Forest", new Vector2(512f, 512f))
        };
        GameObject[] sourceTreePrefabs = new[]
        {
            LoadRequired<GameObject>("Assets/Simple InterceptMissile&TurretBehaviour/PolygonWar/Prefabs/Environments/SM_Env_Tree_01.prefab"),
            LoadRequired<GameObject>("Assets/Simple InterceptMissile&TurretBehaviour/PolygonWar/Prefabs/Environments/SM_Env_Tree_02.prefab"),
            LoadRequired<GameObject>("Assets/Simple InterceptMissile&TurretBehaviour/PolygonWar/Prefabs/Environments/SM_Env_Tree_03.prefab")
        };
        world.treePrefabs = GlobalTreeLodBuilder.BuildTreePrefabs(sourceTreePrefabs);
        world.forestClusterPrototypeCount = 0;
        world.treeCandidateSpacing = 48f;
        // The runtime converts forest-mask area to an instance budget, using
        // this target density only inside suitable, non-arid forest regions.
        world.forestTreesPerSquareKilometre = 1200f;
        EditorUtility.SetDirty(world);
        return world;
    }

    private static TerrainData BuildGlobalTerrainData(GlobalWorldDefinition world)
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (data == null)
        {
            data = new TerrainData { name = "GlobalMap_CoarseTerrain" };
            AssetDatabase.CreateAsset(data, TerrainDataPath);
        }

        data.heightmapResolution = world.globalHeightResolution;
        data.alphamapResolution = world.alphamapResolution;
        data.baseMapResolution = 1024;
        data.size = new Vector3(world.worldSize.x, world.terrainVerticalSize, world.mapFootprintHeight);
        data.terrainLayers = new[] { CreateGlobalMacroBiomeLayer(world) };

        int heightResolution = data.heightmapResolution;
        float[,] heights = new float[heightResolution, heightResolution];
        for (int z = 0; z < heightResolution; z++)
        {
            float worldZ = world.MapMinZ + world.mapFootprintHeight * z / (heightResolution - 1f);
            for (int x = 0; x < heightResolution; x++)
            {
                float worldX = world.MapMinX + world.worldSize.x * x / (heightResolution - 1f);
                float height = world.HeightAtWorld(worldX, worldZ);
                heights[z, x] = Mathf.Clamp01((height - world.terrainBaseY) / world.terrainVerticalSize);
            }

            if (z % 48 == 0)
                EditorUtility.DisplayProgressBar("Global Map", "Gerando relevo de baixa frequência", z / (float)heightResolution * 0.75f);
        }
        data.SetHeights(0, 0, heights);

        int alphaResolution = data.alphamapResolution;
        float[,,] alphamaps = new float[alphaResolution, alphaResolution, 1];
        for (int z = 0; z < alphaResolution; z++)
        {
            for (int x = 0; x < alphaResolution; x++)
                alphamaps[z, x, 0] = 1f;

            if (z % 48 == 0)
                EditorUtility.DisplayProgressBar("Global Map", "Preparando a camada de macrobiomas", 0.75f + z / (float)alphaResolution * 0.25f);
        }
        data.SetAlphamaps(0, 0, alphamaps);
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static TerrainLayer CreateGlobalMacroBiomeLayer(GlobalWorldDefinition world)
    {
        const int textureWidth = 1024;
        const int textureHeight = 576;
        Color32[] pixels = new Color32[textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; y++)
        {
            float worldZ = world.MapMinZ + world.mapFootprintHeight * y / (textureHeight - 1f);
            for (int x = 0; x < textureWidth; x++)
            {
                float worldX = world.MapMinX + world.worldSize.x * x / (textureWidth - 1f);
                pixels[y * textureWidth + x] = world.MacroBiomeColorAtWorld(worldX, worldZ);
            }
        }

        Texture2D generated = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, true, false)
        {
            name = "GlobalMap_MacroBiomes_Generated"
        };
        generated.SetPixels32(pixels);
        generated.Apply(true, false);
        File.WriteAllBytes(MacroBiomeTexturePath, generated.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(generated);
        AssetDatabase.ImportAsset(MacroBiomeTexturePath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(MacroBiomeTexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("A textura processual de macrobiomas não foi importada.");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        Texture2D macroTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MacroBiomeTexturePath);
        if (macroTexture == null)
            throw new InvalidOperationException("O Unity não conseguiu reabrir a textura de macrobiomas.");

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(MacroBiomeLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer { name = "GlobalMap_MacroBiomes" };
            AssetDatabase.CreateAsset(layer, MacroBiomeLayerPath);
        }
        layer.name = "GlobalMap_MacroBiomes";
        layer.diffuseTexture = macroTexture;
        layer.normalMapTexture = null;
        layer.tileSize = new Vector2(world.worldSize.x, world.mapFootprintHeight);
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Scene PrepareSceneForBuild()
    {
        if (!File.Exists(ScenePath))
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Scene existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (GetSceneComponents<GlobalTerrainStreamer>(existing).Count != 1)
            throw new InvalidOperationException("A cena alvo já existe e não pertence a este construtor; nada foi sobrescrito.");

        GameObject[] roots = existing.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            UnityEngine.Object.DestroyImmediate(roots[i]);
        return existing;
    }

    private static void BuildSceneObjects(Scene scene, GlobalWorldDefinition world, TerrainData terrainData)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("A cena global não está carregada para montagem.");

        GameObject worldRoot = new GameObject("GlobalMap_World");
        GameObject terrainGroup = CreateChild("Terrain", worldRoot.transform);
        GameObject oceanGroup = CreateChild("Ocean", worldRoot.transform);
        GameObject environmentGroup = CreateChild("Environment", worldRoot.transform);
        GameObject streamingGroup = CreateChild("Streaming", worldRoot.transform);
        GameObject worldSystemsGroup = CreateChild("WorldSystems", worldRoot.transform);
        GameObject debugGroup = CreateChild("Debug", worldRoot.transform);
        GameObject spawnPointsGroup = CreateChild("SpawnPoints", worldRoot.transform);
        spawnPointsGroup.tag = "Untagged";

        Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
        // NavalPlacementResolver recognizes water from collider names. Keep this
        // land collider unambiguously terrestrial so its existing ship rules
        // can distinguish dry elevations from the Sea mesh beneath them.
        terrain.gameObject.name = "GlobalMap_BaseTerrain";
        terrain.transform.SetParent(terrainGroup.transform, false);
        terrain.transform.position = new Vector3(world.MapMinX, world.terrainBaseY, world.MapMinZ);
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 5f;
        terrain.basemapDistance = 50000f;
        terrain.treeDistance = 0f;
        terrain.detailObjectDistance = 0f;
        terrain.materialTemplate = Resources.Load<Material>("CodexCampaignTerrainURP");
        terrain.gameObject.layer = LayerMask.NameToLayer("Chao") >= 0 ? LayerMask.NameToLayer("Chao") : 0;
        terrain.gameObject.SetActive(false);

        Light sun = CreateSun(environmentGroup.transform);
        GameObject cameraObject = CreateCamera(worldRoot.transform);
        Camera worldCamera = cameraObject.GetComponent<Camera>();

        CreateSingleExistingOcean(oceanGroup.transform, world, sun);

        GameObject streamerObject = new GameObject("GlobalTerrainStreamer");
        streamerObject.transform.SetParent(streamingGroup.transform, false);
        GlobalTerrainStreamer streamer = streamerObject.AddComponent<GlobalTerrainStreamer>();
        streamer.world = world;
        streamer.globalTerrain = terrain;
        streamer.cameraOverride = worldCamera;
        streamer.detailTerrainBelowAltitude = 20000f;
        streamer.tileRadius = 1;
        streamer.refreshInterval = 0.5f;
        streamer.tilesCreatedPerFrame = 1;

        GameObject mapControllerObject = CreateChild("MapaGeralController", worldSystemsGroup.transform);
        mapControllerObject.AddComponent<MapaGeralController>();
        mapControllerObject.AddComponent<GerenteSelecao>();

        GameObject gameManagerObject = CreateChild("GerenteDeJogo", worldSystemsGroup.transform);
        gameManagerObject.AddComponent<GerenteDeJogo>();

        // GerenciadorRecursos persists itself at runtime, so keep it as a scene
        // root instead of parenting it under GlobalMap_World (which would also
        // persist the entire world hierarchy through DontDestroyOnLoad).
        GameObject resourceManagerObject = new GameObject("GerenciadorRecursos");
        SceneManager.MoveGameObjectToScene(resourceManagerObject, scene);
        resourceManagerObject.AddComponent<GerenciadorRecursos>();

        GameObject diagnosticsObject = new GameObject("GlobalWorldDiagnostics");
        diagnosticsObject.transform.SetParent(debugGroup.transform, false);
        GlobalWorldDiagnostics diagnostics = diagnosticsObject.AddComponent<GlobalWorldDiagnostics>();
        diagnostics.streamer = streamer;
        diagnostics.visible = true;

        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.58f, 0.66f);
        RenderSettings.ambientEquatorColor = new Color(0.31f, 0.36f, 0.41f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.20f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.sun = sun;
    }

    private static Light CreateSun(Transform parent)
    {
        GameObject sunObject = new GameObject("Sun", typeof(Light));
        sunObject.transform.SetParent(parent, false);
        sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light sun = sunObject.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.84f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        return sun;
    }

    private static GameObject CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraController));
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(0f, 250000f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.nearClipPlane = 1f;
        camera.farClipPlane = 600000f;
        camera.fieldOfView = 75f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        CameraController controller = cameraObject.GetComponent<CameraController>();
        controller.velocidade = 20f;
        controller.velocidadeZoom = 4000f;
        controller.alturaMaximaZoom = 280000f;
        controller.alturaReferenciaEscalaControles = 8000f;
        controller.campoDeVisaoBase = 75f;
        controller.campoDeVisaoMin = 65f;
        controller.campoDeVisaoMax = 85f;
        controller.alturaMinParaFov = 2f;
        controller.alturaMaxParaFov = 280000f;
        controller.distanciaMinimaRender = 2500f;
        controller.distanciaMaximaRender = 600000f;
        controller.multiplicadorDistanciaRender = 3f;
        return cameraObject;
    }

    private static void CreateSingleExistingOcean(Transform parent, GlobalWorldDefinition world, Light sun)
    {
        GameObject seaPrefab = LoadRequired<GameObject>(SeaPrefabPath);
        Material seaSurface = LoadRequired<Material>(SeaSurfaceMaterialPath);
        Material seaSimulation = LoadRequired<Material>(SeaSimulationMaterialPath);
        GameObject sea = PrefabUtility.InstantiatePrefab(seaPrefab, parent) as GameObject;
        if (sea == null)
            throw new InvalidOperationException("Não foi possível instanciar o Sea.prefab existente.");

        sea.name = "Agua";
        MeshFilter meshFilter = sea.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (mesh == null || mesh.bounds.size.x <= 0f || mesh.bounds.size.z <= 0f)
            throw new InvalidOperationException("Sea.prefab não possui uma malha de superfície válida.");

        // Align the existing Sea mesh to the same mean sea level sampled by
        // OceanAdvanced.GetWaterHeight; its established waves stay unchanged.
        sea.transform.position = Vector3.zero;
        sea.transform.localScale = new Vector3(
            world.worldSize.x / mesh.bounds.size.x * 1.002f,
            1f,
            world.worldSize.x / mesh.bounds.size.z * 1.002f);
        Vector3 seaMeshCenter = sea.transform.TransformPoint(mesh.bounds.center);
        sea.transform.position += Vector3.up * (world.seaLevel - seaMeshCenter.y);

        MeshRenderer renderer = sea.GetComponent<MeshRenderer>();
        if (renderer == null)
            throw new InvalidOperationException("Sea.prefab não possui MeshRenderer.");
        renderer.sharedMaterial = seaSurface;

        MeshCollider meshCollider = sea.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = sea.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;

        MarcadorSuperficieMapa seaMarker = sea.GetComponent<MarcadorSuperficieMapa>();
        if (seaMarker != null)
            UnityEngine.Object.DestroyImmediate(seaMarker);

        OceanAdvanced ocean = sea.GetComponent<OceanAdvanced>();
        if (ocean == null)
            ocean = sea.AddComponent<OceanAdvanced>();
        ocean.ocean = seaSimulation;
        ocean.sun = sun;

        Unity.AI.Navigation.NavMeshModifier modifier = sea.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
        if (modifier == null)
            modifier = sea.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = UnityEngine.AI.NavMesh.GetAreaFromName("Agua");
        modifier.overrideGenerateLinks = false;
        modifier.generateLinks = false;
        modifier.ignoreFromBuild = false;
        modifier.applyToChildren = true;
    }

    private static void ValidateReferenceMask(GlobalWorldDefinition world)
    {
        Vector2[] landSamples =
        {
            PixelToWorld(world, 690f, 278f),
            PixelToWorld(world, 145f, 85f),
            PixelToWorld(world, 1015f, 140f),
            PixelToWorld(world, 300f, 520f),
            PixelToWorld(world, 1000f, 535f)
        };
        for (int i = 0; i < landSamples.Length; i++)
        {
            if (!world.IsLandAtWorld(landSamples[i].x, landSamples[i].y))
                throw new InvalidOperationException("A máscara da fase.png perdeu uma das cinco massas de terra de referência (amostra " + i + ").");
        }

        Vector2 oceanSample = PixelToWorld(world, 576f, 324f);
        if (world.IsLandAtWorld(oceanSample.x, oceanSample.y))
            throw new InvalidOperationException("A máscara da fase.png converteu a amostra central do oceano em terra.");
        if (Mathf.Abs(world.HeightAtWorld(oceanSample.x, oceanSample.y) - (world.seaLevel - 175f)) > 0.01f)
            throw new InvalidOperationException("O fundo oceânico não está referenciado ao nível médio do mar.");
    }

    private static void ValidateTreePrefabs(GlobalWorldDefinition world)
    {
        if (world.treePrefabs == null || world.treePrefabs.Length == 0)
            throw new InvalidOperationException("O Terrain global precisa de ao menos um prefab de vegetação.");

        for (int i = 0; i < world.treePrefabs.Length; i++)
        {
            GameObject prefab = world.treePrefabs[i];
            LODGroup lodGroup = prefab != null ? prefab.GetComponent<LODGroup>() : null;
            LOD[] lods = lodGroup != null ? lodGroup.GetLODs() : Array.Empty<LOD>();
            bool validLods = lods.Length >= 2;
            for (int lodIndex = 0; validLods && lodIndex < lods.Length; lodIndex++)
            {
                bool hasRenderer = false;
                foreach (Renderer renderer in lods[lodIndex].renderers)
                    hasRenderer |= renderer != null && renderer.enabled;
                validLods &= hasRenderer;
            }

            if (!validLods)
            {
                string prefabName = prefab != null ? prefab.name : "<ausente>";
                throw new InvalidOperationException(
                    "O prefab de árvore precisa fornecer malhas visíveis nos LODs próximos e distantes do Terrain: "
                    + prefabName);
            }
        }
    }

    private static Vector2 PixelToWorld(GlobalWorldDefinition world, float xFromLeft, float yFromTop)
    {
        float u = xFromLeft / (world.authorityMap.width - 1f);
        float v = (world.authorityMap.height - 1f - yFromTop) / (world.authorityMap.height - 1f);
        return new Vector2(Mathf.Lerp(world.MapMinX, world.MapMaxX, u), Mathf.Lerp(world.MapMinZ, world.MapMaxZ, v));
    }

    private static void ImportReference(string path, bool mask)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("Imagem de referência oficial ausente: " + path, path);

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = !mask;
        importer.filterMode = mask ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            throw new DirectoryNotFoundException("Pasta pai não existe no AssetDatabase: " + parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TerrainLayer CreateGlobalTerrainLayer(
        string sourcePath,
        string destinationPath,
        string layerName,
        Vector2 tileSize)
    {
        TerrainLayer source = LoadRequired<TerrainLayer>(sourcePath);
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(destinationPath);
        if (layer == null)
        {
            layer = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(layer, destinationPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, layer);
        }

        layer.name = layerName;
        layer.tileSize = tileSize;
        if (layerName == "GlobalMap_Forest")
        {
            // Regional alpha masks break the 512 m repeat at medium range;
            // detailed albedo and tangent normals support close forest ground.
            layer.diffuseTexture = CreateForestGroundTexture();
            layer.normalMapTexture = CreateForestGroundNormalTexture();
            layer.normalScale = 0.65f;
        }
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Texture2D CreateForestGroundTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestGroundTexturePath);
        if (existing != null)
            return existing;

        const int resolution = 1024;
        const float tileWorldSize = 512f;
        Color32[] pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            float wz = y / (float)resolution * tileWorldSize;
            for (int x = 0; x < resolution; x++)
            {
                float wx = x / (float)resolution * tileWorldSize;
                float broad = 0.62f * Mathf.PerlinNoise(wx / 38f + 17.3f, wz / 38f - 9.1f)
                    + 0.38f * Mathf.PerlinNoise(wx / 17f - 4.8f, wz / 17f + 22.6f);
                float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.37f, 0.69f, broad));
                float detail = 0.60f * Mathf.PerlinNoise(wx / 4.5f + 8.6f, wz / 4.5f - 12.2f)
                    + 0.40f * Mathf.PerlinNoise(wx / 1.3f - 24.8f, wz / 1.3f + 5.7f);

                Color soil = new Color(0.235f, 0.205f, 0.125f);
                Color olive = new Color(0.145f, 0.215f, 0.095f);
                Color deepCanopy = new Color(0.075f, 0.135f, 0.060f);
                Color color = Color.Lerp(soil, olive, 0.28f + coverage * 0.57f);
                color = Color.Lerp(color, deepCanopy, Mathf.Clamp01((broad - 0.57f) * 0.72f));
                color *= Mathf.Lerp(0.86f, 1.12f, detail);
                pixels[y * resolution + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                    255);
            }
        }

        Texture2D generated = new Texture2D(resolution, resolution, TextureFormat.RGB24, true, false)
        {
            name = "GlobalMap_ForestGround_Generated"
        };
        generated.SetPixels32(pixels);
        generated.Apply(true, false);
        File.WriteAllBytes(ForestGroundTexturePath, generated.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(generated);
        AssetDatabase.ImportAsset(ForestGroundTexturePath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(ForestGroundTexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("A textura procedural de solo florestal não foi importada.");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = resolution;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestGroundTexturePath);
        if (result == null)
            throw new InvalidOperationException("O Unity não conseguiu reabrir a textura procedural de solo florestal.");
        return result;
    }

    private static Texture2D CreateForestGroundNormalTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestGroundNormalPath);
        if (existing != null)
            return existing;

        const int resolution = 1024;
        const float tileWorldSize = 512f;
        const float tau = Mathf.PI * 2f;
        Color32[] pixels = new Color32[resolution * resolution];
        float texel = 1f / resolution;
        for (int y = 0; y < resolution; y++)
        {
            float v = y / (float)resolution;
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)resolution;
                float left = ForestGroundHeight(u - texel, v);
                float right = ForestGroundHeight(u + texel, v);
                float down = ForestGroundHeight(u, v - texel);
                float up = ForestGroundHeight(u, v + texel);
                float slopeScale = 1f / (2f * texel * tileWorldSize);
                Vector3 normal = new Vector3(
                    -(right - left) * slopeScale,
                    -(up - down) * slopeScale,
                    1f).normalized;
                pixels[y * resolution + x] = new Color32(
                    (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f),
                    255);
            }
        }

        Texture2D generated = new Texture2D(resolution, resolution, TextureFormat.RGB24, true, true)
        {
            name = "GlobalMap_ForestGround_Normal_Generated"
        };
        generated.SetPixels32(pixels);
        generated.Apply(true, false);
        File.WriteAllBytes(ForestGroundNormalPath, generated.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(generated);
        AssetDatabase.ImportAsset(ForestGroundNormalPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(ForestGroundNormalPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("A textura normal procedural do solo florestal não foi importada.");
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = resolution;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestGroundNormalPath);
        if (result == null)
            throw new InvalidOperationException("O Unity não conseguiu reabrir a normal procedural do solo florestal.");
        return result;
    }

    private static float ForestGroundHeight(float u, float v)
    {
        const float tau = Mathf.PI * 2f;
        float warpU = Mathf.Sin(tau * (3f * u + 2f * v + 0.15f * Mathf.Sin(tau * 2f * v)));
        float warpV = Mathf.Cos(tau * (2f * u - 3f * v + 0.13f * Mathf.Sin(tau * 3f * u)));
        return 0.46f * Mathf.Sin(tau * (11f * u + 9f * v + 0.24f * warpU))
            + 0.32f * Mathf.Cos(tau * (31f * u - 27f * v + 0.18f * warpV))
            + 0.15f * Mathf.Sin(tau * (83f * u + 71f * v))
            + 0.07f * Mathf.Cos(tau * (149f * u - 127f * v));
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new FileNotFoundException("Asset obrigatório não encontrado: " + path, path);
        return asset;
    }

    private static List<T> GetSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            components.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return components;
    }
}
