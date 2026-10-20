using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
    private static AsyncOperation pendingValidationSceneUnload;
    private static GameObject testOnlyVisibilityServiceObject;
    private bool restoreSceneAfterBrainMasterRegression;
    private string originalScenePathForBrainMasterRegression;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const int TeamA = 1;
    private const int TeamB = 3;
    private const string FixtureBuildMarker = "IA03_AUTH_FIXTURE_PREUPDATE_V3";
    private const int MaxProbeLinesPerCase = 16;

    private sealed class BehaviourState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    private sealed class TurretState
    {
        public Behaviour behaviour;
        public int instanceId;
        public float range;
        public bool wasEnabled;
        public object priorityTarget;
        public object currentTarget;
        public object passiveMode;
        public object missilePrefab;
        public object rotatingPart;
        public object barrels;
        public readonly Dictionary<string, object> firingValues = new Dictionary<string, object>();
    }

    private sealed class TargetCandidate
    {
        public Transform target;
        public Component identity;
        public float distanceSqr;
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
    private readonly RaycastHit[] projectileTraceBuffer = new RaycastHit[32];

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
    private bool sceneLoadedCallbackSubscribed;
    private bool isolationArmed;
    private bool unexpectedActivityBeforeIsolation;
    private bool originalUnitStateCaptured;
    private bool diplomacyWasModified;
    private bool writingProbeSummary;
    private bool keepCaseProjectileForImpact;
    private string preIsolationEvidence;
    private string currentN2MissionTargetIdentityMarker;
    private string currentN2ShooterIdentityMarker;
    private TurretState selectedTurretState;
    private string automaticPeaceTargetFromC;
    private Scene validationSceneLoadedForCleanup;

    [UnityTest]
    public IEnumerator A_B_C_D_UseRealOrdersAndDoNotPersistSceneChanges()
    {
        yield return RunCombatAuthorizationScenario(false);
    }

    [UnityTest]
    public IEnumerator D_ExplicitAttackInPeace_UsesPriorityBeforeAutomaticGate()
    {
        yield return RunCombatAuthorizationScenario(true);
    }

    [UnityTest]
    public IEnumerator BrainMasterAttackInNeutralPeace_PropagatesExplicitTargetPriority()
    {
        yield return WaitForPendingValidationSceneUnload();
        ResetScenarioState();
        Scene originalScene = SceneManager.GetActiveScene();
        originalScenePathForBrainMasterRegression = originalScene.IsValid() ? originalScene.path : string.Empty;
        restoreSceneAfterBrainMasterRegression = !string.IsNullOrWhiteSpace(originalScenePathForBrainMasterRegression)
            && !string.Equals(originalScenePathForBrainMasterRegression, ValidationScenePath, StringComparison.OrdinalIgnoreCase);

        Component teamABrain = null;
        Application.logMessageReceived += CaptureProbeAndReleaseTestProjectiles;
        projectileProbeSubscribed = true;
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        sceneLoadedCallbackSubscribed = true;

        try
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ValidationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Não foi possível carregar a fixture IA03 como cena única.");
            while (!load.isDone) yield return null;
#else
            Assert.Fail("A fixture IA03 precisa ser carregada em Play Mode no Editor.");
            yield break;
#endif

            yield return null;
            Scene validationScene = SceneManager.GetActiveScene();
            Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "A regressão deve rodar somente com IA03_WarValidation carregada.");
            Assert.That(isolationArmed, Is.True, "A fixture não isolou os comportamentos antes do primeiro Update.");
            Assert.That(validationScene.isDirty, Is.False, "A fixture começou com alterações não salvas.");

            Type governmentType = ResolveType("SistemaGovernoMundial");
            Assert.That(governmentType, Is.Not.Null);
            government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
            Assert.That(government, Is.Not.Null, "O bootstrap da fixture precisa inicializar o sistema diplomático.");
            relation = Invoke(government, "ObterRelacao", TeamA, TeamB);
            Assert.That(relation, Is.Not.Null, "A relação diplomática Team 1/Team 3 precisa existir.");
            diplomacySnapshot = CaptureDiplomacy(government, relation);
            diplomacyWasModified = true;
            ForceDiplomacy(false);

            Transform attackTarget;
            Component chosenTurret;
            string selectionSummary;
            bool querySaturated;
            bool selected = TrySelectShooterAndTarget(validationScene, out shooter, out unitController,
                out navMeshAgent, out chosenTurret, out attackTarget, out selectionSummary, out querySaturated);
            Assert.That(querySaturated, Is.False, "O buffer de colisores saturou; regressão inconclusiva. " + selectionSummary);
            Assert.That(selected, Is.True, "Não foi possível selecionar tanque, torreta e alvo reais. " + selectionSummary);

            originalPosition = shooter.transform.position;
            originalRotation = shooter.transform.rotation;
            originalAgentStopped = navMeshAgent.isStopped;
            originalUnitStateCaptured = true;
            CaptureShooterTurrets(shooter);
            selectedTurretState = FindTurretState(chosenTurret);
            Assert.That(selectedTurretState, Is.Not.Null);
            Assert.That(turretStates.Count, Is.GreaterThanOrEqualTo(2),
                "A unidade precisa manter as duas ControleTorreta reais da fixture.");
            Assert.That(unitController, Is.Not.Null, "A unidade selecionada precisa ter ControleUnidade.");

            Component targetIdentity = FindComponentRecursive(attackTarget, "IdentidadeUnidade");
            Assert.That(targetIdentity, Is.Not.Null, "O alvo Team 3 precisa ter IdentidadeUnidade real.");
            Assert.That(Convert.ToInt32(GetMember(targetIdentity, "teamID")), Is.EqualTo(TeamB));
            bool automaticWouldRefuse = !Convert.ToBoolean(Invoke(chosenTurret, "PodeAtacarAutomaticamente", targetIdentity));
            Assert.That(automaticWouldRefuse, Is.True,
                "A fixture precisa provar que o gate automático recusa o alvo enquanto a relação está em paz neutra.");

            teamABrain = FindBrainMasterForTeam(validationScene, TeamA);
            Assert.That(teamABrain, Is.Not.Null, "BrainMaster Team 1 não encontrado na fixture real.");
            object context = GetMember(teamABrain, "Context");
            object commandQueue = GetMember(context, "CommandQueue");
            Assert.That(context, Is.Not.Null, "BrainMaster não construiu o contexto de runtime.");
            Assert.That(commandQueue, Is.Not.Null, "BrainMaster não construiu a fila de comandos.");
            Assert.That(Convert.ToString(GetMember(teamABrain, "IntegrationMode")), Is.Not.EqualTo("ShadowReadOnly"));

            for (int i = 0; i < turretStates.Count; i++)
            {
                Assert.That(GetMember(turretStates[i].behaviour, "alvoPrioritario"), Is.Null,
                    "A unidade deve começar sem prioridade explícita antes do comando BrainMaster.");
            }

            Type payloadType = ResolveType("Hegemonia.AI.BrainMaster.IA_AttackOrderData");
            Type commandType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType");
            Type commandFactoryType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandFactory");
            Assert.That(payloadType, Is.Not.Null);
            Assert.That(commandType, Is.Not.Null);
            Assert.That(commandFactoryType, Is.Not.Null);
            object payload = Activator.CreateInstance(payloadType);
            SetMember(payload, "Units", new List<GameObject> { shooter });
            SetMember(payload, "Target", attackTarget);
            SetMember(payload, "TargetPosition", attackTarget.position);
            MethodInfo createRequest = commandFactoryType.GetMethod("Create", StaticMembers);
            Assert.That(createRequest, Is.Not.Null);
            object request = createRequest.Invoke(null, new object[]
            {
                Enum.Parse(commandType, "Attack"),
                "IA03EstrategaNacional",
                "estrategico",
                "regressão de prioridade explícita BrainMaster",
                1000,
                "tactical",
                "brainmaster-priority-regression-" + Guid.NewGuid().ToString("N"),
                0f,
                payload,
                null
            });
            Assert.That(request, Is.Not.Null);

            Behaviour brainBehaviour = teamABrain as Behaviour;
            Assert.That(brainBehaviour, Is.Not.Null);
            brainBehaviour.enabled = true;
            Assert.That(Convert.ToBoolean(GetMember(teamABrain, "HasExecutionAuthority")), Is.True,
                "BrainMaster Team 1 precisa recuperar a autoridade de execução antes de receber o comando.");
            MethodInfo enqueueMethod = commandQueue.GetType().GetMethod("Enqueue", InstanceMembers);
            MethodInfo getStatusMethod = commandQueue.GetType().GetMethod("GetStatus", InstanceMembers);
            Assert.That(enqueueMethod, Is.Not.Null);
            Assert.That(getStatusMethod, Is.Not.Null);
            object[] enqueueArguments = { request, Time.time, null };
            bool enqueued = Convert.ToBoolean(enqueueMethod.Invoke(commandQueue, enqueueArguments));
            string enqueueReason = Convert.ToString(enqueueArguments[2]);
            Assert.That(enqueued, Is.True, "BrainMaster recusou enfileirar o Attack: " + enqueueReason);

            string requestId = Convert.ToString(GetMember(request, "Id"));
            float commandDeadline = Time.realtimeSinceStartup + 8f;
            object commandStatus = getStatusMethod.Invoke(commandQueue, new object[] { requestId });
            while (!string.Equals(Convert.ToString(commandStatus), "Success", StringComparison.Ordinal)
                   && !string.Equals(Convert.ToString(commandStatus), "Failed", StringComparison.Ordinal)
                   && Time.realtimeSinceStartup < commandDeadline)
            {
                yield return null;
                commandStatus = getStatusMethod.Invoke(commandQueue, new object[] { requestId });
            }

            Assert.That(Convert.ToString(commandStatus), Is.EqualTo("Success"),
                "A fila real do BrainMaster não aceitou o Attack. status=" + commandStatus);
            Debug.Log("[IA03][BRAINMASTER_PRIORITY_TEST][ORDER] id=" + requestId
                + " shooter=" + shooter.name + " target=" + attackTarget.name
                + " status=" + commandStatus + " gateAutomaticWouldRefuse=" + automaticWouldRefuse);

            for (int i = 0; i < turretStates.Count; i++)
            {
                Assert.That(GetMember(turretStates[i].behaviour, "alvoPrioritario"), Is.SameAs(attackTarget),
                    "A API agregadora não propagou o alvo do BrainMaster para " + turretStates[i].behaviour.name + ".");
            }

            MonoBehaviour[] shooterBehaviours = shooter.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < shooterBehaviours.Length; i++)
            {
                if (shooterBehaviours[i] == null || shooterBehaviours[i].GetType().Name != "ControleTorretaModular") continue;
                Assert.That(GetMember(shooterBehaviours[i], "alvoPrioritario"), Is.SameAs(attackTarget),
                    "A prioridade explícita não chegou à torreta modular " + shooterBehaviours[i].name + ".");
            }

            // ArmUnitForAttack configura alvoAtual diretamente para preparar o disparo.
            // Limpe só esse estado transitório para validar que o Update escolhe pela prioridade.
            currentCase = "BRAINMASTER";
            Invoke(chosenTurret, "DefinirAlvo", (object)null);
            // A fixture pode chegar aqui após outro cenário que deixou o
            // executor em modo ocioso. Reafirme apenas o estado operacional
            // que o comando BrainMaster deveria ter produzido; não reaplique
            // a prioridade nem o alvo, que continuam sendo a parte testada.
            Invoke(unitController, "DefinirModoCombate", true);
            Invoke(chosenTurret, "DefinirModoAtivo", true);
            selectedTurretState.behaviour.enabled = true;
            float probeDeadline = Time.realtimeSinceStartup + 3f;
            while (!ContainsCaseEvent("selector=alvoPrioritario")
                   && Time.realtimeSinceStartup < probeDeadline)
            {
                yield return null;
            }

            string explicitProbe = null;
            for (int i = 0; i < currentCaseEvents.Count; i++)
            {
                if (currentCaseEvents[i].IndexOf("selector=alvoPrioritario", StringComparison.Ordinal) >= 0)
                {
                    explicitProbe = currentCaseEvents[i];
                    break;
                }
            }

            Assert.That(explicitProbe, Is.Not.Null, "O Update real da torreta não selecionou o alvo pela prioridade.");
            Assert.That(explicitProbe, Does.Contain("explicitTarget=True"), explicitProbe);
            Assert.That(GetMember(chosenTurret, "alvoAtual"), Is.SameAs(attackTarget));
            Debug.Log("[IA03][BRAINMASTER_PRIORITY_TEST][PROBE] " + explicitProbe);
        }
        finally
        {
            UnsubscribeSceneLoadedCallback();
            if (projectileProbeSubscribed)
            {
                Application.logMessageReceived -= CaptureProbeAndReleaseTestProjectiles;
                projectileProbeSubscribed = false;
            }

            ReleaseWatchedProjectiles(true);
            if (teamABrain is Behaviour brainBehaviour) brainBehaviour.enabled = false;
            if (originalUnitStateCaptured) StopAndRestorePosition();
            RestoreTurrets();
            if (diplomacyWasModified) RestoreDiplomacy();
            ResetEngagementProbes();
        }
    }

    [Test]
    public void TorpedoOnlySubmarineLaunchesAtNavalTargetInsideConfiguredRange()
    {
        Type submarineType = ResolveType("ControleSubmarino");
        Type torpedoType = ResolveType("Torpedo");
        Assert.That(submarineType, Is.Not.Null);
        Assert.That(torpedoType, Is.Not.Null);

        GameObject submarineObject = new GameObject("IA03 torpedo-only submarine runtime test");
        GameObject tubeObject = new GameObject("IA03 torpedo tube runtime test");
        GameObject targetObject = new GameObject("IA03 naval target runtime test");
        GameObject torpedoPrefab = new GameObject("IA03 torpedo runtime test prefab");
        GameObject launchedTorpedo = null;
        try
        {
            tubeObject.transform.SetParent(submarineObject.transform, false);
            targetObject.transform.position = submarineObject.transform.position + Vector3.forward * 20f;
            targetObject.AddComponent(submarineType);
            torpedoPrefab.AddComponent(torpedoType);

            Component submarine = submarineObject.AddComponent(submarineType);
            SetMember(submarine, "tubosTorpedo", new[] { tubeObject.transform });
            SetMember(submarine, "prefabTorpedo", torpedoPrefab);
            SetMember(submarine, "torpedosDisponiveis", 1);
            SetMember(submarine, "alcanceTorpedos", 100f);
            SetEnumMember(submarine, "modoAtual", "Automatico");
            SetMember(submarine, "tubosTorpedoUsados", new[] { false });

            bool launched = Convert.ToBoolean(Invoke(submarine, "TentarDispararTorpedoIA", targetObject.transform));
            launchedTorpedo = GameObject.Find(torpedoPrefab.name + "(Clone)");

            Assert.That(launched, Is.True, "O submarino deve consumir um tubo quando o alvo naval está no alcance.");
            Assert.That(Convert.ToInt32(GetMember(submarine, "torpedosDisponiveis")), Is.EqualTo(0));
            Assert.That(launchedTorpedo, Is.Not.Null, "A ordem precisa instanciar o projétil de torpedo configurado.");
            Assert.That(launchedTorpedo.GetComponent(torpedoType), Is.Not.Null);
        }
        finally
        {
            if (launchedTorpedo != null) UnityEngine.Object.DestroyImmediate(launchedTorpedo);
            UnityEngine.Object.DestroyImmediate(torpedoPrefab);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(tubeObject);
            UnityEngine.Object.DestroyImmediate(submarineObject);
        }
    }

    [UnityTest]
    public IEnumerator N2_AttackMission_MaintainsTurretAimThroughChassisMovementMultipleShotsAndCleanup()
    {
        yield return WaitForPendingValidationSceneUnload();
        ResetScenarioState();

        Scene originalScene = SceneManager.GetActiveScene();
        originalScenePathForBrainMasterRegression = originalScene.IsValid() ? originalScene.path : string.Empty;
        restoreSceneAfterBrainMasterRegression = !string.IsNullOrWhiteSpace(originalScenePathForBrainMasterRegression)
            && !string.Equals(originalScenePathForBrainMasterRegression, ValidationScenePath, StringComparison.OrdinalIgnoreCase);
        Application.logMessageReceived += CaptureProbeAndReleaseTestProjectiles;
        projectileProbeSubscribed = true;
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        sceneLoadedCallbackSubscribed = true;

        Component team1Brain = null;
        Component team1Strategy = null;
        Behaviour team1StrategyBehaviour = null;
        object activeAttackMission = null;
        object originalAttackMissionPriority = null;
        bool attackMissionPriorityChanged = false;
        object reservedCreaty = null;
        Transform missionTarget = null;
        Transform expectedMissionTarget = null;
        Transform automaticDecoyRoot = null;
        Transform automaticDecoyTarget = null;
        GameObject automaticDecoyInstance = null;
        NavMeshAgent automaticDecoyAgent = null;
        Vector3 automaticDecoyOriginalPosition = Vector3.zero;
        bool automaticDecoyPositionCaptured = false;
        bool automaticDecoyAgentStopped = false;
        bool automaticDecoyAgentStateCaptured = false;
        NavMeshAgent missionTargetAgent = null;
        Vector3 missionTargetOriginalPosition = Vector3.zero;
        bool missionTargetPositionCaptured = false;
        bool missionTargetAgentStopped = false;
        bool missionTargetAgentStateCaptured = false;
        Behaviour missionTargetPatrol = null;
        bool missionTargetPatrolEnabled = false;
        bool missionTargetPatrolStateCaptured = false;
        Behaviour missionTargetController = null;
        bool missionTargetControllerEnabled = false;
        bool missionTargetControllerStateCaptured = false;
        Component visibilityService = null;
        int injectedTargetInstanceId = 0;
        bool visibilityContactInjected = false;
        bool visibilityServiceEnabledBefore = false;
        bool visibilityServiceStateCaptured = false;
        object originalStrategyActiveValue = null;
        bool strategyActiveStateCaptured = false;
        object team1CountryForReadiness = null;
        Dictionary<string, object> team1CountryReadinessState = new Dictionary<string, object>();
        bool team1CountryReadinessStateCaptured = false;
        object team1ResourcesForReadiness = null;
        Dictionary<string, object> team1ResourceManagerReadinessState = new Dictionary<string, object>();
        bool team1ResourceManagerReadinessStateCaptured = false;
        object readinessProductionManager = null;
        Dictionary<string, object> readinessProductionManagerState = new Dictionary<string, object>();
        IList readinessBarracksPoints = null;
        ArrayList readinessBarracksPointsBefore = null;
        IList readinessHangarPoints = null;
        ArrayList readinessHangarPointsBefore = null;
        GameObject temporaryReadinessFactoryObject = null;
        object industrialForReadiness = null;
        GameObject temporaryIndustrialReadinessObject = null;
        object industrialReadinessSave = null;
        double originalTeam1Diesel = 0d;
        double officialDieselReserve = 0d;
        string readinessDieselId = null;

        try
        {
#if UNITY_EDITOR
            Scene sceneBeforeN2Load = SceneManager.GetActiveScene();
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][SCENE_LOAD_BEGIN] active="
                + (sceneBeforeN2Load.IsValid() ? sceneBeforeN2Load.path : "<invalid>")
                + " sceneCount=" + SceneManager.sceneCount
                + " validationAlreadyLoaded=" + SceneManager.GetSceneByPath(ValidationScenePath).isLoaded
                + " callbackSubscribed=" + sceneLoadedCallbackSubscribed
                + " isolationArmed=" + isolationArmed);
            Scene existingValidationScene = SceneManager.GetSceneByPath(ValidationScenePath);
            if (existingValidationScene.IsValid() && existingValidationScene.isLoaded)
            {
                // Single-load keeps an already loaded active scene and does not
                // necessarily emit sceneLoaded for it. Isolate synchronously so
                // an inherited validation scene cannot run IA/combat before N2.
                IsolateValidationScene(existingValidationScene);
                // The snapshot above belongs to the scene that Single-load is
                // about to replace. Re-arm the callback so it also isolates the
                // incoming instance before its first Update.
                isolationArmed = false;
            }
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ValidationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Não foi possível carregar a fixture IA03 como cena única.");
            while (!load.isDone) yield return null;
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][SCENE_LOAD_END] active="
                + SceneManager.GetActiveScene().path
                + " sceneCount=" + SceneManager.sceneCount
                + " validationLoaded=" + SceneManager.GetSceneByPath(ValidationScenePath).isLoaded
                + " isolationArmed=" + isolationArmed);
#else
            Assert.Fail("A fixture IA03 precisa ser carregada em Play Mode no Editor.");
            yield break;
#endif

            yield return null;
            Scene validationScene = SceneManager.GetActiveScene();
            Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "N2 deve rodar somente com IA03_WarValidation carregada.");
            Assert.That(isolationArmed, Is.True, "Os comportamentos não foram isolados antes do primeiro Update.");
            Assert.That(validationScene.isDirty, Is.False, "A fixture não pode iniciar com alterações de cena.");

            team1Brain = FindBrainMasterForTeam(validationScene, TeamA);
            Component team3Brain = FindBrainMasterForTeam(validationScene, TeamB);
            team1Strategy = FindComponentInScene(validationScene, "IA03EstrategaNacional");
            Assert.That(team1Brain, Is.Not.Null, "BrainMaster Team 1 não encontrado.");
            Assert.That(team3Brain, Is.Not.Null, "BrainMaster Team 3 não encontrado.");
            Assert.That(team1Strategy, Is.Not.Null, "IA03 Team 1 não encontrada.");
            team1StrategyBehaviour = team1Strategy as Behaviour;
            Assert.That(team1StrategyBehaviour, Is.Not.Null);

            Type productionManagerType = ResolveType("GerenteDeJogo");
            readinessProductionManager = productionManagerType != null
                ? productionManagerType.GetField("Instancia", StaticMembers)?.GetValue(null)
                : null;
            if (readinessProductionManager != null)
            {
                CaptureMembers(readinessProductionManager, readinessProductionManagerState,
                    "spawnSoldado", "saidaSoldado", "listaQuarteis", "spawnInterno", "pontoSaida", "listaHangares");
                readinessBarracksPoints = GetMember(readinessProductionManager, "listaQuarteis") as IList;
                if (readinessBarracksPoints != null)
                {
                    readinessBarracksPointsBefore = new ArrayList(readinessBarracksPoints.Count);
                    for (int i = 0; i < readinessBarracksPoints.Count; i++)
                        readinessBarracksPointsBefore.Add(readinessBarracksPoints[i]);
                }
                readinessHangarPoints = GetMember(readinessProductionManager, "listaHangares") as IList;
                if (readinessHangarPoints != null)
                {
                    readinessHangarPointsBefore = new ArrayList(readinessHangarPoints.Count);
                    for (int i = 0; i < readinessHangarPoints.Count; i++)
                        readinessHangarPointsBefore.Add(readinessHangarPoints[i]);
                }
            }

            Type governmentType = ResolveType("SistemaGovernoMundial");
            Assert.That(governmentType, Is.Not.Null);
            government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
            Assert.That(government, Is.Not.Null, "O bootstrap da fixture precisa inicializar a diplomacia.");
            relation = Invoke(government, "ObterRelacao", TeamA, TeamB);
            Assert.That(relation, Is.Not.Null);
            diplomacySnapshot = CaptureDiplomacy(government, relation);
            diplomacyWasModified = true;
            ForceDiplomacy(true);

            // A missão N2 será iniciada pela API real da IA03. A seleção/percepção
            // já tem regressão própria; aqui o teste isola o elo da arma com um alvo
            // vivo da própria fixture, após a unidade estar reservada.
            ((Behaviour)team1Brain).enabled = true;
            ((Behaviour)team3Brain).enabled = false;
            object brainContext = GetMember(team1Brain, "Context");
            object worldState = GetMember(brainContext, "WorldState");
            float ownedUnitsDeadline = Time.realtimeSinceStartup + 20f;
            IList ownedCombatUnits = GetMember(worldState, "OwnCombatUnits") as IList;
            while ((ownedCombatUnits == null || ownedCombatUnits.Count == 0)
                   && Time.realtimeSinceStartup < ownedUnitsDeadline)
            {
                yield return null;
                ownedCombatUnits = GetMember(worldState, "OwnCombatUnits") as IList;
            }
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][BRAIN_GROUP] brain=" + team1Brain.name
                + " brainEnabled=" + ((Behaviour)team1Brain).enabled
                + " brainActive=" + team1Brain.gameObject.activeInHierarchy
                + " teamId=" + GetMember(team1Brain, "TeamId")
                + " worldTeam=" + GetMember(worldState, "_teamId")
                + " ownUnits=" + ((GetMember(worldState, "OwnUnits") as IList)?.Count ?? -1)
                + " ownCombatUnits=" + ((GetMember(worldState, "OwnCombatUnits") as IList)?.Count ?? -1)
                + " lastScan=" + GetMember(worldState, "LastScanTime")
                + " identities=" + DescribeTeamIdentities(validationScene, TeamA)
                + " registry=" + DescribeWorldRegistry());
            Assert.That(ownedCombatUnits, Is.Not.Null.And.Not.Empty,
                "BrainMaster ainda não montou o grupo de unidades próprio necessário para a reserva N2.");
            GameObject targetObject = FindByPath(
                validationScene,
                "IA03_TestScenario/PaisB_Karsovia_Team3/Unidades_Reais/Karsovia_Tank_02");
            Assert.That(targetObject, Is.Not.Null, "O alvo terrestre real da fixture não foi encontrado.");
            expectedMissionTarget = targetObject.transform;
            Component targetIdentity = FindComponentRecursive(expectedMissionTarget, "IdentidadeUnidade");
            Assert.That(targetIdentity, Is.Not.Null);
            Assert.That(Convert.ToInt32(GetMember(targetIdentity, "teamID")), Is.EqualTo(TeamB));

            GameObject expectedShooter = FindByPath(
                validationScene,
                "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_01");
            Assert.That(expectedShooter, Is.Not.Null);
            Component shooterIdentity = FindComponentRecursive(expectedShooter.transform, "IdentidadeUnidade");
            Assert.That(shooterIdentity, Is.Not.Null);
            Assert.That(Convert.ToInt32(GetMember(shooterIdentity, "teamID")), Is.EqualTo(TeamA));

