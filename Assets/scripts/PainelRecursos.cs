using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class PainelRecursos : MonoBehaviour
{
    [Header("Configuração Geral")]
    [SerializeField] private bool construirAutomaticamente = true;
    [SerializeField] private float larguraHUD = 330f;
    [SerializeField] private float margemEsquerda = 16f;
    [SerializeField] private float margemTopo = 44f;
    [SerializeField] private float espacamentoBlocos = 8f;
    [SerializeField] private float escalaHUD = 0.60f;

    [Header("Cores")]
    [SerializeField] private Color corPainel = new Color(0.025f, 0.032f, 0.038f, 0.92f);
    [SerializeField] private Color corPainel2 = new Color(0.045f, 0.055f, 0.064f, 0.94f);
    [SerializeField] private Color corHeader = new Color(0.018f, 0.023f, 0.028f, 0.96f);
    [SerializeField] private Color corBorda = new Color(0.35f, 0.55f, 0.65f, 0.38f);
    [SerializeField] private Color corDivisoria = new Color(1f, 1f, 1f, 0.10f);
    [SerializeField] private Color corTexto = new Color(0.90f, 0.95f, 1f, 1f);
    [SerializeField] private Color corTextoSecundario = new Color(0.58f, 0.66f, 0.72f, 1f);
    [SerializeField] private Color corTitulo = new Color(0.86f, 0.94f, 1f, 1f);
    [SerializeField] private Color corCiano = new Color(0.05f, 0.70f, 1f, 1f);
    [SerializeField] private Color corVerde = new Color(0.22f, 1f, 0.36f, 1f);
    [SerializeField] private Color corAmarelo = new Color(1f, 0.70f, 0.20f, 1f);
    [SerializeField] private Color corVermelho = new Color(1f, 0.22f, 0.18f, 1f);

    [Header("Recursos Principais")]
    public TextMeshProUGUI textoDinheiro;
    public TextMeshProUGUI textoPetroleo;
    public TextMeshProUGUI textoAco;
    public TextMeshProUGUI textoEnergia;
    public TextMeshProUGUI textoComida;
    public TextMeshProUGUI textoPopulacao;
    public TextMeshProUGUI textoPais;
    public TextMeshProUGUI textoMoeda;
    public TextMeshProUGUI textoOuro;

    [Header("Ganhos")]
    public TextMeshProUGUI ganhoTextoDinheiro;
    public TextMeshProUGUI ganhoTextoPetroleo;
    public TextMeshProUGUI ganhoTextoAco;
    public TextMeshProUGUI ganhoTextoEnergia;

    [Header("Status Extra")]
    public TextMeshProUGUI textoEstoque;
    public TextMeshProUGUI textoExercito;

    private TextMeshProUGUI statusAmeacaValor;
    private TextMeshProUGUI statusEnergiaValor;
    private TextMeshProUGUI statusCambioValor;
    private TextMeshProUGUI statusEstoqueValor;

    private RectTransform raizHUD;
    private RectTransform blocoRecursos;
    private RectTransform blocoStatus;
    private RectTransform blocoObjetivos;

    private RectTransform conteudoRecursos;
    private RectTransform conteudoStatus;
    private RectTransform conteudoObjetivos;

    private TextMeshProUGUI setaRecursos;
    private TextMeshProUGUI setaStatus;
    private TextMeshProUGUI setaObjetivos;

    private bool recursosAberto = true;
    private bool statusAberto = true;
    private bool objetivosAberto = true;

    private const float AlturaHeader = 30f;
    private const float AlturaFechado = 30f;
    private const float AlturaRecursosAberto = 454f;
    private const float AlturaStatusAberto = 142f;
    private const float AlturaObjetivosAberto = 142f;

    private void AplicarVisualSimplificado()
    {
        larguraHUD = Mathf.Max(larguraHUD, 340f);
        escalaHUD = Mathf.Max(escalaHUD, 0.68f);
        margemEsquerda = Mathf.Max(margemEsquerda, 14f);
        margemTopo = Mathf.Max(margemTopo, 34f);
        espacamentoBlocos = Mathf.Max(espacamentoBlocos, 8f);
    }

    private void Awake()
    {
        AplicarVisualSimplificado();
        RemoverVisualDoObjetoBase();

        if (construirAutomaticamente)
        {
            GarantirCanvas();
            ConstruirHUD();
        }
    }

    private void Start()
    {
        AtualizarTudo();

        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados += AtualizarTudo;

        if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado += AtualizarTudo;

        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados += AtualizarTudo;
    }

    private void OnDestroy()
    {
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados -= AtualizarTudo;

        if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado -= AtualizarTudo;

        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados -= AtualizarTudo;
    }

    [ContextMenu("Reconstruir HUD Lateral Novo")]
    public void ReconstruirHUD()
    {
        RemoverVisualDoObjetoBase();
        GarantirCanvas();
        ConstruirHUD();
        AtualizarTudo();
    }

    private void GarantirCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas_HUD_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            transform.SetParent(canvasGO.transform, false);
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetAsLastSibling();
        }
    }

    private void ConstruirHUD()
    {
        AplicarVisualSimplificado();
        LimparFilhos(transform);

        GameObject raizGO = CriarObjetoUI("HUD_Lateral_Esquerdo_Warno", transform);
        raizHUD = raizGO.GetComponent<RectTransform>();
        raizHUD.anchorMin = new Vector2(0f, 1f);
        raizHUD.anchorMax = new Vector2(0f, 1f);
        raizHUD.pivot = new Vector2(0f, 1f);
        raizHUD.anchoredPosition = new Vector2(margemEsquerda, -margemTopo);
        raizHUD.sizeDelta = new Vector2(larguraHUD, 820f);
        raizHUD.localScale = Vector3.one * escalaHUD;

        // Garante que o hit-test de UI fica restrito à área real do painel
        CanvasGroup cg = raizGO.AddComponent<CanvasGroup>();
        cg.interactable = true;
        cg.blocksRaycasts = true;

        VerticalLayoutGroup layout = raizGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = espacamentoBlocos;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        blocoRecursos = CriarPainelBase(raizHUD, "Bloco_Recursos", larguraHUD, AlturaRecursosAberto);
        CriarPainelRecursos(blocoRecursos);

        blocoStatus = CriarPainelBase(raizHUD, "Bloco_Status", larguraHUD, AlturaStatusAberto);
        CriarPainelStatus(blocoStatus);

        blocoObjetivos = CriarPainelBase(raizHUD, "Bloco_Objetivos", larguraHUD, AlturaObjetivosAberto);
        CriarPainelObjetivos(blocoObjetivos);

        AtualizarAlturasBlocos();
    }

    private void CriarPainelRecursos(RectTransform parent)
    {
        CriarHeaderSecao(parent, "◆", "PAINEL NACIONAL", out setaRecursos, ToggleRecursos);

        conteudoRecursos = CriarRect("Conteudo_Recursos", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -AlturaHeader), Vector2.zero);
        conteudoRecursos.offsetMin = new Vector2(14f, 12f);
        conteudoRecursos.offsetMax = new Vector2(-14f, -AlturaHeader - 6f);

        CriarLinhaRecurso(conteudoRecursos, 0, "$", "DINHEIRO", "$ 0", corVerde, out textoDinheiro, out ganhoTextoDinheiro);
        CriarLinhaRecurso(conteudoRecursos, 1, "E", "ENERGIA", "0%", corAmarelo, out textoEnergia, out ganhoTextoEnergia);
        CriarLinhaRecurso(conteudoRecursos, 2, "OIL", "PETROLEO", "0", corCiano, out textoPetroleo, out ganhoTextoPetroleo);
        CriarLinhaRecurso(conteudoRecursos, 3, "AC", "ACO", "0", new Color(0.75f, 0.80f, 0.85f, 1f), out textoAco, out ganhoTextoAco);
        CriarLinhaRecurso(conteudoRecursos, 4, "FD", "COMIDA", "0", corVerde, out textoComida, out _);
        CriarLinhaRecurso(conteudoRecursos, 5, "POP", "POPULACAO", "0/0", new Color(0.45f, 0.80f, 1f, 1f), out textoPopulacao, out _);
        CriarLinhaRecurso(conteudoRecursos, 6, "BOX", "ARMAZEM", "0%", corAmarelo, out textoEstoque, out _);
        CriarLinhaRecurso(conteudoRecursos, 7, "PA", "PAIS", "-", new Color(0.95f, 0.95f, 0.82f, 1f), out textoPais, out _);
        CriarLinhaRecurso(conteudoRecursos, 8, "MO", "MOEDA", "-", corAmarelo, out textoMoeda, out _);
        CriarLinhaRecurso(conteudoRecursos, 9, "AU", "OURO", "0", new Color(1f, 0.78f, 0.28f, 1f), out textoOuro, out _);
        CriarLinhaRecurso(conteudoRecursos, 10, "MIL", "MILITAR", "0", new Color(0.65f, 0.78f, 0.90f, 1f), out textoExercito, out _);
    }

    private void CriarLinhaRecurso(RectTransform parent, int index, string icone, string nome, string valorInicial, Color corIcone, out TextMeshProUGUI txtValor, out TextMeshProUGUI txtGanho)
    {
        float altura = 34f;
        float y = -index * altura;

        RectTransform linha = CriarRect("Recurso_" + nome, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, altura));

        if (index > 0)
            CriarLinhaHorizontal(linha, 0f, 0f, 0f, 0.08f);

        RectTransform badge = CriarRect("Icone", linha, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 26f));
        Image badgeImg = badge.gameObject.AddComponent<Image>();
        badgeImg.color = new Color(corIcone.r, corIcone.g, corIcone.b, 0.13f);
        badgeImg.raycastTarget = false;

        Outline badgeOutline = badge.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(corIcone.r, corIcone.g, corIcone.b, 0.42f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI iconTxt = CriarTexto("Texto_Icone", badge, icone, 16, corIcone, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform iconRect = iconTxt.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = CriarTexto("Label", linha, nome, 12, corTextoSecundario, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform l = label.GetComponent<RectTransform>();
        l.anchorMin = new Vector2(0f, 0f);
        l.anchorMax = new Vector2(0f, 1f);
        l.pivot = new Vector2(0f, 0.5f);
        l.anchoredPosition = new Vector2(42f, 0f);
        l.sizeDelta = new Vector2(120f, altura);

        txtValor = CriarTexto("Valor", linha, valorInicial, 16, corTexto, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform v = txtValor.GetComponent<RectTransform>();
        v.anchorMin = new Vector2(1f, 0f);
        v.anchorMax = new Vector2(1f, 1f);
        v.pivot = new Vector2(1f, 0.5f);
        v.anchoredPosition = new Vector2(-54f, 0f);
        v.sizeDelta = new Vector2(110f, altura);

        txtGanho = CriarTexto("Ganho", linha, "", 12, corVerde, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform g = txtGanho.GetComponent<RectTransform>();
        g.anchorMin = new Vector2(1f, 0f);
        g.anchorMax = new Vector2(1f, 1f);
        g.pivot = new Vector2(1f, 0.5f);
        g.anchoredPosition = Vector2.zero;
        g.sizeDelta = new Vector2(54f, altura);
    }



    private void CriarPainelStatus(RectTransform parent)
    {
        CriarHeaderSecao(parent, "●", "ALERTAS", out setaStatus, ToggleStatus);

        conteudoStatus = CriarRect("Conteudo_Status", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -AlturaHeader), Vector2.zero);
        conteudoStatus.offsetMin = new Vector2(18f, 12f);
        conteudoStatus.offsetMax = new Vector2(-18f, -AlturaHeader - 12f);

        statusAmeacaValor = CriarLinhaStatus(conteudoStatus, 0, "Ameaca:", "Baixa", corVerde);
        statusEnergiaValor = CriarLinhaStatus(conteudoStatus, 1, "Energia:", "OK", corVerde);
        statusCambioValor = CriarLinhaStatus(conteudoStatus, 2, "Cambio:", "1.00", corCiano);
        statusEstoqueValor = CriarLinhaStatus(conteudoStatus, 3, "Estoque:", "OK", corVerde);
    }

    private TextMeshProUGUI CriarLinhaStatus(RectTransform parent, int index, string label, string valor, Color corValor)
    {
        float h = 28f;

        RectTransform linha = CriarRect("Status_" + index, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * h), new Vector2(0f, h));

        TextMeshProUGUI ponto = CriarTexto("Ponto", linha, "•", 16, corCiano, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform p = ponto.GetComponent<RectTransform>();
        p.anchorMin = new Vector2(0f, 0f);
        p.anchorMax = new Vector2(0f, 1f);
        p.sizeDelta = new Vector2(14f, h);

        TextMeshProUGUI lbl = CriarTexto("Label", linha, label, 14, corTextoSecundario, TextAlignmentOptions.Left, FontStyles.Normal);
        RectTransform l = lbl.GetComponent<RectTransform>();
        l.anchorMin = new Vector2(0f, 0f);
        l.anchorMax = new Vector2(0.65f, 1f);
        l.offsetMin = new Vector2(14f, 0f);
        l.offsetMax = Vector2.zero;

        TextMeshProUGUI val = CriarTexto("Valor", linha, valor, 14, corValor, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform v = val.GetComponent<RectTransform>();
        v.anchorMin = new Vector2(0.55f, 0f);
        v.anchorMax = new Vector2(1f, 1f);
        v.offsetMin = Vector2.zero;
        v.offsetMax = Vector2.zero;
        return val;
    }

    private void CriarPainelObjetivos(RectTransform parent)
    {
        CriarHeaderSecao(parent, "◇", "COMANDO", out setaObjetivos, ToggleObjetivos);

        conteudoObjetivos = CriarRect("Conteudo_Objetivos", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -AlturaHeader), Vector2.zero);
        conteudoObjetivos.offsetMin = new Vector2(18f, 12f);
        conteudoObjetivos.offsetMax = new Vector2(-18f, -AlturaHeader - 12f);

        CriarObjetivo(conteudoObjetivos, 0, "Expandir costa", "0/3");
        CriarObjetivo(conteudoObjetivos, 1, "Suprir petróleo", "320/500");
        CriarObjetivo(conteudoObjetivos, 2, "Aliar neutros", "1/2");
    }

    private void CriarObjetivo(RectTransform parent, int index, string texto, string progresso)
    {
        float h = 34f;

        RectTransform linha = CriarRect("Objetivo_" + index, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * h), new Vector2(0f, h));

        if (index > 0)
            CriarLinhaHorizontal(linha, 0f, 0f, 0f, 0.08f);

        RectTransform badgeNum = CriarRect("Badge_Numero", linha, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 26f));
        Image numBg = badgeNum.gameObject.AddComponent<Image>();
        numBg.color = new Color(1f, 1f, 1f, 0.05f);
        numBg.raycastTarget = false;

        TextMeshProUGUI numero = CriarTexto("Numero", badgeNum, (index + 1).ToString(), 16, corTexto, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform n = numero.GetComponent<RectTransform>();
        n.anchorMin = Vector2.zero;
        n.anchorMax = Vector2.one;
        n.offsetMin = Vector2.zero;
        n.offsetMax = Vector2.zero;

        TextMeshProUGUI lbl = CriarTexto("Texto", linha, texto, 14, corTextoSecundario, TextAlignmentOptions.Left, FontStyles.Normal);
        RectTransform l = lbl.GetComponent<RectTransform>();
        l.anchorMin = new Vector2(0f, 0f);
        l.anchorMax = new Vector2(1f, 1f);
        l.offsetMin = new Vector2(42f, 0f);
        l.offsetMax = new Vector2(-70f, 0f);

        TextMeshProUGUI prog = CriarTexto("Progresso", linha, progresso, 14, corTexto, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform p = prog.GetComponent<RectTransform>();
        p.anchorMin = new Vector2(1f, 0f);
        p.anchorMax = new Vector2(1f, 1f);
        p.pivot = new Vector2(1f, 0.5f);
        p.anchoredPosition = Vector2.zero;
        p.sizeDelta = new Vector2(70f, h);
    }

    private void CriarHeaderSecao(RectTransform parent, string icone, string titulo, out TextMeshProUGUI seta, UnityAction acao)
    {
        RectTransform header = CriarRect("Header", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, AlturaHeader));

        Image headerBg = header.gameObject.AddComponent<Image>();
        headerBg.color = corHeader;
        headerBg.raycastTarget = true;

        CriarFaixaLateral(header, corCiano);

        TextMeshProUGUI iconTxt = CriarTexto("Icone_Header", header, icone, 18, corTextoSecundario, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform i = iconTxt.GetComponent<RectTransform>();
        i.anchorMin = new Vector2(0f, 0f);
        i.anchorMax = new Vector2(0f, 1f);
        i.pivot = new Vector2(0f, 0.5f);
        i.anchoredPosition = new Vector2(16f, 0f);
        i.sizeDelta = new Vector2(26f, AlturaHeader);

        TextMeshProUGUI txt = CriarTexto("Titulo", header, titulo, 15, corTitulo, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform t = txt.GetComponent<RectTransform>();
        t.anchorMin = new Vector2(0f, 0f);
        t.anchorMax = new Vector2(1f, 1f);
        t.offsetMin = new Vector2(44f, 0f);
        t.offsetMax = new Vector2(-42f, 0f);

        seta = CriarTexto("Seta", header, "⌄", 20, corTexto, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform s = seta.GetComponent<RectTransform>();
        s.anchorMin = new Vector2(1f, 0.5f);
        s.anchorMax = new Vector2(1f, 0.5f);
        s.pivot = new Vector2(0.5f, 0.5f);
        s.anchoredPosition = new Vector2(-20f, 0f);
        s.sizeDelta = new Vector2(28f, 28f);

        Button botao = header.gameObject.AddComponent<Button>();
        botao.targetGraphic = headerBg;
        botao.transition = Selectable.Transition.ColorTint;

        ColorBlock cb = botao.colors;
        cb.normalColor = corHeader;
        cb.highlightedColor = new Color(0.04f, 0.11f, 0.14f, 1f);
        cb.pressedColor = new Color(0.04f, 0.22f, 0.30f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.colorMultiplier = 1f;
        botao.colors = cb;
        botao.onClick.AddListener(acao);
    }

    private RectTransform CriarPainelBase(Transform parent, string nome, float largura, float altura)
    {
        GameObject go = CriarObjetoUI(nome, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(largura, altura);

        Image img = go.AddComponent<Image>();
        img.color = corPainel;
        img.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = corBorda;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(2f, -2f);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = largura;
        le.preferredHeight = altura;
        le.minWidth = largura;
        le.minHeight = altura;

        return rect;
    }

    private void CriarFaixaLateral(RectTransform parent, Color cor)
    {
        RectTransform faixa = CriarRect("Faixa_Lateral", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
        faixa.offsetMin = new Vector2(0f, 0f);
        faixa.offsetMax = new Vector2(4f, 0f);

        Image img = faixa.gameObject.AddComponent<Image>();
        img.color = new Color(cor.r, cor.g, cor.b, 0.90f);
        img.raycastTarget = false;
    }

    private void CriarLinhaHorizontal(RectTransform parent, float esquerda, float topo, float direita, float alpha)
    {
        RectTransform linha = CriarRect("Divisoria", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -topo), new Vector2(0f, 1f));
        linha.offsetMin = new Vector2(esquerda, linha.offsetMin.y);
        linha.offsetMax = new Vector2(-direita, linha.offsetMax.y);

        Image img = linha.gameObject.AddComponent<Image>();
        img.color = new Color(corDivisoria.r, corDivisoria.g, corDivisoria.b, alpha);
        img.raycastTarget = false;
    }

    private TextMeshProUGUI CriarTexto(string nome, Transform parent, string texto, int tamanho, Color cor, TextAlignmentOptions alinhamento, FontStyles estilo)
    {
        GameObject go = CriarObjetoUI(nome, parent);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.fontStyle = estilo;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }

    private RectTransform CriarRect(string nome, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = CriarObjetoUI(nome, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private GameObject CriarObjetoUI(string nome, Transform parent)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void LimparFilhos(Transform alvo)
    {
        for (int i = alvo.childCount - 1; i >= 0; i--)
        {
            Transform filho = alvo.GetChild(i);

            if (Application.isPlaying)
                Destroy(filho.gameObject);
            else
                DestroyImmediate(filho.gameObject);
        }
    }

    private void RemoverVisualDoObjetoBase()
    {
        Image[] imagens = GetComponents<Image>();
        for (int i = 0; i < imagens.Length; i++)
        {
            if (Application.isPlaying) Destroy(imagens[i]);
            else DestroyImmediate(imagens[i]);
        }

        RawImage[] rawImages = GetComponents<RawImage>();
        for (int i = 0; i < rawImages.Length; i++)
        {
            if (Application.isPlaying) Destroy(rawImages[i]);
            else DestroyImmediate(rawImages[i]);
        }

        Outline[] outlines = GetComponents<Outline>();
        for (int i = 0; i < outlines.Length; i++)
        {
            if (Application.isPlaying) Destroy(outlines[i]);
            else DestroyImmediate(outlines[i]);
        }

        Shadow[] shadows = GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (Application.isPlaying) Destroy(shadows[i]);
            else DestroyImmediate(shadows[i]);
        }

        Mask[] masks = GetComponents<Mask>();
        for (int i = 0; i < masks.Length; i++)
        {
            if (Application.isPlaying) Destroy(masks[i]);
            else DestroyImmediate(masks[i]);
        }
    }

    private void ToggleRecursos()
    {
        recursosAberto = !recursosAberto;
        AtualizarAlturasBlocos();
    }



    private void ToggleStatus()
    {
        statusAberto = !statusAberto;
        AtualizarAlturasBlocos();
    }

    private void ToggleObjetivos()
    {
        objetivosAberto = !objetivosAberto;
        AtualizarAlturasBlocos();
    }

    private void AtualizarAlturasBlocos()
    {
        AjustarBloco(blocoRecursos, conteudoRecursos, setaRecursos, recursosAberto, AlturaRecursosAberto);
        AjustarBloco(blocoStatus, conteudoStatus, setaStatus, statusAberto, AlturaStatusAberto);
        AjustarBloco(blocoObjetivos, conteudoObjetivos, setaObjetivos, objetivosAberto, AlturaObjetivosAberto);
    }

    private void AjustarBloco(RectTransform bloco, RectTransform conteudo, TextMeshProUGUI seta, bool aberto, float alturaAberta)
    {
        if (bloco == null) return;

        float h = aberto ? alturaAberta : AlturaFechado;
        bloco.sizeDelta = new Vector2(larguraHUD, h);

        LayoutElement le = bloco.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredHeight = h;
            le.minHeight = h;
        }

        if (conteudo != null)
            conteudo.gameObject.SetActive(aberto);

        if (seta != null)
            seta.text = aberto ? "⌄" : "›";
    }

    private void AtualizarTudo()
    {
        if (GerenciadorRecursos.Instancia == null)
        {
            AtualizarTexto(textoDinheiro, 10320, ganhoTextoDinheiro, 11, "$ ");
            if (textoEnergia != null) textoEnergia.text = "0% USO";
            if (ganhoTextoEnergia != null) ganhoTextoEnergia.text = "0/0";
            AtualizarTexto(textoPetroleo, 500, ganhoTextoPetroleo, 0, "");
            AtualizarTexto(textoAco, 325, ganhoTextoAco, 5, "");
            AtualizarTexto(textoComida, 240, null, 0, "");

            if (textoPopulacao != null) textoPopulacao.text = "12/200";
            if (textoPais != null) textoPais.text = "Republica Atlas";
            if (textoMoeda != null) textoMoeda.text = "Atlas 1.00x";
            if (textoOuro != null) textoOuro.text = "500";
            if (textoEstoque != null) textoEstoque.text = "42%";
            if (textoExercito != null) textoExercito.text = "0";
            AtualizarStatusLateral(null, 0f, 0f);

            return;
        }

        var r = GerenciadorRecursos.Instancia;

        AtualizarTexto(textoDinheiro, r.dinheiro, ganhoTextoDinheiro, r.dinheiroPorSegundo, "$ ");
        AtualizarTexto(textoPetroleo, r.petroleo, ganhoTextoPetroleo, r.petroleoPorSegundo, "");
        AtualizarTexto(textoAco, r.aco, ganhoTextoAco, r.acoPorSegundo, "");
        AtualizarTexto(textoComida, r.comida, null, 0, "");

        if (textoPopulacao != null)
            textoPopulacao.text = $"{r.populacaoAtual:N0}/{r.populacaoMaxima:N0}";

        SistemaGovernoMundial.GarantirInstancia();
        DadosPaisGoverno paisJogador = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(SistemaGovernoMundial.Instancia.teamJogador)
            : null;

        float energiaConsumida = paisJogador != null ? paisJogador.energiaConsumida : 0f;
        float energiaProduzida = paisJogador != null ? paisJogador.energiaProduzida : Mathf.Max(0f, r.energia);
        AtualizarEnergiaHUD(energiaConsumida, energiaProduzida, r.energia);

        if (textoPais != null)
            textoPais.text = paisJogador != null ? paisJogador.nomePais : "Pais 1";
        if (textoMoeda != null)
            textoMoeda.text = paisJogador != null ? $"{paisJogador.nomeMoeda} {paisJogador.cambioComLider:0.00}x" : "$";
        if (textoOuro != null)
            textoOuro.text = paisJogador != null ? paisJogador.reservaOuro.ToString("N0") : "0";

        float ocupacaoArmazem = -1f;
        if (textoEstoque != null && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
        {
            ocupacaoArmazem = GerenciadorArmazens.Instancia.armazemRecursos.PercentualOcupacao();
            textoEstoque.text = ocupacaoArmazem >= 90f ? $"{ocupacaoArmazem:F0}% CHEIO" : $"{ocupacaoArmazem:F0}%";
            textoEstoque.color = ocupacaoArmazem >= 90f ? corVermelho : ocupacaoArmazem >= 75f ? corAmarelo : corTexto;
        }
        else if (textoEstoque != null)
        {
            textoEstoque.text = "OK";
            textoEstoque.color = corTexto;
        }

        if (textoExercito != null && CensoImperial.Instancia != null)
            textoExercito.text = CensoImperial.Instancia.totalUnidades.ToString("N0");

        AtualizarStatusLateral(paisJogador, energiaConsumida, energiaProduzida, ocupacaoArmazem);
    }

    private void AtualizarEnergiaHUD(float consumida, float produzida, int estoqueEnergia)
    {
        if (textoEnergia == null) return;

        if (produzida > 0.01f)
        {
            float uso = Mathf.Clamp((consumida / produzida) * 100f, 0f, 999f);
            textoEnergia.text = uso >= 100f ? "DEFICIT" : $"{uso:0}% USO";
            textoEnergia.color = uso >= 100f ? corVermelho : uso >= 90f ? corAmarelo : corTexto;
            if (ganhoTextoEnergia != null)
            {
                ganhoTextoEnergia.text = $"{consumida:0}/{produzida:0}";
                ganhoTextoEnergia.color = uso >= 100f ? corVermelho : uso >= 90f ? corAmarelo : corVerde;
            }
        }
        else
        {
            textoEnergia.text = estoqueEnergia.ToString("N0");
            textoEnergia.color = corTexto;
            if (ganhoTextoEnergia != null)
            {
                ganhoTextoEnergia.text = "+0/s";
                ganhoTextoEnergia.color = corVerde;
            }
        }
    }

    private void AtualizarStatusLateral(DadosPaisGoverno pais, float energiaConsumida, float energiaProduzida, float ocupacaoArmazem = -1f)
    {
        float usoEnergia = energiaProduzida > 0.01f ? (energiaConsumida / energiaProduzida) * 100f : 0f;

        if (statusAmeacaValor != null)
        {
            string valor = pais == null ? "Baixa" : pais.emGuerra ? "Guerra" : pais.sancionado || pais.estabilidade < 45f ? "Alta" : pais.estabilidade < 65f ? "Media" : "Baixa";
            statusAmeacaValor.text = valor;
            statusAmeacaValor.color = valor == "Guerra" || valor == "Alta" ? corVermelho : valor == "Media" ? corAmarelo : corVerde;
        }

        if (statusEnergiaValor != null)
        {
            statusEnergiaValor.text = usoEnergia >= 100f ? "Deficit" : usoEnergia >= 90f ? "Aviso" : "OK";
            statusEnergiaValor.color = usoEnergia >= 100f ? corVermelho : usoEnergia >= 90f ? corAmarelo : corVerde;
        }

        if (statusCambioValor != null)
        {
            statusCambioValor.text = pais != null ? pais.cambioComLider.ToString("0.00") + "x" : "1.00x";
            statusCambioValor.color = pais != null && pais.cambioComLider < 0.75f ? corVermelho : pais != null && pais.cambioComLider < 0.95f ? corAmarelo : corCiano;
        }

        if (statusEstoqueValor != null)
        {
            if (ocupacaoArmazem >= 0f)
            {
                statusEstoqueValor.text = ocupacaoArmazem >= 90f ? "Cheio" : ocupacaoArmazem >= 75f ? "Alto" : "OK";
                statusEstoqueValor.color = ocupacaoArmazem >= 90f ? corVermelho : ocupacaoArmazem >= 75f ? corAmarelo : corVerde;
            }
            else
            {
                statusEstoqueValor.text = "OK";
                statusEstoqueValor.color = corVerde;
            }
        }
    }

    private void AtualizarTexto(TextMeshProUGUI txtValor, float valor, TextMeshProUGUI txtGanho, float ganho, string prefixo)
    {
        if (txtValor != null)
            txtValor.text = $"{prefixo}{valor:N0}";

        if (txtGanho != null)
        {
            if (ganho > 0)
            {
                txtGanho.text = $"+{ganho:N0}/s";
                txtGanho.color = corVerde;
            }
            else if (ganho < 0)
            {
                txtGanho.text = $"{ganho:N0}/s";
                txtGanho.color = corVermelho;
            }
            else
            {
                txtGanho.text = "+0/s";
                txtGanho.color = corVerde;
            }
        }
    }
}
