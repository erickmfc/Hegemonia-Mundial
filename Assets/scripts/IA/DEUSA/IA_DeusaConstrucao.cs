using Hegemonia.AI.BrainMaster;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaConstrucao
    {
        public bool PrecisaHQ { get; private set; }
        public bool PrecisaCasas { get; private set; }
        public bool PrecisaFazenda { get; private set; }
        public bool PrecisaEnergia { get; private set; }
        public bool PrecisaIndustria { get; private set; }
        public bool PrecisaQuartel { get; private set; }
        public bool PrecisaRadar { get; private set; }
        public bool PrecisaAeroporto { get; private set; }
        public bool PrecisaEstaleiro { get; private set; }
        public bool PrecisaPier { get; private set; }
        public bool PrecisaPlataforma { get; private set; }
        public string UltimoResumo { get; private set; } = "construcao nao avaliada";

        public void Atualizar(
            IA_ForceSnapshot snapshot,
            IA_DeusaMapaMemoria mapa,
            IA_DeusaComida comida,
            IA_DeusaHabitacao habitacao,
            IA_DeusaEconomia economia,
            IA_DeusaPoliticaEstagio politica)
        {
            if (snapshot == null || politica == null || mapa == null)
            {
                return;
            }

            PrecisaHQ = snapshot.TotalOwnStructures <= 0;
            PrecisaCasas = habitacao != null && habitacao.PrecisaCasas;
            PrecisaFazenda = comida != null && comida.PrecisaFazenda;
            PrecisaEnergia = economia != null && economia.PrecisaEnergia;
            PrecisaIndustria = politica.PriorizarIndustria && snapshot.FactoryCount <= 0;
            PrecisaQuartel = politica.PriorizarDefesa && snapshot.BarracksCount <= 0;
            PrecisaRadar = politica.PriorizarDefesa && snapshot.RadarCount <= 0;
            PrecisaAeroporto = politica.PriorizarAereo && !snapshot.HasMilitaryAirport && mapa.TemAreaAereaValida;
            PrecisaEstaleiro = politica.PriorizarNaval && snapshot.ShipyardCount <= 0 && mapa.TemAreaNavalValida;
            PrecisaPier = politica.PriorizarNaval && snapshot.PierCount <= 0 && mapa.TemAreaNavalValida;
            PrecisaPlataforma = politica.PriorizarNaval && snapshot.PlatformCount <= 0 && mapa.TemAreaNavalValida;

            UltimoResumo = "hq=" + PrecisaHQ
                           + " | casas=" + PrecisaCasas
                           + " | farm=" + PrecisaFazenda
                           + " | energia=" + PrecisaEnergia
                           + " | quartel=" + PrecisaQuartel
                           + " | radar=" + PrecisaRadar
                           + " | aero=" + PrecisaAeroporto
                           + " | estaleiro=" + PrecisaEstaleiro
                           + " | pier=" + PrecisaPier
                           + " | plataforma=" + PrecisaPlataforma;
        }
    }
}
