using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_NationalDecisionState
    {
        public string StrategicPlan = "Equilibrio";
        public RecursoMercado CriticalNeed = RecursoMercado.Nenhum;
        public RecursoMercado BestSurplus = RecursoMercado.Nenhum;
        public int PendingProposals;
        public int ActiveOffers;
        public int BlockedDecisions;
        public float QualityOfLife;
        public float PopulationPressure;
        public string MainEconomicDeficit = "Nenhum";
        public string MainProduction = "Nenhum";
        public float LastSyncTime;
        public float LastMarketTime;
        public float LastEconomyTime;
        public float LastDiplomacyTime;
    }

    public sealed class IA_GrandStrategy : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;

        public IA_GrandStrategy(IA_Context context, IA_NationalDecisionState state)
        {
            _context = context;
            _state = state;
        }

        public string Name { get { return "IA_GrandStrategy"; } }
        public float Interval { get { return 12f; } }
        public float BudgetMs { get { return 0.08f; } }

        public void Tick(float now, float deltaTime)
        {
            DadosPaisGoverno pais = ResolvePais();
            if (pais == null) return;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId) : null;

            if (economia != null && economia.deficitEnergia > 0.5f)
            {
                _state.StrategicPlan = "ConstruirEnergia";
            }
            else if (economia != null && economia.deficitComida > 0.5f)
            {
                _state.StrategicPlan = "ConstruirFarm";
            }
            else if (economia != null && economia.pressaoPopulacional > 0.88f)
            {
                _state.StrategicPlan = "ConstruirCasas";
            }
            else if (pais.modoInicialIA == ModoInicialPaisIA.GuerraTotal || pais.modoInicialIA == ModoInicialPaisIA.Mobilizacao)
            {
                _state.StrategicPlan = "Mobilizacao";
            }
            else if (pais.estabilidade < 35f || pais.saldo < 1200)
            {
                _state.StrategicPlan = "Crise";
            }
            else if (pais.perfilIA == PerfilPaisIA.Industrial || pais.perfilIA == PerfilPaisIA.ProdutorPetroleo)
            {
                _state.StrategicPlan = "Exportar";
            }
            else if (pais.perfilIA == PerfilPaisIA.Pequeno)
            {
                _state.StrategicPlan = "Sobrevivencia";
            }
            else
            {
                _state.StrategicPlan = "Equilibrio";
            }

            pais.planoEstrategico = _state.StrategicPlan;
        }

        private DadosPaisGoverno ResolvePais()
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            return gov != null && _context != null && _context.Brain != null ? gov.ObterPais(_context.Brain.TeamId) : null;
        }
    }

    public sealed class IA_EconomyDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;

        public IA_EconomyDirector(IA_Context context, IA_NationalDecisionState state)
        {
            _context = context;
            _state = state;
        }

        public string Name { get { return "IA_EconomyDirector"; } }
        public float Interval { get { return 5f; } }
        public float BudgetMs { get { return 0.10f; } }

        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = gov != null && _context.Brain != null ? gov.ObterPais(_context.Brain.TeamId) : null;
            if (pais == null) return;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId) : null;

            _state.CriticalNeed = ResolveCriticalNeed(pais);
            _state.BestSurplus = ResolveBestSurplus(pais);
            if (economia != null)
            {
                _state.QualityOfLife = economia.qualidadeVida;
                _state.PopulationPressure = economia.pressaoPopulacional;
                _state.MainEconomicDeficit = economia.DeficitPrincipal;
                _state.MainProduction = economia.ProducaoPrincipal;
            }
            _state.LastEconomyTime = now;

            if (pais.perfilIA == PerfilPaisIA.Industrial)
            {
                pais.producao = Mathf.Clamp(pais.producao + 0.08f, 0f, 100f);
                pais.gastosPorSegundo = Mathf.Max(pais.gastosPorSegundo, 5f);
            }
            else if (pais.perfilIA == PerfilPaisIA.Pequeno && _state.CriticalNeed != RecursoMercado.Nenhum)
            {
                pais.estabilidade = Mathf.Clamp(pais.estabilidade - 0.06f, 0f, 100f);
            }
        }

        public static RecursoMercado ResolveCriticalNeed(DadosPaisGoverno pais)
        {
            if (pais == null) return RecursoMercado.Nenhum;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId) : null;
            if (economia != null)
            {
                if (economia.deficitComida > 0.5f) return RecursoMercado.Comida;
                if (economia.deficitPetroleo > 0.5f) return RecursoMercado.Petroleo;
                if (economia.deficitEnergia > 1.5f && pais.aco < 260) return RecursoMercado.Aco;
            }

            int comidaMin = pais.perfilIA == PerfilPaisIA.Pequeno ? 420 : 260;
            int petroleoMin = pais.perfilIA == PerfilPaisIA.Industrial || pais.perfilIA == PerfilPaisIA.Militarista ? 520 : 260;
            int acoMin = pais.perfilIA == PerfilPaisIA.Industrial || pais.perfilIA == PerfilPaisIA.Militarista ? 360 : 160;
            int armasMin = pais.emGuerra || pais.perfilIA == PerfilPaisIA.Militarista ? 360 : 160;

            if (pais.comida < comidaMin) return RecursoMercado.Comida;
            if (pais.petroleo < petroleoMin) return RecursoMercado.Petroleo;
            if (pais.aco < acoMin) return RecursoMercado.Aco;
            if (pais.armamentos < armasMin) return RecursoMercado.Armamentos;
            return RecursoMercado.Nenhum;
        }

        public static RecursoMercado ResolveBestSurplus(DadosPaisGoverno pais)
        {
            if (pais == null) return RecursoMercado.Nenhum;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId) : null;
            if (economia != null)
            {
                if (economia.petroleoProduzido > economia.deficitPetroleo + 4f && pais.petroleo > 650) return RecursoMercado.Petroleo;
                if (economia.comidaProduzida > economia.deficitComida + 4f && pais.comida > 800) return RecursoMercado.Comida;
                if (economia.industriaProduzida > 7f && pais.aco > 650) return RecursoMercado.Aco;
            }

            if (pais.perfilIA == PerfilPaisIA.ProdutorPetroleo && pais.petroleo > 900) return RecursoMercado.Petroleo;
            if (pais.perfilIA == PerfilPaisIA.Industrial && pais.armamentos > 650) return RecursoMercado.Armamentos;
            if (pais.comida > 1100) return RecursoMercado.Comida;
            if (pais.petroleo > 1100) return RecursoMercado.Petroleo;
            if (pais.aco > 850) return RecursoMercado.Aco;
            if (pais.armamentos > 750) return RecursoMercado.Armamentos;
            return RecursoMercado.Nenhum;
        }
    }

    public sealed class IA_SyncNetwork : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;

        public IA_SyncNetwork(IA_Context context, IA_NationalDecisionState state)
        {
            _context = context;
            _state = state;
        }

        public string Name { get { return "IA_SyncNetwork"; } }
        public float Interval { get { return IsRuntimePressed() ? 22f : 14f; } }
        public float BudgetMs { get { return 0.14f; } }

        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (gov == null || mercado == null || _context.Brain == null) return;

            DadosPaisGoverno pais = gov.ObterPais(_context.Brain.TeamId);
            if (pais == null || pais.teamId == gov.teamJogador) return;

            _state.LastSyncTime = now;
            _state.PendingProposals = gov.Propostas.Count(p => p != null && p.EstaPendente && (p.origemTeamId == pais.teamId || p.alvoTeamId == pais.teamId));

            RecursoMercado surplus = IA_EconomyDirector.ResolveBestSurplus(pais);
            if (surplus == RecursoMercado.Nenhum) return;

            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(surplus));
            if (item == null) return;

            DadosPaisGoverno jogador = gov.ObterPais(gov.teamJogador);
            if (jogador == null) return;

            RelacaoPaisGoverno rel = gov.ObterRelacao(pais.teamId, jogador.teamId);
            int estoque = gov.ObterEstoque(pais.teamId, surplus);
            int quantidade = Mathf.Clamp(estoque / 6, item.CalcularQuantidadePadrao(), item.CalcularQuantidadePadrao() * 5);
            if (rel.valor >= 20 || surplus == IA_EconomyDirector.ResolveCriticalNeed(jogador))
            {
                float desconto = rel.pactoMilitar ? 0.86f : 0.96f;
                CriarProposta(gov, pais.teamId, jogador.teamId, TipoPropostaInternacional.Venda, surplus, quantidade, Mathf.RoundToInt(item.precoAtual * desconto), "oferta de " + item.nome + " no mercado aliado");
                _state.ActiveOffers++;
            }
        }

        private void CriarProposta(SistemaGovernoMundial gov, int origem, int alvo, TipoPropostaInternacional tipo, RecursoMercado recurso, int quantidade, int preco, string motivo)
        {
            gov.TentarCriarProposta(new PropostaInternacional
            {
                origemTeamId = origem,
                alvoTeamId = alvo,
                tipo = tipo,
                recurso = recurso,
                quantidade = quantidade,
                precoUnitario = Mathf.Max(1, preco),
                prioridade = 55,
                motivo = motivo,
                expiraEm = Time.unscaledTime + 80f,
                dedupKey = "sync:" + origem + ":" + alvo + ":" + tipo + ":" + recurso
            });
        }

        private static bool IsRuntimePressed()
        {
            return DiagnosticoDesempenhoJogo.RuntimeSobPressao() || DiagnosticoDesempenhoJogo.RuntimeSaturado();
        }
    }

    public sealed class IA_MarketDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;

        public IA_MarketDirector(IA_Context context, IA_NationalDecisionState state)
        {
            _context = context;
            _state = state;
        }

        public string Name { get { return "IA_MarketDirector"; } }
        public float Interval { get { return DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 11f : 7f; } }
        public float BudgetMs { get { return 0.18f; } }

        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (gov == null || mercado == null || _context.Brain == null) return;

            DadosPaisGoverno pais = gov.ObterPais(_context.Brain.TeamId);
            if (pais == null || pais.teamId == gov.teamJogador || pais.saldo < 300) return;

            _state.LastMarketTime = now;
            RecursoMercado need = IA_EconomyDirector.ResolveCriticalNeed(pais);
            if (need != RecursoMercado.Nenhum)
            {
                TryBuyNeed(gov, mercado, pais, need);
                return;
            }

            RecursoMercado surplus = IA_EconomyDirector.ResolveBestSurplus(pais);
            if (surplus != RecursoMercado.Nenhum)
            {
                TrySellSurplus(gov, mercado, pais, surplus);
            }
        }

        private void TryBuyNeed(SistemaGovernoMundial gov, SistemaMercadoGlobal mercado, DadosPaisGoverno comprador, RecursoMercado recurso)
        {
            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(recurso));
            if (item == null) return;

            DadosPaisGoverno vendedor = gov.Paises
                .Where(p => p != null && p.teamId != comprador.teamId && gov.ObterEstoque(p.teamId, recurso) > item.CalcularQuantidadePadrao())
                .OrderByDescending(p => ScoreVendedor(gov, comprador, p, recurso))
                .FirstOrDefault();

            if (vendedor == null)
            {
                _state.BlockedDecisions++;
                return;
            }

            int quantidade = Mathf.Min(item.CalcularQuantidadePadrao() * 2, Mathf.Max(10, comprador.saldo / Mathf.Max(1, item.precoAtual) / 2));
            RelacaoPaisGoverno rel = gov.ObterRelacao(comprador.teamId, vendedor.teamId);
            if (vendedor.teamId == gov.teamJogador)
            {
                CriarProposta(gov, comprador.teamId, vendedor.teamId, TipoPropostaInternacional.Compra, recurso, quantidade, AjustarPreco(item.precoAtual, rel, comprador), comprador.nomePais + " quer comprar " + item.nome);
                return;
            }

            if (rel.sancaoAtiva || rel.valor < -80)
            {
                _state.BlockedDecisions++;
                return;
            }

            mercado.Comprar(comprador.teamId, vendedor.teamId, item.id, quantidade, out _);
        }

        private void TrySellSurplus(SistemaGovernoMundial gov, SistemaMercadoGlobal mercado, DadosPaisGoverno vendedor, RecursoMercado recurso)
        {
            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(recurso));
            if (item == null) return;

            DadosPaisGoverno comprador = gov.Paises
                .Where(p => p != null && p.teamId != vendedor.teamId && IA_EconomyDirector.ResolveCriticalNeed(p) == recurso)
                .OrderByDescending(p => ScoreComprador(gov, vendedor, p))
                .FirstOrDefault();
            if (comprador == null) return;

            int quantidade = Mathf.Min(item.CalcularQuantidadePadrao() * 2, gov.ObterEstoque(vendedor.teamId, recurso) / 4);
            if (quantidade <= 0) return;

            RelacaoPaisGoverno rel = gov.ObterRelacao(vendedor.teamId, comprador.teamId);
            int preco = AjustarPreco(item.precoAtual, rel, vendedor);
            if (comprador.teamId == gov.teamJogador)
            {
                CriarProposta(gov, vendedor.teamId, comprador.teamId, TipoPropostaInternacional.Venda, recurso, quantidade, preco, vendedor.nomePais + " oferece " + item.nome);
                return;
            }

            if (!rel.sancaoAtiva && rel.valor > -75)
            {
                mercado.Vender(vendedor.teamId, comprador.teamId, item.id, quantidade, out _);
            }
        }

        private static float ScoreVendedor(SistemaGovernoMundial gov, DadosPaisGoverno comprador, DadosPaisGoverno vendedor, RecursoMercado recurso)
        {
            RelacaoPaisGoverno rel = gov.ObterRelacao(comprador.teamId, vendedor.teamId);
            return gov.ObterEstoque(vendedor.teamId, recurso) + rel.valor * 8f + (rel.pactoMilitar ? 500f : 0f) - (rel.sancaoAtiva ? 5000f : 0f);
        }

        private static float ScoreComprador(SistemaGovernoMundial gov, DadosPaisGoverno vendedor, DadosPaisGoverno comprador)
        {
            RelacaoPaisGoverno rel = gov.ObterRelacao(vendedor.teamId, comprador.teamId);
            return comprador.saldo + rel.valor * 20f + (rel.pactoMilitar ? 500f : 0f);
        }

        private static int AjustarPreco(int precoBase, RelacaoPaisGoverno rel, DadosPaisGoverno origem)
        {
            float mult = 1f;
            if (rel != null && rel.pactoMilitar) mult -= 0.12f;
            if (rel != null && rel.valor < -40) mult += 0.25f;
            if (origem != null && origem.perfilIA == PerfilPaisIA.Rival) mult += 0.18f;
            if (origem != null && origem.perfilIA == PerfilPaisIA.Aliado) mult -= 0.06f;
            return Mathf.Max(1, Mathf.RoundToInt(precoBase * mult));
        }

        private static void CriarProposta(SistemaGovernoMundial gov, int origem, int alvo, TipoPropostaInternacional tipo, RecursoMercado recurso, int quantidade, int preco, string motivo)
        {
            gov.TentarCriarProposta(new PropostaInternacional
            {
                origemTeamId = origem,
                alvoTeamId = alvo,
                tipo = tipo,
                recurso = recurso,
                quantidade = quantidade,
                precoUnitario = Mathf.Max(1, preco),
                prioridade = tipo == TipoPropostaInternacional.Compra ? 70 : 55,
                motivo = motivo,
                expiraEm = Time.unscaledTime + 75f,
                dedupKey = "market:" + origem + ":" + alvo + ":" + tipo + ":" + recurso
            });
        }
    }

    public sealed class IA_DiplomacyDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;

        public IA_DiplomacyDirector(IA_Context context, IA_NationalDecisionState state)
        {
            _context = context;
            _state = state;
        }

        public string Name { get { return "IA_DiplomacyDirector"; } }
        public float Interval { get { return DiagnosticoDesempenhoJogo.RuntimeSobPressao() ? 30f : 20f; } }
        public float BudgetMs { get { return 0.12f; } }

        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null || _context.Brain == null) return;
            DadosPaisGoverno pais = gov.ObterPais(_context.Brain.TeamId);
            if (pais == null || pais.teamId == gov.teamJogador) return;
            _state.LastDiplomacyTime = now;

            // --- RELAÇÃO COM O JOGADOR ---
            RelacaoPaisGoverno relJogador = gov.ObterRelacao(pais.teamId, gov.teamJogador);

            if (pais.perfilIA == PerfilPaisIA.Aliado && pais.aliadoPrioritarioTeamId == gov.teamJogador)
            {
                if (relJogador != null && !relJogador.pactoMilitar && relJogador.valor >= 55)
                {
                    gov.TentarCriarProposta(new PropostaInternacional
                    {
                        origemTeamId = pais.teamId,
                        alvoTeamId = gov.teamJogador,
                        tipo = TipoPropostaInternacional.PactoDefensivo,
                        recurso = RecursoMercado.Nenhum,
                        quantidade = 1,
                        precoUnitario = 0,
                        prioridade = 60,
                        motivo = pais.nomePais + " propõe pacto defensivo",
                        expiraEm = Time.unscaledTime + 120f,
                        dedupKey = "diplo:pacto:" + pais.teamId + ":" + gov.teamJogador
                    });
                }
            }

            // --- OPÇÃO A: DIPLOMACIA E ASFIXIA GEOPOLÍTICA ---
            bool isRivalOrAggressive = pais.perfilIA == PerfilPaisIA.Rival 
                                       || pais.perfilIA == PerfilPaisIA.Militarista
                                       || pais.modoInicialIA == ModoInicialPaisIA.AgressivoContraJogador 
                                       || pais.modoInicialIA == ModoInicialPaisIA.GuerraTotal
                                       || pais.rivalTeamId == gov.teamJogador;

            if (isRivalOrAggressive && relJogador != null)
            {
                // 1. Asfixia por Aço: Tenta esvaziar o estoque de aço do jogador oferecendo preços muito altos (compra do jogador)
                int acoJogador = gov.ObterEstoque(gov.teamJogador, RecursoMercado.Aco);
                if (acoJogador > 60)
                {
                    SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
                    DadosItemMercado itemAco = mercado != null ? mercado.ObterItem("aco") : null;
                    int precoAco = itemAco != null ? itemAco.precoAtual : 40;
                    int precoTentador = Mathf.RoundToInt(precoAco * 1.45f);

                    gov.TentarCriarProposta(new PropostaInternacional
                    {
                        origemTeamId = pais.teamId,
                        alvoTeamId = gov.teamJogador,
                        tipo = TipoPropostaInternacional.Compra,
                        recurso = RecursoMercado.Aco,
                        quantidade = 50,
                        precoUnitario = precoTentador,
                        prioridade = 75,
                        motivo = $"{pais.nomePais} oferece proposta comercial vantajosa pelo seu Aço (Asfixia Econômica)",
                        expiraEm = Time.unscaledTime + 90f,
                        dedupKey = "diplo:asfixia_aco:" + pais.teamId + ":" + gov.teamJogador
                    });
                }

                // 2. Sanções Econômicas: Se a relação for ruim (< -30) e não houver sanção ativa, aplica sanções comerciais unilaterais
                if (relJogador.valor < -30 && !relJogador.sancaoAtiva)
                {
                    relJogador.sancaoAtiva = true;
                    relJogador.tratadoComercial = false;
                    relJogador.valor = Mathf.Clamp(relJogador.valor - 15, -100, 100);

                    DadosPaisGoverno jogador = gov.ObterPais(gov.teamJogador);
                    if (jogador != null)
                    {
                        jogador.sancionado = true;
                        jogador.estabilidade = Mathf.Clamp(jogador.estabilidade - 12f, 5f, 100f);
                        jogador.inflacao = Mathf.Clamp(jogador.inflacao + 4f, 0.5f, 40f);
                    }

                    gov.RegistrarNoticia($"{pais.nomePais} impôs sanções comerciais unilaterais severas contra o jogador para minar sua economia!");
                    SistemaMercadoGlobal.Instancia?.SimularMercado();
                    gov.ProcessarEconomia();
                }
            }
        }
    }

    public sealed class IA_LawDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        public IA_LawDirector(IA_Context context) { _context = context; }
        public string Name { get { return "IA_LawDirector"; } }
        public float Interval { get { return 28f; } }
        public float BudgetMs { get { return 0.06f; } }
        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = gov != null && _context.Brain != null ? gov.ObterPais(_context.Brain.TeamId) : null;
            if (pais == null || pais.teamId == gov.teamJogador) return;
            if (pais.estabilidade < 42f) pais.gastosPorSegundo = Mathf.Max(1f, pais.gastosPorSegundo - 0.05f);
        }
    }

    public sealed class IA_LogisticsDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        public IA_LogisticsDirector(IA_Context context) { _context = context; }
        public string Name { get { return "IA_LogisticsDirector"; } }
        public float Interval { get { return 10f; } }
        public float BudgetMs { get { return 0.06f; } }
        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = gov != null && _context.Brain != null ? gov.ObterPais(_context.Brain.TeamId) : null;
            if (pais == null) return;
            if (pais.petroleo < 80 && pais.producao > 25f) pais.producao = Mathf.Clamp(pais.producao - 0.12f, 0f, 100f);
        }
    }

    public sealed class IA_WarDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly IA_NationalDecisionState _state;
        public IA_WarDirector(IA_Context context, IA_NationalDecisionState state) { _context = context; _state = state; }
        public string Name { get { return "IA_WarDirector"; } }
        public float Interval { get { return 16f; } }
        public float BudgetMs { get { return 0.08f; } }
        public void Tick(float now, float deltaTime)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = gov != null && _context.Brain != null ? gov.ObterPais(_context.Brain.TeamId) : null;
            if (pais == null || pais.teamId == gov.teamJogador) return;
            if (pais.modoInicialIA != ModoInicialPaisIA.GuerraTotal && pais.modoInicialIA != ModoInicialPaisIA.AgressivoContraJogador) return;
            if (pais.rivalTeamId != gov.teamJogador && pais.pesoAgressividade < 0.80f) return;
            if (_state.CriticalNeed != RecursoMercado.Nenhum && pais.armamentos > 500)
            {
                RelacaoPaisGoverno rel = gov.ObterRelacao(pais.teamId, gov.teamJogador);
                rel.valor = Mathf.Clamp(rel.valor - 3, -100, 100);
            }
        }
    }
}
