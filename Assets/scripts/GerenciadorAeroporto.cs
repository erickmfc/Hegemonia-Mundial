using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;

public class GerenciadorAeroporto : MonoBehaviour
{
    private static readonly List<DadosConstrucao> fichasAereasNavaisCache = new List<DadosConstrucao>();
    private static bool fichasAereasNavaisCarregadas;

    /// <summary>
    /// Lista as fichas aeronauticas que podem usar o ciclo de convés existente.
    /// Aeronaves comerciais e transportes com controlador especializado ficam
    /// fora porque usam bases e rotinas de entrega próprias.
    /// </summary>
    public static void ListarFichasAereasNavais(List<DadosConstrucao> destino, bool incluirAvioes, bool incluirHelicopteros)
    {
        if (destino == null) return;
        destino.Clear();

        if (!fichasAereasNavaisCarregadas)
        {
            fichasAereasNavaisCache.Clear();
            fichasAereasNavaisCache.AddRange(Resources.LoadAll<DadosConstrucao>("Construcoes"));
            fichasAereasNavaisCache.Sort((a, b) =>
                string.Compare(a != null ? a.GetDisplayName() : string.Empty,
                    b != null ? b.GetDisplayName() : string.Empty,
                    System.StringComparison.OrdinalIgnoreCase));
            fichasAereasNavaisCarregadas = true;
        }

        for (int i = 0; i < fichasAereasNavaisCache.Count; i++)
        {
            DadosConstrucao ficha = fichasAereasNavaisCache[i];
            if (ficha == null || ficha.categoria != DadosConstrucao.CategoriaItem.Aeronautica
                || !ficha.TryGetPrefabBasico(out GameObject prefab)) continue;

            if (prefab.GetComponentInChildren<ControleAviaoComercial>(true) != null
                || prefab.GetComponentInChildren<C700TransporteAereo>(true) != null
                || prefab.GetComponentInChildren<Hegemonia.Aeronaves.C17.C17TransporteController>(true) != null)
            {
                continue;
            }

            bool helicoptero = prefab.GetComponentInChildren<Helicoptero>(true) != null;
            if ((helicoptero && incluirHelicopteros) || (!helicoptero && incluirAvioes))
            {
                destino.Add(ficha);
            }
        }
    }

    // Estruturas comerciais removidas e transferidas para GerenciadorAeroportoComercial.cs
    
    private enum ModoOrdemHelicoptero
    {
        Nenhum,
        Reconhecimento,
        Patrulha,
        AtaqueLocal,
        Transporte
    }

    [Header("Diagnóstico")]
    [SerializeField] private bool logarDiagnosticoDetalhado = false;

    [Header("Hierarquia do Aeroporto (Vincular do Inspector)")]
    [Tooltip("Grupo pai contendo as marcações 'Parada' a 'Parada 4'")]
    public Transform patio;

    [Tooltip("Grupo adicional de vagas extras, como Patio_Militar.")]
    public Transform patioMilitar;
    
    [Tooltip("Grupo contendo 'Preparacao' e 'Pronto'")]
    public Transform hangarAviao;
    
    [Tooltip("Grupo de waypoints de decolagem: alinhamento -> decolagem -> voo...")]
    public Transform decolagem;
    
    [Tooltip("Grupo de waypoints de pouso OBRIGATÓRIOS (Decida)")]
    public Transform decida;

    [Header("Gestão de Frota e Status")]
    public List<ControleAviao> avioesNoPatio = new List<ControleAviao>();
    public List<ControleAviao> avioesNoHangar = new List<ControleAviao>();
    public List<C700TransporteAereo> transportesC700NoPatio = new List<C700TransporteAereo>();
    public List<Helicoptero> helicopterosDoAeroporto = new List<Helicoptero>();

    [Header("Drone Kamikaze")]
    public GameObject prefabDroneKamikaze;
    public int precoDroneKamikaze = 1500;

    [Header("Su-11")]
    public GameObject prefabSu11;
    public int precoSu11 = 2500;

    [Header("Marcadores")]
    public GameObject prefabMarcadorPatrulhaAviao; // Marker 5 Circle Loop
    public GameObject prefabMarcadorBombardeiro; // Marker 7 Danger zone Loop

    [Header("Interface (UI)")]
    public GameObject menuAeroportoUI;
    
    private bool menuAtivo = false;
#pragma warning disable 0414
    private int abaAtual = 0;
#pragma warning restore 0414
    private Vector2 scrollPosFrota;
    private Vector2 scrollPosHangar;
    private Vector2 scrollPosC700;
    private Vector2 scrollPosAbaMilitar;
    [HideInInspector] public ControleAviao aviaoSelecionadoParaMissao;
    [HideInInspector] public C700TransporteAereo c700SelecionadoParaMissao;
    [HideInInspector] public Helicoptero helicopteroSelecionadoParaMissao;

    // Listas internas de Waypoints lidas no Awake
    [HideInInspector] public List<Transform> waypointsPatio = new List<Transform>();
    [HideInInspector] public Transform wpPreparacao;
    [HideInInspector] public Transform wpPronto;
    [HideInInspector] public List<Transform> waypointsDecolagem = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxi = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida = new List<Transform>();
    
    [HideInInspector] public Transform wpAndadar;
    [HideInInspector] public Transform wpAnalise;

    [HideInInspector] public bool esperandoCliqueMassa = false;
    [HideInInspector] public int qtdMassaDrone = 1;
    [HideInInspector] public bool esperandoCliquePatrulhaGrupo = false;
    [HideInInspector] public int qtdPatrulhaGrupo = 1;

    // --- CACHE DE COMPONENTES (Evita GetComponent repetido) ---
    protected IdentidadeUnidade _identidadeCacheada;
    protected bool _identidadeVerificada = false;

    // --- CACHE PARA OnGUI (Evita alocações repetidas) ---
    private readonly HashSet<Transform> _vagasOcupadas = new HashSet<Transform>();
    private readonly List<Vector3> _posicoesVagaOcupadas = new List<Vector3>(64);
    private readonly List<Vector3> _rotaPatrulhaHelicoptero = new List<Vector3>();
    private readonly List<ControleAviao> _bufferSortidaAereaIA = new List<ControleAviao>(24);
    private readonly List<GerenciadorAeroporto> _basesDestinoAereoUI = new List<GerenciadorAeroporto>(16);
    private Camera cameraPrincipal;
    private ModoOrdemHelicoptero _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
    private string _modeloPatrulhaGrupo = string.Empty;
    private string _ultimoModeloPainelPatrulha = string.Empty;
    private bool _usarMarcadorPatrulhaAviaoNoProximoClique = false;
    private bool _modoPousoC17Pendente = false;
    private float _proximaSortidaIA = -999f;
    private float _proximoReporPatioTime = -999f;
    private float _proximaManutencaoListas = -999f;
    private Transform _vagaHelicopteroReserva;
    private bool _taxiAutomaticoBaseMilitar;

    [Header("⚡ Energia")]
    public bool semEnergia = false;
    [Tooltip("Exibe o painel flutuante de status ao passar o cursor sobre o aeroporto.")]
    [SerializeField] private bool exibirTooltipStatus = false;
    private bool mouseHover = false;
    private Texture2D _texturaTooltip;

    // Estilos e texturas em cache do Menu Tático Z (privados para manter compatibilidade com subclasses)
    private Vector2 scrollPosOrdensUI;
    private GUISkin _uiAeroportoSkinCache;
    private bool _uiAeroportoEstilosProntos;
    private Texture2D _texAeroPainelFundo;
    private Texture2D _texAeroBorda;
    private Texture2D _texAeroCabecalho;
    private Texture2D _texAeroDestaqueCiano;
    private Texture2D _texAeroCardFundo;
    private Texture2D _texAeroBotaoNormal;
    private Texture2D _texAeroBotaoHover;
    private Texture2D _texAeroBotaoAtivo;
    private Texture2D _texAeroBotaoSelecionado;
    private Texture2D _texAeroBotaoPrimario;
    private Texture2D _texAeroBotaoPrimarioHover;
    private Texture2D _texAeroBotaoPerigo;
    private Texture2D _texAeroBotaoPerigoHover;
    private Texture2D _texAeroBotaoAlerta;
    private Texture2D _texAeroAlertaFundo;
    private GUIStyle _stAeroCard;
    private GUIStyle _stAeroAlertaCard;
    private GUIStyle _stAeroTituloHeader;
    private GUIStyle _stAeroSubtituloHeader;
    private GUIStyle _stAeroBadge;
    private GUIStyle _stAeroSecaoTitulo;
    private GUIStyle _stAeroLabel;
    private GUIStyle _stAeroLabelWrap;
    private GUIStyle _stAeroLabelMuted;
    private GUIStyle _stAeroBotao;
    private GUIStyle _stAeroBotaoLista;
    private GUIStyle _stAeroBotaoListaSelecionado;
    private GUIStyle _stAeroBotaoPrimario;
    private GUIStyle _stAeroBotaoPerigo;
    private GUIStyle _stAeroBotaoAlerta;
    private GUIStyle _stAeroBotaoFechar;

    public void SetarSemEnergia(bool status)
    {
        if (semEnergia == status) return;
        semEnergia = status;
        if (semEnergia)
        {
            Debug.Log($"[ENERGIA] Aeroporto {name} está sem energia! Operações e compras bloqueadas.");
            HUDAjudaRTS.MostrarMensagemTemporaria(LocalizationManager.T("airport.no_power_purchase", "AEROPORTO SEM ENERGIA\nConstrua uma usina para reativá-lo."), 4.5f);
        }
    }

    [Header("Spawn IA (Antitravada)")]
    [Tooltip("Quando ativo, compras de aeronaves da IA entram em fila e sao instanciadas aos poucos para evitar queda brutal de FPS/GC.")]
    [SerializeField] private bool usarFilaSpawnAereoIA = true;
    [SerializeField] private float intervaloSpawnIaSaudavel = 0.55f;
    [SerializeField] private float intervaloSpawnIaPressao = 1.7f;
    [SerializeField] private float intervaloSpawnIaCritico = 3.5f;
    private readonly Queue<GameObject> _filaSpawnAeronavesIA = new Queue<GameObject>();
    private float _proximoSpawnAeronaveIA = -999f;
    private float _primeiroEnfileiramentoAeronaveIA = -999f;
    private bool _interacaoManualSolicitada;

    public int ContarUnidadesPendentesIA()
    {
        if (_filaSpawnAeronavesIA == null || _filaSpawnAeronavesIA.Count == 0)
        {
            return 0;
        }

        int total = 0;
        foreach (GameObject prefab in _filaSpawnAeronavesIA)
        {
            if (prefab == null)
            {
                continue;
            }

            IdentidadeUnidade idPrefab = prefab.GetComponentInChildren<IdentidadeUnidade>(true);
            if (idPrefab != null && idPrefab.tipoUnidade == TipoUnidade.Estrutura)
            {
                continue;
            }

            if (prefab.GetComponentInChildren<ControleAviaoComercial>(true) != null)
            {
                continue;
            }

            total++;
        }

        return total;
    }

    public int ObterTeamIdSeguro()
    {
        if (!_identidadeVerificada || _identidadeCacheada == null)
        {
            _identidadeCacheada = GetComponent<IdentidadeUnidade>();
            if (_identidadeCacheada == null)
            {
                _identidadeCacheada = GetComponentInParent<IdentidadeUnidade>();
            }
            _identidadeVerificada = true;
        }

        return _identidadeCacheada != null ? _identidadeCacheada.teamID : 0;
    }

    protected static int RemoveNulls<T>(List<T> lista) where T : class
    {
        if (lista == null)
        {
            return 0;
        }

        int removidos = 0;
        for (int i = lista.Count - 1; i >= 0; i--)
        {
            if (lista[i] == null)
            {
                lista.RemoveAt(i);
                removidos++;
            }
        }

        return removidos;
    }

    public GameObject ObterPrefabMarcadorPatrulhaAviao()
    {
        return prefabMarcadorPatrulhaAviao;
    }

    protected virtual void Awake()
    {
#if UNITY_EDITOR
        GarantirPrefabsMarcadoresNoEditor();
#endif

        if (prefabMarcadorPatrulhaAviao != null)
        {
            PoolDeObjetosCombate.RegistrarMarcadorPatrulhaAerea(prefabMarcadorPatrulhaAviao);
            PoolDeObjetosCombate.Prewarm(prefabMarcadorPatrulhaAviao, 2);
        }

        if (prefabMarcadorBombardeiro != null)
        {
            PoolDeObjetosCombate.Prewarm(prefabMarcadorBombardeiro, 2);
        }

        // Cache da identidade do aeroporto
        _identidadeCacheada = GetComponent<IdentidadeUnidade>();
        _identidadeVerificada = true;

        if (patio != null)
        {
            RegistrarWaypointsPatio(patio);
        }

        if (patioMilitar == null)
        {
            patioMilitar = EncontrarGrupoPatioMilitar();
        }

        if (patioMilitar != null && patioMilitar != patio)
        {
            RegistrarWaypointsPatio(patioMilitar);
        }

        if (hangarAviao != null)
        {
            wpPreparacao = EncontrarFilhoPorNome(hangarAviao, "Preparacao");
            wpPronto = EncontrarFilhoPorNome(hangarAviao, "Pronto");

            // Algumas bases importadas guardam apenas um ponto "Pronto" ou
            // apontam o campo hangarAviao diretamente para esse ponto. Nesse
            // caso o avião não pode nascer na raiz da base: usa o ponto
            // configurado como saída/espera do hangar para os dois estágios.
            if (wpPreparacao == null && wpPronto == null)
            {
                wpPreparacao = hangarAviao;
                wpPronto = hangarAviao;
            }
            else if (wpPreparacao == null)
            {
                // Na Base Militar, "criando" é o ponto de spawn dentro do
                // hangar e "Pronto" é a saída para o pátio. Não colapse as
                // duas fases no mesmo Transform.
                bool hangarEhPontoDeCriacao = EhAeroportoMilitar()
                    && hangarAviao != null
                    && hangarAviao.name.IndexOf("criando", System.StringComparison.OrdinalIgnoreCase) >= 0;
                wpPreparacao = hangarEhPontoDeCriacao ? hangarAviao : wpPronto;
            }
            else if (wpPronto == null)
            {
                wpPronto = wpPreparacao;
            }
        }

        if (decolagem != null)
        {
            foreach (Transform filho in decolagem) waypointsDecolagem.Add(filho);
        }

        CarregarTaxiAutomaticoBaseMilitar();

        // A capacidade do pátio é exclusivamente a capacidade real configurada
        // na cena. Antes este fallback criava 24 GameObjects por aeroporto,
        // inflando a capacidade e mascarando a falta de hangar. Sem vagas reais,
        // a aeronave permanece no hangar interno até que uma vaga seja liberada.
        if (!(this is GerenciadorPortaAvioes) && waypointsPatio.Count == 0)
        {
            Debug.LogWarning("[Aeroporto] Nenhuma vaga real configurada; novas aeronaves aguardarão no hangar.", this);
        }

        if (decida != null)
        {
            foreach (Transform filho in decida) waypointsDecida.Add(filho);
            // Como o objeto no Unity está do inicio (Freiada) ao fim (Alinhando)
            // e o avião entra pelo Alinhando, invertemos a lista inteira!
            waypointsDecida.Reverse();
        }

        // A nova Base Militar reaproveita a mesma sequência física da pista
        // para a aproximação, mas seu campo decida pode apontar para um ponto
        // sem filhos. Reusar a sequência de decolagem invertida mantém o
        // pouso compatível com o sistema atual sem criar uma rota paralela.
        if (waypointsDecida.Count == 0 && waypointsDecolagem.Count > 0)
        {
            waypointsDecida.AddRange(waypointsDecolagem);
            waypointsDecida.Reverse();
        }

        CorrigirOrientacaoDeEstacionamentoMilitar();

        // Tenta achar Andadar e Analise (em qualquer lugar dentro do Aeroporto)
        Transform[] todasAsTags = GetComponentsInChildren<Transform>(true);
        for (int i = 0, count = todasAsTags.Length; i < count; i++)
        {
            string nome = todasAsTags[i].name.ToLower();
            if (nome == "andadar") wpAndadar = todasAsTags[i];
            else if (nome == "analise") wpAnalise = todasAsTags[i];
            // Sai mais cedo se já encontrou ambos
            if (wpAndadar != null && wpAnalise != null) break;
        }
    }

    protected virtual void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    protected virtual void OnDisable()
    {
        GestorMenusExclusivos.Fechar(this);
        if (_interacaoManualSolicitada)
        {
            InteractionModeService.Release(this, ObterDonoInteracaoManual());
            _interacaoManualSolicitada = false;
        }
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        GestorMenusExclusivos.Fechar(this);
        if (_interacaoManualSolicitada)
        {
            InteractionModeService.Release(this, ObterDonoInteracaoManual());
            _interacaoManualSolicitada = false;
        }
        LiberarRecursosUIAeroporto();
        RegistroEntidadesJogo.Unregister(this);
    }

    private static void DestruirTexturaSeguro(ref Texture2D textura)
    {
        if (textura != null)
        {
            Destroy(textura);
            textura = null;
        }
    }

    private void LiberarRecursosUIAeroporto()
    {
        DestruirTexturaSeguro(ref _texturaTooltip);
        DestruirTexturasTemaAeroporto();
        _uiAeroportoEstilosProntos = false;
        _uiAeroportoSkinCache = null;
    }

    protected virtual void Start()
    {
        DiagnosticarConfiguracaoOperacional();
        // Inicia o serviço de reparação automática
        StartCoroutine(ManutencaoDeFrota());
    }

    /// <summary>
    /// Classificação central usada pelas IAs e pelos serviços militares.
    /// Mantém aeroportos comerciais e porta-aviões fora da categoria militar,
    /// mas reconhece a Base Militar mesmo quando o prefab não tem caça
    /// configurado no Inspector.
    /// </summary>
    public bool EhAeroportoMilitar()
    {
        if (this is GerenciadorAeroportoComercial || this is GerenciadorPortaAvioes)
        {
            return false;
        }

        if (patioMilitar != null || prefabSu11 != null)
        {
            return true;
        }

        string nomeNormalizado = name == null ? string.Empty : name.ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
        return nomeNormalizado.Contains("militar")
            || nomeNormalizado.Contains("baseaerea")
            || nomeNormalizado.Contains("airbase");
    }

    private static Transform EncontrarFilhoPorNome(Transform raiz, string nome)
    {
        if (raiz == null || string.IsNullOrWhiteSpace(nome)) return null;
        Transform encontrado = raiz.Find(nome);
        if (encontrado != null) return encontrado;

        Transform[] filhos = raiz.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform candidato = filhos[i];
            if (candidato != null && string.Equals(candidato.name, nome, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidato;
            }
        }

        return null;
    }

    private void CarregarTaxiAutomaticoBaseMilitar()
    {
        if (!EhAeroportoMilitar() || this is GerenciadorPortaAvioes || waypointsTaxi.Count > 0)
        {
            return;
        }

        Transform grupoTaxi = EncontrarFilhoPorNome(transform, "taxiando");
        if (grupoTaxi == null || grupoTaxi.childCount == 0)
        {
            return;
        }

        // Os pontos do prefab estão em ordem hangar -> pista. A lista de
        // waypointsTaxi é consumida no pouso (pista -> estacionamento), então
        // escolhemos a ponta mais próxima do início da pista como entrada.
        bool inverterParaPouso = true;
        if (waypointsDecolagem.Count > 0)
        {
            Vector3 inicioPista = waypointsDecolagem[0].position;
            float distanciaPrimeiro = (grupoTaxi.GetChild(0).position - inicioPista).sqrMagnitude;
            float distanciaUltimo = (grupoTaxi.GetChild(grupoTaxi.childCount - 1).position - inicioPista).sqrMagnitude;
            inverterParaPouso = distanciaUltimo <= distanciaPrimeiro;
        }

        if (inverterParaPouso)
        {
            for (int i = grupoTaxi.childCount - 1; i >= 0; i--) waypointsTaxi.Add(grupoTaxi.GetChild(i));
        }
        else
        {
            for (int i = 0; i < grupoTaxi.childCount; i++) waypointsTaxi.Add(grupoTaxi.GetChild(i));
        }

        _taxiAutomaticoBaseMilitar = waypointsTaxi.Count > 0;
    }

    private void CorrigirOrientacaoDeEstacionamentoMilitar()
    {
        if (!EhAeroportoMilitar() || this is GerenciadorPortaAvioes)
        {
            return;
        }

        Vector3 direcaoPista = Vector3.zero;
        if (waypointsDecolagem.Count > 1 && waypointsDecolagem[0] != null && waypointsDecolagem[1] != null)
        {
            direcaoPista = waypointsDecolagem[1].position - waypointsDecolagem[0].position;
        }

        direcaoPista.y = 0f;
        if (direcaoPista.sqrMagnitude < 0.01f)
        {
            direcaoPista = transform.forward;
            direcaoPista.y = 0f;
        }
        if (direcaoPista.sqrMagnitude < 0.01f) direcaoPista = Vector3.forward;

        Quaternion rumoDaPista = Quaternion.LookRotation(direcaoPista.normalized, Vector3.up);
        for (int i = 0; i < waypointsPatio.Count; i++)
        {
            Transform vaga = waypointsPatio[i];
            if (vaga == null) continue;

            // Mantém a alternância de 180 graus marcada em cada vaga, mas
            // remove inclinação/rotação herdada dos nós de importação GLB.
            float diferencaRumo = vaga.localRotation.eulerAngles.y;
            vaga.rotation = rumoDaPista * Quaternion.Euler(0f, diferencaRumo, 0f);
        }

        if (wpPreparacao != null) wpPreparacao.rotation = rumoDaPista;
        if (wpPronto != null) wpPronto.rotation = rumoDaPista;
    }

