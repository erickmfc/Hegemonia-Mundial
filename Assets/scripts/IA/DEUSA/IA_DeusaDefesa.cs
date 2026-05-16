using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaDefesa
    {
        public int MetaRadar { get; private set; }
        public int MetaCiws { get; private set; }
        public bool PrecisaRadar { get; private set; }
        public bool PrecisaCiws { get; private set; }
        public string UltimoResumo { get; private set; } = "defesa nao avaliada";

        public void Atualizar(IA_ForceSnapshot snapshot, DeusaEstagio estagio, IA_DeusaEspionagemSnapshot espionagem)
        {
            if (snapshot == null)
            {
                return;
            }

            MetaRadar = estagio >= DeusaEstagio.MilitarizacaoDefensiva ? 1 : 0;
            MetaCiws = estagio >= DeusaEstagio.TensaoGeopolitica || (espionagem != null && espionagem.EstimativaAerea >= 4) ? 1 : 0;
            PrecisaRadar = snapshot.RadarCount < MetaRadar;
            PrecisaCiws = MetaCiws > 0 && snapshot.ReadyAircraft + snapshot.RadarCount > 0;

            UltimoResumo = "radar=" + snapshot.RadarCount + "/" + MetaRadar
                           + " | ciws=" + MetaCiws
                           + " | precisaRadar=" + PrecisaRadar;
        }
    }
}
