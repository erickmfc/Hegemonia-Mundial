using System;

namespace Hegemonia.AI.Shared
{
    /// <summary>
    /// Executa produção ou construção militar usando o saldo nacional compartilhado.
    /// Ordens recusadas devolvem o custo ao mesmo caixa.
    /// </summary>
    public static class IA_NationalTreasury
    {
        public static bool TentarProduzirComTesouroNacional(
            int teamId,
            DadosConstrucao item,
            Func<bool> producao)
        {
            return TentarExecutarComTesouroNacional(teamId, item, producao);
        }

        public static bool TentarConstruirComTesouroNacional(
            int teamId,
            DadosConstrucao item,
            Func<bool> construcao)
        {
            return TentarExecutarComTesouroNacional(teamId, item, construcao);
        }

        private static bool TentarExecutarComTesouroNacional(
            int teamId,
            DadosConstrucao item,
            Func<bool> operacao)
        {
            if (teamId <= 0 || item == null || operacao == null)
            {
                return false;
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || governo.ObterPais(teamId) == null)
            {
                return false;
            }

            long custo = Math.Max(0L, item.ObterPrecoEfetivo());
            if (!governo.TentarPagar(teamId, custo))
            {
                return false;
            }

            bool aceita = false;
            try
            {
                aceita = operacao();
                return aceita;
            }
            finally
            {
                if (!aceita)
                {
                    governo.AdicionarSaldo(teamId, custo);
                }
            }
        }
    }
}
