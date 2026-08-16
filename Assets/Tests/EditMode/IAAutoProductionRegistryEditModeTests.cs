using System;
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
