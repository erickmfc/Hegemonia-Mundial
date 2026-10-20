using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Verifica a autorização automática sem depender da cena de validação ou
/// salvar alterações nela. Ordens explícitas com NavMeshAgent são verificadas
/// pela fixture PlayMode, onde o agente pode se vincular à malha em runtime.
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
    public void BaixaAfinidadeHistoricaSemGuerraOuPosturaHostilNaoAutorizaDisparo(string turretTypeName)
    {
        SetRelation("valor", -85);
        SetRelation("valorBaseHistorico", -85);
        SetRelation("baseHistoricaInicializada", true);
        SetRelation("posturaAParaB", Enum.Parse(ResolveType("PosturaRelacaoPais"), "Neutro"));
        SetRelation("guerraDeclarada", false);
        SetRelation("cessarFogoAtivo", false);
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 3, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False,
            "Opinião/base histórica não podem substituir postura operacional ou guerra formal.");
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void PosturaHostilExplicitaAutorizaSemGuerra(string turretTypeName)
    {
        SetRelation("guerraDeclarada", false);
        SetRelation("posturaAParaB", Enum.Parse(ResolveType("PosturaRelacaoPais"), "Inimigo"));
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 3, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.True,
            "A postura operacional explicitamente hostil continua autorizando combate automático.");
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void CessarFogoAceitoBloqueiaTiroMesmoComPosturaHostil(string turretTypeName)
    {
        SetRelation("guerraDeclarada", true);
        SetRelation("posturaAParaB", Enum.Parse(ResolveType("PosturaRelacaoPais"), "Inimigo"));
        object ceasefire = Activator.CreateInstance(ResolveType("PropostaInternacional"));
        SetField(ceasefire, "tipo", Enum.Parse(ResolveType("TipoPropostaInternacional"), "CessarFogo"));
        SetField(ceasefire, "origemTeamId", 1);
        SetField(ceasefire, "alvoTeamId", 3);
        MethodInfo execute = governmentType.GetMethod("ExecutarProposta", InstanceMembers);
        Assert.That(execute, Is.Not.Null);
        object[] arguments = { ceasefire, string.Empty };
        Assert.That((bool)execute.Invoke(government, arguments), Is.True, Convert.ToString(arguments[1]));
        Assert.That(GetFieldValue(governmentType.GetMethod("ObterRelacao", InstanceMembers)
            .Invoke(government, new object[] { 1, 3 }), "cessarFogoAtivo"), Is.EqualTo(true));

        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 3, out targetIdentity);
        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False,
            "O cessar-fogo tem precedência sobre guerra anterior e postura hostil persistida.");
    }

    [Test]
    public void AtaqueDeMissaoIA03RevalidaGuerraNoDespacho()
    {
        GameObject target = Track(new GameObject("IA03 strategic mission target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.SetActive(true);

        string message;
        Assert.That(ExecuteStrategicAttack(target.transform, 3, true, "IA03EstrategaNacional", out message), Is.False);
        Assert.That(message, Does.Contain("guerra formal ausente"));

        SetRelation("guerraDeclarada", true);
        Assert.That(ExecuteStrategicAttack(target.transform, 3, true, "IA03EstrategaNacional", out message), Is.False);
        Assert.That(message, Is.EqualTo("sem unidades"), "Com guerra válida o despacho deve passar pelo gate diplomático.");

        SetRelation("cessarFogoAtivo", true);
        Assert.That(ExecuteStrategicAttack(target.transform, 3, true, "IA03EstrategaNacional", out message), Is.False);
        Assert.That(message, Does.Contain("cessar-fogo ativo"));
    }

    [Test]
    public void AtaqueDeMissaoIA03RecusaAlvoComDonoAlterado()
    {
        SetRelation("guerraDeclarada", true);
        GameObject target = Track(new GameObject("IA03 transferred strategic target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 4);
        target.SetActive(true);

        string message;
        Assert.That(ExecuteStrategicAttack(target.transform, 3, true, "IA03EstrategaNacional", out message), Is.False);
        Assert.That(message, Does.Contain("identidade atual do alvo"));
    }

    [Test]
    public void AtaqueComAlvoIdentificadoDeOutraOrigemTambemExigeGuerraFormal()
    {
        GameObject target = Track(new GameObject("non-IA03 attack target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.SetActive(true);

        string message;
        Assert.That(ExecuteStrategicAttack(target.transform, 3, false, "IA_TacticalDirector", out message), Is.False);
        Assert.That(message, Does.Contain("guerra formal ausente"));

        SetRelation("guerraDeclarada", true);
        Assert.That(ExecuteStrategicAttack(target.transform, 3, false, "IA_TacticalDirector", out message), Is.False);
        Assert.That(message, Is.EqualTo("sem unidades"),
            "Com guerra válida, o alvo identificado deve passar pelo gate e alcançar a validação de unidades.");

        SetRelation("cessarFogoAtivo", true);
        Assert.That(ExecuteStrategicAttack(target.transform, 3, false, "IA_TacticalDirector", out message), Is.False);
        Assert.That(message, Does.Contain("cessar-fogo ativo"));
    }

    [Test]
    public void AtaqueLegadoSomentePorCoordenadasPreservaFluxoSemIdentidadeDeAlvo()
    {
        string message;
        Assert.That(ExecuteStrategicAttack(null, 0, false, "IA_TacticalDirector", out message), Is.False);
        Assert.That(message, Is.EqualTo("sem unidades"),
            "Sem Transform/ID de alvo, o payload legado por coordenadas não usa o gate de identidade.");
    }

    [Test]
    public void AtaqueComEquipeObservadaMasTransformDestruidoNaoViraFallbackPorCoordenadas()
    {
        string message;
        Assert.That(ExecuteStrategicAttack(null, 3, false, "IA_TacticalDirector", out message), Is.False);
        Assert.That(message, Does.Contain("identidade do alvo inválida"),
            "A equipe observada deve impedir que um alvo destruído/descarregado degrade para ataque livre por coordenadas.");
    }

    [Test]
    public void AtaqueEstrategicoPorCoordenadasExigeEquipeConhecidaGuerraEAusenciaDeCessarFogo()
    {
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Assert.That(brainType, Is.Not.Null);
        MethodInfo createCoordinatePayload = brainType.GetMethod("CreateCoordinateAttackOrderData", StaticMembers);
        Assert.That(createCoordinatePayload, Is.Not.Null);
        object coordinatePayload = createCoordinatePayload.Invoke(null, new object[] { Vector3.one, 3 });
        Assert.That(GetFieldValue(coordinatePayload, "Target"), Is.Null);
        Assert.That(GetFieldValue(coordinatePayload, "RequiresDeclaredWar"), Is.EqualTo(true));
        Assert.That(GetFieldValue(coordinatePayload, "TargetTeamId"), Is.EqualTo(3));
        Assert.That(GetFieldValue(coordinatePayload, "AllowCoordinateAttackAgainstKnownEnemy"), Is.EqualTo(true));

        GameObject brainObject = Track(new GameObject("team-scoped coordinate attack contract"));
        brainObject.SetActive(false);
        Component brain = brainObject.AddComponent(brainType);
        SetField(brain, "TeamId", 1);
        MethodInfo issueCoordinateAttack = brainType.GetMethod("TryIssueAttackAtCoordinateAgainstTeam", InstanceMembers);
        Assert.That(issueCoordinateAttack, Is.Not.Null);
        Assert.That((bool)issueCoordinateAttack.Invoke(brain,
            new object[] { 1, "missing_enemy", Vector3.one, 0, 100 }), Is.False);
        Assert.That((bool)issueCoordinateAttack.Invoke(brain,
            new object[] { 1, "self_target", Vector3.one, 1, 100 }), Is.False);

        string message;
        Assert.That(ExecuteStrategicAttack(
            null, 3, true, "IA03EstrategaNacional", out message, allowCoordinateAttackAgainstKnownEnemy: true), Is.False);
        Assert.That(message, Does.Contain("guerra formal ausente"));

        SetRelation("guerraDeclarada", true);
        Assert.That(ExecuteStrategicAttack(
            null, 3, true, "IA03EstrategaNacional", out message, allowCoordinateAttackAgainstKnownEnemy: true), Is.False);
        Assert.That(message, Is.EqualTo("sem unidades"),
            "Com guerra formal válida, o ataque por coordenadas deve passar pelo gate diplomático.");

        SetRelation("cessarFogoAtivo", true);
        Assert.That(ExecuteStrategicAttack(
            null, 3, true, "IA03EstrategaNacional", out message, allowCoordinateAttackAgainstKnownEnemy: true), Is.False);
        Assert.That(message, Does.Contain("cessar-fogo ativo"));
    }

    [TestCase("Hegemonia.AI.BrainMaster.IA_TacticalDirector")]
    [TestCase("Hegemonia.AI.BrainMaster.IA_AirDirector")]
    [TestCase("Hegemonia.AI.BrainMaster.IA_NavalDirector")]
    public void DiretorPropagaEquipeObservadaEGateSomenteParaAtaqueComAlvo(string directorTypeName)
    {
        Type directorType = ResolveType(directorTypeName);
        Type contextType = ResolveType("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Assert.That(directorType, Is.Not.Null);
        Assert.That(contextType, Is.Not.Null);
        Assert.That(queueType, Is.Not.Null);

        GameObject target = Track(new GameObject("BrainMaster targeted attack identity"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.SetActive(true);
        GameObject attacker = Track(new GameObject("BrainMaster targeted attacker"));

        object queue = Activator.CreateInstance(queueType);
        object context = Activator.CreateInstance(contextType);
        SetField(context, "CommandQueue", queue);
        object director = Activator.CreateInstance(directorType, new[] { context });
        MethodInfo queueAttack = directorType.GetMethod("QueueAttack", InstanceMembers);
        MethodInfo tryDequeue = queueType.GetMethod("TryDequeue", InstanceMembers);
        bool airDirectorCarriesCoordinateTeam = directorTypeName == "Hegemonia.AI.BrainMaster.IA_AirDirector";
        Assert.That(queueAttack, Is.Not.Null);
        Assert.That(tryDequeue, Is.Not.Null);

        List<GameObject> attackers = new List<GameObject> { attacker };
        object[] targetedArguments = airDirectorCarriesCoordinateTeam
            ? new object[] { "targeted", attackers, target.transform, target.transform.position, 90, 1f, 0 }
            : new object[] { "targeted", attackers, target.transform, target.transform.position, 90, 1f };
        queueAttack.Invoke(director, targetedArguments);
        object[] dequeueArguments = { Time.time, null };
        Assert.That((bool)tryDequeue.Invoke(queue, dequeueArguments), Is.True);
        object targetedPayload = GetFieldValue(dequeueArguments[1], "Payload");
        Assert.That(GetFieldValue(targetedPayload, "RequiresDeclaredWar"), Is.EqualTo(true));
        Assert.That(GetFieldValue(targetedPayload, "TargetTeamId"), Is.EqualTo(3));

        object[] coordinateArguments = airDirectorCarriesCoordinateTeam
            ? new object[] { "coordinate", attackers, null, Vector3.one, 80, 1f, 0 }
            : new object[] { "coordinate", attackers, null, Vector3.one, 80, 1f };
        queueAttack.Invoke(director, coordinateArguments);
        dequeueArguments[0] = Time.time;
        dequeueArguments[1] = null;
        Assert.That((bool)tryDequeue.Invoke(queue, dequeueArguments), Is.True);
        object coordinatePayload = GetFieldValue(dequeueArguments[1], "Payload");
        Assert.That(GetFieldValue(coordinatePayload, "RequiresDeclaredWar"), Is.EqualTo(false));
        Assert.That(GetFieldValue(coordinatePayload, "TargetTeamId"), Is.EqualTo(0));

        if (airDirectorCarriesCoordinateTeam)
        {
            queueAttack.Invoke(director, new object[] { "known_coordinate", attackers, null, Vector3.one, 80, 1f, 3 });
            dequeueArguments[0] = Time.time;
            dequeueArguments[1] = null;
            Assert.That((bool)tryDequeue.Invoke(queue, dequeueArguments), Is.True);
            object knownCoordinatePayload = GetFieldValue(dequeueArguments[1], "Payload");
            Assert.That(GetFieldValue(knownCoordinatePayload, "Target"), Is.Null);
            Assert.That(GetFieldValue(knownCoordinatePayload, "RequiresDeclaredWar"), Is.EqualTo(true));
            Assert.That(GetFieldValue(knownCoordinatePayload, "TargetTeamId"), Is.EqualTo(3));
            Assert.That(GetFieldValue(knownCoordinatePayload, "AllowCoordinateAttackAgainstKnownEnemy"), Is.EqualTo(true));
        }
    }

    [Test]
    public void PayloadDeCampanhaPreservaIdentidadeEGateSemAlterarPayloadLegado()
    {
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Assert.That(brainType, Is.Not.Null);

        GameObject brainObject = Track(new GameObject("IA01-IA02 campaign attack contract test"));
        brainObject.SetActive(false);
        Component brain = brainObject.AddComponent(brainType);
        SetField(brain, "TeamId", 1);

        GameObject target = Track(new GameObject("campaign attack target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.SetActive(true);

        MethodInfo createPayload = brainType.GetMethod("CreateAttackOrderData", StaticMembers);
        Assert.That(createPayload, Is.Not.Null);
        object targetedPayload = createPayload.Invoke(null,
            new object[] { target.transform, target.transform.position, true, 3 });
        Assert.That(GetFieldValue(targetedPayload, "Target"), Is.EqualTo(target.transform));
        Assert.That(GetFieldValue(targetedPayload, "RequiresDeclaredWar"), Is.EqualTo(true));
        Assert.That(GetFieldValue(targetedPayload, "TargetTeamId"), Is.EqualTo(3));

        object legacyPayload = createPayload.Invoke(null,
            new object[] { null, Vector3.one, false, 0 });
        Assert.That(GetFieldValue(legacyPayload, "Target"), Is.Null);
        Assert.That(GetFieldValue(legacyPayload, "RequiresDeclaredWar"), Is.EqualTo(false));
        Assert.That(GetFieldValue(legacyPayload, "TargetTeamId"), Is.EqualTo(0));

        MethodInfo attackAgainstTeam = brainType.GetMethod("TryIssueAttackAgainstTeam", InstanceMembers);
        Assert.That(attackAgainstTeam, Is.Not.Null);
        Assert.That((bool)attackAgainstTeam.Invoke(brain,
            new object[] { 1, "campaign_targeted", null, 3, 1000 }), Is.False);
        Assert.That((bool)attackAgainstTeam.Invoke(brain,
            new object[] { 1, "campaign_targeted", target.transform, 0, 1000 }), Is.False);
        Assert.That((bool)attackAgainstTeam.Invoke(brain,
            new object[] { 1, "campaign_targeted", target.transform, 1, 1000 }), Is.False);
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, true)]
    public void TorretaAntiaereaSoContaComoArmaOfensivaContraAlvoAereo(
        bool alvoAereo,
        bool modoPassivo,
        bool esperado)
    {
        Type turretType = ResolveType("TorretaAntiaerea");
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Assert.That(turretType, Is.Not.Null);
        Assert.That(strategistType, Is.Not.Null);

        GameObject unit = Track(new GameObject("IA03 anti-air-only unit"));
        unit.SetActive(false);
        Component turret = unit.AddComponent(turretType);
        GameObject projectile = Track(new GameObject("IA03 anti-air projectile marker"));
        GameObject muzzle = Track(new GameObject("IA03 anti-air muzzle"));
        muzzle.transform.SetParent(unit.transform, false);
        SetField(turret, "modoPassivo", modoPassivo);
        SetField(turret, "capacidadeCartucho", 10);
        SetField(turret, "prefabProjetil", projectile);
        SetField(turret, "pontosDeDisparo", new[] { muzzle.transform });

        MethodInfo hasOffensiveCapability = strategistType.GetMethod("PossuiCapacidadeOfensiva", StaticMembers);
        Assert.That(hasOffensiveCapability, Is.Not.Null);

        bool actual = (bool)hasOffensiveCapability.Invoke(null, new object[] { unit, alvoAereo });
        Assert.That(actual, Is.EqualTo(esperado));
    }

    [TestCase("ControleTorreta", false, false)]
    [TestCase("ControleTorreta", true, true)]
    [TestCase("ControleTorretaModular", false, false)]
    [TestCase("ControleTorretaModular", true, true)]
    public void TorretasControladasComoAntiAereasSoContamContraAlvoAereo(
        string turretTypeName,
        bool alvoAereo,
        bool esperado)
    {
        Type turretType = ResolveType(turretTypeName);
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Assert.That(turretType, Is.Not.Null);
        Assert.That(strategistType, Is.Not.Null);

        GameObject unit = Track(new GameObject("IA03 configured anti-air turret unit"));
        unit.SetActive(false);
        Component turret = unit.AddComponent(turretType);
        SetField(turret, "souAntiAereo", true);

        GameObject projectile = Track(new GameObject("IA03 configured anti-air projectile marker"));
        GameObject muzzle = Track(new GameObject("IA03 configured anti-air muzzle"));
        muzzle.transform.SetParent(unit.transform, false);

        if (turretTypeName == "ControleTorreta")
        {
            SetField(turret, "tamanhoCartucho", 10);
            SetField(turret, "balasAtuais", 10);
            SetField(turret, "municaoPrefab", projectile);
            SetField(turret, "locaisDoTiro", new[] { muzzle.transform });
        }
        else
        {
            Type moduloType = ResolveType("ModuloArma");
            Assert.That(moduloType, Is.Not.Null);
            object modulo = Activator.CreateInstance(moduloType);
            SetField(modulo, "tamanhoCartucho", 10);
            SetField(modulo, "municaoAtual", 10);
            SetField(modulo, "municaoPrefab", projectile);
            SetField(modulo, "pontosDisparo", new[] { muzzle.transform });

            Type listaArmasType = typeof(List<>).MakeGenericType(moduloType);
            object listaArmas = Activator.CreateInstance(listaArmasType);
            listaArmasType.GetMethod("Add").Invoke(listaArmas, new[] { modulo });
            SetField(turret, "armas", listaArmas);
        }

        MethodInfo hasOffensiveCapability = strategistType.GetMethod("PossuiCapacidadeOfensiva", StaticMembers);
        Assert.That(hasOffensiveCapability, Is.Not.Null);

        bool actual = (bool)hasOffensiveCapability.Invoke(null, new object[] { unit, alvoAereo });
        Assert.That(actual, Is.EqualTo(esperado));
    }

    [Test]
    public void SubmarinoNaoRegistraAlvoAutomaticoSemGuerraFormal()
    {
        Type submarineType = ResolveType("ControleSubmarino");
        Assert.That(submarineType, Is.Not.Null);

        GameObject shooter = Track(new GameObject("IA03 automatic submarine shooter"));
        shooter.SetActive(false);
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetField(shooterIdentity, "teamID", 1);
        Component submarine = shooter.AddComponent(submarineType);
        SetField(submarine, "alcanceMisseis", 1000f);

        GameObject target = Track(new GameObject("IA03 automatic submarine target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.transform.position = shooter.transform.position + Vector3.forward * 100f;

        shooter.SetActive(true);
        target.SetActive(true);

        MethodInfo register = submarineType.GetMethod(
            "TentarRegistrarAlvoAutomatico",
            InstanceMembers);
        Assert.That(register, Is.Not.Null);

        Assert.That(register.Invoke(submarine, new object[] { target.transform, 1, false, 1000f }), Is.EqualTo(false));

        SetRelation("guerraDeclarada", true);
        Assert.That(register.Invoke(submarine, new object[] { target.transform, 1, false, 1000f }), Is.EqualTo(true));
    }

    [Test]
    public void LancamentoCoordenadoAutomaticoDeSubmarinoExigeGuerraFormal()
    {
        Type submarineType = ResolveType("ControleSubmarino");
        Assert.That(submarineType, Is.Not.Null);

        GameObject shooter = Track(new GameObject("IA03 coordinated submarine shooter"));
        shooter.SetActive(false);
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetField(shooterIdentity, "teamID", 1);
        Component submarine = shooter.AddComponent(submarineType);
        SetField(submarine, "minhaIdentidade", shooterIdentity);
        SetField(submarine, "modoAtual", Enum.Parse(submarineType.GetNestedType("ModoOperacao", InstanceMembers), "Automatico"));
        SetField(submarine, "misseisDisponiveis", 1);
        SetField(submarine, "alcanceMisseis", 1000f);
        GameObject missilePrefab = Track(new GameObject("IA03 submarine missile marker"));
        SetField(submarine, "prefabMisselSubmarino", missilePrefab);

        GameObject target = Track(new GameObject("IA03 coordinated submarine target"));
        target.SetActive(false);
        Component targetIdentity = target.AddComponent(identityType);
        SetField(targetIdentity, "teamID", 3);
        target.transform.position = shooter.transform.position + Vector3.forward * 100f;

        shooter.SetActive(true);
        target.SetActive(true);

        MethodInfo gate = submarineType.GetMethod("PodeLancarCoordenado", InstanceMembers);
        Assert.That(gate, Is.Not.Null);
        object[] arguments = { target.transform.position, target.transform, true, string.Empty };

        Assert.That((bool)gate.Invoke(submarine, arguments), Is.False,
            "O lançamento coordenado automático não pode iniciar agressão sem guerra formal.");

        SetRelation("guerraDeclarada", true);
        arguments[3] = string.Empty;
        Assert.That((bool)gate.Invoke(submarine, arguments), Is.True,
            Convert.ToString(arguments[3]));
    }

    [Test]
    public void DefinirPosturaNaoAlteraOpiniaoAtualNemBaseHistorica()
    {
        Type countryType = ResolveType("DadosPaisGoverno");
        IList countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
        object countryOne = Activator.CreateInstance(countryType);
        object countryThree = Activator.CreateInstance(countryType);
        SetField(countryOne, "teamId", 1);
        SetField(countryOne, "nomePais", "País Um");
        SetField(countryThree, "teamId", 3);
        SetField(countryThree, "nomePais", "País Três");
        countries.Add(countryOne);
        countries.Add(countryThree);

        object relation = governmentType.GetMethod("ObterRelacao", InstanceMembers)
            .Invoke(government, new object[] { 1, 3 });
        SetField(relation, "valor", -35);
        SetField(relation, "valorBaseHistorico", -72);
        SetField(relation, "baseHistoricaInicializada", true);
        MethodInfo setPosture = governmentType.GetMethod("DefinirPostura", InstanceMembers);
        object[] arguments = { 1, 3, Enum.Parse(ResolveType("PosturaRelacaoPais"), "Inimigo"), string.Empty };

        Assert.That((bool)setPosture.Invoke(government, arguments), Is.True, Convert.ToString(arguments[3]));
        Assert.That(GetFieldValue(relation, "posturaAParaB").ToString(), Is.EqualTo("Inimigo"));
        Assert.That(GetFieldValue(relation, "valor"), Is.EqualTo(-35));
        Assert.That(GetFieldValue(relation, "valorBaseHistorico"), Is.EqualTo(-72));
    }

    [Test]
    public void MigracaoDeRelacaoLegadaGuardaBaseSemCriarHostilidadeOperacional()
    {
        Type relationType = ResolveType("RelacaoPaisGoverno");
        object relation = Activator.CreateInstance(relationType);
        SetField(relation, "teamA", 2);
        SetField(relation, "teamB", 4);
        SetField(relation, "valor", -64);
        SetField(relation, "posturaAParaB", Enum.Parse(ResolveType("PosturaRelacaoPais"), "Inimigo"));
        SetField(relation, "posturaBParaA", Enum.Parse(ResolveType("PosturaRelacaoPais"), "Inimigo"));
        IList relations = (IList)governmentType.GetField("relacoes", InstanceMembers).GetValue(government);
        relations.Add(relation);

        MethodInfo migration = governmentType.GetMethod("GarantirBasesHistoricas", InstanceMembers);
        Assert.That(migration, Is.Not.Null);
        migration.Invoke(government, new object[] { true });

        Assert.That(GetFieldValue(relation, "baseHistoricaInicializada"), Is.EqualTo(true));
        Assert.That(GetFieldValue(relation, "valorBaseHistorico"), Is.EqualTo(-64));
        Assert.That(GetFieldValue(relation, "posturaAParaB").ToString(), Is.EqualTo("Neutro"));
        Assert.That(GetFieldValue(relation, "posturaBParaA").ToString(), Is.EqualTo("Neutro"));
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void AlvoDaMesmaEquipeEhRecusado(string turretTypeName)
    {
        Component targetIdentity;
        Component turret = CreateGate(turretTypeName, 1, out targetIdentity);

        Assert.That(InvokeAutomaticGate(turret, targetIdentity), Is.False);
    }

    [TestCase("ControleTorreta")]
    [TestCase("ControleTorretaModular")]
    public void AlvoSemIdentidadeMarcadoComoInimigoNaoEhSelecionadoAutomaticamente(string turretTypeName)
    {
        Type turretType = ResolveType(turretTypeName);
        Assert.That(turretType, Is.Not.Null, "Tipo não encontrado: " + turretTypeName);

        GameObject shooter = Track(new GameObject("IA03 tag-only shooter " + turretTypeName));
        shooter.SetActive(false);
        shooter.transform.position = new Vector3(1000000f, 0f, 1000000f);
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetField(shooterIdentity, "teamID", 1);
        FieldInfo unitType = identityType.GetField("tipoUnidade", InstanceMembers);
        Assert.That(unitType, Is.Not.Null);
        SetField(shooterIdentity, "tipoUnidade", Enum.Parse(unitType.FieldType, "Aereo"));
        Component turret = shooter.AddComponent(turretType);
        shooter.SetActive(true);

        SetField(turret, "meuTime", 1);
        SetField(turret, "exigeGuerraDeclarada", false);
        SetField(turret, "souAntiAereo", true);
        if (turretTypeName == "ControleTorreta")
        {
            SetField(turret, "minhaIdentidade", shooterIdentity);
            SetField(turret, "alcance", 50f);
        }
        else
        {
            SetField(turret, "minhaRaiz", shooter.transform);
            SetField(turret, "alcanceRadar", 50f);
        }

        GameObject target = Track(new GameObject("IA03 aviao sem identidade"));
        target.transform.position = shooter.transform.position + new Vector3(0f, 10f, 1f);
        target.tag = "Inimigo";
        target.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        MethodInfo search = turretType.GetMethod("ProcurarAlvo", InstanceMembers);
        FieldInfo currentTarget = turretType.GetField("alvoAtual", InstanceMembers);
        Assert.That(search, Is.Not.Null, "ProcurarAlvo não encontrado em " + turretTypeName);
        Assert.That(currentTarget, Is.Not.Null, "alvoAtual não encontrado em " + turretTypeName);

        search.Invoke(turret, null);

        Assert.That(currentTarget.GetValue(turret), Is.Null,
            "Uma tag sem IdentidadeUnidade não comprova hostilidade diplomática.");
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

    private bool ExecuteStrategicAttack(
        Transform target,
        int targetTeamId,
        bool requiresDeclaredWar,
        string origin,
        out string message,
        bool allowCoordinateAttackAgainstKnownEnemy = false)
    {
        Type bridgeType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Assert.That(bridgeType, Is.Not.Null);
        object bridge = Activator.CreateInstance(bridgeType, new object[] { 1 });
        object commandService = bridgeType.GetProperty("CommandService", InstanceMembers).GetValue(bridge, null);

        Type payloadType = ResolveType("Hegemonia.AI.BrainMaster.IA_AttackOrderData");
        object payload = Activator.CreateInstance(payloadType);
        SetField(payload, "Target", target);
        SetField(payload, "TargetTeamId", targetTeamId);
        SetField(payload, "RequiresDeclaredWar", requiresDeclaredWar);
        SetField(payload, "AllowCoordinateAttackAgainstKnownEnemy", allowCoordinateAttackAgainstKnownEnemy);

        Type requestType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        object request = Activator.CreateInstance(requestType);
        SetField(request, "Origin", origin);
        SetField(request, "Type", Enum.Parse(ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType"), "Attack"));
        SetField(request, "Payload", payload);

        MethodInfo execute = commandService.GetType().GetMethod("Execute", InstanceMembers);
        Assert.That(execute, Is.Not.Null);
        object[] arguments = { request, null, null };
        bool result = (bool)execute.Invoke(commandService, arguments);
        message = Convert.ToString(arguments[2]);
        return result;
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

    private static object GetFieldValue(object target, string name)
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
