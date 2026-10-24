using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distribui o investimento nacional diário entre produção civil, ciência e
/// forças armadas. O valor total é debitado uma vez; os setores recebem apenas
/// a participação contábil desse mesmo débito.
/// </summary>
public static class OrcamentoInvestimentoNacional
{
    public const long CustoRecrutamentoPorMilitar = 5_000_000L;
    private const int DiasReservaFolha = 30;

    public static void NormalizarPercentuais(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        int[] setores = ObterPercentuaisSetores(pais);
        NormalizarArray(setores);
        DefinirPercentuaisSetores(pais, setores);
        int[] forcas = { pais.orcamentoPctExercito, pais.orcamentoPctMarinha, pais.orcamentoPctAerea };
        NormalizarArray(forcas);
        pais.orcamentoPctExercito = forcas[0];
        pais.orcamentoPctMarinha = forcas[1];
        pais.orcamentoPctAerea = forcas[2];
    }

    public static void AjustarPercentualSetor(DadosPaisGoverno pais, int indice, int valor)
    {
        if (pais == null || indice < 0 || indice >= 6) return;
        int[] valores = ObterPercentuaisSetores(pais);
        RedistribuirRestante(valores, indice, valor);
        DefinirPercentuaisSetores(pais, valores);
    }

    public static void AjustarPercentualForca(DadosPaisGoverno pais, int indice, int valor)
    {
        if (pais == null || indice < 0 || indice >= 3) return;
        int[] valores = { pais.orcamentoPctExercito, pais.orcamentoPctMarinha, pais.orcamentoPctAerea };
        RedistribuirRestante(valores, indice, valor);
        pais.orcamentoPctExercito = valores[0];
        pais.orcamentoPctMarinha = valores[1];
        pais.orcamentoPctAerea = valores[2];
    }

    public static int ObterPercentualSetor(DadosPaisGoverno pais, int indice)
    {
        if (pais == null) return 0;
        switch (indice)
        {
            case 0: return pais.orcamentoPctAgricultura;
            case 1: return pais.orcamentoPctEnergia;
            case 2: return pais.orcamentoPctPetroleo;
            case 3: return pais.orcamentoPctIndustria;
            case 4: return pais.orcamentoPctCiencia;
            case 5: return pais.orcamentoPctMilitar;
            default: return 0;
        }
    }

    public static long ObterValorSetor(DadosPaisGoverno pais, int indice)
    {
        if (pais == null) return 0L;
        long total = Math.Max(0L, pais.orcamentoInvestimentoDiario);
        return (long)Math.Floor(total * (double)ObterPercentualSetor(pais, indice) / 100d);
    }

