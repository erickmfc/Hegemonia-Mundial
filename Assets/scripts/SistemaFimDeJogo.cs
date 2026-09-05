using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hegemonia.RTS;

[DefaultExecutionOrder(15000)]
public class SistemaFimDeJogo : MonoBehaviour
{
    public static bool PartidaEncerrada { get; private set; }
    public static SistemaFimDeJogo Instancia { get; private set; }

    private readonly Color corOverlayPadrao = new Color(0.05f, 0.05f, 0.08f, 0.88f);
    private readonly Color corPainelBase = new Color(0.11f, 0.13f, 0.16f, 0.98f);
    private readonly Color corPainelTopoBase = new Color(0.07f, 0.09f, 0.11f, 0.98f);
    private readonly Color corTextoPrincipal = new Color(0.96f, 0.98f, 1f, 1f);
    private readonly Color corTextoSuave = new Color(0.82f, 0.88f, 0.94f, 1f);

    private Font fontePadrao;
    private Canvas canvas;
    private CanvasGroup grupoRaiz;
    private GameObject raizInterface;
    private RectTransform painelPrincipal;
    private Image fundoOverlay;
    private Image faixaTopo;
    private Text textoCabecalho;
    private Text textoResultado;
    private Text textoObjetivo;
    private Text textoDetalhe;
    private Text textoRodape;
    private Button botaoReiniciar;
    private Button botaoMenuPrincipal;
    private Button botaoContinuar;
    private Coroutine animacaoEntrada;
    private bool interfaceConstruida;
    private readonly List<MonoBehaviour> comportamentosDesativados = new List<MonoBehaviour>();
    private Color corAcentoAtual = new Color(0.22f, 0.82f, 0.62f, 1f);
    private Color corAcentoSecundaria = new Color(0.12f, 0.36f, 0.3f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetarEstadoGlobal()
    {
        PartidaEncerrada = false;
        Instancia = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirBootstrap()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        if (Object.FindFirstObjectByType<SistemaFimDeJogo>() != null)
        {
            return;
        }

        new GameObject("SistemaFimDeJogo").AddComponent<SistemaFimDeJogo>();
    }

    private void Awake()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        fontePadrao = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GarantirEventSystem();
        ConstruirInterface();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }

        RestaurarFluxoNormal();
        PartidaEncerrada = false;
    }

    private void LateUpdate()
    {
        if (!PartidaEncerrada)
        {
            return;
        }

        if (!Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 0f;
        }

        if (!AudioListener.pause)
        {
            AudioListener.pause = true;
        }

        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }
    }

    public static void RegistrarResultado(TipoObjetivoFinal tipoObjetivo, bool alvoPertenceAoJogador, string nomeDaNacao, string nomeObjetivo)
    {
        // Em mapas com varias nacoes, a destruicao de uma unica prefeitura
        // apenas elimina aquela sede. O RTSObjectiveService reavalia as
        // capitais e decide a partida quando nao restar uma capital inimiga
        // ou quando a capital do jogador tiver sido destruida.
        if (tipoObjetivo == TipoObjetivoFinal.Prefeitura
            && RTSObjectiveService.Instancia != null
            && RTSObjectiveService.Instancia.DeveAdiarResultadoDePrefeitura())
        {
            Debug.Log($"[SistemaFimDeJogo] Prefeitura destruida; partida continua porque ha outras nacoes ativas: {nomeDaNacao}.");
            return;
        }

        RTSGameSession.Instancia?.ReportMatchResult(
            alvoPertenceAoJogador ? RTSMatchResult.Defeat : RTSMatchResult.Victory,
            string.IsNullOrWhiteSpace(nomeObjetivo) ? tipoObjetivo.ToString() : nomeObjetivo);

        SistemaFimDeJogo sistema = ObterOuCriarInstancia();
        if (sistema == null)
        {
            Debug.LogError("[SistemaFimDeJogo] Nao foi possivel criar a interface final.");
            return;
        }

        sistema.MostrarResultado(tipoObjetivo, alvoPertenceAoJogador, nomeDaNacao, nomeObjetivo);
    }

    private static SistemaFimDeJogo ObterOuCriarInstancia()
    {
        if (Instancia != null)
        {
            return Instancia;
        }

        Instancia = Object.FindFirstObjectByType<SistemaFimDeJogo>();
        if (Instancia != null)
        {
            return Instancia;
        }

        GameObject novo = new GameObject("SistemaFimDeJogo");
        return novo.AddComponent<SistemaFimDeJogo>();
    }

    private void MostrarResultado(TipoObjetivoFinal tipoObjetivo, bool alvoPertenceAoJogador, string nomeDaNacao, string nomeObjetivo)
    {
        if (PartidaEncerrada)
        {
            return;
        }

        PartidaEncerrada = true;

        if (animacaoEntrada != null)
        {
            StopCoroutine(animacaoEntrada);
            animacaoEntrada = null;
        }

        ConstruirInterface();
        PrepararVisual(tipoObjetivo, alvoPertenceAoJogador, nomeDaNacao, nomeObjetivo);
        AplicarCoresResultado(alvoPertenceAoJogador);
        TornarInterfaceVisivel();
        comportamentosDesativados.Clear();
        CongelarGameplay();

        if (animacaoEntrada != null)
        {
            StopCoroutine(animacaoEntrada);
        }

        animacaoEntrada = StartCoroutine(AnimarEntrada());
    }

    private void PrepararVisual(TipoObjetivoFinal tipoObjetivo, bool alvoPertenceAoJogador, string nomeDaNacao, string nomeObjetivo)
    {
        bool vitoria = !alvoPertenceAoJogador;
        string nomeObjetivoLimpo = string.IsNullOrWhiteSpace(nomeObjetivo) ? tipoObjetivo.ToString() : nomeObjetivo.Trim();
        string nomeNacaoLimpo = string.IsNullOrWhiteSpace(nomeDaNacao) ? "a nação alvo" : nomeDaNacao.Trim();

        corAcentoAtual = vitoria
            ? new Color(0.22f, 0.84f, 0.58f, 1f)
            : new Color(0.96f, 0.34f, 0.3f, 1f);

        corAcentoSecundaria = vitoria
            ? new Color(0.12f, 0.34f, 0.27f, 1f)
            : new Color(0.38f, 0.13f, 0.12f, 1f);

        if (fundoOverlay != null)
        {
            fundoOverlay.color = vitoria
                ? new Color(0.02f, 0.08f, 0.05f, 0.72f)
                : new Color(0.09f, 0.02f, 0.02f, 0.76f);
        }

        if (faixaTopo != null)
        {
            faixaTopo.color = corAcentoAtual;
        }

        if (textoCabecalho != null)
        {
            textoCabecalho.text = vitoria ? "VITÓRIA" : "DERROTA";
            textoCabecalho.color = corAcentoAtual;
        }

        if (textoResultado != null)
        {
            textoResultado.text = vitoria ? "OBJETIVO CONCLUÍDO" : "OBJETIVO PERDIDO";
            textoResultado.color = corTextoPrincipal;
        }

        if (textoObjetivo != null)
        {
            textoObjetivo.text = $"{nomeObjetivoLimpo.ToUpperInvariant()} DE {nomeNacaoLimpo.ToUpperInvariant()}";
            textoObjetivo.color = corTextoSuave;
        }

        if (textoDetalhe != null)
        {
            textoDetalhe.text = vitoria
                ? $"Você venceu ao destruir a {nomeObjetivoLimpo.ToLowerInvariant()} de {nomeNacaoLimpo}."
                : $"Sua {nomeObjetivoLimpo.ToLowerInvariant()} foi destruída.";
            textoDetalhe.color = corTextoPrincipal;
        }

        if (textoRodape != null)
        {
            textoRodape.text = vitoria
                ? "A partida terminou com vitória."
                : "A partida terminou em derrota.";
            textoRodape.color = corTextoSuave;
        }

        if (botaoReiniciar != null)
        {
            botaoReiniciar.GetComponentInChildren<Text>(true).text = vitoria ? "REINICIAR PARTIDA" : "TENTAR NOVAMENTE";
        }

        if (botaoMenuPrincipal != null)
        {
            botaoMenuPrincipal.GetComponentInChildren<Text>(true).text = "MENU PRINCIPAL";
        }

        if (botaoContinuar != null)
        {
            botaoContinuar.gameObject.SetActive(vitoria);
            botaoContinuar.GetComponentInChildren<Text>(true).text = "CONTINUAR JOGO";
        }
    }

    private void CongelarGameplay()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }

        MonoBehaviour[] comportamentos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < comportamentos.Length; i++)
        {
            MonoBehaviour comportamento = comportamentos[i];
            if (comportamento == null || comportamento == this)
            {
                continue;
            }

            if (EhParteDaInterfaceFinal(comportamento) || comportamento is EventSystem || comportamento is BaseInputModule)
            {
                continue;
            }

            if (comportamento.enabled)
            {
                comportamentosDesativados.Add(comportamento);
                comportamento.enabled = false;
            }
        }
    }

    private bool EhParteDaInterfaceFinal(MonoBehaviour comportamento)
    {
        if (comportamento == null || canvas == null)
        {
            return false;
        }

        if (comportamento.gameObject == canvas.gameObject)
        {
            return true;
        }

        return comportamento.transform.IsChildOf(canvas.transform);
    }

    private void TornarInterfaceVisivel()
    {
        if (grupoRaiz == null)
        {
            return;
        }

        grupoRaiz.alpha = 0f;
        grupoRaiz.interactable = false;
        grupoRaiz.blocksRaycasts = false;

        if (painelPrincipal != null)
        {
            painelPrincipal.localScale = Vector3.one * 0.84f;
            painelPrincipal.anchoredPosition = new Vector2(0f, -92f);
        }
    }

    private IEnumerator AnimarEntrada()
    {
        if (grupoRaiz == null || painelPrincipal == null)
        {
            yield break;
        }

        float duracao = 0.7f;
        float tempo = 0f;
        Vector3 escalaInicial = Vector3.one * 0.84f;
        Vector3 escalaFinal = Vector3.one;
        Vector2 posicaoInicial = new Vector2(0f, -92f);
        Vector2 posicaoFinal = Vector2.zero;
        float alphaOverlayFinal = fundoOverlay != null ? fundoOverlay.color.a : corOverlayPadrao.a;

        grupoRaiz.interactable = false;
        grupoRaiz.blocksRaycasts = false;
        painelPrincipal.localScale = escalaInicial;
        painelPrincipal.anchoredPosition = posicaoInicial;

        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(tempo / duracao);
            float suavizado = Mathf.SmoothStep(0f, 1f, t);

            if (grupoRaiz != null)
            {
                grupoRaiz.alpha = Mathf.Lerp(0f, 1f, suavizado);
            }

            if (painelPrincipal != null)
            {
                painelPrincipal.localScale = Vector3.Lerp(escalaInicial, escalaFinal, suavizado);
                painelPrincipal.anchoredPosition = Vector2.Lerp(posicaoInicial, posicaoFinal, suavizado);
            }

            if (fundoOverlay != null)
            {
                Color corOverlay = fundoOverlay.color;
                corOverlay.a = Mathf.Lerp(0f, alphaOverlayFinal, suavizado);
                fundoOverlay.color = corOverlay;
            }

            yield return null;
        }

        if (grupoRaiz != null)
        {
            grupoRaiz.alpha = 1f;
            grupoRaiz.interactable = true;
            grupoRaiz.blocksRaycasts = true;
        }

        if (painelPrincipal != null)
        {
            painelPrincipal.localScale = escalaFinal;
            painelPrincipal.anchoredPosition = posicaoFinal;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && botaoReiniciar != null)
        {
            eventSystem.SetSelectedGameObject(botaoReiniciar.gameObject);
        }
    }

    private void AplicarCoresResultado(bool alvoPertenceAoJogador)
    {
        if (painelPrincipal == null)
        {
            return;
        }

        Image imagemPainel = painelPrincipal.GetComponent<Image>();
        if (imagemPainel != null)
        {
            imagemPainel.color = alvoPertenceAoJogador
                ? new Color(0.12f, 0.06f, 0.06f, 0.98f)
                : new Color(0.05f, 0.1f, 0.08f, 0.98f);
        }

        Outline outline = painelPrincipal.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(corAcentoSecundaria.r, corAcentoSecundaria.g, corAcentoSecundaria.b, 0.34f);
        }

        Shadow shadow = painelPrincipal.GetComponent<Shadow>();
        if (shadow != null)
        {
            shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
        }
    }

    private void ConstruirInterface()
    {
        if (interfaceConstruida)
        {
            return;
        }

        interfaceConstruida = true;

        GameObject canvasObj = new GameObject("Canvas_FimDeJogo");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        raizInterface = new GameObject("RaizFimDeJogo");
        raizInterface.transform.SetParent(canvas.transform, false);
        grupoRaiz = raizInterface.AddComponent<CanvasGroup>();
        grupoRaiz.alpha = 0f;
        grupoRaiz.interactable = false;
        grupoRaiz.blocksRaycasts = false;

        RectTransform raizRect = raizInterface.AddComponent<RectTransform>();
        raizRect.anchorMin = Vector2.zero;
        raizRect.anchorMax = Vector2.one;
        raizRect.offsetMin = Vector2.zero;
        raizRect.offsetMax = Vector2.zero;

        fundoOverlay = CriarPainel("Overlay", raizInterface.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, corOverlayPadrao);
        fundoOverlay.rectTransform.offsetMin = Vector2.zero;
        fundoOverlay.rectTransform.offsetMax = Vector2.zero;

        painelPrincipal = CriarPainel("PainelPrincipal", raizInterface.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 520f), corPainelBase).rectTransform;
        painelPrincipal.anchoredPosition = new Vector2(0f, 0f);
        painelPrincipal.localScale = Vector3.one * 0.9f;

        Image imagemPainel = painelPrincipal.GetComponent<Image>();
        if (imagemPainel != null)
        {
            imagemPainel.color = corPainelBase;
        }

        Outline outline = painelPrincipal.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(corAcentoAtual.r, corAcentoAtual.g, corAcentoAtual.b, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = painelPrincipal.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -8f);

        faixaTopo = CriarPainel("FaixaTopo", painelPrincipal.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(0f, 10f), corPainelTopoBase);

        textoCabecalho = CriarTexto("Cabecalho", painelPrincipal, "HEGEMONIA GLOBAL", 28, FontStyle.Bold, TextAnchor.UpperCenter, corAcentoAtual, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 40f));
        textoResultado = CriarTexto("Resultado", painelPrincipal, "FIM DA PARTIDA", 64, FontStyle.Bold, TextAnchor.UpperCenter, corTextoPrincipal, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(0f, 75f));
        textoObjetivo = CriarTexto("Objetivo", painelPrincipal, "OBJETIVO", 22, FontStyle.Bold, TextAnchor.UpperCenter, corTextoSuave, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -180f), new Vector2(0f, 30f));
        textoDetalhe = CriarTexto("Detalhe", painelPrincipal, "Aguardando o desfecho da batalha.", 20, FontStyle.Normal, TextAnchor.MiddleCenter, corTextoPrincipal, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f), new Vector2(0f, -250f), new Vector2(0f, 90f));
        textoDetalhe.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoDetalhe.verticalOverflow = VerticalWrapMode.Overflow;

        textoRodape = CriarTexto("Rodape", painelPrincipal, "A partida terminou.", 16, FontStyle.Italic, TextAnchor.LowerCenter, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 25f), new Vector2(0f, 24f));

        RectTransform botoesContainer = new GameObject("Botoes").AddComponent<RectTransform>();
        botoesContainer.SetParent(painelPrincipal, false);
        botoesContainer.anchorMin = new Vector2(0.5f, 0f);
        botoesContainer.anchorMax = new Vector2(0.5f, 0f);
        botoesContainer.pivot = new Vector2(0.5f, 0f);
        botoesContainer.anchoredPosition = new Vector2(0f, 65f);
        botoesContainer.sizeDelta = new Vector2(760f, 75f);

        HorizontalLayoutGroup layoutBotoes = botoesContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layoutBotoes.childAlignment = TextAnchor.MiddleCenter;
        layoutBotoes.childControlWidth = true;
        layoutBotoes.childControlHeight = true;
        layoutBotoes.childForceExpandWidth = false;
        layoutBotoes.childForceExpandHeight = true;
        layoutBotoes.spacing = 25f;

        botaoReiniciar = CriarBotao("Reiniciar", botoesContainer, "REINICIAR", new Color(0.12f, 0.40f, 0.65f, 0.98f), ReiniciarPartida);
        botaoContinuar = CriarBotao("Continuar", botoesContainer, "CONTINUAR", new Color(0.12f, 0.50f, 0.35f, 0.98f), ContinuarJogo);
        botaoMenuPrincipal = CriarBotao("MenuPrincipal", botoesContainer, "MENU PRINCIPAL", new Color(0.65f, 0.20f, 0.20f, 0.98f), RetornarAoMenuPrincipal);

        LayoutElement leRestart = botaoReiniciar.gameObject.GetComponent<LayoutElement>();
        if (leRestart != null)
        {
            leRestart.preferredWidth = 220f;
            leRestart.preferredHeight = 65f;
        }

        LayoutElement leMenu = botaoMenuPrincipal.gameObject.GetComponent<LayoutElement>();
        if (leMenu != null)
        {
            leMenu.preferredWidth = 220f;
            leMenu.preferredHeight = 65f;
        }
    }

    private Image CriarPainel(string nome, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color cor)
    {
        GameObject objeto = new GameObject(nome, typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(parent, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image imagem = objeto.GetComponent<Image>();
        if (imagem == null)
        {
            imagem = objeto.AddComponent<Image>();
        }
        imagem.color = cor;
        return imagem;
    }

    private Text CriarTexto(string nome, Transform parent, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(parent, false);

        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text texto = objeto.AddComponent<Text>();
        texto.font = fontePadrao;
        texto.text = conteudo;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.supportRichText = true;

        Shadow sombra = objeto.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.34f);
        sombra.effectDistance = new Vector2(1f, -1f);

        return texto;
    }

    private Button CriarBotao(string nome, Transform parent, string texto, Color cor, UnityEngine.Events.UnityAction acao)
    {
        GameObject objeto = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        objeto.transform.SetParent(parent, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 65f);

        Image imagem = objeto.GetComponent<Image>();
        imagem.color = cor;

        Outline outline = objeto.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        outline.effectDistance = new Vector2(1f, -2f);

        Button botao = objeto.GetComponent<Button>();
        ColorBlock cores = botao.colors;
        cores.normalColor = cor;
        cores.highlightedColor = new Color(cor.r + 0.15f, cor.g + 0.15f, cor.b + 0.15f, cor.a);
        cores.pressedColor = new Color(cor.r - 0.15f, cor.g - 0.15f, cor.b - 0.15f, cor.a);
        cores.selectedColor = cores.highlightedColor;
        cores.disabledColor = new Color(cor.r, cor.g, cor.b, 0.5f);
        cores.fadeDuration = 0.1f;
        botao.colors = cores;
        botao.transition = Selectable.Transition.ColorTint;
        botao.onClick.AddListener(acao);

        Text textoBotao = CriarTexto("Texto", objeto.transform, texto, 20, FontStyle.Bold, TextAnchor.MiddleCenter, corTextoPrincipal, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textoBotao.raycastTarget = false;
        RectTransform rectTexto = textoBotao.GetComponent<RectTransform>();
        rectTexto.anchorMin = Vector2.zero;
        rectTexto.anchorMax = Vector2.one;
        rectTexto.offsetMin = Vector2.zero;
        rectTexto.offsetMax = Vector2.zero;

        LayoutElement le = objeto.GetComponent<LayoutElement>();
        le.preferredWidth = 220f;
        le.preferredHeight = 65f;
        le.minWidth = 200f;
        le.minHeight = 60f;

        return botao;
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

    private void RestaurarFluxoNormal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReiniciarPartida()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        RestaurarFluxoNormal();
        FluxoInicialJogo.AutorizarCarga(cenaAtual);
        SceneManager.LoadScene(cenaAtual);
    }

    private void ContinuarJogo()
    {
        PartidaEncerrada = false;
        RestaurarFluxoNormal();

        foreach (var comp in comportamentosDesativados)
        {
            if (comp != null)
            {
                comp.enabled = true;
            }
        }
        comportamentosDesativados.Clear();

        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
        
        interfaceConstruida = false;
    }

    private void RetornarAoMenuPrincipal()
    {
        string cenaMenu = ConfiguracaoCenasJogo.ResolverCenaMenuPrincipal();

        RestaurarFluxoNormal();

        if (ConfiguracaoCenasJogo.CenaExiste(cenaMenu))
        {
            SceneManager.LoadScene(cenaMenu);
        }
        else
        {
            Debug.LogWarning($"[SistemaFimDeJogo] Cena de menu '{cenaMenu}' nao pode ser carregada.");
        }
    }
}
