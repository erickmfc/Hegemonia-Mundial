using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Reproduz o disparo automático do caça real em paz, usando a cena de
/// validação isolada e deixando o Update do lançador executar normalmente.
/// </summary>
public sealed class IA03AerialCombatIntegrationPlayModeTests
{
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const string FighterPrefabPath = "Assets/Prefabs/Aeroporto/f16b/F-16D Bloco 60 Falcão.prefab";
    private const string G18PrefabPath = "Assets/Prefabs/Aeroporto/G18/G18M.prefab";
    private const string GunFighterPrefabPath = "Assets/Prefabs/Aeroporto/A_20/A10.prefab";
    private const string HasafPrefabPath = "Assets/Prefabs/Aeroporto/Drone Hasaf/DH_Hasaf.prefab";
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject fighter;
    private GameObject target;
    private GameObject attackMenuForTargetPick;
    private GameObject attackOrdersForTest;
    private GameObject navalAttacker;
    private GameObject navalTarget;
    private Component unitController;
    private object government;
    private object relation;
    private bool sceneHandlerSubscribed;
    private bool explicitAirOrderIssued;
    private object[] diplomacyBefore;
    private readonly HashSet<int> activeMissilesBefore = new HashSet<int>();
    private readonly List<Component> missilesSpawnedByTest = new List<Component>();
    private readonly HashSet<int> activeGunProjectilesBefore = new HashSet<int>();
    private readonly List<Component> gunProjectilesSpawnedByTest = new List<Component>();
    private bool trackingGunProjectiles;
    private readonly HashSet<int> activeTacticalMissilesBefore = new HashSet<int>();
    private readonly List<Component> tacticalMissilesSpawnedByTest = new List<Component>();
    private bool trackingTacticalMissiles;
    private Component tacticalMissileSpawnedByTest;
    private readonly HashSet<int> activeNavalMissilesBefore = new HashSet<int>();
    private readonly List<Component> navalMissilesSpawnedByTest = new List<Component>();
    private readonly Dictionary<Behaviour, bool> sceneTurretsBefore = new Dictionary<Behaviour, bool>();

    [UnityTest]
    public IEnumerator CacaRealNaoDisparaAutomaticamenteEmPazNeutra()
    {
        yield return LoadValidationSceneOnly();
        SetNeutralDiplomacy();
        CacheActiveMissiles();
        CreateRealFighterAndTarget();

        Component launcher = fighter.GetComponent(ResolveType("LancadorMisselCaca"));
        Component flight = fighter.GetComponent(ResolveType("ControleAviao"));
        Component unitController = fighter.GetComponent(ResolveType("ControleUnidade"));
        Assert.That(launcher, Is.Not.Null);
        Assert.That(flight, Is.Not.Null);
        Assert.That(unitController, Is.Not.Null);

        SetMember(flight, "estadoAtual", Enum.Parse(GetMemberType(flight, "estadoAtual"), "EmMissao"));
        MethodInfo setCombatMode = unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers,
            null, new[] { typeof(bool) }, null);
        Assert.That(setCombatMode, Is.Not.Null);
        setCombatMode.Invoke(unitController, new object[] { true });

        // Deixa Start registrar identidades e inicializar vida; o lançador e o
        // seu Update permanecem ativos durante toda a janela de observação.
        yield return null;
        yield return null;

        int ammoBefore = Convert.ToInt32(GetMember(launcher, "municaoAtual"));
        Assert.That(Convert.ToBoolean(GetMember(relation, "guerraDeclarada")), Is.False);
        Assert.That(Convert.ToString(GetMember(relation, "posturaAParaB")), Is.EqualTo("Neutro"));
        Assert.That(Convert.ToString(GetMember(relation, "posturaBParaA")), Is.EqualTo("Neutro"));

