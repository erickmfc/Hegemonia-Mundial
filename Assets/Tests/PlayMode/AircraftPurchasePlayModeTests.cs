using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AircraftPurchasePlayModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private Component originalResourcesManager;
    private long originalResourceBalance;
    private float originalMoneyPerSecond;
    private bool originalAutomaticIncome;

    private object originalGovernmentInstance;
    private object isolatedGovernmentInstance;
    private MethodInfo governmentInstanceSetter;
    private bool governmentSingletonWasReplaced;

    private object originalGovernorInstance;
    private MethodInfo governorInstanceSetter;
    private bool governorSingletonWasReplaced;

    private Component testIndustrialManager;
    private object industrialSaveBeforeTest;
    private bool industrialSnapshotCaptured;

    private object productionRegistrySnapshot;
    private bool productionRegistrySnapshotCaptured;

    private Component originalConstructionMenu;
    private Component originalMenuManager;
    private GameObject originalMenuPanel;
    private Component originalMenuCanvasGroup;
    private object originalAircraftCatalog;
    private bool aircraftCatalogOverridden;
    private readonly List<Component> airportsRegisteredBeforeIsolation = new List<Component>();
    private bool airportRegistryIsolated;

    private static Type TypeOf(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Members).SetValue(target, value);

    private static object Read(object target, string name) =>
        target.GetType().GetField(name, Members).GetValue(target);

    private static string Localize(string key, string fallback)
    {
        Type type = TypeOf("LocalizationManager");
        MethodInfo method = type.GetMethod("T", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
        return (string)method.Invoke(null, new object[] { key, fallback });
    }

    private static void MostrarMensagemHUD(string msg, float duracao)
    {
        Type type = TypeOf("HUDAjudaRTS");
        MethodInfo method = type.GetMethod("MostrarMensagemTemporaria", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(float) }, null);
        if (method != null) method.Invoke(null, new object[] { msg, duracao });
    }

    private Component Create(string name)
    {
        var go = new GameObject("Test_" + name);
        go.SetActive(false);
        objects.Add(go);
        return go.AddComponent(TypeOf(name));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (aircraftCatalogOverridden)
        {
            Type menuType = TypeOf("MenuConstrucao");
            FieldInfo catalogField = menuType.GetField("catalogoGlobal", BindingFlags.Static | BindingFlags.Public);
            if (catalogField != null) catalogField.SetValue(null, originalAircraftCatalog);
            originalAircraftCatalog = null;
            aircraftCatalogOverridden = false;
        }

        if (originalResourcesManager != null)
        {
            Set(originalResourcesManager, "dinheiro", originalResourceBalance);
            Set(originalResourcesManager, "dinheiroPorSegundo", originalMoneyPerSecond);
            Set(originalResourcesManager, "ativarGanhosAutomaticos", originalAutomaticIncome);
        }

        if (originalConstructionMenu != null)
        {
            Set(originalConstructionMenu, "gerente", originalMenuManager);
            Set(originalConstructionMenu, "painelPrincipal", originalMenuPanel);
            Set(originalConstructionMenu, "canvasGroupPainel", originalMenuCanvasGroup);
        }

        foreach (var go in objects)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        objects.Clear();

        foreach (var asset in assets)
        {
            if (asset != null) UnityEngine.Object.Destroy(asset);
        }
        assets.Clear();

        yield return null;

        if (productionRegistrySnapshotCaptured)
        {
            Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
            MethodInfo restoreRegistry = registryType.GetMethod("RestoreSaveData", BindingFlags.Static | BindingFlags.Public);
            restoreRegistry.Invoke(null, new[] { productionRegistrySnapshot });
            productionRegistrySnapshot = null;
            productionRegistrySnapshotCaptured = false;
        }

        if (airportRegistryIsolated)
        {
            Type registryType = TypeOf("RegistroEntidadesJogo");
            MethodInfo registerAirport = registryType.GetMethod(
                "Register",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { TypeOf("GerenciadorAeroporto") },
                null);
            for (int i = 0; i < airportsRegisteredBeforeIsolation.Count; i++)
            {
                Component airport = airportsRegisteredBeforeIsolation[i];
                if (airport != null && airport.gameObject.activeInHierarchy)
                {
                    registerAirport.Invoke(null, new[] { airport });
                }
            }

            airportsRegisteredBeforeIsolation.Clear();
            airportRegistryIsolated = false;
        }

        if (governmentSingletonWasReplaced)
        {
            governmentInstanceSetter.Invoke(null, new[] { originalGovernmentInstance });
            governmentSingletonWasReplaced = false;
            originalGovernmentInstance = null;
            governmentInstanceSetter = null;
        }

        if (governorSingletonWasReplaced)
        {
            governorInstanceSetter.Invoke(null, new[] { originalGovernorInstance });
            governorSingletonWasReplaced = false;
            originalGovernorInstance = null;
            governorInstanceSetter = null;
        }

        if (industrialSnapshotCaptured && testIndustrialManager != null)
        {
            MethodInfo restoreIndustrial = testIndustrialManager.GetType().GetMethod("RestaurarSaveData", Members);
            restoreIndustrial.Invoke(testIndustrialManager, new[] { industrialSaveBeforeTest });

            Type marketType = TypeOf("SistemaMercadoGlobal");
            object market = marketType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null, null);
            if (market != null)
            {
                Type integrationType = TypeOf("IntegracaoMercadoIndustrial");
                MethodInfo syncMarket = integrationType.GetMethod(
                    "SincronizarEstoquesNoMercado",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { TypeOf("SistemaIndustrialNacional"), marketType },
                    null);
                syncMarket.Invoke(null, new[] { testIndustrialManager, market });
            }

            industrialSnapshotCaptured = false;
            industrialSaveBeforeTest = null;
            testIndustrialManager = null;
        }

    }

    private Terrain CreateRaisedLandFixture()
    {
        var data = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(100f, 30f, 100f)
        };
        assets.Add(data);
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        {
            for (int x = 0; x < 33; x++)
            {
                heights[z, x] = 1f;
            }
        }
        data.SetHeights(0, 0, heights);
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Test_AirportLand";
        objects.Add(go);
        return go.GetComponent<Terrain>();
    }

    private Component CreatePlayerAirport(Vector3 position)
    {
        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Test_Airport";
        airport.transform.position = position;
        Component identity = airport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        airport.gameObject.SetActive(true);
        return airport;
    }

    private Component CreateLogisticsPier(string label, int teamId, Vector3 position)
    {
        Component pier = Create("PierMarinha");
        pier.name = "Test_FullRoute_Pier_" + label;
        pier.transform.position = position;
        Component identity = pier.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", teamId);
        Set(identity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Estrutura"));

        GameObject entrance = new GameObject("Test_FullRoute_PierEntrance_" + label);
        objects.Add(entrance);
        entrance.transform.SetParent(pier.transform, false);
        entrance.transform.localPosition = Vector3.zero;
        Set(pier, "pontoEntrada", entrance.transform);
        PropertyInfo owner = pier.GetType().GetProperty("OwnerTeamId", Members);
        Assert.IsNotNull(owner, "O pier deve expor o proprietário logístico.");
        owner.SetValue(pier, teamId, null);
        pier.gameObject.SetActive(true);
        return pier;
    }

    private void IsolateAirportRegistry()
    {
        Type registryType = TypeOf("RegistroEntidadesJogo");
        Type airportType = TypeOf("GerenciadorAeroporto");
        MethodInfo fillAirports = registryType.GetMethod(
            "FillAeroportos",
            BindingFlags.Static | BindingFlags.Public);
        MethodInfo unregisterAirport = registryType.GetMethod(
            "Unregister",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { airportType },
            null);
        IList registered = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(airportType));
        fillAirports.Invoke(null, new object[] { registered });
        for (int i = 0; i < registered.Count; i++)
        {
            Component airport = registered[i] as Component;
            if (airport == null) continue;
            airportsRegisteredBeforeIsolation.Add(airport);
            unregisterAirport.Invoke(null, new object[] { airport });
        }

        airportRegistryIsolated = true;
    }

    private void PrepareIsolatedPlayerGovernment(int availableEnlistables)
    {
        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        originalGovernmentInstance = instanceProperty.GetValue(null, null);
        governmentInstanceSetter = instanceProperty.GetSetMethod(true);
        governmentSingletonWasReplaced = true;

        GameObject governmentRoot = new GameObject("Aircraft purchase manpower test government");
        governmentRoot.SetActive(false);
        objects.Add(governmentRoot);
        Component government = governmentRoot.AddComponent(governmentType);
        isolatedGovernmentInstance = government;

        IList countries = (IList)governmentType.GetField("paises", Members).GetValue(government);
        object country = Activator.CreateInstance(countryType);
        Set(country, "teamId", 1);
        Set(country, "alistaveis", availableEnlistables);
        Set(country, "populacaoCivil", 0);
        Set(country, "populacaoMilitarAtiva", 0);
        countries.Add(country);
    }

    private void TrackAirportAircraft(Component airport)
    {
        foreach (string fieldName in new[] { "avioesNoPatio", "avioesNoHangar" })
        {
            IList aircraftList = Read(airport, fieldName) as IList;
            if (aircraftList == null) continue;

            foreach (object entry in aircraftList)
            {
                Component aircraft = entry as Component;
                if (aircraft != null && !objects.Contains(aircraft.gameObject))
                {
                    objects.Add(aircraft.gameObject);
                }
            }
        }
    }

    private Component PrepareResources(long balance)
    {
        Type type = TypeOf("GerenciadorRecursos");
        originalResourcesManager = type.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null) as Component;

        if (originalResourcesManager != null)
        {
            originalResourceBalance = (long)Read(originalResourcesManager, "dinheiro");
            originalMoneyPerSecond = (float)Read(originalResourcesManager, "dinheiroPorSegundo");
            originalAutomaticIncome = (bool)Read(originalResourcesManager, "ativarGanhosAutomaticos");
            Set(originalResourcesManager, "dinheiro", balance);
            Set(originalResourcesManager, "dinheiroPorSegundo", 0f);
            Set(originalResourcesManager, "ativarGanhosAutomaticos", false);
            return originalResourcesManager;
        }

        Component resources = Create("GerenciadorRecursos");
        resources.gameObject.SetActive(true);
        Set(resources, "dinheiro", balance);
        Set(resources, "dinheiroPorSegundo", 0f);
        Set(resources, "ativarGanhosAutomaticos", false);
        return resources;
    }

    private void PrepareEmptyAviationFuelStock()
    {
        Type governmentType = TypeOf("SistemaGovernoMundial");
        MethodInfo ensureGovernment = governmentType.GetMethod(
            "GarantirInstancia",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        ensureGovernment.Invoke(null, null);

        Type industrialType = TypeOf("SistemaIndustrialNacional");
        testIndustrialManager = industrialType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null) as Component;
        Assert.IsNotNull(testIndustrialManager, "O sistema industrial deve existir para testar o estoque real de combustível.");
        industrialSaveBeforeTest = industrialType.GetMethod("CriarSaveData", Members)
            .Invoke(testIndustrialManager, null);
        industrialSnapshotCaptured = industrialSaveBeforeTest != null;
        Assert.IsTrue(industrialSnapshotCaptured, "O estado industrial precisa ser salvo antes de zerar o estoque.");

        string aviationFuelId = (string)TypeOf("IndustriaIds")
            .GetField("CombustivelAviacao", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(null);
        MethodInfo getStock = industrialType.GetMethod(
            "ObterQuantidadePais",
            Members,
            null,
            new[] { typeof(int), typeof(string) },
            null);
        MethodInfo consumeStock = industrialType.GetMethod(
            "TentarConsumir",
            Members,
            null,
            new[] { typeof(string), typeof(string), typeof(double) },
            null);

        double stock = Convert.ToDouble(getStock.Invoke(testIndustrialManager, new object[] { 1, aviationFuelId }));
        if (stock > 0.01d)
        {
            bool consumed = (bool)consumeStock.Invoke(
                testIndustrialManager,
                new object[] { "1", aviationFuelId, stock });
            Assert.IsTrue(consumed, "A fixture precisa conseguir zerar apenas o estoque de combustível aéreo do jogador.");
        }

        Assert.AreEqual(0d, Convert.ToDouble(getStock.Invoke(testIndustrialManager, new object[] { 1, aviationFuelId })), 0.01d,
            "O estoque de combustível de aviação do jogador deve estar zerado antes da compra.");
    }

    private Component PrepareConstructionMenu(Component manager)
    {
        Type type = TypeOf("MenuConstrucao");
        originalConstructionMenu = type.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null) as Component;
        Component menu = originalConstructionMenu;

        if (menu != null)
        {
            originalMenuManager = (Component)Read(menu, "gerente");
            originalMenuPanel = (GameObject)Read(menu, "painelPrincipal");
            originalMenuCanvasGroup = (Component)Read(menu, "canvasGroupPainel");
        }
        else
        {
            menu = Create("MenuConstrucao");
        }

        Set(menu, "gerente", manager);
        if (Read(menu, "painelPrincipal") == null || Read(menu, "canvasGroupPainel") == null)
        {
            var panel = new GameObject("Test_ConstructionPanel");
            objects.Add(panel);
            Component group = panel.AddComponent<CanvasGroup>();
            Set(menu, "painelPrincipal", panel);
            Set(menu, "canvasGroupPainel", group);
        }
        if (!menu.gameObject.activeSelf) menu.gameObject.SetActive(true);
        return menu;
    }

    private ScriptableObject CreateAircraftCard(GameObject prefab, long price)
    {
        ScriptableObject item = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        assets.Add(item);
        item.GetType().GetProperty("NomeItem").SetValue(item, "Jato de Teste", null);
        item.GetType().GetProperty("PrefabDaUnidade").SetValue(item, prefab, null);
        Set(item, "precoDefinitivo", price);
        return item;
    }

    private int ObterTotalAeronaves(Component airport)
    {
        var patio = (IList)Read(airport, "avioesNoPatio");
        var hangar = (IList)Read(airport, "avioesNoHangar");
        return (patio != null ? patio.Count : 0) + (hangar != null ? hangar.Count : 0);
    }

    [UnityTest]
    public IEnumerator Test1_LocalAirportPurchase_WithoutPower_DoesNotDebitOrSpawn_AndNotifies()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", true);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test1_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        // A rede global pode recalcular aeroportos no primeiro frame. Reaplique
        // e valide a ausência de energia para isolar esse gate de forma explícita.
        Set(airport, "semEnergia", true);

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        string localizedMessage = Localize("airport.no_power_purchase", "fallback");
        Assert.AreNotEqual("fallback", localizedMessage, "A chave 'airport.no_power_purchase' deve estar cadastrada.");
        Assert.IsTrue(localizedMessage.Contains("ENERGIA") || localizedMessage.Contains("POWER") || localizedMessage.Contains("电力"),
            "A mensagem deve informar sobre ausencia de energia.");

        bool semEnergiaAtual = (bool)Read(airport, "semEnergia");
        Assert.IsTrue(semEnergiaAtual, "Este caso precisa manter o aeroporto sem energia.");
        bool compraExecutada = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, (long)precoAeronave });

        if (semEnergiaAtual)
        {
            MostrarMensagemHUD(Localize("airport.no_power_purchase", "AEROPORTO SEM ENERGIA\nConstrua uma usina para reativa-lo."), 4.5f);
        }

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Assert.IsFalse(compraExecutada, "Compra local nao deve ser executada quando sem energia.");
        Assert.AreEqual(saldoAntes, saldoDepois, "0 debito: Saldo antes deve ser igual ao saldo depois.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "0 spawn: Quantidade antes deve ser igual a depois.");
    }

    [UnityTest]
    public IEnumerator Test2_LocalAirportPurchase_InsufficientFunds_DoesNotDebitOrSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 500L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test2_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        // A rede global pode recalcular aeroportos no primeiro frame. Reaplique a
        // condição para que este caso isole somente a insuficiência de tesouro.
        Set(airport, "semEnergia", false);

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        bool compraExecutada = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, (long)precoAeronave });

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Assert.IsFalse(compraExecutada, "Compra nao deve ser executada com saldo insuficiente.");
        Assert.AreEqual(saldoAntes, saldoDepois, "0 debito: saldo permanece inalterado.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "0 spawn: nenhuma aeronave criada.");
        Assert.GreaterOrEqual(saldoDepois, 0, "Saldo nao pode ficar negativo.");
    }

    [UnityTest]
    public IEnumerator Test3_LocalAirportPurchase_ValidConditions_DebitsExactlyOnceAndSpawns()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test3_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        bool semEnergiaAposRecalculo = (bool)Read(airport, "semEnergia");
        bool governoOriginalPresente = originalGovernmentInstance != null;
        long manpowerOriginal = governoOriginalPresente
            ? (long)TypeOf("SistemaGovernoMundial").GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
                .Invoke(originalGovernmentInstance, new object[] { 1 })
            : -1L;
        long reservaOriginal = governoOriginalPresente
            ? (long)TypeOf("SistemaGovernoMundial").GetMethod("ObterReservaManpowerEstrategica", Members)
                .Invoke(originalGovernmentInstance, new object[] { 1 })
            : -1L;
        bool governoOriginalMobilizaDois = governoOriginalPresente
            && (bool)TypeOf("SistemaGovernoMundial").GetMethod("PodeMobilizarEfetivo", Members)
                .Invoke(originalGovernmentInstance, new object[] { 1, 2 });
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);
        int notificacoesDeRecursos = 0;
        Action contarAtualizacao = () => notificacoesDeRecursos++;
        EventInfo eventoRecursosAtualizados = TypeOf("GerenciadorRecursos").GetEvent("OnRecursosAtualizados", Members);
        eventoRecursosAtualizados.AddEventHandler(resources, contarAtualizacao);

        long saldoAtual = (long)Read(resources, "dinheiro");
        Assert.GreaterOrEqual(saldoAtual, precoAeronave, "Saldo deve ser suficiente.");

        Type governmentType = TypeOf("SistemaGovernoMundial");
        object government = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
        long manpowerDisponivel = government != null
            ? (long)governmentType.GetMethod("ObterManpowerDisponivelParaMobilizacao", Members).Invoke(government, new object[] { 1 })
            : -1L;
        long reservaManpower = government != null
            ? (long)governmentType.GetMethod("ObterReservaManpowerEstrategica", Members).Invoke(government, new object[] { 1 })
            : -1L;
        bool podeMobilizarDois = government != null
            && (bool)governmentType.GetMethod("PodeMobilizarEfetivo", Members).Invoke(government, new object[] { 1, 2 });
        int efetivoDeclaradoNoPrefab = (int)governmentType.GetMethod(
            "ObterEfetivoNecessario",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(GameObject) },
            null).Invoke(null, new object[] { aircraftPrefab });
        bool precheckAeroporto = (bool)airport.GetType().GetMethod("PodeProduzirAeronave", Members)
            .Invoke(airport, new object[] { aircraftPrefab });
        Debug.Log($"[TESTE 3 DIAGNOSTICO] semEnergiaAposRecalculo={semEnergiaAposRecalculo}, semEnergiaAntesCompra={Read(airport, "semEnergia")}, recursosPresentes={resources != null}, saldo={saldoAtual}, governoOriginalPresente={governoOriginalPresente}, manpowerOriginalT1={manpowerOriginal}, reservaOriginalT1={reservaOriginal}, governoOriginalMobiliza2={governoOriginalMobilizaDois}, governoIsoladoPresente={government != null}, manpowerIsoladoT1={manpowerDisponivel}, reservaIsoladaT1={reservaManpower}, governoIsoladoMobiliza2={podeMobilizarDois}, efetivoPrefab={efetivoDeclaradoNoPrefab}, precheckAeroporto={precheckAeroporto}");

        bool comprado;
        try
        {
            comprado = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
                .Invoke(airport, new object[] { aircraftPrefab, (long)precoAeronave });
        }
        finally
        {
            eventoRecursosAtualizados.RemoveEventHandler(resources, contarAtualizacao);
        }

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);
        TrackAirportAircraft(airport);

        Debug.Log($"[TESTE 3 REGISTRO] Saldo Antes: {saldoAntes}, Preco: {precoAeronave}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}");

        Assert.IsTrue(comprado, "O metodo ComprarAviao deve retornar sucesso.");
        Assert.AreEqual(saldoAntes - precoAeronave, saldoDepois, "Exatamente 1 debito realizado.");
        Assert.AreEqual(quantidadeAntes + 1, quantidadeDepois, "Exatamente 1 aeronave criada.");
        Assert.AreEqual(1, notificacoesDeRecursos,
            "A compra deve notificar o ledger e sincronizadores nacionais uma única vez.");
    }

    [UnityTest]
    public IEnumerator Test3b_LocalAirportPurchase_AtNationalAircraftLimit_IsRejectedWithoutDebitOrSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(4);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);

        Type governorType = TypeOf("GovernadorGameplayRTS");
        PropertyInfo instanceProperty = governorType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        originalGovernorInstance = instanceProperty.GetValue(null, null);
        governorInstanceSetter = instanceProperty.GetSetMethod(true);
        governorSingletonWasReplaced = true;
        governorInstanceSetter.Invoke(null, new object[] { null });
        GameObject governorRoot = new GameObject("Airport national production limit test governor");
        objects.Add(governorRoot);
        Component governor = governorRoot.AddComponent(governorType);
        Set(governor, "limiteTotal", 20);
        Set(governor, "limiteAereos", 1);
        Set(governor, "bloquearProducaoQuandoSaturado", false);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test3b_LimitedAircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", 1500);

        GameObject aircraftAlreadyInField = new GameObject("Test3b_AircraftAlreadyAtNationalLimit");
        Component fieldIdentity = aircraftAlreadyInField.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(fieldIdentity, "teamID", 1);
        Set(fieldIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        objects.Add(aircraftAlreadyInField);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);
        MethodInfo allowProduction = governorType.GetMethod("PermitirProducao", BindingFlags.Static | BindingFlags.Public);
        object[] gateArguments = { CreateAircraftCard(aircraftPrefab, 1500L), 1, null };
        bool governorAllows = (bool)allowProduction.Invoke(null, gateArguments);
        Assert.IsFalse(governorAllows, "O limite aéreo nacional deve estar ocupado antes de tentar a compra rápida.");

        bool accepted = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, 1500L });

        yield return null;
        TrackAirportAircraft(airport);
        Assert.IsFalse(accepted, "A compra rápida do aeroporto deve respeitar o limite nacional aplicado pelo menu geral.");
        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"), "Uma compra bloqueada pelo limite não pode debitar tesouro.");
        Assert.AreEqual(aircraftBefore, ObterTotalAeronaves(airport), "Uma compra bloqueada pelo limite não pode criar aeronave.");
    }

    [UnityTest]
    public IEnumerator Test3c_NeutralAirportCannotUsePlayerTreasuryWithoutNeutralNationalManpower()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(4);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", 0);
        Component resources = PrepareResources(5000L);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test3c_NeutralAirportAircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", 1500);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        object neutralCountry = TypeOf("SistemaGovernoMundial")
            .GetMethod("ObterPais", Members)
            .Invoke(isolatedGovernmentInstance, new object[] { 0 });
        Assert.IsNull(neutralCountry, "O sistema nacional não deve criar um país para a identidade neutra.");

        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);
        bool accepted = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, 1500L });

        yield return null;
        TrackAirportAircraft(airport);
        Assert.IsFalse(accepted, "A base neutra não pode mobilizar tripulação de um país inexistente.");
        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"), "A tentativa neutra recusada não pode gastar o tesouro do jogador.");
        Assert.AreEqual(aircraftBefore, ObterTotalAeronaves(airport), "A tentativa neutra recusada não pode criar aeronave.");
    }

    [UnityTest]
    public IEnumerator Test4_LocalAirportPurchase_SpawnFailure_DoesNotDebitOrSpawn()
    {
        PrepareIsolatedPlayerGovernment(2);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Test4_WaterSurface";
        water.transform.position = new Vector3(5000f, 0f, 5000f);
        water.transform.localScale = new Vector3(100f, 2f, 100f);
        objects.Add(water);

        Component airport = CreatePlayerAirport(new Vector3(5000f, 0f, 5000f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test4_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(5200f, 20f, 5200f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        Assert.IsFalse((bool)Read(airport, "semEnergia"), "Este caso precisa manter o aeroporto energizado para alcançar o gate de spawn em água.");

        Type governmentType = TypeOf("SistemaGovernoMundial");
        object activeGovernment = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
        long manpower = (long)governmentType.GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
            .Invoke(activeGovernment, new object[] { 1 });
        long reserve = (long)governmentType.GetMethod("ObterReservaManpowerEstrategica", Members)
            .Invoke(activeGovernment, new object[] { 1 });
        int requiredManpower = (int)governmentType.GetMethod(
            "ObterEfetivoNecessario", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(GameObject) }, null)
            .Invoke(null, new object[] { aircraftPrefab });
        bool canProduce = (bool)airport.GetType().GetMethod("PodeProduzirAeronave", Members)
            .Invoke(airport, new object[] { aircraftPrefab });
        Debug.Log($"[TESTE 4 DIAGNOSTICO] governoIsolado={ReferenceEquals(activeGovernment, isolatedGovernmentInstance)}, teamBase={Read(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID")}, manpower={manpower}, reserva={reserve}, efetivoNecessario={requiredManpower}, podeProduzir={canProduce}");
        Assert.AreSame(isolatedGovernmentInstance, activeGovernment, "Test4 deve usar o Governo isolado.");
        Assert.AreEqual(2, manpower, "Test4 deve ter dois alistáveis antes da tentativa.");
        Assert.AreEqual(2, requiredManpower, "Test4 deve exigir o mínimo central de uma aeronave.");
        Assert.IsTrue(canProduce, "A recusa deve alcançar o gate de spawn em água, não o de manpower.");

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);
        int notificacoesDeRecursos = 0;
        Action contarAtualizacao = () => notificacoesDeRecursos++;
        EventInfo eventoRecursosAtualizados = TypeOf("GerenciadorRecursos").GetEvent("OnRecursosAtualizados", Members);
        eventoRecursosAtualizados.AddEventHandler(resources, contarAtualizacao);

        LogAssert.Expect(LogType.Error,
            "[Aeroporto] Spawn aéreo bloqueado em água: Test_Airport ((5000.0, 0.0, 5000.0)). Corrija o ponto Preparacao/pista da base.");

        bool retornoComprarAviao;
        try
        {
            retornoComprarAviao = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
                .Invoke(airport, new object[] { aircraftPrefab, (long)precoAeronave });
        }
        finally
        {
            eventoRecursosAtualizados.RemoveEventHandler(resources, contarAtualizacao);
        }

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Debug.Log($"[TESTE 4 REGISTRO] Saldo Antes: {saldoAntes}, Retorno ComprarAviao: {retornoComprarAviao}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}, Motivo: Spawn aereo bloqueado em agua.");

        Assert.IsFalse(retornoComprarAviao, "ComprarAviao deve retornar false ao rejeitar o spawn.");
        Assert.AreEqual(saldoAntes, saldoDepois, "Nenhum debito quando o spawn falha.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "REPRODUCAO: 0 aeronaves foram criadas!");
        Assert.AreEqual(2, notificacoesDeRecursos,
            "Falha após reservar o valor deve registrar débito e estorno pelo fluxo de recursos.");
    }

    [UnityTest]
    public IEnumerator Test5_MenuConstruction_AirportWithoutPower_BlocksPurchaseAndSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", true);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test5_MenuAircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        long precoItem = 200L;
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, precoItem);
        Component menu = PrepareConstructionMenu(gameManager);

        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", true);
        Assert.IsTrue((bool)Read(airport, "semEnergia"), "Este caso precisa manter a base sem energia para isolar o gate do menu.");

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Debug.Log($"[TESTE 5 REGISTRO] MenuConstrucao sem energia: Saldo Antes: {saldoAntes}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}");

        Assert.AreEqual(saldoAntes, saldoDepois, "A base sem energia não deve cobrar pela aeronave recusada.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "A base sem energia não deve gerar aeronave.");
    }

    [UnityTest]
    public IEnumerator Test6_ProductionQueue_SkipsUnpoweredAirportAndUsesPoweredAirport()
    {
        PrepareIsolatedPlayerGovernment(2);
        CreateRaisedLandFixture();
        Component unpoweredAirport = CreatePlayerAirport(new Vector3(40f, 30f, 40f));
        Set(unpoweredAirport, "semEnergia", true);
        Component poweredAirport = CreatePlayerAirport(new Vector3(60f, 30f, 60f));

        Component manager = Create("GerenteDeJogo");
        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test6_QueuedAircraftPrefab";
        objects.Add(aircraftPrefab);

        Type orderType = manager.GetType().GetNestedType("PedidoDeProducao", BindingFlags.Public);
        object order = Activator.CreateInstance(orderType);
        Set(order, "nomeUnidade", aircraftPrefab.name);
        Set(order, "prefab", aircraftPrefab);
        Set(order, "ehAviao", true);

        yield return null;

        // A rede nacional recalcula aeroportos ativos no primeiro frame e
        // desenergiza ambos neste fixture, que não cria uma usina. Este teste
        // isola a seleção do destino da fila: mantém explicitamente um aeroporto
        // desligado e o seguinte operacional depois do tick global.
        Set(unpoweredAirport, "semEnergia", true);
        Set(poweredAirport, "semEnergia", false);
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

        bool entregue = (bool)manager.GetType().GetMethod("FinalizarProducao", Members)
            .Invoke(manager, new[] { order });

        Assert.IsTrue(entregue, "A fila deve ignorar o primeiro aeroporto desligado e entregar no seguinte operacional.");
        Assert.AreEqual(0, ObterTotalAeronaves(unpoweredAirport), "A aeronave não deve ser enviada para o aeroporto sem energia.");
        Assert.AreEqual(1, ObterTotalAeronaves(poweredAirport), "O aeroporto energizado deve receber a aeronave.");
    }

    [UnityTest]
    public IEnumerator LegacyProductionQueueDoesNotDeliverPlayerAircraftToRivalAirport()
    {
        PrepareIsolatedPlayerGovernment(2);
        CreateRaisedLandFixture();
        IsolateAirportRegistry();

        Component rivalAirport = Create("GerenciadorAeroporto");
        rivalAirport.name = "Test_RivalAirport_OnlyRegisteredDestination";
        rivalAirport.transform.position = new Vector3(40f, 30f, 40f);
        Component rivalIdentity = rivalAirport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(rivalIdentity, "teamID", 2);
        Set(rivalIdentity, "nomeDoPais", "Rival");
        Set(rivalAirport, "semEnergia", false);
        rivalAirport.gameObject.SetActive(true);

        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        IList countries = (IList)governmentType.GetField("paises", Members).GetValue(isolatedGovernmentInstance);
        object rivalCountry = Activator.CreateInstance(countryType);
        Set(rivalCountry, "teamId", 2);
        Set(rivalCountry, "alistaveis", 2);
        Set(rivalCountry, "populacaoCivil", 0);
        Set(rivalCountry, "populacaoMilitarAtiva", 0);
        countries.Add(rivalCountry);

        Component manager = Create("GerenteDeJogo");
        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_LegacyPlayerAircraftPrefab";
        objects.Add(aircraftPrefab);
        Type orderType = manager.GetType().GetNestedType("PedidoDeProducao", BindingFlags.Public);
        object order = Activator.CreateInstance(orderType);
        Set(order, "nomeUnidade", aircraftPrefab.name);
        Set(order, "prefab", aircraftPrefab);
        Set(order, "ehAviao", true);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(rivalAirport, "semEnergia", false);

        Type airportType = TypeOf("GerenciadorAeroporto");
        IList registeredAirports = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(airportType));
        TypeOf("RegistroEntidadesJogo").GetMethod("FillAeroportos", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { registeredAirports });
        Assert.AreEqual(1, registeredAirports.Count, "A fixture deve expor somente o aeroporto rival à fila.");
        Assert.AreSame(rivalAirport, registeredAirports[0]);

        bool delivered = (bool)manager.GetType().GetMethod("FinalizarProducao", Members)
            .Invoke(manager, new[] { order });
        TrackAirportAircraft(rivalAirport);
        int rivalAircraftCount = ObterTotalAeronaves(rivalAirport);

        Assert.AreEqual(0, rivalAircraftCount,
            "A fila de produção do jogador não deve entregar no aeroporto rival. "
            + "FinalizarProducao retornou " + delivered + " e o destino contém " + rivalAircraftCount + " aeronave(s).");
    }

    [UnityTest]
    public IEnumerator LegacyProductionQueueAllowsUnidentifiedUnmarkedPlayerAirport()
    {
        PrepareIsolatedPlayerGovernment(2);
        CreateRaisedLandFixture();
        IsolateAirportRegistry();

        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Test_UnidentifiedManualPlayerAirport";
        airport.transform.position = new Vector3(40f, 30f, 40f);
        Set(airport, "semEnergia", false);
        airport.gameObject.SetActive(true);

        Component manager = Create("GerenteDeJogo");
        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_LegacyAircraftForUnidentifiedAirport";
        objects.Add(aircraftPrefab);
        Type orderType = manager.GetType().GetNestedType("PedidoDeProducao", BindingFlags.Public);
        object order = Activator.CreateInstance(orderType);
        Set(order, "nomeUnidade", aircraftPrefab.name);
        Set(order, "prefab", aircraftPrefab);
        Set(order, "ehAviao", true);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);

        Type airportType = TypeOf("GerenciadorAeroporto");
        IList registeredAirports = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(airportType));
        TypeOf("RegistroEntidadesJogo").GetMethod("FillAeroportos", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { registeredAirports });
        Assert.AreEqual(1, registeredAirports.Count);
        Assert.AreSame(airport, registeredAirports[0]);

        bool delivered = (bool)manager.GetType().GetMethod("FinalizarProducao", Members)
            .Invoke(manager, new[] { order });
        TrackAirportAircraft(airport);
        Assert.IsTrue(delivered, "A base sem identidade e sem marcadores de IA deve manter a regra legada de destino do jogador.");
        Assert.AreEqual(1, ObterTotalAeronaves(airport));
    }

    [UnityTest]
    public IEnumerator Test7_LocalAirportPurchase_InsufficientManpower_DoesNotDebitOrSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(0);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test7_InsufficientManpowerAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component identity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        Set(identity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Estrutura"));
        Set(identity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        const long price = 1500L;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", (int)price);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);

        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);
        Type unitType = TypeOf("TipoUnidade");
        object airType = Enum.Parse(unitType, "Aereo");
        int requiredManpower = (int)TypeOf("SistemaGovernoMundial")
            .GetMethod("ObterEfetivoNecessario", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(GameObject), unitType }, null)
            .Invoke(null, new[] { (object)aircraftPrefab, airType });
        Assert.AreEqual(2, requiredManpower, "O cenário precisa alcançar o gate central de efetivo aéreo.");

        bool canProduce = (bool)airport.GetType().GetMethod("PodeProduzirAeronave", Members)
            .Invoke(airport, new object[] { aircraftPrefab });
        Assert.IsFalse(canProduce,
            "Um prefab legado classificado como estrutura ainda deve ser pré-validado com o efetivo mínimo aéreo.");

        bool accepted = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, price });
        Assert.IsNull(GameObject.Find(aircraftPrefab.name + " (Clone)"),
            "A recusa por falta de efetivo deve ocorrer antes de instanciar a aeronave.");
        yield return null;

        TrackAirportAircraft(airport);
        Assert.IsFalse(accepted, "A compra deve ser recusada sem efetivo nacional disponível.");
        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"), "A recusa não pode debitar o tesouro.");
        Assert.AreEqual(aircraftBefore, ObterTotalAeronaves(airport), "A recusa não pode deixar aeronave no aeroporto.");
        Assert.AreEqual(0L, (long)TypeOf("SistemaGovernoMundial")
            .GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
            .Invoke(isolatedGovernmentInstance, new object[] { 1 }));
    }

    [UnityTest]
    public IEnumerator Test8_MenuConstruction_WithoutAirport_DoesNotDebitOrSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test8_NoAirportAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component identity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        Set(identity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(identity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Component menu = PrepareConstructionMenu(gameManager);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

        long balanceBefore = (long)Read(resources, "dinheiro");
        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"), "Sem aeroporto, a operação deve parar antes do débito.");
        Assert.IsNull(GameObject.Find(aircraftPrefab.name + " (Clone)"), "Sem infraestrutura, nenhuma aeronave pode ser criada.");
    }

    [UnityTest]
    public IEnumerator Test8b_MenuConstruction_LegacyAircraftWithoutManpowerBlocksBeforeSpawn()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(0);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test8b_LegacyAircraftWithoutManpower";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Component menu = PrepareConstructionMenu(gameManager);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        long balanceBefore = (long)Read(resources, "dinheiro");

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });

        Assert.IsNull(GameObject.Find(aircraftPrefab.name + " (Clone)"),
            "O menu deve bloquear prefab legado sem efetivo antes de chamar o spawn do aeroporto.");
        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"),
            "A pré-validação de efetivo deve impedir qualquer débito do jogador.");
        yield return null;

        TrackAirportAircraft(airport);
        Assert.AreEqual(0, ObterTotalAeronaves(airport),
            "Uma tentativa recusada por falta de efetivo não pode registrar unidade no aeroporto.");
    }

    [UnityTest]
    public IEnumerator Test9_MenuConstruction_ValidPurchase_DebitsOnceAndRegistersPlayerAircraft()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test9_MenuValidPurchaseAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Component menu = PrepareConstructionMenu(gameManager);
        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        TrackAirportAircraft(airport);
        IList patio = Read(airport, "avioesNoPatio") as IList;
        IList hangar = Read(airport, "avioesNoHangar") as IList;
        Component delivered = patio != null
            ? patio.Cast<object>().OfType<Component>().FirstOrDefault()
            : null;
        if (delivered == null && hangar != null)
        {
            delivered = hangar.Cast<object>().OfType<Component>().FirstOrDefault();
        }

        Assert.AreEqual(3500L, (long)Read(resources, "dinheiro"), "O menu deve cobrar o preço efetivo uma única vez.");
        Assert.AreEqual(balanceBefore - 1500L, (long)Read(resources, "dinheiro"));
        Assert.AreEqual(aircraftBefore + 1, ObterTotalAeronaves(airport), "Uma compra válida deve gerar uma única aeronave registrada.");
        Assert.IsNotNull(delivered, "A aeronave precisa estar no pátio ou hangar do aeroporto de destino.");
        Assert.AreEqual(1, (int)Read(delivered.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"), "A aeronave entregue deve pertencer ao jogador.");
        Assert.AreEqual("Aereo", Read(delivered.GetComponent(TypeOf("IdentidadeUnidade")), "tipoUnidade").ToString());
    }

    [UnityTest]
    public IEnumerator Test9b_MenuConstruction_SpawnFailureRefundsMoneyAndPreservesManpower()
    {
        PrepareIsolatedPlayerGovernment(2);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Test9b_WaterSurface";
        water.transform.position = new Vector3(5000f, 0f, 5000f);
        water.transform.localScale = new Vector3(100f, 2f, 100f);
        objects.Add(water);

        Component airport = CreatePlayerAirport(new Vector3(5000f, 0f, 5000f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test9b_MenuAircraftSpawnFailure";
        aircraftPrefab.transform.position = new Vector3(5200f, 20f, 5200f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Set(item, "categoria", Enum.Parse(TypeOf("DadosConstrucao+CategoriaItem"), "Aeronautica"));
        Component menu = PrepareConstructionMenu(gameManager);
        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        Type governmentType = TypeOf("SistemaGovernoMundial");
        object activeGovernment = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null);
        Assert.AreSame(isolatedGovernmentInstance, activeGovernment, "O caso precisa usar o Governo isolado.");
        long manpowerBefore = (long)governmentType.GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
            .Invoke(activeGovernment, new object[] { 1 });
        Assert.AreEqual(2L, manpowerBefore, "A tentativa precisa ter efetivo suficiente para chegar ao gate de spawn.");
        Assert.IsTrue((bool)airport.GetType().GetMethod("PodeProduzirAeronave", Members)
            .Invoke(airport, new object[] { aircraftPrefab }), "A recusa deve ocorrer no spawn sobre água.");

        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);
        LogAssert.Expect(LogType.Error,
            "[Aeroporto] Spawn aéreo bloqueado em água: Test_Airport ((5000.0, 0.0, 5000.0)). Corrija o ponto Preparacao/pista da base.");

        menu.GetType().GetMethod("ConstruirItem", Members).Invoke(menu, new object[] { item, null });
        yield return null;

        TrackAirportAircraft(airport);
        long balanceAfter = (long)Read(resources, "dinheiro");
        int aircraftAfter = ObterTotalAeronaves(airport);
        long manpowerAfter = (long)governmentType.GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
            .Invoke(activeGovernment, new object[] { 1 });

        Assert.AreEqual(balanceBefore, balanceAfter,
            "O menu cobra antes de chamar o aeroporto, mas precisa devolver o valor quando o spawn é recusado.");
        Assert.AreEqual(aircraftBefore, aircraftAfter, "A recusa de spawn não pode registrar aeronave.");
        Assert.AreEqual(manpowerBefore, manpowerAfter, "A recusa antes da instanciação não pode consumir manpower.");
    }

    [UnityTest]
    public IEnumerator CoastGuardHelicopterUsesPoweredHeliportWhenPlayerAirportsAreUnpowered()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", true);

        Component heliport = Create("Heliporto");
        heliport.name = "Test_PoweredHeliport";
        heliport.transform.position = new Vector3(60f, 30f, 60f);
        Component heliportIdentity = heliport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(heliportIdentity, "teamID", 1);
        heliport.gameObject.SetActive(true);

        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");
        GameObject helicopterPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        helicopterPrefab.name = "Test_UH60_Guarda_Costeira";
        helicopterPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = helicopterPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        helicopterPrefab.AddComponent(TypeOf("Helicoptero"));
        objects.Add(helicopterPrefab);

        ScriptableObject item = CreateAircraftCard(helicopterPrefab, 1500L);
        item.GetType().GetProperty("NomeItem").SetValue(item, "UH60 Guarda Costeira", null);
        Component menu = PrepareConstructionMenu(gameManager);
        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", true);
        Type heliportType = TypeOf("Heliporto");
        IList registeredHeliports = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(heliportType));
        Type entityRegistryType = TypeOf("RegistroEntidadesJogo");
        entityRegistryType.GetMethod("FillHeliportos", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { registeredHeliports });
        Assert.IsTrue(registeredHeliports.Cast<Component>().Any(entry => entry == heliport),
            "A fixture precisa expor o heliporto ao mesmo registro usado pelo menu.");
        Assert.IsNotNull(helicopterPrefab.GetComponent(TypeOf("Helicoptero")), "O item de teste precisa ser reconhecido como helicóptero.");
        StringAssert.Contains("Guarda Costeira", item.GetType().GetMethod("GetDisplayName").Invoke(item, null) as string,
            "O item precisa seguir a rota especial do UH-60 da Guarda Costeira.");
        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        IList parkedHelicopters = (IList)Read(heliport, "helicopterosPousados");
        Assert.AreEqual(3500L, (long)Read(resources, "dinheiro"),
            "Com aeroporto sem energia, a compra deve usar o heliporto energizado disponível.");
        Assert.AreEqual(1, parkedHelicopters.Count,
            "O UH-60 deve ser registrado no heliporto escolhido.");
        Component delivered = parkedHelicopters[0] as Component;
        Assert.IsNotNull(delivered);
        Assert.AreEqual(1, (int)Read(delivered.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"));
    }

    [UnityTest]
    public IEnumerator CarrierDronePurchaseCannotBypassNationalAircraftLimit()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(4);
        Component resources = PrepareResources(5000L);

        Component carrier = Create("GerenciadorPortaAvioes");
        carrier.name = "Test_PlayerCarrier";
        carrier.transform.position = new Vector3(50f, 30f, 50f);
        Component carrierIdentity = carrier.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(carrierIdentity, "teamID", 1);
        Set(carrierIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Naval"));

        GameObject dronePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dronePrefab.name = "Test_CarrierKamikazeDrone";
        dronePrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(dronePrefab);
        Set(carrier, "prefabDroneKamikaze", dronePrefab);
        Set(carrier, "precoDroneKamikaze", 1500);
        carrier.gameObject.SetActive(true);

        GameObject aircraftAlreadyInField = new GameObject("Test_CarrierDroneAtNationalLimit");
        Component fieldIdentity = aircraftAlreadyInField.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(fieldIdentity, "teamID", 1);
        Set(fieldIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        objects.Add(aircraftAlreadyInField);

        Type governorType = TypeOf("GovernadorGameplayRTS");
        PropertyInfo governorInstance = governorType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        originalGovernorInstance = governorInstance.GetValue(null, null);
        governorInstanceSetter = governorInstance.GetSetMethod(true);
        governorSingletonWasReplaced = true;
        governorInstanceSetter.Invoke(null, new object[] { null });
        Component governor = Create("GovernadorGameplayRTS");
        Set(governor, "limiteTotal", 20);
        Set(governor, "limiteAereos", 1);
        Set(governor, "bloquearProducaoQuandoSaturado", false);
        governor.gameObject.SetActive(true);

        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        governorInstanceSetter.Invoke(null, new[] { governor });
        Set(carrier, "semEnergia", false);
        object[] gateArguments =
        {
            dronePrefab,
            Enum.Parse(TypeOf("TipoUnidade"), "Aereo"),
            1,
            null
        };
        bool governorAllows = (bool)governorType.GetMethod("PermitirProducaoDePrefab", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, gateArguments);
        Assert.IsFalse(governorAllows, "O cenário precisa começar no limite nacional de aeronaves.");

        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(carrier);

        bool accepted = (bool)carrier.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(carrier, new object[] { dronePrefab, 1500L });

        yield return null;
        TrackAirportAircraft(carrier);
        Assert.IsFalse(accepted, "O botão do porta-aviões não deve ignorar o limite aéreo nacional.");
        Assert.AreEqual(balanceBefore, (long)Read(resources, "dinheiro"), "Uma compra bloqueada não pode debitar saldo.");
        Assert.AreEqual(aircraftBefore, ObterTotalAeronaves(carrier), "Uma compra bloqueada não pode criar aeronave.");
    }

    [UnityTest]
    public IEnumerator Test10_MenuConstruction_ZeroAviationFuelStillDeliversAircraftWithEmptyTank()
    {
        CreateRaisedLandFixture();
        PrepareEmptyAviationFuelStock();
        PrepareIsolatedPlayerGovernment(2);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);
        Component resources = PrepareResources(5000L);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test10_ZeroFuelPurchaseAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 1);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Component menu = PrepareConstructionMenu(gameManager);
        yield return null;

        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);
        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        TrackAirportAircraft(airport);
        IList patio = Read(airport, "avioesNoPatio") as IList;
        IList hangar = Read(airport, "avioesNoHangar") as IList;
        Component delivered = patio != null
            ? patio.Cast<object>().OfType<Component>().FirstOrDefault()
            : null;
        if (delivered == null && hangar != null)
        {
            delivered = hangar.Cast<object>().OfType<Component>().FirstOrDefault();
        }

        Assert.AreEqual(balanceBefore - 1500L, (long)Read(resources, "dinheiro"),
            "A falta de combustível não deve causar cobrança duplicada nem impedir o pagamento da aeronave.");
        Assert.AreEqual(aircraftBefore + 1, ObterTotalAeronaves(airport),
            "A compra deve entregar exatamente uma aeronave mesmo sem combustível nacional.");
        Assert.IsNotNull(delivered, "A aeronave precisa ser registrada no pátio ou hangar.");
        Assert.AreEqual(1, (int)Read(delivered.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"));

        Component fuel = delivered.GetComponent(TypeOf("CombustivelUnidade"));
        Assert.IsNotNull(fuel, "A aeronave entregue precisa ter o componente de combustível operacional.");
        Assert.IsTrue((bool)Read(fuel, "usaCombustivel"), "A aeronave de teste deve consumir combustível.");
        Assert.Greater(Convert.ToSingle(Read(fuel, "capacidade")), 0f, "A aeronave precisa ter tanque configurado.");
        Assert.AreEqual(0f, Convert.ToSingle(Read(fuel, "combustivelAtual")), 0.01f,
            "Com estoque nacional zerado, a entrega não pode criar combustível grátis.");

        Type industrialType = TypeOf("SistemaIndustrialNacional");
        string aviationFuelId = (string)TypeOf("IndustriaIds")
            .GetField("CombustivelAviacao", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(null);
        double remainingStock = Convert.ToDouble(industrialType.GetMethod(
            "ObterQuantidadePais",
            Members,
            null,
            new[] { typeof(int), typeof(string) },
            null).Invoke(testIndustrialManager, new object[] { 1, aviationFuelId }));
        Assert.AreEqual(0d, remainingStock, 0.01d, "A tentativa de carga não pode criar estoque nacional.");

        float fuelToRestore = Mathf.Min(90f, Convert.ToSingle(Read(fuel, "capacidade")));
        MethodInfo addStock = industrialType.GetMethod(
            "TentarAdicionar",
            Members,
            null,
            new[] { typeof(string), typeof(string), typeof(double) },
            null);
        Assert.IsTrue((bool)addStock.Invoke(
                testIndustrialManager,
                new object[] { "1", aviationFuelId, (double)fuelToRestore }),
            "O teste deve devolver combustível ao estoque nacional antes do serviço do aeroporto.");

        MethodInfo airportRefuelService = airport.GetType().GetMethod("ReabastecerAeronave", Members);
        Assert.IsNotNull(airportRefuelService, "O aeroporto precisa expor internamente o serviço periódico de reabastecimento.");
        airportRefuelService.Invoke(airport, new object[] { delivered, 2f });

        Assert.AreEqual(fuelToRestore, Convert.ToSingle(Read(fuel, "combustivelAtual")), 0.01f,
            "A aeronave estacionada deve receber combustível quando o estoque nacional volta a existir.");
        remainingStock = Convert.ToDouble(industrialType.GetMethod(
            "ObterQuantidadePais",
            Members,
            null,
            new[] { typeof(int), typeof(string) },
            null).Invoke(testIndustrialManager, new object[] { 1, aviationFuelId }));
        Assert.AreEqual(0d, remainingStock, 0.01d, "O reabastecimento da base deve consumir o combustível do país, sem criar carga grátis.");

        // Repete a compra pelo mesmo fluxo do menu, agora com estoque nacional
        // suficiente. Isso cobre o caso mostrado no painel: aeronave nova não
        // deve sair do aeroporto com 0% quando o país possui combustível.
        float estoqueAntesDaSegundaCompra = Convert.ToSingle(Read(fuel, "capacidade"));
        Assert.IsTrue((bool)addStock.Invoke(
                testIndustrialManager,
                new object[] { "1", aviationFuelId, (double)estoqueAntesDaSegundaCompra }),
            "O teste precisa abastecer o estoque nacional antes da segunda compra.");
        int aeronavesAntesDaSegundaCompra = ObterTotalAeronaves(airport);
        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        TrackAirportAircraft(airport);
        patio = Read(airport, "avioesNoPatio") as IList;
        hangar = Read(airport, "avioesNoHangar") as IList;
        Component segundaAeronave = patio != null
            ? patio.Cast<object>().OfType<Component>().FirstOrDefault(aeronave => aeronave != delivered)
            : null;
        if (segundaAeronave == null && hangar != null)
        {
            segundaAeronave = hangar.Cast<object>().OfType<Component>().FirstOrDefault(aeronave => aeronave != delivered);
        }

        Assert.AreEqual(aeronavesAntesDaSegundaCompra + 1, ObterTotalAeronaves(airport),
            "A compra com combustível disponível deve entregar exatamente uma aeronave.");
        Assert.IsNotNull(segundaAeronave, "A segunda aeronave precisa ser registrada no pátio ou hangar.");
        Component combustivelSegundaAeronave = segundaAeronave.GetComponent(TypeOf("CombustivelUnidade"));
        Assert.IsNotNull(combustivelSegundaAeronave);
        Assert.Greater(Convert.ToSingle(Read(combustivelSegundaAeronave, "combustivelAtual")), 0f,
            "A aeronave recém-comprada deve receber combustível do estoque nacional disponível.");

        remainingStock = Convert.ToDouble(industrialType.GetMethod(
            "ObterQuantidadePais",
            Members,
            null,
            new[] { typeof(int), typeof(string) },
            null).Invoke(testIndustrialManager, new object[] { 1, aviationFuelId }));
        Assert.Less(remainingStock, estoqueAntesDaSegundaCompra,
            "A carga inicial da aeronave precisa ser debitada do estoque nacional.");
    }

    [UnityTest]
    public IEnumerator MarketDeliveryRejectsAirportWithoutOwnerIdentity()
    {
        const int buyerTeamId = 1;
        PrepareIsolatedPlayerGovernment(2);

        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Test_Market_LegacyPlayerAirport_WithoutIdentity";
        airport.transform.position = new Vector3(50f, 30f, 50f);
        airport.gameObject.SetActive(true);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_Market_LegacyAirportAircraft";
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 2);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        aircraftPrefab.AddComponent(TypeOf("ControleAviao"));
        objects.Add(aircraftPrefab);

        ScriptableObject card = CreateAircraftCard(aircraftPrefab, 1500L);
        string stableId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);
        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", BindingFlags.Static | BindingFlags.Public);
        originalAircraftCatalog = catalogField.GetValue(null);
        aircraftCatalogOverridden = true;
        IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        catalog.Add(card);
        catalogField.SetValue(null, catalog);

        object marketItem = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(marketItem, "prefabId", stableId);
        Set(marketItem, "tipoEntrega", "aeronave");

        Component logistics = Create("SistemaLogisticaMercado");
        MethodInfo deliver = logistics.GetType().GetMethod("EntregarEquipamento", Members);
        Assert.IsNotNull(deliver);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        bool delivered = (bool)deliver.Invoke(logistics, new object[] { marketItem, buyerTeamId, 1 });
        yield return null;
        TrackAirportAircraft(airport);

        Assert.IsFalse(delivered,
            "O mercado deve recusar uma base sem identidade porque não consegue comprovar a equipe proprietária.");
        Assert.AreEqual(0, ObterTotalAeronaves(airport),
            "Um destino sem proprietário comprovado não pode receber nem registrar a aeronave.");
        Assert.IsNull(GameObject.Find(aircraftPrefab.name + " (Clone)"),
            "A entrega recusada não pode instanciar a aeronave no aeroporto sem identidade.");
        Assert.AreEqual(2L, (long)TypeOf("SistemaGovernoMundial")
                .GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
                .Invoke(isolatedGovernmentInstance, new object[] { buyerTeamId }),
            "Uma entrega sem destino válido não pode mobilizar efetivo do comprador.");
    }

    [UnityTest]
    public IEnumerator Test11_MarketLogisticsDeliveryRegistersAircraftAtBuyerAirport()
    {
        const int buyerTeamId = 876543;
        PrepareIsolatedPlayerGovernment(2);
        IList countries = Read(isolatedGovernmentInstance, "paises") as IList;
        Assert.IsNotNull(countries);
        Assert.AreEqual(1, countries.Count);
        Set(countries[0], "teamId", buyerTeamId);
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", buyerTeamId);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test11_MarketDeliveryAircraft";
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 2);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        aircraftPrefab.AddComponent(TypeOf("ControleAviao"));
        objects.Add(aircraftPrefab);

        ScriptableObject card = CreateAircraftCard(aircraftPrefab, 1500L);
        string stableId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);
        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", BindingFlags.Static | BindingFlags.Public);
        originalAircraftCatalog = catalogField.GetValue(null);
        aircraftCatalogOverridden = true;
        IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        catalog.Add(card);
        catalogField.SetValue(null, catalog);

        object marketItem = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(marketItem, "prefabId", stableId);
        Set(marketItem, "tipoEntrega", "aeronave");

        Component logistics = Create("SistemaLogisticaMercado");
        MethodInfo deliver = logistics.GetType().GetMethod("EntregarEquipamento", Members);
        Assert.IsNotNull(deliver, "O caminho de entrega logística deve estar disponível para a aeronave comprada.");

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

        MethodInfo findCard = logistics.GetType().GetMethod("EncontrarFicha", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreSame(card, findCard.Invoke(null, new object[] { stableId }), "O catálogo temporário deve resolver a ficha pelo ID estável.");
        object[] prefabArgs = { null };
        bool prefabResolved = (bool)card.GetType().GetMethod("TryGetPrefabBasico", Members).Invoke(card, prefabArgs);
        Assert.IsTrue(prefabResolved, "A ficha do mercado precisa resolver o prefab antes da entrega.");

        MethodInfo findDestination = logistics.GetType().GetMethod(
            "EncontrarDestino",
            Members,
            null,
            new[] { typeof(string), typeof(int) },
            null);
        Transform destination = findDestination.Invoke(logistics, new object[] { "aeronave", buyerTeamId }) as Transform;
        Assert.IsNotNull(destination, "A fixture precisa resolver o aeroporto do comprador como destino logístico.");
        Assert.AreSame(airport.transform, destination,
            "A busca do destino precisa resolver o aeroporto exclusivo desta fixture.");

        Type governmentType = TypeOf("SistemaGovernoMundial");
        object government = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
        Type unitType = TypeOf("TipoUnidade");
        object airType = Enum.Parse(unitType, "Aereo");
        MethodInfo requiredManpower = governmentType.GetMethod("ObterEfetivoNecessario", BindingFlags.Static | BindingFlags.Public, null,
            new[] { typeof(GameObject), unitType }, null);
        int required = (int)requiredManpower.Invoke(null, new[] { aircraftPrefab, airType });
        long available = (long)governmentType.GetMethod("ObterManpowerDisponivelParaMobilizacao", Members)
            .Invoke(government, new object[] { buyerTeamId });
        long reserve = (long)governmentType.GetMethod("ObterReservaManpowerEstrategica", Members)
            .Invoke(government, new object[] { buyerTeamId });
        MethodInfo canMobilize = governmentType.GetMethod("PodeProduzirUnidadeComEfetivo", Members, null,
            new[] { typeof(int), typeof(GameObject), typeof(int), unitType }, null);
        bool manpowerAvailable = (bool)canMobilize.Invoke(government, new[] { (object)buyerTeamId, aircraftPrefab, 1, airType });
        TestContext.WriteLine("governmentIsIsolated=" + ReferenceEquals(government, isolatedGovernmentInstance)
            + " available=" + available + " reserve=" + reserve + " required=" + required
            + " canMobilize=" + manpowerAvailable);
        Assert.AreSame(isolatedGovernmentInstance, government, "O teste precisa manter o governo isolado ativo após o primeiro frame.");
        Assert.AreEqual(2L, available, "A fixture precisa preservar os dois alistáveis configurados.");
        Assert.AreEqual(0L, reserve, "A fixture precisa começar sem reserva estratégica.");
        Assert.AreEqual(2, required, "A unidade aérea de teste deve exigir o mínimo normal de efetivo.");
        Assert.IsTrue(manpowerAvailable, "A fixture precisa ter manpower suficiente para receber a aeronave.");

        bool deliveredSuccessfully = (bool)deliver.Invoke(logistics, new object[] { marketItem, buyerTeamId, 1 });
        yield return null;

        IList patio = Read(airport, "avioesNoPatio") as IList;
        IList hangar = Read(airport, "avioesNoHangar") as IList;
        Component deliveredAircraftInBase = patio != null
            ? patio.Cast<object>().OfType<Component>().FirstOrDefault()
            : null;
        if (deliveredAircraftInBase == null && hangar != null)
        {
            deliveredAircraftInBase = hangar.Cast<object>().OfType<Component>().FirstOrDefault();
        }
        GameObject deliveredObject = deliveredAircraftInBase != null ? deliveredAircraftInBase.gameObject : null;
        TestContext.WriteLine("marketDelivery=" + deliveredSuccessfully + " airportCount=" + ObterTotalAeronaves(airport)
            + " airplane=" + (deliveredObject != null ? deliveredObject.name : "<missing>"));
        Assert.IsTrue(deliveredSuccessfully, "A entrega com aeroporto e efetivo válidos deve concluir.");
        Assert.IsNotNull(deliveredObject, "O item de mercado deve materializar uma aeronave para o comprador.");
        objects.Add(deliveredObject);

        Component deliveredAircraft = deliveredObject.GetComponent(TypeOf("ControleAviao"));
        Assert.IsNotNull(deliveredAircraft, "A aeronave entregue deve manter seu controlador operacional.");
        Assert.AreEqual(buyerTeamId, (int)Read(deliveredObject.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"));
        Assert.AreSame(airport, Read(deliveredAircraft, "aeroportoOrigem"),
            "A aeronave deve conhecer o aeroporto comprador como base de retorno.");
        Assert.AreEqual(1, ObterTotalAeronaves(airport),
            "A aeronave recebida deve entrar exatamente uma vez no pátio ou hangar do comprador.");
    }

    [UnityTest]
    public IEnumerator FullInternationalAircraftPurchaseTravelsByFreightAndRegistersAtPlayerAirport()
    {
        const int buyerTeamId = 1;
        const int sellerTeamId = 2;
        const int aircraftPrice = 1500;
        const int expectedFreight = 75;

        PrepareIsolatedPlayerGovernment(2);
        IList countries = Read(isolatedGovernmentInstance, "paises") as IList;
        Assert.IsNotNull(countries);
        Assert.AreEqual(1, countries.Count);
        object buyerCountry = countries[0];
        Set(buyerCountry, "teamId", buyerTeamId);
        Set(buyerCountry, "saldo", aircraftPrice + expectedFreight);

        object sellerCountry = Activator.CreateInstance(TypeOf("DadosPaisGoverno"));
        Set(sellerCountry, "teamId", sellerTeamId);
        Set(sellerCountry, "saldo", 0L);
        Set(sellerCountry, "alistaveis", 0);
        countries.Add(sellerCountry);

        Component airport = CreatePlayerAirport(new Vector3(20f, 0f, 0f));
        Set(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", buyerTeamId);

        Component sellerPier = CreateLogisticsPier("Seller", sellerTeamId, new Vector3(0f, 0f, 0f));
        Component buyerPier = CreateLogisticsPier("Buyer", buyerTeamId, new Vector3(20f, 0f, 0f));
        Assert.IsNotNull(sellerPier);
        Assert.IsNotNull(buyerPier);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_FullRoute_AircraftPrefab";
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", sellerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        aircraftPrefab.AddComponent(TypeOf("ControleAviao"));
        objects.Add(aircraftPrefab);

        ScriptableObject card = CreateAircraftCard(aircraftPrefab, aircraftPrice);
        Set(card, "categoria", Enum.Parse(
            TypeOf("DadosConstrucao").GetNestedType("CategoriaItem"), "Aeronautica"));
        string stableId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);
        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", BindingFlags.Static | BindingFlags.Public);
        originalAircraftCatalog = catalogField.GetValue(null);
        aircraftCatalogOverridden = true;
        IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        catalog.Add(card);
        catalogField.SetValue(null, catalog);

        Type marketType = TypeOf("SistemaMercadoGlobal");
        PropertyInfo marketInstanceProperty = marketType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        object market = marketInstanceProperty.GetValue(null, null);
        bool marketCreatedForTest = false;
        List<object> previousMarketItems = null;
        List<object> previousMarketHistory = null;
        if (market == null)
        {
            Component marketComponent = Create("SistemaMercadoGlobal");
            marketComponent.gameObject.SetActive(true);
            yield return null;
            market = marketInstanceProperty.GetValue(null, null);
            marketCreatedForTest = true;
        }

        IList marketItems = (IList)Read(market, "itens");
        IList marketHistory = (IList)Read(market, "historico");
        previousMarketItems = marketItems.Cast<object>().ToList();
        previousMarketHistory = marketHistory.Cast<object>().ToList();

        object marketItem = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(marketItem, "id", "test_full_route_aircraft_offer");
        Set(marketItem, "nome", "Test Full Route Aircraft");
        Set(marketItem, "categoria", "Aeronautica");
        Set(marketItem, "precoBase", aircraftPrice);
        Set(marketItem, "precoAtual", aircraftPrice);
        Set(marketItem, "estoqueGlobal", 1);
        Set(marketItem, "podeComprar", true);
        Set(marketItem, "podeVender", true);
        Set(marketItem, "equipamentoMilitar", true);
        Set(marketItem, "prefabId", stableId);
        Set(marketItem, "tipoEntrega", "aeronave");
        marketItems.Add(marketItem);

        Type logisticsType = TypeOf("SistemaLogisticaMercado");
        PropertyInfo logisticsInstanceProperty = logisticsType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        object logistics = logisticsInstanceProperty.GetValue(null, null);
        bool logisticsCreatedForTest = false;
        if (logistics == null)
        {
            logisticsType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            yield return null;
            logistics = logisticsInstanceProperty.GetValue(null, null);
            logisticsCreatedForTest = true;
        }

        MethodInfo createLogisticsSave = logisticsType.GetMethod("CriarSaveData", Members);
        MethodInfo restoreLogisticsSave = logisticsType.GetMethod("RestaurarSaveData", Members);
        object logisticsSave = createLogisticsSave.Invoke(logistics, null);
        GameObject cargoPrefab = new GameObject("Test_FullRoute_CargoPrefab");
        objects.Add(cargoPrefab);
        cargoPrefab.AddComponent(TypeOf("NavioCargaMercado"));
        cargoPrefab.SetActive(true);
        GameObject originalCargoPrefab = (GameObject)Read(logistics, "prefabNavioCarga");
        float originalLoadingTime = Convert.ToSingle(Read(logistics, "tempoEmbarque"));
        float originalUnloadingTime = Convert.ToSingle(Read(logistics, "tempoDesembarque"));
        Set(logistics, "prefabNavioCarga", cargoPrefab);
        Set(logistics, "tempoEmbarque", 0.05f);
        Set(logistics, "tempoDesembarque", 0.05f);

        try
        {
            yield return null;
            marketInstanceProperty.GetSetMethod(true).Invoke(null, new[] { market });
            governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

            MethodInfo buy = marketType.GetMethod("Comprar", Members);
            object[] buyArguments = { buyerTeamId, sellerTeamId, "test_full_route_aircraft_offer", 1, null };
            bool purchased = (bool)buy.Invoke(market, buyArguments);
            Assert.IsTrue(purchased, "A compra internacional deve reservar a aeronave e criar a ordem logística. " + Convert.ToString(buyArguments[4]));
            Assert.AreEqual(0L, Convert.ToInt64(Read(buyerCountry, "saldo")), "Compra e frete devem ser debitados uma vez.");

            IList orders = (IList)Read(logistics, "pedidos");
            Assert.AreEqual(1, orders.Count, "A compra deve criar uma ordem de frete.");
            object order = orders[orders.Count - 1];
            Assert.AreEqual(expectedFreight, Convert.ToInt32(Read(order, "frete")));
            Assert.AreEqual("AGUARDANDO EMBARQUE", Convert.ToString(Read(order, "status")));
            Assert.AreEqual(0L, Convert.ToInt64(Read(sellerCountry, "saldo")), "O vendedor só recebe após a entrega.");

            MethodInfo processTrips = logisticsType.GetMethod("ProcessarViagens", Members);
            processTrips.Invoke(logistics, null);
            for (int attempt = 0; attempt < 24
                && !string.Equals(Convert.ToString(Read(order, "status")), "ENTREGUE", StringComparison.OrdinalIgnoreCase);
                attempt++)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }
            TrackAirportAircraft(airport);

            Assert.AreEqual("ENTREGUE", Convert.ToString(Read(order, "status")), "A viagem deve finalizar a entrega no destino.");
            Assert.AreEqual(aircraftPrice, Convert.ToInt64(Read(sellerCountry, "saldo")), "O vendedor deve receber o preço sem duplicar o frete.");
            Assert.AreEqual(1, ObterTotalAeronaves(airport), "A aeronave deve ser registrada uma única vez no aeroporto comprador.");
            IList patio = Read(airport, "avioesNoPatio") as IList;
            IList hangar = Read(airport, "avioesNoHangar") as IList;
            Component delivered = patio != null ? patio.Cast<object>().OfType<Component>().FirstOrDefault() : null;
            if (delivered == null && hangar != null) delivered = hangar.Cast<object>().OfType<Component>().FirstOrDefault();
            Assert.IsNotNull(delivered);
            Component deliveredIdentity = delivered.GetComponent(TypeOf("IdentidadeUnidade"));
            Assert.AreEqual(buyerTeamId, (int)Read(deliveredIdentity, "teamID"));
            Assert.AreSame(airport, Read(delivered.GetComponent(TypeOf("ControleAviao")), "aeroportoOrigem"));
        }
        finally
        {
            if (logistics != null)
            {
                Set(logistics, "prefabNavioCarga", originalCargoPrefab);
                Set(logistics, "tempoEmbarque", originalLoadingTime);
                Set(logistics, "tempoDesembarque", originalUnloadingTime);
                restoreLogisticsSave.Invoke(logistics, new[] { logisticsSave });
            }

            marketItems.Clear();
            for (int i = 0; i < previousMarketItems.Count; i++) marketItems.Add(previousMarketItems[i]);
            marketHistory.Clear();
            for (int i = 0; i < previousMarketHistory.Count; i++) marketHistory.Add(previousMarketHistory[i]);
            if (marketCreatedForTest && market is Component marketComponent)
            {
                marketInstanceProperty.GetSetMethod(true).Invoke(null, new object[] { null });
                UnityEngine.Object.DestroyImmediate(marketComponent.gameObject);
            }
            if (logisticsCreatedForTest && logistics is Component logisticsComponent)
            {
                logisticsInstanceProperty.GetSetMethod(true).Invoke(null, new object[] { null });
                UnityEngine.Object.DestroyImmediate(logisticsComponent.gameObject);
            }

            Type cargoType = TypeOf("NavioCargaMercado");
            UnityEngine.Object[] cargos = UnityEngine.Object.FindObjectsByType(cargoType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cargos.Length; i++)
            {
                Component cargo = cargos[i] as Component;
                if (cargo != null && cargo.name.IndexOf("Test_FullRoute_CargoPrefab", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(cargo.gameObject);
            }
        }
    }

    [UnityTest]
    public IEnumerator BrainMasterRejectsRetryAfterAircraftOrderWasDelivered()
    {
        const int teamId = 987699;
        const long startingBalance = 10000L;
        const long aircraftPrice = 1500L;

        productionRegistrySnapshot = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry")
            .GetMethod("CaptureSaveData", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, null);
        productionRegistrySnapshotCaptured = productionRegistrySnapshot != null;
        Assert.IsTrue(productionRegistrySnapshotCaptured, "O teste precisa preservar as reservas anteriores.");

        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(4);
        IList countries = Read(isolatedGovernmentInstance, "paises") as IList;
        Assert.IsNotNull(countries);
        Assert.AreEqual(1, countries.Count);
        object country = countries[0];
        Set(country, "teamId", teamId);
        Set(country, "saldo", startingBalance);

        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", teamId);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_BrainMaster_RetryAircraft";
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 2);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 2);
        aircraftPrefab.AddComponent(TypeOf("ControleAviao"));
        objects.Add(aircraftPrefab);

        ScriptableObject aircraftCard = CreateAircraftCard(aircraftPrefab, aircraftPrice);
        Set(aircraftCard, "categoria", Enum.Parse(
            TypeOf("DadosConstrucao").GetNestedType("CategoriaItem"), "Aeronautica"));
        string itemKey = (string)aircraftCard.GetType()
            .GetMethod("GetDisplayName", Members)
            .Invoke(aircraftCard, null);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);

        Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        MethodInfo reserve = registryType.GetMethod("TryReserveProduction", BindingFlags.Static | BindingFlags.Public);
        object[] reserveArgs = { teamId, "Fighter", "command:aircraft-retry-test", 1, 0, null, Time.time, 180f };
        Assert.IsTrue((bool)reserve.Invoke(null, reserveArgs), "A fixture precisa criar uma reserva ativa válida.");
        string orderId = reserveArgs[5] as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(orderId));
        Assert.IsTrue((bool)registryType.GetMethod("ConfirmQueued", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { orderId, 0, Time.time }),
            "A ordem deve refletir o estado queued usado pelo diretor antes do dispatch.");

        Component brain = Create("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Set(brain, "TeamId", teamId);
        brain.gameObject.SetActive(true);

        PropertyInfo authorityProperty = brain.GetType().GetProperty("HasExecutionAuthority", Members);
        Assert.IsNotNull(authorityProperty);
        Assert.IsTrue((bool)authorityProperty.GetValue(brain, null),
            "O BrainMaster ativo precisa ter autoridade antes de processar a fila real.");

        object backend = Read(brain, "_backendBridge");
        Assert.IsNotNull(backend, "Awake precisa montar o backend real do BrainMaster.");
        backend.GetType().GetMethod("AddCatalogItem", Members).Invoke(backend, new object[] { aircraftCard });
        object context = brain.GetType().GetProperty("Context", Members).GetValue(brain, null);
        object worldState = Read(brain, "_worldState");
        object queue = Read(brain, "_commandQueue");
        object confirmation = Read(brain, "_commandConfirmationService");
        Assert.IsNotNull(context);
        Assert.IsNotNull(worldState);
        Assert.IsNotNull(queue);
        Assert.IsNotNull(confirmation);

        Type payloadType = TypeOf("Hegemonia.AI.BrainMaster.IA_ProduceOrderData");
        object payload = Activator.CreateInstance(payloadType);
        Set(payload, "ItemKey", itemKey);
        Set(payload, "Quantity", 1);
        Set(payload, "ProductionOrderId", orderId);
        Type requestType = TypeOf("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        object request = Activator.CreateInstance(requestType);
        Set(request, "Type", Enum.Parse(TypeOf("Hegemonia.AI.BrainMaster.IA_CommandType"), "Produce"));
        Set(request, "Origin", "aircraft retry regression");
        Set(request, "Payload", payload);

        Type queueType = TypeOf("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type confirmationType = TypeOf("Hegemonia.AI.BrainMaster.IA_CommandConfirmationService");
        MethodInfo queueEnqueue = queueType.GetMethod("Enqueue", Members);
        MethodInfo confirmationTick = confirmationType.GetMethod("Tick", Members);
        MethodInfo processCommandQueue = brain.GetType().GetMethod("ProcessCommandQueue", Members);
        Assert.IsNotNull(processCommandQueue, "O teste precisa chamar o ciclo de fila do componente real.");

        object[] enqueueArgs = { request, 10f, null };
        Assert.IsTrue((bool)queueEnqueue.Invoke(queue, enqueueArgs), Convert.ToString(enqueueArgs[2]));
        long balanceBefore = (long)Read(country, "saldo");
        processCommandQueue.Invoke(brain, new object[] { 10f });
        TrackAirportAircraft(airport);

        Assert.AreEqual(1, ObterTotalAeronaves(airport), "A primeira execução deve entregar uma aeronave.");
        Assert.AreEqual(balanceBefore - aircraftPrice, (long)Read(country, "saldo"));
        Assert.AreEqual(1, queueType.GetProperty("AwaitingConfirmationCount", Members).GetValue(queue),
            "O BrainMaster deve encaminhar a ordem aceita para confirmação do WorldState.");
        Assert.AreEqual("AwaitingConfirmation", queueType.GetMethod("GetStatus", Members)
            .Invoke(queue, new object[] { Read(request, "Id") }).ToString());
        object[] stateArgs = { orderId, Enum.Parse(TypeOf("Hegemonia.AI.Shared.IAProductionOrderState"), "Released") };
        Assert.IsTrue((bool)registryType.GetMethod("TryGetOrderState", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, stateArgs));
        Assert.AreEqual("Delivered", stateArgs[1].ToString());

        confirmationTick.Invoke(confirmation, new object[] { queue, worldState, 18.01f });
        Assert.AreEqual("Queued", queueType.GetMethod("GetStatus", Members)
            .Invoke(queue, new object[] { Read(request, "Id") }).ToString(),
            "Sem atualização no WorldState após 8 s, o serviço real deve reencaminhar a ordem.");
        Assert.AreEqual(1, queueType.GetProperty("PendingCount", Members).GetValue(queue));

        processCommandQueue.Invoke(brain, new object[] { 18.6f });
        TrackAirportAircraft(airport);
        long balanceAfterRetry = (long)Read(country, "saldo");
        int aircraftCountAfterRetry = ObterTotalAeronaves(airport);

        bool retryPreservedState = balanceAfterRetry == balanceBefore - aircraftPrice
            && aircraftCountAfterRetry == 1;
        Assert.IsTrue(retryPreservedState,
            "O retry deve ser recusado sem novo débito ou aeronave. "
            + " balance=" + balanceAfterRetry + "/" + (balanceBefore - aircraftPrice)
            + " aircraft=" + aircraftCountAfterRetry + "/1"
            + " status=" + queueType.GetMethod("GetStatus", Members)
                .Invoke(queue, new object[] { Read(request, "Id") }));
        Assert.AreEqual("Failed", queueType.GetMethod("GetStatus", Members)
            .Invoke(queue, new object[] { Read(request, "Id") }).ToString(),
            "O retry já entregue deve encerrar sem novo efeito físico.");
        Assert.AreEqual(0, queueType.GetProperty("AwaitingConfirmationCount", Members).GetValue(queue));
    }

    [UnityTest]
    public IEnumerator BrainMasterProductionContinuesToNextPoweredAirport()
    {
        const int testTeamId = 987654;
        CreateRaisedLandFixture();

        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        originalGovernmentInstance = governmentInstance.GetValue(null, null);
        governmentInstanceSetter = governmentInstance.GetSetMethod(true);
        governmentSingletonWasReplaced = true;

        Component government = Create("SistemaGovernoMundial");
        isolatedGovernmentInstance = government;
        IList countries = (IList)Read(government, "paises");
        object country = Activator.CreateInstance(countryType);
        Set(country, "teamId", testTeamId);
        Set(country, "alistaveis", 2);
        Set(country, "populacaoCivil", 0);
        Set(country, "populacaoMilitarAtiva", 0);
        countries.Add(country);
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

        Component unpoweredAirport = CreatePlayerAirport(new Vector3(40f, 30f, 40f));
        unpoweredAirport.name = "Test_BrainMaster_UnpoweredAirport";
        Set(unpoweredAirport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", testTeamId);
        Component poweredAirport = CreatePlayerAirport(new Vector3(60f, 30f, 60f));
        poweredAirport.name = "Test_BrainMaster_PoweredAirport";
        Set(poweredAirport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", testTeamId);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_BrainMaster_MultiAirportAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", testTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);

        yield return null;

        Type airportType = TypeOf("GerenciadorAeroporto");
        Type registryType = TypeOf("RegistroEntidadesJogo");
        IList registeredAirports = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(airportType));
        registryType.GetMethod("FillAeroportos", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { registeredAirports });
        List<Component> teamAirports = registeredAirports.Cast<Component>()
            .Where(airport => airport != null
                && airport.GetComponent(TypeOf("IdentidadeUnidade")) != null
                && (int)Read(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID") == testTeamId)
            .ToList();
        Assert.AreEqual(2, teamAirports.Count, "A fixture precisa registrar os dois aeroportos do país de teste.");
        Component disabledAirport = teamAirports[0];
        Component enabledAirport = teamAirports[1];
        Set(disabledAirport, "semEnergia", true);
        Set(enabledAirport, "semEnergia", false);
        string airportSnapshot = string.Join(", ", teamAirports.Select(airport =>
            airport.name + " semEnergia=" + Read(airport, "semEnergia")));

        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object backend = Activator.CreateInstance(backendType, new object[] { testTeamId });
        object productionService = backendType.GetProperty("ProductionService").GetValue(backend, null);
        MethodInfo produceAircraft = productionService.GetType().GetMethod(
            "ProduceAircraftTracked",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(produceAircraft);

        object[] arguments = { item, null, "ia-brainmaster-multi-airport-test", null };
        GameObject result = produceAircraft.Invoke(productionService, arguments) as GameObject;
        TrackAirportAircraft(disabledAirport);
        TrackAirportAircraft(enabledAirport);
        int unpoweredAirportAircraft = ObterTotalAeronaves(disabledAirport);
        int poweredAirportAircraft = ObterTotalAeronaves(enabledAirport);
        IList poweredHangar = Read(enabledAirport, "avioesNoHangar") as IList;
        Component deliveredAircraft = poweredHangar != null && poweredHangar.Count > 0
            ? poweredHangar[0] as Component
            : null;

        Assert.AreSame(aircraftPrefab, result,
            "O BrainMaster deve ignorar um aeroporto sem energia e tentar o próximo aeroporto do mesmo país. "
            + "Motivo retornado: " + arguments[1] + ". Registro: " + airportSnapshot);
        Assert.AreEqual(0, unpoweredAirportAircraft, "O aeroporto sem energia não deve receber aeronaves.");
        Assert.AreEqual(1, poweredAirportAircraft, "O aeroporto energizado deve registrar uma aeronave.");
        Assert.IsNotNull(deliveredAircraft, "A aeronave deve permanecer registrada no hangar do aeroporto energizado.");
        Assert.AreEqual(string.Empty, arguments[1]);
        Assert.AreEqual(testTeamId, (int)Read(deliveredAircraft.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"));
    }

    [UnityTest]
    public IEnumerator BrainMasterAndNovaIAChooseValidAirportWhenWaterSpawnIsPresent()
    {
        const int testTeamId = 987655;
        Type productionRegistryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        productionRegistrySnapshot = productionRegistryType.GetMethod("CaptureSaveData", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, null);
        productionRegistrySnapshotCaptured = productionRegistrySnapshot != null;
        Assert.IsTrue(productionRegistrySnapshotCaptured, "O teste precisa preservar as reservas preexistentes.");
        CreateRaisedLandFixture();

        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public);
        originalGovernmentInstance = governmentInstance.GetValue(null, null);
        governmentInstanceSetter = governmentInstance.GetSetMethod(true);
        governmentSingletonWasReplaced = true;

        Component government = Create("SistemaGovernoMundial");
        isolatedGovernmentInstance = government;
        IList countries = (IList)Read(government, "paises");
        object country = Activator.CreateInstance(countryType);
        Set(country, "teamId", testTeamId);
        Set(country, "alistaveis", 4);
        Set(country, "populacaoCivil", 0);
        Set(country, "populacaoMilitarAtiva", 0);
        countries.Add(country);
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });

        Component airportA = CreatePlayerAirport(new Vector3(40f, 30f, 40f));
        Set(airportA.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", testTeamId);
        Component airportB = CreatePlayerAirport(new Vector3(60f, 30f, 60f));
        Set(airportB.GetComponent(TypeOf("IdentidadeUnidade")), "teamID", testTeamId);
        yield return null;

        Type airportType = TypeOf("GerenciadorAeroporto");
        Type registryType = TypeOf("RegistroEntidadesJogo");
        IList registeredAirports = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(airportType));
        registryType.GetMethod("FillAeroportos", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { registeredAirports });
        List<Component> teamAirports = registeredAirports.Cast<Component>()
            .Where(airport => airport != null
                && airport.GetComponent(TypeOf("IdentidadeUnidade")) != null
                && (int)Read(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID") == testTeamId)
            .ToList();
        Assert.AreEqual(2, teamAirports.Count, "A fixture precisa registrar exatamente os dois aeroportos do país de teste.");

        Component firstAirport = teamAirports[0];
        Component secondAirport = teamAirports[1];
        firstAirport.name = "Test_RejectingAirport";
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Test_WaterPlane_FirstAirportSpawn";
        water.transform.position = new Vector3(400f, 0f, 400f);
        water.transform.localScale = new Vector3(30f, 1f, 30f);
        objects.Add(water);
        firstAirport.transform.position = water.transform.position;
        Physics.SyncTransforms();
        MethodInfo isWaterAtPosition = TypeOf("NavalPlacementResolver").GetMethod(
            "IsWaterAtPosition",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Vector3) },
            null);
        Assert.IsNotNull(isWaterAtPosition);
        Assert.IsTrue((bool)isWaterAtPosition.Invoke(null, new object[] { firstAirport.transform.position }),
            "A fixture precisa fazer o primeiro aeroporto rejeitar o spawn por estar sobre água.");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_MultiAirportAircraft";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", testTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 1500L);
        Set(item, "categoria", Enum.Parse(TypeOf("DadosConstrucao").GetNestedType("CategoriaItem"), "Aeronautica"));

        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object backend = Activator.CreateInstance(backendType, new object[] { testTeamId });
        object productionService = backendType.GetProperty("ProductionService").GetValue(backend, null);
        MethodInfo produceAircraft = productionService.GetType().GetMethod(
            "ProduceAircraftTracked",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(produceAircraft);
        MethodInfo reserveProduction = productionRegistryType.GetMethod("TryReserveProduction", BindingFlags.Static | BindingFlags.Public);
        object[] brainReservationArguments =
            { testTeamId, "Fighter", "brainmaster-airport-fallback-test", 1, 0, null, Time.time, 180f };
        Assert.IsTrue((bool)reserveProduction.Invoke(null, brainReservationArguments),
            "O teste precisa iniciar com uma ordem real reservada no registro.");
        string brainOrderId = brainReservationArguments[5] as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(brainOrderId), "A reserva do BrainMaster precisa ter um ID real.");
        MethodInfo getProductionOrderState = productionRegistryType.GetMethod("TryGetOrderState", BindingFlags.Static | BindingFlags.Public);
        object[] initialBrainOrderState = { brainOrderId, Enum.Parse(TypeOf("Hegemonia.AI.Shared.IAProductionOrderState"), "Released") };
        Assert.IsTrue((bool)getProductionOrderState.Invoke(null, initialBrainOrderState));
        Assert.AreEqual("Reserved", initialBrainOrderState[1].ToString(), "A rota precisa começar com a reserva ativa.");
        string brainStateAtPhysicalAcceptance = null;
        System.Action captureBrainAcceptanceState = () =>
        {
            object[] stateArguments = { brainOrderId, Enum.Parse(TypeOf("Hegemonia.AI.Shared.IAProductionOrderState"), "Reserved") };
            getProductionOrderState.Invoke(null, stateArguments);
            brainStateAtPhysicalAcceptance = stateArguments[1].ToString();
        };
        object[] brainArguments = { item, null, brainOrderId, captureBrainAcceptanceState };
        GameObject brainResult;
        bool ignoreBrainLogsBefore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            brainResult = produceAircraft.Invoke(productionService, brainArguments) as GameObject;
        }
        finally
        {
            LogAssert.ignoreFailingMessages = ignoreBrainLogsBefore;
        }
        TrackAirportAircraft(firstAirport);
        TrackAirportAircraft(secondAirport);
        Assert.AreEqual("Delivered", brainStateAtPhysicalAcceptance,
            "O aeroporto que aceita a unidade deve concluir a reserva antes de notificar o chamador.");
        Assert.AreSame(aircraftPrefab, brainResult,
            "BrainMaster deve continuar para o segundo aeroporto depois da recusa do primeiro. Motivo: " + brainArguments[1]);
        Assert.AreEqual(0, ObterTotalAeronaves(firstAirport), "O primeiro aeroporto recusado não deve receber aeronave.");
        Assert.AreEqual(1, ObterTotalAeronaves(secondAirport), "BrainMaster deve registrar uma aeronave no segundo aeroporto.");
        object[] brainOrderStateArguments = { brainOrderId, Enum.Parse(TypeOf("Hegemonia.AI.Shared.IAProductionOrderState"), "Reserved") };
        Assert.IsTrue((bool)getProductionOrderState.Invoke(null, brainOrderStateArguments),
            "A ordem do BrainMaster deve continuar registrada após o fallback.");
        Assert.AreEqual("Delivered", brainOrderStateArguments[1].ToString(),
            "A ordem do BrainMaster deve terminar como entregue quando o segundo aeroporto aceita a unidade.");

        registeredAirports.Clear();
        registryType.GetMethod("FillAeroportos", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { registeredAirports });
        List<Component> novaAirports = registeredAirports.Cast<Component>()
            .Where(airport => airport != null
                && airport.GetComponent(TypeOf("IdentidadeUnidade")) != null
                && (int)Read(airport.GetComponent(TypeOf("IdentidadeUnidade")), "teamID") == testTeamId)
            .ToList();
        Assert.AreEqual(2, novaAirports.Count);
        Component novaRejectingAirport = novaAirports[0];
        Component novaReceivingAirport = novaAirports[1];
        novaRejectingAirport.name = "Test_NovaIA_RejectingAirport";
        novaRejectingAirport.transform.position = water.transform.position;
        novaReceivingAirport.transform.position = new Vector3(60f, 30f, 60f);
        int novaRejectedCountBefore = ObterTotalAeronaves(novaRejectingAirport);
        int novaReceivingCountBefore = ObterTotalAeronaves(novaReceivingAirport);
        Physics.SyncTransforms();

        Component controller = Create("Hegemonia.AI.Master.IA_MasterController");
        Set(controller, "_teamId", testTeamId);
        MethodInfo tryProduce = controller.GetType().GetMethod("TryProduceViaInfrastructure", Members);
        Assert.IsNotNull(tryProduce);
        object[] novaReservationArguments =
            { testTeamId, "Fighter", "novaia-airport-fallback-test", 1, 0, null, Time.time, 180f };
        Assert.IsTrue((bool)reserveProduction.Invoke(null, novaReservationArguments),
            "O teste precisa iniciar a rota da NovaIA com uma ordem real reservada.");
        string novaOrderId = novaReservationArguments[5] as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(novaOrderId), "A reserva da NovaIA precisa ter um ID real.");
        object[] novaArguments = { item, novaOrderId, null };
        bool novaResult;
        bool ignoreNovaLogsBefore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            novaResult = (bool)tryProduce.Invoke(controller, novaArguments);
        }
        finally
        {
            LogAssert.ignoreFailingMessages = ignoreNovaLogsBefore;
        }
        TrackAirportAircraft(novaRejectingAirport);
        TrackAirportAircraft(novaReceivingAirport);

        Assert.IsTrue(novaResult, "NovaIA deve continuar para o segundo aeroporto depois da recusa do primeiro.");
        Assert.AreEqual(novaRejectedCountBefore, ObterTotalAeronaves(novaRejectingAirport),
            "NovaIA não deve registrar unidade no aeroporto que recusou o spawn.");
        Assert.AreEqual(novaReceivingCountBefore + 1, ObterTotalAeronaves(novaReceivingAirport),
            "NovaIA deve registrar exatamente mais uma aeronave no próximo aeroporto utilizável.");
        object[] novaOrderStateArguments = { novaOrderId, Enum.Parse(TypeOf("Hegemonia.AI.Shared.IAProductionOrderState"), "Reserved") };
        Assert.IsTrue((bool)getProductionOrderState.Invoke(null, novaOrderStateArguments),
            "A ordem da NovaIA deve continuar registrada após o fallback.");
        Assert.AreEqual("Delivered", novaOrderStateArguments[1].ToString(),
            "A ordem da NovaIA deve terminar como entregue quando o segundo aeroporto aceita a unidade.");
    }

    [UnityTest]
    public IEnumerator PlayerAirportWithoutIdentityDoesNotInheritPrefabTeam()
    {
        CreateRaisedLandFixture();
        PrepareIsolatedPlayerGovernment(2);

        IList countries = (IList)Read(isolatedGovernmentInstance, "paises");
        Type countryType = TypeOf("DadosPaisGoverno");
        object rivalCountry = Activator.CreateInstance(countryType);
        Set(rivalCountry, "teamId", 2);
        Set(rivalCountry, "alistaveis", 2);
        Set(rivalCountry, "populacaoCivil", 0);
        Set(rivalCountry, "populacaoMilitarAtiva", 0);
        countries.Add(rivalCountry);

        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Test_Airport_WithoutIdentity";
        airport.transform.position = new Vector3(50f, 30f, 50f);
        Set(airport, "semEnergia", false);
        airport.gameObject.SetActive(true);
        Component resources = PrepareResources(5000L);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test_AircraftPrefab_RivalTeam";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", 2);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        objects.Add(aircraftPrefab);

        const long price = 1500L;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", (int)price);

        yield return null;
        governmentInstanceSetter.Invoke(null, new[] { isolatedGovernmentInstance });
        Set(airport, "semEnergia", false);

        long balanceBefore = (long)Read(resources, "dinheiro");
        int aircraftBefore = ObterTotalAeronaves(airport);
        bool accepted = (bool)airport.GetType().GetMethod("TentarComprarAviaoComPagamento", Members)
            .Invoke(airport, new object[] { aircraftPrefab, price });

        yield return null;
        TrackAirportAircraft(airport);

        IList patio = (IList)Read(airport, "avioesNoPatio");
        IList hangar = (IList)Read(airport, "avioesNoHangar");
        Component spawnedAircraft = patio.Cast<object>().Concat(hangar.Cast<object>())
            .Select(entry => entry as Component)
            .FirstOrDefault(entry => entry != null);

        Assert.IsTrue(accepted, "O aeroporto sem identidade é tratado como base do jogador pelo fluxo de compra.");
        Assert.AreEqual(aircraftBefore + 1, ObterTotalAeronaves(airport), "A compra deve registrar uma única aeronave.");
        Assert.IsNotNull(spawnedAircraft, "A aeronave precisa estar registrada no pátio ou hangar.");
        Assert.AreEqual(1, (int)Read(spawnedAircraft.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"),
            "Uma base legada sem identidade não pode deixar o teamID do prefab transformar uma compra do jogador em aeronave rival.");
        Assert.AreEqual(balanceBefore - price, (long)Read(resources, "dinheiro"), "A compra válida deve debitar o preço uma vez.");
        Assert.AreEqual(0, (int)Read(countries.Cast<object>().First(country => (int)Read(country, "teamId") == 1), "alistaveis"),
            "O efetivo do jogador deve ser mobilizado para a aeronave entregue na base do jogador.");
        Assert.AreEqual(2, (int)Read(rivalCountry, "alistaveis"), "A aeronave do jogador não deve consumir efetivo do rival.");
    }
}
