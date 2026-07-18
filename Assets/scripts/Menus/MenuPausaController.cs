using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(12000)]
public class MenuPausaController : MonoBehaviour
{
    public static bool EstaPausado { get; private set; }

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
    private Text statusText;

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

    private void Awake()
    {
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
        if (FabricaMineriosMenuController.EstaAberto)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !EstaDigitandoEmCampoTexto())
        {
            AlternarMenu();
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

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EstaPausado = true;
        raizMenu.SetActive(true);
        AtualizarStatus(LocalizationManager.T("pause.status", "Partida pausada."), false);
    }

    private void FecharMenuVisual()
    {
        if (raizMenu != null)
        {
            raizMenu.SetActive(false);
        }
    }

    private void RestaurarFluxoNormal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        EstaPausado = false;
        FecharMenuVisual();
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
        AtualizarStatus(string.Format(LocalizationManager.T("pause.settings_language", "Idioma: {0}"), LocalizationManager.Instancia.NomeIdiomaAtual()), false);
    }

    private void AlternarDificuldade()
    {
        GameDifficultyManager.Instancia.ProximaDificuldade();
        RecriarInterface();
        raizMenu.SetActive(true);
        AtualizarStatus(
            string.Format(
                LocalizationManager.T("pause.settings_difficulty", "Dificuldade: {0}"),
                GameDifficultyManager.Instancia.NomeDificuldadeAtual()),
            false);
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
        SceneManager.LoadScene(cenaDestino);
    }

    private void ReiniciarPartida()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        sistemaSave.IniciarNovoJogo(cenaAtual);
        RestaurarFluxoNormal();
        FluxoInicialJogo.AutorizarCarga(cenaAtual);
        SceneManager.LoadScene(cenaAtual);
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
        botoes.sizeDelta = new Vector2(420f, 500f);

        float posicaoY = 0f;
        CriarBotao(botoes, LocalizationManager.T("pause.resume", "Retomar Jogo"), "GO", corBotaoDestaque, RetomarJogo, ref posicaoY);
        CriarBotao(botoes, string.Format(LocalizationManager.T("pause.settings_language", "Idioma: {0}"), LocalizationManager.Instancia.NomeIdiomaAtual()), "LG", corBotao, AlternarIdioma, ref posicaoY);
        CriarBotao(botoes, string.Format(LocalizationManager.T("pause.settings_difficulty", "Dificuldade: {0}"), GameDifficultyManager.Instancia.NomeDificuldadeAtual()), "DF", corBotao, AlternarDificuldade, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.load", "Carregar Jogo"), "LD", corBotao, CarregarJogo, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.save", "Salvar Jogo"), "SV", corBotao, SalvarJogo, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.restart", "Reiniciar Partida"), "RE", corBotao, ReiniciarPartida, ref posicaoY);
        CriarBotao(botoes, LocalizationManager.T("pause.exit_menu", "Sair para Menu Principal"), "EX", corBotaoSair, SairParaMenuPrincipal, ref posicaoY);

        RectTransform statusBox = CriarPainel("StatusBox", painel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(430f, 54f), new Color(0.07f, 0.12f, 0.15f, 0.96f));
        statusText = CriarTexto("Status", statusBox, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-32f, 0f));
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
        }

        ConstruirInterface();
        if (!estavaAberto)
        {
            FecharMenuVisual();
        }
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

        statusText.text = mensagem;
        statusText.color = alerta ? corTextoAlerta : corTextoSuave;
    }
}
