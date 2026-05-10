using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// MiniMapa Circular — Estilo Scope/Radar no canto inferior direito.
/// Cria automaticamente a câmera, RenderTexture e UI.
/// Basta adicionar este componente em qualquer GameObject na cena.
/// </summary>
public class MiniMapa : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Transform do jogador (câmera do mapa segue este objeto)")]
    public Transform alvoJogador;

    [Tooltip("Altura da câmera do mapa acima do jogador")]
    public float alturaCamera = 120f;

    [Tooltip("Tamanho ortográfico (zoom do mapa — maior = mais distante)")]
    public float tamanhoOrtografico = 150f;

    [Tooltip("Tamanho em pixels do mini-mapa na tela")]
    public int tamanhoUI = 220;

    [Tooltip("Margem do canto da tela")]
    public int margemBorda = 20;

    [Header("Visual")]
    public Color corBorda = new Color(0.18f, 0.15f, 0.12f, 1f);
    public Color corFundoRadar = new Color(0.85f, 0.78f, 0.60f, 0.10f);
    public float espessuraBorda = 8f;

    [Header("Indicador do Jogador")]
    public Color corTrianguloJogador = Color.red;
    public float tamanhoTriangulo = 14f;

    [Header("Ícones no Mapa")]
    public bool mostrarInimigos = true;
    public bool mostrarAliados = true;
    public float raioDeteccao = 400f;   // Raio de detecção de unidades no mapa

    // --- Internos ---
    private Camera _camMapa;
    private RenderTexture _rt;
    private Canvas _canvas;
    private RawImage _imagemMapa;
    private RectTransform _containerCirculo;
    private GameObject _containerObj;
    private GameObject _trianguloJogador;
    private GameObject _camObj;
    private GameObject _canvasObj;
    private bool _inicializado;
    private bool _camObjCriadaPorEsteComponente;
    private bool _canvasObjCriadaPorEsteComponente;

    // Ícones de unidades
    private List<MapaIcone> _icones = new List<MapaIcone>();

    private struct MapaIcone
    {
        public Transform alvo;
        public RectTransform rect;
        public Image img;
        public bool ehInimigo;
    }

    // =========================================================
    void Start()
    {
        InicializarSeNecessario();
    }

    void OnEnable()
    {
        InicializarSeNecessario();
        DefinirAtivoRuntime(true);
    }

    void OnDisable()
    {
        DefinirAtivoRuntime(false);
    }

    void LateUpdate()
    {
        if (!_inicializado)
        {
            InicializarSeNecessario();
        }

        if (alvoJogador == null)
        {
            TentarResolverAlvoJogador();
        }

        if (_camMapa == null || alvoJogador == null) return;

        // Segue o jogador de cima
        _camMapa.transform.position = new Vector3(
            alvoJogador.position.x,
            alvoJogador.position.y + alturaCamera,
            alvoJogador.position.z
        );

        // Rotaciona o mapa para que "frente" fique sempre no topo
        _camMapa.transform.rotation = Quaternion.Euler(90f, alvoJogador.eulerAngles.y, 0f);

        // Atualiza triângulo do jogador (centralizado, apontando para cima)
        if (_trianguloJogador != null)
        {
            _trianguloJogador.transform.rotation = Quaternion.identity; // Sempre apontando para cima
        }

        AtualizarIcones();
    }

    void InicializarSeNecessario()
    {
        if (_inicializado) return;

        TentarResolverAlvoJogador();
        CriarCameraMapa();
        CriarUI();

        _inicializado = true;
        DefinirAtivoRuntime(isActiveAndEnabled);
    }

    void TentarResolverAlvoJogador()
    {
        if (alvoJogador != null) return;

        // Preferência: MainCamera
        Camera cam = Camera.main;
        if (cam != null)
        {
            alvoJogador = cam.transform;
            return;
        }

        // Fallback: primeira câmera ativa que não seja a do minimapa
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            if (_camMapa != null && c == _camMapa) continue;
            alvoJogador = c.transform;
            return;
        }
    }

    void DefinirAtivoRuntime(bool ativo)
    {
        if (_camObj != null) _camObj.SetActive(ativo);
        if (_canvasObj != null) _canvasObj.SetActive(ativo);
    }

    // =========================================================
    // CRIAÇÃO DA CÂMERA DO MAPA
    // =========================================================
    void CriarCameraMapa()
    {
        if (_camMapa != null) return;

        _camObj = GameObject.Find("Cam_MiniMapa");
        if (_camObj != null)
        {
            _camMapa = _camObj.GetComponent<Camera>();
        }

        if (_camMapa == null)
        {
            _camObj = new GameObject("Cam_MiniMapa");
            _camMapa = _camObj.AddComponent<Camera>();
            _camObjCriadaPorEsteComponente = true;
        }

        // Importante: não parentear a câmera sob UI/RectTransforms (pode ter escala 0 e travar a movimentação).
        _camObj.transform.SetParent(null);
        _camObj.transform.localScale = Vector3.one;

        _rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        _rt.name = "RT_MiniMapa";
        _rt.Create();

        _camMapa.orthographic = true;
        _camMapa.orthographicSize = tamanhoOrtografico;
        _camMapa.targetTexture = _rt;
        _camMapa.clearFlags = CameraClearFlags.SolidColor;
        _camMapa.backgroundColor = new Color(0.85f, 0.78f, 0.58f, 1f); // Areia
        _camMapa.cullingMask = ~0; // Renderiza tudo
        _camMapa.depth = -10;
        _camMapa.farClipPlane = 3000f;

        // Exclui camada de UI para não renderizar na câmera do mapa
        int camadaUI = LayerMask.NameToLayer("UI");
        if (camadaUI >= 0)
        {
            _camMapa.cullingMask &= ~(1 << camadaUI);
        }
    }

    // =========================================================
    // CRIAÇÃO DA UI
    // =========================================================
    void CriarUI()
    {
        if (_canvas != null) return;

        _canvasObj = GameObject.Find("Canvas_MiniMapa");
        if (_canvasObj != null)
        {
            _canvas = _canvasObj.GetComponent<Canvas>();
        }

        if (_canvas == null)
        {
            // Canvas dedicado ao mini-mapa
            _canvasObj = new GameObject("Canvas_MiniMapa");
            _canvas = _canvasObj.AddComponent<Canvas>();
            _canvasObjCriadaPorEsteComponente = true;
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            _canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            // Garante as configs esperadas
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            if (_canvasObj.GetComponent<CanvasScaler>() == null)
            {
                _canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            }
            if (_canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                _canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        // Mantém Canvas Overlay no topo da hierarquia (recomendação do Unity) e imune a escala/rotação de pais.
        _canvasObj.transform.SetParent(null);
        _canvasObj.transform.localScale = Vector3.one;

        // --- Container principal (ancora canto inferior direito) ---
        _containerObj = new GameObject("MiniMapa_Container");
        _containerObj.transform.SetParent(_canvasObj.transform, false);
        _containerCirculo = _containerObj.AddComponent<RectTransform>();

        _containerCirculo.anchorMin = new Vector2(1f, 0f);
        _containerCirculo.anchorMax = new Vector2(1f, 0f);
        _containerCirculo.pivot = new Vector2(1f, 0f);
        _containerCirculo.anchoredPosition = new Vector2(-margemBorda, margemBorda);
        _containerCirculo.sizeDelta = new Vector2(tamanhoUI, tamanhoUI);

        // --- Borda circular (usa imagem com máscara circular) ---
        GameObject bordaObj = CriarCirculo("Borda", _containerObj.transform, corBorda,
            new Vector2(tamanhoUI, tamanhoUI));

        // --- Imagem do mapa dentro da borda ---
        int tamanhoInterno = tamanhoUI - (int)(espessuraBorda * 2);
        GameObject mapaObj = new GameObject("MapaImagem");
        mapaObj.transform.SetParent(bordaObj.transform, false);

        RectTransform mapaRect = mapaObj.AddComponent<RectTransform>();
        mapaRect.anchorMin = Vector2.zero;
        mapaRect.anchorMax = Vector2.one;
        mapaRect.offsetMin = new Vector2(espessuraBorda, espessuraBorda);
        mapaRect.offsetMax = new Vector2(-espessuraBorda, -espessuraBorda);

        // Máscara circular para o mapa
        Mask mascara = mapaObj.AddComponent<Mask>();
        mascara.showMaskGraphic = false;
        Image imgMascara = mapaObj.AddComponent<Image>();
        imgMascara.sprite = CriarSpriteCirculo(256);
        imgMascara.color = Color.white;

        // Imagem da RenderTexture
        GameObject imgObj = new GameObject("RT_Image");
        imgObj.transform.SetParent(mapaObj.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        _imagemMapa = imgObj.AddComponent<RawImage>();
        _imagemMapa.texture = _rt;
        _imagemMapa.color = Color.white;

        // --- Triângulo do Jogador (centro do mapa) ---
        _trianguloJogador = CriarTriangulo("Jogador_Triangulo", mapaObj.transform,
            corTrianguloJogador, tamanhoTriangulo);


        // --- Overlay de nitidez (anel escuro nas bordas) ---
        CriarVinheta(bordaObj.transform);
    }

    // =========================================================
    // TRIÂNGULO DO JOGADOR (GL Draw)
    // =========================================================
    GameObject CriarTriangulo(string nome, Transform pai, Color cor, float tamanho)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(tamanho * 2f, tamanho * 2f);

        TrianguloUI tri = obj.AddComponent<TrianguloUI>();
        tri.corTriangulo = cor;
        tri.corBorda = Color.black;

        return obj;
    }


    void CriarVinheta(Transform pai)
    {
        GameObject obj = new GameObject("Vinheta");
        obj.transform.SetParent(pai, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = obj.AddComponent<Image>();
        img.sprite = CriarSpriteCirculo(256);
        img.type = Image.Type.Simple;
        img.color = new Color(0, 0, 0, 0f); // Transparente — só para referência
        img.raycastTarget = false;
    }

    // =========================================================
    // ÍCONES DE UNIDADES NO MAPA
    // =========================================================
    void AtualizarIcones()
    {
        // Remove ícones de unidades que morreram
        _icones.RemoveAll(ic => ic.alvo == null || !ic.alvo.gameObject.activeInHierarchy);
        foreach (var ic in _icones)
        {
            if (ic.alvo == null) continue;
            Vector3 posRelativa = _camMapa.WorldToViewportPoint(ic.alvo.position);

            // Converte viewport para coordenadas locais do mini-mapa
            float x = (posRelativa.x - 0.5f) * tamanhoUI;
            float y = (posRelativa.y - 0.5f) * tamanhoUI;
            ic.rect.anchoredPosition = new Vector2(x, y);
            ic.rect.gameObject.SetActive(posRelativa.z > 0); // Esconde se atrás
        }
    }

    public void RegistrarUnidadeNoMapa(Transform unidade, bool ehInimigo)
    {
        foreach (var ic in _icones)
            if (ic.alvo == unidade) return; // Já registrado

        if (_containerCirculo == null) return;

        GameObject iconObj = new GameObject($"Icone_{unidade.name}");
        iconObj.transform.SetParent(_imagemMapa.transform, false);

        RectTransform rt = iconObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(6f, 6f);

        Image img = iconObj.AddComponent<Image>();
        img.color = ehInimigo ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.3f);
        img.sprite = CriarSpriteCirculo(16);

        _icones.Add(new MapaIcone { alvo = unidade, rect = rt, img = img, ehInimigo = ehInimigo });
    }

    // =========================================================
    // UTILITÁRIOS DE UI
    // =========================================================
    GameObject CriarCirculo(string nome, Transform pai, Color cor, Vector2 tamanho)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tamanho;

        Image img = obj.AddComponent<Image>();
        img.sprite = CriarSpriteCirculo(256);
        img.color = cor;
        img.type = Image.Type.Simple;

        return obj;
    }

    Sprite CriarSpriteCirculo(int resolucao)
    {
        Texture2D tex = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 centro = new Vector2(resolucao / 2f, resolucao / 2f);
        float raio = resolucao / 2f - 1f;

        for (int y = 0; y < resolucao; y++)
        {
            for (int x = 0; x < resolucao; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centro);
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(raio - 1.5f, raio + 0.5f, dist));
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, resolucao, resolucao),
                             new Vector2(0.5f, 0.5f), resolucao / 2f);
    }

    void OnDestroy()
    {
        if (_camMapa != null && _camMapa.targetTexture == _rt)
        {
            _camMapa.targetTexture = null;
        }

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        if (_canvasObjCriadaPorEsteComponente)
        {
            DestruirObjetoSeguro(_canvasObj);
        }
        else
        {
            DestruirObjetoSeguro(_containerObj);
        }

        if (_camObjCriadaPorEsteComponente)
        {
            DestruirObjetoSeguro(_camObj);
        }
    }

    static void DestruirObjetoSeguro(GameObject obj)
    {
        if (obj == null) return;

#if UNITY_EDITOR
        UnityEditor.Selection.activeObject = null;
#endif

        Destroy(obj);
    }
}
