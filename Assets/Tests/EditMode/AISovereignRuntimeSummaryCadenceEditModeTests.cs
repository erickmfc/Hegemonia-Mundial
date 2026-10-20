#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AISovereignRuntimeSummaryCadenceEditModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RuntimeSummaryRefreshesAtMostOncePerSecondAndIncludesTheBoundary()
    {
        Type controllerType = ResolveType("Hegemonia.AI.Sovereign.AISovereignController");
        Assert.That(controllerType, Is.Not.Null);

        GameObject owner = new GameObject("IA Sovereign summary cadence test");
        owner.SetActive(false);

        try
        {
            Component controller = owner.AddComponent(controllerType);
            MethodInfo shouldRefresh = controllerType.GetMethod("ShouldRefreshRuntimeSummary", PrivateInstance);
            Assert.That(shouldRefresh, Is.Not.Null);

            Assert.That(InvokeCadence(shouldRefresh, controller, 10f), Is.True,
                "O primeiro resumo deve estar disponível imediatamente.");
            Assert.That(InvokeCadence(shouldRefresh, controller, 10.99f), Is.False,
                "O resumo não deve ser reconstruído dentro do intervalo.");
            Assert.That(InvokeCadence(shouldRefresh, controller, 11f), Is.True,
                "O resumo deve atualizar quando o intervalo de um segundo for atingido.");
            Assert.That(InvokeCadence(shouldRefresh, controller, 11.5f), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static bool InvokeCadence(MethodInfo method, object target, float unscaledTime)
    {
        return (bool)method.Invoke(target, new object[] { unscaledTime });
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
#endif
