using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavioTransporteTropas
/// - Transporte naval com capacidades separadas para: Veículos, Soldados e Helicópteros.
/// - Embarque/Desembarque organizado usando pontos "saida/entrada" (4 ao redor), fluxo interno Corredor1 <-> chegada.
/// - Porta terrestre anima rotação local Z: 5 -> -186.855 durante operações terrestres.
/// - Menu OnGUI estilo Porta-Aviões para selecionar unidade e quantidade.
/// </summary>
public class NavioTransporteTropas : MonoBehaviour
{
    [Header("Capacidade")]
    public int capacidadeMaxVeiculos = 8;
    public int capacidadeMaxSoldados = 200;
    [Tooltip("0 = auto (usa quantidade de Paradas detectadas)")]
    public int capacidadeMaxAereos = 0;

    [Header("Porta terrestre")]
    [Tooltip("Auto-detect: Entrada terrestre/Porta")]
    public Transform portaTerrestre;
    public float portaZFechada = 5f;
    public float portaZAberta = -186.855f;
    public float velocidadeAnimacaoPorta = 2.5f;

    [Header("Pontos terrestres (auto)")]
    [Tooltip("Auto-detect: Entrada terrestre/Porta/fila")]
    public Transform fila;
    [Tooltip("Auto-detect: filhos de 'fila' com nome 'saida/entrada (0..3)'")]
    public Transform[] pontosSaidaEntrada;
    [Tooltip("Auto-detect: Corredor/Corredor1")]
    public Transform corredor1;
    [Tooltip("Auto-detect: Corredor/chegada")]
    public Transform chegada;

    [Header("Helicópteros (auto)")]
    [Tooltip("Auto-detect: Pista")]
    public Transform pista;
    [Tooltip("Auto-detect: Pista/Pouso/decolagem")]
    public Transform pontoPousoDecolagemHeli;
    [Tooltip("Auto-detect: Pista/Parada* (ordenado)")]
    public Transform[] paradasHeli;
    [Tooltip("Auto-detect: Saida do hangar aereo/Stop")]
    public Transform stop0;
    [Tooltip("Auto-detect: Saida do hangar aereo/Stop (1)")]
    public Transform stop1;

    [Header("Operação / tuning")]
    public float raioBuscaTerrestres = 80f;
    public float raioBuscaHelis = 120f;
    [Tooltip("Distância absoluta onde o barco 'puxa' a unidade se ela estiver próxima.")]
    public float raioPuxarAbsoluto = 12f;
    [Tooltip("Distância máxima caso a unidade esteja parada no NavMesh e não consiga chegar.")]
    public float raioPuxarPresoNavMesh = 25f;
    public float distanciaChegadaPontoFila = 6f;
    public float velocidadeMovimentoInterno = 2.4f;
    public float delayEntreUnidades = 0.35f;
    public float timeoutMoverAteFila = 20f;
    public float timeoutMoverInterno = 12f;

    [Header("Otimização de FPS")]
    [Tooltip("Limita drasticamente a renderização de botões na interface para evitar queda de FPS quando há muitas unidades.")]
    public bool modoDesempenhoUI = true;

    [Header("UI / Debug")]
    public bool debugLogs = false;

    [TextArea(8, 30)]
    public string descricaoParaIA;

    // ======================================================
    // Estado interno
    // ======================================================
    private ControleUnidade _controleUnidade;
    private IdentidadeUnidade _idNavio;
    private Camera _cameraPrincipal;

    private Transform _containerCarga;
    private bool _menuAberto;
    private Vector2 _scrollMenuGeral;
    private Vector2 _scrollVeiculos;
    private Vector2 _scrollSoldados;
    private Vector2 _scrollHelis;
    private int _qtdOperacao = 1;

    private bool _operacaoTerrestreAtiva;
    private bool _operacaoHeliAtiva;

    private float _portaZAtual;
    private float _portaRotX;
    private float _portaRotY;

    private string _mensagemHUD = string.Empty;
    private float _mensagemAte = 0f;

    private int _alternanciaStop = 0;
    private float _proximoScanHeli = 0f;
    private readonly List<Helicoptero> _helisProximosCache = new List<Helicoptero>();
    private readonly List<Helicoptero> _helisCandidatosOperacao = new List<Helicoptero>(32);
    private bool _interacaoHeliSolicitada;

    private enum CategoriaSelecao
    {
        Nenhum,
        Veiculo,
        Soldado,
        Heli
    }

    private CategoriaSelecao _categoriaSelecionada = CategoriaSelecao.Nenhum;
    private int _indiceSelecionadoVeiculo = -1;
    private int _indiceSelecionadoSoldado = -1;
    private int _indiceSelecionadoHeli = -1;
    private Helicoptero _helicopteroSelecionadoParaMissao;
    private readonly List<Vector3> _rotaPatrulhaHelicoptero = new List<Vector3>();

    private enum ModoOrdemHelicopteroNavio
    {
        Nenhum,
        Reconhecimento,
        Patrulha,
        AtaqueLocal,
        Transporte
    }

    private ModoOrdemHelicopteroNavio _modoOrdemHelicoptero = ModoOrdemHelicopteroNavio.Nenhum;

    [System.Serializable]
    private class CargaTerrestre
    {
        public GameObject unidade;
        public bool navAgentEnabledAntes;
        public bool controleEnabledAntes;
        public bool rbExiste;
        public bool rbKinematicAntes;
        public bool rbUseGravityAntes;
        public bool rbDetectCollisionsAntes;
        public Transform parentOriginal;
    }

    [System.Serializable]
    private class CargaHeli
    {
        public Helicoptero heli;
        public Transform paradaAtual;
        public bool emSaida;
    }

    private readonly List<CargaTerrestre> _veiculosCarregados = new List<CargaTerrestre>();
    private readonly List<CargaTerrestre> _soldadosCarregados = new List<CargaTerrestre>();
    private readonly List<CargaHeli> _helisCarregados = new List<CargaHeli>();
    private readonly Dictionary<int, Coroutine> _rotinasEntradaHeli = new Dictionary<int, Coroutine>();
    private readonly Dictionary<int, Coroutine> _rotinasSaidaHeli = new Dictionary<int, Coroutine>();

    // ======================================================
    // Cache UI (evita queda grande de FPS ao abrir o menu com muita carga)
    // ======================================================
    private int _veiculosAtualCache;
    private int _soldadosAtualCache;
    private int _aereosAtualCache;
    private bool _contagemCargaSuja = true;

    private bool _uiMostrarListaCompleta = false;
    // OTIMIZAÇÃO: Reduzido de 28 para 4 itens visíveis por padrão.
    private const int UI_MAX_ITENS_LISTA_RESUMIDA = 4;

    private GUISkin _skinCache;
    private GUIStyle _uiLinhaCompacta;
    private GUIStyle _uiLabelCompacta;
    private GUIStyle _uiLabelWrap;
    private bool _temPontoTerraCache;
    private float _proximaAtualizacaoTemTerra;
    private readonly RaycastHit[] _hitsTerraBuffer = new RaycastHit[32];
    private Collider[] _hitsHeliBuffer = new Collider[128];
    private readonly List<Helicoptero> _bufferHelicopterosFallback = new List<Helicoptero>(32);
    private readonly HashSet<int> _vistosHelisBusca = new HashSet<int>(64);
    private readonly HashSet<int> _vistosSanearHelis = new HashSet<int>(32);
    private Collider[] _hitsTerrestresBuffer = new Collider[256];
    private readonly List<GameObject> _candidatosTerrestresOperacao = new List<GameObject>(128);
    private readonly HashSet<int> _vistosTerrestresBusca = new HashSet<int>(128);

    // ======================================================
    // API Pública (IA / Debug)
    // ======================================================
    public int VeiculosAtual => _veiculosAtualCache;
    public int SoldadosAtual => _soldadosAtualCache;
    public int AereosAtual => _aereosAtualCache;

    public int VeiculosMax => capacidadeMaxVeiculos;
    public int SoldadosMax => capacidadeMaxSoldados;
    public int AereosMax => capacidadeMaxAereos;

    public bool TemPontoEmTerra()
    {
        AtualizarTemPontoEmTerraCache(false);
        return _temPontoTerraCache;
    }

    public bool TemEspaco(TipoUnidade tipo)
    {
        switch (tipo)
        {
            case TipoUnidade.Infantaria:
                return SoldadosAtual < capacidadeMaxSoldados;
            case TipoUnidade.Veiculo:
                return VeiculosAtual < capacidadeMaxVeiculos;
            case TipoUnidade.Aereo:
                return AereosAtual < capacidadeMaxAereos;
            default:
                return false;
        }
    }

