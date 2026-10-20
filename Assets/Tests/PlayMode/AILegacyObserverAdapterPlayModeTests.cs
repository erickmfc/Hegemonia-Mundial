using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AILegacyObserverAdapterPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator EmptyScanIsThrottledAndLateLegacyAiIsFoundAfterBoundedInterval()
    {
        const int teamId = 731947;
        Type adapterType = ResolveType("Hegemonia.AI.Sovereign.AILegacyObserverAdapter");
        Type legacyType = ResolveType("CerebroIA");
        Assert.That(adapterType, Is.Not.Null);
        Assert.That(legacyType, Is.Not.Null);

        object adapter = Activator.CreateInstance(adapterType, teamId, "legacy-adapter-test-" + teamId);
        MethodInfo apply = adapterType.GetMethod("Apply", Members);
        Assert.That(apply, Is.Not.Null);

        GameObject legacyObject = null;
        Behaviour legacy = null;
        try
        {
            // The first empty scan is immediate. A legacy AI spawned just after
            // it must not trigger another full-scene scan on this same frame.
            apply.Invoke(adapter, new object[] { true });

            legacyObject = new GameObject("IA legacy adapter throttle fixture");
            legacy = legacyObject.AddComponent(legacyType) as Behaviour;
            Assert.That(legacy, Is.Not.Null, "A IA legada precisa ser um Behaviour para testar seu estado enabled.");
            FieldInfo teamField = legacyType.GetField("teamID_Inimigo", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(teamField, Is.Not.Null);
            teamField.SetValue(legacy, teamId);

            legacy.enabled = false;
            legacy.enabled = true;
            apply.Invoke(adapter, new object[] { true });
            Assert.That(legacy.enabled, Is.True,
                "A varredura vazia não deve se repetir em cada frame.");

            // Keep the legacy component disabled while waiting so its Start
            // coroutine cannot issue gameplay orders during this test.
            legacy.enabled = false;
            yield return new WaitForSecondsRealtime(1.1f);

            legacy.enabled = true;
            apply.Invoke(adapter, new object[] { true });
            Assert.That(legacy.enabled, Is.False,
                "A varredura periódica deve desativar a IA legada que surgiu depois da primeira busca.");

            apply.Invoke(adapter, new object[] { false });
            Assert.That(legacy.enabled, Is.True,
                "Perder autoridade deve restaurar a IA legada desativada.");

            apply.Invoke(adapter, new object[] { true });
            Assert.That(legacy.enabled, Is.False,
                "Recuperar autoridade deve fazer uma nova busca imediata.");
        }
        finally
        {
            apply.Invoke(adapter, new object[] { false });
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            if (legacyObject != null)
            {
                UnityEngine.Object.Destroy(legacyObject);
            }
        }

        yield return null;
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
