using System.Collections.Generic;
using System.Text;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaDebugPanel
    {
        public string statusGeral = "DEUSA aguardando";
        public string statusEconomia = string.Empty;
        public string statusMilitar = string.Empty;
        public string statusEspionagem = string.Empty;
        public string statusMapa = string.Empty;
        public string proximaPrioridade = "Nenhuma";
        public string proximaConstrucao = "Nenhuma";
        public string alvoDesejado = "Nenhum";

        public void Atualizar(
            IA_DeusaIdentidadeNacional identidade,
            IA_DeusaConfig config,
            IA_DeusaPoliticaNacional politica,
            IList<IA_DeusaPrioridade> prioridades,
            string economia,
            string militar,
            string espionagem,
            string mapa)
        {
            statusEconomia = economia;
            statusMilitar = militar;
            statusEspionagem = espionagem;
            statusMapa = mapa;

            if (prioridades != null && prioridades.Count > 0)
            {
                proximaPrioridade = prioridades[0].ToString();
            }
            else
            {
                proximaPrioridade = "Nenhuma";
            }

            proximaConstrucao = politica != null ? politica.proximaConstrucao : "Nenhuma";
            alvoDesejado = politica != null ? politica.alvoPrioritario : "Nenhum";

            StringBuilder sb = new StringBuilder(256);
            sb.Append(identidade != null ? identidade.ResumoCurto() : "sem identidade");
            if (config != null)
            {
                sb.Append(" | modo=").Append(config.modoInicial);
                if (config.modoObservadorDebug)
                {
                    sb.Append(" | observador");
                }
            }

            if (politica != null)
            {
                sb.Append("\nPolitica: ").Append(politica.ResumoCurto());
            }

            sb.Append("\nPrioridade: ").Append(proximaPrioridade);
            sb.Append("\nConstrucao: ").Append(proximaConstrucao);
            sb.Append("\nAlvo: ").Append(alvoDesejado);
            statusGeral = sb.ToString();
        }
    }
}
