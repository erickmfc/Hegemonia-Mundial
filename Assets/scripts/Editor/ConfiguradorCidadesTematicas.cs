#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilitário de Editor para preparar e validar os prefabs das cidades prontas temáticas
/// (Egito e Moderna) com o componente CidadeComplexoUrbano configurado.
/// </summary>
public static class ConfiguradorCidadesTematicas
{
    private const string PastaCityAI = "Assets/Prefabs/Imobiliario/city AI";
    private const string CaminhoPrefabEgito = PastaCityAI + "/Egyto.prefab";
    private const string CaminhoGlbModerna = PastaCityAI + "/future_city.glb";
    private const string CaminhoPrefabModerna = PastaCityAI + "/CidadeModerna.prefab";

    [MenuItem("Hegemonia/Cidades/Configurar Prefabs de Cidades Tematicas")]
    public static void ConfigurarTodosPrefabs()
    {
        ConfigurarPrefabEgito();
        ConfigurarPrefabModerna();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ConfiguradorCidadesTematicas] Prefabs de Cidades Temáticas (Egito e Moderna) configurados com sucesso!");
    }

    public static void ConfigurarPrefabEgito()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CaminhoPrefabEgito);
        if (prefabRoot == null)
        {
            Debug.LogWarning("[ConfiguradorCidadesTematicas] Não foi possível carregar " + CaminhoPrefabEgito);
            return;
        }

        try
        {
            CidadeComplexoUrbano cidade = prefabRoot.GetComponent<CidadeComplexoUrbano>();
            if (cidade == null)
            {
                cidade = prefabRoot.AddComponent<CidadeComplexoUrbano>();
            }

            cidade.tema = TemaCidade.Egito;
            cidade.nomeCidade = "Mênfis Imperial";
            cidade.capacidadeHabitacional = 1000000;
            cidade.populacaoResidente = 500000;
            cidade.proporcaoComercio = 0.5f;
            cidade.consumoEnergiaPorMorador = 0.075f;
            cidade.consumoEnergiaPorEmprego = 0.025f;
            cidade.qualidadeVida = 80;

            EstruturaEconomica estrutura = prefabRoot.GetComponent<EstruturaEconomica>();
            if (estrutura == null)
            {
                estrutura = prefabRoot.AddComponent<EstruturaEconomica>();
            }
            estrutura.tipo = TipoEstruturaEconomica.PredioResidencial;
            estrutura.capacidadePopulacional = cidade.capacidadeHabitacional;
            estrutura.populacaoAtual = cidade.populacaoResidente;
            estrutura.empregosGerados = cidade.CapacidadeComercial;
            estrutura.energiaConsumida = cidade.ConsumoEnergiaTotal;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CaminhoPrefabEgito);
            Debug.Log("[ConfiguradorCidadesTematicas] Prefab Egito atualizado: " + CaminhoPrefabEgito);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    public static void ConfigurarPrefabModerna()
    {
        GameObject modeloGlb = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoGlbModerna);
        if (modeloGlb == null)
        {
            Debug.LogWarning("[ConfiguradorCidadesTematicas] Modelo não encontrado em " + CaminhoGlbModerna);
            return;
        }

        GameObject go;
        bool existe = System.IO.File.Exists(CaminhoPrefabModerna);
        if (existe)
        {
            go = PrefabUtility.LoadPrefabContents(CaminhoPrefabModerna);
        }
        else
        {
            go = new GameObject("CidadeModerna");
            GameObject instanciaVisual = (GameObject)PrefabUtility.InstantiatePrefab(modeloGlb, go.transform);
            instanciaVisual.name = "Visual_FutureCity";
        }

        try
        {
            CidadeComplexoUrbano cidade = go.GetComponent<CidadeComplexoUrbano>();
            if (cidade == null)
            {
                cidade = go.AddComponent<CidadeComplexoUrbano>();
            }

            cidade.tema = TemaCidade.Moderna;
            cidade.nomeCidade = "Nova Metrópole Central";
            cidade.capacidadeHabitacional = 1200000;
            cidade.populacaoResidente = 600000;
            cidade.proporcaoComercio = 0.5f;
            cidade.consumoEnergiaPorMorador = 0.075f;
            cidade.consumoEnergiaPorEmprego = 0.025f;
            cidade.qualidadeVida = 85;

            EstruturaEconomica estrutura = go.GetComponent<EstruturaEconomica>();
            if (estrutura == null)
            {
                estrutura = go.AddComponent<EstruturaEconomica>();
            }
            estrutura.tipo = TipoEstruturaEconomica.PredioResidencial;
            estrutura.capacidadePopulacional = cidade.capacidadeHabitacional;
            estrutura.populacaoAtual = cidade.populacaoResidente;
            estrutura.empregosGerados = cidade.CapacidadeComercial;
            estrutura.energiaConsumida = cidade.ConsumoEnergiaTotal;

            if (existe)
            {
                PrefabUtility.SaveAsPrefabAsset(go, CaminhoPrefabModerna);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, CaminhoPrefabModerna, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(go);
            }

            Debug.Log("[ConfiguradorCidadesTematicas] Prefab Moderna configurado: " + CaminhoPrefabModerna);
        }
        finally
        {
            if (existe && go != null)
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }
    }
}
#endif
