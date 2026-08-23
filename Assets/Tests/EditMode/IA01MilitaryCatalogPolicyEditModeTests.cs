using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA01MilitaryCatalogPolicyEditModeTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        created.Clear();
    }

    [Test]
    public void SavedIA01RejectsUnknownMilitaryPrefabButPreservesOtherAiTeams()
    {
        Assert.That(
            InvokeSavedEntityPolicy(2, "Veiculo", "Sr71(Clone)", true),
            Is.False);
        Assert.That(
            InvokeSavedEntityPolicy(2, "Veiculo", "Tank_C1(Clone)", true),
            Is.True);
        Assert.That(
            InvokeSavedEntityPolicy(9, "Veiculo", "Sr71(Clone)", false),
            Is.True);
    }

    [Test]
    public void ExplicitConfiguredCatalogOverridesFallbackAllowlist()
    {
        Type dadosConstrucaoType = ResolveType("DadosConstrucao");
        ScriptableObject configured = ScriptableObject.CreateInstance(dadosConstrucaoType);
        SetMember(configured, "NomeItem", "Unit X");
        GameObject prefab = new GameObject("Unit_X_Prefab");
        SetMember(configured, "PrefabDaUnidade", prefab);
        created.Add(configured);
        created.Add(prefab);

        Array allowlist = Array.CreateInstance(dadosConstrucaoType, 1);
        allowlist.SetValue(configured, 0);
        Type policyType = ResolveType("Hegemonia.AI.IA01.IA01MilitaryCatalogPolicy");
        MethodInfo isAllowed = policyType.GetMethod("IsAllowed", BindingFlags.Public | BindingFlags.Static);
        Assert.That(isAllowed, Is.Not.Null);
        Assert.That(isAllowed.Invoke(null, new object[] { configured, allowlist }), Is.EqualTo(true));
        Assert.That(isAllowed.Invoke(null, new object[] { null, allowlist }), Is.EqualTo(false));

        MethodInfo savedPolicy = policyType.GetMethod("IsAllowedSavedEntity", BindingFlags.Public | BindingFlags.Static);
        Type unitType = ResolveType("TipoUnidade");
        object unit = Enum.Parse(unitType, "Infantaria");
        Assert.That(savedPolicy.Invoke(null, new object[] { 2, unit, "Unit_X_Prefab(Clone)", true, allowlist }), Is.EqualTo(true));
        Assert.That(savedPolicy.Invoke(null, new object[] { 2, unit, "Soldier(Clone)", true, allowlist }), Is.EqualTo(false));
    }

    private static bool InvokeSavedEntityPolicy(int teamId, string typeName, string prefabKey, bool isIa01Entity)
    {
            Type policyType = ResolveType("Hegemonia.AI.IA01.IA01MilitaryCatalogPolicy");
            Type unitType = ResolveType("TipoUnidade");
            MethodInfo method = policyType.GetMethod("IsAllowedSavedEntity", BindingFlags.Public | BindingFlags.Static);
            object type = Enum.Parse(unitType, typeName);
        return (bool)method.Invoke(null, new object[] { teamId, type, prefabKey, isIa01Entity, null });
    }

    private static Type ResolveType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não encontrado: " + fullName);
        return type;
    }

    private static void SetMember(object target, string name, object value)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Membro não encontrado: " + name);
        field.SetValue(target, value);
    }
}
