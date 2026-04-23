using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;
using Hegemonia.AI.BrainMaster;

public class MenuConstrucao : MonoBehaviour
{
    [System.Serializable]
    public class ConfigVisualCategoria
    {
        public DadosConstrucao.CategoriaItem categoria;
        public Sprite icone;
        public string glifoFallback = "?";
    }

    // --- CONFIGURAÇÕES VISUAIS (DESIGN MODERNO) ---
    [Header("Design Moderno - Cores & Estilo")]
    public KeyCode teclaAtalho = KeyCode.C;
    
    // Fundo Principal (Glassmorphism Escuro)
    public Color corFundoJanela = new Color(0.12f, 0.12f, 0.15f, 0.95f);
    
    // Cores de Destaque (Neon / Sci-Fi)
    public Color corDestaque = new Color(0.0f, 0.8f, 1.0f, 1.0f); // Ciano Neon
    public Color corTextoPrimario = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color corTextoSecundario = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Cores das Categorias (Sutis)")]
    public Color corCardBase = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    public Color corBordaAtiva = new Color(1f, 1f, 1f, 0.3f);
    public Color corFundoSecundario = new Color(0.07f, 0.1f, 0.13f, 0.94f);
    public Color corBordaCartao = new Color(0.5f, 0.76f, 0.9f, 0.28f);
    public Color corDivisoria = new Color(0.6f, 0.85f, 1f, 0.12f);

    [Header("Abas de Categoria")]
    public List<ConfigVisualCategoria> aparenciasCategorias = new List<ConfigVisualCategoria>();

    [Header("Sistema")]
    public bool autoCarregarFichas = false;
    public List<DadosConstrucao> catalogo = new List<DadosConstrucao>();

    public static List<DadosConstrucao> catalogoGlobal;
    public static bool EstaAberto;

    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private Transform containerAbas;
    private GridLayoutGroup gradeBotoes;
    private RectTransform viewportBotoes;
    private CanvasGroup canvasGroupPainel;
    private InputField campoBusca;
    private Image imagemDetalheIcone;
    private Text textoDetalheIconeFallback;
    private Text textoDetalheNome;
    private Text textoDetalheCategoria;
    private Text textoDetalhePreco;
    private Text textoDetalheTipo;
    private Text textoDetalheVelocidade;
    private Text textoDetalheVida;
    private Text textoDetalhePoderFogo;
    private Text textoDetalheDescricao;
    private DadosConstrucao itemDetalheAtual;
    private bool ignorarEventoBusca;
    
    private GerenteDeJogo gerente;
    private bool menuAberto = false;
    private DadosConstrucao.CategoriaItem categoriaAtual = DadosConstrucao.CategoriaItem.Exercito;
    private string filtroBuscaAtual = string.Empty;
    private Dictionary<string, int> quantidadesPorItem = new Dictionary<string, int>();
    private readonly List<GerenciadorAeroporto> bufferAeroportos = new List<GerenciadorAeroporto>(16);
    private readonly Dictionary<DadosConstrucao.CategoriaItem, ConfigVisualCategoria> lookupVisualCategorias = new Dictionary<DadosConstrucao.CategoriaItem, ConfigVisualCategoria>();

    void Start()
    {
        gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (autoCarregarFichas) CarregarTodasAsFichas();
        GarantirCatalogoValido();

        GarantirConfigsVisuaisCategoria();
        GerarInterfaceCompleta();

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
            if(canvasGroupPainel != null) 
            {
                canvasGroupPainel.alpha = 0;
                canvasGroupPainel.blocksRaycasts = false;
                canvasGroupPainel.interactable = false;
            }
        }

        FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Exercito);
    }

    // (Removido Scanner Global de Prefabs que destruía os arquivos Assets do HD causando Missing Scripts)

    void CarregarTodasAsFichas()
    {
        if (catalogo == null) catalogo = new List<DadosConstrucao>();
        catalogo.Clear();
        
        List<DadosConstrucao> fichasEncontradas = new List<DadosConstrucao>();

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DadosConstrucao");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            DadosConstrucao asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
            if (asset != null) fichasEncontradas.Add(asset);
        }
#else
        fichasEncontradas.AddRange(Resources.FindObjectsOfTypeAll<DadosConstrucao>());
