using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Script para o prédio do Heliporto.
/// Gerencia o ponto de pouso e o menu para chamar helicópteros.
/// Ao clicar no heliporto, abre um menu estilo "C" para selecionar helicópteros.
/// </summary>
public class Heliporto : MonoBehaviour
{
    [Header("Configuração do Ponto de Pouso")]
    [Tooltip("Offset local do ponto de pouso. Aumente o Y para o helicóptero não afundar na plataforma.")]
    public Vector3 pontoDePousoLocal = new Vector3(0, 1.2f, 0); 
    
    [Tooltip("Visualização do ponto de pouso no Gizmo")]
    public float tamanhoPlatforma = 5f;
    
    [Tooltip("Cor do gizmo da plataforma")]
    public Color corPlataforma = new Color(0, 1, 0, 0.5f);

    [Header("Helicópteros no Heliporto")]
    [Tooltip("Lista de helicópteros atualmente pousados aqui")]
    public List<Helicoptero> helicopterosPousados = new List<Helicoptero>();
    
    [Tooltip("Número máximo de helicópteros que podem pousar")]
    public int capacidadeMaxima = 1;

    [Header("Configuração UI")]
    [Tooltip("Largura do menu")]
    public float larguraMenu = 320f;
    
    [Tooltip("Altura de cada botão")]
    public float alturaBotao = 50f;
    
    [Tooltip("Cor de fundo do menu")]
    public Color corFundoMenu = new Color(0.1f, 0.1f, 0.2f, 0.95f);
    
    [Tooltip("Cor do botão disponível")]
    public Color corBotaoDisponivel = new Color(0.2f, 0.5f, 0.3f, 1f);
    
    [Tooltip("Cor do botão ocupado")]
    public Color corBotaoOcupado = new Color(0.5f, 0.2f, 0.2f, 1f);

    // Variáveis Internas UI
    private GameObject canvasObj;
    private GameObject painelMenu;
    private GameObject conteudoScroll;
    private bool menuAberto = false;

    private Animator anim;
    private IdentidadeUnidade identidade;
    private bool selecionado; // Restoring missing field

    void Start()
    {
        CriarInterfaceMenu();
        FecharMenu();

        // Registrar no Gerente para compras futuras
        GerenteDeJogo gerente = FindFirstObjectByType<GerenteDeJogo>();
        if(gerente != null) gerente.RegistrarHeliporto(this);

        // Limpa lista de helicópteros pousados
        helicopterosPousados.RemoveAll(h => h == null);
    }

    void Update()
    {
        // Detecta clique no heliporto
        if (Input.GetMouseButtonDown(0))
        {
            VerificarClique();
        }

        // Fecha menu com ESC
        if (menuAberto && Input.GetKeyDown(KeyCode.Escape))
        {
            FecharMenu();
        }

        // Fecha menu ao clicar fora (botão direito)
        if (menuAberto && Input.GetMouseButtonDown(1))
        {
            FecharMenu();
        }
    }

