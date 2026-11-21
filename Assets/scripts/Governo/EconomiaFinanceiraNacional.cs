using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum TipoLancamentoFinanceiro
{
    ReceitaDiaria,
    DespesaDiaria,
    ReceitaSemanal,
    DespesaSemanal,
    ReceitaTransacao,
    DespesaTransacao,
    Mercado,
    Emprestimo,
    Reembolso,
    Ajuste
}

[Serializable]
public sealed class LancamentoFinanceiroNacional
{
    public int diaJogo;
    public long valor;
    public string categoria = string.Empty;
    public string origem = string.Empty;
    public TipoLancamentoFinanceiro tipo;
}

[Serializable]
public sealed class ProjecaoEconomicaNacional
{
    public int dias;
    public long tesouroAtual;
    public long receitaDiaria;
    public long despesaDiaria;
    public long receitaSemanal;
    public long despesaSemanal;
    public long saldoDiario;
    public long tesouroProjetado;
    public float felicidade;
    public float estabilidade;
    public float desemprego;
}

/// <summary>Formatação e cálculo de projeções sem alterar o estado do país.</summary>
public static class EconomiaFinanceiraNacional
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static string FormatarCompacto(long valor)
    {
        decimal absoluto = Math.Abs((decimal)valor);
        string sufixo = string.Empty;
        decimal escala = 1m;
        if (absoluto >= 1000000000000m) { escala = 1000000000000m; sufixo = "T"; }
        else if (absoluto >= 1000000000m) { escala = 1000000000m; sufixo = "B"; }
        else if (absoluto >= 1000000m) { escala = 1000000m; sufixo = "M"; }
        else if (absoluto >= 1000m) { escala = 1000m; sufixo = "K"; }
        if (escala == 1m) return valor.ToString("N0", Cultura);
        decimal compacto = Math.Round(absoluto / escala, 2, MidpointRounding.AwayFromZero);
        return (valor < 0 ? "-" : string.Empty) + compacto.ToString("0.##", Cultura) + sufixo;
    }

    public static ProjecaoEconomicaNacional Projetar(
        DadosPaisGoverno pais, int dias, long ajusteReceitaDiaria = 0L,
        long ajusteDespesaDiaria = 0L)
    {
        if (pais == null) return new ProjecaoEconomicaNacional { dias = Mathf.Clamp(dias, 1, 7) };
        int periodo = Mathf.Clamp(dias, 1, 7);
        long receita = SaturarSomar(pais.receitaDiariaTesouro, ajusteReceitaDiaria);
        long despesa = SaturarSomar(pais.despesaDiariaTesouro, ajusteDespesaDiaria);
        long receitaSemana = Math.Max(0L, pais.receitaSemanalTesouro);
        long despesaSemana = Math.Max(0L, pais.despesaSemanalTesouro);
        // Fluxo diário inclui a média diária dos tributos semanais. Retira-se essa
        // média antes de acrescentar a parcela que vence dentro do horizonte.
        long tributoSemanalMedioDia = pais.receitaSemanalTesouro > 0L ? pais.receitaSemanalTesouro / 7L : 0L;
        long gastoSemanalMedioDia = pais.despesaSemanalTesouro > 0L ? pais.despesaSemanalTesouro / 7L : 0L;
        long saldo = SaturarSubtrair(receita, despesa);
        long projetado = SaturarSomar(pais.saldo, SaturarMultiplicar(saldo, periodo));
        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        long ajusteSemanalMedio = SaturarSubtrair(tributoSemanalMedioDia, gastoSemanalMedioDia);
        projetado = SaturarSubtrair(projetado, SaturarMultiplicar(ajusteSemanalMedio, periodo));
        int diasAteSemana = 7 - (diaAtual % 7);
        int semanasNoPeriodo = periodo <= diasAteSemana ? 0 : 1 + (periodo - diasAteSemana - 1) / 7;
        projetado = SaturarSomar(projetado, SaturarMultiplicar(receitaSemana, semanasNoPeriodo));
        projetado = SaturarSubtrair(projetado, SaturarMultiplicar(despesaSemana, semanasNoPeriodo));
        float populacao = Mathf.Max(1, pais.populacaoCivil);
        return new ProjecaoEconomicaNacional
        {
            dias = periodo,
            tesouroAtual = pais.saldo,
            receitaDiaria = receita,
            despesaDiaria = despesa,
            receitaSemanal = receitaSemana,
            despesaSemanal = despesaSemana,
            saldoDiario = saldo,
            tesouroProjetado = Math.Max(0L, projetado),
            felicidade = pais.felicidade,
            estabilidade = pais.estabilidade,
            desemprego = Mathf.Clamp(100f - pais.emprego, 0f, 100f)
        };
    }

    public static void Registrar(DadosPaisGoverno pais, long valor, string categoria,
        string origem, TipoLancamentoFinanceiro tipo, int diaJogo)
    {
        if (pais == null || valor == 0L) return;
        if (pais.historicoFinanceiro == null) pais.historicoFinanceiro = new List<LancamentoFinanceiroNacional>();
        pais.historicoFinanceiro.Add(new LancamentoFinanceiroNacional
        {
            diaJogo = Mathf.Max(1, diaJogo), valor = valor,
            categoria = categoria ?? string.Empty, origem = origem ?? string.Empty, tipo = tipo
        });
        const int limiteHistorico = 256;
        if (pais.historicoFinanceiro.Count > limiteHistorico)
            pais.historicoFinanceiro.RemoveRange(0, pais.historicoFinanceiro.Count - limiteHistorico);
    }

    private static long SaturarSomar(long a, long b)
    {
        if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
        if (b < 0 && a < long.MinValue - b) return long.MinValue;
        return a + b;
    }
    private static long SaturarSubtrair(long a, long b)
    {
        if (b < 0 && a > long.MaxValue + b) return long.MaxValue;
        if (b > 0 && a < long.MinValue + b) return long.MinValue;
        return a - b;
    }
    private static long SaturarMultiplicar(long valor, int multiplicador)
    {
        if (valor > 0 && valor > long.MaxValue / multiplicador) return long.MaxValue;
        if (valor < 0 && valor < long.MinValue / multiplicador) return long.MinValue;
        return valor * multiplicador;
    }
}
