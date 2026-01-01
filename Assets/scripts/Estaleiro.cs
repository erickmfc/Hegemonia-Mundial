using UnityEngine;
using UnityEngine.AI; // Necessário para mexer no NavMesh

public class Estaleiro : MonoBehaviour
{
    [Header("Pontos de Logística")]
    public Transform pontoDeNascimento; // Onde o navio aparece (Boca do hangar)
    public Transform pontoDeSaida;      // Para onde ele vai (Meio do mar)

    [Header("Configuração")]
    public float tempoParaSair = 1.0f; // Espera um pouquinho antes de andar

    [Header("TESTE - Remover depois")]
    public GameObject navioDeTeste; // Arraste a corveta aqui no Inspector para testar

    void Update()
    {
        // TEMPORÁRIO: Aperte T para testar a construção
        if (Input.GetKeyDown(KeyCode.T) && navioDeTeste != null)
        {
            Debug.Log("[Estaleiro] TESTE: Construindo navio com tecla T...");
            ConstruirUnidade(navioDeTeste);
        }
    }

    // Essa função será chamada pelo seu GerenteDeJogo ou Botão de UI
    public void ConstruirUnidade(GameObject prefabDoNavio)
    {
        // 1. Cria o navio na posição e rotação do Spawn
        GameObject novoNavio = Instantiate(prefabDoNavio, pontoDeNascimento.position, pontoDeNascimento.rotation);

        // 2. Dá o comando para ele sair da doca
        // Usamos uma "Corrotina" ou Invoke para garantir que o NavMeshAgent inicializou
        StartCoroutine(MoverParaSaida(novoNavio));
    }

    System.Collections.IEnumerator MoverParaSaida(GameObject navio)
    {
        // Espera um frame para o Unity registrar o navio no mundo
        yield return null; 

        NavMeshAgent agente = navio.GetComponent<NavMeshAgent>();

        if (agente != null)
        {
            // Garante que o navio está ativo e no NavMesh
            if (agente.isOnNavMesh)
            {
                agente.SetDestination(pontoDeSaida.position);
                Debug.Log("Navio lançado ao mar! Destino definido.");
            }
            else
            {
                Debug.LogWarning("O navio nasceu fora do NavMesh (água roxa)!");
            }
        }
        else
        {
            Debug.LogError("ERRO: O prefab do navio não tem NavMeshAgent!");
        }
    }
}
