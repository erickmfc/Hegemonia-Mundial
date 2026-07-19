using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Ferramenta de limpeza para remover referências de scripts ausentes
/// </summary>
public static class CleanMissingScripts
{
    [MenuItem("Tools/🧹 Limpar Scripts Ausentes na Cena")]
    static void LimparScriptsAusentesNaCena()
    {
        GameObject[] objs = ObterGameObjectsDaCena();
        int contagem = 0;
        int objetosAfetados = 0;
        
        foreach (GameObject obj in objs)
        {
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            if (count > 0)
            {
                contagem += count;
                objetosAfetados++;
                Debug.Log($"✓ Removidos {count} scripts ausentes de: {obj.name}", obj);
            }
        }
        
        if (contagem > 0)
        {
            Debug.LogWarning($"[CleanMissingScripts] Total: {contagem} scripts ausentes removidos de {objetosAfetados} GameObjects.");
            EditorSceneManager.MarkAllScenesDirty();
        }
        else
        {
            Debug.Log("[CleanMissingScripts] ✓ Nenhum script ausente encontrado! Cena está limpa.");
        }
    }
    
    [MenuItem("Tools/🔍 Encontrar GameObjects com Scripts Ausentes")]
    static void EncontrarObjetosComScriptsAusentes()
    {
        GameObject[] objs = ObterGameObjectsDaCena();
        int encontrados = 0;
        
        Debug.Log("=== PROCURANDO SCRIPTS AUSENTES ===");
        
        foreach (GameObject obj in objs)
        {
            // Conta scripts ausentes sem removê-los
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj);
            if (count > 0)
            {
                encontrados++;
                Debug.LogWarning($"⚠️ Encontrados {count} scripts ausentes em: {obj.name} (Hierarquia: {GetGameObjectPath(obj)})", obj);
                
                // Destaca o objeto no hierarchy
                EditorGUIUtility.PingObject(obj);
            }
        }
        
        if (encontrados == 0)
        {
            Debug.Log("✓ Nenhum script ausente encontrado!");
        }
        else
        {
            Debug.LogWarning($"Total: {encontrados} GameObjects com scripts ausentes. Use 'Tools → Limpar Scripts Ausentes' para remover.");
        }
        
        Debug.Log("===================================");
    }
    
    /// <summary>
    /// Retorna o caminho completo de um GameObject na hierarquia
    /// </summary>
    static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }

    private static GameObject[] ObterGameObjectsDaCena()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<GameObject>();
#endif
    }
}
