#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Component strategist = valdoria.GetComponent(strategistType);
        Assert.That(strategist, Is.Not.Null, "A IA estratégica nacional da Valdoria precisa estar no mapa real.");
        Assert.That(((Behaviour)strategist).isActiveAndEnabled, Is.True);
        Assert.That((bool)Read(strategist, "TemPerfilConfigurado"), Is.True,
            "O perfil estratégico nacional da Valdoria precisa estar ativo.");
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
            int totalCulturasValdoria = 0;
            for (int cultura = 0; cultura < AgriculturaNacional.QuantidadeCulturas; cultura++)
                totalCulturasValdoria += Convert.ToInt32(Call(government, "ObterEstoque", 2,
                    AgriculturaNacional.ObterIdCultura(cultura)));
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
    public IEnumerator ProductionTabCanBuyFoodCapacityAndMiniMapFitsBothTargetResolutions()
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
            cobrarInvestimento.Invoke(agriculture, new[] { player });
            Assert.That(Read(player, "saldo"), Is.EqualTo(saldoAntesDeAgendar - 250_000_000L),
                "A virada semanal deve descontar o valor fixo configurado.");
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
            if (previousCultureFractions != null)
                Array.Copy(cultureFractionsSnapshot, previousCultureFractions,
                    Mathf.Min(cultureFractionsSnapshot.Length, previousCultureFractions.Length));
            menuType.GetMethod("Abrir", Members).Invoke(menu, new object[] { false });
        }

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
        Assert.That(panelTransform.localScale, Is.EqualTo(Vector3.one * 0.5f),
            "O painel visual do minimapa deve ser reduzido em 50%.");
        Transform frameTransform = panelTransform.Find("Frame");
        Assert.That(frameTransform, Is.Not.Null);
        Assert.That(frameTransform.GetComponent<UnityEngine.UI.Image>().enabled, Is.False,
            "espessuraBorda zero deve remover a borda visual.");

        MethodInfo calculate = miniMap.GetType().GetMethod("CalculateResponsivePanelLayout", Members);
        Assert.That(calculate, Is.Not.Null);
        float margin = Convert.ToSingle(Read(miniMap, "margemBorda"));
        AssertLayout(calculate, 1920f, 1080f, 1f, margin);
        AssertLayout(calculate, 1280f, 720f, 2f / 3f, margin);
    }

    private static void AssertLayout(MethodInfo calculate, float width, float height, float canvasScale, float margin)
    {
        Rect layout = (Rect)calculate.Invoke(null, new object[] { width, height, canvasScale, margin });
        float canvasWidth = width / canvasScale;
        float canvasHeight = height / canvasScale;
        float visibleScale = canvasScale * 0.5f;
        float rightEdge = (layout.x + layout.width * 0.5f) * canvasScale;
        float bottomEdge = layout.y * canvasScale;
        float topEdge = (layout.y + layout.height * 0.5f) * canvasScale;
        float hudHeight = Mathf.Min(Mathf.Clamp(canvasHeight * 0.19f, 145f, 190f), canvasHeight * 0.25f);
        float hudTop = (Mathf.Max(8f, margin / canvasScale) + hudHeight) * canvasScale;

        Assert.That(layout.width * visibleScale, Is.GreaterThan(0f));
        Assert.That(layout.height * visibleScale, Is.GreaterThan(0f));
        Assert.That(width - rightEdge, Is.EqualTo(margin).Within(1.5f), "margem direita em pixels da tela");
        Assert.That(bottomEdge, Is.GreaterThanOrEqualTo(hudTop + 22f * canvasScale - 1f), "o minimapa não pode cobrir o HUD inferior");
        Assert.That(topEdge, Is.LessThanOrEqualTo(height - margin * canvasScale + 1f), "o minimapa deve permanecer dentro da tela");
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