#if UNITY_EDITOR
            temporaryReadinessFactoryObject = new GameObject("Fábrica IA03 N2 Readiness Test");
            SceneManager.MoveGameObjectToScene(temporaryReadinessFactoryObject, validationScene);
            Vector3 facilityDirection = expectedShooter.transform.position - expectedMissionTarget.position;
            facilityDirection.y = 0f;
            if (facilityDirection.sqrMagnitude < 0.001f) facilityDirection = Vector3.forward;
            temporaryReadinessFactoryObject.transform.position = expectedShooter.transform.position
                + facilityDirection.normalized * 75f;

            Type identityType = ResolveType("IdentidadeUnidade");
            Assert.That(identityType, Is.Not.Null);
            Component readinessFactoryIdentity = temporaryReadinessFactoryObject.GetComponent(identityType);
            if (readinessFactoryIdentity == null)
                readinessFactoryIdentity = temporaryReadinessFactoryObject.AddComponent(identityType);
            Assert.That(readinessFactoryIdentity, Is.Not.Null);
            SetMember(readinessFactoryIdentity, "teamID", TeamA);
            SetMember(readinessFactoryIdentity, "nomeDoPais", Convert.ToString(GetMember(team1Brain, "NationName")));
            SetMember(readinessFactoryIdentity, "tipoUnidade", Enum.Parse(identityType.GetField("tipoUnidade", InstanceMembers).FieldType, "Estrutura"));

            Type factoryType = ResolveType("Fabrica");
            Assert.That(factoryType, Is.Not.Null, "O componente real Fabrica não foi compilado.");
            Component readinessFactory = temporaryReadinessFactoryObject.AddComponent(factoryType);
            SetMember(readinessFactory, "ehQuartel", false);
            SetMember(readinessFactory, "pontoNascimento", temporaryReadinessFactoryObject.transform);
            SetMember(readinessFactory, "pontoSaida", temporaryReadinessFactoryObject.transform);
            bool readinessFactoryOperational = Convert.ToBoolean(
                Invoke(readinessFactory, "PossuiEnergiaParaProduzir"));
            Assert.That(readinessFactoryOperational, Is.True,
                "A instalação só pode habilitar a prontidão se a regra real de Fabrica confirmar operação.");
            Debug.Log("[IA03][N2_TEST_FACILITY] source=real-Fabrica-component"
                + " owner=" + GetMember(readinessFactoryIdentity, "teamID")
                + " kind=" + GetMember(readinessFactoryIdentity, "tipoUnidade")
                + " modernEnergyContract="
                + (FindComponentRecursive(temporaryReadinessFactoryObject.transform, "EstruturaEconomica") != null)
                + " operational=" + readinessFactoryOperational);

            Type worldStateType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
            Assert.That(worldStateType, Is.Not.Null);
            MethodInfo notifyEntityChanged = worldStateType.GetMethod("NotifyEntityChanged", StaticMembers);
            Assert.That(notifyEntityChanged, Is.Not.Null);
            notifyEntityChanged.Invoke(null, new[] { readinessFactoryIdentity });
            Invoke(worldState, "MarkDirty");
            Invoke(worldState, "Tick", Time.time, Time.deltaTime);
            IList ownStructuresAfterFacility = GetMember(worldState, "OwnStructures") as IList;
            Assert.That(ownStructuresAfterFacility, Is.Not.Null.And.Contains(temporaryReadinessFactoryObject),
                "A prontidão só pode contar a instalação se o WorldState real a reconhecer como estrutura própria.");
