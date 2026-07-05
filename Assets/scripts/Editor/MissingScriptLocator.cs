using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptLocator
{
    private const string SessionKey = "MissingScriptLocator.RanThisSession";

    [InitializeOnLoadMethod]
    private static void ScheduleScanOnLoad()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += ScanOpenScenes;
    }

    [MenuItem("Tools/Diagnostics/Scan Missing Scripts")]
    private static void ScanOpenScenes()
    {
        int totalMissing = 0;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                totalMissing += ScanHierarchy(scene.name, roots[i]);
            }
        }

        if (totalMissing > 0)
        {
            Debug.LogWarning("[MissingScriptLocator] Total de scripts faltando nas cenas abertas: " + totalMissing);
        }
        else
        {
            Debug.Log("[MissingScriptLocator] Nenhum script faltando encontrado nas cenas abertas.");
        }
    }

    [MenuItem("Tools/Diagnostics/Scan Missing Scripts In Prefabs")]
    private static void ScanProjectPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int totalMissing = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabRoot == null)
            {
                continue;
            }

            int missingHere = ScanPrefabHierarchy(path, prefabRoot);
            totalMissing += missingHere;
        }

        if (totalMissing > 0)
        {
            Debug.LogWarning("[MissingScriptLocator] Total de scripts faltando em prefabs: " + totalMissing);
        }
        else
        {
            Debug.Log("[MissingScriptLocator] Nenhum script faltando encontrado nos prefabs.");
        }
    }

    [MenuItem("Tools/Diagnostics/Remove Missing Scripts From Selection")]
    private static void RemoveMissingScriptsFromSelection()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("[MissingScriptLocator] Nada selecionado para limpar.");
            return;
        }

        int totalRemoved = 0;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Object selected = selectedObjects[i];
            if (selected == null)
            {
                continue;
            }

            GameObject selectedGameObject = selected as GameObject;
            if (selectedGameObject != null)
            {
                totalRemoved += RemoveMissingScriptsRecursive(selectedGameObject);
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot != null)
            {
                totalRemoved += RemoveMissingScriptsRecursive(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MissingScriptLocator] Scripts faltando removidos da selecao: " + totalRemoved);
    }

    [MenuItem("Tools/Diagnostics/Remove Missing Scripts From ALL Prefabs (Project-wide)")]
    private static void RemoveMissingScriptsFromAllPrefabs()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Remover Missing Scripts de TODOS os Prefabs",
            "Isso vai escanear e limpar TODOS os prefabs do projeto.\n" +
            "A operação é destrutiva (remove componentes inválidos).\n" +
            "Certifique-se de ter um backup ou commit git antes de continuar.",
            "Continuar",
            "Cancelar");

        if (!confirm) return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int totalRemoved = 0;
        int totalPrefabs = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                EditorUtility.DisplayProgressBar(
                    "Removendo Missing Scripts...",
                    path,
                    (float)i / prefabGuids.Length);

                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null) continue;

                // Usa PrefabUtility para editar o asset de prefab corretamente.
                using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    int removed = RemoveMissingScriptsRecursive(editScope.prefabContentsRoot);
                    if (removed > 0)
                    {
                        totalRemoved += removed;
                        totalPrefabs++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = totalRemoved > 0
            ? $"[MissingScriptLocator] Removidos {totalRemoved} missing script(s) de {totalPrefabs} prefab(s)."
            : "[MissingScriptLocator] Nenhum missing script encontrado em nenhum prefab.";

        Debug.LogWarning(msg);
        EditorUtility.DisplayDialog("Concluído", msg, "OK");
    }

    [MenuItem("Tools/Diagnostics/Remove Missing Scripts From Open Scenes")]
    private static void RemoveMissingScriptsFromOpenScenes()
    {
        int totalRemoved = 0;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                totalRemoved += RemoveMissingScriptsRecursive(roots[i]);
            }
        }

        if (totalRemoved > 0)
        {
            Debug.LogWarning("[MissingScriptLocator] Scripts faltando removidos das cenas abertas: " + totalRemoved);
        }
        else
        {
            Debug.Log("[MissingScriptLocator] Nenhum script faltando precisou ser removido das cenas abertas.");
        }
    }

    private static int ScanHierarchy(string sceneName, GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int missingHere = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        if (missingHere > 0)
        {
            Debug.LogWarning(
                "[MissingScriptLocator] Cena=" + sceneName
                + " | Objeto=" + GetHierarchyPath(root.transform)
                + " | MissingScripts=" + missingHere,
                root);
        }

        int total = missingHere;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            total += ScanHierarchy(sceneName, transform.GetChild(i).gameObject);
        }

        return total;
    }

    private static int ScanPrefabHierarchy(string assetPath, GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int missingHere = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        if (missingHere > 0)
        {
            Debug.LogWarning(
                "[MissingScriptLocator] Prefab=" + assetPath
                + " | Objeto=" + GetHierarchyPath(root.transform)
                + " | MissingScripts=" + missingHere,
                root);
        }

        int total = missingHere;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            total += ScanPrefabHierarchy(assetPath, transform.GetChild(i).gameObject);
        }

        return total;
    }

    private static string GetHierarchyPath(Transform current)
    {
        if (current == null)
        {
            return "(null)";
        }

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }

    private static int RemoveMissingScriptsRecursive(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int removedHere = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
        if (removedHere > 0)
        {
            Debug.LogWarning(
                "[MissingScriptLocator] Missing scripts removidos | Objeto=" + GetHierarchyPath(root.transform)
                + " | Removed=" + removedHere,
                root);

            EditorUtility.SetDirty(root);
        }

        int total = removedHere;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            total += RemoveMissingScriptsRecursive(transform.GetChild(i).gameObject);
        }

        return total;
    }
}
