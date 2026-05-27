using UnityEngine;

public static class SistemaPopulacao
{
    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null || economia == null) return;

        // Consumo de comida (GDD: 1/h a cada 100 civis; 2/h a cada 100 militares)
        float necessidadeComida = (pais.populacaoCivil / 100f * 1f) + (pais.populacaoMilitarAtiva / 100f * 2f);
        economia.deficitComida = Mathf.Max(0f, necessidadeComida - economia.comidaProduzida);

        // Subtrai do estoque a necessidade total (ou até onde tiver estoque)
        int consumoReal = Mathf.Min(pais.comida, Mathf.CeilToInt(necessidadeComida));
        pais.comida -= consumoReal;
        
        // Colapso por fome se o déficit persistir além do estoque
        if (economia.deficitComida > 0 && pais.comida <= 0)
        {
            pais.felicidade = Mathf.Clamp(pais.felicidade - 0.5f, 0f, 100f);
            pais.mortalidade = Mathf.Clamp(pais.mortalidade + 0.1f, 1f, 5f);
        }
        else
        {
            pais.mortalidade = Mathf.Clamp(pais.mortalidade - 0.05f, 1f, 5f);
        }

        int delta = 0;
        bool podeCrescer = economia.qualidadeVida > 70f
            && pais.comida > 80
            && economia.deficitEnergia <= 0f
            && pais.inflacao < 12f
            && !pais.emGuerra
            && !pais.sancionado
            && pais.populacao < pais.populacaoMaxima
            && pais.felicidade > 60f;

        bool deveCair = economia.qualidadeVida < 40f
            || economia.deficitComida > 2f
            || economia.deficitEnergia > 2f
            || pais.emGuerra
            || pais.inflacao > 22f
            || pais.felicidade < 40f;

        if (podeCrescer)
        {
            // Crescimento afeta a população civil
            delta = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, pais.populacaoMaxima - pais.populacao) * 0.02f * pais.natalidade));
        }
        else if (deveCair)
        {
            // Queda afeta a população civil
            delta = -Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, pais.populacao) * 0.015f * pais.mortalidade));
        }

        if (delta != 0)
        {
            // O crescimento / decrescimento altera a base civil, que compõe a total
            pais.populacaoCivil = Mathf.Clamp(pais.populacaoCivil + delta, 0, Mathf.Max(1, pais.populacaoMaxima));
            
            // Recalcula total com base na civil e militar para evitar furos
            pais.populacao = pais.populacaoCivil + pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis;
            pais.populacao = Mathf.Clamp(pais.populacao, 0, Mathf.Max(1, pais.populacaoMaxima));
        }
    }
}
