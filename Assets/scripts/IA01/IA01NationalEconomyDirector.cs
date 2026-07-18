using System;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>Coordinates market and diplomatic actions without competing with construction or defense.</summary>
    public sealed class IA01NationalEconomyDirector
    {
        private readonly IA01RuntimeContext context;
        private float nextActionAt;
        private string lastAction = "Mercado aguardando avaliacao.";

        public string Status => lastAction;

        public IA01NationalEconomyDirector(IA01RuntimeContext context)
        {
            this.context = context;
        }

        public bool Plan(float now, IA01IntentBoard board, bool emergencyReserve, bool constructionPending, bool threatened)
        {
            if (now < nextActionAt || board == null)
            {
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal market = SistemaMercadoGlobal.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            if (government == null || market == null || country == null)
            {
                lastAction = "Mercado indisponivel: aguardando servicos nacionais.";
                return false;
            }

            foreach (IA01Intent intent in board.All)
            {
                if (intent != null && intent.Type == IA01IntentType.EstablishCapital)
                {
                    nextActionAt = now + 4f;
                    lastAction = "Mercado aguardando fundacao da prefeitura.";
                    return false;
                }
            }

            if (HasPendingProposal(government))
            {
                nextActionAt = now + 8f;
                lastAction = "Mercado aguardando proposta pendente.";
                return false;
            }

            RecursoMercado need = IA_EconomyDirector.ResolveCriticalNeed(country);
            if (need != RecursoMercado.Nenhum)
            {
                board.Publish(IA01IntentType.BuyResource, threatened ? 930 : 760, "Comprar " + need, now);
                bool changed = TryBuy(government, market, country, need);
                nextActionAt = now + (changed ? 8f : 4f);
                lastAction = constructionPending && !changed
                    ? "Compra critica nao concluida, mas a fila de obra nao bloqueia mais o mercado."
                    : lastAction;
                return changed;
            }

            RecursoMercado surplus = IA_EconomyDirector.ResolveBestSurplus(country);
            if (surplus != RecursoMercado.Nenhum && !threatened && !emergencyReserve)
            {
                board.Publish(IA01IntentType.SellResource, 360, "Vender excedente de " + surplus, now);
                bool changed = TrySell(government, market, country, surplus);
                nextActionAt = now + 12f;
                return changed;
            }

            lastAction = threatened ? "Venda bloqueada: capital sob ameaca." : constructionPending ? "Mercado estavel: fila de construcao nao bloqueia a avaliacao." : "Mercado estavel: sem operacao necessaria.";
            nextActionAt = now + 8f;
            return false;
        }

        private bool TryBuy(SistemaGovernoMundial government, SistemaMercadoGlobal market, DadosPaisGoverno buyer, RecursoMercado resource)
        {
            DadosItemMercado item = market.ObterItem(SistemaGovernoMundial.IdRecurso(resource));
            if (item == null)
            {
                lastAction = "Compra bloqueada: item nao existe no catalogo do mercado.";
                return false;
            }

            DadosPaisGoverno seller = null;
            float bestScore = float.MinValue;
            int desiredQuantity = item.CalcularQuantidadePadrao();
            for (int i = 0; i < government.Paises.Count; i++)
            {
                DadosPaisGoverno candidate = government.Paises[i];
                if (candidate == null || candidate.teamId == buyer.teamId)
                {
                    continue;
                }

                if (government.ObterEstoque(candidate.teamId, resource) < desiredQuantity)
                {
                    continue;
                }

                float score = ScoreSeller(government, buyer, candidate, resource);
                if (seller == null || score > bestScore)
                {
                    seller = candidate;
                    bestScore = score;
                }
            }
            if (seller == null)
            {
                lastAction = "Compra bloqueada: nenhum vendedor possui " + resource + ".";
                return false;
            }

            int quantity = Mathf.Clamp(desiredQuantity, 1, buyer.saldo / Mathf.Max(1, item.precoAtual));
            if (quantity <= 0)
            {
                lastAction = "Compra bloqueada: saldo livre insuficiente para " + resource + ".";
                return false;
            }

            RelacaoPaisGoverno relation = government.ObterRelacao(buyer.teamId, seller.teamId);
            if (relation != null && (relation.sancaoAtiva || relation.valor < -80))
            {
                lastAction = "Compra bloqueada: relacao ou sancao impede comercio.";
                return false;
            }

            if (seller.teamId == government.teamJogador)
            {
                bool created = government.TentarCriarProposta(new PropostaInternacional
                {
                    origemTeamId = buyer.teamId,
                    alvoTeamId = seller.teamId,
                    tipo = TipoPropostaInternacional.Compra,
                    recurso = resource,
                    quantidade = quantity,
                    precoUnitario = Mathf.Max(1, item.precoAtual),
                    prioridade = 80,
                    motivo = buyer.nomePais + " precisa de " + item.nome,
                    dedupKey = "ia01:buy:" + buyer.teamId + ":" + seller.teamId + ":" + resource
                });
                lastAction = created ? "Proposta de compra enviada: " + item.nome + "." : "Compra aguardando proposta existente.";
                return created;
            }

            bool bought = market.Comprar(buyer.teamId, seller.teamId, item.id, quantity, out string message);
            lastAction = bought ? "Comprado " + item.nome + " x" + quantity + "." : "Compra falhou: " + message;
            return bought;
        }

        private bool TrySell(SistemaGovernoMundial government, SistemaMercadoGlobal market, DadosPaisGoverno seller, RecursoMercado resource)
        {
            DadosItemMercado item = market.ObterItem(SistemaGovernoMundial.IdRecurso(resource));
            if (item == null)
            {
                lastAction = "Venda bloqueada: item nao existe no catalogo do mercado.";
                return false;
            }

            int stock = government.ObterEstoque(seller.teamId, resource);
            int reserve = resource == RecursoMercado.Energia ? 420 : resource == RecursoMercado.Comida ? 650 : 320;
            int quantity = Mathf.Min(item.CalcularQuantidadePadrao(), Mathf.Max(0, stock - reserve));
            if (quantity <= 0)
            {
                lastAction = "Venda bloqueada: excedente abaixo da reserva nacional.";
                return false;
            }

            DadosPaisGoverno buyer = null;
            int bestSaldo = int.MinValue;
            for (int i = 0; i < government.Paises.Count; i++)
            {
                DadosPaisGoverno candidate = government.Paises[i];
                if (candidate == null || candidate.teamId == seller.teamId)
                {
                    continue;
                }

                if (IA_EconomyDirector.ResolveCriticalNeed(candidate) != resource)
                {
                    continue;
                }

                if (buyer == null || candidate.saldo > bestSaldo)
                {
                    buyer = candidate;
                    bestSaldo = candidate.saldo;
                }
            }
            if (buyer == null)
            {
                lastAction = "Venda aguardando comprador para " + resource + ".";
                return false;
            }

            if (buyer.teamId == government.teamJogador)
            {
                bool created = government.TentarCriarProposta(new PropostaInternacional
                {
                    origemTeamId = seller.teamId,
                    alvoTeamId = buyer.teamId,
                    tipo = TipoPropostaInternacional.Venda,
                    recurso = resource,
                    quantidade = quantity,
                    precoUnitario = Mathf.Max(1, item.precoAtual),
                    prioridade = 55,
                    motivo = seller.nomePais + " oferece excedente de " + item.nome,
                    dedupKey = "ia01:sell:" + seller.teamId + ":" + buyer.teamId + ":" + resource
                });
                lastAction = created ? "Oferta de venda enviada: " + item.nome + "." : "Venda aguardando proposta existente.";
                return created;
            }

            bool sold = market.Vender(seller.teamId, buyer.teamId, item.id, quantity, out string message);
            lastAction = sold ? "Vendido " + item.nome + " x" + quantity + "." : "Venda falhou: " + message;
            return sold;
        }

        private bool HasPendingProposal(SistemaGovernoMundial government)
        {
            if (government == null || government.Propostas == null)
            {
                return false;
            }

            for (int i = 0; i < government.Propostas.Count; i++)
            {
                PropostaInternacional proposal = government.Propostas[i];
                if (proposal != null && proposal.EstaPendente &&
                    (proposal.origemTeamId == context.TeamId || proposal.alvoTeamId == context.TeamId))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ScoreSeller(SistemaGovernoMundial government, DadosPaisGoverno buyer, DadosPaisGoverno seller, RecursoMercado resource)
        {
            RelacaoPaisGoverno relation = government.ObterRelacao(buyer.teamId, seller.teamId);
            return government.ObterEstoque(seller.teamId, resource)
                + (relation != null ? relation.valor * 8f : 0f)
                + (relation != null && relation.pactoMilitar ? 500f : 0f)
                - (relation != null && relation.sancaoAtiva ? 5000f : 0f);
        }
    }
}
