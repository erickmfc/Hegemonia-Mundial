using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IACommandQueueDedupEditModeTests
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    [Test]
    public void PendingDuplicateAndCooldownAreRejectedUntilTheCooldownExpires()
    {
        Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type commandType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType");
        object queue = Activator.CreateInstance(queueType);

        object first = CreateRequest(requestType, commandType, "first-order");
        Assert.That(Enqueue(queueType, queue, first, 10f, out string firstReason), Is.True, firstReason);

        object duplicate = CreateRequest(requestType, commandType, "duplicate-order");
        Assert.That(Enqueue(queueType, queue, duplicate, 10.1f, out string duplicateReason), Is.False);
        Assert.That(duplicateReason, Is.EqualTo("duplicada em fila"));

        object[] dequeueArguments = { 10.2f, null };
        Assert.That(queueType.GetMethod("TryDequeue", PublicInstance).Invoke(queue, dequeueArguments), Is.True);
        Assert.That(dequeueArguments[1], Is.SameAs(first));

        queueType.GetMethod("Complete", PublicInstance)
            .Invoke(queue, new object[] { first, true, 10.3f, "produção concluída no teste" });

        object duringCooldown = CreateRequest(requestType, commandType, "cooldown-order");
        Assert.That(Enqueue(queueType, queue, duringCooldown, 12f, out string cooldownReason), Is.False);
        Assert.That(cooldownReason, Is.EqualTo("em cooldown"));
        Assert.That(queueType.GetProperty("PendingCount", PublicInstance).GetValue(queue), Is.EqualTo(0));

        object afterCooldown = CreateRequest(requestType, commandType, "after-cooldown-order");
        Assert.That(Enqueue(queueType, queue, afterCooldown, 15.31f, out string afterCooldownReason), Is.True,
            afterCooldownReason);
        Assert.That(queueType.GetProperty("PendingCount", PublicInstance).GetValue(queue), Is.EqualTo(1));
    }

    [Test]
    public void AircraftCommandConfirmationMatchesCatalogNameToSpawnedPrefabName()
    {
        Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type commandType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType");
        Type produceDataType = ResolveType("Hegemonia.AI.BrainMaster.IA_ProduceOrderData");
        Type worldType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type confirmationType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandConfirmationService");
        Type backendType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type constructionType = ResolveType("DadosConstrucao");
        object queue = Activator.CreateInstance(queueType);
        object world = Activator.CreateInstance(worldType, new object[] { 987654 });
        object confirmation = Activator.CreateInstance(confirmationType);
        object backend = Activator.CreateInstance(backendType, new object[] { 987654 });
        object payload = Activator.CreateInstance(produceDataType);
        SetField(payload, "ItemKey", "Boeing E-3 Sentry");
        SetField(payload, "Quantity", 1);
        ScriptableObject catalogItem = ScriptableObject.CreateInstance(constructionType);
        GameObject prefab = new GameObject("Boeing_E3_Reconhecimento");
        GameObject spawnedAircraft = null;
        try
        {
            constructionType.GetProperty("NomeItem").SetValue(catalogItem, "Boeing E-3 Sentry", null);
            constructionType.GetProperty("PrefabDaUnidade").SetValue(catalogItem, prefab, null);
            backendType.GetMethod("AddCatalogItem", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(backend, new[] { catalogItem });

            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", "aircraft-name-mismatch");
            SetField(request, "Type", Enum.Parse(commandType, "Produce"));
            SetField(request, "Payload", payload);

            object[] enqueueArguments = { request, 10f, null };
            Assert.That(queueType.GetMethod("Enqueue", PublicInstance).Invoke(queue, enqueueArguments), Is.True,
                enqueueArguments[2] as string);
            object[] dequeueArguments = { 10f, null };
            Assert.That(queueType.GetMethod("TryDequeue", PublicInstance).Invoke(queue, dequeueArguments), Is.True);

            MethodInfo captureBaseline = confirmationType.GetMethod(
                "CaptureBaseline",
                PublicInstance,
                null,
                new[] { requestType, worldType, backendType },
                null);
            captureBaseline.Invoke(confirmation, new[] { request, world, backend });
            Assert.That(produceDataType.GetField("PrefabName", PublicInstance).GetValue(payload),
                Is.EqualTo("Boeing_E3_Reconhecimento"));

            var ownUnits = (List<GameObject>)worldType.GetField("OwnUnits", PublicInstance).GetValue(world);
            spawnedAircraft = new GameObject("Boeing_E3_Reconhecimento(Clone)");
            ownUnits.Add(spawnedAircraft);
            MethodInfo countMatching = confirmationType.GetMethod("CountMatching", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(countMatching.Invoke(null, new[] { request, world }), Is.EqualTo(1),
                "A correspondência deve reconhecer o nome do prefab antes do serviço atualizar a fila.");
            confirmationType.GetMethod("TrackAccepted", PublicInstance)
                .Invoke(confirmation, new object[] { request, queue, 10f, "aeroporto aceitou a aeronave" });
            confirmationType.GetMethod("Tick", PublicInstance).Invoke(confirmation, new[] { queue, world, (object)10.21f });

            object status = queueType.GetMethod("GetStatus", PublicInstance)
                .Invoke(queue, new[] { "aircraft-name-mismatch" });
            Assert.That(status.ToString(), Is.EqualTo("Success"),
                "A confirmação precisa casar a ficha Boeing E-3 Sentry com o prefab Boeing_E3_Reconhecimento.");

            confirmationType.GetMethod("Tick", PublicInstance).Invoke(confirmation, new[] { queue, world, (object)19f });
            Assert.That(queueType.GetProperty("PendingCount", PublicInstance).GetValue(queue), Is.EqualTo(0),
                "A ordem confirmada não pode voltar para a fila após o timeout.");
            Assert.That(queueType.GetProperty("CompletedSuccessCount", PublicInstance).GetValue(queue), Is.EqualTo(1));
        }
        finally
        {
            if (spawnedAircraft != null) UnityEngine.Object.DestroyImmediate(spawnedAircraft);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(catalogItem);
        }
    }

    private static object CreateRequest(Type requestType, Type commandType, string id)
    {
        object request = Activator.CreateInstance(requestType);
        SetField(request, "Id", id);
        SetField(request, "Origin", "IA03");
        SetField(request, "Domain", "production");
        SetField(request, "Reason", "necessidade de produção do teste");
        SetField(request, "Family", "production");
        SetField(request, "Type", Enum.Parse(commandType, "Produce"));
        SetField(request, "Priority", 10);
        SetField(request, "DedupKey", "aircraft:test-item");
        SetField(request, "CooldownSeconds", 5f);
        return request;
    }

    private static bool Enqueue(Type queueType, object queue, object request, float now, out string reason)
    {
        object[] arguments = { request, now, null };
        bool accepted = (bool)queueType.GetMethod("Enqueue", PublicInstance).Invoke(queue, arguments);
        reason = arguments[2] as string;
        return accepted;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PublicInstance);
        Assert.That(field, Is.Not.Null, "Campo de request ausente: " + name);
        field.SetValue(target, value);
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
