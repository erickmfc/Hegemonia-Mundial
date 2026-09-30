using UnityEngine;
using System.Collections.Generic;
using Hegemonia.RTS;

/// <summary>
/// MAPA GERAL TÁTICO - Pressione M para abrir/fechar.
/// - Camera ortográfica de cima com fundo pintado de azul oceano.
/// - Mostra ícones de PRÉDIOS aliados e UNIDADES aliadas.
/// - NUNCA revela unidades inimigas (Fog of War).
/// - Zoom com scroll do mouse. Pan apenas com WASD/setas (sem movimento nas bordas).
/// </summary>
public class MapaGeralController : MonoBehaviour
{
    public static MapaGeralController Instancia { get; private set; }
    public static bool EstaAberto { get { return Instancia != null && Instancia.mapaAtivo; } }
    public bool MapaCartograficoAtivo { get { return mapaCartograficoAtivo; } }

    public static Camera ObterCameraDeInteracao()
    {
        if (Instancia != null && Instancia.mapaAtivo && Instancia.cameraMapa != null)
        {
            return Instancia.cameraMapa;
        }

        return Camera.main;
    }

    private Camera cameraPrincipal;
    private Camera cameraMapa;
    private GlobalWorldDefinition definicaoMapaGlobal;
    private Material materialBaseMilitar;
    private static readonly int TexturaAutoridadeMapaId = Shader.PropertyToID("_AuthorityTex");
    private static readonly int TexturaAparenciaMapaId = Shader.PropertyToID("_AppearanceTex");
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
    [Tooltip("Quando habilitado, a demo abre mostrando toda a cobertura dos Terrains. O jogador ainda pode usar zoom e pan normalmente.")]
    [SerializeField] private bool enquadrarCoberturaCompletaAoAbrir = true;
    [SerializeField, Min(1f)] private float margemEnquadramentoInicial = 1.08f;

    [Header("Configurações de Exibição")]
    public int meuTeamID = 1; // ID do jogador (unidades aliadas a mostrar)
    public float nivelDoMar = 0f; // Heights abaixo disso = oceano azul

    // Cores do mapa
    private readonly Color corFundoMar       = new Color32(31, 49, 61, 255);
    private readonly Color corBordaMar       = new Color32(115, 150, 164, 190);
    private readonly Color corPredioProprio  = new Color32(190, 207, 203, 255);
    private readonly Color corUnidadePropria = new Color32(75, 196, 205, 255);
    private readonly Color corUnidadeNeutro  = new Color32(194, 184, 157, 255);
    private readonly Color corInimigoAtual   = new Color32(219, 103, 94, 255);
    private readonly Color corInimigoMemoria = new Color32(202, 133, 101, 165);
    private readonly Color corTextoMapa      = new Color32(222, 228, 218, 255);
    private readonly Color corPainelMapa     = new Color32(17, 29, 37, 232);
    private static readonly Color[] CoresPoliticas =
    {
        new Color32(116, 169, 139, 195),
        new Color32(112, 163, 181, 195),
        new Color32(190, 157, 112, 195),
        new Color32(189, 119, 116, 195),
        new Color32(157, 143, 181, 195)
    };

    // Cache de objetos do mundo para não chamar Find() o tempo todo
    private List<IdentidadeUnidade> _cacheUnidades = new List<IdentidadeUnidade>();
    private readonly HashSet<int> _imoveisMapa = new HashSet<int>();
    private readonly HashSet<int> _prediosMapa = new HashSet<int>();
    private readonly Dictionary<int, int> _categoriasMapa = new Dictionary<int, int>(256);
    private readonly HashSet<int> _unidadesMoveisMapa = new HashSet<int>();
    private readonly List<MissileThreatTracker> _misseisAtivos = new List<MissileThreatTracker>(64);
    private readonly List<RTSMissileVisibilityContact> _contatosMisseis = new List<RTSMissileVisibilityContact>(64);
    private readonly Vector3[] _cantosTerritorioInimigo = new Vector3[4];
    private float _tempoRefreshCache = 0f;
    private DesenharLinhasOrdem _desenharOrdens;

    // Estilos IMGUI sao reutilizados enquanto o mapa esta aberto para nao alocar por repaint.
    private GUIStyle _tituloMapaStyle;
    private GUIStyle _zoomMapaStyle;
    private GUIStyle _legendaMapaStyle;
    private GUIStyle _trianguloSombraStyle;
    private GUIStyle _trianguloCorStyle;
    private GUIStyle _camadaMapaStyle;
    private GUIStyle _rotuloRegiaoStyle;
    private GUIStyle _estiloMiniMapa;
    private GUIStyle _toggleCamadaStyle;
    private readonly List<Rect> _rotulosMapaDesenhados = new List<Rect>(16);

    [Header("Mapa cartográfico M")]
    [SerializeField] private bool mapaCartograficoInicial = true;
    private bool mapaCartograficoAtivo = true;
    private bool camadaFronteiras = true;
    private bool camadaCidades = true;
    private bool camadaBases = true;
    private bool camadaPortos = true;
    private bool camadaAeroportos = true;
    private bool camadaRadares = true;
    private bool camadaUnidades = true;
    private bool camadaAreasPatrulha;
    private bool camadaRecursos;
    private bool camadaEconomia;
    private bool camadaPopulacao;
    private bool camadaInteligencia;
    private bool camadaLogistica;

    // --- Modo de seguir unidade selecionada ---
    private bool _seguindoAlvo = false;
    private Transform _alvoSeguir = null;
    private int _missilSelecionadoId = -1;
    private CartaTerrenoRenderer _cameraRastreamentoMissil;
    private bool _cameraMissilEmRastreamento;
    private int _cameraMissilInstanceId;
    private readonly Dictionary<long, ClusterUnidadeMapa> _clustersUnidades = new Dictionary<long, ClusterUnidadeMapa>(256);

    private struct ClusterUnidadeMapa
    {
        public float x;
        public float y;
        public float direcaoX;
        public float direcaoY;
        public int quantidade;
    }

    private Vector2 centroMapa = Vector2.zero;
    private float metadeMapa = 5000f;
    private float larguraMapa = 10000f;
    private float profundidadeMapa = 10000f;
    private bool limitesMapaInicializados;
    private Terrain terrenoInimigo;
    private Bounds limitesTerrenoInimigo;
    private bool territorioInimigoDisponivel;
    private DadosMapaTerritorial dadosMapaCartograficoFallback;
    private bool tentouCarregarDadosMapaCartografico;

    // --- Shader warmup (evita compilação durante o voo) ---

    private void Awake()
    {
        Instancia = this;
        mapaCartograficoAtivo = mapaCartograficoInicial;
    }

    private void OnDestroy()
    {
        if (_cameraRastreamentoMissil != null) _cameraRastreamentoMissil.PararRastreamento();
        if (materialBaseMilitar != null) Destroy(materialBaseMilitar);
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
        InicializarBaseCartograficaMilitar();
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
        cameraMapa.enabled          = false; // Mapa cartográfico é desenhado pela base e overlays IMGUI.
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
        GlobalWorldDefinition definicaoGlobal = ObterDefinicaoMapaGlobal();
        if (definicaoGlobal != null && definicaoGlobal.worldSize.x > 0.001f && definicaoGlobal.mapFootprintHeight > 0.001f)
        {
            larguraMapa = definicaoGlobal.worldSize.x;
            profundidadeMapa = definicaoGlobal.mapFootprintHeight;
            centroMapa = new Vector2(
                (definicaoGlobal.MapMinX + definicaoGlobal.MapMaxX) * 0.5f,
                (definicaoGlobal.MapMinZ + definicaoGlobal.MapMaxZ) * 0.5f);
            metadeMapa = Mathf.Max(larguraMapa * 0.5f, profundidadeMapa * 0.5f);
            zoomMaximo = Mathf.Max(zoomMaximo, metadeMapa);
            limitesMapaInicializados = true;
            Debug.Log($"[MapaGeral] Footprint lógico do GlobalWorldDefinition: {larguraMapa:F0} x {profundidadeMapa:F0} m; UV independente dos tiles ativos.");
            return;
        }

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

        // The streamed global world can start with its coarse Terrain disabled
        // and no close tiles loaded yet. Use its logical bounds so M opens at
        // the full map instead of falling back to the legacy 10 km area.
        if (detectarLimitesReaisDoMapa)
        {
            GlobalTerrainStreamer[] streamers = FindObjectsByType<GlobalTerrainStreamer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < streamers.Length; i++)
            {
                GlobalWorldDefinition world = streamers[i] != null ? streamers[i].world : null;
                if (world == null) continue;

                minX = Mathf.Min(minX, world.MapMinX);
                maxX = Mathf.Max(maxX, world.MapMaxX);
                minZ = Mathf.Min(minZ, world.MapMinZ);
                maxZ = Mathf.Max(maxZ, world.MapMaxZ);
                encontrouTerrain = true;
            }
        }

