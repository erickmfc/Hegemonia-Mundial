using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class GerenciadorEmojis : MonoBehaviour
{
    // Lógica Estática de Tradução
    private static readonly Dictionary<string, string> mapaEmojis = new Dictionary<string, string>()
    {
        {":dinheiro:", "▣"}, {":tesouro:", "▣"}, {":comida:", "🌾"}, {":reserva:", "≋"},
        {":petroleo:", "💧"}, {":aco:", "▰"}, {":armas:", "▥"}, {":armamento:", "▥"},
        {":populacao:", "♟"}, {":povo:", "♟"}, {":militares:", "⚔"}, {":exercito:", "▱"},
        {":marinha:", "▰"}, {":aerea:", "✈"}, {":status:", "◈"}, {":bloco:", "⚑"},
        {":alianca:", "🤝"}, {":alvo:", "⌖"}, {":casa:", "⌂"}, {":habitacao:", "⌂"},
        {":estrela:", "★"}, {":tecnologia:", "⚛"}, {":ciencia:", "⚗"}, {":energia:", "⚡"},
        {":alerta:", "△"}, {":trabalho:", "♟"}, {":industria:", "🏭"}, {":saude:", "+"},
        {": educação:", "▣"}
    };

    public static string TraduzirEmojis(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        string resultado = texto;
        foreach (var par in mapaEmojis) resultado = resultado.Replace(par.Key, par.Value);
        return resultado;
    }

    [Header("Atalho")]
    public KeyCode teclaAbrir = KeyCode.F9;

    [Header("Fonte")]
    public Font fonteEmoji;
    public Font fonteTexto;

    [Header("Layout")]
    [Range(0.40f, 1.00f)] public float larguraTela = 0.76f;
    [Range(0.40f, 1.00f)] public float alturaTela = 0.78f;
    public int tamanhoEmoji = 34;
    public int tamanhoTexto = 15;
    public int colunas = 6;

    private GameObject canvasObj;
    private GameObject painel;
    private bool aberto;

    private readonly string[] emojisMenuGoverno = new string[]
    {
        "✦", "⚜", "▣", "≋", "◊", "▰", "▥", "♟", "◈", "⚑", "🤝", "⌖",
        "⌂", "★", "🌾", "💧", "⚛", "✈", "☢", "📈", "⚖", "🏭", "🛡", "⚔",
        "📡", "♥", "⚡", "♨", "●", "$", "⚗", "🧠", "🔒", "♙", "☺", "⚙",
        "🌿", "🏦", "🚚", "△", "▱", "╱", "╲", "↑", "→", "+", "X", "</>"
    };

    private readonly Dictionary<string, string> nomes = new Dictionary<string, string>()
    {
        {"✦", "Estrela/Bandeira"},
        {"⚜", "Ornamento Governo"},
        {"▣", "Dinheiro/Tesouro"},
        {"≋", "Comida/Reserva"},
        {"◊", "Petróleo"},
        {"▰", "Aço"},
        {"▥", "Armamentos/Lab"},
        {"♟", "População/Militares"},
        {"◈", "Status Nacional"},
        {"⚑", "Bloco"},
        {"🤝", "Aliança"},
        {"⌖", "Rival/Alvo"},
        {"⌂", "Habitação"},
        {"★", "Favorito"},
        {"🌾", "Comida"},
        {"💧", "Petróleo"},
        {"⚛", "Tecnologia"},
        {"✈", "Força Aérea"},
        {"☢", "Urânio"},
        {"📈", "Crescimento"},
        {"⚖", "Inflação/Equilíbrio"},
        {"🏭", "Indústria"},
        {"🛡", "Defesa"},
        {"⚔", "Operação Militar"},
        {"📡", "Inteligência"},
        {"♥", "Bem-estar"},
        {"⚡", "Energia"},
        {"♨", "Saneamento"},
        {"●", "Prontidão"},
        {"$", "Gasto"},
        {"⚗", "Ciência"},
        {"🧠", "Pesquisa/IA"},
        {"🔒", "Bloqueado"},
        {"♙", "População Ativa"},
        {"☺", "Satisfação"},
        {"⚙", "Engenharia"},
        {"🌿", "Agricultura"},
        {"🏦", "Banco/Fiscal"},
        {"🚚", "Logística"},
        {"△", "Alerta"},
        {"▱", "Exército"},
        {"╱", "Gráfico"},
        {"╲", "Gráfico"},
        {"↑", "Subindo"},
        {"→", "Estável"},
        {"+", "Adicionar"},
        {"X", "Fechar"},
        {"</>", "Programador"}
    };

    private void Awake()
    {
        GarantirCanvasEEventSystem();
        CriarPainel();
        FecharImediato();
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAbrir))
        {
            aberto = !aberto;
            painel.SetActive(aberto);
        }
    }

    private void GarantirCanvasEEventSystem()
    {
        Canvas canvasExistente = FindFirstObjectByType<Canvas>();

        if (canvasExistente != null)
        {
            canvasObj = canvasExistente.gameObject;
            canvasExistente.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasExistente.sortingOrder = Mathf.Max(canvasExistente.sortingOrder, 9000);
        }
        else
        {
            canvasObj = new GameObject("Canvas_Emoji_Debug");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventObj = new GameObject("EventSystem_Auto_Emoji_Debug");
            eventObj.AddComponent<EventSystem>();
            eventObj.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventObj);
        }
    }

    private void CriarPainel()
    {
        Transform antigo = canvasObj.transform.Find("Painel_Emoji_Debug_MenuGoverno");
        if (antigo != null) Destroy(antigo.gameObject);

        painel = CriarObjetoUI("Painel_Emoji_Debug_MenuGoverno", canvasObj.transform);

        RectTransform rt = painel.GetComponent<RectTransform>();
        float meiaLargura = larguraTela * 0.5f;
        float meiaAltura = alturaTela * 0.5f;
        rt.anchorMin = new Vector2(0.5f - meiaLargura, 0.5f - meiaAltura);
        rt.anchorMax = new Vector2(0.5f + meiaLargura, 0.5f + meiaAltura);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image fundo = painel.AddComponent<Image>();
        fundo.color = new Color(0.015f, 0.035f, 0.050f, 0.97f);

        Outline outline = painel.AddComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.72f, 1f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup raiz = painel.AddComponent<VerticalLayoutGroup>();
        raiz.padding = new RectOffset(18, 18, 14, 18);
        raiz.spacing = 12;
        raiz.childControlWidth = true;
        raiz.childControlHeight = true;
        raiz.childForceExpandWidth = true;
        raiz.childForceExpandHeight = false;

        CriarHeader();
        CriarListaEmojis();
        CriarRodape();
    }

    private void CriarHeader()
    {
        GameObject header = CriarObjetoUI("Header", painel.transform);
        LayoutElement le = header.AddComponent<LayoutElement>();
        le.preferredHeight = 72;
        le.minHeight = 72;

        Image bg = header.AddComponent<Image>();
        bg.color = new Color(0.020f, 0.080f, 0.110f, 0.92f);

        HorizontalLayoutGroup h = header.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(18, 12, 8, 8);
        h.spacing = 12;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;

        Text titulo = CriarTexto(header.transform, "⚜ TESTE DE EMOJIS DO MENU GOVERNO", 25, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
        titulo.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;

        Button fechar = CriarBotao(header.transform, "X", new Color(0.65f, 0.08f, 0.06f, 1f));
        fechar.onClick.AddListener(() =>
        {
            aberto = false;
            painel.SetActive(false);
        });
    }

    private void CriarListaEmojis()
    {
        GameObject scrollObj = CriarObjetoUI("Scroll", painel.transform);
        LayoutElement le = scrollObj.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 350;

        Image bg = scrollObj.AddComponent<Image>();
        bg.color = new Color(0.006f, 0.020f, 0.030f, 0.72f);

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 35f;

        GameObject viewport = CriarObjetoUI("Viewport", scrollObj.transform);
        viewport.AddComponent<RectMask2D>();
        Image vpImage = viewport.AddComponent<Image>();
        vpImage.color = Color.clear;

        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(12, 12);
        vpRt.offsetMax = new Vector2(-12, -12);

        GameObject content = CriarObjetoUI("Content", viewport.transform);
        RectTransform cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1);
        cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(2, colunas);
        grid.spacing = new Vector2(10, 10);
        grid.cellSize = new Vector2(210, 92);
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRt;
        scroll.content = cRt;

        for (int i = 0; i < emojisMenuGoverno.Length; i++)
        {
            CriarCardEmoji(content.transform, emojisMenuGoverno[i], i + 1);
        }
    }

    private void CriarCardEmoji(Transform parent, string emoji, int indice)
    {
        GameObject card = CriarObjetoUI("Emoji_" + indice + "_" + LimparNome(emoji), parent);

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.025f, 0.075f, 0.105f, 0.92f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.55f, 0.75f, 0.28f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(8, 8, 5, 5);
        v.spacing = 1;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        Text icone = CriarTexto(card.transform, emoji, tamanhoEmoji, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
        icone.gameObject.GetComponent<LayoutElement>().preferredHeight = 42;

        string nome = nomes.ContainsKey(emoji) ? nomes[emoji] : "Sem nome";
        Text label = CriarTexto(card.transform, nome, tamanhoTexto, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.85f, 0.95f, 1f, 1f));
        label.gameObject.GetComponent<LayoutElement>().preferredHeight = 22;

        Text codigo = CriarTexto(card.transform, "Texto: " + emoji, 11, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.55f, 0.68f, 0.75f, 1f));
        codigo.gameObject.GetComponent<LayoutElement>().preferredHeight = 18;
    }

    private void CriarRodape()
    {
        GameObject rodape = CriarObjetoUI("Rodape", painel.transform);
        LayoutElement le = rodape.AddComponent<LayoutElement>();
        le.preferredHeight = 48;
        le.minHeight = 48;

        Image bg = rodape.AddComponent<Image>();
        bg.color = new Color(0.010f, 0.040f, 0.055f, 0.92f);

        string aviso = "F9 abre/fecha. Se algum emoji aparecer como quadrado, a fonte atual não tem suporte para ele. Use uma fonte emoji no campo Fonte Emoji ou troque esses itens por sprites.";
        CriarTexto(rodape.transform, aviso, 14, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.72f, 0.86f, 0.94f, 1f));
    }

    private Button CriarBotao(Transform parent, string texto, Color cor)
    {
        GameObject go = CriarObjetoUI("Botao_" + texto, parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 56;
        le.minWidth = 56;

        Image img = go.AddComponent<Image>();
        img.color = cor;

        Button btn = go.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor = cor;
        cb.highlightedColor = Color.Lerp(cor, Color.white, 0.18f);
        cb.pressedColor = Color.Lerp(cor, Color.black, 0.22f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = cb;

        CriarTexto(go.transform, texto, 24, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
        return btn;
    }

    private Text CriarTexto(Transform parent, string texto, int tamanho, TextAnchor alinhamento, FontStyle estilo, Color cor)
    {
        GameObject go = CriarObjetoUI("Texto", parent);
        Text t = go.AddComponent<Text>();
        t.text = texto;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.fontStyle = estilo;
        t.color = cor;
        t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        if (fonteEmoji != null && ContemEmojiOuSimbolo(texto))
        {
            t.font = fonteEmoji;
        }
        else if (fonteTexto != null)
        {
            t.font = fonteTexto;
        }
        else
        {
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return t;
    }

    private GameObject CriarObjetoUI(string nome, Transform parent)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.localPosition = Vector3.zero;
        return go;
    }

    private bool ContemEmojiOuSimbolo(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return false;

        for (int i = 0; i < texto.Length; i++)
        {
            int code = char.ConvertToUtf32(texto, i);
            if (code > 255) return true;
            if (char.IsSurrogate(texto[i])) i++;
        }

        return false;
    }

    private string LimparNome(string entrada)
    {
        if (string.IsNullOrEmpty(entrada)) return "vazio";
        string nome = entrada.Replace("<", "tag_abre").Replace(">", "tag_fecha").Replace("/", "barra");
        nome = nome.Replace(" ", "_");
        return nome;
    }

    private void FecharImediato()
    {
        aberto = false;
        if (painel != null) painel.SetActive(false);
    }
}
