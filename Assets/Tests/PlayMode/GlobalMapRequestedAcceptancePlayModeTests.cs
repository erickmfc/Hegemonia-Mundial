#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class GlobalMapRequestedAcceptancePlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Scenes/GlobalMapRTS.unity";
    private string previousActiveScenePath;
    private Camera cameraToRestoreAfterMovementTest;
    private Vector3 originalCameraPositionBeforeMovementTest;
    private Component streamerToRestoreAfterMovementTest;
    private Camera originalStreamerCameraOverride;
    private GameObject temporaryStreamingCameraObject;
    private int originalTilesCreatedPerFrame;
    private int originalTerrainRowsPerFrame;
    private int originalVegetationRowsPerFrame;
    private float originalRefreshInterval;
    private bool originalStreamerEnabled;

    [UnitySetUp]
    public IEnumerator LoadRealGlobalMap()
    {
        previousActiveScenePath = SceneManager.GetActiveScene().path;
        if (!EditorApplication.isPlaying)
            yield return new EnterPlayMode();

        Scene map = SceneManager.GetSceneByPath(ScenePath);
        if (!map.IsValid() || !map.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "GlobalMapRTS deve estar incluído nas cenas de build para validação Play Mode.");
            while (load != null && !load.isDone)
                yield return null;
            map = SceneManager.GetSceneByPath(ScenePath);
        }

        Assert.That(map.IsValid() && map.isLoaded, Is.True, "GlobalMapRTS precisa estar carregado em Play Mode.");
        SceneManager.SetActiveScene(map);
    }

    [UnityTearDown]
    public IEnumerator RestorePreviousScene()
    {
        if (cameraToRestoreAfterMovementTest != null)
            cameraToRestoreAfterMovementTest.transform.position = originalCameraPositionBeforeMovementTest;
        cameraToRestoreAfterMovementTest = null;
        if (streamerToRestoreAfterMovementTest != null)
        {
            Behaviour streamerBehaviour = streamerToRestoreAfterMovementTest as Behaviour;
            if (streamerBehaviour != null && streamerBehaviour.enabled)
                streamerBehaviour.enabled = false;
            Write(streamerToRestoreAfterMovementTest, "cameraOverride", originalStreamerCameraOverride);
            Write(streamerToRestoreAfterMovementTest, "tilesCreatedPerFrame", originalTilesCreatedPerFrame);
            Write(streamerToRestoreAfterMovementTest, "terrainRowsPerFrame", originalTerrainRowsPerFrame);
            Write(streamerToRestoreAfterMovementTest, "vegetationRowsPerFrame", originalVegetationRowsPerFrame);
            Write(streamerToRestoreAfterMovementTest, "refreshInterval", originalRefreshInterval);
            if (streamerBehaviour != null)
                streamerBehaviour.enabled = originalStreamerEnabled;
        }
        streamerToRestoreAfterMovementTest = null;
        originalStreamerCameraOverride = null;
        if (temporaryStreamingCameraObject != null)
        {
            UnityEngine.Object.Destroy(temporaryStreamingCameraObject);
            temporaryStreamingCameraObject = null;
            yield return null;
        }

        Type menuType = ResolveType("MenuGovernoNovoController");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        if (menu != null)
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });

        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(previousActiveScenePath)
            || string.Equals(previousActiveScenePath, ScenePath, StringComparison.OrdinalIgnoreCase))
            yield break;

        Scene previous = SceneManager.GetSceneByPath(previousActiveScenePath);
        if (!previous.IsValid() || !previous.isLoaded)
        {
            AsyncOperation restore = EditorSceneManager.LoadSceneAsyncInPlayMode(
                previousActiveScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            if (restore != null)
                while (!restore.isDone)
                    yield return null;
            previous = SceneManager.GetSceneByPath(previousActiveScenePath);
        }
        if (previous.IsValid() && previous.isLoaded)
            SceneManager.SetActiveScene(previous);
    }

    [UnityTest]
    public IEnumerator GlobalMapShowsKnownEnemyContactsInPeaceAndKeepsFogOfWar()
    {
        Type mapType = ResolveType("MapaGeralController");
        MethodInfo policy = mapType.GetMethod("DeveExibirEntidadeNoMapa", Members);
        Assert.That(policy, Is.Not.Null, "A política de exibição de contatos do mapa não foi encontrada.");

        bool detectedEnemyInPeace = (bool)policy.Invoke(null, new object[] { false, false, true, false });
        bool rememberedEnemyInPeace = (bool)policy.Invoke(null, new object[] { false, false, false, true });
        bool unknownEnemy = (bool)policy.Invoke(null, new object[] { false, false, false, false });
        bool allyWithoutContact = (bool)policy.Invoke(null, new object[] { true, false, false, false });

        Assert.That(detectedEnemyInPeace, Is.True,
            "Um contato hostil já detectado precisa permanecer visível mesmo sem guerra declarada.");
        Assert.That(rememberedEnemyInPeace, Is.True,
            "A última posição conhecida de um contato hostil precisa permanecer disponível na neblina.");
        Assert.That(unknownEnemy, Is.False,
            "A mudança não pode revelar unidades hostis sem visão nem posição conhecida.");
        Assert.That(allyWithoutContact, Is.True,
            "As unidades aliadas precisam continuar visíveis sem depender do serviço de contatos.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator NewCampaignUsesGlobalMapAndKeepsExistingModeScenesAndLegacySaves()
    {
        Type configType = ResolveType("ConfiguracaoCenasJogo");
        Assert.That(CallStatic(configType, "ResolverCenaCampanhaPadrao"), Is.EqualTo("GlobalMapRTS"),
            "Campanhas novas devem iniciar no mapa global.");
        Assert.That(CallStatic(configType, "ResolverCenaEscaramuca"), Is.EqualTo("GlobalMapRTS"),
            "Escaramuças novas também usam o mapa global.");
        Assert.That(CallStatic(configType, "ResolverCenaEscaramucaLegada"), Is.EqualTo("Md Historia"),
            "A cena original da escaramuça permanece disponível pela rota legada.");
        Assert.That(CallStatic(configType, "ResolverCenaTutorial"), Is.EqualTo("Tutorial"),
            "O tutorial mantém sua cena dedicada.");
        Assert.That(CallStatic(configType, "NormalizarCenaCampanha", "cena19)", "GlobalMapRTS"), Is.EqualTo("cena19)"),
            "Saves que apontam para a campanha recuperada devem continuar abrindo aquela cena.");
        Assert.That(CallStatic(configType, "NormalizarCenaCampanha", "Md Historia", "GlobalMapRTS"), Is.EqualTo("Md Historia"),
            "Saves existentes da escaramuça devem continuar abrindo a cena original.");
        Assert.That(CallStatic(configType, "EhCenaCampanhaRecuperada", "cena19)", "Assets/_Recovery/cena19).unity"), Is.True);
        Assert.That(CallStatic(configType, "EhCenaCampanhaRecuperada", "GlobalMapRTS", ScenePath), Is.False,
            "A exceção de inicialização da cena recuperada não pode bloquear o GlobalMapRTS.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator GlobalMapKeepsValdoriaIdentityAndAllManualConstructionAreasResolve()
    {
        Component valdoria = FindBrain(2);
        Component karsovia = FindBrain(3);
        Assert.That(valdoria, Is.Not.Null, "A Valdoria/Team 2 IA_BrainMaster deve existir na cena real.");
        Assert.That(valdoria.gameObject.activeInHierarchy && ((Behaviour)valdoria).isActiveAndEnabled, Is.True);
        Assert.That(karsovia, Is.Not.Null, "Karsovia/Team 3 deve continuar configurada, mas inativa.");
        Assert.That(karsovia.gameObject.activeInHierarchy, Is.False);
        Assert.That((bool)Read(karsovia, "UseScriptedBootstrap"), Is.False);
        Assert.That(Read(valdoria, "IntegrationMode").ToString(), Is.EqualTo("Hybrid"));
        Assert.That((bool)Read(valdoria, "UseScriptedBootstrap"), Is.True);
        Assert.That((bool)Read(valdoria, "UseManualBuildPoints"), Is.True);

        for (int frame = 0; frame < 240 && Read(valdoria, "Context") == null; frame++)
            yield return null;
        Assert.That(Read(valdoria, "Context"), Is.Not.Null, "O grafo da IA real precisa iniciar no GlobalMapRTS.");
        Assert.That((bool)Read(valdoria, "HasExecutionAuthority"), Is.True,
            "O BrainMaster da Valdoria precisa ser o controlador autorizado a emitir ordens no GlobalMapRTS.");
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Component strategist = valdoria.GetComponent(strategistType);
        Assert.That(strategist, Is.Not.Null, "A IA estratégica nacional da Valdoria precisa estar no mapa real.");
        Assert.That(((Behaviour)strategist).isActiveAndEnabled, Is.True);
        Assert.That((bool)Read(strategist, "DebugHabilitado"), Is.False,
            "A campanha não deve ativar logs experimentais da IA03.");
        Assert.That((bool)Read(strategist, "TemPerfilConfigurado"), Is.True,
            "O perfil estratégico nacional da Valdoria precisa estar ativo.");
        Component karsoviaStrategist = karsovia.GetComponent(strategistType);
        Assert.That(karsoviaStrategist, Is.Not.Null, "A IA03 da Karsovia deve continuar configurada no mapa.");
        Assert.That((bool)Read(karsoviaStrategist, "DebugHabilitado"), Is.False,
            "A configuração da Karsovia não deve deixar logs experimentais prontos para uma partida real.");
        for (int frame = 0; frame < 240 && Convert.ToSingle(Read(strategist, "UltimaAnaliseEm")) < 0f; frame++)
            yield return null;
        Assert.That(Convert.ToSingle(Read(strategist, "UltimaAnaliseEm")), Is.GreaterThanOrEqualTo(0f),
            "A IA03 precisa executar ao menos um ciclo real de análise estratégica no GlobalMapRTS.");
        Assert.That(string.IsNullOrWhiteSpace(Convert.ToString(Read(valdoria, "BootstrapLastError"))), Is.True,
            "O BrainMaster da Valdoria não deve reportar erro de bootstrap no cenário real.");
        TestContext.WriteLine("IA03 Team 2 runtime: perfil=" + Read(strategist, "TemPerfilConfigurado")
            + "; analiseEm=" + Read(strategist, "UltimaAnaliseEm")
            + "; estado=" + Read(strategist, "EstadoNacional")
            + "; decisao=" + Read(strategist, "UltimaDecisao")
            + "; bootstrap=" + Read(valdoria, "BootstrapStage")
            + "; bootstrapStatus=" + Read(valdoria, "BootstrapStatus")
            + "; erro=" + (Read(valdoria, "BootstrapLastError") ?? ""));
        UnityEngine.AI.NavMeshAgent[] invalidNavMeshAgents = UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None)
            .Where(agent => agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh)
            .ToArray();
        string invalidAgentSummary = string.Join(", ", invalidNavMeshAgents.Select(agent =>
        {
            MonoBehaviour identity = agent.GetComponentsInParent<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().Name == "IdentidadeUnidade");
            string teamId = identity != null ? Convert.ToString(Read(identity, "teamID")) : "?";
            Component control = agent.GetComponentInParent(ResolveType("ControleUnidade"));
            string executor = control != null ? Convert.ToString(Read(control, "ExecutorAtual")) : "sem ControleUnidade";
            return agent.name + "(Team " + teamId + ", executor=" + executor + ")";
        }));
        TestContext.WriteLine("NavMesh diagnóstico GlobalMapRTS: inválidos=" + invalidNavMeshAgents.Length
            + (invalidNavMeshAgents.Length > 0 ? " [" + invalidAgentSummary + "]" : string.Empty));
        for (int frame = 0; frame < 10; frame++)
            yield return null;

        Type governmentType = ResolveType("SistemaGovernoMundial");
        object government = ReadStatic(governmentType, "Instancia");
        Assert.That(government, Is.Not.Null);
        Assert.That((int)Read(government, "teamJogador"), Is.EqualTo(1));
        object valdoriaCountry = Call(government, "ObterPais", 2);
        Assert.That(Read(valdoriaCountry, "nomePais"), Is.EqualTo("Valdoria"));
        Assert.That(Read(valdoriaCountry, "nomeMoeda"), Is.EqualTo("Valdoria"));
        Assert.That(Read(valdoriaCountry, "simboloMoeda"), Is.EqualTo("VA$"));

        Type markerType = ResolveType("Hegemonia.AI.BrainMaster.IA_ManualBuildPoint");
        Component[] areas = valdoria.GetComponentsInChildren(markerType, true)
            .Where(point => point != null && (bool)Read(point, "IsConstructionAreaRole"))
            .ToArray();
        string[] roles = { "ConstrucaoCidade", "ConstrucaoEnergia", "ConstrucaoPier", "ConstrucaoEstaleiro", "ConstrucaoPlataforma", "ConstrucaoAeroportoMilitar" };
        string[] itemKeys = { "prefeitura", "usina", "pier", "estaleiro", "PLataforma", "aeroporto militar" };
        string[] zones = { "Core", "Economy", "Naval", "Naval", "Naval", "Air" };
        Assert.That(areas.Length, Is.EqualTo(6), "Valdoria deve manter as cinco áreas anteriores e o Creaty manual do aeroporto militar.");

        object director = valdoria.GetType().GetField("_buildDirector", Members).GetValue(valdoria);
        Assert.That(director, Is.Not.Null);
        Type territoryType = ResolveType("GerenteDeTerritorio");
        object territory = ReadStatic(territoryType, "Instancia");
        Assert.That(territory, Is.Not.Null);
        object politicalMap = Read(territory, "MapaPolitico");
        Assert.That(politicalMap, Is.Not.Null);
        Assert.That(((UnityEngine.Object)politicalMap).name, Is.EqualTo("MapaTerritorialGlobalRTS"),
            "A enclave de Valdoria deve valer somente para o cenário GlobalMapRTS.");
        Assert.That((int)Call(territory, "ObterDonoDoPonto", valdoria.transform.position), Is.EqualTo(2),
            "A posição preservada da IA precisa pertencer à Valdoria, sem transferir o restante do Team 4.");
        Assert.That((int)Call(territory, "ObterDonoDoPonto", valdoria.transform.position + new Vector3(5000f, 0f, 0f)), Is.EqualTo(4),
            "A correção local não pode transferir a região maior do Team 4.");
        MethodInfo resolve = director.GetType().GetMethod("TryResolveManualBuildCandidate", Members);
        Assert.That(resolve, Is.Not.Null);
        MethodInfo resolveWithFallback = director.GetType().GetMethods(Members)
            .FirstOrDefault(method => method.Name == "TryFindValidatedCandidate"
                && method.GetParameters().Length == 8);
        Assert.That(resolveWithFallback, Is.Not.Null);
        MethodInfo hasManualOverride = director.GetType().GetMethod("HasManualBuildOverrideForItem", Members);
        Assert.That(hasManualOverride, Is.Not.Null);
        Type zoneType = ResolveType("Hegemonia.AI.BrainMaster.IA_ZoneType");
        Type terrainType = ResolveType("Hegemonia.AI.BrainMaster.IA_TerrainType");
        Type statusType = resolve.ReturnType;
        FieldInfo poseResolved = director.GetType().GetField("_pendingManualNavalPoseResolved", Members);

        for (int i = 0; i < roles.Length; i++)
        {
            Component area = areas.FirstOrDefault(point => Read(point, "ManualRole").ToString() == roles[i]);
            Assert.That(area, Is.Not.Null, "Área ausente: " + roles[i]);
            Assert.That((bool)Read(area, "UsesPlacementArea"), Is.True, roles[i]);
            Assert.That((bool)Call(area, "TargetsItem", valdoria, itemKeys[i]), Is.True,
                roles[i] + " deve selecionar manualmente " + itemKeys[i] + ".");
            if (i < 2)
                Assert.That(Vector3.Distance(area.transform.position, valdoria.transform.position), Is.LessThan(700f),
                    roles[i] + " precisa ficar junto da posição atual da Valdoria.");
            else
            {
                Assert.That((int)Call(territory, "ObterDonoDoPonto", area.transform.position), Is.EqualTo(2),
                    roles[i] + " deve estar dentro da área costeira de Valdoria/Team 2.");
                Assert.That((bool)hasManualOverride.Invoke(director, new object[] { itemKeys[i] }), Is.True,
                    itemKeys[i] + " deve bloquear a busca automática enquanto tiver ponto manual.");
            }
            float radius = Convert.ToSingle(Read(area, "PlacementAreaRadius"));
            Assert.That(radius, Is.GreaterThan(0f), roles[i]);

            object[] args = { itemKeys[i], Enum.Parse(zoneType, zones[i]), valdoria.transform.position, Vector3.zero, null, null };
            object status = resolve.Invoke(director, args);
            Assert.That(status.ToString(), Is.EqualTo("Found"),
                itemKeys[i] + " deve resolver ponto valido dentro da area. Motivo: " + args[5]
                + " | bootstrap=" + Read(valdoria, "BootstrapLastError")
                + (i == 4 || i == 5 ? " | marcador=" + area.transform.position.ToString("F1") : string.Empty));
            Vector3 candidate = (Vector3)args[3];
            Component selectedArea = args[4] as Component;
            Assert.That(selectedArea, Is.SameAs(area), "A construção não pode escapar da área marcada.");
            Assert.That(Vector3.Distance(new Vector3(candidate.x, 0f, candidate.z),
                new Vector3(area.transform.position.x, 0f, area.transform.position.z)),
                Is.LessThanOrEqualTo(radius + 0.1f));
            Assert.That((int)Call(territory, "ObterDonoDoPonto", candidate), Is.EqualTo(2),
                "A construção precisa permanecer dentro do território da Valdoria/Team 2.");
            if (i == 2 || i == 3)
                Assert.That((bool)poseResolved.GetValue(director), Is.True,
                    itemKeys[i] + " precisa resolver pose costeira/naval antes de entrar na fila.");
            else if (i == 4)
                Assert.That((bool)poseResolved.GetValue(director), Is.False,
                    "A plataforma deve continuar no ponto manual offshore; não usa reposicionamento automático de píer/estaleiro.");
            else if (i == 5)
            {
                Type navalResolverType = ResolveType("NavalPlacementResolver");
                MethodInfo isWater = navalResolverType.GetMethods(Members)
                    .First(method => method.Name == "IsWaterAtPosition" && method.GetParameters().Length == 1);
                Assert.That((bool)isWater.Invoke(null, new object[] { candidate }), Is.False,
                    "O aeroporto militar da IA03 deve resolver somente sobre terra firme.");
            }

            if (i >= 2)
            {
                Vector3 previousAreaPosition = area.transform.position;
                try
                {
                    area.transform.position = valdoria.transform.position + new Vector3(5000f, 0f, 0f);
                    Assert.That((int)Call(territory, "ObterDonoDoPonto", area.transform.position), Is.EqualTo(4));
                    object[] fallbackArgs = {
                        itemKeys[i], Enum.Parse(zoneType, zones[i]), valdoria.transform.position,
                        Enum.Parse(terrainType, i == 5 ? "Land" : "Water"), 0f, 1200f, Vector3.zero, string.Empty
                    };
                    bool automaticFallback = (bool)resolveWithFallback.Invoke(director, fallbackArgs);
                    Assert.That(automaticFallback, Is.False,
                        itemKeys[i] + " não deve procurar outro local se a área manual estiver fora do território.");
                    Assert.That(Convert.ToString(fallbackArgs[7]), Does.Contain("nenhum local válido dentro da área manual"),
                        itemKeys[i] + " deve permanecer bloqueado pela área manual, sem fallback automático.");
                }
                finally
                {
                    area.transform.position = previousAreaPosition;
                }
            }
        }

        float prefeituraDeadline = Time.realtimeSinceStartup + 15f;
        string bootstrapStatus = Convert.ToString(Read(valdoria, "BootstrapStatus"));
        while (Time.realtimeSinceStartup < prefeituraDeadline
            && bootstrapStatus.IndexOf("prefeitura enfileirado", StringComparison.OrdinalIgnoreCase) < 0
            && bootstrapStatus.IndexOf("prefeitura pronta", StringComparison.OrdinalIgnoreCase) < 0
            && string.IsNullOrWhiteSpace(Convert.ToString(Read(valdoria, "BootstrapLastError"))))
        {
            yield return null;
            bootstrapStatus = Convert.ToString(Read(valdoria, "BootstrapStatus"));
        }

        TestContext.WriteLine("Bootstrap de construção Team 2: etapa=" + Read(valdoria, "BootstrapStage")
            + "; status=" + bootstrapStatus
            + "; erro=" + (Read(valdoria, "BootstrapLastError") ?? ""));
        bool prefeituraEnfileiradaOuPronta = bootstrapStatus.IndexOf("prefeitura enfileirado", StringComparison.OrdinalIgnoreCase) >= 0
            || bootstrapStatus.IndexOf("prefeitura pronta", StringComparison.OrdinalIgnoreCase) >= 0;
        Assert.That(prefeituraEnfileiradaOuPronta, Is.True,
            "A IA03 deve encontrar sua área manual e iniciar a construção da prefeitura no GlobalMapRTS. Status: "
            + bootstrapStatus + " | erro=" + Read(valdoria, "BootstrapLastError"));
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator ValdoriaBuildsManualShipyardAndPierThroughTheRuntimeQueue()
    {
        Component valdoria = FindBrain(2);
        Assert.That(valdoria, Is.Not.Null);
        for (int frame = 0; frame < 240 && Read(valdoria, "Context") == null; frame++)
            yield return null;

        object director = valdoria.GetType().GetField("_buildDirector", Members).GetValue(valdoria);
        Assert.That(director, Is.Not.Null);
        Vector3 anchor = valdoria.transform.position;
        MethodInfo buildShipyard = director.GetType().GetMethod("TryBootstrapNavalBase", Members);
        Assert.That(buildShipyard, Is.Not.Null);
        buildShipyard.Invoke(director, new object[] { Time.time, anchor, false, anchor });

        float shipyardDeadline = Time.realtimeSinceStartup + 40f;
        Component shipyard = FindOwnedStructure("Estaleiro", 2);
        while (shipyard == null && Time.realtimeSinceStartup < shipyardDeadline)
        {
            yield return null;
            shipyard = FindOwnedStructure("Estaleiro", 2);
        }

        Assert.That(shipyard, Is.Not.Null,
            "A Valdoria encontrou a área manual, mas não concluiu o estaleiro. Diagnóstico: "
            + Read(valdoria, "NavalDiagnosticSummary") + " | fila=" + Read(valdoria, "RuntimeSummary"));
        Assert.That(IsInsideManualArea(valdoria, "ConstrucaoEstaleiro", shipyard.transform.position), Is.True,
            "O estaleiro concluído precisa permanecer dentro da área manual da IA03.");

        MethodInfo buildManualPier = director.GetType().GetMethod("TryBootstrapMandatoryWaterBuild", Members);
        Assert.That(buildManualPier, Is.Not.Null);
        Type zoneType = ResolveType("Hegemonia.AI.BrainMaster.IA_ZoneType");
        buildManualPier.Invoke(director, new object[]
        {
            "pier", anchor, Enum.Parse(zoneType, "Naval"), 12f, 420f, 975, 10f,
            new[] { "pier", "dock" }
        });

        float pierDeadline = Time.realtimeSinceStartup + 40f;
        Component pier = FindOwnedStructure("PierMarinha", 2);
        while (pier == null && Time.realtimeSinceStartup < pierDeadline)
        {
            yield return null;
            pier = FindOwnedStructure("PierMarinha", 2);
        }

        Assert.That(pier, Is.Not.Null,
            "A Valdoria precisa concluir o píer na sua área manual. Diagnóstico: "
            + Read(valdoria, "NavalDiagnosticSummary") + " | fila=" + Read(valdoria, "RuntimeSummary"));
        Assert.That(IsInsideManualArea(valdoria, "ConstrucaoPier", pier.transform.position), Is.True,
            "O píer concluído precisa permanecer dentro da área manual da IA03.");
        Assert.That(FindOwnedStructure("Estaleiro", 1), Is.Null,
            "A construção naval da IA não pode ser atribuída ao Team 1.");
        Assert.That(FindOwnedStructure("PierMarinha", 1), Is.Null,
            "A construção naval da IA não pode ser atribuída ao Team 1.");
    }

    [UnityTest]
    public IEnumerator NewGameFundsOnlyActiveIAsAndLoadedSavePathDoesNotReapplyTheGrant()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        object government = ReadStatic(governmentType, "Instancia");
        Assert.That(government, Is.Not.Null);
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Component[] brains = FindSceneComponents(brainType);
        IList countries = (IList)Read(government, "Paises");
        Dictionary<int, long> oldBalances = countries.Cast<object>().Where(p => p != null)
            .ToDictionary(p => Convert.ToInt32(Read(p, "teamId")), p => Convert.ToInt64(Read(p, "saldo")));
        Dictionary<int, int> oldFoodStocks = countries.Cast<object>().Where(p => p != null)
            .ToDictionary(p => Convert.ToInt32(Read(p, "teamId")), p => Convert.ToInt32(Read(p, "comida")));
        Dictionary<Component, int> oldCredits = brains.ToDictionary(b => b, b => Convert.ToInt32(Read(b, "Credits")));
        Type saveType = ResolveType("SistemaSaveGame");
        object save = saveType.GetField("Instancia", Members).GetValue(null);
        Assert.That(save, Is.Not.Null);
        FieldInfo newGameMarker = saveType.GetField("<partidaNovaRecemIniciada>k__BackingField", Members);
        FieldInfo mapEconomyInitialized = saveType.GetField("economiaMapaGlobalInicializada", Members);
        bool oldLoaded = (bool)saveType.GetField("carregouDeSave", Members).GetValue(save);
        bool oldNewGameMarker = (bool)newGameMarker.GetValue(save);
        bool oldEconomyInitialized = (bool)mapEconomyInitialized.GetValue(save);
        MethodInfo initializeNewGame = saveType.GetMethod("InicializarEconomiaDePartidaNova", Members);
        MethodInfo onSceneLoaded = saveType.GetMethod("AoCarregarCena", Members);

        try
        {
            long playerBalanceBefore = Convert.ToInt64(Read(Call(government, "ObterPais", 1), "saldo"));
            initializeNewGame.Invoke(save, null);
            long grant = Convert.ToInt64(governmentType.GetField("SaldoInicialNacionalPorIAEmPartidaNova", Members).GetValue(null));
            Assert.That(Read(Call(government, "ObterPais", 2), "saldo"), Is.EqualTo(grant));
            Assert.That(Read(FindBrain(2), "Credits"), Is.EqualTo(int.MaxValue));
            Assert.That(Read(Call(government, "ObterPais", 1), "saldo"), Is.EqualTo(playerBalanceBefore),
                "O saldo do jogador não deve ser substituído pelo grant de IA.");
            Assert.That(Read(Call(government, "ObterPais", 3), "saldo"), Is.EqualTo(oldBalances[3]),
                "A IA inativa Team 3 não deve receber grant.");
            Assert.That(Read(Call(government, "ObterPais", 2), "comida"),
                Is.EqualTo(50000), "A Valdoria ativa deve iniciar a partida nova com 50 mil de comida nacional.");
            Assert.That(Read(Call(government, "ObterPais", 2), "estoqueAgricolaTotal"), Is.EqualTo(50000),
                "O estoque inicial da Valdoria deve permanecer conciliado com o armazem por cultura.");
            Assert.That(Read(Call(government, "ObterPais", 2), "capacidadeArmazenamentoAgricola"), Is.GreaterThanOrEqualTo(100000),
                "A capacidade agricola da Valdoria precisa permitir producao depois do estoque inicial.");
            Type agricultureType = ResolveType("AgriculturaNacional");
            int cultureCount = Convert.ToInt32(ReadStatic(agricultureType, "QuantidadeCulturas"));
            MethodInfo getCultureId = agricultureType.GetMethod("ObterIdCultura", Members);
            Assert.That(getCultureId, Is.Not.Null);
            int totalCulturasValdoria = 0;
            for (int cultura = 0; cultura < cultureCount; cultura++)
                totalCulturasValdoria += Convert.ToInt32(Call(government, "ObterEstoque", 2,
                    getCultureId.Invoke(null, new object[] { cultura })));
            Assert.That(totalCulturasValdoria, Is.EqualTo(50000),
                "O estoque inicial da Valdoria deve estar efetivamente dividido entre culturas vendaveis.");
            Assert.That(Read(Call(government, "ObterPais", 1), "comida"), Is.EqualTo(oldFoodStocks[1]),
                "O estoque inicial da IA não deve alterar a comida do jogador Team 1.");
            Assert.That(Read(Call(government, "ObterPais", 3), "comida"), Is.EqualTo(oldFoodStocks[3]),
                "A IA inativa Team 3 não deve receber comida inicial.");

            const long savedBalance = 987654321L;
            const int savedFood = 7654;
            Assert.That(Call(government, "DefinirSaldoNacional", 2, savedBalance), Is.EqualTo(true));
            Write(Call(government, "ObterPais", 2), "comida", savedFood);
            saveType.GetField("carregouDeSave", Members).SetValue(save, true);
            newGameMarker.SetValue(save, false);
            mapEconomyInitialized.SetValue(save, false);
            onSceneLoaded.Invoke(save, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
            Assert.That(Read(Call(government, "ObterPais", 2), "saldo"), Is.EqualTo(savedBalance),
                "Carregar um save não pode disparar novamente o saldo inicial de partida nova.");
            Assert.That(Read(Call(government, "ObterPais", 2), "comida"), Is.EqualTo(savedFood),
                "Carregar um save não pode substituir o estoque nacional salvo pela comida inicial.");
        }
        finally
        {
            foreach (KeyValuePair<int, long> balance in oldBalances)
                Call(government, "DefinirSaldoNacional", balance.Key, balance.Value);
            foreach (KeyValuePair<int, int> food in oldFoodStocks)
                Write(Call(government, "ObterPais", food.Key), "comida", food.Value);
            Call(government, "NotificarGovernoAtualizado");
            foreach (KeyValuePair<Component, int> credit in oldCredits)
                if (credit.Key != null) Write(credit.Key, "Credits", credit.Value);
            saveType.GetField("carregouDeSave", Members).SetValue(save, oldLoaded);
            newGameMarker.SetValue(save, oldNewGameMarker);
            mapEconomyInitialized.SetValue(save, oldEconomyInitialized);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator ProductionTabCanBuyFoodCapacity()
    {
        object government = ReadStatic(ResolveType("SistemaGovernoMundial"), "Instancia");
        Assert.That(government, Is.Not.Null);
        object player = Call(government, "ObterPais", 1);
        Assert.That(player, Is.Not.Null);
        Type menuType = ResolveType("MenuGovernoNovoController");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        Assert.That(menu, Is.Not.Null, "O controller UI Toolkit ativo do Governo precisa estar no GlobalMapRTS.");
        for (int frame = 0; frame < 120 && !(bool)Read(menu, "Pronto"); frame++)
            yield return null;
        Assert.That(Read(menu, "Pronto"), Is.EqualTo(true));

        int[] agricultureLevels = (int[])Read(player, "niveisInvestimentoAgricola");
        int previousLevel = agricultureLevels[0];
        long previousBalance = Convert.ToInt64(Read(player, "saldo"));
        int previousFood = Convert.ToInt32(Read(player, "comida"));
        int previousAgriculturalStock = Convert.ToInt32(Read(player, "estoqueAgricolaTotal"));
        int previousDailyProduction = Convert.ToInt32(Read(player, "producaoAgricolaDiaria"));
        int previousFixedInvestment = Convert.ToInt32(Read(player, "investimentoAgricolaSemanal"));
        float previousPercentInvestment = Convert.ToSingle(Read(player, "investimentoAgricolaPercentualSemanal"));
        bool previousPercentMode = Convert.ToBoolean(Read(player, "investimentoAgricolaPercentualAtivo"));
        bool previousInvestmentWithoutFunds = Convert.ToBoolean(Read(player, "investimentoAgricolaSemRecursos"));
        long previousAgriculturalCharge = Convert.ToInt64(Read(player, "ultimaCobrancaAgricola"));
        bool previousDailyFoodPurchase = Convert.ToBoolean(Read(player, "compraAlimentarDiariaAtiva"));
        float previousDailyFoodPurchasePercent = Convert.ToSingle(Read(player, "compraAlimentarPercentualCaixaDiario"));
        int previousDailyFoodPurchaseDay = Convert.ToInt32(Read(player, "ultimoDiaCompraAlimentarProcessado"));
        float[] previousPurchaseCultureShares = (float[])Read(player, "participacaoCompraAlimentar");
        float[] purchaseCultureSharesSnapshot = previousPurchaseCultureShares != null
            ? (float[])previousPurchaseCultureShares.Clone()
            : Array.Empty<float>();
        float[] previousCultureFractions = (float[])Read(player, "saldoFracionarioCulturas");
        float[] cultureFractionsSnapshot = previousCultureFractions != null
            ? (float[])previousCultureFractions.Clone()
            : Array.Empty<float>();
        Type marketType = ResolveType("SistemaMercadoGlobal");
        object market = ReadStatic(marketType, "Instancia");
        string[] inventoryIds = {
            "agua", "sementes", "fertilizante_organico", "agrotoxicos", "comida_milho", "comida_batata",
            "comida_feijao", "comida_trigo", "comida_arroz", "comida_cana", "comida_soja", "comida_cafe",
            "comida_cacau", "comida_mandioca", "comida_aveia", "comida_cevada", "comida_tomate",
            "comida_frutas", "comida_hortalicas"
        };
        Dictionary<string, int> previousInventory = inventoryIds.ToDictionary(id => id,
            id => Convert.ToInt32(Call(government, "ObterEstoque", 1, id)));
        Dictionary<string, object> marketItemStates = new Dictionary<string, object>();
        if (market != null)
            foreach (string id in inventoryIds.Where(id => id.StartsWith("comida_", StringComparison.Ordinal)))
            {
                object item = Call(market, "ObterItem", id);
                if (item == null) continue;
                marketItemStates[id] = new object[] { Read(item, "estoqueGlobal"), Read(item, "oferta") };
            }
        Component agriculture = ((Component)government).GetComponent(ResolveType("AgriculturaNacional"));
        Assert.That(agriculture, Is.Not.Null, "A cena real precisa iniciar o sistema nacional de agricultura.");
        MethodInfo produce = agriculture.GetType().GetMethod("Produzir", Members);
        Assert.That(produce, Is.Not.Null);
        MethodInfo showSection = menu.GetType().GetMethod("MostrarSecao", Members);
        MethodInfo showPage = menu.GetType().GetMethod("MostrarPagina", Members);
        FieldInfo actionsField = menu.GetType().GetField("acoes", Members);
        try
        {
            int desiredWater = Mathf.Min(Convert.ToInt32(Read(player, "aguaMaxima")), 2500);
            int waterToAdd = Mathf.Max(0, desiredWater - previousInventory["agua"]);
            if (waterToAdd > 0)
                Call(government, "AdicionarEstoque", 1, Enum.Parse(ResolveType("RecursoMercado"), "Agua"), waterToAdd);
            foreach (string id in inventoryIds.Where(id => id == "sementes" || id == "fertilizante_organico" || id == "agrotoxicos"))
                Call(government, "AdicionarEstoque", 1, id, 1000);
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { true });
            showSection.Invoke(menu, new object[] { "Economia" });
            showPage.Invoke(menu, new object[] { "Producao" });
            VisualElement actions = (VisualElement)actionsField.GetValue(menu);
            UnityEngine.UIElements.Button foodButton = actions.Query<UnityEngine.UIElements.Button>().ToList()
                .FirstOrDefault(button => button.text.StartsWith("AMPLIAR +5 t/dia", StringComparison.Ordinal));
            Assert.That(foodButton, Is.Not.Null, "A aba Producao precisa disponibilizar investimento agrícola real.");
            long cost = Convert.ToInt64(ResolveType("AgriculturaNacional")
                .GetMethod("CalcularCobrancaFixaSemanal", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { 100 + previousLevel * 50 }));
            Assert.That(previousBalance, Is.GreaterThanOrEqualTo(cost), "A partida nova deve permitir o investimento de teste.");
            object clickable = typeof(UnityEngine.UIElements.Button).GetProperty("clickable", Members).GetValue(foodButton);
            MethodInfo simulateClick = clickable.GetType().GetMethod(
                "SimulateSingleClick",
                Members,
                null,
                new[] { typeof(EventBase), typeof(int) },
                null);
            Assert.That(simulateClick, Is.Not.Null, "O UI Toolkit precisa disponibilizar a simulação de clique do botão.");
            simulateClick.Invoke(clickable, new object[] { ClickEvent.GetPooled(), 100 });
            Assert.That(agricultureLevels[0], Is.EqualTo(previousLevel + 1));
            Assert.That(Read(player, "saldo"), Is.EqualTo(previousBalance - cost), "Um clique deve cobrar exatamente um nível de investimento.");
            produce.Invoke(agriculture, new[] { player });
            Assert.That(Read(player, "producaoAgricolaDiaria"), Is.GreaterThan(0),
                "Após investir pela interface, o sistema agrícola deve calcular produção diária positiva.");
            Assert.That(Read(player, "comida"), Is.GreaterThan(previousFood),
                "A produção agrícola precisa entrar no estoque nacional de comida.");

            UnityEngine.UIElements.Button modoFixo = actions.Query<UnityEngine.UIElements.Button>().ToList()
                .FirstOrDefault(button => button.text.Contains("VALOR FIXO"));
            Assert.That(modoFixo, Is.Not.Null, "O menu deve permitir escolher investimentos fixos semanais.");
            object modoFixoClickable = typeof(UnityEngine.UIElements.Button).GetProperty("clickable", Members).GetValue(modoFixo);
            MethodInfo clickModoFixo = modoFixoClickable.GetType().GetMethod(
                "SimulateSingleClick", Members, null, new[] { typeof(EventBase), typeof(int) }, null);
            clickModoFixo.Invoke(modoFixoClickable, new object[] { ClickEvent.GetPooled(), 100 });
            UnityEngine.UIElements.Button investimento250 = actions.Query<UnityEngine.UIElements.Button>().ToList()
                .FirstOrDefault(button => button.tooltip.StartsWith("Débito fixo de", StringComparison.Ordinal)
                    && button.tooltip.Contains("250"));
            Assert.That(investimento250, Is.Not.Null, "O orçamento deve oferecer valores fixos claros em milhões.");
            long saldoAntesDeAgendar = Convert.ToInt64(Read(player, "saldo"));
            object investimentoClickable = typeof(UnityEngine.UIElements.Button).GetProperty("clickable", Members).GetValue(investimento250);
            MethodInfo clickInvestimento = investimentoClickable.GetType().GetMethod(
                "SimulateSingleClick", Members, null, new[] { typeof(EventBase), typeof(int) }, null);
            clickInvestimento.Invoke(investimentoClickable, new object[] { ClickEvent.GetPooled(), 100 });
            Assert.That(Read(player, "investimentoAgricolaSemanal"), Is.EqualTo(250));
            Assert.That(Read(player, "investimentoAgricolaPercentualAtivo"), Is.EqualTo(false));
            Assert.That(Read(player, "saldo"), Is.EqualTo(saldoAntesDeAgendar),
                "Escolher o valor recorrente agenda a cobrança, sem desconto imediato.");
            MethodInfo cobrarInvestimento = agriculture.GetType().GetMethod("ProcessarCobrancaSemanal", Members);
            int productionBeforeWeeklyCharge = Convert.ToInt32(Read(player, "producaoAgricolaDiaria"));
            int foodBeforeWeeklyCharge = Convert.ToInt32(Read(player, "comida"));
            cobrarInvestimento.Invoke(agriculture, new[] { player });
            Assert.That(Read(player, "saldo"), Is.EqualTo(saldoAntesDeAgendar - 250_000_000L),
                "A virada semanal deve descontar o valor fixo configurado.");
            produce.Invoke(agriculture, new[] { player });
            Assert.That(Read(player, "producaoAgricolaDiaria"), Is.GreaterThan(productionBeforeWeeklyCharge),
                "Depois da cobrança semanal, a safra precisa receber o bônus agrícola configurado.");
            Assert.That(Read(player, "comida"), Is.GreaterThan(foodBeforeWeeklyCharge),
                "A colheita do ciclo posterior à cobrança precisa creditar comida no estoque nacional.");

            SliderInt dailyPurchaseBudget = actions.Query<SliderInt>(className: "gov-share-slider").First();
            Assert.That(dailyPurchaseBudget, Is.Not.Null,
                "A compra alimentar automatizada precisa expor seu controle de percentual.");
            dailyPurchaseBudget.value = 0;
            object gameClock = ReadStatic(ResolveType("GerenciadorTempo"), "Instancia");
            int dayBeforeAutomation = gameClock != null
                ? Math.Max(1, Convert.ToInt32(Read(gameClock, "totalDias")))
                : 1;
            long balanceBeforeAutomation = Convert.ToInt64(Read(player, "saldo"));
            int foodBeforeAutomation = Convert.ToInt32(Read(player, "comida"));
            UnityEngine.UIElements.Button activateDailyPurchase = actions.Query<UnityEngine.UIElements.Button>().ToList()
                .FirstOrDefault(button => button.text == "ATIVAR + COMPRAR HOJE");
            Assert.That(activateDailyPurchase, Is.Not.Null,
                "A rotina de compra diária precisa oferecer o botão de ativação.");
            SimulateClick(activateDailyPurchase);
            Assert.That(Read(player, "compraAlimentarDiariaAtiva"), Is.EqualTo(true),
                "O botão precisa ativar a rotina diária no país.");
            Assert.That(Read(player, "ultimoDiaCompraAlimentarProcessado"), Is.EqualTo(dayBeforeAutomation),
                "A tentativa imediata precisa marcar hoje e não cobrar duas vezes na mesma virada.");
            Assert.That(Read(player, "saldo"), Is.EqualTo(balanceBeforeAutomation),
                "Com orçamento de compra em zero, ativar a rotina não pode descontar dinheiro.");
            Assert.That(Read(player, "comida"), Is.EqualTo(foodBeforeAutomation),
                "Uma compra sem orçamento não pode creditar comida.");

            UnityEngine.UIElements.Button pauseDailyPurchase = actions.Query<UnityEngine.UIElements.Button>().ToList()
                .FirstOrDefault(button => button.text == "PAUSAR ROTINA");
            Assert.That(pauseDailyPurchase, Is.Not.Null,
                "A rotina ativa precisa oferecer o botão de pausa.");
            SimulateClick(pauseDailyPurchase);
            Assert.That(Read(player, "compraAlimentarDiariaAtiva"), Is.EqualTo(false),
                "O botão precisa pausar a rotina diária.");
        }
        finally
        {
            agricultureLevels[0] = previousLevel;
            Call(government, "DefinirSaldoNacional", 1, previousBalance);
            foreach (KeyValuePair<string, int> stock in previousInventory)
            {
                int current = Convert.ToInt32(Call(government, "ObterEstoque", 1, stock.Key));
                if (current > stock.Value) Call(government, "RemoverEstoque", 1, stock.Key, current - stock.Value);
                else if (current < stock.Value) Call(government, "AdicionarEstoque", 1, stock.Key, stock.Value - current);
            }
            int currentFood = Convert.ToInt32(Read(player, "comida"));
            if (currentFood > previousFood)
                Call(government, "RemoverEstoque", 1, Enum.Parse(ResolveType("RecursoMercado"), "Comida"), currentFood - previousFood);
            else if (currentFood < previousFood)
                Call(government, "AdicionarEstoque", 1, Enum.Parse(ResolveType("RecursoMercado"), "Comida"), previousFood - currentFood);
            foreach (KeyValuePair<string, object> state in marketItemStates)
            {
                object item = Call(market, "ObterItem", state.Key);
                object[] values = state.Value as object[];
                if (item != null && values != null)
                {
                    Write(item, "estoqueGlobal", values[0]);
                    Write(item, "oferta", values[1]);
                }
            }
            Write(player, "estoqueAgricolaTotal", previousAgriculturalStock);
            Write(player, "producaoAgricolaDiaria", previousDailyProduction);
            Write(player, "investimentoAgricolaSemanal", previousFixedInvestment);
            Write(player, "investimentoAgricolaPercentualSemanal", previousPercentInvestment);
            Write(player, "investimentoAgricolaPercentualAtivo", previousPercentMode);
            Write(player, "investimentoAgricolaSemRecursos", previousInvestmentWithoutFunds);
            Write(player, "ultimaCobrancaAgricola", previousAgriculturalCharge);
            Write(player, "compraAlimentarDiariaAtiva", previousDailyFoodPurchase);
            Write(player, "compraAlimentarPercentualCaixaDiario", previousDailyFoodPurchasePercent);
            Write(player, "ultimoDiaCompraAlimentarProcessado", previousDailyFoodPurchaseDay);
            if (previousPurchaseCultureShares != null)
                Array.Copy(purchaseCultureSharesSnapshot, previousPurchaseCultureShares,
                    Mathf.Min(purchaseCultureSharesSnapshot.Length, previousPurchaseCultureShares.Length));
            if (previousCultureFractions != null)
                Array.Copy(cultureFractionsSnapshot, previousCultureFractions,
                    Mathf.Min(cultureFractionsSnapshot.Length, previousCultureFractions.Length));
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });
        }

    }

    [UnityTest]
    public IEnumerator MarketBuyButtonDebitsAndDeliversTheSelectedResource()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        object government = ReadStatic(governmentType, "Instancia");
        object market = ReadStatic(ResolveType("SistemaMercadoGlobal"), "Instancia");
        object logistics = ReadStatic(ResolveType("SistemaLogisticaMercado"), "Instancia");
        Assert.That(government, Is.Not.Null);
        Assert.That(market, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);

        object buyer = Call(government, "ObterPais", 1);
        object seller = Call(government, "ObterPais", 2);
        object relation = Call(government, "ObterRelacao", 1, 2);
        object item = Call(market, "ObterItem", "sementes");
        Assert.That(buyer, Is.Not.Null);
        Assert.That(seller, Is.Not.Null);
        Assert.That(relation, Is.Not.Null);
        Assert.That(item, Is.Not.Null);

        Type menuType = ResolveType("MenuGovernoNovoController");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        Assert.That(menu, Is.Not.Null);
        for (int frame = 0; frame < 120 && !(bool)Read(menu, "Pronto"); frame++)
            yield return null;
        Assert.That(Read(menu, "Pronto"), Is.EqualTo(true));

        long buyerBalance = Convert.ToInt64(Read(buyer, "saldo"));
        long sellerBalance = Convert.ToInt64(Read(seller, "saldo"));
        int itemGlobalStock = Convert.ToInt32(Read(item, "estoqueGlobal"));
        object itemOffer = Read(item, "oferta");
        object itemDemand = Read(item, "demanda");
        IList countries = Read(government, "paises") as IList;
        Assert.That(countries, Is.Not.Null);
        Dictionary<int, int> seedStocks = new Dictionary<int, int>();
        foreach (object country in countries)
        {
            int team = Convert.ToInt32(Read(country, "teamId"));
            int stock = Convert.ToInt32(Call(government, "ObterEstoque", team, "sementes"));
            seedStocks[team] = stock;
            if (team != 2 && stock > 0)
                Call(government, "RemoverEstoque", team, "sementes", stock);
        }

        bool relationTrade = Convert.ToBoolean(Read(relation, "tratadoComercial"));
        int relationValue = Convert.ToInt32(Read(relation, "valor"));
        bool relationWar = Convert.ToBoolean(Read(relation, "guerraDeclarada"));
        bool relationSanction = Convert.ToBoolean(Read(relation, "sancaoAtiva"));
        float logisticsInterval = Convert.ToSingle(Read(logistics, "intervaloProcessamento"));
        float loadingTime = Convert.ToSingle(Read(logistics, "tempoEmbarque"));
        float unloadingTime = Convert.ToSingle(Read(logistics, "tempoDesembarque"));
        IList orders = Read(logistics, "pedidos") as IList;
        IList pending = Read(logistics, "entregasPendentes") as IList;
        IList history = Read(market, "historico") as IList;
        Assert.That(orders, Is.Not.Null);
        Assert.That(pending, Is.Not.Null);
        Assert.That(history, Is.Not.Null);
        int orderCount = orders.Count;
        int pendingCount = pending.Count;
        int historyCount = history.Count;
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Call(government, "DefinirSaldoNacional", 1, 1_000_000_000L);
            Call(government, "DefinirSaldoNacional", 2, 1_000_000_000L);
            Call(government, "AdicionarEstoque", 2, "sementes", 1_000);
            Write(item, "estoqueGlobal", Mathf.Max(1_000, itemGlobalStock));
            Write(relation, "valor", 90);
            Write(relation, "tratadoComercial", true);
            Write(relation, "guerraDeclarada", false);
            Write(relation, "sancaoAtiva", false);

            GameObject sellerPier = new GameObject("GovernmentMarketTest_SellerPier");
            temporaryObjects.Add(sellerPier);
            sellerPier.transform.position = Vector3.zero;
            Component sellerPierComponent = sellerPier.AddComponent(ResolveType("PierMarinha"));
            Write(sellerPierComponent, "OwnerTeamId", 2);

            GameObject buyerPier = new GameObject("GovernmentMarketTest_BuyerPier");
            temporaryObjects.Add(buyerPier);
            buyerPier.transform.position = new Vector3(60f, 0f, 0f);
            Component buyerPierComponent = buyerPier.AddComponent(ResolveType("PierMarinha"));
            Write(buyerPierComponent, "OwnerTeamId", 1);

            Write(logistics, "intervaloProcessamento", 0.25f);
            Write(logistics, "tempoEmbarque", 0.1f);
            Write(logistics, "tempoDesembarque", 0.1f);
            GameObject cargoObject = new GameObject("GovernmentMarketTest_Cargo");
            temporaryObjects.Add(cargoObject);
            cargoObject.transform.position = sellerPier.transform.position;
            Component cargo = cargoObject.AddComponent(ResolveType("NavioCargaMercado"));
            Write(cargo, "velocidadeCruzeiro", 500f);
            Call(cargo, "Inicializar", 1, false);

            // O serviço de logística atualiza o cache dos piers periodicamente.
            yield return new WaitForSecondsRealtime(1.1f);

            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { true });
            menuType.GetMethod("MostrarSecao", Members).Invoke(menu, new object[] { "Economia" });
            menuType.GetMethod("MostrarPagina", Members).Invoke(menu, new object[] { "Producao" });
            FieldInfo actionsField = menuType.GetField("acoes", Members);
            FieldInfo contentField = menuType.GetField("conteudo", Members);
            Assert.That(actionsField, Is.Not.Null);
            Assert.That(contentField, Is.Not.Null);
            VisualElement actions = actionsField.GetValue(menu) as VisualElement;
            Button inputMarketButton = actions.Query<Button>().ToList()
                .FirstOrDefault(button => button.text.StartsWith("SEMENTES E", StringComparison.Ordinal));
            Assert.That(inputMarketButton, Is.Not.Null,
                "A produção agrícola precisa abrir o Mercado > Comprar com o botão de insumos.");
            SimulateClick(inputMarketButton);

            VisualElement content = contentField.GetValue(menu) as VisualElement;
            Button inputsFilter = content.Query<Button>(className: "gov-market-filter").ToList()
                .FirstOrDefault(button => string.Equals(button.text, "INSUMOS", StringComparison.OrdinalIgnoreCase));
            Assert.That(inputsFilter, Is.Not.Null, "O filtro de insumos deve estar acessível no Mercado.");
            SimulateClick(inputsFilter);
            yield return null;
            yield return null;

            content = contentField.GetValue(menu) as VisualElement;
            VisualElement seedCard = content.Query<VisualElement>(className: "gov-market-card").ToList()
                .FirstOrDefault(card => card.Query<Label>(className: "gov-card-title").First()?.text
                    .IndexOf("SEMENTES", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(seedCard, Is.Not.Null, "O card de sementes precisa aparecer no filtro selecionado.");
            Assert.That(seedCard.worldBound.width, Is.GreaterThan(0f));
            foreach (Label label in seedCard.Query<Label>().ToList())
            {
                Assert.That(label.worldBound.xMin, Is.GreaterThanOrEqualTo(seedCard.worldBound.xMin - 1f),
                    "O texto " + label.text + " deve começar dentro do card de mercado.");
                Assert.That(label.worldBound.xMax, Is.LessThanOrEqualTo(seedCard.worldBound.xMax + 1f),
                    "O texto " + label.text + " deve terminar dentro do card de mercado.");
            }

            IntegerField quantity = seedCard.Query<IntegerField>(className: "gov-quantity").First();
            Assert.That(quantity, Is.Not.Null);
            quantity.value = 10;
            Label total = seedCard.Query<Label>(className: "gov-market-total").First();
            Assert.That(total.text, Does.Contain("TOTAL A PAGAR"));
            Assert.That(total.text, Does.Contain("10"), "O total precisa refletir a quantidade escolhida.");

            Button buy = seedCard.Query<Button>().ToList()
                .FirstOrDefault(button => string.Equals(button.text, "COMPRAR", StringComparison.OrdinalIgnoreCase));
            Assert.That(buy, Is.Not.Null);
            Assert.That(buy.enabledSelf, Is.True,
                "Com fornecedor aliado, estoque, rota e saldo disponíveis, COMPRAR precisa estar habilitado.");
            long balanceBeforePurchase = Convert.ToInt64(Read(buyer, "saldo"));
            int buyerStockBeforePurchase = Convert.ToInt32(Call(government, "ObterEstoque", 1, "sementes"));
            SimulateClick(buy);

            float deliveryDeadline = Time.realtimeSinceStartup + 20f;
            object order = null;
            while (order == null && Time.realtimeSinceStartup < deliveryDeadline)
            {
                if (orders.Count > orderCount)
                {
                    for (int index = orders.Count - 1; index >= orderCount; index--)
                    {
                        object candidate = orders[index];
                        if (Convert.ToInt32(Read(candidate, "compradorTeamId")) == 1
                            && string.Equals(Convert.ToString(Read(candidate, "itemId")), "sementes", StringComparison.OrdinalIgnoreCase))
                        {
                            order = candidate;
                            break;
                        }
                    }
                }
                if (order == null)
                    yield return null;
            }

            Assert.That(order, Is.Not.Null, "O clique em COMPRAR precisa criar uma ordem para o fornecedor.");
            Assert.That(Read(order, "vendedorTeamId"), Is.EqualTo(2));
            Assert.That(Read(order, "quantidade"), Is.EqualTo(10));
            Assert.That(Read(buyer, "saldo"), Is.LessThan(balanceBeforePurchase),
                "A confirmação precisa descontar o item e o frete do saldo nacional.");

            while (!string.Equals(Convert.ToString(Read(order, "status")), "ENTREGUE", StringComparison.OrdinalIgnoreCase)
                && Time.realtimeSinceStartup < deliveryDeadline)
                yield return null;

            Assert.That(Read(order, "status").ToString(), Is.EqualTo("ENTREGUE"),
                "A ordem criada pela interface precisa completar o embarque e o desembarque.");
            Assert.That(Call(government, "ObterEstoque", 1, "sementes"), Is.EqualTo(buyerStockBeforePurchase + 10),
                "As sementes compradas devem ser creditadas no estoque do país depois da entrega.");
        }
        finally
        {
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });
            Call(government, "DefinirSaldoNacional", 1, buyerBalance);
            Call(government, "DefinirSaldoNacional", 2, sellerBalance);
            foreach (KeyValuePair<int, int> stock in seedStocks)
            {
                int current = Convert.ToInt32(Call(government, "ObterEstoque", stock.Key, "sementes"));
                int delta = stock.Value - current;
                if (delta > 0) Call(government, "AdicionarEstoque", stock.Key, "sementes", delta);
                else if (delta < 0) Call(government, "RemoverEstoque", stock.Key, "sementes", -delta);
            }
            Write(item, "estoqueGlobal", itemGlobalStock);
            Write(item, "oferta", itemOffer);
            Write(item, "demanda", itemDemand);
            Write(relation, "valor", relationValue);
            Write(relation, "tratadoComercial", relationTrade);
            Write(relation, "guerraDeclarada", relationWar);
            Write(relation, "sancaoAtiva", relationSanction);
            Write(logistics, "intervaloProcessamento", logisticsInterval);
            Write(logistics, "tempoEmbarque", loadingTime);
            Write(logistics, "tempoDesembarque", unloadingTime);
            while (orders.Count > orderCount) orders.RemoveAt(orders.Count - 1);
            while (pending.Count > pendingCount) pending.RemoveAt(pending.Count - 1);
            while (history.Count > historyCount) history.RemoveAt(history.Count - 1);
            for (int index = 0; index < temporaryObjects.Count; index++)
                if (temporaryObjects[index] != null) UnityEngine.Object.Destroy(temporaryObjects[index]);
        }
    }

    [UnityTest]
    public IEnumerator MarketSellButtonPaysAndTransfersEnergyDirectly()
    {
        object government = ReadStatic(ResolveType("SistemaGovernoMundial"), "Instancia");
        object market = ReadStatic(ResolveType("SistemaMercadoGlobal"), "Instancia");
        Assert.That(government, Is.Not.Null);
        Assert.That(market, Is.Not.Null);

        object seller = Call(government, "ObterPais", 1);
        object relation = Call(government, "ObterRelacao", 1, 2);
        object item = Call(market, "ObterItem", "energia");
        Assert.That(seller, Is.Not.Null);
        Assert.That(relation, Is.Not.Null);
        Assert.That(item, Is.Not.Null);

        Type menuType = ResolveType("MenuGovernoNovoController");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        Assert.That(menu, Is.Not.Null);
        for (int frame = 0; frame < 120 && !(bool)Read(menu, "Pronto"); frame++)
            yield return null;
        Assert.That(Read(menu, "Pronto"), Is.EqualTo(true));

        const int quantityToSell = 10;
        string resourceId = Convert.ToString(Read(item, "RecursoIdEfetivo"));
        int sellerStockBefore = Convert.ToInt32(Call(government, "ObterEstoque", 1, resourceId));
        object buyer = Call(government, "ObterPais", 2);
        Assert.That(buyer, Is.Not.Null, "A venda precisa encontrar um comprador comercial disponível.");
        int buyerTeamId = Convert.ToInt32(Read(buyer, "teamId"));
        int buyerStockBefore = Convert.ToInt32(Call(government, "ObterEstoque", buyerTeamId, resourceId));
        long sellerBalanceBefore = Convert.ToInt64(Read(seller, "saldo"));
        long buyerBalanceBefore = Convert.ToInt64(Read(buyer, "saldo"));
        int globalStockBefore = Convert.ToInt32(Read(item, "estoqueGlobal"));
        object previousOffer = Read(item, "oferta");
        object previousDemand = Read(item, "demanda");
        bool previousSellEnabled = Convert.ToBoolean(Read(item, "podeVender"));
        bool previousTrade = Convert.ToBoolean(Read(relation, "tratadoComercial"));
        int previousRelation = Convert.ToInt32(Read(relation, "valor"));
        bool previousWar = Convert.ToBoolean(Read(relation, "guerraDeclarada"));
        bool previousSanction = Convert.ToBoolean(Read(relation, "sancaoAtiva"));
        IList history = Read(market, "historico") as IList;
        Assert.That(history, Is.Not.Null);
        int historyCount = history.Count;
        int total = quantityToSell * Mathf.Max(1, Convert.ToInt32(Read(item, "precoAtual")));

        try
        {
            Call(government, "DefinirSaldoNacional", 1, 1_000_000_000L);
            Call(government, "DefinirSaldoNacional", buyerTeamId, 1_000_000_000L);
            Call(government, "AdicionarEstoque", 1, resourceId, quantityToSell);
            Write(relation, "valor", 90);
            Write(relation, "tratadoComercial", true);
            Write(relation, "guerraDeclarada", false);
            Write(relation, "sancaoAtiva", false);
            Write(item, "podeVender", true);
            Assert.That(Call(market, "EncontrarMelhorComprador", 1, item, quantityToSell), Is.SameAs(buyer),
                "Com o tratado ativo e saldo disponível, a página deve selecionar o comprador preparado pelo teste.");

            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { true });
            menuType.GetMethod("MostrarSecao", Members).Invoke(menu, new object[] { "Mercado" });
            menuType.GetMethod("MostrarPagina", Members).Invoke(menu, new object[] { "Vender" });
            FieldInfo contentField = menuType.GetField("conteudo", Members);
            Assert.That(contentField, Is.Not.Null);
            VisualElement content = contentField.GetValue(menu) as VisualElement;
            VisualElement energyCard = content.Query<VisualElement>(className: "gov-market-card").ToList()
                .FirstOrDefault(card => card.Query<Label>(className: "gov-card-title").First()?.text
                    .IndexOf("ENERGIA", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(energyCard, Is.Not.Null, "Energia precisa aparecer na página Mercado > Vender.");

            IntegerField quantity = energyCard.Query<IntegerField>(className: "gov-quantity").First();
            Assert.That(quantity, Is.Not.Null);
            quantity.value = quantityToSell;
            Button sell = energyCard.Query<Button>().ToList()
                .FirstOrDefault(button => string.Equals(button.text, "VENDER", StringComparison.OrdinalIgnoreCase));
            Assert.That(sell, Is.Not.Null);
            Assert.That(sell.enabledSelf, Is.True,
                "VENDER precisa ficar habilitado com estoque nacional, comprador aliado e saldo disponíveis.");
            SimulateClick(sell);

            Assert.That(Read(seller, "saldo"), Is.EqualTo(1_000_000_000L + total),
                "O clique em VENDER deve creditar o valor ao vendedor.");
            Assert.That(Read(buyer, "saldo"), Is.EqualTo(1_000_000_000L - total),
                "A venda direta de energia deve debitar o comprador.");
            Assert.That(Call(government, "ObterEstoque", 1, resourceId), Is.EqualTo(sellerStockBefore),
                "As unidades vendidas devem sair do estoque do vendedor.");
            Assert.That(Call(government, "ObterEstoque", buyerTeamId, resourceId), Is.EqualTo(buyerStockBefore + quantityToSell),
                "A energia vendida deve entrar no estoque do comprador.");
            Assert.That(Read(item, "estoqueGlobal"), Is.EqualTo(globalStockBefore + quantityToSell));
            Assert.That(history.Count, Is.EqualTo(historyCount + 1));
            Assert.That(Read(history[history.Count - 1], "status"), Is.EqualTo("ENTREGUE"));
        }
        finally
        {
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });
            Call(government, "DefinirSaldoNacional", 1, sellerBalanceBefore);
            Call(government, "DefinirSaldoNacional", buyerTeamId, buyerBalanceBefore);
            int sellerStockNow = Convert.ToInt32(Call(government, "ObterEstoque", 1, resourceId));
            int buyerStockNow = Convert.ToInt32(Call(government, "ObterEstoque", buyerTeamId, resourceId));
            int sellerDelta = sellerStockBefore - sellerStockNow;
            int buyerDelta = buyerStockBefore - buyerStockNow;
            if (sellerDelta > 0) Call(government, "AdicionarEstoque", 1, resourceId, sellerDelta);
            else if (sellerDelta < 0) Call(government, "RemoverEstoque", 1, resourceId, -sellerDelta);
            if (buyerDelta > 0) Call(government, "AdicionarEstoque", buyerTeamId, resourceId, buyerDelta);
            else if (buyerDelta < 0) Call(government, "RemoverEstoque", buyerTeamId, resourceId, -buyerDelta);
            Write(item, "estoqueGlobal", globalStockBefore);
            Write(item, "oferta", previousOffer);
            Write(item, "demanda", previousDemand);
            Write(item, "podeVender", previousSellEnabled);
            Write(relation, "valor", previousRelation);
            Write(relation, "tratadoComercial", previousTrade);
            Write(relation, "guerraDeclarada", previousWar);
            Write(relation, "sancaoAtiva", previousSanction);
            while (history.Count > historyCount) history.RemoveAt(history.Count - 1);
        }
    }

    [UnityTest]
    public IEnumerator CountryNameTextFieldCapturesGameplayShortcuts()
    {
        Type menuType = ResolveType("MenuGovernoNovoController");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        Assert.That(menu, Is.Not.Null);
        for (int frame = 0; frame < 120 && !(bool)Read(menu, "Pronto"); frame++)
            yield return null;
        Assert.That(Read(menu, "Pronto"), Is.EqualTo(true));

        menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { true });
        menuType.GetMethod("MostrarSecao", Members).Invoke(menu, new object[] { "Pais" });
        menuType.GetMethod("MostrarPagina", Members).Invoke(menu, new object[] { "Identidade" });
        yield return null;

        FieldInfo rootField = menuType.GetField("root", Members);
        Assert.That(rootField, Is.Not.Null);
        VisualElement root = rootField.GetValue(menu) as VisualElement;
        TextField countryName = root.Query<TextField>(className: "gov-identity-field").First();
        Assert.That(countryName, Is.Not.Null, "A página de identidade precisa mostrar o campo do nome do país.");
        countryName.Focus();
        yield return null;
        Type inputBindingsType = ResolveType("Hegemonia.RTS.RTSInputBindings");
        MethodInfo isTextEntryFocused = inputBindingsType.GetMethod("IsTextEntryFocused", Members);
        Assert.That(isTextEntryFocused, Is.Not.Null);
        Assert.That(isTextEntryFocused.Invoke(null, null), Is.EqualTo(true),
            "Ao editar o nome, B e os demais atalhos de gameplay precisam ser consumidos pelo campo.");

        countryName.Blur();
        yield return null;
        Assert.That(isTextEntryFocused.Invoke(null, null), Is.EqualTo(false),
            "Os atalhos de gameplay devem voltar a funcionar depois que o campo perder foco.");
        menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });
    }

    [UnityTest]
    public IEnumerator InfantryAndTankMoveOnGeneratedGlobalMapLand()
    {
        Time.timeScale = 1f;
        Type streamerType = ResolveType("GlobalTerrainStreamer");
        Component streamer = UnityEngine.Object.FindFirstObjectByType(streamerType) as Component;
        Assert.That(streamer, Is.Not.Null, "O streaming real de terreno do GlobalMap precisa estar ativo.");
        streamerToRestoreAfterMovementTest = streamer;
        Behaviour streamerBehaviour = streamer as Behaviour;
        Assert.That(streamerBehaviour, Is.Not.Null);
        originalStreamerEnabled = streamerBehaviour.enabled;
        streamerBehaviour.enabled = true;
        originalTilesCreatedPerFrame = Convert.ToInt32(Read(streamer, "tilesCreatedPerFrame"));
        originalTerrainRowsPerFrame = Convert.ToInt32(Read(streamer, "terrainRowsPerFrame"));
        originalVegetationRowsPerFrame = Convert.ToInt32(Read(streamer, "vegetationRowsPerFrame"));
        originalRefreshInterval = Convert.ToSingle(Read(streamer, "refreshInterval"));

        Camera camera = Read(streamer, "cameraOverride") as Camera;
        Assert.That(camera, Is.Not.Null, "O streamer precisa apontar para a câmera ativa do mapa global.");
        cameraToRestoreAfterMovementTest = camera;
        originalStreamerCameraOverride = camera;
        originalCameraPositionBeforeMovementTest = camera.transform.position;

        object world = Read(streamer, "world");
        Assert.That(world, Is.Not.Null, "A definição geográfica do mapa precisa estar atribuída.");
        float tileSize = Convert.ToSingle(Read(world, "terrainTileSize"));
        float minX = Convert.ToSingle(Read(world, "MapMinX"));
        float minZ = Convert.ToSingle(Read(world, "MapMinZ"));
        Vector2 worldSize = (Vector2)Read(world, "worldSize");
        float footprintHeight = Convert.ToSingle(Read(world, "mapFootprintHeight"));
        MethodInfo isLandAtWorld = world.GetType().GetMethod("IsLandAtWorld", Members);
        MethodInfo containsLand = world.GetType().GetMethod("ContainsLand", Members);
        MethodInfo heightAtWorld = world.GetType().GetMethod("HeightAtWorld", Members);
        Assert.That(isLandAtWorld, Is.Not.Null);
        Assert.That(containsLand, Is.Not.Null);
        Assert.That(heightAtWorld, Is.Not.Null);

        Vector3 landCameraPosition = Vector3.zero;
        bool foundLandTile = false;
        int maxTileX = Mathf.CeilToInt(worldSize.x / tileSize);
        int maxTileZ = Mathf.CeilToInt(footprintHeight / tileSize);
        for (int z = 0; z < maxTileZ && !foundLandTile; z++)
        {
            for (int x = 0; x < maxTileX; x++)
            {
                float sampleX = minX + (x + 0.5f) * tileSize;
                float sampleZ = minZ + (z + 0.5f) * tileSize;
                float tileMinX = minX + x * tileSize;
                float tileMinZ = minZ + z * tileSize;
                if (!Convert.ToBoolean(isLandAtWorld.Invoke(world, new object[] { sampleX, sampleZ }))
                    || !Convert.ToBoolean(containsLand.Invoke(world, new object[]
                    { tileMinX, tileMinZ, tileMinX + tileSize, tileMinZ + tileSize, 64 })))
                    continue;

                float terrainHeight = Convert.ToSingle(heightAtWorld.Invoke(world, new object[] { sampleX, sampleZ }));
                landCameraPosition = new Vector3(sampleX, terrainHeight + 300f, sampleZ);
                foundLandTile = true;
                break;
            }
        }
        Assert.That(foundLandTile, Is.True, "A definição do mundo precisa fornecer um tile de terra para testar unidades.");

        // A cena pode iniciar a câmera sobre o oceano; nesse caso, zero tiles
        // locais é correto. Posicione-a temporariamente sobre terra conhecida
        // para validar os agentes e restaure o valor no teardown.
        temporaryStreamingCameraObject = new GameObject("GlobalMap_MovementTest_StreamingCamera_Temporary");
        Camera streamingCamera = temporaryStreamingCameraObject.AddComponent<Camera>();
        streamingCamera.enabled = false;
        streamingCamera.transform.position = landCameraPosition;
        Write(streamer, "cameraOverride", streamingCamera);
        cameraToRestoreAfterMovementTest = null;
        Write(streamer, "tilesCreatedPerFrame", 4);
        Write(streamer, "terrainRowsPerFrame", 64);
        Write(streamer, "vegetationRowsPerFrame", 16);
        Write(streamer, "refreshInterval", 0.1f);

        // StreamContinuously captures camera and world state through its own
        // coroutine. Restart it after swapping the override so it cannot keep
        // streaming around the scene's original ocean camera.
        streamerBehaviour.enabled = false;
        yield return null;
        streamerBehaviour.enabled = true;

        object runtimeNavMesh = null;
        for (int frame = 0; frame < 1800; frame++)
        {
            runtimeNavMesh = Read(streamer, "runtimeLocalNavMeshData");
            if (Convert.ToInt32(Read(streamer, "ActiveTileCount")) > 0 && runtimeNavMesh != null)
                break;
            yield return null;
        }
        int activeTiles = Convert.ToInt32(Read(streamer, "ActiveTileCount"));
        Assert.That(activeTiles, Is.GreaterThan(0),
            "O mapa precisa ter terreno local gerado na região de câmera. Diagnóstico: desejados="
            + Read(streamer, "DesiredTileCount") + ", cache=" + Read(streamer, "CachedTileCount")
            + ", macro=" + Read(streamer, "IsMacroTerrainMode") + ", câmera=" + streamingCamera.transform.position);
        Assert.That(runtimeNavMesh, Is.Not.Null,
            "O NavMesh local precisa estar construído para as unidades terrestres.");

        Type surfaceRegistryType = ResolveType("RegistroSuperficieMapa");
        Type surfaceClassificationType = ResolveType("ClassificacaoSuperficieMapa");
        MethodInfo classifySurface = surfaceRegistryType.GetMethod(
            "TryClassify", Members, null,
            new[] { typeof(Vector3), surfaceClassificationType.MakeByRefType(), typeof(float).MakeByRefType(), typeof(float), typeof(float) },
            null);
        Assert.That(classifySurface, Is.Not.Null);

        Vector3 start = Vector3.zero;
        bool foundLand = false;
        Vector3[] directions = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
            (Vector3.forward + Vector3.right).normalized, (Vector3.forward + Vector3.left).normalized,
            (Vector3.back + Vector3.right).normalized, (Vector3.back + Vector3.left).normalized };
        for (int ring = 0; ring <= 20 && !foundLand; ring++)
        {
            float radius = ring * 100f;
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector3 probe = streamingCamera.transform.position + directions[directionIndex] * radius;
                if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out UnityEngine.AI.NavMeshHit hit, 500f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;
                object[] classificationArgs = { hit.position, Activator.CreateInstance(surfaceClassificationType), 0f, 1.5f, 0f };
                bool classified = Convert.ToBoolean(classifySurface.Invoke(null, classificationArgs));
                if (classified && string.Equals(Convert.ToString(classificationArgs[1]), "Chao", StringComparison.Ordinal))
                {
                    start = hit.position;
                    foundLand = true;
                    break;
                }
            }
        }
        Assert.That(foundLand, Is.True,
            "O NavMesh gerado precisa fornecer um ponto de partida classificado como chão.");

        GameObject unitRoot = new GameObject("GlobalMap_GroundMovementAcceptance_Temporary");
        unitRoot.SetActive(false);
        try
        {
            yield return AssertPrefabCanMoveOnGround(
                "Assets/Prefabs/Soldado_Rifle/Soldier.prefab", "Soldier", start,
                unitRoot, classifySurface, surfaceClassificationType);
            yield return AssertPrefabCanMoveOnGround(
                "Assets/Prefabs/Veiculos/Tank_Ubu/Ubu.prefab", "Ubu", start,
                unitRoot, classifySurface, surfaceClassificationType);
        }
        finally
        {
            if (unitRoot != null)
                UnityEngine.Object.Destroy(unitRoot);
        }
    }

    private static IEnumerator AssertPrefabCanMoveOnGround(
        string prefabPath,
        string unitLabel,
        Vector3 groundStart,
        GameObject unitRoot,
        MethodInfo classifySurface,
        Type surfaceClassificationType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, "Prefab de unidade não encontrado: " + prefabPath);
        GameObject unit = UnityEngine.Object.Instantiate(prefab, groundStart, Quaternion.identity, unitRoot.transform);
        unit.name = "GlobalMap_MovementTest_" + unitLabel;
        Component identity = unit.GetComponentInChildren(ResolveType("IdentidadeUnidade"), true);
        Component control = unit.GetComponentInChildren(ResolveType("ControleUnidade"), true);
        UnityEngine.AI.NavMeshAgent agent = unit.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        Assert.That(identity, Is.Not.Null, unitLabel + " precisa ter IdentidadeUnidade.");
        Assert.That(control, Is.Not.Null, unitLabel + " precisa ter ControleUnidade.");
        Assert.That(agent, Is.Not.Null, unitLabel + " precisa ter NavMeshAgent.");
        Write(identity, "teamID", 1);

        unitRoot.SetActive(true);
        yield return null;
        Assert.That(agent.enabled, Is.True, unitLabel + " precisa habilitar o NavMeshAgent.");
        if (!agent.isOnNavMesh)
            agent.Warp(groundStart);
        Assert.That(agent.isOnNavMesh, Is.True, unitLabel + " precisa iniciar sobre o NavMesh do terreno.");

        Vector3 destination = Vector3.zero;
        bool foundPath = false;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
            (Vector3.forward + Vector3.right).normalized, (Vector3.forward + Vector3.left).normalized,
            (Vector3.back + Vector3.right).normalized, (Vector3.back + Vector3.left).normalized };
        for (int distance = 20; distance <= 120 && !foundPath; distance += 10)
        {
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector3 probe = groundStart + directions[directionIndex] * distance;
                if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out UnityEngine.AI.NavMeshHit hit, 8f, agent.areaMask)
                    || Vector3.Distance(groundStart, hit.position) < 12f)
                    continue;

                object[] classificationArgs = { hit.position, Activator.CreateInstance(surfaceClassificationType), 0f, 1.5f, 0f };
                bool classified = Convert.ToBoolean(classifySurface.Invoke(null, classificationArgs));
                if (!classified || !string.Equals(Convert.ToString(classificationArgs[1]), "Chao", StringComparison.Ordinal))
                    continue;

                var path = new UnityEngine.AI.NavMeshPath();
                if (!UnityEngine.AI.NavMesh.CalculatePath(groundStart, hit.position, agent.areaMask, path)
                    || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    continue;

                destination = hit.position;
                foundPath = true;
                break;
            }
        }
        Assert.That(foundPath, Is.True,
            unitLabel + " precisa encontrar um ponto terrestre conectado no NavMesh da região visível.");

        bool orderAccepted = Convert.ToBoolean(Call(control, "EmitirOrdemMover", destination, true));
        Assert.That(orderAccepted, Is.True,
            unitLabel + " recusou a ordem terrestre: " + Convert.ToString(Read(control, "ultimoMotivoRecusaOrdem")));

        float startDistance = Vector3.Distance(groundStart, destination);
        float deadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < deadline
            && HorizontalDistance(unit.transform.position, groundStart) < 1f)
            yield return null;
        float horizontalMovement = HorizontalDistance(unit.transform.position, groundStart);
        Assert.That(horizontalMovement, Is.GreaterThan(1f),
            unitLabel + " aceitou a ordem, mas não saiu do ponto inicial sobre o chão."
            + " movimentoXZ=" + horizontalMovement.ToString("F2")
            + " agente=" + agent.transform.position
            + " velocidade=" + agent.velocity
            + " destinoAgente=" + agent.destination
            + " parado=" + agent.isStopped
            + " temCaminho=" + agent.hasPath
            + " pathPending=" + agent.pathPending
            + " remaining=" + (agent.hasPath && !agent.pathPending ? agent.remainingDistance.ToString("F2") : "n/a")
            + " executor=" + Convert.ToString(Read(control, "executorControleAtual"))
            + " motivo=" + Convert.ToString(Read(control, "ultimoMotivoRecusaOrdem")));
        Assert.That(agent.pathStatus, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete),
            unitLabel + " precisa manter um caminho completo sobre o terreno.");
        Assert.That(Vector3.Distance(unit.transform.position, destination), Is.LessThan(startDistance),
            unitLabel + " precisa se aproximar do destino terrestre.");

        UnityEngine.Object.Destroy(unit);
        yield return null;
        unitRoot.SetActive(false);
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return Vector3.Distance(left, right);
    }

    [UnityTest]
    public IEnumerator ConstructionMenuSearchRecordsRuntimeRebuildCost()
    {
        Type menuType = ResolveType("MenuConstrucao");
        Component menu = UnityEngine.Object.FindFirstObjectByType(menuType, FindObjectsInactive.Include) as Component;
        Assert.That(menu, Is.Not.Null, "O menu Construção precisa existir na cena global.");
        if (!menu.gameObject.activeSelf)
            menu.gameObject.SetActive(true);

        FieldInfo cardsField = menuType.GetField("containerBotoes", Members);
        FieldInfo searchField = menuType.GetField("campoBusca", Members);
        FieldInfo tabsField = menuType.GetField("containerAbas", Members);
        Assert.That(cardsField, Is.Not.Null);
        Assert.That(searchField, Is.Not.Null);
        Assert.That(tabsField, Is.Not.Null);
        for (int frame = 0; frame < 120; frame++)
        {
            if (cardsField.GetValue(menu) != null && searchField.GetValue(menu) != null)
                break;
            yield return null;
        }

        Transform cards = cardsField.GetValue(menu) as Transform;
        Transform tabs = tabsField.GetValue(menu) as Transform;
        UnityEngine.UI.InputField search = searchField.GetValue(menu) as UnityEngine.UI.InputField;
        Assert.That(cards, Is.Not.Null, "A grade real de cards precisa estar criada.");
        Assert.That(tabs, Is.Not.Null, "As abas reais de categoria precisam estar criadas.");
        Assert.That(search, Is.Not.Null, "O campo real de pesquisa precisa estar conectado ao filtro.");
        FieldInfo categoryField = menuType.GetField("categoriaAtual", Members);
        Assert.That(categoryField, Is.Not.Null);
        bool originalMenuOpen = Convert.ToBoolean(Read(menu, "menuAberto"));
        object originalCategory = categoryField.GetValue(menu);
        string originalSearch = search.text;
        bool wasActive = menu.gameObject.activeSelf;

        Type categoryType = ResolveType("DadosConstrucao+CategoriaItem");
        object[] categories = Enum.GetValues(categoryType).Cast<object>().ToArray();
        Dictionary<string, int> expectedCategoryCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Exercito", 24 },
            { "Marinha", 30 },
            { "Aeronautica", 25 },
            { "Tecnologia", 0 },
            { "Infraestrutura", 13 },
            { "Energia", 7 },
            { "Urbana", 5 },
        };
        FieldInfo catalogField = menuType.GetField("catalogo", Members);
        Assert.That(catalogField, Is.Not.Null);
        IList catalog = catalogField.GetValue(menu) as IList;
        Assert.That(catalog, Is.Not.Null);
        object[] originalCatalog = catalog.Cast<object>().ToArray();
        object categoryWithMostItems = null;
        int largestCategory = 0;
        Stopwatch timer = new Stopwatch();
        try
        {
            menuType.GetMethod("AlternarMenu", Members, null, new[] { typeof(bool) }, null)
                .Invoke(menu, new object[] { true });
            for (int frame = 0; frame < 15; frame++)
                yield return null;

            foreach (object category in categories)
            {
                timer.Restart();
                ClickConstructionCategory(tabs, category);
                timer.Stop();
                yield return null;
                int count = CountVisibleConstructionCards(cards);
                Assert.That(expectedCategoryCounts.ContainsKey(Convert.ToString(category)), Is.True,
                    "A nova categoria precisa ser incluída na auditoria de conteúdo.");
                Assert.That(count, Is.EqualTo(expectedCategoryCounts[Convert.ToString(category)]),
                    "A otimização não pode remover opções da categoria " + category + ".");
                TestContext.WriteLine("Construção categoria " + category + ": " + count + " cards; "
                    + timer.Elapsed.TotalMilliseconds.ToString("F2") + " ms síncronos.");
                catalog = catalogField.GetValue(menu) as IList;
                Assert.That(catalog, Is.Not.Null);
                Assert.That(catalog.Count, Is.EqualTo(originalCatalog.Length), "A validação de cards não pode alterar a quantidade do catálogo.");
                for (int itemIndex = 0; itemIndex < originalCatalog.Length; itemIndex++)
                    Assert.That(catalog[itemIndex], Is.SameAs(originalCatalog[itemIndex]),
                        "A validação de cards não pode remover ou reordenar a ficha " + itemIndex + ".");
                if (count > largestCategory)
                {
                    largestCategory = count;
                    categoryWithMostItems = category;
                }
            }

            Assert.That(categoryWithMostItems, Is.Not.Null);
            Assert.That(largestCategory, Is.GreaterThan(0), "O catálogo deve renderizar ao menos uma opção de construção.");
            ClickConstructionCategory(tabs, categoryWithMostItems);
            yield return null;

            for (int repeat = 0; repeat < 2; repeat++)
            {
                timer.Restart();
                ClickConstructionCategory(tabs, categoryWithMostItems);
                timer.Stop();
                yield return null;
                TestContext.WriteLine("Construção troca repetida " + categoryWithMostItems + " #" + (repeat + 1)
                    + ": " + CountVisibleConstructionCards(cards) + " cards; "
                    + timer.Elapsed.TotalMilliseconds.ToString("F2") + " ms síncronos.");
            }

            string[] prefixes = { "", "a", "ar", "art", "__sem_resultados__" };
            foreach (string prefix in prefixes)
            {
                timer.Restart();
                search.text = prefix;
                timer.Stop();
                yield return null;
                int visibleCards = CountVisibleConstructionCards(cards);
                TestContext.WriteLine("Construção pesquisa '" + prefix + "': " + visibleCards + " cards; "
                    + timer.Elapsed.TotalMilliseconds.ToString("F2") + " ms síncronos.");
                if (prefix.Length <= 1)
                    Assert.That(visibleCards, Is.GreaterThan(0), "A busca curta precisa preservar resultados correspondentes.");
                if (prefix == "__sem_resultados__")
                    Assert.That(visibleCards, Is.EqualTo(0), "A busca sem correspondências deve esconder todos os cards da grade.");
            }
        }
        finally
        {
            search.text = originalSearch;
            ClickConstructionCategory(tabs, originalCategory);
            if (!originalMenuOpen)
                menuType.GetMethod("AlternarMenu", Members, null, new[] { typeof(bool) }, null)
                    .Invoke(menu, new object[] { false });
            if (!wasActive)
                menu.gameObject.SetActive(false);
        }
        for (int frame = 0; frame < 15; frame++)
            yield return null;
    }

    [UnityTest]
    public IEnumerator MiniMapFitsBothTargetResolutions()
    {
        Type miniMapType = ResolveType("MiniMapa");
        Component miniMap = UnityEngine.Object.FindFirstObjectByType(miniMapType, FindObjectsInactive.Include) as Component;
        Assert.That(miniMap, Is.Not.Null);
        GameObject canvasObject = GameObject.Find("Canvas_MiniMapa");
        Assert.That(canvasObject, Is.Not.Null, "A UI runtime do minimapa deve existir na cena global.");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Transform canvasParent = canvasObject.transform.parent;
        Assert.That(canvasParent == null
            || (Mathf.Abs(canvasParent.lossyScale.x) > 0.0001f && Mathf.Abs(canvasParent.lossyScale.y) > 0.0001f), Is.True,
            "O canvas overlay pode usar a hierarquia da cena somente quando a escala do pai é utilizável.");
        Assert.That(canvasObject.transform.localScale.x, Is.GreaterThan(0f),
            "A escala de resolução do Canvas precisa permanecer utilizável.");
        Assert.That(Read(miniMap, "espessuraBorda"), Is.EqualTo(0f));
        Transform panelTransform = canvasObject.transform.Find("MiniMapa_Container");
        Assert.That(panelTransform, Is.Not.Null);
        Assert.That(panelTransform.localScale, Is.EqualTo(Vector3.one),
            "O minimapa deve ocupar o mesmo painel de referência dos dados de desempenho.");
        Transform frameTransform = panelTransform.Find("Frame");
        Assert.That(frameTransform, Is.Not.Null);
        Assert.That(frameTransform.GetComponent<UnityEngine.UI.Image>().enabled, Is.False,
            "espessuraBorda zero deve remover a borda visual.");

        MethodInfo calculate = miniMap.GetType().GetMethod("CalculateResponsivePanelLayout", Members);
        Assert.That(calculate, Is.Not.Null);
        float margin = Convert.ToSingle(Read(miniMap, "margemBorda"));
        AssertLayout(calculate, 1920f, 1080f, 1f, margin);
        AssertLayout(calculate, 1280f, 720f, 2f / 3f, margin);
        yield return null;
    }

    private static void AssertLayout(MethodInfo calculate, float width, float height, float canvasScale, float margin)
    {
        Rect layout = (Rect)calculate.Invoke(null, new object[] { width, height, canvasScale, margin });
        float canvasWidth = width / canvasScale;
        float canvasHeight = height / canvasScale;
        float visibleScale = canvasScale;
        float rightEdge = (layout.x + layout.width) * canvasScale;
        float bottomEdge = layout.y * canvasScale;
        float topEdge = (layout.y + layout.height) * canvasScale;

        Assert.That(layout.width * visibleScale, Is.EqualTo(330f).Within(1f), "largura alinhada ao painel de desempenho");
        Assert.That(layout.height * visibleScale, Is.EqualTo(262f).Within(1f), "altura alinhada ao painel de desempenho");
        Assert.That(width - rightEdge, Is.EqualTo(12f).Within(1.5f), "margem direita alinhada ao painel de desempenho");
        Assert.That(bottomEdge, Is.EqualTo(178f).Within(1f), "posicao vertical igual ao painel de desempenho");
        Assert.That(topEdge, Is.LessThanOrEqualTo(height - margin * canvasScale + 1f), "o minimapa deve permanecer dentro da tela");
    }

    private static void SimulateClick(Button button)
    {
        object clickable = typeof(Button).GetProperty("clickable", Members).GetValue(button);
        MethodInfo simulateClick = clickable.GetType().GetMethod(
            "SimulateSingleClick", Members, null, new[] { typeof(EventBase), typeof(int) }, null);
        Assert.That(simulateClick, Is.Not.Null);
        simulateClick.Invoke(clickable, new object[] { ClickEvent.GetPooled(), 100 });
    }

    private static int CountVisibleConstructionCards(Transform cards)
    {
        int count = 0;
        foreach (Transform child in cards)
            if (child.gameObject.activeSelf)
                count++;
        return count;
    }

    private static void ClickConstructionCategory(Transform tabs, object category)
    {
        Transform tab = tabs.Find("Aba_" + Convert.ToString(category));
        Assert.That(tab, Is.Not.Null, "A aba visual da categoria " + category + " precisa existir.");
        UnityEngine.UI.Button button = tab.GetComponent<UnityEngine.UI.Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.enabled, Is.True);
        button.onClick.Invoke();
    }

    private static Component FindBrain(int teamId)
    {
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        return FindSceneComponents(brainType)
            .FirstOrDefault(brain => Convert.ToInt32(Read(brain, "TeamId")) == teamId);
    }

    private static Component FindOwnedStructure(string typeName, int teamId)
    {
        Type structureType = ResolveType(typeName);
        Type identityType = ResolveType("IdentidadeUnidade");
        return FindSceneComponents(structureType).FirstOrDefault(structure =>
        {
            Component identity = structure.GetComponentInParent(identityType)
                ?? structure.GetComponentInChildren(identityType, true);
            return identity != null && Convert.ToInt32(Read(identity, "teamID")) == teamId;
        });
    }

    private static bool IsInsideManualArea(Component brain, string roleName, Vector3 position)
    {
        Type pointType = ResolveType("Hegemonia.AI.BrainMaster.IA_ManualBuildPoint");
        Component area = brain.GetComponentsInChildren(pointType, true).FirstOrDefault(point =>
            Read(point, "ManualRole").ToString() == roleName);
        if (area == null || !(bool)Read(area, "UsesPlacementArea"))
            return false;

        float radius = Convert.ToSingle(Read(area, "PlacementAreaRadius"));
        Vector3 flatPosition = new Vector3(position.x, 0f, position.z);
        Vector3 flatCenter = new Vector3(area.transform.position.x, 0f, area.transform.position.z);
        return Vector3.Distance(flatPosition, flatCenter) <= radius + 0.1f;
    }

    private static Component[] FindSceneComponents(Type type)
    {
        return Resources.FindObjectsOfTypeAll(type).OfType<Component>()
            .Where(component => component != null && component.gameObject.scene.IsValid())
            .ToArray();
    }

    private static object Read(object target, string name)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null)
            return property.GetValue(target, null);
        FieldInfo field = type.GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo/propriedade ausente: " + type.FullName + "." + name);
        return field.GetValue(target);
    }

    private static object ReadStatic(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null)
            return property.GetValue(null, null);
        FieldInfo field = type.GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo/propriedade estática ausente: " + type.FullName + "." + name);
        return field.GetValue(null);
    }

    private static object CallStatic(Type type, string name, params object[] args)
    {
        MethodInfo method = type.GetMethod(name, Members);
        Assert.That(method, Is.Not.Null, "Método estático ausente: " + type.FullName + "." + name);
        Assert.That(method.IsStatic, Is.True, "O método esperado precisa ser estático: " + type.FullName + "." + name);
        return method.Invoke(null, args);
    }

    private static void Write(object target, string name, object value)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return;
        }
        FieldInfo field = type.GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + type.FullName + "." + name);
        field.SetValue(target, value);
    }

    private static object Call(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethods(Members)
            .FirstOrDefault(candidate =>
            {
                if (candidate.Name != name) return false;
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != args.Length) return false;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args[i] == null)
                    {
                        if (parameters[i].ParameterType.IsValueType
                            && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null) return false;
                    }
                    else if (!parameters[i].ParameterType.IsInstanceOfType(args[i])) return false;
                }
                return true;
            });
        Assert.That(method, Is.Not.Null, "Método ausente: " + target.GetType().FullName + "." + name);
        return method.Invoke(target, args);
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        Assert.Fail("Tipo não encontrado: " + fullName);
        return null;
    }
}
#endif
