using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(13000)]
public sealed class HUDAjudaRTS : MonoBehaviour
{
    public static HUDAjudaRTS Instancia { get; private set; }

    [SerializeField] private KeyCode teclaAlternar = KeyCode.F1;
    [SerializeField] private float duracaoToastPadrao = 3.5f;

    private Font fontePadrao;
    private Canvas canvas;
    private CanvasGroup grupoCanvas;
    private RectTransform painel;
    private Text textoCabecalho;
    private Text textoObjetivo;
    private Text textoSelecao;
    private Text textoAtalhos;
    private Text textoRuntime;
    private Text textoToast;
    private bool expandido = true;
    private float recolherAutomaticamenteEm = -1f;
    private float toastAte = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        if (Object.FindFirstObjectByType<HUDAjudaRTS>() != null)
        {
            return;
        }

        new GameObject("HUDAjudaRTS").AddComponent<HUDAjudaRTS>();
    }

    private void Awake()
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
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
        AtualizarVisibilidadePainel(true);
        recolherAutomaticamenteEm = Time.unscaledTime + 12f;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAlternar))
        {
            expandido = !expandido;
            if (expandido)
            {
                recolherAutomaticamenteEm = -1f;
            }
            AtualizarVisibilidadePainel(false);
        }

        if (expandido && recolherAutomaticamenteEm > 0f && Time.unscaledTime >= recolherAutomaticamenteEm)
        {
            expandido = false;
            recolherAutomaticamenteEm = -1f;
            AtualizarVisibilidadePainel(false);
        }

        AtualizarConteudo();

        float alvoAlpha = SistemaFimDeJogo.PartidaEncerrada ? 0f : 1f;
        if (grupoCanvas != null)
        {
            grupoCanvas.alpha = Mathf.MoveTowards(grupoCanvas.alpha, alvoAlpha, Time.unscaledDeltaTime * 4f);
            grupoCanvas.blocksRaycasts = false;
            grupoCanvas.interactable = false;
        }

        if (textoToast != null)
        {
            bool toastAtivo = Time.unscaledTime <= toastAte;
            textoToast.gameObject.SetActive(toastAtivo);
        }
    }

    public static void MostrarMensagemTemporaria(string mensagem, float duracao = -1f)
    {
        if (Instancia == null || string.IsNullOrWhiteSpace(mensagem))
        {
            return;
        }

        Instancia.textoToast.text = mensagem.Trim();
        Instancia.toastAte = Time.unscaledTime + (duracao > 0f ? duracao : Instancia.duracaoToastPadrao);
    }

    private void AtualizarConteudo()
    {
        if (textoCabecalho == null)
        {
            return;
        }

        bool tutorial = SceneManager.GetActiveScene().name == ConfiguracaoCenasJogo.CenaTutorialCanonica;
        textoCabecalho.text = expandido ? "COMANDO TATICO" : "AJUDA RAPIDA";
        textoObjetivo.text = tutorial
            ? "Tutorial: selecione tropas, mova com RMB, abra C para construcao e proteja sua prefeitura."
            : "Objetivo: destrua a prefeitura ou o presidente inimigo. Proteja os seus para nao perder.";
        textoSelecao.text = MontarTextoSelecao();
        textoRuntime.text = GovernadorGameplayRTS.ObterResumoHud() + MontarSufixoRuntime();

        if (textoAtalhos != null)
        {
            textoAtalhos.text =
                "LMB arrasta selecao  |  RMB envia ordem\n" +
                "C construcao  |  M mapa tatico  |  ESC pausa\n" +
                "X governo  |  V pier  |  TAB acelera se o runtime permitir\n" +
                "PASSIVO/ATIVO/PATRULHA/SEGUIR ficam visiveis ao selecionar unidades";
        }
    }

    private string MontarTextoSelecao()
    {
        GerenteSelecao gerenteSelecao = Object.FindFirstObjectByType<GerenteSelecao>();
        if (gerenteSelecao == null || gerenteSelecao.unidadesSelecionadas == null || gerenteSelecao.unidadesSelecionadas.Count == 0)
        {
            if (MenuConstrucao.EstaAberto)
            {
                return "Construcao aberta: escolha um item e confirme a expansao da base.";
            }

            return "Sem selecao. Arraste com o mouse para formar um grupo ou clique em uma unidade.";
        }

        int total = 0;
        int ativas = 0;
        string estado = "--";

        for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
            if (unidade == null)
            {
                continue;
            }

            total++;
            bool passivo;
            string descricao;
            if (unidade.TryObterEstadoCombate(out passivo, out descricao))
            {
                estado = descricao;
            }

            if (unidade.isActiveAndEnabled)
            {
                ativas++;
            }
        }

        return $"Selecao: {ativas}/{total} prontas | Estado atual: {estado}";
    }

    private string MontarSufixoRuntime()
    {
        if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            return " | Runtime saturado";
        }

        if (DiagnosticoDesempenhoJogo.RuntimeSobPressao())
        {
            return " | Runtime sob pressao";
        }

        return " | Runtime estavel";
    }

    private void AtualizarVisibilidadePainel(bool imediato)
    {
        if (painel == null || textoAtalhos == null)
        {
            return;
        }

        textoAtalhos.gameObject.SetActive(expandido);
        painel.sizeDelta = expandido ? new Vector2(500f, 240f) : new Vector2(500f, 128f);

        if (imediato && grupoCanvas != null)
        {
            grupoCanvas.alpha = 1f;
        }
    }

    private void ConstruirInterface()
    {
        canvas = new GameObject("Canvas_AjudaRTS").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7200;
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvas.gameObject.AddComponent<GraphicRaycaster>();
        grupoCanvas = canvas.gameObject.AddComponent<CanvasGroup>();

        painel = CriarPainel("PainelAjuda", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(500f, 240f), new Color(0.05f, 0.09f, 0.12f, 0.88f));
        painel.pivot = new Vector2(0f, 1f);
        Outline outline = painel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.36f, 0.84f, 0.98f, 0.2f);
        outline.effectDistance = new Vector2(1f, -1f);

        textoCabecalho = CriarTexto("Cabecalho", painel, "COMANDO TATICO", 20, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.86f, 0.98f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -14f), new Vector2(-36f, 28f));
        textoObjetivo = CriarTexto("Objetivo", painel, string.Empty, 13, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.72f, 0.9f, 0.98f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -48f), new Vector2(-36f, 34f));
        textoSelecao = CriarTexto("Selecao", painel, string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.94f, 0.98f, 1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -90f), new Vector2(-36f, 28f));
        textoAtalhos = CriarTexto("Atalhos", painel, string.Empty, 12, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.78f, 0.88f, 0.93f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -122f), new Vector2(-36f, 84f));
        textoRuntime = CriarTexto("Runtime", painel, string.Empty, 12, FontStyle.Bold, TextAnchor.LowerLeft, new Color(0.84f, 0.97f, 0.88f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-36f, 24f));
        textoToast = CriarTexto("Toast", canvas.transform, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.74f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(860f, 34f));
        textoToast.gameObject.SetActive(false);
    }

    private RectTransform CriarPainel(string nome, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color cor)
    {
        GameObject objeto = new GameObject(nome, typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(parent, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image imagem = objeto.GetComponent<Image>();
        imagem.color = cor;
        return rect;
    }

    private Text CriarTexto(string nome, Transform parent, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject objeto = new GameObject(nome, typeof(RectTransform), typeof(Text));
        objeto.transform.SetParent(parent, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text texto = objeto.GetComponent<Text>();
        texto.text = conteudo;
        texto.font = fontePadrao;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.supportRichText = true;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow sombra = objeto.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.28f);
        sombra.effectDistance = new Vector2(1f, -1f);
        return texto;
    }

    private void GarantirEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }
}
