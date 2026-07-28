using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Menu Comando Tático — controlador principal do UI Toolkit.
/// Abre/fecha com a tecla 1. Enquanto aberto, bloqueia Z/X/V/B/N/M
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
    private bool menuAberto;
    public bool MenuAberto => menuAberto;

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
    private bool modoMovimentoMapaAtivo = false;
    private bool modoLancamentoMissilMapaAtivo = false;

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
    private readonly List<ControleUnidade> cacheControlesPersistencia = new List<ControleUnidade>(256);
    private readonly List<IdentidadeIA> cacheIdentidadesIA = new List<IdentidadeIA>(64);
    private readonly HashSet<int> unidadesSelecionadasIds = new HashSet<int>();
    private float proximoRefreshCachesEntidades;
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
        if (controle == null && prepararSeMovel
            && (identidade.tipoUnidade != TipoUnidade.Estrutura
                || identidade.GetComponent<SiloLancadorEstrategico>() != null))
        {
            controle = identidade.gameObject.AddComponent<ControleUnidade>();
        }

        return controle;
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
    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        RegistroEntidadesJogo.EntidadesAlteradas += MarcarCachesEntidadesSujo;

        uiDoc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        root = uiDoc.rootVisualElement;
        menuAberto = false;

        AtualizarLimitesMapa();

        // Oculta o menu na inicialização
        root.style.display = DisplayStyle.None;

        BindUI();
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
        if (root == null || uiDoc == null)
        {
            return;
        }

        // Se o Menu do Governo estiver aberto, ignora atalhos de teclado
        // A tecla 1 Ã© tratada antes do bloqueio do Governo para permitir
        // recuperar o menu quando o estado estÃ¡tico ficou desatualizado.

        // Tecla 1 — toggle
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            if (menuAberto) FecharMenu();
            else AbrirMenu();
            return;
        }

        if (menuAberto && Input.GetKeyDown(KeyCode.Escape))
        {
            FecharMenu();
            return;
        }

        if (MenuGoverno.EstaAberto) return;

        if (!menuAberto) return;

        // Atalho: tecla V alterna câmera do drone
        if (unidadeSelecionadaMenu != null && unidadeSelecionadaMenu.GetComponent<KamikazeDrone>() != null)
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                AlternarModoCameraDrone();
            }
        }

        // Atalho: tecla A seleciona todas as unidades aliadas no mapa
        if (Input.GetKeyDown(KeyCode.A))
        {
            SelecionarTodasUnidadesAliadas();
        }

        // A tecla I executa o mesmo comando do botao ESTADO (ALTERNAR).
        if (Input.GetKeyDown(KeyCode.I))
        {
            ExecutarOrdem("ESTADO_ALTERNAR");
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
        RegistroEntidadesJogo.EntidadesAlteradas -= MarcarCachesEntidadesSujo;
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
        if (menuAberto) return;
        menuAberto = true;

        root.style.display = DisplayStyle.Flex;

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
            if (cu != null && !unidadesSelecionadasMenu.Contains(cu))
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
            if (cu != null && !unidadesSelecionadasMenu.Contains(cu))
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
        modoMovimentoMapaAtivo = false;
        modoLancamentoMissilMapaAtivo = false;

        root.style.display = DisplayStyle.None;

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

        // Botões de ordem
        var btnAtivo = root.Q<Button>("btn-ativo");
        if (btnAtivo != null) VincularBotaoOrdem(btnAtivo, "ATIVO");

        var btnPassivo = root.Q<Button>("btn-passivo");
        if (btnPassivo != null) VincularBotaoOrdem(btnPassivo, "PASSIVO");

        var btnEstado = root.Q<Button>("btn-estado");
        if (btnEstado != null) VincularBotaoOrdem(btnEstado, "ESTADO_ALTERNAR");

        var btnPatrulhar = root.Q<Button>("btn-patrulhar");
        if (btnPatrulhar != null) VincularBotaoOrdem(btnPatrulhar, "PATRULHAR");

        var btnSeguir = root.Q<Button>("btn-seguir");
        if (btnSeguir != null) VincularBotaoOrdem(btnSeguir, "SEGUIR");

        var btnAtacar = root.Q<Button>("btn-atacar");
        if (btnAtacar != null) VincularBotaoOrdem(btnAtacar, "ATACAR");

        var btnVoltarBase = root.Q<Button>("btn-voltar-base");
        if (btnVoltarBase != null) VincularBotaoOrdem(btnVoltarBase, "VOLTAR_BASE");

        var btnTrocaCamera = root.Q<Button>("btn-troca-camera");
        if (btnTrocaCamera != null) VincularBotaoOrdem(btnTrocaCamera, "TROCAR_CAMERA");

        var btnMover = root.Q<Button>("btn-mover-mapa");
        if (btnMover != null) VincularBotaoOrdem(btnMover, "MOVER_MAPA");

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

            int instId = id.gameObject.GetInstanceID();
            bool amigo   = EhUnidadeDoJogador(id);
            bool inimigo = id.teamID > 0 && !amigo;
            if (!amigo && !inimigo) continue;
            mapaVivos.Add(instId);

            Vector3 pos3D = id.transform.position;

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
            }
            if (item.Label != null)
            {
                item.Label.EnableInClassList("selecionado", estasel);
                item.Label.EnableInClassList("foco", estaEmFoco);
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

        var container = new VisualElement();
        container.AddToClassList("mapa-unidade");
        container.name = $"mapa-unit-{id.gameObject.GetInstanceID()}";
        // Unidades aliadas antigas podem chegar sem o adaptador de ordens.
        // Prepara somente as unidades do jogador para que o card/marcador
        // continue selecionável; nunca adiciona controle aos inimigos.
        ControleUnidade controleTatico = ObterControleTatico(id, amigo);
        if (controleTatico != null)
        {
            container.AddToClassList("controlavel");
            container.tooltip = "Unidade controlavel pelo jogador — clique para assumir o controle";
        }

        // Label com nome
        string nomeMapa = ObterNomeExibicao(id.gameObject);
        var label = new Label(nomeMapa.Length > 28 ? nomeMapa.Substring(0, 28) + "..." : nomeMapa);
        label.name = "mapa-label";
        label.AddToClassList("mapa-label");
        label.AddToClassList(classFacao);

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
                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (desenhadorOrdens != null && (desenhadorOrdens.modoPatrulhaAtivo || desenhadorOrdens.modoSeguirAtivo || desenhadorOrdens.modoAtaqueAtivo))
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

    private string ObterEmojiUnidade(IdentidadeUnidade id)
    {
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
            if (EhUnidadeDoJogador(id))
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
            if (EhUnidadeDoJogador(id))
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
        var lmCaca = cu.GetComponent<LancadorMisselCaca>();
        var lmNaval = cu.GetComponent<LancadorNaval>();
        var lmSolo = cu.GetComponent<LancadorMisseis>();
        
        if (lmCaca != null)
            textoArmas = $"MSL: {lmCaca.municaoAtual}/{lmCaca.municaoMaxima}";
        else if (lmNaval != null)
            textoArmas = $"MSL: {lmNaval.municaoTotal}/{lmNaval.municaoMaxima} | TORP: {lmNaval.torpedosTotal}/{lmNaval.torpedosMaximos}";
        else if (lmSolo != null)
            textoArmas = $"MSL: {lmSolo.municaoAtual}/{lmSolo.municaoMaxima}";
            
        AtualizarTextoMisseisEmVoo(textoArmas);

        // Status
        bool passivo;
        string descStatus;
        if (cu.TryObterEstadoCombate(out passivo, out descStatus))
            SetText(statStatus, passivo ? "PASSIVO" : "ATIVO");
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

    private void AlternarModoCameraDrone()
    {
        if (CameraUnidadeHUD.Instancia == null) return;
        
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

        switch (ordem)
        {
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

            case "MOVER_MAPA":
                modoMovimentoMapaAtivo = true;
                if (desenhadorOrdens != null) desenhadorOrdens.CancelarModo();
                FecharPainelSeguimento();
                SetText(ordemFeedback, $"MOVER ATIVO: clique esquerdo ou direito no mapa para escolher o destino.");
                AdicionarLog("OPS", $"{snapshot.Count} unidades: aguardando ponto de movimento no mapa", "normal");
                break;

            case "LANCAR_MISSIL":
                modoLancamentoMissilMapaAtivo = true;
                modoMovimentoMapaAtivo = false;
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

            case "PATRULHAR":
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
                    }
                }
                SetText(ordemFeedback, $"✔ [{retornando} UDS] → RETORNANDO À BASE");
                AdicionarLog("OPS", $"{retornando} aeronaves ordenadas a retornar à base", "normal");
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

        if (modoMovimentoMapaAtivo)
        {
            EnviarOrdemMovimentoMapa(worldPos);
            return;
        }

        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens == null) return;

        if (!desenhadorOrdens.modoPatrulhaAtivo && !desenhadorOrdens.modoSeguirAtivo && !desenhadorOrdens.modoAtaqueAtivo)
            return;

        if (desenhadorOrdens.modoPatrulhaAtivo)
        {
            desenhadorOrdens.AdicionarPontoPatrulhaDoMenu(worldPos);
            SetText(ordemFeedback, $"✔ Ponto de patrulha adicionado em {worldPos.x:F0}, {worldPos.z:F0}\nENTER confirma. ESC ou Botão Direito cancela.");
        }
        else if (desenhadorOrdens.modoSeguirAtivo)
        {
            GameObject alvo = EncontrarUnidadeProxima(worldPos, 150f);
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

        if (modoMovimentoMapaAtivo)
        {
            EnviarOrdemMovimentoMapa(ConverterLocalParaMundo(localPos));
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

    private void EnviarOrdemMovimentoMapa(Vector3 destino)
    {
        int enviadas = 0;
        foreach (var unidade in unidadesSelecionadasMenu)
        {
            if (unidade != null && unidade.EmitirOrdemMover(destino))
            {
                enviadas++;
            }
        }

        modoMovimentoMapaAtivo = false;
        SetText(ordemFeedback, enviadas > 0
            ? $"Movimento enviado para {enviadas} unidade(s)."
            : "Nenhuma ordem aceita; navios precisam de um ponto na água.");
        AdicionarLog("OPS", $"Ordem de movimento no mapa: {enviadas} unidade(s) aceitas.", enviadas > 0 ? "normal" : "alerta");
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

    private void AtualizarCameraSeguimento(GameObject alvo)
    {
        if (alvo == null || CameraUnidadeHUD.Instancia == null)
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
