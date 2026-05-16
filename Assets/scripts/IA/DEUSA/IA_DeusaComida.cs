using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaComida
    {
        public bool EmCrise { get; private set; }
        public bool PrecisaFazenda { get; private set; }
        public bool PrecisaComprar { get; private set; }
        public bool PodeVenderExcedente { get; private set; }
        public int ReservaMinima { get; private set; }
        public string UltimoResumo { get; private set; } = "comida nao avaliada";

        public void Atualizar(DadosPaisGoverno pais, DadosEconomiaPais economia, IA_DeusaPoliticaEstagio politica, bool emGuerra)
        {
            if (pais == null)
            {
                return;
            }

            float deficit = economia != null ? economia.deficitComida : 0f;
            ReservaMinima = emGuerra ? 650 : 320;
            EmCrise = pais.comida < ReservaMinima || deficit > 0.5f;
            PrecisaFazenda = EmCrise || politica.PriorizarComida;
            PrecisaComprar = pais.comida < Mathf.RoundToInt(ReservaMinima * 0.75f);
            PodeVenderExcedente = pais.comida > ReservaMinima * 2 && deficit <= 0.1f;

            UltimoResumo = "estoque=" + pais.comida
                           + " | reserva=" + ReservaMinima
                           + " | deficit=" + deficit.ToString("0.0")
                           + " | crise=" + EmCrise;
        }
    }
}