        bool warWhenPeaceWindowEnded = false;
        float deadline = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < deadline
            && Convert.ToInt32(GetMember(launcher, "municaoAtual")) == ammoBefore)
        {
            yield return null;
        }

        bool targetDetected = Convert.ToBoolean(GetMember(launcher, "TemInimigosDetectados"));
        int ammoAfter = Convert.ToInt32(GetMember(launcher, "municaoAtual"));
        warWhenPeaceWindowEnded = Convert.ToBoolean(GetMember(relation, "guerraDeclarada"));
        CollectNewMissiles();
        Debug.Log("[IA03][AEREO][PAZ_AUTO] fighter=" + fighter.name
            + " target=" + target.name
            + " relation=Neutro/semGuerra"
            + " missionState=" + GetMember(flight, "estadoAtual")
            + " explicitTarget=" + (GetMember(launcher, "_alvoIAForcado") != null)
            + " targetDetected=" + targetDetected
            + " ammo=" + ammoBefore + "->" + ammoAfter
            + " warAtObservation=" + warWhenPeaceWindowEnded
            + " frame=" + Time.frameCount);

        Assert.That(targetDetected, Is.True,
            "O teste não reproduziu o ciclo de radar do lançador; a identidade Team 3 precisa ter sido registrada.");
        Assert.That(ammoAfter, Is.EqualTo(ammoBefore),
            "O caça consumiu munição automaticamente contra uma unidade válida enquanto Team 1 e Team 3 estavam em paz neutra.");

        SetEnumMember(relation, "posturaAParaB", "Inimigo");
        government.GetType().GetMethod("NotificarGovernoAtualizado", InstanceMembers)?.Invoke(government, null);
        float hostileDeadline = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < hostileDeadline
            && Convert.ToInt32(GetMember(launcher, "municaoAtual")) == ammoBefore)
        {
            yield return null;
        }

        int ammoAfterHostility = Convert.ToInt32(GetMember(launcher, "municaoAtual"));
        CollectNewMissiles();
        Debug.Log("[IA03][AEREO][HOSTILIDADE_AUTO] target=" + target.name
            + " postureTeam1=Inimigo ammo=" + ammoBefore + "->" + ammoAfterHostility
            + " frame=" + Time.frameCount);
        Assert.That(ammoAfterHostility, Is.EqualTo(ammoBefore - 1),
            "A postura hostil válida deixou de autorizar o disparo automático do caça.");

        Component targetDamage = target.GetComponent(ResolveType("SistemaDeDanos"));
        float healthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        float impactDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < impactDeadline
            && Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) >= healthBefore)
        {
            CollectNewMissiles();
            yield return null;
        }

        float healthAfter = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        int aggressorTeam = Convert.ToInt32(GetMember(targetDamage, "ultimoAgressorTeamId"));
        Debug.Log("[IA03][AEREO][IMPACTO] hp=" + healthBefore + "->" + healthAfter
            + " aggressorTeam=" + aggressorTeam + " frame=" + Time.frameCount);
        Assert.That(healthAfter, Is.LessThan(healthBefore),
            "O míssil real foi lançado em postura hostil, mas não atingiu o alvo aéreo do teste.");
        Assert.That(aggressorTeam, Is.EqualTo(1),
            "O dano do míssil aéreo não foi atribuído à identidade Team 1 da aeronave lançadora.");
    }

    [UnityTest]
    public IEnumerator OrdemAereaExplicitaEmPazMantemPrioridadeEAplicaDano()
    {
        yield return LoadValidationSceneOnly();
        SetNeutralDiplomacy(2);
        CacheActiveMissiles();
        CreateRealFighterAndTarget(G18PrefabPath, 2);

        Component launcher = fighter.GetComponent(ResolveType("LancadorMisselCaca"));
        Component flight = fighter.GetComponent(ResolveType("ControleAviao"));
        unitController = fighter.GetComponent(ResolveType("ControleUnidade"));
        Assert.That(launcher, Is.Not.Null);
        Assert.That(flight, Is.Not.Null);
        Assert.That(unitController, Is.Not.Null);

        SetMember(flight, "estadoAtual", Enum.Parse(GetMemberType(flight, "estadoAtual"), "EmMissao"));
        ((Behaviour)flight).enabled = false; // mantém o avião estável enquanto o executor de armas roda.
        yield return null;
        yield return null;

        // Start do ControleUnidade configura o combustível da fixture. A
        // aeronave isolada fica fora das bases carregadas pela cena; preserve
        // a validação da ordem real sem testar o retorno logístico inexistente.
        Component fuel = fighter.GetComponent(ResolveType("CombustivelUnidade"));
        Assert.That(fuel, Is.Not.Null, "ControleUnidade não criou o estado de combustível esperado no caça de teste.");
        SetMember(fuel, "usaCombustivel", false);
        Assert.That(Convert.ToBoolean(GetMember(fuel, "usaCombustivel")), Is.False,
            "A fixture deve desativar apenas a reserva de combustível antes de avaliar a ordem aérea.");
        Assert.That(Convert.ToBoolean(flight.GetType().GetMethod("PodeExecutarMissaoComRetorno", InstanceMembers)
            .Invoke(flight, new object[] { target.transform.position })), Is.True,
            "O controle de voo ainda recusa a missão por autonomia da fixture.");
        Assert.That(Convert.ToString(GetMember(flight, "estadoAtual")), Is.EqualTo("EmMissao"));
        int ammoBefore = Convert.ToInt32(GetMember(launcher, "municaoAtual"));
        Component targetDamage = target.GetComponent(ResolveType("SistemaDeDanos"));
        float healthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        Assert.That(Convert.ToBoolean(GetMember(relation, "guerraDeclarada")), Is.False);
        Assert.That(Convert.ToString(GetMember(relation, "posturaAParaB")), Is.EqualTo("Neutro"));
        Assert.That(Convert.ToString(GetMember(relation, "posturaBParaA")), Is.EqualTo("Neutro"));

        attackMenuForTargetPick = new GameObject("IA03_Aerial_ManualAttack_TargetPicker");
        attackMenuForTargetPick.SetActive(false);
        Component menu = attackMenuForTargetPick.AddComponent(ResolveType("MenuComandoController"));
        MethodInfo findAttackTarget = menu.GetType().GetMethod("EncontrarUnidadeProxima", InstanceMembers,
            null, new[] { typeof(Vector3), typeof(float), typeof(bool) }, null);
        Assert.That(findAttackTarget, Is.Not.Null, "O seletor de alvo do Mapa Global não foi encontrado.");
        GameObject selectedTarget = findAttackTarget.Invoke(menu,
            new object[] { target.transform.position, 150f, true }) as GameObject;
        Assert.That(selectedTarget, Is.SameAs(target),
            "O seletor de alvo do mapa descartou a unidade IA 03 enquanto Team 1 e Team 2 estavam em paz.");

        // Evita colidir com registros terminais quando o Unity reutiliza
        // InstanceIDs entre execuções PlayMode nesta mesma sessão.
        SetMember(unitController, "sequenciaOrdemMovimento", 1000 + Time.frameCount);

        attackOrdersForTest = new GameObject("IA03_Aerial_ManualAttack_OrderExecutor");
        attackOrdersForTest.SetActive(false);
        Component orderExecutor = attackOrdersForTest.AddComponent(ResolveType("DesenharLinhasOrdem"));
        SetMember(orderExecutor, "modoAtaqueAtivo", true);
        ((IList)GetMember(orderExecutor, "_alvosModo")).Add(fighter);
        MethodInfo applyAttackOrder = orderExecutor.GetType().GetMethod("AplicarOrdemAtaqueDoMenu", InstanceMembers,
            null, new[] { typeof(Vector3), typeof(Transform) }, null);
        Assert.That(applyAttackOrder, Is.Not.Null, "O executor de ataque do Mapa Global não foi encontrado.");
        int acceptedOrders = Convert.ToInt32(applyAttackOrder.Invoke(orderExecutor,
            new object[] { selectedTarget.transform.position, selectedTarget.transform }));
        bool accepted = acceptedOrders > 0;
        explicitAirOrderIssued = accepted;

        Assert.That(accepted, Is.True, "O executor de voo recusou a ordem explícita contra o alvo de teste.");
        Assert.That(GetMember(launcher, "_alvoIAForcado"), Is.SameAs(target.transform),
            "A ordem aérea explícita não chegou ao alvo forçado do lançador.");
        Assert.That(Convert.ToBoolean(GetMember(flight, "alvoPrioritarioIA")), Is.True,
            "A aeronave não manteve o indicador de alvo prioritário após aceitar Attack.");
        Assert.That(Convert.ToBoolean(GetMember(unitController, "ModoCombateAtivo")), Is.True);
        Assert.That(Convert.ToBoolean(GetMember(relation, "guerraDeclarada")), Is.False,
            "Aceitar Attack não pode alterar a diplomacia antes de haver impacto.");

        float launchDeadline = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < launchDeadline
            && Convert.ToInt32(GetMember(launcher, "municaoAtual")) == ammoBefore)
        {
            yield return null;
        }
        int ammoAfter = Convert.ToInt32(GetMember(launcher, "municaoAtual"));
        CollectNewMissiles();
        Assert.That(ammoAfter, Is.EqualTo(ammoBefore - 1),
            "O alvo explícito da ordem não levou o lançador aéreo a disparar em paz neutra.");

        float impactDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < impactDeadline
            && Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) >= healthBefore)
        {
            CollectNewMissiles();
            yield return null;
        }

        float healthAfter = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        int aggressorTeam = Convert.ToInt32(GetMember(targetDamage, "ultimoAgressorTeamId"));
        Debug.Log("[IA03][AEREO][ATTACK_EXPLICITO] accepted=" + accepted
            + " priorityTarget=" + (GetMember(launcher, "_alvoIAForcado") as Transform)?.name
            + " ammo=" + ammoBefore + "->" + ammoAfter
            + " hp=" + healthBefore + "->" + healthAfter
            + " aggressorTeam=" + aggressorTeam
            + " warAfterImpact=" + Convert.ToBoolean(GetMember(relation, "guerraDeclarada")));
        Assert.That(healthAfter, Is.LessThan(healthBefore),
            "A ordem aérea explícita foi aceita, mas o míssil não aplicou dano real.");
        Assert.That(aggressorTeam, Is.EqualTo(1),
            "O impacto da ordem aérea não foi atribuído à identidade Team 1.");
        Assert.That(Convert.ToBoolean(GetMember(relation, "guerraDeclarada")), Is.False,
            "O primeiro impacto deve iniciar uma crise diplomática, sem declarar guerra.");
        Assert.That(Convert.ToInt32(GetMember(relation, "agressoesConfirmadasNaCrise")), Is.EqualTo(1),
            "O impacto físico precisa chegar ao contador de agressões da relação.");
    }

    [UnityTest]
    public IEnumerator CacaComMetralhadoraRespeitaAlvoExplicitoEPermanecePassivo()
    {
        yield return LoadValidationSceneOnly();
        SetNeutralDiplomacy();
        CacheActiveGunProjectiles();

#if UNITY_EDITOR
        GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunFighterPrefabPath);
        Assert.That(fighterPrefab, Is.Not.Null, "Prefab A10 com metralhadora não foi encontrado.");
        fighter = UnityEngine.Object.Instantiate(
            fighterPrefab, new Vector3(100000f, 10000f, 100000f), Quaternion.identity);
        fighter.name = "IA03_Aerial_Integration_A10_GunOnly";

        Component identity = fighter.GetComponentInChildren(ResolveType("IdentidadeUnidade"), true);
        unitController = fighter.GetComponent(ResolveType("ControleUnidade"));
        Component flight = fighter.GetComponent(ResolveType("ControleAviao"));
        Component gun = fighter.GetComponent(ResolveType("SistemaArmamentoHelice"));
        Assert.That(identity, Is.Not.Null, "O A10 real está sem IdentidadeUnidade.");
        Assert.That(unitController, Is.Not.Null, "O A10 real está sem ControleUnidade.");
        Assert.That(flight, Is.Not.Null, "O A10 real está sem ControleAviao.");
        Assert.That(gun, Is.Not.Null, "O A10 real está sem SistemaArmamentoHelice.");
        SetMember(identity, "teamID", 1);
        SetEnumMember(identity, "tipoUnidade", "Aereo");

        target = new GameObject("IA03_Aerial_Gun_Target_Team3");
        target.transform.position = fighter.transform.position + Vector3.forward * 300f;
        target.AddComponent<BoxCollider>().size = Vector3.one * 12f;
        Component targetDamage = target.AddComponent(ResolveType("SistemaDeDanos"));
        SetMember(targetDamage, "vidaMaxima", 1000f);
        SetMember(targetDamage, "vidaAtual", 1000f);
        Component targetIdentity = target.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(targetIdentity, "teamID", 3);
        SetEnumMember(targetIdentity, "tipoUnidade", "Aereo");

        yield return null;
        yield return null;

        MethodInfo setCombatMode = unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers,
            null, new[] { typeof(bool) }, null);
        Assert.That(setCombatMode, Is.Not.Null);
        setCombatMode.Invoke(unitController, new object[] { true });
        SetMember(flight, "estadoAtual", Enum.Parse(GetMemberType(flight, "estadoAtual"), "EmMissao"));
        ((Behaviour)flight).enabled = false;
        ((Behaviour)gun).enabled = false;
        SetMember(gun, "cronometroScan", 0f);
        SetMember(gun, "cronometroTiro", 0f);

        MethodInfo definePriority = unitController.GetType().GetMethod(
            "DefinirAlvoPrioritario", InstanceMembers, null, new[] { typeof(Transform) }, null);
        Assert.That(definePriority, Is.Not.Null);
        definePriority.Invoke(unitController, new object[] { target.transform });

        MethodInfo scanGun = gun.GetType().GetMethod("EscanearAreaAoRedorLento", InstanceMembers);
        Assert.That(scanGun, Is.Not.Null);
        scanGun.Invoke(gun, null);
        Assert.That(GetMember(gun, "alvoAtualGuardado"), Is.SameAs(target.transform));
        SetMember(gun, "estadoAtualAtaque", Enum.Parse(GetMemberType(gun, "estadoAtualAtaque"), "Mergulho_Atacar"));
        SetMember(gun, "cronometroScan", 10f); // Mantém o alvo recém-adquirido durante a passagem de ataque.

        MethodInfo updateGun = gun.GetType().GetMethod("Update", InstanceMembers);
        Assert.That(updateGun, Is.Not.Null);
        int ammoBeforeActive = Convert.ToInt32(GetMember(gun, "balasAtuais"));
        updateGun.Invoke(gun, null);
        CollectNewGunProjectiles();
        int ammoAfterActive = Convert.ToInt32(GetMember(gun, "balasAtuais"));

        Assert.That(GetMember(gun, "alvoAtualGuardado"), Is.SameAs(target.transform),
            "A metralhadora substituiu o alvo explícito do mapa por outro contato automático.");
        Assert.That(ammoAfterActive, Is.LessThan(ammoBeforeActive),
            "A aeronave ativa com canhão não disparou contra o alvo explícito.");
        Assert.That(gunProjectilesSpawnedByTest.Count, Is.GreaterThan(0),
            "A ordem ativa não criou um projétil real de metralhadora.");
        Assert.That(Convert.ToBoolean(GetMember(relation, "guerraDeclarada")), Is.False,
            "A ordem manual alterou a diplomacia antes de qualquer impacto.");

        setCombatMode.Invoke(unitController, new object[] { false });
        SetMember(gun, "cronometroTiro", 0f);
        int ammoBeforePassive = Convert.ToInt32(GetMember(gun, "balasAtuais"));
        updateGun.Invoke(gun, null);
        Assert.That(Convert.ToInt32(GetMember(gun, "balasAtuais")), Is.EqualTo(ammoBeforePassive),
            "A metralhadora disparou depois que a unidade foi colocada no modo passivo.");
