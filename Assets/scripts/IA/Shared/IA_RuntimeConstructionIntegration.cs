using Hegemonia.AI.BrainMaster;
using UnityEngine;

/// <summary>
/// Aplica perfis econômicos opcionais depois que uma estrutura já foi criada.
/// Não substitui o prefab nem o fluxo de construção; apenas completa dados
/// novos quando a ficha ou o nome da estrutura os identifica.
/// </summary>
public static class IA_RuntimeConstructionIntegration
{
    public static void Apply(GameObject built, DadosConstrucao data)
    {
        if (built == null) return;

        if (data != null && data.nivelIndustriaAlimentos > 0)
        {
            FoodIndustry food = built.GetComponent<FoodIndustry>();
            if (food == null) food = built.AddComponent<FoodIndustry>();
            food.Configure(
                data.nivelIndustriaAlimentos,
                data.producaoComidaPorDia,
                data.manutencaoPorDia,
                data.empregosGeradosPerfil);
        }

        bool nuclear = data != null && data.usinaNuclear;
        nuclear |= built.GetComponent<Usina>() != null && built.GetComponent<Usina>().tipoUsina == TipoUsina.Nuclear;
        if (nuclear)
        {
            EstruturaEconomica economic = built.GetComponent<EstruturaEconomica>();
            if (economic == null) economic = built.AddComponent<EstruturaEconomica>();
            economic.tipo = TipoEstruturaEconomica.UsinaNuclear;
            economic.energiaProduzida = data != null && data.energiaNuclear > 0f ? data.energiaNuclear : 30000f;
            economic.dinheiroGerado = data != null && data.manutencaoPorDia > 0f ? -data.manutencaoPorDia : -450f;
            economic.combustivelConsumido = 8f;
            economic.empregosGerados = 2050;
            economic.militaresNecessarios = 400;
            economic.InferirTeamId();
        }

        CidadeComplexoUrbano city = built.GetComponentInChildren<CidadeComplexoUrbano>(true);
        if (city != null && built.GetComponent<CidadeSaudePopulacional>() == null)
        {
            built.AddComponent<CidadeSaudePopulacional>();
        }
    }

    public static void ApplyByIdentity(GameObject built)
    {
        if (built == null) return;
        string text = IA_Text.Normalize(built.name);
        if (text.Contains("nuclear") || text.Contains("food industry") || text.Contains("industria alimentos"))
        {
            Apply(built, null);
        }
    }
}
