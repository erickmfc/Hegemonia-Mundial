using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IA03ProductionManpowerPlayModeTests
{
    private const int TestTeamId = 2941837;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator FactoryRejectsProductionWhenCountryHasNoAvailableManpower()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type factoryType = ResolveType("Fabrica");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject factoryRoot = null;
        GameObject prefab = null;
        GameObject produced = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            if (government == null)
            {
                governmentRoot = new GameObject("IA03 manpower test government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", TestTeamId);
            SetField(testCountry, "alistaveis", 0);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);
            Assert.That(Invoke(government, "ObterPais", TestTeamId), Is.SameAs(testCountry));

            factoryRoot = new GameObject("IA03 manpower test factory");
            factoryRoot.SetActive(false);
            Component factoryIdentity = factoryRoot.AddComponent(identityType);
            SetField(factoryIdentity, "teamID", TestTeamId);
            Component factory = factoryRoot.AddComponent(factoryType);
            factoryRoot.SetActive(true);

            prefab = new GameObject("IA03 manpower test tank");
            Component prefabIdentity = prefab.AddComponent(identityType);
            SetField(prefabIdentity, "militaresConsumidos", 10);

            object spawned = factoryType.GetMethod("ProduzirUnidade", InstanceMembers)
                .Invoke(factory, new object[] { prefab });
            produced = spawned as GameObject;

            Assert.That(produced, Is.Null,
                "A fábrica criou uma unidade embora o país não tenha alistáveis nem civis disponíveis.");
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(0));
        }
        finally
        {
            if (produced != null) UnityEngine.Object.DestroyImmediate(produced);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    private static Type ResolveType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }

        Assert.Fail("Tipo não encontrado: " + name);
        return null;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, InstanceMembers);
        Assert.That(method, Is.Not.Null, "Método não encontrado: " + name);
        return method.Invoke(target, args);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        return field.GetValue(target);
    }
}
