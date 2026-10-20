using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RTSPersistentFormationPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private Component service;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        service = FindOrCreateService();
        yield return null;
        ResetService();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        ResetService();
        for (int i = 0; i < createdObjects.Count; i++)
        {
            if (createdObjects[i] != null && createdObjects[i] != service.gameObject)
                UnityEngine.Object.Destroy(createdObjects[i]);
        }
        createdObjects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator RestoreAndCaptureKeepMemberIdsOffsetsAndDropDuplicates()
    {
        yield return null;

        Type saveDataType = ResolveType("Hegemonia.RTS.RTSPersistentFormationSaveData");
        Type memberDataType = ResolveType("Hegemonia.RTS.RTSPersistentFormationMemberData");
        Type domainType = ResolveType("Hegemonia.RTS.RTSFormationDomain");
        Type saveableType = ResolveType("SaveableEntity");
        Type unitType = ResolveType("ControleUnidade");
        var unitObjects = new List<GameObject>();
        var saveablesById = new Dictionary<string, object>(StringComparer.Ordinal);

        for (int i = 0; i < 3; i++)
        {
            GameObject unitObject = new GameObject("FormationTestUnit_" + i);
            createdObjects.Add(unitObject);
            unitObject.AddComponent(unitType);
            Component saveable = unitObject.AddComponent(saveableType);
            string id = "formation-test-" + i + "-" + Guid.NewGuid().ToString("N");
            saveableType.GetProperty("UniqueId", Members).SetValue(saveable, id);
            unitObjects.Add(unitObject);
            saveablesById.Add(id, saveable);
        }

        IList members = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(memberDataType));
        string leaderId = ReadId(saveableType, unitObjects[0]);
        string followerId = ReadId(saveableType, unitObjects[1]);
        string missingId = "not-in-scene-" + Guid.NewGuid().ToString("N");
        members.Add(CreateMember(memberDataType, leaderId, Vector3.zero));
        members.Add(CreateMember(memberDataType, followerId, new Vector3(-90f, 0f, -58.5f)));
        members.Add(CreateMember(memberDataType, followerId, new Vector3(50f, 0f, 25f)));
        members.Add(CreateMember(memberDataType, missingId, new Vector3(0f, 0f, -100f)));

        object savedFormation = Activator.CreateInstance(saveDataType);
        Set(savedFormation, "formationId", "formation-test-" + Guid.NewGuid().ToString("N"));
        Set(savedFormation, "teamId", 2);
        Set(savedFormation, "domain", Enum.Parse(domainType, "Naval"));
        Set(savedFormation, "leaderUnitId", leaderId);
        Set(savedFormation, "members", members);

        IList formations = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(saveDataType));
        formations.Add(savedFormation);
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), saveableType);
        object typedDictionary = Activator.CreateInstance(dictionaryType);
        MethodInfo add = dictionaryType.GetMethod("Add");
        foreach (KeyValuePair<string, object> pair in saveablesById)
            add.Invoke(typedDictionary, new[] { (object)pair.Key, pair.Value });

        Invoke(service, "RestaurarEstado", formations, typedDictionary);
        Assert.That((int)service.GetType().GetProperty("FormationCount", Members).GetValue(service), Is.EqualTo(1));

        Type sceneType = typeof(Scene);
        MethodInfo capture = service.GetType().GetMethod("CapturarEstado", Members, null, new[] { sceneType }, null);
        IList captured = (IList)capture.Invoke(service, new object[] { SceneManager.GetActiveScene() });
        Assert.That(captured.Count, Is.EqualTo(1));
        IList capturedMembers = (IList)Read(captured[0], "members");
        Assert.That(capturedMembers.Count, Is.EqualTo(2), "IDs duplicados e membros ausentes não devem ser restaurados.");
        Assert.That(Read(captured[0], "leaderUnitId"), Is.EqualTo(leaderId));
        Assert.That((Vector3)Read(capturedMembers[1], "localOffset"), Is.EqualTo(new Vector3(-90f, 0f, -58.5f)));

        string json = JsonUtility.ToJson(captured[0]);
        object roundTrip = JsonUtility.FromJson(json, saveDataType);
        IList roundTripMembers = (IList)Read(roundTrip, "members");
        Assert.That(roundTripMembers.Count, Is.EqualTo(2));
        Assert.That(Read(roundTripMembers[1], "unitId"), Is.EqualTo(followerId));
    }

    [UnityTest]
    public IEnumerator GroupMoveRegistrationCreatesANavalFormationFromExistingUnitOrders()
    {
        yield return null;

        Type unitType = ResolveType("ControleUnidade");
        Type saveableType = ResolveType("SaveableEntity");
        Type navalIdentityType = ResolveType("IdentidadeNaval");
        var units = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));

        for (int i = 0; i < 2; i++)
        {
            GameObject unitObject = new GameObject("FormationNavalTestUnit_" + i);
            createdObjects.Add(unitObject);
            Component unit = unitObject.AddComponent(unitType);
            unitObject.AddComponent(navalIdentityType);
            Component saveable = unitObject.AddComponent(saveableType);
            saveableType.GetProperty("UniqueId", Members).SetValue(
                saveable,
                "formation-naval-" + i + "-" + Guid.NewGuid().ToString("N"));

            SetUnitMoveSnapshot(unitType, unit, new Vector3(100f + i * 100f, 0f, 200f));
            units.Add(unit);
        }

        Invoke(service, "RegistrarOrdemDeGrupo", units);
        Assert.That((int)service.GetType().GetProperty("FormationCount", Members).GetValue(service), Is.EqualTo(1));

        Type saveDataType = ResolveType("Hegemonia.RTS.RTSPersistentFormationSaveData");
        Type sceneType = typeof(Scene);
        MethodInfo capture = service.GetType().GetMethod("CapturarEstado", Members, null, new[] { sceneType }, null);
        IList captured = (IList)capture.Invoke(service, new object[] { SceneManager.GetActiveScene() });
        Assert.That(captured.Count, Is.EqualTo(1));
        Assert.That(Read(captured[0], "domain").ToString(), Is.EqualTo("Naval"));
        IList members = (IList)Read(captured[0], "members");
        Assert.That(members.Count, Is.EqualTo(2));
        Assert.That((Vector3)Read(members[1], "localOffset"), Is.Not.EqualTo(Vector3.zero));
        Assert.That(captured[0].GetType(), Is.EqualTo(saveDataType));
    }

    private void ResetService()
    {
        if (service == null) return;
        Type dataType = ResolveType("Hegemonia.RTS.RTSPersistentFormationSaveData");
        IList empty = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dataType));
        Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), ResolveType("SaveableEntity"));
        object emptyMap = Activator.CreateInstance(dictionaryType);
        Invoke(service, "RestaurarEstado", empty, emptyMap);
    }

    private static Component FindOrCreateService()
    {
        Type serviceType = ResolveType("Hegemonia.RTS.RTSPersistentFormationService");
        Component existing = UnityEngine.Object.FindFirstObjectByType(serviceType, FindObjectsInactive.Include) as Component;
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject("PersistentFormationPlayModeTestService");
        return created.AddComponent(serviceType);
    }

    private static object CreateMember(Type memberType, string unitId, Vector3 offset)
    {
        object data = Activator.CreateInstance(memberType);
        Set(data, "unitId", unitId);
        Set(data, "localOffset", offset);
        return data;
    }

    private static void SetUnitMoveSnapshot(Type unitType, Component unit, Vector3 destination)
    {
        unitType.GetField("ordemControleAtual", Members).SetValue(
            unit,
            Enum.Parse(ResolveType("OrdemControleUnidade"), "Movendo"));
        unitType.GetField("possuiDestinoOrdenado", Members).SetValue(unit, true);
        unitType.GetField("ultimoDestinoOrdenado", Members).SetValue(unit, destination);
    }

    private static string ReadId(Type saveableType, GameObject unit)
    {
        Component saveable = unit.GetComponent(saveableType);
        return (string)saveableType.GetProperty("UniqueId", Members).GetValue(saveable);
    }

    private static void Invoke(Component target, string methodName, params object[] arguments)
    {
        target.GetType().GetMethod(methodName, Members).Invoke(target, arguments);
    }

    private static object Read(object target, string name)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, Members);
        if (field != null) return field.GetValue(target);
        return type.GetProperty(name, Members).GetValue(target);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Members).SetValue(target, value);
    }

    private static Type ResolveType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null) return type;
        }
        Assert.Fail("Tipo não carregado: " + name);
        return null;
    }
}
