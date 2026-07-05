using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaLogistica
    {
        public bool PrecisaPetroleoMercado { get; private set; }
        public bool PrecisaPetroleiro { get; private set; }
        public bool PrecisaTransporte { get; private set; }
        public bool PodeProjetarForca { get; private set; }
        public string UltimoResumo { get; private set; } = "logistica nao avaliada";

        public void Atualizar(DadosPaisGoverno pais, IA_ForceSnapshot snapshot, IA_DeusaMarinha marinha)
        {
            if (pais == null || snapshot == null)
            {
                return;
            }

            PrecisaPetroleoMercado = pais.petroleo < 240;
            PrecisaPetroleiro = marinha != null && marinha.PrecisaPetroleiro;
            PrecisaTransporte = marinha != null && marinha.PrecisaTransporte;
            PodeProjetarForca = snapshot.HasNavalBase || snapshot.HasMilitaryAirport || snapshot.HoverTransports > 0;

            UltimoResumo = "petroleo=" + pais.petroleo
                           + " | mercado=" + PrecisaPetroleoMercado
                           + " | petroleiro=" + PrecisaPetroleiro
                           + " | transporte=" + PrecisaTransporte
                           + " | projecao=" + PodeProjetarForca;
        }
    }
}