    public void OrdemEmbarcarTerrestres()
    {
        IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Todos, int.MaxValue);
    }

    public void OrdemDesembarcarTerrestres(int qtd, TipoUnidade tipoOuAuto)
    {
        if (tipoOuAuto == TipoUnidade.Veiculo)
        {
            IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Veiculos, qtd, _indiceSelecionadoVeiculo >= 0 ? _indiceSelecionadoVeiculo : 0);
            return;
        }

        if (tipoOuAuto == TipoUnidade.Infantaria)
        {
            IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Soldados, qtd, _indiceSelecionadoSoldado >= 0 ? _indiceSelecionadoSoldado : 0);
            return;
        }

        IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Todos, qtd, 0);
    }

    // ======================================================
    // Unity Lifecycle
    // ======================================================
    private void Reset()
    {
        PreencherDescricaoPadrao();
        AutoDetectarReferencias(true);
    }

    private void Awake()
    {
        _controleUnidade = GetComponent<ControleUnidade>();
        _idNavio = GetComponent<IdentidadeUnidade>();
        if (_idNavio == null) _idNavio = GetComponentInParent<IdentidadeUnidade>();

        AutoDetectarReferencias(false);
        GarantirContainerCarga();
        PrepararPorta();

        if (capacidadeMaxAereos <= 0 && paradasHeli != null && paradasHeli.Length > 0)
        {
            capacidadeMaxAereos = paradasHeli.Length;
        }
    }

    private void Update()
    {
        if (Construtor.EmModoConstrucaoAtivo)
        {
            if (_menuAberto)
            {
                _menuAberto = false;
            }

            if (PossuiOrdemManualAtiva())
            {
                CancelarInteracaoPorConstrucao();
            }
        }

        AtualizarAnimacaoPorta();
        if (!Construtor.EmModoConstrucaoAtivo)
        {
            ProcessarInputMenu();
        }
        AtualizarModoInteracaoHelicopteroNavio();
        if (_menuAberto && Time.time >= _proximoScanHeli)
        {
            AtualizarCacheHelisProximos();
            _proximoScanHeli = Time.time + 1f;
        }
        if (_modoOrdemHelicoptero != ModoOrdemHelicopteroNavio.Nenhum && _helicopteroSelecionadoParaMissao != null)
        {
            ProcessarOrdemHelicopteroNavio();
        }
        ManterHelisNoNavio();
        LimparNulos();
        if (_menuAberto)
        {
            AtualizarContagemCargaSeNecessario();
        }

        if (_menuAberto && !GestorMenusExclusivos.EstaAtivo(this))
        {
            _menuAberto = false;
        }
    }

    private void OnDisable()
    {
        GestorMenusExclusivos.Fechar(this);
        if (_interacaoHeliSolicitada)
        {
            InteractionModeService.Release(this, InteractionOwner.CarrierOrder);
            _interacaoHeliSolicitada = false;
        }
    }

    private void OnGUI()
    {
        if (!_menuAberto) return;
        if (!GestorMenusExclusivos.EstaAtivo(this))
        {
            _menuAberto = false;
            return;
        }
        if (_idNavio != null && _idNavio.teamID != 1) return;

        int oldLabelFont = GUI.skin.label.fontSize;
        int oldButtonFont = GUI.skin.button.fontSize;
        int oldBoxFont = GUI.skin.box.fontSize;
        bool oldLabelRichText = GUI.skin.label.richText;
        bool oldBoxRichText = GUI.skin.box.richText;
        bool oldButtonRichText = GUI.skin.button.richText;
        GUI.skin.label.richText = true;
        GUI.skin.box.richText = true;
        GUI.skin.button.richText = true;
        GUI.skin.label.fontSize = 11;
        GUI.skin.button.fontSize = 10;
        GUI.skin.box.fontSize = 12;

        PrepararEstilosUISeNecessario();

        float menuWidth = Mathf.Clamp(Screen.width * 0.32f, 430f, 560f);
        float menuHeight = Mathf.Min(Screen.height - 8f, 1100f);
        Rect areaMenu = new Rect(Screen.width - menuWidth - 16f, 10f, menuWidth, menuHeight);
        GestorMenusExclusivos.RegistrarAreaBloqueio(this, areaMenu);
        GUI.Box(areaMenu, "<b>🚢 COMANDO - NAVIO TRANSPORTE DE TROPAS</b>");

        GUILayout.BeginArea(new Rect(areaMenu.x + 10f, areaMenu.y + 25f, areaMenu.width - 20f, areaMenu.height - 35f));

        string nome = (_idNavio != null && !string.IsNullOrEmpty(_idNavio.nomeDoPais)) ? _idNavio.nomeDoPais : "Navio";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>⚓ Status:</b> <color=cyan>{nome}</color>", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Fechar", GUILayout.Width(72f), GUILayout.Height(22f)))
        {
            _menuAberto = false;
            GestorMenusExclusivos.Fechar(this);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        AtualizarContagemCargaSeNecessario();
        GUILayout.Label($"<b>🚚 Veículos:</b> <color=yellow>{_veiculosAtualCache}</color> / {capacidadeMaxVeiculos}");
        GUILayout.Label($"<b>🪖 Soldados:</b> <color=yellow>{_soldadosAtualCache}</color> / {capacidadeMaxSoldados}");
        GUILayout.Label($"<b>🚁 Helicópteros:</b> <color=yellow>{_aereosAtualCache}</color> / {capacidadeMaxAereos}");

        float alturaScrollMenu = Mathf.Max(320f, areaMenu.height - 68f);
        _scrollMenuGeral = GUILayout.BeginScrollView(_scrollMenuGeral, false, true, GUILayout.Height(alturaScrollMenu));

        bool temTerra = TemPontoEmTerra();
        if (!temTerra)
        {
            GUILayout.Label("<color=red><b>⚠️ Operação terrestre BLOQUEADA:</b> nenhum 'saida/entrada' está em terra.</color>", _uiLabelWrap);
        }

        GUILayout.Space(6);

        // Quantidade //
        GUILayout.BeginHorizontal("box");
        GUILayout.Label($"<b>Qtd:</b> {_qtdOperacao}", GUILayout.Width(90f));
        if (GUILayout.Button("-", GUILayout.Width(32f), GUILayout.Height(22f))) _qtdOperacao = Mathf.Max(1, _qtdOperacao - 1);
        if (GUILayout.Button("+", GUILayout.Width(32f), GUILayout.Height(22f))) _qtdOperacao = Mathf.Min(9999, _qtdOperacao + 1);
        if (GUILayout.Button("Todos", GUILayout.Width(64f), GUILayout.Height(22f))) _qtdOperacao = 9999;
        GUILayout.EndHorizontal();

        if (_operacaoTerrestreAtiva || _operacaoHeliAtiva)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("❌ CANCELAR TODAS AS OPERAÇÕES", GUILayout.Height(26f)))
            {
                _operacaoTerrestreAtiva = false;
                _operacaoHeliAtiva = false;
                FecharPortaTerrestre();
                MostrarMensagem("🚫 Mover Cancelado! Comportas Fechadas.");
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(6);
        }

        // Embarque terrestre
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>📥 EMBARQUE TERRESTRE</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Puxar Veículos", GUILayout.Height(24f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Veiculos, _qtdOperacao);
        if (GUILayout.Button("Puxar Soldados", GUILayout.Height(24f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Soldados, _qtdOperacao);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Puxar Terrestres (Todos)", GUILayout.Height(24f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Todos, _qtdOperacao);
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // Renderização Otimizada para FPS
        if (modoDesempenhoUI)
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("<color=lime><b>⚡ MODO DESEMPENHO ATIVO:</b> Listas ocultadas para poupar FPS.</color>");
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }
        else
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("<b>Lista:</b>", GUILayout.Width(52f));
            _uiMostrarListaCompleta = GUILayout.Toggle(_uiMostrarListaCompleta, "mostrar completa (MUITO PESADO)");
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        // Listas (Ocultadas ou reduzidas se modoDesempenhoUI estiver ativo)
        int maxItensPermitido = modoDesempenhoUI ? 2 : ( _uiMostrarListaCompleta ? 999 : UI_MAX_ITENS_LISTA_RESUMIDA );

        // VEÍCULOS
        if (_veiculosAtualCache > 0)
        {
            GUILayout.Label($"<color=cyan><b>🚚 VEÍCULOS A BORDO ({_veiculosAtualCache}/{capacidadeMaxVeiculos})</b></color>");
            _scrollVeiculos = GUILayout.BeginScrollView(_scrollVeiculos, GUILayout.Height(modoDesempenhoUI ? 50f : 100f));
            int totalVeiculos = _veiculosCarregados.Count;
            int limiteVeiculos = Mathf.Min(maxItensPermitido, totalVeiculos);
            
            for (int i = 0; i < limiteVeiculos; i++)
            {
                var u = _veiculosCarregados[i]?.unidade;
                if (u == null) continue;
                string pfx = (_categoriaSelecionada == CategoriaSelecao.Veiculo && _indiceSelecionadoVeiculo == i) ? "► " : "";
                if (GUILayout.Button($"{pfx}🚚 {CompactarTextoMenu(LimparClone(u.name), 30)}", _uiLinhaCompacta, GUILayout.Height(22f)))
                {
                    _categoriaSelecionada = CategoriaSelecao.Veiculo;
                    _indiceSelecionadoVeiculo = i;
                    _indiceSelecionadoSoldado = -1;
                    _indiceSelecionadoHeli = -1;
                    _helicopteroSelecionadoParaMissao = null;
                }
            }
            if (totalVeiculos > limiteVeiculos)
            {
                GUILayout.Label($"<color=grey>+{totalVeiculos - limiteVeiculos} veículos na garagem.</color>", _uiLabelCompacta);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(4);
        }

        // SOLDADOS
        if (_soldadosAtualCache > 0)
        {
            GUILayout.Label($"<color=lime><b>🪖 SOLDADOS A BORDO ({_soldadosAtualCache}/{capacidadeMaxSoldados})</b></color>");
            _scrollSoldados = GUILayout.BeginScrollView(_scrollSoldados, GUILayout.Height(modoDesempenhoUI ? 50f : 100f));
            int totalSoldados = _soldadosCarregados.Count;
            int limiteSoldados = Mathf.Min(maxItensPermitido, totalSoldados);
            
            for (int i = 0; i < limiteSoldados; i++)
            {
                var u = _soldadosCarregados[i]?.unidade;
                if (u == null) continue;
                string pfx = (_categoriaSelecionada == CategoriaSelecao.Soldado && _indiceSelecionadoSoldado == i) ? "► " : "";
                if (GUILayout.Button($"{pfx}🪖 {CompactarTextoMenu(LimparClone(u.name), 30)}", _uiLinhaCompacta, GUILayout.Height(22f)))
                {
                    _categoriaSelecionada = CategoriaSelecao.Soldado;
                    _indiceSelecionadoSoldado = i;
                    _indiceSelecionadoVeiculo = -1;
                    _indiceSelecionadoHeli = -1;
                    _helicopteroSelecionadoParaMissao = null;
                }
            }
            if (totalSoldados > limiteSoldados)
            {
                GUILayout.Label($"<color=grey>+{totalSoldados - limiteSoldados} soldados no alojamento.</color>", _uiLabelCompacta);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(4);
        }

        // HELICÓPTEROS (Deixamos um limite um pouco maior pois são poucos)
        if (_aereosAtualCache > 0)
        {
            GUILayout.Label($"<color=orange><b>🚁 HELICÓPTEROS NO CONVÉS / APROXIMAÇÃO ({_aereosAtualCache}/{capacidadeMaxAereos})</b></color>");
            _scrollHelis = GUILayout.BeginScrollView(_scrollHelis, GUILayout.Height(80f));
            int totalHelis = _helisCarregados.Count;
            int limiteHelis = Mathf.Min(modoDesempenhoUI ? 4 : maxItensPermitido, totalHelis);
            
            for (int i = 0; i < limiteHelis; i++)
            {
                var h = _helisCarregados[i]?.heli;
                if (h == null) continue;
                bool aproximando = HelicopteroEstaEmAproximacaoParaConves(h);
                string status = aproximando
                    ? " (aproximando)"
                    : _helisCarregados[i].paradaAtual != null
                        ? $" ({CompactarTextoMenu(_helisCarregados[i].paradaAtual.name, 12)})"
                        : (_helisCarregados[i].emSaida ? " (saída)" : "");
                string pfx = (_categoriaSelecionada == CategoriaSelecao.Heli && _indiceSelecionadoHeli == i) ? "► " : "";
                if (GUILayout.Button($"{pfx}🚁 {CompactarTextoMenu(h.ObterRotuloExibicao(), 24)}{status}", _uiLinhaCompacta, GUILayout.Height(22f)))
                {
                    _categoriaSelecionada = CategoriaSelecao.Heli;
                    _indiceSelecionadoHeli = i;
                    _indiceSelecionadoVeiculo = -1;
                    _indiceSelecionadoSoldado = -1;
                    _helicopteroSelecionadoParaMissao = h;
                }
            }
            if (totalHelis > limiteHelis)
            {
                GUILayout.Label($"<color=grey>+{totalHelis - limiteHelis} helicóptero(s) ocultos.</color>", _uiLabelCompacta);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6);
        }

        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>📡 HELICÓPTEROS ALIADOS PRÓXIMOS ({_helisProximosCache.Count})</b>", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Atualizar", GUILayout.Width(82f), GUILayout.Height(22f)))
        {
            AtualizarCacheHelisProximos();
        }
        GUILayout.EndHorizontal();

        if (_helisProximosCache.Count == 0)
        {
            GUILayout.Label($"<color=grey>Nenhum helicóptero detectado no alcance ({ObterRaioBuscaHelisEfetivo():F0}m).</color>");
        }
        else
        {
            int limiteHelisProximos = Mathf.Min(modoDesempenhoUI ? 2 : 4, _helisProximosCache.Count);
            for (int i = 0; i < limiteHelisProximos; i++)
            {
                Helicoptero heliProximo = _helisProximosCache[i];
                if (heliProximo == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"🚁 {CompactarTextoMenu(heliProximo.ObterRotuloExibicao(), 24)}", _uiLabelCompacta, GUILayout.Width(270f));
                GUILayout.EndHorizontal();
            }
            if (_helisProximosCache.Count > limiteHelisProximos)
            {
                GUILayout.Label($"<color=grey>+{_helisProximosCache.Count - limiteHelisProximos} helicóptero(s) no alcance.</color>");
            }
        }
        GUILayout.EndVertical();
        GUILayout.Space(6);

        // Embarque de Helicópteros Isolado
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>🚁 EMBARQUE DE HELICÓPTEROS</b>");
        if (GUILayout.Button($"Puxar Helicópteros Próximos ({_qtdOperacao})", GUILayout.Height(24f))) IniciarPuxarHelicopteros(_qtdOperacao);
        GUILayout.EndVertical();

        DesenharPainelHelicopteroNavio();

        GUILayout.Space(6);

        // Desembarque e Liberação Unificado
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>📤 SAÍDA / DESEMBARQUE RÁPIDO</b>");

        string infoSelecao = ObterDescricaoSelecaoAtual();
        if (!string.IsNullOrEmpty(infoSelecao) && !modoDesempenhoUI)
            GUILayout.Label($"Selecionado: <color=yellow>{CompactarTextoMenu(infoSelecao, 48)}</color>", _uiLabelWrap);
        
        if (!modoDesempenhoUI && !string.IsNullOrEmpty(infoSelecao))
        {
            if (GUILayout.Button("Desembarcar SÓ O SELECIONADO", GUILayout.Height(24f)))
            {
                DispararDesembarquePeloMenu(1);
            }
            GUILayout.Space(4);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Desembarcar {_qtdOperacao} Soldado(s)", GUILayout.Height(24f))) IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Soldados, _qtdOperacao, 0);
        if (GUILayout.Button($"Desembarcar {_qtdOperacao} Veículo(s)", GUILayout.Height(24f))) IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Veiculos, _qtdOperacao, 0);
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button($"Desembarcar {_qtdOperacao} Helicóptero(s)", GUILayout.Height(24f))) IniciarLiberarHelicopteros(_qtdOperacao, 0);

        GUILayout.Space(6);
        if (GUILayout.Button("🔴 RETIRAR TODAS AS UNIDADES (TUDO) 🔴", GUILayout.Height(26f)))
        {
            IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Todos, int.MaxValue, 0);
            IniciarLiberarHelicopteros(int.MaxValue, 0);
        }
        GUILayout.EndVertical();

        if (Time.time < _mensagemAte && !string.IsNullOrEmpty(_mensagemHUD))
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=yellow>{_mensagemHUD}</color>");
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.skin.label.fontSize = oldLabelFont;
        GUI.skin.button.fontSize = oldButtonFont;
        GUI.skin.box.fontSize = oldBoxFont;
        GUI.skin.label.richText = oldLabelRichText;
        GUI.skin.box.richText = oldBoxRichText;
        GUI.skin.button.richText = oldButtonRichText;
    }

    private void PrepararEstilosUISeNecessario()
    {
        if (_skinCache == GUI.skin && _uiLinhaCompacta != null) return;

        _skinCache = GUI.skin;
        _uiLinhaCompacta = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            margin = new RectOffset(2, 2, 1, 1),
            padding = new RectOffset(6, 4, 2, 2)
        };

        _uiLabelCompacta = new GUIStyle(GUI.skin.label)
        {
            margin = new RectOffset(2, 2, 1, 1),
            padding = new RectOffset(2, 2, 0, 0)
        };

        _uiLabelWrap = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };
    }

    private void AtualizarContagemCargaSeNecessario()
    {
        if (!_contagemCargaSuja && Time.frameCount % 10 != 0) return;
        _contagemCargaSuja = false;

        _veiculosAtualCache = _veiculosCarregados.Count;
        _soldadosAtualCache = _soldadosCarregados.Count;
        _aereosAtualCache = _helisCarregados.Count;
    }

    // ======================================================
    // Operações
    // ======================================================

    private enum ModoOperacaoTerrestre
    {
        Veiculos,
        Soldados,
        Todos
    }

    private void IniciarEmbarqueTerrestre(ModoOperacaoTerrestre modo, int qtd)
    {
        qtd = Mathf.Max(1, qtd);

        if (_operacaoTerrestreAtiva)
        {
            MostrarMensagem("⏳ Operação terrestre já em andamento.");
            return;
        }

        if (!temReferenciasTerrestres())
        {
            MostrarMensagem("❌ ERRO: faltam referências (corredor1/chegada/saida-entrada).");
            return;
        }

        if (!TemPontoEmTerra())
        {
            MostrarMensagem("⚠️ Sem ponto em terra para operar.");
            return;
        }

        StartCoroutine(RotinaEmbarqueTerrestre(modo, qtd));
    }

    private void IniciarDesembarqueTerrestre(ModoOperacaoTerrestre modo, int qtd, int startIndex)
    {
        qtd = Mathf.Max(1, qtd);

        if (_operacaoTerrestreAtiva)
        {
            MostrarMensagem("⏳ Operação terrestre já em andamento.");
            return;
        }

        if (!temReferenciasTerrestres())
        {
            MostrarMensagem("❌ ERRO: faltam referências (corredor1/chegada/saida-entrada).");
            return;
        }

        if (!TemPontoEmTerra())
        {
            MostrarMensagem("⚠️ Sem ponto em terra para operar.");
            return;
        }

        StartCoroutine(RotinaDesembarqueTerrestre(modo, qtd, Mathf.Max(0, startIndex)));
    }

    private void DispararDesembarquePeloMenu(int qtd)
    {
        if (_categoriaSelecionada == CategoriaSelecao.Veiculo && VeiculosAtual > 0)
        {
            int start = _indiceSelecionadoVeiculo >= 0 ? _indiceSelecionadoVeiculo : 0;
            IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Veiculos, qtd, start);
            return;
        }

        if (_categoriaSelecionada == CategoriaSelecao.Soldado && SoldadosAtual > 0)
        {
            int start = _indiceSelecionadoSoldado >= 0 ? _indiceSelecionadoSoldado : 0;
            IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Soldados, qtd, start);
            return;
        }

        if (_categoriaSelecionada == CategoriaSelecao.Heli && AereosAtual > 0)
        {
            int start = _indiceSelecionadoHeli >= 0 ? _indiceSelecionadoHeli : 0;
            IniciarLiberarHelicopteros(qtd, start);
            return;
        }

        MostrarMensagem("Nenhuma unidade válida selecionada.");
    }

    private void IniciarPuxarHelicopteros(int qtd)
    {
        qtd = Mathf.Max(1, qtd);

        if (_operacaoHeliAtiva)
        {
            MostrarMensagem("⏳ Operação de helicópteros já em andamento.");
            return;
        }

        if (!temReferenciasHeli())
        {
            MostrarMensagem("❌ ERRO: faltam referências de helicópteros (Pouso/decolagem + Paradas).");
            return;
        }

        StartCoroutine(RotinaPuxarHelicopteros(qtd));
    }

    private void IniciarLiberarHelicopteros(int qtd, int forcarStart = -1)
    {
        qtd = Mathf.Max(1, qtd);

        if (_operacaoHeliAtiva)
        {
            MostrarMensagem("⏳ Operação de helicópteros já em andamento.");
            return;
        }

        if (_helisCarregados.Count == 0)
        {
            MostrarMensagem("⚠️ Nenhum helicóptero no convés.");
            return;
        }

        if (!temReferenciasHeli())
        {
            MostrarMensagem("❌ ERRO: faltam referências de helicópteros (Pouso/decolagem + Paradas).");
            return;
        }

        int start = forcarStart >= 0 ? forcarStart : (_indiceSelecionadoHeli >= 0 ? _indiceSelecionadoHeli : 0);
        StartCoroutine(RotinaLiberarHelicopteros(qtd, start));
    }

    private int _caminhantesPendentes = 0;

    private IEnumerator MonitorarEEmbarcarIndividual(GameObject u, bool ehSoldado, Transform pontoFila)
    {
        _caminhantesPendentes++;
        float timeoutLimit = ehSoldado ? 20f : 30f;
        
        bool chegou = false;
        float timer = 0f;
        while (timer < timeoutLimit && u != null && u.activeInHierarchy && _operacaoTerrestreAtiva)
        {
            timer += Time.deltaTime;
            Vector3 posDiff = u.transform.position - pontoFila.position;
            posDiff.y = 0f;

            if (posDiff.magnitude <= raioPuxarAbsoluto)
            {
                chegou = true;
                break;
            }

            var nav = u.GetComponent<NavMeshAgent>();
            if (nav != null && nav.enabled && nav.isOnNavMesh && !nav.pathPending)
            {
                if (nav.remainingDistance <= nav.stoppingDistance + 1f && posDiff.magnitude <= raioPuxarPresoNavMesh)
                {
                    chegou = true;
                    break;
                }
            }
            yield return null;
        }

        if (chegou && u != null && u.activeInHierarchy && _operacaoTerrestreAtiva)
        {
            yield return StartCoroutine(EmbarcarUmaUnidadeTerrestre(u, ehSoldado));
        }
        else if (u != null && u.activeInHierarchy)
        {
            MostrarMensagem($"⚠️ Tempo esgotado para {LimparClone(u.name)} entrar.");
            var nav = u.GetComponent<NavMeshAgent>();
            if (nav != null && nav.isOnNavMesh) nav.ResetPath();
        }

        _caminhantesPendentes--;
    }

    private IEnumerator RotinaEmbarqueTerrestre(ModoOperacaoTerrestre modo, int qtd)
    {
        _operacaoTerrestreAtiva = true;
        MostrarMensagem("📥 Iniciando embarque terrestre...");

        AbrirPortaTerrestre();

        List<Transform> pontosTerra = ObterPontosSaidaEmTerra();
        if (pontosTerra.Count == 0)
        {
            MostrarMensagem("⚠️ Sem ponto em terra disponível.");
            _operacaoTerrestreAtiva = false;
            FecharPortaTerrestre();
            yield break;
        }

        List<GameObject> candidatos = ColetarTerrestresProximos(modo);
        if (candidatos.Count == 0)
        {
            MostrarMensagem("⚠️ Nenhuma unidade terrestre próxima para puxar.");
            _operacaoTerrestreAtiva = false;
            FecharPortaTerrestre();
            yield break;
        }

        int embarcados = 0;
        _caminhantesPendentes = 0;

        for (int i = 0; i < candidatos.Count; i++)
        {
            if (!_operacaoTerrestreAtiva || embarcados >= qtd) break;

            GameObject u = candidatos[i];
            if (u == null || !u.activeInHierarchy) continue;

            bool ehSoldado = EhSoldado(u);
            if (ehSoldado && SoldadosAtual >= capacidadeMaxSoldados) continue;
            if (!ehSoldado && VeiculosAtual >= capacidadeMaxVeiculos) continue;

            Transform pontoFila = EscolherPontoFilaMaisProximo(u.transform.position, pontosTerra);
            if (pontoFila == null) break;

            MostrarMensagem($"➡️ Chamando {LimparClone(u.name)}...");
            OrdenarIrPara(u, pontoFila.position);

            StartCoroutine(MonitorarEEmbarcarIndividual(u, ehSoldado, pontoFila));
            embarcados++;
            _contagemCargaSuja = true;

            float tempoEspera = ehSoldado ? 2f : 7f;
            float timerEsp = 0f;
            while(timerEsp < tempoEspera && _operacaoTerrestreAtiva)
            {
                timerEsp += Time.deltaTime;
                yield return null;
            }
        }

        while (_caminhantesPendentes > 0 && _operacaoTerrestreAtiva)
        {
            yield return null;
        }

        if (_operacaoTerrestreAtiva)
        {
            FecharPortaTerrestre();
            _operacaoTerrestreAtiva = false;
            MostrarMensagem("✅ Embarque terrestre finalizado.");
        }
    }

    private IEnumerator RotinaDesembarqueTerrestre(ModoOperacaoTerrestre modo, int qtd, int startIndex)
    {
        _operacaoTerrestreAtiva = true;
        MostrarMensagem("📤 Iniciando desembarque terrestre...");

        AbrirPortaTerrestre();

        List<Transform> pontosTerra = ObterPontosSaidaEmTerra();
        if (pontosTerra.Count == 0)
        {
            MostrarMensagem("⚠️ Sem ponto em terra para sair.");
            _operacaoTerrestreAtiva = false;
            FecharPortaTerrestre();
            yield break;
        }

        List<CargaTerrestre> filaSaida = new List<CargaTerrestre>();
        if (modo == ModoOperacaoTerrestre.Veiculos || modo == ModoOperacaoTerrestre.Todos)
        {
            AdicionarTerrestresParaSaida(filaSaida, _veiculosCarregados, startIndex, qtd);
        }

        if (modo == ModoOperacaoTerrestre.Soldados || modo == ModoOperacaoTerrestre.Todos)
        {
            int restante = qtd == int.MaxValue ? int.MaxValue : Mathf.Max(0, qtd - filaSaida.Count);
            AdicionarTerrestresParaSaida(filaSaida, _soldadosCarregados, startIndex, restante);
        }

        if (filaSaida.Count == 0)
        {
            MostrarMensagem("⚠️ Nenhuma unidade terrestre para desembarcar.");
            _operacaoTerrestreAtiva = false;
            FecharPortaTerrestre();
            yield break;
        }

        Vector3 localChegada = _containerCarga.InverseTransformPoint(chegada.position);
        Vector3 localCorredor = _containerCarga.InverseTransformPoint(corredor1.position);

        for (int i = 0; i < filaSaida.Count; i++)
        {
            if (!_operacaoTerrestreAtiva) break;

            var carga = filaSaida[i];
            if (carga == null || carga.unidade == null) continue;

            GameObject u = carga.unidade;
            u.SetActive(true);
            u.transform.SetParent(_containerCarga, true);
            u.transform.localPosition = localChegada;
            u.transform.rotation = transform.rotation;

            yield return StartCoroutine(MoverLocalAte(u.transform, localCorredor, velocidadeMovimentoInterno, timeoutMoverInterno));

            Transform saida = EscolherPontoSaidaMaisProximo(corredor1.position, pontosTerra);
            if (saida == null) break;

            Vector3 destino = saida.position;
            destino = AjustarParaNavMeshSePossivel(destino);

            u.transform.SetParent(null, true);
            u.transform.position = destino;
            u.transform.rotation = transform.rotation;

            RestaurarUnidadeTerrestre(u, carga, destino);
            RemoverCargaTerrestre(carga);
            _contagemCargaSuja = true;

            float tempoEspera = EhSoldado(u) ? 2f : 7f;
            float timerEsp = 0f;
            while(timerEsp < tempoEspera && _operacaoTerrestreAtiva)
            {
                timerEsp += Time.deltaTime;
                yield return null;
            }
        }

        if (_operacaoTerrestreAtiva)
        {
            FecharPortaTerrestre();
            _operacaoTerrestreAtiva = false;
            MostrarMensagem("✅ Desembarque terrestre finalizado.");
        }
    }

    private IEnumerator RotinaPuxarHelicopteros(int qtd)
    {
        _operacaoHeliAtiva = true;
        MostrarMensagem("🚁 Puxando helicópteros...");

        if (AereosAtual >= capacidadeMaxAereos)
        {
            MostrarMensagem("🚁 Capacidade de helicópteros cheia.");
            _operacaoHeliAtiva = false;
            yield break;
        }

        _helisCandidatosOperacao.Clear();
        ColetarHelisProximos(_helisCandidatosOperacao);
        List<Helicoptero> candidatos = _helisCandidatosOperacao;
        if (candidatos.Count == 0)
        {
            MostrarMensagem("⚠️ Nenhum helicóptero próximo.");
            _operacaoHeliAtiva = false;
            yield break;
        }

        int puxados = 0;
        for (int i = 0; i < candidatos.Count; i++)
        {
            if (puxados >= qtd) break;
            if (AereosAtual >= capacidadeMaxAereos) break;

            Helicoptero h = candidatos[i];
            if (h == null) continue;
            if (JaTenhoHeli(h)) continue;

            Transform paradaLivre = ObterProximaParadaLivre();
            if (paradaLivre == null)
            {
                MostrarMensagem("⚠️ Sem Parada livre para estacionar helicópteros.");
                break;
            }

            if (!EstacionarHelicopteroNoNavio(h, paradaLivre, false))
            {
                continue;
            }

            MostrarMensagem($"➡️ {h.ObterRotuloExibicao()} alinhando em Pouso/decolagem para depois pousar em {paradaLivre.name}.");
            puxados++;
            _contagemCargaSuja = true;
            yield return new WaitForSeconds(delayEntreUnidades);
        }

        AtualizarCacheHelisProximos();
        _operacaoHeliAtiva = false;
        MostrarMensagem("✅ Operação de helicópteros concluída.");
    }

    private IEnumerator RotinaLiberarHelicopteros(int qtd, int startIndex)
    {
        _operacaoHeliAtiva = true;
        MostrarMensagem("🚁 Liberando helicópteros...");

        int liberados = 0;
        for (int pass = 0; pass < _helisCarregados.Count && liberados < qtd; pass++)
        {
            if (_helisCarregados.Count == 0) break;
            int idx = (startIndex + pass) % _helisCarregados.Count;

            var carga = _helisCarregados[idx];
            if (carga == null || carga.heli == null) continue;
            if (carga.emSaida) continue;

            if (!IniciarSaidaHelicopteroDoNavio(
                carga.heli,
                heliLiberado => heliLiberado.RetornarParaVagaAeroporto(),
                "retornando ao aeroporto"))
            {
                continue;
            }

            MostrarMensagem($"➡️ {carga.heli.ObterRotuloExibicao()} saiu da vaga e vai para Pouso/decolagem.");
            liberados++;
            _contagemCargaSuja = true;
            yield return new WaitForSeconds(delayEntreUnidades);
        }

        _operacaoHeliAtiva = false;
        MostrarMensagem("✅ Helicópteros liberados pelo corredor aéreo do navio.");
    }

    // ======================================================
    // Embarque/Desembarque unitário
    // ======================================================

    private IEnumerator EmbarcarUmaUnidadeTerrestre(GameObject u, bool ehSoldado)
    {
        if (u == null) yield break;
        if (_containerCarga == null) GarantirContainerCarga();

        var carga = PrepararUnidadeParaCarga(u);
        carga.parentOriginal = u.transform.parent;

        Vector3 localCorredor = _containerCarga.InverseTransformPoint(corredor1.position);
        Vector3 localChegada = _containerCarga.InverseTransformPoint(chegada.position);

        u.transform.SetParent(_containerCarga, true);
        u.transform.localPosition = localCorredor;
        u.transform.rotation = transform.rotation;

        yield return StartCoroutine(MoverLocalAte(u.transform, localChegada, velocidadeMovimentoInterno, timeoutMoverInterno));

        if (ehSoldado) _soldadosCarregados.Add(carga);
        else _veiculosCarregados.Add(carga);

        // Recarregar vida e combustível
        SistemaDeDanos dano = u.GetComponent<SistemaDeDanos>();
        if (dano != null) dano.vidaAtual = dano.vidaMaxima;
        CombustivelUnidade comb = u.GetComponent<CombustivelUnidade>();
        if (comb != null) comb.PreencherSemCusto();

        u.SetActive(false);
        _contagemCargaSuja = true;
    }

    private void RemoverCargaTerrestre(CargaTerrestre carga)
    {
        if (carga == null) return;
        _veiculosCarregados.Remove(carga);
        _soldadosCarregados.Remove(carga);
        _contagemCargaSuja = true;
    }

    public int TransferirSoldadosParaHelicoptero(Helicoptero heli, int quantidadeMax = int.MaxValue)
    {
        if (heli == null || quantidadeMax <= 0) return 0;

        int transferidos = 0;
        for (int i = _soldadosCarregados.Count - 1; i >= 0 && transferidos < quantidadeMax; i--)
        {
            CargaTerrestre carga = _soldadosCarregados[i];
            if (carga == null || carga.unidade == null)
            {
                _soldadosCarregados.RemoveAt(i);
                continue;
            }
            if (!heli.EmbarcarSoldadoTransferido(carga.unidade)) continue;
            RemoverCargaTerrestre(carga);
            transferidos++;
        }
        return transferidos;
    }

    public bool EmbarcarSoldadoTransferidoDoHelicoptero(GameObject soldado)
    {
        if (soldado == null || SoldadosAtual >= capacidadeMaxSoldados) return false;
        if (_containerCarga == null) GarantirContainerCarga();

        CargaTerrestre carga = PrepararSoldadoTransferidoDoHelicoptero(soldado);
        soldado.transform.SetParent(_containerCarga, true);

        if (chegada != null)
        {
            soldado.transform.localPosition = _containerCarga.InverseTransformPoint(chegada.position);
            soldado.transform.rotation = transform.rotation;
        }

        // Recarregar vida e combustível
        SistemaDeDanos dano = soldado.GetComponent<SistemaDeDanos>();
        if (dano != null) dano.vidaAtual = dano.vidaMaxima;
        CombustivelUnidade comb = soldado.GetComponent<CombustivelUnidade>();
        if (comb != null) comb.PreencherSemCusto();

        soldado.SetActive(false);
        _soldadosCarregados.Add(carga);
        _contagemCargaSuja = true;
        return true;
    }

    public int TransferirSoldadosDoHelicopteroParaNavio(Helicoptero heli, int quantidadeMax = int.MaxValue)
    {
        if (heli == null || quantidadeMax <= 0) return 0;
        return heli.TransferirSoldadosParaNavio(this, quantidadeMax);
    }

    private CargaTerrestre PrepararSoldadoTransferidoDoHelicoptero(GameObject unidade)
    {
        var carga = new CargaTerrestre { unidade = unidade, parentOriginal = unidade.transform.parent };
        var ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            carga.controleEnabledAntes = true;
            try { ctrl.DefinirSelecao(false); } catch { }
            ctrl.enabled = false;
        }
        else carga.controleEnabledAntes = false;

        var nav = unidade.GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            carga.navAgentEnabledAntes = true;
            try { if (nav.enabled && nav.isOnNavMesh) nav.ResetPath(); nav.isStopped = true; } catch { }
            nav.enabled = false;
        }
        else carga.navAgentEnabledAntes = false;

        var rb = unidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            carga.rbExiste = true;
            carga.rbKinematicAntes = rb.isKinematic;
            carga.rbUseGravityAntes = rb.useGravity;
            carga.rbDetectCollisionsAntes = rb.detectCollisions;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else carga.rbExiste = false;

        return carga;
    }

    private void RestaurarUnidadeTerrestre(GameObject u, CargaTerrestre carga, Vector3 destinoFinal)
    {
        if (u == null || carga == null) return;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            try { ctrl.DefinirSelecao(false); } catch { }
            ctrl.enabled = carga.controleEnabledAntes;
        }

        var rb = u.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = carga.rbKinematicAntes;
            rb.useGravity = carga.rbUseGravityAntes;
            rb.detectCollisions = carga.rbDetectCollisionsAntes;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var nav = u.GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            nav.enabled = carga.navAgentEnabledAntes;
            if (nav.enabled)
            {
                Vector3 alvo = destinoFinal;
                if (NavMesh.SamplePosition(destinoFinal, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                {
                    alvo = hit.position;
                }
                try { nav.Warp(alvo); nav.isStopped = false; } catch { }
            }
        }
    }

    private CargaTerrestre PrepararUnidadeParaCarga(GameObject u)
    {
        var carga = new CargaTerrestre { unidade = u };

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            carga.controleEnabledAntes = ctrl.enabled;
            try { ctrl.DefinirSelecao(false); } catch { }
            ctrl.enabled = false;
        }
        else carga.controleEnabledAntes = false;

        var nav = u.GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            carga.navAgentEnabledAntes = nav.enabled;
            try { if (nav.enabled) { if (nav.isOnNavMesh) nav.ResetPath(); nav.isStopped = true; } } catch { }
            nav.enabled = false;
        }
        else carga.navAgentEnabledAntes = false;

        var rb = u.GetComponent<Rigidbody>();
        if (rb != null)
        {
            carga.rbExiste = true;
            carga.rbKinematicAntes = rb.isKinematic;
            carga.rbUseGravityAntes = rb.useGravity;
            carga.rbDetectCollisionsAntes = rb.detectCollisions;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else carga.rbExiste = false;

        return carga;
    }

    private IEnumerator MoverLocalAte(Transform obj, Vector3 destinoLocal, float velocidade, float timeout)
    {
        if (obj == null) yield break;

        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            Vector3 atual = obj.localPosition;
            Vector3 prox = Vector3.MoveTowards(atual, destinoLocal, velocidade * Time.deltaTime);
            obj.localPosition = prox;

            Vector3 dir = (destinoLocal - prox);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(transform.TransformDirection(dir.normalized), Vector3.up);
                obj.rotation = Quaternion.Slerp(obj.rotation, rot, Time.deltaTime * 8f);
            }

            if ((prox - destinoLocal).sqrMagnitude <= 0.01f)
            {
                obj.localPosition = destinoLocal;
                yield break;
            }
            yield return null;
        }
        obj.localPosition = destinoLocal;
    }

    // ======================================================
    // Seleção / UI
    // ======================================================
    private void ProcessarInputMenu()
    {
        if (_idNavio != null && _idNavio.teamID != 1) return;

        if (_controleUnidade != null && _controleUnidade.selecionado && Input.GetKeyDown(KeyCode.O))
        {
            bool novoEstado = !_menuAberto;
            if (novoEstado)
            {
                GestorMenusExclusivos.Abrir(this);
            }
            else
            {
                GestorMenusExclusivos.Fechar(this);
            }

            _menuAberto = novoEstado;
            if (_menuAberto)
            {
                AtualizarCacheHelisProximos();
                _proximoScanHeli = Time.time + 1f;
            }
        }
    }

    public bool PossuiOrdemManualAtiva()
    {
        return _modoOrdemHelicoptero != ModoOrdemHelicopteroNavio.Nenhum && _helicopteroSelecionadoParaMissao != null;
    }

    public void CancelarInteracaoPorConstrucao()
    {
        EncerrarModoHelicopteroNavio();
        _menuAberto = false;
        AtualizarModoInteracaoHelicopteroNavio();
    }

    private Helicoptero ObterHelicopteroSelecionadoNoNavio()
    {
        if (_helicopteroSelecionadoParaMissao != null && JaTenhoHeli(_helicopteroSelecionadoParaMissao))
        {
            return _helicopteroSelecionadoParaMissao;
        }

        if (_categoriaSelecionada == CategoriaSelecao.Heli && _indiceSelecionadoHeli >= 0 && _indiceSelecionadoHeli < _helisCarregados.Count)
        {
            Helicoptero heli = _helisCarregados[_indiceSelecionadoHeli]?.heli;
            if (heli != null)
            {
                _helicopteroSelecionadoParaMissao = heli;
                return heli;
            }
        }
        return null;
    }

    private bool TryResolverPontoMapaNavio(out Vector3 pontoAlvo)
    {
        pontoAlvo = Vector3.zero;
        if (_cameraPrincipal == null) _cameraPrincipal = Camera.main;
        if (_cameraPrincipal == null) return false;

        Ray raio = _cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(raio, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
            return true;
        }

        UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        if (plano.Raycast(raio, out float distancia))
        {
            pontoAlvo = raio.GetPoint(distancia);
            return true;
        }
        return false;
    }

    private void IniciarModoHelicopteroNavio(Helicoptero heli, ModoOrdemHelicopteroNavio modo)
    {
        if (heli == null) return;
        _helicopteroSelecionadoParaMissao = heli;
        _modoOrdemHelicoptero = modo;
        _rotaPatrulhaHelicoptero.Clear();
        _menuAberto = false;
        AtualizarModoInteracaoHelicopteroNavio();
    }

    private void EncerrarModoHelicopteroNavio()
    {
        _rotaPatrulhaHelicoptero.Clear();
        _modoOrdemHelicoptero = ModoOrdemHelicopteroNavio.Nenhum;
        _helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoHelicopteroNavio();
    }

    private void AtualizarModoInteracaoHelicopteroNavio()
    {
        if (PossuiOrdemManualAtiva())
        {
            if (!_interacaoHeliSolicitada || !InteractionModeService.IsActive(this, InteractionOwner.CarrierOrder))
            {
                InteractionModeService.Request(
                    this,
                    InteractionOwner.CarrierOrder,
                    new InteractionPolicy
                    {
                        bloqueiaSelecao = true,
                        bloqueiaOrdemMundo = true,
                        bloqueiaRotacaoCamera = true,
                        consomeLMB = false,
                        consomeRMB = true
                    },
                    "Ordem manual de helicóptero naval aguardando clique");
            }
            _interacaoHeliSolicitada = true;
        }
        else if (_interacaoHeliSolicitada)
        {
            InteractionModeService.Release(this, InteractionOwner.CarrierOrder);
            _interacaoHeliSolicitada = false;
        }
    }

    private void ProcessarOrdemHelicopteroNavio()
    {
        if (_helicopteroSelecionadoParaMissao == null)
        {
            EncerrarModoHelicopteroNavio();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EncerrarModoHelicopteroNavio();
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Patrulha)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EncerrarModoHelicopteroNavio();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _rotaPatrulhaHelicoptero.Count > 0)
            {
                _rotaPatrulhaHelicoptero.RemoveAt(_rotaPatrulhaHelicoptero.Count - 1);
                if (_rotaPatrulhaHelicoptero.Count > 0)
                {
                    if (!JaTenhoHeli(_helicopteroSelecionadoParaMissao) && !HelicopteroEstaEmSaidaDoConves(_helicopteroSelecionadoParaMissao))
                    {
                        _helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
                    }
                }
                else
                {
                    _helicopteroSelecionadoParaMissao.CancelarMissaoAeroporto();
                }
                return;
            }
        }

        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;
        if (!TryResolverPontoMapaNavio(out Vector3 pontoAlvo)) return;

        if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Patrulha)
        {
            _rotaPatrulhaHelicoptero.Add(pontoAlvo);
            if (JaTenhoHeli(_helicopteroSelecionadoParaMissao))
            {
                IniciarSaidaHelicopteroDoNavio(
                    _helicopteroSelecionadoParaMissao,
                    heliLiberado =>
                    {
                        if (_rotaPatrulhaHelicoptero.Count > 0)
                        {
                            PrepararHelicopteroParaPatrulhaCombate(heliLiberado);
                            heliLiberado.IniciarPatrulhaAeroporto(new List<Vector3>(_rotaPatrulhaHelicoptero));
                        }
                        else
                            heliLiberado.RetornarParaVagaAeroporto();
                    },
                    "iniciando patrulha");
            }
            else
            {
                PrepararHelicopteroParaPatrulhaCombate(_helicopteroSelecionadoParaMissao);
                _helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
            }
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Reconhecimento)
        {
            if (JaTenhoHeli(_helicopteroSelecionadoParaMissao))
            {
                IniciarSaidaHelicopteroDoNavio(
                    _helicopteroSelecionadoParaMissao,
                    heliLiberado => heliLiberado.IniciarReconhecimentoAeroporto(pontoAlvo),
                    "indo para reconhecimento");
            }
            else _helicopteroSelecionadoParaMissao.IniciarReconhecimentoAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.AtaqueLocal)
        {
            if (JaTenhoHeli(_helicopteroSelecionadoParaMissao))
            {
                IniciarSaidaHelicopteroDoNavio(
                    _helicopteroSelecionadoParaMissao,
                    heliLiberado => heliLiberado.IniciarAtaqueLocalAeroporto(pontoAlvo),
                    "indo para ataque local");
            }
            else _helicopteroSelecionadoParaMissao.IniciarAtaqueLocalAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Transporte)
        {
            if (JaTenhoHeli(_helicopteroSelecionadoParaMissao))
            {
                IniciarSaidaHelicopteroDoNavio(
                    _helicopteroSelecionadoParaMissao,
                    heliLiberado => heliLiberado.IniciarTransporteAeroporto(pontoAlvo),
                    "indo para transporte");
            }
            else _helicopteroSelecionadoParaMissao.IniciarTransporteAeroporto(pontoAlvo);
        }

        EncerrarModoHelicopteroNavio();
    }

    private void DesenharPainelHelicopteroNavio()
    {
        Helicoptero heli = ObterHelicopteroSelecionadoNoNavio();
        if (heli == null) return;
        bool heliProntoNoConves = HelicopteroPodeTransferirNoConves(heli);
        int qtdTransferencia = ObterQuantidadeOperacaoAtual();

        GUILayout.Space(6);
        GUILayout.BeginVertical("box");
        string nomeHeli = CompactarTextoMenu(heli.ObterRotuloExibicao(), 28);
        GUILayout.Label($"<b>ORDENS DO HELICÓPTERO: 🚁 {nomeHeli}</b>");
        GUILayout.Label($"<color=cyan>{heli.ObterEstadoOperacionalAeroporto()}</color>");

        if (_modoOrdemHelicoptero != ModoOrdemHelicopteroNavio.Nenhum)
        {
            if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Patrulha)
                GUILayout.Label($"<color=yellow>PATRULHA ATIVA: clique direito adiciona pontos ({_rotaPatrulhaHelicoptero.Count}). ENTER encerra, BACKSPACE desfaz, ESC cancela.</color>");
            else
            {
                string textoModo = "ATAQUE LOCAL";
                if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Reconhecimento) textoModo = "RECONHECIMENTO";
                else if (_modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Transporte) textoModo = "TRANSPORTE";
                GUILayout.Label($"<color=yellow>{textoModo} ATIVO: clique direito no mapa. ESC cancela.</color>");
            }

            if (GUILayout.Button("❌ Cancelar Ordem Helicóptero", GUILayout.Height(24f)))
                EncerrarModoHelicopteroNavio();
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(28f))) IniciarModoHelicopteroNavio(heli, ModoOrdemHelicopteroNavio.Reconhecimento);
            if (GUILayout.Button("🛡️ Patrulha", GUILayout.Height(28f))) IniciarModoHelicopteroNavio(heli, ModoOrdemHelicopteroNavio.Patrulha);
            if (GUILayout.Button("💥 Ataque local", GUILayout.Height(28f))) IniciarModoHelicopteroNavio(heli, ModoOrdemHelicopteroNavio.AtaqueLocal);
            GUILayout.EndHorizontal();

            if (heli.EhHelicopteroTransporte())
            {
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🛬 Transporte", GUILayout.Height(26f))) IniciarModoHelicopteroNavio(heli, ModoOrdemHelicopteroNavio.Transporte);
                GUILayout.Label($"Tropas: {heli.soldadosEmbarcados.Count}/{heli.capacidadeMaxima}", GUILayout.Width(120f));
                GUILayout.EndHorizontal();

                if (heli.PodeOperarTropasNoMenu())
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled = heli.TemEspaco() > 0;
                    if (GUILayout.Button("📥 Recolher tropas", GUILayout.Height(24f)))
                    {
                        int recolhidas = heli.RecolherTropasPeloMenu();
                        MostrarMensagem(recolhidas > 0 ? $"📥 {heli.ObterRotuloExibicao()} recolheu {recolhidas} tropa(s)." : $"⚠️ Sem tropas para recolher.");
                    }
                    GUI.enabled = heli.TemSoldados();
                    if (GUILayout.Button("📤 Desembarcar", GUILayout.Height(24f)))
                    {
                        int desembarcadas = heli.DesembarcarTropasNoLocalAtual();
                        MostrarMensagem(desembarcadas > 0 ? $"📤 {heli.ObterRotuloExibicao()} desembarcou {desembarcadas} tropa(s)." : $"⚠️ Sem tropas para desembarcar.");
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4f);
                    GUILayout.Label($"Navio: {SoldadosAtual}/{capacidadeMaxSoldados} | Helicóptero: {heli.soldadosEmbarcados.Count}/{heli.capacidadeMaxima}");
                    GUILayout.BeginHorizontal();
                    GUI.enabled = heliProntoNoConves && SoldadosAtual > 0 && heli.TemEspaco() > 0;
                    if (GUILayout.Button($"⬆️ Navio -> Heli ({_qtdOperacao})", GUILayout.Height(24f)))
                    {
                        int transferidos = TransferirSoldadosParaHelicoptero(heli, qtdTransferencia);
                        MostrarMensagem(transferidos > 0 ? $"⬆️ {transferidos} soldado(s) transferidos." : $"⚠️ Falha na transferência.");
                    }
                    GUI.enabled = heliProntoNoConves && heli.TemSoldados() && SoldadosAtual < capacidadeMaxSoldados;
                    if (GUILayout.Button($"⬇️ Heli -> Navio ({_qtdOperacao})", GUILayout.Height(24f)))
                    {
                        int transferidos = TransferirSoldadosDoHelicopteroParaNavio(heli, qtdTransferencia);
                        MostrarMensagem(transferidos > 0 ? $"⬇️ {transferidos} soldado(s) transferidos." : $"⚠️ Falha na transferência.");
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                    if (!heliProntoNoConves) GUILayout.Label("<color=grey>Transferência direta só no convés.</color>");
                }
            }
        }
        GUILayout.EndVertical();
    }

    private string ObterDescricaoSelecaoAtual()
    {
        if (_categoriaSelecionada == CategoriaSelecao.Veiculo && _indiceSelecionadoVeiculo >= 0 && _indiceSelecionadoVeiculo < _veiculosCarregados.Count)
        {
            var u = _veiculosCarregados[_indiceSelecionadoVeiculo]?.unidade;
            return u != null ? LimparClone(u.name) : string.Empty;
        }

        if (_categoriaSelecionada == CategoriaSelecao.Soldado && _indiceSelecionadoSoldado >= 0 && _indiceSelecionadoSoldado < _soldadosCarregados.Count)
        {
            var u = _soldadosCarregados[_indiceSelecionadoSoldado]?.unidade;
            return u != null ? LimparClone(u.name) : string.Empty;
        }

        if (_categoriaSelecionada == CategoriaSelecao.Heli && _indiceSelecionadoHeli >= 0 && _indiceSelecionadoHeli < _helisCarregados.Count)
        {
            var h = _helisCarregados[_indiceSelecionadoHeli]?.heli;
            return h != null ? LimparClone(h.name) : string.Empty;
        }
        return string.Empty;
    }

    // ======================================================
    // Detecção / Helpers
    // ======================================================
    private void AutoDetectarReferencias(bool forcar)
    {
        Transform entradaTerrestre = EncontrarPorNome("Entrada terrestre");
        if (entradaTerrestre != null)
        {
            if (forcar || portaTerrestre == null) portaTerrestre = EncontrarFilhoPorNome(entradaTerrestre, "Porta");
            if (portaTerrestre != null)
            {
                if (forcar || fila == null) fila = EncontrarFilhoPorNome(portaTerrestre, "fila");

                if ((forcar || pontosSaidaEntrada == null || pontosSaidaEntrada.Length == 0) && fila != null)
                {
                    List<Transform> lista = new List<Transform>();
                    foreach (Transform t in fila.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == null || t == fila) continue;
                        if (NomeNormalizado(t.name).Contains("saida/entrada")) lista.Add(t);
                    }
                    pontosSaidaEntrada = OrdenarPorIndiceParenteses(lista).ToArray();
                }
            }
        }

        Transform corredor = EncontrarPorNome("Corredor");
        if (corredor != null)
        {
            if (forcar || corredor1 == null) corredor1 = EncontrarFilhoPorNome(corredor, "Corredor1");
            if (forcar || chegada == null) chegada = EncontrarFilhoPorNome(corredor, "chegada");
        }

        if (forcar || pista == null) pista = EncontrarPorNome("Pista");
        if (pista != null && (forcar || pontoPousoDecolagemHeli == null))
        {
            pontoPousoDecolagemHeli = EncontrarPontoPousoDecolagemHeli();
        }
        if (pista != null && (forcar || paradasHeli == null || paradasHeli.Length == 0))
        {
            List<Transform> listaParadas = new List<Transform>();
            foreach (Transform t in pista.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == pista) continue;
                if (NomeNormalizado(t.name).Contains("parada")) listaParadas.Add(t);
            }
            paradasHeli = OrdenarPorIndiceParenteses(listaParadas).ToArray();
        }

        Transform saidaHangar = EncontrarPorNome("Saida do hangar aereo");
        if (saidaHangar != null)
        {
            if (forcar || stop0 == null) stop0 = EncontrarFilhoPorNome(saidaHangar, "Stop");
            if (forcar || stop1 == null) stop1 = EncontrarFilhoPorNome(saidaHangar, "Stop (1)");
        }

        if (string.IsNullOrEmpty(descricaoParaIA)) PreencherDescricaoPadrao();
    }

    private Transform EncontrarPorNome(string nome)
    {
        string alvo = NomeNormalizado(nome);
        Transform[] todos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform t = todos[i];
            if (t == null) continue;
            if (NomeNormalizado(t.name) == alvo) return t;
        }
        return null;
    }

    private Transform EncontrarFilhoPorNome(Transform raiz, string nomeFilho)
    {
        if (raiz == null) return null;
        string alvo = NomeNormalizado(nomeFilho);
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == raiz) continue;
            if (NomeNormalizado(t.name) == alvo) return t;
        }
        return null;
    }

    private Transform EncontrarPontoPousoDecolagemHeli()
    {
        if (pista == null) return null;
        Transform exato = EncontrarFilhoPorNome(pista, "Pouso/decolagem");
        if (exato != null) return exato;
        foreach (Transform t in pista.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == pista) continue;
            string nome = NomeNormalizado(t.name);
            if (nome.Contains("pouso/decolagem") || (nome.Contains("pouso") && nome.Contains("decolagem"))) return t;
        }
        return null;
    }

    private string NomeNormalizado(string valor) => string.IsNullOrEmpty(valor) ? string.Empty : valor.Replace(" ", string.Empty).ToLowerInvariant();

    private List<Transform> OrdenarPorIndiceParenteses(List<Transform> pontos) => pontos.Where(p => p != null).OrderBy(p => ExtrairIndiceParentesesOuZero(p.name)).ToList();

    private int ExtrairIndiceParentesesOuZero(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return 0;
        int a = nome.IndexOf('(');
        int b = nome.IndexOf(')');
        if (a >= 0 && b > a)
        {
            string dentro = nome.Substring(a + 1, b - a - 1).Trim();
            if (int.TryParse(dentro, out int v)) return v;
        }
        return 0;
    }

    private string LimparClone(string nome) => string.IsNullOrEmpty(nome) ? string.Empty : nome.Replace("(Clone)", "").Trim();

    private static string CompactarTextoMenu(string texto, int maxChars)
    {
        if (string.IsNullOrEmpty(texto) || maxChars <= 0 || texto.Length <= maxChars) return texto;
        if (maxChars <= 3) return texto.Substring(0, maxChars);
        return texto.Substring(0, maxChars - 3) + "...";
    }

    private bool temReferenciasTerrestres() => corredor1 != null && chegada != null && pontosSaidaEntrada != null && pontosSaidaEntrada.Length > 0;

    private bool temReferenciasHeli() => pontoPousoDecolagemHeli != null && paradasHeli != null && paradasHeli.Length > 0;

    private void GarantirContainerCarga()
    {
        if (_containerCarga != null) return;
        Transform existente = transform.Find("_CargaInterna");
        if (existente == null)
        {
            GameObject go = new GameObject("_CargaInterna");
            existente = go.transform;
            existente.SetParent(transform, false);
            existente.localPosition = Vector3.zero;
            existente.localRotation = Quaternion.identity;
            existente.localScale = Vector3.one;
        }
        _containerCarga = existente;
    }

    private void PrepararPorta()
    {
        if (portaTerrestre == null) return;
        Vector3 euler = portaTerrestre.localEulerAngles;
        _portaRotX = euler.x;
        _portaRotY = euler.y;
        _portaZAtual = euler.z;
    }

    private void AbrirPortaTerrestre()
    {
        if (portaTerrestre == null) return;
        _portaZAtual = Mathf.Repeat(portaTerrestre.localEulerAngles.z, 360f);
    }

    private void FecharPortaTerrestre() { }

    private void AtualizarAnimacaoPorta()
    {
        if (portaTerrestre == null) return;
        float alvoZ = _operacaoTerrestreAtiva ? portaZAberta : portaZFechada;
        float alvoZN = Mathf.Repeat(alvoZ, 360f);
        float atualZN = Mathf.Repeat(_portaZAtual, 360f);
        atualZN = Mathf.LerpAngle(atualZN, alvoZN, Time.deltaTime * Mathf.Max(0.01f, velocidadeAnimacaoPorta));
        _portaZAtual = atualZN;
        portaTerrestre.localEulerAngles = new Vector3(_portaRotX, _portaRotY, atualZN);
    }

    private float _proximoLimparNulos = 0f;
    private void LimparNulos()
    {
        if (Time.time < _proximoLimparNulos) return;
        _proximoLimparNulos = Time.time + 1.0f;

        for (int i = _veiculosCarregados.Count - 1; i >= 0; i--)
        {
            if (_veiculosCarregados[i] == null || _veiculosCarregados[i].unidade == null)
                _veiculosCarregados.RemoveAt(i);
        }
        for (int i = _soldadosCarregados.Count - 1; i >= 0; i--)
        {
            if (_soldadosCarregados[i] == null || _soldadosCarregados[i].unidade == null)
                _soldadosCarregados.RemoveAt(i);
        }
        for (int i = _helisCarregados.Count - 1; i >= 0; i--)
        {
            if (_helisCarregados[i] == null || _helisCarregados[i].heli == null)
                _helisCarregados.RemoveAt(i);
        }
    }

    private void MostrarMensagem(string msg, float duracao = 4f)
    {
        _mensagemHUD = msg;
        _mensagemAte = Time.time + duracao;
        if (debugLogs) Debug.Log("[NavioTransporteTropas] " + msg);
    }

    private List<Transform> ObterPontosSaidaEmTerra()
    {
        List<Transform> lista = new List<Transform>();
        if (pontosSaidaEntrada == null) return lista;
        for (int i = 0; i < pontosSaidaEntrada.Length; i++)
        {
            Transform p = pontosSaidaEntrada[i];
            if (p == null) continue;
            if (PontoTemTerra(p.position)) lista.Add(p);
        }
        return lista;
    }

    private void AtualizarTemPontoEmTerraCache(bool forcar)
    {
        if (!forcar && Time.time < _proximaAtualizacaoTemTerra)
        {
            return;
        }

        _proximaAtualizacaoTemTerra = Time.time + 0.35f;
        _temPontoTerraCache = false;
        if (pontosSaidaEntrada == null)
        {
            return;
        }

        for (int i = 0; i < pontosSaidaEntrada.Length; i++)
        {
            Transform p = pontosSaidaEntrada[i];
            if (p == null)
            {
                continue;
            }

            if (PontoTemTerra(p.position))
            {
                _temPontoTerraCache = true;
                return;
            }
        }
    }

    private bool PontoTemTerra(Vector3 pos)
    {
        if (RegistroSuperficieMapa.TryClassify(pos, out ClassificacaoSuperficieMapa classificacao, out _, 1.75f, 0.5f))
        {
            return classificacao == ClassificacaoSuperficieMapa.Chao || classificacao == ClassificacaoSuperficieMapa.Costa;
        }

        Vector3 origem = new Vector3(pos.x, pos.y + 600f, pos.z);
        int totalHits = Physics.RaycastNonAlloc(origem, Vector3.down, _hitsTerraBuffer, 1200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (totalHits <= 0) return false;

        int melhorIndice = -1;
        float melhorDistancia = float.MaxValue;
        for (int i = 0; i < totalHits; i++)
        {
            Collider col = _hitsTerraBuffer[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;
            if (_hitsTerraBuffer[i].distance < melhorDistancia)
            {
                melhorDistancia = _hitsTerraBuffer[i].distance;
                melhorIndice = i;
            }
        }

        if (melhorIndice >= 0)
        {
            return !EhAgua(_hitsTerraBuffer[melhorIndice].collider);
        }

        return false;
    }

    private bool HelicopteroPodeTransferirNoConves(Helicoptero heli)
    {
        if (heli == null) return false;
        return JaTenhoHeli(heli) && HelicopteroPertenceAEsteNavio(heli) && !heli.estaVoando && !heli.EstaEmPreparacaoDecolagem() && !HelicopteroEstaEmAproximacaoParaConves(heli);
    }

    private int ObterQuantidadeOperacaoAtual() => _qtdOperacao >= 9999 ? int.MaxValue : Mathf.Max(1, _qtdOperacao);

    private bool EhAgua(Collider col)
    {
        if (col == null) return false;
        string nome = col.name;
        if (ContemTermoAgua(nome)) return true;
        string layerName = LayerMask.LayerToName(col.gameObject.layer);
        if (ContemTermoAgua(layerName)) return true;
        return false;
    }

    private static bool ContemTermoAgua(string texto)
    {
        return !string.IsNullOrEmpty(texto)
               && (texto.IndexOf("agua", StringComparison.OrdinalIgnoreCase) >= 0
                   || texto.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0
                   || texto.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0
                   || texto.IndexOf("sea", StringComparison.OrdinalIgnoreCase) >= 0
                   || texto.IndexOf("mar", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private Transform EscolherPontoFilaMaisProximo(Vector3 posUnidade, List<Transform> pontosTerra)
    {
        if (pontosTerra == null || pontosTerra.Count == 0) return null;
        Transform melhor = null;
        float melhorDist = float.MaxValue;
        for (int i = 0; i < pontosTerra.Count; i++)
        {
            Transform p = pontosTerra[i];
            if (p == null) continue;
            float d = (p.position - posUnidade).sqrMagnitude;
            if (d < melhorDist)
            {
                melhorDist = d;
                melhor = p;
            }
        }
        return melhor;
    }

    private Transform EscolherPontoSaidaMaisProximo(Vector3 posRef, List<Transform> pontosTerra) => EscolherPontoFilaMaisProximo(posRef, pontosTerra);

    private void OrdenarIrPara(GameObject u, Vector3 destino)
    {
        if (u == null) return;
        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            ctrl.EmitirOrdemMover(destino);
            return;
        }

        var nav = u.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            try
            {
                if (!nav.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(u.transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas)) nav.Warp(hit.position);
                }
                nav.isStopped = false;
                // Transicao: evita SetDestination direto. Se a unidade nao tem facade, preferimos detectar e corrigir prefab.
                u.SendMessage("MoverParaPonto", destino, SendMessageOptions.DontRequireReceiver);
            }
            catch { }
        }
    }

    private List<GameObject> ColetarTerrestresProximos(ModoOperacaoTerrestre modo)
    {
        int meuTime = _idNavio != null ? _idNavio.teamID : 1;
        int totalHits = CapturarCollidersTerrestres();
        List<GameObject> lista = _candidatosTerrestresOperacao;
        lista.Clear();
        _vistosTerrestresBusca.Clear();

        for (int i = 0; i < totalHits; i++)
        {
            Collider hit = _hitsTerrestresBuffer[i];
            if (hit == null) continue;
            GameObject raiz = ResolverUnidadeLogica(hit.gameObject);
            if (raiz == null || raiz == gameObject) continue;
            int id = raiz.GetInstanceID();
            if (!_vistosTerrestresBusca.Add(id)) continue;

            if (!raiz.activeInHierarchy) continue;
            if (JaCarregado(raiz)) continue;

            IdentidadeUnidade idU = raiz.GetComponent<IdentidadeUnidade>();
            if (idU == null) idU = raiz.GetComponentInChildren<IdentidadeUnidade>();
            if (idU != null && meuTime > 0 && idU.teamID > 0 && idU.teamID != meuTime) continue;

            if (idU != null)
            {
                if (idU.tipoUnidade != TipoUnidade.Infantaria && idU.tipoUnidade != TipoUnidade.Veiculo) continue;
            }
            else
            {
                if (raiz.GetComponent<Helicoptero>() != null) continue;
                if (raiz.GetComponent<ControleAviao>() != null) continue;
                if (raiz.GetComponent<IdentidadeNaval>() != null) continue;
            }

            bool soldado = EhSoldado(raiz);
            if (modo == ModoOperacaoTerrestre.Veiculos && soldado) continue;
            if (modo == ModoOperacaoTerrestre.Soldados && !soldado) continue;

            lista.Add(raiz);
        }

        lista.Sort(CompararGameObjectsPorDistancia);
        return lista;
    }

    private int CapturarCollidersTerrestres()
    {
        int totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioBuscaTerrestres, _hitsTerrestresBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        while (totalHits >= _hitsTerrestresBuffer.Length && _hitsTerrestresBuffer.Length < 2048)
        {
            _hitsTerrestresBuffer = new Collider[_hitsTerrestresBuffer.Length * 2];
            totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioBuscaTerrestres, _hitsTerrestresBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        return totalHits;
    }

    private int CompararGameObjectsPorDistancia(GameObject a, GameObject b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        float da = (a.transform.position - transform.position).sqrMagnitude;
        float db = (b.transform.position - transform.position).sqrMagnitude;
        return da.CompareTo(db);
    }

    private bool EhSoldado(GameObject obj)
    {
        if (obj == null) return false;
        IdentidadeUnidade id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.GetComponentInChildren<IdentidadeUnidade>();
        if (id != null) return id.tipoUnidade == TipoUnidade.Infantaria;

        var sd = obj.GetComponent<SistemaDeDanos>();
        if (sd == null) sd = obj.GetComponentInChildren<SistemaDeDanos>();
        if (sd != null && sd.unidadeBiologica) return true;

        string n = obj.name.ToLowerInvariant();
        return n.Contains("soldado") || n.Contains("infant") || n.Contains("sniper");
    }

    private bool JaCarregado(GameObject u)
    {
        int id = u.GetInstanceID();
        for (int i = 0; i < _veiculosCarregados.Count; i++)
            if (_veiculosCarregados[i] != null && _veiculosCarregados[i].unidade != null && _veiculosCarregados[i].unidade.GetInstanceID() == id) return true;
        for (int i = 0; i < _soldadosCarregados.Count; i++)
            if (_soldadosCarregados[i] != null && _soldadosCarregados[i].unidade != null && _soldadosCarregados[i].unidade.GetInstanceID() == id) return true;
        return false;
    }

    private GameObject ResolverUnidadeLogica(GameObject hit)
    {
        if (hit == null) return null;
        var ctrl = hit.GetComponentInParent<ControleUnidade>();
        if (ctrl != null) return ctrl.gameObject;
        var nav = hit.GetComponentInParent<NavMeshAgent>();
        if (nav != null) return nav.gameObject;
        return hit.transform.root != null ? hit.transform.root.gameObject : hit;
    }

    private void AdicionarTerrestresParaSaida(List<CargaTerrestre> destino, List<CargaTerrestre> origem, int startIndex, int qtd)
    {
        if (origem == null || origem.Count == 0) return;
        if (qtd == int.MaxValue)
        {
            for (int i = startIndex; i < origem.Count; i++) if (origem[i] != null && origem[i].unidade != null) destino.Add(origem[i]);
            for (int i = 0; i < startIndex; i++) if (origem[i] != null && origem[i].unidade != null) destino.Add(origem[i]);
            return;
        }

        int adicionados = 0;
        for (int i = startIndex; i < origem.Count && adicionados < qtd; i++)
        {
            if (origem[i] == null || origem[i].unidade == null) continue;
            destino.Add(origem[i]);
            adicionados++;
        }
    }

    private Vector3 AjustarParaNavMeshSePossivel(Vector3 destinoBruto)
    {
        if (NavMesh.SamplePosition(destinoBruto, out NavMeshHit hit, 18f, NavMesh.AllAreas)) return hit.position;
        return destinoBruto;
    }

    // ======================================================
    // Helicópteros Helpers
    // ======================================================
    private void ColetarHelisProximos(List<Helicoptero> destino)
    {
        if (destino == null)
        {
            return;
        }

        int meuTime = _idNavio != null ? _idNavio.teamID : 1;
        float raioBuscaEfetivo = ObterRaioBuscaHelisEfetivo();
        float raioSqr = raioBuscaEfetivo * raioBuscaEfetivo;
        _vistosHelisBusca.Clear();

        int totalHits = CapturarCollidersHelis(raioBuscaEfetivo);
        for (int i = 0; i < totalHits; i++)
        {
            Collider hit = _hitsHeliBuffer[i];
            if (hit == null) continue;
            TentarAdicionarHeliProximo(hit.GetComponentInParent<Helicoptero>(), meuTime, raioSqr, destino);
        }

        if (destino.Count == 0)
        {
            _bufferHelicopterosFallback.Clear();
            RegistroEntidadesJogo.FillHelicopteros(_bufferHelicopterosFallback);
            for (int i = 0; i < _bufferHelicopterosFallback.Count; i++)
            {
                TentarAdicionarHeliProximo(_bufferHelicopterosFallback[i], meuTime, raioSqr, destino);
            }
        }

        destino.Sort(CompararHelisPorDistancia);
    }

    private int CapturarCollidersHelis(float raioBuscaEfetivo)
    {
        int totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioBuscaEfetivo, _hitsHeliBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        while (totalHits >= _hitsHeliBuffer.Length && _hitsHeliBuffer.Length < 1024)
        {
            _hitsHeliBuffer = new Collider[_hitsHeliBuffer.Length * 2];
            totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioBuscaEfetivo, _hitsHeliBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        return totalHits;
    }

    private bool TentarAdicionarHeliProximo(Helicoptero h, int meuTime, float raioSqr, List<Helicoptero> destino)
    {
        if (h == null) return false;
        int id = h.GetInstanceID();
        if (!_vistosHelisBusca.Add(id)) return false;
        if (JaTenhoHeli(h)) return false;
        if (HelicopteroPertenceAEsteNavio(h)) return false;
        if (!h.estaVoando && !h.EstaEmPreparacaoDecolagem()) return false;

        IdentidadeUnidade idU = h.GetComponent<IdentidadeUnidade>();
        if (idU == null) idU = h.GetComponentInParent<IdentidadeUnidade>();
        if (idU != null && meuTime > 0 && idU.teamID > 0 && idU.teamID != meuTime) return false;

        float dist = (h.transform.position - transform.position).sqrMagnitude;
        if (dist > raioSqr) return false;

        destino.Add(h);
        return true;
    }

    private int CompararHelisPorDistancia(Helicoptero a, Helicoptero b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        float da = (a.transform.position - transform.position).sqrMagnitude;
        float db = (b.transform.position - transform.position).sqrMagnitude;
        return da.CompareTo(db);
    }

    private float ObterRaioBuscaHelisEfetivo() => Mathf.Max(raioBuscaHelis, 600f);

    private void AtualizarCacheHelisProximos()
    {
        SanearHelicopterosCarregados();
        _helisProximosCache.Clear();
        ColetarHelisProximos(_helisProximosCache);
    }

    private bool JaTenhoHeli(Helicoptero h)
    {
        if (h == null) return false;
        int id = h.GetInstanceID();
        for (int i = 0; i < _helisCarregados.Count; i++)
        {
            if (_helisCarregados[i] != null && _helisCarregados[i].heli != null && _helisCarregados[i].heli.GetInstanceID() == id) return true;
        }
        return false;
    }

    private bool HelicopteroEstaEmAproximacaoParaConves(Helicoptero heli) => heli != null && _rotinasEntradaHeli.ContainsKey(heli.GetInstanceID());

    private bool HelicopteroEstaEmSaidaDoConves(Helicoptero heli) => heli != null && _rotinasSaidaHeli.ContainsKey(heli.GetInstanceID());

    private Vector3 ObterDestinoAereoPontoPousoDecolagem()
    {
        if (pontoPousoDecolagemHeli == null) return transform.position + Vector3.up * 12f;
        Vector3 destinoAereo = pontoPousoDecolagemHeli.position;
        destinoAereo.y = Mathf.Max(destinoAereo.y + 2.5f, pontoPousoDecolagemHeli.position.y + 2.5f);
        return destinoAereo;
    }

    private IEnumerator EsperarHelicopteroAlcancarPonto(Helicoptero heli, Vector3 ponto, float timeout, float distanciaAceita)
    {
        float tempo = 0f;
        float distanciaLimite = Mathf.Max(1.5f, distanciaAceita);
        while (tempo < timeout && heli != null)
        {
            tempo += Time.deltaTime;
            Vector2 heliXZ = new Vector2(heli.transform.position.x, heli.transform.position.z);
            Vector2 pontoXZ = new Vector2(ponto.x, ponto.z);
            if (Vector2.Distance(heliXZ, pontoXZ) <= distanciaLimite) yield break;
            yield return null;
        }
    }

    private Transform ObterProximaParadaLivre()
    {
        SanearHelicopterosCarregados();
        if (paradasHeli == null || paradasHeli.Length == 0) return null;

        for (int i = 0; i < paradasHeli.Length; i++)
        {
            Transform p = paradasHeli[i];
            if (p == null) continue;
            bool ocupada = false;
            for (int j = 0; j < _helisCarregados.Count; j++)
            {
                var c = _helisCarregados[j];
                if (c == null || c.heli == null) continue;
                if (c.paradaAtual == p && !c.emSaida)
                {
                    ocupada = true;
                    break;
                }
            }
            if (!ocupada) return p;
        }
        return null;
    }

    private Transform ObterStopAlternado()
    {
        Transform a = stop0 != null ? stop0 : stop1;
        Transform b = stop1 != null ? stop1 : stop0;
        if (a == null && b == null) return null;
        Transform escolhido = (_alternanciaStop % 2 == 0) ? a : b;
        _alternanciaStop++;
        return escolhido ?? a ?? b;
    }

    private bool HelicopteroPertenceAEsteNavio(Helicoptero h)
    {
        if (h == null) return false;
        Transform vagaAtual = h.ObterVagaAeroporto();
        return vagaAtual != null && (vagaAtual == transform || vagaAtual.IsChildOf(transform));
    }

    private bool EstacionarHelicopteroNoNavio(Helicoptero h, Transform vaga, bool emSaida)
    {
        if (h == null || vaga == null) return false;
        SanearHelicopterosCarregados();

        CargaHeli carga = null;
        for (int i = 0; i < _helisCarregados.Count; i++)
        {
            if (_helisCarregados[i] != null && _helisCarregados[i].heli == h)
            {
                carga = _helisCarregados[i];
                break;
            }
        }

        if (carga == null)
        {
            carga = new CargaHeli { heli = h };
            _helisCarregados.Add(carga);
            _contagemCargaSuja = true;
        }

        carga.paradaAtual = vaga;
        carga.emSaida = emSaida;

        h.CancelarMissaoAeroporto();
        h.TornarNavioBasePermanente(vaga);

        if (emSaida)
        {
            h.transform.SetParent(transform, true);
            h.PosicionarInstantaneamenteNaVagaAeroporto(vaga);
            h.transform.position = h.ObterPosicaoEstacionadaNaVaga(vaga);
            h.transform.rotation = vaga.rotation;
        }
        else
        {
            IniciarAnimacaoEntradaHeliNoConves(h, vaga);
        }

        _categoriaSelecionada = CategoriaSelecao.Heli;
        _indiceSelecionadoHeli = _helisCarregados.IndexOf(carga);
        _indiceSelecionadoVeiculo = -1;
        _indiceSelecionadoSoldado = -1;
        _helicopteroSelecionadoParaMissao = h;
        return true;
    }

    private void IniciarAnimacaoEntradaHeliNoConves(Helicoptero heli, Transform vaga)
    {
        if (heli == null || vaga == null) return;
        int id = heli.GetInstanceID();
        if (_rotinasEntradaHeli.TryGetValue(id, out Coroutine antiga) && antiga != null) StopCoroutine(antiga);
        Coroutine nova = StartCoroutine(RotinaAnimarEntradaHeliNoConves(heli, vaga, id));
        _rotinasEntradaHeli[id] = nova;
    }

    private IEnumerator RotinaAnimarEntradaHeliNoConves(Helicoptero heli, Transform vaga, int heliId)
    {
        if (heli == null || vaga == null)
        {
            _rotinasEntradaHeli.Remove(heliId);
            yield break;
        }

        heli.transform.SetParent(null, true);
        heli.CancelarMissaoAeroporto();
        heli.TornarNavioBasePermanente(vaga);
        heli.Decolar(ObterDestinoAereoPontoPousoDecolagem());

        if (pontoPousoDecolagemHeli != null) yield return EsperarHelicopteroAlcancarPonto(heli, pontoPousoDecolagemHeli.position, 20f, 4f);

        if (heli != null && vaga != null) heli.PosicionarNaVagaAeroporto(vaga);

        float timeout = 25f;
        float tempo = 0f;
        while (tempo < timeout && heli != null && vaga != null)
        {
            tempo += Time.deltaTime;
            if (!heli.estaVoando && !heli.EstaEmPreparacaoDecolagem()) break;
            yield return null;
        }

        if (heli != null && vaga != null)
        {
            heli.transform.SetParent(transform, true);
            heli.PosicionarInstantaneamenteNaVagaAeroporto(vaga);
            heli.transform.position = heli.ObterPosicaoEstacionadaNaVaga(vaga);
            heli.transform.rotation = vaga.rotation;
        }

        _rotinasEntradaHeli.Remove(heliId);
    }

    private bool IniciarSaidaHelicopteroDoNavio(Helicoptero heli, Action<Helicoptero> aoLiberar, string descricaoOperacao)
    {
        if (heli == null) return false;
        SanearHelicopterosCarregados();

        CargaHeli carga = null;
        for (int i = 0; i < _helisCarregados.Count; i++)
        {
            if (_helisCarregados[i] != null && _helisCarregados[i].heli == heli)
            {
                carga = _helisCarregados[i];
                break;
            }
        }

        if (carga == null)
        {
            aoLiberar?.Invoke(heli);
            return true;
        }

        int heliId = heli.GetInstanceID();
        if (_rotinasSaidaHeli.TryGetValue(heliId, out Coroutine rotinaExistente) && rotinaExistente != null) return false;

        carga.emSaida = true;
        Coroutine rotinaNova = StartCoroutine(RotinaLancarHelicopteroDoNavio(heli, heliId, aoLiberar, descricaoOperacao));
        _rotinasSaidaHeli[heliId] = rotinaNova;
        return true;
    }

    private IEnumerator RotinaLancarHelicopteroDoNavio(Helicoptero heli, int heliId, Action<Helicoptero> aoLiberar, string descricaoOperacao)
    {
        if (heli == null)
        {
            _rotinasSaidaHeli.Remove(heliId);
            yield break;
        }

        heli.transform.SetParent(null, true);
        heli.CancelarMissaoAeroporto();
        heli.Decolar(ObterDestinoAereoPontoPousoDecolagem());

        if (pontoPousoDecolagemHeli != null) yield return EsperarHelicopteroAlcancarPonto(heli, pontoPousoDecolagemHeli.position, 18f, 5f);

        if (heli != null)
        {
            DesvincularHelicopteroDoNavio(heli);
            aoLiberar?.Invoke(heli);

            if (!string.IsNullOrEmpty(descricaoOperacao))
            {
                MostrarMensagem($"🛫 {heli.ObterRotuloExibicao()} liberado do navio, {descricaoOperacao}.");
            }
        }

        _rotinasSaidaHeli.Remove(heliId);
        AtualizarCacheHelisProximos();
    }

    private void DesvincularHelicopteroDoNavio(Helicoptero heli)
    {
        if (heli == null) return;

        int heliId = heli.GetInstanceID();
        _rotinasEntradaHeli.Remove(heliId);
        _rotinasSaidaHeli.Remove(heliId);

        for (int i = _helisCarregados.Count - 1; i >= 0; i--)
        {
            CargaHeli carga = _helisCarregados[i];
            if (carga == null || carga.heli == null)
            {
                _helisCarregados.RemoveAt(i);
                continue;
            }

            if (carga.heli == heli)
            {
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
                break;
            }
        }

        if (_helicopteroSelecionadoParaMissao == heli && _modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Nenhum)
        {
            _helicopteroSelecionadoParaMissao = null;
        }

        if (heli.transform.parent == transform) heli.transform.SetParent(null, true);

        if (_indiceSelecionadoHeli >= _helisCarregados.Count) _indiceSelecionadoHeli = _helisCarregados.Count - 1;
    }

    private bool ChamarHelicopteroParaConves(Helicoptero h)
    {
        if (h == null || JaTenhoHeli(h)) return false;
        Transform paradaLivre = ObterProximaParadaLivre();
        if (paradaLivre == null) return false;

        bool estacionou = EstacionarHelicopteroNoNavio(h, paradaLivre, false);
        if (estacionou)
        {
            _helisProximosCache.RemoveAll(heli => heli == null || heli == h);
            AtualizarCacheHelisProximos();
        }
        return estacionou;
    }

    private void PrepararHelicopteroParaPatrulhaCombate(Helicoptero heli)
    {
        if (heli == null)
        {
            return;
        }

        heli.DefinirModoCombateAtivo(true);
    }

    private void ManterHelisNoNavio()
    {
        SanearHelicopterosCarregados();

        for (int i = _helisCarregados.Count - 1; i >= 0; i--)
        {
            var c = _helisCarregados[i];
            if (c == null || c.heli == null)
            {
                if (c != null && c.heli != null)
                {
                    _rotinasEntradaHeli.Remove(c.heli.GetInstanceID());
                    _rotinasSaidaHeli.Remove(c.heli.GetInstanceID());
                }
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
                continue;
            }

            Helicoptero h = c.heli;
            int heliId = h.GetInstanceID();
            bool entradaEmAndamento = _rotinasEntradaHeli.ContainsKey(heliId);
            bool saidaEmAndamento = _rotinasSaidaHeli.ContainsKey(heliId);

            if (h.estaVoando)
            {
                if (entradaEmAndamento || saidaEmAndamento) continue;
                _rotinasEntradaHeli.Remove(h.GetInstanceID());
                _rotinasSaidaHeli.Remove(h.GetInstanceID());
                if (h.transform.parent == transform) h.transform.SetParent(null, true);
                if (_helicopteroSelecionadoParaMissao == h && _modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Nenhum) _helicopteroSelecionadoParaMissao = null;
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
                continue;
            }

            if (!HelicopteroPertenceAEsteNavio(h))
            {
                if (saidaEmAndamento) continue;
                _rotinasEntradaHeli.Remove(h.GetInstanceID());
                _rotinasSaidaHeli.Remove(h.GetInstanceID());
                if (h.transform.parent == transform) h.transform.SetParent(null, true);
                if (_helicopteroSelecionadoParaMissao == h && _modoOrdemHelicoptero == ModoOrdemHelicopteroNavio.Nenhum) _helicopteroSelecionadoParaMissao = null;
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
                continue;
            }

            if (h.transform.parent != transform)
            {
                if (entradaEmAndamento || saidaEmAndamento) continue;
                h.transform.SetParent(transform, true);
            }

            if (c.paradaAtual != null)
            {
                if (_rotinasEntradaHeli.ContainsKey(h.GetInstanceID())) continue;
                if (_rotinasSaidaHeli.ContainsKey(h.GetInstanceID())) continue;
                h.transform.position = h.ObterPosicaoEstacionadaNaVaga(c.paradaAtual);
                h.transform.rotation = c.paradaAtual.rotation;
            }
        }
    }

    private void SanearHelicopterosCarregados()
    {
        _vistosSanearHelis.Clear();
        for (int i = _helisCarregados.Count - 1; i >= 0; i--)
        {
            CargaHeli carga = _helisCarregados[i];
            if (carga == null || carga.heli == null)
            {
                if (carga != null && carga.heli != null)
                {
                    _rotinasEntradaHeli.Remove(carga.heli.GetInstanceID());
                    _rotinasSaidaHeli.Remove(carga.heli.GetInstanceID());
                }
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
                continue;
            }

            if (!_vistosSanearHelis.Add(carga.heli.GetInstanceID()))
            {
                _rotinasEntradaHeli.Remove(carga.heli.GetInstanceID());
                _rotinasSaidaHeli.Remove(carga.heli.GetInstanceID());
                if (_helicopteroSelecionadoParaMissao == carga.heli && _indiceSelecionadoHeli == i) _helicopteroSelecionadoParaMissao = null;
                _helisCarregados.RemoveAt(i);
                _contagemCargaSuja = true;
            }
        }

        if (_indiceSelecionadoHeli >= _helisCarregados.Count) _indiceSelecionadoHeli = _helisCarregados.Count - 1;
    }

    // ======================================================
    // Descrição padrão & Gizmos
    // ======================================================
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioBuscaTerrestres);

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioBuscaHelis);

        if (pontosSaidaEntrada != null)
        {
            foreach (Transform t in pontosSaidaEntrada)
            {
                if (t == null) continue;
                Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
                Gizmos.DrawWireSphere(t.position, raioPuxarAbsoluto);
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
                Gizmos.DrawWireSphere(t.position, raioPuxarPresoNavMesh);
            }
        }
    }

    private void PreencherDescricaoPadrao()
    {
        descricaoParaIA =
@"NAVIO TRANSPORTE DE TROPAS (USS Liberty / HelicopterAircraftCarrier_01_Prefeb)

CAPACIDADES (separadas):
- Veículos: capacidadeMaxVeiculos
- Soldados: capacidadeMaxSoldados
- Aéreos: capacidadeMaxAereos (helicópteros com script Helicoptero)

PORTA TERRESTRE:
- Durante operações terrestres a porta abre/fecha animando a rotação local Z.

PONTOS TERRESTRES (fila):
- Existem 4 pontos ao redor do navio.
- Unidades terrestres sempre vão para o ponto em TERRA mais próximo.

MENU:
- Lista veículos/soldados/helis carregados (agora com Modo Desempenho para FPS).";   }
}
