using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Hegemonia.RTS;
using Hegemonia.AI.BrainMaster;

/// <summary>
/// Menu Comando Tático — controlador principal do UI Toolkit.
/// Abre/fecha com a tecla 1. Enquanto aberto, bloqueia Z/X/V/N/M
/// e impede cliques de seleção/ordem no mundo via InteractionModeService.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MenuComandoController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------
    public static MenuComandoController Instancia { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------
    [Header("Câmera FLIR")]
    [SerializeField] private int flirRenderWidth  = 512;
    [SerializeField] private int flirRenderHeight = 360;

    [Header("Mapa Tático")]
    [Tooltip("Metade do tamanho do mundo em unidades (ex: 5000 = mundo de -5000 a +5000)")]
    [SerializeField] private float mundoMetade = 5000f;
    [Tooltip("Descobre automaticamente os limites dos Terrains ativos da cena para o mapa tático.")]
    [SerializeField] private bool detectarLimitesReaisDoMapa = true;
    [Tooltip("Margem adicionada ao redor dos Terrains para não cortar unidades na borda.")]
    [SerializeField] private float margemMapa = 250f;


    // -----------------------------------------------------------------------
    // Estado interno
    // -----------------------------------------------------------------------
    private UIDocument uiDoc;
    private VisualElement root;
    private VisualElement menuComandoRoot;
    private bool menuAberto;
    // Abertura de menus deve depender de uma entrada explícita do jogador.
    private int bloquearAberturaAteFrame = -1;
    private bool bloquearAberturaAteEntradaDaCompraSerLiberada;
    public bool MenuAberto => menuAberto;
    public bool BarraContextualDisponivel => !menuAberto && unidadesSelecionadasMenu != null && unidadesSelecionadasMenu.Count > 0;

    // Zoom e Pan no mapa tático
    private float mapaZoom = 1.0f;
    private Vector2 mapaCentro = Vector2.zero;
    private Vector2 centroMapaDetectado = Vector2.zero;
    private bool limitesMapaInicializados;
    private bool arrastandoMapa = false;
    private Vector2 ultimaPosicaoMouseDrag;

    // Elementos do mapa
    private VisualElement mapaUnidadesLayer;
    private VisualElement mapaLinhasLayer;
    private VisualElement painelMapa;
    private Label mapaTitulo;
    private VisualElement mapaSelecaoBarra;
    private Label selecaoResumo;
    private VisualElement radarSweep;
    private VisualElement mapaCameraMarker;
    private float radarAngulo;

    // Elementos FLIR
    private VisualElement flirImagem;
    private Label flirAlerta;
    private Label flirTc;
    private Label flirUnidadeNome;
    private Label flirTl;
    private Label flirTr;
    private Button btnDroneCam;
    private RenderTexture flirRT;
    private Slider flirZoomSlider;
    private VisualElement painelSeguir;
    private ScrollView seguirScroll;
    private VisualElement seguirLista;
    private Label seguirStatus;
    private Button btnSeguir100;
    private Button btnSeguir200;
    private Button btnSeguir2000;
    private Button btnSeguir5000;
    private Button btnFecharSeguir;
    private Button btnFecharMenu;
    private Button btnDesselecionarTudo;
    private readonly List<GameObject> alvosSeguirUI = new List<GameObject>(64);
    private GameObject alvoSeguimentoSelecionado;
    private Button itemSeguimentoDestacado;
    private float distanciaSeguimentoAtual = 200f;
    private readonly float[] distanciasSeguimento = { 100f, 200f, 2000f, 5000f };

    // Telemetria
    private Label unidadeNome;
    private Label unidadeEmoji;
    private Label statTipo;
    private Label statStatus;
    private Label statPos;
    private Label statArmas;
    private Label statTeam;
    private Label hpValor;
    private Label fuelValor;
    private VisualElement hpBar;
    private VisualElement fuelBar;

    // SITREP
    private Label sitrepAliados;
    private Label sitrepInimigos;
    private Label sitrepVel;
    private Label sitrepFuel;
    private Label sitrepAmeaca;
    private Label sitrepSel;
    private Label sitrepTempo;
    private Label headerTempo;

    private Vector3 ultimaPosicaoTelemetria;
    private float ultimoTempoTelemetria = -1f;
    private int ultimaUnidadeTelemetriaId;
    private readonly Dictionary<int, Vector3> origemMisseis = new Dictionary<int, Vector3>(64);
    private readonly List<GameObject> misseisEmVoo = new List<GameObject>(64);
    private readonly List<int> idsMisseisAtivos = new List<int>(64);

    // Log
    private VisualElement logContainer;
    private ScrollView logScroll;

    // Ordens
    private Label ordemFeedback;
    private readonly List<Button> botoesOrdem = new List<Button>(8);
    private Button botaoOrdemSelecionado;
    private Button btnPatrulhar;
    private Button btnRadarUnidade;
    private VisualElement barraComandoContextual;
    private Label contextoTipo;
    private Label contextoUnidade;
    private Label contextoDetalhes;
    private Label contextoFeedback;
    private Button btnContextoMover;
    private Button btnContextoPatrulhar;
    private Button btnContextoAtacar;
    private Button btnContextoSeguir;
    private Button btnContextoRadar;
    private Button btnContextoAtivo;
    private Button btnContextoPassivo;
    private Button btnContextoBase;
    private Button btnContextoCentro;
    private Button btnHudFechar;
    private Button btnHudReabrir;
    private VisualElement hudDockReabrir;
    private VisualElement hudCardOverlay;
    private VisualElement hudCardPanel;
    private ScrollView hudCardScroll;
    private Label hudCardExpandedTitle;
    private Label hudCardExpandedDescription;
    private VisualElement hudCardExpanded;
    private VisualElement hudCardOriginalParent;
    private HudCardInfo hudCardExpandedInfo;
    private int hudCardOriginalIndex = -1;
    private bool hudExpandida = true;
    private bool hudVaziaAtualizada;
    private int hudPreviewUnidadeId;
    private int hudLarguraResolucao = -1;
    private int hudAlturaResolucao = -1;
    private float proximaAtualizacaoContextual;
    private bool modoLancamentoMissilMapaAtivo = false;
    private bool modoMoverMapaAtivo = false;
    private int indiceArrasteFormacao = -1;
    private Vector2 posicaoInicioArrasteFormacao;
    private bool arrasteSlotFormacaoReconhecido;
    private bool ignorarClickSlotFormacao;
    private bool modoEdicaoFormacaoHud;
    private IVisualElementScheduledItem alertaCriticaHudAgendamento;
    private bool alertaCriticaHudAtivo;
    private bool alertaCriticaHudFase;
    private string perfilPreviewHudAtual;
    private bool hudPreviewImagemConstruida;

    private sealed class HudCardInfo
    {
        public string TitleKey;
        public string TitleFallback;
        public string DescriptionKey;
        public string DescriptionFallback;
    }

    // Mapa — cache de VisualElements por instância
    private sealed class MapaItemUI
    {
        public VisualElement Root;
        public Label Label;
        public VisualElement Marcador;
        public VisualElement Ring;
        public VisualElement HpFill;
        public ControleUnidade Controle;
        public SistemaDeDanos Dano;
        public IdentidadeUnidade Identidade;
        public bool UltimoDestruido;
    }

    private readonly Dictionary<int, MapaItemUI> mapaElementos = new Dictionary<int, MapaItemUI>();
    private readonly HashSet<int> mapaVivos = new HashSet<int>();
    private readonly List<int> mapaRemovidos = new List<int>(64);
    private readonly List<VisualElement> linhasOrdemPool = new List<VisualElement>(64);
    private int linhasOrdemAtivas;

    private readonly List<IdentidadeUnidade> cacheUnidadesMapa = new List<IdentidadeUnidade>(256);
    private readonly List<IdentidadeUnidade> cacheUnidadesCenaFallback = new List<IdentidadeUnidade>(256);
    private readonly List<ControleUnidade> cacheControlesPersistencia = new List<ControleUnidade>(256);
    private readonly List<IdentidadeIA> cacheIdentidadesIA = new List<IdentidadeIA>(64);
    private readonly HashSet<int> unidadesSelecionadasIds = new HashSet<int>();
    private float proximoRefreshCachesEntidades;
    private float proximaBuscaUnidadesCenaFallback;
    private bool cachesEntidadesSujo = true;
    private MiniMapa miniMapaCache;

    // Unidade selecionada DENTRO DO MENU
    private ControleUnidade unidadeSelecionadaMenu; // Unidade focada (telemetria e FLIR)
    private readonly List<ControleUnidade> unidadesSelecionadasMenu = new List<ControleUnidade>(); // Lista de todas as selecionadas no menu
    private const string PlayerPrefsMenuFocusKey = "hegemonia.menu.comando.foco";
    private const string PlayerPrefsMenuSelectionKey = "hegemonia.menu.comando.selecionadas";

    // Referências a sistemas do jogo
    private GerenteSelecao gerenteSelecao;
    private DesenharLinhasOrdem desenhadorOrdens;

    // O jogador pode controlar outro time em campanhas carregadas.  O menu
    // tatico precisa consultar a mesma fonte usada pelo governo, em vez de
    // tratar permanentemente o time 1 como aliado.
    private int TimeJogadorAtual => SistemaGovernoMundial.Instancia != null
        ? Mathf.Max(1, SistemaGovernoMundial.Instancia.teamJogador)
        : 1;

    private bool EhUnidadeDoJogador(IdentidadeUnidade identidade)
    {
        return identidade != null && identidade.teamID == TimeJogadorAtual;
    }

    private static bool UnidadePodeUsarRadar(ControleUnidade unidade)
    {
        if (unidade == null) return false;
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        if (identidade == null) return false;
        if (identidade.tipoUnidade != TipoUnidade.Estrutura) return true;

        IA_ConstructionMetadata metadata = unidade.GetComponent<IA_ConstructionMetadata>();
        return (metadata != null && metadata.IsRadar)
            || unidade.GetComponent<ControleAviao>() != null
            || unidade.GetComponent<ControleAviaoCaca>() != null
            || unidade.GetComponent<Helicoptero>() != null
            || unidade.GetComponent<VooHelicoptero>() != null
            || unidade.GetComponent<ControleNavioRealista>() != null
            || unidade.GetComponent<ControleSubmarino>() != null
            || unidade.GetComponent<IdentidadeNaval>() != null
            || unidade.GetComponent<C700TransporteAereo>() != null;
    }

    // O satélite deve catalogar toda unidade do jogador, inclusive aeronaves
    // comerciais controladas por ele.
    private static bool EhAviaoComercialNoSatelite(GameObject obj)
    {
        return false;
    }

    // Unidades antigas da cena e algumas unidades criadas por produtores
    // externos podem ter IdentidadeUnidade, mas ainda nao ter o adaptador de
    // ordens. O satelite nao deve deixa-las impossiveis de comandar.
    private static ControleUnidade ObterControleTatico(IdentidadeUnidade identidade, bool prepararSeMovel = false)
    {
        if (identidade == null)
        {
            return null;
        }

        ControleUnidade controle = identidade.GetComponent<ControleUnidade>();
        if (controle == null)
        {
            ControleUnidade[] controlesPais = identidade.GetComponentsInParent<ControleUnidade>(true);
            for (int i = 0; i < controlesPais.Length && controle == null; i++)
            {
                if (controlesPais[i] != null
                    && controlesPais[i].GetComponentInParent<IdentidadeUnidade>() == identidade)
                    controle = controlesPais[i];
            }
        }
        if (controle == null)
        {
            ControleUnidade[] controlesFilhos = identidade.GetComponentsInChildren<ControleUnidade>(true);
            for (int i = 0; i < controlesFilhos.Length && controle == null; i++)
            {
                if (controlesFilhos[i] != null
                    && controlesFilhos[i].GetComponentInParent<IdentidadeUnidade>() == identidade)
                    controle = controlesFilhos[i];
            }
        }
        if (controle == null && prepararSeMovel
            && (identidade.tipoUnidade != TipoUnidade.Estrutura
                || identidade.GetComponent<SiloLancadorEstrategico>() != null))
        {
            controle = identidade.gameObject.AddComponent<ControleUnidade>();
        }

        return controle;
    }

    private static bool UnidadeAptaParaPatrulha(ControleUnidade unidade)
    {
        if (unidade == null) return false;

        // Aeronaves continuam usando o executor aéreo existente. Para navios,
        // a elegibilidade é explícita: cargueiros/petroleiros/logística não
        // podem aparecer para o jogador como unidades de patrulha militar.
        if (!unidade.EhUnidadeNaval()
            && !NavalPlacementResolver.IsLogisticsVessel(unidade.gameObject))
        {
            return true;
        }

        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
            ?? unidade.GetComponentInParent<IdentidadeUnidade>()
            ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
        return NavalPlacementResolver.IsNavalPatrolCapable(
            identidade,
            unidade,
            out _);
    }

    // Timer
    private float tempoOperacao;
    private float blink;
    private float tickMapa;
    private float tickLog;

    // Log interno do menu
    private readonly List<(string tempo, string fonte, string msg, string tipo)> logs =
        new List<(string, string, string, string)>();

    // Teclas que o menu bloqueia
    private static readonly KeyCode[] TeclasBloqueadas =
    {
        KeyCode.Z, KeyCode.X, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M,
        KeyCode.C  // construção também
    };

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private bool bindUIFeito;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            // O objeto duplicado pode ainda possuir um UIDocument ativo. Se
            // apenas o controlador for destruído, esse documento continua
            // desenhando uma segunda camada do Menu Satélite e fica por cima
            // do Quartel ou captura os cliques do mundo.
            UIDocument documentoDuplicado = GetComponent<UIDocument>();
            if (documentoDuplicado != null)
            {
                VisualElement raizDuplicada = documentoDuplicado.rootVisualElement;
                if (raizDuplicada != null)
                {
                    raizDuplicada.style.display = DisplayStyle.None;
                    raizDuplicada.pickingMode = PickingMode.Ignore;
                }
                documentoDuplicado.enabled = false;
            }

            Destroy(this);
            return;
        }
        Instancia = this;
        RegistroEntidadesJogo.EntidadesAlteradas += MarcarCachesEntidadesSujo;

        ResolverDocumento();
    }

    /// <summary>
    /// Garante que uiDoc, panelSettings, visualTreeAsset e root estão resolvidos.
    /// NÃO toca no display — isso é responsabilidade de AbrirMenu/FecharMenu/Start.
    /// </summary>
    private void ResolverDocumento()
    {
        if (uiDoc == null)
        {
            uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                uiDoc = gameObject.AddComponent<UIDocument>();
                bindUIFeito = false;
            }
        }

        if (uiDoc != null)
        {
            if (uiDoc.panelSettings == null)
            {
                uiDoc.panelSettings = Resources.Load<PanelSettings>("PanelSettings");
            }

            if (uiDoc.visualTreeAsset == null)
            {
                uiDoc.visualTreeAsset = Resources.Load<VisualTreeAsset>("MenuComando/MenuComando");
                bindUIFeito = false;
            }

            if (root == null)
            {
                root = uiDoc.rootVisualElement;
                if (root != null) bindUIFeito = false;
            }
        }

        if (root != null && menuComandoRoot == null)
        {
            menuComandoRoot = root.Q<VisualElement>("menu-comando-root");
        }
    }

    private void Start()
    {
        ResolverDocumento();
        menuAberto = false;
        hudExpandida = true;

        AtualizarLimitesMapa();

        if (root != null)
        {
            // O host permanece ativo para a barra contextual. Apenas o menu
            // tático de tela cheia começa oculto.
            root.style.display = DisplayStyle.Flex;
            root.pickingMode = PickingMode.Ignore;
            if (menuComandoRoot != null)
                menuComandoRoot.style.display = DisplayStyle.None;

            if (!bindUIFeito)
            {
                BindUI();
                bindUIFeito = true;
            }
            LocalizationManager.IdiomaAlterado -= AoAlterarIdiomaHudTatico;
            LocalizationManager.IdiomaAlterado += AoAlterarIdiomaHudTatico;
            AplicarTextosHudTatico();
            hudVaziaAtualizada = false;
            AtualizarBarraComandoContextual();
            AtualizarAlturaHudResponsiva();
            AtualizarVisibilidadeHudTatico();
        }
        CriarRenderTextureFLIR();
        AdicionarLog("SISTEMA", "Menu Comando inicializado. Tecla [1] para abrir/fechar.", "sistema");
    }

    /// <summary>
    /// Mantém o mapa tático sincronizado com a extensão real do terreno. O
    /// valor do Inspector continua sendo usado como mínimo/fallback, então
    /// cenas antigas sem Terrain não mudam de comportamento.
    /// </summary>
    private void AtualizarLimitesMapa()
    {
        float metadeConfigurada = Mathf.Max(1f, mundoMetade);
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        bool encontrouTerrain = false;

        Terrain[] terrenos = detectarLimitesReaisDoMapa
            ? FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : System.Array.Empty<Terrain>();

        if (detectarLimitesReaisDoMapa)
        {
            for (int i = 0; i < terrenos.Length; i++)
            {
                Terrain terreno = terrenos[i];
                TerrainData dados = terreno != null ? terreno.terrainData : null;
                if (terreno == null || dados == null || !terreno.gameObject.scene.IsValid())
                {
                    continue;
                }

                // Ignora tiles desativados ou com escala zero que pertencem a
                // versões antigas/apoio da cena, sem limitar o mapa jogável.
                Vector3 escala = terreno.transform.lossyScale;
                if (!terreno.gameObject.activeInHierarchy || Mathf.Abs(escala.x) < 0.001f || Mathf.Abs(escala.z) < 0.001f)
                {
                    continue;
                }

                Vector3 origem = terreno.GetPosition();
                Vector3 tamanho = dados.size;
                if (tamanho.x <= 0f || tamanho.z <= 0f)
                {
                    continue;
                }

                minX = Mathf.Min(minX, origem.x);
                maxX = Mathf.Max(maxX, origem.x + tamanho.x);
                minZ = Mathf.Min(minZ, origem.z);
                maxZ = Mathf.Max(maxZ, origem.z + tamanho.z);
                encontrouTerrain = true;
            }

            // O mapa jogável também possui layouts fora do Terrain principal
            // (a cidade/layout da IA01 é o caso atual). Inclui somente raízes
            // de mapa conhecidas e pontos ativos, mantendo tiles antigos
            // desativados ou com escala zero fora do cálculo.
            Transform[] todosTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < todosTransforms.Length; i++)
            {
                Transform layout = todosTransforms[i];
                if (layout == null || !layout.gameObject.activeInHierarchy) continue;

                string nomeLayout = layout.name.ToLowerInvariant();
                bool layoutDeMapa = nomeLayout.Contains("ia01citylayout")
                    || nomeLayout.Contains("cartelmanualcreates")
                    || nomeLayout == "terrenos";
                if (!layoutDeMapa) continue;

                Transform[] pontosLayout = layout.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < pontosLayout.Length; j++)
                {
                    Transform ponto = pontosLayout[j];
                    if (ponto == null || !ponto.gameObject.activeInHierarchy) continue;
                    Vector3 escala = ponto.lossyScale;
                    if (Mathf.Abs(escala.x) < 0.001f || Mathf.Abs(escala.z) < 0.001f) continue;

                    Vector3 posicao = ponto.position;
                    minX = Mathf.Min(minX, posicao.x);
                    maxX = Mathf.Max(maxX, posicao.x);
                    minZ = Mathf.Min(minZ, posicao.z);
                    maxZ = Mathf.Max(maxZ, posicao.z);
                    encontrouTerrain = true;
                }
            }
        }

        if (!encontrouTerrain)
        {
            centroMapaDetectado = Vector2.zero;
            mapaCentro = centroMapaDetectado;
            mundoMetade = metadeConfigurada;
            limitesMapaInicializados = true;
            Debug.LogWarning($"[MenuComando] Nenhum Terrain ativo encontrado; usando limite configurado de {mundoMetade:F0}.");
            return;
        }

        float margem = Mathf.Max(0f, margemMapa);
        minX -= margem;
        maxX += margem;
        minZ -= margem;
        maxZ += margem;

        centroMapaDetectado = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        float metadeTerrain = Mathf.Max((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);

        // Nunca reduz a área configurada anteriormente; apenas amplia quando
        // o mapa/terreno da cena for maior.
        mundoMetade = Mathf.Max(metadeConfigurada, metadeTerrain);
        mapaCentro = centroMapaDetectado;
        limitesMapaInicializados = true;

        Debug.Log($"[MenuComando] Limites do mapa tático: centro=({centroMapaDetectado.x:F0}, {centroMapaDetectado.y:F0}) metade={mundoMetade:F0}.");
    }

    private void LimitarCentroMapa(float rangeX, float rangeZ)
    {
        if (!limitesMapaInicializados)
        {
            AtualizarLimitesMapa();
        }

        float limitePanX = Mathf.Max(0f, mundoMetade - rangeX * 0.5f);
        float limitePanZ = Mathf.Max(0f, mundoMetade - rangeZ * 0.5f);
        mapaCentro.x = Mathf.Clamp(mapaCentro.x, centroMapaDetectado.x - limitePanX, centroMapaDetectado.x + limitePanX);
        mapaCentro.y = Mathf.Clamp(mapaCentro.y, centroMapaDetectado.y - limitePanZ, centroMapaDetectado.y + limitePanZ);
    }

    private void Update()
    {
        ResolverDocumento();

        if (Screen.width != hudLarguraResolucao || Screen.height != hudAlturaResolucao)
            AtualizarAlturaHudResponsiva();

        // Faz o bind do UI na primeira frame em que o root estiver disponível
        if (root != null && !bindUIFeito)
        {
            // O host fica ativo para a barra; o painel tático continua fechado.
            root.style.display = DisplayStyle.Flex;
            root.pickingMode = PickingMode.Ignore;
            if (menuComandoRoot != null)
                menuComandoRoot.style.display = DisplayStyle.None;
            BindUI();
            bindUIFeito = true;
            AtualizarAlturaHudResponsiva();
            AtualizarVisibilidadeHudTatico();
        }

        if (root == null || uiDoc == null)
        {
            return;
        }

        ProcessarAtalhosHud();

        if (!menuAberto && Time.unscaledTime >= proximaAtualizacaoContextual)
        {
            proximaAtualizacaoContextual = Time.unscaledTime + 0.2f;
            SincronizarSelecaoComJogo();
            NormalizarFocoSelecao();
            AtualizarBarraComandoContextual();
        }

        if (bloquearAberturaAteEntradaDaCompraSerLiberada
            && !Construtor.EmModoConstrucaoAtivo
            && !Input.GetMouseButton(0)
            && !Input.GetKey(KeyCode.C)
            && !Input.GetKey(KeyCode.Alpha1)
            && !Input.GetKey(KeyCode.Keypad1))
        {
            bloquearAberturaAteEntradaDaCompraSerLiberada = false;
        }

        // Resolve o atalho do Menu Satelite antes dos bloqueios de outros
        // modais. Um estado legado de Quartel/Governo pode permanecer marcado
        // por um frame depois do fechamento e, se o teste vier antes daqui,
        // o operador perde o atalho e parece que o menu travou.
        bool solicitouMenuComando = RTSInputBindings.GetKeyDown(RTSInputAction.CommandMenu)
            || Input.GetKeyDown(KeyCode.Alpha1)
            || Input.GetKeyDown(KeyCode.Keypad1);
        if (solicitouMenuComando)
        {
            // A confirmação da compra de uma estrutura ainda pode entregar
            // o mesmo frame de entrada ao HUD. O satélite não deve abrir no
            // meio do modo de construção nem capturar o jogo por acidente.
            if (Construtor.EmModoConstrucaoAtivo
                || Time.frameCount <= bloquearAberturaAteFrame
                || bloquearAberturaAteEntradaDaCompraSerLiberada)
            {
                return;
            }

            // EntradaGlobalBloqueada também cobre a janela usada para
            // consumir cliques durante o fechamento do Quartel. Depois que a
            // entrada da compra for liberada, somente um modal realmente
            // aberto deve impedir o Satélite.
            if (GerenciadorQuartel.InterfaceAberta || MenuGoverno.EstaAberto)
            {
                return;
            }

            if (menuAberto) FecharMenu();
            else AbrirMenu();
            return;
        }

        // O Quartel tem prioridade sobre a lista de teclas bloqueadas do
        // Satélite. Algumas cenas de tutorial/demo não carregam um
        // GerenciadorQuartel no YAML; nesse caso o atalho cria somente o
        // controlador administrativo da cena, sem criar unidades ou dados
        // fictícios. Nas cenas que já possuem o gerenciador, o próprio
        // GerenciadorQuartel.Update() continua sendo a autoridade do toggle.
        bool solicitouQuartel = RTSInputBindings.GetKeyDown(RTSInputAction.Barracks)
            || Input.GetKeyDown(KeyCode.B);
        if (solicitouQuartel)
        {
            GerenciadorQuartel quartel = FindFirstObjectByType<GerenciadorQuartel>();
            if (quartel == null)
            {
                // A demo1 mantém o Quartel real desativado enquanto o
                // tutorial aguarda a construção. Ele ainda é o controlador
                // correto para o painel; não crie uma segunda instância
                // quando o prefab já está presente na cena.
                GerenciadorQuartel[] quarteisNaCena = FindObjectsByType<GerenciadorQuartel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < quarteisNaCena.Length; i++)
                {
                    if (quarteisNaCena[i] != null)
                    {
                        quartel = quarteisNaCena[i];
                        break;
                    }
                }
            }

            // O Satélite pode continuar desenhado por um frame quando a
            // abertura vem do mesmo atalho. Fechá-lo antes de ativar o
            // Quartel evita duas interfaces sobrepostas e não deixa o
            // bloqueio modal do Quartel parecer um travamento da tecla 1.
            if (menuAberto)
                FecharMenu();

            if (quartel == null)
            {
                GameObject objetoQuartel = new GameObject("Quartel - Controlador da Cena");
                objetoQuartel.transform.position = Vector3.zero;
                quartel = objetoQuartel.AddComponent<GerenciadorQuartel>();
                quartel.teamID = 1;
                quartel.usarPainelQuartelUIToolkit = true;
                quartel.habilitarLancamentoCoordenado = true;
                quartel.abrirPainelAoIniciarNoPlayMode = false;
                quartel.AlternarInterface();
                quartel.MarcarAtalhoBConsumidoNesteFrame();
            }
            else if (!quartel.gameObject.activeSelf)
            {
                quartel.gameObject.SetActive(true);
                quartel.usarPainelQuartelUIToolkit = true;
                quartel.habilitarLancamentoCoordenado = true;
                quartel.abrirPainelAoIniciarNoPlayMode = false;
                quartel.AlternarInterface();
                quartel.MarcarAtalhoBConsumidoNesteFrame();
            }

            // O gerenciador já existente receberá o mesmo B no próprio
            // Update. Apenas retornamos antes da lista de bloqueios para que
            // o Satélite não consuma a tecla.
            return;
        }

        if (QuartelMenuUIController.EntradaGlobalBloqueada || MenuGoverno.EstaAberto)
        {
            return;
        }

        if (menuAberto && Input.GetKeyDown(KeyCode.Escape))
        {
            FecharMenu();
            return;
        }

        if (!menuAberto) return;

        // V continua alternando a câmera de um drone selecionado. Em outros
        // casos, encaminha o atalho para o Pier em vez de consumi-lo abaixo
        // junto com as demais teclas bloqueadas pelo Menu Comando.
        if (RTSInputBindings.GetKeyDown(RTSInputAction.Pier))
        {
            if (unidadeSelecionadaMenu != null
                && unidadeSelecionadaMenu.GetComponent<KamikazeDrone>() != null)
            {
                AlternarModoCameraDrone();
                return;
            }

            PierMarinha pierPreferido = unidadeSelecionadaMenu != null
                ? unidadeSelecionadaMenu.GetComponent<PierMarinha>()
                    ?? unidadeSelecionadaMenu.GetComponentInParent<PierMarinha>()
                    ?? unidadeSelecionadaMenu.GetComponentInChildren<PierMarinha>(true)
                : null;

            FecharMenu();
            MenuPier.AlternarPorAtalho(pierPreferido);
            return;
        }

        // Atalho: tecla A seleciona todas as unidades aliadas no mapa
        if (Input.GetKeyDown(KeyCode.A))
        {
            SelecionarTodasUnidadesAliadas();
        }

        // A tecla I executa o mesmo comando do botao ESTADO (ALTERNAR).
        if (Input.GetKeyDown(KeyCode.I))
        {
            bool ehTransporteTerrestre = unidadeSelecionadaMenu != null
                && (unidadeSelecionadaMenu.GetComponent<TransporteTerrestre>() != null
                    || unidadeSelecionadaMenu.GetComponentInParent<TransporteTerrestre>() != null
                    || unidadeSelecionadaMenu.GetComponentInChildren<TransporteTerrestre>(true) != null);
            bool ehUnidadeNaval = unidadeSelecionadaMenu != null && unidadeSelecionadaMenu.EhUnidadeNaval();

            if (!ehTransporteTerrestre && !ehUnidadeNaval)
            {
                ExecutarOrdem("ESTADO_ALTERNAR");
            }
            return;
        }

        // Confirmação rápida do alvo de seguir pela mira ou pela lista lateral
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (desenhadorOrdens == null)
                desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

            bool painelSeguirAberto = painelSeguir != null && painelSeguir.style.display.value != DisplayStyle.None;
            if ((desenhadorOrdens != null && desenhadorOrdens.modoSeguirAtivo) || painelSeguirAberto)
            {
                if (ConfirmarSeguimentoAtivo())
                {
                    return;
                }
            }
        }

        // Confirmação de Patrulha via ENTER
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (desenhadorOrdens == null)
                desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

            if (desenhadorOrdens != null && desenhadorOrdens.modoPatrulhaAtivo)
            {
                desenhadorOrdens.ConfirmarPatrulhaDoMenu();
                SetText(ordemFeedback, "✔ Patrulha confirmada.");
                AdicionarLog("OPS", "Patrulha confirmada via teclado.", "normal");
            }
        }

        // Bloqueia todas as teclas de outros menus enquanto o Menu Comando está aberto
        foreach (var k in TeclasBloqueadas)
        {
            if (Input.GetKeyDown(k))
            {
                if (k == KeyCode.V && unidadeSelecionadaMenu != null && unidadeSelecionadaMenu.GetComponent<KamikazeDrone>() != null)
                {
                    continue;
                }
                // Consome o input sem fazer nada
                AdicionarLog("SISTEMA", $"Tecla [{k}] bloqueada — Menu Comando ativo.", "sistema");
            }
        }

        // Atualiza timers
        tempoOperacao += Time.deltaTime;
        blink         += Time.deltaTime;
        tickMapa      += Time.deltaTime;
        tickLog       += Time.deltaTime;
        AtualizarBotaoRadarVisual();

        // Animação do radar (rotação simulada por C#)
        radarAngulo = (radarAngulo + Time.deltaTime * 90f) % 360f;
        if (radarSweep != null)
            radarSweep.style.rotate = new StyleRotate(new Rotate(radarAngulo));

        // Blink do status
        if (blink > 0.8f)
        {
            blink = 0;
            var statusLabel = root.Q<Label>("header-status");
            if (statusLabel != null)
                statusLabel.style.opacity = statusLabel.resolvedStyle.opacity > 0.5f ? 0.2f : 1f;
        }

        // Atualiza relogio e telemetria a cada frame
        AtualizarRelogio();

        // Atualiza mapa a cada 0.1s para não sobrecarregar
        float intervaloMapa = DiagnosticoDesempenhoJogo.RuntimeSaturado()
            ? 0.18f
            : DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                ? 0.12f
                : 0.1f;

        if (tickMapa >= intervaloMapa)
        {
            tickMapa = 0;
            AtualizarMapaTatico();
            AtualizarSitrep();
            AtualizarTelemetriaUnidade();
        }
    }

    private void OnDestroy()
    {
        FecharCardHudExpandido();
        RegistroEntidadesJogo.EntidadesAlteradas -= MarcarCachesEntidadesSujo;
        LocalizationManager.IdiomaAlterado -= AoAlterarIdiomaHudTatico;
        if (Instancia == this) Instancia = null;
        LiberarBloqueioInput();

        if (flirRT != null)
        {
            if (CameraUnidadeHUD.Instanciada && CameraUnidadeHUD.Instancia != null)
                CameraUnidadeHUD.Instancia.DesativarDoMenu();

            flirRT.Release();
            Destroy(flirRT);
        }
    }

    // -----------------------------------------------------------------------
    // Abrir / Fechar
    // -----------------------------------------------------------------------
    public void AbrirMenu()
    {
        FecharCardHudExpandido();
        if (Construtor.EmModoConstrucaoAtivo
            || Time.frameCount <= bloquearAberturaAteFrame
            || bloquearAberturaAteEntradaDaCompraSerLiberada)
        {
            return;
        }

        if (GerenciadorQuartel.InterfaceAberta || MenuGoverno.EstaAberto)
        {
            return;
        }

        if (menuAberto) return;
        menuAberto = true;

        ResolverDocumento();

        if (root != null)
        {
            root.style.display = DisplayStyle.Flex;
        }
        if (menuComandoRoot != null)
        {
            menuComandoRoot.style.display = DisplayStyle.Flex;
        }
        if (barraComandoContextual != null)
        {
            AtualizarVisibilidadeHudTatico();
        }

        // Registra bloqueio de input global
        InteractionModeService.Request(
            this,
            InteractionOwner.MenuComando,
            new InteractionPolicy
            {
                bloqueiaSelecao     = true,
                bloqueiaOrdemMundo  = true,
                bloqueiaRotacaoCamera = false,
                consomeLMB          = true,
                consomeRMB          = true
            },
            "Menu Comando aberto");

        // Ativa câmera FLIR
        if (CameraUnidadeHUD.Instancia != null && flirRT != null)
            CameraUnidadeHUD.Instancia.AtivarNoMenu(flirRT);

        // Desativa o mini-mapa da HUD
        if (miniMapaCache == null)
            miniMapaCache = FindFirstObjectByType<MiniMapa>();

        var miniMapa = miniMapaCache;
        if (miniMapa != null)
        {
            miniMapa.gameObject.SetActive(false);
        }

        bool restaurouPersistencia = RestaurarSelecaoPersistida();
        if (!restaurouPersistencia)
        {
            SincronizarSelecaoComJogo();
        }

        if (unidadeSelecionadaMenu == null && unidadesSelecionadasMenu.Count > 0)
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1];
        }

        AtualizarCacheSelecaoIds();
        cachesEntidadesSujo = true;

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu, true);

        AdicionarLog("COMANDO", "Menu Tático aberto. Sincronizada seleção.", "sistema");
    }

    /// <summary>
    /// Fecha o satélite e impede apenas a reabertura espúria no frame em que
    /// uma construção é confirmada. Os atalhos normais voltam no frame seguinte.
    /// </summary>
    public void BloquearAberturaAposConstrucao()
    {
        if (menuAberto)
        {
            FecharMenu();
        }

        bloquearAberturaAteFrame = Mathf.Max(bloquearAberturaAteFrame, Time.frameCount + 1);
        // A confirmacao pode deixar o mouse ou a tecla de construcao ainda
        // pressionados. Mantenha o Satelite fechado ate a entrada ser solta;
        // assim a compra do Quartel nao reabre a interface por acidente.
        bloquearAberturaAteEntradaDaCompraSerLiberada = true;
    }

    private void SincronizarSelecaoComJogo()
    {
        unidadesSelecionadasMenu.Clear();

        if (gerenteSelecao == null)
            gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();

        if (gerenteSelecao == null || gerenteSelecao.unidadesSelecionadas == null)
        {
            return;
        }

        foreach (var cu in gerenteSelecao.unidadesSelecionadas)
        {
            if (cu != null
                && !EhAviaoComercialNoSatelite(cu.gameObject)
                && !unidadesSelecionadasMenu.Contains(cu))
            {
                unidadesSelecionadasMenu.Add(cu);
            }
        }

        AtualizarCacheSelecaoIds();
    }

    private bool RestaurarSelecaoPersistida()
    {
        string idsSerializados = PlayerPrefs.GetString(PlayerPrefsMenuSelectionKey, string.Empty);
        string focoSerializado = PlayerPrefs.GetString(PlayerPrefsMenuFocusKey, string.Empty);

        if (string.IsNullOrWhiteSpace(idsSerializados) && string.IsNullOrWhiteSpace(focoSerializado))
        {
            return false;
        }

        unidadesSelecionadasMenu.Clear();

        string[] ids = idsSerializados.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < ids.Length; i++)
        {
            ControleUnidade cu = EncontrarUnidadePorIdPersistente(ids[i]);
            if (cu != null
                && !EhAviaoComercialNoSatelite(cu.gameObject)
                && !unidadesSelecionadasMenu.Contains(cu))
            {
                unidadesSelecionadasMenu.Add(cu);
            }
        }

        unidadeSelecionadaMenu = EncontrarUnidadePorIdPersistente(focoSerializado);
        if (unidadeSelecionadaMenu == null && unidadesSelecionadasMenu.Count > 0)
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1];
        }

        AtualizarCacheSelecaoIds();

        return unidadeSelecionadaMenu != null || unidadesSelecionadasMenu.Count > 0;
    }

    private ControleUnidade EncontrarUnidadePorIdPersistente(string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            return null;
        }

        AtualizarCacheEntidadesSeNecessario();

        for (int i = 0; i < cacheControlesPersistencia.Count; i++)
        {
            ControleUnidade cu = cacheControlesPersistencia[i];
            if (cu == null) continue;
            if (EhAviaoComercialNoSatelite(cu.gameObject)) continue;

            SaveableEntity saveable = cu.GetComponent<SaveableEntity>();
            if (saveable != null && saveable.UniqueId == uniqueId)
            {
                return cu;
            }
        }

        return null;
    }

    private string ObterIdPersistente(ControleUnidade cu)
    {
        if (cu == null)
        {
            return string.Empty;
        }

        SaveableEntity saveable = SaveableEntity.Garantir(cu.gameObject);
        return saveable != null ? saveable.UniqueId : string.Empty;
    }

    private void SalvarSelecaoPersistida()
    {
        List<string> ids = new List<string>(unidadesSelecionadasMenu.Count);
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            string id = ObterIdPersistente(unidadesSelecionadasMenu[i]);
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        PlayerPrefs.SetString(PlayerPrefsMenuSelectionKey, ids.Count > 0 ? string.Join(";", ids) : string.Empty);
        PlayerPrefs.SetString(PlayerPrefsMenuFocusKey, ObterIdPersistente(unidadeSelecionadaMenu));
        PlayerPrefs.Save();
    }

    public void FecharMenu()
    {
        if (!menuAberto) return;
        menuAberto = false;
        modoLancamentoMissilMapaAtivo = false;
        modoMoverMapaAtivo = false;

        if (root != null)
        {
            root.style.display = DisplayStyle.Flex;
            root.pickingMode = PickingMode.Ignore;
        }
        if (menuComandoRoot != null)
        {
            menuComandoRoot.style.display = DisplayStyle.None;
        }
        if (barraComandoContextual != null)
        {
            AtualizarVisibilidadeHudTatico();
        }

        LiberarBloqueioInput();

        // Cancela qualquer modo de ordem ativo ao fechar o menu
        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhadorOrdens != null)
        {
            desenhadorOrdens.CancelarModo();
        }

        // Desativa câmera FLIR
        if (CameraUnidadeHUD.Instanciada)
            CameraUnidadeHUD.Instancia.DesativarDoMenu();

        // Reativa o mini-mapa da HUD
        if (miniMapaCache == null)
            miniMapaCache = FindFirstObjectByType<MiniMapa>();

        var miniMapa = miniMapaCache;
        if (miniMapa != null)
        {
            miniMapa.gameObject.SetActive(true);
        }

        SalvarSelecaoPersistida();
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        AtualizarBarraComandoContextual();
    }

    private void LiberarBloqueioInput()
    {
        InteractionModeService.Release(this, InteractionOwner.MenuComando);
    }

    // -----------------------------------------------------------------------
    // Bind UI
    // -----------------------------------------------------------------------
    private void BindUI()
    {
        if (root == null) return;
        menuComandoRoot   = root.Q<VisualElement>("menu-comando-root");
        mapaUnidadesLayer = root.Q<VisualElement>("mapa-unidades-layer");
        mapaLinhasLayer   = root.Q<VisualElement>("mapa-linhas-layer");
        painelMapa        = root.Q<VisualElement>("painel-mapa");
        mapaTitulo        = root.Q<Label>("mapa-titulo");
        mapaSelecaoBarra  = root.Q<VisualElement>("mapa-selecao-barra");
        selecaoResumo     = root.Q<Label>("selecao-resumo");

        if (mapaSelecaoBarra != null)
        {
            mapaSelecaoBarra.style.position = Position.Absolute;
            mapaSelecaoBarra.style.top = 42f;
            mapaSelecaoBarra.style.left = 480f;
            mapaSelecaoBarra.style.width = 570f;
            mapaSelecaoBarra.style.height = 22f;
            mapaSelecaoBarra.style.flexDirection = FlexDirection.Row;
            mapaSelecaoBarra.style.alignItems = Align.Center;
            mapaSelecaoBarra.BringToFront();
        }
        radarSweep        = root.Q<VisualElement>("radar-sweep");

        flirImagem      = root.Q<VisualElement>("flir-imagem");
        flirAlerta      = root.Q<Label>("flir-label-bc");
        flirTc          = root.Q<Label>("flir-label-tc");
        flirUnidadeNome = root.Q<Label>("flir-unidade-nome");
        flirTl          = root.Q<Label>("flir-label-tl");
        flirTr          = root.Q<Label>("flir-label-tr");
        flirZoomSlider  = root.Q<Slider>("flir-zoom-slider");
        if (flirZoomSlider != null)
        {
            flirZoomSlider.RegisterValueChangedCallback(evt =>
            {
                if (CameraUnidadeHUD.Instancia != null)
                {
                    float targetZoom = 0.06f + (evt.newValue / 100f) * (18.0f - 0.06f);
                    if (Mathf.Abs(CameraUnidadeHUD.Instancia.zoomFactor - targetZoom) > 0.01f)
                    {
                        CameraUnidadeHUD.Instancia.zoomFactor = targetZoom;
                    }
                }
            });
            flirZoomSlider.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
            });
        }

        unidadeNome  = root.Q<Label>("unidade-nome");
        unidadeEmoji = root.Q<Label>("unidade-emoji");
        statTipo     = root.Q<Label>("stat-tipo");
        statStatus   = root.Q<Label>("stat-status");
        statPos      = root.Q<Label>("stat-pos");
        statArmas    = root.Q<Label>("stat-armas");
        statTeam     = root.Q<Label>("stat-team");
        hpValor      = root.Q<Label>("hp-valor");
        fuelValor    = root.Q<Label>("fuel-valor");
        hpBar        = root.Q<VisualElement>("hp-bar");
        fuelBar      = root.Q<VisualElement>("fuel-bar");

        sitrepAliados  = root.Q<Label>("sitrep-aliados");
        sitrepInimigos = root.Q<Label>("sitrep-inimigos");
        sitrepVel      = root.Q<Label>("sitrep-vel");
        sitrepFuel     = root.Q<Label>("sitrep-fuel");
        sitrepAmeaca   = root.Q<Label>("sitrep-ameaca");
        sitrepSel      = root.Q<Label>("sitrep-sel");
        sitrepTempo    = root.Q<Label>("sitrep-tempo");
        headerTempo    = root.Q<Label>("header-tempo-op");

        painelSeguir   = root.Q<VisualElement>("painel-seguir");
        seguirScroll   = root.Q<ScrollView>("seguir-scroll");
        seguirLista    = root.Q<VisualElement>("seguir-lista");
        seguirStatus   = root.Q<Label>("seguir-status");
        btnSeguir100   = root.Q<Button>("seguir-dist-100");
        btnSeguir200   = root.Q<Button>("seguir-dist-200");
        btnSeguir2000  = root.Q<Button>("seguir-dist-2000");
        btnSeguir5000  = root.Q<Button>("seguir-dist-5000");
        btnFecharSeguir = root.Q<Button>("btn-fechar-seguir");
        btnFecharMenu = root.Q<Button>("btn-fechar-menu");

        if (btnSeguir100 != null) btnSeguir100.clicked += () => DefinirDistanciaSeguimento(100f);
        if (btnSeguir200 != null) btnSeguir200.clicked += () => DefinirDistanciaSeguimento(200f);
        if (btnSeguir2000 != null) btnSeguir2000.clicked += () => DefinirDistanciaSeguimento(2000f);
        if (btnSeguir5000 != null) btnSeguir5000.clicked += () => DefinirDistanciaSeguimento(5000f);
        if (btnFecharSeguir != null) btnFecharSeguir.clicked += CancelarModoSeguir;
        if (btnFecharMenu != null) btnFecharMenu.clicked += FecharMenu;

        logContainer = root.Q<VisualElement>("log-container");
        logScroll    = root.Q<ScrollView>("log-scroll");

        ordemFeedback = root.Q<Label>("ordem-feedback");
        barraComandoContextual = root.Q<VisualElement>("barra-comando-contextual");
        contextoTipo = root.Q<Label>("contexto-tipo");
        contextoUnidade = root.Q<Label>("contexto-unidade");
        contextoDetalhes = root.Q<Label>("contexto-detalhes");
        contextoFeedback = root.Q<Label>("contexto-feedback");
        btnContextoMover = root.Q<Button>("btn-contexto-mover");
        btnContextoPatrulhar = root.Q<Button>("btn-contexto-patrulhar");
        btnContextoAtacar = root.Q<Button>("btn-contexto-atacar");
        btnContextoSeguir = root.Q<Button>("btn-contexto-seguir");
        btnContextoRadar = root.Q<Button>("btn-contexto-radar");
        btnContextoAtivo = root.Q<Button>("btn-contexto-ativo");
        btnContextoPassivo = root.Q<Button>("btn-contexto-passivo");
        btnContextoBase = root.Q<Button>("btn-contexto-base");
        btnContextoCentro = root.Q<Button>("btn-contexto-centro");
        btnHudFechar = root.Q<Button>("btn-hud-fechar");
        btnHudReabrir = root.Q<Button>("btn-hud-reabrir");
        hudDockReabrir = root.Q<VisualElement>("hud-dock-reabrir");

        if (barraComandoContextual != null)
        {
            barraComandoContextual.pickingMode = PickingMode.Position;
        }
        if (btnHudFechar != null) btnHudFechar.clicked += MinimizarHudTatico;
        if (btnHudReabrir != null) btnHudReabrir.clicked += RestaurarHudTatico;
        AtualizarVisibilidadeHudTatico();

        // Botões de ordem
        var btnAtivo = root.Q<Button>("btn-ativo");
        if (btnAtivo != null) VincularBotaoOrdem(btnAtivo, "ATIVO");

        var btnPassivo = root.Q<Button>("btn-passivo");
        if (btnPassivo != null) VincularBotaoOrdem(btnPassivo, "PASSIVO");

        var btnEstado = root.Q<Button>("btn-estado");
        if (btnEstado != null) VincularBotaoOrdem(btnEstado, "ESTADO_ALTERNAR");

        btnPatrulhar = root.Q<Button>("btn-patrulhar");
        if (btnPatrulhar != null) VincularBotaoOrdem(btnPatrulhar, "PATRULHAR");

        var btnSeguir = root.Q<Button>("btn-seguir");
        if (btnSeguir != null) VincularBotaoOrdem(btnSeguir, "SEGUIR");

        var btnAtacar = root.Q<Button>("btn-atacar");
        if (btnAtacar != null) VincularBotaoOrdem(btnAtacar, "ATACAR");

        var btnVoltarBase = root.Q<Button>("btn-voltar-base");
        if (btnVoltarBase != null) VincularBotaoOrdem(btnVoltarBase, "VOLTAR_BASE");

        var btnTrocaCamera = root.Q<Button>("btn-troca-camera");
        if (btnTrocaCamera != null) VincularBotaoOrdem(btnTrocaCamera, "TROCAR_CAMERA");

        var btnMover = root.Q<Button>("btn-mover");
        if (btnMover != null) VincularBotaoOrdem(btnMover, "MOVER");

        btnRadarUnidade = root.Q<Button>("btn-radar-unidade");
        if (btnRadarUnidade != null)
        {
            btnRadarUnidade.clicked += () => ExecutarOrdem("RADAR_ALTERNAR");
            btnRadarUnidade.pickingMode = PickingMode.Position;
            btnRadarUnidade.tooltip = "Ligar/desligar o radar emissor das unidades selecionadas";
        }

        if (btnContextoMover != null) btnContextoMover.clicked += () => ExecutarOrdemContextual("MOVER");
        if (btnContextoPatrulhar != null) btnContextoPatrulhar.clicked += () => ExecutarOrdemContextual("PATRULHAR");
        if (btnContextoAtacar != null) btnContextoAtacar.clicked += () => ExecutarOrdemContextual("ATACAR");
        if (btnContextoSeguir != null) btnContextoSeguir.clicked += () => ExecutarOrdemContextual("SEGUIR");
        if (btnContextoRadar != null) btnContextoRadar.clicked += () => ExecutarOrdemContextual("RADAR_ALTERNAR");
        if (btnContextoAtivo != null) btnContextoAtivo.clicked += () => ExecutarOrdemContextual("ATIVO");
        if (btnContextoPassivo != null) btnContextoPassivo.clicked += () => ExecutarOrdemContextual("PASSIVO");
        if (btnContextoBase != null) btnContextoBase.clicked += () => ExecutarOrdemContextual("VOLTAR_BASE");
        if (btnContextoCentro != null) btnContextoCentro.clicked += AbrirMenuContextual;
        VincularHudTatico();
        VincularExpansaoCardsHud();

        var btnLancamento = root.Q<Button>("btn-lancar-missil");
        if (btnLancamento != null) VincularBotaoOrdem(btnLancamento, "LANCAR_MISSIL");

        var btnSelTudo = root.Q<Button>("btn-selecionar-tudo");
        if (btnSelTudo != null) btnSelTudo.clicked += () => SelecionarTodasUnidadesAliadas();

        btnDesselecionarTudo = root.Q<Button>("btn-desselecionar-tudo");
        if (btnDesselecionarTudo != null) btnDesselecionarTudo.clicked += DesselecionarUnidadeEmFoco;

        btnDroneCam = root.Q<Button>("btn-drone-cam");
        if (btnDroneCam != null) btnDroneCam.clicked += () => AlternarModoCameraDrone();

        FecharPainelSeguimento();

        // Registro de ouvintes de eventos para Zoom, Pan e Cliques no Mapa
        if (painelMapa != null)
        {
            // Zoom com scroll do mouse
            painelMapa.RegisterCallback<WheelEvent>(evt =>
            {
                float zoomDelta = -evt.delta.y * 0.12f;
                AlterarZoom(zoomDelta, evt.localMousePosition);
                evt.StopPropagation();
            });

            // Arrastar (Pan) com botão do meio (MMB) ou com o cursor
            painelMapa.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 2) // Botão do meio (Scroll click)
                {
                    arrastandoMapa = true;
                    ultimaPosicaoMouseDrag = evt.localPosition;
                    painelMapa.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                else if (evt.button == 0) // Botão esquerdo
                {
                    OnMapClicked(evt.localPosition);
                    evt.StopPropagation();
                }
                else if (evt.button == 1) // Botão direito
                {
                    OnMapRightClicked(evt.localPosition);
                    evt.StopPropagation();
                }
            });

            painelMapa.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (arrastandoMapa)
                {
                    Vector2 delta = (Vector2)evt.localPosition - ultimaPosicaoMouseDrag;
                    ultimaPosicaoMouseDrag = evt.localPosition;

                    float rangeX = (mundoMetade * 2f) / mapaZoom;
                    float rangeZ = (mundoMetade * 2f) / mapaZoom;

                    float W = painelMapa.resolvedStyle.width;
                    float H = painelMapa.resolvedStyle.height;

                    if (W > 0 && H > 0)
                    {
                        float deltaWorldX = -(delta.x / W) * rangeX;
                        float deltaWorldZ = (delta.y / H) * rangeZ;

                        mapaCentro += new Vector2(deltaWorldX, deltaWorldZ);
                        LimitarCentroMapa(rangeX, rangeZ);
                    }
                    evt.StopPropagation();
                }
            });

            painelMapa.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 2 && arrastandoMapa)
                {
                    arrastandoMapa = false;
                    painelMapa.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });
        var painelFlir = root.Q<VisualElement>("painel-flir");
        if (painelFlir != null)
        {
            painelFlir.RegisterCallback<WheelEvent>(evt =>
            {
                if (CameraUnidadeHUD.Instancia != null)
                {
                    CameraUnidadeHUD.Instancia.AddZoom(evt.delta.y * 0.05f);
                    evt.StopPropagation();
                }
            });

            bool draggingFlir = false;
            Vector2 lastPos = Vector2.zero;
            
            painelFlir.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1) // Botão direito
                {
                    draggingFlir = true;
                    lastPos = evt.localPosition;
                    painelFlir.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            painelFlir.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (draggingFlir && CameraUnidadeHUD.Instancia != null)
                {
                    float deltaX = evt.localPosition.x - lastPos.x;
                    float deltaY = evt.localPosition.y - lastPos.y;
                    CameraUnidadeHUD.Instancia.AddRotation(deltaX * 0.5f);
                    CameraUnidadeHUD.Instancia.AddRotationVertical(deltaY * 0.5f);
                    lastPos = evt.localPosition;
                    evt.StopPropagation();
                }
            });

            painelFlir.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 1 && draggingFlir)
                {
                    draggingFlir = false;
                    painelFlir.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });
        }
    }
    }

    // -----------------------------------------------------------------------
    private void NormalizarFocoSelecao()
    {
        unidadesSelecionadasMenu.RemoveAll(unidade => unidade == null);
        if (unidadesSelecionadasMenu.Count == 0)
        {
            unidadeSelecionadaMenu = null;
            return;
        }

        if (unidadeSelecionadaMenu == null || !unidadesSelecionadasMenu.Contains(unidadeSelecionadaMenu))
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1];
        }
    }

    private void AtualizarBarraComandoContextual()
    {
        if (barraComandoContextual == null) return;

        bool outraInterfaceAberta = Construtor.EmModoConstrucaoAtivo
            || GerenciadorQuartel.InterfaceAberta
            || MenuGoverno.EstaAberto;
        AtualizarVisibilidadeHudTatico();
        if (menuAberto || outraInterfaceAberta)
        {
            return;
        }

        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (!hudVaziaAtualizada)
            {
                if (contextoTipo != null) contextoTipo.text = TextoHud("identity.title", "COMANDO TÁTICO");
                if (contextoUnidade != null) contextoUnidade.text = TextoHud("identity.none", "NENHUMA UNIDADE");
                if (contextoDetalhes != null) contextoDetalhes.text = TextoHud("identity.select", "SELECIONE UMA UNIDADE PARA VER O ESTADO");
                if (contextoFeedback != null) contextoFeedback.text = string.Empty;
                ResetarHudTaticoSemSelecao();
            }
            return;
        }

        NormalizarFocoSelecao();
        if (unidadeSelecionadaMenu == null)
        {
            ResetarHudTaticoSemSelecao();
            return;
        }

        hudVaziaAtualizada = false;

        int quantidadeNaval = 0;
        int quantidadeSubmarino = 0;
        int quantidadeAerea = 0;
        int quantidadeTerrestre = 0;
        bool podePatrulhar = false;
        bool podeUsarRadar = false;
        bool temAeronave = false;
        bool radarLigado = false;
        bool radarDesligado = false;

        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null) continue;

            string perfil = ObterPerfilComandoContextual(unidade);
            if (perfil == "SUBMARINO") quantidadeSubmarino++;
            else if (perfil == "NAVAL") quantidadeNaval++;
            else if (perfil == "AÉREA") quantidadeAerea++;
            else quantidadeTerrestre++;

            podePatrulhar |= UnidadeAptaParaPatrulha(unidade);
            temAeronave |= perfil == "AÉREA";

            if (UnidadePodeUsarRadar(unidade))
            {
                IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
                if (identidade != null && identidade.teamID == TimeJogadorAtual)
                {
                    podeUsarRadar = true;
                    RadarUnidadeTatica radar = unidade.GetComponent<RadarUnidadeTatica>();
                    if (radar != null && radar.RadarLigado) radarLigado = true;
                    else radarDesligado = true;
                }
            }
        }

        bool grupo = unidadesSelecionadasMenu.Count > 1;
        string perfilFoco = ObterPerfilComandoContextual(unidadeSelecionadaMenu);
        string perfilTraduzido = TraduzirPerfilHud(perfilFoco);
        if (contextoTipo != null)
        {
            contextoTipo.text = grupo
                ? string.Format(TextoHud("group.title", "GRUPO DE COMANDO · {0} UNIDADES"), unidadesSelecionadasMenu.Count)
                : string.Format(TextoHud("unit.title", "UNIDADE · {0}"), perfilTraduzido);
        }

        if (contextoUnidade != null)
        {
            contextoUnidade.text = grupo
                ? string.Format(TextoHud("group.name", "GRUPO TÁTICO · {0}"), perfilTraduzido)
                : ObterNomeExibicao(unidadeSelecionadaMenu.gameObject);
        }

        if (contextoDetalhes != null)
        {
            contextoDetalhes.text = grupo
                ? string.Format(
                    TextoHud("group.mix", "NAVAL {0} · SUB {1} · AÉREA {2} · TERRA {3}"),
                    quantidadeNaval,
                    quantidadeSubmarino,
                    quantidadeAerea,
                    quantidadeTerrestre)
                : MontarDetalhesComandoContextual(unidadeSelecionadaMenu, perfilFoco);
        }

        if (btnContextoPatrulhar != null)
        {
            btnContextoPatrulhar.SetEnabled(podePatrulhar);
            btnContextoPatrulhar.style.display = podePatrulhar ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (btnContextoRadar != null)
        {
            btnContextoRadar.SetEnabled(podeUsarRadar);
            btnContextoRadar.style.display = podeUsarRadar ? DisplayStyle.Flex : DisplayStyle.None;
            string estadoRadar = radarLigado && radarDesligado
                ? TextoHud("state.mixed", "MISTO")
                : radarLigado ? TextoHud("state.on", "LIGADO") : TextoHud("state.off", "DESLIGADO");
            btnContextoRadar.text = string.Format(
                TextoHud("sensor.row", "{0}  {1}"),
                TextoHud("sensor.radar", "RADAR"),
                estadoRadar);
        }
        if (btnContextoBase != null)
        {
            btnContextoBase.style.display = temAeronave ? DisplayStyle.Flex : DisplayStyle.None;
        }

        AtualizarHudTatico(unidadeSelecionadaMenu, grupo);
    }

    private void AtualizarVisibilidadeHudTatico()
    {
        bool podeExibir = !menuAberto;
        if (barraComandoContextual != null)
            barraComandoContextual.style.display = podeExibir && hudExpandida ? DisplayStyle.Flex : DisplayStyle.None;
        if (hudDockReabrir != null)
            hudDockReabrir.style.display = podeExibir && !hudExpandida ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static string TextoHud(string chave, string fallback)
    {
        return LocalizationManager.T("hud." + chave, fallback);
    }

    private void DefinirTextoHudLocalizado(string nome, string chave, string fallback)
    {
        if (root == null) return;
        VisualElement elemento = root.Q<VisualElement>(nome);
        if (elemento is Label label) label.text = TextoHud(chave, fallback);
        else if (elemento is Button botao) botao.text = TextoHud(chave, fallback);
    }

    private void DefinirTooltipHud(string nome, string chave, string fallback)
    {
        VisualElement elemento = root != null ? root.Q<VisualElement>(nome) : null;
        if (elemento != null) elemento.tooltip = TextoHud(chave, fallback);
    }

    private void AplicarTextosHudTatico()
    {
        if (root == null) return;

        DefinirTextoHudLocalizado("contexto-tipo", "identity.title", "COMANDO TÁTICO");
        DefinirTextoHudLocalizado("contexto-unidade", "identity.none", "NENHUMA UNIDADE");
        DefinirTextoHudLocalizado("hud-status", "status.select", "SELECIONE UMA UNIDADE");
        DefinirTextoHudLocalizado("hud-card-speed", "card.speed", "VELOCIDADE / RUMO");
        DefinirTextoHudLocalizado("hud-speed", "speed.short", "VEL");
        DefinirTextoHudLocalizado("hud-speed-down", "speed.down", "− 10%");
        DefinirTextoHudLocalizado("hud-speed-up", "speed.up", "+ 10%");
        DefinirTextoHudLocalizado("hud-heading", "heading.short", "PROA");
        DefinirTextoHudLocalizado("hud-card-condition", "card.condition", "CONDIÇÃO DA UNIDADE");
        DefinirTextoHudLocalizado("hud-condition-label", "condition.status", "ESTADO");
        DefinirTextoHudLocalizado("hud-condition-detail", "status.no_data", "SEM DADOS");
        DefinirTextoHudLocalizado("hud-context-rtb", "quick.rtb", "BASE ↗");
        DefinirTextoHudLocalizado("hud-card-formation", "card.formation", "FORMAÇÃO");
        DefinirTextoHudLocalizado("hud-formation-label", "formation.selection", "SELEÇÃO TÁTICA");
        DefinirTextoHudLocalizado("hud-formation-hint", "formation.edit", "F3 EDITAR FORMAÇÃO");
        DefinirTextoHudLocalizado("hud-card-roe", "card.roe", "POSTURA / REGRAS DE FOGO");
        DefinirTextoHudLocalizado("hud-roe-hold", "roe.hold", "○  CESSAR FOGO");
        DefinirTextoHudLocalizado("hud-roe-defensive", "roe.defensive", "○  DEFENSIVA");
        DefinirTextoHudLocalizado("hud-roe-tight", "roe.tight", "○  FOGO RESTRITO");
        DefinirTextoHudLocalizado("hud-roe-free", "roe.free", "○  FOGO LIVRE");
        DefinirTextoHudLocalizado("hud-card-sensors", "card.sensors", "SENSORES / EMCON");
        DefinirTextoHudLocalizado("hud-radar", "sensor.radar", "◉ RADAR");
        DefinirTextoHud("hud-sonar", string.Format(TextoHud("sensor.row", "{0}  {1}"), "◎ " + TextoHud("sensor.sonar", "SONAR"), TextoHud("state.na", "N/D")));
        DefinirTextoHud("hud-esm", string.Format(TextoHud("sensor.row", "{0}  {1}"), "▥ " + TextoHud("sensor.esm", "ESM"), TextoHud("state.na", "N/D")));
        DefinirTextoHud("hud-datalink", string.Format(TextoHud("sensor.row", "{0}  {1}"), "⊕ " + TextoHud("sensor.datalink", "LINK DE DADOS"), TextoHud("state.na", "N/D")));
        DefinirTextoHudLocalizado("hud-card-weapons", "card.weapons", "ARMAMENTO");
        DefinirTextoHudLocalizado("hud-card-countermeasures", "card.countermeasures", "CONTRAMEDIDAS");
        DefinirTextoHudLocalizado("hud-counter-state", "counter.not_detected", "SISTEMAS NÃO DETECTADOS");
        DefinirTextoHudLocalizado("hud-card-waypoints", "card.waypoints", "PATRULHA / PONTOS DE ROTA");
        DefinirTextoHudLocalizado("hud-waypoints", "route.undefined", "ROTA NÃO DEFINIDA");
        DefinirTextoHudLocalizado("hud-waypoint-detail", "route.no_order", "SEM DESTINO ORDENADO");
        DefinirTextoHudLocalizado("btn-contexto-patrulhar", "quick.patrol.short", "F4  PATRULHA");
        DefinirTextoHudLocalizado("hud-card-automation", "card.automation", "IA / AUTOMAÇÃO");
        DefinirTextoHudLocalizado("hud-ai-manual", "ai.manual", "○  MANUAL");
        DefinirTextoHudLocalizado("hud-ai-assist", "ai.assist", "○  ASSISTÊNCIA");
        DefinirTextoHudLocalizado("hud-ai-auto", "ai.auto", "○  AUTOMÁTICO");
        DefinirTextoHudLocalizado("hud-ai-status", "ai.na", "MODO N/D");
        DefinirTextoHudLocalizado("btn-contexto-seguir", "quick.follow", "⌘  SEGUIR   F1");
        DefinirTextoHudLocalizado("quick-escort", "quick.escort", "♟  ESCOLTAR   F2");
        DefinirTextoHudLocalizado("quick-formation", "quick.formation", "✥  EDITAR FORMAÇÃO   F3");
        DefinirTextoHudLocalizado("quick-patrol", "quick.patrol", "⌖  PATRULHA   F4");
        DefinirTextoHudLocalizado("quick-intercept", "quick.intercept", "◎  INTERCEPTAR   F5");
        DefinirTextoHudLocalizado("quick-damage", "quick.damage", "⚒  CONTROLE DE DANOS   F6");
        DefinirTextoHudLocalizado("quick-camera", "quick.camera", "▣  CÂMERA   F7");
        DefinirTextoHudLocalizado("btn-hud-fechar", "quick.close", "×");
        DefinirTextoHudLocalizado("btn-hud-reabrir", "quick.reopen", "☰  REABRIR HUD TÁTICO");

        DefinirTextoHudLocalizado("hud-compass-n", "heading.north", "N");
        DefinirTextoHudLocalizado("hud-compass-s", "heading.south", "S");
        DefinirTextoHud("hud-counter-1", string.Format(TextoHud("sensor.row", "{0}  {1}"), TextoHud("counter.chaff", "CHAFF"), TextoHud("state.na", "N/D")));
        DefinirTextoHud("hud-counter-2", string.Format(TextoHud("sensor.row", "{0}  {1}"), TextoHud("counter.noisemaker", "ISCA ACÚSTICA"), TextoHud("state.na", "N/D")));
        Button radarVazio = root.Q<Button>("hud-radar");
        if (radarVazio != null)
        {
            radarVazio.text = string.Format(TextoHud("sensor.row", "{0}  {1}"), "◉ " + TextoHud("sensor.radar", "RADAR"), TextoHud("state.na", "N/D"));
        }

        DefinirTooltipHud("hud-country", "tooltip.flag", "Bandeira do país da unidade");
        DefinirTooltipHud("hud-context-rtb", "tooltip.rtb", "Retornar aeronave à base.");
        DefinirTooltipHud("btn-contexto-patrulhar", "quick.patrol.tooltip", "Criar rota de patrulha.");
        DefinirTooltipHud("btn-contexto-seguir", "quick.follow.tooltip", "Escolher unidade para acompanhar.");
        DefinirTooltipHud("quick-escort", "quick.escort.tooltip", "Escolha uma unidade para escoltar a selecionada.");
        DefinirTooltipHud("quick-formation", "quick.formation.tooltip", "Ativar ou concluir a edição dos slots da formação.");
        DefinirTooltipHud("quick-patrol", "quick.patrol.tooltip", "Criar rota de patrulha.");
        DefinirTooltipHud("quick-intercept", "quick.intercept.tooltip", "Selecionar alvo para interceptar.");
        DefinirTooltipHud("quick-damage", "quick.damage.tooltip", "Exibir o diagnóstico de integridade da unidade em foco.");
        DefinirTooltipHud("quick-camera", "quick.camera.tooltip", "Centralizar câmera na seleção.");
        DefinirTooltipHud("hud-roe-hold", "roe.hold.tooltip", "Desativa o armamento da unidade.");
        DefinirTooltipHud("hud-roe-defensive", "roe.defensive.tooltip", "Mantém a unidade sob comando manual; ela só dispara após uma ordem do jogador.");
        DefinirTooltipHud("hud-roe-tight", "roe.tight.tooltip", "Engaja automaticamente apenas os alvos autorizados.");
        DefinirTooltipHud("hud-roe-free", "roe.free.tooltip", "Engaja automaticamente qualquer alvo válido detectado.");
        DefinirTooltipHud("hud-speed-down", "speed.down.tooltip", "Reduz a velocidade das unidades móveis selecionadas em 10%.");
        DefinirTooltipHud("hud-speed-up", "speed.up.tooltip", "Aumenta a velocidade das unidades móveis selecionadas em 10%.");
        DefinirTooltipHud("hud-ai-manual", "ai.manual.tooltip", "Armamento aguarda uma ordem direta do jogador.");
        DefinirTooltipHud("hud-ai-assist", "ai.assist.tooltip", "Engajamento automático limitado aos alvos autorizados.");
        DefinirTooltipHud("hud-ai-auto", "ai.auto.tooltip", "Engajamento automático de qualquer alvo válido detectado.");
        DefinirTooltipHud("btn-contexto-centro", "quick.center.tooltip", "Abrir centro tático.");
        DefinirTooltipHud("quick-grid", "quick.grid.tooltip", "Aplicar formação em grade às unidades selecionadas.");
        DefinirTooltipHud("quick-camera-options", "quick.camera_options.tooltip", "Alternar câmera de acompanhamento.");
        DefinirTooltipHud("btn-hud-fechar", "quick.minimize.tooltip", "Recolher a barra tática.");
        DefinirTooltipHud("btn-hud-reabrir", "quick.restore.tooltip", "Reabrir a barra tática.");
        DefinirTooltipHud("hud-card-detail-close", "card.close.tooltip", "Fechar o painel ampliado.");

        root.Query<VisualElement>(className: "tactical-card").ForEach(card =>
            card.tooltip = TextoHud("card.expand.tooltip", "Clique para ampliar este painel e ler os detalhes."));

        for (int i = 1; i <= 5; i++)
        {
            Button slot = root.Q<Button>("formation-slot-" + i);
            if (slot != null)
            {
                slot.tooltip = i == 1
                    ? TextoHud("tooltip.formation_leader", "Líder da formação.")
                    : string.Format(TextoHud("tooltip.formation_slot", "Membro {0} — clique para torná-lo líder."), i);
            }
        }
    }

    private void AoAlterarIdiomaHudTatico()
    {
        if (root == null) return;
        AplicarTextosHudTatico();
        AtualizarTextoCardHudExpandido();
        if (contextoFeedback != null) contextoFeedback.text = string.Empty;
        hudVaziaAtualizada = false;
        AtualizarBarraComandoContextual();
    }

    private void MinimizarHudTatico()
    {
        FecharCardHudExpandido();
        hudExpandida = false;
        AtualizarVisibilidadeHudTatico();
    }

    private void RestaurarHudTatico()
    {
        hudExpandida = true;
        AtualizarVisibilidadeHudTatico();
        AtualizarBarraComandoContextual();
    }

    private void AtualizarAlturaHudResponsiva()
    {
        hudLarguraResolucao = Screen.width;
        hudAlturaResolucao = Screen.height;
        if (barraComandoContextual == null) return;

        float alturaDesejada = Mathf.Clamp(Screen.height * 0.11f, 76f, 124f);
        float alturaMaximaDaTela = Screen.height * 0.12f;
        barraComandoContextual.style.height = Mathf.Min(alturaDesejada, alturaMaximaDaTela);
        barraComandoContextual.EnableInClassList(
            "hud-small-resolution",
            Screen.width < 1440 || Screen.height < 800);
    }

    private void ResetarHudTaticoSemSelecao()
    {
        if (root == null) return;
        hudVaziaAtualizada = true;
        AtualizarPreviewHud(null);
        AtualizarBandeiraHud(string.Empty);
        DefinirTextoHud("hud-status", TextoHud("status.select", "SELECIONE UMA UNIDADE"));
        DefinirTextoHud("hud-speed", TextoHud("speed.short", "VEL") + "  —");
        DefinirTextoHud("hud-speed-order", string.Format(TextoHud("speed.order", "GRUPO {0}"), "—"));
        DefinirTextoHud("hud-heading", TextoHud("heading.short", "PROA") + "  —");
        DefinirTextoHud("hud-condition-label", TextoHud("condition.status", "ESTADO"));
        DefinirTextoHud("hud-condition", "—");
        DefinirTextoHud("hud-condition-detail", TextoHud("status.no_data", "SEM DADOS"));
        DefinirTextoHud("hud-integrity", string.Format(TextoHud("integrity.status", "INTEGRIDADE  {0}"), "—"));
        DefinirTextoHud("hud-readiness", string.Format(TextoHud("readiness.status", "PRONTIDÃO  {0}"), "—"));
        DefinirTextoHud("hud-formation-label", TextoHud("formation.none", "SEM FORMAÇÃO"));
        DefinirTextoHud("hud-formation-hint", TextoHud("formation.select", "SELECIONE UNIDADES"));
        DefinirTextoHud("hud-waypoints", "○ ─ ○ ─ ○");
        DefinirTextoHud("hud-waypoint-detail", TextoHud("route.no_order", "SEM DESTINO ORDENADO"));
        DefinirTextoHud("hud-ai-status", TextoHud("ai.na", "MODO N/D"));
        for (int i = 1; i <= 5; i++)
            DefinirTextoHud("hud-weapon-" + i, "—");
        DefinirLarguraHud("hud-integrity-fill", 0f);
        DefinirLarguraHud("hud-readiness-fill", 0f);
        DefinirAlertaCriticoHud(-1f);

        Button radar = root.Q<Button>("hud-radar");
        if (radar != null)
        {
            radar.text = string.Format(
                TextoHud("sensor.row", "{0}  {1}"),
                "◉ " + TextoHud("sensor.radar", "RADAR"),
                TextoHud("state.na", "N/D"));
            radar.SetEnabled(false);
        }
        root.Q<Button>("hud-roe-hold")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-roe-defensive")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-roe-tight")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-roe-free")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-ai-manual")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-ai-assist")?.RemoveFromClassList("roe-selected");
        root.Q<Button>("hud-ai-auto")?.RemoveFromClassList("roe-selected");
        Button patrulha = root.Q<Button>("btn-contexto-patrulhar");
        if (patrulha != null) patrulha.SetEnabled(false);
        Button retornoBase = root.Q<Button>("hud-context-rtb");
        if (retornoBase != null) retornoBase.style.display = DisplayStyle.None;
        for (int i = 1; i <= 5; i++)
        {
            Button slot = root.Q<Button>("formation-slot-" + i);
            if (slot == null) continue;
            slot.SetEnabled(false);
            slot.text = "·";
        }
    }

    private void VincularHudTatico()
    {
        VincularBotaoHud("hud-radar", () => ExecutarOrdemContextual("RADAR_ALTERNAR"));
        VincularBotaoHud("hud-roe-hold", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Passivo));
        VincularBotaoHud("hud-roe-defensive", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Manual));
        VincularBotaoHud("hud-roe-tight", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Automatico, true));
        VincularBotaoHud("hud-roe-free", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Automatico, false));
        VincularBotaoHud("hud-ai-manual", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Manual));
        VincularBotaoHud("hud-ai-assist", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Automatico, true));
        VincularBotaoHud("hud-ai-auto", () => AplicarModoCombateHud(MenuCombateNaval.Modo.Automatico, false));
        VincularBotaoHud("hud-speed-down", () => AjustarVelocidadeSelecaoHud(-0.1f));
        VincularBotaoHud("hud-speed-up", () => AjustarVelocidadeSelecaoHud(0.1f));
        VincularBotaoHud("hud-context-rtb", () => ExecutarOrdemContextual("VOLTAR_BASE"));
        VincularBotaoHud("quick-patrol", () => ExecutarOrdemContextual("PATRULHAR"));
        VincularBotaoHud("quick-escort", ExecutarEscoltaHud);
        VincularBotaoHud("quick-formation", AlternarEdicaoFormacaoHud);
        VincularBotaoHud("quick-intercept", () => ExecutarOrdemContextual("ATACAR"));
        VincularBotaoHud("quick-damage", FocarDanosDaSelecao);
        VincularBotaoHud("quick-camera", FocarCameraNaSelecao);
        VincularBotaoHud("quick-camera-options", () => AlternarModoCameraDrone());
        VincularBotaoHud("quick-grid", AplicarFormacaoEmGradeHud);
        for (int i = 1; i <= 5; i++)
        {
            int indice = i - 1;
            Button slot = root != null ? root.Q<Button>("formation-slot-" + i) : null;
            if (slot == null) continue;
            slot.clicked += () =>
            {
                if (ignorarClickSlotFormacao)
                {
                    ignorarClickSlotFormacao = false;
                    return;
                }
                DefinirLiderHud(indice);
            };
            slot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !modoEdicaoFormacaoHud) return;
                indiceArrasteFormacao = indice;
                posicaoInicioArrasteFormacao = evt.position;
                arrasteSlotFormacaoReconhecido = false;
                ignorarClickSlotFormacao = false;
            });
        }

        root.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (indiceArrasteFormacao >= 0 && ((Vector2)evt.position - posicaoInicioArrasteFormacao).sqrMagnitude >= 25f)
                arrasteSlotFormacaoReconhecido = true;
        });
        root.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0 || indiceArrasteFormacao < 0) return;
            if (arrasteSlotFormacaoReconhecido)
            {
                ignorarClickSlotFormacao = true;
                VisualElement destino = evt.target as VisualElement;
                while (destino != null && (destino.name == null || !destino.name.StartsWith("formation-slot-", StringComparison.Ordinal)))
                    destino = destino.parent;
                if (destino != null && int.TryParse(destino.name.Substring("formation-slot-".Length), out int numeroSlot))
                    ReordenarSlotFormacao(indiceArrasteFormacao, numeroSlot - 1);
                root.schedule.Execute(() => ignorarClickSlotFormacao = false).StartingIn(0);
            }
            indiceArrasteFormacao = -1;
            arrasteSlotFormacaoReconhecido = false;
        });

    }

    private void VincularBotaoHud(string nome, Action acao)
    {
        Button botao = root != null ? root.Q<Button>(nome) : null;
        if (botao != null) botao.clicked += acao;
    }

    private void VincularExpansaoCardsHud()
    {
        if (root == null) return;
        root.Query<VisualElement>(className: "tactical-card").ForEach(card =>
        {
            card.RegisterCallback<ClickEvent>(evt =>
            {
                VisualElement alvo = evt.target as VisualElement;
                while (alvo != null && alvo != card)
                {
                    if (alvo is Button) return;
                    alvo = alvo.parent;
                }
                if (alvo != card) return;
                AbrirCardHudExpandido(card);
                evt.StopPropagation();
            });
            card.tooltip = TextoHud("card.expand.tooltip", "Clique para ampliar este painel.");
        });
    }

    private void CriarOverlayCardsHud()
    {
        if (root == null || hudCardOverlay != null) return;

        hudCardOverlay = new VisualElement { name = "hud-card-overlay", pickingMode = PickingMode.Position };
        hudCardOverlay.AddToClassList("hud-card-overlay");

        VisualElement backdrop = new VisualElement { name = "hud-card-backdrop", pickingMode = PickingMode.Position };
        backdrop.AddToClassList("hud-card-backdrop");
        backdrop.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target != backdrop) return;
            FecharCardHudExpandido();
            evt.StopPropagation();
        });
        hudCardOverlay.Add(backdrop);

        hudCardPanel = new VisualElement { name = "hud-card-detail-panel", pickingMode = PickingMode.Position };
        hudCardPanel.AddToClassList("hud-card-detail-panel");
        hudCardPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

        VisualElement header = new VisualElement { name = "hud-card-detail-header", pickingMode = PickingMode.Ignore };
        header.AddToClassList("hud-card-detail-header");
        hudCardExpandedTitle = new Label { name = "hud-card-detail-title", pickingMode = PickingMode.Ignore };
        hudCardExpandedTitle.AddToClassList("hud-card-detail-title");
        Button closeButton = new Button(FecharCardHudExpandido) { name = "hud-card-detail-close", text = "×" };
        closeButton.AddToClassList("hud-card-detail-close");
        header.Add(hudCardExpandedTitle);
        header.Add(closeButton);

        hudCardExpandedDescription = new Label { name = "hud-card-detail-description", pickingMode = PickingMode.Ignore };
        hudCardExpandedDescription.AddToClassList("hud-card-detail-description");
        hudCardScroll = new ScrollView(ScrollViewMode.Vertical) { name = "hud-card-detail-scroll" };
        hudCardScroll.AddToClassList("hud-card-detail-scroll");

        hudCardPanel.Add(header);
        hudCardPanel.Add(hudCardExpandedDescription);
        hudCardPanel.Add(hudCardScroll);
        hudCardOverlay.Add(hudCardPanel);
        hudCardOverlay.style.display = DisplayStyle.None;
        root.Add(hudCardOverlay);
    }

    private HudCardInfo ObterInfoCardHud(VisualElement card)
    {
        if (card == null) return null;
        if (card.ClassListContains("identity-card")) return new HudCardInfo { TitleKey = "identity.title", TitleFallback = "COMANDO TÁTICO", DescriptionKey = "cardinfo.identity", DescriptionFallback = "Veja a unidade selecionada, o país, a integridade e a prontidão." };
        if (card.ClassListContains("speed-card")) return new HudCardInfo { TitleKey = "card.speed", TitleFallback = "VELOCIDADE / RUMO", DescriptionKey = "cardinfo.speed", DescriptionFallback = "Acompanhe velocidade e rumo da unidade ou a média do grupo." };
        if (card.ClassListContains("condition-card")) return new HudCardInfo { TitleKey = "card.condition", TitleFallback = "CONDIÇÃO DA UNIDADE", DescriptionKey = "cardinfo.condition", DescriptionFallback = "Confira o estado atual e retorne à base quando essa opção estiver disponível." };
        if (card.ClassListContains("formation-card")) return new HudCardInfo { TitleKey = "card.formation", TitleFallback = "FORMAÇÃO", DescriptionKey = "cardinfo.formation", DescriptionFallback = "Escolha o líder e reorganize as unidades. F3 ativa a edição por arraste." };
        if (card.ClassListContains("roe-card")) return new HudCardInfo { TitleKey = "card.roe", TitleFallback = "POSTURA / REGRAS DE FOGO", DescriptionKey = "cardinfo.roe", DescriptionFallback = "Defina quando o armamento pode disparar: passivo, sob ordem, contra alvos autorizados ou livre." };
        if (card.ClassListContains("sensors-card")) return new HudCardInfo { TitleKey = "card.sensors", TitleFallback = "SENSORES / EMCON", DescriptionKey = "cardinfo.sensors", DescriptionFallback = "Alterne o radar. Radar ligado consome energia e pode ser detectado; outros sensores aparecem quando a unidade os possui." };
        if (card.ClassListContains("weapons-card")) return new HudCardInfo { TitleKey = "card.weapons", TitleFallback = "ARMAMENTO", DescriptionKey = "cardinfo.weapons", DescriptionFallback = "Consulte os tipos de arma e a munição disponível na unidade." };
        if (card.ClassListContains("counter-card")) return new HudCardInfo { TitleKey = "card.countermeasures", TitleFallback = "CONTRAMEDIDAS", DescriptionKey = "cardinfo.countermeasures", DescriptionFallback = "Confira chaff e iscas acústicas quando esses sistemas estiverem instalados." };
        if (card.ClassListContains("waypoint-card")) return new HudCardInfo { TitleKey = "card.waypoints", TitleFallback = "PATRULHA / PONTOS DE ROTA", DescriptionKey = "cardinfo.waypoints", DescriptionFallback = "Veja a ordem de rota e use Patrulha para marcar pontos no mapa." };
        if (card.ClassListContains("automation-card")) return new HudCardInfo { TitleKey = "card.automation", TitleFallback = "IA / AUTOMAÇÃO", DescriptionKey = "cardinfo.automation", DescriptionFallback = "Manual aguarda ordens do jogador; Assistência limita o automático a alvos autorizados; Automático engaja alvos válidos detectados." };
        return null;
    }

    private void AbrirCardHudExpandido(VisualElement card)
    {
        HudCardInfo info = ObterInfoCardHud(card);
        if (info == null || card == null || card.parent == null) return;
        if (hudCardExpanded == card) return;
        FecharCardHudExpandido();
        CriarOverlayCardsHud();
        if (hudCardOverlay == null || hudCardScroll == null) return;

        hudCardExpanded = card;
        hudCardExpandedInfo = info;
        hudCardOriginalParent = card.parent;
        hudCardOriginalIndex = hudCardOriginalParent.hierarchy.IndexOf(card);
        hudCardOriginalParent.Remove(card);
        card.AddToClassList("hud-card-expanded-content");
        hudCardScroll.Add(card);
        AtualizarTextoCardHudExpandido();
        hudCardOverlay.style.display = DisplayStyle.Flex;
        hudCardPanel.BringToFront();
    }

    private void AtualizarTextoCardHudExpandido()
    {
        if (hudCardExpandedInfo == null) return;
        if (hudCardExpandedTitle != null)
            hudCardExpandedTitle.text = TextoHud(hudCardExpandedInfo.TitleKey, hudCardExpandedInfo.TitleFallback);
        if (hudCardExpandedDescription != null)
            hudCardExpandedDescription.text = TextoHud(hudCardExpandedInfo.DescriptionKey, hudCardExpandedInfo.DescriptionFallback);
    }

    private void FecharCardHudExpandido()
    {
        if (hudCardExpanded != null)
        {
            if (hudCardScroll != null && hudCardExpanded.parent == hudCardScroll)
                hudCardScroll.Remove(hudCardExpanded);
            hudCardExpanded.RemoveFromClassList("hud-card-expanded-content");
            if (hudCardOriginalParent != null)
            {
                int indice = Mathf.Clamp(hudCardOriginalIndex, 0, hudCardOriginalParent.childCount);
                hudCardOriginalParent.hierarchy.Insert(indice, hudCardExpanded);
            }
        }
        hudCardExpanded = null;
        hudCardExpandedInfo = null;
        hudCardOriginalParent = null;
        hudCardOriginalIndex = -1;
        if (hudCardOverlay != null) hudCardOverlay.style.display = DisplayStyle.None;
    }

    private void DefinirLiderHud(int indice)
    {
        SincronizarSelecaoComJogo();
        unidadesSelecionadasMenu.RemoveAll(unidade => unidade == null);
        if (indice < 0 || indice >= unidadesSelecionadasMenu.Count) return;
        ControleUnidade novoLider = unidadesSelecionadasMenu[indice];
        unidadesSelecionadasMenu.RemoveAt(indice);
        unidadesSelecionadasMenu.Insert(0, novoLider);
        if (gerenteSelecao != null && gerenteSelecao.unidadesSelecionadas != null)
        {
            int indiceJogo = gerenteSelecao.unidadesSelecionadas.IndexOf(novoLider);
            if (indiceJogo >= 0)
            {
                gerenteSelecao.unidadesSelecionadas.RemoveAt(indiceJogo);
                gerenteSelecao.unidadesSelecionadas.Insert(0, novoLider);
            }
        }
        unidadeSelecionadaMenu = novoLider;
        SalvarSelecaoPersistida();
        AtualizarBarraComandoContextual();
    }

    private void AplicarModoCombateHud(MenuCombateNaval.Modo alvo, bool limitarAutomatico = true)
    {
        SincronizarSelecaoComJogo();
        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (contextoFeedback != null)
                contextoFeedback.text = TextoHud("feedback.select_ally", "Selecione uma unidade aliada.");
            return;
        }
        int aplicadas = 0;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null) continue;
            if (MenuCombateNaval.DefinirModoCombateHud(unidade, alvo, limitarAutomatico)) aplicadas++;
        }
        if (contextoFeedback != null)
            contextoFeedback.text = string.Format(
                TextoHud("feedback.mode", "MODO {0} · {1}/{2} UNIDADES"),
                TraduzirModoHud(alvo, limitarAutomatico),
                aplicadas,
                unidadesSelecionadasMenu.Count);
        AtualizarBarraComandoContextual();
    }

    private void AjustarVelocidadeSelecaoHud(float variacao)
    {
        SincronizarSelecaoComJogo();
        unidadesSelecionadasMenu.RemoveAll(unidade => unidade == null);
        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (contextoFeedback != null)
                contextoFeedback.text = TextoHud("feedback.select_ally", "Selecione uma unidade aliada.");
            return;
        }

        int alteradas = 0;
        int noLimite = 0;
        int semMovimentoAjustavel = 0;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null) continue;
            if (!unidade.PossuiControleVelocidadeHud)
            {
                semMovimentoAjustavel++;
                continue;
            }

            if (unidade.AjustarVelocidadeComandoHud(variacao)) alteradas++;
            else noLimite++;
        }

        if (contextoFeedback != null)
        {
            if (alteradas == 0 && noLimite == 0)
                contextoFeedback.text = TextoHud("speed.unavailable", "Nenhuma unidade selecionada possui movimento ajustável.");
            else if (alteradas == 0)
                contextoFeedback.text = TextoHud("speed.limit", "Limite de velocidade atingido para a seleção.");
            else
                contextoFeedback.text = string.Format(
                    TextoHud("speed.feedback", "VELOCIDADE {0}10% · {1}/{2} UNID. · {3} NO LIMITE · {4} SEM MOTOR"),
                    variacao > 0f ? "+" : "−",
                    alteradas,
                    unidadesSelecionadasMenu.Count,
                    noLimite,
                    semMovimentoAjustavel);
        }

        AtualizarBarraComandoContextual();
    }

    private static MenuCombateNaval.Modo ObterModoCombateHud(ControleUnidade unidade)
    {
        return MenuCombateNaval.ObterModoCombateHud(unidade);
    }

    private void FocarCameraNaSelecao()
    {
        SincronizarSelecaoComJogo();
        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.select_ally", "Selecione uma unidade aliada.");
            return;
        }
        Vector3 centro = Vector3.zero;
        int total = 0;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            if (unidadesSelecionadasMenu[i] == null) continue;
            centro += unidadesSelecionadasMenu[i].transform.position;
            total++;
        }
        if (total == 0) return;
        CameraController camera = Camera.main != null ? Camera.main.GetComponent<CameraController>() : FindFirstObjectByType<CameraController>();
        if (camera == null)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.camera.missing", "CÂMERA DE JOGO INDISPONÍVEL.");
            return;
        }
        camera.FocarEm(centro / total);
        if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.camera.centered", "CÂMERA CENTRALIZADA NA SELEÇÃO.");
        AtualizarBarraComandoContextual();
    }

    private void ProcessarAtalhosHud()
    {
        if (hudCardExpanded != null && Input.GetKeyDown(KeyCode.Escape))
        {
            FecharCardHudExpandido();
            return;
        }
        if (menuAberto || !BarraContextualDisponivel || Construtor.EmModoConstrucaoAtivo) return;

        // Permite controlar o ritmo do grupo pela tecla +/−, além dos botões
        // no card de velocidade. Input.inputString cobre layouts de teclado
        // em que o sinal de mais não corresponde a KeyCode.Equals + Shift.
        if (!CampoTextoHudFocado())
        {
            bool aumentarVelocidade = Input.GetKeyDown(KeyCode.KeypadPlus)
                || (Input.GetKeyDown(KeyCode.Equals)
                    && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                || Input.inputString.IndexOf('+') >= 0;
            bool reduzirVelocidade = Input.GetKeyDown(KeyCode.KeypadMinus)
                || Input.GetKeyDown(KeyCode.Minus)
                || Input.inputString.IndexOf('-') >= 0;

            if (aumentarVelocidade)
            {
                AjustarVelocidadeSelecaoHud(0.1f);
                return;
            }
            if (reduzirVelocidade)
            {
                AjustarVelocidadeSelecaoHud(-0.1f);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.F1)) { ExecutarOrdemContextual("SEGUIR"); return; }
        if (Input.GetKeyDown(KeyCode.F2)) { ExecutarEscoltaHud(); return; }
        if (Input.GetKeyDown(KeyCode.F3)) { AlternarEdicaoFormacaoHud(); return; }
        if (Input.GetKeyDown(KeyCode.F6)) { FocarDanosDaSelecao(); return; }
        if (Input.GetKeyDown(KeyCode.F7)) { FocarCameraNaSelecao(); return; }
        string ordem = null;
        if (Input.GetKeyDown(KeyCode.F4)) ordem = "PATRULHAR";
        else if (Input.GetKeyDown(KeyCode.F5)) ordem = "ATACAR";
        if (!string.IsNullOrEmpty(ordem)) ExecutarOrdemContextual(ordem);
    }

    private bool CampoTextoHudFocado()
    {
        return root != null
            && root.panel != null
            && root.panel.focusController.focusedElement is TextField;
    }

    private void ExecutarEscoltaHud()
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        if (unidadeSelecionadaMenu == null || unidadesSelecionadasMenu.Count < 2)
        {
            if (contextoFeedback != null)
                contextoFeedback.text = TextoHud("feedback.escort.need_group", "Selecione um líder e pelo menos uma unidade para escoltá-lo.");
            return;
        }

        int escoltas = 0;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null || unidade == unidadeSelecionadaMenu) continue;
            if (unidade.EmitirOrdemSeguir(unidadeSelecionadaMenu.transform, distanciaSeguimentoAtual)) escoltas++;
        }

        if (contextoFeedback != null)
            contextoFeedback.text = string.Format(
                TextoHud("feedback.escort.issued", "ESCOLTA · {0} UNIDADES SEGUINDO {1}"),
                escoltas,
                ObterNomeExibicao(unidadeSelecionadaMenu.gameObject));
        if (escoltas > 0)
            AdicionarLog("OPS", $"{escoltas} unidades receberam ordem de escoltar {ObterNomeExibicao(unidadeSelecionadaMenu.gameObject)}.", "normal");
        AtualizarBarraComandoContextual();
    }

    private void AplicarFormacaoEmGradeHud()
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        if (unidadesSelecionadasMenu.Count < 2)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.need_two_grid", "Selecione pelo menos duas unidades para formar uma grade.");
            return;
        }

        Vector3 centro = Vector3.zero;
        int total = 0;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null) continue;
            centro += unidade.transform.position;
            total++;
        }
        if (total < 2) return;
        centro /= total;

        Vector3 frente = unidadeSelecionadaMenu != null
            ? unidadeSelecionadaMenu.transform.forward
            : Vector3.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
        frente.Normalize();
        Vector3 direita = Vector3.Cross(Vector3.up, frente).normalized;
        float espacamento = Mathf.Max(12f, Mathf.Sqrt(Mathf.Max(1f, total)) * 8f);
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(total));
        float largura = (colunas - 1) * espacamento;
        int atribuidas = 0;

        // Mantém o líder no centro; membros restantes ocupam uma grade alinhada ao rumo dele.
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null) continue;
            if (unidade == unidadeSelecionadaMenu) continue;
            int slot = atribuidas++;
            int coluna = slot % colunas;
            int linha = slot / colunas;
            float x = coluna * espacamento - largura * 0.5f;
            Vector3 destino = centro - frente * ((linha + 1) * espacamento) + direita * x;
            unidade.EmitirOrdemMover(destino);
        }
        if (contextoFeedback != null)
            contextoFeedback.text = string.Format(TextoHud("feedback.grid_applied", "FORMAÇÃO EM GRADE · {0} UNIDADES"), total);
        AdicionarLog("OPS", $"Formação em grade aplicada a {total} unidades.", "normal");
    }

    private void AlternarEdicaoFormacaoHud()
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        if (unidadesSelecionadasMenu.Count < 2)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.need_two_formation", "Selecione pelo menos duas unidades para editar a formação.");
            return;
        }

        modoEdicaoFormacaoHud = !modoEdicaoFormacaoHud;
        VisualElement cartao = root != null ? root.Q<VisualElement>("formation-card") : null;
        if (cartao != null) cartao.EnableInClassList("formation-editing", modoEdicaoFormacaoHud);
        DefinirTextoHud("hud-formation-hint", modoEdicaoFormacaoHud
            ? TextoHud("feedback.formation_drag", "ARRASTE OS SLOTS · F3 CONCLUIR")
            : TextoHud("formation.edit", "F3 EDITAR FORMAÇÃO"));
        if (contextoFeedback != null)
            contextoFeedback.text = modoEdicaoFormacaoHud
                ? TextoHud("feedback.formation_active", "EDIÇÃO DE FORMAÇÃO ATIVA · ARRASTE UNIDADES ENTRE SLOTS")
                : TextoHud("feedback.formation_done", "EDIÇÃO DE FORMAÇÃO CONCLUÍDA");
        AtualizarBarraComandoContextual();
    }

    private void FocarDanosDaSelecao()
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        if (unidadeSelecionadaMenu == null)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.select_ally", "Selecione uma unidade aliada.");
            return;
        }
        CameraUnidadeHUD cameraHud = CameraUnidadeHUD.Instancia;
        if (cameraHud != null) cameraHud.DefinirTarget(unidadeSelecionadaMenu, true);
        if (contextoFeedback != null)
        {
            SistemaDeDanos danos = unidadeSelecionadaMenu.GetComponentInChildren<SistemaDeDanos>(true);
            contextoFeedback.text = danos != null && danos.vidaMaxima > 0f
                ? string.Format(TextoHud("feedback.damage", "CONTROLE DE DANOS · {0:F0}/{1:F0} HP · {2:P0}"), danos.vidaAtual, danos.vidaMaxima, danos.vidaAtual / danos.vidaMaxima)
                : TextoHud("feedback.damage_missing", "CONTROLE DE DANOS · DADOS DE INTEGRIDADE INDISPONÍVEIS");
        }
        AtualizarBarraComandoContextual();
    }

    private void ReordenarSlotFormacao(int origem, int destino)
    {
        SincronizarSelecaoComJogo();
        if (origem == destino || origem < 0 || destino < 0 || origem >= unidadesSelecionadasMenu.Count || destino >= unidadesSelecionadasMenu.Count) return;
        ControleUnidade unidade = unidadesSelecionadasMenu[origem];
        unidadesSelecionadasMenu[origem] = unidadesSelecionadasMenu[destino];
        unidadesSelecionadasMenu[destino] = unidade;
        if (gerenteSelecao != null && gerenteSelecao.unidadesSelecionadas != null
            && gerenteSelecao.unidadesSelecionadas.Count == unidadesSelecionadasMenu.Count)
        {
            ControleUnidade doGerente = gerenteSelecao.unidadesSelecionadas[origem];
            gerenteSelecao.unidadesSelecionadas[origem] = gerenteSelecao.unidadesSelecionadas[destino];
            gerenteSelecao.unidadesSelecionadas[destino] = doGerente;
        }
        unidadeSelecionadaMenu = unidadesSelecionadasMenu[0];
        SalvarSelecaoPersistida();
        AtualizarBarraComandoContextual();
    }

    private void AtualizarHudTatico(ControleUnidade unidade, bool grupo)
    {
        if (root == null || unidade == null) return;
        float integridadePontos = 0f, integridadeMaxima = 0f;
        float combustivelPontos = 0f, combustivelMaximo = 0f;
        float velocidadeTotal = 0f;
        int contagemVelocidade = 0;
        float multiplicadorVelocidadeTotal = 0f;
        float multiplicadorVelocidadeMinimo = float.MaxValue;
        float multiplicadorVelocidadeMaximo = float.MinValue;
        int contagemMultiplicadorVelocidade = 0;
        bool velocidadesEmNos = true;
        int modosPassivo = 0, modosManual = 0, modosAutomaticoLimitado = 0, modosAutomaticoLivre = 0;
        int quantidade = 0;
        IReadOnlyList<ControleUnidade> selecionadas = unidadesSelecionadasMenu;
        for (int i = 0; i < selecionadas.Count; i++)
        {
            ControleUnidade item = selecionadas[i];
            if (item == null) continue;
            quantidade++;
            SistemaDeDanos danoItem = item.GetComponentInChildren<SistemaDeDanos>(true);
            if (danoItem != null && danoItem.vidaMaxima > 0f)
            {
                integridadePontos += Mathf.Clamp(danoItem.vidaAtual, 0f, danoItem.vidaMaxima);
                integridadeMaxima += danoItem.vidaMaxima;
            }
            CombustivelUnidade combustivelItem = item.GetComponentInChildren<CombustivelUnidade>(true);
            if (combustivelItem != null && combustivelItem.usaCombustivel && combustivelItem.Capacidade > 0f)
            {
                combustivelPontos += combustivelItem.CombustivelAtual;
                combustivelMaximo += combustivelItem.Capacidade;
            }
            float velocidadeItem = item.ObterVelocidadeAtualReal();
            if (velocidadeItem <= 0.01f)
                velocidadeItem = ObterVelocidadeTelemetria(item.gameObject, velocidadeItem);
            ControleNavioRealista navioItem = item.GetComponentInChildren<ControleNavioRealista>(true);
            ControleAviao aviaoItem = item.GetComponentInChildren<ControleAviao>(true);
            if (navioItem != null) velocidadeItem = navioItem.VelocidadeAtual;
            if (aviaoItem != null) velocidadeItem = aviaoItem.VelocidadeVooAtual;
            velocidadeTotal += Mathf.Max(0f, velocidadeItem);
            contagemVelocidade++;
            float multiplicadorHud = item.MultiplicadorVelocidadeComandoHud;
            multiplicadorVelocidadeTotal += multiplicadorHud;
            multiplicadorVelocidadeMinimo = Mathf.Min(multiplicadorVelocidadeMinimo, multiplicadorHud);
            multiplicadorVelocidadeMaximo = Mathf.Max(multiplicadorVelocidadeMaximo, multiplicadorHud);
            contagemMultiplicadorVelocidade++;
            if (!item.EhUnidadeNaval() && !item.EhUnidadeAerea()) velocidadesEmNos = false;
            switch (ObterModoCombateHud(item))
            {
                case MenuCombateNaval.Modo.Passivo: modosPassivo++; break;
                case MenuCombateNaval.Modo.Manual: modosManual++; break;
                default:
                    if (MenuCombateNaval.ModoCombateHudLimitaAlvos(item)) modosAutomaticoLimitado++;
                    else modosAutomaticoLivre++;
                    break;
            }
        }

        float integridade = integridadeMaxima > 0f ? integridadePontos / integridadeMaxima : -1f;
        float prontidao = combustivelMaximo > 0f ? combustivelPontos / combustivelMaximo : -1f;
        bool todosPassivos = quantidade > 0 && modosPassivo == quantidade;
        bool todosManuais = quantidade > 0 && modosManual == quantidade;
        bool todosAssistidos = quantidade > 0 && modosAutomaticoLimitado == quantidade;
        bool todosLivres = quantidade > 0 && modosAutomaticoLivre == quantidade;
        bool misto = !todosPassivos && !todosManuais && !todosAssistidos && !todosLivres;

        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
            ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
        AtualizarBandeiraHud(identidade != null ? identidade.nomeDoPais : string.Empty);
        DefinirTextoHud("hud-status", grupo
            ? string.Format(TextoHud("status.group", "GRUPO · {0} UNIDADES"), quantidade)
            : unidade.ModoCombateAtivo
                ? TextoHud("status.combat", "COMBATE ATIVO")
                : TextoHud("status.formation", "EM FORMAÇÃO"));
        Button roeHold = root.Q<Button>("hud-roe-hold");
        Button roeDefensive = root.Q<Button>("hud-roe-defensive");
        Button roeTight = root.Q<Button>("hud-roe-tight");
        Button roeFree = root.Q<Button>("hud-roe-free");
        Button aiManual = root.Q<Button>("hud-ai-manual");
        Button aiAssist = root.Q<Button>("hud-ai-assist");
        Button aiAuto = root.Q<Button>("hud-ai-auto");
        if (roeHold != null) roeHold.EnableInClassList("roe-selected", todosPassivos);
        if (roeDefensive != null) roeDefensive.EnableInClassList("roe-selected", todosManuais);
        if (roeTight != null) roeTight.EnableInClassList("roe-selected", todosAssistidos);
        if (roeFree != null) roeFree.EnableInClassList("roe-selected", todosLivres);
        if (aiManual != null) aiManual.EnableInClassList("roe-selected", todosManuais);
        if (aiAssist != null) aiAssist.EnableInClassList("roe-selected", todosAssistidos);
        if (aiAuto != null) aiAuto.EnableInClassList("roe-selected", todosLivres);
        AtualizarPreviewHud(ObterPerfilComandoContextual(unidade));
        DefinirTextoHud("hud-integrity", string.Format(
            TextoHud("integrity.status", "INTEGRIDADE  {0}"),
            integridade >= 0f ? $"{integridade * 100f:F0}%" : TextoHud("state.na", "N/D")));
        DefinirTextoHud("hud-readiness", string.Format(
            TextoHud("readiness.status", "PRONTIDÃO  {0}"),
            prontidao >= 0f ? $"{prontidao * 100f:F0}%" : TextoHud("state.na", "N/D")));
        DefinirLarguraHud("hud-integrity-fill", integridade);
        DefinirLarguraHud("hud-readiness-fill", prontidao);
        VisualElement integridadeFill = root.Q<VisualElement>("hud-integrity-fill");
        if (integridadeFill != null)
        {
            integridadeFill.EnableInClassList("hud-warning", integridade >= 0f && integridade <= 0.5f && integridade > 0.25f);
            integridadeFill.EnableInClassList("hud-critical", integridade >= 0f && integridade <= 0.25f);
        }
        DefinirAlertaCriticoHud(integridade);

        ControleNavioRealista navio = unidade.GetComponentInChildren<ControleNavioRealista>(true);
        ControleAviao aviao = unidade.GetComponentInChildren<ControleAviao>(true);
        ControleSubmarino submarino = unidade.GetComponentInChildren<ControleSubmarino>(true);
        float conversaoVelocidade = velocidadesEmNos ? 1.94384f : 3.6f;
        string unidadeVelocidade = velocidadesEmNos
            ? TextoHud("unit.knots", "nós")
            : TextoHud("speed.kmh", "km/h");
        DefinirTextoHud("hud-speed", TextoHud("speed.short", "VEL") + "  "
            + $"{(contagemVelocidade > 0 ? velocidadeTotal / contagemVelocidade * conversaoVelocidade : 0f):F1} "
            + unidadeVelocidade
            + (grupo ? " " + TextoHud("speed.average", "MÉDIA") : string.Empty));
        bool fatorVelocidadeMisto = contagemMultiplicadorVelocidade > 1
            && multiplicadorVelocidadeMaximo - multiplicadorVelocidadeMinimo > 0.001f;
        string fatorVelocidadeHud = contagemMultiplicadorVelocidade == 0
            ? "—"
            : fatorVelocidadeMisto
                ? TextoHud("speed.mixed", "MISTO")
                : $"{Mathf.RoundToInt(multiplicadorVelocidadeTotal / contagemMultiplicadorVelocidade * 100f)}%";
        DefinirTextoHud("hud-speed-order", string.Format(TextoHud("speed.order", "GRUPO {0}"), fatorVelocidadeHud));
        float rumo = unidade.transform.eulerAngles.y;
        DefinirTextoHud("hud-heading", TextoHud("heading.short", "PROA") + $"  {rumo:000}°");
        VisualElement seta = root.Q<VisualElement>("hud-compass-arrow");
        if (seta != null) seta.style.rotate = new Rotate(new Angle(rumo, AngleUnit.Degree));

        if (submarino != null)
        {
            DefinirTextoHud("hud-condition-label", TextoHud("condition.depth", "PROFUNDIDADE"));
            DefinirTextoHud("hud-condition", submarino.estaSubmerso
                ? $"{Mathf.Abs(submarino.profundidadeSubmersao):F0} {TextoHud("unit.metres", "m")}"
                : TextoHud("condition.surface", "SUPERFÍCIE"));
            DefinirTextoHud("hud-condition-detail", TextoHud("condition.mode", "MODO") + " " + TraduzirEstadoHud(submarino.modoAtual.ToString()));
        }
        else if (unidade.EhUnidadeAerea())
        {
            DefinirTextoHud("hud-condition-label", TextoHud("condition.altitude", "ALTITUDE"));
            DefinirTextoHud("hud-condition", $"{unidade.transform.position.y:F0} {TextoHud("unit.metres", "m")}");
            DefinirTextoHud("hud-condition-detail", aviao != null
                ? TraduzirEstadoHud(aviao.estadoAtual.ToString())
                : TraduzirEstadoHud(unidade.OrdemAtual.ToString()));
        }
        else if (navio != null)
        {
            DefinirTextoHud("hud-condition-label", TextoHud("condition.keel", "CALADO"));
            DefinirTextoHud("hud-condition", TextoHud("state.na", "N/D"));
            DefinirTextoHud("hud-condition-detail", TraduzirEstadoHud(unidade.OrdemAtual.ToString()));
        }
        else
        {
            DefinirTextoHud("hud-condition-label", TextoHud("condition.terrain", "TERRENO / ORDEM"));
            DefinirTextoHud("hud-condition", TraduzirEstadoHud(unidade.OrdemAtual.ToString()));
            DefinirTextoHud("hud-condition-detail", string.Format(
                TextoHud("condition.position", "POS {0}, {1}"),
                unidade.transform.position.x.ToString("F0"),
                unidade.transform.position.z.ToString("F0")));
        }
        Button rtb = root.Q<Button>("hud-context-rtb");
        if (rtb != null) rtb.style.display = unidade.EhUnidadeAerea() ? DisplayStyle.Flex : DisplayStyle.None;

        int time = identidade != null ? identidade.teamID : -1;
        RadarUnidadeTatica radar = unidade.GetComponent<RadarUnidadeTatica>();
        Button botaoRadar = root.Q<Button>("hud-radar");
        if (botaoRadar != null)
        {
            bool disponivel = UnidadePodeUsarRadar(unidade) && time == TimeJogadorAtual;
            botaoRadar.SetEnabled(disponivel);
            string estado = !disponivel
                ? TextoHud("state.na", "N/D")
                : radar != null && radar.RadarLigado
                    ? TextoHud("state.on", "LIGADO")
                    : TextoHud("state.off", "DESLIGADO");
            botaoRadar.text = string.Format(
                TextoHud("sensor.row", "{0}  {1}"),
                "◉ " + TextoHud("sensor.radar", "RADAR"),
                estado);
        }

        int mslNaval = 0, torpedos = 0, mslCaca = 0, mslAereo = 0;
        int nNaval = 0, nTorpedos = 0, nCaca = 0, nAereo = 0;
        int municaoCanhao = 0, nCanhoes = 0;
        int totalArmas = 0;
        for (int i = 0; i < selecionadas.Count; i++)
        {
            ControleUnidade item = selecionadas[i];
            if (item == null) continue;
            LancadorNaval[] navais = item.GetComponentsInChildren<LancadorNaval>(true);
            for (int j = 0; j < navais.Length; j++) { mslNaval += navais[j].municaoTotal; torpedos += navais[j].torpedosTotal; nNaval++; if (navais[j].torpedosMaximos > 0 || navais[j].torpedosTotal > 0) nTorpedos++; }
            LancadorMisselCaca[] cacadores = item.GetComponentsInChildren<LancadorMisselCaca>(true);
            for (int j = 0; j < cacadores.Length; j++) { mslCaca += cacadores[j].municaoAtual; nCaca++; }
            LancadorMisseis[] lancadoresAereos = item.GetComponentsInChildren<LancadorMisseis>(true);
            for (int j = 0; j < lancadoresAereos.Length; j++) { mslAereo += lancadoresAereos[j].municaoAtual; nAereo++; }
            ControleTorreta[] canhoes = item.GetComponentsInChildren<ControleTorreta>(true);
            for (int j = 0; j < canhoes.Length; j++) { municaoCanhao += canhoes[j].MunicaoAtual; nCanhoes++; }
            ControleTorretaModular[] torretasModulares = item.GetComponentsInChildren<ControleTorretaModular>(true);
            for (int j = 0; j < torretasModulares.Length; j++)
            {
                if (torretasModulares[j] == null || torretasModulares[j].armas == null) continue;
                for (int k = 0; k < torretasModulares[j].armas.Count; k++)
                {
                    ModuloArma arma = torretasModulares[j].armas[k];
                    if (arma == null || arma.tamanhoCartucho <= 0) continue;
                    municaoCanhao += Mathf.Max(0, arma.municaoAtual);
                    nCanhoes++;
                }
            }
            totalArmas += navais.Length + cacadores.Length + lancadoresAereos.Length;
        }
        string indisponivel = TextoHud("state.na", "N/D");
        DefinirTextoHud("hud-weapon-1", TextoHud("weapon.naval", "MÍSSEIS NAVAIS") + "  " + (nNaval > 0 ? mslNaval.ToString() : indisponivel));
        DefinirTextoHud("hud-weapon-2", TextoHud("weapon.torpedoes", "TORPEDOS") + "  " + (nTorpedos > 0 ? torpedos.ToString() : indisponivel));
        DefinirTextoHud("hud-weapon-3", TextoHud("weapon.air_to_air", "AR-AR") + "  " + (nCaca > 0 ? mslCaca.ToString() : indisponivel));
        DefinirTextoHud("hud-weapon-4", TextoHud("weapon.air_missiles", "MÍSSEIS AÉREOS") + "  " + (nAereo > 0 ? mslAereo.ToString() : indisponivel));
        DefinirTextoHud("hud-weapon-5", nCanhoes > 0
            ? TextoHud("weapon.gun_ammo", "MUNIÇÃO DE CANHÃO") + "  " + municaoCanhao
            : totalArmas > 0
                ? TextoHud("weapon.launchers", "LANÇADORES") + "  " + totalArmas
                : TextoHud("weapon.gun_ammo", "MUNIÇÃO DE CANHÃO") + "  " + indisponivel);

        DesenharLinhasOrdem desenhador = desenhadorOrdens != null ? desenhadorOrdens : FindFirstObjectByType<DesenharLinhasOrdem>();
        int pontos = desenhador != null && desenhador.pontosPatrulha != null ? desenhador.pontosPatrulha.Count : 0;
        OrdemMovimento ordemMovimento = unidade.OrdemMovimentoAtual;
        if (pontos > 0)
        {
            DefinirTextoHud("hud-waypoints", "● ─ ● ─ ● ─ ●");
            DefinirTextoHud("hud-waypoint-detail", string.Format(
                TextoHud("route.editing", "ROTA EM EDIÇÃO · {0} PONTOS"), pontos));
        }
        else if (unidade.PossuiOrdemMovimentoAtiva && ordemMovimento != null)
        {
            float distancia = Vector3.Distance(unidade.transform.position, ordemMovimento.Destino);
            DefinirTextoHud("hud-waypoints", $"● ─ ─ ─ ◉");
            DefinirTextoHud("hud-waypoint-detail", string.Format(
                TextoHud("route.distance", "{0} · {1:F0} U"),
                TraduzirEstadoHud(unidade.OrdemAtual.ToString()),
                distancia));
        }
        else
        {
            DefinirTextoHud("hud-waypoints", "○ ─ ○ ─ ○ ─ ○");
            DefinirTextoHud("hud-waypoint-detail", TextoHud("route.undefined", "ROTA NÃO DEFINIDA"));
        }

        DefinirTextoHud("hud-ai-status", misto
            ? TextoHud("ai.mixed", "MODO MISTO")
            : string.Format(TextoHud("ai.mode", "MODO {0}"), TraduzirModoHud(
                todosPassivos ? MenuCombateNaval.Modo.Passivo : todosManuais ? MenuCombateNaval.Modo.Manual : MenuCombateNaval.Modo.Automatico,
                todosAssistidos)));

        DefinirTextoHud("hud-formation-label", grupo
            ? string.Format(TextoHud("formation.group", "GRUPO TÁTICO ({0})"), unidadesSelecionadasMenu.Count)
            : TextoHud("formation.unit_leader", "UNIDADE / LÍDER"));
        if (!grupo && modoEdicaoFormacaoHud)
        {
            modoEdicaoFormacaoHud = false;
            VisualElement cartaoFormacao = root.Q<VisualElement>("formation-card");
            if (cartaoFormacao != null) cartaoFormacao.EnableInClassList("formation-editing", false);
            DefinirTextoHud("hud-formation-hint", TextoHud("formation.edit", "F3 EDITAR FORMAÇÃO"));
        }
        for (int i = 1; i <= 5; i++)
        {
            Button slot = root.Q<Button>("formation-slot-" + i);
            bool valido = grupo && i <= unidadesSelecionadasMenu.Count;
            if (slot != null)
            {
                slot.SetEnabled(valido);
                slot.EnableInClassList("formation-leader", valido && unidadesSelecionadasMenu[i - 1] == unidadeSelecionadaMenu);
                slot.text = valido ? i.ToString() : "·";
                float distanciaRelativa = valido && unidadeSelecionadaMenu != null
                    ? Vector3.Distance(unidadesSelecionadasMenu[i - 1].transform.position, unidadeSelecionadaMenu.transform.position)
                    : 0f;
                slot.tooltip = valido
                    ? string.Format(
                        TextoHud("tooltip.formation_move", "{0} · {1:F0} u do líder · clique para liderar ou arraste para reposicionar"),
                        ObterNomeExibicao(unidadesSelecionadasMenu[i - 1].gameObject),
                        distanciaRelativa)
                    : TextoHud("tooltip.empty_slot", "Sem unidade neste slot.");
            }
        }
    }

    private void AtualizarBandeiraHud(string nomePais)
    {
        Color topo = new Color(0.10f, 0.74f, 0.92f);
        Color meio = new Color(0.08f, 0.16f, 0.22f);
        Color baixo = new Color(0.56f, 0.65f, 0.70f);
        string pais = (nomePais ?? string.Empty).Trim().ToLowerInvariant();
        if (pais.Contains("aleman") || pais.Contains("german"))
        {
            topo = Color.black; meio = new Color(0.82f, 0.05f, 0.08f); baixo = new Color(0.98f, 0.75f, 0.08f);
        }
        else if (pais.Contains("united states") || pais.Contains("estados unidos") || pais == "eua")
        {
            topo = new Color(0.72f, 0.12f, 0.16f); meio = Color.white; baixo = new Color(0.12f, 0.22f, 0.46f);
        }
        else if (pais.Contains("russ"))
        {
            topo = Color.white; meio = new Color(0.10f, 0.30f, 0.70f); baixo = new Color(0.80f, 0.12f, 0.18f);
        }
        else if (pais.Contains("fran"))
        {
            topo = new Color(0.08f, 0.22f, 0.62f); meio = Color.white; baixo = new Color(0.82f, 0.12f, 0.18f);
        }
        else if (pais.Contains("brasil") || pais.Contains("brazil"))
        {
            topo = new Color(0.02f, 0.55f, 0.25f); meio = new Color(0.98f, 0.82f, 0.08f); baixo = new Color(0.02f, 0.55f, 0.25f);
        }
        else if (pais.Contains("china"))
        {
            topo = new Color(0.80f, 0.05f, 0.08f); meio = new Color(0.98f, 0.78f, 0.10f); baixo = new Color(0.80f, 0.05f, 0.08f);
        }
        else if (pais.Contains("jap"))
        {
            topo = Color.white; meio = new Color(0.82f, 0.08f, 0.16f); baixo = Color.white;
        }
        else if (pais.Contains("ital"))
        {
            topo = new Color(0.02f, 0.48f, 0.28f); meio = Color.white; baixo = new Color(0.82f, 0.12f, 0.18f);
        }

        DefinirCorHud("hud-flag-top", topo);
        DefinirCorHud("hud-flag-middle", meio);
        DefinirCorHud("hud-flag-bottom", baixo);
        VisualElement bandeira = root != null ? root.Q<VisualElement>("hud-country") : null;
        if (bandeira != null) bandeira.tooltip = string.IsNullOrWhiteSpace(nomePais)
            ? TextoHud("tooltip.country_unknown", "País da unidade desconhecido")
            : nomePais;
    }

    private void DefinirCorHud(string nome, Color cor)
    {
        VisualElement elemento = root != null ? root.Q<VisualElement>(nome) : null;
        if (elemento != null) elemento.style.backgroundColor = cor;
    }

    private void AtualizarPreviewHud(string perfil)
    {
        VisualElement preview = root != null ? root.Q<VisualElement>("hud-preview") : null;
        int unidadeId = unidadeSelecionadaMenu != null ? unidadeSelecionadaMenu.GetInstanceID() : 0;
        if (preview == null || (perfilPreviewHudAtual == perfil
            && hudPreviewUnidadeId == unidadeId
            && (preview.childCount > 0 || hudPreviewImagemConstruida || unidadeId == 0))) return;
        perfilPreviewHudAtual = perfil;
        hudPreviewUnidadeId = unidadeId;
        hudPreviewImagemConstruida = false;
        preview.Clear();
        preview.style.backgroundImage = StyleKeyword.None;
        preview.RemoveFromClassList("unit-preview-photo");

        if (unidadeSelecionadaMenu == null) return;

        MenuConstrucao menuConstrucao = MenuConstrucao.Instancia;
        Sprite imagemUnidade = menuConstrucao != null
            ? menuConstrucao.ObterIconeCatalogoUnidade(unidadeSelecionadaMenu.gameObject)
            : null;
        if (imagemUnidade != null)
        {
            preview.style.backgroundImage = new StyleBackground(imagemUnidade);
            preview.AddToClassList("unit-preview-photo");
            hudPreviewImagemConstruida = true;
            return;
        }

    }

    private void DefinirAlertaCriticoHud(float integridade)
    {
        VisualElement status = root != null ? root.Q<VisualElement>("hud-status") : null;
        if (status == null) return;
        bool ativo = integridade >= 0f && integridade <= 0.25f;
        bool aviso = integridade > 0.25f && integridade <= 0.5f;
        alertaCriticaHudAtivo = ativo;
        status.EnableInClassList("status-critical", ativo);
        status.EnableInClassList("status-warning", aviso);
        if (alertaCriticaHudAgendamento == null)
        {
            alertaCriticaHudAgendamento = status.schedule.Execute(() =>
            {
                if (!alertaCriticaHudAtivo)
                {
                    status.style.opacity = 1f;
                    return;
                }
                alertaCriticaHudFase = !alertaCriticaHudFase;
                status.style.opacity = alertaCriticaHudFase ? 1f : 0.45f;
            }).Every(450);
        }
        if (!ativo)
        {
            alertaCriticaHudFase = false;
            status.style.opacity = 1f;
        }
    }

    private void DefinirTextoHud(string nome, string valor)
    {
        Label label = root != null ? root.Q<Label>(nome) : null;
        if (label != null) label.text = valor;
    }

    private void DefinirLarguraHud(string nome, float valor)
    {
        VisualElement fill = root != null ? root.Q<VisualElement>(nome) : null;
        if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(valor) * 100f);
    }

    private static string ObterPerfilComandoContextual(ControleUnidade unidade)
    {
        if (unidade == null) return "UNIDADE";
        if (unidade.GetComponentInChildren<ControleSubmarino>(true) != null) return "SUBMARINO";
        if (unidade.EhUnidadeNaval()) return "NAVAL";
        if (TemControladorAereoMapa(unidade.gameObject)) return "AÉREA";

        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
            ?? unidade.GetComponentInParent<IdentidadeUnidade>()
            ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
        if (identidade == null) return "TERRESTRE";

        switch (identidade.tipoUnidade)
        {
            case TipoUnidade.Aereo: return "AÉREA";
            case TipoUnidade.Naval: return "NAVAL";
            case TipoUnidade.Estrutura: return "ESTRUTURA";
            case TipoUnidade.Veiculo: return "VEÍCULO";
            default: return "INFANTARIA";
        }
    }

    private string TraduzirPerfilHud(string perfil)
    {
        switch (perfil)
        {
            case "SUBMARINO": return TextoHud("profile.submarine", "SUBMARINO");
            case "NAVAL": return TextoHud("profile.naval", "NAVAL");
            case "AÉREA": return TextoHud("profile.air", "AÉREA");
            case "ESTRUTURA": return TextoHud("profile.structure", "ESTRUTURA");
            case "VEÍCULO": return TextoHud("profile.vehicle", "VEÍCULO");
            case "INFANTARIA": return TextoHud("profile.infantry", "INFANTARIA");
            default: return TextoHud("profile.unit", "UNIDADE");
        }
    }

    private string TraduzirModoCombateHud(MenuCombateNaval.Modo modo)
    {
        switch (modo)
        {
            case MenuCombateNaval.Modo.Manual: return TextoHud("mode.manual", "MANUAL");
            case MenuCombateNaval.Modo.Passivo: return TextoHud("mode.passive", "PASSIVO");
            default: return TextoHud("mode.automatic", "AUTOMÁTICO");
        }
    }

    private string TraduzirModoHud(MenuCombateNaval.Modo modo, bool limitarAutomatico)
    {
        if (modo == MenuCombateNaval.Modo.Automatico && limitarAutomatico)
            return TextoHud("ai.assist", "ASSISTÊNCIA");
        return TraduzirModoCombateHud(modo);
    }

    private string TraduzirEstadoHud(string estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return TextoHud("status.no_data", "SEM DADOS");

        switch (estado.Trim().ToLowerInvariant())
        {
            case "automatico":
            case "automático":
            case "automatic": return TextoHud("mode.automatic", "AUTOMÁTICO");
            case "manual": return TextoHud("mode.manual", "MANUAL");
            case "passivo":
            case "passive": return TextoHud("mode.passive", "PASSIVO");
            case "patrulhando":
            case "patrolling": return TextoHud("mode.patrolling", "PATRULHANDO");
            case "movendo":
            case "em movimento":
            case "moving": return TextoHud("mode.moving", "EM MOVIMENTO");
            case "seguindo":
            case "following": return TextoHud("mode.following", "SEGUINDO");
            case "atacando":
            case "attacking": return TextoHud("mode.attacking", "ATACANDO");
            case "aguardando":
            case "waiting":
            case "idle": return TextoHud("mode.waiting", "AGUARDANDO");
            case "retornando":
            case "returning": return TextoHud("mode.returning", "RETORNANDO");
            default: return estado.ToUpperInvariant();
        }
    }

    private string MontarDetalhesComandoContextual(ControleUnidade unidade, string perfil)
    {
        if (unidade == null) return TextoHud("status.no_telemetry", "SEM TELEMETRIA");

        float rumo = unidade.transform.eulerAngles.y;
        if (perfil == "SUBMARINO")
        {
            ControleSubmarino submarino = unidade.GetComponentInChildren<ControleSubmarino>(true);
            if (submarino != null)
            {
                float profundidade = submarino.estaSubmerso
                    ? Mathf.Abs(submarino.profundidadeSubmersao)
                    : 0f;
                return string.Format(TextoHud("detail.submarine", "PROF. {0:F0} m · RUMO {1:F0}°"), profundidade, rumo);
            }
        }

        if (perfil == "NAVAL")
        {
            ControleNavioRealista navio = unidade.GetComponentInChildren<ControleNavioRealista>(true);
            if (navio != null)
            {
                float nos = Mathf.Max(0f, navio.VelocidadeAtual) * 1.94384f;
                return string.Format(TextoHud("detail.naval", "RUMO {0:F0}° · VEL {1:F0} kt"), rumo, nos);
            }
        }

        if (perfil == "AÉREA")
        {
            ControleAviao aviao = unidade.GetComponentInChildren<ControleAviao>(true);
            if (aviao != null)
            {
                float nos = Mathf.Max(0f, aviao.VelocidadeVooAtual) * 1.94384f;
                return string.Format(TextoHud("detail.air", "ALT {0:F0} m · VEL {1:F0} kt · RUMO {2:F0}°"), unidade.transform.position.y, nos, rumo);
            }
            return string.Format(TextoHud("detail.air_no_speed", "ALT {0:F0} m · RUMO {1:F0}°"), unidade.transform.position.y, rumo);
        }

        return string.Format(
            TextoHud("detail.position", "POS {0:F0}, {1:F0} · RUMO {2:F0}°"),
            unidade.transform.position.x,
            unidade.transform.position.z,
            rumo);
    }

    private void ExecutarOrdemContextual(string ordem)
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.select_ally", "Selecione uma unidade aliada.");
            return;
        }

        bool exigeMapaTatico = ordem == "MOVER"
            || ordem == "PATRULHAR"
            || ordem == "ATACAR"
            || ordem == "SEGUIR";

        if (exigeMapaTatico)
        {
            SalvarSelecaoPersistida();
            AbrirMenu();
            if (!menuAberto)
            {
                if (contextoFeedback != null) contextoFeedback.text = TextoHud("feedback.center_unavailable", "O centro tático não pode abrir agora.");
                return;
            }

            // O centro tático restaura a seleção salva; sincronize novamente
            // com a seleção de jogo antes de armar a ordem do grupo.
            SincronizarSelecaoComJogo();
            NormalizarFocoSelecao();
            AtualizarTelemetriaUnidade();
        }

        ExecutarOrdem(ordem);
        if (contextoFeedback != null && ordemFeedback != null)
        {
            contextoFeedback.text = ordemFeedback.text;
        }
        AtualizarBarraComandoContextual();
    }

    private void AbrirMenuContextual()
    {
        SincronizarSelecaoComJogo();
        NormalizarFocoSelecao();
        SalvarSelecaoPersistida();
        AbrirMenu();
    }

    private void VincularBotaoOrdem(Button botao, string ordem)
    {
        if (botao == null) return;
        botao.pickingMode = PickingMode.Position;
        botoesOrdem.Add(botao);
        botao.clicked += () =>
        {
            AtualizarDestaqueOrdem(botao);
            ExecutarOrdem(ordem);
        };
    }

    private void AtualizarDestaqueOrdem(Button selecionado)
    {
        botaoOrdemSelecionado = selecionado;
        for (int i = 0; i < botoesOrdem.Count; i++)
        {
            Button botao = botoesOrdem[i];
            if (botao != null)
            {
                botao.EnableInClassList("ordem-selecionada", botao == selecionado);
                bool ativo = botao == selecionado;
                botao.style.borderTopWidth = ativo ? 3f : 1f;
                botao.style.borderRightWidth = ativo ? 3f : 1f;
                botao.style.borderBottomWidth = ativo ? 3f : 1f;
                botao.style.borderLeftWidth = ativo ? 3f : 1f;
                var corBorda = ativo ? new Color(1f, 0.82f, 0.08f) : new Color(0.05f, 0.7f, 0.85f);
                botao.style.borderTopColor = corBorda;
                botao.style.borderRightColor = corBorda;
                botao.style.borderBottomColor = corBorda;
                botao.style.borderLeftColor = corBorda;
                botao.style.unityFontStyleAndWeight = ativo ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }

    // Câmera FLIR — RenderTexture
    // -----------------------------------------------------------------------
    private void CriarRenderTextureFLIR()
    {
        flirRT = new RenderTexture(flirRenderWidth, flirRenderHeight, 24, RenderTextureFormat.ARGB32);
        flirRT.name = "MenuComando_FLIR_RT";
        flirRT.Create();

        if (flirImagem != null)
        {
            flirImagem.style.backgroundImage = new StyleBackground(
                Background.FromRenderTexture(flirRT));
        }
    }

    // -----------------------------------------------------------------------
    // Mapa Tático
    // -----------------------------------------------------------------------
    private void AtualizarMapaTatico()
    {
        if (mapaUnidadesLayer == null) return;
        AtualizarCacheEntidadesSeNecessario();

        // Atualiza título do mapa com zoom ativo
        if (mapaTitulo != null)
        {
            mapaTitulo.text = $"◉ MAPA TÁTICO (ZOOM: {mapaZoom:F1}X)";
        }

        // Calcula a nova janela de visualização baseada no Zoom e Pan
        float rangeX = (mundoMetade * 2f) / mapaZoom;
        float rangeZ = (mundoMetade * 2f) / mapaZoom;
        float xMin = mapaCentro.x - rangeX / 2f;
        float zMin = mapaCentro.y - rangeZ / 2f;

        mapaVivos.Clear();

        for (int i = 0; i < cacheUnidadesMapa.Count; i++)
        {
            var id = cacheUnidadesMapa[i];
            if (id == null || !id.gameObject.activeInHierarchy) continue;
            if (EhAviaoComercialNoSatelite(id.gameObject)) continue;

            int instId = id.gameObject.GetInstanceID();
            Hegemonia.Cartel.CartelNavalUnidade cartelNaval = id.GetComponent<Hegemonia.Cartel.CartelNavalUnidade>();
            bool amigo   = EhUnidadeDoJogador(id);
            bool inimigo = id.teamID > 0 && !amigo;
            bool ehImovel = EhImovelMapa(id.gameObject);
            if (!amigo && !inimigo && !ehImovel) continue;
            bool contatoAtualInimigo = false;
            bool contatoAntigoInimigo = false;
            Vector3 ultimaPosicaoConhecidaInimigo = Vector3.zero;

            // Casas/imóveis seguem como referência no satélite. Unidades e
            // instalações militares inimigas exigem contato de uma força em
            // guerra, recebido por visão direta ou radar.
            if (inimigo && !ehImovel)
            {
                bool emGuerra = Hegemonia.RTS.RTSVisibilityService.TeamsAtWar(TimeJogadorAtual, id.teamID);
                Hegemonia.RTS.RTSVisibilityService visibilidade = Hegemonia.RTS.RTSVisibilityService.Instancia;
                contatoAtualInimigo = visibilidade != null && visibilidade.IsVisibleToTeam(TimeJogadorAtual, id);
                contatoAntigoInimigo = !contatoAtualInimigo && visibilidade != null
                    && visibilidade.TryGetLastKnownPosition(TimeJogadorAtual, id, out ultimaPosicaoConhecidaInimigo);
                bool radarCartelVisivel = cartelNaval == null || cartelNaval.RadarVisivel || contatoAtualInimigo || contatoAntigoInimigo;
                if (!emGuerra || (!contatoAtualInimigo && !contatoAntigoInimigo) || !radarCartelVisivel)
                {
                    mapaVivos.Add(instId);
                    if (mapaElementos.TryGetValue(instId, out MapaItemUI marcadorOculto)
                        && marcadorOculto != null && marcadorOculto.Root != null)
                        marcadorOculto.Root.style.display = DisplayStyle.None;
                    continue;
                }
            }
            mapaVivos.Add(instId);

            Vector3 pos3D = inimigo && contatoAntigoInimigo
                ? ultimaPosicaoConhecidaInimigo
                : cartelNaval != null
                    ? cartelNaval.PosicaoConhecidaRadar
                    : id.transform.position;

            // Converte para % (0-100) usando a janela visível
            float pctX = ((pos3D.x - xMin) / rangeX) * 100f;
            float pctZ = (1f - (pos3D.z - zMin) / rangeZ) * 100f;

            if (!mapaElementos.TryGetValue(instId, out MapaItemUI item) || item == null || item.Root == null)
            {
                item = CriarElementoMapa(id, amigo, ObterEmojiUnidade(id));
                mapaElementos[instId] = item;
                mapaUnidadesLayer.Add(item.Root);
            }

            float hpPct = 1f;
            if (item.Dano != null && item.Dano.vidaMaxima > 0f)
            {
                hpPct = Mathf.Clamp01(item.Dano.vidaAtual / item.Dano.vidaMaxima);
            }

            // Atualiza posição e visibilidade (se fora do mapa aproximado, esconde para economizar render)
            item.Root.style.left = new StyleLength(new Length(pctX, LengthUnit.Percent));
            item.Root.style.top  = new StyleLength(new Length(pctZ, LengthUnit.Percent));
            item.Root.style.display = (pctX >= -5f && pctX <= 105f && pctZ >= -5f && pctZ <= 105f) ? DisplayStyle.Flex : DisplayStyle.None;

            // Atualiza barra de HP
            if (item.HpFill != null)
            {
                item.HpFill.style.width = new StyleLength(new Length(hpPct * 100f, LengthUnit.Percent));
            }

            // Atualiza seleção visual
            bool estasel = unidadesSelecionadasIds.Contains(instId);
            bool estaEmFoco = unidadeSelecionadaMenu != null
                && unidadeSelecionadaMenu.gameObject.GetInstanceID() == instId;
            if (item.Root != null)
            {
                item.Root.EnableInClassList("selecionado", estasel);
                item.Root.EnableInClassList("foco", estaEmFoco);
                bool contatoAntigo = inimigo && contatoAntigoInimigo && !contatoAtualInimigo;
                item.Root.EnableInClassList("contato-antigo", contatoAntigo);
                item.Root.pickingMode = contatoAntigo ? PickingMode.Ignore : PickingMode.Position;
                item.Root.tooltip = contatoAntigo
                    ? "Última posição conhecida — o contato de radar expirou"
                    : ObterNomeExibicao(id.gameObject);
            }
            if (item.Label != null)
            {
                item.Label.EnableInClassList("selecionado", estasel);
                item.Label.EnableInClassList("foco", estaEmFoco);
                if (!item.Label.ClassListContains("imovel"))
                {
                    item.Label.style.display = estasel ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            if (item.Marcador != null)
            {
                item.Marcador.EnableInClassList("selecionado", estasel);
                item.Marcador.EnableInClassList("foco", estaEmFoco);
            }
            if (item.Ring != null)
            {
                if (estasel) item.Ring.AddToClassList("visivel");
                else         item.Ring.RemoveFromClassList("visivel");
            }

            // Cor correta se HP zerado
            if (item.Marcador != null)
            {
                if (hpPct <= 0f && !item.UltimoDestruido)
                {
                    item.Marcador.RemoveFromClassList("amigo");
                    item.Marcador.RemoveFromClassList("inimigo");
                    item.Marcador.AddToClassList("destruido");
                    item.UltimoDestruido = true;
                }
                else if (hpPct > 0f && item.UltimoDestruido)
                {
                    item.Marcador.RemoveFromClassList("destruido");
                    if (amigo) item.Marcador.AddToClassList("amigo");
                    if (inimigo) item.Marcador.AddToClassList("inimigo");
                    item.UltimoDestruido = false;
                }
            }
        }

        // Marcador Direcional Drone Hasaf (<)
        if (CameraUnidadeHUD.Instancia != null && CameraUnidadeHUD.Instancia.modoDroneCamera && CameraUnidadeHUD.Instancia.gameObject.activeInHierarchy)
        {
            var camTrans = CameraUnidadeHUD.Instancia.transform;
            Vector3 pos3D = camTrans.position;
            
            float camPctX = ((pos3D.x - xMin) / rangeX) * 100f;
            float camPctZ = (1f - (pos3D.z - zMin) / rangeZ) * 100f;

            if (mapaCameraMarker == null)
            {
                var camMarker = new Label("<");
                camMarker.name = "mapa-cam-marker";
                camMarker.style.position = Position.Absolute;
                camMarker.style.color = Color.red;
                camMarker.style.fontSize = 20;
                camMarker.style.unityFontStyleAndWeight = FontStyle.Bold;
                camMarker.style.unityTextAlign = TextAnchor.MiddleCenter;
                camMarker.style.textShadow = new TextShadow { color = Color.black, offset = new Vector2(1,1), blurRadius = 2f };
                
                // Ajuste de pivô para rotacionar corretamente pelo centro
                camMarker.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));
                
                mapaCameraMarker = camMarker;
                mapaUnidadesLayer.Add(camMarker);
            }
            
            mapaCameraMarker.style.left = new StyleLength(new Length(camPctX, LengthUnit.Percent));
            mapaCameraMarker.style.top  = new StyleLength(new Length(camPctZ, LengthUnit.Percent));
            mapaCameraMarker.style.display = (camPctX >= -5f && camPctX <= 105f && camPctZ >= -5f && camPctZ <= 105f) ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Rotação da câmera: -90 para compensar o caractere '<' que aponta pra esquerda, mais a rotação Yaw
            float angle = camTrans.eulerAngles.y - 90f;
            mapaCameraMarker.style.rotate = new StyleRotate(new Rotate(angle));
        }
        else if (mapaCameraMarker != null)
        {
            mapaCameraMarker.style.display = DisplayStyle.None;
        }

        // Remove elementos de unidades que não existem mais
        foreach (var kv in mapaElementos)
        {
            if (!mapaVivos.Contains(kv.Key))
            {
                kv.Value?.Root?.RemoveFromHierarchy();
                mapaRemovidos.Add(kv.Key);
            }
        }
        for (int i = 0; i < mapaRemovidos.Count; i++)
        {
            mapaElementos.Remove(mapaRemovidos[i]);
        }
        mapaRemovidos.Clear();

        // Desenhar linhas de patrulha/ataque na UI
        DesenharLinhasOrdemNoMapaUI();
    }

    private MapaItemUI CriarElementoMapa(IdentidadeUnidade id, bool amigo, string emoji)
    {
        string classFacao = amigo ? "amigo" : "inimigo";
        bool ehImovel = EhImovelMapa(id.gameObject);
        bool ehFazenda = EhFazendaMapa(id.gameObject);

        var container = new VisualElement();
        container.AddToClassList("mapa-unidade");
        container.name = $"mapa-unit-{id.gameObject.GetInstanceID()}";
        if (ehImovel)
        {
            container.AddToClassList("imovel");
            container.AddToClassList(ehFazenda ? "fazenda" : "residencia");
        }
        // Unidades aliadas antigas podem chegar sem o adaptador de ordens.
        // Prepara somente as unidades do jogador para que o card/marcador
        // continue selecionável; nunca adiciona controle aos inimigos.
        // Imóveis civis não entram na seleção normal do satélite. Eles só
        // podem ser encontrados como alvo quando a ordem ATAQUE está ativa.
        ControleUnidade controleTatico = ehImovel ? null : ObterControleTatico(id, amigo);
        if (controleTatico != null)
        {
            container.AddToClassList("controlavel");
            container.tooltip = "Unidade controlavel pelo jogador — clique para assumir o controle";
        }

        // Imóveis não precisam ocupar o mapa com nomes longos. Mantemos um
        // marcador mínimo, com deslocamento determinístico, para que imóveis
        // próximos não formem uma coluna de textos sobrepostos.
        string nomeMapa = ObterNomeExibicao(id.gameObject);
        bool mostrarNomeUnidade = !ehImovel && amigo && controleTatico != null
            && unidadesSelecionadasMenu.Contains(controleTatico);
        string textoMapa = ehImovel
            ? "•"
            : mostrarNomeUnidade
                ? (nomeMapa.Length > 28 ? nomeMapa.Substring(0, 28) + "..." : nomeMapa)
                : string.Empty;
        var label = new Label(textoMapa);
        label.name = "mapa-label";
        label.AddToClassList("mapa-label");
        label.AddToClassList(classFacao);
        if (!ehImovel && !mostrarNomeUnidade)
        {
            label.style.display = DisplayStyle.None;
        }
        container.tooltip = ehImovel
            ? $"{nomeMapa} — alvo disponível somente no modo ATAQUE"
            : nomeMapa;
        if (ehImovel)
        {
            label.AddToClassList("imovel");
            label.tooltip = container.tooltip;

            int dispersao = Mathf.Abs(id.gameObject.GetInstanceID());
            float offsetX = ((dispersao % 5) - 2) * 7f;
            float offsetY = (((dispersao / 5) % 5) - 2) * 5f;
            label.style.left = offsetX;
            label.style.top = -8f + offsetY;
        }

        // Marcador
        var marcador = new VisualElement();
        marcador.name = "mapa-marcador";
        marcador.AddToClassList("mapa-marcador");
        marcador.AddToClassList(classFacao);

        var icone = new Label(emoji);
        icone.style.fontSize = 11;
        icone.style.unityTextAlign = TextAnchor.MiddleCenter;
        if (!amigo) icone.style.rotate = new StyleRotate(new Rotate(-45f)); // desfaz rotação do losango
        marcador.Add(icone);

        // Anel de seleção
        var ring = new VisualElement();
        ring.name = "mapa-sel-ring";
        ring.AddToClassList("mapa-sel-ring");

        // Barra de HP
        var hpTrack = new VisualElement();
        hpTrack.AddToClassList("mapa-hp-track");
        var hpFillEl = new VisualElement();
        hpFillEl.name = "mapa-hp-fill";
        hpFillEl.AddToClassList("mapa-hp-fill");
        hpFillEl.AddToClassList(classFacao);
        hpFillEl.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
        hpTrack.Add(hpFillEl);

        container.Add(label);
        container.Add(ring);
        container.Add(marcador);
        container.Add(hpTrack);

        // Clique para selecionar a unidade no menu
        ControleUnidade cu = controleTatico;
        SistemaDeDanos sd = id.GetComponent<SistemaDeDanos>();
        if (cu != null)
        {
            var capturedCu = cu;
            container.RegisterCallback<ClickEvent>(evt =>
            {
                if (container.ClassListContains("contato-antigo"))
                {
                    evt.StopPropagation();
                    return;
                }

                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (modoMoverMapaAtivo
                    || (desenhadorOrdens != null && (desenhadorOrdens.modoPatrulhaAtivo || desenhadorOrdens.modoSeguirAtivo || desenhadorOrdens.modoAtaqueAtivo)))
                {
                    if (painelMapa != null)
                    {
                        Vector2 localMousePosOnMap = painelMapa.WorldToLocal(evt.position);
                        OnMapClicked(localMousePosOnMap);
                    }
                    evt.StopPropagation();
                    return;
                }

                SelecionarUnidadeNoMenu(capturedCu);
                evt.StopPropagation();
            });
        }

        return new MapaItemUI
        {
            Root = container,
            Label = label,
            Marcador = marcador,
            Ring = ring,
            HpFill = hpFillEl,
            Controle = cu,
            Dano = sd,
            Identidade = id,
            UltimoDestruido = false
        };
    }

    private static bool EhImovelMapa(GameObject obj)
    {
        if (obj == null) return false;

        IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
        if ((identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo)
            || TemControladorAereoMapa(obj))
            return false;

        // Marcadores de imovel devem pertencer a esta unidade. Procurar em
        // toda a hierarquia fazia um aviao estacionado sob uma base/heranca
        // civil virar um ponto de casa no satelite.
        if (obj.GetComponent<Imovel>() != null
            || obj.GetComponent<Fazenda>() != null
            || TagSafe.Matches(obj, "Imovel"))
            return true;

        if (identidade != null && identidade.tipoUnidade != TipoUnidade.Estrutura)
            return false;

        // Algumas cenas colocam a identidade em um filho do prefab; nesse
        // caso, estruturas residenciais legadas podem manter o Imovel na
        // raiz ou no visual filho.
        for (Transform atual = obj.transform; atual != null; atual = atual.parent)
        {
            if (TagSafe.Matches(atual, "Imovel") || atual.GetComponent<Imovel>() != null)
                return true;
        }

        Transform[] filhos = obj.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            if (TagSafe.Matches(filhos[i], "Imovel") || filhos[i].GetComponent<Imovel>() != null)
                return true;
        }

        return false;
    }

    private static bool TemControladorAereoMapa(GameObject obj)
    {
        if (obj == null) return false;

        return obj.GetComponent<ControleAviao>() != null
            || obj.GetComponent<ControleAviaoCaca>() != null
            || obj.GetComponent<ControleAviaoComercial>() != null
            || obj.GetComponent<ControleAviaoAC130>() != null
            || obj.GetComponent<Helicoptero>() != null
            || obj.GetComponent<VooHelicoptero>() != null
            || obj.GetComponent<C700TransporteAereo>() != null
            || obj.GetComponentInChildren<ControleAviao>(true) != null
            || obj.GetComponentInChildren<ControleAviaoCaca>(true) != null
            || obj.GetComponentInChildren<ControleAviaoComercial>(true) != null
            || obj.GetComponentInChildren<ControleAviaoAC130>(true) != null
            || obj.GetComponentInChildren<Helicoptero>(true) != null
            || obj.GetComponentInChildren<VooHelicoptero>(true) != null
            || obj.GetComponentInChildren<C700TransporteAereo>(true) != null;
    }

    private static bool EhFazendaMapa(GameObject obj)
    {
        if (obj == null) return false;

        return obj.GetComponent<Fazenda>() != null
            || obj.GetComponentInParent<Fazenda>() != null
            || obj.GetComponentInChildren<Fazenda>(true) != null;
    }

    private static bool TemComponenteNaHierarquia<T>(GameObject obj) where T : Component
    {
        if (obj == null) return false;

        return obj.GetComponent<T>() != null
            || obj.GetComponentInParent<T>() != null
            || obj.GetComponentInChildren<T>(true) != null;
    }

    private string ObterEmojiUnidade(IdentidadeUnidade id)
    {
        if (id == null)
            return "?";

        // O controlador real prevalece sobre tipoUnidade mal configurado em
        // prefabs antigos (por exemplo, caças salvos como Infantaria).
        if (id.tipoUnidade == TipoUnidade.Aereo || TemControladorAereoMapa(id.gameObject))
            return "✈️";

        // Imóveis ficam como pontos discretos no satélite. Eles continuam
        // encontráveis como alvo, mas não competem visualmente com as tropas.
        if (EhImovelMapa(id.gameObject))
            return "·";

        // Logística naval precisa ser legível sem confundir com navio de
        // combate: petroleiro mostra combustível e transportes mostram âncora.
        if (TemComponenteNaHierarquia<NavioPetroleiro>(id.gameObject))
            return "⛽";

        if (id.GetComponent<Hegemonia.Cartel.CartelNavalUnidade>() != null)
        {
            return "⚔";
        }

        if (NavalPlacementResolver.IsLogisticsVessel(id.gameObject))
        {
            return "⚓";
        }

        if (id.tipoUnidade == TipoUnidade.Naval)
        {
            return "⚔";
        }

        switch (id.tipoUnidade)
        {
            case TipoUnidade.Aereo:     return "✈️";
            case TipoUnidade.Naval:     return "🚢";
            case TipoUnidade.Veiculo:   return "🚜";
            case TipoUnidade.Estrutura: return "🏭";
            default:                    return "🪖";
        }
    }

    // -----------------------------------------------------------------------
    // Seleção de unidade no mapa do menu
    // -----------------------------------------------------------------------
    private void SelecionarUnidadeNoMenu(ControleUnidade cu)
    {
        if (cu == null)
        {
            unidadesSelecionadasMenu.Clear();
            unidadeSelecionadaMenu = null;
            AtualizarCacheSelecaoIds();
            if (flirUnidadeNome != null) flirUnidadeNome.text = "SEM SINAL";
            if (flirAlerta != null) flirAlerta.text = "FLIR OFF-LINE";
            if (ordemFeedback != null) ordemFeedback.text = "Nenhuma unidade selecionada — clique no mapa";
            if (CameraUnidadeHUD.Instanciada) CameraUnidadeHUD.Instancia.DefinirTarget(null);
            SalvarSelecaoPersistida();
            AtualizarTelemetriaUnidade();
            return;
        }

        // Alterna a seleção da unidade
        if (unidadesSelecionadasMenu.Contains(cu))
        {
            unidadesSelecionadasMenu.Remove(cu);
            // Se a unidade removida era a em foco (unidadeSelecionadaMenu), foca na última restante
            if (unidadeSelecionadaMenu == cu)
            {
                unidadeSelecionadaMenu = unidadesSelecionadasMenu.Count > 0 ? 
                    unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1] : null;
            }
            AdicionarLog("OPS", $"Unidade desmarcada: {ObterNomeExibicao(cu.gameObject)} (Total: {unidadesSelecionadasMenu.Count})", "normal");
        }
        else
        {
            unidadesSelecionadasMenu.Add(cu);
            unidadeSelecionadaMenu = cu; // Foca na mais recente
            AdicionarLog("OPS", $"Unidade selecionada: {ObterNomeExibicao(cu.gameObject)} (Total: {unidadesSelecionadasMenu.Count})", "normal");
        }

        AtualizarCacheSelecaoIds();

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu, true);

        // Atualiza labels de foco
        if (flirUnidadeNome != null)
            flirUnidadeNome.text = unidadeSelecionadaMenu != null ? ObterNomeExibicao(unidadeSelecionadaMenu.gameObject) : "SEM SINAL";

        if (flirAlerta != null)
            flirAlerta.text = unidadeSelecionadaMenu != null ? "TRACKING: " + ObterNomeExibicao(unidadeSelecionadaMenu.gameObject) : "FLIR OFF-LINE";

        if (ordemFeedback != null)
        {
            if (unidadesSelecionadasMenu.Count > 0)
                ordemFeedback.text = $"{unidadesSelecionadasMenu.Count} unidade(s) selecionada(s) — escolha uma ordem";
            else
                ordemFeedback.text = "Nenhuma unidade selecionada — clique no mapa";
        }

        AtualizarTelemetriaUnidade();
        SalvarSelecaoPersistida();
    }

    private string ObterNomeExibicao(GameObject obj)
    {
        if (obj == null) return "DESCONHECIDO";
        var id = obj.GetComponent<IdentidadeUnidade>();
        string nome = id != null && !string.IsNullOrWhiteSpace(id.nomeDeBatismo)
            ? id.nomeDeBatismo.Trim()
            : SaveableEntity.NormalizarPrefabKey(obj.name);
        return $"{ObterCategoriaExibicao(obj, id)} — {nome}".ToUpperInvariant();
    }

    private string ObterCategoriaExibicao(GameObject obj, IdentidadeUnidade id)
    {
        if (obj == null) return "UNIDADE";

        if (obj.GetComponent<Estaleiro>() != null) return "ESTALEIRO";
        if (obj.GetComponent<GerenciadorAeroportoComercial>() != null) return "AEROPORTO COMERCIAL";
        if (obj.GetComponent<GerenciadorAeroporto>() != null) return "AEROPORTO";
        if (obj.GetComponent<Heliporto>() != null) return "HELIPORTO";
        if (obj.GetComponent<PlataformaOffshore>() != null) return "PLATAFORMA";
        if (obj.GetComponent<PierMarinha>() != null) return "PIER";
        if (obj.GetComponent<ComplexoGovernamental>() != null) return "PREFEITURA";
        if (obj.GetComponent<SiloNuclear>() != null) return "SILO";
        if (obj.GetComponent<Fabrica>() != null) return "FÁBRICA";
        if (obj.GetComponent<Fazenda>() != null) return "FAZENDA";
        if (obj.GetComponent<Imovel>() != null) return "IMÓVEL";

        if (id != null)
        {
            switch (id.tipoUnidade)
            {
                case TipoUnidade.Aereo:
                    if (obj.GetComponent<Helicoptero>() != null) return "HELICÓPTERO";
                    if (obj.GetComponent<ControleAviaoComercial>() != null) return "AVIÃO COMERCIAL";
                    return "CAÇA";
                case TipoUnidade.Naval: return "UNIDADE NAVAL";
                case TipoUnidade.Veiculo:
                case TipoUnidade.Infantaria: return "UNIDADE TERRESTRE";
                case TipoUnidade.Estrutura: return "ESTRUTURA";
            }
        }

        return "UNIDADE";
    }

    private void AbrirPainelSeguimento()
    {
        AtualizarDrawerSeguimento(true);
    }

    private void CancelarModoSeguir()
    {
        if (desenhadorOrdens == null)
        {
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();
        }

        if (desenhadorOrdens != null && desenhadorOrdens.modoSeguirAtivo)
        {
            desenhadorOrdens.CancelarModo();
        }

        alvoSeguimentoSelecionado = null;
        FecharPainelSeguimento();
        AtualizarEstadoSeguimento();
        SetText(ordemFeedback, "Seguir cancelado.");
    }

    private void FecharPainelSeguimento()
    {
        if (painelSeguir != null)
        {
            painelSeguir.style.display = DisplayStyle.None;
        }
    }

    private void AtualizarDrawerSeguimento(bool forcarVisivel = false)
    {
        if (painelSeguir == null)
        {
            return;
        }

        bool drawerJaAberto = painelSeguir.style.display.value == DisplayStyle.Flex;
        bool ativo = forcarVisivel || drawerJaAberto || (desenhadorOrdens != null && desenhadorOrdens.modoSeguirAtivo);
        painelSeguir.style.display = ativo ? DisplayStyle.Flex : DisplayStyle.None;
        if (!ativo)
        {
            return;
        }

        AtualizarEstadoSeguimento();
        RecarregarListaSeguimento();
    }

    private void AtualizarEstadoSeguimento()
    {
        if (seguirStatus == null)
        {
            return;
        }

        string alvoAtual = alvoSeguimentoSelecionado != null ? ObterNomeExibicao(alvoSeguimentoSelecionado) : "SEM ALVO";
        seguirStatus.text = $"SELECIONADO: {alvoAtual} | DIST: {distanciaSeguimentoAtual:0}m | SPACE confirma a mira";
        AtualizarBotoesDistanciaSeguimento();
    }

    private void AtualizarBotoesDistanciaSeguimento()
    {
        AtualizarBotaoDistanciaSeguimento(btnSeguir100, 100f);
        AtualizarBotaoDistanciaSeguimento(btnSeguir200, 200f);
        AtualizarBotaoDistanciaSeguimento(btnSeguir2000, 2000f);
        AtualizarBotaoDistanciaSeguimento(btnSeguir5000, 5000f);
    }

    private void AtualizarBotaoDistanciaSeguimento(Button botao, float distancia)
    {
        if (botao == null)
        {
            return;
        }

        if (Mathf.Abs(distanciaSeguimentoAtual - distancia) < 0.5f)
        {
            botao.AddToClassList("ativo");
        }
        else
        {
            botao.RemoveFromClassList("ativo");
        }
    }

    private void DefinirDistanciaSeguimento(float distancia)
    {
        distanciaSeguimentoAtual = Mathf.Clamp(distancia, 25f, 10000f);
        AtualizarEstadoSeguimento();

        if (desenhadorOrdens == null)
        {
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();
        }

        if (desenhadorOrdens != null)
        {
            desenhadorOrdens.DefinirDistanciaSeguimento(distanciaSeguimentoAtual);
        }

        if (alvoSeguimentoSelecionado != null)
        {
            ConfirmarSeguimentoEspecifico(alvoSeguimentoSelecionado);
        }
    }

    private void RecarregarListaSeguimento()
    {
        if (seguirLista == null)
        {
            return;
        }

        seguirLista.Clear();
        alvosSeguirUI.Clear();
        itemSeguimentoDestacado = null;

        AtualizarCacheEntidadesSeNecessario();

        List<IdentidadeUnidade> candidatos = new List<IdentidadeUnidade>(cacheUnidadesMapa.Count);
        for (int i = 0; i < cacheUnidadesMapa.Count; i++)
        {
            IdentidadeUnidade id = cacheUnidadesMapa[i];
            if (!EhAlvoSeguivel(id))
            {
                continue;
            }

            candidatos.Add(id);
        }

        Vector3 referencia = unidadeSelecionadaMenu != null
            ? unidadeSelecionadaMenu.transform.position
            : (CameraUnidadeHUD.Instancia != null ? CameraUnidadeHUD.Instancia.transform.position : Vector3.zero);

        candidatos.Sort((a, b) =>
        {
            float da = Vector3.Distance(a.transform.position, referencia);
            float db = Vector3.Distance(b.transform.position, referencia);
            return da.CompareTo(db);
        });
        if (alvoSeguimentoSelecionado != null && !candidatos.Exists(c => c != null && c.gameObject == alvoSeguimentoSelecionado))
        {
            alvoSeguimentoSelecionado = null;
        }

        if (candidatos.Count == 0)
        {
            var vazio = new Label("Sem alvos aliados visíveis");
            vazio.AddToClassList("seguir-status");
            seguirLista.Add(vazio);
            return;
        }

        if (alvoSeguimentoSelecionado == null)
        {
            alvoSeguimentoSelecionado = candidatos[0].gameObject;
        }

        for (int i = 0; i < candidatos.Count; i++)
        {
            IdentidadeUnidade id = candidatos[i];
            GameObject alvo = id.gameObject;
            alvosSeguirUI.Add(alvo);

            float distancia = Vector3.Distance(alvo.transform.position, referencia);

            Button item = new Button();
            item.AddToClassList("seguir-item");
            if (alvoSeguimentoSelecionado == alvo)
            {
                item.AddToClassList("ativo");
                itemSeguimentoDestacado = item;
            }

            item.clicked += () => ConfirmarSeguimentoEspecifico(alvo);

            var nome = new Label(ObterNomeExibicao(alvo));
            nome.AddToClassList("seguir-item-nome");

            var meta = new Label($"{id.tipoUnidade.ToString().ToUpperInvariant()} | {distancia:F0}m");
            meta.AddToClassList("seguir-item-meta");

            item.Add(nome);
            item.Add(meta);
            seguirLista.Add(item);
        }

        if (itemSeguimentoDestacado != null)
        {
            seguirScroll?.ScrollTo(itemSeguimentoDestacado);
            AnimarItemSeguimento(itemSeguimentoDestacado);
        }
    }

    private bool EhAlvoSeguivel(IdentidadeUnidade id)
    {
        if (id == null || !id.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (EhAviaoComercialNoSatelite(id.gameObject))
        {
            return false;
        }

        if (!EhUnidadeDoJogador(id))
        {
            return false;
        }

        SistemaDeDanos sd = id.GetComponent<SistemaDeDanos>();
        if (sd != null && sd.vidaMaxima > 0f && sd.vidaAtual <= 0f)
        {
            return false;
        }

            ControleUnidade controle = ObterControleTatico(id);
        if (controle != null && unidadesSelecionadasIds.Contains(controle.GetInstanceID()))
        {
            return false;
        }

        return true;
    }

    private bool ConfirmarSeguimentoAtivo()
    {
        if (CameraUnidadeHUD.Instancia != null)
        {
            GameObject looked = CameraUnidadeHUD.Instancia.GetLookedTarget();
            if (looked != null && ConfirmarSeguimentoEspecifico(looked))
            {
                return true;
            }
        }

        if (alvoSeguimentoSelecionado != null && ConfirmarSeguimentoEspecifico(alvoSeguimentoSelecionado))
        {
            return true;
        }

        if (alvosSeguirUI.Count > 0)
        {
            return ConfirmarSeguimentoEspecifico(alvosSeguirUI[0]);
        }

        SetText(ordemFeedback, "⚠ Nenhum alvo válido para seguir.");
        return false;
    }

    private bool AplicarSeguimentoSelecionado(Transform alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        bool ordemEmitida = false;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null || unidade.transform == alvo)
            {
                continue;
            }

            ordemEmitida |= unidade.EmitirOrdemSeguir(alvo, distanciaSeguimentoAtual);
        }

        return ordemEmitida;
    }

    private bool ConfirmarSeguimentoEspecifico(GameObject alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        if (unidadesSelecionadasMenu.Count == 0)
        {
            return false;
        }

        IdentidadeUnidade id = alvo.GetComponent<IdentidadeUnidade>();
        if (!EhAlvoSeguivel(id))
        {
            return false;
        }

        alvoSeguimentoSelecionado = alvo;

        if (AplicarSeguimentoSelecionado(alvo.transform))
        {
            if (desenhadorOrdens == null)
            {
                desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();
            }

            if (desenhadorOrdens != null && desenhadorOrdens.modoSeguirAtivo)
            {
                desenhadorOrdens.CancelarModo();
            }

            AtualizarCameraSeguimento(alvo);

            if (CameraUnidadeHUD.Instancia != null)
            {
                CameraUnidadeHUD.Instancia.modoDroneCamera = true;
            }

            AtualizarEstadoSeguimento();
            RecarregarListaSeguimento();
            SetText(ordemFeedback, $"✔ SEGUIR: {ObterNomeExibicao(alvo)} @ {distanciaSeguimentoAtual:0}m");
            AdicionarLog("OPS", $"Seguir alvo {ObterNomeExibicao(alvo)} confirmado a {distanciaSeguimentoAtual:0}m.", "normal");
            return true;
        }

        SetText(ordemFeedback, "⚠ Falha ao aplicar ordem de seguir.");
        return false;
    }

    private void SelecionarPrimeiraUnidadeAliada()
    {
        AtualizarCacheEntidadesSeNecessario(true);

        foreach (var id in cacheUnidadesMapa)
        {
            if (EhUnidadeDoJogador(id)
                && !EhAviaoComercialNoSatelite(id.gameObject))
            {
                var cu = ObterControleTatico(id, true);
                if (cu != null)
                {
                    SelecionarUnidadeNoMenu(cu);
                    SalvarSelecaoPersistida();
                    return;
                }
            }
        }
    }

    private void DesselecionarUnidadeEmFoco()
    {
        if (unidadeSelecionadaMenu == null && unidadesSelecionadasMenu.Count > 0)
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1];
        }

        if (unidadeSelecionadaMenu != null)
        {
            string nomeRemovido = ObterNomeExibicao(unidadeSelecionadaMenu.gameObject);
            SelecionarUnidadeNoMenu(unidadeSelecionadaMenu);
            AdicionarLog("OPS", $"Unidade desmarcada pelo menu: {nomeRemovido}.", "normal");
            return;
        }

        if (ordemFeedback != null)
            ordemFeedback.text = "Nenhuma unidade selecionada no mapa";
        AdicionarLog("OPS", "Nenhuma unidade em foco para desselecionar.", "normal");
    }

    private void SelecionarTodasUnidadesAliadas()
    {
        unidadesSelecionadasMenu.Clear();
        unidadeSelecionadaMenu = null;

        AtualizarCacheEntidadesSeNecessario(true);
        for (int i = 0; i < cacheUnidadesMapa.Count; i++)
        {
            var id = cacheUnidadesMapa[i];
            if (EhUnidadeDoJogador(id)
                && !EhAviaoComercialNoSatelite(id.gameObject))
            {
                var cu = ObterControleTatico(id, true);
                if (cu != null)
                {
                    unidadesSelecionadasMenu.Add(cu);
                    unidadeSelecionadaMenu = cu; // Foca na última
                }
            }
        }

        AtualizarCacheSelecaoIds();

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu, true);

        // Atualiza labels de foco
        if (flirUnidadeNome != null)
            flirUnidadeNome.text = unidadeSelecionadaMenu != null ? ObterNomeExibicao(unidadeSelecionadaMenu.gameObject) : "SEM SINAL";

        if (flirAlerta != null)
            flirAlerta.text = unidadeSelecionadaMenu != null ? "TRACKING: " + ObterNomeExibicao(unidadeSelecionadaMenu.gameObject) : "FLIR OFF-LINE";

        if (ordemFeedback != null)
        {
            if (unidadesSelecionadasMenu.Count > 0)
                ordemFeedback.text = $"Todas as {unidadesSelecionadasMenu.Count} unidade(s) selecionada(s) — escolha uma ordem";
            else
                ordemFeedback.text = "Nenhuma unidade aliada ativa no mapa";
        }

        AdicionarLog("OPS", $"Selecionadas todas as {unidadesSelecionadasMenu.Count} unidades aliadas.", "normal");
        AtualizarTelemetriaUnidade();
        SalvarSelecaoPersistida();
    }

    private void CiclarUnidadeSelecionada()
    {
        if (unidadesSelecionadasMenu.Count <= 1) return;
        
        int indexAtual = unidadesSelecionadasMenu.IndexOf(unidadeSelecionadaMenu);
        if (indexAtual == -1) indexAtual = 0;
        
        indexAtual = (indexAtual + 1) % unidadesSelecionadasMenu.Count;
        unidadeSelecionadaMenu = unidadesSelecionadasMenu[indexAtual];
        AtualizarCacheSelecaoIds();
        
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu, true);
            
        AtualizarTelemetriaUnidade();
        SalvarSelecaoPersistida();
    }

    // -----------------------------------------------------------------------
    // Telemetria
    // -----------------------------------------------------------------------
    private void AtualizarTelemetriaUnidade()
    {
        unidadesSelecionadasMenu.RemoveAll(u => u == null);
        AtualizarCacheSelecaoIds();
        AtualizarDisponibilidadePatrulha();
        if (unidadeSelecionadaMenu == null && unidadesSelecionadasMenu.Count > 0)
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[0];
        }

        if (unidadesSelecionadasMenu.Count == 0)
        {
            if (btnDroneCam != null) btnDroneCam.style.display = DisplayStyle.None;
            if (CameraUnidadeHUD.Instancia != null) CameraUnidadeHUD.Instancia.modoDroneCamera = false;
            unidadeSelecionadaMenu = null;
            SetText(unidadeNome, "NENHUMA");
            SetText(unidadeEmoji, "❓");
            SetText(statTipo, "—");
            SetText(statStatus, "—");
            SetText(statPos, "—");
            SetText(statArmas, "—");
            AtualizarTextoMisseisEmVoo("—");
            SetText(statTeam, "—");
            SetText(hpValor, "—%");
            SetText(fuelValor, "—%");
            SetBarWidth(hpBar, 0f);
            SetBarWidth(fuelBar, 0f);
            
            // Restaura texto padrão do FLIR se não há unidade
            if (flirTl != null) flirTl.text = "FLIR / AUTO-TRK";
            if (flirTr != null) flirTr.text = "ZOOM: 1.0X (14%)";
            if (flirZoomSlider != null) flirZoomSlider.SetValueWithoutNotify(14f);
            return;
        }
        else if (unidadesSelecionadasMenu.Count > 1)
        {
            if (btnDroneCam != null) btnDroneCam.style.display = DisplayStyle.None;
            if (CameraUnidadeHUD.Instancia != null) CameraUnidadeHUD.Instancia.modoDroneCamera = false;
            SetText(unidadeNome, $"MÚLTIPLAS ({unidadesSelecionadasMenu.Count})");
            SetText(unidadeEmoji, "👥");
            SetText(statTipo, "MISTO");
            SetText(statStatus, "VÁRIOS");
            SetText(statPos, "MÚLTIPLAS");
            SetText(statArmas, "VÁRIAS");
            AtualizarTextoMisseisEmVoo("VÁRIAS");
            SetText(statTeam, "ALIADO");

            float somaHp = 0f;
            float maxHp = 0f;
            float somaFuel = 0f;
            float maxFuel = 0f;

            foreach (var u in unidadesSelecionadasMenu)
            {
                if (u == null) continue;
                SistemaDeDanos sd = u.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaMaxima > 0f)
                {
                    somaHp += sd.vidaAtual;
                    maxHp += sd.vidaMaxima;
                }

                CombustivelUnidade cbu = u.GetComponent<CombustivelUnidade>();
                if (cbu != null && cbu.Capacidade > 0f)
                {
                    somaFuel += cbu.CombustivelAtual;
                    maxFuel += cbu.Capacidade;
                }
            }

            float hpPct = maxHp > 0f ? Mathf.Clamp01(somaHp / maxHp) : 1f;
            int hpInt = Mathf.RoundToInt(hpPct * 100f);
            SetText(hpValor, $"{hpInt}%");
            SetBarWidth(hpBar, hpPct);

            if (hpBar != null)
            {
                hpBar.style.backgroundColor = hpInt > 60 ? new Color(0f, 0.9f, 1f) : hpInt > 25 ? new Color(1f, 0.67f, 0f) : new Color(1f, 0.2f, 0.2f);
            }

            if (maxFuel > 0f)
            {
                float fuelPct = Mathf.Clamp01(somaFuel / maxFuel);
                int fuelInt = Mathf.RoundToInt(fuelPct * 100f);
                SetText(fuelValor, $"{fuelInt}%");
                SetBarWidth(fuelBar, fuelPct);
            }
            else
            {
                SetText(fuelValor, "N/A");
                SetBarWidth(fuelBar, 1f);
            }

            if (flirTc != null) flirTc.text = "HDG MÚLTIPLOS";
            
            // Restaura texto padrão do FLIR se múltipla seleção
            if (flirTl != null) flirTl.text = "FLIR / AUTO-TRK";
            if (flirTr != null) flirTr.text = "ZOOM: 1.0X (14%)";
            if (flirZoomSlider != null) flirZoomSlider.SetValueWithoutNotify(14f);
            return;
        }

        ControleUnidade cu = unidadeSelecionadaMenu;

        // Nome
        SetText(unidadeNome, ObterNomeExibicao(cu.gameObject));

        // Emoji + tipo
        IdentidadeUnidade id = cu.GetComponent<IdentidadeUnidade>();
        if (id != null)
        {
            SetText(unidadeEmoji, ObterEmojiUnidade(id));
            SetText(statTipo, ObterCategoriaExibicao(cu.gameObject, id));
            SetText(statTeam, EhUnidadeDoJogador(id) ? "ALIADO" : "INIMIGO");
        }

        // Posição
        Vector3 p = cu.transform.position;
        SetText(statPos, $"{p.x:F0}, {p.z:F0}");

        // Armas
        string textoArmas = "N/A";
        var lmCaca = cu.GetComponentInChildren<LancadorMisselCaca>(true);
        var lmNaval = cu.GetComponentInChildren<LancadorNaval>(true);
        var lmSolo = cu.GetComponentInChildren<LancadorMisseis>(true);
        
        if (lmCaca != null)
            textoArmas = $"MSL: {lmCaca.municaoAtual}/{lmCaca.municaoMaxima}";
        else if (lmNaval != null)
            textoArmas = $"MSL: {lmNaval.municaoTotal}/{lmNaval.municaoMaxima} | TORP: {lmNaval.torpedosTotal}/{lmNaval.torpedosMaximos}";
        else if (lmSolo != null)
            textoArmas = $"MSL: {lmSolo.municaoAtual}/{lmSolo.municaoMaxima}";
            
        if (cu.EhUnidadeNaval())
            textoArmas = MontarStatusArmasNavio(cu, textoArmas);
        AtualizarTextoMisseisEmVoo(textoArmas);

        // Status
        bool passivo;
        string descStatus;
        if (cu.TryObterEstadoCombate(out passivo, out descStatus))
            SetText(statStatus, MontarStatusNavio(cu, passivo, descStatus));
        else
            SetText(statStatus, "OK");

        // HP — via SistemaDeDanos
        float hpPctSingle = 1f;
        SistemaDeDanos sdSingle = cu.GetComponent<SistemaDeDanos>();
        if (sdSingle != null && sdSingle.vidaMaxima > 0f)
            hpPctSingle = Mathf.Clamp01(sdSingle.vidaAtual / sdSingle.vidaMaxima);
        int hpIntSingle = Mathf.RoundToInt(hpPctSingle * 100f);
        SetText(hpValor, $"{hpIntSingle}%");
        SetBarWidth(hpBar, hpPctSingle);

        // Cor da barra de HP
        if (hpBar != null)
        {
            hpBar.style.backgroundColor =
                hpIntSingle > 60 ? new Color(0f, 0.9f, 1f) :
                hpIntSingle > 25 ? new Color(1f, 0.67f, 0f) :
                             new Color(1f, 0.2f, 0.2f);
        }

        // Combustível
        CombustivelUnidade cbuSingle = cu.GetComponent<CombustivelUnidade>();
        if (cbuSingle != null && cbuSingle.Capacidade > 0f)
        {
            float fuelPct = Mathf.Clamp01(cbuSingle.CombustivelAtual / cbuSingle.Capacidade);
            int fuelInt   = Mathf.RoundToInt(fuelPct * 100f);
            SetText(fuelValor, $"{fuelInt}%");
            SetBarWidth(fuelBar, fuelPct);
        }
        else
        {
            SetText(fuelValor, "N/A");
            SetBarWidth(fuelBar, 1f);
        }

        // FLIR HDG
        if (flirTc != null)
        {
            float hdg = cu.transform.eulerAngles.y;
            flirTc.text = $"HDG {hdg:F1}°";
        }

        // Ocultar btnDroneCam antigo, agora usamos o botão CÂMERA nas ordens
        if (btnDroneCam != null)
        {
            btnDroneCam.style.display = DisplayStyle.None;
        }

        // Atualiza textos do overlay FLIR baseado no estado da câmera do drone
        if (CameraUnidadeHUD.Instancia != null && CameraUnidadeHUD.Instancia.modoDroneCamera && cu != null)
        {
            if (flirTl != null) flirTl.text = "DRONE CAM / GIMBAL";
            
            float zoomX = CameraUnidadeHUD.Instancia.zoomFactor;
            float zoomPercent = Mathf.Clamp((zoomX - 0.06f) / (18.0f - 0.06f) * 100f, 0f, 100f);
            if (flirTr != null) flirTr.text = $"ZOOM: {zoomX:F1}X ({zoomPercent:F0}%)";
            if (flirZoomSlider != null) flirZoomSlider.SetValueWithoutNotify(zoomPercent);
            
            GameObject lookedTarget = CameraUnidadeHUD.Instancia.GetLookedTarget();
            if (lookedTarget != null)
            {
                if (flirUnidadeNome != null) flirUnidadeNome.text = $"LOCK: {ObterNomeExibicao(lookedTarget)}";
                if (flirAlerta != null) flirAlerta.text = "🎯 ALVO NA MIRA (CLIQUE/ESPAÇO PARA TRAVAR)";
            }
            else
            {
                if (flirUnidadeNome != null) flirUnidadeNome.text = "BUSCANDO ALVO...";
                if (flirAlerta != null) flirAlerta.text = "ÁREA DE OPERAÇÃO - HUD ATIVO";
            }
        }
        else
        {
            if (flirTl != null) flirTl.text = "FLIR / AUTO-TRK";
            
            float zoomX = CameraUnidadeHUD.Instancia != null ? CameraUnidadeHUD.Instancia.zoomFactor : 1f;
            float zoomPercent = Mathf.Clamp((zoomX - 0.06f) / (18.0f - 0.06f) * 100f, 0f, 100f);
            if (flirTr != null) flirTr.text = $"ZOOM: {zoomX:F1}X ({zoomPercent:F0}%)";
            if (flirZoomSlider != null) flirZoomSlider.SetValueWithoutNotify(zoomPercent);
            
            if (flirUnidadeNome != null)
                flirUnidadeNome.text = cu != null ? ObterNomeExibicao(cu.gameObject) : "— SEM ALVO —";
            
            if (flirAlerta != null)
                flirAlerta.text = cu != null ? "TRACKING: " + ObterNomeExibicao(cu.gameObject) : "FLIR OFF-LINE";
        }

        AtualizarDrawerSeguimento();
    }

    private string MontarStatusNavio(ControleUnidade unidade, bool passivo, string descricao)
    {
        string estado = string.IsNullOrWhiteSpace(descricao)
            ? (passivo ? "PASSIVO" : "ATIVO")
            : descricao;
        if (unidade == null || !unidade.EhUnidadeNaval()) return estado;

        ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>(true);
        ControleTorretaModular[] modulares = unidade.GetComponentsInChildren<ControleTorretaModular>(true);
        int total = 0;
        int ativas = 0;
        string modos = string.Empty;

        for (int i = 0; i < torretas.Length; i++)
        {
            if (torretas[i] == null) continue;
            total++;
            if (!torretas[i].modoPassivo) ativas++;
            if (modos.Length < 24) modos += torretas[i].modoPassivo ? "P" : "A";
        }

        for (int i = 0; i < modulares.Length; i++)
        {
            if (modulares[i] == null) continue;
            total++;
            if (!modulares[i].modoPassivo) ativas++;
            if (modos.Length < 24) modos += modulares[i].modoPassivo ? "P" : "A";
        }

        if (total == 0) return estado;
        return $"{estado} · TORRETAS {ativas}/{total} ATIVAS ({modos})";
    }

    private string MontarStatusArmasNavio(ControleUnidade unidade, string textoBase)
    {
        if (unidade == null) return textoBase;

        string texto = string.Empty;
        LancadorNaval lancador = unidade.GetComponentInChildren<LancadorNaval>(true);
        ControleNavioRealista navio = unidade.GetComponentInChildren<ControleNavioRealista>(true);
        if (lancador != null)
        {
            string modoLancador = lancador.modoAtual.ToString().ToUpperInvariant();
            string recarga = lancador.EstaRecarregando
                ? $"RECARGA {lancador.TempoRecargaRestante:F0}s"
                : "PRONTO";
            int maximosMisseis = Mathf.Max(lancador.municaoTotal, lancador.municaoMaxima);
            int maximosTorpedos = Mathf.Max(lancador.torpedosTotal, lancador.torpedosMaximos);
            texto = $"MSL {lancador.municaoTotal}/{maximosMisseis} {modoLancador} · {recarga}\nTORP {lancador.torpedosTotal}/{maximosTorpedos}";

            if (navio != null && navio.TemSistemaTorpedosConfigurado())
            {
                int maximosTubo = Mathf.Max(navio.torpedosDisponiveis, navio.MaximoTorpedosConfigurado);
                texto += $"\nTUBOS {navio.torpedosDisponiveis}/{maximosTubo}";
            }
        }
        else
        {
            if (navio != null && navio.TemSistemaTorpedosConfigurado())
            {
                int maximos = Mathf.Max(navio.torpedosDisponiveis, navio.MaximoTorpedosConfigurado);
                texto = $"TORP {navio.torpedosDisponiveis}/{maximos}";
            }
        }

        SistemaAntiMissil[] sistemasAA = unidade.GetComponentsInChildren<SistemaAntiMissil>(true);
        int misseisAA = 0;
        int maximosAA = 0;
        bool recarregandoAA = false;
        bool modoAATemAtivo = false;
        bool modoAATemPassivo = false;
        for (int i = 0; i < sistemasAA.Length; i++)
        {
            SistemaAntiMissil sistema = sistemasAA[i];
            if (sistema == null) continue;
            misseisAA += sistema.ObterMisseisRestantesTotais();
            maximosAA += sistema.ObterMisseisMaximosTotais();
            recarregandoAA |= sistema.EstaRecarregando;
            modoAATemPassivo |= sistema.modoPassivo;
            modoAATemAtivo |= !sistema.modoPassivo;
        }

        if (sistemasAA.Length > 0)
        {
            string modoAA = modoAATemAtivo && modoAATemPassivo ? "MISTO" : (modoAATemPassivo ? "PASSIVO" : "ATIVO");
            string estadoAA = recarregandoAA ? "RECARREGANDO" : "PRONTO";
            if (texto.Length > 0) texto += "\n";
            texto += $"AA {misseisAA}/{maximosAA} {modoAA} · {estadoAA}";
        }

        if (texto.Length == 0) return textoBase;
        return texto;
    }

    private void AlternarModoCameraDrone()
    {
        if (CameraUnidadeHUD.Instancia == null)
        {
            if (contextoFeedback != null)
                contextoFeedback.text = TextoHud("feedback.dronecam.missing", "CÂMERA DE ACOMPANHAMENTO INDISPONÍVEL NESTA CENA.");
            return;
        }
        
        CameraUnidadeHUD.Instancia.modoDroneCamera = !CameraUnidadeHUD.Instancia.modoDroneCamera;
        
        if (CameraUnidadeHUD.Instancia.modoDroneCamera)
        {
            // Reset to default angle and zoom when entering drone cam mode
            CameraUnidadeHUD.Instancia.currentRotationX = 35f; // Point downward (35 degrees) to see terrains and units
            CameraUnidadeHUD.Instancia.currentRotationY = 0f;  // Look forward
            CameraUnidadeHUD.Instancia.zoomFactor = 2.57f;     // Default 14% zoom (around 1.0X)
        }
        
        string modoStr = CameraUnidadeHUD.Instancia.modoDroneCamera ? "CÂMERA INTERNA DRONE ACESSADA" : "RETORNADO À CÂMERA ORBITAL";
        AdicionarLog("DRONE", modoStr, "sistema");
        if (contextoFeedback != null)
            contextoFeedback.text = CameraUnidadeHUD.Instancia.modoDroneCamera
                ? TextoHud("feedback.dronecam.on", "CÂMERA DE ACOMPANHAMENTO ATIVADA.")
                : TextoHud("feedback.dronecam.off", "CÂMERA ORBITAL ATIVADA.");
        AtualizarTelemetriaUnidade();
    }

    // -----------------------------------------------------------------------
    // SITREP
    // -----------------------------------------------------------------------
    private void AtualizarSitrep()
    {
        AtualizarCacheEntidadesSeNecessario();

        int aliados  = 0;
        int inimigos = 0;

        foreach (var id in cacheUnidadesMapa)
        {
            if (EhAviaoComercialNoSatelite(id != null ? id.gameObject : null)) continue;
            if (!id.gameObject.activeInHierarchy) continue;
            if (EhUnidadeDoJogador(id)) aliados++;
            else if (id.teamID > 0) inimigos++;
        }

        SetText(sitrepAliados,  aliados.ToString());
        SetText(sitrepInimigos, inimigos.ToString());
        SetText(sitrepSel,      unidadeSelecionadaMenu != null ? ObterNomeExibicao(unidadeSelecionadaMenu.gameObject) : "—");

        // Velocidade
        if (unidadeSelecionadaMenu != null)
        {
            float velMps = 0f;
            ControleAviaoCaca caca = unidadeSelecionadaMenu.GetComponent<ControleAviaoCaca>();
            if (caca != null) velMps = caca.VelocidadeAtual;
            if (velMps <= 0.01f)
            {
                ControleAviao aviao = unidadeSelecionadaMenu.GetComponent<ControleAviao>();
                if (aviao != null) velMps = aviao.VelocidadeVooAtual;
            }
            if (velMps <= 0.01f)
            {
                ControleNavioRealista navio = unidadeSelecionadaMenu.GetComponent<ControleNavioRealista>();
                if (navio != null) velMps = navio.VelocidadeAtual;
            }
            Rigidbody rb = unidadeSelecionadaMenu.GetComponent<Rigidbody>();
            if (velMps <= 0.01f && rb != null) velMps = rb.linearVelocity.magnitude;
            else
            {
                UnityEngine.AI.NavMeshAgent nav = unidadeSelecionadaMenu.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (velMps <= 0.01f && nav != null) velMps = nav.velocity.magnitude;
            }
            velMps = ObterVelocidadeTelemetria(unidadeSelecionadaMenu.gameObject, velMps);
            SetText(sitrepVel, $"{velMps * 3.6f:F0} KM/H");

            CombustivelUnidade cbu = unidadeSelecionadaMenu.GetComponent<CombustivelUnidade>();
            if (cbu != null && cbu.Capacidade > 0f)
            {
                float fuelPct = Mathf.Clamp01(cbu.CombustivelAtual / cbu.Capacidade);
                SetText(sitrepFuel, $"{fuelPct * 100f:F0}%");
            }
            else
            {
                SetText(sitrepFuel, "N/A");
            }
        }
        else
        {
            SetText(sitrepVel, "0 KM/H");
            SetText(sitrepFuel, "N/A");
        }

        // Avaliação de ameaça
        string ameaca;
        if      (inimigos == 0)          ameaca = "NULA";
        else if (inimigos <= aliados / 2) ameaca = "BAIXA";
        else if (inimigos <= aliados)     ameaca = "MÉDIA";
        else if (inimigos <= aliados * 2) ameaca = "ALTA";
        else                              ameaca = "CRÍTICA";

        SetText(sitrepAmeaca, ameaca);
        if (sitrepAmeaca != null)
        {
            sitrepAmeaca.style.color =
                ameaca == "NULA"   ? new Color(0f, 0.9f, 0.4f) :
                ameaca == "BAIXA"  ? new Color(0.5f, 0.9f, 0f) :
                ameaca == "MÉDIA"  ? new Color(1f, 0.85f, 0f) :
                ameaca == "ALTA"   ? new Color(1f, 0.55f, 0f) :
                                     new Color(1f, 0.2f, 0.1f);
        }
    }

    private void AtualizarDisponibilidadePatrulha()
    {
        if (btnPatrulhar == null)
        {
            return;
        }

        bool existeUnidadeApta = false;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            if (UnidadeAptaParaPatrulha(unidadesSelecionadasMenu[i]))
            {
                existeUnidadeApta = true;
                break;
            }
        }

        btnPatrulhar.SetEnabled(existeUnidadeApta);
        btnPatrulhar.tooltip = existeUnidadeApta
            ? "Iniciar patrulha com unidades compatíveis"
            : "Cargueiros e unidades logísticas não realizam patrulha militar";
    }

    private float ObterVelocidadeTelemetria(GameObject unidade, float velocidadeMpsAtual)
    {
        if (unidade == null) return 0f;

        // Aviões e navios antigos podem se mover por transform, sem Rigidbody
        // nem NavMeshAgent. A amostra entre dois frames mantém a velocidade
        // real visível no SITREP nesses casos.
        if (velocidadeMpsAtual > 0.01f)
        {
            ultimaPosicaoTelemetria = unidade.transform.position;
            ultimoTempoTelemetria = Time.unscaledTime;
            ultimaUnidadeTelemetriaId = unidade.GetInstanceID();
            return velocidadeMpsAtual;
        }

        int id = unidade.GetInstanceID();
        float agora = Time.unscaledTime;
        if (ultimaUnidadeTelemetriaId == id && ultimoTempoTelemetria >= 0f)
        {
            float intervalo = agora - ultimoTempoTelemetria;
            if (intervalo > 0.001f)
            {
                float amostrada = Vector3.Distance(unidade.transform.position, ultimaPosicaoTelemetria) / intervalo;
                ultimaPosicaoTelemetria = unidade.transform.position;
                ultimoTempoTelemetria = agora;
                return amostrada;
            }
        }

        ultimaUnidadeTelemetriaId = id;
        ultimaPosicaoTelemetria = unidade.transform.position;
        ultimoTempoTelemetria = agora;
        return 0f;
    }

    private void AtualizarTextoMisseisEmVoo(string textoBase)
    {
        misseisEmVoo.Clear();
        idsMisseisAtivos.Clear();
        Transform[] objetos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        HashSet<int> vistos = new HashSet<int>();

        for (int i = 0; i < objetos.Length; i++)
        {
            Transform t = objetos[i];
            if (t == null) continue;
            GameObject raiz = t.root != null ? t.root.gameObject : t.gameObject;
            if (raiz == null || !vistos.Add(raiz.GetInstanceID())) continue;

            string tag = t.gameObject.tag ?? string.Empty;
            string nome = t.gameObject.name ?? string.Empty;
            bool tagMissel = tag.Equals("Missel", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("Missil", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("Missile", StringComparison.OrdinalIgnoreCase);
            bool nomeMissel = nome.IndexOf("missel", StringComparison.OrdinalIgnoreCase) >= 0
                || nome.IndexOf("missil", StringComparison.OrdinalIgnoreCase) >= 0
                || nome.IndexOf("missile", StringComparison.OrdinalIgnoreCase) >= 0;
            bool componenteMissel = raiz.GetComponentInChildren<MisselNaval>(true) != null
                || raiz.GetComponentInChildren<MisselCaca>(true) != null
                || raiz.GetComponentInChildren<MisselSubmarino>(true) != null
                || raiz.GetComponentInChildren<MisselICBM>(true) != null
                || raiz.GetComponentInChildren<MisselTatico>(true) != null
                || raiz.GetComponentInChildren<MisselLeopardAutomatico>(true) != null
                || raiz.GetComponentInChildren<MissilTeleguiado>(true) != null;
            if (!tagMissel && !nomeMissel && !componenteMissel) continue;

            int id = raiz.GetInstanceID();
            misseisEmVoo.Add(raiz);
            idsMisseisAtivos.Add(id);
            if (!origemMisseis.ContainsKey(id)) origemMisseis[id] = raiz.transform.position;
        }

        HashSet<int> ativos = new HashSet<int>(idsMisseisAtivos);
        List<int> antigos = new List<int>();
        foreach (KeyValuePair<int, Vector3> origem in origemMisseis)
        {
            if (!ativos.Contains(origem.Key)) antigos.Add(origem.Key);
        }
        for (int i = 0; i < antigos.Count; i++) origemMisseis.Remove(antigos[i]);

        string texto = string.IsNullOrWhiteSpace(textoBase) ? string.Empty : textoBase;
        texto += $"\nEM VOO: {misseisEmVoo.Count}";
        int limite = Mathf.Min(3, misseisEmVoo.Count);
        for (int i = 0; i < limite; i++)
        {
            GameObject missel = misseisEmVoo[i];
            if (missel == null) continue;
            Vector3 origem = origemMisseis[missel.GetInstanceID()];
            float deslocamento = Vector3.Distance(origem, missel.transform.position);
            texto += $"\n{missel.name}: Δ{deslocamento:F0}m";
        }
        if (misseisEmVoo.Count > limite) texto += $"\n+{misseisEmVoo.Count - limite} mísseis";
        SetText(statArmas, texto);
    }

    // -----------------------------------------------------------------------
    // Relógio
    // -----------------------------------------------------------------------
    private void AtualizarRelogio()
    {
        TimeSpan ts = TimeSpan.FromSeconds(tempoOperacao);
        string str  = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        SetText(sitrepTempo, str);
        SetText(headerTempo, $"T.OP: {str}");
    }

    // -----------------------------------------------------------------------
    // Ordens
    // -----------------------------------------------------------------------
    private void ExecutarOrdem(string ordem)
    {
        if (unidadesSelecionadasMenu.Count == 0)
        {
            SetText(ordemFeedback, "⚠ Nenhuma unidade selecionada no mapa!");
            return;
        }

        var snapshot = new List<GameObject>();
        foreach (var u in unidadesSelecionadasMenu)
        {
            if (u != null) snapshot.Add(u.gameObject);
        }

        if (snapshot.Count == 0)
        {
            SetText(ordemFeedback, "⚠ Nenhuma unidade selecionada no mapa!");
            return;
        }

        if (ordem == "PATRULHAR")
        {
            snapshot.RemoveAll(u => u == null || !UnidadeAptaParaPatrulha(u.GetComponent<ControleUnidade>()));
            if (snapshot.Count == 0)
            {
                SetText(ordemFeedback, "⚠ Nenhuma unidade selecionada possui patrulha compatível.");
                return;
            }
        }

        switch (ordem)
        {
            case "RADAR_ALTERNAR":
                bool ligarRadar = false;
                for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
                {
                    ControleUnidade unidade = unidadesSelecionadasMenu[i];
                    if (!UnidadePodeUsarRadar(unidade)) continue;
                    RadarUnidadeTatica radarAtual = unidade.GetComponent<RadarUnidadeTatica>();
                    if (radarAtual == null || !radarAtual.RadarLigado)
                    {
                        ligarRadar = true;
                        break;
                    }
                }

                int radaresAtualizados = 0;
                int semEnergia = 0;
                for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
                {
                    ControleUnidade unidade = unidadesSelecionadasMenu[i];
                    if (!UnidadePodeUsarRadar(unidade)) continue;
                    IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
                    if (identidade == null || identidade.teamID != TimeJogadorAtual) continue;
                    RadarUnidadeTatica radar = unidade.GetComponent<RadarUnidadeTatica>();
                    if (radar == null) radar = unidade.gameObject.AddComponent<RadarUnidadeTatica>();
                    radar.AtualizarAlcance(identidade);
                    radaresAtualizados++;
                    if (ligarRadar)
                    {
                        if (!radar.TentarLigarRadar(TimeJogadorAtual)) semEnergia++;
                    }
                    else
                    {
                        radar.DefinirRadar(false);
                    }
                }

                SetText(ordemFeedback, radaresAtualizados > 0
                    ? (ligarRadar
                        ? semEnergia > 0
                            ? $"⚠ Energia insuficiente: {semEnergia} unidade(s) não ligaram o radar. Custo: 1 energia ao ligar e por minuto."
                            : $"📡 Radar ligado em {radaresAtualizados} unidade(s). Emissão detectável pelo inimigo."
                        : $"📡 Radar desligado em {radaresAtualizados} unidade(s).")
                    : "⚠ Selecione unidade(s) aliada(s) para controlar o radar.");
                AdicionarLog("OPS", ligarRadar
                    ? semEnergia > 0 ? $"Energia insuficiente para {semEnergia} unidade(s) ligar(em) radar" : "Radar emissor ligado nas unidades selecionadas"
                    : "Radar emissor desligado nas unidades selecionadas", ligarRadar ? "alerta" : "normal");
                AtualizarBotaoRadarVisual();
                break;

            case "ATIVO":
                foreach (var u in unidadesSelecionadasMenu)
                {
                    if (u != null) u.DefinirModoCombate(true);
                }
                SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → MODO ATIVO");
                AdicionarLog("OPS", $"{snapshot.Count} unidades: modo ATIVO ativado", "normal");
                break;

            case "PASSIVO":
                foreach (var u in unidadesSelecionadasMenu)
                {
                    if (u != null) u.DefinirModoCombate(false);
                }
                SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → MODO PASSIVO");
                AdicionarLog("OPS", $"{snapshot.Count} unidades: modo PASSIVO ativado", "normal");
                break;

            case "ESTADO_ALTERNAR":
                List<string> estados = new List<string>();
                foreach (var u in unidadesSelecionadasMenu)
                {
                    if (u == null) continue;
                    string estado = u.AlternarEstadoOperacional();
                    if (!estados.Contains(estado)) estados.Add(estado);
                }
                SetText(ordemFeedback, $"ESTADO: {string.Join(" / ", estados)}");
                AdicionarLog("OPS", $"{snapshot.Count} unidades: estado alternado pela tecla I/menu", "normal");
                break;

            case "LANCAR_MISSIL":
                modoLancamentoMissilMapaAtivo = true;
                modoMoverMapaAtivo = false;
                if (desenhadorOrdens != null) desenhadorOrdens.CancelarModo();
                FecharPainelSeguimento();
                foreach (var u in unidadesSelecionadasMenu)
                {
                    if (u == null) continue;
                    SiloLancadorEstrategico silo = u.GetComponent<SiloLancadorEstrategico>();
                    if (silo != null) silo.ArmarMarcacaoAlvo();
                }
                SetText(ordemFeedback, "LANÇAMENTO ARMADO: clique no mapa para marcar a área de impacto.");
                AdicionarLog("OPS", $"{snapshot.Count} unidade(s): marcação de alvo estratégico iniciada", "alerta");
                break;

            case "MOVER":
                modoLancamentoMissilMapaAtivo = false;
                modoMoverMapaAtivo = true;
                if (desenhadorOrdens != null) desenhadorOrdens.CancelarModo();
                FecharPainelSeguimento();
                SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → MOVER\nClique no mapa para definir o destino. Navios serão ajustados para a água.");
                AdicionarLog("OPS", $"{snapshot.Count} unidade(s): ordem MOVER armada — clique no mapa", "normal");
                break;

            case "PATRULHAR":
                modoLancamentoMissilMapaAtivo = false;
                modoMoverMapaAtivo = false;
                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (desenhadorOrdens != null)
                {
                    desenhadorOrdens.IniciarModoPatrulha(snapshot);
                    SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → PATRULHANDO\nClique no mapa para marcar pontos. ENTER confirma. ESC ou Botão Direito cancela.");
                    AdicionarLog("OPS", $"{snapshot.Count} unidades: modo patrulha iniciado — clique no mapa", "normal");
                }
                else
                {
                    SetText(ordemFeedback, "⚠ Sistema de patrulha não encontrado");
                }
                break;

            case "SEGUIR":
                modoLancamentoMissilMapaAtivo = false;
                modoMoverMapaAtivo = false;
                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (desenhadorOrdens != null)
                {
                    desenhadorOrdens.IniciarModoSeguir(snapshot);
                    desenhadorOrdens.DefinirDistanciaSeguimento(distanciaSeguimentoAtual);
                    alvoSeguimentoSelecionado = null;
                    AbrirPainelSeguimento();
                    RecarregarListaSeguimento();
                    AtualizarEstadoSeguimento();
                    if (CameraUnidadeHUD.Instancia != null && unidadeSelecionadaMenu != null)
                    {
                        CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu, true);
                        CameraUnidadeHUD.Instancia.modoDroneCamera = true;
                    }
                    SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → SEGUIR\nEscolha um alvo na lista ou na mira. SPACE confirma.");
                    AdicionarLog("OPS", $"{snapshot.Count} unidades: modo seguir iniciado — escolha o alvo", "normal");
                }
                else
                {
                    SetText(ordemFeedback, "⚠ Sistema de ordens não encontrado");
                }
                break;

            case "ATACAR":
                modoLancamentoMissilMapaAtivo = false;
                modoMoverMapaAtivo = false;
                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (desenhadorOrdens != null)
                {
                    desenhadorOrdens.IniciarModoAtaque(snapshot);
                    SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → ATAQUE\nClique no alvo ou área no mapa. ESC ou Botão Direito cancela.");
                    AdicionarLog("OPS", $"{snapshot.Count} unidades: modo ataque iniciado — clique no alvo/área", "alerta");
                }
                else
                {
                    SetText(ordemFeedback, "⚠ Sistema de ataque não encontrado");
                }
                break;

            case "VOLTAR_BASE":
                int retornando = 0;
                foreach (var u in snapshot)
                {
                    if (u != null)
                    {
                        var c17 = u.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>();
                        if (c17 != null)
                        {
                            c17.ComandoZ_VoltarAeroporto();
                            retornando++;
                            continue;
                        }

                        var aviao = u.GetComponent<ControleAviao>();
                        if (aviao != null)
                        {
                            aviao.ComandoRetornarBase();
                            retornando++;
                            continue;
                        }

                        var c700 = u.GetComponent<C700TransporteAereo>();
                        if (c700 != null)
                        {
                            c700.OrdenarRetornoAoAeroporto();
                            retornando++;
                            continue;
                        }

                        var heli = u.GetComponent<Helicoptero>();
                        if (heli != null)
                        {
                            heli.RetornarParaVagaAeroporto();
                            retornando++;
                            continue;
                        }

                        ControleUnidade controleUnidade = u.GetComponent<ControleUnidade>();
                        if (controleUnidade != null && controleUnidade.EmitirOrdemRetornarAoPontoInicial())
                        {
                            retornando++;
                        }
                    }
                }
                SetText(ordemFeedback, $"✔ [{retornando} UDS] → RETORNANDO");
                AdicionarLog("OPS", $"{retornando} unidades ordenadas a retornar", "normal");
                break;

            case "TROCAR_CAMERA":
                if (unidadesSelecionadasMenu.Count > 1)
                {
                    CiclarUnidadeSelecionada();
                    SetText(ordemFeedback, $"✔ CÂMERA ALTERADA PARA {ObterNomeExibicao(unidadeSelecionadaMenu.gameObject)}");
                }
                else
                {
                    AlternarModoCameraDrone();
                    SetText(ordemFeedback, $"✔ MODO DE CÂMERA ALTERNADO");
                }
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Log de comunicações
    // -----------------------------------------------------------------------
    public void AdicionarLog(string fonte, string msg, string tipo)
    {
        var now  = DateTime.Now;
        string t = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}";
        logs.Add((t, fonte, msg, tipo));

        if (logs.Count > 60) logs.RemoveAt(0);

        if (logContainer == null) return;

        while (logContainer.childCount >= 60)
        {
            logContainer.RemoveAt(0);
        }

        var entry = new VisualElement();
        entry.AddToClassList("log-entry");

        var linha = new VisualElement();
        linha.AddToClassList("log-linha");

        var lblTime = new Label($"[{t}] ");
        lblTime.AddToClassList("log-time");
        lblTime.AddToClassList("mono");

        var lblSrc = new Label($"{fonte}: ");
        lblSrc.AddToClassList("log-source");
        lblSrc.AddToClassList("mono");
        if (tipo == "sistema") lblSrc.AddToClassList("sistema");
        else if (fonte.Contains("Z") || fonte == "INIMIGO") lblSrc.AddToClassList("inimigo");

        var lblMsg = new Label(msg);
        lblMsg.AddToClassList("log-msg");

        linha.Add(lblTime);
        linha.Add(lblSrc);
        linha.Add(lblMsg);
        entry.Add(linha);

        entry.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            logScroll?.ScrollTo(entry);
        });

        logContainer.Add(entry);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static void SetText(Label lbl, string text)
    {
        if (lbl != null) lbl.text = text;
    }

    private static void SetBarWidth(VisualElement bar, float pct01)
    {
        if (bar != null)
            bar.style.width = new StyleLength(
                new Length(Mathf.Clamp01(pct01) * 100f, LengthUnit.Percent));
    }

    private void AtualizarBotaoRadarVisual()
    {
        if (btnRadarUnidade == null) return;

        bool temUnidadeAliada = false;
        bool radarLigado = false;
        bool radarSemEnergia = false;
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (!UnidadePodeUsarRadar(unidade)) continue;
            IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
            if (identidade == null || identidade.teamID != TimeJogadorAtual) continue;
            temUnidadeAliada = true;
            RadarUnidadeTatica radar = unidade.GetComponent<RadarUnidadeTatica>();
            radarLigado |= radar != null && radar.RadarLigado;
            radarSemEnergia |= radar != null && radar.BloqueadoPorEnergia;
        }

        btnRadarUnidade.SetEnabled(temUnidadeAliada);
        btnRadarUnidade.EnableInClassList("radar-ligado", radarLigado);
        btnRadarUnidade.EnableInClassList("radar-sem-energia", radarSemEnergia && !radarLigado);
        btnRadarUnidade.text = radarLigado ? "R •" : radarSemEnergia ? "R !" : "R";
        btnRadarUnidade.tooltip = radarLigado
            ? "Radar ligado — emissão detectável pelo inimigo; custo de energia por minuto"
            : radarSemEnergia
                ? "Radar sem energia — aguarde a recarga de energia para ligar novamente"
                : "Radar desligado — clique para detectar unidades inimigas em guerra";
        btnRadarUnidade.style.opacity = radarLigado
            ? (Mathf.Sin(Time.unscaledTime * 8f) > 0f ? 1f : 0.58f)
            : 1f;
    }

    private void AtualizarCacheSelecaoIds()
    {
        unidadesSelecionadasIds.Clear();

        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade cu = unidadesSelecionadasMenu[i];
            if (cu != null)
            {
                // O mapa usa o ID do GameObject, nao o ID do componente ControleUnidade.
                // Usar o componente fazia o anel e a cor de selecao nao aparecerem.
                unidadesSelecionadasIds.Add(cu.gameObject.GetInstanceID());
            }
        }

        AtualizarResumoSelecao();
    }

    private void AtualizarResumoSelecao()
    {
        if (selecaoResumo == null) return;

        const string marcaSelecao = "\u2713 ";

        if (unidadesSelecionadasMenu.Count == 0)
        {
            selecaoResumo.text = marcaSelecao + "NENHUMA UNIDADE SELECIONADA";
            return;
        }

        List<string> nomes = new List<string>(unidadesSelecionadasMenu.Count);
        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade cu = unidadesSelecionadasMenu[i];
            if (cu != null) nomes.Add(ObterNomeExibicao(cu.gameObject));
        }

        if (nomes.Count == 0)
        {
            selecaoResumo.text = marcaSelecao + "NENHUMA UNIDADE SELECIONADA";
        }
        else
        {
            selecaoResumo.text = marcaSelecao + string.Join(" | ", nomes) +
                (nomes.Count == 1 ? " SELECIONADO" : " SELECIONADOS");
        }
    }

    private void AtualizarCacheEntidadesSeNecessario(bool forcar = false)
    {
        float agora = Time.unscaledTime;
        if (!forcar && !cachesEntidadesSujo && agora < proximoRefreshCachesEntidades)
        {
            return;
        }

        RegistroEntidadesJogo.FillUnidades(cacheUnidadesMapa);
        // Alguns spawners antigos não notificam o RegistroEntidadesJogo.
        // Reconciliar a cena periodicamente evita que unidades próprias
        // apareçam no mundo, mas faltem no catálogo satélite.
        if (agora >= proximaBuscaUnidadesCenaFallback)
        {
            proximaBuscaUnidadesCenaFallback = agora + 2f;
            cacheUnidadesCenaFallback.Clear();
            cacheUnidadesCenaFallback.AddRange(FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None));
        }
        for (int i = 0; i < cacheUnidadesCenaFallback.Count; i++)
        {
            IdentidadeUnidade identidade = cacheUnidadesCenaFallback[i];
            if (identidade != null && identidade.gameObject.activeInHierarchy && !cacheUnidadesMapa.Contains(identidade))
            {
                cacheUnidadesMapa.Add(identidade);
            }
        }
        RegistroEntidadesJogo.FillControlesUnidade(cacheControlesPersistencia);
        RegistroEntidadesJogo.FillIdentidadesIA(cacheIdentidadesIA);
        proximoRefreshCachesEntidades = agora + 0.2f;
        cachesEntidadesSujo = false;
    }

    // ── MÉTODOS DE CONTROLE DO MAPA (ZOOM, PAN, CLIQUE E LINHAS) ─────────────
    private void MarcarCachesEntidadesSujo()
    {
        cachesEntidadesSujo = true;
        proximoRefreshCachesEntidades = 0f;
    }

    private void AlterarZoom(float zoomDelta, Vector2 localMousePos)
    {
        if (painelMapa == null) return;

        float zoomAntigo = mapaZoom;
        mapaZoom = Mathf.Clamp(mapaZoom + zoomDelta, 0.2f, 15f);

        if (zoomAntigo != mapaZoom)
        {
            float W = painelMapa.resolvedStyle.width;
            float H = painelMapa.resolvedStyle.height;
            if (W > 0 && H > 0)
            {
                float normX = localMousePos.x / W;
                float normY = localMousePos.y / H;

                float rangeXAntigo = (mundoMetade * 2f) / zoomAntigo;
                float rangeZAntigo = (mundoMetade * 2f) / zoomAntigo;
                float mundoMouseX = (mapaCentro.x - rangeXAntigo / 2f) + normX * rangeXAntigo;
                float mundoMouseZ = (mapaCentro.y - rangeZAntigo / 2f) + (1f - normY) * rangeZAntigo;

                float rangeXNovo = (mundoMetade * 2f) / mapaZoom;
                float rangeZNovo = (mundoMetade * 2f) / mapaZoom;

                mapaCentro.x = mundoMouseX - (normX - 0.5f) * rangeXNovo;
                mapaCentro.y = mundoMouseZ - (0.5f - normY) * rangeZNovo;

                LimitarCentroMapa(rangeXNovo, rangeZNovo);
            }
        }
    }

    private Vector3 ConverterLocalParaMundo(Vector2 localPos)
    {
        float W = painelMapa != null ? painelMapa.resolvedStyle.width : 500f;
        float H = painelMapa != null ? painelMapa.resolvedStyle.height : 500f;

        float rangeX = (mundoMetade * 2f) / mapaZoom;
        float rangeZ = (mundoMetade * 2f) / mapaZoom;

        float normX = W > 0 ? (localPos.x / W) : 0.5f;
        float normY = H > 0 ? (localPos.y / H) : 0.5f;

        float worldX = (mapaCentro.x - rangeX / 2f) + normX * rangeX;
        float worldZ = (mapaCentro.y - rangeZ / 2f) + (1f - normY) * rangeZ;

        return new Vector3(worldX, 0f, worldZ);
    }

    private void OnMapClicked(Vector2 localPos)
    {
        Vector3 worldPos = ConverterLocalParaMundo(localPos);

        if (modoLancamentoMissilMapaAtivo)
        {
            EnviarOrdemLancamentoMissilMapa(worldPos);
            return;
        }

        if (modoMoverMapaAtivo)
        {
            EnviarOrdemMoverMapa(worldPos);
            return;
        }

        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens == null) return;

        if (!desenhadorOrdens.modoPatrulhaAtivo && !desenhadorOrdens.modoSeguirAtivo && !desenhadorOrdens.modoAtaqueAtivo)
            return;

        if (desenhadorOrdens.modoPatrulhaAtivo)
        {
            bool adicionado = desenhadorOrdens.AdicionarPontoPatrulhaDoMenu(worldPos);
            SetText(ordemFeedback, adicionado
                ? $"✔ Ponto de patrulha adicionado em {worldPos.x:F0}, {worldPos.z:F0}\nENTER confirma. ESC ou Botão Direito cancela."
                : "⚠ Ponto naval ignorado: escolha uma área de água.");
        }
        else if (desenhadorOrdens.modoSeguirAtivo)
        {
            GameObject alvo = EncontrarUnidadeProxima(worldPos, 150f);
            if (EhImovelMapa(alvo))
            {
                alvo = null;
            }
            if (alvo != null)
            {
                ConfirmarSeguimentoEspecifico(alvo);
            }
            else
            {
                SetText(ordemFeedback, "⚠ Nenhuma unidade próxima encontrada para seguir.");
            }
        }
        else if (desenhadorOrdens.modoAtaqueAtivo)
        {
            GameObject alvo = EncontrarUnidadeProxima(worldPos, 150f, true);
            desenhadorOrdens.AplicarOrdemAtaqueDoMenu(worldPos, alvo != null ? alvo.transform : null);
            if (alvo != null)
            {
                SetText(ordemFeedback, $"✔ Ordem ATAQUE enviada contra {alvo.name}.");
                AdicionarLog("OPS", $"Ataque ao alvo {ObterNomeExibicao(alvo)} confirmado.", "alerta");
                SetText(ordemFeedback, $"ATAQUE confirmado contra {ObterNomeExibicao(alvo)}.");
            }
            else
            {
                SetText(ordemFeedback, $"✔ Ordem ATAQUE DE ÁREA enviada para {worldPos.x:F0}, {worldPos.z:F0}.");
                AdicionarLog("OPS", $"Ataque de área confirmado em {worldPos.x:F0}, {worldPos.z:F0}.", "alerta");
            }
        }
    }

    private void OnMapRightClicked(Vector2 localPos)
    {
        if (modoLancamentoMissilMapaAtivo)
        {
            EnviarOrdemLancamentoMissilMapa(ConverterLocalParaMundo(localPos));
            return;
        }

        if (modoMoverMapaAtivo)
        {
            modoMoverMapaAtivo = false;
            SetText(ordemFeedback, "Ordem MOVER cancelada.");
            AdicionarLog("OPS", "Ordem MOVER cancelada pelo usuário.", "normal");
            return;
        }

        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens != null)
        {
            desenhadorOrdens.CancelarModo();
            FecharPainelSeguimento();
            SetText(ordemFeedback, "Ordem cancelada.");
            AdicionarLog("OPS", "Ação cancelada pelo usuário.", "normal");
        }
    }

    private void EnviarOrdemLancamentoMissilMapa(Vector3 destino)
    {
        int ordenadas = 0;
        foreach (var unidade in unidadesSelecionadasMenu)
        {
            if (unidade == null) continue;
            SiloLancadorEstrategico silo = unidade.GetComponent<SiloLancadorEstrategico>();
            if (silo != null && silo.TentarLancarNaArea(destino)) ordenadas++;
        }

        modoLancamentoMissilMapaAtivo = false;
        SetText(ordemFeedback, ordenadas > 0
            ? $"Lançamento estratégico preparado para {ordenadas} base(s)."
            : "Nenhuma base estratégica aceitou o alvo.");
        AdicionarLog("OPS", $"Ordem de lançamento estratégico: {ordenadas} base(s).", ordenadas > 0 ? "alerta" : "normal");
    }

    private void EnviarOrdemMoverMapa(Vector3 destino)
    {
        int ordenadas = 0;
        int rejeitadas = 0;
        float nivelMar = NavalPlacementResolver.ResolveSeaLevel();

        for (int i = 0; i < unidadesSelecionadasMenu.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadasMenu[i];
            if (unidade == null)
            {
                continue;
            }

            Vector3 destinoUnidade = destino;
            if (unidade.EhUnidadeNaval())
            {
                destinoUnidade.y = nivelMar;
                if (!NavalPlacementResolver.IsWaterAtPosition(destinoUnidade)
                    && !NavalPlacementResolver.TryResolveNearestWaterPoint(destinoUnidade, 900f, out destinoUnidade))
                {
                    rejeitadas++;
                    continue;
                }
            }

            if (unidade.EmitirOrdemMover(destinoUnidade, true))
            {
                ordenadas++;
            }
            else
            {
                rejeitadas++;
            }
        }

        modoMoverMapaAtivo = false;
        string resumo = ordenadas > 0
            ? $"✔ {ordenadas} unidade(s) em movimento para {destino.x:F0}, {destino.z:F0}."
            : "⚠ Nenhuma unidade aceitou o destino selecionado.";
        if (rejeitadas > 0) resumo += $" {rejeitadas} rejeitada(s).";
        SetText(ordemFeedback, resumo);
        AdicionarLog("OPS", $"Ordem MOVER enviada: {ordenadas} aceita(s), {rejeitadas} rejeitada(s).", ordenadas > 0 ? "normal" : "alerta");
    }

    private void AtualizarCameraSeguimento(GameObject alvo)
    {
        if (alvo == null)
        {
            return;
        }

        // O painel de seguimento tambem deve mover a camera principal para o
        // alvo escolhido na lista lateral; a camera HUD continua sendo usada
        // para a mira/telemetria da unidade.
        CameraController cameraPrincipal = FindFirstObjectByType<CameraController>();
        cameraPrincipal?.FocarEm(alvo.transform.position);

        if (CameraUnidadeHUD.Instancia == null)
        {
            return;
        }

        ControleUnidade alvoControle = alvo.GetComponent<ControleUnidade>();
        if (alvoControle != null)
        {
            CameraUnidadeHUD.Instancia.DefinirTarget(alvoControle, true);
        }

        CameraUnidadeHUD.Instancia.TravadoEmAlvo(alvo.transform);
        CameraUnidadeHUD.Instancia.modoDroneCamera = true;

        if (flirUnidadeNome != null)
        {
            flirUnidadeNome.text = ObterNomeExibicao(alvo);
        }

        if (flirAlerta != null)
        {
            flirAlerta.text = "SEGUIMENTO ATIVO";
        }
    }

    private void AnimarItemSeguimento(Button item)
    {
        if (item == null)
        {
            return;
        }

        item.RemoveFromClassList("seguir-item-pulse");
        item.AddToClassList("seguir-item-pulse");

        int toggles = 0;
        IVisualElementScheduledItem scheduled = null;
        scheduled = item.schedule.Execute(() =>
        {
            if (item == null || item.panel == null)
            {
                scheduled?.Pause();
                return;
            }

            bool pulseAtivo = item.ClassListContains("seguir-item-pulse");
            item.EnableInClassList("seguir-item-pulse", !pulseAtivo);
            toggles++;

            if (toggles >= 6)
            {
                item.EnableInClassList("seguir-item-pulse", true);
                scheduled?.Pause();
            }
        }).Every(120);
    }

    private GameObject EncontrarUnidadeProxima(Vector3 worldPos, float raioMaximo, bool ignorarTimeJogador = false)
    {
        AtualizarCacheEntidadesSeNecessario();

        GameObject melhorAlvo = null;
        float menorDist = raioMaximo * raioMaximo;

        for (int i = 0; i < cacheUnidadesMapa.Count; i++)
        {
            var id = cacheUnidadesMapa[i];
            if (id == null || !id.gameObject.activeInHierarchy) continue;
            if (EhAviaoComercialNoSatelite(id.gameObject)) continue;
            if (ignorarTimeJogador && EhUnidadeDoJogador(id)) continue;

            Vector3 delta = id.transform.position - worldPos;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;
            if (distSqr < menorDist)
            {
                menorDist = distSqr;
                melhorAlvo = id.gameObject;
            }
        }

        for (int i = 0; i < cacheIdentidadesIA.Count; i++)
        {
            var id = cacheIdentidadesIA[i];
            if (id == null || !id.gameObject.activeInHierarchy) continue;
            if (ignorarTimeJogador && id.teamID == TimeJogadorAtual) continue;
            if (id.GetComponentInParent<IdentidadeUnidade>() != null) continue;

            Vector3 delta = id.transform.position - worldPos;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;
            if (distSqr < menorDist)
            {
                menorDist = distSqr;
                melhorAlvo = id.gameObject;
            }
        }

        return melhorAlvo;
    }

    private void DesenharLinhasOrdemNoMapaUI()
    {
        if (mapaLinhasLayer == null) return;

        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens == null)
        {
            for (int i = 0; i < linhasOrdemPool.Count; i++)
            {
                linhasOrdemPool[i].style.display = DisplayStyle.None;
            }
            return;
        }

        float W = mapaLinhasLayer.resolvedStyle.width;
        float H = mapaLinhasLayer.resolvedStyle.height;

        if (W <= 0 || H <= 0)
        {
            for (int i = 0; i < linhasOrdemPool.Count; i++)
            {
                linhasOrdemPool[i].style.display = DisplayStyle.None;
            }
            return;
        }

        linhasOrdemAtivas = 0;

        // 1. Linhas de Patrulha
        if (desenhadorOrdens.modoPatrulhaAtivo && desenhadorOrdens.pontosPatrulha != null && desenhadorOrdens.pontosPatrulha.Count > 0)
        {
            Vector2 pUltimo = Vector2.zero;
            bool temPrimeiro = false;

            for (int i = 0; i < desenhadorOrdens.pontosPatrulha.Count; i++)
            {
                Vector3 pontoMundo = desenhadorOrdens.pontosPatrulha[i];
                Vector2 pPixel = ConvertMundoParaPixel(pontoMundo, W, H);

                if (temPrimeiro)
                {
                    DesenharLinhaUI(pUltimo, pPixel, new Color(0.15f, 0.65f, 1f, 0.85f));
                }
                pUltimo = pPixel;
                temPrimeiro = true;
            }
        }

        // 2. Alvos de Ataque das Unidades Selecionadas
        if (unidadeSelecionadaMenu != null)
        {
            foreach (var cu in unidadesSelecionadasMenu)
            {
                if (cu == null) continue;
                
                Vector3 alvo = Vector3.zero;
                bool temAlvo = false;

                var bombardeiro = cu.GetComponent<AviaoBombardeiro>();
                if (bombardeiro != null && bombardeiro.modoDeAtaque == AviaoBombardeiro.ModoAtaque.AtaqueAoSolo)
                {
                    alvo = bombardeiro.alvoAreaSolo;
                    temAlvo = true;
                }
                else if (cu.OrdemAtual == OrdemControleUnidade.Movendo && cu.ObterEstadoControle().modoCombateAtivo)
                {
                    if (cu.ObterEstadoControle().possuiDestinoOrdenado)
                    {
                        alvo = cu.ObterEstadoControle().ultimoDestino;
                        temAlvo = true;
                    }
                }

                if (temAlvo)
                {
                    Vector2 pPixel = ConvertMundoParaPixel(alvo, W, H);
                    float tamX = 8f; // Tamanho do X no UI
                    DesenharLinhaUI(pPixel + new Vector2(-tamX, -tamX), pPixel + new Vector2(tamX, tamX), new Color(1f, 0.15f, 0.1f, 0.95f));
                    DesenharLinhaUI(pPixel + new Vector2(-tamX, tamX), pPixel + new Vector2(tamX, -tamX), new Color(1f, 0.15f, 0.1f, 0.95f));
                }
            }
        }

        for (int i = linhasOrdemAtivas; i < linhasOrdemPool.Count; i++)
        {
            linhasOrdemPool[i].style.display = DisplayStyle.None;
        }
    }

    private Vector2 ConvertMundoParaPixel(Vector3 pos3D, float W, float H)
    {
        float rangeX = (mundoMetade * 2f) / mapaZoom;
        float rangeZ = (mundoMetade * 2f) / mapaZoom;

        float pctX = (pos3D.x - (mapaCentro.x - rangeX / 2f)) / rangeX;
        float pctZ = 1f - (pos3D.z - (mapaCentro.y - rangeZ / 2f)) / rangeZ;

        return new Vector2(pctX * W, pctZ * H);
    }

    private void DesenharLinhaUI(Vector2 p1, Vector2 p2, Color cor)
    {
        float d = Vector2.Distance(p1, p2);
        if (d < 1f) return;

        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        VisualElement line;
        if (linhasOrdemAtivas < linhasOrdemPool.Count)
        {
            line = linhasOrdemPool[linhasOrdemAtivas];
        }
        else
        {
            line = new VisualElement();
            line.style.position = Position.Absolute;
            line.pickingMode = PickingMode.Ignore;
            line.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(0), Length.Percent(50)));
            linhasOrdemPool.Add(line);
            mapaLinhasLayer.Add(line);
        }

        line.style.left = p1.x;
        line.style.top = p1.y;
        line.style.width = d;
        line.style.height = 2f;
        line.style.backgroundColor = cor;
        line.style.rotate = new StyleRotate(new Rotate(angle));
        line.style.display = DisplayStyle.Flex;
        linhasOrdemAtivas++;
    }

    public void NotificarAtaqueDrone(string msg)
    {
        AdicionarLog("DRONE HASAF", "[FLIR] " + msg, "ATAQUE");
        if (flirAlerta != null)
        {
            flirAlerta.text = "ALERTA: " + msg;
            flirAlerta.style.color = Color.red;
        }
    }
}
