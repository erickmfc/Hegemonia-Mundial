using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
/// Exercita a aquisição automática real dos dois controladores contra um alvo
/// marcado como Inimigo, mas sem IdentidadeUnidade. A cena de validação é
/// carregada em Single para evitar IDs de Creaty duplicados entre cenas.
/// </summary>
public sealed class IA03TagOnlyAcquisitionPlayModeTests
{
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const string AttackerPath = "/IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_01";
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed class MemberSnapshot
    {
        public Component component;
        public readonly Dictionary<string, object> values = new Dictionary<string, object>();
    }

    private readonly List<Behaviour> turretsToRestore = new List<Behaviour>();
    private readonly Dictionary<Behaviour, bool> originalTurretEnabled = new Dictionary<Behaviour, bool>();
    private readonly List<MemberSnapshot> memberSnapshots = new List<MemberSnapshot>();
    private readonly HashSet<int> projectileIdsBefore = new HashSet<int>();
    private readonly HashSet<int> projectileIdsObserved = new HashSet<int>();
    private readonly List<Component> observedProjectiles = new List<Component>();
    private readonly List<Behaviour> strategyBehavioursLeftDisabled = new List<Behaviour>();
    private readonly List<string> projectileRouteEvents = new List<string>();
    private readonly Dictionary<string, object> relationValuesBefore = new Dictionary<string, object>();
    private readonly Dictionary<string, object> countryOneValuesBefore = new Dictionary<string, object>();
    private readonly Dictionary<string, object> countryThreeValuesBefore = new Dictionary<string, object>();
    private GameObject attacker;
    private Component attackerIdentity;
    private Component unitController;
    private bool originalCombatMode;
    private bool combatModeCaptured;
    private GameObject tagOnlyTarget;
    private GameObject modularTurretObject;
    private Component selectedTurret;
    private Scene validationScene;
    private bool isolationCaptured;
    private bool routeLogSubscribed;
    private object government;
    private object teamRelation;
    private object countryOne;
    private object countryThree;
    private bool diplomacySnapshotCaptured;

    [UnityTest]
    public IEnumerator ControleTorreta_UpdateNaoAdquireAlvoTagOnlySemIdentidade()
    {
        ResetFixtureState();
        yield return EnsureValidationSceneOnly();
        PrepareAttackerAndDiplomacy();
        ConfigureRealTankTurret("ControleTorreta");
        yield return ObserveTagOnlyTargetForRuntimeWindow();
        AssertTagOnlyWasBlocked("ControleTorreta");
    }

    [UnityTest]
    public IEnumerator ControleTorretaModular_UpdateNaoAdquireAlvoTagOnlySemIdentidade()
    {
        ResetFixtureState();
        yield return EnsureValidationSceneOnly();
        PrepareAttackerAndDiplomacy();
        ConfigureModularTurret();
        yield return ObserveTagOnlyTargetForRuntimeWindow();
        AssertTagOnlyWasBlocked("ControleTorretaModular");
    }

