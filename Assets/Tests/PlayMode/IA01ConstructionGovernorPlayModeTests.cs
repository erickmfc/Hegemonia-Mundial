using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class IA01ConstructionGovernorPlayModeTests
{
    private const string MenuSceneName = "Menu cena";
    private const string SaveFileName = "save_partida.json";

    private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

    private static Type ManagerType => ResolveType("Hegemonia.AI.IA01.IA01Manager");
    private static Type ControllerType => ResolveType("Hegemonia.AI.IA01.IA01Controller");
    private static Type GovernorSettingsType => ResolveType("Hegemonia.AI.IA01.IA01ConstructionGovernorSettings");
    private static Type GovernmentSystemType => ResolveType("SistemaGovernoMundial");
    private static Type TerritorialManagerType => ResolveType("GerenciadorDivisaoTerritorial");
    private static Type MenuControllerType => ResolveType("MenuInicialController");
    private static Type DiagnosticType => ResolveType("DiagnosticoDesempenhoJogo");
    private static Type SaveGameType => ResolveType("SistemaSaveGame");
    private static Type CountryType => ResolveType("DadosPaisGoverno");

    [UnityTest]
    public IEnumerator CampaignFlow_CreatesCapital_Territory_AndPublishesHudMetrics()
    {
        yield return LoadFreshCampaign();
        yield return WaitUntil(() => FindManager() != null, 15f, "IA01Manager nao foi carregado.");

        object manager = FindManager();
        object controller = ResolveController(manager);
        object runtime = GetRuntime(controller);

        Assert.That(manager, Is.Not.Null);
        Assert.That(controller, Is.Not.Null);
        Assert.That(runtime, Is.Not.Null);

        AssertSafeProfileDefaults(controller);

        List<string> transitions = new List<string>();
        bool sawWaitingConfirmation = false;
        int maxPendingCommands = 0;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 12f)
        {
            string state = GetStringMember(runtime, "ConstructionStateStatus");
            AddTransition(transitions, state);
            sawWaitingConfirmation |= state == "WaitingConfirmation";

            object buildDirector = GetMemberValue(runtime, "BuildDirector");
            maxPendingCommands = Mathf.Max(maxPendingCommands, GetIntMember(buildDirector, "PendingCommandCount"));

            object cityPlanner = GetMemberValue(runtime, "CityPlanner");
            object capital = GetMemberValue(cityPlanner, "Capital");
            if (capital != null
                && GetIntMember(GetMemberValue(runtime, "ConstructionGovernor"), "BuildingsTotal") >= 1
                && state == "Idle"
                && GetIntMember(buildDirector, "PendingCommandCount") == 0
                && sawWaitingConfirmation)
            {
                break;
            }

            yield return null;
        }

        object cityPlannerObject = GetMemberValue(runtime, "CityPlanner");
        object capitalObject = GetMemberValue(cityPlannerObject, "Capital");
        bool openingAlreadyAdvanced = capitalObject != null
            && GetIntMember(GetMemberValue(runtime, "ConstructionGovernor"), "BuildingsTotal") >= 1;
        if (!openingAlreadyAdvanced)
        {
            Assert.That(transitions, Does.Contain("SelectingIntent"));
            Assert.That(transitions, Does.Contain("SelectingCatalogItem"));
            Assert.That(transitions, Does.Contain("SearchingLot"));
            Assert.That(transitions, Does.Contain("Reserved"));
            Assert.That(transitions, Does.Contain("WaitingConfirmation"));
        }
        Assert.That(transitions, Does.Contain("Cooldown"));
        Assert.That(sawWaitingConfirmation || openingAlreadyAdvanced, Is.True, "A abertura nao confirmou nem concluiu a capital.");
        Assert.That(maxPendingCommands, Is.LessThanOrEqualTo(1), "A fila permitiu mais de um comando pendente.");

        Assert.That(capitalObject, Is.Not.Null, "Capital nao foi confirmada.");
        Assert.That(GetIntMember(capitalObject, "teamID"), Is.EqualTo(GetIntMember(controller, "TeamId")));
        Assert.That(GetBoolMember(capitalObject, "ehPrefeitura"), Is.True);
        Assert.That(GetBoolMember(controller, "HasConfirmedCapital"), Is.True, "O controlador nao reconheceu a prefeitura confirmada.");
        Assert.That(GetStringMember(runtime, "CapitalSourceStatus"), Is.Not.EqualTo("Missing"));
        Assert.That(GetStringMember(runtime, "LastConstructionCompletedAtStatus"), Is.Not.EqualTo("n/d"));
        Assert.That(GetIntMember(GetMemberValue(runtime, "BuildDirector"), "PendingCommandCount"), Is.EqualTo(0));

        object territorial = GetStaticMemberValue(TerritorialManagerType, "Instancia");
        Assert.That(territorial, Is.Not.Null);
        Assert.That(GetCollectionCount(GetMemberValue(territorial, "cidades")), Is.GreaterThanOrEqualTo(1));

        yield return WaitUntil(() => FindDiagnostic() != null, 10f, "DiagnosticoDesempenhoJogo nao apareceu.");
        object diagnostic = FindDiagnostic();
        // A captura fica desligada por padrao na campanha para nao adicionar
        // custo de FrameTiming ao gameplay. O teste opta explicitamente pela
        // telemetria antes de validar os campos publicados pela IA01.
        if (diagnostic is Component diagnosticComponent && !diagnosticComponent.gameObject.activeInHierarchy)
        {
            diagnosticComponent.gameObject.SetActive(true);
        }
        InvokeInstance(diagnostic, "SetCaptureMode", true, false);
        Assert.That(GetStaticMemberValue(DiagnosticType, "CapturaAtiva"), Is.EqualTo(true), "A captura de diagnostico nao foi ativada.");

        string[] requiredHudKeys =
        {
            "ia01_construction_mode",
            "ia01_construction_state",
            "ia01_construction_freeze_reason",
            "ia01_next_unfreeze_condition",
            "ia01_active_command",
            "ia01_pending_structure",
            "ia01_confirmation_deadline",
            "ia01_treasury",
            "ia01_emergency_reserve",
            "ia01_available_construction_funds",
            "ia01_sequence_step",
            "ia01_buildings_total",
            "ia01_buildings_by_strategic_role",
            "ia01_current_need",
            "ia01_need_score",
            "ia01_current_lot",
            "ia01_catalog_intent_queries",
            "ia01_catalog_index_builds",
            "ia01_catalog_candidates",
            "ia01_physics_checks",
            "ia01_last_construction_completed_at",
            "ia01_foundation_funding_granted",
            "ia01_foundation_capital_cost",
            "ia01_foundation_available_funds",
            "ia01_last_failure_code"
        };

        yield return WaitUntil(() => HasHudMetrics(diagnostic, requiredHudKeys), 8f, "HUD nao publicou todas as metricas da IA01.");

        for (int i = 0; i < requiredHudKeys.Length; i++)
        {
            AssertHudMetric(diagnostic, requiredHudKeys[i]);
        }

        IA01PerfSummary perf = ReadPerfSummary(diagnostic);
        TestContext.WriteLine(
            "Perf snapshot: fpsMedio=" + perf.FpsMedio.ToString("0.00")
            + " fpsMin=" + perf.FpsMinimo.ToString("0.00")
            + " cpuMainMs=" + perf.CpuMainMs.ToString("0.00")
            + " gpuMs=" + perf.GpuMs.ToString("0.00")
            + " gc0=" + perf.GcGen0
            + " gc1=" + perf.GcGen1
            + " gc2=" + perf.GcGen2
            + " ia01.frame=" + GetMetricTimeText(diagnostic, "ia01.frame")
            + " ia01.slice=" + GetMetricTimeText(diagnostic, "ia01.slice." + GetIntMember(controller, "NationId"))
            + " sliceCount=" + GetMetricCount(diagnostic, "ia01.slice.count"));
    }

    [UnityTest]
    public IEnumerator ConstructionGovernor_FreezesOnLowTreasury_AndReactsToPopulationFoodAndEnergy()
    {
        yield return LoadFreshCampaign();
        yield return WaitUntil(() => FindManager() != null, 15f, "IA01Manager nao foi carregado.");

        object manager = FindManager();
        object controller = ResolveController(manager);
        object runtime = GetRuntime(controller);

        yield return WaitUntil(() => GetIntMember(GetMemberValue(runtime, "ConstructionGovernor"), "BuildingsTotal") >= 1, 12f, "A fundacao inicial nao apareceu.");

        object country = GetCountry(GetIntMember(controller, "TeamId"));
        Assert.That(country, Is.Not.Null);

        yield return WaitUntil(() => GetIntMember(GetMemberValue(runtime, "BuildDirector"), "PendingCommandCount") == 0
                                   && (GetStringMember(runtime, "ConstructionStateStatus") == "Idle"
                                       || GetStringMember(runtime, "ConstructionStateStatus") == "Cooldown"),
            10f,
            "A fila nao estabilizou.");

        int catalogQueriesBeforeFreeze = GetIntMember(GetMemberValue(runtime, "ConstructionGovernor"), "CatalogIntentQueries");
        int candidatesBeforeFreeze = GetIntMember(GetMemberValue(runtime, "BuildDirector"), "CandidatesEvaluated");
        int physicsBeforeFreeze = GetIntMember(GetMemberValue(runtime, "BuildDirector"), "PhysicsChecks");

        SetMemberValue(country, "saldo", 0);
        yield return WaitUntil(() => GetStringMember(GetMemberValue(runtime, "ConstructionGovernor"), "ConstructionMode") == "Frozen", 5f, "O governador nao congelou.");
        yield return WaitSecondsRealtime(0.75f);

        object governor = GetMemberValue(runtime, "ConstructionGovernor");
        Assert.That(GetStringMember(governor, "ConstructionFreezeReason"), Is.Not.EqualTo("Nenhum"));
        Assert.That(GetStringMember(governor, "NextUnfreezeCondition"), Is.Not.EqualTo("Nenhuma"));
        Assert.That(GetIntMember(governor, "CatalogIntentQueries"), Is.EqualTo(catalogQueriesBeforeFreeze), "O catalogo continuou sendo consultado congelado.");
        Assert.That(GetIntMember(GetMemberValue(runtime, "BuildDirector"), "CandidatesEvaluated"), Is.EqualTo(candidatesBeforeFreeze), "O avaliador de lotes continuou rodando congelado.");
        Assert.That(GetIntMember(GetMemberValue(runtime, "BuildDirector"), "PhysicsChecks"), Is.EqualTo(physicsBeforeFreeze), "As checagens de fisica continuaram rodando congelado.");

        SetMemberValue(country, "saldo", 10000);
        yield return WaitUntil(() => GetStringMember(GetMemberValue(runtime, "ConstructionGovernor"), "ConstructionMode") == "Active", 5f, "O governador nao retomou.");

        object context = GetMemberValue(controller, "Context");
        SetPopulationSource(country, 200, 400, 80f, 80f);
        InvokeInstance(context, "SetPopulation", 200, 200, 0, 0, 200, 200, 400, 80f, 80f);
        SetMemberValue(country, "energia", 5000);
        SetMemberValue(country, "comida", 5000);
        yield return WaitSecondsRealtime(0.75f);
        Assert.That(ContainsIgnoreCase(GetStringMember(runtime, "NextObjectiveStatus"), "moradia"), Is.False);
        Assert.That(ContainsIgnoreCase(GetStringMember(runtime, "CurrentNeedStatus"), "moradia"), Is.False);

        SetPopulationSource(country, 800, 200, 75f, 75f);
        InvokeInstance(context, "SetPopulation", 800, 800, 0, 0, 800, 800, 200, 75f, 75f);
        yield return WaitUntil(() => ContainsIgnoreCase(GetStringMember(runtime, "NextObjectiveStatus"), "moradia")
                                   || ContainsIgnoreCase(GetStringMember(runtime, "CurrentNeedStatus"), "moradia"),
            8f,
            "Moradia nao foi reavaliada quando a populacao passou da capacidade.");

        SetMemberValue(country, "energia", 100);
        SetMemberValue(country, "comida", 100);
        yield return WaitUntil(() => ContainsIgnoreCase(GetStringMember(runtime, "NextObjectiveStatus"), "energia")
                                   || ContainsIgnoreCase(GetStringMember(runtime, "NextObjectiveStatus"), "comida")
                                   || ContainsIgnoreCase(GetStringMember(runtime, "CurrentNeedStatus"), "energia")
                                   || ContainsIgnoreCase(GetStringMember(runtime, "CurrentNeedStatus"), "comida"),
            15f,
            "Energia ou comida nao voltaram a ser prioridade.");

        TestContext.WriteLine(
            "Treasury/funds: saldo=" + GetLongMember(country, "saldo")
            + " reserve=" + GetStringMember(governor, "EmergencyReserve")
            + " available=" + GetStringMember(governor, "AvailableConstructionFunds")
            + " buildings=" + GetStringMember(runtime, "BuildingsTotalStatus")
            + " need=" + GetStringMember(runtime, "CurrentNeedStatus")
            + " objective=" + GetStringMember(runtime, "NextObjectiveStatus"));
    }

    [UnityTest]
    public IEnumerator TwoIA01ControllersMaintainIndependentGovernorsQueuesAndTreasuries()
    {
        yield return LoadFreshCampaign();
        yield return WaitUntil(() => FindManager() != null, 15f, "IA01Manager nao foi carregado.");

        object manager = FindManager();
        object firstController = ResolveController(manager);
        object firstRuntime = GetRuntime(firstController);
        object secondController = EnsureSecondController(manager, firstController);
        object secondRuntime = GetRuntime(secondController);

        Assert.That(firstRuntime, Is.Not.Null);
        Assert.That(secondRuntime, Is.Not.Null);

        yield return WaitUntil(
            () => GetMemberValue(GetMemberValue(firstRuntime, "CityPlanner"), "Capital") != null
                && GetIntMember(GetMemberValue(firstRuntime, "ConstructionGovernor"), "BuildingsTotal") >= 1
                && GetMemberValue(GetMemberValue(secondRuntime, "CityPlanner"), "Capital") != null
                && GetIntMember(GetMemberValue(secondRuntime, "ConstructionGovernor"), "BuildingsTotal") >= 1,
            15f,
            "As prefeituras das duas IAs nao foram confirmadas antes do teste de tesouraria.");

        yield return WaitUntil(
            () => GetIntMember(GetMemberValue(firstRuntime, "BuildDirector"), "PendingCommandCount") == 0
                && (GetStringMember(firstRuntime, "ConstructionStateStatus") == "Idle"
                    || GetStringMember(firstRuntime, "ConstructionStateStatus") == "Cooldown")
                && GetIntMember(GetMemberValue(secondRuntime, "BuildDirector"), "PendingCommandCount") == 0
                && (GetStringMember(secondRuntime, "ConstructionStateStatus") == "Idle"
                    || GetStringMember(secondRuntime, "ConstructionStateStatus") == "Cooldown"),
            10f,
            "As filas de construcao nao estabilizaram antes do teste de tesouraria.");

        object firstCountry = GetCountry(GetIntMember(firstController, "TeamId"));
        object secondCountry = GetCountry(GetIntMember(secondController, "TeamId"));
        Assert.That(firstCountry, Is.Not.Null);
        Assert.That(secondCountry, Is.Not.Null);

        SetMemberValue(firstCountry, "saldo", 0);
        SetMemberValue(secondCountry, "saldo", 20000);

        object firstContext = GetMemberValue(firstController, "Context");
        object secondContext = GetMemberValue(secondController, "Context");
        InvokeInstance(firstContext, "SetPopulation", 150, 150, 0, 0, 150, 150, 250, 70f, 70f);
        InvokeInstance(secondContext, "SetPopulation", 150, 150, 0, 0, 150, 150, 250, 70f, 70f);

        yield return WaitUntil(() => GetStringMember(GetMemberValue(firstRuntime, "ConstructionGovernor"), "ConstructionMode") == "Frozen", 5f, "A primeira IA nao congelou.");
        yield return WaitUntil(() => GetStringMember(GetMemberValue(secondRuntime, "ConstructionGovernor"), "ConstructionMode") == "Active", 5f, "A segunda IA foi afetada pelo congelamento da primeira.");

        object firstGovernor = GetMemberValue(firstRuntime, "ConstructionGovernor");
        object secondGovernor = GetMemberValue(secondRuntime, "ConstructionGovernor");
        Assert.That(GetStringMember(firstGovernor, "ConstructionFreezeReason"), Is.Not.EqualTo("Nenhum"));
        Assert.That(GetStringMember(secondGovernor, "ConstructionFreezeReason"), Is.EqualTo("Nenhum"));
        Assert.That(GetIntMember(GetMemberValue(firstRuntime, "BuildDirector"), "PendingCommandCount"), Is.LessThanOrEqualTo(1));
        Assert.That(GetIntMember(GetMemberValue(secondRuntime, "BuildDirector"), "PendingCommandCount"), Is.LessThanOrEqualTo(1));

        TestContext.WriteLine(
            "IA1: mode=" + GetStringMember(firstGovernor, "ConstructionMode")
            + " freeze=" + GetStringMember(firstGovernor, "ConstructionFreezeReason")
            + " queue=" + GetIntMember(GetMemberValue(firstRuntime, "BuildDirector"), "PendingCommandCount")
            + " treasury=" + GetStringMember(firstRuntime, "TreasuryStatus"));
        TestContext.WriteLine(
            "IA2: mode=" + GetStringMember(secondGovernor, "ConstructionMode")
            + " freeze=" + GetStringMember(secondGovernor, "ConstructionFreezeReason")
            + " queue=" + GetIntMember(GetMemberValue(secondRuntime, "BuildDirector"), "PendingCommandCount")
            + " treasury=" + GetStringMember(secondRuntime, "TreasuryStatus"));
    }

    [UnityTest]
    public IEnumerator EstablishCapital_UsesFoundationFunding_WithoutInitialFundsBlock()
    {
        yield return LoadFreshCampaign();
        yield return WaitUntil(() => FindManager() != null, 15f, "IA01Manager nao foi carregado.");

        object manager = FindManager();
        object controller = ResolveController(manager);
        object runtime = GetRuntime(controller);
        object country = GetCountry(GetIntMember(controller, "TeamId"));
        Assert.That(country, Is.Not.Null);
        yield return WaitUntil(() => FindDiagnostic() != null, 10f, "DiagnosticoDesempenhoJogo nao apareceu.");

        yield return WaitUntil(() => !string.IsNullOrWhiteSpace(GetMetricText(FindDiagnostic(), "ia01_foundation_capital_cost")), 10f, "Custo da capital nao foi publicado.");
        yield return WaitUntil(() => GetStringMember(runtime, "FoundationFundingGrantedStatus") == "true", 10f, "Funding de fundacao nao foi protegido.");
        yield return WaitUntil(() => GetIntMember(GetMemberValue(runtime, "ConstructionGovernor"), "AvailableConstructionFundsAmount") > 0, 10f, "Fundos de construcao nao ficaram disponiveis.");
        yield return WaitUntil(() => GetStringMember(runtime, "ConstructionStateStatus") == "WaitingConfirmation"
                                   || GetMemberValue(GetMemberValue(runtime, "CityPlanner"), "Capital") != null,
            12f,
            "A fundacao nao entrou em confirmacao nem foi concluida.");

        // A reserva de emergencia so pode voltar a bloquear a abertura depois
        // de a capital ser confirmada pelo CityPlanner, nunca por um marcador
        // de construcao parcial.
        if (GetMemberValue(GetMemberValue(runtime, "CityPlanner"), "Capital") != null)
        {
            Assert.That(GetBoolMember(controller, "HasConfirmedCapital"), Is.True);
        }
        Assert.That(GetStringMember(runtime, "CapitalSourceStatus"), Is.Not.EqualTo("Missing"));
        Assert.That(GetStringMember(runtime, "LastFailureCodeStatus"), Is.Not.EqualTo("InsufficientFunds"));
        Assert.That(GetStringMember(GetMemberValue(runtime, "BuildDirector"), "BlockReasonStatus"), Is.Not.EqualTo("Funds"));
        Assert.That(GetLongMember(country, "saldo"), Is.GreaterThan(0L));
    }

    [UnityTest]
    public IEnumerator DiagnosticOverlay_DoesNotClaimNoIaData_WhenIa01MetricsExist()
    {
        yield return LoadFreshCampaign();
        yield return WaitUntil(() => FindManager() != null, 15f, "IA01Manager nao foi carregado.");
        yield return WaitUntil(() => FindDiagnostic() != null, 10f, "DiagnosticoDesempenhoJogo nao apareceu.");

        object diagnostic = FindDiagnostic();
        if (diagnostic is Component diagnosticComponent && !diagnosticComponent.gameObject.activeInHierarchy)
        {
            diagnosticComponent.gameObject.SetActive(true);
        }
        InvokeInstance(diagnostic, "SetCaptureMode", true, true);
        yield return WaitUntil(() => !string.IsNullOrWhiteSpace(GetMetricText(diagnostic, "ia01_progress")), 10f, "IA01 nao publicou progresso.");
        InvokeInstance(diagnostic, "ReconstruirLinhasOverlay");
        yield return WaitUntil(
            () => ContainsIgnoreCase(GetStringMember(diagnostic, "_overlayLine7"), "IA:"),
            5f,
            "Overlay nao publicou a secao de IA dentro do intervalo esperado.");

        string overlay = GetStringMember(diagnostic, "_overlayLine7");
        Assert.That(overlay, Does.Contain("IA:"));
        Assert.That(overlay, Does.Not.Contain("IA: sem dados ainda | BrainMaster nao publicou metricas nesta janela."));
    }

    private static IEnumerator LoadFreshCampaign()
    {
        yield return ResetPersistentIA01StateForTest();
        DeleteSaveFile();

        SceneManager.LoadScene(MenuSceneName);
        yield return null;
        yield return null;

        object menu = FindMenuController();
        if (menu == null)
        {
            yield return WaitUntil(() => FindMenuController() != null, 8f, "MenuInicialController nao apareceu.");
            menu = FindMenuController();
        }

        Assert.That(menu, Is.Not.Null, "MenuInicialController nao foi encontrado.");
        InvokeInstance(menu, "Btn_NovaCampanha");
        // O fluxo oficial abre a selecao de dificuldade antes de carregar a
        // cena canonica. Escolha explicitamente o perfil medio para que o
        // teste valide o mesmo caminho usado pelo jogador.
        yield return null;
        InvokeInstance(menu, "IniciarCampanhaSelecionada", "medio");

        // A cena canônica contém o mapa completo e pode levar mais de 15 s
        // para desserializar no editor. O limite continua finito, mas deixa
        // o teste distinguir carregamento pesado de falha de bootstrap.
        yield return WaitUntil(() => SceneManager.GetActiveScene().name != MenuSceneName, 180f, "A campanha nao carregou.");
        yield return null;
        yield return null;

        // A campanha deixa o componente Fps desativado por padrao para nao
        // pagar o custo de FrameTiming em cada partida. Os testes optam
        // explicitamente pela captura antes de validar as metricas da IA01.
        yield return WaitUntil(() => FindDiagnostic() != null, 10f, "DiagnosticoDesempenhoJogo nao apareceu.");
        object diagnostic = FindDiagnostic();
        if (diagnostic is Component diagnosticComponent && !diagnosticComponent.gameObject.activeInHierarchy)
        {
            diagnosticComponent.gameObject.SetActive(true);
        }
        InvokeInstance(diagnostic, "SetCaptureMode", true, false);

        object saveGame = GetStaticMemberValue(SaveGameType, "Instancia");
        Assert.That(saveGame, Is.Not.Null, "SistemaSaveGame nao inicializado.");
        Assert.That(Convert.ToBoolean(GetMemberValue(saveGame, "carregouDeSave")), Is.False, "A campanha nova nao deveria carregar o save antigo.");
    }

    private static IEnumerator ResetPersistentIA01StateForTest()
    {
        DestroyRuntimeObjectsOfType(ManagerType);
        DestroyRuntimeObjectsOfType(ControllerType);
        yield return null;
    }

    private static void DestroyRuntimeObjectsOfType(Type type)
    {
        if (type == null)
        {
            return;
        }

        UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
        for (int i = 0; i < objects.Length; i++)
        {
            Component component = objects[i] as Component;
            if (component == null || component.gameObject == null || !component.gameObject.scene.IsValid())
            {
                continue;
            }

            UnityEngine.Object.Destroy(component.gameObject);
        }
    }

    private static object ResolveController(object manager)
    {
        Assert.That(manager, Is.Not.Null, "IA01Manager nao foi encontrado.");

        // A campanha oficial usa o team 2 como IA adversária; o team 1 é o
        // país do jogador e não deve ser criado como controller IA01 de teste.
        object controller = InvokeInstance(manager, "FindControllerByTeamId", 2);
        if (controller == null)
        {
            object controllers = GetMemberValue(manager, "Controllers");
            if (controllers != null)
            {
                IEnumerator enumerator = EnumerateCollection(controllers).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current != null)
                    {
                        controller = enumerator.Current;
                        break;
                    }
                }
            }
        }

        if (controller == null)
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object country = GetCountry(1) ?? GetFirstCountry();
            if (country != null)
            {
                controller = InvokeInstance(manager, "CreateControllerFromGovernment", country);
            }
        }

        Assert.That(controller, Is.Not.Null, "Nao foi possivel obter um controller IA01 valido.");
        return controller;
    }

    private static object EnsureSecondController(object manager, object primaryController)
    {
        Assert.That(manager, Is.Not.Null);
        Assert.That(primaryController, Is.Not.Null);

        object secondary = null;
        object controllers = GetMemberValue(manager, "Controllers");
        if (controllers != null)
        {
            foreach (object candidate in EnumerateCollection(controllers))
            {
                if (candidate != null && !ReferenceEquals(candidate, primaryController))
                {
                    secondary = candidate;
                    break;
                }
            }
        }

        if (secondary == null)
        {
            InvokeStatic(GovernmentSystemType, "GarantirInstancia");
            object source = GetFirstCountryExcept(GetIntMember(primaryController, "TeamId")) ?? GetFirstCountry();
            Assert.That(source, Is.Not.Null, "Nao ha pais suficiente para criar a segunda IA.");

            Type sourceType = source.GetType();
            object clone;
            if (typeof(ScriptableObject).IsAssignableFrom(sourceType))
            {
                clone = ScriptableObject.CreateInstance(sourceType);
            }
            else
            {
                clone = Activator.CreateInstance(sourceType);
            }

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            SetMemberValue(clone, "teamId", GetNextFreeTeamId());
            SetMemberValue(clone, "nomePais", GetStringMember(source, "nomePais") + " II");
            SetMemberValue(clone, "nomePresidente", GetStringMember(source, "nomePresidente") + " II");
            SetMemberValue(clone, "saldo", GetMemberValue(source, "saldo"));
            SetMemberValue(clone, "comida", GetMemberValue(source, "comida"));
            SetMemberValue(clone, "energia", GetMemberValue(source, "energia"));

            object government = GetStaticMemberValue(GovernmentSystemType, "Instancia");
            object countries = GetMemberValue(government, "paises");
            Assert.That(countries, Is.Not.Null, "Lista de paises nao foi encontrada.");
            ((IList)countries).Add(clone);
            secondary = InvokeInstance(manager, "CreateControllerFromGovernment", clone);
        }

        Assert.That(secondary, Is.Not.Null, "Nao foi possivel criar a segunda IA.");
        return secondary;
    }

    private static object FindManager()
    {
        // Durante as trocas de cena pode existir um manager local marcado para
        // destruicao. A autoridade e o singleton persistente da partida.
        object manager = GetStaticMemberValue(ManagerType, "Instancia");
        if (manager == null)
        {
            manager = FindFirstObjectOfType(ManagerType);
        }

        return manager;
    }

    private static object FindMenuController()
    {
        return FindFirstObjectOfType(MenuControllerType);
    }

    private static object FindDiagnostic()
    {
        object persistent = GetStaticMemberValue(DiagnosticType, "_instancia");
        if (persistent is UnityEngine.Object unityObject && unityObject == null)
        {
            persistent = null;
        }

        if (persistent != null)
        {
            return persistent;
        }

        return FindFirstObjectOfType(DiagnosticType);
    }

    private static object GetRuntime(object controller)
    {
        Assert.That(controller, Is.Not.Null, "Controller nulo.");
        FieldInfo field = ControllerType.GetField("nationRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo nationRuntime nao encontrado.");
        return field.GetValue(controller);
    }

    private static void AssertSafeProfileDefaults(object controller)
    {
        Assert.That(controller, Is.Not.Null, "Perfil da IA nao foi inicializado.");
        object profile = GetMemberValue(controller, "Profile");
        Assert.That(profile, Is.Not.Null, "Perfil da IA nao foi inicializado.");
        object settings = GetMemberValue(profile, "ConstructionGovernor");
        Assert.That(settings, Is.Not.Null, "ConstructionGovernor nao foi inicializado.");

        Assert.That(GetIntMember(settings, "EmergencyReserve"), Is.GreaterThan(0));
        Assert.That(GetIntMember(settings, "MinimumConstructionReserve"), Is.GreaterThan(0));
        Assert.That(GetFloatMember(settings, "MaximumConstructionBudgetPercent"), Is.GreaterThan(0f));
        Assert.That(GetFloatMember(settings, "MaximumMaintenancePercent"), Is.GreaterThan(0f));
        Assert.That(GetFloatMember(settings, "MinimumAcceptableFps"), Is.GreaterThan(0f));
        Assert.That(GetFloatMember(settings, "MaxIaFrameBudgetMs"), Is.GreaterThan(0f));
        Assert.That(GetFloatMember(settings, "MaxBuildPlannerBudgetMs"), Is.GreaterThan(0f));
        Assert.That(GetIntMember(settings, "MaxCandidatesPerSlice"), Is.GreaterThan(0));
        Assert.That(GetIntMember(settings, "MaxPhysicsChecksPerSlice"), Is.GreaterThan(0));
    }

    private static void AddTransition(List<string> transitions, string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return;
        }

        if (transitions.Count == 0 || !string.Equals(transitions[transitions.Count - 1], state, StringComparison.Ordinal))
        {
            transitions.Add(state);
        }
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string failureMessage)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (condition())
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail(failureMessage);
    }

    private static IEnumerator WaitSecondsRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    private static void DeleteSaveFile()
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void SetPopulationSource(object country, int total, int housingCapacity, float stability, float happiness)
    {
        SetMemberValue(country, "populacao", total);
        SetMemberValue(country, "populacaoCivil", total);
        SetMemberValue(country, "populacaoMilitarAtiva", 0);
        SetMemberValue(country, "reservistas", 0);
        SetMemberValue(country, "alistaveis", total);
        SetMemberValue(country, "populacaoMaxima", housingCapacity);
        SetMemberValue(country, "estabilidade", stability);
        SetMemberValue(country, "felicidade", happiness);
    }

    private static void AssertHudMetric(object diagnostic, string key)
    {
        string value = GetMetricText(diagnostic, key);
        Assert.That(string.IsNullOrWhiteSpace(value), Is.False, "HUD nao publicou " + key + ".");
    }

    private static bool HasHudMetrics(object diagnostic, string[] keys)
    {
        if (diagnostic == null || keys == null)
        {
            return false;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(GetMetricText(diagnostic, keys[i])))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetMetricText(object diagnostic, string key)
    {
        return InvokePrivateMetric<string>(diagnostic, "ObterTextoMetrica", key);
    }

    private static string GetMetricTimeText(object diagnostic, string key)
    {
        float value = InvokePrivateMetric<float>(diagnostic, "ObterTempoMetrica", key);
        return value.ToString("0.00");
    }

    private static int GetMetricCount(object diagnostic, string key)
    {
        return InvokePrivateMetric<int>(diagnostic, "ObterContadorMetrica", key);
    }

    private static T InvokePrivateMetric<T>(object diagnostic, string methodName, string key)
    {
        MethodInfo method = DiagnosticType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Nao achei o metodo " + methodName + ".");
        object value = method.Invoke(diagnostic, new object[] { key });
        if (value is T typed)
        {
            return typed;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static IA01PerfSummary ReadPerfSummary(object diagnostic)
    {
        FieldInfo field = DiagnosticType.GetField("_ultimoResumo", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Nao achei o resumo de desempenho.");
        object summary = field.GetValue(diagnostic);
        Assert.That(summary, Is.Not.Null, "Resumo de desempenho nulo.");
        Type summaryType = summary.GetType();
        return new IA01PerfSummary
        {
            FpsMedio = Convert.ToSingle(summaryType.GetField("FpsMedio", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            FpsMinimo = Convert.ToSingle(summaryType.GetField("FpsMinimo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            CpuMainMs = Convert.ToSingle(summaryType.GetField("CpuMainMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            GpuMs = Convert.ToSingle(summaryType.GetField("GpuMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            GcGen0 = Convert.ToInt32(summaryType.GetField("GcGen0", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            GcGen1 = Convert.ToInt32(summaryType.GetField("GcGen1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary)),
            GcGen2 = Convert.ToInt32(summaryType.GetField("GcGen2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(summary))
        };
    }

    private static object GetCountry(int teamId)
    {
        InvokeStatic(GovernmentSystemType, "GarantirInstancia");
        object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
        if (system == null)
        {
            return null;
        }

        object country = InvokeInstance(system, "ObterPais", teamId);
        if (country != null)
        {
            return country;
        }

        object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
        if (countries != null)
        {
            foreach (object candidate in EnumerateCollection(countries))
            {
                if (candidate != null && GetIntMember(candidate, "teamId") == teamId)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static object GetFirstCountry()
    {
        InvokeStatic(GovernmentSystemType, "GarantirInstancia");
        object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
        if (system == null)
        {
            return null;
        }

        object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
        if (countries == null)
        {
            return null;
        }

        foreach (object country in EnumerateCollection(countries))
        {
            if (country != null)
            {
                return country;
            }
        }

        return null;
    }

    private static object GetFirstCountryExcept(int teamId)
    {
        InvokeStatic(GovernmentSystemType, "GarantirInstancia");
        object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
        if (system == null)
        {
            return null;
        }

        object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
        if (countries == null)
        {
            return null;
        }

        foreach (object country in EnumerateCollection(countries))
        {
            if (country != null && GetIntMember(country, "teamId") != teamId)
            {
                return country;
            }
        }

        return null;
    }

    private static int GetNextFreeTeamId()
    {
        InvokeStatic(GovernmentSystemType, "GarantirInstancia");
        object system = GetStaticMemberValue(GovernmentSystemType, "Instancia");
        int maxTeamId = 1;
        if (system != null)
        {
            object countries = GetMemberValue(system, "Paises") ?? GetMemberValue(system, "paises");
            if (countries != null)
            {
                foreach (object country in EnumerateCollection(countries))
                {
                    if (country != null)
                    {
                        maxTeamId = Mathf.Max(maxTeamId, GetIntMember(country, "teamId"));
                    }
                }
            }
        }

        return maxTeamId + 1;
    }

    private static IEnumerable EnumerateCollection(object collection)
    {
        if (collection is IEnumerable enumerable)
        {
            return enumerable;
        }

        throw new InvalidOperationException("Objeto nao e enumeravel: " + collection.GetType().Name);
    }

    private static object FindFirstObjectOfType(Type targetType)
    {
        if (targetType == null)
        {
            return null;
        }

        UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
        object fallback = null;
        object sceneFallback = null;
        for (int i = 0; i < objects.Length; i++)
        {
            UnityEngine.Object candidate = objects[i];
            if (candidate == null || !targetType.IsInstanceOfType(candidate))
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (candidate is Component component && component.gameObject.scene.IsValid())
            {
                if (component.gameObject.activeInHierarchy)
                {
                    return candidate;
                }

                if (sceneFallback == null)
                {
                    sceneFallback = candidate;
                }
            }
        }

        return sceneFallback ?? fallback;
    }

    private static object GetMemberValue(object instance, string memberName)
    {
        if (instance == null)
        {
            return null;
        }

        Type type = instance.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance);
            }

            type = type.BaseType;
        }

        return null;
    }

    private static object GetStaticMemberValue(Type type, string memberName)
    {
        if (type == null)
        {
            return null;
        }

        Type current = type;
        while (current != null)
        {
            FieldInfo field = current.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(null);
            }

            PropertyInfo property = current.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(null);
            }

            current = current.BaseType;
        }

        return null;
    }

    private static void SetMemberValue(object instance, string memberName, object value)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao definir " + memberName + ".");
        Type type = instance.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
                return;
            }

            type = type.BaseType;
        }

        Assert.Fail("Nao achei o membro " + memberName + " em " + instance.GetType().Name + ".");
    }

    private static object InvokeInstance(object instance, string methodName, params object[] args)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao invocar " + methodName + ".");
        MethodInfo method = FindMethod(instance.GetType(), methodName, false, args);
        Assert.That(method, Is.Not.Null, "Nao achei o metodo " + methodName + " em " + instance.GetType().Name + ".");
        return method.Invoke(instance, args);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] args)
    {
        Assert.That(type, Is.Not.Null, "Tipo nulo ao invocar " + methodName + ".");
        MethodInfo method = FindMethod(type, methodName, true, args);
        Assert.That(method, Is.Not.Null, "Nao achei o metodo statico " + methodName + " em " + type.Name + ".");
        return method.Invoke(null, args);
    }

    private static MethodInfo FindMethod(Type type, string methodName, bool isStatic, params object[] args)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        MethodInfo[] methods = type.GetMethods(flags);
        int argCount = args != null ? args.Length : 0;

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != argCount)
            {
                continue;
            }

            bool match = true;
            for (int p = 0; p < parameters.Length; p++)
            {
                object arg = args[p];
                if (arg == null)
                {
                    if (parameters[p].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[p].ParameterType) == null)
                    {
                        match = false;
                        break;
                    }
                }
                else if (!parameters[p].ParameterType.IsAssignableFrom(arg.GetType()))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return method;
            }
        }

        return null;
    }

    private static string GetStringMember(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null ? value.ToString() : string.Empty;
    }

    private static int GetIntMember(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null ? Convert.ToInt32(value) : 0;
    }

    private static long GetLongMember(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null ? Convert.ToInt64(value) : 0L;
    }

    private static float GetFloatMember(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null ? Convert.ToSingle(value) : 0f;
    }

    private static bool GetBoolMember(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null && Convert.ToBoolean(value);
    }

    private static int GetCollectionCount(object collection)
    {
        if (collection == null)
        {
            return 0;
        }

        if (collection is ICollection genericCollection)
        {
            return genericCollection.Count;
        }

        object count = GetMemberValue(collection, "Count");
        return count != null ? Convert.ToInt32(count) : 0;
    }

    private static string GetEnumMemberName(object instance, string memberName)
    {
        object value = GetMemberValue(instance, memberName);
        return value != null ? value.ToString() : string.Empty;
    }

    private static string GetEnumMemberNameFromType(Type type, string memberName)
    {
        object value = GetStaticMemberValue(type, memberName);
        return value != null ? value.ToString() : string.Empty;
    }

    private static Type ResolveType(string typeName)
    {
        lock (TypeCache)
        {
            if (TypeCache.TryGetValue(typeName, out Type cached))
            {
                return cached;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == null)
                {
                    continue;
                }

                try
                {
                    Type type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        TypeCache[typeName] = type;
                        return type;
                    }

                    Type[] assemblyTypes = assembly.GetTypes();
                    for (int i = 0; i < assemblyTypes.Length; i++)
                    {
                        Type candidate = assemblyTypes[i];
                        if (candidate == null)
                        {
                            continue;
                        }

                        if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                            || string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                        {
                            TypeCache[typeName] = candidate;
                            return candidate;
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.Types != null)
                    {
                        for (int i = 0; i < ex.Types.Length; i++)
                        {
                            Type candidate = ex.Types[i];
                            if (candidate == null)
                            {
                                continue;
                            }

                            if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                                || string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                            {
                                TypeCache[typeName] = candidate;
                                return candidate;
                            }
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException("Nao foi possivel resolver o tipo " + typeName + ".");
    }

    private static bool ContainsIgnoreCase(string text, string fragment)
    {
        return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(fragment) && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private sealed class IA01ScenarioContext
    {
        public IA01ScenarioContext(object manager, object controller, object runtime)
        {
            Manager = manager;
            Controller = controller;
            Runtime = runtime;
        }

        public object Manager { get; }
        public object Controller { get; }
        public object Runtime { get; }
    }

    private struct IA01PerfSummary
    {
        public float FpsMedio;
        public float FpsMinimo;
        public float CpuMainMs;
        public float GpuMs;
        public int GcGen0;
        public int GcGen1;
        public int GcGen2;
    }
}
