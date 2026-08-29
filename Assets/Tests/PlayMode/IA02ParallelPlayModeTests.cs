using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class IA02ParallelPlayModeTests
{
    private const string CampaignScenePath = "Assets/Scenes/cena19).unity";

    [UnityTest]
    public IEnumerator CampaignStartsIA01AndIA02OnIndependentTeams()
    {
        if (SceneManager.GetActiveScene().path != CampaignScenePath)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(CampaignScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }
        }

        float deadline = Time.realtimeSinceStartup + 15f;
        Type ia01Type = ResolveType("Hegemonia.AI.IA01.IA01Manager");
        Type ia02Type = ResolveType("Hegemonia.AI.IA02.IA02Manager");
        UnityEngine.Object ia01 = null;
        UnityEngine.Object ia02 = null;
        Assert.That(ia01Type, Is.Not.Null);
        Assert.That(ia02Type, Is.Not.Null);
        while (Time.realtimeSinceStartup < deadline)
        {
            ia01 = FindFirst(ia01Type);
            ia02 = FindFirst(ia02Type);
            if (ia01 != null && ia02 != null)
            {
                break;
            }

            yield return null;
        }

        Assert.That(ia01, Is.Not.Null, "IA01Manager não foi encontrado na cena de campanha.");
        Assert.That(ia02, Is.Not.Null, "IA02Manager não foi encontrado na cena de campanha.");
        Assert.That(ia01, Is.Not.SameAs(ia02));
        Assert.That(FindController(ia01, 2), Is.Not.Null, "IA01 deve continuar no time 2.");
        Assert.That(FindController(ia02, 3), Is.Not.Null, "IA02 deve usar o time 3.");
        Assert.That(FindController(ia01, 3), Is.Null);
        Assert.That(FindController(ia02, 2), Is.Null);
        var controllers = GetMember(ia02, "Controllers") as System.Collections.ICollection;
        Assert.That(controllers, Is.Not.Null);
        Assert.That(controllers.Count, Is.GreaterThanOrEqualTo(1));
    }

    private static Type ResolveType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static UnityEngine.Object FindFirst(Type type)
    {
        UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].hideFlags == HideFlags.None)
            {
                return objects[i];
            }
        }

        return null;
    }

    private static object FindController(UnityEngine.Object manager, int teamId)
    {
        if (manager == null) return null;
        MethodInfo method = manager.GetType().GetMethod("FindControllerByTeamId", BindingFlags.Public | BindingFlags.Instance);
        return method != null ? method.Invoke(manager, new object[] { teamId }) : null;
    }

    private static object GetMember(UnityEngine.Object target, string memberName)
    {
        if (target == null) return null;
        PropertyInfo property = target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null) return property.GetValue(target, null);
        FieldInfo field = target.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        return field != null ? field.GetValue(target) : null;
    }
}
