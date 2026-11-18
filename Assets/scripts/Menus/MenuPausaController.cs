using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hegemonia.RTS;

[DefaultExecutionOrder(12000)]
public class MenuPausaController : MonoBehaviour
{
    public static bool EstaPausado { get; private set; }
    private const string ChaveVSync = "hegemonia.video.vsync";
    private static bool vSyncAtivo = true;
    private static bool configuracaoVideoInicializada;

    public static bool VSyncAtivo
    {
        get
        {
            InicializarConfiguracaoVideo();
            return vSyncAtivo;
        }
    }

    private readonly Color corOverlay = new Color(0f, 0f, 0f, 0.56f);
    private readonly Color corPainel = new Color(0.05f, 0.1f, 0.13f, 0.95f);
    private readonly Color corPainelTopo = new Color(0.08f, 0.18f, 0.22f, 0.97f);
    private readonly Color corBorda = new Color(0.47f, 0.9f, 1f, 0.38f);
    private readonly Color corBotao = new Color(0.06f, 0.12f, 0.15f, 0.96f);
    private readonly Color corBotaoDestaque = new Color(0.08f, 0.3f, 0.42f, 0.98f);
    private readonly Color corBotaoHover = new Color(0.12f, 0.22f, 0.27f, 0.98f);
    private readonly Color corBotaoSair = new Color(0.29f, 0.09f, 0.11f, 0.96f);
    private readonly Color corTexto = new Color(0.92f, 0.98f, 1f, 1f);
    private readonly Color corTextoSuave = new Color(0.74f, 0.86f, 0.91f, 1f);
    private readonly Color corTextoAlerta = new Color(1f, 0.74f, 0.68f, 1f);

