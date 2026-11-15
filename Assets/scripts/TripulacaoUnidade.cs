using System;
using UnityEngine;

/// <summary>
/// Controla a tripulacao nominal e ativa de unidades militares de todas as nacoes
/// (Jogador, IAs e Cartel) sem alterar IdentidadeUnidade.militaresConsumidos,
/// preservando a regra de mobilizacao nacional.
/// </summary>
[DisallowMultipleComponent]
public class TripulacaoUnidade : MonoBehaviour
{
    public enum CategoriaTripulacao
    {
        Nenhuma = 0,
        Infantaria = 1,
        VeiculoTerrestre = 2,
        Aeronave = 3,
        NavioPequeno = 4,
        NavioMedio = 5,
        NavioTransporteTropas = 6,
        PortaAvioes = 7
    }

    private struct RegraNomeTripulacao
    {
        public readonly string fragmento;
        public readonly CategoriaTripulacao categoria;
        public readonly int tripulacao;

        public RegraNomeTripulacao(string fragmento, CategoriaTripulacao categoria, int tripulacao)
        {
            this.fragmento = fragmento;
            this.categoria = categoria;
            this.tripulacao = tripulacao;
        }
    }

    private static readonly RegraNomeTripulacao[] RegrasNavais =
    {
        // Porta-avioes (ate 6.000 pessoas)
        new RegraNomeTripulacao("enterprise", CategoriaTripulacao.PortaAvioes, 6000),
        new RegraNomeTripulacao("sovereign", CategoriaTripulacao.PortaAvioes, 6000),
        new RegraNomeTripulacao("nv_global", CategoriaTripulacao.PortaAvioes, 5200),
        new RegraNomeTripulacao("porta avioes", CategoriaTripulacao.PortaAvioes, 5500),
        new RegraNomeTripulacao("porta-avioes", CategoriaTripulacao.PortaAvioes, 5500),
        new RegraNomeTripulacao("portaavioes", CategoriaTripulacao.PortaAvioes, 5500),
        new RegraNomeTripulacao("carrier", CategoriaTripulacao.PortaAvioes, 5500),

        // Navios de transporte de tropas (2.000 a 3.000 pessoas)
        new RegraNomeTripulacao("uss_america", CategoriaTripulacao.NavioTransporteTropas, 3000),
        new RegraNomeTripulacao("america", CategoriaTripulacao.NavioTransporteTropas, 3000),
        new RegraNomeTripulacao("yuzhao", CategoriaTripulacao.NavioTransporteTropas, 2800),
        new RegraNomeTripulacao("kunlun", CategoriaTripulacao.NavioTransporteTropas, 2800),
        new RegraNomeTripulacao("nav fuku", CategoriaTripulacao.NavioTransporteTropas, 2600),
        new RegraNomeTripulacao("ministral", CategoriaTripulacao.NavioTransporteTropas, 2200),
        new RegraNomeTripulacao("mistral", CategoriaTripulacao.NavioTransporteTropas, 2200),
        new RegraNomeTripulacao("nav_tropas", CategoriaTripulacao.NavioTransporteTropas, 2000),
        new RegraNomeTripulacao("nav_tropa", CategoriaTripulacao.NavioTransporteTropas, 2000),
        new RegraNomeTripulacao("liberty", CategoriaTripulacao.NavioTransporteTropas, 2200),
        new RegraNomeTripulacao("lha", CategoriaTripulacao.NavioTransporteTropas, 2600),

        // Navios medios / destroyers / fragatas / submarinos estrategicos (360 a 400 pessoas)
        new RegraNomeTripulacao("vindicator", CategoriaTripulacao.NavioMedio, 400),
        new RegraNomeTripulacao("des_wall", CategoriaTripulacao.NavioMedio, 400),
        new RegraNomeTripulacao("wall", CategoriaTripulacao.NavioMedio, 390),
        new RegraNomeTripulacao("destroyer", CategoriaTripulacao.NavioMedio, 380),
        new RegraNomeTripulacao("iron", CategoriaTripulacao.NavioMedio, 380),
        new RegraNomeTripulacao("nagoya", CategoriaTripulacao.NavioMedio, 380),
        new RegraNomeTripulacao("oliver", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("perry", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("ffg", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("fragata", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("f200", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("f201", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("leviathan", CategoriaTripulacao.NavioMedio, 360),
        new RegraNomeTripulacao("delta_iv", CategoriaTripulacao.NavioMedio, 360),

        // Navios normais / escoltas / corvetas / apoio (80 a 100 pessoas)
        new RegraNomeTripulacao("abastecimento", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("petroleiro", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("fortaleza", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("corveta", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("coverta", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("icaris", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("liberal", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("michele", CategoriaTripulacao.NavioPequeno, 100),
        new RegraNomeTripulacao("harry", CategoriaTripulacao.NavioPequeno, 90),
        new RegraNomeTripulacao("costeira", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("vigia", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("think", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("arrasto", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("hover", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("barco", CategoriaTripulacao.NavioPequeno, 80),
        new RegraNomeTripulacao("balsa", CategoriaTripulacao.NavioPequeno, 80)
    };

    private static readonly RegraNomeTripulacao[] RegrasAereas =
    {
        new RegraNomeTripulacao("b260", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("b01", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("c17", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("c700", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("c400", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("ac130", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("boeing e3", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("e-3", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("awacs", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("flow", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("mi-80", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("mi_he", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("sh60", CategoriaTripulacao.Aeronave, 2),
        new RegraNomeTripulacao("osprey", CategoriaTripulacao.Aeronave, 2)
    };

    private static readonly RegraNomeTripulacao[] RegrasTerrestres =
    {
        new RegraNomeTripulacao("tank_king", CategoriaTripulacao.VeiculoTerrestre, 6),
        new RegraNomeTripulacao("tank_arthur", CategoriaTripulacao.VeiculoTerrestre, 6),
        new RegraNomeTripulacao("msta", CategoriaTripulacao.VeiculoTerrestre, 6),
        new RegraNomeTripulacao("artillery", CategoriaTripulacao.VeiculoTerrestre, 6),
        new RegraNomeTripulacao("leon_c1", CategoriaTripulacao.VeiculoTerrestre, 5),
        new RegraNomeTripulacao("tank_south", CategoriaTripulacao.VeiculoTerrestre, 5),
        new RegraNomeTripulacao("ubu", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("tank_c1", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("tank_mu", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("tank_unity", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("tank_a", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("antigravity", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("front_shield", CategoriaTripulacao.VeiculoTerrestre, 5),
        new RegraNomeTripulacao("tank", CategoriaTripulacao.VeiculoTerrestre, 4),
        new RegraNomeTripulacao("hamer", CategoriaTripulacao.VeiculoTerrestre, 3),
        new RegraNomeTripulacao("hack", CategoriaTripulacao.VeiculoTerrestre, 3),
        new RegraNomeTripulacao("caoc", CategoriaTripulacao.VeiculoTerrestre, 3),
        new RegraNomeTripulacao("track", CategoriaTripulacao.VeiculoTerrestre, 2),
        new RegraNomeTripulacao("abastecimento", CategoriaTripulacao.VeiculoTerrestre, 2),
        new RegraNomeTripulacao("van", CategoriaTripulacao.VeiculoTerrestre, 2)
    };

    [Header("Tripulacao")]
    public bool usaTripulacao = true;
    public CategoriaTripulacao categoria = CategoriaTripulacao.Nenhuma;
    [Min(0)] public int tripulacaoMaxima = 1;
    [Min(0)] public int tripulacaoAtual = 1;

    [Header("Baixas por Fome e Reposicao")]
    [Min(0f)] public float custoIndenizacaoPorBaixaMorte = 250000f;

    private bool configurado;
    private bool abateJaContabilizado;
    private bool estadoInoperanteAplicado;
    private bool avisoInoperanteEmitido;
    private float acumuladorBaixasFome;

    public bool UsaTripulacao => usaTripulacao && tripulacaoMaxima > 0;
    public int TripulacaoMaxima => usaTripulacao ? Mathf.Max(0, tripulacaoMaxima) : 0;
    public int TripulacaoAtual => usaTripulacao ? Mathf.Clamp(tripulacaoAtual, 0, TripulacaoMaxima) : 0;
    public bool EstaSemTripulacao => UsaTripulacao && TripulacaoAtual <= 0;
    public bool EstaOperacional => !UsaTripulacao || TripulacaoAtual > 0;
    public bool AbateJaContabilizado => abateJaContabilizado;

    private void Awake()
    {
        ConfigurarSeNecessario();
    }

    private void Start()
    {
        ConfigurarSeNecessario();
        SincronizarEstadoOperacional();
    }

    public void ConfigurarSeNecessario()
    {
        if (configurado)
        {
            return;
        }

        IdentidadeUnidade identidade = ObterIdentidade(gameObject);
        if (!DeveUsarTripulacao(gameObject, identidade))
        {
            usaTripulacao = false;
            categoria = CategoriaTripulacao.Nenhuma;
            tripulacaoMaxima = 0;
            tripulacaoAtual = 0;
            configurado = true;
            return;
        }

        ResolverCategoriaETripulacao(gameObject, identidade, out CategoriaTripulacao catResolvida, out int qtdResolvida);
        usaTripulacao = qtdResolvida > 0;
        categoria = catResolvida;
        tripulacaoMaxima = Mathf.Max(0, qtdResolvida);
        tripulacaoAtual = tripulacaoMaxima;
        configurado = true;
    }

    public void RestaurarEstadoPersistido(int tripulacaoMaximaSalva, int tripulacaoAtualSalva)
    {
        ConfigurarSeNecessario();
        if (!UsaTripulacao && tripulacaoMaximaSalva <= 0)
        {
            return;
        }

        if (tripulacaoMaximaSalva > 0)
        {
            usaTripulacao = true;
            tripulacaoMaxima = Mathf.Max(1, tripulacaoMaximaSalva);
        }

        tripulacaoAtual = tripulacaoAtualSalva >= 0
            ? Mathf.Clamp(tripulacaoAtualSalva, 0, tripulacaoMaxima)
            : tripulacaoMaxima;
        configurado = true;
        SincronizarEstadoOperacional();
    }

    public static bool UnidadeEstaOperacional(GameObject alvo)
    {
        if (alvo == null)
        {
            return true;
        }

        TripulacaoUnidade trip = ObterParaObjeto(alvo);
        return trip == null || trip.EstaOperacional;
    }

    /// <summary>
    /// Aplica baixas graduais de tripulacao por falta de racao sem destruir ou
    /// remover a estrutura fisica da unidade do mundo. Ao zerar a tripulacao,
    /// a unidade entra em modo inoperante ate receber resgate/reposicao.
    /// </summary>
    public int AplicarBaixaPorFome(float dt, float percentualPorSegundo = 0.04f)
    {
        ConfigurarSeNecessario();
        if (!UsaTripulacao || TripulacaoAtual <= 0 || dt <= 0f)
        {
            if (EstaSemTripulacao)
            {
                SincronizarEstadoOperacional();
            }
            return 0;
        }

        float taxaPessoasPorSegundo = Mathf.Max(0.25f, TripulacaoMaxima * Mathf.Max(0.005f, percentualPorSegundo));
        acumuladorBaixasFome += taxaPessoasPorSegundo * dt;

        int baixas = Mathf.FloorToInt(acumuladorBaixasFome);
        if (baixas <= 0)
        {
            return 0;
        }

        acumuladorBaixasFome -= baixas;
        int efetivas = Mathf.Min(baixas, TripulacaoAtual);
        if (efetivas <= 0)
        {
            return 0;
        }

        tripulacaoAtual = Mathf.Max(0, TripulacaoAtual - efetivas);
        RegistrarBaixaFinanceiraGoverno(efetivas);

        if (TripulacaoAtual <= 0)
        {
            SincronizarEstadoOperacional();
        }

        return efetivas;
    }

    public int TentarReporTripulacao(int quantidadeMaxima = -1, bool emitirAviso = true)
    {
        ConfigurarSeNecessario();
        if (!UsaTripulacao)
        {
            return 0;
        }

        int faltam = TripulacaoMaxima - TripulacaoAtual;
        if (faltam <= 0)
        {
            return 0;
        }

        int desejado = quantidadeMaxima > 0 ? Mathf.Min(faltam, quantidadeMaxima) : faltam;
        if (desejado <= 0)
        {
            return 0;
        }

        int repostos = MobilizarTripulacaoNacional(desejado);
        if (repostos <= 0)
        {
            return 0;
        }

        bool estavaSemTripulacao = TripulacaoAtual <= 0;
        tripulacaoAtual = Mathf.Clamp(TripulacaoAtual + repostos, 0, TripulacaoMaxima);
        acumuladorBaixasFome = 0f;
        abateJaContabilizado = false;

        if (estavaSemTripulacao && TripulacaoAtual > 0)
        {
            avisoInoperanteEmitido = false;
            SincronizarEstadoOperacional();
        }

        if (emitirAviso && TripulacaoAtual >= TripulacaoMaxima && EhEquipeJogadorLocal())
        {
            HUDAjudaRTS.MostrarMensagemTemporaria(
                $"{name.Replace("(Clone)", string.Empty).Trim()}: nova tripulação embarcada ({TripulacaoAtual}/{TripulacaoMaxima}). Unidade reoperacionalizada!",
                4.5f);
        }

        return repostos;
    }

    public void SincronizarEstadoOperacional()
    {
        bool inoperante = EstaSemTripulacao;
        if (inoperante == estadoInoperanteAplicado)
        {
            return;
        }

        estadoInoperanteAplicado = inoperante;

        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle != null)
        {
            if (inoperante)
            {
                controle.EmitirOrdemParar();
            }
            controle.DefinirDesligado(inoperante);
        }

        ControleNavioRealista navio = GetComponent<ControleNavioRealista>();
        if (navio != null)
        {
            navio.DefinirDesligado(inoperante);
        }

        ControleSubmarino submarino = GetComponent<ControleSubmarino>();
        if (submarino != null && inoperante)
        {
            submarino.DefinirModoOperacao(ControleSubmarino.ModoOperacao.Passivo, false);
        }

        UnityEngine.AI.NavMeshAgent agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null && agente.isOnNavMesh)
        {
            if (inoperante)
            {
                agente.ResetPath();
            }
            agente.isStopped = inoperante;
        }

        // Mantem SistemaDeDanos ativo: a estrutura fisica continua no mundo
        // como alvo facil para o inimigo ate ser resgatada e reabastecida.
        if (inoperante && !avisoInoperanteEmitido && EhEquipeJogadorLocal())
        {
            avisoInoperanteEmitido = true;
            HUDAjudaRTS.MostrarMensagemTemporaria(
                $"{name.Replace("(Clone)", string.Empty).Trim()} ficou INOPERANTE (sem tripulação)! Envie helicóptero, navio de abastecimento ou atracar no píer para resgatar.",
                6f);
        }
    }

    private void RegistrarBaixaFinanceiraGoverno(int baixas)
    {
        if (baixas <= 0)
        {
            return;
        }

        IdentidadeUnidade id = ObterIdentidade(gameObject);
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        if (gov == null || id == null || id.teamID <= 0)
        {
            return;
        }

        DadosPaisGoverno pais = gov.ObterPais(id.teamID);
        if (pais == null)
        {
            return;
        }

        pais.mortosAcumulados += baixas;
        pais.pensionistasMilitares += baixas;
        pais.populacaoMilitarAtiva = Mathf.Max(0, pais.populacaoMilitarAtiva - baixas);
        pais.populacao = Mathf.Max(pais.populacaoCivil, pais.populacao - baixas);

        long indenizacao = (long)baixas * (long)Mathf.Max(0f, custoIndenizacaoPorBaixaMorte);
        if (indenizacao <= 0L)
        {
            return;
        }

        pais.gastosIndenizacoesMilitares += indenizacao;
        if (id.teamID == gov.teamJogador && GerenciadorRecursos.Instancia != null)
        {
            long disponivel = Math.Max(0L, GerenciadorRecursos.Instancia.dinheiro);
            long cobrar = Math.Min(disponivel, indenizacao);
            if (cobrar > 0L)
            {
                GerenciadorRecursos.Instancia.TentarGastar(custoDinheiro: cobrar);
            }
            long faltante = indenizacao - cobrar;
            if (faltante > 0L)
            {
                pais.divida += faltante;
            }
            pais.saldo = GerenciadorRecursos.Instancia.dinheiro;
        }
        else
        {
            pais.saldo -= indenizacao;
        }
    }

    private int MobilizarTripulacaoNacional(int desejado)
    {
        IdentidadeUnidade id = ObterIdentidade(gameObject);
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        if (gov == null || id == null || id.teamID <= 0)
        {
            return desejado;
        }

        int disponivel = gov.ObterEfetivoDisponivel(id.teamID);
        if (disponivel <= 0)
        {
            return 0;
        }

        int transferir = Mathf.Min(desejado, disponivel);
        return gov.TentarMobilizarEfetivo(id.teamID, transferir) ? transferir : 0;
    }

    private bool EhEquipeJogadorLocal()
    {
        IdentidadeUnidade id = ObterIdentidade(gameObject);
        return id == null || id.teamID == 0 || id.teamID == RacaoUnidade.ObterTeamJogadorAtual();
    }

    public int ConsumirParaRegistroAbate()
    {
        ConfigurarSeNecessario();
        if (abateJaContabilizado || !UsaTripulacao)
        {
            return 0;
        }

        abateJaContabilizado = true;
        int totalAbatido = Mathf.Max(tripulacaoAtual, tripulacaoMaxima);
        tripulacaoAtual = 0;
        return totalAbatido;
    }

    public string TextoStatusCurto()
    {
        ConfigurarSeNecessario();
        if (!UsaTripulacao)
        {
            return string.Empty;
        }

        return EstaSemTripulacao
            ? $"SEM TRIPULAÇÃO (0/{TripulacaoMaxima:N0})"
            : $"TRIP {TripulacaoAtual:N0}/{TripulacaoMaxima:N0}";
    }

    public static TripulacaoUnidade Garantir(GameObject alvo)
    {
        if (alvo == null)
        {
            return null;
        }

        IdentidadeUnidade identidade = ObterIdentidade(alvo);
        if (!DeveUsarTripulacao(alvo, identidade))
        {
            return null;
        }

        TripulacaoUnidade comp = alvo.GetComponent<TripulacaoUnidade>();
        if (comp == null)
        {
            comp = alvo.AddComponent<TripulacaoUnidade>();
        }

        comp.ConfigurarSeNecessario();
        return comp;
    }

    public static TripulacaoUnidade ObterParaObjeto(GameObject alvo)
    {
        if (alvo == null)
        {
            return null;
        }

        TripulacaoUnidade comp = alvo.GetComponent<TripulacaoUnidade>();
        if (comp == null) comp = alvo.GetComponentInParent<TripulacaoUnidade>(true);
        if (comp == null) comp = alvo.GetComponentInChildren<TripulacaoUnidade>(true);
        return comp;
    }

    public static int ResolverTripulacaoNominal(GameObject alvo)
    {
        if (alvo == null)
        {
            return 0;
        }

        TripulacaoUnidade comp = ObterParaObjeto(alvo);
        if (comp != null)
        {
            comp.ConfigurarSeNecessario();
            return comp.TripulacaoMaxima;
        }

        IdentidadeUnidade identidade = ObterIdentidade(alvo);
        if (!DeveUsarTripulacao(alvo, identidade))
        {
            return 0;
        }

        ResolverCategoriaETripulacao(alvo, identidade, out _, out int quantidade);
        return quantidade;
    }

    public static int ResolverTripulacaoParaAbate(GameObject alvo)
    {
        if (alvo == null)
        {
            return 0;
        }

        TripulacaoUnidade comp = ObterParaObjeto(alvo);
        if (comp != null)
        {
            return comp.ConsumirParaRegistroAbate();
        }

        return ResolverTripulacaoNominal(alvo);
    }

    public static bool EhDroneNaoTripulado(GameObject alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        if (alvo.GetComponent<KamikazeDrone>() != null
            || alvo.GetComponentInChildren<KamikazeDrone>(true) != null
            || alvo.GetComponent<ControleDroneHasaf>() != null
            || alvo.GetComponentInChildren<ControleDroneHasaf>(true) != null)
        {
            return true;
        }

        string nome = ObterNomeNormalizado(alvo);
        return nome.Contains("drone")
            || nome.Contains("hasaf")
            || nome.Contains("uav")
            || nome.Contains("vap");
    }

    public static bool EhEstruturaImovel(GameObject alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        if (string.Equals(alvo.tag, "Imovel", StringComparison.Ordinal))
        {
            return true;
        }

        IdentidadeUnidade idDireta = alvo.GetComponent<IdentidadeUnidade>();
        if (idDireta != null && idDireta.tipoUnidade == TipoUnidade.Estrutura)
        {
            return true;
        }

        SistemaDeDanos danosDireto = alvo.GetComponent<SistemaDeDanos>();
        if (danosDireto != null && danosDireto.ehEstrutura)
        {
            return true;
        }

        // Garante que unidades moveis (incluindo porta-avioes que herdam GerenciadorAeroporto)
        // nunca sejam confundidas com estruturas fixas.
        bool ehPortaAvioes = alvo.GetComponent<GerenciadorPortaAvioes>() != null;
        if (!ehPortaAvioes && (alvo.GetComponent<Estaleiro>() != null
            || alvo.GetComponent<PierMarinha>() != null
            || alvo.GetComponent<Fabrica>() != null
            || alvo.GetComponent<GerenciadorAeroporto>() != null
            || alvo.GetComponent<GerenciadorAeroportoComercial>() != null
            || alvo.GetComponent<ComplexoGovernamental>() != null
            || alvo.GetComponent<Imovel>() != null
            || alvo.GetComponent<EstruturaEconomica>() != null
            || alvo.GetComponent<MarcadorTerritorio>() != null
            || alvo.GetComponent<CidadeComplexoUrbano>() != null
            || alvo.GetComponent<Fazenda>() != null
            || alvo.GetComponent<GerenciadorQuartel>() != null))
        {
            return true;
        }

        bool possuiControladorMovel = ehPortaAvioes
            || alvo.GetComponent<ControleUnidade>() != null
            || alvo.GetComponent<ControleNavioRealista>() != null
            || alvo.GetComponent<ControleSubmarino>() != null
            || alvo.GetComponent<HovercraftTransporte>() != null
            || alvo.GetComponent<NavioAbastecimento>() != null
            || alvo.GetComponent<NavioPetroleiro>() != null
            || alvo.GetComponent<NavioTransporteTropas>() != null
            || alvo.GetComponent<ControleAviao>() != null
            || alvo.GetComponent<ControleAviaoCaca>() != null
            || alvo.GetComponent<Helicoptero>() != null
            || alvo.GetComponent<C700TransporteAereo>() != null
            || alvo.GetComponent<CaminhaoTanqueAbastecimento>() != null
            || alvo.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

        if (possuiControladorMovel)
        {
            return false;
        }

        return alvo.GetComponentInParent<Estaleiro>() != null
            || alvo.GetComponentInParent<PierMarinha>() != null
            || alvo.GetComponentInParent<Fabrica>() != null
            || alvo.GetComponentInParent<ComplexoGovernamental>() != null
            || alvo.GetComponentInParent<GerenciadorAeroportoComercial>() != null
            || alvo.GetComponentInParent<MarcadorTerritorio>() != null
            || alvo.GetComponentInParent<CidadeComplexoUrbano>() != null
            || (alvo.GetComponentInParent<GerenciadorAeroporto>() != null
                && alvo.GetComponentInParent<GerenciadorPortaAvioes>() == null);
    }

    public static bool DeveUsarTripulacao(GameObject alvo, IdentidadeUnidade identidade = null)
    {
        if (alvo == null)
        {
            return false;
        }

        if (alvo.GetComponent<ControleAviaoComercial>() != null
            || alvo.GetComponentInParent<ControleAviaoComercial>() != null)
        {
            return false;
        }

        if (EhEstruturaImovel(alvo))
        {
            return false;
        }

        if (EhDroneNaoTripulado(alvo))
        {
            return false;
        }

        if (identidade == null)
        {
            identidade = ObterIdentidade(alvo);
        }

        if (identidade != null && identidade.tipoUnidade == TipoUnidade.Estrutura)
        {
            return false;
        }

        bool temControladorMovel = alvo.GetComponent<ControleUnidade>() != null
            || alvo.GetComponent<ControleAviao>() != null
            || alvo.GetComponent<ControleAviaoCaca>() != null
            || alvo.GetComponent<Helicoptero>() != null
            || alvo.GetComponent<C700TransporteAereo>() != null
            || alvo.GetComponent<ControleNavioRealista>() != null
            || alvo.GetComponent<ControleSubmarino>() != null
            || alvo.GetComponent<HovercraftTransporte>() != null
            || alvo.GetComponent<NavioAbastecimento>() != null
            || alvo.GetComponent<NavioPetroleiro>() != null
            || alvo.GetComponent<NavioTransporteTropas>() != null
            || alvo.GetComponent<GerenciadorPortaAvioes>() != null
            || alvo.GetComponent<CaminhaoTanqueAbastecimento>() != null
            || alvo.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

        if (!temControladorMovel && identidade == null)
        {
            return false;
        }

        return true;
    }

    public static void ResolverCategoriaETripulacao(
        GameObject alvo,
        IdentidadeUnidade identidade,
        out CategoriaTripulacao categoria,
        out int tripulacao)
    {
        categoria = CategoriaTripulacao.Nenhuma;
        tripulacao = 0;

        if (!DeveUsarTripulacao(alvo, identidade))
        {
            return;
        }

        string nome = ObterNomeNormalizado(alvo);

        if (EhUnidadeNaval(alvo, identidade))
        {
            ResolverTripulacaoNaval(alvo, nome, out categoria, out tripulacao);
            return;
        }

        if (EhUnidadeAerea(alvo, identidade))
        {
            categoria = CategoriaTripulacao.Aeronave;
            tripulacao = BuscarPorTabela(nome, RegrasAereas, out _, out int valor)
                ? Mathf.Clamp(valor, 1, 2)
                : 1;
            return;
        }

        if (identidade != null && identidade.tipoUnidade == TipoUnidade.Infantaria)
        {
            categoria = CategoriaTripulacao.Infantaria;
            tripulacao = Mathf.Max(1, identidade.militaresConsumidos);
            return;
        }

        if (EhUnidadeTerrestreBlindadaOuViatura(alvo, identidade, nome))
        {
            categoria = CategoriaTripulacao.VeiculoTerrestre;
            if (BuscarPorTabela(nome, RegrasTerrestres, out _, out int valor))
            {
                tripulacao = Mathf.Clamp(valor, 1, 6);
                return;
            }

            if (alvo.GetComponent<CaminhaoTanqueAbastecimento>() != null)
            {
                tripulacao = 2;
                return;
            }

            if (alvo.GetComponent<LancadorMisseis>() != null
                || alvo.GetComponentInChildren<LancadorMisseis>(true) != null)
            {
                tripulacao = 3;
                return;
            }

            tripulacao = 4;
            return;
        }

        categoria = CategoriaTripulacao.Infantaria;
        tripulacao = identidade != null ? Mathf.Max(1, identidade.militaresConsumidos) : 1;
    }

    private static void ResolverTripulacaoNaval(
        GameObject alvo,
        string nome,
        out CategoriaTripulacao categoria,
        out int tripulacao)
    {
        GerenciadorPortaAvioes carrier = alvo.GetComponent<GerenciadorPortaAvioes>()
            ?? alvo.GetComponentInChildren<GerenciadorPortaAvioes>(true);
        if (carrier != null && !carrier.somenteHelicopteros)
        {
            categoria = CategoriaTripulacao.PortaAvioes;
            tripulacao = BuscarPorTabela(nome, RegrasNavais, out CategoriaTripulacao catNome, out int qtdNome)
                && catNome == CategoriaTripulacao.PortaAvioes
                ? Mathf.Clamp(qtdNome, 3500, 6000)
                : 5500;
            return;
        }

        NavioTransporteTropas transporte = alvo.GetComponent<NavioTransporteTropas>()
            ?? alvo.GetComponentInChildren<NavioTransporteTropas>(true);
        if (transporte != null)
        {
            categoria = CategoriaTripulacao.NavioTransporteTropas;
            tripulacao = BuscarPorTabela(nome, RegrasNavais, out CategoriaTripulacao catNome, out int qtdNome)
                && catNome == CategoriaTripulacao.NavioTransporteTropas
                ? Mathf.Clamp(qtdNome, 2000, 3000)
                : 2400;
            return;
        }

        if (BuscarPorTabela(nome, RegrasNavais, out categoria, out tripulacao))
        {
            return;
        }

        if (alvo.GetComponent<ControleSubmarino>() != null)
        {
            categoria = CategoriaTripulacao.NavioPequeno;
            tripulacao = 100;
            return;
        }

        if (alvo.GetComponent<HovercraftTransporte>() != null)
        {
            categoria = CategoriaTripulacao.NavioPequeno;
            tripulacao = 80;
            return;
        }

        if (alvo.GetComponent<NavioAbastecimento>() != null || alvo.GetComponent<NavioPetroleiro>() != null)
        {
            categoria = CategoriaTripulacao.NavioPequeno;
            tripulacao = 100;
            return;
        }

        LancadorNaval lancadorNaval = alvo.GetComponent<LancadorNaval>()
            ?? alvo.GetComponentInChildren<LancadorNaval>(true);
        if (lancadorNaval != null)
        {
            categoria = CategoriaTripulacao.NavioMedio;
            tripulacao = 360;
            return;
        }

        categoria = CategoriaTripulacao.NavioPequeno;
        tripulacao = 90;
    }

    private static bool EhUnidadeNaval(GameObject alvo, IdentidadeUnidade identidade)
    {
        if (identidade != null && identidade.tipoUnidade == TipoUnidade.Naval)
        {
            return true;
        }

        return alvo.GetComponent<IdentidadeNaval>() != null
            || alvo.GetComponent<ControleNavioRealista>() != null
            || alvo.GetComponent<ControleSubmarino>() != null
            || alvo.GetComponent<HovercraftTransporte>() != null
            || alvo.GetComponent<NavioAbastecimento>() != null
            || alvo.GetComponent<NavioPetroleiro>() != null
            || alvo.GetComponent<NavioTransporteTropas>() != null
            || alvo.GetComponent<GerenciadorPortaAvioes>() != null;
    }

    private static bool EhUnidadeAerea(GameObject alvo, IdentidadeUnidade identidade)
    {
        if (identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo)
        {
            return true;
        }

        return alvo.GetComponent<ControleAviao>() != null
            || alvo.GetComponent<ControleAviaoCaca>() != null
            || alvo.GetComponent<Helicoptero>() != null
            || alvo.GetComponent<C700TransporteAereo>() != null;
    }

    private static bool EhUnidadeTerrestreBlindadaOuViatura(GameObject alvo, IdentidadeUnidade identidade, string nome)
    {
        if (identidade != null && identidade.tipoUnidade == TipoUnidade.Veiculo)
        {
            return true;
        }

        if (alvo.GetComponent<CaminhaoTanqueAbastecimento>() != null
            || alvo.GetComponent<ControleTorre>() != null
            || alvo.GetComponent<ControleTorretaModular>() != null)
        {
            return true;
        }

        return nome.Contains("tank")
            || nome.Contains("leon")
            || nome.Contains("ubu")
            || nome.Contains("hamer")
            || nome.Contains("hack")
            || nome.Contains("caoc")
            || nome.Contains("msta")
            || nome.Contains("artillery")
            || nome.Contains("track")
            || nome.Contains("van");
    }

    private static bool BuscarPorTabela(
        string nomeNormalizado,
        RegraNomeTripulacao[] regras,
        out CategoriaTripulacao categoria,
        out int quantidade)
    {
        for (int i = 0; i < regras.Length; i++)
        {
            if (nomeNormalizado.Contains(regras[i].fragmento))
            {
                categoria = regras[i].categoria;
                quantidade = regras[i].tripulacao;
                return true;
            }
        }

        categoria = CategoriaTripulacao.Nenhuma;
        quantidade = 0;
        return false;
    }

    private static IdentidadeUnidade ObterIdentidade(GameObject alvo)
    {
        if (alvo == null) return null;
        return alvo.GetComponent<IdentidadeUnidade>()
            ?? alvo.GetComponentInParent<IdentidadeUnidade>()
            ?? alvo.GetComponentInChildren<IdentidadeUnidade>(true);
    }

    private static string ObterNomeNormalizado(GameObject alvo)
    {
        string raw = alvo != null ? alvo.name : string.Empty;
        raw = raw.Replace("(Clone)", string.Empty).Trim().ToLowerInvariant();
        return raw;
    }
}
