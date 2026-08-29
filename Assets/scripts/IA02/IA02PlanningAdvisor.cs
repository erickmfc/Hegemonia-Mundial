using System;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>
    /// Camada somente de leitura que sugere a prioridade da nação.
    /// Não publica intents, não constrói e não compra unidades. A execução
    /// continua pertencendo aos diretores já testados.
    /// </summary>
    public sealed class IA02PlanningAdvisor
    {
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly bool enabled;
        private float nextRefreshAt;
        private string lastSignature = string.Empty;

        public string Status { get; private set; } = "Planejador aguardando dados.";
        public string Recommendation { get; private set; } = "Aguardar dados nacionais.";
        public int Priority { get; private set; }
        public string Guardrail { get; private set; } = "Execucao protegida: diretores atuais mantidos.";

        public IA02PlanningAdvisor(IA02RuntimeContext context, IA02WorldState world, bool enabled)
        {
            this.context = context;
            this.world = world;
            this.enabled = enabled;
        }

        public bool Refresh(float now, DadosPaisGoverno country, bool emergencyReserve)
        {
            if (!enabled)
            {
                Status = "Planejador desativado; execucao original preservada.";
                Recommendation = "Nenhuma recomendacao aplicada.";
                Priority = 0;
                return false;
            }

            if (now < nextRefreshAt)
            {
                return false;
            }

            nextRefreshAt = now + 2f;
            if (country == null)
            {
                Status = "Planejador aguardando dados nacionais.";
                Recommendation = "Aguardar sincronizacao do governo.";
                Priority = 0;
                return true;
            }

            // A ameaça de proximidade continua sendo decidida pelo WarDirector,
            // que possui a referência correta da prefeitura. Aqui usamos apenas
            // o estado oficial de guerra para não duplicar essa lógica sensível.
            bool threatened = country.emGuerra;
            bool foodCritical = country.comida <= 0 || country.deficitComida > 0.01f;
            bool energyCritical = country.energia <= 0 || country.deficitEnergia > 0.01f;
            bool housingCritical = country.populacao > 0
                && (country.moradia < 75f || country.pressaoHabitacional > 0.85f);
            bool treasuryCritical = country.saldo < 2500 || country.divida > Mathf.Max(1000f, country.saldo * 0.75f);
            bool hasShipyard = HasOwnedStructure("estaleiro", "shipyard", "estaleiros navais");
            bool hasAirfield = HasOwnedStructure("aeroporto militar", "aeroporto_militar", "military airport", "base aerea");

            string recommendation;
            int priority;
            if (threatened || emergencyReserve || country.emGuerra)
            {
                recommendation = "Reforcar defesa e manter a reserva militar.";
                priority = 100;
            }
            else if (foodCritical)
            {
                recommendation = "Garantir comida antes de expandir a cidade.";
                priority = 95;
            }
            else if (energyCritical)
            {
                recommendation = "Garantir energia antes de iniciar novas obras.";
                priority = 90;
            }
            else if (housingCritical)
            {
                recommendation = "Ampliar moradia em lotes residenciais validos.";
                priority = 75;
            }
            else if (treasuryCritical)
            {
                recommendation = "Preservar caixa e comprar somente recursos essenciais.";
                priority = 70;
            }
            else if (!hasAirfield)
            {
                recommendation = "Concluir o aeroporto militar antes de comprar mais cacas.";
                priority = 60;
            }
            else if (!hasShipyard)
            {
                recommendation = "Concluir o estaleiro antes de expandir a frota.";
                priority = 55;
            }
            else
            {
                recommendation = "Expandir gradualmente, respeitando os creates e os intervalos.";
                priority = 40;
            }

            Priority = priority;
            Recommendation = recommendation;
            string signature = priority + "|" + recommendation;
            Status = "Planejador: prioridade=" + priority + " recomendacao=" + recommendation;
            if (context != null)
            {
                context.SetMetric("ia02.planning.priority", priority);
                context.SetMetric("ia02.planning.threatened", threatened ? 1d : 0d);
                context.SetMetric("ia02.planning.food_critical", foodCritical ? 1d : 0d);
                context.SetMetric("ia02.planning.energy_critical", energyCritical ? 1d : 0d);
                context.SetMetric("ia02.planning.housing_critical", housingCritical ? 1d : 0d);
                context.SetMetric("ia02.planning.has_airfield", hasAirfield ? 1d : 0d);
                context.SetMetric("ia02.planning.has_shipyard", hasShipyard ? 1d : 0d);
            }

            bool changed = !string.Equals(lastSignature, signature, StringComparison.Ordinal);
            lastSignature = signature;
            if (changed)
            {
                Debug.Log("[IA02 Planning] " + Status + " | " + Guardrail);
            }
            return changed;
        }

        private bool HasOwnedStructure(params string[] tokens)
        {
            if (world == null || world.OwnedStructures == null || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < world.OwnedStructures.Count; i++)
            {
                IdentidadeUnidade identity = world.OwnedStructures[i];
                if (identity == null || identity.gameObject == null)
                {
                    continue;
                }

                string text = (identity.gameObject.name + " " + identity.name).ToLowerInvariant();
                for (int j = 0; j < tokens.Length; j++)
                {
                    if (!string.IsNullOrWhiteSpace(tokens[j]) && text.Contains(tokens[j].ToLowerInvariant()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
