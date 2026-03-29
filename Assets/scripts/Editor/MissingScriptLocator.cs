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
}