#endif

            missionTargetOriginalPosition = expectedMissionTarget.position;
            missionTargetPositionCaptured = true;
            missionTargetAgent = expectedMissionTarget.GetComponent<NavMeshAgent>();
            if (missionTargetAgent == null) missionTargetAgent = expectedMissionTarget.GetComponentInChildren<NavMeshAgent>(true);
            if (missionTargetAgent != null && missionTargetAgent.enabled && missionTargetAgent.isOnNavMesh)
            {
                missionTargetAgentStopped = missionTargetAgent.isStopped;
                missionTargetAgentStateCaptured = true;
                // O alvo precisa permanecer estático para que esta fixture
                // valide somente a aproximação, a mira e o cleanup do atacante.
                missionTargetAgent.ResetPath();
                missionTargetAgent.isStopped = true;
            }
            missionTargetPatrol = FindComponentRecursive(expectedMissionTarget, "PatrulhaCombateSegura") as Behaviour;
            if (missionTargetPatrol != null)
            {
                missionTargetPatrolEnabled = missionTargetPatrol.enabled;
                missionTargetPatrolStateCaptured = true;
                missionTargetPatrol.enabled = false;
            }
            missionTargetController = FindComponentRecursive(expectedMissionTarget, "ControleUnidade") as Behaviour;
            if (missionTargetController != null)
            {
                missionTargetControllerEnabled = missionTargetController.enabled;
                missionTargetControllerStateCaptured = true;
                missionTargetController.enabled = false;
            }

            Type visibilityType = ResolveType("Hegemonia.RTS.RTSVisibilityService");
            Assert.That(visibilityType, Is.Not.Null, "RTSVisibilityService não foi compilado.");
            visibilityService = visibilityType.GetProperty("Instancia", StaticMembers)?.GetValue(null) as Component;
            if (visibilityService == null)
            {
                testOnlyVisibilityServiceObject = new GameObject("IA03_N2_PlayModeVisibility");
                visibilityService = testOnlyVisibilityServiceObject.AddComponent(visibilityType);
            }
            visibilityServiceEnabledBefore = visibilityService is Behaviour visibilityBehaviour && visibilityBehaviour.enabled;
            visibilityServiceStateCaptured = visibilityService is Behaviour;
            FieldInfo teamContactsField = visibilityService.GetType().GetField("contactsByTeam", InstanceMembers);
            IDictionary allContacts = teamContactsField?.GetValue(visibilityService) as IDictionary;
            IDictionary teamContacts = allContacts != null ? allContacts[TeamA] as IDictionary : null;
            teamContacts?.Clear();
            IList visibleEnemiesBefore = GetMember(worldState, "VisibleEnemies") as IList;
            visibleEnemiesBefore?.Clear();
            Type detectionSourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
            Assert.That(detectionSourceType, Is.Not.Null);
            Invoke(visibilityService, "ReportContact", TeamA, targetIdentity,
                Enum.Parse(detectionSourceType, "Manual"), 300f);
            if (visibilityService is Behaviour visibilityBehaviourToDisable)
                visibilityBehaviourToDisable.enabled = false;
            injectedTargetInstanceId = targetIdentity.GetInstanceID();
            visibilityContactInjected = true;

            float targetObservationDeadline = Time.realtimeSinceStartup + 10f;
            bool targetObserved = false;
            while (!targetObserved && Time.realtimeSinceStartup < targetObservationDeadline)
            {
                IList visible = GetMember(worldState, "VisibleEnemies") as IList;
                if (visible != null)
                {
                    for (int i = 0; i < visible.Count; i++)
                    {
                        Transform observed = GetMember(visible[i], "Transform") as Transform;
                        if (ReferenceEquals(observed, expectedMissionTarget))
                        {
                            targetObserved = true;
                            break;
                        }
                    }
                }
                if (!targetObserved) yield return null;
            }
            Assert.That(targetObserved, Is.True,
                "O WorldState não recebeu o contato manual da fixture para o alvo vivo da N2.");

            // A fixture tem vários tanques reais; a N2 deve usar a unidade que
            // reproduziu a falha da boca2 no runtime controlado.
            IList ownedUnits = GetMember(worldState, "OwnUnits") as IList;
            Assert.That(expectedShooter, Is.Not.Null);
            Assert.That(ownedUnits, Is.Not.Null);
            int expectedShooterIndex = ownedUnits.IndexOf(expectedShooter);
            Assert.That(expectedShooterIndex, Is.GreaterThanOrEqualTo(0),
                "Valdoria_Tank_01 não consta nas unidades reais próprias do BrainMaster.");
            if (expectedShooterIndex > 0)
            {
                ownedUnits.RemoveAt(expectedShooterIndex);
                ownedUnits.Insert(0, expectedShooter);
            }

            IList missions = GetMember(team1Strategy, "missoesEstrategicas") as IList;
            Assert.That(missions, Is.Not.Null);
            for (int i = 0; i < missions.Count; i++)
            {
                object candidate = missions[i];
                if (candidate != null
                    && string.Equals(Convert.ToString(GetMember(candidate, "IdMissao")), "TEST-ATAQUE-N2", StringComparison.Ordinal))
                {
                    activeAttackMission = candidate;
                    break;
                }
            }
            Assert.That(activeAttackMission, Is.Not.Null, "A missão real Ataque N2 não foi encontrada.");

            Type conflictType = ResolveType("Hegemonia.AI.IA03.IA03NivelConflito");
            Assert.That(conflictType, Is.Not.Null);
            object limitedConflict = Enum.Parse(conflictType, "ConflitoLimitado");
            Invoke(team1Strategy, "DefinirNivelConflito", limitedConflict, "regressão de alinhamento N2");
            SetMember(team1Strategy, "proximaDecisaoDeMissaoEm", Time.time + 30f);
            originalAttackMissionPriority = GetMember(activeAttackMission, "Prioridade");
            if (originalAttackMissionPriority == null)
                originalAttackMissionPriority = GetMember(activeAttackMission, "prioridade");
            SetMember(activeAttackMission, "prioridade", 1000);
            attackMissionPriorityChanged = true;

            // Registra a IA03 já habilitada, mas suspende apenas sua decisão automática
            // até a ordem urgente N2 entrar na fila real.
            originalStrategyActiveValue = GetMember(team1Strategy, "ativo");
            strategyActiveStateCaptured = originalStrategyActiveValue != null;
            SetMember(team1Strategy, "ativo", false);
            team1StrategyBehaviour.enabled = true;
            SetMember(team1Brain, "_modulesRegistered", false);
            Invoke(team1Brain, "EnsureRuntimeOperational", false);
            bool requestAccepted = Convert.ToBoolean(Invoke(
                team1Strategy,
                "SolicitarMissaoEstrategica",
                activeAttackMission,
                "regressão PlayMode de alinhamento sustentado da torreta",
                true));
            string requestRejectionDetails = string.Empty;
            if (!requestAccepted)
            {
                object currentLevel = GetMember(team1Strategy, "nivelDeConflito");
                object defaultCreaty = Invoke(team1Strategy, "MissaoDefaultCreaty", activeAttackMission);
                object acceptsAtCurrentLevel = Invoke(activeAttackMission, "Aceita", currentLevel, defaultCreaty);
                object completedInCrisis = Invoke(team1Strategy, "MissaoFoiConcluidaNaCrise", activeAttackMission);
                object equivalentQueued = Invoke(team1Strategy, "ContemMissaoEquivalenteNaFila", activeAttackMission);
                object activeMission = GetMember(team1Strategy, "missaoAtiva");
                MethodInfo equivalentMethod = team1Strategy.GetType().GetMethod(
                    "SaoMissoesEquivalentes", InstanceMembers | StaticMembers);
                object equivalentActive = equivalentMethod != null
                    ? equivalentMethod.Invoke(null, new[] { activeMission, activeAttackMission })
                    : "resolver ausente";
                IList queuedMissions = GetMember(team1Strategy, "filaMissoes") as IList;
                requestRejectionDetails = " level=" + currentLevel
                    + " defaultCreaty=" + defaultCreaty
                    + " acceptedAtLevel=" + acceptsAtCurrentLevel
                    + " completedInCrisis=" + completedInCrisis
                    + " equivalentActive=" + equivalentActive
                    + " activeMission=" + GetMember(activeMission, "IdMissao")
                    + " equivalentQueued=" + equivalentQueued
                    + " queueCount=" + (queuedMissions != null ? queuedMissions.Count.ToString() : "null");
            }
            Assert.That(requestAccepted, Is.True,
                "A API pública da IA03 recusou o pedido urgente da missão N2." + requestRejectionDetails);

            // Este teste cobre a cadeia de combate sob prontidão suficiente.
            // A fixture de combate não define o estado demográfico de Governo;
            // portanto, fornece no runtime apenas o excedente necessário para
            // preservar o piso configurado e sustentar a tripulação do atacante.
            team1CountryForReadiness = Invoke(government, "ObterPais", TeamA);
            Assert.That(team1CountryForReadiness, Is.Not.Null,
                "O país da IA03 precisa existir para avaliar manpower antes do N2.");
            CaptureMembers(
                team1CountryForReadiness,
                team1CountryReadinessState,
                "populacao", "populacaoMaxima", "populacaoCivil", "populacaoMilitarAtiva", "reservistas", "alistaveis",
                "saldo", "comida", "petroleo", "energia", "deficitComida", "deficitPetroleo", "deficitEnergia");
            team1CountryReadinessStateCaptured = true;
            object team1Profile = GetMember(team1Strategy, "perfilPais");
            float reservePercentage = Convert.ToSingle(
                GetMember(team1Profile, "ReservaManpowerMinimaPercentualDaPopulacao") ?? 0f);
            long attackerCrew = Math.Max(1L,
                Convert.ToInt64(GetMember(shooterIdentity, "militaresConsumidos") ?? 1));
            int preparedAlistaveis = CalcularAlistaveisParaHeadroomDeReserva(
                team1CountryForReadiness,
                reservePercentage,
                attackerCrew);
            SetMember(team1CountryForReadiness, "alistaveis", preparedAlistaveis);
            long preparedPopulation = Math.Max(
                Convert.ToInt64(GetMember(team1CountryForReadiness, "populacao") ?? 0L),
                Convert.ToInt64(GetMember(team1CountryForReadiness, "populacaoCivil") ?? 0L)
                    + Convert.ToInt64(GetMember(team1CountryForReadiness, "populacaoMilitarAtiva") ?? 0L)
                    + Convert.ToInt64(GetMember(team1CountryForReadiness, "reservistas") ?? 0L)
                    + preparedAlistaveis);
            int preparedPopulationSerialized = checked((int)preparedPopulation);
            SetMember(team1CountryForReadiness, "populacao", preparedPopulationSerialized);
            SetMember(team1CountryForReadiness, "populacaoMaxima", Math.Max(
                Convert.ToInt32(GetMember(team1CountryForReadiness, "populacaoMaxima") ?? 0),
                preparedPopulationSerialized));

            bool team1IsPlayer = Convert.ToInt32(GetMember(government, "teamJogador") ?? -1) == TeamA;
            Type resourceManagerType = ResolveType("GerenciadorRecursos");
            team1ResourcesForReadiness = resourceManagerType != null
                ? resourceManagerType.GetProperty("Instancia", StaticMembers)?.GetValue(null)
                : null;
            if (team1IsPlayer && team1ResourcesForReadiness != null)
            {
                CaptureMembers(
                    team1ResourcesForReadiness,
                    team1ResourceManagerReadinessState,
                    "dinheiro", "comida", "petroleo", "energia", "populacaoAtual", "populacaoMaxima",
                    "dinheiroPorSegundo", "energiaPorSegundo");
                team1ResourceManagerReadinessStateCaptured = true;
            }

            Type industrialType = ResolveType("SistemaIndustrialNacional");
            Type industryIdsType = ResolveType("IndustriaIds");
            Type industrialIntegrationType = ResolveType("IntegracaoMercadoIndustrial");
            Assert.That(industrialType, Is.Not.Null, "SistemaIndustrialNacional indisponível no runtime.");
            Assert.That(industryIdsType, Is.Not.Null, "IDs do estoque industrial não estão carregados.");
            Assert.That(industrialIntegrationType, Is.Not.Null, "Reservas industriais não estão carregadas.");
            readinessDieselId = Convert.ToString(
                industryIdsType.GetField("Diesel", StaticMembers)?.GetValue(null));
            Assert.That(readinessDieselId, Is.Not.Null.And.Not.Empty);
            industrialForReadiness = industrialType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
            if (industrialForReadiness == null)
            {
                temporaryIndustrialReadinessObject = new GameObject("IA03_N2_Industrial_Readiness_Test");
                industrialForReadiness = temporaryIndustrialReadinessObject.AddComponent(industrialType);
            }
            Assert.That(industrialForReadiness, Is.Not.Null,
                "A cadeia N2 precisa consultar o estoque industrial nacional existente.");
            industrialReadinessSave = Invoke(industrialForReadiness, "CriarSaveData");
            originalTeam1Diesel = Convert.ToDouble(Invoke(
                industrialForReadiness, "ObterQuantidadePais", TeamA, readinessDieselId));
            officialDieselReserve = Convert.ToDouble(industrialIntegrationType
                .GetMethod("ReservaMinimaPadrao", StaticMembers)
                .Invoke(null, new object[] { readinessDieselId }));
            if (originalTeam1Diesel < officialDieselReserve)
            {
                Assert.That(
                    Invoke(industrialForReadiness, "TentarAdicionar",
                        TeamA.ToString(), readinessDieselId, officialDieselReserve - originalTeam1Diesel),
                    Is.True,
                    "O estoque central não aceitou o diesel necessário para a reserva oficial.");
            }

            long profileFinancialReserve = (long)Math.Ceiling(Math.Max(
                0d,
                Convert.ToDouble(GetMember(team1Profile, "ReservaFinanceiraDesejada") ?? 0d)));
            long existingFinancialReserve = Math.Max(0L, Convert.ToInt64(
                Invoke(government, "ObterReservaFinanceiraEstrategica", TeamA) ?? 0L));
            long foundationReserve = Math.Max(0L, Convert.ToInt64(
                Invoke(government, "ObterReservaFundacao", TeamA) ?? 0L));
            long balanceAboveReserve = checked(Math.Max(profileFinancialReserve, existingFinancialReserve)
                + foundationReserve + 1L);
            int preparedFood = Math.Max(150, Convert.ToInt32(GetMember(team1CountryForReadiness, "comida") ?? 0));
            int preparedOil = Math.Max(150, Convert.ToInt32(GetMember(team1CountryForReadiness, "petroleo") ?? 0));
            int preparedEnergy = Math.Max(100, Convert.ToInt32(GetMember(team1CountryForReadiness, "energia") ?? 0));

            if (team1IsPlayer && team1ResourcesForReadiness != null)
            {
                SetMember(team1ResourcesForReadiness, "dinheiro", balanceAboveReserve);
                SetMember(team1ResourcesForReadiness, "comida", preparedFood);
                SetMember(team1ResourcesForReadiness, "petroleo", preparedOil);
                SetMember(team1ResourcesForReadiness, "energia", preparedEnergy);
                SetMember(team1ResourcesForReadiness, "populacaoAtual", (int)Math.Min(int.MaxValue, preparedPopulation));
                SetMember(team1ResourcesForReadiness, "populacaoMaxima", (int)Math.Min(int.MaxValue,
                    Math.Max(preparedPopulation, Convert.ToInt64(
                        GetMember(team1ResourcesForReadiness, "populacaoMaxima") ?? 0L))));
                Invoke(government, "SincronizarJogador");
            }

            SetMember(team1CountryForReadiness, "saldo", balanceAboveReserve);
            SetMember(team1CountryForReadiness, "comida", preparedFood);
            SetMember(team1CountryForReadiness, "petroleo", preparedOil);
            SetMember(team1CountryForReadiness, "energia", preparedEnergy);
            SetMember(team1CountryForReadiness, "deficitComida", 0f);
            SetMember(team1CountryForReadiness, "deficitPetroleo", 0f);
            SetMember(team1CountryForReadiness, "deficitEnergia", 0f);

            SetMember(team1Strategy, "ativo", true);
            // Avança uma vez o mesmo módulo registrado no scheduler para eliminar
            // a latência escalonada da fixture sem substituir o fluxo da missão.
            Invoke(team1Strategy, "Tick", Time.time, Time.deltaTime);

            long manpowerAvailable = Convert.ToInt64(
                Invoke(government, "ObterManpowerDisponivelParaMobilizacao", TeamA));
            long manpowerReserve = Convert.ToInt64(
                Invoke(government, "ObterReservaManpowerEstrategica", TeamA));
            long manpowerAboveReserve = Convert.ToInt64(
                Invoke(government, "ObterManpowerDisponivelAlemDaReserva", TeamA));
            Debug.Log("[IA03][N2_TEST_MANPOWER] population=" + GetMember(team1CountryForReadiness, "populacao")
                + " civilian=" + GetMember(team1CountryForReadiness, "populacaoCivil")
                + " alistaveis=" + preparedAlistaveis
                + " available=" + manpowerAvailable
                + " reserve=" + manpowerReserve
                + " aboveReserve=" + manpowerAboveReserve
                + " attackerCrew=" + attackerCrew
                + " reservePercentage=" + reservePercentage);
            Assert.That(manpowerAboveReserve, Is.GreaterThanOrEqualTo(attackerCrew),
                "O cenário N2 de combate deve ter efetivo acima do piso para a tripulação que já mobilizou.");

            // Avalia a missão real antes de ler a prontidão: o custo de
            // reposição só é resolvido depois que o BrainMaster monta o grupo
            // candidato para o Creaty, não no snapshot demográfico genérico.
            Invoke(team1Strategy, "TentarIniciarMissao", Time.time);
            object readinessSummary = GetMember(team1Strategy, "economiaAtual");
            Debug.Log("[IA03][N2_TEST_READINESS] ready=" + GetMember(readinessSummary, "PodeEscalarParaN2")
                + " reason=" + GetMember(readinessSummary, "MotivoProntidao")
                + " balance=" + GetMember(readinessSummary, "Saldo")
                + " balanceAvailable=" + GetMember(readinessSummary, "SaldoDisponivel")
                + " financialReserve=" + GetMember(readinessSummary, "ReservaFinanceiraEstrategica")
                + " food=" + GetMember(readinessSummary, "Comida")
                + " oil=" + GetMember(readinessSummary, "Petroleo")
                + " energy=" + GetMember(readinessSummary, "Energia")
                + " diesel=" + GetMember(readinessSummary, "CombustivelTerrestre")
                + " dieselId=" + readinessDieselId
                + " dieselReserve=" + officialDieselReserve
                + " fuelLow=" + GetMember(readinessSummary, "CombustivelOperacionalBaixo")
                + " essentialsLow=" + GetMember(readinessSummary, "EstoqueEssencialBaixo")
                + " factories=" + GetMember(readinessSummary, "FabricasOperacionais")
                + " barracks=" + GetMember(readinessSummary, "QuarteisOperacionais")
                + " airports=" + GetMember(readinessSummary, "AeroportosMilitaresOperacionais")
                + " heliports=" + GetMember(readinessSummary, "HeliportosOperacionais")
                + " shipyards=" + GetMember(readinessSummary, "EstaleirosOperacionais"));
            Assert.That(GetMember(readinessSummary, "PodeEscalarParaN2"), Is.EqualTo(false),
                "N2 deve continuar em prontidão limitada enquanto o caixa livre não cobre a reposição. reason="
                + GetMember(readinessSummary, "MotivoProntidao"));
            Assert.That(GetMember(readinessSummary, "ReposicaoDoGrupoConhecida"), Is.EqualTo(true),
                "O bloqueio financeiro só é válido quando a ficha oficial da unidade foi resolvida.");
            Assert.That(Convert.ToString(GetMember(readinessSummary, "MotivoProntidao")),
                Does.Contain("saldo operacional insuficiente"));

            // O snapshot geral pode ter saldo operacional positivo, mas N2 só
            // deve reservar o grupo depois de confirmar que uma baixa real dele
            // pode ser reposta pelo preço efetivo da ficha.
            Assert.That(GetMember(team1Strategy, "missaoAtiva"), Is.Null,
                "N2 não deve iniciar quando o saldo cobre a reserva do perfil, mas não a reposição de uma baixa.");
            readinessSummary = GetMember(team1Strategy, "economiaAtual");
            long replacementCost = Convert.ToInt64(GetMember(readinessSummary, "CustoReposicaoDoGrupo") ?? 0L);
            long availableBeforeReplacement = Convert.ToInt64(GetMember(readinessSummary, "SaldoDisponivel") ?? 0L);
            string mostExpensiveReplacement = Convert.ToString(GetMember(readinessSummary, "ItemMaisCaroDoGrupo"));
            Assert.That(GetMember(readinessSummary, "ReposicaoDoGrupoConhecida"), Is.EqualTo(true),
                "A missão N2 precisa resolver a ficha efetiva de cada unidade do grupo. motivo="
                + GetMember(readinessSummary, "MotivoProntidao"));
            Assert.That(replacementCost, Is.GreaterThan(0L));
            Assert.That(availableBeforeReplacement, Is.LessThan(replacementCost),
                "A primeira tentativa precisa provar o bloqueio por falta de caixa operacional.");
            Assert.That(Convert.ToString(GetMember(team1Strategy, "ultimaDecisao")), Does.Contain("saldo operacional insuficiente"));
            Debug.Log("[IA03][N2_TEST_REPLACEMENT_GATE] started=false item=" + mostExpensiveReplacement
                + " cost=" + replacementCost + " available=" + availableBeforeReplacement
                + " reason=" + GetMember(readinessSummary, "MotivoProntidao"));

            long fundedBalance = checked(Math.Max(profileFinancialReserve, existingFinancialReserve)
                + foundationReserve + replacementCost);
            SetMember(team1CountryForReadiness, "saldo", fundedBalance);
            if (team1IsPlayer && team1ResourcesForReadiness != null)
            {
                SetMember(team1ResourcesForReadiness, "dinheiro", fundedBalance);
                Invoke(government, "SincronizarJogador");
            }

            SetMember(team1Strategy, "proximaDecisaoDeMissaoEm", Time.time - 1f);
            Invoke(team1Strategy, "Tick", Time.time, Time.deltaTime);
            Debug.Log("[IA03][N2_TEST_REPLACEMENT_GATE] retryBalance=" + fundedBalance
                + " available=" + GetMember(GetMember(team1Strategy, "economiaAtual"), "SaldoDisponivel")
                + " mission=" + (GetMember(team1Strategy, "missaoAtiva") != null));
            Assert.That(GetMember(team1Strategy, "missaoAtiva"), Is.SameAs(activeAttackMission),
                "A N2 deve iniciar pelo Tick real depois que o caixa operacional cobre o custo efetivo de reposição.");

            float missionDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < missionDeadline
                   && GetMember(team1Strategy, "missaoAtiva") == null)
            {
                yield return null;
            }
            Assert.That(GetMember(team1Strategy, "missaoAtiva"), Is.SameAs(activeAttackMission),
                "A IA03 não iniciou a missão de ataque pela fila real. decisão="
                + GetMember(team1Strategy, "ultimaDecisao")
                + " | próximoTick=" + GetMember(team1Strategy, "proximaDecisaoDeMissaoEm")
                + " | fila=" + GetMember(team1Strategy, "filaMissoes"));
            readinessSummary = GetMember(team1Strategy, "economiaAtual");
            Assert.That(GetMember(readinessSummary, "ReposicaoDoGrupoConhecida"), Is.EqualTo(true));
            Assert.That(Convert.ToInt64(GetMember(readinessSummary, "SaldoDisponivel") ?? 0L),
                Is.GreaterThanOrEqualTo(Convert.ToInt64(GetMember(readinessSummary, "CustoReposicaoDoGrupo") ?? 0L)),
                "A missão só deve iniciar com caixa operacional suficiente para uma baixa do grupo.");
            missionTarget = GetMember(team1Strategy, "alvoMissaoAtivo") as Transform;
            Component missionTargetIdentity = null;
            for (Transform parent = missionTarget; parent != null && missionTargetIdentity == null; parent = parent.parent)
                missionTargetIdentity = parent.GetComponent("IdentidadeUnidade");
            Assert.That(missionTargetIdentity, Is.SameAs(targetIdentity),
                "A Attack N2 não preservou a unidade que o BrainMaster tornou visível. transform="
                + missionTarget + " expected=" + expectedMissionTarget);
            SetMember(activeAttackMission, "prioridade", originalAttackMissionPriority);
            attackMissionPriorityChanged = false;

            IList missionUnits = GetMember(team1Strategy, "unidadesAtivasNaMissao") as IList;
            Assert.That(missionUnits, Is.Not.Null.And.Not.Empty, "A missão N2 não reservou uma unidade real.");
            shooter = missionUnits[0] as GameObject;
            Assert.That(shooter, Is.Not.Null);
            Assert.That(Convert.ToBoolean(Invoke(team1Strategy, "IsUnitReservedForActiveMission", shooter)), Is.True,
                "A unidade escolhida não está reservada pela missão ativa.");
            reservedCreaty = GetMember(team1Strategy, "creatyAtivo");
            Assert.That(reservedCreaty, Is.Not.Null, "A missão ativa não reservou um Creaty real.");
            unitController = FindComponentRecursive(shooter.transform, "ControleUnidade");
            Assert.That(unitController, Is.Not.Null);
            navMeshAgent = shooter.GetComponent<NavMeshAgent>();
            if (navMeshAgent == null) navMeshAgent = shooter.GetComponentInChildren<NavMeshAgent>(true);
            Assert.That(navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh, Is.True,
                "A unidade reservada precisa usar o NavMesh real da fixture.");
            originalPosition = shooter.transform.position;
            originalRotation = shooter.transform.rotation;
            originalAgentStopped = navMeshAgent.isStopped;
            originalUnitStateCaptured = true;
            CaptureShooterTurrets(shooter);
            Assert.That(turretStates.Count, Is.GreaterThanOrEqualTo(2),
                "As duas ControleTorreta reais do tanque precisam continuar presentes.");

            Component primaryTurret = null;
            for (int i = 0; i < turretStates.Count; i++)
            {
                Behaviour turret = turretStates[i].behaviour;
                turret.enabled = true;
                Transform[] firepoints = GetMember(turret, "locaisDoTiro") as Transform[];
                if (firepoints != null && firepoints.Length > 0 && firepoints[0] != null
                    && firepoints[0].name == "boca2")
                {
                    primaryTurret = turret;
                }
            }
            Assert.That(primaryTurret, Is.Not.Null, "A arma principal boca2 não foi encontrada na unidade reservada.");
            Assert.That(Convert.ToBoolean(GetMember(primaryTurret, "inverterSentidoPitch")), Is.True,
                "O pivô importado da boca2 precisa aplicar o sentido de pitch comprovado no runtime.");
            selectedTurretState = FindTurretState(primaryTurret);
            Assert.That(selectedTurretState, Is.Not.Null);

            float priorityDeadline = Time.realtimeSinceStartup + 15f;
            bool explicitTargetPropagated = false;
            while (!explicitTargetPropagated && Time.realtimeSinceStartup < priorityDeadline)
            {
                explicitTargetPropagated = true;
                for (int i = 0; i < turretStates.Count; i++)
                {
                    if (!ReferenceEquals(GetMember(turretStates[i].behaviour, "alvoPrioritario"), missionTarget))
                    {
                        explicitTargetPropagated = false;
                        break;
                    }
                }
                if (!explicitTargetPropagated) yield return null;
            }
            Assert.That(explicitTargetPropagated, Is.True,
                "O alvo da missão não foi propagado à unidade reservada pela ordem Attack N2.");
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][EXPLICIT_TARGET] shooter=" + shooter.name
                + " target=" + missionTarget.name + " source=Attack N2 runtime flow");

            GameObject prefeituraPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Imobiliario/Prefeitura/Prefeitura.prefab");
            Assert.That(prefeituraPrefab, Is.Not.Null,
                "A Prefeitura real precisa estar disponível como candidato automático da fixture.");
            automaticDecoyInstance = UnityEngine.Object.Instantiate(prefeituraPrefab,
                shooter.transform.position, Quaternion.identity);
            Assert.That(automaticDecoyInstance, Is.Not.Null);
            automaticDecoyRoot = automaticDecoyInstance.transform;
            Component automaticDecoyIdentity = FindComponentRecursive(automaticDecoyRoot, "IdentidadeUnidade");
            Assert.That(automaticDecoyIdentity, Is.Not.Null);
            SetMember(automaticDecoyIdentity, "teamID", TeamB);
            Assert.That(Convert.ToInt32(GetMember(automaticDecoyIdentity, "teamID")), Is.EqualTo(TeamB));
            Assert.That(automaticDecoyIdentity.transform, Is.Not.SameAs(targetIdentity.transform));
            automaticDecoyTarget = automaticDecoyIdentity.transform;
            for (int i = 0; i < turretStates.Count; i++)
            {
                Assert.That(Convert.ToBoolean(Invoke(turretStates[i].behaviour,
                    "PodeAtacarAutomaticamente", automaticDecoyIdentity)), Is.True,
                    "A Prefeitura precisa ser um candidato automático válido para tornar a regressão conclusiva.");
            }
            automaticDecoyOriginalPosition = automaticDecoyRoot.position;
            automaticDecoyPositionCaptured = true;
            automaticDecoyAgent = automaticDecoyRoot.GetComponent<NavMeshAgent>();
            if (automaticDecoyAgent == null)
                automaticDecoyAgent = automaticDecoyRoot.GetComponentInChildren<NavMeshAgent>(true);
            Vector3 decoyPosition = shooter.transform.position + shooter.transform.right * 100f;
            decoyPosition.y = automaticDecoyRoot.position.y;
            if (automaticDecoyAgent != null && automaticDecoyAgent.enabled && automaticDecoyAgent.isOnNavMesh)
            {
                automaticDecoyAgentStopped = automaticDecoyAgent.isStopped;
                automaticDecoyAgentStateCaptured = true;
                automaticDecoyAgent.ResetPath();
                automaticDecoyAgent.isStopped = true;
                automaticDecoyAgent.Warp(decoyPosition);
            }
            else
            {
                automaticDecoyRoot.position = decoyPosition;
            }

            // Reproduz a troca de alvo que acontecia no N2: a ordem mantém o alvo
            // prioritário fora do alcance enquanto as torretas têm tempo de adquirir
            // um candidato automático. Depois o mesmo alvo da missão entra no alcance.
            // Este estágio registra os IDs em cada elo antes de qualquer correção.
            float minimumTurretRange = float.PositiveInfinity;
            for (int i = 0; i < turretStates.Count; i++)
            {
                minimumTurretRange = Mathf.Min(minimumTurretRange, turretStates[i].range);
            }
            Debug.Log("[IA03][N2_TARGET_FIDELITY][PRIORITY_OUT_OF_RANGE] "
                + BuildN2TargetFidelitySnapshot(shooter, unitController, targetIdentity,
                    missionTarget, team1Strategy, "far;decoy=" + automaticDecoyTarget.name
                        + "#" + automaticDecoyTarget.GetInstanceID(), 0f));
            float automaticTargetDeadline = Time.realtimeSinceStartup + 1.5f;
            float lastAutomaticSnapshotAt = 0f;
            bool noAutomaticTargetSubstitution = false;
            while (Time.realtimeSinceStartup < automaticTargetDeadline)
            {
                noAutomaticTargetSubstitution = true;
                for (int i = 0; i < turretStates.Count; i++)
                {
                    Transform current = GetMember(turretStates[i].behaviour, "alvoAtual") as Transform;
                    Transform priority = GetMember(turretStates[i].behaviour, "alvoPrioritario") as Transform;
                    if ((current != null && !ReferenceEquals(current, missionTarget))
                        || !ReferenceEquals(priority, missionTarget))
                    {
                        noAutomaticTargetSubstitution = false;
                        break;
                    }
                }
                if (Time.realtimeSinceStartup - lastAutomaticSnapshotAt >= 0.25f)
                {
                    lastAutomaticSnapshotAt = Time.realtimeSinceStartup;
                    Debug.Log("[IA03][N2_TARGET_FIDELITY][AUTO_SELECTION_SAMPLE] "
                        + BuildN2TargetFidelitySnapshot(shooter, unitController, targetIdentity,
                            missionTarget, team1Strategy, "far;decoy=" + automaticDecoyTarget.name
                                + "#" + automaticDecoyTarget.GetInstanceID(), Time.time));
                }
                if (!noAutomaticTargetSubstitution) break;
                yield return null;
            }
            string noSubstitutionSnapshot = BuildN2TargetFidelitySnapshot(shooter, unitController,
                targetIdentity, missionTarget, team1Strategy,
                "far-final;decoy=" + automaticDecoyTarget.name + "#" + automaticDecoyTarget.GetInstanceID(), Time.time);
            Assert.That(noAutomaticTargetSubstitution, Is.True,
                "Uma Prefeitura elegível não pode substituir o alvo explícito da missão enquanto estiver fora do alcance. "
                + noSubstitutionSnapshot);

            object attackMovementOrder = GetMember(unitController, "OrdemMovimentoAtual");
            Vector3 attackApproachDestination = GetMember(attackMovementOrder, "Destino") is Vector3 approach
                ? approach
                : Vector3.zero;
            string attackOrderId = Convert.ToString(GetMember(team1Strategy, "idOrdemAtivaDaMissao"));
            Assert.That(Convert.ToString(GetMember(attackMovementOrder, "Dono")),
                Is.EqualTo("IA03EstrategaNacional"),
                "A Attack N2 precisa substituir o deslocamento antigo pelo ponto real de aproximação. "
                + BuildN2TargetFidelitySnapshot(shooter, unitController, targetIdentity,
                    missionTarget, team1Strategy, "attack-move-order", Time.time));
            Assert.That(Convert.ToString(GetMember(attackMovementOrder, "Id")),
                Does.StartWith(attackOrderId + ":"));
            Assert.That(Vector3.Distance(attackApproachDestination, missionTarget.position),
                Is.InRange(35f, 45f),
                "O ponto de aproximação da missão deve ficar no standoff calculado para a torreta.");
            Debug.Log("[IA03][N2_ATTACK_APPROACH] order=" + attackOrderId
                + " movementOwner=" + GetMember(attackMovementOrder, "Dono")
                + " movementId=" + GetMember(attackMovementOrder, "Id")
                + " destination=" + attackApproachDestination
                + " target=" + missionTarget.name + "#" + missionTarget.GetInstanceID()
                + " standoff=" + Vector3.Distance(attackApproachDestination, missionTarget.position).ToString("F1"));

            float priorityReacquireDeadline = Time.realtimeSinceStartup + 180f;
            bool bothTurretsReacquiredMissionTarget = false;
            while (Time.realtimeSinceStartup < priorityReacquireDeadline)
            {
                bothTurretsReacquiredMissionTarget = true;
                float missionTargetDistance = Vector3.Distance(shooter.transform.position, missionTarget.position);
                if (missionTargetDistance > minimumTurretRange - 5f)
                    bothTurretsReacquiredMissionTarget = false;
                for (int i = 0; i < turretStates.Count; i++)
                {
                    Transform current = GetMember(turretStates[i].behaviour, "alvoAtual") as Transform;
                    Transform priority = GetMember(turretStates[i].behaviour, "alvoPrioritario") as Transform;
                    if (!ReferenceEquals(priority, missionTarget)
                        || (missionTargetDistance <= turretStates[i].range && !ReferenceEquals(current, missionTarget)))
                    {
                        bothTurretsReacquiredMissionTarget = false;
                        break;
                    }
                }
                if (bothTurretsReacquiredMissionTarget) break;
                yield return null;
            }
            string reacquireSnapshot = BuildN2TargetFidelitySnapshot(shooter, unitController,
                targetIdentity, missionTarget, team1Strategy, "near", Time.time);
            Debug.Log("[IA03][N2_TARGET_FIDELITY][PRIORITY_REACQUIRE] " + reacquireSnapshot);
            Assert.That(bothTurretsReacquiredMissionTarget, Is.True,
                "O tanque não alcançou a solução N2 mantendo ambas as prioridades explícitas. "
                + reacquireSnapshot);

            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][POSITIONED_NEAR] shooter=" + shooter.name
                + " target=" + missionTarget.name
                + " distance=" + Vector3.Distance(shooter.transform.position, missionTarget.position).ToString("F2"));
            currentCaseEvents.Clear();
            currentN2MissionTargetIdentityMarker = targetIdentity.name + "#" + targetIdentity.GetInstanceID();
            currentN2ShooterIdentityMarker = shooterIdentity.name + "#" + shooterIdentity.GetInstanceID();
            currentCase = "N2";
            keepCaseProjectileForImpact = true;
            for (int i = 0; i < turretStates.Count; i++)
                RearmProjectileProbeForTurret(turretStates[i].behaviour);

            float weaponRange = Convert.ToSingle(GetMember(primaryTurret, "alcance"));
            float alignDeadline = Time.realtimeSinceStartup + 8f;
            float aimError = GetSelectedBarrelAngleToTarget(missionTarget);
            while ((aimError > 5f
                    || Vector3.Distance(shooter.transform.position, missionTarget.position) > weaponRange - 5f)
                   && Time.realtimeSinceStartup < alignDeadline)
            {
                yield return null;
                aimError = GetSelectedBarrelAngleToTarget(missionTarget);
            }
            Transform yawPivot = GetMember(primaryTurret, "pecaQueGira") as Transform;
            Transform pitchPivot = GetMember(primaryTurret, "canosDaTorreta") as Transform;
            Transform muzzle = null;
            Transform[] muzzlePoints = GetMember(primaryTurret, "locaisDoTiro") as Transform[];
            if (muzzlePoints != null)
            {
                for (int i = 0; i < muzzlePoints.Length; i++)
                {
                    if (muzzlePoints[i] != null && muzzlePoints[i].name == "boca2")
                    {
                        muzzle = muzzlePoints[i];
                        break;
                    }
                }
            }
            Transform aimReference = yawPivot != null && yawPivot.parent != null ? yawPivot.parent : yawPivot;
            Vector3 targetDirectionFromPivot = yawPivot != null ? missionTarget.position - yawPivot.position : Vector3.zero;
            Vector3 targetDirectionLocal = aimReference != null
                ? aimReference.InverseTransformDirection(targetDirectionFromPivot)
                : targetDirectionFromPivot;
            float desiredYaw = Mathf.Atan2(targetDirectionLocal.x, targetDirectionLocal.z) * Mathf.Rad2Deg;
            float rawDesiredPitch = -Mathf.Atan2(targetDirectionLocal.y,
                new Vector2(targetDirectionLocal.x, targetDirectionLocal.z).magnitude) * Mathf.Rad2Deg;
            float muzzlePitch = muzzle != null
                ? Mathf.Atan2(muzzle.forward.y, new Vector2(muzzle.forward.x, muzzle.forward.z).magnitude) * Mathf.Rad2Deg
                : float.NaN;
            Vector3 predictedTargetPosition = (Vector3)Invoke(primaryTurret, "ObterPosicaoPreditaAlvo", missionTarget);
            string shooterAgentDiagnostic = navMeshAgent != null
                ? "stopped=" + navMeshAgent.isStopped
                    + " velocity=" + navMeshAgent.velocity.ToString("F2")
                    + " hasPath=" + navMeshAgent.hasPath
                    + " destination=" + navMeshAgent.destination.ToString("F2")
                    + " updateRotation=" + navMeshAgent.updateRotation
                : "none";
            string targetAgentDiagnostic = missionTargetAgent != null
                ? "stopped=" + missionTargetAgent.isStopped
                    + " velocity=" + missionTargetAgent.velocity.ToString("F2")
                    + " hasPath=" + missionTargetAgent.hasPath
                    + " destination=" + missionTargetAgent.destination.ToString("F2")
                    + " updateRotation=" + missionTargetAgent.updateRotation
                : "none";
            bool canFireByAim = Convert.ToBoolean(Invoke(primaryTurret, "EstaAlinhadaParaDisparar"));
            int currentBarrelIndex = Convert.ToInt32(GetMember(primaryTurret, "indiceBarrilAtual") ?? 0);
            StringBuilder barrelDiagnostics = new StringBuilder();
            if (muzzlePoints != null)
            {
                for (int i = 0; i < muzzlePoints.Length; i++)
                {
                    Transform barrel = muzzlePoints[i];
                    if (barrel == null) continue;
                    if (barrelDiagnostics.Length > 0) barrelDiagnostics.Append(" | ");
                    barrelDiagnostics.Append(i == currentBarrelIndex ? "*" : string.Empty)
                        .Append(i).Append(':').Append(barrel.name)
                        .Append(" pos=").Append(barrel.position)
                        .Append(" forward=").Append(barrel.forward.ToString("F2"))
                        .Append(" angle=").Append(Vector3.Angle(barrel.forward, missionTarget.position - barrel.position).ToString("F2"));
                }
            }
            string lineOfSightBlocker = muzzle != null ? string.Empty : "no muzzle";
            bool hasLineOfSight = muzzle != null
                && HasLineOfSight(shooter.transform, muzzle, missionTarget, out lineOfSightBlocker);
            string aimDiagnostic = "shooter=" + shooter.name
                + " target=" + missionTarget.name
                + " mission=" + GetMember(activeAttackMission, "NomeMissao")
                + " order=" + GetMember(team1Strategy, "idOrdemAtivaDaMissao")
                + " priority=" + GetMember(primaryTurret, "alvoPrioritario")
                + " explicit=" + object.ReferenceEquals(GetMember(primaryTurret, "alvoPrioritario"), missionTarget)
                + " muzzleAngle=" + aimError.ToString("F2")
                + " yawPart=" + yawPivot?.name
                + " yawCurrent=" + (yawPivot != null ? yawPivot.localEulerAngles.y.ToString("F2") : "none")
                + " yawDesired=" + desiredYaw.ToString("F2")
                + " yawError=" + (yawPivot != null ? Mathf.DeltaAngle(yawPivot.localEulerAngles.y, desiredYaw).ToString("F2") : "none")
                + " pitchPart=" + pitchPivot?.name
                + " barrelEuler=" + (pitchPivot != null ? pitchPivot.localEulerAngles.ToString() : "none")
                + " autoBarrel=" + GetMember(primaryTurret, "descobrirCanosAutomaticamente")
                + " invertPitch=" + GetMember(primaryTurret, "inverterSentidoPitch")
                + " barrelDepression=" + GetMember(primaryTurret, "elevacaoMinima")
                + " barrelElevation=" + GetMember(primaryTurret, "elevacaoMaxima")
                + " pitchCurrentMuzzle=" + muzzlePitch.ToString("F2")
                + " pitchDesiredRaw=" + rawDesiredPitch.ToString("F2")
                + " pitchCommand=" + GetMember(primaryTurret, "giroPitchAlvo")
                + " turnSpeed=" + GetMember(primaryTurret, "velocidadeGiro")
                + " selectedBarrel=" + currentBarrelIndex
                + " barrels={" + barrelDiagnostics + "}"
                + " range=" + weaponRange
                + " distance=" + Vector3.Distance(shooter.transform.position, missionTarget.position).ToString("F2")
                + " targetDistanceFromMuzzle=" + (muzzle != null ? Vector3.Distance(muzzle.position, missionTarget.position).ToString("F2") : "none")
                + " canFireByAim=" + canFireByAim
                + " lineOfSight=" + hasLineOfSight
                + " lineOfSightBlocker=" + (muzzle != null ? lineOfSightBlocker : "no muzzle")
                + " cooldown=" + GetMember(primaryTurret, "contadorTempo")
                + " reloading=" + GetMember(primaryTurret, "estaRecarregando")
                + " attackerPosition=" + shooter.transform.position
                + " attackerRotation=" + shooter.transform.eulerAngles
                + " targetPosition=" + missionTarget.position;
            aimDiagnostic += " predictedTargetPosition=" + predictedTargetPosition
                + " shooterAgent={" + shooterAgentDiagnostic + "}"
                + " targetAgent={" + targetAgentDiagnostic + "}"
                + " controllerMoveOrder=" + GetMember(unitController, "PossuiOrdemMovimentoAtiva")
                + " controllerState=" + GetMember(unitController, "ordemControleAtual");
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][AIM] " + aimDiagnostic);
            Assert.That(aimError, Is.LessThanOrEqualTo(5f),
                "A torreta da unidade N2 reservada não convergiu para o alvo explícito. " + aimDiagnostic);
            Assert.That(Vector3.Distance(shooter.transform.position, missionTarget.position),
                Is.LessThanOrEqualTo(weaponRange - 5f), "O alvo precisa estar dentro do alcance na primeira solução de tiro.");

            int ammoAtStart = Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual"));
            Component targetDamage = FindComponentRecursive(targetIdentity.transform, "SistemaDeDanos");
            Assert.That(targetDamage, Is.Not.Null, "O alvo real não tem SistemaDeDanos.");
            float targetHpAtStart = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
            float firstShotDeadline = Time.realtimeSinceStartup + 8f;
            while (Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual")) >= ammoAtStart
                   && Time.realtimeSinceStartup < firstShotDeadline)
            {
                yield return null;
            }
            int ammoAfterFirstShot = Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual"));
            Assert.That(ammoAfterFirstShot, Is.LessThan(ammoAtStart),
                "A arma principal alinhada não efetuou o primeiro disparo. " + aimDiagnostic);
            Assert.That(GetMember(primaryTurret, "alvoPrioritario"), Is.SameAs(missionTarget),
                "O alvo prioritário foi removido depois do primeiro disparo.");

            // Pequeno deslocamento real pelo NavMesh durante a mesma missão:
            // a mira precisa acompanhar o novo ponto e seguir disparando.
            Vector3 beforeNudge = shooter.transform.position;
            Vector3 lateral = Vector3.Cross(Vector3.up, missionTarget.position - shooter.transform.position).normalized;
            Assert.That(lateral.sqrMagnitude, Is.GreaterThan(0.5f));
            navMeshAgent.Move(lateral * 0.65f);
            float moveDeadline = Time.realtimeSinceStartup + 3f;
            while (Vector3.Distance(beforeNudge, shooter.transform.position) < 0.15f
                   && Time.realtimeSinceStartup < moveDeadline)
            {
                yield return null;
            }
            float movedDistance = Vector3.Distance(beforeNudge, shooter.transform.position);
            Assert.That(movedDistance, Is.GreaterThanOrEqualTo(0.15f),
                "NavMeshAgent.Move não deslocou a unidade reservada para testar a recuperação da mira.");

            alignDeadline = Time.realtimeSinceStartup + 8f;
            aimError = GetSelectedBarrelAngleToTarget(missionTarget);
            while (aimError > 5f && Time.realtimeSinceStartup < alignDeadline)
            {
                yield return null;
                aimError = GetSelectedBarrelAngleToTarget(missionTarget);
            }
            aimDiagnostic += " moved=" + movedDistance.ToString("F2")
                + " muzzleAngleAfterMove=" + aimError.ToString("F2");
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][RECOVERED] " + aimDiagnostic);
            Assert.That(aimError, Is.LessThanOrEqualTo(5f),
                "A torreta não recuperou o alinhamento depois do movimento do chassi. " + aimDiagnostic);
            Assert.That(GetMember(primaryTurret, "alvoPrioritario"), Is.SameAs(missionTarget),
                "O alvo prioritário foi removido durante a recuperação da mira.");

            float secondShotDeadline = Time.realtimeSinceStartup + 8f;
            int ammoAfterMovement = Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual"));
            while (Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual")) >= ammoAfterFirstShot
                   && Time.realtimeSinceStartup < secondShotDeadline)
            {
                yield return null;
            }
            int ammoAfterSecondShot = Convert.ToInt32(GetMember(primaryTurret, "MunicaoAtual"));
            Assert.That(ammoAfterSecondShot, Is.LessThan(ammoAfterFirstShot),
                "A arma não continuou o disparo após recuperar o alinhamento.");
            Assert.That(GetMember(primaryTurret, "alvoPrioritario"), Is.SameAs(missionTarget),
                "O alvo prioritário foi removido antes da destruição do alvo.");

            object reportBeforeDestruction = GetMember(team1Strategy, "RelatorioAtual");
            int enemyKillsBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "InimigosDestruidos"));
            int ownLossesBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "UnidadesPropriasPerdidas"));
            int enemyStructuresBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "EstruturasInimigasDestruidas"));
            int ownStructuresBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "EstruturasPropriasDestruidas"));
            float enemyStructuralDamageBefore = Convert.ToSingle(GetMember(reportBeforeDestruction, "DanoEstruturalInimigo"));
            float ownStructuralDamageBefore = Convert.ToSingle(GetMember(reportBeforeDestruction, "DanoEstruturalProprio"));
            float enemyCostBefore = Convert.ToSingle(GetMember(reportBeforeDestruction, "PrejuizoEconomicoInimigo"));
            float ownCostBefore = Convert.ToSingle(GetMember(reportBeforeDestruction, "PrejuizoEconomicoProprio"));
            int offensiveWinsBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "BatalhasVencidas"));
            int offensiveLossesBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "BatalhasPerdidas"));
            int objectivesCapturedBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "ObjetivosCapturados"));
            int objectivesLostBefore = Convert.ToInt32(GetMember(reportBeforeDestruction, "ObjetivosPerdidos"));
            string persistentTargetId = Convert.ToString(GetMember(team1Strategy, "idPersistenteAlvoMissao"));

            float destructionDeadline = Time.realtimeSinceStartup + 60f;
            float nextDamageSampleAt = Time.realtimeSinceStartup + 5f;
            while (missionTarget != null && targetDamage != null
                   && Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) > 0f
                   && Time.realtimeSinceStartup < destructionDeadline)
            {
                yield return null;
                if (Time.realtimeSinceStartup >= nextDamageSampleAt)
                {
                    nextDamageSampleAt = Time.realtimeSinceStartup + 5f;
                    Debug.Log("[IA03][N2_TURRET_ALIGNMENT][DAMAGE_SAMPLE] hp="
                        + Convert.ToSingle(GetMember(targetDamage, "vidaAtual")).ToString("F1")
                        + " shooterPosition=" + shooter.transform.position.ToString("F2")
                        + " shooterRotation=" + shooter.transform.eulerAngles.ToString("F1")
                        + " shooterAgentStopped=" + (navMeshAgent != null && navMeshAgent.isStopped)
                        + " targetDistance=" + Vector3.Distance(shooter.transform.position, missionTarget.position).ToString("F1")
                        + " controllerMoveOrder=" + GetMember(unitController, "PossuiOrdemMovimentoAtiva")
                        + " controllerState=" + GetMember(unitController, "ordemControleAtual")
                        + " snapshot=" + BuildN2TargetFidelitySnapshot(shooter, unitController,
                            targetIdentity, missionTarget, team1Strategy, "damage", Time.time));
                }
            }
            float finalHp = targetDamage != null ? Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) : 0f;
            int roundsUsed = ammoAtStart - ammoAfterSecondShot;
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][DAMAGE] shooter=" + shooter.name
                + " team=" + TeamA
                + " target=" + missionTarget.name
                + " targetTeam=" + TeamB
                + " rounds=" + roundsUsed
                + " hp=" + targetHpAtStart.ToString("F1") + "->" + finalHp.ToString("F1")
                + " mission=" + GetMember(team1Strategy, "EstadoDaMissao")
                + " targetDestroyedFlag=" + GetMember(team1Strategy, "alvoMissaoDestruido")
                + " persistentTargetId=" + GetMember(team1Strategy, "idPersistenteAlvoMissao")
                + " lastDecision=" + GetMember(team1Strategy, "ultimaDecisao")
                + " finalSnapshot=" + BuildN2TargetFidelitySnapshot(shooter, unitController,
                    targetIdentity, missionTarget, team1Strategy, "damage-final", Time.time));
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][PROBE_EVENTS] count=" + currentCaseEvents.Count
                + " events=" + (currentCaseEvents.Count > 0 ? string.Join(" || ", currentCaseEvents.ToArray()) : "none"));
            Assert.That(finalHp, Is.LessThanOrEqualTo(0f),
                "O alvo não foi destruído pelos disparos sucessivos da unidade reservada.");
            Assert.That(roundsUsed, Is.GreaterThanOrEqualTo(2), "A regressão deve provar múltiplos disparos.");

            Type combatRegistryType = ResolveType("CartaCombateRegistro");
            IEnumerable combatEvents = (IEnumerable)combatRegistryType.GetProperty("Eventos").GetValue(null);
            object targetDeathEvent = null;
            int targetDeathEventCount = 0;
            foreach (object combatEvent in combatEvents)
            {
                if (string.Equals(Convert.ToString(GetMember(combatEvent, "idAlvo")), persistentTargetId, StringComparison.Ordinal)
                    && Convert.ToInt32(GetMember(combatEvent, "equipeAlvo")) == TeamB
                    && Convert.ToInt32(GetMember(combatEvent, "equipeAtacante")) == TeamA)
                {
                    targetDeathEvent = combatEvent;
                    targetDeathEventCount++;
                }
            }
            Assert.That(targetDeathEvent, Is.Not.Null, "A morte N2 deve ter publicado um evento de combate identificável.");
            Assert.That(targetDeathEventCount, Is.EqualTo(1), "Uma morte N2 deve gerar um evento destrutivo único.");
            Assert.That(Convert.ToBoolean(GetMember(targetDeathEvent, "custoReposicaoConhecido")), Is.True,
                "O prefab Tank_Unity catalogado como Tank_South precisa carregar metadata de custo até a morte.");
            Type definitivePricesType = ResolveType("ValoresDefinitivosHegemonia");
            MethodInfo tryGetDefinitivePrice = definitivePricesType.GetMethod("TryObterPreco", StaticMembers);
            Assert.That(tryGetDefinitivePrice, Is.Not.Null);
            object[] priceArguments = { "tank_south", "Tank South", 0L };
            Assert.That(Convert.ToBoolean(tryGetDefinitivePrice.Invoke(null, priceArguments)), Is.True);
            long expectedTankReplacementCost = Convert.ToInt64(priceArguments[2]);
            Assert.That(Convert.ToInt64(GetMember(targetDeathEvent, "custoReposicaoEstimado")), Is.EqualTo(expectedTankReplacementCost),
                "O custo registrado deve usar o preço efetivo da tabela econômica central.");

            string missionTargetIdentityMarker = targetIdentity.name + "#" + targetIdentity.GetInstanceID();
            string shooterIdentityMarker = shooterIdentity.name + "#" + shooterIdentity.GetInstanceID();
            string matchingImpact = FindCaseEventContaining("[PROJECTILE_PROBE][IMPACT]",
                "targetIdentity=" + missionTargetIdentityMarker,
                "targetTeam=" + TeamB,
                "shooterIdentity=" + shooterIdentityMarker);
            Assert.That(matchingImpact, Is.Not.Null,
                "Nenhum projétil identificado da unidade reservada atingiu o tanque alvo da missão.");
            string probeId = ExtractProjectileProbeId(matchingImpact);
            string matchingSpawn = FindCaseEventContaining("[PROJECTILE_PROBE][SPAWN]", "id=" + probeId + " ");
            string matchingDamage = FindCaseEventContaining("[PROJECTILE_PROBE][DAMAGE]", "id=" + probeId + " ");
            Assert.That(matchingSpawn, Is.Not.Null,
                "O impacto no alvo não tem um registro de spawn atribuído à torreta selecionada.");
            Assert.That(matchingSpawn, Does.Contain("shooter=" + shooter.name + " shooterTeam=" + TeamA)
                .And.Contain("shooterIdentity=" + shooterIdentityMarker)
                .And.Contain("targetIdentity=" + missionTargetIdentityMarker)
                .And.Contain("targetTeam=" + TeamB), matchingSpawn);
            bool probeCameFromShooterTurret = false;
            for (int i = 0; i < turretStates.Count; i++)
            {
                if (matchingSpawn.IndexOf("turretComponentId=" + turretStates[i].instanceId + " ", StringComparison.Ordinal) >= 0)
                {
                    probeCameFromShooterTurret = true;
                    break;
                }
            }
            Assert.That(probeCameFromShooterTurret, Is.True,
                "O projétil precisa pertencer a uma das torretas reais da unidade reservada.");
            Assert.That(matchingImpact, Does.Contain("shooter=" + shooter.name + " shooterTeam=" + TeamA)
                .And.Contain("shooterIdentity=" + shooterIdentityMarker)
                .And.Contain("targetIdentity=" + missionTargetIdentityMarker)
                .And.Contain("targetTeam=" + TeamB), matchingImpact);
            Assert.That(matchingDamage, Is.Not.Null,
                "O impacto do projétil rastreado não registrou dano em SistemaDeDanos.");
            Assert.That(matchingDamage, Does.Contain("victim=" + missionTarget.name + " victimTeam=" + TeamB)
                .And.Contain("victimIdentity=" + missionTargetIdentityMarker)
                .And.Contain("aggressor=" + shooter.name + " aggressorTeam=" + TeamA)
                .And.Contain("aggressorIdentity=" + shooterIdentityMarker), matchingDamage);
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][ATTRIBUTED_HIT] spawn={" + matchingSpawn
                + "} impact={" + matchingImpact + "} damage={" + matchingDamage + "}");

            if (Convert.ToBoolean(GetMember(team1Strategy, "alvoMissaoDestruido")))
            {
                // Avalia a condição pelo Tick real da IA03, sem forçar estados
                // terminais nem chamar diretamente o método de cleanup.
                Invoke(team1Strategy, "Tick", Time.time, Time.deltaTime);
            }

            float terminalDeadline = Time.realtimeSinceStartup + 15f;
            while (GetMember(team1Strategy, "missaoAtiva") != null
                   && Time.realtimeSinceStartup < terminalDeadline)
            {
                yield return null;
            }
            Assert.That(GetMember(team1Strategy, "EstadoDaMissao").ToString(), Is.EqualTo("Sucesso"),
                "A destruição não levou a missão N2 ao estado terminal de sucesso.");
            Assert.That(GetMember(team1Strategy, "missaoAtiva"), Is.Null);
            Assert.That(GetMember(team1Strategy, "idOrdemAtivaDaMissao"), Is.EqualTo(string.Empty));
            Assert.That((GetMember(team1Strategy, "unidadesAtivasNaMissao") as IList)?.Count ?? -1, Is.EqualTo(0));
            Assert.That(Convert.ToInt32(GetMember(team1Strategy, "unidadesReservadas")), Is.EqualTo(0));
            Assert.That(GetMember(team1Strategy, "creatyAtivo"), Is.Null);
            Assert.That(Convert.ToInt32(GetMember(reservedCreaty, "UnidadesReservadas")), Is.EqualTo(0));
            Assert.That(Convert.ToBoolean(Invoke(team1Strategy, "IsUnitReservedForActiveMission", shooter)), Is.False);
            Assert.That(Convert.ToBoolean(GetMember(unitController, "PossuiOrdemMovimentoAtiva")), Is.False,
                "A ordem do BrainMaster continuou ativa depois do encerramento da missão.");
            object reportAfterMission = GetMember(team1Strategy, "RelatorioAtual");
            float expectedEnemyCostDelta = Convert.ToBoolean(GetMember(targetDeathEvent, "custoReposicaoConhecido"))
                ? Convert.ToSingle(GetMember(targetDeathEvent, "custoReposicaoEstimado"))
                : 0f;
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "InimigosDestruidos")), Is.EqualTo(enemyKillsBefore + 1));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "UnidadesPropriasPerdidas")), Is.EqualTo(ownLossesBefore));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "EstruturasInimigasDestruidas")), Is.EqualTo(enemyStructuresBefore));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "EstruturasPropriasDestruidas")), Is.EqualTo(ownStructuresBefore));
            Assert.That(Convert.ToSingle(GetMember(reportAfterMission, "DanoEstruturalInimigo")), Is.EqualTo(enemyStructuralDamageBefore).Within(0.001f));
            Assert.That(Convert.ToSingle(GetMember(reportAfterMission, "DanoEstruturalProprio")), Is.EqualTo(ownStructuralDamageBefore).Within(0.001f));
            Assert.That(Convert.ToSingle(GetMember(reportAfterMission, "PrejuizoEconomicoInimigo")) - enemyCostBefore,
                Is.EqualTo(expectedEnemyCostDelta).Within(0.001f), "O custo conhecido do alvo deve ser somado exatamente uma vez.");
            Assert.That(Convert.ToSingle(GetMember(reportAfterMission, "PrejuizoEconomicoProprio")), Is.EqualTo(ownCostBefore).Within(0.001f));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "BatalhasVencidas")), Is.EqualTo(offensiveWinsBefore + 1));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "BatalhasPerdidas")), Is.EqualTo(offensiveLossesBefore));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "ObjetivosCapturados")), Is.EqualTo(objectivesCapturedBefore));
            Assert.That(Convert.ToInt32(GetMember(reportAfterMission, "ObjetivosPerdidos")), Is.EqualTo(objectivesLostBefore));
            Debug.Log("[IA03][N2_CONFLICT_REPORT] beforeKills=" + enemyKillsBefore
                + " afterKills=" + GetMember(reportAfterMission, "InimigosDestruidos")
                + " beforeOwnLosses=" + ownLossesBefore
                + " afterOwnLosses=" + GetMember(reportAfterMission, "UnidadesPropriasPerdidas")
                + " enemyCostDelta=" + (Convert.ToSingle(GetMember(reportAfterMission, "PrejuizoEconomicoInimigo")) - enemyCostBefore)
                + " expectedCostDelta=" + expectedEnemyCostDelta
                + " offensiveWins=" + offensiveWinsBefore + "->" + GetMember(reportAfterMission, "BatalhasVencidas")
                + " offensiveLosses=" + offensiveLossesBefore + "->" + GetMember(reportAfterMission, "BatalhasPerdidas")
                + " eventId=" + GetMember(targetDeathEvent, "id"));
            Debug.Log("[IA03][N2_TURRET_ALIGNMENT][CLEANUP] result=Sucesso order="
                + GetMember(team1Strategy, "idOrdemAtivaDaMissao")
                + " group=" + (GetMember(team1Strategy, "unidadesAtivasNaMissao") as IList)?.Count
                + " unitReserved=" + Invoke(team1Strategy, "IsUnitReservedForActiveMission", shooter)
                + " creatyReserved=" + GetMember(reservedCreaty, "UnidadesReservadas"));
        }
        finally
        {
            if (projectileProbeSubscribed)
            {
                Application.logMessageReceived -= CaptureProbeAndReleaseTestProjectiles;
                projectileProbeSubscribed = false;
            }
            currentCase = null;
            keepCaseProjectileForImpact = false;
            currentCaseEvents.Clear();
            UnsubscribeSceneLoadedCallback();
            if (team1StrategyBehaviour != null && team1StrategyBehaviour.enabled)
                team1StrategyBehaviour.enabled = false;
            if (strategyActiveStateCaptured && team1Strategy != null)
                SetMember(team1Strategy, "ativo", originalStrategyActiveValue);
            if (attackMissionPriorityChanged && activeAttackMission != null)
                SetMember(activeAttackMission, "prioridade", originalAttackMissionPriority);
            if (visibilityContactInjected && visibilityService != null)
            {
                FieldInfo contactsField = visibilityService.GetType().GetField("contactsByTeam", InstanceMembers);
                IDictionary contactsByTeam = contactsField?.GetValue(visibilityService) as IDictionary;
                IDictionary teamContacts = contactsByTeam != null ? contactsByTeam[TeamA] as IDictionary : null;
                teamContacts?.Remove(injectedTargetInstanceId);
            }
            if (visibilityServiceStateCaptured && visibilityService is Behaviour visibilityBehaviourToRestore)
                visibilityBehaviourToRestore.enabled = visibilityServiceEnabledBefore;
            ReleaseWatchedProjectiles(true);
            if (missionTargetPositionCaptured && expectedMissionTarget != null)
            {
                if (missionTargetAgentStateCaptured && missionTargetAgent != null
                    && missionTargetAgent.enabled && missionTargetAgent.isOnNavMesh)
                {
                    missionTargetAgent.ResetPath();
                    missionTargetAgent.Warp(missionTargetOriginalPosition);
                    missionTargetAgent.isStopped = missionTargetAgentStopped;
                }
                else
                {
                    expectedMissionTarget.position = missionTargetOriginalPosition;
                }
            }
            if (missionTargetControllerStateCaptured && missionTargetController != null)
                missionTargetController.enabled = missionTargetControllerEnabled;
            if (missionTargetPatrolStateCaptured && missionTargetPatrol != null)
                missionTargetPatrol.enabled = missionTargetPatrolEnabled;
            if (automaticDecoyPositionCaptured && automaticDecoyRoot != null)
            {
                if (automaticDecoyAgentStateCaptured && automaticDecoyAgent != null
                    && automaticDecoyAgent.enabled && automaticDecoyAgent.isOnNavMesh)
                {
                    automaticDecoyAgent.ResetPath();
                    automaticDecoyAgent.Warp(automaticDecoyOriginalPosition);
                    automaticDecoyAgent.isStopped = automaticDecoyAgentStopped;
                }
                else
                {
                    automaticDecoyRoot.position = automaticDecoyOriginalPosition;
                }
            }
            if (automaticDecoyInstance != null)
                UnityEngine.Object.Destroy(automaticDecoyInstance);
            if (originalUnitStateCaptured) StopAndRestorePosition();
            RestoreTurrets();
            if (industrialForReadiness != null && industrialReadinessSave != null)
            {
                Invoke(industrialForReadiness, "RestaurarSaveData", industrialReadinessSave);
                Type marketType = ResolveType("SistemaMercadoGlobal");
                object market = marketType != null
                    ? marketType.GetProperty("Instancia", StaticMembers)?.GetValue(null)
                    : null;
                if (market != null)
                {
                    Type integrationType = ResolveType("IntegracaoMercadoIndustrial");
                    MethodInfo syncMarket = integrationType != null
                        ? integrationType.GetMethod("SincronizarEstoquesNoMercado", StaticMembers)
                        : null;
                    syncMarket?.Invoke(null, new[] { industrialForReadiness, market });
                }
            }
            if (team1ResourceManagerReadinessStateCaptured && team1ResourcesForReadiness != null)
                RestoreMembers(team1ResourcesForReadiness, team1ResourceManagerReadinessState);
            if (team1CountryReadinessStateCaptured && team1CountryForReadiness != null)
                RestoreMembers(team1CountryForReadiness, team1CountryReadinessState);
            if (temporaryIndustrialReadinessObject != null)
                UnityEngine.Object.Destroy(temporaryIndustrialReadinessObject);
            if (temporaryReadinessFactoryObject != null)
                UnityEngine.Object.Destroy(temporaryReadinessFactoryObject);
            if (readinessProductionManager != null)
            {
                RestoreMembers(readinessProductionManager, readinessProductionManagerState);
                if (readinessBarracksPoints != null && readinessBarracksPointsBefore != null)
                {
                    readinessBarracksPoints.Clear();
                    for (int i = 0; i < readinessBarracksPointsBefore.Count; i++)
                        readinessBarracksPoints.Add(readinessBarracksPointsBefore[i]);
                }
                if (readinessHangarPoints != null && readinessHangarPointsBefore != null)
                {
                    readinessHangarPoints.Clear();
                    for (int i = 0; i < readinessHangarPointsBefore.Count; i++)
                        readinessHangarPoints.Add(readinessHangarPointsBefore[i]);
                }
            }
            if (diplomacyWasModified) RestoreDiplomacy();
            RestoreBehaviours();
            ResetEngagementProbes();
        }
    }

    [UnityTest]
    public IEnumerator BrainMasterTacticalAttack_DoesNotReassignUnitReservedByN2Mission()
    {
        yield return WaitForPendingValidationSceneUnload();
        ResetScenarioState();
        Scene originalScene = SceneManager.GetActiveScene();
        originalScenePathForBrainMasterRegression = originalScene.IsValid() ? originalScene.path : string.Empty;
        restoreSceneAfterBrainMasterRegression = !string.IsNullOrWhiteSpace(originalScenePathForBrainMasterRegression)
            && !string.Equals(originalScenePathForBrainMasterRegression, ValidationScenePath, StringComparison.OrdinalIgnoreCase);
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        sceneLoadedCallbackSubscribed = true;

        Component strategy = null;
        Behaviour strategyBehaviour = null;
        object previousActiveMission = null;
        IList activeMissionUnits = null;
        List<GameObject> previousReservedUnits = new List<GameObject>();
        object commandQueue = null;
        string enqueuedRequestId = null;
        bool missionStateCaptured = false;

        try
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ValidationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Não foi possível carregar a fixture IA03 como cena única.");
            while (!load.isDone) yield return null;
#else
            Assert.Fail("A fixture IA03 precisa ser carregada em Play Mode no Editor.");
            yield break;
#endif

            yield return null;
            Scene validationScene = SceneManager.GetActiveScene();
            Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(isolationArmed, Is.True);
            Assert.That(validationScene.isDirty, Is.False);

            Component brain = FindBrainMasterForTeam(validationScene, TeamA);
            Assert.That(brain, Is.Not.Null, "BrainMaster Team 1 não encontrado na fixture.");
            strategy = FindComponentInScene(validationScene, "IA03EstrategaNacional");
            Assert.That(strategy, Is.Not.Null, "IA03 Team 1 não encontrada na fixture.");
            strategyBehaviour = strategy as Behaviour;
            Assert.That(strategyBehaviour, Is.Not.Null);

            GameObject reservedUnit = FindByPath(
                validationScene,
                "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_02");
            GameObject unreservedUnit = FindByPath(
                validationScene,
                "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_01");
            GameObject target = FindByPath(
                validationScene,
                "IA03_TestScenario/PaisB_Karsovia_Team3/Unidades_Reais/Karsovia_Tank_02");
            Assert.That(reservedUnit, Is.Not.Null);
            Assert.That(unreservedUnit, Is.Not.Null);
            Assert.That(target, Is.Not.Null);

            IList missions = GetMember(strategy, "missoesEstrategicas") as IList;
            Assert.That(missions, Is.Not.Null);
            object n2AttackMission = null;
            for (int i = 0; i < missions.Count; i++)
            {
                object candidate = missions[i];
                if (candidate != null
                    && string.Equals(Convert.ToString(GetMember(candidate, "IdMissao")), "TEST-ATAQUE-N2", StringComparison.Ordinal))
                {
                    n2AttackMission = candidate;
                    break;
                }
            }
            Assert.That(n2AttackMission, Is.Not.Null, "A fixture precisa manter a missão real Ataque N2.");

            strategyBehaviour.enabled = true;
            previousActiveMission = GetMember(strategy, "missaoAtiva");
            activeMissionUnits = GetMember(strategy, "unidadesAtivasNaMissao") as IList;
            Assert.That(activeMissionUnits, Is.Not.Null);
            for (int i = 0; i < activeMissionUnits.Count; i++)
            {
                if (activeMissionUnits[i] is GameObject previousUnit)
                    previousReservedUnits.Add(previousUnit);
            }
            missionStateCaptured = true;

            SetMember(strategy, "missaoAtiva", n2AttackMission);
            activeMissionUnits.Clear();
            activeMissionUnits.Add(reservedUnit);
            Assert.That(Convert.ToBoolean(Invoke(strategy, "IsUnitReservedForActiveMission", reservedUnit)), Is.True);
            Assert.That(Convert.ToBoolean(Invoke(strategy, "IsUnitReservedForActiveMission", unreservedUnit)), Is.False);

            object context = GetMember(brain, "Context");
            Assert.That(context, Is.Not.Null);
            commandQueue = GetMember(context, "CommandQueue");
            object tacticalDirector = GetMember(brain, "_tacticalDirector");
            Assert.That(commandQueue, Is.Not.Null);
            Assert.That(tacticalDirector, Is.Not.Null, "IA_TacticalDirector não foi inicializado pelo BrainMaster.");
            IList pending = GetMember(commandQueue, "_pending") as IList;
            Assert.That(pending, Is.Not.Null);

            MethodInfo queueAttack = tacticalDirector.GetType().GetMethod("QueueAttack", InstanceMembers);
            Assert.That(queueAttack, Is.Not.Null);
            int pendingBefore = pending.Count;
            queueAttack.Invoke(tacticalDirector, new object[]
            {
                "ia03-reserved-only-" + Guid.NewGuid().ToString("N"),
                new List<GameObject> { reservedUnit },
                target.transform,
                target.transform.position,
                92,
                2.8f
            });
            Assert.That(pending.Count, Is.EqualTo(pendingBefore),
                "A diretora não deve enfileirar uma ordem tática vazia só com unidades reservadas pela N2.");

            HashSet<string> previousRequestIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pending.Count; i++)
                previousRequestIds.Add(Convert.ToString(GetMember(pending[i], "Id")));

            queueAttack.Invoke(tacticalDirector, new object[]
            {
                "ia03-reserved-mixed-" + Guid.NewGuid().ToString("N"),
                new List<GameObject> { reservedUnit, unreservedUnit },
                target.transform,
                target.transform.position,
                92,
                2.8f
            });
            Assert.That(pending.Count, Is.EqualTo(pendingBefore + 1),
                "A diretora deve continuar comandando unidades livres no mesmo esquadrão.");

            object queuedRequest = null;
            for (int i = 0; i < pending.Count; i++)
            {
                object candidate = pending[i];
                if (!previousRequestIds.Contains(Convert.ToString(GetMember(candidate, "Id"))))
                {
                    queuedRequest = candidate;
                    break;
                }
            }
            Assert.That(queuedRequest, Is.Not.Null);
            enqueuedRequestId = Convert.ToString(GetMember(queuedRequest, "Id"));
            object payload = GetMember(queuedRequest, "Payload");
            IList orderedUnits = GetMember(payload, "Units") as IList;
            Assert.That(orderedUnits, Is.Not.Null);
            Assert.That(orderedUnits.Count, Is.EqualTo(1));
            Assert.That(orderedUnits[0], Is.SameAs(unreservedUnit));
            Assert.That(GetMember(payload, "Target"), Is.SameAs(target.transform));

            Debug.Log("[IA03][TACTICAL_RESERVATION_TEST] reserved=" + reservedUnit.name
                + " tacticalUnit=" + unreservedUnit.name + " target=" + target.name
                + " queuedUnits=" + orderedUnits.Count);
        }
        finally
        {
            UnsubscribeSceneLoadedCallback();
            if (commandQueue != null && !string.IsNullOrWhiteSpace(enqueuedRequestId))
            {
                MethodInfo cancelPending = commandQueue.GetType().GetMethod("CancelPending", InstanceMembers);
                cancelPending?.Invoke(commandQueue, new object[] { enqueuedRequestId, Time.time, "limpeza da regressão de reserva IA03" });
            }
            if (missionStateCaptured && strategy != null)
            {
                SetMember(strategy, "missaoAtiva", previousActiveMission);
                activeMissionUnits.Clear();
                for (int i = 0; i < previousReservedUnits.Count; i++)
                    activeMissionUnits.Add(previousReservedUnits[i]);
            }
            if (strategyBehaviour != null) strategyBehaviour.enabled = false;
        }
    }

