using System;
using System.Collections.Generic;
using UnityEngine;

public class SistemaEconomiaImoveis : MonoBehaviour
{
    public static SistemaEconomiaImoveis Instancia { get; private set; }

    private static readonly HashSet<EstruturaEconomica> EstruturasRegistradas = new HashSet<EstruturaEconomica>();

    [Header("Tick")]
    public float intervaloLeitura = 1.5f;
    public bool usarTagsEconomicas = false;
    public bool usarCompatibilidadeImovel = true;
    public float intervaloVarreduraTags = 20f;

    [Header("Balanceamento")]
    public float comidaConsumidaPorPopulacao = 0.02f;
    public float petroleoConsumidoPorIndustria = 0.18f;
    public float rendaPorPopulacao = 0.08f;

    // Fatores médios de referência. CO2 segue o fator de carvão da EIA
    // (2,31 lb/kWh em 2023, aproximadamente 1,05 kg/kWh). SO2, NOx e PM
    // são estimativas de referência baseadas nas tabelas AP-42 da EPA.
    private const float HorasPorDia = 24f;
    private const float Co2KgPorMWh = 1050f;
    private const float So2KgPorMWh = 5.5f;
    private const float NoxKgPorMWh = 4.5f;
    private const float ParticulasKgPorMWh = 0.35f;

    public event Action OnEconomiaImoveisAtualizada;

    private readonly Dictionary<int, DadosEconomiaPais> economias = new Dictionary<int, DadosEconomiaPais>();
    private readonly List<Imovel> imoveisBuffer = new List<Imovel>();
    private readonly List<EstruturaEconomica> consumidoresBuffer = new List<EstruturaEconomica>();
    private readonly List<Imovel> consumidoresImoveisBuffer = new List<Imovel>();
    private readonly List<GerenciadorAeroporto> aeroportosBuffer = new List<GerenciadorAeroporto>();
    private readonly List<GerenciadorAeroporto> consumidoresAeroportosBuffer = new List<GerenciadorAeroporto>();
    private readonly HashSet<int> objetosContados = new HashSet<int>();
    private float proximoTick;
    private float proximaVarreduraTags;
    private float ultimoRecalculo = -999f;

    public IReadOnlyDictionary<int, DadosEconomiaPais> Economias { get { return economias; } }

    public static void Register(EstruturaEconomica estrutura)
    {
        if (estrutura != null) EstruturasRegistradas.Add(estrutura);
    }

