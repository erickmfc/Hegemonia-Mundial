using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Interface administrativa do Quartel em UI Toolkit, alimentada por estado de jogo.
///
/// Este componente nao assume o controle de unidades, ordens ou selecao. Ele
/// apenas apresenta os dados do GerenciadorQuartel e encaminha acoes para a
/// API publica do gerenciador. O IMGUI antigo continua disponivel quando
/// usarPainelQuartelUIToolkit estiver desativado.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuartelMenuUIController : MonoBehaviour
{
    // Paleta única do Quartel: marinho profundo, ciano operacional e verde
    // apenas para indicar prontidão. O dourado fica reservado a avisos e não
    // compete com os comandos clicáveis.
    private static readonly Color CorFundoQuartel = new Color(0.004f, 0.012f, 0.020f, 0.96f);
    private static readonly Color CorPainelQuartel = new Color(0.012f, 0.035f, 0.052f, 0.99f);
    private static readonly Color CorCabecalhoQuartel = new Color(0.018f, 0.075f, 0.105f, 1f);
    private static readonly Color CorLateralQuartel = new Color(0.006f, 0.025f, 0.040f, 1f);
    private static readonly Color CorCartaoQuartel = new Color(0.018f, 0.075f, 0.105f, 0.98f);
    private static readonly Color CorNavegacaoQuartel = new Color(0.008f, 0.043f, 0.068f, 1f);
    private static readonly Color CorNavegacaoAtivaQuartel = new Color(0.015f, 0.205f, 0.290f, 1f);
    private static readonly Color CorBotaoQuartel = new Color(0.020f, 0.235f, 0.310f, 1f);
    private static readonly Color CorBordaQuartel = new Color(0.080f, 0.370f, 0.470f, 0.90f);
    private static readonly Color CorBadgeIconeQuartel = new Color(0.015f, 0.125f, 0.175f, 1f);
    private static readonly Color CorBadgeIconeAtivoQuartel = new Color(0.02f, 0.34f, 0.45f, 1f);
    private static readonly Color CorTextoQuartel = new Color(0.86f, 0.94f, 0.97f, 1f);
    private static readonly Color CorTextoSecundarioQuartel = new Color(0.42f, 0.69f, 0.76f, 1f);
    private static readonly Color CorCianoQuartel = new Color(0.12f, 0.79f, 0.98f, 1f);
    private static readonly Color CorVerdeQuartel = new Color(0.35f, 0.92f, 0.42f, 1f);
    private static readonly Color CorAlertaQuartel = new Color(1f, 0.68f, 0.20f, 1f);

    // A carta e seus controles permanecem em cache durante a aba ativa.
    private static QuartelMenuUIController painelAberto;
    private static int ultimoFrameEntradaConsumida = -1;

    private GerenciadorQuartel quartel;
    private QuartelAdministracaoRuntime administracao;
    private QuartelAdministracaoRuntime.Snapshot snapshot;
    private UIDocument documento;
    private PanelSettings panelSettingsRuntime;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement painel;
    private VisualElement conteudo;
    private VisualElement carta;
    private CartaTerrenoRenderer cartaTerrenoRenderer;
    private QuartelCartaTopograficaView cartaTopograficaView;
    private string cartaUnidadeSelecionadaId = string.Empty;
    private string cartaMissilSelecionadoId = string.Empty;
    private string cartaContatoSelecionadoId = string.Empty;
    private bool cartaVista3D;
    private int paginaConstruida = -1;
    private bool cartaPersistenteConstruida;
    private VisualElement mapaCartaPersistente;
    private VisualElement camadaMarcadoresCarta;
    private VisualElement camadaTrajetoriasCarta;
    private VisualElement telemetriaCartaPersistente;
    private Label escalaCartaPersistente;
    private Label tituloCartaPersistente;
    private Button botaoCarta2D;
    private Button botaoCarta3D;
    private readonly Dictionary<string, Button> marcadoresCarta = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, VisualElement> trajetoriasCarta = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
    private readonly Dictionary<string, VisualElement> trajetoriasPercorridasCarta = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
    private readonly Dictionary<string, VisualElement> trajetoriasEstimadasCarta = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> marcadoresTrajetoriaCarta = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> botoesContatosLancamento = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, VisualElement> linhasUnidadesLancamento = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> botoesUnidadesLancamento = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> botoesModoUnidadesLancamento = new Dictionary<string, Button>(StringComparer.Ordinal);
    private VisualElement painelLancamentoPersistente;
    private VisualElement contatosLancamentoPersistente;
    private VisualElement contatosLancamentoListaPersistente;
    private Label contatosLancamentoVazio;
    private VisualElement unidadesLancamentoPersistente;
    private VisualElement unidadesLancamentoListaPersistente;
    private Label unidadesLancamentoVazio;
    private VisualElement validacaoLancamentoPersistente;
    private VisualElement estatisticasLancamentoPersistentes;
    private Button botaoModoManualLancamento;
    private Button botaoModoAutomaticoLancamento;
    private Button botaoConfirmarLancamento;
    private bool telemetriaCartaEstruturaConstruida;
    private VisualElement dadosTelemetriaCartaPersistentes;
    private VisualElement controlesTelemetriaCartaPersistentes;
    private Label tituloTelemetriaCartaPersistente;
    private Label textoTelemetriaCartaPersistente;
    private Button botaoSelecionarLancadorTelemetria;
    private Button botaoUsarCoordenadasTelemetria;
    private VisualElement listaAeronavesCartaPersistente;
    private Label listaAeronavesCartaVazia;
    private readonly Dictionary<string, Button> botoesAeronavesCarta = new Dictionary<string, Button>(StringComparer.Ordinal);
    private Button botaoRastrearMissilCarta;
    private Button botaoPararRastreamentoMissilCarta;
    private Label statusLancamentoCarta;
    private Label resumoDestruicoesCarta;
    private VisualElement listaEventosCombateCarta;
    private readonly List<CartaCombateRegistro.EventoCombate> eventosCombateCarta = new List<CartaCombateRegistro.EventoCombate>(128);
    private readonly Dictionary<string, Button> botoesEventosCombateCarta = new Dictionary<string, Button>(StringComparer.Ordinal);
    private VisualElement camadaInteracaoCarta;
    private bool cartaToolkitArrastando;
    private bool cartaToolkitFoiArrastada;
    private int cartaToolkitPointerId = -1;
    private Vector2 cartaToolkitUltimoPonto;
    private bool cliqueTerrenoArmado;
    private readonly List<string> idsMarcadoresCartaPresentes = new List<string>(256);
    private TextField campoCoordenadaX;
    private TextField campoCoordenadaY;
    private TextField campoCoordenadaZ;
    private string coordenadaXFallback = "0";
    private string coordenadaYFallback = "0";
    private string coordenadaZFallback = "0";
    private string statusLancamentoFallback = string.Empty;
    private Vector2 scrollEventosCombateFallback;
    private float limiteTelemetriaCartaFallback = -1f;
    private int frameAbertura;
    private bool fallbackRendererDecidido;
    private bool usarFallbackRenderer;
    private bool arrastandoCartaFallback;
    private bool cartaFallbackFoiArrastada;
    private Vector2 ultimaPosicaoCartaFallback;
    private Label titulo;
    private Label subtitulo;
    private Label status;
    private Label metricas;
    private readonly List<Button> botoesAbas = new List<Button>();
    // Em algumas Game Views embutidas o InputSystemUIInputModule move o
    // cursor, mas nao despacha o ClickEvent do UI Toolkit. Mantemos o mesmo
    // comando em um registro de botoes para que a rota de fallback possa
    // executar a acao real sem criar uma segunda camada visual.
    private readonly Dictionary<Button, Action> acoesBotoesRuntime = new Dictionary<Button, Action>();
    private Button ultimoBotaoExecutado;
    private int ultimoFrameBotaoExecutado = -1;
    private readonly string[] nomesAbas =
    {
        "TROPAS", "EFETIVO", "RECRUTAMENTO", "FOLHA MILITAR", "TRIPULACOES",
        "RESGATE", "COMUNICACOES", "CARTA NAUTICA", "ARSENAL"
    };

    private int abaAtual;
    private int quantidadeRecrutamentoUI = 1;
    private bool aberto;
    private bool pronto;
    private float proximaAtualizacao;
    private readonly List<IdentidadeUnidade> unidadesRegistradas = new List<IdentidadeUnidade>(256);
    private readonly List<ControleUnidade> controlesRegistrados = new List<ControleUnidade>(256);
    private readonly List<ControleAviao> avioesRegistrados = new List<ControleAviao>(64);

    private GUIStyle designerFundo;
    private GUIStyle designerPainel;
    private GUIStyle designerCabecalho;
    private GUIStyle designerBarraLateral;
    private GUIStyle designerMarca;
    private GUIStyle designerSubmarca;
    private GUIStyle designerIcone;
    private GUIStyle designerTitulo;
    private GUIStyle designerSubtitulo;
    private GUIStyle designerRotulo;
    private GUIStyle designerValor;
    private GUIStyle designerSecao;
    private GUIStyle designerCartao;
    private GUIStyle designerNavegacao;
    private GUIStyle designerNavegacaoAtiva;
    private GUIStyle designerBotao;
    private GUIStyle designerBotaoAtivo;
    private GUIStyle designerBotaoCompacto;
    private GUIStyle designerBotaoLista;
    private GUIStyle designerMapa;
    private GUIStyle designerGrade;
    private GUIStyle designerStatus;
    private GUIStyle designerStatusAlerta;
    private GUIStyle designerPequeno;
    private Texture2D texturaFundo;
    private Texture2D texturaPainel;
    private Texture2D texturaCabecalho;
    private Texture2D texturaLateral;
    private Texture2D texturaCartao;
    private Texture2D texturaNavegacao;
    private Texture2D texturaNavegacaoAtiva;
    private Texture2D texturaBotao;
    private Texture2D texturaMapa;
    private Texture2D texturaGrade;
    private bool designerInicializado;

    private readonly string[] nomesNavegacaoDesigner =
    {
        "TROPAS", "EFETIVO MILITAR", "RECRUTAMENTO E FORMACAO", "FOLHA MILITAR",
        "TRIPULACOES", "RESGATES", "COMUNICACAO E INTERCEPTACAO", "CARTA NAUTICA", "ARSENAL"
    };

    private readonly string[] descricoesNavegacaoDesigner =
    {
        "Unidades armazenadas, aeronaves conectadas e acoes de desdobramento.",
        "Quadro nacional de efetivo e disponibilidade administrativa.",
        "Alistamento, treinamento e distribuicao para as forcas corretas.",
        "Folha, pagamento e custo real do pessoal administrado.",
        "Tripulacao minima, alocacao ativa e unidades inoperantes.",
        "Alertas de dano, perdas e protocolo de recuperacao.",
        "Contatos, sensores e retransmissao de informacoes.",
        "Leitura operacional da cobertura e dos contatos conhecidos.",
        "Misseis, municao e saldo do GerenciadorRecursos."
    };

    public bool EstaAberto => aberto;
    public bool EstaVisivel => aberto && root != null && root.style.display == DisplayStyle.Flex;

    /// <summary>
    /// Bloqueio modal centralizado do Quartel. O menu visual usa um painel
    /// separado, mas os controladores legados ainda podem ler Input direto;
    /// por isso eles consultam esta propriedade antes de abrir outro menu ou
    /// executar uma acao no mundo.
    /// </summary>
    public static bool EntradaGlobalBloqueada
    {
        get
        {
            bool quartelAberto = painelAberto != null
                && painelAberto.isActiveAndEnabled
                && painelAberto.aberto
                && painelAberto.root != null
                && painelAberto.root.style.display != DisplayStyle.None;
            bool gerenciadorAberto = GerenciadorQuartel.InterfaceAberta;
            bool acabouDeFechar = ultimoFrameEntradaConsumida >= 0
                && Time.frameCount <= ultimoFrameEntradaConsumida + 1;

            if (!quartelAberto && !gerenciadorAberto && !acabouDeFechar)
            {
                // Um fechamento antigo não pode bloquear a cena inteira.
                ultimoFrameEntradaConsumida = -1;
                return false;
            }

            return quartelAberto || gerenciadorAberto || acabouDeFechar;
        }
    }

    public static bool ExisteModalAberto
    {
        get { return painelAberto != null && painelAberto.aberto; }
    }

    private bool PrecisaFallbackIMGUI()
    {
        if (fallbackRendererDecidido) return usarFallbackRenderer;
        if (root == null)
        {
            fallbackRendererDecidido = true;
            usarFallbackRenderer = true;
            return true;
        }

        // O root pode ter layout NaN no primeiro OnGUI enquanto o
        // PanelSettings ainda calcula o viewport. Isso não significa que o
        // documento UI Toolkit falhou. Quando há Panel ativo, ele já é a
        // interface oficial e deve ser a única camada visual; decidir IMGUI
        // nesse instante fazia a demo1 desenhar duas camadas concorrentes.
        if (root.panel != null && documento != null && documento.enabled)
        {
            fallbackRendererDecidido = true;
            usarFallbackRenderer = false;
            return false;
        }

        float largura = root.resolvedStyle.width;
        float altura = root.resolvedStyle.height;
        bool dimensaoInvalida = float.IsNaN(largura) || float.IsNaN(altura) || largura < 1f || altura < 1f;
        // Decide uma única vez por abertura. Alternar entre UI Toolkit e
        // IMGUI enquanto o PanelSettings ainda calcula o layout era a causa
        // do pisca visível no mapa.
        fallbackRendererDecidido = true;
        usarFallbackRenderer = dimensaoInvalida;
        return usarFallbackRenderer;
    }

    private void DimensionarRootDocumento()
    {
        if (root == null) return;
        // O root do UIDocument já é o elemento que ocupa o PanelSettings.
        // Forçá-lo como Absolute antes do primeiro layout deixa o
        // resolvedStyle em NaN em alguns Game Views embutidos e faz o
        // painel existir no log, mas não ser desenhado. Mantemos o root no
        // fluxo natural do painel e deixamos o overlay preencher esse root.
        root.style.position = Position.Relative;
        root.style.left = StyleKeyword.Auto;
        root.style.top = StyleKeyword.Auto;
        root.style.right = StyleKeyword.Auto;
        root.style.bottom = StyleKeyword.Auto;
        root.style.width = StyleKeyword.Auto;
        root.style.height = StyleKeyword.Auto;
        root.style.flexGrow = 1;
    }

    private void OnGUI()
    {
        if (!aberto) return;

        bool usarFallback = PrecisaFallbackIMGUI();
        if (!usarFallback && Event.current != null
            && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            // O Game View pode entregar o evento ao IMGUI sem encaminha-lo
            // ao PanelEventHandler do UI Toolkit. Reaproveitamos o hit-test
            // do painel para manter os botoes modernos funcionais.
            ProcessarCliquePainel(Event.current.mousePosition);
        }

        if (!usarFallback) return;

        GarantirDesignerIMGUI();
        snapshot = administracao != null ? administracao.ObterSnapshot() : snapshot;
        GUI.depth = -1000;

        Rect tela = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.Box(tela, GUIContent.none, designerFundo);

        float margem = Mathf.Clamp(Screen.width * 0.012f, 8f, 24f);
        Rect frame = new Rect(margem, margem, Screen.width - margem * 2f, Screen.height - margem * 2f);
        GUI.BeginGroup(frame);
        GUI.Box(new Rect(0f, 0f, frame.width, frame.height), GUIContent.none, designerPainel);

        float lateral = Mathf.Clamp(frame.width * 0.185f, 220f, 285f);
        float cabecalho = Mathf.Clamp(frame.height * 0.105f, 66f, 86f);
        DesenharCabecalhoDesigner(frame.width, lateral, cabecalho);
        DesenharBarraLateralDesigner(lateral, frame.height, cabecalho);

        Rect conteudoDesigner = new Rect(lateral, cabecalho, frame.width - lateral, frame.height - cabecalho);
        DesenharConteudoDesigner(conteudoDesigner);
        GUI.EndGroup();
    }

    private Texture2D CriarTexturaDesigner(Color cor)
    {
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, cor);
        textura.Apply();
        return textura;
    }

    private GUIStyle CriarEstiloDesigner(Texture2D fundo, Color texto, int tamanho, TextAnchor alinhamento, FontStyle fonte = FontStyle.Normal)
    {
        GUIStyle estilo = new GUIStyle(GUI.skin.label)
        {
            fontSize = tamanho,
            alignment = alinhamento,
            fontStyle = fonte,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        estilo.normal.background = fundo;
        estilo.normal.textColor = texto;
        estilo.hover.background = fundo;
        estilo.hover.textColor = texto;
        estilo.padding = new RectOffset(10, 10, 6, 6);
        return estilo;
    }

    private void GarantirDesignerIMGUI()
    {
        if (designerInicializado) return;

        texturaFundo = CriarTexturaDesigner(CorFundoQuartel);
        texturaPainel = CriarTexturaDesigner(CorPainelQuartel);
        texturaCabecalho = CriarTexturaDesigner(CorCabecalhoQuartel);
        texturaLateral = CriarTexturaDesigner(CorLateralQuartel);
        texturaCartao = CriarTexturaDesigner(CorCartaoQuartel);
        texturaNavegacao = CriarTexturaDesigner(CorNavegacaoQuartel);
        texturaNavegacaoAtiva = CriarTexturaDesigner(CorNavegacaoAtivaQuartel);
        texturaBotao = CriarTexturaDesigner(CorBotaoQuartel);
        texturaMapa = CriarTexturaDesigner(new Color(0.008f, 0.065f, 0.090f, 1f));
        texturaGrade = CriarTexturaDesigner(new Color(0.035f, 0.235f, 0.285f, 0.60f));

        designerFundo = CriarEstiloDesigner(texturaFundo, Color.white, 12, TextAnchor.MiddleCenter);
        designerPainel = CriarEstiloDesigner(texturaPainel, Color.white, 12, TextAnchor.UpperLeft);
        designerCabecalho = CriarEstiloDesigner(texturaCabecalho, Color.white, 12, TextAnchor.MiddleLeft);
        designerBarraLateral = CriarEstiloDesigner(texturaLateral, Color.white, 12, TextAnchor.UpperLeft);
        designerMarca = CriarEstiloDesigner(null, CorTextoQuartel, 21, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSubmarca = CriarEstiloDesigner(null, CorCianoQuartel, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerIcone = CriarEstiloDesigner(null, CorCianoQuartel, 17, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerTitulo = CriarEstiloDesigner(null, CorTextoQuartel, 23, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSubtitulo = CriarEstiloDesigner(null, CorTextoSecundarioQuartel, 11, TextAnchor.MiddleLeft);
        designerRotulo = CriarEstiloDesigner(null, CorTextoSecundarioQuartel, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerValor = CriarEstiloDesigner(null, CorTextoQuartel, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSecao = CriarEstiloDesigner(null, CorCianoQuartel, 14, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerCartao = CriarEstiloDesigner(texturaCartao, CorTextoQuartel, 12, TextAnchor.UpperLeft);
        designerNavegacao = CriarEstiloDesigner(texturaNavegacao, new Color(0.71f, 0.82f, 0.86f), 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerNavegacaoAtiva = CriarEstiloDesigner(texturaNavegacaoAtiva, Color.white, 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerBotao = CriarEstiloDesigner(texturaBotao, Color.white, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerBotaoAtivo = CriarEstiloDesigner(texturaNavegacaoAtiva, Color.white, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerBotaoCompacto = CriarEstiloDesigner(texturaBotao, Color.white, 11, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerBotaoCompacto.padding = new RectOffset(5, 5, 3, 3);
        designerBotaoLista = CriarEstiloDesigner(texturaBotao, Color.white, 11, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerBotaoLista.padding = new RectOffset(8, 8, 3, 3);
        designerMapa = CriarEstiloDesigner(texturaMapa, Color.white, 12, TextAnchor.MiddleCenter);
        designerGrade = CriarEstiloDesigner(texturaGrade, Color.white, 1, TextAnchor.MiddleCenter);
        designerStatus = CriarEstiloDesigner(null, CorVerdeQuartel, 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerStatusAlerta = CriarEstiloDesigner(null, CorAlertaQuartel, 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerPequeno = CriarEstiloDesigner(null, CorTextoSecundarioQuartel, 10, TextAnchor.MiddleLeft);

        designerMarca.padding = new RectOffset(0, 0, 0, 0);
        designerSubmarca.padding = new RectOffset(0, 0, 0, 0);
        designerTitulo.padding = new RectOffset(0, 0, 0, 0);
        designerSubtitulo.padding = new RectOffset(0, 0, 0, 0);
        designerRotulo.padding = new RectOffset(0, 0, 0, 0);
        designerValor.padding = new RectOffset(0, 0, 0, 0);
        designerSecao.padding = new RectOffset(0, 0, 0, 0);
        designerStatus.padding = new RectOffset(0, 0, 0, 0);
        designerStatusAlerta.padding = new RectOffset(0, 0, 0, 0);
        designerPequeno.padding = new RectOffset(0, 0, 0, 0);
        designerMarca.wordWrap = false;
        designerSubmarca.wordWrap = false;
        designerTitulo.wordWrap = false;
        designerSubtitulo.wordWrap = false;
        designerRotulo.wordWrap = false;
        designerValor.wordWrap = false;
        designerSecao.wordWrap = false;
        designerInicializado = true;
    }

    private void DesenharCabecalhoDesigner(float largura, float lateral, float altura)
    {
        GUI.Box(new Rect(lateral, 0f, largura - lateral, altura), GUIContent.none, designerCabecalho);
        string nome = quartel != null ? quartel.name.ToUpperInvariant() : "QUARTEL";
        GUI.Label(new Rect(lateral + 28f, 8f, largura - lateral - 370f, 31f), "QUARTEL  —  COMANDO MILITAR", designerTitulo);
        GUI.Label(new Rect(lateral + 30f, 42f, largura - lateral - 370f, 18f), "ESTADO-MAIOR NACIONAL  |  " + nome, designerSubtitulo);

        float direita = largura - 330f;
        GUI.Label(new Rect(direita, 14f, 100f, 16f), "DATA", designerRotulo);
        GUI.Label(new Rect(direita, 33f, 100f, 22f), DateTime.Now.ToString("dd MMM yyyy"), designerSubtitulo);
        GUI.Label(new Rect(direita + 112f, 14f, 100f, 16f), "HORA", designerRotulo);
        GUI.Label(new Rect(direita + 112f, 33f, 100f, 22f), DateTime.Now.ToString("HH:mm:ss"), designerSubtitulo);
        string prontidao = snapshot != null && snapshot.unidadesInoperantes > 0 ? "ATENCAO" : "OPERACIONAL";
        GUI.Label(new Rect(direita + 224f, 14f, 110f, 16f), "PRONTIDAO", designerRotulo);
        GUI.Label(new Rect(direita + 224f, 33f, 110f, 22f), prontidao, prontidao == "ATENCAO" ? designerStatusAlerta : designerStatus);
    }

    private void DesenharBarraLateralDesigner(float largura, float altura, float inicio)
    {
        GUI.Box(new Rect(0f, 0f, largura, altura), GUIContent.none, designerBarraLateral);
        GUI.Label(new Rect(24f, 16f, largura - 32f, 30f), "◈  QUARTEL", designerMarca);
        GUI.Label(new Rect(26f, 47f, largura - 32f, 18f), "COMANDO MILITAR", designerSubmarca);
        GUI.Box(new Rect(0f, inicio - 1f, largura, 1f), GUIContent.none, designerGrade);

        float y = inicio + 22f;
        for (int i = 0; i < nomesNavegacaoDesigner.Length; i++)
        {
            Rect botao = new Rect(10f, y, largura - 20f, 45f);
            GUIStyle estilo = i == abaAtual ? designerNavegacaoAtiva : designerNavegacao;
            if (GUI.Button(botao, GUIContent.none, estilo))
            {
                abaAtual = i;
                AtualizarPainel();
            }
            designerIcone.normal.textColor = i == abaAtual ? CorTextoQuartel : CorCianoQuartel;
            designerIcone.hover.textColor = Color.white;
            GUI.Label(new Rect(botao.x + 7f, botao.y, 30f, botao.height), SimboloNavegacao(i), designerIcone);
            GUI.Label(new Rect(botao.x + 43f, botao.y + 3f, botao.width - 50f, botao.height - 6f), nomesNavegacaoDesigner[i], estilo);
            y += 49f;
        }

        Rect pais = new Rect(14f, altura - 72f, largura - 28f, 56f);
        GUI.Box(pais, GUIContent.none, designerCartao);
        GUI.Label(new Rect(pais.x + 12f, pais.y + 8f, pais.width - 24f, 18f), "REPUBLICA ATLANTICA", designerSubtitulo);
        GUI.Label(new Rect(pais.x + 12f, pais.y + 29f, pais.width - 24f, 18f), "COMANDO CONJUNTO", designerPequeno);
    }

    private string SimboloNavegacao(int indice)
    {
        switch (indice)
        {
            case 0: return "◈";
            case 1: return "◎";
            case 2: return "✦";
            case 3: return "▤";
            case 4: return "⚓";
            case 5: return "✚";
            case 6: return "◌";
            case 7: return "⌖";
            default: return "▣";
        }
    }

    private string RotuloAba(int indice)
    {
        return SimboloNavegacao(indice) + "   " + nomesAbas[indice];
    }

    private void DesenharConteudoDesigner(Rect area)
    {
        GUI.BeginGroup(area);
        GUI.Label(new Rect(28f, 18f, area.width - 56f, 30f), nomesNavegacaoDesigner[abaAtual], designerTitulo);
        GUI.Label(new Rect(30f, 49f, area.width - 60f, 28f), descricoesNavegacaoDesigner[abaAtual], designerSubtitulo);
        GUI.Box(new Rect(28f, 82f, area.width - 56f, 1f), GUIContent.none, designerGrade);
        Rect pagina = new Rect(28f, 96f, area.width - 56f, area.height - 108f);
        switch (abaAtual)
        {
            case 0: DesenharTropasDesigner(pagina); break;
            case 1: DesenharEfetivoDesigner(pagina); break;
            case 2: DesenharRecrutamentoDesigner(pagina); break;
            case 3: DesenharFolhaDesigner(pagina); break;
            case 4: DesenharTripulacoesDesigner(pagina); break;
            case 5: DesenharResgateDesigner(pagina); break;
            case 6: DesenharComunicacoesDesigner(pagina); break;
            case 7: DesenharCartaDesigner(pagina); break;
            case 8: DesenharArsenalDesigner(pagina); break;
        }
        GUI.EndGroup();
    }

    private void DesenharMetricasDesigner(Rect area, string[] rotulos, string[] valores)
    {
        int quantidade = Mathf.Min(rotulos.Length, valores.Length);
        float espacamento = 8f;
        float largura = (area.width - espacamento * (quantidade - 1)) / quantidade;
        for (int i = 0; i < quantidade; i++)
        {
            Rect cartao = new Rect(area.x + i * (largura + espacamento), area.y, largura, area.height);
            GUI.Box(cartao, GUIContent.none, designerCartao);
            GUI.Label(new Rect(cartao.x + 10f, cartao.y + 8f, cartao.width - 20f, 18f), rotulos[i], designerRotulo);
            GUI.Label(new Rect(cartao.x + 10f, cartao.y + 27f, cartao.width - 20f, 30f), valores[i], designerValor);
        }
    }

    private void DesenharLinhaInformacaoDesigner(Rect area, string rotulo, string valor, bool alerta = false)
    {
        GUI.Box(area, GUIContent.none, designerCartao);
        GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width * 0.48f, area.height - 10f), rotulo, designerPequeno);
        GUI.Label(new Rect(area.x + area.width * 0.48f, area.y + 5f, area.width * 0.48f - 12f, area.height - 10f), valor, alerta ? designerStatusAlerta : designerSubtitulo);
    }

    private void DesenharTropasDesigner(Rect area)
    {
        int soldados = quartel?.soldadosNoDormitorio?.Count ?? 0;
        int veiculos = quartel?.veiculosNoQuartel?.Count ?? 0;
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "DORMITORIO", "GARAGEM", "NA COBERTURA", "AERONAVES" },
            new[] { soldados.ToString("N0"), veiculos.ToString("N0"), (snapshot?.unidadesNoRaio ?? 0).ToString("N0"), (snapshot?.aeronavesNoRaio ?? 0).ToString("N0") });

        float y = 84f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "AERONAVES CONECTADAS AO QUARTEL", designerSecao);
        y += 28f;
        float listaAltura = Mathf.Max(105f, area.height - y - 58f);
        GUI.Box(new Rect(0f, y, area.width, listaAltura), GUIContent.none, designerCartao);
        if (snapshot?.aeronaves != null && snapshot.aeronaves.Length > 0)
        {
            float linhaY = y + 8f;
            for (int i = 0; i < snapshot.aeronaves.Length && i < 8; i++)
            {
                QuartelAeronaveSnapshotV2 aviao = snapshot.aeronaves[i];
                if (aviao == null) continue;
                string fuel = aviao.combustivelDisponivel ? $"FUEL {aviao.combustivelPercentual * 100f:0}%" : "FUEL N/R";
                string texto = $"{aviao.nome}  |  {aviao.estadoVoo}  |  {fuel}  |  {aviao.baseAtual}  |  {aviao.distanciaAoQuartel:0} m";
                GUI.Label(new Rect(12f, linhaY, area.width - 24f, 24f), texto, designerSubtitulo);
                linhaY += 26f;
            }
        }
        else
        {
            GUI.Label(new Rect(14f, y + 18f, area.width - 28f, 28f), "NENHUMA AERONAVE DO TIME DENTRO DO RAIO DE COMUNICACAO", designerPequeno);
            GUI.Label(new Rect(14f, y + 48f, area.width - 28f, 24f), "A leitura permanece ligada ao registro real e atualiza automaticamente.", designerPequeno);
        }
        if (GUI.Button(new Rect(0f, area.height - 42f, area.width * 0.48f, 34f), "RECONCILIAR AERONAVES", designerBotao))
            snapshot = administracao != null ? administracao.ObterSnapshot() : snapshot;
        if (GUI.Button(new Rect(area.width * 0.52f, area.height - 42f, area.width * 0.48f, 34f), "FECHAR  (ESC)", designerBotao))
            FecharInterfaceDesigner();
    }

    private void DesenharEfetivoDesigner(Rect area)
    {
        DadosPaisGoverno pais = ObterPaisJogador();
        int armazenados = (quartel?.soldadosNoDormitorio?.Count ?? 0) + (quartel?.veiculosNoQuartel?.Count ?? 0);
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ATIVOS", "RESERVISTAS", "ALISTAVEIS", "ARMAZENADOS", "INOPERANTES" },
            new[] { (pais?.populacaoMilitarAtiva ?? 0).ToString("N0"), (pais?.reservistas ?? 0).ToString("N0"), (pais?.alistaveis ?? 0).ToString("N0"), armazenados.ToString("N0"), (snapshot?.unidadesInoperantes ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "SITUACAO ADMINISTRATIVA", designerSecao);
        y += 28f;
        string ultimo = snapshot != null ? snapshot.ultimoEvento : "n/d";
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO DO EFETIVO", (pais?.populacaoMilitarAtiva ?? 0) > 0 ? "OPERACIONAL" : "SEM EFETIVO", (pais?.populacaoMilitarAtiva ?? 0) == 0); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PESSOAL ALOCADO", (snapshot?.pessoalAlocado ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "BAIXAS NACIONAIS", (pais?.mortosAcumulados ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ULTIMO EVENTO", ultimo); y += 50f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "INTEGRIDADE DO REGISTRO", designerSecao);
        GUI.Label(new Rect(0f, y + 30f, area.width, 24f), "O quadro usa o governo nacional e nao duplica a contabilidade existente.", designerPequeno);
    }

    private void DesenharRecrutamentoDesigner(Rect area)
    {
        QuartelAdministracaoRuntime admin = administracao;
        DadosPaisGoverno pais = ObterPaisJogador();
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ALISTAVEIS", "EM FORMACAO", "CONCLUIDOS", "TEMPO BASE" },
            new[] { (pais?.alistaveis ?? 0).ToString("N0"), (snapshot?.recrutasEmFormacao ?? 0).ToString("N0"), (snapshot?.recrutasConcluidos ?? 0).ToString("N0"), (quartel?.tempoFormacaoSegundos ?? 0f).ToString("0") + " s" });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "PROTOCOLOS DE PESSOAL", designerSecao); y += 30f;
        if (GUI.Button(new Rect(0f, y, area.width, 34f), quartel != null && quartel.recrutamentoAutomatico ? "✓  RECRUTAMENTO AUTOMATICO ATIVO" : "○  ATIVAR RECRUTAMENTO AUTOMATICO", quartel != null && quartel.recrutamentoAutomatico ? designerBotaoAtivo : designerBotao))
            if (quartel != null) quartel.recrutamentoAutomatico = !quartel.recrutamentoAutomatico;
        y += 43f;
        if (GUI.Button(new Rect(0f, y, area.width, 34f), "PROCESSAR RECRUTAMENTO DO DIA", designerBotao) && admin != null)
            admin.SolicitarRecrutamentoManual();
        y += 46f;
        GUI.Label(new Rect(0f, y, area.width * 0.32f, 24f), "QUANTIDADE POR ORDEM", designerRotulo);
        quantidadeRecrutamentoUI = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(area.width * 0.34f, y + 4f, area.width * 0.42f, 18f), quantidadeRecrutamentoUI, 1, 20));
        GUI.Label(new Rect(area.width * 0.80f, y, area.width * 0.20f, 24f), quantidadeRecrutamentoUI.ToString(), designerValor);
        y += 32f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "RECRUTAMENTO DIRECIONADO", designerSecao);
        y += 30f;
        QuartelForcaV2[] forcas = { QuartelForcaV2.Infantaria, QuartelForcaV2.Veiculos, QuartelForcaV2.Naval, QuartelForcaV2.Aerea };
        for (int i = 0; i < forcas.Length; i++)
        {
            float larguraBotao = (area.width - 12f) * 0.5f;
            float bx = (i % 2) * (larguraBotao + 12f);
            float by = y + (i / 2) * 40f;
            QuartelForcaV2 forca = forcas[i];
            if (GUI.Button(new Rect(bx, by, larguraBotao, 34f), "RECRUTAR " + NomeForca(forca), designerBotao) && admin != null)
                admin.SolicitarRecrutamentoManual(forca, quantidadeRecrutamentoUI);
        }
        y += 92f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "META DE EFETIVO LOCAL", (quartel?.metaEfetivo ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PROGRESSO", snapshot != null ? $"{snapshot.progressoTreinamento * 100f:0}%  |  {snapshot.segundosRestantesTreinamento:0.0} s" : "0%"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FORCA DE DESTINO", snapshot?.forcaTreinamento ?? "Nenhuma");
    }

    private void DesenharFolhaDesigner(Rect area)
    {
        DadosPaisGoverno pais = ObterPaisJogador();
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ATIVOS", "FOLHA DIARIA", "PERIODO", "PAGAMENTO" },
            new[] { (pais?.populacaoMilitarAtiva ?? 0).ToString("N0"), "$" + (snapshot?.custoFolhaDiario ?? 0).ToString("N0"), (snapshot?.periodoFolhaDias ?? 0) + " dias", snapshot != null && snapshot.ultimoPagamentoRealizado ? "DESCONTADO" : "PENDENTE" });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "CUSTOS E RESPONSABILIDADE FISCAL", designerSecao); y += 32f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "SALDO DO TESOURO", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FOLHA DO PERIODO", snapshot != null ? "$" + snapshot.custoFolhaCalculado.ToString("N0") : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PROXIMO VENCIMENTO", snapshot != null ? "DIA " + snapshot.proximoDiaFolha : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "DIAS PENDENTES", (snapshot?.diasFolhaPendentes ?? 0).ToString("N0"), snapshot != null && snapshot.folhaPendente);
    }

    private void DesenharTripulacoesDesigner(Rect area)
    {
        QuartelForcaSnapshotV2 naval = ObterResumoForca(QuartelForcaV2.Naval);
        QuartelForcaSnapshotV2 aerea = ObterResumoForca(QuartelForcaV2.Aerea);
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "NAVIOS", "AERONAVES", "EXIGIDO", "ALOCADO", "INOPERANTES" },
            new[] { (naval?.unidades ?? 0).ToString("N0"), (aerea?.unidades ?? 0).ToString("N0"), (snapshot?.pessoalExigido ?? 0).ToString("N0"), (snapshot?.pessoalAlocado ?? 0).ToString("N0"), (snapshot?.unidadesInoperantes ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "SITUACAO DE GUARNICAO", designerSecao); y += 32f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "RESERVA USADA COMO TRIPULACAO", "NAO"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "TRIPULACAO NAVAL", naval != null ? naval.pessoalExigido + " / " + naval.pessoalAlocado : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "TRIPULACAO AEREA", aerea != null ? aerea.pessoalExigido + " / " + aerea.pessoalAlocado : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO", snapshot != null && snapshot.unidadesInoperantes > 0 ? "INOPERANTES" : "OPERACIONAIS", snapshot != null && snapshot.unidadesInoperantes > 0);
    }

    private void DesenharResgateDesigner(Rect area)
    {
        float mapaLargura = area.width * 0.61f;
        Rect mapa = new Rect(0f, 0f, mapaLargura, area.height - 4f);
        GUI.Box(mapa, GUIContent.none, designerMapa);
        for (int i = 1; i < 8; i++) GUI.Box(new Rect(mapa.x + mapa.width * i / 8f, mapa.y, 1f, mapa.height), GUIContent.none, designerGrade);
        for (int i = 1; i < 6; i++) GUI.Box(new Rect(mapa.x, mapa.y + mapa.height * i / 6f, mapa.width, 1f), GUIContent.none, designerGrade);
        GUI.Label(new Rect(mapa.x + 18f, mapa.y + 15f, 50f, 24f), "N", designerSecao);
        GUI.Label(new Rect(mapa.x + mapa.width * 0.44f, mapa.y + mapa.height * 0.47f, 80f, 24f), "QG", designerStatus);
        GUI.Label(new Rect(mapa.x + 18f, mapa.y + mapa.height - 36f, mapa.width - 36f, 24f), "COBERTURA OPERACIONAL  |  ALERTAS EM TEMPO REAL", designerPequeno);

        float x = mapaLargura + 18f;
        float w = area.width - x;
        GUI.Label(new Rect(x, 0f, w, 24f), "SINAL DE EMERGENCIA", designerSecao);
        DesenharLinhaInformacaoDesigner(new Rect(x, 32f, w, 32f), "UNIDADES AVALIADAS", (snapshot?.unidadesAvaliadasResgate ?? 0).ToString("N0"));
        DesenharLinhaInformacaoDesigner(new Rect(x, 70f, w, 32f), "ALERTAS ATIVOS", (snapshot?.unidadesComAlertaResgate ?? 0).ToString("N0"), snapshot != null && snapshot.unidadesComAlertaResgate > 0);
        DesenharLinhaInformacaoDesigner(new Rect(x, 108f, w, 32f), "PERDAS REGISTRADAS", (snapshot?.perdasRegistradas ?? 0).ToString("N0"));
        if (GUI.Button(new Rect(x, 158f, w, 34f), "RESGATE MANUAL", designerBotao)) quartel?.SolicitarResgateManual();
        if (GUI.Button(new Rect(x, 200f, w, 34f), "REPARAR NO RAIO", designerBotao)) quartel?.SolicitarReparosNoRaio();
        GUI.Label(new Rect(x, 250f, w, 48f), snapshot != null ? snapshot.ultimoAvisoResgate : "Nenhum aviso de resgate registrado.", designerPequeno);
    }

    private void DesenharComunicacoesDesigner(Rect area)
    {
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "AERONAVES", "INIMIGOS", "SUBMARINOS", "MISSEIS" },
            new[] { (snapshot?.aeronavesNoRaio ?? 0).ToString("N0"), (snapshot?.contatosInimigos ?? 0).ToString("N0"), (snapshot?.contatosSubmarinos ?? 0).ToString("N0"), (snapshot?.contatosMisseis ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "REDE DE INFORMACOES", designerSecao); y += 34f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "REGISTRO DE AERONAVES", (snapshot?.aeronavesNoRaio ?? 0) > 0 ? "CONECTADO" : "AGUARDANDO CONTATO"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "RETRANSMISSAO", "ATIVA"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ULTIMO EVENTO", snapshot?.ultimoEvento ?? "n/d"); y += 38f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "ULTIMAS TRANSMISSOES", designerSecao); y += 30f;
        if (snapshot?.comunicacoes != null && snapshot.comunicacoes.Length > 0)
        {
            int limite = Mathf.Min(6, snapshot.comunicacoes.Length);
            for (int i = 0; i < limite; i++)
            {
                QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                if (comunicacao == null) continue;
                string linha = "[" + comunicacao.horario + "] " + comunicacao.origem + " | " + comunicacao.tipo + " | " + comunicacao.mensagem;
                GUI.Label(new Rect(0f, y, area.width, 24f), linha, comunicacao.inimigo ? designerStatusAlerta : designerPequeno);
                y += 25f;
            }
        }
        else
        {
            GUI.Label(new Rect(0f, y, area.width, 30f), "Aguardando telemetria do E-3.", designerPequeno);
        }
    }

    private void DesenharCartaDesigner(Rect area)
    {
        GarantirCartaTopograficaView();
        float raio = cartaTopograficaView != null ? cartaTopograficaView.RaioMapa : ObterRaioCarta();
        float barraAltura = 36f;
        GUI.Box(new Rect(0f, 0f, area.width, barraAltura), GUIContent.none, designerCartao);
        GUI.Label(new Rect(12f, 7f, 220f, 22f), "CARTA NAUTICA OPERACIONAL", designerSecao);
        if (GUI.Button(new Rect(area.width - 330f, 5f, 150f, 27f), "2D TOPOGRAFICO", cartaVista3D ? designerNavegacao : designerBotaoAtivo))
        {
            cartaVista3D = false;
            AtualizarPainel();
        }
        if (GUI.Button(new Rect(area.width - 170f, 5f, 158f, 27f), "3D TOPOGRAFICO", cartaVista3D ? designerBotaoAtivo : designerNavegacao))
        {
            cartaVista3D = true;
            AtualizarPainel();
        }

        float painelLateral = Mathf.Clamp(area.width * 0.31f, 235f, 315f);
        // Reserva o espaço livre inferior da página para a carta. O mapa e o
        // painel de ações descem juntos; o rodapé é colocado depois deles,
        // nunca por cima dos botões.
        float alturaDisponivel = Mathf.Max(320f, area.height - barraAltura - 18f);
        // O painel de controle possui campos, acoes e validacao. Com a altura
        // antiga (150 px) as acoes ocupavam a mesma faixa da validacao e os
        // textos ficavam sobrepostos no fallback IMGUI usado pela demo1.
        // A barra de ações tem altura fixa suficiente para os campos, os dois
        // botões de coordenadas e a barra final. Isso deixa o mapa crescer e
        // mantém LANÇAR/CANCELAR/CENTRAR dentro da área visível.
        float alturaAtaque = Mathf.Clamp(alturaDisponivel * 0.34f, 214f, 224f);
        float alturaMapaBase = Mathf.Max(240f, alturaDisponivel - alturaAtaque - 8f);
        float alturaMapa = Mathf.Min(
            alturaMapaBase * 1.20f,
            Mathf.Max(240f, alturaDisponivel - alturaAtaque - 34f));
        Rect mapa = new Rect(0f, barraAltura + 8f, area.width - painelLateral - 8f, alturaMapa);
        Rect telemetria = new Rect(area.width - painelLateral, barraAltura + 8f, painelLateral, alturaMapa);
        Texture texturaTerreno = ObterTexturaTerrenoCarta(raio, mapa.width / Mathf.Max(1f, mapa.height));
        GUI.Box(mapa, GUIContent.none, designerMapa);
        if (texturaTerreno != null) GUI.DrawTexture(mapa, texturaTerreno, ScaleMode.StretchToFill, false);
        DesenharGradeCarta(mapa);
        DesenharCurvasNivelCarta(mapa, raio);
        DesenharRotaSelecionadaCarta(mapa, raio);
        if (quartel != null)
        {
            for (int i = 0; i < quartel.TrilhasLancamento.Count; i++)
            {
                GerenciadorQuartel.TrilhaLancamentoCoordenadoV2 trilha = quartel.TrilhasLancamento[i];
                if (trilha == null) continue;
                Vector2 inicio = PontoCarta(mapa, trilha.pontoLancamento, raio);
                Vector2 fim = PontoCarta(mapa, trilha.pontoImpactoPrevisto, raio);
                DesenharSegmentoCarta(inicio, fim, new Color(1f, 0.54f, 0.16f, 0.95f), 2f);
            }
        }

        int pontosUnidades = 0;
        int pontosMisseis = 0;
        if (cartaTopograficaView != null)
        {
            for (int i = 0; i < cartaTopograficaView.Unidades.Count; i++)
            {
                QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.Unidades[i];
                if (unidade == null) continue;
                Vector2 ponto = PontoCarta(mapa, unidade.posicao, raio);
                Color cor = !unidade.aliada ? new Color(1f, 0.25f, 0.20f) : CorTipoCarta(unidade.tipo);
                DesenharPontoCarta(mapa, ponto, cor, unidade.id, unidade.nome, unidade.id == cartaUnidadeSelecionadaId, false);
                pontosUnidades++;
            }

            for (int i = 0; i < cartaTopograficaView.Misseis.Count; i++)
            {
                QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.Misseis[i];
                if (missil == null) continue;
                Vector2 ponto = PontoCarta(mapa, missil.posicao, raio);
                DesenharPontoCarta(mapa, ponto, missil.aliado ? new Color(0.35f, 0.72f, 1f) : new Color(1f, 0.22f, 0.18f), missil.id, missil.nome, missil.id == cartaMissilSelecionadoId, true);
                pontosMisseis++;
            }
        }

        if (quartel != null)
        {
            for (int i = 0; i < quartel.ContatosMilitares.Count; i++)
            {
                GerenciadorQuartel.ContatoMilitarQuartelV2 contato = quartel.ContatosMilitares[i];
                if (contato == null || !contato.inimigo) continue;
                Vector2 ponto = PontoCarta(mapa, contato.posicao, raio);
                DesenharContatoCartaFallback(mapa, ponto, contato);
            }
        }

        DesenharControlesNavegacaoCarta(mapa);
        Vector2 centro = PontoCarta(mapa, quartel != null ? quartel.transform.position : transform.position, raio);
        Color corAnterior = GUI.color;
        GUI.color = new Color(1f, 0.78f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(centro.x - 6f, centro.y - 6f, 12f, 12f), Texture2D.whiteTexture);
        GUI.color = corAnterior;
        GUI.Label(new Rect(centro.x + 9f, centro.y - 12f, 140f, 22f), "QG  " + quartel.name, designerStatus);
        GUI.Label(new Rect(mapa.x + 12f, mapa.y + 10f, 180f, 24f), cartaVista3D ? "VISUALIZACAO INCLINADA" : "VISTA SUPERIOR", designerSubtitulo);
        GUI.Label(new Rect(mapa.x + 12f, mapa.y + mapa.height - 28f, mapa.width - 24f, 24f), "RAIO " + raio.ToString("0") + " m  |  UNIDADES " + pontosUnidades + "  |  MISSEIS " + pontosMisseis, designerPequeno);
        DesenharTelemetriaCartaDesigner(telemetria, raio);
        Rect painelAtaque = new Rect(0f, mapa.yMax + 8f, area.width, alturaAtaque);
        DesenharPainelAtaqueDesigner(painelAtaque);
        GUI.Label(new Rect(0f, painelAtaque.yMax + 8f, area.width, 24f), "Carta topografica real | arraste para mover | roda para zoom | contatos E-3, telemetria e trajetorias.", designerPequeno);
    }

    private void DesenharControlesNavegacaoCarta(Rect mapa)
    {
        if (cartaTerrenoRenderer == null) return;

        float x = mapa.x + 10f;
        float y = mapa.y + 34f;
        float tamanho = 32f;
        float espacamento = 3f;
        GUI.Box(new Rect(x - 4f, y - 4f, tamanho * 7f + espacamento * 8f, tamanho + 8f), GUIContent.none, designerCartao);

        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "←", designerBotaoCompacto))
            cartaTerrenoRenderer.DeslocarMapa(new Vector2(-0.10f, 0f));
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "→", designerBotaoCompacto))
            cartaTerrenoRenderer.DeslocarMapa(new Vector2(0.10f, 0f));
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "↑", designerBotaoCompacto))
            cartaTerrenoRenderer.DeslocarMapa(new Vector2(0f, -0.10f));
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "↓", designerBotaoCompacto))
            cartaTerrenoRenderer.DeslocarMapa(new Vector2(0f, 0.10f));
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "+", designerBotaoCompacto))
            cartaTerrenoRenderer.AjustarZoom(1f);
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "−", designerBotaoCompacto))
            cartaTerrenoRenderer.AjustarZoom(-1f);
        x += tamanho + espacamento;
        if (GUI.Button(new Rect(x, y, tamanho, tamanho), "QG", designerBotaoCompacto))
            cartaTerrenoRenderer.SolicitarCentralizacao(quartel != null ? quartel.transform.position : transform.position);

        Event evento = Event.current;
        if (evento == null || !mapa.Contains(evento.mousePosition)) return;

        if (evento.type == EventType.ScrollWheel)
        {
            cartaTerrenoRenderer.AjustarZoom(evento.delta.y > 0f ? 1f : -1f);
            evento.Use();
            return;
        }

        if (evento.type == EventType.MouseDown && evento.button == 0 && GUIUtility.hotControl == 0)
        {
            arrastandoCartaFallback = true;
            cartaFallbackFoiArrastada = false;
            ultimaPosicaoCartaFallback = evento.mousePosition;
        }
        else if (evento.type == EventType.MouseDrag && evento.button == 0 && arrastandoCartaFallback)
        {
            Vector2 delta = evento.mousePosition - ultimaPosicaoCartaFallback;
            if (delta.sqrMagnitude > 0.01f)
            {
                cartaTerrenoRenderer.DeslocarMapa(new Vector2(
                    delta.x / Mathf.Max(1f, mapa.width),
                    delta.y / Mathf.Max(1f, mapa.height)));
                ultimaPosicaoCartaFallback = evento.mousePosition;
                cartaFallbackFoiArrastada = true;
                evento.Use();
            }
        }
        else if (evento.type == EventType.MouseUp && evento.button == 0 && arrastandoCartaFallback)
        {
            arrastandoCartaFallback = false;
            if (cartaFallbackFoiArrastada)
            {
                evento.Use();
            }
            else if (GUIUtility.hotControl == 0)
            {
                Vector2 local = evento.mousePosition - new Vector2(mapa.x, mapa.y);
                Vector2 viewport = new Vector2(
                    Mathf.Clamp01(local.x / Mathf.Max(1f, mapa.width)),
                    Mathf.Clamp01(1f - local.y / Mathf.Max(1f, mapa.height)));
                if (ProcessarCliqueCartaViewport(viewport, evento.control, evento.shift))
                    evento.Use();
            }
            cartaFallbackFoiArrastada = false;
        }
    }

    private void DesenharGradeCarta(Rect mapa)
    {
        for (int i = 1; i < 10; i++) GUI.Box(new Rect(mapa.x + mapa.width * i / 10f, mapa.y, 1f, mapa.height), GUIContent.none, designerGrade);
        for (int i = 1; i < 8; i++) GUI.Box(new Rect(mapa.x, mapa.y + mapa.height * i / 8f, mapa.width, 1f), GUIContent.none, designerGrade);
        GUI.Label(new Rect(mapa.x + mapa.width - 32f, mapa.y + 8f, 24f, 22f), "N", designerStatus);
    }

    private void DesenharCurvasNivelCarta(Rect mapa, float raio)
    {
        if (cartaTopograficaView == null || quartel == null) return;
        float elevacaoCentro = cartaTopograficaView.ObterElevacaoTerreno(quartel.transform.position);
        for (int nivel = 1; nivel <= 6; nivel++)
        {
            float raioBase = raio * (0.15f + nivel * 0.115f);
            Vector2 anterior = Vector2.zero;
            bool possuiAnterior = false;
            for (int amostra = 0; amostra <= 24; amostra++)
            {
                float angulo = amostra / 24f * Mathf.PI * 2f;
                Vector3 deslocamentoLocal = new Vector3(Mathf.Cos(angulo) * raioBase, 0f, Mathf.Sin(angulo) * raioBase);
                Vector3 mundo = quartel.transform.TransformPoint(deslocamentoLocal);
                float elevacao = cartaTopograficaView.ObterElevacaoTerreno(mundo);
                float deformacao = Mathf.Clamp((elevacao - elevacaoCentro) / Mathf.Max(80f, raio * 0.4f), -0.16f, 0.16f);
                float raioDeformado = raioBase * (1f + deformacao);
                Vector3 pontoMundo = quartel.transform.TransformPoint(new Vector3(Mathf.Cos(angulo) * raioDeformado, 0f, Mathf.Sin(angulo) * raioDeformado));
                Vector2 ponto = PontoCarta(mapa, pontoMundo, raio);
                if (possuiAnterior) DesenharSegmentoCarta(anterior, ponto, new Color(0.22f, 0.83f, 0.72f, 0.24f), 1f);
                anterior = ponto;
                possuiAnterior = true;
            }
        }
    }

    private void DesenharRotaSelecionadaCarta(Rect mapa, float raio)
    {
        if (cartaTopograficaView == null) return;
        QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId);
        if (unidade == null) return;
        for (int i = 1; i < unidade.rotaPercorrida.Count; i++)
        {
            DesenharSegmentoCarta(PontoCarta(mapa, unidade.rotaPercorrida[i - 1], raio), PontoCarta(mapa, unidade.rotaPercorrida[i], raio), new Color(0.12f, 0.92f, 0.98f, 0.92f), 2f);
        }
        if (unidade.possuiDestino)
        {
            Vector2 atual = PontoCarta(mapa, unidade.posicao, raio);
            Vector2 destino = PontoCarta(mapa, unidade.destino, raio);
            float distancia = Vector2.Distance(atual, destino);
            int segmentos = Mathf.Max(1, Mathf.RoundToInt(distancia / 15f));
            for (int i = 0; i < segmentos; i += 2)
            {
                float t0 = i / (float)segmentos;
                float t1 = Mathf.Min(1f, (i + 1f) / segmentos);
                DesenharSegmentoCarta(Vector2.Lerp(atual, destino, t0), Vector2.Lerp(atual, destino, t1), new Color(0.16f, 0.84f, 1f, 0.95f), 2f);
            }
            GUI.Label(new Rect(destino.x + 6f, destino.y - 12f, 120f, 22f), "DESTINO", designerStatus);
        }
    }

    private void DesenharTelemetriaCartaDesigner(Rect area, float raio)
    {
        GUI.Box(area, GUIContent.none, designerCartao);
        GUI.Label(new Rect(area.x + 12f, area.y + 12f, area.width - 24f, 24f), "TELEMETRIA DA UNIDADE", designerSecao);
        QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView != null ? cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId) : null;
        QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView != null ? cartaTopograficaView.EncontrarMissil(cartaMissilSelecionadoId) : null;
        GerenciadorQuartel.ContatoMilitarQuartelV2 contato = EncontrarContatoCarta(cartaContatoSelecionadoId);
        CartaCombateRegistro.CopiarEventos(eventosCombateCarta);
        limiteTelemetriaCartaFallback = area.yMax - (eventosCombateCarta.Count > 0 ? 164f : 116f);
        float y = area.y + 42f;
        if (unidade != null)
        {
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidadeLancamento = EncontrarUnidadeLancamentoCarta(unidade.id);
            string selecao = unidadeLancamento == null
                ? "NÃO É LANÇADORA DO QUARTEL"
                : unidadeLancamento.selecionada ? "SELECIONADA PARA DISPARO"
                : "NÃO SELECIONADA PARA DISPARO";
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 36f), "UNIDADE CLICADA  |  " + unidade.nome + "\n" + unidade.tipo + " | EQUIPE " + unidade.equipe + " | " + unidade.situacao, designerTitulo);
            y += 44f;
            y = LinhaTelemetriaCarta(area, y, "SELEÇÃO PARA TIRO", selecao);
            y = LinhaTelemetriaCarta(area, y, "ESTADO", unidade.estado);
            y = LinhaTelemetriaCarta(area, y, "MISSAO", unidade.missao);
            y = LinhaTelemetriaCarta(area, y, "POSICAO", FormatarPosicao(unidade.posicao));
            y = LinhaTelemetriaCarta(area, y, "ALTITUDE", unidade.altitudeAbsoluta.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "TERRENO", unidade.elevacaoTerreno.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "ACIMA DO SOLO", unidade.alturaAcimaDoSolo.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "VELOCIDADE", (unidade.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h");
            y = LinhaTelemetriaCarta(area, y, "RUMO", unidade.rumo);
            y = LinhaTelemetriaCarta(area, y, "COMBUSTIVEL", unidade.combustivelCapacidade > 0f ? (unidade.combustivelPercentual * 100f).ToString("0") + "%" : "NAO DISPONIVEL");
            y = LinhaTelemetriaCarta(area, y, "PERCORRIDA", unidade.distanciaPercorrida.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "RESTANTE", unidade.possuiDestino ? unidade.distanciaRestante.ToString("0") + " m" : "SEM DESTINO");
            y = LinhaTelemetriaCarta(area, y, "CHEGADA", unidade.possuiDestino ? FormatarTempo(unidade.tempoEstimadoSegundos) : "N/A");
            y = LinhaTelemetriaCarta(area, y, "ARMAMENTO", unidade.armamento);
            if (!string.IsNullOrWhiteSpace(unidade.baseAtual)) y = LinhaTelemetriaCarta(area, y, "BASE", unidade.baseAtual);
            if (!string.IsNullOrWhiteSpace(unidade.vaga) && y < area.yMax - 46f) y = LinhaTelemetriaCarta(area, y, "VAGA", unidade.vaga);
        }
        else if (contato != null)
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 36f), contato.nome + "\nALVO " + contato.tipo + " | " + contato.estado, designerTitulo);
            y += 44f;
            y = LinhaTelemetriaCarta(area, y, "ID", contato.id);
            y = LinhaTelemetriaCarta(area, y, "PAIS / TIME", contato.pais);
            y = LinhaTelemetriaCarta(area, y, "TRANSMISSOR", contato.transmissor);
            y = LinhaTelemetriaCarta(area, y, "POSICAO", FormatarPosicao(contato.posicao));
            y = LinhaTelemetriaCarta(area, y, "HORARIO", contato.horario);
            float idadeContato = contato.ultimaAtualizacao > 0f
                ? Mathf.Max(0f, Time.unscaledTime - contato.ultimaAtualizacao)
                : 0f;
            y = LinhaTelemetriaCarta(area, y, "IDADE", idadeContato.ToString("0.0") + " s");
            y = LinhaTelemetriaCarta(area, y, "VALIDADE", contato.estado);
        }
        else if (missil != null)
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 36f), missil.nome + "\n" + missil.tipo + " | " + missil.estado, designerTitulo);
            y += 44f;
            y = LinhaTelemetriaCarta(area, y, "LANCADOR", missil.origem);
            y = LinhaTelemetriaCarta(area, y, "VELOCIDADE", (missil.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h");
            y = LinhaTelemetriaCarta(area, y, "PERCORRIDA", missil.distanciaPercorrida.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "ATE O IMPACTO", missil.distanciaRestante.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "PONTO DE IMPACTO", FormatarPosicao(missil.pontoProvavelImpacto));
            y = LinhaTelemetriaCarta(area, y, "TEMPO DE VOO", FormatarTempo(missil.tempoDesdeLancamento));
            if (missil.guiagemPerdida) y = LinhaTelemetriaCarta(area, y, "ALERTA", "GUIAGEM PERDIDA");

            // A demo1 usa o renderer IMGUI quando o UIDocument ainda não
            // recebeu um painel visual. Mesmo nessa rota o operador precisa
            // conseguir acompanhar o míssil real, sem criar uma simulação.
            MissileThreatTracker tracker = ObterTrackerSelecionadoCarta();
            float larguraAcao = (area.width - 30f) * 0.5f;
            float yAcoes = Mathf.Min(y + 4f, area.yMax - 39f);
            float limiteAcoes = eventosCombateCarta.Count > 0 ? area.yMax - 164f : area.yMax - 10f;
            if (tracker != null && tracker.RaizMissil != null && y + 33f < limiteAcoes)
            {
                if (GUI.Button(new Rect(area.x + 12f, yAcoes, larguraAcao, 29f), "◎  RASTREAR", designerBotaoCompacto))
                    IniciarRastreamentoMissilCarta();
                if (GUI.Button(new Rect(area.x + 18f + larguraAcao, yAcoes, larguraAcao, 29f), "■  PARAR", designerBotaoCompacto))
                    PararRastreamentoMissilCarta();
            }
        }
        else
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 54f), "Clique em uma unidade ou missil no mapa para abrir a telemetria completa.", designerSubtitulo);
            y += 70f;
            y = LinhaTelemetriaCarta(area, y, "UNIDADES", cartaTopograficaView != null ? cartaTopograficaView.Unidades.Count.ToString("N0") : "0");
            y = LinhaTelemetriaCarta(area, y, "MISSEIS EM VOO", cartaTopograficaView != null ? cartaTopograficaView.Misseis.Count.ToString("N0") : "0");
            y = LinhaTelemetriaCarta(area, y, "ALTITUDE", "selecione uma unidade aerea");
            if (cartaTopograficaView != null && cartaTopograficaView.Unidades.Count > 0 && eventosCombateCarta.Count == 0)
            {
                GUI.Label(new Rect(area.x + 12f, y + 4f, area.width - 24f, 20f), "AERONAVES / UNIDADES NA CARTA", designerRotulo);
                y += 25f;
                int limite = area.height > 380f ? 4 : 2;
                int exibidas = 0;
                for (int i = 0; i < cartaTopograficaView.Unidades.Count && exibidas < limite; i++)
                {
                    QuartelCartaTopograficaView.UnidadeTelemetria item = cartaTopograficaView.Unidades[i];
                    if (item == null) continue;
                    string texto = EncurtarTextoDesigner(item.nome, 20) + " | " + EncurtarTextoDesigner(item.tipo, 14)
                        + "\n" + FormatarPosicao(item.posicao);
                    if (GUI.Button(new Rect(area.x + 12f, y, area.width - 24f, 32f), texto, designerBotaoLista))
                        SelecionarUnidadeCarta(item.id, Event.current.control, Event.current.shift);
                    y += 36f;
                    exibidas++;
                }
            }
        }

        DesenharLogCombateCartaFallback(area);
    }

    private void DesenharLogCombateCartaFallback(Rect area)
    {
        if (quartel == null) return;

        CartaCombateRegistro.CopiarEventos(eventosCombateCarta);
        if (eventosCombateCarta.Count == 0)
        {
            GUI.Label(new Rect(area.x + 12f, area.yMax - 70f, area.width - 24f, 22f), "EVENTOS RECENTES", designerSecao);
            int eventos = 0;
            if (snapshot != null && snapshot.comunicacoes != null)
            {
                for (int i = 0; i < snapshot.comunicacoes.Length && eventos < 2; i++)
                {
                    QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                    if (comunicacao == null) continue;
                    GUI.Label(new Rect(area.x + 12f, area.yMax - 48f + eventos * 22f, area.width - 24f, 20f), comunicacao.horario + "  " + comunicacao.tipo, designerPequeno);
                    eventos++;
                }
            }
            if (eventos == 0) GUI.Label(new Rect(area.x + 12f, area.yMax - 46f, area.width - 24f, 20f), "Sem eventos transmitidos.", designerPequeno);
            return;
        }

        int abatesAliados = 0;
        int perdasAliadas = 0;
        for (int i = 0; i < eventosCombateCarta.Count; i++)
        {
            CartaCombateRegistro.EventoCombate evento = eventosCombateCarta[i];
            if (evento == null || evento.tipo != "UNIDADE DESTRUÍDA") continue;
            if (evento.equipeAtacante == quartel.teamID && evento.equipeAlvo != quartel.teamID) abatesAliados++;
            else if (evento.equipeAlvo == quartel.teamID && evento.equipeAtacante != quartel.teamID) perdasAliadas++;
        }

        const float alturaCabecalho = 22f;
        const float alturaLinha = 34f;
        float topo = area.yMax - 146f;
        GUI.Label(new Rect(area.x + 12f, topo, area.width - 24f, alturaCabecalho), "LOG DE COMBATE", designerSecao);
        GUI.Label(new Rect(area.x + 12f, topo + alturaCabecalho, area.width - 24f, 18f),
            "ABATES ALIADOS: " + abatesAliados + "  |  PERDAS ALIADAS: " + perdasAliadas, designerPequeno);

        int total = Mathf.Min(8, eventosCombateCarta.Count);
        float viewportAltura = Mathf.Min(98f, Mathf.Max(42f, area.height - 248f));
        Rect viewport = new Rect(area.x + 8f, topo + 42f, area.width - 16f, viewportAltura);
        Rect conteudoScroll = new Rect(0f, 0f, viewport.width - 18f, Mathf.Max(viewport.height, total * alturaLinha));
        scrollEventosCombateFallback = GUI.BeginScrollView(viewport, scrollEventosCombateFallback, conteudoScroll);
        for (int i = 0; i < total; i++)
        {
            CartaCombateRegistro.EventoCombate evento = eventosCombateCarta[i];
            if (evento == null) continue;
            string linha = "[" + evento.horario + "] " + evento.tipo + "\n" + EncurtarTextoDesigner(evento.descricao, 48);
            if (GUI.Button(new Rect(0f, i * alturaLinha, conteudoScroll.width, alturaLinha - 3f), linha, designerBotaoLista))
                AoClicarEventoCombate(evento.id);
        }
        if (total == 0)
        {
            GUI.Label(new Rect(0f, 0f, conteudoScroll.width, 22f),
                snapshot != null && snapshot.comunicacoes != null && snapshot.comunicacoes.Length > 0
                    ? "Comunicações recentes disponíveis na aba COMUNICAÇÕES."
                    : "Sem eventos de combate transmitidos.", designerPequeno);
        }
        GUI.EndScrollView();
    }

    private float LinhaTelemetriaCarta(Rect area, float y, string nome, string valor)
    {
        float limite = limiteTelemetriaCartaFallback > 0f ? limiteTelemetriaCartaFallback : area.yMax - 116f;
        if (y > limite) return y;
        GUI.Label(new Rect(area.x + 12f, y, area.width * 0.42f, 20f), nome, designerPequeno);
        GUI.Label(new Rect(area.x + area.width * 0.40f, y, area.width * 0.56f, 20f), valor, designerSubtitulo);
        return y + 21f;
    }

    private void DesenharPontoCarta(Rect mapa, Vector2 ponto, Color cor, string id, string texto, bool selecionado, bool missil)
    {
        float tamanho = selecionado ? 12f : missil ? 8f : 9f;
        Color anterior = GUI.color;
        GUI.color = cor;
        GUI.DrawTexture(new Rect(ponto.x - tamanho * 0.5f, ponto.y - tamanho * 0.5f, tamanho, tamanho), Texture2D.whiteTexture);
        GUI.color = anterior;
        if (GUI.Button(new Rect(ponto.x - 13f, ponto.y - 13f, 26f, 26f), GUIContent.none, GUIStyle.none))
        {
            if (missil)
            {
                AoSelecionarMissilCarta(id);
            }
            else
            {
                SelecionarUnidadeCarta(id, Event.current.control, Event.current.shift);
            }
        }
        GUI.Label(new Rect(ponto.x + 8f, ponto.y - 10f, 190f, 22f), texto, selecionado ? designerStatus : (cor.r > 0.8f ? designerStatusAlerta : designerPequeno));
    }

    private void DesenharContatoCartaFallback(Rect mapa, Vector2 ponto, GerenciadorQuartel.ContatoMilitarQuartelV2 contato)
    {
        float tamanho = contato.id == quartel.AlvoSelecionadoLancamentoId ? 13f : 10f;
        Color anterior = GUI.color;
        GUI.color = new Color(1f, 0.12f, 0.10f, 1f);
        GUI.DrawTexture(new Rect(ponto.x - tamanho * 0.5f, ponto.y - tamanho * 0.5f, tamanho, tamanho), Texture2D.whiteTexture);
        GUI.color = anterior;
        if (GUI.Button(new Rect(ponto.x - 14f, ponto.y - 14f, 28f, 28f), GUIContent.none, GUIStyle.none))
        {
            AoSelecionarContatoCarta(contato.id);
        }
        GUI.Label(new Rect(ponto.x + 8f, ponto.y - 10f, 210f, 22f), "E-3 " + contato.nome, contato.id == quartel.AlvoSelecionadoLancamentoId ? designerStatus : designerStatusAlerta);
    }

    private void DesenharPainelAtaqueDesigner(Rect area)
    {
        if (quartel == null || !quartel.habilitarLancamentoCoordenado) return;

        GUI.Box(area, GUIContent.none, designerCartao);
        float margem = 12f;
        float espacamento = 10f;
        float conteudoY = area.y + 34f;
        float rodapeY = area.yMax - 34f;
        float larguraEsquerda = Mathf.Clamp(area.width * 0.53f, 360f, area.width - 250f);
        float larguraControle = area.width - larguraEsquerda - margem * 2f - espacamento;
        if (larguraControle < 230f)
        {
            larguraControle = Mathf.Max(230f, area.width * 0.42f);
            larguraEsquerda = area.width - larguraControle - margem * 2f - espacamento;
        }

        GUI.Label(new Rect(area.x + margem, area.y + 7f, larguraEsquerda, 22f), "LANÇAMENTO COORDENADO", designerSecao);
        GUI.Label(new Rect(area.x + larguraEsquerda + margem + espacamento, area.y + 8f, larguraControle, 20f),
            "MODO ATUAL: " + (quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico ? "AUTOMÁTICO" : "MANUAL"), designerSubtitulo);

        float xAlvos = area.x + margem;
        float xUnidades = xAlvos + (larguraEsquerda + espacamento) * 0.5f;
        float larguraLista = (larguraEsquerda - espacamento) * 0.5f;
        GUI.Label(new Rect(xAlvos, conteudoY, larguraLista, 18f), "◉  CONTATOS E-3 / ALVO", designerRotulo);
        GUI.Label(new Rect(xUnidades, conteudoY, larguraLista, 18f), "⚓  LANÇADORES / MODO", designerRotulo);

        int alvosVisiveis = Mathf.Min(3, quartel.AlvosLancamento.Count);
        for (int i = 0; i < alvosVisiveis; i++)
        {
            GerenciadorQuartel.AlvoLancamentoCoordenadoV2 alvo = quartel.AlvosLancamento[i];
            if (alvo == null) continue;
            float linhaY = conteudoY + 21f + i * 36f;
            string prefixo = alvo.id == quartel.AlvoSelecionadoLancamentoId ? "● " : "○ ";
            string texto = prefixo + EncurtarTextoDesigner(alvo.nome, 21) + "\n" + EncurtarTextoDesigner(alvo.tipo, 23);
            if (GUI.Button(new Rect(xAlvos, linhaY, larguraLista, 34f), texto, alvo.id == quartel.AlvoSelecionadoLancamentoId ? designerBotaoAtivo : designerBotaoLista))
            {
                AoSelecionarContatoCarta(alvo.id);
            }
        }
        if (alvosVisiveis == 0)
            GUI.Label(new Rect(xAlvos, conteudoY + 23f, larguraLista, 46f), "Aguardando\ntransmissão do Boeing E-3.", designerPequeno);

        int unidadesVisiveis = Mathf.Min(3, quartel.UnidadesLancamento.Count);
        for (int i = 0; i < unidadesVisiveis; i++)
        {
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = quartel.UnidadesLancamento[i];
            if (unidade == null) continue;
            float linhaY = conteudoY + 21f + i * 36f;
            float larguraSelecao = larguraLista * 0.62f;
            string prefixo = unidade.selecionada ? "☑ " : "☐ ";
            if (GUI.Button(new Rect(xUnidades, linhaY, larguraSelecao - 3f, 30f), prefixo + EncurtarTextoDesigner(unidade.nome, 16), unidade.selecionada ? designerBotaoAtivo : designerBotaoLista))
            {
                SelecionarUnidadeCarta(unidade.id, Event.current.control, Event.current.shift);
            }
            bool eLancadorEstrategico = unidade.lancadorMisseis != null;
            string rotuloModo = eLancadorEstrategico ? "MANUAL · MÍSSEIS" : unidade.modoOperacional;
            if (GUI.Button(new Rect(xUnidades + larguraSelecao, linhaY, larguraLista - larguraSelecao, 30f), rotuloModo, designerBotaoCompacto))
            {
                if (eLancadorEstrategico)
                    quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual);
                else
                    quartel.AlternarModoOperacionalLancador(unidade.id);
            }
        }
        if (unidadesVisiveis == 0)
            GUI.Label(new Rect(xUnidades, conteudoY + 23f, larguraLista, 46f), "Nenhum navio, submarino\nou lançador de mísseis.", designerPequeno);

        float xControle = area.x + larguraEsquerda + margem + espacamento;
        GUI.Label(new Rect(xControle, conteudoY, larguraControle, 18f), "⌖  CONTROLE DE ATAQUE", designerRotulo);
        float larguraModo = (larguraControle - 6f) * 0.5f;
        GUIStyle estiloManual = quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual ? designerBotaoAtivo : designerBotaoCompacto;
        GUIStyle estiloAutomatico = quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico ? designerBotaoAtivo : designerBotaoCompacto;
        if (GUI.Button(new Rect(xControle, conteudoY + 21f, larguraModo, 29f), "MANUAL", estiloManual))
            quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual);
        if (GUI.Button(new Rect(xControle + larguraModo + 6f, conteudoY + 21f, larguraModo, 29f), "AUTOMÁTICO", estiloAutomatico))
            quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico);

        float larguraCampo = (larguraControle - 10f) / 3f;
        float camposY = conteudoY + 55f;
        GUI.Label(new Rect(xControle, camposY, larguraCampo, 15f), "X", designerRotulo);
        GUI.Label(new Rect(xControle + larguraCampo + 5f, camposY, larguraCampo, 15f), "Y", designerRotulo);
        GUI.Label(new Rect(xControle + (larguraCampo + 5f) * 2f, camposY, larguraCampo, 15f), "Z", designerRotulo);
        coordenadaXFallback = GUI.TextField(new Rect(xControle, camposY + 14f, larguraCampo, 25f), coordenadaXFallback);
        coordenadaYFallback = GUI.TextField(new Rect(xControle + larguraCampo + 5f, camposY + 14f, larguraCampo, 25f), coordenadaYFallback);
        coordenadaZFallback = GUI.TextField(new Rect(xControle + (larguraCampo + 5f) * 2f, camposY + 14f, larguraCampo, 25f), coordenadaZFallback);

        float acaoY = camposY + 44f;
        float larguraAcao = (larguraControle - 6f) * 0.5f;
        if (GUI.Button(new Rect(xControle, acaoY, larguraAcao, 29f), "＋  INSERIR XYZ", designerBotaoCompacto))
            InserirCoordenadasFallback();
        if (GUI.Button(new Rect(xControle + larguraAcao + 6f, acaoY, larguraAcao, 29f), cliqueTerrenoArmado ? "●  CLIQUE ARMADO" : "⌖  CLICAR TERRENO", cliqueTerrenoArmado ? designerBotaoAtivo : designerBotaoCompacto))
            cliqueTerrenoArmado = !cliqueTerrenoArmado;

        string validacao = "VÃO ATIRAR: " + ObterResumoLancadoresSelecionados();
        if (quartel.AvaliacoesLancamento.Count > 0)
        {
            GerenciadorQuartel.AvaliacaoLancamentoCoordenadoV2 avaliacao = quartel.AvaliacoesLancamento[0];
            validacao = EncurtarTextoDesigner(avaliacao.unidadeNome + ": " + avaliacao.motivo, 78);
            if (quartel.AvaliacoesLancamento.Count > 1) validacao += "  |  +" + (quartel.AvaliacoesLancamento.Count - 1) + " unidade(s)";
        }
        else if (!quartel.AlvoLancamentoSelecionadoValido)
        {
            validacao += " | selecione um alvo E-3";
        }
        if (!string.IsNullOrWhiteSpace(statusLancamentoFallback))
            validacao = statusLancamentoFallback;
        float larguraLancarRodape = 102f;
        float larguraRodape = 78f;
        float xLancarRodape = area.xMax - (larguraLancarRodape + larguraRodape * 2f + 20f);
        GUI.Label(new Rect(area.x + margem, rodapeY, Mathf.Max(160f, xLancarRodape - area.x - margem - 8f), 22f), validacao, designerPequeno);
        if (GUI.Button(new Rect(xLancarRodape, rodapeY - 3f, larguraLancarRodape, 29f), "▶  LANÇAR", designerBotaoAtivo))
        {
            statusLancamentoFallback = "PREPARANDO LANÇAMENTO...";
            string motivo;
            bool lancou = quartel.TryExecutarLancamentoCoordenado(out motivo);
            statusLancamentoFallback = lancou
                ? "MÍSSIL LANÇADO | " + motivo
                : "LANÇAMENTO BLOQUEADO | " + motivo;
            AtualizarPainel();
        }
        if (GUI.Button(new Rect(xLancarRodape + larguraLancarRodape + 6f, rodapeY - 3f, larguraRodape, 29f), "×  CANCELAR", designerBotaoCompacto))
        {
            quartel.CancelarOperacaoLancamento();
            cliqueTerrenoArmado = false;
        }
        if (GUI.Button(new Rect(xLancarRodape + larguraLancarRodape + larguraRodape + 12f, rodapeY - 3f, larguraRodape, 29f), "◎  CENTRAR", designerBotaoCompacto))
        {
            if (cartaTerrenoRenderer != null) cartaTerrenoRenderer.SolicitarCentralizacao(quartel.transform.position);
        }
    }

    private string EncurtarTextoDesigner(string texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= maximo) return texto ?? string.Empty;
        return texto.Substring(0, Mathf.Max(1, maximo - 1)) + "…";
    }

    private void InserirCoordenadasFallback()
    {
        float x;
        float y;
        float z;
        if (!float.TryParse(coordenadaXFallback, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            || !float.TryParse(coordenadaYFallback, NumberStyles.Float, CultureInfo.InvariantCulture, out y)
            || !float.TryParse(coordenadaZFallback, NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            return;
        quartel.DefinirPontoAlvoManual(new Vector3(x, y, z), "COORDENADAS DIGITADAS");
    }

    private Vector2 PontoCarta(Rect mapa, Vector3 posicao, float raio)
    {
        Vector3 viewport;
        if (cartaTerrenoRenderer != null
            && cartaTerrenoRenderer.TryWorldToViewport(posicao, out viewport)
            && viewport.z > 0f)
        {
            return new Vector2(
                mapa.x + Mathf.Clamp01(viewport.x) * mapa.width,
                mapa.y + (1f - Mathf.Clamp01(viewport.y)) * mapa.height);
        }

        Vector3 local = (quartel != null ? quartel.transform : transform).InverseTransformPoint(posicao);
        float x = mapa.x + Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * mapa.width;
        float y = mapa.y + (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * mapa.height;
        return new Vector2(x, y);
    }

    private void DesenharSegmentoCarta(Vector2 inicio, Vector2 fim, Color cor, float espessura)
    {
        Vector2 delta = fim - inicio;
        float comprimento = delta.magnitude;
        if (comprimento < 0.5f) return;
        Matrix4x4 matrizAnterior = GUI.matrix;
        Color corAnterior = GUI.color;
        GUI.color = cor;
        float angulo = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angulo, inicio);
        GUI.DrawTexture(new Rect(inicio.x, inicio.y - espessura * 0.5f, comprimento, espessura), Texture2D.whiteTexture);
        GUI.matrix = matrizAnterior;
        GUI.color = corAnterior;
    }

    private static Color CorTipoCarta(string tipo)
    {
        if (tipo == "AEREO") return new Color(0.35f, 0.72f, 1f);
        if (tipo == "NAVAL") return new Color(0.20f, 0.92f, 0.90f);
        if (tipo == "VEICULO") return new Color(0.42f, 0.88f, 0.45f);
        return new Color(0.78f, 0.86f, 0.30f);
    }

    private static string FormatarPosicao(Vector3 posicao)
    {
        return "X " + posicao.x.ToString("0") + " | Y " + posicao.y.ToString("0") + " | Z " + posicao.z.ToString("0");
    }

    private static string FormatarTempo(float segundos)
    {
        if (segundos <= 0f) return "N/A";
        TimeSpan tempo = TimeSpan.FromSeconds(segundos);
        return tempo.TotalHours >= 1d ? tempo.ToString(@"hh\:mm\:ss") : tempo.ToString(@"mm\:ss");
    }

    private void DesenharArsenalDesigner(Rect area)
    {
        long dinheiro = GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : -1L;
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "MISSEIS", "MUNICAO", "FUNDO", "FOLHA" },
            new[] { (quartel?.misseisArmazenados ?? 0).ToString("N0"), (quartel?.municaoArmazenada ?? 0).ToString("N0"), dinheiro >= 0 ? "$" + dinheiro.ToString("N0") : "n/d", "$" + (snapshot?.custoFolhaDiario ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "REABASTECER ARSENAL", designerSecao); y += 34f;
        if (GUI.Button(new Rect(0f, y, area.width, 36f), "ENCOMENDAR 10 MISSEIS  (-$" + (quartel?.precoMissil ?? 0).ToString("N0") + ")", designerBotao)) quartel?.TentarEncomendarMisseis();
        y += 44f;
        if (GUI.Button(new Rect(0f, y, area.width, 36f), "ENCOMENDAR 100 PACOTES  (-$" + (quartel?.precoMunicao ?? 0).ToString("N0") + ")", designerBotao)) quartel?.TentarEncomendarMunicao();
        y += 58f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FONTE", "GerenciadorRecursos atual"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO", "ESTOQUE PERSISTENTE");
    }

    private void FecharInterfaceDesigner()
    {
        if (quartel != null) quartel.FecharInterfacePorUI();
        else FecharInterno();
    }

    private void Awake()
    {
        quartel = GetComponent<GerenciadorQuartel>();
        administracao = quartel != null ? quartel.ObterAdministracao() : GetComponent<QuartelAdministracaoRuntime>();
        GarantirCartaTopograficaView();
        GarantirCartaTerrenoRenderer();
        InicializarDocumento();
        if (Application.isPlaying)
        {
            Debug.Log($"[QuartelUI] Awake: objeto={name}, pronto={pronto}, root={(root != null)}, documento={(documento != null)}, administracao={(administracao != null)}", this);
        }
    }

    private void Update()
    {
        if (!aberto)
        {
            return;
        }

        if (cartaTerrenoRenderer != null)
        {
            cartaTerrenoRenderer.DefinirAtualizacaoContinua(abaAtual == 7);
        }

        if (documento != null && documento.enabled)
        {
            DimensionarRootDocumento();
        }

        ProcessarEntradaToolkitFallback();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (quartel != null)
            {
                quartel.FecharInterfacePorUI();
            }
            else
            {
                FecharInterno();
            }
            return;
        }

        if (Time.unscaledTime < proximaAtualizacao)
        {
            return;
        }

        proximaAtualizacao = Time.unscaledTime + 0.75f;
        AtualizarPainel();
    }

    private void RegistrarAcaoBotao(Button botao, Action acao)
    {
        if (botao == null || acao == null)
        {
            return;
        }

        acoesBotoesRuntime[botao] = acao;
        botao.clicked += () => ExecutarAcaoBotao(botao);
        botao.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt != null && evt.button == 0)
            {
                ExecutarAcaoBotao(botao);
                evt.StopPropagation();
            }
        });
    }

    private void ExecutarAcaoBotao(Button botao)
    {
        if (botao == null || !botao.enabledInHierarchy)
        {
            return;
        }

        // PointerDown, ClickEvent e a rota de Input podem ocorrer no mesmo
        // frame. O comando continua sendo unico mesmo quando mais de uma
        // dessas rotas estiver ativa.
        if (ultimoFrameBotaoExecutado == Time.frameCount && ultimoBotaoExecutado == botao)
        {
            return;
        }

        ultimoFrameBotaoExecutado = Time.frameCount;
        ultimoBotaoExecutado = botao;
        Action acao;
        if (acoesBotoesRuntime.TryGetValue(botao, out acao))
        {
            acao.Invoke();
        }
    }

    private void ProcessarEntradaToolkitFallback()
    {
        Vector2 posicaoTela;
        if (!TryObterCliqueEsquerdo(out posicaoTela))
        {
            return;
        }

        if (root == null || root.panel == null)
        {
            return;
        }

        Vector2 posicaoPainel = RuntimePanelUtils.ScreenToPanel(root.panel, posicaoTela);
        ProcessarCliquePainel(posicaoPainel);
    }

    private bool TryObterCliqueEsquerdo(out Vector2 posicaoTela)
    {
        posicaoTela = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            posicaoTela = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private void ProcessarCliquePainel(Vector2 posicaoPainel)
    {
        if (root == null || root.panel == null)
        {
            return;
        }

        VisualElement alvo = root.panel.Pick(posicaoPainel);
        Button botao = alvo as Button;
        if (botao == null && alvo != null)
        {
            botao = alvo.GetFirstAncestorOfType<Button>();
        }

        if (botao != null)
        {
            ExecutarAcaoBotao(botao);
            return;
        }

        // TextField recebe foco mesmo quando o clique nao virou PointerDown
        // no painel. Isso permite preencher XYZ e colar a telemetria sem
        // obrigar o operador a clicar varias vezes no campo.
        TextField[] campos = { campoCoordenadaX, campoCoordenadaY, campoCoordenadaZ };
        for (int i = 0; i < campos.Length; i++)
        {
            TextField campo = campos[i];
            if (campo != null && campo.worldBound.Contains(posicaoPainel))
            {
                campo.Focus();
                return;
            }
        }
    }

    private void GarantirCartaTopograficaView()
    {
        if (cartaTopograficaView == null)
        {
            cartaTopograficaView = GetComponent<QuartelCartaTopograficaView>();
            if (cartaTopograficaView == null) cartaTopograficaView = gameObject.AddComponent<QuartelCartaTopograficaView>();
        }
    }

    private void GarantirCartaTerrenoRenderer()
    {
        if (cartaTerrenoRenderer != null) return;
        cartaTerrenoRenderer = GetComponent<CartaTerrenoRenderer>();
        if (cartaTerrenoRenderer == null)
            cartaTerrenoRenderer = gameObject.AddComponent<CartaTerrenoRenderer>();
    }

    private void OnDestroy()
    {
        InteractionModeService.Release(this, InteractionOwner.QuartelMenu);
        if (painelAberto == this)
        {
            painelAberto = null;
        }

        if (panelSettingsRuntime != null)
        {
            Destroy(panelSettingsRuntime);
            panelSettingsRuntime = null;
        }
    }

    private bool ReanexarRootDocumento()
    {
        if (documento == null)
        {
            return false;
        }

        // UIDocument pode trocar o root ao sair de um domínio/recarregar
        // scripts. Reaproveita o mesmo documento e reconstrói somente a
        // árvore UI perdida, evitando cair silenciosamente no menu IMGUI.
        bool estavaAtivo = documento.enabled;
        documento.enabled = true;
        VisualElement rootAtual = documento.rootVisualElement;
        if (rootAtual == null)
        {
            documento.enabled = estavaAtivo;
            return false;
        }

        if (root != rootAtual || root.childCount == 0)
        {
            root = rootAtual;
            ConstruirLayout();
            root.style.display = DisplayStyle.None;
        }

        documento.enabled = estavaAtivo;
        return root != null;
    }

    public void Abrir()
    {
        if (quartel == null)
        {
            quartel = GetComponent<GerenciadorQuartel>();
        }

        if (!pronto)
        {
            InicializarDocumento();
        }

        if (pronto && (root == null || documento == null || documento.rootVisualElement == null))
        {
            ReanexarRootDocumento();
        }

        if (!pronto || root == null)
        {
            Debug.LogWarning($"[QuartelUI] abertura recusada: pronto={pronto}, root={(root != null)}, documento={(documento != null)}, painelSettings={(panelSettingsRuntime != null)}", this);
            return;
        }

        if (painelAberto != null && painelAberto != this)
        {
            QuartelMenuUIController anterior = painelAberto;
            GerenciadorQuartel quartelAnterior = anterior.quartel;
            anterior.FecharInterno();
            if (quartelAnterior != null && quartelAnterior != quartel)
            {
                quartelAnterior.FecharInterfacePorUI();
            }
        }

        painelAberto = this;
        aberto = true;
        frameAbertura = Time.frameCount;
        ultimoFrameEntradaConsumida = -1;
        fallbackRendererDecidido = false;
        usarFallbackRenderer = false;
        arrastandoCartaFallback = false;
        cartaFallbackFoiArrastada = false;
        SolicitarBloqueioInput();
        documento.enabled = true;
        // Em alguns Game Views embutidos o PanelSettings nao calcula o
        // worldBound do root imediatamente. Fixar o viewport no momento da
        // abertura evita NaN e mantem o overlay visivel no editor e no jogo.
        DimensionarRootDocumento();
        root.pickingMode = PickingMode.Position;
        root.style.display = DisplayStyle.Flex;
        proximaAtualizacao = 0f;
        AtualizarPainel();
        Debug.Log($"[QuartelUI] painel aberto: objeto={name}, root={root.name}, documentoAtivo={documento.enabled}, snapshotAeronaves={(snapshot != null ? snapshot.aeronavesNoRaio : 0)}, tamanho={root.resolvedStyle.width:0}x{root.resolvedStyle.height:0}, visibilidade={root.resolvedStyle.visibility}, opacidade={root.resolvedStyle.opacity:0.00}", this);
    }

    public void FecharInterno()
    {
        if (aberto)
        {
            ultimoFrameEntradaConsumida = Time.frameCount;
        }

        aberto = false;
        if (painelAberto == this)
        {
            painelAberto = null;
        }

        if (root != null)
        {
            // Impede que o PanelEventHandler retenha a camada da Carta sob o
            // ponteiro depois do fechamento do modal.
            root.pickingMode = PickingMode.Ignore;
            root.style.display = DisplayStyle.None;
        }

        if (documento != null)
        {
            if (documento.rootVisualElement != null && documento.rootVisualElement.panel != null)
            {
                Focusable elementoFocado = documento.rootVisualElement.panel.focusController.focusedElement;
                if (elementoFocado != null) elementoFocado.Blur();
            }
            documento.enabled = false;
        }

        // Evita que um campo de coordenadas ou um botao do UI Toolkit deixe
        // foco no EventSystem depois do fechamento. O foco preso fazia o
        // primeiro clique no mundo parecer ignorado mesmo com o painel oculto.
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        cliqueTerrenoArmado = false;

        if (cartaTerrenoRenderer != null)
        {
            cartaTerrenoRenderer.DefinirAtualizacaoContinua(false);
        }

        InteractionModeService.Release(this, InteractionOwner.QuartelMenu);
    }

    private void SolicitarBloqueioInput()
    {
        InteractionModeService.Request(
            this,
            InteractionOwner.QuartelMenu,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = true,
                consomeLMB = true,
                consomeRMB = true
            },
            "Menu Quartel aberto");
    }

    private void InicializarDocumento()
    {
        if (pronto)
        {
            return;
        }

        documento = GetComponent<UIDocument>();
        if (documento == null)
        {
            documento = gameObject.AddComponent<UIDocument>();
        }

        PanelSettings baseSettings = Resources.Load<PanelSettings>("PanelSettings");
        if (baseSettings != null)
        {
            panelSettingsRuntime = Instantiate(baseSettings);
        }
        else
        {
            panelSettingsRuntime = ScriptableObject.CreateInstance<PanelSettings>();
        }

        panelSettingsRuntime.sortingOrder = 1200;
        documento.panelSettings = panelSettingsRuntime;
        // Um UIDocument criado em runtime precisa de um VisualTreeAsset
        // válido para anexar o root ao PanelSettings em todas as Game Views.
        // O conteúdo desse asset é limpo logo abaixo e substituído pelo
        // layout próprio do Quartel; ele serve apenas como âncora válida do
        // painel e não introduz uma segunda interface visual.
        documento.visualTreeAsset = Resources.Load<VisualTreeAsset>("MenuComando/MenuComando");
        documento.enabled = true;
        root = documento.rootVisualElement;
        if (root == null)
        {
            documento.enabled = false;
            return;
        }

        ConstruirLayout();
        root.style.display = DisplayStyle.None;
        documento.enabled = false;
        pronto = true;
        Debug.Log($"[QuartelUI] documento inicializado: objeto={name}, root={(root != null)}, panelSettings={(panelSettingsRuntime != null)}, sorting={(panelSettingsRuntime != null ? panelSettingsRuntime.sortingOrder : -1)}", this);
    }

    private void ConstruirLayout()
    {
        root.Clear();
        acoesBotoesRuntime.Clear();
        ultimoBotaoExecutado = null;
        ultimoFrameBotaoExecutado = -1;
        // O root do UIDocument ja recebe o tamanho do PanelSettings. Deixa-lo
        // como elemento absoluto pode zerar o worldBound em alguns modos de
        // renderizacao do Unity e fazer o menu existir sem aparecer.
        root.style.flexGrow = 1;
        root.pickingMode = PickingMode.Position;

        overlay = new VisualElement { name = "quartel-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = CorFundoQuartel;
        overlay.style.paddingLeft = 18;
        overlay.style.paddingRight = 18;
        overlay.style.paddingTop = 18;
        overlay.style.paddingBottom = 18;
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);

        painel = new VisualElement { name = "quartel-painel" };
        painel.style.width = new Length(94f, LengthUnit.Percent);
        painel.style.maxWidth = 1500;
        painel.style.height = new Length(94f, LengthUnit.Percent);
        painel.style.maxHeight = 930;
        painel.style.flexDirection = FlexDirection.Row;
        painel.style.backgroundColor = CorPainelQuartel;
        painel.style.borderTopWidth = 2;
        painel.style.borderBottomWidth = 2;
        painel.style.borderLeftWidth = 2;
        painel.style.borderRightWidth = 2;
        painel.style.borderTopColor = CorBordaQuartel;
        painel.style.borderBottomColor = CorBordaQuartel;
        painel.style.borderLeftColor = CorBordaQuartel;
        painel.style.borderRightColor = CorBordaQuartel;
        painel.style.borderTopLeftRadius = 6;
        painel.style.borderTopRightRadius = 6;
        painel.style.borderBottomLeftRadius = 6;
        painel.style.borderBottomRightRadius = 6;
        overlay.Add(painel);

        VisualElement lateral = new VisualElement { name = "quartel-navegacao-lateral" };
        lateral.style.width = 226;
        lateral.style.flexShrink = 0;
        lateral.style.flexDirection = FlexDirection.Column;
        lateral.style.backgroundColor = CorLateralQuartel;
        lateral.style.borderRightWidth = 1;
        lateral.style.borderRightColor = CorBordaQuartel;
        painel.Add(lateral);

        VisualElement marca = new VisualElement();
        marca.style.paddingLeft = 18;
        marca.style.paddingRight = 12;
        marca.style.paddingTop = 18;
        marca.style.paddingBottom = 16;
        marca.style.backgroundColor = CorCabecalhoQuartel;
        marca.Add(Texto("◆  QUARTEL", 21, CorTextoQuartel, FontStyle.Bold));
        marca.Add(Texto("COMANDO MILITAR", 10, CorCianoQuartel, FontStyle.Bold));
        lateral.Add(marca);

        ScrollView abasScroll = new ScrollView(ScrollViewMode.Vertical);
        abasScroll.style.flexGrow = 1;
        abasScroll.style.paddingLeft = 8;
        abasScroll.style.paddingRight = 8;
        abasScroll.style.paddingTop = 12;
        abasScroll.style.paddingBottom = 12;
        VisualElement abas = new VisualElement { name = "quartel-abas" };
        abasScroll.Add(abas);
        lateral.Add(abasScroll);

        for (int i = 0; i < nomesAbas.Length; i++)
        {
            int indice = i;
            Button aba = CriarBotaoAbaToolkit(i);
            aba.style.marginTop = 2;
            aba.style.marginBottom = 4;
            RegistrarAcaoBotao(aba, () => SelecionarAba(indice));
            botoesAbas.Add(aba);
            abas.Add(aba);
        }

        VisualElement pais = new VisualElement();
        pais.style.marginLeft = 12;
        pais.style.marginRight = 12;
        pais.style.marginBottom = 14;
        pais.style.paddingLeft = 12;
        pais.style.paddingTop = 10;
        pais.style.paddingBottom = 10;
        pais.style.backgroundColor = CorCartaoQuartel;
        pais.Add(Texto("REPUBLICA ATLANTICA", 10, CorTextoSecundarioQuartel, FontStyle.Bold));
        pais.Add(Texto("COMANDO CONJUNTO", 10, CorTextoSecundarioQuartel, FontStyle.Normal));
        lateral.Add(pais);

        VisualElement principal = new VisualElement { name = "quartel-conteudo-principal" };
        principal.style.flexGrow = 1;
        principal.style.flexDirection = FlexDirection.Column;
        painel.Add(principal);

        VisualElement cabecalho = Linha();
        cabecalho.style.paddingLeft = 16;
        cabecalho.style.paddingRight = 12;
        cabecalho.style.paddingTop = 10;
        cabecalho.style.paddingBottom = 10;
        cabecalho.style.backgroundColor = CorCabecalhoQuartel;

        VisualElement blocoTitulo = new VisualElement();
        blocoTitulo.style.flexGrow = 1;
        titulo = Texto("QUARTEL  —  COMANDO MILITAR", 23, CorTextoQuartel, FontStyle.Bold);
        subtitulo = Texto("ESTADO-MAIOR NACIONAL  |  CENTRO ADMINISTRATIVO", 12, CorTextoSecundarioQuartel, FontStyle.Normal);
        blocoTitulo.Add(titulo);
        blocoTitulo.Add(subtitulo);
        cabecalho.Add(blocoTitulo);

        status = Texto("●  SISTEMA PRONTO", 13, CorVerdeQuartel, FontStyle.Bold);
        status.style.marginRight = 18;
        cabecalho.Add(status);

        Button fechar = Botao("×  FECHAR", 112, 34, new Color(0.30f, 0.075f, 0.10f, 1f));
        RegistrarAcaoBotao(fechar, () =>
        {
            if (quartel != null) quartel.FecharInterfacePorUI();
            else FecharInterno();
        });
        cabecalho.Add(fechar);
        principal.Add(cabecalho);

        metricas = Texto(string.Empty, 12, CorTextoQuartel, FontStyle.Bold);
        metricas.style.paddingLeft = 16;
        metricas.style.paddingTop = 8;
        metricas.style.paddingBottom = 8;
        metricas.style.backgroundColor = CorNavegacaoQuartel;
        metricas.style.whiteSpace = WhiteSpace.Normal;
        principal.Add(metricas);

        conteudo = new ScrollView(ScrollViewMode.Vertical) { name = "quartel-conteudo" };
        conteudo.style.flexGrow = 1;
        conteudo.style.paddingLeft = 14;
        conteudo.style.paddingRight = 14;
        conteudo.style.paddingTop = 12;
        conteudo.style.paddingBottom = 12;
        // O conteúdo precisa crescer para baixo e ser rolável; se o
        // contentContainer puder encolher, o cabeçalho da Carta fica por
        // baixo do mapa e os controles/telemetria são cortados.
        conteudo.contentContainer.style.flexShrink = 0;
        conteudo.contentContainer.style.flexDirection = FlexDirection.Column;
        conteudo.contentContainer.style.alignItems = Align.Stretch;
        principal.Add(conteudo);

        Label rodape = Texto("Acoes passam pelo GerenciadorQuartel. Carta, telemetria e ordens permanecem sincronizadas com o mundo.", 11, CorTextoSecundarioQuartel, FontStyle.Normal);
        rodape.style.paddingLeft = 14;
        rodape.style.paddingTop = 7;
        rodape.style.paddingBottom = 7;
        rodape.style.backgroundColor = CorLateralQuartel;
        principal.Add(rodape);

        SelecionarAba(0);
    }

    private Button CriarBotaoAbaToolkit(int indice)
    {
        Button botao = Botao(string.Empty, 0, 48, CorNavegacaoQuartel);
        botao.name = "quartel-aba-" + indice;
        botao.style.flexDirection = FlexDirection.Row;
        botao.style.alignItems = Align.Center;
        botao.style.justifyContent = Justify.FlexStart;
        botao.style.paddingLeft = 8;
        botao.style.paddingRight = 8;

        Label rotuloAutomatico = botao.Q<Label>(className: "unity-button__text");
        if (rotuloAutomatico != null)
        {
            rotuloAutomatico.style.display = DisplayStyle.None;
        }

        VisualElement linha = new VisualElement { name = "quartel-aba-conteudo" };
        linha.style.flexDirection = FlexDirection.Row;
        linha.style.alignItems = Align.Center;
        linha.style.flexGrow = 1;
        linha.pickingMode = PickingMode.Ignore;

        VisualElement badgeIcone = new VisualElement { name = "quartel-aba-icone-badge" };
        badgeIcone.style.width = 30;
        badgeIcone.style.height = 30;
        badgeIcone.style.flexShrink = 0;
        badgeIcone.style.alignItems = Align.Center;
        badgeIcone.style.justifyContent = Justify.Center;
        badgeIcone.style.backgroundColor = CorBadgeIconeQuartel;
        badgeIcone.style.borderTopWidth = 1;
        badgeIcone.style.borderBottomWidth = 1;
        badgeIcone.style.borderLeftWidth = 1;
        badgeIcone.style.borderRightWidth = 1;
        badgeIcone.style.borderTopColor = CorBordaQuartel;
        badgeIcone.style.borderBottomColor = CorBordaQuartel;
        badgeIcone.style.borderLeftColor = CorBordaQuartel;
        badgeIcone.style.borderRightColor = CorBordaQuartel;
        badgeIcone.style.borderTopLeftRadius = 5;
        badgeIcone.style.borderTopRightRadius = 5;
        badgeIcone.style.borderBottomLeftRadius = 5;
        badgeIcone.style.borderBottomRightRadius = 5;

        Label icone = Texto(SimboloNavegacao(indice), 17, CorCianoQuartel, FontStyle.Bold);
        icone.name = "quartel-aba-icone";
        icone.style.width = 30;
        icone.style.height = 30;
        icone.style.unityTextAlign = TextAnchor.MiddleCenter;
        icone.pickingMode = PickingMode.Ignore;
        badgeIcone.Add(icone);

        Label texto = Texto(nomesAbas[indice], 12, CorTextoSecundarioQuartel, FontStyle.Bold);
        texto.name = "quartel-aba-texto";
        texto.style.flexGrow = 1;
        texto.style.marginLeft = 6;
        texto.style.whiteSpace = WhiteSpace.Normal;
        texto.style.unityTextAlign = TextAnchor.MiddleLeft;
        texto.pickingMode = PickingMode.Ignore;

        linha.Add(badgeIcone);
        linha.Add(texto);
        botao.Add(linha);
        if (indice >= 0 && indice < descricoesNavegacaoDesigner.Length)
        {
            botao.tooltip = descricoesNavegacaoDesigner[indice];
        }
        return botao;
    }

    private void SelecionarAba(int indice)
    {
        abaAtual = Mathf.Clamp(indice, 0, nomesAbas.Length - 1);
        for (int i = 0; i < botoesAbas.Count; i++)
        {
            bool ativo = i == abaAtual;
            botoesAbas[i].style.backgroundColor = ativo
                ? CorNavegacaoAtivaQuartel
                : CorNavegacaoQuartel;
            VisualElement badgeIcone = botoesAbas[i].Q<VisualElement>("quartel-aba-icone-badge");
            Label icone = botoesAbas[i].Q<Label>("quartel-aba-icone");
            Label texto = botoesAbas[i].Q<Label>("quartel-aba-texto");
            if (badgeIcone != null) badgeIcone.style.backgroundColor = ativo ? CorBadgeIconeAtivoQuartel : CorBadgeIconeQuartel;
            if (icone != null) icone.style.color = ativo ? Color.white : CorCianoQuartel;
            if (texto != null) texto.style.color = ativo ? Color.white : CorTextoSecundarioQuartel;
        }
        AtualizarPainel();
    }

    private void AtualizarPainel()
    {
        if (!pronto || quartel == null || conteudo == null)
        {
            return;
        }

        administracao = quartel.ObterAdministracao();
        snapshot = administracao != null ? administracao.ObterSnapshot() : null;
        GarantirCartaTopograficaView();
        if (cartaTopograficaView != null)
        {
            float raioCarta = Mathf.Max(100f, quartel.raioDeCobertura);
            cartaTopograficaView.Modo = cartaVista3D
                ? QuartelCartaTopograficaView.ModoVisualizacao.Topografico3D
                : QuartelCartaTopograficaView.ModoVisualizacao.Topografico2D;
            cartaTopograficaView.Atualizar(quartel.transform, quartel.teamID, raioCarta, abaAtual == 7);
        }
        if (abaAtual == 7)
        {
            quartel.AtualizarDadosLancamento();
        }

        int soldados = quartel.soldadosNoDormitorio != null ? quartel.soldadosNoDormitorio.Count : 0;
        int veiculos = quartel.veiculosNoQuartel != null ? quartel.veiculosNoQuartel.Count : 0;
        metricas.text = $"RESERVA: {soldados} soldados  |  {veiculos} veiculos  |  ATIVOS NACIONAIS: {snapshot?.militaresAtivos ?? 0}  |  UNIDADES NA COBERTURA: {snapshot?.unidadesNoRaio ?? 0}  |  AERONAVES CONECTADAS: {snapshot?.aeronavesNoRaio ?? 0}  |  ARSENAL: {quartel.misseisArmazenados} misseis / {quartel.municaoArmazenada} pacotes  |  COBERTURA: {quartel.raioDeCobertura:0} m";
        titulo.text = "QUARTEL GERAL  |  " + quartel.name.ToUpperInvariant();
        subtitulo.text = "ESTADO-MAIOR NACIONAL  |  CENTRO ADMINISTRATIVO";
        status.text = quartel.modoDefensivoAtivo ? "●  DEFESA AUTOMATICA ATIVA" : "●  SISTEMA OPERACIONAL";
        status.style.color = quartel.modoDefensivoAtivo ? CorAlertaQuartel : CorVerdeQuartel;

        if (abaAtual == 7 && paginaConstruida == 7 && cartaPersistenteConstruida)
        {
            AtualizarCartaPersistente();
            AtualizarPainelLancamentoCoordenado();
            return;
        }

        if (paginaConstruida == 7 && abaAtual != 7)
        {
            cartaPersistenteConstruida = false;
            mapaCartaPersistente = null;
            camadaMarcadoresCarta = null;
            camadaTrajetoriasCarta = null;
            telemetriaCartaPersistente = null;
            painelLancamentoPersistente = null;
            contatosLancamentoPersistente = null;
            contatosLancamentoListaPersistente = null;
            contatosLancamentoVazio = null;
            unidadesLancamentoPersistente = null;
            unidadesLancamentoListaPersistente = null;
            unidadesLancamentoVazio = null;
            validacaoLancamentoPersistente = null;
            estatisticasLancamentoPersistentes = null;
            botaoModoManualLancamento = null;
            botaoModoAutomaticoLancamento = null;
            botaoConfirmarLancamento = null;
            botaoCarta2D = null;
            botaoCarta3D = null;
            telemetriaCartaEstruturaConstruida = false;
            dadosTelemetriaCartaPersistentes = null;
            controlesTelemetriaCartaPersistentes = null;
            tituloTelemetriaCartaPersistente = null;
            textoTelemetriaCartaPersistente = null;
            botaoSelecionarLancadorTelemetria = null;
            botaoUsarCoordenadasTelemetria = null;
            botaoRastrearMissilCarta = null;
            botaoPararRastreamentoMissilCarta = null;
            statusLancamentoCarta = null;
            resumoDestruicoesCarta = null;
            listaEventosCombateCarta = null;
            listaAeronavesCartaPersistente = null;
            listaAeronavesCartaVazia = null;
            botoesAeronavesCarta.Clear();
            marcadoresCarta.Clear();
            trajetoriasCarta.Clear();
            trajetoriasPercorridasCarta.Clear();
            trajetoriasEstimadasCarta.Clear();
            marcadoresTrajetoriaCarta.Clear();
            botoesContatosLancamento.Clear();
            linhasUnidadesLancamento.Clear();
            botoesUnidadesLancamento.Clear();
            botoesModoUnidadesLancamento.Clear();
            botoesEventosCombateCarta.Clear();
            eventosCombateCarta.Clear();
        }
        conteudo.Clear();
        acoesBotoesRuntime.Clear();
        switch (abaAtual)
        {
            case 0: ConstruirAbaTropas(); break;
            case 1: ConstruirAbaEfetivo(); break;
            case 2: ConstruirAbaRecrutamento(); break;
            case 3: ConstruirAbaFolha(); break;
            case 4: ConstruirAbaTripulacoes(); break;
            case 5: ConstruirAbaResgate(); break;
            case 6: ConstruirAbaComunicacoes(); break;
            case 7: ConstruirAbaCartaNautica(); break;
            case 8: ConstruirAbaArsenal(); break;
        }
        paginaConstruida = abaAtual;
    }

    private void ConstruirAbaTropas()
    {
        AdicionarCabecalho("TROPAS", "Recolhimento e emprego das unidades mantidas pelo Quartel.");
        VisualElement resumo = Linha();
        resumo.Add(Cartao("DORMITORIO", (quartel.soldadosNoDormitorio?.Count ?? 0).ToString(), "soldados armazenados"));
        resumo.Add(Cartao("GARAGEM", (quartel.veiculosNoQuartel?.Count ?? 0).ToString(), "veiculos estacionados"));
        resumo.Add(Cartao("NA COBERTURA", snapshot != null ? snapshot.unidadesNoRaio.ToString("N0") : "n/d", "unidades registradas"));
        resumo.Add(Cartao("EM ATIVIDADE", snapshot != null ? snapshot.unidadesEmMissao.ToString("N0") : "n/d", "ordem ou missao ativa"));
        conteudo.Add(resumo);

        VisualElement acoes = Card("ACOES OPERACIONAIS");
        int selecionados = ContarSelecionadosJogador();
        int soldadosArmazenados = quartel.soldadosNoDormitorio?.Count ?? 0;
        int veiculosArmazenados = quartel.veiculosNoQuartel?.Count ?? 0;
        acoes.Add(BotaoAcao("CONVOCAR SELECIONADOS NO MAPA", quartel.SolicitarConvocarSelecionados, false, selecionados > 0, "Nenhuma unidade do jogador selecionada"));
        acoes.Add(BotaoAcao("DESDOBRAR 1 SOLDADO", () => quartel.SolicitarDesdobramentoSoldados(1), false, soldadosArmazenados >= 1, "Nao ha soldado armazenado"));
        acoes.Add(BotaoAcao("DESDOBRAR 5 SOLDADOS", () => quartel.SolicitarDesdobramentoSoldados(5), false, soldadosArmazenados >= 5, "Sao necessarios 5 soldados armazenados"));
        acoes.Add(BotaoAcao("ESVAZIAR DORMITORIO", () => quartel.SolicitarDesdobramentoSoldados(soldadosArmazenados), true, soldadosArmazenados > 0, "Dormitorio vazio"));
        acoes.Add(BotaoAcao("LIGAR TODOS OS VEICULOS", quartel.SolicitarDesdobramentoTodosVeiculos, true, veiculosArmazenados > 0, "Nao ha veiculos armazenados"));
        conteudo.Add(acoes);

        VisualElement lista = Card("UNIDADES ARMAZENADAS");
        int mostradas = 0;
        if (quartel.soldadosNoDormitorio != null)
        {
            for (int i = 0; i < quartel.soldadosNoDormitorio.Count && mostradas < 12; i++)
            {
                ControleUnidade unidade = quartel.soldadosNoDormitorio[i];
                if (unidade == null) continue;
                lista.Add(LinhaUnidade("INFANTARIA", unidade.name, "RESERVA"));
                mostradas++;
            }
        }
        if (quartel.veiculosNoQuartel != null)
        {
            for (int i = 0; i < quartel.veiculosNoQuartel.Count && mostradas < 20; i++)
            {
                ControleUnidade unidade = quartel.veiculosNoQuartel[i];
                if (unidade == null) continue;
                lista.Add(LinhaUnidade("VEICULO", unidade.name, "GARAGEM"));
                mostradas++;
            }
        }
        if (mostradas == 0) lista.Add(Texto("Nenhuma unidade esta armazenada no momento.", 13, new Color(0.68f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(lista);

        VisualElement avioes = Card("AERONAVES CONECTADAS AO QUARTEL");
        int aeronavesMostradas = 0;
        if (snapshot != null && snapshot.aeronaves != null)
        {
            for (int i = 0; i < snapshot.aeronaves.Length && aeronavesMostradas < 24; i++)
            {
                QuartelAeronaveSnapshotV2 aviao = snapshot.aeronaves[i];
                if (aviao == null) continue;
                string combustivel = aviao.combustivelDisponivel
                    ? "FUEL " + (aviao.combustivelPercentual * 100f).ToString("0") + "%"
                    : "FUEL N/R";
                string local = string.IsNullOrWhiteSpace(aviao.baseAtual) ? "sem base" : aviao.baseAtual;
                string vaga = string.IsNullOrWhiteSpace(aviao.vaga) ? string.Empty : " | vaga " + aviao.vaga;
                string estado = aviao.estadoVoo + " | " + combustivel + " | " + local + vaga;
                VisualElement linhaAviao = LinhaUnidade("AERONAVE", aviao.nome, estado);
                linhaAviao.tooltip = "ID: " + aviao.id
                    + "\nEstado porta-avioes V2: " + aviao.estadoPortaAvioes
                    + "\nOperacao: " + aviao.operacao
                    + "\nMissao: " + aviao.missao
                    + "\nIntegridade: " + (aviao.integridadePercentual * 100f).ToString("0") + "%"
                    + "\nDistancia ao Quartel: " + aviao.distanciaAoQuartel.ToString("0") + " m"
                    + (string.IsNullOrWhiteSpace(aviao.autoridadeMovimento) ? string.Empty : "\nAutoridade de movimento: " + aviao.autoridadeMovimento);
                avioes.Add(linhaAviao);
                aeronavesMostradas++;
            }
        }
        if (aeronavesMostradas == 0)
        {
            avioes.Add(Texto("Nenhuma aeronave do time esta dentro do raio de comunicacao do Quartel.", 13, new Color(0.68f, 0.78f, 0.78f), FontStyle.Normal));
        }
        avioes.Add(Texto("Leitura somente. Movimento, pouso, taxiamento, hangar e decolagem continuam sob ControleAviao/GerenciadorOperacoesPortaAvioesV2.", 11, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(avioes);
    }

    private void ConstruirAbaEfetivo()
    {
        AdicionarCabecalho("EFETIVO", "Quadro consolidado do pais e das unidades sob este Quartel.");
        DadosPaisGoverno pais = ObterPaisJogador();
        int ativos = pais != null ? pais.populacaoMilitarAtiva : 0;
        int reservistas = pais != null ? pais.reservistas : 0;
        int alistaveis = pais != null ? pais.alistaveis : 0;
        int armazenados = (quartel.soldadosNoDormitorio?.Count ?? 0) + (quartel.veiculosNoQuartel?.Count ?? 0);
        int inoperantes = snapshot != null ? snapshot.unidadesInoperantes : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("ATIVOS", ativos.ToString("N0"), "forca militar nacional"));
        resumo.Add(Cartao("RESERVISTAS", reservistas.ToString("N0"), "disponiveis para chamada"));
        resumo.Add(Cartao("ALISTAVEIS", alistaveis.ToString("N0"), "base de recrutamento"));
        resumo.Add(Cartao("ARMAZENADOS", armazenados.ToString("N0"), "unidades no Quartel"));
        resumo.Add(Cartao("INOPERANTES", inoperantes.ToString("N0"), "sem tripulacao ativa"));
        conteudo.Add(resumo);

        VisualElement quadro = Card("SITUACAO ADMINISTRATIVA");
        quadro.Add(LinhaInformacao("Estado do efetivo", ativos > 0 ? "OPERACIONAL" : "SEM EFETIVO ATIVO"));
        quadro.Add(LinhaInformacao("Reserva local", armazenados > 0 ? "COM UNIDADES" : "VAZIA"));
        quadro.Add(LinhaInformacao("Prontidao de defesa", quartel.modoDefensivoAtivo ? "PROTOCOLO ATIVO" : "SOB COMANDO"));
        quadro.Add(LinhaInformacao("Integracao com Censo Imperial", CensoImperial.Instancia != null ? "CONECTADA" : "AGUARDANDO CENSO"));
        quadro.Add(LinhaInformacao("Pessoal alocado neste Quartel", snapshot != null ? snapshot.pessoalAlocado.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Baixas nacionais registradas", pais != null ? pais.mortosAcumulados.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Feridos / temporarios / desaparecidos", "N/R — nao existe registro nacional dessas categorias"));
        quadro.Add(LinhaInformacao("Ultimo evento", snapshot != null ? snapshot.ultimoEvento : "n/d"));
        conteudo.Add(quadro);

        VisualElement forcas = Card("UNIDADES POR FORCA — DADOS DO REGISTRO ATUAL");
        AdicionarResumoForcas(forcas);
        conteudo.Add(forcas);

        VisualElement orientacao = Card("OBSERVACAO");
        orientacao.Add(Texto("O quadro nacional continua sendo calculado pelo sistema governamental existente. Este painel nao duplica a contabilidade; ele apenas organiza a leitura para o Quartel.", 13, new Color(0.70f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(orientacao);
    }

    private void ConstruirAbaRecrutamento()
    {
        AdicionarCabecalho("RECRUTAMENTO", "Protocolos automaticos sem criar uma segunda autoridade de producao.");
        VisualElement protocolos = Card("PROTOCOLOS DE PESSOAL");
        Toggle toggleRecrutamento = new Toggle("Recrutamento automatico por disponibilidade nacional")
        {
            value = quartel.recrutamentoAutomatico
        };
        toggleRecrutamento.RegisterValueChangedCallback(e => quartel.recrutamentoAutomatico = e.newValue);
        protocolos.Add(toggleRecrutamento);
        QuartelAdministracaoRuntime admin = quartel.ObterAdministracao();
        if (admin != null)
        {
            IntegerField metaEfetivo = new IntegerField("META DE EFETIVO LOCAL")
            {
                value = quartel.metaEfetivo
            };
            metaEfetivo.RegisterValueChangedCallback(e => quartel.metaEfetivo = Mathf.Clamp(e.newValue, 1, 100000));
            protocolos.Add(metaEfetivo);

            IntegerField metaDiaria = new IntegerField("META DE RECRUTAMENTO DIARIO")
            {
                value = admin.recrutamentoPorDia
            };
            metaDiaria.RegisterValueChangedCallback(e => admin.recrutamentoPorDia = Mathf.Clamp(e.newValue, 1, 1000));
            protocolos.Add(metaDiaria);

            FloatField tempoFormacao = new FloatField("TEMPO BASE DE FORMACAO (s)")
            {
                value = quartel.tempoFormacaoSegundos
            };
            tempoFormacao.RegisterValueChangedCallback(e =>
            {
                quartel.tempoFormacaoSegundos = Mathf.Clamp(e.newValue, 1f, 86400f);
                admin.tempoFormacaoPadraoSegundos = quartel.tempoFormacaoSegundos;
            });
            protocolos.Add(tempoFormacao);

            DadosPaisGoverno paisParaRecrutamento = ObterPaisJogador();
            bool podeRecrutar = paisParaRecrutamento != null
                && paisParaRecrutamento.alistaveis > 0
                && paisParaRecrutamento.populacaoMilitarAtiva + (snapshot != null ? snapshot.recrutasEmFormacao : 0) < quartel.metaEfetivo;
            protocolos.Add(BotaoAcao("PROCESSAR RECRUTAMENTO DO DIA", admin.SolicitarRecrutamentoManual, false, podeRecrutar, podeRecrutar ? string.Empty : "Sem alistaveis ou meta de efetivo atingida"));

            IntegerField quantidade = new IntegerField("QUANTIDADE POR ORDEM")
            {
                value = quantidadeRecrutamentoUI
            };
            quantidade.style.marginTop = 8;
            quantidade.style.marginBottom = 6;
            quantidade.RegisterValueChangedCallback(e => quantidadeRecrutamentoUI = Mathf.Clamp(e.newValue, 1, 100));
            protocolos.Add(quantidade);

            VisualElement escolhas = Card("RECRUTAMENTO DIRECIONADO");
            escolhas.style.flexGrow = 1;
            escolhas.Add(Texto("Escolha a forca de destino; os recrutas entram na fila de formacao e nao criam clones de unidade.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
            VisualElement linhaForcas = Linha();
            QuartelForcaV2[] forcas = { QuartelForcaV2.Infantaria, QuartelForcaV2.Veiculos, QuartelForcaV2.Naval, QuartelForcaV2.Aerea };
            for (int i = 0; i < forcas.Length; i++)
            {
                QuartelForcaV2 forca = forcas[i];
                Button botaoForca = BotaoAcao("RECRUTAR " + NomeForca(forca), () => admin.SolicitarRecrutamentoManual(forca, quantidadeRecrutamentoUI), false, podeRecrutar, podeRecrutar ? string.Empty : "Sem alistaveis ou meta de efetivo atingida");
                botaoForca.style.minWidth = 150;
                linhaForcas.Add(botaoForca);
            }
            escolhas.Add(linhaForcas);
            conteudo.Add(escolhas);
        }
        Toggle toggleTreino = new Toggle("Treinamento automatico de unidades em repouso")
        {
            value = quartel.treinamentoAutomatico
        };
        toggleTreino.RegisterValueChangedCallback(e => quartel.treinamentoAutomatico = e.newValue);
        protocolos.Add(toggleTreino);
        protocolos.Add(LinhaInformacao("Modo atual", quartel.treinamentoAutomatico ? "AUTOMATICO" : "MANUAL"));
        protocolos.Add(LinhaInformacao("Treinamento passivo legado", quartel.treinamentoPassivo ? "ATIVO" : "DESATIVADO"));
        conteudo.Add(protocolos);

        VisualElement fila = Card("FILA E CRITERIOS");
        DadosPaisGoverno pais = ObterPaisJogador();
        fila.Add(LinhaInformacao("Alistaveis nacionais", pais != null ? pais.alistaveis.ToString("N0") : "n/d"));
        fila.Add(LinhaInformacao("Meta de efetivo local", quartel.metaEfetivo.ToString("N0")));
        fila.Add(LinhaInformacao("Tempo base de formacao", quartel.tempoFormacaoSegundos.ToString("0") + " s"));
        fila.Add(LinhaInformacao("Meta de recrutamento diario", admin != null ? admin.recrutamentoPorDia.ToString("N0") : "n/d"));
        fila.Add(LinhaInformacao("Recrutas no dia", snapshot != null ? snapshot.recrutasRecrutados.ToString("N0") : "0"));
        fila.Add(LinhaInformacao("Em formacao", snapshot != null ? snapshot.recrutasEmFormacao.ToString("N0") : "0"));
        fila.Add(LinhaInformacao("Progresso", snapshot != null ? (snapshot.progressoTreinamento * 100f).ToString("0") + "%  |  " + snapshot.segundosRestantesTreinamento.ToString("0.0") + " s" : "0%"));
        fila.Add(LinhaInformacao("Forca de destino", snapshot != null ? snapshot.forcaTreinamento : "Nenhuma"));
        if (snapshot != null)
        {
            fila.Add(LinhaInformacao("Distribuicao", "Inf " + snapshot.recrutasInfantaria + " | Veic " + snapshot.recrutasVeiculos + " | Naval " + snapshot.recrutasNaval + " | Aerea " + snapshot.recrutasAerea));
        }
        fila.Add(LinhaInformacao("Ultimo evento", snapshot != null ? snapshot.ultimoEvento : "n/d"));
        fila.Add(Texto("A fila usa os dados do SistemaGovernoMundial/SistemaMilitar. O Quartel nao instancia clones nem substitui a producao de unidades.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(fila);
    }

    private void ConstruirAbaFolha()
    {
        AdicionarCabecalho("FOLHA MILITAR", "Leitura de custo e responsabilidade fiscal do efetivo.");
        DadosPaisGoverno pais = ObterPaisJogador();
        VisualElement resumo = Card("CUSTOS ATUAIS");
        resumo.Add(LinhaInformacao("Custo de manutencao nacional", pais != null ? "$" + pais.custoManutencao.ToString("N2") : "n/d"));
        resumo.Add(LinhaInformacao("Militares ativos", pais != null ? pais.populacaoMilitarAtiva.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Reservistas", pais != null ? pais.reservistas.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Saldo do tesouro", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Folha diaria do pessoal alocado", snapshot != null ? "$" + snapshot.custoFolhaDiario.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Folha do periodo", snapshot != null ? "$" + snapshot.custoFolhaCalculado.ToString("N0") + " / " + snapshot.periodoFolhaDias + " dias" : "n/d"));
        resumo.Add(LinhaInformacao("Proximo vencimento", snapshot != null ? "dia " + snapshot.proximoDiaFolha : "n/d"));
        resumo.Add(LinhaInformacao("Pagamento", snapshot != null ? (snapshot.ultimoPagamentoRealizado ? "DESCONTADO" : "PENDENTE") : "n/d"));
        resumo.Add(LinhaInformacao("Dias pendentes", snapshot != null ? snapshot.diasFolhaPendentes.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Total pago pelo Quartel", snapshot != null ? "$" + snapshot.folhaPagaTotal.ToString("N0") : "n/d"));
        conteudo.Add(resumo);

        VisualElement nota = Card("REGRA DE FOLHA");
        nota.Add(Texto(snapshot != null && snapshot.folhaPendente
            ? "Pagamento pendente: " + snapshot.ultimoMotivoFolha
            : "A folha nacional continua no governo. Este quadro cobre apenas o pessoal alocado por este Quartel e cobra o periodo configurado; nao mistura reservistas com tripulacao.", 13, new Color(0.72f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(nota);
    }

    private void ConstruirAbaTripulacoes()
    {
        AdicionarCabecalho("TRIPULACOES", "Controle administrativo de pessoal minimo para unidades navais.");
        QuartelForcaSnapshotV2 naval = ObterResumoForca(QuartelForcaV2.Naval);
        QuartelForcaSnapshotV2 aerea = ObterResumoForca(QuartelForcaV2.Aerea);
        int navais = naval != null ? naval.unidades : 0;
        int aereos = aerea != null ? aerea.unidades : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("NAVIOS", navais.ToString("N0"), "unidades navais reconhecidas"));
        resumo.Add(Cartao("AERONAVES", aereos.ToString("N0"), "unidades aereas reconhecidas"));
        resumo.Add(Cartao("EXIGIDO", snapshot != null ? snapshot.pessoalExigido.ToString("N0") : "0", "pessoal minimo"));
        resumo.Add(Cartao("ALOCADO", snapshot != null ? snapshot.pessoalAlocado.ToString("N0") : "0", "somente ativos"));
        resumo.Add(Cartao("INOPERANTES", snapshot != null ? snapshot.unidadesInoperantes.ToString("N0") : "0", "sem tripulacao"));
        conteudo.Add(resumo);

        VisualElement quadro = Card("SITUACAO DE GUARNICAO");
        quadro.Add(LinhaInformacao("Estado", navais == 0 ? "SEM NAVIOS NO CENSO" : "COBERTURA ADMINISTRATIVA ATIVA"));
        quadro.Add(LinhaInformacao("Reserva usada como tripulacao", "NAO"));
        quadro.Add(LinhaInformacao("Tripulacao naval exigida / alocada", naval != null ? naval.pessoalExigido + " / " + naval.pessoalAlocado : "n/d"));
        quadro.Add(LinhaInformacao("Tripulacao aerea exigida / alocada", aerea != null ? aerea.pessoalExigido + " / " + aerea.pessoalAlocado : "n/d"));
        quadro.Add(LinhaInformacao("Aeronaves conectadas ao Quartel", snapshot != null ? snapshot.aeronavesNoRaio.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Unidades sem militares", snapshot != null && snapshot.unidadesInoperantes > 0 ? "INOPERANTES" : "OPERACIONAIS"));
        quadro.Add(Texto("Aba administrativa: nao emite ordem de movimento, patrulha ou combate. Quando faltam militares ativos, o bloqueio administrativo recusa novas ordens sem desativar o objeto.", 13, new Color(0.70f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(quadro);
    }

    private void ConstruirAbaResgate()
    {
        AdicionarCabecalho("RESGATE", "Alertas de integridade e reparo das unidades do jogador dentro da cobertura.");
        int danificadas = snapshot != null ? snapshot.unidadesDanificadas : 0;
        int avaliadas = snapshot != null ? snapshot.unidadesAvaliadasResgate : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("AVALIADAS", avaliadas.ToString("N0"), "unidades na cobertura"));
        resumo.Add(Cartao("ALERTAS", danificadas.ToString("N0"), "unidades com dano"));
        resumo.Add(Cartao("PROTOCOLO", danificadas > 0 ? "ATENCAO" : "NORMAL", "estado de recuperacao"));
        resumo.Add(Cartao("PERDAS", snapshot != null ? snapshot.perdasRegistradas.ToString("N0") : "0", "historico registrado"));
        conteudo.Add(resumo);

        VisualElement acoes = Card("ACAO DE RECUPERACAO");
        acoes.Add(BotaoAcao("SOLICITAR RECUPERACAO MANUAL", quartel.SolicitarResgateManual, false, danificadas > 0, "Nenhuma unidade danificada na cobertura"));
        acoes.Add(LinhaInformacao("Aviso de resgate", snapshot != null && snapshot.unidadesComAlertaResgate > 0 ? snapshot.ultimoAvisoResgate : "Nenhum alerta"));
        acoes.Add(Texto("O reparo e encaminhado ao GerenciadorQuartel e nao reativa unidades, altera parentesco ou teletransporta objetos.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(acoes);
    }

    private void ConstruirAbaComunicacoes()
    {
        AdicionarCabecalho("COMUNICACOES", "Protocolos de radio, chamada automatica e defesa da base.");
        VisualElement protocolos = Card("REDE DE COMANDO");
        Toggle recolhimento = new Toggle("Recolhimento automatico de unidades ociosas")
        {
            value = quartel.recolhimentoAutomatico
        };
        recolhimento.RegisterValueChangedCallback(e => quartel.recolhimentoAutomatico = e.newValue);
        protocolos.Add(recolhimento);
        Toggle defesa = new Toggle("Defesa automatica e despertar da base")
        {
            value = quartel.modoDefensivoAtivo
        };
        defesa.RegisterValueChangedCallback(e => quartel.modoDefensivoAtivo = e.newValue);
        protocolos.Add(defesa);
        Toggle combustivel = new Toggle("Abastecimento automatico global de Tracks")
        {
            value = CaminhaoCombustivel.AbastecimentoAutomaticoGlobal
        };
        combustivel.RegisterValueChangedCallback(e => CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = e.newValue);
        protocolos.Add(combustivel);
        protocolos.Add(LinhaInformacao("Raio de cobertura", quartel.raioDeCobertura.ToString("0") + " m"));
        protocolos.Add(LinhaInformacao("Contatos inimigos transmitidos", snapshot != null ? snapshot.contatosInimigos.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Submarinos na superficie", snapshot != null ? snapshot.contatosSubmarinos.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Ameacas de missil", snapshot != null ? snapshot.contatosMisseis.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Aeronaves com telemetria local", snapshot != null ? snapshot.aeronavesNoRaio.ToString("N0") : "n/d"));
        conteudo.Add(protocolos);

        VisualElement registro = Card("REGISTRO DE COMUNICACOES — QUARTEL / AERONAVE");
        if (snapshot != null && snapshot.comunicacoes != null && snapshot.comunicacoes.Length > 0)
        {
            int limite = Mathf.Min(12, snapshot.comunicacoes.Length);
            for (int i = 0; i < limite; i++)
            {
                QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                if (comunicacao == null) continue;
                string tituloRegistro = "[" + comunicacao.horario + "] " + comunicacao.origem;
                string detalheRegistro = comunicacao.tipo + " | " + comunicacao.distanciaAoQuartel.ToString("0") + " m | " + comunicacao.mensagem;
                registro.Add(LinhaUnidade(comunicacao.inimigo ? "ALERTA" : "RADIO", tituloRegistro, detalheRegistro));
            }
        }
        else
        {
            registro.Add(Texto("Aguardando telemetria do E-3. O registro aparece quando o aviao estiver em missao e transmitir contatos.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        }
        conteudo.Add(registro);

        VisualElement acoes = Card("LOGISTICA");
        acoes.Add(BotaoAcao("CARREGAR TRACKS NESTE QG", quartel.SolicitarTracksNoQuartel));
        acoes.Add(BotaoAcao("FORCAR RETORNO A BASE", quartel.SolicitarRetornoTracks));
        conteudo.Add(acoes);
    }

    private void ConstruirPainelLancamentoCoordenado()
    {
        if (quartel == null || !quartel.habilitarLancamentoCoordenado) return;
        if (painelLancamentoPersistente == null)
        {
            painelLancamentoPersistente = Card("LANÇAMENTO COORDENADO");
            painelLancamentoPersistente.style.flexShrink = 0;
            // Mantem a carta e a telemetria acima; o bloco de disparo fica
            // separado visualmente na parte inferior do Quartel.
            painelLancamentoPersistente.style.marginTop = 24;
            painelLancamentoPersistente.Add(Texto("O Quartel apenas autoriza o disparo. Navios e submarinos permanecem exatamente nas posições atuais.", 12, CorTextoSecundarioQuartel, FontStyle.Normal));

            VisualElement modos = Linha();
            botaoModoManualLancamento = Botao("◉  ATAQUE MANUAL COORDENADO", 0, 34, CorNavegacaoQuartel);
            botaoModoAutomaticoLancamento = Botao("◌  ATAQUE AUTOMATICO COORDENADO", 0, 34, CorNavegacaoQuartel);
            botaoModoManualLancamento.style.flexGrow = 1;
            botaoModoAutomaticoLancamento.style.flexGrow = 1;
            RegistrarAcaoBotao(botaoModoManualLancamento, () => { quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual); AtualizarPainel(); });
            RegistrarAcaoBotao(botaoModoAutomaticoLancamento, () => { quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico); AtualizarPainel(); });
            modos.Add(botaoModoManualLancamento);
            modos.Add(botaoModoAutomaticoLancamento);
            painelLancamentoPersistente.Add(modos);

            contatosLancamentoPersistente = Card("CONTATOS TRANSMITIDOS PELO E-3");
            contatosLancamentoListaPersistente = new VisualElement { name = "quartel-lancamento-contatos-lista" };
            contatosLancamentoListaPersistente.style.flexDirection = FlexDirection.Column;
            contatosLancamentoVazio = Texto("Nenhum contato inimigo válido foi transmitido no momento. Aguarde o E-3 atualizar a cobertura.", 12, new Color(1f, 0.70f, 0.28f), FontStyle.Normal);
            contatosLancamentoVazio.style.display = DisplayStyle.None;
            contatosLancamentoListaPersistente.Add(contatosLancamentoVazio);
            contatosLancamentoPersistente.Add(contatosLancamentoListaPersistente);
            painelLancamentoPersistente.Add(contatosLancamentoPersistente);
            unidadesLancamentoPersistente = Card("UNIDADES SELECIONÁVEIS");
            unidadesLancamentoListaPersistente = new VisualElement { name = "quartel-lancamento-unidades-lista" };
            unidadesLancamentoListaPersistente.style.flexDirection = FlexDirection.Column;
            unidadesLancamentoVazio = Texto("Nenhum navio, submarino ou lançador de mísseis compatível foi encontrado.", 12, new Color(1f, 0.70f, 0.28f), FontStyle.Normal);
            unidadesLancamentoVazio.style.display = DisplayStyle.None;
            unidadesLancamentoListaPersistente.Add(unidadesLancamentoVazio);
            unidadesLancamentoPersistente.Add(unidadesLancamentoListaPersistente);
            painelLancamentoPersistente.Add(unidadesLancamentoPersistente);
            estatisticasLancamentoPersistentes = new VisualElement { name = "quartel-lancamento-estatisticas" };
            painelLancamentoPersistente.Add(estatisticasLancamentoPersistentes);
            validacaoLancamentoPersistente = Card("RESULTADO DA VALIDAÇÃO");
            painelLancamentoPersistente.Add(validacaoLancamentoPersistente);

            botaoConfirmarLancamento = BotaoAcao("▶  LANÇAR", () =>
            {
                ExecutarAtaqueCarta(quartel.ModoLancamentoCoordenado);
            });
            botaoConfirmarLancamento.style.minHeight = 42;
            botaoConfirmarLancamento.tooltip = "Executar o lançamento a partir dos lançadores selecionados";
            painelLancamentoPersistente.Add(botaoConfirmarLancamento);
            conteudo.Add(painelLancamentoPersistente);
        }

        AtualizarPainelLancamentoCoordenado();
    }

    private void AtualizarPainelLancamentoCoordenado()
    {
        if (quartel == null || painelLancamentoPersistente == null) return;

        Color corAtivo = CorNavegacaoAtivaQuartel;
        Color corInativo = CorNavegacaoQuartel;
        if (botaoModoManualLancamento != null)
            botaoModoManualLancamento.style.backgroundColor = quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual ? corAtivo : corInativo;
        if (botaoModoAutomaticoLancamento != null)
            botaoModoAutomaticoLancamento.style.backgroundColor = quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico ? corAtivo : corInativo;

        if (contatosLancamentoPersistente != null)
        {
            HashSet<string> contatosPresentes = new HashSet<string>(StringComparer.Ordinal);
            bool possuiContatos = quartel.AlvosLancamento.Count > 0;
            if (contatosLancamentoVazio != null)
                contatosLancamentoVazio.style.display = possuiContatos ? DisplayStyle.None : DisplayStyle.Flex;

            if (possuiContatos && contatosLancamentoListaPersistente != null)
            {
                for (int i = 0; i < quartel.AlvosLancamento.Count; i++)
                {
                    GerenciadorQuartel.AlvoLancamentoCoordenadoV2 alvo = quartel.AlvosLancamento[i];
                    if (alvo == null) continue;
                    string idAlvo = alvo.id;
                    contatosPresentes.Add(idAlvo);
                    bool ativo = idAlvo == quartel.AlvoSelecionadoLancamentoId;
                    Button botao;
                    if (!botoesContatosLancamento.TryGetValue(idAlvo, out botao) || botao == null)
                    {
                        botao = Botao(string.Empty, 0, 40, corInativo);
                        botao.style.flexGrow = 1;
                        string idAlvoPersistente = idAlvo;
                        RegistrarAcaoBotao(botao, () => AoSelecionarContatoCarta(idAlvoPersistente));
                        botoesContatosLancamento[idAlvo] = botao;
                        contatosLancamentoListaPersistente.Add(botao);
                    }
                    botao.text = (ativo ? "● " : "○ ") + EncurtarTextoDesigner(alvo.nome, 24) + " | "
                        + EncurtarTextoDesigner(alvo.tipo, 14) + " | " + EncurtarTextoDesigner(alvo.origem, 14);
                    botao.style.backgroundColor = ativo ? CorNavegacaoAtivaQuartel : corInativo;
                    botao.style.whiteSpace = WhiteSpace.Normal;
                    botao.tooltip = "ID " + alvo.id + " | posicao " + FormatarPosicao(alvo.posicao) + " | idade " + alvo.idadeSegundos.ToString("0.0") + " s";
                    botao.style.display = DisplayStyle.Flex;
                }
            }

            foreach (KeyValuePair<string, Button> par in botoesContatosLancamento)
            {
                if (!contatosPresentes.Contains(par.Key) && par.Value != null)
                    par.Value.style.display = DisplayStyle.None;
            }
        }

        int selecionadas = 0;
        int aptas = 0;
        if (unidadesLancamentoPersistente != null)
        {
            HashSet<string> unidadesPresentes = new HashSet<string>(StringComparer.Ordinal);
            bool possuiUnidades = quartel.UnidadesLancamento.Count > 0;
            if (unidadesLancamentoVazio != null)
                unidadesLancamentoVazio.style.display = possuiUnidades ? DisplayStyle.None : DisplayStyle.Flex;

            for (int i = 0; i < quartel.UnidadesLancamento.Count; i++)
            {
                GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = quartel.UnidadesLancamento[i];
                if (unidade == null) continue;
                if (unidade.selecionada) selecionadas++;

                string idUnidade = unidade.id;
                unidadesPresentes.Add(idUnidade);
                string prefixo = unidade.selecionada ? "☑ " : "☐ ";
                string estado = unidade.modoOperacional + " | " + unidade.sistemaLancamento;
                if (!string.IsNullOrWhiteSpace(unidade.estadoLancamento)) estado += " | " + unidade.estadoLancamento;
                VisualElement linhaUnidade;
                Button botao;
                Button botaoModo;
                if (!linhasUnidadesLancamento.TryGetValue(idUnidade, out linhaUnidade) || linhaUnidade == null)
                {
                    linhaUnidade = Linha();
                    linhaUnidade.style.flexWrap = Wrap.NoWrap;
                    linhaUnidade.style.marginTop = 2;
                    linhaUnidade.style.marginBottom = 2;
                    botao = Botao(string.Empty, 0, 36, corInativo);
                    botao.style.flexGrow = 1;
                    botao.style.whiteSpace = WhiteSpace.Normal;
                    string idUnidadePersistente = idUnidade;
                    RegistrarAcaoBotao(botao, () => SelecionarUnidadeCarta(idUnidadePersistente, false, false));
                    botaoModo = Botao(string.Empty, 0, 32, corInativo);
                    botaoModo.style.width = 172;
                    botaoModo.style.flexGrow = 0;
                    botaoModo.style.whiteSpace = WhiteSpace.Normal;
                    RegistrarAcaoBotao(botaoModo, () => AlternarModoUnidadeLancamento(idUnidadePersistente));
                    linhaUnidade.Add(botao);
                    linhaUnidade.Add(botaoModo);
                    linhasUnidadesLancamento[idUnidade] = linhaUnidade;
                    botoesUnidadesLancamento[idUnidade] = botao;
                    botoesModoUnidadesLancamento[idUnidade] = botaoModo;
                    if (unidadesLancamentoListaPersistente != null) unidadesLancamentoListaPersistente.Add(linhaUnidade);
                }
                else
                {
                    botoesUnidadesLancamento.TryGetValue(idUnidade, out botao);
                    botoesModoUnidadesLancamento.TryGetValue(idUnidade, out botaoModo);
                }

                if (botao == null || botaoModo == null) continue;
                botao.text = prefixo + EncurtarTextoDesigner(unidade.nome, 22) + " | "
                    + EncurtarTextoDesigner(unidade.tipo, 13) + " | " + EncurtarTextoDesigner(estado, 20);
                botao.style.backgroundColor = unidade.selecionada ? corAtivo : corInativo;
                botao.style.whiteSpace = WhiteSpace.Normal;
                botao.tooltip = "ID " + idUnidade + " | posicao " + FormatarPosicao(unidade.posicao);

                bool eLancadorEstrategico = unidade.lancadorMisseis != null;
                botaoModo.text = eLancadorEstrategico ? "MANUAL · MÍSSEIS" : "MODO " + unidade.modoOperacional;
                botaoModo.style.backgroundColor = !eLancadorEstrategico && unidade.modoOperacional == "AUTOMATICO"
                    ? new Color(0.08f, 0.30f, 0.23f) : corInativo;
                botaoModo.tooltip = eLancadorEstrategico
                    ? "LancadorMisseis: lançamento estratégico manual pelo botão CONFIRMAR LANÇAMENTO."
                    : "Alternar o modo real desta unidade: PASSIVO → MANUAL → AUTOMÁTICO";
                linhaUnidade.style.display = DisplayStyle.Flex;
            }

            foreach (KeyValuePair<string, VisualElement> par in linhasUnidadesLancamento)
            {
                if (!unidadesPresentes.Contains(par.Key) && par.Value != null)
                    par.Value.style.display = DisplayStyle.None;
            }
        }

        for (int i = 0; i < quartel.AvaliacoesLancamento.Count; i++)
            if (quartel.AvaliacoesLancamento[i] != null && quartel.AvaliacoesLancamento[i].apta) aptas++;
        string resumoLancadores = ObterResumoLancadoresSelecionados();
        if (estatisticasLancamentoPersistentes != null)
        {
            estatisticasLancamentoPersistentes.Clear();
            estatisticasLancamentoPersistentes.Add(LinhaInformacao("UNIDADES SELECIONADAS", selecionadas.ToString("00")));
            estatisticasLancamentoPersistentes.Add(LinhaInformacao("APTAS PARA LANÇAMENTO", aptas.ToString("00")));
            estatisticasLancamentoPersistentes.Add(LinhaInformacao("FORA DAS CONDIÇÕES AUTOMÁTICAS", Mathf.Max(0, selecionadas - aptas).ToString("00")));
            estatisticasLancamentoPersistentes.Add(LinhaInformacao("VÃO ATIRAR", resumoLancadores));
        }

        if (validacaoLancamentoPersistente != null)
        {
            validacaoLancamentoPersistente.Clear();
            validacaoLancamentoPersistente.Add(Texto("RESULTADO DA VALIDAÇÃO", 12, Color.white, FontStyle.Bold));
            validacaoLancamentoPersistente.Add(Texto("VÃO ATIRAR: " + resumoLancadores, 12,
                selecionadas > 0 ? CorVerdeQuartel : CorAlertaQuartel, FontStyle.Bold));
            if (quartel.AvaliacoesLancamento.Count == 0)
            {
                validacaoLancamentoPersistente.Add(Texto("Selecione um contato e uma ou mais unidades.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
            }
            else
            {
                for (int i = 0; i < quartel.AvaliacoesLancamento.Count; i++)
                {
                    GerenciadorQuartel.AvaliacaoLancamentoCoordenadoV2 avaliacao = quartel.AvaliacoesLancamento[i];
                    if (avaliacao == null) continue;
                    string texto = avaliacao.unidadeNome + " — " + (avaliacao.apta ? "lançamento autorizado" : "lançamento bloqueado") + " | " + avaliacao.motivo;
                    validacaoLancamentoPersistente.Add(Texto(texto, 12, avaliacao.apta ? new Color(0.45f, 0.94f, 0.58f) : new Color(1f, 0.62f, 0.30f), FontStyle.Normal));
                }
            }
            if (!string.IsNullOrWhiteSpace(quartel.UltimoMotivoLancamento))
                validacaoLancamentoPersistente.Add(Texto("Última operação " + quartel.UltimoIdOperacaoLancamento + ": " + quartel.UltimoMotivoLancamento, 12, Color.white, FontStyle.Bold));
        }

        bool podeConfirmar = selecionadas > 0 && quartel.AlvoLancamentoSelecionadoValido;
        if (botaoConfirmarLancamento != null)
        {
            botaoConfirmarLancamento.SetEnabled(podeConfirmar);
            botaoConfirmarLancamento.text = selecionadas > 0
                ? "▶  LANÇAR COM: " + EncurtarTextoDesigner(resumoLancadores, 42)
                : "▶  LANÇAR";
            botaoConfirmarLancamento.tooltip = podeConfirmar
                ? "Executar o lançamento usando: " + resumoLancadores
                : "Selecione um contato/ponto e pelo menos uma unidade";
        }
    }

    private void ConstruirAbaCartaNautica()
    {
        AdicionarCabecalho("CARTA NAUTICA", "Carta topografica operacional; leitura de telemetria sem substituir ordens ou FLIR.");
        VisualElement modos = Linha();
        modos.style.marginBottom = 8;
        Button modo2D = Botao("▦  2D TOPOGRAFICO", 0, 36, cartaVista3D ? CorNavegacaoQuartel : CorNavegacaoAtivaQuartel);
        Button modo3D = Botao("◇  3D TOPOGRAFICO", 0, 36, cartaVista3D ? CorNavegacaoAtivaQuartel : CorNavegacaoQuartel);
        botaoCarta2D = modo2D;
        botaoCarta3D = modo3D;
        modo2D.style.flexGrow = 1;
        modo3D.style.flexGrow = 1;
        RegistrarAcaoBotao(modo2D, () => { cartaVista3D = false; AtualizarPainel(); });
        RegistrarAcaoBotao(modo3D, () => { cartaVista3D = true; AtualizarPainel(); });
        modos.Add(modo2D);
        modos.Add(modo3D);
        conteudo.Add(modos);

        carta = new VisualElement { name = "quartel-carta" };
        // A carta ganhou uma faixa vertical maior para que o mapa e os
        // controles inferiores não sejam espremidos nem cubram a telemetria.
        // O PanelSettings da campanha aplica escala ao UI Toolkit. 820px
        // internos resultam no aumento visual pedido também no Game View
        // reduzido, mantendo o painel de lançamento logo abaixo da Carta.
        // O ScrollView pai permite acessar os controles inferiores quando a
        // altura da janela for menor que a faixa operacional completa.
        carta.style.height = 980;
        carta.style.minHeight = 980;
        carta.style.flexBasis = 980;
        carta.style.flexShrink = 0;
        carta.style.flexDirection = FlexDirection.Row;
        carta.style.position = Position.Relative;
        carta.style.backgroundColor = CorCartaoQuartel;
        carta.style.borderTopWidth = 1;
        carta.style.borderBottomWidth = 1;
        carta.style.borderLeftWidth = 1;
        carta.style.borderRightWidth = 1;
        carta.style.borderTopColor = CorBordaQuartel;
        carta.style.borderBottomColor = CorBordaQuartel;
        carta.style.borderLeftColor = CorBordaQuartel;
        carta.style.borderRightColor = CorBordaQuartel;
        DesenharCartaToolkitNovo();
        conteudo.Add(carta);

        ConstruirPainelLancamentoCoordenado();

        VisualElement legenda = Card("LEGENDA");
        legenda.Add(LinhaInformacao("● QG", "posicao deste Quartel"));
        legenda.Add(LinhaInformacao("● Azul", "unidade aerea"));
        legenda.Add(LinhaInformacao("● Verde", "unidade terrestre"));
        legenda.Add(LinhaInformacao("● Ciano", "unidade naval"));
        legenda.Add(LinhaInformacao("Atualizacao", "a cada 0,75 s enquanto a aba esta aberta"));
        conteudo.Add(legenda);
    }

    private void DesenharCartaToolkitNovo()
    {
        if (carta == null || quartel == null) return;
        carta.Clear();
        carta.style.minHeight = 980;
        carta.style.overflow = Overflow.Hidden;

        float raio = Mathf.Max(100f, ObterRaioCarta());
        VisualElement mapa = new VisualElement { name = "quartel-carta-mapa" };
        // O mapa fica com a maior parte da largura; a telemetria permanece
        // fixa ao lado para não espremer os botões nem cortar os dados.
        mapa.style.width = 0;
        mapa.style.flexGrow = 7;
        mapa.style.flexShrink = 1;
        mapa.style.height = new Length(100f, LengthUnit.Percent);
        mapa.style.minHeight = 980;
        mapa.style.minWidth = 430;
        mapa.style.position = Position.Relative;
        mapa.style.overflow = Overflow.Hidden;
        mapa.style.backgroundColor = new Color(0.006f, 0.045f, 0.070f, 1f);
        ScrollView telemetria = new ScrollView(ScrollViewMode.Vertical) { name = "quartel-carta-telemetria" };
        telemetria.style.width = 0;
        telemetria.style.flexGrow = 3;
        telemetria.style.flexShrink = 1;
        telemetria.style.height = new Length(100f, LengthUnit.Percent);
        telemetria.style.minHeight = 980;
        telemetria.style.minWidth = 300;
        telemetria.style.paddingLeft = 12;
        telemetria.style.paddingRight = 12;
        telemetria.style.paddingTop = 12;
        telemetria.style.paddingBottom = 10;
        telemetria.style.backgroundColor = CorPainelQuartel;
        carta.Add(mapa);
        carta.Add(telemetria);
        mapaCartaPersistente = mapa;
        telemetriaCartaPersistente = telemetria;

        Texture terreno = ObterTexturaTerrenoCarta(raio, 1.75f);
        if (terreno != null)
        {
            Image imagemTerreno = new Image { image = terreno, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            imagemTerreno.style.position = Position.Absolute;
            imagemTerreno.style.left = 0;
            imagemTerreno.style.right = 0;
            imagemTerreno.style.top = 0;
            imagemTerreno.style.bottom = 0;
            imagemTerreno.style.opacity = 0.86f;
            mapa.Add(imagemTerreno);
        }

        for (int i = 1; i < 10; i++)
        {
            VisualElement vertical = new VisualElement();
            vertical.style.position = Position.Absolute;
            vertical.style.left = new Length(i * 10f, LengthUnit.Percent);
            vertical.style.top = 0;
            vertical.style.bottom = 0;
            vertical.style.width = 1;
            vertical.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            mapa.Add(vertical);
            VisualElement horizontal = new VisualElement();
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = new Length(i * 10f, LengthUnit.Percent);
            horizontal.style.left = 0;
            horizontal.style.right = 0;
            horizontal.style.height = 1;
            horizontal.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            mapa.Add(horizontal);
        }

        Label tituloMapa = Texto(cartaVista3D ? "VISUALIZACAO 3D INCLINADA" : "TOPOGRAFIA 2D  |  CURVAS DE NIVEL", 12, Color.white, FontStyle.Bold);
        tituloCartaPersistente = tituloMapa;
        tituloMapa.style.position = Position.Absolute;
        tituloMapa.style.left = 12;
        tituloMapa.style.top = 10;
        tituloMapa.pickingMode = PickingMode.Ignore;
        mapa.Add(tituloMapa);

        VisualElement centro = Marcador("QG", 50f, 50f, new Color(0.95f, 0.80f, 0.25f), 18);
        mapa.Add(centro);
        Label nomeQG = Texto("QG  " + quartel.name, 11, Color.white, FontStyle.Bold);
        nomeQG.style.position = Position.Absolute;
        nomeQG.style.left = new Length(50f, LengthUnit.Percent);
        nomeQG.style.top = new Length(50f, LengthUnit.Percent);
        nomeQG.style.marginLeft = 12;
        nomeQG.style.marginTop = -10;
        nomeQG.pickingMode = PickingMode.Ignore;
        mapa.Add(nomeQG);

        camadaInteracaoCarta = new VisualElement { name = "quartel-carta-interacao" };
        camadaInteracaoCarta.style.position = Position.Absolute;
        camadaInteracaoCarta.style.left = 0;
        camadaInteracaoCarta.style.right = 0;
        camadaInteracaoCarta.style.top = 0;
        camadaInteracaoCarta.style.bottom = 0;
        camadaInteracaoCarta.pickingMode = PickingMode.Position;
        camadaInteracaoCarta.RegisterCallback<PointerDownEvent>(AoIniciarArrastoCarta);
        camadaInteracaoCarta.RegisterCallback<PointerMoveEvent>(AoMoverArrastoCarta);
        camadaInteracaoCarta.RegisterCallback<PointerUpEvent>(AoFinalizarArrastoCarta);
        camadaInteracaoCarta.RegisterCallback<PointerCaptureOutEvent>(AoPerderCapturaCarta);
        camadaInteracaoCarta.RegisterCallback<WheelEvent>(AoUsarRodaNaCarta);
        mapa.Add(camadaInteracaoCarta);

        camadaTrajetoriasCarta = new VisualElement { name = "quartel-carta-trajetorias" };
        camadaTrajetoriasCarta.style.position = Position.Absolute;
        camadaTrajetoriasCarta.style.left = 0;
        camadaTrajetoriasCarta.style.right = 0;
        camadaTrajetoriasCarta.style.top = 0;
        camadaTrajetoriasCarta.style.bottom = 0;
        camadaTrajetoriasCarta.pickingMode = PickingMode.Ignore;
        mapa.Add(camadaTrajetoriasCarta);

        camadaMarcadoresCarta = new VisualElement { name = "quartel-carta-marcadores" };
        camadaMarcadoresCarta.style.position = Position.Absolute;
        camadaMarcadoresCarta.style.left = 0;
        camadaMarcadoresCarta.style.right = 0;
        camadaMarcadoresCarta.style.top = 0;
        camadaMarcadoresCarta.style.bottom = 0;
        camadaMarcadoresCarta.pickingMode = PickingMode.Position;
        // Esta camada ocupa todo o mapa para hospedar marcadores virtuais
        // (por exemplo, contatos E-3 sem collider). Quando o clique cai em
        // uma área vazia, ela precisa encaminhar o mesmo gesto para a camada
        // de interação da Carta; caso contrário, o elemento irmão abaixo
        // nunca recebe o PointerDown e o terreno não pode ser selecionado.
        camadaMarcadoresCarta.RegisterCallback<PointerDownEvent>(AoIniciarArrastoCarta);
        camadaMarcadoresCarta.RegisterCallback<PointerMoveEvent>(AoMoverArrastoCarta);
        camadaMarcadoresCarta.RegisterCallback<PointerUpEvent>(AoFinalizarArrastoCarta);
        camadaMarcadoresCarta.RegisterCallback<PointerCaptureOutEvent>(AoPerderCapturaCarta);
        camadaMarcadoresCarta.RegisterCallback<WheelEvent>(AoUsarRodaNaCarta);
        mapa.Add(camadaMarcadoresCarta);

        VisualElement controlesNavegacao = new VisualElement { name = "quartel-carta-navegacao" };
        controlesNavegacao.style.position = Position.Absolute;
        controlesNavegacao.style.left = 10;
        controlesNavegacao.style.top = 36;
        controlesNavegacao.style.height = 44;
        controlesNavegacao.style.paddingLeft = 4;
        controlesNavegacao.style.paddingRight = 4;
        controlesNavegacao.style.paddingTop = 4;
        controlesNavegacao.style.paddingBottom = 4;
        controlesNavegacao.style.flexDirection = FlexDirection.Row;
        controlesNavegacao.style.alignItems = Align.Center;
        controlesNavegacao.style.backgroundColor = new Color(0.008f, 0.055f, 0.080f, 0.96f);
        controlesNavegacao.style.borderTopWidth = 1;
        controlesNavegacao.style.borderBottomWidth = 1;
        controlesNavegacao.style.borderLeftWidth = 1;
        controlesNavegacao.style.borderRightWidth = 1;
        controlesNavegacao.style.borderTopColor = CorBordaQuartel;
        controlesNavegacao.style.borderBottomColor = CorBordaQuartel;
        controlesNavegacao.style.borderLeftColor = CorBordaQuartel;
        controlesNavegacao.style.borderRightColor = CorBordaQuartel;
        controlesNavegacao.style.borderTopLeftRadius = 5;
        controlesNavegacao.style.borderTopRightRadius = 5;
        controlesNavegacao.style.borderBottomLeftRadius = 5;
        controlesNavegacao.style.borderBottomRightRadius = 5;
        controlesNavegacao.pickingMode = PickingMode.Position;
        mapa.Add(controlesNavegacao);
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "←", () => cartaTerrenoRenderer?.DeslocarMapa(new Vector2(-0.10f, 0f)));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "→", () => cartaTerrenoRenderer?.DeslocarMapa(new Vector2(0.10f, 0f)));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "↑", () => cartaTerrenoRenderer?.DeslocarMapa(new Vector2(0f, -0.10f)));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "↓", () => cartaTerrenoRenderer?.DeslocarMapa(new Vector2(0f, 0.10f)));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "+", () => cartaTerrenoRenderer?.AjustarZoom(1f));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "−", () => cartaTerrenoRenderer?.AjustarZoom(-1f));
        AdicionarBotaoNavegacaoToolkit(controlesNavegacao, "QG", () =>
        {
            if (cartaTerrenoRenderer != null && quartel != null)
                cartaTerrenoRenderer.SolicitarCentralizacao(quartel.transform.position);
        });

        GarantirCartaTopograficaView();
        Label escala = Texto("CARTA REAL | CAMERA ESTAVEL", 11, new Color(0.65f, 0.82f, 0.82f), FontStyle.Normal);
        escala.style.position = Position.Absolute;
        escala.style.left = 10;
        escala.style.bottom = 8;
        escala.pickingMode = PickingMode.Ignore;
        mapa.Add(escala);
        escalaCartaPersistente = escala;
        cartaPersistenteConstruida = true;
        AtualizarCartaPersistente();
    }

    private void AdicionarBotaoNavegacaoToolkit(VisualElement pai, string texto, Action acao)
    {
        if (pai == null) return;
        bool centralizar = texto == "QG";
        float largura = centralizar ? 46f : 34f;
        Button botao = Botao(texto, largura, 34f, new Color(0.04f, 0.27f, 0.34f, 0.98f));
        botao.style.marginLeft = 3;
        botao.style.marginRight = 3;
        botao.style.marginTop = 0;
        botao.style.marginBottom = 0;
        botao.style.fontSize = centralizar ? 11 : 17;
        botao.tooltip = centralizar ? "Centralizar a Carta no Quartel" :
            texto == "+" ? "Aproximar" : texto == "−" ? "Afastar" :
            texto == "←" ? "Mover a Carta para a esquerda" :
            texto == "→" ? "Mover a Carta para a direita" :
            texto == "↑" ? "Mover a Carta para cima" : "Mover a Carta para baixo";
        RegistrarAcaoBotao(botao, () =>
        {
            acao?.Invoke();
            AtualizarPainel();
        });
        pai.Add(botao);
    }

    private void AtualizarCartaPersistente()
    {
        if (!cartaPersistenteConstruida || mapaCartaPersistente == null || quartel == null) return;

        // Reavalia o modo/area apenas nesta rotina de estado. A chamada não
        // recria a Camera nem o RenderTexture; ela só aplica uma centralização
        // ou troca 2D/3D explicitamente solicitada pela interface.
        ObterTexturaTerrenoCarta(Mathf.Max(100f, ObterRaioCarta()), ObterAspectoCarta());
        if (tituloCartaPersistente != null)
            tituloCartaPersistente.text = cartaVista3D ? "VISUALIZACAO 3D INCLINADA" : "TOPOGRAFIA 2D  |  CURVAS DE NIVEL";
        if (botaoCarta2D != null)
            botaoCarta2D.style.backgroundColor = cartaVista3D ? CorNavegacaoQuartel : CorNavegacaoAtivaQuartel;
        if (botaoCarta3D != null)
            botaoCarta3D.style.backgroundColor = cartaVista3D ? CorNavegacaoAtivaQuartel : CorNavegacaoQuartel;

        AtualizarMarcadoresCarta();
        AtualizarTrajetoriasCarta();
        AtualizarTelemetriaCarta();
    }

    private bool CampoCoordenadaEmEdicao()
    {
        if (campoCoordenadaX == null || campoCoordenadaY == null || campoCoordenadaZ == null) return false;
        Focusable focado = campoCoordenadaX.panel != null ? campoCoordenadaX.panel.focusController.focusedElement : null;
        return focado == campoCoordenadaX || focado == campoCoordenadaY || focado == campoCoordenadaZ;
    }

    private void AtualizarMarcadoresCarta()
    {
        if (camadaMarcadoresCarta == null || cartaTerrenoRenderer == null) return;
        idsMarcadoresCartaPresentes.Clear();

        if (cartaTopograficaView != null)
        {
            for (int i = 0; i < cartaTopograficaView.Unidades.Count; i++)
            {
                QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.Unidades[i];
                if (unidade == null) continue;
                string id = "unidade:" + unidade.id;
                idsMarcadoresCartaPresentes.Add(id);
                Color cor = !unidade.aliada ? new Color(1f, 0.25f, 0.20f) : CorTipoCarta(unidade.tipo);
                bool emFoco = unidade.id == cartaUnidadeSelecionadaId;
                bool selecionadaParaDisparo = UnidadeLancamentoCartaEstaSelecionada(unidade.id);
                string tooltip = (emFoco ? "UNIDADE CLICADA: " : "UNIDADE: ") + unidade.nome
                    + " | " + unidade.estado
                    + (selecionadaParaDisparo ? "\nSELECIONADA PARA DISPARO" : string.Empty);
                Button marcador = ObterMarcadorCarta(id, tooltip, cor, emFoco || selecionadaParaDisparo ? 18f : 16f,
                    evt => AoSelecionarUnidadeCarta(unidade.id, evt));
                int larguraBorda = emFoco ? 3 : selecionadaParaDisparo ? 2 : 0;
                Color corBorda = emFoco ? Color.white : CorVerdeQuartel;
                marcador.style.borderTopWidth = larguraBorda;
                marcador.style.borderBottomWidth = larguraBorda;
                marcador.style.borderLeftWidth = larguraBorda;
                marcador.style.borderRightWidth = larguraBorda;
                marcador.style.borderTopColor = corBorda;
                marcador.style.borderBottomColor = corBorda;
                marcador.style.borderLeftColor = corBorda;
                marcador.style.borderRightColor = corBorda;
                AtualizarPosicaoMarcador(marcador, unidade.posicao);
            }

            for (int i = 0; i < cartaTopograficaView.Misseis.Count; i++)
            {
                QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.Misseis[i];
                if (missil == null) continue;
                string id = "missil:" + missil.id;
                idsMarcadoresCartaPresentes.Add(id);
                Color cor = missil.aliado ? new Color(0.35f, 0.72f, 1f) : new Color(1f, 0.22f, 0.18f);
                Button marcador = ObterMarcadorCarta(id, missil.nome + " | " + missil.estado, cor, 12f,
                    evt => AoSelecionarMissilCarta(missil.id));
                AtualizarPosicaoMarcador(marcador, missil.posicao);
            }
        }

        if (quartel != null)
        {
            for (int i = 0; i < quartel.ContatosMilitares.Count; i++)
            {
                GerenciadorQuartel.ContatoMilitarQuartelV2 contato = quartel.ContatosMilitares[i];
                if (contato == null || !contato.inimigo) continue;
                string id = "contato:" + contato.id;
                idsMarcadoresCartaPresentes.Add(id);
                Button marcador = ObterMarcadorCarta(id,
                    contato.nome + " | " + contato.estado + " | " + contato.transmissor,
                    new Color(1f, 0.10f, 0.10f), 18f,
                    evt => AoSelecionarContatoCarta(contato.id));
                AtualizarPosicaoMarcador(marcador, contato.posicao);
            }
        }

        foreach (KeyValuePair<string, Button> par in marcadoresCarta)
        {
            if (!idsMarcadoresCartaPresentes.Contains(par.Key))
                par.Value.style.display = DisplayStyle.None;
        }
    }

    private Button ObterMarcadorCarta(string id, string tooltip, Color cor, float tamanho, Action<PointerDownEvent> aoClicar)
    {
        Button marcador;
        if (!marcadoresCarta.TryGetValue(id, out marcador) || marcador == null)
        {
            marcador = new Button { text = string.Empty };
            marcador.style.position = Position.Absolute;
            marcador.style.paddingLeft = 0;
            marcador.style.paddingRight = 0;
            marcador.style.paddingTop = 0;
            marcador.style.paddingBottom = 0;
            marcador.style.borderTopLeftRadius = tamanho;
            marcador.style.borderTopRightRadius = tamanho;
            marcador.style.borderBottomLeftRadius = tamanho;
            marcador.style.borderBottomRightRadius = tamanho;
            marcador.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
                aoClicar?.Invoke(evt);
            });
            marcadoresCarta[id] = marcador;
            camadaMarcadoresCarta.Add(marcador);
        }

        marcador.tooltip = tooltip;
        marcador.style.width = tamanho;
        marcador.style.height = tamanho;
        marcador.style.marginLeft = -tamanho * 0.5f;
        marcador.style.marginTop = -tamanho * 0.5f;
        marcador.style.backgroundColor = cor;
        marcador.style.display = DisplayStyle.Flex;
        return marcador;
    }

    private void AtualizarPosicaoMarcador(Button marcador, Vector3 posicao)
    {
        if (marcador == null || cartaTerrenoRenderer == null) return;
        Vector3 viewport;
        if (!cartaTerrenoRenderer.TryWorldToViewport(posicao, out viewport)
            || viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            marcador.style.display = DisplayStyle.None;
            return;
        }

        marcador.style.display = DisplayStyle.Flex;
        marcador.style.left = new Length(viewport.x * 100f, LengthUnit.Percent);
        marcador.style.top = new Length((1f - viewport.y) * 100f, LengthUnit.Percent);
    }

    private void AtualizarTrajetoriasCarta()
    {
        if (camadaTrajetoriasCarta == null || quartel == null || cartaTerrenoRenderer == null) return;

        HashSet<string> presentes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> misseisAtivos = new HashSet<string>(StringComparer.Ordinal);

        // Cada míssil real recebe duas linhas independentes: o trecho que já
        // percorreu e o trecho ainda estimado até o ponto-alvo conhecido.
        // Nenhuma linha cria ou move um objeto no mundo; é apenas a leitura da
        // posição atual do MissileThreatTracker.
        if (cartaTopograficaView != null)
        {
            for (int i = 0; i < cartaTopograficaView.Misseis.Count; i++)
            {
                QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.Misseis[i];
                if (missil == null || string.IsNullOrWhiteSpace(missil.id)) continue;

                string idBase = "missil-traj:" + missil.id;
                misseisAtivos.Add(missil.id);

                Vector3 vpOrigem;
                Vector3 vpAtual;
                Vector3 vpDestino;
                bool possuiOrigem = cartaTerrenoRenderer.TryWorldToViewport(missil.pontoLancamento, out vpOrigem);
                bool possuiAtual = cartaTerrenoRenderer.TryWorldToViewport(missil.posicao, out vpAtual);
                bool possuiDestino = cartaTerrenoRenderer.TryWorldToViewport(missil.pontoProvavelImpacto, out vpDestino);

                if (possuiOrigem && possuiAtual && vpOrigem.z > 0f && vpAtual.z > 0f)
                {
                    AtualizarLinhaTrajetoria(
                        trajetoriasPercorridasCarta,
                        idBase + ":percorrida",
                        new Vector2(vpOrigem.x, 1f - vpOrigem.y),
                        new Vector2(vpAtual.x, 1f - vpAtual.y),
                        new Color(0.20f, 0.85f, 1f, 0.95f),
                        3f,
                        presentes);
                }

                if (possuiAtual && possuiDestino && vpAtual.z > 0f && vpDestino.z > 0f)
                {
                    AtualizarLinhaTrajetoria(
                        trajetoriasEstimadasCarta,
                        idBase + ":estimada",
                        new Vector2(vpAtual.x, 1f - vpAtual.y),
                        new Vector2(vpDestino.x, 1f - vpDestino.y),
                        new Color(1f, 0.55f, 0.16f, 0.95f),
                        2f,
                        presentes);
                }
            }
        }

        // A trilha do Quartel continua sendo usada como fallback durante a
        // janela entre o disparo e o primeiro registro do rastreador real.
        for (int i = 0; i < quartel.TrilhasLancamento.Count; i++)
        {
            GerenciadorQuartel.TrilhaLancamentoCoordenadoV2 trilha = quartel.TrilhasLancamento[i];
            if (trilha == null) continue;
            if (!string.IsNullOrWhiteSpace(trilha.missilId) && misseisAtivos.Contains("missil-" + trilha.missilId))
                continue;

            string idTrilha = "operacao:" + trilha.id;
            Vector3 origem = trilha.pontoLancamento;
            Vector3 destino = trilha.pontoImpactoPrevisto;
            Vector3 vpOrigem;
            Vector3 vpDestino;
            if (!cartaTerrenoRenderer.TryWorldToViewport(origem, out vpOrigem)
                || !cartaTerrenoRenderer.TryWorldToViewport(destino, out vpDestino)
                || vpOrigem.z <= 0f || vpDestino.z <= 0f) continue;

            AtualizarLinhaTrajetoria(
                trajetoriasCarta,
                idTrilha,
                new Vector2(vpOrigem.x, 1f - vpOrigem.y),
                new Vector2(vpDestino.x, 1f - vpDestino.y),
                new Color(1f, 0.54f, 0.16f, 0.95f),
                2f,
                presentes);
        }

        OcultarTrajetoriasNaoPresentes(trajetoriasCarta, presentes);
        OcultarTrajetoriasNaoPresentes(trajetoriasPercorridasCarta, presentes);
        OcultarTrajetoriasNaoPresentes(trajetoriasEstimadasCarta, presentes);
    }

    private void AtualizarLinhaTrajetoria(
        Dictionary<string, VisualElement> cache,
        string id,
        Vector2 inicio,
        Vector2 fim,
        Color cor,
        float espessura,
        HashSet<string> presentes)
    {
        if (cache == null || string.IsNullOrWhiteSpace(id) || camadaTrajetoriasCarta == null) return;
        presentes.Add(id);

        VisualElement linha;
        if (!cache.TryGetValue(id, out linha) || linha == null)
        {
            linha = new LinhaCartaLancamentoToolkit(Vector2.zero, Vector2.zero, cor, espessura);
            cache[id] = linha;
            camadaTrajetoriasCarta.Add(linha);
        }

        LinhaCartaLancamentoToolkit linhaToolkit = linha as LinhaCartaLancamentoToolkit;
        if (linhaToolkit != null)
        {
            linhaToolkit.Atualizar(inicio, fim);
            linhaToolkit.style.display = DisplayStyle.Flex;
        }
    }

    private static void OcultarTrajetoriasNaoPresentes(Dictionary<string, VisualElement> cache, HashSet<string> presentes)
    {
        if (cache == null) return;
        foreach (KeyValuePair<string, VisualElement> par in cache)
        {
            if (par.Value != null)
                par.Value.style.display = presentes.Contains(par.Key) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void AtualizarTelemetriaCarta()
    {
        if (telemetriaCartaPersistente == null || quartel == null) return;

        if (!telemetriaCartaEstruturaConstruida)
        {
            telemetriaCartaPersistente.Clear();
            telemetriaCartaPersistente.Add(Texto("TELEMETRIA E CONTROLE DE ATAQUE", 14, new Color(0.16f, 0.79f, 0.98f), FontStyle.Bold));
            dadosTelemetriaCartaPersistentes = new VisualElement { name = "quartel-carta-dados" };
            dadosTelemetriaCartaPersistentes.style.flexShrink = 0;
            tituloTelemetriaCartaPersistente = Texto("SEM SELEÇÃO", 18, Color.white, FontStyle.Bold);
            tituloTelemetriaCartaPersistente.style.marginTop = 8;
            tituloTelemetriaCartaPersistente.style.marginBottom = 6;
            dadosTelemetriaCartaPersistentes.Add(tituloTelemetriaCartaPersistente);
            textoTelemetriaCartaPersistente = Texto(string.Empty, 12, CorTextoSecundarioQuartel, FontStyle.Normal);
            textoTelemetriaCartaPersistente.style.whiteSpace = WhiteSpace.Normal;
            dadosTelemetriaCartaPersistentes.Add(textoTelemetriaCartaPersistente);
            statusLancamentoCarta = Texto("ESTADO DO DISPARO: AGUARDANDO ORDEM", 12, CorTextoSecundarioQuartel, FontStyle.Bold);
            statusLancamentoCarta.style.marginTop = 6;
            statusLancamentoCarta.style.marginBottom = 6;
            statusLancamentoCarta.style.whiteSpace = WhiteSpace.Normal;
            dadosTelemetriaCartaPersistentes.Add(statusLancamentoCarta);
            botaoSelecionarLancadorTelemetria = BotaoAcao("SELECIONAR LANÇADOR", () =>
            {
                if (quartel != null && !string.IsNullOrWhiteSpace(cartaUnidadeSelecionadaId))
                {
                    quartel.AlternarSelecaoLancamento(cartaUnidadeSelecionadaId, false);
                    AtualizarPainel();
                }
            });
            botaoSelecionarLancadorTelemetria.style.display = DisplayStyle.None;
            dadosTelemetriaCartaPersistentes.Add(botaoSelecionarLancadorTelemetria);
            botaoUsarCoordenadasTelemetria = BotaoAcao("USAR COORDENADAS DO ALVO", () =>
            {
                if (quartel != null)
                {
                    quartel.UsarCoordenadasDoAlvo();
                    AtualizarPainel();
                }
            });
            botaoUsarCoordenadasTelemetria.style.display = DisplayStyle.None;
            dadosTelemetriaCartaPersistentes.Add(botaoUsarCoordenadasTelemetria);
            botaoRastrearMissilCarta = BotaoAcao("◎  RASTREAR MÍSSIL", IniciarRastreamentoMissilCarta);
            botaoRastrearMissilCarta.style.display = DisplayStyle.None;
            botaoRastrearMissilCarta.tooltip = "Acompanhar somente com a câmera da Carta Náutica";
            dadosTelemetriaCartaPersistentes.Add(botaoRastrearMissilCarta);
            botaoPararRastreamentoMissilCarta = BotaoAcao("■  PARAR RASTREAMENTO", PararRastreamentoMissilCarta, true);
            botaoPararRastreamentoMissilCarta.style.display = DisplayStyle.None;
            botaoPararRastreamentoMissilCarta.tooltip = "Parar o acompanhamento e manter a câmera no último centro";
            dadosTelemetriaCartaPersistentes.Add(botaoPararRastreamentoMissilCarta);
            listaAeronavesCartaPersistente = Card("AERONAVES E UNIDADES NA CARTA");
            listaAeronavesCartaVazia = Texto("Nenhuma unidade com telemetria foi encontrada na cobertura.", 12, CorTextoSecundarioQuartel, FontStyle.Normal);
            listaAeronavesCartaVazia.style.display = DisplayStyle.None;
            listaAeronavesCartaPersistente.Add(listaAeronavesCartaVazia);
            dadosTelemetriaCartaPersistentes.Add(listaAeronavesCartaPersistente);
            telemetriaCartaPersistente.Add(dadosTelemetriaCartaPersistentes);

            controlesTelemetriaCartaPersistentes = Card("CONTROLE DE ATAQUE");
            VisualElement coordenadas = Linha();
            campoCoordenadaX = new TextField("X") { value = "0" };
            campoCoordenadaY = new TextField("Y") { value = "0" };
            campoCoordenadaZ = new TextField("Z") { value = "0" };
            campoCoordenadaX.style.flexGrow = 1;
            campoCoordenadaY.style.flexGrow = 1;
            campoCoordenadaZ.style.flexGrow = 1;
            coordenadas.Add(campoCoordenadaX);
            coordenadas.Add(campoCoordenadaY);
            coordenadas.Add(campoCoordenadaZ);
            controlesTelemetriaCartaPersistentes.Add(coordenadas);
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("＋  INSERIR COORDENADAS", InserirCoordenadasCarta));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("⌖  CLICAR NO TERRENO", () => { cliqueTerrenoArmado = true; }));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("◎  CENTRALIZAR NO QG", () =>
            {
                if (cartaTerrenoRenderer != null && quartel != null)
                    cartaTerrenoRenderer.SolicitarCentralizacao(quartel.transform.position);
                AtualizarPainel();
            }));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("◉  ATAQUE MANUAL", () => ExecutarAtaqueCarta(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual)));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("◌  ATAQUE AUTOMÁTICO", () => ExecutarAtaqueCarta(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico)));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("▶  LANÇAMENTO COORDENADO", () => ExecutarAtaqueCarta(quartel.ModoLancamentoCoordenado)));
            controlesTelemetriaCartaPersistentes.Add(BotaoAcao("×  CANCELAR", () => { quartel.CancelarOperacaoLancamento(); cliqueTerrenoArmado = false; AtualizarPainel(); }));
            telemetriaCartaPersistente.Add(controlesTelemetriaCartaPersistentes);

            VisualElement logCombate = Card("LOG DE COMBATE");
            resumoDestruicoesCarta = Texto("ABATES ALIADOS: 0  |  PERDAS ALIADAS: 0", 12, Color.white, FontStyle.Bold);
            resumoDestruicoesCarta.style.marginTop = 4;
            resumoDestruicoesCarta.style.marginBottom = 6;
            logCombate.Add(resumoDestruicoesCarta);
            ScrollView rolagemEventos = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "quartel-carta-log-combate"
            };
            rolagemEventos.style.minHeight = 110;
            rolagemEventos.style.maxHeight = 230;
            rolagemEventos.style.flexShrink = 1;
            listaEventosCombateCarta = new VisualElement { name = "quartel-carta-eventos" };
            listaEventosCombateCarta.style.flexDirection = FlexDirection.Column;
            rolagemEventos.Add(listaEventosCombateCarta);
            logCombate.Add(rolagemEventos);
            telemetriaCartaPersistente.Add(logCombate);
            telemetriaCartaEstruturaConstruida = true;
        }

        if (dadosTelemetriaCartaPersistentes == null || textoTelemetriaCartaPersistente == null) return;

        QuartelCartaTopograficaView.UnidadeTelemetria selecionada = cartaTopograficaView != null
            ? cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId) : null;
        GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidadeLancamentoFoco = selecionada != null
            ? EncontrarUnidadeLancamentoCarta(selecionada.id) : null;
        QuartelCartaTopograficaView.MissilTelemetria missilSelecionado = cartaTopograficaView != null
            ? cartaTopograficaView.EncontrarMissil(cartaMissilSelecionadoId) : null;
        GerenciadorQuartel.ContatoMilitarQuartelV2 contatoSelecionado = EncontrarContatoCarta(quartel.AlvoSelecionadoLancamentoId);
        MissileThreatTracker trackerSelecionado = ObterTrackerSelecionadoCarta();
        string resumoLancadores = ObterResumoLancadoresSelecionados();
        bool rastreamentoEncerrado = !string.IsNullOrWhiteSpace(cartaMissilSelecionadoId)
            && cartaTerrenoRenderer != null
            && cartaTerrenoRenderer.EstaRastreando
            && trackerSelecionado == null;
        if (rastreamentoEncerrado)
            cartaTerrenoRenderer.PararRastreamento();
        List<string> linhasTelemetria = new List<string>();

        if (botaoSelecionarLancadorTelemetria != null)
            botaoSelecionarLancadorTelemetria.style.display = DisplayStyle.None;
        if (botaoUsarCoordenadasTelemetria != null)
            botaoUsarCoordenadasTelemetria.style.display = DisplayStyle.None;
        if (botaoRastrearMissilCarta != null)
            botaoRastrearMissilCarta.style.display = DisplayStyle.None;
        if (botaoPararRastreamentoMissilCarta != null)
            botaoPararRastreamentoMissilCarta.style.display = DisplayStyle.None;

        if (statusLancamentoCarta != null)
        {
            statusLancamentoCarta.text = rastreamentoEncerrado
                ? "ESTADO DO DISPARO: RASTREAMENTO ENCERRADO — MÍSSIL FORA DO REGISTRO ATIVO"
                : string.IsNullOrWhiteSpace(quartel.UltimoMotivoLancamento)
                ? "ESTADO DO DISPARO: AGUARDANDO ORDEM | VÃO ATIRAR: " + resumoLancadores
                : "ESTADO DO DISPARO: " + quartel.UltimoMotivoLancamento + " | PRÓXIMO: " + resumoLancadores;
            statusLancamentoCarta.style.color = CorTextoSecundarioQuartel;
        }

        if (selecionada != null)
        {
            tituloTelemetriaCartaPersistente.text = "UNIDADE CLICADA  |  " + selecionada.nome;
            linhasTelemetria.Add("Tipo: " + selecionada.tipo);
            linhasTelemetria.Add("Seleção para disparo: " + (unidadeLancamentoFoco == null
                ? "NÃO É LANÇADORA DO QUARTEL"
                : unidadeLancamentoFoco.selecionada ? "SIM — VAI ATIRAR" : "NÃO — AINDA NÃO VAI ATIRAR"));
            linhasTelemetria.Add("Posição atual: " + FormatarPosicao(selecionada.posicao));
            linhasTelemetria.Add("Modo operacional: " + selecionada.estado);
            linhasTelemetria.Add("Estado: " + selecionada.situacao);
            linhasTelemetria.Add("Missão: " + selecionada.missao);
            linhasTelemetria.Add("Rumo: " + selecionada.rumo);
            linhasTelemetria.Add("Velocidade: " + selecionada.velocidadeMetrosPorSegundo.ToString("0.0") + " m/s");
            linhasTelemetria.Add("Altitude: " + selecionada.altitudeAbsoluta.ToString("0.0") + " m");
            linhasTelemetria.Add("Acima do terreno: " + selecionada.alturaAcimaDoSolo.ToString("0.0") + " m");
            linhasTelemetria.Add("Combustível: " + (selecionada.combustivelCapacidade > 0f
                ? (selecionada.combustivelPercentual * 100f).ToString("0") + "%"
                : "SEM SENSOR"));
            linhasTelemetria.Add("Armamento: " + selecionada.armamento);
            if (unidadeLancamentoFoco != null && botaoSelecionarLancadorTelemetria != null)
            {
                botaoSelecionarLancadorTelemetria.text = unidadeLancamentoFoco.selecionada
                    ? "✓ LANÇADORA SELECIONADA — REMOVER"
                    : "SELECIONAR COMO LANÇADORA";
                botaoSelecionarLancadorTelemetria.tooltip = unidadeLancamentoFoco.selecionada
                    ? "Remover " + selecionada.nome + " da lista que vai atirar"
                    : "Adicionar " + selecionada.nome + " à lista que vai atirar";
                botaoSelecionarLancadorTelemetria.style.display = DisplayStyle.Flex;
            }
        }
        else if (contatoSelecionado != null)
        {
            tituloTelemetriaCartaPersistente.text = "ALVO SELECIONADO  |  " + contatoSelecionado.nome;
            linhasTelemetria.Add("ID: " + contatoSelecionado.id);
            linhasTelemetria.Add("Tipo: " + contatoSelecionado.tipo);
            linhasTelemetria.Add("País/Time: " + contatoSelecionado.pais);
            linhasTelemetria.Add("Transmitido por: " + contatoSelecionado.transmissor);
            linhasTelemetria.Add("X/Y/Z: " + FormatarPosicao(contatoSelecionado.posicao));
            linhasTelemetria.Add("Horário: " + contatoSelecionado.horario);
            linhasTelemetria.Add("Estado: " + contatoSelecionado.estado);
            if (botaoUsarCoordenadasTelemetria != null)
                botaoUsarCoordenadasTelemetria.style.display = DisplayStyle.Flex;
        }
        else if (missilSelecionado != null)
        {
            tituloTelemetriaCartaPersistente.text = "MÍSSIL  |  " + missilSelecionado.nome;
            string estadoMissil = trackerSelecionado != null
                ? (cartaTerrenoRenderer != null && cartaTerrenoRenderer.EstaRastreando ? "RASTREADO" : "EM VOO")
                : missilSelecionado.estado;
            string alvoMissil = trackerSelecionado != null ? trackerSelecionado.AlvoNome : string.Empty;
            if (string.IsNullOrWhiteSpace(alvoMissil)) alvoMissil = "coordenada real " + FormatarPosicao(missilSelecionado.pontoProvavelImpacto);
            linhasTelemetria.Add("Tipo: " + missilSelecionado.tipo);
            linhasTelemetria.Add("Lançador: " + missilSelecionado.origem);
            linhasTelemetria.Add("Alvo: " + alvoMissil);
            linhasTelemetria.Add("Estado: " + estadoMissil);
            linhasTelemetria.Add("Posição atual: " + FormatarPosicao(missilSelecionado.posicao));
            linhasTelemetria.Add("Ponto de lançamento: " + FormatarPosicao(missilSelecionado.pontoLancamento));
            linhasTelemetria.Add("Ponto provável de impacto: " + FormatarPosicao(missilSelecionado.pontoProvavelImpacto));
            linhasTelemetria.Add("Distância percorrida: " + missilSelecionado.distanciaPercorrida.ToString("0.0") + " m");
            linhasTelemetria.Add("Distância restante: " + missilSelecionado.distanciaRestante.ToString("0.0") + " m");
            linhasTelemetria.Add("Distância total: " + missilSelecionado.distanciaLancadorAlvo.ToString("0.0") + " m");
            linhasTelemetria.Add("Altitude: " + missilSelecionado.posicao.y.ToString("0.0") + " m");
            linhasTelemetria.Add("Velocidade: " + missilSelecionado.velocidadeMetrosPorSegundo.ToString("0.0") + " m/s");
            linhasTelemetria.Add("Tempo de voo: " + FormatarTempo(missilSelecionado.tempoDesdeLancamento));
            linhasTelemetria.Add("Guiagem: " + (missilSelecionado.guiagemPerdida ? "ALVO DINÂMICO INDISPONÍVEL" : "DISPONÍVEL"));
            if (trackerSelecionado != null)
            {
                if (cartaTerrenoRenderer != null && cartaTerrenoRenderer.EstaRastreando)
                {
                    if (statusLancamentoCarta != null)
                    {
                        statusLancamentoCarta.text = "ESTADO DO DISPARO: RASTREANDO MÍSSIL REAL";
                        statusLancamentoCarta.style.color = CorCianoQuartel;
                    }
                    if (botaoPararRastreamentoMissilCarta != null)
                        botaoPararRastreamentoMissilCarta.style.display = DisplayStyle.Flex;
                }
                else if (botaoRastrearMissilCarta != null)
                {
                    botaoRastrearMissilCarta.style.display = DisplayStyle.Flex;
                }
            }
        }
        else
        {
            tituloTelemetriaCartaPersistente.text = "SEM SELEÇÃO";
            linhasTelemetria.Add("Selecione uma aeronave, navio, submarino, míssil ou contato inimigo na carta.");
        }

        linhasTelemetria.Add("Unidades selecionadas: " + ContarUnidadesLancamentoSelecionadas().ToString("00"));
        linhasTelemetria.Add("VÃO ATIRAR: " + resumoLancadores);
        linhasTelemetria.Add("Modo do Quartel: " + (quartel.ModoLancamentoCoordenado == GerenciadorQuartel.ModoLancamentoCoordenadoV2.Automatico ? "AUTOMÁTICO" : "MANUAL"));
        linhasTelemetria.Add("Clique no terreno: " + (cliqueTerrenoArmado ? "ARMADO" : "DESARMADO"));
        if (quartel.UnidadesAbatidas.Count > 0)
        {
            linhasTelemetria.Add("Unidades abatidas registradas: " + quartel.UnidadesAbatidas.Count.ToString("N0"));
            int limite = Mathf.Min(8, quartel.UnidadesAbatidas.Count);
            for (int i = 0; i < limite; i++)
            {
                GerenciadorQuartel.UnidadeAbatidaQuartelV2 unidade = quartel.UnidadesAbatidas[i];
                if (unidade == null) continue;
                linhasTelemetria.Add("  • " + unidade.nome + " | " + unidade.tipo + " | " + unidade.horario);
            }
        }

        textoTelemetriaCartaPersistente.text = string.Join("\n", linhasTelemetria);

        HashSet<string> aeronavesPresentes = new HashSet<string>(StringComparer.Ordinal);
        int unidadesVisiveis = 0;
        if (cartaTopograficaView != null && listaAeronavesCartaPersistente != null)
        {
            int limite = Mathf.Min(12, cartaTopograficaView.Unidades.Count);
            for (int i = 0; i < limite; i++)
            {
                QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.Unidades[i];
                if (unidade == null || string.IsNullOrWhiteSpace(unidade.id)) continue;
                string idUnidade = unidade.id;
                aeronavesPresentes.Add(idUnidade);
                unidadesVisiveis++;
                bool emFoco = idUnidade == cartaUnidadeSelecionadaId;
                bool selecionadaParaDisparo = UnidadeLancamentoCartaEstaSelecionada(idUnidade);
                Button botaoUnidade;
                if (!botoesAeronavesCarta.TryGetValue(idUnidade, out botaoUnidade) || botaoUnidade == null)
                {
                    botaoUnidade = Botao(string.Empty, 0, 38, CorNavegacaoQuartel);
                    botaoUnidade.style.flexGrow = 1;
                    string idPersistente = idUnidade;
                    RegistrarAcaoBotao(botaoUnidade, () => SelecionarUnidadeCarta(idPersistente, false, false));
                    botoesAeronavesCarta[idUnidade] = botaoUnidade;
                    listaAeronavesCartaPersistente.Add(botaoUnidade);
                }
                botaoUnidade.text = (emFoco ? "▶ " : unidade.aliada ? "● " : "◆ ")
                    + EncurtarTextoDesigner(unidade.nome, 22) + " | "
                    + EncurtarTextoDesigner(unidade.tipo, 12) + " | "
                    + EncurtarTextoDesigner(unidade.estado, 16)
                    + (selecionadaParaDisparo ? " | VAI ATIRAR" : string.Empty);
                botaoUnidade.tooltip = (emFoco ? "UNIDADE CLICADA: " : "Abrir telemetria de ") + unidade.nome
                    + " | " + FormatarPosicao(unidade.posicao)
                    + (selecionadaParaDisparo ? "\nSelecionada para disparo." : string.Empty);
                botaoUnidade.style.backgroundColor = selecionadaParaDisparo
                    ? CorNavegacaoAtivaQuartel : emFoco ? new Color(0.05f, 0.16f, 0.20f) : CorNavegacaoQuartel;
                botaoUnidade.style.display = DisplayStyle.Flex;
            }
        }

        if (listaAeronavesCartaVazia != null)
            listaAeronavesCartaVazia.style.display = unidadesVisiveis == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        foreach (KeyValuePair<string, Button> par in botoesAeronavesCarta)
        {
            if (!aeronavesPresentes.Contains(par.Key) && par.Value != null)
                par.Value.style.display = DisplayStyle.None;
        }

        AtualizarEventosCombateCarta();
    }

    private MissileThreatTracker ObterTrackerSelecionadoCarta()
    {
        if (string.IsNullOrWhiteSpace(cartaMissilSelecionadoId)) return null;
        string idTexto = cartaMissilSelecionadoId.StartsWith("missil-", StringComparison.Ordinal)
            ? cartaMissilSelecionadoId.Substring("missil-".Length)
            : cartaMissilSelecionadoId;
        int id;
        return int.TryParse(idTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
            && MissileThreatTracker.TryObterAtivo(id, out MissileThreatTracker tracker)
            ? tracker
            : null;
    }

    private void IniciarRastreamentoMissilCarta()
    {
        MissileThreatTracker tracker = ObterTrackerSelecionadoCarta();
        if (tracker == null || tracker.RaizMissil == null || cartaTerrenoRenderer == null) return;
        cartaTerrenoRenderer.IniciarRastreamento(tracker.RaizMissil);
        cartaTerrenoRenderer.SolicitarCentralizacao(tracker.RaizMissil.position);
        if (statusLancamentoCarta != null)
        {
            statusLancamentoCarta.text = "ESTADO DO DISPARO: RASTREANDO MÍSSIL REAL";
            statusLancamentoCarta.style.color = CorCianoQuartel;
        }
        AtualizarPainel();
    }

    private void PararRastreamentoMissilCarta()
    {
        if (cartaTerrenoRenderer != null) cartaTerrenoRenderer.PararRastreamento();
        if (statusLancamentoCarta != null)
        {
            statusLancamentoCarta.text = "ESTADO DO DISPARO: RASTREAMENTO PARADO";
            statusLancamentoCarta.style.color = CorTextoSecundarioQuartel;
        }
        AtualizarPainel();
    }

    private void AtualizarEventosCombateCarta()
    {
        if (listaEventosCombateCarta == null || quartel == null) return;
        CartaCombateRegistro.CopiarEventos(eventosCombateCarta);

        int abatesAliados = 0;
        int perdasAliadas = 0;
        int exibidos = 0;
        HashSet<string> presentes = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < eventosCombateCarta.Count; i++)
        {
            CartaCombateRegistro.EventoCombate evento = eventosCombateCarta[i];
            if (evento == null) continue;

            if (evento.tipo == "UNIDADE DESTRUÍDA")
            {
                if (evento.equipeAtacante == quartel.teamID && evento.equipeAlvo != quartel.teamID)
                    abatesAliados++;
                else if (evento.equipeAlvo == quartel.teamID && evento.equipeAtacante != quartel.teamID)
                    perdasAliadas++;
            }

            if (exibidos >= 24 || string.IsNullOrWhiteSpace(evento.id)) continue;
            exibidos++;
            presentes.Add(evento.id);

            Button botao;
            if (!botoesEventosCombateCarta.TryGetValue(evento.id, out botao) || botao == null)
            {
                botao = Botao(string.Empty, 0, 46, CorNavegacaoQuartel);
                botao.style.flexGrow = 1;
                botao.style.whiteSpace = WhiteSpace.Normal;
                string idEvento = evento.id;
                RegistrarAcaoBotao(botao, () => AoClicarEventoCombate(idEvento));
                botoesEventosCombateCarta[evento.id] = botao;
                listaEventosCombateCarta.Add(botao);
            }

            botao.text = "[" + evento.horario + "] " + evento.tipo + "\n" + evento.descricao;
            botao.tooltip = MontarTooltipEventoCombate(evento);
            botao.style.display = DisplayStyle.Flex;
        }

        foreach (KeyValuePair<string, Button> par in botoesEventosCombateCarta)
        {
            if (!presentes.Contains(par.Key) && par.Value != null)
                par.Value.style.display = DisplayStyle.None;
        }

        if (resumoDestruicoesCarta != null)
            resumoDestruicoesCarta.text = "ABATES ALIADOS: " + abatesAliados + "  |  PERDAS ALIADAS: " + perdasAliadas;
    }

    private static string MontarTooltipEventoCombate(CartaCombateRegistro.EventoCombate evento)
    {
        if (evento == null) return string.Empty;
        return "Atacante: " + (string.IsNullOrWhiteSpace(evento.atacante) ? "N/D" : evento.atacante)
            + "\nAlvo: " + (string.IsNullOrWhiteSpace(evento.alvo) ? "N/D" : evento.alvo)
            + "\nArma: " + (string.IsNullOrWhiteSpace(evento.arma) ? "N/D" : evento.arma)
            + "\nResultado: " + (string.IsNullOrWhiteSpace(evento.resultado) ? "N/D" : evento.resultado)
            + "\nPosição: " + FormatarPosicao(evento.posicao);
    }

    private void AoClicarEventoCombate(string idEvento)
    {
        CartaCombateRegistro.EventoCombate escolhido = null;
        for (int i = 0; i < eventosCombateCarta.Count; i++)
        {
            if (eventosCombateCarta[i] != null && eventosCombateCarta[i].id == idEvento)
            {
                escolhido = eventosCombateCarta[i];
                break;
            }
        }
        if (escolhido == null) return;

        int idMissil;
        if (int.TryParse(escolhido.missilId, NumberStyles.Integer, CultureInfo.InvariantCulture, out idMissil)
            && MissileThreatTracker.TryObterAtivo(idMissil, out MissileThreatTracker tracker)
            && tracker.RaizMissil != null)
        {
            AoSelecionarMissilCarta("missil-" + idMissil);
            if (cartaTerrenoRenderer != null)
            {
                cartaTerrenoRenderer.SolicitarCentralizacao(tracker.RaizMissil.position);
                cartaTerrenoRenderer.IniciarRastreamento(tracker.RaizMissil);
            }
            AtualizarPainel();
            return;
        }

        if (cartaTerrenoRenderer != null)
            cartaTerrenoRenderer.SolicitarCentralizacao(escolhido.posicao);
        AtualizarPainel();
    }

    private int ContarUnidadesLancamentoSelecionadas()
    {
        int total = 0;
        for (int i = 0; i < quartel.UnidadesLancamento.Count; i++)
            if (quartel.UnidadesLancamento[i] != null && quartel.UnidadesLancamento[i].selecionada) total++;
        return total;
    }

    private bool UnidadeLancamentoCartaEstaSelecionada(string idUnidade)
    {
        GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamentoCarta(idUnidade);
        return unidade != null && unidade.selecionada;
    }

    private string ObterResumoLancadoresSelecionados()
    {
        if (quartel == null || quartel.UnidadesLancamento == null) return "NENHUMA UNIDADE";

        List<string> nomes = new List<string>(3);
        int total = 0;
        for (int i = 0; i < quartel.UnidadesLancamento.Count; i++)
        {
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = quartel.UnidadesLancamento[i];
            if (unidade == null || !unidade.selecionada) continue;
            total++;
            if (nomes.Count < 3) nomes.Add(EncurtarTextoDesigner(unidade.nome, 22));
        }

        if (total == 0) return "NENHUMA UNIDADE";
        string resumo = string.Join(", ", nomes);
        if (total > nomes.Count) resumo += " +" + (total - nomes.Count);
        return resumo;
    }

    private void AlternarModoUnidadeLancamento(string idUnidade)
    {
        if (quartel == null || string.IsNullOrWhiteSpace(idUnidade)) return;
        for (int i = 0; i < quartel.UnidadesLancamento.Count; i++)
        {
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = quartel.UnidadesLancamento[i];
            if (unidade == null || !string.Equals(unidade.id, idUnidade, StringComparison.Ordinal)) continue;
            if (unidade.lancadorMisseis != null)
                quartel.DefinirModoLancamentoCoordenado(GerenciadorQuartel.ModoLancamentoCoordenadoV2.Manual);
            else
                quartel.AlternarModoOperacionalLancador(idUnidade);
            AtualizarPainel();
            return;
        }
    }

    private void InserirCoordenadasCarta()
    {
        float x;
        float y;
        float z;
        if (campoCoordenadaX == null || !float.TryParse(campoCoordenadaX.value, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            || !float.TryParse(campoCoordenadaY.value, NumberStyles.Float, CultureInfo.InvariantCulture, out y)
            || !float.TryParse(campoCoordenadaZ.value, NumberStyles.Float, CultureInfo.InvariantCulture, out z))
        {
            return;
        }
        quartel.DefinirPontoAlvoManual(new Vector3(x, y, z), "COORDENADAS DIGITADAS");
        AtualizarPainel();
    }

    private void ExecutarAtaqueCarta(GerenciadorQuartel.ModoLancamentoCoordenadoV2 modo)
    {
        if (quartel == null) return;
        quartel.DefinirModoLancamentoCoordenado(modo);
        if (statusLancamentoCarta != null)
        {
            statusLancamentoCarta.text = "ESTADO DO DISPARO: PREPARANDO LANÇAMENTO...";
            statusLancamentoCarta.style.color = CorAlertaQuartel;
        }
        string motivo;
        bool lancou = quartel.TryExecutarLancamentoCoordenado(out motivo);
        if (statusLancamentoCarta != null)
        {
            statusLancamentoCarta.text = lancou
                ? "ESTADO DO DISPARO: MÍSSIL LANÇADO | " + motivo
                : "ESTADO DO DISPARO: LANÇAMENTO BLOQUEADO | " + motivo;
            statusLancamentoCarta.style.color = lancou ? CorVerdeQuartel : CorAlertaQuartel;
        }
        AtualizarPainel();
    }

    private GerenciadorQuartel.ContatoMilitarQuartelV2 EncontrarContatoCarta(string id)
    {
        if (quartel == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < quartel.ContatosMilitares.Count; i++)
        {
            GerenciadorQuartel.ContatoMilitarQuartelV2 contato = quartel.ContatosMilitares[i];
            if (contato != null && contato.id == id) return contato;
        }
        return null;
    }

    private void AoSelecionarUnidadeCarta(string id, PointerDownEvent evt)
    {
        SelecionarUnidadeCarta(id, evt != null && evt.ctrlKey, evt != null && evt.shiftKey);
    }

    private void SelecionarUnidadeCarta(string id, bool ctrl, bool shift)
    {
        cartaUnidadeSelecionadaId = id;
        cartaContatoSelecionadoId = string.Empty;
        cartaMissilSelecionadoId = string.Empty;
        if (shift)
        {
            quartel.SelecionarUnidadeLancamento(id);
        }
        else
        {
            quartel.AlternarSelecaoLancamento(id, !ctrl);
        }
        if (cartaTopograficaView != null)
        {
            QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.EncontrarUnidade(id);
            if (unidade != null) PreencherCoordenadasCarta(unidade.posicao);
        }
        AtualizarPainel();
    }

    private void AoSelecionarMissilCarta(string id)
    {
        cartaMissilSelecionadoId = id;
        cartaUnidadeSelecionadaId = string.Empty;
        cartaContatoSelecionadoId = string.Empty;
        if (cartaTopograficaView != null)
        {
            QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.EncontrarMissil(id);
            if (missil != null) PreencherCoordenadasCarta(missil.posicao);
        }
        AtualizarPainel();
    }

    private void AoSelecionarContatoCarta(string id)
    {
        cartaContatoSelecionadoId = id;
        cartaUnidadeSelecionadaId = string.Empty;
        cartaMissilSelecionadoId = string.Empty;
        quartel.SelecionarAlvoLancamento(id);
        GerenciadorQuartel.ContatoMilitarQuartelV2 contato = EncontrarContatoCarta(id);
        if (contato != null) PreencherCoordenadasCarta(contato.posicao);
        AtualizarPainel();
    }

    private void PreencherCoordenadasCarta(Vector3 posicao)
    {
        string x = posicao.x.ToString("0.###", CultureInfo.InvariantCulture);
        string y = posicao.y.ToString("0.###", CultureInfo.InvariantCulture);
        string z = posicao.z.ToString("0.###", CultureInfo.InvariantCulture);
        coordenadaXFallback = x;
        coordenadaYFallback = y;
        coordenadaZFallback = z;

        // Selecionar uma aeronave, contato, míssil ou ponto no mapa é uma ação
        // explícita do operador. Ela deve sempre copiar a posição real para os
        // três campos, mesmo que o foco ainda esteja em um TextField.
        if (campoCoordenadaX != null) campoCoordenadaX.value = x;
        if (campoCoordenadaY != null) campoCoordenadaY.value = y;
        if (campoCoordenadaZ != null) campoCoordenadaZ.value = z;
    }

    private void AoIniciarArrastoCarta(PointerDownEvent evt)
    {
        if (camadaInteracaoCarta == null || cartaTerrenoRenderer == null || evt == null) return;
        cartaToolkitArrastando = true;
        cartaToolkitFoiArrastada = false;
        cartaToolkitPointerId = evt.pointerId;
        cartaToolkitUltimoPonto = evt.position;
        camadaInteracaoCarta.CapturePointer(cartaToolkitPointerId);
        evt.StopPropagation();
    }

    private void AoMoverArrastoCarta(PointerMoveEvent evt)
    {
        if (!cartaToolkitArrastando || evt == null || evt.pointerId != cartaToolkitPointerId
            || cartaTerrenoRenderer == null || camadaInteracaoCarta == null) return;

        Vector2 delta = new Vector2(evt.position.x, evt.position.y) - cartaToolkitUltimoPonto;
        if (delta.sqrMagnitude < 0.25f) return;
        cartaToolkitUltimoPonto = evt.position;
        if (delta.sqrMagnitude >= 9f) cartaToolkitFoiArrastada = true;

        float largura = Mathf.Max(1f, camadaInteracaoCarta.layout.width);
        float altura = Mathf.Max(1f, camadaInteracaoCarta.layout.height);
        cartaTerrenoRenderer.DeslocarMapa(new Vector2(delta.x / largura, -delta.y / altura));
        cartaTerrenoRenderer.MarcarRenderNecessario();
        evt.StopPropagation();
    }

    private void AoFinalizarArrastoCarta(PointerUpEvent evt)
    {
        if (evt == null || evt.pointerId != cartaToolkitPointerId) return;
        bool foiArrasto = cartaToolkitFoiArrastada;
        Vector2 posicao = evt.position;
        bool ctrl = evt.ctrlKey;
        bool shift = evt.shiftKey;
        if (camadaInteracaoCarta != null && camadaInteracaoCarta.HasPointerCapture(cartaToolkitPointerId))
            camadaInteracaoCarta.ReleasePointer(cartaToolkitPointerId);
        cartaToolkitArrastando = false;
        cartaToolkitFoiArrastada = false;
        cartaToolkitPointerId = -1;

        if (!foiArrasto && camadaInteracaoCarta != null)
        {
            Vector2 posicaoLocal = camadaInteracaoCarta.WorldToLocal(posicao);
            float largura = Mathf.Max(1f, camadaInteracaoCarta.layout.width);
            float altura = Mathf.Max(1f, camadaInteracaoCarta.layout.height);
            Vector2 viewport = new Vector2(
                Mathf.Clamp01(posicaoLocal.x / largura),
                Mathf.Clamp01(1f - posicaoLocal.y / altura));
            ProcessarCliqueCartaViewport(viewport, ctrl, shift);
        }
        evt.StopPropagation();
    }

    private void AoPerderCapturaCarta(PointerCaptureOutEvent evt)
    {
        cartaToolkitArrastando = false;
        cartaToolkitFoiArrastada = false;
        cartaToolkitPointerId = -1;
    }

    private void AoUsarRodaNaCarta(WheelEvent evt)
    {
        if (evt == null || cartaTerrenoRenderer == null) return;
        float delta = evt.delta.y;
        if (Mathf.Abs(delta) < 0.01f) return;
        cartaTerrenoRenderer.AjustarZoom(delta > 0f ? -1f : 1f);
        AtualizarPainel();
        evt.StopPropagation();
    }

    private bool ProcessarCliqueCartaViewport(Vector2 viewport, bool ctrl, bool shift)
    {
        if (quartel == null || cartaTerrenoRenderer == null) return false;
        Ray ray;
        if (!cartaTerrenoRenderer.TryViewportPointToRay(viewport, out ray)) return false;

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            // Alguns trechos da demo1 não têm Collider no terreno. O modo
            // manual ainda precisa aceitar o clique: usa o plano operacional
            // como fallback somente quando o botão CLICAR TERRENO está armado.
            if (cliqueTerrenoArmado)
            {
                Plane planoOperacional = new Plane(Vector3.up, Vector3.zero);
                float distanciaPlano;
                if (planoOperacional.Raycast(ray, out distanciaPlano))
                {
                    Vector3 pontoPlano = ray.GetPoint(distanciaPlano);
                    cliqueTerrenoArmado = false;
                    quartel.DefinirPontoAlvoManual(pontoPlano, "CLIQUE NO TERRENO");
                    PreencherCoordenadasCarta(pontoPlano);
                    AtualizarPainel();
                    return true;
                }
            }
            return false;
        }

        IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(hit.collider);
        if (identidade != null && identidade.gameObject.activeInHierarchy)
        {
            string idUnidade = ObterIdUnidadeCarta(identidade.gameObject);
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamentoCarta(idUnidade);
            if (unidade != null && identidade.teamID == quartel.teamID)
            {
                SelecionarUnidadeCarta(idUnidade, ctrl, shift);
                return true;
            }

            if (identidade.teamID != quartel.teamID)
            {
                GerenciadorQuartel.AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoCarta(identidade);
                if (alvo != null)
                {
                    AoSelecionarContatoCarta(alvo.id);
                    // Clique em alvo real também alimenta os campos XYZ. O
                    // usuário pode confirmar manualmente sem redigitar a
                    // posição transmitida pelo E-3.
                    PreencherCoordenadasCarta(hit.point.sqrMagnitude > 0.001f ? hit.point : alvo.posicao);
                    cliqueTerrenoArmado = false;
                    return true;
                }
            }
        }

        MissileThreatTracker ameaca = hit.collider.GetComponentInParent<MissileThreatTracker>();
        if (ameaca != null)
        {
            AoSelecionarMissilCarta("missil-" + ameaca.MissileId);
            PreencherCoordenadasCarta(ameaca.RaizMissil != null ? ameaca.RaizMissil.position : ameaca.PontoAlvoConhecido);
            return true;
        }

        if (cliqueTerrenoArmado)
        {
            cliqueTerrenoArmado = false;
            quartel.DefinirPontoAlvoManual(hit.point, "CLIQUE NO TERRENO");
            PreencherCoordenadasCarta(hit.point);
            AtualizarPainel();
            return true;
        }

        return false;
    }

    private GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 EncontrarUnidadeLancamentoCarta(string id)
    {
        if (quartel == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < quartel.UnidadesLancamento.Count; i++)
        {
            GerenciadorQuartel.UnidadeLancamentoCoordenadoV2 unidade = quartel.UnidadesLancamento[i];
            if (unidade != null && string.Equals(unidade.id, id, StringComparison.Ordinal)) return unidade;
        }
        return null;
    }

    private GerenciadorQuartel.AlvoLancamentoCoordenadoV2 EncontrarAlvoCarta(IdentidadeUnidade identidade)
    {
        if (quartel == null || identidade == null) return null;
        for (int i = 0; i < quartel.AlvosLancamento.Count; i++)
        {
            GerenciadorQuartel.AlvoLancamentoCoordenadoV2 alvo = quartel.AlvosLancamento[i];
            if (alvo == null || !alvo.inimigo) continue;
            if (alvo.transformAlvo == identidade.transform || alvo.id == ObterIdUnidadeCarta(identidade.gameObject)) return alvo;
        }
        return null;
    }

    private static string ObterIdUnidadeCarta(GameObject objeto)
    {
        SaveableEntity saveable = objeto != null ? objeto.GetComponent<SaveableEntity>() : null;
        if (saveable == null && objeto != null) saveable = objeto.GetComponentInParent<SaveableEntity>();
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)) return saveable.UniqueId;
        return objeto == null ? string.Empty : "runtime-" + objeto.GetInstanceID();
    }

    private Button CriarMarcadorCartaToolkit(VisualElement mapa, string tooltip, float x, float y, Color cor, float tamanho)
    {
        Button marcador = new Button { text = string.Empty, tooltip = tooltip };
        marcador.style.position = Position.Absolute;
        marcador.style.left = new Length(x, LengthUnit.Percent);
        marcador.style.top = new Length(y, LengthUnit.Percent);
        marcador.style.width = tamanho;
        marcador.style.height = tamanho;
        marcador.style.marginLeft = -tamanho * 0.5f;
        marcador.style.marginTop = -tamanho * 0.5f;
        marcador.style.backgroundColor = cor;
        marcador.style.borderTopLeftRadius = tamanho;
        marcador.style.borderTopRightRadius = tamanho;
        marcador.style.borderBottomLeftRadius = tamanho;
        marcador.style.borderBottomRightRadius = tamanho;
        marcador.style.paddingLeft = 0;
        marcador.style.paddingRight = 0;
        mapa.Add(marcador);
        return marcador;
    }

    private void DesenharCarta()
    {
        if (carta == null || quartel == null) return;
        carta.Clear();

        carta.style.minHeight = 980;
        carta.style.overflow = Overflow.Visible;

        float raio = ObterRaioCarta();
        Texture terreno = ObterTexturaTerrenoCarta(raio, ObterAspectoCarta());
        if (terreno != null)
        {
            Image imagemTerreno = new Image
            {
                image = terreno,
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore
            };
            imagemTerreno.style.position = Position.Absolute;
            imagemTerreno.style.left = 0;
            imagemTerreno.style.right = 0;
            imagemTerreno.style.top = 0;
            imagemTerreno.style.bottom = 0;
            imagemTerreno.style.opacity = 0.92f;
            carta.Add(imagemTerreno);
        }

        for (int i = 1; i < 10; i++)
        {
            VisualElement vertical = new VisualElement();
            vertical.style.position = Position.Absolute;
            vertical.style.left = new Length(i * 10f, LengthUnit.Percent);
            vertical.style.top = 0;
            vertical.style.bottom = 0;
            vertical.style.width = 1;
            vertical.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            carta.Add(vertical);

            VisualElement horizontal = new VisualElement();
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = new Length(i * 10f, LengthUnit.Percent);
            horizontal.style.left = 0;
            horizontal.style.right = 0;
            horizontal.style.height = 1;
            horizontal.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            carta.Add(horizontal);
        }

        Label norte = Texto("N", 16, new Color(0.96f, 0.82f, 0.35f), FontStyle.Bold);
        norte.style.position = Position.Absolute;
        norte.style.left = new Length(50f, LengthUnit.Percent);
        norte.style.top = 7;
        carta.Add(norte);

        VisualElement centro = Marcador("QG", 50f, 50f, new Color(0.95f, 0.80f, 0.25f), 18);
        carta.Add(centro);
        Label nomeQG = Texto("QG  " + quartel.name, 11, Color.white, FontStyle.Bold);
        nomeQG.style.position = Position.Absolute;
        nomeQG.style.left = new Length(50f, LengthUnit.Percent);
        nomeQG.style.top = new Length(50f, LengthUnit.Percent);
        nomeQG.style.marginLeft = 12;
        nomeQG.style.marginTop = -10;
        carta.Add(nomeQG);

        RegistroEntidadesJogo.FillUnidades(unidadesRegistradas);
        if (unidadesRegistradas.Count == 0)
        {
            IdentidadeUnidade[] encontrados = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !unidadesRegistradas.Contains(encontrados[i]))
                    unidadesRegistradas.Add(encontrados[i]);
            }
        }

        int total = 0;
        for (int i = 0; i < unidadesRegistradas.Count; i++)
        {
            IdentidadeUnidade identidade = unidadesRegistradas[i];
            if (identidade == null || identidade.teamID != quartel.teamID || identidade.transform == transform) continue;
            Vector3 local = transform.InverseTransformPoint(identidade.transform.position);
            if (Mathf.Abs(local.x) > raio || Mathf.Abs(local.z) > raio) continue;

            float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
            float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
            BoeingE3Reconhecimento.ContatoReconhecimento contato;
            bool contatoInimigo = BoeingE3Reconhecimento.TryObterContato(quartel.teamID, identidade.GetInstanceID(), out contato)
                && contato != null && contato.inimigo;
            Color cor = contatoInimigo
                ? new Color(1f, 0.25f, 0.20f)
                : identidade.tipoUnidade == TipoUnidade.Naval
                ? new Color(0.20f, 0.90f, 0.95f)
                : identidade.tipoUnidade == TipoUnidade.Aereo
                    ? new Color(0.35f, 0.62f, 1f)
                    : new Color(0.35f, 0.90f, 0.48f);
            string estado = ObterEstadoCarta(identidade);
            carta.Add(Marcador(identidade.name + " | " + estado, x, y, cor, 10));
            total++;
            if (total >= 80) break;
        }

        // Contatos inimigos recebidos pelo E-3 também fazem parte da carta,
        // mesmo que a unidade ainda não esteja dentro do registro administrativo.
        for (int i = 0; i < unidadesRegistradas.Count && total < 100; i++)
        {
            IdentidadeUnidade identidade = unidadesRegistradas[i];
            if (identidade == null || identidade.teamID == quartel.teamID || identidade.teamID <= 0) continue;
            BoeingE3Reconhecimento.ContatoReconhecimento contato;
            if (!BoeingE3Reconhecimento.TryObterContato(quartel.teamID, identidade.GetInstanceID(), out contato) || contato == null) continue;

            Vector3 local = transform.InverseTransformPoint(contato.ultimaPosicaoConhecida);
            if (Mathf.Abs(local.x) > raio || Mathf.Abs(local.z) > raio) continue;
            float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
            float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
            carta.Add(Marcador("CONTATO: " + contato.nomeAlvo + " | " + contato.tipo, x, y, new Color(1f, 0.25f, 0.20f), 10));
            total++;
        }

        Label escala = Texto("RAIO OPERACIONAL  " + raio.ToString("0") + " m  |  CONTATOS: " + total, 11, new Color(0.65f, 0.82f, 0.82f), FontStyle.Normal);
        escala.style.position = Position.Absolute;
        escala.style.left = 10;
        escala.style.bottom = 8;
        carta.Add(escala);
    }

    private float ObterAspectoCarta()
    {
        float largura = carta != null ? carta.resolvedStyle.width : 0f;
        float altura = carta != null ? carta.resolvedStyle.height : 0f;
        if (float.IsNaN(largura) || largura < 1f || float.IsNaN(altura) || altura < 1f)
        {
            return 1.9f;
        }

        return Mathf.Clamp(largura / altura, 1f, 4f);
    }

    private Texture ObterTexturaTerrenoCarta(float raio, float aspecto)
    {
        if (quartel == null)
        {
            return null;
        }

        GarantirCartaTerrenoRenderer();

        return cartaTerrenoRenderer.Renderizar(transform.position, raio, aspecto, cartaVista3D);
    }

    private float ObterRaioCarta()
    {
        float raio = Mathf.Max(1f, quartel != null ? quartel.raioDeCobertura : 2000f);
        RegistroEntidadesJogo.FillAvioes(avioesRegistrados);
        if (avioesRegistrados.Count == 0)
        {
            ControleAviao[] encontrados = FindObjectsByType<ControleAviao>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !avioesRegistrados.Contains(encontrados[i]))
                    avioesRegistrados.Add(encontrados[i]);
            }
        }

        for (int i = 0; i < avioesRegistrados.Count; i++)
        {
            ControleAviao aviao = avioesRegistrados[i];
            if (aviao == null) continue;
            IdentidadeUnidade identidade = aviao.GetComponent<IdentidadeUnidade>();
            if (identidade == null || identidade.teamID != quartel.teamID) continue;
            BoeingE3Reconhecimento e3 = aviao.GetComponent<BoeingE3Reconhecimento>();
            if (e3 != null) raio = Mathf.Max(raio, e3.alcanceReconhecimento);
        }
        return raio;
    }

    private VisualElement Marcador(string texto, float xPercent, float yPercent, Color cor, int tamanho)
    {
        VisualElement marcador = new VisualElement();
        marcador.style.position = Position.Absolute;
        marcador.style.left = new Length(xPercent, LengthUnit.Percent);
        marcador.style.top = new Length(yPercent, LengthUnit.Percent);
        marcador.style.width = tamanho;
        marcador.style.height = tamanho;
        marcador.style.marginLeft = -tamanho * 0.5f;
        marcador.style.marginTop = -tamanho * 0.5f;
        marcador.style.backgroundColor = cor;
        marcador.style.borderTopLeftRadius = tamanho;
        marcador.style.borderTopRightRadius = tamanho;
        marcador.style.borderBottomLeftRadius = tamanho;
        marcador.style.borderBottomRightRadius = tamanho;
        marcador.tooltip = texto;
        return marcador;
    }

    private void ConstruirAbaArsenal()
    {
        AdicionarCabecalho("ARSENAL", "Estoque existente e compras encaminhadas ao sistema de recursos atual.");
        VisualElement estoque = Linha();
        estoque.Add(Cartao("MISSEIS", quartel.misseisArmazenados.ToString("N0"), "unidades armazenadas"));
        estoque.Add(Cartao("MUNICAO", quartel.municaoArmazenada.ToString("N0"), "pacotes armazenados"));
        estoque.Add(Cartao("FUNDO", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d", "saldo real do GerenciadorRecursos"));
        conteudo.Add(estoque);

        VisualElement compras = Card("REABASTECER ARSENAL");
        long saldo = GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : -1L;
        compras.Add(BotaoAcao("ENCOMENDAR 10 MISSEIS  (-$" + quartel.precoMissil.ToString("N0") + ")", () => quartel.TentarEncomendarMisseis(), false, saldo >= quartel.precoMissil, "Saldo do GerenciadorRecursos insuficiente"));
        compras.Add(BotaoAcao("ENCOMENDAR 100 PACOTES  (-$" + quartel.precoMunicao.ToString("N0") + ")", () => quartel.TentarEncomendarMunicao(), false, saldo >= quartel.precoMunicao, "Saldo do GerenciadorRecursos insuficiente"));
        compras.Add(Texto("Os valores e o debito continuam sob responsabilidade de GerenciadorRecursos; o painel nao duplica a contabilidade.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(compras);

        VisualElement logistica = Card("LOGISTICA DE COMBUSTIVEL");
        Toggle abastecimento = new Toggle("Abastecimento automatico de Tracks")
        {
            value = CaminhaoCombustivel.AbastecimentoAutomaticoGlobal
        };
        abastecimento.RegisterValueChangedCallback(e => CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = e.newValue);
        logistica.Add(abastecimento);
        conteudo.Add(logistica);
    }

    private void AdicionarCabecalho(string nome, string descricao)
    {
        Label tituloAba = Texto(nome, 20, CorCianoQuartel, FontStyle.Bold);
        tituloAba.style.flexShrink = 0;
        tituloAba.style.marginTop = 6;
        Label descricaoAba = Texto(descricao, 13, CorTextoSecundarioQuartel, FontStyle.Normal);
        descricaoAba.style.flexShrink = 0;
        conteudo.Add(tituloAba);
        conteudo.Add(descricaoAba);
        VisualElement separador = new VisualElement();
        separador.style.flexShrink = 0;
        separador.style.height = 1;
        separador.style.marginTop = 8;
        separador.style.marginBottom = 16;
        separador.style.backgroundColor = CorBordaQuartel;
        conteudo.Add(separador);
    }

    private VisualElement Card(string nome)
    {
        VisualElement card = new VisualElement();
        card.style.flexShrink = 0;
        card.style.marginBottom = 10;
        card.style.paddingLeft = 12;
        card.style.paddingRight = 12;
        card.style.paddingTop = 10;
        card.style.paddingBottom = 10;
        card.style.backgroundColor = CorCartaoQuartel;
        card.style.borderTopWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderTopColor = CorBordaQuartel;
        card.style.borderBottomColor = CorBordaQuartel;
        card.style.borderLeftColor = CorBordaQuartel;
        card.style.borderRightColor = CorBordaQuartel;
        if (!string.IsNullOrWhiteSpace(nome))
        {
            card.Add(Texto(nome, 13, CorCianoQuartel, FontStyle.Bold));
        }
        return card;
    }

    private VisualElement Cartao(string nome, string valor, string detalhe)
    {
        VisualElement cartao = Card(string.Empty);
        cartao.style.flexGrow = 1;
        cartao.style.minWidth = 150;
        cartao.style.marginRight = 8;
        cartao.Add(Texto(nome, 11, CorTextoSecundarioQuartel, FontStyle.Bold));
        cartao.Add(Texto(valor, 22, CorTextoQuartel, FontStyle.Bold));
        cartao.Add(Texto(detalhe, 11, CorTextoSecundarioQuartel, FontStyle.Normal));
        return cartao;
    }

    private VisualElement LinhaUnidade(string tipo, string nome, string estado)
    {
        VisualElement linha = Linha();
        linha.style.marginTop = 3;
        linha.style.marginBottom = 3;
        linha.style.paddingTop = 6;
        linha.style.paddingBottom = 6;
        linha.style.paddingLeft = 8;
        linha.style.paddingRight = 8;
        linha.style.backgroundColor = CorNavegacaoQuartel;
        linha.Add(Texto(tipo, 11, CorCianoQuartel, FontStyle.Bold));
        Label nomeLabel = Texto(nome, 13, Color.white, FontStyle.Normal);
        nomeLabel.style.flexGrow = 1;
        nomeLabel.style.marginLeft = 12;
        linha.Add(nomeLabel);
        linha.Add(Texto(estado, 11, CorAlertaQuartel, FontStyle.Bold));
        return linha;
    }

    private VisualElement LinhaInformacao(string nome, string valor)
    {
        VisualElement linha = Linha();
        linha.style.marginTop = 3;
        linha.style.marginBottom = 3;
        Label chave = Texto(nome, 12, CorTextoSecundarioQuartel, FontStyle.Normal);
        chave.style.flexGrow = 1;
        linha.Add(chave);
        linha.Add(Texto(valor, 13, Color.white, FontStyle.Bold));
        return linha;
    }

    private Button BotaoAcao(string label, Action acao, bool perigo = false)
    {
        return BotaoAcao(label, acao, perigo, true, string.Empty);
    }

    private Button BotaoAcao(string label, Action acao, bool perigo, bool habilitado, string motivo)
    {
        Button botao = Botao(label, 0, 38, perigo ? new Color(0.34f, 0.12f, 0.10f) : CorBotaoQuartel);
        botao.style.flexGrow = 1;
        botao.style.marginTop = 4;
        botao.style.marginBottom = 4;
        botao.SetEnabled(habilitado);
        if (!habilitado && !string.IsNullOrWhiteSpace(motivo)) botao.tooltip = motivo;
        if (habilitado) RegistrarAcaoBotao(botao, () => acao?.Invoke());
        return botao;
    }

    private VisualElement Linha()
    {
        VisualElement linha = new VisualElement();
        linha.style.flexDirection = FlexDirection.Row;
        linha.style.alignItems = Align.Center;
        linha.style.flexWrap = Wrap.Wrap;
        return linha;
    }

    private Label Texto(string texto, int tamanho, Color cor, FontStyle estilo)
    {
        Label label = new Label(texto ?? string.Empty);
        label.style.fontSize = tamanho;
        label.style.color = cor;
        label.style.unityFontStyleAndWeight = estilo;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private Button Botao(string texto, float largura, float altura, Color cor)
    {
        Button botao = new Button { text = texto };
        if (largura > 0) botao.style.width = largura;
        botao.style.height = altura;
        botao.style.minHeight = Mathf.Max(altura, 34f);
        botao.style.flexShrink = 0;
        botao.style.alignItems = Align.Center;
        botao.style.justifyContent = Justify.Center;
        botao.style.overflow = Overflow.Hidden;
        botao.style.marginTop = 3;
        botao.style.marginBottom = 3;
        botao.style.paddingLeft = 10;
        botao.style.paddingRight = 10;
        botao.style.paddingTop = 6;
        botao.style.paddingBottom = 6;
        botao.style.backgroundColor = cor;
        botao.style.color = CorTextoQuartel;
        botao.style.unityFontStyleAndWeight = FontStyle.Bold;
        botao.style.fontSize = 12;
        botao.style.unityTextAlign = TextAnchor.MiddleCenter;
        botao.style.whiteSpace = WhiteSpace.Normal;
        botao.style.borderTopWidth = 1;
        botao.style.borderBottomWidth = 1;
        botao.style.borderLeftWidth = 1;
        botao.style.borderRightWidth = 1;
        botao.style.borderTopColor = CorBordaQuartel;
        botao.style.borderBottomColor = CorBordaQuartel;
        botao.style.borderLeftColor = CorBordaQuartel;
        botao.style.borderRightColor = CorBordaQuartel;
        botao.style.borderTopLeftRadius = 4;
        botao.style.borderTopRightRadius = 4;
        botao.style.borderBottomLeftRadius = 4;
        botao.style.borderBottomRightRadius = 4;
        Label textoInterno = botao.Q<Label>(className: "unity-button__text");
        if (textoInterno != null)
        {
            textoInterno.style.color = CorTextoQuartel;
            textoInterno.style.flexGrow = 1;
            textoInterno.style.whiteSpace = WhiteSpace.Normal;
            textoInterno.style.overflow = Overflow.Hidden;
            textoInterno.style.unityTextAlign = TextAnchor.MiddleCenter;
            textoInterno.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        return botao;
    }

    private QuartelForcaSnapshotV2 ObterResumoForca(QuartelForcaV2 forca)
    {
        if (snapshot == null || snapshot.forcas == null) return null;
        for (int i = 0; i < snapshot.forcas.Length; i++)
        {
            QuartelForcaSnapshotV2 resumo = snapshot.forcas[i];
            if (resumo != null && resumo.forca == forca) return resumo;
        }
        return null;
    }

    private void AdicionarResumoForcas(VisualElement destino)
    {
        if (destino == null) return;
        for (int i = 0; i < 4; i++)
        {
            QuartelForcaSnapshotV2 resumo = ObterResumoForca((QuartelForcaV2)i);
            if (resumo == null) continue;
            destino.Add(LinhaInformacao(
                NomeForca(resumo.forca),
                "unidades " + resumo.unidades
                + " | missao " + resumo.unidadesEmMissao
                + " | dano " + resumo.unidadesDanificadas
                + " | pessoal " + resumo.pessoalAlocado + "/" + resumo.pessoalExigido));
        }
    }

    private string ObterEstadoCarta(IdentidadeUnidade identidade)
    {
        if (identidade == null) return "sem identidade";
        ControleUnidade controle = identidade.GetComponent<ControleUnidade>();
        if (controle == null) return "sem controlador";
        if (controle.BloqueioAdministrativoQuartelAtivo) return "inoperante: tripulacao";
        if (controle.PossuiOrdemMovimentoAtiva || controle.OrdemAtual == OrdemControleUnidade.Patrulhando) return "em atividade";
        SistemaDeDanos danos = controle.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.vidaAtual + 0.01f < danos.vidaMaxima) return "danificada";
        return "disponivel";
    }

    private string NomeForca(QuartelForcaV2 forca)
    {
        switch (forca)
        {
            case QuartelForcaV2.Veiculos: return "VEICULOS";
            case QuartelForcaV2.Naval: return "NAVAL";
            case QuartelForcaV2.Aerea: return "AEREA";
            default: return "INFANTARIA";
        }
    }

    private int ContarSelecionadosJogador()
    {
        RegistroEntidadesJogo.FillControlesUnidade(controlesRegistrados);
        int total = 0;
        for (int i = 0; i < controlesRegistrados.Count; i++)
        {
            ControleUnidade controle = controlesRegistrados[i];
            IdentidadeUnidade identidade = controle != null ? controle.GetComponent<IdentidadeUnidade>() : null;
            if (controle != null && controle.selecionado && identidade != null && identidade.teamID == quartel.teamID) total++;
        }
        return total;
    }

    private DadosPaisGoverno ObterPaisJogador()
    {
        return SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(quartel != null ? quartel.teamID : SistemaGovernoMundial.Instancia.teamJogador)
            : null;
    }

    /// <summary>
    /// Elemento de desenho leve para as linhas individuais do lançamento
    /// coordenado. As coordenadas são normalizadas, então a carta acompanha
    /// redimensionamentos do Game View sem criar objetos no mundo.
    /// </summary>
    private sealed class LinhaCartaLancamentoToolkit : VisualElement
    {
        private Vector2 inicio;
        private Vector2 fim;
        private readonly Color cor;
        private readonly float espessura;

        public LinhaCartaLancamentoToolkit(Vector2 inicioNormalizado, Vector2 fimNormalizado, Color corLinha, float espessuraLinha)
        {
            inicio = inicioNormalizado;
            fim = fimNormalizado;
            cor = corLinha;
            espessura = Mathf.Max(1f, espessuraLinha);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            generateVisualContent += Desenhar;
        }

        public void Atualizar(Vector2 novoInicio, Vector2 novoFim)
        {
            inicio = novoInicio;
            fim = novoFim;
            MarkDirtyRepaint();
        }

        private void Desenhar(MeshGenerationContext contexto)
        {
            Vector2 tamanho = contentRect.size;
            Painter2D pincel = contexto.painter2D;
            pincel.strokeColor = cor;
            pincel.lineWidth = espessura;
            pincel.BeginPath();
            pincel.MoveTo(new Vector2(inicio.x * tamanho.x, inicio.y * tamanho.y));
            pincel.LineTo(new Vector2(fim.x * tamanho.x, fim.y * tamanho.y));
            pincel.Stroke();
        }
    }
}
