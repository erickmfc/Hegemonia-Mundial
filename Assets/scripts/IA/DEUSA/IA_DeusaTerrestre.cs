using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaTerrestre
    {
        private const int MetaMinimaTanquesComFabrica = 4;

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

            int baseInf = estagio >= DeusaEstagio.GuerraTotal ? 14 : estagio >= DeusaEstagio.MilitarizacaoDefensiva ? 8 : 4;
            int baseTank = estagio >= DeusaEstagio.GuerraTotal ? 10 : estagio >= DeusaEstagio.ProjecaoPoder ? 6 : estagio >= DeusaEstagio.MilitarizacaoDefensiva ? 4 : 2;
            int inimigoTerrestre = espionagem != null ? espionagem.EstimativaTerrestre : 0;

            MetaInfantaria = Mathf.Clamp(Mathf.Max(baseInf, Mathf.RoundToInt(inimigoTerrestre * 0.90f)), 4, 24);
            MetaTanques = Mathf.Clamp(Mathf.Max(baseTank, Mathf.RoundToInt(inimigoTerrestre * 0.30f)), 2, 14);
            if (snapshot.HasFactory && estagio >= DeusaEstagio.MilitarizacaoDefensiva)
            {
                MetaTanques = Mathf.Max(MetaTanques, MetaMinimaTanquesComFabrica);
            }
            PrecisaInfantaria = snapshot.InfantryUnits < MetaInfantaria;
            PrecisaTanques = snapshot.TankUnits < MetaTanques;

            UltimoResumo = "infantaria=" + snapshot.InfantryUnits + "/" + MetaInfantaria
                           + " | tanques=" + snapshot.TankUnits + "/" + MetaTanques;
        }
    }
}
