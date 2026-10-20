#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MenuGovernoTeamRegressionEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase(1)]
    [TestCase(98)]
    public void MenuResolveTeamUsesConfiguredPlayerCountry(int configuredTeam)
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type controllerType = ResolveType("MenuGovernoNovoController");
        GameObject governmentObject = new GameObject("Teste_Governo_Team_Menu");
        governmentObject.SetActive(false);

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            FieldInfo teamField = governmentType.GetField("teamJogador", Members);
            Assert.That(teamField, Is.Not.Null, "SistemaGovernoMundial precisa expor o team configurado do jogador.");
            teamField.SetValue(government, configuredTeam);

            MethodInfo resolver = controllerType.GetMethod("ResolverTeamJogador", Members);
            Assert.That(resolver, Is.Not.Null, "O controller do Governo deve concentrar a resolução do team do jogador.");
            Assert.That((int)resolver.Invoke(null, new object[] { government }), Is.EqualTo(configuredTeam));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(governmentObject);
        }
    }

    [Test]
    public void MenuResolveTeamFallsBackWhenGovernmentIsNotInitialized()
    {
        Type controllerType = ResolveType("MenuGovernoNovoController");
        MethodInfo resolver = controllerType.GetMethod("ResolverTeamJogador", Members);

        Assert.That(resolver, Is.Not.Null);
        Assert.That((int)resolver.Invoke(null, new object[] { null }), Is.EqualTo(1));
    }

    private static Type ResolveType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
                return type;
        }

        Assert.Fail("Tipo nao encontrado: " + typeName);
        return null;
    }
}
#endif
