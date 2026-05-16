using Hegemonia.AI.BrainMaster;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaAerea
    {
        public int MetaAeronaves { get; private set; }
        public int MinimoEsquadrao { get; private set; }
        public int MinimoAtaquePesado { get; private set; }
        public bool PrecisaAviacao { get; private set; }
        public string UltimoResumo { get; private set; } = "aeronautica nao avaliada";

        public void Atualizar(IA_ForceSnapshot snapshot, IA_DeusaEspionagemSnapshot espionagem, IA_DeusaPoliticaEstagio politica)
        {
            if (snapshot == null || politica == null)
            {
                return;
            }

            MinimoEsquadrao = politica.MinimoEsquadraoAereo;
            MinimoAtaquePesado = politica.MinimoAtaqueAereoPesado;
            MetaAeronaves = MinimoEsquadrao <= 0
                ? 0
                : UnityEngine.Mathf.Max(MinimoEsquadrao, UnityEngine.Mathf.RoundToInt((espionagem != null ? espionagem.EstimativaAerea : 0) * 1.25f));
            PrecisaAviacao = snapshot.FixedWingAircraft < MetaAeronaves;

            UltimoResumo = "aeronaves=" + snapshot.FixedWingAircraft + "/" + MetaAeronaves
                           + " | esquadraoMin=" + MinimoEsquadrao
                           + " | ataquePesado=" + MinimoAtaquePesado;
        }
    }
}
