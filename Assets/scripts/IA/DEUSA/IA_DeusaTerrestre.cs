using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaTerrestre
    {
        public int MetaInfantaria { get; private set; }
        public int MetaTanques { get; private set; }
        public bool PrecisaInfantaria { get; private set; }
        public bool PrecisaTanques { get; private set; }
        public string UltimoResumo { get; private set; } = "exercito nao avaliado";

        public void Atualizar(IA_ForceSnapshot snapshot, IA_DeusaEspionagemSnapshot espionagem, DeusaEstagio estagio)
        {
            if (snapshot == null)
            {
                return;
            }

            int baseInf = estagio >= DeusaEstagio.GuerraTotal ? 18 : estagio >= DeusaEstagio.MilitarizacaoDefensiva ? 10 : 4;
            int baseTank = estagio >= DeusaEstagio.GuerraTotal ? 10 : estagio >= DeusaEstagio.MilitarizacaoDefensiva ? 4 : 0;
            int inimigoTerrestre = espionagem != null ? espionagem.EstimativaTerrestre : 0;

            MetaInfantaria = Mathf.Max(baseInf, Mathf.RoundToInt(inimigoTerrestre * 1.15f));
            MetaTanques = Mathf.Max(baseTank, Mathf.RoundToInt(inimigoTerrestre * 0.35f));
            PrecisaInfantaria = snapshot.InfantryUnits < MetaInfantaria;
            PrecisaTanques = snapshot.TankUnits < MetaTanques;

            UltimoResumo = "infantaria=" + snapshot.InfantryUnits + "/" + MetaInfantaria
                           + " | tanques=" + snapshot.TankUnits + "/" + MetaTanques;
        }
    }
}
