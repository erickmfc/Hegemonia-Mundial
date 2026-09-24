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
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
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
        foreach (var go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        objects.Clear();
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
}
