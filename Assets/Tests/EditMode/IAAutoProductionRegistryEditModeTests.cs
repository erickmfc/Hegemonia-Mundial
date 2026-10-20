using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

public sealed class IAAutoProductionRegistryEditModeTests
{
    private static readonly Type RegistryType = ResolveType("Hegemonia.AI.Shared.IAAutoProductionRegistry");
    private static readonly Type StateType = ResolveType("Hegemonia.AI.Shared.IAProductionOrderState");

    [SetUp]
    public void SetUp()
    {
        InvokeStatic("Clear");
    }

    [TearDown]
    public void TearDown()
    {
        InvokeStatic("Clear");
    }

    [Test]
    public void RepeatedPlannerCyclesCreateOnlyOneEquivalentOrder()
    {
        Assert.That(TryReserve(2, "fighter", "defense", 6, 5, 10f, out string first), Is.True);
        Assert.That(TryReserve(2, "fighter", "defense", 6, 5, 10.1f, out _), Is.False);
        Assert.That(first, Is.Not.Empty);
        Assert.That(Count(2, "fighter", "Reserved"), Is.EqualTo(1));
    }

    [Test]
    public void ReservationCountsTowardsNetDemand()
    {
        Assert.That(TryReserve(2, "fighter", "defense", 6, 5, 1f, out string id), Is.True);
        object diagnostics = GetDiagnostics(2, "fighter", 6, 5);
        Assert.That(GetField<int>(diagnostics, "Reserved"), Is.EqualTo(1));
        Assert.That(GetField<int>(diagnostics, "NetDemand"), Is.EqualTo(0));
        InvokeStatic("ConfirmQueued", id, 7, 2f);
        diagnostics = GetDiagnostics(2, "fighter", 6, 5);
        Assert.That(GetField<int>(diagnostics, "Queued"), Is.EqualTo(1));
        Assert.That(GetField<int>(diagnostics, "NetDemand"), Is.EqualTo(0));
    }

    [Test]
    public void CancellationReleasesReservation()
    {
        Assert.That(TryReserve(2, "tank", "defense", 3, 2, 1f, out string id), Is.True);
        Assert.That((bool)InvokeStatic("Release", id, 2f), Is.True);
        Assert.That(GetField<int>(GetDiagnostics(2, "tank", 3, 2), "NetDemand"), Is.EqualTo(1));
    }

    [Test]
    public void SpecificReplacementSurvivesGenericCategoryReservation()
    {
        Assert.That(TryReserve(2, "tank", "military", 2, 1, 1f, out _), Is.True);

        object[] arguments = { 2, "tank", "ia03-replacement:specific-tank", 1, 1, null, 2f, 180f };
        Assert.That((bool)InvokeStatic("TryReserveReplacement", arguments), Is.True);
        Assert.That(arguments[5] as string, Is.Not.Empty);
        Assert.That(Count(2, "tank", "Reserved"), Is.EqualTo(2));

        object diagnostics = GetDiagnostics(2, "tank", 3, 1);
        Assert.That(GetField<int>(diagnostics, "NetDemand"), Is.EqualTo(0));
    }