    public static void Unregister(EstruturaEconomica estrutura)
    {
        if (estrutura != null) EstruturasRegistradas.Remove(estrutura);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        // Fazendas e estruturas modernas se registram por EstruturaEconomica; varrer tags em cena grande
        // causa travadas e ainda dispara erros quando tags antigas nao existem no projeto.
        usarTagsEconomicas = false;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < proximoTick) return;
        proximoTick = Time.unscaledTime + Mathf.Max(1f, intervaloLeitura);
        Recalcular();
    }

    public DadosEconomiaPais ObterEconomia(int teamId)
    {
        DadosEconomiaPais economia;
        return economias.TryGetValue(teamId, out economia) ? economia : null;
    }

    public void Recalcular()
    {
        if (Time.unscaledTime - ultimoRecalculo < 0.25f) return;
        ultimoRecalculo = Time.unscaledTime;
        economias.Clear();
        objetosContados.Clear();
        consumidoresBuffer.Clear();
        consumidoresImoveisBuffer.Clear();
        consumidoresAeroportosBuffer.Clear();

        foreach (EstruturaEconomica estrutura in EstruturasRegistradas)
        {
            if (estrutura == null) continue;
            ContabilizarEstrutura(estrutura);
        }

        if (usarTagsEconomicas && Time.unscaledTime >= proximaVarreduraTags)
        {
            proximaVarreduraTags = Time.unscaledTime + Mathf.Max(5f, intervaloVarreduraTags);
            LerTagsEconomicas();
        }

        if (usarCompatibilidadeImovel)
        {
            LerImoveisAntigos();
            LerAeroportos();
        }

        DistribuirEnergia();
        FinalizarSnapshots();
        OnEconomiaImoveisAtualizada?.Invoke();
    }

    private void DistribuirEnergia()
    {
        foreach (var par in economias)
        {
            int teamId = par.Key;
            DadosEconomiaPais economia = par.Value;
            float energiaDisponivel = economia.energiaProduzida;

            // Processa estruturas modernas
            for (int i = 0; i < consumidoresBuffer.Count; i++)
            {
                EstruturaEconomica est = consumidoresBuffer[i];
                if (est == null || est.teamId != teamId) continue;
                if (est.energiaConsumida <= 0) continue;
                if (energiaDisponivel >= est.energiaConsumida)
                {
                    energiaDisponivel -= est.energiaConsumida;
                    if (est.status == StatusEstruturaEconomica.SemEnergia) est.status = StatusEstruturaEconomica.Ativa;
                    est.eficiencia = 1f;
                }
                else if (energiaDisponivel > 0)
                {
                    est.eficiencia = energiaDisponivel / est.energiaConsumida; // Falta parcial de energia
                    energiaDisponivel = 0;
                    if (est.status == StatusEstruturaEconomica.SemEnergia) est.status = StatusEstruturaEconomica.Ativa;
                }
                else
                {
                    est.eficiencia = 0f;
                    est.status = StatusEstruturaEconomica.SemEnergia;
                    economia.estruturasSemEnergia++;
                }
            }

            // Processa imoveis antigos
            for (int i = 0; i < consumidoresImoveisBuffer.Count; i++)
            {
                Imovel imovel = consumidoresImoveisBuffer[i];
                if (imovel == null || ResolverTeamId(imovel.gameObject) != teamId) continue;
                float baseConsumo = Mathf.Max(0.5f, imovel.MoradoresAtuais * 0.05f);
                float consumo = baseConsumo * 1.5f; 

                if (energiaDisponivel >= consumo)
                {
                    energiaDisponivel -= consumo;
                    imovel.SetarSemEnergia(false); 
                }
                else
                {
                    imovel.SetarSemEnergia(true);
                    economia.estruturasSemEnergia++;
                }
            }

            // Processa aeroportos
            for (int i = 0; i < consumidoresAeroportosBuffer.Count; i++)
            {
                GerenciadorAeroporto aeroporto = consumidoresAeroportosBuffer[i];
                if (aeroporto == null || ResolverTeamId(aeroporto.gameObject) != teamId) continue;
                
                float baseConsumo = 15.0f;
                int totalAvioes = aeroporto.avioesNoPatio.Count + aeroporto.avioesNoHangar.Count;
                int totalHelis = aeroporto.helicopterosDoAeroporto.Count;
                int totalHeavy = aeroporto.transportesC700NoPatio.Count;
                float consumo = baseConsumo + (totalAvioes * 2.0f) + (totalHelis * 1.5f) + (totalHeavy * 5.0f);

                if (energiaDisponivel >= consumo)
                {
                    energiaDisponivel -= consumo;
                    aeroporto.SetarSemEnergia(false);
                }
                else
                {
                    aeroporto.SetarSemEnergia(true);
                    economia.estruturasSemEnergia++;
                }
            }
        }
    }

    private void LerTagsEconomicas()
    {
        LerTag("Casa", TipoEstruturaEconomica.Casa);
        LerTag("Industria", TipoEstruturaEconomica.Industria);
        LerTag("Petroleo", TipoEstruturaEconomica.Petroleo);
        LerTag("Comercio", TipoEstruturaEconomica.Comercio);
        LerTag("Farm", TipoEstruturaEconomica.Farm);
        LerTag("Energia", TipoEstruturaEconomica.Energia);
        LerTag("imovel", TipoEstruturaEconomica.Casa);
        LerTag("PesquisaMilitar", TipoEstruturaEconomica.PesquisaMilitar);
        LerTag("UsinaSolar", TipoEstruturaEconomica.UsinaSolar);
        LerTag("UsinaCarvao", TipoEstruturaEconomica.UsinaCarvao);
    }

    private void LerTag(string tagName, TipoEstruturaEconomica tipo)
    {
        GameObject[] encontrados;
        try
        {
            encontrados = GameObject.FindGameObjectsWithTag(tagName);
        }
        catch (UnityException)
        {
            return;
        }

        for (int i = 0; i < encontrados.Length; i++)
        {
            GameObject go = encontrados[i];
            if (go == null || objetosContados.Contains(go.GetInstanceID())) continue;
            EstruturaEconomica estrutura = go.GetComponent<EstruturaEconomica>();
            if (estrutura != null)
            {
                ContabilizarEstrutura(estrutura);
            }
            else
            {
                ContabilizarFallback(go, tipo);
            }
        }
    }

    private void LerImoveisAntigos()
    {
        RegistroEntidadesJogo.FillImoveis(imoveisBuffer);
        for (int i = 0; i < imoveisBuffer.Count; i++)
        {
            Imovel imovel = imoveisBuffer[i];
            if (imovel == null || objetosContados.Contains(imovel.gameObject.GetInstanceID())) continue;
            if (imovel.GetComponent<EstruturaEconomica>() != null) continue;
            ContabilizarImovel(imovel);
        }
    }

    private void LerAeroportos()
    {
        RegistroEntidadesJogo.FillAeroportos(aeroportosBuffer);
        for (int i = 0; i < aeroportosBuffer.Count; i++)
        {
            GerenciadorAeroporto aeroporto = aeroportosBuffer[i];
            if (aeroporto == null || objetosContados.Contains(aeroporto.gameObject.GetInstanceID())) continue;
            if (aeroporto.GetComponent<EstruturaEconomica>() != null) continue;
            if (aeroporto is GerenciadorPortaAvioes) continue;
            ContabilizarAeroporto(aeroporto);
        }
    }

    private void ContabilizarAeroporto(GerenciadorAeroporto aeroporto)
    {
        objetosContados.Add(aeroporto.gameObject.GetInstanceID());
        consumidoresAeroportosBuffer.Add(aeroporto);
        int teamId = ResolverTeamId(aeroporto.gameObject);
        DadosEconomiaPais economia = ObterOuCriar(teamId);
        economia.estruturasContadas++;

        float baseConsumo = 15.0f;
        int totalAvioes = aeroporto.avioesNoPatio.Count + aeroporto.avioesNoHangar.Count;
        int totalHelis = aeroporto.helicopterosDoAeroporto.Count;
        int totalHeavy = aeroporto.transportesC700NoPatio.Count;
        float consumo = baseConsumo + (totalAvioes * 2.0f) + (totalHelis * 1.5f) + (totalHeavy * 5.0f);

        economia.energiaConsumida += consumo;
        economia.empregosDisponiveis += 150; // Aeroportos geram muitos empregos

    }

    private void ContabilizarEstrutura(EstruturaEconomica estrutura)
    {
        if (estrutura == null || !estrutura.isActiveAndEnabled) return;
        objetosContados.Add(estrutura.gameObject.GetInstanceID());
        if (estrutura.teamId <= 0) estrutura.InferirTeamId();
        
        if (estrutura.energiaConsumida > 0)
        {
            consumidoresBuffer.Add(estrutura);
        }

        estrutura.AplicarPadraoPorTipo();

        float eficiencia = estrutura.Ativa ? Mathf.Clamp01(estrutura.eficiencia) : 0f;
        DadosEconomiaPais economia = ObterOuCriar(estrutura.teamId);
        economia.estruturasContadas++;
        SomarTipo(economia, estrutura.tipo);
        economia.moradiaTotal += estrutura.capacidadePopulacional;
        economia.populacaoTotal += estrutura.populacaoAtual;
        economia.empregosDisponiveis += estrutura.empregosGerados;
        economia.comidaProduzida += estrutura.comidaProduzida * eficiencia;
        economia.petroleoProduzido += estrutura.petroleoProduzido * eficiencia;
            economia.industriaProduzida += estrutura.industriaProduzida * eficiencia;
            economia.energiaProduzida += estrutura.energiaProduzida * eficiencia;
            if (estrutura.tipo == TipoEstruturaEconomica.UsinaSolar)
            {
                economia.usinasSolares++;
                economia.energiaSolarProduzida += estrutura.energiaProduzida * eficiencia;
            }
            else if (estrutura.tipo == TipoEstruturaEconomica.UsinaCarvao)
            {
                economia.usinasCarvao++;
                economia.energiaCarvaoProduzida += estrutura.energiaProduzida * eficiencia;
                economia.custoUsinasCarvaoPorDia += Mathf.Abs(estrutura.dinheiroGerado * eficiencia) * GerenciadorTempoSegundosPorDia();
            }
        economia.energiaConsumida += estrutura.energiaConsumida;
        economia.combustivelConsumido += estrutura.combustivelConsumido;
        economia.militaresNecessarios += estrutura.militaresNecessarios;
        RegistrarFluxoEconomico(economia, estrutura.tipo, estrutura.dinheiroGerado * eficiencia);
        float manutencaoExtra = 0f;
        switch (estrutura.tipo)
        {
            case TipoEstruturaEconomica.Farm:
                manutencaoExtra = Mathf.Max(0.45f, estrutura.comidaProduzida * 0.12f + estrutura.energiaConsumida * 0.08f);
                break;
            case TipoEstruturaEconomica.Energia:
                manutencaoExtra = Mathf.Max(0.35f, estrutura.energiaProduzida * 0.10f);
                break;
            case TipoEstruturaEconomica.UsinaSolar:
                manutencaoExtra = Mathf.Max(0.25f, estrutura.energiaProduzida * 0.06f);
                break;
            case TipoEstruturaEconomica.UsinaCarvao:
                manutencaoExtra = Mathf.Max(55f, estrutura.energiaProduzida * 0.22f);
                break;
        }
        if (manutencaoExtra > 0f)
        {
            RegistrarFluxoEconomico(economia, estrutura.tipo, -manutencaoExtra);
            if (estrutura.tipo == TipoEstruturaEconomica.UsinaCarvao)
            {
                economia.custoUsinasCarvaoPorDia += manutencaoExtra * GerenciadorTempoSegundosPorDia();
            }
        }
        RegistrarFluxoEconomico(economia, TipoEstruturaEconomica.Casa, estrutura.populacaoAtual * rendaPorPopulacao);
        economia.eficienciaMedia += eficiencia;
    }

    private void ContabilizarImovel(Imovel imovel)
    {
        objetosContados.Add(imovel.gameObject.GetInstanceID());
        consumidoresImoveisBuffer.Add(imovel);
        int teamId = ResolverTeamId(imovel.gameObject);
        DadosEconomiaPais economia = ObterOuCriar(teamId);
        economia.estruturasContadas++;
        economia.casas++;
        economia.moradiaTotal += imovel.Capacidade;
        economia.populacaoTotal += imovel.MoradoresAtuais;
        RegistrarFluxoEconomico(economia, TipoEstruturaEconomica.Casa, imovel.RendaAtual);
        
        // Casas gastam mais energia ainda (conforme pedido pelo usuario)
        float baseConsumo = Mathf.Max(0.5f, imovel.MoradoresAtuais * 0.05f);
        economia.energiaConsumida += baseConsumo * 1.5f; 
        
        economia.eficienciaMedia += Mathf.Clamp01(imovel.QualidadeAtual / 100f);

        PredioRecursos predio = imovel.GetComponent<PredioRecursos>();
        if (predio != null)
        {
            SomarPredioRecursos(economia, predio);
        }
    }

    private void ContabilizarFallback(GameObject go, TipoEstruturaEconomica tipo)
    {
        objetosContados.Add(go.GetInstanceID());
        DadosEconomiaPais economia = ObterOuCriar(ResolverTeamId(go));
        economia.estruturasContadas++;
        SomarTipo(economia, tipo);

        switch (tipo)
        {
            case TipoEstruturaEconomica.Casa:
                economia.moradiaTotal += 10;
                economia.populacaoTotal += 5;
                RegistrarFluxoEconomico(economia, tipo, 1f);
                economia.energiaConsumida += 0.3f;
                break;
            case TipoEstruturaEconomica.Industria:
                economia.empregosDisponiveis += 24;
                economia.industriaProduzida += 5f;
                RegistrarFluxoEconomico(economia, tipo, 7f);
                economia.energiaConsumida += 3f;
                break;
            case TipoEstruturaEconomica.Petroleo:
                economia.empregosDisponiveis += 12;
                economia.petroleoProduzido += 4f;
                RegistrarFluxoEconomico(economia, tipo, 5f);
                economia.energiaConsumida += 1.5f;
                break;
            case TipoEstruturaEconomica.Comercio:
                economia.empregosDisponiveis += 10;
                RegistrarFluxoEconomico(economia, tipo, 6f);
                economia.energiaConsumida += 1f;
                break;
            case TipoEstruturaEconomica.Farm:
                economia.empregosDisponiveis += 10;
                economia.comidaProduzida += 5f;
                RegistrarFluxoEconomico(economia, tipo, 3f);
                RegistrarFluxoEconomico(economia, tipo, -1.3f);
                economia.energiaConsumida += 0.7f;
                break;
            case TipoEstruturaEconomica.Energia:
                economia.empregosDisponiveis += 8;
                economia.energiaProduzida += 8f;
                RegistrarFluxoEconomico(economia, tipo, 2f);
                RegistrarFluxoEconomico(economia, tipo, -1.5f);
                break;
            case TipoEstruturaEconomica.PesquisaMilitar:
                economia.empregosDisponiveis += 15;
                economia.energiaConsumida += 8f; // Gasta muito mais energia
                RegistrarFluxoEconomico(economia, tipo, -5f); // Custo de manutencao
                break;
            case TipoEstruturaEconomica.UsinaSolar:
                economia.empregosDisponiveis += 4;
                economia.energiaProduzida += 8f;
                RegistrarFluxoEconomico(economia, tipo, -3f); // Custo de manutencao
                break;
            case TipoEstruturaEconomica.UsinaCarvao:
                economia.empregosDisponiveis += 30;
                economia.usinasCarvao++;
                economia.energiaCarvaoProduzida += 24f;
                economia.energiaProduzida += 24f;
                economia.custoUsinasCarvaoPorDia += 180f * GerenciadorTempoSegundosPorDia();
                RegistrarFluxoEconomico(economia, tipo, -180f);
                break;
        }

        PredioRecursos predio = go.GetComponent<PredioRecursos>();
        if (predio != null)
        {
            SomarPredioRecursos(economia, predio);
        }
    }

    private void SomarPredioRecursos(DadosEconomiaPais economia, PredioRecursos predio)
    {
        if (economia == null || predio == null || !predio.estaProduzindo) return;
        RegistrarFluxoEconomico(economia, ResolverTipoPredio(predio), predio.producaoDinheiro);
        economia.petroleoProduzido += predio.producaoPetroleo;
        economia.industriaProduzida += predio.producaoAco;
        economia.energiaProduzida += predio.producaoEnergia;

        // Adiciona empregos de acordo com o tipo
        TipoEstruturaEconomica tipo = ResolverTipoPredio(predio);
        switch (tipo)
        {
            case TipoEstruturaEconomica.Petroleo: economia.empregosDisponiveis += 12; break;
            case TipoEstruturaEconomica.Industria: economia.empregosDisponiveis += 24; break;
            case TipoEstruturaEconomica.Energia: economia.empregosDisponiveis += 8; break;
            case TipoEstruturaEconomica.Comercio: economia.empregosDisponiveis += 10; break;
            default: economia.empregosDisponiveis += 10; break;
        }
    }

    private void FinalizarSnapshots()
    {
        foreach (DadosEconomiaPais economia in economias.Values)
        {
            int populacaoReal = Mathf.Max(0, economia.populacaoTotal);
            if (SistemaGovernoMundial.Instancia != null
                && economia.teamId == SistemaGovernoMundial.Instancia.teamJogador
                && GerenciadorRecursos.Instancia != null)
            {
                populacaoReal = Mathf.Max(0, GerenciadorRecursos.Instancia.populacaoAtual);
            }

            economia.populacaoTotal = populacaoReal;
            economia.empregosOcupados = Mathf.Min(populacaoReal, economia.empregosDisponiveis);
            economia.deficitEmprego = Mathf.Max(0, populacaoReal - economia.empregosDisponiveis);
            economia.deficitEnergia = Mathf.Max(0f, economia.energiaConsumida - economia.energiaProduzida);
            economia.pressaoPopulacional = economia.moradiaTotal <= 0 ? 1f : Mathf.Clamp01(populacaoReal / (float)economia.moradiaTotal);
            economia.deficitPetroleo = Mathf.Max(0f, economia.industriaProduzida * petroleoConsumidoPorIndustria - economia.petroleoProduzido);
            economia.eficienciaMedia = economia.estruturasContadas > 0 ? Mathf.Clamp01(economia.eficienciaMedia / economia.estruturasContadas) : 1f;
            economia.exportacaoTotal = economia.comidaProduzida + economia.petroleoProduzido + economia.industriaProduzida;
            economia.importacaoTotal = economia.deficitEnergia + economia.deficitPetroleo;
            CalcularImpactoAmbiental(economia);
            economia.qualidadeVida = CalcularQualidadeVida(economia);

            // Uma partida nova sem estruturas não tem manutenção econômica
            // para cobrar. Os custos classificados abaixo só entram depois
            // que uma estrutura real foi registrada para esta nação.
            if (economia.estruturasContadas <= 0)
            {
                economia.custoSocial = 0f;
                economia.custoInfraestrutura = 0f;
                economia.custoMilitar = 0f;
                economia.custoProducao = 0f;
                economia.custoManutencao = 0f;
                economia.saldoOperacional = 0f;
                economia.dinheiroGerado = 0f;
                continue;
            }

            float custosClassificados = Mathf.Max(0f,
                economia.custoSocial + economia.custoInfraestrutura +
                economia.custoMilitar + economia.custoProducao);
            float custosNaoClassificados = Mathf.Max(0f, economia.custoManutencao - custosClassificados);
            float custoSocialBase = Mathf.Max(24f, populacaoReal * 0.018f);
            float custoInfraestruturaBase = Mathf.Max(2f, economia.energiaConsumida * 0.35f + economia.estruturasSemEnergia * 2.5f);
            float custoMilitarBase = Mathf.Max(2f, economia.empregosOcupados * 0.0015f);
            float custoProducaoBase = Mathf.Max(2f, economia.industriaProduzida * 0.02f);
            economia.custoSocial += custoSocialBase;
            economia.custoInfraestrutura += custoInfraestruturaBase;
            economia.custoMilitar += custoMilitarBase;
            economia.custoProducao += custoProducaoBase;
            economia.custoManutencao = custosNaoClassificados + economia.custoSocial + economia.custoInfraestrutura + economia.custoMilitar + economia.custoProducao;
            economia.saldoOperacional = economia.ReceitaBruta - economia.custoManutencao;
            economia.dinheiroGerado = economia.saldoOperacional;
        }
    }

    private void RegistrarFluxoEconomico(DadosEconomiaPais economia, TipoEstruturaEconomica tipo, float valor)
    {
        if (economia == null || Mathf.Approximately(valor, 0f)) return;

        if (valor < 0f)
        {
            float absValor = Mathf.Abs(valor);
            economia.custoManutencao += absValor;
            switch (tipo)
            {
                case TipoEstruturaEconomica.PesquisaMilitar:
                case TipoEstruturaEconomica.BaseMilitarPequena:
                case TipoEstruturaEconomica.BaseMilitarMedia:
                case TipoEstruturaEconomica.GrandeBaseMilitar:
                case TipoEstruturaEconomica.BaseAerea:
                case TipoEstruturaEconomica.BaseNaval:
                    economia.custoMilitar += absValor;
                    break;
                case TipoEstruturaEconomica.Energia:
                case TipoEstruturaEconomica.UsinaSolar:
                case TipoEstruturaEconomica.UsinaCarvao:
                case TipoEstruturaEconomica.UsinaTermicaPequena:
                case TipoEstruturaEconomica.UsinaTermicaGrande:
                case TipoEstruturaEconomica.UsinaNuclear:
                case TipoEstruturaEconomica.UsinaHidreletrica:
                case TipoEstruturaEconomica.AeroportoCivil:
                case TipoEstruturaEconomica.PortoComercial:
                    economia.custoInfraestrutura += absValor;
                    break;
                case TipoEstruturaEconomica.Industria:
                case TipoEstruturaEconomica.IndustriaLeve:
                case TipoEstruturaEconomica.IndustriaPesada:
                case TipoEstruturaEconomica.Petroleo:
                case TipoEstruturaEconomica.Refinaria:
                    economia.custoProducao += absValor;
                    break;
                default:
                    economia.custoSocial += absValor;
                    break;
            }
            return;
        }

        switch (tipo)
        {
            case TipoEstruturaEconomica.Casa:
                economia.receitaMoradia += valor;
                break;
            case TipoEstruturaEconomica.Comercio:
                economia.receitaComercio += valor;
                break;
            case TipoEstruturaEconomica.Energia:
            case TipoEstruturaEconomica.UsinaSolar:
            case TipoEstruturaEconomica.UsinaCarvao:
                economia.receitaEnergia += valor;
                break;
            default:
                economia.receitaIndustria += valor;
                break;
        }
    }

    private static TipoEstruturaEconomica ResolverTipoPredio(PredioRecursos predio)
    {
        if (predio == null) return TipoEstruturaEconomica.Industria;
        if (predio.producaoEnergia > 0f) return TipoEstruturaEconomica.Energia;
        if (predio.producaoPetroleo > 0f) return TipoEstruturaEconomica.Petroleo;
        if (predio.producaoAco > 0f) return TipoEstruturaEconomica.Industria;
        return TipoEstruturaEconomica.Comercio;
    }

    private float CalcularQualidadeVida(DadosEconomiaPais economia)
    {
        float qv = 50f;
        qv += economia.moradiaTotal > 0 && economia.populacaoTotal <= economia.moradiaTotal ? 12f : -18f;
        qv += economia.TaxaEmprego >= 0.75f ? 15f : -18f;
        qv += economia.deficitComida <= 0f ? 10f : -18f;
        qv += economia.deficitEnergia <= 0f ? 10f : -16f;
        qv += economia.deficitPetroleo <= 0f ? 4f : -8f;
        qv += economia.eficienciaMedia * 8f;
        qv -= Mathf.Clamp(economia.poluicaoIndice * 0.08f, 0f, 8f);
        return Mathf.Clamp(qv, 0f, 100f);
    }

    private static float GerenciadorTempoSegundosPorDia()
    {
        GerenciadorTempo tempo = GerenciadorTempo.Instancia;
        return tempo != null ? Mathf.Max(1f, tempo.duracaoDiaSegundos) : 30f;
    }

    private static void CalcularImpactoAmbiental(DadosEconomiaPais economia)
    {
        if (economia == null) return;

        float energiaCarvao = Mathf.Max(0f, economia.energiaCarvaoProduzida);
        float energiaTotal = Mathf.Max(0.001f, economia.energiaProduzida);
        float energiaLimpa = Mathf.Max(0f, energiaTotal - energiaCarvao);
        float geracaoCarvaoMWhDia = energiaCarvao * HorasPorDia;

        economia.co2ToneladasDia = geracaoCarvaoMWhDia * Co2KgPorMWh / 1000f;
        economia.so2KgDia = geracaoCarvaoMWhDia * So2KgPorMWh;
        economia.noxKgDia = geracaoCarvaoMWhDia * NoxKgPorMWh;
        economia.particulasKgDia = geracaoCarvaoMWhDia * ParticulasKgPorMWh;
        economia.energiaFossilPercentual = energiaCarvao / energiaTotal * 100f;
        economia.energiaLimpaPercentual = energiaLimpa / energiaTotal * 100f;

        // Índice 0–100: 100 equivale a uma usina de 240 MW em operação
        // integral por um dia. É um indicador do jogo, não limite legal.
        float referenciaCo2Toneladas = 240f * HorasPorDia * Co2KgPorMWh / 1000f;
        economia.poluicaoIndice = referenciaCo2Toneladas <= 0f
            ? 0f
            : Mathf.Clamp(economia.co2ToneladasDia / referenciaCo2Toneladas * 100f, 0f, 100f);
    }

    private DadosEconomiaPais ObterOuCriar(int teamId)
    {
        teamId = Mathf.Max(1, teamId);
        DadosEconomiaPais economia;
        if (!economias.TryGetValue(teamId, out economia))
        {
            economia = new DadosEconomiaPais { teamId = teamId };
            economias.Add(teamId, economia);
        }

        return economia;
    }

    private int ResolverTeamId(GameObject go)
    {
        if (go == null) return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1;

        IdentidadeUnidade identidade = go.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null && identidade.teamID > 0) return identidade.teamID;

        IdentidadeIA identidadeIA = go.GetComponentInParent<IdentidadeIA>();
        if (identidadeIA != null && identidadeIA.teamID > 0) return identidadeIA.teamID;

        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        return gov != null ? gov.teamJogador : 1;
    }

    private static void SomarTipo(DadosEconomiaPais economia, TipoEstruturaEconomica tipo)
    {
        switch (tipo)
        {
            case TipoEstruturaEconomica.Casa: economia.casas++; break;
            case TipoEstruturaEconomica.Industria: economia.industrias++; break;
            case TipoEstruturaEconomica.Petroleo: economia.pocosPetroleo++; break;
            case TipoEstruturaEconomica.Comercio: economia.comercios++; break;
            case TipoEstruturaEconomica.Farm: economia.farms++; break;
            case TipoEstruturaEconomica.Energia: economia.usinas++; break;
            case TipoEstruturaEconomica.PesquisaMilitar: break;
            case TipoEstruturaEconomica.UsinaSolar: economia.usinas++; break;
            case TipoEstruturaEconomica.UsinaCarvao: economia.usinas++; break;
        }
    }
}
