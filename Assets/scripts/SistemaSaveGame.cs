using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.DEUSA;
using Hegemonia.AI.IA01;
using Hegemonia.AI.IA02;
using Hegemonia.AI.IA03;
using Hegemonia.AI.Shared;
using Hegemonia.RTS;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[Serializable]
public class DadosDoJogo
{
    public int saveVersion = 29;
    public string nomeSave = "Partida";
    public string salvoEmUtc = string.Empty;
    public long creditosJogador = 5000L;
    public int petroleoJogador = 500;
    public int acoJogador = 300;
    public int energiaJogador = 100;
    public int comidaJogador = 500;
    public string mapaAtual = "Mapa_1";
    public string idioma = "pt-BR";
    public string dificuldade = "medio";
    public float tempoJogo;
    public SaveVector3 cameraPosicao;
    public SaveQuaternion cameraRotacao;
    public List<string> itensDesbloqueados = new List<string>();
    public List<SaveEntityData> entidades = new List<SaveEntityData>();
    public List<RTSPersistentFormationSaveData> formacoesTaticas = new List<RTSPersistentFormationSaveData>();
    public List<SaveCidadeAbstrataData> cidadesAbstratas = new List<SaveCidadeAbstrataData>();
    public List<SaveQuartelStateData> estadosQuarteis = new List<SaveQuartelStateData>();
    public List<RescueIncident> incidentesSAR = new List<RescueIncident>();
    public List<SaveTransportManifestData> manifestosTransporte = new List<SaveTransportManifestData>();
    public List<SaveProductionOrderData> filaProducao = new List<SaveProductionOrderData>();
    public List<SaveAiStrategicStateData> estadosIA = new List<SaveAiStrategicStateData>();
    public List<SaveDeusaStateData> estadosDeusa = new List<SaveDeusaStateData>();
    // Nulo em saves legados: evita sobrescrever a cena com listas vazias.
    public SaveGovernoMundialData governoMundial;
    public List<SaveProprietarioTerritorio> territoriosCapturados = new List<SaveProprietarioTerritorio>();
    public List<SaveOcupacaoTerritorial> territoriosOcupados = new List<SaveOcupacaoTerritorial>();
    public List<SaveIA01NationState> estadosIA01 = new List<SaveIA01NationState>();
    public List<SaveIA02NationState> estadosIA02 = new List<SaveIA02NationState>();
    public IAAutoProductionSaveData producaoAutomaticaIA = new IAAutoProductionSaveData();
    public float qgPosX;
    public float qgPosY;
    public float qgPosZ;
    public int totalDias = 1;
    public IndustrialSaveData industria = new IndustrialSaveData();
    public SistemaLogisticaMercadoSaveData logisticaMercado = new SistemaLogisticaMercadoSaveData();
    // Sistema Industrial: salvo permanentemente (perfil mineral nunca muda)
    public List<SavePerfilMineral>  perfisMineral  = new List<SavePerfilMineral>();
    public List<SaveEstoqueMineral> estoquesMineral = new List<SaveEstoqueMineral>();
    public float rtsSimulationTime;
    public int rtsSimulationTick;
    public int rtsMatchResult;
    public int rtsPlayerTeamId = 1;
    public int rtsPrimaryAiTeamId = 2;
    public List<RTSObjectiveSaveData> rtsObjectives = new List<RTSObjectiveSaveData>();
}

[Serializable]
public sealed class SaveQuartelStateData
{
    public string quartelUniqueId;
    public int quartelTeamID = 1;
    public bool usarPainelQuartelUIToolkit = true;
    public bool recolhimentoAutomatico;
    public bool modoDefensivoAtivo;
    public bool treinamentoPassivo = true;
    public bool recrutamentoAutomatico = true;
    public bool treinamentoAutomatico = true;
    public int metaEfetivo = 24;
    public float tempoFormacaoSegundos = 10f;
    public int periodoFolhaDias = 15;
    public int misseisArmazenados;
    public int municaoArmazenada;
    public List<string> soldadosNoDormitorio = new List<string>();
    public List<string> veiculosNoQuartel = new List<string>();
    public int adminUltimoDiaObservado = -1;
    public int adminUltimoDiaRecrutamento = -1;
    public int adminUltimoDiaFolha = -1;
    public int adminProximoDiaFolha = -1;
    public int adminSequenciaRecruta;
    public int adminRecrutasConcluidos;
    public int adminRecrutasRecrutados;
    public long adminFolhaPagaTotal;
    public long adminCustoFolhaDiario;
    public long adminCustoFolhaPeriodo;
    public int adminDiasFolhaPendentes;
    public int adminPerdasRegistradas;
    public List<SaveQuartelRecrutaData> adminRecrutas = new List<SaveQuartelRecrutaData>();
    public List<SaveQuartelPerdaData> adminPerdas = new List<SaveQuartelPerdaData>();
}

[Serializable]
public sealed class SaveQuartelRecrutaData
{
    public string id;
    public int forca;
    public int estado;
    public float progressoSegundos;
    public float tempoTotalSegundos;
    public int diaRecrutamento;
}

[Serializable]
public sealed class SaveQuartelPerdaData
{
    public string unidadeId;
    public string nomeUnidade;
    public int forca;
    public int militares;
    public string motivo;
    public int dia;
}

[Serializable]
public sealed class SaveSlotInfo
{
    public string id;
    public string nome;
    public string mapa;
    public string salvoEmUtc;
    public float tempoJogo;
}

// â”€â”€ SerializaÃ§Ã£o do Perfil GeolÃ³gico â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Serializable]
public class SavePerfilMineral
{
    public int teamId;
    public bool perfilGerado;
    public int ferro;    // AbundanciaMineralNivel como int
    public int cobre;
    public int bauxita;
    public int titanio;
    public int uranio;
    public bool extraindoFerro;
    public bool extraindoCobre;
    public bool extraindoBauxita;
    public bool extraindoTitanio;
    public bool extraindoUranio;
    public bool refinandoAco;
    public bool refinandoCobreEletrolitico;
    public bool refinandoDuraluminio;
    public bool refinandoLigaTitanio;
    public bool refinandoComponentes;
    public bool refinandoUranioEnriquecido;
    public float modificadorIndustrial = 1f;
}

// â”€â”€ SerializaÃ§Ã£o do Estoque Mineral â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Serializable]
public class SaveEstoqueMineral
{
    public int   teamId;
    public float minerioFerro;
    public float minerioCobre;
    public float bauxita;
    public float minerioTitanio;
    public float uranioBruto;
    public float acoEstrutural;
    public float cobreEletrolitico;
    public float duraluminio;
    public float ligaTitanio;
    public float componentesEletronicos;
    public float uranioEnriquecido;
}

[Serializable]
public struct SaveVector3
{
    public float x;
    public float y;
    public float z;

    public SaveVector3(Vector3 value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public struct SaveQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public SaveQuaternion(Quaternion value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
        w = value.w;
    }

    public Quaternion ToQuaternion()
    {
        return new Quaternion(x, y, z, w);
    }
}

[Serializable]
public sealed class SaveCidadeAbstrataData
{
    public string id;
    public string nome;
    public int teamID;
    public string nomePais;
    public SaveVector3 localizacao;
    public int populacaoCivil;
    public int capacidadeHabitacional;
    public int empregosTotais;
    public float desenvolvimento;
    public float importanciaEstrategica;
    public float estadoDano;
    public float producaoEconomicaMensal;
    public float producaoAgricola;
    public float taxaCrescimentoAnual;
    public int populacaoEstagio2;
    public int populacaoEstagio3;
    public int populacaoEstagio4;
    public float scoreIndustrial;
    public float scoreComercial;
    public float scoreAgricola;
    public float scoreTurismo;
    public float scoreLogistica;
    public float scoreEnergia;
    public string identidadePrincipal;
    public float atratividade;
    public bool temAeroporto;
    public bool temPorto;
    public string prefabCidade;
}

[Serializable]
public class SaveEntityData
{
    public string uniqueId;
    public string prefabKey;
    public string nomeCena;
    public bool ativo;
    public SaveVector3 posicao;
    public SaveQuaternion rotacao;
    public SaveVector3 escala;
    public int teamID;
    public string nomeDoPais;
    public TipoUnidade tipoUnidade;
    public bool possuiMilitaresConsumidos;
    public int militaresConsumidos;
    public bool possuiVida;
    public float vidaAtual;
    public float vidaMaxima;
    public bool possuiCombustivel;
    public float combustivelAtual;
    public float capacidadeCombustivel;
    public bool capacidadeCombustivelAtualizada;
    public bool capacidadeNavalAtualizada;
    public OrdemControleUnidade ordemAtual;
    public string ordemMovimentoOwner = string.Empty;
    public string ordemMovimentoId = string.Empty;
    public bool modoCombateAtivo;
    public bool possuiDestino;
    public SaveVector3 ultimoDestino;
    public List<SaveVector3> pontosPatrulha = new List<SaveVector3>();
    public int indicePatrulha;
    public string seguirAlvoId;
    public string baseAereaAtualId;
    public string destinoPousoAereoId;
    public bool destinoManualAereo;
    public bool aeronaveEstavaEmVoo;
    public bool possuiPrazoAeroportoTemporario;
    public bool prazoAeroportoTemporarioAtivo;
    public int diasAeroportoTemporario;
    public int diaExpiracaoAeroportoTemporario;
    public bool possuiCidadeComplexoUrbano;
    public TemaCidade cidadeTema;
    public string cidadeNome;
    public int cidadeTeamId;
    public int cidadeCapacidadeHospitalarInicial;
    public bool cidadePossuiRedeSaudeBasica;
    public bool cidadeParticipaProgressaoGlobal;
    public int cidadeOrdemProgressaoGlobal;
    public int cidadeCapacidadeHabitacional;
    public int cidadePopulacaoResidente;
    public int cidadeQualidadeVida;
    public float cidadeProporcaoComercio;
    public float cidadeRendaPorTrabalhador;
    public float cidadeConsumoEnergiaPorMorador;
    public float cidadeConsumoEnergiaPorEmprego;
    public bool cidadeSemEnergia;
}

[Serializable]
public sealed class SaveTransportManifestData
{
    public string transporteUniqueId;
    public bool veiculo;
    public SaveEntityData entidade;
}

[Serializable]
public class SaveProductionOrderData
{
    public string nomeUnidade;
    public string prefabKey;
    public float tempoTotal;
    public float tempoRestante;
    public bool ehSoldado;
    public bool ehHelicoptero;
    public bool ehNavio;
    public bool ehAviao;
    public bool ehCarrier;
}

[Serializable]
public class SaveAiStrategicStateData
{
    public int teamID;
    public IA03RelatorioSaveData relatorioIA03;
    public SaveIA03MissionStateData estadoMissaoIA03;
    public int strategicPhase;
    public string activeImperialPlan;
    public string activeStrategicTarget;
    public string imperialLastFailure;
    public string imperialPlanSummary;
    public int targetFleet;
    public int targetAircraft;
    public int targetOilTankers;
    public int targetPlatforms;
    public int targetPiers;
    public int targetShipyards;
    public int targetCoastalDefenseShips;
    public int targetRadars;
    public int targetCiws;
    public int playerFleetEstimate;
    public int playerAircraftEstimate;
    public bool weakEmpireRecoveryActive;
}

[Serializable]
public sealed class SaveIA03MissionStateData
{
    public int teamID;
    public string missaoAtivaKey = string.Empty;
    public string missaoAtivaId = string.Empty;
    public string objetivoEstrategico = string.Empty;
    public string nomeMissao = string.Empty;
    public string creatyAtivoId = string.Empty;
    public int equipeAlvoAtiva;
    public string alvoPersistenteId = string.Empty;
    public string idOrdemAtiva = string.Empty;
    public int estadoMissao;
    public float tempoDecorridoMissao;
    public float tempoPermanenciaNoDestino;
    public int unidadesReservadas;
    public int unidadesOriginaisNaMissao;
    public bool grupoChegouAoDestino;
    public bool grupoSaiuDoDestino;
    public bool grupoPerdeuUnidade;
    public bool alvoMissaoFoiDefinido;
    public bool alvoMissaoDestruido;
    public bool territorioDoObjetivoCapturado;
    public bool territorioDoObjetivoPerdido;
    public bool confirmacaoExternaDeSucesso;
    public bool confirmacaoExternaDeFracasso;
    public string motivoConfirmacaoExterna = string.Empty;
    public List<string> unidadesAtivasIds = new List<string>();
    public List<string> missoesPendentesKeys = new List<string>();
    public List<string> missoesConcluidasKeys = new List<string>();
}

[Serializable]
public sealed class SaveGovernoMundialData
{
    public int teamJogador = 1;
    public List<DadosPaisGoverno> paises = new List<DadosPaisGoverno>();
    public List<RelacaoPaisGoverno> relacoes = new List<RelacaoPaisGoverno>();
    public List<PropostaInternacional> propostas = new List<PropostaInternacional>();
    public List<string> noticias = new List<string>();
}

[Serializable]
public class SaveDeusaStateData
{
    public int teamID;
    public int personalidade;
    public int modoInicial;
    public int estagioAtual;
    public bool travarEstagio;
    public bool modoObservadorDebug;
    public bool bloquearFilaBrainMasterEmObservador;
    public bool usarEspionagemJusta;
    public bool permitirComercioComJogador;
    public bool permitirComercioComOutrasIAs;
    public bool permitirSancoes;
    public bool permitirGuerraTotal;
    public string nomePais;
    public string nomePresidente;
    public string nomeMoeda;
    public string resumoNacional;
}

public class SistemaSaveGame : MonoBehaviour
{
    public static bool RestauracaoDoMundoEmAndamento { get; private set; }
    public static SistemaSaveGame Instancia;

    public DadosDoJogo dadosAtuais;
    public bool exibirLogsNoConsole = false;
    public bool carregouDeSave = false;
    public bool partidaNovaRecemIniciada { get; private set; }