    [UnityTest]
    public IEnumerator BalaPrefabMantemAutoriaEEquipeAteOImpacto()
    {
        ResetFixtureState();
        yield return EnsureValidationSceneOnly();
        PrepareAttackerAndDiplomacy();
        ConfigureBalaImpactScenario();

        yield return null; // deixa SistemaDeDanos inicializar a vida do alvo.
        float vidaAntes = Convert.ToSingle(GetMember(tagOnlyTarget.GetComponent(ResolveType("SistemaDeDanos")), "vidaAtual"));
        Assert.That(vidaAntes, Is.EqualTo(100f));
        Assert.That(Convert.ToBoolean(GetMember(teamRelation, "guerraDeclarada")), Is.False,
            "A relação precisa estar em paz antes do impacto.");

        Application.logMessageReceived += CaptureProjectileRouteLog;
        routeLogSubscribed = true;
        MethodInfo fire = selectedTurret.GetType().GetMethod("Disparar", InstanceMembers);
        Assert.That(fire, Is.Not.Null, "ControleTorreta.Disparar não foi encontrado.");
        fire.Invoke(selectedTurret, null);
        CollectNewProjectiles();

        MethodInfo getOwner = ResolveType("Projetil").GetMethod("GetDono", InstanceMembers);
        Assert.That(getOwner, Is.Not.Null);
        Component projectile = observedProjectiles.FirstOrDefault(candidate => candidate != null
            && string.Equals(candidate.gameObject.name.Replace("(Clone)", string.Empty), "Bala", StringComparison.Ordinal)
            && object.ReferenceEquals(getOwner.Invoke(candidate, null), attackerIdentity.gameObject));
        Assert.That(projectile, Is.Not.Null,
            "O disparo da arma real com Bala.prefab não criou um Projetil sob autoria Team 1.");

        object ownerImmediatelyAfterSpawn = getOwner.Invoke(projectile, null);
        object teamImmediatelyAfterSpawn = GetMember(projectile, "TeamDono");
        object debugOwnerImmediatelyAfterSpawn = GetMember(projectile, "dono");
        Debug.Log("[IA03][BALA_AUTORIA][SPAWN] projectile=" + projectile.name
            + " prefab=" + GetMember(selectedTurret, "municaoPrefab")
            + " owner=" + (ownerImmediatelyAfterSpawn as GameObject)?.name
            + " ownerTeam=" + teamImmediatelyAfterSpawn
            + " privateDono=" + (debugOwnerImmediatelyAfterSpawn as GameObject)?.name
            + " turret=" + selectedTurret.name
            + " frame=" + Time.frameCount);
        Assert.That(ownerImmediatelyAfterSpawn, Is.SameAs(attackerIdentity.gameObject));
        Assert.That(debugOwnerImmediatelyAfterSpawn, Is.SameAs(attackerIdentity.gameObject));
        Assert.That(Convert.ToInt32(teamImmediatelyAfterSpawn), Is.EqualTo(1));

        float deadline = Time.realtimeSinceStartup + 3f;
        Component damage = tagOnlyTarget.GetComponent(ResolveType("SistemaDeDanos"));
        while (Time.realtimeSinceStartup < deadline
            && Convert.ToSingle(GetMember(damage, "vidaAtual")) >= vidaAntes)
        {
            CollectNewProjectiles();
            yield return null;
        }
        CollectNewProjectiles();

        float vidaDepois = Convert.ToSingle(GetMember(damage, "vidaAtual"));
        object ultimoAgressor = GetMember(damage, "ultimoAgressor");
        int ultimoAgressorTeam = Convert.ToInt32(GetMember(damage, "ultimoAgressorTeamId"));
        bool guerraDepois = Convert.ToBoolean(GetMember(teamRelation, "guerraDeclarada"));
        Debug.Log("[IA03][BALA_AUTORIA][DAMAGE] target=" + tagOnlyTarget.name
            + " hpBefore=" + vidaAntes
            + " hpAfter=" + vidaDepois
            + " ultimoAgressor=" + (ultimoAgressor as GameObject)?.name
            + " aggressorTeam=" + ultimoAgressorTeam
            + " warBefore=false warAfter=" + guerraDepois
            + " route=" + string.Join(" | ", projectileRouteEvents));

        Assert.That(vidaDepois, Is.LessThan(vidaAntes),
            "Bala.prefab foi criado, mas não atingiu SistemaDeDanos no alvo configurado.");
        Assert.That(ultimoAgressor, Is.SameAs(attackerIdentity.gameObject));
        Assert.That(ultimoAgressorTeam, Is.EqualTo(1));
        Assert.That(projectileRouteEvents.Any(entry => entry.Contains("[SPAWN]")), Is.True,
            "O probe não registrou a criação do projétil.");
        Assert.That(projectileRouteEvents.Any(entry => entry.Contains("[IMPACT]")), Is.True,
            "O probe não registrou o impacto do projétil.");
        Assert.That(projectileRouteEvents.Any(entry => entry.Contains("[DAMAGE]")), Is.True,
            "O probe não registrou a entrada em SistemaDeDanos.");
        Assert.That(guerraDepois, Is.True,
            "O dano cross-team em paz não passou por RegistrarAgressao/transição diplomática existente.");
    }

    [UnityTearDown]
    public IEnumerator RestoreRuntimeFixture()
    {
        if (tagOnlyTarget != null) UnityEngine.Object.Destroy(tagOnlyTarget);
        if (modularTurretObject != null) UnityEngine.Object.Destroy(modularTurretObject);
        DestroyNewProjectiles();
        if (routeLogSubscribed)
        {
            Application.logMessageReceived -= CaptureProjectileRouteLog;
            routeLogSubscribed = false;
        }

        if (combatModeCaptured && unitController != null)
        {
            MethodInfo defineCombat = unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers,
                null, new[] { typeof(bool) }, null);
            defineCombat?.Invoke(unitController, new object[] { originalCombatMode });
        }

        RestoreMemberSnapshots();
        RestoreDiplomacySnapshot();

        for (int i = 0; i < turretsToRestore.Count; i++)
        {
            Behaviour turret = turretsToRestore[i];
            if (turret != null && originalTurretEnabled.TryGetValue(turret, out bool wasEnabled))
                turret.enabled = wasEnabled;
        }

