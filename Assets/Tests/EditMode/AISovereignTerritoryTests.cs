#if UNITY_EDITOR
using System;
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
        bool allowed = Validate("Factory", new Vector3(0f, 0f, 0f), out string reason);

        Assert.That(allowed, Is.False);
        Assert.That(reason, Is.EqualTo("territorio_do_jogador"));
    }

    [Test]
    public void SovereignBuild_IsBlockedInNeutralTerritory()
    {
        bool allowed = Validate("Factory", new Vector3(1000f, 0f, 1000f), out string reason);

        Assert.That(allowed, Is.False);
        Assert.That(reason, Is.EqualTo("territorio_nao_reivindicado"));
    }

    [Test]
    public void SovereignBuild_IsAllowedAfterTerritoryChangesToAi()
    {
        InvokeInstance(markerComponent, "ConfigureOwnership", 2, true, 300f);

        bool allowed = Validate("Factory", new Vector3(0f, 0f, 0f), out string reason);

        Assert.That(allowed, Is.True, reason);
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
}
#endif
