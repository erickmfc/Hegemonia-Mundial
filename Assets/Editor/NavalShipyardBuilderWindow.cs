using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit Edit Mode workflow for the generated naval shipyard visuals.
/// It only replaces the reserved MilitaryNavalShipyard child on the existing
/// gameplay prefab; its root, scripts, anchors and other children are preserved.
/// </summary>
public sealed class NavalShipyardBuilderWindow : EditorWindow
{
    private const string PrefabPath = "Assets/Prefabs/Estaleiro Marinho/Estaleiros navais.prefab";
    private const string ModelPath = "Assets/Game/Environment/NavalShipyard/Models/NavalShipyard_Blockout.fbx";
    private const string PrefabRootName = "MilitaryNavalShipyard";
    private const string PrefabsRoot = "Assets/Game/Environment/NavalShipyard/Prefabs";
    private const string MaterialsRoot = "Assets/Game/Environment/NavalShipyard/Materials";
    private const string TexturesRoot = "Assets/Game/Environment/NavalShipyard/Textures";
    private const string MeshesRoot = "Assets/Game/Environment/NavalShipyard/Meshes";
    private const string LodCubeMeshPath = MeshesRoot + "/NavalShipyard_LOD_Cube.asset";
    private const string ScenesRoot = "Assets/Game/Environment/NavalShipyard/Scenes";
    private const string PreviewScenePath = ScenesRoot + "/NavalShipyard_Preview.unity";
    private const string PreviewPreferenceKey = "Hegemonia.NavShipyardPreviewScene";
    private const string GeneratedAssetLabel = "NavalShipyardGenerated";

    private static readonly Dictionary<string, string> CategoryFolders = new Dictionary<string, string>
    {
        { "MainBuilding", "Buildings" }, { "Maintenance", "Buildings" }, { "Workshop", "Buildings" },
        { "Admin", "Buildings" }, { "Storage", "Buildings" }, { "Crane", "Cranes" },
        { "Dock", "Dock" }, { "Pier", "Piers" }, { "Pipe", "Utilities" },
        { "Utility", "Utilities" }, { "Light", "Utilities" }, { "Security", "Security" },
        { "Ground", "Props" }, { "Road", "Props" }, { "Detail", "Props" }, { "Environment", "Props" }
    };

    private static readonly Dictionary<string, Color> MaterialColors = new Dictionary<string, Color>
    {
        { "MAT_Concrete_Naval", new Color(0.62f, 0.63f, 0.60f) },
        { "MAT_Steel_Grey", new Color(0.27f, 0.31f, 0.33f) },
        { "MAT_GalvanizedMetal", new Color(0.55f, 0.62f, 0.64f) },
        { "MAT_RoofMetal", new Color(0.71f, 0.73f, 0.73f) },
        { "MAT_SafetyYellow", new Color(1.0f, 0.74f, 0.06f) },
        { "MAT_Asphalt", new Color(0.10f, 0.11f, 0.12f) },
        { "MAT_DarkSteel", new Color(0.055f, 0.075f, 0.088f) },
        { "MAT_Glass", new Color(0.12f, 0.30f, 0.38f) },
        { "MAT_Water", new Color(0.025f, 0.16f, 0.21f) },
        { "MAT_Concrete_Light", new Color(0.73f, 0.74f, 0.70f) },
        { "MAT_Green", new Color(0.25f, 0.33f, 0.18f) },
        { "MAT_Foliage_Olive", new Color(0.31f, 0.40f, 0.16f) },
        { "MAT_White_Marking", new Color(0.82f, 0.84f, 0.80f) },
        { "MAT_Red_Safety", new Color(0.58f, 0.09f, 0.06f) }
    };

    private Vector2 scroll;
    private string status = "Pronto. A cena de teste fica isolada do Build Settings.";

    [MenuItem("Tools/Hegemonia Global/Naval Shipyard Builder")]
    public static void OpenWindow()
    {
        NavalShipyardBuilderWindow window = GetWindow<NavalShipyardBuilderWindow>("Naval Shipyard Builder");
        window.minSize = new Vector2(380, 440);
        window.Show();
    }

