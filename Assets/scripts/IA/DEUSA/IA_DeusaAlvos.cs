using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;

namespace Hegemonia.AI.DEUSA
{
    public static class IA_DeusaTargetRegistry
    {
        private static readonly Dictionary<int, Dictionary<IA_StrategicTargetKind, float>> PrioridadesPorTime =
            new Dictionary<int, Dictionary<IA_StrategicTargetKind, float>>();

        public static void AtualizarPrioridades(int teamId, Dictionary<IA_StrategicTargetKind, float> prioridades)
        {
            if (teamId <= 0 || prioridades == null)
            {
                return;
            }

            PrioridadesPorTime[teamId] = prioridades;
        }

        public static float ResolverPrioridade(int teamId, IA_StrategicTargetKind kind, float fallback)
        {
            Dictionary<IA_StrategicTargetKind, float> prioridades;
            float valor;
            if (PrioridadesPorTime.TryGetValue(teamId, out prioridades) && prioridades != null && prioridades.TryGetValue(kind, out valor))
            {
                return valor;
            }

            return fallback;
        }
    }

    public sealed class IA_DeusaAlvos
    {
        public string UltimoResumo { get; private set; } = "alvos nao avaliados";

        public void Atualizar(IA_DeusaIdentidadeNacional identidade, IA_DeusaConfig config, IA_DeusaPoliticaEstagio politica)
        {
            if (identidade == null || politica == null)
            {
                return;
            }

            Dictionary<IA_StrategicTargetKind, float> pesos = new Dictionary<IA_StrategicTargetKind, float>
            {
                { IA_StrategicTargetKind.Radar, 420f },
                { IA_StrategicTargetKind.Energy, 395f },
                { IA_StrategicTargetKind.OilPlatform, 380f },
                { IA_StrategicTargetKind.OilTanker, 365f },
                { IA_StrategicTargetKind.Airport, 350f },
                { IA_StrategicTargetKind.Shipyard, 340f },
                { IA_StrategicTargetKind.Pier, 330f },
                { IA_StrategicTargetKind.Barracks, 320f },
                { IA_StrategicTargetKind.Factory, 305f },
                { IA_StrategicTargetKind.Industry, 285f },
                { IA_StrategicTargetKind.Farm, 245f },
                { IA_StrategicTargetKind.Defense, 220f },
                { IA_StrategicTargetKind.ReadyAircraft, 310f },
                { IA_StrategicTargetKind.NavalPatrol, 235f },
                { IA_StrategicTargetKind.CityHall, politica.PriorizarGuerraTotal && (config == null || config.permitirGuerraTotal) ? 180f : 120f }
            };

            if (identidade.personalidade == DeusaPersonalidade.Naval)
            {
                pesos[IA_StrategicTargetKind.OilPlatform] += 18f;
                pesos[IA_StrategicTargetKind.OilTanker] += 15f;
                pesos[IA_StrategicTargetKind.Shipyard] += 14f;
                pesos[IA_StrategicTargetKind.Pier] += 14f;
            }
            else if (identidade.personalidade == DeusaPersonalidade.Aerea)
            {
                pesos[IA_StrategicTargetKind.Radar] += 18f;
                pesos[IA_StrategicTargetKind.Airport] += 18f;
                pesos[IA_StrategicTargetKind.ReadyAircraft] += 18f;
            }
            else if (identidade.personalidade == DeusaPersonalidade.Defensiva)
            {
                pesos[IA_StrategicTargetKind.Radar] += 10f;
                pesos[IA_StrategicTargetKind.Barracks] += 8f;
                pesos[IA_StrategicTargetKind.Defense] += 10f;
            }

            IA_DeusaTargetRegistry.AtualizarPrioridades(identidade.teamID, pesos);
            UltimoResumo = "prioridade radar>energia>petroleo>prefeitura-final";
        }
    }
}
