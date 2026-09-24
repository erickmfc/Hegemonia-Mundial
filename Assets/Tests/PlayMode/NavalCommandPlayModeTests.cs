using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

public sealed class NavalCommandPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator SurfaceShipAcceptsWaterOrderMovesAndCanBeRetargeted()
    {
        GameObject agua = CriarAgua();
        GameObject navio = new GameObject("NavioComandoPlayMode");
        try
        {
            Component controle = navio.AddComponent(ResolveType("ControleNavioRealista"));
            Set(controle, "velocidadeMaxima", 45f);
            Set(controle, "tempoAceleracao", 0.2f);
            Set(controle, "curvaMaximaGraus", 90f);
            Set(controle, "velocidadeLeme", 90f);
            navio.transform.position = new Vector3(0f, 0f, -28f);
            navio.transform.rotation = Quaternion.identity;
            navio.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleNavioRealista").GetMethod("DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            Assert.IsTrue((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 0f, 8f) }));
            yield return new WaitForSeconds(0.4f);
            bool segundaOrdem = (bool)definir.Invoke(controle, new object[] { new Vector3(0f, 0f, 30f) });
            Assert.IsTrue(segundaOrdem);

            float limite = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < limite && navio.transform.position.z < 18f)
                yield return null;

            Assert.Greater(navio.transform.position.z, 18f, "O navio aceitou a ordem, mas não avançou pela água.");
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator SubmarineAcceptsWaterOrderAndFollowsIt()
    {
        GameObject agua = CriarAgua();
        GameObject submarino = new GameObject("SubmarinoComandoPlayMode");
        try
        {
            NavMeshAgent agente = submarino.AddComponent<NavMeshAgent>();
            agente.speed = 35f;
            agente.acceleration = 100f;
            Component controle = submarino.AddComponent(ResolveType("ControleSubmarino"));
            Set(controle, "usarNavMeshParaNavegacao", false);
            Set(controle, "velocidadeMovimento", 35f);
            Set(controle, "velocidadeGiroMax", 90f);
            Set(controle, "aceleracao", 20f);
            submarino.transform.position = new Vector3(0f, 0f, -28f);
            submarino.transform.rotation = Quaternion.identity;
            submarino.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleSubmarino").GetMethod("DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            Assert.IsTrue((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 0f, 28f) }));

            float limite = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < limite && submarino.transform.position.z < 15f)
                yield return null;

            Assert.Greater(submarino.transform.position.z, 15f, "O submarino recebeu a ordem, mas não a seguiu pela água.");
            Assert.IsTrue((bool)ReadProperty(controle, "TemDestinoAtivo"));
        }
        finally
        {
            UnityEngine.Object.Destroy(submarino);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator SurfaceShipRejectsAnOrderOutsideTheWater()
    {
        GameObject agua = CriarAgua();
        GameObject navio = new GameObject("NavioDestinoEmTerra");
        try
        {
            Component controle = navio.AddComponent(ResolveType("ControleNavioRealista"));
            navio.transform.position = new Vector3(0f, 0f, -20f);
            navio.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleNavioRealista").GetMethod("DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Destino recusado.*fora da água"));
            Assert.IsFalse((bool)definir.Invoke(controle, new object[] { new Vector3(450f, 0f, 0f) }),
                "Um navio não pode aceitar uma ordem cujo destino não está na água.");
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator WaterRouteDetoursAroundAnIslandInsteadOfCrossingLand()
    {
        GameObject water = CriarAgua();
        GameObject island = new GameObject("IlhaSolida");
        BoxCollider islandCollider = island.AddComponent<BoxCollider>();
        islandCollider.size = new Vector3(130f, 8f, 230f);
        try
        {
            yield return new WaitForFixedUpdate();

            Type resolver = ResolveType("NavalPlacementResolver");
            MethodInfo buildRoute = resolver.GetMethod(
                "TryBuildWaterRoute",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo waterSegment = resolver.GetMethod(
                "IsWaterSegment",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(buildRoute, Is.Not.Null);
            Assert.That(waterSegment, Is.Not.Null);

            Vector3 start = new Vector3(-180f, 0f, 0f);
            Vector3 destination = new Vector3(180f, 0f, 0f);
            object[] args = { start, destination, 18f, null };
            Assert.That((bool)buildRoute.Invoke(null, args), Is.True,
                "Deve existir uma rota de água em volta da ilha.");

            IList route = args[3] as IList;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Count, Is.GreaterThan(1),
                "A rota não pode usar o segmento reto que atravessa a ilha.");

            Vector3 previous = start;
            for (int index = 0; index < route.Count; index++)
            {
                Vector3 point = (Vector3)route[index];
                Assert.That((bool)waterSegment.Invoke(null, new object[] { previous, point, 18f }), Is.True,
                    "Trecho naval " + index + " atravessou terra.");
                previous = point;
            }
            // TryBuildWaterRoute snaps waypoint Y to the resolved sea level.
            // The click destination's X/Z must remain exact, while its input Y
            // is intentionally replaced by that sea-level value.
            Assert.That(Vector2.Distance(
                new Vector2(previous.x, previous.z),
                new Vector2(destination.x, destination.z)), Is.LessThan(0.1f));
        }
        finally
        {
            UnityEngine.Object.Destroy(island);
            UnityEngine.Object.Destroy(water);
        }
    }

    private static GameObject CriarAgua()
    {
        GameObject agua = new GameObject("Agua");
        BoxCollider colisor = agua.AddComponent<BoxCollider>();
        colisor.center = new Vector3(0f, -0.5f, 0f);
        colisor.size = new Vector3(600f, 1f, 600f);
        return agua;
    }

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .First(type => type != null);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Members).SetValue(target, value);
    }

    private static object Read(object target, string name)
    {
        return target.GetType().GetField(name, Members).GetValue(target);
    }

    private static object ReadProperty(object target, string name)
    {
        return target.GetType().GetProperty(name, Members).GetValue(target, null);
    }
}
