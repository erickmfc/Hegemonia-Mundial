using UnityEngine;

namespace Hegemonia.AI.IA01
{
    // Read-only adapter: the simulation remains the source of truth and IA01 only mirrors observations.
    public sealed class IA01GameStateBridge
    {
        private int industrialResourceCursor;

        public int LastChangedResources { get; private set; }

        public int Refresh(IA01RuntimeContext context, int maxIndustrialResources)
        {
            LastChangedResources = 0;
            if (context == null || context.TeamId <= 0)
            {
                return 0;
            }

            int inspectedResources = RefreshIndustrialResources(context, Mathf.Max(1, maxIndustrialResources));
            RefreshMarketMetrics(context);
            RefreshTimeMetrics(context);
            return inspectedResources;
        }

        private int RefreshIndustrialResources(IA01RuntimeContext context, int maxResources)
        {
            SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
            if (industrial == null)
            {
                return 0;
            }

            string[] resources = IndustriaIds.TodosOsMateriais;
            if (resources == null || resources.Length == 0)
            {
                return 0;
            }

            int inspected = Mathf.Min(resources.Length, Mathf.Max(1, maxResources));
            for (int i = 0; i < inspected; i++)
            {
                string resourceId = resources[industrialResourceCursor];
                industrialResourceCursor = (industrialResourceCursor + 1) % resources.Length;
                double amount = industrial.ObterQuantidadePais(context.TeamId, resourceId);
                if (context.SetResourceSnapshot(resourceId, (float)System.Math.Max(0d, amount), 0f, 0f, "industrial"))
                {
                    LastChangedResources++;
                }
            }

            EstadoIndustrialPais state = industrial.ObterEstadoPais(context.TeamId);
            if (state != null)
            {
                context.SetMetric("industrial.level", state.nivelFabrica);
                context.SetMetric("industrial.lines_available", state.linhasDisponiveis);
                context.SetMetric("industrial.lines_busy", state.linhasOcupadas);
                context.SetMetric("industrial.orders_active", state.ordensAtivas);
                context.SetMetric("industrial.daily_output", state.producaoDiariaTotal);
                context.SetMetric("industrial.efficiency", state.eficienciaIndustrial * 100d);
                context.SetMetric("industrial.stability", state.estabilidadeNacional * 100d);
                context.SetMetric("industrial.capacity", state.capacidadeIndustrial * 100d);
            }

            return inspected;
        }

        private static void RefreshMarketMetrics(IA01RuntimeContext context)
        {
            SistemaMercadoGlobal market = SistemaMercadoGlobal.Instancia;
            if (market == null || market.itens == null)
            {
                return;
            }

            int activeItems = 0;
            float totalDemand = 0f;
            float totalOffer = 0f;
            for (int i = 0; i < market.itens.Count; i++)
            {
                DadosItemMercado item = market.itens[i];
                if (item == null)
                {
                    continue;
                }

                activeItems++;
                totalDemand += item.demanda;
                totalOffer += item.oferta;
            }

            context.SetMetric("market.items", activeItems);
            context.SetMetric("market.demand", totalDemand);
            context.SetMetric("market.offer", totalOffer);
        }

        private static void RefreshTimeMetrics(IA01RuntimeContext context)
        {
            GerenciadorTempo timeService = GerenciadorTempo.Instancia;
            if (timeService != null)
            {
                context.SetMetric("time.day", timeService.totalDias);
            }
        }
    }
}
