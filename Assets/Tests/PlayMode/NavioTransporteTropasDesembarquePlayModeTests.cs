using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

public sealed class NavioTransporteTropasDesembarquePlayModeTests
{
    [UnityTest]
    [Timeout(30000)]
    public IEnumerator PontoDeSaidaAbaixoDoTerrenoDesembarcaNaAlturaDoSolo()
    {
        const float worldX = 400000f;
        const float worldZ = 400000f;
        const float surfaceY = 20f;
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "NavioDesembarque_PlayMode_Ground";
        ground.transform.position = new Vector3(worldX, surfaceY - 0.5f, worldZ);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);

        GameObject ship = new GameObject("NavioDesembarque_PlayMode_Ship");
        ship.transform.position = new Vector3(worldX, 0f, worldZ);
        ship.SetActive(false);
        Transform container = new GameObject("_CargaInterna").transform;
        container.SetParent(ship.transform, false);
        Transform chegada = new GameObject("chegada").transform;
        chegada.SetParent(ship.transform, false);
        Transform corredor = new GameObject("Corredor1").transform;
        corredor.SetParent(ship.transform, false);
        Transform saida = new GameObject("saida/entrada (0)").transform;
        saida.SetParent(ship.transform, false);
        saida.localPosition = new Vector3(5f, 0f, 0f);

        System.Type transportType = System.Type.GetType("NavioTransporteTropas, Assembly-CSharp");
        Assert.That(transportType, Is.Not.Null, "O script de transporte deve estar disponível no Assembly-CSharp.");
        Component transport = ship.AddComponent(transportType);
        transportType.GetField("chegada").SetValue(transport, chegada);
        transportType.GetField("corredor1").SetValue(transport, corredor);
        transportType.GetField("pontosSaidaEntrada").SetValue(transport, new[] { saida });
        transportType.GetField("velocidadeMovimentoInterno").SetValue(transport, 100f);
        transportType.GetField("timeoutMoverInterno").SetValue(transport, 1f);

        GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vehicle.name = "NavioDesembarque_PlayMode_Veiculo";
        vehicle.transform.position = ship.transform.position;
        vehicle.SetActive(false);

        try
        {
            ship.SetActive(true);

            Type cargaType = transportType.GetNestedType("CargaTerrestre", BindingFlags.NonPublic);
            Assert.That(cargaType, Is.Not.Null);
            object carga = Activator.CreateInstance(cargaType, true);
            cargaType.GetField("unidade").SetValue(carga, vehicle);
            var cargas = (IList)transportType.GetField("_veiculosCarregados", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(transport);
            cargas.Add(carga);

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(
                NavMesh.SamplePosition(ship.transform.position, out _, 18f, NavMesh.AllAreas),
                Is.False,
                "Este caso precisa cobrir a saída sem NavMesh próximo, que antes deixava a unidade na altura do navio.");

            MethodInfo pontoTemTerra = transportType.GetMethod(
                "PontoTemTerra", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo ajustarDestino = transportType.GetMethod(
                "AjustarParaNavMeshSePossivel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pontoTemTerra, Is.Not.Null);
            Assert.That(ajustarDestino, Is.Not.Null);

            Vector3 pontoSaida = ship.transform.position;
            Assert.That((bool)pontoTemTerra.Invoke(transport, new object[] { pontoSaida }), Is.True,
                "A saída deve reconhecer como terra o terreno elevado abaixo do ponto do navio.");

            Vector3 destino = (Vector3)ajustarDestino.Invoke(transport, new object[] { pontoSaida });
            Assert.That(destino.x, Is.EqualTo(worldX).Within(0.01f));
            Assert.That(destino.z, Is.EqualTo(worldZ).Within(0.01f));
            Assert.That(destino.y, Is.EqualTo(surfaceY).Within(0.1f),
                "A unidade não pode usar a altura do navio quando não existe NavMesh perto do ponto de saída.");

            Type modoOperacaoType = transportType.GetNestedType("ModoOperacaoTerrestre", BindingFlags.NonPublic);
            MethodInfo rotinaDesembarque = transportType.GetMethod(
                "RotinaDesembarqueTerrestre", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modoOperacaoType, Is.Not.Null);
            Assert.That(rotinaDesembarque, Is.Not.Null);

            IEnumerator rotina = (IEnumerator)rotinaDesembarque.Invoke(transport, new[]
            {
                Enum.Parse(modoOperacaoType, "Veiculos"),
                1,
                0
            });
            MonoBehaviour transportBehaviour = (MonoBehaviour)transport;
            Coroutine coroutine = transportBehaviour.StartCoroutine(rotina);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                && (vehicle.transform.parent != null || Mathf.Abs(vehicle.transform.position.y - surfaceY) > 0.1f))
            {
                yield return null;
            }

            Assert.That(vehicle.transform.parent, Is.Null,
                "O veículo deve ter sido desanexado do navio durante o desembarque.");
            Assert.That(vehicle.transform.position.x, Is.EqualTo(worldX + 5f).Within(0.1f));
            Assert.That(vehicle.transform.position.y, Is.EqualTo(surfaceY).Within(0.1f),
                "A rotina do navio precisa concluir o desembarque na superfície, não na altura do convés.");
            transportBehaviour.StopCoroutine(coroutine);
        }
        finally
        {
            if (transport != null && vehicle != null)
            {
                var transportBehaviour = transport as MonoBehaviour;
                if (transportBehaviour != null)
                {
                    transportBehaviour.StopAllCoroutines();
                }
            }
            UnityEngine.Object.Destroy(ship);
            UnityEngine.Object.Destroy(vehicle);
            UnityEngine.Object.Destroy(ground);
        }
    }
}
