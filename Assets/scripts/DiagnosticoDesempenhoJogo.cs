using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class DiagnosticoDesempenhoJogo : MonoBehaviour
{
    private struct EventoRuntime
    {
        public float Tempo;
        public string Categoria;
        public string Descricao;
    }

    private struct ResumoSegundo
    {
        public string Cena;
        public string Dificuldade;
        public float Inicio;
        public float Fim;
        public bool EmWarmup;
        public float FpsMedio;
        public float FpsMinimo;
        public float FpsMaximo;
        public float FrameMsMedio;
        public float PiorFrameMs;
        public int FramesLentos;
        public int Travadas;
        public float CpuMainMs;
        public float CpuRenderMs;
        public float GpuMs;
        public int GcGen0;
        public int GcGen1;
        public int GcGen2;
        public float MemoriaGerenciadaMb;
        public float DeltaMemoriaGerenciadaMb;
        public float MemoriaAlocadaMb;
        public float MemoriaReservadaMb;
        public float UiRebuildMs;
        public float ZonePlannerMs;
        public float PressaoCpuPercentual;
        public float PressaoGpuPercentual;
        public float FolgaGpuPercentual;
        public float CoastScanMs;
        public float NavalCandidateMs;
        public float WorldRefreshMs;
        public float VisibleEnemyMs;
        public float ProductionMs;
        public float BuildExecuteMs;
        public float ProduceExecuteMs;
        public float SpawnStructureMs;
        public float SpawnLandMs;
        public float SpawnNavalMs;
        public float SpawnAirMs;
        public float NavmeshSpawnMs;
        public float PrefabInitMs;
        public float AirUnitUpdateMs;
        public float NavalUnitUpdateMs;
        public float LandUnitUpdateMs;
        public float SensorUpdateMs;
        public float TargetingMs;
        public float PathfindingMs;
        public float WeaponUpdateMs;
        public float FormationUpdateMs;
        public float ConstructorPreviewMs;
        public float ConstructorConfirmMs;
        public float NavalPreviewMs;
        public float NavalCommitMs;
        public int PreviewOverflowCount;
        public int OrdersEmitted;
        public int PoolHits;
        public int PoolMisses;
        public int SpawnRegistrations;
        public int EngagedUnits;
        public int SupportUnits;
        public int ReserveUnits;
        public int TransportCapacityReady;
        public int ActiveAirWings;
        public int ActiveNavalTaskforces;
        public int ActiveLandFronts;
        public string NavalAutoDisabledReason;
        public string SpawnPrefabName;
        public string GovernorBand;
        public string InputOwner;
        public string InputLockReason;
        public string TopOffenders;
        public string CausaProvavel;
        public string Detalhes;
    }

    private static DiagnosticoDesempenhoJogo _instancia;
    private static float _runtimeOverloadUntil;
    private static float _runtimeSevereUntil;
    private static int _runtimePressureConsecutiveSeconds;
    private static string _runtimeLockReason = string.Empty;

    [Header("Captura")]
    [SerializeField] private int fpsAlvo = 60;
    [SerializeField] private float intervaloAmostragemSegundos = 1f;
    [SerializeField] private float limiteTravamentoMs = 50f;
    [SerializeField] [Range(0.5f, 1f)] private float percentualFrameLento = 0.9f;
    [SerializeField] private float warmupInicialSegundos = 20f;

    [Header("Saida")]
    [SerializeField] private bool exibirOverlay = true;
    [SerializeField] private bool gravarCsv = false;
    [SerializeField] private bool persistirEntreCenas = true;
    [SerializeField] private KeyCode teclaAlternarOverlay = KeyCode.F8;
    [SerializeField] private KeyCode teclaAlternarCaptura = KeyCode.F9;
    [SerializeField] private bool capturaAtivaNoInicio = true;
    [SerializeField] private bool ativarAutomaticamenteQuandoPresenteNaCena = true;
    [SerializeField] private bool mostrarOverlayNaAtivacaoAutomatica = true;

    [Header("Eventos")]
    [SerializeField] private bool registrarCarregamentoDeCena = true;
    [SerializeField] private int maxEventosEmMemoria = 512;
    [SerializeField] private int maxEventosNoOverlay = 4;

    private readonly List<EventoRuntime> _eventos = new List<EventoRuntime>(128);
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
    private readonly Dictionary<string, float> _metricasTempoAcumuladas = new Dictionary<string, float>(16);
    private readonly Dictionary<string, int> _metricasContagem = new Dictionary<string, int>(16);
    private readonly Dictionary<string, string> _metricasTexto = new Dictionary<string, string>(8);
    private readonly StringBuilder _csvBuilder = new StringBuilder(1024);

    private StreamWriter _csvWriter;
    private string _csvPath = string.Empty;
    private float _tempoInicioCaptura;
    private float _tempoNoSegundo;
    private float _inicioSegundoAtual;
    private int _framesNoSegundo;
    private int _framesLentos;
    private int _travadasNoSegundo;
    private float _somaFps;
    private float _fpsMinimo;
    private float _fpsMaximo;
    private float _somaFrameMs;
    private float _piorFrameMs;
    private double _somaCpuMainMs;
    private double _somaCpuRenderMs;
    private double _somaGpuMs;
    private int _amostrasCpu;
    private int _amostrasGpu;
    private long _memoriaGerenciadaInicial;
    private int _gcGen0Inicial;
    private int _gcGen1Inicial;
    private int _gcGen2Inicial;
    private ResumoSegundo _ultimoResumo;

    // --- Overlay throttle: rebuilt a cada 0.35 s em vez de todo frame ---
    private string _overlayLine1 = string.Empty;
    private string _overlayLine2 = string.Empty;
    private string _overlayLine3 = string.Empty;
    private string _overlayLine4 = string.Empty;
    private string _overlayLine5 = string.Empty;
    private string _overlayLine6 = string.Empty;
    private string _overlayLine7 = string.Empty;
    private string _ultimoBlocoEventos = "Nenhum evento marcado.";
    private float _proximoRefreshOverlay;

    private GUIStyle _tituloStyle;
    private GUIStyle _textoStyle;
    private GUIStyle _caixaStyle;
    private bool _capturaAtiva;

    // --- API publica ---

    public static void RegistrarEvento(string categoria, string descricao)
    {
        if (_instancia == null || !_instancia._capturaAtiva)
        {
            return;
        }

        _instancia.RegistrarEventoInterno(Time.unscaledTime, categoria, descricao);
    }

    public static void RegistrarExcecao(string origem, Exception excecao)
    {
        if (_instancia == null || !_instancia._capturaAtiva || excecao == null)
        {
            return;
        }

        _instancia.RegistrarEventoInterno(Time.unscaledTime, "Excecao", (origem ?? "Runtime") + ": " + excecao.GetType().Name + " - " + excecao.Message);
    }

    public static void RegistrarMetricaTempo(string nome, float valorMs)
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrWhiteSpace(nome) || valorMs <= 0f)
        {
            return;
        }

        _instancia.RegistrarMetricaTempoInterna(nome, valorMs);
    }

    public static void IncrementarContadorMetrica(string nome, int delta = 1)
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrWhiteSpace(nome) || delta == 0)
        {
            return;
        }

        _instancia.IncrementarContadorMetricaInterna(nome, delta);
    }

    public static void DefinirContadorMetrica(string nome, int valor)
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrWhiteSpace(nome))
        {
            return;
        }

        _instancia.DefinirContadorMetricaInterna(nome, valor);
    }

    public static void RegistrarTextoMetrica(string nome, string valor)
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        _instancia.RegistrarTextoMetricaInterna(nome, valor);
    }

    public static bool RuntimeSobPressao()
    {
        return _instancia != null
               && _instancia._capturaAtiva
               && Application.isPlaying
               && Time.unscaledTime <= _runtimeOverloadUntil;
    }

    public static bool RuntimeSaturado()
    {
        return _instancia != null
               && _instancia._capturaAtiva
               && Application.isPlaying
               && Time.unscaledTime <= _runtimeSevereUntil;
    }

    public static string ObterRazaoLockRuntime()
    {
        return _runtimeLockReason ?? string.Empty;
    }

    public static bool TryObterSnapshotRuntime(out float fpsMedio, out float cpuMainMs, out bool gcPressure, out bool warmup)
    {
        fpsMedio = 0f;
        cpuMainMs = 0f;
        gcPressure = false;
        warmup = false;

        if (_instancia == null || !_instancia._capturaAtiva)
        {
            return false;
        }

        ResumoSegundo resumo = _instancia._ultimoResumo;
        if (resumo.Fim <= 0f)
        {
            return false;
        }

        fpsMedio = resumo.FpsMedio;
        cpuMainMs = resumo.CpuMainMs;
        gcPressure = resumo.GcGen1 > 0 || resumo.GcGen2 > 0 || resumo.DeltaMemoriaGerenciadaMb >= 32f;
        warmup = resumo.EmWarmup;
        return true;
    }

    public static float ObterOrcamentoCategoriaMs(CategoriaBudgetGameplay categoria)
    {
        return InfraPerformanceGameplay.ObterBudgetMs(categoria);
    }

    // Atalho para producao da IA (chamado pelo ProductionDirector)
    public static void RegistrarProducao(string itemKey, string categoria = "IA_Prod")
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrEmpty(itemKey))
        {
            return;
        }

        _instancia.RegistrarEventoInterno(Time.unscaledTime, categoria, "Produzindo: " + itemKey);
    }

    // Atalho para construcao da IA (chamado pelo BuildDirector)
    public static void RegistrarConstrucao(string itemKey, UnityEngine.Vector3 posicao, string categoria = "IA_Build")
    {
        if (_instancia == null || !_instancia._capturaAtiva || string.IsNullOrEmpty(itemKey))
        {
            return;
        }

        _instancia.RegistrarEventoInterno(
            Time.unscaledTime,
            categoria,
            string.Format(CultureInfo.InvariantCulture,
                "Construindo: {0} @ ({1:0},{2:0},{3:0})",
                itemKey, posicao.x, posicao.y, posicao.z));
    }

    // --- Ciclo de vida ---

    private void Awake()
    {
        if (_instancia != null && _instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        _instancia = this;

        if (persistirEntreCenas)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_instancia != this)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.lowMemory += OnLowMemory;
        Application.logMessageReceived += AoLogUnity;
        ReiniciarAcumuladores(Time.unscaledTime);
        bool ativacaoAutomatica = ativarAutomaticamenteQuandoPresenteNaCena && gameObject.scene.IsValid();
        _capturaAtiva = capturaAtivaNoInicio || ativacaoAutomatica;

        if (ativacaoAutomatica && !capturaAtivaNoInicio && mostrarOverlayNaAtivacaoAutomatica)
        {
            exibirOverlay = true;
        }

        if (_capturaAtiva)
        {
            _tempoInicioCaptura = Time.unscaledTime;
            PrepararCsv();
            RegistrarEventoInterno(Time.unscaledTime, "Sistema", "Diagnostico de desempenho iniciado.");
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_instancia != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.lowMemory -= OnLowMemory;
        Application.logMessageReceived -= AoLogUnity;
        FecharCsv();
    }

    private void AoLogUnity(string condition, string stackTrace, LogType type)
    {
        if (!_capturaAtiva || (type != LogType.Exception && type != LogType.Error))
        {
            return;
        }

        RegistrarEventoInterno(Time.unscaledTime, type == LogType.Exception ? "Excecao" : "Erro", condition);
    }

    private void OnDestroy()
    {
        if (_instancia == this)
        {
            _instancia = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(teclaAlternarOverlay))
        {
            exibirOverlay = !exibirOverlay;
        }

        if (Input.GetKeyDown(teclaAlternarCaptura))
        {
            SetCaptureMode(!_capturaAtiva, exibirOverlay);
        }

        if (!_capturaAtiva)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float fpsAtual = 1f / deltaTime;
        float frameMs = deltaTime * 1000f;
        float frameLentoMs = ObterOrcamentoFrameMs() / Mathf.Max(0.01f, percentualFrameLento);

        _tempoNoSegundo += deltaTime;
        _framesNoSegundo++;
        _somaFps += fpsAtual;
        _fpsMinimo = Mathf.Min(_fpsMinimo, fpsAtual);
        _fpsMaximo = Mathf.Max(_fpsMaximo, fpsAtual);
        _somaFrameMs += frameMs;
        _piorFrameMs = Mathf.Max(_piorFrameMs, frameMs);

        if (frameMs >= frameLentoMs)
        {
            _framesLentos++;
        }

        if (frameMs >= limiteTravamentoMs)
        {
            _travadasNoSegundo++;
        }

        CapturarFrameTiming();

        if (_tempoNoSegundo >= Mathf.Max(0.25f, intervaloAmostragemSegundos))
        {
            FinalizarSegundo();
            ReiniciarAcumuladores(Time.unscaledTime);
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!exibirOverlay || _instancia != this)
        {
            return;
        }

        // The diagnostic panel is IMGUI and consumes pointer events in its
        // rectangle.  Never draw it over the Government menu: the menu must
        // receive every click and nothing behind it should be selectable.
        if (MenuGoverno.EstaAberto)
        {
            return;
        }

        if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (Time.timeScale <= 0f)
        {
            return;
        }

        GarantirGuiStyles();

        // Throttle: reconstroi as strings do overlay a cada 0.35 s
        float agora = Time.unscaledTime;
        if (agora >= _proximoRefreshOverlay)
        {
            _proximoRefreshOverlay = agora + 0.35f;
            ReconstruirLinhasOverlay();
        }

        float largura = Mathf.Min(Screen.width - 20f, 780f);
        float altura = 320f;
        GUILayout.BeginArea(new Rect(10f, 10f, largura, altura), _caixaStyle);
        GUILayout.Label("Diagnostico de Desempenho", _tituloStyle);
        GUILayout.Label(_overlayLine1, _textoStyle);
        GUILayout.Label(_overlayLine2, _textoStyle);
        GUILayout.Label(_overlayLine3, _textoStyle);
        GUILayout.Label(_overlayLine4, _textoStyle);
        GUILayout.Label(_overlayLine5, _textoStyle);
        GUILayout.Label(_overlayLine6, _textoStyle);
        if (!string.IsNullOrEmpty(_overlayLine7))
        {
            GUILayout.Label(_overlayLine7, _textoStyle);
        }

        if (!string.IsNullOrEmpty(_csvPath))
        {
            GUILayout.Label("CSV: " + _csvPath, _textoStyle);
        }

        GUILayout.EndArea();
    }

    private void ReconstruirLinhasOverlay()
    {
        string cena = string.IsNullOrEmpty(_ultimoResumo.Cena)
            ? SceneManager.GetActiveScene().name
            : _ultimoResumo.Cena;

        _overlayLine1 = string.Format(
            CultureInfo.InvariantCulture,
            "Cena: {0} | Dif: {1} | FPS medio: {2:0.0} | Min: {3:0.0} | Max: {4:0.0} | Travadas: {5} | Warm-up: {6}",
            cena,
            string.IsNullOrEmpty(_ultimoResumo.Dificuldade) ? "normal" : _ultimoResumo.Dificuldade,
            _ultimoResumo.FpsMedio,
            _ultimoResumo.FpsMinimo,
            _ultimoResumo.FpsMaximo,
            _ultimoResumo.Travadas,
            _ultimoResumo.EmWarmup ? "sim" : "nao");

        _overlayLine2 = string.Format(
            CultureInfo.InvariantCulture,
            "CPU main: {0:0.00} ms | Render: {1:0.00} ms | GPU: {2:0.00} ms | Folga GPU: {3:0}%",
            _ultimoResumo.CpuMainMs,
            _ultimoResumo.CpuRenderMs,
            _ultimoResumo.GpuMs,
            _ultimoResumo.FolgaGpuPercentual);

        _overlayLine3 = string.Format(
            CultureInfo.InvariantCulture,
            "GC: G0 {0} | G1 {1} | G2 {2} | Mem gerenciada: {3:0.0} MB | Delta: {4:+0.0;-0.0;0.0} MB",
            _ultimoResumo.GcGen0,
            _ultimoResumo.GcGen1,
            _ultimoResumo.GcGen2,
            _ultimoResumo.MemoriaGerenciadaMb,
            _ultimoResumo.DeltaMemoriaGerenciadaMb);

        _overlayLine4 = "Causa provavel: " + (string.IsNullOrEmpty(_ultimoResumo.CausaProvavel)
            ? "Aguardando dados..."
            : _ultimoResumo.CausaProvavel);

        _overlayLine5 = string.Format(
            CultureInfo.InvariantCulture,
            "Units ms T/N/A {0:0.0}/{1:0.0}/{2:0.0} | Sensor/Path/Arma {3:0.0}/{4:0.0}/{5:0.0} | Budgets {6:0.0}/{7:0.0}/{8:0.0}",
            _ultimoResumo.LandUnitUpdateMs,
            _ultimoResumo.NavalUnitUpdateMs,
            _ultimoResumo.AirUnitUpdateMs,
            _ultimoResumo.SensorUpdateMs,
            _ultimoResumo.PathfindingMs,
            _ultimoResumo.WeaponUpdateMs,
            ObterOrcamentoCategoriaMs(CategoriaBudgetGameplay.Terra),
            ObterOrcamentoCategoriaMs(CategoriaBudgetGameplay.Naval),
            ObterOrcamentoCategoriaMs(CategoriaBudgetGameplay.Aereo));

        string linhaIa = string.Format(
            CultureInfo.InvariantCulture,
            "IA ms: coast {0:0.0} | naval {1:0.0} | world {2:0.0} | vis {3:0.0} | prev/conf {4:0.0}/{5:0.0} | naval prev/commit {6:0.0}/{7:0.0}",
            _ultimoResumo.CoastScanMs,
            _ultimoResumo.NavalCandidateMs,
            _ultimoResumo.WorldRefreshMs,
            _ultimoResumo.VisibleEnemyMs,
            _ultimoResumo.ConstructorPreviewMs,
            _ultimoResumo.ConstructorConfirmMs,
            _ultimoResumo.NavalPreviewMs,
            _ultimoResumo.NavalCommitMs);

        string ofensores = string.IsNullOrEmpty(_ultimoResumo.TopOffenders) ? "sem ofensores fortes" : _ultimoResumo.TopOffenders;
        string navalLock = string.IsNullOrEmpty(_ultimoResumo.NavalAutoDisabledReason) ? string.Empty : " | navalLock: " + _ultimoResumo.NavalAutoDisabledReason;
        string inputOwner = string.IsNullOrEmpty(_ultimoResumo.InputOwner) ? "None" : _ultimoResumo.InputOwner;
        string inputReason = string.IsNullOrEmpty(_ultimoResumo.InputLockReason) ? string.Empty : " | motivo: " + _ultimoResumo.InputLockReason;
        _overlayLine6 = string.Format(
            CultureInfo.InvariantCulture,
            "Governor: {0} | fronts/air/naval {1}/{2}/{3} | tiers {4}/{5}/{6} | ordens {7} | pool {8}/{9} | input {10}{11} | {12} | lock {13} | Eventos: {14}",
            string.IsNullOrEmpty(_ultimoResumo.GovernorBand) ? "n/d" : _ultimoResumo.GovernorBand,
            _ultimoResumo.ActiveLandFronts,
            _ultimoResumo.ActiveAirWings,
            _ultimoResumo.ActiveNavalTaskforces,
            _ultimoResumo.EngagedUnits,
            _ultimoResumo.SupportUnits,
            _ultimoResumo.ReserveUnits,
            _ultimoResumo.OrdersEmitted,
            _ultimoResumo.PoolHits,
            _ultimoResumo.PoolMisses,
            inputOwner,
            inputReason,
            linhaIa,
            ofensores + navalLock,
            _ultimoBlocoEventos);

        string iaState = ObterTextoMetrica("ia_runtime_state");
        string iaBootstrap = ObterTextoMetrica("ia_runtime_bootstrap");
        string iaTrace = ObterTextoMetrica("ia_runtime_trace");
        string iaError = ObterTextoMetrica("ia_runtime_error");
        string iaAuthority = ObterTextoMetrica("ia_runtime_authority");
        string ia01Progress = ObterTextoMetrica("ia01_progress");
        string ia01Objective = ObterTextoMetrica("ia01_objective");
        string ia01Construction = ObterTextoMetrica("ia01_construction");
        string ia01Combat = ObterTextoMetrica("ia01_combat");
        string ia01Military = ObterTextoMetrica("ia01_military_reserve");
        string ia01Market = ObterTextoMetrica("ia01_market");
        string ia01BlockedIntent = ObterTextoMetrica("ia01_blocked_intent");
        string ia01BlockReason = ObterTextoMetrica("ia01_block_reason");
        string ia01Failures = ObterTextoMetrica("ia01_failures");
        string ia01Cooldown = ObterTextoMetrica("ia01_cooldown");
        string ia01Unblock = ObterTextoMetrica("ia01_unblock");
        string ia01ExpensiveModule = ObterTextoMetrica("ia01_expensive_module");
        string ia01LastSlice = ObterTextoMetrica("ia01_last_slice");
        string ia01CatalogQueries = ObterTextoMetrica("ia01_catalog_queries");
        string ia01CatalogIntentQueries = ObterTextoMetrica("ia01_catalog_intent_queries");
        string ia01CatalogIndexBuilds = ObterTextoMetrica("ia01_catalog_index_builds");
        string ia01CatalogCandidates = ObterTextoMetrica("ia01_catalog_candidates");
        string ia01PhysicsChecks = ObterTextoMetrica("ia01_physics_checks");
        string ia01CapitalSource = ObterTextoMetrica("ia01_capital_source");
        string ia01CapitalItem = ObterTextoMetrica("ia01_capital_item");
        string ia01CapitalPrefab = ObterTextoMetrica("ia01_capital_prefab");
        string ia01CapitalDiagnostic = ObterTextoMetrica("ia01_capital_diagnostic");
        string ia01ConstructionMode = ObterTextoMetrica("ia01_construction_mode");
        string ia01ConstructionState = ObterTextoMetrica("ia01_construction_state");
        string ia01ConstructionCommand = ObterTextoMetrica("ia01_construction_command");
        string ia01ActiveCommand = ObterTextoMetrica("ia01_active_command");
        string ia01PendingStructure = ObterTextoMetrica("ia01_pending_structure");
        string ia01ConfirmationDeadline = ObterTextoMetrica("ia01_confirmation_deadline");
        string ia01Treasury = ObterTextoMetrica("ia01_treasury");
        string ia01BuildingsTotal = ObterTextoMetrica("ia01_buildings_total");
        string ia01BuildingsByRole = ObterTextoMetrica("ia01_buildings_by_role");
        string ia01BuildingsByStrategicRole = ObterTextoMetrica("ia01_buildings_by_strategic_role");
        string ia01HousingNeed = ObterTextoMetrica("ia01_housing_need");
        string ia01FoodCoverage = ObterTextoMetrica("ia01_food_coverage");
        string ia01EnergyCoverage = ObterTextoMetrica("ia01_energy_coverage");
        string ia01StorageOccupancy = ObterTextoMetrica("ia01_storage_occupancy");
        string ia01EmergencyReserve = ObterTextoMetrica("ia01_emergency_reserve");
        string ia01AvailableConstructionFunds = ObterTextoMetrica("ia01_available_construction_funds");
        string ia01CityCoverage = ObterTextoMetrica("ia01_city_coverage");
        string ia01CurrentSector = ObterTextoMetrica("ia01_current_sector");
        string ia01CurrentNeed = ObterTextoMetrica("ia01_current_need");
        string ia01NeedScore = ObterTextoMetrica("ia01_need_score");
        string ia01CurrentLot = ObterTextoMetrica("ia01_current_lot");
        string ia01LastConstructionCompletedAt = ObterTextoMetrica("ia01_last_construction_completed_at");
        string ia01ConstructionFreezeReason = ObterTextoMetrica("ia01_construction_freeze_reason");
        string ia01NextUnfreezeCondition = ObterTextoMetrica("ia01_next_unfreeze_condition");
        string ia01FoundationFundingGranted = ObterTextoMetrica("ia01_foundation_funding_granted");
        string ia01FoundationCapitalCost = ObterTextoMetrica("ia01_foundation_capital_cost");
        string ia01FoundationAvailableFunds = ObterTextoMetrica("ia01_foundation_available_funds");
        string ia01LastFailureCode = ObterTextoMetrica("ia01_last_failure_code");
        string ia01LastFailureDetail = ObterTextoMetrica("ia01_last_failure_detail");
        bool hasIa01Metrics = !string.IsNullOrEmpty(ia01Progress)
            || !string.IsNullOrEmpty(ia01Objective)
            || !string.IsNullOrEmpty(ia01Construction)
            || !string.IsNullOrEmpty(ia01Combat)
            || !string.IsNullOrEmpty(ia01Market);
        if (string.IsNullOrEmpty(iaState) && string.IsNullOrEmpty(iaBootstrap) && string.IsNullOrEmpty(iaTrace) && string.IsNullOrEmpty(iaError) && string.IsNullOrEmpty(iaAuthority))
        {
            _overlayLine7 = hasIa01Metrics
                ? "IA: BrainMaster sem metricas nesta janela | IA01 ativa."
                : "IA: sem dados ainda | BrainMaster nao publicou metricas nesta janela.";
        }
        else
        {
            _overlayLine7 = string.Format(
                CultureInfo.InvariantCulture,
                "IA: {0} | bootstrap {1} | trace {2} | authority {3} | erro {4}",
                string.IsNullOrEmpty(iaState) ? "n/d" : iaState,
                string.IsNullOrEmpty(iaBootstrap) ? "n/d" : iaBootstrap,
                string.IsNullOrEmpty(iaTrace) ? "n/d" : iaTrace,
                string.IsNullOrEmpty(iaAuthority) ? "n/d" : iaAuthority,
                string.IsNullOrEmpty(iaError) ? "sem erro" : iaError);
        }

        if (!string.IsNullOrEmpty(ia01Objective) || !string.IsNullOrEmpty(ia01Construction) || !string.IsNullOrEmpty(ia01Combat) || !string.IsNullOrEmpty(ia01Market))
        {
            _overlayLine7 += "\nIA01: " + (string.IsNullOrEmpty(ia01Progress) ? "progresso aguardando" : ia01Progress)
                + " | " + (string.IsNullOrEmpty(ia01Objective) ? "objetivo aguardando" : ia01Objective)
                 + " | obra=" + (string.IsNullOrEmpty(ia01Construction) ? "n/d" : ia01Construction)
                 + " | combate=" + (string.IsNullOrEmpty(ia01Combat) ? "n/d" : ia01Combat)
                 + " | reserva militar=" + (string.IsNullOrEmpty(ia01Military) ? "n/d" : ia01Military)
                 + " | mercado=" + (string.IsNullOrEmpty(ia01Market) ? "n/d" : ia01Market)
                + "\nIA01 bloqueio: intencao=" + (string.IsNullOrEmpty(ia01BlockedIntent) ? "n/d" : ia01BlockedIntent)
                + " motivo=" + (string.IsNullOrEmpty(ia01BlockReason) ? "n/d" : ia01BlockReason)
                + " falhas=" + (string.IsNullOrEmpty(ia01Failures) ? "0" : ia01Failures)
                + " codigo=" + (string.IsNullOrEmpty(ia01LastFailureCode) ? "None" : ia01LastFailureCode)
                + " cooldown=" + (string.IsNullOrEmpty(ia01Cooldown) ? "n/d" : ia01Cooldown)
                + " desbloqueio=" + (string.IsNullOrEmpty(ia01Unblock) ? "n/d" : ia01Unblock)
                + "\nIA01 falha: detalhe=" + (string.IsNullOrEmpty(ia01LastFailureDetail) ? "n/d" : ia01LastFailureDetail)
                + "\nIA01 perf: mais caro=" + (string.IsNullOrEmpty(ia01ExpensiveModule) ? "n/d" : ia01ExpensiveModule)
                + " ultima fatia=" + (string.IsNullOrEmpty(ia01LastSlice) ? "n/d" : ia01LastSlice)
                + " consultas catalogo=" + (string.IsNullOrEmpty(ia01CatalogQueries) ? "0" : ia01CatalogQueries)
                + " consultas intent=" + (string.IsNullOrEmpty(ia01CatalogIntentQueries) ? "0" : ia01CatalogIntentQueries)
                + " indices=" + (string.IsNullOrEmpty(ia01CatalogIndexBuilds) ? "0" : ia01CatalogIndexBuilds)
                + " candidatos=" + (string.IsNullOrEmpty(ia01CatalogCandidates) ? "0" : ia01CatalogCandidates)
                + " fisica=" + (string.IsNullOrEmpty(ia01PhysicsChecks) ? "0" : ia01PhysicsChecks);
            _overlayLine7 += "\nIA01 capital: fonte=" + (string.IsNullOrEmpty(ia01CapitalSource) ? "n/d" : ia01CapitalSource)
                + " itemId=" + (string.IsNullOrEmpty(ia01CapitalItem) ? "n/d" : ia01CapitalItem)
                + " prefab=" + (string.IsNullOrEmpty(ia01CapitalPrefab) ? "n/d" : ia01CapitalPrefab)
                + " diagnostico=" + (string.IsNullOrEmpty(ia01CapitalDiagnostic) ? "n/d" : ia01CapitalDiagnostic);
            _overlayLine7 += "\nIA01 construcao: modo=" + (string.IsNullOrEmpty(ia01ConstructionMode) ? "n/d" : ia01ConstructionMode)
                + " estado=" + (string.IsNullOrEmpty(ia01ConstructionState) ? "n/d" : ia01ConstructionState)
                + " freeze=" + (string.IsNullOrEmpty(ia01ConstructionFreezeReason) ? "n/d" : ia01ConstructionFreezeReason)
                + " desbloqueio=" + (string.IsNullOrEmpty(ia01NextUnfreezeCondition) ? "n/d" : ia01NextUnfreezeCondition)
                + "\nIA01 comando: active=" + (string.IsNullOrEmpty(ia01ActiveCommand) ? "n/d" : ia01ActiveCommand)
                + " pending=" + (string.IsNullOrEmpty(ia01PendingStructure) ? "n/d" : ia01PendingStructure)
                + " deadline=" + (string.IsNullOrEmpty(ia01ConfirmationDeadline) ? "n/d" : ia01ConfirmationDeadline)
                + " lote=" + (string.IsNullOrEmpty(ia01CurrentLot) ? "n/d" : ia01CurrentLot)
                + "\nIA01 financeiro: treasury=" + (string.IsNullOrEmpty(ia01Treasury) ? "n/d" : ia01Treasury)
                + " reserva=" + (string.IsNullOrEmpty(ia01EmergencyReserve) ? "n/d" : ia01EmergencyReserve)
                + " fundos=" + (string.IsNullOrEmpty(ia01AvailableConstructionFunds) ? "n/d" : ia01AvailableConstructionFunds)
                + " fundingFundacao=" + (string.IsNullOrEmpty(ia01FoundationFundingGranted) ? "false" : ia01FoundationFundingGranted)
                + " custoCapital=" + (string.IsNullOrEmpty(ia01FoundationCapitalCost) ? "0" : ia01FoundationCapitalCost)
                + " fundosFundacao=" + (string.IsNullOrEmpty(ia01FoundationAvailableFunds) ? "0" : ia01FoundationAvailableFunds)
                + " concluidaEm=" + (string.IsNullOrEmpty(ia01LastConstructionCompletedAt) ? "n/d" : ia01LastConstructionCompletedAt)
                + "\nIA01 base: total=" + (string.IsNullOrEmpty(ia01BuildingsTotal) ? "0" : ia01BuildingsTotal)
                + " roles=" + (string.IsNullOrEmpty(ia01BuildingsByStrategicRole) ? "n/d" : ia01BuildingsByStrategicRole)
                + " necessidade=" + (string.IsNullOrEmpty(ia01CurrentNeed) ? "n/d" : ia01CurrentNeed)
                + " score=" + (string.IsNullOrEmpty(ia01NeedScore) ? "0" : ia01NeedScore)
                + "\nIA01 necessidades: moradia=" + (string.IsNullOrEmpty(ia01HousingNeed) ? "n/d" : ia01HousingNeed)
                + " comida=" + (string.IsNullOrEmpty(ia01FoodCoverage) ? "n/d" : ia01FoodCoverage)
                + " energia=" + (string.IsNullOrEmpty(ia01EnergyCoverage) ? "n/d" : ia01EnergyCoverage)
                + " armazenamento=" + (string.IsNullOrEmpty(ia01StorageOccupancy) ? "n/d" : ia01StorageOccupancy)
                + " cobertura=" + (string.IsNullOrEmpty(ia01CityCoverage) ? "n/d" : ia01CityCoverage)
                + " setor=" + (string.IsNullOrEmpty(ia01CurrentSector) ? "n/d" : ia01CurrentSector)
                + "\nIA01 index: catalogo=" + (string.IsNullOrEmpty(ia01CatalogIndexBuilds) ? "0" : ia01CatalogIndexBuilds)
                + " candidatos=" + (string.IsNullOrEmpty(ia01CatalogCandidates) ? "0" : ia01CatalogCandidates)
                + " fisica=" + (string.IsNullOrEmpty(ia01PhysicsChecks) ? "0" : ia01PhysicsChecks);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_capturaAtiva && registrarCarregamentoDeCena)
        {
            RegistrarEventoInterno(Time.unscaledTime, "Cena", string.Format("Cena carregada: {0} ({1})", scene.name, mode));
        }
    }

    private void OnLowMemory()
    {
        if (!_capturaAtiva)
        {
            return;
        }

        RegistrarEventoInterno(Time.unscaledTime, "Memoria", "Unity disparou Application.lowMemory.");
    }

    public void SetCaptureMode(bool ativo, bool mostrarOverlay)
    {
        _capturaAtiva = ativo;
        exibirOverlay = mostrarOverlay && ativo;

        if (_capturaAtiva)
        {
            _tempoInicioCaptura = Time.unscaledTime;
            ReiniciarAcumuladores(Time.unscaledTime);
            PrepararCsv();
            RegistrarEventoInterno(Time.unscaledTime, "Sistema", "Captura de desempenho ativada.");
        }
        else
        {
            FecharCsv();
            _eventos.Clear();
            _ultimoBlocoEventos = "Captura desativada.";
            ReiniciarAcumuladores(Time.unscaledTime);
        }
    }

    private void FinalizarSegundo()
    {
        float tempoFim = _inicioSegundoAtual + _tempoNoSegundo;
        float fpsMedio = _framesNoSegundo > 0 ? _somaFps / _framesNoSegundo : 0f;
        float frameMsMedio = _framesNoSegundo > 0 ? _somaFrameMs / _framesNoSegundo : 0f;
        float cpuMainMs = _amostrasCpu > 0 ? (float)(_somaCpuMainMs / _amostrasCpu) : 0f;
        float cpuRenderMs = _amostrasCpu > 0 ? (float)(_somaCpuRenderMs / _amostrasCpu) : 0f;
        float gpuMs = _amostrasGpu > 0 ? (float)(_somaGpuMs / _amostrasGpu) : 0f;
        float orcamentoMs = ObterOrcamentoFrameMs();
        int gcGen0 = Mathf.Max(0, GC.CollectionCount(0) - _gcGen0Inicial);
        int gcGen1 = Mathf.Max(0, GC.CollectionCount(1) - _gcGen1Inicial);
        int gcGen2 = Mathf.Max(0, GC.CollectionCount(2) - _gcGen2Inicial);
        float memoriaGerenciadaMb = BytesParaMb(GC.GetTotalMemory(false));
        float memoriaAlocadaMb = BytesParaMb(Profiler.GetTotalAllocatedMemoryLong());
        float memoriaReservadaMb = BytesParaMb(Profiler.GetTotalReservedMemoryLong());
        float deltaMemoriaGerenciadaMb = BytesParaMb(GC.GetTotalMemory(false) - _memoriaGerenciadaInicial);

        // FIX: extrai eventos ANTES de qualquer limpeza
        string eventosDoSegundo = ExtrairEventosDoIntervalo(_inicioSegundoAtual, tempoFim);
        LimparEventosAntigos(_inicioSegundoAtual);
        bool emWarmup = tempoFim - _tempoInicioCaptura < Mathf.Max(0f, warmupInicialSegundos);
        float uiRebuildMs = ObterTempoMetrica("ui_rebuild_ms");
        float zonePlannerMs = ObterTempoMetrica("zone_planner_ms");
        float coastScanMs = ObterTempoMetrica("coast_scan_ms");
        float navalCandidateMs = ObterTempoMetrica("naval_candidate_ms");
        float worldRefreshMs = ObterTempoMetrica("world_refresh_ms");
        float visibleEnemyMs = ObterTempoMetrica("visible_enemy_ms");
        float productionMs = ObterTempoMetrica("production_ms");
        float buildExecuteMs = ObterTempoMetrica("build_execute_ms");
        float produceExecuteMs = ObterTempoMetrica("produce_execute_ms");
        float spawnStructureMs = ObterTempoMetrica("spawn_structure_ms");
        float spawnLandMs = ObterTempoMetrica("spawn_land_ms");
        float spawnNavalMs = ObterTempoMetrica("spawn_naval_ms");
        float spawnAirMs = ObterTempoMetrica("spawn_air_ms");
        float navmeshSpawnMs = ObterTempoMetrica("navmesh_spawn_ms");
        float prefabInitMs = ObterTempoMetrica("prefab_init_ms");
        float airUnitUpdateMs = ObterTempoMetrica("air_unit_update_ms");
        float navalUnitUpdateMs = ObterTempoMetrica("naval_unit_update_ms");
        float landUnitUpdateMs = ObterTempoMetrica("land_unit_update_ms");
        float sensorUpdateMs = ObterTempoMetrica("sensor_update_ms");
        float targetingMs = ObterTempoMetrica("targeting_ms");
        float pathfindingMs = ObterTempoMetrica("pathfinding_ms");
        float weaponUpdateMs = ObterTempoMetrica("weapon_update_ms");
        float formationUpdateMs = ObterTempoMetrica("formation_update_ms");
        float constructorPreviewMs = ObterTempoMetrica("constructor_preview_ms");
        float constructorConfirmMs = ObterTempoMetrica("constructor_confirm_ms");
        float navalPreviewMs = ObterTempoMetrica("naval_preview_ms");
        float navalCommitMs = ObterTempoMetrica("naval_commit_ms");
        int previewOverflowCount = ObterContadorMetrica("preview_overflow_count");
        int ordersEmitted = ObterContadorMetrica("orders_emitted");
        int poolHits = ObterContadorMetrica("pool_hits");
        int poolMisses = ObterContadorMetrica("pool_misses");
        int spawnRegistrations = ObterContadorMetrica("spawn_registrations");
        int engagedUnits = ObterContadorMetrica("engaged_units");
        int supportUnits = ObterContadorMetrica("support_units");
        int reserveUnits = ObterContadorMetrica("reserve_units");
        int transportCapacityReady = ObterContadorMetrica("transport_capacity_ready");
        int activeAirWings = ObterContadorMetrica("active_air_wings");
        int activeNavalTaskforces = ObterContadorMetrica("active_naval_taskforces");
        int activeLandFronts = ObterContadorMetrica("active_land_fronts");
        string navalAutoDisabledReason = ObterTextoMetrica("naval_auto_disabled_reason");
        string spawnPrefabName = ObterTextoMetrica("spawn_prefab_name");
        string governorBand = ObterTextoMetrica("governor_band");
        string inputOwner = ObterTextoMetrica("input_owner");
        string inputLockReason = ObterTextoMetrica("input_lock_reason");
        string topOffenders = MontarResumoTopOffenders();
        string dificuldadeAtual = GameDifficultyManager.Instancia.ObterCodigoDificuldade();

        ResumoSegundo resumo = new ResumoSegundo
        {
            Cena = SceneManager.GetActiveScene().name,
            Dificuldade = dificuldadeAtual,
            Inicio = _inicioSegundoAtual,
            Fim = tempoFim,
            EmWarmup = emWarmup,
            FpsMedio = fpsMedio,
            FpsMinimo = _fpsMinimo == float.MaxValue ? 0f : _fpsMinimo,
            FpsMaximo = _fpsMaximo,
            FrameMsMedio = frameMsMedio,
            PiorFrameMs = _piorFrameMs,
            FramesLentos = _framesLentos,
            Travadas = _travadasNoSegundo,
            CpuMainMs = cpuMainMs,
            CpuRenderMs = cpuRenderMs,
            GpuMs = gpuMs,
            GcGen0 = gcGen0,
            GcGen1 = gcGen1,
            GcGen2 = gcGen2,
            MemoriaGerenciadaMb = memoriaGerenciadaMb,
            DeltaMemoriaGerenciadaMb = deltaMemoriaGerenciadaMb,
            MemoriaAlocadaMb = memoriaAlocadaMb,
            MemoriaReservadaMb = memoriaReservadaMb,
            UiRebuildMs = uiRebuildMs,
            ZonePlannerMs = zonePlannerMs,
            PressaoCpuPercentual = CalcularPressaoPercentual(Mathf.Max(cpuMainMs, cpuRenderMs), orcamentoMs),
            PressaoGpuPercentual = CalcularPressaoPercentual(gpuMs, orcamentoMs),
            FolgaGpuPercentual = Mathf.Clamp(100f - CalcularPressaoPercentual(gpuMs, orcamentoMs), 0f, 100f),
            CoastScanMs = coastScanMs,
            NavalCandidateMs = navalCandidateMs,
            WorldRefreshMs = worldRefreshMs,
            VisibleEnemyMs = visibleEnemyMs,
            ProductionMs = productionMs,
            BuildExecuteMs = buildExecuteMs,
            ProduceExecuteMs = produceExecuteMs,
            SpawnStructureMs = spawnStructureMs,
            SpawnLandMs = spawnLandMs,
            SpawnNavalMs = spawnNavalMs,
            SpawnAirMs = spawnAirMs,
            NavmeshSpawnMs = navmeshSpawnMs,
            PrefabInitMs = prefabInitMs,
            AirUnitUpdateMs = airUnitUpdateMs,
            NavalUnitUpdateMs = navalUnitUpdateMs,
            LandUnitUpdateMs = landUnitUpdateMs,
            SensorUpdateMs = sensorUpdateMs,
            TargetingMs = targetingMs,
            PathfindingMs = pathfindingMs,
            WeaponUpdateMs = weaponUpdateMs,
            FormationUpdateMs = formationUpdateMs,
            ConstructorPreviewMs = constructorPreviewMs,
            ConstructorConfirmMs = constructorConfirmMs,
            NavalPreviewMs = navalPreviewMs,
            NavalCommitMs = navalCommitMs,
            PreviewOverflowCount = previewOverflowCount,
            OrdersEmitted = ordersEmitted,
            PoolHits = poolHits,
            PoolMisses = poolMisses,
            SpawnRegistrations = spawnRegistrations,
            EngagedUnits = engagedUnits,
            SupportUnits = supportUnits,
            ReserveUnits = reserveUnits,
            TransportCapacityReady = transportCapacityReady,
            ActiveAirWings = activeAirWings,
            ActiveNavalTaskforces = activeNavalTaskforces,
            ActiveLandFronts = activeLandFronts,
            NavalAutoDisabledReason = navalAutoDisabledReason,
            SpawnPrefabName = spawnPrefabName,
            GovernorBand = governorBand,
            InputOwner = inputOwner,
            InputLockReason = inputLockReason,
            TopOffenders = topOffenders,
            Detalhes = eventosDoSegundo
        };

        resumo.CausaProvavel = DiagnosticarCausa(ref resumo);
        AtualizarSinalSobrecargaRuntime(resumo);
        _ultimoResumo = resumo;
        _ultimoBlocoEventos = LimitarTexto(eventosDoSegundo, maxEventosNoOverlay);

        if (gravarCsv)
        {
            EscreverCsv(resumo);
        }
    }

    private void ReiniciarAcumuladores(float tempoAtual)
    {
        _tempoNoSegundo = 0f;
        _inicioSegundoAtual = tempoAtual;
        _framesNoSegundo = 0;
        _framesLentos = 0;
        _travadasNoSegundo = 0;
        _somaFps = 0f;
        _fpsMinimo = float.MaxValue;
        _fpsMaximo = 0f;
        _somaFrameMs = 0f;
        _piorFrameMs = 0f;
        _somaCpuMainMs = 0d;
        _somaCpuRenderMs = 0d;
        _somaGpuMs = 0d;
        _amostrasCpu = 0;
        _amostrasGpu = 0;
        _memoriaGerenciadaInicial = GC.GetTotalMemory(false);
        _gcGen0Inicial = GC.CollectionCount(0);
        _gcGen1Inicial = GC.CollectionCount(1);
        _gcGen2Inicial = GC.CollectionCount(2);
        LimparMetricasAcumuladas();
    }

    private void CapturarFrameTiming()
    {
        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
        if (count == 0)
        {
            return;
        }

        FrameTiming timing = _frameTimings[0];

        if (timing.cpuMainThreadFrameTime > 0d || timing.cpuRenderThreadFrameTime > 0d)
        {
            _somaCpuMainMs += timing.cpuMainThreadFrameTime;
            _somaCpuRenderMs += timing.cpuRenderThreadFrameTime;
            _amostrasCpu++;
        }

        if (timing.gpuFrameTime > 0d)
        {
            _somaGpuMs += timing.gpuFrameTime;
            _amostrasGpu++;
        }
    }

    // FIX: diagnostico revisto — GC nao mascara mais o gargalo de CPU/GPU.
    // Agora coleta TODAS as causas encontradas e escolhe a mais grave.
    private string DiagnosticarCausa(ref ResumoSegundo resumo)
    {
        float orcamentoMs = ObterOrcamentoFrameMs();
        float spawnRuntimeMs = resumo.BuildExecuteMs
                               + resumo.ProduceExecuteMs
                               + resumo.SpawnStructureMs
                               + resumo.SpawnLandMs
                               + resumo.SpawnNavalMs
                               + resumo.SpawnAirMs
                               + resumo.NavmeshSpawnMs
                               + resumo.PrefabInitMs;
        float previewRuntimeMs = resumo.ConstructorPreviewMs
                                 + resumo.ConstructorConfirmMs
                                 + resumo.NavalPreviewMs
                                 + resumo.NavalCommitMs;

        bool houveGcLeve = resumo.GcGen0 > 0 && resumo.GcGen1 == 0 && resumo.GcGen2 == 0 && resumo.DeltaMemoriaGerenciadaMb < 8f;
        bool houveGcGrave = resumo.GcGen1 > 0 || resumo.GcGen2 > 0 || resumo.DeltaMemoriaGerenciadaMb >= 8f;
        bool gargaloGpu = resumo.GpuMs > 0f
                          && resumo.GpuMs > Mathf.Max(resumo.CpuMainMs, resumo.CpuRenderMs) * 1.15f
                          && resumo.GpuMs >= orcamentoMs * 0.92f;
        bool gargaloCpuMain = resumo.CpuMainMs > Mathf.Max(1f, resumo.GpuMs) * 1.15f
                              && resumo.CpuMainMs >= orcamentoMs * 0.92f;
        bool gargaloRender = resumo.CpuRenderMs >= orcamentoMs * 0.92f
                             && resumo.CpuRenderMs >= resumo.CpuMainMs * 0.9f;
        bool travamentoPontual = resumo.Travadas > 0
                                 && resumo.PiorFrameMs >= Mathf.Max(limiteTravamentoMs, orcamentoMs * 1.8f);
        bool gargaloSpawnRuntime = spawnRuntimeMs >= 8f
                                   || resumo.BuildExecuteMs >= 4f
                                   || resumo.ProduceExecuteMs >= 4f
                                   || resumo.SpawnStructureMs >= 4f
                                   || resumo.SpawnLandMs >= 4f
                                   || resumo.SpawnNavalMs >= 4f
                                   || resumo.SpawnAirMs >= 4f
                                   || resumo.NavmeshSpawnMs >= 3f
                                   || resumo.PrefabInitMs >= 4f;
        bool hitchPreview = previewRuntimeMs >= 25f
                            || resumo.ConstructorPreviewMs >= 25f
                            || resumo.NavalPreviewMs >= 25f
                            || resumo.ConstructorConfirmMs >= 25f
                            || resumo.NavalCommitMs >= 25f;
        bool fpsCapado = resumo.FpsMedio > 0f
                         && Mathf.Abs(resumo.FpsMedio - fpsAlvo) < 1.5f
                         && resumo.CpuMainMs < orcamentoMs * 0.75f
                         && (resumo.GpuMs <= 0f || resumo.GpuMs < orcamentoMs * 0.75f);

        if (resumo.EmWarmup)
        {
            resumo.Detalhes = AcrescentarDetalhe(resumo.Detalhes, "Warm-up ativo: nao usar este segundo como baseline final.");
        }

        // Acumular detalhes sem sobrescrever — cada causa contribui para o campo
        if (houveGcGrave || houveGcLeve)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GC: G0={0} G1={1} G2={2} | deltaMem={3:+0.0;-0.0;0.0} MB",
                    resumo.GcGen0, resumo.GcGen1, resumo.GcGen2,
                    resumo.DeltaMemoriaGerenciadaMb));
        }

        if (gargaloSpawnRuntime)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawn runtime: buildExec={0:0.0} | produceExec={1:0.0} | struct={2:0.0} | land={3:0.0} | naval={4:0.0} | air={5:0.0} | navmesh={6:0.0} | init={7:0.0} | prefab={8}",
                    resumo.BuildExecuteMs,
                    resumo.ProduceExecuteMs,
                    resumo.SpawnStructureMs,
                    resumo.SpawnLandMs,
                    resumo.SpawnNavalMs,
                    resumo.SpawnAirMs,
                    resumo.NavmeshSpawnMs,
                    resumo.PrefabInitMs,
                    string.IsNullOrEmpty(resumo.SpawnPrefabName) ? "n/d" : resumo.SpawnPrefabName));
        }

        if (hitchPreview)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Preview/build hitch: preview={0:0.0} | confirm={1:0.0} | navalPreview={2:0.0} | navalCommit={3:0.0} | overflow={4}",
                    resumo.ConstructorPreviewMs,
                    resumo.ConstructorConfirmMs,
                    resumo.NavalPreviewMs,
                    resumo.NavalCommitMs,
                    resumo.PreviewOverflowCount));
        }

        if (gargaloGpu)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(CultureInfo.InvariantCulture,
                    "GPU acima do orcamento: {0:0.00} ms (budget {1:0.00} ms)",
                    resumo.GpuMs, orcamentoMs));
        }

        if (gargaloCpuMain)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(CultureInfo.InvariantCulture,
                    "Main thread pesada: {0:0.00} ms (budget {1:0.00} ms)",
                    resumo.CpuMainMs, orcamentoMs));
        }

        if (gargaloRender)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(CultureInfo.InvariantCulture,
                    "Render thread pressionada: {0:0.00} ms",
                    resumo.CpuRenderMs));
        }

        if (travamentoPontual)
        {
            resumo.Detalhes = AcrescentarDetalhe(
                resumo.Detalhes,
                string.Format(CultureInfo.InvariantCulture,
                    "Travamento detectado: pior frame {0:0.00} ms",
                    resumo.PiorFrameMs));
        }

        // Escolhe a causa mais grave (prioridade: CPU > GPU > travamento > GC grave > GC leve)
        if (gargaloCpuMain)
        {
            if (hitchPreview && previewRuntimeMs >= spawnRuntimeMs)
            {
                return "Preview/construcao pesada na main thread";
            }

            if (gargaloSpawnRuntime)
            {
                return houveGcGrave
                    ? "Build/Produce/Spawn runtime pesado + GC grave"
                    : "Build/Produce/Spawn runtime pesado";
            }

            return houveGcGrave
                ? "Gargalo de CPU na main thread + GC grave"
                : "Gargalo de CPU na main thread";
        }

        if (gargaloGpu)
        {
            return houveGcGrave
                ? "Gargalo de GPU + GC grave"
                : "Gargalo de GPU";
        }

        if (gargaloRender)
        {
            return "Gargalo na render thread";
        }

        if (travamentoPontual)
        {
            return houveGcGrave
                ? "Travamento pontual + GC grave"
                : "Travamento pontual";
        }

        if (houveGcGrave)
        {
            return "GC grave (Gen1/Gen2 ou memoria subiu muito)";
        }

        if (houveGcLeve)
        {
            return "GC leve (Gen0 apenas) — monitore alocacoes";
        }

        if (fpsCapado)
        {
            return "FPS travado/capado, sem indicio forte de gargalo";
        }

        if (resumo.FramesLentos == 0 && resumo.Travadas == 0)
        {
            return "Segundo estavel, sem queda relevante";
        }

        resumo.Detalhes = AcrescentarDetalhe(resumo.Detalhes, "Carga mista: sem causa unica clara, revisar eventos do segundo.");
        return "Carga mista ou causa nao instrumentada";
    }

    private static void AtualizarSinalSobrecargaRuntime(ResumoSegundo resumo)
    {
        if (resumo.EmWarmup)
        {
            _runtimePressureConsecutiveSeconds = 0;
            return;
        }

        float agora = Time.unscaledTime;
        bool gcGrave = resumo.GcGen1 > 0 || resumo.GcGen2 > 0 || resumo.DeltaMemoriaGerenciadaMb >= 32f;
        // Auto-Trava do usuario: FPS caindo pra <= 15 aciona SEVERE. FPS <= 22 aciona PRESSAO.
        bool fpsCritico = resumo.FpsMedio > 0f && resumo.FpsMedio <= 15.5f;
        bool fpsBaixo = resumo.FpsMedio > 0f && resumo.FpsMedio <= 22.0f;

        bool severe = resumo.CpuMainMs >= 100f || resumo.PiorFrameMs >= 600f || gcGrave || fpsCritico;
        bool overload = resumo.CpuMainMs >= 40f || resumo.PiorFrameMs >= 250f || resumo.Travadas > 0 || fpsBaixo;

        if (severe)
        {
            _runtimePressureConsecutiveSeconds = Mathf.Max(_runtimePressureConsecutiveSeconds, 2);
            _runtimeSevereUntil = Mathf.Max(_runtimeSevereUntil, agora + 30f);
            _runtimeOverloadUntil = Mathf.Max(_runtimeOverloadUntil, agora + 30f);
            _runtimeLockReason = MontarRazaoLockRuntime(resumo, true);
            return;
        }

        if (overload)
        {
            _runtimePressureConsecutiveSeconds++;
            if (_runtimePressureConsecutiveSeconds >= 2 || resumo.PiorFrameMs >= 250f)
            {
                _runtimeOverloadUntil = Mathf.Max(_runtimeOverloadUntil, agora + 22f);
                _runtimeLockReason = MontarRazaoLockRuntime(resumo, false);
            }
        }
        else if (agora > _runtimeOverloadUntil)
        {
            _runtimePressureConsecutiveSeconds = 0;
            if (agora > _runtimeSevereUntil)
            {
                _runtimeLockReason = string.Empty;
            }
        }
    }

    private static string MontarRazaoLockRuntime(ResumoSegundo resumo, bool severe)
    {
        string prefix = severe ? "runtime saturado" : "runtime sob pressao";
        string prefab = string.IsNullOrEmpty(resumo.SpawnPrefabName) ? "n/d" : resumo.SpawnPrefabName;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} | cpu={1:0.0}ms | pior={2:0.0}ms | deltaMem={3:+0.0;-0.0;0.0}MB | prefab={4}",
            prefix,
            resumo.CpuMainMs,
            resumo.PiorFrameMs,
            resumo.DeltaMemoriaGerenciadaMb,
            prefab);
    }

    // FIX: extrai eventos sem remover — a limpeza e feita separado, apos a extracao
    private string ExtrairEventosDoIntervalo(float inicio, float fim)
    {
        if (_eventos.Count == 0)
        {
            return "Nenhum evento marcado.";
        }

        StringBuilder sb = new StringBuilder(256);

        for (int i = 0; i < _eventos.Count; i++)
        {
            EventoRuntime evento = _eventos[i];
            if (evento.Tempo < inicio || evento.Tempo > fim + 0.001f)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(" | ");
            }

            sb.Append('[')
              .Append(evento.Categoria)
              .Append("] ")
              .Append(evento.Descricao);
        }

        return sb.Length > 0 ? sb.ToString() : "Nenhum evento marcado.";
    }

    // FIX: limpeza de eventos antigos separada da extracao
    private void LimparEventosAntigos(float inicioSegmento)
    {
        float limiteAntigo = inicioSegmento - 15f;
        int removiveis = 0;
        for (int i = 0; i < _eventos.Count; i++)
        {
            if (_eventos[i].Tempo < limiteAntigo)
            {
                removiveis++;
            }
            else
            {
                break; // lista esta ordenada por tempo
            }
        }

        if (removiveis > 0)
        {
            _eventos.RemoveRange(0, Mathf.Min(removiveis, _eventos.Count));
        }
    }

    private void RegistrarMetricaTempoInterna(string nome, float valorMs)
    {
        string chave = NormalizarCampoLivre(nome).ToLowerInvariant();
        float atual;
        _metricasTempoAcumuladas.TryGetValue(chave, out atual);
        _metricasTempoAcumuladas[chave] = atual + Mathf.Max(0f, valorMs);
    }

    private void IncrementarContadorMetricaInterna(string nome, int delta)
    {
        string chave = NormalizarCampoLivre(nome).ToLowerInvariant();
        int atual;
        _metricasContagem.TryGetValue(chave, out atual);
        _metricasContagem[chave] = atual + delta;
    }

    private void DefinirContadorMetricaInterna(string nome, int valor)
    {
        string chave = NormalizarCampoLivre(nome).ToLowerInvariant();
        _metricasContagem[chave] = valor;
    }

    private void RegistrarTextoMetricaInterna(string nome, string valor)
    {
        string chave = NormalizarCampoLivre(nome).ToLowerInvariant();
        _metricasTexto[chave] = NormalizarCampoLivre(valor);
    }

    private float ObterTempoMetrica(string nome)
    {
        float valor;
        return _metricasTempoAcumuladas.TryGetValue(NormalizarCampoLivre(nome).ToLowerInvariant(), out valor) ? valor : 0f;
    }

    private int ObterContadorMetrica(string nome)
    {
        int valor;
        return _metricasContagem.TryGetValue(NormalizarCampoLivre(nome).ToLowerInvariant(), out valor) ? valor : 0;
    }

    private string ObterTextoMetrica(string nome)
    {
        string valor;
        return _metricasTexto.TryGetValue(NormalizarCampoLivre(nome).ToLowerInvariant(), out valor) ? valor : string.Empty;
    }

    private string MontarResumoTopOffenders()
    {
        if (_metricasTempoAcumuladas.Count == 0)
        {
            return string.Empty;
        }

        KeyValuePair<string, float> top1 = default(KeyValuePair<string, float>);
        KeyValuePair<string, float> top2 = default(KeyValuePair<string, float>);

        foreach (KeyValuePair<string, float> pair in _metricasTempoAcumuladas)
        {
            if (pair.Value <= 0.01f)
            {
                continue;
            }

            if (pair.Value > top1.Value)
            {
                top2 = top1;
                top1 = pair;
            }
            else if (pair.Value > top2.Value)
            {
                top2 = pair;
            }
        }

        if (top1.Value <= 0.01f)
        {
            return string.Empty;
        }

        if (top2.Value <= 0.01f)
        {
            return FormatarTopOffender(top1);
        }

        return FormatarTopOffender(top1) + " | " + FormatarTopOffender(top2);
    }

    private static string FormatarTopOffender(KeyValuePair<string, float> pair)
    {
        return pair.Key + "=" + pair.Value.ToString("0.0", CultureInfo.InvariantCulture) + "ms";
    }

    private void LimparMetricasAcumuladas()
    {
        _metricasTempoAcumuladas.Clear();
        _metricasContagem.Clear();
        _metricasTexto.Clear();
    }

    private void RegistrarEventoInterno(float tempo, string categoria, string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return;
        }

        _eventos.Add(new EventoRuntime
        {
            Tempo = tempo,
            Categoria = NormalizarCampoLivre(string.IsNullOrWhiteSpace(categoria) ? "Jogo" : categoria),
            Descricao = NormalizarCampoLivre(descricao)
        });

        if (_eventos.Count > Mathf.Max(32, maxEventosEmMemoria))
        {
            _eventos.RemoveRange(0, _eventos.Count - Mathf.Max(32, maxEventosEmMemoria));
        }
    }

    private void PrepararCsv()
    {
        if (!gravarCsv || _csvWriter != null)
        {
            return;
        }

        string pasta = @"C:\Users\Mathe\OneDrive\Documentos\Hegemonia-Mundial-main (1)\Hegemonia\Diagnostico fps";
        Directory.CreateDirectory(pasta);
        _csvPath = Path.Combine(
            pasta,
            "desempenho_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".csv");
        _csvWriter = new StreamWriter(_csvPath, false, new UTF8Encoding(true));
        _csvWriter.WriteLine("timestamp_iso;segundo_inicio;segundo_fim;cena;dificuldade;warmup;fps_medio;fps_minimo;fps_maximo;frame_ms_medio;pior_frame_ms;frames_lentos;travadas;cpu_main_ms;cpu_render_ms;gpu_ms;pressao_cpu_pct;pressao_gpu_pct;folga_gpu_pct;gc_gen0;gc_gen1;gc_gen2;mem_gerenciada_mb;delta_mem_gerenciada_mb;mem_alocada_mb;mem_reservada_mb;ui_rebuild_ms;zone_planner_ms;coast_scan_ms;naval_candidate_ms;world_refresh_ms;visible_enemy_ms;production_ms;build_execute_ms;produce_execute_ms;spawn_structure_ms;spawn_land_ms;spawn_naval_ms;spawn_air_ms;navmesh_spawn_ms;prefab_init_ms;air_unit_update_ms;naval_unit_update_ms;land_unit_update_ms;sensor_update_ms;targeting_ms;pathfinding_ms;weapon_update_ms;formation_update_ms;orders_emitted;pool_hits;pool_misses;spawn_registrations;engaged_units;support_units;reserve_units;transport_capacity_ready;active_air_wings;active_naval_taskforces;active_land_fronts;governor_band;spawn_prefab_name;naval_auto_disabled_reason;top_offenders;causa;detalhes");
        _csvWriter.Flush();
    }

    private void EscreverCsv(ResumoSegundo resumo)
    {
        if (_csvWriter == null)
        {
            return;
        }

        _csvBuilder.Length = 0;
        _csvBuilder.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.Inicio.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.Fim.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
            .Append(SanearCsv(resumo.Cena)).Append(';')
            .Append(SanearCsv(resumo.Dificuldade)).Append(';')
            .Append(resumo.EmWarmup ? "1" : "0").Append(';')
            .Append(resumo.FpsMedio.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FpsMinimo.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FpsMaximo.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FrameMsMedio.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PiorFrameMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FramesLentos.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.Travadas.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.CpuMainMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.CpuRenderMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.GpuMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PressaoCpuPercentual.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PressaoGpuPercentual.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FolgaGpuPercentual.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.GcGen0.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.GcGen1.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.GcGen2.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.MemoriaGerenciadaMb.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.DeltaMemoriaGerenciadaMb.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.MemoriaAlocadaMb.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.MemoriaReservadaMb.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.UiRebuildMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ZonePlannerMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.CoastScanMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.NavalCandidateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.WorldRefreshMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.VisibleEnemyMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ProductionMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.BuildExecuteMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ProduceExecuteMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SpawnStructureMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SpawnLandMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SpawnNavalMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SpawnAirMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.NavmeshSpawnMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PrefabInitMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.AirUnitUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.NavalUnitUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.LandUnitUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SensorUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.TargetingMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PathfindingMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.WeaponUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.FormationUpdateMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.OrdersEmitted.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PoolHits.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.PoolMisses.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SpawnRegistrations.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.EngagedUnits.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.SupportUnits.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ReserveUnits.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.TransportCapacityReady.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ActiveAirWings.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ActiveNavalTaskforces.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(resumo.ActiveLandFronts.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(SanearCsv(resumo.GovernorBand)).Append(';')
            .Append(SanearCsv(resumo.SpawnPrefabName)).Append(';')
            .Append(SanearCsv(resumo.NavalAutoDisabledReason)).Append(';')
            .Append(SanearCsv(resumo.TopOffenders)).Append(';')
            .Append(SanearCsv(resumo.CausaProvavel)).Append(';')
            .Append(SanearCsv(resumo.Detalhes));

        _csvWriter.WriteLine(_csvBuilder.ToString());
        _csvWriter.Flush();
    }

    private void FecharCsv()
    {
        if (_csvWriter == null)
        {
            return;
        }

        _csvWriter.Flush();
        _csvWriter.Dispose();
        _csvWriter = null;
    }

    private void GarantirGuiStyles()
    {
        if (_tituloStyle != null)
        {
            return;
        }

        _tituloStyle = new GUIStyle(GUI.skin.label);
        _tituloStyle.fontSize = 18;
        _tituloStyle.fontStyle = FontStyle.Bold;
        _tituloStyle.normal.textColor = Color.white;

        _textoStyle = new GUIStyle(GUI.skin.label);
        _textoStyle.fontSize = 13;
        _textoStyle.wordWrap = true;
        _textoStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);

        _caixaStyle = new GUIStyle(GUI.skin.box);
        _caixaStyle.padding = new RectOffset(12, 12, 10, 10);
        _caixaStyle.alignment = TextAnchor.UpperLeft;
        _caixaStyle.normal.textColor = Color.white;
    }

    private float ObterOrcamentoFrameMs()
    {
        return 1000f / Mathf.Max(15, fpsAlvo);
    }

    private static float CalcularPressaoPercentual(float tempoMs, float orcamentoMs)
    {
        if (tempoMs <= 0f || orcamentoMs <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp((tempoMs / orcamentoMs) * 100f, 0f, 999f);
    }

    private static float BytesParaMb(long bytes)
    {
        return bytes / (1024f * 1024f);
    }

    private static string NormalizarCampoLivre(string valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        bool hasNewline = valor.IndexOf('\n') >= 0 || valor.IndexOf('\r') >= 0;
        if (hasNewline) valor = valor.Replace('\n', ' ').Replace('\r', ' ');
        return valor.Trim();
    }

    private static string SanearCsv(string valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        return valor.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string AcrescentarDetalhe(string atual, string detalhe)
    {
        if (string.IsNullOrWhiteSpace(atual) || atual == "Nenhum evento marcado.")
        {
            return detalhe;
        }

        return atual + " | " + detalhe;
    }

    private static string LimitarTexto(string texto, int maxPartes)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "Nenhum evento marcado.";
        }

        string[] partes = texto.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length <= Mathf.Max(1, maxPartes))
        {
            return texto;
        }

        StringBuilder sb = new StringBuilder();
        int limite = Mathf.Max(1, maxPartes);
        for (int i = 0; i < limite; i++)
        {
            if (i > 0)
            {
                sb.Append(" | ");
            }

            sb.Append(partes[i]);
        }

        sb.Append(" | ...");
        return sb.ToString();
    }
}