#else
        Assert.Fail("O teste aéreo precisa executar no Editor para carregar o prefab de A10.");
#endif
    }

    [UnityTest]
    public IEnumerator DroneHasafDisparaNoAlvoManualEmPazEPermanecePassivo()
    {
        yield return LoadValidationSceneOnly();
        SetNeutralDiplomacy();
        CacheActiveTacticalMissiles();

#if UNITY_EDITOR
        GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HasafPrefabPath);
        Assert.That(dronePrefab, Is.Not.Null, "Prefab do drone Hasaf não foi encontrado.");
        fighter = UnityEngine.Object.Instantiate(
            dronePrefab, new Vector3(100000f, 10000f, 100000f), Quaternion.identity);
        fighter.name = "IA03_Aerial_Integration_Hasaf";

        Component identity = fighter.GetComponentInChildren(ResolveType("IdentidadeUnidade"), true);
        unitController = fighter.GetComponent(ResolveType("ControleUnidade"));
        Component flight = fighter.GetComponent(ResolveType("ControleAviao"));
        Component drone = fighter.GetComponent(ResolveType("ControleDroneHasaf"));
        Assert.That(identity, Is.Not.Null, "O Hasaf real está sem IdentidadeUnidade.");
        Assert.That(unitController, Is.Not.Null, "O Hasaf real está sem ControleUnidade.");
        Assert.That(flight, Is.Not.Null, "O Hasaf real está sem ControleAviao.");
        Assert.That(drone, Is.Not.Null, "O Hasaf real está sem ControleDroneHasaf.");
        SetMember(identity, "teamID", 1);
        SetEnumMember(identity, "tipoUnidade", "Aereo");

        target = new GameObject("IA03_Aerial_Hasaf_Target_Team3");
        target.transform.position = fighter.transform.position + Vector3.forward * 70f;
        target.AddComponent<BoxCollider>().size = Vector3.one * 12f;
        Component targetDamage = target.AddComponent(ResolveType("SistemaDeDanos"));
        SetMember(targetDamage, "vidaMaxima", 1000f);
        SetMember(targetDamage, "vidaAtual", 1000f);
        Component targetIdentity = target.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(targetIdentity, "teamID", 3);
        SetEnumMember(targetIdentity, "tipoUnidade", "Aereo");

        yield return null;
        yield return null;

        MethodInfo setCombatMode = unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers,
            null, new[] { typeof(bool) }, null);
        Assert.That(setCombatMode, Is.Not.Null);
        setCombatMode.Invoke(unitController, new object[] { true });
        SetMember(flight, "estadoAtual", Enum.Parse(GetMemberType(flight, "estadoAtual"), "EmMissao"));
        ((Behaviour)flight).enabled = false;
        ((Behaviour)drone).enabled = false;

        MethodInfo definePriority = unitController.GetType().GetMethod(
            "DefinirAlvoPrioritario", InstanceMembers, null, new[] { typeof(Transform) }, null);
        Assert.That(definePriority, Is.Not.Null);
        definePriority.Invoke(unitController, new object[] { target.transform });
        SetMember(drone, "cronometroRecarga", 10f);

        MethodInfo updateDrone = drone.GetType().GetMethod("Update", InstanceMembers);
        Assert.That(updateDrone, Is.Not.Null);
        updateDrone.Invoke(drone, null);
        CollectNewTacticalMissiles();
        int ammoAfterActive = Convert.ToInt32(GetMember(drone, "municaoAtual"));
        tacticalMissileSpawnedByTest = tacticalMissilesSpawnedByTest.Count > 0
            ? tacticalMissilesSpawnedByTest[tacticalMissilesSpawnedByTest.Count - 1]
            : null;

        Assert.That(GetMember(drone, "alvoSeguir"), Is.SameAs(target.transform),
            "A ordem de ataque do mapa não chegou ao controlador do Hasaf.");
        Assert.That(ammoAfterActive, Is.EqualTo(3),
            "O Hasaf ativo não consumiu uma munição contra o alvo manual em paz.");
        Assert.That(tacticalMissileSpawnedByTest, Is.Not.Null,
            "O Hasaf não criou um míssil tático real usando a saída de fallback do próprio avião.");

        setCombatMode.Invoke(unitController, new object[] { false });
        SetMember(drone, "cronometroRecarga", 10f);
        int ammoBeforePassive = Convert.ToInt32(GetMember(drone, "municaoAtual"));
        drone.GetType().GetMethod("DispararMissil", InstanceMembers)
            ?.Invoke(drone, new object[] { target.transform });
        updateDrone.Invoke(drone, null);
        Assert.That(Convert.ToInt32(GetMember(drone, "municaoAtual")), Is.EqualTo(ammoBeforePassive),
            "O Hasaf disparou um míssil depois que a unidade foi colocada no modo passivo.");