    public static float FatorProducao(DadosPaisGoverno pais, int indice)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return 1f;
        GerenciadorTempo tempo = GerenciadorTempo.Instancia;
        int diaAtual = tempo != null ? Mathf.Max(1, tempo.totalDias) : 1;
        if (pais.ultimoDiaOrcamentoProcessado < diaAtual) return 1f;
        return FatorProducaoPrevisto(pais, indice, ObterValorAplicadoSetor(pais, indice));
    }

    public static float FatorProducaoPrevisto(DadosPaisGoverno pais, int indice, long valorAplicado)
    {
        if (pais == null) return 1f;
        long baseSetor = ObterValorBaseSetor(pais, indice);
        long aplicado = Math.Max(0L, valorAplicado);
        if (baseSetor <= 0L) return 0.25f;
        return Mathf.Clamp(0.25f + 0.75f * (float)((double)aplicado / baseSetor), 0.25f, 2f);
    }

    public static bool ProcessarDia(SistemaGovernoMundial governo, DadosPaisGoverno pais, int dia)
    {
        if (governo == null || pais == null || dia < 1 || pais.ultimoDiaOrcamentoProcessado >= dia)
            return false;

        NormalizarPercentuais(pais);
        pais.ultimoDiaOrcamentoProcessado = dia;
        LimparValoresDiarios(pais);
        if (!pais.orcamentoInvestimentoAtivo) return true;

        long solicitado = Math.Max(0L, pais.orcamentoInvestimentoDiario);
        long disponivel = governo.ObterSaldoDisponivelParaOperacao(pais.teamId);
        long gasto = Math.Min(solicitado, disponivel);
        if (gasto > 0L && !governo.TentarPagar(pais.teamId, gasto))
            gasto = 0L;
        pais.orcamentoGastoHoje = gasto;
        long[] rateio = DistribuirValor(gasto, ObterPercentuaisSetores(pais));
        pais.orcamentoAgriculturaHoje = rateio[0];
        pais.orcamentoEnergiaHoje = rateio[1];
        pais.orcamentoPetroleoHoje = rateio[2];
        pais.orcamentoIndustriaHoje = rateio[3];
        pais.orcamentoCienciaHoje = rateio[4];
        pais.orcamentoMilitarHoje = rateio[5];

        long aporteFolha = AcumularReservaMilitar(pais, pais.orcamentoMilitarHoje);
        AcumularOrcamentoRecrutamento(pais, Math.Max(0L, pais.orcamentoMilitarHoje - aporteFolha));
        AvancarPesquisaFinanciada(pais, dia);
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public static bool TentarPagarFolhaComReserva(DadosPaisGoverno pais, long valor)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return false;
        long coberto = Math.Min(Math.Max(0L, valor), Math.Max(0L, pais.orcamentoReservaFolhaMilitar));
        long restante = Math.Max(0L, valor - coberto);
        if (restante > 0L)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || !governo.TentarPagar(pais.teamId, restante)) return false;
        }
        pais.orcamentoReservaFolhaMilitar -= coberto;
        return true;
    }

    public static bool ConsumirVerbaDeRecrutamento(DadosPaisGoverno pais, QuartelForcaV2 forca, long valor)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return true;
        if (valor <= 0L) return true;
        switch (forca)
        {
            case QuartelForcaV2.Naval:
                if (pais.orcamentoRecrutamentoMarinhaHoje < valor) return false;
                pais.orcamentoRecrutamentoMarinhaHoje -= valor;
                return true;
            case QuartelForcaV2.Aerea:
                if (pais.orcamentoRecrutamentoAereaHoje < valor) return false;
                pais.orcamentoRecrutamentoAereaHoje -= valor;
                return true;
            default:
                if (pais.orcamentoRecrutamentoExercitoHoje < valor) return false;
                pais.orcamentoRecrutamentoExercitoHoje -= valor;
                return true;
        }
    }

    public static bool TemVerbaDeRecrutamento(DadosPaisGoverno pais, QuartelForcaV2 forca)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return true;
        switch (forca)
        {
            case QuartelForcaV2.Naval: return pais.orcamentoRecrutamentoMarinhaHoje >= CustoRecrutamentoPorMilitar;
            case QuartelForcaV2.Aerea: return pais.orcamentoRecrutamentoAereaHoje >= CustoRecrutamentoPorMilitar;
            default: return pais.orcamentoRecrutamentoExercitoHoje >= CustoRecrutamentoPorMilitar;
        }
    }

    public static int ObterLimiteRecrutamentoDiario(DadosPaisGoverno pais)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return 0;
        long total = pais.orcamentoRecrutamentoExercitoHoje
            + pais.orcamentoRecrutamentoMarinhaHoje
            + pais.orcamentoRecrutamentoAereaHoje;
        return Mathf.Clamp((int)Math.Min(100L, total / CustoRecrutamentoPorMilitar), 0, 100);
    }

    public static int ObterLimiteRecrutamentoForca(DadosPaisGoverno pais, QuartelForcaV2 forca)
    {
        if (pais == null || !pais.orcamentoInvestimentoAtivo) return int.MaxValue;
        long saldo;
        switch (forca)
        {
            case QuartelForcaV2.Naval: saldo = pais.orcamentoRecrutamentoMarinhaHoje; break;
            case QuartelForcaV2.Aerea: saldo = pais.orcamentoRecrutamentoAereaHoje; break;
            default: saldo = pais.orcamentoRecrutamentoExercitoHoje; break;
        }
        return (int)Math.Min(100L, Math.Max(0L, saldo) / CustoRecrutamentoPorMilitar);
    }

    private static long AcumularReservaMilitar(DadosPaisGoverno pais, long verbaMilitar)
    {
        long folhaDiaria = 0L;
        QuartelAdministracaoRuntime[] quarteis = UnityEngine.Object.FindObjectsByType<QuartelAdministracaoRuntime>(FindObjectsSortMode.None);
        for (int i = 0; i < quarteis.Length; i++)
        {
            QuartelAdministracaoRuntime quartel = quarteis[i];
            if (quartel != null && quartel.isActiveAndEnabled && quartel.teamID == pais.teamId)
                folhaDiaria += Math.Max(0L, quartel.CustoFolhaDiario);
        }
        if (folhaDiaria <= 0L) return 0L;
        long aporte = Math.Min(Math.Max(0L, verbaMilitar), folhaDiaria);
        long teto = Math.Max(folhaDiaria, 1L) * DiasReservaFolha;
        long saldoReserva = Math.Min(teto, Math.Max(0L, pais.orcamentoReservaFolhaMilitar));
        long aporteEfetivo = Math.Min(aporte, Math.Max(0L, teto - saldoReserva));
        pais.orcamentoReservaFolhaMilitar = saldoReserva + aporteEfetivo;
        return aporteEfetivo;
    }

    private static void AcumularOrcamentoRecrutamento(DadosPaisGoverno pais, long verbaRecrutamento)
    {
        long[] rateio = DistribuirValor(Math.Max(0L, verbaRecrutamento),
            new[] { pais.orcamentoPctExercito, pais.orcamentoPctMarinha, pais.orcamentoPctAerea });
        pais.orcamentoRecrutamentoExercitoHoje = SomarComLimite(pais.orcamentoRecrutamentoExercitoHoje, rateio[0]);
        pais.orcamentoRecrutamentoMarinhaHoje = SomarComLimite(pais.orcamentoRecrutamentoMarinhaHoje, rateio[1]);
        pais.orcamentoRecrutamentoAereaHoje = SomarComLimite(pais.orcamentoRecrutamentoAereaHoje, rateio[2]);
    }

    private static void AvancarPesquisaFinanciada(DadosPaisGoverno pais, int dia)
    {
        long baseCiencia = ObterValorBaseSetor(pais, 4);
        float progresso = baseCiencia > 0L
            ? Mathf.Clamp((float)((double)pais.orcamentoCienciaHoje / baseCiencia), 0f, 2f)
            : 0f;
        if (progresso <= 0f) return;
        if (pais.pesquisas != null)
            foreach (PesquisaNacionalEstado item in pais.pesquisas)
                if (item != null && item.emAndamento) item.diasPesquisaFinanciados += progresso;
        if (pais.tecnologias != null)
            foreach (TecnologiaNacionalEstado item in pais.tecnologias)
                if (item != null && item.emAndamento) item.diasPesquisaFinanciados += progresso;
        if (pais.laboratorios != null)
            foreach (LaboratorioNacionalEstado item in pais.laboratorios)
                if (item != null && item.emExpansao) item.diasPesquisaFinanciados += progresso;
        pais.diasPesquisaFinanciadosAcumulados += progresso;
    }

    private static long ObterValorAplicadoSetor(DadosPaisGoverno pais, int indice)
    {
        if (pais == null) return 0L;
        switch (indice)
        {
            case 0: return pais.orcamentoAgriculturaHoje;
            case 1: return pais.orcamentoEnergiaHoje;
            case 2: return pais.orcamentoPetroleoHoje;
            case 3: return pais.orcamentoIndustriaHoje;
            case 4: return pais.orcamentoCienciaHoje;
            case 5: return pais.orcamentoMilitarHoje;
            default: return 0L;
        }
    }

    private static long ObterValorBaseSetor(DadosPaisGoverno pais, int indice)
    {
        return (long)Math.Floor(Math.Max(0L, pais.orcamentoInvestimentoDiario)
            * (double)PercentualBaseSetor(indice) / 100d);
    }

    private static int PercentualBaseSetor(int indice)
    {
        switch (indice)
        {
            case 0: return 20;
            case 1: return 20;
            case 2: return 15;
            case 3: return 15;
            case 4: return 10;
            case 5: return 20;
            default: return 0;
        }
    }

    private static int[] ObterPercentuaisSetores(DadosPaisGoverno pais)
    {
        return new[] { pais.orcamentoPctAgricultura, pais.orcamentoPctEnergia, pais.orcamentoPctPetroleo,
            pais.orcamentoPctIndustria, pais.orcamentoPctCiencia, pais.orcamentoPctMilitar };
    }

    private static void DefinirPercentuaisSetores(DadosPaisGoverno pais, int[] valores)
    {
        pais.orcamentoPctAgricultura = valores[0];
        pais.orcamentoPctEnergia = valores[1];
        pais.orcamentoPctPetroleo = valores[2];
        pais.orcamentoPctIndustria = valores[3];
        pais.orcamentoPctCiencia = valores[4];
        pais.orcamentoPctMilitar = valores[5];
    }

    private static void LimparValoresDiarios(DadosPaisGoverno pais)
    {
        pais.orcamentoGastoHoje = 0L;
        pais.orcamentoAgriculturaHoje = 0L;
        pais.orcamentoEnergiaHoje = 0L;
        pais.orcamentoPetroleoHoje = 0L;
        pais.orcamentoIndustriaHoje = 0L;
        pais.orcamentoCienciaHoje = 0L;
        pais.orcamentoMilitarHoje = 0L;
    }

    private static long SomarComLimite(long atual, long valor)
    {
        atual = Math.Max(0L, atual);
        valor = Math.Max(0L, valor);
        return atual > long.MaxValue - valor ? long.MaxValue : atual + valor;
    }

    private static long[] DistribuirValor(long total, int[] percentuais)
    {
        long[] resultado = new long[percentuais.Length];
        if (total <= 0L || percentuais.Length == 0) return resultado;
        long atribuido = 0L;
        for (int i = 0; i < percentuais.Length; i++)
        {
            resultado[i] = i == percentuais.Length - 1
                ? total - atribuido
                : (long)Math.Floor(total * (double)Mathf.Clamp(percentuais[i], 0, 100) / 100d);
            atribuido += resultado[i];
        }
        return resultado;
    }

    private static void NormalizarArray(int[] valores)
    {
        int soma = 0;
        for (int i = 0; i < valores.Length; i++)
        {
            valores[i] = Mathf.Clamp(valores[i], 0, 100);
            soma += valores[i];
        }
        if (soma == 100) return;
        if (soma <= 0)
        {
            for (int i = 0; i < valores.Length; i++) valores[i] = i == valores.Length - 1 ? 100 - 100 / valores.Length * (valores.Length - 1) : 100 / valores.Length;
            return;
        }
        int atribuido = 0;
        for (int i = 0; i < valores.Length; i++)
        {
            valores[i] = i == valores.Length - 1 ? 100 - atribuido : Mathf.FloorToInt(valores[i] * 100f / soma);
            atribuido += valores[i];
        }
    }

    private static void RedistribuirRestante(int[] valores, int indice, int novoValor)
    {
        novoValor = Mathf.Clamp(novoValor, 0, 100);
        int somaAnterior = 0;
        for (int i = 0; i < valores.Length; i++) if (i != indice) somaAnterior += Mathf.Max(0, valores[i]);
        int restante = 100 - novoValor;
        int atribuido = 0;
        int ultimoIndiceRestante = indice == valores.Length - 1 ? valores.Length - 2 : valores.Length - 1;
        for (int i = 0; i < valores.Length; i++)
        {
            if (i == indice) { valores[i] = novoValor; continue; }
            if (i == ultimoIndiceRestante)
            {
                valores[i] = Mathf.Max(0, restante - atribuido);
            }
            else
            {
                valores[i] = somaAnterior > 0
                    ? Mathf.FloorToInt(Mathf.Max(0, valores[i]) * restante / (float)somaAnterior)
                    : Mathf.FloorToInt(restante / (float)(valores.Length - 1));
                atribuido += valores[i];
            }
        }
    }
}