#endif

        foreach (var ficha in fichasEncontradas)
        {
            if (ficha != null && ficha.prefabDaUnidade != null && !string.IsNullOrEmpty(ficha.nomeItem))
            {
                string nm = ficha.nomeItem.ToLower();
                if (nm.Contains("destroc") || nm.Contains("destroç") || nm.Contains("chama") || 
                    ficha.prefabDaUnidade.GetComponent<DestrocosEmChamas>() != null)
                {
                    continue; 
                }

                catalogo.Add(ficha);
                if (!quantidadesPorItem.ContainsKey(ficha.nomeItem))
                    quantidadesPorItem.Add(ficha.nomeItem, 1);
            }
        }
        catalogo = catalogo.OrderBy(f => (int)f.categoria).ThenBy(f => f.nomeItem).ToList();
        catalogoGlobal = new List<DadosConstrucao>(catalogo);
    }

    void GarantirCatalogoValido()
    {
        if (catalogo == null)
        {
            catalogo = new List<DadosConstrucao>();
        }

        catalogo.RemoveAll(item => item == null);

        if (catalogo.Count == 0 && catalogoGlobal != null && catalogoGlobal.Count > 0)
        {
            catalogo = catalogoGlobal
                .Where(item => item != null)
                .OrderBy(item => (int)item.categoria)
                .ThenBy(item => item.nomeItem)
                .ToList();
        }

        if (catalogo.Count == 0 && autoCarregarFichas)
        {
            CarregarTodasAsFichas();
        }

        if ((catalogoGlobal == null || catalogoGlobal.Count == 0) && catalogo.Count > 0)
        {
            catalogoGlobal = new List<DadosConstrucao>(catalogo);
        }
    }

    List<DadosConstrucao> ObterItensDaCategoria(DadosConstrucao.CategoriaItem categoriaDesejada, bool aplicarBusca)
    {
        GarantirCatalogoValido();

        IEnumerable<DadosConstrucao> query = catalogo.Where(item => item != null && item.categoria == categoriaDesejada);
        if (aplicarBusca)
        {
            query = query.Where(ItemPassaFiltroBusca);
        }

        return query.ToList();
    }

    void DefinirTextoBuscaSemEvento(string novoTexto)
    {
        filtroBuscaAtual = novoTexto ?? string.Empty;

        if (campoBusca == null)
        {
            return;
        }

        ignorarEventoBusca = true;
        campoBusca.text = filtroBuscaAtual;
        ignorarEventoBusca = false;
    }

    bool BuscaPodeSerRecuperadaAutomaticamente()
    {
        if (string.IsNullOrWhiteSpace(filtroBuscaAtual))
        {
            return false;
        }

        if (campoBusca == null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(campoBusca.text)
            || !string.Equals(campoBusca.text, filtroBuscaAtual, System.StringComparison.Ordinal);
    }

    void LimparBuscaPresa()
    {
        if (string.IsNullOrEmpty(filtroBuscaAtual) && (campoBusca == null || string.IsNullOrEmpty(campoBusca.text)))
        {
            return;
        }

        DefinirTextoBuscaSemEvento(string.Empty);

        if (campoBusca != null)
        {
            campoBusca.DeactivateInputField();

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == campoBusca.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    void Update()
    {
        if (EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null)
        {
            return;
        }

        if (Input.GetKeyDown(teclaAtalho))
        {
            AlternarMenu(!menuAberto);
        }
    }

    public void AlternarMenu()
    {
        if (painelPrincipal == null) return;
        AlternarMenu(!menuAberto);
    }

    public void AlternarMenu(bool abrir)
    {
        if (painelPrincipal == null) return;
        
        StopAllCoroutines(); 
        
        if (abrir)
        {
            // Fecha todos os outros se for abrir este
            MenuPier menuPier = Object.FindFirstObjectByType<MenuPier>();
            if (menuPier != null) menuPier.FecharMenu();

            MenuMisseis menuMiss = Object.FindFirstObjectByType<MenuMisseis>();
            if (menuMiss != null && MenuMisseis.EstaAberto) menuMiss.CancelarLancamento();

            MenuGoverno menuGov = Object.FindFirstObjectByType<MenuGoverno>();
            if (menuGov != null && MenuGoverno.EstaAberto) menuGov.AlternarMenu(false);

            RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);
            foreach (GerenciadorAeroporto aeroporto in bufferAeroportos)
            {
                if (aeroporto != null)
                {
                    aeroporto.CancelarInteracaoPorConstrucao();
                }
            }

            GarantirCatalogoValido();
            List<DadosConstrucao> itensBaseCategoria = ObterItensDaCategoria(categoriaAtual, false);
            List<DadosConstrucao> itensFiltradosCategoria = ObterItensDaCategoria(categoriaAtual, true);
            if (itensFiltradosCategoria.Count == 0 && itensBaseCategoria.Count > 0 && BuscaPodeSerRecuperadaAutomaticamente())
            {
                LimparBuscaPresa();
            }

            FiltrarPorCategoria(categoriaAtual);
        }
        
        if (canvasGroupPainel != null)
        {
            canvasGroupPainel.blocksRaycasts = abrir;
            canvasGroupPainel.interactable = abrir;
        }

        StartCoroutine(AnimarMenu(abrir));
    }

    IEnumerator AnimarMenu(bool abrir)
    {
        menuAberto = abrir;
        EstaAberto = abrir;

        if (abrir)
        {
            painelPrincipal.SetActive(true);
            if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
        }

        float alphaInicial = canvasGroupPainel.alpha;
        float alphaFinal = abrir ? 1f : 0f;
        float duracao = 0.2f;
        float tempo = 0;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;
            canvasGroupPainel.alpha = Mathf.Lerp(alphaInicial, alphaFinal, tempo / duracao);
            yield return null;
        }

        canvasGroupPainel.alpha = alphaFinal;

        if (!abrir)
        {
            painelPrincipal.SetActive(false);
        }
    }

    void GerarInterfaceCompleta()
    {
        GameObject canvasObj = GameObject.Find("Canvas_Interface");
        if (canvasObj == null)
        {
            Canvas canvasExistente = Object.FindFirstObjectByType<Canvas>();
            if (canvasExistente != null) canvasObj = canvasExistente.gameObject;
            else
            {
                canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        Transform painelAntigo = canvasObj.transform.Find("Painel_Construcao_Moderno");
        if (painelAntigo != null) DestroyImmediate(painelAntigo.gameObject);

        painelPrincipal = CriarRetangulo("Painel_Construcao_Moderno", canvasObj.transform);
        Image imgFundo = painelPrincipal.AddComponent<Image>();
        imgFundo.color = corFundoJanela;

        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();
        canvasGroupPainel.blocksRaycasts = true;
        canvasGroupPainel.interactable = true;

        RectTransform rtPanel = painelPrincipal.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.13f, 0.22f);
        rtPanel.anchorMax = new Vector2(0.93f, 0.92f);
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;

        Outline outline = painelPrincipal.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.82f, 0.94f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = painelPrincipal.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0.65f, 0.85f, 0.14f);
        shadow.effectDistance = new Vector2(0f, -4f);

        VerticalLayoutGroup layoutPrincipal = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutPrincipal.padding = new RectOffset(18, 18, 10, 14);
        layoutPrincipal.spacing = 10;
        layoutPrincipal.childControlHeight = true;
        layoutPrincipal.childControlWidth = true;
        layoutPrincipal.childForceExpandHeight = false;
        layoutPrincipal.childForceExpandWidth = true;

        if (Object.FindFirstObjectByType<ModoDemolicao>() == null)
        {
            GameObject go = new GameObject("ModoDemolicao_Manager");
            go.AddComponent<ModoDemolicao>();
        }

        CriarPlacaTitulo(painelPrincipal.transform);
        CriarLinhaNavegacao(painelPrincipal.transform);
        CriarCorpoPrincipal(painelPrincipal.transform);
    }

    void CriarPlacaTitulo(Transform pai)
    {
        GameObject topoObj = CriarRetangulo("FaixaTitulo", pai);
        LayoutElement leTopo = topoObj.AddComponent<LayoutElement>();
        leTopo.minHeight = 28;
        leTopo.preferredHeight = 28;
        leTopo.flexibleHeight = 0f;

        HorizontalLayoutGroup layoutTopo = topoObj.AddComponent<HorizontalLayoutGroup>();
        layoutTopo.childAlignment = TextAnchor.MiddleCenter;
        layoutTopo.childControlWidth = true;
        layoutTopo.childControlHeight = true;
        layoutTopo.childForceExpandWidth = true;
        layoutTopo.childForceExpandHeight = false;
        layoutTopo.spacing = 10;

        CriarLinhaDecorativa(topoObj.transform);

        GameObject badgeTitulo = CriarRetangulo("BadgeTitulo", topoObj.transform);
        LayoutElement leBadge = badgeTitulo.AddComponent<LayoutElement>();
        leBadge.minWidth = 300;
        leBadge.preferredWidth = 360;
        leBadge.minHeight = 24;
        leBadge.preferredHeight = 24;
        leBadge.flexibleHeight = 0f;

        Image imgBadge = badgeTitulo.AddComponent<Image>();
        imgBadge.color = new Color(0.09f, 0.13f, 0.18f, 0.98f);

        Outline badgeOutline = badgeTitulo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0.56f, 0.84f, 0.96f, 0.28f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        Shadow badgeShadow = badgeTitulo.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0.8f, 1f, 0.2f);
        badgeShadow.effectDistance = new Vector2(0f, -2f);

        GameObject tituloObj = CriarRetangulo("TextoTitulo", badgeTitulo.transform);
        Text titulo = tituloObj.AddComponent<Text>();
        titulo.text = "HEGEMONIA GLOBAL";
        titulo.font = ObterFontePadrao();
        titulo.fontSize = 19;
        titulo.fontStyle = FontStyle.Bold;
        titulo.alignment = TextAnchor.MiddleCenter;
        titulo.color = corTextoPrimario;
        titulo.raycastTarget = false;
        EsticarRectTransform(tituloObj.GetComponent<RectTransform>());

        CriarLinhaDecorativa(topoObj.transform);
    }

    void CriarLinhaDecorativa(Transform pai)
    {
        GameObject linha = CriarRetangulo("Linha", pai);
        LayoutElement leLinha = linha.AddComponent<LayoutElement>();
        leLinha.flexibleWidth = 1f;
        leLinha.minHeight = 1f;
        leLinha.preferredHeight = 1f;

        Image imgLinha = linha.AddComponent<Image>();
        imgLinha.color = corDivisoria;
        imgLinha.raycastTarget = false;
    }

    void CriarLinhaNavegacao(Transform pai)
    {
        GameObject navObj = CriarRetangulo("LinhaNavegacao", pai);
        LayoutElement leNav = navObj.AddComponent<LayoutElement>();
        leNav.minHeight = 30;
        leNav.preferredHeight = 30;
        leNav.flexibleHeight = 0f;

        HorizontalLayoutGroup layoutNav = navObj.AddComponent<HorizontalLayoutGroup>();
        layoutNav.spacing = 6;
        layoutNav.childAlignment = TextAnchor.MiddleCenter;
        layoutNav.childControlWidth = true;
        layoutNav.childControlHeight = true;
        layoutNav.childForceExpandWidth = false;
        layoutNav.childForceExpandHeight = true;

        GameObject abasHost = CriarRetangulo("AbasHost", navObj.transform);
        LayoutElement leAbasHost = abasHost.AddComponent<LayoutElement>();
        leAbasHost.flexibleWidth = 1f;

        HorizontalLayoutGroup layoutAbas = abasHost.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childControlWidth = true;
        layoutAbas.childControlHeight = true;
        layoutAbas.childForceExpandWidth = false;
        layoutAbas.childForceExpandHeight = true;
        layoutAbas.childAlignment = TextAnchor.MiddleLeft;
        layoutAbas.spacing = 5;
        containerAbas = abasHost.transform;

        foreach (DadosConstrucao.CategoriaItem cat in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            CriarBotaoAbaModerno(cat, containerAbas);
        }

        CriarBuscaVisual(navObj.transform);
    }

    void CriarBuscaVisual(Transform pai)
    {
        GameObject buscaObj = CriarRetangulo("BuscaVisual", pai);
        LayoutElement leBusca = buscaObj.AddComponent<LayoutElement>();
        leBusca.minWidth = 120;
        leBusca.preferredWidth = 140;
        leBusca.minHeight = 22;
        leBusca.preferredHeight = 22;
        leBusca.flexibleHeight = 0f;

        Image imgBusca = buscaObj.AddComponent<Image>();
        imgBusca.color = new Color(0.08f, 0.12f, 0.16f, 0.96f);
        imgBusca.raycastTarget = true;

        Outline outline = buscaObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        outline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutBusca = buscaObj.AddComponent<HorizontalLayoutGroup>();
        layoutBusca.padding = new RectOffset(5, 5, 0, 0);
        layoutBusca.spacing = 4;
        layoutBusca.childAlignment = TextAnchor.MiddleLeft;
        layoutBusca.childControlWidth = true;
        layoutBusca.childControlHeight = true;
        layoutBusca.childForceExpandWidth = false;
        layoutBusca.childForceExpandHeight = true;

        GameObject iconeObj = CriarRetangulo("IconeBusca", buscaObj.transform);
        LayoutElement leIcone = iconeObj.AddComponent<LayoutElement>();
        leIcone.minWidth = 10;
        leIcone.preferredWidth = 10;

        Text iconeBusca = iconeObj.AddComponent<Text>();
        iconeBusca.text = "Q";
        iconeBusca.font = ObterFontePadrao();
        iconeBusca.fontSize = 8;
        iconeBusca.fontStyle = FontStyle.Bold;
        iconeBusca.alignment = TextAnchor.MiddleCenter;
        iconeBusca.color = corTextoSecundario;
        iconeBusca.raycastTarget = false;

        GameObject inputAreaObj = CriarRetangulo("InputAreaBusca", buscaObj.transform);
        LayoutElement leInputArea = inputAreaObj.AddComponent<LayoutElement>();
        leInputArea.flexibleWidth = 1f;

        Image imgInput = inputAreaObj.AddComponent<Image>();
        imgInput.color = Color.clear;
        imgInput.raycastTarget = true;

        InputField inputBusca = inputAreaObj.AddComponent<InputField>();
        inputBusca.lineType = InputField.LineType.SingleLine;
        inputBusca.text = filtroBuscaAtual;
        inputBusca.targetGraphic = imgInput;

        GameObject textoObj = CriarRetangulo("TextoBusca", inputAreaObj.transform);
        Text textoBusca = textoObj.AddComponent<Text>();
        textoBusca.font = ObterFontePadrao();
        textoBusca.fontSize = 8;
        textoBusca.alignment = TextAnchor.MiddleLeft;
        textoBusca.color = corTextoPrimario;
        textoBusca.raycastTarget = false;
        EsticarRectTransform(textoObj.GetComponent<RectTransform>());

        GameObject placeholderObj = CriarRetangulo("PlaceholderBusca", inputAreaObj.transform);
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.text = "Pesquisar Unidade...";
        placeholder.font = ObterFontePadrao();
        placeholder.fontSize = 8;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(corTextoSecundario.r, corTextoSecundario.g, corTextoSecundario.b, 0.75f);
        placeholder.raycastTarget = false;
        EsticarRectTransform(placeholderObj.GetComponent<RectTransform>());

        inputBusca.textComponent = textoBusca;
        inputBusca.placeholder = placeholder;
        inputBusca.onValueChanged.AddListener(AtualizarFiltroBusca);
        campoBusca = inputBusca;

        EventTrigger triggerBusca = buscaObj.AddComponent<EventTrigger>();
        triggerBusca.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry cliqueBusca = new EventTrigger.Entry();
        cliqueBusca.eventID = EventTriggerType.PointerDown;
        cliqueBusca.callback.AddListener((_) =>
        {
            if (campoBusca != null)
            {
                campoBusca.Select();
                campoBusca.ActivateInputField();
            }
        });
        triggerBusca.triggers.Add(cliqueBusca);
    }

    void CriarCorpoPrincipal(Transform pai)
    {
        GameObject corpoObj = CriarRetangulo("CorpoMenu", pai);
        LayoutElement leCorpo = corpoObj.AddComponent<LayoutElement>();
        leCorpo.flexibleHeight = 1f;

        HorizontalLayoutGroup layoutCorpo = corpoObj.AddComponent<HorizontalLayoutGroup>();
        layoutCorpo.spacing = 12;
        layoutCorpo.childControlWidth = true;
        layoutCorpo.childControlHeight = true;
        layoutCorpo.childForceExpandWidth = false;
        layoutCorpo.childForceExpandHeight = true;

        GameObject colunaPrincipal = CriarRetangulo("ColunaPrincipal", corpoObj.transform);
        LayoutElement leColunaPrincipal = colunaPrincipal.AddComponent<LayoutElement>();
        leColunaPrincipal.minWidth = 0;
        leColunaPrincipal.preferredWidth = 0;
        leColunaPrincipal.flexibleWidth = 1f;

        Image imgColuna = colunaPrincipal.AddComponent<Image>();
        imgColuna.color = new Color(0.04f, 0.06f, 0.08f, 0.48f);

        Outline outlineColuna = colunaPrincipal.AddComponent<Outline>();
        outlineColuna.effectColor = corDivisoria;
        outlineColuna.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layoutColuna = colunaPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutColuna.padding = new RectOffset(10, 10, 10, 10); // Margem restaurada (padding agora na Grade!)
        layoutColuna.spacing = 8;
        layoutColuna.childControlWidth = true;
        layoutColuna.childControlHeight = true;
        layoutColuna.childForceExpandWidth = true;
        layoutColuna.childForceExpandHeight = false;

        CriarAreaScroll(colunaPrincipal.transform);
        CriarPainelLateralDecorativo(corpoObj.transform);
    }

    void CriarAreaScroll(Transform pai)
    {
        GameObject bodyObj = CriarRetangulo("Body_Scroll", pai);
        LayoutElement leBody = bodyObj.AddComponent<LayoutElement>();
        leBody.flexibleHeight = 1f;

        Image imgBody = bodyObj.AddComponent<Image>();
        imgBody.color = new Color(0f, 0f, 0f, 0.18f);
        imgBody.raycastTarget = true;

        ScrollRect sr = bodyObj.AddComponent<ScrollRect>();
        sr.scrollSensitivity = 15f;
        sr.decelerationRate = 0.135f;
        sr.elasticity = 0.1f;
        sr.inertia = true;
        sr.horizontal = false;
        sr.vertical = true;

        GameObject viewport = CriarRetangulo("Viewport", bodyObj.transform);
        Image imgView = viewport.AddComponent<Image>();
        imgView.color = Color.clear;
        imgView.raycastTarget = false;
        viewport.AddComponent<RectMask2D>();

        RectTransform rtView = viewport.GetComponent<RectTransform>();
        EsticarRectTransform(rtView);
        rtView.offsetMin = new Vector2(8f, 8f);
        rtView.offsetMax = new Vector2(-8f, -8f);
        viewportBotoes = rtView;

        GameObject content = CriarRetangulo("Content_Grid", viewport.transform);
        containerBotoes = content.transform;

        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0f, 1f);
        rtContent.anchorMax = new Vector2(1f, 1f);
        rtContent.pivot = new Vector2(0f, 1f); // PIVOT EM 0 IMPEDE QUE A GRADE "VAZE" PARA A ESQUERDA!

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(138f, 186f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(30, 10, 10, 20); // 5% movido para a esquerda (de 60 foi pra 30)
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.Flexible; // Permite preencher todo o espaço disponível sem limite
        gradeBotoes = grid;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = rtView;
    }

    void CriarPainelLateralDecorativo(Transform pai)
    {
        GameObject lateralSlotObj = CriarRetangulo("PainelLateralSlot", pai);
        LayoutElement leSlot = lateralSlotObj.AddComponent<LayoutElement>();
        leSlot.minWidth = 190; // Ainda MAIS largo para acomodar fontes maiores e imagem gigante
        leSlot.preferredWidth = 200;
        leSlot.flexibleWidth = 0f;

        GameObject lateralObj = CriarRetangulo("PainelLateral", lateralSlotObj.transform);
        EsticarRectTransform(lateralObj.GetComponent<RectTransform>());

        CanvasGroup group = lateralObj.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Image imgLateral = lateralObj.AddComponent<Image>();
        imgLateral.color = new Color(0.05f, 0.09f, 0.12f, 0.92f);
        imgLateral.raycastTarget = false;

        Outline outline = lateralObj.AddComponent<Outline>();
        outline.effectColor = corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layoutLateral = lateralObj.AddComponent<VerticalLayoutGroup>();
        layoutLateral.padding = new RectOffset(6, 6, 6, 6);
        layoutLateral.spacing = 5;
        layoutLateral.childControlWidth = true;
        layoutLateral.childControlHeight = true;
        layoutLateral.childForceExpandWidth = true;
        layoutLateral.childForceExpandHeight = false;

        CriarCabecalhoLateral("Ficha do Item", lateralObj.transform);

        GameObject previewObj = CriarCartaoLateral("PreviewItem", lateralObj.transform);
        LayoutElement lePreview = previewObj.AddComponent<LayoutElement>();
        lePreview.minHeight = 150; // Quase o dobro de altura pro preview
        lePreview.preferredHeight = 150;

        GameObject iconFrame = CriarRetangulo("IconeFrame", previewObj.transform);
        LayoutElement leIcone = iconFrame.AddComponent<LayoutElement>();
        leIcone.minHeight = 100; // Imagem gigante agora, mais alta que as dos botões (84)
        leIcone.preferredHeight = 100;

        Image imgFrame = iconFrame.AddComponent<Image>();
        imgFrame.color = new Color(1f, 1f, 1f, 0.04f);
        imgFrame.raycastTarget = false;

        Outline outlineFrame = iconFrame.AddComponent<Outline>();
        outlineFrame.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outlineFrame.effectDistance = new Vector2(1f, -1f);

        GameObject iconSpriteObj = CriarRetangulo("IconeSprite", iconFrame.transform);
        imagemDetalheIcone = iconSpriteObj.AddComponent<Image>();
        imagemDetalheIcone.preserveAspect = true;
        imagemDetalheIcone.raycastTarget = false;
        EsticarRectTransform(iconSpriteObj.GetComponent<RectTransform>());
        iconSpriteObj.GetComponent<RectTransform>().offsetMin = new Vector2(5f, 5f);
        iconSpriteObj.GetComponent<RectTransform>().offsetMax = new Vector2(-5f, -5f);

        GameObject iconFallbackObj = CriarRetangulo("IconeFallback", iconFrame.transform);
        textoDetalheIconeFallback = iconFallbackObj.AddComponent<Text>();
        textoDetalheIconeFallback.font = ObterFontePadrao();
        textoDetalheIconeFallback.fontSize = 32; // Glifo cresce também
        textoDetalheIconeFallback.fontStyle = FontStyle.Bold;
        textoDetalheIconeFallback.alignment = TextAnchor.MiddleCenter;
        textoDetalheIconeFallback.color = corTextoSecundario;
        textoDetalheIconeFallback.raycastTarget = false;
        EsticarRectTransform(iconFallbackObj.GetComponent<RectTransform>());

        GameObject nomeObj = CriarRetangulo("NomeDetalhe", previewObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 24; // Mais espaço pro texto
        leNome.preferredHeight = 24;

        textoDetalheNome = nomeObj.AddComponent<Text>();
        textoDetalheNome.font = ObterFontePadrao();
        textoDetalheNome.fontSize = 15; // Nome grandão
        textoDetalheNome.fontStyle = FontStyle.Bold;
        textoDetalheNome.alignment = TextAnchor.MiddleCenter;
        textoDetalheNome.color = corTextoPrimario;
        textoDetalheNome.resizeTextForBestFit = true;
        textoDetalheNome.resizeTextMinSize = 9;
        textoDetalheNome.resizeTextMaxSize = 15;
        textoDetalheNome.raycastTarget = false;

        GameObject categoriaObj = CriarRetangulo("CategoriaDetalhe", previewObj.transform);
        LayoutElement leCategoria = categoriaObj.AddComponent<LayoutElement>();
        leCategoria.minHeight = 16;
        leCategoria.preferredHeight = 16;

        textoDetalheCategoria = categoriaObj.AddComponent<Text>();
        textoDetalheCategoria.font = ObterFontePadrao();
        textoDetalheCategoria.fontSize = 11; // fonte categoria legivel
        textoDetalheCategoria.alignment = TextAnchor.MiddleCenter;
        textoDetalheCategoria.color = corTextoSecundario;
        textoDetalheCategoria.raycastTarget = false;

        CriarSeparadorLateral(lateralObj.transform);

        GameObject infoObj = CriarCartaoLateral("InfoItem", lateralObj.transform);
        textoDetalhePreco = CriarLinhaInfoDetalhe("Custo", infoObj.transform);
        textoDetalheTipo = CriarLinhaInfoDetalhe("Tipo", infoObj.transform);
        textoDetalheVelocidade = CriarLinhaInfoDetalhe("Velocidade", infoObj.transform);
        textoDetalheVida = CriarLinhaInfoDetalhe("Blindagem", infoObj.transform);
        textoDetalhePoderFogo = CriarLinhaInfoDetalhe("Poder Ofensivo", infoObj.transform);

        GameObject descricaoBox = CriarCartaoLateral("DescricaoItem", lateralObj.transform);
        LayoutElement leDescricaoBox = descricaoBox.AddComponent<LayoutElement>();
        leDescricaoBox.minHeight = 110;
        leDescricaoBox.preferredHeight = 110;

        CriarCabecalhoLateral("Serve Para", descricaoBox.transform);

        GameObject descricaoObj = CriarRetangulo("TextoDescricao", descricaoBox.transform);
        LayoutElement leDescricao = descricaoObj.AddComponent<LayoutElement>();
        leDescricao.minHeight = 52;
        leDescricao.preferredHeight = 52;

        textoDetalheDescricao = descricaoObj.AddComponent<Text>();
        textoDetalheDescricao.font = ObterFontePadrao();
        textoDetalheDescricao.fontSize = 11; // fonte da descrição
        textoDetalheDescricao.alignment = TextAnchor.UpperLeft;
        textoDetalheDescricao.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoDetalheDescricao.verticalOverflow = VerticalWrapMode.Truncate;
        textoDetalheDescricao.color = corTextoPrimario;
        textoDetalheDescricao.raycastTarget = false;

        AtualizarPainelDetalhes(null);
    }

    void CriarCabecalhoLateral(string titulo, Transform pai)
    {
        GameObject tituloObj = CriarRetangulo("Titulo_" + titulo, pai);
        LayoutElement leTitulo = tituloObj.AddComponent<LayoutElement>();
        leTitulo.minHeight = 14;
        leTitulo.preferredHeight = 14;

        Text texto = tituloObj.AddComponent<Text>();
        texto.text = titulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 11; // fonte cabecalho
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.color = corTextoSecundario;
        texto.raycastTarget = false;
    }

    void CriarListaDecorativa(string rotulo, bool ativa, Transform pai)
    {
        GameObject itemObj = CriarRetangulo("Item_" + rotulo, pai);
        LayoutElement leItem = itemObj.AddComponent<LayoutElement>();
        leItem.minHeight = 22;
        leItem.preferredHeight = 22;

        HorizontalLayoutGroup layoutItem = itemObj.AddComponent<HorizontalLayoutGroup>();
        layoutItem.spacing = 8;
        layoutItem.childAlignment = TextAnchor.MiddleLeft;
        layoutItem.childControlWidth = false;
        layoutItem.childControlHeight = true;
        layoutItem.childForceExpandWidth = false;
        layoutItem.childForceExpandHeight = true;

        GameObject bulletObj = CriarRetangulo("Bullet", itemObj.transform);
        LayoutElement leBullet = bulletObj.AddComponent<LayoutElement>();
        leBullet.minWidth = 16;
        leBullet.preferredWidth = 16;

        Text bullet = bulletObj.AddComponent<Text>();
        bullet.text = ativa ? "[x]" : "[ ]";
        bullet.font = ObterFontePadrao();
        bullet.fontSize = 10;
        bullet.alignment = TextAnchor.MiddleCenter;
        bullet.color = ativa ? corDestaque : corTextoSecundario;
        bullet.raycastTarget = false;

        GameObject textoObj = CriarRetangulo("Texto", itemObj.transform);
        LayoutElement leTexto = textoObj.AddComponent<LayoutElement>();
        leTexto.flexibleWidth = 1f;

        Text texto = textoObj.AddComponent<Text>();
        texto.text = rotulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 11;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.color = ativa ? corTextoPrimario : corTextoSecundario;
        texto.raycastTarget = false;
    }

    void CriarChipDecorativo(string rotulo, bool ativo, Transform pai)
    {
        GameObject chipObj = CriarRetangulo("Chip_" + rotulo, pai);
        LayoutElement leChip = chipObj.AddComponent<LayoutElement>();
        leChip.minWidth = 34;
        leChip.preferredWidth = 34;
        leChip.minHeight = 24;
        leChip.preferredHeight = 24;

        Image imgChip = chipObj.AddComponent<Image>();
        imgChip.color = ativo
            ? new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.24f)
            : new Color(1f, 1f, 1f, 0.06f);
        imgChip.raycastTarget = false;

        Outline outline = chipObj.AddComponent<Outline>();
        outline.effectColor = ativo ? new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.35f) : corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textoObj = CriarRetangulo("Texto", chipObj.transform);
        Text texto = textoObj.AddComponent<Text>();
        texto.text = rotulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 10;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = ativo ? corTextoPrimario : corTextoSecundario;
        texto.raycastTarget = false;
        EsticarRectTransform(textoObj.GetComponent<RectTransform>());
    }

    void CriarSeparadorLateral(Transform pai)
    {
        GameObject separador = CriarRetangulo("Separador", pai);
        LayoutElement leSeparador = separador.AddComponent<LayoutElement>();
        leSeparador.minHeight = 1;
        leSeparador.preferredHeight = 1;

        Image imgSeparador = separador.AddComponent<Image>();
        imgSeparador.color = corDivisoria;
        imgSeparador.raycastTarget = false;
    }

    GameObject CriarCartaoLateral(string nome, Transform pai)
    {
        GameObject cardObj = CriarRetangulo(nome, pai);
        Image imgCard = cardObj.AddComponent<Image>();
        imgCard.color = new Color(1f, 1f, 1f, 0.04f);
        imgCard.raycastTarget = false;

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = cardObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 3;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return cardObj;
    }

    Text CriarLinhaInfoDetalhe(string rotulo, Transform pai)
    {
        GameObject linhaObj = CriarRetangulo("Linha_" + rotulo, pai);
        LayoutElement leLinha = linhaObj.AddComponent<LayoutElement>();
        leLinha.minHeight = 20; // Altura da linha de status
        leLinha.preferredHeight = 20;

        HorizontalLayoutGroup layoutLinha = linhaObj.AddComponent<HorizontalLayoutGroup>();
        layoutLinha.spacing = 2;
        layoutLinha.childAlignment = TextAnchor.MiddleLeft;
        layoutLinha.childControlWidth = true;
        layoutLinha.childControlHeight = true;
        layoutLinha.childForceExpandWidth = false;
        layoutLinha.childForceExpandHeight = true;

        GameObject rotuloObj = CriarRetangulo("Rotulo", linhaObj.transform);
        LayoutElement leRotulo = rotuloObj.AddComponent<LayoutElement>();
        leRotulo.minWidth = 72; // Muito mais largo para caber "Velocidade", "Blindagem" sem quebrar linha
        leRotulo.preferredWidth = 72;

        Text textoRotulo = rotuloObj.AddComponent<Text>();
        textoRotulo.text = rotulo;
        textoRotulo.font = ObterFontePadrao();
        textoRotulo.fontSize = 11; // Etiqueta tipo "Tipo", "Custo"
        textoRotulo.fontStyle = FontStyle.Bold;
        textoRotulo.alignment = TextAnchor.MiddleLeft;
        textoRotulo.color = corTextoSecundario;
        textoRotulo.raycastTarget = false;

        GameObject valorObj = CriarRetangulo("Valor", linhaObj.transform);
        LayoutElement leValor = valorObj.AddComponent<LayoutElement>();
        leValor.flexibleWidth = 1f;

        Text textoValor = valorObj.AddComponent<Text>();
        textoValor.font = ObterFontePadrao();
        textoValor.fontSize = 12; // Valor real em si
        textoValor.alignment = TextAnchor.MiddleRight;
        textoValor.color = corTextoPrimario;
        textoValor.resizeTextForBestFit = true;
        textoValor.resizeTextMinSize = 9;
        textoValor.resizeTextMaxSize = 12;
        textoValor.raycastTarget = false;

        return textoValor;
    }

    void AtualizarFiltroBusca(string novoTexto)
    {
        if (ignorarEventoBusca)
        {
            return;
        }

        filtroBuscaAtual = novoTexto ?? string.Empty;

        if (campoBusca != null && campoBusca.text != filtroBuscaAtual)
        {
            DefinirTextoBuscaSemEvento(filtroBuscaAtual);
        }

        FiltrarPorCategoria(categoriaAtual);
    }

    bool ItemPassaFiltroBusca(DadosConstrucao item)
    {
        if (item == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filtroBuscaAtual))
        {
            return true;
        }

        string filtro = filtroBuscaAtual.Trim();
        if (filtro.Length == 0)
        {
            return true;
        }

        return (!string.IsNullOrEmpty(item.nomeItem) && item.nomeItem.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(item.descricao) && item.descricao.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0)
            || ObterRotuloCategoria(item.categoria).IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void AjustarGradeAoEspacoDisponivel()
    {
        if (gradeBotoes == null || viewportBotoes == null)
        {
            return;
        }

        float larguraViewport = viewportBotoes.rect.width;
        if (larguraViewport <= 0f)
        {
            return;
        }

        float larguraUtil = larguraViewport - gradeBotoes.padding.left - gradeBotoes.padding.right;
        float bloco = gradeBotoes.cellSize.x + gradeBotoes.spacing.x;
        int colunas = Mathf.FloorToInt((larguraUtil + gradeBotoes.spacing.x) / bloco);
        colunas = Mathf.Max(colunas, 4); // Remove o teto de 7, permite quantas colunas couberem na tela!

        if (gradeBotoes.constraintCount != colunas && gradeBotoes.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            gradeBotoes.constraintCount = colunas;
        }
    }

    void AtualizarPainelDetalhes(DadosConstrucao item)
    {
        if (textoDetalheNome == null)
        {
            return;
        }

        if (item != null && item == itemDetalheAtual)
        {
            return;
        }

        itemDetalheAtual = item;

        if (item == null)
        {
            if (imagemDetalheIcone != null)
            {
                imagemDetalheIcone.sprite = null;
                imagemDetalheIcone.color = Color.clear;
            }

            if (textoDetalheIconeFallback != null)
            {
                textoDetalheIconeFallback.text = ObterGlifoPadrao(categoriaAtual);
            }

            textoDetalheNome.text = "Selecione um item";
            textoDetalheCategoria.text = ObterRotuloCategoria(categoriaAtual);
            textoDetalhePreco.text = "--";
            textoDetalheTipo.text = "--";
            textoDetalheVelocidade.text = "--";
            if (textoDetalheVida != null) textoDetalheVida.text = "--";
            if (textoDetalhePoderFogo != null) textoDetalhePoderFogo.text = "--";
            textoDetalheDescricao.text = "Passe o mouse sobre um card para ver velocidade, funcao e detalhes da unidade.";
            return;
        }

        if (imagemDetalheIcone != null)
        {
            imagemDetalheIcone.sprite = item.icone;
            imagemDetalheIcone.color = item.icone != null ? Color.white : Color.clear;
        }

        if (textoDetalheIconeFallback != null)
        {
            ConfigVisualCategoria config = ObterConfigVisualCategoria(item.categoria);
            textoDetalheIconeFallback.text = item.icone == null
                ? (config != null ? config.glifoFallback : ObterGlifoPadrao(item.categoria))
                : string.Empty;
        }

        textoDetalheNome.text = item.nomeItem;
        textoDetalheCategoria.text = ObterRotuloCategoria(item.categoria);
        textoDetalhePreco.text = "$ " + item.preco;
        textoDetalheTipo.text = ObterTipoItem(item);
        textoDetalheVelocidade.text = ObterTextoVelocidadeItem(item);
        if (textoDetalheVida != null) textoDetalheVida.text = ObterTextoVidaItem(item);
        if (textoDetalhePoderFogo != null) textoDetalhePoderFogo.text = ObterTextoPoderFogoItem(item);
        textoDetalheDescricao.text = ObterDescricaoDetalheItem(item);
    }

    void AdicionarEventoHoverDetalhes(GameObject alvo, DadosConstrucao item)
    {
        if (alvo == null || item == null)
        {
            return;
        }

        EventTrigger trigger = alvo.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = alvo.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((_) => AtualizarPainelDetalhes(item));
        trigger.triggers.Add(entry);
    }

    void CriarBotaoAbaModerno(DadosConstrucao.CategoriaItem categoria, Transform pai)
    {
        GameObject btnObj = CriarRetangulo("Aba_" + categoria, pai);
        LayoutElement leBotao = btnObj.AddComponent<LayoutElement>();
        leBotao.minWidth = 60;
        leBotao.preferredWidth = 60;
        leBotao.minHeight = 28; // Trazendo um pouco do tamanho de volta (+10% perceptível)
        leBotao.preferredHeight = 28;
        leBotao.flexibleHeight = 0f;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.05f);

        Button btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.98f);
        colors.pressedColor = new Color(0.85f, 0.95f, 1f, 0.95f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow glow = btnObj.AddComponent<Shadow>();
        glow.effectColor = new Color(0f, 0.75f, 1f, 0.08f);
        glow.effectDistance = new Vector2(0f, -2f);

        GameObject accentObj = CriarRetangulo("Accent", btnObj.transform);
        Image accent = accentObj.AddComponent<Image>();
        accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0f);
        accent.raycastTarget = false;
        RectTransform rtAccent = accentObj.GetComponent<RectTransform>();
        rtAccent.anchorMin = new Vector2(0f, 0f);
        rtAccent.anchorMax = new Vector2(1f, 0f);
        rtAccent.pivot = new Vector2(0.5f, 0f);
        rtAccent.sizeDelta = new Vector2(0f, 2f);

        GameObject conteudoObj = CriarRetangulo("Conteudo", btnObj.transform);
        RectTransform rtConteudo = conteudoObj.GetComponent<RectTransform>();
        EsticarRectTransform(rtConteudo);
        rtConteudo.offsetMin = new Vector2(0f, 2f);

        VerticalLayoutGroup layoutConteudo = conteudoObj.AddComponent<VerticalLayoutGroup>();
        layoutConteudo.padding = new RectOffset(2, 2, 3, 2);
        layoutConteudo.spacing = 1;
        layoutConteudo.childAlignment = TextAnchor.MiddleCenter;
        layoutConteudo.childControlWidth = true;
        layoutConteudo.childControlHeight = true;
        layoutConteudo.childForceExpandWidth = true;
        layoutConteudo.childForceExpandHeight = false;

        GameObject iconeFrame = CriarRetangulo("Icone", conteudoObj.transform);
        LayoutElement leIcone = iconeFrame.AddComponent<LayoutElement>();
        leIcone.minHeight = 12; // Compensando
        leIcone.preferredHeight = 12;

        ConfigVisualCategoria config = ObterConfigVisualCategoria(categoria);
        if (config != null && config.icone != null)
        {
            GameObject spriteObj = CriarRetangulo("IconeSprite", iconeFrame.transform);
            Image imgIcone = spriteObj.AddComponent<Image>();
            imgIcone.sprite = config.icone;
            imgIcone.preserveAspect = true;
            imgIcone.color = new Color(0.88f, 0.94f, 1f, 0.95f);
            imgIcone.raycastTarget = false;
            EsticarRectTransform(spriteObj.GetComponent<RectTransform>());
        }
        else
        {
            GameObject glyphObj = CriarRetangulo("IconeGlyph", iconeFrame.transform);
            Text glyph = glyphObj.AddComponent<Text>();
            glyph.text = config != null ? config.glifoFallback : ObterGlifoPadrao(categoria);
            glyph.font = ObterFontePadrao();
            glyph.alignment = TextAnchor.MiddleCenter;
            glyph.fontSize = 9;
            glyph.fontStyle = FontStyle.Bold;
            glyph.color = corTextoSecundario;
            glyph.raycastTarget = false;
            EsticarRectTransform(glyphObj.GetComponent<RectTransform>());
        }

        GameObject txtObj = CriarRetangulo("Texto", conteudoObj.transform);
        LayoutElement leTexto = txtObj.AddComponent<LayoutElement>();
        leTexto.minHeight = 10;
        leTexto.preferredHeight = 10;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = ObterRotuloCategoria(categoria);
        txt.font = ObterFontePadrao();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 6;
        txt.fontStyle = FontStyle.Bold;
        txt.color = corTextoSecundario;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 5;
        txt.resizeTextMaxSize = 6;
        txt.raycastTarget = false;

        btn.onClick.AddListener(() => 
        {
            if(ModoDemolicao.Instancia) ModoDemolicao.Instancia.AlternarModo(false); 
            
            if (menuAberto && categoriaAtual == categoria)
            {
                AlternarMenu(false); // Clicou na mesma aba, fecha o menu.
            }
            else
            {
                if (!menuAberto) AlternarMenu(true); // Se tiver fechado, abre.
                FiltrarPorCategoria(categoria);
            }
        });
    }

    void AtualizarVisualAbas(DadosConstrucao.CategoriaItem catAtiva)
    {
        if (containerAbas == null) return;
        foreach (Transform aba in containerAbas)
        {
            Image img = aba.GetComponent<Image>();
            Outline outline = aba.GetComponent<Outline>();
            Shadow shadow = aba.GetComponent<Shadow>();
            Text txt = aba.Find("Conteudo/Texto") != null ? aba.Find("Conteudo/Texto").GetComponent<Text>() : null;
            Image accent = aba.Find("Accent") != null ? aba.Find("Accent").GetComponent<Image>() : null;
            Image spriteIcone = aba.Find("Conteudo/Icone/IconeSprite") != null ? aba.Find("Conteudo/Icone/IconeSprite").GetComponent<Image>() : null;
            Text glyphIcone = aba.Find("Conteudo/Icone/IconeGlyph") != null ? aba.Find("Conteudo/Icone/IconeGlyph").GetComponent<Text>() : null;

            bool ehAtiva = aba.name == "Aba_" + catAtiva;

            if (ehAtiva)
            {
                img.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.18f);
                if (outline != null) outline.effectColor = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.45f);
                if (shadow != null) shadow.effectColor = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.16f);
                if (accent != null) accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.95f);
                if (txt != null)
                {
                    txt.color = corTextoPrimario;
                    txt.fontSize = 6;
                }
                if (spriteIcone != null) spriteIcone.color = Color.white;
                if (glyphIcone != null) glyphIcone.color = corDestaque;
            }
            else
            {
                img.color = new Color(1f, 1f, 1f, 0.05f);
                if (outline != null) outline.effectColor = corDivisoria;
                if (shadow != null) shadow.effectColor = new Color(0f, 0.75f, 1f, 0.08f);
                if (accent != null) accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0f);
                if (txt != null)
                {
                    txt.color = corTextoSecundario;
                    txt.fontSize = 6;
                }
                if (spriteIcone != null) spriteIcone.color = new Color(0.78f, 0.85f, 0.9f, 0.78f);
                if (glyphIcone != null) glyphIcone.color = corTextoSecundario;
            }
        }
    }

    public void FiltrarPorCategoria(DadosConstrucao.CategoriaItem categoriaDesejada)
    {
        categoriaAtual = categoriaDesejada;
        AtualizarVisualAbas(categoriaDesejada);
        GarantirCatalogoValido();

        if (containerBotoes == null) return;

        foreach (Transform child in containerBotoes) Destroy(child.gameObject);

        List<DadosConstrucao> itensDaCategoria = ObterItensDaCategoria(categoriaDesejada, false);
        List<DadosConstrucao> itensFiltrados = string.IsNullOrWhiteSpace(filtroBuscaAtual)
            ? itensDaCategoria
            : ObterItensDaCategoria(categoriaDesejada, true);

        DadosConstrucao primeiroItemCategoria = null;
        foreach (DadosConstrucao item in itensFiltrados)
        {
            if (item != null)
            {
                if (primeiroItemCategoria == null)
                {
                    primeiroItemCategoria = item;
                }
                CriarCardItemModerno(item);
            }
        }

        AtualizarPainelDetalhes(primeiroItemCategoria);
        
        StartCoroutine(AtualizarLayouts());
    }

    IEnumerator AtualizarLayouts()
    {
        yield return new WaitForEndOfFrame();
        AjustarGradeAoEspacoDisponivel();
        if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
    }

    void CriarCardItemModerno(DadosConstrucao item)
    {
        GameObject cardObj = CriarRetangulo("Card_" + item.nomeItem, containerBotoes);
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;
        imgBg.raycastTarget = true;

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = corBordaCartao;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = cardObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0.65f, 0.85f, 0.08f);
        shadow.effectDistance = new Vector2(0f, -2f);

        Button btnCard = cardObj.AddComponent<Button>();
        btnCard.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btnCard.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.98f, 1f, 1f);
        colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        btnCard.colors = colors;
        btnCard.onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnCard.onClick.AddListener(() => ConstruirItem(item, imgBg));
        AdicionarEventoHoverDetalhes(cardObj, item);

        VerticalLayoutGroup layoutCard = cardObj.AddComponent<VerticalLayoutGroup>();
        layoutCard.padding = new RectOffset(7, 7, 7, 7);
        layoutCard.spacing = 4;
        layoutCard.childControlHeight = true;
        layoutCard.childControlWidth = true;
        layoutCard.childForceExpandWidth = true;
        layoutCard.childForceExpandHeight = false;

        GameObject brilhoTopo = CriarRetangulo("BrilhoTopo", cardObj.transform);
        LayoutElement leBrilho = brilhoTopo.AddComponent<LayoutElement>();
        leBrilho.minHeight = 2;
        leBrilho.preferredHeight = 2;
        Image imgBrilho = brilhoTopo.AddComponent<Image>();
        imgBrilho.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.65f);
        imgBrilho.raycastTarget = false;

        GameObject iconArea = CriarRetangulo("AreaIcone", cardObj.transform);
        LayoutElement leIcon = iconArea.AddComponent<LayoutElement>();
        leIcon.minHeight = 84;
        leIcon.preferredHeight = 84;
        leIcon.flexibleHeight = 0;

        Image imgFrame = iconArea.AddComponent<Image>();
        imgFrame.color = new Color(1f, 1f, 1f, 0.04f);
        imgFrame.raycastTarget = false;

        Outline frameOutline = iconArea.AddComponent<Outline>();
        frameOutline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        frameOutline.effectDistance = new Vector2(1f, -1f);

        GameObject iconHolder = CriarRetangulo("IconeHolder", iconArea.transform);
        RectTransform rtIconHolder = iconHolder.GetComponent<RectTransform>();
        EsticarRectTransform(rtIconHolder);
        rtIconHolder.offsetMin = new Vector2(8f, 8f);
        rtIconHolder.offsetMax = new Vector2(-8f, -8f);

        Image imgIcon = iconHolder.AddComponent<Image>();
        imgIcon.raycastTarget = false;

        if (item.icone != null)
        {
            imgIcon.sprite = item.icone;
            imgIcon.preserveAspect = true;
            imgIcon.color = Color.white;
        }
        else
        {
            imgIcon.color = new Color(1f, 1f, 1f, 0.1f);
            GameObject textPlace = CriarRetangulo("TxtPlace", iconHolder.transform);
            Text tPlace = textPlace.AddComponent<Text>();
            tPlace.text = "NO IMAGE";
            tPlace.font = ObterFontePadrao();
            tPlace.alignment = TextAnchor.MiddleCenter;
            tPlace.fontSize = 11;
            tPlace.color = new Color(1f, 1f, 1f, 0.24f);
            tPlace.resizeTextForBestFit = true;
            tPlace.resizeTextMaxSize = 11;
            tPlace.raycastTarget = false;
            EsticarRectTransform(textPlace.GetComponent<RectTransform>());
        }

        GameObject nomeObj = CriarRetangulo("NomeItem", cardObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 30;
        leNome.preferredHeight = 30;

        Text tNome = nomeObj.AddComponent<Text>();
        tNome.text = item.nomeItem;
        tNome.font = ObterFontePadrao();
        tNome.fontSize = 14;
        tNome.alignment = TextAnchor.MiddleCenter;
        tNome.color = corTextoPrimario;
        tNome.fontStyle = FontStyle.Bold;
        tNome.resizeTextForBestFit = true;
        tNome.resizeTextMinSize = 10;
        tNome.resizeTextMaxSize = 14;
        tNome.raycastTarget = false;

        GameObject precoObj = CriarRetangulo("Preco", cardObj.transform);
        LayoutElement lePreco = precoObj.AddComponent<LayoutElement>();
        lePreco.minHeight = 18;
        lePreco.preferredHeight = 18;

        Text tPreco = precoObj.AddComponent<Text>();
        tPreco.text = $"<color=#44FF88>$ {item.preco}</color>";
        tPreco.font = ObterFontePadrao();
        tPreco.fontSize = 13;
        tPreco.alignment = TextAnchor.MiddleCenter;
        tPreco.supportRichText = true;
        tPreco.raycastTarget = false;

        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(cardObj.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement leSpacer = spacer.AddComponent<LayoutElement>();
        leSpacer.flexibleHeight = 1;

        GameObject controlsObj = CriarRetangulo("Controles", cardObj.transform);
        LayoutElement leControls = controlsObj.AddComponent<LayoutElement>();
        leControls.minHeight = 34;
        leControls.preferredHeight = 34;

        Image imgControls = controlsObj.AddComponent<Image>();
        imgControls.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
        imgControls.raycastTarget = false;

        Outline outlineControls = controlsObj.AddComponent<Outline>();
        outlineControls.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outlineControls.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutControls = controlsObj.AddComponent<HorizontalLayoutGroup>();
        layoutControls.padding = new RectOffset(4, 4, 4, 4);
        layoutControls.spacing = 4;
        layoutControls.childControlWidth = true;
        layoutControls.childForceExpandWidth = true;
        layoutControls.childControlHeight = true;
        layoutControls.childForceExpandHeight = true;

        GameObject qtdBox = CriarRetangulo("BoxQtd", controlsObj.transform);
        LayoutElement leQtdBox = qtdBox.AddComponent<LayoutElement>();
        leQtdBox.minWidth = 58;
        leQtdBox.preferredWidth = 58;

        Image imgQtdBox = qtdBox.AddComponent<Image>();
        imgQtdBox.color = new Color(1f, 1f, 1f, 0.05f);
        imgQtdBox.raycastTarget = false;

        Outline outlineQtd = qtdBox.AddComponent<Outline>();
        outlineQtd.effectColor = new Color(1f, 1f, 1f, 0.05f);
        outlineQtd.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutQtd = qtdBox.AddComponent<HorizontalLayoutGroup>();
        layoutQtd.spacing = 1;
        layoutQtd.childAlignment = TextAnchor.MiddleCenter;
        layoutQtd.childControlWidth = true;
        layoutQtd.childControlHeight = true;
        layoutQtd.childForceExpandWidth = false;
        layoutQtd.childForceExpandHeight = true;

        if (!quantidadesPorItem.ContainsKey(item.nomeItem)) quantidadesPorItem[item.nomeItem] = 1;

        GameObject btnMenos = CriarBotaoSimples("-", qtdBox.transform, new Color(1,0.3f,0.3f));

        GameObject txtQtdObj = CriarRetangulo("TxtQtd", qtdBox.transform);
        Text tQtd = txtQtdObj.AddComponent<Text>();
        tQtd.text = quantidadesPorItem[item.nomeItem].ToString();
        tQtd.font = ObterFontePadrao();
        tQtd.alignment = TextAnchor.MiddleCenter;
        tQtd.color = Color.white;
        tQtd.fontSize = 13;
        tQtd.raycastTarget = false;
        LayoutElement leTxtQ = txtQtdObj.AddComponent<LayoutElement>();
        leTxtQ.minWidth = 18;
        leTxtQ.preferredWidth = 18;
        leTxtQ.flexibleWidth = 1;

        GameObject btnMais = CriarBotaoSimples("+", qtdBox.transform, new Color(0.3f,1f,0.3f));

        GameObject btnComprarObj = CriarRetangulo("BtnComprar", controlsObj.transform);
        Image imgComprar = btnComprarObj.AddComponent<Image>();
        imgComprar.color = new Color(0.12f, 0.46f, 0.24f, 0.95f);

        Outline outlineComprar = btnComprarObj.AddComponent<Outline>();
        outlineComprar.effectColor = new Color(0.45f, 1f, 0.65f, 0.22f);
        outlineComprar.effectDistance = new Vector2(1f, -1f);

        Button btnComp = btnComprarObj.AddComponent<Button>();
        LayoutElement leComp = btnComprarObj.AddComponent<LayoutElement>();
        leComp.flexibleWidth = 1.4f;

        GameObject txtCompObj = CriarRetangulo("TxtComp", btnComprarObj.transform);
        Text tComp = txtCompObj.AddComponent<Text>();
        tComp.text = "COMPRAR";
        tComp.font = ObterFontePadrao();
        tComp.alignment = TextAnchor.MiddleCenter;
        tComp.color = Color.white;
        tComp.fontSize = 9;
        tComp.fontStyle = FontStyle.Bold;
        tComp.resizeTextForBestFit = true;
        tComp.resizeTextMaxSize = 10;
        tComp.resizeTextMinSize = 6;
        tComp.raycastTarget = false;
        EsticarRectTransform(txtCompObj.GetComponent<RectTransform>());

        Text refTextoQtd = tQtd;
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, -1, refTextoQtd));
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, 1, refTextoQtd));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnComp.onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnComp.onClick.AddListener(() => ConstruirItem(item, imgBg));

        AdicionarEventoHoverDetalhes(btnMenos, item);
        AdicionarEventoHoverDetalhes(btnMais, item);
        AdicionarEventoHoverDetalhes(btnComprarObj, item);
    }

    GameObject CriarBotaoSimples(string texto, Transform pai, Color corTexto)
    {
        GameObject btnObj = CriarRetangulo("Btn" + texto, pai);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = CriarRetangulo("Txt", btnObj.transform);
        Text t = txtObj.AddComponent<Text>();
        t.text = texto;
        t.font = ObterFontePadrao();
        t.color = corTexto;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.fontSize = 13;
        t.raycastTarget = false;
        EsticarRectTransform(txtObj.GetComponent<RectTransform>());

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minWidth = 17;
        le.preferredWidth = 17;

        return btnObj;
    }

    void AlterarQuantidade(string idItem, int delta, Text textoUI)
    {
        if (quantidadesPorItem.ContainsKey(idItem))
        {
            int novaQtd = quantidadesPorItem[idItem] + delta;
            if (novaQtd < 1) novaQtd = 1;
            if (novaQtd > 50) novaQtd = 50;

            quantidadesPorItem[idItem] = novaQtd;
            textoUI.text = novaQtd.ToString();
        }
    }

    GameObject CriarRetangulo(string nome, Transform pai)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    void GarantirConfigsVisuaisCategoria()
    {
        if (aparenciasCategorias == null)
        {
            aparenciasCategorias = new List<ConfigVisualCategoria>();
        }

        lookupVisualCategorias.Clear();

        foreach (DadosConstrucao.CategoriaItem categoria in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            ConfigVisualCategoria config = aparenciasCategorias.FirstOrDefault(c => c != null && c.categoria == categoria);
            if (config == null)
            {
                config = new ConfigVisualCategoria
                {
                    categoria = categoria,
                    glifoFallback = ObterGlifoPadrao(categoria)
                };
                aparenciasCategorias.Add(config);
            }
            else if (string.IsNullOrWhiteSpace(config.glifoFallback))
            {
                config.glifoFallback = ObterGlifoPadrao(categoria);
            }

            lookupVisualCategorias[categoria] = config;
        }
    }

    ConfigVisualCategoria ObterConfigVisualCategoria(DadosConstrucao.CategoriaItem categoria)
    {
        if (lookupVisualCategorias.Count == 0)
        {
            GarantirConfigsVisuaisCategoria();
        }

        ConfigVisualCategoria config;
        if (lookupVisualCategorias.TryGetValue(categoria, out config))
        {
            return config;
        }

        return null;
    }

    string ObterRotuloCategoria(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "EXERCITO";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "MARINHA";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "AERONAUTICA";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "TECNOLOGIA";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "INFRAESTRUTURA";
            case DadosConstrucao.CategoriaItem.Energia:
                return "ENERGIA";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "URBANA";
            default:
                return categoria.ToString().ToUpperInvariant();
        }
    }

    string ObterGlifoPadrao(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "E";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "N";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "A";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "T";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "I";
            case DadosConstrucao.CategoriaItem.Energia:
                return "P";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "U";
            default:
                return "?";
        }
    }

    Font ObterFontePadrao()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void EsticarRectTransform(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    string ObterTipoItem(DadosConstrucao item)
    {
        if (item == null)
        {
            return "--";
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.rotuloTipo))
        {
            return item.balanceamento.rotuloTipo.Trim();
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return "Pesquisa";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura)
        {
            return "Infraestrutura";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Energia)
        {
            return "Energia";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Urbana)
        {
            return "Estrutura urbana";
        }

        if (EhUnidadeAerea(item))
        {
            return "Unidade aerea";
        }

        if (EhUnidadeNaval(item))
        {
            return "Unidade naval";
        }

        if (EhEstruturaParaDetalhes(item))
        {
            return "Estrutura fixa";
        }

        return "Unidade terrestre";
    }

    string ObterTextoVelocidadeItem(DadosConstrucao item)
    {
        if (item == null || item.prefabDaUnidade == null)
        {
            return "--";
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.velocidadeExibida))
        {
            return item.balanceamento.velocidadeExibida.Trim();
        }

        float velocidade = -1f;
        GameObject prefab = item.prefabDaUnidade;

        ControleAviao controleAviao = prefab.GetComponent<ControleAviao>() ?? prefab.GetComponentInChildren<ControleAviao>(true);
        if (controleAviao != null)
        {
            velocidade = controleAviao.velocidadeMaximaVoo;
        }

        if (velocidade <= 0f)
        {
            C700TransporteAereo transporteAereo = prefab.GetComponent<C700TransporteAereo>() ?? prefab.GetComponentInChildren<C700TransporteAereo>(true);
            if (transporteAereo != null)
            {
                velocidade = transporteAereo.velocidadeCruzeiro;
            }
        }

        if (velocidade <= 0f)
        {
            CacaVooRealista caca = prefab.GetComponent<CacaVooRealista>() ?? prefab.GetComponentInChildren<CacaVooRealista>(true);
            if (caca != null)
            {
                velocidade = caca.velocidadeMaxima;
            }
        }

        if (velocidade <= 0f)
        {
            ControleNavioRealista navio = prefab.GetComponent<ControleNavioRealista>() ?? prefab.GetComponentInChildren<ControleNavioRealista>(true);
            if (navio != null)
            {
                velocidade = navio.velocidadeMaxima;
            }
        }

        if (velocidade <= 0f)
        {
            HovercraftTransporte hovercraft = prefab.GetComponent<HovercraftTransporte>() ?? prefab.GetComponentInChildren<HovercraftTransporte>(true);
            if (hovercraft != null)
            {
                velocidade = hovercraft.velocidade;
            }
        }

        if (velocidade <= 0f)
        {
            NavMeshAgent agente = prefab.GetComponent<NavMeshAgent>() ?? prefab.GetComponentInChildren<NavMeshAgent>(true);
            if (agente != null)
            {
                velocidade = agente.speed;
            }
        }

        if (velocidade > 0f)
        {
            return (velocidade * 3.6f).ToString("0") + " km/h";
        }

        return EhEstruturaParaDetalhes(item) ? "Fixa" : "N/A";
    }

    string ObterTextoVidaItem(DadosConstrucao item)
    {
        if (item == null || item.prefabDaUnidade == null) return "--";
        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.blindagemExibida))
        {
            return item.balanceamento.blindagemExibida.Trim();
        }
        SistemaDeDanos sys = item.prefabDaUnidade.GetComponent<SistemaDeDanos>();
        if (sys == null) sys = item.prefabDaUnidade.GetComponentInChildren<SistemaDeDanos>(true);

        if (sys != null)
        {
            return sys.vidaMaxima.ToString();
        }
        return EhEstruturaParaDetalhes(item) ? "Forte" : "Leve";
    }

    string ObterTextoPoderFogoItem(DadosConstrucao item)
    {
        if (item == null || item.prefabDaUnidade == null) return "--";
        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.poderOfensivoExibido))
        {
            return item.balanceamento.poderOfensivoExibido.Trim();
        }
        var comps = item.prefabDaUnidade.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            string n = c.GetType().Name.ToLower();
            if (n.Contains("torreta") || n.Contains("arma") || n.Contains("lancador") || n.Contains("caca"))
                return "Armado";
        }
        return EhEstruturaParaDetalhes(item) ? "Fixo" : "Desarmado";
    }

    string ObterDescricaoDetalheItem(DadosConstrucao item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.descricaoTatica))
        {
            string descricaoBalanceamento = item.balanceamento.descricaoTatica.Trim();
            if (item.balanceamento.limiteEmCampo > 0)
            {
                descricaoBalanceamento += $"\nLimite recomendado em campo: {item.balanceamento.limiteEmCampo}.";
            }
            return descricaoBalanceamento;
        }

        if (!string.IsNullOrWhiteSpace(item.descricao))
        {
            return item.descricao.Trim();
        }

        return ObterDescricaoFallback(item.categoria);
    }

    bool EhEstruturaParaDetalhes(DadosConstrucao item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura
            || item.categoria == DadosConstrucao.CategoriaItem.Energia
            || item.categoria == DadosConstrucao.CategoriaItem.Urbana
            || item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return true;
        }

        if (item.prefabDaUnidade == null)
        {
            return false;
        }

        GameObject prefab = item.prefabDaUnidade;
        bool possuiIdentidadeNaval = prefab.GetComponent<IdentidadeNaval>() != null
                                   || prefab.GetComponentInChildren<IdentidadeNaval>(true) != null;
        bool ehPortaAvioes = prefab.GetComponent<GerenciadorPortaAvioes>() != null
                           || (prefab.GetComponent<GerenciadorAeroporto>() != null && possuiIdentidadeNaval);

        if (ehPortaAvioes)
        {
            return false;
        }

        if (prefab.CompareTag("Imovel")
            || prefab.GetComponent<Fabrica>() != null
            || prefab.GetComponent<Estaleiro>() != null
            || prefab.GetComponent<Heliporto>() != null
            || prefab.GetComponent<GerenciadorAeroporto>() != null
            || prefab.GetComponent<PierMarinha>() != null)
        {
            return true;
        }

        string nomeComposto = (prefab.name + "_" + item.nomeItem).ToLowerInvariant();
        return nomeComposto.Contains("hangar")
            || nomeComposto.Contains("fabrica")
            || nomeComposto.Contains("quartel")
            || nomeComposto.Contains("silo")
            || nomeComposto.Contains("torre")
            || nomeComposto.Contains("bunker")
            || nomeComposto.Contains("estaleiro")
            || nomeComposto.Contains("pier")
            || nomeComposto.Contains("plataforma");
    }

    bool EhUnidadeNaval(DadosConstrucao item)
    {
        if (item == null || item.prefabDaUnidade == null)
        {
            return item != null && item.categoria == DadosConstrucao.CategoriaItem.Marinha;
        }

        GameObject prefab = item.prefabDaUnidade;
        return item.categoria == DadosConstrucao.CategoriaItem.Marinha
            || prefab.GetComponent<IdentidadeNaval>() != null
            || prefab.GetComponentInChildren<IdentidadeNaval>(true) != null
            || prefab.GetComponent<ControleNavioRealista>() != null
            || prefab.GetComponentInChildren<ControleNavioRealista>(true) != null
            || prefab.GetComponent<HovercraftTransporte>() != null
            || prefab.GetComponentInChildren<HovercraftTransporte>(true) != null;
    }

    bool EhUnidadeAerea(DadosConstrucao item)
    {
        if (item == null || item.prefabDaUnidade == null)
        {
            return item != null && item.categoria == DadosConstrucao.CategoriaItem.Aeronautica;
        }

        GameObject prefab = item.prefabDaUnidade;
        return item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
            || prefab.GetComponent<ControleAviao>() != null
            || prefab.GetComponentInChildren<ControleAviao>(true) != null
            || prefab.GetComponent<C700TransporteAereo>() != null
            || prefab.GetComponentInChildren<C700TransporteAereo>(true) != null
            || prefab.GetComponent<CacaVooRealista>() != null
            || prefab.GetComponentInChildren<CacaVooRealista>(true) != null;
    }

    string ObterDescricaoFallback(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "Unidade voltada para combate terrestre, pressao ofensiva e defesa de territorio.";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "Unidade para dominar rotas costeiras, travessias e confrontos navais.";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "Unidade de alcance rapido para patrulha, ataque e resposta aerea.";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "Melhoria estrategica para desbloquear vantagem tecnica e fortalecer seu dominio.";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "Estrutura de suporte para expandir producao, logistica e capacidade operacional.";
            case DadosConstrucao.CategoriaItem.Energia:
                return "Estrutura essencial para sustentar operacao, consumo e crescimento da base.";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "Estrutura civil e administrativa para apoiar o desenvolvimento do territorio.";
            default:
                return "Informacoes tecnicas indisponiveis para este item.";
        }
    }

    void ConstruirItem(DadosConstrucao item, Image cardImage = null)
    {
        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (gerente == null) return;

        if (item.prefabDaUnidade == null)
        {
            Debug.LogError($"[MenuConstrucao] Prefab '{item.nomeItem}' faltando!");
            return;
        }
        
        // Porta-aviões (GerenciadorPortaAvioes) tem GerenciadorAeroporto mas é NAVIO, não prédio!
        bool ehPortaAvioes = item.prefabDaUnidade.GetComponent<GerenciadorPortaAvioes>() != null
                           || (item.prefabDaUnidade.GetComponent<GerenciadorAeroporto>() != null
                               && (item.prefabDaUnidade.GetComponent<IdentidadeNaval>() != null
                                   || item.prefabDaUnidade.GetComponentInChildren<IdentidadeNaval>() != null
                                   || item.nomeItem.ToLower().Contains("uss")
                                   || item.nomeItem.ToLower().Contains("porta")
                                   || item.nomeItem.ToLower().Contains("carrier")));

        bool temComponentePredio = !ehPortaAvioes && (
                                   item.prefabDaUnidade.GetComponent<Fabrica>() != null ||
                                   item.prefabDaUnidade.GetComponent<Estaleiro>() != null ||
                                   item.prefabDaUnidade.GetComponent<Heliporto>() != null ||
                                   item.prefabDaUnidade.GetComponent<GerenciadorAeroporto>() != null ||
                                   item.prefabDaUnidade.GetComponent<PierMarinha>() != null);

        string nomeLower = (item.prefabDaUnidade.name + "_" + item.nomeItem).ToLower();

        // Lista de segurança para garantir que certos itens sejam lidos como prédios
        bool ehPredioExplícito = item.prefabDaUnidade.CompareTag("Imovel") || 
                                 temComponentePredio || 
                                 nomeLower.Contains("hangar") || nomeLower.Contains("fabrica") || nomeLower.Contains("construtor") || nomeLower.Contains("refinaria") || 
                                 nomeLower.Contains("quartel") || nomeLower.Contains("tenda") || nomeLower.Contains("silo") ||
                                 nomeLower.Contains("torre") || nomeLower.Contains("muro") || nomeLower.Contains("wall") ||
                                 nomeLower.Contains("aeroporto") || nomeLower.Contains("heliporto") || nomeLower.Contains("pista") ||
                                 nomeLower.Contains("ares") || nomeLower.Contains("area") || 
                                 nomeLower.Contains("antiaerea") || nomeLower.Contains("missil") ||
                                 nomeLower.Contains("bunker") || nomeLower.Contains("defesa") || 
                                 nomeLower.Contains("torreta") || 
                                 nomeLower.Contains("canhao") || nomeLower.Contains("metralhadora") || nomeLower.Contains("plataforma") || 
                                 nomeLower.Contains("estaleiro") || nomeLower.Contains("pier") ||
                                 item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura || item.categoria == DadosConstrucao.CategoriaItem.Energia || item.categoria == DadosConstrucao.CategoriaItem.Urbana ||
                                 item.categoria == DadosConstrucao.CategoriaItem.Tecnologia;

        int qtd = quantidadesPorItem.ContainsKey(item.nomeItem) ? quantidadesPorItem[item.nomeItem] : 1;
        
        if (ehPredioExplícito || item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura || item.categoria == DadosConstrucao.CategoriaItem.Energia || item.categoria == DadosConstrucao.CategoriaItem.Urbana)
        {
             qtd = 1; 
        }

        int custoTotal = item.preco * qtd;
        int qtdParaConstruir = qtd;

        string motivoGovernanca;
        if (!GovernadorGameplayRTS.PermitirProducao(item, qtdParaConstruir, out motivoGovernanca))
        {
            if (cardImage != null)
            {
                StartCoroutine(FlashCardErro(cardImage));
            }

            EmitirAvisoJogador(motivoGovernanca);
            return;
        }

        bool temDinheiro = false;
        if (GerenciadorRecursos.Instancia != null)
        {
            if (GerenciadorRecursos.Instancia.dinheiro >= custoTotal) temDinheiro = true;
        }
        else
        {
            if (gerente.dinheiroAtual >= custoTotal) temDinheiro = true;
        }

        if (temDinheiro && cardImage != null) StartCoroutine(FlashCard(cardImage));
        else if (!temDinheiro && cardImage != null) 
        {
            StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador($"Fundos insuficientes para comprar {item.nomeItem}.");
            return; 
        }

        // ==========================================
        // 1. ROTEAMENTO PARA MARINHA (Navios)
        // ==========================================
        string nomeItemLower = item.nomeItem.ToLower();
        string nomePrefabLower = item.prefabDaUnidade.name.ToLower();

        bool pareceSerNavio = item.prefabDaUnidade.GetComponent<IdentidadeNaval>() != null 
                            || item.prefabDaUnidade.GetComponentInChildren<IdentidadeNaval>() != null
                            || item.prefabDaUnidade.GetComponent<HovercraftTransporte>() != null
                            || item.prefabDaUnidade.GetComponent<ControleNavioRealista>() != null
                            || item.prefabDaUnidade.GetComponent<ControleSubmarino>() != null
                            || item.prefabDaUnidade.GetComponent<NavioPetroleiro>() != null
                            || nomeItemLower.Contains("navio") || nomeItemLower.Contains("sub")
                            || nomeItemLower.Contains("leviathan") || nomeItemLower.Contains("lancha")
                            || nomeItemLower.Contains("corveta") || nomeItemLower.Contains("hovercraft")
                            || nomeItemLower.Contains("hover") || nomeItemLower.Contains("marinha")
                            || nomeItemLower.Contains("destroier") || nomeItemLower.Contains("fragata")
                            || nomeItemLower.Contains("ironclad") || nomeItemLower.Contains("vindicator")
                            || nomeItemLower.Contains("barco") || nomeItemLower.Contains("carrier")
                            || nomeItemLower.Contains("porta") || nomeItemLower.Contains("uss")
                            || nomePrefabLower.Contains("hover") || nomePrefabLower.Contains("marinha")
                            || nomePrefabLower.Contains("nav_") || nomePrefabLower.Contains("navio")
                            || nomePrefabLower.Contains("corveta") || nomePrefabLower.Contains("wall")
                            || nomePrefabLower.Contains("uss") || nomePrefabLower.Contains("ironclad")
                            || nomePrefabLower.Contains("vindicator") || nomePrefabLower.Contains("destroier");

        if (item.categoria == DadosConstrucao.CategoriaItem.Marinha || pareceSerNavio)
        {
            bool ehPredioNaval = !ehPortaAvioes && (
                                 item.prefabDaUnidade.CompareTag("Imovel") ||
                                 temComponentePredio ||
                                 item.nomeItem.ToLower().Contains("estaleiro") || 
                                 item.nomeItem.ToLower().Contains("pier") || 
                                 item.nomeItem.ToLower().Contains("plataforma"));

            if (!ehPredioNaval)
            {
                bool ehNavioGrande = EhNavioGrande(item.prefabDaUnidade);
                List<Estaleiro> estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None)
                    .Where(e => e != null && EhEstruturaDoJogador(e) && EstruturaNavalOperacional(e))
                    .ToList();

                List<PierMarinha> piers = ehNavioGrande
                    ? new List<PierMarinha>()
                    : Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None)
                        .Where(p => p != null && EhEstruturaDoJogador(p) && EstruturaNavalOperacional(p))
                        .ToList();

                if (estaleiros.Count == 0 && piers.Count == 0)
                {
                    EmitirAvisoJogador(ehNavioGrande
                        ? "❌ BLOQUEADO: construa um ESTALEIRO costeiro válido para produzir esse navio grande."
                        : "❌ BLOQUEADO: construa um ESTALEIRO ou PIER costeiro válido para produzir navios.");
                    return;
                }

                int quantidadeEnfileirada = 0;
                for (int i = 0; i < qtdParaConstruir; i++)
                {
                    if (!gerente.TentarGastarDinheiro(item.preco))
                    {
                        break;
                    }

                    bool sucesso = false;

                    foreach (Estaleiro estaleiro in estaleiros)
                    {
                        if (estaleiro == null || !estaleiro.TemVaga || !EstruturaNavalOperacional(estaleiro))
                        {
                            continue;
                        }

                        if (estaleiro.ConstruirUnidade(item.prefabDaUnidade))
                        {
                            Debug.Log("⚓ [Construção] Enviando para Estaleiro: " + estaleiro.name);
                            sucesso = true;
                            quantidadeEnfileirada++;
                            break;
                        }
                    }

                    if (!sucesso)
                    {
                        foreach (PierMarinha pier in piers)
                        {
                            if (pier == null || !EstruturaNavalOperacional(pier))
                            {
                                continue;
                            }

                            if (pier.ConstruirNavio(item.prefabDaUnidade))
                            {
                                Debug.Log("⚓ [Construção] Construindo no Píer: " + pier.name);
                                sucesso = true;
                                quantidadeEnfileirada++;
                                break;
                            }
                        }
                    }

                    if (!sucesso)
                    {
                        ReembolsarDinheiro(item.preco);
                        EmitirAvisoJogador($"Falha ao produzir '{item.nomeItem}' em estruturas navais validas.");
                        break;
                    }
                }

                if (quantidadeEnfileirada > 0)
                {
                    if (quantidadeEnfileirada < qtdParaConstruir)
                    {
                        Debug.LogWarning($"⚓ [Construção] Produção parcial: {quantidadeEnfileirada}/{qtdParaConstruir} navios enviados.");
                    }

                    AlternarMenu(false);
                }

                return;
            }
        }

        // ==========================================
        // 2. ROTEAMENTO PARA AERONÁUTICA (Aviões) - MANTIDO INTACTO!
        // ==========================================
        if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
        {
            bool pareceSerAviao = item.prefabDaUnidade.GetComponent<ControleAviao>() != null 
                                || item.nomeItem.ToLower().Contains("caca") 
                                || item.nomeItem.ToLower().Contains("avi") 
                                || item.nomeItem.ToLower().Contains("tuk")
                                || item.nomeItem.ToLower().Contains("g15");
                                
            bool ehPredioAeronautica = item.prefabDaUnidade.CompareTag("Imovel") || temComponentePredio
                                    || item.nomeItem.ToLower().Contains("aeroporto") 
                                    || item.nomeItem.ToLower().Contains("pista");

            if (!ehPredioAeronautica && pareceSerAviao)
            {
                RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);
                
                // Filtra apenas aeroportos do jogador (Time 1)
                var meusAeroportos = bufferAeroportos.Where(a => {
                    IdentidadeUnidade id = a.GetComponent<IdentidadeUnidade>();
                    return id == null || id.teamID == 1;
                }).ToList();

                if (meusAeroportos.Count == 0)
                {
                    EmitirAvisoJogador("Bloqueado: voce precisa construir um AEROPORTO primeiro para comprar avioes.");
                    return;
                }

                // Tenta encontrar o melhor aeroporto:
                // 1. Prioriza o que NÃO é um Porta-Aviões (se houver um aeroporto terrestre disponível)
                // 2. Prioriza o que tem vaga no pátio
                GerenciadorAeroporto meuAero = meusAeroportos.OrderByDescending(a => {
                    int score = 0;
                    // Se não for porta-aviões, ganha pontos (aviões terrestres devem preferir bases terrestres)
                    if (a.GetType() != typeof(GerenciadorPortaAvioes)) score += 100;
                    
                    // Se tem vaga no pátio, ganha pontos
                    if (a.ObterPrimeiraVagaLivre() != null) score += 50;
                    
                    return score;
                }).FirstOrDefault();

                if (meuAero != null)
                {
                    if (!gerente.TentarGastarDinheiro(item.preco * qtdParaConstruir)) return; 
                    Debug.Log($"✈️ [Construção] Fabricando {qtdParaConstruir}x aviões no Aeroporto: {meuAero.name}");
                    for(int i=0; i<qtdParaConstruir; i++) meuAero.ComprarAviao(item.prefabDaUnidade);
                    AlternarMenu(false);
                    return;
                }
                else
                {
                    EmitirAvisoJogador("Erro: nenhum aeroporto valido encontrado para entregar esta aeronave.");
                    return;
                }
            }
        }

        // ==========================================
        // 3. IDENTIFICAÇÃO CORRETA: É PRÉDIO OU UNIDADE MÓVEL?
        // ==========================================
        // Lógica simplificada e segura: Se tem motor/pernas, é veículo. Se não tem, é prédio!
        
        bool ehUnidadeMovel = item.prefabDaUnidade.GetComponent<UnityEngine.AI.NavMeshAgent>() != null 
                            || item.prefabDaUnidade.GetComponent<ControleUnidade>() != null
                            || item.prefabDaUnidade.GetComponent("Helicoptero") != null; 

        // Proteção 1: Se o nome tem "soldado", "tanque", forçamos ser móvel para não colar no mouse
        if (nomeLower.Contains("soldado") || nomeLower.Contains("tank") || nomeLower.Contains("veiculo") || nomeLower.Contains("infantaria") || nomeLower.Contains("fuzileiro") || item.categoria == DadosConstrucao.CategoriaItem.Exercito)
        {
            ehUnidadeMovel = true;
        }

        // Proteção 2: Se sabemos com certeza que é prédio (Tag Imovel, Torreta, Hangar), NUNCA será unidade móvel!
        if (ehPredioExplícito || temComponentePredio || item.prefabDaUnidade.CompareTag("Imovel"))
        {
            ehUnidadeMovel = false; 
        }

        // Se for Unidade Móvel (Tanque, Soldado), vai pro Quartel/Gerente
        if (ehUnidadeMovel)
        {
            gerente.ComprarUnidade(item.prefabDaUnidade, item.preco, qtdParaConstruir);
            AlternarMenu(false); 
            return;
        }

        // ==========================================
        // 4. CONSTRUÇÃO FINAL (O ITEM É UM PRÉDIO/ESTRUTURA)
        // ==========================================
        // Se o código chegou aqui, ele Sabe que é uma Torreta, um Hangar ou um Ares.
        // Ele vai mandar direto para o seu Mouse!
        
        Construtor construtor = Construtor.Instancia != null ? Construtor.Instancia : Object.FindFirstObjectByType<Construtor>();
        if (construtor == null)
        {
             EmitirAvisoJogador("Construtor nao encontrado na cena. Impossivel posicionar a estrutura.");
             return;
        }

        if (construtor.modoConstrucao && construtor.prefabSelecionado == item.prefabDaUnidade)
        {
            AlternarMenu(false);
            return;
        }

        if (construtor.modoConstrucao)
        {
            construtor.CancelarConstrucao(true);
        }

        if (!gerente.TentarGastarDinheiro(item.preco)) return; 
        
        construtor.SelecionarParaConstruir(item.prefabDaUnidade, item.preco, item.categoria);
        AlternarMenu(false); 
    }

    void EmitirAvisoJogador(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return;
        }

        Debug.LogWarning(mensagem);
        HUDAjudaRTS.MostrarMensagemTemporaria(mensagem, 3.6f);
    }

    bool EhEstruturaDoJogador(Component estrutura)
    {
        if (estrutura == null)
        {
            return false;
        }

        IdentidadeUnidade id = estrutura.GetComponent<IdentidadeUnidade>();
        if (id == null)
        {
            id = estrutura.GetComponentInParent<IdentidadeUnidade>();
        }

        return id == null || id.teamID == 1;
    }

    bool EstruturaNavalOperacional(Component estrutura)
    {
        if (estrutura == null || estrutura.gameObject == null)
        {
            return false;
        }

        if (estrutura.GetComponent<IA_ManualPlacementTag>() != null
            || estrutura.GetComponentInParent<IA_ManualPlacementTag>() != null)
        {
            return true;
        }

        if (estrutura is Estaleiro estaleiro)
        {
            string validacao;
            if (NavalPlacementResolver.IsCurrentStructurePoseValid(estaleiro.gameObject, out validacao))
            {
                return true;
            }

            return estaleiro.EstaNaConstrucaoValida(NavalPlacementResolver.ResolveSeaLevel());
        }

        if (estrutura is PierMarinha pier)
        {
            string validacao;
            return NavalPlacementResolver.IsCurrentStructurePoseValid(pier.gameObject, out validacao);
        }

        return true;
    }

    bool EhNavioGrande(GameObject prefabDoNavio)
    {
        if (prefabDoNavio == null)
        {
            return false;
        }

        IdentidadeNaval identidadeNaval = prefabDoNavio.GetComponent<IdentidadeNaval>();
        if (identidadeNaval == null)
        {
            identidadeNaval = prefabDoNavio.GetComponentInChildren<IdentidadeNaval>();
        }

        if (identidadeNaval != null && identidadeNaval.categoriaNavio == IdentidadeNaval.CategoriaNavio.TransporteGrande)
        {
            return true;
        }

        return prefabDoNavio.GetComponent<NavioPetroleiro>() != null
            || prefabDoNavio.GetComponent<TransporteAnfibio>() != null
            || prefabDoNavio.GetComponent<NavioLiberty>() != null;
    }

    void ReembolsarDinheiro(int valor)
    {
        if (valor <= 0)
        {
            return;
        }

        if (GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: valor);
        }
    }

    IEnumerator FlashCard(Image img)
    {
        if (img == null) yield break;
        Color corOriginal = corCardBase;
        Color corSucesso = new Color(0.2f, 0.8f, 0.2f, 0.9f); 

        float tempo = 0;
        while(tempo < 0.15f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corOriginal, corSucesso, tempo / 0.15f);
            yield return null;
        }

        tempo = 0;
        while(tempo < 0.4f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corSucesso, corOriginal, tempo / 0.4f);
            yield return null;
        }
        if(img != null) img.color = corOriginal;
    }

    IEnumerator FlashCardErro(Image img)
    {
        if (img == null) yield break;
        Color corOriginal = corCardBase;
        Color corErro = new Color(0.8f, 0.2f, 0.2f, 0.9f); 

        float tempo = 0;
        while(tempo < 0.1f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corOriginal, corErro, tempo / 0.1f);
            yield return null;
        }
        tempo = 0;
        while(tempo < 0.3f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corErro, corOriginal, tempo / 0.3f);
            yield return null;
        }
        if(img != null) img.color = corOriginal;
    }
}
