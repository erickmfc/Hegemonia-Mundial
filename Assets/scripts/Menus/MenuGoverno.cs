using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MenuGoverno : MonoBehaviour
{
    [Header("Atalho")]
    public KeyCode teclaAtalho = KeyCode.X;

    [Header("Layout 16:9")]
    [Range(0.40f, 0.95f)] public float larguraTela = 0.90f;
    [Range(0.40f, 0.98f)] public float alturaTela = 0.91f;
    [Range(-0.25f, 0.25f)] public float deslocamentoVertical = 0.025f;

    [Header("Medidas Fixas")]
    public float larguraSidebar = 214f;
    public float larguraPainelDireito = 360f;
    public float alturaCabecalho = 78f;
    public float alturaRecursos = 56f;
    public float alturaSubAbas = 40f;
    public float alturaRodape = 148f;
    public float alturaDica = 30f;
    public float espacamento = 9f;

    [Header("Animação")]
    public float duracaoAnimacaoMenu = 0.18f;
    public float duracaoAnimacaoAba = 0.11f;

    [Header("Fontes / Emojis")]
    public Font fonteEmoji;
    public Font fonteTexto;
    public bool usarFonteEmoji = true;
    public bool mostrarAvisoEmojiNoRodape = true;

    [Header("Cores - Hegemonia Global")]
    public Color corFundoJanela = new Color(0.010f, 0.026f, 0.038f, 0.985f);
    public Color corFundoEscuro = new Color(0.006f, 0.018f, 0.027f, 0.98f);
    public Color corPainel = new Color(0.026f, 0.078f, 0.105f, 0.97f);
    public Color corPainel2 = new Color(0.040f, 0.105f, 0.138f, 0.96f);
    public Color corCard = new Color(0.055f, 0.125f, 0.160f, 0.95f);
    public Color corCardClara = new Color(0.078f, 0.155f, 0.195f, 0.94f);
    public Color corLinha = new Color(0.000f, 0.720f, 1.000f, 0.70f);
    public Color corLinhaFraca = new Color(0.340f, 0.640f, 0.780f, 0.34f);
    public Color corDestaque = new Color(0.000f, 0.730f, 1.000f, 1.00f);
    public Color corAzulBotao = new Color(0.020f, 0.270f, 0.455f, 1.00f);
    public Color corAbaAtiva = new Color(0.000f, 0.345f, 0.560f, 0.98f);
    public Color corVerde = new Color(0.330f, 0.950f, 0.250f, 1.00f);
    public Color corAmarelo = new Color(1.000f, 0.650f, 0.100f, 1.00f);
    public Color corLaranja = new Color(1.000f, 0.330f, 0.080f, 1.00f);
    public Color corVermelho = new Color(0.950f, 0.120f, 0.080f, 1.00f);
    public Color corRoxo = new Color(0.700f, 0.360f, 1.000f, 1.00f);
    public Color corTextoPrimario = new Color(0.915f, 0.970f, 1.000f, 1.00f);
    public Color corTextoSecundario = new Color(0.720f, 0.835f, 0.910f, 1.00f);
    public Color corTextoApagado = new Color(0.560f, 0.660f, 0.740f, 1.00f);

    public static MenuGoverno Instancia;
    public static bool EstaAberto;

    private static Font fontePadraoCache;

    private GameObject canvasObj;
    private GameObject painelPrincipal;
    private CanvasGroup canvasGroupPainel;

    private Transform headerRoot;
    private Transform barraRecursos;
    private Transform sidebarRoot;
    private Transform subAbasRoot;
    private Transform conteudoCentral;
    private Transform painelDireito;
    private Transform rodapeEsquerdo;
    private Transform rodapeMeio;
    private Transform rodapeDireito;
    private Transform barraDica;

    private Coroutine animacaoMenuAtual;
    private Coroutine animacaoConteudoAtual;
    private bool menuAberto;

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
        Trabalho
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
        public int militaresDisponiveis = 0;
        public int militaresAtivos = 3250;
        public int dinheiro = 39534;
        public int comida = 500;
        public int petroleo = 3830;
        public int aco = 100;
        public int armamentos = 500;
        public int uranio = 0;
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

    [Header("Sistema")]
    public int paisJogadorId = 1;
    public float intervaloTickGoverno = 5f;

    public List<PaisGoverno> paises = new List<PaisGoverno>();
    public List<RelacaoDiplomatica> relacoes = new List<RelacaoDiplomatica>();
    public List<NotificacaoGoverno> notificacoes = new List<NotificacaoGoverno>();

    private float proximoTickGoverno;
    private CategoriaGoverno abaAtual = CategoriaGoverno.RelacoesExteriores;
    private int subAbaAtualIndex = 0;
    private int paisSelecionadoId = 2;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;

        MenuGoverno existente = Achar<MenuGoverno>();
        if (existente != null)
        {
            Instancia = existente;
            return;
        }

        GameObject go = new GameObject("MenuGoverno_Runtime");
        Instancia = go.AddComponent<MenuGoverno>();
        DontDestroyOnLoad(go);
    }

    private static T Achar<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<T>();
#else
        return FindObjectOfType<T>();
