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
        GameObject soldado = null;

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

            yield return null;
            Campo(transporte, "velocidadeCruzeiro", 100f);
            Campo(transporte, "velocidadeDecolagem", 35f);
            Campo(transporte, "altitudeCruzeiro", 45f);
            Campo(transporte, "distanciaAproximacao", 65f);
            Campo(transporte, "distanciaDescida", 30f);
            Campo(transporte, "distanciaRolagem", 20f);
            Campo(transporte, "raioBuscaCarga", 30f);
            Campo(transporte, "debugLogs", true);
            Assert.That(Estado(transporte), Is.EqualTo("Solo"));
            Assert.That(Propriedade<int>(transporte, "CapacidadeCargaAtual"), Is.GreaterThan(0));

            Vector3 primeiroDestino = new Vector3(340f, 0f, 0f);
            Chamar(transporte, "ReceberOrdemMover", primeiroDestino);
            yield return EsperarEstadoSolo(transporte, 55f);

            Assert.That(Estado(transporte), Is.EqualTo("Solo"));
            Assert.That(Vector3.Distance(new Vector3(transporte.transform.position.x, 0f, transporte.transform.position.z), primeiroDestino), Is.LessThan(6f));
            Assert.That(transporte.transform.position.y, Is.EqualTo(0.2f).Within(0.3f));

            soldado = CriarSoldado(transporte.transform.position + Vector3.right * 4f);
            yield return new WaitForFixedUpdate();
            Chamar(transporte, "PuxarUnidadesProximas");
            yield return new WaitForSeconds(1.5f);

            Assert.That(Propriedade<int>(transporte, "QuantidadeCargaAtual"), Is.EqualTo(1), "O transporte deveria embarcar uma unidade próxima quando está no solo.");
            Assert.That(soldado.activeSelf, Is.False, "A unidade embarcada deve ficar protegida dentro da carga.");

            Chamar(transporte, "DesembarcarTudo");
            yield return null;

            Assert.That(Propriedade<int>(transporte, "QuantidadeCargaAtual"), Is.EqualTo(0));
            Assert.That(soldado.activeSelf, Is.True, "A unidade deve voltar ao cenário ao descarregar.");

            Vector3 segundoDestino = new Vector3(-300f, 0f, 80f);
            Chamar(transporte, "ReceberOrdemMover", segundoDestino);
            yield return EsperarEstadoSolo(transporte, 65f);

            Assert.That(Estado(transporte), Is.EqualTo("Solo"));
            Assert.That(Vector3.Distance(new Vector3(transporte.transform.position.x, 0f, transporte.transform.position.z), segundoDestino), Is.LessThan(6f));
            Assert.That(transporte.transform.position.y, Is.EqualTo(0.2f).Within(0.3f));
            Assert.That(float.IsNaN(transporte.transform.position.x), Is.False);
            Assert.That(float.IsNaN(transporte.transform.position.y), Is.False);
            Assert.That(float.IsNaN(transporte.transform.position.z), Is.False);
        }
        finally
        {
            if (soldado != null) UnityEngine.Object.Destroy(soldado);
            if (transporteObjeto != null) UnityEngine.Object.Destroy(transporteObjeto);
            if (piso != null) UnityEngine.Object.Destroy(piso);
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

    private static GameObject CriarSoldado(Vector3 posicao)
    {
        GameObject soldado = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        soldado.name = "Soldado_C700_PlayMode";
        soldado.transform.position = posicao;
        return soldado;
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

    private static void Campo(Component transporte, string nome, object valor)
    {
        FieldInfo campo = transporte.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(campo, Is.Not.Null, "Campo público ausente no C700: " + nome);
        campo.SetValue(transporte, valor);
    }
}