    public List<Transform> ObterWaypointsTaxiDecolagem()
    {
        if (!_taxiAutomaticoBaseMilitar || waypointsTaxi == null || waypointsTaxi.Count == 0)
        {
            return null;
        }

        List<Transform> rota = new List<Transform>(waypointsTaxi);
        rota.Reverse();
        return rota;
    }

    private void DiagnosticarConfiguracaoOperacional()
    {
        if (!EhAeroportoMilitar()) return;

        string aviso = string.Empty;
        if (waypointsPatio == null || waypointsPatio.Count == 0) aviso += " pátio/vagas;";
        if (wpPreparacao == null) aviso += " saída do hangar;";
        if (wpPronto == null) aviso += " ponto pronto;";
        if (waypointsDecolagem == null || waypointsDecolagem.Count < 2) aviso += " pista decolagem;";
        if (waypointsDecida == null || waypointsDecida.Count < 2) aviso += " pista pouso;";

        if (string.IsNullOrEmpty(aviso))
        {
            Debug.Log($"[Aeroporto Militar] {name}: vagas={waypointsPatio.Count}, hangar={wpPreparacao.name}, decolagem={waypointsDecolagem.Count}, pouso={waypointsDecida.Count}.", this);
        }
        else
        {
            Debug.LogWarning($"[Aeroporto Militar] {name}: referências incompletas:{aviso}", this);
        }

        if (logarDiagnosticoDetalhado)
        {
            LogarPontosOperacionais("Vaga", waypointsPatio);
            LogarPontosOperacionais("Decolagem", waypointsDecolagem);
            LogarPontosOperacionais("Pouso", waypointsDecida);
        }

        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        int creates = 0;
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho == null || !filho.name.StartsWith("Create Patrulha Aerea", System.StringComparison.OrdinalIgnoreCase)) continue;
            creates++;
            if (logarDiagnosticoDetalhado)
            {
                Debug.Log($"[Aeroporto Militar] {name}: {filho.name} pos={filho.position.ToString("F1")} setor local={filho.localPosition.ToString("F1")}", filho);
            }
        }

        if (creates == 0 && logarDiagnosticoDetalhado)
        {
            Debug.Log($"[Aeroporto Militar] {name}: nenhum Create de patrulha no prefab; o diretor da IA poderá criá-los em runtime.", this);
        }
    }

    private static void LogarPontosOperacionais(string tipo, IList<Transform> pontos)
    {
        if (pontos == null) return;
        for (int i = 0; i < pontos.Count; i++)
        {
            Transform ponto = pontos[i];
            if (ponto == null) continue;
            Debug.Log($"[Aeroporto Militar] {tipo} {i + 1}: {ponto.name} pos={ponto.position.ToString("F1")} dir={ponto.forward.ToString("F2")}", ponto);
        }
    }

    private IEnumerator ManutencaoDeFrota()
    {
        WaitForSeconds espera = new WaitForSeconds(2.0f); // Reutiliza o objeto de espera
        while (true)
        {
            yield return espera;

            // Repara quem está no pátio
            for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
            {
                ControleAviao a = avioesNoPatio[i];
                if (a == null) { avioesNoPatio.RemoveAt(i); continue; }
                bool estacionado = !a.estaEmModoVooFisico
                    && a.estadoAtual != ControleAviao.EstadoAviao.EmMissao
                    && a.estadoAtual != ControleAviao.EstadoAviao.Decolando;
                if (!estacionado) continue;

                if (a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    SistemaDeDanos sd = a.GetComponent<SistemaDeDanos>();
                    if (sd != null && sd.vidaAtual < sd.vidaMaxima)
                        sd.Reparar(sd.vidaMaxima * 0.05f); // 5% de vida no pátio
                }

                // Mantém o abastecimento tentando enquanto a aeronave fica
                // parada, inclusive depois de o estoque nacional ser reposto.
                ReabastecerAeronave(a, 2f);
            }

            // No hangar o reparo é prioritário
            for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
            {
                ControleAviao h = avioesNoHangar[i];
                if (h == null) { avioesNoHangar.RemoveAt(i); continue; }

                SistemaDeDanos sd = h.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaAtual < sd.vidaMaxima) 
                    sd.Reparar(sd.vidaMaxima * 0.10f); // 10% de vida no hangar

                ReabastecerAeronave(h, 2f);
            }

            for (int i = transportesC700NoPatio.Count - 1; i >= 0; i--)
            {
                C700TransporteAereo c700 = transportesC700NoPatio[i];
                if (c700 == null) { transportesC700NoPatio.RemoveAt(i); continue; }
                if (c700.EstaNoSolo)
                {
                    ReabastecerAeronave(c700, 2f);
                }
            }

            for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
            {
                Helicoptero helicoptero = helicopterosDoAeroporto[i];
                if (helicoptero == null) { helicopterosDoAeroporto.RemoveAt(i); continue; }
                if (helicoptero.EstaEstacionadoNoAeroporto())
                {
                    ReabastecerAeronave(helicoptero, 2f);
                }
            }
        }
    }

    private void ReabastecerAeronave(Component aeronave, float deltaServico)
    {
        if (aeronave == null)
        {
            return;
        }

        CombustivelUnidade combustivel = CombustivelUnidade.ObterParaObjeto(aeronave.gameObject);
        if (combustivel == null)
        {
            combustivel = CombustivelUnidade.Garantir(aeronave.gameObject, false);
        }

        if (combustivel != null && combustivel.CombustivelAtual < combustivel.Capacidade)
        {
            ServicoAbastecimento.TentarAbastecer(combustivel, 45f * Mathf.Max(0.1f, deltaServico), out _);
        }

        RacaoUnidade racao = RacaoUnidade.ObterParaObjeto(aeronave.gameObject);
        if (racao != null && racao.UsaRacao && racao.RacaoAtual < racao.Capacidade)
        {
            racao.AbastecerUsandoEstoqueNacional(90f * Mathf.Max(0.1f, deltaServico));
        }

        // O serviço periódico só é chamado para aeronaves já estacionadas no
        // pátio/hangar. Rearma aqui também para que uma aeronave que ficou em
        // reserva não dependa de pousar novamente para recuperar munição.
        LancadorMisselCaca[] misseis = aeronave.GetComponentsInChildren<LancadorMisselCaca>(true);
        for (int i = 0; i < misseis.Length; i++)
        {
            if (misseis[i] != null)
            {
                misseis[i].RecarregarCompletoNaBase();
            }
        }

        LancadorMisseis[] armamentoMultiplo = aeronave.GetComponentsInChildren<LancadorMisseis>(true);
        for (int i = 0; i < armamentoMultiplo.Length; i++)
        {
            if (armamentoMultiplo[i] != null)
            {
                armamentoMultiplo[i].municaoAtual = armamentoMultiplo[i].municaoMaxima;
            }
        }

        SistemaArmamentoHelice[] canhoes = aeronave.GetComponentsInChildren<SistemaArmamentoHelice>(true);
        for (int i = 0; i < canhoes.Length; i++)
        {
            if (canhoes[i] != null)
            {
                canhoes[i].RecarregarCompletoNaBase();
            }
        }

        ControleTorretaModular[] torretas = aeronave.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < torretas.Length; i++)
        {
            if (torretas[i] == null || torretas[i].armas == null) continue;
            for (int j = 0; j < torretas[i].armas.Count; j++)
            {
                if (torretas[i].armas[j] != null)
                {
                    torretas[i].armas[j].municaoAtual = torretas[i].armas[j].tamanhoCartucho;
                }
            }
        }

        // Preserve a missão de uma aeronave estacionada sem estoque e
        // reavalie-a depois de cada transferência incremental de combustível.
        ControleAviao aviao = aeronave as ControleAviao;
        if (aviao != null)
        {
            aviao.TentarRetomarMissaoAposReabastecimento();
        }
    }

    private void ProcessarFilaCompraAeronavesIA()
    {
        if (!usarFilaSpawnAereoIA) return;
        if (_filaSpawnAeronavesIA == null || _filaSpawnAeronavesIA.Count == 0) return;
        if (Time.unscaledTime < _proximoSpawnAeronaveIA) return;

        // A fila conserva pedidos pagos enquanto o país não puder mobilizar o
        // efetivo da próxima aeronave. Não descarte a unidade ao tentar spawn.
        if (semEnergia || !PodeProduzirAeronave(_filaSpawnAeronavesIA.Peek())) return;

        // O primeiro caça da IA não pode ficar preso indefinidamente pelo
        // limitador de desempenho: a fila é escalonada, mas depois de alguns
        // segundos ela precisa liberar ao menos uma aeronave no próprio pátio.
        bool liberarPrimeiroMesmoSobPressao = _primeiroEnfileiramentoAeronaveIA > 0f
            && Time.unscaledTime - _primeiroEnfileiramentoAeronaveIA >= 4f;
        if (DiagnosticoDesempenhoJogo.RuntimeSaturado() && !liberarPrimeiroMesmoSobPressao)
        {
            _proximoSpawnAeronaveIA = Time.unscaledTime + Mathf.Max(0.2f, intervaloSpawnIaCritico);
            return;
        }

        GameObject prefab = _filaSpawnAeronavesIA.Peek();
        if (!ComprarAviaoImediato(prefab))
        {
            // A compra já pode ter sido aceita pelo chamador. Preserve o item
            // pago quando uma condição de spawn muda enquanto ele aguarda na
            // fila; uma tentativa recusada não pode apagar silenciosamente a
            // aeronave nem deixar o débito sem entrega.
            float intervaloFalha = DiagnosticoDesempenhoJogo.RuntimeSaturado()
                ? intervaloSpawnIaCritico
                : DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                    ? intervaloSpawnIaPressao
                    : intervaloSpawnIaSaudavel;
            _proximoSpawnAeronaveIA = Time.unscaledTime + Mathf.Max(0.05f, intervaloFalha);
            return;
        }

        _filaSpawnAeronavesIA.Dequeue();
        if (_filaSpawnAeronavesIA.Count == 0) _primeiroEnfileiramentoAeronaveIA = -999f;

        float cooldown = intervaloSpawnIaSaudavel;
        if (DiagnosticoDesempenhoJogo.RuntimeSaturado()) cooldown = intervaloSpawnIaCritico;
        else if (DiagnosticoDesempenhoJogo.RuntimeSobPressao()) cooldown = intervaloSpawnIaPressao;

        _proximoSpawnAeronaveIA = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
    }

    protected virtual void Update()
    {
        if (!Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada)
            ProcessarFilaCompraAeronavesIA();
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;

        // A limpeza de referencias e uma manutencao de baixa prioridade;
        // executa em intervalo, mantendo a resposta normal dos menus e da
        // pista sem percorrer as listas em todos os frames.
        if (Time.unscaledTime >= _proximaManutencaoListas)
        {
            _proximaManutencaoListas = Time.unscaledTime + 0.5f;
            RemoveNulls(helicopterosDoAeroporto);
            LimparHelicopterosTransferidos();
        }

        // Replenish patio from hangar periodically if space is available
        if (avioesNoHangar.Count > 0 && Time.time >= _proximoReporPatioTime)
        {
            _proximoReporPatioTime = Time.time + 2.0f;
            ReporPatioComAvioesDoHangar();
        }

        if (Construtor.EmModoConstrucaoAtivo)
        {
            if (menuAtivo || aviaoSelecionadoParaMissao != null || c700SelecionadoParaMissao != null || helicopteroSelecionadoParaMissao != null)
            {
                CancelarInteracaoPorConstrucao();
            }
            AtualizarModoInteracaoManualAeroporto();
            return;
        }

        if (Hegemonia.RTS.RTSInputBindings.IsTextEntryFocused()) return;

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Keypad7))
        {
            AlternarMenuPorAtalho();
        }

        if (menuAtivo && !GestorMenusExclusivos.EstaAtivo(this))
        {
            menuAtivo = false;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        }

        if (c700SelecionadoParaMissao != null)
        {
            if (c700SelecionadoParaMissao.AguardandoDestinoAereo && Input.GetMouseButtonDown(1))
            {
                if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

                if (cameraPrincipal == null) return;
                Ray rC700 = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                Vector3 pontoAlvoC700 = Vector3.zero;

                if (Physics.Raycast(rC700, out RaycastHit hitC700, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    pontoAlvoC700 = hitC700.point;
                }
                else
                {
                    UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                    if (plano.Raycast(rC700, out float distC700))
                    {
                        pontoAlvoC700 = rC700.GetPoint(distC700);
                    }
                }

                if (pontoAlvoC700 != Vector3.zero)
                {
                    c700SelecionadoParaMissao.ReceberOrdemMover(pontoAlvoC700);
                    CriarSinalizador(pontoAlvoC700, c700SelecionadoParaMissao);
                    c700SelecionadoParaMissao = null;
                }
            }
        }

        if (_modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum && helicopteroSelecionadoParaMissao != null)
        {
            ProcessarOrdemHelicoptero();
        }

        if (aviaoSelecionadoParaMissao == null)
        {
            AtualizarModoInteracaoManualAeroporto();
            return;
        }

        bool esperandoAutorizacao = aviaoSelecionadoParaMissao.aguardandoCliqueRadar;
        bool emVooConstante = (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao);

        if (!esperandoAutorizacao && !emVooConstante)
        {
            AtualizarModoInteracaoManualAeroporto();
            return;
        }
        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

        if (cameraPrincipal == null) return;
        Ray r = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        Vector3 pontoAlvo = Vector3.zero;

        if (Physics.Raycast(r, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
        }
        else
        {
            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
            float distanciaIntersecao;
            if (marPlano.Raycast(r, out distanciaIntersecao))
            {
                pontoAlvo = r.GetPoint(distanciaIntersecao);
            }
            else
            {
                return;
            }
        }

        bool usarMarcadorPatrulhaNoClique = _usarMarcadorPatrulhaAviaoNoProximoClique;

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;

        if (esperandoCliqueMassa)
        {
            int quantidadeMassa = qtdMassaDrone;
            LimparModoMassaAereo();
            StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, quantidadeMassa));
        }
        else if (esperandoCliquePatrulhaGrupo)
        {
            int quantidadeGrupo = qtdPatrulhaGrupo;
            string modeloGrupo = _modeloPatrulhaGrupo;
            LimparModoMassaAereo();
            StartCoroutine(RotinaLancarPatrulhaMesmoModelo(pontoAlvo, modeloGrupo, quantidadeGrupo));
        }
        else
        {
            // IMPORTANTE: o C17 possui máquina de estados e controlador de voo próprios.
            // Ele precisa receber a ordem aqui independentemente do EstadoAviao do ControleAviao.
            var c17Update = aviaoSelecionadoParaMissao.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>();
            if (c17Update != null)
            {
                bool comandoAceito = true;

                if (_modoPousoC17Pendente)
                {
                    comandoAceito = c17Update.IniciarModoMarcaacaoPouso(pontoAlvo);
                }
                else
                {
                    c17Update.DirecionarParaPonto(pontoAlvo);
                }

                if (!comandoAceito)
                {
                    // Área inválida: mantém o modo ativo para o jogador tentar outro ponto.
                    aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
                    AtualizarModoInteracaoManualAeroporto();
                    return;
                }

                _modoPousoC17Pendente = false;
                Debug.Log($"[Aeroporto] Ordem C17 confirmada em: {pontoAlvo}");
            }
            else
            {
                AviaoBombardeiro bombardeiroSelecionado = aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>();
                AviaoBombardeiro.ModoAtaque modoAtaqueAnterior = bombardeiroSelecionado != null
                    ? bombardeiroSelecionado.modoDeAtaque
                    : AviaoBombardeiro.ModoAtaque.Patrulha;
                Vector3 alvoAreaAnterior = bombardeiroSelecionado != null ? bombardeiroSelecionado.alvoAreaSolo : Vector3.zero;
                Vector3 alvoMassa1Anterior = bombardeiroSelecionado != null ? bombardeiroSelecionado.alvoMassa1 : Vector3.zero;
                Vector3 alvoMassa2Anterior = bombardeiroSelecionado != null ? bombardeiroSelecionado.alvoMassa2 : Vector3.zero;
                if (bombardeiroSelecionado != null)
                {
                    // O painel de bombardeiro tambem precisa receber o ponto
                    // novo. Sem isso o LateUpdate podia continuar usando o
                    // alvoAreaSolo/alvoMassa de uma missão anterior.
                    if (bombardeiroSelecionado.modoDeAtaque == AviaoBombardeiro.ModoAtaque.AtaqueAoSolo)
                    {
                        bombardeiroSelecionado.alvoAreaSolo = pontoAlvo;
                    }
                    else if (bombardeiroSelecionado.modoDeAtaque == AviaoBombardeiro.ModoAtaque.AtaqueEmMassa)
                    {
                        bombardeiroSelecionado.alvoMassa1 = pontoAlvo;
                        bombardeiroSelecionado.alvoMassa2 = pontoAlvo;
                    }
                }

                // O controlador moderno decide se a aeronave esta pronta,
                // taxiando, voando ou pousando. Assim o clique nao escreve
                // apenas o GPS e deixa a patrulha antiga disputando o alvo.
                ControleUnidade controleAereoSelecionado = aviaoSelecionadoParaMissao.GetComponent<ControleUnidade>();
                bool ordemAceita;
                if (usarMarcadorPatrulhaNoClique)
                {
                    ordemAceita = controleAereoSelecionado != null
                        ? controleAereoSelecionado.EmitirOrdemPatrulha(new List<Vector3> { pontoAlvo })
                        : aviaoSelecionadoParaMissao.ReceberOrdemPatrulha(new List<Vector3> { pontoAlvo });
                }
                else
                {
                    ordemAceita = controleAereoSelecionado != null
                        ? controleAereoSelecionado.EmitirOrdemMover(pontoAlvo, true)
                        : aviaoSelecionadoParaMissao.ReceberOrdemManual(pontoAlvo);
                }
                if (!ordemAceita)
                {
                    if (bombardeiroSelecionado != null)
                    {
                        bombardeiroSelecionado.modoDeAtaque = modoAtaqueAnterior;
                        bombardeiroSelecionado.alvoAreaSolo = alvoAreaAnterior;
                        bombardeiroSelecionado.alvoMassa1 = alvoMassa1Anterior;
                        bombardeiroSelecionado.alvoMassa2 = alvoMassa2Anterior;
                    }
                    aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
                    AtualizarModoInteracaoManualAeroporto();
                    Debug.LogWarning($"[Aeroporto] Ordem recusada enquanto {aviaoSelecionadoParaMissao.gameObject.name} conclui o pouso.");
                    return;
                }

                Debug.Log($"[Aeroporto] Coordenadas recebidas! {aviaoSelecionadoParaMissao.gameObject.name} recebeu {(usarMarcadorPatrulhaNoClique ? "patrulha" : "destino")}: {pontoAlvo}");
            }
        }

        CriarSinalizadorAereoNoAlvo(pontoAlvo, aviaoSelecionadoParaMissao, usarMarcadorPatrulhaNoClique);
        aviaoSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    public bool AlternarMenuPorAtalho()
    {
        if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return false;
        if (!_identidadeVerificada)
        {
            _identidadeCacheada = GetComponent<IdentidadeUnidade>();
            _identidadeVerificada = true;
        }

        if (_identidadeCacheada != null && _identidadeCacheada.teamID != 1 && _identidadeCacheada.teamID != 0)
        {
            return false;
        }

        bool novoEstadoMenu = !menuAtivo;
        if (novoEstadoMenu)
        {
            GestorMenusExclusivos.Abrir(this);
        }
        else
        {
            GestorMenusExclusivos.Fechar(this);
        }

        menuAtivo = novoEstadoMenu;
        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(menuAtivo);
        Debug.Log("[Aeroporto] Centro de Controle " + (menuAtivo ? "ABERTO" : "FECHADO"));
        return true;
    }

    public void CancelarInteracaoPorConstrucao()
    {
        if (aviaoSelecionadoParaMissao != null)
        {
            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
        }

        if (helicopteroSelecionadoParaMissao != null)
        {
            CancelarModoHelicoptero();
        }

        if (c700SelecionadoParaMissao != null)
        {
            c700SelecionadoParaMissao.CancelarModoAereo();
        }

        aviaoSelecionadoParaMissao = null;
        c700SelecionadoParaMissao = null;
        helicopteroSelecionadoParaMissao = null;
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        _rotaPatrulhaHelicoptero.Clear();
        _modoPousoC17Pendente = false;
        menuAtivo = false;
        LimparModoMassaAereo();

        if (menuAeroportoUI != null)
        {
            menuAeroportoUI.SetActive(false);
        }

        AtualizarModoInteracaoManualAeroporto();
    }

    public virtual bool PossuiOrdemManualAtiva()
    {
        bool aviaoAguardandoClique = aviaoSelecionadoParaMissao != null && aviaoSelecionadoParaMissao.aguardandoCliqueRadar;
        bool c700AguardandoClique = c700SelecionadoParaMissao != null && c700SelecionadoParaMissao.AguardandoDestinoAereo;
        bool helicopteroAguardandoClique = _modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum && helicopteroSelecionadoParaMissao != null;
        return aviaoAguardandoClique || c700AguardandoClique || helicopteroAguardandoClique || esperandoCliqueMassa || esperandoCliquePatrulhaGrupo;
    }

    protected virtual InteractionOwner ObterDonoInteracaoManual()
    {
        return InteractionOwner.AirportOrder;
    }

    protected void AtualizarModoInteracaoManualAeroporto()
    {
        InteractionOwner owner = ObterDonoInteracaoManual();
        if (PossuiOrdemManualAtiva())
        {
            if (!_interacaoManualSolicitada || !InteractionModeService.IsActive(this, owner))
            {
                InteractionModeService.Request(
                    this,
                    owner,
                    new InteractionPolicy
                    {
                        bloqueiaSelecao = true,
                        bloqueiaOrdemMundo = true,
                        bloqueiaRotacaoCamera = true,
                        consomeLMB = false,
                        consomeRMB = true
                    },
                    "Ordem manual aerea aguardando clique");
            }
            _interacaoManualSolicitada = true;
        }
        else
        {
            if (_interacaoManualSolicitada)
            {
                InteractionModeService.Release(this, owner);
                _interacaoManualSolicitada = false;
            }
        }
    }

    private void ProcessarOrdemHelicoptero()
    {
        if (cameraPrincipal == null || helicopteroSelecionadoParaMissao == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelarModoHelicoptero();
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelarModoHelicoptero();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EncerrarModoHelicoptero();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _rotaPatrulhaHelicoptero.Count > 0)
            {
                _rotaPatrulhaHelicoptero.RemoveAt(_rotaPatrulhaHelicoptero.Count - 1);
                if (_rotaPatrulhaHelicoptero.Count > 0)
                {
                    helicopteroSelecionadoParaMissao.ReceberOrdemPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
                }
                else
                {
                    helicopteroSelecionadoParaMissao.CancelarMissaoAeroporto();
                }
                return;
            }
        }

        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

        if (!TryResolverPontoMapa(out Vector3 pontoAlvo))
        {
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
        {
            _rotaPatrulhaHelicoptero.Add(pontoAlvo);
            bool ordemAceita = helicopteroSelecionadoParaMissao.ReceberOrdemPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
            if (ordemAceita)
            {
                CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
                Debug.Log($"[Aeroporto] Patrulha atualizada com {_rotaPatrulhaHelicoptero.Count} ponto(s). Clique direito adiciona mais, ENTER encerra edição.");
            }
            else
            {
                _rotaPatrulhaHelicoptero.RemoveAt(_rotaPatrulhaHelicoptero.Count - 1);
                Debug.LogWarning("[Aeroporto] O helicóptero recusou a patrulha; ponto não registrado.");
            }
            return;
        }

        bool ordemEspecialAceita = false;
        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Reconhecimento)
        {
            ordemEspecialAceita = helicopteroSelecionadoParaMissao.ReceberOrdemReconhecimentoAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.AtaqueLocal)
        {
            ordemEspecialAceita = helicopteroSelecionadoParaMissao.ReceberOrdemAtaqueLocalAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Transporte)
        {
            helicopteroSelecionadoParaMissao.IniciarTransporteAeroporto(pontoAlvo);
            ordemEspecialAceita = true;
        }

        if (ordemEspecialAceita)
        {
            CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
        }
        else
        {
            Debug.LogWarning("[Aeroporto] O helicóptero recusou a ordem especial; nenhum sinalizador foi criado.");
        }
        EncerrarModoHelicoptero();
    }

    private bool TryResolverPontoMapa(out Vector3 pontoAlvo)
    {
        pontoAlvo = Vector3.zero;
        if (cameraPrincipal == null)
        {
            return false;
        }

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
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

    private void IniciarModoHelicoptero(Helicoptero helicoptero, ModoOrdemHelicoptero modo)
    {
        if (helicoptero == null)
        {
            return;
        }

        helicopteroSelecionadoParaMissao = helicoptero;
        _modoOrdemHelicoptero = modo;
        _rotaPatrulhaHelicoptero.Clear();
        aviaoSelecionadoParaMissao = null;
        c700SelecionadoParaMissao = null;

        if (modo == ModoOrdemHelicoptero.Patrulha)
        {
            Debug.Log("[Aeroporto] Patrulha armada. O primeiro clique direito já inicia; cliques seguintes expandem a rota. ENTER encerra edição.");
        }
        else
        {
            string textoModo = "Ataque local";
            if (modo == ModoOrdemHelicoptero.Reconhecimento) textoModo = "Reconhecimento";
            else if (modo == ModoOrdemHelicoptero.Transporte) textoModo = "Transporte tático";
            Debug.Log($"[Aeroporto] {textoModo} armado para o helicóptero. Clique com o botão direito no destino.");
        }

        menuAtivo = false;
        if (menuAeroportoUI != null)
        {
            menuAeroportoUI.SetActive(false);
        }

        AtualizarModoInteracaoManualAeroporto();
    }

    private void CancelarModoHelicoptero()
    {
        _rotaPatrulhaHelicoptero.Clear();
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void EncerrarModoHelicoptero()
    {
        _rotaPatrulhaHelicoptero.Clear();
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    protected void CriarSinalizador(Vector3 pos, Component aviao)
    {
        bool ehBombardeiroOuKamikaze = false;
        if (aviao != null)
        {
            ehBombardeiroOuKamikaze = (aviao.GetComponent<AviaoBombardeiro>() != null || aviao.GetComponent<KamikazeDrone>() != null);
        }

        float escala = ehBombardeiroOuKamikaze ? 20f : 12f;
        if (PoolDeObjetosCombate.SpawnarMarcadorOrdemAerea(pos, escala, 4f, prefabMarcadorPatrulhaAviao) != null)
        {
            return;
        }

        if (ehBombardeiroOuKamikaze && prefabMarcadorBombardeiro != null)
        {
            GameObject marcadorBombardeiro = InstanciarMarcadorSeguro(prefabMarcadorBombardeiro, pos, Quaternion.identity);
            if (marcadorBombardeiro != null)
            {
                marcadorBombardeiro.transform.localScale = new Vector3(40f, 40f, 40f);
            }
        }
    }

    private void CriarSinalizadorAereoNoAlvo(Vector3 pos, Component aviao, bool forcarMarcadorPatrulha = false)
    {
        bool usarMarcadorPatrulha = forcarMarcadorPatrulha || _usarMarcadorPatrulhaAviaoNoProximoClique;
        _usarMarcadorPatrulhaAviaoNoProximoClique = false;

        bool ehBombardeiroOuKamikaze = aviao != null
            && (aviao.GetComponent<AviaoBombardeiro>() != null || aviao.GetComponent<KamikazeDrone>() != null);
        float escala = ehBombardeiroOuKamikaze ? 20f : 12f;

        if (usarMarcadorPatrulha
            && PoolDeObjetosCombate.SpawnarMarcadorOrdemAerea(pos, escala, 4f, prefabMarcadorPatrulhaAviao) != null)
        {
            return;
        }

        CriarSinalizador(pos, aviao);
    }

    private GameObject InstanciarMarcadorSeguro(UnityEngine.Object prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null)
        {
            return null;
        }

        if (prefab is GameObject prefabGo)
        {
            return PoolDeObjetosCombate.SpawnTemporario(prefabGo, posicao, rotacao, 4f);
        }

        UnityEngine.Object instancia = Instantiate(prefab, posicao, rotacao);
        if (instancia is GameObject go)
        {
            return go;
        }

        if (instancia is Component componente)
        {
            return componente.gameObject;
        }

        Debug.LogWarning($"[Aeroporto] Marcador instanciado com tipo inesperado: {instancia.GetType().Name}");
        return null;
    }

    public string UltimoMotivoFalhaAeronave { get; private set; } = string.Empty;

    public bool ComprarAviao(GameObject prefabDeAeronave)
    {
        return ComprarAviao(prefabDeAeronave, out _);
    }

    public bool ComprarAviao(GameObject prefabDeAeronave, out string motivoFalha)
    {
        motivoFalha = string.Empty;
        UltimoMotivoFalhaAeronave = string.Empty;

        if (prefabDeAeronave == null)
        {
            motivoFalha = "Prefab da aeronave inválido.";
            UltimoMotivoFalhaAeronave = motivoFalha;
            return false;
        }

        if (semEnergia)
        {
            motivoFalha = $"Aeroporto '{name.Replace("(Clone)", string.Empty).Trim()}' está sem energia.";
            UltimoMotivoFalhaAeronave = motivoFalha;
            Debug.LogWarning($"[Aeroporto] {name} está sem energia. Compra de aeronaves bloqueada.");
            return false;
        }

        if (!PodeProduzirAeronave(prefabDeAeronave))
        {
            int necessario = SistemaGovernoMundial.ObterEfetivoNecessario(prefabDeAeronave, TipoUnidade.Aereo);
            IdentidadeUnidade idBase = GetComponent<IdentidadeUnidade>();
            int teamId = idBase != null ? idBase.teamID : 1;
            int disponivel = SistemaGovernoMundial.Instancia != null
                ? SistemaGovernoMundial.Instancia.ObterEfetivoDisponivel(teamId)
                : 0;
            motivoFalha = $"Efetivo militar insuficiente: precisa {necessario:N0}, disponível {disponivel:N0}.";
            UltimoMotivoFalhaAeronave = motivoFalha;
            Debug.LogWarning($"[Aeroporto] Produção de {prefabDeAeronave.name} aguardando efetivo disponível.");
            return false;
        }

        if (prefabDeAeronave.GetComponentInChildren<C700TransporteAereo>(true) != null
            && ObterParadaRecebimentoTransporteAereo() == null)
        {
            motivoFalha = "Este aeroporto não possui pátio configurado para aeronaves C-700.";
            UltimoMotivoFalhaAeronave = motivoFalha;
            return false;
        }

        // --- SISTEMA DE IDENTIDADE (HERANÇA DO AEROPORTO) ---
        // A IA pode receber sua identidade depois do Awake, quando a base acaba
        // de ser construída. Releia a raiz a cada compra para não tratar um
        // aeroporto da IA como aeroporto do jogador por causa de cache antigo.
        _identidadeCacheada = GetComponent<IdentidadeUnidade>();
        _identidadeVerificada = true;

        bool aeroportoEhIA = _identidadeCacheada != null && _identidadeCacheada.teamID > 1;

        // Para IA, enfileira para não spawnar vários aviões no mesmo segundo e travar o jogo.
        if (aeroportoEhIA && usarFilaSpawnAereoIA)
        {
            // Validar antes de aceitar uma compra paga e enfileirá-la. O spawn
            // imediato repete a validação porque a infraestrutura pode mudar
            // enquanto a aeronave aguarda na fila.
            if (prefabDeAeronave.GetComponentInChildren<C700TransporteAereo>(true) != null
                && ObterParadaRecebimentoTransporteAereo() == null)
            {
                return false;
            }

            if (_filaSpawnAeronavesIA.Count == 0) _primeiroEnfileiramentoAeronaveIA = Time.unscaledTime;
            _filaSpawnAeronavesIA.Enqueue(prefabDeAeronave);
            ProcessarFilaCompraAeronavesIA();
            return true;
        }

        bool entregue = ComprarAviaoImediato(prefabDeAeronave);
        if (!entregue && string.IsNullOrEmpty(motivoFalha))
        {
            motivoFalha = "Não foi possível acomodar a aeronave no aeroporto selecionado (verifique espaço no pátio/pista).";
            UltimoMotivoFalhaAeronave = motivoFalha;
        }
        return entregue;
    }

    private bool PodeProduzirAeronave(GameObject prefab)
    {
        if (prefab == null) return false;

        if (SistemaGovernoMundial.ObterEfetivoNecessario(prefab, TipoUnidade.Aereo) <= 0) return true;

        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        IdentidadeUnidade identidadeBase = GetComponent<IdentidadeUnidade>();
        int teamId = identidadeBase != null ? identidadeBase.teamID : 1;
        return governo != null
            && governo.PodeProduzirUnidadeComEfetivo(teamId, prefab, 1, TipoUnidade.Aereo);
    }

    /// <summary>
    /// Compra acionada pela interface do aeroporto. Reserva o valor pelo
    /// ledger antes do spawn e estorna pela mesma rota se a base recusar a
    /// aeronave, mantendo tesouro e notificações consistentes.
    /// </summary>
    protected bool TentarComprarAviaoComPagamento(GameObject prefabDeAeronave, long custo)
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (prefabDeAeronave == null || semEnergia || custo < 0 || recursos == null || recursos.dinheiro < custo)
        {
            return false;
        }

        IdentidadeUnidade identidadeBase = GetComponent<IdentidadeUnidade>();
        if ((identidadeBase == null || identidadeBase.teamID == 1)
            && !GovernadorGameplayRTS.PermitirProducaoDePrefab(
                prefabDeAeronave,
                TipoUnidade.Aereo,
                1,
                out string motivoGovernanca))
        {
            HUDAjudaRTS.MostrarMensagemTemporaria(motivoGovernanca, 3.2f);
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", prefabDeAeronave.name + ": " + motivoGovernanca);
            return false;
        }

        if (!recursos.TentarGastarDinheiro(custo))
        {
            return false;
        }

        bool compraAceita = false;
        try
        {
            compraAceita = ComprarAviao(prefabDeAeronave);
            return compraAceita;
        }
        finally
        {
            if (!compraAceita)
            {
                recursos.AdicionarRecursos(addDinheiro: custo);
            }
        }
    }

    /// <summary>
    /// Spawn da reserva mínima da IA usando o mesmo pátio e a mesma rotina do
    /// jogador, sem deixar o primeiro caça preso no limitador da fila.
    /// </summary>
    public void ComprarAviaoIAImediato(GameObject prefabDeAeronave, string productionOrderId = "")
    {
        TryComprarAviaoIAImediato(prefabDeAeronave, productionOrderId);
    }

    /// <summary>
    /// Tenta materializar uma aeronave da IA e informa se o aeroporto aceitou
    /// o spawn. O wrapper void legado permanece para os chamadores existentes.
    /// </summary>
    public bool TryComprarAviaoIAImediato(GameObject prefabDeAeronave, string productionOrderId = "")
    {
        return TryComprarAviaoIAImediatoRastreado(prefabDeAeronave, productionOrderId, null);
    }

    /// <summary>
    /// Tenta uma base durante uma busca com fallback. O chamador encerra a
    /// reserva somente depois de testar todas as bases disponíveis.
    /// </summary>
    public bool TryComprarAviaoIAImediato(
        GameObject prefabDeAeronave,
        string productionOrderId,
        bool liberarReservaEmFalha)
    {
        return TryComprarAviaoIAImediatoRastreado(prefabDeAeronave, productionOrderId, null, liberarReservaEmFalha);
    }

    public bool TryComprarAviaoIAImediatoRastreado(
        GameObject prefabDeAeronave,
        string productionOrderId,
        System.Action onPhysicalAcceptance)
    {
        return TryComprarAviaoIAImediatoRastreado(
            prefabDeAeronave,
            productionOrderId,
            onPhysicalAcceptance,
            true);
    }

    public bool TryComprarAviaoIAImediatoRastreado(
        GameObject prefabDeAeronave,
        string productionOrderId,
        System.Action onPhysicalAcceptance,
        bool liberarReservaEmFalha)
    {
        if (semEnergia)
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            return false;
        }

        // A IA pode encontrar fichas legadas com uma referência vazia/objeto
        // quebrado. Não encaminhe esse objeto para Instantiate: isso gerava o
        // erro de prefab sem nome e interrompia a fila militar.
        if (prefabDeAeronave == null || string.IsNullOrWhiteSpace(prefabDeAeronave.name))
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            Debug.LogWarning("[Aeroporto] Ordem da IA ignorada: prefab de aeronave invalido.", this);
            return false;
        }

        return ComprarAviaoImediato(prefabDeAeronave, productionOrderId, onPhysicalAcceptance, liberarReservaEmFalha);
    }


    private bool ComprarAviaoImediato(
        GameObject prefabDeAeronave,
        string productionOrderId = "",
        System.Action onPhysicalAcceptance = null)
    {
        return ComprarAviaoImediato(prefabDeAeronave, productionOrderId, onPhysicalAcceptance, true);
    }

    private bool ComprarAviaoImediato(
        GameObject prefabDeAeronave,
        string productionOrderId,
        System.Action onPhysicalAcceptance,
        bool liberarReservaEmFalha)
    {
        if (prefabDeAeronave == null)
        {
            return false;
        }

        if (!PodeProduzirAeronave(prefabDeAeronave))
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            return false;
        }

        // O C-700 só pode ser aceito quando a base tem um ponto físico de
        // recebimento. Sem esta validação antecipada, o spawn retornava sucesso
        // e a coroutine ocultava o avião depois, mantendo a cobrança do menu.
        if (prefabDeAeronave.GetComponentInChildren<C700TransporteAereo>(true) != null
            && ObterParadaRecebimentoTransporteAereo() == null)
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            return false;
        }

        long spawnStart = System.Diagnostics.Stopwatch.GetTimestamp();

        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefabDeAeronave.name);
        // O ponto de preparacao pertence ao aeroporto. Se um prefab/scene object
        // apontar para um transform externo (por exemplo, a prefeitura), ignora a
        // referencia e usa a raiz da propria base para nunca criar a aeronave fora dela.
        bool pontoPreparacaoDaBase = EhAeroportoMilitar()
            && wpPreparacao != null
            && wpPreparacao.name.IndexOf("criando", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool pontoPreparacaoLocal = wpPreparacao != null
            && (wpPreparacao == transform || wpPreparacao.IsChildOf(transform))
            && (pontoPreparacaoDaBase || (wpPreparacao.position - transform.position).sqrMagnitude <= 40000f);
        Vector3 posSpawn = pontoPreparacaoLocal ? wpPreparacao.position : transform.position;
        Quaternion rotacaoSpawn = pontoPreparacaoLocal ? wpPreparacao.rotation : transform.rotation;

        // Um aeroporto terrestre nunca pode materializar aviões no mar. Isso
        // acontecia quando uma referência de preparação era inválida e o
        // fallback usava uma base construída/posicionada sobre a água.
        if (!(this is GerenciadorPortaAvioes) && NavalPlacementResolver.IsWaterAtPosition(posSpawn))
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            string mensagemAgua = $"[Aeroporto] Spawn aéreo bloqueado em água: {name} ({posSpawn:F1}). Corrija o ponto Preparacao/pista da base.";
            if (liberarReservaEmFalha)
                Debug.LogError(mensagemAgua, this);
            else
                Debug.LogWarning(mensagemAgua + " A IA tentará outra base elegível.", this);
            return false;
        }

        // A rotação do ponto Preparacao (ou da própria base no fallback) é a
        // orientação da pista. Quaternion.identity deixava modelos importados
        // nascerem de lado, mesmo com a base corretamente apontada.
        GameObject aeronaveNascente = UnityEngine.Object.Instantiate((UnityEngine.Object)prefabDeAeronave, posSpawn, rotacaoSpawn) as GameObject;
        if (aeronaveNascente == null)
        {
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            Debug.LogError("[Aeroporto] Nao foi possivel instanciar o prefab de aeronave: " + prefabDeAeronave.name, this);
            return false;
        }

        IAAutoProductionRegistry.ConfirmConstructionStarted(productionOrderId, GetInstanceID(), Time.time);

        string nomePrefabNormalizado = prefabDeAeronave.name.ToLowerInvariant();
        if ((nomePrefabNormalizado.Contains("nara") || nomePrefabNormalizado.Contains("american_plane_transport"))
            && aeronaveNascente.GetComponentInChildren<AviaoBombardeiro>(true) == null)
        {
            aeronaveNascente.AddComponent<AviaoBombardeiro>();
        }

        // Mede init pós-instantiate (o custo total do spawn fica em spawn_air_ms).
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // --- SISTEMA DE IDENTIDADE (HERANÇA DO AEROPORTO) ---
        if (!_identidadeVerificada)
        {
            _identidadeCacheada = GetComponent<IdentidadeUnidade>();
            _identidadeVerificada = true;
        }

        IdentidadeUnidade idAviao = aeronaveNascente != null ? aeronaveNascente.GetComponent<IdentidadeUnidade>() : null;
        if (aeronaveNascente != null && idAviao == null) idAviao = aeronaveNascente.AddComponent<IdentidadeUnidade>();

        if (_identidadeCacheada != null && idAviao != null)
        {
            idAviao.teamID = Mathf.Max(1, _identidadeCacheada.teamID);
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = governo != null ? governo.ObterPais(idAviao.teamID) : null;
            idAviao.nomeDoPais = pais != null && !string.IsNullOrWhiteSpace(pais.nomePais)
                ? pais.nomePais
                : _identidadeCacheada.nomeDoPais;
            idAviao.tipoUnidade = TipoUnidade.Aereo;

            // Se pertencer à IA (Time 2 ou maior), empurra o avião pra mente dela
            if (idAviao.teamID > 1)
            {
                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
            }
        }
        else if (idAviao != null)
        {
            // Bases legadas do jogador podem não ter IdentidadeUnidade. A
            // validação de efetivo usa time 1 nesse caso; mantenha o mesmo
            // proprietário no avião em vez de herdar o teamID serializado no prefab.
            idAviao.teamID = 1;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = governo != null ? governo.ObterPais(idAviao.teamID) : null;
            idAviao.nomeDoPais = pais != null && !string.IsNullOrWhiteSpace(pais.nomePais)
                ? pais.nomePais
                : "Hegemonia";
            idAviao.tipoUnidade = TipoUnidade.Aereo;
        }

        int teamAeronave = idAviao != null ? idAviao.teamID : 1;
        string paisAeronave = idAviao != null ? idAviao.nomeDoPais : "Hegemonia";
        // Instantiate registra identidades antes de o aeroporto transferir
        // equipe e classificação. Aplique o proprietário também às identidades
        // internas para que sensores não tratem partes da aeronave como outro país.
        IdentidadeUnidade[] identidadesAeronave = aeronaveNascente.GetComponentsInChildren<IdentidadeUnidade>(true);
        for (int i = 0; i < identidadesAeronave.Length; i++)
        {
            IdentidadeUnidade identidade = identidadesAeronave[i];
            if (identidade == null) continue;
            identidade.teamID = teamAeronave;
            identidade.nomeDoPais = paisAeronave;
            if (identidade == idAviao) identidade.tipoUnidade = TipoUnidade.Aereo;
            IA_WorldState.NotifyEntityChanged(identidade);
        }
        RegistroEntidadesJogo.NotificarAlteracao();

        SistemaGovernoMundial governoAeronave = SistemaGovernoMundial.Instancia;
        if (governoAeronave != null && !governoAeronave.TentarMobilizarEfetivoDaUnidade(teamAeronave, aeronaveNascente))
        {
            Destroy(aeronaveNascente);
            if (liberarReservaEmFalha) IAAutoProductionRegistry.Release(productionOrderId, Time.time);
            return false;
        }

        // A aeronave já foi aceita pelo aeroporto, mesmo que o pátio esteja
        // cheio e ela precise aguardar no hangar interno.
        IAAutoProductionRegistry.Complete(productionOrderId, Time.time);
        onPhysicalAcceptance?.Invoke();

        // Publica a compra no boletim mundial. O painel STATUS consome este
        // feed sem acoplar o aeroporto a nenhuma tela especifica.
        if (_identidadeCacheada != null)
        {
            string paisComprador = string.IsNullOrWhiteSpace(_identidadeCacheada.nomeDoPais)
                ? "Uma nação"
                : _identidadeCacheada.nomeDoPais;
            string categoria = _identidadeCacheada.teamID > 1 ? "MUNDO" : "AEROPORTO";
            string titulo = _identidadeCacheada.teamID > 1 ? "Compra militar registrada" : "Nova aeronave incorporada";
            StatusNotificacaoFeed.Publicar(
                categoria,
                titulo,
                paisComprador + " comprou a aeronave " + prefabDeAeronave.name + ".",
                StatusNotificacaoSeveridade.Info);
        }

        ServicoAbastecimento.AbastecerUnidadeProduzida(aeronaveNascente);

        C700TransporteAereo c700 = aeronaveNascente != null
            ? aeronaveNascente.GetComponentInChildren<C700TransporteAereo>(true)
            : null;
        if (c700 != null)
        {
            c700.DefinirAeroportoOrigem(this);
            ControleAviao controleC700 = aeronaveNascente.GetComponentInChildren<ControleAviao>(true);
            if (controleC700 != null)
            {
                controleC700.aeroportoOrigem = this;
            }
            StartCoroutine(RotinaRecebimentoC700(c700));
            RegistrarTempoDiagnostico("prefab_init_ms", initStart);
            RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
            return true;
        }

        Helicoptero helicoptero = aeronaveNascente != null
            ? aeronaveNascente.GetComponentInChildren<Helicoptero>(true)
            : null;
        if (helicoptero != null)
        {
            StartCoroutine(RotinaRecebimentoHelicoptero(helicoptero));
            RegistrarTempoDiagnostico("prefab_init_ms", initStart);
            RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
            return true;
        }

        ControleAviao controleDaNave = aeronaveNascente != null
            ? aeronaveNascente.GetComponentInChildren<ControleAviao>(true)
            : null;
        if (aeronaveNascente != null && controleDaNave == null) controleDaNave = aeronaveNascente.AddComponent<ControleAviao>();

        if (controleDaNave != null)
        {
            controleDaNave.aeroportoOrigem = this;
            Transform vaga = ObterPrimeiraVagaLivre();
            if (vaga != null)
            {
                controleDaNave.vagaRetorno = vaga;
                if (!avioesNoPatio.Contains(controleDaNave)) avioesNoPatio.Add(controleDaNave);
            }
            StartCoroutine(RotinaRecebimento(controleDaNave));
        }

        RegistrarTempoDiagnostico("prefab_init_ms", initStart);
        RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
        return true;
    }

    public int ExecutarSortidaIA(Vector3 alvoReconhecimento, Vector3 alvoPatrulha, Vector3 alvoAtaque, int quantidadeMaxima = 5)
    {
        if (Time.time < _proximaSortidaIA)
        {
            return 0;
        }

        if (quantidadeMaxima <= 0)
        {
            quantidadeMaxima = 1;
        }

        ReporPatioComAvioesDoHangar();
        _bufferSortidaAereaIA.Clear();
        ColetarAeronavesProntas(_bufferSortidaAereaIA);
        if (_bufferSortidaAereaIA.Count == 0)
        {
            _proximaSortidaIA = Time.time + 20f;
            return 0;
        }

        const int reservaMinimaNoAeroporto = 1;
        int disponiveisParaLancar = Mathf.Max(0, _bufferSortidaAereaIA.Count - reservaMinimaNoAeroporto);
        if (disponiveisParaLancar <= 0)
        {
            _proximaSortidaIA = Time.time + 12f;
            return 0;
        }

        int limiteLote = Mathf.Min(Mathf.Max(1, quantidadeMaxima), disponiveisParaLancar);
        int lote = 1;
        if (limiteLote >= 2)
        {
            bool ataqueEmGrupo = Random.value < 0.45f;
            lote = ataqueEmGrupo ? Random.Range(2, Mathf.Min(4, limiteLote) + 1) : 1;
        }

        int lancados = 0;

        for (int i = 0; i < lote && _bufferSortidaAereaIA.Count > 0; i++)
        {
            int indice = Random.Range(0, _bufferSortidaAereaIA.Count);
            ControleAviao aviao = _bufferSortidaAereaIA[indice];
            _bufferSortidaAereaIA.RemoveAt(indice);
            if (LancarAeronaveIA(aviao, alvoReconhecimento, alvoPatrulha, alvoAtaque))
            {
                lancados++;
            }
        }

        if (lancados > 0)
        {
            _proximaSortidaIA = Time.time + (lancados > 1 ? Random.Range(24f, 36f) : Random.Range(18f, 28f));
            ReporPatioComAvioesDoHangar();
        }
        else
        {
            _proximaSortidaIA = Time.time + 10f;
        }

        return lancados;
    }

    private static void RegistrarTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }
    }

    protected virtual IEnumerator RotinaRecebimento(ControleAviao aviao)
    {
        if (aviao == null) yield break;

        Transform vagaDesignada = null;
        if (aviao.vagaRetorno != null
            && aviao.vagaRetorno != transform
            && aviao.vagaRetorno.IsChildOf(transform))
        {
            vagaDesignada = aviao.vagaRetorno;
        }
        else
        {
            vagaDesignada = ObterPrimeiraVagaLivre();
        }
        
        if (vagaDesignada == null)
        {
            // Pátio lotado: a aeronave fica na reserva interna. Não crie
            // vagas artificiais, pois elas espalhavam aviões pelo mapa.
            GuardarNoHangarAutomatico(aviao);
            yield break;
        }

        aviao.vagaRetorno = vagaDesignada;
        if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);

        // Vai devagarzinho do Hangar até a frente do Hangar
        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, wpPronto));
            if (aviao == null || !aviao.MovimentoWaypointConcluido)
            {
                if (aviao != null) GuardarNoHangarAutomatico(aviao);
                yield break;
            }
        }

        if (aviao == null) yield break;
        
        // Vai devagarzinho pra Vaga do Pátio
        yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, vagaDesignada));
        if (aviao == null || !aviao.MovimentoWaypointConcluido)
        {
            if (aviao != null) GuardarNoHangarAutomatico(aviao);
            yield break;
        }
        
        if (aviao != null)
        {
            // O recebimento termina na mesma convenção do pouso: a aeronave
            // fica presa à vaga real, com o offset de estacionamento e a
            // rotação authored. Sem isso ela parecia pronta, mas permanecia
            // solta no mundo e podia iniciar o próximo voo de um ponto errado.
            aviao.transform.SetParent(vagaDesignada, true);
            aviao.transform.localPosition = new Vector3(0f, aviao.ObterAlturaEstacionamento(), 0f);
            aviao.transform.localRotation = Quaternion.identity;
            aviao.DefinirEstado(ControleAviao.EstadoAviao.ProntoNoPatio);
            // A IA ou o jogador podem emitir uma ordem enquanto o avião ainda
            // está saindo do hangar. Libere tanto a missão manual quanto a
            // patrulha quando a vaga real estiver pronta; chamar somente a
            // rotina de patrulha deixava ordens manuais presas no pátio.
            aviao.TentarIniciarOrdemPendente();
            aviao.TentarRetomarMissaoAposReabastecimento();
        }
    }

    private IEnumerator RotinaRecebimentoHelicoptero(Helicoptero helicoptero)
    {
        if (helicoptero == null) yield break;

        Transform vagaHelicoptero = ObterVagaHelicopteroPreferencial(false);
        if (vagaHelicoptero == null)
        {
            // Sem vaga para o helicóptero, oculta ele imediatamente no hangar
            helicoptero.gameObject.SetActive(false);
            RegistrarHelicopteroControlado(helicoptero);
            yield break;
        }

        helicoptero.VincularAoAeroporto(this, vagaHelicoptero);
        helicoptero.PosicionarInstantaneamenteNaVagaAeroporto(vagaHelicoptero);

        GerenciadorPortaAvioes carrier = this as GerenciadorPortaAvioes;
        if (carrier != null)
        {
            helicoptero.FixarEmVagaMovel(vagaHelicoptero, carrier.transform);
        }

        RegistrarHelicopteroControlado(helicoptero);
    }

    public virtual void RegistrarHelicopteroControlado(Helicoptero helicoptero)
    {
        if (helicoptero == null)
        {
            return;
        }

        if (!helicopterosDoAeroporto.Contains(helicoptero))
        {
            helicopterosDoAeroporto.Add(helicoptero);
        }
    }

    /// <summary>
    /// Registra uma aeronave que chegou por compra, transferência ou carga
    /// especial. Esse caminho mantém o pátio/hangar e o retorno da aeronave
    /// sincronizados, assim como acontece com uma aeronave construída aqui.
    /// </summary>
    public virtual bool RegistrarAeronaveRecebida(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return false;
        }

        avioesNoPatio.Remove(aviao);
        avioesNoHangar.Remove(aviao);
        aviao.aeroportoOrigem = this;

        Transform vaga = ObterPrimeiraVagaLivre();
        if (vaga != null)
        {
            return ColocarAviaoInstantaneamenteNoPatio(aviao, vaga, false);
        }

        GuardarNoHangarAutomatico(aviao);
        // Algumas bases especializadas, como porta-aviões, podem estacionar
        // no pátio/convés mesmo quando não há vaga interna disponível.
        return avioesNoHangar.Contains(aviao) || avioesNoPatio.Contains(aviao);
    }

    /// <summary>Finaliza a chegada de um helicóptero no pátio ou reserva.</summary>
    public virtual bool RegistrarHelicopteroRecebido(Helicoptero helicoptero)
    {
        if (helicoptero == null)
        {
            return false;
        }

        Transform vaga = ObterVagaHelicopteroPreferencial(false);
        if (vaga == null)
        {
            RegistrarHelicopteroControlado(helicoptero);
            helicoptero.gameObject.SetActive(false);
            return true;
        }

        helicoptero.VincularAoAeroporto(this, vaga);
        helicoptero.PosicionarInstantaneamenteNaVagaAeroporto(vaga);

        GerenciadorPortaAvioes carrier = this as GerenciadorPortaAvioes;
        if (carrier != null)
        {
            helicoptero.FixarEmVagaMovel(vaga, carrier.transform);
        }

        RegistrarHelicopteroControlado(helicoptero);
        return true;
    }

    /// <summary>Resolve a vaga que o recebimento de um C-700 usará.</summary>
    public virtual Transform ObterParadaRecebimentoTransporteAereo()
    {
        Transform parada = ObterParadaGrandePreferencial(false);
        if (parada == null) parada = ObterPrimeiraVagaLivre();
        if (parada == null)
        {
            Transform fallbackOcupado = ObterParadaGrandePreferencial(true);
            // A chegada de um C-700 não pode compartilhar uma parada já
            // ocupada. Devolver null faz o chamador rejeitar a unidade e
            // executar o rollback transacional do lote.
            if (fallbackOcupado != null)
            {
                for (int i = transportesC700NoPatio.Count - 1; i >= 0; i--)
                {
                    C700TransporteAereo transporte = transportesC700NoPatio[i];
                    if (transporte == null)
                    {
                        transportesC700NoPatio.RemoveAt(i);
                        continue;
                    }

                    if (transporte.pontoEstacionamentoPreferencial == fallbackOcupado
                        || (transporte.transform.position - fallbackOcupado.position).sqrMagnitude <= 100f)
                    {
                        return null;
                    }
                }
            }
            parada = fallbackOcupado;
        }
        return parada;
    }

    /// <summary>Finaliza a chegada de um C-700 no ponto grande do pátio.</summary>
    public virtual bool RegistrarTransporteAereoRecebido(C700TransporteAereo aviao)
    {
        if (aviao == null) return false;
        aviao.DefinirAeroportoOrigem(this);
        Transform parada = ObterParadaRecebimentoTransporteAereo();
        if (parada == null)
        {
            aviao.gameObject.SetActive(false);
            return false;
        }

        if (!transportesC700NoPatio.Contains(aviao))
        {
            transportesC700NoPatio.Add(aviao);
        }
        aviao.RegistrarPontoEstacionamento(parada);
        aviao.FinalizarPosicionamentoNoPatio(parada);
        return true;
    }

    private IEnumerator RotinaRecebimentoC700(C700TransporteAereo aviao)
    {
        if (aviao == null) yield break;

        Transform paradaGrande = ObterParadaRecebimentoTransporteAereo();

        if (paradaGrande == null)
        {
            // Sem vaga para C700, manda para a reserva (invisível)
            aviao.gameObject.SetActive(false);
            yield break;
        }

        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.TaxiarAteTransform(wpPronto));
        }

        if (aviao == null) yield break;

        aviao.RegistrarPontoEstacionamento(paradaGrande);
        if (!transportesC700NoPatio.Contains(aviao)) transportesC700NoPatio.Add(aviao);
        yield return StartCoroutine(aviao.TaxiarAteTransform(paradaGrande));
        aviao.FinalizarPosicionamentoNoPatio(paradaGrande);
    }

    public IEnumerator RotinaLancarMissaoEmMassa(Vector3 alvo, int quantidade)
    {
        int lancados = 0;
        quantidade = Mathf.Max(1, quantidade);
        
        while (lancados < quantidade)
        {
            ControleAviao proximo = avioesNoPatio.Find(a => a != null && a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio && a.GetComponent<KamikazeDrone>() != null);
            
            if (proximo == null)
            {
                proximo = avioesNoHangar.Find(a => a != null && a.GetComponent<KamikazeDrone>() != null);
                if (proximo != null)
                {
                    PrepararAviaoReservaParaLancamento(proximo);
                }
            }

            if (proximo == null)
            {
                Debug.LogWarning("[Ataque Massa] Sem kamikazes suficientes na reserva ou pátio!");
                break;
            }

            ControleUnidade controle = proximo.GetComponent<ControleUnidade>();
            bool ordemAceita = controle != null
                ? controle.EmitirOrdemMover(alvo, true)
                : proximo.ReceberOrdemManual(alvo);
            if (!ordemAceita)
            {
                Debug.LogWarning($"[Ataque Massa] A aeronave {proximo.name} recusou a missão.");
                break;
            }
            lancados++;
            
            if (lancados < quantidade) yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator RotinaLancarPatrulhaMesmoModelo(Vector3 alvo, string modelo, int quantidade)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            yield break;
        }

        int lancados = 0;
        quantidade = Mathf.Max(1, quantidade);
        List<ControleAviao> ignorados = null;

        while (lancados < quantidade)
        {
            ControleAviao proximo = avioesNoPatio.Find(a =>
                a != null &&
                a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio &&
                EhMesmoModelo(a, modelo) &&
                (ignorados == null || !ignorados.Contains(a)));

            if (proximo == null)
            {
                proximo = avioesNoHangar.Find(a =>
                    a != null &&
                    EhMesmoModelo(a, modelo) &&
                    (ignorados == null || !ignorados.Contains(a)));
                if (proximo != null)
                {
                    PrepararAviaoReservaParaLancamento(proximo);
                }
            }

            if (proximo == null)
            {
                Debug.LogWarning($"[Patrulha Grupo] Sem aeronaves suficientes do modelo {modelo} no pátio ou hangar.");
                yield break;
            }

            ReabastecerAeronave(proximo, 10f);
            ConfigurarAviaoParaPatrulhaEmGrupo(proximo);
            ControleUnidade controle = proximo.GetComponent<ControleUnidade>();
            if (controle == null || !controle.EmitirOrdemPatrulha(new List<Vector3> { alvo }))
            {
                Debug.LogWarning($"[Patrulha Grupo] A aeronave {proximo.name} recusou a rota.");
                if (ignorados == null) ignorados = new List<ControleAviao>(4);
                ignorados.Add(proximo);
                continue;
            }
            lancados++;

            if (lancados < quantidade)
            {
                yield return new WaitForSeconds(5f);
            }
        }
    }

    public virtual Transform ObterPrimeiraVagaLivre()
    {
        if (waypointsPatio == null || waypointsPatio.Count == 0) return null;

        _vagasOcupadas.Clear();
        _posicoesVagaOcupadas.Clear();
        for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
        {
            ControleAviao av = avioesNoPatio[i];
            if (av == null) { avioesNoPatio.RemoveAt(i); continue; }
            bool ocupandoVaga = av.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio
                || av.estadoAtual == ControleAviao.EstadoAviao.Taxiando
                || av.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga;
            if (ocupandoVaga && av.vagaRetorno != null)
            {
                _vagasOcupadas.Add(av.vagaRetorno);
                _posicoesVagaOcupadas.Add(av.vagaRetorno.position);
            }
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            Transform vagaHeli = heli.ObterVagaAeroporto();
            string estadoHeli = heli.ObterEstadoOperacionalAeroporto();
            bool chegandoParaPouso = heli.estaVoando
                && heli.EstaSobControleDoAeroporto()
                && (estadoHeli.IndexOf("aproxim", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || estadoHeli.IndexOf("pousando", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (vagaHeli != null && (heli.EstaEstacionadoNoAeroporto() || chegandoParaPouso))
            {
                _vagasOcupadas.Add(vagaHeli);
                _posicoesVagaOcupadas.Add(vagaHeli.position);
            }
        }

        // O convés V2 e o menu legado podem apontar para transforms distintos
        // que representam a mesma vaga física. Compartilhe a reserva por
        // posição para que helicópteros do fluxo legado não ocupem uma vaga
        // que o V2 já reservou para um avião em aproximação.
        GerenciadorPortaAvioes carrier = this as GerenciadorPortaAvioes;
        GerenciadorOperacoesPortaAvioesV2 operacoesV2 = carrier != null
            ? carrier.GetComponentInChildren<GerenciadorOperacoesPortaAvioesV2>(true)
            : null;
        if (operacoesV2 != null && operacoesV2.usarSistemaOperacoesV2 && operacoesV2.layout != null
            && operacoesV2.layout.vagasConves != null)
        {
            for (int i = 0; i < operacoesV2.layout.vagasConves.Count; i++)
            {
                VagaPortaAvioesV2 vagaV2 = operacoesV2.layout.vagasConves[i];
                if (vagaV2 != null && vagaV2.estado != EstadoVagaPortaAvioesV2.Livre)
                    _posicoesVagaOcupadas.Add(vagaV2.transform.position);
            }
        }

        for (int i = 0, count = waypointsPatio.Count; i < count; i++)
        {
            Transform wp = waypointsPatio[i];
            if (wp == null || _vagasOcupadas.Contains(wp) || EstaVagaOcupadaPorPosicao(wp.position)) continue;

            bool ocupadoPorC700 = false;
            for (int c = transportesC700NoPatio.Count - 1; c >= 0; c--)
            {
                C700TransporteAereo transporte = transportesC700NoPatio[c];
                if (transporte == null)
                {
                    transportesC700NoPatio.RemoveAt(c);
                    continue;
                }

                if (transporte.pontoEstacionamentoPreferencial == wp
                    || (transporte.transform.position - wp.position).sqrMagnitude <= 100f)
                {
                    ocupadoPorC700 = true;
                    break;
                }
            }

            if (ocupadoPorC700) continue;

            Collider[] hits = Physics.OverlapSphere(wp.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h] == null) continue;

                C700TransporteAereo transporte = hits[h].GetComponentInParent<C700TransporteAereo>();
                if (transporte != null)
                {
                    ocupadoPorC700 = true;
                    break;
                }
            }

            if (!ocupadoPorC700) return wp;
        }
        return null;
    }

    private bool EstaVagaOcupadaPorPosicao(Vector3 posicao)
    {
        const float distanciaMinima = 10f;
        float distanciaSqr = distanciaMinima * distanciaMinima;
        for (int i = 0; i < _posicoesVagaOcupadas.Count; i++)
        {
            if ((_posicoesVagaOcupadas[i] - posicao).sqrMagnitude < distanciaSqr) return true;
        }
        return false;
    }

    public Transform ObterVagaHelicopteroPreferencial(bool aceitarOcupada = false)
    {
        Transform vagaEncontrada = ProcurarVagaHelicopteroEmRaiz(patio != null ? patio : transform, aceitarOcupada);
        if (vagaEncontrada == null && patio != transform)
        {
            vagaEncontrada = ProcurarVagaHelicopteroEmRaiz(transform, aceitarOcupada);
        }

        // Algumas bases militares importadas possuem pista e hangar, mas não
        // trazem os marcadores H/I/J/K/L/Q usados pelo aeroporto convencional.
        // Sem uma vaga real, a compra do helicóptero era ocultada e depois
        // removida da lista por não ter origem registrada. Cria uma única vaga
        // reserva visível apenas nesse caso; aeroportos que já têm vagas
        // continuam obedecendo a capacidade configurada na cena.
        if (vagaEncontrada == null && !aceitarOcupada && !ExisteVagaHelicopteroConfigurada())
        {
            vagaEncontrada = ObterOuCriarVagaHelicopteroReserva();
        }

        return vagaEncontrada;
    }

    private bool ExisteVagaHelicopteroConfigurada()
    {
        Transform raizBusca = patio != null ? patio : transform;
        if (ExisteVagaHelicopteroNaRaiz(raizBusca)) return true;
        return patio != transform && ExisteVagaHelicopteroNaRaiz(transform);
    }

    private static bool ExisteVagaHelicopteroNaRaiz(Transform raizBusca)
    {
        if (raizBusca == null) return false;

        Transform[] filhos = raizBusca.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform candidato = filhos[i];
            if (candidato != null && NomeEhVagaHelicopteroMilitar(candidato.name.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private Transform ObterOuCriarVagaHelicopteroReserva()
    {
        if (_vagaHelicopteroReserva != null)
        {
            return _vagaHelicopteroReserva;
        }

        Transform raiz = patio != null ? patio : transform;
        GameObject marcador = new GameObject("Vaga_H_Reserva");
        marcador.transform.SetParent(transform, true);

        Vector3 posicao = raiz != null ? raiz.position : transform.position;
        marcador.transform.position = posicao + Vector3.up * 3.5f;
        marcador.transform.rotation = transform.rotation;
        marcador.transform.localScale = Vector3.one;

        _vagaHelicopteroReserva = marcador.transform;
        return _vagaHelicopteroReserva;
    }

    protected bool HelicopteroPertenceAEstaBase(Helicoptero heli)
    {
        if (heli == null)
        {
            return false;
        }

        Transform vaga = heli.ObterVagaAeroporto();
        return vaga != null && (vaga == transform || vaga.IsChildOf(transform));
    }

    protected void LimparHelicopterosTransferidos()
    {
        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (HelicopteroPertenceAEstaBase(heli))
            {
                continue;
            }

            if (helicopteroSelecionadoParaMissao == heli)
            {
                helicopteroSelecionadoParaMissao = null;
            }

            helicopterosDoAeroporto.RemoveAt(i);
        }
    }

    private Transform ProcurarVagaHelicopteroEmRaiz(Transform raizBusca, bool aceitarOcupada)
    {
        if (raizBusca == null)
        {
            return null;
        }

        Transform[] filhos = raizBusca.GetComponentsInChildren<Transform>(true);
        List<Transform> vagasPatioMilitar = new List<Transform>();
        List<Transform> vagasGerais = new List<Transform>();

        for (int i = 0; i < filhos.Length; i++)
        {
            Transform candidato = filhos[i];
            if (candidato == null) continue;

            string nome = candidato.name.ToLowerInvariant();
            if (!NomeEhVagaHelicopteroMilitar(nome)) continue;

            Transform pai = candidato.parent;
            if (pai != null && pai.name.ToLowerInvariant().Contains("patio_militar"))
            {
                vagasPatioMilitar.Add(candidato);
            }
            else
            {
                vagasGerais.Add(candidato);
            }
        }

        vagasPatioMilitar.Sort(CompararVagasHelicopteroMilitar);
        vagasGerais.Sort(CompararVagasHelicopteroMilitar);

        Transform vagaLivrePatio = EncontrarPrimeiraVagaHelicoptero(vagasPatioMilitar, aceitarOcupada);
        if (vagaLivrePatio != null)
        {
            return vagaLivrePatio;
        }

        return EncontrarPrimeiraVagaHelicoptero(vagasGerais, aceitarOcupada);
    }

    private static bool NomeEhVagaHelicopteroMilitar(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return false;
        }

        return nome == "h" || nome == "i" || nome == "j" || nome == "k" || nome == "l" || nome == "q"
            || nome == "vaga_h" || nome == "vaga_i" || nome == "vaga_j" || nome == "vaga_k" || nome == "vaga_l" || nome == "vaga_q"
            || nome.StartsWith("vaga_h_") || nome.StartsWith("vaga_i_") || nome.StartsWith("vaga_j_")
            || nome.StartsWith("vaga_k_") || nome.StartsWith("vaga_l_") || nome.StartsWith("vaga_q_");
    }

    private Transform EncontrarPrimeiraVagaHelicoptero(List<Transform> vagas, bool aceitarOcupada)
    {
        if (vagas == null || vagas.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < vagas.Count; i++)
        {
            Transform vaga = vagas[i];
            if (vaga == null)
            {
                continue;
            }

            if (aceitarOcupada || !VagaHelicopteroOcupada(vaga))
            {
                return vaga;
            }
        }

        return null;
    }

    private static int CompararVagasHelicopteroMilitar(Transform a, Transform b)
    {
        int ordemA = OrdemVagaHelicopteroMilitar(a != null ? a.name : string.Empty);
        int ordemB = OrdemVagaHelicopteroMilitar(b != null ? b.name : string.Empty);
        if (ordemA != ordemB)
        {
            return ordemA.CompareTo(ordemB);
        }

        string nomeA = a != null ? a.name : string.Empty;
        string nomeB = b != null ? b.name : string.Empty;
        return string.Compare(nomeA, nomeB, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int OrdemVagaHelicopteroMilitar(string nomeOriginal)
    {
        string nome = string.IsNullOrEmpty(nomeOriginal) ? string.Empty : nomeOriginal.ToLowerInvariant();
        switch (nome)
        {
            case "h":
            case "vaga_h":
                return 0;
            case "i":
            case "vaga_i":
                return 1;
            case "j":
            case "vaga_j":
                return 2;
            case "k":
            case "vaga_k":
                return 3;
            case "l":
            case "vaga_l":
                return 4;
            case "q":
            case "vaga_q":
                return 5;
            default:
                if (nome.StartsWith("vaga_h_")) return 0;
                if (nome.StartsWith("vaga_i_")) return 1;
                if (nome.StartsWith("vaga_j_")) return 2;
                if (nome.StartsWith("vaga_k_")) return 3;
                if (nome.StartsWith("vaga_l_")) return 4;
                if (nome.StartsWith("vaga_q_")) return 5;
                return 99;
        }
    }

    private bool VagaHelicopteroOcupada(Transform vaga)
    {
        if (vaga == null)
        {
            return false;
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (!heli.EstaEstacionadoNoAeroporto())
            {
                continue;
            }

            if (heli.ObterVagaAeroporto() == vaga)
            {
                return true;
            }

            Vector3 diff = heli.transform.position - vaga.position;
            diff.y = 0f;
            if (diff.sqrMagnitude <= 36f)
            {
                return true;
            }
        }

        return false;
    }

    private void RegistrarWaypointsPatio(Transform grupo)
    {
        if (grupo == null)
        {
            return;
        }

        foreach (Transform filho in grupo)
        {
            if (filho == null)
            {
                continue;
            }

            string nome = filho.name.ToLowerInvariant();
            if (!NomeEhVagaAviaoExtra(nome))
            {
                continue;
            }

            if (!waypointsPatio.Contains(filho))
            {
                waypointsPatio.Add(filho);
            }
        }
    }

    private Transform EncontrarGrupoPatioMilitar()
    {
        Transform[] todos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform candidato = todos[i];
            if (candidato == null)
            {
                continue;
            }

            string nome = candidato.name.ToLowerInvariant().Replace(" ", string.Empty);
            if (nome.Contains("patio_militar") || nome.Contains("patiomilitar"))
            {
                return candidato;
            }
        }

        return null;
    }

    private static bool NomeEhVagaAviaoExtra(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return false;
        }

        if (nome.Contains("parada") || nome.Contains("descanso") || nome.Contains("vaga grande") || nome.Contains("vaga_grande"))
        {
            return true;
        }

        return nome == "h" || nome == "i" || nome == "j" || nome == "k" || nome == "l" || nome == "q"
            || nome == "vaga_h" || nome == "vaga_i" || nome == "vaga_j" || nome == "vaga_k" || nome == "vaga_l" || nome == "vaga_q"
            || nome.StartsWith("vaga_h_") || nome.StartsWith("vaga_i_") || nome.StartsWith("vaga_j_")
            || nome.StartsWith("vaga_k_") || nome.StartsWith("vaga_l_") || nome.StartsWith("vaga_q_");
    }

    public Transform ObterParadaGrandePreferencial(bool aceitarOcupada = false)
    {
        Transform primeiraEncontrada = null;
        if (waypointsPatio != null)
        {
            for (int i = 0; i < waypointsPatio.Count; i++)
            {
                Transform wp = waypointsPatio[i];
                if (!NomeEhVagaGrande(wp)) continue;
                if (primeiraEncontrada == null) primeiraEncontrada = wp;
                if (aceitarOcupada || !VagaGrandeOcupada(wp))
                {
                    return wp;
                }
            }
        }

        if (primeiraEncontrada == null)
        {
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < filhos.Length; i++)
            {
                Transform vaga = filhos[i];
                if (!NomeEhVagaGrande(vaga)) continue;
                if (primeiraEncontrada == null) primeiraEncontrada = vaga;
                if (aceitarOcupada || !VagaGrandeOcupada(vaga))
                {
                    return vaga;
                }
            }
        }

        if (aceitarOcupada)
        {
            return primeiraEncontrada;
        }

        return null;
    }

    private static bool NomeEhVagaGrande(Transform vaga)
    {
        if (vaga == null) return false;
        string nome = vaga.name.ToLowerInvariant();
        return nome.Contains("parada_grande")
            || nome.Contains("vaga grande")
            || nome.Contains("vaga_grande");
    }

    private bool VagaGrandeOcupada(Transform vaga)
    {
        if (vaga == null) return true;

        for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
        {
            ControleAviao aviao = avioesNoPatio[i];
            if (aviao == null)
            {
                avioesNoPatio.RemoveAt(i);
                continue;
            }

            bool ocupaVaga = aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio
                || aviao.estadoAtual == ControleAviao.EstadoAviao.Taxiando
                || aviao.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga;
            if (ocupaVaga && aviao.vagaRetorno == vaga) return true;
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero helicoptero = helicopterosDoAeroporto[i];
            if (helicoptero == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (helicoptero.ObterVagaAeroporto() == vaga
                && (helicoptero.EstaEstacionadoNoAeroporto() || !helicoptero.estaVoando)) return true;
        }

        // Transportes C-700 ocupam uma parada grande mesmo quando o prefab
        // não possui um Collider utilizável. A lista é atualizada antes do
        // posicionamento final, então ela também fecha a janela em que duas
        // chegadas do mesmo lote tentariam reservar a mesma vaga no mesmo
        // frame.
        for (int i = transportesC700NoPatio.Count - 1; i >= 0; i--)
        {
            C700TransporteAereo transporte = transportesC700NoPatio[i];
            if (transporte == null)
            {
                transportesC700NoPatio.RemoveAt(i);
                continue;
            }

            if (transporte.pontoEstacionamentoPreferencial == vaga
                || (transporte.transform.position - vaga.position).sqrMagnitude <= 100f)
            {
                return true;
            }
        }

        Collider[] hits = Physics.OverlapSphere(vaga.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            if (hits[i].GetComponentInParent<C700TransporteAereo>() != null) return true;

            ControleAviao aviao = hits[i].GetComponentInParent<ControleAviao>();
            if (aviao != null && (aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio
                || aviao.estadoAtual == ControleAviao.EstadoAviao.Taxiando
                || aviao.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga)) return true;

            Helicoptero helicoptero = hits[i].GetComponentInParent<Helicoptero>();
            if (helicoptero != null && helicoptero.EstaEstacionadoNoAeroporto()) return true;
        }

        return false;
    }

    // --- HELPER: Extrai texto formatado de um avião para OnGUI (evita código repetido) ---
    private string ObterInfoAviao(Component aviao, out string corCristal, out string vidaStr)
    {
        corCristal = "white";
        vidaStr = "";
        
        if (aviao == null) { return ""; }

        ControleAviaoCaca cacaScript = aviao.GetComponent<ControleAviaoCaca>();
        if (cacaScript != null) corCristal = "#" + ColorUtility.ToHtmlStringRGB(cacaScript.corIdentificacao);
        
        string nomeLimpo = aviao.gameObject.name.Replace("(Clone)", "").Trim();

        SistemaDeDanos danos = aviao.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.vidaMaxima > 0)
        {
            int pct = Mathf.RoundToInt((danos.vidaAtual / danos.vidaMaxima) * 100f);
            string corVid = (pct > 50) ? "white" : (pct > 25 ? "yellow" : "red");
            vidaStr = $" (<color={corVid}>{pct}%</color>)";
        }

        CombustivelUnidade combustivel = CombustivelUnidade.ObterParaObjeto(aviao.gameObject);
        if (combustivel != null && combustivel.usaCombustivel)
        {
            int pctComb = Mathf.RoundToInt(combustivel.Percentual * 100f);
            string corComb = pctComb > 50 ? "cyan" : (pctComb > 25 ? "yellow" : "red");
            vidaStr += $" <color={corComb}>Comb {pctComb}%</color>";
        }

        return nomeLimpo;
    }

    private string ObterChaveModeloAviao(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return string.Empty;
        }

        return aviao.gameObject.name.Replace("(Clone)", "").Trim();
    }

    private bool EhMesmoModelo(ControleAviao aviao, string modelo)
    {
        return aviao != null && !string.IsNullOrEmpty(modelo) && ObterChaveModeloAviao(aviao) == modelo;
    }

    private int ContarAvioesDisponiveisMesmoModelo(ControleAviao aviaoBase)
    {
        return ContarAvioesDisponiveisMesmoModelo(ObterChaveModeloAviao(aviaoBase));
    }

    private int ContarAvioesDisponiveisMesmoModelo(string modelo)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            return 0;
        }

        int total = 0;

        for (int i = 0; i < avioesNoPatio.Count; i++)
        {
            ControleAviao aviao = avioesNoPatio[i];
            if (aviao == null || aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                continue;
            }

            if (EhMesmoModelo(aviao, modelo))
            {
                total++;
            }
        }

        for (int i = 0; i < avioesNoHangar.Count; i++)
        {
            ControleAviao aviao = avioesNoHangar[i];
            if (EhMesmoModelo(aviao, modelo))
            {
                total++;
            }
        }

        return total;
    }

    private void PrepararAviaoReservaParaLancamento(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return;
        }

        avioesNoHangar.Remove(aviao);
        if (!avioesNoPatio.Contains(aviao))
        {
            avioesNoPatio.Add(aviao);
        }

        aviao.gameObject.SetActive(true);

        if (wpPronto != null)
        {
            aviao.transform.position = wpPronto.position;
            aviao.transform.rotation = wpPronto.rotation;
        }
        else
        {
            aviao.transform.position = transform.position;
            aviao.transform.rotation = transform.rotation;
        }

        // Reserve uma vaga real para o retorno mesmo quando a aeronave veio
        // do hangar. Limpar essa referência fazia a sequência capturar o pai
        // raiz do aeroporto como se ele fosse uma vaga.
        Transform vagaReservada = ObterPrimeiraVagaLivre();
        aviao.vagaRetorno = vagaReservada;
        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        aviao.AplicarEstadoSistemasAeronave(false, false);
        aviao.transform.SetParent(transform, true);
        if (vagaReservada != null && !avioesNoPatio.Contains(aviao))
        {
            avioesNoPatio.Add(aviao);
        }

        // A ordem pode ter sido emitida enquanto a aeronave ainda estava no
        // hangar. Este é um caminho de preparação direta (não passa por
        // DefinirEstado), então libere explicitamente a ordem agora que ela
        // está pronta no pátio.
        aviao.TentarIniciarOrdemPendente();
        aviao.TentarRetomarMissaoAposReabastecimento();
    }

    private void ConfigurarAviaoParaPatrulhaEmGrupo(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return;
        }

        LancadorMisselCaca missilScript = aviao.GetComponent<LancadorMisselCaca>();
        if (missilScript != null)
        {
            missilScript.modoPassivo = false;
        }

        AviaoBombardeiro bombardeiro = aviao.GetComponent<AviaoBombardeiro>();
        if (bombardeiro != null)
        {
            bombardeiro.modoDeAtaque = AviaoBombardeiro.ModoAtaque.Patrulha;
        }
    }

    private void PrepararPatrulhaEmGrupo()
    {
        if (aviaoSelecionadoParaMissao == null)
        {
            return;
        }

        LimparModoMassaAereo();
        _modeloPatrulhaGrupo = ObterChaveModeloAviao(aviaoSelecionadoParaMissao);
        esperandoCliquePatrulhaGrupo = !string.IsNullOrEmpty(_modeloPatrulhaGrupo);
        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = esperandoCliquePatrulhaGrupo;
        _usarMarcadorPatrulhaAviaoNoProximoClique = esperandoCliquePatrulhaGrupo;
    }

    private void LimparModoMassaAereo()
    {
        esperandoCliqueMassa = false;
        esperandoCliquePatrulhaGrupo = false;
        _modeloPatrulhaGrupo = string.Empty;
        _usarMarcadorPatrulhaAviaoNoProximoClique = false;
    }

