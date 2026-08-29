#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class IA02ParallelEditModeTests
{
    private const string CampaignScenePath = "Assets/Scenes/cena19).unity";
    private const string ProfilePath = "Assets/IA02/Profiles/IA02NationProfile.asset";
    private const string PlanPath = "Assets/IA02/BuildPlans/IA02BuildPlan.asset";

    [Test]
    public void IA02TypesAreInTheirOwnNamespaceAndDoNotAliasIA01()
    {
        Type ia01Controller = ResolveType("Hegemonia.AI.IA01.IA01Controller");
        Type ia02Controller = ResolveType("Hegemonia.AI.IA02.IA02Controller");
        Type ia01Manager = ResolveType("Hegemonia.AI.IA01.IA01Manager");
        Type ia02Manager = ResolveType("Hegemonia.AI.IA02.IA02Manager");

        Assert.That(ia01Controller, Is.Not.Null);
        Assert.That(ia02Controller, Is.Not.Null);
        Assert.That(ia01Manager, Is.Not.Null);
        Assert.That(ia02Manager, Is.Not.Null);
        Assert.That(ia02Controller, Is.Not.SameAs(ia01Controller));
        Assert.That(ia02Manager, Is.Not.SameAs(ia01Manager));
        Assert.That(ia02Controller.Namespace, Is.EqualTo("Hegemonia.AI.IA02"));
        Assert.That(ia02Manager.Namespace, Is.EqualTo("Hegemonia.AI.IA02"));
    }

    [Test]
    public void IA02UsesIndependentIdentifiersAndTeamThree()
    {
        Type managerType = ResolveType("Hegemonia.AI.IA02.IA02Manager");
        var globalTaskId = managerType.GetField("GlobalTaskId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.That(globalTaskId, Is.Not.Null);
        Assert.That(globalTaskId.GetValue(null), Is.EqualTo("ia/ia02/manager"));

        ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
        Assert.That(profile, Is.Not.Null, "O perfil IA02 deve existir em " + ProfilePath);
        Assert.That(GetInt(profile, "nationIdHint"), Is.EqualTo(3));
        Assert.That(GetInt(profile, "teamIdHint"), Is.EqualTo(3));
        Assert.That(GetString(profile, "profileKey"), Does.StartWith("ia02."));
        Assert.That(GetString(profile, "difficultyProfile"), Is.EqualTo("aggressive_expansion"));
        Assert.That(GetInt(profile, "personality"), Is.EqualTo(7));
    }

    [Test]
    public void IA02PlanAndSceneAssetsAreSeparateFromIA01()
    {
        ScriptableObject plan = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PlanPath);
        Assert.That(plan, Is.Not.Null, "O plano IA02 deve existir em " + PlanPath);
        Assert.That(GetString(plan, "planId"), Is.EqualTo("ia02.plan.expansionista"));

        string sceneText = File.ReadAllText(CampaignScenePath);
        Assert.That(sceneText, Does.Contain("IA02 Runtime - Uniao Carmesim"));
        Assert.That(sceneText, Does.Contain("Hegemonia.AI.IA02.IA02Manager"));
        Assert.That(sceneText, Does.Contain("Hegemonia.AI.IA02.IA02Controller"));
        Assert.That(sceneText, Does.Contain("Hegemonia.AI.IA02.IA02CityLayout"));
        Assert.That(sceneText, Does.Contain("teamId: 3"));
    }

    [Test]
    public void SaveGameDeclaresSeparateIA01AndIA02StateCollections()
    {
        Type saveType = ResolveType("DadosDoJogo");
        Type ia01StateType = ResolveType("Hegemonia.AI.IA01.SaveIA01NationState");
        Type ia02StateType = ResolveType("Hegemonia.AI.IA02.SaveIA02NationState");

        Assert.That(saveType, Is.Not.Null);
        Assert.That(ia01StateType, Is.Not.Null);
        Assert.That(ia02StateType, Is.Not.Null);
        FieldInfo ia01States = saveType.GetField("estadosIA01");
        FieldInfo ia02States = saveType.GetField("estadosIA02");
        Assert.That(ia01States, Is.Not.Null);
        Assert.That(ia02States, Is.Not.Null);
        Assert.That(ia01States.FieldType, Is.Not.EqualTo(ia02States.FieldType));

        Type managerType = ResolveType("Hegemonia.AI.IA02.IA02Manager");
        Assert.That(managerType.GetMethod("CaptureSaveStates"), Is.Not.Null);
        Assert.That(managerType.GetMethod("RestoreSaveStates"), Is.Not.Null);
    }

    [Test]
    public void IA02SourceDoesNotContainIA01ControllerOrManagerReferences()
    {
        string sourceRoot = Path.Combine(Application.dataPath, "scripts", "IA02");
        string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(files.Length, Is.GreaterThan(0));

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain("IA01Controller"), file);
            Assert.That(text, Does.Not.Contain("IA01Manager"), file);
            Assert.That(text, Does.Not.Contain("ia/ia01/manager"), file);
        }
    }

    private static Type ResolveType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static string GetString(UnityEngine.Object asset, string propertyName)
    {
        return new SerializedObject(asset).FindProperty(propertyName).stringValue;
    }

    private static int GetInt(UnityEngine.Object asset, string propertyName)
    {
        return new SerializedObject(asset).FindProperty(propertyName).intValue;
    }
}
#endif
