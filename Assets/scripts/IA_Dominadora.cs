using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// IA DOMINADORA
/// IA RTS robusta, com validaÃ§Ã£o espacial, zonas, score de construÃ§Ã£o,
/// separaÃ§Ã£o absoluta entre terra / Ã¡gua / ar e proteÃ§Ã£o contra erros.
/// VERSÃƒO OTIMIZADA: Preserva TODA a lÃ³gica original (Pier, Ares, Estaleiros),
/// mas reduz drasticamente o uso de CPU e geraÃ§Ã£o de Garbage (Lixo de MemÃ³ria).
/// </summary>
public class IA_Dominadora : MonoBehaviour
{
    // =========================================================
    // IDENTIDADE
    // =========================================================
    [Header("Identidade")]
    public int teamID = 2;
    public string nomeNacao = "ImpÃ©rio Dominador";
    public Color corNacao = Color.red;

    // =========================================================
    // ECONOMIA E DIFICULDADE
    // =========================================================
    [Header("Economia")]
    public float dinheiroIA = 15000f;
    public float rendaBase = 60f;
    [Range(1, 5)] public int nivelDificuldade = 3;

    // =========================================================
    // REFERÃŠNCIAS DE MAPA
    // =========================================================
    [Header("ReferÃªncias do Mapa")]
    public Transform referenciaAgua;
    public Transform referenciaTerra;
    public Transform referenciaCapitalInicial;

    [Tooltip("Opcional. Use se sua Ã¡gua estiver numa layer especÃ­fica.")]
    public string nomeLayerAgua = "Water";

    [Tooltip("Opcional. Use se sua terra estiver numa layer especÃ­fica.")]
    public string nomeLayerTerra = "Chao";

    [Tooltip("Se true, tenta achar agua/terra automaticamente pelo nome na cena.")]
    public bool autoDetectarMarcadores = true;

    // =========================================================
    // ESPAÃ‡O E TERRENO
    // =========================================================
    [Header("EspaÃ§o e Terreno")]
    public float nivelDoMar = 1f;
    public float distanciaNavalDaCosta = 170f;
    public float distanciaMinimaEntreBases = 350f;
    public float raioZonaCapital = 90f;
    public float raioZonaEconomica = 180f;
    public float raioZonaMilitar = 230f;
    public float raioZonaDefensiva = 260f;
    public float raioZonaAerea = 320f;
    public float raioZonaNavalBusca = 160f;
    public float raioBuscaConstrucaoExtra = 420f;
    public float alturaAereaSpawn = 25f;
    public float raioSaidaFabrica = 36f;
    public float raioChecagemSpawnNaval = 26f;

    // =========================================================
    // TEMPO E RITMO
    // =========================================================
    [Header("Ciclos")]
    public float tempoPazInicial = 60f;
    public float cooldownConstrucao = 8f;
    public float intervaloEconomia = 1f;
    public float intervaloLogistica = 2.5f;
    public float intervaloTatica = 4f;
    public float intervaloManutencao = 5f;
    public float intervaloReescaneamento = 8f;

    // =========================================================
    // LIMITES E METAS
    // =========================================================
    [Header("Metas")]
    public int metaSoldados = 18;
    public int metaTanques = 8;
    public int metaHelicopteros = 4;
    public int metaCacas = 4;
    public int metaNavios = 3;
    public int metaSubmarinos = 2;
    public int maxTropasTotais = 120;

    // =========================================================
    // DEBUG
    // =========================================================
    [Header("Debug")]
    public bool debugLogs = true;
    public bool permitirLogsEmRuntime = false;
    public bool debugGizmos = true;
    public bool debugMostrarZonas = true;
    public bool debugMostrarCandidatos = true;
    public bool debugMostrarRejeicoes = true;
    public bool debugMostrarAlvos = true;

    [Header("Grid Tatico")]
    public bool usarGridTatico = true;
    [Range(10f, 80f)] public float tamanhoCelula = 24f;
    [Range(4, 32)] public int raioGrid = 14;
    public float intervaloAtualizacaoGrid = 10f;

    [Header("Producao Inteligente")]
    public float cooldownProducaoPadrao = 4f;
    [Range(1, 4)] public int maxProducoesPorCiclo = 2;

    [Header("Scheduler Tatico")]
    [Range(0.1f, 1f)] public float tickSchedulerIA = 0.25f;
    [Range(0.15f, 2f)] public float intervaloPosturaTatica = 0.6f;
    [Range(0.2f, 3f)] public float intervaloProducaoTatica = 1.1f;
    [Range(0.2f, 2f)] public float intervaloTransportesTatico = 0.55f;
    [Range(0.2f, 2f)] public float intervaloNavalTatico = 0.8f;
    [Range(0.15f, 1.5f)] public float intervaloCombateTatico = 0.35f;

    [Header("Lotes Taticos")]
    [Range(4, 64)] public int maxTropasProcessadasPorCiclo = 18;
    [Range(2, 24)] public int maxTransportesProcessadosPorCiclo = 6;
    [Range(2, 24)] public int maxNaviosProcessadosPorCiclo = 6;
    [Range(1, 12)] public int maxAvioesPatioPorCiclo = 4;

    [Header("Performance")]
    [Range(24, 192)] public int maxCandidatosPorBusca = 96;
    [Range(8, 48)] public int maxCandidatosDoGrid = 18;
    [Range(0.5f, 20f)] public float cooldownFalhaConstrucao = 8f;
    [Range(0.5f, 10f)] public float intervaloResumoRejeicoes = 2f;
    [Range(1, 6)] public int maxMotivosResumoRejeicoes = 3;

    [Header("Diagnostico")]
    public bool debugPerformance = false;
    [TextArea(5, 12)] public string resumoPerformance = string.Empty;

    // =========================================================
    // ESTADOS E CLASSES
    // =========================================================
    public enum EstadoIA { Acordando, FundandoCapital, ExpandindoBase, Reagrupando, GuerraTotal, DefesaDesesperada }
    public enum TipoTerreno { Desconhecido, Terra, Agua }
    public enum TipoZona { Nenhuma, Capital, Economia, Militar, Defesa, Aerea, Naval, Expansao }
    public enum CategoriaObjeto
    {
        Desconhecido, Prefeitura, Quartel, Fabrica, Refinaria, Torreta, Antiaerea, Aeroporto,
        Estaleiro, Pier, Plataforma, Soldado, Tanque, Helicoptero, Caca, Transporte, TransporteAereo, Navio, Submarino
    }

    [System.Serializable]
    public class ZonaIA
    {
        public TipoZona tipo;
        public Vector3 centro;
        public float raio;

        public ZonaIA(TipoZona tipo, Vector3 centro, float raio)
        {
            this.tipo = tipo;
            this.centro = centro;
            this.raio = raio;
        }

        public bool Contem(Vector3 pos)
        {
            Vector3 a = centro; a.y = 0;
            Vector3 b = pos; b.y = 0;
            return Vector3.Distance(a, b) <= raio;
        }
    }

    public class RegistroRejeicao
    {
        public Vector3 pos;
        public string motivo;
        public float tempo;

        public RegistroRejeicao(Vector3 pos, string motivo)
        {
            this.pos = pos;
            this.motivo = motivo;
            this.tempo = Time.time;
        }
    }

    public class CandidatoConstrucao
    {
        public Vector3 pos;
        public float score;
        public string motivoRejeicao;
        public bool valido;

        public CandidatoConstrucao(Vector3 pos)
        {
            this.pos = pos;
        }
    }

    public class DadosCategoria
    {
        public CategoriaObjeto categoria;
        public TipoTerreno terreno;
        public TipoZona zonaPreferida;
        public float raioSeguranca;
        public float distanciaIdeal;
        public float custoPadrao;
        public bool ehPredio;
        public bool ehNaval;
        public bool ehAereo;
        public Vector2 footprint;
        public float raioSaida;
        public int prioridade;
        public float valorEstrategico;
    }

    public class CelulaTatica
    {
        public Vector3 posicao;
        public TipoTerreno terreno;
        public bool navegavel;
        public bool agua;
        public bool terra;
        public bool areaAereaValida;
        public bool ocupada;
        public TipoZona zona;
        public float ameaca;
        public float distanciaBase;
        public float distanciaRecursos;
        public float distanciaCosta;
    }

    public class PedidoProducao
    {
        public string chave;
        public float custo;
        public float score;
        public bool voa;
        public bool naval;
    }

    public enum PapelNavalIA
    {
        Nenhum,
        Carrier,
        Transporte,
        EscoltaMissil,
        PatrulhaEscolta,
        Submarino,
        Logistico,
        Estaleiro
    }

    public enum EstadoPlanoNaval
    {
        PatrulhaCosteira,
        ConcentracaoNaval,
        ContatoNaval,
        PreparandoInvasaoAnfibia,
        ComboioEmTransito,
        AssaltoCosteiro,
        SuportePortaAvioes
    }

    public class ContatoNavalIA
    {
        public Transform alvo;
        public Vector3 posicao;
        public float ultimaDeteccao = -999f;
        public int forcaEstimativa;
        public bool altoValor;

        public bool EstaAtivo(float janelaSegundos = 12f)
        {
            return Time.time - ultimaDeteccao <= janelaSegundos && posicao != Vector3.zero;
        }

        public void Limpar()
        {
            alvo = null;
            posicao = Vector3.zero;
            ultimaDeteccao = -999f;
            forcaEstimativa = 0;
            altoValor = false;
        }
    }

    public class GrupoNavalIA
    {
        public readonly List<GameObject> carriers = new List<GameObject>();
        public readonly List<GameObject> transportes = new List<GameObject>();
        public readonly List<GameObject> escoltasMissil = new List<GameObject>();
        public readonly List<GameObject> patrulhas = new List<GameObject>();
        public readonly List<GameObject> submarinos = new List<GameObject>();
        public readonly List<GameObject> logisticos = new List<GameObject>();

        public void Limpar()
        {
            carriers.Clear();
            transportes.Clear();
            escoltasMissil.Clear();
            patrulhas.Clear();
            submarinos.Clear();
            logisticos.Clear();
        }

        public int TotalTransportes()
        {
            return transportes.Count;
        }

        public int TotalEscoltas()
        {
            return escoltasMissil.Count + patrulhas.Count + submarinos.Count;
        }
    }

    // =========================================================
    // MEMÃ“RIA E BUFFERS (OTIMIZADOS PARA PERFORMANCE)
    // =========================================================
    public EstadoIA estadoAtual = EstadoIA.Acordando;

    private float momentoFimPaz;
    private float proximoReescaneamento;
    private bool prefeituraPronta;

    private Dictionary<string, List<GameObject>> biblioteca = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, DadosCategoria> bancoDados = new Dictionary<string, DadosCategoria>();

    private readonly List<GameObject> meusPredios = new List<GameObject>();
    private readonly List<GameObject> minhasTropas = new List<GameObject>();
    private readonly List<GameObject> meusTransportes = new List<GameObject>();
    private readonly List<GameObject> meusNavios = new List<GameObject>();
    private readonly List<IdentidadeUnidade> inimigosConhecidos = new List<IdentidadeUnidade>();
    private readonly List<Transform> basesInimigasConhecidas = new List<Transform>();
    private readonly List<Transform> economiasInimigasConhecidas = new List<Transform>();
    private readonly List<IdentidadeUnidade> bufferUnidadesRegistradas = new List<IdentidadeUnidade>(256);
    private readonly List<Imovel> bufferImoveisRegistrados = new List<Imovel>(128);
    private readonly List<ZonaIA> zonas = new List<ZonaIA>();
    private readonly List<RegistroRejeicao> rejeicoesRecentes = new List<RegistroRejeicao>();
    private readonly Dictionary<Vector2Int, CelulaTatica> gridTatico = new Dictionary<Vector2Int, CelulaTatica>();
    private readonly Dictionary<int, float> cooldownProducaoEstruturas = new Dictionary<int, float>();
    private readonly Dictionary<string, float> cooldownFalhaConstrucaoPorTipo = new Dictionary<string, float>();
    private readonly Dictionary<string, int> contagemRejeicoesPorMotivo = new Dictionary<string, int>();
    
    // Buffers para evitar criaÃ§Ã£o de lixo na memÃ³ria (NonAlloc)
    private readonly Collider[] bufferOcupacao = new Collider[128];
    private readonly Collider[] bufferFootprint = new Collider[128];
    private readonly Collider[] bufferAmeaca = new Collider[128];
    private readonly Collider[] bufferContatoNaval = new Collider[128];
    private readonly RaycastHit[] bufferRaycast = new RaycastHit[128]; // NOVO BUFFER OTIMIZADO
    private NavMeshPath bufferCaminhoTerrestre;

    private readonly List<Vector3> debugUltimosCandidatosValidos = new List<Vector3>();
    private readonly List<Vector3> debugUltimosCandidatosInvalidos = new List<Vector3>();
    private readonly GrupoNavalIA grupoNavalPlanejado = new GrupoNavalIA();
    private readonly ContatoNavalIA contatoNavalAtual = new ContatoNavalIA();

    private Transform alvoJogadorBase;
    private Transform alvoJogadorEconomia;
    private int forcaInimigaAerea;
    private int avioesAliadosConhecidos;
    private float proximaAtualizacaoGrid;
    private float proximoResumoRejeicoes;
    private float proximaAtualizacaoPostura;
    private float proximaAtualizacaoProducao;
    private float proximaAtualizacaoTransportes;
    private float proximaAtualizacaoNaval;
    private float proximaAtualizacaoCombate;
    private int cursorTropasTaticas;
    private int cursorTransportesTaticos;
    private int cursorNaviosTaticos;
    private int cursorAvioesPatio;
    private int ultimoLoteTropas;
    private int ultimoLoteTransportes;
    private int ultimoLoteNavios;
    private int ultimoLoteAvioes;
    private int ultimoPedidosProduzidos;
    private int ultimoInimigosReconhecidos;
    private float custoEconomiaMs;
    private float custoLogisticaMs;
    private float custoTaticaMs;
    private float custoManutencaoMs;
    private float custoReconhecimentoMs;
    private float picoEconomiaMs;
    private float picoLogisticaMs;
    private float picoTaticaMs;
    private float picoManutencaoMs;
    private float picoReconhecimentoMs;
    private float proximaAtualizacaoResumoPerformance;
    private EstadoPlanoNaval estadoPlanoNaval = EstadoPlanoNaval.PatrulhaCosteira;
    private float inicioPreparacaoAnfibia = -999f;
    private Vector3 ultimoPontoReuniaoNaval;
    private Vector3 ultimoPontoCargaNaval;
    private Vector3 ultimoPontoDesembarqueNaval;
    private Vector3 ultimoPontoCarrierNaval;

    // =========================================================
    // UNITY
    // =========================================================
    void Awake()
    {
        bufferCaminhoTerrestre = new NavMeshPath();
        ConstruirBancoDeDados();
    }

    void Start()
    {
        // A IA cria um nome do país ao iniciar o jogo
        nomeNacao = "Império de " + GeradorNomesBatismo.GerarNome();

        if (autoDetectarMarcadores)
            BuscarSinalizadoresGlobais();

        CriarZonasIniciais();
        StartCoroutine(RotinaInicial());
    }

    // =========================================================
    // INICIALIZAÃ‡ÃƒO E BANCO DE DADOS (Preservado 100%)
    // =========================================================
    void ConstruirBancoDeDados()
    {
        bancoDados["prefeitura"] = new DadosCategoria { categoria = CategoriaObjeto.Prefeitura, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Capital, raioSeguranca = 80f, distanciaIdeal = 0f, custoPadrao = 2000f, ehPredio = true };
        bancoDados["quartel"] = new DadosCategoria { categoria = CategoriaObjeto.Quartel, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Militar, raioSeguranca = 48f, distanciaIdeal = 120f, custoPadrao = 300f, ehPredio = true };
        bancoDados["fabrica"] = new DadosCategoria { categoria = CategoriaObjeto.Fabrica, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Militar, raioSeguranca = 70f, distanciaIdeal = 155f, custoPadrao = 800f, ehPredio = true };
        bancoDados["refinaria"] = new DadosCategoria { categoria = CategoriaObjeto.Refinaria, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Economia, raioSeguranca = 48f, distanciaIdeal = 110f, custoPadrao = 500f, ehPredio = true };
        bancoDados["torreta"] = new DadosCategoria { categoria = CategoriaObjeto.Torreta, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Defesa, raioSeguranca = 30f, distanciaIdeal = 210f, custoPadrao = 400f, ehPredio = true };
        bancoDados["antiaerea"] = new DadosCategoria { categoria = CategoriaObjeto.Antiaerea, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Defesa, raioSeguranca = 34f, distanciaIdeal = 230f, custoPadrao = 800f, ehPredio = true };
        bancoDados["aeroporto"] = new DadosCategoria { categoria = CategoriaObjeto.Aeroporto, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Aerea, raioSeguranca = 180f, distanciaIdeal = 300f, custoPadrao = 2500f, ehPredio = true };
        bancoDados["estaleiro"] = new DadosCategoria { categoria = CategoriaObjeto.Estaleiro, terreno = TipoTerreno.Agua, zonaPreferida = TipoZona.Naval, raioSeguranca = 75f, distanciaIdeal = 0f, custoPadrao = 1500f, ehPredio = true, ehNaval = true };
        bancoDados["pier"] = new DadosCategoria { categoria = CategoriaObjeto.Pier, terreno = TipoTerreno.Agua, zonaPreferida = TipoZona.Naval, raioSeguranca = 70f, distanciaIdeal = 0f, custoPadrao = 1000f, ehPredio = true, ehNaval = true };
        bancoDados["plataforma"] = new DadosCategoria { categoria = CategoriaObjeto.Plataforma, terreno = TipoTerreno.Agua, zonaPreferida = TipoZona.Naval, raioSeguranca = 70f, distanciaIdeal = 0f, custoPadrao = 2000f, ehPredio = true, ehNaval = true };
        bancoDados["soldado"] = new DadosCategoria { categoria = CategoriaObjeto.Soldado, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Militar, raioSeguranca = 6f, custoPadrao = 150f, ehPredio = false };
        bancoDados["tanque"] = new DadosCategoria { categoria = CategoriaObjeto.Tanque, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Militar, raioSeguranca = 10f, custoPadrao = 600f, ehPredio = false };
        bancoDados["helicoptero"] = new DadosCategoria { categoria = CategoriaObjeto.Helicoptero, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Aerea, raioSeguranca = 12f, custoPadrao = 900f, ehPredio = false, ehAereo = true };
        bancoDados["caca"] = new DadosCategoria { categoria = CategoriaObjeto.Caca, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Aerea, raioSeguranca = 14f, custoPadrao = 1200f, ehPredio = false, ehAereo = true };
        bancoDados["transporte"] = new DadosCategoria { categoria = CategoriaObjeto.Transporte, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Militar, raioSeguranca = 10f, custoPadrao = 400f, ehPredio = false };
        bancoDados["transporte_aereo"] = new DadosCategoria { categoria = CategoriaObjeto.TransporteAereo, terreno = TipoTerreno.Terra, zonaPreferida = TipoZona.Aerea, raioSeguranca = 12f, custoPadrao = 400f, ehPredio = false, ehAereo = true };
        bancoDados["navio"] = new DadosCategoria { categoria = CategoriaObjeto.Navio, terreno = TipoTerreno.Agua, zonaPreferida = TipoZona.Naval, raioSeguranca = 18f, custoPadrao = 1500f, ehPredio = false, ehNaval = true };
        bancoDados["submarino"] = new DadosCategoria { categoria = CategoriaObjeto.Submarino, terreno = TipoTerreno.Agua, zonaPreferida = TipoZona.Naval, raioSeguranca = 18f, custoPadrao = 2000f, ehPredio = false, ehNaval = true };

        AplicarDadosEstruturais();
    }

