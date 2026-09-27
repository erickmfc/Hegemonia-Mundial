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
    private readonly Color corFundoMar      = new Color(0.10f, 0.25f, 0.55f, 1f);   // Azul oceano profundo
    private readonly Color corBordaMar      = new Color(0.18f, 0.40f, 0.70f, 1f);   // Borda da água
    private readonly Color corPredioProprio = new Color(0.90f, 0.90f, 0.90f, 1f);   // Branco
    private readonly Color corUnidadePropria= new Color(0.25f, 1.00f, 0.25f, 1f);   // Verde claro
    private readonly Color corUnidadeNeutro = new Color(0.70f, 0.70f, 0.70f, 1f);   // Cinza

    // Cache de objetos do mundo para não chamar Find() o tempo todo
    private List<IdentidadeUnidade> _cacheUnidades = new List<IdentidadeUnidade>();
    private readonly HashSet<int> _imoveisMapa = new HashSet<int>();
    private readonly Dictionary<int, int> _categoriasMapa = new Dictionary<int, int>(256);
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
        return yTopo <= 34f
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
        _categoriasMapa.Clear();
        if (_desenharOrdens == null) _desenharOrdens = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        var todos = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todos)
        {
            if (u == null) continue;
            _cacheUnidades.Add(u);
            if (EhImovelMapa(u.gameObject))
            {
                _imoveisMapa.Add(u.GetInstanceID());
                _categoriasMapa[u.GetInstanceID()] = ClassificarCategoriaMapa(u);
            }
            else _categoriasMapa[u.GetInstanceID()] = 64; // unidade móvel
        }
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

        if (mapaCartograficoAtivo)
        {
            DesenharFundoCartografico();
            if (camadaFronteiras) DesenharFronteirasPoliticas();
            if (camadaAreasPatrulha) DesenharRotasPatrulha();
        }

        // --- Barra superior com info e botão de fechar ---
        float barH = 30f;
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = _tituloMapaStyle;
        string modoSeguir = _seguindoAlvo && _alvoSeguir != null
            ? $"[F seguir: {_alvoSeguir.name.ToUpper()}]"
            : "[F seguir unidade]";
        string titulo = mapaCartograficoAtivo ? "MAPA TÁTICO 2D" : "CÂMERA SUPERIOR (DEBUG)";
        GUI.Label(new Rect(8, 0, Screen.width - 300f, barH),
            $"{titulo}  [WASD mover] [Scroll zoom] [{modoSeguir}] [M fechar]", titleStyle);

        GUIStyle zoomStyle = _zoomMapaStyle;
        if (GUI.Button(new Rect(Screen.width - 250f, 3f, 116f, 24f), mapaCartograficoAtivo ? "Câmera 3D" : "Mapa 2D", zoomStyle))
        {
            mapaCartograficoAtivo = !mapaCartograficoAtivo;
            AtualizarRenderizacaoCameraMapa();
        }
        if (GUI.Button(new Rect(Screen.width - 118f, 3f, 34f, 24f), "+", zoomStyle)) AjustarZoomMapa(-1f);
        if (GUI.Button(new Rect(Screen.width - 78f, 3f, 34f, 24f), "−", zoomStyle)) AjustarZoomMapa(1f);

        GUIStyle legStyle = _legendaMapaStyle;
        if (mapaCartograficoAtivo)
        {
            GUI.Label(new Rect(12f, Screen.height - 28f, Screen.width - 250f, 22f),
                "A base colorida mostra geografia; soberania, dono e neutralidade vêm dos polígonos territoriais.", legStyle);
        }
        else
        {
            // --- Legenda do modo de câmera superior ---
            float legX = 12f, legY = Screen.height - 100f;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(legX - 6, legY - 6, 175f, 90f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(legX, legY,      170, 20), "■  Prédio Aliado",   legStyle);
            GUI.Label(new Rect(legX, legY + 22, 170, 20), "▲  Unidade Aliada",  legStyle);
            GUI.Label(new Rect(legX, legY + 44, 170, 20), "●  Unidade Neutra",  legStyle);
            GUI.Label(new Rect(legX, legY + 66, 170, 20), "🔵  Oceano",          legStyle);
            DesenharTerritorioInimigo();
        }

        // Os mesmos registros de entidades e visibilidade alimentam os dois modos.
        DesenharIconesNoMapa();
        DesenharDisparosNoMapa();
        if (mapaCartograficoAtivo) DesenharCamadasMapa();
        if (mapaCartograficoAtivo)
            GUI.Label(new Rect(12f, barH + 7f, 440f, 22f), "MÍSSEIS: ciano aliado | vermelho detectado | dourado sem IFF", legStyle);
        else
            GUI.Label(new Rect(Screen.width - 420f, barH + 8f, 405f, 22f), "MÍSSEIS: ciano aliado | vermelho detectado | dourado sem IFF", legStyle);
        DesenharPainelRastreamentoMissil();
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
        GUI.DrawTexture(new Rect(x, y, largura, altura), mapa, ScaleMode.StretchToFill, false);
    }

    private void DesenharFronteirasPoliticas()
    {
        if (Event.current.type != EventType.Repaint || cameraMapa == null) return;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        DadosMapaTerritorial dados = ObterDadosMapaCartografico(gerente);
        if (dados == null) return;

        for (int i = 0; i < dados.Regioes.Count; i++)
        {
            RegiaoPolitica regiao = dados.Regioes[i];
            if (regiao == null || !regiao.PossuiPoligono) continue;
            Color cor = CorPoliticaDaRegiao(gerente, regiao);
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
                if (anteriorVisivel) DesenharLinhaTela(anterior, atual, cor, regiao.tipo == TipoRegiaoPolitica.Terra ? 2.5f : 1.8f);
                anterior = atual;
                anteriorVisivel = true;
            }

            if (regiao.tipo == TipoRegiaoPolitica.Terra)
            {
                Vector2 centro = CentroideRegiao(regiao.vertices);
                Vector3 telaCentro = cameraMapa.WorldToScreenPoint(MapUvToWorldCartografico(gerente, centro));
                if (telaCentro.z > 0f)
                {
                    string dono = TextoDonoTerritorial(gerente, regiao);
                    Rect rotulo = new Rect(telaCentro.x - 100f, Screen.height - telaCentro.y - 10f, 200f, 20f);
                    GUI.color = new Color(0.03f, 0.07f, 0.09f, 0.82f);
                    GUI.DrawTexture(rotulo, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(rotulo, regiao.nome + "  " + dono, _camadaMapaStyle);
                }
            }
        }
    }

    private void DesenharCamadasMapa()
    {
        Rect painel = new Rect(Screen.width - 204f, 34f, 202f, Screen.height - 67f);
        GUI.color = new Color(0.025f, 0.045f, 0.07f, 0.94f);
        GUI.DrawTexture(painel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(painel.x + 10f, painel.y + 10f, painel.width - 20f, painel.height - 18f));
        GUILayout.Label("CAMADAS", _camadaMapaStyle);
        camadaFronteiras = GUILayout.Toggle(camadaFronteiras, "Fronteiras políticas");
        camadaCidades = GUILayout.Toggle(camadaCidades, "Cidades");
        camadaBases = GUILayout.Toggle(camadaBases, "Bases");
        camadaPortos = GUILayout.Toggle(camadaPortos, "Portos");
        camadaAeroportos = GUILayout.Toggle(camadaAeroportos, "Aeroportos");
        camadaRadares = GUILayout.Toggle(camadaRadares, "Radares");
        camadaUnidades = GUILayout.Toggle(camadaUnidades, "Unidades");
        GUILayout.Space(8f);
        GUILayout.Label("DADOS NÃO CONECTADOS", EditorStyleMini());
        camadaRecursos = GUILayout.Toggle(camadaRecursos, "Recursos");
        camadaEconomia = GUILayout.Toggle(camadaEconomia, "Economia");
        camadaPopulacao = GUILayout.Toggle(camadaPopulacao, "População");
        camadaInteligencia = GUILayout.Toggle(camadaInteligencia, "Inteligência");
        camadaLogistica = GUILayout.Toggle(camadaLogistica, "Logística");
        camadaAreasPatrulha = GUILayout.Toggle(camadaAreasPatrulha, "Áreas de patrulha");
        GUILayout.FlexibleSpace();
        GUILayout.Label("Ícones inimigos respeitam a inteligência disponível.", _legendaMapaStyle);
        GUILayout.EndArea();
    }

    private static GUIStyle EditorStyleMini()
    {
        return new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0.65f, 0.72f, 0.8f) } };
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
            if (estado.tipo == TipoRegiaoPolitica.AguasTerritoriais) return new Color(1f, 0.34f, 0.70f, 0.95f);
            return estado.neutral ? corUnidadeNeutro : Color.white;
        }
        Color[] cores = { new Color(0.2f, 0.9f, 0.5f), new Color(0.2f, 0.75f, 1f), new Color(1f, 0.58f, 0.2f), new Color(1f, 0.3f, 0.35f), new Color(0.76f, 0.55f, 1f) };
        Color cor = cores[(dono - 1) % cores.Length];
        if (estado.tipo == TipoRegiaoPolitica.AguasTerritoriais) cor = Color.Lerp(cor, new Color(0.2f, 0.55f, 0.95f), 0.38f);
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
                new Vector2(b.x, Screen.height - b.y), new Color(1f, 0.84f, 0.2f, 0.9f), 2.2f);
        }
    }

    private void GarantirEstilosGui()
    {
        if (_tituloMapaStyle != null
            && _zoomMapaStyle != null
            && _legendaMapaStyle != null
            && _trianguloSombraStyle != null
            && _trianguloCorStyle != null
            && _camadaMapaStyle != null) return;

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
        _camadaMapaStyle = new GUIStyle(_legendaMapaStyle) { fontStyle = FontStyle.Bold, fontSize = 13 };
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

        bool agrupar = cameraMapa.orthographicSize > 120f;
        float tamanhoCelula = cameraMapa.orthographicSize > 350f ? 48f : 30f;
        _clustersUnidades.Clear();

        foreach (var id in _cacheUnidades)
        {
            if (id == null || id.gameObject == null) continue;

            bool ehAliado  = (id.teamID == meuTeamID);
            bool ehImovel = _imoveisMapa.Contains(id.GetInstanceID());
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

            // Aeronaves e alguns navios não usam Rigidbody/NavMeshAgent. A
            // ausência desses componentes não os torna prédios no mapa.
            bool temControladorDeUnidade = id.GetComponent<ControleUnidade>() != null
                || id.GetComponent<ControleAviao>() != null
                || id.GetComponent<ControleAviaoCaca>() != null
                || id.GetComponent<Helicoptero>() != null
                || id.GetComponent<VooHelicoptero>() != null
                || id.GetComponent<ControleNavioRealista>() != null
                || id.GetComponent<ControleSubmarino>() != null
                || id.GetComponent<IdentidadeNaval>() != null
                || id.GetComponent<C700TransporteAereo>() != null;
            bool ehPredio = id.tipoUnidade == TipoUnidade.Estrutura
                || (!temControladorDeUnidade && id.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null);
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
                : (ehNeutro ? corUnidadeNeutro
                    : (contatoAtual ? new Color(1f, 0.2f, 0.12f, 1f) : new Color(1f, 0.35f, 0.18f, 0.45f)));
            int classe = ehAliado ? 0 : (ehNeutro ? 1 : (contatoAtual ? 2 : 3));
            int tipo = ehPredio ? (int)TipoUnidade.Estrutura : (int)id.tipoUnidade;
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
                    cluster.quantidade++;
                    _clustersUnidades[chave] = cluster;
                }
                else
                {
                    _clustersUnidades[chave] = new ClusterUnidadeMapa { x = sx, y = sy, quantidade = 1 };
                }
            }
            else
            {
                DesenharIconeUnidade(sx, sy, ehPredio ? 10f : 8f, (TipoUnidade)tipo, cor);
            }
        }

        if (!agrupar) return;
        foreach (KeyValuePair<long, ClusterUnidadeMapa> item in _clustersUnidades)
        {
            long chave = item.Key;
            int classe = (int)((chave >> 56) & 0xff);
            TipoUnidade tipo = (TipoUnidade)((chave >> 48) & 0xff);
            ClusterUnidadeMapa cluster = item.Value;
            float x = cluster.x / Mathf.Max(1, cluster.quantidade);
            float y = cluster.y / Mathf.Max(1, cluster.quantidade);
            Color cor = classe == 0 ? (tipo == TipoUnidade.Estrutura ? corPredioProprio : corUnidadePropria)
                : (classe == 1 ? corUnidadeNeutro
                    : (classe == 2 ? new Color(1f, 0.2f, 0.12f, 1f) : new Color(1f, 0.35f, 0.18f, 0.45f)));
            DesenharIconeUnidade(x, y, cluster.quantidade > 1 ? 9f : 7f, tipo, cor);
            if (cluster.quantidade > 1)
            {
                GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.92f);
                GUI.DrawTexture(new Rect(x + 5f, y - 8f, 19f, 16f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 5f, y - 8f, 19f, 16f), cluster.quantidade.ToString(), _legendaMapaStyle);
            }
        }
    }

    private void DesenharIconeUnidade(float x, float y, float tamanho, TipoUnidade tipo, Color cor)
    {
        if (tipo == TipoUnidade.Estrutura)
        {
            DesenharIcone(x, y, tamanho + 2f, tamanho + 2f, cor);
        }
        else if (tipo == TipoUnidade.Naval)
        {
            DesenharSimbolo(x, y, "◆", tamanho * 2.2f, cor);
        }
        else if (tipo == TipoUnidade.Aereo)
        {
            DesenharSimbolo(x, y, "▲", tamanho * 2.1f, cor);
        }
        else
        {
            DesenharSimbolo(x, y, "●", tamanho * 1.8f, cor);
        }
    }

    private void DesenharSimbolo(float x, float y, string simbolo, float tamanho, Color cor)
    {
        GUIStyle estilo = _trianguloCorStyle ?? GUI.skin.label;
        estilo.alignment = TextAnchor.MiddleCenter;
        estilo.fontSize = Mathf.Max(10, Mathf.RoundToInt(tamanho));
        estilo.normal.textColor = cor;
        GUI.Label(new Rect(x - tamanho, y - tamanho, tamanho * 2f, tamanho * 2f), simbolo, estilo);
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
                contato.missileId == _missilSelecionadoId);
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
            if (anteriorVisivel) DesenharLinhaTela(anterior, tela, cor, 2f);
            anterior = tela;
            anteriorVisivel = true;
        }

        if (contatoAtual && anteriorVisivel)
        {
            Vector3 atual3D = cameraMapa.WorldToScreenPoint(posicaoAtual);
            if (atual3D.z > 0f)
                DesenharLinhaTela(anterior, new Vector2(atual3D.x, Screen.height - atual3D.y), cor, 2f);
        }
    }

    private void DesenharMarcadorMissil(Vector3 posicao, Vector3 direcao, int equipeOrigem, bool ultimoConhecido, bool selecionado)
    {
        Vector3 tela = cameraMapa.WorldToScreenPoint(posicao);
        if (tela.z <= 0f) return;
        float sx = tela.x;
        float sy = Screen.height - tela.y;
        if (sx < -20f || sx > Screen.width + 20f || sy < -20f || sy > Screen.height + 20f) return;

        Color cor = CorDoDisparo(equipeOrigem);
        if (equipeOrigem <= 0) cor = new Color(1f, 0.78f, 0.2f, 1f);
        if (ultimoConhecido) cor.a = 0.5f;
        float alcanceRumo = Mathf.Clamp(cameraMapa.orthographicSize * 0.05f, 20f, 90f);
        Vector3 plano = direcao;
        plano.y = 0f;
        if (plano.sqrMagnitude > 0.001f)
        {
            Vector3 frente = cameraMapa.WorldToScreenPoint(posicao + plano.normalized * alcanceRumo);
            if (frente.z > 0f)
                DesenharLinhaTela(new Vector2(sx, sy), new Vector2(frente.x, Screen.height - frente.y), cor, selecionado ? 3f : 2f);
        }
        DesenharIcone(sx, sy, selecionado ? 14f : 9f, selecionado ? 14f : 9f, cor);
        if (selecionado)
        {
            GUI.color = cor;
            GUI.Label(new Rect(sx + 10f, sy - 18f, 160f, 22f), ultimoConhecido ? "MÍSSIL — ÚLTIMA POSIÇÃO" : "MÍSSIL DETECTADO", _legendaMapaStyle);
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
