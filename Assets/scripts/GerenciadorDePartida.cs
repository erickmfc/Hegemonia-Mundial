using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.RTS;

public class GerenciadorDePartida : MonoBehaviour
{
    public static GerenciadorDePartida Instancia;

    [Header("Definicao dos Times")]
    [Tooltip("ID do Time do Jogador Humano")]
    public int idJogador = 1;

    [Tooltip("ID do Time da Inteligencia Artificial")]
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
        SanitizarCenaDePartidaNova();
        if (SistemaSaveGame.Instancia != null && SistemaSaveGame.Instancia.carregouDeSave)
        {
            RTSGameSession.Instancia?.EnterGameplay(idJogador, idIA, 1);
        }
        else
        {
            RTSGameSession.Instancia?.BeginGameplay(idJogador, idIA, 1);
        }
        Debug.Log("[Gerenciador] Partida iniciada.");
        // Configuracoes futuras de jogo podem vir aqui
    }

    private void SanitizarCenaDePartidaNova()
    {
        SistemaSaveGame sistemaSave = SistemaSaveGame.Instancia;
        if (sistemaSave == null || sistemaSave.carregouDeSave || !sistemaSave.partidaNovaRecemIniciada)
        {
            return;
        }

        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        int unidadesRemovidas = 0;
        int comandantesRemovidos = 0;

        IdentidadeUnidade[] unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            IdentidadeUnidade unidade = unidades[i];
            if (unidade == null || unidade.tipoUnidade == TipoUnidade.Estrutura || unidade.teamID <= 0)
            {
                continue;
            }

            Destroy(unidade.gameObject);
            unidadesRemovidas++;
        }

        IdentidadeIA[] identidadesIA = FindObjectsByType<IdentidadeIA>(FindObjectsSortMode.None);
        for (int i = 0; i < identidadesIA.Length; i++)
        {
            IdentidadeIA identidade = identidadesIA[i];
            if (identidade == null)
            {
                continue;
            }

            bool pareceComandanteDeTeste =
                (!string.IsNullOrWhiteSpace(identidade.biografia) && identidade.biografia.Contains("testes militares"))
                || identidade.name.StartsWith("IA_", System.StringComparison.OrdinalIgnoreCase)
                || identidade.teamID > 1;

            if (!pareceComandanteDeTeste)
            {
                continue;
            }

            Destroy(identidade.gameObject);
            comandantesRemovidos++;
        }

        sistemaSave.ConsumirMarcadorPartidaNova();
        Debug.Log("[Gerenciador] Sanitizacao de partida nova concluida. Unidades removidas=" + unidadesRemovidas + " | comandantes removidos=" + comandantesRemovidos + ".");
    }

    Transform BuscarAlvoDoJogador()
    {
        var todasUnidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todasUnidades)
        {
            if (u.teamID == idJogador) return u.transform;
        }

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) return playerObj.transform;

        return null;
    }

    public bool EhInimigo(GameObject observador, GameObject alvo)
    {
        var idObservador = observador.GetComponentInParent<IdentidadeUnidade>();
        var idAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();

        if (idObservador != null && idAlvo != null)
        {
            return idObservador.teamID != idAlvo.teamID;
        }

        string tagObs = observador.tag;
        string tagAlvo = alvo.tag;

        if (tagObs == "Inimigo" && tagAlvo == "Player") return true;
        if (tagObs == "Player" && tagAlvo == "Inimigo") return true;

        return false;
    }
}
