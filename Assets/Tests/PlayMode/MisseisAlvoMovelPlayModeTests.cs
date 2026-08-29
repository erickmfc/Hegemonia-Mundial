using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MisseisAlvoMovelPlayModeTests
{
    [UnityTest]
    public IEnumerator MisselTaticoAtualizaAlvoVivoSemTeletransportar()
    {
        Type misselType = ResolveType("MisselTatico");
        GameObject alvo = new GameObject("AlvoMovelMisselTaticoPlayMode");
        GameObject misselObjeto = new GameObject("MisselTaticoPlayMode");

        try
        {
            alvo.transform.position = new Vector3(1000f, 0f, 0f);
            Rigidbody corpoAlvo = alvo.AddComponent<Rigidbody>();
            corpoAlvo.useGravity = false;
            corpoAlvo.linearVelocity = new Vector3(0f, 0f, 30f);

            misselObjeto.transform.position = Vector3.zero;
            Component missel = misselObjeto.AddComponent(misselType);
            SetField(misselType, missel, "velocidade", 50f);
            SetField(misselType, missel, "velocidadeDeGiro", 240f);
            SetField(misselType, missel, "atrasoParaVirar", 0f);

            MethodInfo iniciar = misselType.GetMethod(
                "IniciarLancamento",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector3), typeof(Transform) },
                null);
            Assert.That(iniciar, Is.Not.Null);
            iniciar.Invoke(missel, new object[] { alvo.transform.position, alvo.transform });

            yield return null;

            // Um míssil pode avançar somente a sua velocidade por frame; ele
            // nunca pode aparecer na coordenada inicial do alvo.
            Assert.That(misselObjeto.transform.position.x, Is.LessThan(20f));

            alvo.transform.position += new Vector3(0f, 0f, 40f);
            yield return null;

            FieldInfo alvoField = misselType.GetField("alvo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(alvoField, Is.Not.Null);
            Assert.That((Vector3)alvoField.GetValue(missel), Is.EqualTo(alvo.transform.position));
        }
        finally
        {
            UnityEngine.Object.Destroy(alvo);
            UnityEngine.Object.Destroy(misselObjeto);
        }
    }

    [UnityTest]
    public IEnumerator TorpedoPorCoordenadaNaoAdquireOutroAlvo()
    {
        Type torpedoType = ResolveType("Torpedo");
        GameObject torpedoObjeto = new GameObject("TorpedoCoordenadaPlayMode");

        try
        {
            Component torpedo = torpedoObjeto.AddComponent(torpedoType);
            MethodInfo definirAlvo = torpedoType.GetMethod(
                "DefinirAlvo",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector3) },
                null);
            Assert.That(definirAlvo, Is.Not.Null);

            Vector3 pontoOrdenado = new Vector3(500f, 0f, 700f);
            definirAlvo.Invoke(torpedo, new object[] { pontoOrdenado });
            yield return null;

            FieldInfo alvoFixoField = torpedoType.GetField("alvoFixoPorCoordenada", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo pontoField = torpedoType.GetField("posicaoAlvoPerdido", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(alvoFixoField, Is.Not.Null);
            Assert.That(pontoField, Is.Not.Null);
            Assert.That((bool)alvoFixoField.GetValue(torpedo), Is.True);
            Assert.That((Vector3)pontoField.GetValue(torpedo), Is.EqualTo(pontoOrdenado));
        }
        finally
        {
            UnityEngine.Object.Destroy(torpedoObjeto);
        }
    }

    [UnityTest]
    public IEnumerator TorpedoPorCoordenadaCorrigeRotaAteOPonto()
    {
        Type torpedoType = ResolveType("Torpedo");
        GameObject torpedoObjeto = new GameObject("TorpedoRotaCoordenadaPlayMode");

        try
        {
            torpedoObjeto.transform.forward = Vector3.right;
            Component torpedo = torpedoObjeto.AddComponent(torpedoType);
            SetField(torpedoType, torpedo, "velocidade", 30f);
            SetField(torpedoType, torpedo, "taxaCurva", 0.4f);
            MethodInfo definirAlvo = torpedoType.GetMethod(
                "DefinirAlvo",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector3) },
                null);
            Assert.That(definirAlvo, Is.Not.Null);
            definirAlvo.Invoke(torpedo, new object[] { new Vector3(0f, 0f, 80f) });

            yield return new WaitForSeconds(0.35f);

            Assert.That(torpedoObjeto.transform.position.z, Is.GreaterThan(0.1f));
        }
        finally
        {
            UnityEngine.Object.Destroy(torpedoObjeto);
        }
    }

    [UnityTest]
    public IEnumerator MissilTeleguiadoPorCoordenadaNaoProcuraAlvoLegado()
    {
        Type misselType = ResolveType("MissilTeleguiado");
        GameObject misselObjeto = new GameObject("MissilTeleguiadoCoordenadaPlayMode");

        try
        {
            Component missel = misselObjeto.AddComponent(misselType);
            SetField(misselType, missel, "velocidade", 40f);
            MethodInfo definirAlvo = misselType.GetMethod(
                "DefinirAlvo",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector3) },
                null);
            Assert.That(definirAlvo, Is.Not.Null);

            Vector3 pontoOrdenado = new Vector3(0f, 0f, 80f);
            definirAlvo.Invoke(missel, new object[] { pontoOrdenado });
            yield return new WaitForSeconds(0.15f);

            FieldInfo fixoField = misselType.GetField("alvoFixoPorCoordenada", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo alvoField = misselType.GetField("alvo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fixoField, Is.Not.Null);
            Assert.That(alvoField, Is.Not.Null);
            Assert.That((bool)fixoField.GetValue(missel), Is.True);
            Assert.That(alvoField.GetValue(missel), Is.Null);
            Assert.That(misselObjeto.transform.position.z, Is.GreaterThan(0.1f));
        }
        finally
        {
            UnityEngine.Object.Destroy(misselObjeto);
        }
    }

    [UnityTest]
    public IEnumerator MisselBombardeiroRastreadoAtivaAlvoMovelMesmoComPrefabDesligado()
    {
        Type misselType = ResolveType("MisselBombardeiro");
        GameObject alvo = new GameObject("AlvoBombardeiroMovelPlayMode");
        GameObject misselObjeto = new GameObject("MisselBombardeiroPlayMode");

        try
        {
            Component missel = misselObjeto.AddComponent(misselType);
            SetField(misselType, missel, "rastrearAlvoMovel", false);

            MethodInfo iniciar = misselType.GetMethod(
                "IniciarLancamentoRastreado",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Transform), typeof(GameObject) },
                null);
            Assert.That(iniciar, Is.Not.Null);
            iniciar.Invoke(missel, new object[] { alvo.transform, null });

            FieldInfo rastrearField = misselType.GetField("rastrearAlvoMovel", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(rastrearField, Is.Not.Null);
            Assert.That((bool)rastrearField.GetValue(missel), Is.True);
            yield return null;
        }
        finally
        {
            UnityEngine.Object.Destroy(alvo);
            UnityEngine.Object.Destroy(misselObjeto);
        }
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não encontrado: " + name);
        return type;
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        field.SetValue(target, value);
    }
}
