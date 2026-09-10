#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ReformaGameplayRegressionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    private Component Create(string name)
    {
        var go = new GameObject("Regression_" + name);
        go.SetActive(false);
        objects.Add(go);
        return go.AddComponent(TypeOf(name));
    }

    private Component CreateActive(string name, Type componentType)
    {
        var go = new GameObject("Regression_" + name);
        objects.Add(go);
        return go.AddComponent(componentType);
    }

    private static object Read(object target, string field) => target.GetType().GetField(field, Members).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Members).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Members).Invoke(target, args);

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

    [TearDown]
    public void Cleanup()
    {
        foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear();
    }

    [TestCase(90f, 0.016666667f)]
    [TestCase(180f, 0.05f)]
    [TestCase(270f, 0.1f)]
    public void TankerReachesBerthAndFinishesAligned(float heading, float dt)
    {
        Component ship = Create("NavioPetroleiro");
        var marker = new GameObject("Berth");
        objects.Add(marker);
        marker.transform.position = new Vector3(0f, 20f, 0f);
        marker.transform.rotation = Quaternion.Euler(0f, heading, 0f);
        ship.transform.position = new Vector3(0f, 2f, -14f);
        Type state = TypeOf("NavioPetroleiro+EstadoPetroleiro");
        Set(ship, "estadoAtual", Enum.Parse(state, "ACOPLANDO_PIER"));
        object unloading = Enum.Parse(state, "DESCARREGANDO");
        for (int i = 0; i < 1000 && !Read(ship, "estadoAtual").Equals(unloading); i++)
            Call(ship, "AvancarAcoplagem", marker.transform, unloading, dt);
        Assert.AreEqual(unloading, Read(ship, "estadoAtual"));
        Assert.Less(Vector3.Distance(ship.transform.position, new Vector3(0f, 2f, 0f)), 0.01f);
        Assert.Less(Quaternion.Angle(ship.transform.rotation, marker.transform.rotation), 1f);
    }

    [Test]
    public void TankerDoesNotUnloadBeforeTurningOrWithoutABerth()
    {
        Component ship = Create("NavioPetroleiro");
        var marker = new GameObject("Berth");
        objects.Add(marker);
        marker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Type state = TypeOf("NavioPetroleiro+EstadoPetroleiro");
        object docking = Enum.Parse(state, "ACOPLANDO_PIER");
        object unloading = Enum.Parse(state, "DESCARREGANDO");
        Set(ship, "estadoAtual", docking);
        Call(ship, "AvancarAcoplagem", marker.transform, unloading, 0.02f);
        Assert.AreEqual(docking, Read(ship, "estadoAtual"));
        Assert.Less(Quaternion.Angle(Quaternion.identity, ship.transform.rotation), 2f);
        Call(ship, "AvancarAcoplagem", null, unloading, 0.02f);
        Assert.AreEqual(Enum.Parse(state, "AGUARDANDO_INFRAESTRUTURA"), Read(ship, "estadoAtual"));
    }

    [Test]
    public void AircraftRequestsReturnAtReserveAndRefillsAtBase()
    {
        Component aircraft = Create("ControleAviao");
        Component airport = Create("GerenciadorAeroporto");
        ConfigureMinimalLandingRoute(airport);
        Component fuel = aircraft.gameObject.AddComponent(TypeOf("CombustivelUnidade"));
        Set(fuel, "capacidade", 100f);
        Set(fuel, "combustivelAtual", 29f);
        Component launcher = aircraft.gameObject.AddComponent(TypeOf("LancadorMisselCaca"));
        Set(launcher, "municaoMaxima", 4);
        Set(launcher, "municaoAtual", 0);
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Call(aircraft, "AvaliarRetornoSeguroAutomatico");
        Assert.IsTrue((bool)Read(aircraft, "ordemParaRetorno"));
        Assert.IsTrue((bool)Read(aircraft, "retomarMissaoAposAbastecer"));
        Call(aircraft, "ProcessarServicoDeBaseAposPouso");
        Assert.AreEqual(100f, Read(fuel, "combustivelAtual"));
        Assert.AreEqual(4, Read(launcher, "municaoAtual"));
    }

    [Test]
    public void AircraftDoesNotArmReturnWhenNoBaseHasAValidLandingRunway()
    {
        Component aircraft = Create("ControleAviao");
        Component airport = Create("GerenciadorAeroporto");
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));

        Call(aircraft, "ComandoRetornarBase");

        Assert.IsFalse((bool)Read(aircraft, "ordemParaRetorno"),
            "Uma aeronave não pode iniciar a aproximação sem pista de pouso válida.");
    }

    [Test]
    public void AircraftRejectsAFlightOrderWhenItsOriginBaseHasNoLandingRoute()
    {
        Component aircraft = Create("ControleAviao");
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ProntoNoPatio"));

        Assert.IsFalse((bool)Call(
            aircraft,
            "ReceberOrdemManual",
            new Vector3(900f, 180f, -450f)),
            "Uma aeronave sem base operacional não pode aceitar uma missão que terminará sem pouso.");
    }

    [Test]
    public void FuelConsumptionNeverDropsBelowZeroAndRefuelRestoresTheConfiguredTank()
    {
        Component fuel = Create("CombustivelUnidade");
        Set(fuel, "usaCombustivel", true);
        Set(fuel, "combustivelInfinito", false);
        Set(fuel, "capacidade", 120f);
        Set(fuel, "combustivelAtual", 15f);
        Set(fuel, "pararAoEsvaziar", false);

        Assert.IsTrue((bool)Call(fuel, "Consumir", 999f));
        Assert.AreEqual(0f, Read(fuel, "combustivelAtual"));
        Assert.IsFalse((bool)Call(fuel, "Consumir", 1f),
            "Uma unidade já sem combustível não pode consumir nem gerar valor negativo.");
        Assert.AreEqual(0f, Read(fuel, "combustivelAtual"));

        Call(fuel, "PreencherSemCusto");
        Assert.AreEqual(120f, Read(fuel, "combustivelAtual"));
        Assert.AreEqual(12f, Call(fuel, "EstimarConsumoParaDistancia", 600f, 50f));
    }

    [Test]
    public void EconomySeparatesTeamsAndStopsAnUnpoweredStructureOnTheNextCycle()
    {
        Type economyType = TypeOf("SistemaEconomiaImoveis");
        Type structureType = TypeOf("EstruturaEconomica");
        Type structureKind = TypeOf("TipoEstruturaEconomica");
        Type structureStatus = TypeOf("StatusEstruturaEconomica");
        FieldInfo registeredField = economyType.GetField(
            "EstruturasRegistradas",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(registeredField, Is.Not.Null);

        object registered = registeredField.GetValue(null);
        MethodInfo clear = registered.GetType().GetMethod("Clear", Members);
        MethodInfo add = registered.GetType().GetMethod("Add", Members);
        Assert.That(clear, Is.Not.Null);
        Assert.That(add, Is.Not.Null);

        var original = new List<object>();
        foreach (object item in (IEnumerable)registered)
        {
            original.Add(item);
        }

        clear.Invoke(registered, null);
        try
        {
            Component economy = Create("SistemaEconomiaImoveis");
            Set(economy, "usarCompatibilidadeImovel", false);
            Set(economy, "usarTagsEconomicas", false);

            Component generator = CreateActive("UsinaTime2", structureType);
            Set(generator, "teamId", 2);
            Set(generator, "tipo", Enum.Parse(structureKind, "UsinaSolar"));
            Set(generator, "energiaProduzida", 100f);

            Component poweredIndustry = CreateActive("IndustriaComEnergia", structureType);
            Set(poweredIndustry, "teamId", 2);
            Set(poweredIndustry, "tipo", Enum.Parse(structureKind, "Industria"));
            Set(poweredIndustry, "energiaConsumida", 50f);
            Set(poweredIndustry, "industriaProduzida", 40f);

            Component unpoweredIndustry = CreateActive("IndustriaSemEnergia", structureType);
            Set(unpoweredIndustry, "teamId", 3);
            Set(unpoweredIndustry, "tipo", Enum.Parse(structureKind, "Industria"));
            Set(unpoweredIndustry, "energiaConsumida", 20f);
            Set(unpoweredIndustry, "industriaProduzida", 40f);

            Set(economy, "ultimoRecalculo", -999f);
            Call(economy, "Recalcular");

            object team2 = Call(economy, "ObterEconomia", 2);
            object team3 = Call(economy, "ObterEconomia", 3);
            Assert.That((float)Read(team2, "deficitEnergia"), Is.EqualTo(0f).Within(0.01f));
            Assert.That((float)Read(team3, "deficitEnergia"), Is.EqualTo(20f).Within(0.01f));
            Assert.That(Read(unpoweredIndustry, "status"), Is.EqualTo(Enum.Parse(structureStatus, "SemEnergia")));

            Set(economy, "ultimoRecalculo", -999f);
            Call(economy, "Recalcular");
            team3 = Call(economy, "ObterEconomia", 3);
            Assert.That((float)Read(team3, "industriaProduzida"), Is.EqualTo(0f).Within(0.01f),
                "Uma estrutura sem energia não pode continuar contribuindo produção no ciclo seguinte.");
        }
        finally
        {
            clear.Invoke(registered, null);
            for (int i = 0; i < original.Count; i++)
            {
                add.Invoke(registered, new[] { original[i] });
            }
        }
    }

    [Test]
    public void BuildingClearsNaturalPropsInsideItsFootprintWithoutDeletingTerrain()
    {
        TerrainData originalTerrainData = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(100f, 20f, 100f)
        };
        GameObject terrainObject = Terrain.CreateTerrainGameObject(originalTerrainData);
        terrainObject.name = "Regression_Terrain";
        terrainObject.transform.position = new Vector3(1000000f, 0f, 1000000f);
        objects.Add(terrainObject);

        Vector3 testCenter = terrainObject.transform.position + new Vector3(50f, 0f, 50f);
        GameObject building = new GameObject("Regression_Building");
        building.transform.position = testCenter;
        building.AddComponent<BoxCollider>().size = new Vector3(12f, 6f, 12f);
        objects.Add(building);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.name = "Regression_Rock";
        rock.transform.position = testCenter;
        objects.Add(rock);

        MethodInfo apply = TypeOf("LimpezaVegetacaoConstrucao").GetMethod(
            "Aplicar", BindingFlags.Public | BindingFlags.Static);
        Assert.That(apply, Is.Not.Null);
        apply.Invoke(null, new object[] { building });

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        Assert.That(terrain, Is.Not.Null);
        Assert.That(terrain.terrainData, Is.Not.Null, "A construção não pode remover o Terrain.");
        Assert.That(terrain.terrainData, Is.Not.SameAs(originalTerrainData),
            "A limpeza deve operar em uma cópia de runtime, nunca no asset do Terrain.");
        Assert.That(rock.activeSelf, Is.False, "Pedra dentro do footprint deve ser removida visualmente.");

        UnityEngine.Object.DestroyImmediate(building);
        objects.Remove(building);
        Assert.That(rock.activeSelf, Is.True, "A pedra deve voltar quando a construção for demolida.");
    }

    [Test]
    public void SaveDataPreservesWarRelationsAndCountryStateAcrossSerialization()
    {
        object before = Activator.CreateInstance(TypeOf("DadosDoJogo"));
        object governo = Activator.CreateInstance(TypeOf("SaveGovernoMundialData"));
        Set(before, "governoMundial", governo);
        Set(governo, "teamJogador", 1);

        object atlas = Activator.CreateInstance(TypeOf("DadosPaisGoverno"));
        Set(atlas, "teamId", 1);
        Set(atlas, "nomePais", "Atlas");
        Set(atlas, "emGuerra", true);
        Set(atlas, "rivalTeamId", 3);
        Set(atlas, "saldo", 9876543210L);
        object carmesim = Activator.CreateInstance(TypeOf("DadosPaisGoverno"));
        Set(carmesim, "teamId", 3);
        Set(carmesim, "nomePais", "Carmesim");
        Set(carmesim, "emGuerra", true);
        Set(carmesim, "rivalTeamId", 1);
        ((IList)Read(governo, "paises")).Add(atlas);
        ((IList)Read(governo, "paises")).Add(carmesim);

        object relacao = Activator.CreateInstance(TypeOf("RelacaoPaisGoverno"));
        Set(relacao, "teamA", 1);
        Set(relacao, "teamB", 3);
        Set(relacao, "guerraDeclarada", true);
        Set(relacao, "sancaoAtiva", true);
        Set(relacao, "valor", -100);
        ((IList)Read(governo, "relacoes")).Add(relacao);

        object proposta = Activator.CreateInstance(TypeOf("PropostaInternacional"));
        Set(proposta, "id", "cessar-fogo-1");
        Set(proposta, "origemTeamId", 1);
        Set(proposta, "alvoTeamId", 3);
        ((IList)Read(governo, "propostas")).Add(proposta);

        object after = JsonUtility.FromJson(JsonUtility.ToJson(before), TypeOf("DadosDoJogo"));
        object governoRestaurado = Read(after, "governoMundial");
        Assert.That(governoRestaurado, Is.Not.Null);
        IList paises = (IList)Read(governoRestaurado, "paises");
        Assert.That(paises, Has.Count.EqualTo(2));
        Assert.That((bool)Read(paises[0], "emGuerra"), Is.True);
        Assert.That((int)Read(paises[0], "rivalTeamId"), Is.EqualTo(3));
        Assert.That((long)Read(paises[0], "saldo"), Is.EqualTo(9876543210L));
        IList relacoes = (IList)Read(governoRestaurado, "relacoes");
        Assert.That((bool)Read(relacoes[0], "guerraDeclarada"), Is.True);
        Assert.That((bool)Read(relacoes[0], "sancaoAtiva"), Is.True);
        IList propostas = (IList)Read(governoRestaurado, "propostas");
        Assert.That((string)Read(propostas[0], "id"), Is.EqualTo("cessar-fogo-1"));
    }

    [Test]
    public void CarrierServiceRefuelsAndReloadsAircraftBeforeOperationalResume()
    {
        Component aircraft = Create("ControleAviao");
        Component fuel = aircraft.gameObject.AddComponent(TypeOf("CombustivelUnidade"));
        Set(fuel, "capacidade", 100f);
        Set(fuel, "combustivelAtual", 30f);

        Component launcher = aircraft.gameObject.AddComponent(TypeOf("LancadorMisselCaca"));
        Set(launcher, "municaoMaxima", 6);
        Set(launcher, "municaoAtual", 1);

        Type carrierType = TypeOf("GerenciadorPortaAvioes");
        MethodInfo service = carrierType.GetMethod(
            "ReabastecerAeronaveCarrier",
            BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(service);
        service.Invoke(null, new object[] { aircraft, true });

        Assert.AreEqual(100f, Read(fuel, "combustivelAtual"));
        Assert.AreEqual(6, Read(launcher, "municaoAtual"));
    }

    [Test]
    public void FarmDoesNotCaptureClicksOrExposeAControlPanel()
    {
        Component farm = Create("Fazenda");
        Type farmType = farm.GetType();
        MethodInfo click = farmType.GetMethod("CliqueCapturadoPeloMenu", BindingFlags.Static | BindingFlags.Public);
        PropertyInfo menu = farmType.GetProperty("MenuAberto", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(click);
        Assert.IsNotNull(menu);
        Assert.IsFalse((bool)click.Invoke(null, null));
        Assert.IsFalse((bool)menu.GetValue(farm, null));

        Type menuType = TypeOf("FazendaMenuController");
        PropertyInfo aberto = menuType.GetProperty("EstaAberto", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(aberto);
        Assert.IsFalse((bool)aberto.GetValue(null, null));
    }

    [Test]
    public void NavalUnitAcceptsAValidWaterDestinationAndKeepsItActive()
    {
        GameObject water = new GameObject("Agua");
        objects.Add(water);
        water.transform.position = new Vector3(0f, -0.5f, 0f);
        BoxCollider surface = water.AddComponent<BoxCollider>();
        surface.size = new Vector3(4000f, 1f, 4000f);

        Component ship = Create("ControleNavioRealista");
        ship.transform.position = new Vector3(-500f, 0f, -500f);
        Vector3 destination = new Vector3(500f, 0f, 500f);

        MethodInfo definirDestino = ship.GetType().GetMethod(
            "DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
        Assert.IsNotNull(definirDestino);
        Assert.IsTrue((bool)definirDestino.Invoke(ship, new object[] { destination }));
        Assert.IsTrue((bool)Read(ship, "temDestino"));
        Vector3 applied = (Vector3)Read(ship, "destinoAtual");
        Assert.AreEqual(destination.x, applied.x, 0.01f);
        Assert.AreEqual(destination.z, applied.z, 0.01f);
        Assert.AreEqual(-0.5f, applied.y, 0.01f, "O destino naval é normalizado para o nível da água.");
    }

    [TestCase(60f, 120f)]
    [TestCase(15f, 80f)]
    public void LandingBrakesOutsideTheTouchdownZoneToAvoidAnUnreachableTurningCircle(float turnRate, float speed)
    {
        Component aircraft = Create("ControleAviao");
        Set(aircraft, "alvoGPSVoo", new Vector3(0f, 0f, 120f));
        Set(aircraft, "taxaDeGiroLeme", turnRate);
        float limited = (float)Call(aircraft, "LimitarVelocidadeAproximacao", speed);
        Assert.Greater(limited, 0f);
        Assert.Less(limited, speed);
        Assert.LessOrEqual(limited / (turnRate * Mathf.Deg2Rad), 60.01f);
        Set(aircraft, "alvoGPSVoo", new Vector3(0f, 0f, 5000f));
        Assert.AreEqual(speed, Call(aircraft, "LimitarVelocidadeAproximacao", speed));
    }

    [Test]
    public void CarrierMenuDoesNotTurnAnAirborneAircraftIntoAParkedOne()
    {
        Component manager = Create("GerenciadorOperacoesPortaAvioesV2");
        Component aircraft = Create("ControleAviao");
        Set(aircraft, "estaEmModoVooFisico", true);
        object registered = Call(manager, "PrepararAeronaveParaMenu", aircraft, false);
        object record = registered.GetType().GetProperty("Registro").GetValue(registered);
        Assert.AreEqual("EmVoo", Read(record, "estado").ToString());
        Assert.IsTrue((bool)Read(aircraft, "estaEmModoVooFisico"));
    }

    [Test]
    public void AircraftKeepsItsRouteWhenCarrierResubmitsTheSameList()
    {
        Component aircraft = Create("ControleAviao");
        var route = (IList)Read(aircraft, "rotaPatrulhaSalva");
        var points = new[] { new Vector3(100f, 181f, 100f), new Vector3(500f, 181f, 100f),
            new Vector3(500f, 181f, 500f), new Vector3(100f, 181f, 500f) };
        foreach (var point in points) route.Add(point);
        Call(aircraft, "RegistrarPatrulha", route);
        CollectionAssert.AreEqual(points, route);
    }

    [Test]
    public void CarrierLandingRetriesAfterWaitingAndReleasesReservationIfControlIsBusy()
    {
        Component manager = Create("GerenciadorOperacoesPortaAvioesV2");
        Component layout = Create("LayoutConvesPortaAvioesV2");
        Component aircraft = Create("ControleAviao");
        Set(manager, "layout", layout);
        Set(manager, "usarSistemaOperacoesV2", true);
        ((IList)Read(layout, "pontosPouso")).Add(layout.transform);
        Assert.IsFalse((bool)Call(manager, "TrySolicitarPouso", aircraft));
        Component embarked = aircraft.GetComponent(TypeOf("AeronaveEmbarcadaV2"));
        object record = embarked.GetType().GetProperty("Registro").GetValue(embarked);
        Assert.AreEqual("CircuitoDeEspera", Read(record, "estado").ToString());
        Component berth = Create("VagaPortaAvioesV2");
        Set(berth, "tamanhoMaximo", 1000f);
        ((IList)Read(layout, "vagasConves")).Add(berth);
        Set(embarked, "donoMovimento", "another-operation");
        Assert.IsFalse((bool)Call(manager, "TrySolicitarPouso", aircraft));
        Assert.AreEqual("EmVoo", Read(record, "estado").ToString(), "The waiting request must be evaluated again.");
        Assert.AreEqual("Livre", Read(berth, "estado").ToString(), "A rejected operation must not leak its berth reservation.");
    }

    [Test]
    public void AircraftManualOrderReplacesAnActivePatrolWithoutLeavingOldWaypoints()
    {
        Component aircraft = Create("ControleAviao");
        Component stateComponent = aircraft;
        Set(stateComponent, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Set(stateComponent, "estaEmModoVooFisico", true);
        Set(stateComponent, "alvoPrioritarioIA", true);

        var route = (IList)Read(stateComponent, "rotaPatrulhaSalva");
        route.Add(new Vector3(100f, 181f, 100f));
        route.Add(new Vector3(300f, 181f, 100f));

        Vector3 destination = new Vector3(900f, 20f, -450f);
        Assert.IsTrue((bool)Call(stateComponent, "ReceberOrdemManual", destination));
        Assert.AreEqual(0, route.Count);
        Assert.AreEqual(new Vector3(900f, 181f, -450f), Read(stateComponent, "centroDaPatrulha"));
        Assert.AreEqual(new Vector3(900f, 181f, -450f), Read(stateComponent, "alvoGPSVoo"));
        Assert.IsFalse((bool)Read(stateComponent, "ordemParaRetorno"));
        Assert.IsFalse((bool)Read(stateComponent, "alvoPrioritarioIA"));
    }

    [Test]
    public void AircraftPatrolOrderUpdatesTheExistingFlightInsteadOfStartingASecondOne()
    {
        Component aircraft = Create("ControleAviao");
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "EmMissao"));
        Set(aircraft, "estaEmModoVooFisico", true);

        var points = new List<Vector3>
        {
            new Vector3(200f, 181f, 200f),
            new Vector3(500f, 181f, 200f),
            new Vector3(500f, 181f, 500f),
            new Vector3(200f, 181f, 500f)
        };

        Assert.IsTrue((bool)Call(aircraft, "ReceberOrdemPatrulha", points));
        var saved = (IList)Read(aircraft, "rotaPatrulhaSalva");
        Assert.AreEqual(points.Count, saved.Count);
        Assert.AreEqual(points[0], Read(aircraft, "alvoGPSVoo"));
        Assert.IsFalse((bool)Read(aircraft, "ordemParaRetorno"));
    }

    [Test]
    public void RejectedManualOrderDuringLandingPreservesTheCurrentMissionState()
    {
        Component aircraft = Create("ControleAviao");
        Type stateType = TypeOf("ControleAviao+EstadoAviao");
        Set(aircraft, "estadoAtual", Enum.Parse(stateType, "Pousando"));

        Vector3 oldTarget = new Vector3(120f, 181f, 240f);
        Set(aircraft, "alvoGPSVoo", oldTarget);
        Set(aircraft, "centroDaPatrulha", oldTarget);
        var route = (IList)Read(aircraft, "rotaPatrulhaSalva");
        route.Add(oldTarget);

        Assert.IsFalse((bool)Call(aircraft, "ReceberOrdemManual", new Vector3(900f, 20f, -450f)));
        Assert.AreEqual(1, route.Count);
        Assert.AreEqual(oldTarget, Read(aircraft, "alvoGPSVoo"));
        Assert.AreEqual(oldTarget, Read(aircraft, "centroDaPatrulha"));
    }

    [Test]
    public void HangarLaunchReservesARealPatioSlotForTheReturnFlight()
    {
        Component airport = Create("GerenciadorAeroporto");
        Component aircraft = Create("ControleAviao");
        var slot = new GameObject("Vaga_Aerea_01");
        objects.Add(slot);
        slot.transform.SetParent(airport.transform, true);
        ((IList)Read(airport, "waypointsPatio")).Add(slot.transform);
        ((IList)Read(airport, "avioesNoHangar")).Add(aircraft);
        Set(aircraft, "aeroportoOrigem", airport);
        Set(aircraft, "estadoAtual", Enum.Parse(TypeOf("ControleAviao+EstadoAviao"), "ReservaHangar"));

        Call(airport, "PrepararAviaoReservaParaLancamento", aircraft);

        Assert.AreSame(slot.transform, Read(aircraft, "vagaRetorno"));
        Assert.AreSame(airport.transform, aircraft.transform.parent);
        Assert.AreEqual("ProntoNoPatio", Read(aircraft, "estadoAtual").ToString());
    }

    [Test]
    public void HelicopterPreservesAOnePointPatrolForRefuelResume()
    {
        Component helicopter = Create("Helicoptero");
        Vector3 point = new Vector3(250f, 20f, 400f);
        Call(helicopter, "IniciarPatrulhaAeroporto", new List<Vector3> { point });
        var route = (IList)Read(helicopter, "rotaPatrulhaAeroporto");
        Assert.AreEqual(1, route.Count);
        Assert.AreEqual(3, Read(helicopter, "missaoAtualAeroporto"));

        Call(helicopter, "SalvarMissaoAntesDoReabastecimento");

        Assert.IsTrue((bool)Read(helicopter, "retomarPatrulhaDepoisDeAbastecer"));
        var saved = (IList)Read(helicopter, "rotaPatrulhaSalva");
        Assert.AreEqual(1, saved.Count);
        Assert.AreEqual(route[0], saved[0]);
    }

    [Test]
    public void HelicopterPatrolGatewayRejectsAnEmptyRoute()
    {
        Component helicopter = Create("Helicoptero");

        Assert.IsFalse((bool)Call(
            helicopter,
            "ReceberOrdemPatrulhaAeroporto",
            new List<Vector3>()));
        Assert.AreEqual(0, Read(helicopter, "missaoAtualAeroporto"));
    }
}
#endif
