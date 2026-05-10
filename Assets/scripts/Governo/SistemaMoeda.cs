using UnityEngine;

public static class SistemaMoeda
{
    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null) return;

        float producao = pais.producao / 100f;
        float estabilidade = pais.estabilidade / 100f;
        float comercio = pais.pesoComercio;
        float exportacao = economia != null ? Mathf.Clamp01(economia.exportacaoTotal / 35f) : 0.35f;
        float escassez = economia != null ? Mathf.Clamp01((economia.deficitComida + economia.deficitEnergia + economia.deficitPetroleo) / 12f) : 0f;
        float lastroOuro = Mathf.Clamp01(pais.reservaOuro / 1200f);

        float alvo = 0.45f
            + producao * 0.55f
            + estabilidade * 0.50f
            + comercio * 0.25f
            + exportacao * 0.35f
            + lastroOuro * 0.22f
            - escassez * 0.55f
            - Mathf.Clamp01(pais.inflacao / 30f) * 0.40f
            - (pais.emGuerra ? 0.30f : 0f)
            - (pais.sancionado ? 0.22f : 0f);

        float anterior = Mathf.Max(0.01f, pais.valorMoeda);
        pais.valorMoeda = Mathf.Lerp(pais.valorMoeda, Mathf.Clamp(alvo, 0.12f, 2.5f), 0.18f);
        pais.variacaoMoeda = ((pais.valorMoeda - anterior) / anterior) * 100f;
    }
}
