using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// O coração da nação. A Prefeitura ou Complexo Governamental.
/// A destruição deste complexo deve resultar no fim do jogo (derrota para o jogador, ou eliminação da IA).
/// Será anexado ao prédio principal da cidade.
/// </summary>
public class ComplexoGovernamental : MonoBehaviour
{
    [Header("Informações do Estado")]
    public string nomeDoPais = "República Federativa";
    public bool ehDoJogador = true; // Se false, é o complexo central de um inimigo
    
    [Header("Sistema de Estado")]
    [Tooltip("Nível de Influência e Poder do Governo")]
    public int nivelDoGoverno = 1;

    [Header("Eventos")]
    public UnityEvent aoSerDestruido;
    public UnityEvent aoAbrirMenuGestao;

    private IdentidadeUnidade identidade;
    private SistemaDeDanos sistemaDano;
    private bool jaDerrotado = false;

    void Start()
    {
        identidade = GetComponent<IdentidadeUnidade>();
        sistemaDano = GetComponent<SistemaDeDanos>();

        // Registra automaticamente a identidade se houver
        if (identidade != null)
        {
            ehDoJogador = (identidade.teamID == 1);
        }
    }

    void Update()
    {
        // Segurança: Constantemente verifica se o prédio foi destruído (vida zerada).
        // Assim, se uma bomba cair e o SistemaDeDanos destruir o prédio, nós captamos isso.
        if (!jaDerrotado && sistemaDano != null && sistemaDano.vidaAtual <= 0)
        {
            DecretarQuedaDoGoverno();
        }
    }

    /// <summary>
    /// Detecta o clique do mouse no prédio 3D para abrir o Menu de Gestão de Estado.
    /// Futuramente, este menu permitirá controlar taxas de juros, diplomacia e decretos.
    /// </summary>
    void OnMouseDown()
    {
        // Evita abrir o menu do inimigo, a menos que tenhamos um modo "espião"
        if (ehDoJogador && !jaDerrotado)
        {
            AbrirMenuGestaoDeEstado();
        }
    }

    public void AbrirMenuGestaoDeEstado()
    {
        Debug.Log($"🏛️ [Complexo Governamental] Abrindo o painel central da {nomeDoPais}!");
        aoAbrirMenuGestao?.Invoke();
        
        // TODO: Chamar e exibir a UI Canvas do Gestor do Estado (em desenvolvimento).
        // Ex: MenuGestaoEstado.Instancia.AbrirPainel(this);
    }

    /// <summary>
    /// Método chamado no momento em que a vida do prédio chega a zero.
    /// Declara a perda do jogo se for o jogador, ou vitória local se for o inimigo.
    /// </summary>
    public void DecretarQuedaDoGoverno()
    {
        if (jaDerrotado) return;
        jaDerrotado = true;

        Debug.LogWarning($"💥⚠️ ALERTA MÁXIMO! O Complexo Governamental da {nomeDoPais} caiu! O governo ruiu!");
        aoSerDestruido?.Invoke();

        if (ehDoJogador)
        {
            // O jogador perdeu a capital. Encerra o jogo.
            Debug.LogError("GAME OVER: Você perdeu sua prefeitura principal!");
            // TODO: Integrar com a tela de Game Over do GerenteDeJogo.
        }
        else
        {
            // O jogador conquistou/destruiu a capital inimiga.
            Debug.Log("VITÓRIA: Um governo inimigo foi neutralizado!");
            // TODO: Adicionar bônus de recursos, anexação ou tela de vitória local.
        }
    }
}
