using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Play Mode tests use reflection because this test assembly intentionally
/// does not reference the project's predefined Assembly-CSharp runtime.
/// </summary>
public sealed class IAntiAirAndModernCityPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Test]
    public void AresUnlockVariesByNationOnlyWithinDaysFourToSeven()
    {
        Type policy = ResolveType("IA_AntiAirPurchasePolicy");
        bool[] usedDays = new bool[8];
        for (int teamId = 1; teamId <= 8; teamId++)
        {
            int unlockDay = (int)InvokeStatic(policy, "GetUnlockDay", teamId);
            Assert.That(unlockDay, Is.InRange(4, 7), "team=" + teamId);
            usedDays[unlockDay] = true;
            Assert.IsFalse((bool)InvokeStatic(policy, "IsAvailable", teamId, unlockDay - 1), "team=" + teamId);
            Assert.IsTrue((bool)InvokeStatic(policy, "IsAvailable", teamId, unlockDay), "team=" + teamId);
        }
        for (int day = 4; day <= 7; day++) Assert.IsTrue(usedDays[day], "No AI is assigned unlock day " + day);
    }

    [Test]
    public void ResourceCatalogExposesAresWithRequestedRangeBurstAndCampaignCost()
    {
        Type dataType = ResolveType("DadosConstrucao");
        UnityEngine.Object data = Resources.Load("Construcoes/Ares_Ar", dataType);
        Assert.IsNotNull(data);
        Assert.AreEqual(120000000L, (long)Invoke(data, "ObterPrecoEfetivo"));
        GameObject prefab = GetPrefab(data, dataType);

        Type turretType = ResolveType("TorretaAntiaerea");
        Component ares = prefab.GetComponent(turretType);
        Assert.IsNotNull(ares);
        Assert.AreEqual(2250f, Convert.ToSingle(Read(ares, "alcanceArea")), 0.01f);
        Assert.AreEqual(2, Convert.ToInt32(Read(ares, "quantidadeDeDisparo")));
        Assert.AreEqual(2f, Convert.ToSingle(Read(ares, "tirosPorSegundo")), 0.01f);
        Assert.AreEqual(3f, Convert.ToSingle(Read(ares, "tempoPausaRajada")), 0.01f);
        float burstDuration = Convert.ToInt32(Read(ares, "quantidadeDeDisparo"))
            / Convert.ToSingle(Read(ares, "tirosPorSegundo"))
            + Convert.ToSingle(Read(ares, "tempoPausaRajada"));
        Assert.AreEqual(4f, burstDuration, 0.01f);

        Type policy = ResolveType("IA_AntiAirPurchasePolicy");
        Assert.AreEqual(ReadStatic(policy, "AresAmmoId"), Read(ares, "idMunicao"));
        Assert.AreEqual(220000L, Convert.ToInt64(ReadStatic(policy, "AresAmmoUnitPrice")));

        Type prices = ResolveType("ValoresDefinitivosHegemonia");
        MethodInfo tryPrice = prices.GetMethod("TryObterPreco", Members, null,
            new[] { typeof(string), typeof(string), typeof(long).MakeByRefType() }, null);
        Assert.IsNotNull(tryPrice);
        object[] args = { "ares ar", "Ares Ar", 0L };
        Assert.IsTrue((bool)tryPrice.Invoke(null, args));
        Assert.AreEqual(120000000L, (long)args[2]);
    }

    [Test]
    public void ModernCityIsRegisteredAsTheResidentialMegacityForBothAIFlows()
    {
        Type dataType = ResolveType("DadosConstrucao");
        UnityEngine.Object data = Resources.Load("Construcoes/CidadeModerna", dataType);
        Assert.IsNotNull(data);
        Assert.AreEqual("urbana.cidade moderna", Invoke(data, "GetStableId"));
        Assert.AreEqual(6000000000L, (long)Invoke(data, "ObterPrecoEfetivo"));
        GameObject prefab = GetPrefab(data, dataType);

        Type cityPolicy = ResolveType("IA_CityExpansionPolicy");
        MethodInfo isCityConstruction = cityPolicy.GetMethod("IsCityConstruction", Members);
        Assert.IsNotNull(isCityConstruction);
        Assert.IsTrue((bool)isCityConstruction.Invoke(null, new[] { data }));

        Type cityType = ResolveType("CidadeComplexoUrbano");
        Component city = prefab.GetComponentInChildren(cityType, true);
        Assert.IsNotNull(city);
        Assert.AreEqual(2000000, Convert.ToInt32(Read(city, "capacidadeHabitacional")));
        Assert.AreEqual(100000, Convert.ToInt32(Read(city, "populacaoResidente")));

        Component identity = prefab.GetComponent(ResolveType("IdentidadeUnidade"));
        Assert.IsNotNull(identity);
        Assert.AreEqual(1, Convert.ToInt32(Read(identity, "teamID")));
        Assert.AreEqual("Estrutura", Read(identity, "tipoUnidade").ToString());

        Component damage = prefab.GetComponent(ResolveType("SistemaDeDanos"));
        Assert.IsNotNull(damage);
        Assert.IsTrue(Convert.ToBoolean(Read(damage, "ehEstrutura")));

        BoxCollider cityVolume = prefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(cityVolume);
        Assert.IsTrue(cityVolume.isTrigger, "A caixa raiz delimita a cidade; os MeshColliders mantêm a colisao física.");
        Assert.That(cityVolume.size.x, Is.GreaterThan(20f));
        Assert.That(cityVolume.size.y, Is.GreaterThan(30f), "A caixa precisa cobrir o comprimento todo da cidade.");
        Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true).Length, Is.GreaterThan(0));
    }

    private static GameObject GetPrefab(UnityEngine.Object data, Type dataType)
    {
        MethodInfo method = dataType.GetMethod("TryGetPrefab", Members);
        Assert.IsNotNull(method);
        object[] args = { null };
        Assert.IsTrue((bool)method.Invoke(data, args));
        GameObject prefab = args[0] as GameObject;
        Assert.IsNotNull(prefab);
        return prefab;
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        if (type == null) type = Type.GetType(name + ", Assembly-CSharp", false);
        Assert.That(type, Is.Not.Null, "Tipo nao carregado: " + name);
        return type;
    }

    private static object InvokeStatic(Type type, string name, params object[] args)
    {
        MethodInfo method = type.GetMethod(name, Members);
        Assert.IsNotNull(method, type.FullName + "." + name);
        return method.Invoke(null, args);
    }

    private static object Invoke(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, Members);
        Assert.IsNotNull(method, target.GetType().FullName + "." + name);
        return method.Invoke(target, null);
    }

    private static object Read(object target, string name)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null) return property.GetValue(target, null);
        FieldInfo field = type.GetField(name, Members);
        Assert.IsNotNull(field, type.FullName + "." + name);
        return field.GetValue(target);
    }

    private static object ReadStatic(Type type, string name)
    {
        FieldInfo field = type.GetField(name, Members);
        Assert.IsNotNull(field, type.FullName + "." + name);
        return field.GetValue(null);
    }
}
