using UnityEngine;
using Hegemonia.AI.Shared;
using UnityEngine.Events;

// 3. O SEGUNDO NÍVEL: O "ESTRATEGISTA"
// Este cara analisa o contexto de guerra.
public class Analista2 : MonoBehaviour
{
    private AnalistaExecutivo executor;
    
    // Variável simples de estado para substituir o Overlord complexo
    [Header("Percepção de Mundo")]
    public FactionState estadoPercebido = FactionState.Peace; 

    void Start()
    {
        executor = GetComponent<AnalistaExecutivo>();
    }

    public void ReceberValidado(TaskRequest pedido)
    {
        bool inteligente = AnalisarEstrategia(pedido);

        if (inteligente)
        {
            Debug.Log($"[Analista 2] Pedido {pedido.type} de {pedido.requester} APROVADO! Enviando para Execução.");
            executor.ExecutarFinal(pedido);
        }
        else
        {
            IAAutoProductionRegistry.Release(pedido.productionOrderId, Time.time);
        }
    }

    // A Mágica do Nível 2
    bool AnalisarEstrategia(TaskRequest pedido)
    {
        // 1. REJEITAR PRIORIDADE BAIXA EM EMERGÊNCIA
        if (estadoPercebido == FactionState.Emergency && pedido.priority != PriorityLevel.High && pedido.priority != PriorityLevel.Critical)
        {
            Debug.LogWarning("[Analista 2] Veto de Emergência! Só prioridade alta agora.");
            return false;
        }

        // 2. PRIORIDADE BASEADA NO INIMIGO (Exemplo)
        if (estadoPercebido == FactionState.War)
        {
            // Se o pedido é CONSTRUIR ECONÔMICO (Moinho, Mina) durante GUERRA -> Rejeita
            if (pedido.type == ActionType.ConstructBuilding && pedido.requester == "Economia")
            {
                 Debug.LogWarning("[Analista 2] Veto Estratégico: Não construir economia durante ataque!");
                 return false;
            }
        }
        
        return true;
    }
}
