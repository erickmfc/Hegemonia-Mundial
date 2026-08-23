using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.DEUSA;
using Hegemonia.AI.IA01;
using Hegemonia.AI.Shared;
using Hegemonia.RTS;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[Serializable]
public class DadosDoJogo
{
    public int saveVersion = 12;
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
    public List<SaveTransportManifestData> manifestosTransporte = new List<SaveTransportManifestData>();
    public List<SaveProductionOrderData> filaProducao = new List<SaveProductionOrderData>();
    public List<SaveAiStrategicStateData> estadosIA = new List<SaveAiStrategicStateData>();
    public List<SaveDeusaStateData> estadosDeusa = new List<SaveDeusaStateData>();
    public List<SaveIA01NationState> estadosIA01 = new List<SaveIA01NationState>();
    public IAAutoProductionSaveData producaoAutomaticaIA = new IAAutoProductionSaveData();
    public float qgPosX;
    public float qgPosY;
    public float qgPosZ;
    public int totalDias = 1;
    public IndustrialSaveData industria = new IndustrialSaveData();
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
    public bool possuiVida;
    public float vidaAtual;
    public float vidaMaxima;
    public bool possuiCombustivel;
    public float combustivelAtual;
    public float capacidadeCombustivel;
    public OrdemControleUnidade ordemAtual;
    public bool modoCombateAtivo;
    public bool possuiDestino;
    public SaveVector3 ultimoDestino;
    public List<SaveVector3> pontosPatrulha = new List<SaveVector3>();
    public int indicePatrulha;
    public string seguirAlvoId;
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
    public static SistemaSaveGame Instancia;

    public DadosDoJogo dadosAtuais;
    public bool exibirLogsNoConsole = false;
    public bool carregouDeSave = false;
    public bool partidaNovaRecemIniciada { get; private set; }

    private string caminhoDoArquivo;
    private string diretorioSaves;
    private string saveSelecionadoId = string.Empty;
    private bool restauracaoPendente;
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
        dadosAtuais.saveVersion = 13;
        dadosAtuais.nomeSave = NormalizarNomeSave(dadosAtuais.nomeSave);
        dadosAtuais.salvoEmUtc = DateTime.UtcNow.ToString("O");
        RegistrarCenaAtual(SceneManager.GetActiveScene().name);
        CapturarRecursos();
        CapturarCamera();
        CapturarIdioma();
        CapturarDificuldade();
        CapturarFilaProducao();
        CapturarEstadoIAImperial();
        CapturarEstadoIA01();
        CapturarProducaoAutomaticaIA();
        CapturarEstadoDeusa();
        CapturarEntidades();
        CapturarManifestosTransporte();
        CapturarSistemaIndustrial();
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
        bool saveLegadoAntesDaVersao12 = dadosAtuais.saveVersion < 12;
        if (dadosAtuais.saveVersion <= 0)
        {
            dadosAtuais.saveVersion = 1;
        }

        if (dadosAtuais.totalDias <= 0)
        {
            dadosAtuais.totalDias = 1;
        }

        MigrarIndustriaLegadaSeNecessario();

