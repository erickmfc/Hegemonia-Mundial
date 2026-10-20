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

    /// <summary>
    /// Crise que impede ganhos demográficos e de felicidade. Um déficit de
    /// produção só é fome quando o estoque caiu abaixo de duas rodadas de
    /// consumo; reservas suficientes continuam sustentando o crescimento.
    /// </summary>
    public static bool HaCriseBasica(DadosPaisGoverno pais, DadosEconomiaPais economia, bool houveMortesNoCiclo)
    {
        if (houveMortesNoCiclo) return true;
        if (pais == null || economia == null) return false;

        float reservaMinimaComida = UnityEngine.Mathf.Max(1f, economia.comidaConsumida * 2f);
        bool fome = economia.deficitComida > 0f && pais.comida < reservaMinimaComida;
        bool faltaEnergia = economia.deficitEnergia > 0f || economia.estruturasSemEnergia > 0;
        return fome || faltaEnergia;
    }

    public static int BloquearCrescimentoEmCrise(
        DadosPaisGoverno pais, DadosEconomiaPais economia, int variacaoMigratoria, bool houveMortesNoCiclo)
    {
        return variacaoMigratoria > 0 && HaCriseBasica(pais, economia, houveMortesNoCiclo)
            ? 0
            : variacaoMigratoria;
    }

    public static float ImpedirGanhoFelicidadeEmCrise(
        DadosPaisGoverno pais, DadosEconomiaPais economia, bool houveMortesNoCiclo,
        float felicidadeAnterior, float felicidadeCalculada)
    {
        return HaCriseBasica(pais, economia, houveMortesNoCiclo)
            ? UnityEngine.Mathf.Min(felicidadeAnterior, felicidadeCalculada)
            : felicidadeCalculada;
    }

    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        Processar(pais, economia, true);
    }

    /// <summary>
    /// Atualiza os indicadores demográficos no ciclo econômico. O consumo de
    /// comida pode ser adiado para a virada do dia, onde a agricultura credita
    /// primeiro a colheita diária.
    /// </summary>
    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia, bool processarConsumoComida)
    {
        if (pais == null || economia == null) return;

        int populacaoCivilAtual = UnityEngine.Mathf.Max(0, pais.populacaoCivil);

        // CONSUMO DE COMIDA
        float necessidadeComida = CalcularNecessidadeComida(pais);
        economia.deficitComida = UnityEngine.Mathf.Max(0f, necessidadeComida - economia.comidaProduzida);
        economia.comidaConsumida = necessidadeComida;
        pais.deficitComida = economia.deficitComida;

        if (processarConsumoComida)
        {
            ConsumirComidaDoEstoque(pais, UnityEngine.Mathf.CeilToInt(necessidadeComida));
        }

        // CAMADA 1 - INDICE DE ATRATIVIDADE NACIONAL (0 a 1)
        float indiceAtratividade = CalcularIndiceAtratividade(pais, economia);
        pais.indiceAtratividade = indiceAtratividade;

        // CAMADA 2 - PRESSAO HABITACIONAL E CRESCIMENTO / EMIGRACAO
        int variacaoMigratoria = 0;
        int popMax = UnityEngine.Mathf.Max(1, pais.populacaoMaxima);
        int capacidadeResidencial = UnityEngine.Mathf.Max(0, economia.moradiaTotal);

        // Prefeitura permite uma administracao inicial, mas nao e moradia.
        // Sem casas, a populacao existente sofre pressao habitacional extrema.
        pais.pressaoHabitacional = capacidadeResidencial > 0
            ? (float)pais.populacaoCivil / capacidadeResidencial
            : (pais.populacaoCivil > 0 ? 2f : 0f);

        float fatorAeroporto = ObterFatorAeroporto(pais.teamId);
        SaudePopulacionalResumo saude = CidadeSaudePopulacional.ObterResumo(pais.teamId, populacaoCivilAtual);
        bool emSuperpopulacao = pais.pressaoHabitacional > MARGEM_SUPERPOPULACAO;

        if (indiceAtratividade >= 0.35f && !emSuperpopulacao)
        {
            float bonusMigracao = pais.felicidade > 80f
                ? UnityEngine.Mathf.Lerp(1f, 2.5f, (pais.felicidade - 80f) / 20f)
                : 1f;

            float fatorTotal = indiceAtratividade * bonusMigracao * fatorAeroporto * saude.fatorCrescimento;
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

            variacaoMigratoria = -UnityEngine.Mathf.RoundToInt(populacaoCivilAtual * taxaEmig);
            pais.taxaMigracao = variacaoMigratoria;
        }
        else
        {
            pais.taxaMigracao = 0f;
        }

        // CAMADA 3 - CHOQUES DEMOGRAFICOS
        int evacuacaoForcada = 0;
        int mortesPorCrise = 0;

        if (processarConsumoComida)
            mortesPorCrise = AplicarConsequenciasDaFome(pais, necessidadeComida, economia.deficitComida);

        if (populacaoCivilAtual > 0 && economia.estruturasSemEnergia > 0 && economia.deficitEnergia > 2f)
        {
            evacuacaoForcada += UnityEngine.Mathf.RoundToInt(populacaoCivilAtual * 0.002f);
        }

        if (populacaoCivilAtual > 0 && pais.emGuerra)
        {
            mortesPorCrise += UnityEngine.Mathf.RoundToInt(populacaoCivilAtual * 0.008f);
        }

        mortesPorCrise = UnityEngine.Mathf.Clamp(mortesPorCrise, 0, populacaoCivilAtual);
        evacuacaoForcada = UnityEngine.Mathf.Clamp(evacuacaoForcada, 0, UnityEngine.Mathf.Max(0, populacaoCivilAtual - mortesPorCrise));

        int mortesPorDoenca = populacaoCivilAtual > 0
            ? UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(populacaoCivilAtual * saude.mortalidadeExtra), 0, populacaoCivilAtual)
            : 0;
        int variacaoMigratoriaAntesDaCrise = variacaoMigratoria;
        variacaoMigratoria = BloquearCrescimentoEmCrise(
            pais, economia, variacaoMigratoria, mortesPorCrise + mortesPorDoenca > 0);
        if (variacaoMigratoriaAntesDaCrise > 0 && variacaoMigratoria == 0)
            pais.taxaMigracao = 0f;

        int deltaPopulacao = variacaoMigratoria - evacuacaoForcada - mortesPorCrise;
        deltaPopulacao -= mortesPorDoenca;
        mortesPorCrise += mortesPorDoenca;

        // APLICAR DELTA NA POPULACAO
        int limiteMax = UnityEngine.Mathf.RoundToInt(popMax * MARGEM_SUPERPOPULACAO);
        if (capacidadeResidencial <= 0)
            limiteMax = UnityEngine.Mathf.Min(limiteMax, popMax);

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
        if (saude.riscoDoenca > 0f)
        {
            pais.felicidade = UnityEngine.Mathf.Clamp(pais.felicidade - saude.penalidadeFelicidade * 0.05f, 0f, 100f);
            economia.custoSocial += saude.custoDiario / 24f;
        }
    }

    /// <summary>
    /// Consome a necessidade de um dia uma vez, junto da colheita. As culturas
    /// são consumidas do armazém específico e do estoque geral em conjunto.
    /// </summary>
    public static void ProcessarConsumoComidaDiaria(
        DadosPaisGoverno pais, float producaoComidaDiaria, DadosEconomiaPais economia)
    {
        if (pais == null) return;

        float necessidadeComida = CalcularNecessidadeComida(pais);
        float producao = UnityEngine.Mathf.Max(0f, producaoComidaDiaria);
        float deficit = UnityEngine.Mathf.Max(0f, necessidadeComida - producao);
        if (economia != null)
        {
            economia.comidaProduzida = producao;
            economia.comidaConsumida = necessidadeComida;
            economia.deficitComida = deficit;
        }
        pais.deficitComida = deficit;

        ConsumirComidaDoEstoque(pais, UnityEngine.Mathf.CeilToInt(necessidadeComida));
        int mortesPorFome = AplicarConsequenciasDaFome(pais, necessidadeComida, deficit);
        AplicarMortesPorFome(pais, mortesPorFome);
    }

    /// <summary>
    /// Inicia o consumo diário antes da produção, liberando espaço de armazém.
    /// O retorno é a quantidade de toneladas que ainda precisa ser atendida.
    /// </summary>
    public static int IniciarConsumoComidaDiario(DadosPaisGoverno pais)
    {
        if (pais == null) return 0;
        int necessidade = UnityEngine.Mathf.CeilToInt(CalcularNecessidadeComida(pais));
        int consumido = ConsumirComidaDoEstoque(pais, necessidade);
        return UnityEngine.Mathf.Max(0, necessidade - consumido);
    }

    /// <summary>
    /// Finaliza o consumo diário com a comida recém-produzida e calcula a falta
    /// real da refeição depois de contabilizar estoque e colheita.
    /// </summary>
    public static void FinalizarConsumoComidaDiario(
        DadosPaisGoverno pais, int necessidadeAindaNaoAtendida,
        float producaoComidaDiaria, DadosEconomiaPais economia)
    {
        if (pais == null) return;

        float necessidadeComida = CalcularNecessidadeComida(pais);
        float producao = UnityEngine.Mathf.Max(0f, producaoComidaDiaria);
        float deficit = UnityEngine.Mathf.Max(0f, necessidadeComida - producao);
        if (economia != null)
        {
            economia.comidaProduzida = producao;
            economia.comidaConsumida = necessidadeComida;
            economia.deficitComida = deficit;
        }
        pais.deficitComida = deficit;

        int pendente = UnityEngine.Mathf.Max(0, necessidadeAindaNaoAtendida);
        int consumidoDaProducao = ConsumirComidaDoEstoque(pais, pendente);
        bool faltouComidaNoDia = consumidoDaProducao < pendente;
        int mortesPorFome = faltouComidaNoDia
            ? AplicarConsequenciasDaFome(pais, necessidadeComida, UnityEngine.Mathf.Max(deficit, pendente - consumidoDaProducao))
            : 0;
        if (!faltouComidaNoDia && deficit > 0f && pais.comida < necessidadeComida * 2f)
            mortesPorFome = AplicarConsequenciasDaFome(pais, necessidadeComida, deficit);
        else if (!faltouComidaNoDia)
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade - 0.05f, 1f, 5f);

        AplicarMortesPorFome(pais, mortesPorFome);
    }

    private static void AplicarMortesPorFome(DadosPaisGoverno pais, int mortesPorFome)
    {
        if (mortesPorFome <= 0) return;

        int mortesAplicadas = UnityEngine.Mathf.Min(
            UnityEngine.Mathf.Max(0, pais.populacaoCivil), mortesPorFome);
        if (mortesAplicadas <= 0) return;

        pais.populacaoCivil = UnityEngine.Mathf.Max(0, pais.populacaoCivil - mortesAplicadas);
        pais.populacao = UnityEngine.Mathf.Max(0, pais.populacao - mortesAplicadas);
        pais.mortosAcumulados += mortesAplicadas;

        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo != null && pais.teamId == governo.teamJogador
            && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.populacaoAtual = pais.populacao;
            GerenciadorRecursos.Instancia.populacaoMaxima = pais.populacaoMaxima;
        }
        CidadeComplexoUrbano.SincronizarPopulacaoNacional(pais.teamId, pais.populacaoCivil);
    }

    private static float CalcularNecessidadeComida(DadosPaisGoverno pais)
    {
        if (pais == null) return 0f;
        return (UnityEngine.Mathf.Max(0, pais.populacaoCivil) / 100f)
            + (UnityEngine.Mathf.Max(0, pais.populacaoMilitarAtiva) / 100f * 2f);
    }

    private static int AplicarConsequenciasDaFome(
        DadosPaisGoverno pais, float necessidadeComida, float deficitComida)
    {
        int populacaoCivil = UnityEngine.Mathf.Max(0, pais != null ? pais.populacaoCivil : 0);
        if (pais == null || populacaoCivil <= 0) return 0;

        if (pais.comida <= 0 && deficitComida > 0f)
        {
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade + 0.45f, 1f, 20f);
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(populacaoCivil * 0.03f));
        }

        if (pais.comida < necessidadeComida * 2f && deficitComida > 0f)
        {
            pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade + 0.12f, 1f, 20f);
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(populacaoCivil * 0.008f));
        }

        pais.mortalidade = UnityEngine.Mathf.Clamp(pais.mortalidade - 0.05f, 1f, 5f);
        return 0;
    }

    private static int ConsumirComidaDoEstoque(DadosPaisGoverno pais, int quantidadeSolicitada)
    {
        if (pais == null || quantidadeSolicitada <= 0) return 0;

        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        {
            int consumidaSemGoverno = UnityEngine.Mathf.Min(
                UnityEngine.Mathf.Max(0, pais.comida), quantidadeSolicitada);
            pais.comida = UnityEngine.Mathf.Max(0, pais.comida - quantidadeSolicitada);
            return consumidaSemGoverno;
        }

        governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
        int consumo = UnityEngine.Mathf.Min(
            UnityEngine.Mathf.Max(0, pais.comida), quantidadeSolicitada);
        if (consumo <= 0) return 0;

        int quantidadeCulturas = AgriculturaNacional.QuantidadeCulturas;
        int[] estoques = new int[quantidadeCulturas];
        int[] alocacoes = new int[quantidadeCulturas];
        long estoqueCulturasTotalLong = 0L;
        for (int i = 0; i < quantidadeCulturas; i++)
        {
            estoques[i] = UnityEngine.Mathf.Max(0,
                governo.ObterEstoque(pais.teamId, AgriculturaNacional.ObterIdCultura(i)));
            estoqueCulturasTotalLong = System.Math.Min(
                int.MaxValue, estoqueCulturasTotalLong + estoques[i]);
        }

        int estoqueCulturasTotal = (int)estoqueCulturasTotalLong;
        int consumirCulturas = UnityEngine.Mathf.Min(consumo, estoqueCulturasTotal);
        int alocado = 0;
        if (estoqueCulturasTotal > 0 && consumirCulturas > 0)
        {
            for (int i = 0; i < quantidadeCulturas; i++)
            {
                alocacoes[i] = UnityEngine.Mathf.Min(estoques[i],
                    UnityEngine.Mathf.FloorToInt((float)consumirCulturas * estoques[i] / estoqueCulturasTotal));
                alocado += alocacoes[i];
            }

            int restanteCulturas = consumirCulturas - alocado;
            while (restanteCulturas > 0)
            {
                int maiorDisponivel = -1;
                for (int i = 0; i < quantidadeCulturas; i++)
                {
                    if (alocacoes[i] >= estoques[i]) continue;
                    if (maiorDisponivel < 0
                        || estoques[i] - alocacoes[i] > estoques[maiorDisponivel] - alocacoes[maiorDisponivel])
                    {
                        maiorDisponivel = i;
                    }
                }

                if (maiorDisponivel < 0) break;
                alocacoes[maiorDisponivel]++;
                restanteCulturas--;
            }

            for (int i = 0; i < quantidadeCulturas; i++)
            {
                if (alocacoes[i] <= 0) continue;
                governo.RemoverEstoque(pais.teamId, AgriculturaNacional.ObterIdCultura(i), alocacoes[i]);
            }
        }

        int consumoGenerico = consumo - consumirCulturas;
        if (consumoGenerico > 0)
            governo.RemoverEstoque(pais.teamId, RecursoMercado.Comida, consumoGenerico);
        governo.ReconciliarEstoqueComidaCulturas(pais.teamId);
        return consumo;
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
            : 0f;
        score += taxaEmprego * 0.25f;

        // Moradia disponivel (20%)
        float taxaMoradia = economia.moradiaTotal > 0
            ? UnityEngine.Mathf.Clamp01((float)(economia.moradiaTotal - pais.populacao) / UnityEngine.Mathf.Max(1, economia.moradiaTotal))
            : 0f;
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

        // Empregos e produtos tornam a cidade atraente mesmo durante sua
        // recuperacao, mas o bonus comercial e deliberadamente limitado.
        DadosComercioNacional comercio = SistemaComercioNacional.ObterResumo(pais.teamId);
        score += UnityEngine.Mathf.Clamp01(comercio.capacidadeAtracao / 100f) * 0.15f;

        // Penalidades diretas
        if (pais.emGuerra)    score -= 0.30f;
        if (pais.sancionado)  score -= 0.10f;
        if (pais.inflacao > 20f) score -= (pais.inflacao - 20f) * 0.005f;

        return UnityEngine.Mathf.Clamp01(score);
    }

    // --- FATOR DO AEROPORTO COMERCIAL ---
    private static float ObterFatorAeroporto(int teamId)
    {
        if (teamId > 0)
        {
#if UNITY_2023_1_OR_NEWER
            GerenciadorAeroportoComercial[] aeroportos = UnityEngine.Object.FindObjectsByType<GerenciadorAeroportoComercial>(FindObjectsSortMode.None);
#else
            GerenciadorAeroportoComercial[] aeroportos = UnityEngine.Object.FindObjectsOfType<GerenciadorAeroportoComercial>();
#endif
            float melhor = 0f;
            for (int i = 0; i < aeroportos.Length; i++)
            {
                GerenciadorAeroportoComercial aeroporto = aeroportos[i];
                if (aeroporto == null || !aeroporto.isActiveAndEnabled || ResolverTeamIdAeroporto(aeroporto.gameObject) != teamId) continue;
                melhor = Mathf.Max(melhor, CalcularFatorAeroporto(aeroporto));
            }
            if (melhor > 0f) return melhor;
        }

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

        return CalcularFatorAeroporto(_aeroportoCache);
    }

    private static float CalcularFatorAeroporto(GerenciadorAeroportoComercial aeroporto)
    {
        if (aeroporto == null) return 1f;
        int passagens = aeroporto.estatisticaPassagensVendidasDia;
        int contratos  = aeroporto.contratosAtivos != null ? aeroporto.contratosAtivos.Count : 0;

        if (passagens > 1000 || contratos >= 4) return 4f;
        if (passagens > 500  || contratos >= 3) return 3f;
        if (passagens > 200  || contratos >= 2) return 2f;
        if (passagens > 0    || contratos >= 1) return 1.5f;
        return 0.8f;
    }

    private static int ResolverTeamIdAeroporto(GameObject go)
    {
        if (go == null) return 1;
        IdentidadeUnidade identity = go.GetComponentInParent<IdentidadeUnidade>();
        if (identity != null && identity.teamID > 0) return identity.teamID;
        IdentidadeIA identityIA = go.GetComponentInParent<IdentidadeIA>();
        if (identityIA != null && identityIA.teamID > 0) return identityIA.teamID;
        return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1;
    }
}
