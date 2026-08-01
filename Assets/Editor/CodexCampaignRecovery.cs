using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.BrainMaster;

public static class CodexCampaignRecovery
{
    private const string CampaignPath = "Assets/Scenes/cena19).unity";
    private const string SafeBasePath = "Assets/Scenes/cena19 Base Segura.unity";
    private const string RecoverySourcePath = "Assets/_Recovery/cena19).unity";
    private const string FinalOutputPath = "C:/Users/Mathe/Desktop/Hegemonia Global Build Final/Hegemonia Global.exe";
    private const string CampaignTerrainMaterialPath = "Assets/Resources/CodexCampaignTerrainURP.mat";

    [MenuItem("Hegemonia/Codex/1. Criar campanha limpa")]
    public static void CreateCleanCampaignScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignPath) != null)
        {
            Debug.Log("[Codex] Cena limpa ja existe: " + CampaignPath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject("CampanhaPrincipal");
        root.AddComponent<CampanhaRecuperadaBootstrap>();

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 14f, -18f);
        cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.09f, 0.13f);

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        EditorSceneManager.SaveScene(scene, CampaignPath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Menu cena.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MenuPrincipal.unity", true),
            new EditorBuildSettingsScene(CampaignPath, true),
            new EditorBuildSettingsScene("Assets/_Recovery/teste.unity", true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[Codex] Cena de campanha limpa criada e configurada.");
    }

    public static void BuildRecoveryValidation()
    {
        const string output = "C:/Users/Mathe/Desktop/Hegemonia Global Recuperada Teste/Hegemonia Global.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("A build de validacao falhou: " + report.summary.result);
        }
        Debug.Log("[Codex] Build de validacao concluida: " + report.summary.totalSize + " bytes.");
    }

    [MenuItem("Hegemonia/Codex/3. Preparar e gerar build final limpa")]
    public static void PrepareAndBuildFinalClean()
    {
        StabilizeCampaignStartup();

        Scene scene = EditorSceneManager.GetSceneByPath(CampaignPath);
        ConfigureCampaignForRelease(scene);
        EnsureMainCampaignCamera(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, CampaignPath);

        Directory.CreateDirectory(Path.GetDirectoryName(FinalOutputPath));
        string[] scenes = EditorBuildSettings.scenes
            .Where(sceneEntry => sceneEntry.enabled)
            .Select(sceneEntry => sceneEntry.path)
            .ToArray();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = FinalOutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("A build final limpa falhou: " + report.summary.result);
        }

        Debug.Log("[Codex] Build final limpa concluida: " + report.summary.totalSize + " bytes.");
    }

    public static void RebuildAndPrepareFinalClean()
    {
        RebuildCampaignFromRecovery();
        PrepareAndBuildFinalClean();
    }

    public static void RebuildCampaignFromRecovery()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignPath) == null)
        {
            throw new System.Exception("Cena-base segura nao encontrada: " + CampaignPath);
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RecoverySourcePath) == null)
        {
            throw new System.Exception("Cena de recuperacao nao encontrada: " + RecoverySourcePath);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SafeBasePath) == null && !AssetDatabase.CopyAsset(CampaignPath, SafeBasePath))
        {
            throw new System.Exception("Nao foi possivel preservar a cena-base segura.");
        }

        Scene target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene source = EditorSceneManager.OpenScene(RecoverySourcePath, OpenSceneMode.Additive);
        GameObject[] roots = source.GetRootGameObjects();
        int copiedRoots = 0;
        foreach (GameObject root in roots)
        {
            if (root == null)
            {
                continue;
            }
            GameObject clone = Object.Instantiate(root);
            clone.name = root.name;
            SceneManager.MoveGameObjectToScene(clone, target);
            copiedRoots++;
        }

        foreach (IA_BrainMaster brain in Object.FindObjectsByType<IA_BrainMaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (brain.gameObject.scene != target)
            {
                continue;
            }
            brain.MaxCommandsPerFrame = 1;
            brain.BootstrapMobilizationSeconds = Mathf.Max(brain.BootstrapMobilizationSeconds, 150f);
            brain.EnableVerboseLogs = false;
            brain.EnableBootstrapConsoleTrace = false;
        }

        EditorSceneManager.MarkSceneDirty(target);
        if (!EditorSceneManager.SaveScene(target, CampaignPath))
        {
            throw new System.Exception("Nao foi possivel gravar a campanha reconstruida.");
        }
        EditorSceneManager.CloseScene(source, true);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Menu cena.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MenuPrincipal.unity", true),
            new EditorBuildSettingsScene(CampaignPath, true),
            new EditorBuildSettingsScene("Assets/_Recovery/teste.unity", true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[Codex] Campanha reconstruida: " + copiedRoots + " objetos-raiz; BrainMaster limitado a 1 comando por frame.");
    }

    public static void StabilizeCampaignStartup()
    {
        Scene scene = EditorSceneManager.OpenScene(CampaignPath, OpenSceneMode.Single);
        int removedMissing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null)
                {
                    removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
            }
        }

        int removedAi = 0;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }

            string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
            if (typeName.Contains("IA_BrainMaster")
                || typeName.Contains("IA_Deusa")
                || typeName.Contains("AISovereign"))
            {
                Object.DestroyImmediate(behaviour, true);
                removedAi++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, CampaignPath);
        Debug.Log("[Codex] Startup estabilizado: scripts ausentes removidos=" + removedMissing + ", componentes BrainMaster/IA removidos=" + removedAi + ".");
    }

    private static void EnsureMainCampaignCamera(Scene scene)
    {
        if (!scene.IsValid())
        {
            throw new System.Exception("Cena de campanha invalida ao preparar a build final.");
        }

        GameObject cameraObject = GameObject.Find("Main Camera");
        if (cameraObject == null || cameraObject.scene != scene)
        {
            throw new System.Exception("Main Camera nao encontrada na cena de campanha.");
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
        {
            throw new System.Exception("Main Camera nao possui componente Camera.");
        }

        cameraObject.tag = "MainCamera";
        camera.enabled = true;
    }

    private static void ConfigureCampaignForRelease(Scene scene)
    {
        if (!scene.IsValid())
        {
            throw new System.Exception("Cena de campanha invalida ao configurar a release.");
        }

        GameObject menusUi = EncontrarObjetoNaCena(scene, "menus ui");
        if (menusUi != null)
        {
            menusUi.SetActive(true);
        }

        // O objeto pai vinha desativado no backup, portanto os dois
        // IA01Controller e o IA01Manager pareciam existir no Inspector, mas
        // nunca recebiam Update na build.
        GameObject ia01Root = EncontrarObjetoNaCena(scene, "ia01");
        if (ia01Root != null)
        {
            ia01Root.SetActive(true);
        }

        GameObject menuFixo = EncontrarObjetoNaCena(scene, "menufixo");
        if (menuFixo != null)
        {
            menuFixo.SetActive(false);
        }

        GameObject interfaceMenu = EncontrarObjetoNaCena(scene, "Interface");
        if (interfaceMenu != null)
        {
            // Interface e o container dos menus reais de gameplay. O menu
            // inicial ja e desativado pelo MenuInicialController ao entrar na
            // campanha; desativar o container inteiro remove C/X/Z/V/B/N,
            // governo, satelite e construcao.
            interfaceMenu.SetActive(true);
            RectTransform interfaceRect = interfaceMenu.GetComponent<RectTransform>();
            if (interfaceRect != null)
            {
                interfaceRect.localScale = Vector3.one;
            }

            MenuInicialController menuInicial = interfaceMenu.GetComponent<MenuInicialController>();
            if (menuInicial != null)
            {
                menuInicial.enabled = false;
            }
        }

        MapaGeralController[] mapas = Object.FindObjectsByType<MapaGeralController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(mapa => mapa != null && mapa.gameObject.scene == scene)
            .ToArray();
        MapaGeralController mapaPrincipal = mapas.FirstOrDefault();
        if (mapaPrincipal == null)
        {
            mapaPrincipal = new GameObject("MapaGeralRelease").AddComponent<MapaGeralController>();
            SceneManager.MoveGameObjectToScene(mapaPrincipal.gameObject, scene);
        }
        else
        {
            mapaPrincipal.transform.SetParent(null, true);
        }

        mapaPrincipal.gameObject.name = "MapaGeralRelease";
        mapaPrincipal.gameObject.SetActive(true);
        mapaPrincipal.enabled = true;
        mapaPrincipal.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        for (int i = 0; i < mapas.Length; i++)
        {
            if (mapas[i] != null && mapas[i] != mapaPrincipal)
            {
                mapas[i].enabled = false;
            }
        }

        foreach (DiagnosticoDesempenhoJogo diagnostico in Object.FindObjectsByType<DiagnosticoDesempenhoJogo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (diagnostico != null && diagnostico.gameObject.scene == scene)
            {
                diagnostico.gameObject.SetActive(false);
            }
        }

        NormalizarTerrenosDaCampanha(scene);

        GameObject cameraObject = EncontrarObjetoNaCena(scene, "Main Camera");
        if (cameraObject != null)
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            // Usa o enquadramento recuperado sobre a base terrestre. O ponto
            // antigo caia no porto e fazia a campanha parecer sem chao.
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-53f, 116f, -383.5f),
                Quaternion.Euler(8.3f, 447.79f, 0f));
            if (camera != null)
            {
                camera.enabled = true;
                camera.fieldOfView = 75f;
            }

            CameraController controller = cameraObject.GetComponent<CameraController>();
            if (controller != null)
            {
                controller.campoDeVisaoBase = 75f;
                controller.campoDeVisaoMin = 65f;
                controller.campoDeVisaoMax = 85f;
            }
        }
    }

    private static void NormalizarTerrenosDaCampanha(Scene scene)
    {
        Terrain[] terrenos = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(terrain => terrain != null && terrain.gameObject.scene == scene)
            .ToArray();

        Debug.Log("[Codex] TERRAIN_PIPELINE_MARKER_20260730");
        Material materialTerreno = ObterMaterialTerrenoCampanha();

        int corrigidos = 0;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terrain = terrenos[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            // A cena recuperada trazia templates de material ausentes ou
            // pertencentes a outros prefabs. O template nulo faz o Unity
            // usar o shader padrão de Terrain e preserva as TerrainLayers.
            if (materialTerreno != null)
            {
                terrain.materialType = Terrain.MaterialType.Custom;
                terrain.materialTemplate = materialTerreno;
            }
            else
            {
                terrain.materialType = Terrain.MaterialType.BuiltInStandard;
                terrain.materialTemplate = null;
            }
            terrain.drawInstanced = false;
            terrain.enabled = true;
            terrain.gameObject.SetActive(true);
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
            corrigidos++;
        }

        Debug.Log("[Codex] Terrenos normalizados para a campanha: " + corrigidos);
    }

    private static Material ObterMaterialTerrenoCampanha()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null)
        {
            Debug.LogError("[Codex] Shader URP Terrain/Lit nao encontrado.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CampaignTerrainMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "CodexCampaignTerrainURP"
            };
            AssetDatabase.CreateAsset(material, CampaignTerrainMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Codex] Material do terreno: " + material.shader.name + " em " + CampaignTerrainMaterialPath);
        return material;
    }

    private static GameObject EncontrarObjetoNaCena(Scene scene, string nome)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != null && transform.name == nome)
                {
                    return transform.gameObject;
                }
            }
        }
        return null;
    }
}