        // The validation scene's BrainMaster and IA03 stay isolated for the
        // remainder of this PlayMode run. Unity restores serialized enabled
        // states when the Editor exits PlayMode.
        yield return null;
    }

    private void ResetFixtureState()
    {
        turretsToRestore.Clear();
        originalTurretEnabled.Clear();
        memberSnapshots.Clear();
        projectileIdsBefore.Clear();
        projectileIdsObserved.Clear();
        observedProjectiles.Clear();
        strategyBehavioursLeftDisabled.Clear();
        projectileRouteEvents.Clear();
        relationValuesBefore.Clear();
        countryOneValuesBefore.Clear();
        countryThreeValuesBefore.Clear();

        attacker = null;
        attackerIdentity = null;
        unitController = null;
        tagOnlyTarget = null;
        modularTurretObject = null;
        selectedTurret = null;
        validationScene = default;
        government = null;
        teamRelation = null;
        countryOne = null;
        countryThree = null;
        originalCombatMode = false;
        combatModeCaptured = false;
        isolationCaptured = false;
        routeLogSubscribed = false;
        diplomacySnapshotCaptured = false;
    }

    private IEnumerator EnsureValidationSceneOnly()
    {
        // Reload even when the Editor already had this scene open: loading it
        // in PlayMode gives the sceneLoaded callback a chance to stop AI before
        // Start/Update and guarantees there is no additive GlobalMapRTS.
#if UNITY_EDITOR
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        Assert.That(load, Is.Not.Null, "Não foi possível carregar IA03_WarValidation em LoadSceneMode.Single.");
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= OnValidationSceneLoaded;
#else
        Assert.Fail("A validação tag-only precisa executar no Editor para carregar a cena de validação.");
        yield break;
#endif

        validationScene = SceneManager.GetSceneByPath(ValidationScenePath);
        Assert.That(validationScene.IsValid() && validationScene.isLoaded, Is.True,
            "IA03_WarValidation não está carregada após o isolamento.");
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ValidationScenePath));
        Assert.That(SceneManager.GetSceneByName("GlobalMapRTS").isLoaded, Is.False,
            "GlobalMapRTS permaneceu carregada junto da fixture IA03.");

        List<string> loadedScenePaths = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.path.Length > 0) loadedScenePaths.Add(scene.path);
        }
        Assert.That(loadedScenePaths, Does.Contain(ValidationScenePath));
        Assert.That(loadedScenePaths.Where(path => !string.Equals(path, ValidationScenePath,
                StringComparison.OrdinalIgnoreCase)), Is.Empty,
            "Outra cena de gameplay ficou carregada: " + string.Join(", ", loadedScenePaths));

        IsolateStrategyAndTurrets(validationScene);
        CacheActiveProjectiles();
        Debug.Log("[IA03][TAG_ONLY][ISOLATION] scenes=" + SceneManager.sceneCount
            + " gameplayScenes=" + string.Join(",", loadedScenePaths)
            + " sceneDirty=" + validationScene.isDirty
            + " brainAndIA03Disabled=" + strategyBehavioursLeftDisabled.Count);
    }

    private void OnValidationSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase)) return;
        validationScene = scene;
        IsolateStrategyAndTurrets(scene);
    }

    private void IsolateStrategyAndTurrets(Scene scene)
    {
        if (isolationCaptured) return;
        isolationCaptured = true;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour.gameObject.scene != scene) continue;
            string typeName = behaviour.GetType().Name;
            bool isStrategy = typeName == "IA_BrainMaster" || typeName == "IA03EstrategaNacional";
            bool isTurret = typeName == "ControleTorreta" || typeName == "ControleTorretaModular";
            if (!isStrategy && !isTurret) continue;

            if (isTurret)
            {
                turretsToRestore.Add(behaviour);
                originalTurretEnabled[behaviour] = behaviour.enabled;
            }
            else
            {
                strategyBehavioursLeftDisabled.Add(behaviour);
            }
            behaviour.enabled = false;
        }
    }

    private void PrepareAttackerAndDiplomacy()
    {
        attacker = FindGameObjectInScene(AttackerPath);
        Assert.That(attacker, Is.Not.Null, "Unidade real Valdoria_Tank_01 não foi encontrada na cena de validação.");

        Type identityType = ResolveType("IdentidadeUnidade");
        Type controlType = ResolveType("ControleUnidade");
        attackerIdentity = attacker.GetComponentInChildren(identityType, true) as Component;
        unitController = attacker.GetComponentInChildren(controlType, true) as Component;
        Assert.That(attackerIdentity, Is.Not.Null, "O atacante real precisa ter IdentidadeUnidade.");
        Assert.That(unitController, Is.Not.Null, "O atacante real precisa ter ControleUnidade.");
        Assert.That(Convert.ToInt32(GetMember(attackerIdentity, "teamID")), Is.EqualTo(1));

        PropertyInfo combatProperty = controlType.GetProperty("ModoCombateAtivo", InstanceMembers);
        MethodInfo defineCombat = controlType.GetMethod("DefinirModoCombate", InstanceMembers,
            null, new[] { typeof(bool) }, null);
        Assert.That(combatProperty, Is.Not.Null);
        Assert.That(defineCombat, Is.Not.Null);
        originalCombatMode = Convert.ToBoolean(combatProperty.GetValue(unitController));
        combatModeCaptured = true;
        defineCombat.Invoke(unitController, new object[] { true });
        Assert.That(Convert.ToBoolean(combatProperty.GetValue(unitController)), Is.True,
            "O atacante real não entrou em modo de combate para o teste runtime.");

        Type governmentType = ResolveType("SistemaGovernoMundial");
        government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
        Assert.That(government, Is.Not.Null, "O bootstrap da cena não criou SistemaGovernoMundial.");
        MethodInfo getRelation = governmentType.GetMethods(InstanceMembers).FirstOrDefault(method =>
        {
            if (method.Name != "ObterRelacao") return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 2
                && parameters[0].ParameterType == typeof(int)
                && parameters[1].ParameterType == typeof(int);
        });
        teamRelation = getRelation?.Invoke(government, new object[] { 1, 3 });
        Assert.That(teamRelation, Is.Not.Null, "A relação neutra Team 1/Team 3 não existe.");
        Assert.That(Convert.ToBoolean(GetMember(teamRelation, "guerraDeclarada")), Is.False,
            "O teste tag-only precisa começar em paz neutra.");
        Assert.That(Convert.ToString(GetMember(teamRelation, "posturaAParaB")), Is.EqualTo("Neutro"));
        Assert.That(Convert.ToString(GetMember(teamRelation, "posturaBParaA")), Is.EqualTo("Neutro"));

        MethodInfo getCountry = governmentType.GetMethods(InstanceMembers).FirstOrDefault(method =>
        {
            if (method.Name != "ObterPais") return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(int);
        });
        countryOne = getCountry?.Invoke(government, new object[] { 1 });
        countryThree = getCountry?.Invoke(government, new object[] { 3 });
        CaptureMembers(teamRelation, relationValuesBefore, "valor", "guerraDeclarada", "sancaoAtiva",
            "cessarFogoAtivo", "posturaAParaB", "posturaBParaA");
        CaptureMembers(countryOne, countryOneValuesBefore, "emGuerra", "rivalTeamId");
        CaptureMembers(countryThree, countryThreeValuesBefore, "emGuerra", "rivalTeamId");
        diplomacySnapshotCaptured = true;

        for (int i = 0; i < turretsToRestore.Count; i++)
        {
            Behaviour turret = turretsToRestore[i];
            if (turret == null) continue;
            MemberSnapshot snapshot = new MemberSnapshot { component = turret };
            CaptureMember(snapshot, "alvoAtual");
            CaptureMember(snapshot, "alvoPrioritario");
            CaptureMember(snapshot, "modoPassivo");
            CaptureMember(snapshot, "alcance");
            CaptureMember(snapshot, "alcanceRadar");
            CaptureMember(snapshot, "dispararMesmoDesalinhado");
            CaptureMember(snapshot, "contadorTempo");
            CaptureMember(snapshot, "balasAtuais");
            CaptureMember(snapshot, "estaRecarregando");
            CaptureMember(snapshot, "etiquetaAlvo");
            CaptureMember(snapshot, "minhaIdentidade");
            memberSnapshots.Add(snapshot);
        }
    }

    private void ConfigureRealTankTurret(string controllerTypeName)
    {
        Type controllerType = ResolveType(controllerTypeName);
        Component[] candidates = attacker.GetComponentsInChildren(controllerType, true);
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject ammo = GetMember(candidates[i], "municaoPrefab") as GameObject;
            if (ammo != null && ammo.name == "Bala")
            {
                selectedTurret = candidates[i];
                break;
            }
        }
        Assert.That(selectedTurret, Is.Not.Null,
            "O atacante real não tem uma ControleTorreta configurada com o prefab Bala.prefab.");

        ConfigureTagOnlyTargetFromExistingMuzzle(selectedTurret, "locaisDoTiro");
        SetMember(selectedTurret, "alvoAtual", null);
        SetMember(selectedTurret, "alvoPrioritario", null);
        SetMember(selectedTurret, "modoPassivo", false);
        SetMember(selectedTurret, "etiquetaAlvo", "Inimigo");
        SetMember(selectedTurret, "dispararMesmoDesalinhado", true);
        SetMember(selectedTurret, "contadorTempo", 0f);
        SetMember(selectedTurret, "balasAtuais", 50);
        ((Behaviour)selectedTurret).enabled = true;
    }

    private void ConfigureBalaImpactScenario()
    {
        Type controllerType = ResolveType("ControleTorreta");
        Component[] candidates = attacker.GetComponentsInChildren(controllerType, true);
        GameObject ammo = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            ammo = GetMember(candidates[i], "municaoPrefab") as GameObject;
            if (ammo != null && ammo.name == "Bala")
            {
                selectedTurret = candidates[i];
                break;
            }
        }
        Assert.That(selectedTurret, Is.Not.Null,
            "O tanque Team 1 precisa manter uma torreta real configurada com Bala.prefab.");
        Assert.That(ammo, Is.Not.Null);
