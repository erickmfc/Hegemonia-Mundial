using System;
using System.Collections.Generic;
using UnityEngine;

public enum AreaInvestimentoAgricola
{
    Producao, Mecanizacao, Irrigacao, Fertilizacao, SementesBiotecnologia,
    Agrotoxicos, Pesquisa, Agroindustria, Logistica, Armazenamento,
    RecursosHidricos, Pecuaria
}

/// <summary>Calcula a produção agrícola nacional na passagem de cada dia de jogo.</summary>
[DisallowMultipleComponent]
public sealed class AgriculturaNacional : MonoBehaviour
{
    public const string PesquisaAgrotoxicosId = "pesquisa_tecnologia_agrotoxicos";
    public const int InvestimentoMinimoSemanal = 100; // $100M por semana
    public const int CapacidadeArmazenamentoPadrao = 5000;
    private const float ProducaoBaseDiaria = 24f;
    // Mesmo sem irrigação e insumos, uma parcela das lavouras de sequeiro continua
    // produzindo. Os recursos elevam o rendimento acima desse piso.
    private const float FatorMinimoSafraSequeiro = 0.30f;
    private static readonly string[] Culturas = {
        "comida_milho", "comida_batata", "comida_feijao", "comida_trigo",
        "comida_arroz", "comida_cana", "comida_soja", "comida_cafe", "comida_cacau",
        "comida_mandioca", "comida_aveia", "comida_cevada", "comida_tomate",
        "comida_frutas", "comida_hortalicas"
    };

    [Header("Consumo configurável por tonelada produzida")]
    [Min(0f)] public float aguaPorTonelada = 2f;
    [Min(0f)] public float sementesPorTonelada = 0.03f;
    [Min(0f)] public float fertilizantePorTonelada = 0.02f;
    [Min(0f)] public float agrotoxicosPorTonelada = 0.005f;
    [Range(0f, 1f)] public float produtividadeSemFertilizante = 0.35f;
    [Range(0f, 1f)] public float produtividadeSemSementes = 0.15f;
    [Range(0f, 1f)] public float produtividadeSemAgrotoxico = 0.9f;
    [Min(0)] public int producaoBaseAgrotoxicoDiaria = 2;

    private SistemaGovernoMundial governo;
    private GerenciadorTempo relogioInscrito;
    private int ultimoDia;

    public int UltimoDiaProcessado => ultimoDia;
    public bool RelogioConectado => relogioInscrito != null;

    public static string NomeArea(AreaInvestimentoAgricola area)
    {
        switch (area)
        {
            case AreaInvestimentoAgricola.Producao: return "Produção agrícola";
            case AreaInvestimentoAgricola.Mecanizacao: return "Mecanização";
            case AreaInvestimentoAgricola.Irrigacao: return "Irrigação";
            case AreaInvestimentoAgricola.Fertilizacao: return "Fertilização";
            case AreaInvestimentoAgricola.SementesBiotecnologia: return "Sementes e biotecnologia";
            case AreaInvestimentoAgricola.Agrotoxicos: return "Agrotóxicos";
            case AreaInvestimentoAgricola.Pesquisa: return "Pesquisa agrícola";
            case AreaInvestimentoAgricola.Agroindustria: return "Agroindústria";
            case AreaInvestimentoAgricola.Logistica: return "Logística agrícola";
            case AreaInvestimentoAgricola.Armazenamento: return "Armazenamento";
            case AreaInvestimentoAgricola.RecursosHidricos: return "Recursos hídricos";
            default: return "Pecuária";
        }
    }

    private void Awake()
    {
        governo = GetComponent<SistemaGovernoMundial>();
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        GerenciadorTempo.GarantirInstancia();
        ultimoDia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        if (governo != null && governo.Paises != null)
        {
            for (int i = 0; i < governo.Paises.Count; i++)
            {
                DadosPaisGoverno pais = governo.Paises[i];
                if (pais != null)
                    GarantirCapacidadeArmazenamento(pais, pais.teamId == governo.teamJogador);
            }
        }
        GarantirEstoqueHidricoInicial();
    }

