using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaMarinha
    {
        public int MetaNavios { get; private set; }
        public int MetaPetroleiros { get; private set; }
        public int MetaTransportes { get; private set; }
        public int MetaPiers { get; private set; }
        public int MetaPlataformas { get; private set; }
        public bool PrecisaNavio { get; private set; }
        public bool PrecisaPetroleiro { get; private set; }
        public bool PrecisaTransporte { get; private set; }
        public string UltimoResumo { get; private set; } = "marinha nao avaliada";

        public void Atualizar(IA_ForceSnapshot snapshot, DeusaEstagio estagio, IA_DeusaMapaMemoria mapa)
        {
            if (snapshot == null || mapa == null)
            {
                return;
            }

            if (estagio < DeusaEstagio.ProjecaoRegional || !mapa.TemAreaNavalValida)
            {
                MetaNavios = 0;
                MetaPetroleiros = 0;
                MetaTransportes = 0;
                MetaPiers = 0;
                MetaPlataformas = 0;
                PrecisaNavio = false;
                PrecisaPetroleiro = false;
                PrecisaTransporte = false;
                UltimoResumo = "marinha adiada";
                return;
            }

            MetaNavios = estagio >= DeusaEstagio.GuerraTotal ? 6 : estagio >= DeusaEstagio.TensaoGeopolitica ? 4 : 2;
            MetaPetroleiros = snapshot.PlatformCount > 0 && snapshot.PierCount > 0 ? 1 : 0;
            MetaTransportes = estagio >= DeusaEstagio.GuerraTotal ? 1 : 0;
            MetaPiers = 1;
            MetaPlataformas = estagio >= DeusaEstagio.ProjecaoRegional ? 1 : 0;

            PrecisaNavio = snapshot.NavalUnits < MetaNavios;
            PrecisaPetroleiro = snapshot.OilTankers < MetaPetroleiros;
            PrecisaTransporte = snapshot.NavalTransports < MetaTransportes;
            UltimoResumo = "navios=" + snapshot.NavalUnits + "/" + MetaNavios
                           + " | petroleiros=" + snapshot.OilTankers + "/" + MetaPetroleiros
                           + " | transportes=" + snapshot.NavalTransports + "/" + MetaTransportes;
        }
    }
}
