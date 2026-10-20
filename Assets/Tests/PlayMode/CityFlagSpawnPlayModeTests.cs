using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CityFlagSpawnPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject markerObject;
    private GameObject terrainObject;
    private TerrainData terrainData;
    private Component spawnedCity;
    private readonly List<GameObject> sequenceParents = new List<GameObject>();
    private readonly List<GameObject> sequenceCities = new List<GameObject>();
    private float previousTimeScale;
    private int testTeamId;

    [UnityTest]
    public IEnumerator CityMarkerBuildsNovaAuroraAtItsGroundedPositionWithTenThousandResidents()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        testTeamId = UnityEngine.Random.Range(700000, 1900000000);

        terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(100f, 10f, 100f) };
        terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "CityFlagSpawn_TestTerrain";

        Type dataType = ResolveType("DadosConstrucao");
        UnityEngine.Object cityData = Resources.Load("Construcoes/CidadeModerna", dataType);
        Assert.That(cityData, Is.Not.Null, "A validação usa o cadastro de construção já existente.");
        GameObject cityPrefab = cityData.GetType().GetProperty("PrefabDaUnidade", Members)
            .GetValue(cityData, null) as GameObject;
        Assert.That(cityPrefab, Is.Not.Null);

        markerObject = new GameObject("CityFlagSpawn_Runtime");
        markerObject.transform.position = new Vector3(20f, 8f, 20f);
        Component identity = markerObject.AddComponent(ResolveType("IdentidadeUnidade"));
        Set(identity, "teamID", testTeamId);
        FieldInfo countryName = identity.GetType().GetField("nomeDoPais", Members);
        if (countryName != null) countryName.SetValue(identity, "Nação City Teste");

        Component marker = markerObject.AddComponent(ResolveType("MarcadorTerritorio"));
        Set(marker, "construirCidadeAoIniciar", true);
        Set(marker, "atrasoConstrucaoCidadeSegundos", 0.1f);
        Set(marker, "prefabCidadeInicial", cityPrefab);

        yield return new WaitForSeconds(0.35f);

        spawnedCity = UnityEngine.Object.FindObjectsOfType(ResolveType("CidadeComplexoUrbano"))
            .OfType<Component>()
            .FirstOrDefault(candidate => Read<int>(candidate, "teamId") == testTeamId);
        Assert.That(spawnedCity, Is.Not.Null, "O marco deve materializar a cidade após o atraso.");
        Assert.That(Read<string>(spawnedCity, "nomeCidade"), Is.EqualTo("Nova Aurora"));
        Assert.That(Read<int>(spawnedCity, "populacaoResidente"), Is.EqualTo(10000));

        GameObject cityObject = spawnedCity.gameObject;
        Assert.That(cityObject.transform.position.x, Is.EqualTo(20f).Within(0.01f));
        Assert.That(cityObject.transform.position.z, Is.EqualTo(20f).Within(0.01f));
        Assert.That(cityObject.GetComponentsInChildren<Renderer>(true).Min(renderer => renderer.bounds.min.y),
            Is.EqualTo(0f).Within(0.15f), "A base visual da cidade deve tocar o terreno, não flutuar.");
        Assert.That(markerObject == null, Is.True, "O marcador temporário deve ser consumido após construir.");

        Component cityIdentity = cityObject.GetComponent(ResolveType("IdentidadeUnidade"));
        Assert.That(cityIdentity, Is.Not.Null);
        Assert.That(Read<int>(cityIdentity, "teamID"), Is.EqualTo(testTeamId));
    }

    [UnityTest]
    public IEnumerator CityFlagsActivateSequentiallyOnlyAfterCurrentCityReachesCapacity()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        testTeamId = UnityEngine.Random.Range(700000, 1900000000);

        terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(100f, 10f, 100f) };
        terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "CityFlagSequence_TestTerrain";

        Type dataType = ResolveType("DadosConstrucao");
        UnityEngine.Object cityData = Resources.Load("Construcoes/CidadeModerna", dataType);
        Assert.That(cityData, Is.Not.Null, "A validação usa o cadastro de construção já existente.");
        GameObject cityPrefab = cityData.GetType().GetProperty("PrefabDaUnidade", Members)
            .GetValue(cityData, null) as GameObject;
        Assert.That(cityPrefab, Is.Not.Null);

        GameObject parent = new GameObject("CityFlagSequence_Runtime");
        sequenceParents.Add(parent);
        GameObject firstMarker = CreateCityMarker(parent.transform, "City_01", new Vector3(20f, 8f, 20f), cityPrefab);
        GameObject secondMarker = CreateCityMarker(parent.transform, "City_02", new Vector3(60f, 8f, 20f), cityPrefab);
        GameObject thirdMarker = CreateCityMarker(parent.transform, "City_03", new Vector3(90f, 8f, 20f), cityPrefab);

        yield return new WaitForSeconds(0.5f);

        List<Component> cities = FindCitiesForTeam(testTeamId);
        Assert.That(cities, Has.Count.EqualTo(1), "Só o primeiro marcador deve construir antes de a primeira cidade encher.");
        sequenceCities.Add(cities[0].gameObject);
        Assert.That(firstMarker == null, Is.True, "O marcador consumido deve desaparecer após a construção.");
        Assert.That(secondMarker, Is.Not.Null, "O marcador seguinte deve permanecer aguardando sua vez.");
        Assert.That(thirdMarker, Is.Not.Null, "As bandeiras posteriores também devem permanecer em espera.");

        Component firstCity = cities[0];
        Set(firstCity, "populacaoResidente", Read<int>(firstCity, "capacidadeHabitacional"));
        firstCity.GetType().GetMethod("SincronizarEstruturaEconomica", Members).Invoke(firstCity, null);

        yield return new WaitForSeconds(0.5f);

        cities = FindCitiesForTeam(testTeamId);
        Assert.That(cities, Has.Count.EqualTo(2), "A segunda cidade só deve surgir depois que a primeira atingir a capacidade.");
        Component secondCity = cities.FirstOrDefault(city => city.transform.position.x > 50f);
        Assert.That(secondCity, Is.Not.Null, "A segunda cidade deve usar a posição do segundo marcador.");
        sequenceCities.Add(secondCity.gameObject);
        Assert.That(secondMarker == null, Is.True, "O segundo marcador deve ser consumido quando sua cidade for criada.");
        Assert.That(thirdMarker, Is.Not.Null, "A terceira bandeira ainda deve esperar a segunda cidade crescer.");

        Set(secondCity, "populacaoResidente", Read<int>(secondCity, "capacidadeHabitacional"));
        secondCity.GetType().GetMethod("SincronizarEstruturaEconomica", Members).Invoke(secondCity, null);

        yield return new WaitForSeconds(0.5f);

        cities = FindCitiesForTeam(testTeamId);
        Assert.That(cities, Has.Count.EqualTo(3), "A terceira cidade deve surgir apenas quando a segunda atingir a própria capacidade.");
        Component thirdCity = cities.FirstOrDefault(city => city.transform.position.x > 80f);
        Assert.That(thirdCity, Is.Not.Null, "A terceira cidade deve usar a posição do terceiro marcador.");
        sequenceCities.Add(thirdCity.gameObject);
        Assert.That(thirdMarker == null, Is.True, "O terceiro marcador deve ser consumido quando sua cidade for criada.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (markerObject != null) UnityEngine.Object.Destroy(markerObject);
        if (spawnedCity != null) UnityEngine.Object.Destroy(spawnedCity.gameObject);
        for (int i = 0; i < sequenceParents.Count; i++)
            if (sequenceParents[i] != null) UnityEngine.Object.Destroy(sequenceParents[i]);
        for (int i = 0; i < sequenceCities.Count; i++)
            if (sequenceCities[i] != null) UnityEngine.Object.Destroy(sequenceCities[i]);
        if (terrainObject != null) UnityEngine.Object.Destroy(terrainObject);
        if (terrainData != null) UnityEngine.Object.Destroy(terrainData);
        sequenceParents.Clear();
        sequenceCities.Clear();
        Time.timeScale = previousTimeScale;
        yield return null;
    }

    private GameObject CreateCityMarker(Transform parent, string markerName, Vector3 position, GameObject cityPrefab)
    {
        GameObject markerObject = new GameObject(markerName);
        markerObject.transform.SetParent(parent, false);
        markerObject.transform.position = position;
        Component identity = markerObject.AddComponent(ResolveType("IdentidadeUnidade"));
        Set(identity, "teamID", testTeamId);
        FieldInfo countryName = identity.GetType().GetField("nomeDoPais", Members);
        if (countryName != null) countryName.SetValue(identity, "Nação Sequencial Teste");

        Component marker = markerObject.AddComponent(ResolveType("MarcadorTerritorio"));
        Set(marker, "construirCidadeAoIniciar", true);
        Set(marker, "atrasoConstrucaoCidadeSegundos", 0.1f);
        Set(marker, "prefabCidadeInicial", cityPrefab);
        return markerObject;
    }

    private static List<Component> FindCitiesForTeam(int teamId)
    {
        return UnityEngine.Object.FindObjectsOfType(ResolveType("CidadeComplexoUrbano"))
            .OfType<Component>()
            .Where(city => Read<int>(city, "teamId") == teamId)
            .ToList();
    }

    private static void Set(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, $"Campo '{name}' ausente em {instance.GetType().Name}.");
        field.SetValue(instance, value);
    }

    private static T Read<T>(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, $"Campo '{name}' ausente em {instance.GetType().Name}.");
        return (T)field.GetValue(instance);
    }

    private static Type ResolveType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null) return type;
        }

        Assert.Fail($"Tipo '{typeName}' não foi carregado.");
        return null;
    }
}
