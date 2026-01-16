using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class CorretorHeliporto : MonoBehaviour
{
    [MenuItem("Hegemonia/Corrigir Colisor do Heliporto")]
    public static void Corrigir()
    {
        // Procura o prefab do HeliPad
        string[] guids = AssetDatabase.FindAssets("HeliPad t:Prefab");
        if (guids.Length == 0)
        {
            Debug.LogError("❌ Prefab 'HeliPad' não encontrado!");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab != null)
        {
            // Adiciona BoxCollider se não tiver
            BoxCollider col = prefab.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = prefab.AddComponent<BoxCollider>();
                // Ajusta tamanho generico (assumindo que é plano)
                col.center = new Vector3(0, 0.2f, 0);
                col.size = new Vector3(10f, 0.5f, 10f); 
                Debug.Log("✅ [Corretor] BoxCollider adicionado e configurado.");
            }
            else
            {
                // Garante que é grande o suficiente
                 col.center = new Vector3(0, 0.2f, 0);
                 col.size = new Vector3(10f, 0.5f, 10f);
                 Debug.Log("✅ [Corretor] BoxCollider ajustado.");
            }

            // Garante que tem o script Heliporto
            Heliporto script = prefab.GetComponent<Heliporto>();
            if (script == null)
            {
                prefab.AddComponent<Heliporto>();
                Debug.Log("✅ [Corretor] Script Heliporto adicionado.");
            }

            // Define Layer Default (0) para garantir que o Raycast pegue
            prefab.layer = 0; 

            // Salva
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("🎉 Heliporto corrigido com sucesso! Tente construir um novo.");
        }
    }
}
#endif
