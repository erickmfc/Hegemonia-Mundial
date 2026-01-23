using UnityEngine;

public class GerenciadorDePartida : MonoBehaviour
{
    public static GerenciadorDePartida Instancia;
    
    [Header("Definição dos Times")]
    [Tooltip("ID do Time do Jogador Humano")]
    public int idJogador = 1;
    
    [Tooltip("ID do Time da Inteligência Artificial")]
    public int idIA = 2;

    [Header("Estado da Partida")]
    public bool partidaEmAndamento = true;

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }
    
    void Start()
    {
        ConfigurarPartida();
    }

    public void ConfigurarPartida()
    {
        Debug.Log("[Gerenciador] Partida iniciada.");
        // Configurações futuras de jogo podem vir aqui
    }

    Transform BuscarAlvoDoJogador()
    {
        // Tenta achar qualquer coisa com ID do Jogador
        var todasUnidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach(var u in todasUnidades)
        {
            if (u.teamID == idJogador) return u.transform;
        }
        
        // Fallback: Procura Tag "Player"
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) return playerObj.transform;

        return null;
    }

    // --- SISTEMA CENTRAL DE JULGAMENTO (Quem é inimigo de quem?) ---
    
    /// <summary>
    /// Retorna TRUE se 'observador' deve considerar 'alvo' como inimigo.
    /// </summary>
    public bool EhInimigo(GameObject observador, GameObject alvo)
    {
        // 1. Verificação por Identidade (RG) - O método mais confiável
        var idObservador = observador.GetComponentInParent<IdentidadeUnidade>();
        var idAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();

        if (idObservador != null && idAlvo != null)
        {
            // Se têm times diferentes, são inimigos!
            return idObservador.teamID != idAlvo.teamID;
        }

        // 2. Fallback: Verificação por Tags (Legado)
        string tagObs = observador.tag;
        string tagAlvo = alvo.tag;

        if (tagObs == "Inimigo" && tagAlvo == "Player") return true;
        if (tagObs == "Player" && tagAlvo == "Inimigo") return true;

        return false;
    }
}