#if UNITY_EDITOR
    private void GarantirPrefabsMarcadoresNoEditor()
    {
        if (prefabMarcadorPatrulhaAviao == null)
        {
            prefabMarcadorPatrulhaAviao = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/animacao click/Map track markers VFX/Prefabs/Marker 5 Circle Loop.prefab");
        }

        if (prefabMarcadorBombardeiro == null)
        {
            prefabMarcadorBombardeiro = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/animacao click/Map track markers VFX/Prefabs/Marker 7 Danger zone Loop.prefab");
        }
    }
#endif

    void OnMouseEnter()
    {
        mouseHover = true;
    }

    void OnMouseExit()
    {
        mouseHover = false;
    }

    protected virtual void OnGUI()
    {
        if (Construtor.EmModoConstrucaoAtivo) return;

        if (!IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto
            && exibirTooltipStatus && mouseHover && !(this is GerenciadorPortaAvioes))
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (_texturaTooltip == null)
            {
                _texturaTooltip = new Texture2D(1, 1);
                _texturaTooltip.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.13f, 0.95f));
                _texturaTooltip.Apply();
            }
            boxStyle.normal.background = _texturaTooltip;
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
            boxStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.richText = true;
            textStyle.fontSize = 13;
            textStyle.normal.textColor = Color.white;

            float baseConsumo = 15.0f;
            int totalAvioes = avioesNoPatio.Count + avioesNoHangar.Count;
            int totalHelis = helicopterosDoAeroporto.Count;
            int totalHeavy = transportesC700NoPatio.Count;
            float consumo = baseConsumo + (totalAvioes * 2.0f) + (totalHelis * 1.5f) + (totalHeavy * 5.0f);
            
            string statusEnergia = semEnergia ? "<color=#ff5555>⚡ APAGÃO (SEM ENERGIA)</color>" : "<color=#55ff55>⚡ OPERACIONAL</color>";
            string avisoBlackout = semEnergia
                ? "\n<color=orange>⚠️ " + LocalizationManager.T("airport.no_power_purchase", "AEROPORTO SEM ENERGIA\nConstrua uma usina para reativá-lo.") + "</color>"
                : "";

            string content = $"<b>✈️ AEROPORTO MILITAR ({name.Replace("(Clone)", "")})</b>\n\n" +
                             $"🛸 Frota no Pátio: <b>{avioesNoPatio.Count + transportesC700NoPatio.Count}</b>\n" +
                             $"🔒 Frota no Hangar: <b>{avioesNoHangar.Count}</b>\n" +
                             $"🚁 Helicópteros: <b>{helicopterosDoAeroporto.Count}</b>\n" +
                             $"⚡ Consumo: <b>{consumo:F2} MW</b>\n" +
                             $"🔌 Conectividade: {statusEnergia}{avisoBlackout}";

            Vector2 size = textStyle.CalcSize(new GUIContent(content));
            float width = size.x + 20f;
            float height = size.y + 20f;

            Vector2 mousePos = Input.mousePosition;
            Rect rect = new Rect(mousePos.x + 15f, Screen.height - mousePos.y + 15f, width, height);

            if (rect.xMax > Screen.width) rect.x = mousePos.x - width - 15f;
            if (rect.yMax > Screen.height) rect.y = Screen.height - mousePos.y - height - 15f;

            GUI.Box(rect, "", boxStyle);
            GUI.Label(new Rect(rect.x + 10, rect.y + 10, size.x, size.y), content, textStyle);
        }

        if (!menuAtivo) return;
        if (!GestorMenusExclusivos.EstaAtivo(this))
        {
            menuAtivo = false;
            return;
        }
        if (menuAeroportoUI != null && menuAeroportoUI.activeInHierarchy) return;

        // --- SISTEMA DE FAXINA (Fix para os fantasmas no pátio) ---
        RemoveNulls(avioesNoPatio);
        RemoveNulls(avioesNoHangar);
        RemoveNulls(transportesC700NoPatio);
        RemoveNulls(helicopterosDoAeroporto);

        GarantirEstilosMenuTatico();

        bool guiHabilitadoOriginal = GUI.enabled;
        Rect telaDeMenu = CalcularRetanguloMenuSeguro();
        GestorMenusExclusivos.RegistrarAreaBloqueio(this, telaDeMenu);

        DesenharMolduraMenuAeroporto(telaDeMenu);

        // --- BOTÃO DE FECHAR (X) ---
        if (GUI.Button(new Rect(telaDeMenu.xMax - 40f, telaDeMenu.y + 10f, 30f, 26f), "✕", _stAeroBotaoFechar))
        {
            FecharPainelAeroporto();
            GUI.enabled = guiHabilitadoOriginal;
            return;
        }

        Rect areaConteudo = new Rect(
            telaDeMenu.x + 12f,
            telaDeMenu.y + 54f,
            telaDeMenu.width - 24f,
            Mathf.Max(140f, telaDeMenu.height - 64f));

        GUILayout.BeginArea(areaConteudo);
        DesenharAbaMilitar(areaConteudo.width, areaConteudo.height);
        GUILayout.EndArea();

        GUI.enabled = guiHabilitadoOriginal;
    }

    private Rect CalcularRetanguloMenuSeguro()
    {
        float topoHud = MenuFixadoController.ObterMargemSuperiorHudNaTela(82f);
        float baseHud = MenuComandoController.ObterMargemInferiorHudGlobalNaTela(180f);

        float topSafe = Mathf.Max(8f, topoHud + 10f);
        float bottomSafe = Mathf.Max(12f, baseHud + 14f);
        float alturaDisponivel = Screen.height - topSafe - bottomSafe;

        if (alturaDisponivel < 260f)
        {
            topSafe = Mathf.Min(topSafe, Mathf.Max(6f, topoHud + 4f));
            bottomSafe = Mathf.Min(bottomSafe, Mathf.Max(10f, baseHud + 8f));
            alturaDisponivel = Mathf.Max(210f, Screen.height - topSafe - bottomSafe);
        }

        float margemLateral = Screen.width < 1000f ? 12f : 22f;
        float larguraMaxima = Mathf.Max(340f, Screen.width - margemLateral * 2f);
        float larguraIdeal = Screen.width >= 1200f
            ? Mathf.Clamp(Screen.width * 0.62f, 840f, 1020f)
            : Mathf.Clamp(Screen.width * 0.80f, 640f, 900f);
        float widthMenu = Mathf.Min(larguraIdeal, larguraMaxima);
        float heightMenu = Mathf.Min(alturaDisponivel, Mathf.Max(460f, Screen.height * 0.72f));

        return new Rect(margemLateral, topSafe, widthMenu, heightMenu);
    }

    private void FecharPainelAeroporto()
    {
        menuAtivo = false;
        GestorMenusExclusivos.Fechar(this);
        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
    }

    private static Texture2D CriarTexturaUiAeroporto(Color cor)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixel(0, 0, cor);
        tex.Apply();
        return tex;
    }

    private void GarantirEstilosMenuTatico()
    {
        if (_uiAeroportoEstilosProntos && _uiAeroportoSkinCache == GUI.skin && _texAeroPainelFundo != null)
        {
            return;
        }

        DestruirTexturasTemaAeroporto();
        _uiAeroportoSkinCache = GUI.skin;

        _texAeroPainelFundo = CriarTexturaUiAeroporto(new Color(0.027f, 0.066f, 0.105f, 0.96f));
        _texAeroBorda = CriarTexturaUiAeroporto(new Color(0.243f, 0.486f, 0.596f, 0.92f));
        _texAeroCabecalho = CriarTexturaUiAeroporto(new Color(0.043f, 0.105f, 0.165f, 0.98f));
        _texAeroDestaqueCiano = CriarTexturaUiAeroporto(new Color(0.0f, 0.898f, 1.0f, 0.95f));
        _texAeroCardFundo = CriarTexturaUiAeroporto(new Color(0.043f, 0.098f, 0.145f, 0.95f));
        _texAeroBotaoNormal = CriarTexturaUiAeroporto(new Color(0.055f, 0.145f, 0.200f, 0.98f));
        _texAeroBotaoHover = CriarTexturaUiAeroporto(new Color(0.086f, 0.259f, 0.349f, 0.98f));
        _texAeroBotaoAtivo = CriarTexturaUiAeroporto(new Color(0.039f, 0.114f, 0.161f, 0.98f));
        _texAeroBotaoSelecionado = CriarTexturaUiAeroporto(new Color(0.082f, 0.278f, 0.388f, 0.99f));
        _texAeroBotaoPrimario = CriarTexturaUiAeroporto(new Color(0.055f, 0.302f, 0.392f, 0.98f));
        _texAeroBotaoPrimarioHover = CriarTexturaUiAeroporto(new Color(0.078f, 0.427f, 0.553f, 0.99f));
        _texAeroBotaoPerigo = CriarTexturaUiAeroporto(new Color(0.329f, 0.102f, 0.125f, 0.98f));
        _texAeroBotaoPerigoHover = CriarTexturaUiAeroporto(new Color(0.455f, 0.141f, 0.173f, 0.99f));
        _texAeroBotaoAlerta = CriarTexturaUiAeroporto(new Color(0.361f, 0.227f, 0.071f, 0.98f));
        _texAeroAlertaFundo = CriarTexturaUiAeroporto(new Color(0.240f, 0.145f, 0.045f, 0.92f));

        Color textoPrincipal = new Color(0.89f, 0.95f, 0.98f, 1f);
        Color textoSecundario = new Color(0.56f, 0.71f, 0.79f, 1f);

        _stAeroCard = new GUIStyle(GUI.skin.box);
        _stAeroCard.normal.background = _texAeroCardFundo;
        _stAeroCard.normal.textColor = textoPrincipal;
        _stAeroCard.richText = true;
        _stAeroCard.padding = new RectOffset(10, 10, 8, 8);
        _stAeroCard.margin = new RectOffset(0, 0, 2, 4);

        _stAeroAlertaCard = new GUIStyle(_stAeroCard);
        _stAeroAlertaCard.normal.background = _texAeroAlertaFundo;
        _stAeroAlertaCard.padding = new RectOffset(10, 10, 6, 6);

        _stAeroTituloHeader = new GUIStyle(GUI.skin.label);
        _stAeroTituloHeader.richText = true;
        _stAeroTituloHeader.fontSize = 15;
        _stAeroTituloHeader.fontStyle = FontStyle.Bold;
        _stAeroTituloHeader.alignment = TextAnchor.MiddleLeft;
        _stAeroTituloHeader.normal.textColor = Color.white;

        _stAeroSubtituloHeader = new GUIStyle(GUI.skin.label);
        _stAeroSubtituloHeader.richText = true;
        _stAeroSubtituloHeader.fontSize = 11;
        _stAeroSubtituloHeader.alignment = TextAnchor.MiddleLeft;
        _stAeroSubtituloHeader.normal.textColor = textoSecundario;

        _stAeroBadge = new GUIStyle(GUI.skin.label);
        _stAeroBadge.richText = true;
        _stAeroBadge.fontSize = 11;
        _stAeroBadge.alignment = TextAnchor.MiddleRight;
        _stAeroBadge.normal.textColor = textoPrincipal;

        _stAeroSecaoTitulo = new GUIStyle(GUI.skin.label);
        _stAeroSecaoTitulo.richText = true;
        _stAeroSecaoTitulo.fontSize = 12;
        _stAeroSecaoTitulo.fontStyle = FontStyle.Bold;
        _stAeroSecaoTitulo.normal.textColor = new Color(0.45f, 0.89f, 1.0f, 1f);
        _stAeroSecaoTitulo.margin = new RectOffset(0, 0, 0, 4);

        _stAeroLabel = new GUIStyle(GUI.skin.label);
        _stAeroLabel.richText = true;
        _stAeroLabel.fontSize = 12;
        _stAeroLabel.alignment = TextAnchor.MiddleLeft;
        _stAeroLabel.normal.textColor = textoPrincipal;

        _stAeroLabelWrap = new GUIStyle(GUI.skin.label);
        _stAeroLabelWrap.richText = true;
        _stAeroLabelWrap.wordWrap = true;
        _stAeroLabelWrap.fontSize = 12;
        _stAeroLabelWrap.normal.textColor = textoPrincipal;

        _stAeroLabelMuted = new GUIStyle(GUI.skin.label);
        _stAeroLabelMuted.richText = true;
        _stAeroLabelMuted.wordWrap = true;
        _stAeroLabelMuted.fontSize = 12;
        _stAeroLabelMuted.normal.textColor = textoSecundario;

        _stAeroBotao = CriarEstiloBotaoTatico(
            _texAeroBotaoNormal, _texAeroBotaoHover, _texAeroBotaoAtivo, textoPrincipal, TextAnchor.MiddleCenter, 12, new RectOffset(8, 8, 5, 5));

        _stAeroBotaoLista = CriarEstiloBotaoTatico(
            _texAeroBotaoNormal, _texAeroBotaoHover, _texAeroBotaoAtivo, textoPrincipal, TextAnchor.MiddleLeft, 12, new RectOffset(10, 10, 4, 4));
        _stAeroBotaoLista.clipping = TextClipping.Clip;

        _stAeroBotaoListaSelecionado = CriarEstiloBotaoTatico(
            _texAeroBotaoSelecionado, _texAeroBotaoPrimarioHover, _texAeroBotaoSelecionado, Color.white, TextAnchor.MiddleLeft, 12, new RectOffset(10, 10, 4, 4));
        _stAeroBotaoListaSelecionado.clipping = TextClipping.Clip;
        _stAeroBotaoListaSelecionado.fontStyle = FontStyle.Bold;

        _stAeroBotaoPrimario = CriarEstiloBotaoTatico(
            _texAeroBotaoPrimario, _texAeroBotaoPrimarioHover, _texAeroBotaoAtivo, Color.white, TextAnchor.MiddleCenter, 12, new RectOffset(8, 8, 5, 5));
        _stAeroBotaoPrimario.fontStyle = FontStyle.Bold;

        _stAeroBotaoPerigo = CriarEstiloBotaoTatico(
            _texAeroBotaoPerigo, _texAeroBotaoPerigoHover, _texAeroBotaoAtivo, Color.white, TextAnchor.MiddleCenter, 12, new RectOffset(8, 8, 5, 5));
        _stAeroBotaoPerigo.fontStyle = FontStyle.Bold;

        _stAeroBotaoAlerta = CriarEstiloBotaoTatico(
            _texAeroBotaoAlerta, _texAeroBotaoHover, _texAeroBotaoAtivo, new Color(1f, 0.90f, 0.65f, 1f), TextAnchor.MiddleCenter, 12, new RectOffset(8, 8, 5, 5));
        _stAeroBotaoAlerta.fontStyle = FontStyle.Bold;

        _stAeroBotaoFechar = CriarEstiloBotaoTatico(
            _texAeroBotaoPerigo, _texAeroBotaoPerigoHover, _texAeroBotaoAtivo, Color.white, TextAnchor.MiddleCenter, 13, new RectOffset(0, 0, 0, 0));
        _stAeroBotaoFechar.fontStyle = FontStyle.Bold;

        _uiAeroportoEstilosProntos = true;
    }

    private void DestruirTexturasTemaAeroporto()
    {
        DestruirTexturaSeguro(ref _texAeroPainelFundo);
        DestruirTexturaSeguro(ref _texAeroBorda);
        DestruirTexturaSeguro(ref _texAeroCabecalho);
        DestruirTexturaSeguro(ref _texAeroDestaqueCiano);
        DestruirTexturaSeguro(ref _texAeroCardFundo);
        DestruirTexturaSeguro(ref _texAeroBotaoNormal);
        DestruirTexturaSeguro(ref _texAeroBotaoHover);
        DestruirTexturaSeguro(ref _texAeroBotaoAtivo);
        DestruirTexturaSeguro(ref _texAeroBotaoSelecionado);
        DestruirTexturaSeguro(ref _texAeroBotaoPrimario);
        DestruirTexturaSeguro(ref _texAeroBotaoPrimarioHover);
        DestruirTexturaSeguro(ref _texAeroBotaoPerigo);
        DestruirTexturaSeguro(ref _texAeroBotaoPerigoHover);
        DestruirTexturaSeguro(ref _texAeroBotaoAlerta);
        DestruirTexturaSeguro(ref _texAeroAlertaFundo);
    }

    private GUIStyle CriarEstiloBotaoTatico(
        Texture2D normalTex,
        Texture2D hoverTex,
        Texture2D activeTex,
        Color textColor,
        TextAnchor alignment,
        int fontSize,
        RectOffset padding)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.richText = true;
        style.alignment = alignment;
        style.fontSize = fontSize;
        style.padding = padding;
        style.wordWrap = false;
        style.normal.background = normalTex;
        style.hover.background = hoverTex;
        style.active.background = activeTex;
        style.focused.background = hoverTex;
        style.onNormal.background = hoverTex;
        style.normal.textColor = textColor;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;
        return style;
    }

    private void DesenharMolduraMenuAeroporto(Rect r)
    {
        // Fundo principal
        GUI.DrawTexture(r, _texAeroPainelFundo);

        // Cabeçalho
        Rect header = new Rect(r.x, r.y, r.width, 46f);
        GUI.DrawTexture(header, _texAeroCabecalho);

        // Borda externa 1px + filete ciano superior
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), _texAeroDestaqueCiano);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), _texAeroBorda);
        GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), _texAeroBorda);
        GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), _texAeroBorda);
        GUI.DrawTexture(new Rect(r.x, header.yMax, r.width, 1f), _texAeroBorda);

        string nomeBase = string.IsNullOrEmpty(name) ? "BASE AÉREA MILITAR" : name.Replace("(Clone)", "").Trim().ToUpperInvariant();
        GUI.Label(new Rect(r.x + 14f, r.y + 5f, Mathf.Max(220f, r.width * 0.48f), 22f),
            "✈  CENTRO DE CONTROLE TÁTICO & AEROPORTO", _stAeroTituloHeader);
        GUI.Label(new Rect(r.x + 14f, r.y + 24f, Mathf.Max(220f, r.width * 0.48f), 18f),
            $"<color=#8FB5C9>{nomeBase}</color>  •  Frota Militar", _stAeroSubtituloHeader);

        string resumoFrota = $"Pátio <b>{avioesNoPatio.Count + transportesC700NoPatio.Count}</b>  •  Hangar <b>{avioesNoHangar.Count}</b>  •  Heli <b>{helicopterosDoAeroporto.Count}</b>";
        string statusEnergia = semEnergia
            ? "<color=#FFB347>⚡ SEM ENERGIA</color>"
            : "<color=#3FE49B>⚡ OPERACIONAL</color>";

        float larguraChips = Mathf.Max(180f, r.width * 0.44f);
        GUI.Label(new Rect(r.xMax - larguraChips - 48f, r.y + 12f, larguraChips, 22f),
            $"{resumoFrota}   |   {statusEnergia}", _stAeroBadge);
    }

    private static (string texto, string corHex) FormatarEstadoAviao(ControleAviao.EstadoAviao estado)
    {
        switch (estado)
        {
            case ControleAviao.EstadoAviao.ProntoNoPatio: return ("Pronto no Pátio", "#3FE49B");
            case ControleAviao.EstadoAviao.Decolando: return ("Decolando", "#FFC247");
            case ControleAviao.EstadoAviao.EmMissao: return ("Em Missão", "#33E1FF");
            case ControleAviao.EstadoAviao.Pousando: return ("Pousando", "#FFC247");
            case ControleAviao.EstadoAviao.RetornandoPraVaga: return ("Retornando", "#FFA94D");
            case ControleAviao.EstadoAviao.Taxiando: return ("Taxiando", "#FFD166");
            case ControleAviao.EstadoAviao.ReservaHangar: return ("No Hangar", "#9BB0BC");
            default: return (estado.ToString(), "#E2F1F8");
        }
    }

    private static (string texto, string corHex) FormatarEstadoC700(C700TransporteAereo.EstadoC700 estado)
    {
        switch (estado)
        {
            case C700TransporteAereo.EstadoC700.Solo: return ("No Solo", "#3FE49B");
            case C700TransporteAereo.EstadoC700.Estacionado: return ("Estacionado", "#3FE49B");
            case C700TransporteAereo.EstadoC700.Carregando: return ("Carregando", "#FFD166");
            case C700TransporteAereo.EstadoC700.Taxiando: return ("Taxiando", "#FFD166");
            case C700TransporteAereo.EstadoC700.Decolando: return ("Decolando", "#FFC247");
            case C700TransporteAereo.EstadoC700.EmVoo: return ("Em Voo", "#33E1FF");
            case C700TransporteAereo.EstadoC700.Aproximando: return ("Aproximando", "#FFA94D");
            case C700TransporteAereo.EstadoC700.Alinhando: return ("Alinhando", "#FFA94D");
            case C700TransporteAereo.EstadoC700.Pousando: return ("Pousando", "#FFC247");
            case C700TransporteAereo.EstadoC700.FalhaMissao: return ("Falha na Missão", "#FF6B6B");
            default: return (estado.ToString(), "#E2F1F8");
        }
    }

    private static string FormatarEstadoC17(Hegemonia.Aeronaves.C17.EstadoAviaoTransporte estado)
    {
        switch (estado)
        {
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Estacionado: return "Estacionado";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Pousado: return "Pousado";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.AguardandoDestino: return "Aguardando Destino";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.TaxiandoParaDecolagem: return "Taxiando p/ Decolagem";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.TaxiandoParaPista: return "Taxiando p/ Pista";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.AlinhandoNaPista: return "Alinhando na Pista";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Decolando: return "Decolando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.CorridaDecolagem: return "Corrida de Decolagem";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.RotacaoDecolagem: return "Rotação de Decolagem";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Subindo: return "Subindo";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Navegando: return "Navegando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.OrbitandoDestino: return "Orbitando Destino";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.PreparandoAproximacao: return "Preparando Aproximação";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.AlinhandoPouso: return "Alinhando Pouso";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Descendo: return "Descendo";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Pousando: return "Pousando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.TocandoPista: return "Tocando a Pista";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Freando: return "Freando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.TaxiandoParaVaga: return "Taxiando p/ Vaga";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Embarcando: return "Embarcando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Desembarcando: return "Desembarcando";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.RetornandoBase: return "Retornando à Base";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Arremetendo: return "Arremetendo";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.SemCombustivel: return "Sem Combustível";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.SemRota: return "Sem Rota";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Emergencia: return "Emergência";
            case Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Destruido: return "Destruído";
            default: return estado.ToString();
        }
    }

    // Aba Comercial removida

    private void DesenharAbaMilitar(float larguraDisponivel, float alturaDisponivel)
    {
        if (semEnergia)
        {
            GUILayout.BeginHorizontal(_stAeroAlertaCard);
            GUILayout.Label(
                "<color=#FFD082>⚡ " + LocalizationManager.T("airport.no_power_purchase", "AEROPORTO SEM ENERGIA — Construa uma usina para reativá-lo.").Replace("\n", " — ") + "</color>",
                _stAeroLabelWrap);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        float alturaBannerSemEnergia = semEnergia ? 54f : 0f;
        float alturaUtilColunas = Mathf.Max(220f, alturaDisponivel - alturaBannerSemEnergia - 6f);

        bool layoutDuasColunas = larguraDisponivel >= 720f && alturaDisponivel >= 250f;
        if (layoutDuasColunas)
        {
            float larguraEsquerda = Mathf.Clamp(larguraDisponivel * 0.45f, 320f, 445f);
            float alturaCardHangar = Mathf.Clamp(alturaUtilColunas * 0.25f, 122f, 150f);
            float alturaCardOrdens = Mathf.Max(220f, alturaUtilColunas - 6f);
            float alturaCardFrota = Mathf.Max(160f, alturaUtilColunas - alturaCardHangar - 20f);

            GUILayout.BeginHorizontal(GUILayout.Height(alturaUtilColunas), GUILayout.ExpandHeight(true));

            GUILayout.BeginVertical(GUILayout.Width(larguraEsquerda), GUILayout.Height(alturaUtilColunas));
            DesenharCardFrotaAtiva(true, alturaCardFrota);
            GUILayout.Space(8f);
            DesenharCardHangar(true, alturaCardHangar);
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            GUILayout.BeginVertical(_stAeroCard, GUILayout.ExpandWidth(true), GUILayout.Height(alturaCardOrdens));
            DesenharColunaOrdens(true, alturaCardOrdens);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
        else
        {
            scrollPosAbaMilitar = GUILayout.BeginScrollView(scrollPosAbaMilitar, GUILayout.ExpandHeight(true));
            DesenharCardFrotaAtiva(false);
            GUILayout.Space(8f);
            DesenharCardHangar(false);
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_stAeroCard);
            DesenharColunaOrdens(false);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }
    }

    private void DesenharCardFrotaAtiva(bool expandirAltura, float alturaCard = 0f)
    {
        int totalFrotaAtiva = avioesNoPatio.Count + transportesC700NoPatio.Count + helicopterosDoAeroporto.Count;
        float alturaFinalCard = expandirAltura && alturaCard > 0f ? alturaCard : 260f;
        GUILayout.BeginVertical(_stAeroCard, GUILayout.Height(alturaFinalCard));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>FROTA ATIVA</b>  <color=#8FB5C9>({totalFrotaAtiva})</color>", _stAeroSecaoTitulo);
        GUILayout.FlexibleSpace();
        GUILayout.Label("<color=#7D98A6>Clique para comandar</color>", _stAeroSubtituloHeader);
        GUILayout.EndHorizontal();

        float alturaListaFrota = expandirAltura && alturaCard > 0f
            ? Mathf.Max(110f, alturaFinalCard - 48f)
            : 220f;
        scrollPosFrota = GUILayout.BeginScrollView(scrollPosFrota, GUILayout.Height(alturaListaFrota), GUILayout.ExpandHeight(expandirAltura));

        if (totalFrotaAtiva == 0)
        {
            GUILayout.Label("Nenhuma aeronave pronta ou em voo vinculada a esta base.", _stAeroLabelMuted);
        }

        for (int i = 0, count = avioesNoPatio.Count; i < count; i++)
        {
            ControleAviao a = avioesNoPatio[i];
            if (a == null) continue;

            string nomeLimpo = ObterInfoAviao(a, out string corCristal, out string vidaStr);
            (string estadoRotulo, string corEst) = FormatarEstadoAviao(a.estadoAtual);
            bool selecionado = aviaoSelecionadoParaMissao == a;
            GUIStyle estiloLinha = selecionado ? _stAeroBotaoListaSelecionado : _stAeroBotaoLista;
            string prefixoSel = selecionado ? "<color=#00E5FF>▸ </color>" : "";

            if (GUILayout.Button($"{prefixoSel}<color={corCristal}>■</color> ✈️ <b>{nomeLimpo}</b>{vidaStr}  <color={corEst}>[{estadoRotulo}]</color>", estiloLinha, GUILayout.Height(30f)))
            {
                aviaoSelecionadoParaMissao = a;
                c700SelecionadoParaMissao = null;
                helicopteroSelecionadoParaMissao = null;
                _modoPousoC17Pendente = false;
            }
        }

        for (int i = 0; i < helicopterosDoAeroporto.Count; i++)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null) continue;

            string nomeHeli = heli.ObterRotuloExibicao();
            string estadoHeli = heli.ObterEstadoOperacionalAeroporto();
            string corEstadoHeli = heli.EstaEstacionadoNoAeroporto() ? "#3FE49B" : "#FFC247";
            string combustivelHeli = CombustivelUnidade.TextoCurto(heli);
            if (!string.IsNullOrEmpty(combustivelHeli))
            {
                combustivelHeli = $" <color=#33E1FF>{combustivelHeli}</color>";
            }

            bool selecionado = helicopteroSelecionadoParaMissao == heli;
            GUIStyle estiloLinha = selecionado ? _stAeroBotaoListaSelecionado : _stAeroBotaoLista;
            string prefixoSel = selecionado ? "<color=#00E5FF>▸ </color>" : "";

            if (GUILayout.Button($"{prefixoSel}🚁 <b>{nomeHeli}</b>{combustivelHeli}  <color={corEstadoHeli}>[{estadoHeli}]</color>", estiloLinha, GUILayout.Height(30f)))
            {
                helicopteroSelecionadoParaMissao = heli;
                aviaoSelecionadoParaMissao = null;
                c700SelecionadoParaMissao = null;
            }
        }

        for (int i = 0; i < transportesC700NoPatio.Count; i++)
        {
            C700TransporteAereo transporte = transportesC700NoPatio[i];
            if (transporte == null) continue;

            string nomeLimpo = ObterInfoAviao(transporte, out string corCristal, out string vidaStr);
            (string estadoC700, string corEstado) = FormatarEstadoC700(transporte.estadoAtual);
            bool selecionado = c700SelecionadoParaMissao == transporte;
            GUIStyle estiloLinha = selecionado ? _stAeroBotaoListaSelecionado : _stAeroBotaoLista;
            string prefixoSel = selecionado ? "<color=#00E5FF>▸ </color>" : "";

            if (GUILayout.Button($"{prefixoSel}<color={corCristal}>■</color> 🛫 <b>{nomeLimpo}</b>{vidaStr}  <color={corEstado}>[{estadoC700}]</color>", estiloLinha, GUILayout.Height(30f)))
            {
                c700SelecionadoParaMissao = transporte;
                aviaoSelecionadoParaMissao = null;
                helicopteroSelecionadoParaMissao = null;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DesenharCardHangar(bool expandirAltura, float alturaCard = 0f)
    {
        float alturaFinalCard = expandirAltura
            ? (alturaCard > 0f ? alturaCard : 145f)
            : 130f;
        GUILayout.BeginVertical(_stAeroCard, GUILayout.Height(alturaFinalCard));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>🔒 HANGAR</b>  <color=#8FB5C9>({avioesNoHangar.Count})</color>", _stAeroSecaoTitulo);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Liberar Todos", _stAeroBotao, GUILayout.Width(106f), GUILayout.Height(22f)))
        {
            LiberarTodosDoHangar();
        }
        GUILayout.EndHorizontal();

        float alturaListaHangar = expandirAltura
            ? Mathf.Max(68f, alturaFinalCard - 48f)
            : 90f;
        scrollPosHangar = GUILayout.BeginScrollView(scrollPosHangar, GUILayout.Height(alturaListaHangar), GUILayout.ExpandHeight(expandirAltura));

        if (avioesNoHangar.Count == 0)
        {
            GUILayout.Label("Hangar vazio — nenhuma aeronave na reserva.", _stAeroLabelMuted);
        }

        for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
        {
            ControleAviao h = avioesNoHangar[i];
            if (h == null) continue;

            string nomeLimpo = ObterInfoAviao(h, out string corCristal, out string vidaStr);
            bool interromperLista = false;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={corCristal}>■</color> 🔒 {nomeLimpo}{vidaStr}", _stAeroLabel, GUILayout.ExpandWidth(true), GUILayout.Height(26f));

            if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
            {
                if (GUILayout.Button("⮂ TROCAR", _stAeroBotao, GUILayout.Width(96f), GUILayout.Height(26f)))
                {
                    TrocarAvioesLogicaGeral(h, aviaoSelecionadoParaMissao);
                    aviaoSelecionadoParaMissao = null;
                    interromperLista = true;
                }
            }
            else if (ObterPrimeiraVagaLivre() != null)
            {
                if (GUILayout.Button("▶ LIBERAR", _stAeroBotaoPrimario, GUILayout.Width(96f), GUILayout.Height(26f)))
                {
                    LiberarAviaoParaPatio(h);
                    interromperLista = true;
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("Pátio Lotado", _stAeroBotao, GUILayout.Width(96f), GUILayout.Height(26f));
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();

            if (interromperLista) break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DesenharColunaOrdens(bool usarScrollInterno, float alturaCard = 0f)
    {
        if (aviaoSelecionadoParaMissao == null && helicopteroSelecionadoParaMissao == null
            && c700SelecionadoParaMissao == null && transportesC700NoPatio.Count == 1)
        {
            c700SelecionadoParaMissao = transportesC700NoPatio[0];
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>PAINEL DE ORDENS & TELEMETRIA</b>", _stAeroSecaoTitulo);
        GUILayout.FlexibleSpace();
        if (aviaoSelecionadoParaMissao != null || helicopteroSelecionadoParaMissao != null || c700SelecionadoParaMissao != null)
        {
            if (GUILayout.Button("Limpar Seleção", _stAeroBotao, GUILayout.Width(108f), GUILayout.Height(22f)))
            {
                aviaoSelecionadoParaMissao = null;
                helicopteroSelecionadoParaMissao = null;
                c700SelecionadoParaMissao = null;
                _modoPousoC17Pendente = false;
            }
        }
        GUILayout.EndHorizontal();

        if (usarScrollInterno)
        {
            scrollPosOrdensUI = alturaCard > 0f
                ? GUILayout.BeginScrollView(scrollPosOrdensUI, GUILayout.Height(Mathf.Max(150f, alturaCard - 48f)), GUILayout.ExpandHeight(true))
                : GUILayout.BeginScrollView(scrollPosOrdensUI, GUILayout.ExpandHeight(true));
        }

        bool desenhouAviao = false;
        if (aviaoSelecionadoParaMissao != null && (avioesNoPatio.Contains(aviaoSelecionadoParaMissao) || aviaoSelecionadoParaMissao.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null))
        {
            desenhouAviao = true;
            var c17Script = aviaoSelecionadoParaMissao.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>();
            if (c17Script != null)
            {
                DesenharOrdensC17(c17Script);
            }
            else
            {
                DesenharOrdensAviaoMilitar();
            }
        }

        bool desenhouHeli = helicopteroSelecionadoParaMissao != null && helicopterosDoAeroporto.Contains(helicopteroSelecionadoParaMissao);
        if (desenhouHeli)
        {
            DesenharPainelHelicoptero();
        }

        bool desenhouC700 = transportesC700NoPatio.Count > 0 && c700SelecionadoParaMissao != null && transportesC700NoPatio.Contains(c700SelecionadoParaMissao);
        if (desenhouC700 || (!desenhouAviao && !desenhouHeli && transportesC700NoPatio.Count > 0))
        {
            DesenharPainelC700();
        }

        if (!desenhouAviao && !desenhouHeli && !desenhouC700 && transportesC700NoPatio.Count == 0)
        {
            GUILayout.Space(12f);
            GUILayout.Label("<b>Nenhuma aeronave selecionada</b>", _stAeroLabel);
            GUILayout.Space(4f);
            GUILayout.Label(
                "Selecione uma aeronave na coluna <b>FROTA ATIVA</b> à esquerda para configurar a base de pouso, definir patrulha, reconhecimento, ataque ou retorno.",
                _stAeroLabelMuted);
            GUILayout.Space(10f);
            GUILayout.Label("<color=#7D98A6>Atalhos:  <b>[Z]</b> Abre/Fecha este painel   •   <b>Botão Direito</b> no mapa confirma o alvo selecionado.</color>", _stAeroLabelMuted);
        }

        if (usarScrollInterno)
        {
            GUILayout.EndScrollView();
        }
    }

    private void DesenharOrdensC17(Hegemonia.Aeronaves.C17.C17TransporteController c17Script)
    {
        string nomeLimpoC17 = ObterInfoAviao(aviaoSelecionadoParaMissao, out string corCristalC17, out string vidaStrC17);
        GUILayout.Label($"<size=14><b><color={corCristalC17}>■</color> {nomeLimpoC17}{vidaStrC17}</b></size>  <color=#8FB5C9>(C10/C17)</color>", _stAeroLabel);
        GUILayout.Label($"<b>Estado Operacional:</b> <color=#33E1FF>{FormatarEstadoC17(c17Script.EstadoAtual)}</color>", _stAeroLabel);

        var transportSys = c17Script.GetComponent<Hegemonia.Aeronaves.C17.C17TransportSystem>();
        if (transportSys != null)
        {
            GUILayout.Label(
                $"<b>Carga Militar C17:</b> Soldados <color=#33E1FF>{transportSys.TropasEmbarcadasCount}/{transportSys.CapacidadeSoldados}</color>  •  Veículos <color=#33E1FF>{transportSys.VeiculosEmbarcadosCount}/{transportSys.CapacidadeVeiculos}</color>",
                _stAeroLabel);
        }

        GUILayout.Space(6f);

        if (aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
        {
            GUILayout.Label("<color=#FFD166>⚠️ MODO ALVO ATIVO! Feche o menu e clique no mapa com o Botão Direito.</color>", _stAeroLabelWrap);
            if (GUILayout.Button("❌ Cancelar Ordem", _stAeroBotaoPerigo, GUILayout.Height(30f)))
            {
                aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                _modoPousoC17Pendente = false;
                LimparModoMassaAereo();
                AtualizarModoInteracaoManualAeroporto();
            }
            GUILayout.Space(4f);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🚀 Decolar & Direcionar", _stAeroBotaoPrimario, GUILayout.Height(34f)))
        {
            _modoPousoC17Pendente = false;
            ExecutarModoRadar(false);
        }
        GUILayout.Space(6f);
        if (GUILayout.Button("🛬 Marcar Pouso", _stAeroBotao, GUILayout.Height(34f)))
        {
            _modoPousoC17Pendente = true;
            ExecutarModoRadar(false);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);

        bool noSolo = c17Script.EstadoAtual == Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Pousado
                   || c17Script.EstadoAtual == Hegemonia.Aeronaves.C17.EstadoAviaoTransporte.Estacionado;

        GUILayout.BeginHorizontal();
        GUI.enabled = noSolo;
        if (GUILayout.Button("📥 Embarcar Tropas", _stAeroBotao, GUILayout.Height(32f)))
        {
            c17Script.Comando_EmbarcarTropas();
        }
        GUILayout.Space(6f);

        bool temCarga = transportSys != null && (transportSys.TropasEmbarcadasCount > 0 || transportSys.VeiculosEmbarcadosCount > 0);
        GUI.enabled = temCarga;
        if (GUILayout.Button("📤 Desembarcar", _stAeroBotao, GUILayout.Height(32f)))
        {
            c17Script.Comando_Desembarcar();
        }
        GUILayout.Space(6f);

        GUI.enabled = true;
        if (GUILayout.Button("🔙 Retornar à Base", _stAeroBotaoAlerta, GUILayout.Height(32f)))
        {
            c17Script.ComandoZ_VoltarAeroporto();
        }
        GUILayout.EndHorizontal();

        GUI.enabled = true;
    }

    private void DesenharOrdensAviaoMilitar()
    {
        string nomeLimpo = ObterInfoAviao(aviaoSelecionadoParaMissao, out string corCristal, out string vidaStr);
        (string estadoStr, string corEstado) = FormatarEstadoAviao(aviaoSelecionadoParaMissao.estadoAtual);

        GUILayout.Label($"<size=14><b><color={corCristal}>■</color> {nomeLimpo}</b></size>{vidaStr}  <color={corEstado}>[{estadoStr}]</color>", _stAeroLabel);
        GUILayout.Label($"📍 {aviaoSelecionadoParaMissao.ObterResumoLocalizacao()}", _stAeroLabelWrap);

        GerenciadorAeroporto destinoAtualAereo = aviaoSelecionadoParaMissao.DestinoPousoEscolhido;
        if (destinoAtualAereo != null && !ControleAviao.PodeReceberPouso(destinoAtualAereo))
        {
            GUILayout.Label("<color=#FFA94D>Destino mantido; base indisponível ou sem vaga. Escolha outra base manualmente se desejar.</color>", _stAeroLabelWrap);
        }

        ControleAviao.ListarDestinosPouso(aviaoSelecionadoParaMissao, _basesDestinoAereoUI);
        if (_basesDestinoAereoUI.Count > 0)
        {
            GUILayout.Space(6f);
            GUILayout.Label("<b>BASE DE POUSO</b>", _stAeroSecaoTitulo);
            int desenhadas = 0;
            for (int i = 0; i < _basesDestinoAereoUI.Count; i++)
            {
                GerenciadorAeroporto baseDestinoUI = _basesDestinoAereoUI[i];
                if (baseDestinoUI == null) continue;

                if (desenhadas % 2 == 0) GUILayout.BeginHorizontal();

                bool destinoAtual = baseDestinoUI == destinoAtualAereo;
                GUIStyle estiloBase = destinoAtual ? _stAeroBotaoPrimario : _stAeroBotao;
                if (GUILayout.Button((destinoAtual ? "✓ " : "✈ ") + ControleAviao.RotuloBase(baseDestinoUI), estiloBase, GUILayout.Height(26f), GUILayout.ExpandWidth(true)))
                {
                    bool emVoo = aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao
                        || aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.Decolando;
                    if (!aviaoSelecionadoParaMissao.SelecionarDestinoPouso(baseDestinoUI, emVoo))
                        HUDAjudaRTS.MostrarMensagemTemporaria("A base não pode receber esta aeronave agora.", 3f);
                }

                desenhadas++;
                if (desenhadas % 2 == 1)
                {
                    GUILayout.Space(6f);
                }
                else
                {
                    GUILayout.EndHorizontal();
                }
            }
            if (desenhadas % 2 == 1)
            {
                GUILayout.Label(GUIContent.none, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
        }

        GUI.enabled = true;
        GUILayout.Space(8f);
        GUILayout.Label("<b>ORDENS DE MISSÃO</b>", _stAeroSecaoTitulo);

        if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
        {
            if (aviaoSelecionadoParaMissao.aeroportoOrigem != this && aviaoSelecionadoParaMissao.aeroportoOrigem != null)
            {
                GUILayout.Label($"<color=#FFA94D>✈️ Estacionado em outra base/navio: {aviaoSelecionadoParaMissao.aeroportoOrigem.name.Replace("(Clone)", "")}</color>", _stAeroLabelWrap);
                GUILayout.Space(4f);
                if (GUILayout.Button("🔙 REQUISITAR RETORNO IMEDIATO", _stAeroBotaoPrimario, GUILayout.Height(36f)))
                {
                    // Transferência entre bases precisa marcar o voo como retorno.
                    // Enviar somente a posição do aeroporto fazia o avião orbitar sem pousar.
                    aviaoSelecionadoParaMissao.DefinirBaseAlternativaEIniciarRetorno(this);
                    aviaoSelecionadoParaMissao = null;
                    FecharPainelAeroporto();
                    return;
                }
            }
            else if (aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
            {
                GUILayout.Label("<color=#FFD166>⚠️ MODO ALVO ATIVO! Feche o menu e clique no mapa com o Botão Direito.</color>", _stAeroLabelWrap);
                if (GUILayout.Button("❌ Cancelar Ordem", _stAeroBotaoPerigo, GUILayout.Height(30f)))
                {
                    aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                    _modoPousoC17Pendente = false;
                    LimparModoMassaAereo();
                    AtualizarModoInteracaoManualAeroporto();
                }
                GUILayout.Space(4f);
            }

            bool isKamikaze = aviaoSelecionadoParaMissao.GetComponent<KamikazeDrone>() != null;
            bool isBombardeiro = aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>() != null;
            int totalMesmoModelo = 0;

            if (!isKamikaze)
            {
                string modeloPainel = ObterChaveModeloAviao(aviaoSelecionadoParaMissao);
                if (_ultimoModeloPainelPatrulha != modeloPainel)
                {
                    _ultimoModeloPainelPatrulha = modeloPainel;
                    qtdPatrulhaGrupo = 1;
                }

                totalMesmoModelo = ContarAvioesDisponiveisMesmoModelo(aviaoSelecionadoParaMissao);
                if (totalMesmoModelo < 3)
                {
                    qtdPatrulhaGrupo = 1;
                }
                else
                {
                    qtdPatrulhaGrupo = Mathf.Clamp(qtdPatrulhaGrupo, 1, totalMesmoModelo);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>Qtd. P/ Patrulha:</b> <color=#8BE9FD>{qtdPatrulhaGrupo}/{totalMesmoModelo}</color>", _stAeroLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("−", _stAeroBotao, GUILayout.Width(32f), GUILayout.Height(26f))) qtdPatrulhaGrupo = Mathf.Max(1, qtdPatrulhaGrupo - 1);
                    if (GUILayout.Button("+", _stAeroBotao, GUILayout.Width(32f), GUILayout.Height(26f))) qtdPatrulhaGrupo = Mathf.Min(totalMesmoModelo, qtdPatrulhaGrupo + 1);
                    if (GUILayout.Button("Todos", _stAeroBotao, GUILayout.Width(64f), GUILayout.Height(26f))) qtdPatrulhaGrupo = totalMesmoModelo;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                }
            }

            if (isKamikaze)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Qtd. P/ Ataque:</b> <color=#8BE9FD>{qtdMassaDrone}</color>", _stAeroLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("−", _stAeroBotao, GUILayout.Width(32f), GUILayout.Height(26f))) qtdMassaDrone = Mathf.Max(1, qtdMassaDrone - 1);
                if (GUILayout.Button("+", _stAeroBotao, GUILayout.Width(32f), GUILayout.Height(26f))) qtdMassaDrone++;
                if (GUILayout.Button("Todos", _stAeroBotao, GUILayout.Width(64f), GUILayout.Height(26f)))
                {
                    int totais = 0;
                    foreach (var a in avioesNoPatio) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                    foreach (var a in avioesNoHangar) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                    qtdMassaDrone = totais;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🚀 ATAQUE EM MASSA", _stAeroBotaoPerigo, GUILayout.Height(36f)))
                {
                    LimparModoMassaAereo();
                    aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
                    esperandoCliqueMassa = true;
                }
                GUILayout.Space(6f);
                if (GUILayout.Button("💣 Ataque Solo", _stAeroBotaoPrimario, GUILayout.Height(36f)))
                {
                    ExecutarModoRadar(false);
                    esperandoCliqueMassa = false;
                }
                GUILayout.EndHorizontal();
            }
            else if (isBombardeiro)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("💣 Ataque Área (Tapete)", _stAeroBotaoPerigo, GUILayout.Height(36f)))
                {
                    aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueAoSolo;
                    ExecutarModoRadar(false);
                }
                GUILayout.Space(6f);
                string textoPatrulhaBombardeiro = (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                    ? $"🛡️ Radar (Móvel) x{qtdPatrulhaGrupo}"
                    : "🛡️ Radar (Móvel)";
                if (GUILayout.Button(textoPatrulhaBombardeiro, _stAeroBotaoPrimario, GUILayout.Height(36f)))
                {
                    aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.Patrulha;
                    if (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                    {
                        PrepararPatrulhaEmGrupo();
                    }
                    else
                    {
                        ExecutarModoRadar(false, true);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
                if (GUILayout.Button("🚀 Ataque em Massa", _stAeroBotaoAlerta, GUILayout.Height(34f)))
                {
                    aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueEmMassa;
                    ExecutarModoRadar(false);
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("👁️ Reconhecimento", _stAeroBotao, GUILayout.Height(36f))) ExecutarModoRadar(true);
                GUILayout.Space(6f);
                string textoPatrulhaGrupo = (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                    ? $"🛡️ Patrulha Aérea x{qtdPatrulhaGrupo}"
                    : "🛡️ Patrulha Aérea";
                if (GUILayout.Button(textoPatrulhaGrupo, _stAeroBotaoPrimario, GUILayout.Height(36f)))
                {
                    if (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                    {
                        PrepararPatrulhaEmGrupo();
                    }
                    else
                    {
                        ExecutarModoRadar(false, true);
                    }
                }
                GUILayout.Space(6f);
                if (GUILayout.Button("💣 Ataque Solo", _stAeroBotaoPerigo, GUILayout.Height(36f))) ExecutarModoRadar(false);
                GUILayout.EndHorizontal();
            }
        }
        else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            var c17EmVoo = aviaoSelecionadoParaMissao.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>();
            GUILayout.Label("<color=#33E1FF>Aeronave civil/militar operando no espaço aéreo.</color>", _stAeroLabelWrap);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 ALTERAR ALVO/DESTINO", _stAeroBotaoPrimario, GUILayout.Height(38f)))
            {
                _modoPousoC17Pendente = false;
                ExecutarModoRadar(false);
            }
            GUILayout.Space(6f);

            if (c17EmVoo != null)
            {
                if (GUILayout.Button("🛬 MARCAR ÁREA DE POUSO", _stAeroBotao, GUILayout.Height(38f)))
                {
                    _modoPousoC17Pendente = true;
                    ExecutarModoRadar(false);
                }
                GUILayout.Space(6f);

                if (GUILayout.Button("🔙 RETORNAR AO AEROPORTO", _stAeroBotaoAlerta, GUILayout.Height(38f)))
                {
                    c17EmVoo.ComandoZ_VoltarAeroporto();
                    aviaoSelecionadoParaMissao = null;
                    FecharPainelAeroporto();
                }
            }
            else if (GUILayout.Button("🔙 ABORTAR E RETORNAR À BASE", _stAeroBotaoPerigo, GUILayout.Height(38f)))
            {
                aviaoSelecionadoParaMissao.ComandoRetornarBase();
                aviaoSelecionadoParaMissao = null;
                FecharPainelAeroporto();
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label($"<color=#FFA94D>Aeronave em trânsito: {estadoStr}...</color>", _stAeroLabel);
            GUI.enabled = false;
            GUILayout.Button("Aguarde a manobra de pista...", _stAeroBotao, GUILayout.Height(34f));
            GUI.enabled = true;
        }

        if (semEnergia) GUI.enabled = true;
    }

    private void DesenharPainelHelicoptero()
    {
        if (helicopteroSelecionadoParaMissao == null || !helicopterosDoAeroporto.Contains(helicopteroSelecionadoParaMissao))
        {
            return;
        }

        GUILayout.Space(6f);
        GUILayout.BeginVertical();
        if (semEnergia) GUI.enabled = false;

        string nomeHeliSelecionado = helicopteroSelecionadoParaMissao.ObterRotuloExibicao();
        GUILayout.Label($"<size=14><b>🚁 {nomeHeliSelecionado}</b></size>", _stAeroLabel);
        GUILayout.Label($"<color=#33E1FF>{helicopteroSelecionadoParaMissao.ObterEstadoOperacionalAeroporto()}</color>", _stAeroLabel);
        string combustivelHeliSelecionado = CombustivelUnidade.TextoCurto(helicopteroSelecionadoParaMissao);
        if (!string.IsNullOrEmpty(combustivelHeliSelecionado))
        {
            GUILayout.Label($"<color=#33E1FF>{combustivelHeliSelecionado}</color>", _stAeroLabel);
        }

        GUILayout.Space(6f);

        if (_modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum)
        {
            if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
            {
                GUILayout.Label($"<color=#FFD166>PATRULHA ATIVA: clique direito adiciona e já aplica ponto ({_rotaPatrulhaHelicoptero.Count}). ENTER encerra edição, BACKSPACE desfaz, ESC cancela.</color>", _stAeroLabelWrap);
            }
            else
            {
                string modoTexto = "ATAQUE LOCAL";
                if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Reconhecimento) modoTexto = "RECONHECIMENTO";
                else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Transporte) modoTexto = "TRANSPORTE";
                GUILayout.Label($"<color=#FFD166>{modoTexto} ATIVO: clique com botão direito no mapa. ESC cancela.</color>", _stAeroLabelWrap);
            }

            if (GUILayout.Button("❌ Cancelar Ordem Helicóptero", _stAeroBotaoPerigo, GUILayout.Height(32f)))
            {
                CancelarModoHelicoptero();
            }
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👁️ Reconhecimento", _stAeroBotao, GUILayout.Height(34f)))
            {
                IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Reconhecimento);
            }
            GUILayout.Space(6f);
            if (GUILayout.Button("🛡️ Patrulha", _stAeroBotaoPrimario, GUILayout.Height(34f)))
            {
                IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Patrulha);
            }
            if (helicopteroSelecionadoParaMissao.podeAtacar)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("💥 Ataque local", _stAeroBotaoPerigo, GUILayout.Height(34f)))
                {
                    IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.AtaqueLocal);
                }
            }
            GUILayout.EndHorizontal();

            if (helicopteroSelecionadoParaMissao.EhHelicopteroTransporte())
            {
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🛬 Transporte", _stAeroBotaoPrimario, GUILayout.Height(32f)))
                {
                    IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Transporte);
                }
                GUILayout.Space(8f);
                GUILayout.Label($"Tropas: <b>{helicopteroSelecionadoParaMissao.soldadosEmbarcados.Count}/{helicopteroSelecionadoParaMissao.capacidadeMaxima}</b>", _stAeroLabel, GUILayout.Width(130f), GUILayout.Height(32f));
                GUILayout.EndHorizontal();

                bool podeOperarTropas = helicopteroSelecionadoParaMissao.PodeOperarTropasNoMenu();
                bool pousadoForaDaBase = podeOperarTropas && !helicopteroSelecionadoParaMissao.EstaEstacionadoNoAeroporto();
                if (podeOperarTropas && (pousadoForaDaBase || helicopteroSelecionadoParaMissao.TemSoldados()))
                {
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = pousadoForaDaBase && helicopteroSelecionadoParaMissao.TemEspaco() > 0;
                    if (GUILayout.Button("📥 Recolher tropas", _stAeroBotao, GUILayout.Height(30f)))
                    {
                        int recolhidos = helicopteroSelecionadoParaMissao.RecolherTropasPeloMenu();
                        Debug.Log($"[Aeroporto] Helicóptero recolheu {recolhidos} tropa(s).");
                    }
                    GUILayout.Space(6f);
                    GUI.enabled = podeOperarTropas && helicopteroSelecionadoParaMissao.TemSoldados();
                    if (GUILayout.Button("📤 Desembarcar", _stAeroBotao, GUILayout.Height(30f)))
                    {
                        int desembarcados = helicopteroSelecionadoParaMissao.DesembarcarTropasNoLocalAtual();
                        Debug.Log($"[Aeroporto] Helicóptero desembarcou {desembarcados} tropa(s).");
                    }
                    GUI.enabled = !semEnergia;
                    GUILayout.EndHorizontal();
                }
            }

            if (!helicopteroSelecionadoParaMissao.EstaEstacionadoNoAeroporto())
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("🔙 Retornar para vaga H", _stAeroBotaoAlerta, GUILayout.Height(32f)))
                {
                    helicopteroSelecionadoParaMissao.RetornarParaVagaAeroporto();
                }
            }
        }

        if (semEnergia) GUI.enabled = true;
        GUILayout.EndVertical();
    }

    private void DesenharPainelC700()
    {
        if (transportesC700NoPatio.Count == 0)
        {
            return;
        }

        if (c700SelecionadoParaMissao == null && transportesC700NoPatio.Count == 1)
        {
            c700SelecionadoParaMissao = transportesC700NoPatio[0];
        }

        GUILayout.Space(6f);
        GUILayout.Label("<b>TRANSPORTE C700</b>", _stAeroSecaoTitulo);

        if (transportesC700NoPatio.Count > 1)
        {
            scrollPosC700 = GUILayout.BeginScrollView(scrollPosC700, GUILayout.Height(84f));
            for (int i = 0; i < transportesC700NoPatio.Count; i++)
            {
                C700TransporteAereo transporte = transportesC700NoPatio[i];
                if (transporte == null) continue;

                string nomeLimpo = ObterInfoAviao(transporte, out string corCristal, out string vidaStr);
                (string estadoC700, string corEstado) = FormatarEstadoC700(transporte.estadoAtual);
                GUIStyle estiloC700 = c700SelecionadoParaMissao == transporte ? _stAeroBotaoListaSelecionado : _stAeroBotaoLista;
                if (GUILayout.Button($"<color={corCristal}>■</color> {nomeLimpo}{vidaStr} <color={corEstado}>[{estadoC700}]</color>", estiloC700, GUILayout.Height(26f)))
                {
                    c700SelecionadoParaMissao = transporte;
                    aviaoSelecionadoParaMissao = null;
                    helicopteroSelecionadoParaMissao = null;
                }
            }
            GUILayout.EndScrollView();
        }

        if (c700SelecionadoParaMissao != null && transportesC700NoPatio.Contains(c700SelecionadoParaMissao))
        {
            GUILayout.Space(4f);
            string nomeSelecionado = ObterInfoAviao(c700SelecionadoParaMissao, out string corCristalSelecionado, out string vidaSelecionada);
            (string estadoC700Sel, string corC700Sel) = FormatarEstadoC700(c700SelecionadoParaMissao.estadoAtual);
            GUILayout.Label($"<size=14><b><color={corCristalSelecionado}>■</color> {nomeSelecionado}{vidaSelecionada}</b></size>  <color={corC700Sel}>[{estadoC700Sel}]</color>", _stAeroLabel);

            if (semEnergia) GUI.enabled = false;

            GUILayout.Label($"Carga real: <color=#33E1FF>{c700SelecionadoParaMissao.QuantidadeCargaAtual}/{c700SelecionadoParaMissao.CapacidadeCargaAtual}</color>  •  Manifesto: <color=#33E1FF>{c700SelecionadoParaMissao.QuantidadeManifestoTotal}</color>", _stAeroLabel);

            if (c700SelecionadoParaMissao.TemDestinoVisual)
            {
                Vector3 destinoAtual = c700SelecionadoParaMissao.DestinoVisualAtual;
                GUILayout.Label($"Destino: <color=#8BE9FD>X {destinoAtual.x:0} / Z {destinoAtual.z:0}</color>", _stAeroLabel);
            }

            GUILayout.Space(6f);

            GUI.enabled = c700SelecionadoParaMissao.EstaNoSolo;
            if (GUILayout.Button("📥 Puxar tropas", _stAeroBotao, GUILayout.Height(30f)))
            {
                c700SelecionadoParaMissao.PuxarUnidadesProximas();
            }
            GUI.enabled = true;
            GUILayout.Space(4f);

            if (c700SelecionadoParaMissao.EstaNoSolo)
            {
                if (c700SelecionadoParaMissao.AguardandoDestinoAereo)
                {
                    GUILayout.Label("<color=#FFD166>MODO AÉREO ATIVO. Feche o menu e clique com o botão direito no mapa.</color>", _stAeroLabelWrap);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Cancelar ordem", _stAeroBotaoPerigo, GUILayout.Height(32f)))
                    {
                        c700SelecionadoParaMissao.CancelarModoAereo();
                        c700SelecionadoParaMissao = null;
                    }
                    GUILayout.Space(6f);
                    if (GUILayout.Button("Fechar menu", _stAeroBotao, GUILayout.Height(32f)))
                    {
                        FecharPainelAeroporto();
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Preparar decolagem / destino", _stAeroBotaoPrimario, GUILayout.Height(36f)))
                    {
                        c700SelecionadoParaMissao.PrepararMissaoAerea();
                        FecharPainelAeroporto();
                    }
                    GUILayout.Space(6f);
                    if (GUILayout.Button("Voltar para aeroporto", _stAeroBotaoAlerta, GUILayout.Height(36f)))
                    {
                        c700SelecionadoParaMissao.OrdenarRetornoAoAeroporto();
                        c700SelecionadoParaMissao = null;
                        FecharPainelAeroporto();
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4f);
                if (GUILayout.Button("Desembarcar carga", _stAeroBotao, GUILayout.Height(32f)))
                {
                    c700SelecionadoParaMissao.DesembarcarTudo();
                }
            }
            else
            {
                if (GUILayout.Button("Retornar a base", _stAeroBotaoAlerta, GUILayout.Height(36f)))
                {
                    c700SelecionadoParaMissao.OrdenarRetornoAoAeroporto();
                    c700SelecionadoParaMissao = null;
                    FecharPainelAeroporto();
                }
            }

            if (semEnergia) GUI.enabled = true;
        }
    }

    private void ExecutarModoRadar(bool deveSerPassivo, bool usarMarcadorPatrulhaAviao = false)
    {
        if (aviaoSelecionadoParaMissao == null) return;
        LimparModoMassaAereo();
        _usarMarcadorPatrulhaAviaoNoProximoClique = usarMarcadorPatrulhaAviao;
        
        // Tenta forçar o modo no script de Missil, caso exista
        LancadorMisselCaca missilScript = aviaoSelecionadoParaMissao.GetComponent<LancadorMisselCaca>();
        if (missilScript != null)
        {
            missilScript.modoPassivo = deveSerPassivo;
            Debug.Log($"[Aeroporto] Modo Passivo definido como: {deveSerPassivo}");
        }

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
        menuAtivo = false;
        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        AtualizarModoInteracaoManualAeroporto();
        Debug.Log($"[Aeroporto] Modo Missão Ativado. Fechando painel. Dê a ordem com o clique Direito!");
    }

    private void TrocarAvioesLogicaGeral(ControleAviao modeloSubsaturado, ControleAviao hangarASeAfastar)
    {
        if (hangarASeAfastar.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) return;

        Transform vagaOcupadaLivre = hangarASeAfastar.vagaRetorno;
        Vector3 xyzVaga = hangarASeAfastar.transform.position;
        Quaternion anguloVaga = hangarASeAfastar.transform.rotation;

        avioesNoPatio.Remove(hangarASeAfastar);
        avioesNoHangar.Remove(modeloSubsaturado);

        GuardarAviaoNoHangarInstantaneo(hangarASeAfastar, false);

        modeloSubsaturado.gameObject.SetActive(true);
        modeloSubsaturado.transform.position = xyzVaga;
        modeloSubsaturado.transform.rotation = anguloVaga;
        modeloSubsaturado.vagaRetorno = vagaOcupadaLivre; 
        modeloSubsaturado.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        avioesNoPatio.Add(modeloSubsaturado);
        // Esta troca usa o estado pronto diretamente, sem passar por
        // DefinirEstado. Libere aqui uma ordem emitida enquanto a aeronave
        // ainda estava no hangar, mantendo o mesmo contrato dos outros
        // caminhos de saída para o pátio.
        modeloSubsaturado.TentarIniciarOrdemPendente();
        modeloSubsaturado.TentarRetomarMissaoAposReabastecimento();
    }

    private void LiberarAviaoParaPatio(ControleAviao aviaoDoHangar)
    {
        Transform vagaDesignada = ObterPrimeiraVagaLivre();
        if (vagaDesignada == null) return;
        
        ColocarAviaoInstantaneamenteNoPatio(aviaoDoHangar, vagaDesignada, true);
    }
    
    public void LiberarTodosDoHangar()
    {
        // Copia a lista para evitar modificação simultânea no foreach
        List<ControleAviao> copiaHangar = new List<ControleAviao>(avioesNoHangar);
        for (int i = 0, count = copiaHangar.Count; i < count; i++)
        {
            if (ObterPrimeiraVagaLivre() == null) break; // Pátio lotado
            if (copiaHangar[i] != null) LiberarAviaoParaPatio(copiaHangar[i]);
        }
    }
    
    public virtual void GuardarNoHangarAutomatico(ControleAviao aviao)
    {
        GuardarAviaoNoHangarInstantaneo(aviao, true);
    }

    protected void GuardarAviaoNoHangarInstantaneo(ControleAviao aviao, bool removerDoPatio)
    {
        if (aviao == null) return;
        aviao.baseEstacionamentoAtual = this;

        if (removerDoPatio)
        {
            avioesNoPatio.Remove(aviao);
        }

        if (!avioesNoHangar.Contains(aviao)) avioesNoHangar.Add(aviao);

        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
        aviao.AplicarEstadoSistemasAeronave(false, false);
        aviao.vagaRetorno = null;
        aviao.transform.SetParent(transform, true);

        if (wpPreparacao != null)
        {
            aviao.transform.position = wpPreparacao.position;
            aviao.transform.rotation = wpPreparacao.rotation;
        }
        else if (hangarAviao != null)
        {
            aviao.transform.position = hangarAviao.position;
            aviao.transform.rotation = hangarAviao.rotation;
        }

        aviao.gameObject.SetActive(false);
    }

    protected bool ColocarAviaoInstantaneamenteNoPatio(ControleAviao aviao, Transform vaga, bool removerDoHangar)
    {
        if (aviao == null || vaga == null) return false;
        aviao.baseEstacionamentoAtual = this;

        if (removerDoHangar)
        {
            avioesNoHangar.Remove(aviao);
        }

        aviao.gameObject.SetActive(true);
        aviao.transform.SetParent(transform, true);
        // Offset de altura para evitar que o avião fique dentro da mesh do convés
        float alturaOffset = aviao.ObterAlturaEstacionamento();
        aviao.transform.position = vaga.position + (vaga.up * alturaOffset);
        aviao.transform.rotation = vaga.rotation;
        aviao.vagaRetorno = vaga;
        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        aviao.AplicarEstadoSistemasAeronave(false, false);

        if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);
        // Se a aeronave havia voltado para abastecer e precisou aguardar no
        // hangar, retoma a patrulha somente agora, quando existe uma vaga e
        // ela está realmente pronta para decolar.
        aviao.TentarIniciarOrdemPendente();
        aviao.TentarRetomarMissaoAposReabastecimento();
        return true;
    }

    protected virtual void ReporPatioComAvioesDoHangar()
    {
        for (int i = avioesNoHangar.Count - 1; i >= 0 && ObterPrimeiraVagaLivre() != null; i--)
        {
            ControleAviao aviaoDoHangar = avioesNoHangar[i];
            if (aviaoDoHangar == null)
            {
                avioesNoHangar.RemoveAt(i);
                continue;
            }

            LiberarAviaoParaPatio(aviaoDoHangar);
        }
    }

    private void ColetarAeronavesProntas(List<ControleAviao> destino)
    {
        destino.Clear();
        for (int i = 0; i < avioesNoPatio.Count; i++)
        {
            ControleAviao aviao = avioesNoPatio[i];
            if (aviao != null
                && aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio
                && ControleAviao.BasesDaMesmaEquipe(aviao, this))
            {
                destino.Add(aviao);
            }
        }
    }

    private bool LancarAeronaveIA(ControleAviao aviao, Vector3 alvoReconhecimento, Vector3 alvoPatrulha, Vector3 alvoAtaque)
    {
        if (aviao == null || aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
        {
            return false;
        }

        Vector3 alvo;
        int missao = Random.Range(0, 3);
        switch (missao)
        {
            case 0:
                alvo = alvoReconhecimento != Vector3.zero ? alvoReconhecimento : transform.position + transform.forward * 700f;
                break;
            case 1:
                alvo = alvoPatrulha != Vector3.zero ? alvoPatrulha : transform.position + transform.right * 350f;
                break;
            default:
                alvo = alvoAtaque != Vector3.zero ? alvoAtaque : transform.position + transform.forward * 1100f;
                break;
        }

        Vector3 alvoEstrategico = alvo;
        Vector3 alvoVoo = alvo;
        alvoVoo.y = Mathf.Max(alvoVoo.y, 60f);
        if (!aviao.PodeExecutarMissaoComRetorno(alvoVoo))
        {
            Debug.LogWarning($"[Aeroporto] Sortida da IA recusada para {aviao.name}: combustível ou rota de retorno insuficientes.", aviao);
            return false;
        }
        ControleUnidade controle = aviao.GetComponent<ControleUnidade>();
        bool ordemAceita;

        // Sortidas de patrulha da IA precisam ser registradas como rota, não
        // como uma missão pontual. Assim o controlador preserva o circuito
        // quando a aeronave retorna com 30% de combustível, é abastecida e
        // volta a decolar.
        if (missao == 1)
        {
            ordemAceita = controle != null
                ? controle.EmitirOrdemPatrulha(new List<Vector3> { alvoVoo })
                : aviao.ReceberOrdemPatrulha(new List<Vector3> { alvoVoo });
        }
        else
        {
            ordemAceita = controle != null
                ? controle.EmitirOrdemMover(alvoEstrategico, true)
                : aviao.ReceberOrdemManual(alvoEstrategico);
        }

        // Nenhum subsistema deve ser configurado antes de o executor de voo
        // aceitar a missão. Isso evita uma aeronave ficar armada, com alvo
        // antigo ou em modo de combate quando a pista/combustível a recusou.
        if (!ordemAceita)
        {
            return false;
        }

        aviao.aguardandoCliqueRadar = false;
        aviao.alvoEstrategico = alvoEstrategico;

        AviaoBombardeiro bombardeiro = aviao.GetComponent<AviaoBombardeiro>();
        if (bombardeiro != null)
        {
            bombardeiro.alvoAreaSolo = alvoEstrategico;
            bombardeiro.alvoMassa1 = alvoEstrategico + new Vector3(-35f, 0f, -10f);
            bombardeiro.alvoMassa2 = alvoEstrategico + new Vector3(35f, 0f, 10f);
            bombardeiro.modoDeAtaque = missao == 2
                ? (Random.value > 0.45f ? AviaoBombardeiro.ModoAtaque.AtaqueAoSolo : AviaoBombardeiro.ModoAtaque.AtaqueEmMassa)
                : AviaoBombardeiro.ModoAtaque.Patrulha;
        }

        CacaVooRealista vooRealista = aviao.GetComponent<CacaVooRealista>();
        if (vooRealista != null)
        {
            vooRealista.alvoGPS = alvoVoo;
        }

        LancadorMisselCaca lancadorCaca = aviao.GetComponent<LancadorMisselCaca>();
        if (lancadorCaca != null)
        {
            lancadorCaca.modoPassivo = missao != 2;
        }

        if (controle != null)
        {
            // Deslocamentos de reconhecimento/patrulha permanecem passivos;
            // somente a missão ofensiva arma o modo de combate oficial.
            controle.DefinirModoCombate(missao == 2);
        }

        aviao.alvoPrioritarioIA = missao == 2;
        return true;
    }
}
