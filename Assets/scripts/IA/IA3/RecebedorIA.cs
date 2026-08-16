using UnityEngine;
using System.Collections.Generic;
using Hegemonia.AI.IA01;
using Hegemonia.AI.Shared;

// --- DEFINIÇÕES GLOBAIS (ENUMS E CLASSES DE DADOS) ---
// Agora vivem aqui para evitar arquivos soltos

public enum FactionState
{
    Peace,
    Expansion,
    War,
    Emergency
}

public enum PriorityLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum ActionType
{
    ConstructBuilding,
    RecruitUnit,
    MoveSquad,
    AttackTarget,
    Retreat
}

public enum UnitRole
{
    Tank,
    Soldier,
    Artillery,
    Scout
}

[System.Serializable]
public class TaskRequest
{
    public string id; 
    public string productionOrderId;
    public string requester; 
    public ActionType type; 
    public Vector3 targetPosition; 
    public Quaternion targetRotation = Quaternion.identity; // Novo: Rotação específica
    public GameObject targetObject; 
    public float cost; 
    public PriorityLevel priority; 
    
    // Novo: Conexão direta com o Menu
    public DadosConstrucao menuItem; 

    public TaskRequest(string req, ActionType t, Vector3 pos, DadosConstrucao item = null, GameObject obj = null, float c = 0, PriorityLevel p = PriorityLevel.Low, Quaternion rot = default, string orderId = "")
    {
        id = System.Guid.NewGuid().ToString();
        productionOrderId = orderId ?? string.Empty;
        requester = req;
        type = t;
        targetPosition = pos;
        targetRotation = (rot == default) ? Quaternion.identity : rot;
        
        if (item != null)
        {
            menuItem = item;
            targetObject = item.PrefabDaUnidade;
            cost = item.preco;
        }
        else 
        {
            targetObject = obj;
            cost = c;
        }
        
        priority = p;
    }
}

// --- O SCRIPT PRINCIPAL DO RECEBEDOR ---

public class RecebedorIA : MonoBehaviour
{
    [Header("Scripts e Missões (Alimente Aqui!)")]
    [Tooltip("Arraste aqui os scripts (.asset) que você quer que a IA processe")]
    public List<ScriptMissao> scriptsDeMissao = new List<ScriptMissao>();

    [Header("Pedidos Chegando (Fila Interna)")]
    private Queue<TaskRequest> filaDeEntrada = new Queue<TaskRequest>();

    // Processa os scripts da lista automaticamente
    void Start()
    {
        ProcessarListaDeScripts();
    }

    void ProcessarListaDeScripts()
    {
        foreach (var script in scriptsDeMissao)
        {
            if (script != null)
            {
                // Converte o ScriptableObject em um Pedido Interno
                ReceberPedido(
                    script.solicitante,
                    script.tipoAcao,
                    script.posicaoAlvo,
                    script.objetoAlvo,
                    script.custoEstimado,
                    script.prioridade
                );
            }
        }
    }

    // Método Público para outros códigos injetarem pedidos diretamente
    public void ReceberPedido(string quemPediu, ActionType oQue, Vector3 onde, GameObject oAlvo = null, float custo = 0, PriorityLevel prioridade = PriorityLevel.Low)
    {
        string orderId = string.Empty;
        if (oQue == ActionType.RecruitUnit)
        {
            IdentidadeIA identity = GetComponent<IdentidadeIA>();
            int teamId = identity != null ? identity.teamID : 0;
            string unitType = oAlvo != null ? oAlvo.name.Trim().ToLowerInvariant().Replace(" ", "_") : string.Empty;
            if (teamId <= 0 || string.IsNullOrEmpty(unitType)
                || !IAAutoProductionRegistry.TryReserveProduction(teamId, unitType, "ia3", 1, 0, out orderId, Time.time, 180f))
            {
                return;
            }
        }

        TaskRequest novoPedido = new TaskRequest(quemPediu, oQue, onde, null, oAlvo, custo, prioridade, default, orderId);
        filaDeEntrada.Enqueue(novoPedido);
        
        Debug.Log($"[Recebedor] Requisição recebida: {oQue} de {quemPediu}, custo {custo}. (Fila: {filaDeEntrada.Count})");
    }

    // Sobrecarga Inteligente: Recebe o item do menu direto!
    public void ReceberPedido(string quemPediu, ActionType oQue, Vector3 onde, DadosConstrucao itemMenu, PriorityLevel prioridade = PriorityLevel.Low, Quaternion rot = default, string productionOrderId = "")
    {
        string orderId = productionOrderId;
        if (oQue == ActionType.RecruitUnit && string.IsNullOrWhiteSpace(orderId))
        {
            if (!TryReserveRecruitment(itemMenu, out orderId)) return;
        }

        TaskRequest novoPedido = new TaskRequest(quemPediu, oQue, onde, itemMenu, null, 0, prioridade, rot, orderId);
        filaDeEntrada.Enqueue(novoPedido);
        
        Debug.Log($"[Recebedor] Requisição INTELIGENTE: {itemMenu.NomeItem} ({oQue}) de {quemPediu}, Rot={rot.eulerAngles}. Custo: {itemMenu.preco}");
    }

    public TaskRequest PegarProximoPedido()
    {
        if (filaDeEntrada.Count > 0) return filaDeEntrada.Dequeue();
        return null;
    }

    public bool TemPedidos() => filaDeEntrada.Count > 0;

    private bool TryReserveRecruitment(DadosConstrucao item, out string orderId)
    {
        orderId = string.Empty;
        IdentidadeIA identity = GetComponent<IdentidadeIA>();
        int teamId = identity != null ? identity.teamID : 0;
        if (item == null || teamId <= 0) return false;

        IA01MilitaryAssetKind kind = IA01MilitaryProductionGuard.Classify(item);
        string unitType = kind == IA01MilitaryAssetKind.Other ? item.GetStableId() : kind.ToString();
        int alive = CountOwnedForProduction(teamId, kind);
        return IAAutoProductionRegistry.TryReserveProduction(teamId, unitType, "ia3", alive + 1, alive, out orderId, Time.time, 180f);
    }

    private static int CountOwnedForProduction(int teamId, IA01MilitaryAssetKind kind)
    {
        switch (kind)
        {
            case IA01MilitaryAssetKind.Infantry:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Infantaria);
            case IA01MilitaryAssetKind.Tank:
            case IA01MilitaryAssetKind.AntiAir:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Veiculo);
            case IA01MilitaryAssetKind.Fighter:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Aereo);
            case IA01MilitaryAssetKind.Naval:
            case IA01MilitaryAssetKind.OilTanker:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Naval);
            default:
                return 0;
        }
    }
}
