using UnityEngine;
using System.Collections.Generic;
using Hegemonia.RTS;

/// <summary>
/// MAPA GERAL TÁTICO - Pressione M para abrir/fechar.
/// - Camera ortográfica de cima com fundo pintado de azul oceano.
/// - Mostra ícones de PRÉDIOS aliados e UNIDADES aliadas.
/// - NUNCA revela unidades inimigas (Fog of War).
/// - Zoom com scroll do mouse. Pan com WASD/setas ou bordas da tela.
/// </summary>
public class MapaGeralController : MonoBehaviour
{
    public static MapaGeralController Instancia { get; private set; }
    public static bool EstaAberto { get { return Instancia != null && Instancia.mapaAtivo; } }

    private Camera cameraPrincipal;
    private Camera cameraMapa;
    private bool mapaAtivo = false;
    private Vector3 cameraPrincipalPosicaoAntesDoMapa;
    private Quaternion cameraPrincipalRotacaoAntesDoMapa;
    private float cameraPrincipalFovAntesDoMapa;
    private bool snapshotCameraPrincipalValido;
    private bool fogOriginal;
    private Color fogColorOriginal;
    private float fogStartOriginal;
    private float fogEndOriginal;
    private FogMode fogModeOriginal;
    private float volumeAudioOriginal = 1f;

    [Header("Configurações do Mapa")]
    public float velocidadeMover  = 120f;
    public float zoomVelocidade   = 1800f;
    public float zoomMinimo = 30f;
    public float zoomMaximo = 500f;

    [Header("Limites automáticos do mapa")]
    [SerializeField] private bool detectarLimitesReaisDoMapa = true;
    [SerializeField] private float margemMapa = 250f;

    [Header("Configurações de Exibição")]
    public int meuTeamID = 1; // ID do jogador (unidades aliadas a mostrar)
    public float nivelDoMar = 0f; // Heights abaixo disso = oceano azul

    // Cores do mapa
    private readonly Color corFundoMar      = new Color(0.10f, 0.25f, 0.55f, 1f);   // Azul oceano profundo
    private readonly Color corBordaMar      = new Color(0.18f, 0.40f, 0.70f, 1f);   // Borda da água
    private readonly Color corPredioProprio = new Color(0.90f, 0.90f, 0.90f, 1f);   // Branco
    private readonly Color corUnidadePropria= new Color(0.25f, 1.00f, 0.25f, 1f);   // Verde claro
    private readonly Color corUnidadeNeutro = new Color(0.70f, 0.70f, 0.70f, 1f);   // Cinza

    // Cache de objetos do mundo para não chamar Find() o tempo todo
    private List<IdentidadeUnidade> _cacheUnidades = new List<IdentidadeUnidade>();
    private readonly List<MissileThreatTracker> _misseisAtivos = new List<MissileThreatTracker>(64);
    private readonly List<Projetil> _projeteisAtivos = new List<Projetil>(256);
    private readonly Vector3[] _cantosTerritorioInimigo = new Vector3[4];
    private float _tempoRefreshCache = 0f;

    // Estilos IMGUI sao reutilizados enquanto o mapa esta aberto para nao alocar por repaint.
    private GUIStyle _tituloMapaStyle;
    private GUIStyle _zoomMapaStyle;
    private GUIStyle _legendaMapaStyle;
    private GUIStyle _trianguloSombraStyle;
    private GUIStyle _trianguloCorStyle;

    // --- Modo de seguir unidade selecionada ---
    private bool _seguindoAlvo = false;
    private Transform _alvoSeguir = null;

    private Vector2 centroMapa = Vector2.zero;
    private float metadeMapa = 5000f;
    private bool limitesMapaInicializados;
    private Terrain terrenoInimigo;
    private Bounds limitesTerrenoInimigo;
    private bool territorioInimigoDisponivel;

    // --- Shader warmup (evita compilação durante o voo) ---

