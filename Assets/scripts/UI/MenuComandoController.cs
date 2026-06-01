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

    [Header("Referências Opcionais")]
    [Tooltip("Prefixo dos GameObjects de unidade inimiga para identificação (Tag)")]
    [SerializeField] private string tagInimigo = "Inimigo";

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
    private bool arrastandoMapa = false;
    private Vector2 ultimaPosicaoMouseDrag;

    // Elementos do mapa
    private VisualElement mapaUnidadesLayer;
    private VisualElement mapaLinhasLayer;
    private VisualElement radarSweep;
    private float radarAngulo;

    // Elementos FLIR
    private VisualElement flirImagem;
    private Label flirAlerta;
    private Label flirTc;
    private Label flirUnidadeNome;
    private RenderTexture flirRT;

    // Telemetria
    private Label unidadeNome;
    private Label unidadeEmoji;
    private Label statTipo;
    private Label statStatus;
    private Label statPos;
    private Label statTeam;
    private Label hpValor;
    private Label fuelValor;
    private VisualElement hpBar;
    private VisualElement fuelBar;

    // SITREP
    private Label sitrepAliados;
    private Label sitrepInimigos;
    private Label sitrepVel;
    private Label sitrepAmeaca;
    private Label sitrepSel;
    private Label sitrepTempo;
    private Label headerTempo;

    // Log
    private VisualElement logContainer;
    private ScrollView logScroll;

    // Ordens
    private Label ordemFeedback;

    // Mapa — cache de VisualElements por instância
    private readonly Dictionary<int, VisualElement> mapaElementos = new Dictionary<int, VisualElement>();

    // Unidade selecionada DENTRO DO MENU
    private ControleUnidade unidadeSelecionadaMenu; // Unidade focada (telemetria e FLIR)
    private readonly List<ControleUnidade> unidadesSelecionadasMenu = new List<ControleUnidade>(); // Lista de todas as selecionadas no menu

    // Referências a sistemas do jogo
    private GerenteSelecao gerenteSelecao;
    private DesenharLinhasOrdem desenhadorOrdens;

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

        uiDoc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        root = uiDoc.rootVisualElement;

        // Oculta o menu na inicialização
        root.style.display = DisplayStyle.None;

        BindUI();
        CriarRenderTextureFLIR();
        AdicionarLog("SISTEMA", "Menu Comando inicializado. Tecla [1] para abrir/fechar.", "sistema");
    }

    private void Update()
    {
        // Tecla 1 — toggle
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            if (menuAberto) FecharMenu();
            else AbrirMenu();
            return;
        }

        if (!menuAberto) return;

        // Atalho: tecla A seleciona todas as unidades aliadas no mapa
        if (Input.GetKeyDown(KeyCode.A))
        {
            SelecionarTodasUnidadesAliadas();
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
        if (tickMapa >= 0.1f)
        {
            tickMapa = 0;
            AtualizarMapaTatico();
            AtualizarSitrep();
            AtualizarTelemetriaUnidade();
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        LiberarBloqueioInput();

        if (flirRT != null)
        {
            if (CameraUnidadeHUD.Instanciada)
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
        var miniMapa = FindFirstObjectByType<MiniMapa>();
        if (miniMapa != null)
        {
            miniMapa.gameObject.SetActive(false);
        }

        // Sincroniza a seleção do menu com a seleção atual do jogo
        unidadesSelecionadasMenu.Clear();
        if (gerenteSelecao == null)
            gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();

        if (gerenteSelecao != null && gerenteSelecao.unidadesSelecionadas != null)
        {
            foreach (var cu in gerenteSelecao.unidadesSelecionadas)
            {
                if (cu != null && !unidadesSelecionadasMenu.Contains(cu))
                {
                    unidadesSelecionadasMenu.Add(cu);
                }
            }
        }

        // Define a unidade em foco para telemetria/FLIR
        if (unidadesSelecionadasMenu.Count > 0)
        {
            unidadeSelecionadaMenu = unidadesSelecionadasMenu[unidadesSelecionadasMenu.Count - 1];
        }
        else
        {
            unidadeSelecionadaMenu = null;
            SelecionarPrimeiraUnidadeAliada();
        }

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu);

        AdicionarLog("COMANDO", "Menu Tático aberto. Sincronizada seleção.", "sistema");
    }

    public void FecharMenu()
    {
        if (!menuAberto) return;
        menuAberto = false;

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
        var miniMapa = FindFirstObjectByType<MiniMapa>();
        if (miniMapa != null)
        {
            miniMapa.gameObject.SetActive(true);
        }
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
        radarSweep        = root.Q<VisualElement>("radar-sweep");

        flirImagem      = root.Q<VisualElement>("flir-imagem");
        flirAlerta      = root.Q<Label>("flir-label-bc");
        flirTc          = root.Q<Label>("flir-label-tc");
        flirUnidadeNome = root.Q<Label>("flir-unidade-nome");

        unidadeNome  = root.Q<Label>("unidade-nome");
        unidadeEmoji = root.Q<Label>("unidade-emoji");
        statTipo     = root.Q<Label>("stat-tipo");
        statStatus   = root.Q<Label>("stat-status");
        statPos      = root.Q<Label>("stat-pos");
        statTeam     = root.Q<Label>("stat-team");
        hpValor      = root.Q<Label>("hp-valor");
        fuelValor    = root.Q<Label>("fuel-valor");
        hpBar        = root.Q<VisualElement>("hp-bar");
        fuelBar      = root.Q<VisualElement>("fuel-bar");

        sitrepAliados  = root.Q<Label>("sitrep-aliados");
        sitrepInimigos = root.Q<Label>("sitrep-inimigos");
        sitrepVel      = root.Q<Label>("sitrep-vel");
        sitrepAmeaca   = root.Q<Label>("sitrep-ameaca");
        sitrepSel      = root.Q<Label>("sitrep-sel");
        sitrepTempo    = root.Q<Label>("sitrep-tempo");
        headerTempo    = root.Q<Label>("header-tempo-op");

        logContainer = root.Q<VisualElement>("log-container");
        logScroll    = root.Q<ScrollView>("log-scroll");

        ordemFeedback = root.Q<Label>("ordem-feedback");

        // Botões de ordem
        var btnAtivo = root.Q<Button>("btn-ativo");
        if (btnAtivo != null) btnAtivo.clicked += () => ExecutarOrdem("ATIVO");

        var btnPassivo = root.Q<Button>("btn-passivo");
        if (btnPassivo != null) btnPassivo.clicked += () => ExecutarOrdem("PASSIVO");

        var btnFuncionar = root.Q<Button>("btn-funcionar");
        if (btnFuncionar != null) btnFuncionar.clicked += () => ExecutarOrdem("FUNCIONAR");

        var btnPatrulhar = root.Q<Button>("btn-patrulhar");
        if (btnPatrulhar != null) btnPatrulhar.clicked += () => ExecutarOrdem("PATRULHAR");

        var btnSeguir = root.Q<Button>("btn-seguir");
        if (btnSeguir != null) btnSeguir.clicked += () => ExecutarOrdem("SEGUIR");

        var btnAtacar = root.Q<Button>("btn-atacar");
        if (btnAtacar != null) btnAtacar.clicked += () => ExecutarOrdem("ATACAR");

        var btnVoltarBase = root.Q<Button>("btn-voltar-base");
        if (btnVoltarBase != null) btnVoltarBase.clicked += () => ExecutarOrdem("VOLTAR_BASE");

        var btnSelTudo = root.Q<Button>("btn-selecionar-tudo");
        if (btnSelTudo != null) btnSelTudo.clicked += () => SelecionarTodasUnidadesAliadas();

        // Registro de ouvintes de eventos para Zoom, Pan e Cliques no Mapa
        var painelMapa = root.Q<VisualElement>("painel-mapa");
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
                    OnMapRightClicked();
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
                        mapaCentro.x = Mathf.Clamp(mapaCentro.x, -mundoMetade, mundoMetade);
                        mapaCentro.y = Mathf.Clamp(mapaCentro.y, -mundoMetade, mundoMetade);
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
                    CameraUnidadeHUD.Instancia.AddRotation(deltaX * 0.5f);
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

        // Atualiza título do mapa com zoom ativo
        var titulo = root.Q<Label>("mapa-titulo");
        if (titulo != null)
        {
            titulo.text = $"◉ MAPA TÁTICO (ZOOM: {mapaZoom:F1}X)";
        }

        // Calcula a nova janela de visualização baseada no Zoom e Pan
        float rangeX = (mundoMetade * 2f) / mapaZoom;
        float rangeZ = (mundoMetade * 2f) / mapaZoom;
        float xMin = mapaCentro.x - rangeX / 2f;
        float zMin = mapaCentro.y - rangeZ / 2f;

        // Busca todas as identidades (aliadas + inimigas)
        var todasIdentidades = new List<IdentidadeUnidade>(
            FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None));

        var vivos = new HashSet<int>();

        foreach (var id in todasIdentidades)
        {
            if (id == null || !id.gameObject.activeInHierarchy) continue;

            int instId = id.gameObject.GetInstanceID();
            vivos.Add(instId);

            bool amigo   = id.teamID == 1;
            bool inimigo = id.teamID == 2;
            if (!amigo && !inimigo) continue;

            Vector3 pos3D = id.transform.position;

            // Converte para % (0-100) usando a janela visível
            float pctX = ((pos3D.x - xMin) / rangeX) * 100f;
            float pctZ = (1f - (pos3D.z - zMin) / rangeZ) * 100f;

            // Obtém HP via SistemaDeDanos
            float hpPct = 1f;
            ControleUnidade cu = id.GetComponent<ControleUnidade>();
            SistemaDeDanos sdMapa = id.GetComponent<SistemaDeDanos>();
            if (sdMapa != null && sdMapa.vidaMaxima > 0f)
                hpPct = Mathf.Clamp01(sdMapa.vidaAtual / sdMapa.vidaMaxima);

            string emoji = ObterEmojiUnidade(id);

            if (!mapaElementos.TryGetValue(instId, out VisualElement el) || el == null)
            {
                el = CriarElementoMapa(id, amigo, emoji);
                mapaElementos[instId] = el;
                mapaUnidadesLayer.Add(el);
            }

            // Atualiza posição e visibilidade (se fora do mapa aproximado, esconde para economizar render)
            el.style.left = new StyleLength(new Length(pctX, LengthUnit.Percent));
            el.style.top  = new StyleLength(new Length(pctZ, LengthUnit.Percent));
            el.style.display = (pctX >= -5f && pctX <= 105f && pctZ >= -5f && pctZ <= 105f) ? DisplayStyle.Flex : DisplayStyle.None;

            // Atualiza barra de HP
            var hpFill = el.Q<VisualElement>("mapa-hp-fill");
            if (hpFill != null)
                hpFill.style.width = new StyleLength(new Length(hpPct * 100f, LengthUnit.Percent));

            // Atualiza seleção visual
            var ring = el.Q<VisualElement>("mapa-sel-ring");
            if (ring != null)
            {
                bool estasel = cu != null && unidadesSelecionadasMenu.Contains(cu);
                if (estasel) ring.AddToClassList("visivel");
                else         ring.RemoveFromClassList("visivel");
            }

            // Cor correta se HP zerado
            if (hpPct <= 0f)
            {
                var marc = el.Q<VisualElement>("mapa-marcador");
                marc?.RemoveFromClassList("amigo");
                marc?.RemoveFromClassList("inimigo");
                marc?.AddToClassList("destruido");
            }
        }

        // Remove elementos de unidades que não existem mais
        var removidos = new List<int>();
        foreach (var kv in mapaElementos)
        {
            if (!vivos.Contains(kv.Key))
            {
                kv.Value?.RemoveFromHierarchy();
                removidos.Add(kv.Key);
            }
        }
        foreach (var r in removidos) mapaElementos.Remove(r);

        // Desenhar linhas de patrulha/ataque na UI
        DesenharLinhasOrdemNoMapaUI();
    }

    private VisualElement CriarElementoMapa(IdentidadeUnidade id, bool amigo, string emoji)
    {
        string classFacao = amigo ? "amigo" : "inimigo";

        var container = new VisualElement();
        container.AddToClassList("mapa-unidade");
        container.name = $"mapa-unit-{id.gameObject.GetInstanceID()}";

        // Label com nome
        var label = new Label(id.name.Length > 10 ? id.name.Substring(0, 10) : id.name);
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
        ControleUnidade cu = id.GetComponent<ControleUnidade>();
        if (cu != null)
        {
            var capturedCu = cu;
            container.RegisterCallback<ClickEvent>(evt =>
            {
                if (desenhadorOrdens == null)
                    desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

                if (desenhadorOrdens != null && (desenhadorOrdens.modoPatrulhaAtivo || desenhadorOrdens.modoSeguirAtivo || desenhadorOrdens.modoAtaqueAtivo))
                {
                    var painelMapa = root.Q<VisualElement>("painel-mapa");
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

        return container;
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
            if (flirUnidadeNome != null) flirUnidadeNome.text = "SEM SINAL";
            if (flirAlerta != null) flirAlerta.text = "FLIR OFF-LINE";
            if (ordemFeedback != null) ordemFeedback.text = "Nenhuma unidade selecionada — clique no mapa";
            if (CameraUnidadeHUD.Instanciada) CameraUnidadeHUD.Instancia.DefinirTarget(null);
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
            AdicionarLog("OPS", $"Unidade desmarcada: {cu.name} (Total: {unidadesSelecionadasMenu.Count})", "normal");
        }
        else
        {
            unidadesSelecionadasMenu.Add(cu);
            unidadeSelecionadaMenu = cu; // Foca na mais recente
            AdicionarLog("OPS", $"Unidade selecionada: {cu.name} (Total: {unidadesSelecionadasMenu.Count})", "normal");
        }

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu);

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
    }

    private string ObterNomeExibicao(GameObject obj)
    {
        if (obj == null) return "DESCONHECIDO";
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id != null && !string.IsNullOrEmpty(id.nomeDeBatismo)) return id.nomeDeBatismo.ToUpper();
        return obj.name.ToUpper();
    }

    private void SelecionarPrimeiraUnidadeAliada()
    {
        var ids = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var id in ids)
        {
            if (id.teamID == 1)
            {
                var cu = id.GetComponent<ControleUnidade>();
                if (cu != null)
                {
                    SelecionarUnidadeNoMenu(cu);
                    return;
                }
            }
        }
    }

    private void SelecionarTodasUnidadesAliadas()
    {
        unidadesSelecionadasMenu.Clear();
        unidadeSelecionadaMenu = null;

        var ids = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var id in ids)
        {
            if (id != null && id.teamID == 1)
            {
                var cu = id.GetComponent<ControleUnidade>();
                if (cu != null)
                {
                    unidadesSelecionadasMenu.Add(cu);
                    unidadeSelecionadaMenu = cu; // Foca na última
                }
            }
        }

        // Conecta câmera FLIR à unidade focada
        if (CameraUnidadeHUD.Instancia != null)
            CameraUnidadeHUD.Instancia.DefinirTarget(unidadeSelecionadaMenu);

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
    }

    // -----------------------------------------------------------------------
    // Telemetria
    // -----------------------------------------------------------------------
    private void AtualizarTelemetriaUnidade()
    {
        if (unidadeSelecionadaMenu == null)
        {
            SetText(unidadeNome, "NENHUMA");
            SetText(unidadeEmoji, "❓");
            SetText(statTipo, "—");
            SetText(statStatus, "—");
            SetText(statPos, "—");
            SetText(statTeam, "—");
            SetText(hpValor, "—%");
            SetText(fuelValor, "—%");
            SetBarWidth(hpBar, 0f);
            SetBarWidth(fuelBar, 0f);
            return;
        }

        ControleUnidade cu = unidadeSelecionadaMenu;

        // Nome
        SetText(unidadeNome, cu.name.ToUpper());

        // Emoji + tipo
        IdentidadeUnidade id = cu.GetComponent<IdentidadeUnidade>();
        if (id != null)
        {
            SetText(unidadeEmoji, ObterEmojiUnidade(id));
            SetText(statTipo, id.tipoUnidade.ToString().ToUpper());
            SetText(statTeam, id.teamID == 1 ? "ALIADO" : "INIMIGO");
        }

        // Posição
        Vector3 p = cu.transform.position;
        SetText(statPos, $"{p.x:F0}, {p.z:F0}");

        // Status
        bool passivo;
        string descStatus;
        if (cu.TryObterEstadoCombate(out passivo, out descStatus))
            SetText(statStatus, passivo ? "PASSIVO" : "ATIVO");
        else
            SetText(statStatus, "OK");

        // HP — via SistemaDeDanos
        float hpPct = 1f;
        SistemaDeDanos sd = cu.GetComponent<SistemaDeDanos>();
        if (sd != null && sd.vidaMaxima > 0f)
            hpPct = Mathf.Clamp01(sd.vidaAtual / sd.vidaMaxima);
        int hpInt = Mathf.RoundToInt(hpPct * 100f);
        SetText(hpValor, $"{hpInt}%");
        SetBarWidth(hpBar, hpPct);

        // Cor da barra de HP
        if (hpBar != null)
        {
            hpBar.style.backgroundColor =
                hpInt > 60 ? new Color(0f, 0.9f, 1f) :
                hpInt > 25 ? new Color(1f, 0.67f, 0f) :
                             new Color(1f, 0.2f, 0.2f);
        }

        // Combustível
        CombustivelUnidade cbu = cu.GetComponent<CombustivelUnidade>();
        if (cbu != null && cbu.Capacidade > 0f)
        {
            float fuelPct = Mathf.Clamp01(cbu.CombustivelAtual / cbu.Capacidade);
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
    }

    // -----------------------------------------------------------------------
    // SITREP
    // -----------------------------------------------------------------------
    private void AtualizarSitrep()
    {
        var ids   = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        int aliados  = 0;
        int inimigos = 0;

        foreach (var id in ids)
        {
            if (!id.gameObject.activeInHierarchy) continue;
            if (id.teamID == 1) aliados++;
            else if (id.teamID == 2) inimigos++;
        }

        SetText(sitrepAliados,  aliados.ToString());
        SetText(sitrepInimigos, inimigos.ToString());
        SetText(sitrepSel,      unidadeSelecionadaMenu != null ? unidadeSelecionadaMenu.name : "—");

        // Velocidade
        if (unidadeSelecionadaMenu != null)
        {
            float vel = 0f;
            Rigidbody rb = unidadeSelecionadaMenu.GetComponent<Rigidbody>();
            if (rb != null) vel = rb.linearVelocity.magnitude * 3.6f; // m/s to km/h
            else
            {
                UnityEngine.AI.NavMeshAgent nav = unidadeSelecionadaMenu.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null) vel = nav.velocity.magnitude * 3.6f;
            }
            SetText(sitrepVel, $"{vel:F0} KM/H");
        }
        else
        {
            SetText(sitrepVel, "0 KM/H");
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

            case "FUNCIONAR":
                foreach (var u in unidadesSelecionadasMenu)
                {
                    if (u != null)
                    {
                        // Envia mensagem genérica para qualquer componente que suporte "AlternarFuncionamento"
                        u.gameObject.SendMessage("AlternarFuncionamento", SendMessageOptions.DontRequireReceiver);
                        
                        // Fallback manual para caminhão se existir a variável
                        var caminhao = u.GetComponent<CaminhaoTanqueAbastecimento>();
                        if (caminhao != null)
                        {
                            caminhao.abastecerAutomaticamente = !caminhao.abastecerAutomaticamente;
                        }
                    }
                }
                SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → FUNCIONAMENTO ALTERNADO");
                AdicionarLog("OPS", $"{snapshot.Count} unidades: funcionamento alternado", "normal");
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
                    SetText(ordemFeedback, $"✔ [{snapshot.Count} UDS] → SEGUIR\nClique em uma unidade aliada no mapa.");
                    AdicionarLog("OPS", $"{snapshot.Count} unidades: modo seguir iniciado — clique na unidade alvo", "normal");
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
        }
    }

    // -----------------------------------------------------------------------
    // Log de comunicações
    // -----------------------------------------------------------------------
    private void AdicionarLog(string fonte, string msg, string tipo)
    {
        var now  = DateTime.Now;
        string t = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}";
        logs.Add((t, fonte, msg, tipo));

        if (logs.Count > 60) logs.RemoveAt(0);

        if (logContainer == null) return;

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

        logContainer.Add(entry);

        // Auto-scroll para o fundo
        logContainer.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            logScroll?.ScrollTo(entry);
        });
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

    // ── MÉTODOS DE CONTROLE DO MAPA (ZOOM, PAN, CLIQUE E LINHAS) ─────────────
    private void AlterarZoom(float zoomDelta, Vector2 localMousePos)
    {
        var painelMapa = root.Q<VisualElement>("painel-mapa");
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

                mapaCentro.x = Mathf.Clamp(mapaCentro.x, -mundoMetade, mundoMetade);
                mapaCentro.y = Mathf.Clamp(mapaCentro.y, -mundoMetade, mundoMetade);
            }
        }
    }

    private Vector3 ConverterLocalParaMundo(Vector2 localPos)
    {
        var painelMapa = root.Q<VisualElement>("painel-mapa");
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
        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens == null) return;

        if (!desenhadorOrdens.modoPatrulhaAtivo && !desenhadorOrdens.modoSeguirAtivo && !desenhadorOrdens.modoAtaqueAtivo)
            return;

        Vector3 worldPos = ConverterLocalParaMundo(localPos);

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
                desenhadorOrdens.AplicarOrdemSeguirDoMenu(alvo.transform);
                SetText(ordemFeedback, $"✔ Ordem SEGUIR enviada para {alvo.name}.");
                AdicionarLog("OPS", $"Seguir alvo {alvo.name} confirmado.", "normal");
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
                AdicionarLog("OPS", $"Ataque ao alvo {alvo.name} confirmado.", "alerta");
            }
            else
            {
                SetText(ordemFeedback, $"✔ Ordem ATAQUE DE ÁREA enviada para {worldPos.x:F0}, {worldPos.z:F0}.");
                AdicionarLog("OPS", $"Ataque de área confirmado em {worldPos.x:F0}, {worldPos.z:F0}.", "alerta");
            }
        }
    }

    private void OnMapRightClicked()
    {
        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens != null)
        {
            desenhadorOrdens.CancelarModo();
            SetText(ordemFeedback, "Ordem cancelada.");
            AdicionarLog("OPS", "Ação cancelada pelo usuário.", "normal");
        }
    }

    private GameObject EncontrarUnidadeProxima(Vector3 worldPos, float raioMaximo, bool ignorarTimeJogador = false)
    {
        var ids = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        GameObject melhorAlvo = null;
        float menorDist = raioMaximo;
        foreach (var id in ids)
        {
            if (id == null || !id.gameObject.activeInHierarchy) continue;
            if (ignorarTimeJogador && id.teamID == 1) continue;
            
            float dist = Vector3.Distance(new Vector3(id.transform.position.x, 0, id.transform.position.z), new Vector3(worldPos.x, 0, worldPos.z));
            if (dist < menorDist)
            {
                menorDist = dist;
                melhorAlvo = id.gameObject;
            }
        }
        return melhorAlvo;
    }

    private void DesenharLinhasOrdemNoMapaUI()
    {
        if (mapaLinhasLayer == null) return;
        mapaLinhasLayer.Clear();

        if (desenhadorOrdens == null)
            desenhadorOrdens = FindFirstObjectByType<DesenharLinhasOrdem>();

        if (desenhadorOrdens == null) return;

        float W = mapaLinhasLayer.resolvedStyle.width;
        float H = mapaLinhasLayer.resolvedStyle.height;

        if (W <= 0 || H <= 0) return;

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

        VisualElement line = new VisualElement();
        line.style.position = Position.Absolute;
        line.style.left = p1.x;
        line.style.top = p1.y;
        line.style.width = d;
        line.style.height = 2f;
        line.style.backgroundColor = cor;
        line.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(0), Length.Percent(50)));
        line.style.rotate = new StyleRotate(new Rotate(angle));
        
        mapaLinhasLayer.Add(line);
    }
}
