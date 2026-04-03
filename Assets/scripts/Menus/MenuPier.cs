using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MenuPier : MonoBehaviour
{
    private static int ultimoFrameAtalho = -1;

    // --- REFERÊNCIAS ---
    [Header("Conexão")]
    public PierMarinha pierAlvo; // Arraste o objeto Pier aqui no Inspector!

    // --- CONFIGURAÇÃO VISUAL ---
    [Header("Cores da UI")]
    public Color corFundo = new Color(0.1f, 0.15f, 0.25f, 0.95f);
    public Color corDocaLivre = new Color(0.2f, 0.6f, 0.3f);
    public Color corDocaOcupada = new Color(0.7f, 0.25f, 0.25f);
    public Color corBotaoAcao = new Color(0.2f, 0.5f, 0.8f);
    public bool debugLogs = false;

    // --- VARIÁVEIS INTERNAS ---
    private GameObject painelMestre;
    private bool menuAberto = false;
    public static bool EstaAberto; // 🔹 STATICO PARA OCULTAR HUD
    
    // Containers para listas dinâmicas
    private Transform listaDocasContainer;
    private Transform listaContextoContainer; 
    private Text tituloContexto;
    private readonly List<IdentidadeNaval> naviosBuffer = new List<IdentidadeNaval>(64);

    void Update()
    {
        // Tecla de atalho alterada para 'V' conforme solicitado
        if (Input.GetKeyDown(KeyCode.V))
        {
            AlternarPorAtalho(pierAlvo);
        }
    }

    public static bool AlternarPorAtalho(PierMarinha pierPreferido = null)
    {
        if (Time.frameCount == ultimoFrameAtalho)
        {
            return false;
        }

        ultimoFrameAtalho = Time.frameCount;

        MenuPier menu = Object.FindFirstObjectByType<MenuPier>();
        if (menu == null)
        {
            GameObject go = new GameObject("MenuPier_Auto");
            menu = go.AddComponent<MenuPier>();
        }

        if (pierPreferido != null)
        {
            menu.pierAlvo = pierPreferido;
        }
        else if (menu.pierAlvo == null)
        {
            menu.pierAlvo = RegistroEntidadesJogo.GetPrimeiroPier();
            if (menu.pierAlvo == null)
            {
                menu.pierAlvo = Object.FindFirstObjectByType<PierMarinha>();
            }
        }

        menu.AlternarMenu();
        return true;
    }

    // Método chamado por outros scripts (como MenuConstrucao) para fechar este menu
    public void FecharMenu()
    {
        if (menuAberto)
        {
            AlternarMenu();
        }
    }

    public void AlternarMenu()
    {
        // 1. Garante que temos um Pier alvo
        if (pierAlvo == null)
        {
            pierAlvo = RegistroEntidadesJogo.GetPrimeiroPier();
            if (pierAlvo == null) pierAlvo = FindFirstObjectByType<PierMarinha>();
        }

        if (pierAlvo == null)
        {
            Debug.LogWarning("[MenuPier] Nenhum objeto 'PierMarinha' encontrado na cena. Construa um Pier primeiro para acessar este menu.");
            return;
        }

        // 2. Garante que a UI existe
        if (painelMestre == null) CriarInterfaceDoZero();

        // 3. Alterna visibilidade
        menuAberto = !menuAberto;
        EstaAberto = menuAberto; // 🔹 Atualiza global
        painelMestre.SetActive(menuAberto);
        
        if (debugLogs)
        {
            Debug.Log($"[MenuPier] Menu alternado. Aberto: {menuAberto}");
        }

        if (menuAberto)
        {
            // Fecha os outros 
            MenuConstrucao menuCon = Object.FindFirstObjectByType<MenuConstrucao>();
            if (menuCon != null && MenuConstrucao.EstaAberto) menuCon.AlternarMenu(false);

            MenuMisseis menuMiss = Object.FindFirstObjectByType<MenuMisseis>();
            if (menuMiss != null && MenuMisseis.EstaAberto) menuMiss.CancelarLancamento();

            MenuGoverno menuGov = Object.FindFirstObjectByType<MenuGoverno>();
            if (menuGov != null && MenuGoverno.EstaAberto) menuGov.AlternarMenu(false);

            AtualizarListaDeDocas();
            LimparPainelContexto("Selecione uma doca à esquerda...");
        }
    }

    // =========================================================
    //              LÓGICA DE ATUALIZAÇÃO (CORE)
    // =========================================================

    void AtualizarListaDeDocas()
    {
        // Limpa lista visual anterior
        foreach (Transform child in listaDocasContainer) Destroy(child.gameObject);

        if (pierAlvo.vagasDisponiveis == null || pierAlvo.vagasDisponiveis.Count == 0)
        {
            CriarTextoLista(listaDocasContainer, "<color=yellow>Nenhuma vaga configurada!</color>\n<size=11>Configure no Inspector.</size>");
            return;
        }

        // Cria um botão para cada vaga configurada no Pier
        for (int i = 0; i < pierAlvo.vagasDisponiveis.Count; i++)
        {
            int index = i; // Cópia local para o botão funcionar no loop
            var vaga = pierAlvo.vagasDisponiveis[i];
            
            bool livre = vaga.EstaLivre();
            Color corStatus = livre ? corDocaLivre : corDocaOcupada;
            
            // Texto do botão
            string nomeNavio = livre ? "-- Vazia --" : (vaga.navioOcupante != null ? vaga.navioOcupante.nomeDoNavio : "Erro: Navio Nulo");
            string txtStatus = $"<b>{vaga.nomeDaVaga}</b>\n<size=12>{vaga.categoriaAceita}</size>\n<color={(livre?"#aaffaa":"#ffaaaa")}>{nomeNavio}</color>";

            // Cria o botão da doca na lista da esquerda
            CriarBotaoLista(listaDocasContainer, txtStatus, corStatus, () => {
                SelecionarDoca(index);
            });
        }
    }

    void SelecionarDoca(int indexVaga)
    {
        if (pierAlvo.vagasDisponiveis == null || indexVaga >= pierAlvo.vagasDisponiveis.Count) return;

        var vaga = pierAlvo.vagasDisponiveis[indexVaga];
        LimparPainelContexto(""); // Limpa painel da direita

        if (vaga.EstaLivre())
        {
            // === MODO CHAMAR NAVIO ===
            tituloContexto.text = $"CHAMAR: {vaga.categoriaAceita}\n<size=12>(Raio: {pierAlvo.raioDeBusca}m)</size>";
            tituloContexto.color = Color.green;

            // Busca todos os piers na cena manualmente
            List<IdentidadeNaval> naviosCompativeis = EncontrarNaviosDisponiveis(vaga.categoriaAceita);

            if (naviosCompativeis.Count == 0)
            {
                CriarTextoLista(listaContextoContainer, "Nenhum navio compatível e livre encontrado no raio de alcance.");
            }
            else
            {
                foreach (var navio in naviosCompativeis)
                {
                    float dist = Vector3.Distance(pierAlvo.transform.position, navio.transform.position);
                    string info = $"<b>{navio.nomeDoNavio}</b>\n<size=11>Distância: {dist:F0}m</size>";
                    
                    CriarBotaoLista(listaContextoContainer, info, corBotaoAcao, () => {
                        // Ação: Chamar o navio
                        pierAlvo.AtribuirVaga(vaga, navio);
                        AtualizarListaDeDocas(); // Atualiza esquerda
                        SelecionarDoca(indexVaga); // Atualiza direita (mostra que ocupou)
                    });
                }
            }
        }
        else
        {
            // === MODO LIBERAR NAVIO ===
            string nome = vaga.navioOcupante != null ? vaga.navioOcupante.nomeDoNavio : "Desconhecido";
            tituloContexto.text = $"GERENCIAR: {nome}\n<size=12>Escolha o destino de saída:</size>";
            tituloContexto.color = Color.yellow;

            // Opção 1: Saída Automática (Padrão)
            CriarBotaoLista(listaContextoContainer, "SAÍDA AUTOMÁTICA\n(Mais Próxima)", Color.red, () => {
                if (vaga.navioOcupante != null) pierAlvo.LiberarVaga(vaga); // Null = automático
                AtualizarListaDeDocas(); 
                SelecionarDoca(indexVaga); 
            });

            // Opção 2: Listar pontos de saída específicos
            if (pierAlvo.pontosDeSaida != null)
            {
                for(int i=0; i < pierAlvo.pontosDeSaida.Length; i++)
                {
                    Transform saida = pierAlvo.pontosDeSaida[i];
                    if (saida != null)
                    {
                        CriarBotaoLista(listaContextoContainer, $"IR PARA: {saida.name}", new Color(0.5f, 0.2f, 0.2f), () => {
                            pierAlvo.LiberarVaga(vaga, saida);
                            AtualizarListaDeDocas();
                            SelecionarDoca(indexVaga);
                        });
                    }
                }
            }
        }
    }

    public void RegistrarNovoPier(PierMarinha pier)
    {
        if (pierAlvo == null)
        {
            pierAlvo = pier;
        }
    }

    // Função auxiliar para buscar navios
    List<IdentidadeNaval> EncontrarNaviosDisponiveis(IdentidadeNaval.CategoriaNavio categoria)
    {
        var lista = new List<IdentidadeNaval>();
        RegistroEntidadesJogo.FillNavios(naviosBuffer);

        if (debugLogs)
        {
            Debug.Log($"[MenuPier] Buscando navios da categoria: {categoria}. Total de navios na cena: {naviosBuffer.Count}");
        }

        foreach (var navio in naviosBuffer)
        {
            if (navio == null) continue;

            float distancia = Vector3.Distance(pierAlvo.transform.position, navio.transform.position);
            
            // Debug para entender o que está acontecendo
            if (debugLogs) Debug.Log($"[MenuPier] Checando navio: '{navio.nomeDoNavio}' ({navio.name}) | Cat: {navio.categoriaNavio} | Atracado: {navio.EstaAtracado} | Dist: {distancia:F1}m");

            // Filtros: Categoria certa + Não está atracado
            bool catCheck = (navio.categoriaNavio == categoria);
            
            // CORREÇÃO: Aceita qualquer navio compatível, com ou sem NavMeshAgent
            if (catCheck && !navio.EstaAtracado)
            {
                lista.Add(navio);
                if (debugLogs) Debug.Log($"[MenuPier] --> Navio '{navio.nomeDoNavio}' ACEITO!");
            }
            else
            {
                string motivo = !catCheck ? "Categoria diferente" : "Já está atracado";
                if (debugLogs) Debug.Log($"[MenuPier] --> Navio '{navio.nomeDoNavio}' REJEITADO. Motivo: {motivo}");
            }
        }
        
        if (debugLogs) Debug.Log($"[MenuPier] Encontrados: {lista.Count} navios compatíveis");

        // Ordena por distância (mais perto primeiro)
        lista.Sort((a, b) =>
        {
            float distA = Vector3.Distance(pierAlvo.transform.position, a.transform.position);
            float distB = Vector3.Distance(pierAlvo.transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });
        return lista;
    }

    void LimparPainelContexto(string msgPadrao)
    {
        foreach (Transform child in listaContextoContainer) Destroy(child.gameObject);
        tituloContexto.text = msgPadrao;
        tituloContexto.color = Color.white;
    }

    // =========================================================
    //              CONSTRUÇÃO DA INTERFACE (UI FACTORY)
    // =========================================================
    
    void CriarInterfaceDoZero()
    {
        // 1. Canvas
        GameObject canvasObj = GameObject.Find("Canvas_Game");
        if (canvasObj == null)
        {
            // Tenta achar qualquer canvas
            Canvas c = Object.FindFirstObjectByType<Canvas>();
            if (c != null) canvasObj = c.gameObject;
            else
            {
                canvasObj = new GameObject("Canvas_Game", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
        }

        // 2. Painel Mestre (Janela) -> Reduzido (~25% menor, centralizado)
        // Original: (0.1, 0.1) -> (0.9, 0.9)
        // Novo: (0.2, 0.2) -> (0.8, 0.8)
        painelMestre = CriarPainel(canvasObj.transform, "Janela_Pier", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), corFundo);

        // 3. Header (Título + Botão Fechar)
        GameObject header = CriarPainel(painelMestre.transform, "Header", new Vector2(0, 0.9f), new Vector2(1, 1), Color.clear);
        CriarTexto(header.transform, "CONTROLE DE TRÁFEGO NAVAL", new Vector2(0.02f, 0), new Vector2(0.8f, 1), 24, Color.cyan, TextAnchor.MiddleLeft);
        
        CriarBotao(header.transform, "X", new Vector2(0.92f, 0.1f), new Vector2(0.99f, 0.9f), new Color(0.8f, 0.2f, 0.2f), () => AlternarMenu());

        // 4. Divisão do Corpo
        // Lado Esquerdo (40%) - Lista de Docas
        GameObject areaDocas = CriarPainel(painelMestre.transform, "AreaDocas", new Vector2(0.02f, 0.05f), new Vector2(0.40f, 0.88f), new Color(0,0,0,0.2f));
        CriarTexto(areaDocas.transform, "DOCAS DISPONÍVEIS", new Vector2(0, 0.92f), new Vector2(1, 1), 14, Color.yellow, TextAnchor.MiddleCenter);
        
        GameObject scrollDocas = CriarScrollArea(areaDocas.transform, "Scroll", new Vector2(0, 0), new Vector2(1, 0.92f));
        listaDocasContainer = scrollDocas.GetComponent<ScrollRect>().content;

        // Lado Direito (58%) - Contexto (Ações)
        GameObject areaContexto = CriarPainel(painelMestre.transform, "AreaContexto", new Vector2(0.42f, 0.05f), new Vector2(0.98f, 0.88f), new Color(0,0,0,0.3f));
        
        // Título do Contexto
        GameObject headerCtx = CriarPainel(areaContexto.transform, "HeadCtx", new Vector2(0, 0.85f), new Vector2(1, 1), Color.clear);
        tituloContexto = CriarTexto(headerCtx.transform, "Selecione uma doca...", new Vector2(0.05f, 0), new Vector2(0.95f, 1), 16, Color.white, TextAnchor.MiddleCenter);

        // Lista do Contexto
        GameObject scrollCtx = CriarScrollArea(areaContexto.transform, "ScrollCtx", new Vector2(0, 0), new Vector2(1, 0.85f));
        listaContextoContainer = scrollCtx.GetComponent<ScrollRect>().content;

        painelMestre.SetActive(false);
    }

    // --- Helpers de Criação ---

    GameObject CriarPainel(Transform pai, string nome, Vector2 min, Vector2 max, Color cor) {
        GameObject obj = new GameObject(nome, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(pai, false);
        obj.GetComponent<Image>().color = cor;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return obj;
    }

    Text CriarTexto(Transform pai, string msg, Vector2 min, Vector2 max, int tam, Color cor, TextAnchor align) {
        GameObject obj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(pai, false);
        Text t = obj.GetComponent<Text>();
        t.text = msg; 
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tam; t.color = cor; t.alignment = align; t.resizeTextForBestFit = false;
        t.raycastTarget = false;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    GameObject CriarBotao(Transform pai, string msg, Vector2 min, Vector2 max, Color cor, UnityEngine.Events.UnityAction acao) {
        GameObject obj = CriarPainel(pai, "Btn", min, max, cor);
        Button b = obj.AddComponent<Button>();
        b.onClick.AddListener(acao);
        CriarTexto(obj.transform, msg, Vector2.zero, Vector2.one, 14, Color.white, TextAnchor.MiddleCenter);
        return obj;
    }

    GameObject CriarScrollArea(Transform pai, string nome, Vector2 min, Vector2 max) {
        GameObject root = CriarPainel(pai, nome, min, max, Color.clear);
        ScrollRect sr = root.AddComponent<ScrollRect>();
        
        GameObject view = CriarPainel(root.transform, "View", Vector2.zero, Vector2.one, Color.clear);
        view.AddComponent<RectMask2D>();

        GameObject content = CriarPainel(view.transform, "Content", new Vector2(0,1), new Vector2(1,1), Color.clear);
        content.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
        
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true; vlg.childControlWidth = true; vlg.childForceExpandHeight = false; 
        vlg.spacing = 5; vlg.padding = new RectOffset(5,5,5,5);
        
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = content.GetComponent<RectTransform>();
        sr.viewport = view.GetComponent<RectTransform>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 20;
        return root;
    }

    GameObject CriarBotaoLista(Transform pai, string texto, Color cor, UnityEngine.Events.UnityAction acao) {
        GameObject btn = new GameObject("BtnItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btn.transform.SetParent(pai, false);
        
        btn.GetComponent<Image>().color = cor;
        btn.GetComponent<Button>().onClick.AddListener(acao);
        btn.GetComponent<LayoutElement>().minHeight = 60; // Altura fixa do item
        
        GameObject txtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(btn.transform, false);
        Text t = txtObj.GetComponent<Text>();
        t.text = texto; t.color = Color.white; t.supportRichText = true;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleLeft; 
        
        // Margem interna do texto
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 0); rt.offsetMax = new Vector2(-10, 0);
        
        return btn;
    }

    void CriarTextoLista(Transform pai, string texto) {
        GameObject obj = new GameObject("Info", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        obj.transform.SetParent(pai, false);
        Text t = obj.GetComponent<Text>();
        t.text = texto; t.color = Color.gray; 
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Italic;
        obj.GetComponent<LayoutElement>().minHeight = 40;
    }
}