#else
        Assert.Fail("O teste aéreo precisa executar no Editor para carregar o prefab do Hasaf.");
#endif
    }

    [UnityTest]
    public IEnumerator LancadorNavalCoordenadoBloqueiaPazEDestruiAlvoComMissilReal()
    {
        yield return LoadValidationSceneOnly();
        SetNeutralDiplomacy();
        CacheActiveNavalMissiles();

        Vector3 origin = new Vector3(100000f, 10000f, 100000f);
        navalAttacker = new GameObject("IA03_Naval_Integration_Attacker_Team1");
        navalAttacker.transform.SetPositionAndRotation(origin, Quaternion.identity);
        Component attackerIdentity = navalAttacker.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(attackerIdentity, "teamID", 1);
        SetEnumMember(attackerIdentity, "tipoUnidade", "Naval");
        unitController = navalAttacker.AddComponent(ResolveType("ControleUnidade"));
        Component launcher = navalAttacker.AddComponent(ResolveType("LancadorNaval"));

        navalTarget = new GameObject("IA03_Naval_Integration_Target_Team3");
        navalTarget.transform.position = origin + Vector3.forward * 300f;
        BoxCollider targetCollider = navalTarget.AddComponent<BoxCollider>();
        targetCollider.size = Vector3.one * 32f;
        Component targetDamage = navalTarget.AddComponent(ResolveType("SistemaDeDanos"));
        SetMember(targetDamage, "vidaMaxima", 400f);
        Component targetIdentity = navalTarget.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(targetIdentity, "teamID", 3);
        SetEnumMember(targetIdentity, "tipoUnidade", "Naval");

#if UNITY_EDITOR
        GameObject realMissilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Navios_Guerra/Efeitos/N-02 Tridente.prefab");
        Assert.That(realMissilePrefab, Is.Not.Null, "Prefab naval real N-02 Tridente não foi encontrado.");
        SetMember(launcher, "prefabMissel", realMissilePrefab);
#else
        Assert.Fail("O teste naval precisa executar no Editor para carregar o prefab real.");
        yield break;
#endif
        SetMember(launcher, "prefabTorpedo", null);
        SetMember(launcher, "municaoTotal", 1);
        SetMember(launcher, "municaoMaxima", 1);
        SetMember(launcher, "tirosPorSalva", 1);
        SetMember(launcher, "alcanceRadar", 1000f);
        SetMember(launcher, "maxMisseisSimultaneosPorAlvo", 1);

        yield return null;
        yield return null;

        unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers)
            .Invoke(unitController, new object[] { true });
        SetEnumMember(launcher, "modoAtual", "Automatico");
        SetMember(launcher, "tempoParaAtivarAutomatico", Time.time);
        SetMember(launcher, "tempoRecargaSalva", 0f);
        SetMember(launcher, "tempoUltimoDisparo", Time.time - 10f);
        SetMember(launcher, "tempoUltimoDisparoConfirmado", -1f);
        SetMember(launcher, "salvaEmAndamento", false);
        MethodInfo configureAuthorizedTargets = launcher.GetType().GetMethod(
            "DefinirAlvosAutorizados", InstanceMembers);
        Assert.That(configureAuthorizedTargets, Is.Not.Null);
        configureAuthorizedTargets.Invoke(launcher, new object[] { new List<Transform>(), true });

        MethodInfo canLaunch = launcher.GetType().GetMethod("PodeLancarCoordenado", InstanceMembers);
        MethodInfo launch = launcher.GetType().GetMethod("TentarLancarCoordenado", InstanceMembers);
        Assert.That(canLaunch, Is.Not.Null);
        Assert.That(launch, Is.Not.Null);
        object[] validation = { navalTarget.transform.position, navalTarget.transform, true, string.Empty };
        bool allowedDuringPeace = Convert.ToBoolean(canLaunch.Invoke(launcher, validation));
        string peaceReason = Convert.ToString(validation[3]);
        Assert.That(allowedDuringPeace, Is.False,
            "O lançador naval automático aceitou disparar enquanto Team 1 e Team 3 estavam em paz.");
        StringAssert.Contains("guerra", peaceReason,
            "O bloqueio em paz deve vir da autorização diplomática, não de recarga, alcance ou modo.");

        SetMember(relation, "guerraDeclarada", true);
        government.GetType().GetMethod("NotificarGovernoAtualizado", InstanceMembers)?.Invoke(government, null);
        validation[3] = string.Empty;
        bool allowedDuringWar = Convert.ToBoolean(canLaunch.Invoke(launcher, validation));
        string warReason = Convert.ToString(validation[3]);
        Assert.That(allowedDuringWar, Is.True,
            "A guerra declarada não autorizou o lançador naval: " + warReason);

        SetMember(targetDamage, "vidaAtual", 400f);
        float healthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        int ammoBefore = Convert.ToInt32(GetMember(launcher, "municaoTotal"));
        object[] launchArguments = { navalTarget.transform.position, navalTarget.transform, true, string.Empty };
        bool launched = Convert.ToBoolean(launch.Invoke(launcher, launchArguments));
        string launchReason = Convert.ToString(launchArguments[3]);
        Assert.That(launched, Is.True, "O lançador recusou a criação do míssil real: " + launchReason);
        SetEnumMember(launcher, "modoAtual", "Passivo"); // impede nova varredura automática durante o voo.
        CollectNewNavalMissiles();
        int ammoAfter = Convert.ToInt32(GetMember(launcher, "municaoTotal"));
        Assert.That(ammoAfter, Is.EqualTo(ammoBefore - 1),
            "A munição naval não foi consumida quando o míssil real saiu do lançador.");

        float impactDeadline = Time.realtimeSinceStartup + 18f;
        while (Time.realtimeSinceStartup < impactDeadline
            && targetDamage != null
            && Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) > 0f)
        {
            CollectNewNavalMissiles();
            yield return null;
        }

        float healthAfter = targetDamage != null ? Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) : 0f;
        int aggressorTeam = targetDamage != null ? Convert.ToInt32(GetMember(targetDamage, "ultimoAgressorTeamId")) : -1;
        bool destroyed = targetDamage == null
            || healthAfter <= 0f
            || Convert.ToBoolean(GetMember(targetDamage, "morreu"));
        Debug.Log("[IA03][NAVAL][INTEGRACAO_REAL] peaceAllowed=" + allowedDuringPeace
            + " peaceReason=" + peaceReason
            + " warAllowed=" + allowedDuringWar
            + " missile=" + realMissilePrefab.name
            + " ammo=" + ammoBefore + "->" + ammoAfter
            + " hp=" + healthBefore + "->" + healthAfter
            + " destroyed=" + destroyed
            + " aggressorTeam=" + aggressorTeam);
        Assert.That(destroyed, Is.True,
            "O míssil naval real foi lançado, mas não destruiu o alvo durante a janela do teste.");
        Assert.That(aggressorTeam, Is.EqualTo(1),
            "O dano do míssil naval não foi atribuído à identidade Team 1 do lançador.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (sceneHandlerSubscribed)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            sceneHandlerSubscribed = false;
        }

        if (unitController != null)
        {
            unitController.GetType().GetMethod("EmitirOrdemParar", InstanceMembers)?.Invoke(unitController, null);
            explicitAirOrderIssued = false;
        }
        CollectNewMissiles();
        DestroyNewMissiles();
        if (trackingGunProjectiles)
        {
            CollectNewGunProjectiles();
            DestroyNewGunProjectiles();
            trackingGunProjectiles = false;
        }
        if (trackingTacticalMissiles)
        {
            CollectNewTacticalMissiles();
            DestroyNewTacticalMissiles();
            trackingTacticalMissiles = false;
        }
        CollectNewNavalMissiles();
        DestroyNewNavalMissiles();
        if (fighter != null) UnityEngine.Object.Destroy(fighter);
        if (target != null) UnityEngine.Object.Destroy(target);
        if (attackMenuForTargetPick != null) UnityEngine.Object.Destroy(attackMenuForTargetPick);
        if (attackOrdersForTest != null) UnityEngine.Object.Destroy(attackOrdersForTest);
        if (navalAttacker != null) UnityEngine.Object.Destroy(navalAttacker);
        if (navalTarget != null) UnityEngine.Object.Destroy(navalTarget);
        RestoreDiplomacy();
        foreach (KeyValuePair<Behaviour, bool> entry in sceneTurretsBefore)
        {
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        }
        sceneTurretsBefore.Clear();
        fighter = null;
        target = null;
        attackMenuForTargetPick = null;
        attackOrdersForTest = null;
        navalAttacker = null;
        navalTarget = null;
        unitController = null;
        yield return null;
    }

    private IEnumerator LoadValidationSceneOnly()
    {
#if UNITY_EDITOR
        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHandlerSubscribed = true;
        AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        Assert.That(operation, Is.Not.Null, "Não foi possível carregar IA03_WarValidation em modo Single.");
        while (!operation.isDone) yield return null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHandlerSubscribed = false;

        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ValidationScenePath));
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.path.Length > 0)
                Assert.That(scene.path, Is.EqualTo(ValidationScenePath), "Cena extra de gameplay carregada no teste aéreo.");
        }

        DisableStrategicDrivers();