    // Can be invoked by Unity's -executeMethod for an isolated Edit Mode build.
    public static void BuildAndValidateFromCommandLine()
    {
        try
        {
            int modules = CreateModularPrefabs();
            AttachGeneratedPrefab();
            EnsurePreviewSceneExists();
            AssetDatabase.SaveAssets();
            ValidateLayout();
            Debug.Log("[Naval Shipyard Builder] Batch build completed with " + modules + " modular prefabs.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Estaleiro Naval Militar", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Estaleiro modular texturizado em escala real, ligado ao prefab de gameplay existente.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Construção em Edit Mode", EditorStyles.boldLabel);
            if (GUILayout.Button("Build Shipyard", GUILayout.Height(30))) Run(BuildShipyard);
            if (GUILayout.Button("Clear Generated Shipyard", GUILayout.Height(25))) Run(ClearGeneratedShipyard);
            if (GUILayout.Button("Rebuild Shipyard", GUILayout.Height(25))) Run(RebuildShipyard);
            if (GUILayout.Button("Create Prefabs", GUILayout.Height(25))) Run(() => { CreateModularPrefabs(); });
            if (GUILayout.Button("Validate Layout", GUILayout.Height(25))) Run(ValidateLayout);
        }

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Inspeção isolada", EditorStyles.boldLabel);
            if (GUILayout.Button("Create / Refresh Preview Scene", GUILayout.Height(25))) Run(CreatePreviewScene);
            if (GUILayout.Button("Play Preview", GUILayout.Height(25))) Run(PlayPreview);
            EditorGUILayout.HelpBox("Play Preview abre somente a cena NavalShipyard_Preview; ela não é adicionada ao Build Settings.", MessageType.Info);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            status = exception.Message;
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Naval Shipyard Builder", exception.Message, "OK");
        }
        Repaint();
    }

    private static void BuildShipyard()
    {
        int modules = CreateModularPrefabs();
        AttachGeneratedPrefab();
        EnsurePreviewSceneExists();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogStatus("Estaleiro construído em Edit Mode. Prefabs modulares: " + modules + ".");
    }

    private static void RebuildShipyard()
    {
        int modules = CreateModularPrefabs();
        AttachGeneratedPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogStatus("Estaleiro reconstruído. Prefabs modulares: " + modules + ". A cena de teste existente continua isolada.");
    }

    private static void ClearGeneratedShipyard()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform generated = contents.transform.Find(PrefabRootName);
            if (generated != null)
            {
                DestroyImmediate(generated.gameObject);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        AssetDatabase.SaveAssets();
        LogStatus("Apenas o filho gerado MilitaryNavalShipyard foi removido do prefab de gameplay.");
    }

    private static void AttachGeneratedPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform existing = contents.transform.Find(PrefabRootName);
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            Transform generated = new GameObject(PrefabRootName).transform;
            generated.SetParent(contents.transform, false);
            generated.localPosition = Vector3.zero;
            generated.localRotation = Quaternion.identity;

            // The legacy gameplay root is deliberately non-uniformly scaled.
            // Cancel only scale here; retaining the parent's rotation keeps the
            // metric geometry free of shear while preserving the yard heading.
            Vector3 parentScale = contents.transform.localScale;
            generated.localScale = new Vector3(
                InverseNonZero(parentScale.x),
                InverseNonZero(parentScale.y),
                InverseNonZero(parentScale.z));

            int attached = 0;
            Dictionary<string, Transform> hierarchyGroups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (string path in GetGeneratedPrefabPaths().OrderBy(item => item, StringComparer.Ordinal))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new InvalidOperationException("Prefab modular inválido: " + path);
                string groupName = GetHierarchyGroupName(prefab.name);
                Transform group;
                if (!hierarchyGroups.TryGetValue(groupName, out group))
                {
                    group = new GameObject(groupName).transform;
                    group.SetParent(generated, false);
                    hierarchyGroups.Add(groupName, group);
                }
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, contents.scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Não foi possível instanciar o prefab modular: " + path);
                instance.name = prefab.name;
                instance.transform.SetParent(group, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                attached++;
            }
            if (attached == 0)
                throw new InvalidOperationException("Nenhum prefab modular foi criado para anexar ao estaleiro.");

