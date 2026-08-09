using UnityEngine;
using System.Collections.Generic;

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
    public string requester; 
    public ActionType type; 
    public Vector3 targetPosition; 
    public Quaternion targetRotation = Quaternion.identity; // Novo: Rotação específica
    public GameObject targetObject; 
    public float cost; 
    public PriorityLevel priority; 
    
    // Novo: Conexão direta com o Menu
    public DadosConstrucao menuItem; 

    public TaskRequest(string req, ActionType t, Vector3 pos, DadosConstrucao item = null, GameObject obj = null, float c = 0, PriorityLevel p = PriorityLevel.Low, Quaternion rot = default)
    {
        id = System.Guid.NewGuid().ToString();
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
        TaskRequest novoPedido = new TaskRequest(quemPediu, oQue, onde, null, oAlvo, custo, prioridade);
        filaDeEntrada.Enqueue(novoPedido);
        
        Debug.Log($"[Recebedor] Requisição recebida: {oQue} de {quemPediu}, custo {custo}. (Fila: {filaDeEntrada.Count})");
    }

    // Sobrecarga Inteligente: Recebe o item do menu direto!
    public void ReceberPedido(string quemPediu, ActionType oQue, Vector3 onde, DadosConstrucao itemMenu, PriorityLevel prioridade = PriorityLevel.Low, Quaternion rot = default)
    {
        TaskRequest novoPedido = new TaskRequest(quemPediu, oQue, onde, itemMenu, null, 0, prioridade, rot);
        filaDeEntrada.Enqueue(novoPedido);
        
        Debug.Log($"[Recebedor] Requisição INTELIGENTE: {itemMenu.NomeItem} ({oQue}) de {quemPediu}, Rot={rot.eulerAngles}. Custo: {itemMenu.preco}");
    }

    public TaskRequest PegarProximoPedido()
    {
        if (filaDeEntrada.Count > 0) return filaDeEntrada.Dequeue();
        return null;
    }

    public bool TemPedidos() => filaDeEntrada.Count > 0;
}
