using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public sealed class IA03CombatAuthorizationPlayModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const int TeamA = 1;
    private const int TeamB = 3;

    private sealed class BehaviourState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    private sealed class TurretState
    {
        public Behaviour behaviour;
        public object priorityTarget;
        public object currentTarget;
        public object passiveMode;
        public object missilePrefab;
        public object rotatingPart;
        public object barrels;
    }

    private sealed class DiplomacySnapshot
    {
        public object relation;
        public object countryA;
        public object countryB;
        public readonly Dictionary<string, object> relationValues = new Dictionary<string, object>();
        public readonly Dictionary<string, object> countryAValues = new Dictionary<string, object>();
        public readonly Dictionary<string, object> countryBValues = new Dictionary<string, object>();
    }

    private readonly List<BehaviourState> behaviourStates = new List<BehaviourState>();
    private readonly List<TurretState> turretStates = new List<TurretState>();
    private readonly List<Component> projectilesToRelease = new List<Component>();
    private readonly List<string> currentCaseEvents = new List<string>();

    private object government;
    private object relation;
    private DiplomacySnapshot diplomacySnapshot;
    private GameObject shooter;
    private Component originalPatrolComponent;
    private Component unitController;
    private NavMeshAgent navMeshAgent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool originalAgentStopped;
    private string currentCase;
    private bool projectileProbeSubscribed;

    [UnityTest]
    public IEnumerator A_B_C_D_UseRealOrdersAndDoNotPersistSceneChanges()
    {
        if (!string.Equals(SceneManager.GetActiveScene().path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ValidationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Não foi possível carregar a cena de validação sem salvá-la.");
            while (!load.isDone) yield return null;
#else
            Assert.Fail("A cena de validação precisa ser carregada em Play Mode no Editor.");
            yield break;
#endif
        }

        yield return null;

        Scene validationScene = SceneManager.GetActiveScene();
        Assert.That(validationScene.isLoaded, Is.True, "A cena de validação não terminou de carregar.");
        Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
        Assert.That(validationScene.name, Is.EqualTo("IA03_WarValidation"));
        Assert.That(validationScene.isDirty, Is.False, "A cena já estava dirty antes da fixture começar.");
        Assert.That(FindComponentInScene(validationScene, "IA03ValidationDiplomacyBootstrap"), Is.Not.Null,
            "Bootstrap diplomático da validação não encontrado.");
        Assert.That(FindComponentInScene(validationScene, "NavMeshSurface"), Is.Not.Null,
            "NavMeshSurface real da cena de validação não encontrado.");
        Debug.Log("[IA03][CENARIO] scene=" + validationScene.name
            + " path=" + validationScene.path
            + " buildProfileNeeded=false"
            + " isDirtyInicial=" + validationScene.isDirty);

        shooter = FindByPath(validationScene, "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_01");
        GameObject enemyTank = FindByPath(validationScene, "IA03_TestScenario/PaisB_Karsovia_Team3/Unidades_Reais/Karsovia_Tank_01");
        Assert.That(shooter, Is.Not.Null, "Tanque Team 1 não encontrado na cena de validação.");
        Assert.That(enemyTank, Is.Not.Null, "Tanque Team 3 não encontrado na cena de validação.");

        unitController = FindComponent(shooter, "ControleUnidade");
        navMeshAgent = shooter.GetComponent<NavMeshAgent>();
        Assert.That(unitController, Is.Not.Null, "A unidade real precisa ter ControleUnidade.");
        Assert.That(navMeshAgent, Is.Not.Null, "A unidade real precisa ter NavMeshAgent.");

        Type governmentType = ResolveType("SistemaGovernoMundial");
        Assert.That(governmentType, Is.Not.Null);
        government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
        Assert.That(government, Is.Not.Null, "O bootstrap de validação precisa criar o sistema diplomático.");
        relation = Invoke(government, "ObterRelacao", TeamA, TeamB);
        Assert.That(relation, Is.Not.Null, "A relação 1/3 precisa existir no sistema diplomático.");
        diplomacySnapshot = CaptureDiplomacy(government, relation);

        originalPosition = shooter.transform.position;
        originalRotation = shooter.transform.rotation;
        originalAgentStopped = navMeshAgent.isStopped;
        Assert.That(navMeshAgent.isOnNavMesh, Is.True,
            "A unidade escolhida precisa estar sobre o NavMesh real antes dos testes de patrulha.");

        CaptureShooterTurrets(shooter);
        Assert.That(turretStates.Count, Is.GreaterThan(0), "O tanque não contém ControleTorreta.");
        Vector3[] patrolPoints = BuildValidatedPatrol(navMeshAgent);
        Transform attackTarget = FindNearestEnemyInTurretRange(validationScene, shooter, TeamB, turretStates);
        Assert.That(attackTarget, Is.Not.Null,
            "Não há alvo Team 3 ativo dentro do alcance das torretas reais para o teste D.");

        Component patrolBefore = FindComponent(shooter, "ComportamentoPatrulhaUniversal");
        originalPatrolComponent = patrolBefore;

        Application.logMessageReceived += CaptureProbeAndReleaseTestProjectiles;
        projectileProbeSubscribed = true;

        try
        {
            DisableStrategicBrains(validationScene);
            DisableCombatComponentsExcept(validationScene, shooter);
            ForceDiplomacy(false);
            yield return RunPatrolCase("A", false, patrolPoints);

            StopAndRestorePosition();
            ForceDiplomacy(true);
            yield return RunPatrolCase("B", true, patrolPoints);

            StopAndRestorePosition();
            ForceDiplomacy(false);
            yield return RunNoPatrolCase();

            StopAndRestorePosition();
            ForceDiplomacy(false);
            yield return RunAttackCase(attackTarget);
        }
        finally
        {
            if (projectileProbeSubscribed)
            {
                Application.logMessageReceived -= CaptureProbeAndReleaseTestProjectiles;
                projectileProbeSubscribed = false;
            }

            ReleaseWatchedProjectiles();
            StopAndRestorePosition();
            RestoreTurrets();
            RestoreBehaviours();
            RestoreDiplomacy();
            RemovePatrolComponentCreatedByTest();
        }
    }

    private IEnumerator RunPatrolCase(string caseName, bool war, Vector3[] patrolPoints)
    {
        currentCase = caseName;
        currentCaseEvents.Clear();
        ResetEngagementProbes();
        EnableShooterTurrets();

        object command = CreateCommand("Patrol", patrolPoints[0], null, patrolPoints);
        bool accepted = ExecuteOrder(unitController, command);
        bool patrolActive = IsPatrolActive(unitController);
        Debug.Log("[IA03][AB][" + caseName + "][ORDEM] war=" + war
            + " accepted=" + accepted
            + " patrolActive=" + patrolActive
            + " isOnNavMesh=" + navMeshAgent.isOnNavMesh
            + " pathPoints=" + patrolPoints.Length
            + " frame=" + Time.frameCount);

        yield return WaitForShotOrTimeout(3f);
        LogCaseSummary(caseName, war, accepted, patrolActive, null);
        DisableShooterTurrets();
    }

    private IEnumerator RunAttackCase(Transform target)
    {
        currentCase = "D";
        currentCaseEvents.Clear();
        ResetEngagementProbes();
        DisableShooterTurrets();

        Transform[] priorBefore = ReadTargets("alvoPrioritario");
        object command = CreateCommand("Attack", target.position, target, null);
        bool accepted = ExecuteOrder(unitController, command);
        Transform[] priorityAfterDispatch = ReadTargets("alvoPrioritario");
        Debug.Log("[IA03][AB][D][ORDEM] war=false accepted=" + accepted
            + " explicitTarget=" + target.name
            + " priorityBefore=" + DescribeTargets(priorBefore)
            + " priorityAfterExecute=" + DescribeTargets(priorityAfterDispatch)
            + " movementOrder=" + DescribeMember(unitController, "OrdemMovimentoAtual")
            + " frame=" + Time.frameCount);

        EnableShooterTurrets();
        yield return WaitForShotOrTimeout(3f);
        LogCaseSummary("D", false, accepted, IsPatrolActive(unitController), target);
        DisableShooterTurrets();
    }

    private IEnumerator RunNoPatrolCase()
    {
        currentCase = "C";
        currentCaseEvents.Clear();
        ResetEngagementProbes();

        bool stopAccepted = ExecuteOrder(unitController, CreateCommand("Stop", originalPosition, null, null));
        StopAndRestorePosition();
        bool patrolActive = IsPatrolActive(unitController);
        Assert.That(patrolActive, Is.False, "O teste C precisa estar em paz e sem ordem de patrulha ativa.");

        EnableShooterTurrets();
        Debug.Log("[IA03][AB][C][ORDEM] war=false acceptedStop=" + stopAccepted
            + " patrolActive=" + patrolActive
            + " order=" + DescribeMember(unitController, "OrdemAtual")
            + " movementOrder=" + DescribeMember(unitController, "OrdemMovimentoAtual")
            + " frame=" + Time.frameCount);

        yield return WaitForShotOrTimeout(3f);
        LogCaseSummary("C", false, stopAccepted, patrolActive, null);
        DisableShooterTurrets();
    }

    private IEnumerator WaitForShotOrTimeout(float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (HasDischargeOrSpawn()) yield break;
        }
    }

    private void LogCaseSummary(string caseName, bool war, bool orderAccepted, bool patrolActive, Transform explicitTarget)
    {
        bool explicitSelection = ContainsCaseEvent("selector=alvoPrioritario")
            || ContainsCaseEvent("explicitTarget=True");
        bool automaticSelection = ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente");
        int dischargeCount = CountCaseEvents("[DISPARAR_INVOCADO]");
        int projectileCount = CountCaseEvents("[PROJECTILE_PROBE][SPAWN]");

        Debug.Log("[IA03][AB][" + caseName + "][RESULTADO] war=" + war
            + " relationWar=" + GetValue(relation, "guerraDeclarada")
            + " orderAccepted=" + orderAccepted
            + " patrolActive=" + patrolActive
            + " explicitTarget=" + (explicitTarget != null ? explicitTarget.name : "none")
            + " priorityNow=" + DescribeTargets(ReadTargets("alvoPrioritario"))
            + " explicitBranchObserved=" + explicitSelection
            + " automaticBranchObserved=" + automaticSelection
            + " autoCandidateSelected=" + automaticSelection
            + " dischargeEvents=" + dischargeCount
            + " projectileSpawns=" + projectileCount
            + " frame=" + Time.frameCount);

        for (int i = 0; i < currentCaseEvents.Count; i++)
        {
            Debug.Log("[IA03][AB][" + caseName + "][PROBE] " + currentCaseEvents[i]);
        }
    }

    private void CaptureProbeAndReleaseTestProjectiles(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;
        bool isEngagement = condition.IndexOf("[IA03][ENGAGEMENT_PROBE]", StringComparison.Ordinal) >= 0;
        bool isSpawn = condition.IndexOf("[IA03][PROJECTILE_PROBE][SPAWN]", StringComparison.Ordinal) >= 0;
        if (!isEngagement && !isSpawn) return;

        currentCaseEvents.Add(condition);
        if (isSpawn) ReleaseWatchedProjectiles();
    }

    private void ReleaseWatchedProjectiles()
    {
        Type projectileType = ResolveType("Projetil");
        FieldInfo activeField = projectileType != null
            ? projectileType.GetField("ativosNoMapa", StaticMembers)
            : null;
        IEnumerable active = activeField != null ? activeField.GetValue(null) as IEnumerable : null;
        if (active == null) return;

        MethodInfo getOwner = projectileType.GetMethod("GetDono", InstanceMembers);
        foreach (object item in active)
        {
            Component projectile = item as Component;
            if (projectile == null || getOwner == null) continue;
            GameObject owner = getOwner.Invoke(projectile, null) as GameObject;
            if (owner == shooter)
            {
                projectilesToRelease.Add(projectile);
            }
        }

        Type poolType = ResolveType("PoolDeObjetosCombate");
        MethodInfo release = poolType != null
            ? poolType.GetMethod("Release", StaticMembers, null, new[] { typeof(GameObject) }, null)
            : null;
        for (int i = 0; i < projectilesToRelease.Count; i++)
        {
            Component projectile = projectilesToRelease[i];
            if (projectile == null) continue;
            if (release != null) release.Invoke(null, new object[] { projectile.gameObject });
            else projectile.gameObject.SetActive(false);
        }
        projectilesToRelease.Clear();
    }

    private void ForceDiplomacy(bool war)
    {
        SetMember(relation, "guerraDeclarada", war);
        SetMember(relation, "valor", war ? -82 : 0);
        SetMember(relation, "sancaoAtiva", false);
        SetMember(relation, "cessarFogoAtivo", false);
        SetEnumMember(relation, "posturaAParaB", war ? "Inimigo" : "Neutro");
        SetEnumMember(relation, "posturaBParaA", war ? "Inimigo" : "Neutro");

        object countryA = Invoke(government, "ObterPais", TeamA);
        object countryB = Invoke(government, "ObterPais", TeamB);
        SetMember(countryA, "emGuerra", war);
        SetMember(countryA, "rivalTeamId", war ? TeamB : -1);
        SetMember(countryB, "emGuerra", war);
        SetMember(countryB, "rivalTeamId", war ? TeamA : -1);
        Invoke(government, "NotificarGovernoAtualizado");

        Type visibilityType = ResolveType("Hegemonia.RTS.RTSVisibilityService");
        MethodInfo teamsAtWar = visibilityType != null
            ? visibilityType.GetMethod("TeamsAtWar", StaticMembers)
            : null;
        bool actualWar = teamsAtWar != null && (bool)teamsAtWar.Invoke(null, new object[] { TeamA, TeamB });
        Assert.That(actualWar, Is.EqualTo(war), "Diplomacia de teste não assumiu o estado solicitado.");
    }

    private DiplomacySnapshot CaptureDiplomacy(object gov, object relationToCapture)
    {
        DiplomacySnapshot snapshot = new DiplomacySnapshot();
        snapshot.relation = relationToCapture;
        snapshot.countryA = Invoke(gov, "ObterPais", TeamA);
        snapshot.countryB = Invoke(gov, "ObterPais", TeamB);
        CaptureMembers(snapshot.relation, snapshot.relationValues,
            "valor", "guerraDeclarada", "sancaoAtiva", "cessarFogoAtivo", "posturaAParaB", "posturaBParaA");
        CaptureMembers(snapshot.countryA, snapshot.countryAValues, "emGuerra", "rivalTeamId");
        CaptureMembers(snapshot.countryB, snapshot.countryBValues, "emGuerra", "rivalTeamId");
        return snapshot;
    }

    private void RestoreDiplomacy()
    {
        if (diplomacySnapshot == null) return;
        RestoreMembers(diplomacySnapshot.relation, diplomacySnapshot.relationValues);
        RestoreMembers(diplomacySnapshot.countryA, diplomacySnapshot.countryAValues);
        RestoreMembers(diplomacySnapshot.countryB, diplomacySnapshot.countryBValues);
        Invoke(government, "NotificarGovernoAtualizado");
    }

    private void DisableStrategicBrains(Scene scene)
    {
        behaviourStates.Clear();
        CollectBehaviours(scene, delegate(MonoBehaviour component)
        {
            return component.GetType().Name == "IA_BrainMaster"
                || component.GetType().Name == "IA03EstrategaNacional";
        }, true);
    }

    private void DisableCombatComponentsExcept(Scene scene, GameObject allowedShooter)
    {
        CollectBehaviours(scene, delegate(MonoBehaviour component)
        {
            string name = component.GetType().Name;
            return name == "ControleTorreta"
                || name == "ControleTorretaModular"
                || name == "SistemaDeTiro"
                || name == "Torreta"
                || name == "TorretaAntiaerea";
        }, false);

        for (int i = 0; i < behaviourStates.Count; i++)
        {
            Behaviour component = behaviourStates[i].behaviour;
            if (component == null) continue;
            bool belongsToShooter = component.gameObject == allowedShooter
                || component.transform.IsChildOf(allowedShooter.transform);
            component.enabled = belongsToShooter && behaviourStates[i].wasEnabled;
        }
    }

    private void CollectBehaviours(Scene scene, Predicate<MonoBehaviour> predicate, bool disableImmediately)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) CollectBehavioursRecursive(roots[i].transform, predicate, disableImmediately);
    }

    private void CollectBehavioursRecursive(Transform parent, Predicate<MonoBehaviour> predicate, bool disableImmediately)
    {
        MonoBehaviour[] components = parent.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null || !predicate(component)) continue;
            behaviourStates.Add(new BehaviourState { behaviour = component, wasEnabled = component.enabled });
            if (disableImmediately) component.enabled = false;
        }

        for (int i = 0; i < parent.childCount; i++)
            CollectBehavioursRecursive(parent.GetChild(i), predicate, disableImmediately);
    }

    private void CaptureShooterTurrets(GameObject unit)
    {
        MonoBehaviour[] components = unit.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null || component.GetType().Name != "ControleTorreta") continue;
            turretStates.Add(new TurretState
            {
                behaviour = component,
                priorityTarget = GetMember(component, "alvoPrioritario"),
                currentTarget = GetMember(component, "alvoAtual"),
                passiveMode = GetMember(component, "modoPassivo"),
                missilePrefab = GetMember(component, "misselPrefab"),
                rotatingPart = GetMember(component, "pecaQueGira"),
                barrels = GetMember(component, "locaisDoTiro")
            });
        }
    }

    private void EnableShooterTurrets()
    {
        for (int i = 0; i < turretStates.Count; i++)
        {
            Behaviour component = turretStates[i].behaviour;
            if (component == null) continue;
            SetMember(component, "misselPrefab", null);
            component.enabled = true;
        }
    }

    private void DisableShooterTurrets()
    {
        for (int i = 0; i < turretStates.Count; i++)
        {
            Behaviour component = turretStates[i].behaviour;
            if (component != null) component.enabled = false;
        }
    }

    private void RestoreTurrets()
    {
        DisableShooterTurrets();
        for (int i = 0; i < turretStates.Count; i++)
        {
            TurretState state = turretStates[i];
            if (state.behaviour == null) continue;
            MethodInfo setTarget = state.behaviour.GetType().GetMethod("DefinirAlvo", InstanceMembers);
            if (setTarget != null) setTarget.Invoke(state.behaviour, new[] { state.currentTarget });
            SetMember(state.behaviour, "alvoPrioritario", state.priorityTarget);
            SetMember(state.behaviour, "modoPassivo", state.passiveMode);
            SetMember(state.behaviour, "misselPrefab", state.missilePrefab);
            SetMember(state.behaviour, "pecaQueGira", state.rotatingPart);
            SetMember(state.behaviour, "locaisDoTiro", state.barrels);
        }
    }

    private void RestoreBehaviours()
    {
        for (int i = behaviourStates.Count - 1; i >= 0; i--)
        {
            BehaviourState state = behaviourStates[i];
            if (state.behaviour != null) state.behaviour.enabled = state.wasEnabled;
        }
    }

    private void ResetEngagementProbes()
    {
        InvokeStaticIfPresent("ControleTorreta", "ResetarProbeEngajamento");
        InvokeStaticIfPresent("Projetil", "ResetarProbeDisparo");
    }

    private void StopAndRestorePosition()
    {
        if (unitController != null)
        {
            try
            {
                object stop = CreateCommand("Stop", originalPosition, null, null);
                ExecuteOrder(unitController, stop);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[IA03][AB][Teardown] Não foi possível emitir Stop: " + exception.GetType().Name);
            }
        }

        if (shooter != null)
        {
            shooter.transform.SetPositionAndRotation(originalPosition, originalRotation);
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Warp(originalPosition);
                navMeshAgent.isStopped = originalAgentStopped;
            }
        }
    }

    private void RemovePatrolComponentCreatedByTest()
    {
        if (shooter == null || originalPatrolComponent != null) return;
        Component patrol = FindComponent(shooter, "ComportamentoPatrulhaUniversal");
        if (patrol != null) UnityEngine.Object.Destroy(patrol);
    }

    private Vector3[] BuildValidatedPatrol(NavMeshAgent agent)
    {
        Vector3 origin = agent.nextPosition;
        if (!agent.isOnNavMesh) origin = agent.transform.position;
        Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        for (int d = 0; d < directions.Length; d++)
        {
            for (int distance = 6; distance <= 30; distance += 6)
            {
                Vector3 requested = origin + directions[d] * distance;
                if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 8f, agent.areaMask)) continue;
                if ((hit.position - origin).sqrMagnitude < 9f) continue;
                if (!PathIsComplete(origin, hit.position, agent.areaMask)) continue;

                Vector3 secondRequested = origin - directions[d] * distance;
                if (!NavMesh.SamplePosition(secondRequested, out NavMeshHit second, 8f, agent.areaMask)) continue;
                if ((second.position - hit.position).sqrMagnitude < 9f) continue;
                if (!PathIsComplete(origin, second.position, agent.areaMask)) continue;
                if (!PathIsComplete(hit.position, second.position, agent.areaMask)) continue;
                return new[] { hit.position, second.position };
            }
        }

        Assert.Fail("Não foi possível criar dois waypoints no mesmo NavMesh com caminhos completos.");
        return Array.Empty<Vector3>();
    }

    private static bool PathIsComplete(Vector3 from, Vector3 to, int areaMask)
    {
        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, areaMask, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private Transform FindNearestEnemyInTurretRange(Scene scene, GameObject unit, int targetTeam, List<TurretState> turrets)
    {
        Type identityType = ResolveType("IdentidadeUnidade");
        if (identityType == null) return null;
        float maxRange = 0f;
        for (int i = 0; i < turrets.Count; i++)
        {
            object value = GetMember(turrets[i].behaviour, "alcance");
            if (value != null) maxRange = Mathf.Max(maxRange, Convert.ToSingle(value));
        }

        float bestDistance = maxRange * maxRange;
        Transform best = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Component[] identities = roots[i].GetComponentsInChildren(identityType, true);
            for (int j = 0; j < identities.Length; j++)
            {
                Component identity = identities[j];
                if (identity == null || !identity.gameObject.activeInHierarchy || identity.gameObject == unit) continue;
                if (Convert.ToInt32(GetMember(identity, "teamID")) != targetTeam) continue;
                float distance = (identity.transform.position - unit.transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = identity.transform;
            }
        }
        return best;
    }

    private Transform[] ReadTargets(string fieldName)
    {
        Transform[] targets = new Transform[turretStates.Count];
        for (int i = 0; i < turretStates.Count; i++)
            targets[i] = GetMember(turretStates[i].behaviour, fieldName) as Transform;
        return targets;
    }

    private static string DescribeTargets(Transform[] targets)
    {
        if (targets == null || targets.Length == 0) return "none";
        string result = string.Empty;
        for (int i = 0; i < targets.Length; i++)
        {
            if (i > 0) result += ",";
            result += targets[i] != null ? targets[i].name : "none";
        }
        return result;
    }

    private static string DescribeMember(object target, string name)
    {
        object value = GetMember(target, name);
        if (value == null) return "none";
        object nested = GetMember(value, "Tipo");
        return nested != null ? nested.ToString() : value.ToString();
    }

    private static bool IsPatrolActive(object control)
    {
        object order = GetMember(control, "OrdemAtual");
        object moveOrder = GetMember(control, "OrdemMovimentoAtual");
        return (order != null && order.ToString().IndexOf("Patrul", StringComparison.OrdinalIgnoreCase) >= 0)
            || (moveOrder != null && moveOrder.ToString().IndexOf("Patrul", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private bool HasDischargeOrSpawn()
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
        {
            if (currentCaseEvents[i].IndexOf("[DISPARAR_INVOCADO]", StringComparison.Ordinal) >= 0
                || currentCaseEvents[i].IndexOf("[PROJECTILE_PROBE][SPAWN]", StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private int CountCaseEvents(string fragment)
    {
        int count = 0;
        for (int i = 0; i < currentCaseEvents.Count; i++)
            if (currentCaseEvents[i].IndexOf(fragment, StringComparison.Ordinal) >= 0) count++;
        return count;
    }

    private bool ContainsCaseEvent(string fragment)
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
            if (currentCaseEvents[i].IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static object CreateCommand(string orderName, Vector3 destination, Transform target, Vector3[] patrolPoints)
    {
        Type orderType = ResolveType("Hegemonia.RTS.RTSOrderType");
        Type commandType = ResolveType("Hegemonia.RTS.RTSOrderCommand");
        Assert.That(orderType, Is.Not.Null);
        Assert.That(commandType, Is.Not.Null);
        object enumValue = Enum.Parse(orderType, orderName);
        ConstructorInfo constructor = commandType.GetConstructor(new[] { orderType, typeof(Vector3), typeof(Transform) });
        object command = constructor.Invoke(new[] { enumValue, (object)destination, target });
        FieldInfo points = commandType.GetField("patrolPoints", InstanceMembers);
        if (points != null) points.SetValue(command, patrolPoints);
        return command;
    }

    private static bool ExecuteOrder(object control, object command)
    {
        Type dispatcher = ResolveType("Hegemonia.RTS.RTSOrderDispatcher");
        MethodInfo execute = dispatcher != null ? dispatcher.GetMethod("Execute", StaticMembers) : null;
        Assert.That(execute, Is.Not.Null);
        return (bool)execute.Invoke(null, new[] { control, command });
    }

    private static GameObject FindByPath(Scene scene, string relativePath)
    {
        string[] parts = relativePath.Split('/');
        GameObject[] roots = scene.GetRootGameObjects();
        Transform current = null;
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == parts[0]) { current = roots[i].transform; break; }
        for (int p = 1; current != null && p < parts.Length; p++)
        {
            Transform next = null;
            for (int c = 0; c < current.childCount; c++)
                if (current.GetChild(c).name == parts[p]) { next = current.GetChild(c); break; }
            current = next;
        }
        return current != null ? current.gameObject : null;
    }

    private static Component FindComponentInScene(Scene scene, string typeName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Component found = FindComponentRecursive(roots[i].transform, typeName);
            if (found != null) return found;
        }
        return null;
    }

    private static Component FindComponentRecursive(Transform parent, string typeName)
    {
        Component[] components = parent.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && components[i].GetType().Name == typeName) return components[i];

        for (int i = 0; i < parent.childCount; i++)
        {
            Component found = FindComponentRecursive(parent.GetChild(i), typeName);
            if (found != null) return found;
        }
        return null;
    }

    private static Component FindComponent(GameObject gameObject, string typeName)
    {
        if (gameObject == null) return null;
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && components[i].GetType().Name == typeName) return components[i];
        return null;
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

    private static object Invoke(object target, string methodName, params object[] args)
    {
        if (target == null) return null;
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
        return method != null ? method.Invoke(target, args) : null;
    }

    private static object GetMember(object target, string name)
    {
        if (target == null) return null;
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        return property != null && property.CanRead ? property.GetValue(target) : null;
    }

    private static object GetValue(object target, string name)
    {
        return GetMember(target, name);
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) return;
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) { field.SetValue(target, value); return; }
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null && property.CanWrite) property.SetValue(target, value);
    }

    private static void SetEnumMember(object target, string name, string value)
    {
        if (target == null) return;
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null && field.FieldType.IsEnum) field.SetValue(target, Enum.Parse(field.FieldType, value));
    }

    private static void CaptureMembers(object target, Dictionary<string, object> destination, params string[] names)
    {
        for (int i = 0; i < names.Length; i++) destination[names[i]] = GetMember(target, names[i]);
    }

    private static void RestoreMembers(object target, Dictionary<string, object> values)
    {
        foreach (KeyValuePair<string, object> entry in values)
        {
            FieldInfo field = target.GetType().GetField(entry.Key, InstanceMembers);
            if (field != null && field.FieldType.IsEnum && entry.Value != null)
            {
                SetEnumMember(target, entry.Key, entry.Value.ToString());
            }
            else
            {
                SetMember(target, entry.Key, entry.Value);
            }
        }
    }

    private static void InvokeStaticIfPresent(string typeName, string methodName)
    {
        Type type = ResolveType(typeName);
        MethodInfo method = type != null ? type.GetMethod(methodName, StaticMembers) : null;
        method?.Invoke(null, null);
    }
}
