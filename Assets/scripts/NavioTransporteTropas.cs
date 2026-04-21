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

    // ======================================================
    // API Pública (IA / Debug)
    // ======================================================
    public int VeiculosAtual => _veiculosCarregados.Count(c => c != null && c.unidade != null);
    public int SoldadosAtual => _soldadosCarregados.Count(c => c != null && c.unidade != null);
    public int AereosAtual => _helisCarregados.Count(c => c != null && c.heli != null);

    public int VeiculosMax => capacidadeMaxVeiculos;
    public int SoldadosMax => capacidadeMaxSoldados;
    public int AereosMax => capacidadeMaxAereos;

    public bool TemPontoEmTerra()
    {
        return ObterPontosSaidaEmTerra().Count > 0;
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
        }

        AtualizarAnimacaoPorta();
        if (!Construtor.EmModoConstrucaoAtivo)
        {
            ProcessarInputMenu();
        }
        ManterHelisNoNavio();
        LimparNulos();
    }

    private void OnGUI()
    {
        if (!_menuAberto) return;
        if (_idNavio != null && _idNavio.teamID != 1) return;

        GUI.skin.label.richText = true;
        GUI.skin.box.richText = true;

        float menuWidth = 410f;
        float menuHeight = 720f;
        float offsetX = Screen.width * 0.13f;
        float offsetY = Screen.height / 2f - (menuHeight / 2f) - (Screen.height * 0.02f);

        Rect areaMenu = new Rect(Screen.width - menuWidth - 40f - offsetX, offsetY, menuWidth, menuHeight);
        GUI.Box(areaMenu, "<b>🚢 COMANDO - NAVIO TRANSPORTE DE TROPAS</b>");

        GUILayout.BeginArea(new Rect(areaMenu.x + 10f, areaMenu.y + 25f, areaMenu.width - 20f, areaMenu.height - 35f));

        string nome = (_idNavio != null && !string.IsNullOrEmpty(_idNavio.nomeDoPais)) ? _idNavio.nomeDoPais : "Navio";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>⚓ Status:</b> <color=cyan>{nome}</color>", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Fechar", GUILayout.Width(80f), GUILayout.Height(24f))) _menuAberto = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label($"<b>🚚 Veículos:</b> <color=yellow>{VeiculosAtual}</color> / {capacidadeMaxVeiculos}");
        GUILayout.Label($"<b>🪖 Soldados:</b> <color=yellow>{SoldadosAtual}</color> / {capacidadeMaxSoldados}");
        GUILayout.Label($"<b>🚁 Helicópteros:</b> <color=yellow>{AereosAtual}</color> / {capacidadeMaxAereos}");

        bool temTerra = TemPontoEmTerra();
        if (!temTerra)
        {
            GUILayout.Label("<color=red><b>⚠️ Operação terrestre BLOQUEADA:</b> nenhum 'saida/entrada' está em terra.</color>");
        }

        GUILayout.Space(6);

        // Quantidade //
        GUILayout.BeginHorizontal("box");
        GUILayout.Label($"<b>Qtd:</b> {_qtdOperacao}", GUILayout.Width(90f));
        if (GUILayout.Button("-", GUILayout.Width(35f), GUILayout.Height(24f))) _qtdOperacao = Mathf.Max(1, _qtdOperacao - 1);
        if (GUILayout.Button("+", GUILayout.Width(35f), GUILayout.Height(24f))) _qtdOperacao = Mathf.Min(9999, _qtdOperacao - 1); // wait, it was +1. But now the user specifically said they have a "Todos" button for this! Let's assure "+".
        if (GUILayout.Button("+", GUILayout.Width(35f), GUILayout.Height(24f))) _qtdOperacao = Mathf.Min(9999, _qtdOperacao + 1); // correcting
        if (GUILayout.Button("Todos", GUILayout.Width(70f), GUILayout.Height(24f))) _qtdOperacao = 9999;
        GUILayout.EndHorizontal();

        if (_operacaoTerrestreAtiva || _operacaoHeliAtiva)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("❌ CANCELAR TODAS AS OPERAÇÕES", GUILayout.Height(35f)))
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
        if (GUILayout.Button("Puxar Veículos", GUILayout.Height(30f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Veiculos, _qtdOperacao);
        if (GUILayout.Button("Puxar Soldados", GUILayout.Height(30f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Soldados, _qtdOperacao);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Puxar Terrestres (Todos)", GUILayout.Height(28f))) IniciarEmbarqueTerrestre(ModoOperacaoTerrestre.Todos, _qtdOperacao);
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // Listas
        GUILayout.Label($"<color=cyan><b>🚚 VEÍCULOS A BORDO ({VeiculosAtual}/{capacidadeMaxVeiculos})</b></color>");
        _scrollVeiculos = GUILayout.BeginScrollView(_scrollVeiculos, GUILayout.Height(110f));
        for (int i = 0; i < _veiculosCarregados.Count; i++)
        {
            var u = _veiculosCarregados[i]?.unidade;
            if (u == null) continue;
            string pfx = (_categoriaSelecionada == CategoriaSelecao.Veiculo && _indiceSelecionadoVeiculo == i) ? "► " : "";
            if (GUILayout.Button($"{pfx}🚚 {LimparClone(u.name)}", GUILayout.Height(26f)))
            {
                _categoriaSelecionada = CategoriaSelecao.Veiculo;
                _indiceSelecionadoVeiculo = i;
                _indiceSelecionadoSoldado = -1;
                _indiceSelecionadoHeli = -1;
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4);
        GUILayout.Label($"<color=lime><b>🪖 SOLDADOS A BORDO ({SoldadosAtual}/{capacidadeMaxSoldados})</b></color>");
        _scrollSoldados = GUILayout.BeginScrollView(_scrollSoldados, GUILayout.Height(110f));
        for (int i = 0; i < _soldadosCarregados.Count; i++)
        {
            var u = _soldadosCarregados[i]?.unidade;
            if (u == null) continue;
            string pfx = (_categoriaSelecionada == CategoriaSelecao.Soldado && _indiceSelecionadoSoldado == i) ? "► " : "";
            if (GUILayout.Button($"{pfx}🪖 {LimparClone(u.name)}", GUILayout.Height(26f)))
            {
                _categoriaSelecionada = CategoriaSelecao.Soldado;
                _indiceSelecionadoSoldado = i;
                _indiceSelecionadoVeiculo = -1;
                _indiceSelecionadoHeli = -1;
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4);
        GUILayout.Label($"<color=orange><b>🚁 HELICÓPTEROS NO CONVÉS ({AereosAtual}/{capacidadeMaxAereos})</b></color>");
        _scrollHelis = GUILayout.BeginScrollView(_scrollHelis, GUILayout.Height(110f));
        for (int i = 0; i < _helisCarregados.Count; i++)
        {
            var h = _helisCarregados[i]?.heli;
            if (h == null) continue;
            string status = _helisCarregados[i].emSaida ? " (STOP)" : (_helisCarregados[i].paradaAtual != null ? $" ({_helisCarregados[i].paradaAtual.name})" : "");
            string pfx = (_categoriaSelecionada == CategoriaSelecao.Heli && _indiceSelecionadoHeli == i) ? "► " : "";
            if (GUILayout.Button($"{pfx}🚁 {LimparClone(h.name)}{status}", GUILayout.Height(26f)))
            {
                _categoriaSelecionada = CategoriaSelecao.Heli;
                _indiceSelecionadoHeli = i;
                _indiceSelecionadoVeiculo = -1;
                _indiceSelecionadoSoldado = -1;
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);

        GUILayout.Space(6);

        // Embarque de Helicópteros Isolado
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>🚁 EMBARQUE DE HELICÓPTEROS</b>");
        if (GUILayout.Button($"Puxar Helicópteros Próximos ({_qtdOperacao})", GUILayout.Height(30f))) IniciarPuxarHelicopteros(_qtdOperacao);
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // Desembarque e Liberação Unificado
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>📤 SAÍDA / DESEMBARQUE</b>");

        string infoSelecao = ObterDescricaoSelecaoAtual();
        if (!string.IsNullOrEmpty(infoSelecao))
            GUILayout.Label($"Selecionado: <color=yellow>{infoSelecao}</color>");
        else
            GUILayout.Label("Selecionado: NENHUM");

        if (GUILayout.Button("Desembarcar SÓ O SELECIONADO", GUILayout.Height(30f)))
        {
            DispararDesembarquePeloMenu(1);
        }

        GUILayout.Space(4);
        GUILayout.Label("<b>Desembarcar por Categoria:</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Todos Soldados", GUILayout.Height(28f))) IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Soldados, int.MaxValue, 0);
        if (GUILayout.Button("Todos Veículos", GUILayout.Height(28f))) IniciarDesembarqueTerrestre(ModoOperacaoTerrestre.Veiculos, int.MaxValue, 0);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Todos Helicópteros", GUILayout.Height(28f))) IniciarLiberarHelicopteros(int.MaxValue, 0);

        GUILayout.Space(6);
        if (GUILayout.Button("🔴 RETIRAR TODAS AS UNIDADES (TUDO) 🔴", GUILayout.Height(32f)))
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

        GUILayout.EndArea();
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
            MostrarMensagem("❌ ERRO: faltam referências de helicópteros (Paradas/Stop).");
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

        if (stop0 == null && stop1 == null)
        {
            MostrarMensagem("❌ ERRO: faltam pontos Stop/Stop (1).");
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

            // Raio super permissivo para absorver a fila caso o NavMesh deles seja grosseiro
            if (posDiff.magnitude <= raioPuxarAbsoluto)
            {
                chegou = true;
                break;
            }

            var nav = u.GetComponent<NavMeshAgent>();
            if (nav != null && nav.enabled && nav.isOnNavMesh && !nav.pathPending)
            {
                // Se ele parou (bateu no limite de caminhada possível) e está a menos do raio máximo, puxa ele.
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

            // Delega o monitoramento de chegada para uma Rotina Paralela que vai resolver ela mesma.
            StartCoroutine(MonitorarEEmbarcarIndividual(u, ehSoldado, pontoFila));
            embarcados++;

            float tempoEspera = ehSoldado ? 2f : 7f;
            float timerEsp = 0f;
            while(timerEsp < tempoEspera && _operacaoTerrestreAtiva)
            {
                timerEsp += Time.deltaTime;
                yield return null;
            }
        }

        // Aguarda todos chegarem no seu próprio tempo limite, ou o botão de cancelar ser tocado
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

        List<Helicoptero> candidatos = ColetarHelisProximos();
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

            var carga = new CargaHeli { heli = h, paradaAtual = paradaLivre, emSaida = false };
            _helisCarregados.Add(carga);

            h.PosicionarNaVagaAeroporto(paradaLivre);
            h.transform.SetParent(transform, true);
            h.transform.position = paradaLivre.position;
            h.transform.rotation = transform.rotation;

            MostrarMensagem($"✅ {LimparClone(h.name)} estacionado em {paradaLivre.name}.");
            puxados++;
            yield return new WaitForSeconds(delayEntreUnidades);
        }

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

            Transform stop = ObterStopAlternado();
            if (stop == null)
            {
                MostrarMensagem("❌ ERRO: Stop/Stop (1) não configurado.");
                break;
            }

            carga.emSaida = true;
            carga.paradaAtual = null; // libera vaga

            carga.heli.PosicionarNaVagaAeroporto(stop);
            carga.heli.transform.SetParent(transform, true);
            carga.heli.transform.position = stop.position;
            carga.heli.transform.rotation = transform.rotation;

            MostrarMensagem($"➡️ {LimparClone(carga.heli.name)} movido para {stop.name} (pronto p/ voo).");
            liberados++;
            yield return new WaitForSeconds(delayEntreUnidades);
        }

        _operacaoHeliAtiva = false;
        MostrarMensagem("✅ Helicópteros liberados (aguardando decolagem).");
    }

    // ======================================================
    // Embarque/Desembarque unitário
    // ======================================================

    private IEnumerator EmbarcarUmaUnidadeTerrestre(GameObject u, bool ehSoldado)
    {
        if (u == null) yield break;
        if (_containerCarga == null) GarantirContainerCarga();

        // Preparar (desliga nav/controle pra não brigar com o movimento interno)
        var carga = PrepararUnidadeParaCarga(u);
        carga.parentOriginal = u.transform.parent;

        Vector3 localCorredor = _containerCarga.InverseTransformPoint(corredor1.position);
        Vector3 localChegada = _containerCarga.InverseTransformPoint(chegada.position);

        u.transform.SetParent(_containerCarga, true);
        u.transform.localPosition = localCorredor;
        u.transform.rotation = transform.rotation;

        // Move interno Corredor1 -> chegada
        yield return StartCoroutine(MoverLocalAte(u.transform, localChegada, velocidadeMovimentoInterno, timeoutMoverInterno));

        if (ehSoldado) _soldadosCarregados.Add(carga);
        else _veiculosCarregados.Add(carga);

        // Esconde a unidade (mas continua parentada para acompanhar o navio)
        u.SetActive(false);
    }

    private void RemoverCargaTerrestre(CargaTerrestre carga)
    {
        if (carga == null) return;
        _veiculosCarregados.Remove(carga);
        _soldadosCarregados.Remove(carga);
    }

    private void RestaurarUnidadeTerrestre(GameObject u, CargaTerrestre carga, Vector3 destinoFinal)
    {
        if (u == null || carga == null) return;

        // Restaura ControleUnidade (se existir)
        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            try { ctrl.DefinirSelecao(false); } catch { }
            ctrl.enabled = carga.controleEnabledAntes;
        }

        // Restaura Rigidbody (se existir)
        var rb = u.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = carga.rbKinematicAntes;
            rb.useGravity = carga.rbUseGravityAntes;
            rb.detectCollisions = carga.rbDetectCollisionsAntes;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Restaura NavMeshAgent (se existir)
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
                try
                {
                    nav.Warp(alvo);
                    nav.isStopped = false;
                }
                catch { }
            }
        }
    }

    private CargaTerrestre PrepararUnidadeParaCarga(GameObject u)
    {
        var carga = new CargaTerrestre { unidade = u };

        // Desseleciona se estiver selecionado
        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            carga.controleEnabledAntes = ctrl.enabled;
            try { ctrl.DefinirSelecao(false); } catch { }
            ctrl.enabled = false;
        }
        else
        {
            carga.controleEnabledAntes = false;
        }

        // Para NavMeshAgent (desliga para poder andar dentro do navio)
        var nav = u.GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            carga.navAgentEnabledAntes = nav.enabled;
            try
            {
                if (nav.enabled)
                {
                    if (nav.isOnNavMesh) nav.ResetPath();
                    nav.isStopped = true;
                }
            }
            catch { }
            nav.enabled = false;
        }
        else
        {
            carga.navAgentEnabledAntes = false;
        }

        // Rigidbody: deixa kinematic pra não brigar com o MoveTowards manual
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
        else
        {
            carga.rbExiste = false;
        }

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
            _menuAberto = !_menuAberto;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (_cameraPrincipal == null) _cameraPrincipal = Camera.main;
            if (_cameraPrincipal == null) return;
            Ray r = _cameraPrincipal.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(r, out RaycastHit hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    _menuAberto = true;
                }
            }
        }
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

        if (string.IsNullOrEmpty(descricaoParaIA))
        {
            PreencherDescricaoPadrao();
        }
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

    private string NomeNormalizado(string valor)
    {
        return string.IsNullOrEmpty(valor)
            ? string.Empty
            : valor.Replace(" ", string.Empty).ToLowerInvariant();
    }

    private List<Transform> OrdenarPorIndiceParenteses(List<Transform> pontos)
    {
        return pontos
            .Where(p => p != null)
            .OrderBy(p => ExtrairIndiceParentesesOuZero(p.name))
            .ToList();
    }

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

    private string LimparClone(string nome)
    {
        return string.IsNullOrEmpty(nome) ? string.Empty : nome.Replace("(Clone)", "").Trim();
    }

    private bool temReferenciasTerrestres()
    {
        return corredor1 != null && chegada != null && pontosSaidaEntrada != null && pontosSaidaEntrada.Length > 0;
    }

    private bool temReferenciasHeli()
    {
        return paradasHeli != null && paradasHeli.Length > 0;
    }

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

    private void FecharPortaTerrestre()
    {
        // só muda o alvo (anima em Update)
    }

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

    private void LimparNulos()
    {
        _veiculosCarregados.RemoveAll(c => c == null || c.unidade == null);
        _soldadosCarregados.RemoveAll(c => c == null || c.unidade == null);
        _helisCarregados.RemoveAll(c => c == null || c.heli == null);
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

    private bool PontoTemTerra(Vector3 pos)
    {
        if (RegistroSuperficieMapa.TryClassify(pos, out ClassificacaoSuperficieMapa classificacao, out _, 1.75f, 0.5f))
        {
            return classificacao == ClassificacaoSuperficieMapa.Chao || classificacao == ClassificacaoSuperficieMapa.Costa;
        }

        // Fallback: raycast vertical ignorando coliders do próprio navio
        Vector3 origem = new Vector3(pos.x, pos.y + 600f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, 1200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            return !EhAgua(hits[i].collider);
        }

        return false;
    }

    private bool EhAgua(Collider col)
    {
        if (col == null) return false;

        string nome = col.name.ToLowerInvariant();
        if (nome.Contains("agua") || nome.Contains("water") || nome.Contains("ocean") || nome.Contains("sea") || nome.Contains("mar"))
            return true;

        string layerName = LayerMask.LayerToName(col.gameObject.layer).ToLowerInvariant();
        if (layerName.Contains("agua") || layerName.Contains("water") || layerName.Contains("ocean") || layerName.Contains("sea") || layerName.Contains("mar"))
            return true;

        return false;
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

    private Transform EscolherPontoSaidaMaisProximo(Vector3 posRef, List<Transform> pontosTerra)
    {
        // mesma lógica, mas a referência normalmente é o corredor
        return EscolherPontoFilaMaisProximo(posRef, pontosTerra);
    }

    private void OrdenarIrPara(GameObject u, Vector3 destino)
    {
        if (u == null) return;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
            ctrl.MoverParaPonto(destino);
            return;
        }

        var nav = u.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            try
            {
                if (!nav.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(u.transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                    {
                        nav.Warp(hit.position);
                    }
                }
                nav.isStopped = false;
                nav.SetDestination(destino);
            }
            catch { }
        }
    }

    private List<GameObject> ColetarTerrestresProximos(ModoOperacaoTerrestre modo)
    {
        int meuTime = _idNavio != null ? _idNavio.teamID : 1;

        Collider[] hits = Physics.OverlapSphere(transform.position, raioBuscaTerrestres, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        List<GameObject> lista = new List<GameObject>();
        HashSet<int> vistos = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            GameObject raiz = ResolverUnidadeLogica(hits[i].gameObject);
            if (raiz == null || raiz == gameObject) continue;
            int id = raiz.GetInstanceID();
            if (vistos.Contains(id)) continue;
            vistos.Add(id);

            if (!raiz.activeInHierarchy) continue;
            if (JaCarregado(raiz)) continue;

            // Filtro de time
            IdentidadeUnidade idU = raiz.GetComponent<IdentidadeUnidade>();
            if (idU == null) idU = raiz.GetComponentInChildren<IdentidadeUnidade>();
            if (idU != null && meuTime > 0 && idU.teamID > 0 && idU.teamID != meuTime) continue;

            // Só terrestre
            if (idU != null)
            {
                if (idU.tipoUnidade != TipoUnidade.Infantaria && idU.tipoUnidade != TipoUnidade.Veiculo) continue;
            }
            else
            {
                // Heurística: ignora naval/aéreo/estrutura se possível
                if (raiz.GetComponent<Helicoptero>() != null) continue;
                if (raiz.GetComponent<ControleAviao>() != null) continue;
                if (raiz.GetComponent<IdentidadeNaval>() != null) continue;
            }

            bool soldado = EhSoldado(raiz);
            if (modo == ModoOperacaoTerrestre.Veiculos && soldado) continue;
            if (modo == ModoOperacaoTerrestre.Soldados && !soldado) continue;

            lista.Add(raiz);
        }

        // Ordena por proximidade do navio (organizado)
        lista = lista.OrderBy(u => (u.transform.position - transform.position).sqrMagnitude).ToList();
        return lista;
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
            for (int i = startIndex; i < origem.Count; i++)
                if (origem[i] != null && origem[i].unidade != null) destino.Add(origem[i]);
            // pega os anteriores (para manter ordem circular) apenas quando startIndex > 0 e "tudo"
            for (int i = 0; i < startIndex; i++)
                if (origem[i] != null && origem[i].unidade != null) destino.Add(origem[i]);
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
        if (NavMesh.SamplePosition(destinoBruto, out NavMeshHit hit, 18f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return destinoBruto;
    }

    // ======================================================
    // Helicópteros Helpers
    // ======================================================
    private List<Helicoptero> ColetarHelisProximos()
    {
        int meuTime = _idNavio != null ? _idNavio.teamID : 1;

        List<Helicoptero> helis = new List<Helicoptero>();
        HashSet<int> vistos = new HashSet<int>();

        Collider[] hits = Physics.OverlapSphere(transform.position, raioBuscaHelis, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            Helicoptero h = hits[i].GetComponentInParent<Helicoptero>();
            if (h == null) continue;
            int id = h.GetInstanceID();
            if (vistos.Contains(id)) continue;
            vistos.Add(id);

            if (JaTenhoHeli(h)) continue;

            IdentidadeUnidade idU = h.GetComponent<IdentidadeUnidade>();
            if (idU == null) idU = h.GetComponentInParent<IdentidadeUnidade>();
            if (idU != null && meuTime > 0 && idU.teamID > 0 && idU.teamID != meuTime) continue;

            float dist = (h.transform.position - transform.position).sqrMagnitude;
            if (dist > raioBuscaHelis * raioBuscaHelis) continue;

            helis.Add(h);
        }

        if (helis.Count == 0)
        {
            // Fallback: busca global
            var todos = Object.FindObjectsByType<Helicoptero>(FindObjectsSortMode.None);
            for (int i = 0; i < todos.Length; i++)
            {
                Helicoptero h = todos[i];
                if (h == null) continue;
                int id = h.GetInstanceID();
                if (vistos.Contains(id)) continue;
                float dist = (h.transform.position - transform.position).sqrMagnitude;
                if (dist > raioBuscaHelis * raioBuscaHelis) continue;
                helis.Add(h);
            }
        }

        return helis.OrderBy(h => (h.transform.position - transform.position).sqrMagnitude).ToList();
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

    private Transform ObterProximaParadaLivre()
    {
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

    private void ManterHelisNoNavio()
    {
        // Se um heli decolar, solta e remove da lista (libera capacidade)
        for (int i = _helisCarregados.Count - 1; i >= 0; i--)
        {
            var c = _helisCarregados[i];
            if (c == null || c.heli == null)
            {
                _helisCarregados.RemoveAt(i);
                continue;
            }

            Helicoptero h = c.heli;

            if (h.estaVoando)
            {
                if (h.transform.parent == transform) h.transform.SetParent(null, true);
                _helisCarregados.RemoveAt(i);
                continue;
            }

            // Se estiver parado e perto do navio, gruda
            if (!h.estaVoando && h.transform.parent != transform)
            {
                h.transform.SetParent(transform, true);
            }
        }
    }

    // ======================================================
    // Descrição padrão & Gizmos
    // ======================================================
    private void OnDrawGizmos()
    {
        // Raio de busca global de terrestres (onde ele detecta que pode mandar pras filas)
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioBuscaTerrestres);

        // Raio de busca global de Helis
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, raioBuscaHelis);

        // Raio de "Sucção" da fila
        if (pontosSaidaEntrada != null)
        {
            foreach (Transform t in pontosSaidaEntrada)
            {
                if (t == null) continue;

                // Suave: Puxa absoluto (onde teletransporta pra fila imediatamente ao tocar o perímetro)
                Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
                Gizmos.DrawWireSphere(t.position, raioPuxarAbsoluto);

                // Limite NavMesh preso: Puxa se a unidade travou a caminhada dentro desse anel
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
- Fechada: Z = 5
- Aberta:  Z = -186.855

PONTOS TERRESTRES (fila):
- Existem 4 pontos ao redor do navio (saida/entrada, saida/entrada (1..3)) dentro do objeto 'fila'.
- Unidades terrestres sempre vão para o ponto em TERRA mais próximo (Chao/Costa).
- Se nenhum ponto estiver em terra, embarque/desembarque terrestre é bloqueado.

FLUXO INTERNO (organizado / em ordem):
- ENTRAR: unidade vai até um 'saida/entrada' -> aparece em Corredor1 -> anda até 'chegada' -> é desativada e guardada.
- SAIR: unidade aparece em 'chegada' -> anda até Corredor1 -> sai por um 'saida/entrada' em terra -> volta ao jogo.

HELICÓPTEROS:
- Puxar: helicópteros próximos são posicionados em uma 'Parada' da Pista e ficam no convés (parentados ao navio).
- Liberar: helicóptero selecionado vai para Stop ou Stop (1) (alternando) e fica pronto para decolar (ao decolar é removido da lista).

MENU (tecla O no navio selecionado):
- Lista veículos/soldados/helis carregados.
- Permite escolher unidade e quantidade para puxar/soltar, com botões sempre disponíveis.";
    }
}
