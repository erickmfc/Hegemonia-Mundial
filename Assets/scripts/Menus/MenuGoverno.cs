using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuGoverno : MonoBehaviour
{
    [Header("Atalho")]
    public KeyCode teclaAtalho = KeyCode.X;

    [Header("Layout RTS")]
    [Range(0.40f, 0.98f)] public float larguraTela = 0.86f;
    [Range(0.40f, 0.98f)] public float alturaTela = 0.96f;
    [Range(-0.18f, 0.18f)] public float deslocamentoVertical = -0.01f;
    public float larguraSidebar = 198f;
    public float larguraPainelDireito = 300f;
    public float alturaCabecalho = 70f;
    public float alturaRecursos = 48f;
    public float alturaSubAbas = 40f;
    public float alturaRodape = 34f;
    public float alturaDica = 28f;
    public float espacamento = 10f;

    [Header("Animacao")]
    public float duracaoAnimacaoMenu = 0.12f;
    public float duracaoAnimacaoAba = 0.06f;

    [Header("Fontes")]
    public Font fonteEmoji;
    public Font fonteTexto;
    public bool usarFonteEmoji = false;
    public bool mostrarAvisoEmojiNoRodape = false;

    [Header("Cores - RTS Moderno")]
    public Color corFundoJanela = new Color(0.020f, 0.026f, 0.032f, 0.985f);
    public Color corFundoEscuro = new Color(0.012f, 0.016f, 0.020f, 0.98f);
    public Color corPainel = new Color(0.043f, 0.055f, 0.066f, 0.96f);
    public Color corPainel2 = new Color(0.060f, 0.075f, 0.090f, 0.96f);
    public Color corCard = new Color(0.074f, 0.086f, 0.098f, 0.94f);
    public Color corCardClara = new Color(0.100f, 0.114f, 0.128f, 0.94f);
    public Color corLinha = new Color(0.180f, 0.640f, 0.900f, 0.92f);
    public Color corLinhaFraca = new Color(0.310f, 0.420f, 0.500f, 0.28f);
    public Color corDestaque = new Color(0.220f, 0.720f, 0.940f, 1f);
    public Color corAzulBotao = new Color(0.075f, 0.245f, 0.360f, 1f);
    public Color corAbaAtiva = new Color(0.090f, 0.230f, 0.320f, 0.98f);
    public Color corVerde = new Color(0.220f, 0.790f, 0.390f, 1f);
    public Color corAmarelo = new Color(0.950f, 0.720f, 0.280f, 1f);
    public Color corLaranja = new Color(0.950f, 0.450f, 0.180f, 1f);
    public Color corVermelho = new Color(0.900f, 0.180f, 0.140f, 1f);
    public Color corRoxo = new Color(0.610f, 0.430f, 0.900f, 1f);
    public Color corTextoPrimario = new Color(0.930f, 0.965f, 0.990f, 1f);
    public Color corTextoSecundario = new Color(0.690f, 0.775f, 0.840f, 1f);
    public Color corTextoApagado = new Color(0.500f, 0.560f, 0.620f, 1f);

    [Header("Sistema")]
    public int paisJogadorId = 1;
    public float intervaloTickGoverno = 1.00f;

    public List<PaisGoverno> paises = new List<PaisGoverno>();
    public List<RelacaoDiplomatica> relacoes = new List<RelacaoDiplomatica>();
    public List<NotificacaoGoverno> notificacoes = new List<NotificacaoGoverno>();

    public static MenuGoverno Instancia;
    public static bool EstaAberto;

    public enum CategoriaGoverno
    {
        RelacoesExteriores,
        Aliancas,
        Sancoes,
        Economia,
        MercadoGlobal,
        Interior,
        Defesa,
        Ciencia,
        Trabalho,
        DiversaoCultura
    }

    public enum BlocoGlobal { Nenhum, OrdemAtlas, PactoSolaris, LigaContinental }
    public enum StatusGeopolitico { Paz, Tensao, Crise, Sancoes, ConflitoLimitado, GuerraAberta }
    public enum EstadoRelacao { AliancaEstrategica, ParceiroMilitar, ParceiroComercial, Cordial, Neutro, Rivalidade, Hostilidade, CriseMilitar, Guerra }
    public enum TipoSancao { EmbargoComida, EmbargoPetroleo, EmbargoAco, EmbargoArmamentos, BloqueioTecnologico, BloqueioMilitar, RestricaoComercialTotal }

    [Serializable]
    public class PaisGoverno
    {
        public int id;
        public string nome;
        public bool jogador;
        public BlocoGlobal bloco = BlocoGlobal.Nenhum;
        public int aliadoPrioritarioId = -1;
        public int rivalEstrategicoId = -1;
        public StatusGeopolitico status = StatusGeopolitico.Paz;
        public int casas = 4;
        public int capacidadePorCasa = 250;
        public int populacaoAtual = 110;
        public int populacaoMaximaPorCasas = 200;
        public int quarteis = 2;
        public int militaresDisponiveis;
        public int militaresAtivos = 3250;
        public int dinheiro = 39534;
        public int comida = 500;
        public int petroleo = 3830;
        public int aco = 100;
        public int armamentos = 500;
        public int uranio;
    }

    [Serializable]
    public class RelacaoDiplomatica
    {
        public int paisA;
        public int paisB;
        [Range(-100, 100)] public int valor;
        public EstadoRelacao estado = EstadoRelacao.Neutro;
        public StatusGeopolitico status = StatusGeopolitico.Paz;
        public bool tratadoComercial = true;
        public bool pactoMilitar;
        public bool guerraDeclarada;
        public List<TipoSancao> sancoesAContraB = new List<TipoSancao>();
        public List<TipoSancao> sancoesBContraA = new List<TipoSancao>();
    }

    [Serializable]
    public class NotificacaoGoverno
    {
        public string icone;
        public string titulo;
        public string mensagem;
        public string hora;
        public Color cor;
    }

    private const string RootName = "Painel_Governo_RTS_Leve";
    private const float DynamicRefreshMinInterval = 0.20f;

    private static Font fontePadraoCache;
    private static readonly CategoriaGoverno[] Categorias = (CategoriaGoverno[])Enum.GetValues(typeof(CategoriaGoverno));
    private static readonly string[] SubRelacoes = { "Resumo", "Nacoes", "Tratados", "Crises" };
    private static readonly string[] SubAliancas = { "Federacoes", "Pactos", "Operacoes", "Pedidos" };
    private static readonly string[] SubSancoes = { "Visao Geral", "Aplicadas", "Tipos", "Historico", "Legitimidade", "Emprestimos" };
    private static readonly string[] SubEconomia = { "Tesouro", "Orcamento", "Producao", "Impostos" };
    private static readonly string[] SubMercado = { "Comprar", "Vender", "Precos", "Rotas" };
    private static readonly string[] SubInterior = { "Populacao", "Cidades", "Bem-estar", "Projetos", "Meio Ambiente" };
    private static readonly string[] SubDefesa = { "Comando", "Exercito", "Marinha", "Aerea", "Alertas" };
    private static readonly string[] SubCiencia = { "Pesquisa", "Tecnologias", "Projetos", "Laboratorios" };
    private static readonly string[] SubTrabalho = { "Empregos", "Setores", "Formacao", "Politicas" };
    private static readonly string[] SubDiversao = { "Resumo", "Estruturas", "Eventos" };

    private readonly Dictionary<CategoriaGoverno, NavButtonView> navButtons = new Dictionary<CategoriaGoverno, NavButtonView>();
    private readonly List<SubTabView> subTabViews = new List<SubTabView>();
    private readonly Dictionary<string, PageView> centerPages = new Dictionary<string, PageView>();
    private readonly Dictionary<string, PageView> rightPages = new Dictionary<string, PageView>();
    private readonly Dictionary<string, float> centerScrollByPage = new Dictionary<string, float>();
    private readonly Dictionary<string, float> rightScrollByPage = new Dictionary<string, float>();
    private readonly Dictionary<string, ResourceTopView> resourceViews = new Dictionary<string, ResourceTopView>();
    private readonly Dictionary<string, MarketSellRow> sellRows = new Dictionary<string, MarketSellRow>();
    private readonly Dictionary<string, MarketBuyRow> buyRows = new Dictionary<string, MarketBuyRow>();
    private readonly Dictionary<string, MarketPriceRow> priceRows = new Dictionary<string, MarketPriceRow>();
    private readonly List<RouteRow> routeRows = new List<RouteRow>();

    private GameObject canvasObj;
    private GameObject painelPrincipal;
    private CanvasGroup canvasGroupPainel;
    private RectTransform painelRect;
    private Transform resourceRoot;
    private Transform sidebarRoot;
    private Transform subTabsRoot;
    private Transform centerContentRoot;
    private Transform rightContentRoot;
    private ScrollRect centerScroll;
    private ScrollRect rightScroll;
    private Text titleText;
    private Text subtitleText;
    private Text footerLeftText;
    private Text footerRightText;
    private InputField campoNomePais;
    private InputField campoNomePresidente;
    private InputField campoNomeMoeda;
    private Coroutine menuAnimation;
    private CategoriaGoverno categoriaAtual = CategoriaGoverno.RelacoesExteriores;
    private int subAbaAtualIndex;
    private string activePageKey = string.Empty;
    private string cachedSceneName;
    private bool shellBuilt;
    private bool eventsConnected;
    private bool dynamicDirty;
    private float nextDynamicRefresh;
    private float nextPeriodicRefresh;
    private int paisSelecionadoId = 2;
    private string cidadeSelecionadaId = string.Empty;

    private bool hudCached;
    private MiniMapa cachedMiniMapa;
    private MenuComportamento cachedMenuComportamento;
    private MenuConstrucao cachedMenuConstrucao;

    public static void GarantirInstancia()
    {
        if (Instancia != null)
        {
            GarantirAtivo(Instancia);
            return;
        }

        MenuGoverno existente = AcharComponenteMesmoInativo<MenuGoverno>();
        if (existente != null)
        {
            Instancia = existente;
            GarantirAtivo(Instancia);
            return;
        }

        GameObject go = new GameObject("MenuGoverno_Runtime");
        Instancia = go.AddComponent<MenuGoverno>();
        DontDestroyOnLoad(go);
    }

    private static void GarantirAtivo(MenuGoverno menu)
    {
        if (menu == null) return;
        Transform t = menu.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
        menu.enabled = true;
    }

    private void AplicarLayoutGovernamentalAtual()
    {
        larguraTela = Mathf.Clamp(Mathf.Max(larguraTela, 0.94f), 0.80f, 0.98f);
        alturaTela = Mathf.Clamp(Mathf.Max(alturaTela, 0.82f), 0.70f, 0.99f);
        deslocamentoVertical = Mathf.Clamp(deslocamentoVertical, -0.08f, 0.08f);
        larguraSidebar = Mathf.Clamp(Mathf.Max(larguraSidebar, 220f), 190f, 240f);
        larguraPainelDireito = Mathf.Clamp(Mathf.Max(larguraPainelDireito, 340f), 280f, 380f);
        alturaCabecalho = Mathf.Clamp(Mathf.Max(alturaCabecalho, 78f), 66f, 90f);
        alturaRecursos = Mathf.Clamp(Mathf.Max(alturaRecursos, 54f), 44f, 64f);
        alturaSubAbas = Mathf.Clamp(Mathf.Max(alturaSubAbas, 44f), 38f, 52f);
        alturaRodape = Mathf.Clamp(Mathf.Max(alturaRodape, 40f), 32f, 48f);
        alturaDica = Mathf.Clamp(Mathf.Max(alturaDica, 30f), 24f, 36f);
        espacamento = Mathf.Clamp(Mathf.Max(espacamento, 12f), 8f, 16f);
        intervaloTickGoverno = Mathf.Clamp(intervaloTickGoverno, 0.75f, 2f);

        corFundoJanela = new Color(0.045f, 0.048f, 0.054f, 0.985f);
        corFundoEscuro = new Color(0.020f, 0.023f, 0.028f, 0.98f);
        corPainel = new Color(0.080f, 0.086f, 0.096f, 0.965f);
        corPainel2 = new Color(0.102f, 0.110f, 0.122f, 0.965f);
        corCard = new Color(0.122f, 0.130f, 0.144f, 0.94f);
        corCardClara = new Color(0.160f, 0.170f, 0.186f, 0.94f);
        corLinha = new Color(0.820f, 0.655f, 0.290f, 0.96f);
        corLinhaFraca = new Color(0.520f, 0.395f, 0.180f, 0.28f);
        corDestaque = new Color(0.920f, 0.740f, 0.300f, 1f);
        corAzulBotao = new Color(0.108f, 0.240f, 0.305f, 1f);
        corAbaAtiva = new Color(0.166f, 0.145f, 0.094f, 0.98f);
        corVerde = new Color(0.305f, 0.720f, 0.395f, 1f);
        corAmarelo = new Color(0.935f, 0.705f, 0.295f, 1f);
        corLaranja = new Color(0.930f, 0.485f, 0.210f, 1f);
        corVermelho = new Color(0.815f, 0.195f, 0.165f, 1f);
        corRoxo = new Color(0.560f, 0.440f, 0.850f, 1f);
        corTextoPrimario = new Color(0.945f, 0.965f, 0.985f, 1f);
        corTextoSecundario = new Color(0.735f, 0.795f, 0.845f, 1f);
        corTextoApagado = new Color(0.535f, 0.580f, 0.630f, 1f);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        EstaAberto = false;
        AplicarLayoutGovernamentalAtual();
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        cachedSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += AoMudarCena;

        SistemaGovernoMundial.GarantirInstancia();
        if (MenuGovernoNovoController.GarantirInstancia() && MenuGovernoNovoController.Instancia != null)
        {
            MenuGovernoNovoController.Instancia.Abrir(false);
            return;
        }
        BuildShell();
        painelPrincipal.SetActive(false);
    }

    private void OnEnable()
    {
        ConnectEvents();
    }

    private void OnDisable()
    {
        DisconnectEvents();
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= AoMudarCena;
        DisconnectEvents();
        if (Instancia == this)
        {
            Instancia = null;
            EstaAberto = false;
        }
    }

    private void Update()
    {
        if (!string.IsNullOrEmpty(cachedSceneName) && ConfiguracaoCenasJogo.EhCenaDeMenu(cachedSceneName))
            return;

        if (QuartelMenuUIController.EntradaGlobalBloqueada)
            return;

        if (Input.GetKeyDown(teclaAtalho))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
            AlternarMenu(!EstaAberto);
        }

        if (!EstaAberto)
            return;

        if (dynamicDirty && Time.unscaledTime >= nextDynamicRefresh)
        {
            dynamicDirty = false;
            nextDynamicRefresh = Time.unscaledTime + DynamicRefreshMinInterval;
            RefreshDynamicData(false);
        }

        if (Time.unscaledTime >= nextPeriodicRefresh)
        {
            nextPeriodicRefresh = Time.unscaledTime + Mathf.Max(0.2f, intervaloTickGoverno);
            RefreshDynamicData(false);
        }
    }

    public void AlternarMenu(bool abrir)
    {
        if (abrir && QuartelMenuUIController.EntradaGlobalBloqueada)
            return;

        GarantirAtivo(this);
        if (MenuGovernoNovoController.GarantirInstancia() && MenuGovernoNovoController.Instancia != null)
        {
            EstaAberto = abrir;
            MenuGovernoNovoController.Instancia.Abrir(abrir);
            EsconderHUD(abrir);
            if (painelPrincipal != null) painelPrincipal.SetActive(false);
            return;
        }
        BuildShell();

        if (abrir == EstaAberto && painelPrincipal.activeSelf == abrir)
            return;

        if (abrir)
        {
            SistemaGovernoMundial.GarantirInstancia();
            ShowCurrentPage();
            RefreshDynamicData(false);
            EsconderHUD(true);
        }
        else
        {
            SaveScrollPositions();
            EsconderHUD(false);
        }

        if (menuAnimation != null) StopCoroutine(menuAnimation);
        menuAnimation = StartCoroutine(AnimateMenu(abrir));
    }

    private IEnumerator AnimateMenu(bool abrir)
    {
        EstaAberto = abrir;
        painelPrincipal.SetActive(true);
        canvasGroupPainel.blocksRaycasts = abrir;
        canvasGroupPainel.interactable = abrir;

        float startAlpha = canvasGroupPainel.alpha;
        float endAlpha = abrir ? 1f : 0f;
        Vector2 startPos = painelRect.anchoredPosition;
        Vector2 endPos = new Vector2(0f, abrir ? 0f : -16f);
        if (abrir)
        {
            painelRect.anchoredPosition = new Vector2(0f, -16f);
            startPos = painelRect.anchoredPosition;
            endPos = Vector2.zero;
        }

        float t = 0f;
        float duration = Mathf.Max(0.01f, duracaoAnimacaoMenu);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            p = 1f - Mathf.Pow(1f - p, 2f);
            canvasGroupPainel.alpha = Mathf.Lerp(startAlpha, endAlpha, p);
            painelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            yield return null;
        }

        canvasGroupPainel.alpha = endAlpha;
        painelRect.anchoredPosition = endPos;
        if (!abrir) painelPrincipal.SetActive(false);
        menuAnimation = null;
    }

    private void AoMudarCena(UnityEngine.SceneManagement.Scene antiga, UnityEngine.SceneManagement.Scene nova)
    {
        cachedSceneName = nova.name;
        hudCached = false;
    }

    private void ConnectEvents()
    {
        if (eventsConnected) return;
        SistemaGovernoMundial.GarantirInstancia();

        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.OnGovernoAtualizado += MarkDynamicDirty;
            SistemaGovernoMundial.Instancia.OnNoticia += OnGovernmentNews;
            SistemaGovernoMundial.Instancia.OnPropostaCriada += OnProposalCreated;
        }

        if (SistemaMercadoGlobal.Instancia != null)
        {
            SistemaMercadoGlobal.Instancia.OnMercadoAtualizado += MarkDynamicDirty;
            SistemaMercadoGlobal.Instancia.OnTransacaoExecutada += OnMarketTransaction;
        }

        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados += MarkDynamicDirty;

        eventsConnected = true;
    }

    private void DisconnectEvents()
    {
        if (!eventsConnected) return;

        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.OnGovernoAtualizado -= MarkDynamicDirty;
            SistemaGovernoMundial.Instancia.OnNoticia -= OnGovernmentNews;
            SistemaGovernoMundial.Instancia.OnPropostaCriada -= OnProposalCreated;
        }

        if (SistemaMercadoGlobal.Instancia != null)
        {
            SistemaMercadoGlobal.Instancia.OnMercadoAtualizado -= MarkDynamicDirty;
            SistemaMercadoGlobal.Instancia.OnTransacaoExecutada -= OnMarketTransaction;
        }

        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados -= MarkDynamicDirty;

        eventsConnected = false;
    }

    private void MarkDynamicDirty()
    {
        dynamicDirty = true;
        if (!EstaAberto) return;
        if (Time.unscaledTime >= nextDynamicRefresh)
        {
            dynamicDirty = false;
            nextDynamicRefresh = Time.unscaledTime + DynamicRefreshMinInterval;
            RefreshDynamicData(false);
        }
    }

    private void OnGovernmentNews(string mensagem)
    {
        Notificar("Governo", mensagem);
    }

    private void OnProposalCreated(PropostaInternacional proposta)
    {
        if (proposta != null && proposta.alvoTeamId == paisJogadorId)
            Notificar("Proposta", proposta.motivo);
    }

    private void OnMarketTransaction(TransacaoMercado transacao)
    {
        if (transacao != null)
            Notificar("Mercado", transacao.mensagem);
    }

    private void BuildShell()
    {
        if (shellBuilt && painelPrincipal != null) return;
        AplicarLayoutGovernamentalAtual();

        GarantirCanvasEEventSystem();
        Transform antigo = canvasObj.transform.Find(RootName);
        if (antigo != null) Destroy(antigo.gameObject);

        painelPrincipal = CreateUIObject(RootName, canvasObj.transform);
        painelRect = painelPrincipal.GetComponent<RectTransform>();
        float w = Mathf.Clamp01(larguraTela);
        float halfH = Mathf.Clamp01(alturaTela) * 0.5f;
        painelRect.anchorMin = new Vector2(0.01f, 0.5f - halfH + deslocamentoVertical);
        painelRect.anchorMax = new Vector2(0.01f + w, 0.5f + halfH + deslocamentoVertical);
        painelRect.offsetMin = Vector2.zero;
        painelRect.offsetMax = Vector2.zero;

        Image bg = painelPrincipal.AddComponent<Image>();
        bg.color = corFundoJanela;
        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();
        canvasGroupPainel.alpha = 0f;
        canvasGroupPainel.blocksRaycasts = false;
        canvasGroupPainel.interactable = false;

        VerticalLayoutGroup root = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        root.spacing = 0;
        root.childControlWidth = true;
        root.childControlHeight = true;
        root.childForceExpandWidth = true;
        root.childForceExpandHeight = false;

        BuildAmbientBackdrop(painelPrincipal.transform);
        BuildHeader(painelPrincipal.transform);
        BuildResourceBar(painelPrincipal.transform);
        BuildMainArea(painelPrincipal.transform);
        BuildFooter(painelPrincipal.transform);

        shellBuilt = true;
        RefreshStaticNavigation();
        ShowCurrentPage();
    }

    private void BuildAmbientBackdrop(Transform parent)
    {
        GameObject wash = CreateUIObject("BackdropWash", parent);
        wash.transform.SetAsFirstSibling();
        LayoutElement washLe = wash.AddComponent<LayoutElement>();
        washLe.ignoreLayout = true;
        Image washImg = wash.AddComponent<Image>();
        washImg.color = new Color(0.018f, 0.022f, 0.030f, 0.18f);
        RectTransform washRt = wash.GetComponent<RectTransform>();
        Stretch(washRt, 0f, 0f, 0f, 0f);

        GameObject topGlow = CreateUIObject("GlowTopRight", parent);
        topGlow.transform.SetAsFirstSibling();
        LayoutElement topGlowLe = topGlow.AddComponent<LayoutElement>();
        topGlowLe.ignoreLayout = true;
        Image topGlowImg = topGlow.AddComponent<Image>();
        topGlowImg.color = new Color(0.920f, 0.720f, 0.250f, 0.08f);
        RectTransform topGlowRt = topGlow.GetComponent<RectTransform>();
        topGlowRt.anchorMin = new Vector2(0.66f, 0.70f);
        topGlowRt.anchorMax = new Vector2(1f, 1f);
        topGlowRt.offsetMin = Vector2.zero;
        topGlowRt.offsetMax = Vector2.zero;

        GameObject bottomGlow = CreateUIObject("GlowBottomLeft", parent);
        bottomGlow.transform.SetAsFirstSibling();
        LayoutElement bottomGlowLe = bottomGlow.AddComponent<LayoutElement>();
        bottomGlowLe.ignoreLayout = true;
        Image bottomGlowImg = bottomGlow.AddComponent<Image>();
        bottomGlowImg.color = new Color(0.070f, 0.170f, 0.220f, 0.11f);
        RectTransform bottomGlowRt = bottomGlow.GetComponent<RectTransform>();
        bottomGlowRt.anchorMin = new Vector2(0f, 0f);
        bottomGlowRt.anchorMax = new Vector2(0.34f, 0.32f);
        bottomGlowRt.offsetMin = Vector2.zero;
        bottomGlowRt.offsetMax = Vector2.zero;

        GameObject leftLine = CreateUIObject("AccentLine", parent);
        leftLine.transform.SetAsFirstSibling();
        LayoutElement lineLe = leftLine.AddComponent<LayoutElement>();
        lineLe.ignoreLayout = true;
        Image lineImg = leftLine.AddComponent<Image>();
        lineImg.color = new Color(0.820f, 0.655f, 0.290f, 0.22f);
        RectTransform lineRt = leftLine.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(0f, 1f);
        lineRt.sizeDelta = new Vector2(4f, 0f);
        lineRt.anchoredPosition = Vector2.zero;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, alturaCabecalho, new Color(0.016f, 0.022f, 0.028f, 0.98f));
        HorizontalLayoutGroup h = header.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(18, 16, 12, 12);
        h.spacing = 14;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;

        GameObject idBox = CreatePanel("Identidade", header.transform, 0f, corPainel);
        LayoutElement idLe = idBox.GetComponent<LayoutElement>();
        idLe.preferredWidth = 320f;
        idLe.minWidth = 280f;
        idLe.flexibleWidth = 0f;
        VerticalLayoutGroup idV = idBox.AddComponent<VerticalLayoutGroup>();
        idV.padding = new RectOffset(16, 14, 10, 8);
        idV.spacing = 2;
        titleText = CreateLayoutText(idBox.transform, "GOVERNO", 20, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 28f);
        subtitleText = CreateLayoutText(idBox.transform, "Painel nacional", 11, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Normal, 18f);

        GameObject center = CreateUIObject("TituloCentral", header.transform);
        center.AddComponent<LayoutElement>().flexibleWidth = 1f;
        CreateFreeText(center.transform, "HEGEMONIA GLOBAL", 25, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold);

        GameObject identidadeEditor = CreatePanel("EditorIdentidade", header.transform, 0f, corPainel2);
        LayoutElement editorLe = identidadeEditor.GetComponent<LayoutElement>();
        editorLe.preferredWidth = 500f;
        editorLe.minWidth = 440f;
        editorLe.flexibleWidth = 0f;

        VerticalLayoutGroup editorLayout = identidadeEditor.AddComponent<VerticalLayoutGroup>();
        editorLayout.padding = new RectOffset(12, 12, 10, 10);
        editorLayout.spacing = 6;
        editorLayout.childControlWidth = true;
        editorLayout.childControlHeight = true;
        editorLayout.childForceExpandHeight = false;

        GameObject linhaSuperior = CreateUIObject("LinhaSuperior", identidadeEditor.transform);
        HorizontalLayoutGroup linhaSuperiorLayout = linhaSuperior.AddComponent<HorizontalLayoutGroup>();
        linhaSuperiorLayout.spacing = 6;
        linhaSuperiorLayout.childControlWidth = true;
        linhaSuperiorLayout.childControlHeight = true;
        linhaSuperiorLayout.childForceExpandWidth = true;
        linhaSuperiorLayout.childForceExpandHeight = true;
        linhaSuperior.AddComponent<LayoutElement>().preferredHeight = 30f;

        campoNomePais = CreateCompactInput(linhaSuperior.transform, "Pais");
        campoNomePais.GetComponent<LayoutElement>().flexibleWidth = 1f;
        campoNomePresidente = CreateCompactInput(linhaSuperior.transform, "Presidente");
        campoNomePresidente.GetComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject linhaInferior = CreateUIObject("LinhaInferior", identidadeEditor.transform);
        HorizontalLayoutGroup linhaInferiorLayout = linhaInferior.AddComponent<HorizontalLayoutGroup>();
        linhaInferiorLayout.spacing = 6;
        linhaInferiorLayout.childControlWidth = true;
        linhaInferiorLayout.childControlHeight = true;
        linhaInferiorLayout.childForceExpandWidth = true;
        linhaInferiorLayout.childForceExpandHeight = true;
        linhaInferior.AddComponent<LayoutElement>().preferredHeight = 34f;

        campoNomeMoeda = CreateCompactInput(linhaInferior.transform, "Moeda");
        campoNomeMoeda.GetComponent<LayoutElement>().flexibleWidth = 1f;
        Button aplicar = CreateMiniActionButton(linhaInferior.transform, "Aplicar identidade", corAzulBotao, AplicarIdentidadeNacional);
        LayoutElement aplicarLe = aplicar.GetComponent<LayoutElement>();
        aplicarLe.flexibleWidth = 1f;
        aplicarLe.minWidth = 150f;

    }

    private void BuildResourceBar(Transform parent)
    {
        GameObject bar = CreatePanel("Recursos", parent, alturaRecursos, new Color(0.014f, 0.020f, 0.025f, 0.98f));
        resourceRoot = bar.transform;
        HorizontalLayoutGroup h = bar.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 6, 6);
        h.spacing = 6;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;

        CreateResourceView("DINHEIRO", "$", corVerde);
        CreateResourceView("PETROLEO", "OIL", corDestaque);
        CreateResourceView("ACO", "ACO", corAmarelo);
        CreateResourceView("ENERGIA", "NRG", corRoxo);
        CreateResourceView("COMIDA", "FOOD", corVerde);
        CreateResourceView("POP", "POP", corTextoSecundario);
        CreateResourceView("ESTAB", "EST", corDestaque);
        CreateResourceView("STATUS", "STS", corTextoSecundario);
    }

    private void BuildMainArea(Transform parent)
    {
        GameObject body = CreateUIObject("Corpo", parent);
        LayoutElement bodyLe = body.AddComponent<LayoutElement>();
        bodyLe.flexibleHeight = 1f;
        bodyLe.minHeight = 400f;

        HorizontalLayoutGroup h = body.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 14, 14);
        h.spacing = espacamento;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.childForceExpandWidth = false;

        BuildSidebar(body.transform);
        BuildContentArea(body.transform);
    }

    private void BuildSidebar(Transform parent)
    {
        GameObject side = CreatePanel("Sidebar", parent, 0f, new Color(0.026f, 0.034f, 0.041f, 0.98f));
        sidebarRoot = side.transform;
        LayoutElement le = side.GetComponent<LayoutElement>();
        le.preferredWidth = larguraSidebar;
        le.minWidth = Mathf.Min(200f, larguraSidebar);
        le.flexibleWidth = 0f;

        VerticalLayoutGroup v = side.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(6, 6, 6, 6);
        v.spacing = 5;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        foreach (CategoriaGoverno categoria in Categorias)
        {
            CreateNavButton(categoria);
        }
    }

    private void BuildContentArea(Transform parent)
    {
        GameObject area = CreateUIObject("AreaConteudo", parent);
        area.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup v = area.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        GameObject sub = CreatePanel("SubAbas", area.transform, alturaSubAbas, new Color(0.020f, 0.027f, 0.034f, 0.98f));
        subTabsRoot = sub.transform;
        HorizontalLayoutGroup subH = sub.AddComponent<HorizontalLayoutGroup>();
        subH.padding = new RectOffset(8, 8, 6, 6);
        subH.spacing = 6;
        subH.childControlWidth = false;
        subH.childControlHeight = true;
        subH.childForceExpandWidth = false;

        GameObject split = CreateUIObject("Split", area.transform);
        LayoutElement splitLe = split.AddComponent<LayoutElement>();
        splitLe.flexibleHeight = 1f;
        splitLe.minHeight = 320f;

        HorizontalLayoutGroup splitH = split.AddComponent<HorizontalLayoutGroup>();
        splitH.spacing = espacamento;
        splitH.childControlWidth = true;
        splitH.childControlHeight = true;
        splitH.childForceExpandHeight = true;
        splitH.childForceExpandWidth = false;

        centerScroll = CreateScrollPanel(split.transform, "CentroScroll", 0f, 0f, 1f, out centerContentRoot);
        rightScroll = CreateScrollPanel(split.transform, "AcoesScroll", larguraPainelDireito, Mathf.Min(320f, larguraPainelDireito), 0f, out rightContentRoot);
    }

    private void BuildFooter(Transform parent)
    {
        GameObject footer = CreatePanel("FooterCompacto", parent, alturaRodape, new Color(0.014f, 0.020f, 0.025f, 0.98f));
        HorizontalLayoutGroup h = footer.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(14, 14, 6, 6);
        h.spacing = 10;
        h.childControlWidth = true;
        h.childControlHeight = true;

        footerLeftText = CreateLayoutText(footer.transform, "Sistema pronto", 12, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 28f);
        footerLeftText.GetComponent<LayoutElement>().flexibleWidth = 1f;
        footerRightText = CreateLayoutText(footer.transform, "Resumo operacional", 11, corTextoApagado, TextAnchor.MiddleRight, FontStyle.Normal, 28f);
        footerRightText.GetComponent<LayoutElement>().preferredWidth = 340f;
    }

    private void RefreshStaticNavigation()
    {
        foreach (KeyValuePair<CategoriaGoverno, NavButtonView> pair in navButtons)
        {
            bool active = pair.Key == categoriaAtual;
            pair.Value.Background.color = active ? corAbaAtiva : new Color(0.038f, 0.047f, 0.056f, 0.88f);
            pair.Value.Accent.color = active ? corDestaque : Color.clear;
            pair.Value.Text.color = active ? Color.white : corTextoSecundario;
        }

        string[] subtabs = GetSubTabs(categoriaAtual);
        if (subAbaAtualIndex < 0 || subAbaAtualIndex >= subtabs.Length) subAbaAtualIndex = 0;

        EnsureSubTabButtons(subtabs);
        for (int i = 0; i < subTabViews.Count; i++)
        {
            bool visible = i < subtabs.Length;
            subTabViews[i].Root.SetActive(visible);
            if (!visible) continue;

            bool active = i == subAbaAtualIndex;
            subTabViews[i].Label.text = subtabs[i].ToUpperInvariant();
            subTabViews[i].Background.color = active ? corAbaAtiva : new Color(0.045f, 0.056f, 0.067f, 0.94f);
            subTabViews[i].Label.color = active ? Color.white : corTextoSecundario;
            subTabViews[i].Accent.color = active ? corDestaque : Color.clear;
        }

        DadosPaisGoverno jogador = GetPlayerGov();
        if (titleText != null) titleText.text = jogador != null ? jogador.nomePais.ToUpperInvariant() : "GOVERNO";
        if (subtitleText != null)
        {
            string presidente = jogador != null && !string.IsNullOrWhiteSpace(jogador.nomePresidente)
                ? jogador.nomePresidente
                : "Sem presidente";
            string cambio = jogador != null ? " | 1 " + jogador.nomeMoeda + " = " + jogador.cambioComLider.ToString("0.00") + " " + jogador.moedaLiderReferencia : string.Empty;
            subtitleText.text = GetCategoryTitle(categoriaAtual) + " | " + presidente + cambio;
        }
    }

    private void ShowCurrentPage()
    {
        BuildShell();
        SaveScrollPositions();

        string key = CurrentPageKey();
        activePageKey = key;

        foreach (PageView page in centerPages.Values)
            page.Root.SetActive(page.Key == key);
        foreach (PageView page in rightPages.Values)
            page.Root.SetActive(page.Key == key);

        PageView center = EnsureCenterPage(key);
        PageView right = EnsureRightPage(key);
        center.Root.SetActive(true);
        right.Root.SetActive(true);
        center.Refresh?.Invoke();
        right.Refresh?.Invoke();
        RefreshStaticNavigation();
        RestoreScrollPositions();
    }

    private PageView EnsureCenterPage(string key)
    {
        PageView view;
        if (centerPages.TryGetValue(key, out view)) return view;

        GameObject root = CreatePageRoot("Centro_" + key, centerContentRoot);
        view = new PageView { Key = key, Root = root };
        centerPages[key] = view;
        BuildCenterPage(view);
        return view;
    }

    private PageView EnsureRightPage(string key)
    {
        PageView view;
        if (rightPages.TryGetValue(key, out view)) return view;

        GameObject root = CreatePageRoot("Acoes_" + key, rightContentRoot);
        view = new PageView { Key = key, Root = root };
        rightPages[key] = view;
        BuildRightPage(view);
        return view;
    }

    private void BuildCenterPage(PageView page)
    {
        switch (categoriaAtual)
        {
            case CategoriaGoverno.MercadoGlobal:
                BuildMarketCenterPage(page);
                break;
            case CategoriaGoverno.RelacoesExteriores:
                BuildRelationsPage(page);
                break;
            case CategoriaGoverno.Aliancas:
                BuildAlliancePage(page);
                break;
            case CategoriaGoverno.Sancoes:
                BuildSanctionsPage(page);
                break;
            case CategoriaGoverno.Economia:
                BuildEconomyPage(page);
                break;
            case CategoriaGoverno.Interior:
                BuildInteriorPage(page);
                break;
            case CategoriaGoverno.Defesa:
                BuildDefensePage(page);
                break;
            case CategoriaGoverno.Ciencia:
                BuildSciencePage(page);
                break;
            case CategoriaGoverno.Trabalho:
                BuildWorkPage(page);
                break;
            case CategoriaGoverno.DiversaoCultura:
                BuildCulturePage(page);
                break;
        }
    }

    private void BuildRightPage(PageView page)
    {
        switch (categoriaAtual)
        {
            case CategoriaGoverno.MercadoGlobal:
                BuildMarketActionsPage(page);
                break;
            case CategoriaGoverno.Sancoes:
                BuildSanctionActionsPage(page);
                break;
            case CategoriaGoverno.Economia:
                BuildEconomyActionsPage(page);
                break;
            case CategoriaGoverno.Interior:
                BuildInteriorActionsPage(page);
                break;
            case CategoriaGoverno.Defesa:
                BuildDefenseActionsPage(page);
                break;
            case CategoriaGoverno.Ciencia:
                BuildScienceActionsPage(page);
                break;
            case CategoriaGoverno.Trabalho:
                BuildWorkActionsPage(page);
                break;
            case CategoriaGoverno.DiversaoCultura:
                BuildCultureActionsPage(page);
                break;
            default:
                BuildDiplomacyActionsPage(page);
                break;
        }
    }

    private void RefreshDynamicData(bool preserveScroll)
    {
        if (!shellBuilt) return;
        SaveScrollPositions();

        RefreshResourceBar();
        RefreshFooter();
        RefreshIdentityFields();
        RefreshStaticNavigation();

        if (preserveScroll)
        {
            PageView center;
            if (centerPages.TryGetValue(CurrentPageKey(), out center))
                center.Refresh?.Invoke();

            PageView right;
            if (rightPages.TryGetValue(CurrentPageKey(), out right))
                right.Refresh?.Invoke();
        }

        RestoreScrollPositions();
    }

    private void BuildMarketCenterPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        if (tab == 1)
        {
            BuildMarketSellPage(page);
            return;
        }

        if (tab == 2)
        {
            BuildMarketPricesPage(page);
            return;
        }

        if (tab == 3)
        {
            BuildMarketRoutesPage(page);
            return;
        }

        BuildMarketBuyPage(page);
    }

    private void BuildMarketBuyPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Comprar recursos");
        CreateDescription(page.Root.transform, "Ofertas compactas com parceiro recomendado, preco atual e compra em lote.");
        CreateHeaderRow(page.Root.transform, new[] { "RECURSO", "ESTOQUE", "PRECO", "PARCEIRO", "ACAO" }, new[] { 1.25f, 0.8f, 0.8f, 1.2f, 1.0f });

        page.Refresh = () =>
        {
            SistemaMercadoGlobal mercado = Market();
            if (mercado == null) return;
            foreach (DadosItemMercado item in mercado.ItensOrdenados().Where(i => i.podeComprar).Take(64))
            {
                if (!buyRows.ContainsKey(item.id))
                    buyRows[item.id] = CreateBuyRow(page.Root.transform, item);
                buyRows[item.id].Refresh(item);
            }
        };
        page.Refresh();
    }

    private void BuildMarketSellPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Vender recursos reais");
        CreateDescription(page.Root.transform, "Acoes de venda atualizam somente as linhas e mantem a posicao do scroll.");
        CreateHeaderRow(page.Root.transform, new[] { "RECURSO", "ESTOQUE", "PRECO", "AUTO", "VENDA" }, new[] { 1.1f, 0.8f, 0.8f, 1.1f, 1.6f });

        string[] ids = MercadoIdsVendaveis();
        for (int i = 0; i < ids.Length; i++)
        {
            if (!sellRows.ContainsKey(ids[i]))
                sellRows[ids[i]] = CreateSellRow(page.Root.transform, ids[i]);
        }

        page.Refresh = () =>
        {
            foreach (string id in ids)
            {
                MarketSellRow row;
                if (sellRows.TryGetValue(id, out row)) row.Refresh();
            }
        };
        page.Refresh();
    }

    private string[] MercadoIdsVendaveis()
    {
        SistemaMercadoGlobal mercado = Market();
        if (mercado == null)
            return new[] { "energia" };

        return mercado.ItensOrdenados()
            .Where(i => i != null && i.podeVender && !i.equipamentoMilitar && !i.municaoMilitar)
            .Select(i => i.id)
            .Distinct()
            .Take(64)
            .ToArray();
    }

    private void BuildMarketPricesPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Precos globais");
        CreateDescription(page.Root.transform, "Tabela leve para comparar preco, variacao, oferta, demanda e estoque.");
        CreateHeaderRow(page.Root.transform, new[] { "ITEM", "PRECO", "VAR", "OFERTA", "DEMANDA", "ESTOQUE" }, new[] { 1.25f, 0.8f, 0.6f, 0.75f, 0.75f, 0.9f });

        page.Refresh = () =>
        {
            SistemaMercadoGlobal mercado = Market();
            if (mercado == null) return;
            foreach (DadosItemMercado item in mercado.ItensOrdenados().Take(14))
            {
                if (!priceRows.ContainsKey(item.id))
                    priceRows[item.id] = CreatePriceRow(page.Root.transform, item);
                priceRows[item.id].Refresh(item);
            }
        };
        page.Refresh();
    }

    private void BuildMarketRoutesPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Rotas e historico");
        CreateDescription(page.Root.transform, "Resumo dos parceiros mais provaveis e ultimas transacoes do mercado.");
        for (int i = 0; i < 10; i++)
            routeRows.Add(CreateRouteRow(page.Root.transform));

        page.Refresh = () =>
        {
            SistemaMercadoGlobal mercado = Market();
            SistemaGovernoMundial gov = Government();
            if (mercado == null || gov == null) return;

            List<string> lines = new List<string>();
            foreach (DadosItemMercado item in mercado.ItensOrdenados().Take(5))
            {
                DadosPaisGoverno seller = ChooseMarketPartner(gov, item, false);
                lines.Add(item.nome + "  |  " + (seller != null ? seller.nomePais : "sem rota") + "  |  $" + FormatNumber(item.precoAtual));
            }

            foreach (TransacaoMercado t in mercado.historico.Take(5))
            {
                if (t != null) lines.Add("Historico  |  " + t.mensagem + "  |  $" + FormatNumber(t.total));
            }

            for (int i = 0; i < routeRows.Count; i++)
            {
                bool active = i < lines.Count;
                routeRows[i].Root.SetActive(active);
                if (active) routeRows[i].Text.text = lines[i];
            }
        };
        page.Refresh();
    }

    private void BuildMarketActionsPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Acoes rapidas");
        Text summary = CreateInfoBlock(page.Root.transform, "Mercado carregando...");
        Button buy = CreateActionButton(page.Root.transform, "COMPRAR MELHOR OFERTA", corAzulBotao, BuyBestOffer);
        Button sell = CreateActionButton(page.Root.transform, "VENDER LOTE RECOMENDADO", corPainel2, SellRecommendedLot);
        Button simulate = CreateActionButton(page.Root.transform, "ATUALIZAR PRECOS", new Color(0.120f, 0.170f, 0.210f, 1f), () =>
        {
            Market()?.SimularMercado();
            Notificar("Mercado", "Precos atualizados.");
            RefreshDynamicData(true);
        });

        page.Refresh = () =>
        {
            SistemaMercadoGlobal mercado = Market();
            if (mercado == null)
            {
                summary.text = "Sistema de mercado indisponivel.";
                buy.interactable = false;
                sell.interactable = false;
                simulate.interactable = false;
                return;
            }

            DadosItemMercado best = mercado.MelhorCompra() ?? mercado.ItensOrdenados().FirstOrDefault();
            DadosItemMercado risk = mercado.MaiorRisco();
            summary.text = "Melhor compra: " + (best != null ? best.nome + " $" + FormatNumber(best.precoAtual) : "nenhuma")
                + "\nMaior risco: " + (risk != null ? risk.nome + " " + SignedPercent(risk.variacaoPercentual) : "nenhum")
                + "\nHistorico: " + mercado.historico.Count + " transacoes";
            buy.interactable = best != null;
            sell.interactable = best != null;
            simulate.interactable = true;
        };
        page.Refresh();
    }

    private void BuildRelationsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            switch (tab)
            {
                case 1:
                    CreateSectionTitle(page.Root.transform, "Nacoes monitoradas");
                    CreateDescription(page.Root.transform, "Panorama dos paises ativos com status politico, bloco e economia.");
                    BuildNationOverviewRows(page.Root.transform, OrderedCountries(false));
                    break;
                case 2:
                    CreateSectionTitle(page.Root.transform, "Tratados ativos");
                    CreateDescription(page.Root.transform, "Acordos comerciais, pactos militares e afinidade entre paises.");
                    BuildTreatyRows(page.Root.transform, false);
                    break;
                case 3:
                    CreateSectionTitle(page.Root.transform, "Crises e atritos");
                    CreateDescription(page.Root.transform, "Focos de guerra, sancoes, tensao diplomatica e baixa estabilidade.");
                    BuildCrisisRows(page.Root.transform);
                    break;
                default:
                    CreateSectionTitle(page.Root.transform, "Relacoes exteriores");
                    CreateDescription(page.Root.transform, "Visao compacta de paises, relacao, status e tratados.");
                    CreateHeaderRow(page.Root.transform, new[] { "PAIS", "BLOCO", "REL", "STATUS" }, new[] { 1.35f, 0.95f, 0.45f, 0.75f });
                    RebuildSimpleCountryRows(page.Root.transform, "Relacao");
                    break;
            }
        };
        page.Refresh();
    }

    private void BuildAlliancePage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            switch (tab)
            {
                case 1:
                    CreateSectionTitle(page.Root.transform, "Pactos militares");
                    CreateDescription(page.Root.transform, "Aliados com pacto ativo ou negociacao militar em andamento.");
                    BuildTreatyRows(page.Root.transform, true);
                    break;
                case 2:
                    CreateSectionTitle(page.Root.transform, "Operacoes conjuntas");
                    CreateDescription(page.Root.transform, "Prioridades da IA parceira, excedentes e necessidades para cooperacao.");
                    BuildAllianceOperationRows(page.Root.transform);
                    break;
                case 3:
                    CreateSectionTitle(page.Root.transform, "Pedidos diplomáticos");
                    CreateDescription(page.Root.transform, "Propostas recebidas da IA: aceitar, negociar ou recusar.");
                    BuildPendingProposalRows(page.Root.transform, true);
                    break;
                default:
                    CreateSectionTitle(page.Root.transform, "Federacoes globais");
                    CreateDescription(page.Root.transform, "Filiacao obrigatoria, balanceamento automatico e influencia dos dois blocos.");
                    BuildFederationRows(page.Root.transform);
                    break;
            }
        };
        page.Refresh();
    }

    private void BuildSanctionsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            switch (tab)
            {
                case 1:
                    CreateSectionTitle(page.Root.transform, "Sancoes aplicadas");
                    CreateDescription(page.Root.transform, "Paises atualmente sancionados e o impacto politico observado.");
                    BuildSanctionRows(page.Root.transform, true);
                    break;
                case 2:
                    CreateSectionTitle(page.Root.transform, "Doutrina de sancoes");
                    CreateDescription(page.Root.transform, "Leituras taticas para embargo comercial, pressao militar e isolamento.");
                    BuildSanctionTypeNotes(page.Root.transform);
                    break;
                case 3:
                    CreateSectionTitle(page.Root.transform, "Historico de crises");
                    CreateDescription(page.Root.transform, "Ultimas noticias politicas e comerciais registradas pelo governo.");
                    BuildNewsRows(page.Root.transform);
                    break;
                case 4:
                    CreateSectionTitle(page.Root.transform, "Legitimidade global");
                    BuildLegitimacyRows(page.Root.transform);
                    break;
                case 5:
                    CreateSectionTitle(page.Root.transform, "Emprestimos federativos");
                    BuildLoanRows(page.Root.transform);
                    break;
                default:
                    CreateSectionTitle(page.Root.transform, "Sancoes");
                    CreateDescription(page.Root.transform, "Controle rapido de sancoes e crises comerciais.");
                    CreateHeaderRow(page.Root.transform, new[] { "PAIS", "BLOCO", "REL", "STATUS" }, new[] { 1.35f, 0.95f, 0.45f, 0.75f });
                    RebuildSimpleCountryRows(page.Root.transform, "Sancao");
                    break;
            }
        };
        page.Refresh();
    }

    private void BuildEconomyPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosPaisGoverno p = GetPlayerGov();
            DadosEconomiaPais economia = PlayerEconomy();
            if (p == null)
            {
                CreateSectionTitle(page.Root.transform, "Economia nacional");
                CreateInfoBlock(page.Root.transform, "Dados economicos indisponiveis.");
                return;
            }

            if (tab == 1)
            {
                CreateSectionTitle(page.Root.transform, "Orcamento nacional");
                CreateDescription(page.Root.transform, "Receitas reais por setor, manutencao e saldo operacional.");
                CreateEconomyBudgetRows(page.Root.transform, p);
            }
            else if (tab == 2)
            {
                CreateSectionTitle(page.Root.transform, "Producao e capacidade");
                CreateDescription(page.Root.transform, "Oferta industrial, energia, alimentos, gargalos e infraestrutura.");
                CreateEconomyProductionRows(page.Root.transform, p, economia);
            }
            else if (tab == 3)
            {
                CreateSectionTitle(page.Root.transform, "Politica fiscal");
                CreateDescription(page.Root.transform, "Aliquotas atuais, pressao fiscal e impacto esperado em estabilidade.");
                CreateTaxOverviewRows(page.Root.transform, p);
            }
            else
            {
                CreateSectionTitle(page.Root.transform, "Tesouro nacional");
                CreateDescription(page.Root.transform, "Saldo, poder de compra, comercio exterior e pulso economico do pais.");
                Text stats = CreateInfoBlock(page.Root.transform, string.Empty);
                stats.text = "Saldo: $" + FormatNumber(p.saldo)
                    + "\nRenda bruta: +" + Mathf.RoundToInt(p.rendaPorSegundo) + "/s"
                    + "\nGastos: -$" + FormatNumber(p.gastosPorSegundo) + "/s"
                    + "\nSaldo operacional: " + SignedRate(p.saldoOperacional)
                    + "\nPoder de compra: " + p.PoderDeCompra.ToString("0.00")
                    + "\nMoeda: 1 " + p.nomeMoeda + " = " + p.cambioComLider.ToString("0.00") + " " + p.moedaLiderReferencia
                    + "\nOuro/reserva: " + p.reservaOuro.ToString("0")
                    + "\nInflacao: " + p.inflacao.ToString("0.0") + "%"
                    + "\nExportacao: " + p.exportacaoTotal.ToString("0.0")
                    + "\nImportacao: " + p.importacaoTotal.ToString("0.0")
                    + "\nEstoque energia: " + FormatNumber(p.energia)
                    + "\nEstoque comida: " + FormatNumber(p.comida)
                    + "\nDeficit principal: " + MainDeficit(p);
            }
        };
        page.Refresh();
    }

    private void BuildInteriorPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosPaisGoverno p = GetPlayerGov();
            if (p == null) return;

            if (tab == 4)
            {
                BuildEnvironmentPage(page, p);
            }
            else if (tab == 1) // Cidades e Estados
            {
                CreateSectionTitle(page.Root.transform, "Divisão Territorial");
                CreateDescription(page.Root.transform, "Cidades e Estados no mapa. Selecione uma cidade para gerenciar à direita.");
                CreateHeaderRow(page.Root.transform, new[] { "NOME DA CIDADE", "TIPO", "POP. CIVIL", "AEROPORTO", "DOMÍNIO" }, new[] { 1.4f, 1.0f, 0.8f, 0.8f, 0.8f });
                BuildCidadesRows(page.Root.transform);
            }
            else
            {
                CreateSectionTitle(page.Root.transform, "Interior");
                Text stats = CreateInfoBlock(page.Root.transform, string.Empty);
                stats.text = "Populacao: " + FormatNumber(p.populacao) + " / " + FormatNumber(p.populacaoMaxima)
                  + "\nEmprego: " + p.emprego.ToString("0") + "%"
                  + "\nMoradia: " + p.moradia.ToString("0") + "%"
                  + "\nQualidade de vida: " + p.qualidadeVida.ToString("0") + "%"
                  + "\nDeficit principal: " + MainDeficit(p);
            }
        };
        page.Refresh();
    }

    private void BuildEnvironmentPage(PageView page, DadosPaisGoverno pais)
    {
        DadosEconomiaPais economia = PlayerEconomy();
        CreateSectionTitle(page.Root.transform, "Poluicao e Meio Ambiente");

        if (economia == null)
        {
            CreateInfoBlock(page.Root.transform, "Dados ambientais indisponiveis: a economia ainda nao foi recalculada.");
            return;
        }

        CreateDescription(page.Root.transform,
            "Estimativa nacional baseada na energia gerada. O percentual de energia mede a matriz do pais; o indice ambiental e um indicador interno de 0 a 100.");

        Text resumo = CreateInfoBlock(page.Root.transform, string.Empty);
        resumo.text = "INDICE DE POLUICAO: " + economia.poluicaoIndice.ToString("0.0") + "/100"
            + "\nEnergia limpa: " + economia.energiaLimpaPercentual.ToString("0.0") + "%"
            + "\nEnergia fossil (carvao): " + economia.energiaFossilPercentual.ToString("0.0") + "%"
            + "\nUsinas solares: " + economia.usinasSolares
            + " | Usinas de carvao: " + economia.usinasCarvao
            + "\nGeracao solar: " + economia.energiaSolarProduzida.ToString("0.0") + " MW"
            + "\nGeracao a carvao: " + economia.energiaCarvaoProduzida.ToString("0.0") + " MW";

        CreateSectionTitle(page.Root.transform, "Emissoes estimadas por dia");
        Text emissoes = CreateInfoBlock(page.Root.transform, string.Empty);
        emissoes.text = "CO2: " + economia.co2ToneladasDia.ToString("N1") + " toneladas/dia"
            + "\nSO2: " + economia.so2KgDia.ToString("N1") + " kg/dia"
            + "\nNOx: " + economia.noxKgDia.ToString("N1") + " kg/dia"
            + "\nParticulas finas (PM): " + economia.particulasKgDia.ToString("N1") + " kg/dia";

        CreateSectionTitle(page.Root.transform, "Impacto no tesouro");
        Text custo = CreateInfoBlock(page.Root.transform, string.Empty);
        custo.text = "CUSTO DAS USINAS DE CARVAO: -$" + FormatNumber(economia.custoUsinasCarvaoPorDia) + "/dia"
            + "\nEste valor inclui o custo operacional da usina, combustivel, filtros, cinzas e controle ambiental."
            + "\nSem usina de carvao, este custo fica em $0/dia.";

        CreateDescription(page.Root.transform,
            "Base do modelo: fator medio de CO2 do carvao publicado pela EIA e fatores de emissao atmosferica de referencia AP-42/EPA. Valores reais variam por combustivel, eficiencia e filtros da usina; nao sao leitura de sensor.");
    }

    private void BuildCidadesRows(Transform parent)
    {
        GerenciadorDivisaoTerritorial.GarantirInstancia();
        var lista = GerenciadorDivisaoTerritorial.Instancia.cidades;
        
        if (lista.Count == 0)
        {
            CreateInfoBlock(parent, "Nenhuma cidade ou estado detectado no mapa. Construa uma prefeitura ou base militar.");
            return;
        }

        foreach (var c in lista)
        {
            string tipoStr = c.ehEstado ? "Capital (Estado)" : "Cidade (Distrito)";
            string aeroStr = c.temAeroporto ? "Sim 🛫" : "Não";
            string donoStr = c.teamID == 1 ? "Jogador" : (c.teamID > 1 ? "IA (" + c.teamID + ")" : "Neutro");

            GameObject row = CreatePanel("CidadeRow_" + c.id, parent, 38f, corCard);
            HorizontalLayoutGroup hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(8, 8, 4, 4);
            hLayout.spacing = 10;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            // Nome da Cidade (Botão de Seleção)
            Button btn = CreateMiniActionButton(row.transform, c.nome, c.id == cidadeSelecionadaId ? corDestaque : corAzulBotao, () =>
            {
                cidadeSelecionadaId = c.id;
                RefreshDynamicData(true);
            });
            btn.GetComponent<LayoutElement>().preferredWidth = 140f;

            // Tipo
            CreateLayoutText(row.transform, tipoStr, 11, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 30f)
                .GetComponent<LayoutElement>().preferredWidth = 100f;

            // Pop. Civil
            CreateLayoutText(row.transform, c.populacaoCivil.ToString("N0"), 11, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Normal, 30f)
                .GetComponent<LayoutElement>().preferredWidth = 80f;

            // Aeroporto
            CreateLayoutText(row.transform, aeroStr, 11, c.temAeroporto ? corVerde : corTextoApagado, TextAnchor.MiddleLeft, FontStyle.Normal, 30f)
                .GetComponent<LayoutElement>().preferredWidth = 80f;

            // Dono
            CreateLayoutText(row.transform, donoStr, 11, c.teamID == 1 ? corVerde : (c.teamID > 1 ? corVermelho : corTextoApagado), TextAnchor.MiddleRight, FontStyle.Bold, 30f)
                .GetComponent<LayoutElement>().preferredWidth = 80f;
        }
    }

    private void BuildDefensePage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            SistemaGovernoMundial gov = Government();
            DadosPaisGoverno p = GetPlayerGov();
            int wars = gov != null ? gov.Paises.Count(x => x != null && x.emGuerra) : 0;
            CreateSectionTitle(page.Root.transform, "Defesa");
            if (p == null)
            {
                CreateInfoBlock(page.Root.transform, "Comando indisponivel.");
                return;
            }

            if (tab == 4)
            {
                CreateDescription(page.Root.transform, "Alertas militares, pressao global de guerra e pedidos de apoio.");
                BuildDefenseAlertRows(page.Root.transform, p, gov, wars);
                return;
            }

            string ramo = tab == 1 ? "Exercito" : tab == 2 ? "Marinha" : tab == 3 ? "Aerea" : "Comando";
            Text stats = CreateInfoBlock(page.Root.transform, string.Empty);
            stats.text = ramo
                + "\nArmamentos: " + FormatNumber(p.armamentos)
                + "\nUranio: " + FormatNumber(p.uranio)
                + "\nPressao de guerra: " + (gov != null ? (gov.PressaoGlobalGuerra() * 100f).ToString("0") + "%" : "n/d")
                + "\nPaises em guerra: " + wars
                + "\nPlano atual: " + p.planoEstrategico;

            if (tab == 3 || tab == 0)
            {
                CreateDescription(page.Root.transform, "Ramo aereo e orbital com foco em manutencao, cobertura e desempenho do satelite nacional.");
                CreateDefenseSatelliteCard(page.Root.transform, p);
            }
            else if (tab == 1)
            {
                CreateDescription(page.Root.transform, "Prontidao terrestre, estoques de armamento e capacidade de resposta do exercito.");
            }
            else if (tab == 2)
            {
                CreateDescription(page.Root.transform, "Capacidade naval, projecao maritima e cobertura de municao costeira.");
            }
        };
        page.Refresh();
    }

    private void BuildSciencePage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosPaisGoverno p = GetPlayerGov();
            CreateSectionTitle(page.Root.transform, "Ciencia");
            if (p == null)
            {
                CreateInfoBlock(page.Root.transform, "Pesquisa indisponivel.");
                return;
            }

            SistemaGovernoMundial gov = Government();
            if (gov == null)
            {
                CreateInfoBlock(page.Root.transform, "Sistema cientifico indisponivel.");
                return;
            }

            string sugestao = p.deficitEnergia > 0f ? "Energia" : p.comida < 300 ? "Comida" : p.nivelIndustrial < p.nivelMilitar ? "Industria" : "Diplomacia";
            Text stats = CreateInfoBlock(page.Root.transform, string.Empty);
            stats.text = "Nivel economico: " + p.nivelEconomico
                + "\nNivel industrial: " + p.nivelIndustrial
                + "\nNivel diplomatico: " + p.nivelDiplomatico
                + "\nNivel militar: " + p.nivelMilitar
                + "\nEnergia disponivel: " + FormatNumber(p.energia)
                + "\nComida em estoque: " + FormatNumber(p.comida)
                + "\nMortos acumulados: " + FormatNumber(p.mortosAcumulados)
                + "\nPrioridade sugerida: " + sugestao;

            if (tab == 0)
            {
                CreateDescription(page.Root.transform, "Pesquisas clicaveis com custo, tempo em dias e desbloqueios reais para o programa cientifico.");
                foreach (PesquisaNacionalEstado pesquisa in p.pesquisas)
                {
                    CreateScienceResearchCard(page.Root.transform, pesquisa);
                }
                return;
            }

            if (tab == 1)
            {
                CreateDescription(page.Root.transform, "Tecnologias permanentes por nivel, com investimento progressivo e efeitos nacionais.");
                foreach (TecnologiaNacionalEstado tecnologia in p.tecnologias)
                {
                    CreateScienceTechnologyCard(page.Root.transform, tecnologia);
                }
                return;
            }

            if (tab == 2)
            {
                BuildScienceProjectsCards(page.Root.transform, p);
                return;
            }

            CreateDescription(page.Root.transform, "Laboratorios nacionais interativos ligados a eficiencia, energia e teto de pesquisa.");
            foreach (LaboratorioNacionalEstado laboratorio in p.laboratorios)
            {
                CreateScienceLabCard(page.Root.transform, laboratorio);
            }
        };
        page.Refresh();
    }

    private void BuildWorkPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosPaisGoverno p = GetPlayerGov();
            CreateSectionTitle(page.Root.transform, "Trabalho");
            if (p == null)
            {
                CreateInfoBlock(page.Root.transform, "Mercado de trabalho indisponivel.");
                return;
            }

            string foco = tab == 1 ? "Setores produtivos" : tab == 2 ? "Formacao" : tab == 3 ? "Politicas" : "Empregos";
            Text stats = CreateInfoBlock(page.Root.transform, string.Empty);
            stats.text = foco
                + "\nEmprego: " + p.emprego.ToString("0") + "%"
                + "\nProducao: " + p.producao.ToString("0") + "%"
                + "\nPeso industria: " + (p.pesoIndustria * 100f).ToString("0") + "%"
                + "\nMoradia: " + p.moradia.ToString("0") + "%"
                + "\nPlano atual: " + p.planoEstrategico;
        };
        page.Refresh();
    }

    private void BuildCulturePage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosCulturaNacional cultura = SistemaCulturaEntretenimento.ObterResumo(paisJogadorId);
            CreateSectionTitle(page.Root.transform, "Diversao, Cultura e Turismo");
            CreateDescription(page.Root.transform, "Estruturas caras e automaticas: o sistema mede publico, eventos, turismo, energia e retorno nacional.");
            Text resumo = CreateInfoBlock(page.Root.transform, string.Empty);
            resumo.text = "Total de estruturas: " + cultura.totalEstruturas
                + "\nAtivas / fechadas: " + cultura.estruturasAtivas + " / " + cultura.estruturasFechadas
                + "\nEstadios: " + cultura.estadios + " | Museus: " + cultura.museus + " | Torres: " + cultura.torres
                + "\nParques: " + cultura.parques + " | Arenas: " + cultura.arenas + " | Monumentos: " + cultura.monumentos
                + "\nCapacidade de visitantes: " + cultura.capacidadeTotalVisitantes.ToString("N0")
                + "\nVisitantes atuais: " + cultura.visitantesAtuais.ToString("N0")
                + "\nTuristas nacionais: " + cultura.turistasNacionais.ToString("N0")
                + "\nTuristas internacionais: " + cultura.turistasInternacionais.ToString("N0")
                + "\nEmpregos permanentes / temporarios: " + cultura.empregosPermanentes.ToString("N0") + " / " + cultura.empregosTemporarios.ToString("N0")
                + "\nEventos em andamento: " + cultura.eventosEmAndamento
                + "\nReceita direta / indireta: $" + cultura.receitaIngressos.ToString("N0") + " / $" + cultura.receitaTuristicaIndireta.ToString("N0")
                + "\nImpostos gerados: $" + cultura.impostosGerados.ToString("N0")
                + "\nManutencao diaria: $" + cultura.custoManutencaoDiario.ToString("N0")
                + "\nEnergia consumida: " + cultura.consumoEnergia.ToString("0") + " MW"
                + "\nFelicidade: +" + cultura.contribuicaoFelicidade.ToString("0.0") + " | Atratividade: +" + cultura.atratividadeTuristica.ToString("0.0") + "%"
                + "\nPrestigio nacional: " + cultura.prestigioNacional.ToString("0.0")
                + "\nPrincipal parada: " + cultura.principalMotivoParada;

            if (tab == 1)
            {
                CreateSectionTitle(page.Root.transform, "Estrutura selecionada");
                EstruturaCulturaEntretenimento selecionada = EstruturaCulturaEntretenimento.Selecionada;
                CreateInfoBlock(page.Root.transform, selecionada != null ? selecionada.GerarDetalhe() : "Clique em uma estrutura no mapa para ver capacidade, empregos, receita e proximo evento.");
            }
            else if (tab == 2)
            {
                CreateSectionTitle(page.Root.transform, "Agenda automatica");
                CreateInfoBlock(page.Root.transform, cultura.eventosEmAndamento > 0
                    ? "Evento em andamento: " + cultura.proximoEvento + "\nO comercio local recebe visitantes e faturamento adicional."
                    : "Nenhum evento em andamento. O proximo evento sera escolhido conforme publico, seguranca e prestígio.");
            }
        };
        page.Refresh();
    }

    private void BuildCultureActionsPage(PageView page)
    {
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            DadosCulturaNacional cultura = SistemaCulturaEntretenimento.ObterResumo(paisJogadorId);
            CreateSectionTitle(page.Root.transform, "Politica cultural");
            CreateDescription(page.Root.transform, "O governo define o momento do investimento; eventos e ocupacao sao administrados automaticamente.");
            CreateInfoBlock(page.Root.transform, "Capacidade de atrair moradores: " + cultura.capacidadeAtracao.ToString("0.0") + "%"
                + "\nObras monumentais: " + cultura.obrasMonumentais
                + "\nEstruturas em prejuizo: " + cultura.estruturasPrejuizo
                + "\nRecomendacao: " + (cultura.totalEstruturas == 0 ? "construa primeiro uma estrutura local" : cultura.estruturasFechadas > 0 ? "melhore energia, transporte ou seguranca" : "rede cultural sustentavel"));
            CreateActionButton(page.Root.transform, "ATUALIZAR AGENDA", corAzulBotao, () => RefreshDynamicData(true));
        };
        page.Refresh();
    }

    private void BuildDiplomacyActionsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            CreateSectionTitle(page.Root.transform, "Diplomacia");
            Text selected = CreateInfoBlock(page.Root.transform, string.Empty);
            CreateCountrySelector(page.Root.transform);
            DadosPaisGoverno p = Government()?.ObterPais(paisSelecionadoId);
            RelacaoPaisGoverno r = Government()?.ObterRelacao(paisJogadorId, paisSelecionadoId);
            selected.text = p == null
                ? "Selecione um pais."
                : p.nomePais + "\nRelacao: " + (r != null ? r.valor.ToString() : "0")
                  + "\nBloco: " + p.bloco
                  + "\nStatus: " + StatusGov(p)
                  + "\nPlano IA: " + p.planoEstrategico;

            if (tab == 3)
            {
                PropostaInternacional proposta = PendingPlayerProposalList().FirstOrDefault();
                if (proposta != null)
                {
                    CreateActionButton(page.Root.transform, "ACEITAR PEDIDO", corVerde, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Aceita, "Proposta"));
                    CreateActionButton(page.Root.transform, "ENVIAR CONTRAOFERTA", corAzulBotao, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Negociando, "Proposta"));
                    CreateActionButton(page.Root.transform, "RECUSAR PEDIDO", corVermelho, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Recusada, "Proposta"));
                }
                else
                {
                    CreateInfoBlock(page.Root.transform, "Nenhum pedido pendente para resposta imediata.");
                }
                return;
            }

            CreateActionButton(page.Root.transform, "PROPOR ALIANCA", corAzulBotao, () =>
            {
                Government()?.ProporAlianca(paisSelecionadoId);
                Notificar("Diplomacia", "Alianca proposta para " + CountryName(paisSelecionadoId) + ".");
                RefreshDynamicData(true);
            });
            CreateActionButton(page.Root.transform, "PACTO DEFENSIVO", corPainel2, () =>
            {
                bool ok = Government() != null && Government().CriarPropostaJogador(paisSelecionadoId, TipoPropostaInternacional.PactoDefensivo, RecursoMercado.Armamentos, 40, 1, "Pacto defensivo solicitado.");
                Notificar("Diplomacia", ok ? "Pacto defensivo enviado para analise." : "Ja existe negociacao parecida em andamento.");
                RefreshDynamicData(true);
            });
            CreateActionButton(page.Root.transform, "PEDIR AJUDA", corVerde, () =>
            {
                EnviarPropostaRecurso(paisSelecionadoId, TipoPropostaInternacional.PedidoAjuda, RecursoMercado.Comida, 120, "Pedido de ajuda humanitaria");
            });
            CreateActionButton(page.Root.transform, "OFERECER COMIDA", new Color(0.120f, 0.220f, 0.150f, 1f), () =>
            {
                EnviarPropostaRecurso(paisSelecionadoId, TipoPropostaInternacional.Venda, RecursoMercado.Comida, 100, "Oferta de comida no corredor aliado");
            });
            CreateActionButton(page.Root.transform, "ROMPER ALIANCA", new Color(0.330f, 0.080f, 0.070f, 1f), () =>
            {
                Government()?.RomperAlianca(paisSelecionadoId);
                Notificar("Diplomacia", "Alianca rompida.");
                RefreshDynamicData(true);
            });
        };
        page.Refresh();
    }

    private void BuildSanctionActionsPage(PageView page)
    {
        CreateSectionTitle(page.Root.transform, "Sancoes");
        Text selected = CreateInfoBlock(page.Root.transform, string.Empty);
        CreateCountrySelector(page.Root.transform);
        CreateActionButton(page.Root.transform, "APLICAR SANCAO", new Color(0.420f, 0.120f, 0.080f, 1f), () =>
        {
            Government()?.AplicarSancao(paisSelecionadoId);
            Notificar("Sancoes", "Sancao aplicada.");
            RefreshDynamicData(true);
        });
        CreateActionButton(page.Root.transform, "REMOVER SANCAO", corAzulBotao, () =>
        {
            Government()?.RemoverSancao(paisSelecionadoId);
            Notificar("Sancoes", "Sancao removida.");
            RefreshDynamicData(true);
        });
        CreateActionButton(page.Root.transform, "PEDIR EMPRESTIMO DE AJUSTE", corAmarelo, () =>
        {
            string mensagem = "federacao indisponivel";
            if (SistemaFederacoesGlobais.Instancia == null)
            {
                mensagem = "federacao indisponivel";
            }
            else
            {
                bool aceito = SistemaFederacoesGlobais.Instancia.SolicitarEmprestimo(paisSelecionadoId, paisJogadorId, 2500f, false, out mensagem);
            }
            Notificar("Federacao", mensagem);
            RefreshDynamicData(true);
        });
        CreateActionButton(page.Root.transform, "PEDIR CREDITO MILITAR", corLaranja, () =>
        {
            string mensagem = "federacao indisponivel";
            if (SistemaFederacoesGlobais.Instancia == null)
            {
                mensagem = "federacao indisponivel";
            }
            else
            {
                bool aceito = SistemaFederacoesGlobais.Instancia.SolicitarEmprestimo(paisSelecionadoId, paisJogadorId, 2500f, true, out mensagem);
            }
            Notificar("Federacao", mensagem);
            RefreshDynamicData(true);
        });
        page.Refresh = () =>
        {
            DadosPaisGoverno p = Government()?.ObterPais(paisSelecionadoId);
            selected.text = p == null ? "Nenhum alvo." : p.nomePais + "\nStatus: " + StatusGov(p) + "\nSancionado: " + (p.sancionado ? "sim" : "nao") + "\nFederacao: " + SistemaFederacoesGlobais.NomeFederacao(p.federacaoGlobal) + "\nLegitimidade: " + p.legitimidadeGlobal.ToString("0");
        };
        page.Refresh();
    }

    private void BuildFederationRows(Transform parent)
    {
        SistemaFederacoesGlobais.GarantirInstancia();
        CreateHeaderRow(parent, new[] { "PAIS", "FEDERACAO", "LEGIT.", "DIVIDA" }, new[] { 1.25f, 1.25f, 0.55f, 0.70f });
        foreach (DadosPaisGoverno p in Government() != null ? Government().Paises.OrderBy(x => x.teamId) : Enumerable.Empty<DadosPaisGoverno>())
        {
            if (p == null) continue;
            GameObject row = CreateRow(parent, "Federacao_" + p.teamId, 34f);
            // Each row is a horizontal table row. Without the layout group the
            // labels keep their default rect (all at the same origin), which
            // makes the federation columns overlap in the live menu.
            SetupRow(row);
            CreateFlexText(row.transform, p.nomePais, 11, corTextoPrimario, 1.25f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, SistemaFederacoesGlobais.NomeFederacao(p.federacaoGlobal), 10, corTextoSecundario, 1.25f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, p.legitimidadeGlobal.ToString("0"), 11, p.legitimidadeGlobal < 35f ? corVermelho : corVerde, 0.55f, TextAnchor.MiddleCenter);
            float divida = p.emprestimos == null ? 0f : p.emprestimos.Sum(x => x != null ? x.saldoDevedor : 0f);
            CreateFlexText(row.transform, divida.ToString("0"), 10, divida > 0f ? corAmarelo : corTextoApagado, 0.70f, TextAnchor.MiddleRight);
        }
    }

    private void BuildLegitimacyRows(Transform parent)
    {
        CreateDescription(parent, "Crimes de guerra, sancoes e trocas de bloco reduzem legitimidade; ajuda e consenso recuperam apoio.");
        BuildFederationRows(parent);
    }

    private void BuildLoanRows(Transform parent)
    {
        DadosPaisGoverno p = GetPlayerGov();
        if (p == null || p.emprestimos == null || p.emprestimos.Count == 0)
        {
            CreateInfoBlock(parent, "Nenhum emprestimo ativo.");
            return;
        }
        foreach (EmprestimoFederativoEstado loan in p.emprestimos)
        {
            if (loan == null) continue;
            GameObject row = CreateRow(parent, "Emprestimo_" + loan.id, 52f);
            SetupRow(row);
            CreateFlexText(row.transform, loan.id + "\nCredor: " + loan.credorTeamId, 10, corTextoPrimario, 1.3f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, loan.saldoDevedor.ToString("0") + "\n" + (loan.inadimplente ? "INADIMPLENTE" : "ATIVO"), 10, loan.inadimplente ? corVermelho : corAmarelo, 1f, TextAnchor.MiddleLeft);
            CreateActionButton(row.transform, "QUITAR", corVerde, () =>
            {
                string mensagem = "emprestimo indisponivel";
                if (SistemaFederacoesGlobais.Instancia == null)
                {
                    mensagem = "emprestimo indisponivel";
                }
                else
                {
                    bool quitado = SistemaFederacoesGlobais.Instancia.QuitarEmprestimo(p.teamId, loan.id, out mensagem);
                }
                Notificar("Emprestimo", mensagem);
                RefreshDynamicData(true);
            });
        }
    }

    private void BuildEconomyActionsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            CreateSectionTitle(page.Root.transform, "Economia");
            DadosPaisGoverno p = GetPlayerGov();
            Text info = CreateInfoBlock(page.Root.transform, p == null ? "Tesouro indisponivel." : "Poder de compra: " + p.PoderDeCompra.ToString("0.00") + "\nEstabilidade: " + p.estabilidade.ToString("0") + "%\nSaldo operacional: " + SignedRate(p.saldoOperacional));
            if (tab == 3)
            {
                CreateActionButton(page.Root.transform, "MORADIA -5%", corPainel2, () => AjustarImpostoUI("moradia", -1));
                CreateActionButton(page.Root.transform, "MORADIA +5%", corAzulBotao, () => AjustarImpostoUI("moradia", 1));
                CreateActionButton(page.Root.transform, "INDUSTRIA -5%", corPainel2, () => AjustarImpostoUI("industria", -1));
                CreateActionButton(page.Root.transform, "INDUSTRIA +5%", corAzulBotao, () => AjustarImpostoUI("industria", 1));
                CreateActionButton(page.Root.transform, "COMERCIO -5%", corPainel2, () => AjustarImpostoUI("comercio", -1));
                CreateActionButton(page.Root.transform, "COMERCIO +5%", corAzulBotao, () => AjustarImpostoUI("comercio", 1));
                return;
            }

            CreateActionButton(page.Root.transform, "GERAR EMPREGOS", corAzulBotao, () =>
            {
                Government()?.AlterarEmprego(paisJogadorId, 4f);
                Notificar("Economia", "Programa de emprego ativado.");
                RefreshDynamicData(true);
            });
            CreateActionButton(page.Root.transform, "INVESTIR EM MORADIA", corPainel2, () =>
            {
                Government()?.AlterarMoradia(paisJogadorId, 4f);
                Notificar("Economia", "Investimento em moradia aplicado.");
                RefreshDynamicData(true);
            });
            CreateActionButton(page.Root.transform, "COMPRAR COMIDA", corVerde, () => ExecuteBuy("comida"));
            CreateActionButton(page.Root.transform, "COMPRAR PETROLEO", corLaranja, () => ExecuteBuy("petroleo"));
            CreateActionButton(page.Root.transform, "VENDER ENERGIA (EXCESSO)", corVerde, () => SellRealResource("energia", 50));
            CreateActionButton(page.Root.transform, "COMPRAR ENERGIA (EMERGENCIA)", corVermelho, () => ExecuteBuy("energia"));
        };
        page.Refresh();
    }

    private void BuildInteriorActionsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            if (tab == 1) // Cidades e Estados
            {
                CreateSectionTitle(page.Root.transform, "Editar Território");
                
                GerenciadorDivisaoTerritorial.GarantirInstancia();
                var selecionada = GerenciadorDivisaoTerritorial.Instancia.cidades.FirstOrDefault(c => c.id == cidadeSelecionadaId);
                
                if (selecionada == null)
                {
                    CreateInfoBlock(page.Root.transform, "Selecione uma cidade na lista à esquerda para editar seu nome ou ver estatísticas detalhadas.");
                    return;
                }

                string tipoStr = selecionada.ehEstado ? "Capital (Estado)" : "Cidade (Distrito)";
                string aeroStr = selecionada.temAeroporto ? "Sim" : "Nenhum";
                string donoStr = selecionada.teamID == 1 ? "Jogador" : "IA";

                Text info = CreateInfoBlock(page.Root.transform, string.Empty);
                info.text = "Nome: " + selecionada.nome
                    + "\nTipo: " + tipoStr
                    + "\nJurisdição: " + donoStr
                    + "\nPop. Civil: " + selecionada.populacaoCivil.ToString("N0")
                    + "\nAeroporto: " + aeroStr
                    + "\nLocalização: " + selecionada.marcador.transform.position.ToString("F0");

                CreateDescription(page.Root.transform, "Alterar nome do território:");
                InputField inputNome = CreateCompactInput(page.Root.transform, selecionada.nome);
                
                CreateActionButton(page.Root.transform, "SALVAR NOME", corVerde, () =>
                {
                    if (inputNome != null && !string.IsNullOrWhiteSpace(inputNome.text))
                    {
                        GerenciadorDivisaoTerritorial.Instancia.RenomearCidade(selecionada.id, inputNome.text);
                        Notificar("Divisão", "Território renomeado para " + inputNome.text);
                        RefreshDynamicData(true);
                    }
                });

                if (selecionada.temAeroporto)
                {
                    CreateActionButton(page.Root.transform, "ROTAS CIVIS E LOGISTICA", corAzulBotao, () =>
                    {
                        bool ok = InvestirSetorUI("Logistica", 650, "Logistica", string.Empty);
                        Notificar("Aeroporto", ok ? "Rotas civis fortalecidas para " + selecionada.nome + "." : "Nao foi possivel financiar as rotas civis.");
                    });
                }
            }
            else
            {
                CreateSectionTitle(page.Root.transform, "Acoes internas");
                Text info = CreateInfoBlock(page.Root.transform, "Acoes civis de baixo custo visual.");
                info.text = "Foco atual: " + MainDeficit(GetPlayerGov());
                CreateActionButton(page.Root.transform, "MELHORAR MORADIA", corAzulBotao, () =>
                {
                    Government()?.AlterarMoradia(paisJogadorId, 3f);
                    Notificar("Interior", "Moradia melhorada.");
                    RefreshDynamicData(true);
                });
                CreateActionButton(page.Root.transform, "MUTIRAO DE EMPREGOS", corPainel2, () =>
                {
                    Government()?.AlterarEmprego(paisJogadorId, 3f);
                    Notificar("Interior", "Empregos estimulados.");
                    RefreshDynamicData(true);
                });
            }
        };
        page.Refresh();
    }

    private void BuildDefenseActionsPage(PageView page)
    {
        int tab = subAbaAtualIndex;
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            string titulo = tab == 3 ? "Aerea / Orbital" : tab == 2 ? "Marinha" : tab == 1 ? "Exercito" : "Comando";
            CreateSectionTitle(page.Root.transform, titulo);
            CreateInfoBlock(page.Root.transform, tab == 3
                ? "Controles aereos e orbitais conectados ao satelite, manutencao e cobertura nacional."
                : "Acoes militares conectadas ao estado geopolitico e aos pedidos da IA.");
            DadosPaisGoverno p = GetPlayerGov();
            if (p != null && (tab == 0 || tab == 3))
            {
                CreateDefenseSatelliteCard(page.Root.transform, p);
            }
            CreateCountrySelector(page.Root.transform);
            CreateActionButton(page.Root.transform, "DECLARAR ALERTA", new Color(0.420f, 0.150f, 0.080f, 1f), () =>
            {
                Government()?.NotificarGuerra(paisSelecionadoId);
                Notificar("Defesa", "Alerta militar emitido.");
                RefreshDynamicData(true);
            });
            if (tab == 4 || tab == 0)
            {
                CreateActionButton(page.Root.transform, "PEDIR APOIO MILITAR", corAzulBotao, () =>
                {
                    EnviarPropostaRecurso(paisSelecionadoId, TipoPropostaInternacional.PedidoAjuda, RecursoMercado.Armamentos, 80, "Pedido de apoio militar");
                });
            }
            CreateActionButton(page.Root.transform, "PACTO DEFENSIVO DIRETO", corPainel2, () =>
            {
                bool ok = Government() != null && Government().CriarPropostaJogador(paisSelecionadoId, TipoPropostaInternacional.PactoDefensivo, RecursoMercado.Armamentos, 30, 1, "Solicitacao de pacto defensivo.");
                Notificar("Defesa", ok ? "Pedido de pacto enviado." : "Ja existe proposta militar semelhante.");
                RefreshDynamicData(true);
            });
        };
        page.Refresh();
    }

    private void BuildScienceActionsPage(PageView page)
    {
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            CreateSectionTitle(page.Root.transform, "Pesquisa");
            DadosPaisGoverno p = GetPlayerGov();
            if (p != null)
            {
                CreateScienceSummaryCard(page.Root.transform, p);
            }
            CreateInfoBlock(page.Root.transform, "Cada card abaixo aplica investimento real, muda o cofre nacional e conversa com pesquisa, tecnologia, projetos e laboratorios.");
            CreateActionButton(page.Root.transform, "INVESTIR EM ENERGIA", corAzulBotao, () => InvestirSetorUI("Energia", 750, "Energia"));
            CreateActionButton(page.Root.transform, "INVESTIR EM INDUSTRIA", corPainel2, () => InvestirSetorUI("Industria", 700, "Industria"));
            CreateActionButton(page.Root.transform, "INVESTIR EM DIPLOMACIA", new Color(0.110f, 0.200f, 0.280f, 1f), () => InvestirSetorUI("Diplomacia", 600, "Diplomacia"));
            CreateActionButton(page.Root.transform, "INVESTIR EM DEFESA", new Color(0.280f, 0.130f, 0.120f, 1f), () => InvestirSetorUI("Defesa", 850, "Defesa"));
        };
        page.Refresh();
    }

    private void BuildWorkActionsPage(PageView page)
    {
        page.Refresh = () =>
        {
            ClearChildren(page.Root.transform);
            CreateSectionTitle(page.Root.transform, "Trabalho");
            CreateInfoBlock(page.Root.transform, "Politicas ligadas a emprego, moradia e foco produtivo.");
            CreateActionButton(page.Root.transform, "CAPACITACAO", corAzulBotao, () =>
            {
                Government()?.AlterarEmprego(paisJogadorId, 2.5f);
                DefinirPlanoUI("Capacitacao");
            });
            CreateActionButton(page.Root.transform, "POLITICA INDUSTRIAL", corPainel2, () =>
            {
                Government()?.AlterarEmprego(paisJogadorId, 1.5f);
                DefinirPlanoUI("Industria");
            });
            CreateActionButton(page.Root.transform, "MORADIA OPERARIA", new Color(0.120f, 0.200f, 0.160f, 1f), () =>
            {
                Government()?.AlterarMoradia(paisJogadorId, 2f);
                DefinirPlanoUI("Moradia");
            });
            CreateActionButton(page.Root.transform, "MOBILIZAR SETORES", new Color(0.280f, 0.130f, 0.120f, 1f), () =>
            {
                Government()?.AlterarEmprego(paisJogadorId, 1f);
                Government()?.AlterarMoradia(paisJogadorId, -0.5f);
                DefinirPlanoUI("Mobilizacao");
            });
        };
        page.Refresh();
    }

    private void CreateCountrySelector(Transform parent)
    {
        List<DadosPaisGoverno> paisesOrdenados = OrderedCountries(false);
        if (paisesOrdenados.Count == 0) return;
        if (paisesOrdenados.All(p => p.teamId != paisSelecionadoId))
            paisSelecionadoId = paisesOrdenados[0].teamId;

        Transform content;
        ScrollRect scroll = CreateScrollPanel(parent, "SeletorPais", 0f, 0f, 1f, out content);
        scroll.GetComponent<LayoutElement>().minHeight = 150f;
        scroll.GetComponent<Image>().color = corCard;

        foreach (DadosPaisGoverno pais in paisesOrdenados)
        {
            int id = pais.teamId;
            RelacaoPaisGoverno rel = Government()?.ObterRelacao(paisJogadorId, id);
            Button b = CreateActionButton(content, pais.nomePais.ToUpperInvariant() + " | " + (rel != null ? rel.valor.ToString() : "0"), id == paisSelecionadoId ? corAbaAtiva : corPainel2, () =>
            {
                paisSelecionadoId = id;
                RefreshDynamicData(true);
            });
            b.GetComponent<LayoutElement>().preferredHeight = 34f;
        }
    }

    private void RebuildSimpleCountryRows(Transform parent, string mode)
    {
        Transform list = parent.Find("ListaPaises");
        if (list == null)
        {
            GameObject root = CreateUIObject("ListaPaises", parent);
            root.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            list = root.transform;
        }

        ClearChildren(list);
        SistemaGovernoMundial gov = Government();
        if (gov == null) return;

        foreach (DadosPaisGoverno p in OrderedCountries(false).Take(12))
        {
            RelacaoPaisGoverno r = gov.ObterRelacao(paisJogadorId, p.teamId);
            GameObject row = CreatePanel("Pais_" + p.teamId, list, 44f, p.teamId == paisSelecionadoId ? corAbaAtiva : corCard);
            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 5, 5);
            h.spacing = 8;
            h.childControlHeight = true;
            h.childControlWidth = true;
            CreateFlexText(row.transform, p.nomePais, 13, corTextoPrimario, 1.35f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, mode == "Sancao" ? (p.sancionado ? "SANCIONADO" : "LIVRE") : p.bloco, 11, StatusColor(p), 0.9f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, r != null ? r.valor.ToString() : "-", 12, r != null ? RelationColor(r.valor) : corTextoSecundario, 0.45f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, StatusGov(p), 11, StatusColor(p), 0.75f, TextAnchor.MiddleRight);
            int id = p.teamId;
            Button b = row.AddComponent<Button>();
            b.onClick.AddListener(() =>
            {
                paisSelecionadoId = id;
                RefreshDynamicData(true);
            });
        }
    }

    private List<DadosPaisGoverno> OrderedCountries(bool includePlayer)
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null) return new List<DadosPaisGoverno>();

        IEnumerable<DadosPaisGoverno> query = gov.Paises
            .Where(p => p != null && (includePlayer || p.teamId != paisJogadorId))
            .GroupBy(p => p.teamId)
            .Select(g => g.First());

        return query
            .OrderByDescending(p =>
            {
                if (p.teamId == paisJogadorId) return 101;
                RelacaoPaisGoverno rel = gov.ObterRelacao(paisJogadorId, p.teamId);
                return rel != null ? rel.valor : -999;
            })
            .ThenByDescending(p => p.estabilidade)
            .ThenBy(p => p.nomePais)
            .ToList();
    }

    private DadosEconomiaPais PlayerEconomy()
    {
        return SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(paisJogadorId) : null;
    }

    private IEnumerable<PropostaInternacional> PendingPlayerProposalList()
    {
        SistemaGovernoMundial gov = Government();
        return gov != null
            ? gov.ObterPropostasPendentesPara(paisJogadorId).OrderByDescending(p => p.prioridade).ThenByDescending(p => p.criadaEm)
            : Enumerable.Empty<PropostaInternacional>();
    }

    private void BuildNationOverviewRows(Transform parent, IEnumerable<DadosPaisGoverno> countries)
    {
        foreach (DadosPaisGoverno p in countries.Take(10))
        {
            GameObject row = CreateRow(parent, "Nacao_" + p.teamId, 54f);
            SetupRow(row);
            CreateFlexText(row.transform, p.nomePais, 12, corTextoPrimario, 1.15f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, p.bloco, 10, corTextoSecundario, 0.95f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, "Eco " + p.PontuacaoEconomica().ToString("0"), 11, corDestaque, 0.55f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, "Est. " + p.estabilidade.ToString("0") + "%", 11, StatusColor(p), 0.65f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, StatusGov(p), 11, StatusColor(p), 0.7f, TextAnchor.MiddleRight);
        }
    }

    private void BuildTreatyRows(Transform parent, bool allianceOnly)
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null) return;
        CreateHeaderRow(parent, new[] { "PAIS", "PACTO", "COMERCIO", "REL", "STATUS" }, new[] { 1.2f, 0.85f, 0.8f, 0.45f, 0.65f });
        foreach (DadosPaisGoverno p in OrderedCountries(false))
        {
            RelacaoPaisGoverno rel = gov.ObterRelacao(paisJogadorId, p.teamId);
            if (rel == null) continue;
            if (allianceOnly && !rel.pactoMilitar && !rel.pedidoPendente) continue;
            if (!allianceOnly && !rel.tratadoComercial && !rel.pactoMilitar) continue;

            GameObject row = CreateRow(parent, "Tratado_" + p.teamId, 42f);
            SetupRow(row);
            CreateFlexText(row.transform, p.nomePais, 12, corTextoPrimario, 1.2f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, rel.pactoMilitar ? "Ativo" : rel.pedidoPendente ? "Pedido" : "-", 11, rel.pactoMilitar ? corVerde : corAmarelo, 0.85f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, rel.tratadoComercial ? "Aberto" : "Suspenso", 11, rel.tratadoComercial ? corDestaque : corVermelho, 0.8f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, rel.valor.ToString(), 11, RelationColor(rel.valor), 0.45f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, StatusGov(p), 11, StatusColor(p), 0.65f, TextAnchor.MiddleRight);
        }
    }

    private void BuildCrisisRows(Transform parent)
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null) return;
        foreach (DadosPaisGoverno p in OrderedCountries(true).Where(x => x.emGuerra || x.sancionado || x.estabilidade < 55f))
        {
            RelacaoPaisGoverno rel = p.teamId != paisJogadorId ? gov.ObterRelacao(paisJogadorId, p.teamId) : null;
            Text box = CreateInfoBlock(parent,
                p.nomePais
                + "\nStatus: " + StatusGov(p)
                + "\nEstabilidade: " + p.estabilidade.ToString("0") + "%"
                + "\nRelacao: " + (rel != null ? rel.valor.ToString() : "-")
                + "\nDeficit: " + MainDeficit(p));
            box.color = corTextoPrimario;
        }
    }

    private void BuildAllianceOperationRows(Transform parent)
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null) return;
        CreateHeaderRow(parent, new[] { "PAIS", "PLANO IA", "NECESSIDADE", "EXCEDENTE" }, new[] { 1.15f, 1.1f, 0.8f, 0.8f });
        foreach (DadosPaisGoverno p in OrderedCountries(false).Take(8))
        {
            GameObject row = CreateRow(parent, "Operacao_" + p.teamId, 44f);
            SetupRow(row);
            CreateFlexText(row.transform, p.nomePais, 12, corTextoPrimario, 1.15f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, p.planoEstrategico, 11, corTextoSecundario, 1.1f, TextAnchor.MiddleLeft);
            CreateFlexText(row.transform, IA_EconomyDirector.ResolveCriticalNeed(p).ToString(), 11, corAmarelo, 0.8f, TextAnchor.MiddleCenter);
            CreateFlexText(row.transform, IA_EconomyDirector.ResolveBestSurplus(p).ToString(), 11, corVerde, 0.8f, TextAnchor.MiddleCenter);
        }
    }

    private void BuildPendingProposalRows(Transform parent, bool includeActions)
    {
        List<PropostaInternacional> propostas = PendingPlayerProposalList().ToList();
        if (propostas.Count == 0)
        {
            CreateInfoBlock(parent, "Nenhuma proposta pendente no momento.");
            return;
        }

        foreach (PropostaInternacional proposta in propostas.Take(8))
        {
            GameObject row = CreateRow(parent, "Proposta_" + proposta.id, includeActions ? 74f : 48f);
            VerticalLayoutGroup v = row.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 6, 6);
            v.spacing = 4;
            v.childControlWidth = true;
            v.childControlHeight = true;
            CreateLayoutText(row.transform, CountryName(proposta.origemTeamId).ToUpperInvariant() + " | " + proposta.tipo.ToString().ToUpperInvariant(), 11, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 18f);
            CreateLayoutText(row.transform, proposta.motivo + " | " + proposta.quantidade + " " + proposta.recurso + " | $" + FormatNumber(proposta.precoUnitario), 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 18f);

            if (!includeActions) continue;

            GameObject actions = CreateUIObject("AcoesProposta", row.transform);
            HorizontalLayoutGroup h = actions.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            CreateSmallButton(actions.transform, "Aceitar", corVerde, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Aceita, "Proposta"));
            CreateSmallButton(actions.transform, "Negociar", corAzulBotao, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Negociando, "Proposta"));
            CreateSmallButton(actions.transform, "Recusar", corVermelho, () => ResolverPropostaUI(proposta.id, StatusPropostaInternacional.Recusada, "Proposta"));
        }
    }

    private void BuildSanctionRows(Transform parent, bool onlySanctioned)
    {
        IEnumerable<DadosPaisGoverno> paisesAlvo = OrderedCountries(true).Where(p => !onlySanctioned || p.sancionado);
        foreach (DadosPaisGoverno p in paisesAlvo.Take(10))
        {
            Text box = CreateInfoBlock(parent,
                p.nomePais
                + "\nStatus: " + StatusGov(p)
                + "\nEstabilidade: " + p.estabilidade.ToString("0") + "%"
                + "\nComercio: " + (p.sancionado ? "Travado" : "Monitorado"));
            box.color = p.sancionado ? corVermelho : corTextoSecundario;
        }
    }

    private void BuildSanctionTypeNotes(Transform parent)
    {
        CreateInfoBlock(parent, "Embargo comercial: reduz troca e piora a relacao diplomatica.");
        CreateInfoBlock(parent, "Pressao energetica: util quando o alvo depende de importacao ou esta com baixa estabilidade.");
        CreateInfoBlock(parent, "Isolamento militar: combina melhor com paises ja em tensao ou guerra.");
    }

    private void BuildNewsRows(Transform parent)
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null || gov.noticias.Count == 0)
        {
            CreateInfoBlock(parent, "Sem historico recente.");
            return;
        }

        foreach (string noticia in gov.noticias.Take(8))
            CreateInfoBlock(parent, noticia);
    }

    private void CreateEconomyBudgetRows(Transform parent, DadosPaisGoverno p)
    {
        CreateInfoBlock(parent,
            "Moradia: +" + Mathf.RoundToInt(p.receitaMoradia) + "/s\n"
            + "Industria: +" + Mathf.RoundToInt(p.receitaIndustria) + "/s\n"
            + "Comercio: +" + Mathf.RoundToInt(p.receitaComercio) + "/s\n"
            + "Energia: +" + Mathf.RoundToInt(p.receitaEnergia) + "/s");
        CreateInfoBlock(parent,
            "Manutencao: -" + Mathf.RoundToInt(p.custoManutencao) + "/s\n"
            + "Manutencao militar cobrada no ultimo dia: -$" + FormatNumber(GestorDeConsumo.Instancia != null ? GestorDeConsumo.Instancia.totalConsumoDinheiro : 0L) + "/dia\n"
            + "Saldo operacional: " + SignedRate(p.saldoOperacional) + "\n"
            + "Divida: $" + FormatNumber(Mathf.RoundToInt(p.divida)));
    }

    private void CreateEconomyProductionRows(Transform parent, DadosPaisGoverno p, DadosEconomiaPais economia)
    {
        if (economia == null)
        {
            CreateInfoBlock(parent, "Snapshot economico indisponivel.");
            return;
        }

        float consumoCivil = p.populacaoCivil / 100f * 1f;
        float consumoMilitar = p.populacaoMilitarAtiva / 100f * 2f;
        float consumoTotalComida = consumoCivil + consumoMilitar;

        CreateInfoBlock(parent,
            "Estoque de comida: " + p.comida.ToString() + " t"
            + "\nComida produzida: " + economia.comidaProduzida.ToString("0.0") + " t"
            + "\nComida consumida: " + consumoTotalComida.ToString("0.0") + " t"
            + "\n   - Consumo populacao civil: " + consumoCivil.ToString("0.0") + " t"
            + "\n   - Consumo forcas militares: " + consumoMilitar.ToString("0.0") + " t"
            + "\nDeficit de comida: " + economia.deficitComida.ToString("0.0") + " t");

        CreateInfoBlock(parent,
            "Energia gerada: " + p.energiaProduzida.ToString("0.0") + " MW"
            + "\nEnergia consumida: " + p.energiaConsumida.ToString("0.0") + " MW"
            + "\nPredios sem energia: " + p.estruturasSemEnergia
            + "\nQualidade de vida: " + p.qualidadeVida.ToString("0") + "%");

        CreateInfoBlock(parent,
            "Petroleo produzido: " + economia.petroleoProduzido.ToString("0.0") + " t"
            + "\nAco produzido: " + economia.industriaProduzida.ToString("0.0") + " t"
            + "\nDeficit principal: " + economia.DeficitPrincipal);
    }

    private void CreateTaxOverviewRows(Transform parent, DadosPaisGoverno p)
    {
        float cargaMedia = (p.impostoMoradia + p.impostoIndustria + p.impostoComercio) / 3f;
        CreateInfoBlock(parent,
            "Moradia: " + p.impostoMoradia + "%\n"
            + "Industria: " + p.impostoIndustria + "%\n"
            + "Comercio: " + p.impostoComercio + "%");
        CreateInfoBlock(parent,
            "Receita prevista: +" + Mathf.RoundToInt(p.rendaPorSegundo) + "/s\n"
            + "Carga fiscal media: " + cargaMedia.ToString("0") + "%\n"
            + "Cambio: 1 " + p.nomeMoeda + " = " + p.cambioComLider.ToString("0.00") + " " + p.moedaLiderReferencia + "\n"
            + "Impacto em estabilidade: " + (cargaMedia > 18f ? "pressao crescente" : "controlado"));
    }

    private void BuildDefenseAlertRows(Transform parent, DadosPaisGoverno p, SistemaGovernoMundial gov, int wars)
    {
        CreateInfoBlock(parent,
            "Armamentos: " + FormatNumber(p.armamentos)
            + "\nUranio: " + FormatNumber(p.uranio)
            + "\nPressao global: " + (gov != null ? (gov.PressaoGlobalGuerra() * 100f).ToString("0") + "%" : "n/d"));
        CreateInfoBlock(parent,
            "Paises em guerra: " + wars
            + "\nPedidos pendentes: " + PendingPlayerProposalList().Count()
            + "\nPlano atual: " + p.planoEstrategico);
    }

    private void ResolverPropostaUI(string propostaId, StatusPropostaInternacional status, string contexto)
    {
        if (Government() == null || string.IsNullOrEmpty(propostaId))
        {
            Notificar(contexto, "Sistema de governo indisponivel.");
            return;
        }

        string mensagem = "Proposta indisponivel.";
        bool ok = Government().ResolverProposta(propostaId, status, out mensagem);
        Notificar(contexto, ok ? mensagem : "Falha ao resolver proposta.");
        RefreshDynamicData(true);
    }

    private void EnviarPropostaRecurso(int alvoId, TipoPropostaInternacional tipo, RecursoMercado recurso, int quantidade, string motivo)
    {
        if (alvoId <= 0 || alvoId == paisJogadorId)
        {
            Notificar("Diplomacia", "Escolha um pais valido para negociar.");
            return;
        }

        SistemaMercadoGlobal mercado = Market();
        DadosItemMercado item = mercado != null ? mercado.ObterItem(SistemaGovernoMundial.IdRecurso(recurso)) : null;
        int preco = item != null ? Mathf.RoundToInt(item.precoAtual * (tipo == TipoPropostaInternacional.PedidoAjuda ? 1f : 0.94f)) : 1;
        bool ok = Government() != null && Government().CriarPropostaJogador(alvoId, tipo, recurso, quantidade, preco, motivo);
        Notificar("Diplomacia", ok ? motivo + "." : "Ja existe proposta semelhante em andamento.");
        RefreshDynamicData(true);
    }

    private void DefinirPlanoUI(string plano)
    {
        if (Government() == null)
        {
            Notificar("Plano nacional", "Sistema de governo indisponivel.");
            return;
        }

        bool mudou = Government().DefinirPlanoEstrategico(paisJogadorId, plano);
        Notificar("Plano nacional", mudou ? plano + " priorizado." : "Esse foco ja esta ativo.");
        RefreshDynamicData(true);
    }

    private bool InvestirSetorUI(string foco, int custo, string plano = null, string categoriaAviso = "Ciencia")
    {
        string canalAviso = string.IsNullOrWhiteSpace(categoriaAviso) ? "Governo" : categoriaAviso;
        if (Government() == null)
        {
            Notificar(canalAviso, "Sistema de governo indisponivel.");
            return false;
        }

        bool ok = Government().InvestirCapacidadeNacional(paisJogadorId, foco, custo);
        if (ok && !string.IsNullOrWhiteSpace(plano))
        {
            Government().DefinirPlanoEstrategico(paisJogadorId, plano);
        }

        if (!string.IsNullOrWhiteSpace(categoriaAviso))
        {
            Notificar(canalAviso, ok ? foco + " financiado." : "Nao foi possivel concluir o investimento.");
        }
        RefreshDynamicData(true);
        return ok;
    }

    private void AjustarImpostoUI(string categoria, int delta)
    {
        if (Government() == null)
        {
            Notificar("Impostos", "Sistema de governo indisponivel.");
            return;
        }

        bool mudou = Government().AjustarImposto(paisJogadorId, categoria, delta);
        Notificar("Impostos", mudou ? categoria + " ajustado em " + (delta > 0 ? "+5%" : "-5%") + "." : "Esse imposto ja esta no limite.");
        RefreshDynamicData(true);
    }

    private MarketBuyRow CreateBuyRow(Transform parent, DadosItemMercado item)
    {
        GameObject row = CreateRow(parent, "Comprar_" + item.id, 42f);
        MarketBuyRow view = new MarketBuyRow { Root = row, Menu = this, ItemId = item.id };
        HorizontalLayoutGroup h = SetupRow(row);
        view.Name = CreateFlexText(row.transform, item.nome, 12, corTextoPrimario, 1.25f, TextAnchor.MiddleLeft);
        view.Stock = CreateFlexText(row.transform, string.Empty, 12, corTextoSecundario, 0.8f, TextAnchor.MiddleRight);
        view.Price = CreateFlexText(row.transform, string.Empty, 12, corVerde, 0.8f, TextAnchor.MiddleRight);
        view.Partner = CreateFlexText(row.transform, string.Empty, 11, corTextoSecundario, 1.2f, TextAnchor.MiddleLeft);
        view.Action = CreateSmallButton(row.transform, "Comprar", corAzulBotao, () => ExecuteBuy(view.ItemId));
        view.Action.GetComponent<LayoutElement>().flexibleWidth = 1f;
        h.enabled = true;
        return view;
    }

    private MarketSellRow CreateSellRow(Transform parent, string itemId)
    {
        GameObject row = CreateRow(parent, "Vender_" + itemId, 54f);
        MarketSellRow view = new MarketSellRow { Root = row, Menu = this, ItemId = itemId };
        HorizontalLayoutGroup h = SetupRow(row);
        view.Name = CreateFlexText(row.transform, DisplayItemName(itemId), 12, corTextoPrimario, 1.1f, TextAnchor.MiddleLeft);
        view.Stock = CreateFlexText(row.transform, string.Empty, 12, corTextoSecundario, 0.8f, TextAnchor.MiddleRight);
        view.Price = CreateFlexText(row.transform, string.Empty, 12, corVerde, 0.8f, TextAnchor.MiddleRight);
        view.Auto = CreateSmallButton(row.transform, "Auto", corPainel2, () => ToggleAutoSell(itemId));
        view.AutoText = view.Auto.transform.Find("Label").GetComponent<Text>();
        view.Auto.GetComponent<LayoutElement>().flexibleWidth = 1.1f;
        GameObject actions = CreateUIObject("Acoes", row.transform);
        actions.AddComponent<LayoutElement>().flexibleWidth = 1.6f;
        HorizontalLayoutGroup ah = actions.AddComponent<HorizontalLayoutGroup>();
        ah.spacing = 4;
        ah.childControlWidth = true;
        ah.childControlHeight = true;
        ah.childForceExpandWidth = true;
        view.Sell50 = CreateSmallButton(actions.transform, "50", corAzulBotao, () => SellMarketResource(itemId, 50));
        view.Sell200 = CreateSmallButton(actions.transform, "200", corAzulBotao, () => SellMarketResource(itemId, 200));
        view.SellAll = CreateSmallButton(actions.transform, "Tudo", new Color(0.360f, 0.100f, 0.070f, 1f), () => SellAllMarketResource(itemId));
        h.enabled = true;
        return view;
    }

    private MarketPriceRow CreatePriceRow(Transform parent, DadosItemMercado item)
    {
        GameObject row = CreateRow(parent, "Preco_" + item.id, 38f);
        MarketPriceRow view = new MarketPriceRow { Root = row };
        SetupRow(row);
        view.Name = CreateFlexText(row.transform, item.nome, 12, corTextoPrimario, 1.25f, TextAnchor.MiddleLeft);
        view.Price = CreateFlexText(row.transform, string.Empty, 12, corVerde, 0.8f, TextAnchor.MiddleRight);
        view.Var = CreateFlexText(row.transform, string.Empty, 12, corTextoSecundario, 0.6f, TextAnchor.MiddleRight);
        view.Offer = CreateFlexText(row.transform, string.Empty, 11, corTextoSecundario, 0.75f, TextAnchor.MiddleRight);
        view.Demand = CreateFlexText(row.transform, string.Empty, 11, corTextoSecundario, 0.75f, TextAnchor.MiddleRight);
        view.Stock = CreateFlexText(row.transform, string.Empty, 11, corTextoSecundario, 0.9f, TextAnchor.MiddleRight);
        return view;
    }

    private RouteRow CreateRouteRow(Transform parent)
    {
        GameObject row = CreatePanel("Rota", parent, 34f, corCard);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 4, 4);
        RouteRow view = new RouteRow { Root = row, Text = CreateLayoutText(row.transform, string.Empty, 11, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 26f) };
        view.Text.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return view;
    }

    private void RefreshResourceBar()
    {
        DadosPaisGoverno p = GetPlayerGov();
        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;

        if (p != null && gr != null)
        {
            p.saldo = gr.dinheiro;
            p.petroleo = gr.petroleo;
            p.aco = gr.aco;
            p.energia = gr.energia;
            p.comida = gr.comida;
            p.populacao = gr.populacaoAtual;
            p.populacaoMaxima = gr.populacaoMaxima;
        }

        float taxaComida = 0f;
        float taxaEnergia = 0f;
        DadosEconomiaPais playerEcon = PlayerEconomy();
        if (p != null && playerEcon != null)
        {
            float consumoCivil = p.populacaoCivil / 100f * 1f;
            float consumoMilitar = p.populacaoMilitarAtiva / 100f * 2f;
            taxaComida = playerEcon.comidaProduzida - (consumoCivil + consumoMilitar);
            taxaEnergia = playerEcon.energiaProduzida - playerEcon.energiaConsumida;
        }

        SetResource("DINHEIRO", p != null ? "$" + FormatNumber(p.saldo) : gr != null ? "$" + FormatNumber(gr.dinheiro) : "n/d", p != null ? SignedRate(p.rendaPorSegundo) : string.Empty, corVerde);
        SetResource("PETROLEO", p != null ? FormatNumber(p.petroleo) : gr != null ? FormatNumber(gr.petroleo) : "0", gr != null ? SignedRate(gr.petroleoPorSegundo) : string.Empty, gr == null || gr.petroleoPorSegundo >= 0f ? corVerde : corVermelho);
        SetResource("ACO", p != null ? FormatNumber(p.aco) : gr != null ? FormatNumber(gr.aco) : "0", gr != null ? SignedRate(gr.acoPorSegundo) : string.Empty, gr == null || gr.acoPorSegundo >= 0f ? corVerde : corVermelho);
        SetResource("ENERGIA", p != null ? FormatNumber(p.energia) : gr != null ? FormatNumber(gr.energia) : "0", p != null && playerEcon != null ? SignedRate(taxaEnergia) : string.Empty, p == null || taxaEnergia >= 0f ? corVerde : corVermelho);
        SetResource("COMIDA", p != null ? FormatNumber(p.comida) : gr != null ? FormatNumber(gr.comida) : "0", p != null && playerEcon != null ? SignedRate(taxaComida) : string.Empty, p == null || taxaComida >= 0f ? corVerde : corVermelho);
        SetResource("POP", p != null ? FormatNumber(p.populacao) + "/" + FormatNumber(p.populacaoMaxima) : gr != null ? gr.populacaoAtual + "/" + gr.populacaoMaxima : "0", string.Empty, corTextoSecundario);
        SetResource("ESTAB", p != null ? p.estabilidade.ToString("0") + "%" : "n/d", p != null ? "Infl. " + p.inflacao.ToString("0.0") + "%" : string.Empty, p != null ? StatusColor(p) : corTextoSecundario);
        SetResource("STATUS", p != null ? StatusGov(p).ToUpperInvariant() : "OK", string.Empty, p != null ? StatusColor(p) : corTextoSecundario);
    }

    private void RefreshFooter()
    {
        if (footerLeftText == null) return;
        int pendentes = Government() != null ? Government().ObterPropostasPendentesPara(paisJogadorId).Count() : 0;

        if (notificacoes.Count > 0)
        {
            NotificacaoGoverno n = notificacoes[0];
            footerLeftText.text = n.titulo + ": " + n.mensagem;
            footerLeftText.color = n.cor;
        }
        else
        {
            footerLeftText.text = pendentes > 0 ? "Diplomacia ativa. Ha " + pendentes + " pedido(s) aguardando decisao." : "Governo ativo. Nenhuma pendencia critica no momento.";
            footerLeftText.color = corTextoSecundario;
        }

        if (footerRightText != null)
            footerRightText.text = GetCategoryTitle(categoriaAtual) + " / " + GetSubTabs(categoriaAtual)[subAbaAtualIndex] + " | Pedidos: " + pendentes;
    }

    private void ExecuteBuy(string itemId)
    {
        SistemaMercadoGlobal mercado = Market();
        SistemaGovernoMundial gov = Government();
        DadosItemMercado item = mercado != null ? mercado.ObterItem(itemId) : null;
        DadosPaisGoverno partner = item != null ? ChooseMarketPartner(gov, item, false) : null;
        if (mercado == null || item == null || partner == null)
        {
            Notificar("Mercado", "Sem oferta disponivel.");
            return;
        }

        string msg;
        int quantity = item.CalcularQuantidadePadrao();
        if (mercado.Comprar(paisJogadorId, partner.teamId, item.id, quantity, out msg))
            Notificar("Compra", msg);
        else
            Notificar("Compra", msg);

        RefreshDynamicData(true);
    }

    private void BuyBestOffer()
    {
        DadosItemMercado item = Market()?.MelhorCompra() ?? Market()?.ItensOrdenados().FirstOrDefault();
        if (item == null)
        {
            Notificar("Mercado", "Sem oferta disponivel.");
            return;
        }
        ExecuteBuy(item.id);
    }

    private void SellRecommendedLot()
    {
        SistemaMercadoGlobal mercado = Market();
        DadosItemMercado item = mercado != null ? mercado.MelhorCompra() ?? mercado.ObterItem("petroleo") : null;
        if (item == null)
        {
            Notificar("Mercado", "Sem item para venda.");
            return;
        }

        string id = item.id == "energia" || item.id == "comida" || item.id == "aco" || item.id == "petroleo" ? item.id : "petroleo";
        SellMarketResource(id, 50);
    }

    private void SellRealResource(string itemId, int quantity)
    {
        SistemaMercadoGlobal mercado = Market();
        if (mercado == null)
        {
            Notificar("Venda", "Sistema de mercado indisponivel.");
            return;
        }

        string msg;
        int gain;
        if (mercado.VenderRecursoReal(itemId, quantity, out msg, out gain))
            Notificar("Venda", msg + " (+$" + FormatNumber(gain) + ")");
        else
            Notificar("Venda", msg);

        RefreshDynamicData(true);
    }

    private void SellMarketResource(string itemId, int quantity)
    {
        DadosItemMercado item = Market()?.ObterItem(itemId);
        if (item == null)
        {
            Notificar("Venda", "Item indisponivel.");
            return;
        }

        // Energia is the only resource allowed to bypass maritime delivery.
        if (item.recurso == RecursoMercado.Energia || item.id == "energia")
        {
            SellRealResource(itemId, quantity);
            return;
        }

        SistemaGovernoMundial gov = Government();
        SistemaMercadoGlobal mercado = Market();
        DadosPaisGoverno comprador = ChooseMarketPartner(gov, item, true);
        int estoque = StockForMarket(itemId);
        quantity = Mathf.Min(Mathf.Max(1, quantity), estoque);
        if (mercado == null || gov == null || comprador == null || quantity <= 0)
        {
            Notificar("Venda", comprador == null ? "Nenhum comprador com dinheiro disponivel." : "Sem estoque para vender.");
            return;
        }

        string msg;
        if (mercado.Vender(paisJogadorId, comprador.teamId, item.id, quantity, out msg))
            Notificar("Venda", msg + " | navio de carga em rota.");
        else
            Notificar("Venda", msg);

        RefreshDynamicData(true);
    }

    private void SellAllRealResource(string itemId)
    {
        int stock = RealStock(itemId);
        if (stock <= 0)
        {
            Notificar("Venda", "Sem estoque para vender.");
            return;
        }
        SellRealResource(itemId, stock);
    }

    private void SellAllMarketResource(string itemId)
    {
        int stock = StockForMarket(itemId);
        if (stock <= 0)
        {
            Notificar("Venda", "Sem estoque para vender.");
            return;
        }

        SellMarketResource(itemId, stock);
    }

    private void ToggleAutoSell(string itemId)
    {
        SistemaMercadoGlobal mercado = Market();
        if (mercado == null) return;

        if (itemId == "petroleo") mercado.autoVenderPetroleo = !mercado.autoVenderPetroleo;
        else if (itemId == "aco") mercado.autoVenderAco = !mercado.autoVenderAco;
        else if (itemId == "energia") mercado.autoVenderEnergia = !mercado.autoVenderEnergia;
        else if (itemId == "comida") mercado.autoVenderComida = !mercado.autoVenderComida;

        Notificar("Auto-venda", DisplayItemName(itemId) + " " + (AutoSellEnabled(itemId) ? "ativada." : "desativada."));
        RefreshDynamicData(true);
    }

    private void CreateNavButton(CategoriaGoverno categoria)
    {
        GameObject root = CreatePanel("Aba_" + categoria, sidebarRoot, 44f, corPainel);
        HorizontalLayoutGroup h = root.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(0, 10, 0, 0);
        h.spacing = 8;
        h.childControlHeight = true;
        h.childControlWidth = true;

        GameObject accent = CreateUIObject("Accent", root.transform);
        LayoutElement accentLe = accent.AddComponent<LayoutElement>();
        accentLe.preferredWidth = 4f;
        accentLe.minWidth = 4f;
        Image accentImage = accent.AddComponent<Image>();
        accentImage.raycastTarget = false;

        Text text = CreateLayoutText(root.transform, GetShortCategoryName(categoria).ToUpperInvariant(), 12, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 42f);
        text.GetComponent<LayoutElement>().flexibleWidth = 1f;

        Image bg = root.GetComponent<Image>();
        Button button = root.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() =>
        {
            if (categoriaAtual == categoria) return;
            SaveScrollPositions();
            categoriaAtual = categoria;
            subAbaAtualIndex = 0;
            RefreshStaticNavigation();
            ShowCurrentPage();
        });

        navButtons[categoria] = new NavButtonView { Root = root, Background = bg, Accent = accentImage, Text = text };
    }

    private void EnsureSubTabButtons(string[] labels)
    {
        while (subTabViews.Count < labels.Length)
        {
            int index = subTabViews.Count;
            GameObject root = CreatePanel("SubAba_" + index, subTabsRoot, 0f, corPainel2);
            LayoutElement le = root.GetComponent<LayoutElement>();
            le.preferredWidth = 132f;
            le.minWidth = 102f;
            le.flexibleWidth = 0f;
            HorizontalLayoutGroup h = root.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(0, 10, 0, 0);
            h.spacing = 6;
            h.childControlHeight = true;

            GameObject accent = CreateUIObject("Accent", root.transform);
            LayoutElement aLe = accent.AddComponent<LayoutElement>();
            aLe.preferredWidth = 3f;
            aLe.minWidth = 3f;
            Image accentImage = accent.AddComponent<Image>();
            accentImage.raycastTarget = false;

            Text text = CreateLayoutText(root.transform, "", 11, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Bold, 34f);
            text.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Image bg = root.GetComponent<Image>();
            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                if (subAbaAtualIndex == index) return;
                SaveScrollPositions();
                subAbaAtualIndex = index;
                RefreshStaticNavigation();
                ShowCurrentPage();
            });

            subTabViews.Add(new SubTabView { Root = root, Background = bg, Accent = accentImage, Label = text });
        }
    }

    private void CreateResourceView(string id, string label, Color accentColor)
    {
        GameObject box = CreatePanel("Recurso_" + id, resourceRoot, 0f, corPainel);
        box.GetComponent<LayoutElement>().flexibleWidth = 1f;
        HorizontalLayoutGroup h = box.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 4, 4);
        h.spacing = 6;
        h.childControlHeight = true;
        h.childControlWidth = true;

        Text tag = CreateLayoutText(box.transform, label, 10, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold, 42f);
        tag.GetComponent<LayoutElement>().preferredWidth = 40f;

        GameObject texts = CreateUIObject("Textos", box.transform);
        texts.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup v = texts.AddComponent<VerticalLayoutGroup>();
        v.spacing = -2;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        Text value = CreateLayoutText(texts.transform, "-", 12, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 20f);
        Text delta = CreateLayoutText(texts.transform, "", 8, corTextoApagado, TextAnchor.UpperLeft, FontStyle.Normal, 14f);
        resourceViews[id] = new ResourceTopView { Value = value, Delta = delta };
    }

    private ScrollRect CreateScrollPanel(Transform parent, string name, float preferredWidth, float minWidth, float flexibleWidth, out Transform content)
    {
        GameObject box = CreatePanel(name, parent, 0f, new Color(0.018f, 0.025f, 0.031f, 0.96f));
        LayoutElement le = box.GetComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.minWidth = minWidth;
        le.flexibleWidth = flexibleWidth;

        ScrollRect scroll = box.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 32f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateUIObject("Viewport", box.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 8, 8, 8, 8);
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<RectMask2D>();

        GameObject c = CreateUIObject("Content", viewport.transform);
        RectTransform cRt = c.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup v = c.AddComponent<VerticalLayoutGroup>();
        v.spacing = 7;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = c.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = cRt;
        content = c.transform;
        return scroll;
    }

    private GameObject CreatePageRoot(string name, Transform parent)
    {
        GameObject root = CreateUIObject(name, parent);
        root.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup v = root.AddComponent<VerticalLayoutGroup>();
        v.spacing = 7;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        root.SetActive(false);
        return root;
    }

    private GameObject CreatePanel(string name, Transform parent, float height, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        if (height > 0f)
        {
            le.preferredHeight = height;
            le.minHeight = Mathf.Min(height, 34f);
        }
        else
        {
            le.flexibleHeight = 1f;
        }

        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private GameObject CreateRow(Transform parent, string name, float height)
    {
        return CreatePanel(name, parent, height, corCard);
    }

    private HorizontalLayoutGroup SetupRow(GameObject row)
    {
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(9, 9, 4, 4);
        h.spacing = 7;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        return h;
    }

    private void CreateSectionTitle(Transform parent, string title)
    {
        Text t = CreateLayoutText(parent, title.ToUpperInvariant(), 16, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 28f);
        t.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private void CreateDescription(Transform parent, string text)
    {
        Text t = CreateLayoutText(parent, text, 11, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 34f);
        t.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private Text CreateInfoBlock(Transform parent, string text)
    {
        GameObject box = CreatePanel("Info", parent, 0f, corCard);
        LayoutElement layout = box.GetComponent<LayoutElement>();
        layout.minHeight = 96f;
        layout.preferredHeight = 96f;
        Text t = CreateFreeText(box.transform, text, 12, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Normal, 10, 8, 10, 8);
        t.verticalOverflow = VerticalWrapMode.Overflow;
        StartCoroutine(AjustarAlturaInfoBlockDepoisDoLayout(box, t));
        return t;
    }

    private IEnumerator AjustarAlturaInfoBlockDepoisDoLayout(GameObject box, Text text)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (box == null || text == null) yield break;

        RectTransform rect = box.GetComponent<RectTransform>();
        LayoutElement layout = box.GetComponent<LayoutElement>();
        if (rect == null || layout == null) yield break;

        float larguraTexto = Mathf.Max(220f, rect.rect.width - 20f);
        float caracteresPorLinha = Mathf.Max(24f, larguraTexto / Mathf.Max(6f, text.fontSize * 0.55f));
        string conteudo = text.text ?? string.Empty;
        int linhas = 0;
        string[] linhasTexto = conteudo.Split('\n');
        for (int i = 0; i < linhasTexto.Length; i++)
        {
            linhas += Mathf.Max(1, Mathf.CeilToInt(linhasTexto[i].Length / caracteresPorLinha));
        }

        float alturaEstimada = 16f + linhas * (text.fontSize + 4f);
        float alturaPreferida = Mathf.Max(text.preferredHeight + 16f, alturaEstimada);
        layout.minHeight = Mathf.Clamp(alturaPreferida, 96f, 320f);
        layout.preferredHeight = layout.minHeight;
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    private void CreateHeaderRow(Transform parent, string[] labels, float[] widths)
    {
        GameObject row = CreatePanel("HeaderRow", parent, 28f, new Color(0.026f, 0.034f, 0.042f, 0.98f));
        HorizontalLayoutGroup h = SetupRow(row);
        h.padding = new RectOffset(9, 9, 2, 2);
        for (int i = 0; i < labels.Length; i++)
        {
            float flex = i < widths.Length ? widths[i] : 1f;
            Text text = CreateFlexText(row.transform, labels[i], 9, corTextoApagado, flex, TextAnchor.MiddleLeft);
            text.fontStyle = FontStyle.Bold;
        }
    }

    private Button CreateActionButton(Transform parent, string label, Color color, Action onClick)
    {
        Button button = CreateButton(parent, label, color, onClick);
        LayoutElement le = button.GetComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 36f;
        le.flexibleWidth = 1f;
        Transform t = button.transform.Find("Label");
        if (t != null) t.GetComponent<Text>().fontSize = 12;
        return button;
    }

    private Button CreateMiniActionButton(Transform parent, string label, Color color, Action onClick)
    {
        Button button = CreateButton(parent, label, color, onClick);
        LayoutElement le = button.GetComponent<LayoutElement>();
        le.preferredHeight = 32f;
        le.minHeight = 30f;
        Transform t = button.transform.Find("Label");
        if (t != null) t.GetComponent<Text>().fontSize = 10;
        return button;
    }

    private Button CreateSmallButton(Transform parent, string label, Color color, Action onClick)
    {
        Button button = CreateButton(parent, label, color, onClick);
        LayoutElement le = button.GetComponent<LayoutElement>();
        le.preferredHeight = 34f;
        le.minHeight = 30f;
        le.flexibleWidth = 1f;
        Transform t = button.transform.Find("Label");
        if (t != null) t.GetComponent<Text>().fontSize = 11;
        return button;
    }

    private Button CreateButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject go = CreatePanel("Btn_" + label, parent, 38f, color);
        Image img = go.GetComponent<Image>();
        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.22f);
        cb.selectedColor = cb.highlightedColor;
        button.colors = cb;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        Text text = CreateFreeText(go.transform, label, 11, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        text.name = "Label";
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 9;
        text.resizeTextMaxSize = 12;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return button;
    }

    private InputField CreateCompactInput(Transform parent, string placeholderText)
    {
        GameObject go = CreatePanel("Input_" + placeholderText, parent, 30f, new Color(0.018f, 0.024f, 0.030f, 0.98f));
        Image img = go.GetComponent<Image>();
        img.raycastTarget = true;

        InputField input = go.AddComponent<InputField>();
        input.lineType = InputField.LineType.SingleLine;
        input.targetGraphic = img;

        GameObject textObj = CreateUIObject("Text", go.transform);
        Stretch(textObj.GetComponent<RectTransform>(), 8, 3, 8, 3);
        Text text = textObj.AddComponent<Text>();
        text.font = GetFont(placeholderText);
        text.fontSize = 11;
        text.color = corTextoPrimario;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = 12;
        text.raycastTarget = false;

        GameObject placeholderObj = CreateUIObject("Placeholder", go.transform);
        Stretch(placeholderObj.GetComponent<RectTransform>(), 8, 3, 8, 3);
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.text = placeholderText;
        placeholder.font = GetFont(placeholderText);
        placeholder.fontSize = 11;
        placeholder.color = corTextoApagado;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.resizeTextForBestFit = true;
        placeholder.resizeTextMinSize = 10;
        placeholder.resizeTextMaxSize = 12;
        placeholder.raycastTarget = false;

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private Text CreateFlexText(Transform parent, string text, int size, Color color, float flex, TextAnchor anchor)
    {
        Text t = CreateLayoutText(parent, text, size, color, anchor, FontStyle.Normal, 30f);
        t.GetComponent<LayoutElement>().flexibleWidth = flex;
        return t;
    }

    private Text CreateLayoutText(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style, float height)
    {
        GameObject go = CreateUIObject("Text", parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = Mathf.Min(height, 14f);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = GetFont(text);
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(8, size - 2);
        t.resizeTextMaxSize = size;
        t.raycastTarget = false;
        return t;
    }

    private Text CreateFreeText(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        return CreateFreeText(parent, text, size, color, anchor, style, 0, 0, 0, 0);
    }

    private Text CreateFreeText(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style, float left, float top, float right, float bottom)
    {
        GameObject go = CreateUIObject("TextFree", parent);
        Stretch(go.GetComponent<RectTransform>(), left, top, right, bottom);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = GetFont(text);
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(8, size - 2);
        t.resizeTextMaxSize = size;
        t.raycastTarget = false;
        return t;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = Vector3.zero;
        return go;
    }

    private void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void SaveScrollPositions()
    {
        string key = string.IsNullOrEmpty(activePageKey) ? CurrentPageKey() : activePageKey;
        if (centerScroll != null) centerScrollByPage[key] = centerScroll.verticalNormalizedPosition;
        if (rightScroll != null) rightScrollByPage[key] = rightScroll.verticalNormalizedPosition;
    }

    private void RestoreScrollPositions()
    {
        string key = CurrentPageKey();
        float value;
        Canvas.ForceUpdateCanvases();
        if (centerScroll != null && centerScrollByPage.TryGetValue(key, out value))
            centerScroll.verticalNormalizedPosition = value;
        if (rightScroll != null && rightScrollByPage.TryGetValue(key, out value))
            rightScroll.verticalNormalizedPosition = value;
    }

    private void GarantirCanvasEEventSystem()
    {
        if (canvasObj != null) return;

        GameObject existing = GameObject.Find("Canvas_MenuGoverno_RTS");
        if (existing != null)
        {
            canvasObj = existing;
            return;
        }

        canvasObj = new GameObject("Canvas_MenuGoverno_RTS");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9200;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        if (EventSystem.current == null && Achar<EventSystem>() == null)
        {
            GameObject eventObj = new GameObject("EventSystem_Auto_Governo");
            eventObj.AddComponent<EventSystem>();
            eventObj.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventObj);
        }
    }

    private void EsconderHUD(bool esconder)
    {
        CacheHUDComponents();

        if (cachedMiniMapa != null)
        {
            Transform canvasMM = cachedMiniMapa.transform.root.Find("Canvas_MiniMapa");
            if (canvasMM != null) canvasMM.gameObject.SetActive(!esconder);
            cachedMiniMapa.gameObject.SetActive(!esconder);
        }

        if (cachedMenuComportamento != null)
            SetHudComponentVisibility(cachedMenuComportamento, !esconder);

        if (cachedMenuConstrucao != null)
        {
            if (esconder && MenuConstrucao.EstaAberto) cachedMenuConstrucao.AlternarMenu(false);
            SetHudComponentVisibility(cachedMenuConstrucao, !esconder);
        }
    }

    private void CacheHUDComponents()
    {
        if (hudCached && cachedMiniMapa != null && cachedMenuComportamento != null && cachedMenuConstrucao != null) return;
        cachedMiniMapa = Achar<MiniMapa>();
        cachedMenuComportamento = AcharComponenteMesmoInativo<MenuComportamento>();
        cachedMenuConstrucao = AcharComponenteMesmoInativo<MenuConstrucao>();
        hudCached = true;
    }

    private void SetHudComponentVisibility(MonoBehaviour component, bool visible)
    {
        if (component == null) return;

        MenuComportamento comportamento = component as MenuComportamento;
        if (comportamento != null)
        {
            comportamento.DefinirVisibilidadeHud(visible);
            return;
        }

        MenuConstrucao construcao = component as MenuConstrucao;
        if (construcao != null)
        {
            construcao.DefinirVisibilidadeHud(visible);
            return;
        }

        if (component.gameObject == gameObject || component.GetComponent<MenuGoverno>() != null || component.GetComponent<GerenteDeJogo>() != null)
            component.enabled = visible;
        else
            component.gameObject.SetActive(visible);
    }

    private void Notificar(string title, string message)
    {
        if (string.IsNullOrEmpty(message)) message = "Acao executada.";
        notificacoes.Insert(0, new NotificacaoGoverno
        {
            icone = "!",
            titulo = title,
            mensagem = message,
            hora = "Agora",
            cor = title == "Venda" || title == "Compra" || title == "Mercado" ? corVerde : corDestaque
        });
        while (notificacoes.Count > 8) notificacoes.RemoveAt(notificacoes.Count - 1);
        RefreshFooter();
    }

    private string CurrentPageKey()
    {
        return categoriaAtual + ":" + subAbaAtualIndex;
    }

    private string[] GetSubTabs(CategoriaGoverno categoria)
    {
        switch (categoria)
        {
            case CategoriaGoverno.RelacoesExteriores: return SubRelacoes;
            case CategoriaGoverno.Aliancas: return SubAliancas;
            case CategoriaGoverno.Sancoes: return SubSancoes;
            case CategoriaGoverno.Economia: return SubEconomia;
            case CategoriaGoverno.MercadoGlobal: return SubMercado;
            case CategoriaGoverno.Interior: return SubInterior;
            case CategoriaGoverno.Defesa: return SubDefesa;
            case CategoriaGoverno.Ciencia: return SubCiencia;
            case CategoriaGoverno.Trabalho: return SubTrabalho;
            case CategoriaGoverno.DiversaoCultura: return SubDiversao;
            default: return new[] { "Geral" };
        }
    }

    private string GetShortCategoryName(CategoriaGoverno categoria)
    {
        switch (categoria)
        {
            case CategoriaGoverno.RelacoesExteriores: return "Relacoes";
            case CategoriaGoverno.MercadoGlobal: return "Mercado";
            case CategoriaGoverno.DiversaoCultura: return "Diversao";
            default: return categoria.ToString();
        }
    }

    private string GetCategoryTitle(CategoriaGoverno categoria)
    {
        switch (categoria)
        {
            case CategoriaGoverno.RelacoesExteriores: return "Relacoes Exteriores";
            case CategoriaGoverno.MercadoGlobal: return "Mercado Global";
            case CategoriaGoverno.DiversaoCultura: return "Diversao, Cultura e Turismo";
            default: return GetShortCategoryName(categoria);
        }
    }

    private SistemaGovernoMundial Government()
    {
        SistemaGovernoMundial.GarantirInstancia();
        return SistemaGovernoMundial.Instancia;
    }

    private SistemaMercadoGlobal Market()
    {
        SistemaGovernoMundial.GarantirInstancia();
        return SistemaMercadoGlobal.Instancia;
    }

    private void AplicarIdentidadeNacional()
    {
        SistemaGovernoMundial gov = Government();
        if (gov == null) return;

        gov.AtualizarIdentidadeNacional(
            paisJogadorId,
            campoNomePais != null ? campoNomePais.text : string.Empty,
            campoNomePresidente != null ? campoNomePresidente.text : string.Empty,
            campoNomeMoeda != null ? campoNomeMoeda.text : string.Empty);

        RefreshIdentityFields();
        RefreshStaticNavigation();
        RefreshDynamicData(true);
        Notificar("Governo", "Identidade nacional atualizada.");
    }

    private void RefreshIdentityFields()
    {
        DadosPaisGoverno jogador = GetPlayerGov();
        if (jogador == null) return;

        if (campoNomePais != null && !campoNomePais.isFocused)
            campoNomePais.text = jogador.nomePais ?? string.Empty;
        if (campoNomePresidente != null && !campoNomePresidente.isFocused)
            campoNomePresidente.text = jogador.nomePresidente ?? string.Empty;
        if (campoNomeMoeda != null && !campoNomeMoeda.isFocused)
            campoNomeMoeda.text = jogador.nomeMoeda ?? string.Empty;
    }

    private DadosPaisGoverno GetPlayerGov()
    {
        SistemaGovernoMundial gov = Government();
        return gov != null ? gov.ObterPais(paisJogadorId) : null;
    }

    private string CountryName(int teamId)
    {
        SistemaGovernoMundial gov = Government();
        return gov != null ? gov.NomePais(teamId) : "Pais " + teamId;
    }

    private DadosPaisGoverno ChooseMarketPartner(SistemaGovernoMundial gov, DadosItemMercado item, bool buyer)
    {
        if (gov == null || item == null) return null;
        IEnumerable<DadosPaisGoverno> candidates = gov.Paises.Where(p => p != null && p.teamId != paisJogadorId);
        if (buyer)
        {
            return candidates
                .Where(p => p.saldo >= item.precoAtual * item.CalcularQuantidadePadrao())
                .OrderByDescending(p => gov.ObterRelacao(paisJogadorId, p.teamId).valor)
                .FirstOrDefault();
        }

        return candidates
            .Where(p => item.equipamentoMilitar || item.municaoMilitar
                ? item.estoqueGlobal > 0
                : gov.ObterEstoque(p.teamId, item.RecursoIdEfetivo) >= item.CalcularQuantidadePadrao())
            .OrderByDescending(p => gov.ObterRelacao(paisJogadorId, p.teamId).valor)
            .FirstOrDefault();
    }

    private int RealStock(string itemId)
    {
        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;
        if (gr == null) return 0;
        if (itemId == "petroleo") return gr.petroleo;
        if (itemId == "aco") return gr.aco;
        if (itemId == "energia") return gr.energia;
        if (itemId == "comida") return gr.comida;
        return 0;
    }

    private int StockForMarket(string itemId)
    {
        DadosItemMercado item = Market()?.ObterItem(itemId);
        if (item == null) return RealStock(itemId);

        SistemaGovernoMundial gov = Government();
        if (gov == null) return RealStock(itemId);
        if (item.equipamentoMilitar || item.municaoMilitar)
            return Mathf.Max(0, item.estoqueGlobal);

        return Mathf.Max(0, Mathf.FloorToInt((float)gov.ObterEstoque(paisJogadorId, item.RecursoIdEfetivo)));
    }

    private bool AutoSellEnabled(string itemId)
    {
        SistemaMercadoGlobal m = Market();
        if (m == null) return false;
        if (itemId == "petroleo") return m.autoVenderPetroleo;
        if (itemId == "aco") return m.autoVenderAco;
        if (itemId == "energia") return m.autoVenderEnergia;
        if (itemId == "comida") return m.autoVenderComida;
        return false;
    }

    private int AutoSellAmount(string itemId)
    {
        SistemaMercadoGlobal m = Market();
        if (m == null) return 0;
        if (itemId == "petroleo") return m.autoVendaQuantidadePetroleo;
        if (itemId == "aco") return m.autoVendaQuantidadeAco;
        if (itemId == "energia") return m.autoVendaQuantidadeEnergia;
        if (itemId == "comida") return m.autoVendaQuantidadeComida;
        return 0;
    }

    private string DisplayItemName(string itemId)
    {
        if (itemId == "petroleo") return "PETROLEO";
        if (itemId == "aco") return "ACO";
        if (itemId == "energia") return "ENERGIA";
        if (itemId == "comida") return "COMIDA";
        DadosItemMercado item = Market()?.ObterItem(itemId);
        return item != null ? item.nome.ToUpperInvariant() : itemId.ToUpperInvariant();
    }

    private void SetResource(string id, string value, string delta, Color deltaColor)
    {
        ResourceTopView view;
        if (!resourceViews.TryGetValue(id, out view)) return;
        view.Value.text = value;
        view.Delta.text = delta;
        view.Delta.color = deltaColor;
        view.Delta.gameObject.SetActive(!string.IsNullOrEmpty(delta));
    }

    private string StatusGov(DadosPaisGoverno p)
    {
        if (p == null) return "Desconhecido";
        if (p.emGuerra) return "Guerra";
        if (p.sancionado) return "Sancoes";
        if (p.estabilidade < 35f) return "Crise";
        if (p.estabilidade < 55f) return "Tensao";
        return "Paz";
    }

    private Color StatusColor(DadosPaisGoverno p)
    {
        if (p == null) return corTextoSecundario;
        if (p.emGuerra || p.sancionado || p.estabilidade < 35f) return corVermelho;
        if (p.estabilidade < 55f) return corAmarelo;
        return corVerde;
    }

    private Color RelationColor(int value)
    {
        if (value >= 55) return corVerde;
        if (value >= 10) return corDestaque;
        if (value > -25) return corAmarelo;
        if (value > -60) return corLaranja;
        return corVermelho;
    }

    private string MainDeficit(DadosPaisGoverno p)
    {
        if (p == null) return "Nenhum";
        if (p.deficitEnergia > 0.5f) return "Energia";
        if (p.deficitComida > 0.5f) return "Comida";
        if (p.deficitPetroleo > 0.5f) return "Petroleo";
        return "Nenhum";
    }

    private void CreateScienceSummaryCard(Transform parent, DadosPaisGoverno pais)
    {
        Text box = CreateInfoBlock(parent, string.Empty);
        box.text = "Tesouro cientifico: $" + FormatNumber(pais.saldo)
            + "\nLinhas industriais: " + ObterResumoLinhasIndustriais(pais.teamId)
            + "\nMortos acumulados: " + FormatNumber(pais.mortosAcumulados)
            + "\nFoco atual: " + pais.planoEstrategico
            + "\nPrograma orbital: " + (pais.sateliteDefesa != null && pais.sateliteDefesa.desbloqueado ? "ativo" : "bloqueado");
    }

    private void CreateScienceResearchCard(Transform parent, PesquisaNacionalEstado pesquisa)
    {
        if (pesquisa == null) return;
        GameObject card = CreatePanel("Pesquisa_" + pesquisa.id, parent, 0f, corCardClara);
        card.GetComponent<LayoutElement>().minHeight = 150f;
        VerticalLayoutGroup v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 6;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        string estado = pesquisa.concluida ? "CONCLUIDA"
            : pesquisa.emAndamento ? "EM ANDAMENTO"
            : DependenciasAtendidasUI(pesquisa.dependencias) ? "PRONTA"
            : "BLOQUEADA";
        Text body = CreateLayoutText(card.transform,
            pesquisa.nome.ToUpperInvariant()
            + "\nCategoria: " + pesquisa.categoria
            + "\n" + pesquisa.descricao
            + "\nRequisitos: " + pesquisa.requisitosVisuais
            + "\nDesbloqueia: " + pesquisa.desbloqueia
            + "\nCusto: $" + FormatNumber(pesquisa.custoSaldo) + " | Energia: " + FormatNumber(pesquisa.custoEnergia)
            + "\nTempo: " + pesquisa.duracaoDias + " dias"
            + "\nEstado: " + estado + PesquisaTempoRestanteTexto(pesquisa),
            11, corTextoPrimario, TextAnchor.UpperLeft, FontStyle.Normal, 108f);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        if (!pesquisa.concluida && !pesquisa.emAndamento)
        {
            CreateSmallButton(card.transform, "INICIAR PESQUISA", corAzulBotao, () =>
            {
                string mensagem = "Sistema de pesquisa indisponivel.";
                bool ok = Government() != null && Government().IniciarPesquisaNacional(paisJogadorId, pesquisa.id, out mensagem);
                Notificar("Ciencia", mensagem);
                RefreshDynamicData(true);
            });
        }
        else if (pesquisa.concluida)
        {
            CreateSmallButton(card.transform, "DESBLOQUEADA", corVerde, null);
        }
    }

    private void CreateScienceTechnologyCard(Transform parent, TecnologiaNacionalEstado tecnologia)
    {
        if (tecnologia == null) return;
        GameObject card = CreatePanel("Tecnologia_" + tecnologia.id, parent, 0f, corCardClara);
        card.GetComponent<LayoutElement>().minHeight = 146f;
        VerticalLayoutGroup v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 6;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        int proximoNivel = Mathf.Min(tecnologia.nivelMaximo, tecnologia.nivelAtual + 1);

        Text body = CreateLayoutText(card.transform,
            tecnologia.nome.ToUpperInvariant()
            + "\nCategoria: " + tecnologia.categoria
            + "\n" + tecnologia.descricao
            + "\nEfeito: " + tecnologia.efeito
            + "\nNivel: " + tecnologia.nivelAtual + "/" + tecnologia.nivelMaximo
            + "\nInvestimento: $" + FormatNumber(tecnologia.custoSaldo * Mathf.Max(1, proximoNivel))
            + " | Energia: " + FormatNumber(tecnologia.custoEnergia * Mathf.Max(1, proximoNivel))
            + "\nTempo: " + tecnologia.duracaoDias + " dias"
            + "\nEstado: " + (tecnologia.emAndamento ? "PESQUISANDO" : (tecnologia.nivelAtual >= tecnologia.nivelMaximo ? "MAXIMO" : "DISPONIVEL"))
            + TecnologiaTempoRestanteTexto(tecnologia),
            11, corTextoPrimario, TextAnchor.UpperLeft, FontStyle.Normal, 104f);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        if (tecnologia.nivelAtual < tecnologia.nivelMaximo && !tecnologia.emAndamento)
        {
            CreateSmallButton(card.transform, "INVESTIR NIVEL " + proximoNivel, corPainel2, () =>
            {
                string mensagem = "Sistema de tecnologia indisponivel.";
                bool ok = Government() != null && Government().IniciarTecnologiaNacional(paisJogadorId, tecnologia.id, out mensagem);
                Notificar("Tecnologia", mensagem);
                RefreshDynamicData(true);
            });
        }
        else if (tecnologia.nivelAtual >= tecnologia.nivelMaximo)
        {
            CreateSmallButton(card.transform, "EFEITO APLICADO", corVerde, null);
        }
    }

    private void BuildScienceProjectsCards(Transform parent, DadosPaisGoverno pais)
    {
        CreateDescription(parent, "Projetos industriais conectados as linhas reais do pais e aos custos do cofre.");

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            CreateInfoBlock(parent, "Sistema industrial nacional indisponivel.");
            return;
        }

        EstadoIndustrialPais estado = industrial.ObterEstadoPais(pais.teamId);
        IReadOnlyList<LinhaIndustrial> linhas = industrial.ObterLinhasPais(pais.teamId);
        Text overview = CreateInfoBlock(parent, string.Empty);
        overview.text = "Linhas atuais: " + (linhas != null ? linhas.Count.ToString() : "0")
            + "\nOcupadas: " + (estado != null ? estado.linhasOcupadas.ToString() : "0")
            + "\nDisponiveis: " + (estado != null ? estado.linhasDisponiveis.ToString() : "0")
            + "\nOrdens ativas: " + (estado != null ? estado.ordensAtivas.ToString() : "0")
            + "\nProducao diaria: " + (estado != null ? estado.producaoDiariaTotal.ToString("N0") : "0")
            + "\nProjetos catalogados: " + industrial.ReceitasCatalogo.Count;

        if (linhas != null && linhas.Count > 0)
        {
            CreateDescription(parent, "Linhas industriais atuais: linha 1, linha 2 e demais filas com status, progresso e receita em execucao.");
            foreach (LinhaIndustrial linha in linhas)
            {
                if (linha == null) continue;
                CreateScienceLineCard(parent, linha);
            }
        }

        CreateDescription(parent, "Lotes disponiveis para investimento industrial. Variantes pesadas e militares custam mais e drenam mais energia do cofre.");
        foreach (ReceitaIndustrialSO receita in industrial.ReceitasCatalogo)
        {
            if (receita == null) continue;
            CreateScienceProjectCard(parent, receita);
        }
    }

    private void CreateScienceLineCard(Transform parent, LinhaIndustrial linha)
    {
        GameObject card = CreatePanel("LinhaIndustrial_" + linha.indice, parent, 0f, corCardClara);
        card.GetComponent<LayoutElement>().minHeight = 114f;
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        bool ocupada = linha.EstaOcupada;
        string receita = string.IsNullOrWhiteSpace(linha.receitaId) ? "Livre" : linha.receitaId;
        string texto = "LINHA " + (linha.indice + 1)
            + "\nEstado: " + linha.estado
            + "\nReceita atual: " + receita
            + "\nDias restantes: " + linha.diasRestantes
            + "\nProgresso: " + (linha.progresso * 100f).ToString("0") + "%";
        Text body = CreateLayoutText(card.transform, texto, 11, corTextoPrimario, TextAnchor.UpperLeft, FontStyle.Normal, 74f);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        CreateSmallButton(card.transform, ocupada ? "EM EXECUCAO" : "PRONTA PARA LOTE", ocupada ? corAzulBotao : corVerde, null);
    }

    private void CreateScienceProjectCard(Transform parent, ReceitaIndustrialSO receita)
    {
        GameObject card = CreatePanel("Projeto_" + receita.id, parent, 0f, corCardClara);
        card.GetComponent<LayoutElement>().minHeight = 156f;
        VerticalLayoutGroup v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 6;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        string materiais = receita.materiaisNecessarios != null && receita.materiaisNecessarios.Count > 0
            ? string.Join(" | ", receita.materiaisNecessarios.Select(m => m.recursoId + ": " + m.quantidade.ToString("N0")))
            : "Sem entrada";
        DadosPaisGoverno pais = GetPlayerGov();
        bool pesquisaOk = string.IsNullOrEmpty(receita.pesquisaExigida) || DependenciasAtendidasUI(receita.pesquisaExigida);
        bool nivelOk = pais != null && pais.nivelIndustrial >= receita.nivelIndustrialExigido;
        string statusProjeto = pesquisaOk && nivelOk ? "DISPONIVEL" : "BLOQUEADO";

        Text body = CreateLayoutText(card.transform,
            receita.nome.ToUpperInvariant()
            + "\nEntradas: " + materiais
            + "\nSaida: " + receita.quantidadeProduzida.ToString("N0") + " " + receita.produtoFinalId
            + "\nCusto: $" + FormatNumber(receita.dinheiroNecessario) + " | Energia: " + FormatNumber(receita.energiaNecessaria)
            + "\nDuracao: " + receita.diasNecessarios + " dias"
            + "\nPesquisa exigida: " + (string.IsNullOrEmpty(receita.pesquisaExigida) ? "Nenhuma" : receita.pesquisaExigida)
            + "\nNivel industrial: " + receita.nivelIndustrialExigido
            + "\nStatus: " + statusProjeto,
            11, corTextoPrimario, TextAnchor.UpperLeft, FontStyle.Normal, 112f);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        CreateSmallButton(card.transform, pesquisaOk && nivelOk ? "INICIAR LOTE" : "VER REQUISITOS", pesquisaOk && nivelOk ? corVerde : corPainel2, () =>
        {
            SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
            OrdemRefinoIndustrial ordem = industrial != null ? industrial.CriarOrdemRefino(paisJogadorId, receita.id) : null;
            bool ok = ordem != null && ordem.estado != EstadoOrdemRefinoIndustrial.PausadaSemVerba;
            Notificar("Projetos", ok ? "Lote industrial iniciado para " + receita.nome + "." : "Nao foi possivel iniciar " + receita.nome + ".");
            RefreshDynamicData(true);
        });
    }

    private void CreateScienceLabCard(Transform parent, LaboratorioNacionalEstado laboratorio)
    {
        if (laboratorio == null) return;
        GameObject card = CreatePanel("Lab_" + laboratorio.id, parent, 0f, corCardClara);
        card.GetComponent<LayoutElement>().minHeight = 146f;
        VerticalLayoutGroup v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 10, 10);
        v.spacing = 6;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        Text body = CreateLayoutText(card.transform,
            laboratorio.nome.ToUpperInvariant()
            + "\nEspecializacao: " + laboratorio.especializacao
            + "\n" + laboratorio.descricao
            + "\nNivel: " + laboratorio.nivelAtual + "/" + laboratorio.nivelMaximo
            + "\nCusto: $" + FormatNumber(laboratorio.custoSaldo * Mathf.Max(1, laboratorio.nivelAtual + 1))
            + " | Energia: " + FormatNumber(laboratorio.custoEnergia * Mathf.Max(1, laboratorio.nivelAtual + 1))
            + "\nTempo: " + laboratorio.duracaoDias + " dias"
            + "\nEstado: " + (laboratorio.emExpansao ? "EM EXPANSAO" : (laboratorio.nivelAtual >= laboratorio.nivelMaximo ? "MAXIMO" : "DISPONIVEL"))
            + LaboratorioTempoRestanteTexto(laboratorio),
            11, corTextoPrimario, TextAnchor.UpperLeft, FontStyle.Normal, 104f);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        if (laboratorio.nivelAtual < laboratorio.nivelMaximo && !laboratorio.emExpansao)
        {
            CreateSmallButton(card.transform, "EXPANDIR", corAzulBotao, () =>
            {
                string mensagem = "Sistema de laboratorio indisponivel.";
                bool ok = Government() != null && Government().ExpandirLaboratorio(paisJogadorId, laboratorio.id, out mensagem);
                Notificar("Laboratorio", mensagem);
                RefreshDynamicData(true);
            });
        }
        else if (laboratorio.nivelAtual >= laboratorio.nivelMaximo)
        {
            CreateSmallButton(card.transform, "OPERANDO", corVerde, null);
        }
    }

    private void CreateDefenseSatelliteCard(Transform parent, DadosPaisGoverno pais)
    {
        if (pais == null)
        {
            return;
        }

        // Saves/estados antigos podem desserializar o campo como nulo. O
        // painel continua apresentando o programa orbital e o mesmo estado
        // fica disponivel para os botoes de manutencao e aporte.
        if (pais.sateliteDefesa == null)
        {
            pais.sateliteDefesa = new SateliteDefesaEstado();
        }

        SateliteDefesaEstado satelite = pais.sateliteDefesa;

        string prontidao = satelite.integridade >= 75f && satelite.desempenho >= 70f
            ? "OPERACIONAL"
            : satelite.integridade >= 45f && satelite.desempenho >= 45f
                ? "ATENCAO"
                : "CRITICO";
        string textoSatelite = "SATELITE NACIONAL"
            + "\nStatus: " + (satelite.desbloqueado ? "ATIVO" : "BLOQUEADO")
            + "\nProntidao: " + prontidao
            + "\nDesempenho: " + satelite.desempenho.ToString("0") + "%"
            + "\nIntegridade: " + satelite.integridade.ToString("0") + "%"
            + "\nCusto operacao: $" + FormatNumber(satelite.custoOperacionalDiario) + "/dia"
            + "\nCusto manutencao: $" + FormatNumber(satelite.custoManutencaoDiaria) + "/dia"
            + "\nManutencao automatica: " + (satelite.manutencaoAutomatica ? "SIM" : "NAO");
        CreateInfoBlock(parent, textoSatelite);

        CreateActionButton(parent, satelite.manutencaoAutomatica ? "DESLIGAR MANUTENCAO AUTO" : "LIGAR MANUTENCAO AUTO",
            corPainel2, () =>
            {
                Government()?.ConfigurarSatelite(paisJogadorId, !satelite.manutencaoAutomatica);
                Notificar("Defesa", "Manutencao automatica do satelite atualizada.");
                RefreshDynamicData(true);
            });

        CreateActionButton(parent, "APORTAR $1.200 NO SATELITE", corAzulBotao, () =>
        {
            string mensagem = "Sistema de satelite indisponivel.";
            bool ok = Government() != null && Government().InvestirNoSatelite(paisJogadorId, 1200, out mensagem);
            Notificar("Defesa", mensagem);
            RefreshDynamicData(true);
        });
    }

    private bool DependenciasAtendidasUI(string dependencias)
    {
        DadosPaisGoverno pais = GetPlayerGov();
        if (pais == null || string.IsNullOrWhiteSpace(dependencias))
        {
            return true;
        }

        string[] partes = dependencias.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < partes.Length; i++)
        {
            string dependencia = partes[i].Trim();
            if (dependencia.StartsWith("lab_", StringComparison.OrdinalIgnoreCase))
            {
                LaboratorioNacionalEstado laboratorio = pais.laboratorios.FirstOrDefault(l => l != null && l.id == dependencia);
                if (laboratorio == null || laboratorio.nivelAtual <= 0)
                {
                    return false;
                }
            }
            else
            {
                PesquisaNacionalEstado pesquisa = pais.pesquisas.FirstOrDefault(p => p != null && p.id == dependencia);
                if (pesquisa == null || !pesquisa.concluida)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private string PesquisaTempoRestanteTexto(PesquisaNacionalEstado pesquisa)
    {
        if (pesquisa == null || !pesquisa.emAndamento)
        {
            return string.Empty;
        }

        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        int restante = Mathf.Max(0, (pesquisa.diaInicio + pesquisa.duracaoDias) - diaAtual);
        return " | Restam " + restante + " dias";
    }

    private string TecnologiaTempoRestanteTexto(TecnologiaNacionalEstado tecnologia)
    {
        if (tecnologia == null || !tecnologia.emAndamento)
        {
            return string.Empty;
        }

        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        int restante = Mathf.Max(0, (tecnologia.diaInicio + tecnologia.duracaoDias) - diaAtual);
        return " | Restam " + restante + " dias";
    }

    private string LaboratorioTempoRestanteTexto(LaboratorioNacionalEstado laboratorio)
    {
        if (laboratorio == null || !laboratorio.emExpansao)
        {
            return string.Empty;
        }

        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        int restante = Mathf.Max(0, (laboratorio.diaInicio + laboratorio.duracaoDias) - diaAtual);
        return " | Restam " + restante + " dias";
    }

    private string ObterResumoLinhasIndustriais(int teamId)
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return "0/0";
        }

        EstadoIndustrialPais estado = industrial.ObterEstadoPais(teamId);
        if (estado == null)
        {
            return "0/0";
        }

        return estado.linhasOcupadas + "/" + (estado.linhasOcupadas + estado.linhasDisponiveis);
    }

    private string FormatNumber(int number)
    {
        return ValoresDefinitivosHegemonia.FormatarDinheiro(number).TrimStart('$');
    }

    private string FormatNumber(long number)
    {
        return ValoresDefinitivosHegemonia.FormatarDinheiro(number).TrimStart('$');
    }

    private string FormatNumber(float number)
    {
        return FormatNumber((long)Math.Round(number, MidpointRounding.AwayFromZero));
    }

    private string SignedRate(float value)
    {
        return (value >= 0f ? "+" : "") + Mathf.RoundToInt(value) + "/s";
    }

    private string SignedPercent(float value)
    {
        return (value >= 0f ? "+" : "") + value.ToString("0.0") + "%";
    }

    private Font GetFont(string text)
    {
        if (usarFonteEmoji && fonteEmoji != null && ContainsNonAscii(text)) return fonteEmoji;
        if (fonteTexto != null) return fonteTexto;
        if (fontePadraoCache == null)
        {
            fontePadraoCache = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fontePadraoCache == null) fontePadraoCache = Font.CreateDynamicFontFromOSFont("Arial", 14);
        }
        return fontePadraoCache;
    }

    private bool ContainsNonAscii(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] > 255) return true;
        }
        return false;
    }

    private static T Achar<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<T>();