#if UNITY_EDITOR
    [UnityTearDown]
    public IEnumerator RestoreSceneAfterBrainMasterPriorityRegression()
    {
        if (restoreSceneAfterBrainMasterRegression && !string.IsNullOrWhiteSpace(originalScenePathForBrainMasterRegression))
        {
            string restorePath = originalScenePathForBrainMasterRegression;
            restoreSceneAfterBrainMasterRegression = false;
            AsyncOperation restore = EditorSceneManager.LoadSceneAsyncInPlayMode(
                restorePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(restore, Is.Not.Null, "Não foi possível restaurar a cena que estava aberta antes da regressão.");
            while (!restore.isDone) yield return null;
            Debug.Log("[IA03][BRAINMASTER_PRIORITY_TEST][SCENE_RESTORED] " + restorePath);
        }
        else
        {
            int restoredBehaviourCount = behaviourStates.Count;
            RestoreBehaviours();
            Debug.Log("[IA03][TEST_CLEANUP][BEHAVIOURS_RESTORED] scene="
                + (SceneManager.GetActiveScene().IsValid() ? SceneManager.GetActiveScene().path : "<invalid>")
                + " count=" + restoredBehaviourCount);
        }

        if (testOnlyVisibilityServiceObject != null)
        {
            UnityEngine.Object.Destroy(testOnlyVisibilityServiceObject);
            testOnlyVisibilityServiceObject = null;
            yield return null;
        }
    }
#endif

    private void ResetScenarioState()
    {
        UnsubscribeSceneLoadedCallback();
        if (projectileProbeSubscribed)
        {
            Application.logMessageReceived -= CaptureProbeAndReleaseTestProjectiles;
            projectileProbeSubscribed = false;
        }

        behaviourStates.Clear();
        turretStates.Clear();
        projectilesToRelease.Clear();
        currentCaseEvents.Clear();
        government = null;
        relation = null;
        diplomacySnapshot = null;
        shooter = null;
        originalPatrolComponent = null;
        unitController = null;
        navMeshAgent = null;
        originalAgentStopped = false;
        currentCase = null;
        sceneLoadedCallbackSubscribed = false;
        isolationArmed = false;
        unexpectedActivityBeforeIsolation = false;
        originalUnitStateCaptured = false;
        diplomacyWasModified = false;
        writingProbeSummary = false;
        keepCaseProjectileForImpact = false;
        preIsolationEvidence = null;
        currentN2MissionTargetIdentityMarker = null;
        currentN2ShooterIdentityMarker = null;
        selectedTurretState = null;
        automaticPeaceTargetFromC = null;
        validationSceneLoadedForCleanup = default;
    }

    private IEnumerator RunCombatAuthorizationScenario(bool onlyExplicitAttack)
    {
        yield return WaitForPendingValidationSceneUnload();
        ResetScenarioState();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        System.Reflection.Assembly fixtureAssembly = GetType().Assembly;
        Debug.Log("[IA03 FIXTURE V3 EXECUTANDO] marker=" + FixtureBuildMarker
            + " assembly=" + fixtureAssembly.FullName
            + " MVID=" + fixtureAssembly.ManifestModule.ModuleVersionId);

        Scene validationScene = default;
        Application.logMessageReceived += CaptureProbeAndReleaseTestProjectiles;
        projectileProbeSubscribed = true;
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        sceneLoadedCallbackSubscribed = true;

        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (string.Equals(activeScene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
            {
                IsolateValidationScene(activeScene);
                UnsubscribeSceneLoadedCallback();
            }
            else
            {
#if UNITY_EDITOR
                AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ValidationScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                Assert.That(load, Is.Not.Null, "Não foi possível carregar a cena de validação sem salvá-la.");
                while (!load.isDone) yield return null;
                Scene loadedValidationScene = SceneManager.GetSceneByPath(ValidationScenePath);
                Assert.That(loadedValidationScene.IsValid() && loadedValidationScene.isLoaded, Is.True,
                    "A cena de validação aditiva não terminou de carregar.");
                Assert.That(SceneManager.SetActiveScene(loadedValidationScene), Is.True,
                    "Não foi possível tornar a cena de validação ativa.");
#else
                Assert.Fail("A cena de validação precisa ser carregada em Play Mode no Editor.");
                yield break;
#endif
            }

            yield return null;

            validationScene = SceneManager.GetActiveScene();
            Assert.That(validationScene.isLoaded, Is.True, "A cena de validação não terminou de carregar.");
            Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
            Assert.That(validationScene.name, Is.EqualTo("IA03_WarValidation"));
            Assert.That(isolationArmed, Is.True,
                "Os cérebros e sistemas de combate não foram isolados no evento sceneLoaded.");
            Assert.That(unexpectedActivityBeforeIsolation, Is.False,
                "Houve atividade de combate antes do isolamento: " + preIsolationEvidence);
            Assert.That(validationScene.isDirty, Is.False, "A cena já estava dirty antes da fixture começar.");
            Assert.That(FindComponentInScene(validationScene, "IA03ValidationDiplomacyBootstrap"), Is.Not.Null,
                "Bootstrap diplomático da validação não encontrado.");
            Assert.That(FindComponentInScene(validationScene, "NavMeshSurface"), Is.Not.Null,
                "NavMeshSurface real da cena de validação não encontrado.");
            Debug.Log("[IA03][CENARIO] scene=" + validationScene.name
                + " path=" + validationScene.path
                + " buildProfileNeeded=false"
                + " isDirtyInicial=" + validationScene.isDirty
                + " isolationArmed=" + isolationArmed);

            Type governmentType = ResolveType("SistemaGovernoMundial");
            Assert.That(governmentType, Is.Not.Null);
            government = governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null);
            Assert.That(government, Is.Not.Null, "O bootstrap de validação precisa criar o sistema diplomático.");
            relation = Invoke(government, "ObterRelacao", TeamA, TeamB);
            Assert.That(relation, Is.Not.Null, "A relação 1/3 precisa existir no sistema diplomático.");
            diplomacySnapshot = CaptureDiplomacy(government, relation);
            diplomacyWasModified = true;
            ForceDiplomacy(false);

            Transform attackTarget;
            Component chosenTurret;
            string selectionSummary;
            bool querySaturated;
            bool selected = TrySelectShooterAndTarget(validationScene, out shooter, out unitController,
                out navMeshAgent, out chosenTurret, out attackTarget, out selectionSummary, out querySaturated);
            Debug.Log("[IA03][AB][PREPARACAO] " + selectionSummary);
            Assert.That(querySaturated, Is.False,
                "O buffer real de 40 colisores saturou durante a seleção física; teste inconclusivo. " + selectionSummary);
            Assert.That(selected, Is.True,
                "Não foi possível selecionar deterministicamente tanque, torreta e alvo com rota válida. " + selectionSummary);

            originalPosition = shooter.transform.position;
            originalRotation = shooter.transform.rotation;
            originalAgentStopped = navMeshAgent.isStopped;
            originalUnitStateCaptured = true;
            originalPatrolComponent = FindComponent(shooter, "ComportamentoPatrulhaUniversal");
            Assert.That(navMeshAgent.isOnNavMesh, Is.True,
                "A unidade escolhida precisa estar sobre o NavMesh real antes dos testes de patrulha.");

            CaptureShooterTurrets(shooter);
            Assert.That(turretStates.Count, Is.GreaterThan(0), "O tanque não contém ControleTorreta.");
            selectedTurretState = FindTurretState(chosenTurret);
            Assert.That(selectedTurretState, Is.Not.Null, "Nenhuma torreta real elegível foi selecionada.");
            Debug.Log("[IA03][AB][TORRETA_SELECIONADA] shooter=" + shooter.name
                + " turret=" + selectedTurretState.behaviour.name
                + " turretId=" + selectedTurretState.instanceId
                + " range=" + selectedTurretState.range
                + " target=" + attackTarget.name);
            if (!onlyExplicitAttack)
            {
                Vector3[] patrolPoints = BuildValidatedPatrol(navMeshAgent);
                yield return RunNoPatrolCase("B", true, attackTarget);

                StopAndRestorePosition();
                ForceDiplomacy(false);
                yield return RunPatrolCase("A", false, patrolPoints);

                StopAndRestorePosition();
                ForceDiplomacy(false);
                yield return RunNoPatrolCase("C", false, attackTarget);
            }

            StopAndRestorePosition();
            ForceDiplomacy(false);
            yield return RunAttackCase(attackTarget);

            if (!onlyExplicitAttack)
            {
                // Este caso fecha com impacto real; mantenha-o por último para que
                // um dano letal não altere os alvos usados nos casos anteriores.
                yield return RunNoPatrolCase("E", false, attackTarget, true);
            }
        }
        finally
        {
            UnsubscribeSceneLoadedCallback();
            if (projectileProbeSubscribed)
            {
                Application.logMessageReceived -= CaptureProbeAndReleaseTestProjectiles;
                projectileProbeSubscribed = false;
            }

            ReleaseWatchedProjectiles();
            if (originalUnitStateCaptured) StopAndRestorePosition();
            RestoreTurrets();
            if (diplomacyWasModified) RestoreDiplomacy();
            RemovePatrolComponentCreatedByTest();

            if (validationSceneLoadedForCleanup.IsValid() && validationSceneLoadedForCleanup.isLoaded)
            {
                // A cena aditiva é um fixture descartável. Não reative nela os
                // cérebros no teardown: outra UnityTest da mesma bateria poderia
                // herdar um cérebro ativo e disputar as ordens do novo cenário.
                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                    SceneManager.SetActiveScene(originalActiveScene);

                pendingValidationSceneUnload = SceneManager.UnloadSceneAsync(validationSceneLoadedForCleanup);
                Debug.Log("[IA03][AB][SCENE_CLEANUP] unloading=" + validationSceneLoadedForCleanup.path
                    + " sceneCount=" + SceneManager.sceneCount
                    + " unloadStarted=" + (pendingValidationSceneUnload != null));
            }
            else
            {
                RestoreBehaviours();
            }
        }
    }

    private static IEnumerator WaitForPendingValidationSceneUnload()
    {
        while (pendingValidationSceneUnload != null && !pendingValidationSceneUnload.isDone)
            yield return null;

        pendingValidationSceneUnload = null;
    }

    [UnityTest]
    public IEnumerator GroupedScenarioProjectileOwnership_UsesShooterIdentityForTeamsThreeAndOne()
    {
        yield return WaitForPendingValidationSceneUnload();
        GameObject scenario = new GameObject("IA03_TestScenario");
        GameObject projectilePrefab = new GameObject("IA03_OwnershipRegressionProjectile");
        Type projectileType = ResolveType("Projetil");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type turretType = ResolveType("ControleTorreta");
        Type damageType = ResolveType("SistemaDeDanos");
        Assert.That(projectileType, Is.Not.Null);
        Assert.That(identityType, Is.Not.Null);
        Assert.That(turretType, Is.Not.Null);
        Assert.That(damageType, Is.Not.Null);
        projectilePrefab.AddComponent(projectileType);
        GameObject shotFromTeamThree = null;
        GameObject shotFromTeamOne = null;

        try
        {
            GameObject countryA = new GameObject("Valdoria");
            countryA.transform.SetParent(scenario.transform, false);
            GameObject countryB = new GameObject("Karsovia");
            countryB.transform.SetParent(scenario.transform, false);

            GameObject tankA = new GameObject("Tank_A");
            tankA.transform.SetParent(countryA.transform, false);
            Component identityA = tankA.AddComponent(identityType);
            SetMember(identityA, "teamID", TeamA);

            GameObject tankB = new GameObject("Tank_B");
            tankB.transform.SetParent(countryB.transform, false);
            Component identityB = tankB.AddComponent(identityType);
            SetMember(identityB, "teamID", TeamB);

            Component turretB = CreateOwnershipRegressionTurret(
                tankB, identityB, tankA.transform, projectilePrefab, turretType);
            Component projectileB = FireAndAssertGroupedOwner(turretB, tankB, identityB, TeamB, damageType);
            shotFromTeamThree = projectileB.gameObject;

            Component turretA = CreateOwnershipRegressionTurret(
                tankA, identityA, tankB.transform, projectilePrefab, turretType);
            Component projectileA = FireAndAssertGroupedOwner(turretA, tankA, identityA, TeamA, damageType);
            shotFromTeamOne = projectileA.gameObject;
        }
        finally
        {
            if (shotFromTeamThree != null) UnityEngine.Object.Destroy(shotFromTeamThree);
            if (shotFromTeamOne != null) UnityEngine.Object.Destroy(shotFromTeamOne);
            UnityEngine.Object.Destroy(scenario);
            UnityEngine.Object.Destroy(projectilePrefab);
        }

        yield return null;
    }

    private static Component CreateOwnershipRegressionTurret(
        GameObject unit,
        Component identity,
        Transform target,
        GameObject projectilePrefab,
        Type turretType)
    {
        GameObject turretObject = new GameObject("Torre");
        turretObject.transform.SetParent(unit.transform, false);
        Behaviour turret = turretObject.AddComponent(turretType) as Behaviour;
        Assert.That(turret, Is.Not.Null);
        turret.enabled = false;

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(turretObject.transform, false);
        SetMember(turret, "municaoPrefab", projectilePrefab);
        SetMember(turret, "locaisDoTiro", new[] { muzzle.transform });
        SetMember(turret, "descobrirCanosAutomaticamente", false);
        SetMember(turret, "usarRecuoAoDisparar", false);

        SetMember(turret, "minhaIdentidade", identity);
        SetMember(turret, "alvoAtual", target);
        SetMember(turret, "balasAtuais", 1);
        return turret;
    }

    private static Component FireAndAssertGroupedOwner(
        Component turret,
        GameObject expectedOwner,
        Component expectedIdentity,
        int expectedTeam,
        Type damageType)
    {
        MethodInfo fire = turret.GetType().GetMethod("Disparar", InstanceMembers);
        Assert.That(fire, Is.Not.Null, "O produtor real ControleTorreta.Disparar precisa estar disponível.");
        fire.Invoke(turret, null);

        MonoBehaviour[] activeBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        Component shot = null;
        MethodInfo getOwner = ResolveType("Projetil").GetMethod("GetDono", InstanceMembers);
        for (int i = 0; i < activeBehaviours.Length; i++)
        {
            MonoBehaviour candidate = activeBehaviours[i];
            if (candidate == null || candidate.GetType().Name != "Projetil" || getOwner == null) continue;
            if (object.ReferenceEquals(getOwner.Invoke(candidate, null), expectedOwner))
            {
                shot = candidate;
                break;
            }
        }

        Assert.That(shot, Is.Not.Null, "A torreta não produziu projétil com a unidade esperada como autora.");
        Assert.That(getOwner.Invoke(shot, null), Is.SameAs(expectedOwner), "O dono precisa ser a unidade, não a raiz compartilhada.");
        Assert.That(Convert.ToInt32(GetMember(shot, "TeamDono")), Is.EqualTo(expectedTeam));

        MethodInfo resolveIdentity = damageType.GetMethod("ResolverIdentidade", StaticMembers,
            null, new[] { typeof(Component) }, null);
        Assert.That(resolveIdentity, Is.Not.Null);
        Component resolved = resolveIdentity.Invoke(null, new object[] { expectedOwner.transform }) as Component;
        Assert.That(resolved, Is.SameAs(expectedIdentity));
        Assert.That(Convert.ToInt32(GetMember(resolved, "teamID")), Is.EqualTo(expectedTeam),
            "A resolução usada pelo sistema de dano precisa manter a equipe do agressor.");
        return shot;
    }

    private IEnumerator RunPatrolCase(string caseName, bool war, Vector3[] patrolPoints)
    {
        currentCase = caseName;
        currentCaseEvents.Clear();
        ResetEngagementProbes();
        DisableShooterTurrets();

        object command = CreateCommand("Patrol", patrolPoints[0], null, patrolPoints);
        bool accepted = ExecuteOrder(unitController, command);
        bool patrolActive = IsPatrolActive(unitController);
        Debug.Log("[IA03][AB][" + caseName + "][ORDEM] war=" + war
            + " accepted=" + accepted
            + " patrolActive=" + patrolActive
            + " isOnNavMesh=" + navMeshAgent.isOnNavMesh
            + " pathPoints=" + patrolPoints.Length
            + " frame=" + Time.frameCount);

        Assert.That(accepted, Is.True, "A ordem real de patrulha foi recusada no caso " + caseName + ".");
        Assert.That(patrolActive, Is.True, "O controlador não registrou a patrulha real no caso " + caseName + ".");
        EnableSelectedShooterTurret();
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

        Assert.That(Convert.ToBoolean(GetValue(relation, "guerraDeclarada")), Is.False,
            "A ordem Attack explícita precisa começar em paz.");
        Type identityType = ResolveType("IdentidadeUnidade");
        Component targetIdentity = identityType != null ? target.GetComponentInParent(identityType) : null;
        Assert.That(targetIdentity, Is.Not.Null, "O alvo Attack precisa manter uma identidade de equipe.");
        Assert.That(Convert.ToInt32(GetMember(targetIdentity, "teamID")), Is.EqualTo(TeamB));
        Component targetDamage = FindComponentRecursive(target, "SistemaDeDanos");
        Assert.That(targetDamage, Is.Not.Null, "O alvo Attack precisa ter SistemaDeDanos para validar o impacto.");
        float targetHealthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));

        Transform[] priorBefore = ReadTargets("alvoPrioritario");
        object command = CreateCommand("Attack", target.position, target, null);
        bool accepted = ExecuteOrder(unitController, command);
        Transform[] priorityAfterDispatch = ReadTargets("alvoPrioritario");
        Debug.Log("[IA03][AB][D][ORDEM] war=false accepted=" + accepted
            + " explicitTarget=" + target.name
            + " priorityBefore=" + DescribeTargets(priorBefore)
            + " priorityAfterExecute=" + DescribeTargets(priorityAfterDispatch)
            + " priorityClearedAfterMove=" + AreAllTargetsNull(priorityAfterDispatch)
            + " movementAccepted=" + accepted
            + " movementCancellationDefault=true"
            + " movementOrder=" + DescribeMember(unitController, "OrdemMovimentoAtual")
            + " frame=" + Time.frameCount);

        Assert.That(accepted, Is.True,
            "A ordem Attack foi recusada apesar da validação prévia do NavMesh e da posição do alvo.");
        Assert.That(priorityAfterDispatch, Does.Contain(target),
            "A ordem Attack explícita precisa preservar o alvo prioritário depois que o movimento é aceito.");
        keepCaseProjectileForImpact = true;
        SetMember(selectedTurretState.behaviour, "contadorTempo", 1000f);
        EnableSelectedShooterTurret();
        yield return WaitForTurretAlignment(target, 0.5f, 5f);
        SetMember(selectedTurretState.behaviour, "contadorTempo", 0f);
        yield return WaitForAttackSelectionOrTimeout(target, 3f);

        Transform selectedTurretTarget = GetMember(selectedTurretState.behaviour, "alvoAtual") as Transform;
        Assert.That(selectedTurretTarget, Is.SameAs(target),
            "A torreta precisa selecionar o alvo explícito após a ordem Attack.");
        Assert.That(ContainsCaseEvent("selector=alvoPrioritario") || ContainsCaseEvent("explicitTarget=True"), Is.True,
            "A torreta não registrou a seleção pelo caminho prioritário explícito.");
        Assert.That(ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente"), Is.False,
            "A ordem Attack explícita não deve depender da aquisição automática.");
        Assert.That(CountCaseEvents("[DISPARAR_INVOCADO]"), Is.GreaterThan(0),
            "A ordem Attack em paz não chegou a chamar Disparar.");
        Assert.That(CountCaseEvents("[PROJECTILE_PROBE][SPAWN]"), Is.GreaterThan(0),
            "A ordem Attack em paz não criou projétil.");

        CaptureProjectileRouteTrace();
        yield return WaitForDamageProbeOrTimeout(5f);
        keepCaseProjectileForImpact = false;
        DisableShooterTurrets();

        string impactProbe = FindCaseEvent("[PROJECTILE_PROBE][IMPACT]");
        string damageProbe = FindCaseEvent("[PROJECTILE_PROBE][DAMAGE]");
        float targetHealthAfter = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        Debug.Log("[IA03][AB][D][IMPACT_RESULT] target=" + target.name
            + " targetTeam=" + GetMember(targetIdentity, "teamID")
            + " hpBefore=" + targetHealthBefore
            + " hpAfter=" + targetHealthAfter
            + " impact=" + (impactProbe ?? "<none>")
            + " damage=" + (damageProbe ?? "<none>")
            + " warAfter=" + GetValue(relation, "guerraDeclarada"));

        Assert.That(impactProbe, Is.Not.Null,
            "O projétil da ordem Attack não registrou impacto no runtime.");
        Assert.That(impactProbe, Does.Contain("shooter=" + shooter.name + " shooterTeam=" + TeamA),
            "O impacto não preservou a autoria/equipe da unidade que recebeu a ordem Attack.");
        Assert.That(damageProbe, Is.Not.Null,
            "O impacto da ordem Attack não chegou a SistemaDeDanos.");
        Assert.That(damageProbe, Does.Contain("aggressor=" + shooter.name + " aggressorTeam=" + TeamA),
            "SistemaDeDanos não atribuiu o dano ao atacante Team 1.");
        Assert.That(damageProbe, Does.Contain("victimTeam=" + TeamB),
            "SistemaDeDanos não identificou a vítima Team 3.");
        Assert.That(targetHealthAfter, Is.LessThan(targetHealthBefore),
            "O alvo Attack não perdeu vida após o impacto.");
        Assert.That(Convert.ToBoolean(GetValue(relation, "guerraDeclarada")), Is.True,
            "A agressão do impacto Attack em paz não declarou guerra.");

        LogControlledState("D.afterWait", target);
        LogCaseSummary("D", false, accepted, IsPatrolActive(unitController), target);
    }

    private IEnumerator RunNoPatrolCase(string caseName, bool war, Transform target, bool hostilePostureWithoutWar = false)
    {
        currentCase = caseName;
        keepCaseProjectileForImpact = false;
        currentCaseEvents.Clear();
        ResetEngagementProbes();
        ForceDiplomacy(war);
        if (hostilePostureWithoutWar) ForceHostilityWithoutWar();
        DisableShooterTurrets();
        ResetSelectedTurretFiringStateForCase();

        bool stopAccepted = ExecuteOrder(unitController, CreateCommand("Stop", originalPosition, null, null));
        if (hostilePostureWithoutWar) LogControlledState("E.afterStopCommand", target);
        StopAndRestorePosition();
        if (hostilePostureWithoutWar) LogControlledState("E.afterRestore", target);
        MethodInfo setCombatMode = unitController.GetType().GetMethod("DefinirModoCombate", InstanceMembers,
            null, new[] { typeof(bool) }, null);
        MethodInfo setPriority = unitController.GetType().GetMethod("DefinirAlvoPrioritario", InstanceMembers,
            null, new[] { typeof(Transform) }, null);
        Assert.That(setCombatMode, Is.Not.Null, "DefinirModoCombate não encontrado para o cenário controlado.");
        Assert.That(setPriority, Is.Not.Null, "DefinirAlvoPrioritario não encontrado para o cenário controlado.");
        setPriority.Invoke(unitController, new object[] { null });

        Type identityType = ResolveType("IdentidadeUnidade");
        Component shooterIdentity = identityType != null
            ? FindComponentRecursive(shooter.transform, identityType.Name)
            : null;
        Component targetIdentity = target != null && identityType != null
            ? target.GetComponentInParent(identityType)
            : null;
        Assert.That(shooterIdentity, Is.Not.Null, "A unidade atiradora não possui IdentidadeUnidade.");
        Assert.That(targetIdentity, Is.Not.Null, "A unidade alvo não possui IdentidadeUnidade ancestral.");

        // O isolamento desativa as torretas antes do primeiro Start. Deixe o
        // componente executar seu ciclo de inicialização sem modo de combate
        // antes de medir o gate diretamente; até Start, meuTime é -1.
        setCombatMode.Invoke(unitController, new object[] { false });
        EnableSelectedShooterTurret();
        yield return null;
        DisableShooterTurrets();
        ResetSelectedTurretFiringStateForCase();
        int initializedShooterTeam = Convert.ToInt32(GetMember(selectedTurretState.behaviour, "meuTime"));
        int expectedShooterTeam = Convert.ToInt32(GetMember(shooterIdentity, "teamID"));
        Assert.That(initializedShooterTeam, Is.EqualTo(expectedShooterTeam),
            "A torreta precisa inicializar sua equipe antes de avaliar autorização diplomática.");

        setCombatMode.Invoke(unitController, new object[] { true });
        bool patrolActive = IsPatrolActive(unitController);
        Assert.That(stopAccepted, Is.True, "O controlador recusou a ordem Stop do caso C.");
        Assert.That(patrolActive, Is.False, "O teste precisa estar sem ordem de patrulha ativa.");

        MethodInfo automaticGate = selectedTurretState.behaviour.GetType().GetMethod("PodeAtacarAutomaticamente", InstanceMembers);
        MethodInfo requiresWarGate = selectedTurretState.behaviour.GetType().GetMethod("NavioExigeGuerraDeclarada", InstanceMembers);
        MethodInfo isTargetDemilitarized = government.GetType().GetMethod("EstaPosicaoDesmilitarizada", InstanceMembers,
            null, new[] { typeof(int), typeof(Vector3) }, null);
        Assert.That(automaticGate, Is.Not.Null, "PodeAtacarAutomaticamente não encontrado na torreta selecionada.");
        Assert.That(requiresWarGate, Is.Not.Null, "Consulta do gate naval não encontrada na torreta selecionada.");
        bool gateResult = (bool)automaticGate.Invoke(selectedTurretState.behaviour, new[] { targetIdentity });
        bool requiresDeclaredWar = (bool)requiresWarGate.Invoke(selectedTurretState.behaviour, null);
        bool warWasDeclaredBeforeShot = Convert.ToBoolean(GetValue(relation, "guerraDeclarada"));
        bool targetIsDemilitarized = isTargetDemilitarized != null
            && (bool)isTargetDemilitarized.Invoke(government, new object[]
            {
                Convert.ToInt32(GetMember(targetIdentity, "teamID")),
                targetIdentity.transform.position
            });
        if (hostilePostureWithoutWar)
        {
            Assert.That(warWasDeclaredBeforeShot, Is.False,
                "O caso E precisa começar sem guerra declarada antes do disparo.");
            keepCaseProjectileForImpact = true;
            // Segure o disparo até a mira se estabilizar. O jogo aceita abrir
            // fogo dentro de 5 graus; a essa distância isso ainda pode errar o alvo.
            SetMember(selectedTurretState.behaviour, "contadorTempo", 1000f);
        }

        EnableSelectedShooterTurret();
        Debug.Log("[IA03][AB][" + caseName + "][GATE] war=" + war
            + " relationWar=" + GetValue(relation, "guerraDeclarada")
            + " shooter=" + shooter.name
            + " shooterTeam=" + GetMember(shooterIdentity, "teamID")
            + " target=" + target.name
            + " targetTeam=" + GetMember(targetIdentity, "teamID")
            + " initializedShooterTeam=" + initializedShooterTeam
            + " directPodeAtacarAutomaticamenteResult=" + gateResult
            + " requiresDeclaredWarGate=" + requiresDeclaredWar
            + " targetDemilitarized=" + targetIsDemilitarized
            + " ceasefire=" + GetValue(relation, "cessarFogoAtivo")
            + " postureAtoB=" + GetValue(relation, "posturaAParaB")
            + " postureBtoA=" + GetValue(relation, "posturaBParaA")
            + " hostilePostureWithoutWar=" + hostilePostureWithoutWar
            + " ammo=" + GetMember(selectedTurretState.behaviour, "balasAtuais")
            + "/" + GetMember(selectedTurretState.behaviour, "tamanhoCartucho")
            + " reloading=" + GetMember(selectedTurretState.behaviour, "estaRecarregando")
            + " reloadTimer=" + GetMember(selectedTurretState.behaviour, "contadorTempo")
            + " modeCombat=" + GetMember(unitController, "modoCombateOficialAtivo")
            + " Creaty=none mission=none");
        Debug.Log("[IA03][AB][" + caseName + "][ORDEM] war=" + war + " acceptedStop=" + stopAccepted
            + " patrolActive=" + patrolActive
            + " order=" + DescribeMember(unitController, "OrdemAtual")
            + " movementOrder=" + DescribeMember(unitController, "OrdemMovimentoAtual")
            + " frame=" + Time.frameCount);

        if (hostilePostureWithoutWar)
        {
            yield return WaitForTurretAlignment(target, 0.5f, 5f);
            SetMember(selectedTurretState.behaviour, "contadorTempo", 0f);
        }
        yield return WaitForShotOrTimeout(3f);
        if (hostilePostureWithoutWar)
        {
            // Disparar registra o probe antes de chamar SetDirecao; observe
            // um frame depois para medir a direção efetivamente aplicada.
            yield return null;
            LogControlledState("E.afterShotWait", target);
            CaptureProjectileRouteTrace();
            yield return WaitForDamageProbeOrTimeout(3f);
            keepCaseProjectileForImpact = false;
        }
        LogCaseSummary(caseName, war, stopAccepted, patrolActive, null);
        DisableShooterTurrets();

        if (caseName == "B")
        {
            Assert.That(gateResult, Is.True,
                "O gate direto recusou um alvo terrestre fisicamente alcançável durante guerra declarada.");
            Assert.That(ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente"), Is.True,
                "A torre não selecionou o alvo pelo caminho automático no caso B.");
            Assert.That(CountCaseEvents("[DISPARAR_INVOCADO]"), Is.GreaterThan(0),
                "A torre não chegou a Disparar no caso B controlado.");
            Assert.That(CountCaseEvents("[PROJECTILE_PROBE][SPAWN]"), Is.GreaterThan(0),
                "A torre não criou projétil no caso B controlado.");
        }
        else if (caseName == "C")
        {
            Assert.That(gateResult, Is.False,
                "A torre terrestre autorizou tiro automático com relação neutra e sem guerra declarada.");
            Assert.That(ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente"), Is.False,
                "A torre selecionou alvo automaticamente durante a paz neutra.");
            Assert.That(CountCaseEvents("[DISPARAR_INVOCADO]"), Is.EqualTo(0),
                "A torre tentou disparar automaticamente durante a paz neutra.");
            Assert.That(CountCaseEvents("[PROJECTILE_PROBE][SPAWN]"), Is.EqualTo(0),
                "A torre criou projétil durante a paz neutra.");
        }
        else if (caseName == "E")
        {
            Assert.That(gateResult, Is.True,
                "A postura hostil sem guerra declarada deveria autorizar o gate automático terrestre.");
            Assert.That(ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente"), Is.True,
                "A torre não selecionou o alvo sob postura hostil sem guerra declarada.");
            Assert.That(CountCaseEvents("[DISPARAR_INVOCADO]"), Is.GreaterThan(0),
                "A torre não chegou ao disparo sob postura hostil sem guerra declarada.");
            Assert.That(CountCaseEvents("[PROJECTILE_PROBE][SPAWN]"), Is.GreaterThan(0),
                "A torreta não disparou com postura hostil sem guerra declarada.");
            string firstDamage = FindCaseEvent("[IA03][PROJECTILE_PROBE][DAMAGE]");
            Assert.That(firstDamage, Is.Not.Null,
                "O projétil do caso E não atingiu uma unidade nem registrou o dano em SistemaDeDanos.");
            Assert.That(firstDamage, Does.Contain("aggressor=" + shooter.name + " aggressorTeam=" + TeamA),
                "O primeiro dano precisa identificar a unidade/equipe que realmente disparou.");
            Assert.That(firstDamage, Does.Contain("victim=" + target.name + " victimTeam=" + TeamB),
                "O primeiro dano precisa identificar a unidade/equipe atingida.");
            Assert.That(GetValue(relation, "guerraDeclarada"), Is.True,
                "O impacto hostil deveria converter a postura em guerra declarada.");
        }
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

    private IEnumerator WaitForAttackSelectionOrTimeout(Transform target, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        string previousState = null;
        Type submarineType = ResolveType("ControleSubmarino");
        MethodInfo canBeConventionalTarget = submarineType != null
            ? submarineType.GetMethod("PodeSerAlvoConvencional", StaticMembers, null, new[] { typeof(Transform) }, null)
            : null;
        MethodInfo closestPoint = selectedTurretState != null && selectedTurretState.behaviour != null
            ? selectedTurretState.behaviour.GetType().GetMethod("ObterPontoMaisProximoSeguro", StaticMembers,
                null, new[] { typeof(Collider), typeof(Vector3) }, null)
            : null;

        while (Time.realtimeSinceStartup < deadline)
        {
            string state = DescribeAttackTurretStates(target, canBeConventionalTarget, closestPoint);
            if (!string.Equals(state, previousState, StringComparison.Ordinal))
            {
                Debug.Log("[IA03][D][TORRETAS] frame=" + Time.frameCount
                    + " realtime=" + Time.realtimeSinceStartup.ToString("F2")
                    + " " + state);
                previousState = state;
            }

            if (HasDischargeOrSpawn()) break;
            yield return null;
        }

        Debug.Log("[IA03][D][PROBES] total=" + currentCaseEvents.Count
            + " eventos=" + (currentCaseEvents.Count > 0 ? string.Join(" || ", currentCaseEvents.ToArray()) : "nenhum"));
        Debug.Log("[IA03][D][ESTADO_FINAL] frame=" + Time.frameCount
            + " " + DescribeAttackTurretStates(target, canBeConventionalTarget, closestPoint));
    }

    private string DescribeAttackTurretStates(
        Transform target,
        MethodInfo canBeConventionalTarget,
        MethodInfo closestPoint)
    {
        StringBuilder summary = new StringBuilder(640);
        Type identityType = ResolveType("IdentidadeUnidade");
        Component targetIdentity = target != null && identityType != null
            ? target.GetComponentInParent(identityType)
            : null;
        bool targetActive = target != null && target.gameObject.activeInHierarchy;
        bool conventional = target != null && canBeConventionalTarget != null
            && Convert.ToBoolean(canBeConventionalTarget.Invoke(null, new object[] { target }));
        string targetTeam = targetIdentity != null ? Convert.ToString(GetMember(targetIdentity, "teamID")) : "none";

        for (int i = 0; i < turretStates.Count; i++)
        {
            TurretState state = turretStates[i];
            Behaviour turret = state.behaviour;
            if (turret == null) continue;

            Transform priority = GetMember(turret, "alvoPrioritario") as Transform;
            Transform current = GetMember(turret, "alvoAtual") as Transform;
            Component unitControl = GetMember(turret, "meuControle") as Component;
            bool combatMode = unitControl != null
                && Convert.ToBoolean(GetMember(unitControl, "ModoCombateAtivo") ?? false);
            bool passive = Convert.ToBoolean(GetMember(turret, "modoPassivo") ?? false);
            bool automaticBlocked = Convert.ToBoolean(GetMember(turret, "bloquearMovimentoAutomatico") ?? false);
            bool manualNaval = Convert.ToBoolean(GetMember(turret, "modoManualNave") ?? false);
            bool antiAir = Convert.ToBoolean(GetMember(turret, "souAntiAereo") ?? false);
            float range = Convert.ToSingle(GetMember(turret, "alcance") ?? 0f);
            float nextSearch = Convert.ToSingle(GetMember(turret, "proximaBuscaAlvo") ?? 0f);
            Collider priorityCollider = priority != null ? priority.GetComponentInChildren<Collider>() : null;
                Vector3 priorityPoint = priority != null ? priority.position : Vector3.zero;
            if (priorityCollider != null && closestPoint != null)
            {
                priorityPoint = (Vector3)closestPoint.Invoke(null, new object[] { priorityCollider, turret.transform.position });
            }

            bool priorityActive = priority != null && priority.gameObject.activeInHierarchy;
            bool priorityIsRequested = priority == target;
            float distance = priority != null ? Vector3.Distance(turret.transform.position, priorityPoint) : -1f;
            bool withinRange = priority != null && distance * distance <= range * range;

            if (summary.Length > 0) summary.Append(" | ");
            summary.Append(turret.name).Append("#").Append(turret.GetInstanceID())
                .Append(" enabled=").Append(turret.enabled)
                .Append(" active=").Append(turret.gameObject.activeInHierarchy)
                .Append(" activeAndEnabled=").Append(turret.isActiveAndEnabled)
                .Append(" wasEnabled=").Append(state.wasEnabled)
                .Append(" passive=").Append(passive)
                .Append(" autoBlocked=").Append(automaticBlocked)
                .Append(" combatMode=").Append(combatMode)
                .Append(" manualNaval=").Append(manualNaval)
                .Append(" antiAir=").Append(antiAir)
                .Append(" priority=").Append(priority != null ? priority.name : "none")
                .Append(" priorityIsRequested=").Append(priorityIsRequested)
                .Append(" priorityActive=").Append(priorityActive)
                .Append(" current=").Append(current != null ? current.name : "none")
                .Append(" targetActive=").Append(targetActive)
                .Append(" targetConventional=").Append(conventional)
                .Append(" targetTeam=").Append(targetTeam)
                .Append(" distance=").Append(distance.ToString("F2"))
                .Append(" range=").Append(range.ToString("F2"))
                .Append(" withinRange=").Append(withinRange)
                .Append(" nextSearch=").Append(nextSearch.ToString("F3"))
                .Append(" currentAmmo=").Append(GetMember(turret, "balasAtuais") ?? "n/a")
                .Append(" reloading=").Append(GetMember(turret, "estaRecarregando") ?? "n/a");
        }

        return summary.Length > 0 ? summary.ToString() : "nenhuma torreta capturada";
    }

    private IEnumerator WaitForTurretAlignment(Transform target, float maxAngle, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        float angle = GetSelectedBarrelAngleToTarget(target);
        while (angle > maxAngle && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            angle = GetSelectedBarrelAngleToTarget(target);
        }

        Debug.Log("[IA03][AB][" + currentCase + "][AIM] angle=" + angle.ToString("F2")
            + " maxAngle=" + maxAngle.ToString("F2")
            + " timeout=" + timeout.ToString("F1")
            + " root=" + (shooter != null ? shooter.transform.position.ToString("F2") : "none")
            + " target=" + (target != null ? target.position.ToString("F2") : "none")
            + " yaw=" + (selectedTurretState.behaviour != null
                ? (GetMember(selectedTurretState.behaviour, "pecaQueGira") as Transform)?.localEulerAngles.ToString("F2") ?? "none"
                : "none")
            + " yawOffset=" + GetMember(selectedTurretState.behaviour, "offsetAzimuteCano")
            + " muzzle=" + GetSelectedMuzzleDescription(target)
            + " turretEnabled=" + (selectedTurretState.behaviour != null && selectedTurretState.behaviour.enabled));
        Assert.That(angle, Is.LessThanOrEqualTo(maxAngle),
            "A torre não estabilizou a mira antes do disparo controlado do caso " + currentCase + ".");
    }

    private float GetSelectedBarrelAngleToTarget(Transform target)
    {
        if (target == null || selectedTurretState == null || selectedTurretState.behaviour == null)
            return float.PositiveInfinity;

        Transform[] barrels = GetMember(selectedTurretState.behaviour, "locaisDoTiro") as Transform[];
        if (barrels == null || barrels.Length == 0) return float.PositiveInfinity;

        int index = Convert.ToInt32(GetMember(selectedTurretState.behaviour, "indiceBarrilAtual") ?? 0);
        Transform barrel = barrels[Mathf.Clamp(index, 0, barrels.Length - 1)];
        return barrel != null
            ? Vector3.Angle(barrel.forward, target.position - barrel.position)
            : float.PositiveInfinity;
    }

    private string GetSelectedMuzzleDescription(Transform target)
    {
        if (selectedTurretState == null || selectedTurretState.behaviour == null) return "none";
        Transform[] barrels = GetMember(selectedTurretState.behaviour, "locaisDoTiro") as Transform[];
        if (barrels == null || barrels.Length == 0) return "none";
        int index = Convert.ToInt32(GetMember(selectedTurretState.behaviour, "indiceBarrilAtual") ?? 0);
        Transform barrel = barrels[Mathf.Clamp(index, 0, barrels.Length - 1)];
        return barrel != null
            ? "pos=" + barrel.position.ToString("F2")
                + " forward=" + barrel.forward.ToString("F2")
                + " toTarget=" + (target != null ? (target.position - barrel.position).ToString("F2") : "none")
            : "none";
    }

    private static bool HasLineOfSight(Transform shooter, Transform muzzle, Transform target, out string blocker)
    {
        blocker = string.Empty;
        Vector3 offset = target.position - muzzle.position;
        float distance = offset.magnitude;
        if (distance <= 0.001f) return true;

        RaycastHit[] hits = Physics.RaycastAll(
            muzzle.position,
            offset / distance,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform.IsChildOf(shooter)) continue;

            for (Transform candidate = hitTransform; candidate != null; candidate = candidate.parent)
            {
                if (candidate == target) return true;
            }

            blocker = hits[i].collider != null ? hits[i].collider.name : hitTransform.name;
            return false;
        }

        return true;
    }

    private IEnumerator WaitForDamageProbeOrTimeout(float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (ContainsCaseEvent("[PROJECTILE_PROBE][DAMAGE]")) yield break;
            yield return null;
        }
    }

    private void LogCaseSummary(string caseName, bool war, bool orderAccepted, bool patrolActive, Transform explicitTarget)
    {
        bool explicitSelection = ContainsCaseEvent("selector=alvoPrioritario")
            || ContainsCaseEvent("explicitTarget=True");
        bool automaticSelection = ContainsCaseEvent("selector=ProcurarAlvo/PodeAtacarAutomaticamente");
        int dischargeCount = CountCaseEvents("[DISPARAR_INVOCADO]");
        int projectileCount = CountCaseEvents("[PROJECTILE_PROBE][SPAWN]");
        string automaticTarget = ExtractAutomaticTarget();
        if (caseName == "C") automaticPeaceTargetFromC = automaticTarget;

        Debug.Log("[IA03][AB][" + caseName + "][RESULTADO] war=" + war
            + " relationWar=" + GetValue(relation, "guerraDeclarada")
            + " orderAccepted=" + orderAccepted
            + " patrolActive=" + patrolActive
            + " explicitTarget=" + (explicitTarget != null ? explicitTarget.name : "none")
            + " priorityNow=" + DescribeTargets(ReadTargets("alvoPrioritario"))
            + " explicitBranchObserved=" + explicitSelection
            + " automaticBranchObserved=" + automaticSelection
            + " autoCandidateSelected=" + automaticSelection
            + " automaticTargetObserved=" + (automaticTarget ?? "none")
            + " automaticPeaceTargetFromC=" + (automaticPeaceTargetFromC ?? "not-observed")
            + " explicitDTargetWasSelectedByPhysics=true"
            + " dischargeEvents=" + dischargeCount
            + " projectileSpawns=" + projectileCount
            + " frame=" + Time.frameCount);

        int capturedProbeCount = currentCaseEvents.Count;
        int probeLines = Mathf.Min(capturedProbeCount, MaxProbeLinesPerCase);
        writingProbeSummary = true;
        try
        {
            for (int i = 0; i < probeLines; i++)
            {
                Debug.Log("[IA03][AB][" + caseName + "][PROBE] " + currentCaseEvents[i]);
            }
            if (capturedProbeCount > probeLines)
            {
                Debug.Log("[IA03][AB][" + caseName + "][PROBE] omitidos="
                    + (capturedProbeCount - probeLines) + " total=" + capturedProbeCount);
            }
        }
        finally
        {
            writingProbeSummary = false;
        }
    }

    private string ExtractAutomaticTarget()
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
        {
            string entry = currentCaseEvents[i];
            if (entry.IndexOf("selector=ProcurarAlvo/PodeAtacarAutomaticamente", StringComparison.Ordinal) < 0) continue;
            int start = entry.IndexOf(" target=", StringComparison.Ordinal);
            if (start < 0) continue;
            start += " target=".Length;
            int end = entry.IndexOf(" path=", start, StringComparison.Ordinal);
            if (end < 0) continue;
            return entry.Substring(start, end - start);
        }
        return null;
    }

    private void CaptureProbeAndReleaseTestProjectiles(string condition, string stackTrace, LogType type)
    {
        if (writingProbeSummary) return;
        if (string.IsNullOrEmpty(condition)) return;
        bool isEngagement = condition.IndexOf("[IA03][ENGAGEMENT_PROBE]", StringComparison.Ordinal) >= 0;
        bool isSpawn = condition.IndexOf("[IA03][PROJECTILE_PROBE][SPAWN]", StringComparison.Ordinal) >= 0;
        bool isImpact = condition.IndexOf("[IA03][PROJECTILE_PROBE][IMPACT]", StringComparison.Ordinal) >= 0;
        bool isDamage = condition.IndexOf("[IA03][PROJECTILE_PROBE][DAMAGE]", StringComparison.Ordinal) >= 0
            || condition.IndexOf("[IA03][Validação][PrimeiroDano]", StringComparison.Ordinal) >= 0;
        if (!isEngagement && !isSpawn && !isImpact && !isDamage) return;

        if (!isolationArmed
            && string.Equals(SceneManager.GetActiveScene().path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
        {
            unexpectedActivityBeforeIsolation = true;
            if (preIsolationEvidence == null) preIsolationEvidence = condition;
            DisableRelevantBehaviours(SceneManager.GetActiveScene());
            if (isSpawn) ReleaseWatchedProjectiles(true);
            return;
        }

        if (isImpact || isDamage)
        {
            if (currentCase == "D" || currentCase == "E" || currentCase == "N2")
                currentCaseEvents.Add(condition);
            return;
        }

        Component probedTurret = currentCase == "N2" ? FindCapturedTurretForProbe(condition) : null;
        if (!IsSelectedTurretProbe(condition) && probedTurret == null) return;
        currentCaseEvents.Add(condition);
        if (currentCase == "N2" && isSpawn && !ContainsN2MissionTargetImpact())
            RearmProjectileProbeForTurret(probedTurret != null ? probedTurret : selectedTurretState.behaviour);
        if (isSpawn && !keepCaseProjectileForImpact) ReleaseWatchedProjectiles();
    }

    private bool ContainsN2MissionTargetImpact()
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
        {
            string entry = currentCaseEvents[i];
            if (entry.IndexOf("[PROJECTILE_PROBE][IMPACT]", StringComparison.Ordinal) >= 0
                && !string.IsNullOrEmpty(currentN2MissionTargetIdentityMarker)
                && entry.IndexOf("targetIdentity=" + currentN2MissionTargetIdentityMarker, StringComparison.Ordinal) >= 0
                && entry.IndexOf("targetTeam=" + TeamB, StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private void RearmProjectileProbeForTurret(Component turret)
    {
        if (turret == null) return;
        Type projectileType = ResolveType("Projetil");
        FieldInfo field = projectileType != null
            ? projectileType.GetField("torretasComDisparoRastreado", StaticMembers)
            : null;
        HashSet<int> trackedTurrets = field != null ? field.GetValue(null) as HashSet<int> : null;
        trackedTurrets?.Remove(turret.GetInstanceID());
    }

    private string FindCaseEventContaining(params string[] markers)
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
        {
            string entry = currentCaseEvents[i];
            bool matches = true;
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                if (entry.IndexOf(markers[markerIndex], StringComparison.Ordinal) < 0)
                {
                    matches = false;
                    break;
                }
            }
            if (matches) return entry;
        }
        return null;
    }

    private static string ExtractProjectileProbeId(string entry)
    {
        if (string.IsNullOrEmpty(entry)) return string.Empty;
        const string marker = "id=";
        int start = entry.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += marker.Length;
        int end = entry.IndexOf(' ', start);
        if (end < 0) end = entry.Length;
        return entry.Substring(start, end - start);
    }

    private void CaptureProjectileRouteTrace()
    {
        Type projectileType = ResolveType("Projetil");
        FieldInfo activeField = projectileType != null
            ? projectileType.GetField("ativosNoMapa", StaticMembers)
            : null;
        IEnumerable active = activeField != null ? activeField.GetValue(null) as IEnumerable : null;
        if (active == null || selectedTurretState == null) return;

        MethodInfo getOwner = projectileType.GetMethod("GetDono", InstanceMembers);
        foreach (object item in active)
        {
            Component projectile = item as Component;
            if (projectile == null || getOwner == null || !object.ReferenceEquals(getOwner.Invoke(projectile, null), shooter)) continue;

            Vector3 origin = projectile.transform.position;
            Vector3 direction = GetMember(projectile, "direcaoCustom") is Vector3 customDirection
                ? customDirection
                : projectile.transform.forward;
            Transform target = GetMember(selectedTurretState.behaviour, "alvoAtual") as Transform;
            Vector3 targetRoot = target != null ? target.position : Vector3.zero;
            Vector3 targetColliderCenter = targetRoot;
            string targetColliderName = "<none>";
            if (target != null)
            {
                Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
                float closestDistanceSqr = float.PositiveInfinity;
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider candidate = colliders[i];
                    if (candidate == null || candidate.isTrigger) continue;
                    Vector3 closest = candidate.ClosestPoint(origin);
                    float distanceSqr = (closest - origin).sqrMagnitude;
                    if (distanceSqr >= closestDistanceSqr) continue;
                    closestDistanceSqr = distanceSqr;
                    targetColliderCenter = candidate.bounds.center;
                    targetColliderName = candidate.name;
                }
            }

            string actualPath = TraceFirstCollider(projectile.gameObject, shooter, origin, direction);
            Vector3 towardCollider = targetColliderCenter - origin;
            string colliderCenterPath = towardCollider.sqrMagnitude > 0.001f
                ? TraceFirstCollider(projectile.gameObject, shooter, origin, towardCollider.normalized)
                : "<degenerate>";
            object aimMode = GetMember(selectedTurretState.behaviour, "direcionarProjetilParaPredicao");
            Debug.Log("[IA03][PROJECTILE_TRACE][SHOT] case=" + currentCase
                + " projectile=" + projectile.name
                + " owner=" + shooter.name
                + " team=" + GetMember(projectile, "TeamDono")
                + " origin=" + origin
                + " direction=" + direction
                + " speed=" + GetMember(projectile, "velocidade")
                + " target=" + (target != null ? target.name : "<null>")
                + " targetRoot=" + targetRoot
                + " targetCollider=" + targetColliderName
                + " targetColliderCenter=" + targetColliderCenter
                + " angleToRoot=" + Vector3.Angle(direction, targetRoot - origin).ToString("F2")
                + " angleToCollider=" + Vector3.Angle(direction, towardCollider).ToString("F2")
                + " predictionAim=" + aimMode
                + " firstHitAlongShot=" + actualPath
                + " firstHitTowardColliderCenter=" + colliderCenterPath);
            return;
        }

        Debug.LogWarning("[IA03][PROJECTILE_TRACE][SHOT] Não encontrei projétil ativo do atirador para medir a trajetória.");
    }

    private void LogControlledState(string stage, Transform target)
    {
        if (shooter == null || unitController == null || selectedTurretState == null
            || selectedTurretState.behaviour == null)
        {
            Debug.Log("[IA03][AB][STATE] stage=" + stage + " fixtureReferences=missing");
            return;
        }

        object movementOrder = GetMember(unitController, "OrdemMovimentoAtual");
        object movementState = GetMember(movementOrder, "Estado");
        object movementType = GetMember(movementOrder, "Tipo");
        object movementDestination = GetMember(movementOrder, "Destino");
        bool hasAgentPath = navMeshAgent != null && navMeshAgent.enabled
            && navMeshAgent.isOnNavMesh && navMeshAgent.hasPath;
        string agentState = navMeshAgent == null
            ? "none"
            : "enabled=" + navMeshAgent.enabled
                + ",onNavMesh=" + navMeshAgent.isOnNavMesh
                + ",stopped=" + navMeshAgent.isStopped
                + ",hasPath=" + hasAgentPath
                + ",pathPending=" + navMeshAgent.pathPending
                + ",next=" + (navMeshAgent.enabled && navMeshAgent.isOnNavMesh
                    ? navMeshAgent.nextPosition.ToString("F2") : "n/a")
                + ",remaining=" + (hasAgentPath ? navMeshAgent.remainingDistance.ToString("F2") : "n/a")
                + ",velocity=" + navMeshAgent.velocity.ToString("F2");

        Transform rotatingPart = selectedTurretState.rotatingPart as Transform;
        Transform[] barrels = GetMember(selectedTurretState.behaviour, "locaisDoTiro") as Transform[];
        int barrelIndex = Convert.ToInt32(GetMember(selectedTurretState.behaviour, "indiceBarrilAtual") ?? 0);
        Transform barrel = barrels != null && barrels.Length > 0
            ? barrels[Mathf.Clamp(barrelIndex, 0, barrels.Length - 1)]
            : null;
        float barrelAngleToTarget = target != null && barrel != null
            ? Vector3.Angle(barrel.forward, target.position - barrel.position)
            : -1f;

        Debug.Log("[IA03][AB][STATE] stage=" + stage
            + " root=" + shooter.transform.position.ToString("F2")
            + " rootRotation=" + shooter.transform.rotation.eulerAngles.ToString("F1")
            + " possuiDestino=" + GetMember(unitController, "PossuiDestinoOrdenado")
            + " order=" + DescribeMember(unitController, "OrdemAtual")
            + " movementOrderState=" + (movementState != null ? movementState.ToString() : "none")
            + " movementOrderType=" + (movementType != null ? movementType.ToString() : "none")
            + " movementDestination=" + (movementDestination != null ? movementDestination.ToString() : "none")
            + " agent=" + agentState
            + " turretEnabled=" + selectedTurretState.behaviour.enabled
            + " rotatingPart=" + (rotatingPart != null ? rotatingPart.localRotation.eulerAngles.ToString("F1") : "none")
            + " barrel=" + (barrel != null ? barrel.forward.ToString("F2") : "none")
            + " barrelTargetAngle=" + barrelAngleToTarget.ToString("F2")
            + " misalignedFire=" + GetMember(selectedTurretState.behaviour, "dispararMesmoDesalinhado")
            + " target=" + (target != null ? target.position.ToString("F2") : "none")
            + " frame=" + Time.frameCount);
    }

    private string TraceFirstCollider(GameObject projectile, GameObject owner, Vector3 origin, Vector3 direction)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            projectileTraceBuffer,
            300f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearest = default;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = projectileTraceBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.gameObject == projectile) continue;
            if (owner != null && (hit.collider.gameObject == owner || hit.transform.IsChildOf(owner.transform))) continue;
            if (hit.distance >= nearestDistance) continue;
            nearestDistance = hit.distance;
            nearest = hit;
            found = true;
        }

        return found
            ? nearest.collider.name + " distance=" + nearest.distance.ToString("F2") + " point=" + nearest.point
            : "<no collider within 300>";
    }

    private bool IsSelectedTurretProbe(string condition)
    {
        if (selectedTurretState == null || selectedTurretState.instanceId == 0)
        {
            return false;
        }

        string marker = "turretComponentId=" + selectedTurretState.instanceId;
        int markerIndex = condition.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        int valueEnd = markerIndex + marker.Length;
        return valueEnd == condition.Length || char.IsWhiteSpace(condition[valueEnd]);
    }

    private Component FindCapturedTurretForProbe(string condition)
    {
        const string marker = "turretComponentId=";
        int markerIndex = condition != null ? condition.IndexOf(marker, StringComparison.Ordinal) : -1;
        if (markerIndex < 0) return null;

        int valueStart = markerIndex + marker.Length;
        int valueEnd = valueStart;
        while (valueEnd < condition.Length
            && (char.IsDigit(condition[valueEnd]) || condition[valueEnd] == '-'))
        {
            valueEnd++;
        }
        if (!int.TryParse(condition.Substring(valueStart, valueEnd - valueStart), out int instanceId))
            return null;

        for (int i = 0; i < turretStates.Count; i++)
        {
            if (turretStates[i].instanceId == instanceId)
                return turretStates[i].behaviour;
        }
        return null;
    }

    private void ReleaseWatchedProjectiles(bool releaseAnyValidationTeam = false)
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
            bool shouldRelease = owner == shooter;
            if (!shouldRelease && releaseAnyValidationTeam && owner != null)
            {
                Type identityType = ResolveType("IdentidadeUnidade");
                Component identity = identityType != null ? owner.GetComponentInParent(identityType) : null;
                int ownerTeam = identity != null ? Convert.ToInt32(GetMember(identity, "teamID")) : -1;
                shouldRelease = ownerTeam == TeamA || ownerTeam == TeamB;
            }

            if (shouldRelease)
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

    private void ForceHostilityWithoutWar()
    {
        SetEnumMember(relation, "posturaAParaB", "Inimigo");
        SetEnumMember(relation, "posturaBParaA", "Inimigo");
        SetMember(relation, "guerraDeclarada", false);
        SetMember(relation, "cessarFogoAtivo", false);

        object countryA = Invoke(government, "ObterPais", TeamA);
        object countryB = Invoke(government, "ObterPais", TeamB);
        SetMember(countryA, "emGuerra", false);
        SetMember(countryA, "rivalTeamId", -1);
        SetMember(countryB, "emGuerra", false);
        SetMember(countryB, "rivalTeamId", -1);
        Invoke(government, "NotificarGovernoAtualizado");

        Type visibilityType = ResolveType("Hegemonia.RTS.RTSVisibilityService");
        MethodInfo teamsAtWar = visibilityType != null
            ? visibilityType.GetMethod("TeamsAtWar", StaticMembers)
            : null;
        bool actualWar = teamsAtWar != null && (bool)teamsAtWar.Invoke(null, new object[] { TeamA, TeamB });
        Assert.That(actualWar, Is.False, "Postura hostil não pode marcar guerra declarada no caso E.");
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

    private void OnValidationSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase)) return;
        validationSceneLoadedForCleanup = scene;
        IsolateValidationScene(scene);
        UnsubscribeSceneLoadedCallback();
    }

    private void IsolateValidationScene(Scene scene)
    {
        if (isolationArmed || !scene.IsValid() || !scene.isLoaded) return;
        DisableRelevantBehaviours(scene);
        isolationArmed = true;
        Debug.Log("[IA03][AB][ISOLAMENTO] cena=" + scene.name
            + " componentesSalvos=" + behaviourStates.Count
            + " componentesDesativados=" + CountDisabledRelevantBehaviours(scene)
            + " frame=" + Time.frameCount);
    }

    private void UnsubscribeSceneLoadedCallback()
    {
        if (!sceneLoadedCallbackSubscribed) return;
        SceneManager.sceneLoaded -= OnValidationSceneLoaded;
        sceneLoadedCallbackSubscribed = false;
    }

    private void DisableRelevantBehaviours(Scene scene)
    {
        CollectBehaviours(scene, delegate(MonoBehaviour component)
        {
            return component.GetType().Name == "IA_BrainMaster"
                || component.GetType().Name == "IA03EstrategaNacional"
                || component.GetType().Name == "PatrulhaCombateSegura"
                || IsCombatComponent(component);
        }, true);
    }

    private int CountDisabledRelevantBehaviours(Scene scene)
    {
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            count += CountDisabledRelevantBehavioursRecursive(roots[i].transform);
        }
        return count;
    }

    private int CountDisabledRelevantBehavioursRecursive(Transform parent)
    {
        int count = 0;
        MonoBehaviour[] components = parent.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && IsRelevantComponent(components[i]) && !components[i].enabled) count++;
        for (int i = 0; i < parent.childCount; i++)
            count += CountDisabledRelevantBehavioursRecursive(parent.GetChild(i));
        return count;
    }

    private static bool IsRelevantComponent(MonoBehaviour component)
    {
        if (component == null) return false;
        string name = component.GetType().Name;
        return name == "IA_BrainMaster"
            || name == "IA03EstrategaNacional"
            || name == "PatrulhaCombateSegura"
            || IsCombatComponent(component);
    }

    private static bool IsCombatComponent(MonoBehaviour component)
    {
        if (component == null) return false;
        string name = component.GetType().Name;
        return name == "ControleTorreta"
            || name == "ControleTorretaModular"
            || name == "SistemaDeTiro"
            || name == "Torreta"
            || name == "TorretaAntiaerea";
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
            BehaviourState state = FindBehaviourState(component);
            if (state == null)
            {
                state = new BehaviourState { behaviour = component, wasEnabled = component.enabled };
                behaviourStates.Add(state);
            }
            if (disableImmediately) component.enabled = false;
        }

        for (int i = 0; i < parent.childCount; i++)
            CollectBehavioursRecursive(parent.GetChild(i), predicate, disableImmediately);
    }

    private void CaptureShooterTurrets(GameObject unit)
    {
        turretStates.Clear();
        MonoBehaviour[] components = unit.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null || component.GetType().Name != "ControleTorreta") continue;
            BehaviourState behaviourState = FindBehaviourState(component);
            turretStates.Add(new TurretState
            {
                behaviour = component,
                instanceId = component.GetInstanceID(),
                range = Convert.ToSingle(GetMember(component, "alcance")),
                wasEnabled = behaviourState != null && behaviourState.wasEnabled,
                priorityTarget = GetMember(component, "alvoPrioritario"),
                currentTarget = GetMember(component, "alvoAtual"),
                passiveMode = GetMember(component, "modoPassivo"),
                missilePrefab = GetMember(component, "misselPrefab"),
                rotatingPart = GetMember(component, "pecaQueGira"),
                barrels = GetMember(component, "locaisDoTiro")
            });
            CaptureMembers(component, turretStates[turretStates.Count - 1].firingValues,
                "balasAtuais", "estaRecarregando", "contadorTempo", "misseisAtuais",
                "estaRecarregandoMisseis", "contadorRecargaMissel", "cooldownMissel",
                "indiceBarrilAtual", "estaProntoParaAtirar", "progressoDesdobramento");
        }
    }

    private string BuildN2TargetFidelitySnapshot(
        GameObject shooter,
        Component controller,
        Component targetIdentity,
        Transform missionTarget,
        Component strategy,
        string phase,
        float elapsed)
    {
        object activeMission = strategy != null ? GetMember(strategy, "missaoAtiva") : null;
        object movementOrder = controller != null ? GetMember(controller, "OrdemMovimentoAtual") : null;
        NavMeshAgent shooterAgent = shooter != null ? shooter.GetComponent<NavMeshAgent>() : null;
        string snapshot = "phase=" + phase
            + " frame=" + Time.frameCount
            + " elapsed=" + elapsed.ToString("F2")
            + " mission=" + GetMember(activeMission, "IdMissao")
            + " order=" + GetMember(strategy, "idOrdemAtivaDaMissao")
            + " shooter=" + (shooter != null ? shooter.name + "#" + shooter.GetInstanceID() : "null")
            + " reserved=" + (shooter != null && Convert.ToBoolean(Invoke(strategy, "IsUnitReservedForActiveMission", shooter)))
            + " controller=" + (controller != null ? controller.GetType().Name + "#" + controller.GetInstanceID() : "null")
            + " controllerCombat=" + GetMember(controller, "ModoCombateAtivo")
            + " controllerMoveOrder=" + GetMember(controller, "PossuiOrdemMovimentoAtiva")
            + " controllerState=" + GetMember(controller, "ordemControleAtual")
            + " movementOwner=" + GetMember(movementOrder, "Dono")
            + " movementOrderId=" + GetMember(movementOrder, "Id")
            + " movementDestination=" + GetMember(movementOrder, "Destino")
            + " movementOrderState=" + GetMember(movementOrder, "Estado")
            + " navStopped=" + (shooterAgent != null && shooterAgent.isStopped)
            + " navDestination=" + (shooterAgent != null ? shooterAgent.destination.ToString("F2") : "none")
            + " missionTarget=" + (missionTarget != null ? missionTarget.name + "#" + missionTarget.GetInstanceID() : "null")
            + " identity=" + (targetIdentity != null ? targetIdentity.name + "#" + targetIdentity.GetInstanceID() : "null")
            + " targetPosition=" + (missionTarget != null ? missionTarget.position.ToString() : "none");

        for (int i = 0; i < turretStates.Count; i++)
        {
            Behaviour turret = turretStates[i].behaviour;
            Transform priority = turret != null ? GetMember(turret, "alvoPrioritario") as Transform : null;
            Transform current = turret != null ? GetMember(turret, "alvoAtual") as Transform : null;
            Transform[] muzzles = turret != null ? GetMember(turret, "locaisDoTiro") as Transform[] : null;
            int barrelIndex = turret != null ? Convert.ToInt32(GetMember(turret, "indiceBarrilAtual") ?? 0) : 0;
            Transform activeMuzzle = muzzles != null && muzzles.Length > 0
                ? muzzles[Mathf.Clamp(barrelIndex, 0, muzzles.Length - 1)]
                : null;
            float barrelAngle = current != null && activeMuzzle != null
                ? Vector3.Angle(activeMuzzle.forward, current.position - activeMuzzle.position)
                : float.NaN;
            object canFire = turret != null ? Invoke(turret, "EstaAlinhadaParaDisparar") : null;
            snapshot += " | turret=" + (turret != null ? turret.name + "#" + turret.GetInstanceID() : "null")
                + " enabled=" + (turret != null && turret.enabled)
                + " passive=" + GetMember(turret, "modoPassivo")
                + " priority=" + (priority != null ? priority.name + "#" + priority.GetInstanceID() : "null")
                + " current=" + (current != null ? current.name + "#" + current.GetInstanceID() : "null")
                + " range=" + turretStates[i].range.ToString("F1")
                + " currentDistance=" + (current != null ? Vector3.Distance(turret.transform.position, current.position).ToString("F1") : "none")
                + " priorityDistance=" + (priority != null ? Vector3.Distance(turret.transform.position, priority.position).ToString("F1") : "none")
                + " barrelIndex=" + barrelIndex
                + " barrelAngle=" + barrelAngle.ToString("F2")
                + " canFireByAim=" + canFire
                + " ready=" + GetMember(turret, "estaProntoParaAtirar")
                + " ammo=" + GetMember(turret, "MunicaoAtual")
                + " reloading=" + GetMember(turret, "estaRecarregando")
                + " cooldown=" + GetMember(turret, "contadorTempo");
        }

        return snapshot;
    }

    private void ResetSelectedTurretFiringStateForCase()
    {
        if (selectedTurretState == null || selectedTurretState.behaviour == null) return;

        RestoreMembers(selectedTurretState.behaviour, selectedTurretState.firingValues);
        MethodInfo clearTarget = selectedTurretState.behaviour.GetType().GetMethod("DefinirAlvo", InstanceMembers);
        if (clearTarget != null) clearTarget.Invoke(selectedTurretState.behaviour, new object[] { null });
    }

    private void EnableSelectedShooterTurret()
    {
        if (selectedTurretState == null || selectedTurretState.behaviour == null) return;
        selectedTurretState.behaviour.enabled = selectedTurretState.wasEnabled;
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
            RestoreMembers(state.behaviour, state.firingValues);
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

    private BehaviourState FindBehaviourState(Behaviour behaviour)
    {
        for (int i = 0; i < behaviourStates.Count; i++)
            if (behaviourStates[i].behaviour == behaviour) return behaviourStates[i];
        return null;
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

    private bool TrySelectShooterAndTarget(
        Scene scene,
        out GameObject selectedShooter,
        out Component selectedController,
        out NavMeshAgent selectedAgent,
        out Component chosenTurret,
        out Transform selectedTarget,
        out string summary,
        out bool querySaturated)
    {
        selectedShooter = null;
        selectedController = null;
        selectedAgent = null;
        chosenTurret = null;
        selectedTarget = null;
        summary = string.Empty;
        querySaturated = false;

        Type identityType = ResolveType("IdentidadeUnidade");
        Type damageType = ResolveType("SistemaDeDanos");
        Type submarineType = ResolveType("ControleSubmarino");
        if (identityType == null || damageType == null || submarineType == null)
        {
            summary = "Dependência ausente: IdentidadeUnidade, SistemaDeDanos ou ControleSubmarino.";
            return false;
        }

        MethodInfo resolveIdentity = damageType.GetMethod("ResolverIdentidade", StaticMembers,
            null, new[] { typeof(Component) }, null);
        MethodInfo canBeConventionalTarget = submarineType.GetMethod("PodeSerAlvoConvencional", StaticMembers,
            null, new[] { typeof(Transform) }, null);
        if (resolveIdentity == null || canBeConventionalTarget == null)
        {
            summary = "Não foi possível vincular aos resolvers reais de identidade/alvo convencional.";
            return false;
        }

        string[] shooterPaths =
        {
            "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_02",
            "IA03_TestScenario/PaisA_Valdoria_Team1/Unidades_Reais/Valdoria_Tank_01"
        };
        string[] enemyTankPaths =
        {
            "IA03_TestScenario/PaisB_Karsovia_Team3/Unidades_Reais/Karsovia_Tank_01",
            "IA03_TestScenario/PaisB_Karsovia_Team3/Unidades_Reais/Karsovia_Tank_02"
        };

        Dictionary<int, GameObject> enemyTankRoots = new Dictionary<int, GameObject>();
        Dictionary<int, Component> enemyIdentities = new Dictionary<int, Component>();
        for (int i = 0; i < enemyTankPaths.Length; i++)
        {
            GameObject enemyRoot = FindByPath(scene, enemyTankPaths[i]);
            Component enemyIdentity = enemyRoot != null ? FindComponentRecursive(enemyRoot.transform, identityType.Name) : null;
            if (enemyRoot == null || enemyIdentity == null) continue;
            object teamValue = GetMember(enemyIdentity, "teamID");
            object typeValue = GetMember(enemyIdentity, "tipoUnidade");
            if (!enemyRoot.activeInHierarchy || teamValue == null || typeValue == null) continue;
            if (Convert.ToInt32(teamValue) != TeamB || !string.Equals(typeValue.ToString(), "Veiculo", StringComparison.Ordinal)) continue;
            int identityId = enemyIdentity.GetInstanceID();
            enemyTankRoots[identityId] = enemyRoot;
            enemyIdentities[identityId] = enemyIdentity;
        }
        if (enemyIdentities.Count == 0)
        {
            summary = "Nenhum tanque Karsovia ativo com Team 3 e classificação Veiculo foi encontrado.";
            return false;
        }

        MethodInfo closestPoint = null;
        Collider[] overlapBuffer = new Collider[40];
        Physics.SyncTransforms();
        for (int s = 0; s < shooterPaths.Length; s++)
        {
            GameObject candidateShooter = FindByPath(scene, shooterPaths[s]);
            if (candidateShooter == null || !candidateShooter.activeInHierarchy)
            {
                summary += "[shooter ausente/inativo: " + shooterPaths[s] + "] ";
                continue;
            }

            Component candidateIdentity = FindComponentRecursive(candidateShooter.transform, identityType.Name);
            object shooterTeamValue = GetMember(candidateIdentity, "teamID");
            if (candidateIdentity == null || shooterTeamValue == null || Convert.ToInt32(shooterTeamValue) != TeamA)
            {
                summary += "[identidade Team 1 inválida: " + candidateShooter.name + "] ";
                continue;
            }

            Component candidateController = FindComponentRecursive(candidateShooter.transform, "ControleUnidade");
            NavMeshAgent candidateAgent = candidateShooter.GetComponent<NavMeshAgent>();
            if (candidateAgent == null) candidateAgent = candidateShooter.GetComponentInChildren<NavMeshAgent>(true);
            if (candidateController == null || candidateAgent == null || !candidateAgent.enabled || !candidateAgent.isOnNavMesh)
            {
                summary += "[ControleUnidade/NavMesh indisponível: " + candidateShooter.name + "] ";
                continue;
            }

            MonoBehaviour[] turretCandidates = candidateShooter.GetComponentsInChildren<MonoBehaviour>(true);
            Array.Sort(turretCandidates, delegate(MonoBehaviour left, MonoBehaviour right)
            {
                int leftId = left != null ? left.GetInstanceID() : int.MaxValue;
                int rightId = right != null ? right.GetInstanceID() : int.MaxValue;
                return leftId.CompareTo(rightId);
            });

            for (int t = 0; t < turretCandidates.Length; t++)
            {
                MonoBehaviour turret = turretCandidates[t];
                if (turret == null || turret.GetType().Name != "ControleTorreta") continue;
                BehaviourState originalTurretState = FindBehaviourState(turret);
            if (originalTurretState == null)
            {
                summary += "[sem snapshot da torreta: shooter=" + candidateShooter.name
                    + " turret=" + turret.name + " enabled=" + turret.enabled + "] ";
                continue;
            }
            if (!originalTurretState.wasEnabled)
            {
                summary += "[torreta já estava desabilitada ao isolar: shooter=" + candidateShooter.name
                    + " turret=" + turret.name + " id=" + turret.GetInstanceID()
                    + " enabled=" + turret.enabled + "] ";
                continue;
            }

                MethodInfo classifyAntiAir = turret.GetType().GetMethod("DeterminarSouAntiAereo", InstanceMembers,
                    null, Type.EmptyTypes, null);
                MethodInfo resolveTarget = turret.GetType().GetMethod("ResolverTransformAlvo", InstanceMembers,
                    null, new[] { typeof(Transform) }, null);
                closestPoint = turret.GetType().GetMethod("ObterPontoMaisProximoSeguro", StaticMembers,
                    null, new[] { typeof(Collider), typeof(Vector3) }, null);
                if (classifyAntiAir == null || resolveTarget == null || closestPoint == null)
                {
                    summary += "[resolver real da torreta ausente: " + turret.name + "] ";
                    continue;
                }
            if ((bool)classifyAntiAir.Invoke(turret, null))
            {
                summary += "[torreta classificada AA: shooter=" + candidateShooter.name
                    + " turret=" + turret.name + "] ";
                continue;
            }

                object rangeValue = GetMember(turret, "alcance");
            if (rangeValue == null || Convert.ToSingle(rangeValue) <= 0f)
            {
                summary += "[alcance terrestre inválido: shooter=" + candidateShooter.name
                    + " turret=" + turret.name + " range=" + rangeValue + "] ";
                continue;
            }
                float range = Convert.ToSingle(rangeValue);
                int overlapCount = Physics.OverlapSphereNonAlloc(turret.transform.position, range, overlapBuffer,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (overlapCount >= overlapBuffer.Length)
                {
                    querySaturated = true;
                    summary = "Buffer de 40 colisores saturado na primeira torreta elegível. shooter="
                        + candidateShooter.name + " turret=" + turret.name + " id=" + turret.GetInstanceID()
                        + " range=" + range + " count=" + overlapCount + "; seleção inconclusiva.";
                    return false;
                }

                Dictionary<int, TargetCandidate> candidatesByIdentity = new Dictionary<int, TargetCandidate>();
                for (int c = 0; c < overlapCount; c++)
                {
                    Collider hit = overlapBuffer[c];
                    if (hit == null || hit.isTrigger) continue;
                    if (!(bool)canBeConventionalTarget.Invoke(null, new object[] { hit.transform })) continue;

                    Component identity = resolveIdentity.Invoke(null, new object[] { hit }) as Component;
                    if (identity == null) continue;
                    int identityId = identity.GetInstanceID();
                    if (!enemyIdentities.ContainsKey(identityId)) continue;
                    if (identity == candidateIdentity) continue;

                    object targetTeamValue = GetMember(identity, "teamID");
                    object targetTypeValue = GetMember(identity, "tipoUnidade");
                    if (targetTeamValue == null || Convert.ToInt32(targetTeamValue) != TeamB
                        || targetTypeValue == null || !string.Equals(targetTypeValue.ToString(), "Veiculo", StringComparison.Ordinal)) continue;

                    Transform resolvedTarget = resolveTarget.Invoke(turret, new object[] { hit.transform }) as Transform;
                    if (resolvedTarget == null || !resolvedTarget.gameObject.activeInHierarchy || resolvedTarget.position.y > 6f) continue;

                    GameObject enemyRoot = enemyTankRoots[identityId];
                    NavMeshAgent targetAgent = resolvedTarget.GetComponentInParent<NavMeshAgent>();
                    if (targetAgent == null && enemyRoot != null) targetAgent = enemyRoot.GetComponentInChildren<NavMeshAgent>(true);
                    if (targetAgent == null || !targetAgent.enabled || !targetAgent.isOnNavMesh) continue;
                    if (!PathIsComplete(candidateAgent.nextPosition, resolvedTarget.position, candidateAgent.areaMask)) continue;

                    Vector3 closest = (Vector3)closestPoint.Invoke(null, new object[] { hit, turret.transform.position });
                    float distanceSqr = (closest - turret.transform.position).sqrMagnitude;
                    if (distanceSqr > range * range) continue;

                    TargetCandidate current;
                    if (!candidatesByIdentity.TryGetValue(identityId, out current)
                        || distanceSqr < current.distanceSqr)
                    {
                        candidatesByIdentity[identityId] = new TargetCandidate
                        {
                            target = resolvedTarget,
                            identity = identity,
                            distanceSqr = distanceSqr
                        };
                    }
                }

                TargetCandidate best = null;
                int bestIdentityId = int.MaxValue;
                foreach (KeyValuePair<int, TargetCandidate> candidate in candidatesByIdentity)
                {
                    if (best == null || candidate.Value.distanceSqr < best.distanceSqr
                        || (Mathf.Approximately(candidate.Value.distanceSqr, best.distanceSqr)
                            && candidate.Key < bestIdentityId))
                    {
                        best = candidate.Value;
                        bestIdentityId = candidate.Key;
                    }
                }

                if (best == null)
                {
                    summary += "[sem tanque inimigo com collider no alcance/caminho: shooter="
                        + candidateShooter.name + " turret=" + turret.name + "] ";
                    continue;
                }

                selectedShooter = candidateShooter;
                selectedController = candidateController;
                selectedAgent = candidateAgent;
                chosenTurret = turret;
                selectedTarget = best.target;
                summary = "seleção por OverlapSphereNonAlloc real; shooter=" + candidateShooter.name
                    + " turret=" + turret.name + " turretId=" + turret.GetInstanceID()
                    + " origin=" + turret.transform.position + " range=" + range
                    + " target=" + best.identity.name + " resolved=" + best.target.name
                    + " closestPointDistance=" + Mathf.Sqrt(best.distanceSqr).ToString("F2")
                    + " colliderCount=" + overlapCount + " path=complete"
                    + " Karsovia_Tank_02 foi apenas candidato físico entre os tanques ativos.";
                return true;
            }
        }

        if (string.IsNullOrEmpty(summary)) summary = "Nenhuma torreta terrestre previamente habilitada foi elegível.";
        return false;
    }

    private TurretState FindTurretState(Component component)
    {
        for (int i = 0; i < turretStates.Count; i++)
            if (turretStates[i].behaviour == component) return turretStates[i];
        return null;
    }

    private Transform[] ReadTargets(string fieldName)
    {
        Transform[] targets = new Transform[turretStates.Count];
        for (int i = 0; i < turretStates.Count; i++)
            targets[i] = GetMember(turretStates[i].behaviour, fieldName) as Transform;
        return targets;
    }

    private static bool AreAllTargetsNull(Transform[] targets)
    {
        if (targets == null) return true;
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] != null) return false;
        return true;
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

    private string FindCaseEvent(string fragment)
    {
        for (int i = 0; i < currentCaseEvents.Count; i++)
            if (currentCaseEvents[i].IndexOf(fragment, StringComparison.Ordinal) >= 0)
                return currentCaseEvents[i];
        return null;
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

    private static Component FindBrainMasterForTeam(Scene scene, int teamId)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Component[] brains = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int b = 0; b < brains.Length; b++)
            {
                if (brains[b] != null
                    && brains[b].GetType().Name == "IA_BrainMaster"
                    && Convert.ToInt32(GetMember(brains[b], "TeamId")) == teamId)
                {
                    return brains[b];
                }
            }
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

    private static string DescribeTeamIdentities(Scene scene, int teamId)
    {
        Type identityType = ResolveType("IdentidadeUnidade");
        if (identityType == null || !scene.IsValid() || !scene.isLoaded) return "unavailable";

        int total = 0;
        int active = 0;
        int enabled = 0;
        List<string> samples = new List<string>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int b = 0; b < behaviours.Length; b++)
            {
                MonoBehaviour behaviour = behaviours[b];
                if (behaviour == null || !identityType.IsInstanceOfType(behaviour)
                    || Convert.ToInt32(GetMember(behaviour, "teamID")) != teamId) continue;

                total++;
                if (behaviour.gameObject.activeInHierarchy) active++;
                if (behaviour.enabled) enabled++;
                if (samples.Count < 6)
                {
                    samples.Add(behaviour.name + "#" + behaviour.GetInstanceID()
                        + " active=" + behaviour.gameObject.activeInHierarchy
                        + " enabled=" + behaviour.enabled
                        + " tipo=" + GetMember(behaviour, "tipoUnidade"));
                }
            }
        }

        return "team=" + teamId + " total=" + total + " active=" + active
            + " enabled=" + enabled + " samples=[" + string.Join(" | ", samples) + "]";
    }

    private static string DescribeWorldRegistry()
    {
        Type worldStateType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
        FieldInfo registryField = worldStateType?.GetField("_globalRegistry", StaticMembers);
        object registry = registryField?.GetValue(null);
        PropertyInfo countProperty = registry?.GetType().GetProperty("Count", InstanceMembers);
        object count = countProperty != null ? countProperty.GetValue(registry) : "unavailable";
        FieldInfo versionField = worldStateType?.GetField("_registryVersion", StaticMembers);
        object version = versionField != null ? versionField.GetValue(null) : "unavailable";
        return "count=" + count + " version=" + version;
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

    private static int CalcularAlistaveisParaHeadroomDeReserva(
        object country,
        float reservePercentage,
        long requiredHeadroom)
    {
        long population = Math.Max(0L, Convert.ToInt64(GetMember(country, "populacao") ?? 0));
        long civilians = Math.Max(0L, Convert.ToInt64(GetMember(country, "populacaoCivil") ?? 0));
        long activeMilitary = Math.Max(0L, Convert.ToInt64(GetMember(country, "populacaoMilitarAtiva") ?? 0));
        long reservists = Math.Max(0L, Convert.ToInt64(GetMember(country, "reservistas") ?? 0));
        long eligible = Math.Max(0L, Convert.ToInt64(GetMember(country, "alistaveis") ?? 0));
        decimal percentage = Convert.ToDecimal(Mathf.Clamp(reservePercentage, 0f, 0.5f));

        for (int attempt = 0; attempt < 64; attempt++)
        {
            long populationComponents = civilians + activeMilitary + reservists + eligible;
            long totalPopulation = Math.Max(population, populationComponents);
            long reserve = (long)decimal.Ceiling(totalPopulation * percentage);
            long available = civilians + eligible;
            if (available >= reserve + requiredHeadroom)
            {
                if (eligible > int.MaxValue)
                    throw new InvalidOperationException("A fixture excedeu o limite de alistáveis suportado pelo país.");
                return (int)eligible;
            }

            eligible += Math.Max(1L, reserve + requiredHeadroom - available);
        }

        throw new InvalidOperationException(
            "Não foi possível preparar manpower acima da reserva estratégica para o teste N2.");
    }

    private static void InvokeStaticIfPresent(string typeName, string methodName)
    {
        Type type = ResolveType(typeName);
        MethodInfo method = type != null ? type.GetMethod(methodName, StaticMembers) : null;
        method?.Invoke(null, null);
    }
}
