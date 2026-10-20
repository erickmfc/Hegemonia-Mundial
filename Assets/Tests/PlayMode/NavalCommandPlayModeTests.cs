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
    public IEnumerator IA03ExplicitAttackPropagatesPriorityToSurfaceLauncher()
    {
        GameObject navio = new GameObject("NavioAlvoPrioritarioIA03PlayMode");
        GameObject alvo = new GameObject("AlvoNavalPrioritarioIA03PlayMode");
        navio.SetActive(false);
        try
        {
            navio.AddComponent(ResolveType("LancadorNaval"));
            Component controle = navio.AddComponent(ResolveType("ControleUnidade"));
            navio.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleUnidade").GetMethod(
                "DefinirAlvoPrioritario",
                Members,
                null,
                new[] { typeof(Transform) },
                null);
            MethodInfo obter = ResolveType("LancadorNaval").GetMethod(
                "ObterAlvoPrioritario",
                Members,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(definir, Is.Not.Null);
            Assert.That(obter, Is.Not.Null);

            definir.Invoke(controle, new object[] { alvo.transform });
            Component lancador = navio.GetComponent(ResolveType("LancadorNaval"));
            Assert.That(obter.Invoke(lancador, null), Is.SameAs(alvo.transform),
                "O alvo explícito da IA03 precisa chegar ao lançador naval de superfície.");

            definir.Invoke(controle, new object[] { null });
            Assert.That(obter.Invoke(lancador, null), Is.Null,
                "Limpar a prioridade da unidade precisa limpar o lançador naval.");
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(alvo);
        }
    }

    [UnityTest]
    public IEnumerator IA03NavalOrderPreservesGlobalOwnerAndIdentity()
    {
        GameObject agua = CriarAgua();
        GameObject navio = new GameObject("NavioOrdemIA03PlayMode");
        const string orderId = "ia03-naval-identity-regression-01";
        try
        {
            navio.SetActive(false);
            // O executor naval precisa existir antes da fachada para ser
            // descoberto no Awake do ControleUnidade.
            Component naval = navio.AddComponent(ResolveType("ControleNavioRealista"));
            Component controle = navio.AddComponent(ResolveType("ControleUnidade"));
            navio.transform.position = new Vector3(0f, 500f, -28f);
            navio.SetActive(true);
            yield return null;

            MethodInfo emitir = ResolveType("ControleUnidade").GetMethod(
                "EmitirOrdemMovimento",
                Members,
                null,
                new[] { typeof(Vector3), typeof(string), typeof(bool), typeof(string) },
                null);
            Assert.That(emitir, Is.Not.Null);
            Assert.That(
                (bool)emitir.Invoke(controle, new object[] {
                    new Vector3(0f, 500f, 8f),
                    "IA03EstrategaNacional",
                    true,
                    orderId
                }),
                Is.True,
                "A ordem naval IA03 precisa ser aceita pelo executor físico.");

            Type ordensType = ResolveType("OrquestradorGlobalOrdens");
            MethodInfo tentarObter = ordensType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "TentarObter");
            object[] registroArguments = { orderId, null };
            Assert.That(
                (bool)tentarObter.Invoke(null, registroArguments),
                Is.True,
                "A ordem IA03 precisa continuar registrada no orquestrador global.");
            object registro = registroArguments[1];
            Assert.That(ReadProperty(registro, "Dono"), Is.EqualTo("IA03EstrategaNacional"));
            Assert.That(ReadProperty(registro, "Unidade"), Is.SameAs(navio));
            Assert.That(ReadProperty(registro, "Id"), Is.EqualTo(orderId));
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(agua);
        }
    }

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
        submarino.SetActive(false);
        try
        {
            NavMeshAgent agente = submarino.AddComponent<NavMeshAgent>();
            agente.enabled = false;
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
            Vector3 destino = new Vector3(0f, 0f, 28f);
            Assert.That(
                (bool)ReadProperty(controle, "TemDestinoAtivo")
                || Vector3.Distance(submarino.transform.position, destino) <= 4.5f,
                Is.True,
                "O submarino deve continuar seguindo a rota ou já ter chegado ao destino.");
        }
        finally
        {
            UnityEngine.Object.Destroy(submarino);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator SurfaceShipRejectsRouteBeyondOnboardFuelAndAcceptsItAfterRefueling()
    {
        GameObject agua = CriarAgua();
        GameObject navio = new GameObject("NavioAutonomiaCombustivel");
        navio.SetActive(false);
        try
        {
            Component controle = navio.AddComponent(ResolveType("ControleNavioRealista"));
            Set(controle, "velocidadeMaxima", 10f);
            navio.transform.position = new Vector3(0f, 500f, -100f);
            Component combustivel = AdicionarCombustivelNaval(navio);
            navio.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleNavioRealista").GetMethod(
                "DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            Assert.That((bool)ResolveType("CombustivelUnidade").GetMethod("PodeOperarObjeto", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { navio }), Is.True,
                "O teste precisa provar falta de autonomia com combustível restante, não tanque vazio.");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("autonomia de combustível insuficiente"));
            Assert.That((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 500f, 100f) }), Is.False,
                "A rota exige mais combustível do que o navio leva a bordo.");

            Set(combustivel, "combustivelAtual", 1000f);
            Assert.That((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 500f, 100f) }), Is.True,
                "Após reabastecer, o mesmo navio pode aceitar a rota aquática.");
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator SubmarineRejectsRouteBeyondOnboardFuelAndAcceptsItAfterRefueling()
    {
        GameObject agua = CriarAgua();
        GameObject submarino = new GameObject("SubmarinoAutonomiaCombustivel");
        submarino.SetActive(false);
        try
        {
            NavMeshAgent agente = submarino.AddComponent<NavMeshAgent>();
            agente.enabled = false;
            agente.speed = 10f;
            Component controle = submarino.AddComponent(ResolveType("ControleSubmarino"));
            Set(controle, "usarNavMeshParaNavegacao", false);
            Set(controle, "velocidadeMovimento", 10f);
            submarino.transform.position = new Vector3(0f, 500f, -100f);
            Component combustivel = AdicionarCombustivelNaval(submarino);
            submarino.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleSubmarino").GetMethod(
                "DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            Assert.That((bool)ResolveType("CombustivelUnidade").GetMethod("PodeOperarObjeto", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { submarino }), Is.True,
                "O teste precisa provar falta de autonomia com combustível restante, não tanque vazio.");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("autonomia de combustível insuficiente"));
            Assert.That((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 500f, 100f) }), Is.False,
                "A rota exige mais combustível do que o submarino leva a bordo.");

            Set(combustivel, "combustivelAtual", 1000f);
            Assert.That((bool)definir.Invoke(controle, new object[] { new Vector3(0f, 500f, 100f) }), Is.True,
                "Após reabastecer, o mesmo submarino pode aceitar a rota aquática.");
        }
        finally
        {
            UnityEngine.Object.Destroy(submarino);
            UnityEngine.Object.Destroy(agua);
        }
    }

    [UnityTest]
    public IEnumerator GroundUnitRejectsRouteBeyondOnboardFuelAndAcceptsItAfterRefueling()
    {
        Vector3 center = new Vector3(10000f, 0f, 10000f);
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TerrenoNavMeshAutonomiaTerrestre";
        ground.transform.position = center;
        ground.transform.localScale = Vector3.one * 100f;

        NavMeshData navMeshData = null;
        NavMeshDataInstance navMeshInstance = default;
        GameObject unit = new GameObject("TanqueAutonomiaTerrestre");
        unit.SetActive(false);
        try
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = ground.GetComponent<MeshFilter>().sharedMesh,
                    transform = ground.transform.localToWorldMatrix,
                    area = 0
                }
            };
            navMeshData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(center, new Vector3(1200f, 100f, 1200f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.That(navMeshData, Is.Not.Null, "Não foi possível construir o NavMesh isolado do teste.");
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);

            unit.transform.position = center + new Vector3(-200f, 0f, 0f);
            NavMeshAgent agent = unit.AddComponent<NavMeshAgent>();
            agent.speed = 10f;
            agent.acceleration = 100f;
            agent.angularSpeed = 360f;
            agent.radius = 0.3f;

            Component fuel = unit.AddComponent(ResolveType("CombustivelUnidade"));
            Set(fuel, "classe", Enum.Parse(ResolveType("ClasseCombustivelUnidade"), "Terrestre"));
            Set(fuel, "capacidade", 100f);
            Set(fuel, "combustivelAtual", 1f);
            Set(fuel, "consumoPorSegundoMovendo", 0.3f);
            Set(fuel, "preservarCapacidadeConfigurada", true);
            Set(fuel, "preencherAoIniciar", false);
            Component controle = unit.AddComponent(ResolveType("ControleUnidade"));
            unit.SetActive(true);
            yield return null;

            Assert.That(agent.isOnNavMesh, Is.True,
                "O tanque sintético precisa estar numa rota NavMesh válida antes de medir autonomia.");
            Assert.That((bool)ResolveType("CombustivelUnidade").GetMethod(
                "PodeOperarObjeto", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { unit }), Is.True,
                "O teste comprova autonomia insuficiente com combustível restante, não tanque vazio.");

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("combustível insuficiente para concluir a rota NavMesh"));
            MethodInfo emitirOrdem = ResolveType("ControleUnidade").GetMethod(
                "EmitirOrdemMover", Members, null, new[] { typeof(Vector3), typeof(bool) }, null);
            Assert.That(emitirOrdem, Is.Not.Null);
            Vector3 destination = center + new Vector3(200f, 0f, 0f);
            Assert.That((bool)emitirOrdem.Invoke(controle, new object[] { destination, true }), Is.False,
                "O tanque não deve aceitar uma rota que consome mais combustível do que leva a bordo.");

            Set(fuel, "combustivelAtual", 100f);
            Assert.That((bool)emitirOrdem.Invoke(controle, new object[] { destination, true }), Is.True,
                "Após reabastecer, o mesmo tanque deve aceitar a rota NavMesh.");
        }
        finally
        {
            if (navMeshInstance.valid)
            {
                navMeshInstance.Remove();
            }
            UnityEngine.Object.Destroy(unit);
            UnityEngine.Object.Destroy(ground);
            if (navMeshData != null)
            {
                UnityEngine.Object.Destroy(navMeshData);
            }
        }
    }

    [UnityTest]
    public IEnumerator SurfaceShipRejectsAnOrderOutsideTheWater()
    {
        GameObject agua = CriarAgua();
        GameObject terra = new GameObject("TerraDestinoNaval");
        terra.transform.position = new Vector3(450f, 505f, 0f);
        terra.AddComponent<BoxCollider>().size = new Vector3(80f, 12f, 80f);
        Type tipoMarcador = ResolveType("MarcadorSuperficieMapa");
        Component marcador = terra.AddComponent(tipoMarcador);
        tipoMarcador.GetMethod("DefinirTipo", Members)
            .Invoke(marcador, new object[] { Enum.Parse(ResolveType("TipoSuperficieMapa"), "Chao") });
        GameObject navio = new GameObject("NavioDestinoEmTerra");
        try
        {
            Component controle = navio.AddComponent(ResolveType("ControleNavioRealista"));
            navio.transform.position = new Vector3(0f, 0f, -20f);
            navio.SetActive(true);
            yield return null;

            MethodInfo definir = ResolveType("ControleNavioRealista").GetMethod("DefinirDestino", Members, null, new[] { typeof(Vector3) }, null);
            Assert.That(definir, Is.Not.Null);
            Assert.That((bool)Read(controle, "ehCenaDeMenuCache"), Is.False,
                "O contrato de recusa de destino em terra precisa exercitar uma unidade em contexto de campanha, não o bloqueio do diorama de menu.");
            Type resolver = ResolveType("NavalPlacementResolver");
            MethodInfo isWater = resolver.GetMethod("IsWaterAtPosition", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3) }, null);
            Assert.That(isWater, Is.Not.Null);
            Assert.That((bool)isWater.Invoke(null, new object[] { new Vector3(450f, 0f, 0f) }), Is.False,
                "O destino sintético precisa ser classificado como terra antes da ordem.");
            Assert.That((bool)ResolveType("CombustivelUnidade").GetMethod("PodeOperarObjeto", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { navio }), Is.True,
                "O teste de água não deve ser encerrado pelo bloqueio de combustível.");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Destino recusado.*fora da água"));
            Assert.IsFalse((bool)definir.Invoke(controle, new object[] { new Vector3(450f, 0f, 0f) }),
                "Um navio não pode aceitar uma ordem cujo destino não está na água.");
        }
        finally
        {
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(terra);
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
        island.transform.position = new Vector3(0f, water.transform.position.y + 5f, 0f);
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

    [UnityTest]
    public IEnumerator DistantWaterRouteDetoursBeyondTheFormerLocalSearchMargin()
    {
        GameObject water = CriarAgua();
        water.GetComponent<BoxCollider>().size = new Vector3(40000f, 1f, 40000f);
        GameObject peninsula = new GameObject("PeninsulaRotaNavalDistante");
        BoxCollider land = peninsula.AddComponent<BoxCollider>();
        land.size = new Vector3(600f, 12f, 13000f);
        peninsula.transform.position = new Vector3(0f, water.transform.position.y + 5f, -2500f);
        try
        {
            yield return new WaitForFixedUpdate();

            Type resolver = ResolveType("NavalPlacementResolver");
            MethodInfo buildRoute = resolver.GetMethod("TryBuildWaterRoute", BindingFlags.Public | BindingFlags.Static);
            MethodInfo waterSegment = resolver.GetMethod("IsWaterSegment", BindingFlags.Public | BindingFlags.Static);
            Assert.That(buildRoute, Is.Not.Null);
            Assert.That(waterSegment, Is.Not.Null);

            Vector3 start = new Vector3(-6000f, 0f, 0f);
            Vector3 destination = new Vector3(6000f, 0f, 0f);
            object[] args = { start, destination, 18f, null };
            Assert.That((bool)buildRoute.Invoke(null, args), Is.True,
                "O navio deve contornar uma costa que ultrapassa a antiga margem de 1800 m.");

            IList route = args[3] as IList;
            Assert.That(route, Is.Not.Null.And.Count.GreaterThan(1));
            Vector3 previous = start;
            bool clearedPeninsula = false;
            foreach (object waypoint in route)
            {
                Vector3 point = (Vector3)waypoint;
                clearedPeninsula |= point.z > 4000f;
                Assert.That((bool)waterSegment.Invoke(null, new object[] { previous, point, 18f }), Is.True,
                    "Nenhum trecho da rota distante pode cruzar a península.");
                previous = point;
            }

            Assert.That(clearedPeninsula, Is.True,
                "A rota precisa sair do antigo corredor para passar pela ponta da península.");
            Assert.That(Vector2.Distance(new Vector2(previous.x, previous.z),
                new Vector2(destination.x, destination.z)), Is.LessThan(0.1f));
        }
        finally
        {
            UnityEngine.Object.Destroy(peninsula);
            UnityEngine.Object.Destroy(water);
        }
    }

    private static GameObject CriarAgua()
    {
        GameObject agua = new GameObject("Agua");
        // As PlayMode suites carregam cena19 antes destes testes. Eleva o mar
        // sintético acima do terreno dessa cena para que as coordenadas do
        // teste representem água, mantendo a ilha acima da superfície.
        agua.transform.position = new Vector3(0f, 500f, 0f);
        BoxCollider colisor = agua.AddComponent<BoxCollider>();
        colisor.center = new Vector3(0f, -0.5f, 0f);
        colisor.size = new Vector3(600f, 1f, 600f);
        return agua;
    }

    private static Component AdicionarCombustivelNaval(GameObject unidade)
    {
        Component combustivel = unidade.AddComponent(ResolveType("CombustivelUnidade"));
        Set(combustivel, "classe", Enum.Parse(ResolveType("ClasseCombustivelUnidade"), "Naval"));
        Set(combustivel, "capacidade", 1000f);
        Set(combustivel, "combustivelAtual", 1f);
        Set(combustivel, "consumoPorSegundoMovendo", 2f);
        Set(combustivel, "preservarCapacidadeConfigurada", true);
        Set(combustivel, "preencherAoIniciar", false);
        return combustivel;
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
