using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MenuGoverno : MonoBehaviour
{
    [Header("Design Moderno - Cores & Estilo")]
    public KeyCode teclaAtalho = KeyCode.X;
    
    // Fundo Principal (Glassmorphism Escuro)
    public Color corFundoJanela = new Color(0.12f, 0.12f, 0.15f, 0.95f);
    public Color corDestaque = new Color(0.0f, 0.8f, 1.0f, 1.0f);
    public Color corCardBase = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    public Color corBordaAtiva = new Color(1f, 1f, 1f, 0.3f);
    public Color corTextoSecundario = new Color(0.7f, 0.7f, 0.7f, 1f);
    public Color corTextoPrimario = new Color(0.95f, 0.95f, 0.95f, 1f);

    public static bool EstaAberto;

    // Elementos da UI
    private GameObject painelPrincipal;
    private Transform containerBotoes; // Content do scroll
    private Transform containerAbas;
    private CanvasGroup canvasGroupPainel;
    
    private bool menuAberto = false;

    public enum CategoriaGoverno
    {
        RelacoesExteriores,
        Economia,
        ComercioExterior,
        Interior,
        Defesa,
        Ciencia,
        Trabalho
    }

    private CategoriaGoverno abaAtual = CategoriaGoverno.RelacoesExteriores;

    public static MenuGoverno Instancia;

    void Awake()
    {
        if (Instancia == null) 
        {
            Instancia = this;
        }
        else 
        {
            Destroy(gameObject); // Se o usuário colocou solto na hierarquia, apagamos o clone todo
            return; // CRÍTICO: Não continua gerando interface se for clone!
        }

        GerarInterfaceCompleta();

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
            if (canvasGroupPainel != null) canvasGroupPainel.alpha = 0;
        }
    }

    void Update()
    {
        // Se há caixas de texto ativas, não processa o 'X'
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null)
        {
            return;
        }

        if (Input.GetKeyDown(teclaAtalho))
        {
            AlternarMenu(!menuAberto);
        }
    }

    public void AlternarMenu(bool abrir)
    {
        if (painelPrincipal == null) return;
        
        StopAllCoroutines();
        StartCoroutine(AnimarMenu(abrir));
    }

    IEnumerator AnimarMenu(bool abrir)
    {
        menuAberto = abrir;
        EstaAberto = abrir;

        if (abrir)
        {
            painelPrincipal.SetActive(true);
            MudarAba(abaAtual); // Atualiza dados ao abrir
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

    // --- GERAÇÃO DA INTERFACE ---
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

        Transform painelAntigo = canvasObj.transform.Find("Painel_Governo_Moderno");
        if (painelAntigo != null) DestroyImmediate(painelAntigo.gameObject);

        // Painel Principal
        painelPrincipal = CriarRetangulo("Painel_Governo_Moderno", canvasObj.transform);
        Image imgFundo = painelPrincipal.AddComponent<Image>();
        imgFundo.color = corFundoJanela;
        
        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();
        
        // Layout Completo igual 'C'
        RectTransform rtPanel = painelPrincipal.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.05f, 0.05f); // 90% da largura da tela, ocupa bem mais o monitor
        rtPanel.anchorMax = new Vector2(0.95f, 0.95f);
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;
        
        Outline outline = painelPrincipal.AddComponent<Outline>();
        outline.effectColor = new Color(1, 1, 1, 0.1f);
        outline.effectDistance = new Vector2(1, -1);

        VerticalLayoutGroup layoutPrincipal = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutPrincipal.padding = new RectOffset(10, 10, 10, 10);
        layoutPrincipal.spacing = 5; // Mais próximo
        layoutPrincipal.childControlHeight = true;
        layoutPrincipal.childControlWidth = true;
        layoutPrincipal.childForceExpandHeight = false;

        // -- CABEÇALHO SUPERIOR (Título Gabinete + Botão X)
        GameObject topoObj = CriarRetangulo("Topo_Gabinete", painelPrincipal.transform);
        LayoutElement leTopo = topoObj.AddComponent<LayoutElement>();
        leTopo.minHeight = 25; leTopo.preferredHeight = 25; leTopo.flexibleHeight = 0;
        HorizontalLayoutGroup hlTopo = topoObj.AddComponent<HorizontalLayoutGroup>();
        hlTopo.childControlWidth = true; hlTopo.childForceExpandWidth = false;

        GameObject espacoEsquerdo = CriarRetangulo("EspacoEsq", topoObj.transform);
        espacoEsquerdo.AddComponent<LayoutElement>().flexibleWidth = 1;

        Text txtGabinete = CriarTextoLocal(topoObj, "<b>GABINETE DA PREFEITURA</b>", 13, corTextoPrimario);
        txtGabinete.alignment = TextAnchor.MiddleCenter;
        txtGabinete.gameObject.AddComponent<LayoutElement>().minWidth = 250;

        GameObject espacoDir = CriarRetangulo("EspacoDir", topoObj.transform);
        espacoDir.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Botão Fechar X
        GameObject btnFecharObj = CriarRetangulo("BtnFechar", topoObj.transform);
        btnFecharObj.AddComponent<LayoutElement>().minWidth = 25;
        Image imgFecharBg = btnFecharObj.AddComponent<Image>();
        imgFecharBg.color = new Color(0.2f,0.2f,0.2f, 1f);
        Button btnFechar = btnFecharObj.AddComponent<Button>();
        btnFechar.onClick.AddListener(() => AlternarMenu(false));
        Text txtX = CriarTextoLocal(btnFecharObj, "X", 12, Color.white);
        txtX.alignment = TextAnchor.MiddleCenter;
        RectTransform rtX = txtX.GetComponent<RectTransform>();
        rtX.anchorMin = Vector2.zero; rtX.anchorMax = Vector2.one; rtX.offsetMin = rtX.offsetMax = Vector2.zero;
        
        // -- CABEÇALHO (Abas)
        GameObject headerObj = CriarRetangulo("Header_Abas", painelPrincipal.transform);
        LayoutElement leHeader = headerObj.AddComponent<LayoutElement>();
        leHeader.minHeight = 35; leHeader.preferredHeight = 35; leHeader.flexibleHeight = 0;

        // Barra de Abas preenchendo o espaço Horizontalmente (Sem Scroll)
        containerAbas = headerObj.transform;
        HorizontalLayoutGroup layoutAbas = headerObj.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childControlWidth = true; layoutAbas.childForceExpandWidth = true; // Preenchem o Header total
        layoutAbas.childControlHeight = true; layoutAbas.childForceExpandHeight = true;
        layoutAbas.spacing = 5;

        foreach (CategoriaGoverno cat in System.Enum.GetValues(typeof(CategoriaGoverno)))
        {
            CriarBotaoAba(cat, containerAbas);
        }
        
        // -- CORPO (Conteúdo com Scroll)
        GameObject bodyObj = CriarRetangulo("Body_Scroll", painelPrincipal.transform);
        LayoutElement leBody = bodyObj.AddComponent<LayoutElement>();
        leBody.flexibleHeight = 1;

        Image imgBody = bodyObj.AddComponent<Image>();
        imgBody.color = new Color(0, 0, 0, 0.2f); 

        ScrollRect sr = bodyObj.AddComponent<ScrollRect>();
        sr.scrollSensitivity = 15;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped; // Sem bounce pra lista limpa

        GameObject viewport = CriarRetangulo("Viewport", bodyObj.transform);
        viewport.AddComponent<RectMask2D>();
        RectTransform rtView = viewport.GetComponent<RectTransform>();
        rtView.anchorMin = Vector2.zero; rtView.anchorMax = Vector2.one; rtView.sizeDelta = Vector2.zero;

        GameObject content = CriarRetangulo("Content_Grid", viewport.transform);
        containerBotoes = content.transform;
        sr.content = containerBotoes.GetComponent<RectTransform>();
        
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);
        rtContent.sizeDelta = new Vector2(0, 0); // Trava a largura pra não transbordar e cortar letras!

        // Lista vertical de opções, ao invés de grid de fotos
        VerticalLayoutGroup vGrid = content.AddComponent<VerticalLayoutGroup>();
        vGrid.spacing = 8;
        vGrid.padding = new RectOffset(10, 10, 10, 10);
        vGrid.childAlignment = TextAnchor.UpperCenter;
        vGrid.childControlHeight = true; vGrid.childControlWidth = true;
        vGrid.childForceExpandHeight = false; vGrid.childForceExpandWidth = true;

        ContentSizeFitter csfBody = content.AddComponent<ContentSizeFitter>();
        csfBody.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void CriarBotaoAba(CategoriaGoverno cat, Transform parentP)
    {
        string nomeAba = cat.ToString();
        // Nomes mais amigáveis
        if (cat == CategoriaGoverno.RelacoesExteriores) nomeAba = "Relações Exteriores";
        if (cat == CategoriaGoverno.ComercioExterior) nomeAba = "Comércio Exterior";

        GameObject btnObj = CriarRetangulo("Aba_" + cat.ToString(), parentP);
        
        // Retiramos MinWidth e Preferred para que a HorizontalLayoutGroup divida igualmente
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        
        Image imgBg = btnObj.AddComponent<Image>();
        imgBg.color = new Color(0.18f, 0.22f, 0.25f, 1f); // Fundo azul sombrio

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => MudarAba(cat));
        
        // Cria Text em um objeto filho para não conflitar com o componente Image do botão
        GameObject txtObj = CriarRetangulo("TextoAba", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = nomeAba.ToUpper();
        txt.fontStyle = FontStyle.Bold;
        txt.fontSize = 11; 
        txt.resizeTextForBestFit = true; txt.resizeTextMaxSize = 11; txt.resizeTextMinSize = 5;
        txt.color = corTextoSecundario;
        txt.alignment = TextAnchor.MiddleCenter;
        
        RectTransform rtT = txt.GetComponent<RectTransform>();
        rtT.anchorMin = Vector2.zero; 
        rtT.anchorMax = Vector2.one; 
        rtT.offsetMin = rtT.offsetMax = Vector2.zero;
    }

    void MudarAba(CategoriaGoverno cat)
    {
        abaAtual = cat;
        
        // Pinta Aba Ativa
        foreach (Transform child in containerAbas)
        {
            Image i = child.GetComponent<Image>();
            Text t = child.GetComponentInChildren<Text>();
            if (i != null && t != null)
            {
                if (child.name == "Aba_" + cat.ToString())
                {
                    i.color = corDestaque;
                    t.color = Color.white;
                }
                else
                {
                    i.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                    t.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            }
        }

        // Limpa lista central
        foreach (Transform child in containerBotoes)
        {
            Destroy(child.gameObject);
        }

        // Popula baseado na aba
        switch (cat)
        {
            case CategoriaGoverno.RelacoesExteriores: ExibirRelacoesExteriores(); break;
            case CategoriaGoverno.Economia: ExibirEconomia(); break;
            case CategoriaGoverno.ComercioExterior: ExibirComercioExterior(); break;
            case CategoriaGoverno.Interior: ExibirInterior(); break;
            case CategoriaGoverno.Defesa: ExibirDefesa(); break;
            case CategoriaGoverno.Ciencia: ExibirCiencia(); break;
            case CategoriaGoverno.Trabalho: ExibirTrabalho(); break;
        }
    }

    // ==========================================
    // POPULADORES DAS ABAS DE GOVERNO
    // ==========================================
    
    void ExibirRelacoesExteriores()
    {
        CriarTituloSessao("GABINETE: Política de Fronteiras e Imigração");
        
        string polStr = SistemaConsulado.Instancia != null ? SistemaConsulado.Instancia.politicaAtual.ToString() : "N/A";
        CriarCardAcao(
            "Fronteiras Abertas (Isenção Total)", 
            "Turistas e imigrantes entram livremente. População e Dinheiro sobem muito rápido. RISCO: Espiões inimigos entram disfarçados.",
            "ATIVAR ABERTURA", new Color(0.1f, 0.6f, 0.1f, 1f),
            () => {
                if(SistemaConsulado.Instancia != null) SistemaConsulado.Instancia.politicaAtual = SistemaConsulado.PoliticaFronteira.SimAutomatico;
                Debug.Log("Fronteiras Abertas Ativadas!");
            }
        );

        CriarCardAcao(
            "Vistoria Rigorosa (Alfândega)", 
            "Turistas entram devagar. Renda média. Espiões inimigos têm 80% de chance de serem barrados.",
            "ATIVAR VISTORIA", new Color(0.8f, 0.6f, 0f, 1f),
            () => {
                Debug.Log("[WIP] Vistoria Rigorosa ativada. Lógica de % pendente.");
            }
        );

        CriarCardAcao(
            "Fronteiras Fechadas (Bloqueio)", 
            "Ninguém entra, ninguém sai. Renda de turismo cai pra zero. Segurança total contra civis inimigos.",
            "ATIVAR FECHAMENTO", new Color(0.8f, 0.1f, 0.1f, 1f),
            () => {
                if(SistemaConsulado.Instancia != null) SistemaConsulado.Instancia.politicaAtual = SistemaConsulado.PoliticaFronteira.NaoAutomatico;
                Debug.Log("Fronteiras Fechadas!");
            }
        );

        CriarTituloSessao("Tratados Comerciais e Status com Países");
        CriarCardAcao(
            "Acordo de Aço Estrangeiro", 
            "Paga $100/s continuos para receber 20 de Aço/s automaticamente na sua base.",
            "ASSINAR TRATADO", new Color(0.2f, 0.2f, 0.8f, 1f),
            () => {
                Debug.Log("[WIP] Tratado de aço ativado. O Coroutine de Income/sec será iniciado.");
            }
        );

        // Stub simplório de diplomacia
        CriarTituloSessao("Diplomacia Direta (Status com Vizinhos)");
        CriarCardAcao(
            "Declarar Nação [Team 2] como Aliada", 
            "Garante fim imediato de agressões.",
            "TORNAR ALIADO", new Color(0.3f, 0.7f, 0.9f, 1f),
            () => { Debug.Log("[WIP] Naçao 2 aliada!"); }
        );
        CriarCardAcao(
            "Declarar Nação [Team 2] como Rival/Guerra", 
            "Rompe fronteiras, libera fogo livre de sua IA.",
            "DECLARAR GUERRA", new Color(0.9f, 0.3f, 0.3f, 1f),
            () => { Debug.Log("[WIP] Guerra iniciada contra Naçao 2!"); }
        );
    }

    void ExibirEconomia()
    {
        CriarTituloSessao("GABINETE: Impostos e Casa da Moeda");

        CriarSliderInvestimento(
            "AJUSTE FISCAL (IMPOSTOS SOBRE RENDA)", 
            "Define a fatia de impostos arrecadada da população civil. Tributos elevados congelam o crescimento urbano.",
            15f,
            (novoValor) => { Debug.Log($"[WIP] Impostos em {novoValor}%"); }
        );

        CriarSliderInvestimento(
            "TAXA DE IMPRESSÃO DE MOEDA", 
            "Injeta dinheiro no cofre ciclicamente mas aumenta brutalmente o risco de hiperinflação nacional.",
            0f,
            (novoValor) => { Debug.Log($"[WIP] Nível de Impressão em {novoValor}%"); }
        );
    }

    void ExibirComercioExterior()
    {
        CriarTituloSessao("MERCADO INTERNACIONAL DE COMMODITIES");

        CriarSliderInvestimento(
            "VOLUME DE EXPORTAÇÃO DE PETRÓLEO", 
            "Direciona uma porcentagem da extração de petróleo para a venda imediata internacional salvando contas.",
            0f,
            (novoValor) => { Debug.Log($"[WIP] Exportação de Óleo em {novoValor}%"); }
        );

        CriarSliderInvestimento(
            "VOLUME DE IMPORTAÇÃO DE AÇO", 
            "Paga sobretaxas mensais para forçar a importação cíclica de materiais pesados para as indústrias.",
            0f,
            (novoValor) => { Debug.Log($"[WIP] Importação de Aço em {novoValor}%"); }
        );
    }

    void ExibirInterior()
    {
        CriarTituloSessao("INFRAESTRUTURA E RACIONAMENTO");

        CriarCardAcao(
            "Racionamento Nacional de Energia", 
            "Corta a energia de casas civis. A renda para, mas libera eletricidade imensa para fábricas de armas pesadas.",
            "CORTAR ENERGIA CIVIL", new Color(0.8f, 0.8f, 0.1f, 1f),
            () => { Debug.Log("[WIP] Apagão nas cidades ativado! Liberando MW militar."); }
        );

        CriarTituloSessao("INVESTIMENTOS PÚBLICOS (GABINETE)");

        CriarSliderInvestimento(
            "INVESTIMENTO EM INFRAESTRUTURA URBANA",
            "Aumenta a velocidade de construção e o movimento terrestre.",
            35f,
            (novoValor) => { Debug.Log($"[WIP] Infraestrutura alterada para {novoValor}%"); }
        );

        CriarSliderInvestimento(
            "INVESTIMENTO EM SEGURANÇA NACIONAL",
            "Reduz o risco de sabotagem inimiga e agitação popular.",
            55f,
            (novoValor) => { Debug.Log($"[WIP] Segurança alterada para {novoValor}%"); }
        );
    }

    void ExibirDefesa()
    {
        CriarTituloSessao("MINISTÉRIO DA DEFESA E ALISTAMENTO");

        CriarSliderInvestimento(
            "TAXA DE CONSCRICÃO FORÇADA (DRAFT)", 
            "Manda uma fração populacional para a linha de frente tornando-os soldados. Destrói a felicidade.",
            0f,
            (novoValor) => { Debug.Log($"[WIP] Conscrição em {novoValor}%"); }
        );
    }

    void ExibirCiencia()
    {
        CriarTituloSessao("BLACK OPS E PESQUISA ARMAMENTISTA");

        CriarCardAcao(
            "Licitação: Tanque Pesado T-90", 
            "Compre a patente estrangeira. Sem isso, o Quartel Militar não poderá ser construído/fabricar T-90.",
            "CUSTO: $50.000", new Color(0.2f, 0.2f, 0.6f, 1f),
            () => { Debug.Log("[WIP] Licitação do T-90 comprada. Veículo desbloqueado."); }
        );

        CriarCardAcao(
            "Engenharia Nuclear", 
            "Investe pesado em tecnologia sub-atômica par abrir viabilidade de ogivas.",
            "PESQUISAR", new Color(0.4f, 0.8f, 0.2f, 1f),
            () => { Debug.Log("[WIP] Silo Nuclear em estado de pesquisa."); }
        );
    }

    void ExibirTrabalho()
    {
        CriarTituloSessao("PARQUE INDUSTRIAL");

        CriarCardAcao(
            "Turnos Dobrados", 
            "Força os operários das fábricas 24h. Veículos fabricam 50% mais rápido, mas a manutenção em aço deles dobra.",
            "OBRIGAR TURNOS", new Color(0.9f, 0.5f, 0.1f, 1f),
            () => { Debug.Log("[WIP] Indústrias sob stress. Custo de Steel duplicado, Velocidade Vroom!"); }
        );
    }


    // ==========================================
    // UTILITÁRIOS DA UI INTERNA
    // ==========================================
    void CriarTituloSessao(string tituloStr)
    {
        GameObject txtObj = CriarRetangulo("TitSessao", containerBotoes);
        txtObj.AddComponent<LayoutElement>().minHeight = 25;
        
        // Fundo sutíl para destacar as divisões
        Image bgImg = txtObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.15f, 0.2f, 0.3f); 

        GameObject txtReal = CriarRetangulo("Txt", txtObj.transform);
        Text txt = txtReal.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = $"<color=#00ffff><b>{tituloStr.ToUpper()}</b></color>"; // Cor Ciano Neon vivo
        txt.fontSize = 11; txt.supportRichText = true;
        
        RectTransform rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; 
        rt.offsetMin = new Vector2(10, 0); // Padding esquerdo
        rt.offsetMax = Vector2.zero;
        txt.alignment = TextAnchor.MiddleLeft;
    }

    void CriarSliderInvestimento(string titulo, string descricao, float valorAtualPct, UnityEngine.Events.UnityAction<float> mudanca)
    {
        GameObject cardObj = CriarRetangulo("SliderCard", containerBotoes);
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;
        cardObj.AddComponent<Outline>().effectColor = corBordaAtiva;
        
        HorizontalLayoutGroup hLayout = cardObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(8, 8, 4, 4);
        hLayout.spacing = 10;
        hLayout.childControlHeight = true; hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth = true; hLayout.childForceExpandWidth = false;

        LayoutElement leCard = cardObj.AddComponent<LayoutElement>();
        leCard.minHeight = 45;

        // Container Esquerdo (Texto)
        GameObject txtContainer = CriarRetangulo("TxtContainer", cardObj.transform);
        VerticalLayoutGroup vTxt = txtContainer.AddComponent<VerticalLayoutGroup>();
        vTxt.childControlHeight = true; vTxt.childForceExpandHeight = false;
        vTxt.childControlWidth = true; vTxt.childForceExpandWidth = true;
        vTxt.spacing = 2;
        txtContainer.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Text tTit = CriarTextoLocal(txtContainer, titulo, 11, Color.white);
        tTit.fontStyle = FontStyle.Bold;
        Text tDesc = CriarTextoLocal(txtContainer, $"{descricao} (Atual: {valorAtualPct}%)", 9, new Color(0.8f, 0.8f, 0.8f, 1f));

        // -- Container Direito (Slider + Controles)
        GameObject ctrlContainer = CriarRetangulo("CtrlContainer", cardObj.transform);
        HorizontalLayoutGroup hCtrl = ctrlContainer.AddComponent<HorizontalLayoutGroup>();
        hCtrl.childControlWidth = true; hCtrl.childForceExpandWidth = false;
        hCtrl.spacing = 5;
        ctrlContainer.AddComponent<LayoutElement>().minWidth = 250;

        // Botão Menos
        GameObject btnMenosObj = CriarRetangulo("BtnMenos", ctrlContainer.transform);
        btnMenosObj.AddComponent<LayoutElement>().minWidth = 25;
        Image bgMenos = btnMenosObj.AddComponent<Image>();
        bgMenos.color = new Color(0.2f,0.2f,0.25f,1f);
        Button btnMenos = btnMenosObj.AddComponent<Button>();
        Text txtMenos = CriarTextoLocal(btnMenosObj, "-", 14, Color.white);
        txtMenos.alignment = TextAnchor.MiddleCenter;

        // Slider Base
        GameObject sliderObj = CriarRetangulo("Slider", ctrlContainer.transform);
        sliderObj.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Slider slider = sliderObj.AddComponent<Slider>();
        
        // Estrutura física do Slider exigida pela Unity
        GameObject bgCor = CriarRetangulo("Background", sliderObj.transform);
        bgCor.AddComponent<Image>().color = new Color(0.1f,0.1f,0.1f, 1f);
        RectTransform rtB = bgCor.GetComponent<RectTransform>();
        rtB.anchorMin = new Vector2(0, 0.35f); rtB.anchorMax = new Vector2(1, 0.65f);
        rtB.offsetMin = Vector2.zero; rtB.offsetMax = Vector2.zero;

        GameObject fillArea = CriarRetangulo("Fill Area", sliderObj.transform);
        RectTransform rtFA = fillArea.GetComponent<RectTransform>();
        rtFA.anchorMin = new Vector2(0, 0.35f); rtFA.anchorMax = new Vector2(1, 0.65f);
        rtFA.offsetMin = Vector2.zero; rtFA.offsetMax = Vector2.zero;
        
        GameObject fill = CriarRetangulo("Fill", fillArea.transform);
        fill.AddComponent<Image>().color = corDestaque; // Ciano da barra
        RectTransform rtF = fill.GetComponent<RectTransform>();
        rtF.anchorMin = Vector2.zero; rtF.anchorMax = Vector2.zero; 
        rtF.offsetMin = Vector2.zero; rtF.offsetMax = Vector2.zero;
        
        GameObject handleArea = CriarRetangulo("Handle Slide Area", sliderObj.transform);
        RectTransform rtHA = handleArea.GetComponent<RectTransform>();
        rtHA.anchorMin = Vector2.zero; rtHA.anchorMax = Vector2.one;
        rtHA.offsetMin = Vector2.zero; rtHA.offsetMax = Vector2.zero;
        
        GameObject handle = CriarRetangulo("Handle", handleArea.transform);
        Image imgHandle = handle.AddComponent<Image>();
        imgHandle.color = Color.white;
        RectTransform rtH = handle.GetComponent<RectTransform>();
        rtH.anchorMin = new Vector2(0,0.2f); rtH.anchorMax = new Vector2(0,0.8f);
        rtH.sizeDelta = new Vector2(10, 0); // Grossura de 10px da alavanca
        rtH.offsetMin = new Vector2(-5, 0); 
        rtH.offsetMax = new Vector2(5, 0);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.minValue = 0; slider.maxValue = 100;
        slider.wholeNumbers = true;
        slider.value = valorAtualPct;

        // Botão Mais
        GameObject btnMaisObj = CriarRetangulo("BtnMais", ctrlContainer.transform);
        btnMaisObj.AddComponent<LayoutElement>().minWidth = 25;
        Image bgMais = btnMaisObj.AddComponent<Image>();
        bgMais.color = new Color(0.2f,0.2f,0.25f,1f);
        Button btnMais = btnMaisObj.AddComponent<Button>();
        Text txtMais = CriarTextoLocal(btnMaisObj, "+", 14, Color.white);
        txtMais.alignment = TextAnchor.MiddleCenter;

        // Valor Texto Final
        GameObject valObj = CriarRetangulo("TxtValor", ctrlContainer.transform);
        valObj.AddComponent<LayoutElement>().minWidth = 40;
        Text valTxt = CriarTextoLocal(valObj, $"{valorAtualPct}%", 11, Color.white);
        valTxt.alignment = TextAnchor.MiddleCenter;

        // Lógicas do Slider
        btnMenos.onClick.AddListener(() => { slider.value -= 5f; });
        btnMais.onClick.AddListener(() => { slider.value += 5f; });
        
        slider.onValueChanged.AddListener((val) => {
            valTxt.text = $"{val}%";
            tDesc.text = $"{descricao} (Atual: {val}%)";
            mudanca?.Invoke(val);
        });
    }

    void CriarCardAcao(string titulo, string descricao, string lblBotao, Color corBotao, UnityEngine.Events.UnityAction acao)
    {
        GameObject cardObj = CriarRetangulo("CardAcao", containerBotoes);
        
        // Background do Card
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;
        
        // Outline suave
        cardObj.AddComponent<Outline>().effectColor = corBordaAtiva;
        
        HorizontalLayoutGroup hLayout = cardObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(4, 4, 4, 4);
        hLayout.spacing = 5;
        hLayout.childControlHeight = true; hLayout.childForceExpandHeight = true;
        hLayout.childControlWidth = true; hLayout.childForceExpandWidth = false;

        LayoutElement leCard = cardObj.AddComponent<LayoutElement>();
        leCard.minHeight = 35; // Extremamente compacto (-50%)
        leCard.flexibleHeight = 0;

        // Container Esquerdo (Texto)
        GameObject txtContainer = CriarRetangulo("TxtContainer", cardObj.transform);
        VerticalLayoutGroup vTxt = txtContainer.AddComponent<VerticalLayoutGroup>();
        vTxt.childControlHeight = true; vTxt.childForceExpandHeight = true;
        vTxt.childControlWidth = true; vTxt.childForceExpandWidth = true;
        vTxt.spacing = 1;

        LayoutElement leTxtC = txtContainer.AddComponent<LayoutElement>();
        leTxtC.flexibleWidth = 1f;

        Text tTit = CriarTextoLocal(txtContainer, titulo, 10, Color.white);
        tTit.fontStyle = FontStyle.Bold;
        tTit.resizeTextForBestFit = true; tTit.resizeTextMaxSize = 10; tTit.resizeTextMinSize = 6;
        
        Text tDesc = CriarTextoLocal(txtContainer, descricao, 8, new Color(0.8f, 0.8f, 0.8f, 1f));
        tDesc.resizeTextForBestFit = true; tDesc.resizeTextMaxSize = 8; tDesc.resizeTextMinSize = 5;

        // Botão Direita
        GameObject btnObj = CriarRetangulo("BtnAcao", cardObj.transform);
        LayoutElement leBtn = btnObj.AddComponent<LayoutElement>();
        leBtn.minWidth = 80; leBtn.preferredWidth = 80; // Compacto o botão tb

        Image imgBtn = btnObj.AddComponent<Image>();
        imgBtn.color = corBotao;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(acao);
        
        // Efeito simples
        btn.transition = Selectable.Transition.ColorTint;

        Text tLabelBtn = CriarTextoLocal(btnObj, lblBotao, 9, Color.white);
        tLabelBtn.fontStyle = FontStyle.Bold;
        tLabelBtn.alignment = TextAnchor.MiddleCenter;
        tLabelBtn.resizeTextForBestFit = true; tLabelBtn.resizeTextMaxSize = 9; tLabelBtn.resizeTextMinSize = 5;
        
        RectTransform rtLB = tLabelBtn.GetComponent<RectTransform>();
        rtLB.anchorMin = Vector2.zero; rtLB.anchorMax = Vector2.one; rtLB.offsetMin = rtLB.offsetMax = Vector2.zero;
    }

    Text CriarTextoLocal(GameObject parentObj, string texto, int size, Color c)
    {
        GameObject textObj = CriarRetangulo("Txt", parentObj.transform);
        Text txt = textObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = texto; txt.fontSize = size; txt.color = c;
        txt.supportRichText = true;
        return txt;
    }

    GameObject CriarRetangulo(string n, Transform p)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        return go;
    }
}
