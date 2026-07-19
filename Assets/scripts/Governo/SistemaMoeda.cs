using System.Collections.Generic;
using UnityEngine;

public static class SistemaMoeda
{
    private static Dictionary<int, float> tempoUltimaAtualizacao = new Dictionary<int, float>();

    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null) return;

        // Se é a primeira vez, inicializa e ajusta para base baixa (10-15%)
        if (!tempoUltimaAtualizacao.ContainsKey(pais.teamId) || Time.time < tempoUltimaAtualizacao[pais.teamId])
        {
            tempoUltimaAtualizacao[pais.teamId] = Time.time;
            if (pais.valorMoeda <= 0f) pais.valorMoeda = 0.80f;
            return;
        }

        // Atualiza a cada 5 dias in-game (1 dia = 120s -> 5 dias = 600s)
        if (Time.time - tempoUltimaAtualizacao[pais.teamId] < 3f)
        {
            return;
        }

        tempoUltimaAtualizacao[pais.teamId] = Time.time;

        // Fatores: Energia, Impostos, Comida, Metais, Militares, Empregos, Casas, População, Status
        float energiaScore = (economia != null && economia.energiaConsumida > 0) ? Mathf.Clamp01(economia.energiaProduzida / economia.energiaConsumida) : 1f;
        
        float impostoMedio = (pais.impostoMoradia + pais.impostoIndustria + pais.impostoComercio) / 3f;
        float impostoScore = 1f - Mathf.Clamp01(impostoMedio / 35f);
        
        float comidaScore = (economia != null && economia.deficitComida <= 0) ? 1f : 0f;
        float metaisScore = (economia != null && economia.deficitPetroleo <= 0) ? 1f : 0f; 
        
        float militaresScore = Mathf.Clamp01((pais.populacaoMilitarAtiva + pais.reservistas) / Mathf.Max(1f, pais.populacao) * 10f);
        float empregosScore = pais.emprego / 100f;
        float casasScore = pais.moradia / 100f;
        float populacaoScore = Mathf.Clamp01(pais.populacao / 500000f);
        float caixaScore = Mathf.Clamp01(Mathf.Max(0f, pais.saldo) / Mathf.Max(1f, pais.populacao * 8f + 1000f));
        float dividaScore = Mathf.Clamp01(Mathf.Max(0f, pais.divida) / Mathf.Max(1f, pais.saldo + pais.divida + 1000f));
        float legitimidadeScore = Mathf.Clamp01(pais.legitimidadeGlobal / 100f);
        
        float statusScore = pais.estabilidade / 100f;
        float lastroOuro = Mathf.Clamp01(pais.reservaOuro / 1200f);

        float alvo = 0.10f
            + energiaScore * 0.15f
            + impostoScore * 0.10f
            + comidaScore * 0.15f
            + metaisScore * 0.10f
            + militaresScore * 0.05f
            + empregosScore * 0.20f
            + casasScore * 0.10f
            + populacaoScore * 0.15f
            + statusScore * 0.15f
            + lastroOuro * 0.15f
            + caixaScore * 0.25f
            + legitimidadeScore * 0.20f
            - dividaScore * 0.30f
            - (pais.inflacao / 30f) * 0.40f
            - (pais.emGuerra ? 0.30f : 0f)
            - (pais.sancionado ? 0.22f : 0f);

        float anterior = Mathf.Max(0.01f, pais.valorMoeda);
        pais.valorMoeda = Mathf.Lerp(pais.valorMoeda, Mathf.Clamp(alvo, 0.10f, 3.5f), 0.18f);
        pais.variacaoMoeda = ((pais.valorMoeda - anterior) / anterior) * 100f;
    }
}
