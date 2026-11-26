using System;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.RTS;
using UnityEngine;

public enum EstadoMissaoPacificacao
{
    Disponivel,
    AguardandoChegada,
    OperacaoAtiva,
    AguardandoDecisaoInvestimento,
    InvestimentoSuspenso,
    Mobilizando,
    RetiradaEmAndamento,
    ObjetivosTerritoriaisConcluidos,
    ResistenciaRemanescente,
    Vitoria,
    AguardandoDecisaoFinal,
    EstabelecendoBase,
    RetiradaFinal,
    TransferindoControle,
    Concluida,
    Abandonada
}

public enum EstadoInvestimentoPacificacao
{
    Ativo,
    AguardandoDecisao,
    Suspenso,
    Encerrado
}

public enum DecisaoFinalPacificacao
{
    Nenhuma,
    BasePermanente,
    RetiradaCompleta,
    TransferenciaAliada
}

internal enum ProgressoTerritorialPacificacao
{
    Disponivel,
    AguardandoChegada,
    OperacaoAtiva,
    RetiradaEmAndamento,
    ObjetivosTerritoriaisConcluidos,
    ResistenciaRemanescente,
    AguardandoDecisaoFinal,
    EstabelecendoBase,
    RetiradaFinal,
    TransferindoControle,
    Concluida,
    Abandonada
}

[Serializable]
public sealed class SaveGrupoResistenciaPacificacao
{
    public string identificador = string.Empty;
    public int unidadesRestantes;
}

[Serializable]
public sealed class SaveOperacaoPacificacaoData
{
    public int estado;
    public int progressoTerritorial;
    public int estadoInvestimento;
    public int decisaoFinal;
    public float tempoOperacaoSegundos;
    public float proximaAvaliacaoSegundos;
    public float tempoPermanenciaParcialSegundos;
    public long investimentoAcumulado;
    public long custoPermanenciaAcumulado;
    public int baixasInimigas;
    public int perdasMilitaresNacionais;
    public int veiculosEAeronavesPerdidos;
    public int unidadesGuarnicao;
    public bool reforcosInimigosBloqueados;
    public bool recompensaPrincipalPaga;
    public bool recompensaAdicionalPaga;
    public bool avaliacaoPendente;
    public int proprietarioBasePermanenteTeamId;
    public int paisAliadoSelecionadoTeamId;
    public string propostaTransferenciaId = string.Empty;
    public List<string> objetivosTerritoriaisRegistrados = new List<string>();
    public List<string> unidadesInimigasRegistradas = new List<string>();
    public List<string> unidadesPropriasVinculadas = new List<string>();
    public List<string> baixasProcessadas = new List<string>();
    public List<SaveGrupoResistenciaPacificacao> gruposDeResistencia = new List<SaveGrupoResistenciaPacificacao>();
}

/// <summary>
/// Controlador apenas da Operação Pacificação. Usa os relógios, eventos de
/// combate, diplomacia, economia e registro de unidades já existentes.
/// </summary>
[DisallowMultipleComponent]
public sealed class OperacaoPacificacaoControlador : MonoBehaviour
{
    public const string IdInicio = "Creatys_PAC_Inicio";
    public const string IdLimite = "Creatys_PAC_Limite";
    public const string IdBloqueioSpawn = "Creatys_PAC_BloqueioSpawn";
    public const string IdControleFinal = "Creatys_PAC_ControleFinal";
    public const string IdAreaPermanencia = "Creatys_PAC_Area_PermanenciaMilitar";
    public const string IdRetirada = "Creatys_PAC_Retirada";
    public const string IdBasePermanente = "Creatys_PAC_BasePermanente";
    public const string IdGuarnicao = "Creatys_PAC_Guarnicao";
    public const string IdTransferencia = "Creatys_PAC_TransferenciaControle";

    public static OperacaoPacificacaoControlador Instancia { get; private set; }
    public event Action EstadoAlterado;
    public event Action ReforcosInimigosBloqueados;

    [Header("Avaliação de investimento")]
    [SerializeField, Min(1f)] private float primeiraAvaliacaoMinutos = 30f;
    [SerializeField, Min(1f)] private float intervaloAvaliacaoMinutos = 10f;
    [Header("Economia")]
    [SerializeField, Min(0)] private long custoDiarioPermanencia = 50000000L;
    [Header("Recompensas")]
    [SerializeField, Min(0)] private long recompensaPrincipal = 5000000000L;
    [SerializeField, Min(0)] private long bonusBasePermanente = 5000000000L;
    [SerializeField, Min(0)] private long bonusRetirada = 2000000000L;
    [SerializeField, Min(0)] private long bonusTransferencia = 500000000L;
    [Header("Controle da missão")]
    [SerializeField] private bool derrotaAutomaticaPorTempo = false;
    [SerializeField] private bool bloquearSpawnAoConcluirAreas = true;
    [SerializeField] private bool permitirCombateSemInvestimento = true;
    [SerializeField, Min(0.25f)] private float intervaloConsultaPresenca = 2f;
    [SerializeField, Min(0.25f)] private float intervaloAtualizacaoInterface = 1f;
    [SerializeField] private List<string> identificadoresAreasChegada = new List<string>
    {
        "Creatys_PAC_Chegada_01",
        "Creatys_PAC_Chegada_02",
        "Creatys_PAC_Chegada_03",
        "Creatys_PAC_Chegada_04",
        "Creatys_PAC_Chegada_05"
    };
    [Header("Integração territorial e diplomática")]
    [SerializeField] private List<int> equipesInimigasDaOperacao = new List<int>();

    private readonly Dictionary<string, ZonaMissaoPacificacao> zonasPorId = new Dictionary<string, ZonaMissaoPacificacao>(StringComparer.Ordinal);
    private readonly HashSet<string> objetivosRegistrados = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> unidadesInimigasRegistradas = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> unidadesPropriasVinculadas = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> baixasProcessadas = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> gruposDeResistencia = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<IdentidadeUnidade> bufferUnidades = new List<IdentidadeUnidade>(256);
    private readonly List<ZonaMissaoPacificacao> bufferZonas = new List<ZonaMissaoPacificacao>(16);
    private readonly List<string> bufferObjetivosPresentes = new List<string>(5);

    private ProgressoTerritorialPacificacao progressoTerritorial = ProgressoTerritorialPacificacao.Disponivel;
    private EstadoInvestimentoPacificacao estadoInvestimento = EstadoInvestimentoPacificacao.Ativo;
    private DecisaoFinalPacificacao decisaoFinal = DecisaoFinalPacificacao.Nenhuma;
    private float tempoOperacaoSegundos;
    private float proximaAvaliacaoSegundos;
    private float tempoPermanenciaParcialSegundos;
    private float proximaConsultaPresencaSegundos;
    private float proximaAtualizacaoInterfaceSegundos;
    private float ultimoTempoSimulacao = -1f;
    private long investimentoAcumulado;
    private long custoPermanenciaAcumulado;
    private int baixasInimigas;
    private int perdasMilitaresNacionais;
    private int veiculosEAeronavesPerdidos;
    private int unidadesGuarnicao;
    private int proprietarioBasePermanenteTeamId;
    private int paisAliadoSelecionadoTeamId;
    private string propostaTransferenciaId = string.Empty;
    private bool reforcosInimigosBloqueados;
    private bool recompensaPrincipalPaga;
    private bool recompensaAdicionalPaga;
    private bool avaliacaoPendente;
    private bool haPresencaNaOperacao;
    private bool haPresencaNaAreaPermanencia;
    private bool haPresencaNaGuarnicao;
    private bool consultandoPresenca;
    private int teamJogadorOperacao;
    private RTSSimulationClock relogioInscrito;
    private GerenciadorTempo calendarioInscrito;
    private SistemaGovernoMundial governoInscrito;

