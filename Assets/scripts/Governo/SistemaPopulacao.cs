using UnityEngine;

/// <summary>
/// Sistema de Crescimento Populacional – Estilo Cities Skylines
///
/// TRES CAMADAS:
///   1. Indice de Atratividade  -> determina se o pais atrai ou expulsa moradores
///   2. Pressao Habitacional    -> determina o quanto da capacidade esta ocupada e se ha superpopulacao
///   3. Choques                 -> eventos criticos que derrubam ou aceleram a populacao instantaneamente
///
/// Tudo conectado a felicidade: felicidade alta -> mais imigracao e natalidade.
/// Empregos e moradia retroalimentam a felicidade, que retroalimenta o crescimento.
/// </summary>
public static class SistemaPopulacao
{
    // --- Constantes de Calibracao ---
    private const float TAXA_CRESCIMENTO_BASE   = 0.015f;
    private const float TAXA_EMIGRACAO_BASE      = 0.012f;
    private const float OCUP_MINIMA_SEM_MORADIA  = 0.30f;
    private const float MARGEM_SUPERPOPULACAO    = 1.05f;

    // --- Aeroporto (cache) ---
    private static GerenciadorAeroportoComercial _aeroportoCache;
    private static int _aeroportoFrame = -1;

    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null || economia == null) return;

        // CONSUMO DE COMIDA
        float necessidadeComida = (pais.populacaoCivil / 100f * 1f)
                                + (pais.populacaoMilitarAtiva / 100f * 2f);
        economia.deficitComida = UnityEngine.Mathf.Max(0f, necessidadeComida - economia.comidaProduzida);
        economia.comidaConsumida = necessidadeComida;

        int consumoReal = UnityEngine.Mathf.Min(pais.comida, UnityEngine.Mathf.CeilToInt(necessidadeComida));
        pais.comida -= consumoReal;

        if (SistemaGovernoMundial.Instancia != null
            && pais.teamId == SistemaGovernoMundial.Instancia.teamJogador
            && GerenciadorRecursos.Instancia != null
            && consumoReal > 0)
        {
            GerenciadorRecursos.Instancia.RemoverRecurso("Comida", consumoReal);
        }

        // CAMADA 1 - INDICE DE ATRATIVIDADE NACIONAL (0 a 1)
        float indiceAtratividade = CalcularIndiceAtratividade(pais, economia);
        pais.indiceAtratividade = indiceAtratividade;

        // CAMADA 2 - PRESSAO HABITACIONAL E CRESCIMENTO / EMIGRACAO
        int variacaoMigratoria = 0;
        int popMax = UnityEngine.Mathf.Max(1, pais.populacaoMaxima);

        pais.pressaoHabitacional = pais.populacaoMaxima > 0
            ? (float)pais.populacao / pais.populacaoMaxima
            : 1f;

        float fatorAeroporto = ObterFatorAeroporto();
        bool emSuperpopulacao = pais.pressaoHabitacional > MARGEM_SUPERPOPULACAO;

        if (indiceAtratividade >= 0.35f && !emSuperpopulacao)
        {
            float bonusMigracao = pais.felicidade > 80f
                ? UnityEngine.Mathf.Lerp(1f, 2.5f, (pais.felicidade - 80f) / 20f)
                : 1f;

            float fatorTotal = indiceAtratividade * bonusMigracao * fatorAeroporto;
            float espacoLivre = UnityEngine.Mathf.Max(0f, popMax - pais.populacao);
            float deltaBase   = espacoLivre * TAXA_CRESCIMENTO_BASE * pais.natalidade;
            variacaoMigratoria = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(deltaBase * fatorTotal));
            pais.taxaMigracao = variacaoMigratoria * fatorAeroporto;
        }
        else if (indiceAtratividade < 0.35f || emSuperpopulacao)
        {
            float taxaEmig;
            if (emSuperpopulacao)
            {
                taxaEmig = (pais.pressaoHabitacional - 1f) * 0.05f;
            }
            else
            {
                taxaEmig = UnityEngine.Mathf.Lerp(TAXA_EMIGRACAO_BASE * 0.3f, TAXA_EMIGRACAO_BASE * 2f,
                    1f - (indiceAtratividade / 0.35f));
            }

            variacaoMigratoria = -UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                UnityEngine.Mathf.Max(1f, pais.populacao) * taxaEmig * pais.mortalidade));
            pais.taxaMigracao = variacaoMigratoria;
        }
        else
        {
            pais.taxaMigracao = 0f;
        }

        // CAMADA 3 - CHOQUES DEMOGRAFICOS
        int evacuacaoForcada = 0;
        int mortesPorCrise = 0;

        if (pais.comida <= 0 && economia.deficitComida > 0f)
        {
            mortesPorCrise += UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(pais.populacao * 0.005f));
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade + 0.15f, 1f, 8f);
        }
        else if (pais.comida < necessidadeComida * 2f && economia.deficitComida > 0f)
        {
            mortesPorCrise += UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(pais.populacao * 0.002f));
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade + 0.05f, 1f, 8f);
        }
        else
        {
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade - 0.05f, 1f, 5f);
        }

        if (economia.estruturasSemEnergia > 0 && economia.deficitEnergia > 2f)
        {
            evacuacaoForcada += UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(pais.populacao * 0.002f));
        }

        if (pais.emGuerra)
        {
            mortesPorCrise += UnityEngine.Mathf.Max(2, UnityEngine.Mathf.RoundToInt(pais.populacao * 0.008f));
        }

        int deltaPopulacao = variacaoMigratoria - evacuacaoForcada - mortesPorCrise;

        // APLICAR DELTA NA POPULACAO
        int limiteMax = UnityEngine.Mathf.RoundToInt(popMax * MARGEM_SUPERPOPULACAO);
        if (pais.moradia < 10f)
        {
            limiteMax = UnityEngine.Mathf.Max(limiteMax, UnityEngine.Mathf.RoundToInt(popMax * OCUP_MINIMA_SEM_MORADIA));
        }

        if (deltaPopulacao != 0)
        {
            pais.populacaoCivil = UnityEngine.Mathf.Clamp(pais.populacaoCivil + deltaPopulacao, 0, limiteMax);
            pais.populacao      = pais.populacaoCivil
                                + pais.populacaoMilitarAtiva
                                + pais.reservistas
                                + pais.alistaveis;
            pais.populacao      = UnityEngine.Mathf.Clamp(pais.populacao, 0, limiteMax);
        }

        if (mortesPorCrise > 0)
        {
            pais.mortosAcumulados += mortesPorCrise;
        }

        if (SistemaGovernoMundial.Instancia != null
            && pais.teamId == SistemaGovernoMundial.Instancia.teamJogador
            && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.populacaoAtual = pais.populacao;
            GerenciadorRecursos.Instancia.populacaoMaxima = pais.populacaoMaxima;
        }

        // SATISFACAO DE SERVICOS (indice composto, 0-100)
        float satisfacao = 0f;
        satisfacao += UnityEngine.Mathf.Clamp01(pais.emprego / 100f)       * 30f;
        satisfacao += UnityEngine.Mathf.Clamp01(pais.moradia / 100f)       * 25f;
        satisfacao += UnityEngine.Mathf.Clamp01(pais.qualidadeVida / 100f) * 20f;
        satisfacao += (pais.comida > 0 ? 15f : 0f);
        satisfacao += (economia.deficitEnergia <= 0f ? 10f
            : UnityEngine.Mathf.Max(0f, 10f - economia.deficitEnergia));
        pais.indiceSatisfacaoServicos = UnityEngine.Mathf.Clamp(satisfacao, 0f, 100f);
    }

    // --- CALCULO DO INDICE DE ATRATIVIDADE (0 a 1) ---
    private static float CalcularIndiceAtratividade(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        float score = 0f;

        // Felicidade tem o maior peso (30%)
        score += UnityEngine.Mathf.Clamp01(pais.felicidade / 100f) * 0.30f;

        // Emprego (25%) - empregos disponiveis > populacao = muito atraente
        float taxaEmprego = economia.empregosDisponiveis > 0
            ? UnityEngine.Mathf.Clamp01((float)economia.empregosDisponiveis / UnityEngine.Mathf.Max(1, pais.populacao))
            : UnityEngine.Mathf.Clamp01(pais.emprego / 100f);
        score += taxaEmprego * 0.25f;

        // Moradia disponivel (20%)
        float taxaMoradia = economia.moradiaTotal > 0
            ? UnityEngine.Mathf.Clamp01((float)(economia.moradiaTotal - pais.populacao) / UnityEngine.Mathf.Max(1, economia.moradiaTotal))
            : UnityEngine.Mathf.Clamp01(pais.moradia / 100f) * 0.5f;
        score += UnityEngine.Mathf.Max(0f, taxaMoradia) * 0.20f;

        // Comida suficiente (15%)
        float fatorComida = pais.comida > 0
            ? (economia.deficitComida <= 0f ? 1f
                : UnityEngine.Mathf.Clamp01(pais.comida / UnityEngine.Mathf.Max(1f, economia.deficitComida * 5f)))
            : 0f;
        score += fatorComida * 0.15f;

        // Energia estavel (10%)
        float fatorEnergia = economia.deficitEnergia <= 0f ? 1f
            : UnityEngine.Mathf.Clamp01(1f - economia.deficitEnergia / UnityEngine.Mathf.Max(1f, economia.energiaProduzida));
        score += fatorEnergia * 0.10f;

        // Penalidades diretas
        if (pais.emGuerra)    score -= 0.30f;
        if (pais.sancionado)  score -= 0.10f;
        if (pais.inflacao > 20f) score -= (pais.inflacao - 20f) * 0.005f;

        return UnityEngine.Mathf.Clamp01(score);
    }

    // --- FATOR DO AEROPORTO COMERCIAL ---
    private static float ObterFatorAeroporto()
    {
        if (Time.frameCount != _aeroportoFrame)
        {
            _aeroportoFrame = Time.frameCount;
#if UNITY_2023_1_OR_NEWER
            _aeroportoCache = UnityEngine.Object.FindFirstObjectByType<GerenciadorAeroportoComercial>();
#else
            _aeroportoCache = UnityEngine.Object.FindObjectOfType<GerenciadorAeroportoComercial>();
#endif
        }

        if (_aeroportoCache == null || !_aeroportoCache.isActiveAndEnabled)
            return 1f;

        int passagens = _aeroportoCache.estatisticaPassagensVendidasDia;
        int contratos  = _aeroportoCache.contratosAtivos != null ? _aeroportoCache.contratosAtivos.Count : 0;

        if (passagens > 1000 || contratos >= 4) return 4f;
        if (passagens > 500  || contratos >= 3) return 3f;
        if (passagens > 200  || contratos >= 2) return 2f;
        if (passagens > 0    || contratos >= 1) return 1.5f;
        return 0.8f;
    }
}
