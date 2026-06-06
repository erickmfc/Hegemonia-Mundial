using UnityEngine;
using UnityEditor;
using System.IO;

public class FindMissingScriptsWindow : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<FindMissingScriptsWindow>("Missing Scripts");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Scan Prefabs for Missing Scripts"))
        {
            Scan();
        }
    }

    public static void Scan()
    {
        string[] prefabPaths = Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories);
        int totalMissing = 0;

        foreach (string path in prefabPaths)
        {
            string relativePath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
            if (prefab == null) continue;

            // Let's traverse all GameObjects in the prefab to find exactly where the missing script is
            Transform[] allTransforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                int missingHere = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missingHere > 0)
                {
                    Debug.LogWarning($"GameObject '{GetPath(t)}' in prefab '{relativePath}' has {missingHere} missing script(s)!", prefab);
                    totalMissing += missingHere;
                }
            }
        }
        Debug.Log($"Scan completed. Total missing scripts found: {totalMissing}");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        Transform current = t;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }
}
