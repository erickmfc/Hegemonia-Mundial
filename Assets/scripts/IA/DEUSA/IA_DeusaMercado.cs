using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaMercado
    {
        private float _nextActionTime;

        public string UltimoResumo { get; private set; } = "mercado aguardando";

        public void Atualizar(
            IA_DeusaGovernoBridge governoBridge,
            IA_DeusaConfig config,
            DadosPaisGoverno pais,
            IA_DeusaComida comida,
            IA_DeusaEconomia economia,
            IA_DeusaLogistica logistica,
            float now)
        {
            if (governoBridge == null || pais == null || now < _nextActionTime)
            {
                return;
            }

            string mensagem;
            bool executou = false;
            if (comida != null && comida.PrecisaComprar)
            {
                executou = governoBridge.TentarComprarRecurso(
                    pais.teamId,
                    RecursoMercado.Comida,
                    100,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else if (logistica != null && logistica.PrecisaPetroleoMercado)
            {
                executou = governoBridge.TentarComprarRecurso(
                    pais.teamId,
                    RecursoMercado.Petroleo,
                    120,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else if (economia != null && economia.PrecisaIndustria && pais.aco < 260)
            {
                executou = governoBridge.TentarComprarRecurso(
                    pais.teamId,
                    RecursoMercado.Aco,
                    80,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else if (comida != null && comida.PodeVenderExcedente)
            {
                executou = governoBridge.TentarVenderRecurso(
                    pais.teamId,
                    RecursoMercado.Comida,
                    100,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else if (pais.petroleo > 1200)
            {
                executou = governoBridge.TentarVenderRecurso(
                    pais.teamId,
                    RecursoMercado.Petroleo,
                    120,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else if (pais.aco > 900)
            {
                executou = governoBridge.TentarVenderRecurso(
                    pais.teamId,
                    RecursoMercado.Aco,
                    90,
                    config != null && config.permitirComercioComJogador,
                    config == null || config.permitirComercioComOutrasIAs,
                    out mensagem);
            }
            else
            {
                mensagem = "sem necessidade urgente de mercado";
            }

            _nextActionTime = now + (executou ? 18f : 8f);
            UltimoResumo = mensagem;
        }
    }
}
