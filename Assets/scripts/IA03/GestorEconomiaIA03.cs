using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Lê o retrato econômico central e ajusta preferências dos diretores existentes.
    /// Não altera saldos, estoques nem executa compras por conta própria.
    /// </summary>
    public sealed class GestorEconomiaIA03
    {
        private const float IntervaloTentativaFinanciamentoSegundos = 180f;
        private const int MaxTentativasReducaoEmprestimo = 8;

        private float proximaTentativaFinanciamentoEm;

        public struct Resumo
        {
            public long Saldo;
            public long SaldoDisponivel;
            public long ReservaFinanceiraEstrategica;
            public long ManpowerDisponivel;
            public long ReservaManpowerEstrategica;
            public long ManpowerDisponivelAlemDaReserva;
            public int Comida;
            public int Petroleo;
            public int Energia;
            public float DeficitComida;
            public float DeficitPetroleo;
            public float DeficitEnergia;
            public int CombustivelTerrestre;
            public int CombustivelAereo;
            public int CombustivelNaval;
            public int UnidadesTerrestresOperacionais;
            public int UnidadesAereasOperacionais;
            public int UnidadesNavaisOperacionais;
            public int FabricasOperacionais;
            public int QuarteisOperacionais;
            public int AeroportosMilitaresOperacionais;
            public int HeliportosOperacionais;
            public int EstaleirosOperacionais;
            public float AutonomiaOperacionalEstimadaHoras;
            public float PressaoEconomica;
            public long CustoReposicaoDoGrupo;
            public string ItemMaisCaroDoGrupo;
            public long ManpowerReposicaoDoGrupo;
            public bool DadosDisponiveis;
            public bool ReservaFinanceiraBaixa;
            public bool CombustivelOperacionalBaixo;
            public bool EstoqueEssencialBaixo;
            public bool PodeEscalarParaN2;
            public bool ReposicaoDoGrupoConhecida;
            public string ClassificacaoProntidao;
            public string MotivoProntidao;
        }

        /// <summary>
        /// Verifica se uma perda do grupo pode ser reposta pela rota militar existente:
        /// item exato do catálogo, instalação adequada, manpower acima da reserva e tesouro.
        /// Não reserva recursos nem cria uma ordem de produção.
        /// </summary>
        public bool TentarAvaliarReposicaoDoGrupo(
            IA_BackendBridge backend,
            SistemaGovernoMundial governo,
            IList<GameObject> unidades,
            ref Resumo resumo,
            out string motivo)
        {
            motivo = string.Empty;
            resumo.CustoReposicaoDoGrupo = 0L;
            resumo.ItemMaisCaroDoGrupo = string.Empty;
            resumo.ManpowerReposicaoDoGrupo = 0L;
            resumo.ReposicaoDoGrupoConhecida = false;

            if (backend == null || governo == null || unidades == null || unidades.Count == 0)
            {
                motivo = "grupo, tesouro nacional ou catálogo de produção indisponível";
                return false;
            }

            int unidadesValidas = 0;
            bool faltaFabrica = false;
            bool faltaQuartel = false;
            bool faltaAeroporto = false;
            bool faltaHeliporto = false;
            bool faltaEstaleiro = false;
            for (int i = 0; i < unidades.Count; i++)
            {
                GameObject unidade = unidades[i];
                if (unidade == null)
                {
                    continue;
                }

                unidadesValidas++;
                IA_ConstructionMetadata metadata = unidade.GetComponent<IA_ConstructionMetadata>()
                    ?? unidade.GetComponentInChildren<IA_ConstructionMetadata>(true);
                DadosConstrucao ficha;
                if (!TryResolveFichaDeReposicao(backend, unidade, metadata, out ficha) || ficha == null)
                {
                    resumo.CustoReposicaoDoGrupo = 0L;
                    resumo.ItemMaisCaroDoGrupo = string.Empty;
                    motivo = "ficha de reposição não encontrada no catálogo para " + unidade.name;
                    return false;
                }

                long precoEfetivo = System.Math.Max(0L, ficha.ObterPrecoEfetivo());
                if (precoEfetivo <= 0L)
                {
                    resumo.CustoReposicaoDoGrupo = 0L;
                    resumo.ItemMaisCaroDoGrupo = string.Empty;
                    motivo = "preço efetivo de reposição não definido para " + ficha.GetDisplayName();
                    return false;
                }

                GameObject fichaPrefab;
                if (!ficha.TryGetPrefabBasico(out fichaPrefab) || fichaPrefab == null)
                {
                    resumo.CustoReposicaoDoGrupo = 0L;
                    resumo.ItemMaisCaroDoGrupo = string.Empty;
                    motivo = "prefab de reposição não está disponível para " + ficha.GetDisplayName();
                    return false;
                }

                if (precoEfetivo > resumo.CustoReposicaoDoGrupo)
                {
                    resumo.CustoReposicaoDoGrupo = precoEfetivo;
                    resumo.ItemMaisCaroDoGrupo = ficha.GetDisplayName();
                }

                IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
                    ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
                if (identidade == null)
                {
                    motivo = "tipo de unidade indisponível para avaliar a reposição de " + ficha.GetDisplayName();
                    return false;
                }

                int manpowerNecessario = Mathf.Max(0, SistemaGovernoMundial.ObterEfetivoNecessario(
                    fichaPrefab,
                    identidade.tipoUnidade));
                resumo.ManpowerReposicaoDoGrupo = System.Math.Max(resumo.ManpowerReposicaoDoGrupo, manpowerNecessario);

                switch (identidade.tipoUnidade)
                {
                    case TipoUnidade.Infantaria:
                        faltaQuartel |= resumo.QuarteisOperacionais <= 0;
                        break;
                    case TipoUnidade.Veiculo:
                        faltaFabrica |= resumo.FabricasOperacionais <= 0;
                        break;
                    case TipoUnidade.Aereo:
                    {
                        string nomeFicha = IA_Text.Normalize(ficha.NomeItem + " " + fichaPrefab.name);
                        bool helicoptero = fichaPrefab.GetComponent<Helicoptero>() != null
                            || nomeFicha.Contains("heli") || nomeFicha.Contains("ray") || nomeFicha.Contains("vans");
                        faltaHeliporto |= helicoptero && resumo.HeliportosOperacionais <= 0;
                        faltaAeroporto |= !helicoptero && resumo.AeroportosMilitaresOperacionais <= 0;
                        break;
                    }
                    case TipoUnidade.Naval:
                        faltaEstaleiro |= resumo.EstaleirosOperacionais <= 0;
                        break;
                    default:
                        motivo = "tipo de unidade sem rota militar de reposição conhecida para " + ficha.GetDisplayName();
                        return false;
                }
            }

            if (unidadesValidas == 0 || resumo.CustoReposicaoDoGrupo <= 0L)
            {
                motivo = "grupo da missão sem unidades válidas para avaliar reposição";
                return false;
            }

            resumo.ReposicaoDoGrupoConhecida = true;

            if (faltaQuartel || faltaFabrica || faltaAeroporto || faltaHeliporto || faltaEstaleiro)
            {
                motivo = "infraestrutura operacional insuficiente para repor o grupo: "
                    + (faltaQuartel ? "quartel " : string.Empty)
                    + (faltaFabrica ? "fábrica " : string.Empty)
                    + (faltaAeroporto ? "aeroporto militar " : string.Empty)
                    + (faltaHeliporto ? "heliporto " : string.Empty)
                    + (faltaEstaleiro ? "estaleiro " : string.Empty);
                return false;
            }

            long manpowerDisponivel = governo.ObterManpowerDisponivelAlemDaReserva(backend.TeamId);
            if (manpowerDisponivel < resumo.ManpowerReposicaoDoGrupo)
            {
                motivo = "manpower acima da reserva insuficiente para repor uma baixa do grupo: necessário "
                    + resumo.ManpowerReposicaoDoGrupo + "; disponível " + manpowerDisponivel;
                return false;
            }

            long saldoDisponivel = governo.ObterSaldoDisponivelParaOperacao(backend.TeamId);
            resumo.SaldoDisponivel = saldoDisponivel;
            if (saldoDisponivel < resumo.CustoReposicaoDoGrupo)
            {
                motivo = "saldo operacional insuficiente para repor uma baixa do grupo: "
                    + resumo.ItemMaisCaroDoGrupo + " custa " + resumo.CustoReposicaoDoGrupo
                    + "; disponível " + saldoDisponivel;
                return false;
            }

            return true;
        }

        private static bool TryResolveFichaDeReposicao(
            IA_BackendBridge backend,
            GameObject unidade,
            IA_ConstructionMetadata metadata,
            out DadosConstrucao ficha)
        {
            ficha = null;
            if (backend == null || unidade == null)
            {
                return false;
            }

            if (metadata != null
                && !string.IsNullOrWhiteSpace(metadata.ItemId)
                && backend.TryResolveItemExact(metadata.ItemId, out ficha))
            {
                return true;
            }

            if (metadata != null
                && !string.IsNullOrWhiteSpace(metadata.SourcePrefabName)
                && backend.TryResolveItemExact(metadata.SourcePrefabName, out ficha))
            {
                return true;
            }

            if (metadata != null
                && !string.IsNullOrWhiteSpace(metadata.DisplayName)
                && backend.TryResolveItemExact(metadata.DisplayName, out ficha))
            {
                return true;
            }

            const string cloneSuffix = "(Clone)";
            string prefabName = unidade.name;
            if (prefabName.EndsWith(cloneSuffix, System.StringComparison.Ordinal))
            {
                prefabName = prefabName.Substring(0, prefabName.Length - cloneSuffix.Length).TrimEnd();
            }

            return backend.TryResolveItemExact(prefabName, out ficha);
        }

        public bool TentarAplicarPesos(
            SistemaGovernoMundial governo,
            IA_BrainMaster brain,
            PerfilPaisSO perfil,
            int teamId,
            bool emGuerra,
            out Resumo resumo)
        {
            resumo = default;
            if (governo == null || brain == null || perfil == null)
            {
                return false;
            }

            resumo.AutonomiaOperacionalEstimadaHoras = -1f;

            DadosPaisGoverno pais = governo.ObterPais(teamId);
            if (pais == null)
            {
                return false;
            }

            resumo.Saldo = pais.saldo;
            resumo.SaldoDisponivel = governo.ObterSaldoDisponivelParaOperacao(teamId);
            resumo.ReservaFinanceiraEstrategica = Mathf.CeilToInt(Mathf.Max(0f, perfil.ReservaFinanceiraDesejada));
            resumo.ManpowerDisponivel = governo.ObterManpowerDisponivelParaMobilizacao(teamId);
            resumo.ReservaManpowerEstrategica = governo.ObterReservaManpowerEstrategica(teamId);
            resumo.ManpowerDisponivelAlemDaReserva = governo.ObterManpowerDisponivelAlemDaReserva(teamId);
            resumo.Comida = pais.comida;
            resumo.Petroleo = pais.petroleo;
            resumo.Energia = pais.energia;
            resumo.DeficitComida = pais.deficitComida;
            resumo.DeficitPetroleo = pais.deficitPetroleo;
            resumo.DeficitEnergia = pais.deficitEnergia;
            resumo.DadosDisponiveis = true;
            resumo.ReservaFinanceiraBaixa = pais.saldo < perfil.ReservaFinanceiraDesejada
                || resumo.SaldoDisponivel <= 0;

            SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
            if (industrial != null)
            {
                resumo.CombustivelTerrestre = Mathf.Max(0, Mathf.RoundToInt((float)industrial.ObterQuantidadePais(teamId, IndustriaIds.Diesel)));
                resumo.CombustivelAereo = Mathf.Max(0, Mathf.RoundToInt((float)industrial.ObterQuantidadePais(teamId, IndustriaIds.CombustivelAviacao)));
                resumo.CombustivelNaval = Mathf.Max(0, Mathf.RoundToInt((float)industrial.ObterQuantidadePais(teamId, IndustriaIds.CombustivelNaval)));
            }

            Hegemonia.AI.BrainMaster.IA_ForceSnapshot forca = brain.Context != null
                ? brain.Context.ForceSnapshot
                : null;
            if (forca == null && brain.Context != null && brain.Context.WorldState != null)
            {
                forca = brain.Context.WorldState.ForceSnapshot;
            }
            IA_WorldState mundo = brain.Context != null ? brain.Context.WorldState : null;
            if (forca != null)
            {
                bool operaTerrestre = forca.TankUnits > 0 || forca.ArtilleryUnits > 0 || forca.GroundTransports > 0;
                bool operaAereo = forca.FixedWingAircraft > 0 || forca.Helicopters > 0;
                bool operaNaval = forca.NavalUnits > 0 || forca.Submarines > 0 || forca.AircraftCarriers > 0;
                int reservaDiesel = Mathf.CeilToInt((float)IntegracaoMercadoIndustrial.ReservaMinimaPadrao(IndustriaIds.Diesel));
                int reservaAerea = Mathf.CeilToInt((float)IntegracaoMercadoIndustrial.ReservaMinimaPadrao(IndustriaIds.CombustivelAviacao));
                int reservaNaval = Mathf.CeilToInt((float)IntegracaoMercadoIndustrial.ReservaMinimaPadrao(IndustriaIds.CombustivelNaval));
                resumo.CombustivelOperacionalBaixo = (operaTerrestre && resumo.CombustivelTerrestre < reservaDiesel)
                    || (operaAereo && resumo.CombustivelAereo < reservaAerea)
                    || (operaNaval && resumo.CombustivelNaval < reservaNaval);
            }

            if (mundo != null)
            {
                CapturarUnidadesOperacionais(mundo, ref resumo);
                CapturarInfraestruturaOperacional(mundo, ref resumo);
            }

            resumo.EstoqueEssencialBaixo = pais.comida < 150
                                           || pais.petroleo < 150
                                           || pais.energia < 100
                                           || pais.deficitComida > 0f
                                           || pais.deficitPetroleo > 0f
                                           || pais.deficitEnergia > 0f
                                           || resumo.CombustivelOperacionalBaixo;

            AvaliarProntidao(forca, ref resumo);

            float pressao = Mathf.Clamp01((resumo.ReservaFinanceiraBaixa ? 0.55f : 0f)
                                          + (resumo.EstoqueEssencialBaixo ? 0.45f : 0f));
            resumo.PressaoEconomica = pressao;
            brain.TradeWeight = Mathf.Clamp01(
                (perfil.InteresseEmPetroleo + perfil.InteresseEmAlimentos + perfil.TendenciaDeExportacao) / 3f
                + (resumo.EstoqueEssencialBaixo ? 0.12f : 0f)
                - (emGuerra ? 0.10f : 0f));
            brain.StockControlWeight = Mathf.Clamp01(Mathf.Lerp(0.48f, 0.95f, Mathf.Max(pressao, emGuerra ? 0.75f : 0f)));
            brain.SelfSufficiencyWeight = Mathf.Clamp01(Mathf.Lerp(0.40f, 0.90f, pressao));
            brain.ExternalDependencyWeight = Mathf.Clamp01(Mathf.Lerp(0.55f, 0.85f, resumo.EstoqueEssencialBaixo ? 1f : 0f));
            brain.EconomicRiskWeight = Mathf.Clamp01(Mathf.Lerp(0.35f, 0.90f, pressao));
            return true;
        }

        public bool TentarSolicitarFinanciamento(
            SistemaGovernoMundial governo,
            int teamId,
            float now,
            ref Resumo resumo,
            out string motivo)
        {
            motivo = string.Empty;
            if (governo == null || teamId <= 0 || teamId == governo.teamJogador
                || !resumo.DadosDisponiveis || !resumo.ReservaFinanceiraBaixa)
            {
                return false;
            }

            if (now < proximaTentativaFinanciamentoEm)
            {
                return false;
            }

            proximaTentativaFinanciamentoEm = now + IntervaloTentativaFinanciamentoSegundos;
            DadosPaisGoverno devedor = governo.ObterPais(teamId);
            SistemaFederacoesGlobais federacoes = SistemaFederacoesGlobais.Instancia;
            if (devedor == null || federacoes == null
                || !SistemaFederacoesGlobais.EhFederacaoValida(devedor.federacaoGlobal))
            {
                return false;
            }

            if (devedor.emprestimos != null)
            {
                for (int i = 0; i < devedor.emprestimos.Count; i++)
                {
                    if (devedor.emprestimos[i] != null)
                    {
                        motivo = "a IA03 aguarda a quitação do empréstimo federativo atual";
                        return false;
                    }
                }
            }

            double reservaEstrategica = System.Math.Max(0d, resumo.ReservaFinanceiraEstrategica);
            double reservaOperacional = System.Math.Max(
                500d,
                System.Math.Max(0d, devedor.gastosPorSegundo)
                * Mathf.Max(1f, federacoes.intervaloTick)
                * 5d);
            double saldoAlvo = reservaEstrategica + reservaOperacional;
            double necessidade = System.Math.Max(0d, saldoAlvo - devedor.saldo);
            if (necessidade <= 0d)
            {
                necessidade = System.Math.Max(
                    0d,
                    reservaOperacional - System.Math.Max(0L, resumo.SaldoDisponivel));
            }

            long valorSolicitado = (long)System.Math.Ceiling(
                System.Math.Min(int.MaxValue, necessidade));
            if (valorSolicitado <= 0L)
            {
                return false;
            }

            string ultimaRecusa = string.Empty;
            IReadOnlyList<DadosPaisGoverno> paises = governo.Paises;
            for (int tentativa = 0;
                tentativa < MaxTentativasReducaoEmprestimo && valorSolicitado > 0L;
                tentativa++, valorSolicitado /= 2L)
            {
                for (int i = 0; i < paises.Count; i++)
                {
                    DadosPaisGoverno credor = paises[i];
                    if (credor == null || credor.teamId == teamId
                        || !string.Equals(credor.federacaoGlobal, devedor.federacaoGlobal, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string resposta;
                    if (!federacoes.SolicitarEmprestimo(
                            credor.teamId,
                            teamId,
                            valorSolicitado,
                            false,
                            out resposta))
                    {
                        ultimaRecusa = resposta;
                        continue;
                    }

                    resumo.Saldo = devedor.saldo;
                    resumo.SaldoDisponivel = governo.ObterSaldoDisponivelParaOperacao(teamId);
                    resumo.ReservaFinanceiraBaixa = devedor.saldo < resumo.ReservaFinanceiraEstrategica
                        || resumo.SaldoDisponivel <= 0L;
                    resumo.PressaoEconomica = Mathf.Clamp01(
                        (resumo.ReservaFinanceiraBaixa ? 0.55f : 0f)
                        + (resumo.EstoqueEssencialBaixo ? 0.45f : 0f));

                    motivo = "financiamento federativo aprovado por " + credor.nomePais;
                    governo.RegistrarNoticia(
                        devedor.nomePais + " recebeu financiamento federativo de " + credor.nomePais + ".");
                    return true;
                }
            }

            motivo = string.IsNullOrWhiteSpace(ultimaRecusa)
                ? "nenhum integrante do bloco possui crédito compatível"
                : ultimaRecusa;
            return false;
        }

        private static void CapturarUnidadesOperacionais(IA_WorldState mundo, ref Resumo resumo)
        {
            float combustivelEmTanques = 0f;
            float consumoContinuoPorSegundo = 0f;
            List<GameObject> unidades = mundo.OwnUnits;
            if (unidades == null)
            {
                return;
            }

            for (int i = 0; i < unidades.Count; i++)
            {
                GameObject unidade = unidades[i];
                if (unidade == null)
                {
                    continue;
                }

                IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
                    ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
                if (identidade == null || identidade.tipoUnidade == TipoUnidade.Estrutura)
                {
                    continue;
                }

                if (CombustivelUnidade.PodeOperarObjeto(unidade))
                {
                    switch (identidade.tipoUnidade)
                    {
                        case TipoUnidade.Infantaria:
                        case TipoUnidade.Veiculo:
                            resumo.UnidadesTerrestresOperacionais++;
                            break;
                        case TipoUnidade.Aereo:
                            resumo.UnidadesAereasOperacionais++;
                            break;
                        case TipoUnidade.Naval:
                            resumo.UnidadesNavaisOperacionais++;
                            break;
                    }
                }

                CombustivelUnidade tanque = unidade.GetComponent<CombustivelUnidade>();
                if (tanque == null || !tanque.usaCombustivel || tanque.combustivelInfinito)
                {
                    continue;
                }

                combustivelEmTanques += tanque.CombustivelAtual;
                if (tanque.consumoPorSegundoMovendo > 0f)
                {
                    consumoContinuoPorSegundo += tanque.consumoPorSegundoMovendo;
                }
            }

            resumo.AutonomiaOperacionalEstimadaHoras = combustivelEmTanques > 0f && consumoContinuoPorSegundo > 0f
                ? combustivelEmTanques / consumoContinuoPorSegundo / 3600f
                : -1f;
        }

        private static void CapturarInfraestruturaOperacional(IA_WorldState mundo, ref Resumo resumo)
        {
            List<GameObject> estruturas = mundo.OwnStructures;
            if (estruturas == null)
            {
                return;
            }

            for (int i = 0; i < estruturas.Count; i++)
            {
                GameObject estrutura = estruturas[i];
                if (estrutura == null)
                {
                    continue;
                }

                Fabrica fabrica = estrutura.GetComponent<Fabrica>() ?? estrutura.GetComponentInChildren<Fabrica>(true);
                if (fabrica != null && fabrica.PossuiEnergiaParaProduzir())
                {
                    if (fabrica.ehQuartel)
                    {
                        resumo.QuarteisOperacionais++;
                    }
                    else
                    {
                        resumo.FabricasOperacionais++;
                    }
                }

                GerenciadorAeroporto aeroporto = estrutura.GetComponent<GerenciadorAeroporto>()
                    ?? estrutura.GetComponentInChildren<GerenciadorAeroporto>(true);
                if (aeroporto != null && !(aeroporto is GerenciadorAeroportoComercial) && !aeroporto.semEnergia)
                {
                    resumo.AeroportosMilitaresOperacionais++;
                }

                Heliporto heliporto = estrutura.GetComponent<Heliporto>() ?? estrutura.GetComponentInChildren<Heliporto>(true);
                if (heliporto != null)
                {
                    Imovel imovel = heliporto.GetComponent<Imovel>() ?? heliporto.GetComponentInParent<Imovel>();
                    if (imovel == null || !imovel.semEnergia)
                    {
                        resumo.HeliportosOperacionais++;
                    }
                }

                Estaleiro estaleiro = estrutura.GetComponent<Estaleiro>() ?? estrutura.GetComponentInChildren<Estaleiro>(true);
                if (estaleiro != null && estaleiro.PossuiEnergiaOperacional)
                {
                    resumo.EstaleirosOperacionais++;
                }
            }
        }

        private static void AvaliarProntidao(IA_ForceSnapshot forca, ref Resumo resumo)
        {
            int unidadesOperacionais = resumo.UnidadesTerrestresOperacionais
                + resumo.UnidadesAereasOperacionais
                + resumo.UnidadesNavaisOperacionais;
            int forcaTotal = forca != null ? Mathf.Max(0, forca.TotalCombatUnits) : 0;
            bool semCapacidadeDeReposicao = resumo.FabricasOperacionais <= 0
                && resumo.QuarteisOperacionais <= 0
                && resumo.AeroportosMilitaresOperacionais <= 0
                && resumo.HeliportosOperacionais <= 0
                && resumo.EstaleirosOperacionais <= 0;

            resumo.PodeEscalarParaN2 = resumo.DadosDisponiveis
                && !resumo.ReservaFinanceiraBaixa
                && !resumo.EstoqueEssencialBaixo
                && resumo.ManpowerDisponivelAlemDaReserva > 0
                && forcaTotal > 0
                && unidadesOperacionais > 0
                && !semCapacidadeDeReposicao;

            if (!resumo.DadosDisponiveis || resumo.Saldo <= 0 || resumo.ManpowerDisponivel <= 0
                || resumo.ManpowerDisponivelAlemDaReserva <= 0
                || (forcaTotal > 0 && unidadesOperacionais <= 0))
            {
                resumo.ClassificacaoProntidao = "CRÍTICA";
                resumo.MotivoProntidao = !resumo.DadosDisponiveis ? "dados nacionais indisponíveis"
                    : resumo.Saldo <= 0 ? "tesouro nacional sem saldo"
                    : resumo.ManpowerDisponivel <= 0 ? "sem efetivo civil ou alistável"
                    : resumo.ManpowerDisponivelAlemDaReserva <= 0 ? "manpower mobilizável abaixo da reserva estratégica do perfil"
                    : "nenhuma unidade de combate pode operar com o combustível a bordo";
                return;
            }

            if (!resumo.PodeEscalarParaN2)
            {
                resumo.ClassificacaoProntidao = "LIMITADA";
                resumo.MotivoProntidao = resumo.ReservaFinanceiraBaixa ? "saldo operacional indisponível ou abaixo da reserva financeira do perfil"
                    : resumo.CombustivelOperacionalBaixo ? "combustível nacional abaixo da reserva operacional existente"
                    : resumo.EstoqueEssencialBaixo ? "estoque essencial em nível baixo ou déficit"
                    : semCapacidadeDeReposicao ? "sem fábrica, aeroporto ou estaleiro operacional"
                    : "prontidão nacional incompleta";
                return;
            }

            resumo.ClassificacaoProntidao = "ADEQUADA";
            resumo.MotivoProntidao = string.Empty;
        }
    }
}
