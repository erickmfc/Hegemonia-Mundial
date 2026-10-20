using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    public void PlayerPaymentUsesTreasuryWhenAvailableAndFailsClosedWithoutIt()
    {
        Type resourcesType = RequireType("GerenciadorRecursos");
        PropertyInfo instance = resourcesType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(instance, Is.Not.Null);
        MethodInfo setInstance = instance.GetSetMethod(true);
        Assert.That(setInstance, Is.Not.Null);
        object originalInstance = instance.GetValue(null);
        GameObject resourcesHost = new GameObject("RTS_TestResources");
        GameObject gameHost = new GameObject("RTS_TestGameManager");
        resourcesHost.SetActive(false);
        gameHost.SetActive(false);

        try
        {
            Component resources = resourcesHost.AddComponent(resourcesType);
            Component gameManager = gameHost.AddComponent(RequireType("GerenteDeJogo"));
            FieldInfo money = resourcesType.GetField("dinheiro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(money, Is.Not.Null);
            money.SetValue(resources, 100L);
            setInstance.Invoke(null, new[] { resources });

            Assert.That((bool)Invoke(gameManager, "TentarGastarDinheiro", 25L), Is.True);
            Assert.That((long)money.GetValue(resources), Is.EqualTo(75L));

            setInstance.Invoke(null, new object[] { null });
            LogAssert.Expect(LogType.Warning, "⚠️ GerenciadorRecursos não encontrado. Transação financeira recusada.");
            Assert.That((bool)Invoke(gameManager, "TentarGastarDinheiro", 1L), Is.False);
            Assert.That((long)money.GetValue(resources), Is.EqualTo(75L));
        }
        finally
        {
            setInstance.Invoke(null, new[] { originalInstance });
            UnityEngine.Object.DestroyImmediate(gameHost);
            UnityEngine.Object.DestroyImmediate(resourcesHost);
        }
    }

    [Test]
    public void GroundProductionRequiresPoweredRegisteredFactoryBeforeCharging()
    {
        Type resourcesType = RequireType("GerenciadorRecursos");
        PropertyInfo instance = resourcesType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(instance, Is.Not.Null);
        MethodInfo setInstance = instance.GetSetMethod(true);
        Assert.That(setInstance, Is.Not.Null);
        object originalInstance = instance.GetValue(null);
        GameObject resourcesHost = new GameObject("RTS_GroundProductionResources");
        GameObject managerHost = new GameObject("RTS_GroundProductionManager");
        GameObject prefab = new GameObject("UnitTest");
        GameObject factoryHost = null;
        resourcesHost.SetActive(false);
        managerHost.SetActive(false);
        prefab.SetActive(false);

        try
        {
            Component resources = resourcesHost.AddComponent(resourcesType);
            Component manager = managerHost.AddComponent(RequireType("GerenteDeJogo"));
            FieldInfo money = resourcesType.GetField("dinheiro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            money.SetValue(resources, 100L);
            setInstance.Invoke(null, new object[] { resources });

            FieldInfo queueField = manager.GetType().GetField("filaProducao", BindingFlags.Instance | BindingFlags.Public);
            System.Collections.ICollection queue = (System.Collections.ICollection)queueField.GetValue(manager);
            LogAssert.Expect(LogType.Warning, "[Produção] Pedido de UnitTest recusado: nenhum ponto terrestre operacional (fábrica/quartel com energia).");
            Assert.That((bool)Invoke(manager, "TentarComprarUnidade", prefab, 25L, 1), Is.False);
            Assert.That((long)money.GetValue(resources), Is.EqualTo(100L));
            Assert.That(queue.Count, Is.EqualTo(0));

            factoryHost = new GameObject("RTS_GroundProductionFactory");
            factoryHost.SetActive(false);
            Component economy = factoryHost.AddComponent(RequireType("EstruturaEconomica"));
            factoryHost.AddComponent(RequireType("Fabrica"));
            FieldInfo efficiency = economy.GetType().GetField("eficiencia", BindingFlags.Instance | BindingFlags.Public);
            efficiency.SetValue(economy, 0.5f);
            GameObject spawn = new GameObject("RTS_GroundProductionSpawn");
            spawn.transform.SetParent(factoryHost.transform, false);
            factoryHost.SetActive(true);
            Invoke(manager, "AtualizarPontoHangar", spawn.transform, spawn.transform);

            LogAssert.Expect(LogType.Warning, "[Produção] Pedido de UnitTest recusado: nenhum ponto terrestre operacional (fábrica/quartel com energia).");
            Assert.That((bool)Invoke(manager, "TentarComprarUnidade", prefab, 25L, 1), Is.False);
            Assert.That((long)money.GetValue(resources), Is.EqualTo(100L));
            Assert.That(queue.Count, Is.EqualTo(0));

            efficiency.SetValue(economy, 1f);
            Assert.That((bool)Invoke(manager, "TentarComprarUnidade", prefab, 25L, 1), Is.True);
            Assert.That((long)money.GetValue(resources), Is.EqualTo(75L));
            Assert.That(queue.Count, Is.EqualTo(1));
        }
        finally
        {
            setInstance.Invoke(null, new object[] { originalInstance });
            if (factoryHost != null) UnityEngine.Object.DestroyImmediate(factoryHost);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(managerHost);
            UnityEngine.Object.DestroyImmediate(resourcesHost);
        }
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
    public void LosingOwnPrefectureDoesNotFinishTheMatch()
    {
        Type endGameType = RequireType("SistemaFimDeJogo");
        PropertyInfo matchEnded = endGameType.GetProperty(
            "PartidaEncerrada",
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(matchEnded, Is.Not.Null);
        MethodInfo resetMatchEnded = matchEnded.GetSetMethod(true);
        Assert.That(resetMatchEnded, Is.Not.Null);
        resetMatchEnded.Invoke(null, new object[] { false });

        Type objectiveType = RequireType("TipoObjetivoFinal");
        object prefecture = Enum.Parse(objectiveType, "Prefeitura");
        MethodInfo reportResult = endGameType.GetMethod(
            "RegistrarResultado",
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(reportResult, Is.Not.Null);

        reportResult.Invoke(null, new[] { prefecture, (object)true, "Sua nação", "Capital própria" });

        Assert.That((bool)matchEnded.GetValue(null), Is.False);
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