    /// <summary>
    /// Verifica se o jogador clicou neste heliporto.
    /// </summary>
    void VerificarClique()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Usa Physics.Raycast sem máscara de layer para pegar tudo, ou usa Default
        if (Physics.Raycast(ray, out hit))
        {
            // Debug para saber o que clicou
            // Debug.Log("Clicou em: " + hit.collider.name);

            // Verifica se clicou neste heliporto ou em algum filho dele
            if (hit.collider.transform.root == this.transform || 
                hit.collider.transform == this.transform ||
                hit.collider.GetComponentInParent<Heliporto>() == this)
            {
                Debug.Log("✅ Clique reconhecido no Heliporto!");
                selecionado = true;
                AbrirMenu();
            }
            else if (menuAberto)
            {
                // Se clicou fora e não é UI, fecha
                if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    FecharMenu();
                }
            }
            else
            {
                selecionado = false;
            }
        }
    }

    /// <summary>
    /// Retorna a posição mundial do ponto de pouso.
    /// </summary>
    public Vector3 ObterPontoDePousoMundial()
    {
        return transform.TransformPoint(pontoDePousoLocal);
    }

    /// <summary>
    /// Verifica se há espaço para mais helicópteros.
    /// </summary>
    public bool TemEspacoParaPousar()
    {
        helicopterosPousados.RemoveAll(h => h == null);
        return helicopterosPousados.Count < capacidadeMaxima;
    }

    /// <summary>
    /// Chamado quando um helicóptero pousa neste heliporto.
    /// </summary>
    public void HelicopteroPousou(Helicoptero heli)
    {
        if (heli != null && !helicopterosPousados.Contains(heli))
        {
            helicopterosPousados.Add(heli);
            Debug.Log($"[Heliporto] Helicóptero {heli.nomeHelicoptero} pousou. Total: {helicopterosPousados.Count}");
        }
    }

    /// <summary>
    /// Chamado quando um helicóptero decola deste heliporto.
    /// </summary>
    public void HelicopteroDecolou(Helicoptero heli)
    {
        if (heli != null && helicopterosPousados.Contains(heli))
        {
            helicopterosPousados.Remove(heli);
            Debug.Log($"[Heliporto] Helicóptero {heli.nomeHelicoptero} decolou. Restantes: {helicopterosPousados.Count}");
        }
    }

    // ========================================
    // === SISTEMA DE MENU (Estilo C) ===
    // ========================================

    /// <summary>
    /// Abre o menu de seleção de helicópteros.
    /// </summary>
    void AbrirMenu()
    {
        if (painelMenu == null) return;

        // Atualiza a lista de helicópteros antes de mostrar
        AtualizarListaHelicopteros();

        // Posiciona o menu no centro da tela (estilo C)
        RectTransform rt = painelMenu.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;

        painelMenu.SetActive(true);
        menuAberto = true;
    }

    /// <summary>
    /// Fecha o menu.
    /// </summary>
    void FecharMenu()
    {
        if (painelMenu != null)
        {
            painelMenu.SetActive(false);
        }
        menuAberto = false;
    }

    [Header("Lista Manual (Opcional)")]
    [Tooltip("Arraste helicópteros da cena aqui se eles não aparecerem sozinhos.")]
    public List<Helicoptero> listaManual = new List<Helicoptero>();

    /// <summary>
    /// Atualiza a lista de helicópteros no menu.
    /// </summary>
    void AtualizarListaHelicopteros()
    {
        if (conteudoScroll == null) return;

        // Remove botões antigos
        foreach (Transform child in conteudoScroll.transform)
        {
            Destroy(child.gameObject);
        }

        // --- MODO HÍBRIDO SUPER SEGURO ---
        // 1. Pega do Gerenciador Global
        List<Helicoptero> todosHelicopteros = new List<Helicoptero>();
        if (GerenciadorHelicopteros.Instancia != null)
        {
            todosHelicopteros.AddRange(GerenciadorHelicopteros.Instancia.ObterTodosHelicopteros());
        }

        // 2. FORÇA BRUTA: Varre todo o mapa procurando helicópteros perdidos
        Helicoptero[] encontradosNaCena = FindObjectsByType<Helicoptero>(FindObjectsSortMode.None);
        foreach(var h in encontradosNaCena)
        {
            if(!todosHelicopteros.Contains(h)) todosHelicopteros.Add(h);
        }

        // 3. Adiciona lista manual arrastada no Inspector
        foreach(var h in listaManual)
        {
             if(h != null && !todosHelicopteros.Contains(h)) todosHelicopteros.Add(h);
        }

        // Remove nulos e duplicatas
        todosHelicopteros.RemoveAll(h => h == null);

        if (todosHelicopteros.Count == 0)
        {
            CriarTextoVazio("Nenhum helicóptero encontrado no mapa.");
            return;
        }

        // Cria botão para cada helicóptero
        int indice = 0;
        foreach (Helicoptero heli in todosHelicopteros)
        {
            if (heli != null)
            {
                CriarBotaoHelicoptero(heli, indice);
                indice++;
            }
        }

        // Atualiza tamanho do conteúdo do scroll
        RectTransform conteudoRT = conteudoScroll.GetComponent<RectTransform>();
        conteudoRT.sizeDelta = new Vector2(larguraMenu - 40, (indice * (alturaBotao + 5)) + 10);
    }

    /// <summary>
    /// Cria um botão para um helicóptero específico.
    /// </summary>
    void CriarBotaoHelicoptero(Helicoptero heli, int indice)
    {
        // Container LINHA (Horizontal)
        GameObject rowObj = new GameObject("Row_" + heli.nomeHelicoptero);
        rowObj.transform.SetParent(conteudoScroll.transform);

        RectTransform rowRT = rowObj.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(larguraMenu - 40, alturaBotao);
        rowRT.anchorMin = new Vector2(0.5f, 1f); rowRT.anchorMax = new Vector2(0.5f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = new Vector2(0, -(indice * (alturaBotao + 5)) - 5);

        // --- BOTÃO CHAMAR (Esquerda, maior) ---
        GameObject btnCallObj = new GameObject("Btn_Call");
        btnCallObj.transform.SetParent(rowObj.transform);

        RectTransform btnCallRT = btnCallObj.AddComponent<RectTransform>();
        btnCallRT.anchorMin = Vector2.zero; btnCallRT.anchorMax = Vector2.one;
        btnCallRT.offsetMin = Vector2.zero; btnCallRT.offsetMax = new Vector2(-60, 0); // Deixa espaço na direita

        Image btnImg = btnCallObj.AddComponent<Image>();
        bool disponivel = heli.EstaDisponivel() && TemEspacoParaPousar();
        btnImg.color = disponivel ? corBotaoDisponivel : corBotaoOcupado;

        Button btnCall = btnCallObj.AddComponent<Button>();
        btnCall.interactable = disponivel;
        btnCall.onClick.AddListener(() => ChamarHelicoptero(heli));

        // Texto Chamar
        GameObject txtObj = new GameObject("Texto");
        txtObj.transform.SetParent(btnCallObj.transform);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10, 0); txtRT.offsetMax = new Vector2(-5, 0);
        
        Text txt = txtObj.AddComponent<Text>();
        txt.text = heli.ObterDescricaoMenu();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14; 
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        // --- BOTÃO UPGRADE (Direita, pequeno) ---
        GameObject btnUpObj = new GameObject("Btn_Upgrade");
        btnUpObj.transform.SetParent(rowObj.transform);

        RectTransform btnUpRT = btnUpObj.AddComponent<RectTransform>();
        btnUpRT.anchorMin = new Vector2(1, 0); btnUpRT.anchorMax = new Vector2(1, 1);
        btnUpRT.pivot = new Vector2(1, 0.5f);
        btnUpRT.sizeDelta = new Vector2(55, 0);
        btnUpRT.anchoredPosition = Vector2.zero;

        Image imgUp = btnUpObj.AddComponent<Image>();
        imgUp.color = new Color(0.2f, 0.6f, 0.8f);

        Button btnUp = btnUpObj.AddComponent<Button>();
        
        // Texto Custo Upgrade
        GameObject txtUpObj = new GameObject("Txt_Custo");
        txtUpObj.transform.SetParent(btnUpObj.transform);
        RectTransform txtUpRT = txtUpObj.AddComponent<RectTransform>();
        txtUpRT.anchorMin = Vector2.zero; txtUpRT.anchorMax = Vector2.one;
        txtUpRT.offsetMin = Vector2.zero; txtUpRT.offsetMax = Vector2.zero;

        Text txtUp = txtUpObj.AddComponent<Text>();
        txtUp.text = $"UP\n${heli.custoUpgrade}";
        txtUp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtUp.fontSize = 10;
        txtUp.color = Color.yellow;
        txtUp.alignment = TextAnchor.MiddleCenter;

        // Lógica de Upgrade
        btnUp.onClick.AddListener(() => TentarEvoluir(heli));
    }

    void TentarEvoluir(Helicoptero heli)
    {
        GerenteDeJogo gerente = FindFirstObjectByType<GerenteDeJogo>();
        if(gerente != null)
        {
            if(gerente.TentarGastarDinheiro(heli.custoUpgrade))
            {
                heli.MelhorarHelicoptero();
                AtualizarListaHelicopteros(); // Atualiza a UI para mostrar novo nível e preço
            }
            else
            {
                Debug.Log("Dinheiro insuficiente para o upgrade!");
            }
        }
    }

    /// <summary>
    /// Cria texto quando não há helicópteros.
    /// </summary>
    void CriarTextoVazio(string mensagem)
    {
        GameObject txtObj = new GameObject("TextoVazio");
        txtObj.transform.SetParent(conteudoScroll.transform);

        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.sizeDelta = new Vector2(larguraMenu - 40, 60);
        txtRT.anchorMin = new Vector2(0.5f, 1f);
        txtRT.anchorMax = new Vector2(0.5f, 1f);
        txtRT.pivot = new Vector2(0.5f, 1f);
        txtRT.anchoredPosition = new Vector2(0, -10);

        Text txt = txtObj.AddComponent<Text>();
        txt.text = mensagem;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;
        txt.color = new Color(1f, 0.8f, 0.3f);
        txt.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>
    /// Chama um helicóptero específico para pousar neste heliporto.
    /// </summary>
    void ChamarHelicoptero(Helicoptero heli)
    {
        if (heli == null)
        {
            Debug.LogWarning("[Heliporto] Helicóptero inválido!");
            return;
        }

        if (!heli.EstaDisponivel())
        {
            Debug.LogWarning($"[Heliporto] Helicóptero {heli.nomeHelicoptero} não está disponível!");
            return;
        }

        if (!TemEspacoParaPousar())
        {
            Debug.LogWarning("[Heliporto] Heliporto cheio! Não há espaço para mais helicópteros.");
            return;
        }

        Debug.Log($"[Heliporto] Chamando helicóptero {heli.nomeHelicoptero}...");
        heli.ChamarParaHeliporto(this);

        FecharMenu();
    }

    /// <summary>
    /// Cria toda a interface do menu via script.
    /// </summary>
    void CriarInterfaceMenu()
    {
        // Busca ou cria Canvas
        canvasObj = GameObject.Find("Canvas_Heliportos");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas_Heliportos");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Acima de outros UI

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem se não existir
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSys = new GameObject("EventSystem");
                eventSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // Painel Principal (Estilo Menu C - Centralizado)
        painelMenu = new GameObject("Menu_Heliporto_" + gameObject.GetInstanceID());
        painelMenu.transform.SetParent(canvasObj.transform);

        RectTransform painelRT = painelMenu.AddComponent<RectTransform>();
        painelRT.sizeDelta = new Vector2(larguraMenu, 400);
        painelRT.anchorMin = new Vector2(0.5f, 0.5f);
        painelRT.anchorMax = new Vector2(0.5f, 0.5f);
        painelRT.pivot = new Vector2(0.5f, 0.5f);
        painelRT.anchoredPosition = Vector2.zero;

        Image painelImg = painelMenu.AddComponent<Image>();
        painelImg.color = corFundoMenu;

        // Título
        GameObject tituloObj = new GameObject("Titulo");
        tituloObj.transform.SetParent(painelMenu.transform);

        RectTransform tituloRT = tituloObj.AddComponent<RectTransform>();
        tituloRT.sizeDelta = new Vector2(larguraMenu, 50);
        tituloRT.anchorMin = new Vector2(0.5f, 1f);
        tituloRT.anchorMax = new Vector2(0.5f, 1f);
        tituloRT.pivot = new Vector2(0.5f, 1f);
        tituloRT.anchoredPosition = new Vector2(0, 0);

        Image tituloFundo = tituloObj.AddComponent<Image>();
        tituloFundo.color = new Color(0.15f, 0.4f, 0.6f, 1f);

        GameObject tituloTxtObj = new GameObject("TituloTexto");
        tituloTxtObj.transform.SetParent(tituloObj.transform);

        RectTransform tituloTxtRT = tituloTxtObj.AddComponent<RectTransform>();
        tituloTxtRT.anchorMin = Vector2.zero;
        tituloTxtRT.anchorMax = Vector2.one;
        tituloTxtRT.offsetMin = Vector2.zero;
        tituloTxtRT.offsetMax = Vector2.zero;

        Text tituloTxt = tituloTxtObj.AddComponent<Text>();
        tituloTxt.text = "🚁 SELECIONAR HELICÓPTERO";
        tituloTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tituloTxt.fontSize = 20;
        tituloTxt.fontStyle = FontStyle.Bold;
        tituloTxt.color = Color.white;
        tituloTxt.alignment = TextAnchor.MiddleCenter;

        // Área de Scroll (Lista de Helicópteros)
        GameObject scrollAreaObj = new GameObject("ScrollArea");
        scrollAreaObj.transform.SetParent(painelMenu.transform);

        RectTransform scrollRT = scrollAreaObj.AddComponent<RectTransform>();
        scrollRT.sizeDelta = new Vector2(larguraMenu - 20, 300);
        scrollRT.anchorMin = new Vector2(0.5f, 1f);
        scrollRT.anchorMax = new Vector2(0.5f, 1f);
        scrollRT.pivot = new Vector2(0.5f, 1f);
        scrollRT.anchoredPosition = new Vector2(0, -60);

        Image scrollBg = scrollAreaObj.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.3f);

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollAreaObj.transform);

        RectTransform viewportRT = viewportObj.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        // Conteúdo (onde os botões serão criados)
        conteudoScroll = new GameObject("Conteudo");
        conteudoScroll.transform.SetParent(viewportObj.transform);

        RectTransform conteudoRT = conteudoScroll.AddComponent<RectTransform>();
        conteudoRT.sizeDelta = new Vector2(larguraMenu - 40, 0);
        conteudoRT.anchorMin = new Vector2(0f, 1f);
        conteudoRT.anchorMax = new Vector2(1f, 1f);
        conteudoRT.pivot = new Vector2(0.5f, 1f);
        conteudoRT.anchoredPosition = Vector2.zero;

        // ScrollRect
        ScrollRect scroll = scrollAreaObj.AddComponent<ScrollRect>();
        scroll.content = conteudoRT;
        scroll.viewport = viewportRT;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // Botão Fechar
        GameObject btnFechar = new GameObject("BotaoFechar");
        btnFechar.transform.SetParent(painelMenu.transform);

        RectTransform fecharRT = btnFechar.AddComponent<RectTransform>();
        fecharRT.sizeDelta = new Vector2(larguraMenu - 40, 35);
        fecharRT.anchorMin = new Vector2(0.5f, 0f);
        fecharRT.anchorMax = new Vector2(0.5f, 0f);
        fecharRT.pivot = new Vector2(0.5f, 0f);
        fecharRT.anchoredPosition = new Vector2(0, 10);

        Image fecharImg = btnFechar.AddComponent<Image>();
        fecharImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);

        Button fecharBtn = btnFechar.AddComponent<Button>();
        fecharBtn.onClick.AddListener(FecharMenu);

        GameObject fecharTxtObj = new GameObject("Texto");
        fecharTxtObj.transform.SetParent(btnFechar.transform);

        RectTransform fecharTxtRT = fecharTxtObj.AddComponent<RectTransform>();
        fecharTxtRT.anchorMin = Vector2.zero;
        fecharTxtRT.anchorMax = Vector2.one;
        fecharTxtRT.offsetMin = Vector2.zero;
        fecharTxtRT.offsetMax = Vector2.zero;

        Text fecharTxt = fecharTxtObj.AddComponent<Text>();
        fecharTxt.text = "FECHAR [ESC]";
        fecharTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fecharTxt.fontSize = 16;
        fecharTxt.color = Color.white;
        fecharTxt.alignment = TextAnchor.MiddleCenter;
    }



    // ========================================
    // === VISUAL (GIZMOS) ===
    // ========================================

    void OnDrawGizmos()
    {
        // Desenha o ponto de pouso
        Gizmos.color = corPlataforma;
        Vector3 pontoMundial = transform.TransformPoint(pontoDePousoLocal);
        
        // Marca o ponto EXATO onde o helicóptero vai ficar (pivô dele)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(pontoMundial, 0.3f);
        Gizmos.DrawWireSphere(pontoMundial, 0.5f);

        // Linha indicando a altura em relação ao chão do objeto
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, pontoMundial);

        // Plataforma visual
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(pontoMundial, new Vector3(tamanhoPlatforma, 0.1f, tamanhoPlatforma));
    }

    void OnDrawGizmosSelected()
    {
        // Quando selecionado, mostra mais detalhes
        Gizmos.color = Color.yellow;
        Vector3 pontoMundial = transform.TransformPoint(pontoDePousoLocal);
        Gizmos.DrawWireCube(pontoMundial, new Vector3(tamanhoPlatforma + 1, 1f, tamanhoPlatforma + 1));
    }
}
