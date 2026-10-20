using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CityFlagAndPopulationEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void CityFlagUsesNovaAuroraPrefabAndWaitsFifteenSeconds()
    {
        GameObject markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Imobiliario/Prefeitura/City.prefab");
        Assert.That(markerPrefab, Is.Not.Null, "O marco City precisa existir como prefab do mapa.");

        Component marker = markerPrefab.GetComponent("MarcadorTerritorio");
        Assert.That(marker, Is.Not.Null);
        Assert.That(ReadMember(marker, "construirCidadeAoIniciar"), Is.True);
        Assert.That((float)ReadMember(marker, "atrasoConstrucaoCidadeSegundos"), Is.EqualTo(15f));

        GameObject cityPrefab = ReadMember(marker, "prefabCidadeInicial") as GameObject;
        Assert.That(cityPrefab, Is.Not.Null, "O marco precisa apontar ao prefab de Nova Aurora já usado no jogo.");
        Assert.That(cityPrefab.GetComponent("CidadeComplexoUrbano"), Is.Not.Null);
    }

    [Test]
    public void FomeApagaoOuMortesBloqueiamCrescimentoERecuperacaoDeFelicidade()
    {
        Type countryType = ResolveType("DadosPaisGoverno");
        Type economyType = ResolveType("DadosEconomiaPais");
        Type populationType = ResolveType("SistemaPopulacao");
        object country = Activator.CreateInstance(countryType);
        object economy = Activator.CreateInstance(economyType);

        SetMember(country, "comida", 0);
        SetMember(economy, "comidaConsumida", 1000f);
        SetMember(economy, "deficitComida", 500f);
        AssertCrisisBlocks(populationType, country, economy, false);

        SetMember(country, "comida", 10000);
        SetMember(economy, "deficitComida", 0f);
        SetMember(economy, "deficitEnergia", 5f);
        AssertCrisisBlocks(populationType, country, economy, false);

        SetMember(economy, "deficitEnergia", 0f);
        AssertCrisisBlocks(populationType, country, economy, true);

        SetMember(country, "comida", 10000);
        SetMember(economy, "comidaConsumida", 1000f);
        SetMember(economy, "deficitComida", 500f);
        Assert.That(Invoke(populationType, "BloquearCrescimentoEmCrise", country, economy, 10, false), Is.EqualTo(10),
            "Déficit de produção não deve bloquear crescimento enquanto há reserva alimentar sustentável.");
        Assert.That(Invoke(populationType, "ImpedirGanhoFelicidadeEmCrise", country, economy, false, 60f, 61f), Is.EqualTo(61f));
    }

    private static void AssertCrisisBlocks(Type populationType, object country, object economy, bool deaths)
    {
        Assert.That(Invoke(populationType, "BloquearCrescimentoEmCrise", country, economy, 10, deaths), Is.EqualTo(0));
        Assert.That(Invoke(populationType, "ImpedirGanhoFelicidadeEmCrise", country, economy, deaths, 60f, 61f), Is.EqualTo(60f));
    }

    private static object ReadMember(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, $"Campo '{name}' ausente em {instance.GetType().Name}.");
        return field.GetValue(instance);
    }

    private static void SetMember(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, $"Campo '{name}' ausente em {instance.GetType().Name}.");
        field.SetValue(instance, value);
    }

    private static object Invoke(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, Members);
        Assert.That(method, Is.Not.Null, $"Método '{methodName}' ausente em {type.Name}.");
        return method.Invoke(null, arguments);
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
