using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ReformaAircraftPlayModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Component originalResourcesManager;
    private long originalResourceBalance;
    private float originalMoneyPerSecond;
    private bool originalAutomaticIncome;
    private Component originalConstructionMenu;
    private Component originalMenuManager;
    private GameObject originalMenuPanel;
    private Component originalMenuCanvasGroup;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Read(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);

    private void ConfigureMinimalLandingRoute(Component airport)
    {
        var decida = (IList)Read(airport, "waypointsDecida");
        for (int i = 0; i < 2; i++)
        {
            var marker = new GameObject("Regression_LandingPoint_" + i);
            objects.Add(marker);
            marker.transform.position = new Vector3(0f, 181f, i * 20f);
            decida.Add(marker.transform);
        }
    }

    private Component Create(string name)
    {
        var go = new GameObject("Regression_" + name);
        go.SetActive(false);
        objects.Add(go);
        return go.AddComponent(TypeOf(name));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
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
        foreach (var go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        objects.Clear();
        foreach (var asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        assets.Clear();
        yield return null;
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
        for (int x = 0; x < 33; x++)
            heights[z, x] = 1f;
        data.SetHeights(0, 0, heights);
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Regression_AirportTransactionLand";
        objects.Add(go);
        return go.GetComponent<Terrain>();
    }

    private Component CreatePlayerAirport(Vector3 position)
    {
        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Regression_AirportTransaction";
        airport.transform.position = position;
        Component identity = airport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        airport.gameObject.SetActive(true);
        return airport;
    }

    private Component PrepareResources(long balance)
    {
        Type type = TypeOf("GerenciadorRecursos");
        originalResourcesManager = type.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null) as Component;
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
            var panel = new GameObject("Regression_ConstructionPanel");
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
        item.GetType().GetProperty("NomeItem").SetValue(item, "Jato de regressão", null);
        item.GetType().GetProperty("PrefabDaUnidade").SetValue(item, prefab, null);
        Set(item, "precoDefinitivo", price);
        return item;
    }

    [UnityTest]
    public IEnumerator AircraftAtGlobalMapCoordinatesKeepsItsWaypointAndClimbsTowardCommandedAltitude()
    {
        var world = (ScriptableObject)ScriptableObject.CreateInstance(TypeOf("GlobalWorldDefinition"));
        assets.Add(world);
        Set(world, "worldSize", new Vector2(512000f, 512000f));
        Set(world, "mapFootprintHeight", 288000f);

        Component aircraft = Create("ControleAviao");
        Vector3 initialPosition = new Vector3(-196000f, 15f, 80000f);
        Vector3 waypoint = new Vector3(-195000f, 181f, 80000f);
        aircraft.transform.SetPositionAndRotation(
            initialPosition,
            Quaternion.LookRotation((waypoint - initialPosition).normalized, Vector3.up));
        Set(aircraft, "definicaoLimitesVoo", world);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Set(aircraft, "alvoGPSVoo", waypoint);
        Set(aircraft, "altitudeVoo", 181f);
        Set(aircraft, "velocidadeMaximaVoo", 110f);
        Set(aircraft, "velocidadeVooAtual", 110f);
        Set(aircraft, "taxaDeGiroLeme", 45f);
        aircraft.gameObject.SetActive(true);
        ((MonoBehaviour)aircraft).enabled = false;

        MethodInfo maneuver = TypeOf("ControleAviao").GetMethod("ManobraVooRealista", Members);
        Assert.IsNotNull(maneuver);
        float start = Time.realtimeSinceStartup;
        float[] samples = { 1f, 3f, 5f };
        float altitudeAtOneSecond = initialPosition.y;

        Debug.Log($"[AircraftMapBounds] t=0 y={aircraft.transform.position.y:F1} altitudeVoo=181 targetY={waypoint.y:F1} state=EmMissao");
        foreach (float sampleAt in samples)
        {
            while (Time.realtimeSinceStartup - start < sampleAt)
            {
                yield return null;
                maneuver.Invoke(aircraft, new object[] { 1f });
            }

            Vector3 current = aircraft.transform.position;
            Vector3 currentTarget = (Vector3)Read(aircraft, "alvoGPSVoo");
            Assert.AreEqual(waypoint.x, currentTarget.x, 0.01f,
                "Um waypoint válido dentro do mapa global não deve ser substituído pelo centro.");
            Assert.AreEqual(waypoint.z, currentTarget.z, 0.01f);
            Assert.AreEqual(waypoint.y, currentTarget.y, 0.01f,
                "A correção de limites não deve rebaixar a altitude ordenada.");
            Assert.That(current.x, Is.InRange(-256000f, 256000f));
            Assert.That(current.z, Is.InRange(-144000f, 144000f));
            if (Mathf.Approximately(sampleAt, 1f)) altitudeAtOneSecond = current.y;
            Debug.Log($"[AircraftMapBounds] t={sampleAt:F0} y={current.y:F1} AGL=sem terreno no fixture altitudeVoo=181 targetY={currentTarget.y:F1} state=EmMissao");
        }

        Assert.Greater(altitudeAtOneSecond, initialPosition.y,
            "O avião deve começar a subir quando o waypoint ordena altitude maior que a altitude inicial.");

        MethodInfo clamp = TypeOf("ControleAviao").GetMethod("LimitarPosicaoAosLimitesDoMapa", Members);
        Assert.IsNotNull(clamp);
        Vector3 correctedOutsideWaypoint = (Vector3)clamp.Invoke(
            aircraft, new object[] { new Vector3(-300000f, 350f, 180000f) });
        Assert.AreEqual(-256000f, correctedOutsideWaypoint.x, 0.01f);
        Assert.AreEqual(350f, correctedOutsideWaypoint.y, 0.01f,
            "A correção horizontal não pode alterar a altitude Y do destino.");
        Assert.AreEqual(144000f, correctedOutsideWaypoint.z, 0.01f);
    }

    [UnityTest]
    public IEnumerator AirportPurchaseReturnsSuccessOnlyAfterSpawningAircraft()
    {
        var terrainData = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(100f, 30f, 100f)
        };
        assets.Add(terrainData);
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        for (int x = 0; x < 33; x++)
            heights[z, x] = 1f;
        terrainData.SetHeights(0, 0, heights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "Regression_AirportPurchaseLand";
        objects.Add(terrainObject);

        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Regression_AirportPurchase";
        airport.transform.position = new Vector3(50f, 30f, 50f);
        Component identity = airport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        airport.gameObject.SetActive(true);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Regression_AircraftPurchasePrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);
        yield return null;

        bool purchased = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
            .Invoke(airport, new object[] { aircraftPrefab });
        yield return null;

        Assert.IsTrue(purchased, "O aeroporto deve confirmar somente um spawn realmente criado.");
        Assert.AreEqual(1, ((IList)Read(airport, "avioesNoHangar")).Count,
            "Sem vaga de pátio, a aeronave criada deve ficar guardada no hangar.");
    }

    [UnityTest]
    public IEnumerator AirportPurchaseRejectedOnWaterDoesNotReportSuccessOrSpawn()
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Regression_Water_Surface";
        water.transform.position = new Vector3(5000f, 0f, 5000f);
        water.transform.localScale = new Vector3(100f, 2f, 100f);
        objects.Add(water);

        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Regression_AirportOnWater";
        airport.transform.position = new Vector3(5000f, 0f, 5000f);
        Component identity = airport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        airport.gameObject.SetActive(true);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Regression_AircraftWaterPrefab";
        aircraftPrefab.transform.position = new Vector3(5200f, 20f, 5200f);
        objects.Add(aircraftPrefab);
        yield return null;

        LogAssert.Expect(LogType.Error,
            "[Aeroporto] Spawn aéreo bloqueado em água: Regression_AirportOnWater ((5000.0, 0.0, 5000.0)). Corrija o ponto Preparacao/pista da base.");
        bool purchased = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
            .Invoke(airport, new object[] { aircraftPrefab });

        Assert.IsFalse(purchased, "Um spawn bloqueado não pode ser registrado como compra concluída.");
        Assert.IsNull(GameObject.Find("Regression_AircraftWaterPrefab(Clone)"),
            "Uma compra rejeitada não pode deixar aeronave no mundo.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MenuAircraftPurchaseDebitsOnceAfterOneSuccessfulSpawn()
    {
        CreateRaisedLandFixture();
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Component resources = PrepareResources(1000L);
        Component gameManager = Create("GerenteDeJogo");
        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Regression_MenuAircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 200L);
        Component menu = PrepareConstructionMenu(gameManager);

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        Assert.AreEqual(800L, Read(resources, "dinheiro"),
            "Uma aeronave entregue deve debitar exatamente um preço.");
        Assert.AreEqual(1, ((IList)Read(airport, "avioesNoHangar")).Count,
            "Sem vagas no pátio, a aeronave comprada deve ficar registrada no hangar.");
    }

    [UnityTest]
    public IEnumerator MenuAircraftPurchaseRefundsWhenAirportRejectsWaterSpawn()
    {
        CreateRaisedLandFixture();
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Regression_Water_Surface";
        water.transform.position = new Vector3(5000f, 0f, 5000f);
        water.transform.localScale = new Vector3(100f, 2f, 100f);
        objects.Add(water);

        Component airport = CreatePlayerAirport(new Vector3(5000f, 0f, 5000f));
        Component resources = PrepareResources(1000L);
        Component gameManager = Create("GerenteDeJogo");
        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Regression_MenuAircraftWaterPrefab";
        aircraftPrefab.transform.position = new Vector3(5200f, 20f, 5200f);
        objects.Add(aircraftPrefab);
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, 200L);
        Component menu = PrepareConstructionMenu(gameManager);

        LogAssert.Expect(LogType.Error,
            "[Aeroporto] Spawn aéreo bloqueado em água: Regression_AirportTransaction ((5000.0, 0.0, 5000.0)). Corrija o ponto Preparacao/pista da base.");
        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });
        yield return null;

        Assert.AreEqual(1000L, Read(resources, "dinheiro"),
            "Uma compra rejeitada pelo aeroporto deve devolver o débito integral.");
        Assert.AreEqual(0, ((IList)Read(airport, "avioesNoHangar")).Count);
        Assert.AreEqual(0, ((IList)Read(airport, "avioesNoPatio")).Count);
    }

    [UnityTest]
    public IEnumerator AircraftMaintainsCruiseAltitudeAboveElevatedTerrain()
    {
        var terrainData = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(4000f, 1000f, 4000f)
        };
        assets.Add(terrainData);
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
                heights[z, x] = 0.5f;
        terrainData.SetHeights(0, 0, heights);
        var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        objects.Add(terrainObject);
        terrainObject.transform.position = new Vector3(99000f, 0f, 99000f);
        Terrain terrain = terrainObject.GetComponent<Terrain>();

        Component aircraft = Create("ControleAviao");
        Vector3 initialPosition = new Vector3(100000f, 515f, 100000f);
        Vector3 waypoint = new Vector3(100500f, 181f, 100000f);
        aircraft.transform.SetPositionAndRotation(
            initialPosition,
            Quaternion.LookRotation((waypoint - initialPosition).normalized, Vector3.up));
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Set(aircraft, "alvoGPSVoo", waypoint);
        Set(aircraft, "altitudeVoo", 181f);
        Set(aircraft, "velocidadeMaximaVoo", 110f);
        Set(aircraft, "velocidadeVooAtual", 110f);
        Set(aircraft, "taxaDeGiroLeme", 45f);
        aircraft.gameObject.SetActive(true);
        ((MonoBehaviour)aircraft).enabled = false;

        MethodInfo maneuver = TypeOf("ControleAviao").GetMethod("ManobraVooRealista", Members);
        Assert.IsNotNull(maneuver);
        maneuver.Invoke(aircraft, new object[] { 1f });

        Vector3 currentTarget = (Vector3)Read(aircraft, "alvoGPSVoo");
        float groundHeight = terrain.SampleHeight(waypoint) + terrain.transform.position.y;
        Debug.Log($"[AircraftTerrainClearance] ground={groundHeight:F1} targetY={currentTarget.y:F1} aircraftY={aircraft.transform.position.y:F1}");

        Assert.GreaterOrEqual(currentTarget.y, groundHeight + 181f,
            "Altitude de cruzeiro é AGL e precisa compensar elevação do terreno no waypoint.");
        Assert.Greater(aircraft.transform.position.y, groundHeight,
            "A aeronave deve permanecer acima do terreno elevado durante a subida.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator RefuelledAircraftResumesTheExistingPatrol()
    {
        Component airport = Create("GerenciadorAeroporto");
        ConfigureMinimalLandingRoute(airport);
        Component unit = Create("ControleUnidade");
        Component aircraft = Create("ControleAviao");
        aircraft.gameObject.SetActive(true);
        yield return null; // Initialize the aircraft's component caches first.
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "_controleUnidade", unit);
        Set(unit, "ordemControleAtual", Enum.Parse(TypeOf("OrdemControleUnidade"), "Patrulhando"));
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ProntoNoPatio"));
        var route = (IList)Read(aircraft, "rotaPatrulhaSalva");
        route.Add(new Vector3(500f, 181f, 500f));
        route.Add(new Vector3(1000f, 181f, 500f));
        route.Add(new Vector3(1000f, 181f, 1000f));
        route.Add(new Vector3(500f, 181f, 1000f));
        var routine = (IEnumerator)aircraft.GetType().GetMethod("RotinaRetomarMissaoAposReabastecimento", Members).Invoke(aircraft, null);
        ((MonoBehaviour)aircraft).StartCoroutine(routine);
        yield return new WaitForSeconds(2f);
        Assert.AreEqual("EmMissao", Read(aircraft, "estadoAtual").ToString());
        Assert.IsTrue((bool)Read(aircraft, "estaEmModoVooFisico"));
        Assert.AreEqual(4, route.Count);
    }

    [UnityTest]
    public IEnumerator AircraftLandingServiceRefuelsRearmsAndResumesPatrol()
    {
        Component airport = Create("GerenciadorAeroporto");
        ConfigureMinimalLandingRoute(airport);
        Component unit = Create("ControleUnidade");
        Component aircraft = Create("ControleAviao");
        Component fuel = aircraft.gameObject.AddComponent(TypeOf("CombustivelUnidade"));
        Component launcher = aircraft.gameObject.AddComponent(TypeOf("LancadorMisselCaca"));

        // OnValidate auto-binds a missile prefab in the Editor. Keep this test
        // on the missing-prefab service path it is meant to exercise.
        Set(launcher, "missilCacaPrefab", null);
        LogAssert.Expect(LogType.Error, "[LancadorMisselCaca] Regression_ControleAviao está sem missilCacaPrefab; o caça não poderá disparar.");
        aircraft.gameObject.SetActive(true);
        yield return null;

        Set(fuel, "capacidade", 100f);
        Set(fuel, "combustivelAtual", 20f);
        Set(launcher, "municaoMaxima", 4);
        Set(launcher, "municaoAtual", 0);
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "_controleUnidade", unit);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ProntoNoPatio"));
        Set(unit, "ordemControleAtual", Enum.Parse(TypeOf("OrdemControleUnidade"), "Patrulhando"));

        var route = (IList)Read(aircraft, "rotaPatrulhaSalva");
        route.Add(new Vector3(30f, 80f, 0f));
        route.Add(new Vector3(-30f, 80f, 0f));
        Set(aircraft, "retomarMissaoAposAbastecer", true);

        MethodInfo service = aircraft.GetType().GetMethod("ProcessarServicoDeBaseAposPouso", Members);
        MethodInfo resume = aircraft.GetType().GetMethod("ProcessarRetomadaAposReabastecimento", Members);
        Assert.IsNotNull(service);
        Assert.IsNotNull(resume);
        service.Invoke(aircraft, null);
        Assert.AreEqual(100f, (float)Read(fuel, "combustivelAtual"));
        Assert.AreEqual(4, (int)Read(launcher, "municaoAtual"));
        resume.Invoke(aircraft, null);

        yield return new WaitForSeconds(2.5f);

        Assert.Greater((float)Read(fuel, "combustivelAtual"), 95f);
        Assert.AreEqual(4, (int)Read(launcher, "municaoAtual"));
        Assert.AreEqual("EmMissao", Read(aircraft, "estadoAtual").ToString());
        Assert.IsTrue((bool)Read(aircraft, "estaEmModoVooFisico"));
        Assert.AreEqual(2, route.Count);
    }

    [UnityTest]
    public IEnumerator AircraftUpdatesPatrolAreaWhileAlreadyFlying()
    {
        Component aircraft = Create("ControleAviao");
        aircraft.gameObject.SetActive(true);
        yield return null;

        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Set(aircraft, "estaEmModoVooFisico", true);
        Set(aircraft, "velocidadeMaximaVoo", 180f);
        Set(aircraft, "velocidadeVooAtual", 120f);
        aircraft.transform.position = new Vector3(0f, 100f, 0f);

        var novaRota = new List<Vector3>
        {
            new Vector3(0f, 100f, 120f),
            new Vector3(120f, 100f, 120f)
        };
        MethodInfo ordem = aircraft.GetType().GetMethod("ReceberOrdemPatrulha", Members);
        Assert.IsNotNull(ordem);
        Assert.IsTrue((bool)ordem.Invoke(aircraft, new object[] { novaRota }));
        IList rotaSalva = (IList)Read(aircraft, "rotaPatrulhaSalva");
        Vector3 pontoSalvo = (Vector3)rotaSalva[0];
        Vector3 alvoAplicado = (Vector3)Read(aircraft, "alvoGPSVoo");
        Assert.AreEqual(pontoSalvo.x, alvoAplicado.x);
        Assert.AreEqual(pontoSalvo.z, alvoAplicado.z);
        Assert.GreaterOrEqual(alvoAplicado.y, 60f);

        yield return new WaitForSeconds(1f);

        Assert.Greater(aircraft.transform.position.z, 10f,
            "A aeronave em voo manteve o destino antigo depois da troca de patrulha.");
        Assert.GreaterOrEqual(rotaSalva.Count, 2);
    }

    [UnityTest]
    public IEnumerator HelicopterManualClickUsesTheCentralMovementGateway()
    {
        GameObject helicopterObject = new GameObject("Regression_HelicopterManualOrder");
        objects.Add(helicopterObject);
        helicopterObject.SetActive(false);
        Component helicopter = helicopterObject.AddComponent(TypeOf("Helicoptero"));
        Component control = helicopterObject.AddComponent(TypeOf("ControleUnidade"));
        helicopterObject.SetActive(true);
        yield return null;

        MethodInfo manualOrder = helicopter.GetType().GetMethod(
            "ReceberOrdemCliqueManual", Members);
        Assert.IsNotNull(manualOrder);

        Vector3 destination = new Vector3(0f, 120f, 180f);
        Assert.IsTrue((bool)manualOrder.Invoke(helicopter, new object[] { destination, false }));
        Assert.AreEqual("Movendo", Read(control, "ordemControleAtual").ToString());
        Assert.AreEqual("Helicoptero", Read(control, "executorControleAtual"));
        Assert.IsTrue((bool)Read(control, "possuiDestinoOrdenado"));

        yield return null;
    }

    [UnityTest]
    public IEnumerator CompositeHelicopterReusesTheParentMovementController()
    {
        GameObject root = new GameObject("Regression_CompositeHelicopterRoot");
        objects.Add(root);
        root.SetActive(false);
        root.AddComponent(TypeOf("ControleUnidade"));

        GameObject child = new GameObject("Regression_CompositeHelicopter");
        objects.Add(child);
        child.transform.SetParent(root.transform, false);
        child.AddComponent(TypeOf("Helicoptero"));

        root.SetActive(true);
        yield return null;

        Component[] controllers = root.GetComponentsInChildren(TypeOf("ControleUnidade"), true);
        Assert.AreEqual(1, controllers.Length,
            "Um helicóptero composto não pode criar um segundo controlador no filho.");
    }

    [UnityTest]
    public IEnumerator RejectedAircraftOrderDoesNotLeaveTheCentralControllerMoving()
    {
        GameObject aircraftObject = new GameObject("Regression_RejectedAircraftOrder");
        objects.Add(aircraftObject);
        aircraftObject.SetActive(false);
        Component aircraft = aircraftObject.AddComponent(TypeOf("ControleAviao"));
        Component control = aircraftObject.AddComponent(TypeOf("ControleUnidade"));
        aircraftObject.SetActive(true);
        yield return null;

        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ProntoNoPatio"));
        MethodInfo move = control.GetType().GetMethod(
            "EmitirOrdemMover", Members, null, new[] { typeof(Vector3), typeof(bool) }, null);
        Assert.IsNotNull(move);

        Assert.IsFalse((bool)move.Invoke(control, new object[] { new Vector3(0f, 120f, 250f), true }));
        Assert.AreEqual("Ociosa", Read(control, "ordemControleAtual").ToString());
        Assert.IsFalse((bool)Read(control, "possuiDestinoOrdenado"));
    }

    [UnityTest]
    public IEnumerator AircraftKeepsPendingOrderWhenTheAirportTemporarilyStoresIt()
    {
        GameObject aircraftObject = new GameObject("Regression_PendingAircraftOrder");
        objects.Add(aircraftObject);
        aircraftObject.SetActive(false);
        Component aircraft = aircraftObject.AddComponent(TypeOf("ControleAviao"));
        aircraftObject.SetActive(true);
        yield return null;

        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ReservaHangar"));
        Set(aircraft, "missaoManualPendente", true);
        aircraftObject.SetActive(false);
        aircraftObject.SetActive(true);
        yield return null;

        Assert.IsTrue((bool)Read(aircraft, "missaoManualPendente"),
            "Desativar uma aeronave para guardá-la no hangar não pode apagar a ordem pendente.");
    }

    [UnityTest]
    public IEnumerator StopCancelsAnAircraftOrderWaitingForTheHangar()
    {
        GameObject aircraftObject = new GameObject("Regression_CancelPendingAircraftOrder");
        objects.Add(aircraftObject);
        aircraftObject.SetActive(false);
        Component aircraft = aircraftObject.AddComponent(TypeOf("ControleAviao"));
        Component control = aircraftObject.AddComponent(TypeOf("ControleUnidade"));
        aircraftObject.SetActive(true);
        yield return null;

        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ReservaHangar"));
        Set(aircraft, "missaoManualPendente", true);
        Set(control, "ordemControleAtual", Enum.Parse(TypeOf("OrdemControleUnidade"), "Movendo"));

        MethodInfo stop = control.GetType().GetMethod("EmitirOrdemParar", Members);
        Assert.IsNotNull(stop);
        Assert.IsTrue((bool)stop.Invoke(control, null));
        Assert.IsFalse((bool)Read(aircraft, "missaoManualPendente"));
        Assert.IsFalse((bool)Read(aircraft, "patrulhaPendente"));
    }

    [UnityTest]
    public IEnumerator AircraftResumesRefuelledPatrolAfterReturningFromTheHangar()
    {
        Component airport = Create("GerenciadorAeroporto");
        ConfigureMinimalLandingRoute(airport);
        GameObject aircraftObject = new GameObject("Regression_ResumeFromHangar");
        objects.Add(aircraftObject);
        aircraftObject.SetActive(false);
        Component aircraft = aircraftObject.AddComponent(TypeOf("ControleAviao"));
        Component control = aircraftObject.AddComponent(TypeOf("ControleUnidade"));
        aircraftObject.SetActive(true);
        yield return null;

        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ReservaHangar"));
        Set(aircraft, "retomarMissaoAposAbastecer", true);
        Set(control, "ordemControleAtual", Enum.Parse(TypeOf("OrdemControleUnidade"), "Patrulhando"));
        IList route = (IList)Read(aircraft, "rotaPatrulhaSalva");
        route.Add(new Vector3(120f, 181f, 120f));
        route.Add(new Vector3(240f, 181f, 120f));

        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ProntoNoPatio"));
        MethodInfo resume = aircraft.GetType().GetMethod(
            "TentarRetomarMissaoAposReabastecimento", Members);
        Assert.IsNotNull(resume);
        resume.Invoke(aircraft, null);

        yield return new WaitForSeconds(2.5f);

        Assert.AreEqual("EmMissao", Read(aircraft, "estadoAtual").ToString());
        Assert.IsTrue((bool)Read(aircraft, "estaEmModoVooFisico"));
        Assert.AreEqual(2, route.Count);
    }

    [UnityTest]
    public IEnumerator LandingAircraftEscapesTheTurningCircleAndReachesTouchdown()
    {
        Component airport = Create("GerenciadorAeroporto");
        Component aircraft = Create("ControleAviao");
        var marker = new GameObject("TouchdownRegression");
        objects.Add(marker);
        marker.transform.position = new Vector3(0f, 181f, 0f);
        var points = (IList)Read(airport, "waypointsDecida");
        points.Add(marker.transform);
        points.Add(marker.transform);
        aircraft.gameObject.SetActive(true);
        yield return null;
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "Pousando"));
        Set(aircraft, "alvoGPSVoo", marker.transform.position);
        Set(aircraft, "taxaDeGiroLeme", 60f);
        Set(aircraft, "velocidadeMaximaVoo", 180f);
        Set(aircraft, "velocidadeVooAtual", 126f);
        Set(aircraft, "estaEmModoVooFisico", true);
        aircraft.transform.position = new Vector3(120f, 181f, 0f);
        aircraft.transform.rotation = Quaternion.identity;
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Vector3.Distance(aircraft.transform.position, marker.transform.position) > 30f
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.LessOrEqual(Vector3.Distance(aircraft.transform.position, marker.transform.position), 30f,
            "The old approach circled outside the 100 m braking zone until fuel ran out.");
    }

    [UnityTest]
    public IEnumerator HiddenNaturalPropIsRestoredWhenItsBuildingIsDestroyed()
    {
        Vector3 center = new Vector3(1000000f, 0f, 1000000f);
        GameObject building = new GameObject("Regression_BuildingForDemolition");
        objects.Add(building);
        building.transform.position = center;
        building.AddComponent<BoxCollider>().size = new Vector3(12f, 6f, 12f);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(rock);
        rock.name = "Regression_RockForDemolition";
        rock.transform.position = center;

        Type cleanupType = TypeOf("LimpezaVegetacaoConstrucao");
        MethodInfo apply = cleanupType.GetMethod("Aplicar", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(apply);
        apply.Invoke(null, new object[] { building });
        Assert.IsFalse(rock.activeSelf, "A pedra dentro da obra deve desaparecer enquanto ela existe.");

        UnityEngine.Object.Destroy(building);
        objects.Remove(building);
        yield return null;

        Assert.IsTrue(rock.activeSelf, "A pedra deve voltar quando a obra for demolida em runtime.");
    }
}
