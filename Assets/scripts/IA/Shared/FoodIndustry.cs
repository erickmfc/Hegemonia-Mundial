using UnityEngine;

/// <summary>Perfil runtime dos três níveis de indústria de alimentos.</summary>
[DisallowMultipleComponent]
public sealed class FoodIndustry : MonoBehaviour
{
    [Range(1, 3)] public int nivel = 1;
    public float producaoComidaPorDia = 1200f;
    public float manutencaoPorDia = 25f;
    public int empregosGerados = 60;

    public void Configure(int level, float production, float maintenance, int jobs)
    {
        nivel = Mathf.Clamp(level, 1, 3);
        producaoComidaPorDia = production > 0f ? production : NivelPadrao(nivel);
        manutencaoPorDia = maintenance > 0f ? maintenance : (20f * nivel);
        empregosGerados = jobs > 0 ? jobs : (nivel * 60);
        ApplyToEconomy();
    }

    private void Awake()
    {
        MigrarPerfilNivel1Legado();
        ApplyToEconomy();
    }

    private void OnEnable()
    {
        MigrarPerfilNivel1Legado();
        ApplyToEconomy();
    }

    private void OnValidate()
    {
        MigrarPerfilNivel1Legado();
        ApplyToEconomy();
    }

    private void MigrarPerfilNivel1Legado()
    {
        // Componentes criados antes dos três níveis usavam 120/40 como
        // valores iniciais. Migra somente esse par exato para não sobrescrever
        // uma configuração manual diferente.
        if (nivel == 1 && Mathf.Approximately(producaoComidaPorDia, 120f) && empregosGerados == 40)
        {
            producaoComidaPorDia = NivelPadrao(1);
            empregosGerados = 60;
        }
    }

    private void ApplyToEconomy()
    {
        EstruturaEconomica economic = GetComponent<EstruturaEconomica>();
        if (economic == null) economic = gameObject.AddComponent<EstruturaEconomica>();
        economic.tipo = nivel <= 1
            ? TipoEstruturaEconomica.IndustriaAlimentosNivel1
            : nivel == 2 ? TipoEstruturaEconomica.IndustriaAlimentosNivel2 : TipoEstruturaEconomica.IndustriaAlimentosNivel3;
        economic.comidaProduzida = Mathf.Max(0f, producaoComidaPorDia);
        economic.manutencaoAlimentosPorDia = Mathf.Max(0f, manutencaoPorDia);
        economic.empregosGerados = Mathf.Max(0, empregosGerados);
        economic.energiaConsumida = Mathf.Max(15f, nivel * 45f);
        economic.eficiencia = Mathf.Clamp01(economic.eficiencia <= 0f ? 1f : economic.eficiencia);
        economic.InferirTeamId();
    }

    private static float NivelPadrao(int level)
    {
        switch (Mathf.Clamp(level, 1, 3))
        {
            case 2: return 4500f;
            case 3: return 12000f;
            default: return 1200f;
        }
    }
}
