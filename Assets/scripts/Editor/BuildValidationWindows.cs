using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hegemonia.EditorTools
{
    /// <summary>
    /// Entry point deterministico para builds locais e CI. O batchmode passa a
    /// falhar explicitamente se nao houver cenas ou se o BuildPipeline falhar.
    /// </summary>
    public static class BuildValidationWindows
    {
        private const BuildTarget WindowsTarget = BuildTarget.StandaloneWindows64;
        private const string GlobalMapScenePath = "Assets/Scenes/GlobalMapRTS.unity";
        private const string TerrainMaterialPath = "Assets/Resources/CodexCampaignTerrainURP.mat";
        private const string WorldDefinitionPath = "Assets/Worlds/GlobalMap/Data/GlobalWorldDefinition.asset";
        private const string TerrainShaderName = "Universal Render Pipeline/Terrain/Lit";

        [MenuItem("Hegemonia/Build/Validate Windows 64")]
        public static void BuildWindows64()
        {
            string outputFromEnvironment = Environment.GetEnvironmentVariable("HEGEMONIA_BUILD_OUTPUT");
            string outputPath = string.IsNullOrWhiteSpace(outputFromEnvironment)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows", "Hegemonia-Mundial.exe"))
                : Path.GetFullPath(outputFromEnvironment);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("BuildValidation: nenhuma cena habilitada em EditorBuildSettings.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "BuildValidation: build Windows falhou. Resultado=" + report.summary.result
                    + " erros=" + report.summary.totalErrors
                    + " avisos=" + report.summary.totalWarnings
                    + " tamanho=" + report.summary.totalSize);
            }

            Debug.Log(
                "BuildValidation: SUCCESS | target=Windows64 | scenes=" + scenes.Length
                + " | output=" + outputPath
                + " | size=" + report.summary.totalSize);
        }

        [MenuItem("Hegemonia/Build/Compatibility Windows 64 (D3D11)")]
        public static void BuildWindows64Compatibility()
        {
            string requestedOutputPath = Environment.GetEnvironmentVariable("HEGEMONIA_BUILD_OUTPUT");
            string outputPath = Path.GetFullPath(string.IsNullOrWhiteSpace(requestedOutputPath)
                ? Path.Combine(Application.dataPath, "..", "Builds", "Windows-Release-D3D11", "Hegemonia Global.exe")
                : requestedOutputPath);
            string[] scenes = ObterCenasHabilitadas();
            ValidarEntradasDaBuild(scenes);
            bool developmentProfile = string.Equals(
                Environment.GetEnvironmentVariable("HEGEMONIA_BUILD_PROFILE"), "1", StringComparison.Ordinal);
            BuildOptions buildOptions = BuildOptions.StrictMode;
            if (developmentProfile)
                buildOptions |= BuildOptions.Development | BuildOptions.AllowDebugging;

            bool useDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(WindowsTarget);
            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(WindowsTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildReport report;
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(WindowsTarget, false);
                PlayerSettings.SetGraphicsAPIs(WindowsTarget, new[] { GraphicsDeviceType.Direct3D11 });

                Debug.Log("[BuildValidation] Compatibilidade Windows: APIs=[Direct3D11], cenas="
                    + string.Join(";", scenes) + ", saída=" + outputPath
                    + ", developmentProfile=" + developmentProfile);

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = WindowsTarget,
                    options = buildOptions
                });

                EscreverRelatorioBuild(outputPath, scenes, report, developmentProfile);
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(WindowsTarget, graphicsApis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(WindowsTarget, useDefaultGraphicsApis);
            }

            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
            {
                throw new BuildFailedException(
                    "BuildValidation D3D11 falhou. Resultado=" + report.summary.result
                    + " erros=" + report.summary.totalErrors
                    + " avisos=" + report.summary.totalWarnings
                    + " tamanho=" + report.summary.totalSize);
            }

            Debug.Log("[BuildValidation] D3D11 SUCCESS | scenes=" + scenes.Length
                + " | output=" + outputPath
                + " | size=" + report.summary.totalSize
                + " | duration=" + report.summary.totalTime
                + " | warnings=" + report.summary.totalWarnings
                + " | errors=" + report.summary.totalErrors);
        }

        private static string[] ObterCenasHabilitadas()
        {
            string overrideFromEnvironment = Environment.GetEnvironmentVariable("HEGEMONIA_BUILD_SCENES");
            if (!string.IsNullOrWhiteSpace(overrideFromEnvironment))
            {
                string[] requestedScenes = overrideFromEnvironment
                    .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(scene => scene.Trim())
                    .Where(scene => !string.IsNullOrWhiteSpace(scene))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                string[] invalidScenes = requestedScenes
                    .Where(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    .ToArray();
                if (requestedScenes.Length == 0 || invalidScenes.Length > 0)
                {
                    throw new BuildFailedException(
                        "BuildValidation: HEGEMONIA_BUILD_SCENES contém cenas inválidas: "
                        + string.Join(";", invalidScenes));
                }

                return requestedScenes;
            }

            return EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
        }

        [MenuItem("Hegemonia/Build/Diagnose Windows Build Content")]
        public static void DiagnoseWindowsBuildContent()
        {
            string[] scenes = ObterCenasHabilitadas();
            if (scenes.Length == 0)
                throw new BuildFailedException("BuildValidation: nenhuma cena habilitada para diagnóstico.");

            StringBuilder results = new StringBuilder();
            results.AppendLine("Diagnóstico somente leitura do conteúdo da build Windows");
            results.AppendLine("Unity: " + Application.unityVersion);
            results.AppendLine("Cenas habilitadas: " + scenes.Length);

            int missingSceneScripts = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                Scene existing = SceneManager.GetSceneByPath(scenes[i]);
                bool openedForScan = !existing.IsValid() || !existing.isLoaded;
                Scene scene = openedForScan
                    ? EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Additive)
                    : existing;

                int missingInScene = 0;
                try
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                        missingInScene += CountMissingScripts(roots[rootIndex]);
                }
                finally
                {
                    if (openedForScan && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }

                missingSceneScripts += missingInScene;
                results.AppendLine("SCENE " + scenes[i] + " | missingScripts=" + missingInScene);
            }

            string[] dependencies = scenes
                .SelectMany(AssetDatabase.GetDependencies)
                .Distinct()
                .ToArray();
            int failedAssetLoads = 0;
            int recoveredByReimport = 0;
            int missingPrefabScripts = 0;
            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = dependencies[i];
                string extension = Path.GetExtension(path);
                if (!string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".terrainlayer", StringComparison.OrdinalIgnoreCase))
                    continue;

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    AssetDatabase.ImportAsset(
                        path,
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null)
                    {
                        failedAssetLoads++;
                        results.AppendLine("UNLOADABLE " + path);
                        continue;
                    }

                    recoveredByReimport++;
                    results.AppendLine("REIMPORTED " + path + " | type=" + asset.GetType().FullName);
                }

                GameObject prefab = asset as GameObject;
                if (prefab != null && string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    int missingInPrefab = CountMissingScripts(prefab);
                    if (missingInPrefab > 0)
                    {
                        missingPrefabScripts += missingInPrefab;
                        results.AppendLine("PREFAB " + path + " | missingScripts=" + missingInPrefab);
                    }
                }
            }

            results.AppendLine("DEPENDENCIES SCANNED: " + dependencies.Length);
            results.AppendLine("FAILED ASSET LOADS: " + failedAssetLoads);
            results.AppendLine("RECOVERED BY REIMPORT: " + recoveredByReimport);
            results.AppendLine("MISSING SCRIPTS IN ENABLED SCENES: " + missingSceneScripts);
            results.AppendLine("MISSING SCRIPTS IN DEPENDENCY PREFABS: " + missingPrefabScripts);

            string outputPath = Path.Combine(
                Path.GetTempPath(),
                "HegemoniaBuildContentDiagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(outputPath, results.ToString());
            Debug.Log("[BuildValidation] Diagnóstico concluído: " + outputPath
                + " | failedAssets=" + failedAssetLoads
                + " | reimported=" + recoveredByReimport
                + " | missingSceneScripts=" + missingSceneScripts
                + " | missingPrefabScripts=" + missingPrefabScripts);
        }

        [MenuItem("Hegemonia/Build/Diagnose Legacy Terrain Textures")]
        public static void DiagnoseLegacyTerrainTextures()
        {
            const string candidateFolder = "Assets/__HegemoniaAuditCandidates";
            string[] candidatePaths = Directory.GetFiles(candidateFolder, "*.asset")
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            StringBuilder results = new StringBuilder();
            int loadedTextures = 0;
            int loadedTerrainData = 0;

            for (int i = 0; i < candidatePaths.Length; i++)
            {
                string path = candidatePaths[i];
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                TerrainData terrainData = asset as TerrainData;
                if (terrainData != null)
                {
                    loadedTerrainData++;
                    results.AppendLine(path + " | TerrainData | size=" + terrainData.size
                        + " heightmapResolution=" + terrainData.heightmapResolution
                        + " alphamapResolution=" + terrainData.alphamapResolution
                        + " layers=" + (terrainData.terrainLayers == null ? 0 : terrainData.terrainLayers.Length)
                        + " details=" + (terrainData.detailPrototypes == null ? 0 : terrainData.detailPrototypes.Length)
                        + " trees=" + terrainData.treeInstanceCount);

                    try
                    {
                        const int amostrasPorEixo = 8;
                        float minHeight = float.MaxValue;
                        float maxHeight = float.MinValue;
                        double sumHeight = 0d;
                        int sampleCount = 0;
                        StringBuilder sampleGrid = new StringBuilder();
                        for (int sampleZ = 0; sampleZ <= amostrasPorEixo; sampleZ++)
                        {
                            for (int sampleX = 0; sampleX <= amostrasPorEixo; sampleX++)
                            {
                                float height = terrainData.GetInterpolatedHeight(
                                    sampleX / (float)amostrasPorEixo,
                                    sampleZ / (float)amostrasPorEixo);
                                minHeight = Mathf.Min(minHeight, height);
                                maxHeight = Mathf.Max(maxHeight, height);
                                sumHeight += height;
                                sampleCount++;
                                if (sampleX < 5 && sampleZ < 5)
                                    sampleGrid.Append(height.ToString("F1")).Append(sampleX == 4 ? " | " : ",");
                            }
                        }

                        results.AppendLine("  sampledHeight=" + minHeight.ToString("F1") + ".." + maxHeight.ToString("F1")
                            + " mean=" + (sumHeight / sampleCount).ToString("F1")
                            + " grid=" + sampleGrid);
                        TerrainLayer[] layers = terrainData.terrainLayers;
                        if (layers != null)
                        {
                            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                                results.AppendLine("  layer[" + layerIndex + "]="
                                    + (layers[layerIndex] == null ? "null" : AssetDatabase.GetAssetPath(layers[layerIndex])));
                        }
                    }
                    catch (Exception exception)
                    {
                        results.AppendLine("  terrainRead=" + exception.GetType().Name + ": " + exception.Message);
                    }

                    continue;
                }

                if (!(asset is Texture2D texture))
                {
                    results.AppendLine(path + " | type=" + (asset == null ? "null" : asset.GetType().FullName)
                        + " | mainType=" + (mainAssetType == null ? "null" : mainAssetType.FullName));
                    UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int subIndex = 0; subIndex < subAssets.Length; subIndex++)
                    {
                        UnityEngine.Object subAsset = subAssets[subIndex];
                        if (subAsset != null)
                            results.AppendLine("  subasset=" + subAsset.name + " type=" + subAsset.GetType().FullName
                                + " instanceId=" + subAsset.GetInstanceID());
                    }
                    continue;
                }

                loadedTextures++;
                results.AppendLine(path + " | width=" + texture.width
                    + " height=" + texture.height
                    + " format=" + texture.format
                    + " readable=" + texture.isReadable
                    + " mipmaps=" + texture.mipmapCount);

                try
                {
                    Color32[] pixels = texture.GetPixels32();
                    byte minR = byte.MaxValue;
                    byte maxR = byte.MinValue;
                    byte minG = byte.MaxValue;
                    byte maxG = byte.MinValue;
                    byte minB = byte.MaxValue;
                    byte maxB = byte.MinValue;
                    double sumR = 0d;
                    for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
                    {
                        Color32 pixel = pixels[pixelIndex];
                        minR = Math.Min(minR, pixel.r);
                        maxR = Math.Max(maxR, pixel.r);
                        minG = Math.Min(minG, pixel.g);
                        maxG = Math.Max(maxG, pixel.g);
                        minB = Math.Min(minB, pixel.b);
                        maxB = Math.Max(maxB, pixel.b);
                        sumR += pixel.r;
                    }

                    results.AppendLine("  pixels=" + pixels.Length
                        + " R=" + minR + ".." + maxR
                        + " G=" + minG + ".." + maxG
                        + " B=" + minB + ".." + maxB
                        + " meanR=" + (pixels.Length == 0 ? 0d : sumR / pixels.Length));
                }
                catch (Exception exception)
                {
                    results.AppendLine("  pixelRead=" + exception.GetType().Name + ": " + exception.Message);
                }
            }

            string outputPath = Path.Combine(
                Path.GetTempPath(),
                "HegemoniaLegacyTerrainTextureDiagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(outputPath, results.ToString());
            Debug.Log("[BuildValidation] Assets de terreno lidos: TerrainData=" + loadedTerrainData
                + " textures=" + loadedTextures
                + " | relatório=" + outputPath);
        }

        [MenuItem("Hegemonia/Build/Map Unloadable Build Dependencies")]
        public static void MapUnloadableBuildDependencies()
        {
            string[] scenes = ObterCenasHabilitadas();
            var sceneDependencies = scenes
                .Select(path => new { path, dependencies = AssetDatabase.GetDependencies(path) })
                .ToArray();
            string[] dependencies = sceneDependencies
                .SelectMany(scene => scene.dependencies)
                .Distinct()
                .ToArray();
            string[] unloadable = dependencies
                .Where(path =>
                {
                    string extension = Path.GetExtension(path);
                    bool inspectable = string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".terrainlayer", StringComparison.OrdinalIgnoreCase);
                    return inspectable && AssetDatabase.LoadMainAssetAtPath(path) == null;
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            StringBuilder results = new StringBuilder();
            results.AppendLine("Mapeamento de dependências não carregáveis da build Windows");
            results.AppendLine("Unity: " + Application.unityVersion);
            results.AppendLine("Cenas: " + scenes.Length + " | dependências: " + dependencies.Length
                + " | não carregáveis: " + unloadable.Length);
            for (int i = 0; i < unloadable.Length; i++)
            {
                string path = unloadable[i];
                Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string[] directDependencies = AssetDatabase.GetDependencies(path, false)
                    .Where(dependency => !string.Equals(dependency, path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                string[] owningScenes = sceneDependencies
                    .Where(scene => scene.dependencies.Contains(path, StringComparer.OrdinalIgnoreCase))
                    .Select(scene => scene.path)
                    .ToArray();

                results.AppendLine();
                results.AppendLine("ASSET " + path);
                results.AppendLine("  guid=" + guid + " mainType=" + (mainType == null ? "<null>" : mainType.FullName));
                results.AppendLine("  ownerScenes=" + (owningScenes.Length == 0 ? "<none>" : string.Join("; ", owningScenes)));
                results.AppendLine("  directDependencies=" + (directDependencies.Length == 0
                    ? "<none>" : string.Join("; ", directDependencies)));
            }

            string outputDirectory = Environment.GetEnvironmentVariable("HEGEMONIA_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = Path.GetTempPath();
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory,
                "UnloadableBuildDependencies-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(outputPath, results.ToString());
            Debug.Log("[BuildValidation] Mapeamento de dependências concluído: " + outputPath
                + " | unloadable=" + unloadable.Length);
        }

        [MenuItem("Hegemonia/Build/Diagnose Hasaf Prefab Geometry")]
        public static void DiagnoseHasafPrefabGeometry()
        {
            const string prefabPath = "Assets/Library/Prefabs/Aeroporto/DH_Hasaf/DH_Hasaf.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new BuildFailedException("BuildValidation: não foi possível carregar o prefab " + prefabPath);

            try
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                StringBuilder report = new StringBuilder();
                report.AppendLine("Prefab: " + prefabPath);
                report.AppendLine("Root: " + root.name + " | scale=" + root.transform.localScale);
                report.AppendLine("Renderers: " + renderers.Length + " | colliders=" + colliders.Length);

                if (renderers.Length > 0)
                {
                    Bounds worldBounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        worldBounds.Encapsulate(renderers[i].bounds);

                    Vector3 worldMin = worldBounds.min;
                    Vector3 worldMax = worldBounds.max;
                    Vector3 localMin = Vector3.one * float.MaxValue;
                    Vector3 localMax = Vector3.one * float.MinValue;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = new Vector3(
                            (corner & 1) == 0 ? worldMin.x : worldMax.x,
                            (corner & 2) == 0 ? worldMin.y : worldMax.y,
                            (corner & 4) == 0 ? worldMin.z : worldMax.z);
                        point = root.transform.InverseTransformPoint(point);
                        localMin = Vector3.Min(localMin, point);
                        localMax = Vector3.Max(localMax, point);
                    }

                    report.AppendLine("Renderer world bounds: center=" + worldBounds.center + " size=" + worldBounds.size);
                    report.AppendLine("Root-local enclosing bounds: center=" + ((localMin + localMax) * 0.5f)
                        + " size=" + (localMax - localMin));
                }

                for (int i = 0; i < renderers.Length; i++)
                    report.AppendLine("Renderer[" + i + "]=" + renderers[i].name + " bounds=" + renderers[i].bounds.size);
                for (int i = 0; i < colliders.Length; i++)
                    report.AppendLine("Collider[" + i + "]=" + colliders[i].GetType().Name + " on " + colliders[i].name);

                string outputPath = Path.Combine(Path.GetTempPath(), "HegemoniaHasafPrefabGeometry-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(outputPath, report.ToString());
                Debug.Log("[BuildValidation] Hasaf geometry diagnostic: " + outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Hegemonia/Build/Diagnose Hasaf Catalog References")]
        public static void DiagnoseHasafCatalogReferences()
        {
            string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
            StringBuilder report = new StringBuilder();
            int matchingEntries = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string dataPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                DadosConstrucao ficha = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(dataPath);
                if (ficha == null)
                    continue;

                string displayName = ficha.GetDisplayName() ?? string.Empty;
                bool hasPrefab = ficha.TryGetPrefab(out GameObject prefab);
                string prefabName = hasPrefab && prefab != null ? prefab.name : string.Empty;
                if (displayName.IndexOf("hasaf", StringComparison.OrdinalIgnoreCase) < 0
                    && prefabName.IndexOf("hasaf", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                matchingEntries++;
                string prefabPath = hasPrefab && prefab != null ? AssetDatabase.GetAssetPath(prefab) : string.Empty;
                Collider[] colliders = hasPrefab && prefab != null
                    ? prefab.GetComponentsInChildren<Collider>(true)
                    : Array.Empty<Collider>();

                report.AppendLine("Ficha: " + dataPath
                    + " | assetName=" + ficha.name
                    + " | displayName=" + displayName
                    + " | stableId=" + ficha.GetStableId()
                    + " | categoria=" + ficha.categoria
                    + " | prefab=" + (hasPrefab && prefab != null ? prefab.name : "ausente")
                    + " | prefabPath=" + prefabPath
                    + " | colliders=" + colliders.Length);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];
                    report.AppendLine("  " + collider.GetType().Name
                        + " | objeto=" + collider.name
                        + " | enabled=" + collider.enabled
                        + " | trigger=" + collider.isTrigger);
                }
            }

            report.Insert(0, "DadosConstrucao examinados: " + guids.Length
                + " | entradas Hasaf: " + matchingEntries + Environment.NewLine);
            Directory.CreateDirectory(@"D:\HegemoniaValidation");
            string outputPath = Path.Combine(@"D:\HegemoniaValidation",
                "hasaf-catalog-diagnostic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(outputPath, report.ToString());
            Debug.Log("[BuildValidation] Diagnóstico do catálogo Hasaf: " + outputPath
                + " | entradas=" + matchingEntries);
        }

        [MenuItem("Hegemonia/Build/Repair Hasaf Collider From Visual Bounds")]
        public static void RepairHasafColliderFromVisualBounds()
        {
            const string prefabPath = "Assets/Library/Prefabs/Aeroporto/DH_Hasaf/DH_Hasaf.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new BuildFailedException("BuildValidation: não foi possível carregar o prefab " + prefabPath);

            try
            {
                if (root.GetComponentInChildren<Collider>(true) != null)
                {
                    Debug.Log("[BuildValidation] Hasaf já possui Collider; nenhum componente foi alterado.");
                    return;
                }

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null && renderer.enabled)
                    .ToArray();
                if (renderers.Length == 0)
                    throw new BuildFailedException("BuildValidation: o prefab Hasaf não possui renderers habilitados para calcular a colisão.");

                Bounds worldBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    worldBounds.Encapsulate(renderers[i].bounds);

                Vector3 worldMin = worldBounds.min;
                Vector3 worldMax = worldBounds.max;
                Vector3 localMin = Vector3.one * float.MaxValue;
                Vector3 localMax = Vector3.one * float.MinValue;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? worldMin.x : worldMax.x,
                        (corner & 2) == 0 ? worldMin.y : worldMax.y,
                        (corner & 4) == 0 ? worldMin.z : worldMax.z);
                    point = root.transform.InverseTransformPoint(point);
                    localMin = Vector3.Min(localMin, point);
                    localMax = Vector3.Max(localMax, point);
                }

                Vector3 size = localMax - localMin;
                if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
                    throw new BuildFailedException("BuildValidation: limites visuais inválidos para o Collider do Hasaf: " + size);

                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = (localMin + localMax) * 0.5f;
                box.size = size;
                box.isTrigger = false;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null)
                    throw new BuildFailedException("BuildValidation: não foi possível salvar o prefab reparado " + prefabPath);

                Debug.Log("[BuildValidation] Hasaf BoxCollider salvo no root; center=" + box.center
                    + " size=" + box.size + " renderers=" + renderers.Length + " trigger=false");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void DiagnoseRecoveryCandidatesAndHasaf()
        {
            DiagnoseLegacyTerrainTextures();
            DiagnoseHasafPrefabGeometry();
        }

        private static int CountMissingScripts(GameObject root)
        {
            if (root == null) return 0;
            int total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Transform current = root.transform;
            for (int i = 0; i < current.childCount; i++)
                total += CountMissingScripts(current.GetChild(i).gameObject);
            return total;
        }

        private static void ValidarEntradasDaBuild(string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
                throw new BuildFailedException("BuildValidation: nenhuma cena habilitada em EditorBuildSettings.");

            if (!scenes.Contains(GlobalMapScenePath))
                throw new BuildFailedException("BuildValidation: GlobalMapRTS não está habilitada em EditorBuildSettings.");

            for (int i = 0; i < scenes.Length; i++)
            {
                if (!File.Exists(scenes[i]) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[i]) == null)
                    throw new BuildFailedException("BuildValidation: cena habilitada inválida ou ausente: " + scenes[i]);
            }

            Shader terrainShader = Shader.Find(TerrainShaderName);
            Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            GlobalWorldDefinition world = AssetDatabase.LoadAssetAtPath<GlobalWorldDefinition>(WorldDefinitionPath);
            if (terrainShader == null || !terrainShader.isSupported)
                throw new BuildFailedException("BuildValidation: shader URP Terrain/Lit ausente ou não suportado no Editor.");
            if (terrainMaterial == null || terrainMaterial.shader != terrainShader)
                throw new BuildFailedException("BuildValidation: material do terreno não está associado ao shader URP Terrain/Lit.");
            if (world == null)
                throw new BuildFailedException("BuildValidation: GlobalWorldDefinition não foi encontrado.");

            Scene scene = SceneManager.GetSceneByPath(GlobalMapScenePath);
            bool abriuCena = !scene.IsValid() || !scene.isLoaded;
            if (abriuCena)
                scene = EditorSceneManager.OpenScene(GlobalMapScenePath, OpenSceneMode.Additive);

            try
            {
                Terrain terrain = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length && terrain == null; i++)
                {
                    Terrain[] candidatos = roots[i].GetComponentsInChildren<Terrain>(true);
                    terrain = candidatos.FirstOrDefault(item => item != null && item.name == "GlobalMap_BaseTerrain");
                }

                if (terrain == null || terrain.terrainData == null)
                    throw new BuildFailedException("BuildValidation: GlobalMap_BaseTerrain ou seu TerrainData está ausente.");
                if (terrain.materialTemplate != terrainMaterial || terrain.materialTemplate.shader != terrainShader)
                    throw new BuildFailedException("BuildValidation: material/shader do terreno principal diverge do material URP validado.");

                TerrainData data = terrain.terrainData;
                if (Mathf.Abs(data.size.x - world.worldSize.x) > 1f
                    || Mathf.Abs(data.size.z - world.mapFootprintHeight) > 1f)
                    throw new BuildFailedException("BuildValidation: dimensões do TerrainData não correspondem à definição geográfica do mapa.");

                TerrainLayer[] layers = data.terrainLayers;
                if (layers == null || layers.Length == 0)
                    throw new BuildFailedException("BuildValidation: o TerrainData não possui TerrainLayers.");
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == null || layers[i].diffuseTexture == null)
                        throw new BuildFailedException("BuildValidation: TerrainLayer ou diffuseTexture ausente no índice " + i + ".");
                }

                Debug.Log("[BuildValidation] Preflight do terreno OK | shader=" + terrainShader.name
                    + " | terrainData=" + data.name
                    + " | dimensions=" + data.size.x + "x" + data.size.z
                    + " | layers=" + layers.Length
                    + " | shaderSupported=" + terrainShader.isSupported);
            }
            finally
            {
                if (abriuCena && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EscreverRelatorioBuild(string outputPath, string[] scenes, BuildReport report, bool developmentProfile)
        {
            string exeHash = string.Empty;
            if (File.Exists(outputPath))
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(outputPath))
                    exeHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }

            StringBuilder reportText = new StringBuilder();
            reportText.AppendLine("Build Hegemonia Global — Windows 64 — Direct3D 11");
            reportText.AppendLine("Unity: " + Application.unityVersion);
            reportText.AppendLine("Development profile: " + developmentProfile);
            reportText.AppendLine("Result: " + report.summary.result);
            reportText.AppendLine("Output: " + outputPath);
            reportText.AppendLine("Build size (bytes): " + report.summary.totalSize);
            reportText.AppendLine("Build time: " + report.summary.totalTime);
            reportText.AppendLine("Warnings: " + report.summary.totalWarnings);
            reportText.AppendLine("Errors: " + report.summary.totalErrors);
            reportText.AppendLine("EXE SHA-256: " + exeHash);
            reportText.AppendLine("Scenes:");
            for (int i = 0; i < scenes.Length; i++)
                reportText.AppendLine(i + ": " + scenes[i]);

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(outputPath), "BuildValidationReport.txt"), reportText.ToString());
        }
    }
}
