using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Ferramenta de limpeza para remover referências de scripts ausentes
/// </summary>
public class CleanMissingScripts : MonoBehaviour
{
    [MenuItem("Tools/🧹 Limpar Scripts Ausentes na Cena")]
    static void LimparScriptsAusentesNaCena()
    {
        GameObject[] objs = FindObjectsOfType<GameObject>();
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
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        else
        {
            Debug.Log("[CleanMissingScripts] ✓ Nenhum script ausente encontrado! Cena está limpa.");
        }
    }
    
    [MenuItem("Tools/🔍 Encontrar GameObjects com Scripts Ausentes")]
    static void EncontrarObjetosComScriptsAusentes()
    {
        GameObject[] objs = FindObjectsOfType<GameObject>();
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
}
