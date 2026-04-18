using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(12000)]
public class MenuPausaController : MonoBehaviour
{
    private const string CenaMenuPrincipal = "Menu cena";
    private const string CenaMenuFallback = "MenuPrincipal";

    public static bool EstaPausado { get; private set; }

    private readonly Color corOverlay = new Color(0f, 0f, 0f, 0.45f);
    private readonly Color corPainel = new Color(0.06f, 0.12f, 0.16f, 0.92f);
    private readonly Color corPainelTopo = new Color(0.09f, 0.18f, 0.23f, 0.94f);
    private readonly Color corBorda = new Color(0.47f, 0.9f, 1f, 0.34f);
    private readonly Color corBotao = new Color(0.11f, 0.18f, 0.23f, 0.92f);
    private readonly Color corBotaoDestaque = new Color(0.19f, 0.43f, 0.55f, 0.96f);
    private readonly Color corBotaoHover = new Color(0.16f, 0.28f, 0.34f, 0.96f);
    private readonly Color corBotaoSair = new Color(0.36f, 0.17f, 0.17f, 0.94f);
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
        if (cenaAtiva.name == CenaMenuPrincipal || cenaAtiva.name == CenaMenuFallback)
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
        if (cenaAtiva.name == CenaMenuPrincipal || cenaAtiva.name == CenaMenuFallback)
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
        AtualizarStatus("Partida pausada.", false);
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

    private void AbrirConfiguracoes()
    {
        AtualizarStatus("Configuracoes entram na proxima etapa.", true);
    }

    private void SalvarJogo()
    {
        sistemaSave.RegistrarCenaAtual(SceneManager.GetActiveScene().name);
        sistemaSave.SalvarJogo();
        AtualizarStatus("Jogo salvo com sucesso.", false);
    }

    private void CarregarJogo()
    {
        if (!sistemaSave.TentarCarregarJogo())
        {
            AtualizarStatus("Nenhum save encontrado para carregar.", true);
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
        SceneManager.LoadScene(CenaMenuPrincipal);
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

        RectTransform painel = CriarPainel("PainelCentral", raizMenu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 720f), corPainel);
        CriarPainel("FaixaTopo", painel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 130f), corPainelTopo);
        CriarPainel("Brilho", painel, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

        Outline outline = painel.gameObject.AddComponent<Outline>();
        outline.effectColor = corBorda;
        outline.effectDistance = new Vector2(2f, -2f);

        CriarTexto("Cabecalho", painel, "HEGEMONIA GLOBAL", 22, FontStyle.Bold, TextAnchor.UpperCenter, corTextoSuave, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(0f, 36f));
        CriarTexto("Titulo", painel, "PAUSADO", 52, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.43f, 0.93f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, 56f));
        CriarTexto("Subtitulo", painel, "HEGEMONIA GLOBAL", 24, FontStyle.Bold, TextAnchor.UpperCenter, corTexto, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -180f), new Vector2(0f, 30f));

        RectTransform botoes = new GameObject("BotoesMenuPausa").AddComponent<RectTransform>();
        botoes.SetParent(painel, false);
        botoes.anchorMin = new Vector2(0.5f, 1f);
        botoes.anchorMax = new Vector2(0.5f, 1f);
        botoes.pivot = new Vector2(0.5f, 1f);
        botoes.anchoredPosition = new Vector2(0f, -278f);
        botoes.sizeDelta = new Vector2(340f, 360f);

        float posicaoY = 0f;
        CriarBotao(botoes, "Retomar Jogo", "GO", corBotaoDestaque, RetomarJogo, ref posicaoY);
        CriarBotao(botoes, "Configuracoes", "CF", corBotao, AbrirConfiguracoes, ref posicaoY);
        CriarBotao(botoes, "Carregar Jogo", "LD", corBotao, CarregarJogo, ref posicaoY);
        CriarBotao(botoes, "Salvar Jogo", "SV", corBotao, SalvarJogo, ref posicaoY);
        CriarBotao(botoes, "Reiniciar Partida", "RE", corBotao, ReiniciarPartida, ref posicaoY);
        CriarBotao(botoes, "Sair para Menu Principal", "EX", corBotaoSair, SairParaMenuPrincipal, ref posicaoY);

        statusText = CriarTexto("Status", painel, string.Empty, 16, FontStyle.Bold, TextAnchor.LowerCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 36f), new Vector2(-40f, 30f));
        CriarTexto("Rodape", painel, "ESC retoma a partida.", 14, FontStyle.Normal, TextAnchor.LowerCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(-40f, 20f));
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
        rect.sizeDelta = new Vector2(340f, 58f);

        Image fundo = botaoObject.AddComponent<Image>();
        fundo.color = corBase;

        Outline borda = botaoObject.AddComponent<Outline>();
        borda.effectColor = new Color(corBorda.r, corBorda.g, corBorda.b, 0.22f);
        borda.effectDistance = new Vector2(1f, -1f);

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

        CriarTexto("Label", botaoObject.transform, titulo.ToUpper(), 20, FontStyle.Bold, TextAnchor.MiddleCenter, corTexto, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-18f, 0f), new Vector2(-82f, 0f));
        CriarTexto("Icon", botaoObject.transform, icone, 20, FontStyle.Bold, TextAnchor.MiddleCenter, corTexto, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-28f, 0f), new Vector2(36f, 0f));

        posicaoY += 70f;
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
