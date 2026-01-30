using UnityEngine;

[System.Serializable]
public class IdentidadeIA : MonoBehaviour
{
    [Header("Perfil do Comandante")]
    public string nomeComandante = "General Desconhecido";
    [TextArea(2,5)]
    public string biografia = "Uma IA criada para testes militares.";
    
    [Header("Configuração de Time")]
    public int teamID = 2; // 2 = Padrão Inimigo
    public Color corDaNacao = Color.red;
    
    [Header("Parâmetros de Personalidade")]
    [Range(0, 10)] public int agressividade = 5;
    [Range(0, 10)] public int inteligenciaEconomica = 5;
    [Range(0, 10)] public int expansaoTerritorial = 5;
    
    [Header("Status de Jogo")]
    public bool estaAtivo = true;
    public bool eliminado = false;

    [Header("Alvos Prioritários (Futuro)")]
    // Aqui reservamos espaço para o sistema de alvo que você pediu pra depois
    public Transform alvoPrincipal; 
    public Transform liderInimigo;

    void Start()
    {
        // Ao nascer, tenta se registrar no Gerente de Jogo
        if (GerenteDeJogo.Instancia != null)
        {
            GerenteDeJogo.Instancia.RegistrarJogadorIA(this);
        }
    }
}
