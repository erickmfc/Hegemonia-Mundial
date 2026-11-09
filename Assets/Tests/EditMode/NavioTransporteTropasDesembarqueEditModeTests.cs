using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class NavioTransporteTropasDesembarqueEditModeTests
{
    private const float WorldX = 400000f;
    private const float WorldZ = 400000f;
    private const float GroundSurfaceY = 20f;

    private GameObject ground;
    private GameObject water;
    private GameObject ship;
    private Type transportType;
    private Component transport;

    [TearDown]
    public void TearDown()
    {
        if (ship != null) UnityEngine.Object.DestroyImmediate(ship);
        if (water != null) UnityEngine.Object.DestroyImmediate(water);
        if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
        Physics.SyncTransforms();
    }

    [Test]
    public void SaidaSemNavMeshUsaAlturaDoTerrenoEmVezDaAlturaDoNavio()
    {
        CriarTerreno();
        CriarNavio();
        Physics.SyncTransforms();

        Assert.That(
            NavMesh.SamplePosition(ship.transform.position, out _, 18f, NavMesh.AllAreas),
            Is.False,
            "O caso deve validar o fallback de terreno sem NavMesh próximo.");

        bool pontoEmTerra = Invocar<bool>("PontoTemTerra", ship.transform.position);
        Vector3 destino = Invocar<Vector3>("AjustarParaNavMeshSePossivel", ship.transform.position);

        Assert.That(pontoEmTerra, Is.True,
            "A saída deve reconhecer o terreno físico acima da posição do navio.");
        Assert.That(destino.x, Is.EqualTo(WorldX).Within(0.05f));
        Assert.That(destino.z, Is.EqualTo(WorldZ).Within(0.05f));
        Assert.That(destino.y, Is.EqualTo(GroundSurfaceY).Within(0.1f),
            "A unidade deve sair sobre a superfície do terreno, não na altura do convés.");
    }

    [Test]
    public void AguaSobreTerrenoNaoTransformaFundoMarinhoEmPontoDeDesembarque()
    {
        CriarTerreno();
        water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "WaterOverlay_Test";
        water.layer = LayerMask.NameToLayer("Water");
        water.transform.position = new Vector3(WorldX, GroundSurfaceY + 0.6f, WorldZ);
        water.transform.localScale = new Vector3(40f, 0.2f, 40f);
        CriarNavio();
        Physics.SyncTransforms();

        Assert.That(water.layer, Is.EqualTo(LayerMask.NameToLayer("Water")));
        Assert.That(
            Invocar<bool>("PontoTemTerra", ship.transform.position),
            Is.False,
            "O collider de água sobreposto deve impedir que o terreno submerso seja tratado como costa segura.");
    }

    private void CriarTerreno()
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Desembarque_EditMode_Ground";
        ground.transform.position = new Vector3(WorldX, GroundSurfaceY - 0.5f, WorldZ);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
    }

    private void CriarNavio()
    {
        ship = new GameObject("Desembarque_EditMode_Ship");
        ship.transform.position = new Vector3(WorldX, 0f, WorldZ);
        ship.SetActive(false);
        transportType = Type.GetType("NavioTransporteTropas, Assembly-CSharp");
        Assert.That(transportType, Is.Not.Null,
            "O script de transporte deve estar disponível no Assembly-CSharp.");
        transport = ship.AddComponent(transportType);
    }

    private T Invocar<T>(string metodo, params object[] argumentos)
    {
        MethodInfo method = transportType.GetMethod(
            metodo,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "Método esperado ausente: " + metodo);
        return (T)method.Invoke(transport, argumentos);
    }
}