    /// <summary>
    /// Corrige saves antigos com capacidade nula ou negativa, preservando toda
    /// capacidade positiva já configurada pelo jogador.
    /// </summary>
    public static bool GarantirCapacidadeArmazenamento(DadosPaisGoverno pais, bool jogador)
    {
        if (pais == null) return false;

        bool alterou = false;
        if (pais.capacidadeArmazenamentoAgricola <= 0)
        {
            pais.capacidadeArmazenamentoAgricola = CapacidadeArmazenamentoPadrao;
            alterou = true;
        }
        if (pais.aguaMaxima <= 0)
        {
            pais.aguaMaxima = CapacidadeArmazenamentoPadrao;
            alterou = true;
        }

        if (jogador)
        {
            DadosArmazemRecursos armazem = GerenciadorArmazens.Instancia != null
                ? GerenciadorArmazens.Instancia.armazemRecursos
                : null;
            if (armazem != null)
            {
                if (armazem.alimentosMaximo <= 0)
                {
                    armazem.alimentosMaximo = Mathf.Max(CapacidadeArmazenamentoPadrao, pais.capacidadeArmazenamentoAgricola);
                    alterou = true;
                }
                if (armazem.aguaMaximo <= 0)
                {
                    armazem.aguaMaximo = Mathf.Max(CapacidadeArmazenamentoPadrao, pais.aguaMaxima);
                    alterou = true;
                }
            }
        }

        return alterou;
    }

    private void OnEnable()
    {
        GarantirConexaoComRelogio();
    }

    private void OnDisable()
    {
        if (relogioInscrito != null)
        {
            relogioInscrito.OnDataAlterada -= ProcessarNovoDia;
            relogioInscrito.OnDataRestaurada -= SincronizarUltimoDia;
        }
        relogioInscrito = null;
    }

    private void Update()
    {
        if (relogioInscrito != GerenciadorTempo.Instancia)
            GarantirConexaoComRelogio();

        // O evento é o caminho normal. A conferência também recupera um dia
        // caso o GerenciadorTempo tenha sido substituído durante a partida.
        if (relogioInscrito != null && relogioInscrito.totalDias > ultimoDia)
            ProcessarNovoDia();
    }

    private void GarantirConexaoComRelogio()
    {
        GerenciadorTempo.GarantirInstancia();
        GerenciadorTempo atual = GerenciadorTempo.Instancia;
        if (relogioInscrito == atual) return;

        if (relogioInscrito != null)
        {
            relogioInscrito.OnDataAlterada -= ProcessarNovoDia;
            relogioInscrito.OnDataRestaurada -= SincronizarUltimoDia;
        }
        relogioInscrito = atual;
        if (relogioInscrito != null)
        {
            relogioInscrito.OnDataAlterada -= ProcessarNovoDia;
            relogioInscrito.OnDataAlterada += ProcessarNovoDia;
            relogioInscrito.OnDataRestaurada -= SincronizarUltimoDia;
            relogioInscrito.OnDataRestaurada += SincronizarUltimoDia;
            SincronizarUltimoDia();
        }
    }

    private void SincronizarUltimoDia()
    {
        if (relogioInscrito != null)
            ultimoDia = Mathf.Max(1, relogioInscrito.totalDias);
    }