    void AplicarDadosEstruturais()
    {
        ConfigurarDadosEstruturais("prefeitura", new Vector2(42f, 42f), 38f, 10, 200f);
        ConfigurarDadosEstruturais("quartel", new Vector2(26f, 26f), 28f, 8, 120f);
        ConfigurarDadosEstruturais("fabrica", new Vector2(34f, 34f), 34f, 9, 180f);
        ConfigurarDadosEstruturais("refinaria", new Vector2(24f, 24f), 20f, 9, 160f);
        ConfigurarDadosEstruturais("torreta", new Vector2(14f, 14f), 10f, 6, 80f);
        ConfigurarDadosEstruturais("antiaerea", new Vector2(15f, 15f), 10f, 8, 110f);
        ConfigurarDadosEstruturais("aeroporto", new Vector2(56f, 56f), 60f, 10, 220f);
        ConfigurarDadosEstruturais("estaleiro", new Vector2(34f, 34f), 28f, 9, 180f);
        ConfigurarDadosEstruturais("pier", new Vector2(28f, 28f), 24f, 8, 150f);
        ConfigurarDadosEstruturais("plataforma", new Vector2(24f, 24f), 20f, 7, 140f);
        ConfigurarDadosEstruturais("soldado", new Vector2(2f, 2f), 2f, 7, 40f);
        ConfigurarDadosEstruturais("tanque", new Vector2(4f, 4f), 4f, 8, 90f);
        ConfigurarDadosEstruturais("helicoptero", new Vector2(6f, 6f), 8f, 8, 100f);
        ConfigurarDadosEstruturais("caca", new Vector2(8f, 8f), 10f, 9, 120f);
        ConfigurarDadosEstruturais("transporte", new Vector2(4f, 4f), 4f, 5, 60f);
        ConfigurarDadosEstruturais("transporte_aereo", new Vector2(6f, 6f), 8f, 6, 70f);
        ConfigurarDadosEstruturais("navio", new Vector2(10f, 10f), 12f, 9, 140f);
        ConfigurarDadosEstruturais("submarino", new Vector2(10f, 10f), 12f, 9, 150f);
    }

    void ConfigurarDadosEstruturais(string chave, Vector2 footprint, float raioSaida, int prioridade, float valorEstrategico)
    {
        if (!bancoDados.ContainsKey(chave)) return;
        bancoDados[chave].footprint = footprint;
        bancoDados[chave].raioSaida = raioSaida;
        bancoDados[chave].prioridade = prioridade;
        bancoDados[chave].valorEstrategico = valorEstrategico;
    }

    IEnumerator RotinaInicial()
    {
        Log($"[{nomeNacao}] IA Dominadora iniciando.");
        estadoAtual = EstadoIA.Acordando;
        momentoFimPaz = Time.time + 4f + tempoPazInicial;

        yield return new WaitForSeconds(2f);

        RealizarScanDeArquivos();
        BuscarSinalizadoresGlobais();
        CriarZonasIniciais();
        AtualizarReconhecimentoGlobal(true);
        AnalisarOponente();

        StartCoroutine(CicloEconomico());
        StartCoroutine(CicloLogistico());
        StartCoroutine(CicloTatico());
        StartCoroutine(CicloManutencao());
    }

    // =========================================================
    // CICLOS (cache de WaitForSeconds para zero alloc por iteração)
    // =========================================================
    IEnumerator CicloEconomico()
    {
        // Cache: reutiliza o mesmo objeto a cada itéração em vez de alocar new WaitForSeconds
        WaitForSeconds _espera = new WaitForSeconds(intervaloEconomia);
        while (true)
        {
            yield return _espera;
            float inicio = Time.realtimeSinceStartup;
            LimparMortos();

            float ganho = rendaBase * (1f + nivelDificuldade * 0.12f);
            // OTIMIZAÇÃO: loop manual evita alocação do enumerador LINQ toda execução
            int prediosVivos = 0;
            for (int _i = 0; _i < meusPredios.Count; _i++)
                if (meusPredios[_i] != null) prediosVivos++;
            ganho += prediosVivos * 4f;
            ganho += Contar("refinaria") * 22f;
            ganho += Contar("plataforma") * 24f;

            dinheiroIA += ganho;
            if (dinheiroIA < 0f) dinheiroIA = 0f;
            RegistrarTempoModulo(ref custoEconomiaMs, ref picoEconomiaMs, inicio);
            AtualizarResumoPerformance();
        }
    }

    IEnumerator CicloManutencao()
    {
        // Cache: reutiliza o mesmo WaitForSeconds a cada iteração
        WaitForSeconds _espera = new WaitForSeconds(intervaloManutencao);
        while (true)
        {
            yield return _espera;
            float inicio = Time.realtimeSinceStartup;
            LimparMortos();
            LimparRejeicoesAntigas();

            if (dinheiroIA < 1000f)
            {
                RegistrarTempoModulo(ref custoManutencaoMs, ref picoManutencaoMs, inicio);
                AtualizarResumoPerformance();
                continue;
            }

            // OTIMIZAÇÃO: for indexado evita enumerador do foreach em List<T>
            for (int _i = 0; _i < meusPredios.Count; _i++)
            {
                GameObject predio = meusPredios[_i];
                if (predio == null) continue;
                SistemaDeDanos dmg = predio.GetComponent<SistemaDeDanos>();
                if (dmg == null) continue;
                if (dmg.vidaAtual >= dmg.vidaMaxima) continue;

                dinheiroIA -= 50f;
                dmg.vidaAtual += 150f;
                if (dmg.vidaAtual > dmg.vidaMaxima) dmg.vidaAtual = dmg.vidaMaxima;
            }

            RegistrarTempoModulo(ref custoManutencaoMs, ref picoManutencaoMs, inicio);
            AtualizarResumoPerformance();
        }
    }

    IEnumerator CicloLogistico()
    {
        // Caches: reutiliza WaitForSeconds em vez de alocar new por iteração
        WaitForSeconds _esperaObra     = new WaitForSeconds(cooldownConstrucao);
        WaitForSeconds _esperaPadrao   = new WaitForSeconds(intervaloLogistica);
        while (true)
        {
            float inicio = Time.realtimeSinceStartup;
            LimparMortos();

            if (Time.time >= proximoReescaneamento)
            {
                proximoReescaneamento = Time.time + intervaloReescaneamento;
                BuscarSinalizadoresGlobais();
                RevalidarZonas();
                AtualizarReconhecimentoGlobal(true);
                AnalisarOponente();
            }

            bool fezObra = false;

            if (!prefeituraPronta)
            {
                estadoAtual = EstadoIA.FundandoCapital;
                fezObra = FundarCapital();
            }
            else
            {
                if (estadoAtual != EstadoIA.DefesaDesesperada)
                {
                    estadoAtual = EstadoIA.ExpandindoBase;
                    fezObra = GerenciarExpansaoBase();
                }
            }

            RegistrarTempoModulo(ref custoLogisticaMs, ref picoLogisticaMs, inicio);
            AtualizarResumoPerformance();

            if (fezObra) yield return _esperaObra;
            else yield return _esperaPadrao;
        }
    }

    IEnumerator CicloTatico()
    {
        // Cache: reutiliza WaitForSeconds (valor fixo de tickSchedulerIA)
        WaitForSeconds _esperaTick = new WaitForSeconds(Mathf.Max(0.1f, tickSchedulerIA));
        while (true)
        {
            float inicio = Time.realtimeSinceStartup;
            if (prefeituraPronta)
            {
                LimparMortos();
                if (Time.time >= proximaAtualizacaoPostura)
                {
                    AnalisarOponente();
                    DefinirPosturaGlobal();
                    proximaAtualizacaoPostura = Time.time + Mathf.Max(0.15f, intervaloPosturaTatica);
                }

                if (Time.time >= proximaAtualizacaoProducao)
                {
                    GerenciarProducaoTropas();
                    proximaAtualizacaoProducao = Time.time + Mathf.Max(0.2f, intervaloProducaoTatica);
                }

                if (Time.time >= proximaAtualizacaoTransportes)
                {
                    ultimoLoteTransportes = GerenciarLogisticaTransportes();
                    proximaAtualizacaoTransportes = Time.time + Mathf.Max(0.2f, intervaloTransportesTatico);
                }

                if (Time.time >= proximaAtualizacaoNaval)
                {
                    ultimoLoteNavios = GerenciarTaticaNaval();
                    proximaAtualizacaoNaval = Time.time + Mathf.Max(0.2f, intervaloNavalTatico);
                }

                if (Time.time >= proximaAtualizacaoCombate)
                {
                    ultimoLoteTropas = 0;
                    ultimoLoteAvioes = 0;

                    if (estadoAtual == EstadoIA.GuerraTotal)
                    {
                        ultimoLoteTropas = LancarOfensivaMassa();
                    }
                    else if (estadoAtual == EstadoIA.Reagrupando)
                    {
                        ultimoLoteTropas = PatrulharFronteiras();
                    }
                    else if (estadoAtual == EstadoIA.DefesaDesesperada)
                    {
                        ultimoLoteTropas = RecuarParaDefesa();
                    }

                    proximaAtualizacaoCombate = Time.time + Mathf.Max(0.15f, intervaloCombateTatico);
                }
            }

            RegistrarTempoModulo(ref custoTaticaMs, ref picoTaticaMs, inicio);
            AtualizarResumoPerformance();
            yield return _esperaTick;
        }
    }

    // =========================================================
    // MAPA, MARCADORES E ZONAS
    // =========================================================
    void BuscarSinalizadoresGlobais()
    {
        if (referenciaAgua == null)
        {
            MarcadorSuperficieMapa marcadorAgua = RegistroSuperficieMapa.EncontrarPrimeiro(TipoSuperficieMapa.Agua);
            if (marcadorAgua != null)
            {
                referenciaAgua = marcadorAgua.transform;
                nivelDoMar = referenciaAgua.position.y;
            }
            else
            {
                GameObject a = GameObject.Find("agua") ?? GameObject.Find("Agua");
                if (a != null) referenciaAgua = a.transform;
            }
        }

        if (referenciaTerra == null)
        {
            MarcadorSuperficieMapa marcadorTerra = RegistroSuperficieMapa.EncontrarPrimeiro(TipoSuperficieMapa.Chao);
            if (marcadorTerra != null)
            {
                referenciaTerra = marcadorTerra.transform;
            }
            else
            {
                GameObject t = GameObject.Find("terra") ?? GameObject.Find("Terra");
                if (t != null) referenciaTerra = t.transform;
            }
        }

        if (referenciaCapitalInicial == null)
            referenciaCapitalInicial = referenciaTerra != null ? referenciaTerra : transform;

        if (referenciaAgua != null)
            nivelDoMar = referenciaAgua.position.y;
    }

    public void ReceberSinalizador(Vector3 posicao, bool ehAgua)
    {
        if (ehAgua)
        {
            nivelDoMar = posicao.y;
            if (referenciaAgua == null)
            {
                GameObject marcador = new GameObject("IA_Dominadora_Agua");
                marcador.transform.position = posicao;
                referenciaAgua = marcador.transform;
            }
        }
        else if (referenciaTerra == null)
        {
            GameObject marcador = new GameObject("IA_Dominadora_Terra");
            marcador.transform.position = posicao;
            referenciaTerra = marcador.transform;
        }
    }

    void CriarZonasIniciais()
    {
        zonas.Clear();
        Vector3 centro = ObterCentroBase();

        zonas.Add(new ZonaIA(TipoZona.Capital, centro, raioZonaCapital));
        zonas.Add(new ZonaIA(TipoZona.Economia, centro, raioZonaEconomica));
        zonas.Add(new ZonaIA(TipoZona.Militar, centro, raioZonaMilitar));
        zonas.Add(new ZonaIA(TipoZona.Defesa, centro, raioZonaDefensiva));
        zonas.Add(new ZonaIA(TipoZona.Aerea, centro, raioZonaAerea));

        Vector3 zonaNaval = ObterAncoraNavalSegura();
        if (zonaNaval != Vector3.zero)
            zonas.Add(new ZonaIA(TipoZona.Naval, zonaNaval, raioZonaNavalBusca));

        ReconstruirGridTatico(true);
    }

    void RevalidarZonas()
    {
        Vector3 centro = ObterCentroBase();
        foreach (var z in zonas)
        {
            if (z.tipo == TipoZona.Capital || z.tipo == TipoZona.Economia || 
                z.tipo == TipoZona.Militar || z.tipo == TipoZona.Defesa || z.tipo == TipoZona.Aerea)
            {
                z.centro = centro;
            }
            else if (z.tipo == TipoZona.Naval)
            {
                Vector3 naval = ObterAncoraNavalSegura();
                if (naval != Vector3.zero) z.centro = naval;
            }
        }
        ReconstruirGridTatico();
    }

    Vector3 ObterCentroBase()
    {
        if (TemPrefeitura())
        {
            // OTIMIZAÇÃO: loop manual — FirstOrDefault aloca enumerador no heap
            for (int _i = 0; _i < meusPredios.Count; _i++)
            {
                GameObject p = meusPredios[_i];
                if (p != null && EhCategoria(p.name, "prefeitura")) return p.transform.position;
            }
        }
        if (referenciaCapitalInicial != null) return referenciaCapitalInicial.position;
        if (referenciaTerra != null) return referenciaTerra.position;
        return transform.position;
    }

    Vector3 ObterCentroZona(TipoZona tipo)
    {
        // OTIMIZAÇÃO: loop manual — FirstOrDefault aloca enumerador
        for (int _i = 0; _i < zonas.Count; _i++)
            if (zonas[_i] != null && zonas[_i].tipo == tipo) return zonas[_i].centro;
        return ObterCentroBase();
    }

    float ObterRaioZona(TipoZona tipo)
    {
        // OTIMIZAÇÃO: loop manual — FirstOrDefault aloca enumerador
        for (int _i = 0; _i < zonas.Count; _i++)
            if (zonas[_i] != null && zonas[_i].tipo == tipo) return zonas[_i].raio;
        return 100f;
    }

    // =========================================================
    // TERRENO E VALIDAÃ‡ÃƒO (OTIMIZADOS COM NON-ALLOC)
    // =========================================================
    TipoTerreno DetectarTerreno(Vector3 ponto, out float altura)
    {
        ClassificacaoSuperficieMapa classificacaoMarcada;
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryClassify(ponto, out classificacaoMarcada, out alturaMarcada))
        {
            altura = alturaMarcada;
            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Agua || classificacaoMarcada == ClassificacaoSuperficieMapa.Costa)
            {
                return TipoTerreno.Agua;
            }

