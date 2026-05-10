using UnityEngine;

public static class SistemaPopulacao
{
    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null || economia == null) return;

        int delta = 0;
        bool podeCrescer = economia.qualidadeVida > 70f
            && pais.comida > 80
            && economia.deficitEnergia <= 0f
            && pais.inflacao < 12f
            && !pais.emGuerra
            && !pais.sancionado
            && pais.populacao < pais.populacaoMaxima;

        bool deveCair = economia.qualidadeVida < 40f
            || economia.deficitComida > 2f
            || economia.deficitEnergia > 2f
            || pais.emGuerra
            || pais.inflacao > 22f;

        if (podeCrescer)
        {
            delta = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, pais.populacaoMaxima - pais.populacao) * 0.02f));
        }
        else if (deveCair)
        {
            delta = -Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, pais.populacao) * 0.015f));
        }

        if (delta != 0)
        {
            pais.populacao = Mathf.Clamp(pais.populacao + delta, 0, Mathf.Max(1, pais.populacaoMaxima));
        }
    }
}