        if (!encontrouTerrain)
        {
            centroMapa = Vector2.zero;
            metadeMapa = metadeConfigurada;
            larguraMapa = metadeConfigurada * 2f;
            profundidadeMapa = metadeConfigurada * 2f;
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
        larguraMapa = Mathf.Max(1f, maxX - minX);
        profundidadeMapa = Mathf.Max(1f, maxZ - minZ);
        float metadeTerrain = Mathf.Max((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
        metadeMapa = Mathf.Max(metadeConfigurada, metadeTerrain);
        zoomMaximo = Mathf.Max(zoomMaximo, metadeMapa);
        limitesMapaInicializados = true;

        Debug.Log($"[MapaGeral] Limites do mapa: centro=({centroMapa.x:F0}, {centroMapa.y:F0}) metade={metadeMapa:F0} zoomMaximo={zoomMaximo:F0}.");
    }

    private GlobalWorldDefinition ObterDefinicaoMapaGlobal()
    {
        if (definicaoMapaGlobal != null) return definicaoMapaGlobal;

        GlobalTerrainStreamer[] streamers = FindObjectsByType<GlobalTerrainStreamer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < streamers.Length; i++)
        {
            if (streamers[i] == null || streamers[i].world == null) continue;
            if (streamers[i].gameObject.scene == gameObject.scene)
            {
                definicaoMapaGlobal = streamers[i].world;
                return definicaoMapaGlobal;
            }
        }

        for (int i = 0; i < streamers.Length; i++)
        {
            if (streamers[i] != null && streamers[i].world != null)
            {
                definicaoMapaGlobal = streamers[i].world;
                return definicaoMapaGlobal;
            }
        }
        return null;
    }

    private void InicializarBaseCartograficaMilitar()
    {
        GlobalWorldDefinition world = ObterDefinicaoMapaGlobal();
        if (world == null || world.authorityMap == null || world.appearanceMap == null) return;

        Shader shader = Resources.Load<Shader>("MapaMBaseMilitar");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[MapaGeral] Shader MapaMBaseMilitar indisponível; mantendo a base cartográfica original.");
            return;
        }

        materialBaseMilitar = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        materialBaseMilitar.SetTexture(TexturaAutoridadeMapaId, world.authorityMap);
        materialBaseMilitar.SetTexture(TexturaAparenciaMapaId, world.appearanceMap);
    }

