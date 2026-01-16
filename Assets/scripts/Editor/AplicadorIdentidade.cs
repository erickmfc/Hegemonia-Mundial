using UnityEngine;
using UnityEditor; // Necessário para criar menus no Unity

public class AplicadorIdentidade : EditorWindow
{
    [MenuItem("Hegemonia/Aplicar RG (Identidade) em Tudo")]
    public static void AplicarRG()
    {
        // Encontra todos os Prefabs do projeto que são GameObjects
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        
        int contagem = 0;

        foreach (string guid in guids)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);

            if (prefab != null)
            {
                // Verifica se o objeto parece uma unidade (tem colisor ou navmesh)
                // Se quiser forçar em TUDO, pode remover o 'if' abaixo
                if (prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null || 
                    prefab.GetComponent<Rigidbody>() != null)
                {
                    // Verifica se já tem o RG. Se não tiver, adiciona.
                    if (prefab.GetComponent<IdentidadeUnidade>() == null)
                    {
                        prefab.AddComponent<IdentidadeUnidade>();
                        Debug.Log("RG aplicado em: " + prefab.name);
                        contagem++;
                        
                        // Salva a alteração no arquivo
                        EditorUtility.SetDirty(prefab);
                    }
                }
            }
        }
        
        Debug.Log($"<color=green>Sucesso! RG aplicado em {contagem} novas unidades.</color>");
    }
}
