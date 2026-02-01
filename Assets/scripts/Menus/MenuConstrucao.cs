using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq; 

public class MenuConstrucao : MonoBehaviour
{
    [Header("Configurações")]
    public KeyCode teclaAtalho = KeyCode.C;
    public Color corFundo = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    public Color corAbas = new Color(0.2f, 0.2f, 0.25f);
    
    [Header("Catálogo")]
    [Tooltip("Marque para carregar TODAS as fichas DadosConstrucao do projeto automaticamente")]
    public bool autoCarregarFichas = true;
    
    [Tooltip("Lista manual de fichas (usada se autoCarregarFichas = false)")]
    public List<DadosConstrucao> catalogo = new List<DadosConstrucao>();
    public static List<DadosConstrucao> catalogoGlobal; // Acesso global para a IA

    // Variáveis Internas
    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private GerenteDeJogo gerente;
    private bool menuAberto = true;

    void Start()
    {
        gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        
        // Auto-carregar todas as fichas se a opção estiver marcada
        if (autoCarregarFichas)
        {
            CarregarTodasAsFichas();
        }
        
        GerarInterfaceCompleta();
        AlternarMenu(false);
        FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Exercito);
    }
    
    // Carrega TODAS as fichas DadosConstrucao encontradas no projeto
    void CarregarTodasAsFichas()
    {
        catalogo.Clear();
        
        // Carrega todos os ScriptableObjects do tipo DadosConstrucao
        DadosConstrucao[] todasFichas = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
        
        foreach (var ficha in todasFichas)
        {
            // Ignora fichas sem prefab válido para evitar itens quebrados
            if (ficha != null && ficha.prefabDaUnidade != null)
            {
                catalogo.Add(ficha);
            }
        }
        
        // Ordena por categoria e depois por nome
        catalogo = catalogo.OrderBy(f => (int)f.categoria).ThenBy(f => f.nomeItem).ToList();
        
        catalogoGlobal = catalogo; // Expor para a IA

        Debug.Log($"[MenuConstrucao] Auto-carregadas {catalogo.Count} fichas de construção.");
    }
    
    // Botão para atualizar no Editor (útil para testar)
    [ContextMenu("Atualizar Catálogo Agora")]
    public void AtualizarCatalogoEditor()
    {
        CarregarTodasAsFichas();
        Debug.Log($"Catálogo atualizado! {catalogo.Count} fichas encontradas.");
    }

    void Update()
    {
        if (Input.GetKeyDown(teclaAtalho))
        {
            menuAberto = !menuAberto;
            AlternarMenu(menuAberto);
        }
    }

    void AlternarMenu(bool estado)
    {
        menuAberto = estado;
        if(painelPrincipal != null) painelPrincipal.SetActive(estado);
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
                // Se não achar canvas, cria um
                canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        // Verifica se já existe para não duplicar
        Transform painelExistente = canvasObj.transform.Find("Painel_Construcao");
        if (painelExistente != null)
        {
            painelPrincipal = painelExistente.gameObject;
            // Se já existe, não recria tudo, apenas atualiza referência
            // Obs: Se quiser forçar recriação, teria que dar Destroy(painelExistente.gameObject)
            return; 
        }

        painelPrincipal = CriarRetangulo("Painel_Construcao", canvasObj.transform);
        Image imgFundo = painelPrincipal.AddComponent<Image>();
        imgFundo.color = corFundo;
        
        RectTransform rtPanel = painelPrincipal.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.5f, 0.5f); rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
        rtPanel.sizeDelta = new Vector2(600, 350); 
        rtPanel.anchoredPosition = Vector2.zero;

        // Abas
        GameObject areaAbas = CriarRetangulo("Area_Abas", painelPrincipal.transform);
        RectTransform rtAbas = areaAbas.GetComponent<RectTransform>();
        rtAbas.anchorMin = new Vector2(0, 1); rtAbas.anchorMax = new Vector2(1, 1);
        rtAbas.pivot = new Vector2(0.5f, 1);
        rtAbas.sizeDelta = new Vector2(0, 40); 
        rtAbas.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup layoutAbas = areaAbas.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childForceExpandWidth = true; layoutAbas.childControlWidth = true;

        foreach (DadosConstrucao.CategoriaItem cat in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            CriarBotaoAba(cat, areaAbas.transform);
        }

        // Scroll
        GameObject scrollObj = CriarRetangulo("Scroll_Itens", painelPrincipal.transform);
        RectTransform rtScroll = scrollObj.GetComponent<RectTransform>();
        rtScroll.anchorMin = Vector2.zero; rtScroll.anchorMax = Vector2.one;
        rtScroll.offsetMin = new Vector2(10, 10);
        rtScroll.offsetMax = new Vector2(-10, -50); 

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        Image imgScroll = scrollObj.AddComponent<Image>(); imgScroll.color = new Color(0,0,0,0.2f);
        
        GameObject viewport = CriarRetangulo("Viewport", scrollObj.transform);
        viewport.AddComponent<RectMask2D>();
        RectTransform rtView = viewport.GetComponent<RectTransform>();
        rtView.anchorMin = Vector2.zero; rtView.anchorMax = Vector2.one;
        rtView.sizeDelta = Vector2.zero;
        
        GameObject content = CriarRetangulo("Content", viewport.transform);
        containerBotoes = content.transform;
        
        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);
        rtContent.sizeDelta = new Vector2(0, 300);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 120);
        grid.spacing = new Vector2(10, 10);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = rtView;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 20;
    }

    GameObject CriarRetangulo(string nome, Transform pai)
    {
        GameObject obj = new GameObject(nome, typeof(RectTransform));
        obj.transform.SetParent(pai, false);
        return obj;
    }

    void CriarBotaoAba(DadosConstrucao.CategoriaItem categoria, Transform pai)
    {
        GameObject btnObj = CriarRetangulo("Aba_" + categoria, pai);
        Image img = btnObj.AddComponent<Image>(); img.color = corAbas;
        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = CriarRetangulo("Texto", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = categoria.ToString();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        
        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero; rtTxt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() => FiltrarPorCategoria(categoria));
    }

    public void FiltrarPorCategoria(DadosConstrucao.CategoriaItem categoriaDesejada)
    {
        if (containerBotoes == null) return; // Segurança

        foreach(Transform child in containerBotoes) Destroy(child.gameObject);

        foreach(DadosConstrucao item in catalogo)
        {
            if(item != null && item.categoria == categoriaDesejada)
            {
                CriarBotaoItem(item);
            }
        }
    }

    void CriarBotaoItem(DadosConstrucao item)
    {
        GameObject btnObj = CriarRetangulo("Item_" + item.nomeItem, containerBotoes);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.35f);

        // --- ICONE ---
        if(item.icone != null)
        {
            GameObject iconObj = CriarRetangulo("Icone", btnObj.transform);
            Image imgIcon = iconObj.AddComponent<Image>();
            imgIcon.sprite = item.icone;
            imgIcon.preserveAspect = true;
            
            RectTransform rtIcon = iconObj.GetComponent<RectTransform>();
            rtIcon.anchorMin = new Vector2(0, 0.3f); rtIcon.anchorMax = Vector2.one; 
            rtIcon.offsetMin = new Vector2(5, 5); rtIcon.offsetMax = new Vector2(-5, -5);
        }
        else
        {
            // Tenta gerar um SNAPSHOT do prefab 3D em tempo real
            if (item.prefabDaUnidade != null)
            {
                Texture2D snapshot = GerarSnapshot(item.prefabDaUnidade);
                
                if (snapshot != null)
                {
                    GameObject iconObj = CriarRetangulo("Icone3D", btnObj.transform);
                    Image imgIcon = iconObj.AddComponent<Image>();
                    
                    // Converte Texture2D para Sprite
                    Sprite spriteGerado = Sprite.Create(snapshot, new Rect(0, 0, snapshot.width, snapshot.height), new Vector2(0.5f, 0.5f));
                    imgIcon.sprite = spriteGerado;
                    imgIcon.preserveAspect = true;

                    RectTransform rtIcon = iconObj.GetComponent<RectTransform>();
                    rtIcon.anchorMin = new Vector2(0, 0.3f); rtIcon.anchorMax = Vector2.one;
                    rtIcon.offsetMin = new Vector2(5, 5); rtIcon.offsetMax = new Vector2(-5, -5);
                }
                else
                {
                    CriarTextoPlaceholder(btnObj);
                }
            }
            else
            {
               CriarTextoPlaceholder(btnObj);
            }
        }

        // TEXTO
        GameObject txtObj = CriarRetangulo("Info", btnObj.transform);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = $"{item.nomeItem}\n<color=yellow>${item.preco}</color>";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 12;

        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = new Vector2(1, 0.3f); 
        rtTxt.offsetMin = Vector2.zero; rtTxt.offsetMax = Vector2.zero;

        Button btn = btnObj.AddComponent<Button>();
        
        // --- VALIDAÇÃO DE PRÉ-REQUISITOS ---
        // Exército, Marinha e Aeronáutica são unidades de compra direta
        bool ehUnidadeMilitar = (item.categoria == DadosConstrucao.CategoriaItem.Exercito ||
                                  item.categoria == DadosConstrucao.CategoriaItem.Marinha ||
                                  item.categoria == DadosConstrucao.CategoriaItem.Aeronautica);
        bool podeComprar = true;
        string motivoBloqueio = "";

        if(ehUnidadeMilitar && gerente != null && item.prefabDaUnidade != null)
        {
             string nomeLower = item.prefabDaUnidade.name.ToLower();
             bool ehSoldado = (nomeLower.Contains("soldado") || nomeLower.Contains("soldier") || nomeLower.Contains("person") || nomeLower.Contains("infantry") || nomeLower.Contains("variant"));

             // --- DESABILITADO TEMPORARIAMENTE POR SOLICITAÇÃO ---
             /*
             if(ehSoldado && gerente.spawnSoldado == null)
             {
                 podeComprar = false;
                 motivoBloqueio = "(Requer Tenda)";
             }
             else if(!ehSoldado && gerente.spawnInterno == null)
             {
                 podeComprar = false;
                 motivoBloqueio = "(Requer Hangar)";
             }
             */
        }
        
        // Bloqueia se o prefab não existe
        if(item.prefabDaUnidade == null)
        {
            podeComprar = false;
            motivoBloqueio = "(Prefab Ausente!)";
        }

        // --- VALIDAÇÃO HELIPORTO PARA HELICÓPTEROS ---
        if(item.categoria == DadosConstrucao.CategoriaItem.Aeronautica && item.prefabDaUnidade != null)
        {
            string nomePrefab = item.prefabDaUnidade.name.ToLower();
            bool ehHelicoptero = nomePrefab.Contains("heli") || nomePrefab.Contains("chopper") || nomePrefab.Contains("copter");
            
            if(ehHelicoptero)
            {
                bool temHeliporto = (GerenciadorHelicopteros.Instancia != null && GerenciadorHelicopteros.Instancia.ExisteHeliporto()) 
                                     || Object.FindFirstObjectByType<Heliporto>() != null;
                if(!temHeliporto)
                {
                    podeComprar = false;
                    motivoBloqueio = "(Requer Heliporto)";
                }
            }
        }

        if(podeComprar)
        {
             btn.onClick.AddListener(() => ClicouNoItem(item));
        }
        else
        {
             // Bloqueia Visualmente
             img.color = new Color(0.3f, 0.1f, 0.1f); // Vermelho escuro
             txt.text = $"<color=red>BLOQUEADO</color>\n<size=10>{motivoBloqueio}</size>";
             btn.interactable = false;
        }

    }

    void CriarTextoPlaceholder(GameObject parent)
    {
        GameObject iconObj = CriarRetangulo("IconePlaceholder", parent.transform);
        Text txtIcon = iconObj.AddComponent<Text>();
        txtIcon.text = "(Sem Imagem)";
        txtIcon.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtIcon.alignment = TextAnchor.MiddleCenter;
        txtIcon.color = new Color(1,1,1,0.3f);
            
        RectTransform rtIcon = iconObj.GetComponent<RectTransform>();
        rtIcon.anchorMin = new Vector2(0, 0.3f); rtIcon.anchorMax = Vector2.one;
        rtIcon.offsetMin = Vector2.zero; rtIcon.offsetMax = Vector2.zero;
    }

    // --- GERADOR DE ICONES 3D (RUNTIME) V2.0 - ALTA QUALIDADE ---
    Texture2D GerarSnapshot(GameObject prefab)
    {
        if(prefab == null) return null;

        // 1. Cria estúdio isolado
        GameObject studio = new GameObject("Studio_Snapshot");
        // Movemos o estúdio para longe, mas vamos instanciar o objeto em 0,0,0 primeiro para tentar evitar erro de NavMesh
        studio.transform.position = new Vector3(0, -5000, 0); 
        
        GameObject obj = null;
        try 
        {
            // Tenta instanciar desativado se possível, mas Unity n tem isso direto sem ser via editor.
            // Vamos instanciar em 0,0,0 e mover.
            bool estavaAtivo = prefab.activeSelf;
            // Se pudessemos desativar o prefab... nao podemos (é asset).
            
            // Instancia na origem (esperando que 0,0,0 tenha NavMesh ou seja seguro)
            obj = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, 45, 0));
            obj.transform.SetParent(studio.transform, false); // Move para o estudio (-5000)
            
            // Mas se o NavMeshAgent reclamar no Instantiate, ja era.
            // A solução robusta seria ter NavMesh em -5000 ou desativar o Agent no Prefab.
            // Porem como workaround de código:
        }
        catch
        {
            if(studio != null) Destroy(studio);
            return null;
        }

        if(obj == null) { if(studio!=null) Destroy(studio); return null; }

        // Desativa scripts e componentes indesejados
        foreach(var c in obj.GetComponentsInChildren<MonoBehaviour>()) c.enabled = false;
        foreach(var nav in obj.GetComponentsInChildren<NavMeshAgent>()) nav.enabled = false;
        foreach(var p in obj.GetComponentsInChildren<ParticleSystem>()) p.gameObject.SetActive(false);
        // Desativa UI flutuante (HealthBars etc) para não poluir o ícone e evitar erros de fonte
        foreach(var kv in obj.GetComponentsInChildren<Canvas>()) kv.enabled = false;
        // Se usar TMP
        var tmps = obj.GetComponentsInChildren<TMPro.TMP_Text>();
        foreach(var t in tmps) t.enabled = false;

        // 3. Calcula Bounds precisos
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        bool primeiro = true;
        
        foreach (Renderer r in renderers) 
        {
            if(!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if(r.bounds.size.magnitude > 1000f) continue; 
            
            if(primeiro) { bounds = r.bounds; primeiro = false; }
            else bounds.Encapsulate(r.bounds);
        }
        
        if(renderers.Length == 0 || bounds.size.magnitude < 0.1f) { Destroy(studio); return null; }

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if(maxDim < 1f) maxDim = 1f;

        // 4. Configura Câmera
        GameObject camObj = new GameObject("Cam_Snapshot");
        camObj.transform.SetParent(studio.transform);
        Camera cam = camObj.AddComponent<Camera>();
        
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = new Color(0,0,0,0); 
        cam.cullingMask = -1; // Vê tudo no layer (se isolasse layers seria melhor, mas hacky studio funciona)
        
        cam.orthographic = true;
        cam.orthographicSize = maxDim * 0.7f; 
        
        float dist = maxDim * 5f;
        Vector3 dir = Quaternion.Euler(30, 0, 0) * Vector3.back; 
        cam.transform.position = bounds.center - (dir * dist);
        cam.transform.LookAt(bounds.center);

        // 5. Iluminação
        CriarLuz(studio.transform, bounds.center, Quaternion.Euler(50, -30, 0), 1.2f, new Color(1f, 0.95f, 0.9f));
        CriarLuz(studio.transform, bounds.center, Quaternion.Euler(30, 150, 0), 0.6f, new Color(0.8f, 0.85f, 1f));
        CriarLuz(studio.transform, bounds.center, Quaternion.Euler(-10, 0, 0), 0.8f, Color.white);

        // 6. Renderiza
        int res = 512; 
        RenderTexture rt = RenderTexture.GetTemporary(res, res, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;
        
        cam.targetTexture = rt;
        cam.Render();

        // 7. Gera Textura
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();

        // 8. Limpeza
        cam.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(studio);

        return tex;
    }

    void CriarLuz(Transform parent, Vector3 target, Quaternion rotation, float intensity, Color color)
    {
        GameObject lObj = new GameObject("Light_Snap");
        lObj.transform.SetParent(parent);
        Light l = lObj.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.color = color;
        l.transform.rotation = rotation;
    }
    void ClicouNoItem(DadosConstrucao item)
    {
        // 1. Separa o que é Construção (Muro, Prédio) do que é Tropa (Navio, Tanque)
        // Se NÃO for exército, nem marinha, nem aeronáutica, então é construção.
        bool ehConstrucao = (item.categoria != DadosConstrucao.CategoriaItem.Exercito &&
                             item.categoria != DadosConstrucao.CategoriaItem.Marinha &&
                             item.categoria != DadosConstrucao.CategoriaItem.Aeronautica);

        if (ehConstrucao)
        {
            // --- LÓGICA DE CONSTRUÇÃO (Muros, Prédios) ---
            var construtor = Object.FindFirstObjectByType<Construtor>();
            if (construtor != null) 
            {
                AlternarMenu(false); // Fecha o menu
                construtor.SelecionarParaConstruir(item.prefabDaUnidade);
            }
            else
            {
                Debug.LogError("ERRO: Não achei o script CONSTRUTOR na cena!");
            }
        }
        else
        {
            // --- LÓGICA DE COMPRA DE UNIDADES (Marinha / Exército) ---

            // AQUI ESTÁ A CORREÇÃO: Se for MARINHA, chama o Estaleiro!
            if (item.categoria == DadosConstrucao.CategoriaItem.Marinha)
            {
                // Procura o script do Estaleiro na cena
                var estaleiro = Object.FindFirstObjectByType<Estaleiro>(); 
                
                if (estaleiro != null)
                {
                    // Manda o Estaleiro criar o navio no lugar certo
                    estaleiro.ConstruirUnidade(item.prefabDaUnidade);
                    Debug.Log("Ordem enviada ao Estaleiro: Construir " + item.nomeItem);
                    AlternarMenu(false); // Fecha o menu após compra
                }
                else
                {
                    Debug.LogError("ERRO: Não achei o Hangar Naval com o script 'Estaleiro' na cena! Construa um Estaleiro primeiro.");
                }
            }
            // Exército usa o GerenteDeJogo (Hangar/Tenda)
            else if (item.categoria == DadosConstrucao.CategoriaItem.Exercito)
            {
                if(gerente != null) 
                {
                    gerente.ComprarUnidade(item.prefabDaUnidade, item.preco);
                    AlternarMenu(false); // Fecha o menu após compra
                }
                else
                {
                    Debug.LogError("ERRO: GerenteDeJogo não encontrado!");
                }
            }
            // Aeronáutica - Requer Heliporto para helicópteros
            else if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
            {
                // Verifica se é um helicóptero (nome contém "heli", "chopper", "copter")
                string nomePrefab = item.prefabDaUnidade.name.ToLower();
                bool ehHelicoptero = nomePrefab.Contains("heli") || nomePrefab.Contains("chopper") || nomePrefab.Contains("copter");

                // Se for helicóptero, verifica se existe heliporto
                if (ehHelicoptero)
                {
                    bool temHeliporto = (GerenciadorHelicopteros.Instancia != null && GerenciadorHelicopteros.Instancia.ExisteHeliporto()) 
                                         || FindObjectOfType<Heliporto>() != null;
                    
                    if (!temHeliporto)
                    {
                        Debug.LogWarning("AVISO: Construa um Heliporto antes de comprar helicópteros!");
                        return;
                    }
                }

                if(gerente != null) 
                {
                    gerente.ComprarUnidade(item.prefabDaUnidade, item.preco);
                    AlternarMenu(false);
                }
            }
        }
    }
}