    private void LimitarCameraMapa()
    {
        if (cameraMapa == null) return;
        if (!limitesMapaInicializados) AtualizarLimitesMapa();

        float aspecto = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1f, Screen.height));
        float meiaAltura = cameraMapa.orthographicSize;
        float meiaLargura = meiaAltura * aspecto;
        float limiteX = Mathf.Max(0f, larguraMapa * 0.5f - meiaLargura);
        float limiteZ = Mathf.Max(0f, profundidadeMapa * 0.5f - meiaAltura);

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
            ProcessarInteracaoNoMapa();

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
        AtualizarRenderizacaoCameraMapa();
        AplicarModoMapa(mapaAtivo);

        if (mapaAtivo && cameraPrincipal != null)
        {
            Vector3 p = cameraPrincipal.transform.position;
            cameraMapa.transform.position = new Vector3(p.x, 1350f, p.z);
            if (enquadrarCoberturaCompletaAoAbrir)
            {
                EnquadrarCoberturaCompleta();
            }
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
            PararCameraDeRastreamentoMissil();
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

    private void ProcessarInteracaoNoMapa()
    {
        if (cameraMapa == null || AreaDeInterfaceMapa(Input.mousePosition))
        {
            return;
        }

        DesenharLinhasOrdem desenhador = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhador != null && (desenhador.modoPatrulhaAtivo || desenhador.modoSeguirAtivo || desenhador.modoAtaqueAtivo))
        {
            // Os modos de ordem têm prioridade: eles usam a mesma câmera
            // satélite e consomem o clique para desenhar/confirmar a ordem.
            return;
        }

        GerenteSelecao gerente = Object.FindFirstObjectByType<GerenteSelecao>();
        if (gerente == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (SelecionarMissilSobCursor()) return;

            ControleUnidade unidade = EncontrarUnidadeAliadaNoIcone();
            if (unidade != null)
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    gerente.DeselecionarTudo();
                }

                gerente.AdicionarSelecao(unidade);
                return;
            }
        }

        if (Input.GetMouseButtonDown(1) && gerente.unidadesSelecionadas != null && gerente.unidadesSelecionadas.Count > 0)
        {
            if (TryObterDestinoNoMapa(out Vector3 destino))
            {
                gerente.EmitirOrdemNoMapa(destino);
            }
        }
    }

    private ControleUnidade EncontrarUnidadeAliadaNoIcone()
    {
        ControleUnidade melhor = null;
        float menorDistancia = 24f;
        Vector2 mouse = Input.mousePosition;

        for (int i = 0; i < _cacheUnidades.Count; i++)
        {
            IdentidadeUnidade identidade = _cacheUnidades[i];
            if (identidade == null || identidade.teamID != meuTeamID)
            {
                continue;
            }

            ControleUnidade controle = identidade.GetComponent<ControleUnidade>()
                ?? identidade.GetComponentInParent<ControleUnidade>()
                ?? identidade.GetComponentInChildren<ControleUnidade>(true);
            if (controle == null)
            {
                continue;
            }

            Vector3 tela = cameraMapa.WorldToScreenPoint(identidade.transform.position);
            if (tela.z <= 0f)
            {
                continue;
            }

            float distancia = Vector2.Distance(mouse, new Vector2(tela.x, tela.y));
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhor = controle;
            }
        }

        return melhor;
    }

    private bool TryObterDestinoNoMapa(out Vector3 destino)
    {
        Ray raio = cameraMapa.ScreenPointToRay(Input.mousePosition);
        int mascara = ~(1 << 2);
        RaycastHit[] hits = Physics.RaycastAll(raio, Mathf.Infinity, mascara, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null)
            {
                continue;
            }

            if (collider.GetComponentInParent<ControleUnidade>() != null
                || collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null
                || collider.GetComponentInParent<TorreDeControle>() != null
                || collider.GetComponentInParent<GerenciadorAeroporto>() != null
                || collider.GetComponentInParent<Estaleiro>() != null
                || collider.GetComponentInParent<PierMarinha>() != null
                || collider.GetComponentInParent<Fabrica>() != null
                || collider.GetComponentInParent<AtributosPredio>() != null
                || collider.GetComponentInParent<Edificio>() != null
                || collider.GetComponentInParent<MdHistoriaMapaRuntime>() != null)
            {
                continue;
            }

            destino = hits[i].point;
            return true;
        }

        Plane planoMar = new Plane(Vector3.up, nivelDoMar);
        if (planoMar.Raycast(raio, out float distanciaPlano))
        {
            destino = raio.GetPoint(distanciaPlano);
            destino.y = nivelDoMar;
            return true;
        }

        destino = Vector3.zero;
        return false;
    }

    private bool AreaDeInterfaceMapa(Vector3 posicaoMouse)
    {
        float yTopo = Screen.height - posicaoMouse.y;
        if (_missilSelecionadoId >= 0
            && posicaoMouse.x >= Screen.width - 390f
            && yTopo >= Screen.height - 275f)
            return true;
        return yTopo <= 44f
            || (posicaoMouse.x <= 205f && yTopo >= Screen.height - 108f)
            || (mapaCartograficoAtivo && posicaoMouse.x >= Screen.width - 210f);
    }

    private void OnDisable()
    {
        AudioListener.volume = volumeAudioOriginal;
        PararCameraDeRastreamentoMissil();
        AplicarModoMapa(false);
    }

    void RefreshCache()
    {
        _cacheUnidades.Clear();
        _imoveisMapa.Clear();
        _prediosMapa.Clear();
        _categoriasMapa.Clear();
        _unidadesMoveisMapa.Clear();
        if (_desenharOrdens == null) _desenharOrdens = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        var todos = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todos)
        {
            if (u == null) continue;
            _cacheUnidades.Add(u);
            int id = u.GetInstanceID();
            bool ehImovel = EhImovelMapa(u.gameObject);
            bool ehMovel = EhUnidadeMovelMapa(u);
            bool ehPredio = u.tipoUnidade == TipoUnidade.Estrutura
                || (!ehMovel && u.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null);
            if (ehPredio) _prediosMapa.Add(id);
            if (ehImovel)
            {
                _imoveisMapa.Add(id);
                _categoriasMapa[id] = ClassificarCategoriaMapa(u);
            }
            else
            {
                _categoriasMapa[id] = 64; // entidade móvel ou estrutura sem classificação de imóvel.
                if (ehMovel) _unidadesMoveisMapa.Add(id);
            }
        }
    }

    private static bool EhUnidadeMovelMapa(IdentidadeUnidade identidade)
    {
        if (identidade == null) return false;
        return identidade.GetComponent<ControleUnidade>() != null
            || identidade.GetComponent<ControleAviao>() != null
            || identidade.GetComponent<ControleAviaoCaca>() != null
            || identidade.GetComponent<Helicoptero>() != null
            || identidade.GetComponent<VooHelicoptero>() != null
            || identidade.GetComponent<ControleNavioRealista>() != null
            || identidade.GetComponent<ControleSubmarino>() != null
            || identidade.GetComponent<IdentidadeNaval>() != null
            || identidade.GetComponent<C700TransporteAereo>() != null;
    }

    private static int ClassificarCategoriaMapa(IdentidadeUnidade identidade)
    {
        int resultado = 0;
        GameObject raiz = identidade != null && identidade.transform.root != null ? identidade.transform.root.gameObject : identidade.gameObject;
        Component[] componentes = raiz.GetComponentsInChildren<Component>(true);
        bool ehQuartelOuBase = false;
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null) continue;
            string tipo = componente.GetType().Name;
            if (tipo.IndexOf("Radar", System.StringComparison.OrdinalIgnoreCase) >= 0) resultado |= 32;
            if (tipo.IndexOf("Aeroporto", System.StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("TorreDeControle", System.StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("Hangar", System.StringComparison.OrdinalIgnoreCase) >= 0) resultado |= 16;
            if (tipo.IndexOf("Porto", System.StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("PierMarinha", System.StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("Estaleiro", System.StringComparison.OrdinalIgnoreCase) >= 0) resultado |= 8;
            if (tipo.IndexOf("Quartel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("Base", System.StringComparison.OrdinalIgnoreCase) >= 0) ehQuartelOuBase = true;
        }
        if (ehQuartelOuBase) resultado |= 4;
        if (resultado == 0) resultado = 2; // Estruturas restantes: cidades/instalações existentes.
        return resultado;
    }

    private bool CamadaMostraEntidade(IdentidadeUnidade identidade, bool ehPredio)
    {
        if (!mapaCartograficoAtivo) return true;
        if (identidade == null) return false;
        int categoria;
        if (!_categoriasMapa.TryGetValue(identidade.GetInstanceID(), out categoria))
            categoria = ehPredio ? ClassificarCategoriaMapa(identidade) : 64;
        if (!ehPredio) return camadaUnidades && (categoria & 64) != 0;
        return ((categoria & 2) != 0 && camadaCidades)
            || ((categoria & 4) != 0 && camadaBases)
            || ((categoria & 8) != 0 && camadaPortos)
            || ((categoria & 16) != 0 && camadaAeroportos)
            || ((categoria & 32) != 0 && camadaRadares);
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
                AplicarZoomPorScroll(scroll);
                LimitarCameraMapa();
            }
            return;
        }

        // --- MODO LIVRE: pan somente por teclas; mouse fica para zoom e cliques ---
        // Nao use os eixos Horizontal/Vertical aqui: eles podem ser remapeados
        // para Mouse X/Y no Input Manager e fazer o cursor deslocar o mapa.
        float movX = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
            - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float movZ = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
            - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

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
            AplicarZoomPorScroll(scrollLivre);
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
            cameraMapa.orthographicSize * Mathf.Exp(direcao * 0.2f),
            zoomMinimo,
            zoomMaximo);
        LimitarCameraMapa();
    }

    private void AplicarZoomPorScroll(float scrollDelta)
    {
        if (cameraMapa == null || Mathf.Approximately(scrollDelta, 0f)) return;
        cameraMapa.orthographicSize = Mathf.Clamp(
            cameraMapa.orthographicSize * Mathf.Exp(-scrollDelta * 2f),
            zoomMinimo,
            zoomMaximo);
        LimitarCameraMapa();
    }

    private void AtualizarRenderizacaoCameraMapa()
    {
        if (cameraMapa == null) return;
        // No modo cartográfico a câmera serve para projeção e picking. Deixá-la
        // renderizar por cima do IMGUI apagava a base, as fronteiras e os rótulos.
        cameraMapa.enabled = mapaAtivo && !mapaCartograficoAtivo;
    }

    /// <summary>
    /// Ajusta somente o zoom da câmera do mapa para que o quadrado de
    /// cobertura calculado a partir dos Terrains caiba na tela. A posição do
    /// jogador e a câmera principal não são alteradas.
    /// </summary>
    private void EnquadrarCoberturaCompleta()
    {
        if (cameraMapa == null) return;
        if (!limitesMapaInicializados) AtualizarLimitesMapa();

        float margem = Mathf.Max(1f, margemEnquadramentoInicial);
        float aspecto = cameraMapa.aspect > 0.1f
            ? cameraMapa.aspect
            : (float)Screen.width / Mathf.Max(1f, Screen.height);
        // OrthographicSize is half the vertical world span. Using only the
        // largest world half-size leaves excessive blue margins on wide
        // screens and makes side tiles look as if they were missing.
        float metadePorLargura = (larguraMapa * 0.5f) / Mathf.Max(0.1f, aspecto);
        float tamanhoNecessario = Mathf.Max(
            zoomMinimo,
            Mathf.Max(profundidadeMapa * 0.5f, metadePorLargura) * margem);
        zoomMaximo = Mathf.Max(zoomMaximo, tamanhoNecessario);
        cameraMapa.orthographicSize = Mathf.Clamp(tamanhoNecessario, zoomMinimo, zoomMaximo);
        cameraMapa.transform.position = new Vector3(
            centroMapa.x,
            cameraMapa.transform.position.y,
            centroMapa.y);
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
        if (Event.current.type == EventType.Repaint) _rotulosMapaDesenhados.Clear();

        if (mapaCartograficoAtivo)
        {
            DesenharFundoCartografico();
            if (camadaFronteiras) DesenharFronteirasPoliticas();
            if (camadaAreasPatrulha) DesenharRotasPatrulha();
        }

        // Barra superior e controles mantêm a hierarquia visual da carta.
        const float barH = 38f;
        GUI.color = corPainelMapa;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.color = new Color32(89, 177, 184, 225);
        GUI.DrawTexture(new Rect(0, barH - 2f, Screen.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string modoSeguir = _seguindoAlvo && _alvoSeguir != null
            ? "SEGUINDO: " + _alvoSeguir.name.ToUpperInvariant()
            : "F  ACOMPANHAR UNIDADE";
        string nivelZoom = ObterDescricaoZoomMapa();
        string titulo = mapaCartograficoAtivo ? "HEGEMONIA  /  CARTA TÁTICA" : "HEGEMONIA  /  CÂMERA 3D";
        GUI.Label(new Rect(14f, 2f, Mathf.Max(280f, Screen.width - 380f), 19f), titulo, _tituloMapaStyle);
        GUI.Label(new Rect(15f, 20f, Mathf.Max(280f, Screen.width - 380f), 16f),
            nivelZoom + "   ·   " + modoSeguir + "   ·   WASD mover   ·   Scroll zoom   ·   M fechar", _legendaMapaStyle);

        if (GUI.Button(new Rect(Screen.width - 270f, 7f, 88f, 25f), mapaCartograficoAtivo ? "Câmera 3D" : "Carta 2D", _zoomMapaStyle))
        {
            mapaCartograficoAtivo = !mapaCartograficoAtivo;
            AtualizarRenderizacaoCameraMapa();
        }
        if (GUI.Button(new Rect(Screen.width - 174f, 7f, 32f, 25f), "+", _zoomMapaStyle)) AjustarZoomMapa(-1f);
        if (GUI.Button(new Rect(Screen.width - 136f, 7f, 32f, 25f), "−", _zoomMapaStyle)) AjustarZoomMapa(1f);
        if (GUI.Button(new Rect(Screen.width - 98f, 7f, 90f, 25f), "M  FECHAR", _zoomMapaStyle))
            AlternarMapa(false);

        if (mapaCartograficoAtivo)
        {
            Rect rodape = new Rect(12f, Screen.height - 29f, Mathf.Min(640f, Screen.width - 24f), 20f);
            GUI.color = new Color(corPainelMapa.r, corPainelMapa.g, corPainelMapa.b, 0.78f);
            GUI.DrawTexture(rodape, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rodape.x + 8f, rodape.y, rodape.width - 16f, rodape.height),
                "Geografia: fase.png   ·   Soberania: polígonos políticos   ·   Contatos: inteligência disponível", _legendaMapaStyle);
        }
        else
        {
            // --- Legenda do modo de câmera superior ---
            float legX = 12f, legY = Screen.height - 100f;
            GUI.color = corPainelMapa;
            GUI.DrawTexture(new Rect(legX - 6, legY - 6, 190f, 90f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(legX, legY,      180, 20), "BASE  /  INSTALAÇÃO", _legendaMapaStyle);
            GUI.Label(new Rect(legX, legY + 22, 180, 20), "UNIDADE ALIADA", _legendaMapaStyle);
            GUI.Label(new Rect(legX, legY + 44, 180, 20), "UNIDADE NEUTRA", _legendaMapaStyle);
            GUI.Label(new Rect(legX, legY + 66, 180, 20), "OCEANO", _legendaMapaStyle);
            DesenharTerritorioInimigo();
        }

        // Os mesmos registros de entidades e visibilidade alimentam os dois modos.
        DesenharIconesNoMapa();
        DesenharDisparosNoMapa();
        if (mapaCartograficoAtivo) DesenharCamadasMapa();
        Rect legendaContatos = new Rect(14f, barH + 8f, 305f, 20f);
        GUI.color = new Color(corPainelMapa.r, corPainelMapa.g, corPainelMapa.b, 0.72f);
        GUI.DrawTexture(legendaContatos, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(legendaContatos.x + 8f, legendaContatos.y, legendaContatos.width - 16f, legendaContatos.height),
            "ALIADO  ·  HOSTIL DETECTADO  ·  ÚLTIMA POSIÇÃO", _legendaMapaStyle);
        DesenharPainelRastreamentoMissil();
    }

    private string ObterDescricaoZoomMapa()
    {
        if (cameraMapa == null) return "MAPA";
        float zoom = cameraMapa.orthographicSize;
        if (zoom > 90000f) return "VISÃO GLOBAL";
        if (zoom > 22000f) return "VISÃO OPERACIONAL";
        return "VISÃO TÁTICA";
    }

    private void DesenharFundoCartografico()
    {
        if (Event.current.type != EventType.Repaint) return;
        GUI.color = corFundoMar;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        DadosMapaTerritorial dados = ObterDadosMapaCartografico(gerente);
        Texture2D mapa = dados != null && dados.ImagemReferencia != null
            ? dados.ImagemReferencia
            : Resources.Load<Texture2D>("fase");
        if (mapa == null || cameraMapa == null) return;

        Vector3 cantoSuperiorEsquerdo = MapUvToWorldCartografico(gerente, Vector2.zero);
        Vector3 cantoInferiorDireito = MapUvToWorldCartografico(gerente, Vector2.one);
        Vector3 telaSuperiorEsquerda = cameraMapa.WorldToScreenPoint(cantoSuperiorEsquerdo);
        Vector3 telaInferiorDireita = cameraMapa.WorldToScreenPoint(cantoInferiorDireito);
        if (telaSuperiorEsquerda.z <= 0f || telaInferiorDireita.z <= 0f) return;
        float x = telaSuperiorEsquerda.x;
        float y = Screen.height - telaSuperiorEsquerda.y;
        float largura = telaInferiorDireita.x - telaSuperiorEsquerda.x;
        float altura = telaSuperiorEsquerda.y - telaInferiorDireita.y;
        if (largura <= 0f || altura <= 0f) return;
        Rect areaMapa = new Rect(x, y, largura, altura);
        if (materialBaseMilitar != null)
        {
            Graphics.DrawTexture(areaMapa, mapa, materialBaseMilitar);
        }
        else
        {
            GUI.DrawTexture(areaMapa, mapa, ScaleMode.StretchToFill, false);
            // Fallback para cenas antigas sem GlobalWorldDefinition ou shader.
            GUI.color = new Color32(33, 49, 57, 34);
            GUI.DrawTexture(areaMapa, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DesenharFronteirasPoliticas()
    {
        if (Event.current.type != EventType.Repaint || cameraMapa == null) return;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        DadosMapaTerritorial dados = ObterDadosMapaCartografico(gerente);
        if (dados == null) return;

        float zoom = cameraMapa.orthographicSize;
        bool visaoGlobal = zoom > 90000f;
        bool visaoMedia = zoom > 22000f;
        for (int i = 0; i < dados.Regioes.Count; i++)
        {
            RegiaoPolitica regiao = dados.Regioes[i];
            if (regiao == null || !regiao.PossuiPoligono) continue;
            Color cor = CorPoliticaDaRegiao(gerente, regiao);
            bool limiteMaritimo = regiao.tipo == TipoRegiaoPolitica.AguasTerritoriais;
            float espessura = limiteMaritimo ? 0.65f : (visaoGlobal ? 0.75f : (visaoMedia ? 0.9f : 1.05f));
            Vector3 anterior = Vector3.zero;
            bool anteriorVisivel = false;
            for (int v = 0; v <= regiao.vertices.Count; v++)
            {
                Vector2 uv = regiao.vertices[v % regiao.vertices.Count];
                Vector3 mundo = MapUvToWorldCartografico(gerente, uv);
                Vector3 tela = cameraMapa.WorldToScreenPoint(mundo);
                if (tela.z <= 0f)
                {
                    anteriorVisivel = false;
                    continue;
                }
                Vector2 atual = new Vector2(tela.x, Screen.height - tela.y);
                if (anteriorVisivel)
                {
                    float comprimento = Vector2.Distance(anterior, atual);
                    float comprimentoMinimo = visaoGlobal ? 3.5f : (visaoMedia ? 1.5f : 0.65f);
                    if (comprimento >= comprimentoMinimo)
                    {
                        if (limiteMaritimo && !visaoGlobal)
                            DesenharLinhaTracejada(anterior, atual, cor, espessura, 9f, 6f);
                        else
                            DesenharLinhaTela(anterior, atual, cor, espessura);
                    }
                }
                anterior = atual;
                anteriorVisivel = true;
            }

            if (regiao.tipo == TipoRegiaoPolitica.Terra && DeveExibirRotuloTerritorio(regiao, visaoGlobal, visaoMedia))
            {
                Vector2 centro = CentroideRegiao(regiao.vertices);
                Vector3 telaCentro = cameraMapa.WorldToScreenPoint(MapUvToWorldCartografico(gerente, centro));
                if (telaCentro.z > 0f && telaCentro.x >= 0f && telaCentro.x <= Screen.width
                    && telaCentro.y >= 0f && telaCentro.y <= Screen.height)
                {
                    string dono = TextoDonoTerritorial(gerente, regiao);
                    string texto = visaoGlobal ? regiao.nome.ToUpperInvariant() : regiao.nome + "  ·  " + dono;
                    float fonte = visaoGlobal ? 9f : (visaoMedia ? 10f : 11f);
                    _rotuloRegiaoStyle.fontSize = Mathf.RoundToInt(fonte);
                    float larguraRotulo = Mathf.Clamp(texto.Length * fonte * 0.58f + 16f, 82f, 230f);
                    Rect rotulo = new Rect(telaCentro.x - larguraRotulo * 0.5f,
                        Screen.height - telaCentro.y - 11f, larguraRotulo, fonte + 9f);
                    if (PodeDesenharRotuloSemSobrepor(ref rotulo))
                    {
                        GUI.color = new Color(corPainelMapa.r, corPainelMapa.g, corPainelMapa.b, 0.73f);
                        GUI.DrawTexture(rotulo, Texture2D.whiteTexture);
                        GUI.color = new Color32(102, 164, 169, 205);
                        GUI.DrawTexture(new Rect(rotulo.x, rotulo.y, 2f, rotulo.height), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(rotulo.x + 5f, rotulo.y, rotulo.width - 9f, rotulo.height), texto, _rotuloRegiaoStyle);
                    }
                }
            }
            else if (limiteMaritimo && zoom <= 9000f)
            {
                Vector2 centro = CentroideRegiao(regiao.vertices);
                Vector3 telaCentro = cameraMapa.WorldToScreenPoint(MapUvToWorldCartografico(gerente, centro));
                if (telaCentro.z > 0f && telaCentro.x >= 0f && telaCentro.x <= Screen.width
                    && telaCentro.y >= 0f && telaCentro.y <= Screen.height)
                {
                    string texto = regiao.nome.ToUpperInvariant();
                    _estiloMiniMapa.fontSize = 8;
                    float larguraRotulo = Mathf.Clamp(texto.Length * 4.8f + 14f, 80f, 190f);
                    Rect rotulo = new Rect(telaCentro.x - larguraRotulo * 0.5f,
                        Screen.height - telaCentro.y - 10f, larguraRotulo, 17f);
                    if (PodeDesenharRotuloSemSobrepor(ref rotulo))
                    {
                        GUI.color = new Color(corPainelMapa.r, corPainelMapa.g, corPainelMapa.b, 0.46f);
                        GUI.DrawTexture(rotulo, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(rotulo, texto, _estiloMiniMapa);
                    }
                }
            }
        }
    }

    private bool DeveExibirRotuloTerritorio(RegiaoPolitica regiao, bool visaoGlobal, bool visaoMedia)
    {
        if (regiao == null || regiao.tipo != TipoRegiaoPolitica.Terra) return false;
        if (!visaoGlobal && !visaoMedia) return true;
        if (regiao.vertices == null || regiao.vertices.Count < 3) return false;
        Vector2 min = regiao.vertices[0];
        Vector2 max = min;
        for (int i = 1; i < regiao.vertices.Count; i++)
        {
            min = Vector2.Min(min, regiao.vertices[i]);
            max = Vector2.Max(max, regiao.vertices[i]);
        }
        float areaCaixaUv = (max.x - min.x) * (max.y - min.y);
        return visaoGlobal ? areaCaixaUv >= 0.045f : areaCaixaUv >= 0.008f;
    }

    private bool PodeDesenharRotuloSemSobrepor(ref Rect candidato)
    {
        candidato.x = Mathf.Clamp(candidato.x, 6f, Mathf.Max(6f, Screen.width - candidato.width - 6f));
        candidato.y = Mathf.Clamp(candidato.y, 44f, Mathf.Max(44f, Screen.height - candidato.height - 35f));
        for (int i = 0; i < _rotulosMapaDesenhados.Count; i++)
            if (_rotulosMapaDesenhados[i].Overlaps(candidato)) return false;
        _rotulosMapaDesenhados.Add(candidato);
        return true;
    }

    private void DesenharLinhaTracejada(Vector2 inicio, Vector2 fim, Color cor, float espessura, float tracado, float intervalo)
    {
        Vector2 delta = fim - inicio;
        float comprimento = delta.magnitude;
        if (comprimento < 0.1f) return;
        Vector2 direcao = delta / comprimento;
        float passo = Mathf.Max(1f, tracado + intervalo);
        for (float distancia = 0f; distancia < comprimento; distancia += passo)
        {
            Vector2 a = inicio + direcao * distancia;
            Vector2 b = inicio + direcao * Mathf.Min(distancia + tracado, comprimento);
            DesenharLinhaTela(a, b, cor, espessura);
        }
    }

    private void DesenharCamadasMapa()
    {
        Rect painel = new Rect(Screen.width - 212f, 44f, 200f, Screen.height - 84f);
        GUI.color = corPainelMapa;
        GUI.DrawTexture(painel, Texture2D.whiteTexture);
        GUI.color = new Color32(88, 152, 158, 220);
        GUI.DrawTexture(new Rect(painel.x, painel.y, 2f, painel.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(painel.x + 12f, painel.y + 10f, painel.width - 22f, painel.height - 18f));
        GUILayout.Label("CAMADAS", _camadaMapaStyle);
        camadaFronteiras = GUILayout.Toggle(camadaFronteiras, "Fronteiras políticas", _toggleCamadaStyle);
        camadaCidades = GUILayout.Toggle(camadaCidades, "Cidades", _toggleCamadaStyle);
        camadaBases = GUILayout.Toggle(camadaBases, "Bases", _toggleCamadaStyle);
        camadaPortos = GUILayout.Toggle(camadaPortos, "Portos", _toggleCamadaStyle);
        camadaAeroportos = GUILayout.Toggle(camadaAeroportos, "Aeroportos", _toggleCamadaStyle);
        camadaRadares = GUILayout.Toggle(camadaRadares, "Radares", _toggleCamadaStyle);
        camadaUnidades = GUILayout.Toggle(camadaUnidades, "Unidades", _toggleCamadaStyle);
        GUILayout.Space(8f);
        GUILayout.Label("CAMADAS ADICIONAIS", _estiloMiniMapa);
        camadaRecursos = GUILayout.Toggle(camadaRecursos, "Recursos", _toggleCamadaStyle);
        camadaEconomia = GUILayout.Toggle(camadaEconomia, "Economia", _toggleCamadaStyle);
        camadaPopulacao = GUILayout.Toggle(camadaPopulacao, "População", _toggleCamadaStyle);
        camadaInteligencia = GUILayout.Toggle(camadaInteligencia, "Inteligência", _toggleCamadaStyle);
        camadaLogistica = GUILayout.Toggle(camadaLogistica, "Logística", _toggleCamadaStyle);
        camadaAreasPatrulha = GUILayout.Toggle(camadaAreasPatrulha, "Áreas de patrulha", _toggleCamadaStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Contatos hostis seguem a inteligência disponível.", _estiloMiniMapa);
        GUILayout.EndArea();
    }

    private Color CorPoliticaDaRegiao(GerenteDeTerritorio gerente, RegiaoPolitica regiao)
    {
        ResultadoConsultaTerritorio estado = gerente != null
            ? gerente.ObterEstadoDaRegiao(regiao.territorioId)
            : new ResultadoConsultaTerritorio
            {
                ownerCountryTeamId = regiao.ownerCountryTeamId,
                neutral = regiao.neutral,
                tipo = regiao.tipo
            };
        int dono = estado.ownerCountryTeamId;
        if (dono <= 0)
        {
            if (estado.tipo == TipoRegiaoPolitica.AguasTerritoriais) return corBordaMar;
            return estado.neutral ? new Color32(208, 198, 169, 185) : new Color32(204, 211, 201, 185);
        }
        Color cor = CoresPoliticas[(dono - 1) % CoresPoliticas.Length];
        if (estado.tipo == TipoRegiaoPolitica.AguasTerritoriais) cor = Color.Lerp(cor, corBordaMar, 0.7f);
        return cor;
    }

    private string TextoDonoTerritorial(GerenteDeTerritorio gerente, RegiaoPolitica regiao)
    {
        ResultadoConsultaTerritorio estado = gerente != null
            ? gerente.ObterEstadoDaRegiao(regiao.territorioId)
            : new ResultadoConsultaTerritorio
            {
                ownerCountryTeamId = regiao.ownerCountryTeamId,
                neutral = regiao.neutral,
                tipo = regiao.tipo
            };
        int dono = estado.ownerCountryTeamId;
        if (estado.tipo == TipoRegiaoPolitica.AguasTerritoriais)
        {
            if (dono <= 0) return "ÁGUAS SEM DONO";
            SistemaGovernoMundial governoMar = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno paisMar = governoMar != null ? governoMar.ObterPais(dono) : null;
            return "ÁGUAS " + (paisMar != null ? paisMar.nomePais : "PAÍS " + dono);
        }
        if (estado.neutral && dono <= 0) return "NEUTRO";
        if (dono <= 0) return "SEM DONO";
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(dono) : null;
        return pais != null ? pais.nomePais : "PAÍS " + dono;
    }

    private DadosMapaTerritorial ObterDadosMapaCartografico(GerenteDeTerritorio gerente)
    {
        if (gerente != null && gerente.MapaPolitico != null) return gerente.MapaPolitico;
        if (!tentouCarregarDadosMapaCartografico)
        {
            dadosMapaCartograficoFallback = Resources.Load<DadosMapaTerritorial>(DadosMapaTerritorial.NomeResource);
            tentouCarregarDadosMapaCartografico = true;
        }
        return dadosMapaCartograficoFallback;
    }

    private Vector3 MapUvToWorldCartografico(GerenteDeTerritorio gerente, Vector2 uv)
    {
        if (gerente != null) return gerente.MapUvToWorld(uv);
        GlobalWorldDefinition world = ObterDefinicaoMapaGlobal();
        if (world != null) return world.MapUvToWorld(uv, nivelDoMar);
        if (!limitesMapaInicializados) AtualizarLimitesMapa();

        // Usa o footprint lógico do mundo para projetar o mesmo asset quando
        // o singleton territorial está ausente; margemMapa só é de navegação.
        float larguraBase = Mathf.Max(1f, larguraMapa - Mathf.Max(0f, margemMapa) * 2f);
        float profundidadeBase = Mathf.Max(1f, profundidadeMapa - Mathf.Max(0f, margemMapa) * 2f);
        return new Vector3(
            centroMapa.x - larguraBase * 0.5f + Mathf.Clamp01(uv.x) * larguraBase,
            nivelDoMar,
            centroMapa.y + profundidadeBase * 0.5f - Mathf.Clamp01(uv.y) * profundidadeBase);
    }

    private void DesenharLinhaTela(Vector2 inicio, Vector2 fim, Color cor, float espessura)
    {
        Vector2 delta = fim - inicio;
        float comprimento = delta.magnitude;
        if (comprimento < 0.1f) return;
        Matrix4x4 matriz = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, inicio);
        GUI.color = cor;
        GUI.DrawTexture(new Rect(inicio.x, inicio.y - espessura * 0.5f, comprimento, espessura), Texture2D.whiteTexture);
        GUI.matrix = matriz;
        GUI.color = Color.white;
    }

    private static Vector2 CentroideRegiao(IList<Vector2> vertices)
    {
        Vector2 centro = Vector2.zero;
        for (int i = 0; i < vertices.Count; i++) centro += vertices[i];
        return vertices.Count > 0 ? centro / vertices.Count : Vector2.zero;
    }

    private void DesenharRotasPatrulha()
    {
        if (Event.current.type != EventType.Repaint || cameraMapa == null) return;
        if (_desenharOrdens == null) _desenharOrdens = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        if (_desenharOrdens == null || _desenharOrdens.pontosPatrulha == null) return;
        List<Vector3> pontos = _desenharOrdens.pontosPatrulha;
        for (int i = 1; i < pontos.Count; i++)
        {
            Vector3 a = cameraMapa.WorldToScreenPoint(pontos[i - 1]);
            Vector3 b = cameraMapa.WorldToScreenPoint(pontos[i]);
            if (a.z <= 0f || b.z <= 0f) continue;
            DesenharLinhaTela(new Vector2(a.x, Screen.height - a.y),
                new Vector2(b.x, Screen.height - b.y), new Color32(212, 173, 103, 188), 1.4f);
        }
    }

    private void GarantirEstilosGui()
    {
        if (_tituloMapaStyle != null
            && _zoomMapaStyle != null
            && _legendaMapaStyle != null
            && _trianguloSombraStyle != null
            && _trianguloCorStyle != null
            && _camadaMapaStyle != null
            && _rotuloRegiaoStyle != null
            && _estiloMiniMapa != null
            && _toggleCamadaStyle != null) return;

        _tituloMapaStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = corTextoMapa }
        };
        _zoomMapaStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = corTextoMapa },
            hover = { textColor = Color.white },
            active = { textColor = Color.white }
        };
        _legendaMapaStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            normal = { textColor = new Color32(182, 199, 199, 245) }
        };
        _trianguloSombraStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.black }
        };
        _trianguloCorStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };
        _camadaMapaStyle = new GUIStyle(_legendaMapaStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 11,
            normal = { textColor = corTextoMapa }
        };
        _rotuloRegiaoStyle = new GUIStyle(_legendaMapaStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip,
            normal = { textColor = corTextoMapa }
        };
        _estiloMiniMapa = new GUIStyle(_legendaMapaStyle)
        {
            fontSize = 8,
            normal = { textColor = new Color32(137, 160, 164, 235) }
        };
        _toggleCamadaStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = 10,
            normal = { textColor = new Color32(187, 200, 197, 245) },
            onNormal = { textColor = corTextoMapa },
            hover = { textColor = Color.white },
            onHover = { textColor = Color.white }
        };
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

        GUI.color = new Color32(162, 83, 79, 38);
        GUI.DrawTexture(new Rect(minX, minY, maxX - minX, maxY - minY), Texture2D.whiteTexture);
        GUI.color = new Color32(185, 115, 107, 165);
        GUI.DrawTexture(new Rect(minX, minY, maxX - minX, 1.2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(minX, maxY - 1.2f, maxX - minX, 1.2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(minX, minY, 1.2f, maxY - minY), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(maxX - 1.2f, minY, 1.2f, maxY - minY), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DesenharIconesNoMapa()
    {
        if (cameraMapa == null) return;

        float zoom = cameraMapa.orthographicSize;
        bool visaoGlobal = zoom > 90000f;
        bool agrupar = zoom > 22000f;
        float tamanhoCelula = visaoGlobal ? 76f : 42f;
        _clustersUnidades.Clear();

        foreach (var id in _cacheUnidades)
        {
            if (id == null || id.gameObject == null) continue;

            bool ehAliado  = (id.teamID == meuTeamID);
            int instanceId = id.GetInstanceID();
            bool ehImovel = _imoveisMapa.Contains(instanceId);
            bool ehNeutro  = (id.teamID == 0);
            bool ehInimigo = (!ehAliado && !ehNeutro);

            // Estruturas inimigas também respeitam a neblina: ser imóvel não
            // concede conhecimento estratégico automático ao observador.
            if (!ehAliado && !ehNeutro && !RTSVisibilityService.TeamsAtWar(meuTeamID, id.teamID))
                continue;

            Vector3 posicaoMapa = id.transform.position;
            RTSVisibilityService visibilidade = RTSVisibilityService.Instancia;
            bool contatoAtual = !ehInimigo || (visibilidade != null
                && visibilidade.IsVisibleToTeam(meuTeamID, id));
            if (!ehAliado && !ehNeutro && !contatoAtual)
            {
                if (visibilidade == null
                    || !visibilidade.TryGetLastKnownPosition(meuTeamID, id, out posicaoMapa))
                {
                    continue;
                }
            }

            // Componentes móveis e NavMeshObstacle são classificados uma vez
            // por atualização do cache, não em cada repaint IMGUI.
            bool ehPredio = _prediosMapa.Contains(instanceId);
            if (!CamadaMostraEntidade(id, ehPredio)) continue;

            // Converte posição 3D para coordenadas da tela relativa à cameraMapa
            Vector3 screenPos = cameraMapa.WorldToScreenPoint(posicaoMapa);

            // Só mostra se estiver na frente da câmera (z > 0) e dentro da tela
            if (screenPos.z <= 0) continue;

            // Inverte Y (Unity GUI vs tela)
            float sx = screenPos.x;
            float sy = Screen.height - screenPos.y;

            if (sx < -20 || sx > Screen.width + 20 || sy < -20 || sy > Screen.height + 20) continue;

            Color cor = ehAliado
                ? (ehPredio ? corPredioProprio : corUnidadePropria)
                : (ehNeutro ? corUnidadeNeutro : (contatoAtual ? corInimigoAtual : corInimigoMemoria));
            int classe = ehAliado ? 0 : (ehNeutro ? 1 : (contatoAtual ? 2 : 3));
            int categoria = _categoriasMapa.TryGetValue(instanceId, out int categoriaCache) ? categoriaCache : 0;
            int tipo = ehPredio ? (0x80 | (categoria & 0x7f)) : (int)id.tipoUnidade;
            float angulo = 0f;
            if (!agrupar && _unidadesMoveisMapa.Contains(instanceId) && (ehAliado || ehNeutro || contatoAtual))
            {
                Vector3 frente = cameraMapa.WorldToScreenPoint(posicaoMapa + id.transform.forward * 10f);
                Vector2 direcaoTela = new Vector2(frente.x - screenPos.x, screenPos.y - frente.y);
                if (direcaoTela.sqrMagnitude > 0.01f)
                    angulo = Mathf.Atan2(direcaoTela.x, -direcaoTela.y) * Mathf.Rad2Deg;
            }
            if (agrupar)
            {
                int cellX = Mathf.FloorToInt(sx / tamanhoCelula);
                int cellY = Mathf.FloorToInt(sy / tamanhoCelula);
                long chave = ((long)(classe & 0xff) << 56)
                    | ((long)(tipo & 0xff) << 48)
                    | ((long)(cellX & 0xffffff) << 24)
                    | (uint)(cellY & 0xffffff);
                if (_clustersUnidades.TryGetValue(chave, out ClusterUnidadeMapa cluster))
                {
                    cluster.x += sx;
                    cluster.y += sy;
                    if (classe != 3 && tipo < 0x80)
                    {
                        Vector3 frente = cameraMapa.WorldToScreenPoint(posicaoMapa + id.transform.forward * 10f);
                        cluster.direcaoX += frente.x - screenPos.x;
                        cluster.direcaoY += screenPos.y - frente.y;
                    }
                    cluster.quantidade++;
                    _clustersUnidades[chave] = cluster;
                }
                else
                {
                    Vector3 frente = cameraMapa.WorldToScreenPoint(posicaoMapa + id.transform.forward * 10f);
                    _clustersUnidades[chave] = new ClusterUnidadeMapa
                    {
                        x = sx,
                        y = sy,
                        direcaoX = classe == 3 || ehPredio ? 0f : frente.x - screenPos.x,
                        direcaoY = classe == 3 || ehPredio ? 0f : screenPos.y - frente.y,
                        quantidade = 1
                    };
                }
            }
            else
            {
                DesenharIconeUnidade(sx, sy, zoom < 9000f ? 10f : 8f, tipo,
                    ehPredio ? TipoUnidade.Estrutura : id.tipoUnidade, cor, angulo, classe == 3);
            }
        }

        if (!agrupar) return;
        foreach (KeyValuePair<long, ClusterUnidadeMapa> item in _clustersUnidades)
        {
            long chave = item.Key;
            int classe = (int)((chave >> 56) & 0xff);
            int codigoSimbolo = (int)((chave >> 48) & 0xff);
            ClusterUnidadeMapa cluster = item.Value;
            float x = cluster.x / Mathf.Max(1, cluster.quantidade);
            float y = cluster.y / Mathf.Max(1, cluster.quantidade);
            bool ehPredio = (codigoSimbolo & 0x80) != 0;
            TipoUnidade tipo = ehPredio ? TipoUnidade.Estrutura : (TipoUnidade)codigoSimbolo;
            Color cor = classe == 0 ? (ehPredio ? corPredioProprio : corUnidadePropria)
                : (classe == 1 ? corUnidadeNeutro : (classe == 2 ? corInimigoAtual : corInimigoMemoria));
            float angulo = cluster.direcaoX * cluster.direcaoX + cluster.direcaoY * cluster.direcaoY > 0.01f
                ? Mathf.Atan2(cluster.direcaoX, -cluster.direcaoY) * Mathf.Rad2Deg
                : 0f;
            DesenharIconeUnidade(x, y, cluster.quantidade > 1 ? 7.5f : 6.5f,
                codigoSimbolo, tipo, cor, angulo, classe == 3);
            if (cluster.quantidade > 1)
            {
                GUI.color = corPainelMapa;
                GUI.DrawTexture(new Rect(x + 5f, y - 8f, 20f, 16f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 5f, y - 8f, 19f, 16f), cluster.quantidade.ToString(), _legendaMapaStyle);
            }
        }
    }

    private void DesenharIconeUnidade(float x, float y, float tamanho, int codigoSimbolo,
        TipoUnidade tipo, Color cor, float angulo, bool ultimaPosicaoConhecida)
    {
        if ((codigoSimbolo & 0x80) != 0 || tipo == TipoUnidade.Estrutura)
        {
            int categoria = codigoSimbolo & 0x7f;
            DesenharIconeEstrutura(x, y, tamanho, categoria, cor, ultimaPosicaoConhecida);
        }
        else if (tipo == TipoUnidade.Naval)
        {
            Vector2 proa = new Vector2(0f, -1f);
            Vector2 ombroE = new Vector2(-0.58f, -0.1f);
            Vector2 ombroD = new Vector2(0.58f, -0.1f);
            Vector2 popaE = new Vector2(-0.38f, 0.72f);
            Vector2 popaD = new Vector2(0.38f, 0.72f);
            DesenharLinhaIcone(x, y, tamanho, angulo, proa, ombroE, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, proa, ombroD, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, ombroE, popaE, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, ombroD, popaD, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, popaE, popaD, cor, ultimaPosicaoConhecida);
        }
        else if (tipo == TipoUnidade.Aereo)
        {
            DesenharLinhaIcone(x, y, tamanho, angulo, new Vector2(0f, 0.9f), new Vector2(0f, -1f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, new Vector2(-0.88f, 0.12f), new Vector2(0.88f, 0.12f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, new Vector2(-0.42f, 0.58f), new Vector2(0.42f, 0.58f), cor, ultimaPosicaoConhecida);
        }
        else
        {
            Vector2 topo = new Vector2(0f, -0.8f);
            Vector2 direita = new Vector2(0.8f, 0f);
            Vector2 baixo = new Vector2(0f, 0.8f);
            Vector2 esquerda = new Vector2(-0.8f, 0f);
            DesenharLinhaIcone(x, y, tamanho, angulo, topo, direita, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, direita, baixo, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, baixo, esquerda, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, esquerda, topo, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, angulo, new Vector2(-0.38f, 0f), new Vector2(0.38f, 0f), cor, ultimaPosicaoConhecida);
        }
    }

    private void DesenharIconeEstrutura(float x, float y, float tamanho, int categoria, Color cor, bool ultimaPosicaoConhecida)
    {
        if ((categoria & 32) != 0)
        {
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(0f, 0.6f), new Vector2(0f, -0.25f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.58f, -0.25f), new Vector2(0.58f, -0.25f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.36f, -0.62f), new Vector2(0.36f, -0.62f), cor, ultimaPosicaoConhecida);
        }
        else if ((categoria & 16) != 0)
        {
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(0f, -0.8f), new Vector2(0f, 0.8f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.55f, -0.35f), new Vector2(0.55f, -0.35f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.55f, 0.35f), new Vector2(0.55f, 0.35f), cor, ultimaPosicaoConhecida);
        }
        else if ((categoria & 8) != 0)
        {
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.7f, -0.45f), new Vector2(0.7f, -0.45f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(-0.45f, -0.45f), new Vector2(-0.45f, 0.65f), cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, new Vector2(0.45f, -0.45f), new Vector2(0.45f, 0.65f), cor, ultimaPosicaoConhecida);
        }
        else
        {
            Vector2 topo = new Vector2(0f, -0.7f);
            Vector2 direita = new Vector2(0.7f, 0f);
            Vector2 baixo = new Vector2(0f, 0.7f);
            Vector2 esquerda = new Vector2(-0.7f, 0f);
            DesenharLinhaIcone(x, y, tamanho, 0f, topo, direita, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, direita, baixo, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, baixo, esquerda, cor, ultimaPosicaoConhecida);
            DesenharLinhaIcone(x, y, tamanho, 0f, esquerda, topo, cor, ultimaPosicaoConhecida);
        }
    }

    private void DesenharLinhaIcone(float centroX, float centroY, float tamanho, float angulo,
        Vector2 inicioLocal, Vector2 fimLocal, Color cor, bool ultimaPosicaoConhecida)
    {
        float radians = angulo * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        Vector2 inicio = new Vector2(inicioLocal.x * cos - inicioLocal.y * sin,
            inicioLocal.x * sin + inicioLocal.y * cos) * tamanho + new Vector2(centroX, centroY);
        Vector2 fim = new Vector2(fimLocal.x * cos - fimLocal.y * sin,
            fimLocal.x * sin + fimLocal.y * cos) * tamanho + new Vector2(centroX, centroY);
        if (ultimaPosicaoConhecida) cor.a *= 0.58f;
        DesenharLinhaTela(inicio, fim, cor, Mathf.Max(1.2f, tamanho * 0.16f));
    }

    private static bool EhImovelMapa(GameObject objeto)
    {
        if (objeto == null) return false;
        if (objeto.GetComponent<Imovel>() != null
            || objeto.GetComponentInParent<Imovel>() != null
            || objeto.GetComponentInChildren<Imovel>(true) != null
            || objeto.GetComponent<Fazenda>() != null
            || objeto.GetComponentInParent<Fazenda>() != null
            || objeto.GetComponentInChildren<Fazenda>(true) != null
            || TagSafe.Matches(objeto, "Imovel"))
            return true;

        for (Transform atual = objeto.transform; atual != null; atual = atual.parent)
        {
            if (TagSafe.Matches(atual, "Imovel")) return true;
        }

        Transform[] filhos = objeto.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            if (TagSafe.Matches(filhos[i], "Imovel")) return true;
        }

        return false;
    }

    // Desenha um quadrado colorido
    void DesenharDisparosNoMapa()
    {
        if (cameraMapa == null || Event.current.type != EventType.Repaint) return;
        RTSVisibilityService visibilidade = RTSVisibilityService.Instancia;
        if (visibilidade == null) return;

        visibilidade.CopiarContatosDeMisseis(meuTeamID, _contatosMisseis);
        MissileThreatTracker.CopiarAmeacasAtivas(_misseisAtivos);
        for (int i = 0; i < _contatosMisseis.Count; i++)
        {
            RTSMissileVisibilityContact contato = _contatosMisseis[i];
            if (contato == null) continue;

            MissileThreatTracker tracker = EncontrarMissilAtivo(contato.missileId);
            bool equipeConhecidaAliada = contato.sourceTeamId == meuTeamID;
            bool aoVivoConhecido = tracker != null && tracker.RaizMissil != null
                && (equipeConhecidaAliada || contato.currentlyVisible);
            Vector3 posicao = aoVivoConhecido ? tracker.RaizMissil.position : contato.lastKnownPosition;
            Vector3 velocidade = aoVivoConhecido ? tracker.ObterVelocidadeAtual() : contato.lastKnownDirection * contato.lastKnownSpeed;
            Vector3 direcaoPlano = velocidade;
            direcaoPlano.y = 0f;
            DesenharHistoricoMissil(contato, posicao, aoVivoConhecido);
            DesenharMarcadorMissil(posicao, direcaoPlano, contato.sourceTeamId, !contato.currentlyVisible && !equipeConhecidaAliada,
                contato.missileId == _missilSelecionadoId, contato.lastSeenAt);
        }
    }

    private MissileThreatTracker EncontrarMissilAtivo(int missileId)
    {
        for (int i = 0; i < _misseisAtivos.Count; i++)
        {
            MissileThreatTracker tracker = _misseisAtivos[i];
            if (tracker == null) continue;
            int id = tracker.MissileId >= 0 ? tracker.MissileId : tracker.GetInstanceID();
            if (id == missileId) return tracker;
        }
        return null;
    }

    private void DesenharHistoricoMissil(RTSMissileVisibilityContact contato, Vector3 posicaoAtual, bool contatoAtual)
    {
        if (contato == null || contato.trajectory == null || contato.trajectory.Count < 2) return;
        Color cor = CorDoDisparo(contato.sourceTeamId);
        if (!contatoAtual && contato.sourceTeamId != meuTeamID) cor.a = 0.42f;
        Vector3 anterior = Vector3.zero;
        bool anteriorVisivel = false;
        for (int i = 0; i < contato.trajectory.Count; i++)
        {
            Vector3 tela3D = cameraMapa.WorldToScreenPoint(contato.trajectory[i]);
            if (tela3D.z <= 0f) { anteriorVisivel = false; continue; }
            Vector2 tela = new Vector2(tela3D.x, Screen.height - tela3D.y);
            if (anteriorVisivel)
            {
                if (!contatoAtual && contato.sourceTeamId != meuTeamID)
                    DesenharLinhaTracejada(anterior, tela, cor, 1.2f, 6f, 5f);
                else
                    DesenharLinhaTela(anterior, tela, cor, 1.4f);
            }
            anterior = tela;
            anteriorVisivel = true;
        }

        if (contatoAtual && anteriorVisivel)
        {
            Vector3 atual3D = cameraMapa.WorldToScreenPoint(posicaoAtual);
            if (atual3D.z > 0f)
                DesenharLinhaTela(anterior, new Vector2(atual3D.x, Screen.height - atual3D.y), cor, 1.4f);
        }
    }

    private void DesenharMarcadorMissil(Vector3 posicao, Vector3 direcao, int equipeOrigem,
        bool ultimoConhecido, bool selecionado, float ultimoContatoEm)
    {
        Vector3 tela = cameraMapa.WorldToScreenPoint(posicao);
        if (tela.z <= 0f) return;
        float sx = tela.x;
        float sy = Screen.height - tela.y;
        if (sx < -20f || sx > Screen.width + 20f || sy < -20f || sy > Screen.height + 20f) return;

        Color cor = CorDoDisparo(equipeOrigem);
        if (equipeOrigem <= 0) cor = new Color32(224, 184, 104, 255);
        if (ultimoConhecido) cor.a *= 0.58f;
        float alcanceRumo = Mathf.Clamp(cameraMapa.orthographicSize * 0.05f, 20f, 90f);
        Vector3 plano = direcao;
        plano.y = 0f;
        float angulo = 0f;
        if (plano.sqrMagnitude > 0.001f)
        {
            Vector3 frente = cameraMapa.WorldToScreenPoint(posicao + plano.normalized * alcanceRumo);
            if (frente.z > 0f)
            {
                DesenharLinhaTela(new Vector2(sx, sy), new Vector2(frente.x, Screen.height - frente.y), cor, selecionado ? 3f : 2f);
                Vector2 direcaoTela = new Vector2(frente.x - tela.x, tela.y - frente.y);
                if (direcaoTela.sqrMagnitude > 0.01f)
                    angulo = Mathf.Atan2(direcaoTela.x, -direcaoTela.y) * Mathf.Rad2Deg;
            }
        }
        DesenharIcone(sx, sy, selecionado ? 11f : 8f, cor, angulo);
        if (selecionado)
        {
            GUI.color = cor;
            float idade = Mathf.Max(0f, Time.unscaledTime - ultimoContatoEm);
            string estado = ultimoConhecido
                ? "ÚLTIMA POSIÇÃO  ·  " + idade.ToString("0") + " s"
                : "CONTATO ATUAL";
            GUI.Label(new Rect(sx + 11f, sy - 18f, 190f, 22f), "MÍSSIL  /  " + estado, _legendaMapaStyle);
            GUI.color = Color.white;
        }
    }

    private bool SelecionarMissilSobCursor()
    {
        RTSVisibilityService visibilidade = RTSVisibilityService.Instancia;
        if (visibilidade == null || cameraMapa == null) return false;
        visibilidade.CopiarContatosDeMisseis(meuTeamID, _contatosMisseis);
        MissileThreatTracker.CopiarAmeacasAtivas(_misseisAtivos);
        float menorDistancia = 16f;
        RTSMissileVisibilityContact escolhido = null;
        for (int i = 0; i < _contatosMisseis.Count; i++)
        {
            RTSMissileVisibilityContact contato = _contatosMisseis[i];
            if (contato == null) continue;
            MissileThreatTracker tracker = EncontrarMissilAtivo(contato.missileId);
            bool aliado = contato.sourceTeamId == meuTeamID;
            bool contatoAtual = tracker != null && tracker.RaizMissil != null
                && (aliado || contato.currentlyVisible);
            Vector3 posicao = contatoAtual ? tracker.RaizMissil.position : contato.lastKnownPosition;
            Vector3 tela = cameraMapa.WorldToScreenPoint(posicao);
            if (tela.z <= 0f) continue;
            float distancia = Vector2.Distance(Input.mousePosition, new Vector2(tela.x, tela.y));
            if (distancia >= menorDistancia) continue;
            menorDistancia = distancia;
            escolhido = contato;
        }
        if (escolhido == null) return false;

        _missilSelecionadoId = escolhido.missileId;
        IniciarCameraMissilSeObservado(escolhido);
        return true;
    }

    private void IniciarCameraMissilSeObservado(RTSMissileVisibilityContact contato)
    {
        if (contato == null) return;
        MissileThreatTracker tracker = EncontrarMissilAtivo(contato.missileId);
        bool podeVerTransformReal = tracker != null && tracker.RaizMissil != null
            && (contato.sourceTeamId == meuTeamID || contato.currentlyVisible);
        if (!podeVerTransformReal)
        {
            PararCameraDeRastreamentoMissil();
            return;
        }

        if (_cameraRastreamentoMissil == null)
            _cameraRastreamentoMissil = GetComponent<CartaTerrenoRenderer>() ?? gameObject.AddComponent<CartaTerrenoRenderer>();

        Transform raiz = tracker.RaizMissil;
        if (!_cameraMissilEmRastreamento || _cameraMissilInstanceId != raiz.GetInstanceID())
        {
            _cameraRastreamentoMissil.IniciarRastreamento(raiz);
            _cameraMissilEmRastreamento = true;
            _cameraMissilInstanceId = raiz.GetInstanceID();
        }
    }

    private void PararCameraDeRastreamentoMissil()
    {
        if (_cameraRastreamentoMissil != null)
        {
            _cameraRastreamentoMissil.PararRastreamento();
            _cameraRastreamentoMissil.DefinirAtualizacaoContinua(false);
        }
        _cameraMissilEmRastreamento = false;
        _cameraMissilInstanceId = 0;
    }

    private void DesenharPainelRastreamentoMissil()
    {
        if (!mapaAtivo || _missilSelecionadoId < 0) return;
        RTSVisibilityService visibilidade = RTSVisibilityService.Instancia;
        if (visibilidade == null) return;
        visibilidade.CopiarContatosDeMisseis(meuTeamID, _contatosMisseis);
        RTSMissileVisibilityContact contato = null;
        for (int i = 0; i < _contatosMisseis.Count; i++)
            if (_contatosMisseis[i] != null && _contatosMisseis[i].missileId == _missilSelecionadoId) { contato = _contatosMisseis[i]; break; }
        if (contato == null)
        {
            _missilSelecionadoId = -1;
            PararCameraDeRastreamentoMissil();
            return;
        }

        IniciarCameraMissilSeObservado(contato);
        MissileThreatTracker tracker = EncontrarMissilAtivo(contato.missileId);
        bool vivoConhecido = tracker != null && tracker.RaizMissil != null
            && (contato.sourceTeamId == meuTeamID || contato.currentlyVisible);
        Vector3 posicao = vivoConhecido ? tracker.RaizMissil.position : contato.lastKnownPosition;
        Texture camera = null;
        if (vivoConhecido && _cameraRastreamentoMissil == null)
            _cameraRastreamentoMissil = GetComponent<CartaTerrenoRenderer>() ?? gameObject.AddComponent<CartaTerrenoRenderer>();
        if (vivoConhecido && _cameraRastreamentoMissil != null && Event.current.type == EventType.Repaint)
            camera = _cameraRastreamentoMissil.Renderizar(posicao, 360f, 16f / 9f, true);

        Rect painel = new Rect(Screen.width - 382f, Screen.height - 266f, 370f, 250f);
        GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.96f);
        GUI.DrawTexture(painel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (camera != null) GUI.DrawTexture(new Rect(painel.x + 8f, painel.y + 28f, 354f, 199f), camera, ScaleMode.ScaleToFit, false);
        else
        {
            GUI.color = new Color(0.08f, 0.11f, 0.13f, 1f);
            GUI.DrawTexture(new Rect(painel.x + 8f, painel.y + 28f, 354f, 199f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(painel.x + 20f, painel.y + 110f, 330f, 40f), "SEM CONTATO VISUAL\nÚltima posição observada", _tituloMapaStyle);
        }
        GUI.Label(new Rect(painel.x + 10f, painel.y + 5f, 270f, 20f), "ACOMPANHAMENTO — MÍSSIL " + contato.missileId, _camadaMapaStyle);
        if (GUI.Button(new Rect(painel.xMax - 28f, painel.y + 3f, 24f, 22f), "×"))
        {
            _missilSelecionadoId = -1;
            PararCameraDeRastreamentoMissil();
            return;
        }
        string estado = vivoConhecido ? "AO VIVO" : "ÚLTIMA POSIÇÃO CONHECIDA";
        string telemetria = estado + "  |  " + contato.lastKnownSpeed.ToString("0") + " m/s  |  ALT " + contato.lastKnownPosition.y.ToString("0") + " m";
        if (contato.targetKnown)
        {
            string alvo = string.IsNullOrWhiteSpace(contato.knownTargetName) ? "COORDENADA CONHECIDA" : contato.knownTargetName;
            telemetria += "  |  ALVO " + alvo + " (" + contato.knownTargetPosition.x.ToString("0") + ", " + contato.knownTargetPosition.z.ToString("0") + ")";
        }
        GUI.Label(new Rect(painel.x + 10f, painel.yMax - 24f, 350f, 18f), telemetria, _legendaMapaStyle);
    }

    Color CorDoDisparo(int team)
    {
        if (team == meuTeamID) return corUnidadePropria;
        if (team > 0) return corInimigoAtual;
        return new Color32(224, 184, 104, 255);
    }

    void DesenharIcone(float cx, float cy, float tamanho, Color cor, float angulo)
    {
        Vector2 topo = new Vector2(0f, -1f);
        Vector2 direita = new Vector2(0.68f, 0f);
        Vector2 baixo = new Vector2(0f, 1f);
        Vector2 esquerda = new Vector2(-0.68f, 0f);
        DesenharLinhaIcone(cx, cy, tamanho, angulo, topo, direita, cor, false);
        DesenharLinhaIcone(cx, cy, tamanho, angulo, direita, baixo, cor, false);
        DesenharLinhaIcone(cx, cy, tamanho, angulo, baixo, esquerda, cor, false);
        DesenharLinhaIcone(cx, cy, tamanho, angulo, esquerda, topo, cor, false);
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