#else
        return FindObjectOfType<T>();
#endif
    }

    private static T AcharComponenteMesmoInativo<T>() where T : MonoBehaviour
    {
#if UNITY_2023_1_OR_NEWER
        T ativo = FindFirstObjectByType<T>();
        if (ativo != null) return ativo;
        T[] encontrados = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return encontrados.FirstOrDefault();
#else
        return FindObjectOfType<T>(true);
#endif
    }

    private class PageView
    {
        public string Key;
        public GameObject Root;
        public Action Refresh;
    }

    private class NavButtonView
    {
        public GameObject Root;
        public Image Background;
        public Image Accent;
        public Text Text;
    }

    private class SubTabView
    {
        public GameObject Root;
        public Image Background;
        public Image Accent;
        public Text Label;
    }

    private class ResourceTopView
    {
        public Text Value;
        public Text Delta;
    }

    private class MarketBuyRow
    {
        public MenuGoverno Menu;
        public GameObject Root;
        public string ItemId;
        public Text Name;
        public Text Stock;
        public Text Price;
        public Text Partner;
        public Button Action;

        public void Refresh(DadosItemMercado item)
        {
            SistemaGovernoMundial gov = Menu.Government();
            DadosPaisGoverno partner = Menu.ChooseMarketPartner(gov, item, false);
            ItemId = item.id;
            Name.text = item.nome.ToUpperInvariant();
            Stock.text = Menu.FormatNumber(item.estoqueGlobal);
            Price.text = ValoresDefinitivosHegemonia.FormatarDinheiro(item.precoAtual);
            Price.color = item.variacaoPercentual >= 0f ? Menu.corVerde : Menu.corVermelho;
            Partner.text = partner != null ? partner.nomePais : "sem oferta";
            Action.interactable = partner != null && item.estoqueGlobal > 0;
        }
    }

    private class MarketSellRow
    {
        public MenuGoverno Menu;
        public GameObject Root;
        public string ItemId;
        public Text Name;
        public Text Stock;
        public Text Price;
        public Button Auto;
        public Text AutoText;
        public Button Sell50;
        public Button Sell200;
        public Button SellAll;

        public void Refresh()
        {
            SistemaMercadoGlobal market = Menu.Market();
            DadosItemMercado item = market != null ? market.ObterItem(ItemId) : null;
            int stock = Menu.StockForMarket(ItemId);
            bool auto = Menu.AutoSellEnabled(ItemId);
            int autoAmount = Menu.AutoSellAmount(ItemId);
            bool vendaDireta = ItemId == "energia";

            Name.text = Menu.DisplayItemName(ItemId);
            Stock.text = Menu.FormatNumber(stock);
            Price.text = item != null ? "$" + Menu.FormatNumber(item.precoAtual) : "$0";
            AutoText.text = vendaDireta ? (auto ? "Auto " + autoAmount : "Auto off") : "Navio";
            Auto.GetComponent<Image>().color = auto ? new Color(0.070f, 0.290f, 0.130f, 1f) : Menu.corPainel2;
            Auto.interactable = vendaDireta;
            Sell50.interactable = stock >= 50;
            Sell200.interactable = stock >= 200;
            SellAll.interactable = stock > 0;
        }
    }

    private class MarketPriceRow
    {
        public GameObject Root;
        public Text Name;
        public Text Price;
        public Text Var;
        public Text Offer;
        public Text Demand;
        public Text Stock;

        public void Refresh(DadosItemMercado item)
        {
            Name.text = item.nome.ToUpperInvariant();
            Price.text = ValoresDefinitivosHegemonia.FormatarDinheiro(item.precoAtual);
            Var.text = (item.variacaoPercentual >= 0f ? "+" : "") + item.variacaoPercentual.ToString("0.0") + "%";
            Var.color = item.variacaoPercentual >= 0f ? new Color(0.220f, 0.790f, 0.390f, 1f) : new Color(0.900f, 0.180f, 0.140f, 1f);
            Offer.text = item.oferta.ToString("0");
            Demand.text = item.demanda.ToString("0");
            Stock.text = item.estoqueGlobal.ToString("N0").Replace(",", ".");
        }
    }

    private class RouteRow
    {
        public GameObject Root;
        public Text Text;
    }
}
