using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaHabitacao
    {
        public float PressaoPopulacional { get; private set; }
        public bool PrecisaCasas { get; private set; }
        public bool Urgente { get; private set; }
        public string UltimoResumo { get; private set; } = "habitacao nao avaliada";

        public void Atualizar(DadosPaisGoverno pais, DadosEconomiaPais economia)
        {
            if (pais == null)
            {
                return;
            }

            int populacao = economia != null ? Mathf.Max(pais.populacao, economia.populacaoTotal) : pais.populacao;
            int moradia = economia != null ? Mathf.Max(pais.populacaoMaxima, economia.moradiaTotal) : pais.populacaoMaxima;
            PressaoPopulacional = moradia <= 0 ? 1f : Mathf.Clamp01(populacao / (float)moradia);
            PrecisaCasas = PressaoPopulacional >= 0.80f;
            Urgente = PressaoPopulacional >= 0.95f;

            UltimoResumo = "pop=" + populacao
                           + "/" + moradia
                           + " | pressao=" + PressaoPopulacional.ToString("0.00")
                           + " | urgente=" + Urgente;
        }
    }
}
