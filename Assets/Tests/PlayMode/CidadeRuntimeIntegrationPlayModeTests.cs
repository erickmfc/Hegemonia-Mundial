using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Type = System.Type;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CidadeRuntimeIntegrationPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject cityObject;
    private GameObject managerObject;
    private GameObject capitalObject;
    private GameObject economySystemObject;
    private Component manager;
    private Component resources;
    private object previousManagerSingleton;
    private bool restoreManagerSingleton;
    private float previousTimeScale = 1f;
    private bool timeScaleCaptured;
    private int originalPopulation;
    private int originalPopulationCapacity;

    [UnityTest]
    public IEnumerator NovaAuroraIsListedAndItsRuntimeDataStaysSynchronized()
    {
        Type managerType = ResolveType("GerenciadorDivisaoTerritorial");
        PropertyInfo managerInstance = managerType.GetProperty("Instancia", Members);
        manager = managerInstance.GetValue(null, null) as Component;
        if (manager == null)
        {
            managerObject = new GameObject("CidadeRuntimeIntegration_Gerenciador");
            manager = managerObject.AddComponent(managerType);
        }

        Type resourceType = ResolveType("GerenciadorRecursos");
        resources = resourceType.GetProperty("Instancia", Members)?.GetValue(null, null) as Component;
        if (resources != null)
        {
            originalPopulation = Read<int>(resources, "populacaoAtual");
            originalPopulationCapacity = Read<int>(resources, "populacaoMaxima");
        }

        Type buildDataType = ResolveType("DadosConstrucao");
        Object cityData = Resources.Load("Construcoes/CidadeModerna", buildDataType);
        Assert.That(cityData, Is.Not.Null, "A ficha existente da Nova Aurora precisa continuar carregável por Resources.");
        GameObject prefab = buildDataType.GetProperty("PrefabDaUnidade", Members).GetValue(cityData, null) as GameObject;
        Assert.That(prefab, Is.Not.Null, "CidadeModerna deve continuar apontando ao prefab-base Nova Aurora.");

        cityObject = Object.Instantiate(prefab, new Vector3(780000f, 0f, 780000f), Quaternion.identity);
        Component city = cityObject.GetComponent(ResolveType("CidadeComplexoUrbano"));
        Assert.That(city, Is.Not.Null);
        Set(city, "teamId", 98);
        Set(city, "nomeCidade", "Nova Aurora Runtime Teste");
        Set(city, "capacidadeHabitacional", 1500000);
        Set(city, "populacaoResidente", 123456);
        Set(city, "qualidadeVida", 67);

        Component identity = cityObject.GetComponent(ResolveType("IdentidadeUnidade"));
        if (identity != null)
        {
            Set(identity, "teamID", 98);
            FieldInfo countryName = identity.GetType().GetField("nomeDoPais", Members);
            if (countryName != null) countryName.SetValue(identity, "Nação de Teste Urbano");
        }

        yield return null;

        IList cities = Read<IList>(manager, "cidades");
        object record = cities.Cast<object>().FirstOrDefault(candidate =>
            Read<string>(candidate, "id") == cityObject.GetInstanceID().ToString());
        Assert.That(record, Is.Not.Null,
            "CidadeComplexoUrbano precisa entrar no mesmo diretório que a aba Cidades do Governo consulta.");
        Assert.That(Read<string>(record, "nome"), Is.EqualTo("Nova Aurora Runtime Teste"));
        Assert.That(Read<int>(record, "teamID"), Is.EqualTo(98));
        Assert.That(Read<int>(record, "populacaoCivil"), Is.EqualTo(123456));
        Assert.That(Read<int>(record, "capacidadeHabitacional"), Is.EqualTo(1500000));
        Assert.That(Read<Component>(record, "cidadeUrbana"), Is.EqualTo(city));
        Assert.That(Read<Component>(record, "marcador"), Is.Null,
            "O registro urbano não deve criar uma reivindicação territorial.");
        string recordId = Read<string>(record, "id");

        Set(city, "populacaoResidente", 234567);
        managerType.GetMethod("RecalcularDados", Members).Invoke(manager, null);
        Assert.That(Read<int>(record, "populacaoCivil"), Is.EqualTo(234567),
            "A aba deve ler a população atual da cidade materializada.");

        managerType.GetMethod("RenomearCidade", Members).Invoke(manager,
            new object[] { Read<string>(record, "id"), "Nova Aurora Renomeada" });
        Assert.That(Read<string>(city, "nomeCidade"), Is.EqualTo("Nova Aurora Renomeada"),
            "Renomear pelo Governo deve atualizar a entidade que aparece no mapa.");

        Object.Destroy(cityObject);
        cityObject = null;
        yield return null;

        cities = Read<IList>(manager, "cidades");
        Assert.That(cities.Cast<object>().Any(candidate => Read<string>(candidate, "id") == recordId),
            Is.False,
            "A entidade destruída não pode permanecer como cidade ativa no diretório.");
    }

    [UnityTest]
    public IEnumerator PrefeituraRuntimeRegistraCidadeAbstrataComIdentidadePersistente()
    {
        Type managerType = ResolveType("GerenciadorDivisaoTerritorial");
        PropertyInfo managerInstance = managerType.GetProperty("Instancia", Members);
        previousManagerSingleton = managerInstance.GetValue(null, null);
        managerObject = new GameObject("PrefeituraRuntime_Gerenciador");
        managerObject.SetActive(false);
        manager = managerObject.AddComponent(managerType);
        managerInstance.GetSetMethod(true).Invoke(null, new[] { manager });
        restoreManagerSingleton = true;

        previousTimeScale = Time.timeScale;
        timeScaleCaptured = true;
        Time.timeScale = 1f;
        string sourceId = "capital-playmode-" + System.Guid.NewGuid().ToString("N");
        Vector3 position = new Vector3(790000f, 0f, 790000f);
        capitalObject = new GameObject("Prefeitura_Runtime_Cidade_Abstrata");
        capitalObject.transform.position = position;

        Component identity = capitalObject.AddComponent(ResolveType("IdentidadeUnidade"));
        Set(identity, "teamID", 98);
        FieldInfo countryName = identity.GetType().GetField("nomeDoPais", Members);
        if (countryName != null) countryName.SetValue(identity, "Nação Runtime Teste");

        Component saveable = capitalObject.AddComponent(ResolveType("SaveableEntity"));
        saveable.GetType().GetProperty("UniqueId", Members).SetValue(saveable, sourceId);

        Component marker = capitalObject.AddComponent(ResolveType("MarcadorTerritorio"));
        Set(marker, "mostrarBordasNoJogo", false);
        marker.GetType().GetMethod("ConfigureOwnership", Members)
            .Invoke(marker, new object[] { 98, true, 300f });

        // Exercita Start/Invoke reais de MarcadorTerritorio, não chama
        // RegistrarCidade diretamente como o teste EditMode faz.
        yield return new WaitForSeconds(0.7f);

        IList cities = Read<IList>(manager, "cidades");
        string expectedCityId = "cidade-capital-" + sourceId;
        object abstractCity = cities.Cast<object>().FirstOrDefault(candidate =>
            Read<string>(candidate, "id") == expectedCityId);
        Assert.That(abstractCity, Is.Not.Null,
            "O registro automático do marcador de prefeitura deve criar a cidade abstrata.");
        Assert.That(Read<bool>(abstractCity, "ehCidadeNaoMaterializada"), Is.True);
        Assert.That(Read<int>(abstractCity, "teamID"), Is.EqualTo(98));
        Assert.That(Read<int>(abstractCity, "populacaoCivil"), Is.EqualTo(100000));
        Assert.That(Read<int>(abstractCity, "capacidadeHabitacional"), Is.EqualTo(2000000));
        Assert.That(Read<Vector3>(abstractCity, "localizacao"), Is.EqualTo(position));
        Assert.That(Read<string>(abstractCity, "nomePais"), Is.EqualTo("Nação Runtime Teste"));

        object territoryRecord = cities.Cast<object>().FirstOrDefault(candidate =>
            Read<Component>(candidate, "marcador") == marker);
        Assert.That(territoryRecord, Is.Not.Null,
            "A integração urbana deve preservar o registro territorial original da prefeitura.");
    }

    [UnityTest]
    public IEnumerator CrescimentoDemograficoNacionalAumentaCidadeSemCasasSeparadas()
    {
        Type managerType = ResolveType("GerenciadorDivisaoTerritorial");
        PropertyInfo managerInstance = managerType.GetProperty("Instancia", Members);
        previousManagerSingleton = managerInstance.GetValue(null, null);
        manager = previousManagerSingleton as Component;
        if (manager == null)
        {
            managerObject = new GameObject("CidadeCrescimento_Gerenciador_Isolado");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent(managerType);
            managerInstance.GetSetMethod(true).Invoke(null, new[] { manager });
            restoreManagerSingleton = true;
        }

        Type economySystemType = ResolveType("SistemaEconomiaImoveis");
        economySystemObject = new GameObject("CidadeCrescimento_Economia_Isolada");
        economySystemObject.SetActive(false);
        Component economySystem = economySystemObject.AddComponent(economySystemType);

        resources = ResolveType("GerenciadorRecursos").GetProperty("Instancia", Members)?.GetValue(null, null) as Component;
        if (resources != null)
        {
            originalPopulation = Read<int>(resources, "populacaoAtual");
            originalPopulationCapacity = Read<int>(resources, "populacaoMaxima");
        }

        const int teamId = 987654;
        const int initialPopulation = 100000;
        const int housingCapacity = 1500000;
        cityObject = new GameObject("Nova_Aurora_Crescimento_Runtime");
        Component city = cityObject.AddComponent(ResolveType("CidadeComplexoUrbano"));
        Set(city, "teamId", teamId);
        Set(city, "nomeCidade", "Nova Aurora Crescimento");
        Set(city, "capacidadeHabitacional", housingCapacity);
        Set(city, "populacaoResidente", initialPopulation);
        Set(city, "qualidadeVida", 90);
        city.GetType().GetMethod("SincronizarEstruturaEconomica", Members).Invoke(city, null);
        yield return null;

        Type structureType = ResolveType("EstruturaEconomica");
        Component structure = cityObject.GetComponent(structureType);
        Assert.That(structure, Is.Not.Null, "A cidade precisa expor sua estrutura ao sistema econômico existente.");
        economySystemType.GetMethod("ContabilizarEstrutura", Members).Invoke(economySystem, new[] { structure });
        object economy = economySystemType.GetMethod("ObterEconomia", Members).Invoke(economySystem, new object[] { teamId });
        Assert.That(economy, Is.Not.Null);
        Assert.That(Read<int>(economy, "moradiaTotal"), Is.EqualTo(housingCapacity),
            "A capacidade habitacional do próprio complexo urbano deve atender a população sem casas adicionais.");
        Assert.That(Read<int>(economy, "casas"), Is.Zero,
            "A fixture não deve depender de unidades separadas do tipo Casa.");

        Type countryType = ResolveType("DadosPaisGoverno");
        object country = System.Activator.CreateInstance(countryType);
        Set(country, "teamId", teamId);
        Set(country, "populacao", initialPopulation);
        Set(country, "populacaoCivil", initialPopulation);
        Set(country, "populacaoMaxima", housingCapacity);
        Set(country, "populacaoMilitarAtiva", 0);
        Set(country, "reservistas", 0);
        Set(country, "alistaveis", 0);
        Set(country, "felicidade", 100f);
        Set(country, "natalidade", 1.2f);
        Set(country, "comida", 1000000);

        ResolveType("SistemaPopulacao").GetMethod("Processar", Members)
            .Invoke(null, new[] { country, economy });
        int populationAfterGrowth = Read<int>(country, "populacaoCivil");
        Assert.That(populationAfterGrowth, Is.GreaterThan(initialPopulation),
            "Com atratividade, alimento e moradia disponíveis, o ciclo demográfico deve crescer sem construir casas.");

        ResolveType("CidadeComplexoUrbano").GetMethod("SincronizarPopulacaoNacional", Members)
            .Invoke(null, new object[] { teamId, populationAfterGrowth });
        Assert.That(Read<int>(city, "populacaoResidente"), Is.EqualTo(populationAfterGrowth),
            "O aumento nacional precisa alcançar os moradores da cidade materializada.");

        managerType.GetMethod("RecalcularDados", Members).Invoke(manager, null);
        IList cities = Read<IList>(manager, "cidades");
        object record = cities.Cast<object>().FirstOrDefault(candidate =>
            Read<string>(candidate, "id") == cityObject.GetInstanceID().ToString());
        Assert.That(record, Is.Not.Null, "A cidade de runtime deve estar no diretório do Governo.");
        Assert.That(Read<int>(record, "populacaoCivil"), Is.EqualTo(populationAfterGrowth));
        TestContext.WriteLine("População urbana sem casas adicionais: " + initialPopulation + " -> " + populationAfterGrowth
            + "; moradia=" + Read<int>(economy, "moradiaTotal") + "; casas separadas=" + Read<int>(economy, "casas"));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (cityObject != null) Object.Destroy(cityObject);
        if (capitalObject != null) Object.Destroy(capitalObject);
        if (cityObject != null || capitalObject != null) yield return null;

        if (resources != null)
        {
            Set(resources, "populacaoAtual", originalPopulation);
            Set(resources, "populacaoMaxima", originalPopulationCapacity);
        }

        if (economySystemObject != null) Object.Destroy(economySystemObject);

        if (restoreManagerSingleton && manager != null)
        {
            Type managerType = ResolveType("GerenciadorDivisaoTerritorial");
            Type saveCityType = ResolveType("SaveCidadeAbstrataData");
            object emptyCities = System.Activator.CreateInstance(typeof(List<>).MakeGenericType(saveCityType));
            managerType.GetMethod("RestaurarCidadesAbstratas", Members).Invoke(manager, new[] { emptyCities });
            managerType.GetProperty("Instancia", Members).GetSetMethod(true)
                .Invoke(null, new[] { previousManagerSingleton });
            restoreManagerSingleton = false;
        }

        if (managerObject != null) Object.Destroy(managerObject);
        if (timeScaleCaptured)
        {
            Time.timeScale = previousTimeScale;
            timeScaleCaptured = false;
        }
        yield return null;
    }

    private static System.Type ResolveType(string name)
    {
        System.Type type = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não carregado: " + name);
        return type;
    }

    private static void Set(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + fieldName);
        field.SetValue(target, value);
    }

    private static T Read<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + fieldName);
        return (T)field.GetValue(target);
    }
}
