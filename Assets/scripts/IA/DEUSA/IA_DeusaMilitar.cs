using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaMilitar
    {
        public string UltimoResumo { get; private set; } = "militar nao avaliado";

        public void Atualizar(
            IA_BrainMaster brain,
            IA_DeusaConfig config,
            IA_ForceSnapshot snapshot,
            IA_DeusaEspionagemSnapshot espionagem,
            IA_DeusaTerrestre terrestre,
            IA_DeusaAerea aerea,
            IA_DeusaMarinha marinha,
            IA_DeusaDefesa defesa,
            DeusaEstagio estagio)
        {
            if (brain == null || snapshot == null)
            {
                return;
            }

            float mult = config != null ? config.MultiplicadorMilitar() : 1f;
            int enemyNaval = espionagem != null ? espionagem.EstimativaNaval : 0;
            int enemyAir = espionagem != null ? espionagem.EstimativaAerea : 0;

            brain.TargetFleet = Mathf.Max(brain.TargetFleet, Mathf.CeilToInt(Mathf.Max(marinha != null ? marinha.MetaNavios : 0, enemyNaval * 1.20f) * mult));
            brain.TargetAircraft = Mathf.Max(brain.TargetAircraft, Mathf.CeilToInt(Mathf.Max(aerea != null ? aerea.MetaAeronaves : 0, enemyAir * 1.25f) * mult));
            brain.TargetOilTankers = Mathf.Max(brain.TargetOilTankers, marinha != null ? marinha.MetaPetroleiros : 0);
            brain.TargetPiers = Mathf.Max(brain.TargetPiers, marinha != null ? marinha.MetaPiers : 0);
            brain.TargetPlatforms = Mathf.Max(brain.TargetPlatforms, marinha != null ? marinha.MetaPlataformas : 0);
            brain.TargetRadars = Mathf.Max(brain.TargetRadars, defesa != null ? defesa.MetaRadar : 0);
            brain.TargetCiws = Mathf.Max(brain.TargetCiws, defesa != null ? defesa.MetaCiws : 0);
            brain.StrategicPhase = MapearFase(estagio);
            brain.ActiveImperialPlan = "DEUSA: " + estagio;

            UltimoResumo = "fase=" + brain.StrategicPhase
                           + " | inf=" + (terrestre != null ? terrestre.MetaInfantaria : 0)
                           + " | tanque=" + (terrestre != null ? terrestre.MetaTanques : 0)
                           + " | ar=" + brain.TargetAircraft
                           + " | mar=" + brain.TargetFleet;
        }

        private static IA_StrategicPhase MapearFase(DeusaEstagio estagio)
        {
            switch (estagio)
            {
                case DeusaEstagio.Inicializacao:
                case DeusaEstagio.FundacaoNacional:
                    return IA_StrategicPhase.Abertura;
                case DeusaEstagio.OrganizacaoEconomica:
                case DeusaEstagio.ExpansaoTerritorial:
                    return IA_StrategicPhase.Expansao;
                case DeusaEstagio.Industrializacao:
                    return IA_StrategicPhase.LogisticaPetroleo;
                case DeusaEstagio.MilitarizacaoDefensiva:
                    return IA_StrategicPhase.DefesaCosteira;
                case DeusaEstagio.ProjecaoRegional:
                case DeusaEstagio.TensaoGeopolitica:
                    return IA_StrategicPhase.PressaoEconomica;
                default:
                    return IA_StrategicPhase.Dominacao;
            }
        }
    }
}
