using UnityEngine;

// Este é o "Script" que você vai criar no projeto e arrastar para o Recebedor
[CreateAssetMenu(fileName = "NovoScriptMissao", menuName = "Hegemonia/IA/Script de Missao")]
public class ScriptMissao : ScriptableObject
{
    [Header("Identificação")]
    public string nomeDaMissao = "Missão Padrão";
    public string solicitante = "Comando Central";

    [Header("Ação a Realizar")]
    public ActionType tipoAcao; // Construir, Atacar, Recrutar (do GlobalAIEnums)
    public PriorityLevel prioridade = PriorityLevel.Medium;
    
    [Header("Detalhes do Alvo")]
    public Vector3 posicaoAlvo; // Se for fixo
    public GameObject objetoAlvo; // Se for para construir algo específico
    public float custoEstimado = 100f;

    [Header("Condições (Opcional)")]
    public bool executarImediatamente = false;
}
