using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RTSTacticalContactAdapterPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const int ExpectedMaximumContacts = 2048;

    [UnityTest]
    public IEnumerator AnonymousRadarContactHidesIdentityEstimatesPositionAndExpires()
    {
        Component adapter = FindOrCreateAdapter(out GameObject createdObject);
        try
        {
            yield return null;

            Type snapshotType = ResolveType("Hegemonia.RTS.RTSTacticalContactSnapshot");
            Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
            Type sourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
            Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
            Type identificationType = ResolveType("Hegemonia.RTS.RTSTacticalContactIdentification");
            Type threatType = ResolveType("Hegemonia.RTS.RTSTacticalContactThreatState");
            Type verticalType = ResolveType("Hegemonia.RTS.RTSTacticalVerticalKind");
            MethodInfo report = adapter.GetType().GetMethod("ReportAnonymousContact", Members);
            MethodInfo copy = adapter.GetType().GetMethod("CopiarContatos", Members);
            Assert.That(report, Is.Not.Null);
            Assert.That(copy, Is.Not.Null);

            int observerTeam = 700000 + (int)(Time.realtimeSinceStartup * 1000f) % 90000;
            object[] arguments =
            {
                observerTeam,
                41001,
                Enum.Parse(categoryType, "Naval"),
                Enum.Parse(sourceType, "Radar"),
                new Vector3(120f, 0f, 240f),
                new Vector3(10f, 0f, 0f),
                0.40f,
                0.35f,
                0.60f,
                90f,
                "test/radar",
                Enum.Parse(relationType, "Unknown"),
                Enum.Parse(identificationType, "Unknown"),
                Enum.Parse(threatType, "Unknown"),
                Enum.Parse(verticalType, "Unknown"),
                0f,
                true,
                false
            };

            Assert.That((bool)report.Invoke(adapter, arguments), Is.True);

            object snapshot = CopySingle(adapter, copy, snapshotType, observerTeam);
            Assert.That(Read(snapshot, "State").ToString(), Is.EqualTo("Current"));
            Assert.That((bool)Read(snapshot, "PositionIsEstimated"), Is.True);
            Assert.That((bool)Read(snapshot, "IdentityRevealed"), Is.False);
            Assert.That((int)Read(snapshot, "TargetInstanceId"), Is.EqualTo(0));
            Assert.That((int)Read(snapshot, "TargetTeamId"), Is.EqualTo(0));
            Assert.That((string)Read(snapshot, "IdentificationLabel"), Is.Empty);

            yield return new WaitForSecondsRealtime(0.45f);

            snapshot = CopySingle(adapter, copy, snapshotType, observerTeam);
            Assert.That(Read(snapshot, "State").ToString(), Is.EqualTo("Estimated"));
            Assert.That((bool)Read(snapshot, "HasPosition"), Is.True);
            Assert.That((bool)Read(snapshot, "PositionIsEstimated"), Is.True);
            Assert.That((bool)Read(snapshot, "IdentityRevealed"), Is.False);
            Assert.That((int)Read(snapshot, "TargetInstanceId"), Is.EqualTo(0));

            yield return new WaitForSecondsRealtime(0.90f);

            var expiredList = CreateSnapshotList(snapshotType);
            int remaining = (int)copy.Invoke(adapter, new object[] { observerTeam, expiredList });
            Assert.That(remaining, Is.EqualTo(0), "O contato deve sair da consulta depois do fim da retenção.");
        }
        finally
        {
            if (createdObject != null)
                UnityEngine.Object.Destroy(createdObject);
        }
    }

    [UnityTest]
    public IEnumerator AnonymousContactRequiresExplicitAffiliationEvidence()
    {
        Component adapter = FindOrCreateAdapter(out GameObject createdObject);
        try
        {
            yield return null;

            Type snapshotType = ResolveType("Hegemonia.RTS.RTSTacticalContactSnapshot");
            Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
            Type sourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
            Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
            Type identificationType = ResolveType("Hegemonia.RTS.RTSTacticalContactIdentification");
            Type threatType = ResolveType("Hegemonia.RTS.RTSTacticalContactThreatState");
            Type verticalType = ResolveType("Hegemonia.RTS.RTSTacticalVerticalKind");
            MethodInfo report = adapter.GetType().GetMethod("ReportAnonymousContact", Members);
            Assert.That(report, Is.Not.Null);

            int observerTeam = 2100000 + UnityEngine.Random.Range(1, 50000);
            object[] unverifiedArguments = CreateAnonymousArguments(
                observerTeam,
                91001,
                categoryType,
                sourceType,
                relationType,
                identificationType,
                threatType,
                verticalType,
                60f,
                60f,
                source: "AirRecon",
                relation: "Hostile");

            Assert.That((bool)report.Invoke(adapter, unverifiedArguments), Is.True);
            object unverified = CopySingle(adapter,
                adapter.GetType().GetMethod("CopiarContatos", Members),
                snapshotType,
                observerTeam);
            Assert.That(Read(unverified, "Relation").ToString(), Is.EqualTo("Unknown"));
            Assert.That((bool)Read(unverified, "IdentityRevealed"), Is.False);
            Assert.That((int)Read(unverified, "TargetTeamId"), Is.Zero);

            object[] verifiedArguments = CreateAnonymousArguments(
                observerTeam,
                91001,
                categoryType,
                sourceType,
                relationType,
                identificationType,
                threatType,
                verticalType,
                60f,
                60f,
                source: "AirRecon",
                relation: "Hostile",
                affiliationConfirmedByIndependentEvidence: true);

            Assert.That((bool)report.Invoke(adapter, verifiedArguments), Is.True);
            object verified = CopySingle(adapter,
                adapter.GetType().GetMethod("CopiarContatos", Members),
                snapshotType,
                observerTeam);
            Assert.That(Read(verified, "Relation").ToString(), Is.EqualTo("Hostile"));
            Assert.That((bool)Read(verified, "IdentityRevealed"), Is.False,
                "IFF pode confirmar afiliação sem revelar a identidade individual.");
            Assert.That((int)Read(verified, "TargetTeamId"), Is.Zero);

            object[] revokedArguments = CreateAnonymousArguments(
                observerTeam,
                91001,
                categoryType,
                sourceType,
                relationType,
                identificationType,
                threatType,
                verticalType,
                60f,
                60f,
                source: "AirRecon",
                relation: "Hostile");

            Assert.That((bool)report.Invoke(adapter, revokedArguments), Is.True);
            object revoked = CopySingle(adapter,
                adapter.GetType().GetMethod("CopiarContatos", Members),
                snapshotType,
                observerTeam);
            Assert.That(Read(revoked, "Relation").ToString(), Is.EqualTo("Unknown"),
                "Uma nova observação sem IFF deve remover a afiliação anterior.");
        }
        finally
        {
            if (createdObject != null)
                UnityEngine.Object.Destroy(createdObject);
        }
    }

    [UnityTest]
    public IEnumerator ContactCapacityStopsAt2048AndSingleSceneLoadClearsContacts()
    {
        Component adapter = FindOrCreateAdapter(out GameObject createdObject);
        try
        {
            yield return null;

            Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
            Type sourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
            Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
            Type identificationType = ResolveType("Hegemonia.RTS.RTSTacticalContactIdentification");
            Type threatType = ResolveType("Hegemonia.RTS.RTSTacticalContactThreatState");
            Type verticalType = ResolveType("Hegemonia.RTS.RTSTacticalVerticalKind");
            MethodInfo report = adapter.GetType().GetMethod("ReportAnonymousContact", Members);
            PropertyInfo count = adapter.GetType().GetProperty("ContactCount", Members);
            Assert.That(report, Is.Not.Null);
            Assert.That(count, Is.Not.Null);

            int baseline = (int)count.GetValue(adapter, null);
            int observerTeam = 1000000 + UnityEngine.Random.Range(1, 1000000);
            int accepted = 0;
            for (int i = 1; i <= ExpectedMaximumContacts + 1; i++)
            {
                object[] arguments = CreateAnonymousArguments(
                    observerTeam,
                    i,
                    categoryType,
                    sourceType,
                    relationType,
                    identificationType,
                    threatType,
                    verticalType,
                    60f,
                    60f);
                if (!(bool)report.Invoke(adapter, arguments))
                    break;
                accepted++;
            }

            Assert.That((int)count.GetValue(adapter, null), Is.EqualTo(ExpectedMaximumContacts));
            Assert.That(accepted, Is.EqualTo(ExpectedMaximumContacts - baseline));

            object[] overflowArguments = CreateAnonymousArguments(
                observerTeam,
                ExpectedMaximumContacts + 10,
                categoryType,
                sourceType,
                relationType,
                identificationType,
                threatType,
                verticalType,
                60f,
                60f);
            Assert.That((bool)report.Invoke(adapter, overflowArguments), Is.False,
                "O adaptador deve recusar contatos acima do limite e manter a contagem limitada.");

            SimulateSingleSceneLoad(adapter);
            Assert.That((int)count.GetValue(adapter, null), Is.Zero,
                "A limpeza associada à troca de cena deve liberar os registros em memória.");
        }
        finally
        {
            if (createdObject != null)
                UnityEngine.Object.Destroy(createdObject);
        }
    }

    [UnityTest]
    public IEnumerator SingleSceneLoadRemovesPreviouslyReportedContact()
    {
        Component adapter = FindOrCreateAdapter(out GameObject createdObject);
        try
        {
            yield return null;

            Type snapshotType = ResolveType("Hegemonia.RTS.RTSTacticalContactSnapshot");
            Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
            Type sourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
            Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
            Type identificationType = ResolveType("Hegemonia.RTS.RTSTacticalContactIdentification");
            Type threatType = ResolveType("Hegemonia.RTS.RTSTacticalContactThreatState");
            Type verticalType = ResolveType("Hegemonia.RTS.RTSTacticalVerticalKind");
            MethodInfo report = adapter.GetType().GetMethod("ReportAnonymousContact", Members);
            MethodInfo copy = adapter.GetType().GetMethod("CopiarContatos", Members);
            int observerTeam = 1900000 + UnityEngine.Random.Range(1, 50000);

            Assert.That((bool)report.Invoke(adapter, CreateAnonymousArguments(
                observerTeam,
                90001,
                categoryType,
                sourceType,
                relationType,
                identificationType,
                threatType,
                verticalType,
                60f,
                60f)), Is.True);
            Assert.That((int)copy.Invoke(adapter, new object[] { observerTeam, CreateSnapshotList(snapshotType) }), Is.EqualTo(1));

            SimulateSingleSceneLoad(adapter);

            Assert.That((int)copy.Invoke(adapter, new object[] { observerTeam, CreateSnapshotList(snapshotType) }), Is.Zero);
        }
        finally
        {
            if (createdObject != null)
                UnityEngine.Object.Destroy(createdObject);
        }
    }

    private static object CopySingle(Component adapter, MethodInfo copy, Type snapshotType, int observerTeam)
    {
        IList list = CreateSnapshotList(snapshotType);
        int count = (int)copy.Invoke(adapter, new object[] { observerTeam, list });
        Assert.That(count, Is.EqualTo(1));
        Assert.That(list.Count, Is.EqualTo(1));
        return list[0];
    }

    private static IList CreateSnapshotList(Type snapshotType)
    {
        Type listType = typeof(List<>).MakeGenericType(snapshotType);
        return (IList)Activator.CreateInstance(listType);
    }

    private static object[] CreateAnonymousArguments(
        int observerTeam,
        int sensorContactId,
        Type categoryType,
        Type sourceType,
        Type relationType,
        Type identificationType,
        Type threatType,
        Type verticalType,
        float validForSeconds,
        float retentionSeconds,
        string source = "Radar",
        string relation = "Unknown",
        bool affiliationConfirmedByIndependentEvidence = false)
    {
        return new object[]
        {
            observerTeam,
            sensorContactId,
            Enum.Parse(categoryType, "Naval"),
            Enum.Parse(sourceType, source),
            new Vector3(sensorContactId, 0f, 240f),
            new Vector3(10f, 0f, 0f),
            0.4f,
            validForSeconds,
            retentionSeconds,
            90f,
            "test/radar",
            Enum.Parse(relationType, relation),
            Enum.Parse(identificationType, "Unknown"),
            Enum.Parse(threatType, "Unknown"),
            Enum.Parse(verticalType, "Unknown"),
            0f,
            true,
            affiliationConfirmedByIndependentEvidence
        };
    }

    private static void SimulateSingleSceneLoad(Component adapter)
    {
        MethodInfo onSceneLoaded = adapter.GetType().GetMethod("OnSceneLoaded", Members);
        Assert.That(onSceneLoaded, Is.Not.Null);
        onSceneLoaded.Invoke(adapter, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
    }

    private static Component FindOrCreateAdapter(out GameObject createdObject)
    {
        Type adapterType = ResolveType("Hegemonia.RTS.RTSTacticalContactAdapter");
        Component adapter = UnityEngine.Object.FindFirstObjectByType(adapterType) as Component;
        if (adapter != null)
        {
            createdObject = null;
            return adapter;
        }

        createdObject = new GameObject("RTSTacticalContactAdapterPlayModeRuntime");
        return createdObject.AddComponent(adapterType);
    }

    private static object Read(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        if (field != null) return field.GetValue(target);

        PropertyInfo property = target.GetType().GetProperty(name, Members);
        if (property != null) return property.GetValue(target, null);
        throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static Type ResolveType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .First(type => type != null);
    }
}
