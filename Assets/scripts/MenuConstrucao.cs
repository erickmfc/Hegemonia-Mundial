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

    // Variáveis Internas
    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private GerenteDeJogo gerente;
    private bool menuAberto = true;

    void Start()
    {
        gerente = FindObjectOfType<GerenteDeJogo>();
        
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
            Canvas canvasExistente = FindObjectOfType<Canvas>();
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

    // --- GERADOR DE ICONES 3D (RUNTIME) MELHORADO ---
    Texture2D GerarSnapshot(GameObject prefab)
    {
        if(prefab == null) return null;

        // 1. Cria um estúdio invisível lá longe
        GameObject studio = new GameObject("Studio_Snapshot");
        studio.transform.position = new Vector3(0, -5000, 0); // Bem longe
        int layerSnapshot = LayerMask.NameToLayer("UI"); // Tenta usar UI ou Default
        if(layerSnapshot < 0) layerSnapshot = 0;

        // 2. Instancia o objeto
        GameObject obj = Instantiate(prefab, studio.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.Euler(0, 150, 0); // Rotaciona para ficar de frente (ajuste conforme seu modelo)

        // Previne scripts chatos de rodar
        foreach(var c in obj.GetComponentsInChildren<MonoBehaviour>()) c.enabled = false;
        foreach(var nav in obj.GetComponentsInChildren<NavMeshAgent>()) nav.enabled = false;
        
        // Define Layers para a câmera ver apenas isso se quisesse, mas aqui vamos na força bruta
        
        // 3. Calcula o tamanho do objeto para posicionar a câmera
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        // Filtra renderers (Ignora partículas, trilhas, etc)
        var validRenderers = new List<Renderer>();
        foreach(var r in renderers)
        {
            if(r is ParticleSystemRenderer || r is TrailRenderer) continue;

            // FILTRO DE SEGURANÇA: Ignora "sujeira" que possa vir junto no prefab (Terreno, Mapa, etc)
            string rName = r.gameObject.name.ToLower();
            if(rName.Contains("terrain") || rName.Contains("mapa") || rName.Contains("chao") || rName.Contains("ground") || rName.Contains("environment")) 
                continue;

            validRenderers.Add(r);
            
            // Força atualização de SkinnedMesh
            if(r is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
                smr.forceMatrixRecalculationPerRender = true; 
            }
        }
        
        if(validRenderers.Count == 0) { Destroy(studio); return null; }

        bool primeiro = true;
        foreach (Renderer r in validRenderers) 
        {
            if(primeiro) { bounds = r.bounds; primeiro = false; }
            else bounds.Encapsulate(r.bounds);
        }
        
        // --- CORREÇÃO DE TAMANHO (Para soldados não ficarem minúsculos ou estourados) ---
        // Se o objeto for muito pequeno (tipo um soldado), fingimos que ele é maior para a câmera afastar
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if(maxDim < 2.0f) maxDim = 2.0f; // Tamanho mínimo de referência (2 metros)

        float cameraDist = maxDim * 2.0f; 

        // 4. Configura Câmera e Luz
        GameObject camObj = new GameObject("Cam_Snapshot");
        camObj.transform.SetParent(studio.transform);
        
        Camera cam = camObj.AddComponent<Camera>();
        cam.backgroundColor = new Color(0.25f, 0.3f, 0.35f, 1f); 
        cam.clearFlags = CameraClearFlags.Color;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.orthographic = true; 
        cam.orthographicSize = maxDim * 0.6f; // Ajuste de Zoom (quanto menor, mais perto)

        // Posiciona a câmera em ângulo isométrico "RTS"
        // 45 graus de cima, 45 do lado
        Vector3 direcao = new Vector3(1, 1, -1).normalized;
        cam.transform.position = bounds.center + direcao * cameraDist;
        cam.transform.LookAt(bounds.center); 
        
        // Ajuste fino para centralizar verticalmente (Humanoides costumam ter pivot no pé)
        if(maxDim < 3.0f) cam.transform.position += Vector3.up * (maxDim * 0.2f);

        // Luz Principal
        GameObject luz = new GameObject("Luz_Key");
        luz.transform.SetParent(studio.transform);
        Light l = luz.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.2f;
        l.transform.rotation = Quaternion.LookRotation(bounds.center - (bounds.center + new Vector3(1, 2, -1))); // Luz vindo de cima/direita
        
        // Luz de Preenchimento (para não ficar sombras pretas)
        GameObject luzFill = new GameObject("Luz_Fill");
        luzFill.transform.SetParent(studio.transform);
        Light lf = luzFill.AddComponent<Light>();
        lf.type = LightType.Directional;
        lf.color = new Color(0.5f, 0.5f, 0.6f);
        lf.intensity = 0.5f;
        lf.transform.rotation = Quaternion.LookRotation(bounds.center - (bounds.center + new Vector3(-1, 0, -1)));

        // 5. Renderiza
        int res = 256; // Resolução do ícone
        RenderTexture rt = RenderTexture.GetTemporary(res, res, 24);
        cam.targetTexture = rt;
        cam.Render();

        // 6. Passa para Texture2D
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();

        // 7. Limpa a bagunça
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(studio); // Tchau estúdio

        return tex;
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
            var construtor = FindObjectOfType<Construtor>();
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
                var estaleiro = FindObjectOfType<Estaleiro>(); 
                
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
            // Aeronáutica (futuro - por enquanto usa GerenteDeJogo)
            else if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
            {
                if(gerente != null) 
                {
                    gerente.ComprarUnidade(item.prefabDaUnidade, item.preco);
                    AlternarMenu(false);
                }
            }
        }
    }
}