    public EstadoMissaoPacificacao Estado => ResolverEstadoPublico();
    public EstadoInvestimentoPacificacao EstadoInvestimento => estadoInvestimento;
    public DecisaoFinalPacificacao DecisaoFinal => decisaoFinal;
    public float TempoOperacaoSegundos => tempoOperacaoSegundos;
    public long InvestimentoAcumulado => investimentoAcumulado;
    public long CustoPermanenciaAcumulado => custoPermanenciaAcumulado;
    public int BaixasInimigas => baixasInimigas;
    public int PerdasMilitaresNacionais => perdasMilitaresNacionais;
    public int VeiculosEAeronavesPerdidos => veiculosEAeronavesPerdidos;
    public int ObjetivosTerritoriaisRegistrados => objetivosRegistrados.Count;
    public int TotalObjetivosTerritoriais => IdentificadoresDeChegada().Distinct().Count();
    public bool ReforcosInimigosEstaoBloqueados => reforcosInimigosBloqueados;
    public bool ReforcosPropriosDaMissaoPermitidos => estadoInvestimento == EstadoInvestimentoPacificacao.Ativo
        && progressoTerritorial != ProgressoTerritorialPacificacao.Concluida
        && progressoTerritorial != ProgressoTerritorialPacificacao.Abandonada;
    public bool PermitirCombateSemInvestimento => permitirCombateSemInvestimento;
    public bool DerrotaAutomaticaPorTempo => derrotaAutomaticaPorTempo;
    public float ProximaAtualizacaoInterfaceSegundos => intervaloAtualizacaoInterface;
    public long RecompensaPrincipal => recompensaPrincipal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSingletonRuntime()
    {
        Instancia = null;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            enabled = false;
            return;
        }

