using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class GuidagemAlvoMovelEditModeTests
{
    private GameObject alvo;

    private static Type TipoGuidagem()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GuidagemAlvoMovel"))
            .FirstOrDefault(type => type != null);
    }

    [SetUp]
    public void SetUp()
    {
        alvo = new GameObject("AlvoMovelGuidagemTeste");
    }

    [TearDown]
    public void TearDown()
    {
        if (alvo != null) UnityEngine.Object.DestroyImmediate(alvo);
    }

    [Test]
    public void PrevisaoRespeitaVelocidadeDoAlvoEAntecipacaoMaxima()
    {
        alvo.transform.position = new Vector3(100f, 20f, 0f);
        Rigidbody corpo = alvo.AddComponent<Rigidbody>();
        corpo.linearVelocity = new Vector3(30f, 0f, 0f);

        Type tipo = TipoGuidagem();
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod("ObterPontoDeMira", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        Vector3 ponto = (Vector3)metodo.Invoke(null, new object[]
        {
            alvo.transform,
            Vector3.zero,
            50f,
            2f
        });

        Assert.That(ponto.x, Is.GreaterThan(alvo.transform.position.x));
        Assert.That(ponto.x, Is.LessThanOrEqualTo(alvo.transform.position.x + 60.01f));
        Assert.That(ponto.y, Is.EqualTo(alvo.transform.position.y));
    }

    [Test]
    public void SegmentoDetectaAlvoMesmoQuandoFimDoFrameJaPassouDoPonto()
    {
        Type tipo = TipoGuidagem();
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod("SegmentoAtingePonto", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);

        Assert.That(
            (bool)metodo.Invoke(null, new object[]
            {
                new Vector3(-20f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                Vector3.zero,
                1f}),
            Is.True);

        Assert.That(
            (bool)metodo.Invoke(null, new object[]
            {
                new Vector3(-20f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                new Vector3(0f, 5f, 0f),
                1f}),
            Is.False);
    }
}
