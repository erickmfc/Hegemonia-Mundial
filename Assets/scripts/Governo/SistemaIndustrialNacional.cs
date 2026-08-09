using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class SistemaIndustrialNacional : MonoBehaviour
{
    [Serializable]
    public class ConfiguracaoExtracaoIndustrial
    {
        public string recursoId = IndustriaIds.MinerioFerro;
        public string nomeRecurso = "Minério";
        public float custoDinheiro = 400f;
        public float custoEnergia = 50f;
        public float producaoMinima = 500f;
        public float producaoMaxima = 10000f;
        public float limiteUranioPaisNormal = 2500f;
        public float limiteUranioGrandeProdutor = 10000f;
        public bool exigeAutorizacao;
        public string descricaoBloqueio = string.Empty;
    }

    public static SistemaIndustrialNacional Instancia { get; private set; }

    [Header("Catálogo")]
    [SerializeField] private bool criarCatalogoPadrao = true;
    [SerializeField] private List<RecursoIndustrialSO> recursosCatalogo = new List<RecursoIndustrialSO>();
    [SerializeField] private List<ReceitaIndustrialSO> receitasCatalogo = new List<ReceitaIndustrialSO>();
    [SerializeField] private List<ConfiguracaoExtracaoIndustrial> configuracoesExtracao = new List<ConfiguracaoExtracaoIndustrial>();

    [Header("Diagnóstico")]
    [SerializeField] private int limiteHistorico = 200;

    public ArmazemNacional Armazem { get; private set; } = new ArmazemNacional();
    public ImpactoPublicoIndustrial ImpactoPublico { get; private set; } = new ImpactoPublicoIndustrial();

    private readonly Dictionary<int, PerfilMineralPais> perfis = new Dictionary<int, PerfilMineralPais>();
    private readonly Dictionary<int, EstadoIndustrialPais> estadosPais = new Dictionary<int, EstadoIndustrialPais>();
    private readonly Dictionary<int, List<LinhaIndustrial>> linhasPorPais = new Dictionary<int, List<LinhaIndustrial>>();
    private readonly List<OrdemExtracaoIndustrial> ordensExtracao = new List<OrdemExtracaoIndustrial>();
    private readonly List<OrdemRefinoIndustrial> ordensRefino = new List<OrdemRefinoIndustrial>();
    private readonly List<SaveHistoricoIndustrial> historico = new List<SaveHistoricoIndustrial>();

    public event Action<int> OnPaisAtualizado;
    public event Action<OrdemExtracaoIndustrial> OnOrdemExtracaoAtualizada;
    public event Action<OrdemRefinoIndustrial> OnOrdemRefinoAtualizada;
    public event Action<LinhaIndustrial> OnLinhaAtualizada;
    public event Action OnSistemaAtualizado;

    public IReadOnlyList<RecursoIndustrialSO> RecursosCatalogo => recursosCatalogo;
    public IReadOnlyList<ReceitaIndustrialSO> ReceitasCatalogo => receitasCatalogo;
    public IReadOnlyList<ConfiguracaoExtracaoIndustrial> ConfiguracoesExtracao => configuracoesExtracao;
    public IReadOnlyList<OrdemExtracaoIndustrial> OrdensExtracao => ordensExtracao;
    public IReadOnlyList<OrdemRefinoIndustrial> OrdensRefino => ordensRefino;
    public IReadOnlyList<SaveHistoricoIndustrial> Historico => historico;
    public IReadOnlyDictionary<int, PerfilMineralPais> Perfis => perfis;
    public IReadOnlyDictionary<int, EstadoIndustrialPais> EstadosPais => estadosPais;

    private static readonly Dictionary<string, RecursoMineral> MapaRecursoBruto = new Dictionary<string, RecursoMineral>(StringComparer.OrdinalIgnoreCase)
    {
        { IndustriaIds.MinerioFerro, RecursoMineral.MinerioFerro },
        { IndustriaIds.MinerioCobre, RecursoMineral.MinerioCobre },
        { IndustriaIds.Bauxita, RecursoMineral.Bauxita },
        { IndustriaIds.MinerioTitanio, RecursoMineral.MinerioTitanio },
        { IndustriaIds.UranioBruto, RecursoMineral.UranioBruto }
    };

    private static readonly Dictionary<string, MaterialRefinado> MapaMaterialRefinado = new Dictionary<string, MaterialRefinado>(StringComparer.OrdinalIgnoreCase)
    {
        { IndustriaIds.AcoEstrutural, MaterialRefinado.AcoEstrutural },
        { IndustriaIds.CobreEletrolitico, MaterialRefinado.CobreEletrolitico },
        { IndustriaIds.Duraluminio, MaterialRefinado.Duraluminio },
        { IndustriaIds.LigaTitanio, MaterialRefinado.LigaTitanio },
        { IndustriaIds.ComponentesEletronicos, MaterialRefinado.ComponentesEletronicos },
        { IndustriaIds.UranioEnriquecido, MaterialRefinado.UranioEnriquecido }
    };

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        InicializarCatalogoPadrao();
        CatalogoProdutoCompartilhado.RegistrarIndustrial(recursosCatalogo, receitasCatalogo);
        GarantirPaisExistentes();
        ConectarEventosTempoEMercado();
        IntegracaoMercadoIndustrial.GarantirCatalogoNoMercado(SistemaMercadoGlobal.Instancia);
        IntegracaoMercadoIndustrial.SincronizarEstoquesNoMercado(this, SistemaMercadoGlobal.Instancia);
    }

    private void Start()
    {
        GarantirPaisExistentes();
        ConectarEventosTempoEMercado();
        CatalogoProdutoCompartilhado.RegistrarIndustrial(recursosCatalogo, receitasCatalogo);
        IntegracaoMercadoIndustrial.GarantirCatalogoNoMercado(SistemaMercadoGlobal.Instancia);
        IntegracaoMercadoIndustrial.SincronizarEstoquesNoMercado(this, SistemaMercadoGlobal.Instancia);
    }

    private void OnEnable()
    {
        ConectarEventosTempoEMercado();
    }

    private void OnDisable()
    {
        DesconectarEventosTempoEMercado();
    }

    private void OnDestroy()
    {
        DesconectarEventosTempoEMercado();
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void ConectarEventosTempoEMercado()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada -= AoDiaAlterado;
            GerenciadorTempo.Instancia.OnDataAlterada += AoDiaAlterado;
        }

        if (SistemaMercadoGlobal.Instancia != null)
        {
            SistemaMercadoGlobal.Instancia.OnTransacaoExecutada -= OnTransacaoMercadoExecutada;
            SistemaMercadoGlobal.Instancia.OnTransacaoExecutada += OnTransacaoMercadoExecutada;
        }
    }

    private void DesconectarEventosTempoEMercado()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada -= AoDiaAlterado;
        }

        if (SistemaMercadoGlobal.Instancia != null)
        {
            SistemaMercadoGlobal.Instancia.OnTransacaoExecutada -= OnTransacaoMercadoExecutada;
        }
    }

    private void AoDiaAlterado()
    {
        ProcessarDia();
    }

    private void OnTransacaoMercadoExecutada(TransacaoMercado transacao)
    {
        IntegracaoMercadoIndustrial.ProcessarTransacao(transacao);
    }

    public void GarantirPaisExistentes()
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
        {
            if (pais == null)
            {
                continue;
            }

            GarantirPerfil(pais.teamId);
            GarantirLinhasPais(pais.teamId);
            AtualizarEstadoPais(pais.teamId);
        }
    }

    public PerfilMineralPais GarantirPerfil(int teamId)
    {
        if (!perfis.TryGetValue(teamId, out PerfilMineralPais perfil) || perfil == null)
        {
            perfil = new PerfilMineralPais();
            perfil.GerarPerfil(teamId);
            perfis[teamId] = perfil;

            foreach (KeyValuePair<string, RecursoMineral> par in MapaRecursoBruto)
            {
                if (perfil.EstaExtraindo(par.Value))
                {
                    continue;
                }

                perfil.SetExtracao(par.Value, true);
            }
        }

        GarantirOrdensExtracaoPadrao(teamId);
        GarantirLinhasPais(teamId);
        return perfil;
    }

    public void CarregarPerfil(PerfilMineralPais perfil)
    {
        if (perfil == null)
        {
            return;
        }

        perfis[perfil.teamId] = perfil;
        GarantirOrdensExtracaoPadrao(perfil.teamId);
        GarantirLinhasPais(perfil.teamId);
    }

    public void CarregarEstoque(EstoqueMineral estoque)
    {
        if (estoque == null)
        {
            return;
        }

        string paisId = estoque.teamId.ToString();
        List<QuantidadeRecursoIndustrial> totais = new List<QuantidadeRecursoIndustrial>
        {
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioFerro, estoque.minerioFerro),
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioCobre, estoque.minerioCobre),
            new QuantidadeRecursoIndustrial(IndustriaIds.Bauxita, estoque.bauxita),
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioTitanio, estoque.minerioTitanio),
            new QuantidadeRecursoIndustrial(IndustriaIds.UranioBruto, estoque.uranioBruto),
            new QuantidadeRecursoIndustrial(IndustriaIds.AcoEstrutural, estoque.acoEstrutural),
            new QuantidadeRecursoIndustrial(IndustriaIds.CobreEletrolitico, estoque.cobreEletrolitico),
            new QuantidadeRecursoIndustrial(IndustriaIds.Duraluminio, estoque.duraluminio),
            new QuantidadeRecursoIndustrial(IndustriaIds.LigaTitanio, estoque.ligaTitanio),
            new QuantidadeRecursoIndustrial(IndustriaIds.ComponentesEletronicos, estoque.componentesEletronicos),
            new QuantidadeRecursoIndustrial(IndustriaIds.UranioEnriquecido, estoque.uranioEnriquecido)
        };

        Armazem.AplicarSnapshot(paisId, totais, null);
        SincronizarPaisEmGoverno(estoque.teamId);
    }

    public void SincronizarDoPais(DadosPaisGoverno pais)
    {
        if (pais == null)
        {
            return;
        }

        string paisId = pais.teamId.ToString();
        List<QuantidadeRecursoIndustrial> totais = new List<QuantidadeRecursoIndustrial>
        {
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioFerro, pais.minerioFerro),
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioCobre, pais.minerioCobre),
            new QuantidadeRecursoIndustrial(IndustriaIds.Bauxita, pais.bauxita),
            new QuantidadeRecursoIndustrial(IndustriaIds.MinerioTitanio, pais.minerioTitanio),
            new QuantidadeRecursoIndustrial(IndustriaIds.UranioBruto, pais.uranioBruto),
            new QuantidadeRecursoIndustrial(IndustriaIds.AcoEstrutural, pais.acoEstrutural),
            new QuantidadeRecursoIndustrial(IndustriaIds.CobreEletrolitico, pais.cobreEletrolitico),
            new QuantidadeRecursoIndustrial(IndustriaIds.Duraluminio, pais.duraluminio),
            new QuantidadeRecursoIndustrial(IndustriaIds.LigaTitanio, pais.ligaTitanio),
            new QuantidadeRecursoIndustrial(IndustriaIds.ComponentesEletronicos, pais.componentesEletronicos),
            new QuantidadeRecursoIndustrial(IndustriaIds.UranioEnriquecido, pais.uranioEnriquecido)
        };

        Armazem.AplicarSnapshot(paisId, totais, Armazem.ObterReservasPais(paisId));
        SincronizarPaisEmGoverno(pais.teamId);
    }

    public List<PerfilMineralPais> TodosOsPerfis()
    {
        return perfis.Values.Where(p => p != null).ToList();
    }

    public List<EstoqueMineral> TodosOsEstoques()
    {
        List<EstoqueMineral> resultado = new List<EstoqueMineral>();
        if (SistemaGovernoMundial.Instancia == null)
        {
            return resultado;
        }

        foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
        {
            if (pais == null)
            {
                continue;
            }

            resultado.Add(new EstoqueMineral
            {
                teamId = pais.teamId,
                minerioFerro = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.MinerioFerro),
                minerioCobre = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.MinerioCobre),
                bauxita = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.Bauxita),
                minerioTitanio = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.MinerioTitanio),
                uranioBruto = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.UranioBruto),
                acoEstrutural = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.AcoEstrutural),
                cobreEletrolitico = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.CobreEletrolitico),
                duraluminio = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.Duraluminio),
                ligaTitanio = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.LigaTitanio),
                componentesEletronicos = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.ComponentesEletronicos),
                uranioEnriquecido = (float)Armazem.ObterDisponivel(pais.teamId.ToString(), IndustriaIds.UranioEnriquecido)
            });
        }

        return resultado;
    }

    public EstadoIndustrialPais ObterEstadoPais(int teamId)
    {
        if (estadosPais.TryGetValue(teamId, out EstadoIndustrialPais estado) && estado != null)
        {
            return estado;
        }

        if (SistemaGovernoMundial.Instancia == null)
        {
            return null;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return null;
        }

        estado = new EstadoIndustrialPais();
        AtualizarEstadoPais(estado, pais);
        estadosPais[teamId] = estado;
        return estado;
    }

    public IReadOnlyList<LinhaIndustrial> ObterLinhasPais(int teamId)
    {
        GarantirLinhasPais(teamId);
        return linhasPorPais.TryGetValue(teamId, out List<LinhaIndustrial> linhas) ? linhas : Array.Empty<LinhaIndustrial>();
    }

    public LinhaIndustrial ObterLinha(string linhaId)
    {
        if (string.IsNullOrWhiteSpace(linhaId))
        {
            return null;
        }

        foreach (List<LinhaIndustrial> linhas in linhasPorPais.Values)
        {
            LinhaIndustrial linha = linhas.FirstOrDefault(l => l != null && string.Equals(l.id, linhaId, StringComparison.OrdinalIgnoreCase));
            if (linha != null)
            {
                return linha;
            }
        }

        return null;
    }

    public OrdemExtracaoIndustrial CriarOuAtualizarOrdemExtracao(int teamId, string recursoId, bool continua, double quantidadeAlvo = 0d, int diasObjetivo = 1, double estoqueAlvo = 0d)
    {
        GarantirPerfil(teamId);

        OrdemExtracaoIndustrial ordem = ordensExtracao.FirstOrDefault(o => o != null && o.teamId == teamId && string.Equals(o.recursoId, recursoId, StringComparison.OrdinalIgnoreCase));
        if (ordem == null)
        {
            ordem = new OrdemExtracaoIndustrial();
            ordensExtracao.Add(ordem);
        }

        ordem.teamId = teamId;
        ordem.paisId = teamId.ToString();
        ordem.recursoId = NormalizarRecursoId(recursoId);
        ordem.nomeRecurso = NomeRecursoLegivel(recursoId);
        ordem.continua = continua;
        ordem.quantidadeAlvo = quantidadeAlvo;
        ordem.quantidadeRestante = quantidadeAlvo > 0d ? quantidadeAlvo : 0d;
        ordem.diasObjetivo = Mathf.Max(1, diasObjetivo);
        ordem.diasRestantes = ordem.diasObjetivo;
        ordem.estoqueAlvo = estoqueAlvo;
        ordem.custoDinheiro = ObterConfiguracaoExtracao(ordem.recursoId).custoDinheiro;
        ordem.custoEnergia = ObterConfiguracaoExtracao(ordem.recursoId).custoEnergia;
        ordem.exigeAutorizacao = ObterConfiguracaoExtracao(ordem.recursoId).exigeAutorizacao;
        ordem.autorizada = !ordem.exigeAutorizacao;
        ordem.estado = ordem.exigeAutorizacao ? EstadoOrdemExtracaoIndustrial.Bloqueada : EstadoOrdemExtracaoIndustrial.Aguardando;
        ordem.motivoBloqueio = ordem.exigeAutorizacao ? ObterConfiguracaoExtracao(ordem.recursoId).descricaoBloqueio : string.Empty;
        ordem.producaoBase = ObterConfiguracaoExtracao(ordem.recursoId).producaoMinima;
        ordem.ultimaDataProcessada = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        ordem.Inicializar();

        GarantirPerfil(teamId).SetExtracao(ConverterParaRecursoMineral(ordem.recursoId), true);
        OnOrdemExtracaoAtualizada?.Invoke(ordem);
        return ordem;
    }

    public bool CancelarOrdemExtracao(string ordemId)
    {
        OrdemExtracaoIndustrial ordem = ordensExtracao.FirstOrDefault(o => o != null && string.Equals(o.id, ordemId, StringComparison.OrdinalIgnoreCase));
        if (ordem == null)
        {
            return false;
        }

        ordem.estado = EstadoOrdemExtracaoIndustrial.Pausada;
        if (ordem.teamId > 0)
        {
            GarantirPerfil(ordem.teamId).SetExtracao(ConverterParaRecursoMineral(ordem.recursoId), false);
        }

        OnOrdemExtracaoAtualizada?.Invoke(ordem);
        return true;
    }

    public OrdemRefinoIndustrial CriarOrdemRefino(int teamId, string receitaId)
    {
        ReceitaIndustrialSO receita = ObterReceita(receitaId);
        if (receita == null)
        {
            return null;
        }

        GarantirPerfil(teamId);
        OrdemRefinoIndustrial ordem = ordensRefino.FirstOrDefault(o => o != null && o.teamId == teamId && string.Equals(o.receitaId, receita.id, StringComparison.OrdinalIgnoreCase));
        if (ordem == null)
        {
            ordem = new OrdemRefinoIndustrial();
            ordensRefino.Add(ordem);
        }

        string paisId = teamId.ToString();
        ordem.teamId = teamId;
        ordem.paisId = paisId;
        ordem.receitaId = receita.id;
        ordem.produtoId = receita.produtoFinalId;
        ordem.diasTotais = Mathf.Max(1, receita.diasNecessarios);
        ordem.diasRestantes = ordem.diasTotais;
        ordem.quantidadeEntrada = CalcularQuantidadeEntradaReceita(receita);
        ordem.quantidadeResultadoPrevista = receita.quantidadeProduzida;
        ordem.pesquisaExigida = receita.pesquisaExigida;
        ordem.nivelIndustrialExigido = receita.nivelIndustrialExigido;
        ordem.dinheiroReservado = receita.dinheiroNecessario;
        ordem.energiaReservada = receita.energiaNecessaria;
        ordem.inicioDia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        ordem.ultimaDataProcessada = ordem.inicioDia;
        ordem.estado = EstadoOrdemRefinoIndustrial.ReservandoRecursos;
        ordem.materiaisReservados.Clear();

        if (SistemaGovernoMundial.Instancia == null)
        {
            return null;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return null;
        }

        if (pais.nivelIndustrial < receita.nivelIndustrialExigido)
        {
            ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemVerba;
            ordem.motivoBloqueio = "Nível industrial insuficiente.";
            return ordem;
        }

        if (!ReservarMateriaisReceita(teamId, receita, ordem))
        {
            ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemVerba;
            ordem.motivoBloqueio = "Materiais insuficientes.";
            return ordem;
        }

        if (!SistemaGovernoMundial.Instancia.TentarPagar(teamId, receita.dinheiroNecessario))
        {
            LiberarMateriaisReservados(teamId, ordem, 1f);
            ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemVerba;
            ordem.motivoBloqueio = "Dinheiro insuficiente.";
            return ordem;
        }

        ordem.estado = EstadoOrdemRefinoIndustrial.Aguardando;
        VincularLinhaLivre(teamId, ordem);
        GarantirPerfil(teamId).SetRefino(ConverterParaMaterialRefinado(ordem.produtoId), true);
        OnOrdemRefinoAtualizada?.Invoke(ordem);
        return ordem;
    }

    public bool CancelarOrdemRefino(string ordemId)
    {
        OrdemRefinoIndustrial ordem = ordensRefino.FirstOrDefault(o => o != null && string.Equals(o.id, ordemId, StringComparison.OrdinalIgnoreCase));
        if (ordem == null)
        {
            return false;
        }

        float fatorReembolso = FatorReembolsoCancelamento(ordem);
        LiberarMateriaisReservados(ordem.teamId, ordem, fatorReembolso);

        if (ordem.dinheiroReservado > 0d && SistemaGovernoMundial.Instancia != null)
        {
            int dinheiro = Mathf.RoundToInt((float)(ordem.dinheiroReservado * fatorReembolso));
            if (dinheiro > 0)
            {
                SistemaGovernoMundial.Instancia.AdicionarSaldo(ordem.teamId, dinheiro);
            }
        }

        LinhaIndustrial linha = ObterLinha(ordem.linhaId);
        if (linha != null)
        {
            linha.Limpar();
            OnLinhaAtualizada?.Invoke(linha);
        }

        ordem.estado = EstadoOrdemRefinoIndustrial.Cancelada;
        ordem.motivoBloqueio = "Cancelada pelo jogador.";
        OnOrdemRefinoAtualizada?.Invoke(ordem);
        return true;
    }

    public double ObterQuantidadeTotal(string recursoId)
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return 0d;
        }

        double total = 0d;
        foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
        {
            if (pais == null)
            {
                continue;
            }

            total += Armazem.ObterDisponivel(pais.teamId.ToString(), recursoId);
        }

        return total;
    }

    public double ObterQuantidadePais(int teamId, string recursoId)
    {
        return Armazem.ObterDisponivel(teamId.ToString(), recursoId);
    }

    public double ObterQuantidadeReserva(int teamId, string recursoId)
    {
        return Armazem.ObterReservado(teamId.ToString(), recursoId);
    }

    public bool ReservarMaterial(string teamId, string recursoId, double quantidade)
    {
        bool ok = Armazem.Reservar(teamId, recursoId, quantidade);
        if (ok)
        {
            SincronizarPaisEmGoverno(ParseTeamId(teamId));
        }
        return ok;
    }

    public bool LiberarReserva(string teamId, string recursoId, double quantidade)
    {
        bool ok = Armazem.LiberarReserva(teamId, recursoId, quantidade);
        if (ok)
        {
            SincronizarPaisEmGoverno(ParseTeamId(teamId));
        }
        return ok;
    }

    public bool ConsumirReserva(string teamId, string recursoId, double quantidade)
    {
        bool ok = Armazem.ConsumirReserva(teamId, recursoId, quantidade);
        if (ok)
        {
            SincronizarPaisEmGoverno(ParseTeamId(teamId));
        }
        return ok;
    }

    public bool TentarAdicionar(string teamId, string recursoId, double quantidade)
    {
        Armazem.Adicionar(teamId, recursoId, quantidade);
        SincronizarPaisEmGoverno(ParseTeamId(teamId));
        return true;
    }

    /// <summary>
    /// Adds a complete factory cycle with one government synchronization.
    /// This avoids one economy/UI refresh per material when a factory produces
    /// the whole catalog automatically.
    /// </summary>
    public int AdicionarProducaoAutomatica(int teamId, IEnumerable<QuantidadeRecursoIndustrial> lote)
    {
        if (teamId <= 0 || lote == null) return 0;

        int adicionados = 0;
        foreach (QuantidadeRecursoIndustrial item in lote)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.recursoId) || item.quantidade <= 0d)
                continue;

            Armazem.Adicionar(teamId.ToString(), item.recursoId, item.quantidade);
            adicionados++;
        }

        if (adicionados > 0)
        {
            SincronizarPaisEmGoverno(teamId);
            OnSistemaAtualizado?.Invoke();
        }

        return adicionados;
    }

    public bool TentarConsumir(string teamId, string recursoId, double quantidade)
    {
        bool ok = Armazem.TentarConsumir(teamId, recursoId, quantidade);
        if (ok)
        {
            SincronizarPaisEmGoverno(ParseTeamId(teamId));
        }
        return ok;
    }

    public bool EstoqueAbaixoReservaMinima(int teamId, string recursoId)
    {
        double restante = ObterQuantidadePais(teamId, recursoId);
        return restante < IntegracaoMercadoIndustrial.ReservaMinimaPadrao(recursoId);
    }

    public void ProcessarDia()
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        int diaAtual = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        GarantirPaisExistentes();

        foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
        {
            if (pais == null)
            {
                continue;
            }

            ProcessarExtracaoPais(pais, diaAtual);
            ProcessarRefinoPais(pais, diaAtual);
            SincronizarPaisEmGoverno(pais.teamId);
            AtualizarEstadoPais(pais.teamId);
        }

        ImpactoPublico.ProcessarPendentes(diaAtual, SistemaGovernoMundial.Instancia);
        IntegracaoMercadoIndustrial.SincronizarEstoquesNoMercado(this, SistemaMercadoGlobal.Instancia);
        RegistrarHistoricoInterno("Sistema", string.Empty, "Ciclo diário processado.");
        OnSistemaAtualizado?.Invoke();
    }

    public void RestaurarSaveData(IndustrialSaveData save)
    {
        if (save == null)
        {
            return;
        }

        perfis.Clear();
        estadosPais.Clear();
        linhasPorPais.Clear();
        ordensExtracao.Clear();
        ordensRefino.Clear();
        historico.Clear();
        Armazem = new ArmazemNacional();
        ImpactoPublico = new ImpactoPublicoIndustrial();

        if (save.perfisMineral != null)
        {
            foreach (SavePerfilMineralIndustrial salvo in save.perfisMineral)
            {
                if (salvo == null)
                {
                    continue;
                }

                PerfilMineralPais perfil = new PerfilMineralPais
                {
                    teamId = salvo.teamId,
                    perfilGerado = salvo.perfilGerado,
                    ferro = (AbundanciaMineralNivel)salvo.ferro,
                    cobre = (AbundanciaMineralNivel)salvo.cobre,
                    bauxita = (AbundanciaMineralNivel)salvo.bauxita,
                    titanio = (AbundanciaMineralNivel)salvo.titanio,
                    uranio = (AbundanciaMineralNivel)salvo.uranio,
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
                };

                perfis[perfil.teamId] = perfil;
            }
        }

        if (save.estoques != null)
        {
            foreach (SaveEstoqueIndustrial salvo in save.estoques)
            {
                if (salvo == null || string.IsNullOrWhiteSpace(salvo.paisId))
                {
                    continue;
                }

                Armazem.AplicarSnapshot(salvo.paisId, salvo.estoques, salvo.reservas);
            }
        }

        if (save.ordensExtracao != null)
        {
            foreach (SaveOrdemExtracaoIndustrial salvo in save.ordensExtracao)
            {
                if (salvo == null)
                {
                    continue;
                }

                OrdemExtracaoIndustrial ordem = new OrdemExtracaoIndustrial
                {
                    id = salvo.id,
                    teamId = salvo.teamId,
                    paisId = salvo.teamId.ToString(),
                    recursoId = salvo.recursoId,
                    nomeRecurso = salvo.nomeRecurso,
                    estado = ParseEstadoExtracao(salvo.estado),
                    continua = salvo.continua,
                    diasObjetivo = Mathf.Max(1, salvo.diasObjetivo),
                    diasRestantes = Mathf.Max(0, salvo.diasRestantes),
                    quantidadeAlvo = salvo.quantidadeAlvo,
                    quantidadeRestante = salvo.quantidadeRestante,
                    estoqueAlvo = salvo.estoqueAlvo,
                    totalProduzido = salvo.totalProduzido,
                    custoDinheiro = salvo.custoDinheiro,
                    custoEnergia = salvo.custoEnergia,
                    producaoBase = salvo.producaoBase,
                    producaoUltimoDia = salvo.producaoUltimoDia,
                    exigeAutorizacao = salvo.exigeAutorizacao,
                    autorizada = salvo.autorizada,
                    motivoBloqueio = salvo.motivoBloqueio,
                    ultimaDataProcessada = salvo.ultimaDataProcessada
                };
                ordensExtracao.Add(ordem);
            }
        }

        if (save.ordensRefino != null)
        {
            foreach (SaveOrdemRefinoIndustrial salvo in save.ordensRefino)
            {
                if (salvo == null)
                {
                    continue;
                }

                OrdemRefinoIndustrial ordem = new OrdemRefinoIndustrial
                {
                    id = salvo.id,
                    teamId = salvo.teamId,
                    paisId = salvo.teamId.ToString(),
                    receitaId = salvo.receitaId,
                    produtoId = salvo.produtoId,
                    estado = ParseEstadoRefino(salvo.estado),
                    linhaId = salvo.linhaId,
                    progresso = salvo.progresso,
                    diasTotais = Mathf.Max(1, salvo.diasTotais),
                    diasRestantes = Mathf.Max(0, salvo.diasRestantes),
                    quantidadeEntrada = salvo.quantidadeEntrada,
                    quantidadeProduzida = salvo.quantidadeProduzida,
                    dinheiroReservado = salvo.dinheiroReservado,
                    energiaReservada = salvo.energiaReservada,
                    materiaisReservados = salvo.materiaisReservados != null ? new List<QuantidadeRecursoIndustrial>(salvo.materiaisReservados) : new List<QuantidadeRecursoIndustrial>(),
                    inicioDia = salvo.inicioDia,
                    ultimaDataProcessada = salvo.ultimaDataProcessada,
                    pesquisaExigida = salvo.pesquisaExigida,
                    nivelIndustrialExigido = salvo.nivelIndustrialExigido
                };
                ordensRefino.Add(ordem);
            }
        }

        if (save.linhas != null)
        {
            foreach (SaveLinhaIndustrial salvo in save.linhas)
            {
                if (salvo == null)
                {
                    continue;
                }

                if (!linhasPorPais.TryGetValue(salvo.teamId, out List<LinhaIndustrial> linhas))
                {
                    linhas = new List<LinhaIndustrial>();
                    linhasPorPais[salvo.teamId] = linhas;
                }

                LinhaIndustrial linha = new LinhaIndustrial
                {
                    id = salvo.id,
                    teamId = salvo.teamId,
                    indice = salvo.indice,
                    estado = ParseEstadoLinha(salvo.estado),
                    ordemRefinoId = salvo.ordemRefinoId,
                    receitaId = salvo.receitaId,
                    progresso = salvo.progresso,
                    diasTotais = Mathf.Max(1, salvo.diasTotais),
                    diasRestantes = Mathf.Max(0, salvo.diasRestantes),
                    motivoBloqueio = salvo.motivoBloqueio
                };
                linhas.Add(linha);
            }
        }

        if (save.impactosPendentes != null)
        {
            ImpactoPublico.AplicarSnapshot(save.impactosPendentes);
        }

        if (save.historico != null)
        {
            historico.AddRange(save.historico.Where(h => h != null));
        }

        GarantirPaisExistentes();
        IntegracaoMercadoIndustrial.SincronizarEstoquesNoMercado(this, SistemaMercadoGlobal.Instancia);
    }

    public IndustrialSaveData CriarSaveData()
    {
        IndustrialSaveData save = new IndustrialSaveData
        {
            totalDias = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1
        };

        save.perfisMineral.AddRange(perfis.Values.Where(p => p != null).Select(ConvertParaSavePerfil));

        if (SistemaGovernoMundial.Instancia != null)
        {
            foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
            {
                if (pais == null)
                {
                    continue;
                }

                save.estoques.Add(Armazem.CriarSnapshot(pais.teamId.ToString()));
            }
        }

        foreach (OrdemExtracaoIndustrial ordem in ordensExtracao.Where(o => o != null))
        {
            save.ordensExtracao.Add(ConvertParaSaveOrdemExtracao(ordem));
        }

        foreach (OrdemRefinoIndustrial ordem in ordensRefino.Where(o => o != null))
        {
            save.ordensRefino.Add(ConvertParaSaveOrdemRefino(ordem));
        }

        foreach (KeyValuePair<int, List<LinhaIndustrial>> par in linhasPorPais)
        {
            foreach (LinhaIndustrial linha in par.Value.Where(l => l != null))
            {
                save.linhas.Add(ConvertParaSaveLinha(linha));
            }
        }

        save.impactosPendentes.AddRange(ImpactoPublico.CriarSnapshot());
        save.historico.AddRange(historico);
        return save;
    }

    public SavePerfilMineralIndustrial ConverterPerfilParaSave(PerfilMineralPais perfil)
    {
        return ConvertParaSavePerfil(perfil);
    }

    public SaveEstoqueIndustrial CriarSnapshotEstoque(int teamId)
    {
        return Armazem.CriarSnapshot(teamId.ToString());
    }

    public double ObterQuantidadeDisponivel(RecursoMercado recurso, int teamId)
    {
        return ObterQuantidadePais(teamId, IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso));
    }

    public bool TentarConsumir(int teamId, RecursoMercado recurso, int quantidade)
    {
        return TentarConsumir(teamId.ToString(), IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso), quantidade);
    }

    public void AdicionarEstoque(int teamId, RecursoMercado recurso, int quantidade)
    {
        string recursoId = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return;
        }

        if (MapaRecursoBruto.ContainsKey(recursoId))
        {
            // Mantém o recurso bruto sob o ID interno do armazém, não no campo legado.
            TentarAdicionar(teamId.ToString(), recursoId, quantidade);
        }
        else if (MapaMaterialRefinado.ContainsKey(recursoId))
        {
            TentarAdicionar(teamId.ToString(), recursoId, quantidade);
        }
        else
        {
            TentarAdicionar(teamId.ToString(), recursoId, quantidade);
        }
    }

    public void RemoverEstoque(int teamId, RecursoMercado recurso, int quantidade)
    {
        string recursoId = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return;
        }

        TentarConsumir(teamId.ToString(), recursoId, quantidade);
    }

    public int ObterQuantidadeInt(int teamId, RecursoMercado recurso)
    {
        string recursoId = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
        return Mathf.RoundToInt((float)ObterQuantidadePais(teamId, recursoId));
    }

    public float SimularProducao(int teamId, string recursoId, int dia)
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return 0f;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return 0f;
        }

        PerfilMineralPais perfil = GarantirPerfil(teamId);
        if (!MapaRecursoBruto.TryGetValue(recursoId, out RecursoMineral recurso))
        {
            return 0f;
        }

        AbundanciaMineralNivel nivel = perfil.ObterAbundancia(recurso);
        ConfiguracaoExtracaoIndustrial cfg = ObterConfiguracaoExtracao(recursoId);
        return (float)CalcularProducaoDiaria(pais, perfil, recursoId, nivel, cfg, dia);
    }

    private void GarantirOrdensExtracaoPadrao(int teamId)
    {
        if (!perfis.TryGetValue(teamId, out PerfilMineralPais perfil) || perfil == null)
        {
            return;
        }

        foreach (ConfiguracaoExtracaoIndustrial cfg in configuracoesExtracao)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.recursoId))
            {
                continue;
            }

            OrdemExtracaoIndustrial ordem = ordensExtracao.FirstOrDefault(o => o != null && o.teamId == teamId && string.Equals(o.recursoId, cfg.recursoId, StringComparison.OrdinalIgnoreCase));
            if (ordem != null)
            {
                continue;
            }

            ordem = new OrdemExtracaoIndustrial
            {
                teamId = teamId,
                paisId = teamId.ToString(),
                recursoId = cfg.recursoId,
                nomeRecurso = string.IsNullOrWhiteSpace(cfg.nomeRecurso) ? NomeRecursoLegivel(cfg.recursoId) : cfg.nomeRecurso,
                continua = true,
                diasObjetivo = 1,
                diasRestantes = 1,
                quantidadeAlvo = 0d,
                quantidadeRestante = 0d,
                estoqueAlvo = 0d,
                custoDinheiro = cfg.custoDinheiro,
                custoEnergia = cfg.custoEnergia,
                producaoBase = cfg.producaoMinima,
                exigeAutorizacao = cfg.exigeAutorizacao,
                autorizada = !cfg.exigeAutorizacao,
                estado = cfg.exigeAutorizacao ? EstadoOrdemExtracaoIndustrial.Bloqueada : EstadoOrdemExtracaoIndustrial.Aguardando,
                motivoBloqueio = cfg.exigeAutorizacao ? cfg.descricaoBloqueio : string.Empty,
                ultimaDataProcessada = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1
            };
            ordem.Inicializar();
            ordensExtracao.Add(ordem);
            perfil.SetExtracao(ConverterParaRecursoMineral(cfg.recursoId), true);
        }
    }

    private void ProcessarExtracaoPais(DadosPaisGoverno pais, int diaAtual)
    {
        PerfilMineralPais perfil = GarantirPerfil(pais.teamId);
        IEnumerable<OrdemExtracaoIndustrial> ordens = ordensExtracao.Where(o => o != null && o.teamId == pais.teamId);

        foreach (OrdemExtracaoIndustrial ordem in ordens)
        {
            if (!MapaRecursoBruto.TryGetValue(ordem.recursoId, out RecursoMineral recurso))
            {
                continue;
            }

            AbundanciaMineralNivel nivel = perfil.ObterAbundancia(recurso);
            if (nivel == AbundanciaMineralNivel.Inexistente)
            {
                continue;
            }

            ConfiguracaoExtracaoIndustrial cfg = ObterConfiguracaoExtracao(ordem.recursoId);
            if (ordem.exigeAutorizacao && !ordem.autorizada)
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.Bloqueada;
                continue;
            }

            if (!perfil.EstaExtraindo(recurso))
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.Pausada;
                continue;
            }

            if (pais.energia < cfg.custoEnergia)
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.SemEnergia;
                continue;
            }

            if (!TentarCobrarExtracao(pais, cfg))
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.SemVerba;
                continue;
            }

            double producao = CalcularProducaoDiaria(pais, perfil, ordem.recursoId, nivel, cfg, diaAtual);
            producao = Math.Max(cfg.producaoMinima, producao);
            producao = Math.Min(producao, cfg.producaoMaxima > 0f ? cfg.producaoMaxima : 10000d);
            if (string.Equals(ordem.recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
            {
                producao = Math.Min(producao, nivel >= AbundanciaMineralNivel.Alto ? cfg.limiteUranioGrandeProdutor : cfg.limiteUranioPaisNormal);
            }

            ordem.producaoUltimoDia = (float)producao;
            ordem.totalProduzido += producao;
            ordem.ultimaDataProcessada = diaAtual;
            ordem.estado = EstadoOrdemExtracaoIndustrial.ConcluindoCiclo;

            if (ordem.quantidadeAlvo > 0d)
            {
                ordem.quantidadeRestante = Math.Max(0d, ordem.quantidadeRestante - producao);
            }

            Armazem.Adicionar(pais.teamId.ToString(), ordem.recursoId, producao);
            RegistrarHistoricoInterno(pais.teamId.ToString(), ordem.recursoId, $"{ordem.nomeRecurso} +{producao:N0} t.");

            if (ordem.quantidadeAlvo > 0d && ordem.quantidadeRestante <= 0d)
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.Pausada;
                perfil.SetExtracao(recurso, false);
            }
            else if (ordem.continua)
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.Aguardando;
            }
            else
            {
                ordem.estado = EstadoOrdemExtracaoIndustrial.Pausada;
            }

            ordem.MarcarCicloConcluido();
            OnOrdemExtracaoAtualizada?.Invoke(ordem);
        }
    }

    private void ProcessarRefinoPais(DadosPaisGoverno pais, int diaAtual)
    {
        GarantirLinhasPais(pais.teamId);
        List<LinhaIndustrial> linhas = linhasPorPais.TryGetValue(pais.teamId, out List<LinhaIndustrial> lista) ? lista : new List<LinhaIndustrial>();
        List<OrdemRefinoIndustrial> ordensPais = ordensRefino.Where(o => o != null && o.teamId == pais.teamId && o.estado != EstadoOrdemRefinoIndustrial.Cancelada && o.estado != EstadoOrdemRefinoIndustrial.Concluida).ToList();

        foreach (OrdemRefinoIndustrial ordem in ordensPais)
        {
            ReceitaIndustrialSO receita = ObterReceita(ordem.receitaId);
            if (receita == null)
            {
                continue;
            }

            if (pais.nivelIndustrial < receita.nivelIndustrialExigido)
            {
                ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemVerba;
                ordem.motivoBloqueio = "Nível industrial insuficiente.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(ordem.linhaId))
            {
                LinhaIndustrial linhaLivre = linhas.FirstOrDefault(l => l != null && l.EstaLivre);
                if (linhaLivre == null)
                {
                    ordem.estado = EstadoOrdemRefinoIndustrial.Aguardando;
                    continue;
                }

                VincularLinha(linhaLivre, ordem, receita);
            }

            LinhaIndustrial linha = ObterLinha(ordem.linhaId);
            if (linha == null)
            {
                ordem.linhaId = string.Empty;
                ordem.estado = EstadoOrdemRefinoIndustrial.Aguardando;
                continue;
            }

            double energiaPorDia = Math.Max(1d, receita.energiaNecessaria / (double)Math.Max(1, receita.diasNecessarios));
            if (pais.energia < energiaPorDia)
            {
                ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemEnergia;
                linha.estado = EstadoLinhaIndustrial.PausadaSemEnergia;
                linha.motivoBloqueio = "Sem energia.";
                OnLinhaAtualizada?.Invoke(linha);
                continue;
            }

            if (!ConsumirEnergiaPais(pais, energiaPorDia))
            {
                ordem.estado = EstadoOrdemRefinoIndustrial.PausadaSemEnergia;
                linha.estado = EstadoLinhaIndustrial.PausadaSemEnergia;
                linha.motivoBloqueio = "Sem energia.";
                OnLinhaAtualizada?.Invoke(linha);
                continue;
            }

            ordem.estado = EstadoOrdemRefinoIndustrial.Produzindo;
            linha.estado = EstadoLinhaIndustrial.Produzindo;
            ordem.ultimaDataProcessada = diaAtual;
            linha.AvancarDia(1f / Mathf.Max(1, receita.diasNecessarios));
            ordem.RegistrarProgresso(1f / Mathf.Max(1, receita.diasNecessarios));
            ordem.diasRestantes = Mathf.Max(0, ordem.diasRestantes - 1);

            if (ordem.diasRestantes > 0)
            {
                OnOrdemRefinoAtualizada?.Invoke(ordem);
                OnLinhaAtualizada?.Invoke(linha);
                continue;
            }

            ProduzirResultadoRefino(pais, ordem, receita, linha);
            OnOrdemRefinoAtualizada?.Invoke(ordem);
            OnLinhaAtualizada?.Invoke(linha);
        }
    }

    private void ProduzirResultadoRefino(DadosPaisGoverno pais, OrdemRefinoIndustrial ordem, ReceitaIndustrialSO receita, LinhaIndustrial linha)
    {
        if (receita == null || ordem == null || linha == null)
        {
            return;
        }

        foreach (QuantidadeRecursoIndustrial material in ordem.materiaisReservados)
        {
            if (material == null || string.IsNullOrWhiteSpace(material.recursoId) || material.quantidade <= 0d)
            {
                continue;
            }

            Armazem.ConsumirReserva(ordem.teamId.ToString(), material.recursoId, material.quantidade);
        }

        Armazem.Adicionar(ordem.teamId.ToString(), receita.produtoFinalId, receita.quantidadeProduzida);
        ordem.quantidadeProduzida += receita.quantidadeProduzida;
        ordem.RegistrarConclusao(receita.quantidadeProduzida);
        ordem.estado = EstadoOrdemRefinoIndustrial.Concluida;
        linha.Limpar();
        RegistrarHistoricoInterno(ordem.teamId.ToString(), receita.produtoFinalId, $"{receita.nome} concluída: +{receita.quantidadeProduzida:N0} {receita.unidadeResultado}");

        PerfilMineralPais perfil = GarantirPerfil(ordem.teamId);
        perfil.SetRefino(ConverterParaMaterialRefinado(receita.produtoFinalId), false);
    }

    private void VincularLinhaLivre(int teamId, OrdemRefinoIndustrial ordem)
    {
        if (!linhasPorPais.TryGetValue(teamId, out List<LinhaIndustrial> linhas))
        {
            return;
        }

        LinhaIndustrial linha = linhas.FirstOrDefault(l => l != null && l.EstaLivre);
        if (linha == null)
        {
            return;
        }

        ReceitaIndustrialSO receita = ObterReceita(ordem.receitaId);
        VincularLinha(linha, ordem, receita);
    }

    private void VincularLinha(LinhaIndustrial linha, OrdemRefinoIndustrial ordem, ReceitaIndustrialSO receita)
    {
        if (linha == null || ordem == null || receita == null)
        {
            return;
        }

        linha.AtribuirOrdem(ordem.id, receita.id, receita.diasNecessarios);
        ordem.linhaId = linha.id;
        ordem.estado = EstadoOrdemRefinoIndustrial.Produzindo;
        OnLinhaAtualizada?.Invoke(linha);
    }

    private void GarantirLinhasPais(int teamId)
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return;
        }

        int nivelFabrica = ObterNivelFabrica(pais.nivelIndustrial);
        int quantidadeLinhas = ObterQuantidadeLinhas(nivelFabrica);
        if (!linhasPorPais.TryGetValue(teamId, out List<LinhaIndustrial> linhas))
        {
            linhas = new List<LinhaIndustrial>();
            linhasPorPais[teamId] = linhas;
        }

        while (linhas.Count < quantidadeLinhas)
        {
            LinhaIndustrial linha = new LinhaIndustrial();
            linha.Inicializar(teamId, linhas.Count);
            linhas.Add(linha);
            OnLinhaAtualizada?.Invoke(linha);
        }

        for (int i = 0; i < linhas.Count; i++)
        {
            if (linhas[i] == null)
            {
                linhas[i] = new LinhaIndustrial();
                linhas[i].Inicializar(teamId, i);
            }
        }
    }

    private void AtualizarEstadoPais(int teamId)
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return;
        }

        List<LinhaIndustrial> linhas = ObterLinhasPais(teamId).ToList();
        int linhasOcupadas = linhas.Count(l => l != null && l.EstaOcupada);
        int linhasDisponiveis = linhas.Count(l => l != null && l.EstaLivre);
        int ordensAtivas = ordensExtracao.Count(o => o != null && o.teamId == teamId && o.estado != EstadoOrdemExtracaoIndustrial.Pausada && o.estado != EstadoOrdemExtracaoIndustrial.Bloqueada) +
                           ordensRefino.Count(o => o != null && o.teamId == teamId && o.estado != EstadoOrdemRefinoIndustrial.Cancelada && o.estado != EstadoOrdemRefinoIndustrial.Concluida);
        double estoqueTotal = SomarEstoqueDisponivel(teamId);
        float dependencia = pais.importacaoTotal > 0f ? Mathf.Clamp01(pais.importacaoTotal / Mathf.Max(1f, pais.importacaoTotal + pais.exportacaoTotal)) : 0f;
        float producaoDiaria = CalcularProducaoDiariaTotal(teamId);
        int nivelFabrica = ObterNivelFabrica(pais.nivelIndustrial);

        if (!estadosPais.TryGetValue(teamId, out EstadoIndustrialPais estado) || estado == null)
        {
            estado = new EstadoIndustrialPais();
            estadosPais[teamId] = estado;
        }

        estado.Atualizar(pais, nivelFabrica, linhasDisponiveis, linhasOcupadas, ordensAtivas, producaoDiaria, estoqueTotal, dependencia);
        OnPaisAtualizado?.Invoke(teamId);
    }

    private void AtualizarEstadoPais(EstadoIndustrialPais estado, DadosPaisGoverno pais)
    {
        if (estado == null || pais == null)
        {
            return;
        }

        float dependencia = pais.importacaoTotal > 0f ? Mathf.Clamp01(pais.importacaoTotal / Mathf.Max(1f, pais.importacaoTotal + pais.exportacaoTotal)) : 0f;
        estado.Atualizar(pais, ObterNivelFabrica(pais.nivelIndustrial), 0, 0, 0, 0f, SomarEstoqueDisponivel(pais.teamId), dependencia);
    }

    private void SincronizarPaisEmGoverno(int teamId)
    {
        if (SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null)
        {
            return;
        }

        pais.minerioFerro = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.MinerioFerro));
        pais.minerioCobre = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.MinerioCobre));
        pais.bauxita = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.Bauxita));
        pais.minerioTitanio = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.MinerioTitanio));
        pais.uranioBruto = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.UranioBruto));
        pais.acoEstrutural = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.AcoEstrutural));
        pais.cobreEletrolitico = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.CobreEletrolitico));
        pais.duraluminio = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.Duraluminio));
        pais.ligaTitanio = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.LigaTitanio));
        pais.componentesEletronicos = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.ComponentesEletronicos));
        pais.uranioEnriquecido = Mathf.RoundToInt((float)Armazem.ObterDisponivel(teamId.ToString(), IndustriaIds.UranioEnriquecido));

        pais.aco = Mathf.RoundToInt(pais.acoEstrutural);
        pais.uranio = Mathf.RoundToInt(pais.uranioBruto);

        if (teamId == SistemaGovernoMundial.Instancia.teamJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.aco = Mathf.RoundToInt(pais.acoEstrutural);
            GerenciadorRecursos.Instancia.NotificarAtualizacao();
        }
    }

    private double SomarEstoqueDisponivel(int teamId)
    {
        string paisId = teamId.ToString();
        double total = 0d;
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.MinerioFerro);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.MinerioCobre);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.Bauxita);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.MinerioTitanio);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.UranioBruto);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.AcoEstrutural);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.CobreEletrolitico);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.Duraluminio);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.LigaTitanio);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.ComponentesEletronicos);
        total += Armazem.ObterDisponivel(paisId, IndustriaIds.UranioEnriquecido);
        return total;
    }

    private float CalcularProducaoDiariaTotal(int teamId)
    {
        float total = 0f;
        foreach (OrdemExtracaoIndustrial ordem in ordensExtracao)
        {
            if (ordem == null || ordem.teamId != teamId)
            {
                continue;
            }

            total += ordem.producaoUltimoDia;
        }
        return total;
    }

    private void RegistrarHistoricoInterno(string teamId, string recursoId, string mensagem, double quantidade = 0d, double custoDinheiro = 0d, double custoEnergia = 0d)
    {
        if (historico.Count >= limiteHistorico)
        {
            historico.RemoveAt(0);
        }

        historico.Add(new SaveHistoricoIndustrial
        {
            teamId = int.TryParse(teamId, out int parsed) ? parsed : 0,
            recursoId = recursoId,
            categoria = CategoriaDoRecurso(recursoId),
            quantidade = quantidade,
            custoDinheiro = custoDinheiro,
            custoEnergia = custoEnergia,
            dia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1,
            mensagem = mensagem
        });
    }

    private bool TentarCobrarExtracao(DadosPaisGoverno pais, ConfiguracaoExtracaoIndustrial cfg)
    {
        if (pais == null || cfg == null)
        {
            return false;
        }

        bool pagou = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.TentarPagar(pais.teamId, Mathf.RoundToInt(cfg.custoDinheiro))
            : pais.saldo >= cfg.custoDinheiro;

        if (!pagou)
        {
            return false;
        }

        int energia = Mathf.RoundToInt(cfg.custoEnergia);
        if (pais.energia < energia)
        {
            if (SistemaGovernoMundial.Instancia != null)
            {
                SistemaGovernoMundial.Instancia.AdicionarSaldo(pais.teamId, Mathf.RoundToInt(cfg.custoDinheiro));
            }
            return false;
        }

        pais.energia = Mathf.Max(0, pais.energia - energia);
        if (pais.teamId == SistemaGovernoMundial.Instancia?.teamJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.energia = pais.energia;
            GerenciadorRecursos.Instancia.NotificarAtualizacao();
        }

        RegistrarHistoricoInterno(pais.teamId.ToString(), string.Empty, $"Extração cobrada: ${cfg.custoDinheiro:N0} + {cfg.custoEnergia:N0} energia.", 0d, cfg.custoDinheiro, cfg.custoEnergia);
        return true;
    }

    private bool ConsumirEnergiaPais(DadosPaisGoverno pais, double energia)
    {
        if (pais == null)
        {
            return false;
        }

        int valor = Mathf.RoundToInt((float)energia);
        if (pais.energia < valor)
        {
            return false;
        }

        pais.energia -= valor;
        if (pais.teamId == SistemaGovernoMundial.Instancia?.teamJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.energia = pais.energia;
            GerenciadorRecursos.Instancia.NotificarAtualizacao();
        }

        return true;
    }

    private static int ParseTeamId(string teamId)
    {
        return int.TryParse(teamId, out int parsed) ? parsed : 0;
    }

    private static string NormalizarRecursoId(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return string.Empty;
        }

        string normalizado = recursoId.Trim();
        if (string.Equals(normalizado, "aco", StringComparison.OrdinalIgnoreCase))
        {
            return IndustriaIds.AcoEstrutural;
        }

        if (string.Equals(normalizado, "uranio", StringComparison.OrdinalIgnoreCase))
        {
            return IndustriaIds.UranioBruto;
        }

        return normalizado.ToLowerInvariant();
    }

    private bool ReservarMateriaisReceita(int teamId, ReceitaIndustrialSO receita, OrdemRefinoIndustrial ordem)
    {
        if (receita == null || ordem == null)
        {
            return false;
        }

        string paisId = teamId.ToString();
        foreach (MaterialNecessarioIndustrial material in receita.materiaisNecessarios)
        {
            if (material == null || string.IsNullOrWhiteSpace(material.recursoId) || material.quantidade <= 0d)
            {
                continue;
            }

            if (!Armazem.Reservar(paisId, material.recursoId, material.quantidade))
            {
                LiberarMateriaisReservados(teamId, ordem, 1f);
                return false;
            }

            ordem.AdicionarMaterialReservado(material.recursoId, material.quantidade);
        }

        return true;
    }

    private void LiberarMateriaisReservados(int teamId, OrdemRefinoIndustrial ordem, float fatorReembolso)
    {
        if (ordem == null || ordem.materiaisReservados == null)
        {
            return;
        }

        string paisId = teamId.ToString();
        foreach (QuantidadeRecursoIndustrial material in ordem.materiaisReservados)
        {
            if (material == null || string.IsNullOrWhiteSpace(material.recursoId) || material.quantidade <= 0d)
            {
                continue;
            }

            double devolucao = material.quantidade * fatorReembolso;
            double perda = material.quantidade - devolucao;
            if (devolucao > 0d)
            {
                Armazem.LiberarReserva(paisId, material.recursoId, devolucao);
            }

            if (perda > 0d)
            {
                Armazem.ConsumirReserva(paisId, material.recursoId, perda);
            }
        }
    }

    private float FatorReembolsoCancelamento(OrdemRefinoIndustrial ordem)
    {
        float progresso = Mathf.Clamp01(ordem != null ? ordem.progresso : 0f);
        if (progresso < 0.25f)
        {
            return 0.90f;
        }

        if (progresso < 0.75f)
        {
            return 0.60f;
        }

        return 0.30f;
    }

    private float CalcularProducaoDiaria(DadosPaisGoverno pais, PerfilMineralPais perfil, string recursoId, AbundanciaMineralNivel nivel, ConfiguracaoExtracaoIndustrial cfg, int diaAtual)
    {
        float baseProducao = TabelaProducaoMineral.ObterProducaoBase(nivel);
        if (baseProducao <= 0f)
        {
            baseProducao = cfg != null ? cfg.producaoMinima : 500f;
        }

        float eficienciaIndustrial = Mathf.Clamp01(pais.nivelIndustrial / 100f);
        float energiaDisponivel = Mathf.Clamp01(pais.energia / 200f);
        float estabilidade = Mathf.Clamp01(pais.estabilidade / 100f);
        float investimento = Mathf.Clamp(0.75f + (pais.saldo / 60000f), 0.75f, 1.25f);
        float modificadorPerfil = perfil != null ? Mathf.Max(0.5f, perfil.modificadorIndustrial) : 1f;

        int seed = GerarSeedDeterministica(pais.teamId, recursoId, diaAtual);
        System.Random rng = new System.Random(seed);
        float variacao = 0.75f + (float)rng.NextDouble() * 0.50f;

        float producao = baseProducao * eficienciaIndustrial * energiaDisponivel * estabilidade * investimento * modificadorPerfil * variacao;
        float minimo = cfg != null ? cfg.producaoMinima : 500f;
        float maximo = cfg != null ? cfg.producaoMaxima : 10000f;
        producao = Mathf.Clamp(producao, minimo, maximo);
        return producao;
    }

    private static int GerarSeedDeterministica(int teamId, string recursoId, int dia)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + teamId;
            hash = hash * 31 + dia;
            if (!string.IsNullOrWhiteSpace(recursoId))
            {
                for (int i = 0; i < recursoId.Length; i++)
                {
                    hash = hash * 31 + recursoId[i];
                }
            }
            return hash;
        }
    }

    private ConfiguracaoExtracaoIndustrial ObterConfiguracaoExtracao(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return configuracoesExtracao.Count > 0 ? configuracoesExtracao[0] : CriarConfiguracaoPadrao(IndustriaIds.MinerioFerro, "Minério de ferro", 400f, 50f, 500f, 10000f, false, string.Empty);
        }

        ConfiguracaoExtracaoIndustrial cfg = configuracoesExtracao.FirstOrDefault(c => c != null && string.Equals(c.recursoId, recursoId, StringComparison.OrdinalIgnoreCase));
        if (cfg != null)
        {
            return cfg;
        }

        return CriarConfiguracaoPadrao(recursoId, NomeRecursoLegivel(recursoId), 400f, 50f, 500f, 10000f, string.Equals(recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase), string.Empty);
    }

    private ReceitaIndustrialSO ObterReceita(string receitaId)
    {
        if (string.IsNullOrWhiteSpace(receitaId))
        {
            return null;
        }

        return receitasCatalogo.FirstOrDefault(r => r != null && (string.Equals(r.id, receitaId, StringComparison.OrdinalIgnoreCase) || string.Equals(r.produtoFinalId, receitaId, StringComparison.OrdinalIgnoreCase)));
    }

    private static string CategoriaDoRecurso(string recursoId)
    {
        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return "Matéria-prima";
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
            {
                return "Nuclear";
            }

            return "Refinado";
        }

        return "Industrial";
    }

    private static string NomeRecursoLegivel(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return "Recurso";
        }

        switch (recursoId.ToLowerInvariant())
        {
            case IndustriaIds.MinerioFerro: return "Minério de ferro";
            case IndustriaIds.MinerioCobre: return "Minério de cobre";
            case IndustriaIds.Bauxita: return "Bauxita";
            case IndustriaIds.MinerioTitanio: return "Minério de titânio";
            case IndustriaIds.UranioBruto: return "Urânio bruto";
            case IndustriaIds.AcoEstrutural: return "Aço estrutural";
            case IndustriaIds.CobreEletrolitico: return "Cobre eletrolítico";
            case IndustriaIds.Duraluminio: return "Duralumínio";
            case IndustriaIds.LigaTitanio: return "Liga de titânio";
            case IndustriaIds.ComponentesEletronicos: return "Componentes eletrônicos";
            case IndustriaIds.UranioEnriquecido: return "Urânio enriquecido";
            default: return recursoId;
        }
    }

    private static RecursoMineral ConverterParaRecursoMineral(string recursoId)
    {
        if (MapaRecursoBruto.TryGetValue(recursoId, out RecursoMineral recurso))
        {
            return recurso;
        }

        return RecursoMineral.MinerioFerro;
    }

    private static MaterialRefinado ConverterParaMaterialRefinado(string recursoId)
    {
        if (MapaMaterialRefinado.TryGetValue(recursoId, out MaterialRefinado material))
        {
            return material;
        }

        return MaterialRefinado.AcoEstrutural;
    }

    private static int ObterNivelFabrica(int nivelIndustrial)
    {
        if (nivelIndustrial < 25)
        {
            return 1;
        }

        if (nivelIndustrial < 50)
        {
            return 2;
        }

        if (nivelIndustrial < 75)
        {
            return 3;
        }

        return 4;
    }

    private static int ObterQuantidadeLinhas(int nivelFabrica)
    {
        switch (nivelFabrica)
        {
            case 1: return 2;
            case 2: return 3;
            case 3: return 5;
            default: return 8;
        }
    }

    private float CalcularQuantidadeEntradaReceita(ReceitaIndustrialSO receita)
    {
        if (receita == null || receita.materiaisNecessarios == null)
        {
            return 0f;
        }

        double total = 0d;
        foreach (MaterialNecessarioIndustrial material in receita.materiaisNecessarios)
        {
            if (material != null)
            {
                total += material.quantidade;
            }
        }

        return (float)total;
    }

    private void InicializarCatalogoPadrao()
    {
        if (!criarCatalogoPadrao)
        {
            return;
        }

        if (recursosCatalogo.Count == 0)
        {
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioFerro, "Minério de ferro", "Matéria-prima bruta para aço.", CategoriaRecursoIndustrial.MateriaPrima, "t", 78, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioCobre, "Minério de cobre", "Matéria-prima para cobre eletrolítico.", CategoriaRecursoIndustrial.MateriaPrima, "t", 118, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Bauxita, "Bauxita", "Base de duralumínio.", CategoriaRecursoIndustrial.MateriaPrima, "t", 95, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioTitanio, "Minério de titânio", "Matéria estratégica para liga de titânio.", CategoriaRecursoIndustrial.Estrategico, "t", 260, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.UranioBruto, "Urânio bruto", "Recurso nuclear bruto.", CategoriaRecursoIndustrial.Estrategico, "t", 520, RaridadeRecursoIndustrial.MuitoRaro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.AcoEstrutural, "Aço estrutural", "Material de construção e indústria pesada.", CategoriaRecursoIndustrial.Refinado, "t", 180, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CobreEletrolitico, "Cobre eletrolítico", "Cobre refinado para cabos e eletrônicos.", CategoriaRecursoIndustrial.Refinado, "t", 240, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Duraluminio, "Duralumínio", "Liga leve para veículos e aeronaves.", CategoriaRecursoIndustrial.Refinado, "t", 320, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.LigaTitanio, "Liga de titânio", "Liga estratégica de alta resistência.", CategoriaRecursoIndustrial.Estrategico, "t", 720, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.ComponentesEletronicos, "Componentes eletrônicos", "Guiagem, sensores e automação.", CategoriaRecursoIndustrial.Componente, "unidades", 980, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.UranioEnriquecido, "Urânio enriquecido", "Carga estratégica nuclear abstrata.", CategoriaRecursoIndustrial.MilitarFuturo, "cargas", 5200, RaridadeRecursoIndustrial.Estrategico, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioLitio, "Minério de lítio", "Base para células de lítio e armazenamento moderno.", CategoriaRecursoIndustrial.MateriaPrima, "t", 220, RaridadeRecursoIndustrial.Raro, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.TerrasRaras, "Terras raras", "Sensores, eletrônicos e guiagem de precisão.", CategoriaRecursoIndustrial.Estrategico, "t", 420, RaridadeRecursoIndustrial.MuitoRaro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioNiquel, "Minério de níquel", "Liga e aço especial.", CategoriaRecursoIndustrial.MateriaPrima, "t", 160, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MinerioManganes, "Minério de manganês", "Refino de aço especial e blindagem.", CategoriaRecursoIndustrial.MateriaPrima, "t", 150, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Silica, "Sílica", "Vidro industrial, eletrônicos e componentes.", CategoriaRecursoIndustrial.MateriaPrima, "t", 90, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Calcario, "Calcário", "Base para cimento e infraestrutura pesada.", CategoriaRecursoIndustrial.MateriaPrima, "t", 70, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.AreiaIndustrial, "Areia industrial", "Entrada para vidro industrial.", CategoriaRecursoIndustrial.MateriaPrima, "t", 65, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Fosfato, "Fosfato", "Base de fertilizantes e agroindústria.", CategoriaRecursoIndustrial.MateriaPrima, "t", 85, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CarvaoMineral, "Carvão mineral", "Energia térmica e metalurgia pesada.", CategoriaRecursoIndustrial.MateriaPrima, "t", 100, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.PetroleoBruto, "Petróleo bruto", "Base energética e química industrial.", CategoriaRecursoIndustrial.Estrategico, "barris", 180, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.GasNatural, "Gás natural", "Energia industrial e fertilizante.", CategoriaRecursoIndustrial.Estrategico, "m3", 170, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.AluminioIndustrial, "Alumínio industrial", "Material leve para construção e aeronáutica.", CategoriaRecursoIndustrial.Refinado, "t", 210, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.AcoEspecial, "Aço especial", "Aço reforçado para blindagem e motores pesados.", CategoriaRecursoIndustrial.Estrategico, "t", 430, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CelulasLitio, "Células de lítio", "Armazenamento industrial e baterias.", CategoriaRecursoIndustrial.Refinado, "t", 360, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.VidroIndustrial, "Vidro industrial", "Construção, visão e sensores.", CategoriaRecursoIndustrial.Refinado, "t", 120, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Cimento, "Cimento", "Base de construção civil e fortificações.", CategoriaRecursoIndustrial.Refinado, "t", 80, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Fertilizante, "Fertilizante", "Aumenta produtividade agrícola e agroindustrial.", CategoriaRecursoIndustrial.Refinado, "t", 150, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.PlasticoIndustrial, "Plástico industrial", "Química leve para cabos, carenagens e logística.", CategoriaRecursoIndustrial.Refinado, "t", 160, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BorrachaSintetica, "Borracha sintética", "Pneus, vedação e isolamento.", CategoriaRecursoIndustrial.Refinado, "t", 170, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CabosEletricos, "Cabos elétricos", "Transmissão, energia e comunicação.", CategoriaRecursoIndustrial.Componente, "t", 220, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CircuitosEletronicos, "Circuitos eletrônicos", "Base de controle e automação.", CategoriaRecursoIndustrial.Componente, "unidades", 500, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Sensores, "Sensores", "Leitura tática, navegação e radar.", CategoriaRecursoIndustrial.Componente, "unidades", 650, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BateriaIndustrial, "Bateria industrial", "Armazenamento para veículos e drones.", CategoriaRecursoIndustrial.Componente, "unidades", 720, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BateriaAltaCapacidade, "Bateria de alta capacidade", "Autonomia ampliada para unidades avançadas.", CategoriaRecursoIndustrial.Componente, "unidades", 980, RaridadeRecursoIndustrial.Estrategico, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MotorEletrico, "Motor elétrico", "Propulsão leve para veículos e drones.", CategoriaRecursoIndustrial.Componente, "unidades", 430, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MotorCombustao, "Motor a combustão", "Propulsão base para veículos terrestres.", CategoriaRecursoIndustrial.Componente, "unidades", 400, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MotorDiesel, "Motor diesel", "Propulsão pesada para logística e blindados.", CategoriaRecursoIndustrial.Componente, "unidades", 460, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MotorNaval, "Motor naval", "Propulsão marítima e logística costeira.", CategoriaRecursoIndustrial.Componente, "unidades", 540, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.TurbinaAerea, "Turbina aérea", "Propulsão de aeronaves e plataformas de caça.", CategoriaRecursoIndustrial.Componente, "unidades", 900, RaridadeRecursoIndustrial.Estrategico, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.TurbinaNaval, "Turbina naval", "Propulsão avançada para navios pesados.", CategoriaRecursoIndustrial.Componente, "unidades", 980, RaridadeRecursoIndustrial.Estrategico, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.PneusIndustriais, "Pneus industriais", "Mobilidade terrestre e veículos pesados.", CategoriaRecursoIndustrial.Componente, "unidades", 180, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.SistemaHidraulico, "Sistema hidráulico", "Movimento, braços e torres pesadas.", CategoriaRecursoIndustrial.Componente, "unidades", 260, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.ChassiLeve, "Chassi leve", "Base de veículos rápidos.", CategoriaRecursoIndustrial.Componente, "unidades", 300, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.ChassiPesado, "Chassi pesado", "Base de blindados e caminhões militares.", CategoriaRecursoIndustrial.Componente, "unidades", 600, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Esteiras, "Esteiras", "Mobilidade blindada e veículos pesados.", CategoriaRecursoIndustrial.Componente, "unidades", 240, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BlindagemLeve, "Blindagem leve", "Proteção para veículos rápidos.", CategoriaRecursoIndustrial.Componente, "unidades", 320, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BlindagemMedia, "Blindagem média", "Proteção para blindados de linha.", CategoriaRecursoIndustrial.Componente, "unidades", 500, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.BlindagemPesada, "Blindagem pesada", "Proteção para unidades de ponta.", CategoriaRecursoIndustrial.Componente, "unidades", 750, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Avionicos, "Aviônicos", "Controle de voo e navegação aérea.", CategoriaRecursoIndustrial.Componente, "unidades", 850, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Radar, "Radar", "Detecção e defesa aérea/naval.", CategoriaRecursoIndustrial.Componente, "unidades", 760, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Sonar, "Sonar", "Detecção submarina e marinha.", CategoriaRecursoIndustrial.Componente, "unidades", 640, RaridadeRecursoIndustrial.Raro, true));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.ModuloComunicacao, "Módulo de comunicação", "Telemetria e comando.", CategoriaRecursoIndustrial.Componente, "unidades", 300, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.ModuloNavegacao, "Módulo de navegação", "Rotas e precisão tática.", CategoriaRecursoIndustrial.Componente, "unidades", 280, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.EquipamentoLogistico, "Equipamento logístico", "Apoio, transporte e carga.", CategoriaRecursoIndustrial.Componente, "unidades", 240, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.MaquinasIndustriais, "Máquinas industriais", "Linha de produção e manufatura pesada.", CategoriaRecursoIndustrial.Componente, "unidades", 500, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.GuindasteIndustrial, "Guindaste industrial", "Construção pesada e estaleiros.", CategoriaRecursoIndustrial.Componente, "unidades", 620, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Etanol, "Etanol", "Combustível renovável para logística leve.", CategoriaRecursoIndustrial.Refinado, "l", 120, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Biodiesel, "Biodiesel", "Combustível renovável para frotas.", CategoriaRecursoIndustrial.Refinado, "l", 135, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Biogas, "Biogás", "Gás de origem biológica e industrial.", CategoriaRecursoIndustrial.Refinado, "m3", 90, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Gasolina, "Gasolina", "Combustível leve para veículos.", CategoriaRecursoIndustrial.Refinado, "l", 180, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.Diesel, "Diesel", "Combustível pesado para frota e blindados.", CategoriaRecursoIndustrial.Refinado, "l", 185, RaridadeRecursoIndustrial.Comum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CombustivelAviacao, "Combustível de aviação", "Combustível refinado para aeronaves.", CategoriaRecursoIndustrial.Refinado, "l", 240, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.CombustivelNaval, "Combustível naval", "Combustível para rotas marítimas.", CategoriaRecursoIndustrial.Refinado, "l", 220, RaridadeRecursoIndustrial.Incomum, false));
            recursosCatalogo.Add(CriarRecursoPadrao(IndustriaIds.LubrificanteIndustrial, "Lubrificante industrial", "Base para motores e linhas pesadas.", CategoriaRecursoIndustrial.Refinado, "l", 110, RaridadeRecursoIndustrial.Comum, false));
        }

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.AcoEstrutural, "Aço estrutural", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.MinerioFerro, quantidade = 1000d }
        }, 500, 120, 2, 750d, "pesquisa_metalurgia", 1, false, false));

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.CobreEletrolitico, "Cobre eletrolítico", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.MinerioCobre, quantidade = 1000d }
        }, 650, 140, 3, 700d, "pesquisa_metalurgia", 1, false, false));

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.Duraluminio, "Duralumínio", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.Bauxita, quantidade = 1000d }
        }, 1200, 240, 4, 550d, "pesquisa_metalurgia", 2, false, false));

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.LigaTitanio, "Liga de titânio", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.MinerioTitanio, quantidade = 1000d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.AcoEstrutural, quantidade = 300d }
        }, 3500, 500, 6, 450d, "pesquisa_metalurgia", 3, false, true));

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.ComponentesEletronicos, "Componentes eletrônicos", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.CobreEletrolitico, quantidade = 300d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.Duraluminio, quantidade = 200d }
        }, 2500, 350, 5, 100d, "pesquisa_eletronica", 3, false, true));

        GarantirReceitaCatalogo(CriarReceitaPadrao(IndustriaIds.UranioEnriquecido, "Urânio enriquecido", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.UranioBruto, quantidade = 1000d }
        }, 25000, 2500, 30, 1d, "pesquisa_nuclear", 4, true, true));

        GarantirReceitaCatalogo(CriarReceitaPadrao("aco_estrutural_lote_pesado", "Aço estrutural - lote pesado", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.MinerioFerro, quantidade = 1500d }
        }, 900, 210, 3, 900d, "pesquisa_extracao_ferro", 2, false, false, IndustriaIds.AcoEstrutural));

        GarantirReceitaCatalogo(CriarReceitaPadrao("duraluminio_aeroespacial", "Duralumínio aeroespacial", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.Bauxita, quantidade = 1200d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.CobreEletrolitico, quantidade = 180d }
        }, 1800, 320, 5, 600d, "pesquisa_aeroespacial_1", 3, false, true, IndustriaIds.Duraluminio));

        GarantirReceitaCatalogo(CriarReceitaPadrao("liga_titanio_blindada", "Liga de titânio blindada", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.MinerioTitanio, quantidade = 900d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.AcoEstrutural, quantidade = 450d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.ComponentesEletronicos, quantidade = 60d }
        }, 4200, 620, 7, 420d, "pesquisa_aeroespacial_2", 3, false, true, IndustriaIds.LigaTitanio));

        GarantirReceitaCatalogo(CriarReceitaPadrao("componentes_eletronicos_militares", "Componentes eletrônicos militares", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.CobreEletrolitico, quantidade = 500d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.Duraluminio, quantidade = 260d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.LigaTitanio, quantidade = 80d }
        }, 3800, 480, 6, 180d, "pesquisa_missil_guiado", 3, false, true, IndustriaIds.ComponentesEletronicos));

        GarantirReceitaCatalogo(CriarReceitaPadrao("uranio_enriquecido_controlado", "Urânio enriquecido controlado", new[]
        {
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.UranioBruto, quantidade = 1200d },
            new MaterialNecessarioIndustrial { recursoId = IndustriaIds.ComponentesEletronicos, quantidade = 120d }
        }, 30000, 2800, 24, 2d, "pesquisa_icnu", 4, true, true, IndustriaIds.UranioEnriquecido));

        if (configuracoesExtracao.Count == 0)
        {
            configuracoesExtracao.Add(CriarConfiguracaoPadrao(IndustriaIds.MinerioFerro, "Minério de ferro", 400f, 50f, 500f, 10000f, false, string.Empty));
            configuracoesExtracao.Add(CriarConfiguracaoPadrao(IndustriaIds.MinerioCobre, "Minério de cobre", 650f, 70f, 500f, 10000f, false, string.Empty));
            configuracoesExtracao.Add(CriarConfiguracaoPadrao(IndustriaIds.Bauxita, "Bauxita", 850f, 90f, 500f, 10000f, false, string.Empty));
            configuracoesExtracao.Add(CriarConfiguracaoPadrao(IndustriaIds.MinerioTitanio, "Minério de titânio", 2000f, 180f, 500f, 10000f, false, string.Empty));
            configuracoesExtracao.Add(CriarConfiguracaoPadrao(IndustriaIds.UranioBruto, "Urânio bruto", 5000f, 350f, 500f, 2500f, true, "Exige autorização nuclear."));
        }
    }

    private static RecursoIndustrialSO CriarRecursoPadrao(string id, string nome, string descricao, CategoriaRecursoIndustrial categoria, string unidade, int precoBase, RaridadeRecursoIndustrial raridade, bool estrategico)
    {
        RecursoIndustrialSO recurso = ScriptableObject.CreateInstance<RecursoIndustrialSO>();
        recurso.hideFlags = HideFlags.HideAndDontSave;
        recurso.id = id;
        recurso.nome = nome;
        recurso.descricao = descricao;
        recurso.categoria = categoria;
        recurso.unidade = unidade;
        recurso.precoBase = precoBase;
        recurso.raridade = raridade;
        recurso.estrategico = estrategico;
        recurso.podeComprar = true;
        recurso.podeVender = true;
        return recurso;
    }

    private void GarantirReceitaCatalogo(ReceitaIndustrialSO receita)
    {
        if (receita == null)
        {
            return;
        }

        bool existe = receitasCatalogo.Any(r => r != null && string.Equals(r.id, receita.id, StringComparison.OrdinalIgnoreCase));
        if (!existe)
        {
            receitasCatalogo.Add(receita);
        }
    }

    private static ReceitaIndustrialSO CriarReceitaPadrao(string id, string nome, MaterialNecessarioIndustrial[] materiais, int dinheiro, int energia, int dias, double quantidadeProduzida, string pesquisa, int nivelIndustrial, bool nuclear, bool estrategico, string produtoFinalId = null)
    {
        ReceitaIndustrialSO receita = ScriptableObject.CreateInstance<ReceitaIndustrialSO>();
        receita.hideFlags = HideFlags.HideAndDontSave;
        receita.id = id;
        receita.nome = nome;
        receita.produtoFinalId = string.IsNullOrWhiteSpace(produtoFinalId) ? id : produtoFinalId;
        receita.quantidadeProduzida = quantidadeProduzida;
        receita.dinheiroNecessario = dinheiro;
        receita.energiaNecessaria = energia;
        receita.diasNecessarios = dias;
        receita.pesquisaExigida = pesquisa;
        receita.nivelIndustrialExigido = nivelIndustrial;
        receita.requerLaboratorioNuclear = nuclear;
        receita.materialEstrategico = estrategico;
        receita.materiaisNecessarios = materiais != null ? new List<MaterialNecessarioIndustrial>(materiais) : new List<MaterialNecessarioIndustrial>();
        return receita;
    }

    private static ConfiguracaoExtracaoIndustrial CriarConfiguracaoPadrao(string recursoId, string nomeRecurso, float custoDinheiro, float custoEnergia, float producaoMinima, float producaoMaxima, bool exigeAutorizacao, string descricaoBloqueio)
    {
        return new ConfiguracaoExtracaoIndustrial
        {
            recursoId = recursoId,
            nomeRecurso = nomeRecurso,
            custoDinheiro = custoDinheiro,
            custoEnergia = custoEnergia,
            producaoMinima = producaoMinima,
            producaoMaxima = producaoMaxima,
            limiteUranioPaisNormal = 2500f,
            limiteUranioGrandeProdutor = 10000f,
            exigeAutorizacao = exigeAutorizacao,
            descricaoBloqueio = descricaoBloqueio
        };
    }

    private SavePerfilMineralIndustrial ConvertParaSavePerfil(PerfilMineralPais perfil)
    {
        if (perfil == null)
        {
            return null;
        }

        return new SavePerfilMineralIndustrial
        {
            teamId = perfil.teamId,
            perfilGerado = perfil.perfilGerado,
            ferro = (int)perfil.ferro,
            cobre = (int)perfil.cobre,
            bauxita = (int)perfil.bauxita,
            titanio = (int)perfil.titanio,
            uranio = (int)perfil.uranio,
            modificadorIndustrial = perfil.modificadorIndustrial,
            extraindoFerro = perfil.extraindoFerro,
            extraindoCobre = perfil.extraindoCobre,
            extraindoBauxita = perfil.extraindoBauxita,
            extraindoTitanio = perfil.extraindoTitanio,
            extraindoUranio = perfil.extraindoUranio,
            refinandoAco = perfil.refinandoAco,
            refinandoCobreEletrolitico = perfil.refinandoCobreEletrolitico,
            refinandoDuraluminio = perfil.refinandoDuraluminio,
            refinandoLigaTitanio = perfil.refinandoLigaTitanio,
            refinandoComponentes = perfil.refinandoComponentes,
            refinandoUranioEnriquecido = perfil.refinandoUranioEnriquecido
        };
    }

    private SaveOrdemExtracaoIndustrial ConvertParaSaveOrdemExtracao(OrdemExtracaoIndustrial ordem)
    {
        if (ordem == null)
        {
            return null;
        }

        return new SaveOrdemExtracaoIndustrial
        {
            id = ordem.id,
            teamId = ordem.teamId,
            recursoId = ordem.recursoId,
            nomeRecurso = ordem.nomeRecurso,
            estado = ordem.estado.ToString(),
            continua = ordem.continua,
            diasObjetivo = ordem.diasObjetivo,
            diasRestantes = ordem.diasRestantes,
            quantidadeAlvo = ordem.quantidadeAlvo,
            quantidadeRestante = ordem.quantidadeRestante,
            estoqueAlvo = ordem.estoqueAlvo,
            totalProduzido = ordem.totalProduzido,
            custoDinheiro = ordem.custoDinheiro,
            custoEnergia = ordem.custoEnergia,
            producaoBase = ordem.producaoBase,
            producaoUltimoDia = ordem.producaoUltimoDia,
            exigeAutorizacao = ordem.exigeAutorizacao,
            autorizada = ordem.autorizada,
            motivoBloqueio = ordem.motivoBloqueio,
            ultimaDataProcessada = ordem.ultimaDataProcessada
        };
    }

    private SaveOrdemRefinoIndustrial ConvertParaSaveOrdemRefino(OrdemRefinoIndustrial ordem)
    {
        if (ordem == null)
        {
            return null;
        }

        return new SaveOrdemRefinoIndustrial
        {
            id = ordem.id,
            teamId = ordem.teamId,
            receitaId = ordem.receitaId,
            produtoId = ordem.produtoId,
            estado = ordem.estado.ToString(),
            linhaId = ordem.linhaId,
            progresso = ordem.progresso,
            diasTotais = ordem.diasTotais,
            diasRestantes = ordem.diasRestantes,
            quantidadeEntrada = ordem.quantidadeEntrada,
            quantidadeProduzida = ordem.quantidadeProduzida,
            dinheiroReservado = ordem.dinheiroReservado,
            energiaReservada = ordem.energiaReservada,
            materiaisReservados = ordem.materiaisReservados != null ? new List<QuantidadeRecursoIndustrial>(ordem.materiaisReservados) : new List<QuantidadeRecursoIndustrial>(),
            inicioDia = ordem.inicioDia,
            ultimaDataProcessada = ordem.ultimaDataProcessada,
            pesquisaExigida = ordem.pesquisaExigida,
            nivelIndustrialExigido = ordem.nivelIndustrialExigido
        };
    }

    private SaveLinhaIndustrial ConvertParaSaveLinha(LinhaIndustrial linha)
    {
        if (linha == null)
        {
            return null;
        }

        return new SaveLinhaIndustrial
        {
            id = linha.id,
            teamId = linha.teamId,
            indice = linha.indice,
            estado = linha.estado.ToString(),
            ordemRefinoId = linha.ordemRefinoId,
            receitaId = linha.receitaId,
            progresso = linha.progresso,
            diasTotais = linha.diasTotais,
            diasRestantes = linha.diasRestantes,
            motivoBloqueio = linha.motivoBloqueio
        };
    }

    private static EstadoOrdemExtracaoIndustrial ParseEstadoExtracao(string estado)
    {
        return Enum.TryParse(estado, out EstadoOrdemExtracaoIndustrial parsed) ? parsed : EstadoOrdemExtracaoIndustrial.Aguardando;
    }

    private static EstadoOrdemRefinoIndustrial ParseEstadoRefino(string estado)
    {
        return Enum.TryParse(estado, out EstadoOrdemRefinoIndustrial parsed) ? parsed : EstadoOrdemRefinoIndustrial.Aguardando;
    }

    private static EstadoLinhaIndustrial ParseEstadoLinha(string estado)
    {
        return Enum.TryParse(estado, out EstadoLinhaIndustrial parsed) ? parsed : EstadoLinhaIndustrial.Livre;
    }
}