    public bool DefinirInvestimentoSemanal(int teamId, int milhoes)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        int valor = Mathf.Clamp(milhoes, InvestimentoMinimoSemanal, 10000);
        if (pais == null) return false;
        pais.investimentoAgricolaSemanal = valor;
        pais.investimentoAgricolaPercentualAtivo = false;
        pais.investimentoAgricolaSemRecursos = false;
        pais.ultimaCobrancaAgricola = 0;
        governo.NotificarGovernoAtualizado();
        return true;
    }

    /// <summary>Define percentual (0–0,25%) do saldo operacional cobrado semanalmente.</summary>
    public bool DefinirInvestimentoPercentualSemanal(int teamId, float percentual)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || float.IsNaN(percentual) || float.IsInfinity(percentual)) return false;
        pais.investimentoAgricolaPercentualSemanal = Mathf.Clamp(percentual, 0f, 0.25f);
        pais.investimentoAgricolaPercentualAtivo = true;
        pais.investimentoAgricolaSemRecursos = false;
        pais.ultimaCobrancaAgricola = 0;
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public static int QuantidadeCulturas { get { return Culturas.Length; } }
    public static string ObterIdCultura(int indice)
    {
        return indice >= 0 && indice < Culturas.Length ? Culturas[indice] : string.Empty;
    }

    public static string ObterNomeCultura(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Alimento";
        string nome = id.Replace("comida_", string.Empty).Replace('_', ' ');
        return System.Globalization.CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(nome);
    }

    public bool DefinirParticipacaoCultura(int teamId, int indice, int percentual)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || indice < 0 || indice >= Culturas.Length) return false;
        GarantirDadosCulturas(pais);
        float desejado = Mathf.Clamp(percentual, 0, 100) / 100f;
        float restanteAnterior = 0f;
        for (int i = 0; i < Culturas.Length; i++) if (i != indice) restanteAnterior += Mathf.Max(0f, pais.mixCulturaAgricola[i]);
        float restanteNovo = 1f - desejado;
        pais.mixCulturaAgricola[indice] = desejado;
        for (int i = 0; i < Culturas.Length; i++)
        {
            if (i == indice) continue;
            pais.mixCulturaAgricola[i] = restanteAnterior > 0f
                ? Mathf.Max(0f, pais.mixCulturaAgricola[i]) / restanteAnterior * restanteNovo
                : restanteNovo / (Culturas.Length - 1);
        }
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public static long CalcularOrcamentoCompraAlimentar(long saldoDisponivel, float percentualCaixa)
    {
        if (saldoDisponivel <= 0 || float.IsNaN(percentualCaixa) || float.IsInfinity(percentualCaixa))
            return 0L;

        double percentual = Math.Max(0d, Math.Min(100d, percentualCaixa));
        return (long)Math.Floor(saldoDisponivel * percentual / 100d);
    }

    public bool DefinirPercentualCompraAlimentar(int teamId, float percentual)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || float.IsNaN(percentual) || float.IsInfinity(percentual)) return false;

        pais.compraAlimentarPercentualCaixaDiario = Mathf.Clamp(percentual, 0f, 100f);
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public bool DefinirParticipacaoCompraAlimentar(int teamId, int indice, int percentual)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || indice < 0 || indice >= Culturas.Length) return false;

        GarantirDadosCompraAlimentar(pais);
        float desejado = Mathf.Clamp(percentual, 0, 100) / 100f;
        float restanteAnterior = 0f;
        for (int i = 0; i < Culturas.Length; i++)
            if (i != indice) restanteAnterior += Mathf.Max(0f, pais.participacaoCompraAlimentar[i]);

        float restanteNovo = 1f - desejado;
        pais.participacaoCompraAlimentar[indice] = desejado;
        for (int i = 0; i < Culturas.Length; i++)
        {
            if (i == indice) continue;
            pais.participacaoCompraAlimentar[i] = restanteAnterior > 0f
                ? Mathf.Max(0f, pais.participacaoCompraAlimentar[i]) / restanteAnterior * restanteNovo
                : restanteNovo / (Culturas.Length - 1);
        }

        governo.NotificarGovernoAtualizado();
        return true;
    }

    public bool DefinirCompraAlimentarDiaria(int teamId, bool ativa, out string mensagem)
    {
        mensagem = string.Empty;
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null)
        {
            mensagem = "Governo nacional indisponivel.";
            return false;
        }

        GarantirDadosCompraAlimentar(pais);
        pais.compraAlimentarDiariaAtiva = ativa;
        if (!ativa)
        {
            mensagem = "Compra diaria de alimentos pausada.";
            governo.NotificarGovernoAtualizado();
            return true;
        }

        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        if (pais.ultimoDiaCompraAlimentarProcessado == diaAtual)
        {
            mensagem = "Compra diaria ativada; o orçamento de hoje ja foi processado.";
            governo.NotificarGovernoAtualizado();
            return true;
        }

        long valorGasto;
        bool comprou = ExecutarCompraAlimentarPercentualCaixa(teamId, out mensagem, out valorGasto);
        // A tentativa de hoje e registrada mesmo quando o caixa/estoque impede
        // a compra, para que o evento de data nao possa cobrar duas vezes.
        pais.ultimoDiaCompraAlimentarProcessado = diaAtual;
        if (!comprou && string.IsNullOrEmpty(mensagem)) mensagem = "Nenhum alimento foi comprado hoje.";
        governo.NotificarGovernoAtualizado();
        return comprou;
    }

    public bool ExecutarCompraAlimentarPercentualCaixa(int teamId, out string mensagem, out long valorGasto)
    {
        mensagem = string.Empty;
        valorGasto = 0L;
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (governo == null || pais == null || mercado == null)
        {
            mensagem = "Governo ou mercado nacional indisponivel.";
            return false;
        }

        GarantirDadosCompraAlimentar(pais);
        governo.ReconciliarEstoqueComidaCulturas(teamId);
        long saldo = governo.ObterSaldoDisponivelParaOperacao(teamId);
        long orcamento = CalcularOrcamentoCompraAlimentar(saldo, pais.compraAlimentarPercentualCaixaDiario);
        int capacidadeLivre = ObterCapacidadeLivreComida(pais);
        if (orcamento <= 0L)
        {
            mensagem = "Orcamento de alimentos igual a zero; ajuste o percentual ou o saldo nacional.";
            return false;
        }
        if (capacidadeLivre <= 0)
        {
            mensagem = "Armazem de alimentos cheio; libere espaco antes de comprar.";
            return false;
        }

        bool comprou = mercado.ComprarCestaAlimentar(teamId, orcamento, pais.participacaoCompraAlimentar,
            capacidadeLivre, out valorGasto, out mensagem);
        if (comprou && pais.compraAlimentarDiariaAtiva && GerenciadorTempo.Instancia != null)
            pais.ultimoDiaCompraAlimentarProcessado = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
        return comprou;
    }

    private static void GarantirDadosCompraAlimentar(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        if (float.IsNaN(pais.compraAlimentarPercentualCaixaDiario)
            || float.IsInfinity(pais.compraAlimentarPercentualCaixaDiario))
            pais.compraAlimentarPercentualCaixaDiario = 1f;
        pais.compraAlimentarPercentualCaixaDiario = Mathf.Clamp(pais.compraAlimentarPercentualCaixaDiario, 0f, 100f);
        if (pais.participacaoCompraAlimentar == null || pais.participacaoCompraAlimentar.Length != Culturas.Length)
            Array.Resize(ref pais.participacaoCompraAlimentar, Culturas.Length);
        float soma = 0f;
        for (int i = 0; i < Culturas.Length; i++)
        {
            float valor = pais.participacaoCompraAlimentar[i];
            if (float.IsNaN(valor) || float.IsInfinity(valor) || valor < 0f)
                pais.participacaoCompraAlimentar[i] = 0f;
            soma += pais.participacaoCompraAlimentar[i];
        }
        if (soma <= 0f)
            for (int i = 0; i < Culturas.Length; i++) pais.participacaoCompraAlimentar[i] = 1f;
    }

    private static void GarantirDadosCulturas(DadosPaisGoverno pais)
    {
        if (pais.mixCulturaAgricola == null || pais.mixCulturaAgricola.Length != Culturas.Length)
            Array.Resize(ref pais.mixCulturaAgricola, Culturas.Length);
        if (pais.saldoFracionarioCulturas == null || pais.saldoFracionarioCulturas.Length != Culturas.Length)
            Array.Resize(ref pais.saldoFracionarioCulturas, Culturas.Length);
    }

    public bool InvestirArea(int teamId, AreaInvestimentoAgricola area)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null) return false;
        GarantirDadosArea(pais);
        int index = (int)area;
        if (index < 0 || index >= pais.niveisInvestimentoAgricola.Length || pais.niveisInvestimentoAgricola[index] >= 10) return false;
        long custo = CalcularCobrancaFixaSemanal(100 + pais.niveisInvestimentoAgricola[index] * 50);
        if (!governo.TentarPagar(teamId, custo)) return false;
        pais.niveisInvestimentoAgricola[index]++;
        if (area == AreaInvestimentoAgricola.RecursosHidricos)
        {
            pais.aguaMaxima += 500;
            if (teamId == governo.teamJogador && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
                GerenciadorArmazens.Instancia.armazemRecursos.aguaMaximo += 500;
        }
        if (area == AreaInvestimentoAgricola.Armazenamento)
        {
            pais.capacidadeArmazenamentoAgricola += 1000;
            if (teamId == governo.teamJogador && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
                GerenciadorArmazens.Instancia.armazemRecursos.alimentosMaximo += 1000;
        }
        governo.NotificarGovernoAtualizado();
        return true;
    }

    private static void GarantirDadosArea(DadosPaisGoverno pais)
    {
        if (pais.niveisInvestimentoAgricola == null || pais.niveisInvestimentoAgricola.Length != 12)
            Array.Resize(ref pais.niveisInvestimentoAgricola, 12);
    }

    private void GarantirEstoqueHidricoInicial()
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return;
        DadosPaisGoverno jogador = governo.ObterPais(governo.teamJogador);
        GarantirCapacidadeArmazenamento(jogador, true);
        if (GerenciadorArmazens.Instancia == null || GerenciadorArmazens.Instancia.armazemRecursos == null) return;
        DadosArmazemRecursos armazem = GerenciadorArmazens.Instancia.armazemRecursos;
        if (jogador != null && !jogador.estoqueHidricoInicializado && armazem.agua <= 0 && jogador.agua > 0)
        {
            armazem.AdicionarRecurso(TipoRecurso.Agua, Mathf.Min(jogador.agua, armazem.aguaMaximo));
            jogador.agua = armazem.agua;
            GerenciadorArmazens.Instancia.NotificarAtualizacaoManual();
        }
        if (jogador != null) jogador.estoqueHidricoInicializado = true;
    }

    public static float CalcularProducaoDiaria(float capacidade, float eficiencia, float agua,
        float insumos, float tecnologia, float mecanizacao, float logistica)
    {
        return Mathf.Max(0f, capacidade) * Mathf.Clamp(eficiencia, 0f, 2f)
            * Mathf.Clamp01(agua) * Mathf.Clamp01(insumos) * Mathf.Clamp(tecnologia, 0f, 2f)
            * Mathf.Clamp(mecanizacao, 0f, 2f) * Mathf.Clamp01(logistica);
    }

    private void ProcessarNovoDia()
    {
        GarantirEstoqueHidricoInicial();
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        if (governo == null || GerenciadorTempo.Instancia == null) return;
        int diaAtual = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
        if (diaAtual <= ultimoDia) return;

        IReadOnlyList<DadosPaisGoverno> paises = governo.Paises;
        // O evento diário normalmente entrega um dia por chamada. Se o sistema
        // recuperar uma virada perdida, processa cada dia pendente para manter
        // produção, consumo e cobranças semanais alinhados ao calendário.
        while (ultimoDia < diaAtual)
        {
            int dia = ultimoDia + 1;
            ultimoDia = dia;
            ProcessarDiaAgricola(paises, dia);
        }
        governo.NotificarGovernoAtualizado();
    }

    private void ProcessarDiaAgricola(IReadOnlyList<DadosPaisGoverno> paises, int dia)
    {
        if (paises == null) return;
        for (int i = 0; i < paises.Count; i++)
        {
            DadosPaisGoverno pais = paises[i];
            if (pais == null) continue;
            OrcamentoInvestimentoNacional.ProcessarDia(governo, pais, dia);
            GarantirCapacidadeArmazenamento(pais, pais.teamId == governo.teamJogador);
            GarantirDadosArea(pais);
            governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
            if ((dia - 1) % 7 == 0) ProcessarCobrancaSemanal(pais);
            ProcessarCompraAlimentarDiaria(pais, dia);
            int comidaPendenteDoDia = SistemaPopulacao.IniciarConsumoComidaDiario(pais);
            float producaoIndustrialSolicitada = ObterProducaoIndustrialDiaria(pais);
            Produzir(pais);
            int producaoIndustrialCreditada = CreditarProducaoIndustrialDiaria(pais, producaoIndustrialSolicitada);
            float producaoComidaCreditada = pais.producaoAgricolaDiaria + producaoIndustrialCreditada;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null
                ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId)
                : null;
            SistemaPopulacao.FinalizarConsumoComidaDiario(
                pais, comidaPendenteDoDia, producaoComidaCreditada, economia);
        }
    }

    private void ProcessarCompraAlimentarDiaria(DadosPaisGoverno pais, int dia)
    {
        if (pais == null || !pais.compraAlimentarDiariaAtiva || pais.ultimoDiaCompraAlimentarProcessado >= dia)
            return;

        // Registrar antes da chamada impede reentrancia se a compra notificar
        // listeners que consultam novamente o calendario durante a liquidacao.
        pais.ultimoDiaCompraAlimentarProcessado = dia;
        ExecutarCompraAlimentarPercentualCaixa(pais.teamId, out _, out _);
    }

    private void ProcessarCobrancaSemanal(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        if (pais.orcamentoInvestimentoAtivo)
        {
            // O orçamento nacional já pagou o aporte diário; converte-o em
            // equivalente semanal apenas para calcular o rendimento agrícola.
            pais.ultimaCobrancaAgricola = pais.orcamentoAgriculturaHoje > long.MaxValue / 7L
                ? long.MaxValue
                : pais.orcamentoAgriculturaHoje * 7L;
            pais.investimentoAgricolaSemRecursos = pais.ultimaCobrancaAgricola <= 0L;
            return;
        }
        if (pais.investimentoAgricolaPercentualAtivo)
        {
            long saldo = governo.ObterSaldoDisponivelParaOperacao(pais.teamId);
            long custo = CalcularCobrancaSemanal(saldo, pais.investimentoAgricolaPercentualSemanal);
            pais.investimentoAgricolaSemRecursos = custo > 0 && !governo.TentarPagar(pais.teamId, custo);
            pais.ultimaCobrancaAgricola = pais.investimentoAgricolaSemRecursos ? 0 : custo;
        }
        else
        {
            long custoSemanal = CalcularCobrancaFixaSemanal(pais.investimentoAgricolaSemanal);
            pais.investimentoAgricolaSemRecursos = !governo.TentarPagar(pais.teamId, custoSemanal);
            pais.ultimaCobrancaAgricola = pais.investimentoAgricolaSemRecursos ? 0 : custoSemanal;
        }
    }

    private void Produzir(DadosPaisGoverno pais)
    {
        if (pais == null || governo == null) return;
        pais.producaoAgricolaDiaria = 0;
        bool tecnologia = TecnologiaAgrotoxicosDesbloqueada(pais.teamId);
        pais.producaoAgrotoxicosDiaria = 0;
        if (tecnologia)
        {
            int producaoAgro = Mathf.Max(0, producaoBaseAgrotoxicoDiaria + pais.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Agrotoxicos] * 2);
            governo.AdicionarEstoque(pais.teamId, "agrotoxicos", producaoAgro);
            pais.producaoAgrotoxicosDiaria = producaoAgro;
            AtualizarOferta("agrotoxicos", producaoAgro);
        }

        int investimento = Mathf.Max(0, pais.investimentoAgricolaSemanal);
        float bonusInvestimento = pais.orcamentoInvestimentoAtivo
            ? CalcularBonusInvestimentoPercentual(pais.orcamentoAgriculturaHoje > long.MaxValue / 7L
                ? long.MaxValue : pais.orcamentoAgriculturaHoje * 7L)
            : pais.investimentoAgricolaPercentualAtivo
                ? CalcularBonusInvestimentoPercentual(pais.ultimaCobrancaAgricola)
                : CalcularBonusAplicadoInvestimentoFixo(
                    investimento, pais.ultimaCobrancaAgricola, pais.investimentoAgricolaSemRecursos);
        int quantidade = Mathf.FloorToInt(EstimarProducaoDiaria(pais, bonusInvestimento)
            * OrcamentoInvestimentoNacional.FatorProducao(pais, 0));
        if (quantidade <= 0) return;

        float irrigacao = pais.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Irrigacao];

        Consumir(pais.teamId, "agua", Mathf.CeilToInt(quantidade * aguaPorTonelada / (1f + irrigacao * 0.1f)));
        Consumir(pais.teamId, "sementes", Mathf.CeilToInt(quantidade * sementesPorTonelada));
        Consumir(pais.teamId, "fertilizante_organico", Mathf.CeilToInt(quantidade * fertilizantePorTonelada));
        Consumir(pais.teamId, "agrotoxicos", Mathf.CeilToInt(quantidade * agrotoxicosPorTonelada));
        DistribuirCulturas(pais, quantidade);
    }

    /// <summary>
    /// Estima a colheita usando as mesmas regras de água, insumos, tecnologia e
    /// capacidade do ciclo diário. O menu usa o resultado para mostrar o efeito
    /// real de cada valor antes de o investimento ser cobrado.
    /// </summary>
    public int EstimarProducaoDiaria(DadosPaisGoverno pais, float bonusInvestimento)
    {
        if (pais == null) return 0;
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return 0;

        GarantirDadosArea(pais);
        float capacidade = CalcularCapacidadeNominal(pais);
        int[] niveis = pais.niveisInvestimentoAgricola;
        float eficiencia = Mathf.Clamp(pais.eficienciaAgricola + Mathf.Max(0f, bonusInvestimento)
            + niveis[(int)AreaInvestimentoAgricola.Pesquisa] * 0.015f, 0.5f, 1.8f);
        float mecanizacao = 1f + pais.nivelMecanizacaoAgricola
            + niveis[(int)AreaInvestimentoAgricola.Mecanizacao] * 0.025f;
        float logistica = Mathf.Clamp01(pais.eficienciaLogisticaAgricola
            + niveis[(int)AreaInvestimentoAgricola.Logistica] * 0.01f);
        float demandaAgua = capacidade * aguaPorTonelada
            / (1f + niveis[(int)AreaInvestimentoAgricola.Irrigacao] * 0.1f);
        float fatorAgua = CalcularFatorAgua(
            governo.ObterEstoque(pais.teamId, RecursoMercado.Agua), demandaAgua, pais.eficienciaHidrica);
        float sementes = CalcularFatorInsumo(
            governo.ObterEstoque(pais.teamId, "sementes"), capacidade * sementesPorTonelada,
            produtividadeSemSementes, niveis[(int)AreaInvestimentoAgricola.SementesBiotecnologia] * 0.01f);
        float fertilizante = CalcularFatorInsumo(
            governo.ObterEstoque(pais.teamId, "fertilizante_organico"), capacidade * fertilizantePorTonelada,
            produtividadeSemFertilizante, niveis[(int)AreaInvestimentoAgricola.Fertilizacao] * 0.02f);
        float pesticida = CalcularFatorInsumo(
            governo.ObterEstoque(pais.teamId, "agrotoxicos"), capacidade * agrotoxicosPorTonelada,
            produtividadeSemAgrotoxico, 0f);
        float rendimento = CalcularFatorRendimentoSafra(
            fatorAgua, Mathf.Clamp01(sementes * fertilizante * pesticida), logistica);
        float diaria = CalcularProducaoDiaria(capacidade, eficiencia, rendimento,
            1f, 1f + niveis[(int)AreaInvestimentoAgricola.Pesquisa] * 0.01f, mecanizacao, 1f);
        int bruta = Mathf.Max(0, Mathf.RoundToInt(diaria));
        return Mathf.Min(bruta, ObterCapacidadeLivreComida(pais));
    }

    private static float CalcularCapacidadeNominal(DadosPaisGoverno pais)
    {
        if (pais == null) return 0f;
        int[] niveis = pais.niveisInvestimentoAgricola;
        return ProducaoBaseDiaria + Mathf.Max(0, pais.capacidadeAgricola)
            + niveis[(int)AreaInvestimentoAgricola.Producao] * 5f
            + niveis[(int)AreaInvestimentoAgricola.Agroindustria] * 2f;
    }

    private float ObterProducaoIndustrialDiaria(DadosPaisGoverno pais)
    {
        SistemaEconomiaImoveis economia = SistemaEconomiaImoveis.Instancia;
        if (economia == null) return 0f;
        DadosEconomiaPais snapshot = economia.ObterEconomia(pais.teamId);
        if (snapshot == null) return 0f;
        return Mathf.Max(0f, snapshot.comidaProduzida - Mathf.Max(0, pais.producaoAgricolaDiaria));
    }

    private int CreditarProducaoIndustrialDiaria(DadosPaisGoverno pais, float toneladasSolicitadas)
    {
        int toneladasIndustriais = Mathf.Max(0, Mathf.FloorToInt(toneladasSolicitadas));
        int creditar = Mathf.Min(toneladasIndustriais, ObterCapacidadeLivreComida(pais));
        if (creditar > 0)
        {
            governo.AdicionarEstoque(pais.teamId, RecursoMercado.Comida, creditar);
            AtualizarOferta("comida", creditar);
        }
        return creditar;
    }

    private int ObterCapacidadeLivreComida(DadosPaisGoverno pais)
    {
        if (pais == null || governo == null) return 0;
        GarantirCapacidadeArmazenamento(pais, pais.teamId == governo.teamJogador);
        // As culturas vivem no armazem industrial; consolide-as antes de
        // calcular o espaco para evitar aceitar uma colheita sobre estoque antigo.
        governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
        int capacidade = Mathf.Max(0, pais.capacidadeArmazenamentoAgricola);
        int estoque = Mathf.Max(0, governo.ObterEstoque(pais.teamId, RecursoMercado.Comida));
        int espaco = Mathf.Max(0, capacidade - estoque);
        if (pais.teamId != governo.teamJogador) return espaco;

        GerenciadorArmazens gerenciador = GerenciadorArmazens.Instancia;
        DadosArmazemRecursos armazem = gerenciador != null ? gerenciador.armazemRecursos : null;
        if (armazem == null) return espaco;
        if (armazem.alimentosMaximo <= 0) return espaco;
        espaco = Mathf.Min(espaco, Mathf.Max(0, armazem.alimentosMaximo - armazem.alimentos));
        return espaco;
    }

    public static float CalcularFatorAgua(float disponivel, float demanda, float eficienciaHidrica)
    {
        float disponibilidade = demanda <= 0f ? 1f : Mathf.Sqrt(Mathf.Clamp01(disponivel / demanda));
        return disponibilidade * Mathf.Clamp01(eficienciaHidrica);
    }

    public static float CalcularFatorInsumo(float estoque, float demanda, float minimo, float bonus)
    {
        if (demanda <= 0f) return 1f + bonus;
        float proporcao = Mathf.Clamp01(estoque / demanda);
        return Mathf.Clamp(Mathf.Lerp(Mathf.Clamp01(minimo), 1f, Mathf.Sqrt(proporcao)) * (1f + bonus), 0f, 1.5f);
    }

    public static float CalcularBonusInvestimentoPercentual(long valorCobranca)
    {
        return Mathf.Clamp01(1f - Mathf.Exp(-(float)Math.Max(0L, valorCobranca) / 10000000f)) * 0.5f;
    }

    /// <summary>Estimativa de eficiência para o valor fixo semanal escolhido, em milhões.</summary>
    public static float CalcularBonusInvestimentoFixo(int investimentoEmMilhoes)
    {
        int investimento = Mathf.Max(InvestimentoMinimoSemanal, investimentoEmMilhoes);
        return 0.005f + 0.115f * (1f - Mathf.Exp(-Mathf.Max(0, investimento - InvestimentoMinimoSemanal) / 3200f));
    }

    /// <summary>O bônus fixo só entra depois de uma cobrança real concluída.</summary>
    public static float CalcularBonusAplicadoInvestimentoFixo(
        int investimentoEmMilhoes, long ultimaCobrancaRealizada, bool semRecursos)
    {
        if (semRecursos || ultimaCobrancaRealizada <= 0L) return 0f;
        return CalcularBonusInvestimentoFixo(investimentoEmMilhoes);
    }

    /// <summary>
    /// Calcula a fração do saldo operacional debitada na próxima cobrança semanal.
    /// Este mesmo cálculo é usado pelo menu para exibir um valor que bate com o débito real.
    /// </summary>
    public static long CalcularCobrancaSemanal(long saldoOperacional, float percentual)
    {
        if (float.IsNaN(percentual) || float.IsInfinity(percentual)) return 0L;
        double percentualNormalizado = Math.Round(
            Mathf.Clamp(percentual, 0f, 0.25f) * 1_000_000d,
            MidpointRounding.AwayFromZero) / 1_000_000d;
        double valor = Math.Floor(Math.Max(0L, saldoOperacional) * percentualNormalizado / 100d);
        return (long)Math.Max(0d, valor);
    }

    /// <summary>Converte o investimento fixo informado em milhões para o débito em dinheiro.</summary>
    public static long CalcularCobrancaFixaSemanal(int investimentoEmMilhoes)
    {
        long milhoes = Math.Max(InvestimentoMinimoSemanal, investimentoEmMilhoes);
        return milhoes * 1_000_000L;
    }

    /// <summary>
    /// Mantém uma produção básica de sequeiro enquanto água ou insumos escasseiam.
    /// A produção completa continua dependendo dos recursos e da logística.
    /// </summary>
    public static float CalcularFatorRendimentoSafra(float fatorAgua, float fatorInsumos, float fatorLogistica)
    {
        float rendimentoComRecursos = Mathf.Clamp01(fatorAgua)
            * Mathf.Clamp01(fatorInsumos)
            * Mathf.Clamp01(fatorLogistica);
        return Mathf.Max(FatorMinimoSafraSequeiro, rendimentoComRecursos);
    }

    private void Consumir(int teamId, string recursoId, int quantidade)
    {
        if (quantidade <= 0) return;
        int disponivel = governo.ObterEstoque(teamId, recursoId);
        governo.RemoverEstoque(teamId, recursoId, Mathf.Min(disponivel, quantidade));
    }

    private void DistribuirCulturas(DadosPaisGoverno pais, int total)
    {
        if (pais.mixCulturaAgricola == null || pais.mixCulturaAgricola.Length != Culturas.Length)
            Array.Resize(ref pais.mixCulturaAgricola, Culturas.Length);
        if (pais.saldoFracionarioCulturas == null || pais.saldoFracionarioCulturas.Length != Culturas.Length)
            Array.Resize(ref pais.saldoFracionarioCulturas, Culturas.Length);
        float somaPesos = 0f;
        for (int i = 0; i < Culturas.Length; i++) somaPesos += Mathf.Max(0f, pais.mixCulturaAgricola[i]);
        bool pesosIguais = somaPesos <= 0f;
        if (pesosIguais) somaPesos = Culturas.Length;
        int[] partes = new int[Culturas.Length];
        int alocado = 0;
        for (int i = 0; i < Culturas.Length; i++)
        {
            float peso = pesosIguais ? 1f : Mathf.Max(0f, pais.mixCulturaAgricola[i]);
            pais.saldoFracionarioCulturas[i] += total * peso / somaPesos;
            partes[i] = Mathf.FloorToInt(Mathf.Max(0f, pais.saldoFracionarioCulturas[i]));
            pais.saldoFracionarioCulturas[i] -= partes[i];
            alocado += partes[i];
        }
        while (alocado < total)
        {
            int melhor = 0;
            for (int i = 1; i < Culturas.Length; i++)
                if (pais.saldoFracionarioCulturas[i] > pais.saldoFracionarioCulturas[melhor]) melhor = i;
            partes[melhor]++;
            pais.saldoFracionarioCulturas[melhor] -= 1f;
            alocado++;
        }
        for (int i = 0; i < Culturas.Length; i++)
        {
            if (partes[i] <= 0) continue;
            governo.AdicionarEstoque(pais.teamId, Culturas[i], partes[i]);
            AtualizarOferta(Culturas[i], partes[i]);
        }
        pais.producaoAgricolaDiaria = total;
        // Confirma que a soma das culturas também aparece no saldo nacional,
        // no consumo e no menu de governo no mesmo ciclo da produção.
        governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
    }

    private void AtualizarOferta(string id, int quantidade)
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null || quantidade <= 0) return;
        DadosItemMercado item = mercado.ObterItem(id);
        if (item == null) return;
        item.estoqueGlobal = Mathf.Clamp(item.estoqueGlobal + quantidade, 0, int.MaxValue);
        item.oferta = Mathf.Clamp(item.oferta + quantidade * 0.02f, 0f, 160f);
    }

    public bool TecnologiaAgrotoxicosDesbloqueada(int teamId)
    {
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || pais.pesquisas == null) return false;
        for (int i = 0; i < pais.pesquisas.Count; i++)
        {
            PesquisaNacionalEstado pesquisa = pais.pesquisas[i];
            if (pesquisa != null && string.Equals(pesquisa.id, PesquisaAgrotoxicosId, StringComparison.OrdinalIgnoreCase))
                return pesquisa.concluida;
        }
        return false;
    }
}