    private string caminhoDoArquivo;
    private string diretorioSaves;
    private string saveSelecionadoId = string.Empty;
    private bool restauracaoPendente;
    private bool economiaMapaGlobalInicializada;
    [Header("Desempenho de restauracao")]
    [SerializeField, Min(1)] private int entidadesPorFrameNaRestauracao = 12;
    [SerializeField, Min(1)] private int ordensPorFrameNaRestauracao = 24;
    private readonly List<GameObject> bufferObjetos = new List<GameObject>(512);
    private readonly List<GerenciadorAeroporto> bufferAeroportos = new List<GerenciadorAeroporto>(32);
    private readonly Dictionary<string, SaveableEntity> saveablesPorId = new Dictionary<string, SaveableEntity>(StringComparer.Ordinal);
    // Um save costuma conter muitas instancias do mesmo prefab. Cachear tambem
    // os misses evita varredura do catalogo/Resources para cada entidade.
    private readonly Dictionary<string, GameObject> prefabsPorChaveSave = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public static SistemaSaveGame GarantirInstancia()
    {
        if (Instancia != null)
        {
            return Instancia;
        }

        GameObject objetoSave = new GameObject("SistemaSaveGame");
        return objetoSave.AddComponent<SistemaSaveGame>();
    }

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        diretorioSaves = Path.Combine(Application.persistentDataPath, "Saves");
        Directory.CreateDirectory(diretorioSaves);
        caminhoDoArquivo = Path.Combine(Application.persistentDataPath, "save_partida.json");
        SceneManager.sceneLoaded += AoCarregarCena;
        InicializarDados();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            RestauracaoDoMundoEmAndamento = false;
            SceneManager.sceneLoaded -= AoCarregarCena;
        }
    }

    private void InicializarDados()
    {
        partidaNovaRecemIniciada = false;

        if (PossuiSave())
        {
            // Nao restauramos automaticamente a campanha ao iniciar a aplicacao.
            // O save deve ser carregado apenas por acao explicita do jogador.
            dadosAtuais = new DadosDoJogo();
            GarantirColecoesIA01();
            GarantirColecoesIA02();
            carregouDeSave = false;
            restauracaoPendente = false;
            AplicarIdiomaSalvo();
            AplicarDificuldadeSalva();
            return;
        }

        dadosAtuais = new DadosDoJogo();
        AplicarIdiomaSalvo();
        AplicarDificuldadeSalva();
    }

    public bool PossuiSave()
    {
        return ListarSaves().Count > 0;
    }

    public IReadOnlyList<SaveSlotInfo> ListarSaves()
    {
        List<SaveSlotInfo> resultado = new List<SaveSlotInfo>();
        List<string> arquivos = new List<string>();
        string legado = Path.Combine(Application.persistentDataPath, "save_partida.json");
        if (File.Exists(legado)) arquivos.Add(legado);
        if (!string.IsNullOrWhiteSpace(diretorioSaves) && Directory.Exists(diretorioSaves))
        {
            arquivos.AddRange(Directory.GetFiles(diretorioSaves, "*.json"));
        }

        for (int i = 0; i < arquivos.Count; i++)
        {
            try
            {
                string arquivo = arquivos[i];
                DadosDoJogo dados = JsonUtility.FromJson<DadosDoJogo>(File.ReadAllText(arquivo));
                if (dados == null) continue;
                resultado.Add(new SaveSlotInfo
                {
                    id = arquivo,
                    nome = string.IsNullOrWhiteSpace(dados.nomeSave)
                        ? (arquivo == legado ? "Partida antiga" : Path.GetFileNameWithoutExtension(arquivo))
                        : dados.nomeSave.Trim(),
                    mapa = dados.mapaAtual,
                    salvoEmUtc = dados.salvoEmUtc,
                    tempoJogo = dados.tempoJogo
                });
            }
            catch (Exception ex)
            {
                LogInfo("Save ignorado por estar invalido: " + arquivos[i] + " | " + ex.Message);
            }
        }

        return resultado
            .OrderByDescending(s => s.salvoEmUtc)
            .ThenBy(s => s.nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool SelecionarSave(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !File.Exists(id)) return false;
        caminhoDoArquivo = id;
        saveSelecionadoId = id;
        return true;
    }

    public bool TentarCarregarSave(string id)
    {
        return SelecionarSave(id) && TentarCarregarJogo();
    }

    public bool RenomearSave(string id, string novoNome)
    {
        if (string.IsNullOrWhiteSpace(id) || !File.Exists(id) || string.IsNullOrWhiteSpace(novoNome)) return false;
        try
        {
            DadosDoJogo dados = JsonUtility.FromJson<DadosDoJogo>(File.ReadAllText(id));
            if (dados == null) return false;
            dados.nomeSave = NormalizarNomeSave(novoNome);
            File.WriteAllText(id, JsonUtility.ToJson(dados, true));
            if (string.Equals(caminhoDoArquivo, id, StringComparison.OrdinalIgnoreCase)) dadosAtuais = dados;
            return true;
        }
        catch (Exception ex)
        {
            LogInfo("Falha ao renomear save: " + ex.Message);
            return false;
        }
    }

    public bool ExcluirSave(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !File.Exists(id)) return false;
        try
        {
            File.Delete(id);
            if (string.Equals(caminhoDoArquivo, id, StringComparison.OrdinalIgnoreCase))
            {
                caminhoDoArquivo = Path.Combine(Application.persistentDataPath, "save_partida.json");
                saveSelecionadoId = string.Empty;
            }
            return true;
        }
        catch (Exception ex)
        {
            LogInfo("Falha ao excluir save: " + ex.Message);
            return false;
        }
    }

    public void SalvarJogo(string nomeSave)
    {
        string nome = NormalizarNomeSave(nomeSave);
        if (string.IsNullOrWhiteSpace(saveSelecionadoId) || !File.Exists(saveSelecionadoId))
        {
            string id = "save_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json";
            caminhoDoArquivo = Path.Combine(diretorioSaves, id);
            saveSelecionadoId = caminhoDoArquivo;
        }

        if (dadosAtuais == null) dadosAtuais = new DadosDoJogo();
        dadosAtuais.nomeSave = nome;
        SalvarJogo();
    }

    public void SalvarJogo()
    {
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
        }

        GarantirColecoesIA01();
        GarantirColecoesIA02();
        GarantirColecoesCidadesAbstratas();
        dadosAtuais.saveVersion = 29;
        dadosAtuais.nomeSave = NormalizarNomeSave(dadosAtuais.nomeSave);
        dadosAtuais.salvoEmUtc = DateTime.UtcNow.ToString("O");
        RegistrarCenaAtual(SceneManager.GetActiveScene().name);
        CapturarRecursos();
        CapturarCamera();
        CapturarIdioma();
        CapturarDificuldade();
        CapturarFilaProducao();
        CapturarEstadoGovernoMundial();
        CapturarEstadoTerritorial();
        CapturarCidadesAbstratas();
        CapturarEstadoIA01();
        CapturarEstadoIA02();
        CapturarProducaoAutomaticaIA();
        CapturarEstadoDeusa();
        CapturarEstadoQuarteis();
        CapturarEstadoSAR();
        CapturarEntidades();
        // A captura de entidades garante IDs estáveis para unidades e alvos da IA03.
        CapturarEstadoIAImperial();
        CapturarFormacoesTaticas();
        CapturarManifestosTransporte();
        CapturarSistemaIndustrial();
        CapturarSistemaLogisticaMercado();
        CapturarRTSState();
        dadosAtuais.totalDias = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : dadosAtuais.totalDias;

        string json = JsonUtility.ToJson(dadosAtuais, true);
        File.WriteAllText(caminhoDoArquivo, json);
        LogInfo("Jogo salvo com sucesso em: " + caminhoDoArquivo);
    }

    public void CarregarJogo()
    {
        if (string.IsNullOrWhiteSpace(caminhoDoArquivo) || !File.Exists(caminhoDoArquivo))
        {
            SaveSlotInfo primeiro = ListarSaves().FirstOrDefault();
            if (primeiro != null) SelecionarSave(primeiro.id);
        }

        if (string.IsNullOrWhiteSpace(caminhoDoArquivo) || !File.Exists(caminhoDoArquivo))
        {
            dadosAtuais = new DadosDoJogo();
            GerenteDeTerritorio.Instancia?.RestaurarProprietariosCapturados(null);
            IAAutoProductionRegistry.Clear();
            carregouDeSave = false;
            partidaNovaRecemIniciada = false;
            return;
        }

        string json = File.ReadAllText(caminhoDoArquivo);
        dadosAtuais = JsonUtility.FromJson<DadosDoJogo>(json);
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
            IAAutoProductionRegistry.Clear();
            carregouDeSave = false;
            partidaNovaRecemIniciada = false;
            return;
        }

        GarantirColecoesIA01();
        GarantirColecoesIA02();
        GarantirColecoesCidadesAbstratas();
        bool saveLegadoAntesDaVersao12 = dadosAtuais.saveVersion < 12;
        if (dadosAtuais.saveVersion <= 0)
        {
            dadosAtuais.saveVersion = 1;
        }

        if (dadosAtuais.totalDias <= 0)
        {
            dadosAtuais.totalDias = 1;
        }

        // Remove filas da sessão que está sendo substituída. O estado salvo
        // volta a entrar só depois da restauração de Governo e armazéns.
        SistemaLogisticaMercado.Instancia?.RestaurarSaveData(null);
        MigrarIndustriaLegadaSeNecessario();

        carregouDeSave = true;
        partidaNovaRecemIniciada = false;
        restauracaoPendente = PossuiEstadoRestauravel(dadosAtuais);
        // Limpa ordens da sessão anterior imediatamente. A restauração tardia
        // continua sendo repetida após a cena para manter o mesmo ponto de
        // sincronização das demais ordens salvas.
        RestaurarRegistroProducaoAutomaticaIA(notificarIA03: false);
        AplicarIdiomaSalvo();
        AplicarDificuldadeSalva();
        AplicarRecursosSalvos();
        AplicarTempoSalvo();
        AplicarRTSState();
        AplicarEstadoTerritorial();
        if (dadosAtuais.saveVersion >= 19 && GerenciadorDivisaoTerritorial.Instancia != null)
            RestaurarCidadesAbstratas(criarGerenciadorSeAusente: false);
        if (saveLegadoAntesDaVersao12)
        {
            // JsonUtility converte o numero antigo para long sem arredondamento.
            // A versao 12 sera gravada no proximo SalvarJogo, mantendo saves antigos carregaveis.
            LogInfo("Save legado carregado e preparado para migracao monetaria para long (versao " + dadosAtuais.saveVersion + ").");
        }
        LogInfo("Jogo carregado com sucesso.");
    }

    private static bool PossuiEstadoRestauravel(DadosDoJogo dados)
    {
        if (dados == null || dados.saveVersion < 2) return false;

        bool possuiDadosIndustriais = dados.industria != null
            && (TemItens(dados.industria.perfisMineral)
                || TemItens(dados.industria.estoques)
                || TemItens(dados.industria.ordensExtracao)
                || TemItens(dados.industria.ordensRefino)
                || TemItens(dados.industria.linhas)
                || TemItens(dados.industria.impactosPendentes)
                || TemItens(dados.industria.historico));
        bool possuiEstadoRts = TemItens(dados.rtsObjectives)
            || dados.rtsSimulationTime > 0f
            || dados.rtsSimulationTick != 0
            || dados.rtsMatchResult != 0
            || dados.tempoJogo > 0f;
        bool possuiProducaoAutomatica = dados.producaoAutomaticaIA != null
            && TemItens(dados.producaoAutomaticaIA.orders);
        bool possuiCameraSalva = dados.cameraPosicao.x != 0f
            || dados.cameraPosicao.y != 0f
            || dados.cameraPosicao.z != 0f
            || dados.cameraRotacao.x != 0f
            || dados.cameraRotacao.y != 0f
            || dados.cameraRotacao.z != 0f
            || dados.cameraRotacao.w != 0f;

        return TemItens(dados.entidades)
            || TemItens(dados.estadosIA01)
            || TemItens(dados.estadosIA02)
            || TemItens(dados.estadosIA)
            || TemItens(dados.estadosDeusa)
            || TemItens(dados.estadosQuarteis)
            || TemItens(dados.incidentesSAR)
            || TemItens(dados.manifestosTransporte)
            || TemItens(dados.filaProducao)
            || possuiProducaoAutomatica
            || TemItens(dados.territoriosCapturados)
            || (dados.saveVersion >= 28 && TemItens(dados.territoriosOcupados))
            || TemItens(dados.perfisMineral)
            || TemItens(dados.estoquesMineral)
            || dados.governoMundial != null
            || possuiDadosIndustriais
            || dados.totalDias > 1
            || possuiEstadoRts
            || possuiCameraSalva
            || (dados.saveVersion >= 19 && TemItens(dados.cidadesAbstratas))
            || (dados.saveVersion >= 22 && dados.logisticaMercado != null
                && (TemItens(dados.logisticaMercado.pedidos) || TemItens(dados.logisticaMercado.repeticoes)));
    }

    private static bool TemItens<T>(List<T> itens)
    {
        return itens != null && itens.Count > 0;
    }

    private static bool DeveSubstituirEntidadesDaCena(DadosDoJogo dados)
    {
        return dados != null && TemItens(dados.entidades);
    }

    public void IniciarNovoJogo(string cenaInicial = null)
    {
        dadosAtuais = new DadosDoJogo();
        SistemaGuardaCosteira.GarantirInstancia();
        SistemaGuardaCosteira.Instancia?.LimparEstadoParaNovoJogo();
        SistemaLogisticaMercado.Instancia?.LimparEstadoParaNovoJogo();
        GerenteDeTerritorio.Instancia?.RestaurarProprietariosCapturados(null);
        IAAutoProductionRegistry.Clear();
        saveSelecionadoId = string.Empty;
        caminhoDoArquivo = Path.Combine(Application.persistentDataPath, "save_partida.json");
        GarantirColecoesIA01();
        dadosAtuais.idioma = LocalizationManager.Instancia.ObterCodigoIdioma();
        PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
        dadosAtuais.dificuldade = perfil.Codigo;
        dadosAtuais.creditosJogador = perfil.DinheiroInicial;
        carregouDeSave = false;
        partidaNovaRecemIniciada = true;
        economiaMapaGlobalInicializada = false;
        restauracaoPendente = false;
        RegistrarCenaAtual(string.IsNullOrWhiteSpace(cenaInicial) ? dadosAtuais.mapaAtual : cenaInicial);
        if (GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.dinheiro = perfil.DinheiroInicial;
            GerenciadorRecursos.Instancia.NotificarAtualizacao();
        }
        Debug.Log("[NovoJogo] modo=" + perfil.Codigo + " | dinheiro inicial=" + perfil.DinheiroInicial + " | cena=" + dadosAtuais.mapaAtual);
        LogInfo("Novo jogo iniciado com dados reiniciados.");
    }

    public bool TentarCarregarJogo()
    {
        if (!PossuiSave())
        {
            return false;
        }

        CarregarJogo();
        return true;
    }

    public void RegistrarCenaAtual(string nomeCena)
    {
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
        }

        if (!string.IsNullOrWhiteSpace(nomeCena))
        {
            dadosAtuais.mapaAtual = nomeCena;
        }
    }

    public string ObterCenaSalvaOuPadrao(string cenaPadrao)
    {
        if (dadosAtuais == null || string.IsNullOrWhiteSpace(dadosAtuais.mapaAtual))
        {
            return cenaPadrao;
        }

        return ConfiguracaoCenasJogo.NormalizarCenaCampanha(dadosAtuais.mapaAtual, cenaPadrao);
    }

    public void ApagarDados()
    {
        // O save antigo ficava fora de /Saves e o botao apagava apenas o
        // caminho atualmente selecionado. Assim, um slot antigo continuava
        // sendo listado e podia reintroduzir estruturas corrompidas.
        List<string> arquivosParaApagar = new List<string>();
        string legado = Path.Combine(Application.persistentDataPath, "save_partida.json");
        if (File.Exists(legado)) arquivosParaApagar.Add(legado);
        if (!string.IsNullOrWhiteSpace(diretorioSaves) && Directory.Exists(diretorioSaves))
        {
            arquivosParaApagar.AddRange(Directory.GetFiles(diretorioSaves, "*.json"));
        }

        for (int i = 0; i < arquivosParaApagar.Count; i++)
        {
            try
            {
                File.Delete(arquivosParaApagar[i]);
            }
            catch (Exception ex)
            {
                LogInfo("Falha ao apagar save " + arquivosParaApagar[i] + ": " + ex.Message);
            }
        }

        dadosAtuais = new DadosDoJogo();
        GerenteDeTerritorio.Instancia?.RestaurarProprietariosCapturados(null);
        IAAutoProductionRegistry.Clear();
        caminhoDoArquivo = legado;
        saveSelecionadoId = string.Empty;
        carregouDeSave = false;
        partidaNovaRecemIniciada = false;
        restauracaoPendente = false;
        LogInfo("Todos os saves foram apagados do computador. Comecando do zero.");
    }

    private static string NormalizarNomeSave(string nome)
    {
        string resultado = string.IsNullOrWhiteSpace(nome) ? "Partida" : nome.Trim();
        foreach (char invalido in Path.GetInvalidFileNameChars()) resultado = resultado.Replace(invalido, ' ');
        return resultado.Length > 48 ? resultado.Substring(0, 48).Trim() : resultado;
    }

    public void ConsumirMarcadorPartidaNova()
    {
        partidaNovaRecemIniciada = false;
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        // Capture before sanitization: it can consume the new-game marker,
        // but the selected starting resources still need to be applied.
        bool aplicarRecursosDePartidaNova = !carregouDeSave && partidaNovaRecemIniciada;

        if (aplicarRecursosDePartidaNova)
        {
            SanitizarCenaDePartidaNova(cena);
        }

        if (!carregouDeSave && !aplicarRecursosDePartidaNova)
        {
            // Ao iniciar uma cena diretamente, preserve os recursos definidos
            // no proprio mapa em vez de aplicar o objeto de save vazio (5.000).
            // GlobalMapRTS e um modo jogavel autonomo: sem save carregado, sua
            // primeira entrada precisa criar o estoque nacional usado pelas
            // unidades iniciais e pelos spawns da IA.
            if (string.Equals(cena.name, ConfiguracaoCenasJogo.CenaMapaGlobal, StringComparison.OrdinalIgnoreCase)
                && !economiaMapaGlobalInicializada)
            {
                InicializarEconomiaDePartidaNova();
            }

            return;
        }

        AplicarRecursosSalvos();
        if (!carregouDeSave)
        {
            if (aplicarRecursosDePartidaNova)
            {
                InicializarEconomiaDePartidaNova();
            }

            return;
        }

        if (restauracaoPendente && dadosAtuais != null && dadosAtuais.saveVersion >= 2)
        {
            StartCoroutine(RestaurarMundoDepoisDaCena());
        }
    }

    private void InicializarEconomiaDePartidaNova()
    {
        AtivarAutonomiaDeValdoriaEmPartidaNova();
        SistemaGovernoMundial.GarantirInstancia();
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        {
            Debug.LogError("[NovoJogo] Não foi possível inicializar o tesouro nacional: SistemaGovernoMundial indisponível.");
            return;
        }

        int playerTeamId = GerenciadorDePartida.Instancia != null
            ? Mathf.Max(1, GerenciadorDePartida.Instancia.idJogador)
            : Mathf.Max(1, governo.teamJogador);
        HashSet<int> teamsComEstoqueInicial = new HashSet<int> { playerTeamId };
        IA_BrainMaster[] brains = FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
        for (int i = 0; i < brains.Length; i++)
        {
            IA_BrainMaster brain = brains[i];
            if (brain != null && brain.isActiveAndEnabled && brain.gameObject.activeInHierarchy && brain.TeamId > 0)
                teamsComEstoqueInicial.Add(brain.TeamId);
        }

        int iasFinanciadas = governo.AplicarSaldoInicialPartidaNovaParaIAsAtivas(playerTeamId);
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
            industrial = FindFirstObjectByType<SistemaIndustrialNacional>();
        if (industrial == null)
        {
            Debug.LogError("[NovoJogo] Não foi possível inicializar os combustíveis nacionais: SistemaIndustrialNacional indisponível.");
            return;
        }

        industrial.InicializarEstoquesCombustivelPartidaNova(teamsComEstoqueInicial);
        int iasComComidaInicial = governo.AplicarComidaInicialPartidaNovaParaIAsAtivas(playerTeamId);
        Debug.Log("[NovoJogo] Tesouro inicial aplicado a " + iasFinanciadas
            + " IA(s) ativa(s); comida inicial de " + iasComComidaInicial + " IA(s) definida para "
            + SistemaGovernoMundial.ComidaInicialNacionalPorIAEmPartidaNova
            + "; estoques de combustíveis iniciais aplicados aos times "
            + string.Join(",", teamsComEstoqueInicial) + ".");
        economiaMapaGlobalInicializada = true;
    }

    private static void AtivarAutonomiaDeValdoriaEmPartidaNova()
    {
        IA_BrainMaster[] brains = FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
        for (int i = 0; i < brains.Length; i++)
        {
            IA_BrainMaster brain = brains[i];
            if (brain == null || !brain.isActiveAndEnabled || brain.TeamId != 2)
            {
                continue;
            }

            IA_DeusaBrain deusa = brain.GetComponent<IA_DeusaBrain>();
            if (deusa == null)
            {
                deusa = brain.gameObject.AddComponent<IA_DeusaBrain>();
            }

            if (deusa.config == null)
            {
                deusa.config = new IA_DeusaConfig();
            }

            deusa.config.modoObservadorDebug = false;
            if (deusa.politicaNacional != null)
            {
                deusa.politicaNacional.modoObservador = false;
            }

            Debug.Log("[NovoJogo][Valdoria] DEUSA ativada em modo autônomo; autoridade de construção permanece no BrainMaster.", brain);
            return;
        }

        Debug.LogWarning("[NovoJogo][Valdoria] BrainMaster team 2 não foi encontrado; autonomia não foi aplicada.");
    }

    private IEnumerator RestaurarMundoDepoisDaCena()
    {
        restauracaoPendente = false;
        RestauracaoDoMundoEmAndamento = true;
        yield return null;
        yield return null;

        bool possuiEntidadesSalvas = DeveSubstituirEntidadesDaCena(dadosAtuais);
        if (possuiEntidadesSalvas)
        {
            LimparEntidadesPersistidasDaCena();
        }
        saveablesPorId.Clear();
        prefabsPorChaveSave.Clear();

        if (possuiEntidadesSalvas)
        {
            float inicioInstanciacao = Time.realtimeSinceStartup;
            int loteEntidades = Mathf.Max(1, entidadesPorFrameNaRestauracao);
            for (int i = 0; i < dadosAtuais.entidades.Count; i++)
            {
                InstanciarEntidadeSalva(dadosAtuais.entidades[i]);
                if ((i + 1) % loteEntidades == 0)
                {
                    yield return null;
                }
            }
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("save_restore_spawn_ms", (Time.realtimeSinceStartup - inicioInstanciacao) * 1000f);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("save_restore_entities", dadosAtuais.entidades.Count);

            yield return null;
        }
        else
        {
            MapearSaveablesDaCenaAtiva();
        }

        RestaurarManifestosTransporte();
        RestaurarBasesAereasSalvas();
        RestaurarEstadoQuarteis();

        if (possuiEntidadesSalvas)
        {
            float inicioOrdens = Time.realtimeSinceStartup;
            int loteOrdens = Mathf.Max(1, ordensPorFrameNaRestauracao);
            for (int i = 0; i < dadosAtuais.entidades.Count; i++)
            {
                RestaurarOrdemEntidade(dadosAtuais.entidades[i]);
                if ((i + 1) % loteOrdens == 0)
                {
                    yield return null;
                }
            }
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("save_restore_orders_ms", (Time.realtimeSinceStartup - inicioOrdens) * 1000f);
        }

        RTSPersistentFormationService.GarantirInstancia().RestaurarEstado(
            dadosAtuais.formacoesTaticas,
            saveablesPorId);

        RestaurarFilaProducao();
        AplicarEstadoGovernoMundial();
        if (dadosAtuais.saveVersion >= 19) RestaurarCidadesAbstratas(criarGerenciadorSeAusente: true);
        RestaurarDadosCidadesSalvas();
        AplicarEstadoTerritorial();
        SistemaGovernoMundial.Instancia?.ReconciliarAcordosTemporarios();
        AplicarEstadoIAImperial();
        AplicarEstadoDeusa();
        RestaurarEstadoIA01();
        RestaurarEstadoIA02();
        SistemaGuardaCosteira.GarantirInstancia();
        SistemaGuardaCosteira.Instancia?.RestaurarEstadoSave(dadosAtuais.incidentesSAR);
        RestaurarProducaoAutomaticaIA();
        AplicarCameraSalva();
        AplicarRecursosSalvos();
        AplicarTempoSalvo();
        AplicarRTSState();
        RestaurarSistemaIndustrial();
        RestaurarSistemaLogisticaMercado();
        DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Mundo restaurado (entidades=" + (dadosAtuais.entidades != null ? dadosAtuais.entidades.Count : 0) + ")");
        RestauracaoDoMundoEmAndamento = false;
    }

    private void SanitizarCenaDePartidaNova(Scene cena)
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cena.name))
        {
            return;
        }

        // GlobalMapRTS contem unidades de cenario colocadas intencionalmente
        // (por exemplo, a forca inicial da Valdoria). Elas nao sao restos de
        // save; preserva-las tambem permite que recebam o combustivel inicial.
        if (string.Equals(cena.name, ConfiguracaoCenasJogo.CenaMapaGlobal, StringComparison.OrdinalIgnoreCase))
        {
            partidaNovaRecemIniciada = false;
            return;
        }

        int unidadesRemovidas = 0;
        int estruturasIARemovidas = 0;
        int comandantesRemovidos = 0;
        int entidadesOperacionaisSemIdentidadeRemovidas = 0;
        HashSet<GameObject> objetosRemovidos = new HashSet<GameObject>();

        IdentidadeUnidade[] unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            IdentidadeUnidade unidade = unidades[i];
            if (unidade == null || unidade.teamID <= 0 || unidade.gameObject.scene != cena)
            {
                continue;
            }

            // Partida nova nao deve herdar creates da IA deixados por um save,
            // pelo editor ou por uma tentativa anterior de abertura. Os slots
            // oficiais continuam na cena; somente a estrutura ja instanciada e
            // marcada com team de IA e removida.
            if (unidade.tipoUnidade == TipoUnidade.Estrutura && unidade.teamID > 1)
            {
                Destroy(unidade.gameObject);
                objetosRemovidos.Add(unidade.gameObject);
                estruturasIARemovidas++;
                continue;
            }

            if (unidade.tipoUnidade == TipoUnidade.Estrutura)
            {
                continue;
            }

            Destroy(unidade.gameObject);
            objetosRemovidos.Add(unidade.gameObject);
            unidadesRemovidas++;
        }

        entidadesOperacionaisSemIdentidadeRemovidas = LimparEntidadesOperacionaisSemIdentidade(cena, objetosRemovidos);

        IdentidadeIA[] identidadesIA = FindObjectsByType<IdentidadeIA>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < identidadesIA.Length; i++)
        {
            IdentidadeIA identidade = identidadesIA[i];
            if (identidade == null || identidade.gameObject.scene != cena)
            {
                continue;
            }

            bool pareceComandanteDeTeste =
                (!string.IsNullOrWhiteSpace(identidade.biografia) && identidade.biografia.Contains("testes militares"))
                || identidade.name.StartsWith("IA_", StringComparison.OrdinalIgnoreCase)
                || identidade.teamID > 1;

            if (!pareceComandanteDeTeste)
            {
                continue;
            }

            Destroy(identidade.gameObject);
            objetosRemovidos.Add(identidade.gameObject);
            comandantesRemovidos++;
        }

        partidaNovaRecemIniciada = false;
        DiagnosticoDesempenhoJogo.RegistrarEvento("Partida", "Sanitizacao de partida nova (unidades=" + unidadesRemovidas + ", estruturasIA=" + estruturasIARemovidas + ", semIdentidade=" + entidadesOperacionaisSemIdentidadeRemovidas + ", comandantes=" + comandantesRemovidos + ")");
        LogInfo("Sanitizacao de partida nova concluida. Unidades removidas=" + unidadesRemovidas + " | estruturas IA removidas=" + estruturasIARemovidas + " | entidades sem identidade removidas=" + entidadesOperacionaisSemIdentidadeRemovidas + " | comandantes removidos=" + comandantesRemovidos + ".");
    }

    private int LimparEntidadesOperacionaisSemIdentidade(Scene cena, HashSet<GameObject> objetosRemovidos)
    {
        HashSet<GameObject> candidatos = new HashSet<GameObject>();
        ControleUnidade[] controles = FindObjectsByType<ControleUnidade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controles.Length; i++)
        {
            ControleUnidade controle = controles[i];
            if (controle == null || controle.gameObject.scene != cena || controle.gameObject == gameObject
                || objetosRemovidos.Contains(controle.gameObject)
                || controle.GetComponent<IdentidadeUnidade>() != null
                || PareceMarcadorOuPontoDeSpawn(controle.gameObject))
            {
                continue;
            }

            candidatos.Add(controle.gameObject);
        }

        IdentidadeNaval[] identidadesNavais = FindObjectsByType<IdentidadeNaval>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < identidadesNavais.Length; i++)
        {
            IdentidadeNaval identidadeNaval = identidadesNavais[i];
            if (identidadeNaval == null || identidadeNaval.gameObject.scene != cena || identidadeNaval.gameObject == gameObject
                || objetosRemovidos.Contains(identidadeNaval.gameObject)
                || identidadeNaval.GetComponent<IdentidadeUnidade>() != null
                || PareceMarcadorOuPontoDeSpawn(identidadeNaval.gameObject))
            {
                continue;
            }

            candidatos.Add(identidadeNaval.gameObject);
        }

        // Tambem existem restos visuais colocados diretamente na cena, sem
        // qualquer componente de gameplay ou SaveableEntity. Eles nao podem
        // sobreviver a um novo jogo se tiverem nome inequivocamente militar;
        // marcadores e pontos de spawn sao excluidos antes dessa classificacao.
        Renderer[] visuais = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < visuais.Length; i++)
        {
            Renderer visual = visuais[i];
            if (visual == null || visual.gameObject.scene != cena || visual.gameObject == gameObject
                || objetosRemovidos.Contains(visual.gameObject)
                || visual.GetComponentInParent<IdentidadeUnidade>() != null
                || visual.GetComponentInParent<ControleUnidade>() != null
                || visual.GetComponentInParent<IdentidadeNaval>() != null
                || visual.GetComponentInParent<SaveableEntity>() != null
                || PareceMarcadorOuPontoDeSpawn(visual.gameObject)
                || !PareceVisualMilitarAntigo(visual.gameObject))
            {
                continue;
            }

            candidatos.Add(visual.gameObject);
        }

        // Saves antigos tambem podem ter deixado apenas o componente de save
        // em um visual de unidade. Nao removemos qualquer SaveableEntity
        // indiscriminadamente: somente clones/nomes inequivocamente militares,
        // sem identidade, controle naval ou marcador de cena.
        SaveableEntity[] saveables = FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < saveables.Length; i++)
        {
            SaveableEntity saveable = saveables[i];
            if (saveable == null || saveable.gameObject.scene != cena || saveable.gameObject == gameObject
                || objetosRemovidos.Contains(saveable.gameObject)
                || saveable.GetComponent<IdentidadeUnidade>() != null
                || saveable.GetComponent<ControleUnidade>() != null
                || saveable.GetComponent<IdentidadeNaval>() != null
                || PareceMarcadorOuPontoDeSpawn(saveable.gameObject)
                || !PareceVisualMilitarAntigo(saveable.gameObject))
            {
                continue;
            }

            candidatos.Add(saveable.gameObject);
        }

        foreach (GameObject candidato in candidatos)
        {
            if (candidato == null || objetosRemovidos.Contains(candidato))
            {
                continue;
            }

            Destroy(candidato);
            objetosRemovidos.Add(candidato);
        }

        return candidatos.Count;
    }

    private static bool PareceMarcadorOuPontoDeSpawn(GameObject objeto)
    {
        if (objeto == null)
        {
            return true;
        }

        string nome = objeto.name.ToLowerInvariant();
        string[] tokens =
        {
            "marker", "marcador", "spawn", "create", "slot", "patrulha", "patrol",
            "route", "rota", "target", "alvo", "position", "posicao", "entry",
            "entrada", "exit", "saida", "parking", "estacionamento", "escape", "fuga", "hide"
        };

        for (int i = 0; i < tokens.Length; i++)
        {
            if (nome.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PareceVisualMilitarAntigo(GameObject objeto)
    {
        if (objeto == null)
        {
            return false;
        }

        string nome = objeto.name.ToLowerInvariant();
        string[] tokens =
        {
            "soldado", "soldier", "rifle", "tank", "tanque", "blindado", "artilharia",
            "ares", "su11", "sr71", "fighter", "bomber", "aviao", "aircraft", "helicopter",
            "destroyer", "submarine", "submarino", "carrier", "portaavioes", "navio", "ship", "f200"
        };

        for (int i = 0; i < tokens.Length; i++)
        {
            if (nome.Contains(tokens[i]))
            {
                return true;
            }
        }

        return nome.Contains("(clone)") || nome.Contains(" clone");
    }

    private void CapturarRecursos()
    {
        if (GerenciadorRecursos.Instancia == null)
        {
            return;
        }

        dadosAtuais.creditosJogador = GerenciadorRecursos.Instancia.dinheiro;
        dadosAtuais.petroleoJogador = GerenciadorRecursos.Instancia.petroleo;
        dadosAtuais.acoJogador = GerenciadorRecursos.Instancia.aco;
        dadosAtuais.energiaJogador = GerenciadorRecursos.Instancia.energia;
        dadosAtuais.comidaJogador = GerenciadorRecursos.Instancia.comida;
    }

    private void AplicarRecursosSalvos()
    {
        if (dadosAtuais == null || GerenciadorRecursos.Instancia == null)
        {
            return;
        }

        GerenciadorRecursos.Instancia.dinheiro = dadosAtuais.creditosJogador;
        GerenciadorRecursos.Instancia.petroleo = dadosAtuais.petroleoJogador;
        GerenciadorRecursos.Instancia.aco = dadosAtuais.acoJogador;
        GerenciadorRecursos.Instancia.energia = dadosAtuais.energiaJogador;
        GerenciadorRecursos.Instancia.comida = dadosAtuais.comidaJogador;
    }

    private void CapturarIdioma()
    {
        dadosAtuais.idioma = LocalizationManager.Instancia.ObterCodigoIdioma();
    }

    private void CapturarDificuldade()
    {
        dadosAtuais.dificuldade = GameDifficultyManager.Instancia.ObterCodigoDificuldade();
    }

    private void AplicarIdiomaSalvo()
    {
        if (dadosAtuais != null && !string.IsNullOrWhiteSpace(dadosAtuais.idioma))
        {
            LocalizationManager.Instancia.AplicarCodigo(dadosAtuais.idioma);
        }
    }

    private void AplicarDificuldadeSalva()
    {
        string codigo = dadosAtuais != null ? dadosAtuais.dificuldade : null;
        GameDifficultyManager.Instancia.AplicarCodigo(string.IsNullOrWhiteSpace(codigo) ? "normal" : codigo);
    }

    private void CapturarCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        dadosAtuais.cameraPosicao = new SaveVector3(cam.transform.position);
        dadosAtuais.cameraRotacao = new SaveQuaternion(cam.transform.rotation);
    }

    private void AplicarCameraSalva()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.transform.position = dadosAtuais.cameraPosicao.ToVector3();
        cam.transform.rotation = dadosAtuais.cameraRotacao.ToQuaternion();
    }

    private void CapturarFilaProducao()
    {
        dadosAtuais.filaProducao.Clear();
        if (GerenteDeJogo.Instancia == null || GerenteDeJogo.Instancia.filaProducao == null)
        {
            return;
        }

        foreach (GerenteDeJogo.PedidoDeProducao pedido in GerenteDeJogo.Instancia.filaProducao)
        {
            if (pedido == null)
            {
                continue;
            }

            dadosAtuais.filaProducao.Add(new SaveProductionOrderData
            {
                nomeUnidade = pedido.nomeUnidade,
                prefabKey = SaveableEntity.NormalizarPrefabKey(pedido.prefab != null ? pedido.prefab.name : pedido.nomeUnidade),
                tempoTotal = pedido.tempoTotal,
                tempoRestante = pedido.tempoRestante,
                ehSoldado = pedido.ehSoldado,
                ehHelicoptero = pedido.ehHelicoptero,
                ehNavio = pedido.ehNavio,
                ehAviao = pedido.ehAviao,
                ehCarrier = pedido.ehCarrier
            });
        }
    }

    private void CapturarEstadoIAImperial()
    {
        if (dadosAtuais.estadosIA == null)
        {
            dadosAtuais.estadosIA = new List<SaveAiStrategicStateData>();
        }

        dadosAtuais.estadosIA.Clear();
        IA_BrainMaster[] brains = IA_UnitySearch.FindAll<IA_BrainMaster>();
        foreach (IA_BrainMaster brain in brains)
        {
            if (brain == null)
            {
                continue;
            }

            IA03EstrategaNacional estrategaIA03 = brain.GetComponent<IA03EstrategaNacional>();

            dadosAtuais.estadosIA.Add(new SaveAiStrategicStateData
            {
                teamID = brain.TeamId,
                relatorioIA03 = estrategaIA03 != null
                    ? estrategaIA03.CapturarEstadoRelatorioSalvavel()
                    : null,
                estadoMissaoIA03 = estrategaIA03 != null
                    ? estrategaIA03.CapturarEstadoMissaoSalvavel()
                    : null,
                strategicPhase = (int)brain.StrategicPhase,
                activeImperialPlan = brain.ActiveImperialPlan,
                activeStrategicTarget = brain.ActiveStrategicTarget,
                imperialLastFailure = brain.ImperialLastFailure,
                imperialPlanSummary = brain.ImperialPlanSummary,
                targetFleet = brain.TargetFleet,
                targetAircraft = brain.TargetAircraft,
                targetOilTankers = brain.TargetOilTankers,
                targetPlatforms = brain.TargetPlatforms,
                targetPiers = brain.TargetPiers,
                targetShipyards = brain.TargetShipyards,
                targetCoastalDefenseShips = brain.TargetCoastalDefenseShips,
                targetRadars = brain.TargetRadars,
                targetCiws = brain.TargetCiws,
                playerFleetEstimate = brain.PlayerFleetEstimate,
                playerAircraftEstimate = brain.PlayerAircraftEstimate,
                weakEmpireRecoveryActive = brain.WeakEmpireRecoveryActive
            });
        }
    }

    private void CapturarEstadoIA01()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        GarantirColecoesIA01();
        dadosAtuais.estadosIA01.Clear();

        if (IA01Manager.TryGetInstance(out IA01Manager manager) && manager != null)
        {
            List<SaveIA01NationState> estados = manager.CaptureSaveStates();
            if (estados != null)
            {
                dadosAtuais.estadosIA01.AddRange(estados);
            }

            return;
        }

        IA01Controller[] controllers = IA_UnitySearch.FindAll<IA01Controller>();
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            IA01Controller controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            dadosAtuais.estadosIA01.Add(controller.CaptureSaveState());
        }

        dadosAtuais.estadosIA01.Sort(CompararEstadosIA01);
    }

    private void CapturarEstadoGovernoMundial()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        {
            dadosAtuais.governoMundial = null;
            return;
        }

        dadosAtuais.governoMundial = new SaveGovernoMundialData
        {
            teamJogador = governo.teamJogador,
            paises = governo.paises != null
                ? governo.paises.Where(p => p != null).ToList()
                : new List<DadosPaisGoverno>(),
            relacoes = governo.relacoes != null
                ? governo.relacoes.Where(r => r != null).ToList()
                : new List<RelacaoPaisGoverno>(),
            propostas = governo.propostas != null
                ? governo.propostas.Where(p => p != null).ToList()
                : new List<PropostaInternacional>(),
            noticias = governo.noticias != null
                ? governo.noticias.Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                : new List<string>()
        };
    }

    private void CapturarEstadoTerritorial()
    {
        if (dadosAtuais == null) return;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        dadosAtuais.territoriosCapturados = gerente != null
            ? gerente.CopiarProprietariosCapturados()
            : new List<SaveProprietarioTerritorio>();
        dadosAtuais.territoriosOcupados = gerente != null
            ? gerente.CopiarOcupacoesTerritoriais()
            : new List<SaveOcupacaoTerritorial>();
    }

    private void CapturarCidadesAbstratas()
    {
        if (dadosAtuais == null) return;
        GerenciadorDivisaoTerritorial gerente = GerenciadorDivisaoTerritorial.Instancia;
        dadosAtuais.cidadesAbstratas = gerente != null
            ? gerente.CapturarCidadesAbstratas()
            : new List<SaveCidadeAbstrataData>();
    }

    private void RestaurarCidadesAbstratas(bool criarGerenciadorSeAusente)
    {
        if (dadosAtuais == null || dadosAtuais.saveVersion < 19) return;
        GerenciadorDivisaoTerritorial gerente = GerenciadorDivisaoTerritorial.Instancia;
        if (gerente == null && criarGerenciadorSeAusente)
        {
            GerenciadorDivisaoTerritorial.GarantirInstancia();
            gerente = GerenciadorDivisaoTerritorial.Instancia;
        }
        gerente?.RestaurarCidadesAbstratas(dadosAtuais.cidadesAbstratas ?? new List<SaveCidadeAbstrataData>());
    }

    private void CapturarEstadoIA02()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        GarantirColecoesIA02();
        dadosAtuais.estadosIA02.Clear();

        if (IA02Manager.TryGetInstance(out IA02Manager manager) && manager != null)
        {
            List<SaveIA02NationState> estados = manager.CaptureSaveStates();
            if (estados != null)
            {
                dadosAtuais.estadosIA02.AddRange(estados);
            }

            return;
        }

        IA02Controller[] controllers = IA_UnitySearch.FindAll<IA02Controller>();
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            IA02Controller controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            dadosAtuais.estadosIA02.Add(controller.CaptureSaveState());
        }

        dadosAtuais.estadosIA02.Sort(CompararEstadosIA02);
    }

    private void CapturarProducaoAutomaticaIA()
    {
        if (dadosAtuais == null) return;
        dadosAtuais.producaoAutomaticaIA = IAAutoProductionRegistry.CaptureSaveData();
    }

    private void RestaurarProducaoAutomaticaIA()
    {
        if (dadosAtuais == null) return;
        RestaurarRegistroProducaoAutomaticaIA(notificarIA03: true);
    }

    private void RestaurarRegistroProducaoAutomaticaIA(bool notificarIA03)
    {
        IAAutoProductionRegistry.RestoreSaveData(dadosAtuais.producaoAutomaticaIA);
        if (notificarIA03)
        {
            IA03EstrategaNacional.NotificarRestauracaoProducaoAutomatica();
        }
    }

    private void CapturarEstadoDeusa()
    {
        if (dadosAtuais.estadosDeusa == null)
        {
            dadosAtuais.estadosDeusa = new List<SaveDeusaStateData>();
        }

        dadosAtuais.estadosDeusa.Clear();
        IA_DeusaBrain[] deuses = IA_UnitySearch.FindAll<IA_DeusaBrain>();
        for (int i = 0; i < deuses.Length; i++)
        {
            IA_DeusaBrain deusa = deuses[i];
            if (deusa == null || deusa.identidade == null || deusa.config == null)
            {
                continue;
            }

            dadosAtuais.estadosDeusa.Add(new SaveDeusaStateData
            {
                teamID = deusa.identidade.teamID > 0 ? deusa.identidade.teamID : (deusa.GetComponent<IA_BrainMaster>() != null ? deusa.GetComponent<IA_BrainMaster>().TeamId : 0),
                personalidade = (int)deusa.config.personalidade,
                modoInicial = (int)deusa.config.modoInicial,
                estagioAtual = (int)deusa.identidade.estagioAtual,
                travarEstagio = deusa.config.travarEstagio,
                modoObservadorDebug = deusa.config.modoObservadorDebug,
                bloquearFilaBrainMasterEmObservador = deusa.config.bloquearFilaBrainMasterEmObservador,
                usarEspionagemJusta = deusa.config.usarEspionagemJusta,
                permitirComercioComJogador = deusa.config.permitirComercioComJogador,
                permitirComercioComOutrasIAs = deusa.config.permitirComercioComOutrasIAs,
                permitirSancoes = deusa.config.permitirSancoes,
                permitirGuerraTotal = deusa.config.permitirGuerraTotal,
                nomePais = deusa.identidade.nomePais,
                nomePresidente = deusa.identidade.nomePresidente,
                nomeMoeda = deusa.identidade.nomeMoeda,
                resumoNacional = deusa.ResumoSalvavel()
            });
        }
    }

    private void AplicarEstadoIAImperial()
    {
        if (dadosAtuais == null || dadosAtuais.estadosIA == null || dadosAtuais.estadosIA.Count == 0)
        {
            return;
        }

        IA_BrainMaster[] brains = IA_UnitySearch.FindAll<IA_BrainMaster>();
        foreach (IA_BrainMaster brain in brains)
        {
            if (brain == null)
            {
                continue;
            }

            SaveAiStrategicStateData salvo = dadosAtuais.estadosIA.FirstOrDefault(e => e != null && e.teamID == brain.TeamId);
            if (salvo == null)
            {
                continue;
            }

            brain.StrategicPhase = (IA_StrategicPhase)Mathf.Clamp(salvo.strategicPhase, 0, (int)IA_StrategicPhase.Dominacao);
            brain.ActiveImperialPlan = string.IsNullOrWhiteSpace(salvo.activeImperialPlan) ? brain.ActiveImperialPlan : salvo.activeImperialPlan;
            brain.ActiveStrategicTarget = salvo.activeStrategicTarget ?? string.Empty;
            brain.ImperialLastFailure = salvo.imperialLastFailure ?? string.Empty;
            brain.ImperialPlanSummary = salvo.imperialPlanSummary ?? string.Empty;
            brain.TargetFleet = Mathf.Max(0, salvo.targetFleet);
            brain.TargetAircraft = Mathf.Max(0, salvo.targetAircraft);
            brain.TargetOilTankers = Mathf.Max(0, salvo.targetOilTankers);
            brain.TargetPlatforms = Mathf.Max(0, salvo.targetPlatforms);
            brain.TargetPiers = Mathf.Max(0, salvo.targetPiers);
            brain.TargetShipyards = Mathf.Max(0, salvo.targetShipyards);
            brain.TargetCoastalDefenseShips = Mathf.Max(0, salvo.targetCoastalDefenseShips);
            brain.TargetRadars = Mathf.Max(0, salvo.targetRadars);
            brain.TargetCiws = Mathf.Max(0, salvo.targetCiws);
            brain.PlayerFleetEstimate = Mathf.Max(0, salvo.playerFleetEstimate);
            brain.PlayerAircraftEstimate = Mathf.Max(0, salvo.playerAircraftEstimate);
            brain.WeakEmpireRecoveryActive = salvo.weakEmpireRecoveryActive;

            IA03EstrategaNacional estrategaIA03 = brain.GetComponent<IA03EstrategaNacional>();
            if (estrategaIA03 != null && salvo.relatorioIA03 != null)
            {
                estrategaIA03.RestaurarEstadoRelatorioSalvavel(salvo.relatorioIA03);
            }
            if (estrategaIA03 != null && salvo.estadoMissaoIA03 != null)
            {
                estrategaIA03.RestaurarEstadoMissaoSalvavel(salvo.estadoMissaoIA03);
            }
        }
    }

    private void AplicarEstadoDeusa()
    {
        if (dadosAtuais == null || dadosAtuais.estadosDeusa == null || dadosAtuais.estadosDeusa.Count == 0)
        {
            return;
        }

        IA_DeusaBrain[] deuses = IA_UnitySearch.FindAll<IA_DeusaBrain>();
        for (int i = 0; i < deuses.Length; i++)
        {
            IA_DeusaBrain deusa = deuses[i];
            if (deusa == null)
            {
                continue;
            }

            IA_BrainMaster brain = deusa.GetComponent<IA_BrainMaster>();
            int teamId = deusa.identidade != null && deusa.identidade.teamID > 0
                ? deusa.identidade.teamID
                : (brain != null ? brain.TeamId : 0);
            SaveDeusaStateData salvo = dadosAtuais.estadosDeusa.FirstOrDefault(e => e != null && e.teamID == teamId);
            if (salvo == null)
            {
                continue;
            }

            deusa.AplicarEstadoSalvo(
                salvo.personalidade,
                salvo.modoInicial,
                salvo.estagioAtual,
                salvo.travarEstagio,
                dadosAtuais.saveVersion >= 6 ? salvo.modoObservadorDebug : (deusa.config != null ? deusa.config.modoObservadorDebug : true),
                dadosAtuais.saveVersion >= 7 ? salvo.bloquearFilaBrainMasterEmObservador : (deusa.config != null && deusa.config.bloquearFilaBrainMasterEmObservador),
                salvo.usarEspionagemJusta,
                salvo.permitirComercioComJogador,
                salvo.permitirComercioComOutrasIAs,
                salvo.permitirSancoes,
                salvo.permitirGuerraTotal,
                salvo.nomePais,
                salvo.nomePresidente,
                salvo.nomeMoeda,
                salvo.resumoNacional);
        }
    }

    private void AplicarEstadoGovernoMundial()
    {
        if (dadosAtuais == null || dadosAtuais.governoMundial == null)
        {
            return;
        }

        SaveGovernoMundialData salvo = dadosAtuais.governoMundial;
        if (salvo.paises == null || salvo.paises.Count == 0)
        {
            return;
        }

        if (dadosAtuais.saveVersion < 27)
        {
            for (int i = 0; i < salvo.paises.Count; i++)
            {
                DadosPaisGoverno paisLegado = salvo.paises[i];
                if (paisLegado == null) continue;
                paisLegado.configuracaoInicialConcluida = true;
                paisLegado.orcamentoInvestimentoAtivo = false;
                paisLegado.orcamentoInvestimentoDiario = 100000000L;
                paisLegado.orcamentoPctAgricultura = 20;
                paisLegado.orcamentoPctEnergia = 20;
                paisLegado.orcamentoPctPetroleo = 15;
                paisLegado.orcamentoPctIndustria = 15;
                paisLegado.orcamentoPctCiencia = 10;
                paisLegado.orcamentoPctMilitar = 20;
                paisLegado.orcamentoPctExercito = 60;
                paisLegado.orcamentoPctMarinha = 25;
                paisLegado.orcamentoPctAerea = 15;
            }
        }

        SistemaGovernoMundial.GarantirInstancia();
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        {
            return;
        }

        governo.RestaurarEstadoSalvo(
            salvo.teamJogador,
            salvo.paises,
            salvo.relacoes,
            salvo.propostas,
            salvo.noticias);
    }

    private void AplicarEstadoTerritorial()
    {
        if (dadosAtuais == null) return;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        if (gerente == null)
        {
            GameObject raiz = new GameObject("GerenteDeTerritorio_SaveRestore");
            gerente = raiz.AddComponent<GerenteDeTerritorio>();
        }
        // The base map is ensured by MapaPolitico before applying the save.
        // Version 17 introduced the serialized mutable Neutral field; older
        // JsonUtility snapshots default it to false when absent, so ignore it.
        gerente.RestaurarEstadoPolitico(dadosAtuais.territoriosCapturados, dadosAtuais.saveVersion >= 17);
        gerente.RestaurarOcupacoesTerritoriais(
            dadosAtuais.saveVersion >= 28 ? dadosAtuais.territoriosOcupados : null);
    }

    private void RestaurarEstadoIA01()
    {
        if (dadosAtuais == null || dadosAtuais.estadosIA01 == null || dadosAtuais.estadosIA01.Count == 0)
        {
            return;
        }

        IA01Manager manager = IA01Manager.Instancia;
        if (manager != null)
        {
            manager.RestoreSaveStates(dadosAtuais.estadosIA01);
            return;
        }

        IA01Controller[] controllers = IA_UnitySearch.FindAll<IA01Controller>();
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < dadosAtuais.estadosIA01.Count; i++)
        {
            SaveIA01NationState state = dadosAtuais.estadosIA01[i];
            if (state == null)
            {
                continue;
            }

            IA01Controller controller = EncontrarControllerIA01(controllers, state);
            if (controller == null)
            {
                continue;
            }

            controller.RestoreFromSaveState(state);
        }
    }

    private void RestaurarEstadoIA02()
    {
        if (dadosAtuais == null || dadosAtuais.estadosIA02 == null || dadosAtuais.estadosIA02.Count == 0)
        {
            return;
        }

        if (IA02Manager.TryGetInstance(out IA02Manager manager) && manager != null)
        {
            manager.RestoreSaveStates(dadosAtuais.estadosIA02);
            return;
        }

        IA02Controller[] controllers = IA_UnitySearch.FindAll<IA02Controller>();
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < dadosAtuais.estadosIA02.Count; i++)
        {
            SaveIA02NationState state = dadosAtuais.estadosIA02[i];
            if (state == null)
            {
                continue;
            }

            IA02Controller controller = EncontrarControllerIA02(controllers, state);
            if (controller == null)
            {
                continue;
            }

            controller.RestoreFromSaveState(state);
        }
    }

    private void RestaurarFilaProducao()
    {
        if (GerenteDeJogo.Instancia == null || dadosAtuais.filaProducao == null)
        {
            return;
        }

        GerenteDeJogo.Instancia.filaProducao.Clear();
        foreach (SaveProductionOrderData salvo in dadosAtuais.filaProducao)
        {
            GameObject prefab = ResolverPrefab(salvo.prefabKey);
            if (prefab == null)
            {
                DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Fila ignorada sem prefab: " + salvo.prefabKey);
                continue;
            }

            GerenteDeJogo.Instancia.filaProducao.Add(new GerenteDeJogo.PedidoDeProducao
            {
                nomeUnidade = salvo.nomeUnidade,
                prefab = prefab,
                tempoTotal = salvo.tempoTotal,
                tempoRestante = salvo.tempoRestante,
                ehSoldado = salvo.ehSoldado,
                ehHelicoptero = salvo.ehHelicoptero,
                ehNavio = salvo.ehNavio,
                ehAviao = salvo.ehAviao,
                ehCarrier = salvo.ehCarrier
            });
        }
    }

    private void CapturarEstadoSAR()
    {
        if (dadosAtuais == null) return;

        SistemaGuardaCosteira.GarantirInstancia();
        dadosAtuais.incidentesSAR = SistemaGuardaCosteira.Instancia != null
            ? SistemaGuardaCosteira.Instancia.CapturarEstadoSave()
            : new List<RescueIncident>();
    }

    private void CapturarEstadoQuarteis()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        if (dadosAtuais.estadosQuarteis == null)
        {
            dadosAtuais.estadosQuarteis = new List<SaveQuartelStateData>();
        }
        dadosAtuais.estadosQuarteis.Clear();

        GerenciadorQuartel[] quarteis = FindObjectsByType<GerenciadorQuartel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < quarteis.Length; i++)
        {
            GerenciadorQuartel quartel = quarteis[i];
            if (quartel == null || quartel.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            SaveableEntity saveable = SaveableEntity.Garantir(quartel.gameObject);
            if (saveable == null || string.IsNullOrWhiteSpace(saveable.UniqueId))
            {
                continue;
            }

            SaveQuartelStateData salvo = new SaveQuartelStateData
            {
                quartelUniqueId = saveable.UniqueId,
                quartelTeamID = quartel.teamID,
                usarPainelQuartelUIToolkit = quartel.usarPainelQuartelUIToolkit,
                recolhimentoAutomatico = quartel.recolhimentoAutomatico,
                modoDefensivoAtivo = quartel.modoDefensivoAtivo,
                treinamentoPassivo = quartel.treinamentoPassivo,
                recrutamentoAutomatico = quartel.recrutamentoAutomatico,
                treinamentoAutomatico = quartel.treinamentoAutomatico,
                metaEfetivo = quartel.metaEfetivo,
                tempoFormacaoSegundos = quartel.tempoFormacaoSegundos,
                periodoFolhaDias = quartel.ObterAdministracao() != null ? quartel.ObterAdministracao().periodoFolhaDias : 15,
                misseisArmazenados = quartel.misseisArmazenados,
                municaoArmazenada = quartel.municaoArmazenada
            };

            AdicionarIdsUnidades(quartel.soldadosNoDormitorio, salvo.soldadosNoDormitorio);
            AdicionarIdsUnidades(quartel.veiculosNoQuartel, salvo.veiculosNoQuartel);
            QuartelAdministracaoRuntime administracao = quartel.ObterAdministracao();
            if (administracao != null) administracao.CapturarEstadoSave(salvo);
            dadosAtuais.estadosQuarteis.Add(salvo);
        }
    }

    private void AdicionarIdsUnidades(List<ControleUnidade> unidades, List<string> destino)
    {
        if (unidades == null || destino == null)
        {
            return;
        }

        for (int i = 0; i < unidades.Count; i++)
        {
            ControleUnidade unidade = unidades[i];
            if (unidade == null) continue;
            SaveableEntity saveable = SaveableEntity.Garantir(unidade.gameObject);
            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId) && !destino.Contains(saveable.UniqueId))
            {
                destino.Add(saveable.UniqueId);
            }
        }
    }

    private void RestaurarEstadoQuarteis()
    {
        if (dadosAtuais == null || dadosAtuais.estadosQuarteis == null || dadosAtuais.estadosQuarteis.Count == 0)
        {
            return;
        }

        Dictionary<string, ControleUnidade> unidadesPorId = new Dictionary<string, ControleUnidade>(StringComparer.OrdinalIgnoreCase);
        ControleUnidade[] unidades = FindObjectsByType<ControleUnidade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            ControleUnidade unidade = unidades[i];
            if (unidade == null) continue;
            SaveableEntity saveable = SaveableEntity.Garantir(unidade.gameObject);
            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId))
            {
                unidadesPorId[saveable.UniqueId] = unidade;
            }
        }

        GerenciadorQuartel[] quarteis = FindObjectsByType<GerenciadorQuartel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < quarteis.Length; i++)
        {
            GerenciadorQuartel quartel = quarteis[i];
            if (quartel == null) continue;
            SaveableEntity saveable = SaveableEntity.Garantir(quartel.gameObject);
            if (saveable == null || !TryEncontrarEstadoQuartel(saveable.UniqueId, out SaveQuartelStateData salvo)) continue;

            quartel.usarPainelQuartelUIToolkit = salvo.usarPainelQuartelUIToolkit;
            quartel.teamID = salvo.quartelTeamID > 0 ? salvo.quartelTeamID : quartel.teamID;
            quartel.recolhimentoAutomatico = salvo.recolhimentoAutomatico;
            quartel.modoDefensivoAtivo = salvo.modoDefensivoAtivo;
            quartel.treinamentoPassivo = salvo.treinamentoPassivo;
            quartel.recrutamentoAutomatico = salvo.recrutamentoAutomatico;
            quartel.treinamentoAutomatico = salvo.treinamentoAutomatico;
            quartel.metaEfetivo = Mathf.Max(1, salvo.metaEfetivo);
            quartel.tempoFormacaoSegundos = Mathf.Max(1f, salvo.tempoFormacaoSegundos);
            quartel.misseisArmazenados = Mathf.Max(0, salvo.misseisArmazenados);
            quartel.municaoArmazenada = Mathf.Max(0, salvo.municaoArmazenada);
            quartel.soldadosNoDormitorio.Clear();
            quartel.veiculosNoQuartel.Clear();
            RestaurarIdsUnidades(salvo.soldadosNoDormitorio, unidadesPorId, quartel.soldadosNoDormitorio);
            RestaurarIdsUnidades(salvo.veiculosNoQuartel, unidadesPorId, quartel.veiculosNoQuartel);
            QuartelAdministracaoRuntime administracao = quartel.ObterAdministracao();
            if (administracao != null)
            {
                administracao.periodoFolhaDias = Mathf.Max(1, salvo.periodoFolhaDias > 0 ? salvo.periodoFolhaDias : administracao.periodoFolhaDias);
                administracao.RestaurarEstadoSave(salvo);
            }
        }
    }

    private bool TryEncontrarEstadoQuartel(string uniqueId, out SaveQuartelStateData estado)
    {
        estado = null;
        if (string.IsNullOrWhiteSpace(uniqueId) || dadosAtuais == null || dadosAtuais.estadosQuarteis == null) return false;
        for (int i = 0; i < dadosAtuais.estadosQuarteis.Count; i++)
        {
            SaveQuartelStateData candidato = dadosAtuais.estadosQuarteis[i];
            if (candidato != null && string.Equals(candidato.quartelUniqueId, uniqueId, StringComparison.OrdinalIgnoreCase))
            {
                estado = candidato;
                return true;
            }
        }
        return false;
    }

    private void RestaurarIdsUnidades(List<string> ids, Dictionary<string, ControleUnidade> unidadesPorId, List<ControleUnidade> destino)
    {
        if (ids == null || unidadesPorId == null || destino == null) return;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id) || !unidadesPorId.TryGetValue(id, out ControleUnidade unidade) || unidade == null) continue;
            if (!destino.Contains(unidade)) destino.Add(unidade);
        }
    }

    private void CapturarEntidades()
    {
        dadosAtuais.entidades.Clear();
        bufferObjetos.Clear();

        AdicionarCandidatos(IA_UnitySearch.FindAll<SaveableEntity>().Select(s => s != null ? s.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<IdentidadeUnidade>().Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<ControleUnidade>().Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<IdentidadeNaval>().Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<Imovel>().Select(c => c != null ? c.gameObject : null));
        bufferAeroportos.Clear();
        RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);
        AdicionarCandidatos(bufferAeroportos.Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<Fabrica>().Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<Estaleiro>().Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(IA_UnitySearch.FindAll<PierMarinha>().Select(c => c != null ? c.gameObject : null));

        foreach (GameObject obj in bufferObjetos)
        {
            if (obj == null || obj.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            // O frete fretado é temporário. A ordem logística será restaurada
            // e despachará outro navio; salvar o clone pelo prefab genérico o
            // faria voltar como cargueiro permanente e disponível.
            NavioCargaMercado navioCarga = obj.GetComponent<NavioCargaMercado>();
            if (navioCarga != null && navioCarga.Fretado)
            {
                continue;
            }

            SaveEntityData data = CapturarEntidade(obj);
            if (data != null && !string.IsNullOrWhiteSpace(data.prefabKey))
            {
                dadosAtuais.entidades.Add(data);
            }
        }
    }

    private void CapturarFormacoesTaticas()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        RTSPersistentFormationService service = RTSPersistentFormationService.GarantirInstancia();
        dadosAtuais.formacoesTaticas = service.CapturarEstado(SceneManager.GetActiveScene());
    }

    private void AdicionarCandidatos(IEnumerable<GameObject> candidatos)
    {
        foreach (GameObject candidato in candidatos)
        {
            if (candidato == null || candidato == gameObject || bufferObjetos.Contains(candidato))
            {
                continue;
            }

            if (candidato.GetComponentInParent<Canvas>() != null || candidato.GetComponent<Camera>() != null)
            {
                continue;
            }

            bufferObjetos.Add(candidato);
        }
    }

    private SaveEntityData CapturarEntidade(GameObject obj)
    {
        SaveableEntity saveable = SaveableEntity.Garantir(obj);
        IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
        SistemaDeDanos danos = obj.GetComponent<SistemaDeDanos>();
        CombustivelUnidade combustivel = obj.GetComponent<CombustivelUnidade>();
        ControleUnidade controle = obj.GetComponent<ControleUnidade>();
        ComportamentoPatrulhaUniversal patrulha = obj.GetComponent<ComportamentoPatrulhaUniversal>();
        ComportamentoSeguirUniversal seguir = obj.GetComponent<ComportamentoSeguirUniversal>();
        CidadeComplexoUrbano cidade = obj.GetComponent<CidadeComplexoUrbano>();
        int teamId = cidade != null ? cidade.teamId : identidade != null ? identidade.teamID : 1;
        if (identidade == null && cidade == null)
        {
            IA01Controller[] controllers = IA_UnitySearch.FindAll<IA01Controller>();
            for (int i = 0; i < controllers.Length; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller != null && controller.IsPositionInsidePreparedTerritory(obj.transform.position, 80f))
                {
                    teamId = controller.TeamId;
                    break;
                }
            }
        }

        ControleAviao controleAviao = obj.GetComponent<ControleAviao>();
        SaveEntityData data = new SaveEntityData
        {
            uniqueId = saveable.UniqueId,
            prefabKey = saveable.PrefabKey,
            nomeCena = obj.name,
            ativo = obj.activeSelf,
            posicao = new SaveVector3(obj.transform.position),
            rotacao = new SaveQuaternion(obj.transform.rotation),
            escala = new SaveVector3(obj.transform.localScale),
            teamID = teamId,
            nomeDoPais = identidade != null ? identidade.nomeDoPais : string.Empty,
            baseAereaAtualId = controleAviao != null && controleAviao.baseEstacionamentoAtual != null
                ? SaveableEntity.Garantir(controleAviao.baseEstacionamentoAtual.gameObject).UniqueId : string.Empty,
            destinoPousoAereoId = controleAviao != null && controleAviao.DestinoPousoEscolhido != null
                ? SaveableEntity.Garantir(controleAviao.DestinoPousoEscolhido.gameObject).UniqueId : string.Empty,
            destinoManualAereo = controleAviao != null && controleAviao.DestinoManualDefinido,
            aeronaveEstavaEmVoo = controleAviao != null
                && (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao
                    || controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando),
            tipoUnidade = identidade != null ? identidade.tipoUnidade : TipoUnidade.Estrutura,
            possuiMilitaresConsumidos = identidade != null,
            militaresConsumidos = identidade != null ? Mathf.Max(0, identidade.militaresConsumidos) : 0,
            possuiVida = danos != null,
            vidaAtual = danos != null ? danos.vidaAtual : 0f,
            vidaMaxima = danos != null ? danos.vidaMaxima : 0f,
            possuiCombustivel = combustivel != null && combustivel.usaCombustivel,
            combustivelAtual = combustivel != null ? combustivel.combustivelAtual : 0f,
            capacidadeCombustivel = combustivel != null ? combustivel.capacidade : 0f,
            capacidadeCombustivelAtualizada = true,
            capacidadeNavalAtualizada = true,
            ordemAtual = controle != null ? controle.OrdemAtual : OrdemControleUnidade.Ociosa,
            modoCombateAtivo = true
        };
        CapturarDadosCidade(cidade, data);

        AeroportoTemporarioC400 aeroportoTemporario = obj.GetComponent<AeroportoTemporarioC400>();
        if (aeroportoTemporario != null)
        {
            data.possuiPrazoAeroportoTemporario = true;
            data.prazoAeroportoTemporarioAtivo = aeroportoTemporario.prazoAtivo;
            data.diasAeroportoTemporario = aeroportoTemporario.diasAteDestruir;
            data.diaExpiracaoAeroportoTemporario = aeroportoTemporario.diaExpiracao;
        }

        if (controle != null)
        {
            EstadoControleUnidadeSnapshot estado = controle.ObterEstadoControle();
            data.ordemAtual = estado.ordemAtual;
            data.modoCombateAtivo = estado.modoCombateAtivo;
            data.possuiDestino = estado.possuiDestinoOrdenado;
            data.ultimoDestino = new SaveVector3(estado.ultimoDestino);
            OrdemMovimento ordemMovimento = controle.OrdemMovimentoAtual;
            if (ordemMovimento != null)
            {
                data.ordemMovimentoOwner = ordemMovimento.Dono ?? string.Empty;
                data.ordemMovimentoId = ordemMovimento.Id ?? string.Empty;
            }
            if (patrulha != null)
            {
                data.indicePatrulha = patrulha.IndiceAtual;
                foreach (Vector3 ponto in patrulha.ObterPontos())
                {
                    data.pontosPatrulha.Add(new SaveVector3(ponto));
                }
            }

            if (seguir != null && seguir.AlvoSeguido != null)
            {
                SaveableEntity alvoSave = SaveableEntity.Garantir(seguir.AlvoSeguido.gameObject);
                if (alvoSave != null)
                {
                    data.seguirAlvoId = alvoSave.UniqueId;
                }
            }
        }

        return data;
    }

    private static void CapturarDadosCidade(CidadeComplexoUrbano cidade, SaveEntityData destino)
    {
        if (cidade == null || destino == null) return;

        destino.possuiCidadeComplexoUrbano = true;
        destino.cidadeTema = cidade.tema;
        destino.cidadeNome = cidade.nomeCidade;
        destino.cidadeTeamId = cidade.teamId;
        destino.cidadeCapacidadeHospitalarInicial = cidade.capacidadeHospitalarInicial;
        destino.cidadePossuiRedeSaudeBasica = cidade.possuiRedeSaudeBasica;
        destino.cidadeParticipaProgressaoGlobal = cidade.participarProgressaoCidadesGlobais;
        destino.cidadeOrdemProgressaoGlobal = cidade.ordemProgressaoGlobal;
        destino.cidadeCapacidadeHabitacional = cidade.capacidadeHabitacional;
        destino.cidadePopulacaoResidente = cidade.populacaoResidente;
        destino.cidadeQualidadeVida = cidade.qualidadeVida;
        destino.cidadeProporcaoComercio = cidade.proporcaoComercio;
        destino.cidadeRendaPorTrabalhador = cidade.rendaPorTrabalhador;
        destino.cidadeConsumoEnergiaPorMorador = cidade.consumoEnergiaPorMorador;
        destino.cidadeConsumoEnergiaPorEmprego = cidade.consumoEnergiaPorEmprego;
        destino.cidadeSemEnergia = cidade.semEnergia;
    }

    private static void AplicarDadosCidade(GameObject objeto, SaveEntityData origem)
    {
        if (objeto == null || origem == null || !origem.possuiCidadeComplexoUrbano) return;
        CidadeComplexoUrbano cidade = objeto.GetComponent<CidadeComplexoUrbano>();
        if (cidade == null) return;

        cidade.tema = origem.cidadeTema;
        cidade.nomeCidade = origem.cidadeNome ?? cidade.nomeCidade;
        cidade.teamId = Mathf.Max(0, origem.cidadeTeamId);
        cidade.capacidadeHospitalarInicial = Mathf.Max(0, origem.cidadeCapacidadeHospitalarInicial);
        cidade.possuiRedeSaudeBasica = origem.cidadePossuiRedeSaudeBasica;
        cidade.participarProgressaoCidadesGlobais = origem.cidadeParticipaProgressaoGlobal;
        cidade.ordemProgressaoGlobal = Mathf.Max(0, origem.cidadeOrdemProgressaoGlobal);
        cidade.capacidadeHabitacional = origem.cidadeCapacidadeHabitacional;
        cidade.populacaoResidente = origem.cidadePopulacaoResidente;
        cidade.qualidadeVida = origem.cidadeQualidadeVida;
        cidade.proporcaoComercio = origem.cidadeProporcaoComercio;
        cidade.rendaPorTrabalhador = origem.cidadeRendaPorTrabalhador;
        cidade.consumoEnergiaPorMorador = origem.cidadeConsumoEnergiaPorMorador;
        cidade.consumoEnergiaPorEmprego = origem.cidadeConsumoEnergiaPorEmprego;

        IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            identidade.teamID = cidade.teamId;
            identidade.nomeDoPais = origem.nomeDoPais ?? identidade.nomeDoPais;
            identidade.tipoUnidade = TipoUnidade.Estrutura;
        }

        CidadeSaudePopulacional saude = objeto.GetComponent<CidadeSaudePopulacional>();
        if (saude != null)
        {
            saude.teamId = cidade.teamId;
            saude.capacidadeBase = cidade.possuiRedeSaudeBasica
                ? Mathf.Max(0, cidade.capacidadeHospitalarInicial)
                : 0;
        }

        cidade.SincronizarEstruturaEconomica();
        cidade.DefinirStatusEnergia(origem.cidadeSemEnergia);
        cidade.SincronizarEstruturaEconomica();
    }

    private void RestaurarDadosCidadesSalvas()
    {
        if (dadosAtuais == null || dadosAtuais.entidades == null || saveablesPorId == null) return;
        for (int i = 0; i < dadosAtuais.entidades.Count; i++)
        {
            SaveEntityData entidade = dadosAtuais.entidades[i];
            if (entidade == null || !entidade.possuiCidadeComplexoUrbano
                || string.IsNullOrWhiteSpace(entidade.uniqueId)
                || !saveablesPorId.TryGetValue(entidade.uniqueId, out SaveableEntity saveable)
                || saveable == null)
            {
                continue;
            }

            AplicarDadosCidade(saveable.gameObject, entidade);
        }
    }

    private void LimparEntidadesPersistidasDaCena()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        SaveableEntity[] existentes = IA_UnitySearch.FindAll<SaveableEntity>();
        foreach (SaveableEntity existente in existentes)
        {
            if (existente != null
                && existente.gameObject != gameObject
                && existente.gameObject.scene == cenaAtiva)
            {
                Destroy(existente.gameObject);
            }
        }
    }

    private void MapearSaveablesDaCenaAtiva()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        SaveableEntity[] existentes = IA_UnitySearch.FindAll<SaveableEntity>();
        for (int i = 0; i < existentes.Length; i++)
        {
            SaveableEntity existente = existentes[i];
            if (existente == null || existente.gameObject == gameObject || existente.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            string id = existente.UniqueId;
            if (!string.IsNullOrWhiteSpace(id))
            {
                saveablesPorId[id] = existente;
            }
        }
    }

    private void InstanciarEntidadeSalva(SaveEntityData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.prefabKey))
        {
            return;
        }

        bool isIa01Entity = data.teamID > 1
            && (data.teamID == dadosAtuais.rtsPrimaryAiTeamId
                || (IA01Manager.Instancia != null && IA01Manager.Instancia.FindControllerByTeamId(data.teamID) != null));
        IA01Controller ia01Controller = isIa01Entity && IA01Manager.Instancia != null
            ? IA01Manager.Instancia.FindControllerByTeamId(data.teamID)
            : null;
        if (!IA01MilitaryCatalogPolicy.IsAllowedSavedEntity(
                data.teamID,
                data.tipoUnidade,
                data.prefabKey,
                isIa01Entity,
                ia01Controller != null ? ia01Controller.FichasMilitaresPermitidas : null))
        {
            Debug.LogWarning("[Save] Unidade IA01 fora do catálogo militar permitido ignorada: "
                + data.prefabKey + " team=" + data.teamID + " tipo=" + data.tipoUnidade);
            DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Unidade IA01 fora do catálogo militar permitido ignorada: " + data.prefabKey);
            return;
        }

        // Saves anteriores ao layout territorial atual podem conter estruturas
        // da IA01 em coordenadas antigas. Não mover nem apagar unidades móveis:
        // somente rejeitar a estrutura fora do envelope oficial da própria IA,
        // permitindo que o runtime a reconstrua nos Creates atuais.
        if (data.tipoUnidade == TipoUnidade.Estrutura && data.teamID > 0)
        {
            IA01Controller controller = ia01Controller;
            if (controller != null && controller.CityLayout != null
                && !controller.IsPositionInsidePreparedTerritory(data.posicao.ToVector3()))
            {
                Debug.LogWarning("[Save] Estrutura IA01 antiga fora do layout atual ignorada: "
                    + data.prefabKey + " team=" + data.teamID + " pos=" + data.posicao.ToVector3().ToString("F2"));
                DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Estrutura IA01 antiga ignorada fora do layout: " + data.prefabKey);
                return;
            }
        }

        GameObject prefab = ResolverPrefab(data.prefabKey);
        if (prefab == null)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Prefab nao encontrado: " + data.prefabKey);
            return;
        }

        GameObject obj = Instantiate(prefab, data.posicao.ToVector3(), data.rotacao.ToQuaternion());
        obj.name = string.IsNullOrWhiteSpace(data.nomeCena) ? data.prefabKey : data.nomeCena;
        obj.transform.localScale = data.escala.ToVector3();
        obj.SetActive(data.ativo);

        SaveableEntity saveable = SaveableEntity.Garantir(obj, data.prefabKey);
        saveable.UniqueId = data.uniqueId;
        saveablesPorId[data.uniqueId] = saveable;

        AeroportoTemporarioC400 aeroportoTemporario = obj.GetComponent<AeroportoTemporarioC400>();
        if (aeroportoTemporario != null && data.possuiPrazoAeroportoTemporario)
        {
            aeroportoTemporario.RestaurarPrazo(
                data.prazoAeroportoTemporarioAtivo,
                data.diasAeroportoTemporario,
                data.diaExpiracaoAeroportoTemporario);
        }

        IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            identidade.teamID = data.teamID;
            identidade.nomeDoPais = data.nomeDoPais;
            identidade.tipoUnidade = data.tipoUnidade;
            identidade.militaresConsumidos = data.possuiMilitaresConsumidos
                ? Mathf.Max(0, data.militaresConsumidos)
                : SistemaGovernoMundial.ObterEfetivoNecessario(obj, data.tipoUnidade);
        }

        SistemaDeDanos danos = obj.GetComponent<SistemaDeDanos>();
        if (danos != null && data.possuiVida)
        {
            danos.vidaMaxima = Mathf.Max(1f, data.vidaMaxima);
            danos.vidaAtual = Mathf.Clamp(data.vidaAtual, 0f, danos.vidaMaxima);
        }

        CombustivelUnidade combustivel = obj.GetComponent<CombustivelUnidade>();
        if (combustivel != null && data.possuiCombustivel)
        {
            bool migrarCapacidadeAntiga = dadosAtuais != null
                && dadosAtuais.saveVersion < 24
                && !data.capacidadeCombustivelAtualizada;
            bool migrarPoliticaNaval = !data.capacidadeNavalAtualizada && combustivel.UnidadeNaval;
            combustivel.RestaurarEstadoPersistido(data.capacidadeCombustivel, data.combustivelAtual, migrarCapacidadeAntiga, migrarPoliticaNaval);
            if (migrarCapacidadeAntiga || migrarPoliticaNaval)
            {
                // A restauração pode ser repetida após a troca de cena. Grave o
                // tanque migrado no snapshot em memória para evitar regressão.
                data.capacidadeCombustivel = combustivel.capacidade;
                data.combustivelAtual = combustivel.combustivelAtual;
            }
            data.capacidadeCombustivelAtualizada = true;
            data.capacidadeNavalAtualizada = true;
        }

        AplicarDadosCidade(obj, data);

        NavMeshAgent agente = obj.GetComponent<NavMeshAgent>();
        if (agente != null && agente.enabled && !agente.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(obj.transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas))
            {
                agente.Warp(hit.position);
            }
        }
    }

    private void RestaurarBasesAereasSalvas()
    {
        if (dadosAtuais == null || dadosAtuais.entidades == null) return;
        for (int i = 0; i < dadosAtuais.entidades.Count; i++)
        {
            SaveEntityData salvo = dadosAtuais.entidades[i];
            if (salvo == null || string.IsNullOrWhiteSpace(salvo.uniqueId)
                || !saveablesPorId.TryGetValue(salvo.uniqueId, out SaveableEntity entidade) || entidade == null) continue;
            ControleAviao aviao = entidade.GetComponent<ControleAviao>();
            if (aviao == null) continue;
            GerenciadorAeroporto baseAtual = ResolverBaseAereaPorId(salvo.baseAereaAtualId);
            GerenciadorAeroporto destino = ResolverBaseAereaPorId(salvo.destinoPousoAereoId);
            if (dadosAtuais.saveVersion < 23 || destino == null) destino = baseAtual;
            aviao.RestaurarBasesSalvas(baseAtual, destino, salvo.destinoManualAereo, salvo.aeronaveEstavaEmVoo);
        }
    }

    private GerenciadorAeroporto ResolverBaseAereaPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !saveablesPorId.TryGetValue(id, out SaveableEntity entidade) || entidade == null) return null;
        return entidade.GetComponent<GerenciadorAeroporto>();
    }

    private void RestaurarOrdemEntidade(SaveEntityData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.uniqueId) || !saveablesPorId.TryGetValue(data.uniqueId, out SaveableEntity saveable) || saveable == null)
        {
            return;
        }

        ControleUnidade controle = saveable.GetComponent<ControleUnidade>();
        if (controle == null)
        {
            return;
        }

        controle.DefinirModoCombate(data.modoCombateAtivo);
        if (data.ordemAtual == OrdemControleUnidade.Patrulhando && data.pontosPatrulha != null && data.pontosPatrulha.Count > 0)
        {
            List<Vector3> pontos = data.pontosPatrulha.Select(p => p.ToVector3()).ToList();
            if (!string.IsNullOrWhiteSpace(data.ordemMovimentoOwner)
                && !string.IsNullOrWhiteSpace(data.ordemMovimentoId))
            {
                controle.EmitirOrdemPatrulha(pontos, data.ordemMovimentoOwner, data.ordemMovimentoId);
            }
            else
            {
                controle.EmitirOrdemPatrulha(pontos);
            }
            ComportamentoPatrulhaUniversal patrulha = saveable.GetComponent<ComportamentoPatrulhaUniversal>();
            if (patrulha != null)
            {
                patrulha.DefinirIndiceAtual(data.indicePatrulha);
            }
            return;
        }

        if (data.ordemAtual == OrdemControleUnidade.Seguindo && !string.IsNullOrWhiteSpace(data.seguirAlvoId) && saveablesPorId.TryGetValue(data.seguirAlvoId, out SaveableEntity alvoSeguir) && alvoSeguir != null)
        {
            controle.EmitirOrdemSeguir(alvoSeguir.transform);
            return;
        }

        if ((data.ordemAtual == OrdemControleUnidade.Movendo || data.ordemAtual == OrdemControleUnidade.Recuando) && data.possuiDestino)
        {
            if (!string.IsNullOrWhiteSpace(data.ordemMovimentoOwner)
                && !string.IsNullOrWhiteSpace(data.ordemMovimentoId))
            {
                controle.EmitirOrdemMovimento(
                    data.ultimoDestino.ToVector3(),
                    data.ordemMovimentoOwner,
                    cancelarComportamentos: true,
                    id: data.ordemMovimentoId);
            }
            else
            {
                controle.EmitirOrdemMover(data.ultimoDestino.ToVector3());
            }
        }
    }

    private GameObject ResolverPrefab(string prefabKey)
    {
        string key = SaveableEntity.NormalizarPrefabKey(prefabKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        GameObject prefabEmCache;
        if (prefabsPorChaveSave.TryGetValue(key, out prefabEmCache))
        {
            return prefabEmCache;
        }

        if (MenuConstrucao.catalogoGlobal != null)
        {
            foreach (DadosConstrucao ficha in MenuConstrucao.catalogoGlobal)
            {
                if (ficha == null || ficha.PrefabDaUnidade == null)
                {
                    continue;
                }

                string fichaKey = SaveableEntity.NormalizarPrefabKey(ficha.PrefabDaUnidade.name);
                if (string.Equals(fichaKey, key, StringComparison.OrdinalIgnoreCase) || string.Equals(ficha.NomeItem, key, StringComparison.OrdinalIgnoreCase))
                {
                    return ArmazenarPrefabResolvido(key, ficha.PrefabDaUnidade);
                }
            }
        }

        GameObject resourcePrefab = Resources.Load<GameObject>(key);
        if (resourcePrefab != null)
        {
            return ArmazenarPrefabResolvido(key, resourcePrefab);
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(key + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && string.Equals(SaveableEntity.NormalizarPrefabKey(prefab.name), key, StringComparison.OrdinalIgnoreCase))
            {
                return ArmazenarPrefabResolvido(key, prefab);
            }
        }
#endif

        return ArmazenarPrefabResolvido(key, null);
    }

    private GameObject ArmazenarPrefabResolvido(string key, GameObject prefab)
    {
        prefabsPorChaveSave[key] = prefab;
        return prefab;
    }

    public GameObject MaterializarEntidadeOperacional(SaveEntityData data, Vector3 posicao, Quaternion rotacao)
    {
        if (data == null)
        {
            return null;
        }

        GameObject prefab = ResolverPrefab(data.prefabKey);
        if (prefab == null)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("Manifesto", "Prefab ausente: " + data.prefabKey);
            return null;
        }

        GameObject obj = Instantiate(prefab, posicao, rotacao);
        obj.name = string.IsNullOrWhiteSpace(data.nomeCena) ? data.prefabKey : data.nomeCena;
        obj.transform.localScale = data.escala.ToVector3();

        SaveableEntity saveable = SaveableEntity.Garantir(obj, data.prefabKey);
        saveable.UniqueId = data.uniqueId;
        if (!string.IsNullOrWhiteSpace(data.uniqueId))
        {
            saveablesPorId[data.uniqueId] = saveable;
        }
        IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            identidade.teamID = data.teamID;
            identidade.nomeDoPais = data.nomeDoPais;
            identidade.tipoUnidade = data.tipoUnidade;
        }

        SistemaDeDanos danos = obj.GetComponent<SistemaDeDanos>();
        if (danos != null && data.possuiVida)
        {
            danos.vidaMaxima = Mathf.Max(1f, data.vidaMaxima);
            danos.vidaAtual = Mathf.Clamp(data.vidaAtual, 0f, danos.vidaMaxima);
        }

        CombustivelUnidade combustivel = obj.GetComponent<CombustivelUnidade>();
        if (combustivel != null && data.possuiCombustivel)
        {
            bool migrarCapacidadeAntiga = dadosAtuais != null
                && dadosAtuais.saveVersion < 24
                && !data.capacidadeCombustivelAtualizada;
            bool migrarPoliticaNaval = !data.capacidadeNavalAtualizada && combustivel.UnidadeNaval;
            combustivel.RestaurarEstadoPersistido(data.capacidadeCombustivel, data.combustivelAtual, migrarCapacidadeAntiga, migrarPoliticaNaval);
            if (migrarCapacidadeAntiga || migrarPoliticaNaval)
            {
                data.capacidadeCombustivel = combustivel.capacidade;
                data.combustivelAtual = combustivel.combustivelAtual;
            }
            data.capacidadeCombustivelAtualizada = true;
            data.capacidadeNavalAtualizada = true;
        }

        ControleUnidade controle = obj.GetComponent<ControleUnidade>();
        if (controle != null)
        {
            controle.DefinirModoCombate(data.modoCombateAtivo);
            if ((data.ordemAtual == OrdemControleUnidade.Movendo || data.ordemAtual == OrdemControleUnidade.Recuando) && data.possuiDestino)
            {
                controle.EmitirOrdemMover(data.ultimoDestino.ToVector3());
            }
        }

        return obj;
    }

    private void LogInfo(string mensagem)
    {
        if (exibirLogsNoConsole)
        {
            Debug.Log(mensagem);
        }
    }

    [ContextMenu("Forcar Salvar Jogo")]
    private void TesteSalvar() { SalvarJogo(); }

    [ContextMenu("Forcar Carregar Jogo")]
    private void TesteCarregar() { CarregarJogo(); }

    [ContextMenu("Forcar Apagar Save")]
    private void TesteApagar() { ApagarDados(); }

    // â”€â”€ Sistema Industrial: Salvar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private void CapturarSistemaIndustrial()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return;
        }

        dadosAtuais.industria = industrial.CriarSaveData();
        if (dadosAtuais.industria != null)
        {
            dadosAtuais.totalDias = dadosAtuais.industria.totalDias;
        }

        SincronizarCompatibilidadeIndustrialLegada(dadosAtuais.industria);
        LogInfo($"[SistemaIndustrial] {(dadosAtuais.industria != null && dadosAtuais.industria.perfisMineral != null ? dadosAtuais.industria.perfisMineral.Count : 0)} perfis e " +
                $"{(dadosAtuais.industria != null && dadosAtuais.industria.estoques != null ? dadosAtuais.industria.estoques.Count : 0)} estoques minerais salvos.");
    }

    // â”€â”€ Sistema Industrial: Carregar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private void RestaurarSistemaIndustrial()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        MigrarIndustriaLegadaSeNecessario();

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return;
        }

        industrial.RestaurarSaveData(dadosAtuais.industria);
        SincronizarCompatibilidadeIndustrialLegada(dadosAtuais.industria);
        ReconciliarCulturasAlimentaresRestauradas();

        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.RestaurarDiasSemNotificar(Mathf.Max(1, dadosAtuais.totalDias));
        }

        LogInfo("[SistemaIndustrial] Sistema industrial restaurado do save.");
    }

    private void ReconciliarCulturasAlimentaresRestauradas()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        IndustrialSaveData saveIndustrial = dadosAtuais != null ? dadosAtuais.industria : null;
        if (governo == null || saveIndustrial == null || saveIndustrial.estoques == null) return;

        foreach (DadosPaisGoverno pais in governo.Paises)
        {
            if (pais == null) continue;
            string teamKey = pais.teamId.ToString();
            bool encontrouCultivoComEstoque = false;
            for (int i = 0; i < saveIndustrial.estoques.Count && !encontrouCultivoComEstoque; i++)
            {
                SaveEstoqueIndustrial estoquePais = saveIndustrial.estoques[i];
                if (estoquePais == null || !string.Equals(estoquePais.paisId, teamKey, StringComparison.Ordinal)
                    || estoquePais.estoques == null) continue;

                for (int r = 0; r < estoquePais.estoques.Count && !encontrouCultivoComEstoque; r++)
                {
                    QuantidadeRecursoIndustrial recurso = estoquePais.estoques[r];
                    if (recurso == null || recurso.quantidade <= 0d) continue;
                    for (int c = 0; c < AgriculturaNacional.QuantidadeCulturas; c++)
                    {
                        if (string.Equals(recurso.recursoId, AgriculturaNacional.ObterIdCultura(c), StringComparison.OrdinalIgnoreCase))
                        {
                            encontrouCultivoComEstoque = true;
                            break;
                        }
                    }
                }
            }

            // Saves antigos podem ter apenas o estoque agregado. Nesses casos,
            // manter a comida salva evita apagar recursos que ainda não tinham
            // sido separados por cultura.
            if (encontrouCultivoComEstoque)
                governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
        }
    }

    private void CapturarSistemaLogisticaMercado()
    {
        if (dadosAtuais == null) return;

        SistemaLogisticaMercado logistica = SistemaLogisticaMercado.Instancia;
        dadosAtuais.logisticaMercado = logistica != null
            ? logistica.CriarSaveData()
            : new SistemaLogisticaMercadoSaveData();
    }

    private void RestaurarSistemaLogisticaMercado()
    {
        if (dadosAtuais == null) return;

        SistemaLogisticaMercado.GarantirInstancia();
        SistemaLogisticaMercado.Instancia?.RestaurarSaveData(dadosAtuais.logisticaMercado);
    }

    private void AplicarTempoSalvo()
    {
        if (dadosAtuais == null || GerenciadorTempo.Instancia == null)
        {
            return;
        }

        int totalDias = dadosAtuais.totalDias;
        if (dadosAtuais.industria != null && dadosAtuais.industria.totalDias > 0)
        {
            totalDias = dadosAtuais.industria.totalDias;
        }

        GerenciadorTempo.Instancia.RestaurarDiasSemNotificar(Mathf.Max(1, totalDias));
    }

    private void CapturarRTSState()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        RTSGameSession session = RTSGameSession.Instancia;
        RTSSimulationClock clock = RTSSimulationClock.Instancia;
        if (session != null)
        {
            dadosAtuais.tempoJogo = session.ElapsedSeconds;
            dadosAtuais.rtsPlayerTeamId = session.PlayerTeamId;
            dadosAtuais.rtsPrimaryAiTeamId = session.PrimaryAiTeamId;
            dadosAtuais.rtsMatchResult = (int)session.Result;
        }

        if (clock != null)
        {
            dadosAtuais.rtsSimulationTime = clock.SimulationTime;
            dadosAtuais.rtsSimulationTick = clock.Tick;
        }

        RTSObjectiveService objectives = RTSObjectiveService.Instancia;
        if (objectives != null)
        {
            dadosAtuais.rtsObjectives = objectives.CaptureState();
        }
    }

    private void AplicarRTSState()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        RTSGameSession session = RTSGameSession.Instancia;
        if (session != null)
        {
            RTSMatchResult result = Enum.IsDefined(typeof(RTSMatchResult), dadosAtuais.rtsMatchResult)
                ? (RTSMatchResult)dadosAtuais.rtsMatchResult
                : RTSMatchResult.None;
            session.RestoreState(
                dadosAtuais.rtsPlayerTeamId,
                dadosAtuais.rtsPrimaryAiTeamId,
                1,
                dadosAtuais.rtsSimulationTime > 0f ? dadosAtuais.rtsSimulationTime : dadosAtuais.tempoJogo,
                result);
        }

        RTSSimulationClock clock = RTSSimulationClock.Instancia;
        clock?.RestoreState(dadosAtuais.rtsSimulationTime, dadosAtuais.rtsSimulationTick);

        RTSObjectiveService objectives = RTSObjectiveService.Instancia;
        objectives?.RestoreState(dadosAtuais.rtsObjectives);
    }

    private void MigrarIndustriaLegadaSeNecessario()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        if (dadosAtuais.industria == null)
        {
            dadosAtuais.industria = new IndustrialSaveData();
        }

        if (dadosAtuais.industria.totalDias <= 0)
        {
            dadosAtuais.industria.totalDias = Mathf.Max(1, dadosAtuais.totalDias);
        }

        if ((dadosAtuais.industria.perfisMineral == null || dadosAtuais.industria.perfisMineral.Count == 0) && dadosAtuais.perfisMineral != null)
        {
            foreach (SavePerfilMineral salvo in dadosAtuais.perfisMineral)
            {
                if (salvo == null)
                {
                    continue;
                }

                dadosAtuais.industria.perfisMineral.Add(new SavePerfilMineralIndustrial
                {
                    teamId = salvo.teamId,
                    perfilGerado = salvo.perfilGerado,
                    ferro = salvo.ferro,
                    cobre = salvo.cobre,
                    bauxita = salvo.bauxita,
                    titanio = salvo.titanio,
                    uranio = salvo.uranio,
                    modificadorIndustrial = salvo.modificadorIndustrial,
                    extraindoFerro = salvo.extraindoFerro,
                    extraindoCobre = salvo.extraindoCobre,
                    extraindoBauxita = salvo.extraindoBauxita,
                    extraindoTitanio = salvo.extraindoTitanio,
                    extraindoUranio = salvo.extraindoUranio,
                    refinandoAco = salvo.refinandoAco,
                    refinandoCobreEletrolitico = salvo.refinandoCobreEletrolitico,
                    refinandoDuraluminio = salvo.refinandoDuraluminio,
                    refinandoLigaTitanio = salvo.refinandoLigaTitanio,
                    refinandoComponentes = salvo.refinandoComponentes,
                    refinandoUranioEnriquecido = salvo.refinandoUranioEnriquecido
                });
            }
        }

        if ((dadosAtuais.industria.estoques == null || dadosAtuais.industria.estoques.Count == 0) && dadosAtuais.estoquesMineral != null)
        {
            foreach (SaveEstoqueMineral salvo in dadosAtuais.estoquesMineral)
            {
                if (salvo == null)
                {
                    continue;
                }

                dadosAtuais.industria.estoques.Add(new SaveEstoqueIndustrial
                {
                    paisId = salvo.teamId.ToString(),
                    estoques = new List<QuantidadeRecursoIndustrial>
                    {
                        new QuantidadeRecursoIndustrial(IndustriaIds.MinerioFerro, salvo.minerioFerro),
                        new QuantidadeRecursoIndustrial(IndustriaIds.MinerioCobre, salvo.minerioCobre),
                        new QuantidadeRecursoIndustrial(IndustriaIds.Bauxita, salvo.bauxita),
                        new QuantidadeRecursoIndustrial(IndustriaIds.MinerioTitanio, salvo.minerioTitanio),
                        new QuantidadeRecursoIndustrial(IndustriaIds.UranioBruto, salvo.uranioBruto),
                        new QuantidadeRecursoIndustrial(IndustriaIds.AcoEstrutural, salvo.acoEstrutural),
                        new QuantidadeRecursoIndustrial(IndustriaIds.CobreEletrolitico, salvo.cobreEletrolitico),
                        new QuantidadeRecursoIndustrial(IndustriaIds.Duraluminio, salvo.duraluminio),
                        new QuantidadeRecursoIndustrial(IndustriaIds.LigaTitanio, salvo.ligaTitanio),
                        new QuantidadeRecursoIndustrial(IndustriaIds.ComponentesEletronicos, salvo.componentesEletronicos),
                        new QuantidadeRecursoIndustrial(IndustriaIds.UranioEnriquecido, salvo.uranioEnriquecido)
                    }
                });
            }
        }

        dadosAtuais.totalDias = Mathf.Max(1, dadosAtuais.totalDias);
    }

    private void SincronizarCompatibilidadeIndustrialLegada(IndustrialSaveData industria)
    {
        if (dadosAtuais == null)
        {
            return;
        }

        if (dadosAtuais.perfisMineral == null)
        {
            dadosAtuais.perfisMineral = new List<SavePerfilMineral>();
        }

        if (dadosAtuais.estoquesMineral == null)
        {
            dadosAtuais.estoquesMineral = new List<SaveEstoqueMineral>();
        }

        dadosAtuais.perfisMineral.Clear();
        dadosAtuais.estoquesMineral.Clear();

        if (industria == null)
        {
            return;
        }

        if (industria.perfisMineral != null)
        {
            foreach (SavePerfilMineralIndustrial salvo in industria.perfisMineral)
            {
                if (salvo == null)
                {
                    continue;
                }

                dadosAtuais.perfisMineral.Add(new SavePerfilMineral
                {
                    teamId = salvo.teamId,
                    perfilGerado = salvo.perfilGerado,
                    ferro = salvo.ferro,
                    cobre = salvo.cobre,
                    bauxita = salvo.bauxita,
                    titanio = salvo.titanio,
                    uranio = salvo.uranio,
                    extraindoFerro = salvo.extraindoFerro,
                    extraindoCobre = salvo.extraindoCobre,
                    extraindoBauxita = salvo.extraindoBauxita,
                    extraindoTitanio = salvo.extraindoTitanio,
                    extraindoUranio = salvo.extraindoUranio,
                    refinandoAco = salvo.refinandoAco,
                    refinandoCobreEletrolitico = salvo.refinandoCobreEletrolitico,
                    refinandoDuraluminio = salvo.refinandoDuraluminio,
                    refinandoLigaTitanio = salvo.refinandoLigaTitanio,
                    refinandoComponentes = salvo.refinandoComponentes,
                    refinandoUranioEnriquecido = salvo.refinandoUranioEnriquecido,
                    modificadorIndustrial = salvo.modificadorIndustrial
                });
            }
        }

        if (industria.estoques != null)
        {
            foreach (SaveEstoqueIndustrial salvo in industria.estoques)
            {
                if (salvo == null)
                {
                    continue;
                }

                SaveEstoqueMineral legado = new SaveEstoqueMineral();
                if (int.TryParse(salvo.paisId, out int teamId))
                {
                    legado.teamId = teamId;
                }

                if (salvo.estoques != null)
                {
                    foreach (QuantidadeRecursoIndustrial recurso in salvo.estoques)
                    {
                        if (recurso == null)
                        {
                            continue;
                        }

                        if (string.Equals(recurso.recursoId, IndustriaIds.MinerioFerro, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.minerioFerro = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.MinerioCobre, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.minerioCobre = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.Bauxita, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.bauxita = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.MinerioTitanio, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.minerioTitanio = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.uranioBruto = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.AcoEstrutural, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.acoEstrutural = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.CobreEletrolitico, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.cobreEletrolitico = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.Duraluminio, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.duraluminio = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.LigaTitanio, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.ligaTitanio = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.ComponentesEletronicos, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.componentesEletronicos = (float)recurso.quantidade;
                        }
                        else if (string.Equals(recurso.recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
                        {
                            legado.uranioEnriquecido = (float)recurso.quantidade;
                        }
                    }
                }

                dadosAtuais.estoquesMineral.Add(legado);
            }
        }
    }

    private void CapturarManifestosTransporte()
    {
        if (dadosAtuais.manifestosTransporte == null)
        {
            dadosAtuais.manifestosTransporte = new List<SaveTransportManifestData>();
        }

        dadosAtuais.manifestosTransporte.Clear();
        NavioTransporteTropas[] transportes = IA_UnitySearch.FindAll<NavioTransporteTropas>();
        for (int i = 0; i < transportes.Length; i++)
        {
            NavioTransporteTropas transporte = transportes[i];
            if (transporte == null) continue;

            SaveableEntity saveable = SaveableEntity.Garantir(transporte.gameObject);
            transporte.AdicionarManifestosAoSave(saveable.UniqueId, dadosAtuais.manifestosTransporte);
        }

        TransporteAnfibio[] anfibios = IA_UnitySearch.FindAll<TransporteAnfibio>();
        for (int i = 0; i < anfibios.Length; i++)
        {
            TransporteAnfibio transporte = anfibios[i];
            if (transporte == null) continue;

            SaveableEntity saveable = SaveableEntity.Garantir(transporte.gameObject);
            transporte.AdicionarManifestosAoSave(saveable.UniqueId, dadosAtuais.manifestosTransporte);
        }
    }

    private void RestaurarManifestosTransporte()
    {
        if (dadosAtuais == null || dadosAtuais.manifestosTransporte == null)
        {
            return;
        }

        for (int i = 0; i < dadosAtuais.manifestosTransporte.Count; i++)
        {
            SaveTransportManifestData salvo = dadosAtuais.manifestosTransporte[i];
            if (salvo == null || salvo.entidade == null || string.IsNullOrWhiteSpace(salvo.transporteUniqueId))
            {
                continue;
            }

            SaveableEntity transporteSalvavel;
            if (!saveablesPorId.TryGetValue(salvo.transporteUniqueId, out transporteSalvavel) || transporteSalvavel == null)
            {
                continue;
            }

            NavioTransporteTropas transporte = transporteSalvavel.GetComponent<NavioTransporteTropas>();
            if (transporte != null)
            {
                transporte.RestaurarManifesto(salvo.entidade, salvo.veiculo);
                continue;
            }

            TransporteAnfibio anfibio = transporteSalvavel.GetComponent<TransporteAnfibio>();
            if (anfibio != null)
            {
                anfibio.RestaurarManifesto(salvo.entidade, salvo.veiculo);
            }
        }
    }

    private void GarantirColecoesIA01()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        if (dadosAtuais.estadosIA01 == null)
        {
            dadosAtuais.estadosIA01 = new List<SaveIA01NationState>();
        }
    }

    private void GarantirColecoesIA02()
    {
        if (dadosAtuais == null)
        {
            return;
        }

        if (dadosAtuais.estadosIA02 == null)
        {
            dadosAtuais.estadosIA02 = new List<SaveIA02NationState>();
        }
    }

    private void GarantirColecoesCidadesAbstratas()
    {
        if (dadosAtuais == null) return;
        if (dadosAtuais.cidadesAbstratas == null)
            dadosAtuais.cidadesAbstratas = new List<SaveCidadeAbstrataData>();
    }

    private static IA01Controller EncontrarControllerIA01(IEnumerable<IA01Controller> controllers, SaveIA01NationState state)
    {
        if (controllers == null || state == null)
        {
            return null;
        }

        IA01Controller porNationId = null;
        IA01Controller porTeamId = null;
        IA01Controller porNome = null;

        foreach (IA01Controller controller in controllers)
        {
            if (controller == null)
            {
                continue;
            }

            if (state.instanceId > 0 && controller.InstanceId == state.instanceId)
            {
                return controller;
            }

            if (porNationId == null && state.nationId > 0 && controller.NationId == state.nationId)
            {
                porNationId = controller;
            }

            if (porTeamId == null && state.teamId > 0 && controller.TeamId == state.teamId)
            {
                porTeamId = controller;
            }

            if (porNome == null && !string.IsNullOrWhiteSpace(state.nationName) && string.Equals(controller.NationName, state.nationName, StringComparison.Ordinal))
            {
                porNome = controller;
            }
        }

        return porNationId ?? porTeamId ?? porNome;
    }

    private static int CompararEstadosIA01(SaveIA01NationState left, SaveIA01NationState right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int comparacao = left.nationId.CompareTo(right.nationId);
        if (comparacao != 0)
        {
            return comparacao;
        }

        comparacao = left.teamId.CompareTo(right.teamId);
        if (comparacao != 0)
        {
            return comparacao;
        }

        return left.instanceId.CompareTo(right.instanceId);
    }

    private static IA02Controller EncontrarControllerIA02(IEnumerable<IA02Controller> controllers, SaveIA02NationState state)
    {
        if (controllers == null || state == null)
        {
            return null;
        }

        IA02Controller porNationId = null;
        IA02Controller porTeamId = null;
        IA02Controller porNome = null;

        foreach (IA02Controller controller in controllers)
        {
            if (controller == null)
            {
                continue;
            }

            if (state.instanceId > 0 && controller.InstanceId == state.instanceId)
            {
                return controller;
            }

            if (porNationId == null && state.nationId > 0 && controller.NationId == state.nationId)
            {
                porNationId = controller;
            }

            if (porTeamId == null && state.teamId > 0 && controller.TeamId == state.teamId)
            {
                porTeamId = controller;
            }

            if (porNome == null && !string.IsNullOrWhiteSpace(state.nationName) && string.Equals(controller.NationName, state.nationName, StringComparison.Ordinal))
            {
                porNome = controller;
            }
        }

        return porNationId ?? porTeamId ?? porNome;
    }

    private static int CompararEstadosIA02(SaveIA02NationState left, SaveIA02NationState right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int comparacao = left.nationId.CompareTo(right.nationId);
        if (comparacao != 0)
        {
            return comparacao;
        }

        comparacao = left.teamId.CompareTo(right.teamId);
        if (comparacao != 0)
        {
            return comparacao;
        }

        return left.instanceId.CompareTo(right.instanceId);
    }
}
