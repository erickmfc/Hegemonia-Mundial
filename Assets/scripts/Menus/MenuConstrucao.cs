using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;

public class MenuConstrucao : MonoBehaviour
{
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

    [Header("Sistema")]
    public bool autoCarregarFichas = false;
    public List<DadosConstrucao> catalogo = new List<DadosConstrucao>();

    public static List<DadosConstrucao> catalogoGlobal;
    public static bool EstaAberto;

    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private Transform containerAbas;
    private CanvasGroup canvasGroupPainel; 
    
    private GerenteDeJogo gerente;
    private bool menuAberto = false;
    private DadosConstrucao.CategoriaItem categoriaAtual = DadosConstrucao.CategoriaItem.Exercito;
    private Dictionary<string, int> quantidadesPorItem = new Dictionary<string, int>();

    void Start()
    {
        gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (autoCarregarFichas) CarregarTodasAsFichas();

        if (catalogoGlobal == null || catalogoGlobal.Count == 0) catalogoGlobal = catalogo;

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
        catalogoGlobal = catalogo;
    }

    void Update()
    {
        if (Input.GetKeyDown(teclaAtalho))
        {
            MenuPier menuPier = Object.FindFirstObjectByType<MenuPier>();
            if (menuPier != null) menuPier.FecharMenu();

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
        rtPanel.anchorMin = new Vector2(0.13f, 0.15f); // Menu movido 3% pra direita
        rtPanel.anchorMax = new Vector2(0.93f, 0.85f); // Menu movido 3% pra direita
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;
        
        Outline outline = painelPrincipal.AddComponent<Outline>();
        outline.effectColor = new Color(1, 1, 1, 0.1f);
        outline.effectDistance = new Vector2(1, -1);

        VerticalLayoutGroup layoutPrincipal = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutPrincipal.padding = new RectOffset(20, 20, 20, 20);
        layoutPrincipal.spacing = 15;
        layoutPrincipal.childControlHeight = true;
        layoutPrincipal.childControlWidth = true;
        layoutPrincipal.childForceExpandHeight = false; 
        
        if (Object.FindFirstObjectByType<ModoDemolicao>() == null)
        {
            GameObject go = new GameObject("ModoDemolicao_Manager");
            go.AddComponent<ModoDemolicao>();
        }

        GameObject headerObj = CriarRetangulo("Header_Abas", painelPrincipal.transform);
        LayoutElement leHeader = headerObj.AddComponent<LayoutElement>();
        leHeader.minHeight = 50;
        leHeader.preferredHeight = 50;
        leHeader.flexibleHeight = 0;

        HorizontalLayoutGroup layoutAbas = headerObj.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childControlWidth = true;
        layoutAbas.childForceExpandWidth = true; 
        layoutAbas.spacing = 10;
        containerAbas = headerObj.transform;

        foreach (DadosConstrucao.CategoriaItem cat in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            CriarBotaoAbaModerno(cat, containerAbas);
        }

        GameObject bodyObj = CriarRetangulo("Body_Scroll", painelPrincipal.transform);
        LayoutElement leBody = bodyObj.AddComponent<LayoutElement>();
        leBody.flexibleHeight = 1; 

        Image imgBody = bodyObj.AddComponent<Image>();
        imgBody.color = new Color(0, 0, 0, 0.2f); 
        imgBody.raycastTarget = true; 

        ScrollRect sr = bodyObj.AddComponent<ScrollRect>();
        sr.scrollSensitivity = 15; 
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
        rtView.anchorMin = Vector2.zero; rtView.anchorMax = Vector2.one;
        rtView.sizeDelta = Vector2.zero;

        GameObject content = CriarRetangulo("Content_Grid", viewport.transform);
        containerBotoes = content.transform;
        
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(113, 158); // -10% do tamanho anterior de 126x176
        grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; 
        grid.constraintCount = 5;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = rtView;
    }

    void CriarBotaoAbaModerno(DadosConstrucao.CategoriaItem categoria, Transform pai)
    {
        GameObject btnObj = CriarRetangulo("Aba_" + categoria, pai);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.05f); 

        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = CriarRetangulo("Texto", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = categoria.ToString().ToUpper();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 8; 
        txt.fontStyle = FontStyle.Bold;
        txt.color = corTextoSecundario;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 5;
        txt.resizeTextMaxSize = 8;
        txt.raycastTarget = false; 
        
        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;

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
            Text txt = aba.GetComponentInChildren<Text>();
            
            bool ehAtiva = aba.name == "Aba_" + catAtiva;

            if (ehAtiva)
            {
                img.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.2f); 
                if (txt)
                {
                    txt.color = corDestaque;
                    txt.fontSize = 8; 
                }
            }
            else
            {
                img.color = new Color(1, 1, 1, 0.05f);
                if (txt)
                {
                    txt.color = corTextoSecundario;
                    txt.fontSize = 8;
                }
            }
        }
    }

    public void FiltrarPorCategoria(DadosConstrucao.CategoriaItem categoriaDesejada)
    {
        categoriaAtual = categoriaDesejada;
        AtualizarVisualAbas(categoriaDesejada);

        if (containerBotoes == null) return;

        foreach (Transform child in containerBotoes) Destroy(child.gameObject);

        foreach (DadosConstrucao item in catalogo)
        {
            if (item != null && item.categoria == categoriaDesejada)
            {
                CriarCardItemModerno(item);
            }
        }
        
        StartCoroutine(AtualizarLayouts());
    }

    IEnumerator AtualizarLayouts()
    {
        yield return new WaitForEndOfFrame();
        if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
    }

    void CriarCardItemModerno(DadosConstrucao item)
    {
        GameObject cardObj = CriarRetangulo("Card_" + item.nomeItem, containerBotoes);
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;

        Button btnCard = cardObj.AddComponent<Button>();
        btnCard.transition = Selectable.Transition.None; 
        btnCard.onClick.AddListener(() => ConstruirItem(item, imgBg));
        
        VerticalLayoutGroup layoutCard = cardObj.AddComponent<VerticalLayoutGroup>();
        layoutCard.padding = new RectOffset(5, 5, 5, 5);
        layoutCard.spacing = 2;
        layoutCard.childControlHeight = true; 
        layoutCard.childForceExpandHeight = false;

        GameObject iconArea = CriarRetangulo("AreaIcone", cardObj.transform);
        LayoutElement leIcon = iconArea.AddComponent<LayoutElement>();
        leIcon.minHeight = 67; 
        leIcon.preferredHeight = 67;
        leIcon.flexibleHeight = 0;
        
        Image imgIcon = iconArea.AddComponent<Image>();
        
        if (item.icone != null)
        {
            imgIcon.sprite = item.icone;
            imgIcon.preserveAspect = true;
            imgIcon.color = Color.white;
        }
        else
        {
            imgIcon.color = new Color(1, 1, 1, 0.1f);
            GameObject textPlace = CriarRetangulo("TxtPlace", iconArea.transform);
            Text tPlace = textPlace.AddComponent<Text>();
            tPlace.text = "NO IMAGE";
            tPlace.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tPlace.alignment = TextAnchor.MiddleCenter;
            tPlace.fontSize = 10; 
            tPlace.color = new Color(1,1,1,0.2f);
            tPlace.resizeTextForBestFit = true;
            tPlace.resizeTextMaxSize = 10;
            tPlace.raycastTarget = false; 
            RectTransform rtTp = textPlace.GetComponent<RectTransform>();
            rtTp.anchorMin = Vector2.zero; rtTp.anchorMax = Vector2.one;
        }

        GameObject nomeObj = CriarRetangulo("NomeItem", cardObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 22; leNome.preferredHeight = 22; 
        
        Text tNome = nomeObj.AddComponent<Text>();
        tNome.text = item.nomeItem;
        tNome.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tNome.fontSize = 11; 
        tNome.alignment = TextAnchor.MiddleCenter;
        tNome.color = corTextoPrimario;
        tNome.fontStyle = FontStyle.Bold;
        tNome.resizeTextForBestFit = true;
        tNome.resizeTextMinSize = 9;
        tNome.resizeTextMaxSize = 14;
        tNome.raycastTarget = false; 
        
        GameObject precoObj = CriarRetangulo("Preco", cardObj.transform);
        LayoutElement lePreco = precoObj.AddComponent<LayoutElement>();
        lePreco.minHeight = 17; lePreco.preferredHeight = 17; 

        Text tPreco = precoObj.AddComponent<Text>();
        tPreco.text = $"<color=#00FF00>$ {item.preco}</color>";
        tPreco.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tPreco.fontSize = 11; 
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
        leControls.minHeight = 25; leControls.preferredHeight = 25; 
        
        HorizontalLayoutGroup layoutControls = controlsObj.AddComponent<HorizontalLayoutGroup>();
        layoutControls.padding = new RectOffset(5, 5, 0, 0); 
        layoutControls.spacing = 2;
        layoutControls.childControlWidth = true;
        layoutControls.childForceExpandWidth = true;

        GameObject qtdBox = CriarRetangulo("BoxQtd", controlsObj.transform);
        HorizontalLayoutGroup layoutQtd = qtdBox.AddComponent<HorizontalLayoutGroup>();
        layoutQtd.spacing = 2;
        
        if (!quantidadesPorItem.ContainsKey(item.nomeItem)) quantidadesPorItem[item.nomeItem] = 1;
        
        GameObject btnMenos = CriarBotaoSimples("-", qtdBox.transform, new Color(1,0.3f,0.3f));
        
        GameObject txtQtdObj = CriarRetangulo("TxtQtd", qtdBox.transform);
        Text tQtd = txtQtdObj.AddComponent<Text>();
        tQtd.text = quantidadesPorItem[item.nomeItem].ToString();
        tQtd.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tQtd.alignment = TextAnchor.MiddleCenter;
        tQtd.color = Color.white;
        tQtd.fontSize = 14;
        LayoutElement leTxtQ = txtQtdObj.AddComponent<LayoutElement>();
        leTxtQ.flexibleWidth = 1;

        GameObject btnMais = CriarBotaoSimples("+", qtdBox.transform, new Color(0.3f,1f,0.3f));

        GameObject btnComprarObj = CriarRetangulo("BtnComprar", controlsObj.transform);
        Image imgComprar = btnComprarObj.AddComponent<Image>();
        imgComprar.color = new Color(0.2f, 0.6f, 0.2f); 
        
        Button btnComp = btnComprarObj.AddComponent<Button>();
        LayoutElement leComp = btnComprarObj.AddComponent<LayoutElement>();
        leComp.flexibleWidth = 1.5f;

        GameObject txtCompObj = CriarRetangulo("TxtComp", btnComprarObj.transform);
        Text tComp = txtCompObj.AddComponent<Text>();
        tComp.text = "COMPRAR";
        tComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tComp.alignment = TextAnchor.MiddleCenter;
        tComp.color = Color.white;
        tComp.fontSize = 8; 
        tComp.resizeTextForBestFit = true;
        tComp.resizeTextMaxSize = 8;
        tComp.resizeTextMinSize = 5;
        tComp.raycastTarget = false; 
        RectTransform rtTC = txtCompObj.GetComponent<RectTransform>();
        rtTC.anchorMin = Vector2.zero; rtTC.anchorMax = Vector2.one;

        Text refTextoQtd = tQtd;
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, -1, refTextoQtd));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, 1, refTextoQtd));
        btnComp.onClick.AddListener(() => ConstruirItem(item, imgBg));
    }

    GameObject CriarBotaoSimples(string texto, Transform pai, Color corTexto)
    {
        GameObject btnObj = CriarRetangulo("Btn" + texto, pai);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1,1,1,0.1f); 
        
        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = CriarRetangulo("Txt", btnObj.transform);
        Text t = txtObj.AddComponent<Text>();
        t.text = texto;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.color = corTexto;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false; 
        
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minWidth = 30;
        
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

    void ConstruirItem(DadosConstrucao item, Image cardImage = null)
    {
        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (gerente == null) return;

        if (item.prefabDaUnidade == null)
        {
            Debug.LogError($"[MenuConstrucao] Prefab '{item.nomeItem}' faltando!");
            return;
        }
        
        bool temComponentePredio = item.prefabDaUnidade.GetComponent<Fabrica>() != null ||
                                   item.prefabDaUnidade.GetComponent<Estaleiro>() != null ||
                                   item.prefabDaUnidade.GetComponent<Heliporto>() != null ||
                                   item.prefabDaUnidade.GetComponent<GerenciadorAeroporto>() != null ||
                                   item.prefabDaUnidade.GetComponent<PierMarinha>() != null;

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
            Debug.LogWarning($"💰 Fundos insuficientes para comprar {item.nomeItem}!");
            return; 
        }

        // ==========================================
        // 1. ROTEAMENTO PARA MARINHA (Navios) - MANTIDO INTACTO!
        // ==========================================
        if(item.categoria == DadosConstrucao.CategoriaItem.Marinha)
        {
            bool ehPredioNaval = item.prefabDaUnidade.CompareTag("Imovel") ||
                                 temComponentePredio ||
                                 item.nomeItem.ToLower().Contains("estaleiro") || 
                                 item.nomeItem.ToLower().Contains("pier") || 
                                 item.nomeItem.ToLower().Contains("plataforma");

            bool pareceSerNavio = item.prefabDaUnidade.GetComponent<IdentidadeNaval>() != null 
                                || item.prefabDaUnidade.GetComponentInChildren<IdentidadeNaval>() != null
                                || item.nomeItem.ToLower().Contains("navio") || item.nomeItem.ToLower().Contains("sub")
                                || item.nomeItem.ToLower().Contains("lancha") || item.nomeItem.ToLower().Contains("corveta");

            if (!ehPredioNaval && pareceSerNavio)
            {
                Estaleiro[] estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
                Estaleiro estaleiroDisponivel = estaleiros.FirstOrDefault(e => {
                    if (!e.TemVaga) return false;
                    IdentidadeUnidade id = e.GetComponent<IdentidadeUnidade>();
                    if (id == null) id = e.GetComponentInParent<IdentidadeUnidade>();
                    return (id == null || id.teamID == 1);
                });

                if (estaleiroDisponivel != null)
                {
                    if (!gerente.TentarGastarDinheiro(item.preco * qtdParaConstruir)) return; 
                    
                    Debug.Log("⚓ [Construção] Enviando para Estaleiro: " + estaleiroDisponivel.name);
                    bool sucesso = false;
                    for(int i=0; i<qtdParaConstruir; i++) 
                    {
                        if(estaleiroDisponivel.ConstruirUnidade(item.prefabDaUnidade)) sucesso = true;
                    }
                    
                    if (sucesso)
                    {
                        AlternarMenu(false);
                        return;
                    }
                }
                
                PierMarinha[] piers = Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
                PierMarinha pierDisponivel = piers.FirstOrDefault(p => {
                    IdentidadeUnidade id = p.GetComponent<IdentidadeUnidade>();
                    if (id == null) id = p.GetComponentInParent<IdentidadeUnidade>();
                    return (id == null || id.teamID == 1);
                });

                if (pierDisponivel != null)
                {
                    if (!gerente.TentarGastarDinheiro(item.preco * qtdParaConstruir)) return; 
                    
                    Debug.Log("⚓ [Construção] Construindo instantaneamente no Píer: " + pierDisponivel.name);
                    for(int i=0; i<qtdParaConstruir; i++) 
                    {
                        pierDisponivel.ConstruirNavio(item.prefabDaUnidade);
                    }
                    
                    AlternarMenu(false);
                    return;
                }

                Debug.LogWarning("⚓ [Construção] Nenhum prédio naval livre. Jogando para a fila global do GerenteDeJogo...");
                gerente.ComprarUnidade(item.prefabDaUnidade, item.preco, qtdParaConstruir);
                AlternarMenu(false);
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
                GerenciadorAeroporto[] aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
                GerenciadorAeroporto meuAero = aeroportos.FirstOrDefault(a => {
                    IdentidadeUnidade id = a.GetComponent<IdentidadeUnidade>();
                    return id == null || id.teamID == 1; 
                });

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
                    Debug.LogWarning("❌ BLOQUEADO: Você precisa construir um AEROPORTO primeiro para comprar aviões!");
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
        if (nomeLower.Contains("soldado") || nomeLower.Contains("tank") || nomeLower.Contains("veiculo") || nomeLower.Contains("infantaria") || nomeLower.Contains("fuzileiro") || item.categoria == DadosConstrucao.CategoriaItem.Exercito || item.categoria == DadosConstrucao.CategoriaItem.Marinha)
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
        
        Construtor construtor = Object.FindFirstObjectByType<Construtor>();
        if (construtor == null)
        {
             Debug.LogWarning("⚠️ Construtor não encontrado na cena! Impossível posicionar a construção.");
             return;
        }

        if (!gerente.TentarGastarDinheiro(item.preco)) return; 
        
        construtor.SelecionarParaConstruir(item.prefabDaUnidade, item.preco, item.categoria);
        AlternarMenu(false); 
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