            GameObjectUtility.SetStaticEditorFlags(generated.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            bool success;
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out success);
            if (!success)
                throw new InvalidOperationException("O prefab de gameplay não foi salvo.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static int CreateModularPrefabs()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            throw new FileNotFoundException("Prefab de gameplay do estaleiro não encontrado.", PrefabPath);

        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Environment");
        EnsureFolder("Assets/Game/Environment/NavalShipyard");
        EnsureFolder(PrefabsRoot);
        foreach (string folder in new[] { "Buildings", "Cranes", "Dock", "Piers", "Utilities", "Props", "Security" })
            EnsureFolder(PrefabsRoot + "/" + folder);
        EnsureFolder(MaterialsRoot);
        EnsureFolder(MeshesRoot);
        EnsureFolder(ScenesRoot);
        AssetDatabase.Refresh();

        ConfigureModelImporter();
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelRoot == null)
            throw new InvalidOperationException("Unity não conseguiu importar NavalShipyard_Blockout.fbx.");

        Dictionary<string, Material> materialAssets = CreateMaterials();
        Dictionary<string, ModuleData> modules = new Dictionary<string, ModuleData>(StringComparer.Ordinal);
        MeshFilter[] filters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;
            MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
                continue;

            string[] nameParts = filter.gameObject.name.Split(new[] { "__" }, StringSplitOptions.None);
            if (nameParts.Length < 3)
                continue;
            string category = nameParts[0];
            string module = nameParts[1];
            string key = category + "__" + module;
            ModuleData data;
            if (!modules.TryGetValue(key, out data))
            {
                data = new ModuleData(category, module);
                modules.Add(key, data);
            }
            data.Parts.Add(new PartData(filter, sourceRenderer));
        }

        if (modules.Count < 20)
            throw new InvalidOperationException("A importação contém apenas " + modules.Count + " módulos reconhecidos; verifique os nomes do FBX.");

        foreach (ModuleData module in modules.Values)
            SaveModulePrefab(modelRoot.transform, module, materialAssets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogStatus("Criados/atualizados " + modules.Count + " prefabs por módulo; " + filters.Length + " meshes separados no FBX.");
        return modules.Count;
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return;
        importer.globalScale = 1f;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        Dictionary<string, Material> result = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Color> item in MaterialColors)
        {
            Texture2D albedo = LoadNavalTexture(TexturesRoot + "/" + item.Key + "_Albedo.png", false, true);
            Texture2D normal = LoadNavalTexture(TexturesRoot + "/" + item.Key + "_Normal.png", true, false);
            string path = MaterialsRoot + "/" + item.Key + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("Não foi encontrado shader Lit para criar os materiais do estaleiro.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = item.Key;
            material.color = Color.white; // The authored albedo already contains the sober naval colour.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            else material.mainTexture = albedo;
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            SetFloatIfPresent(material, "_BumpScale", normal != null ? 0.38f : 0f);
            SetFloatIfPresent(material, "_Metallic", MaterialMetallic(item.Key));
            SetFloatIfPresent(material, "_Smoothness", MaterialSmoothness(item.Key));
            EditorUtility.SetDirty(material);
            result.Add(item.Key, material);
        }
        return result;
    }

    private static Texture2D LoadNavalTexture(string path, bool isNormal, bool required)
    {
        if (!AssetPathExists(path))
        {
            if (required) throw new FileNotFoundException("Textura do estaleiro ausente.", path);
            return null;
        }
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null)
            throw new InvalidOperationException("Unity não reconheceu a textura: " + path);
        TextureImporterType type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool changed = importer.textureType != type || importer.wrapMode != TextureWrapMode.Repeat ||
            !importer.mipmapEnabled || importer.maxTextureSize != 512 || importer.anisoLevel != 2 ||
            importer.filterMode != FilterMode.Trilinear || importer.textureCompression != TextureImporterCompression.Compressed;
        if (changed)
        {
            importer.textureType = type;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.anisoLevel = 2;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new InvalidOperationException("Não foi possível carregar a textura: " + path);
        return texture;
    }

    private static float MaterialMetallic(string name)
    {
        if (name == "MAT_GalvanizedMetal" || name == "MAT_DarkSteel") return 0.55f;
        if (name == "MAT_Steel_Grey") return 0.42f;
        if (name == "MAT_RoofMetal") return 0.35f;
        if (name == "MAT_SafetyYellow") return 0.12f;
        if (name == "MAT_Water") return 0.05f;
        return 0f;
    }

    private static float MaterialSmoothness(string name)
    {
        if (name == "MAT_Water") return 0.72f;
        if (name == "MAT_Glass") return 0.68f;
        if (name == "MAT_GalvanizedMetal") return 0.54f;
        if (name == "MAT_DarkSteel") return 0.46f;
        if (name == "MAT_RoofMetal") return 0.38f;
        if (name == "MAT_Steel_Grey") return 0.36f;
        if (name == "MAT_SafetyYellow") return 0.33f;
        if (name == "MAT_Asphalt" || name == "MAT_Green") return 0.08f;
        return 0.18f;
    }

    private static void SaveModulePrefab(Transform modelRoot, ModuleData module, Dictionary<string, Material> materialAssets)
    {
        string outputFolder;
        if (!CategoryFolders.TryGetValue(module.Category, out outputFolder))
            outputFolder = "Props";
        string prefabName = module.Category == "Crane" && module.Name.StartsWith("ShipyardGantry_", StringComparison.Ordinal)
            ? "Shipyard_GantryCrane_" + module.Name.Substring("ShipyardGantry_".Length)
            : module.Category + "__" + module.Name;
        string path = PrefabsRoot + "/" + outputFolder + "/" + prefabName + ".prefab";

        GameObject root = new GameObject(prefabName);
        try
        {
            Transform gantryCrane = null;
            Dictionary<string, Transform> craneGroups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (module.Category == "Crane" && module.Name.StartsWith("ShipyardGantry_", StringComparison.Ordinal))
            {
                gantryCrane = new GameObject("GantryCrane").transform;
                gantryCrane.SetParent(root.transform, false);
            }

            foreach (PartData part in module.Parts)
            {
                string partName = GetPartName(part.Filter.gameObject.name, module);
                Transform partParent = root.transform;
                if (gantryCrane != null)
                {
                    string groupName = GetCranePartGroup(partName);
                    if (!craneGroups.TryGetValue(groupName, out partParent))
                    {
                        partParent = new GameObject(groupName).transform;
                        partParent.SetParent(gantryCrane, false);
                        craneGroups.Add(groupName, partParent);
                    }
                }
                GameObject child = new GameObject(partName);
                child.transform.SetParent(partParent, false);
                Matrix4x4 relative = modelRoot.worldToLocalMatrix * part.Filter.transform.localToWorldMatrix;
                child.transform.localPosition = relative.GetColumn(3);
                child.transform.localRotation = relative.rotation;
                child.transform.localScale = relative.lossyScale;

                MeshFilter meshFilter = child.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = part.Filter.sharedMesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                Material[] sourceMaterials = part.Renderer.sharedMaterials;
                if (sourceMaterials == null || sourceMaterials.Length == 0)
                    sourceMaterials = new Material[] { null };
                renderer.sharedMaterials = sourceMaterials.Select(source => ResolveMaterial(source, module, materialAssets)).ToArray();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(child,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            }

            AddColliders(root, module);
            if (ShouldHaveLod(module))
                AddSilhouetteLod(root);
            GameObjectUtility.SetStaticEditorFlags(root,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null)
                throw new InvalidOperationException("Falha ao salvar prefab modular: " + path);
            AssetDatabase.SetLabels(saved, new[] { GeneratedAssetLabel });
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private static Material ResolveMaterial(Material source, ModuleData module, Dictionary<string, Material> materialAssets)
    {
        if (source != null)
        {
            string materialName = source.name.Replace(" (Instance)", string.Empty);
            Material asset;
            if (materialAssets.TryGetValue(materialName, out asset))
                return asset;
        }

        string fallback = "MAT_Concrete_Naval";
        if (module.Category == "Crane" || module.Category == "Pipe" || module.Category == "Utility") fallback = "MAT_Steel_Grey";
        else if (module.Category == "Road") fallback = "MAT_Asphalt";
        else if (module.Category == "Environment" && module.Name == "HarborWater") fallback = "MAT_Water";
        else if (module.Category == "Environment") fallback = "MAT_Green";
        else if (module.Category == "Ground" || module.Category == "Dock" || module.Category == "Pier") fallback = "MAT_Concrete_Naval";
        return materialAssets[fallback];
    }

    private static void AddColliders(GameObject root, ModuleData module)
    {
        if (module.Category == "Environment" || module.Category == "Detail")
            return;

        if (module.Category == "Dock")
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                string part = child.name;
                bool solidDockPart = part.Contains("BasinFloor") || part.Contains("WestWall") || part.Contains("EastWall") ||
                                     part.Contains("FarWall") || part.Contains("Fender") || part.Contains("Bollard") ||
                                     part.Contains("CradleRail") || part.Contains("KeelBlock");
                if (!solidDockPart)
                    continue;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                BoxCollider collider = child.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            return;
        }

        if (module.Category == "Pier")
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                if (!child.name.Contains("Deck"))
                    continue;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                BoxCollider collider = child.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            return;
        }

        if (module.Category == "Crane")
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform ||
                    (!child.name.Contains("Leg_") && !child.name.Contains("LegBrace") &&
                     !child.name.Contains("Foot") && !child.name.Contains("RailWheel") &&
                     !child.name.Contains("GantryRail")))
                    continue;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                BoxCollider collider = child.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            return;
        }

        if (module.Category == "Ground")
        {
            // Keep the dry-dock cut-outs open by colliding only with individual concrete slabs.
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                BoxCollider collider = child.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        BoxCollider rootCollider = root.AddComponent<BoxCollider>();
        rootCollider.center = root.transform.InverseTransformPoint(bounds.center);
        rootCollider.size = bounds.size;
    }

    private static bool ShouldHaveLod(ModuleData module)
    {
        return (module.Category == "MainBuilding" && module.Name.StartsWith("MainHangar", StringComparison.Ordinal)) ||
               module.Category == "Maintenance" || module.Category == "Workshop" ||
               (module.Category == "Admin" && module.Name.StartsWith("Administration_Block", StringComparison.Ordinal)) ||
               (module.Category == "Storage" && module.Name.StartsWith("Warehouse", StringComparison.Ordinal));
    }

    private static void AddSilhouetteLod(GameObject root)
    {
        Renderer[] lod0 = root.GetComponentsInChildren<MeshRenderer>(true);
        if (lod0.Length == 0)
            return;
        Bounds bounds = root.transform.InverseTransformBounds(lod0[0].bounds);
        for (int i = 1; i < lod0.Length; i++)
            bounds.Encapsulate(root.transform.InverseTransformBounds(lod0[i].bounds));

        GameObject proxy = new GameObject("LOD1_Silhouette");
        proxy.transform.SetParent(root.transform, false);
        proxy.transform.localPosition = bounds.center;
        proxy.transform.localScale = bounds.size;
        MeshFilter filter = proxy.AddComponent<MeshFilter>();
        filter.sharedMesh = GetLodCubeMesh();
        MeshRenderer renderer = proxy.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = lod0[0].sharedMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(proxy,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

        LODGroup lodGroup = root.AddComponent<LODGroup>();
        lodGroup.fadeMode = LODFadeMode.CrossFade;
        lodGroup.SetLODs(new[]
        {
            new LOD(0.16f, lod0),
            new LOD(0.035f, new[] { renderer })
        });
        lodGroup.RecalculateBounds();
    }

    private static Mesh CreateUnitCubeMesh()
    {
        Mesh mesh = new Mesh { name = "NavalShipyard_LOD_Silhouette" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GetLodCubeMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LodCubeMeshPath);
        if (mesh != null)
            return mesh;
        mesh = CreateUnitCubeMesh();
        AssetDatabase.CreateAsset(mesh, LodCubeMeshPath);
        return mesh;
    }

    private static void CreatePreviewScene()
    {
        string path = GetUniquePreviewPath();
        CreatePreviewSceneAtPath(path);
        EditorPrefs.SetString(PreviewPreferenceKey, path);
        LogStatus("Cena de pré-visualização isolada criada em " + path + ".");
    }

    private static void EnsurePreviewSceneExists()
    {
        string configured = EditorPrefs.GetString(PreviewPreferenceKey, string.Empty);
        if (!string.IsNullOrEmpty(configured) && AssetPathExists(configured))
            return;
        string path = AssetPathExists(PreviewScenePath) ? GetUniquePreviewPath() : PreviewScenePath;
        CreatePreviewSceneAtPath(path);
        EditorPrefs.SetString(PreviewPreferenceKey, path);
    }

    private static string GetUniquePreviewPath()
    {
        if (!AssetPathExists(PreviewScenePath))
            return PreviewScenePath;
        string directory = Path.GetDirectoryName(PreviewScenePath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(PreviewScenePath);
        for (int i = 1; i < 100; i++)
        {
            string candidate = directory + "/" + name + "_" + i.ToString("00") + ".unity";
            if (!AssetPathExists(candidate))
                return candidate;
        }
        throw new IOException("Não há caminho disponível para uma nova cena de preview.");
    }

    private static void CreatePreviewSceneAtPath(string path)
    {
        EnsureFolder(ScenesRoot);
        GameObject shipyardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (shipyardPrefab == null || shipyardPrefab.transform.Find(PrefabRootName) == null)
            throw new InvalidOperationException("Execute Build Shipyard antes de criar a cena de pré-visualização.");

        Scene activeScene = SceneManager.GetActiveScene();
        bool replaceUntitledBatchScene = Application.isBatchMode && string.IsNullOrEmpty(activeScene.path);
        Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            replaceUntitledBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
        try
        {
            GameObject marker = new GameObject("NAVAL_SHIPYARD_GENERATED_PREVIEW_SCENE");
            SceneManager.MoveGameObjectToScene(marker, preview);

            GameObject instance = PrefabUtility.InstantiatePrefab(shipyardPrefab, preview) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Não foi possível carregar o prefab do estaleiro na cena de preview.");
            instance.name = shipyardPrefab.name;
            instance.transform.position = Vector3.zero;

            GameObject sunObject = new GameObject("Preview_Directional_Light");
            SceneManager.MoveGameObjectToScene(sunObject, preview);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sunObject.transform.rotation = Quaternion.Euler(35f, -32f, 0f);

            GameObject cameraObject = new GameObject("Preview_Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 425f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 2500f;
            camera.backgroundColor = new Color(0.08f, 0.15f, 0.19f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(620f, 510f, -660f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 6f, -25f) - cameraObject.transform.position, Vector3.up);

            EditorSceneManager.MarkSceneDirty(preview);
            if (!EditorSceneManager.SaveScene(preview, path))
                throw new InvalidOperationException("Não foi possível salvar a cena de pré-visualização: " + path);
        }
        finally
        {
            if (!replaceUntitledBatchScene)
            {
                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);
                EditorSceneManager.CloseScene(preview, true);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void PlayPreview()
    {
        EnsurePreviewSceneExists();
        string path = EditorPrefs.GetString(PreviewPreferenceKey, PreviewScenePath);
        if (!AssetPathExists(path))
            throw new FileNotFoundException("Cena de pré-visualização não encontrada. Crie-a novamente pelo Builder.", path);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        LogStatus("Play Mode iniciado na cena isolada " + path + ".");
    }

    private static void ValidateLayout()
    {
        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (modelRoot == null)
            throw new InvalidOperationException("FBX ausente ou não importado: " + ModelPath);
        if (prefab == null)
            throw new InvalidOperationException("Prefab de gameplay ausente: " + PrefabPath);

        MeshFilter[] meshes = modelRoot.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null).ToArray();
        MeshRenderer[] renderers = modelRoot.GetComponentsInChildren<MeshRenderer>(true);
        if (meshes.Length < 500)
            throw new InvalidOperationException("O FBX possui apenas " + meshes.Length + " meshes; esperava-se um estaleiro modular completo.");

        Bounds platform = default(Bounds);
        bool hasPlatform = false;
        foreach (MeshRenderer renderer in renderers)
        {
            if (!renderer.gameObject.name.StartsWith("Ground__ConcreteBase__", StringComparison.Ordinal))
                continue;
            if (!hasPlatform) { platform = renderer.bounds; hasPlatform = true; }
            else platform.Encapsulate(renderer.bounds);
        }
        if (!hasPlatform || Mathf.Abs(platform.size.x - 500f) > 1.5f || Mathf.Abs(platform.size.z - 340f) > 1.5f)
            throw new InvalidOperationException("A plataforma não mede aproximadamente 500 x 340 m (medidas lidas: " + platform.size + ").");

        Transform generated = prefab.transform.Find(PrefabRootName);
        if (generated == null)
            throw new InvalidOperationException("O filho gerado ainda não foi anexado ao prefab de gameplay.");
        string[] requiredGroups =
        {
            "Concrete_Base", "DryDock", "Main_Hangar", "Maintenance_Hangars", "Workshops",
            "Administration", "Cranes", "Piers", "Storage", "Utilities", "Pipes",
            "Lighting", "Security", "Roads", "Details", "Environment"
        };
        foreach (string groupName in requiredGroups)
            if (generated.Find(groupName) == null)
                throw new InvalidOperationException("Grupo modular ausente na hierarquia: " + groupName);

        GameObject westGantry = AssetDatabase.LoadAssetAtPath<GameObject>(
            PrefabsRoot + "/Cranes/Shipyard_GantryCrane_West_1.prefab");
        if (westGantry == null)
            throw new InvalidOperationException("Prefab Shipyard_GantryCrane_West_1 ausente.");
        Transform craneBody = westGantry.transform.Find("GantryCrane");
        if (craneBody == null)
            throw new InvalidOperationException("Hierarquia GantryCrane ausente no prefab do guindaste.");
        foreach (string cranePart in new[] { "LeftLeg", "RightLeg", "UpperBeam", "RailWheels", "OperatorCabin", "Trolley", "Cable", "Hook" })
            if (craneBody.Find(cranePart) == null)
                throw new InvalidOperationException("Parte do guindaste ausente: " + cranePart);
        Vector3 worldVisualScale = Vector3.Scale(prefab.transform.localScale, generated.localScale);
        if ((worldVisualScale - Vector3.one).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException("A escala compensadora do visual não está neutralizando a escala antiga do prefab: " + worldVisualScale);

        Estaleiro gameplay = prefab.GetComponent<Estaleiro>();
        if (gameplay == null || gameplay.slots == null || gameplay.slots.Length < 2)
            throw new InvalidOperationException("O componente Estaleiro e seus dois pontos de construção devem permanecer no prefab.");
        string[] anchors = gameplay.slots.Where(slot => slot != null && slot.pontoDeConstrucao != null).Select(slot => slot.pontoDeConstrucao.name).ToArray();
        if (!anchors.Contains("Atracagem") || !anchors.Contains("Atracagem_Grande") || gameplay.pontoDeSaida == null || gameplay.pontoDeSaida.name != "saida")
            throw new InvalidOperationException("Os anchors Atracagem, Atracagem_Grande e saida não foram preservados.");

        int modularPrefabs = GetGeneratedPrefabPaths().Count();
        if (modularPrefabs < 20)
            throw new InvalidOperationException("Foram encontrados apenas " + modularPrefabs + " prefabs modulares.");
        foreach (string name in MaterialColors.Keys)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/" + name + ".mat");
            if (material == null || (material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture) == null)
                throw new InvalidOperationException("Material de jogo sem textura: " + name);
        }
        foreach (string path in GetGeneratedPrefabPaths())
        {
            GameObject module = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (MeshRenderer renderer in module.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterials.Any(material => material == null))
                    throw new InvalidOperationException("Parte sem material no prefab modular: " + path + "/" + renderer.name);
        }
        LogStatus("Validação concluída: " + meshes.Length + " meshes; plataforma " + platform.size.x.ToString("F1") + " x " + platform.size.z.ToString("F1") + " m; " + modularPrefabs + " prefabs; " + MaterialColors.Count + " materiais texturizados; hierarquia, scripts e anchors preservados.");
    }

    private static IEnumerable<string> GetGeneratedPrefabPaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (path.StartsWith(PrefabsRoot + "/", StringComparison.Ordinal) &&
                prefab != null && AssetDatabase.GetLabels(prefab).Contains(GeneratedAssetLabel))
                yield return path;
        }
    }

    private static string GetHierarchyGroupName(string prefabName)
    {
        if (prefabName.StartsWith("Ground__", StringComparison.Ordinal)) return "Concrete_Base";
        if (prefabName.StartsWith("Dock__", StringComparison.Ordinal)) return "DryDock";
        if (prefabName.StartsWith("MainBuilding__", StringComparison.Ordinal)) return "Main_Hangar";
        if (prefabName.StartsWith("Maintenance__", StringComparison.Ordinal)) return "Maintenance_Hangars";
        if (prefabName.StartsWith("Workshop__", StringComparison.Ordinal)) return "Workshops";
        if (prefabName.StartsWith("Admin__", StringComparison.Ordinal)) return "Administration";
        if (prefabName.StartsWith("Crane__", StringComparison.Ordinal) ||
            prefabName.StartsWith("Shipyard_GantryCrane_", StringComparison.Ordinal)) return "Cranes";
        if (prefabName.StartsWith("Pier__", StringComparison.Ordinal)) return "Piers";
        if (prefabName.StartsWith("Storage__", StringComparison.Ordinal)) return "Storage";
        if (prefabName.StartsWith("Utility__", StringComparison.Ordinal)) return "Utilities";
        if (prefabName.StartsWith("Pipe__", StringComparison.Ordinal)) return "Pipes";
        if (prefabName.StartsWith("Light__", StringComparison.Ordinal)) return "Lighting";
        if (prefabName.StartsWith("Security__", StringComparison.Ordinal)) return "Security";
        if (prefabName.StartsWith("Road__", StringComparison.Ordinal)) return "Roads";
        if (prefabName.StartsWith("Detail__", StringComparison.Ordinal)) return "Details";
        if (prefabName.StartsWith("Environment__", StringComparison.Ordinal)) return "Environment";
        throw new InvalidOperationException("Não há grupo de hierarquia configurado para " + prefabName + ".");
    }

    private static string GetPartName(string objectName, ModuleData module)
    {
        string prefix = module.Category + "__" + module.Name + "__";
        return objectName.StartsWith(prefix, StringComparison.Ordinal)
            ? objectName.Substring(prefix.Length)
            : objectName;
    }

    private static string GetCranePartGroup(string partName)
    {
        if (partName.StartsWith("Leg_0_", StringComparison.Ordinal) ||
            partName.StartsWith("LegBrace_0_", StringComparison.Ordinal)) return "LeftLeg";
        if (partName.StartsWith("Leg_1_", StringComparison.Ordinal) ||
            partName.StartsWith("LegBrace_1_", StringComparison.Ordinal)) return "RightLeg";
        if (partName.StartsWith("UpperBeam", StringComparison.Ordinal) ||
            partName.StartsWith("BridgeGantry", StringComparison.Ordinal) ||
            partName.StartsWith("Truss_", StringComparison.Ordinal)) return "UpperBeam";
        if (partName.StartsWith("RailWheel", StringComparison.Ordinal)) return "RailWheels";
        if (partName.StartsWith("OperatorCabin", StringComparison.Ordinal) ||
            partName.StartsWith("CabinBase", StringComparison.Ordinal)) return "OperatorCabin";
        if (partName.StartsWith("Trolley", StringComparison.Ordinal) ||
            partName.StartsWith("HoistMotor", StringComparison.Ordinal)) return "Trolley";
        if (partName.StartsWith("Cable", StringComparison.Ordinal)) return "Cable";
        if (partName.StartsWith("Hook", StringComparison.Ordinal)) return "Hook";
        if (partName.StartsWith("GantryRail", StringComparison.Ordinal)) return "RailTracks";
        return "StructuralDetails";
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string folder = Path.GetFileName(assetPath);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static float InverseNonZero(float value)
    {
        return Mathf.Abs(value) < 0.000001f ? 1f : 1f / value;
    }

    private static bool AssetPathExists(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) && File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath)));
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void LogStatus(string message)
    {
        Debug.Log("[Naval Shipyard Builder] " + message);
        NavalShipyardBuilderWindow window = GetWindow<NavalShipyardBuilderWindow>("Naval Shipyard Builder");
        window.status = message;
        window.Repaint();
    }

    private sealed class ModuleData
    {
        public readonly string Category;
        public readonly string Name;
        public readonly List<PartData> Parts = new List<PartData>();
        public ModuleData(string category, string name) { Category = category; Name = name; }
    }

    private sealed class PartData
    {
        public readonly MeshFilter Filter;
        public readonly MeshRenderer Renderer;
        public PartData(MeshFilter filter, MeshRenderer renderer) { Filter = filter; Renderer = renderer; }
    }
}

internal static class NavalShipyardBoundsExtensions
{
    public static Bounds InverseTransformBounds(this Transform transform, Bounds bounds)
    {
        Vector3 center = transform.InverseTransformPoint(bounds.center);
        Vector3 extents = bounds.extents;
        Vector3 axisX = transform.InverseTransformVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = transform.InverseTransformVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = transform.InverseTransformVector(new Vector3(0f, 0f, extents.z));
        Vector3 localExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, localExtents * 2f);
    }
}
