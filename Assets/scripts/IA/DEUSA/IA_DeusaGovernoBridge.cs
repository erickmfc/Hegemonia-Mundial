using System.Linq;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaGovernoBridge
    {
        public DadosPaisGoverno SincronizarNacao(IA_DeusaIdentidadeNacional identidade, IA_DeusaConfig config)
        {
            if (identidade == null)
            {
                return null;
            }

            SistemaGovernoMundial.GarantirInstancia();
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null)
            {
                return null;
            }

            governo.GarantirPaisIA(
                identidade.teamID,
                identidade.nomePais,
                identidade.nomeMoeda,
                GerarSimboloMoeda(identidade.nomeMoeda),
                MapearPerfil(identidade.personalidade),
                MapearModoInicial(config != null ? config.modoInicial : identidade.modoInicial));

            governo.AtualizarIdentidadeNacional(
                identidade.teamID,
                identidade.nomePais,
                identidade.nomePresidente,
                identidade.nomeMoeda);

            return governo.ObterPais(identidade.teamID);
        }

        public DadosEconomiaPais ObterEconomia(int teamId)
        {
            return SistemaEconomiaImoveis.Instancia != null
                ? SistemaEconomiaImoveis.Instancia.ObterEconomia(teamId)
                : null;
        }

        public bool TentarComprarRecurso(int compradorTeamId, RecursoMercado recurso, int quantidade, bool permitirJogador, bool permitirOutrasIAs, out string mensagem)
        {
            mensagem = string.Empty;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (governo == null || mercado == null || recurso == RecursoMercado.Nenhum || quantidade <= 0)
            {
                mensagem = "bridge sem dependencias para compra";
                return false;
            }

            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(recurso));
            if (item == null)
            {
                mensagem = "item de mercado nao encontrado";
                return false;
            }

            DadosPaisGoverno comprador = governo.ObterPais(compradorTeamId);
            if (comprador == null)
            {
                mensagem = "comprador inexistente";
                return false;
            }

            DadosPaisGoverno vendedor = governo.Paises
                .Where(p => p != null && p.teamId != compradorTeamId)
                .Where(p => (permitirJogador || p.teamId != governo.teamJogador) && (permitirOutrasIAs || p.teamId == governo.teamJogador))
                .Where(p => governo.ObterEstoque(p.teamId, recurso) >= quantidade)
                .OrderByDescending(p => ScoreContraparte(governo, compradorTeamId, p.teamId, recurso))
                .FirstOrDefault();

            if (vendedor == null)
            {
                mensagem = "sem vendedor com estoque";
                return false;
            }

            if (vendedor.teamId == governo.teamJogador)
            {
                PropostaInternacional proposta = new PropostaInternacional
                {
                    origemTeamId = compradorTeamId,
                    alvoTeamId = vendedor.teamId,
                    tipo = TipoPropostaInternacional.Compra,
                    recurso = recurso,
                    quantidade = quantidade,
                    precoUnitario = Mathf.Max(1, item.precoAtual),
                    prioridade = 72,
                    motivo = comprador.nomePais + " quer comprar " + item.nome,
                    expiraEm = Time.unscaledTime + 95f,
                    dedupKey = "deusa_buy:" + compradorTeamId + ":" + recurso
                };

                return governo.TentarCriarProposta(proposta);
            }

            return mercado.Comprar(compradorTeamId, vendedor.teamId, item.id, quantidade, out mensagem);
        }

        public bool TentarVenderRecurso(int vendedorTeamId, RecursoMercado recurso, int quantidade, bool permitirJogador, bool permitirOutrasIAs, out string mensagem)
        {
            mensagem = string.Empty;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (governo == null || mercado == null || recurso == RecursoMercado.Nenhum || quantidade <= 0)
            {
                mensagem = "bridge sem dependencias para venda";
                return false;
            }

            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(recurso));
            if (item == null)
            {
                mensagem = "item de mercado nao encontrado";
                return false;
            }

            DadosPaisGoverno comprador = governo.Paises
                .Where(p => p != null && p.teamId != vendedorTeamId)
                .Where(p => (permitirJogador || p.teamId != governo.teamJogador) && (permitirOutrasIAs || p.teamId == governo.teamJogador))
                .OrderByDescending(p => ScoreContraparte(governo, vendedorTeamId, p.teamId, recurso))
                .FirstOrDefault();

            if (comprador == null)
            {
                mensagem = "sem comprador disponivel";
                return false;
            }

            if (comprador.teamId == governo.teamJogador)
            {
                PropostaInternacional proposta = new PropostaInternacional
                {
                    origemTeamId = vendedorTeamId,
                    alvoTeamId = comprador.teamId,
                    tipo = TipoPropostaInternacional.Venda,
                    recurso = recurso,
                    quantidade = quantidade,
                    precoUnitario = Mathf.Max(1, item.precoAtual),
                    prioridade = 56,
                    motivo = governo.NomePais(vendedorTeamId) + " oferece " + item.nome,
                    expiraEm = Time.unscaledTime + 95f,
                    dedupKey = "deusa_sell:" + vendedorTeamId + ":" + recurso
                };

                return governo.TentarCriarProposta(proposta);
            }

            return mercado.Vender(vendedorTeamId, comprador.teamId, item.id, quantidade, out mensagem);
        }

        public bool TentarAplicarSancaoDireta(int origemTeamId, int alvoTeamId, out string mensagem)
        {
            mensagem = string.Empty;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || origemTeamId <= 0 || alvoTeamId <= 0 || origemTeamId == alvoTeamId)
            {
                mensagem = "sancao invalida";
                return false;
            }

            DadosPaisGoverno alvo = governo.ObterPais(alvoTeamId);
            if (alvo == null)
            {
                mensagem = "alvo nao encontrado";
                return false;
            }

            RelacaoPaisGoverno relacao = governo.ObterRelacao(origemTeamId, alvoTeamId);
            if (relacao.sancaoAtiva)
            {
                mensagem = "sancao ja ativa";
                return false;
            }

            alvo.sancionado = true;
            alvo.estabilidade = Mathf.Clamp(alvo.estabilidade - 6f, 0f, 100f);
            relacao.sancaoAtiva = true;
            relacao.tratadoComercial = false;
            relacao.valor = Mathf.Clamp(relacao.valor - 12, -100, 100);
            governo.RegistrarNoticia(governo.NomePais(origemTeamId) + " aplicou sancoes em " + governo.NomePais(alvoTeamId) + ".");
            SistemaMercadoGlobal.Instancia?.SimularMercado();
            mensagem = "sancao aplicada";
            return true;
        }

        public static PerfilPaisIA MapearPerfil(DeusaPersonalidade personalidade)
        {
            switch (personalidade)
            {
                case DeusaPersonalidade.Militarista:
                    return PerfilPaisIA.Militarista;
                case DeusaPersonalidade.Economica:
                    return PerfilPaisIA.Industrial;
                case DeusaPersonalidade.Naval:
                    return PerfilPaisIA.ProdutorPetroleo;
                case DeusaPersonalidade.Diplomatica:
                    return PerfilPaisIA.Aliado;
                case DeusaPersonalidade.Defensiva:
                    return PerfilPaisIA.Neutro;
                case DeusaPersonalidade.Expansionista:
                    return PerfilPaisIA.Rival;
                default:
                    return PerfilPaisIA.Neutro;
            }
        }

        public static ModoInicialPaisIA MapearModoInicial(DeusaModoInicial modo)
        {
            switch (modo)
            {
                case DeusaModoInicial.Paz:
                    return ModoInicialPaisIA.Paz;
                case DeusaModoInicial.Guerra:
                    return ModoInicialPaisIA.Mobilizacao;
                case DeusaModoInicial.Manual:
                    return ModoInicialPaisIA.Crescimento;
                default:
                    return ModoInicialPaisIA.Crescimento;
            }
        }

        private static float ScoreContraparte(SistemaGovernoMundial governo, int origem, int alvo, RecursoMercado recurso)
        {
            if (governo == null)
            {
                return 0f;
            }

            RelacaoPaisGoverno relacao = governo.ObterRelacao(origem, alvo);
            float score = relacao.valor * 10f;
            score += relacao.pactoMilitar ? 120f : 0f;
            score -= relacao.sancaoAtiva ? 500f : 0f;
            score += governo.ObterEstoque(alvo, recurso) * 0.25f;
            return score;
        }

        private static string GerarSimboloMoeda(string nomeMoeda)
        {
            if (string.IsNullOrWhiteSpace(nomeMoeda))
            {
                return "IA$";
            }

            string limpa = new string(nomeMoeda.Where(char.IsLetter).Take(2).ToArray()).ToUpperInvariant();
            return string.IsNullOrWhiteSpace(limpa) ? "IA$" : limpa + "$";
        }
    }
}
