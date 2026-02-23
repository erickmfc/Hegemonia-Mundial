using UnityEngine;
using UnityEngine.AI;

// 2. O PRIMEIRO NÍVEL DE ANÁLISE: "FILTRO DE POSSIBILIDADE"
// Este cara diz SIM ou NÃO para coisas básicas (path, viabilidade física).
public class Analista1 : MonoBehaviour
{
    private RecebedorIA recebedor;
    private Analista2 analistaEstrategia;
    private CerebroIA cerebro;

    // Conecta as peças
    void Start()
    {
        recebedor = GetComponent<RecebedorIA>();
        analistaEstrategia = GetComponent<Analista2>();
        cerebro = GetComponent<CerebroIA>();
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
                // Se foi negado, devolve o dinheiro?? 
                // COMPLEXIDADE: Se o Cerebro já pagou e foi negado aqui, o dinheiro sumiu.
                // Idealmente deveríamos estornar.
                if (cerebro != null && pedido.cost > 0)
                {
                    cerebro.recursosIA += pedido.cost;
                    Debug.Log($"[Analista 1] Estornando {pedido.cost} para a IA por pedido cancelado.");
                }

                Debug.Log($"[Analista 1] Pedido {pedido.type} negado! (Motivo: Inválido)");
            }
        }
    }

    // A Mágica do Filtro 1
    bool ValidarPedido(TaskRequest pedido)
    {
        // 1. CHECAGEM DE DINHEIRO REAL (Apenas informativo ou para prioridades baixas)
        float dinheiroAtual = (cerebro != null) ? cerebro.recursosIA : 0f;

        // NÃO bloqueamos por falta de fundo aqui, pois o Cerebro já descontou.
        // A menos que o Cerebro tenha permitido 'fiado' (saldo negativo), o que seria um bug lá.

        // 2. CHECAGEM FÍSICA (Pathfinding, Colisão)
        if (pedido.type == ActionType.ConstructBuilding || pedido.type == ActionType.MoveSquad)
        {
            // Verifica se a posição alvo é válida no NavMesh
            NavMeshHit hit;
            // Aumentei o raio para 20f para facilitar em terrenos irregulares
            if (!NavMesh.SamplePosition(pedido.targetPosition, out hit, 20f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Analista 1] Local {pedido.targetPosition} inacessível no NavMesh.");
                return false; 
            }
        }

        // 3. PRIORIDADE
        // Se a prioridade for BAIXA e estamos pobres, talvez devêssemos guardar o dinheiro?
        // Mas como já foi pago, negar agora só gera estorno. Melhor deixar passar.
        
        return true; 
    }
}
