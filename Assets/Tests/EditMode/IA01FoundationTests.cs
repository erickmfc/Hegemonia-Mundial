#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class IA01FoundationTests
{
    private static readonly Type ControllerType = ResolveType("Hegemonia.AI.IA01.IA01Controller");
    private static readonly Type ManagerType = ResolveType("Hegemonia.AI.IA01.IA01Manager");
    private static readonly Type RuntimeContextType = ResolveType("Hegemonia.AI.IA01.IA01RuntimeContext");
    private static readonly Type NationIdentityType = ResolveType("Hegemonia.AI.IA01.IA01NationIdentity");
    private static readonly Type DirtyReasonType = ResolveType("Hegemonia.AI.IA01.IA01DirtyReason");

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        createdObjects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = createdObjects[i];
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void ControllersMaintainIndependentRuntimeState()
    {
        object manager;
        object alpha;
        object beta;
        BuildFoundation(out manager, out alpha, out beta);

        object alphaContext = GetMemberValue(alpha, "Context");
        object betaContext = GetMemberValue(beta, "Context");
        InvokeInstance(alphaContext, "SetResource", "food", 100f, 0f, 200f, "test");
        InvokeInstance(betaContext, "SetResource", "food", 250f, 0f, 300f, "test");
        InvokeInstance(alphaContext, "StoreMemory", "checkpoint", "alpha", string.Empty);
        InvokeInstance(betaContext, "StoreMemory", "checkpoint", "beta", string.Empty);

        Assert.That((float)InvokeInstance(alphaContext, "GetResourceAmount", "food"), Is.EqualTo(100f));
        Assert.That((float)InvokeInstance(betaContext, "GetResourceAmount", "food"), Is.EqualTo(250f));
        Assert.That(TryGetMemory(alphaContext, "checkpoint", out string alphaMemory), Is.True);
        Assert.That(alphaMemory, Is.EqualTo("alpha"));
        Assert.That(TryGetMemory(betaContext, "checkpoint", out string betaMemory), Is.True);
        Assert.That(betaMemory, Is.EqualTo("beta"));
        object worldRegistry = GetMemberValue(manager, "WorldRegistry");
        Assert.That((int)InvokeInstance(worldRegistry, "CountByNation", GetIntProperty(alpha, "NationId")), Is.EqualTo(1));
        Assert.That((int)InvokeInstance(worldRegistry, "CountByNation", GetIntProperty(beta, "NationId")), Is.EqualTo(1));
        Assert.That((int)InvokeInstance(worldRegistry, "CountByTeam", GetIntProperty(alpha, "TeamId")), Is.EqualTo(1));
        Assert.That((int)InvokeInstance(worldRegistry, "CountByTeam", GetIntProperty(beta, "TeamId")), Is.EqualTo(1));
    }

    [Test]
    public void ReapplyingTheSameIdentityDoesNotResetRandomOrDirtyState()
    {
        object context = Activator.CreateInstance(RuntimeContextType);
        object identity = CreateIdentity(10, 10, 10, "Deterministic Nation", "President", "Credit", "$", "Neutral", "normal", 12345);
        SetMemberValue(identity, "ExecutionMode", Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01ExecutionMode"), "Full"));
        SetMemberValue(identity, "NationMode", Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01NationMode"), "Normal"));
        SetMemberValue(identity, "CurrentStage", Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01NationStage"), "Initialization"));
        SetMemberValue(identity, "CurrentPosture", Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01NationPosture"), "Development"));

        InvokeInstance(context, "ApplyIdentity", identity);
        InvokeInstance(context, "ConsumeDirtyReasons");
        int first = (int)InvokeInstance(context, "AdvanceRandomInt", 0, int.MaxValue);

        InvokeInstance(context, "ApplyIdentity", identity);
        int second = (int)InvokeInstance(context, "AdvanceRandomInt", 0, int.MaxValue);

        object expected = Activator.CreateInstance(RuntimeContextType);
        InvokeInstance(expected, "ApplyIdentity", identity);
        InvokeInstance(expected, "ConsumeDirtyReasons");
        InvokeInstance(expected, "AdvanceRandomInt", 0, int.MaxValue);
        int expectedSecond = (int)InvokeInstance(expected, "AdvanceRandomInt", 0, int.MaxValue);

        Assert.That((bool)GetMemberValue(context, "IsDirty"), Is.False);
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(second, Is.EqualTo(expectedSecond));
    }

    [Test]
    public void ReapplyingAnUnchangedResourceSnapshotDoesNotCreateDirtyWork()
    {
        object context = Activator.CreateInstance(RuntimeContextType);
        object identity = CreateIdentity(7, 7, 7, null, null, null, null, "Neutral", "normal", 7);
        InvokeInstance(context, "ApplyIdentity", identity);
        InvokeInstance(context, "ConsumeDirtyReasons");

        Assert.That((bool)InvokeInstance(context, "SetResourceSnapshot", "food", 100f, 0f, 150f, "government"), Is.True);
        InvokeInstance(context, "ConsumeDirtyReasons");

        Assert.That((bool)InvokeInstance(context, "SetResourceSnapshot", "food", 100f, 0f, 150f, "government"), Is.False);
        Assert.That((bool)GetMemberValue(context, "IsDirty"), Is.False);
    }

    [Test]
    public void EventBusRoutesEventsToMatchingNationOnly()
    {
        object manager;
        object alpha;
        object beta;
        BuildFoundation(out manager, out alpha, out beta);

        int alphaDelivered = (int)InvokeInstance(alpha, "PublishEvent", "ia01.isolation", "alpha event", null, Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01EventSeverity"), "Info"));
        int betaDelivered = (int)InvokeInstance(beta, "PublishEvent", "ia01.isolation", "beta event", null, Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01EventSeverity"), "Info"));

        Assert.That(alphaDelivered, Is.EqualTo(1));
        Assert.That(betaDelivered, Is.EqualTo(1));
        Assert.That(GetIntProperty(alpha, "PendingEventCount"), Is.EqualTo(1));
        Assert.That(GetIntProperty(beta, "PendingEventCount"), Is.EqualTo(1));
        object eventBus = GetMemberValue(manager, "EventBus");
        Assert.That(GetIntProperty(eventBus, "PublishedCount"), Is.EqualTo(2));
        Assert.That(((ICollection)GetMemberValue(eventBus, "History")).Count, Is.EqualTo(2));
    }

    [Test]
    public void ManagerCapturesRestoresAndSchedulesTwoControllers()
    {
        object manager;
        object alpha;
        object beta;
        BuildFoundation(out manager, out alpha, out beta);

        object alphaContext = GetMemberValue(alpha, "Context");
        object betaContext = GetMemberValue(beta, "Context");
        InvokeInstance(alphaContext, "SetResource", "food", 111f, 0f, 200f, "before-save");
        InvokeInstance(betaContext, "SetResource", "food", 222f, 0f, 300f, "before-save");
        InvokeInstance(alphaContext, "StoreMemory", "checkpoint", "alpha-original", string.Empty);
        InvokeInstance(betaContext, "StoreMemory", "checkpoint", "beta-original", string.Empty);

        IList saves = (IList)InvokeInstance(manager, "CaptureSaveStates");
        Assert.That(saves, Has.Count.EqualTo(2));
        Assert.That(GetIntProperty(saves[0], "nationId"), Is.EqualTo(GetIntProperty(alpha, "NationId")));
        Assert.That(GetIntProperty(saves[1], "nationId"), Is.EqualTo(GetIntProperty(beta, "NationId")));

        InvokeInstance(alphaContext, "SetResource", "food", 1f, 0f, 2f, "after-save");
        InvokeInstance(betaContext, "SetResource", "food", 2f, 0f, 3f, "after-save");
        InvokeInstance(alphaContext, "StoreMemory", "checkpoint", "alpha-mutated", string.Empty);
        InvokeInstance(betaContext, "StoreMemory", "checkpoint", "beta-mutated", string.Empty);

        InvokeInstance(manager, "RestoreSaveStates", saves);

        Assert.That((float)InvokeInstance(alphaContext, "GetResourceAmount", "food"), Is.EqualTo(111f));
        Assert.That((float)InvokeInstance(betaContext, "GetResourceAmount", "food"), Is.EqualTo(222f));
        Assert.That(TryGetMemory(alphaContext, "checkpoint", out string alphaMemory), Is.True);
        Assert.That(alphaMemory, Is.EqualTo("alpha-original"));
        Assert.That(TryGetMemory(betaContext, "checkpoint", out string betaMemory), Is.True);
        Assert.That(betaMemory, Is.EqualTo("beta-original"));

        InvokeInstance(alpha, "MarkDirty", Enum.Parse(DirtyReasonType, "ManualRefresh"));
        InvokeInstance(beta, "MarkDirty", Enum.Parse(DirtyReasonType, "ManualRefresh"));

        int executed = (int)InvokeInstance(manager, "ExecuteTick", 0f, 16f, 100f);

        Assert.That(executed, Is.EqualTo(2));
        Assert.That(GetIntProperty(GetMemberValue(manager, "LastPlan"), "ScheduledCount"), Is.EqualTo(2));
        Assert.That((bool)GetMemberValue(GetMemberValue(alpha, "LastExecutionResult"), "Completed"), Is.True);
        Assert.That((bool)GetMemberValue(GetMemberValue(beta, "LastExecutionResult"), "Completed"), Is.True);
        Assert.That(GetIntProperty(GetMemberValue(manager, "Telemetry"), "SliceCount"), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void NationProfileStartsWithOpeningInfrastructureTreasury()
    {
        Type profileType = ResolveType("Hegemonia.AI.IA01.IA01NationProfile");
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Assert.That(Convert.ToInt32(GetMemberValue(profile, "InitialTreasury")), Is.EqualTo(30000));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ConstructionGovernorSettingsExistWithSafeDefaults()
    {
        Type profileType = ResolveType("Hegemonia.AI.IA01.IA01NationProfile");
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            object governor = GetMemberValue(profile, "ConstructionGovernor");
            Assert.That(governor, Is.Not.Null);
            Assert.That(Convert.ToInt32(GetMemberValue(governor, "EmergencyReserve")), Is.GreaterThan(0));
            Assert.That(Convert.ToInt32(GetMemberValue(governor, "MinimumConstructionReserve")), Is.GreaterThan(0));
            Assert.That(Convert.ToSingle(GetMemberValue(governor, "MinimumAcceptableFps")), Is.GreaterThan(0f));
            Assert.That(Convert.ToInt32(GetMemberValue(governor, "MaxCandidatesPerSlice")), Is.GreaterThan(0));
            Assert.That(Convert.ToInt32(GetMemberValue(governor, "MaxPhysicsChecksPerSlice")), Is.GreaterThan(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void OperationalStageProgressesAfterCapitalAndRecoversFromEconomicCollapse()
    {
        Type profileType = ResolveType("Hegemonia.AI.IA01.IA01NationProfile");
        Type stageType = ResolveType("Hegemonia.AI.IA01.IA01NationStage");
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            object initialization = Enum.Parse(stageType, "Initialization");
            object globalPower = Enum.Parse(stageType, "GlobalPower");

            object survival = InvokeInstance(profile, "ResolveOperationalStage", initialization, true, 1, 1000, 1000, 1000, false, false, false);
            object industrialization = InvokeInstance(profile, "ResolveOperationalStage", initialization, true, 4, 1000, 1000, 1000, false, false, false);
            object recovery = InvokeInstance(profile, "ResolveOperationalStage", globalPower, true, 8, 100, 100, 100, false, false, false);

            Assert.That(survival, Is.EqualTo(Enum.Parse(stageType, "Survival")));
            Assert.That(industrialization, Is.EqualTo(Enum.Parse(stageType, "Industrialization")));
            Assert.That(recovery, Is.EqualTo(Enum.Parse(stageType, "Stabilization")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void BuildReservationRejectsTheSameLotTwice()
    {
        Type gridType = ResolveType("Hegemonia.AI.IA01.IA01BuildReservationGrid");
        Type lotType = ResolveType("Hegemonia.AI.IA01.IA01BuildLot");
        object grid = Activator.CreateInstance(gridType);
        object lot = Activator.CreateInstance(lotType);
        SetMemberValue(lot, "Key", "10:20");

        Assert.That((bool)InvokeInstance(grid, "TryReserve", lot), Is.True);
        Assert.That((bool)InvokeInstance(grid, "TryReserve", lot), Is.False);
    }

    [Test]
    public void RealCapitalAssetHasExplicitFoundationMetadata()
    {
        ScriptableObject capital = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Prefabs/Imobiliario/Prefeitura/Prefeitura.asset");
        Assert.That(capital, Is.Not.Null);
        Assert.That(InvokeInstance(capital, "GetStableId"), Is.EqualTo("capital.prefeitura"));
        Assert.That(GetMemberValue(capital, "prefabDaUnidade"), Is.Not.Null);
        Assert.That(GetMemberValue(capital, "strategicRole"), Is.EqualTo(Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01StrategicRole"), "Capital")));

        Type capabilityType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability");
        Assert.That((bool)InvokeInstance(capital, "HasCapability", Enum.Parse(capabilityType, "Structure")), Is.True);
        Assert.That((bool)InvokeInstance(capital, "HasCapability", Enum.Parse(capabilityType, "Land")), Is.True);
    }

    [Test]
    public void CapitalCatalogResolvesByRoleAndDoesNotUseNames()
    {
        Type itemType = ResolveType("DadosConstrucao");
        Type menuType = ResolveType("MenuConstrucao");
        Type adapterType = ResolveType("Hegemonia.AI.IA01.IA01BuildCatalogAdapter");
        Type capabilityType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability");
        Type roleType = ResolveType("Hegemonia.AI.IA01.IA01StrategicRole");
        Type definitionType = ResolveType("Hegemonia.AI.IA01.IA01BuildDefinition");
        Type domainType = ResolveType("Hegemonia.AI.IA01.IA01BuildDomain");
        Type stageType = ResolveType("Hegemonia.AI.IA01.IA01NationStage");
        IList previous = (IList)GetMemberValue(null, menuType, "catalogoGlobal");
        GameObject prefab = new GameObject("generic_command_prefab");
        ScriptableObject capital = ScriptableObject.CreateInstance(itemType);
        SetMemberValue(capital, "nomeItem", "estrutura generica");
        SetMemberValue(capital, "itemId", "capital.test");
        SetMemberValue(capital, "prefabDaUnidade", prefab);
        object capabilities = Enum.Parse(capabilityType, "Structure");
        capabilities = OrEnum(capabilities, Enum.Parse(capabilityType, "Land"));
        capabilities = OrEnum(capabilities, Enum.Parse(capabilityType, "Core"));
        SetMemberValue(capital, "capacidades", capabilities);
        SetMemberValue(capital, "strategicRole", Enum.Parse(roleType, "Capital"));
        Type catalogListType = typeof(List<>).MakeGenericType(itemType);
        IList catalog = (IList)Activator.CreateInstance(catalogListType);
        catalog.Add(capital);
        SetMemberValue(null, menuType, "catalogoGlobal", catalog);

        try
        {
            object adapter = Activator.CreateInstance(adapterType, new object[] { null });
            object[] arguments = { null };
            Assert.That((bool)InvokeInstance(adapter, "TryGetCapital", arguments), Is.True);
            object definition = arguments[0];
            Assert.That(GetMemberValue(definition, "ItemId"), Is.EqualTo("capital.test"));
            Assert.That(GetMemberValue(definition, "Domain"), Is.EqualTo(Enum.Parse(domainType, "Land")));
            Assert.That(GetMemberValue(definition, "MinimumStage"), Is.EqualTo(Enum.Parse(stageType, "Initialization")));
        }
        finally
        {
            SetMemberValue(null, menuType, "catalogoGlobal", previous);
            UnityEngine.Object.DestroyImmediate(capital);
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void CatalogMissingCircuitBreakerWaitsForStateChange()
    {
        Type memoryType = ResolveType("Hegemonia.AI.IA01.IA01BuildFailureMemory");
        Type intentType = ResolveType("Hegemonia.AI.IA01.IA01IntentType");
        Type roleType = ResolveType("Hegemonia.AI.IA01.IA01StrategicRole");
        Type failureType = ResolveType("Hegemonia.AI.IA01.IA01FailureCode");
        Type reasonType = ResolveType("Hegemonia.AI.IA01.IA01IntentBlockReason");
        object memory = Activator.CreateInstance(memoryType);
        string key = (string)InvokeInstance(memory, "BuildIntentKey", Enum.Parse(intentType, "EstablishCapital"), Enum.Parse(roleType, "Capital"), "capital:101");
        string state = (string)InvokeInstance(memory, "BuildStateToken", 1, -1, false, false, 22026, 0, 0);

        InvokeInstance(memory, "Record", key, 0f, state, Enum.Parse(failureType, "NoValidCatalogItem"), Enum.Parse(reasonType, "CatalogMissing"));
        InvokeInstance(memory, "Record", key, 7f, state, Enum.Parse(failureType, "NoValidCatalogItem"), Enum.Parse(reasonType, "CatalogMissing"));
        InvokeInstance(memory, "Record", key, 22f, state, Enum.Parse(failureType, "NoValidCatalogItem"), Enum.Parse(reasonType, "CatalogMissing"));

        Assert.That((bool)InvokeInstance(memory, "CanAttempt", key, 1000f, state), Is.False);
        Assert.That((bool)InvokeInstance(memory, "CanAttempt", key, 1000f, state + "|blueprint"), Is.True);
    }

    private void BuildFoundation(out object manager, out object alpha, out object beta)
    {
        alpha = CreateController("IA01_Alpha", 101, 101, "Alpha Nation", "Alpha President");
        beta = CreateController("IA01_Beta", 202, 202, "Beta Nation", "Beta President");
        manager = CreateManager();

        InvokeInstance(manager, "RegisterController", alpha);
        InvokeInstance(manager, "RegisterController", beta);
    }

    private object CreateController(string objectName, int nationId, int teamId, string nationName, string presidentName)
    {
        GameObject go = new GameObject(objectName);
        createdObjects.Add(go);

        object controller = go.AddComponent(ControllerType);
        InvokeInstance(controller, "ConfigureIdentity", nationId, teamId, nationName, presidentName, "Credit", "$", "Neutral", "normal");
        InvokeInstance(controller, "SetMatchSeed", nationId * 100 + teamId);
        return controller;
    }

    private object CreateManager()
    {
        GameObject go = new GameObject("IA01Manager_Test");
        createdObjects.Add(go);
        return go.AddComponent(ManagerType);
    }

    private static object CreateIdentity(int instanceId, int nationId, int teamId, string nationName, string presidentName, string currencyName, string currencySymbol, string countryProfile, string difficultyProfile, int randomSeed)
    {
        object identity = Activator.CreateInstance(NationIdentityType);
        SetMemberValue(identity, "InstanceId", instanceId);
        SetMemberValue(identity, "NationId", nationId);
        SetMemberValue(identity, "TeamId", teamId);
        SetMemberValue(identity, "NationName", nationName ?? "Nation");
        SetMemberValue(identity, "PresidentName", presidentName ?? "President");
        SetMemberValue(identity, "CurrencyName", currencyName ?? "Credit");
        SetMemberValue(identity, "CurrencySymbol", currencySymbol ?? "$");
        SetMemberValue(identity, "CountryProfile", countryProfile ?? "Neutral");
        SetMemberValue(identity, "DifficultyProfile", difficultyProfile ?? "normal");
        SetMemberValue(identity, "RandomSeed", randomSeed);
        return identity;
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException("Nao foi possivel resolver o tipo " + fullName + ".");
    }

    private static object InvokeInstance(object instance, string methodName, params object[] arguments)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao invocar " + methodName + ".");
        MethodInfo method = ResolveCompatibleMethod(instance.GetType(), methodName, arguments);
        Assert.That(method, Is.Not.Null, "Nao achei o metodo " + methodName + " em " + instance.GetType().Name + ".");
        return method.Invoke(instance, arguments);
    }

    private static MethodInfo ResolveCompatibleMethod(Type type, string methodName, object[] arguments)
    {
        MethodInfo[] candidates = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < candidates.Length; i++)
        {
            MethodInfo candidate = candidates[i];
            if (candidate.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length != arguments.Length)
            {
                continue;
            }

            bool matches = true;
            for (int p = 0; p < parameters.Length; p++)
            {
                object argument = arguments[p];
                Type parameterType = parameters[p].ParameterType;
                if (argument == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        matches = false;
                        break;
                    }
                }
                else if (!parameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate;
            }
        }

        return null;
    }

    private static object GetMemberValue(object instance, string memberName)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao ler " + memberName + ".");
        Type type = instance.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (field != null)
        {
            return field.GetValue(instance);
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.That(property, Is.Not.Null, "Nao achei " + memberName + " em " + type.Name + ".");
        return property.GetValue(instance);
    }

    private static object GetMemberValue(object instance, Type declaringType, string memberName)
    {
        FieldInfo field = declaringType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Nao achei o campo " + memberName + " em " + declaringType.Name + ".");
        return field.GetValue(instance);
    }

    private static void SetMemberValue(object instance, string memberName, object value)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao definir " + memberName + ".");
        Type type = instance.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, "Nao achei o campo " + memberName + " em " + type.Name + ".");
        field.SetValue(instance, value);
    }

    private static void SetMemberValue(object instance, Type declaringType, string memberName, object value)
    {
        FieldInfo field = declaringType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Nao achei o campo " + memberName + " em " + declaringType.Name + ".");
        field.SetValue(instance, value);
    }

    private static object OrEnum(object left, object right)
    {
        Type enumType = left.GetType();
        long combined = Convert.ToInt64(left) | Convert.ToInt64(right);
        return Enum.ToObject(enumType, combined);
    }

    private static int GetIntProperty(object instance, string memberName)
    {
        return Convert.ToInt32(GetMemberValue(instance, memberName));
    }

    private static bool TryGetMemory(object context, string key, out string value)
    {
        object[] arguments = { key, null };
        bool found = (bool)InvokeInstance(context, "TryGetMemory", arguments);
        value = arguments[1] as string;
        return found;
    }
}
#endif