        carregouDeSave = true;
        partidaNovaRecemIniciada = false;
        restauracaoPendente = dadosAtuais.saveVersion >= 2 && ((dadosAtuais.entidades != null && dadosAtuais.entidades.Count > 0) || (dadosAtuais.estadosIA01 != null && dadosAtuais.estadosIA01.Count > 0));
        // Limpa ordens da sessão anterior imediatamente. A restauração tardia
        // continua sendo repetida após a cena para manter o mesmo ponto de
        // sincronização das demais ordens salvas.
        IAAutoProductionRegistry.RestoreSaveData(dadosAtuais.producaoAutomaticaIA);
        AplicarIdiomaSalvo();
        AplicarDificuldadeSalva();
        AplicarRecursosSalvos();
        AplicarTempoSalvo();
        AplicarRTSState();
        if (saveLegadoAntesDaVersao12)
        {
            // JsonUtility converte o numero antigo para long sem arredondamento.
            // A versao 12 sera gravada no proximo SalvarJogo, mantendo saves antigos carregaveis.
            LogInfo("Save legado carregado e preparado para migracao monetaria para long (versao " + dadosAtuais.saveVersion + ").");
        }
        LogInfo("Jogo carregado com sucesso.");
    }

    public void IniciarNovoJogo(string cenaInicial = null)
    {
        dadosAtuais = new DadosDoJogo();
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
        if (!carregouDeSave && partidaNovaRecemIniciada)
        {
            SanitizarCenaDePartidaNova(cena);
        }

        if (!carregouDeSave)
        {
            // A sanitizacao pode consumir o marcador antes do Start de
            // GerenciadorRecursos. A partida nova ainda precisa receber os
            // valores escolhidos no menu, nunca os valores serializados da cena.
            AplicarRecursosSalvos();
            return;
        }

        AplicarRecursosSalvos();
        if (restauracaoPendente && dadosAtuais != null && dadosAtuais.saveVersion >= 2)
        {
            StartCoroutine(RestaurarMundoDepoisDaCena());
        }
    }

    private IEnumerator RestaurarMundoDepoisDaCena()
    {
        restauracaoPendente = false;
        yield return null;
        yield return null;

        LimparEntidadesPersistidasDaCena();
        saveablesPorId.Clear();
        prefabsPorChaveSave.Clear();

        if (dadosAtuais.entidades != null)
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

            RestaurarManifestosTransporte();

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

        RestaurarFilaProducao();
        AplicarEstadoIAImperial();
        AplicarEstadoDeusa();
        RestaurarEstadoIA01();
        RestaurarProducaoAutomaticaIA();
        AplicarCameraSalva();
        AplicarRecursosSalvos();
        AplicarTempoSalvo();
        AplicarRTSState();
        RestaurarSistemaIndustrial();
        DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Mundo restaurado (entidades=" + (dadosAtuais.entidades != null ? dadosAtuais.entidades.Count : 0) + ")");
    }

    private void SanitizarCenaDePartidaNova(Scene cena)
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cena.name))
        {
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

            dadosAtuais.estadosIA.Add(new SaveAiStrategicStateData
            {
                teamID = brain.TeamId,
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

    private void CapturarProducaoAutomaticaIA()
    {
        if (dadosAtuais == null) return;
        dadosAtuais.producaoAutomaticaIA = IAAutoProductionRegistry.CaptureSaveData();
    }

    private void RestaurarProducaoAutomaticaIA()
    {
        if (dadosAtuais == null) return;
        IAAutoProductionRegistry.RestoreSaveData(dadosAtuais.producaoAutomaticaIA);
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

            SaveEntityData data = CapturarEntidade(obj);
            if (data != null && !string.IsNullOrWhiteSpace(data.prefabKey))
            {
                dadosAtuais.entidades.Add(data);
            }
        }
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
        int teamId = identidade != null ? identidade.teamID : 1;
        if (identidade == null)
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
            tipoUnidade = identidade != null ? identidade.tipoUnidade : TipoUnidade.Estrutura,
            possuiVida = danos != null,
            vidaAtual = danos != null ? danos.vidaAtual : 0f,
            vidaMaxima = danos != null ? danos.vidaMaxima : 0f,
            possuiCombustivel = combustivel != null && combustivel.usaCombustivel,
            combustivelAtual = combustivel != null ? combustivel.combustivelAtual : 0f,
            capacidadeCombustivel = combustivel != null ? combustivel.capacidade : 0f,
            ordemAtual = controle != null ? controle.OrdemAtual : OrdemControleUnidade.Ociosa,
            modoCombateAtivo = true
        };

        if (controle != null)
        {
            EstadoControleUnidadeSnapshot estado = controle.ObterEstadoControle();
            data.ordemAtual = estado.ordemAtual;
            data.modoCombateAtivo = estado.modoCombateAtivo;
            data.possuiDestino = estado.possuiDestinoOrdenado;
            data.ultimoDestino = new SaveVector3(estado.ultimoDestino);
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

    private void LimparEntidadesPersistidasDaCena()
    {
        SaveableEntity[] existentes = IA_UnitySearch.FindAll<SaveableEntity>();
        foreach (SaveableEntity existente in existentes)
        {
            if (existente != null && existente.gameObject != gameObject)
            {
                Destroy(existente.gameObject);
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
            combustivel.capacidade = Mathf.Max(0f, data.capacidadeCombustivel);
            combustivel.combustivelAtual = Mathf.Clamp(data.combustivelAtual, 0f, combustivel.capacidade);
        }

        NavMeshAgent agente = obj.GetComponent<NavMeshAgent>();
        if (agente != null && agente.enabled && !agente.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(obj.transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas))
            {
                agente.Warp(hit.position);
            }
        }
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
            controle.EmitirOrdemPatrulha(pontos);
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
            controle.EmitirOrdemMover(data.ultimoDestino.ToVector3());
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
            combustivel.capacidade = Mathf.Max(0f, data.capacidadeCombustivel);
            combustivel.combustivelAtual = Mathf.Clamp(data.combustivelAtual, 0f, combustivel.capacidade);
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

        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.RestaurarDias(Mathf.Max(1, dadosAtuais.totalDias));
        }

        LogInfo("[SistemaIndustrial] Sistema industrial restaurado do save.");
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

        GerenciadorTempo.Instancia.RestaurarDias(Mathf.Max(1, totalDias));
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
}