#if UNITY_EDITOR
        Assert.That(AssetDatabase.GetAssetPath(ammo), Is.EqualTo("Assets/Prefabs/Soldado_Rifle/Bala.prefab"),
            "O teste precisa disparar exatamente o prefab Bala.prefab citado no smoke.");
#endif

        Transform[] muzzles = GetMember(selectedTurret, "locaisDoTiro") as Transform[];
        Assert.That(muzzles, Is.Not.Null, "A torreta real precisa ter uma lista de locais de disparo.");
        Assert.That(muzzles, Is.Not.Empty, "A torreta real precisa ter pelo menos um local de disparo.");
        Transform muzzle = muzzles[0];
        Assert.That(muzzle, Is.Not.Null);

        tagOnlyTarget = new GameObject("IA03 Bala Authorship Impact Target Team 3");
        tagOnlyTarget.tag = "Inimigo";
        tagOnlyTarget.transform.position = muzzle.position + muzzle.forward * 30f;
        tagOnlyTarget.transform.position = new Vector3(
            tagOnlyTarget.transform.position.x,
            Mathf.Clamp(muzzle.position.y, 0f, 5f),
            tagOnlyTarget.transform.position.z);
        BoxCollider collider = tagOnlyTarget.AddComponent<BoxCollider>();
        collider.size = new Vector3(10f, 10f, 10f);
        Component targetIdentity = tagOnlyTarget.AddComponent(ResolveType("IdentidadeUnidade")) as Component;
        Assert.That(targetIdentity, Is.Not.Null);
        SetMember(targetIdentity, "teamID", 3);
        Component targetDamage = tagOnlyTarget.AddComponent(ResolveType("SistemaDeDanos")) as Component;
        Assert.That(targetDamage, Is.Not.Null);
        SetMember(targetDamage, "vidaMaxima", 100f);
        SetMember(targetDamage, "vidaAtual", 100f);

        SetMember(selectedTurret, "minhaIdentidade", attackerIdentity);
        SetMember(selectedTurret, "alvoAtual", tagOnlyTarget.transform);
        SetMember(selectedTurret, "alvoPrioritario", tagOnlyTarget.transform);
        SetMember(selectedTurret, "modoPassivo", false);
        SetMember(selectedTurret, "balasAtuais", 50);
        SetMember(selectedTurret, "estaRecarregando", false);
        ((Behaviour)selectedTurret).enabled = false; // o teste invoca uma vez o método real Disparar.
    }

    private void CaptureProjectileRouteLog(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;
        if (condition.Contains("[IA03][PROJECTILE_PROBE]")
            || condition.Contains("[IA03][Validação][PrimeiroDano]"))
            projectileRouteEvents.Add(condition);
    }

    private void RestoreDiplomacySnapshot()
    {
        if (!diplomacySnapshotCaptured) return;
        RestoreMembers(teamRelation, relationValuesBefore);
        RestoreMembers(countryOne, countryOneValuesBefore);
        RestoreMembers(countryThree, countryThreeValuesBefore);
        MethodInfo notify = government?.GetType().GetMethod("NotificarGovernoAtualizado", InstanceMembers,
            null, Type.EmptyTypes, null);
        notify?.Invoke(government, null);
        diplomacySnapshotCaptured = false;
    }

    private void ConfigureModularTurret()
    {
        GameObject ammoPrefab = null;
        Type legacyTurretType = ResolveType("ControleTorreta");
        Component[] existingTurrets = attacker.GetComponentsInChildren(legacyTurretType, true);
        for (int i = 0; i < existingTurrets.Length && ammoPrefab == null; i++)
            ammoPrefab = GetMember(existingTurrets[i], "municaoPrefab") as GameObject;
        Assert.That(ammoPrefab, Is.Not.Null, "Não foi possível reutilizar o prefab Bala.prefab da fixture real.");

        modularTurretObject = new GameObject("IA03 Tag Only Modular Turret");
        modularTurretObject.transform.SetParent(attacker.transform, true);
        modularTurretObject.transform.position = attacker.transform.position + Vector3.up * 1.2f;
        modularTurretObject.transform.rotation = Quaternion.identity;
        selectedTurret = modularTurretObject.AddComponent(ResolveType("ControleTorretaModular")) as Component;
        Assert.That(selectedTurret, Is.Not.Null);
        SetMember(selectedTurret, "etiquetaAlvo", "Inimigo");
        SetMember(selectedTurret, "alcanceRadar", 100f);
        SetMember(selectedTurret, "modoPassivo", false);
        SetMember(selectedTurret, "alvoPrioritario", null);
        SetMember(selectedTurret, "pecaQueGira", modularTurretObject.transform);
        SetMember(selectedTurret, "velocidadeGiro", 720f);

        GameObject muzzleObject = new GameObject("IA03 Tag Only Modular Muzzle");
        muzzleObject.transform.SetParent(modularTurretObject.transform, false);
        muzzleObject.transform.localPosition = Vector3.zero;
        muzzleObject.transform.localRotation = Quaternion.identity;

        Type weaponType = ResolveType("ModuloArma");
        object weapon = Activator.CreateInstance(weaponType);
        SetMember(weapon, "nomeArma", "Tag-only runtime observer");
        SetMember(weapon, "municaoPrefab", ammoPrefab);
        SetMember(weapon, "pontosDisparo", new[] { muzzleObject.transform });
        SetMember(weapon, "velocidadeProjetil", 120f);
        SetMember(weapon, "danoBase", 2f);
        SetMember(weapon, "intervaloTiro", 0.05f);
        SetMember(weapon, "tamanhoCartucho", 50);
        SetMember(weapon, "alcanceMaximo", 100f);
        Type weaponListType = typeof(List<>).MakeGenericType(weaponType);
        System.Collections.IList weapons = Activator.CreateInstance(weaponListType) as System.Collections.IList;
        weapons.Add(weapon);
        SetMember(selectedTurret, "armas", weapons);

        ConfigureTagOnlyTargetFromExistingMuzzle(selectedTurret, "<modular-muzzle>", muzzleObject.transform);
        ((Behaviour)selectedTurret).enabled = true;
    }

    private void ConfigureTagOnlyTargetFromExistingMuzzle(Component turret, string muzzleMember,
        Transform explicitMuzzle = null)
    {
        Transform muzzle = explicitMuzzle;
        if (muzzle == null)
        {
            Transform[] muzzles = GetMember(turret, muzzleMember) as Transform[];
            Assert.That(muzzles, Is.Not.Null,
                "A torreta selecionada precisa de um conjunto real de pontos de disparo.");
            Assert.That(muzzles, Is.Not.Empty,
                "A torreta selecionada precisa de ao menos um ponto de disparo real.");
            muzzle = muzzles[0];
        }
        Assert.That(muzzle, Is.Not.Null);

        tagOnlyTarget = new GameObject("IA03 Tag Only Inimigo Target");
        tagOnlyTarget.tag = "Inimigo";
        tagOnlyTarget.transform.position = muzzle.position + muzzle.forward * 45f;
        tagOnlyTarget.transform.position = new Vector3(
            tagOnlyTarget.transform.position.x,
            Mathf.Clamp(muzzle.position.y, 0f, 5f),
            tagOnlyTarget.transform.position.z);
        BoxCollider collider = tagOnlyTarget.AddComponent<BoxCollider>();
        collider.size = new Vector3(5f, 5f, 5f);
        Component damage = tagOnlyTarget.AddComponent(ResolveType("SistemaDeDanos")) as Component;
        Assert.That(damage, Is.Not.Null);
        SetMember(damage, "vidaMaxima", 100f);
        SetMember(damage, "vidaAtual", 100f);

        Type identityType = ResolveType("IdentidadeUnidade");
        Assert.That(tagOnlyTarget.GetComponent(identityType), Is.Null);
        Assert.That(tagOnlyTarget.GetComponentInParent(identityType), Is.Null);
        Assert.That(tagOnlyTarget.GetComponentsInChildren(identityType, true), Is.Empty);
        Assert.That(tagOnlyTarget.tag, Is.EqualTo("Inimigo"));
    }

    private IEnumerator ObserveTagOnlyTargetForRuntimeWindow()
    {
        Assert.That(selectedTurret, Is.Not.Null);
        Assert.That(GetMember(selectedTurret, "alvoPrioritario"), Is.Null,
            "O teste não pode iniciar com alvoPrioritario.");
        Assert.That(tagOnlyTarget, Is.Not.Null);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ValidationScenePath));
        Assert.That(SceneManager.GetSceneByName("GlobalMapRTS").isLoaded, Is.False);

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            CollectNewProjectiles();
            yield return null;
        }
        CollectNewProjectiles();

        Component health = tagOnlyTarget.GetComponent(ResolveType("SistemaDeDanos"));
        Debug.Log("[IA03][TAG_ONLY][RESULT] controller=" + selectedTurret.GetType().Name
            + " attacker=" + attacker.name
            + " attackerTeam=" + GetMember(attackerIdentity, "teamID")
            + " combatActive=" + GetMember(unitController, "ModoCombateAtivo")
            + " target=" + tagOnlyTarget.name
            + " tag=" + tagOnlyTarget.tag
            + " identityDirect=<null> identityParent=<null> identityChildren=0"
            + " targetCurrent=" + DescribeTarget(GetMember(selectedTurret, "alvoAtual"))
            + " targetPriority=" + DescribeTarget(GetMember(selectedTurret, "alvoPrioritario"))
            + " projectilesSpawned=" + projectileIdsObserved.Count
            + " damageBefore=100 damageAfter=" + GetMember(health, "vidaAtual")
            + " activeGameplayScene=" + SceneManager.GetActiveScene().path);
    }

    private void AssertTagOnlyWasBlocked(string controllerTypeName)
    {
        Component current = GetMember(selectedTurret, "alvoAtual") as Component;
        Component identity = tagOnlyTarget.GetComponent(ResolveType("IdentidadeUnidade"));
        MethodInfo policy = ResolveType(controllerTypeName).GetMethod("PodeAtacarAutomaticamente", InstanceMembers);
        string policyResult = policy != null
            ? Convert.ToString(policy.Invoke(selectedTurret, new object[] { identity }))
            : "method-not-found";
        string path = controllerTypeName == "ControleTorretaModular"
            ? "Update -> AtualizarAgendamentoBusca -> ProcurarAlvo -> OverlapSphereNonAlloc -> GetComponentInParent<IdentidadeUnidade>"
            : "Update -> AtualizarAgendamentoBusca -> ProcurarAlvo -> OverlapSphereNonAlloc -> ObterIdentidadeUnidadeComCache/GetComponentInParent<IdentidadeUnidade>";
        string report = "controller=" + controllerTypeName
            + " target=" + tagOnlyTarget.name
            + " tag=" + tagOnlyTarget.tag
            + " identityDirect=<null> identityParent=<null> identityChildren=0"
            + " explicitTarget=<null> policy(null)=" + policyResult
            + " runtimePath=" + path
            + " currentTarget=" + DescribeTarget(current)
            + " projectilesSpawned=" + projectileIdsObserved.Count
            + " damage=" + GetMember(tagOnlyTarget.GetComponent(ResolveType("SistemaDeDanos")), "vidaAtual")
            + " activeScene=" + SceneManager.GetActiveScene().path
            + " loadedSceneCount=" + SceneManager.sceneCount;

        Assert.That(current, Is.Null, "Alvo Inimigo sem identidade foi adquirido automaticamente. " + report);
        Assert.That(GetMember(selectedTurret, "alvoPrioritario"), Is.Null,
            "O alvo tag-only não pode ser promovido a alvo prioritário. " + report);
        Assert.That(projectileIdsObserved, Is.Empty,
            "O alvo tag-only provocou spawn de projétil pelo Update real. " + report);
        Assert.That(Convert.ToSingle(GetMember(tagOnlyTarget.GetComponent(ResolveType("SistemaDeDanos")), "vidaAtual")),
            Is.EqualTo(100f), "O alvo tag-only recebeu dano. " + report);
        Assert.That(SceneManager.GetSceneByName("GlobalMapRTS").isLoaded, Is.False,
            "GlobalMapRTS foi carregada durante a validação. " + report);
    }

    private void CacheActiveProjectiles()
    {
        foreach (Component projectile in FindActiveProjectiles())
            projectileIdsBefore.Add(projectile.GetInstanceID());
    }

    private void CollectNewProjectiles()
    {
        foreach (Component projectile in FindActiveProjectiles())
        {
            int id = projectile.GetInstanceID();
            if (projectileIdsBefore.Contains(id) || !projectileIdsObserved.Add(id)) continue;
            observedProjectiles.Add(projectile);
            Type projectileType = ResolveType("Projetil");
            MethodInfo getOwner = projectileType != null
                ? projectileType.GetMethod("GetDono", InstanceMembers)
                : null;
            GameObject owner = getOwner != null ? getOwner.Invoke(projectile, null) as GameObject : null;
            Debug.Log("[IA03][TAG_ONLY][UNEXPECTED_PROJECTILE] id=" + id
                + " name=" + projectile.name
                + " owner=" + (owner != null ? owner.name : "<null>")
                + " team=" + GetMember(projectile, "TeamDono")
                + " position=" + projectile.transform.position
                + " attacker=" + (attacker != null ? attacker.name : "<null>")
                + " target=" + (tagOnlyTarget != null ? tagOnlyTarget.name : "<null>")
                + " targetDistance=" + (tagOnlyTarget != null
                    ? Vector3.Distance(projectile.transform.position, tagOnlyTarget.transform.position).ToString("F2")
                    : "n/a"));
        }
    }

    private List<Component> FindActiveProjectiles()
    {
        Type projectileType = ResolveType("Projetil");
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(projectileType, FindObjectsSortMode.None);
        List<Component> result = new List<Component>();
        for (int i = 0; i < found.Length; i++)
        {
            Component projectile = found[i] as Component;
            if (projectile == null || !projectile.gameObject.activeInHierarchy) continue;
            // O pool de combate é DontDestroyOnLoad; projéteis reutilizados podem
            // continuar na cena persistente. O snapshot por instance ID separa
            // projéteis preexistentes sem perder esses disparos nem deixá-los vivos
            // para atingir o alvo do próximo teste.
            result.Add(projectile);
        }
        return result;
    }

    private void DestroyNewProjectiles()
    {
        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        foreach (Component projectile in observedProjectiles)
        {
            if (projectile == null) continue;
            if (release != null) release.Invoke(null, new object[] { projectile.gameObject });
            else UnityEngine.Object.Destroy(projectile.gameObject);
        }
    }

    private void CaptureMember(MemberSnapshot snapshot, string name)
    {
        if (snapshot == null || snapshot.component == null) return;
        MemberInfo member = (MemberInfo)snapshot.component.GetType().GetField(name, InstanceMembers)
            ?? snapshot.component.GetType().GetProperty(name, InstanceMembers);
        if (member is FieldInfo field) snapshot.values[name] = field.GetValue(snapshot.component);
        else if (member is PropertyInfo property && property.CanRead) snapshot.values[name] = property.GetValue(snapshot.component);
    }

    private static void CaptureMembers(object target, Dictionary<string, object> values, params string[] names)
    {
        if (target == null || values == null) return;
        for (int i = 0; i < names.Length; i++) values[names[i]] = GetMember(target, names[i]);
    }

    private static void RestoreMembers(object target, Dictionary<string, object> values)
    {
        if (target == null || values == null) return;
        foreach (KeyValuePair<string, object> value in values) SetMember(target, value.Key, value.Value);
    }

    private void RestoreMemberSnapshots()
    {
        for (int i = 0; i < memberSnapshots.Count; i++)
        {
            MemberSnapshot snapshot = memberSnapshots[i];
            if (snapshot.component == null) continue;
            foreach (KeyValuePair<string, object> value in snapshot.values)
                SetMember(snapshot.component, value.Key, value.Value);
        }
    }

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(type => type != null);
    }

    private GameObject FindGameObjectInScene(string hierarchyPath)
    {
        string[] segments = hierarchyPath.Trim('/').Split('/');
        if (!validationScene.IsValid() || segments.Length == 0) return null;
        GameObject[] roots = validationScene.GetRootGameObjects();
        Transform current = null;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == segments[0])
            {
                current = roots[i].transform;
                break;
            }
        }
        if (current == null) return null;
        for (int i = 1; i < segments.Length; i++)
        {
            current = current.Find(segments[i]);
            if (current == null) return null;
        }
        return current.gameObject;
    }

    private static object GetMember(object target, string name)
    {
        if (target == null) return null;
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        return property != null && property.CanRead ? property.GetValue(target) : null;
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) return;
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        if (property != null && property.CanWrite) property.SetValue(target, value);
    }

    private static string DescribeTarget(object target)
    {
        Component component = target as Component;
        return component != null ? component.name + "@" + component.transform.position : "<null>";
    }
}