#else
        Assert.Fail("O teste aéreo precisa executar no Editor para carregar a cena de validação.");
        yield break;
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
            DisableStrategicDrivers();
    }

    private void DisableStrategicDrivers()
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            string typeName = behaviour.GetType().Name;
            if (typeName == "IA_BrainMaster" || typeName == "IA03EstrategaNacional")
                behaviour.enabled = false;
            else if (typeName == "ControleTorreta" || typeName == "ControleTorretaModular")
            {
                if (!sceneTurretsBefore.ContainsKey(behaviour))
                    sceneTurretsBefore.Add(behaviour, behaviour.enabled);
                behaviour.enabled = false;
            }
        }
    }

    private void SetNeutralDiplomacy(int targetTeamId = 3)
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Assert.That(governmentType, Is.Not.Null);
        government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
        Assert.That(government, Is.Not.Null, "O bootstrap não criou SistemaGovernoMundial.");

        MethodInfo getRelation = governmentType.GetMethod("ObterRelacao", InstanceMembers,
            null, new[] { typeof(int), typeof(int) }, null);
        Assert.That(getRelation, Is.Not.Null);
        relation = getRelation.Invoke(government, new object[] { 1, targetTeamId });
        Assert.That(relation, Is.Not.Null, "A relação diplomática do cenário não existe.");

        diplomacyBefore = new[]
        {
            GetMember(relation, "valor"),
            GetMember(relation, "guerraDeclarada"),
            GetMember(relation, "sancaoAtiva"),
            GetMember(relation, "cessarFogoAtivo"),
            GetMember(relation, "posturaAParaB"),
            GetMember(relation, "posturaBParaA")
        };

        SetMember(relation, "valor", 0);
        SetMember(relation, "guerraDeclarada", false);
        SetMember(relation, "sancaoAtiva", false);
        SetMember(relation, "cessarFogoAtivo", false);
        SetEnumMember(relation, "posturaAParaB", "Neutro");
        SetEnumMember(relation, "posturaBParaA", "Neutro");
        government.GetType().GetMethod("NotificarGovernoAtualizado", InstanceMembers)?.Invoke(government, null);
    }

    private void CreateRealFighterAndTarget(string prefabPath = FighterPrefabPath, int targetTeamId = 3)
    {
#if UNITY_EDITOR
        GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(fighterPrefab, Is.Not.Null, "Prefab real de caça não foi encontrado: " + prefabPath);
        fighter = UnityEngine.Object.Instantiate(fighterPrefab, new Vector3(100000f, 10000f, 100000f), Quaternion.identity);
        fighter.name = "IA03_Aerial_Integration_F16";

        Type unitIdentityType = ResolveType("IdentidadeUnidade");
        Component identity = fighter.GetComponentInChildren(unitIdentityType, true);
        Assert.That(identity, Is.Not.Null, "F-16 real sem IdentidadeUnidade.");
        SetMember(identity, "teamID", 1);
        SetEnumMember(identity, "tipoUnidade", "Aereo");

        Type unitControllerType = ResolveType("ControleUnidade");
        Component unitController = fighter.GetComponent(unitControllerType);
        if (unitController == null) unitController = fighter.AddComponent(unitControllerType);

        target = new GameObject("IA03_Aerial_Integration_Target_Team" + targetTeamId);
        target.transform.position = fighter.transform.position + Vector3.forward * 90f;
        target.AddComponent<BoxCollider>().size = Vector3.one * 12f;
        Component targetDamage = target.AddComponent(ResolveType("SistemaDeDanos"));
        SetMember(targetDamage, "vidaMaxima", 1000f);
        Component targetIdentity = target.AddComponent(unitIdentityType);
        SetMember(targetIdentity, "teamID", targetTeamId);
        SetEnumMember(targetIdentity, "tipoUnidade", "Aereo");
#endif
    }

    private void CacheActiveMissiles()
    {
        activeMissilesBefore.Clear();
        Type missileType = ResolveType("MisselCaca");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null) activeMissilesBefore.Add(active[i].GetInstanceID());
        }
    }

    private void CollectNewMissiles()
    {
        Type missileType = ResolveType("MisselCaca");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            Component missile = active[i] as Component;
            if (missile == null || activeMissilesBefore.Contains(missile.GetInstanceID())) continue;
            if (!missilesSpawnedByTest.Contains(missile)) missilesSpawnedByTest.Add(missile);
        }
    }

    private void DestroyNewMissiles()
    {
        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        for (int i = 0; i < missilesSpawnedByTest.Count; i++)
        {
            Component missile = missilesSpawnedByTest[i];
            if (missile == null) continue;
            if (release != null) release.Invoke(null, new object[] { missile.gameObject });
            else UnityEngine.Object.Destroy(missile.gameObject);
        }
        missilesSpawnedByTest.Clear();
    }

    private void CacheActiveGunProjectiles()
    {
        trackingGunProjectiles = true;
        activeGunProjectilesBefore.Clear();
        Type projectileType = ResolveType("Projetil");
        if (projectileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            projectileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null) activeGunProjectilesBefore.Add(active[i].GetInstanceID());
        }
    }

    private void CollectNewGunProjectiles()
    {
        Type projectileType = ResolveType("Projetil");
        if (projectileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            projectileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            Component projectile = active[i] as Component;
            if (projectile == null || activeGunProjectilesBefore.Contains(projectile.GetInstanceID())) continue;
            if (!gunProjectilesSpawnedByTest.Contains(projectile)) gunProjectilesSpawnedByTest.Add(projectile);
        }
    }

    private void DestroyNewGunProjectiles()
    {
        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        for (int i = 0; i < gunProjectilesSpawnedByTest.Count; i++)
        {
            Component projectile = gunProjectilesSpawnedByTest[i];
            if (projectile == null || !projectile.gameObject.activeInHierarchy) continue;
            if (release != null) release.Invoke(null, new object[] { projectile.gameObject });
            else UnityEngine.Object.Destroy(projectile.gameObject);
        }
        gunProjectilesSpawnedByTest.Clear();
    }

    private void CacheActiveTacticalMissiles()
    {
        trackingTacticalMissiles = true;
        activeTacticalMissilesBefore.Clear();
        Type missileType = ResolveType("MisselTatico");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null) activeTacticalMissilesBefore.Add(active[i].GetInstanceID());
        }
    }

    private void CollectNewTacticalMissiles()
    {
        Type missileType = ResolveType("MisselTatico");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            Component missile = active[i] as Component;
            if (missile == null || activeTacticalMissilesBefore.Contains(missile.GetInstanceID())) continue;
            if (!tacticalMissilesSpawnedByTest.Contains(missile)) tacticalMissilesSpawnedByTest.Add(missile);
        }
    }

    private void DestroyNewTacticalMissiles()
    {
        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        for (int i = 0; i < tacticalMissilesSpawnedByTest.Count; i++)
        {
            Component missile = tacticalMissilesSpawnedByTest[i];
            if (missile == null || !missile.gameObject.activeInHierarchy) continue;
            if (release != null) release.Invoke(null, new object[] { missile.gameObject });
            else UnityEngine.Object.Destroy(missile.gameObject);
        }
        tacticalMissilesSpawnedByTest.Clear();
        tacticalMissileSpawnedByTest = null;
    }

    private void CacheActiveNavalMissiles()
    {
        activeNavalMissilesBefore.Clear();
        Type missileType = ResolveType("MisselNaval");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null) activeNavalMissilesBefore.Add(active[i].GetInstanceID());
        }
    }

    private void CollectNewNavalMissiles()
    {
        Type missileType = ResolveType("MisselNaval");
        if (missileType == null) return;
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(
            missileType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < active.Length; i++)
        {
            Component missile = active[i] as Component;
            if (missile == null || activeNavalMissilesBefore.Contains(missile.GetInstanceID())) continue;
            if (!navalMissilesSpawnedByTest.Contains(missile)) navalMissilesSpawnedByTest.Add(missile);
        }
    }

    private void DestroyNewNavalMissiles()
    {
        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        for (int i = 0; i < navalMissilesSpawnedByTest.Count; i++)
        {
            Component missile = navalMissilesSpawnedByTest[i];
            if (missile == null || !missile.gameObject.activeInHierarchy) continue;
            if (release != null) release.Invoke(null, new object[] { missile.gameObject });
            else UnityEngine.Object.Destroy(missile.gameObject);
        }
        navalMissilesSpawnedByTest.Clear();
    }

    private void RestoreDiplomacy()
    {
        if (relation == null || diplomacyBefore == null || diplomacyBefore.Length != 6) return;
        string[] members = { "valor", "guerraDeclarada", "sancaoAtiva", "cessarFogoAtivo", "posturaAParaB", "posturaBParaA" };
        for (int i = 0; i < members.Length; i++) SetMember(relation, members[i], diplomacyBefore[i]);
        government?.GetType().GetMethod("NotificarGovernoAtualizado", InstanceMembers)?.Invoke(government, null);
    }

    private static Type GetMemberType(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, InstanceMembers);
        if (field != null) return field.FieldType;
        PropertyInfo property = instance.GetType().GetProperty(name, InstanceMembers);
        return property != null ? property.PropertyType : null;
    }

    private static object GetMember(object instance, string name)
    {
        if (instance == null) return null;
        FieldInfo field = instance.GetType().GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(instance);
        PropertyInfo property = instance.GetType().GetProperty(name, InstanceMembers);
        return property != null && property.CanRead ? property.GetValue(instance) : null;
    }

    private static void SetMember(object instance, string name, object value)
    {
        if (instance == null) return;
        FieldInfo field = instance.GetType().GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }
        PropertyInfo property = instance.GetType().GetProperty(name, InstanceMembers);
        if (property != null && property.CanWrite) property.SetValue(instance, value);
    }

    private static void SetEnumMember(object instance, string name, string value)
    {
        Type type = GetMemberType(instance, name);
        if (type != null && type.IsEnum) SetMember(instance, name, Enum.Parse(type, value));
    }

    private static Type ResolveType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }
}
