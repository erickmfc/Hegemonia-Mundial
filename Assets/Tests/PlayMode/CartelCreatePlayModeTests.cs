using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CartelCreatePlayModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private GameObject packageRoot;
    private bool packageRootWasActive;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        packageRoot = GameObject.Find("CartelManualCreates_Pais01");
        if (packageRoot != null)
        {
            packageRootWasActive = packageRoot.activeSelf;
            packageRoot.SetActive(false);
        }
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
        objects.Clear();
        if (packageRoot != null) packageRoot.SetActive(packageRootWasActive);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayMode_CreateRegistersAndRespectsRadiusAndOccupancy()
    {
        Component create = NewCreate("PlayMode_CoastalMeeting", "CartelCoastalMeetingCreate");
        Set(create, "Radius", 10f);
        Set(create, "MaxOccupants", 1);
        yield return null;

        Assert.That(Call<bool>(create, "Contains", new Vector3(8f, 0f, 0f)), Is.True);
        Assert.That(Call<bool>(create, "Contains", new Vector3(11f, 0f, 0f)), Is.False);
        Assert.That(Call<bool>(create, "TryReserve", objects[0]), Is.True);
    }

    [UnityTest]
    public IEnumerator PlayMode_ControllerBuildsBaseInsideConfiguredArea()
    {
        Component candidate = NewCreate("Pais01_BaseCreate_01", "CartelBaseCreate");
        Set(candidate, "CountryId", "Pais01");
        Component area = NewCreate("Pais01_BaseAreaCreate_01", "CartelBaseAreaCreate");
        Set(area, "CountryId", "Pais01");
        Set(area, "LinkId", "Base_01");
        Component controller = NewController("CartelController_PlayMode", "Pais01");
        yield return null;
        Call(controller, "Initialize");
        yield return null;

        Assert.That(Get(controller, "State").ToString(), Is.EqualTo("Operational"));
        Assert.That(Count(Get(controller, "Bases")), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PlayMode_ControllerChoosesCandidateFartherFromCity()
    {
        Component near = NewCreate("Pais02_BaseCreate_01", "CartelBaseCreate");
        Set(near, "CountryId", "Pais02");
        near.transform.position = Vector3.zero;
        Component far = NewCreate("Pais02_BaseCreate_02", "CartelBaseCreate");
        Set(far, "CountryId", "Pais02");
        far.transform.position = new Vector3(100f, 0f, 0f);
        Component nearArea = NewCreate("Pais02_BaseAreaCreate_01", "CartelBaseAreaCreate");
        Set(nearArea, "CountryId", "Pais02");
        Set(nearArea, "LinkId", "Base_01");
        nearArea.transform.position = Vector3.zero;
        Component farArea = NewCreate("Pais02_BaseAreaCreate_02", "CartelBaseAreaCreate");
        Set(farArea, "CountryId", "Pais02");
        Set(farArea, "LinkId", "Base_02");
        farArea.transform.position = new Vector3(100f, 0f, 0f);
        Component city = NewCreate("Pais02_CityReference_01", "CityReference");
        Set(city, "CountryId", "Pais02");
        city.transform.position = Vector3.zero;
        Component controller = NewController("CartelController_Pais02_PlayMode", "Pais02");
        yield return null;
        Call(controller, "Initialize");
        yield return null;

        object runtime = First(Get(controller, "Bases"));
        Component chosen = Get(runtime, "Candidate") as Component;
        Assert.That(chosen, Is.Not.Null);
        Assert.That(chosen.gameObject.name, Is.EqualTo("Pais02_BaseCreate_02"));
    }

    [UnityTest]
    public IEnumerator PlayMode_CreateTypeExposesAllManualCategories()
    {
        yield return null;
        Type enumType = FindType("Hegemonia.Cartel.CartelCreateType");
        Assert.That(Enum.GetValues(enumType).Length, Is.GreaterThanOrEqualTo(35));
        Assert.That(Enum.IsDefined(enumType, "CartelMaritimeHideCreate"), Is.True);
        Assert.That(Enum.IsDefined(enumType, "CartelTerrestrialHideCreate"), Is.True);
        Assert.That(Enum.IsDefined(enumType, "OilPlatformExitCreate"), Is.True);
    }

    private Component NewCreate(string name, string typeName)
    {
        GameObject go = NewObject(name);
        Component create = go.AddComponent(FindType("Hegemonia.Cartel.CartelManualCreate"));
        Set(create, "Type", Enum.Parse(FindType("Hegemonia.Cartel.CartelCreateType"), typeName));
        return create;
    }

    private Component NewController(string name, string country)
    {
        GameObject go = NewObject(name);
        Component controller = go.AddComponent(FindType("Hegemonia.Cartel.CartelAIController"));
        Set(controller, "InitialCountryId", country);
        Set(controller, "StartAutomatically", false);
        return controller;
    }

    private GameObject NewObject(string name)
    {
        GameObject go = new GameObject(name);
        objects.Add(go);
        return go;
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        Assert.Fail("Tipo nao encontrado: " + fullName);
        return null;
    }

    private static object Get(object target, string name)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null) return field.GetValue(target);
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target, null);
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null) { field.SetValue(target, value); return; }
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value, null);
    }

    private static void Call(object target, string name, params object[] args)
    {
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    }

    private static T Call<T>(object target, string name, params object[] args)
    {
        return (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    }

    private static int Count(object value)
    {
        return value is ICollection collection ? collection.Count : ((IEnumerable)value).Cast<object>().Count();
    }

    private static object First(object value)
    {
        foreach (object item in (IEnumerable)value) return item;
        return null;
    }
}