            return TipoTerreno.Terra;
        }

        float alturaTerra = ObterAlturaSolo(ponto);
        float alturaAgua = float.MinValue;
        bool achouAgua = false;

        // OTIMIZAÃ‡ÃƒO: Usando RaycastNonAlloc para nÃ£o gerar lixo na memÃ³ria.
        int hitCount = Physics.RaycastNonAlloc(
            new Vector3(ponto.x, 1200f, ponto.z),
            Vector3.down,
            bufferRaycast,
            2500f,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            var hit = bufferRaycast[i];
            if (hit.collider == null) continue;
            MarcadorSuperficieMapa marcador = hit.collider.GetComponentInParent<MarcadorSuperficieMapa>();
            if (marcador != null)
            {
                float alturaMarcador;
                if (!marcador.TrySampleSurfaceHeight(ponto, out alturaMarcador))
                {
                    continue;
                }

                if (marcador.TipoSuperficie == TipoSuperficieMapa.Agua)
                {
                    if (alturaMarcador > alturaAgua)
                    {
                        alturaAgua = alturaMarcador;
                        achouAgua = true;
                    }
                }
                else
                {
                    alturaTerra = alturaMarcador;
                }

                continue;
            }

            if (ColliderEhAgua(hit.collider))
            {
                if (hit.point.y > alturaAgua)
                {
                    alturaAgua = hit.point.y;
                    achouAgua = true;
                }
            }
        }

        if (achouAgua && alturaAgua >= alturaTerra - 0.15f)
        {
            altura = alturaAgua;
            return TipoTerreno.Agua;
        }

        altura = alturaTerra;
        return TipoTerreno.Terra;
    }

    float ObterAlturaSolo(Vector3 p)
    {
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryGetAltura(p, TipoSuperficieMapa.Chao, out alturaMarcada))
        {
            return alturaMarcada;
        }

        // OTIMIZAÃ‡ÃƒO: Usando RaycastNonAlloc
        int hitCount = Physics.RaycastNonAlloc(
            new Vector3(p.x, 1200f, p.z),
            Vector3.down,
            bufferRaycast,
            2500f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float maiorAltura = float.MinValue;
        bool achou = false;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = bufferRaycast[i];
            if (hit.collider == null) continue;

            MarcadorSuperficieMapa marcador = hit.collider.GetComponentInParent<MarcadorSuperficieMapa>();
            if (marcador != null)
            {
                if (marcador.TipoSuperficie == TipoSuperficieMapa.Agua)
                {
                    continue;
                }

                float alturaMarcador;
                if (marcador.TrySampleSurfaceHeight(p, out alturaMarcador) && alturaMarcador > maiorAltura)
                {
                    maiorAltura = alturaMarcador;
                    achou = true;
                }

                continue;
            }

            if (ColliderEhAgua(hit.collider)) continue;

            string nome = hit.collider.gameObject.name.ToLower();
            if (nome.Contains("bip001") || nome.Contains("bone")) continue;

            if (hit.collider.GetComponentInParent<IdentidadeUnidade>() != null) continue;

            if (hit.point.y > maiorAltura)
            {
                maiorAltura = hit.point.y;
                achou = true;
            }
        }

        return achou ? maiorAltura : 0f;
    }

    bool ColliderEhAgua(Collider col)
    {
        if (col == null) return false;

        MarcadorSuperficieMapa marcador = col.GetComponentInParent<MarcadorSuperficieMapa>();
        if (marcador != null)
        {
            return marcador.TipoSuperficie == TipoSuperficieMapa.Agua;
        }

        string nome = col.gameObject.name.ToLower();
        bool nomeIndicaAgua = nome == "agua" || nome.Contains("water") || nome.Contains("sea") || nome.Contains("mar") || nome.Contains("ocean");
        bool scriptOceano = col.GetComponent("OceanAdvanced") != null || col.GetComponentInParent<MonoBehaviour>()?.GetType().Name.Contains("Ocean") == true;
        
        int waterLayer = LayerMask.NameToLayer(nomeLayerAgua);
        bool layerAgua = waterLayer >= 0 && col.gameObject.layer == waterLayer;

        return nomeIndicaAgua || scriptOceano || layerAgua;
    }

    bool LocalOcupado(Vector3 pos, float raioNecessario, bool ignorarUnidadesMoveis = false)
    {
        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            float outroRaio = CalcularRaioSeguro(predio.name);
            if (Vector3.Distance(Plano(pos), Plano(predio.transform.position)) < raioNecessario + outroRaio)
                return true;
        }

        int totalHits = Physics.OverlapSphereNonAlloc(pos, raioNecessario * 0.9f, bufferOcupacao, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < totalHits; i++)
        {
            Collider h = bufferOcupacao[i];
            if (h == null) continue;
            if (ColliderEhAgua(h)) continue;

            string n = h.gameObject.name.ToLower();
            if (n == "terra" || n == "terrain") continue;
            if (h.GetComponent("SinalizadorIA") != null || h.GetComponentInParent<MarcadorSuperficieMapa>() != null) continue;

            IdentidadeUnidade id = h.GetComponentInParent<IdentidadeUnidade>();
            if (id != null)
            {
                if (ignorarUnidadesMoveis)
                {
                    NavMeshAgent nav = h.GetComponentInParent<NavMeshAgent>();
                    if (nav != null && nav.enabled) continue;
                }
                return true;
            }

            NavMeshObstacle obst = h.GetComponentInParent<NavMeshObstacle>();
            if (obst != null) return true;
        }

        return false;
    }

    bool PosicaoEmZonaCorreta(string chave, Vector3 pos)
    {
        List<ZonaIA> zonasDeBusca = ObterZonasDeBusca(chave);
        if (zonasDeBusca.Count == 0) return true;

        for (int i = 0; i < zonasDeBusca.Count; i++)
        {
            if (zonasDeBusca[i].Contem(pos)) return true;
        }
        return false;
    }

    bool PosicaoMuitoPertoDeRejeicao(Vector3 pos, float raio = 25f)
    {
        for (int i = 0; i < rejeicoesRecentes.Count; i++)
        {
            if (Vector3.Distance(Plano(pos), Plano(rejeicoesRecentes[i].pos)) <= raio)
                return true;
        }
        return false;
    }

    void RegistrarRejeicao(Vector3 pos, string motivo)
    {
        rejeicoesRecentes.Add(new RegistroRejeicao(pos, motivo));
        if (rejeicoesRecentes.Count > 180) rejeicoesRecentes.RemoveAt(0);

        if (!debugMostrarRejeicoes) return;

        if (string.IsNullOrEmpty(motivo)) motivo = "motivo desconhecido";
        if (!contagemRejeicoesPorMotivo.ContainsKey(motivo)) contagemRejeicoesPorMotivo[motivo] = 0;
        contagemRejeicoesPorMotivo[motivo]++;

        if (Time.time >= proximoResumoRejeicoes) EmitirResumoRejeicoes();
    }

    void LimparRejeicoesAntigas()
    {
        rejeicoesRecentes.RemoveAll(r => Time.time - r.tempo > 30f);
        var chavesRemover = new List<string>();
        foreach(var kvp in cooldownFalhaConstrucaoPorTipo)
        {
             if(Time.time >= kvp.Value) chavesRemover.Add(kvp.Key);
        }
        foreach(var key in chavesRemover) cooldownFalhaConstrucaoPorTipo.Remove(key);
    }

    void EmitirResumoRejeicoes(bool forcar = false)
    {
        if (!debugMostrarRejeicoes || contagemRejeicoesPorMotivo.Count == 0) return;
        if (!forcar && Time.time < proximoResumoRejeicoes) return;

        proximoResumoRejeicoes = Time.time + Mathf.Max(0.5f, intervaloResumoRejeicoes);

        string resumo = string.Join(", ",
            contagemRejeicoesPorMotivo
                .OrderByDescending(kvp => kvp.Value)
                .Take(Mathf.Max(1, maxMotivosResumoRejeicoes))
                .Select(kvp => kvp.Key + " x" + kvp.Value)
                .ToArray());

        Log("Rejeicoes recentes: " + resumo);
        contagemRejeicoesPorMotivo.Clear();
    }

    bool ValidarAcessibilidadeTerrestre(Vector3 pos)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(pos, out hit, 18f, NavMesh.AllAreas);
    }

    bool ValidarSaidaDaEstrutura(Vector3 origem, string chave)
    {
        if (EhCategoria(chave, "estaleiro") || EhCategoria(chave, "pier") || EhCategoria(chave, "plataforma"))
        {
            float raioNaval = Mathf.Max(24f, CalcularRaioSeguro(chave) + 10f);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                Vector3 teste = origem + new Vector3(Mathf.Cos(ang) * raioNaval, 0f, Mathf.Sin(ang) * raioNaval);
                teste.y = nivelDoMar;
                if (PosicaoNavalProfundaValida(teste, raioNaval) && !LocalOcupado(new Vector3(teste.x, nivelDoMar, teste.z), 10f, true))
                    return true;
            }
            return false;
        }

        if (EhAereo(chave)) return true;

        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f * Mathf.Deg2Rad;
            Vector3 teste = origem + new Vector3(Mathf.Cos(ang) * raioSaidaFabrica, 0f, Mathf.Sin(ang) * raioSaidaFabrica);
            float alt;
            TipoTerreno terr = DetectarTerreno(teste, out alt);
            if (terr != TipoTerreno.Terra) continue;

            teste.y = alt;
            if (ValidarAcessibilidadeTerrestre(teste) && !LocalOcupado(teste, 10f, true))
                return true;
        }

        return false;
    }

    float CalcularRaioSeguro(string chave)
    {
        string n = chave.ToLower();
        if (bancoDados.ContainsKey(n)) return bancoDados[n].raioSeguranca;

        if (n.Contains("aeroporto") || n.Contains("hangar")) return 180f;
        if (n.Contains("prefeitura") || n.Contains("complexo")) return 80f;
        if (n.Contains("estaleiro") || n.Contains("pier") || n.Contains("plataforma")) return 75f;
        if (n.Contains("fabrica") || n.Contains("construtor")) return 70f;
        if (n.Contains("quartel")) return 48f;
        if (n.Contains("refinaria")) return 48f;
        if (n.Contains("torreta")) return 30f;
        if (n.Contains("antiaerea")) return 34f;
        if (n.Contains("navio") || n.Contains("sub")) return 18f;

        return 35f;
    }

    // =========================================================
    // FUNDAÃ‡ÃƒO E EXPANSÃƒO (Intacto)
    // =========================================================
    bool FundarCapital()
    {
        if (!biblioteca.ContainsKey("prefeitura")) return false;

        Vector3 basePos = ObterCentroBase();
        float alt;
        TipoTerreno terr = DetectarTerreno(basePos, out alt);

        if (terr != TipoTerreno.Terra && referenciaTerra != null)
        {
            basePos = referenciaTerra.position;
            terr = DetectarTerreno(basePos, out alt);
        }

        if (terr != TipoTerreno.Terra) return false;
        basePos.y = alt;

        if (LocalOcupado(basePos, CalcularRaioSeguro("prefeitura"))) return false;
        if (!ValidarAcessibilidadeTerrestre(basePos)) return false;

        SpawnarObjeto(ObterPrefab("prefeitura"), basePos, "prefeitura");
        prefeituraPronta = true;
        CriarZonasIniciais();
        Log("Capital fundada.");
        return true;
    }

    bool GerenciarExpansaoBase()
    {
        if (dinheiroIA < 300f) return false;

        int quarteis = Contar("quartel");
        int fabricas = Contar("fabrica");
        int refinarias = Contar("refinaria");
        int aeroportos = Contar("aeroporto");
        int estaleiros = Contar("estaleiro");
        int piers = Contar("pier");
        int plataformas = Contar("plataforma");
        int defesas = Contar("torreta");
        int antiAereas = Contar("antiaerea");

        if (BaseEstaSaturada() && dinheiroIA > 2200f)
            if (TentarAbrirZonaExpansao()) return true;

        if (quarteis == 0 && biblioteca.ContainsKey("quartel"))
            if (TentarConstruir("quartel", 300f)) return true;

        if (refinarias == 0 && biblioteca.ContainsKey("refinaria"))
            if (TentarConstruir("refinaria", 500f)) return true;

        if (defesas < 1 && biblioteca.ContainsKey("torreta"))
            if (TentarConstruir("torreta", 400f)) return true;

        if (antiAereas < 2 && dinheiroIA > 800f && biblioteca.ContainsKey("antiaerea"))
            if (TentarConstruir("antiaerea", 800f)) return true;

        if (fabricas == 0 && biblioteca.ContainsKey("fabrica"))
            if (TentarConstruir("fabrica", 800f)) return true;

        if (defesas < 3 && biblioteca.ContainsKey("torreta"))
            if (TentarConstruir("torreta", 400f)) return true;

        if (dinheiroIA > 1000f)
        {
            if (estaleiros == 0 && biblioteca.ContainsKey("estaleiro"))
                if (TentarConstruir("estaleiro", 1500f)) return true;

            if (piers == 0 && biblioteca.ContainsKey("pier"))
                if (TentarConstruir("pier", 1000f)) return true;

            bool baseNavalPronta = (estaleiros > 0 || !biblioteca.ContainsKey("estaleiro")) && (piers > 0 || !biblioteca.ContainsKey("pier"));
            if (baseNavalPronta && plataformas == 0 && biblioteca.ContainsKey("plataforma"))
                if (TentarConstruir("plataforma", 2000f)) return true;
        }

        if (aeroportos == 0 && dinheiroIA > 2500f && biblioteca.ContainsKey("aeroporto"))
            if (TentarConstruir("aeroporto", 2500f)) return true;

        if (antiAereas < 4 && (forcaInimigaAerea > 0 || dinheiroIA > 3000f) && biblioteca.ContainsKey("antiaerea"))
            if (TentarConstruir("antiaerea", 800f)) return true;

        if (refinarias < 2 && dinheiroIA > 1600f && biblioteca.ContainsKey("refinaria"))
            if (TentarConstruir("refinaria", 600f)) return true;

        if (fabricas < 2 && dinheiroIA > 2500f && biblioteca.ContainsKey("fabrica"))
            if (TentarConstruir("fabrica", 1000f)) return true;

        if (defesas < 6 && dinheiroIA > 1000f && biblioteca.ContainsKey("torreta"))
            if (TentarConstruir("torreta", 400f)) return true;

        return false;
    }

    bool TentarConstruir(string chave, float custo)
    {
        chave = NormalizarChave(chave);
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;
        if (!PodeTentarConstrucao(chave)) return false;

        debugUltimosCandidatosValidos.Clear();
        debugUltimosCandidatosInvalidos.Clear();

        CandidatoConstrucao melhor = EncontrarMelhorPosicaoConstrucao(chave);
        if (melhor == null || !melhor.valido)
        {
            RegistrarFalhaConstrucao(chave);
            EmitirResumoRejeicoes(true);
            return false;
        }

        GameObject prefab = ObterPrefab(chave);
        if (prefab == null)
        {
            RegistrarFalhaConstrucao(chave);
            return false;
        }

        dinheiroIA -= custo;
        SpawnarObjeto(prefab, melhor.pos, chave);
        cooldownFalhaConstrucaoPorTipo.Remove(chave);
        EmitirResumoRejeicoes(true);

        Log($"ConstruÃ­do: {chave} em {melhor.pos}");
        return true;
    }

    CandidatoConstrucao EncontrarMelhorPosicaoConstrucao(string chave)
    {
        float raioObj = CalcularRaioSeguro(chave);
        List<ZonaIA> zonasDeBusca = ObterZonasDeBusca(chave);
        CandidatoConstrucao melhor = null;
        int avaliados = 0;
        int limiteAvaliacao = Mathf.Max(24, maxCandidatosPorBusca);

        if (zonasDeBusca.Count == 0)
            zonasDeBusca.Add(new ZonaIA(ObterZonaPreferida(chave), ObterCentroBase(), raioZonaEconomica));

        foreach (Vector3 posGrid in ColetarCandidatosDoGrid(zonasDeBusca, chave))
        {
            CandidatoConstrucao cand = AvaliarPosicaoConstrucao(chave, posGrid, raioObj);
            avaliados++;
            RegistrarCandidatoDebug(cand);
            AtualizarMelhorCandidato(ref melhor, cand);
            if (DeveEncerrarBuscaConstrucao(melhor, avaliados, limiteAvaliacao)) return melhor;
        }

        int aneis = EhNaval(chave) ? 8 : 10;
        int particoesBase = EhNaval(chave) ? 12 : 14;

        foreach (var zona in zonasDeBusca)
        {
            Vector3 centro = zona.centro;
            float raioZona = Mathf.Max(zona.raio, 25f);

            for (int a = 0; a < aneis; a++)
            {
                float r = Mathf.Lerp(20f, raioZona + raioBuscaConstrucaoExtra, a / (float)(aneis - 1));
                int particoes = Mathf.Min(particoesBase + a * 2, 48);

                for (int i = 0; i < particoes; i++)
                {
                    float ang = (360f / particoes) * i * Mathf.Deg2Rad;
                    Vector3 pos = centro + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

                    if (EhNaval(chave))
                    {
                        Vector3 ancoraNaval = ObterAncoraNavalSegura();
                        pos = ancoraNaval + new Vector3(Mathf.Cos(ang) * r * 0.5f, 0f, Mathf.Sin(ang) * r * 0.5f);
                    }

                    CandidatoConstrucao cand = AvaliarPosicaoConstrucao(chave, pos, raioObj);
                    avaliados++;
                    RegistrarCandidatoDebug(cand);
                    AtualizarMelhorCandidato(ref melhor, cand);

                    if (DeveEncerrarBuscaConstrucao(melhor, avaliados, limiteAvaliacao)) return melhor;
                }
                if (avaliados >= limiteAvaliacao) break;
            }
            if (avaliados >= limiteAvaliacao) break;
        }
        return melhor;
    }

    bool PodeTentarConstrucao(string chave) { return !cooldownFalhaConstrucaoPorTipo.ContainsKey(chave) || Time.time >= cooldownFalhaConstrucaoPorTipo[chave]; }
    void RegistrarFalhaConstrucao(string chave) { cooldownFalhaConstrucaoPorTipo[chave] = Time.time + Mathf.Max(0.5f, cooldownFalhaConstrucao); }
    
    void AtualizarMelhorCandidato(ref CandidatoConstrucao melhor, CandidatoConstrucao cand)
    {
        if (cand != null && cand.valido && (melhor == null || cand.score > melhor.score)) melhor = cand;
    }

    void RegistrarCandidatoDebug(CandidatoConstrucao cand)
    {
        if (cand == null) return;
        List<Vector3> destino = cand.valido ? debugUltimosCandidatosValidos : debugUltimosCandidatosInvalidos;
        destino.Add(cand.pos);
        if (destino.Count > 64) destino.RemoveAt(0);
    }

    bool DeveEncerrarBuscaConstrucao(CandidatoConstrucao melhor, int avaliados, int limiteAvaliacao)
    {
        if (avaliados >= limiteAvaliacao) return true;
        return melhor != null && avaliados >= 18 && melhor.score >= 1400f;
    }

    IEnumerable<Vector3> ColetarCandidatosDoGrid(List<ZonaIA> zonasDeBusca, string chave)
    {
        if (!usarGridTatico || gridTatico.Count == 0 || maxCandidatosDoGrid <= 0) yield break;

        string chaveNormalizada = NormalizarChave(chave);
        TipoTerreno terrenoDesejado = EhNaval(chaveNormalizada) ? TipoTerreno.Agua : TipoTerreno.Terra;
        float distanciaIdeal = bancoDados.ContainsKey(chaveNormalizada) ? bancoDados[chaveNormalizada].distanciaIdeal : 0f;

        List<CelulaTatica> melhoresCelulas = gridTatico.Values
            .Where(c => c != null && c.terreno == terrenoDesejado && !c.ocupada)
            .Where(c => zonasDeBusca.Any(z => z.Contem(c.posicao)))
            .OrderByDescending(c => ScoreCelulaParaConstrucao(c, chaveNormalizada, distanciaIdeal))
            .Take(Mathf.Max(1, maxCandidatosDoGrid))
            .ToList();

        for (int i = 0; i < melhoresCelulas.Count; i++) yield return melhoresCelulas[i].posicao;
    }

    float ScoreCelulaParaConstrucao(CelulaTatica celula, string chave, float distanciaIdeal)
    {
        if (celula == null) return float.MinValue;
        float score = 0f;
        score -= celula.ameaca * 4f;
        score -= Mathf.Abs(celula.distanciaBase - distanciaIdeal) * 0.45f;
        score -= celula.distanciaRecursos * (EhCategoria(chave, "refinaria") ? 0.06f : 0.01f);
        score += EhNaval(chave) ? celula.distanciaCosta * 1.8f : 0f;
        score += celula.zona == ObterZonaPreferida(chave) ? 120f : 0f;
        score += celula.zona == TipoZona.Expansao ? 40f : 0f;
        return score;
    }

    CandidatoConstrucao AvaliarPosicaoConstrucao(string chave, Vector3 pos, float raioObj)
    {
        CandidatoConstrucao cand = new CandidatoConstrucao(pos);
        float altura;
        TipoTerreno terr = DetectarTerreno(pos, out altura);
        pos.y = EhNaval(chave) ? nivelDoMar : altura;
        cand.pos = pos;

        if (PosicaoMuitoPertoDeRejeicao(pos))
        {
            cand.valido = false;
            cand.motivoRejeicao = "regiao rejeitada recentemente";
            return cand;
        }

        if (!ValidarPipelineConstrucao(chave, cand.pos, terr, raioObj, out string motivo))
        {
            cand.valido = false;
            cand.motivoRejeicao = motivo;
            RegistrarRejeicao(pos, cand.motivoRejeicao);
            return cand;
        }

        cand.valido = true;
        cand.score = CalcularScoreConstrucao(chave, pos);
        return cand;
    }

    float CalcularScoreConstrucao(string chave, Vector3 pos)
    {
        Vector3 centroBase = ObterCentroBase();
        float distBase = Vector3.Distance(Plano(pos), Plano(centroBase));
        float score = 1200f;
        CelulaTatica celula = ObterCelulaTatica(pos);

        TipoZona zona = ObterZonaPreferida(chave);
        Vector3 centroZona = ObterCentroZona(zona);
        float distZona = Vector3.Distance(Plano(pos), Plano(centroZona));

        score -= distZona * 1.4f;

        if (EhCategoria(chave, "prefeitura")) score -= distBase * 4f;
        if (EhCategoria(chave, "refinaria")) score -= Mathf.Abs(distBase - 110f) * 2f;
        if (EhCategoria(chave, "quartel")) score -= Mathf.Abs(distBase - 130f) * 1.6f;
        if (EhCategoria(chave, "fabrica")) score -= Mathf.Abs(distBase - 170f) * 1.8f;
        if (EhCategoria(chave, "torreta") || EhCategoria(chave, "antiaerea")) score -= Mathf.Abs(distBase - 230f) * 1.2f;
        
        if (EhCategoria(chave, "aeroporto"))
        {
            score -= Mathf.Abs(distBase - 300f) * 1.2f;
            score += DistanciaParaPredioMaisProximo(pos) * 0.25f;
            score += Mathf.Clamp(DistanciaParaImovelMaisProximo(pos), 0f, 220f) * 0.45f;
        }

        if (EhNaval(chave))
        {
            float distanciaCosta = celula != null ? celula.distanciaCosta : DistanciaAteTerraMaisProxima(pos, 250f);
            score += Mathf.Clamp(distanciaCosta, 0f, 120f) * 5f;
        }

        string normalChave = NormalizarChave(chave);
        if (bancoDados.ContainsKey(normalChave))
        {
            score += bancoDados[normalChave].valorEstrategico;
            score += bancoDados[normalChave].prioridade * 8f;
        }

        if (celula != null)
        {
            if (celula.ocupada) score -= 500f;
            score -= celula.ameaca * 2.5f;
            score -= celula.distanciaRecursos * (EhCategoria(chave, "refinaria") ? 0.15f : 0.02f);
            if (EhCategoria(chave, "aeroporto")) score += celula.distanciaCosta * 0.1f;
        }

        score += EspacamentoLivreBonus(pos);
        if (celula == null) score -= PenalidadePorAmeaca(pos);

        return score;
    }

    float DistanciaParaPredioMaisProximo(Vector3 pos)
    {
        float menor = float.MaxValue;
        foreach (var p in meusPredios)
        {
            if (p == null) continue;
            float d = Vector3.Distance(Plano(pos), Plano(p.transform.position));
            if (d < menor) menor = d;
        }
        return menor == float.MaxValue ? 999f : menor;
    }

    float DistanciaParaImovelMaisProximo(Vector3 pos)
    {
        float menor = float.MaxValue;
        RegistroEntidadesJogo.FillImoveis(bufferImoveisRegistrados);
        for (int i = 0; i < bufferImoveisRegistrados.Count; i++)
        {
            Imovel imovel = bufferImoveisRegistrados[i];
            if (imovel == null) continue;

            float d = Vector3.Distance(Plano(pos), Plano(imovel.transform.position));
            if (d < menor) menor = d;
        }
        return menor == float.MaxValue ? 9999f : menor;
    }

    float DistanciaAteTerraMaisProxima(Vector3 origem, float maxBusca)
    {
        for (float r = 10f; r <= maxBusca; r += 10f)
        {
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 22.5f * Mathf.Deg2Rad;
                Vector3 teste = origem + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float alt;
                if (DetectarTerreno(teste, out alt) == TipoTerreno.Terra) return r;
            }
        }
        return maxBusca;
    }

    float EspacamentoLivreBonus(Vector3 pos) { return Mathf.Clamp(DistanciaParaPredioMaisProximo(pos), 0f, 140f) * 0.9f; }

    float PenalidadePorAmeaca(Vector3 pos)
    {
        float penalidade = 0f;
        int totalHits = Physics.OverlapSphereNonAlloc(pos, 180f, bufferAmeaca, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < totalHits; i++)
        {
            Collider h = bufferAmeaca[i];
            if (h == null) continue;
            IdentidadeUnidade id = h.GetComponentInParent<IdentidadeUnidade>();
            if (id == null || id.teamID == teamID) continue;
            penalidade += 25f;
        }
        return penalidade;
    }

    // =========================================================
    // PRODUÃ‡ÃƒO (Intacto - Pier e Aeroporto preservados)
    // =========================================================
    void GerenciarProducaoTropas()
    {
        int totalTropas = 0;
        for (int i = 0; i < minhasTropas.Count; i++) if(minhasTropas[i] != null) totalTropas++;
        for (int i = 0; i < meusTransportes.Count; i++) if(meusTransportes[i] != null) totalTropas++;
        for (int i = 0; i < meusNavios.Count; i++) if(meusNavios[i] != null) totalTropas++;

        if (totalTropas >= maxTropasTotais)
        {
            ultimoPedidosProduzidos = 0;
            return;
        }

        List<PedidoProducao> pedidos = MontarPedidosProducao().Where(p => p.score > 0f).OrderByDescending(p => p.score).ToList();

        int produzidas = 0;
        foreach (var pedido in pedidos)
        {
            if (produzidas >= maxProducoesPorCiclo) break;

            bool sucesso = pedido.chave == "caca"
                ? TreinarAviao(pedido.chave, pedido.custo)
                : TreinarTropa(pedido.chave, pedido.custo, pedido.voa, pedido.naval);

            if (sucesso) produzidas++;
        }

        ultimoPedidosProduzidos = produzidas;
    }

    bool TreinarAviao(string chave, float custo)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;

        // OTIMIZAÇÃO: loop manual em vez de FirstOrDefault (sem alocação de enumerador)
        GameObject aeroporto = null;
        for (int _i = 0; _i < meusPredios.Count; _i++)
            if (meusPredios[_i] != null && EhCategoria(meusPredios[_i].name, "aeroporto")) { aeroporto = meusPredios[_i]; break; }
        if (aeroporto == null || !PodeUsarEstruturaParaProducao(aeroporto)) return false;

        GerenciadorAeroporto aero = aeroporto.GetComponent<GerenciadorAeroporto>();
        if (aero == null) return false;

        GameObject prefab = ObterPrefab(chave);
        if (prefab == null) return false;

        dinheiroIA -= custo;
        aero.ComprarAviao(prefab);
        RegistrarCooldownEstrutura(aeroporto, chave);
        return true;
    }

    int ContarAvioes()
    {
        return avioesAliadosConhecidos;
    }

    bool TreinarTropa(string chave, float custo, bool voa = false, bool naval = false)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;
        if (naval) return TentarProduzirNaval(chave, custo);

        GameObject estrutura = EscolherEstruturaDeProducao(chave, voa);
        if (estrutura == null || !PodeUsarEstruturaParaProducao(estrutura)) return false;

        GameObject prefab = ObterPrefab(chave);
        if (prefab == null) return false;

        if (voa && chave == "caca")
        {
            GerenciadorAeroporto aeroporto = estrutura.GetComponent<GerenciadorAeroporto>();
            if (aeroporto != null)
            {
                dinheiroIA -= custo;
                aeroporto.ComprarAviao(prefab);
                RegistrarCooldownEstrutura(estrutura, chave);
                return true;
            }
        }

        Vector3 spawn;
        if (!ObterSpawnTerrestreOuAereo(estrutura, voa, out spawn)) return false;

        dinheiroIA -= custo;
        GameObject nova = null;
        Fabrica fab = estrutura.GetComponent<Fabrica>();

        if (fab != null)
        {
            nova = fab.ProduzirUnidade(prefab);
            if (nova != null)
            {
                nova.transform.position = spawn;
                NavMeshAgent ag = nova.GetComponent<NavMeshAgent>();
                if (ag != null && ag.enabled) ag.Warp(spawn);
            }
        }
        else
        {
            nova = Instantiate(prefab, spawn, Quaternion.identity);
            nova.name = chave;
            ConfigurarObjeto(nova, false);
        }

        if (nova == null) return false;

        RegistrarCooldownEstrutura(estrutura, chave);

        if (chave == "transporte" || chave == "transporte_aereo") meusTransportes.Add(nova);
        else minhasTropas.Add(nova);

        Vector3 rally = ObterRallyPoint(voa);
        if (fab != null) StartCoroutine(MoverComAtraso(nova, rally, 1.5f));
        else Mover(nova, rally);

        return true;
    }

    bool TentarProduzirNaval(string chave, float custo)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;

        GameObject estruturaNaval = EscolherEstruturaNaval();
        if (estruturaNaval == null || !PodeUsarEstruturaParaProducao(estruturaNaval)) return false;

        GameObject prefab = ObterPrefab(chave);
        if (prefab == null) return false;

        Vector3 spawnAgua;
        if (!ObterSpawnNavalSeguro(estruturaNaval, chave, out spawnAgua)) return false;

        dinheiroIA -= custo;
        GameObject nova = null;
        Fabrica fab = estruturaNaval.GetComponent<Fabrica>();

        if (fab != null)
        {
            nova = fab.ProduzirUnidade(prefab);
            if (nova != null)
            {
                nova.transform.position = spawnAgua;
                NavMeshAgent ag = nova.GetComponent<NavMeshAgent>();
                if (ag != null && ag.enabled) ag.Warp(spawnAgua);
            }
        }
        else
        {
            nova = Instantiate(prefab, spawnAgua, Quaternion.identity);
            nova.name = chave;
            ConfigurarObjeto(nova, false);
        }

        if (nova == null) return false;

        float raioNavalFinal = Mathf.Max(24f, CalcularRaioSeguro(chave) + 12f);
        if (!PosicaoNavalProfundaValida(nova.transform.position, raioNavalFinal))
        {
            Vector3 correcao = EncontrarAguaProfundaMaisProxima(nova.transform.position, raioNavalFinal);
            if (correcao == Vector3.zero)
            {
                Destroy(nova);
                dinheiroIA += custo;
                return false;
            }
            nova.transform.position = correcao;
            NavMeshAgent agenteNaval = nova.GetComponent<NavMeshAgent>();
            if (agenteNaval != null && agenteNaval.enabled) agenteNaval.Warp(correcao);
        }

        RegistrarCooldownEstrutura(estruturaNaval, chave);
        meusNavios.Add(nova);
        return true;
    }

    GameObject EscolherEstruturaDeProducao(string chave, bool voa)
    {
        // OTIMIZAÇÃO: todos os FirstOrDefault substituídos por loops manuais (zero heap alloc)
        if (voa)
        {
            if (chave == "caca")
            {
                for (int _i = 0; _i < meusNavios.Count; _i++)
                    if (meusNavios[_i] != null && EhPortaAvioes(meusNavios[_i])) return meusNavios[_i];
            }
            for (int _i = 0; _i < meusPredios.Count; _i++)
                if (meusPredios[_i] != null && EhCategoria(meusPredios[_i].name, "aeroporto")) return meusPredios[_i];
        }

        for (int _i = 0; _i < meusPredios.Count; _i++)
            if (meusPredios[_i] != null && EhCategoria(meusPredios[_i].name, "fabrica")) return meusPredios[_i];
        for (int _i = 0; _i < meusPredios.Count; _i++)
            if (meusPredios[_i] != null && EhCategoria(meusPredios[_i].name, "quartel")) return meusPredios[_i];
        return null;
    }

    GameObject EscolherEstruturaNaval()
    {
        // OTIMIZAÇÃO: loop manual — sem alocação de enumerador LINQ
        for (int _i = 0; _i < meusPredios.Count; _i++)
        {
            if (meusPredios[_i] == null) continue;
            string _n = meusPredios[_i].name;
            if (EhCategoria(_n, "estaleiro") || EhCategoria(_n, "pier") || EhCategoria(_n, "plataforma"))
                return meusPredios[_i];
        }
        return null;
    }

    bool ObterSpawnTerrestreOuAereo(GameObject origem, bool voa, out Vector3 spawn)
    {
        Vector3 baseSpawn = origem != null ? origem.transform.position : ObterCentroBase();
        for (int i = 0; i < 16; i++)
        {
            float ang = i * 22.5f * Mathf.Deg2Rad;
            Vector3 teste = baseSpawn + new Vector3(Mathf.Cos(ang) * raioSaidaFabrica, 0f, Mathf.Sin(ang) * raioSaidaFabrica);

            float alt;
            TipoTerreno terr = DetectarTerreno(teste, out alt);
            if (terr != TipoTerreno.Terra) continue;

            teste.y = alt;
            if (!LocalOcupado(teste, voa ? 8f : 10f, true))
            {
                if (voa) { teste.y += alturaAereaSpawn; spawn = teste; return true; }
                NavMeshHit hit;
                if (NavMesh.SamplePosition(teste, out hit, 18f, NavMesh.AllAreas)) { spawn = hit.position; return true; }
            }
        }
        spawn = Vector3.zero;
        return false;
    }

    bool ObterSpawnNavalSeguro(GameObject origem, string chaveNaval, out Vector3 spawn)
    {
        Vector3 centro = ObterAncoraNavalSegura();
        if (centro == Vector3.zero && origem != null) centro = origem.transform.position;
        float raioNaval = Mathf.Max(24f, CalcularRaioSeguro(chaveNaval) + 12f);
        float[] multiplicadores = new float[] { 1f, 1.45f, 2.1f };

        for (int m = 0; m < multiplicadores.Length; m++)
        {
            float raioBusca = raioNaval * multiplicadores[m];
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = centro + new Vector3(Mathf.Cos(ang) * raioBusca, 0f, Mathf.Sin(ang) * raioBusca);
                teste.y = nivelDoMar;
                if (PosicaoNavalProfundaValida(teste, raioNaval) && !LocalOcupado(teste, 12f, true))
                {
                    spawn = teste;
                    return true;
                }
            }
        }
        spawn = Vector3.zero;
        return false;
    }

    IEnumerator MoverComAtraso(GameObject unidade, Vector3 destino, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (unidade != null) Mover(unidade, destino);
    }

    Vector3 ObterRallyPoint(bool voa)
    {
        Vector3[] pontos = ObterPontosFronteira();
        Vector3 rally = pontos[Random.Range(0, pontos.Length)] + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f));

        float alt;
        DetectarTerreno(rally, out alt);
        rally.y = alt + (voa ? alturaAereaSpawn : 0f);
        return rally;
    }

    // =========================================================
    // TÃTICA (OTIMIZADO - Sem Lixo de MemÃ³ria)
    // =========================================================
    void AtualizarReconhecimentoGlobal(bool forcar = false)
    {
        if (!forcar && Time.time < proximoReescaneamento) return;

        float inicio = Time.realtimeSinceStartup;
        forcaInimigaAerea = 0;
        avioesAliadosConhecidos = 0;
        inimigosConhecidos.Clear();
        basesInimigasConhecidas.Clear();
        economiasInimigasConhecidas.Clear();

        RegistroEntidadesJogo.FillUnidades(bufferUnidadesRegistradas);
        for (int i = 0; i < bufferUnidadesRegistradas.Count; i++)
        {
            IdentidadeUnidade u = bufferUnidadesRegistradas[i];
            if (u == null) continue;

            string fn = NormalizarChave(u.name);
            bool ehAereo = u.GetComponent<ControleAviao>() != null || fn == "helicoptero" || fn == "caca" || fn == "transporte_aereo" || u.name.IndexOf("aviao", System.StringComparison.OrdinalIgnoreCase) >= 0 || u.name.IndexOf("jet", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (u.teamID == teamID)
            {
                if (ehAereo) avioesAliadosConhecidos++;
                continue;
            }

            inimigosConhecidos.Add(u);

            if (fn == "prefeitura" || u.name.IndexOf("governo", System.StringComparison.OrdinalIgnoreCase) >= 0 || u.name.IndexOf("complexo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                basesInimigasConhecidas.Add(u.transform);

            if (fn == "refinaria" || u.name.IndexOf("armazem", System.StringComparison.OrdinalIgnoreCase) >= 0)
                economiasInimigasConhecidas.Add(u.transform);

            if (ehAereo) forcaInimigaAerea++;
        }

        ultimoInimigosReconhecidos = inimigosConhecidos.Count;
        RegistrarTempoModulo(ref custoReconhecimentoMs, ref picoReconhecimentoMs, inicio);
        AtualizarResumoPerformance();
    }

    void AnalisarOponente()
    {
        alvoJogadorBase = PrimeiroTransformValido(basesInimigasConhecidas);
        alvoJogadorEconomia = PrimeiroTransformValido(economiasInimigasConhecidas);

        if (alvoJogadorBase == null)
        {
            IdentidadeUnidade inimigo = PrimeiroInimigoConhecido();
            if (inimigo != null) alvoJogadorBase = inimigo.transform;
        }

        if (alvoJogadorEconomia == null) alvoJogadorEconomia = alvoJogadorBase;
    }

    void DefinirPosturaGlobal()
    {
        if (InimigoNoPortao())
        {
            estadoAtual = EstadoIA.DefesaDesesperada;
            momentoFimPaz = 0f;
            return;
        }

        if (Time.time < momentoFimPaz)
        {
            estadoAtual = EstadoIA.Reagrupando;
            return;
        }

        int soldados = Contar("soldado");
        int tanques = Contar("tanque");

        bool prontoParaAtacar = soldados >= Mathf.RoundToInt(metaSoldados * 0.8f) && tanques >= Mathf.RoundToInt(metaTanques * 0.7f);
        estadoAtual = prontoParaAtacar ? EstadoIA.GuerraTotal : EstadoIA.Reagrupando;
    }

    bool InimigoNoPortao()
    {
        int totalHits = Physics.OverlapSphereNonAlloc(ObterCentroBase(), 150f, bufferAmeaca, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < totalHits; i++)
        {
            Collider h = bufferAmeaca[i];
            if (h == null) continue;
            IdentidadeUnidade id = h.GetComponentInParent<IdentidadeUnidade>();
            if (id == null || id.teamID == teamID) continue;
            if (h.name.ToLower().Contains("aviao")) continue;
            return true;
        }
        return false;
    }

    int LancarOfensivaMassa()
    {
        if (alvoJogadorBase == null) return 0;

        int processados = 0;
        int tentativas = 0;
        int total = minhasTropas.Count;

        while (tentativas < total && processados < Mathf.Max(1, maxTropasProcessadasPorCiclo))
        {
            if (cursorTropasTaticas >= minhasTropas.Count) cursorTropasTaticas = 0;
            GameObject t = minhasTropas[cursorTropasTaticas];
            int indiceAtual = cursorTropasTaticas;
            cursorTropasTaticas++;
            tentativas++;

            if (t == null || t.transform.parent != null) continue;

            bool aereo = EhObjetoAereo(t.name);
            Vector3 alvo = alvoJogadorBase.position;

            if (aereo && alvoJogadorEconomia != null) alvo = alvoJogadorEconomia.position;

            float deslocamentoX = Mathf.Sin(Time.time * 0.75f + indiceAtual * 0.73f) * 90f;
            float deslocamentoZ = Mathf.Cos(Time.time * 0.65f + indiceAtual * 0.51f) * 90f;
            Vector3 destino = alvo + new Vector3(deslocamentoX, 0f, deslocamentoZ);
            float alt;
            DetectarTerreno(destino, out alt);
            destino.y = alt + (aereo ? alturaAereaSpawn : 0f);

            Mover(t, destino);
            processados++;
        }

        ultimoLoteAvioes = ProcessarAvioesPatioOfensiva();
        return processados;
    }

    int ProcessarAvioesPatioOfensiva()
    {
        if (alvoJogadorBase == null) return 0;

        Vector3 alvoAereo = alvoJogadorEconomia != null ? alvoJogadorEconomia.position : alvoJogadorBase.position;
        int processados = 0;
        int inicio = Mathf.Max(0, cursorAvioesPatio);

        foreach (var predio in meusPredios)
        {
            if (processados >= Mathf.Max(1, maxAvioesPatioPorCiclo)) break;
            if (predio == null || !EhCategoria(predio.name, "aeroporto")) continue;

            var aero = predio.GetComponent<GerenciadorAeroporto>();
            if (aero == null) continue;

            if (aero.avioesNoPatio == null || aero.avioesNoPatio.Count == 0) continue;

            for (int offset = 0; offset < aero.avioesNoPatio.Count && processados < Mathf.Max(1, maxAvioesPatioPorCiclo); offset++)
            {
                int indice = (inicio + offset) % aero.avioesNoPatio.Count;
                ControleAviao av = aero.avioesNoPatio[indice];
                if (av != null && av.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    av.IniciarMissaoCompleta(alvoAereo);
                    processados++;
                }
            }
        }

        cursorAvioesPatio += Mathf.Max(1, processados);
        return processados;
    }

    int PatrulharFronteiras()
    {
        Vector3[] pontos = ObterPontosFronteira();
        int processados = 0;
        int tentativas = 0;
        int total = minhasTropas.Count;

        while (tentativas < total && processados < Mathf.Max(1, maxTropasProcessadasPorCiclo))
        {
            if (cursorTropasTaticas >= minhasTropas.Count) cursorTropasTaticas = 0;
            GameObject tropa = minhasTropas[cursorTropasTaticas];
            int indiceAtual = cursorTropasTaticas;
            cursorTropasTaticas++;
            tentativas++;

            if (tropa == null || tropa.transform.parent != null) continue;

            Vector3 destino = pontos[indiceAtual % pontos.Length] + new Vector3(Mathf.Cos(Time.time + indiceAtual) * 15f, 0f, Mathf.Sin(Time.time + indiceAtual) * 15f);
            bool voa = EhObjetoAereo(tropa.name);

            float alt;
            DetectarTerreno(destino, out alt);
            destino.y = alt + (voa ? alturaAereaSpawn : 0f);

            if (Vector3.Distance(tropa.transform.position, destino) > 20f) Mover(tropa, destino);
            processados++;
        }

        return processados;
    }

    int RecuarParaDefesa()
    {
        Vector3 centro = ObterCentroBase();
        int processados = 0;
        int tentativas = 0;
        int total = minhasTropas.Count;

        while (tentativas < total && processados < Mathf.Max(1, maxTropasProcessadasPorCiclo))
        {
            if (cursorTropasTaticas >= minhasTropas.Count) cursorTropasTaticas = 0;
            GameObject tropa = minhasTropas[cursorTropasTaticas];
            int indiceAtual = cursorTropasTaticas;
            cursorTropasTaticas++;
            tentativas++;

            if (tropa == null || tropa.transform.parent != null) continue;

            bool voa = EhObjetoAereo(tropa.name);
            Vector3 destino = centro + new Vector3(Mathf.Sin(Time.time + indiceAtual) * 35f, 0f, Mathf.Cos(Time.time + indiceAtual * 1.1f) * 35f);
            float alt;
            DetectarTerreno(destino, out alt);
            destino.y = alt + (voa ? alturaAereaSpawn : 0f);

            Mover(tropa, destino);
            processados++;
        }

        return processados;
    }

    Vector3[] ObterPontosFronteira()
    {
        Vector3 centro = ObterCentroBase();
        Vector3 frente = transform.forward;

        if (alvoJogadorBase != null)
        {
            frente = (alvoJogadorBase.position - centro).normalized;
            frente.y = 0f;
        }

        if (frente == Vector3.zero) frente = Vector3.forward;

        float dist = 110f;
        Vector3[] pts = new Vector3[3];
        pts[0] = centro + Quaternion.Euler(0f, -45f, 0f) * frente * dist;
        pts[1] = centro + frente * dist;
        pts[2] = centro + Quaternion.Euler(0f, 45f, 0f) * frente * dist;

        for (int i = 0; i < pts.Length; i++)
        {
            float alt;
            DetectarTerreno(pts[i], out alt);
            pts[i].y = alt;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(pts[i], out hit, 25f, NavMesh.AllAreas)) pts[i] = hit.position;
        }

        return pts;
    }

    // =========================================================
    // NAVAL (Intacto)
    // =========================================================
    Vector3 ObterAncoraNavalSegura()
    {
        if (referenciaAgua != null && referenciaCapitalInicial != null)
        {
            Vector3 offset = referenciaAgua.position - referenciaCapitalInicial.position;
            offset.y = 0;
            Vector3 cb = ObterCentroBase();
            Vector3 espelho = cb + new Vector3(-offset.x, 0, -offset.z);
            espelho.y = nivelDoMar;
            Vector3 trad = cb + offset;
            trad.y = nivelDoMar;
            if (DetectarTerreno(espelho, out _) == TipoTerreno.Agua) return espelho;
            if (DetectarTerreno(trad, out _) == TipoTerreno.Agua) return trad;
            return espelho;
        }

        if (referenciaAgua != null)
        {
            Vector3 ancora = referenciaAgua.position;
            ancora.y = nivelDoMar;
            Vector3 baseCentro = ObterCentroBase();
            Vector3 dir = (ancora - baseCentro).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;

            Vector3 segura = ancora + dir * distanciaNavalDaCosta * 0.35f;
            segura.y = nivelDoMar;
            if (PosicaoNavalProfundaValida(segura, 28f)) return segura;
        }

        Vector3 centroBase = ObterCentroBase();
        for (float r = 80f; r <= 2600f; r += 40f)
        {
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = centroBase + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (PosicaoNavalProfundaValida(teste, 28f))
                {
                    teste.y = nivelDoMar;
                    return teste;
                }
            }
        }
        return Vector3.zero;
    }

    PapelNavalIA ClassificarNavio(GameObject navio)
    {
        if (navio == null) return PapelNavalIA.Nenhum;

        string nome = navio.name.ToLower();
        if (nome.Contains("estaleiro")) return PapelNavalIA.Estaleiro;
        if (navio.GetComponent<NavioPetroleiro>() != null || nome.Contains("petrole")) return PapelNavalIA.Logistico;
        if (EhPortaAvioes(navio) || nome.Contains("sovereign")) return PapelNavalIA.Carrier;
        if (EhTransporteNaval(navio)) return PapelNavalIA.Transporte;
        if (EhSubmarino(navio) || navio.GetComponent<ControleSubmarino>() != null || nome.Contains("leviathan")) return PapelNavalIA.Submarino;
        if (navio.GetComponentInChildren<LancadorNaval>(true) != null || NomeContemTrecho(nome, "ironclad", "vindicator", "wall", "fortaleza", "sam"))
            return PapelNavalIA.EscoltaMissil;
        return PapelNavalIA.PatrulhaEscolta;
    }

    void MontarGrupoNaval(GrupoNavalIA grupo)
    {
        grupo.Limpar();

        for (int i = 0; i < meusNavios.Count; i++)
        {
            GameObject navio = meusNavios[i];
            if (navio == null) continue;

            switch (ClassificarNavio(navio))
            {
                case PapelNavalIA.Carrier: grupo.carriers.Add(navio); break;
                case PapelNavalIA.Transporte: grupo.transportes.Add(navio); break;
                case PapelNavalIA.EscoltaMissil: grupo.escoltasMissil.Add(navio); break;
                case PapelNavalIA.Submarino: grupo.submarinos.Add(navio); break;
                case PapelNavalIA.Logistico: grupo.logisticos.Add(navio); break;
                default: grupo.patrulhas.Add(navio); break;
            }
        }
    }

    float ObterAlcanceDeteccaoNaval(GameObject navio)
    {
        if (navio == null) return 180f;

        LancadorNaval lancador = navio.GetComponentInChildren<LancadorNaval>(true);
        if (lancador != null) return Mathf.Max(240f, lancador.alcanceRadar * 0.9f);
        if (navio.GetComponent<ControleSubmarino>() != null) return 360f;
        if (navio.GetComponent<ControladorNavioVigilante>() != null) return 260f;
        return 200f;
    }

    bool EhAlvoNavalAltoValor(Transform alvo)
    {
        if (alvo == null) return false;

        string nome = alvo.name.ToLower();
        if (NomeContemTrecho(nome, "carrier", "porta", "sovereign", "prefeitura", "complexo", "fortaleza", "antiaerea", "estaleiro"))
            return true;

        return alvoJogadorBase != null && Vector3.Distance(Plano(alvo.position), Plano(alvoJogadorBase.position)) <= 220f;
    }

    void AtualizarContatoNaval(GrupoNavalIA grupo)
    {
        Transform melhorAlvo = null;
        Vector3 melhorPosicao = Vector3.zero;
        float melhorScore = -1f;
        int contatos = 0;
        bool altoValor = false;

        for (int lista = 0; lista < 4; lista++)
        {
            List<GameObject> origem = lista == 0 ? grupo.escoltasMissil
                : lista == 1 ? grupo.patrulhas
                : lista == 2 ? grupo.submarinos
                : grupo.carriers;

            for (int i = 0; i < origem.Count; i++)
            {
                GameObject navio = origem[i];
                if (navio == null) continue;

                float alcance = ObterAlcanceDeteccaoNaval(navio);
                int totalHits = Physics.OverlapSphereNonAlloc(navio.transform.position, alcance, bufferContatoNaval, ~0, QueryTriggerInteraction.Collide);
                for (int hitIndex = 0; hitIndex < totalHits; hitIndex++)
                {
                    Collider hit = bufferContatoNaval[hitIndex];
                    if (hit == null) continue;

                    IdentidadeUnidade id = hit.GetComponentInParent<IdentidadeUnidade>();
                    if (id == null || id.teamID == teamID || id.teamID == 0) continue;
                    if (id.transform == navio.transform || id.transform.IsChildOf(navio.transform)) continue;

                    contatos++;
                    bool alvoImportante = EhAlvoNavalAltoValor(id.transform);
                    float distancia = Vector3.Distance(Plano(navio.transform.position), Plano(id.transform.position));
                    float score = (alvoImportante ? 450f : 0f) + Mathf.Max(0f, alcance - distancia);

                    if (score > melhorScore)
                    {
                        melhorScore = score;
                        melhorAlvo = id.transform;
                        melhorPosicao = id.transform.position;
                        altoValor = alvoImportante;
                    }
                }
            }
        }

        if (melhorAlvo != null)
        {
            contatoNavalAtual.alvo = melhorAlvo;
            contatoNavalAtual.posicao = melhorPosicao;
            contatoNavalAtual.ultimaDeteccao = Time.time;
            contatoNavalAtual.forcaEstimativa = Mathf.Max(1, contatos);
            contatoNavalAtual.altoValor = altoValor;
        }
        else if (!contatoNavalAtual.EstaAtivo())
        {
            contatoNavalAtual.Limpar();
        }
    }

    void DefinirModoLancadores(GameObject navio, LancadorNaval.ModoOperacao modo)
    {
        if (navio == null) return;

        LancadorNaval[] lancadores = navio.GetComponentsInChildren<LancadorNaval>(true);
        for (int i = 0; i < lancadores.Length; i++)
        {
            if (lancadores[i] != null)
                lancadores[i].DefinirModoIA(modo);
        }
    }

    void AtualizarLancadoresGrupo(GrupoNavalIA grupo, bool contatoAtivo)
    {
        LancadorNaval.ModoOperacao modoCombate = contatoAtivo ? LancadorNaval.ModoOperacao.Automatico : LancadorNaval.ModoOperacao.Passivo;

        for (int i = 0; i < grupo.escoltasMissil.Count; i++) DefinirModoLancadores(grupo.escoltasMissil[i], modoCombate);
        for (int i = 0; i < grupo.patrulhas.Count; i++) DefinirModoLancadores(grupo.patrulhas[i], modoCombate);
        for (int i = 0; i < grupo.transportes.Count; i++) DefinirModoLancadores(grupo.transportes[i], LancadorNaval.ModoOperacao.Passivo);
        for (int i = 0; i < grupo.carriers.Count; i++) DefinirModoLancadores(grupo.carriers[i], LancadorNaval.ModoOperacao.Passivo);
        for (int i = 0; i < grupo.logisticos.Count; i++) DefinirModoLancadores(grupo.logisticos[i], LancadorNaval.ModoOperacao.Passivo);
    }

    bool TemRotaTerrestreParaAlvoNaval()
    {
        if (alvoJogadorBase == null) return true;
        if (bufferCaminhoTerrestre == null) bufferCaminhoTerrestre = new NavMeshPath();

        NavMeshHit origem;
        NavMeshHit destino;
        if (!NavMesh.SamplePosition(ObterCentroBase(), out origem, 80f, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(alvoJogadorBase.position, out destino, 120f, NavMesh.AllAreas)) return false;
        if (!NavMesh.CalculatePath(origem.position, destino.position, NavMesh.AllAreas, bufferCaminhoTerrestre)) return false;

        return bufferCaminhoTerrestre.status == NavMeshPathStatus.PathComplete
            && bufferCaminhoTerrestre.corners != null
            && bufferCaminhoTerrestre.corners.Length > 1;
    }

    Vector3 ObterPontoReuniaoTerrestreNaval(Vector3 pontoCarga)
    {
        for (float raio = 30f; raio <= 220f; raio += 20f)
        {
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = pontoCarga + new Vector3(Mathf.Cos(ang) * raio, 0f, Mathf.Sin(ang) * raio);
                float alt;
                if (DetectarTerreno(teste, out alt) != TipoTerreno.Terra) continue;

                teste.y = alt;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(teste, out hit, 20f, NavMesh.AllAreas))
                    return hit.position;
            }
        }

        return ObterCentroBase();
    }

    int ReunirTropasParaInvasao(Vector3 pontoReuniao)
    {
        int prontas = 0;
        int convocadas = 0;

        for (int i = 0; i < minhasTropas.Count; i++)
        {
            GameObject tropa = minhasTropas[i];
            if (tropa == null || tropa.transform.parent != null) continue;
            if (EhObjetoAereo(tropa.name)) continue;

            string nome = tropa.name.ToLower();
            if (nome.Contains("transporte")) continue;

            float distancia = Vector3.Distance(Plano(tropa.transform.position), Plano(pontoReuniao));
            if (distancia <= 55f)
            {
                prontas++;
                continue;
            }

            if (convocadas >= 12 || distancia > 500f) continue;

            Vector3 destino = pontoReuniao + new Vector3(Mathf.Sin((i + 1) * 1.37f) * 18f, 0f, Mathf.Cos((i + 1) * 1.37f) * 18f);
            float alt;
            if (DetectarTerreno(destino, out alt) == TipoTerreno.Terra)
            {
                destino.y = alt;
                Mover(tropa, destino);
                convocadas++;
            }
        }

        return prontas;
    }

    bool EhHoverNaval(GameObject navio)
    {
        return navio != null && navio.GetComponent<HovercraftTransporte>() != null;
    }

    float ObterPercentualCargaNaval(GameObject navio)
    {
        if (navio == null) return 0f;

        HovercraftTransporte hover = navio.GetComponent<HovercraftTransporte>();
        if (hover != null) return hover.TemCarga() ? 1f : 0f;

        TransporteAnfibio anfibio = navio.GetComponent<TransporteAnfibio>();
        if (anfibio != null)
        {
            int unidades = anfibio.unidadesGuardadas != null ? anfibio.unidadesGuardadas.Count : 0;
            return Mathf.Clamp01(unidades / 6f);
        }

        return TransporteNavalTemCarga(navio) ? 1f : 0f;
    }

    int ContarTransportesComCargaMinima(GrupoNavalIA grupo)
    {
        int total = 0;
        for (int i = 0; i < grupo.transportes.Count; i++)
        {
            GameObject navio = grupo.transportes[i];
            if (navio == null) continue;

            float minimo = EhHoverNaval(navio) ? 0.2f : 0.6f;
            if (ObterPercentualCargaNaval(navio) >= minimo) total++;
        }
        return total;
    }

    int ContarUnidadesEmbarcadas(GrupoNavalIA grupo)
    {
        int total = 0;
        for (int i = 0; i < grupo.transportes.Count; i++)
        {
            GameObject navio = grupo.transportes[i];
            if (navio == null) continue;

            HovercraftTransporte hover = navio.GetComponent<HovercraftTransporte>();
            if (hover != null)
            {
                if (hover.TemCarga()) total += 4;
                continue;
            }

            TransporteAnfibio anfibio = navio.GetComponent<TransporteAnfibio>();
            if (anfibio != null && anfibio.unidadesGuardadas != null)
            {
                total += anfibio.unidadesGuardadas.Count;
            }
        }
        return total;
    }

    int ObterEscoltaMinimaComboio(GrupoNavalIA grupo)
    {
        int grandes = 0;
        int hovers = 0;

        for (int i = 0; i < grupo.transportes.Count; i++)
        {
            if (EhHoverNaval(grupo.transportes[i])) hovers++;
            else grandes++;
        }

        return grandes * 2 + hovers;
    }

    bool PodePartirComboio(GrupoNavalIA grupo)
    {
        if (grupo.TotalTransportes() == 0) return false;

        int escoltaMinima = ObterEscoltaMinimaComboio(grupo);
        int embarcadas = ContarUnidadesEmbarcadas(grupo);
        int transportesProntos = ContarTransportesComCargaMinima(grupo);
        float tempoPreparacao = inicioPreparacaoAnfibia < 0f ? 0f : Time.time - inicioPreparacaoAnfibia;

        if (tempoPreparacao >= 45f)
            return embarcadas >= 8 && grupo.TotalEscoltas() >= escoltaMinima && transportesProntos > 0;

        return embarcadas >= 8 && grupo.TotalEscoltas() >= escoltaMinima && transportesProntos >= grupo.TotalTransportes();
    }

    Vector3 CalcularCentroTransportes(GrupoNavalIA grupo, Vector3 fallback, bool preferirCarregados)
    {
        Vector3 soma = Vector3.zero;
        int total = 0;

        for (int i = 0; i < grupo.transportes.Count; i++)
        {
            GameObject navio = grupo.transportes[i];
            if (navio == null) continue;
            if (preferirCarregados && !TransporteNavalTemCarga(navio)) continue;
            soma += Plano(navio.transform.position);
            total++;
        }

        if (total == 0 && preferirCarregados)
            return CalcularCentroTransportes(grupo, fallback, false);

        if (total == 0) return fallback;

        Vector3 centro = soma / total;
        centro.y = nivelDoMar;
        return centro;
    }

    Vector3 CalcularSlotNaval(Vector3 centro, Vector3 objetivo, float avancarBase, float espacamentoLateral, float espacamentoLinha, int indice, bool permitirCentro, float jitter = 6f)
    {
        Vector3 frente = Plano(objetivo) - Plano(centro);
        if (frente.sqrMagnitude < 1f) frente = Vector3.forward;
        frente.Normalize();

        Vector3 direita = Vector3.Cross(Vector3.up, frente).normalized;
        if (direita == Vector3.zero) direita = Vector3.right;

        int faixa = permitirCentro ? (indice == 0 ? 0 : ((indice + 1) / 2)) : ((indice / 2) + 1);
        float lado = permitirCentro
            ? (indice == 0 ? 0f : (indice % 2 == 1 ? -1f : 1f))
            : (indice % 2 == 0 ? -1f : 1f);

        float avancar = avancarBase - Mathf.Max(0, faixa - 1) * espacamentoLinha;
        float lateral = lado * faixa * espacamentoLateral;
        float micro = Mathf.Sin((indice + 1) * 1.719f) * jitter;

        Vector3 destino = centro + frente * avancar + direita * (lateral + micro);
        destino.y = nivelDoMar;
        return destino;
    }

    int MoverListaNavalFormacao(List<GameObject> lista, Vector3 centro, Vector3 objetivo, float avancarBase, float espacamentoLateral, float espacamentoLinha, bool permitirCentro)
    {
        int processados = 0;
        for (int i = 0; i < lista.Count; i++)
        {
            GameObject navio = lista[i];
            if (navio == null) continue;

            Vector3 destino = CalcularSlotNaval(centro, objetivo, avancarBase, espacamentoLateral, espacamentoLinha, i, permitirCentro, 7f);
            if (Vector3.Distance(Plano(navio.transform.position), Plano(destino)) > 18f)
                MoverNavio(navio, destino);
            processados++;
        }
        return processados;
    }

    int MoverTransportesNavais(GrupoNavalIA grupo, bool comboioPronto, int tropasReunidas)
    {
        int processados = 0;

        for (int i = 0; i < grupo.transportes.Count; i++)
        {
            GameObject navio = grupo.transportes[i];
            if (navio == null) continue;

            bool carregado = TransporteNavalTemCarga(navio);
            Vector3 ancora = comboioPronto && carregado ? ultimoPontoDesembarqueNaval : ultimoPontoCargaNaval;
            float avancar = comboioPronto && carregado ? -20f : -70f;
            if (!comboioPronto && carregado) avancar = -110f;

            Vector3 destino = CalcularSlotNaval(ancora, comboioPronto ? ultimoPontoDesembarqueNaval : ultimoPontoCargaNaval, avancar, 36f, 28f, i, true, 4f);
            MoverNavio(navio, destino);

            if (!carregado && tropasReunidas > 0 && Vector3.Distance(Plano(navio.transform.position), Plano(ultimoPontoCargaNaval)) <= 95f)
                IniciarCargaNaval(navio);
            else if (comboioPronto && carregado && Vector3.Distance(Plano(navio.transform.position), Plano(ultimoPontoDesembarqueNaval)) <= 130f)
                IniciarDescargaNaval(navio);

            processados++;
        }

        return processados;
    }

    bool CarrierPodeLancar(GrupoNavalIA grupo, bool contatoAtivo, bool comboioPronto)
    {
        return grupo.TotalEscoltas() >= 2 && (contatoAtivo || comboioPronto);
    }

    int ExecutarPlanoAnfibio(GrupoNavalIA grupo, Vector3 alvoAgua, Vector3 alvoAereo, bool contatoAtivo)
    {
        if (inicioPreparacaoAnfibia < 0f) inicioPreparacaoAnfibia = Time.time;

        ultimoPontoCargaNaval = ObterPontoCargaNaval();
        ultimoPontoDesembarqueNaval = ObterPontoDesembarqueNaval(alvoAgua);
        ultimoPontoCarrierNaval = ObterPontoLancamentoPortaAvioes(ultimoPontoDesembarqueNaval);
        ultimoPontoReuniaoNaval = ObterPontoReuniaoTerrestreNaval(ultimoPontoCargaNaval);

        int tropasReunidas = ReunirTropasParaInvasao(ultimoPontoReuniaoNaval);
        bool comboioPronto = PodePartirComboio(grupo);
        estadoPlanoNaval = comboioPronto ? (contatoAtivo ? EstadoPlanoNaval.AssaltoCosteiro : EstadoPlanoNaval.ComboioEmTransito) : EstadoPlanoNaval.PreparandoInvasaoAnfibia;

        AtualizarLancadoresGrupo(grupo, contatoAtivo);

        int processados = MoverTransportesNavais(grupo, comboioPronto, tropasReunidas);
        Vector3 centroComboio = CalcularCentroTransportes(grupo, ultimoPontoCargaNaval, comboioPronto);
        processados += MoverListaNavalFormacao(grupo.patrulhas, centroComboio, ultimoPontoDesembarqueNaval, 160f, 90f, 35f, true);
        processados += MoverListaNavalFormacao(grupo.escoltasMissil, centroComboio, ultimoPontoDesembarqueNaval, 40f, 120f, 40f, true);
        processados += MoverListaNavalFormacao(grupo.submarinos, centroComboio, ultimoPontoDesembarqueNaval, -10f, 210f, 30f, false);
        processados += MoverListaNavalFormacao(grupo.logisticos, ultimoPontoCargaNaval, ultimoPontoDesembarqueNaval, -180f, 90f, 40f, true);

        for (int i = 0; i < grupo.carriers.Count; i++)
        {
            GameObject carrier = grupo.carriers[i];
            if (carrier == null) continue;

            Vector3 destinoCarrier = CalcularSlotNaval(ultimoPontoCarrierNaval, ultimoPontoDesembarqueNaval, -20f, 100f, 40f, i, true, 8f);
            MoverNavio(carrier, destinoCarrier);

            if (CarrierPodeLancar(grupo, contatoAtivo, comboioPronto)
                && Vector3.Distance(Plano(carrier.transform.position), Plano(ultimoPontoCarrierNaval)) <= 260f)
            {
                LancarAvioesDoPortaAvioes(carrier, alvoAereo);
            }

            processados++;
        }

        for (int i = 0; i < grupo.submarinos.Count; i++)
        {
            GameObject submarino = grupo.submarinos[i];
            if (submarino == null || !contatoAtivo || !contatoNavalAtual.altoValor) continue;

            ControleSubmarino controle = submarino.GetComponent<ControleSubmarino>();
            if (controle != null && controle.PodeAtacarIA())
                controle.DispararMisselIA(contatoNavalAtual.posicao);
        }

        return processados;
    }

    int ExecutarAtaqueNaval(GrupoNavalIA grupo, Vector3 alvoAgua, Vector3 alvoAereo, bool contatoAtivo)
    {
        ultimoPontoCargaNaval = ObterPontoCargaNaval();
        ultimoPontoCarrierNaval = ObterPontoLancamentoPortaAvioes(alvoAgua);
        estadoPlanoNaval = contatoAtivo ? EstadoPlanoNaval.ContatoNaval : EstadoPlanoNaval.ConcentracaoNaval;
        inicioPreparacaoAnfibia = -999f;

        AtualizarLancadoresGrupo(grupo, contatoAtivo);

        int processados = 0;
        processados += MoverListaNavalFormacao(grupo.patrulhas, alvoAgua, alvoJogadorBase.position, 160f, 90f, 35f, true);
        processados += MoverListaNavalFormacao(grupo.escoltasMissil, alvoAgua, alvoJogadorBase.position, 40f, 120f, 40f, true);
        processados += MoverListaNavalFormacao(grupo.submarinos, alvoAgua, alvoJogadorBase.position, -10f, 210f, 30f, false);
        processados += MoverListaNavalFormacao(grupo.logisticos, ultimoPontoCargaNaval, alvoAgua, -140f, 90f, 35f, true);
        processados += MoverListaNavalFormacao(grupo.transportes, ultimoPontoCargaNaval, ultimoPontoCargaNaval, -70f, 34f, 25f, true);

        bool carrierEmSuporte = CarrierPodeLancar(grupo, contatoAtivo, false);
        if (carrierEmSuporte) estadoPlanoNaval = EstadoPlanoNaval.SuportePortaAvioes;

        for (int i = 0; i < grupo.carriers.Count; i++)
        {
            GameObject carrier = grupo.carriers[i];
            if (carrier == null) continue;

            Vector3 destinoCarrier = CalcularSlotNaval(ultimoPontoCarrierNaval, alvoAgua, -20f, 100f, 40f, i, true, 8f);
            MoverNavio(carrier, destinoCarrier);

            if (carrierEmSuporte && Vector3.Distance(Plano(carrier.transform.position), Plano(ultimoPontoCarrierNaval)) <= 260f)
                LancarAvioesDoPortaAvioes(carrier, alvoAereo);

            processados++;
        }

        for (int i = 0; i < grupo.submarinos.Count; i++)
        {
            GameObject submarino = grupo.submarinos[i];
            if (submarino == null || !contatoAtivo || !contatoNavalAtual.altoValor) continue;

            ControleSubmarino controle = submarino.GetComponent<ControleSubmarino>();
            if (controle != null && controle.PodeAtacarIA())
                controle.DispararMisselIA(contatoNavalAtual.posicao);
        }

        return processados;
    }

    int PatrulharCosta()
    {
        Vector3 ancora = ObterAncoraNavalSegura();
        if (ancora == Vector3.zero) return 0;

        estadoPlanoNaval = EstadoPlanoNaval.PatrulhaCosteira;
        inicioPreparacaoAnfibia = -999f;
        AtualizarLancadoresGrupo(grupoNavalPlanejado, false);

        int processados = 0;
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.patrulhas, ancora, ancora + Vector3.forward * 50f, 110f, 80f, 30f, true);
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.escoltasMissil, ancora, ancora + Vector3.forward * 50f, 20f, 110f, 35f, true);
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.submarinos, ancora, ancora + Vector3.forward * 50f, -20f, 190f, 25f, false);
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.transportes, ultimoPontoCargaNaval != Vector3.zero ? ultimoPontoCargaNaval : ancora, ancora, -60f, 34f, 25f, true);
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.logisticos, ancora, ancora, -140f, 80f, 30f, true);
        processados += MoverListaNavalFormacao(grupoNavalPlanejado.carriers, ancora, ancora, -220f, 100f, 35f, true);
        return processados;
    }

    int GerenciarTaticaNaval()
    {
        meusNavios.RemoveAll(n => n == null);
        if (meusNavios.Count == 0) return 0;

        MontarGrupoNaval(grupoNavalPlanejado);
        AtualizarContatoNaval(grupoNavalPlanejado);

        bool contatoAtivo = contatoNavalAtual.EstaAtivo();
        bool semRotaTerrestre = !TemRotaTerrestreParaAlvoNaval();
        Vector3 alvoAgua = alvoJogadorBase != null ? EncontrarAguaPertoDoAlvo(alvoJogadorBase.position) : Vector3.zero;
        Vector3 alvoAereo = alvoJogadorEconomia != null ? alvoJogadorEconomia.position : (alvoJogadorBase != null ? alvoJogadorBase.position : ObterCentroBase());

        if (estadoAtual == EstadoIA.DefesaDesesperada)
        {
            ultimoPontoCargaNaval = ObterAncoraNavalSegura();
            estadoPlanoNaval = contatoAtivo ? EstadoPlanoNaval.ContatoNaval : EstadoPlanoNaval.PatrulhaCosteira;
            AtualizarLancadoresGrupo(grupoNavalPlanejado, contatoAtivo);

            int processados = 0;
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.patrulhas, ultimoPontoCargaNaval, ultimoPontoCargaNaval, 90f, 70f, 25f, true);
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.escoltasMissil, ultimoPontoCargaNaval, ultimoPontoCargaNaval, 10f, 90f, 30f, true);
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.submarinos, ultimoPontoCargaNaval, ultimoPontoCargaNaval, -20f, 170f, 25f, false);
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.transportes, ultimoPontoCargaNaval, ultimoPontoCargaNaval, -70f, 34f, 25f, true);
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.carriers, ultimoPontoCargaNaval, ultimoPontoCargaNaval, -190f, 90f, 35f, true);
            processados += MoverListaNavalFormacao(grupoNavalPlanejado.logisticos, ultimoPontoCargaNaval, ultimoPontoCargaNaval, -240f, 80f, 35f, true);
            return processados;
        }

        if (alvoJogadorBase != null && alvoAgua != Vector3.zero && grupoNavalPlanejado.TotalTransportes() > 0 && semRotaTerrestre)
            return ExecutarPlanoAnfibio(grupoNavalPlanejado, alvoAgua, alvoAereo, contatoAtivo);

        if (estadoAtual == EstadoIA.GuerraTotal && alvoJogadorBase != null && alvoAgua != Vector3.zero)
            return ExecutarAtaqueNaval(grupoNavalPlanejado, alvoAgua, alvoAereo, contatoAtivo);

        return PatrulharCosta();
    }

    Vector3 EncontrarAguaPertoDoAlvo(Vector3 alvo)
    {
        for (float r = 80f; r <= 1200f; r += 40f)
        {
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = alvo + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float alt;
                if (DetectarTerreno(teste, out alt) == TipoTerreno.Agua)
                {
                    teste.y = nivelDoMar;
                    return teste;
                }
            }
        }
        return Vector3.zero;
    }

    void MoverNavio(GameObject navio, Vector3 destino)
    {
        if (navio == null) return;
        destino.y = nivelDoMar;
        var controle = navio.GetComponent<ControleUnidade>();
        if (controle != null)
        {
            controle.EmitirOrdemMover(destino);
            navio.SendMessage("DefinirDestino", destino, SendMessageOptions.DontRequireReceiver);
            return;
        }

        navio.SendMessage("MoverParaPonto", destino, SendMessageOptions.DontRequireReceiver);
        navio.SendMessage("DefinirDestino", destino, SendMessageOptions.DontRequireReceiver);

        var nav = navio.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled && nav.isOnNavMesh)
        {
            nav.isStopped = false;
            MovimentoFallbackTransicional.TrySetNavDestination(navio, destino);
        }
    }

    bool NomeContemTrecho(string nome, params string[] trechos)
    {
        if (string.IsNullOrEmpty(nome)) return false;
        string n = nome.ToLower();
        for (int i = 0; i < trechos.Length; i++)
        {
            if (n.Contains(trechos[i])) return true;
        }
        return false;
    }

    bool EhPortaAvioesNome(string nome)
    {
        string n = string.IsNullOrEmpty(nome) ? string.Empty : nome.ToLower();
        return (n.Contains("porta") && n.Contains("avio"))
            || n.Contains("porta_avio")
            || n.Contains("porta-avio")
            || n.Contains("carrier")
            || n.Contains("sovereign");
    }

    bool EhSubmarinoNome(string nome)
    {
        return NomeContemTrecho(nome, "submarino", "submarine", "leviathan");
    }

    bool EhTransporteNavalNome(string nome)
    {
        string n = string.IsNullOrEmpty(nome) ? string.Empty : nome.ToLower();
        if (EhPortaAvioesNome(n) || EhSubmarinoNome(n)) return false;

        return n.Contains("hovercraft")
            || n.Contains("hover")
            || n.Contains("liberty")
            || n.Contains("barco ww")
            || n.Contains("anfib")
            || n.Contains("landing")
            || n.Contains("lst")
            || (n.Contains("transporte") && (n.Contains("nav") || n.Contains("mar") || n.Contains("praia")));
    }

    bool EhNavioCombateNome(string nome)
    {
        string n = string.IsNullOrEmpty(nome) ? string.Empty : nome.ToLower();
        if (EhPortaAvioesNome(n) || EhTransporteNavalNome(n) || EhSubmarinoNome(n)) return false;

        return n.Contains("navio")
            || n.Contains("uss ")
            || n.Contains("wall")
            || n.Contains("ironclad")
            || n.Contains("vindicator")
            || n.Contains("vigia")
            || n.Contains("corveta")
            || n.Contains("fragata")
            || n.Contains("destroyer")
            || n.Contains("cruzador")
            || n.Contains("barco")
            || n.Contains("lancha")
            || n.Contains("marinha")
            || n.Contains("encoura");
    }

    bool EhPortaAvioes(GameObject navio)
    {
        return navio != null && (navio.GetComponent<GerenciadorPortaAvioes>() != null || EhPortaAvioesNome(navio.name));
    }

    bool EhSubmarino(GameObject navio)
    {
        return navio != null && EhSubmarinoNome(navio.name);
    }

    bool EhTransporteNaval(GameObject navio)
    {
        return navio != null && (navio.GetComponent<HovercraftTransporte>() != null
            || navio.GetComponent<TransporteAnfibio>() != null
            || navio.GetComponent<NavioLiberty>() != null
            || EhTransporteNavalNome(navio.name));
    }

    bool ExistePortaAvioesOuTransporteNaval()
    {
        for (int i = 0; i < meusNavios.Count; i++)
        {
            GameObject navio = meusNavios[i];
            if (navio == null) continue;
            if (EhPortaAvioes(navio) || EhTransporteNaval(navio)) return true;
        }
        return false;
    }

    bool TransporteNavalTemCarga(GameObject navio)
    {
        if (navio == null) return false;

        HovercraftTransporte hover = navio.GetComponent<HovercraftTransporte>();
        if (hover != null) return hover.TemCarga();

        TransporteAnfibio anfibio = navio.GetComponent<TransporteAnfibio>();
        if (anfibio != null) return anfibio.unidadesGuardadas != null && anfibio.unidadesGuardadas.Count > 0;

        return false;
    }

    void IniciarCargaNaval(GameObject navio)
    {
        if (navio == null) return;

        HovercraftTransporte hover = navio.GetComponent<HovercraftTransporte>();
        if (hover != null)
        {
            hover.IniciarEmbarque();
            return;
        }

        navio.SendMessage("ChamarReforcos", SendMessageOptions.DontRequireReceiver);
        navio.SendMessage("IniciarEmbarque", SendMessageOptions.DontRequireReceiver);
        navio.SendMessage("TentarEmbarcar", SendMessageOptions.DontRequireReceiver);
    }

    void IniciarDescargaNaval(GameObject navio)
    {
        if (navio == null) return;

        HovercraftTransporte hover = navio.GetComponent<HovercraftTransporte>();
        if (hover != null)
        {
            hover.DesembarcarTudo();
            return;
        }

        TransporteAnfibio anfibio = navio.GetComponent<TransporteAnfibio>();
        if (anfibio != null)
        {
            navio.SendMessage("CicloFecharDesembarcar", SendMessageOptions.DontRequireReceiver);
            return;
        }

        navio.SendMessage("DesembarcarTudo", SendMessageOptions.DontRequireReceiver);
    }

    Vector3 ObterPontoCargaNaval()
    {
        Vector3 aguaBase = EncontrarAguaPertoDoAlvo(ObterCentroBase());
        if (aguaBase != Vector3.zero)
        {
            aguaBase.y = nivelDoMar;
            return aguaBase;
        }

        Vector3 ancora = ObterAncoraNavalSegura();
        if (ancora != Vector3.zero)
        {
            ancora.y = nivelDoMar;
            return ancora;
        }

        Vector3 centro = ObterCentroBase();
        centro.y = nivelDoMar;
        return centro;
    }

    Vector3 ObterPontoDesembarqueNaval(Vector3 alvoAgua)
    {
        if (alvoAgua != Vector3.zero)
        {
            alvoAgua.y = nivelDoMar;
            return alvoAgua;
        }

        Vector3 fallback = EncontrarAguaPertoDoAlvo(alvoJogadorBase != null ? alvoJogadorBase.position : ObterCentroBase());
        if (fallback != Vector3.zero)
        {
            fallback.y = nivelDoMar;
            return fallback;
        }

        return ObterPontoCargaNaval();
    }

    Vector3 ObterPontoLancamentoPortaAvioes(Vector3 alvoAgua)
    {
        if (alvoJogadorBase == null) return alvoAgua;

        Vector3 direcaoRetaguarda = Plano(alvoAgua) - Plano(alvoJogadorBase.position);
        if (direcaoRetaguarda.sqrMagnitude < 1f)
            direcaoRetaguarda = Plano(alvoAgua) - Plano(ObterCentroBase());
        if (direcaoRetaguarda.sqrMagnitude < 1f)
            direcaoRetaguarda = Vector3.forward;

        direcaoRetaguarda.Normalize();
        Vector3 ponto = alvoAgua + direcaoRetaguarda * 360f;
        ponto.y = nivelDoMar;

        float altura;
        if (DetectarTerreno(ponto, out altura) != TipoTerreno.Agua) return alvoAgua;
        return ponto;
    }

    void LancarAvioesDoPortaAvioes(GameObject navio, Vector3 alvoAereo)
    {
        if (navio == null) return;

        GerenciadorPortaAvioes carrier = navio.GetComponent<GerenciadorPortaAvioes>();
        if (carrier == null) return;

        if (carrier.avioesNoPatio.Count == 0 && carrier.avioesNoHangar.Count > 0)
            carrier.LiberarTodosDoHangar();

        for (int i = 0; i < carrier.avioesNoPatio.Count; i++)
        {
            ControleAviao aviao = carrier.avioesNoPatio[i];
            if (aviao == null) continue;
            if (aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) continue;
            aviao.IniciarMissaoCompleta(alvoAereo);
        }
    }

    void ObterResumoFrotaNaval(out int portaAvioes, out int transportesNavais, out int submarinos, out int escoltas)
    {
        portaAvioes = 0;
        transportesNavais = 0;
        submarinos = 0;
        escoltas = 0;

        for (int i = 0; i < meusNavios.Count; i++)
        {
            GameObject navio = meusNavios[i];
            if (navio == null) continue;

            if (EhPortaAvioes(navio)) portaAvioes++;
            else if (EhTransporteNaval(navio)) transportesNavais++;
            else if (EhSubmarino(navio)) submarinos++;
            else escoltas++;
        }
    }

    int ContarTropasTerrestresDisponiveis()
    {
        int total = 0;
        for (int i = 0; i < minhasTropas.Count; i++)
        {
            GameObject tropa = minhasTropas[i];
            if (tropa == null || tropa.transform.parent != null) continue;
            if (EhObjetoAereo(tropa.name)) continue;
            total++;
        }
        return total;
    }

    // =========================================================
    // TRANSPORTES
    // =========================================================
    int GerenciarLogisticaTransportes()
    {
        if (alvoJogadorBase == null) return 0;
        Vector3 centro = ObterCentroBase();
        int processados = 0;
        int tentativas = 0;
        int total = meusTransportes.Count;

        while (tentativas < total && processados < Mathf.Max(1, maxTransportesProcessadosPorCiclo))
        {
            if (cursorTransportesTaticos >= meusTransportes.Count) cursorTransportesTaticos = 0;
            GameObject veiculo = meusTransportes[cursorTransportesTaticos];
            cursorTransportesTaticos++;
            tentativas++;

            if (veiculo == null) continue;

            if (EhTransporteNaval(veiculo))
            {
                if (!meusNavios.Contains(veiculo)) meusNavios.Add(veiculo);
                processados++;
                continue;
            }

            bool voa = veiculo.name.ToLower().Contains("aereo");
            float distAlvo = Vector3.Distance(veiculo.transform.position, alvoJogadorBase.position);
            float distBase = Vector3.Distance(veiculo.transform.position, centro);

            Vector3 dir = (alvoJogadorBase.position - centro).normalized;
            Vector3 pontoDesembarque = alvoJogadorBase.position - dir * 150f;

            float alt;
            DetectarTerreno(pontoDesembarque, out alt);
            pontoDesembarque.y = alt + (voa ? alturaAereaSpawn : 0f);

            int passageiros = 0;
            int capacidade = 4;

            if (veiculo.name.ToLower().Contains("helicoptero") || veiculo.name.ToLower().Contains("aereo"))
            {
                Helicoptero h = veiculo.GetComponent<Helicoptero>();
                if (h != null)
                {
                    passageiros = h.soldadosEmbarcados.Count;
                    capacidade = h.capacidadeMaxima;
                }
            }
            else
            {
                TransporteTerrestre t = veiculo.GetComponent<TransporteTerrestre>();
                if (t != null)
                {
                    passageiros = t.QuantidadePassageiros;
                    capacidade = t.capacidadeMaxima;
                }
            }

            bool ataque = estadoAtual == EstadoIA.GuerraTotal;
            bool pronto = passageiros >= Mathf.CeilToInt(capacidade * 0.7f) || (distBase > 140f && passageiros > 0);

            if (ataque && pronto)
            {
                if (distAlvo > 165f) Mover(veiculo, pontoDesembarque);
                else
                {
                    bool heli = veiculo.name.ToLower().Contains("helicoptero") || veiculo.name.ToLower().Contains("aereo");
                    if (heli) veiculo.SendMessage("OrdemPousoOuDesembarque", SendMessageOptions.DontRequireReceiver);
                    else
                    {
                        veiculo.SendMessage("DesembarcarTudo", SendMessageOptions.DontRequireReceiver);
                        veiculo.SendMessage("OrdemPousoOuDesembarque", SendMessageOptions.DontRequireReceiver);
                        Mover(veiculo, centro);
                    }
                }
            }
            else
            {
                if (distBase > 85f)
                {
                    Vector3 retorno = centro + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
                    float hAlt;
                    DetectarTerreno(retorno, out hAlt);
                    retorno.y = hAlt + (voa ? alturaAereaSpawn : 0f);
                    Mover(veiculo, retorno);
                }
                else
                {
                    veiculo.SendMessage("ChamarReforcos", SendMessageOptions.DontRequireReceiver);
                    veiculo.SendMessage("TentarEmbarcar", SendMessageOptions.DontRequireReceiver);
                }
            }

            processados++;
        }

        return processados;
    }

    // =========================================================
    // MOVIMENTO
    // =========================================================
    void Mover(GameObject unidade, Vector3 destino)
    {
        if (unidade == null) return;
        bool aereo = EhObjetoAereo(unidade.name);

        if (aereo)
        {
            float alt;
            DetectarTerreno(destino, out alt);
            if (destino.y <= alt + 5f) destino.y = alt + alturaAereaSpawn;
        }
        else
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(destino, out hit, 30f, NavMesh.AllAreas)) destino = hit.position;
        }

        ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
        if (controle != null)
        {
            controle.EmitirOrdemMover(destino);
            return;
        }

        unidade.SendMessage("MoverParaPonto", destino, SendMessageOptions.DontRequireReceiver);

        NavMeshAgent nav = unidade.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled && nav.isOnNavMesh)
        {
            nav.isStopped = false;
            MovimentoFallbackTransicional.TrySetNavDestination(unidade, destino);
        }
    }

    // =========================================================
    // SPAWN E CONFIGURAÃ‡ÃƒO
    // =========================================================
    void SpawnarObjeto(GameObject prefab, Vector3 pos, string nome, Quaternion rot = default)
    {
        if (prefab == null) return;
        if (rot == default) rot = Quaternion.identity;

        string lower = nome.ToLower();

        if (EhNaval(lower))
        {
            float raioNaval = Mathf.Max(28f, CalcularRaioSeguro(lower) + 12f);
            if (!PosicaoNavalProfundaValida(pos, raioNaval))
            {
                Vector3 alternativa = EncontrarAguaProfundaMaisProxima(pos, raioNaval);
                if (alternativa == Vector3.zero) alternativa = ObterAncoraNavalSegura();
                if (alternativa == Vector3.zero) return;
                pos = alternativa;
            }
            pos.y = nivelDoMar;
        }
        else
        {
            float alt;
            if (DetectarTerreno(pos, out alt) != TipoTerreno.Terra) return;
            pos.y = alt;
        }

        GameObject novo = Instantiate(prefab, pos, rot);
        novo.name = nome;
        ConfigurarObjeto(novo, true);
        meusPredios.Add(novo);
        ReconstruirGridTatico(true);
    }

    void ConfigurarObjeto(GameObject obj, bool ehPredio)
    {
        if (obj == null) return;

        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.AddComponent<IdentidadeUnidade>();
        id.teamID = teamID;
        id.nomeDoPais = nomeNacao;

        var raycasters = obj.GetComponentsInChildren<GraphicRaycaster>(true);
        foreach (var g in raycasters) Destroy(g);

        var dmg = obj.GetComponent<SistemaDeDanos>();
        if (dmg == null)
        {
            dmg = obj.AddComponent<SistemaDeDanos>();
            dmg.vidaMaxima = 1500;
            dmg.vidaAtual = 1500;
        }

        if (obj.GetComponent<Collider>() == null)
        {
            Vector3 s = obj.transform.lossyScale;
            if (s.x < 0 || s.y < 0 || s.z < 0) obj.AddComponent<MeshCollider>().convex = true;
            else obj.AddComponent<BoxCollider>();
        }

        var nav = obj.GetComponent<NavMeshAgent>();
        var obs = obj.GetComponent<NavMeshObstacle>();

        if (ehPredio)
        {
            if (nav != null) nav.enabled = false;
            if (obs != null) obs.enabled = true;
        }
        else
        {
            if (obs != null) obs.enabled = false;
            if (nav != null) nav.enabled = true;
        }
    }

    // =========================================================
    // SCAN DE CATÃLOGO
    // =========================================================
    void RealizarScanDeArquivos()
    {
        biblioteca.Clear();
        if (MenuConstrucao.catalogoGlobal == null) return;

        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item == null || item.prefabDaUnidade == null) continue;
            string n = (item.nomeItem + " " + item.prefabDaUnidade.name).ToLower();
            Mapear(n, item.prefabDaUnidade);
        }
    }

    void Mapear(string n, GameObject obj)
    {
        bool ehCarrierPrefab = obj != null && obj.GetComponent<GerenciadorPortaAvioes>() != null;
        bool ehTransporteNavalPrefab = obj != null && (obj.GetComponent<HovercraftTransporte>() != null
            || obj.GetComponent<TransporteAnfibio>() != null
            || obj.GetComponent<NavioLiberty>() != null);
        bool ehSubmarinoPrefab = obj != null && obj.GetComponent<ControleSubmarino>() != null;
        bool ehCombateNavalPrefab = obj != null && (obj.GetComponent<ControleNavioRealista>() != null
            || obj.GetComponent<ControladorNavioVigilante>() != null
            || obj.GetComponentInChildren<LancadorNaval>(true) != null
            || obj.GetComponent<IdentidadeNaval>() != null);

        if (n.Contains("prefeitura") || n.Contains("complexo") || n.Contains("governo")) AddLib("prefeitura", obj);
        else if (n.Contains("quartel") || n.Contains("tenda") || n.Contains("barraca")) AddLib("quartel", obj);
        else if (n.Contains("fabrica") || n.Contains("construtor") || n.Contains("hangar")) AddLib("fabrica", obj);
        else if (n.Contains("plataforma") || n.Contains("platform")) AddLib("plataforma", obj);
        else if (n.Contains("refinaria") || n.Contains("petroleo") || n.Contains("mina")) AddLib("refinaria", obj);
        else if (!(ehCarrierPrefab || ehTransporteNavalPrefab || ehSubmarinoPrefab || ehCombateNavalPrefab)
            && (n.Contains("antiaerea") || n.Contains("ares") || n.Contains("sam") || n.Contains("missil"))) AddLib("antiaerea", obj);
        else if (n.Contains("torreta") || n.Contains("defesa") || n.Contains("canhao")) AddLib("torreta", obj);
        else if (n.Contains("aeroporto") || n.Contains("pista")) AddLib("aeroporto", obj);
        else if (n.Contains("estaleiro") || n.Contains("naval")) AddLib("estaleiro", obj);
        else if (n.Contains("pier") || n.Contains("porto")) AddLib("pier", obj);
        else if (n.Contains("soldado") || n.Contains("infantaria") || n.Contains("fuzileiro") || n.Contains("person")) AddLib("soldado", obj);
        else if (n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado")) AddLib("tanque", obj);
        else if (ehCarrierPrefab || EhPortaAvioesNome(n))
        {
            AddLib("porta_avioes", obj);
            AddLib("navio", obj);
        }
        else if (ehTransporteNavalPrefab || EhTransporteNavalNome(n))
        {
            AddLib("navio_transporte", obj);
            AddLib("navio", obj);
        }
        else if (n.Contains("ray") || n.Contains("guincho")) AddLib("transporte_aereo", obj);
        else if (n.Contains("heli") || n.Contains("apache") || n.Contains("cobra")) AddLib("helicoptero", obj);
        else if (n.Contains("transporte") || n.Contains("caminhao") || n.Contains("truck")) AddLib("transporte", obj);
        else if (n.Contains("caca") || n.Contains("aviao") || n.Contains("jet") || n.Contains("tuk") || n.Contains("super") || n.Contains("g15")) AddLib("caca", obj);
        else if (ehSubmarinoPrefab || EhSubmarinoNome(n)) AddLib("submarino", obj);
        else if (ehCombateNavalPrefab || EhNavioCombateNome(n))
            AddLib("navio", obj);
    }

    void AddLib(string k, GameObject o)
    {
        if (!biblioteca.ContainsKey(k)) biblioteca.Add(k, new List<GameObject>());
        if (!biblioteca[k].Contains(o)) biblioteca[k].Add(o);
    }

    GameObject ObterPrefab(string chave)
    {
        if (!biblioteca.ContainsKey(chave) || biblioteca[chave].Count == 0) return null;
        return biblioteca[chave][Random.Range(0, biblioteca[chave].Count)];
    }

    // =========================================================
    // CONTAGEM E LIMPEZA
    // =========================================================
    void LimparMortos()
    {
        meusPredios.RemoveAll(x => x == null);
        minhasTropas.RemoveAll(x => x == null);
        meusTransportes.RemoveAll(x => x == null);
        meusNavios.RemoveAll(x => x == null);
    }

    int Contar(string chave)
    {
        return meusPredios.Count(x => x != null && EhCategoria(x.name, chave))
             + minhasTropas.Count(x => x != null && EhCategoria(x.name, chave))
             + meusTransportes.Count(x => x != null && EhCategoria(x.name, chave))
             + meusNavios.Count(x => x != null && EhCategoria(x.name, chave));
    }

    int ContarNavios()
    {
        meusNavios.RemoveAll(x => x == null);
        return meusNavios.Count;
    }

    bool TemPrefeitura() { return meusPredios.Any(p => p != null && EhCategoria(p.name, "prefeitura")); }

    // =========================================================
    // UTILITÃRIOS
    // =========================================================
    TipoZona ObterZonaPreferida(string chave)
    {
        chave = NormalizarChave(chave);
        if (bancoDados.ContainsKey(chave)) return bancoDados[chave].zonaPreferida;
        return TipoZona.Nenhuma;
    }

    bool EhNaval(string chave)
    {
        chave = NormalizarChave(chave);
        return bancoDados.ContainsKey(chave) && bancoDados[chave].ehNaval;
    }

    bool EhAereo(string chave)
    {
        chave = NormalizarChave(chave);
        return bancoDados.ContainsKey(chave) && bancoDados[chave].ehAereo;
    }

    bool EhObjetoAereo(string nome)
    {
        string n = NormalizarChave(nome);
        return n == "helicoptero" || n == "caca" || n == "transporte_aereo" || nome.IndexOf("aviao", System.StringComparison.OrdinalIgnoreCase) >= 0 || nome.IndexOf("jet", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    Transform PrimeiroTransformValido(List<Transform> lista)
    {
        if (lista == null) return null;
        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i] != null) return lista[i];
        }
        return null;
    }

    IdentidadeUnidade PrimeiroInimigoConhecido()
    {
        for (int i = 0; i < inimigosConhecidos.Count; i++)
        {
            if (inimigosConhecidos[i] != null) return inimigosConhecidos[i];
        }
        return null;
    }

    void RegistrarTempoModulo(ref float ultimoMs, ref float picoMs, float inicio)
    {
        ultimoMs = (Time.realtimeSinceStartup - inicio) * 1000f;
        if (ultimoMs > picoMs) picoMs = ultimoMs;
    }

    void AtualizarResumoPerformance()
    {
        if (!debugPerformance)
        {
            if (!string.IsNullOrEmpty(resumoPerformance)) resumoPerformance = string.Empty;
            return;
        }

        if (!Application.isPlaying || Time.unscaledTime < proximaAtualizacaoResumoPerformance) return;
        proximaAtualizacaoResumoPerformance = Time.unscaledTime + 1f;

        StringBuilder sb = new StringBuilder(320);
        sb.Append("Recon ").Append(custoReconhecimentoMs.ToString("F2")).Append("ms (pico ").Append(picoReconhecimentoMs.ToString("F2")).Append("ms)").Append('\n');
        sb.Append("Economia ").Append(custoEconomiaMs.ToString("F2")).Append("ms | Logistica ").Append(custoLogisticaMs.ToString("F2")).Append("ms | Tatica ").Append(custoTaticaMs.ToString("F2")).Append("ms | Manutencao ").Append(custoManutencaoMs.ToString("F2")).Append("ms").Append('\n');
        sb.Append("Lotes: tropas ").Append(ultimoLoteTropas).Append(", transportes ").Append(ultimoLoteTransportes).Append(", navios ").Append(ultimoLoteNavios).Append(", avioes ").Append(ultimoLoteAvioes).Append('\n');
        sb.Append("Producao ").Append(ultimoPedidosProduzidos).Append(" | Inimigos cacheados ").Append(ultimoInimigosReconhecidos).Append(" | Rejeicoes ativas ").Append(rejeicoesRecentes.Count);
        resumoPerformance = sb.ToString();
    }

    private System.Collections.Generic.Dictionary<string, string> _cacheNormalizarChave = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    string NormalizarChave(string n)
    {
        if (string.IsNullOrEmpty(n)) return string.Empty;
        if (_cacheNormalizarChave.TryGetValue(n, out string cached)) return cached;

        string nLower = n.ToLower();
        string result = nLower.Trim();

        if (nLower.Contains("prefeitura") || nLower.Contains("complexo") || nLower.Contains("governo")) result = "prefeitura";
        else if (nLower.Contains("quartel") || nLower.Contains("tenda") || nLower.Contains("barraca")) result = "quartel";
        else if (nLower.Contains("fabrica") || nLower.Contains("construtor") || nLower.Contains("hangar")) result = "fabrica";
        else if (nLower.Contains("refinaria") || nLower.Contains("mina") || nLower.Contains("petroleo")) result = "refinaria";
        else if (nLower.Contains("torreta") || nLower.Contains("defesa") || nLower.Contains("canhao")) result = "torreta";
        else if (nLower.Contains("antiaerea") || nLower.Contains("ares") || nLower.Contains("sam") || nLower.Contains("missil")) result = "antiaerea";
        else if (nLower.Contains("aeroporto") || nLower.Contains("pista")) result = "aeroporto";
        else if (nLower.Contains("estaleiro") || nLower.Contains("naval")) result = "estaleiro";
        else if (nLower.Contains("pier") || nLower.Contains("porto")) result = "pier";
        else if (nLower.Contains("plataforma")) result = "plataforma";
        else if (nLower.Contains("soldado") || nLower.Contains("infantaria") || nLower.Contains("fuzileiro") || nLower.Contains("person")) result = "soldado";
        else if (nLower.Contains("tanque") || nLower.Contains("tank") || nLower.Contains("blindado") || nLower.Contains("leopard")) result = "tanque";
        else if (EhPortaAvioesNome(nLower) || EhTransporteNavalNome(nLower)) result = "navio";
        else if (nLower.Contains("transporte_aereo") || nLower.Contains("ray") || nLower.Contains("guincho")) result = "transporte_aereo";
        else if (nLower.Contains("helicoptero") || nLower.Contains("apache") || nLower.Contains("cobra") || nLower.Contains("heli")) result = "helicoptero";
        else if (nLower.Contains("transporte") || nLower.Contains("caminhao") || nLower.Contains("truck")) result = "transporte";
        else if (nLower.Contains("caca") || nLower.Contains("aviao") || nLower.Contains("jet") || nLower.Contains("tuk") || nLower.Contains("super") || nLower.Contains("g15")) result = "caca";
        else if (nLower.Contains("submarino") || nLower.Contains("submarine")) result = "submarino";
        else if (EhNavioCombateNome(nLower)) result = "navio";

        _cacheNormalizarChave[n] = result;
        return result;
    }

    List<ZonaIA> ObterZonasDeBusca(string chave)
    {
        TipoZona preferida = ObterZonaPreferida(chave);
        List<ZonaIA> resultado = new List<ZonaIA>();

        foreach (var zona in zonas) { if (zona.tipo == preferida) resultado.Add(zona); }

        if (preferida != TipoZona.Naval)
        {
            foreach (var zona in zonas) { if (zona.tipo == TipoZona.Expansao && !resultado.Contains(zona)) resultado.Add(zona); }
        }
        return resultado;
    }

    List<PedidoProducao> MontarPedidosProducao()
    {
        List<PedidoProducao> pedidos = new List<PedidoProducao>();

        bool temQuartel = Contar("quartel") > 0;
        bool temFabrica = Contar("fabrica") > 0;
        bool temAeroporto = Contar("aeroporto") > 0;
        bool temNaval = EscolherEstruturaNaval() != null;

        float rendaEstimada = rendaBase + Contar("refinaria") * 22f + Contar("plataforma") * 24f;
        bool economiaFragil = rendaEstimada < 120f || dinheiroIA < 1000f;
        bool guerraTotal = estadoAtual == EstadoIA.GuerraTotal;
        bool defesaCritica = estadoAtual == EstadoIA.DefesaDesesperada;
        float bonusNaval = zonas.Any(z => z.tipo == TipoZona.Naval) ? 45f : 0f;

        int soldados = Contar("soldado");
        int tanques = Contar("tanque");
        int helicopteros = Contar("helicoptero");
        int avioes = ContarAvioes();
        int transportes = Contar("transporte");
        int transportesAereos = Contar("transporte_aereo");
        int tropasTerrestres = ContarTropasTerrestresDisponiveis();
        int portaAvioes;
        int transportesNavais;
        int submarinos;
        int escoltas;
        ObterResumoFrotaNaval(out portaAvioes, out transportesNavais, out submarinos, out escoltas);
        int navios = portaAvioes + transportesNavais + escoltas;
        bool temPistaCaca = temAeroporto || portaAvioes > 0;
        bool semFronteiraTerrestre = alvoJogadorBase != null && !TemRotaTerrestreParaAlvoNaval();
        bool prepararInvasaoAnfibia = temNaval && semFronteiraTerrestre && tropasTerrestres >= 8;
        float bonusAnfibio = prepararInvasaoAnfibia ? 95f : 0f;
        float bonusEscoltaComboio = prepararInvasaoAnfibia && escoltas < Mathf.Max(2, transportesNavais * 2) ? 75f : 0f;

        if (temQuartel)
        {
            float scoreSoldado = Mathf.Max(0, metaSoldados - soldados) * 42f;
            scoreSoldado += defesaCritica ? 110f : (guerraTotal ? 35f : 15f);
            scoreSoldado += economiaFragil ? 20f : 0f;
            AdicionarPedido(pedidos, "soldado", 150f, scoreSoldado, false, false);
        }

        if (temFabrica)
        {
            float scoreTanque = Mathf.Max(0, metaTanques - tanques) * 55f;
            scoreTanque += guerraTotal ? 45f : 10f;
            scoreTanque -= economiaFragil ? 70f : 0f;
            AdicionarPedido(pedidos, "tanque", 600f, scoreTanque, false, false);

            float scoreTransporte = transportes < 2 && soldados >= 6 ? 95f - transportes * 25f : 0f;
            scoreTransporte += guerraTotal ? 10f : 0f;
            AdicionarPedido(pedidos, "transporte", 400f, scoreTransporte, false, false);
        }

        if (temAeroporto)
        {
            float scoreHelicoptero = Mathf.Max(0, metaHelicopteros - helicopteros) * 45f;
            scoreHelicoptero += defesaCritica ? 35f : 10f;
            scoreHelicoptero -= economiaFragil ? 25f : 0f;
            AdicionarPedido(pedidos, "helicoptero", 900f, scoreHelicoptero, true, false);

            float scoreTransporteAereo = transportesAereos < 2 && (guerraTotal || alvoJogadorEconomia != null) ? 90f - transportesAereos * 30f : 0f;
            scoreTransporteAereo -= economiaFragil ? 20f : 0f;
            AdicionarPedido(pedidos, "transporte_aereo", 400f, scoreTransporteAereo, true, false);
        }

        if (temPistaCaca)
        {
            float scoreCaca = Mathf.Max(0, metaCacas - avioes) * 50f;
            scoreCaca += forcaInimigaAerea * 70f;
            scoreCaca += portaAvioes > 0 ? 35f : 0f;
            scoreCaca -= economiaFragil ? 60f : 0f;
            AdicionarPedido(pedidos, "caca", 1200f, scoreCaca, true, false);
        }

        if (temNaval)
        {
            if (biblioteca.ContainsKey("porta_avioes"))
            {
                float scorePortaAvioes = portaAvioes == 0 ? 170f + bonusNaval : 0f;
                scorePortaAvioes += guerraTotal ? 40f : 10f;
                scorePortaAvioes += bonusAnfibio * 0.6f;
                scorePortaAvioes += avioes >= 2 ? 30f : 0f;
                scorePortaAvioes -= economiaFragil ? 120f : 0f;
                AdicionarPedido(pedidos, "porta_avioes", 2600f, scorePortaAvioes, false, true);
            }

            if (biblioteca.ContainsKey("navio_transporte"))
            {
                float scoreTransporteNaval = guerraTotal && tropasTerrestres >= 8 ? 145f - transportesNavais * 35f : 0f;
                scoreTransporteNaval += bonusAnfibio;
                scoreTransporteNaval += Mathf.Max(0, tropasTerrestres - 8) * 4f;
                scoreTransporteNaval -= economiaFragil ? 55f : 0f;
                AdicionarPedido(pedidos, "navio_transporte", 1800f, scoreTransporteNaval, false, true);
            }

            float scoreNavio = Mathf.Max(0, metaNavios - navios) * 60f + bonusNaval;
            scoreNavio += portaAvioes > 0 ? 20f : 0f;
            scoreNavio += transportesNavais > 0 ? 10f : 0f;
            scoreNavio += bonusEscoltaComboio;
            scoreNavio -= economiaFragil ? 50f : 0f;
            AdicionarPedido(pedidos, "navio", 1500f, scoreNavio, false, true);

            if (biblioteca.ContainsKey("submarino"))
            {
                float scoreSubmarino = Mathf.Max(0, metaSubmarinos - submarinos) * 65f + bonusNaval * 0.8f;
                scoreSubmarino += guerraTotal ? 15f : 0f;
                scoreSubmarino += prepararInvasaoAnfibia ? 20f : 0f;
                scoreSubmarino -= economiaFragil ? 65f : 0f;
                AdicionarPedido(pedidos, "submarino", 2000f, scoreSubmarino, false, true);
            }
        }
        return pedidos;
    }

    void AdicionarPedido(List<PedidoProducao> pedidos, string chave, float custo, float score, bool voa, bool naval)
    {
        if (score > 0f && biblioteca.ContainsKey(chave))
            pedidos.Add(new PedidoProducao { chave = chave, custo = custo, score = score, voa = voa, naval = naval });
    }

    bool ValidarPipelineConstrucao(string chave, Vector3 pos, TipoTerreno terrenoDetectado, float raioObj, out string motivo)
    {
        string chaveNormalizada = NormalizarChave(chave);
        CelulaTatica celula = ObterCelulaTatica(pos);

        if (!PosicaoEmZonaCorreta(chaveNormalizada, pos)) { motivo = "zona incorreta"; return false; }

        bool ehNaval = EhNaval(chaveNormalizada);
        if (ehNaval)
        {
            if (referenciaAgua != null && Mathf.Abs(pos.y - nivelDoMar) < 1.0f) terrenoDetectado = TipoTerreno.Agua;
            
            if (terrenoDetectado != TipoTerreno.Agua) { motivo = "terreno invalido: naval fora da agua"; return false; }
            float margemNaval = Mathf.Max(24f, raioObj + 12f);
            if (referenciaAgua == null && !PosicaoNavalProfundaValida(pos, margemNaval)) { motivo = "agua invalida ou perto demais da costa"; return false; }
        }
        else
        {
            if (terrenoDetectado != TipoTerreno.Terra) { motivo = "terreno invalido: terrestre fora da terra"; return false; }
            if (!ValidarAcessibilidadeTerrestre(pos)) { motivo = "sem acessibilidade terrestre"; return false; }
        }

        Vector3 halfExtents = ObterHalfExtentsCategoria(chaveNormalizada, raioObj);
        if (!FootprintMantemTerrenoValido(chaveNormalizada, pos, halfExtents, terrenoDetectado)) { motivo = "footprint atravessa terreno invalido"; return false; }
        if (!ValidarFootprintLivre(pos, chaveNormalizada, halfExtents)) { motivo = "colisao no footprint"; return false; }
        if (LocalOcupado(pos, raioObj)) { motivo = "ocupado ou sobreposto"; return false; }

        float distanciaPredio = DistanciaParaPredioMaisProximo(pos);
        if (distanciaPredio < Mathf.Max(raioObj * 1.1f, 18f)) { motivo = "distancia minima entre construcoes violada"; return false; }
        if (EhCategoria(chaveNormalizada, "aeroporto") && DistanciaParaImovelMaisProximo(pos) < 200f) { motivo = "aeroporto muito perto de imovel"; return false; }
        if (PosicaoBloqueiaSaidaAliada(pos, chaveNormalizada)) { motivo = "bloqueia saida de estrutura aliada"; return false; }
        if (!ValidarSaidaDaEstrutura(pos, chaveNormalizada)) { motivo = "saida bloqueada"; return false; }

        float risco = celula != null ? celula.ameaca : PenalidadePorAmeaca(pos);
        float limiteDeRisco = EhCategoria(chaveNormalizada, "torreta") || EhCategoria(chaveNormalizada, "antiaerea") ? 160f : 75f;
        if (risco > limiteDeRisco) { motivo = "area ameacada"; return false; }

        motivo = string.Empty;
        return true;
    }

    bool FootprintMantemTerrenoValido(string chave, Vector3 pos, Vector3 halfExtents, TipoTerreno terrenoEsperado)
    {
        Vector3[] offsets = new Vector3[]
        {
            Vector3.zero, new Vector3(halfExtents.x * 0.9f, 0f, halfExtents.z * 0.9f), new Vector3(halfExtents.x * 0.9f, 0f, -halfExtents.z * 0.9f),
            new Vector3(-halfExtents.x * 0.9f, 0f, halfExtents.z * 0.9f), new Vector3(-halfExtents.x * 0.9f, 0f, -halfExtents.z * 0.9f),
            new Vector3(halfExtents.x * 0.9f, 0f, 0f), new Vector3(-halfExtents.x * 0.9f, 0f, 0f),
            new Vector3(0f, 0f, halfExtents.z * 0.9f), new Vector3(0f, 0f, -halfExtents.z * 0.9f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            float altura;
            if (DetectarTerreno(pos + offsets[i], out altura) != terrenoEsperado) return false;
        }
        return true;
    }

    bool ValidarFootprintLivre(Vector3 pos, string chave, Vector3 halfExtents)
    {
        Vector3 volume = new Vector3(Mathf.Max(halfExtents.x, 2f), 18f, Mathf.Max(halfExtents.z, 2f));
        int totalHits = Physics.OverlapBoxNonAlloc(pos, volume, bufferFootprint, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < totalHits; i++)
        {
            Collider hit = bufferFootprint[i];
            if (hit == null || hit is TerrainCollider || ColliderEhAgua(hit)) continue;
            if (hit.GetComponent<SinalizadorIA>() != null || hit.GetComponentInParent<SinalizadorIA>() != null || hit.GetComponentInParent<MarcadorSuperficieMapa>() != null) continue;

            string nome = hit.gameObject.name.ToLower();
            if (nome == "terra" || nome == "terrain" || nome == "agua") continue;

            if (hit.GetComponentInParent<IdentidadeUnidade>() != null) return false;
            if (hit.GetComponentInParent<NavMeshObstacle>() != null) return false;
        }
        return true;
    }

    bool PosicaoBloqueiaSaidaAliada(Vector3 pos, string chave)
    {
        float raioNovo = CalcularRaioSeguro(chave);
        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            string categoria = NormalizarChave(predio.name);
            bool precisaFluxo = EhCategoria(categoria, "quartel") || EhCategoria(categoria, "fabrica") || EhCategoria(categoria, "aeroporto") ||
                                EhCategoria(categoria, "estaleiro") || EhCategoria(categoria, "pier") || EhCategoria(categoria, "plataforma");
            if (!precisaFluxo) continue;

            float dist = Vector3.Distance(Plano(pos), Plano(predio.transform.position));
            float limite = raioNovo + ObterRaioSaidaCategoria(categoria) + CalcularRaioSeguro(categoria) * 0.35f;
            if (dist < limite) return true;
        }
        return false;
    }

    bool BaseEstaSaturada()
    {
        int estruturas = Contar("prefeitura") + Contar("quartel") + Contar("fabrica") + Contar("refinaria") + Contar("aeroporto") + Contar("torreta") + Contar("antiaerea");
        if (estruturas < 7) return false;

        int livres = 0;
        foreach (var zona in zonas)
        {
            if (zona.tipo == TipoZona.Naval) continue;
            livres += ContarPontosLivresNaZona(zona, 8);
            if (livres >= 6) return false;
        }
        return livres < 6;
    }

    bool TentarAbrirZonaExpansao()
    {
        if (zonas.Count(z => z.tipo == TipoZona.Expansao) >= 2) return false;
        Vector3 centroBase = ObterCentroBase();

        for (float raio = distanciaMinimaEntreBases * 0.55f; raio <= distanciaMinimaEntreBases * 1.35f; raio += 35f)
        {
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = centroBase + new Vector3(Mathf.Cos(ang) * raio, 0f, Mathf.Sin(ang) * raio);
                float altura;
                if (DetectarTerreno(teste, out altura) != TipoTerreno.Terra) continue;
                teste.y = altura;

                if (!ValidarAcessibilidadeTerrestre(teste) || LocalOcupado(teste, 40f)) continue;

                CelulaTatica celula = ObterCelulaTatica(teste);
                float ameaca = celula != null ? celula.ameaca : PenalidadePorAmeaca(teste);
                if (ameaca > 60f) continue;

                if (alvoJogadorBase != null && Vector3.Distance(Plano(teste), Plano(alvoJogadorBase.position)) < 220f) continue;

                if (zonas.Any(z => Vector3.Distance(Plano(z.centro), Plano(teste)) < Mathf.Max(z.raio * 0.8f, 120f))) continue;

                zonas.Add(new ZonaIA(TipoZona.Expansao, teste, Mathf.Max(raioZonaEconomica, 180f)));
                ReconstruirGridTatico(true);
                Log($"Nova zona de expansao aberta em {teste}");
                return true;
            }
        }
        return false;
    }

    int ContarPontosLivresNaZona(ZonaIA zona, int maxAmostras)
    {
        int livres = 0, amostras = 0;
        for (int anel = 1; anel <= 3 && amostras < maxAmostras; anel++)
        {
            float raio = Mathf.Lerp(18f, zona.raio, anel / 3f);
            int particoes = 6 + anel * 4;

            for (int i = 0; i < particoes && amostras < maxAmostras; i++)
            {
                float ang = (360f / particoes) * i * Mathf.Deg2Rad;
                Vector3 pos = zona.centro + new Vector3(Mathf.Cos(ang) * raio, 0f, Mathf.Sin(ang) * raio);
                amostras++;
                float altura;
                if (DetectarTerreno(pos, out altura) != TipoTerreno.Terra) continue;
                pos.y = altura;
                if (!LocalOcupado(pos, 24f) && ValidarAcessibilidadeTerrestre(pos)) livres++;
            }
        }
        return livres;
    }

    bool PodeUsarEstruturaParaProducao(GameObject estrutura)
    {
        if (estrutura == null) return false;
        int id = estrutura.GetInstanceID();
        return !cooldownProducaoEstruturas.ContainsKey(id) || Time.time >= cooldownProducaoEstruturas[id];
    }

    void RegistrarCooldownEstrutura(GameObject estrutura, string chave)
    {
        if (estrutura != null) cooldownProducaoEstruturas[estrutura.GetInstanceID()] = Time.time + ObterCooldownProducao(chave);
    }

    float ObterCooldownProducao(string chave)
    {
        string normal = NormalizarChave(chave);
        if (EhCategoria(normal, "soldado")) return Mathf.Max(2.5f, cooldownProducaoPadrao * 0.8f);
        if (EhCategoria(normal, "transporte") || EhCategoria(normal, "transporte_aereo")) return cooldownProducaoPadrao + 1f;
        if (EhCategoria(normal, "tanque")) return cooldownProducaoPadrao + 2f;
        if (EhCategoria(normal, "helicoptero")) return cooldownProducaoPadrao + 3f;
        if (EhCategoria(normal, "caca")) return cooldownProducaoPadrao + 4f;
        if (EhCategoria(normal, "navio") || EhCategoria(normal, "submarino")) return cooldownProducaoPadrao + 5f;
        return cooldownProducaoPadrao;
    }

    void ReconstruirGridTatico(bool forcar = false)
    {
        if (!usarGridTatico) return;
        if (!forcar && Application.isPlaying && Time.time < proximaAtualizacaoGrid) return;
        if (Application.isPlaying) proximaAtualizacaoGrid = Time.time + Mathf.Max(12f, intervaloAtualizacaoGrid);

        gridTatico.Clear();
        if (zonas.Count == 0)
        {
            AmostrarGridEmZona(ObterCentroBase(), raioGrid);
            return;
        }

        List<ZonaIA> amostras = new List<ZonaIA>();
        foreach (var zona in zonas)
        {
            ZonaIA existente = amostras.FirstOrDefault(a => Vector3.Distance(Plano(a.centro), Plano(zona.centro)) <= Mathf.Max(2f, tamanhoCelula * 0.5f));
            if (existente != null) { existente.raio = Mathf.Max(existente.raio, zona.raio); continue; }
            amostras.Add(new ZonaIA(zona.tipo, zona.centro, zona.raio));
        }

        foreach (var amostra in amostras)
        {
            int raioLocal = Mathf.Max(4, Mathf.CeilToInt(amostra.raio / Mathf.Max(4f, tamanhoCelula)));
            AmostrarGridEmZona(amostra.centro, Mathf.Min(raioLocal, raioGrid + 6));
        }
    }

    void AmostrarGridEmZona(Vector3 centro, int raioLocal)
    {
        int passo = raioLocal > 10 ? 2 : 1;
        for (int x = -raioLocal; x <= raioLocal; x += passo)
        {
            for (int z = -raioLocal; z <= raioLocal; z += passo)
            {
                Vector3 pos = centro + new Vector3(x * tamanhoCelula, 0f, z * tamanhoCelula);
                Vector2Int indice = PosicaoParaIndiceCelula(pos);
                if (!gridTatico.ContainsKey(indice)) gridTatico[indice] = CriarCelulaTatica(pos);
            }
        }
    }

    CelulaTatica CriarCelulaTatica(Vector3 pos)
    {
        float altura;
        TipoTerreno terreno = DetectarTerreno(pos, out altura);
        pos.y = terreno == TipoTerreno.Agua ? nivelDoMar : altura;

        return new CelulaTatica
        {
            posicao = pos, terreno = terreno, agua = terreno == TipoTerreno.Agua, terra = terreno == TipoTerreno.Terra,
            areaAereaValida = true, ocupada = LocalOcupado(pos, Mathf.Max(4f, tamanhoCelula * 0.35f), true),
            navegavel = terreno == TipoTerreno.Agua || ValidarAcessibilidadeTerrestre(pos),
            zona = DescobrirZonaLocal(pos), ameaca = PenalidadePorAmeaca(pos),
            distanciaBase = Vector3.Distance(Plano(pos), Plano(ObterCentroBase())),
            distanciaRecursos = DistanciaParaPontoEconomicoMaisProximo(pos),
            distanciaCosta = terreno == TipoTerreno.Agua ? DistanciaAteTerraMaisProxima(pos, 250f) : DistanciaAteAguaMaisProxima(pos, 250f)
        };
    }

    Vector2Int PosicaoParaIndiceCelula(Vector3 pos)
    {
        float tamanho = Mathf.Max(1f, tamanhoCelula);
        return new Vector2Int(Mathf.RoundToInt(pos.x / tamanho), Mathf.RoundToInt(pos.z / tamanho));
    }

    CelulaTatica ObterCelulaTatica(Vector3 pos)
    {
        if (!usarGridTatico) return CriarCelulaTatica(pos);
        Vector2Int indice = PosicaoParaIndiceCelula(pos);
        if (!gridTatico.ContainsKey(indice)) gridTatico[indice] = CriarCelulaTatica(pos);
        return gridTatico[indice];
    }

    TipoZona DescobrirZonaLocal(Vector3 pos)
    {
        foreach (var zona in zonas) { if (zona.Contem(pos)) return zona.tipo; }
        return TipoZona.Nenhuma;
    }

    float DistanciaParaPontoEconomicoMaisProximo(Vector3 pos)
    {
        float menor = float.MaxValue;
        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            if (!EhCategoria(predio.name, "refinaria") && !EhCategoria(predio.name, "plataforma")) continue;
            float dist = Vector3.Distance(Plano(pos), Plano(predio.transform.position));
            if (dist < menor) menor = dist;
        }
        if (menor < float.MaxValue) return menor;
        if (alvoJogadorEconomia != null) return Vector3.Distance(Plano(pos), Plano(alvoJogadorEconomia.position));
        return Vector3.Distance(Plano(pos), Plano(ObterCentroBase()));
    }

    float DistanciaAteAguaMaisProxima(Vector3 origem, float maxBusca)
    {
        for (float r = 10f; r <= maxBusca; r += 10f)
        {
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 22.5f * Mathf.Deg2Rad;
                Vector3 teste = origem + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float alt;
                if (DetectarTerreno(teste, out alt) == TipoTerreno.Agua) return r;
            }
        }
        return maxBusca;
    }

    bool PosicaoNavalProfundaValida(Vector3 pos, float raioSeguranca)
    {
        float altura;
        if (DetectarTerreno(pos, out altura) != TipoTerreno.Agua) return false;

        float distanciaCosta = DistanciaAteTerraMaisProxima(pos, Mathf.Max(raioSeguranca * 3f, 220f));
        float distanciaMinimaCosta = Mathf.Max(raioSeguranca * 1.25f, 34f);
        if (distanciaCosta < distanciaMinimaCosta) return false;

        float raioInterno = raioSeguranca * 0.55f;
        float raioMedio = raioSeguranca * 0.9f;
        float raioExterno = raioSeguranca * 1.25f;

        for (int i = 0; i < 20; i++)
        {
            float ang = i * 18f * Mathf.Deg2Rad;
            Vector3 interno = pos + new Vector3(Mathf.Cos(ang) * raioInterno, 0f, Mathf.Sin(ang) * raioInterno);
            Vector3 medio = pos + new Vector3(Mathf.Cos(ang) * raioMedio, 0f, Mathf.Sin(ang) * raioMedio);
            Vector3 externo = pos + new Vector3(Mathf.Cos(ang) * raioExterno, 0f, Mathf.Sin(ang) * raioExterno);
            float altInterno, altMedio, altExterno;
            if (DetectarTerreno(interno, out altInterno) != TipoTerreno.Agua) return false;
            if (DetectarTerreno(medio, out altMedio) != TipoTerreno.Agua) return false;
            if (DetectarTerreno(externo, out altExterno) != TipoTerreno.Agua) return false;
        }
        return true;
    }

    Vector3 EncontrarAguaProfundaMaisProxima(Vector3 origem, float raioSeguranca)
    {
        for (float r = Mathf.Max(raioSeguranca, 30f); r <= 320f; r += 18f)
        {
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f * Mathf.Deg2Rad;
                Vector3 teste = origem + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                teste.y = nivelDoMar;
                if (PosicaoNavalProfundaValida(teste, raioSeguranca) && !LocalOcupado(teste, 12f, true)) return teste;
            }
        }
        return Vector3.zero;
    }

    Vector3 ObterHalfExtentsCategoria(string chave, float fallbackRaio)
    {
        string normal = NormalizarChave(chave);
        if (bancoDados.ContainsKey(normal) && bancoDados[normal].footprint != Vector2.zero)
        {
            Vector2 footprint = bancoDados[normal].footprint * 0.5f;
            return new Vector3(Mathf.Max(footprint.x, 1.5f), 1f, Mathf.Max(footprint.y, 1.5f));
        }
        float raio = Mathf.Max(2f, fallbackRaio * 0.65f);
        return new Vector3(raio, 1f, raio);
    }

    float ObterRaioSaidaCategoria(string chave)
    {
        string normal = NormalizarChave(chave);
        if (bancoDados.ContainsKey(normal) && bancoDados[normal].raioSaida > 0f) return bancoDados[normal].raioSaida;
        return raioSaidaFabrica;
    }

    bool EhCategoria(string nome, string chave) { return NormalizarChave(nome) == NormalizarChave(chave); }
    Vector3 Plano(Vector3 pos) { pos.y = 0f; return pos; }

    Color CorZona(TipoZona tipo)
    {
        switch (tipo)
        {
            case TipoZona.Capital: return new Color(1f, 0.9f, 0.2f, 0.55f);
            case TipoZona.Economia: return new Color(0.2f, 1f, 0.2f, 0.55f);
            case TipoZona.Militar: return new Color(1f, 0.35f, 0.35f, 0.55f);
            case TipoZona.Defesa: return new Color(1f, 0.55f, 0.1f, 0.55f);
            case TipoZona.Aerea: return new Color(0.45f, 0.85f, 1f, 0.55f);
            case TipoZona.Naval: return new Color(0.1f, 0.45f, 1f, 0.55f);
            case TipoZona.Expansao: return new Color(0.9f, 0.2f, 1f, 0.55f);
            default: return new Color(1f, 1f, 1f, 0.25f);
        }
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        if (Application.isPlaying && !permitirLogsEmRuntime) return;
        Debug.Log($"[IA_Dominadora][Team {teamID}] {msg}", this);
    }

    void OnDrawGizmos()
    {
        if (!debugGizmos) return;

        if (debugMostrarZonas)
        {
            foreach (var zona in zonas)
            {
                Gizmos.color = CorZona(zona.tipo);
                Gizmos.DrawWireSphere(zona.centro, zona.raio);
                Gizmos.DrawSphere(zona.centro, 4f);
            }
        }

        if (usarGridTatico && Application.isPlaying)
        {
            foreach (var celula in gridTatico.Values)
            {
                Color cor = celula.agua ? new Color(0.1f, 0.45f, 1f, 0.15f) : new Color(0.4f, 0.28f, 0.12f, 0.12f);
                if (celula.ocupada) cor = new Color(1f, 0.2f, 0.2f, 0.22f);
                else if (celula.ameaca > 50f) cor = new Color(1f, 0.55f, 0.1f, 0.18f);

                Gizmos.color = cor;
                Gizmos.DrawCube(celula.posicao + Vector3.up * 0.15f, new Vector3(tamanhoCelula * 0.72f, 0.2f, tamanhoCelula * 0.72f));
            }
        }

        if (debugMostrarCandidatos)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.75f);
            foreach (var pos in debugUltimosCandidatosValidos) Gizmos.DrawSphere(pos + Vector3.up * 2f, 2.2f);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.65f);
            foreach (var pos in debugUltimosCandidatosInvalidos) Gizmos.DrawCube(pos + Vector3.up * 1.5f, Vector3.one * 2.4f);
        }

        if (debugMostrarRejeicoes)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.7f, 0.75f);
            foreach (var rejeicao in rejeicoesRecentes) Gizmos.DrawWireSphere(rejeicao.pos + Vector3.up, 4f);
        }

        if (debugMostrarAlvos)
        {
            if (alvoJogadorBase != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(ObterCentroBase(), alvoJogadorBase.position);
                Gizmos.DrawSphere(alvoJogadorBase.position, 5f);
            }
            if (alvoJogadorEconomia != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(ObterCentroBase(), alvoJogadorEconomia.position);
                Gizmos.DrawSphere(alvoJogadorEconomia.position, 4f);
            }
        }
    }
}





