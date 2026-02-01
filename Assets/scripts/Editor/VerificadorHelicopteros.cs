using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class VerificadorHelicopteros : MonoBehaviour
{
    [MenuItem("Hegemonia/Corrigir Helicópteros")]
    public static void Corrigir()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int corrigidos = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;

            string nome = prefab.name.ToLower();
            // Verifica se parece um helicóptero pelo nome
            if (nome.Contains("heli") || nome.Contains("chopper") || nome.Contains("copter") || nome.Contains("apache") || nome.Contains("blackhawk"))
            {
                // Verifica se FALTA o script Helicoptero
                if (prefab.GetComponent<Helicoptero>() == null)
                {
                    prefab.AddComponent<Helicoptero>();
                    EditorUtility.SetDirty(prefab);
                    corrigidos++;
                    Debug.Log($"✅ Adicionado script 'Helicoptero' ao prefab: {prefab.name}");
                }
            }
        }
        
        AssetDatabase.SaveAssets();

        if (corrigidos > 0)
            Debug.Log($"🎉 Total de {corrigidos} helicópteros corrigidos! Agora eles aparecerão no menu.");
        else
            Debug.Log("👍 Todos os helicópteros já parecem estar corretos.");
    }
}
#endif
