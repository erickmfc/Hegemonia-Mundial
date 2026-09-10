using UnityEngine;

/// <summary>
/// Fazenda passiva. Ao ser construída, registra a estrutura econômica e
/// inicia uma produção automática de alimentos sem menu, painel ou clique de
/// gerenciamento.
/// </summary>
[DisallowMultipleComponent]
public sealed class Fazenda : MonoBehaviour
{
    // Mantidos para compatibilidade com câmera, seleção e saves antigos. Uma
    // fazenda nunca abre interface e estes valores permanecem sempre falsos.
    public static Fazenda FazendaAtiva;
    public static bool QualquerFazendaAberta = false;

    [Header("Identificação")]
    public string nomeFazenda = "Fazenda Nacional";
    public bool mostrarLogs = false;

    private EstruturaEconomica estrutura;
    private ProducaoAutomaticaEdificio producao;

    private void Awake()
    {
        if (Construtor.CriandoPreviewConstrucao)
        {
            enabled = false;
            return;
        }

        GarantirEstruturaEconomica();
        producao = ProducaoAutomaticaEdificio.Garantir(gameObject, ProducaoAutomaticaEdificio.TipoInstalacao.Fazenda);
        FazendaAtiva = null;
        QualquerFazendaAberta = false;
    }

    private void OnDestroy()
    {
        if (FazendaAtiva == this) FazendaAtiva = null;
        QualquerFazendaAberta = false;
    }

    /// <summary>Fazendas não capturam cliques; o mapa continua selecionável.</summary>
    public static bool CliqueCapturadoPeloMenu() => false;

    // APIs legadas viram operações vazias para não quebrar prefabs ou saves
    // antigos que ainda contenham referências ao controlador anterior.
    public void FecharMenu() { }
    internal void EncerrarEstadoDoMenu() { }
    public bool MenuAberto => false;
    public string NomeFazendaExibicao => string.IsNullOrWhiteSpace(nomeFazenda) ? "Fazenda Nacional" : nomeFazenda;
    public float ComidaPorSegundoAtual => 0f;
    public int CatalogoAgricolaCount => 0;

    private void GarantirEstruturaEconomica()
    {
        estrutura = GetComponent<EstruturaEconomica>();
        if (estrutura == null) estrutura = gameObject.AddComponent<EstruturaEconomica>();

        estrutura.tipo = TipoEstruturaEconomica.Farm;
        estrutura.InferirTeamId();
        estrutura.AplicarPadraoPorTipo();
    }
}
