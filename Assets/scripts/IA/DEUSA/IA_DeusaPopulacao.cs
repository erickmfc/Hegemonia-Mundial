using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaPopulacao
    {
        public int PopulacaoAtual { get; private set; }
        public int PopulacaoMaxima { get; private set; }
        public int CapacidadeMilitarDisponivel { get; private set; }
        public bool PodeRecrutarMilitar { get; private set; }
        public string UltimoResumo { get; private set; } = "populacao nao avaliada";

        public void Atualizar(DadosPaisGoverno pais, DadosEconomiaPais economia, DeusaEstagio estagio)
        {
            if (pais == null)
            {
                return;
            }

            PopulacaoAtual = economia != null ? Mathf.Max(pais.populacao, economia.populacaoTotal) : pais.populacao;
            PopulacaoMaxima = economia != null ? Mathf.Max(pais.populacaoMaxima, economia.moradiaTotal) : pais.populacaoMaxima;

            int reservaCivil = estagio >= DeusaEstagio.GuerraTotal ? 8 : 14;
            CapacidadeMilitarDisponivel = Mathf.Max(0, PopulacaoAtual - reservaCivil);
            PodeRecrutarMilitar = CapacidadeMilitarDisponivel > 0;

            UltimoResumo = "popAtual=" + PopulacaoAtual
                           + " | popMax=" + PopulacaoMaxima
                           + " | reservaMilitar=" + CapacidadeMilitarDisponivel
                           + " | recrutar=" + PodeRecrutarMilitar;
        }
    }
}
