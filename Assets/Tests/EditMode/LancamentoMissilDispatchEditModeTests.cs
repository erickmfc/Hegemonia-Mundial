using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LancamentoMissilDispatchEditModeTests
{
    private readonly List<GameObject> objetos = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objetos.Count - 1; i >= 0; i--)
        {
            if (objetos[i] != null) UnityEngine.Object.DestroyImmediate(objetos[i]);
        }
        objetos.Clear();
    }

    [Test]
    public void DespachoPorCoordenadaNaoDeixaMissilGuiadoSemDestino()
    {
        GameObject origem = Criar("OrigemDispatch");
        GameObject missil = Criar("MissilDispatch");
        Component guiado = missil.AddComponent(Tipo("MissilTeleguiado"));

        Vector3 destino = new Vector3(321f, 18f, -77f);
        Assert.That(
            Inicializar(missil, destino, null, origem.transform, origem.transform, origem),
            Is.True);

        FieldInfo alvoFixo = Tipo("MissilTeleguiado").GetField(
            "pontoAlvoFixo", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo modoFixo = Tipo("MissilTeleguiado").GetField(
            "alvoFixoPorCoordenada", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(alvoFixo, Is.Not.Null);
        Assert.That(modoFixo, Is.Not.Null);
        Assert.That((Vector3)alvoFixo.GetValue(guiado), Is.EqualTo(destino));
        Assert.That((bool)modoFixo.GetValue(guiado), Is.True);
    }

    [Test]
    public void DespachoComAlvoMovelEntregaOTransformAoControladorTatico()
    {
        GameObject origem = Criar("OrigemTaticoDispatch");
        GameObject alvo = Criar("AlvoMovelDispatch");
        GameObject missil = Criar("TaticoDispatch");
        Component tatico = missil.AddComponent(Tipo("MisselTatico"));

        Assert.That(
            Inicializar(missil, alvo.transform.position, alvo.transform, origem.transform, origem.transform, origem),
            Is.True);

        FieldInfo campoAlvo = Tipo("MisselTatico").GetField(
            "alvoTransform", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(campoAlvo, Is.Not.Null);
        Assert.That(campoAlvo.GetValue(tatico), Is.EqualTo(alvo.transform));
    }

    [Test]
    public void DespachoDeTorpedoPorCoordenadaMantemModoFixo()
    {
        GameObject origem = Criar("OrigemTorpedoDispatch");
        GameObject missil = Criar("TorpedoDispatch");
        Component torpedo = missil.AddComponent(Tipo("Torpedo"));

        Assert.That(
            Inicializar(missil, new Vector3(-90f, -2f, 42f), null, origem.transform, origem.transform, origem),
            Is.True);

        FieldInfo modoFixo = Tipo("Torpedo").GetField(
            "alvoFixoPorCoordenada", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(modoFixo, Is.Not.Null);
        Assert.That((bool)modoFixo.GetValue(torpedo), Is.True);
    }

    private static bool Inicializar(
        GameObject missil,
        Vector3 destino,
        Transform alvoMovel,
        Transform origem,
        Transform lancador,
        GameObject dono)
    {
        MethodInfo metodo = Tipo("InicializadorLancamentoMissil").GetMethod(
            "Inicializar",
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(metodo, Is.Not.Null);
        object resultado = metodo.Invoke(null, new object[]
        {
            missil,
            destino,
            alvoMovel,
            origem,
            lancador,
            dono,
            false,
            Vector3.zero
        });
        return (bool)resultado;
    }

    private static Type Tipo(string nome)
    {
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "Assembly-CSharp");
        Assert.That(assembly, Is.Not.Null);
        Type tipo = assembly.GetType(nome);
        Assert.That(tipo, Is.Not.Null, "Tipo ausente no Assembly-CSharp: " + nome);
        return tipo;
    }

    private GameObject Criar(string nome)
    {
        GameObject objeto = new GameObject(nome);
        objetos.Add(objeto);
        return objeto;
    }
}
