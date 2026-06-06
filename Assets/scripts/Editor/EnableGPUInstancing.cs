using UnityEngine;
using UnityEditor;
using System.Linq;

public class EnableGPUInstancing : EditorWindow
{
    [MenuItem("Tools/Otimização/Habilitar GPU Instancing (Todos os Materiais)")]
    public static void EnableInstancing()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat != null && !mat.enableInstancing)
            {
                // Só habilita se o shader suportar
                if (mat.shader != null && mat.shader.isSupported)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Otimização] GPU Instancing ativado em {count} materiais! Isso reduzirá brutalmente as chamadas de CPU (Draw Calls).");
    }
}
