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
    // Cores mais sóbrias para os cards, usando transparência
    public Color corCardBase = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    public Color corBordaAtiva = new Color(1f, 1f, 1f, 0.3f);

    [Header("Sistema")]
    public bool autoCarregarFichas = false;
    public List<DadosConstrucao> catalogo = new List<DadosConstrucao>();

    // Referências Globais (Mantendo compatibilidade)
    public static List<DadosConstrucao> catalogoGlobal;
    public static bool EstaAberto;

    // Elementos da UI
    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private Transform containerAbas;
    private CanvasGroup canvasGroupPainel; // Para animações de fade
    
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

        // Estado inicial: Fechado
        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
            if(canvasGroupPainel != null) canvasGroupPainel.alpha = 0;
        }

        FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Exercito);
    }

    void CarregarTodasAsFichas()
    {
        catalogo.Clear();
        DadosConstrucao[] todasFichas = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
        foreach (var ficha in todasFichas)
        {
            if (ficha != null && ficha.prefabDaUnidade != null)
            {
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
            // Fecha Menu do Pier se estiver aberto para evitar conflito visual
            MenuPier menuPier = Object.FindFirstObjectByType<MenuPier>();
            if (menuPier != null) menuPier.FecharMenu();

            AlternarMenu(!menuAberto);
        }
    }

    public void AlternarMenu(bool abrir)
    {
        if (painelPrincipal == null) return;
        
        StopAllCoroutines(); // Para animações anteriores
        StartCoroutine(AnimarMenu(abrir));
    }

    // Animação suave de Fade In/Out
    IEnumerator AnimarMenu(bool abrir)
    {
        menuAberto = abrir;
        EstaAberto = abrir;

        if (abrir)
        {
            painelPrincipal.SetActive(true);
            // Atualiza layout para evitar glitches visuais no primeiro frame
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

    // --- GERAÇÃO DA INTERFACE MODERNA ---
    void GerarInterfaceCompleta()
    {
        // 1. Canvas e Limpeza
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

        // 2. Painel Principal (Background)
        painelPrincipal = CriarRetangulo("Painel_Construcao_Moderno", canvasObj.transform);
        Image imgFundo = painelPrincipal.AddComponent<Image>();
        imgFundo.color = corFundoJanela;
        
        // CanvasGroup para animações de fade
        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();
        
        // Layout Centralizado
        RectTransform rtPanel = painelPrincipal.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.1f, 0.1f);
        rtPanel.anchorMax = new Vector2(0.9f, 0.9f);
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;
        
        // Adiciona um contorno sutil (Outline) se desejar, ou apenas sombra
        Outline outline = painelPrincipal.AddComponent<Outline>();
        outline.effectColor = new Color(1, 1, 1, 0.1f);
        outline.effectDistance = new Vector2(1, -1);

        // -- ESTRUTURA INTERNA (Header + Body) --
        VerticalLayoutGroup layoutPrincipal = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutPrincipal.padding = new RectOffset(20, 20, 20, 20);
        layoutPrincipal.spacing = 15;
        layoutPrincipal.childControlHeight = true;
        layoutPrincipal.childControlWidth = true;
        layoutPrincipal.childForceExpandHeight = false; // Header fixo
        
        // Inicializa Modo Demolição se não existir
        if (Object.FindFirstObjectByType<ModoDemolicao>() == null)
        {
            GameObject go = new GameObject("ModoDemolicao_Manager");
            go.AddComponent<ModoDemolicao>();
        }

        // 3. CABEÇALHO (Categorias + Demolição)
        GameObject headerObj = CriarRetangulo("Header_Abas", painelPrincipal.transform);
        LayoutElement leHeader = headerObj.AddComponent<LayoutElement>();
        leHeader.minHeight = 50;
        leHeader.preferredHeight = 50;
        leHeader.flexibleHeight = 0;

        HorizontalLayoutGroup layoutAbas = headerObj.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childControlWidth = true;
        layoutAbas.childForceExpandWidth = true; // Botões preenchem espaço
        layoutAbas.spacing = 10;
        containerAbas = headerObj.transform;

        foreach (DadosConstrucao.CategoriaItem cat in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            CriarBotaoAbaModerno(cat, containerAbas);
        }
        
        // BOTÃO DE DEMOLIÇÃO (Adicionado ao Header)
        // (Removido a pedido do usuário que não gostou da aba extra)
        // CriarBotaoDemolicao(containerAbas);

        // 4. ÁREA DE CONTEÚDO (Scroll)
        GameObject bodyObj = CriarRetangulo("Body_Scroll", painelPrincipal.transform);
        LayoutElement leBody = bodyObj.AddComponent<LayoutElement>();
        leBody.flexibleHeight = 1; // Ocupa o resto do espaço

        // Fundo sutil para a área de scroll
        Image imgBody = bodyObj.AddComponent<Image>();
        imgBody.color = new Color(0, 0, 0, 0.2f); 

        ScrollRect sr = bodyObj.AddComponent<ScrollRect>();
        sr.scrollSensitivity = 15; // Mais suave (era 40)
        sr.decelerationRate = 0.135f; // Parada suave, estilo toque
        sr.elasticity = 0.1f; // "Bounce" sutil no final
        sr.inertia = true;
        sr.horizontal = false;
        sr.vertical = true;

        // Viewport
        GameObject viewport = CriarRetangulo("Viewport", bodyObj.transform);
        Image imgView = viewport.AddComponent<Image>();
        imgView.color = Color.clear; // Transparente!
        viewport.AddComponent<RectMask2D>();
        
        RectTransform rtView = viewport.GetComponent<RectTransform>();
        rtView.anchorMin = Vector2.zero; rtView.anchorMax = Vector2.one;
        rtView.sizeDelta = Vector2.zero;

        // Content
        GameObject content = CriarRetangulo("Content_Grid", viewport.transform);
        containerBotoes = content.transform;
        
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        // Compacto: 100x140
        grid.cellSize = new Vector2(100, 140);
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
        
        // Fundo do botão da aba
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.05f); // Quase transparente inativo

        Button btn = btnObj.AddComponent<Button>();
        
        // Texto da aba
        GameObject txtObj = CriarRetangulo("Texto", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = categoria.ToString().ToUpper();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 8; // Reduzido +10% (era 9)
        txt.fontStyle = FontStyle.Bold;
        txt.color = corTextoSecundario;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 5;
        txt.resizeTextMaxSize = 8;
        txt.raycastTarget = false; // Disable Raycast
        
        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;

        btn.onClick.AddListener(() => 
        {
            if(ModoDemolicao.Instancia) ModoDemolicao.Instancia.AlternarModo(false); // Desativa demolição
            FiltrarPorCategoria(categoria);
        });
    }

    void CriarBotaoDemolicao(Transform pai)
    {
        GameObject btnObj = CriarRetangulo("Btn_Demolicao", pai);
        
        // Fundo Vermelho Sutil
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1, 0, 0, 0.2f); 

        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = CriarRetangulo("Texto", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = "DEMOLIR";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 8;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(1f, 0.5f, 0.5f); // Texto Vermelho Claro
        txt.resizeTextForBestFit = true;
        
        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;

        // Lógica
        btn.onClick.AddListener(() => 
        {
            if(ModoDemolicao.Instancia)
            {
                ModoDemolicao.Instancia.AlternarModo(true);
                AlternarMenu(false); // Fecha o menu para poder clicar nas coisas
                Debug.Log("[Menu] Modo Demolição Ativado!");
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
                img.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.2f); // Glow sutil
                if (txt)
                {
                    txt.color = corDestaque;
                    txt.fontSize = 8; // Mantemos consistente
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

        // Limpa itens antigos
        foreach (Transform child in containerBotoes) Destroy(child.gameObject);

        // Cria novos cards
        foreach (DadosConstrucao item in catalogo)
        {
            if (item != null && item.categoria == categoriaDesejada)
            {
                CriarCardItemModerno(item);
            }
        }
        
        // Força atualização layouts
        StartCoroutine(AtualizarLayouts());
    }

    IEnumerator AtualizarLayouts()
    {
        yield return new WaitForEndOfFrame();
        if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
    }

    // --- CARDS MODERNOS ---
    void CriarCardItemModerno(DadosConstrucao item)
    {
        GameObject cardObj = CriarRetangulo("Card_" + item.nomeItem, containerBotoes);
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;

        // Tornar o card INTEIRO clicável para construção
        Button btnCard = cardObj.AddComponent<Button>();
        btnCard.transition = Selectable.Transition.None; 
        // Passa a imagem de fundo para o feedback de compra
        btnCard.onClick.AddListener(() => ConstruirItem(item, imgBg));
        
        // Layout Vertical do Card
        VerticalLayoutGroup layoutCard = cardObj.AddComponent<VerticalLayoutGroup>();
        layoutCard.padding = new RectOffset(5, 5, 5, 5);
        layoutCard.spacing = 2;
        layoutCard.childControlHeight = true; // IMPORTANT: Force children to stack nicely
        layoutCard.childForceExpandHeight = false;

        // 1. Área do Ícone (Topo, quadrado pequeno)
        GameObject iconArea = CriarRetangulo("AreaIcone", cardObj.transform);
        LayoutElement leIcon = iconArea.AddComponent<LayoutElement>();
        leIcon.minHeight = 60; // Reduzido drasticamente
        leIcon.preferredHeight = 60;
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
            // Placeholder visual bonito
            imgIcon.color = new Color(1, 1, 1, 0.1f);
            GameObject textPlace = CriarRetangulo("TxtPlace", iconArea.transform);
            Text tPlace = textPlace.AddComponent<Text>();
            tPlace.text = "NO IMAGE";
            tPlace.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tPlace.alignment = TextAnchor.MiddleCenter;
            tPlace.fontSize = 7; // Reduzido drasticamente (40%-)
            tPlace.color = new Color(1,1,1,0.2f);
            tPlace.resizeTextForBestFit = true;
            tPlace.resizeTextMaxSize = 7;
            tPlace.raycastTarget = false; // Disable Raycast 
            RectTransform rtTp = textPlace.GetComponent<RectTransform>();
            rtTp.anchorMin = Vector2.zero; rtTp.anchorMax = Vector2.one;
        }

        // 2. Nome do Item
        GameObject nomeObj = CriarRetangulo("NomeItem", cardObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 20; leNome.preferredHeight = 20; // Reduzido
        
        Text tNome = nomeObj.AddComponent<Text>();
        tNome.text = item.nomeItem;
        tNome.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tNome.fontSize = 9; // Menor
        tNome.alignment = TextAnchor.MiddleCenter;
        tNome.color = corTextoPrimario;
        tNome.fontStyle = FontStyle.Bold;
        tNome.resizeTextForBestFit = true;
        tNome.resizeTextMinSize = 8;
        tNome.resizeTextMaxSize = 12;
        tNome.raycastTarget = false; // Disable Raycast
        
        // 3. Preço
        GameObject precoObj = CriarRetangulo("Preco", cardObj.transform);
        LayoutElement lePreco = precoObj.AddComponent<LayoutElement>();
        lePreco.minHeight = 15; lePreco.preferredHeight = 15; // Reduzido

        Text tPreco = precoObj.AddComponent<Text>();
        tPreco.text = $"<color=#00FF00>$ {item.preco}</color>";
        tPreco.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tPreco.fontSize = 9; // Menor
        tPreco.alignment = TextAnchor.MiddleCenter;
        tPreco.supportRichText = true;
        tPreco.raycastTarget = false; // Disable Raycast

        // Spacer to push controls to bottom
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(cardObj.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement leSpacer = spacer.AddComponent<LayoutElement>();
        leSpacer.flexibleHeight = 1; // Pushes everything below down

        // 4. Controles de Quantidade e Compra
        GameObject controlsObj = CriarRetangulo("Controles", cardObj.transform);
        LayoutElement leControls = controlsObj.AddComponent<LayoutElement>();
        leControls.minHeight = 25; leControls.preferredHeight = 25; // Reduzido
        
        HorizontalLayoutGroup layoutControls = controlsObj.AddComponent<HorizontalLayoutGroup>();
        layoutControls.spacing = 2;
        layoutControls.childControlWidth = true;
        layoutControls.childForceExpandWidth = true;

        // Quantidade (Esquerda)
        GameObject qtdBox = CriarRetangulo("BoxQtd", controlsObj.transform);
        HorizontalLayoutGroup layoutQtd = qtdBox.AddComponent<HorizontalLayoutGroup>();
        layoutQtd.spacing = 2;
        
        // Inicializa quantidade
        if (!quantidadesPorItem.ContainsKey(item.nomeItem)) quantidadesPorItem[item.nomeItem] = 1;
        
        // Botão Menos
        GameObject btnMenos = CriarBotaoSimples("-", qtdBox.transform, new Color(1,0.3f,0.3f));
        
        // Texto Qtd
        GameObject txtQtdObj = CriarRetangulo("TxtQtd", qtdBox.transform);
        Text tQtd = txtQtdObj.AddComponent<Text>();
        tQtd.text = quantidadesPorItem[item.nomeItem].ToString();
        tQtd.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tQtd.alignment = TextAnchor.MiddleCenter;
        tQtd.color = Color.white;
        tQtd.fontSize = 14;
        LayoutElement leTxtQ = txtQtdObj.AddComponent<LayoutElement>();
        leTxtQ.flexibleWidth = 1;

        // Botão Mais
        GameObject btnMais = CriarBotaoSimples("+", qtdBox.transform, new Color(0.3f,1f,0.3f));

        // Botão COMPRAR (Direita, maior destaque)
        GameObject btnComprarObj = CriarRetangulo("BtnComprar", controlsObj.transform);
        Image imgComprar = btnComprarObj.AddComponent<Image>();
        imgComprar.color = new Color(0.2f, 0.6f, 0.2f); // Verde botão
        
        Button btnComp = btnComprarObj.AddComponent<Button>();
        LayoutElement leComp = btnComprarObj.AddComponent<LayoutElement>();
        leComp.flexibleWidth = 1.5f;

        GameObject txtCompObj = CriarRetangulo("TxtComp", btnComprarObj.transform);
        Text tComp = txtCompObj.AddComponent<Text>();
        tComp.text = "ABRIR";
        tComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tComp.alignment = TextAnchor.MiddleCenter;
        tComp.color = Color.white;
        tComp.fontSize = 8; // Menor
        tComp.resizeTextForBestFit = true;
        tComp.resizeTextMaxSize = 8;
        tComp.resizeTextMinSize = 5;
        tComp.raycastTarget = false; // Disable Raycast
        RectTransform rtTC = txtCompObj.GetComponent<RectTransform>();
        rtTC.anchorMin = Vector2.zero; rtTC.anchorMax = Vector2.one;

        // Eventos
        Text refTextoQtd = tQtd;
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, -1, refTextoQtd));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.nomeItem, 1, refTextoQtd));
        btnComp.onClick.AddListener(() => ConstruirItem(item, imgBg));
    }

    GameObject CriarBotaoSimples(string texto, Transform pai, Color corTexto)
    {
        GameObject btnObj = CriarRetangulo("Btn" + texto, pai);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1,1,1,0.1f); // Fundo sutil
        
        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = CriarRetangulo("Txt", btnObj.transform);
        Text t = txtObj.AddComponent<Text>();
        t.text = texto;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.color = corTexto;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false; // Disable Raycast
        
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
        
        // --- 1. VERIFICAÇÃO PRELIMINAR DE DINHEIRO (Para Feedback Visual) ---
        int qtd = quantidadesPorItem.ContainsKey(item.nomeItem) ? quantidadesPorItem[item.nomeItem] : 1;
        int custoTotal = item.preco * qtd;
        
        // Se for prédio (qtd sempre 1)
        if (!item.Equals(null) && (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura || item.categoria == DadosConstrucao.CategoriaItem.Energia || item.categoria == DadosConstrucao.CategoriaItem.Urbana))
        {
             custoTotal = item.preco;
        }

        bool temDinheiro = false;
        if (GerenciadorRecursos.Instancia != null)
        {
            if (GerenciadorRecursos.Instancia.dinheiro >= custoTotal) temDinheiro = true;
        }
        else
        {
            // Fallback para GerenteDeJogo antigo
            if (gerente.dinheiroAtual >= custoTotal) temDinheiro = true;
        }

        if (temDinheiro && cardImage != null)
        {
            StartCoroutine(FlashCard(cardImage));
        }
        else if (!temDinheiro && cardImage != null)
        {
            // Opçãonal: Flash Vermelho de erro
            StartCoroutine(FlashCardErro(cardImage));
            // Opcional: Se quiser bloquear a compra aqui... mas o gerente já bloqueia e mostra erro. 
            // Vamos deixar seguir para o gerente mostrar o log de erro se quiser.
        }

        if (item.prefabDaUnidade == null)
        {
            Debug.LogError($"[MenuConstrucao] Prefab '{item.nomeItem}' faltando!");
            return;
        }

        int qtdParaConstruir = 1;
        if(quantidadesPorItem.ContainsKey(item.nomeItem))
        {
            qtdParaConstruir = quantidadesPorItem[item.nomeItem];
        }

        if(item.categoria == DadosConstrucao.CategoriaItem.Marinha)
        {
            // VERIFICAÇÃO CRÍTICA: Só mandar para o Estaleiro se for REALMENTE um navio (unidade)
            // Se não tiver IdentidadeNaval, assumimos que é o PREDIO do Estaleiro/Pier, 
            // então deixamos passar para o Construtor (fantasma) lá embaixo.
            // EXTRA GUARD: O pré-fabricado do Estaleiro NÃO deve ser confundido com um navio!
            bool ehPredioNaval = item.nomeItem.ToLower().Contains("estaleiro") || item.nomeItem.ToLower().Contains("pier") || item.nomeItem.ToLower().Contains("plataforma");

            // Se for categoria Marinha e NÃO for um prédio conhecido, tentamos construir no Estaleiro
            // Isso cobre navios que talvez estejam sem o script IdentidadeNaval na raiz
            bool pareceSerNavio = item.prefabDaUnidade.GetComponent<IdentidadeNaval>() != null 
                                || item.prefabDaUnidade.GetComponentInChildren<IdentidadeNaval>() != null
                                || item.prefabDaUnidade.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

            if (!ehPredioNaval && pareceSerNavio)
            {
                // Tenta achar um Estaleiro com vaga
                Estaleiro[] estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
                Estaleiro estaleiroDisponivel = estaleiros.FirstOrDefault(e => e.TemVaga);

                if (estaleiroDisponivel != null)
                {
                     if (!gerente.TentarGastarDinheiro(item.preco)) return; 

                    Debug.Log("[MenuConstrucao] Construindo no Estaleiro: " + estaleiroDisponivel.name);
                    estaleiroDisponivel.ConstruirUnidade(item.prefabDaUnidade);
                    AlternarMenu(false);
                    return;
                }
                
                // Se não achou estaleiro com vaga, tenta Pier (sem gastar ainda, pier gasta dentro dele? Não, aqui gasta antes)
                // O código original do Pier gastava antes? 
                // Original: if (!gerente.TentarGastarDinheiro(item.preco)) return; ... estaleiroNaval.Construir...
                
                // Vamos manter a lógica: Se não achou Estaleiro Vago, tenta Pier.


                // Fallback para PierMarinha antigo se não achar o novo ou se Estaleiros estiverem cheios
                PierMarinha[] piers = Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
                if(piers.Length > 0)
                {
                    if (!gerente.TentarGastarDinheiro(item.preco)) return;

                    piers[0].ConstruirNavio(item.prefabDaUnidade);
                    AlternarMenu(false);
                    return;
                }
            }
        }

        bool ehUnidadeMovel = item.prefabDaUnidade.GetComponent<UnityEngine.AI.NavMeshAgent>() != null 
                            || item.prefabDaUnidade.GetComponent<ControleUnidade>() != null
                            || item.prefabDaUnidade.GetComponent("Helicoptero") != null; 
        
        string nomeLower = item.prefabDaUnidade.name.ToLower();

        // CORREÇÃO CRÍTICA: Se tiver nome de PRÉDIO, força ser prédio, mesmo que tenha scripts de unidade por engano
        // Isso resolve o "Hangar de Veículos" sendo tratado como unidade
        bool ehPredioExplícito = nomeLower.Contains("hangar") || nomeLower.Contains("fabrica") || nomeLower.Contains("refinaria") || 
                                 nomeLower.Contains("quartel") || nomeLower.Contains("tenda") || nomeLower.Contains("silo") ||
                                 nomeLower.Contains("torre") || nomeLower.Contains("muro") || nomeLower.Contains("wall");

        if (ehPredioExplícito)
        {
            ehUnidadeMovel = false; // Força ser tratado como construção
        }
        else 
        {
            // Verifica também se o nome sugere uma unidade para garantir (Ex: "Helicóptero")
            if (!ehUnidadeMovel)
            {
                 if (nomeLower.Contains("helicoptero") || nomeLower.Contains("soldado") || nomeLower.Contains("tank") || nomeLower.Contains("veiculo"))
                 {
                     ehUnidadeMovel = true;
                 }
            }
        }

        if (ehUnidadeMovel || item.categoria == DadosConstrucao.CategoriaItem.Exercito || item.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
        {
            // É unidade! Manda comprar direto (Spawn na fábrica/heliporto)
            gerente.ComprarUnidade(item.prefabDaUnidade, item.preco, qtdParaConstruir);
            return;
        }

        if (!gerente.TentarGastarDinheiro(item.preco)) return; 
        
        Construtor construtor = Object.FindFirstObjectByType<Construtor>();
        if (construtor != null)
        {
            // CORREÇÃO: Passamos o preço para permitir reembolso se cancelar! E agora a categoria!
            construtor.SelecionarParaConstruir(item.prefabDaUnidade, item.preco, item.categoria);
            AlternarMenu(false); // Fecha o menu para construir
        }
    }

    // --- ANIMAÇÕES DE FEEDBACK ---
    IEnumerator FlashCard(Image img)
    {
        if (img == null) yield break;
        
        Color corOriginal = corCardBase;
        Color corSucesso = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Verde brilhante

        float tempo = 0;
        // Ida (Verde)
        while(tempo < 0.15f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corOriginal, corSucesso, tempo / 0.15f);
            yield return null;
        }

        // Volta (Original)
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
        Color corErro = new Color(0.8f, 0.2f, 0.2f, 0.9f); // Vermelho

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