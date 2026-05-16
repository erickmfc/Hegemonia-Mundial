using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.DEUSA;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[Serializable]
public class DadosDoJogo
{
    public int saveVersion = 7;
    public int creditosJogador = 5000;
    public int petroleoJogador = 500;
    public int acoJogador = 300;
    public int energiaJogador = 100;
    public string mapaAtual = "Mapa_1";
    public string idioma = "pt-BR";
    public string dificuldade = "normal";
    public float tempoJogo;
    public SaveVector3 cameraPosicao;
    public SaveQuaternion cameraRotacao;
    public List<string> itensDesbloqueados = new List<string>();
    public List<SaveEntityData> entidades = new List<SaveEntityData>();
    public List<SaveProductionOrderData> filaProducao = new List<SaveProductionOrderData>();
    public List<SaveAiStrategicStateData> estadosIA = new List<SaveAiStrategicStateData>();
    public List<SaveDeusaStateData> estadosDeusa = new List<SaveDeusaStateData>();
    public float qgPosX;
    public float qgPosY;
    public float qgPosZ;
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

    private string caminhoDoArquivo;
    private bool restauracaoPendente;
    private readonly List<GameObject> bufferObjetos = new List<GameObject>(512);
    private readonly Dictionary<string, SaveableEntity> saveablesPorId = new Dictionary<string, SaveableEntity>(StringComparer.Ordinal);

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
        if (PossuiSave())
        {
            CarregarJogo();
            restauracaoPendente = false;
            return;
        }

