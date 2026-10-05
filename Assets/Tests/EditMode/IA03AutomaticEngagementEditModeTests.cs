using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Verifica a autorização automática e a preservação do alvo explicitamente
/// enviado, sem depender da cena de validação ou salvar alterações nela.
/// O assembly EditMode é isolado, por isso os tipos do jogo são resolvidos por reflexão.
/// </summary>
public sealed class IA03AutomaticEngagementEditModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> objects = new List<GameObject>();
    private Type identityType;
    private Type governmentType;
    private Component government;
    private object previousGovernment;
    private Type territoryManagerType;
    private object previousTerritoryManager;
    private NavMeshData navMeshData;
    private NavMeshDataInstance navMeshInstance;
    private bool navMeshRegistered;

    [SetUp]
    public void SetUp()
    {
        identityType = ResolveType("IdentidadeUnidade");
        governmentType = ResolveType("SistemaGovernoMundial");
        Assert.That(identityType, Is.Not.Null);
        Assert.That(governmentType, Is.Not.Null);

        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        previousGovernment = instanceProperty.GetValue(null, null);

        territoryManagerType = ResolveType("GerenteDeTerritorio");
        FieldInfo territoryInstance = territoryManagerType.GetField("Instancia", StaticMembers);
        previousTerritoryManager = territoryInstance != null ? territoryInstance.GetValue(null) : null;
        if (territoryInstance != null) territoryInstance.SetValue(null, null);

        GameObject governmentObject = Track(new GameObject("IA03 engagement test government"));
        governmentObject.SetActive(false);
        government = governmentObject.AddComponent(governmentType);
        instanceProperty.GetSetMethod(true).Invoke(null, new[] { government });
        object relation = governmentType.GetMethod("ObterRelacao", InstanceMembers)
            .Invoke(government, new object[] { 1, 3 });
        Assert.That(relation, Is.Not.Null);

        ResetGlobalOrderRegistry();
    }

    [TearDown]
    public void TearDown()
    {
        if (navMeshRegistered) navMeshInstance.Remove();
        if (navMeshData != null) UnityEngine.Object.DestroyImmediate(navMeshData);

        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        instanceProperty.GetSetMethod(true).Invoke(null, new[] { previousGovernment });

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        }
        objects.Clear();

        FieldInfo territoryInstance = territoryManagerType.GetField("Instancia", StaticMembers);
        if (territoryInstance != null) territoryInstance.SetValue(null, previousTerritoryManager);
        ResetGlobalOrderRegistry();
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void AlvoAutomaticoEmPazNeutraEhRecusado(string turretTypeName)
    {
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 3, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False,
            "Uma relação neutra em paz não autoriza disparo automático.");
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void AlvoAutomaticoEmGuerraDeclaradaEhAutorizado(string turretTypeName)
    {
        SetRelation("guerraDeclarada", true);
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 3, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.True);
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void AlvoDaMesmaEquipeEhRecusado(string turretTypeName)
    {
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 1, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False);
    }

    [Test]
    public void AttackExplicitoEmPazPreservaPrioridadeDepoisDoMovimento()
    {
        BuildTemporaryNavMesh();

        Type controlType = ResolveType("ControleUnidade");
        Type turretType = ResolveType("ControleTorreta");
        Type orderType = ResolveType("Hegemonia.RTS.RTSOrderType");
        Type commandType = ResolveType("Hegemonia.RTS.RTSOrderCommand");
        Type dispatcherType = ResolveType("Hegemonia.RTS.RTSOrderDispatcher");
        Assert.That(controlType, Is.Not.Null);
        Assert.That(turretType, Is.Not.Null);
        Assert.That(orderType, Is.Not.Null);
        Assert.That(commandType, Is.Not.Null);
        Assert.That(dispatcherType, Is.Not.Null);

        GameObject shooter = Track(new GameObject("IA03 explicit attack shooter"));
        NavMeshAgent agent = shooter.AddComponent<NavMeshAgent>();
        agent.speed = 4f;
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetField(shooterIdentity, "teamID", 1);

        GameObject turretObject = Track(new GameObject("IA03 explicit attack turret"));
        turretObject.transform.SetParent(shooter.transform, false);
        Component turret = turretObject.AddComponent(turretType);
        SetField(turret, "minhaIdentidade", shooterIdentity);
        SetField(turret, "meuTime", 1);
        SetField(turret, "alcance", 20f);
        SetField(turret, "souAntiAereo", false);
        SetField(turret, "exigeGuerraDeclarada", false);
        SetField(turret, "exigeHostilidadeNaBuscaAutomatica", true);

        Component control = shooter.AddComponent(controlType);

        GameObject target = Track(new GameObject("IA03 explicit attack target"));
        target.transform.position = new Vector3(4f, 0f, 0f);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);

        Assert.That(agent.isOnNavMesh, Is.True, "A unidade de teste precisa começar na malha temporária.");
        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False,
            "O alvo não deve ser autorizado pela aquisição automática durante a paz.");

        ConstructorInfo commandConstructor = commandType.GetConstructor(new[] { orderType, typeof(Vector3), typeof(Transform) });
        object command = commandConstructor.Invoke(new[]
        {
            Enum.Parse(orderType, "Attack"),
            (object)target.transform.position,
            target.transform
        });
        MethodInfo execute = dispatcherType.GetMethod("Execute", StaticMembers);
        Assert.That(execute, Is.Not.Null);
        Assert.That((bool)execute.Invoke(null, new[] { control, command }), Is.True,
            "O despacho Attack deve aceitar o movimento para o alvo.");
        Assert.That(ReadField(turret, "alvoPrioritario"), Is.SameAs(target.transform),
            "A ordem de movimento não pode apagar o alvo explícito.");

        MethodInfo search = turretType.GetMethod("ProcurarAlvo", InstanceMembers);
        Assert.That(search, Is.Not.Null);
        search.Invoke(turret, null);
        Assert.That(ReadField(turret, "alvoAtual"), Is.SameAs(target.transform),
            "O alvo explícito precisa chegar ao caminho ofensivo sem passar pelo gate automático.");
    }

    private Component CreateGate(string turretTypeName, int targetTeam, out Component targetIdentity)
    {
        Type turretType = ResolveType(turretTypeName);
        Assert.That(turretType, Is.Not.Null, "Tipo não encontrado: " + turretTypeName);

        GameObject shooter = Track(new GameObject("IA03 gate shooter " + turretTypeName));
        shooter.SetActive(false);
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetField(shooterIdentity, "teamID", 1);
        Component turret = shooter.AddComponent(turretType);
        if (turretTypeName == "ControleTorreta")
        {
            SetField(turret, "minhaIdentidade", shooterIdentity);
        }
        SetField(turret, "meuTime", 1);
        SetField(turret, "exigeGuerraDeclarada", false);
        SetField(turret, "exigeHostilidadeNaBuscaAutomatica", true);

        GameObject target = Track(new GameObject("IA03 gate target " + targetTeam));
        target.SetActive(false);
        targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", targetTeam);
        return turret;
    }

    private bool InvokeAutomaticGate(Component turret, Component targetIdentity)
    {
        MethodInfo gate = turret.GetType().GetMethod("PodeAtacarAutomaticamente", InstanceMembers);
        Assert.That(gate, Is.Not.Null, "PodeAtacarAutomaticamente não foi encontrado em " + turret.GetType().Name);
        return (bool)gate.Invoke(turret, new[] { targetIdentity });
    }

    private void SetRelation(string fieldName, object value)
    {
        object relation = governmentType.GetMethod("ObterRelacao", InstanceMembers)
            .Invoke(government, new object[] { 1, 3 });
        SetField(relation, fieldName, value);
    }

    private void BuildTemporaryNavMesh()
    {
        GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
        floor.name = "IA03 temporary NavMesh floor";
        floor.transform.localScale = Vector3.one * 10f;
        Collider floorCollider = floor.GetComponent<Collider>();
        if (floorCollider != null) floorCollider.enabled = false;

        MeshFilter meshFilter = floor.GetComponent<MeshFilter>();
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = meshFilter.sharedMesh,
                transform = floor.transform.localToWorldMatrix,
                area = 0
            }
        };
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        navMeshData = NavMeshBuilder.BuildNavMeshData(
            settings,
            sources,
            new Bounds(Vector3.zero, new Vector3(200f, 40f, 200f)),
            Vector3.zero,
            Quaternion.identity);
        Assert.That(navMeshData, Is.Not.Null, "Não foi possível construir o NavMesh temporário do teste.");
        navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
        navMeshRegistered = true;
    }

    private GameObject Track(GameObject gameObject)
    {
        objects.Add(gameObject);
        return gameObject;
    }

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(type => type != null);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + target.GetType().Name + "." + name);
        field.SetValue(target, value);
    }

    private static object ReadField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + target.GetType().Name + "." + name);
        return field.GetValue(target);
    }

    private static void ResetGlobalOrderRegistry()
    {
        Type registryType = ResolveType("OrquestradorGlobalOrdens");
        MethodInfo reset = registryType != null
            ? registryType.GetMethod("ResetRuntimeState", StaticMembers)
            : null;
        if (reset != null) reset.Invoke(null, null);
    }
}
