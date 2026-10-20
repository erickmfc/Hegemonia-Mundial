#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AISovereignTerritoryTests
{
    private static readonly Type BackendType = ResolveType("Hegemonia.AI.Sovereign.AISovereignBackend");
    private static readonly Type RoleType = ResolveType("Hegemonia.AI.Sovereign.AISovereignCatalogRole");
    private static readonly Type ConstructionType = ResolveType("DadosConstrucao");
    private static readonly Type TerritoryManagerType = ResolveType("GerenteDeTerritorio");
    private static readonly Type MarkerType = ResolveType("MarcadorTerritorio");
    private static readonly Type IdentityType = ResolveType("IdentidadeUnidade");

    private GameObject territoryManagerObject;
    private GameObject markerObject;
    private Component markerComponent;
    private ScriptableObject constructionData;
    private GameObject constructionPrefab;

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        territoryManagerObject = new GameObject("Test_GerenteDeTerritorio");
        territoryManagerObject.AddComponent(TerritoryManagerType);

        markerObject = new GameObject("Test_MarcadorTerritorio");
        // Keep this fixture outside the political polygon coverage so it
        // exercises the legacy marker fallback rather than the authoritative map.
        markerObject.transform.position = new Vector3(10000f, 0f, 10000f);
        markerObject.AddComponent(IdentityType);
        markerComponent = markerObject.AddComponent(MarkerType);
        InvokeInstance(markerComponent, "ConfigureOwnership", 1, true, 300f);
        InvokeInstance(GetStaticMemberValue(TerritoryManagerType, "Instancia"), "RegistrarMarcador", markerComponent);

        constructionPrefab = new GameObject("Test_Estrutura");
        constructionData = ScriptableObject.CreateInstance(ConstructionType);
        SetMemberValue(constructionData, "nomeItem", "Fabrica de teste");
        SetMemberValue(constructionData, "prefabDaUnidade", constructionPrefab);
    }

    [TearDown]
    public void TearDown()
    {
        if (constructionData != null) UnityEngine.Object.DestroyImmediate(constructionData);
        if (constructionPrefab != null) UnityEngine.Object.DestroyImmediate(constructionPrefab);
        if (markerObject != null) UnityEngine.Object.DestroyImmediate(markerObject);
        if (territoryManagerObject != null) UnityEngine.Object.DestroyImmediate(territoryManagerObject);
    }

    [Test]
    public void SovereignBuild_IsBlockedInsidePlayerTerritory()
    {
        bool allowed = Validate("Factory", new Vector3(10000f, 0f, 10000f), out string reason);

        Assert.That(allowed, Is.False);
        Assert.That(reason, Is.EqualTo("territorio_do_jogador"));
    }

    [Test]
    public void SovereignBuild_IsBlockedInNeutralTerritory()
    {
        bool allowed = Validate("Factory", new Vector3(20000f, 0f, 20000f), out string reason);

        Assert.That(allowed, Is.False);
        Assert.That(reason, Is.EqualTo("territorio_nao_reivindicado"));
    }

    [Test]
    public void SovereignBuild_IsAllowedAfterTerritoryChangesToAi()
    {
        InvokeInstance(markerComponent, "ConfigureOwnership", 2, true, 300f);

        bool allowed = Validate("Factory", new Vector3(10000f, 0f, 10000f), out string reason);

        Assert.That(allowed, Is.True, reason);
    }

    [Test]
    public void SovereignEmergencyProduction_RequiresPoweredFactory()
    {
        GameObject prefab = new GameObject("Test_NoFactoryTechnologyUnit");
        try
        {
            Component identity = prefab.AddComponent(IdentityType);
            SetMemberValue(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Estrutura"));
            SetMemberValue(constructionData, "nomeItem", "Console de tecnologia de teste");
            SetMemberValue(constructionData, "prefabDaUnidade", prefab);
            Type categoryType = ConstructionType.GetNestedType("CategoriaItem", BindingFlags.Public);
            SetMemberValue(constructionData, "categoria", Enum.Parse(categoryType, "Tecnologia"));

            object backend = Activator.CreateInstance(BackendType, 2);
            object role = Enum.Parse(RoleType, "Core");
            Type envelopeType = ResolveType("Hegemonia.AI.Sovereign.AISovereignEnvelope");
            object envelope = Enum.Parse(envelopeType, "SemiTrapaca");
            MethodInfo method = BackendType.GetMethod(
                "TryProduceResolved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { role, constructionData, envelope, null, string.Empty };
            bool produced = (bool)method.Invoke(backend, arguments);

            Assert.That(produced, Is.False);
            Assert.That(arguments[4] as string, Is.EqualTo("fabrica_sem_energia_ou_indisponivel"));
            Assert.That(GameObject.Find(prefab.name + "(Clone)"), Is.Null,
                "A dificuldade SemiTrapaca não deve materializar produção terrestre sem fábrica operacional.");
        }
        finally
        {
            GameObject clone = GameObject.Find(prefab.name + "(Clone)");
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void SovereignProductionInfrastructureExceptionRefundsTreasuryAndReleasesReservation()
    {
        const int teamId = 987655;
        const long startingBalance = 500000L;
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type menuType = ResolveType("MenuConstrucao");
        Type registryType = ResolveType("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo setGovernment = governmentInstance.GetSetMethod(true);
        object previousGovernment = governmentInstance.GetValue(null, null);
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousCatalog = catalogField.GetValue(null);
        GameObject governmentRoot = null;
        GameObject aircraftPrefab = null;
        ScriptableObject aircraftItem = null;
        string leakedOrderId = string.Empty;

        try
        {
            governmentRoot = new GameObject("Sovereign production exception treasury fixture");
            governmentRoot.SetActive(false);
            Component government = governmentRoot.AddComponent(governmentType);
            object country = Activator.CreateInstance(countryType);
            SetMemberValue(country, "teamId", teamId);
            SetMemberValue(country, "saldo", startingBalance);
            ((IList)GetMemberValue(government, "paises")).Add(country);
            setGovernment.Invoke(null, new[] { government });

            aircraftPrefab = new GameObject("Sovereign_Exception_Fighter");
            aircraftItem = ScriptableObject.CreateInstance(ConstructionType);
            SetMemberValue(aircraftItem, "nomeItem", aircraftPrefab.name);
            SetMemberValue(aircraftItem, "prefabDaUnidade", aircraftPrefab);
            SetMemberValue(aircraftItem, "categoria", Enum.Parse(
                ConstructionType.GetNestedType("CategoriaItem", BindingFlags.Public), "Aeronautica"));
            SetMemberValue(aircraftItem, "capacidades", Enum.Parse(
                ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability"), "Aircraft, FighterAircraft"));
            SetMemberValue(aircraftItem, "precoDefinitivo", 1200L);

            IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ConstructionType));
            catalog.Add(aircraftItem);
            catalogField.SetValue(null, catalog);

            object backend = Activator.CreateInstance(BackendType, new object[] { teamId });
            FieldInfo airportBuffer = BackendType.GetField("_airports", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(airportBuffer, Is.Not.Null);
            airportBuffer.SetValue(backend, null);
            Assert.That(airportBuffer.GetValue(backend), Is.Null, "A falha precisa ser injetada no buffer de aeroportos do backend.");

            object fighterRole = Enum.Parse(RoleType, "Fighter");
            object envelope = Enum.Parse(ResolveType("Hegemonia.AI.Sovereign.AISovereignEnvelope"), "Justa");
            MethodInfo tryProduce = BackendType.GetMethod("TryProduce", BindingFlags.Instance | BindingFlags.Public);
            Exception productionException = null;
            object[] produceArguments = { fighterRole, envelope, null, null };
            bool accepted = false;
            try
            {
                accepted = (bool)tryProduce.Invoke(backend, produceArguments);
            }
            catch (TargetInvocationException exception)
            {
                productionException = exception.InnerException ?? exception;
            }

            leakedOrderId = (string)registryType.GetMethod("FindActiveOrder", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { teamId, "Fighter", "Fighter" });
            Assert.That(productionException, Is.TypeOf<NullReferenceException>(),
                "A lista de aeroportos nula deve injetar uma falha antes de qualquer spawn. accepted="
                + accepted + " reason=" + Convert.ToString(produceArguments[3]));
            Assert.That(GetMemberValue(country, "saldo"), Is.EqualTo(startingBalance),
                "A transação do tesouro deve reembolsar o custo da falha injetada.");
            Assert.That(leakedOrderId, Is.Empty,
                "Uma falha de infraestrutura não pode deixar reserva ativa na IA soberana.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(leakedOrderId))
            {
                registryType.GetMethod("Release", BindingFlags.Static | BindingFlags.Public)
                    .Invoke(null, new object[] { leakedOrderId, Time.time });
            }
            catalogField.SetValue(null, previousCatalog);
            setGovernment.Invoke(null, new[] { previousGovernment });
            if (aircraftItem != null) UnityEngine.Object.DestroyImmediate(aircraftItem);
            if (aircraftPrefab != null) UnityEngine.Object.DestroyImmediate(aircraftPrefab);
            if (governmentRoot != null) UnityEngine.Object.DestroyImmediate(governmentRoot);
        }
    }

    [Test]
    public void NationalTreasuryBuild_ChargesSuccessAndRefundsRejectedBuild()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type treasuryType = ResolveType("Hegemonia.AI.Shared.IA_NationalTreasury");
        PropertyInfo instanceProperty = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentObject = new GameObject("National treasury build transaction test");
        governmentObject.SetActive(false);
        Component government = governmentObject.AddComponent(governmentType);
        object country = Activator.CreateInstance(countryType);
        const int testTeamId = 987654;
        const long startingBalance = 1000000000L;
        SetMemberValue(country, "teamId", testTeamId);
        SetMemberValue(country, "saldo", startingBalance);
        IList countries = (IList)governmentType.GetField(
            "paises", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(government);
        countries.Add(country);
        setGovernment.Invoke(null, new[] { government });

        try
        {
            SetMemberValue(constructionData, "precoDefinitivo", 123L);
            MethodInfo effectivePrice = ConstructionType.GetMethod("ObterPrecoEfetivo");
            long cost = (long)effectivePrice.Invoke(constructionData, null);
            Assert.That(cost, Is.GreaterThan(0L));
            Assert.That(instanceProperty.GetValue(null, null), Is.SameAs(government),
                "A transação precisa usar a instância de governo isolada desta fixture.");
            Assert.That(governmentType.GetMethod("ObterPais").Invoke(government, new object[] { testTeamId }),
                Is.SameAs(country), "A fixture precisa registrar o país antes de cobrar.");
            long availableBalance = (long)governmentType.GetMethod("ObterSaldoDisponivelParaOperacao")
                .Invoke(government, new object[] { testTeamId });
            Assert.That(availableBalance, Is.GreaterThanOrEqualTo(cost),
                "O saldo inicial deve cobrir o preço efetivo tabelado antes da simulação de estorno.");

            MethodInfo buildTransaction = treasuryType.GetMethod(
                "TentarConstruirComTesouroNacional",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(buildTransaction, Is.Not.Null);

            bool rejectedBuildRan = false;
            bool rejected = (bool)buildTransaction.Invoke(null, new object[]
            {
                testTeamId,
                constructionData,
                (Func<bool>)(() =>
                {
                    rejectedBuildRan = true;
                    return false;
                })
            });

            Assert.That(rejected, Is.False);
            Assert.That(rejectedBuildRan, Is.True);
            Assert.That(GetMemberValue(country, "saldo"), Is.EqualTo(startingBalance),
                "A obra recusada deve devolver o custo ao mesmo tesouro nacional.");

            SetMemberValue(country, "saldo", cost - 1L);
            bool insufficientFundsOperationRan = false;
            bool insufficientFundsAccepted = (bool)buildTransaction.Invoke(null, new object[]
            {
                testTeamId,
                constructionData,
                (Func<bool>)(() =>
                {
                    insufficientFundsOperationRan = true;
                    return true;
                })
            });

            Assert.That(insufficientFundsAccepted, Is.False,
                "A construção deve falhar se o saldo nacional disponível não cobre o preço efetivo.");
            Assert.That(insufficientFundsOperationRan, Is.False,
                "A estrutura não pode ser materializada antes da autorização do tesouro.");
            Assert.That(GetMemberValue(country, "saldo"), Is.EqualTo(cost - 1L));

            SetMemberValue(country, "saldo", startingBalance);

            bool built = (bool)buildTransaction.Invoke(null, new object[]
            {
                testTeamId,
                constructionData,
                (Func<bool>)(() => true)
            });

            Assert.That(built, Is.True);
            Assert.That(GetMemberValue(country, "saldo"), Is.EqualTo(startingBalance - cost),
                "Uma obra aceita deve cobrar o preço monetário efetivo uma única vez.");
        }
        finally
        {
            countries.Remove(country);
            setGovernment.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(governmentObject);
        }
    }

    private bool Validate(string roleName, Vector3 position, out string reason)
    {
        object backend = Activator.CreateInstance(BackendType, 2);
        object role = Enum.Parse(RoleType, roleName);
        MethodInfo method = BackendType.GetMethod(
            "TryValidateBuildTerritory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { constructionData, role, position, null };
        bool allowed = (bool)method.Invoke(backend, arguments);
        reason = arguments[3] as string;
        return allowed;
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }

        Assert.Fail("Tipo nao encontrado: " + fullName);
        return null;
    }

    private static object GetStaticMemberValue(Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField(memberName, flags);
        if (field != null) return field.GetValue(null);

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null) return property.GetValue(null, null);

        Assert.Fail("Membro estatico nao encontrado: " + type.FullName + "." + memberName);
        return null;
    }

    private static object InvokeInstance(object target, string methodName, params object[] arguments)
    {
        Assert.That(target, Is.Not.Null);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = target.GetType().GetMethod(methodName, flags);
        Assert.That(method, Is.Not.Null, target.GetType().FullName + "." + methodName);
        return method.Invoke(target, arguments);
    }

    private static void SetMemberValue(object target, string memberName, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return;
        }

        Assert.Fail("Membro nao encontrado: " + type.FullName + "." + memberName);
    }

    private static object GetMemberValue(object target, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, flags);
        if (field != null) return field.GetValue(target);

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null) return property.GetValue(target, null);

        Assert.Fail("Membro nao encontrado: " + type.FullName + "." + memberName);
        return null;
    }
}

public sealed class AISovereignBootstrapperTests
{
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase(true, ValidationScenePath, true)]
    [TestCase(false, ValidationScenePath, false)]
    [TestCase(true, "Assets/Scenes/GlobalMapRTS.unity", false)]
    [TestCase(true, "", false)]
    public void ValidationSceneSuppressesSovereignBootstrapOnlyInEditor(
        bool isEditor,
        string scenePath,
        bool expected)
    {
        Type bootstrapperType = ResolveType("Hegemonia.AI.Sovereign.AISovereignBootstrapper");
        MethodInfo method = bootstrapperType.GetMethod("ShouldSkipBootstrapForScene", StaticMembers);
        Assert.That(method, Is.Not.Null);

        bool actual = (bool)method.Invoke(null, new object[] { isEditor, scenePath });

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }

        Assert.Fail("Tipo nao encontrado: " + fullName);
        return null;
    }
}
#endif