        Instancia = this;
        ZonaMissaoPacificacao.PreencherRegistradas(bufferZonas);
        for (int i = 0; i < bufferZonas.Count; i++) RegistrarZona(bufferZonas[i]);
        InscreverRelogio();
        InscreverCalendario();
    }

    private void OnEnable()
    {
        SistemaDeDanos.OnMorteGlobal += AoMorrerUnidade;
        ZonaMissaoPacificacao.RegistroAlterado += AoAlterarRegistroZona;
        InscreverRelogio();
        InscreverCalendario();
    }

    private void Start()
    {
        InscreverRelogio();
        InscreverCalendario();
    }

    private void OnDisable()
    {
        SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
        ZonaMissaoPacificacao.RegistroAlterado -= AoAlterarRegistroZona;
        if (relogioInscrito != null) relogioInscrito.OnTick -= AoTickSimulacao;
        if (calendarioInscrito != null) calendarioInscrito.OnDataAlterada -= AoMudarData;
        if (governoInscrito != null) governoInscrito.OnGovernoAtualizado -= AoAtualizarGoverno;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private EstadoMissaoPacificacao ResolverEstadoPublico()
    {
        if (progressoTerritorial == ProgressoTerritorialPacificacao.Concluida) return EstadoMissaoPacificacao.Concluida;
        if (progressoTerritorial == ProgressoTerritorialPacificacao.Abandonada) return EstadoMissaoPacificacao.Abandonada;
        if (estadoInvestimento == EstadoInvestimentoPacificacao.AguardandoDecisao)
            return EstadoMissaoPacificacao.AguardandoDecisaoInvestimento;
        if (estadoInvestimento == EstadoInvestimentoPacificacao.Suspenso
            && progressoTerritorial != ProgressoTerritorialPacificacao.Disponivel
            && progressoTerritorial != ProgressoTerritorialPacificacao.AguardandoDecisaoFinal
            && progressoTerritorial != ProgressoTerritorialPacificacao.EstabelecendoBase
            && progressoTerritorial != ProgressoTerritorialPacificacao.RetiradaFinal
            && progressoTerritorial != ProgressoTerritorialPacificacao.TransferindoControle)
            return EstadoMissaoPacificacao.InvestimentoSuspenso;

        switch (progressoTerritorial)
        {
            case ProgressoTerritorialPacificacao.AguardandoChegada: return EstadoMissaoPacificacao.AguardandoChegada;
            case ProgressoTerritorialPacificacao.OperacaoAtiva: return EstadoMissaoPacificacao.OperacaoAtiva;
            case ProgressoTerritorialPacificacao.RetiradaEmAndamento: return EstadoMissaoPacificacao.RetiradaEmAndamento;
            case ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos: return EstadoMissaoPacificacao.ObjetivosTerritoriaisConcluidos;
            case ProgressoTerritorialPacificacao.ResistenciaRemanescente: return EstadoMissaoPacificacao.ResistenciaRemanescente;
            case ProgressoTerritorialPacificacao.AguardandoDecisaoFinal: return EstadoMissaoPacificacao.AguardandoDecisaoFinal;
            case ProgressoTerritorialPacificacao.EstabelecendoBase: return EstadoMissaoPacificacao.EstabelecendoBase;
            case ProgressoTerritorialPacificacao.RetiradaFinal: return EstadoMissaoPacificacao.RetiradaFinal;
            case ProgressoTerritorialPacificacao.TransferindoControle: return EstadoMissaoPacificacao.TransferindoControle;
            default: return EstadoMissaoPacificacao.Disponivel;
        }
    }

    private void InscreverRelogio()
    {
        RTSSimulationClock atual = RTSSimulationClock.Instancia;
        if (atual == relogioInscrito) return;
        if (relogioInscrito != null) relogioInscrito.OnTick -= AoTickSimulacao;
        relogioInscrito = atual;
        if (relogioInscrito != null)
        {
            ultimoTempoSimulacao = relogioInscrito.SimulationTime;
            relogioInscrito.OnTick += AoTickSimulacao;
        }
    }

    private void InscreverCalendario()
    {
        GerenciadorTempo atual = GerenciadorTempo.Instancia;
        if (atual == calendarioInscrito) return;
        if (calendarioInscrito != null) calendarioInscrito.OnDataAlterada -= AoMudarData;
        calendarioInscrito = atual;
        if (calendarioInscrito != null) calendarioInscrito.OnDataAlterada += AoMudarData;
    }

    private void GarantirAssinaturaGoverno()
    {
        SistemaGovernoMundial atual = SistemaGovernoMundial.Instancia;
        if (atual == governoInscrito) return;
        if (governoInscrito != null) governoInscrito.OnGovernoAtualizado -= AoAtualizarGoverno;
        governoInscrito = atual;
        if (governoInscrito != null) governoInscrito.OnGovernoAtualizado += AoAtualizarGoverno;
    }

    private void AoAtualizarGoverno()
    {
        GarantirAssinaturaGoverno();
        VerificarRespostaTransferenciaPendente();
    }

    private void AoAlterarRegistroZona(ZonaMissaoPacificacao zona, bool registrada)
    {
        if (registrada) RegistrarZona(zona);
        else if (zona != null && !string.IsNullOrWhiteSpace(zona.IdentificadorUnico)
            && zonasPorId.TryGetValue(zona.IdentificadorUnico, out ZonaMissaoPacificacao existente)
            && existente == zona)
            zonasPorId.Remove(zona.IdentificadorUnico);
        NotificarEstadoAlterado();
    }

    private void RegistrarZona(ZonaMissaoPacificacao zona)
    {
        if (zona == null || string.IsNullOrWhiteSpace(zona.IdentificadorUnico)) return;
        if (zonasPorId.TryGetValue(zona.IdentificadorUnico, out ZonaMissaoPacificacao existente)
            && existente != null && existente != zona)
        {
            Debug.LogWarning("Creaty da Operação Pacificação duplicado: " + zona.IdentificadorUnico, zona);
            return;
        }

        zonasPorId[zona.IdentificadorUnico] = zona;
    }

    private void AoTickSimulacao(int tick, float tempoSimulacao)
    {
        InscreverCalendario();
        GarantirAssinaturaGoverno();
        float delta = ultimoTempoSimulacao < 0f ? 0f : Mathf.Max(0f, tempoSimulacao - ultimoTempoSimulacao);
        ultimoTempoSimulacao = tempoSimulacao;

        if (OperacaoEmAndamento())
        {
            tempoOperacaoSegundos += delta;
            if (estadoInvestimento == EstadoInvestimentoPacificacao.Suspenso && haPresencaNaAreaPermanencia)
                tempoPermanenciaParcialSegundos += delta;
        }

        bool precisaConsultarPresenca = OperacaoEmAndamento()
            || progressoTerritorial == ProgressoTerritorialPacificacao.RetiradaFinal;
        if (precisaConsultarPresenca && tempoSimulacao >= proximaConsultaPresencaSegundos)
        {
            AtualizarPresencaLocal();
            proximaConsultaPresencaSegundos = tempoSimulacao + Mathf.Max(0.25f, intervaloConsultaPresenca);
        }

        if (progressoTerritorial == ProgressoTerritorialPacificacao.RetiradaEmAndamento && !haPresencaNaOperacao)
            FinalizarRetirada();

        if (progressoTerritorial == ProgressoTerritorialPacificacao.RetiradaFinal && !haPresencaNaOperacao)
            FinalizarRetirada();

        if (progressoTerritorial == ProgressoTerritorialPacificacao.ResistenciaRemanescente
            && ObjetivosConcluidos() && ResistenciaInimigaRestante == 0)
            ConcluirVitoria();

        if (estadoInvestimento == EstadoInvestimentoPacificacao.Ativo
            && (progressoTerritorial == ProgressoTerritorialPacificacao.OperacaoAtiva
                || progressoTerritorial == ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos
                || progressoTerritorial == ProgressoTerritorialPacificacao.ResistenciaRemanescente)
            && proximaAvaliacaoSegundos > 0f && tempoOperacaoSegundos >= proximaAvaliacaoSegundos)
            AvaliarInvestimento();
    }

    private void AoMudarData()
    {
        if (estadoInvestimento != EstadoInvestimentoPacificacao.Suspenso || !OperacaoEmAndamento()) return;
        AtualizarPresencaLocal();
        if (!haPresencaNaAreaPermanencia) return;

        float duracaoDia = calendarioInscrito != null ? Mathf.Max(1f, calendarioInscrito.duracaoDiaSegundos) : 30f;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        int team = ResolverTeamJogador(governo);
        int diasCobrados = 0;
        while (tempoPermanenciaParcialSegundos >= duracaoDia)
        {
            if (governo == null || !governo.TentarPagar(team, custoDiarioPermanencia,
                    "Permanência militar — Operação Pacificação", "Operação Pacificação", TipoLancamentoFinanceiro.DespesaDiaria))
            {
                if (diasCobrados == 0)
                    governo?.RegistrarNoticia("Operação Pacificação: saldo insuficiente para pagar o custo de permanência militar.");
                break;
            }

            tempoPermanenciaParcialSegundos -= duracaoDia;
            custoPermanenciaAcumulado = SomarSaturado(custoPermanenciaAcumulado, custoDiarioPermanencia);
            diasCobrados++;
        }

        if (diasCobrados > 0)
        {
            governo.RegistrarNoticia("Operação Pacificação: permanência militar cobrada por " + diasCobrados + " dia(s).");
            NotificarEstadoAlterado();
        }
    }

    private void AtualizarPresencaLocal()
    {
        if (consultandoPresenca) return;
        consultandoPresenca = true;
        string idLimite = IdLimite;
        ZonaMissaoPacificacao limite = ObterZona(idLimite);
        ZonaMissaoPacificacao areaPermanencia = ObterZona(IdAreaPermanencia);
        ZonaMissaoPacificacao guarnicao = ObterZona(IdGuarnicao);
        haPresencaNaOperacao = false;
        haPresencaNaAreaPermanencia = false;
        haPresencaNaGuarnicao = false;
        unidadesGuarnicao = 0;
        bufferObjetivosPresentes.Clear();

        try
        {
            RegistroEntidadesJogo.FillUnidades(bufferUnidades);
            int teamJogador = ResolverTeamJogador(SistemaGovernoMundial.Instancia);
            for (int i = 0; i < bufferUnidades.Count; i++)
            {
                IdentidadeUnidade unidade = bufferUnidades[i];
                if (unidade == null || !unidade.isActiveAndEnabled || unidade.tipoUnidade == TipoUnidade.Estrutura) continue;

                Vector3 posicao = unidade.transform.position;
                if (unidade.teamID != teamJogador)
                {
                    if (EquipeHostil(unidade.teamID) && EstaDentroDeAreaOperacional(posicao))
                        unidadesInimigasRegistradas.Add(ResolverIdPersistente(unidade.gameObject));
                    continue;
                }

                string idUnidade = ResolverIdPersistente(unidade.gameObject);
                if (limite != null && limite.Contem(posicao))
                {
                    haPresencaNaOperacao = true;
                    unidadesPropriasVinculadas.Add(idUnidade);
                }
                if (areaPermanencia != null && areaPermanencia.Contem(posicao)) haPresencaNaAreaPermanencia = true;
                if (guarnicao != null && guarnicao.Contem(posicao))
                {
                    haPresencaNaGuarnicao = true;
                    unidadesGuarnicao++;
                }

                if (OperacaoEmAndamento())
                {
                    if (identificadoresAreasChegada != null)
                    {
                        for (int objetivoIndex = 0; objetivoIndex < identificadoresAreasChegada.Count; objetivoIndex++)
                        {
                            string idObjetivo = identificadoresAreasChegada[objetivoIndex];
                            ZonaMissaoPacificacao zonaObjetivo = ObterZona(idObjetivo);
                            if (zonaObjetivo != null && zonaObjetivo.Contem(posicao)
                                && !objetivosRegistrados.Contains(idObjetivo)
                                && !bufferObjetivosPresentes.Contains(idObjetivo))
                                bufferObjetivosPresentes.Add(idObjetivo);
                        }
                    }
                }
            }

            if (teamJogadorOperacao == 0) teamJogadorOperacao = teamJogador;
            for (int i = 0; i < bufferObjetivosPresentes.Count; i++)
                RegistrarObjetivoTerritorial(bufferObjetivosPresentes[i]);
        }
        finally
        {
            consultandoPresenca = false;
        }
    }

    private void AoMorrerUnidade(SistemaDeDanos danos, GameObject agressor)
    {
        if (danos == null) return;
        IdentidadeUnidade vitima = danos.GetComponent<IdentidadeUnidade>()
            ?? danos.GetComponentInParent<IdentidadeUnidade>()
            ?? danos.GetComponentInChildren<IdentidadeUnidade>(true);
        if (vitima == null || !OperacaoEmAndamento()) return;

        string idVitima = ResolverIdPersistente(vitima.gameObject);
        int teamJogador = ResolverTeamJogador(SistemaGovernoMundial.Instancia);
        if (vitima.teamID == teamJogador)
        {
            if (!unidadesPropriasVinculadas.Contains(idVitima)
                && !EstaDentroDeAreaOperacional(vitima.transform.position)) return;
            if (!baixasProcessadas.Add(idVitima)) return;
            unidadesPropriasVinculadas.Remove(idVitima);
            int quantidade = Mathf.Max(1, vitima.militaresConsumidos);
            if (vitima.tipoUnidade == TipoUnidade.Infantaria)
                perdasMilitaresNacionais = SaturarInteiro(perdasMilitaresNacionais, quantidade);
            else if (vitima.tipoUnidade == TipoUnidade.Veiculo || vitima.tipoUnidade == TipoUnidade.Aereo
                || vitima.tipoUnidade == TipoUnidade.Naval)
                veiculosEAeronavesPerdidos = SaturarInteiro(veiculosEAeronavesPerdidos, 1);
            NotificarEstadoAlterado();
            return;
        }

        if (!EquipeHostil(vitima.teamID) || !EstaDentroDeAreaOperacional(vitima.transform.position)) return;
        if (!baixasProcessadas.Add(idVitima)) return;
        unidadesInimigasRegistradas.Remove(idVitima);

        IdentidadeUnidade identidadeAgressora = agressor != null
            ? agressor.GetComponent<IdentidadeUnidade>() ?? agressor.GetComponentInParent<IdentidadeUnidade>()
            : null;
        if (identidadeAgressora != null && identidadeAgressora.teamID == teamJogador)
            baixasInimigas = SaturarInteiro(baixasInimigas, Mathf.Max(1, vitima.militaresConsumidos));

        if (ObjetivosConcluidos() && ResistenciaInimigaRestante == 0)
            ConcluirVitoria();
        else NotificarEstadoAlterado();
    }

    private bool EquipeHostil(int teamId)
    {
        if (teamId <= 0 || teamId == ResolverTeamJogador(SistemaGovernoMundial.Instancia)) return false;
        if (equipesInimigasDaOperacao != null && equipesInimigasDaOperacao.Contains(teamId)) return true;
        RelacaoPaisGoverno relacao = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterRelacao(ResolverTeamJogador(SistemaGovernoMundial.Instancia), teamId)
            : null;
        return relacao != null && relacao.guerraDeclarada;
    }

    private bool EstaDentroDeAreaOperacional(Vector3 posicao)
    {
        ZonaMissaoPacificacao inicio = ObterZona(IdInicio);
        if (inicio != null && inicio.Contem(posicao)) return true;
        ZonaMissaoPacificacao limite = ObterZona(IdLimite);
        if (limite != null && limite.Contem(posicao)) return true;
        foreach (string id in IdentificadoresDeChegada())
        {
            ZonaMissaoPacificacao zona = ObterZona(id);
            if (zona != null && zona.Contem(posicao)) return true;
        }
        return false;
    }

    private static string ResolverIdPersistente(GameObject objeto)
    {
        if (objeto == null) return string.Empty;
        SaveableEntity salvavel = SaveableEntity.Garantir(objeto);
        return salvavel != null && !string.IsNullOrWhiteSpace(salvavel.UniqueId)
            ? salvavel.UniqueId
            : "instancia:" + objeto.GetInstanceID();
    }

    private static int ResolverTeamJogador(SistemaGovernoMundial governo)
    {
        return governo != null ? Mathf.Max(1, governo.teamJogador) : 1;
    }

    public bool ConfiguracaoOperacionalCompleta(out string pendencias)
    {
        List<string> ausentes = new List<string>(10);
        ExigirZona(IdInicio, ausentes);
        ExigirZona(IdLimite, ausentes);
        ExigirZona(IdBloqueioSpawn, ausentes);
        ExigirZona(IdControleFinal, ausentes);
        ExigirZona(IdAreaPermanencia, ausentes);
        ExigirZona(IdRetirada, ausentes);
        foreach (string id in IdentificadoresDeChegada()) ExigirZona(id, ausentes);
        HashSet<string> idsObjetivo = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in IdentificadoresDeChegada())
            if (!idsObjetivo.Add(id)) ausentes.Add("identificador de chegada duplicado: " + id);
        pendencias = ausentes.Count == 0 ? string.Empty : string.Join(", ", ausentes);
        return ausentes.Count == 0;
    }

    public bool ConfiguracaoPosGuerraCompleta(out string pendencias)
    {
        List<string> ausentes = new List<string>(3);
        ExigirZona(IdBasePermanente, ausentes);
        ExigirZona(IdGuarnicao, ausentes);
        ExigirZona(IdTransferencia, ausentes);
        pendencias = ausentes.Count == 0 ? string.Empty : string.Join(", ", ausentes);
        return ausentes.Count == 0;
    }

    private void ExigirZona(string id, List<string> pendencias)
    {
        if (ObterZona(id) == null) pendencias.Add(id);
    }

    private IEnumerable<string> IdentificadoresDeChegada()
    {
        if (identificadoresAreasChegada == null) yield break;
        for (int i = 0; i < identificadoresAreasChegada.Count; i++)
        {
            string id = identificadoresAreasChegada[i];
            if (!string.IsNullOrWhiteSpace(id)) yield return id;
        }
    }

    private ZonaMissaoPacificacao ObterZona(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return zonasPorId.TryGetValue(id, out ZonaMissaoPacificacao zona) && zona != null && zona.isActiveAndEnabled
            ? zona : null;
    }

    public bool TentarIniciar(out string mensagem)
    {
        mensagem = string.Empty;
        if (progressoTerritorial != ProgressoTerritorialPacificacao.Disponivel)
        {
            mensagem = "A Operação Pacificação já foi iniciada ou encerrada.";
            return false;
        }
        if (!ConfiguracaoOperacionalCompleta(out string pendencias))
        {
            mensagem = "Configure as áreas da missão no GlobalMapRTS antes de iniciar: " + pendencias + ".";
            return false;
        }
        if (SistemaGovernoMundial.Instancia == null)
        {
            mensagem = "O sistema de governo não está disponível.";
            return false;
        }

        teamJogadorOperacao = ResolverTeamJogador(SistemaGovernoMundial.Instancia);
        objetivosRegistrados.Clear();
        baixasProcessadas.Clear();
        unidadesInimigasRegistradas.Clear();
        unidadesPropriasVinculadas.Clear();
        gruposDeResistencia.Clear();
        baixasInimigas = 0;
        perdasMilitaresNacionais = 0;
        veiculosEAeronavesPerdidos = 0;
        investimentoAcumulado = 0L;
        custoPermanenciaAcumulado = 0L;
        tempoPermanenciaParcialSegundos = 0f;
        tempoOperacaoSegundos = 0f;
        proximaAvaliacaoSegundos = 0f;
        recompensaPrincipalPaga = false;
        recompensaAdicionalPaga = false;
        reforcosInimigosBloqueados = false;
        avaliacaoPendente = false;
        estadoInvestimento = EstadoInvestimentoPacificacao.Ativo;
        decisaoFinal = DecisaoFinalPacificacao.Nenhuma;
        progressoTerritorial = ProgressoTerritorialPacificacao.AguardandoChegada;
        SistemaGovernoMundial.Instancia.RegistrarNoticia("Operação Pacificação autorizada. As forças devem alcançar a área de ativação.");
        NotificarEstadoAlterado();
        mensagem = "Operação Pacificação autorizada. O relógio começa quando as forças alcançarem a área de ativação.";
        return true;
    }

    public void RegistrarEntradaEmArea(ZonaMissaoPacificacao zona, IdentidadeUnidade unidade)
    {
        if (zona == null || unidade == null || unidade.tipoUnidade == TipoUnidade.Estrutura) return;

        int teamJogador = ResolverTeamJogador(SistemaGovernoMundial.Instancia);
        if (unidade.teamID != teamJogador)
        {
            RegistrarUnidadeInimiga(unidade);
            return;
        }

        string idUnidade = ResolverIdPersistente(unidade.gameObject);
        bool entrouNaAtivacao = zona.IdentificadorUnico == IdInicio
            && progressoTerritorial == ProgressoTerritorialPacificacao.AguardandoChegada;
        if (!entrouNaAtivacao && !EstaDentroDeAreaOperacional(unidade.transform.position)) return;
        unidadesPropriasVinculadas.Add(idUnidade);

        if (progressoTerritorial == ProgressoTerritorialPacificacao.AguardandoChegada
            && zona.IdentificadorUnico == IdInicio)
        {
            progressoTerritorial = ProgressoTerritorialPacificacao.OperacaoAtiva;
            estadoInvestimento = EstadoInvestimentoPacificacao.Ativo;
            tempoOperacaoSegundos = 0f;
            proximaAvaliacaoSegundos = Mathf.Max(1f, primeiraAvaliacaoMinutos) * 60f;
            ultimoTempoSimulacao = relogioInscrito != null ? relogioInscrito.SimulationTime : 0f;
            SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Pacificação em andamento na região designada.");
            NotificarEstadoAlterado();
            return;
        }

        if (progressoTerritorial != ProgressoTerritorialPacificacao.OperacaoAtiva
            && progressoTerritorial != ProgressoTerritorialPacificacao.RetiradaEmAndamento
            && progressoTerritorial != ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos
            && progressoTerritorial != ProgressoTerritorialPacificacao.ResistenciaRemanescente)
            return;

        if (IdentificadoresDeChegada().Contains(zona.IdentificadorUnico)) RegistrarObjetivoTerritorial(zona.IdentificadorUnico);
    }

    private void RegistrarObjetivoTerritorial(string identificador)
    {
        if (!OperacaoEmAndamento()) return;
        if (objetivosRegistrados.Add(identificador))
        {
            if (ObjetivosConcluidos())
            {
                if (bloquearSpawnAoConcluirAreas) BloquearReforcosInimigosUmaVez();
                progressoTerritorial = ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos;
                if (ResistenciaInimigaRestante > 0)
                    progressoTerritorial = ProgressoTerritorialPacificacao.ResistenciaRemanescente;
                else
                    ConcluirVitoria();
            }
            NotificarEstadoAlterado();
        }
    }

    private bool ObjetivosConcluidos()
    {
        int total = TotalObjetivosTerritoriais;
        return total > 0 && objetivosRegistrados.Count >= total;
    }

    private void BloquearReforcosInimigosUmaVez()
    {
        if (reforcosInimigosBloqueados) return;
        reforcosInimigosBloqueados = true;
        ReforcosInimigosBloqueados?.Invoke();
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Pacificação: os objetivos territoriais foram concluídos; novos reforços inimigos estão bloqueados nesta operação.");
    }

    public bool PodeGerarReforcosInimigos()
    {
        return !reforcosInimigosBloqueados
            && progressoTerritorial != ProgressoTerritorialPacificacao.Concluida
            && progressoTerritorial != ProgressoTerritorialPacificacao.Abandonada;
    }

    public bool RegistrarUnidadeInimiga(IdentidadeUnidade unidade)
    {
        if (unidade == null || !unidade.isActiveAndEnabled || unidade.tipoUnidade == TipoUnidade.Estrutura
            || !EquipeHostil(unidade.teamID) || !OperacaoEmAndamento()
            || !EstaDentroDeAreaOperacional(unidade.transform.position))
            return false;

        bool registrada = unidadesInimigasRegistradas.Add(ResolverIdPersistente(unidade.gameObject));
        if (registrada) NotificarEstadoAlterado();
        return registrada;
    }

    public bool RegistrarGrupoResistenciaInimiga(string identificadorGrupo, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        if (string.IsNullOrWhiteSpace(identificadorGrupo) || quantidade <= 0)
        {
            mensagem = "Informe um identificador de grupo e uma quantidade válidos.";
            return false;
        }
        if (!PodeGerarReforcosInimigos())
        {
            mensagem = "Os reforços inimigos desta operação já foram bloqueados.";
            return false;
        }

        if (gruposDeResistencia.ContainsKey(identificadorGrupo))
        {
            mensagem = "Esse grupo de resistência já está registrado.";
            return false;
        }
        gruposDeResistencia.Add(identificadorGrupo, quantidade);
        NotificarEstadoAlterado();
        return true;
    }

    public bool RegistrarBaixasDeGrupoResistencia(string identificadorGrupo, int quantidade)
    {
        if (string.IsNullOrWhiteSpace(identificadorGrupo) || quantidade <= 0
            || !gruposDeResistencia.TryGetValue(identificadorGrupo, out int restantes)) return false;
        int baixasRegistradas = Mathf.Min(quantidade, restantes);
        restantes = Mathf.Max(0, restantes - baixasRegistradas);
        baixasInimigas = SaturarInteiro(baixasInimigas, baixasRegistradas);
        if (restantes == 0) gruposDeResistencia.Remove(identificadorGrupo);
        else gruposDeResistencia[identificadorGrupo] = restantes;
        if (ObjetivosConcluidos() && ResistenciaInimigaRestante == 0) ConcluirVitoria();
        else NotificarEstadoAlterado();
        return true;
    }

    public int ResistenciaInimigaRestante => unidadesInimigasRegistradas.Count + gruposDeResistencia.Values.Sum();

    public void RegistrarInvestimentoOperacao(long valor)
    {
        if (valor <= 0L || !OperacaoEmAndamento() || estadoInvestimento != EstadoInvestimentoPacificacao.Ativo) return;
        investimentoAcumulado = SomarSaturado(investimentoAcumulado, valor);
        NotificarEstadoAlterado();
    }

    private bool OperacaoEmAndamento()
    {
        return progressoTerritorial == ProgressoTerritorialPacificacao.OperacaoAtiva
            || progressoTerritorial == ProgressoTerritorialPacificacao.RetiradaEmAndamento
            || progressoTerritorial == ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos
            || progressoTerritorial == ProgressoTerritorialPacificacao.ResistenciaRemanescente;
    }

    private void AvaliarInvestimento()
    {
        float intervalo = Mathf.Max(1f, intervaloAvaliacaoMinutos) * 60f;
        if (perdasMilitaresNacionais > baixasInimigas)
        {
            estadoInvestimento = EstadoInvestimentoPacificacao.AguardandoDecisao;
            avaliacaoPendente = true;
            SistemaGovernoMundial.Instancia?.RegistrarNoticia(
                "COMANDO MILITAR NACIONAL — Investimento militar em risco. Perdas nacionais: "
                + perdasMilitaresNacionais.ToString("N0") + "; baixas inimigas: " + baixasInimigas.ToString("N0")
                + "; investimento: AT$ " + investimentoAcumulado.ToString("N0") + ". O Menu Governo aguarda uma decisão presidencial.");
            NotificarEstadoAlterado();
            return;
        }

        proximaAvaliacaoSegundos = tempoOperacaoSegundos + intervalo;
    }

    public bool DecidirContinuarInvestimento(out string mensagem)
    {
        mensagem = string.Empty;
        if (estadoInvestimento != EstadoInvestimentoPacificacao.AguardandoDecisao)
        {
            mensagem = "Não há uma decisão de investimento pendente.";
            return false;
        }
        estadoInvestimento = EstadoInvestimentoPacificacao.Ativo;
        avaliacaoPendente = false;
        proximaAvaliacaoSegundos = tempoOperacaoSegundos + Mathf.Max(1f, intervaloAvaliacaoMinutos) * 60f;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Governo autorizou a continuidade do investimento na Operação Pacificação.");
        NotificarEstadoAlterado();
        mensagem = "O investimento foi mantido. A próxima avaliação será em " + intervaloAvaliacaoMinutos.ToString("0.#") + " minutos de simulação.";
        return true;
    }

    public bool SuspenderInvestimento(out string mensagem)
    {
        mensagem = string.Empty;
        if (estadoInvestimento != EstadoInvestimentoPacificacao.AguardandoDecisao)
        {
            mensagem = "Não há uma decisão de investimento pendente.";
            return false;
        }
        estadoInvestimento = EstadoInvestimentoPacificacao.Suspenso;
        avaliacaoPendente = false;
        AtualizarPresencaLocal();
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Governo suspendeu os reforços automáticos da Operação Pacificação. As unidades existentes permanecem sob controle do jogador.");
        NotificarEstadoAlterado();
        mensagem = "Investimento suspenso. As forças existentes continuam operacionais; não haverá retirada automática.";
        return true;
    }

    public bool OrdenarRetirada(out string mensagem)
    {
        mensagem = string.Empty;
        if (!OperacaoEmAndamento())
        {
            mensagem = "A retirada não está disponível neste estado da missão.";
            return false;
        }
        progressoTerritorial = ProgressoTerritorialPacificacao.RetiradaEmAndamento;
        estadoInvestimento = EstadoInvestimentoPacificacao.Suspenso;
        AtualizarPresencaLocal();
        if (!haPresencaNaOperacao) FinalizarRetirada();
        else SistemaGovernoMundial.Instancia?.RegistrarNoticia("Retirada da Operação Pacificação ordenada. As unidades continuam sob controle do jogador; a operação não será encerrada automaticamente.");
        NotificarEstadoAlterado();
        mensagem = haPresencaNaOperacao
            ? "Retirada registrada. Mova suas unidades para fora da área; nenhuma será removida, e a missão continuará aberta."
            : "Nenhuma força própria permanece na região. A operação continua suspensa e pode ser retomada depois.";
        return true;
    }

    public bool AbandonarOperacao(out string mensagem)
    {
        mensagem = string.Empty;
        if (progressoTerritorial == ProgressoTerritorialPacificacao.Disponivel
            || progressoTerritorial == ProgressoTerritorialPacificacao.Concluida
            || progressoTerritorial == ProgressoTerritorialPacificacao.Abandonada)
        {
            mensagem = "A operação não pode ser abandonada neste estado.";
            return false;
        }
        progressoTerritorial = ProgressoTerritorialPacificacao.Abandonada;
        estadoInvestimento = EstadoInvestimentoPacificacao.Encerrado;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Pacificação abandonada por decisão presidencial. As unidades restantes continuam no mundo e não foram removidas.");
        NotificarEstadoAlterado();
        mensagem = "Operação abandonada definitivamente. As unidades restantes continuam no mapa.";
        return true;
    }

    private void FinalizarRetirada()
    {
        if (progressoTerritorial == ProgressoTerritorialPacificacao.RetiradaEmAndamento)
        {
            progressoTerritorial = ProgressoTerritorialPacificacao.OperacaoAtiva;
            estadoInvestimento = EstadoInvestimentoPacificacao.Suspenso;
            SistemaGovernoMundial.Instancia?.RegistrarNoticia("A retirada das forças da área foi concluída. A Operação Pacificação continua suspensa e não foi encerrada.");
            NotificarEstadoAlterado();
            return;
        }

        if (progressoTerritorial != ProgressoTerritorialPacificacao.RetiradaFinal) return;
        if (!PagarBonusFinal(bonusRetirada, "Retirada completa")) return;
        progressoTerritorial = ProgressoTerritorialPacificacao.Concluida;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Pacificação concluída após retirada completa das forças. A região foi devolvida à administração local.");
        NotificarEstadoAlterado();
    }

    private void ConcluirVitoria()
    {
        if (progressoTerritorial == ProgressoTerritorialPacificacao.AguardandoDecisaoFinal
            || progressoTerritorial == ProgressoTerritorialPacificacao.Concluida
            || progressoTerritorial == ProgressoTerritorialPacificacao.Abandonada) return;
        if (!ObjetivosConcluidos()) return;
        AtualizarPresencaLocal();
        if (ResistenciaInimigaRestante > 0) return;

        BloquearReforcosInimigosUmaVez();
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        int team = ResolverTeamJogador(governo);
        if (governo == null || governo.ObterPais(team) == null) return;
        if (!recompensaPrincipalPaga)
        {
            governo.AdicionarSaldo(team, recompensaPrincipal,
                "Recompensa principal — Operação Pacificação", "Operação Pacificação", TipoLancamentoFinanceiro.ReceitaTransacao);
            recompensaPrincipalPaga = true;
        }
        progressoTerritorial = ProgressoTerritorialPacificacao.AguardandoDecisaoFinal;
        estadoInvestimento = EstadoInvestimentoPacificacao.Encerrado;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("MISSÃO CONCLUÍDA — Operação Pacificação. A resistência foi encerrada; o Menu Governo aguarda a decisão sobre o futuro da região.");
        NotificarEstadoAlterado();
    }

    public bool TentarEstabelecerBasePermanente(out string mensagem)
    {
        mensagem = string.Empty;
        if (progressoTerritorial != ProgressoTerritorialPacificacao.AguardandoDecisaoFinal)
        {
            mensagem = "A decisão final da missão não está pendente.";
            return false;
        }
        if (!ConfiguracaoPosGuerraCompleta(out string pendencias))
        {
            mensagem = "Configure as áreas de base e guarnição antes de manter presença: " + pendencias + ".";
            return false;
        }
        AtualizarPresencaLocal();
        if (!haPresencaNaGuarnicao || unidadesGuarnicao <= 0)
        {
            mensagem = "A base precisa manter ao menos uma unidade existente na área de guarnição; a missão não criará tropas gratuitamente.";
            return false;
        }

        progressoTerritorial = ProgressoTerritorialPacificacao.EstabelecendoBase;
        decisaoFinal = DecisaoFinalPacificacao.BasePermanente;
        proprietarioBasePermanenteTeamId = ResolverTeamJogador(SistemaGovernoMundial.Instancia);
        if (!PagarBonusFinal(bonusBasePermanente, "Base permanente"))
        {
            progressoTerritorial = ProgressoTerritorialPacificacao.AguardandoDecisaoFinal;
            decisaoFinal = DecisaoFinalPacificacao.Nenhuma;
            mensagem = "Não foi possível registrar o bônus no tesouro nacional.";
            return false;
        }
        progressoTerritorial = ProgressoTerritorialPacificacao.Concluida;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Pacificação: presença militar permanente registrada com "
            + unidadesGuarnicao.ToString("N0") + " unidade(s) existente(s). Nenhuma unidade foi criada pela missão.");
        NotificarEstadoAlterado();
        mensagem = "Base permanente registrada com as unidades já presentes. O bônus foi creditado.";
        return true;
    }

    public bool TentarSolicitarTransferenciaControle(int teamAliado, out string mensagem)
    {
        mensagem = string.Empty;
        if (progressoTerritorial != ProgressoTerritorialPacificacao.AguardandoDecisaoFinal)
        {
            mensagem = "A decisão final da missão não está pendente.";
            return false;
        }
        if (ObterZona(IdTransferencia) == null)
        {
            mensagem = "Configure Creatys_PAC_TransferenciaControle no mapa antes de transferir a região.";
            return false;
        }
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno aliado = governo != null ? governo.ObterPais(teamAliado) : null;
        RelacaoPaisGoverno relacao = governo != null ? governo.ObterRelacao(ResolverTeamJogador(governo), teamAliado) : null;
        if (aliado == null || teamAliado == ResolverTeamJogador(governo)
            || !ContextoTerritorialDiplomatico.SaoAliadosParaApoioAereo(ResolverTeamJogador(governo), teamAliado)
            || relacao == null || relacao.guerraDeclarada || relacao.sancaoAtiva
            || aliado.saldo < 500000000L || aliado.estabilidade < 45f || aliado.nivelMilitar < 30)
        {
            mensagem = "Selecione uma nação aliada sem guerra ou sanções, com estabilidade, capacidade militar e ao menos AT$ 500 milhões.";
            return false;
        }

        if (!TentarObterTerritoriosDaOperacao(governo, out List<string> territorios, out mensagem)) return false;

        PropostaInternacional proposta = new PropostaInternacional
        {
            tipo = TipoPropostaInternacional.TransferenciaControlePosPacificacao,
            origemTeamId = ResolverTeamJogador(governo),
            alvoTeamId = teamAliado,
            quantidade = territorios.Count,
            prioridade = 90,
            motivo = "Transferência administrativa da região pacificada, condicionada à capacidade e ao aceite do país aliado.",
            expiraEm = Time.unscaledTime + 180f,
            dedupKey = "pacificacao_transferencia:" + ResolverTeamJogador(governo) + ":" + teamAliado,
            territoriosConcedidos = territorios
        };
        GarantirAssinaturaGoverno();
        if (!governo.TentarCriarProposta(proposta))
        {
            mensagem = "Já existe uma proposta de transferência pendente para esse país.";
            return false;
        }

        paisAliadoSelecionadoTeamId = teamAliado;
        propostaTransferenciaId = proposta.id;
        progressoTerritorial = ProgressoTerritorialPacificacao.TransferindoControle;
        governo.RegistrarNoticia("A transferência da região pacificada foi enviada a " + governo.NomePais(teamAliado) + " para decisão diplomática.");
        mensagem = "Proposta enviada a " + governo.NomePais(teamAliado) + ". A região e o bônus só mudam após o aceite da IA.";
        NotificarEstadoAlterado();
        return true;
    }

    private bool TentarObterTerritoriosDaOperacao(SistemaGovernoMundial governo, out List<string> territorios, out string mensagem)
    {
        territorios = new List<string>(5);
        mensagem = string.Empty;
        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        int teamJogador = ResolverTeamJogador(governo);
        if (gerente == null || gerente.MapaPolitico == null)
        {
            mensagem = "O mapa político não está disponível para validar a transferência.";
            return false;
        }

        HashSet<string> idsUnicos = new HashSet<string>(StringComparer.Ordinal);
        foreach (string idZona in IdentificadoresDeChegada())
        {
            ZonaMissaoPacificacao zona = ObterZona(idZona);
            string territorioId = zona != null ? zona.IdentificadorTerritorial : string.Empty;
            ResultadoConsultaTerritorio estado = !string.IsNullOrWhiteSpace(territorioId)
                ? gerente.ObterEstadoDaRegiao(territorioId)
                : ResultadoConsultaTerritorio.NaoDefinido;
            if (string.IsNullOrWhiteSpace(territorioId) || !idsUnicos.Add(territorioId)
                || !estado.encontrouRegiao || estado.neutral || estado.ownerCountryTeamId != teamJogador)
            {
                mensagem = "As cinco áreas de chegada precisam apontar para regiões terrestres distintas sob soberania do jogador.";
                territorios.Clear();
                return false;
            }
            territorios.Add(territorioId);
        }

        return territorios.Count > 0;
    }

    private void VerificarRespostaTransferenciaPendente()
    {
        if (progressoTerritorial != ProgressoTerritorialPacificacao.TransferindoControle
            || string.IsNullOrWhiteSpace(propostaTransferenciaId)) return;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        PropostaInternacional proposta = governo != null && governo.Propostas != null
            ? governo.Propostas.FirstOrDefault(p => p != null && p.id == propostaTransferenciaId)
            : null;
        if (proposta == null) return;

        if (proposta.status == StatusPropostaInternacional.Executada)
        {
            decisaoFinal = DecisaoFinalPacificacao.TransferenciaAliada;
            if (PagarBonusFinal(bonusTransferencia, "Transferência a país aliado"))
            {
                progressoTerritorial = ProgressoTerritorialPacificacao.Concluida;
                propostaTransferenciaId = string.Empty;
                governo.RegistrarNoticia("Operação Pacificação: transferência regional aceita e concluída por " + governo.NomePais(paisAliadoSelecionadoTeamId) + ".");
            }
            else
            {
                progressoTerritorial = ProgressoTerritorialPacificacao.AguardandoDecisaoFinal;
                decisaoFinal = DecisaoFinalPacificacao.Nenhuma;
            }
            NotificarEstadoAlterado();
            return;
        }

        if (proposta.status == StatusPropostaInternacional.Recusada
            || proposta.status == StatusPropostaInternacional.Expirada)
        {
            progressoTerritorial = ProgressoTerritorialPacificacao.AguardandoDecisaoFinal;
            paisAliadoSelecionadoTeamId = 0;
            propostaTransferenciaId = string.Empty;
            governo.RegistrarNoticia("A transferência da Operação Pacificação foi recusada ou expirou; nenhuma região foi transferida e nenhum bônus adicional foi pago.");
            NotificarEstadoAlterado();
        }
    }

    public bool OrdenarRetiradaFinal(out string mensagem)
    {
        mensagem = string.Empty;
        if (progressoTerritorial != ProgressoTerritorialPacificacao.AguardandoDecisaoFinal)
        {
            mensagem = "A decisão final da missão não está pendente.";
            return false;
        }

        decisaoFinal = DecisaoFinalPacificacao.RetiradaCompleta;
        progressoTerritorial = ProgressoTerritorialPacificacao.RetiradaFinal;
        AtualizarPresencaLocal();
        if (!haPresencaNaOperacao)
            FinalizarRetirada();
        else
        {
            SistemaGovernoMundial.Instancia?.RegistrarNoticia("Retirada final da Operação Pacificação ordenada. Mova as forças para fora da área; o bônus será pago após a evacuação completa.");
            mensagem = "Retirada final ordenada. Mova todas as unidades para fora da região; elas não serão removidas automaticamente.";
        }
        NotificarEstadoAlterado();
        if (string.IsNullOrEmpty(mensagem)) mensagem = "Retirada final concluída e bônus registrado.";
        return true;
    }

    private bool PagarBonusFinal(long valor, string categoria)
    {
        if (recompensaAdicionalPaga || valor <= 0L) return recompensaAdicionalPaga;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        int team = ResolverTeamJogador(governo);
        if (governo == null || governo.ObterPais(team) == null) return false;
        governo.AdicionarSaldo(team, valor,
            "Bônus final — " + categoria, "Operação Pacificação", TipoLancamentoFinanceiro.ReceitaTransacao);
        recompensaAdicionalPaga = true;
        return true;
    }

    private void NotificarEstadoAlterado()
    {
        EstadoAlterado?.Invoke();
    }

    private static int SaturarInteiro(int valor, int incremento)
    {
        return (int)Math.Min(int.MaxValue, (long)Mathf.Max(0, valor) + Mathf.Max(0, incremento));
    }

    private static long SomarSaturado(long valor, long incremento)
    {
        if (incremento <= 0L) return valor;
        return valor > long.MaxValue - incremento ? long.MaxValue : valor + incremento;
    }

    public string CriarRelatorio()
    {
        float diasPermanencia = calendarioInscrito != null
            ? tempoPermanenciaParcialSegundos / Mathf.Max(1f, calendarioInscrito.duracaoDiaSegundos)
            : 0f;
        return "COMANDO DE OPERAÇÕES — PACIFICAÇÃO\n"
            + "Estado: " + Estado.ToString().ToUpperInvariant() + "\n"
            + "Investimento: " + estadoInvestimento.ToString().ToUpperInvariant() + "\n"
            + "Tempo de operação: " + TimeSpan.FromSeconds(Mathf.Max(0f, tempoOperacaoSegundos)).ToString(@"dd\d\ hh\h\ mm\m") + "\n"
            + "Investimento acumulado: AT$ " + investimentoAcumulado.ToString("N0") + "\n"
            + "Baixas inimigas: " + baixasInimigas.ToString("N0") + "\n"
            + "Perdas nacionais: " + perdasMilitaresNacionais.ToString("N0") + "\n"
            + "Veículos e aeronaves perdidos: " + veiculosEAeronavesPerdidos.ToString("N0") + "\n"
            + "Objetivos territoriais: " + objetivosRegistrados.Count.ToString("N0") + "/" + TotalObjetivosTerritoriais.ToString("N0") + "\n"
            + "Resistência restante (inclui grupos simulados): " + ResistenciaInimigaRestante.ToString("N0") + "\n"
            + "Reforços nacionais: " + (ReforcosPropriosDaMissaoPermitidos ? "AUTORIZADOS" : "SUSPENSOS") + "\n"
            + "Reforços inimigos: " + (reforcosInimigosBloqueados ? "BLOQUEADOS" : "ATIVOS") + "\n"
            + "Presença militar na região: " + (haPresencaNaOperacao ? "CONFIRMADA" : "NÃO CONFIRMADA") + "\n"
            + "Permanência acumulada: " + diasPermanencia.ToString("0.00") + " dia(s) | AT$ " + custoPermanenciaAcumulado.ToString("N0") + " pago(s)";
    }

    public SaveOperacaoPacificacaoData CapturarEstadoSalvavel()
    {
        return new SaveOperacaoPacificacaoData
        {
            estado = (int)Estado,
            progressoTerritorial = (int)progressoTerritorial,
            estadoInvestimento = (int)estadoInvestimento,
            decisaoFinal = (int)decisaoFinal,
            tempoOperacaoSegundos = tempoOperacaoSegundos,
            proximaAvaliacaoSegundos = proximaAvaliacaoSegundos,
            tempoPermanenciaParcialSegundos = tempoPermanenciaParcialSegundos,
            investimentoAcumulado = investimentoAcumulado,
            custoPermanenciaAcumulado = custoPermanenciaAcumulado,
            baixasInimigas = baixasInimigas,
            perdasMilitaresNacionais = perdasMilitaresNacionais,
            veiculosEAeronavesPerdidos = veiculosEAeronavesPerdidos,
            unidadesGuarnicao = unidadesGuarnicao,
            reforcosInimigosBloqueados = reforcosInimigosBloqueados,
            recompensaPrincipalPaga = recompensaPrincipalPaga,
            recompensaAdicionalPaga = recompensaAdicionalPaga,
            avaliacaoPendente = avaliacaoPendente,
            proprietarioBasePermanenteTeamId = proprietarioBasePermanenteTeamId,
            paisAliadoSelecionadoTeamId = paisAliadoSelecionadoTeamId,
            propostaTransferenciaId = propostaTransferenciaId,
            objetivosTerritoriaisRegistrados = objetivosRegistrados.ToList(),
            unidadesInimigasRegistradas = unidadesInimigasRegistradas.ToList(),
            unidadesPropriasVinculadas = unidadesPropriasVinculadas.ToList(),
            baixasProcessadas = baixasProcessadas.ToList(),
            gruposDeResistencia = gruposDeResistencia.Select(par => new SaveGrupoResistenciaPacificacao
            {
                identificador = par.Key,
                unidadesRestantes = par.Value
            }).ToList()
        };
    }

    public void RestaurarEstadoSalvo(SaveOperacaoPacificacaoData salvo)
    {
        if (salvo == null)
        {
            ResetarParaNovaPartida();
            return;
        }

        progressoTerritorial = Enum.IsDefined(typeof(ProgressoTerritorialPacificacao), salvo.progressoTerritorial)
            ? (ProgressoTerritorialPacificacao)salvo.progressoTerritorial
            : (ProgressoTerritorialPacificacao)MapearEstadoSalvo(salvo.estado);
        estadoInvestimento = Enum.IsDefined(typeof(EstadoInvestimentoPacificacao), salvo.estadoInvestimento)
            ? (EstadoInvestimentoPacificacao)salvo.estadoInvestimento
            : EstadoInvestimentoPacificacao.Ativo;
        decisaoFinal = Enum.IsDefined(typeof(DecisaoFinalPacificacao), salvo.decisaoFinal)
            ? (DecisaoFinalPacificacao)salvo.decisaoFinal
            : DecisaoFinalPacificacao.Nenhuma;
        tempoOperacaoSegundos = Mathf.Max(0f, salvo.tempoOperacaoSegundos);
        proximaAvaliacaoSegundos = Mathf.Max(0f, salvo.proximaAvaliacaoSegundos);
        tempoPermanenciaParcialSegundos = Mathf.Max(0f, salvo.tempoPermanenciaParcialSegundos);
        investimentoAcumulado = Math.Max(0L, salvo.investimentoAcumulado);
        custoPermanenciaAcumulado = Math.Max(0L, salvo.custoPermanenciaAcumulado);
        baixasInimigas = Mathf.Max(0, salvo.baixasInimigas);
        perdasMilitaresNacionais = Mathf.Max(0, salvo.perdasMilitaresNacionais);
        veiculosEAeronavesPerdidos = Mathf.Max(0, salvo.veiculosEAeronavesPerdidos);
        unidadesGuarnicao = Mathf.Max(0, salvo.unidadesGuarnicao);
        reforcosInimigosBloqueados = salvo.reforcosInimigosBloqueados;
        recompensaPrincipalPaga = salvo.recompensaPrincipalPaga;
        recompensaAdicionalPaga = salvo.recompensaAdicionalPaga;
        avaliacaoPendente = salvo.avaliacaoPendente;
        proprietarioBasePermanenteTeamId = Mathf.Max(0, salvo.proprietarioBasePermanenteTeamId);
        paisAliadoSelecionadoTeamId = Mathf.Max(0, salvo.paisAliadoSelecionadoTeamId);
        propostaTransferenciaId = salvo.propostaTransferenciaId ?? string.Empty;
        RestaurarConjunto(objetivosRegistrados, salvo.objetivosTerritoriaisRegistrados);
        RestaurarConjunto(unidadesInimigasRegistradas, salvo.unidadesInimigasRegistradas);
        RestaurarConjunto(unidadesPropriasVinculadas, salvo.unidadesPropriasVinculadas);
        RestaurarConjunto(baixasProcessadas, salvo.baixasProcessadas);
        gruposDeResistencia.Clear();
        if (salvo.gruposDeResistencia != null)
            foreach (SaveGrupoResistenciaPacificacao grupo in salvo.gruposDeResistencia)
                if (grupo != null && !string.IsNullOrWhiteSpace(grupo.identificador) && grupo.unidadesRestantes > 0)
                    gruposDeResistencia[grupo.identificador] = grupo.unidadesRestantes;

        ultimoTempoSimulacao = relogioInscrito != null ? relogioInscrito.SimulationTime : -1f;
        AtualizarPresencaLocal();
        NotificarEstadoAlterado();
    }

    private static void RestaurarConjunto(HashSet<string> destino, List<string> origem)
    {
        destino.Clear();
        if (origem == null) return;
        for (int i = 0; i < origem.Count; i++)
            if (!string.IsNullOrWhiteSpace(origem[i])) destino.Add(origem[i]);
    }

    private static int MapearEstadoSalvo(int estado)
    {
        EstadoMissaoPacificacao salvo = Enum.IsDefined(typeof(EstadoMissaoPacificacao), estado)
            ? (EstadoMissaoPacificacao)estado : EstadoMissaoPacificacao.Disponivel;
        switch (salvo)
        {
            case EstadoMissaoPacificacao.AguardandoChegada: return (int)ProgressoTerritorialPacificacao.AguardandoChegada;
            case EstadoMissaoPacificacao.OperacaoAtiva: return (int)ProgressoTerritorialPacificacao.OperacaoAtiva;
            case EstadoMissaoPacificacao.RetiradaEmAndamento: return (int)ProgressoTerritorialPacificacao.RetiradaEmAndamento;
            case EstadoMissaoPacificacao.ObjetivosTerritoriaisConcluidos: return (int)ProgressoTerritorialPacificacao.ObjetivosTerritoriaisConcluidos;
            case EstadoMissaoPacificacao.ResistenciaRemanescente: return (int)ProgressoTerritorialPacificacao.ResistenciaRemanescente;
            case EstadoMissaoPacificacao.Vitoria:
            case EstadoMissaoPacificacao.AguardandoDecisaoFinal: return (int)ProgressoTerritorialPacificacao.AguardandoDecisaoFinal;
            case EstadoMissaoPacificacao.EstabelecendoBase: return (int)ProgressoTerritorialPacificacao.EstabelecendoBase;
            case EstadoMissaoPacificacao.RetiradaFinal: return (int)ProgressoTerritorialPacificacao.RetiradaFinal;
            case EstadoMissaoPacificacao.TransferindoControle: return (int)ProgressoTerritorialPacificacao.TransferindoControle;
            case EstadoMissaoPacificacao.Concluida: return (int)ProgressoTerritorialPacificacao.Concluida;
            case EstadoMissaoPacificacao.Abandonada: return (int)ProgressoTerritorialPacificacao.Abandonada;
            default: return (int)ProgressoTerritorialPacificacao.Disponivel;
        }
    }

    public void ResetarParaNovaPartida()
    {
        progressoTerritorial = ProgressoTerritorialPacificacao.Disponivel;
        estadoInvestimento = EstadoInvestimentoPacificacao.Ativo;
        decisaoFinal = DecisaoFinalPacificacao.Nenhuma;
        tempoOperacaoSegundos = 0f;
        proximaAvaliacaoSegundos = 0f;
        tempoPermanenciaParcialSegundos = 0f;
        investimentoAcumulado = 0L;
        custoPermanenciaAcumulado = 0L;
        baixasInimigas = 0;
        perdasMilitaresNacionais = 0;
        veiculosEAeronavesPerdidos = 0;
        unidadesGuarnicao = 0;
        proprietarioBasePermanenteTeamId = 0;
        paisAliadoSelecionadoTeamId = 0;
        propostaTransferenciaId = string.Empty;
        reforcosInimigosBloqueados = false;
        recompensaPrincipalPaga = false;
        recompensaAdicionalPaga = false;
        avaliacaoPendente = false;
        haPresencaNaOperacao = false;
        haPresencaNaAreaPermanencia = false;
        haPresencaNaGuarnicao = false;
        teamJogadorOperacao = 0;
        objetivosRegistrados.Clear();
        unidadesInimigasRegistradas.Clear();
        unidadesPropriasVinculadas.Clear();
        baixasProcessadas.Clear();
        gruposDeResistencia.Clear();
        proximaConsultaPresencaSegundos = relogioInscrito != null ? relogioInscrito.SimulationTime : 0f;
        NotificarEstadoAlterado();
    }
}
