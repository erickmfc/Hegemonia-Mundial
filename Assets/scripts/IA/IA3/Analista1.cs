using UnityEngine;
using UnityEngine.AI;

// 2. O PRIMEIRO NÍVEL DE ANÁLISE: "FILTRO DE POSSIBILIDADE"
// Este cara diz SIM ou NÃO para coisas básicas (custo, path, viabilidade).
public class Analista1 : MonoBehaviour
{
    private RecebedorIA recebedor;
    private Analista2 analistaEstrategia;
    
    // Conecta as peças
    void Start()
    {
        recebedor = GetComponent<RecebedorIA>();
        analistaEstrategia = GetComponent<Analista2>();
    }

    void Update()
    {
        // Se o Recebedor tem algo, eu puxo
        if (recebedor != null && recebedor.TemPedidos())
        {
            TaskRequest pedido = recebedor.PegarProximoPedido();
            bool aprovado = ValidarPedido(pedido);

            if (aprovado)
            {
                Debug.Log($"[Analista 1] Pedido {pedido.type} de {pedido.requester} aprovado! Passando para Estratégia.");
                analistaEstrategia.ReceberValidado(pedido);
            }
            else
            {
                Debug.Log($"[Analista 1] Pedido {pedido.type} negado! (Motivo: Inválido/Sem fundos)");
                // Aqui morre o pedido. Ninguém mais ouve falar dele.
            }
        }
    }

    // A Mágica do Filtro 1
    bool ValidarPedido(TaskRequest pedido)
    {
        // 1. CHECAGEM DE DINHEIRO
        float dinheiroAtual = 1000f; // Simulação. Usar GerenciadorRecursos real no futuro.
        if (pedido.cost > dinheiroAtual) 
        {
            Debug.LogWarning("[Analista 1] Sem dinheiro.");
            return false;
        }

        // 2. CHECAGEM FÍSICA (Pathfinding, Colisão)
        if (pedido.type == ActionType.ConstructBuilding || pedido.type == ActionType.MoveSquad)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(pedido.targetPosition, out hit, 10f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[Analista 1] Local inacessível no NavMesh.");
                return false; 
            }
        }

        // 3. PRIORIDADE
        // Se a prioridade for BAIXA e o caixa está apertado (< 500), bloqueia
        if (pedido.priority == PriorityLevel.Low && dinheiroAtual < 500f)
        {
             Debug.LogWarning("[Analista 1] Caixa baixo para prioridade baixa.");
             return false;
        }

        return true; 
    }
}
