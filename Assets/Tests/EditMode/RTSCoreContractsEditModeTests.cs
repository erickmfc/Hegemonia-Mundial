using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Este assembly de testes nao referencia Assembly-CSharp diretamente. Os testes
// existentes usam reflexao pelo mesmo motivo; manter o contrato assim permite
// que a suite continue compilando com o asmdef atual do projeto.
public sealed class RTSCoreContractsEditModeTests
{
    private GameObject sessionHost;
    private GameObject visibilityHost;
    private GameObject targetHost;

    [TearDown]
    public void TearDown()
    {
        InvokeStaticIfPresent("Hegemonia.RTS.RTSInputBindings", "Reset");
        DestroyIfPresent(ref targetHost);
        DestroyIfPresent(ref visibilityHost);
        DestroyIfPresent(ref sessionHost);
    }

    [Test]
    public void ResourceCostClampsNegativeValues()
    {
        Type costType = RequireType("Hegemonia.RTS.RTSResourceCost");
        object cost = Activator.CreateInstance(costType, new object[] { -10L, -2, -3, -4, -5 });

        Assert.That(GetField<long>(cost, "dinheiro"), Is.EqualTo(0L));
        Assert.That(GetField<int>(cost, "petroleo"), Is.EqualTo(0));
        Assert.That(GetField<int>(cost, "aco"), Is.EqualTo(0));
        Assert.That(GetField<int>(cost, "energia"), Is.EqualTo(0));
        Assert.That(GetField<int>(cost, "comida"), Is.EqualTo(0));
    }

    [Test]
    public void SessionPublishesVictoryOnce()
    {
        Type sessionType = RequireType("Hegemonia.RTS.RTSGameSession");
        Type resultType = RequireType("Hegemonia.RTS.RTSMatchResult");
        sessionHost = new GameObject("RTS_TestSession");
        Component session = sessionHost.AddComponent(sessionType);

        Invoke(session, "BeginGameplay", 1, 2, 1);
        Assert.That(GetProperty<bool>(session, "IsGameplay"), Is.True);

        object victory = Enum.Parse(resultType, "Victory");
        Assert.That((bool)Invoke(session, "ReportMatchResult", victory, "test"), Is.True);
        Assert.That(GetProperty<bool>(session, "IsFinished"), Is.True);
        Assert.That(GetProperty<object>(session, "Result").ToString(), Is.EqualTo("Victory"));
        Assert.That((bool)Invoke(session, "ReportMatchResult", Enum.Parse(resultType, "Defeat"), "ignored"), Is.False);
    }

    [Test]
    public void ManualVisibilityContactIsQueryable()
    {
        Type visibilityType = RequireType("Hegemonia.RTS.RTSVisibilityService");
        Type identityType = RequireType("IdentidadeUnidade");
        Type sourceType = RequireType("Hegemonia.RTS.RTSDetectionSource");

        visibilityHost = new GameObject("RTS_TestVisibility");
        Component service = visibilityHost.AddComponent(visibilityType);
        targetHost = new GameObject("RTS_TestEnemy");
        Component target = targetHost.AddComponent(identityType);
        identityType.GetField("teamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(target, 2);

        Assert.That((bool)Invoke(service, "IsVisibleToTeam", 1, target), Is.False);
        object manual = Enum.Parse(sourceType, "Manual");
        Invoke(service, "ReportContact", 1, target, manual, 10f);

        Assert.That((bool)Invoke(service, "IsVisibleToTeam", 1, target), Is.True);
        object[] args = { 1, target, Vector3.zero };
        Assert.That((bool)Invoke(service, "TryGetLastKnownPosition", args), Is.True);
        Assert.That((Vector3)args[2], Is.EqualTo(target.transform.position));
    }

    private static Type RequireType(string fullName)
    {
        Type type = ResolveType(fullName);
        Assert.That(type, Is.Not.Null, "Tipo nao carregado: " + fullName);
        return type;
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

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Metodo nao encontrado: " + methodName);
        return method.Invoke(target, args);
    }

    private static void InvokeStaticIfPresent(string typeName, string methodName)
    {
        Type type = ResolveType(typeName);
        if (type == null)
        {
            return;
        }

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(null, null);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo nao encontrado: " + fieldName);
        return (T)field.GetValue(target);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, "Propriedade nao encontrada: " + propertyName);
        return (T)property.GetValue(target);
    }

    private static void DestroyIfPresent(ref GameObject target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
        }
    }
}