    [Test]
    public void StrategicReplacementCanReleaseReservationWhenItsCommandFails()
    {
        Assert.That(TryReserve(2, "tank", "ia03-replacement:tank", 1, 0, 1f, out string orderId), Is.True);

        Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        object queue = Activator.CreateInstance(queueType);
        queueType.GetProperty("TraceTeamId", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(queue, 2, null);

        Type payloadType = ResolveType("Hegemonia.AI.BrainMaster.IA_ProduceOrderData");
        object payload = Activator.CreateInstance(payloadType);
        payloadType.GetField("ItemKey", BindingFlags.Instance | BindingFlags.Public).SetValue(payload, "tank");
        payloadType.GetField("Quantity", BindingFlags.Instance | BindingFlags.Public).SetValue(payload, 1);
        payloadType.GetField("ProductionOrderId", BindingFlags.Instance | BindingFlags.Public).SetValue(payload, orderId);

        Type factoryType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandFactory");
        object request = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[]
        {
            Enum.Parse(ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType"), "Produce"),
            "IA03EstrategaNacional",
            "estrategico",
            "reposição de baixa da missão estratégica",
            500,
            "production",
            "ia03-replacement:tank",
            0.35f,
            payload,
            orderId
        });

        MethodInfo enqueue = queueType.GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.Public);
        object[] enqueueArguments = { request, 1f, null };
        Assert.That((bool)enqueue.Invoke(queue, enqueueArguments), Is.True, enqueueArguments[2] as string ?? string.Empty);
        Assert.That((bool)InvokeStatic("ConfirmQueued", orderId, 0, 1f), Is.True);
        MethodInfo dequeue = queueType.GetMethod("TryDequeue", BindingFlags.Instance | BindingFlags.Public);
        object[] dequeueArguments = { 2f, null };
        Assert.That((bool)dequeue.Invoke(queue, dequeueArguments), Is.True);

        queueType.GetMethod("Complete", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(queue, new object[] { dequeueArguments[1], false, 2f, "falha controlada do executor" });
        object status = queueType.GetMethod("GetStatus", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(queue, new object[] { orderId });
        Assert.That(status.ToString(), Is.EqualTo("Failed"));
        Assert.That((bool)InvokeStatic("Release", orderId, 2f), Is.True);

        object[] stateArguments = { orderId, null };
        Assert.That((bool)InvokeStatic("TryGetOrderState", stateArguments), Is.True);
        Assert.That(stateArguments[1].ToString(), Is.EqualTo("Released"));
    }

    [Test]
    public void UnacceptedReservationExpiresAfterItsTtl()
    {
        Assert.That(TryReserve(2, "tank", "defense", 3, 2, 1f, out string id), Is.True);

        CleanupAt(182f);

        Assert.That((string)InvokeStatic("FindActiveOrder", 2, "tank", "defense"), Is.Empty);
    }

    [Test]
    public void SchedulerQueuedOrderWithoutProducerExpiresAfterItsTtl()
    {
        Assert.That(TryReserve(2, "fighter", "air defense", 3, 2, 1f, out string id), Is.True);
        Assert.That((bool)InvokeStatic("ConfirmQueued", id, 0, 2f), Is.True);

        CleanupAt(183f);

        Assert.That((string)InvokeStatic("FindActiveOrder", 2, "fighter", "air defense"), Is.Empty);
    }

    [TestCase("Queued", 11)]
    [TestCase("Constructing", 11)]
    [TestCase("Constructing", -11)]
    public void ProducerOwnedOrderDoesNotExpireAfterItsReservationTtl(string state, int producerInstanceId)
    {
        Assert.That(TryReserve(2, "naval", "fleet", 1, 0, 1f, out string id), Is.True);
        string transition = state == "Queued" ? "ConfirmQueued" : "ConfirmConstructionStarted";
        Assert.That((bool)InvokeStatic(transition, id, producerInstanceId, 2f), Is.True);
        Assert.That((bool)InvokeStatic("IsOwnedByProducer", id), Is.True);

        CleanupAt(183f);

        Assert.That((string)InvokeStatic("FindActiveOrder", 2, "naval", "fleet"), Is.EqualTo(id));
    }

    [TestCase(11)]
    [TestCase(-11)]
    public void SchedulerQueuedOrderCanTransferToPhysicalProducerOwnership(int producerInstanceId)
    {
        Assert.That(TryReserve(2, "naval", "fleet", 1, 0, 1f, out string id), Is.True);
        Assert.That((bool)InvokeStatic("ConfirmQueued", id, 0, 2f), Is.True);
        Assert.That((bool)InvokeStatic("IsOwnedByProducer", id), Is.False);

        Assert.That((bool)InvokeStatic("ConfirmQueued", id, producerInstanceId, 3f), Is.True);
        Assert.That((bool)InvokeStatic("IsOwnedByProducer", id), Is.True);
        CleanupAt(183f);

        Assert.That((string)InvokeStatic("FindActiveOrder", 2, "naval", "fleet"), Is.EqualTo(id));
    }

    [Test]
    public void ActiveOrdersCanBeRecoveredByTeamAndPurposePrefix()
    {
        Assert.That(TryReserve(2, "naval", "ia03-replacement:destroyer.alpha", 1, 0, 1f, out string ia03Order), Is.True);
        Assert.That(TryReserve(2, "tank", "defense", 1, 0, 1f, out _), Is.True);
        Assert.That(TryReserve(3, "naval", "ia03-replacement:destroyer.bravo", 1, 0, 1f, out _), Is.True);

        IList found = (IList)InvokeStatic("GetActiveOrdersForTeamByPurposePrefix", 2, "ia03-replacement:");

        Assert.That(found.Count, Is.EqualTo(1));
        Assert.That(GetField<string>(found[0], "orderId"), Is.EqualTo(ia03Order));
        Assert.That(GetField<string>(found[0], "purpose"), Is.EqualTo("ia03-replacement:destroyer.alpha"));
    }

    [Test]
    public void CompletionDoesNotRemainInDemandOrDuplicateCount()
    {
        Assert.That(TryReserve(2, "naval", "fleet", 1, 0, 1f, out string id), Is.True);
        InvokeStatic("ConfirmConstructionStarted", id, 11, 2f);
        Assert.That((bool)InvokeStatic("Complete", id, 3f), Is.True);
        object diagnostics = GetDiagnostics(2, "naval", 1, 1);
        Assert.That(GetField<int>(diagnostics, "Reserved") + GetField<int>(diagnostics, "Queued") + GetField<int>(diagnostics, "Constructing"), Is.EqualTo(0));
        Assert.That(GetField<int>(diagnostics, "NetDemand"), Is.EqualTo(0));
    }

    [Test]
    public void SaveLoadDoesNotRecreateCompletedReservations()
    {
        Assert.That(TryReserve(2, "fighter", "defense", 2, 1, 1f, out string completed), Is.True);
        InvokeStatic("Complete", completed, 2f);
        Assert.That(TryReserve(3, "tank", "defense", 2, 1, 2f, out string active), Is.True);

        object save = InvokeStatic("CaptureSaveData");
        InvokeStatic("Clear");
        InvokeStatic("RestoreSaveData", save);

        Assert.That((string)InvokeStatic("FindActiveOrder", 2, "fighter", "defense"), Is.Empty);
        Assert.That((string)InvokeStatic("FindActiveOrder", 3, "tank", "defense"), Is.EqualTo(active));
        Assert.That(TryReserve(2, "fighter", "defense", 2, 1, 3f, out string next), Is.True);
        Assert.That(next, Is.Not.EqualTo(completed));
    }

    [Test]
    public void TeamsHaveIndependentCounters()
    {
        Assert.That(TryReserve(2, "fighter", "defense", 1, 0, 1f, out _), Is.True);
        Assert.That(TryReserve(3, "fighter", "defense", 1, 0, 1f, out _), Is.True);
        Assert.That(Count(2, "fighter", "Reserved"), Is.EqualTo(1));
        Assert.That(Count(3, "fighter", "Reserved"), Is.EqualTo(1));
    }

    [Test]
    public void RealDemandStillAllowsAutomaticProduction()
    {
        Assert.That(TryReserve(2, "infantry", "defense", 6, 2, 1f, out _), Is.True);
        Assert.That(GetField<int>(GetDiagnostics(2, "infantry", 6, 2), "NetDemand"), Is.EqualTo(3));
    }

    private static bool TryReserve(int teamId, string unitType, string purpose, int desired, int alive, float now, out string orderId)
    {
        object[] args = { teamId, unitType, purpose, desired, alive, null, now, 180f };
        bool result = (bool)InvokeStatic("TryReserveProduction", args);
        orderId = args[5] as string;
        return result;
    }

    private static int Count(int teamId, string unitType, string stateName)
    {
        return (int)InvokeStatic("Count", teamId, unitType, Enum.Parse(StateType, stateName));
    }

    private static object GetDiagnostics(int teamId, string unitType, int desired, int alive)
    {
        return InvokeStatic("GetDiagnostics", teamId, unitType, desired, alive);
    }

    private static T GetField<T>(object value, string name)
    {
        return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public).GetValue(value);
    }

    private static object InvokeStatic(string methodName, params object[] arguments)
    {
        MethodInfo method = RegistryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new MissingMethodException(RegistryType.FullName, methodName);
        return method.Invoke(null, arguments);
    }

    private static void CleanupAt(float now)
    {
        MethodInfo method = RegistryType.GetMethod("Cleanup", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new MissingMethodException(RegistryType.FullName, "Cleanup");
        method.Invoke(null, new object[] { now });
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }

        Type resolved = Type.GetType(fullName + ", Assembly-CSharp", false);
        if (resolved != null) return resolved;
        throw new InvalidOperationException("Tipo não carregado: " + fullName);
    }
}
