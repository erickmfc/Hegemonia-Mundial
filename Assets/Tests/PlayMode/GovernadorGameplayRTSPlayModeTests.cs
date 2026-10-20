using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GovernadorGameplayRTSPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private Type governorType;
    private Type identityType;
    private Type constructionType;
    private PropertyInfo instanceProperty;
    private MethodInfo instanceSetter;
    private object previousInstance;
    private bool singletonOverridden;
    private GameObject governorObject;
    private GameObject aircraftPrefab;
    private GameObject fieldAircraft;
    private UnityEngine.Object aircraftData;
    private GameObject managerObject;
    private GameObject vehiclePrefab;
    private UnityEngine.Object vehicleData;
    private FieldInfo gameManagerInstanceField;
    private object previousGameManagerInstance;
    private bool gameManagerSingletonOverridden;
    private GameObject shipyardObject;
    private GameObject navalPrefab;
    private UnityEngine.Object navalData;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        governorType = ResolveType("GovernadorGameplayRTS");
        identityType = ResolveType("IdentidadeUnidade");
        constructionType = ResolveType("DadosConstrucao");
        instanceProperty = governorType.GetProperty("Instancia", Members);
        Assert.That(instanceProperty, Is.Not.Null);
        instanceSetter = instanceProperty.GetSetMethod(true);
        Assert.That(instanceSetter, Is.Not.Null);
        previousInstance = instanceProperty.GetValue(null, null);
        instanceSetter.Invoke(null, new object[] { null });
        singletonOverridden = true;

        governorObject = new GameObject("GovernadorGameplayRTS stale-count regression fixture");
        Component governor = governorObject.AddComponent(governorType);
        SetField(governor, "limiteTotal", 1);
        SetField(governor, "limiteAereos", 1);
        SetField(governor, "bloquearProducaoQuandoSaturado", false);

        FieldInfo countField = governorType.GetField("contagem", Members);
        Assert.That(countField, Is.Not.Null);
        object emptyCount = Activator.CreateInstance(countField.FieldType);
        SetField(emptyCount, "total", 0);
        SetField(emptyCount, "aereos", 0);
        countField.SetValue(governor, emptyCount);
        SetField(governor, "proximoRefresh", Time.unscaledTime + 10f);

        aircraftPrefab = new GameObject("Stale-count aircraft prefab");
        aircraftPrefab.SetActive(false);
        Component prefabIdentity = aircraftPrefab.AddComponent(identityType);
        SetField(prefabIdentity, "teamID", 1);
        SetField(prefabIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Aereo"));
        aircraftData = ScriptableObject.CreateInstance(constructionType);
        SetProperty(aircraftData, "NomeItem", "Stale-count aircraft");
        SetProperty(aircraftData, "PrefabDaUnidade", aircraftPrefab);
        SetField(aircraftData, "categoria", Enum.Parse(ResolveType("DadosConstrucao+CategoriaItem"), "Aeronautica"));

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (fieldAircraft != null) UnityEngine.Object.Destroy(fieldAircraft);
        if (aircraftPrefab != null) UnityEngine.Object.Destroy(aircraftPrefab);
        if (aircraftData != null) UnityEngine.Object.Destroy(aircraftData);
        if (vehiclePrefab != null) UnityEngine.Object.Destroy(vehiclePrefab);
        if (vehicleData != null) UnityEngine.Object.Destroy(vehicleData);
        if (managerObject != null) UnityEngine.Object.Destroy(managerObject);
        if (navalPrefab != null) UnityEngine.Object.Destroy(navalPrefab);
        if (navalData != null) UnityEngine.Object.Destroy(navalData);
        if (shipyardObject != null) UnityEngine.Object.Destroy(shipyardObject);
        if (governorObject != null) UnityEngine.Object.Destroy(governorObject);
        yield return null;

        if (gameManagerSingletonOverridden && gameManagerInstanceField != null)
        {
            gameManagerInstanceField.SetValue(null, previousGameManagerInstance);
            gameManagerSingletonOverridden = false;
        }

        if (singletonOverridden && instanceSetter != null)
        {
            instanceSetter.Invoke(null, new[] { previousInstance });
            singletonOverridden = false;
        }
    }

    [UnityTest]
    public IEnumerator ProductionGateMustSeeUnitRegisteredAfterCachedSnapshot()
    {
        fieldAircraft = new GameObject("Stale-count aircraft already in field");
        Component fieldIdentity = fieldAircraft.AddComponent(identityType);
        SetField(fieldIdentity, "teamID", 1);
        SetField(fieldIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Aereo"));
        yield return null;

        MethodInfo allowProduction = governorType.GetMethod("PermitirProducao", Members);
        Assert.That(allowProduction, Is.Not.Null);
        object[] args = { aircraftData, 1, null };
        bool allowed = (bool)allowProduction.Invoke(null, args);
        string reason = args[2] as string;

        Assert.That(allowed, Is.False,
            "Com limite aéreo 1 e uma aeronave já registrada depois do snapshot, o gate não pode permitir outra unidade. Motivo: " + reason);
    }

    [UnityTest]
    public IEnumerator ProductionGateMustIncludeAlreadyQueuedGroundOrders()
    {
        Type managerType = ResolveType("GerenteDeJogo");
        gameManagerInstanceField = managerType.GetField("Instancia", Members);
        Assert.That(gameManagerInstanceField, Is.Not.Null);
        previousGameManagerInstance = gameManagerInstanceField.GetValue(null);

        managerObject = new GameObject("GerenteDeJogo pending-order regression fixture");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(managerType);
        gameManagerInstanceField.SetValue(null, manager);
        gameManagerSingletonOverridden = true;

        vehiclePrefab = new GameObject("Pending queued ground vehicle");
        vehiclePrefab.SetActive(false);
        Component identity = vehiclePrefab.AddComponent(identityType);
        SetField(identity, "teamID", 1);
        SetField(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Veiculo"));
        vehicleData = ScriptableObject.CreateInstance(constructionType);
        SetProperty(vehicleData, "NomeItem", "Pending queued ground vehicle");
        SetProperty(vehicleData, "PrefabDaUnidade", vehiclePrefab);
        SetField(vehicleData, "categoria", Enum.Parse(ResolveType("DadosConstrucao+CategoriaItem"), "Exercito"));

        IList productionQueue = (IList)managerType.GetField("filaProducao", Members).GetValue(manager);
        Type orderType = ResolveType("GerenteDeJogo+PedidoDeProducao");
        object queuedOrder = Activator.CreateInstance(orderType);
        SetField(queuedOrder, "nomeUnidade", vehiclePrefab.name);
        SetField(queuedOrder, "prefab", vehiclePrefab);
        productionQueue.Add(queuedOrder);

        bool allowed = (bool)governorType.GetMethod("PermitirProducao", Members)
            .Invoke(null, new object[] { vehicleData, 1, null });

        Assert.That(allowed, Is.False,
            "Com limite total 1 e uma unidade terrestre já aguardando na fila central, o gate não pode aceitar uma segunda unidade.");

        SetField(governorObject.GetComponent(governorType), "limiteTotal", 10);
        SetField(governorObject.GetComponent(governorType), "limiteVeiculos", 1);
        object[] categoryArgs = { vehicleData, 1, null };
        bool categoryAllowed = (bool)governorType.GetMethod("PermitirProducao", Members).Invoke(null, categoryArgs);
        Assert.That(categoryAllowed, Is.False,
            "Com limite de veículos 1 e um veículo aguardando na fila central, o limite da categoria também deve bloquear a nova produção.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ProductionGateMustIncludeShipsAlreadyBuildingInShipyardSlots()
    {
        SetField(governorObject.GetComponent(governorType), "limiteTotal", 2);

        navalPrefab = new GameObject("Ship already building in shipyard");
        navalPrefab.SetActive(false);
        Component prefabIdentity = navalPrefab.AddComponent(identityType);
        SetField(prefabIdentity, "teamID", 1);
        SetField(prefabIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Naval"));
        navalData = ScriptableObject.CreateInstance(constructionType);
        SetProperty(navalData, "NomeItem", "Ship waiting to finish");
        SetProperty(navalData, "PrefabDaUnidade", navalPrefab);
        SetField(navalData, "categoria", Enum.Parse(ResolveType("DadosConstrucao+CategoriaItem"), "Marinha"));

        shipyardObject = new GameObject("Player shipyard active-slot regression fixture");
        shipyardObject.SetActive(false);
        Component shipyardIdentity = shipyardObject.AddComponent(identityType);
        SetField(shipyardIdentity, "teamID", 1);
        SetField(shipyardIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Estrutura"));
        Component shipyard = shipyardObject.AddComponent(ResolveType("Estaleiro"));
        SetProperty(shipyard, "OwnerTeamId", 1);

        Type slotType = ResolveType("Estaleiro+SlotConstrucao");
        object activeSlot = Activator.CreateInstance(slotType);
        SetField(activeSlot, "estaOcupado", true);
        SetField(activeSlot, "prefabAtual", navalPrefab);
        Array slots = Array.CreateInstance(slotType, 1);
        slots.SetValue(activeSlot, 0);
        SetField(shipyard, "slots", slots);
        shipyardObject.SetActive(true);

        bool allowed = (bool)governorType.GetMethod("PermitirProducao", Members)
            .Invoke(null, new object[] { navalData, 1, null });

        Assert.That(allowed, Is.False,
            "Com limite total 2, a estrutura naval e um navio já em construção ocupam toda a capacidade antes do próximo pedido.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ProductionGateMustIncludeShipsWaitingInShipyardQueue()
    {
        SetField(governorObject.GetComponent(governorType), "limiteTotal", 2);

        navalPrefab = new GameObject("Ship already queued at shipyard");
        navalPrefab.SetActive(false);
        Component prefabIdentity = navalPrefab.AddComponent(identityType);
        SetField(prefabIdentity, "teamID", 1);
        SetField(prefabIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Naval"));
        navalData = ScriptableObject.CreateInstance(constructionType);
        SetProperty(navalData, "NomeItem", "Ship waiting for next yard batch");
        SetProperty(navalData, "PrefabDaUnidade", navalPrefab);
        SetField(navalData, "categoria", Enum.Parse(ResolveType("DadosConstrucao+CategoriaItem"), "Marinha"));

        shipyardObject = new GameObject("Player shipyard pending-queue regression fixture");
        shipyardObject.SetActive(false);
        Component shipyardIdentity = shipyardObject.AddComponent(identityType);
        SetField(shipyardIdentity, "teamID", 1);
        SetField(shipyardIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Estrutura"));
        Component shipyard = shipyardObject.AddComponent(ResolveType("Estaleiro"));
        SetProperty(shipyard, "OwnerTeamId", 1);

        Type navalOrderType = ResolveType("Estaleiro+PedidoNavalAutomatico");
        object navalOrder = Activator.CreateInstance(navalOrderType, true);
        SetField(navalOrder, "Prefab", navalPrefab);
        FieldInfo queueField = shipyard.GetType().GetField("filaNaval", Members);
        Assert.That(queueField, Is.Not.Null);
        object queue = queueField.GetValue(shipyard);
        queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { navalOrder });
        shipyardObject.SetActive(true);

        bool allowed = (bool)governorType.GetMethod("PermitirProducao", Members)
            .Invoke(null, new object[] { navalData, 1, null });

        Assert.That(allowed, Is.False,
            "Com limite total 2, a estrutura naval e um navio na fila do próximo lote já ocupam a capacidade antes do próximo pedido.");
        yield return null;
    }

    private static Type ResolveType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null) return type;
        }

        Assert.Fail("Tipo do jogo não encontrado: " + fullName);
        return null;
    }

    private static void SetField(object target, string name, object value)
    {
        Assert.That(target, Is.Not.Null);
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo esperado não encontrado: " + name);
        field.SetValue(target, value);
    }

    private static void SetProperty(object target, string name, object value)
    {
        Assert.That(target, Is.Not.Null);
        PropertyInfo property = target.GetType().GetProperty(name, Members);
        Assert.That(property, Is.Not.Null, "Propriedade esperada não encontrada: " + name);
        property.SetValue(target, value, null);
    }
}