#endif
    }

    private static Font ObterFontePadrao()
    {
        if (fontePadraoCache != null) return fontePadraoCache;

        fontePadraoCache = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (fontePadraoCache == null)
            fontePadraoCache = Font.CreateDynamicFontFromOSFont("Arial", 14);

        if (fontePadraoCache == null)
            fontePadraoCache = Font.CreateDynamicFontFromOSFont("Liberation Sans", 14);

        return fontePadraoCache;
    }

    private Font ObterFonteParaTexto(string texto)
    {
        if (usarFonteEmoji && fonteEmoji != null && ContemEmojiOuSimbolo(texto))
            return fonteEmoji;

        if (fonteTexto != null)
            return fonteTexto;

        return ObterFontePadrao();
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

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        EstaAberto = false;
        DontDestroyOnLoad(gameObject);

        InicializarDadosDoMundo();
        GarantirCanvasEEventSystem();
        GerarInterfaceCompleta();
        FecharImediato();
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAtalho)) AlternarMenu(!EstaAberto);

        if (EstaAberto && Time.unscaledTime >= proximoTickGoverno)
        {
            proximoTickGoverno = Time.unscaledTime + 1.0f;
            
            // Verifica se os objetos ainda existem antes de atualizar
            if (painelPrincipal != null)
            {
                AtualizarBarraRecursos(); // Atualiza os números no topo
                GerarPainelDireito();      // Atualiza o painel da lateral direita
            }
            else
            {
                // Se o menu deveria estar aberto mas o painel sumiu, força regeneração
                GerarInterfaceCompleta();
            }
        }
    }

    public void AlternarMenu(bool abrir)
    {
        // Se já estiver no estado desejado, não faz nada
        if (abrir == EstaAberto) return;

        if (abrir)
        {
            GerarInterfaceCompleta();
            if (animacaoMenuAtual != null) StopCoroutine(animacaoMenuAtual);
            animacaoMenuAtual = StartCoroutine(AnimarMenu(true));
            EsconderHUD(true);
        }
        else
        {
            if (animacaoMenuAtual != null) StopCoroutine(animacaoMenuAtual);
            animacaoMenuAtual = StartCoroutine(AnimarMenu(false));
            EsconderHUD(false);
        }
    }

    private void EsconderHUD(bool esconder)
    {
        // MiniMapa
        MiniMapa mm = UnityEngine.Object.FindFirstObjectByType<MiniMapa>();
        if (mm != null)
        {
            Transform canvasMM = mm.transform.root.Find("Canvas_MiniMapa");
            if (canvasMM != null) canvasMM.gameObject.SetActive(!esconder);
            mm.gameObject.SetActive(!esconder);
        }

        // Menu de Construção / Lateral
        MenuConstrucao mc = UnityEngine.Object.FindFirstObjectByType<MenuConstrucao>();
        if (mc != null)
        {
            if (esconder)
            {
                if (MenuConstrucao.EstaAberto) mc.AlternarMenu(false);
                mc.gameObject.SetActive(false);
            }
            else
            {
                mc.gameObject.SetActive(true);
            }
        }
    }

    private IEnumerator AnimarMenu(bool abrir)
    {
        menuAberto = abrir;
        EstaAberto = abrir;

        if (painelPrincipal == null) GerarInterfaceCompleta();
        if (painelPrincipal == null) yield break; // Falha ao criar

        painelPrincipal.SetActive(true);
        if (abrir) AtualizarInterfaceCompleta();

        canvasGroupPainel.interactable = abrir;
        canvasGroupPainel.blocksRaycasts = abrir;

        float inicioAlpha = canvasGroupPainel.alpha;
        float fimAlpha = abrir ? 1f : 0f;
        Vector3 inicioScale = abrir ? new Vector3(0.965f, 0.965f, 1f) : painelPrincipal.transform.localScale;
        Vector3 fimScale = abrir ? Vector3.one : new Vector3(0.965f, 0.965f, 1f);

        if (abrir) painelPrincipal.transform.localScale = inicioScale;

        float t = 0f;
        while (t < duracaoAnimacaoMenu)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, duracaoAnimacaoMenu));
            p = 1f - Mathf.Pow(1f - p, 3f);
            canvasGroupPainel.alpha = Mathf.Lerp(inicioAlpha, fimAlpha, p);
            painelPrincipal.transform.localScale = Vector3.Lerp(inicioScale, fimScale, p);
            yield return null;
        }

        canvasGroupPainel.alpha = fimAlpha;
        painelPrincipal.transform.localScale = fimScale;

        if (!abrir)
        {
            painelPrincipal.SetActive(false);
            canvasGroupPainel.interactable = false;
            canvasGroupPainel.blocksRaycasts = false;
        }
    }

    private void FecharImediato()
    {
        menuAberto = false;
        EstaAberto = false;
        if (painelPrincipal != null) painelPrincipal.SetActive(false);
        if (canvasGroupPainel != null)
        {
            canvasGroupPainel.alpha = 0f;
            canvasGroupPainel.interactable = false;
            canvasGroupPainel.blocksRaycasts = false;
        }
    }

    private void GarantirCanvasEEventSystem()
    {
        Canvas canvasExistente = Achar<Canvas>();
        if (canvasExistente != null)
        {
            canvasObj = canvasExistente.gameObject;
            canvasExistente.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasExistente.sortingOrder = Mathf.Max(canvasExistente.sortingOrder, 6000);

            if (canvasObj.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scalerExistente = canvasObj.AddComponent<CanvasScaler>();
                scalerExistente.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scalerExistente.referenceResolution = new Vector2(1920, 1080);
                scalerExistente.matchWidthOrHeight = 0.5f;
            }

            if (canvasObj.GetComponent<GraphicRaycaster>() == null) canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvasObj = new GameObject("Canvas_Interface_Hegemonia");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        if (Achar<EventSystem>() == null)
        {
            GameObject eventObj = new GameObject("EventSystem_Auto");
            eventObj.AddComponent<EventSystem>();
            eventObj.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventObj);
        }
    }

    private void GerarInterfaceCompleta()
    {
        // Garante que o Canvas existe antes de prosseguir
        if (canvasObj == null) GarantirCanvasEEventSystem();

        // Se ainda for nulo (falha catastrófica), aborta
        if (canvasObj == null) return;

        Transform antigo = canvasObj.transform.Find("Painel_Governo_Hegemonia_Refeito");
        if (antigo != null) Destroy(antigo.gameObject);

        painelPrincipal = CriarUIObjeto("Painel_Governo_Hegemonia_Refeito", canvasObj.transform);
        RectTransform rt = painelPrincipal.GetComponent<RectTransform>();
        float meiaLargura = Mathf.Clamp01(larguraTela) * 0.5f;
        float meiaAltura = Mathf.Clamp01(alturaTela) * 0.5f;
        rt.anchorMin = new Vector2(0.5f - meiaLargura, 0.5f - meiaAltura + deslocamentoVertical);
        rt.anchorMax = new Vector2(0.5f + meiaLargura, 0.5f + meiaAltura + deslocamentoVertical);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = painelPrincipal.AddComponent<Image>();
        bg.color = corFundoJanela;
        AddOutline(painelPrincipal, corLinha, 2f);
        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();

        VerticalLayoutGroup raiz = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        raiz.spacing = 0;
        raiz.padding = new RectOffset(0, 0, 0, 0);
        raiz.childControlWidth = true;
        raiz.childControlHeight = true;
        raiz.childForceExpandWidth = true;
        raiz.childForceExpandHeight = false;

        CriarCabecalho(painelPrincipal.transform);
        CriarBarraRecursosSuperior(painelPrincipal.transform);
        CriarCorpoPrincipal(painelPrincipal.transform);
        CriarRodapePrincipal(painelPrincipal.transform);
        CriarBarraInferiorDica(painelPrincipal.transform);

        AtualizarInterfaceCompleta();
    }

    private void CriarCabecalho(Transform parent)
    {
        GameObject topo = CriarCardBase(parent, alturaCabecalho, new Color(0.007f, 0.033f, 0.050f, 0.985f));
        topo.name = "Header";
        headerRoot = topo.transform;
        AddOutline(topo, corLinhaFraca, 1f);

        HorizontalLayoutGroup h = topo.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 8, 8);
        h.spacing = 12;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        GameObject identidade = CriarCardBase(topo.transform, 0, new Color(0.025f, 0.078f, 0.110f, 0.86f));
        identidade.name = "IdentidadeNacional";
        identidade.GetComponent<LayoutElement>().preferredWidth = 324f;
        identidade.GetComponent<LayoutElement>().flexibleWidth = 0f;

        HorizontalLayoutGroup idH = identidade.AddComponent<HorizontalLayoutGroup>();
        idH.padding = new RectOffset(10, 10, 8, 8);
        idH.spacing = 10;
        idH.childControlHeight = true;

        GameObject bandeira = CriarCardBase(identidade.transform, 0, new Color(0.018f, 0.315f, 0.580f, 1f));
        bandeira.GetComponent<LayoutElement>().preferredWidth = 78f;
        CriarTextoLivre(bandeira.transform, "✦  ✦\n✦ ✦ ✦", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        GameObject textos = CriarUIObjeto("TextosPais", identidade.transform);
        textos.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup v = textos.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleLeft;
        v.spacing = 1;
        CriarTextoLayout(textos.transform, ObterPais(paisJogadorId).nome.ToUpper(), 17, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 26);
        CriarTextoLayout(textos.transform, "ID 01  |  Governo Nacional", 11, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Normal, 20);

        GameObject titulo = CriarUIObjeto("TituloCentral", topo.transform);
        titulo.AddComponent<LayoutElement>().flexibleWidth = 1f;
        CriarTextoLivre(titulo.transform, "GABINETE GOVERNAMENTAL — HEGEMONIA GLOBAL", 25, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold);
        GameObject subtitulo = CriarUIObjeto("Subtitulo", titulo.transform);
        RectTransform subRt = subtitulo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.35f);
        subRt.offsetMin = new Vector2(0, 0);
        subRt.offsetMax = new Vector2(0, 0);
        CriarTextoLivre(subtitulo.transform, "diplomacia • economia • defesa • ciência • trabalho", 11, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Bold);

        Button fechar = CriarBotao(topo.transform, "X", new Color(0.245f, 0.030f, 0.030f, 1f), () => AlternarMenu(false));
        LayoutElement fecharLe = fechar.gameObject.GetComponent<LayoutElement>();
        fecharLe.preferredWidth = 54f;
        fecharLe.minWidth = 54f;
    }

    private void CriarBarraRecursosSuperior(Transform parent)
    {
        GameObject barra = CriarCardBase(parent, alturaRecursos, new Color(0.006f, 0.022f, 0.032f, 0.98f));
        barra.name = "BarraRecursos";
        barraRecursos = barra.transform;
        AddOutline(barra, corLinhaFraca, 1f);

        HorizontalLayoutGroup h = barra.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 6, 6);
        h.spacing = 5;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
    }

    private void CriarCorpoPrincipal(Transform parent)
    {
        GameObject corpo = CriarUIObjeto("Corpo", parent);
        LayoutElement le = corpo.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight = 440f;

        HorizontalLayoutGroup h = corpo.AddComponent<HorizontalLayoutGroup>();
        h.spacing = espacamento;
        h.padding = new RectOffset(12, 12, 10, 8);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.childForceExpandWidth = false;

        CriarSidebar(corpo.transform);
        CriarAreaConteudo(corpo.transform);
    }

    private void CriarSidebar(Transform parent)
    {
        GameObject side = CriarCardBase(parent, 0, new Color(0.014f, 0.046f, 0.066f, 0.96f));
        side.name = "Sidebar";
        sidebarRoot = side.transform;
        LayoutElement le = side.GetComponent<LayoutElement>();
        le.preferredWidth = larguraSidebar;
        le.minWidth = larguraSidebar;
        le.flexibleWidth = 0f;
        AddOutline(side, corLinhaFraca, 1f);

        VerticalLayoutGroup v = side.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 0);
        v.spacing = 0;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        foreach (CategoriaGoverno cat in Enum.GetValues(typeof(CategoriaGoverno))) CriarBotaoAbaLateral(cat);
    }

    private void CriarBotaoAbaLateral(CategoriaGoverno cat)
    {
        GameObject btn = CriarUIObjeto("Aba_" + cat, sidebarRoot);
        LayoutElement le = btn.AddComponent<LayoutElement>();
        le.preferredHeight = 48f;
        le.minHeight = 48f;

        Image img = btn.AddComponent<Image>();
        img.color = Color.clear;
        AddOutline(btn, corLinhaFraca, 1f);

        GameObject brilho = CriarUIObjeto("Brilho", btn.transform);
        Esticar(brilho.GetComponent<RectTransform>(), 0, 0, 0, 0);
        Image brilhoImg = brilho.AddComponent<Image>();
        brilhoImg.color = Color.clear;
        brilhoImg.raycastTarget = false;

        GameObject linha = CriarUIObjeto("LinhaAtiva", btn.transform);
        RectTransform lRt = linha.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0, 0);
        lRt.anchorMax = new Vector2(0, 1);
        lRt.offsetMin = new Vector2(0, 0);
        lRt.offsetMax = new Vector2(5, 0);
        Image linhaImg = linha.AddComponent<Image>();
        linhaImg.color = Color.clear;
        linhaImg.raycastTarget = false;

        HorizontalLayoutGroup h = btn.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(15, 8, 0, 0);
        h.spacing = 10;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.childControlWidth = true;

        Text icone = CriarTextoLayout(btn.transform, IconeAba(cat), 22, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Bold, 48);
        icone.name = "Icone";
        icone.GetComponent<LayoutElement>().preferredWidth = 32f;

        Text titulo = CriarTextoLayout(btn.transform, NomeAba(cat).ToUpper(), 13, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 48);
        titulo.name = "Titulo";
        titulo.GetComponent<LayoutElement>().flexibleWidth = 1f;

        Button b = btn.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() =>
        {
            if (abaAtual == cat) return;
            abaAtual = cat;
            subAbaAtualIndex = 0;
            AtualizarInterfaceCompleta();
        });
    }

    private void CriarAreaConteudo(Transform parent)
    {
        GameObject area = CriarUIObjeto("AreaConteudo", parent);
        area.AddComponent<LayoutElement>().flexibleWidth = 1f;

        VerticalLayoutGroup v = area.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        GameObject sub = CriarCardBase(area.transform, alturaSubAbas, new Color(0.010f, 0.034f, 0.048f, 0.96f));
        sub.name = "SubAbas";
        subAbasRoot = sub.transform;
        AddOutline(sub, corLinhaFraca, 1f);
        HorizontalLayoutGroup subH = sub.AddComponent<HorizontalLayoutGroup>();
        subH.padding = new RectOffset(9, 9, 7, 7);
        subH.spacing = 7;
        subH.childControlWidth = false;
        subH.childControlHeight = true;
        subH.childForceExpandWidth = false;

        GameObject split = CriarUIObjeto("SplitConteudo", area.transform);
        LayoutElement spLe = split.AddComponent<LayoutElement>();
        spLe.flexibleHeight = 1f;
        spLe.minHeight = 260f;
        HorizontalLayoutGroup spH = split.AddComponent<HorizontalLayoutGroup>();
        spH.spacing = espacamento;
        spH.childControlWidth = true;
        spH.childControlHeight = true;
        spH.childForceExpandHeight = true;
        spH.childForceExpandWidth = false;

        CriarScrollPanel(split.transform, "CentroScroll", new Color(0.008f, 0.030f, 0.043f, 0.91f), 0f, 0f, 1f, out conteudoCentral);
        CriarScrollPanel(split.transform, "PainelDireitoScroll", new Color(0.014f, 0.050f, 0.070f, 0.965f), larguraPainelDireito, larguraPainelDireito, 0f, out painelDireito);
    }

    private void CriarRodapePrincipal(Transform parent)
    {
        GameObject rodape = CriarUIObjeto("Rodape", parent);
        LayoutElement le = rodape.AddComponent<LayoutElement>();
        le.preferredHeight = alturaRodape;
        le.minHeight = alturaRodape;

        HorizontalLayoutGroup h = rodape.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 0, 0);
        h.spacing = espacamento;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;

        rodapeEsquerdo = CriarCardBase(rodape.transform, 0, corPainel).transform;
        rodapeMeio = CriarCardBase(rodape.transform, 0, corPainel).transform;
        rodapeDireito = CriarCardBase(rodape.transform, 0, corPainel).transform;

        rodapeEsquerdo.GetComponent<LayoutElement>().flexibleWidth = 1.05f;
        rodapeMeio.GetComponent<LayoutElement>().flexibleWidth = 1.10f;
        rodapeDireito.GetComponent<LayoutElement>().flexibleWidth = 1.35f;

        AddVertical(rodapeEsquerdo.gameObject, 10, 8, 7);
        AddVertical(rodapeMeio.gameObject, 10, 8, 7);
        AddVertical(rodapeDireito.gameObject, 10, 8, 7);
    }

    private void CriarBarraInferiorDica(Transform parent)
    {
        GameObject barra = CriarCardBase(parent, alturaDica, new Color(0.006f, 0.025f, 0.035f, 0.98f));
        barra.name = "BarraDica";
        barraDica = barra.transform;
        HorizontalLayoutGroup h = barra.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 0, 0);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
    }

    private void CriarScrollPanel(Transform parent, string nome, Color cor, float preferredWidth, float minWidth, float flexWidth, out Transform contentTransform)
    {
        GameObject box = CriarCardBase(parent, 0, cor);
        box.name = nome;
        LayoutElement le = box.GetComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.minWidth = minWidth;
        le.flexibleWidth = flexWidth;
        AddOutline(box, corLinhaFraca, 1f);

        ScrollRect scroll = box.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 34f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CriarUIObjeto("Viewport", box.transform);
        Esticar(viewport.GetComponent<RectTransform>(), 10, 10, 10, 10);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.clear;
        viewport.AddComponent<RectMask2D>();

        GameObject content = CriarUIObjeto("Content", viewport.transform);
        RectTransform cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1);
        cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup cv = content.AddComponent<VerticalLayoutGroup>();
        cv.padding = new RectOffset(0, 0, 0, 0);
        cv.spacing = 9;
        cv.childControlWidth = true;
        cv.childControlHeight = true;
        cv.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = cRt;
        contentTransform = content.transform;
    }

    private void AtualizarInterfaceCompleta()
    {
        AtualizarSidebar();
        AtualizarBarraRecursos();
        GerarSubAbas();
        AtualizarConteudoComAnimacao();
        AtualizarRodape();
        AtualizarDica();
    }

    private void AtualizarConteudoComAnimacao()
    {
        if (animacaoConteudoAtual != null) StopCoroutine(animacaoConteudoAtual);
        animacaoConteudoAtual = StartCoroutine(AnimarTrocaConteudo());
    }

    private IEnumerator AnimarTrocaConteudo()
    {
        CanvasGroup cgCentro = GarantirCanvasGroup(conteudoCentral.gameObject);
        CanvasGroup cgDireito = GarantirCanvasGroup(painelDireito.gameObject);

        cgCentro.alpha = 0f;
        cgDireito.alpha = 0f;
        conteudoCentral.localPosition = new Vector3(0, -8, 0);
        painelDireito.localPosition = new Vector3(8, 0, 0);

        GerarConteudoCentral();
        GerarPainelDireito();

        float t = 0f;
        while (t < duracaoAnimacaoAba)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, duracaoAnimacaoAba));
            p = 1f - Mathf.Pow(1f - p, 2f);

            if (cgCentro == null || cgDireito == null || conteudoCentral == null || painelDireito == null) yield break;

            cgCentro.alpha = p;
            cgDireito.alpha = p;
            conteudoCentral.localPosition = Vector3.Lerp(new Vector3(0, -8, 0), Vector3.zero, p);
            painelDireito.localPosition = Vector3.Lerp(new Vector3(8, 0, 0), Vector3.zero, p);
            yield return null;
        }

        if (cgCentro != null) cgCentro.alpha = 1f;
        if (cgDireito != null) cgDireito.alpha = 1f;
        if (conteudoCentral != null) conteudoCentral.localPosition = Vector3.zero;
        if (painelDireito != null) painelDireito.localPosition = Vector3.zero;
    }

    private void AtualizarSidebar()
    {
        foreach (Transform child in sidebarRoot)
        {
            if (!child.name.StartsWith("Aba_")) continue;
            bool ativo = child.name == "Aba_" + abaAtual;
            Image bg = child.GetComponent<Image>();
            Transform brilho = child.Find("Brilho");
            Transform linha = child.Find("LinhaAtiva");
            Transform icone = child.Find("Icone");
            Transform titulo = child.Find("Titulo");

            if (bg != null) bg.color = ativo ? corAbaAtiva : new Color(0.026f, 0.078f, 0.104f, 0.82f);
            if (brilho != null) brilho.GetComponent<Image>().color = ativo ? new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.20f) : new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.035f);
            if (linha != null) linha.GetComponent<Image>().color = ativo ? corDestaque : new Color(corLinha.r, corLinha.g, corLinha.b, 0.16f);
            if (icone != null) icone.GetComponent<Text>().color = ativo ? Color.white : new Color(0.760f, 0.900f, 0.980f, 1f);
            if (titulo != null) titulo.GetComponent<Text>().color = ativo ? Color.white : new Color(0.760f, 0.870f, 0.940f, 1f);
        }
    }

    private void AtualizarBarraRecursos()
    {
        if (barraRecursos == null || !EstaAberto) return;
        LimparFilhos(barraRecursos);

        PaisGoverno p = ObterPais(paisJogadorId);
        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;

        if (gr != null && p != null)
        {
            // Sincroniza dados do objeto PaisGoverno com os dados REAIS do sistema de recursos
            p.dinheiro = gr.dinheiro;
            p.petroleo = gr.petroleo;
            p.aco = gr.aco;
            p.populacaoAtual = gr.populacaoAtual;
            p.populacaoMaximaPorCasas = gr.populacaoMaxima;

            CriarRecursoTopo("▣", "DINHEIRO", "$" + FormatNumero(gr.dinheiro), (gr.dinheiroPorSegundo >= 0 ? "+" : "") + (int)gr.dinheiroPorSegundo + "/s", gr.dinheiroPorSegundo >= 0 ? corVerde : corVermelho);
            CriarRecursoTopo("≋", "COMIDA", p.comida.ToString(), "+5/s", corVerde);
            CriarRecursoTopo("💧", "PETRÓLEO", FormatNumero(gr.petroleo), (gr.petroleoPorSegundo >= 0 ? "+" : "") + (int)gr.petroleoPorSegundo + "/s", gr.petroleoPorSegundo >= 0 ? corVerde : corVermelho);
            CriarRecursoTopo("▰", "AÇO", FormatNumero(gr.aco), (gr.acoPorSegundo >= 0 ? "+" : "") + (int)gr.acoPorSegundo + "/s", gr.acoPorSegundo >= 0 ? corVerde : corVermelho);
            CriarRecursoTopo("▥", "ARMAMENTOS", p.armamentos.ToString(), "+3/s", corVerde);
            CriarRecursoTopo("♟", "POPULAÇÃO", gr.populacaoAtual + " / " + gr.populacaoMaxima, "", corTextoSecundario);
            CriarRecursoTopo("⚔", "MILITARES", FormatNumero(p.militaresAtivos), "", corTextoSecundario);
            CriarRecursoTopo("◈", "STATUS", NomeStatus(p.status).ToUpper(), "", CorStatus(p.status));
        }
        else if (p != null)
        {
            CriarRecursoTopo("▣", "DINHEIRO", "$" + FormatNumero(p.dinheiro), "+60/s", corVerde);
            CriarRecursoTopo("≋", "COMIDA", p.comida.ToString(), "+5/s", corVerde);
            CriarRecursoTopo("💧", "PETRÓLEO", FormatNumero(p.petroleo), "+5/s", corVerde);
            CriarRecursoTopo("▰", "AÇO", p.aco.ToString(), "+2/s", corVerde);
            CriarRecursoTopo("▥", "ARMAMENTOS", p.armamentos.ToString(), "+3/s", corVerde);
            CriarRecursoTopo("♟", "POPULAÇÃO", p.populacaoAtual + " / " + p.populacaoMaximaPorCasas, "", corTextoSecundario);
            CriarRecursoTopo("⚔", "MILITARES", FormatNumero(p.militaresAtivos), "", corTextoSecundario);
            CriarRecursoTopo("◈", "STATUS", NomeStatus(p.status).ToUpper(), "", CorStatus(p.status));
        }
    }

    private void CriarRecursoTopo(string icone, string nome, string valor, string ganho, Color corGanho)
    {
        GameObject box = CriarCardBase(barraRecursos, 0, new Color(0.018f, 0.055f, 0.074f, 0.82f));
        box.GetComponent<LayoutElement>().flexibleWidth = 1f;
        HorizontalLayoutGroup h = box.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 3, 3);
        h.spacing = 6;
        h.childControlHeight = true;
        h.childControlWidth = true;

        Text ic = CriarTextoLayout(box.transform, icone, 20, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 46);
        ic.GetComponent<LayoutElement>().preferredWidth = 24f;

        GameObject textos = CriarUIObjeto("Textos", box.transform);
        textos.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup v = textos.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleLeft;
        v.spacing = -1;
        CriarTextoLayout(textos.transform, nome, 8, corTextoSecundario, TextAnchor.LowerLeft, FontStyle.Bold, 18);

        GameObject linhaValor = CriarUIObjeto("Valor", textos.transform);
        linhaValor.AddComponent<LayoutElement>().preferredHeight = 22f;
        HorizontalLayoutGroup hv = linhaValor.AddComponent<HorizontalLayoutGroup>();
        hv.spacing = 4;
        hv.childControlWidth = true;
        hv.childForceExpandWidth = false;
        CriarTextoLayout(linhaValor.transform, valor, 14, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 22);
        if (!string.IsNullOrEmpty(ganho)) CriarTextoLayout(linhaValor.transform, "(" + ganho + ")", 9, corGanho, TextAnchor.MiddleLeft, FontStyle.Bold, 22);
    }

    private void GerarSubAbas()
    {
        LimparFilhos(subAbasRoot);
        List<string> abas = ObterSubAbas(abaAtual);

        for (int i = 0; i < abas.Count; i++)
        {
            int index = i;
            bool ativo = index == subAbaAtualIndex;
            GameObject btn = CriarBotaoBloco(subAbasRoot, abas[i], ativo ? corAbaAtiva : new Color(0.035f, 0.075f, 0.100f, 0.92f), () =>
            {
                subAbaAtualIndex = index;
                AtualizarInterfaceCompleta();
            });

            LayoutElement le = btn.GetComponent<LayoutElement>();
            le.preferredWidth = Mathf.Clamp(abas[i].Length * 11 + 38, 104, 190);
            le.minWidth = 96;
            le.flexibleWidth = 0f;
            le.preferredHeight = 30f;
            le.minHeight = 28f;

            Transform t = btn.transform.Find("TextoBotao");
            if (t != null) t.GetComponent<Text>().color = ativo ? Color.white : corTextoSecundario;

            GameObject linha = CriarUIObjeto("LinhaInferior", btn.transform);
            RectTransform rt = linha.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, 3);
            Image img = linha.AddComponent<Image>();
            img.color = ativo ? corDestaque : Color.clear;
            img.raycastTarget = false;
        }
    }

    private void GerarConteudoCentral()
    {
        LimparFilhos(conteudoCentral);

        switch (abaAtual)
        {
            case CategoriaGoverno.RelacoesExteriores: ExibirRelacoesExteriores(); break;
            case CategoriaGoverno.Aliancas: ExibirAliancas(); break;
            case CategoriaGoverno.Sancoes: ExibirSancoes(); break;
            case CategoriaGoverno.Economia: ExibirEconomia(); break;
            case CategoriaGoverno.MercadoGlobal: ExibirMercadoGlobal(); break;
            case CategoriaGoverno.Interior: ExibirInterior(); break;
            case CategoriaGoverno.Defesa: ExibirDefesa(); break;
            case CategoriaGoverno.Ciencia: ExibirCiencia(); break;
            case CategoriaGoverno.Trabalho: ExibirTrabalho(); break;
        }
    }

    private void GerarPainelDireito()
    {
        LimparFilhos(painelDireito);

        switch (abaAtual)
        {
            case CategoriaGoverno.Sancoes: PainelNovaSancao(); break;
            case CategoriaGoverno.MercadoGlobal: PainelMercado(); break;
            case CategoriaGoverno.Interior: PainelInterior(); break;
            case CategoriaGoverno.Defesa: PainelDefesa(); break;
            case CategoriaGoverno.Ciencia: PainelCiencia(); break;
            case CategoriaGoverno.Trabalho: PainelTrabalho(); break;
            case CategoriaGoverno.Economia: PainelEconomia(); break;
            case CategoriaGoverno.Aliancas: PainelAliancas(); break;
            default: PainelFichaDiplomatica(); break;
        }
    }

    private void ExibirRelacoesExteriores()
    {
        PaisGoverno jogador = ObterPais(paisJogadorId);
        CriarTituloSecao("RESUMO DO ESTADO NACIONAL", conteudoCentral);

        GameObject grid = CriarLinha(conteudoCentral, 120, 10);
        CriarCardBrasao(grid.transform, jogador.nome.ToUpper(), "⚜");
        CriarMetricCard(grid.transform, "♟", "POPULAÇÃO", jogador.populacaoAtual + " / " + jogador.populacaoMaximaPorCasas, "Capacidade nacional", corDestaque, 0.55f);
        CriarMetricCard(grid.transform, "⌂", "CASAS", jogador.casas + " / 10", "Cap. por casa: " + jogador.capacidadePorCasa, corDestaque, 0.40f);
        CriarMetricCard(grid.transform, "▥", "QUARTÉIS", jogador.quarteis + " / 10", "Reserva militar", corDestaque, 0.20f);

        GameObject status = CriarLinha(conteudoCentral, 74, 10);
        CriarMiniStatus(status.transform, "◈", "STATUS GEOPOLÍTICO", NomeStatus(jogador.status), CorStatus(jogador.status));
        CriarMiniStatus(status.transform, "⚑", "BLOCO ATUAL", NomeBloco(jogador.bloco), corDestaque);
        CriarMiniStatus(status.transform, "🤝", "ALIADO PRIORITÁRIO", NomePais(jogador.aliadoPrioritarioId), corDestaque);
        CriarMiniStatus(status.transform, "⌖", "RIVAL ESTRATÉGICO", NomePais(jogador.rivalEstrategicoId), corVermelho);

        CriarTituloSecao("RELAÇÕES DIPLOMÁTICAS", conteudoCentral);
        foreach (PaisGoverno p in paises.Where(x => x.id != paisJogadorId)) CriarCardPaisDiplomacia(conteudoCentral, p, ObterRelacao(paisJogadorId, p.id));
    }

    private void ExibirAliancas()
    {
        CriarTituloSecao("ALIANÇAS E BLOCOS MILITARES", conteudoCentral);
        CriarDescricao("Gerencie acordos estratégicos, coalizões militares e tratados de proteção entre nações.");

        GameObject grid = CriarLinha(conteudoCentral, 96, 10);
        CriarResumoCard(grid.transform, "ALIADOS ATIVOS", "2", "Nações com pacto", corVerde);
        CriarResumoCard(grid.transform, "FORÇA DO BLOCO", "7.850", "Poder combinado", corDestaque);
        CriarResumoCard(grid.transform, "CONFIANÇA", "82%", "Estabilidade", corVerde);
        CriarResumoCard(grid.transform, "RISCO DE TRAIÇÃO", "Baixo", "Acordos estáveis", corAmarelo);

        CriarTituloSecao("MEMBROS DO BLOCO ORDEM ATLAS", conteudoCentral);
        GameObject table = CriarTabela(conteudoCentral, new string[] { "PAÍS", "RELAÇÃO", "CONTRIBUIÇÃO", "PACTO", "AÇÃO" }, 40);
        CriarLinhaTabela(table.transform, new string[] { "República Boreal", "+75", "Naval / Petróleo", "Ativo", "Gerenciar" }, new Color[] { corTextoPrimario, corVerde, corDestaque, corVerde, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "Federação Alvorada", "+58", "Comida / Indústria", "Ativo", "Gerenciar" }, new Color[] { corTextoPrimario, corVerde, corAmarelo, corVerde, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "Confederação Oriental", "+21", "Tecnologia", "Pendente", "Convidar" }, new Color[] { corTextoPrimario, corAmarelo, corRoxo, corAmarelo, corDestaque });

        CriarTituloSecao("AÇÕES DE ALIANÇA", conteudoCentral);
        GameObject acoes = CriarLinha(conteudoCentral, 100, 10);
        CriarAcaoCard(acoes.transform, "🤝", "PROPOR ALIANÇA", "$2.000", corDestaque);
        CriarAcaoCard(acoes.transform, "🛡", "PACTO DEFENSIVO", "$4.000", corVerde);
        CriarAcaoCard(acoes.transform, "⚔", "OPERAÇÃO CONJUNTA", "$8.000", corAmarelo);
        CriarAcaoCard(acoes.transform, "📡", "COMPARTILHAR INTEL", "$3.000", corRoxo);
    }

    private void ExibirSancoes()
    {
        CriarTituloSecao("VISÃO GERAL DE SANÇÕES", conteudoCentral);
        CriarDescricao("Imponha restrições econômicas, tecnológicas e militares contra nações hostis sem cobrir a ficha lateral.");

        GameObject grid = CriarLinha(conteudoCentral, 96, 10);
        CriarResumoCard(grid.transform, "PAÍSES SANCIONADOS", "3", "Alvos ativos", corVermelho);
        CriarResumoCard(grid.transform, "IMPACTO ECONÔMICO", "-18%", "Média nos alvos", corAmarelo);
        CriarResumoCard(grid.transform, "DURAÇÃO MÉDIA", "8 meses", "Tempo médio", corDestaque);
        CriarResumoCard(grid.transform, "APOIO GLOBAL", "72%", "Conformidade", corVerde);

        CriarTituloSecao("PAÍSES SANCIONADOS", conteudoCentral);
        GameObject table = CriarTabela(conteudoCentral, new string[] { "PAÍS ALVO", "STATUS", "SANÇÕES", "IMPACTO", "DURAÇÃO", "APOIO", "AÇÃO" }, 46);
        CriarLinhaTabela(table.transform, new string[] { "União Carmesim", "Crise", "Comida / Petróleo / Tecnologia", "-25%", "6 meses", "68%", "Revisar" }, new Color[] { corTextoPrimario, corVermelho, corAmarelo, corVermelho, corTextoSecundario, corVerde, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "Domínio Valerian", "Sanções", "Comida / Aço / Armamentos", "-15%", "4 meses", "52%", "Revisar" }, new Color[] { corTextoPrimario, corLaranja, corAmarelo, corVermelho, corTextoSecundario, corAmarelo, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "República Boreal", "Tensão", "Petróleo", "-8%", "2 meses", "35%", "Encerrar" }, new Color[] { corTextoPrimario, corAmarelo, corDestaque, corAmarelo, corTextoSecundario, corTextoSecundario, corDestaque });

        CriarTituloSecao("TIPOS DE SANÇÕES DISPONÍVEIS", conteudoCentral);
        GameObject tipos1 = CriarLinha(conteudoCentral, 82, 8);
        CriarTipoSancao(tipos1.transform, "🌾", "Embargo\nde Comida");
        CriarTipoSancao(tipos1.transform, "💧", "Embargo de\nPetróleo");
        CriarTipoSancao(tipos1.transform, "▰", "Embargo\nde Aço");
        CriarTipoSancao(tipos1.transform, "▥", "Embargo de\nArmamentos");

        GameObject tipos2 = CriarLinha(conteudoCentral, 82, 8);
        CriarTipoSancao(tipos2.transform, "⚛", "Bloqueio\nTecnológico");
        CriarTipoSancao(tipos2.transform, "✈", "Bloqueio\nMilitar");
        CriarTipoSancao(tipos2.transform, "⌖", "Restrição\nComercial Total");
        CriarTipoSancao(tipos2.transform, "+", "Criar nova\nmedida");
    }

    private void ExibirEconomia()
    {
        CriarTituloSecao("VISÃO GERAL DA ECONOMIA", conteudoCentral);
        CriarDescricao("Controle orçamento, impostos, produção nacional, inflação e investimento estratégico.");

        GameObject grid = CriarLinha(conteudoCentral, 110, 10);
        CriarMetricCard(grid.transform, "▣", "TESOURO", "$39.534", "+$60/s", corVerde, 0.72f);
        CriarMetricCard(grid.transform, "📈", "CRESCIMENTO", "+4.8%", "Expansão", corVerde, 0.64f);
        CriarMetricCard(grid.transform, "⚖", "INFLAÇÃO", "3.2%", "Risco baixo", corAmarelo, 0.32f);
        CriarMetricCard(grid.transform, "🏭", "PRODUÇÃO", "78%", "Capacidade", corDestaque, 0.78f);

        CriarTituloSecao("ORÇAMENTO NACIONAL", conteudoCentral);
        GameObject orcamento = CriarLinha(conteudoCentral, 168, 10);
        CriarBudgetCard(orcamento.transform, "DEFESA", "32%", "$12.650", corVermelho);
        CriarBudgetCard(orcamento.transform, "INTERIOR", "24%", "$9.420", corDestaque);
        CriarBudgetCard(orcamento.transform, "CIÊNCIA", "18%", "$7.080", corRoxo);
        CriarBudgetCard(orcamento.transform, "TRABALHO", "16%", "$6.320", corVerde);
        CriarBudgetCard(orcamento.transform, "RESERVA", "10%", "$4.064", corAmarelo);

        CriarTituloSecao("SETOR PRODUTIVO", conteudoCentral);
        GameObject table = CriarTabela(conteudoCentral, new string[] { "SETOR", "PRODUÇÃO", "RECEITA", "CUSTO", "TENDÊNCIA" }, 40);
        CriarLinhaTabela(table.transform, new string[] { "Indústria pesada", "84%", "$8.400", "$3.100", "↑" }, new Color[] { corTextoPrimario, corVerde, corVerde, corAmarelo, corVerde });
        CriarLinhaTabela(table.transform, new string[] { "Agricultura", "72%", "$5.900", "$1.600", "↑" }, new Color[] { corTextoPrimario, corVerde, corVerde, corAmarelo, corVerde });
        CriarLinhaTabela(table.transform, new string[] { "Serviços", "68%", "$4.700", "$1.200", "→" }, new Color[] { corTextoPrimario, corAmarelo, corVerde, corAmarelo, corAmarelo });
    }

    private void ExibirMercadoGlobal()
    {
        CriarTituloSecao("MERCADO GLOBAL", conteudoCentral);
        CriarDescricao("Compre e venda recursos no mercado internacional. Preços flutuam conforme oferta, demanda e relações diplomáticas.");

        CriarTituloSecao("RECURSOS DISPONÍVEIS", conteudoCentral);
        GameObject recursos1 = CriarLinha(conteudoCentral, 140, 8);
        CriarMercadoRecurso(recursos1.transform, "🌾", "COMIDA", "24.850", "$120 / un", "+1.2%", corVerde);
        CriarMercadoRecurso(recursos1.transform, "💧", "PETRÓLEO", "18.340", "$185 / un", "-2.4%", corVermelho);
        CriarMercadoRecurso(recursos1.transform, "▰", "AÇO", "31.760", "$95 / un", "-0.8%", corVerde);
        CriarMercadoRecurso(recursos1.transform, "▥", "ARMAMENTOS", "7.420", "$420 / un", "+3.7%", corVermelho);

        CriarTituloSecao("OFERTAS ATUAIS DO MERCADO", conteudoCentral);
        GameObject table = CriarTabela(conteudoCentral, new string[] { "VENDEDOR", "RECURSO", "QUANT.", "PREÇO", "TOTAL", "RELAÇÃO", "ENTREGA", "AÇÃO" }, 40);
        CriarLinhaTabela(table.transform, new string[] { "União Carmesim", "Petróleo", "5.000", "$172", "$860.000", "-40", "2 turnos", "Comprar" }, new Color[] { corTextoPrimario, corTextoPrimario, corTextoPrimario, corAmarelo, corVerde, corVermelho, corTextoSecundario, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "República Boreal", "Aço", "8.000", "$92", "$736.000", "+75", "1 turno", "Comprar" }, new Color[] { corTextoPrimario, corTextoPrimario, corTextoPrimario, corAmarelo, corVerde, corVerde, corTextoSecundario, corDestaque });
        CriarLinhaTabela(table.transform, new string[] { "Federação Alvorada", "Comida", "10.000", "$118", "$1.180.000", "+10", "1 turno", "Comprar" }, new Color[] { corTextoPrimario, corTextoPrimario, corTextoPrimario, corAmarelo, corVerde, corVerde, corTextoSecundario, corDestaque });
    }

    private void ExibirInterior()
    {
        CriarTituloSecao("VISÃO GERAL DO INTERIOR", conteudoCentral);
        GameObject cards = CriarLinha(conteudoCentral, 128, 10);
        CriarMetricCard(cards.transform, "♟", "POPULAÇÃO", "110 / 200", "Crescimento +5", corDestaque, 0.55f);
        CriarMetricCard(cards.transform, "♥", "BEM-ESTAR", "68%", "Estável", corVerde, 0.68f);
        CriarMetricCard(cards.transform, "◈", "ESTABILIDADE", "62%", "Política", corVerde, 0.62f);
        CriarMetricCard(cards.transform, "🏭", "PRODUTIVIDADE", "1.24", "Moderada", corAmarelo, 0.58f);

        CriarTituloSecao("INFRAESTRUTURA NACIONAL", conteudoCentral);
        GameObject infra1 = CriarLinha(conteudoCentral, 142, 8);
        CriarInfraCard(infra1.transform, "⌂", "HABITAÇÃO", "4 / 10", "Capacidade por casa: 250", "$2.000");
        CriarInfraCard(infra1.transform, "+", "SAÚDE", "1 / 5", "Eficiência: 60%", "$2.500");
        CriarInfraCard(infra1.transform, "▣", "EDUCAÇÃO", "1 / 5", "Eficiência: 58%", "$1.800");

        GameObject infra2 = CriarLinha(conteudoCentral, 142, 8);
        CriarInfraCard(infra2.transform, "⚡", "ENERGIA", "2 / 5", "Eficiência: 70%", "$2.800");
        CriarInfraCard(infra2.transform, "◊", "ÁGUA", "1 / 5", "Eficiência: 65%", "$2.500");
        CriarInfraCard(infra2.transform, "♨", "SANEAMENTO", "1 / 5", "Eficiência: 60%", "$1.500");
    }

    private void ExibirDefesa()
    {
        CriarTituloSecao("VISÃO GERAL DA DEFESA", conteudoCentral);
        GameObject top = CriarLinha(conteudoCentral, 116, 10);
        CriarMetricCard(top.transform, "♟", "PODER MILITAR", "3.250", "Força 62%", corDestaque, 0.62f);
        CriarMetricCard(top.transform, "●", "PRONTIDÃO", "78%", "Operacional", corVerde, 0.78f);
        CriarMetricCard(top.transform, "$", "GASTO MILITAR", "$2.450", "por turno", corAmarelo, 0.48f);
        CriarMetricCard(top.transform, "⚗", "PESQUISAS", "3", "em andamento", corRoxo, 0.60f);

        CriarTituloSecao("COMPOSIÇÃO DAS FORÇAS", conteudoCentral);
        GameObject forcas1 = CriarLinha(conteudoCentral, 190, 10);
        CriarForcaCard(forcas1.transform, "▱", "EXÉRCITO", "1.250", new string[] { "Tanques 350", "Blindados 620", "Artilharia 180", "Infantaria 100" }, 0.62f);
        CriarForcaCard(forcas1.transform, "▰", "MARINHA", "850", new string[] { "Navios 18", "Submarinos 6", "Porta-Aviões 1", "Apoio 7" }, 0.71f);

        GameObject forcas2 = CriarLinha(conteudoCentral, 190, 10);
        CriarForcaCard(forcas2.transform, "✈", "FORÇA AÉREA", "950", new string[] { "Caças 68", "Bombardeiros 12", "Helicópteros 26", "Drones 124" }, 0.80f);
        CriarForcaCard(forcas2.transform, "⌖", "DEFESA ANTIAÉREA", "200", new string[] { "SAM 28", "Baterias 64", "Radares 35", "Interceptadores 73" }, 0.60f);
    }

    private void ExibirCiencia()
    {
        CriarTituloSecao("VISÃO GERAL DA CIÊNCIA", conteudoCentral);
        GameObject top = CriarLinha(conteudoCentral, 112, 10);
        CriarMetricCard(top.transform, "⚗", "INVESTIMENTO", "2.45%", "do PIB", corVerde, 0.49f);
        CriarMetricCard(top.transform, "🧠", "PESQUISA", "1.250", "+45 / turno", corRoxo, 0.70f);
        CriarMetricCard(top.transform, "♟", "CIENTISTAS", "128 / 150", "Ocupação 85%", corDestaque, 0.85f);
        CriarMetricCard(top.transform, "⚜", "NÍVEL TEC.", "III", "Avançado", corAmarelo, 0.66f);

        CriarTituloSecao("PESQUISAS EM ANDAMENTO", conteudoCentral);
        GameObject table = CriarTabela(conteudoCentral, new string[] { "PROJETO", "CATEGORIA", "PROGRESSO", "TEMPO", "AÇÃO" }, 40);
        CriarLinhaPesquisa(table.transform, "Motores de Fusão Compactos", "Energia", 0.62f, "3 turnos");
        CriarLinhaPesquisa(table.transform, "Mísseis Hipersônicos", "Defesa", 0.48f, "4 turnos");
        CriarLinhaPesquisa(table.transform, "IA Tática Avançada", "Tecnologia", 0.71f, "2 turnos");
        CriarLinhaPesquisa(table.transform, "Blindagem Reativa Avançada", "Defesa", 0.35f, "5 turnos");
    }

    private void ExibirTrabalho()
    {
        CriarTituloSecao("VISÃO GERAL DO TRABALHO", conteudoCentral);
        GameObject top = CriarLinha(conteudoCentral, 126, 10);
        CriarMetricCard(top.transform, "♟", "POPULAÇÃO", "110 / 200", "Capacidade", corDestaque, 0.55f);
        CriarMetricCard(top.transform, "♙", "ATIVA", "88 / 110", "80%", corDestaque, 0.80f);
        CriarMetricCard(top.transform, "▣", "EMPREGADOS", "82 / 88", "93%", corVerde, 0.93f);
        CriarMetricCard(top.transform, "📈", "PRODUTIVIDADE", "1.24", "Moderada", corVerde, 0.62f);

        CriarTituloSecao("DISTRIBUIÇÃO DA FORÇA DE TRABALHO", conteudoCentral);
        GameObject dist = CriarLinha(conteudoCentral, 230, 10);
        CriarGraficoDonutFake(dist.transform);
        CriarSetorResumo(dist.transform);

        CriarTituloSecao("FORMAÇÃO PROFISSIONAL", conteudoCentral);
        GameObject form = CriarLinha(conteudoCentral, 110, 8);
        CriarFormacaoCard(form.transform, "🏭", "Técnico Industrial", "Formando: 12", "2 turnos", 0.68f);
        CriarFormacaoCard(form.transform, "⚙", "Engenheiro", "Formando: 8", "3 turnos", 0.40f);
        CriarFormacaoCard(form.transform, "</>", "Programador", "Formando: 6", "2 turnos", 0.30f);
        CriarFormacaoCard(form.transform, "🌿", "Téc. Agrícola", "Formando: 10", "1 turno", 0.80f);
    }

    private void PainelFichaDiplomatica()
    {
        PaisGoverno alvo = ObterPais(paisSelecionadoId) ?? paises.FirstOrDefault(p => p.id != paisJogadorId);
        if (alvo == null) return;

        RelacaoDiplomatica r = ObterRelacao(paisJogadorId, alvo.id);
        if (r == null) r = new RelacaoDiplomatica { paisA = paisJogadorId, paisB = alvo.id, valor = 0 };

        CriarTituloSecao("FICHA DIPLOMÁTICA", painelDireito);

        GameObject head = CriarCardBase(painelDireito, 86, new Color(0.020f, 0.080f, 0.100f, 0.96f));
        HorizontalLayoutGroup h = head.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 8, 8);
        h.spacing = 10;

        Text brasao = CriarTextoLayout(head.transform, "✦", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 70);
        brasao.GetComponent<LayoutElement>().preferredWidth = 54f;

        GameObject tx = CriarUIObjeto("TextoPais", head.transform);
        tx.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(tx, 0, 0, 0);
        CriarTextoLayout(tx.transform, alvo.nome.ToUpper(), 16, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 32);
        CriarTextoLayout(tx.transform, NomeBloco(alvo.bloco).ToUpper(), 10, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Bold, 22);

        CriarTituloSecao("INFORMAÇÕES", painelDireito);
        CriarLinhaInfo("BLOCO", NomeBloco(alvo.bloco), corDestaque);
        CriarLinhaInfo("RELAÇÃO", (r.valor > 0 ? "+" : "") + r.valor, CorRelacao(r.valor));
        CriarLinhaInfo("ESTADO", NomeEstadoRelacao(r.estado), CorRelacao(r.valor));
        CriarLinhaInfo("STATUS", NomeStatus(alvo.status), CorStatus(alvo.status));
        CriarLinhaInfo("TRATADO COMERCIAL", r.tratadoComercial ? "Ativo" : "Inativo", r.tratadoComercial ? corVerde : corVermelho);
        CriarLinhaInfo("PACTO MILITAR", r.pactoMilitar ? "Ativo" : "Inativo", r.pactoMilitar ? corVerde : corVermelho);

        CriarTituloSecao("RECURSOS PRINCIPAIS", painelDireito);
        CriarLinhaInfo("🌾 Comida", alvo.comida.ToString(), corVerde);
        CriarLinhaInfo("💧 Petróleo", alvo.petroleo.ToString(), corDestaque);
        CriarLinhaInfo("▰ Aço", alvo.aco.ToString(), corAmarelo);
        CriarLinhaInfo("▥ Armamentos", alvo.armamentos.ToString(), corVermelho);

        CriarTituloSecao("AÇÕES RÁPIDAS", painelDireito);
        CriarBotaoBloco(painelDireito, "ABRIR PERFIL COMPLETO", corAzulBotao, () => Notificar("Perfil", "Perfil diplomático aberto.")).GetComponent<LayoutElement>().preferredHeight = 40;
        CriarBotaoBloco(painelDireito, "ENVIAR PROPOSTA", corPainel2, () => Notificar("Diplomacia", "Proposta diplomática enviada.")).GetComponent<LayoutElement>().preferredHeight = 40;
        CriarBotaoBloco(painelDireito, "DECLARAR CRISE", new Color(0.260f, 0.070f, 0.055f, 1f), () => Notificar("Crise", "Crise diplomática registrada.")).GetComponent<LayoutElement>().preferredHeight = 40;
    }

    private void PainelNovaSancao()
    {
        CriarTituloSecao("NOVA SANÇÃO", painelDireito);
        CriarDescricaoNoPainel("A ficha lateral não ocupa mais o centro. As ações ficam presas nesta coluna fixa.");
        CriarTituloSecao("PAÍS ALVO", painelDireito);
        foreach (PaisGoverno p in paises.Where(x => x.id != paisJogadorId).Take(4))
        {
            CriarBotaoBloco(painelDireito, p.nome, p.id == paisSelecionadoId ? corAbaAtiva : corPainel2, () =>
            {
                paisSelecionadoId = p.id;
                AtualizarInterfaceCompleta();
            }).GetComponent<LayoutElement>().preferredHeight = 38;
        }

        CriarTituloSecao("TIPO", painelDireito);
        CriarCheckSancao("🌾", "Embargo de Comida", "Reduz produção e estabilidade");
        CriarCheckSancao("💧", "Embargo de Petróleo", "Afeta veículos e indústria");
        CriarCheckSancao("⚛", "Bloqueio Tecnológico", "Trava pesquisas avançadas");
        CriarCheckSancao("⌖", "Restrição Comercial", "Corta rotas comerciais");

        CriarTituloSecao("DURAÇÃO", painelDireito);
        GameObject dur = CriarLinha(painelDireito, 34, 6);
        CriarBotaoBloco(dur.transform, "1 Mês", corPainel2, null);
        CriarBotaoBloco(dur.transform, "3 Meses", corPainel2, null);
        CriarBotaoBloco(dur.transform, "6 Meses", corAbaAtiva, null);
        CriarBotaoBloco(dur.transform, "1 Ano", corPainel2, null);
        CriarBotaoBloco(painelDireito, "APLICAR SANÇÃO", new Color(0.34f, 0.100f, 0.080f, 1f), () => Notificar("Sanção", "Sanção aplicada contra " + NomePais(paisSelecionadoId) + ".")).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelAliancas()
    {
        CriarTituloSecao("CONSELHO DE ALIANÇAS", painelDireito);
        CriarInfoCard("Tratado atual", "Ordem Atlas", corDestaque);
        CriarInfoCard("Confiança do bloco", "82%", corVerde);
        CriarInfoCard("Operações conjuntas", "3 ativas", corAmarelo);
        CriarTituloSecao("PEDIDOS PENDENTES", painelDireito);
        CriarLinhaInfo("Federação Alvorada", "Entrada no bloco", corDestaque);
        CriarLinhaInfo("República Boreal", "Apoio naval", corVerde);
        CriarBotaoBloco(painelDireito, "ABRIR CONSELHO", corAzulBotao, () => Notificar("Alianças", "Conselho aberto.")).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelEconomia()
    {
        CriarTituloSecao("DETALHES ECONÔMICOS", painelDireito);
        CriarLinhaInfo("Receita por turno", "+$7.250", corVerde);
        CriarLinhaInfo("Gastos por turno", "-$4.800", corVermelho);
        CriarLinhaInfo("Saldo líquido", "+$2.450", corVerde);
        CriarLinhaInfo("Dívida nacional", "$0", corVerde);
        CriarTituloSecao("POLÍTICAS FISCAIS", painelDireito);
        CriarCheckSancao("📈", "Incentivo à indústria", "+10% produção industrial");
        CriarCheckSancao("🏦", "Controle de inflação", "Reduz instabilidade");
        CriarCheckSancao("🚚", "Subsídio logístico", "Melhora mercado global");
        CriarBotaoBloco(painelDireito, "GERENCIAR ECONOMIA", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelMercado()
    {
        CriarTituloSecao("RESUMO DO MERCADO", painelDireito);
        GameObject graf = CriarCardBase(painelDireito, 118, corCard);
        CriarTextoLivre(graf.transform, "╱╲╱╲╲╱╲╱\nComida  Petróleo  Aço\nArmamentos  Urânio", 13, corDestaque, TextAnchor.MiddleCenter, FontStyle.Bold);
        CriarLinhaInfo("Tendência geral", "Volátil", corAmarelo);
        CriarLinhaInfo("Melhor compra", "Aço", corVerde);
        CriarLinhaInfo("Maior risco", "Petróleo", corVermelho);
        CriarBotaoBloco(painelDireito, "CRIAR ORDEM DE COMPRA", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelInterior()
    {
        CriarTituloSecao("PLANEJAMENTO INTERNO", painelDireito);
        CriarInfoCard("Moradias", "4 / 10", corDestaque);
        CriarInfoCard("Bem-estar", "68%", corVerde);
        CriarInfoCard("Estabilidade", "62%", corVerde);
        CriarTituloSecao("PRIORIDADES", painelDireito);
        CriarCheckSancao("⌂", "Habitação Popular", "+250 capacidade");
        CriarCheckSancao("+", "Saúde Pública", "+8% bem-estar");
        CriarCheckSancao("⚡", "Rede de Energia", "+10% indústria");
        CriarBotaoBloco(painelDireito, "INVESTIR NO INTERIOR", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelDefesa()
    {
        CriarTituloSecao("COMANDO DE DEFESA", painelDireito);
        CriarInfoCard("Prontidão", "78%", corVerde);
        CriarInfoCard("Ameaça aérea", "Alta", corVermelho);
        CriarInfoCard("Ameaça naval", "Moderada", corAmarelo);
        CriarTituloSecao("ORDENS", painelDireito);
        CriarCheckSancao("✈", "Alerta aéreo", "Caças em prontidão");
        CriarCheckSancao("▰", "Patrulha naval", "Rotas costeiras");
        CriarCheckSancao("⌖", "Defesa antiaérea", "Radares ativos");
        CriarBotaoBloco(painelDireito, "ABRIR COMANDO MILITAR", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelCiencia()
    {
        CriarTituloSecao("PROJETO SELECIONADO", painelDireito);
        CriarInfoCard("Mísseis Hipersônicos", "48%", corRoxo);
        CriarDescricaoNoPainel("Desbloqueia armas de alta velocidade para ataques estratégicos e defesa avançada.");
        CriarLinhaInfo("Categoria", "Defesa", corTextoPrimario);
        CriarLinhaInfo("Nível", "III - Avançado", corTextoPrimario);
        CriarLinhaInfo("Custo", "1.800", corAmarelo);
        CriarTituloSecao("REQUISITOS", painelDireito);
        CriarLinhaInfo("Propulsão avançada", "✓", corVerde);
        CriarLinhaInfo("Materiais compostos II", "✓", corVerde);
        CriarBotaoBloco(painelDireito, "VER ÁRVORE TECNOLÓGICA", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
    }

    private void PainelTrabalho()
    {
        CriarTituloSecao("POLÍTICAS TRABALHISTAS", painelDireito);
        CriarCheckSancao("♟", "Geração de Emprego", "+5% empregos");
        CriarCheckSancao("🌿", "Indústria Nacional", "+10% produtividade");
        CriarCheckSancao("♟", "Redução de Jornada", "+8% satisfação");
        CriarCheckSancao("🛡", "Salário Mínimo", "+8% estabilidade");
        CriarBotaoBloco(painelDireito, "GERENCIAR POLÍTICAS", corAzulBotao, null).GetComponent<LayoutElement>().preferredHeight = 42;
        CriarTituloSecao("ALERTAS", painelDireito);
        CriarLinhaInfo("Desemprego acima do ideal", "6.8%", corAmarelo);
        CriarLinhaInfo("Produtividade agrícola baixa", "1.10", corAmarelo);
        CriarLinhaInfo("Satisfação em queda", "-2%", corVermelho);
    }

    private void AtualizarRodape()
    {
        LimparFilhos(rodapeEsquerdo);
        LimparFilhos(rodapeMeio);
        LimparFilhos(rodapeDireito);

        CriarTituloSecao("NOTIFICAÇÕES E EVENTOS", rodapeEsquerdo);
        if (notificacoes.Count == 0)
        {
            CriarLinhaRodape(rodapeEsquerdo, "✓", "Sistema", "Nenhuma notificação crítica.", corVerde);
        }
        else
        {
            foreach (NotificacaoGoverno n in notificacoes.Take(2)) CriarLinhaRodape(rodapeEsquerdo, n.icone, n.titulo, n.mensagem, n.cor);
        }
        CriarLinhaRodape(rodapeEsquerdo, "◈", "Mercado", "Petróleo caiu 2.4% no último ciclo.", corAmarelo);

        CriarTituloSecao("SITUAÇÃO GLOBAL", rodapeMeio);
        CriarLinhaRodape(rodapeMeio, "△", "Defesa", "Atividade aérea hostil detectada.", corVermelho);
        CriarLinhaRodape(rodapeMeio, "⚑", "Diplomacia", "Boreal reforçou pacto militar.", corDestaque);
        CriarLinhaRodape(rodapeMeio, "▣", "Economia", "Indústria pesada acima de 80%.", corVerde);

        CriarTituloSecao("AÇÕES RÁPIDAS", rodapeDireito);
        CriarBotaoBloco(rodapeDireito, "ABRIR RELATÓRIO NACIONAL", corAzulBotao, () => Notificar("Relatório", "Relatório nacional aberto.")).GetComponent<LayoutElement>().preferredHeight = 32;
        CriarBotaoBloco(rodapeDireito, "CONVOCAR CONSELHO", corPainel2, () => Notificar("Conselho", "Conselho governamental convocado.")).GetComponent<LayoutElement>().preferredHeight = 32;
        CriarBotaoBloco(rodapeDireito, "EMITIR ALERTA GLOBAL", new Color(0.270f, 0.070f, 0.055f, 1f), () => Notificar("Alerta", "Alerta global emitido.")).GetComponent<LayoutElement>().preferredHeight = 32;
    }

    private void AtualizarDica()
    {
        LimparFilhos(barraDica);
        string dicaEmoji = mostrarAvisoEmojiNoRodape ? "  •  F9 testa emojis se o GerenciadorEmojis estiver na cena" : "";
        CriarTextoLayout(barraDica, "ATALHO: X abre/fecha  •  Layout 16:9  •  Ficha fixa na direita  •  Centro e ficha com scroll" + dicaEmoji, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, alturaDica);
    }

    private void CriarTituloSecao(string titulo, Transform parent)
    {
        GameObject box = CriarUIObjeto("Titulo_" + titulo, parent);
        LayoutElement le = box.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;
        le.minHeight = 24f;

        HorizontalLayoutGroup h = box.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 7;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        GameObject linha = CriarUIObjeto("LinhaFina", box.transform);
        LayoutElement linhaLe = linha.AddComponent<LayoutElement>();
        linhaLe.preferredWidth = 3f;
        linhaLe.minWidth = 3f;
        linhaLe.flexibleWidth = 0f;
        Image img = linha.AddComponent<Image>();
        img.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.88f);

        Text tituloTxt = CriarTextoLayout(box.transform, titulo, 12, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 24);
        tituloTxt.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private void CriarDescricao(string texto)
    {
        CriarDescricaoNo(conteudoCentral, texto);
    }

    private void CriarDescricaoNoPainel(string texto)
    {
        CriarDescricaoNo(painelDireito, texto);
    }

    private void CriarDescricaoNo(Transform parent, string texto)
    {
        GameObject card = CriarCardBase(parent, 54, new Color(0.020f, 0.060f, 0.078f, 0.82f));
        CriarTextoLivre(card.transform, texto, 12, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 12, 8, 12, 8);
    }

    private GameObject CriarLinha(Transform parent, float altura, float spacing)
    {
        GameObject row = CriarUIObjeto("Linha", parent);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = altura;
        le.minHeight = altura;
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        return row;
    }

    private void CriarCardBrasao(Transform parent, string titulo, string icone)
    {
        GameObject card = CriarCardBase(parent, 0, new Color(0.020f, 0.092f, 0.130f, 0.96f));
        card.GetComponent<LayoutElement>().flexibleWidth = 1.2f;
        AddVertical(card, 10, 10, 8);
        CriarTextoLayout(card.transform, icone, 30, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 38);
        CriarTextoLayout(card.transform, titulo, 15, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, "Governo nacional", 10, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Normal, 18);
    }

    private void CriarMetricCard(Transform parent, string icone, string titulo, string valor, string subtitulo, Color cor, float progresso)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 9, 8, 7);
        CriarTextoLayout(card.transform, icone, 22, cor, TextAnchor.MiddleLeft, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, titulo, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 18);
        CriarTextoLayout(card.transform, valor, 19, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 28);
        CriarTextoLayout(card.transform, subtitulo, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 18);
        CriarBarraProgresso(card.transform, progresso, cor, 6);
    }

    private void CriarMiniStatus(Transform parent, string icone, string titulo, string valor, Color cor)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        HorizontalLayoutGroup h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 6, 6);
        h.spacing = 8;
        CriarTextoLayout(card.transform, icone, 21, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 62).GetComponent<LayoutElement>().preferredWidth = 30;
        GameObject tx = CriarUIObjeto("Textos", card.transform);
        tx.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(tx, 0, 0, 0);
        CriarTextoLayout(tx.transform, titulo, 9, corTextoSecundario, TextAnchor.LowerLeft, FontStyle.Bold, 24);
        CriarTextoLayout(tx.transform, valor, 14, cor, TextAnchor.UpperLeft, FontStyle.Bold, 26);
    }

    private void CriarResumoCard(Transform parent, string titulo, string valor, string subtitulo, Color cor)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 10, 9, 7);
        CriarTextoLayout(card.transform, titulo, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 20);
        CriarTextoLayout(card.transform, valor, 22, cor, TextAnchor.MiddleLeft, FontStyle.Bold, 34);
        CriarTextoLayout(card.transform, subtitulo, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 22);
    }

    private void CriarCardPaisDiplomacia(Transform parent, PaisGoverno pais, RelacaoDiplomatica rel)
    {
        GameObject card = CriarCardBase(parent, 86, pais.id == paisSelecionadoId ? new Color(0.030f, 0.115f, 0.155f, 0.96f) : corCard);
        HorizontalLayoutGroup h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 8, 8);
        h.spacing = 10;

        CriarTextoLayout(card.transform, "✦", 26, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 70).GetComponent<LayoutElement>().preferredWidth = 50;

        GameObject tx = CriarUIObjeto("Textos", card.transform);
        tx.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(tx, 0, 0, 0);
        CriarTextoLayout(tx.transform, pais.nome.ToUpper(), 14, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 28);
        CriarTextoLayout(tx.transform, NomeBloco(pais.bloco) + "  •  " + NomeStatus(pais.status), 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 22);
        int valor = rel != null ? rel.valor : 0;
        CriarTextoLayout(tx.transform, "Relação: " + (valor > 0 ? "+" : "") + valor + "  •  " + (rel != null ? NomeEstadoRelacao(rel.estado) : "Neutro"), 11, CorRelacao(valor), TextAnchor.UpperLeft, FontStyle.Bold, 24);

        GameObject areaBtn = CriarUIObjeto("Acoes", card.transform);
        areaBtn.GetComponent<RectTransform>();
        areaBtn.AddComponent<LayoutElement>().preferredWidth = 120;
        AddVertical(areaBtn, 0, 0, 0);
        CriarBotaoBloco(areaBtn.transform, "SELECIONAR", pais.id == paisSelecionadoId ? corAbaAtiva : corAzulBotao, () =>
        {
            paisSelecionadoId = pais.id;
            AtualizarInterfaceCompleta();
        }).GetComponent<LayoutElement>().preferredHeight = 36;
    }

    private void CriarAcaoCard(Transform parent, string icone, string titulo, string custo, Color cor)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 10, 8, 8);
        CriarTextoLayout(card.transform, icone, 22, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 28);
        CriarTextoLayout(card.transform, titulo, 11, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 30);
        CriarTextoLayout(card.transform, custo, 12, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 22);
    }

    private void CriarTipoSancao(Transform parent, string icone, string titulo)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 6, 6, 4);
        CriarTextoLayout(card.transform, icone, 22, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 28);
        CriarTextoLayout(card.transform, titulo, 10, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 42);
    }

    private void CriarBudgetCard(Transform parent, string titulo, string percentual, string valor, Color cor)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 8, 8, 8);
        CriarTextoLayout(card.transform, titulo, 11, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, percentual, 22, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 34);
        CriarBarraProgresso(card.transform, Mathf.Clamp01(float.Parse(percentual.Replace("%", "")) / 100f), cor, 8);
        CriarTextoLayout(card.transform, valor, 12, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
    }

    private void CriarMercadoRecurso(Transform parent, string icone, string nome, string estoque, string preco, string variacao, Color cor)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 9, 8, 7);
        CriarTextoLayout(card.transform, icone, 24, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 30);
        CriarTextoLayout(card.transform, nome, 12, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 22);
        CriarTextoLayout(card.transform, estoque, 16, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 26);
        CriarTextoLayout(card.transform, preco, 11, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Normal, 20);
        CriarTextoLayout(card.transform, variacao, 12, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 20);
    }

    private void CriarInfraCard(Transform parent, string icone, string nome, string nivel, string desc, string custo)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 8, 8, 7);
        CriarTextoLayout(card.transform, icone, 22, corDestaque, TextAnchor.MiddleCenter, FontStyle.Bold, 28);
        CriarTextoLayout(card.transform, nome, 12, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, nivel, 16, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 28);
        CriarTextoLayout(card.transform, desc, 9, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Normal, 24);
        CriarTextoLayout(card.transform, custo, 11, corVerde, TextAnchor.MiddleCenter, FontStyle.Bold, 20);
    }

    private void CriarForcaCard(Transform parent, string icone, string nome, string total, string[] linhas, float prontidao)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 9, 8, 7);
        CriarTextoLayout(card.transform, icone + "  " + nome, 13, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, total + " unidades", 20, corDestaque, TextAnchor.MiddleLeft, FontStyle.Bold, 28);
        foreach (string s in linhas) CriarTextoLayout(card.transform, "• " + s, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Normal, 18);
        CriarBarraProgresso(card.transform, prontidao, prontidao > 0.7f ? corVerde : corAmarelo, 7);
    }

    private void CriarLinhaPesquisa(Transform parent, string projeto, string categoria, float progresso, string tempo)
    {
        GameObject row = CriarLinhaTabelaBase(parent, 40);
        CriarCelula(row.transform, projeto, corTextoPrimario, 1.8f, FontStyle.Bold);
        CriarCelula(row.transform, categoria, corRoxo, 1.0f, FontStyle.Bold);
        GameObject cel = CriarUIObjeto("CelulaProgresso", row.transform);
        cel.AddComponent<LayoutElement>().flexibleWidth = 1.2f;
        AddVertical(cel, 8, 8, 0);
        CriarBarraProgresso(cel.transform, progresso, corDestaque, 8);
        CriarCelula(row.transform, tempo, corAmarelo, 1.0f, FontStyle.Normal);
        CriarCelula(row.transform, "Detalhes", corDestaque, 0.9f, FontStyle.Bold);
    }

    private void CriarGraficoDonutFake(Transform parent)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 0.9f;
        CriarTextoLivre(card.transform, "      ████\n   ██      ██\n ██   46%   ██\n   ██      ██\n      ████\n\nINDÚSTRIA", 16, corDestaque, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    private void CriarSetorResumo(Transform parent)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1.3f;
        AddVertical(card, 12, 10, 9);
        CriarTextoLayout(card.transform, "SETORES", 13, corTextoPrimario, TextAnchor.MiddleLeft, FontStyle.Bold, 26);
        CriarLinhaInfoNo(card.transform, "Indústria", "46%", corDestaque);
        CriarLinhaInfoNo(card.transform, "Serviços", "28%", corVerde);
        CriarLinhaInfoNo(card.transform, "Agricultura", "16%", corAmarelo);
        CriarLinhaInfoNo(card.transform, "Ciência", "10%", corRoxo);
    }

    private void CriarFormacaoCard(Transform parent, string icone, string nome, string formando, string tempo, float progresso)
    {
        GameObject card = CriarCardBase(parent, 0, corCard);
        card.GetComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(card, 8, 8, 7);
        CriarTextoLayout(card.transform, icone, 20, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, nome, 11, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold, 24);
        CriarTextoLayout(card.transform, formando, 10, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Normal, 18);
        CriarTextoLayout(card.transform, tempo, 10, corTextoSecundario, TextAnchor.MiddleCenter, FontStyle.Normal, 18);
        CriarBarraProgresso(card.transform, progresso, corVerde, 6);
    }

    private GameObject CriarTabela(Transform parent, string[] headers, float alturaLinha)
    {
        GameObject table = CriarCardBase(parent, 0, new Color(0.014f, 0.043f, 0.060f, 0.94f));
        VerticalLayoutGroup v = table.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 0);
        v.spacing = 1;
        table.GetComponent<LayoutElement>().minHeight = alturaLinha * 2f;
        table.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject head = CriarLinhaTabelaBase(table.transform, 34);
        head.GetComponent<Image>().color = new Color(0.000f, 0.255f, 0.390f, 0.96f);
        foreach (string h in headers) CriarCelula(head.transform, h, Color.white, 1f, FontStyle.Bold);
        return table;
    }

    private void CriarLinhaTabela(Transform parent, string[] valores, Color[] cores)
    {
        GameObject row = CriarLinhaTabelaBase(parent, 40);
        for (int i = 0; i < valores.Length; i++)
        {
            CriarCelula(row.transform, valores[i], i < cores.Length ? cores[i] : corTextoPrimario, 1f, i == 0 ? FontStyle.Bold : FontStyle.Normal);
        }
    }

    private GameObject CriarLinhaTabelaBase(Transform parent, float altura)
    {
        GameObject row = CriarUIObjeto("LinhaTabela", parent);
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = altura;
        le.minHeight = altura;
        Image img = row.AddComponent<Image>();
        img.color = new Color(0.024f, 0.073f, 0.095f, 0.82f);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 0, 0);
        h.spacing = 4;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        return row;
    }

    private void CriarCelula(Transform parent, string texto, Color cor, float flex, FontStyle style)
    {
        Text t = CriarTextoLayout(parent, texto, 10, cor, TextAnchor.MiddleCenter, style, 40);
        t.GetComponent<LayoutElement>().flexibleWidth = flex;
    }

    private void CriarLinhaInfo(string label, string valor, Color corValor)
    {
        CriarLinhaInfoNo(painelDireito, label, valor, corValor);
    }

    private void CriarLinhaInfoNo(Transform parent, string label, string valor, Color corValor)
    {
        GameObject row = CriarCardBase(parent, 32, new Color(0.018f, 0.054f, 0.072f, 0.82f));
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 0, 0);
        h.spacing = 6;
        Text a = CriarTextoLayout(row.transform, label, 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 32);
        a.GetComponent<LayoutElement>().flexibleWidth = 1f;
        Text b = CriarTextoLayout(row.transform, valor, 11, corValor, TextAnchor.MiddleRight, FontStyle.Bold, 32);
        b.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private void CriarInfoCard(string titulo, string valor, Color cor)
    {
        GameObject card = CriarCardBase(painelDireito, 58, corCard);
        AddVertical(card, 10, 7, 7);
        CriarTextoLayout(card.transform, titulo.ToUpper(), 10, corTextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold, 18);
        CriarTextoLayout(card.transform, valor, 18, cor, TextAnchor.MiddleLeft, FontStyle.Bold, 28);
    }

    private void CriarCheckSancao(string icone, string titulo, string desc)
    {
        GameObject card = CriarCardBase(painelDireito, 56, corCard);
        HorizontalLayoutGroup h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 6, 6);
        h.spacing = 9;
        CriarTextoLayout(card.transform, icone, 19, corAmarelo, TextAnchor.MiddleCenter, FontStyle.Bold, 44).GetComponent<LayoutElement>().preferredWidth = 32;
        GameObject tx = CriarUIObjeto("Textos", card.transform);
        tx.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(tx, 0, 0, 0);
        CriarTextoLayout(tx.transform, titulo, 11, corTextoPrimario, TextAnchor.LowerLeft, FontStyle.Bold, 24);
        CriarTextoLayout(tx.transform, desc, 9, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Normal, 20);
    }

    private void CriarLinhaRodape(Transform parent, string icone, string titulo, string mensagem, Color cor)
    {
        GameObject row = CriarCardBase(parent, 36, new Color(0.018f, 0.055f, 0.072f, 0.80f));
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 3, 3);
        h.spacing = 8;
        CriarTextoLayout(row.transform, icone, 15, cor, TextAnchor.MiddleCenter, FontStyle.Bold, 32).GetComponent<LayoutElement>().preferredWidth = 26;
        GameObject tx = CriarUIObjeto("Textos", row.transform);
        tx.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddVertical(tx, 0, 0, 0);
        CriarTextoLayout(tx.transform, titulo, 10, cor, TextAnchor.LowerLeft, FontStyle.Bold, 17);
        CriarTextoLayout(tx.transform, mensagem, 9, corTextoSecundario, TextAnchor.UpperLeft, FontStyle.Normal, 17);
    }

    private void CriarBarraProgresso(Transform parent, float valor, Color cor, float altura)
    {
        GameObject bg = CriarUIObjeto("BarraProgresso", parent);
        LayoutElement le = bg.AddComponent<LayoutElement>();
        le.preferredHeight = altura;
        le.minHeight = altura;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.28f);

        GameObject fill = CriarUIObjeto("Fill", bg.transform);
        RectTransform rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(Mathf.Clamp01(valor), 1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image fImg = fill.AddComponent<Image>();
        fImg.color = cor;
        fImg.raycastTarget = false;
    }

    private GameObject CriarBotaoBloco(Transform parent, string texto, Color cor, Action onClick)
    {
        GameObject btn = CriarUIObjeto("Botao_" + texto, parent);
        LayoutElement le = btn.AddComponent<LayoutElement>();
        le.preferredHeight = 34f;
        le.minHeight = 30f;
        le.flexibleWidth = 1f;

        Image img = btn.AddComponent<Image>();
        img.color = cor;
        AddOutline(btn, new Color(corLinha.r, corLinha.g, corLinha.b, 0.22f), 1f);

        Button b = btn.AddComponent<Button>();
        b.targetGraphic = img;
        if (onClick != null) b.onClick.AddListener(() => onClick());

        Text t = CriarTextoLivre(btn.transform, texto, 11, corTextoPrimario, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.name = "TextoBotao";
        return btn;
    }

    private Button CriarBotao(Transform parent, string texto, Color cor, Action onClick)
    {
        GameObject btn = CriarUIObjeto("Botao_" + texto, parent);
        LayoutElement le = btn.AddComponent<LayoutElement>();
        le.preferredHeight = 50f;
        le.minHeight = 40f;
        le.flexibleWidth = 0f;

        Image img = btn.AddComponent<Image>();
        img.color = cor;
        AddOutline(btn, new Color(1f, 1f, 1f, 0.12f), 1f);

        Button b = btn.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = cor;
        cb.highlightedColor = Color.Lerp(cor, Color.white, 0.18f);
        cb.pressedColor = Color.Lerp(cor, Color.black, 0.24f);
        cb.selectedColor = cb.highlightedColor;
        b.colors = cb;
        if (onClick != null) b.onClick.AddListener(() => onClick());

        CriarTextoLivre(btn.transform, texto, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        return b;
    }

    private GameObject CriarCardBase(Transform parent, float altura, Color cor)
    {
        GameObject go = CriarUIObjeto("Card", parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        if (altura > 0)
        {
            le.preferredHeight = altura;
            le.minHeight = altura;
        }
        else
        {
            le.flexibleHeight = 1f;
        }
        le.flexibleWidth = 1f;

        Image img = go.AddComponent<Image>();
        img.color = cor;
        AddOutline(go, new Color(corLinha.r, corLinha.g, corLinha.b, 0.14f), 1f);
        return go;
    }

    private Text CriarTextoLayout(Transform parent, string texto, int tamanho, Color cor, TextAnchor anchor, FontStyle style, float altura)
    {
        GameObject go = CriarUIObjeto("Texto", parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = altura;
        le.minHeight = Mathf.Min(altura, 18f);

        Text t = go.AddComponent<Text>();
        t.text = texto;
        t.font = ObterFonteParaTexto(texto);
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    private Text CriarTextoLivre(Transform parent, string texto, int tamanho, Color cor, TextAnchor anchor, FontStyle style)
    {
        return CriarTextoLivre(parent, texto, tamanho, cor, anchor, style, 0, 0, 0, 0);
    }

    private Text CriarTextoLivre(Transform parent, string texto, int tamanho, Color cor, TextAnchor anchor, FontStyle style, float left, float top, float right, float bottom)
    {
        GameObject go = CriarUIObjeto("TextoLivre", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        Esticar(rt, left, top, right, bottom);

        Text t = go.AddComponent<Text>();
        t.text = texto;
        t.font = ObterFonteParaTexto(texto);
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    private void AddVertical(GameObject go, int left, int right, int topBottom)
    {
        AddVertical(go, left, right, topBottom, topBottom);
    }

    private void AddVertical(GameObject go, int left, int right, int top, int bottom)
    {
        VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(left, right, top, bottom);
        v.spacing = 3;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
    }

    private GameObject CriarUIObjeto(string nome, Transform parent)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = Vector3.zero;
        return go;
    }

    private void Esticar(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private void AddOutline(GameObject go, Color cor, float distancia)
    {
        Outline outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = cor;
        outline.effectDistance = new Vector2(distancia, -distancia);
    }

    private CanvasGroup GarantirCanvasGroup(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private void LimparFilhos(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    private void InicializarDadosDoMundo()
    {
        if (paises.Count == 0)
        {
            paises.Add(new PaisGoverno { id = 1, nome = "República Atlas", jogador = true, bloco = BlocoGlobal.OrdemAtlas, aliadoPrioritarioId = 2, rivalEstrategicoId = 3, status = StatusGeopolitico.Paz, dinheiro = 39534, comida = 500, petroleo = 3830, aco = 100, armamentos = 500, militaresAtivos = 3250 });
            paises.Add(new PaisGoverno { id = 2, nome = "República Boreal", bloco = BlocoGlobal.OrdemAtlas, status = StatusGeopolitico.Paz, comida = 1800, petroleo = 2600, aco = 700, armamentos = 900, militaresAtivos = 2850 });
            paises.Add(new PaisGoverno { id = 3, nome = "União Carmesim", bloco = BlocoGlobal.PactoSolaris, status = StatusGeopolitico.Crise, comida = 900, petroleo = 4800, aco = 1200, armamentos = 1600, militaresAtivos = 5100 });
            paises.Add(new PaisGoverno { id = 4, nome = "Domínio Valerian", bloco = BlocoGlobal.LigaContinental, status = StatusGeopolitico.Sancoes, comida = 600, petroleo = 900, aco = 1800, armamentos = 2100, militaresAtivos = 3900 });
            paises.Add(new PaisGoverno { id = 5, nome = "Federação Alvorada", bloco = BlocoGlobal.Nenhum, status = StatusGeopolitico.Tensao, comida = 3400, petroleo = 600, aco = 500, armamentos = 350, militaresAtivos = 1700 });
        }

        if (relacoes.Count == 0)
        {
            relacoes.Add(new RelacaoDiplomatica { paisA = 1, paisB = 2, valor = 75, estado = EstadoRelacao.AliancaEstrategica, status = StatusGeopolitico.Paz, tratadoComercial = true, pactoMilitar = true });
            relacoes.Add(new RelacaoDiplomatica { paisA = 1, paisB = 3, valor = -82, estado = EstadoRelacao.CriseMilitar, status = StatusGeopolitico.Crise, tratadoComercial = false, pactoMilitar = false, sancoesAContraB = new List<TipoSancao> { TipoSancao.EmbargoPetroleo, TipoSancao.BloqueioTecnologico } });
            relacoes.Add(new RelacaoDiplomatica { paisA = 1, paisB = 4, valor = -55, estado = EstadoRelacao.Hostilidade, status = StatusGeopolitico.Sancoes, tratadoComercial = false, pactoMilitar = false, sancoesAContraB = new List<TipoSancao> { TipoSancao.EmbargoArmamentos } });
            relacoes.Add(new RelacaoDiplomatica { paisA = 1, paisB = 5, valor = 28, estado = EstadoRelacao.ParceiroComercial, status = StatusGeopolitico.Tensao, tratadoComercial = true, pactoMilitar = false });
        }

        if (notificacoes.Count == 0)
        {
            notificacoes.Add(new NotificacaoGoverno { icone = "⚑", titulo = "Diplomacia", mensagem = "Boreal aceitou cooperação naval.", hora = "Agora", cor = corDestaque });
            notificacoes.Add(new NotificacaoGoverno { icone = "△", titulo = "Sanções", mensagem = "Carmesim sofre impacto econômico.", hora = "Agora", cor = corVermelho });
        }
    }

    private void Notificar(string titulo, string mensagem)
    {
        notificacoes.Insert(0, new NotificacaoGoverno
        {
            icone = "◈",
            titulo = titulo,
            mensagem = mensagem,
            hora = "Agora",
            cor = corDestaque
        });
        while (notificacoes.Count > 8) notificacoes.RemoveAt(notificacoes.Count - 1);
        AtualizarRodape();
    }

    private PaisGoverno ObterPais(int id) { return paises.FirstOrDefault(p => p.id == id); }
    private RelacaoDiplomatica ObterRelacao(int a, int b) { return relacoes.FirstOrDefault(r => (r.paisA == a && r.paisB == b) || (r.paisA == b && r.paisB == a)); }
    private string NomePais(int id) { PaisGoverno p = ObterPais(id); return p != null ? p.nome : "Nenhum"; }

    private string NomeBloco(BlocoGlobal bloco)
    {
        if (bloco == BlocoGlobal.OrdemAtlas) return "Ordem Atlas";
        if (bloco == BlocoGlobal.PactoSolaris) return "Pacto Solaris";
        if (bloco == BlocoGlobal.LigaContinental) return "Liga Continental";
        return "Nenhum";
    }

    private string NomeStatus(StatusGeopolitico s)
    {
        if (s == StatusGeopolitico.Paz) return "Paz";
        if (s == StatusGeopolitico.Tensao) return "Tensão";
        if (s == StatusGeopolitico.Crise) return "Crise";
        if (s == StatusGeopolitico.Sancoes) return "Sanções";
        if (s == StatusGeopolitico.ConflitoLimitado) return "Conflito Limitado";
        if (s == StatusGeopolitico.GuerraAberta) return "Guerra Aberta";
        return s.ToString();
    }

    private string NomeEstadoRelacao(EstadoRelacao e)
    {
        if (e == EstadoRelacao.AliancaEstrategica) return "Aliado Estratégico";
        if (e == EstadoRelacao.ParceiroMilitar) return "Parceiro Militar";
        if (e == EstadoRelacao.ParceiroComercial) return "Parceiro Comercial";
        if (e == EstadoRelacao.CriseMilitar) return "Crise Militar";
        return e.ToString();
    }

    private string NomeAba(CategoriaGoverno cat)
    {
        if (cat == CategoriaGoverno.RelacoesExteriores) return "Relações\nExteriores";
        if (cat == CategoriaGoverno.Aliancas) return "Alianças";
        if (cat == CategoriaGoverno.Sancoes) return "Sanções";
        if (cat == CategoriaGoverno.Economia) return "Economia";
        if (cat == CategoriaGoverno.MercadoGlobal) return "Mercado\nGlobal";
        if (cat == CategoriaGoverno.Interior) return "Interior";
        if (cat == CategoriaGoverno.Defesa) return "Defesa";
        if (cat == CategoriaGoverno.Ciencia) return "Ciência";
        if (cat == CategoriaGoverno.Trabalho) return "Trabalho";
        return cat.ToString();
    }

    private string IconeAba(CategoriaGoverno cat)
    {
        if (cat == CategoriaGoverno.RelacoesExteriores) return "🤝";
        if (cat == CategoriaGoverno.Aliancas) return "⚑";
        if (cat == CategoriaGoverno.Sancoes) return "⚖";
        if (cat == CategoriaGoverno.Economia) return "▣";
        if (cat == CategoriaGoverno.MercadoGlobal) return "⇄";
        if (cat == CategoriaGoverno.Interior) return "⌂";
        if (cat == CategoriaGoverno.Defesa) return "🛡";
        if (cat == CategoriaGoverno.Ciencia) return "⚗";
        if (cat == CategoriaGoverno.Trabalho) return "♟";
        return "•";
    }

    private List<string> ObterSubAbas(CategoriaGoverno cat)
    {
        if (cat == CategoriaGoverno.RelacoesExteriores) return new List<string> { "Resumo", "Nações", "Tratados", "Crises" };
        if (cat == CategoriaGoverno.Aliancas) return new List<string> { "Blocos", "Pactos", "Operações", "Pedidos" };
        if (cat == CategoriaGoverno.Sancoes) return new List<string> { "Visão Geral", "Aplicadas", "Tipos", "Histórico" };
        if (cat == CategoriaGoverno.Economia) return new List<string> { "Tesouro", "Orçamento", "Produção", "Impostos" };
        if (cat == CategoriaGoverno.MercadoGlobal) return new List<string> { "Comprar", "Vender", "Preços", "Rotas" };
        if (cat == CategoriaGoverno.Interior) return new List<string> { "População", "Infraestrutura", "Bem-estar", "Projetos" };
        if (cat == CategoriaGoverno.Defesa) return new List<string> { "Comando", "Exército", "Marinha", "Força Aérea", "Alertas" };
        if (cat == CategoriaGoverno.Ciencia) return new List<string> { "Projetos", "Tecnologias", "Laboratórios", "Fila" };
        if (cat == CategoriaGoverno.Trabalho) return new List<string> { "Empregos", "Setores", "Formação", "Políticas" };
        return new List<string> { "Geral" };
    }

    private Color CorRelacao(int valor)
    {
        if (valor >= 55) return corVerde;
        if (valor >= 15) return corDestaque;
        if (valor > -20) return corAmarelo;
        if (valor > -60) return corLaranja;
        return corVermelho;
    }

    private Color CorStatus(StatusGeopolitico s)
    {
        if (s == StatusGeopolitico.Paz) return corVerde;
        if (s == StatusGeopolitico.Tensao) return corAmarelo;
        if (s == StatusGeopolitico.Crise) return corLaranja;
        if (s == StatusGeopolitico.Sancoes) return corVermelho;
        if (s == StatusGeopolitico.ConflitoLimitado) return corVermelho;
        if (s == StatusGeopolitico.GuerraAberta) return corVermelho;
        return corTextoPrimario;
    }

    private string FormatNumero(int n)
    {
        return n.ToString("N0").Replace(",", ".");
    }
}