    private Font fontePadrao;
    private SistemaSaveGame sistemaSave;
    private Canvas canvasMenu;
    private GameObject raizMenu;
    private GameObject painelPrincipalBotoes;
    private GameObject painelSubConfiguracoes;
    private GameObject painelSubEstatisticas;
    private Text textoEstatisticas;
    private Text statusText;
    private GameObject statusBoxObject;
    private GameObject hudTempoObject;
    private Text textoVelocidade;
    private Text textoBotaoPausa;
    private RawImage imagemVelocidadeAtual;
    private RawImage imagemBotaoPausa;
    private Texture2D texturaMapaTempo;
    private Texture2D texturaVelocidade1x;
    private Texture2D texturaDiminuirTempo;
    private Texture2D texturaPausarTempo;
    private Texture2D texturaAumentarTempo;
    private Texture2D texturaVelocidade2x;
    private Texture2D texturaNavioAviao;
    private Texture2D texturaMolduraTempo;
    private int cicloHUDN;
    private float velocidadeHUDAnterior = -1f;
    private float velocidadeFallbackAtual = 1f;
    private bool estadoPausaHUDAnterior;
    private bool tempoOcultoPorAeroporto;
    private bool estadoTempoAntesAeroporto = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarBootstrap()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtiva.name))
        {
            return;
        }

        if (Object.FindFirstObjectByType<MenuPausaController>() != null)
        {
            return;
        }

        new GameObject("MenuPausaController").AddComponent<MenuPausaController>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InicializarConfiguracaoVideo()
    {
        if (configuracaoVideoInicializada) return;
        vSyncAtivo = PlayerPrefs.GetInt(ChaveVSync, 1) != 0;
        AplicarConfiguracaoVideo();
        configuracaoVideoInicializada = true;
    }

    private static void AlternarConfiguracaoVideo()
    {
        InicializarConfiguracaoVideo();
        vSyncAtivo = !vSyncAtivo;
        PlayerPrefs.SetInt(ChaveVSync, vSyncAtivo ? 1 : 0);
        PlayerPrefs.Save();
        AplicarConfiguracaoVideo();
    }

    public static void DefinirVSync(bool ativo)
    {
        InicializarConfiguracaoVideo();
        vSyncAtivo = ativo;
        PlayerPrefs.SetInt(ChaveVSync, vSyncAtivo ? 1 : 0);
        PlayerPrefs.Save();
        AplicarConfiguracaoVideo();
    }

    private static void AplicarConfiguracaoVideo()
    {
        QualitySettings.vSyncCount = vSyncAtivo ? 1 : 0;
        if (!vSyncAtivo) Application.targetFrameRate = -1;
    }

    private void Awake()
    {
        // O estado ético é estatico e pode sobreviver a uma troca de cena ou
        // a uma nova execucao do Play Mode quando o Domain Reload está
        // desativado. Nesse caso o painel já não existe, mas o relogio e as
        // IAs continuam acreditando que a partida está pausada.
        EstaPausado = false;
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (relogio != null)
        {
            relogio.ResetSimulationSpeed();
            relogio.SetPaused(false);
        }
        else
        {
            velocidadeFallbackAtual = Hegemonia.RTS.RTSSimulationClock.NormalizarVelocidade(1f);
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        Scene cenaAtiva = SceneManager.GetActiveScene();
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtiva.name))
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        fontePadrao = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        sistemaSave = SistemaSaveGame.GarantirInstancia();
        GarantirEventSystem();
        ConstruirInterface();
        FecharMenuVisual();
    }

    private void Update()
    {
        if (RTSInputBindings.IsTextEntryFocused())
            return;

        if (QuartelMenuUIController.EntradaGlobalBloqueada)
        {
            return;
        }

        if (FabricaMineriosMenuController.EstaAberto || FazendaMenuController.EstaAberto)
        {
            return;
        }

        if (AudioSettingsPanelUI.EstaAberto)
        {
            return;
        }

        ProcessarAtalhosTempo();

        if (Input.GetKeyDown(KeyCode.Escape) && !EstaDigitandoEmCampoTexto())
        {
            AlternarMenu();
        }

        AtualizarVisibilidadeTempoPorAeroporto();
        AtualizarHUDTempo();
    }

    private void AtualizarVisibilidadeTempoPorAeroporto()
    {
        if (hudTempoObject == null) return;

        bool aeroportoAberto = GestorMenusExclusivos.EhMenuAeroportoAtivo;
        if (aeroportoAberto)
        {
            if (!tempoOcultoPorAeroporto)
            {
                estadoTempoAntesAeroporto = hudTempoObject.activeSelf;
                hudTempoObject.SetActive(false);
                tempoOcultoPorAeroporto = true;
            }
        }
        else if (tempoOcultoPorAeroporto)
        {
            hudTempoObject.SetActive(estadoTempoAntesAeroporto);
            tempoOcultoPorAeroporto = false;
        }
    }

    private void OnDestroy()
    {
        if (EstaPausado)
        {
            RestaurarFluxoNormal();
        }
    }

    private void AlternarMenu()
    {
        if (EstaPausado)
        {
            RetomarJogo();
        }
        else
        {
            AbrirMenu();
        }
    }

    private void AbrirMenu()
    {
        if (raizMenu == null)
        {
            ConstruirInterface();
        }

        DefinirPausaSimulacao(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        raizMenu.SetActive(true);
        // O aviso de pausa nao deve ficar por cima do ultimo botao (principalmente
        // "Sair para menu principal"). Os avisos de salvar/carregar continuam
        // aparecendo quando realmente existe uma operacao para informar.
        OcultarStatus();
    }

    private void FecharMenuVisual()
    {
        VoltarParaMenuPrincipalPausa();
        if (raizMenu != null)
        {
            raizMenu.SetActive(false);
        }
    }

    private void RestaurarFluxoNormal()
    {
        DefinirPausaSimulacao(false);
        FecharMenuVisual();
    }

    private void DefinirPausaSimulacao(bool pausado)
    {
        Hegemonia.RTS.RTSGameSession sessao = Hegemonia.RTS.RTSGameSession.Instancia;
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;

        if (relogio != null)
        {
            relogio.SetPaused(pausado);
        }
        else if (sessao != null)
        {
            sessao.SetPaused(pausado);
        }

        if (relogio == null)
        {
            bool pausaEfetiva = sessao != null
                ? sessao.Phase == Hegemonia.RTS.RTSSessionPhase.Paused
                : pausado;
            bool partidaFinalizada = sessao != null
                && sessao.Phase == Hegemonia.RTS.RTSSessionPhase.Finished;
            Time.timeScale = pausaEfetiva || partidaFinalizada ? 0f : velocidadeFallbackAtual;
            AudioListener.pause = pausaEfetiva || partidaFinalizada;
        }

        if (sessao != null)
        {
            EstaPausado = sessao.Phase == Hegemonia.RTS.RTSSessionPhase.Paused;
        }
        else if (relogio != null)
        {
            EstaPausado = relogio.Paused;
        }
        else
        {
            EstaPausado = pausado;
        }

        // Ao sair de uma tela de resultado, preserve o fluxo atual de troca de cena.
        if (!pausado && sessao != null && sessao.Phase == Hegemonia.RTS.RTSSessionPhase.Finished)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            velocidadeFallbackAtual = 1f;
        }
    }

    private void ProcessarAtalhosTempo()
    {
        if (EstaDigitandoEmCampoTexto()) return;

        if (Input.GetKeyDown(KeyCode.Alpha4)) AlternarHUDTempo();
        if (Input.GetKeyDown(KeyCode.N)) CiclarHUDTempoEMiniMapa();

        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        SincronizarEstadoPausa();
        if (Input.GetKeyDown(KeyCode.Alpha6)) AlternarPausaTempo();
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            if (relogio != null) relogio.AdjustSimulationSpeed(-1);
            else AjustarVelocidadeFallback(-1);
        }
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            if (relogio != null) relogio.AdjustSimulationSpeed(1);
            else AjustarVelocidadeFallback(1);
        }
    }

    private void AlternarHUDTempo()
    {
        if (hudTempoObject == null) return;
        hudTempoObject.SetActive(!hudTempoObject.activeSelf);
        cicloHUDN = ResolverEstadoCicloHUDN();
    }

    private void CiclarHUDTempoEMiniMapa()
    {
        cicloHUDN = cicloHUDN >= 4 ? 1 : cicloHUDN + 1;
        bool mostrarTempo = cicloHUDN == 1 || cicloHUDN == 4;
        bool mostrarMiniMapa = cicloHUDN == 1 || cicloHUDN == 2;

        if (hudTempoObject != null)
        {
            hudTempoObject.SetActive(mostrarTempo);
        }

        MiniMapa miniMapa = Object.FindFirstObjectByType<MiniMapa>(FindObjectsInactive.Include);
        if (miniMapa != null)
        {
            miniMapa.DefinirVisibilidadePeloAtalho(mostrarMiniMapa);
        }
    }

    private int ResolverEstadoCicloHUDN()
    {
        bool mostrarTempo = hudTempoObject != null && hudTempoObject.activeSelf;
        MiniMapa miniMapa = Object.FindFirstObjectByType<MiniMapa>(FindObjectsInactive.Include);
        bool mostrarMiniMapa = miniMapa != null && miniMapa.VisibilidadeSolicitada;

        if (mostrarTempo && mostrarMiniMapa) return 1;
        if (!mostrarTempo && mostrarMiniMapa) return 2;
        if (!mostrarTempo && !mostrarMiniMapa) return 3;
        return 0;
    }

    private void AlternarPausaTempo()
    {
        if (EstaPausado)
        {
            RestaurarFluxoNormal();
        }
        else
        {
            DefinirPausaSimulacao(true);
        }

        AtualizarHUDTempo();
    }

    private void ReduzirVelocidadeTempo()
    {
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (relogio != null) relogio.AdjustSimulationSpeed(-1);
        else AjustarVelocidadeFallback(-1);
        AtualizarHUDTempo();
    }

    private void AumentarVelocidadeTempo()
    {
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (relogio != null) relogio.AdjustSimulationSpeed(1);
        else AjustarVelocidadeFallback(1);
        AtualizarHUDTempo();
    }

    private void RestaurarVelocidadeNormal()
    {
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (relogio != null) relogio.ResetSimulationSpeed();
        else DefinirVelocidadeFallback(1f);
        AtualizarHUDTempo();
    }

    private void AjustarVelocidadeFallback(int direcao)
    {
        float proxima = Hegemonia.RTS.RTSSimulationClock.ProximaVelocidade(velocidadeFallbackAtual, direcao);
        DefinirVelocidadeFallback(proxima);
    }

    private void DefinirVelocidadeFallback(float velocidade)
    {
        float normalizada = Hegemonia.RTS.RTSSimulationClock.NormalizarVelocidade(velocidade);
        if (normalizada > 1f && GovernadorGameplayRTS.BloquearAceleracaoTempo(out string motivoBloqueio))
        {
            normalizada = 1f;
            HUDAjudaRTS.MostrarMensagemTemporaria(motivoBloqueio, 2.8f);
        }

        velocidadeFallbackAtual = normalizada;
        if (!EstaPausado)
        {
            Time.timeScale = velocidadeFallbackAtual;
        }
    }

    private void SincronizarEstadoPausa()
    {
        Hegemonia.RTS.RTSGameSession sessao = Hegemonia.RTS.RTSGameSession.Instancia;
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (sessao != null)
        {
            EstaPausado = sessao.Phase == Hegemonia.RTS.RTSSessionPhase.Paused;
        }
        else if (relogio != null)
        {
            EstaPausado = relogio.Paused;
        }
    }

    private void AtualizarHUDTempo()
    {
        SincronizarEstadoPausa();
        Hegemonia.RTS.RTSSimulationClock relogio = Hegemonia.RTS.RTSSimulationClock.Instancia;
        if (relogio == null && !EstaPausado
            && !Mathf.Approximately(Time.timeScale, velocidadeFallbackAtual))
        {
            // Outros sistemas podem forcar 1x (por exemplo, quando a aceleracao
            // e bloqueada). Mantem o HUD e a retomada alinhados ao valor real.
            velocidadeFallbackAtual = Hegemonia.RTS.RTSSimulationClock.NormalizarVelocidade(Time.timeScale);
        }

        float velocidade = relogio != null
            ? relogio.SimulationSpeed
            : Hegemonia.RTS.RTSSimulationClock.NormalizarVelocidade(velocidadeFallbackAtual);

        if (textoVelocidade != null && !Mathf.Approximately(velocidadeHUDAnterior, velocidade))
        {
            textoVelocidade.text = Hegemonia.RTS.RTSSimulationClock.RotuloVelocidade(velocidade);
            velocidadeHUDAnterior = velocidade;
        }

        if (imagemVelocidadeAtual != null)
        {
            bool velocidadeNormal = Mathf.Approximately(velocidade, 1f);
            bool velocidadeDupla = Mathf.Approximately(velocidade, 6.75f);
            imagemVelocidadeAtual.texture = velocidadeNormal ? texturaVelocidade1x
                : velocidadeDupla ? texturaVelocidade2x : null;
            imagemVelocidadeAtual.color = imagemVelocidadeAtual.texture != null
                ? Color.white
                : new Color(0.08f, 0.1f, 0.045f, 0.98f);
            if (textoVelocidade != null)
            {
                textoVelocidade.enabled = imagemVelocidadeAtual.texture == null;
            }
        }

        if (textoBotaoPausa != null && estadoPausaHUDAnterior != EstaPausado)
        {
            if (imagemBotaoPausa != null)
            {
                imagemBotaoPausa.texture = EstaPausado ? null : texturaPausarTempo;
                imagemBotaoPausa.color = Color.white;

                Button botaoPausa = imagemBotaoPausa.GetComponent<Button>();
                if (botaoPausa != null)
                {
                    ColorBlock cores = botaoPausa.colors;
                    cores.normalColor = EstaPausado
                        ? new Color(0.16f, 0.15f, 0.07f, 0.98f)
                        : Color.white;
                    cores.highlightedColor = EstaPausado
                        ? new Color(0.28f, 0.24f, 0.1f, 1f)
                        : new Color(1f, 0.9f, 0.6f, 1f);
                    cores.pressedColor = EstaPausado
                        ? new Color(0.1f, 0.1f, 0.045f, 1f)
                        : new Color(0.78f, 0.78f, 0.78f, 1f);
                    cores.selectedColor = cores.highlightedColor;
                    botaoPausa.colors = cores;
                }

                textoBotaoPausa.enabled = EstaPausado || texturaPausarTempo == null;
            }

            textoBotaoPausa.text = EstaPausado ? "▶ RETOMAR" : "PAUSAR";
            estadoPausaHUDAnterior = EstaPausado;
        }

        if (textoBotaoPausa != null)
        {
            if (EstaPausado)
            {
                float pulso = 0.35f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.65f);
                textoBotaoPausa.color = Color.Lerp(new Color(1f, 0.82f, 0.4f, 1f),
                    new Color(1f, 0.96f, 0.72f, 1f), pulso);
            }
            else
            {
                textoBotaoPausa.color = corTexto;
            }
        }
    }

    private void RetomarJogo()
    {
        RestaurarFluxoNormal();
    }

    private void AlternarIdioma()
    {
        LocalizationManager.Instancia.ProximoIdioma();
        RecriarInterface();
        raizMenu.SetActive(true);
        AbrirSubmenuConfiguracoes();
        AtualizarStatus(string.Format(LocalizationManager.T("pause.settings_language", "Idioma: {0}"), LocalizationManager.Instancia.NomeIdiomaAtual()), false);
    }

    private void AlternarDificuldade()
    {
        GameDifficultyManager.Instancia.ProximaDificuldade();
        RecriarInterface();
        raizMenu.SetActive(true);
        AbrirSubmenuConfiguracoes();
        AtualizarStatus(
            string.Format(
                LocalizationManager.T("pause.settings_difficulty", "Dificuldade: {0}"),
                GameDifficultyManager.Instancia.NomeDificuldadeAtual()),
            false);
    }

    private void AbrirConfiguracoesAudio()
    {
        AudioSettingsPanelUI.Abrir(canvasMenu != null ? canvasMenu.transform : transform);
    }

    private void AlternarVSync()
    {
        AlternarConfiguracaoVideo();
        bool estavaAberto = raizMenu != null && raizMenu.activeSelf;
        RecriarInterface();
        if (estavaAberto)
        {
            raizMenu.SetActive(true);
            AbrirSubmenuConfiguracoes();
        }
        AtualizarStatus(ObterRotuloVSync(LocalizationManager.IdiomaAtual), false);
    }

    private static string ObterRotuloVSync(IdiomaJogo idioma)
    {
        bool ativo = VSyncAtivo;
        if (idioma == IdiomaJogo.EnUs) return "VSync: " + (ativo ? "ON" : "OFF");
        if (idioma == IdiomaJogo.ZhHans) return "垂直同步: " + (ativo ? "开" : "关");
        return "VSync: " + (ativo ? "ATIVADO" : "DESATIVADO");
    }

    private void SalvarJogo()
    {
        sistemaSave.RegistrarCenaAtual(SceneManager.GetActiveScene().name);
        PainelSavesUI.Abrir(canvasMenu.transform, sistemaSave, true, null,
            () => AtualizarStatus(LocalizationManager.T("pause.saved", "Gerenciador de saves fechado."), false));
    }

    private void CarregarJogo()
    {
        if (!sistemaSave.PossuiSave())
        {
            AtualizarStatus(LocalizationManager.T("pause.no_save", "Nenhum save encontrado para carregar."), true);
            return;
        }

        PainelSavesUI.Abrir(canvasMenu.transform, sistemaSave, false, CarregarSaveSelecionado);
    }

    private void CarregarSaveSelecionado(string saveId)
    {
        if (!sistemaSave.TentarCarregarSave(saveId))
        {
            AtualizarStatus("Nao foi possivel carregar a partida selecionada.", true);
            return;
        }

        string cenaDestino = sistemaSave.ObterCenaSalvaOuPadrao(SceneManager.GetActiveScene().name);
        RestaurarFluxoNormal();
        if (!Application.CanStreamedLevelBeLoaded(cenaDestino))
        {
            cenaDestino = SceneManager.GetActiveScene().name;
        }

        FluxoInicialJogo.AutorizarCarga(cenaDestino);
        LoadingScreenService.CarregarCena(cenaDestino, LoadingRequestKind.Save);
    }

    private void ReiniciarPartida()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        sistemaSave.IniciarNovoJogo(cenaAtual);
        RestaurarFluxoNormal();
        FluxoInicialJogo.AutorizarCarga(cenaAtual);
        LoadingScreenService.CarregarCena(cenaAtual, LoadingRequestKind.Save);
    }

    private void SairParaMenuPrincipal()
    {
        RestaurarFluxoNormal();
        SceneManager.LoadScene(ConfiguracaoCenasJogo.ResolverCenaMenuPrincipal());
    }

    private void GarantirEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private bool EstaDigitandoEmCampoTexto()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
        {
            return false;
        }

        GameObject selecionado = eventSystem.currentSelectedGameObject;
        return selecionado.GetComponent<InputField>() != null || selecionado.GetComponent("TMP_InputField") != null;
    }

    private void ConstruirInterface()
    {
        if (canvasMenu != null)
        {
            return;
        }

        canvasMenu = new GameObject("CanvasMenuPausa").AddComponent<Canvas>();
        canvasMenu.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasMenu.sortingOrder = 6500;

        CanvasScaler scaler = canvasMenu.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasMenu.gameObject.AddComponent<GraphicRaycaster>();

        raizMenu = new GameObject("RaizMenuPausa");
        raizMenu.transform.SetParent(canvasMenu.transform, false);

        RectTransform overlay = CriarPainel("Overlay", raizMenu.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, corOverlay);
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Sprite molduraSprite = Resources.Load<Sprite>("UI/MenuPausa/painel_pausa_moldura");
        if (molduraSprite != null)
        {
            ConstruirInterfaceComMoldura(molduraSprite);
        }
        else
        {
            ConstruirInterfaceLegada();
        }

        ConstruirHUDTempo();
        cicloHUDN = ResolverEstadoCicloHUDN();
    }

    private void ConstruirInterfaceComMoldura(Sprite molduraSprite)
    {
        // Proporcao exata da moldura recortada: 775 x 975 -> 680 x 855 em 1920x1080
        RectTransform painel = new GameObject("PainelCentral").AddComponent<RectTransform>();
        painel.SetParent(raizMenu.transform, false);
        painel.anchorMin = new Vector2(0.5f, 0.5f);
        painel.anchorMax = new Vector2(0.5f, 0.5f);
        painel.pivot = new Vector2(0.5f, 0.5f);
        painel.anchoredPosition = new Vector2(0f, -26f);
        painel.sizeDelta = new Vector2(680f, 855f);
        painel.localScale = new Vector3(0.9f, 0.9f, 1f);

        Image imagemMoldura = painel.gameObject.AddComponent<Image>();
        imagemMoldura.sprite = molduraSprite;
        imagemMoldura.preserveAspect = true;
        imagemMoldura.color = Color.white;
        imagemMoldura.raycastTarget = true;

        IdiomaJogo idioma = LocalizationManager.IdiomaAtual;
        bool ehPtBr = idioma == IdiomaJogo.PtBr;

        if (!ehPtBr)
        {
            RectTransform mascaraTitulo = CriarPainel("MascaraTitulo", painel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(540f, 48f), new Color(0.05f, 0.1f, 0.16f, 0.96f));
            CriarTexto("TituloLocalizado", mascaraTitulo, idioma == IdiomaJogo.ZhHans ? "全球霸权" : "GLOBAL HEGEMONY", 30, FontStyle.Bold, TextAnchor.MiddleCenter, corTexto, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        // Area interna dos botoes dentro da moldura
        RectTransform areaInterna = new GameObject("AreaInternaMenu").AddComponent<RectTransform>();
        areaInterna.SetParent(painel, false);
        areaInterna.anchorMin = new Vector2(0.5f, 1f);
        areaInterna.anchorMax = new Vector2(0.5f, 1f);
        areaInterna.pivot = new Vector2(0.5f, 1f);
        areaInterna.anchoredPosition = new Vector2(0f, -228f);
        areaInterna.sizeDelta = new Vector2(578f, 560f);

        painelPrincipalBotoes = new GameObject("BotoesMenuPausa");
        painelPrincipalBotoes.transform.SetParent(areaInterna, false);
        RectTransform rectBotoes = painelPrincipalBotoes.AddComponent<RectTransform>();
        rectBotoes.anchorMin = Vector2.zero;
        rectBotoes.anchorMax = Vector2.one;
        rectBotoes.offsetMin = Vector2.zero;
        rectBotoes.offsetMax = Vector2.zero;

        float posY = 0f;
        CriarBotaoPausaSprite(rectBotoes, "RetomarJogoButton", ehPtBr ? "UI/MenuPausa/btn_retomar_jogo" : "UI/MenuPausa/btn_template_pausa_azul", 76f, RetomarJogo, ref posY, ObterTextoPausa(idioma, "resume"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "ConfiguracoesButton", ehPtBr ? "UI/MenuPausa/btn_configuracoes" : "UI/MenuPausa/btn_template_pausa_normal", 60f, AbrirSubmenuConfiguracoes, ref posY, ObterTextoPausa(idioma, "settings"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "DiplomaciaButton", ehPtBr ? "UI/MenuPausa/btn_diplomacia" : "UI/MenuPausa/btn_template_pausa_normal", 60f, AbrirDiplomacia, ref posY, ObterTextoPausa(idioma, "diplomacy"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "EstatisticasButton", ehPtBr ? "UI/MenuPausa/btn_estatisticas" : "UI/MenuPausa/btn_template_pausa_normal", 60f, AbrirEstatisticas, ref posY, ObterTextoPausa(idioma, "stats"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "SalvarJogoButton", ehPtBr ? "UI/MenuPausa/btn_salvar_jogo" : "UI/MenuPausa/btn_template_pausa_normal", 60f, SalvarJogo, ref posY, ObterTextoPausa(idioma, "save"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "CarregarJogoButton", ehPtBr ? "UI/MenuPausa/btn_carregar_jogo" : "UI/MenuPausa/btn_template_pausa_normal", 60f, CarregarJogo, ref posY, ObterTextoPausa(idioma, "load"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "ReiniciarPartidaButton", ehPtBr ? "UI/MenuPausa/btn_reiniciar_partida" : "UI/MenuPausa/btn_template_pausa_normal", 60f, ReiniciarPartida, ref posY, ObterTextoPausa(idioma, "restart"), !ehPtBr);
        CriarBotaoPausaSprite(rectBotoes, "SairMenuButton", ehPtBr ? "UI/MenuPausa/btn_sair_menu" : "UI/MenuPausa/btn_template_pausa_vermelho", 62f, SairParaMenuPrincipal, ref posY, ObterTextoPausa(idioma, "exit"), !ehPtBr);

        ConstruirSubmenuConfiguracoes(areaInterna, idioma);
        ConstruirSubmenuEstatisticas(areaInterna, idioma);

        RectTransform statusBox = CriarPainel("StatusBox", painel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(540f, 38f), new Color(0.04f, 0.1f, 0.16f, 0.96f));
        Outline statusOutline = statusBox.gameObject.AddComponent<Outline>();
        statusOutline.effectColor = new Color(0.32f, 0.78f, 0.98f, 0.6f);
        statusOutline.effectDistance = new Vector2(1f, -1f);
        statusBoxObject = statusBox.gameObject;
        statusText = CriarTexto("Status", statusBox, string.Empty, 15, FontStyle.Bold, TextAnchor.MiddleCenter, corTextoSuave, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24f, 0f));
        statusBoxObject.SetActive(false);
    }

    private void ConstruirInterfaceLegada()
    {
        RectTransform painel = CriarPainel("PainelCentral", raizMenu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 900f), corPainel);
        CriarPainel("FaixaTopo", painel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 150f), corPainelTopo);
        CriarPainel("LinhaTopo", painel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 3f), new Color(0.56f, 0.86f, 0.93f, 0.9f));
        CriarPainel("Brilho", painel, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

        Outline outline = painel.gameObject.AddComponent<Outline>();
        outline.effectColor = corBorda;
        outline.effectDistance = new Vector2(3f, -3f);

        CriarTexto("Cabecalho", painel, "PAINEL TÁTICO", 13, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.67f, 0.9f, 0.96f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(0f, 24f));
        CriarTexto("Titulo", painel, LocalizationManager.T("pause.header", "HEGEMONIA GLOBAL"), 34, FontStyle.Bold, TextAnchor.UpperCenter, corTexto, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -62f), new Vector2(0f, 42f));
        CriarTexto("Subtitulo", painel, LocalizationManager.T("pause.title", "PAUSADO"), 50, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.43f, 0.93f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -112f), new Vector2(0f, 56f));
        CriarTexto("Descricao", painel, "Controle rápido da campanha, idioma, saves e retorno imediato ao combate.", 15, FontStyle.Normal, TextAnchor.UpperCenter, corTextoSuave, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -168f), new Vector2(-64f, 34f));

        RectTransform botoes = new GameObject("BotoesMenuPausa").AddComponent<RectTransform>();
        botoes.SetParent(painel, false);
        botoes.anchorMin = new Vector2(0.5f, 1f);
        botoes.anchorMax = new Vector2(0.5f, 1f);
        botoes.pivot = new Vector2(0.5f, 1f);
        botoes.anchoredPosition = new Vector2(0f, -238f);
        botoes.sizeDelta = new Vector2(420f, 600f);

        float posicaoY = 0f;
        CriarBotao(botoes, LocalizationManager.T("pause.resume", "Retomar Jogo"), "GO", corBotaoDestaque, RetomarJogo, ref posicaoY);
        CriarBotao(botoes, string.Format(LocalizationManager.T("pause.settings_language", "Idioma: {0}"), LocalizationManager.Instancia.NomeIdiomaAtual()), "LG", corBotao, AlternarIdioma, ref posicaoY);
        CriarBotao(botoes, string.Format(LocalizationManager.T("pause.settings_difficulty", "Dificuldade: {0}"), GameDifficultyManager.Instancia.NomeDificuldadeAtual()), "DF", corBotao, AlternarDificuldade, ref posicaoY);
        CriarBotao(botoes, ObterRotuloVSync(LocalizationManager.IdiomaAtual), "VS", corBotao, AlternarVSync, ref posicaoY);
        CriarBotao(botoes, "Audio", "AU", corBotao, AbrirConfiguracoesAudio, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.load", "Carregar Jogo"), "LD", corBotao, CarregarJogo, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.save", "Salvar Jogo"), "SV", corBotao, SalvarJogo, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.restart", "Reiniciar Partida"), "RE", corBotao, ReiniciarPartida, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.exit_menu", "Sair para Menu Principal"), "EX", corBotaoSair, SairParaMenuPrincipal, ref posicaoY);

        RectTransform statusBox = CriarPainel("StatusBox", painel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(430f, 42f), new Color(0.07f, 0.12f, 0.15f, 0.96f));
        statusBoxObject = statusBox.gameObject;
        statusText = CriarTexto("Status", statusBox, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-32f, 0f));
        statusBoxObject.SetActive(false);
        CriarTexto("Rodape", painel, LocalizationManager.T("pause.footer", "ESC retoma a partida."), 14, FontStyle.Normal, TextAnchor.LowerCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-48f, 20f));
    }

    private void RecriarInterface()
    {
        bool estavaAberto = raizMenu != null && raizMenu.activeSelf;
        if (canvasMenu != null)
        {
            Destroy(canvasMenu.gameObject);
            canvasMenu = null;
            raizMenu = null;
            statusText = null;
            statusBoxObject = null;
            hudTempoObject = null;
            textoVelocidade = null;
            textoBotaoPausa = null;
            velocidadeHUDAnterior = -1f;
            estadoPausaHUDAnterior = false;
        }

        ConstruirInterface();
        if (!estavaAberto)
        {
            FecharMenuVisual();
        }
    }

    private void ConstruirHUDTempo()
    {
        texturaMapaTempo = Resources.Load<Texture2D>("UI/PainelTempo/01_mapa_mundial");
        texturaVelocidade1x = Resources.Load<Texture2D>("UI/PainelTempo/02_velocidade_1x");
        texturaDiminuirTempo = Resources.Load<Texture2D>("UI/PainelTempo/03_diminuir_velocidade");
        texturaPausarTempo = Resources.Load<Texture2D>("UI/PainelTempo/04_pausar");
        texturaAumentarTempo = Resources.Load<Texture2D>("UI/PainelTempo/05_aumentar_velocidade");
        texturaVelocidade2x = Resources.Load<Texture2D>("UI/PainelTempo/06_velocidade_2x");
        texturaNavioAviao = Resources.Load<Texture2D>("UI/PainelTempo/07_navio_aviao");
        texturaMolduraTempo = Resources.Load<Texture2D>("UI/PainelTempo/08_moldura_conectora");

        RectTransform faixa = CriarPainel("ControlesTempoSimulacao", canvasMenu.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f),
            new Vector2(670f, 80f), new Color(0f, 0f, 0f, 0f));
        faixa.localScale = new Vector3(0.75f, 0.75f, 1f);
        hudTempoObject = faixa.gameObject;

        Image fundoFaixa = faixa.GetComponent<Image>();
        if (fundoFaixa != null)
        {
            fundoFaixa.color = Color.clear;
            fundoFaixa.raycastTarget = false;
        }

        CriarImagemHUD("MolduraDourada", faixa, texturaMolduraTempo,
            Vector2.zero, new Vector2(670f, 80f));
        CriarImagemHUD("MapaMundialDecorativo", faixa, texturaMapaTempo,
            new Vector2(-266f, 0f), new Vector2(94f, 52f));
        CriarImagemHUD("NavioEAviaoDecorativo", faixa, texturaNavioAviao,
            new Vector2(264f, 0f), new Vector2(101f, 52f));

        RectTransform indicadorVelocidade = CriarElementoHUD("IndicadorVelocidade",
            faixa, new Vector2(-171f, 0f), new Vector2(80f, 49f));
        Image fundoVelocidade = indicadorVelocidade.gameObject.AddComponent<Image>();
        fundoVelocidade.color = new Color(0.08f, 0.1f, 0.045f, 0.98f);
        fundoVelocidade.raycastTarget = false;
        Outline bordaVelocidade = indicadorVelocidade.gameObject.AddComponent<Outline>();
        bordaVelocidade.effectColor = new Color(0.83f, 0.68f, 0.31f, 0.9f);
        bordaVelocidade.effectDistance = new Vector2(1.5f, -1.5f);

        imagemVelocidadeAtual = CriarImagemHUD("ArteVelocidadeAtual", indicadorVelocidade,
            texturaVelocidade1x, Vector2.zero, new Vector2(80f, 49f));
        textoVelocidade = CriarTexto("VelocidadeAtual", indicadorVelocidade, "1x", 25,
            FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.72f, 1f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textoVelocidade.raycastTarget = false;
        textoVelocidade.enabled = false;

        textoBotaoPausa = CriarBotaoCompactoTempo(faixa, "PausarTempo", "PAUSAR 6",
            new Vector2(-1f, 0f), new Vector2(130f, 52f), AlternarPausaTempo, true,
            texturaPausarTempo);
        imagemBotaoPausa = textoBotaoPausa.transform.parent.GetComponent<RawImage>();
        CriarBotaoCompactoTempo(faixa, "ReduzirTempo", "- 7",
            new Vector2(-92f, 0f), new Vector2(52f, 52f), ReduzirVelocidadeTempo, false,
            texturaDiminuirTempo);
        CriarBotaoCompactoTempo(faixa, "AumentarTempo", "+ 8",
            new Vector2(94f, 0f), new Vector2(52f, 52f), AumentarVelocidadeTempo, false,
            texturaAumentarTempo);
        CriarBotaoCompactoTempo(faixa, "VelocidadeNormal", "1x",
            new Vector2(163f, 0f), new Vector2(81f, 52f), RestaurarVelocidadeNormal, false,
            texturaVelocidade1x);

        CriarDicaTeclaTempo(faixa.Find("PausarTempo"), "6");
        CriarDicaTeclaTempo(faixa.Find("ReduzirTempo"), "7");
        CriarDicaTeclaTempo(faixa.Find("AumentarTempo"), "8");
        AtualizarHUDTempo();
    }

    private Text CriarBotaoCompactoTempo(Transform parent, string nome, string titulo,
        Vector2 posicao, Vector2 tamanho, UnityEngine.Events.UnityAction acao, bool destaque,
        Texture2D textura)
    {
        GameObject botaoObject = new GameObject(nome);
        botaoObject.transform.SetParent(parent, false);

        RectTransform rect = botaoObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = textura != null ? AjustarTamanhoPelaTextura(textura, tamanho) : tamanho;

        Graphic fundo;
        if (textura != null)
        {
            RawImage arte = botaoObject.AddComponent<RawImage>();
            arte.texture = textura;
            arte.color = Color.white;
            arte.raycastTarget = true;
            fundo = arte;
        }
        else
        {
            Image imagem = botaoObject.AddComponent<Image>();
            imagem.color = destaque ? corBotaoDestaque : corBotao;
            fundo = imagem;
        }

        Button botao = botaoObject.AddComponent<Button>();
        botao.targetGraphic = fundo;
        ColorBlock cores = botao.colors;
        Color corNormal = textura != null ? Color.white : fundo.color;
        cores.normalColor = corNormal;
        cores.highlightedColor = textura != null ? new Color(1f, 0.9f, 0.6f, 1f) : corBotaoHover;
        cores.pressedColor = textura != null
            ? new Color(0.78f, 0.78f, 0.78f, 1f)
            : new Color(corNormal.r * 0.84f, corNormal.g * 0.84f, corNormal.b * 0.84f, corNormal.a);
        cores.selectedColor = cores.highlightedColor;
        cores.fadeDuration = 0.08f;
        botao.colors = cores;
        botao.onClick.AddListener(acao);

        Text label = CriarTexto("Label", botaoObject.transform, titulo, 17, FontStyle.Bold,
            TextAnchor.MiddleCenter, corTexto, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6f, -2f));
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = 17;
        label.raycastTarget = false;
        label.enabled = textura == null;
        return label;
    }

    private RectTransform CriarElementoHUD(string nome, Transform parent, Vector2 posicao, Vector2 tamanho)
    {
        GameObject elemento = new GameObject(nome);
        elemento.transform.SetParent(parent, false);

        RectTransform rect = elemento.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
        return rect;
    }

    private RawImage CriarImagemHUD(string nome, Transform parent, Texture2D textura,
        Vector2 posicao, Vector2 tamanhoMaximo)
    {
        if (textura == null) return null;

        RectTransform rect = CriarElementoHUD(nome, parent, posicao,
            AjustarTamanhoPelaTextura(textura, tamanhoMaximo));
        RawImage imagem = rect.gameObject.AddComponent<RawImage>();
        imagem.texture = textura;
        imagem.color = Color.white;
        imagem.raycastTarget = false;
        return imagem;
    }

    private Vector2 AjustarTamanhoPelaTextura(Texture2D textura, Vector2 tamanhoMaximo)
    {
        if (textura == null || textura.width <= 0 || textura.height <= 0)
        {
            return tamanhoMaximo;
        }

        float escala = Mathf.Min(tamanhoMaximo.x / textura.width, tamanhoMaximo.y / textura.height);
        return new Vector2(textura.width * escala, textura.height * escala);
    }

    private void CriarDicaTeclaTempo(Transform botao, string tecla)
    {
        if (botao == null) return;

        Text dica = CriarTexto("AtalhoTeclado", botao, tecla, 12, FontStyle.Bold,
            TextAnchor.LowerRight, new Color(1f, 0.91f, 0.68f, 0.95f),
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-7f, 5f),
            new Vector2(20f, 16f));
        dica.raycastTarget = false;
    }

    private RectTransform CriarPainel(string nome, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color cor)
    {
        GameObject painel = new GameObject(nome);
        painel.transform.SetParent(parent, false);

        RectTransform rect = painel.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image imagem = painel.AddComponent<Image>();
        imagem.color = cor;
        return rect;
    }

    private Text CriarTexto(string nome, Transform parent, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textoObject = new GameObject(nome);
        textoObject.transform.SetParent(parent, false);

        RectTransform rect = textoObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text texto = textoObject.AddComponent<Text>();
        texto.text = conteudo;
        texto.font = fontePadrao;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;

        Shadow sombra = textoObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.3f);
        sombra.effectDistance = new Vector2(1f, -1f);
        return texto;
    }

    private void AbrirSubmenuConfiguracoes()
    {
        if (painelPrincipalBotoes != null) painelPrincipalBotoes.SetActive(false);
        if (painelSubEstatisticas != null) painelSubEstatisticas.SetActive(false);
        if (painelSubConfiguracoes != null) painelSubConfiguracoes.SetActive(true);
        OcultarStatus();
    }

    private void AbrirDiplomacia()
    {
        MenuGoverno menuGoverno = MenuGoverno.Instancia != null
            ? MenuGoverno.Instancia
            : Object.FindFirstObjectByType<MenuGoverno>(FindObjectsInactive.Include);
        if (menuGoverno != null)
        {
            RestaurarFluxoNormal();
            FecharMenuVisual();
            menuGoverno.AlternarMenu(true);
            return;
        }

        AtualizarStatus("Painel de Governo/Diplomacia indisponível nesta cena.", true);
    }

    private void AbrirEstatisticas()
    {
        if (painelPrincipalBotoes != null) painelPrincipalBotoes.SetActive(false);
        if (painelSubConfiguracoes != null) painelSubConfiguracoes.SetActive(false);
        if (painelSubEstatisticas != null)
        {
            AtualizarTextoEstatisticas();
            painelSubEstatisticas.SetActive(true);
        }
        OcultarStatus();
    }

    private void VoltarParaMenuPrincipalPausa()
    {
        if (painelSubConfiguracoes != null) painelSubConfiguracoes.SetActive(false);
        if (painelSubEstatisticas != null) painelSubEstatisticas.SetActive(false);
        if (painelPrincipalBotoes != null) painelPrincipalBotoes.SetActive(true);
    }

    private void ConstruirSubmenuConfiguracoes(RectTransform parent, IdiomaJogo idioma)
    {
        painelSubConfiguracoes = new GameObject("SubmenuConfiguracoes");
        painelSubConfiguracoes.transform.SetParent(parent, false);
        RectTransform rect = painelSubConfiguracoes.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CriarTexto("TituloConfig", rect, ObterTextoPausa(idioma, "settings"), 22, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.45f, 0.92f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -12f), new Vector2(0f, 34f));

        float posY = 66f;
        CriarBotao(rect, string.Format(LocalizationManager.T("pause.settings_language", "Idioma: {0}"), LocalizationManager.Instancia.NomeIdiomaAtual()), "LG", corBotao, AlternarIdioma, ref posY);
        CriarBotao(rect, string.Format(LocalizationManager.T("pause.settings_difficulty", "Dificuldade: {0}"), GameDifficultyManager.Instancia.NomeDificuldadeAtual()), "DF", corBotao, AlternarDificuldade, ref posY);
        CriarBotao(rect, ObterRotuloVSync(idioma), "VS", corBotao, AlternarVSync, ref posY);
        CriarBotao(rect, ObterTextoPausa(idioma, "audio"), "AU", corBotao, AbrirConfiguracoesAudio, ref posY);
        posY += 20f;
        CriarBotao(rect, ObterTextoPausa(idioma, "back"), "<<", corBotaoDestaque, VoltarParaMenuPrincipalPausa, ref posY);

        painelSubConfiguracoes.SetActive(false);
    }

    private void ConstruirSubmenuEstatisticas(RectTransform parent, IdiomaJogo idioma)
    {
        painelSubEstatisticas = new GameObject("SubmenuEstatisticas");
        painelSubEstatisticas.transform.SetParent(parent, false);
        RectTransform rect = painelSubEstatisticas.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CriarTexto("TituloStats", rect, ObterTextoPausa(idioma, "stats"), 22, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.45f, 0.92f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -12f), new Vector2(0f, 34f));

        RectTransform caixaStats = CriarPainel("CaixaStats", rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(520f, 340f), new Color(0.04f, 0.09f, 0.14f, 0.94f));
        Outline outlineStats = caixaStats.gameObject.AddComponent<Outline>();
        outlineStats.effectColor = new Color(0.32f, 0.78f, 0.98f, 0.45f);
        outlineStats.effectDistance = new Vector2(1f, -1f);

        textoEstatisticas = CriarTexto("TextoStats", caixaStats, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft, corTexto, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36f, -36f));

        float posY = 430f;
        CriarBotao(rect, ObterTextoPausa(idioma, "back"), "<<", corBotaoDestaque, VoltarParaMenuPrincipalPausa, ref posY);
        painelSubEstatisticas.SetActive(false);
    }

    private void AtualizarTextoEstatisticas()
    {
        if (textoEstatisticas == null) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string tesouro = recursos != null ? $"$ {recursos.dinheiro:N0}" : "N/D";
        int efetivo = governo != null ? governo.ObterEfetivoDisponivel(1) : 0;

        int unidades = 0;
        int estruturas = 0;
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < todas.Length; i++)
        {
            IdentidadeUnidade id = todas[i];
            if (id == null || id.teamID != 1) continue;
            if (id.tipoUnidade == TipoUnidade.Estrutura) estruturas++;
            else unidades++;
        }

        textoEstatisticas.text =
            $"NAÇÃO: HEGEMONIA\n\n" +
            $"• Tesouro Nacional: {tesouro}\n" +
            $"• Efetivo Militar Disponível: {efetivo:N0}\n" +
            $"• Unidades Ativas (Time 1): {unidades}\n" +
            $"• Estruturas Ativas (Time 1): {estruturas}\n" +
            $"• Dificuldade: {GameDifficultyManager.Instancia.NomeDificuldadeAtual()}\n" +
            $"• Tempo de Sessão: {Mathf.FloorToInt(Time.unscaledTime)} s";
    }

    private static string ObterTextoPausa(IdiomaJogo idioma, string chave)
    {
        if (idioma == IdiomaJogo.EnUs)
        {
            switch (chave)
            {
                case "resume": return "RESUME GAME";
                case "settings": return "SETTINGS";
                case "diplomacy": return "DIPLOMACY";
                case "stats": return "STATISTICS";
                case "save": return "SAVE GAME";
                case "load": return "LOAD GAME";
                case "restart": return "RESTART MATCH";
                case "exit": return "EXIT TO MAIN MENU";
                case "audio": return "AUDIO";
                default: return "BACK";
            }
        }
        if (idioma == IdiomaJogo.ZhHans)
        {
            switch (chave)
            {
                case "resume": return "继续游戏";
                case "settings": return "设置";
                case "diplomacy": return "外交";
                case "stats": return "统计";
                case "save": return "保存游戏";
                case "load": return "载入游戏";
                case "restart": return "重新开始";
                case "exit": return "返回主菜单";
                case "audio": return "音频";
                default: return "返回";
            }
        }
        switch (chave)
        {
            case "resume": return "RETOMAR JOGO";
            case "settings": return "CONFIGURAÇÕES";
            case "diplomacy": return "DIPLOMACIA";
            case "stats": return "ESTATÍSTICAS";
            case "save": return "SALVAR JOGO";
            case "load": return "CARREGAR JOGO";
            case "restart": return "REINICIAR PARTIDA";
            case "exit": return "SAIR PARA O MENU PRINCIPAL";
            case "audio": return "ÁUDIO";
            default: return "VOLTAR";
        }
    }

    private void CriarBotaoPausaSprite(
        Transform parent,
        string nomeObjeto,
        string caminhoSprite,
        float altura,
        UnityEngine.Events.UnityAction acao,
        ref float posicaoY,
        string textoBotao,
        bool desenharTextoSobreSprite)
    {
        Sprite sprite = Resources.Load<Sprite>(caminhoSprite);
        if (sprite == null)
        {
            CriarBotao(parent, textoBotao, ">", corBotao, acao, ref posicaoY);
            return;
        }

        GameObject botaoObject = new GameObject(nomeObjeto);
        botaoObject.transform.SetParent(parent, false);

        RectTransform rect = botaoObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -posicaoY);
        rect.sizeDelta = new Vector2(578f, altura);

        Image imagem = botaoObject.AddComponent<Image>();
        imagem.sprite = sprite;
        imagem.preserveAspect = true;
        imagem.color = Color.white;
        imagem.raycastTarget = true;

        Button botao = botaoObject.AddComponent<Button>();
        ColorBlock cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(0.88f, 0.98f, 1f, 1f);
        cores.pressedColor = new Color(0.76f, 0.88f, 0.94f, 1f);
        cores.selectedColor = new Color(0.92f, 1f, 1f, 1f);
        cores.disabledColor = new Color(0.55f, 0.6f, 0.65f, 0.65f);
        cores.fadeDuration = 0.08f;
        botao.colors = cores;
        botao.onClick.AddListener(acao);

        if (desenharTextoSobreSprite)
        {
            Text label = CriarTexto(
                "Label",
                botaoObject.transform,
                textoBotao,
                21,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Color.white,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(64f, 0f),
                new Vector2(-210f, 0f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 21;
        }

        posicaoY += altura + 4f;
    }

    private void CriarBotao(Transform parent, string titulo, string icone, Color corBase, UnityEngine.Events.UnityAction acao, ref float posicaoY)
    {
        GameObject botaoObject = new GameObject(titulo.Replace(" ", string.Empty) + "Button");
        botaoObject.transform.SetParent(parent, false);

        RectTransform rect = botaoObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -posicaoY);
        rect.sizeDelta = new Vector2(420f, 62f);

        Image fundo = botaoObject.AddComponent<Image>();
        fundo.color = corBase;

        Outline borda = botaoObject.AddComponent<Outline>();
        borda.effectColor = new Color(corBorda.r, corBorda.g, corBorda.b, 0.26f);
        borda.effectDistance = new Vector2(2f, -2f);

        Button botao = botaoObject.AddComponent<Button>();
        ColorBlock cores = botao.colors;
        cores.normalColor = corBase;
        cores.highlightedColor = corBotaoHover;
        cores.pressedColor = new Color(corBase.r * 0.84f, corBase.g * 0.84f, corBase.b * 0.84f, corBase.a);
        cores.selectedColor = corBotaoHover;
        cores.disabledColor = corBase;
        cores.fadeDuration = 0.08f;
        botao.colors = cores;
        botao.onClick.AddListener(acao);

        RectTransform barra = CriarPainel("Accent", botaoObject.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(5f, 0f), new Color(0.6f, 0.88f, 0.97f, 0.95f));
        barra.SetAsFirstSibling();

        Text label = CriarTexto("Label", botaoObject.transform, titulo.ToUpper(), 19, FontStyle.Bold, TextAnchor.MiddleLeft, corTexto, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(26f, 0f), new Vector2(-112f, 0f));
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = 19;

        RectTransform iconBadge = CriarPainel("IconBadge", botaoObject.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(56f, 34f), new Color(1f, 1f, 1f, 0.08f));
        Text iconText = CriarTexto("Icon", iconBadge, icone, 16, FontStyle.Bold, TextAnchor.MiddleCenter, corTexto, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        iconText.resizeTextForBestFit = true;
        iconText.resizeTextMinSize = 12;
        iconText.resizeTextMaxSize = 16;

        posicaoY += 74f;
    }

    private void AtualizarStatus(string mensagem, bool alerta)
    {
        if (statusText == null)
        {
            return;
        }

        if (statusBoxObject != null) statusBoxObject.SetActive(true);
        statusText.text = mensagem;
        statusText.color = alerta ? corTextoAlerta : corTextoSuave;
    }

    private void OcultarStatus()
    {
        if (statusText != null) statusText.text = string.Empty;
        if (statusBoxObject != null) statusBoxObject.SetActive(false);
    }
}
