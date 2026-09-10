using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class C700TransporteAereoPlayModeTests
{
    [UnityTest]
    public IEnumerator C700CompletaPousoCargaDescargaEDecolagemNovamente()
    {
        GameObject piso = CriarPiso();
        GameObject transporteObjeto = null;
        GameObject pistaAObjeto = null;
        GameObject pistaBObjeto = null;

        try
        {
            ScriptableObject ficha = Resources.Load<ScriptableObject>("Construcoes/C700");
            Assert.That(ficha, Is.Not.Null, "A ficha C700 precisa estar disponível no catálogo de Resources.");
            PropertyInfo prefabPropriedade = ficha.GetType().GetProperty("PrefabDaUnidade", BindingFlags.Instance | BindingFlags.Public);
            GameObject prefab = prefabPropriedade == null ? null : prefabPropriedade.GetValue(ficha) as GameObject;
            Assert.That(prefab, Is.Not.Null, "A ficha C700 precisa apontar para o prefab do transporte.");

            // Use o prefab real: o problema original acontecia na instância
            // completa, onde collider, slots e componentes serializados
            // participam da descoberta e do embarque.
            transporteObjeto = UnityEngine.Object.Instantiate(
                prefab,
                new Vector3(0f, 0.2f, 0f),
                Quaternion.identity);
            transporteObjeto.name = "C700_PlayMode_Test";
            Type tipoTransporte = prefab.GetComponent("C700TransporteAereo").GetType();
            Component transporte = transporteObjeto.GetComponentInChildren(tipoTransporte, true);
            Assert.That(transporte, Is.Not.Null, "O prefab do C700 precisa ter C700TransporteAereo.");

            pistaAObjeto = CriarPista("MiniPista_C700_A", new Vector3(160f, 0f, 0f));
            pistaBObjeto = CriarPista("MiniPista_C700_B", new Vector3(-500f, 0f, 0f));
            Component pistaA = pistaAObjeto.GetComponent("MiniPistaLogistica");
            Component pistaB = pistaBObjeto.GetComponent("MiniPistaLogistica");

            yield return null;
            Campo(transporte, "velocidadeCruzeiro", 100f);
            Campo(transporte, "velocidadeDecolagem", 35f);
            Campo(transporte, "altitudeCruzeiro", 35f);
            Campo(transporte, "distanciaAproximacao", 65f);
            Campo(transporte, "distanciaDescida", 30f);
            Campo(transporte, "distanciaRolagem", 20f);
            Campo(transporte, "raioBuscaCarga", 30f);
            Campo(transporte, "debugLogs", true);
            Assert.That(Estado(transporte), Is.EqualTo("Solo"));
            Assert.That(Propriedade<int>(transporte, "CapacidadeCargaAtual"), Is.GreaterThan(0));
            Assert.That(pistaA, Is.Not.Null);
            Assert.That(pistaB, Is.Not.Null);
            Assert.That(Propriedade<bool>(pistaA, "PistaValida"), Is.True, "A mini pista A precisa possuir todos os pontos fixos.");
            Assert.That(Propriedade<bool>(pistaB, "PistaValida"), Is.True, "A mini pista B precisa possuir todos os pontos fixos.");
            Chamar(transporte, "ReceberOrdemMover", pistaA.transform.position);
            yield return EsperarEstado(transporte, "Estacionado", 90f);

            Transform estacionamentoA = Campo<Transform>(pistaA, "parkingPoint");
            Assert.That(estacionamentoA, Is.Not.Null);
            Assert.That(Vector3.Distance(transporte.transform.position, estacionamentoA.position), Is.LessThan(6f));
            Assert.That(Propriedade<Component>(pistaA, "ParkingReservedBy"), Is.SameAs(transporte));

            Assert.That(ChamarComResultado<bool>(pistaA, "AdicionarTropas", 8), Is.True);
            Assert.That(Campo<int>(pistaA, "tropasEsperando"), Is.EqualTo(8));
            Assert.That(ChamarComResultado<bool>(transporte, "IniciarTransportMission", pistaA, pistaB), Is.True);
            yield return EsperarEstadoSolo(transporte, 120f);

            Assert.That(Estado(transporte), Is.EqualTo("Solo"));
            Assert.That(Campo<int>(pistaA, "tropasEsperando"), Is.EqualTo(0));
            Assert.That(Propriedade<bool>(pistaB, "ParkingReserved"), Is.False);
            Assert.That(Propriedade<int>(transporte, "TropasArmazenadas"), Is.EqualTo(0));
            Assert.That(float.IsNaN(transporte.transform.position.x), Is.False);
            Assert.That(float.IsNaN(transporte.transform.position.y), Is.False);
            Assert.That(float.IsNaN(transporte.transform.position.z), Is.False);
        }
        finally
        {
            if (transporteObjeto != null) UnityEngine.Object.Destroy(transporteObjeto);
            if (pistaAObjeto != null) UnityEngine.Object.Destroy(pistaAObjeto);
            if (pistaBObjeto != null) UnityEngine.Object.Destroy(pistaBObjeto);
            if (piso != null) UnityEngine.Object.Destroy(piso);
        }
    }

    private static IEnumerator EsperarEstado(Component transporte, string esperado, float timeout)
    {
        float fim = Time.realtimeSinceStartup + timeout;
        while (transporte != null && Estado(transporte) != esperado)
        {
            if (Time.realtimeSinceStartup >= fim)
            {
                Assert.Fail("O C700 não chegou ao estado " + esperado + ". Estado atual: " + Estado(transporte) + "; posição: " + transporte.transform.position);
            }

            yield return null;
        }
    }

    private static IEnumerator EsperarEstadoSolo(Component transporte, float timeout)
    {
        float fim = Time.realtimeSinceStartup + timeout;
        while (transporte != null && Estado(transporte) != "Solo")
        {
            if (Time.realtimeSinceStartup >= fim)
            {
                Assert.Fail("O C700 não voltou ao estado Solo dentro do tempo esperado. Estado atual: " + Estado(transporte) + "; posição: " + transporte.transform.position);
            }

            yield return null;
        }
    }

    private static GameObject CriarPiso()
    {
        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piso.name = "C700_PlayMode_Ground";
        piso.transform.position = new Vector3(0f, -1f, 0f);
        piso.transform.localScale = new Vector3(1600f, 2f, 1600f);
        return piso;
    }

    private static GameObject CriarPista(string nome, Vector3 posicao)
    {
        GameObject pistaObjeto = new GameObject(nome);
        pistaObjeto.transform.position = posicao;
        Type tipoPista = Type.GetType("MiniPistaLogistica, Assembly-CSharp");
        Assert.That(tipoPista, Is.Not.Null, "A classe MiniPistaLogistica precisa estar compilada no Assembly-CSharp.");
        Component pista = pistaObjeto.AddComponent(tipoPista);
        Campo(pista, "teamId", 1);
        Campo(pista, "raioAceitacaoDestino", 400f);
        return pistaObjeto;
    }

    private static string Estado(Component transporte)
    {
        FieldInfo campo = transporte.GetType().GetField("estadoAtual", BindingFlags.Instance | BindingFlags.Public);
        return campo == null ? string.Empty : campo.GetValue(transporte).ToString();
    }

    private static T Propriedade<T>(Component transporte, string nome)
    {
        PropertyInfo propriedade = transporte.GetType().GetProperty(nome, BindingFlags.Instance | BindingFlags.Public);
        return (T)propriedade.GetValue(transporte);
    }

    private static void Chamar(Component transporte, string nome, params object[] argumentos)
    {
        MethodInfo metodo = transporte.GetType().GetMethod(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(metodo, Is.Not.Null, "Método público ausente no C700: " + nome);
        metodo.Invoke(transporte, argumentos);
    }

    private static T ChamarComResultado<T>(Component componente, string nome, params object[] argumentos)
    {
        MethodInfo metodo = componente.GetType().GetMethod(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(metodo, Is.Not.Null, "Método público ausente: " + nome);
        return (T)metodo.Invoke(componente, argumentos);
    }

    private static void Campo(Component transporte, string nome, object valor)
    {
        FieldInfo campo = transporte.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(campo, Is.Not.Null, "Campo público ausente no C700: " + nome);
        campo.SetValue(transporte, valor);
    }

    private static T Campo<T>(Component componente, string nome)
    {
        FieldInfo campo = componente.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(campo, Is.Not.Null, "Campo público ausente: " + nome);
        return (T)campo.GetValue(componente);
    }
}
