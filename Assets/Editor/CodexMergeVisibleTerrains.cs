using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot editor utility used to bring the terrain objects from the recovery
/// scene into the complete campaign scene. It intentionally does not copy any
/// water object or any gameplay root.
/// </summary>
public static class CodexMergeVisibleTerrains
{
    private const string MainScenePath = "Assets/Scenes/cena19).unity";
    private const string RecoveryScenePath = "Assets/_Recovery/cena19).unity";

    private static readonly HashSet<string> TerrainNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pais4",
        "pais3",
        "paredao inimigo",
        "Pais2",
        "Ilha",
        "Ilha ",
        "pais usuario"
    };

    [MenuItem("Tools/Codex/Merge visible terrains into campaign")]
    public static void MergeVisibleTerrains()
    {
        Scene main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Scene recovery = EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(main);

        try
        {
            Transform terrainRoot = main.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Terrenos");

            Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Resources/CodexCampaignTerrainURP.mat");

            var existingNames = new HashSet<string>(
                main.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                    .Select(t => t.gameObject.name),
                StringComparer.OrdinalIgnoreCase);

            var sourceTerrains = recovery.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .Where(t => TerrainNames.Contains(t.gameObject.name))
                .ToArray();

            int copied = 0;
            foreach (Terrain sourceTerrain in sourceTerrains)
            {
                if (existingNames.Contains(sourceTerrain.gameObject.name))
                    continue;

                GameObject clone = UnityEngine.Object.Instantiate(sourceTerrain.gameObject);
                clone.name = sourceTerrain.gameObject.name;
                SceneManager.MoveGameObjectToScene(clone, main);

                if (terrainRoot != null)
                    clone.transform.SetParent(terrainRoot, true);

                foreach (Terrain terrain in clone.GetComponentsInChildren<Terrain>(true))
                {
                    terrain.gameObject.layer = LayerMask.NameToLayer("Chao") >= 0
                        ? LayerMask.NameToLayer("Chao")
                        : terrain.gameObject.layer;
                    terrain.drawInstanced = false;
                    if (terrainMaterial != null)
                        terrain.materialTemplate = terrainMaterial;

                    TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                    if (collider != null)
                        collider.gameObject.layer = terrain.gameObject.layer;

                    EditorUtility.SetDirty(terrain);
                }

                existingNames.Add(clone.name);
                EditorUtility.SetDirty(clone);
                copied++;
            }

            // Keep the same safe material/instancing settings on the three
            // original playable terrains, without touching water renderers.
            foreach (Terrain terrain in main.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Terrain>(true)))
            {
                terrain.drawInstanced = false;
                if (terrainMaterial != null)
                    terrain.materialTemplate = terrainMaterial;
                EditorUtility.SetDirty(terrain);
            }

            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            Debug.Log($"[Codex] Terrenos incorporados na cena oficial: {copied}. Agua e gameplay preservados.");
        }
        finally
        {
            if (recovery.IsValid() && recovery.isLoaded)
                EditorSceneManager.CloseScene(recovery, false);
        }
    }
}