        dadosAtuais = new DadosDoJogo();
        AplicarIdiomaSalvo();
        AplicarDificuldadeSalva();
    }

    public bool PossuiSave()
    {
        return !string.IsNullOrEmpty(caminhoDoArquivo) && File.Exists(caminhoDoArquivo);
    }

    public void SalvarJogo()
    {
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
        }

        dadosAtuais.saveVersion = 7;
        RegistrarCenaAtual(SceneManager.GetActiveScene().name);
        CapturarRecursos();
        CapturarCamera();
        CapturarIdioma();
        CapturarDificuldade();
        CapturarFilaProducao();
        CapturarEstadoIAImperial();
        CapturarEstadoDeusa();
        CapturarEntidades();

        string json = JsonUtility.ToJson(dadosAtuais, true);
        File.WriteAllText(caminhoDoArquivo, json);
        LogInfo("Jogo salvo com sucesso em: " + caminhoDoArquivo);
    }

    public void CarregarJogo()
    {
        if (!PossuiSave())
        {
            dadosAtuais = new DadosDoJogo();
            carregouDeSave = false;
            return;
        }

        string json = File.ReadAllText(caminhoDoArquivo);
        dadosAtuais = JsonUtility.FromJson<DadosDoJogo>(json);
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
            carregouDeSave = false;
            return;
        }

        if (dadosAtuais.saveVersion <= 0)
        {
            dadosAtuais.saveVersion = 1;
        }

        carregouDeSave = true;
        restauracaoPendente = dadosAtuais.saveVersion >= 2 && dadosAtuais.entidades != null && dadosAtuais.entidades.Count > 0;
        AplicarIdiomaSalvo();
        AplicarDificuldadeSalva();
        AplicarRecursosSalvos();
        LogInfo("Jogo carregado com sucesso.");
    }

    public void IniciarNovoJogo(string cenaInicial = null)
    {
        dadosAtuais = new DadosDoJogo();
        dadosAtuais.idioma = LocalizationManager.Instancia.ObterCodigoIdioma();
        dadosAtuais.dificuldade = GameDifficultyManager.Instancia.ObterCodigoDificuldade();
        carregouDeSave = false;
        restauracaoPendente = false;
        RegistrarCenaAtual(string.IsNullOrWhiteSpace(cenaInicial) ? dadosAtuais.mapaAtual : cenaInicial);
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

        return dadosAtuais.mapaAtual;
    }

    public void ApagarDados()
    {
        if (PossuiSave())
        {
            File.Delete(caminhoDoArquivo);
        }

        dadosAtuais = new DadosDoJogo();
        carregouDeSave = false;
        restauracaoPendente = false;
        LogInfo("Save apagado do computador. Comecando do zero.");
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!carregouDeSave)
        {
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

        if (dadosAtuais.entidades != null)
        {
            for (int i = 0; i < dadosAtuais.entidades.Count; i++)
            {
                InstanciarEntidadeSalva(dadosAtuais.entidades[i]);
            }

            yield return null;

            for (int i = 0; i < dadosAtuais.entidades.Count; i++)
            {
                RestaurarOrdemEntidade(dadosAtuais.entidades[i]);
            }
        }

        RestaurarFilaProducao();
        AplicarEstadoIAImperial();
        AplicarEstadoDeusa();
        AplicarCameraSalva();
        AplicarRecursosSalvos();
        DiagnosticoDesempenhoJogo.RegistrarEvento("Save", "Mundo restaurado (entidades=" + (dadosAtuais.entidades != null ? dadosAtuais.entidades.Count : 0) + ")");
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
        IA_BrainMaster[] brains = UnityEngine.Object.FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
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

    private void CapturarEstadoDeusa()
    {
        if (dadosAtuais.estadosDeusa == null)
        {
            dadosAtuais.estadosDeusa = new List<SaveDeusaStateData>();
        }

        dadosAtuais.estadosDeusa.Clear();
#if UNITY_2023_1_OR_NEWER
        IA_DeusaBrain[] deuses = UnityEngine.Object.FindObjectsByType<IA_DeusaBrain>(FindObjectsSortMode.None);
#else
        IA_DeusaBrain[] deuses = UnityEngine.Object.FindObjectsOfType<IA_DeusaBrain>();
#endif
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

        IA_BrainMaster[] brains = UnityEngine.Object.FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
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

#if UNITY_2023_1_OR_NEWER
        IA_DeusaBrain[] deuses = UnityEngine.Object.FindObjectsByType<IA_DeusaBrain>(FindObjectsSortMode.None);
#else
        IA_DeusaBrain[] deuses = UnityEngine.Object.FindObjectsOfType<IA_DeusaBrain>();
#endif
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

        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<SaveableEntity>(FindObjectsSortMode.None).Select(s => s != null ? s.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<IdentidadeNaval>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<Imovel>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<Fabrica>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));
        AdicionarCandidatos(UnityEngine.Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None).Select(c => c != null ? c.gameObject : null));

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

        SaveEntityData data = new SaveEntityData
        {
            uniqueId = saveable.UniqueId,
            prefabKey = saveable.PrefabKey,
            nomeCena = obj.name,
            ativo = obj.activeSelf,
            posicao = new SaveVector3(obj.transform.position),
            rotacao = new SaveQuaternion(obj.transform.rotation),
            escala = new SaveVector3(obj.transform.localScale),
            teamID = identidade != null ? identidade.teamID : 1,
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
        SaveableEntity[] existentes = UnityEngine.Object.FindObjectsByType<SaveableEntity>(FindObjectsSortMode.None);
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

        if (MenuConstrucao.catalogoGlobal != null)
        {
            foreach (DadosConstrucao ficha in MenuConstrucao.catalogoGlobal)
            {
                if (ficha == null || ficha.prefabDaUnidade == null)
                {
                    continue;
                }

                string fichaKey = SaveableEntity.NormalizarPrefabKey(ficha.prefabDaUnidade.name);
                if (string.Equals(fichaKey, key, StringComparison.OrdinalIgnoreCase) || string.Equals(ficha.nomeItem, key, StringComparison.OrdinalIgnoreCase))
                {
                    return ficha.prefabDaUnidade;
                }
            }
        }

        GameObject resourcePrefab = Resources.Load<GameObject>(key);
        if (resourcePrefab != null)
        {
            return resourcePrefab;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(key + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && string.Equals(SaveableEntity.NormalizarPrefabKey(prefab.name), key, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }
#endif

        return null;
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
}
