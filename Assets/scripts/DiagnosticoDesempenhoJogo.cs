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
        public float Inicio;
        public float Fim;
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
        public float PressaoCpuPercentual;
        public float PressaoGpuPercentual;
        public float FolgaGpuPercentual;
        public string CausaProvavel;
        public string Detalhes;
    }

    private static DiagnosticoDesempenhoJogo _instancia;

    [Header("Captura")]
    [SerializeField] private int fpsAlvo = 60;
    [SerializeField] private float intervaloAmostragemSegundos = 1f;
    [SerializeField] private float limiteTravamentoMs = 50f;
    [SerializeField] [Range(0.5f, 1f)] private float percentualFrameLento = 0.9f;

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

    private StreamWriter _csvWriter;
    private string _csvPath = string.Empty;
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
        ReiniciarAcumuladores(Time.unscaledTime);
        bool ativacaoAutomatica = ativarAutomaticamenteQuandoPresenteNaCena && gameObject.scene.IsValid();
        _capturaAtiva = capturaAtivaNoInicio || ativacaoAutomatica;

        if (ativacaoAutomatica && !capturaAtivaNoInicio && mostrarOverlayNaAtivacaoAutomatica)
        {
            exibirOverlay = true;
        }

        if (_capturaAtiva)
        {
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
        FecharCsv();
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

        GarantirGuiStyles();

        // Throttle: reconstroi as strings do overlay a cada 0.35 s
        float agora = Time.unscaledTime;
        if (agora >= _proximoRefreshOverlay)
        {
            _proximoRefreshOverlay = agora + 0.35f;
            ReconstruirLinhasOverlay();
        }

        float largura = Mathf.Min(Screen.width - 20f, 700f);
        float altura = 250f;
        GUILayout.BeginArea(new Rect(10f, 10f, largura, altura), _caixaStyle);
        GUILayout.Label("Diagnostico de Desempenho", _tituloStyle);
        GUILayout.Label(_overlayLine1, _textoStyle);
        GUILayout.Label(_overlayLine2, _textoStyle);
        GUILayout.Label(_overlayLine3, _textoStyle);
        GUILayout.Label(_overlayLine4, _textoStyle);
        GUILayout.Label(_overlayLine5, _textoStyle);
        GUILayout.Label(_overlayLine6, _textoStyle);

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
            "Cena: {0} | FPS medio: {1:0.0} | Min: {2:0.0} | Max: {3:0.0} | Travadas: {4}",
            cena,
            _ultimoResumo.FpsMedio,
            _ultimoResumo.FpsMinimo,
            _ultimoResumo.FpsMaximo,
            _ultimoResumo.Travadas);

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

        _overlayLine5 = "Detalhes: " + (string.IsNullOrEmpty(_ultimoResumo.Detalhes)
            ? "Sem detalhes."
            : _ultimoResumo.Detalhes);

        _overlayLine6 = "Eventos recentes: " + _ultimoBlocoEventos;
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

        ResumoSegundo resumo = new ResumoSegundo
        {
            Cena = SceneManager.GetActiveScene().name,
            Inicio = _inicioSegundoAtual,
            Fim = tempoFim,
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
            PressaoCpuPercentual = CalcularPressaoPercentual(Mathf.Max(cpuMainMs, cpuRenderMs), orcamentoMs),
            PressaoGpuPercentual = CalcularPressaoPercentual(gpuMs, orcamentoMs),
            FolgaGpuPercentual = Mathf.Clamp(100f - CalcularPressaoPercentual(gpuMs, orcamentoMs), 0f, 100f),
            Detalhes = eventosDoSegundo
        };

        resumo.CausaProvavel = DiagnosticarCausa(ref resumo);
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
        bool fpsCapado = resumo.FpsMedio > 0f
                         && Mathf.Abs(resumo.FpsMedio - fpsAlvo) < 1.5f
                         && resumo.CpuMainMs < orcamentoMs * 0.75f
                         && (resumo.GpuMs <= 0f || resumo.GpuMs < orcamentoMs * 0.75f);

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

        string pasta = Path.Combine(Application.persistentDataPath, "Diagnosticos");
        Directory.CreateDirectory(pasta);
        _csvPath = Path.Combine(
            pasta,
            "desempenho_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".csv");
        _csvWriter = new StreamWriter(_csvPath, false, new UTF8Encoding(true));
        _csvWriter.WriteLine("timestamp_iso;segundo_inicio;segundo_fim;cena;fps_medio;fps_minimo;fps_maximo;frame_ms_medio;pior_frame_ms;frames_lentos;travadas;cpu_main_ms;cpu_render_ms;gpu_ms;pressao_cpu_pct;pressao_gpu_pct;folga_gpu_pct;gc_gen0;gc_gen1;gc_gen2;mem_gerenciada_mb;delta_mem_gerenciada_mb;mem_alocada_mb;mem_reservada_mb;causa;detalhes");
        _csvWriter.Flush();
    }

    private void EscreverCsv(ResumoSegundo resumo)
    {
        if (_csvWriter == null)
        {
            return;
        }

        string linha = string.Join(
            ";",
            new[]
            {
                DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                resumo.Inicio.ToString("0.000", CultureInfo.InvariantCulture),
                resumo.Fim.ToString("0.000", CultureInfo.InvariantCulture),
                SanearCsv(resumo.Cena),
                resumo.FpsMedio.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.FpsMinimo.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.FpsMaximo.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.FrameMsMedio.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.PiorFrameMs.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.FramesLentos.ToString(CultureInfo.InvariantCulture),
                resumo.Travadas.ToString(CultureInfo.InvariantCulture),
                resumo.CpuMainMs.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.CpuRenderMs.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.GpuMs.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.PressaoCpuPercentual.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.PressaoGpuPercentual.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.FolgaGpuPercentual.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.GcGen0.ToString(CultureInfo.InvariantCulture),
                resumo.GcGen1.ToString(CultureInfo.InvariantCulture),
                resumo.GcGen2.ToString(CultureInfo.InvariantCulture),
                resumo.MemoriaGerenciadaMb.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.DeltaMemoriaGerenciadaMb.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.MemoriaAlocadaMb.ToString("0.00", CultureInfo.InvariantCulture),
                resumo.MemoriaReservadaMb.ToString("0.00", CultureInfo.InvariantCulture),
                SanearCsv(resumo.CausaProvavel),
                SanearCsv(resumo.Detalhes)
            });

        _csvWriter.WriteLine(linha);
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
        return valor.Replace('\n', ' ').Replace('\r', ' ').Trim();
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
