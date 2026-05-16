using Hegemonia.AI.BrainMaster;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaEconomia
    {
        public bool PrecisaEnergia { get; private set; }
        public bool PrecisaIndustria { get; private set; }
        public bool PrecisaExpansao { get; private set; }
        public string PlanoEconomico { get; private set; } = "Equilibrio";
        public string UltimoResumo { get; private set; } = "economia nao avaliada";

        public void Atualizar(
            DadosPaisGoverno pais,
            DadosEconomiaPais economia,
            IA_DeusaComida comida,
            IA_DeusaHabitacao habitacao,
            IA_DeusaPoliticaEstagio politica,
            IA_NationalDecisionState estadoNacional)
        {
            if (pais == null || politica == null)
            {
                return;
            }

            PrecisaEnergia = economia != null && (economia.deficitEnergia > 0.5f || economia.estruturasSemEnergia > 0);
            PrecisaIndustria = politica.PriorizarIndustria && (economia == null || economia.industriaProduzida < 4f);
            PrecisaExpansao = politica.PriorizarExpansao;

            if (PrecisaEnergia)
            {
                PlanoEconomico = "ConstruirEnergia";
            }
            else if (comida != null && comida.PrecisaFazenda)
            {
                PlanoEconomico = "ConstruirFarm";
            }
            else if (habitacao != null && habitacao.PrecisaCasas)
            {
                PlanoEconomico = "ConstruirCasas";
            }
            else if (politica.PriorizarIndustria)
            {
                PlanoEconomico = "Industrializar";
            }
            else if (politica.PriorizarExpansao)
            {
                PlanoEconomico = "Expandir";
            }
            else
            {
                PlanoEconomico = "Equilibrio";
            }

            if (estadoNacional != null)
            {
                estadoNacional.StrategicPlan = PlanoEconomico;
            }

            UltimoResumo = PlanoEconomico
                           + " | energia=" + PrecisaEnergia
                           + " | industria=" + PrecisaIndustria
                           + " | expansao=" + PrecisaExpansao;
        }
    }
}
