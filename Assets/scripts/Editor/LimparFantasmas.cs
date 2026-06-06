using UnityEngine;
using UnityEditor;

public class LimparFantasmas : EditorWindow
{
    [MenuItem("Hegemonia/Limpar Scripts Fantasmas")]
    public static void LimparTudo()
    {
        string[] prefabPaths = AssetDatabase.GetAllAssetPaths();
        int limpados = 0;
        int prefabsLimpados = 0;

        foreach (string path in prefabPaths)
        {
            if (path.EndsWith(".prefab"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    int removidos = LimparRecursivo(prefab);
                    if (removidos > 0)
                    {
                        limpados += removidos;
                        prefabsLimpados++;
                        EditorUtility.SetDirty(prefab);
                        Debug.Log($"Limpo: {prefab.name} em {path} (-{removidos} fantasmas)");
                    }
                }
            }
        }

        if (limpados > 0)
        {
            AssetDatabase.SaveAssets();
        }
        
        Debug.Log($"Limpeza Concluída! Total de scripts fantasmas destruídos: {limpados} em {prefabsLimpados} prefabs.");
        EditorUtility.DisplayDialog("Limpeza de Fantasmas", 
            $"Limpeza Concluída!\n\nScripts fantasmas destruídos: {limpados}\nPrefabs corrigidos: {prefabsLimpados}", 
            "OK");
    }

    private static int LimparRecursivo(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        for (int i = 0; i < go.transform.childCount; i++)
        {
            count += LimparRecursivo(go.transform.GetChild(i).gameObject);
        }
        return count;
    }
}