    private void Awake()
    {
        Instancia = this;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    void Start()
    {
        OcultarTerrenoInimigoAuxiliar();
        cameraPrincipal = Camera.main;
        AtualizarLimitesMapa();
        AtualizarLimitesTerritorioInimigo();

        GameObject camObj = new GameObject("Camera_MapaGeral");
        cameraMapa = camObj.AddComponent<Camera>();

        cameraMapa.orthographic     = true;
        cameraMapa.orthographicSize = 260f;
        cameraMapa.clearFlags       = CameraClearFlags.SolidColor;
        cameraMapa.backgroundColor  = corFundoMar; // Fundo azul oceano!
        cameraMapa.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cameraMapa.cullingMask      = ~0; // Vê tudo inicialmente
        cameraMapa.depth            = 100;
        cameraMapa.nearClipPlane    = 0.3f;
        cameraMapa.farClipPlane     = Mathf.Max(6000f, metadeMapa * 4f);
        cameraMapa.gameObject.SetActive(false);

        fogOriginal = RenderSettings.fog;
        fogColorOriginal = RenderSettings.fogColor;
        fogStartOriginal = RenderSettings.fogStartDistance;
        fogEndOriginal = RenderSettings.fogEndDistance;
        fogModeOriginal = RenderSettings.fogMode;

        // Evita Shader.WarmupAllShaders: no URP ele pode combinar keyword spaces
        // incompatíveis entre shaders e gerar asserts durante a entrada no Play Mode.
    }

    private void OcultarTerrenoInimigoAuxiliar()
    {
        Terrain[] terrenos = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null) continue;

            string nome = terreno.name.ToLowerInvariant();
            if (!nome.Contains("mapa inimigo") && !nome.Contains("mapa_inimigo")) continue;

            terrenoInimigo = terreno;
            // Desliga somente o renderer do Terrain. O TerrainCollider e o
            // NavMeshSurface continuam disponíveis para a lógica do jogo.
            terreno.enabled = false;
        }
    }

    private void AtualizarLimitesTerritorioInimigo()
    {
        territorioInimigoDisponivel = false;
        if (terrenoInimigo == null || terrenoInimigo.terrainData == null) return;

        TerrainData dados = terrenoInimigo.terrainData;
        Vector3 escala = terrenoInimigo.transform.lossyScale;
        float escalaX = Mathf.Abs(escala.x) > 0.001f ? Mathf.Abs(escala.x) : 1f;
        float escalaZ = Mathf.Abs(escala.z) > 0.001f ? Mathf.Abs(escala.z) : 1f;
        Vector3 tamanho = new Vector3(dados.size.x * escalaX, Mathf.Max(1f, dados.size.y), dados.size.z * escalaZ);
        Vector3 centro = terrenoInimigo.GetPosition() + new Vector3(tamanho.x * 0.5f, tamanho.y * 0.5f, tamanho.z * 0.5f);
        limitesTerrenoInimigo = new Bounds(centro, tamanho);
        territorioInimigoDisponivel = tamanho.x > 1f && tamanho.z > 1f;
    }

    /// <summary>
    /// Descobre a extensão real dos Terrains ativos. O valor original continua
    /// sendo o mínimo/fallback para preservar cenas antigas sem Terrain.
    /// </summary>
    private void AtualizarLimitesMapa()
    {
        float metadeConfigurada = Mathf.Max(1f, metadeMapa);
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        bool encontrouTerrain = false;

        Terrain[] terrenos = detectarLimitesReaisDoMapa
            ? FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : System.Array.Empty<Terrain>();

        if (detectarLimitesReaisDoMapa)
        {
            for (int i = 0; i < terrenos.Length; i++)
            {
                Terrain terreno = terrenos[i];
                TerrainData dados = terreno != null ? terreno.terrainData : null;
                if (terreno == null || dados == null || !terreno.gameObject.scene.IsValid())
                {
                    continue;
                }

                // Terrains desativados ou com escala zero são restos de mapas
                // antigos/tiles de apoio e não devem ampliar o limite jogável.
                Vector3 escala = terreno.transform.lossyScale;
                if (!terreno.gameObject.activeInHierarchy || Mathf.Abs(escala.x) < 0.001f || Mathf.Abs(escala.z) < 0.001f)
                {
                    continue;
                }

                Vector3 origem = terreno.GetPosition();
                Vector3 tamanho = dados.size;
                if (tamanho.x <= 0f || tamanho.z <= 0f)
                {
                    continue;
                }

                minX = Mathf.Min(minX, origem.x);
                maxX = Mathf.Max(maxX, origem.x + tamanho.x);
                minZ = Mathf.Min(minZ, origem.z);
                maxZ = Mathf.Max(maxZ, origem.z + tamanho.z);
                encontrouTerrain = true;
            }

            // Algumas partes jogáveis ficam fora do Terrain principal. A cena
            // atual, por exemplo, mantém a cidade/layout da IA01 ao sul do
            // terreno. Inclui apenas layouts-raiz conhecidos e seus filhos
            // ativos, sem usar objetos desativados ou tiles de apoio.
            Transform[] todosTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < todosTransforms.Length; i++)
            {
                Transform layout = todosTransforms[i];
                if (layout == null || !layout.gameObject.activeInHierarchy) continue;

                string nomeLayout = layout.name.ToLowerInvariant();
                bool layoutDeMapa = nomeLayout.Contains("ia01citylayout")
                    || nomeLayout.Contains("cartelmanualcreates")
                    || nomeLayout == "terrenos";
                if (!layoutDeMapa) continue;

                Transform[] pontosLayout = layout.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < pontosLayout.Length; j++)
                {
                    Transform ponto = pontosLayout[j];
                    if (ponto == null || !ponto.gameObject.activeInHierarchy) continue;
                    Vector3 escala = ponto.lossyScale;
                    if (Mathf.Abs(escala.x) < 0.001f || Mathf.Abs(escala.z) < 0.001f) continue;

                    Vector3 posicao = ponto.position;
                    minX = Mathf.Min(minX, posicao.x);
                    maxX = Mathf.Max(maxX, posicao.x);
                    minZ = Mathf.Min(minZ, posicao.z);
                    maxZ = Mathf.Max(maxZ, posicao.z);
                    encontrouTerrain = true;
                }
            }
        }

        if (!encontrouTerrain)
        {
            centroMapa = Vector2.zero;
            metadeMapa = metadeConfigurada;
            limitesMapaInicializados = true;
            Debug.LogWarning($"[MapaGeral] Nenhum Terrain ativo encontrado; usando limite configurado de {metadeMapa:F0}.");
            return;
        }

        float margem = Mathf.Max(0f, margemMapa);
        minX -= margem;
        maxX += margem;
        minZ -= margem;
        maxZ += margem;

        centroMapa = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        float metadeTerrain = Mathf.Max((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
        metadeMapa = Mathf.Max(metadeConfigurada, metadeTerrain);
        zoomMaximo = Mathf.Max(zoomMaximo, metadeMapa);
        limitesMapaInicializados = true;

        Debug.Log($"[MapaGeral] Limites do mapa: centro=({centroMapa.x:F0}, {centroMapa.y:F0}) metade={metadeMapa:F0} zoomMaximo={zoomMaximo:F0}.");
    }

    private void LimitarCameraMapa()
    {
        if (cameraMapa == null) return;
        if (!limitesMapaInicializados) AtualizarLimitesMapa();

        float aspecto = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1f, Screen.height));
        float meiaAltura = cameraMapa.orthographicSize;
        float meiaLargura = meiaAltura * aspecto;
        float limiteX = Mathf.Max(0f, metadeMapa - meiaLargura);
        float limiteZ = Mathf.Max(0f, metadeMapa - meiaAltura);

        Vector3 pos = cameraMapa.transform.position;
        pos.x = Mathf.Clamp(pos.x, centroMapa.x - limiteX, centroMapa.x + limiteX);
        pos.z = Mathf.Clamp(pos.z, centroMapa.y - limiteZ, centroMapa.y + limiteZ);
        cameraMapa.transform.position = pos;
    }

    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;
        if (RTSInputBindings.GetKeyDown(RTSInputAction.StrategicMap) && (MenuComandoController.Instancia == null || !MenuComandoController.Instancia.MenuAberto))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
            AlternarMapa(!mapaAtivo);
        }

        if (mapaAtivo && cameraMapa != null)
        {
            // Tecla F: alterna modo de seguir unidade selecionada
            if (RTSInputBindings.GetKeyDown(RTSInputAction.Follow))
            {
                _seguindoAlvo = !_seguindoAlvo;
                if (_seguindoAlvo)
                {
                    // Tenta obter a unidade selecionada no GerenteSelecao
                    _alvoSeguir = ObterTransformSelecionado();
                    if (_alvoSeguir == null) _seguindoAlvo = false; // Nada selecionado
                }
                else
                {
                    _alvoSeguir = null;
                }
            }

            // Se estiver seguindo, verifica se o alvo ainda existe
            if (_seguindoAlvo && (_alvoSeguir == null || !_alvoSeguir.gameObject.activeInHierarchy))
            {
                _seguindoAlvo = false;
                _alvoSeguir = null;
            }

            ControlarMapa();

            // Refresh do cache a cada 2s
            if (Time.time > _tempoRefreshCache)
            {
                RefreshCache();
                _tempoRefreshCache = Time.time + 2f;
            }
        }
    }

    public void AbrirMapaEstrategico()
    {
        if (!mapaAtivo) AlternarMapa(true);
    }

    private void AlternarMapa(bool abrir)
    {
        if (cameraMapa == null) return;

        mapaAtivo = abrir;
        if (mapaAtivo && cameraPrincipal != null)
        {
            cameraPrincipalPosicaoAntesDoMapa = cameraPrincipal.transform.position;
            cameraPrincipalRotacaoAntesDoMapa = cameraPrincipal.transform.rotation;
            cameraPrincipalFovAntesDoMapa = cameraPrincipal.fieldOfView;
            snapshotCameraPrincipalValido = true;
        }
        cameraMapa.gameObject.SetActive(mapaAtivo);
        AplicarModoMapa(mapaAtivo);

        if (mapaAtivo && cameraPrincipal != null)
        {
            Vector3 p = cameraPrincipal.transform.position;
            cameraMapa.transform.position = new Vector3(p.x, 1350f, p.z);
            LimitarCameraMapa();
            volumeAudioOriginal = AudioListener.volume;
            AudioListener.volume = 0f;
            RefreshCache();
        }
        else
        {
            AudioListener.volume = volumeAudioOriginal;
            _seguindoAlvo = false;
            _alvoSeguir = null;
            if (snapshotCameraPrincipalValido && cameraPrincipal != null)
            {
                cameraPrincipal.transform.SetPositionAndRotation(
                    cameraPrincipalPosicaoAntesDoMapa,
                    cameraPrincipalRotacaoAntesDoMapa);
                cameraPrincipal.fieldOfView = cameraPrincipalFovAntesDoMapa;
            }
        }
    }

    private Transform ObterTransformSelecionado()
    {
        GerenteSelecao gerente = null;
#if UNITY_2023_1_OR_NEWER
        gerente = Object.FindFirstObjectByType<GerenteSelecao>();
#else
        gerente = Object.FindObjectOfType<GerenteSelecao>();
#endif
        if (gerente == null || gerente.unidadesSelecionadas == null || gerente.unidadesSelecionadas.Count == 0)
            return null;

        ControleUnidade cu = gerente.unidadesSelecionadas[0];
        return cu != null ? cu.transform : null;
    }

    private void OnDisable()
    {
        AudioListener.volume = volumeAudioOriginal;
        AplicarModoMapa(false);
    }

    void RefreshCache()
    {
        _cacheUnidades.Clear();
        var todos = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todos)
            if (u != null) _cacheUnidades.Add(u);
    }

    void ControlarMapa()
    {
        // --- MODO SEGUIR: câmera fica centrada no alvo ---
        if (_seguindoAlvo && _alvoSeguir != null)
        {
            Vector3 alvoPos = _alvoSeguir.position;
            cameraMapa.transform.position = Vector3.Lerp(
                cameraMapa.transform.position,
                new Vector3(alvoPos.x, cameraMapa.transform.position.y, alvoPos.z),
                Time.unscaledDeltaTime * 8f);
            LimitarCameraMapa();

            // Ainda permite zoom enquanto segue
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                cameraMapa.orthographicSize -= scroll * zoomVelocidade * Time.unscaledDeltaTime;
                cameraMapa.orthographicSize  = Mathf.Clamp(cameraMapa.orthographicSize, zoomMinimo, zoomMaximo);
                LimitarCameraMapa();
            }
            return;
        }

        // --- MODO LIVRE: pan normal com WASD/bordas ---
        float movX = Input.GetAxisRaw("Horizontal");
        float movZ = Input.GetAxisRaw("Vertical");

        if (Input.mousePosition.x >= Screen.width  - 5) movX =  1;
        if (Input.mousePosition.x <= 5)                 movX = -1;
        if (Input.mousePosition.y >= Screen.height - 5) movZ =  1;
        if (Input.mousePosition.y <= 5)                 movZ = -1;

        if (movX != 0 || movZ != 0)
        {
            float mult = cameraMapa.orthographicSize / 50f;
            cameraMapa.transform.position += new Vector3(movX, 0, movZ).normalized
                * (velocidadeMover * mult) * Time.unscaledDeltaTime;
            LimitarCameraMapa();
        }

        float scrollLivre = Input.GetAxis("Mouse ScrollWheel");
        if (scrollLivre != 0)
        {
            cameraMapa.orthographicSize -= scrollLivre * zoomVelocidade * Time.unscaledDeltaTime;
            cameraMapa.orthographicSize  = Mathf.Clamp(cameraMapa.orthographicSize, zoomMinimo, zoomMaximo);
            LimitarCameraMapa();
        }

        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus))
            AjustarZoomMapa(-1f);
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
            AjustarZoomMapa(1f);
    }

    private void AjustarZoomMapa(float direcao)
    {
        if (cameraMapa == null) return;
        cameraMapa.orthographicSize = Mathf.Clamp(
            cameraMapa.orthographicSize + direcao * Mathf.Max(10f, zoomVelocidade * 0.08f),
            zoomMinimo,
            zoomMaximo);
        LimitarCameraMapa();
    }

    private void AplicarModoMapa(bool ativo)
    {
        // A neblina pertence ao mundo inteiro. Alterá-la ao alternar a câmera
        // do satélite fazia a câmera principal receber uma mudança de estado
        // durante um frame, produzindo flashes e cores estouradas na build.
        // O mapa usa sua própria câmera/limites; portanto não disputa mais o
        // RenderSettings global com o gameplay.
    }

    // ================================================================
    // OVERLAY DE INTERFACE DO MAPA (desenhado quando mapa está ativo)
    // ================================================================
    void OnGUI()
    {
        if (!mapaAtivo || cameraMapa == null) return;
        GarantirEstilosGui();

        // --- Barra superior com info e botão de fechar ---
        float barH = 30f;
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = _tituloMapaStyle;
        string modoSeguir = _seguindoAlvo && _alvoSeguir != null
            ? $"[F seguir: {_alvoSeguir.name.ToUpper()}]"
            : "[F seguir unidade]";
        GUI.Label(new Rect(0, 0, Screen.width, barH),
            $"MAPA ESTRATEGICO  [WASD mover] [Scroll zoom] [{modoSeguir}] [M fechar]", titleStyle);

        GUIStyle zoomStyle = _zoomMapaStyle;
        if (GUI.Button(new Rect(Screen.width - 118f, 3f, 34f, 24f), "+", zoomStyle)) AjustarZoomMapa(-1f);
        if (GUI.Button(new Rect(Screen.width - 78f, 3f, 34f, 24f), "−", zoomStyle)) AjustarZoomMapa(1f);

        // --- Legenda no canto inferior esquerdo ---
        float legX = 12f, legY = Screen.height - 100f;
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(legX - 6, legY - 6, 175f, 90f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle legStyle = _legendaMapaStyle;
        GUI.Label(new Rect(legX, legY,      170, 20), "■  Prédio Aliado",   legStyle);
        GUI.Label(new Rect(legX, legY + 22, 170, 20), "▲  Unidade Aliada",  legStyle);
        GUI.Label(new Rect(legX, legY + 44, 170, 20), "●  Unidade Neutra",  legStyle);
        GUI.Label(new Rect(legX, legY + 66, 170, 20), "🔵  Oceano",          legStyle);

        // --- Ícones das unidades e prédios no mapa ---
        DesenharTerritorioInimigo();
        DesenharIconesNoMapa();
        DesenharDisparosNoMapa();
        GUI.Label(new Rect(Screen.width - 330f, barH + 8f, 315f, 22f), "DISPAROS: ciano aliado | vermelho inimigo | amarelo neutro", legStyle);
    }

    private void GarantirEstilosGui()
    {
        if (_trianguloSombraStyle != null) return;

        _tituloMapaStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan }
        };
        _zoomMapaStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        _legendaMapaStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        _trianguloSombraStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.black }
        };
        _trianguloCorStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };
    }

    private void DesenharTerritorioInimigo()
    {
        if (cameraMapa == null || !territorioInimigoDisponivel) return;

        Vector3 min = limitesTerrenoInimigo.min;
        Vector3 max = limitesTerrenoInimigo.max;
        Vector3[] cantos = _cantosTerritorioInimigo;
        cantos[0] = cameraMapa.WorldToScreenPoint(new Vector3(min.x, 0f, min.z));
        cantos[1] = cameraMapa.WorldToScreenPoint(new Vector3(min.x, 0f, max.z));
        cantos[2] = cameraMapa.WorldToScreenPoint(new Vector3(max.x, 0f, min.z));
        cantos[3] = cameraMapa.WorldToScreenPoint(new Vector3(max.x, 0f, max.z));

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        bool visivel = false;
        for (int i = 0; i < cantos.Length; i++)
        {
            if (cantos[i].z <= 0f) continue;
            visivel = true;
            minX = Mathf.Min(minX, cantos[i].x);
            maxX = Mathf.Max(maxX, cantos[i].x);
            minY = Mathf.Min(minY, Screen.height - cantos[i].y);
            maxY = Mathf.Max(maxY, Screen.height - cantos[i].y);
        }

        if (!visivel || maxX < 0f || minX > Screen.width || maxY < 0f || minY > Screen.height) return;

        minX = Mathf.Clamp(minX, -2f, Screen.width + 2f);
        maxX = Mathf.Clamp(maxX, -2f, Screen.width + 2f);
        minY = Mathf.Clamp(minY, -2f, Screen.height + 2f);
        maxY = Mathf.Clamp(maxY, -2f, Screen.height + 2f);
        if (maxX <= minX || maxY <= minY) return;

        GUI.color = new Color(0.82f, 0.08f, 0.08f, 0.22f);
        GUI.DrawTexture(new Rect(minX, minY, maxX - minX, maxY - minY), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.18f, 0.12f, 0.8f);
        GUI.DrawTexture(new Rect(minX, minY, maxX - minX, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(minX, maxY - 2f, maxX - minX, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(minX, minY, 2f, maxY - minY), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(maxX - 2f, minY, 2f, maxY - minY), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DesenharIconesNoMapa()
    {
        if (cameraMapa == null) return;

        foreach (var id in _cacheUnidades)
        {
            if (id == null || id.gameObject == null) continue;

            bool ehAliado  = (id.teamID == meuTeamID);
            bool ehNeutro  = (id.teamID == 0);
            bool ehInimigo = (!ehAliado && !ehNeutro);

            Vector3 posicaoMapa = id.transform.position;
            bool contatoAtual = !ehInimigo || RTSVisibilityService.Instancia == null
                || RTSVisibilityService.Instancia.IsVisibleToTeam(meuTeamID, id);
            if (ehInimigo && !contatoAtual)
            {
                if (RTSVisibilityService.Instancia == null
                    || !RTSVisibilityService.Instancia.TryGetLastKnownPosition(meuTeamID, id, out posicaoMapa))
                {
                    continue;
                }
            }

            bool ehPredio = (id.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
                         && (id.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null
                          || id.GetComponent<Rigidbody>() == null);

            // Converte posição 3D para coordenadas da tela relativa à cameraMapa
            Vector3 screenPos = cameraMapa.WorldToScreenPoint(posicaoMapa);

            // Só mostra se estiver na frente da câmera (z > 0) e dentro da tela
            if (screenPos.z <= 0) continue;

            // Inverte Y (Unity GUI vs tela)
            float sx = screenPos.x;
            float sy = Screen.height - screenPos.y;

            if (sx < -20 || sx > Screen.width + 20 || sy < -20 || sy > Screen.height + 20) continue;

            if (ehAliado && ehPredio)
            {
                // Prédio aliado: quadrado branco com borda preta
                DesenharIcone(sx, sy, 10f, 10f, corPredioProprio);
            }
            else if (ehAliado)
            {
                // Unidade aliada: triângulo verde
                DesenharTriangulo(sx, sy, 8f, corUnidadePropria);
            }
            else if (ehNeutro)
            {
                // Unidade neutra: círculo cinza
                DesenharIcone(sx, sy, 7f, 7f, corUnidadeNeutro);
            }
            else if (ehInimigo)
            {
                DesenharIcone(sx, sy, 7f, 7f, contatoAtual
                    ? new Color(1f, 0.2f, 0.12f, 1f)
                    : new Color(1f, 0.35f, 0.18f, 0.45f));
            }
        }
    }

    // Desenha um quadrado colorido
    void DesenharDisparosNoMapa()
    {
        if (cameraMapa == null || Event.current.type != EventType.Repaint) return;

        MissileThreatTracker.CopiarAmeacasAtivas(_misseisAtivos);
        Projetil.CopiarAtivosNoMapa(_projeteisAtivos);

        int inicioMisseis = Mathf.Max(0, _misseisAtivos.Count - 128);
        for (int i = inicioMisseis; i < _misseisAtivos.Count; i++)
        {
            MissileThreatTracker missil = _misseisAtivos[i];
            if (missil == null || missil.RaizMissil == null) continue;
            DesenharRastroCombate(missil.RaizMissil.position, missil.ObterVelocidadeAtual(), CorDoDisparo(missil.TeamOrigem), true);
        }

        int inicioProjeteis = Mathf.Max(0, _projeteisAtivos.Count - 160);
        for (int i = inicioProjeteis; i < _projeteisAtivos.Count; i++)
        {
            Projetil projetil = _projeteisAtivos[i];
            if (projetil == null || projetil.GetComponent<MissileThreatTracker>() != null) continue;
            DesenharRastroCombate(projetil.transform.position, projetil.transform.forward, CorDoDisparo(projetil.TeamDono), false);
        }
    }

    void DesenharRastroCombate(Vector3 posicao, Vector3 direcao, Color cor, bool missil)
    {
        Vector3 tela = cameraMapa.WorldToScreenPoint(posicao);
        if (tela.z <= 0f) return;

        float sx = tela.x;
        float sy = Screen.height - tela.y;
        if (sx < -20f || sx > Screen.width + 20f || sy < -20f || sy > Screen.height + 20f) return;

        Vector3 dir = direcao.sqrMagnitude > 0.001f ? direcao.normalized : Vector3.forward;
        Vector3 telaFrente = cameraMapa.WorldToScreenPoint(posicao + dir * (missil ? 45f : 20f));
        Vector2 delta = new Vector2(telaFrente.x - tela.x, -(telaFrente.y - tela.y));
        if (delta.sqrMagnitude > 0.01f) delta.Normalize();

        GUI.color = new Color(cor.r, cor.g, cor.b, 0.65f);
        int pontos = missil ? 5 : 3;
        for (int i = 1; i <= pontos; i++)
        {
            float distancia = i * (missil ? 4f : 3f);
            GUI.DrawTexture(new Rect(sx - delta.x * distancia - 1f, sy - delta.y * distancia - 1f, 2f, 2f), Texture2D.whiteTexture);
        }

        GUI.color = cor;
        float tamanho = missil ? 8f : 4f;
        GUI.DrawTexture(new Rect(sx - tamanho * 0.5f, sy - tamanho * 0.5f, tamanho, tamanho), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    Color CorDoDisparo(int team)
    {
        if (team == meuTeamID) return Color.cyan;
        if (team > 0) return new Color(1f, 0.2f, 0.12f, 1f);
        return Color.yellow;
    }

    void DesenharIcone(float cx, float cy, float w, float h, Color cor)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(cx - w/2f - 1, cy - h/2f - 1, w + 2, h + 2), Texture2D.whiteTexture);
        GUI.color = cor;
        GUI.DrawTexture(new Rect(cx - w/2f, cy - h/2f, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // Desenha triângulo (simulado com labels de símbolo)
    void DesenharTriangulo(float cx, float cy, float size, Color cor)
    {
        int fontSize = (int)(size * 2);
        _trianguloSombraStyle.fontSize = fontSize;
        _trianguloCorStyle.fontSize = fontSize;
        // Sombra
        GUI.Label(new Rect(cx - size + 1, cy - size + 1, size * 2, size * 2), "▲", _trianguloSombraStyle);
        // Cor real
        _trianguloCorStyle.normal.textColor = cor;
        GUI.Label(new Rect(cx - size, cy - size, size * 2, size * 2), "▲", _trianguloCorStyle);
    }
}
