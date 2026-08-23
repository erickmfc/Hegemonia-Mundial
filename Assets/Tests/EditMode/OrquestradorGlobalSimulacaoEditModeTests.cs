using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OrquestradorGlobalSimulacaoEditModeTests
{
    private readonly List<GameObject> objetos = new List<GameObject>();
    private Component scheduler;
    private Type schedulerType;
    private Type camadaType;

    [SetUp]
    public void SetUp()
    {
        schedulerType = ResolveType("Hegemonia.RTS.OrquestradorGlobalSimulacao");
        camadaType = ResolveType("Hegemonia.RTS.CamadaSimulacao");
        Assert.That(schedulerType, Is.Not.Null);
        Assert.That(camadaType, Is.Not.Null);
        GameObject objeto = new GameObject("scheduler-test");
        objetos.Add(objeto);
        scheduler = objeto.AddComponent(schedulerType);
        Invoke("LimparTarefasParaTeste");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < objetos.Count; i++)
        {
            if (objetos[i] != null) UnityEngine.Object.DestroyImmediate(objetos[i]);
        }
        objetos.Clear();
    }

    [Test]
    public void RegistroDuplicadoMantemUmaTarefa()
    {
        Func<float, bool> callback = now => true;
        Assert.That(Register("pais-1/ordens", 1, "Operacional", 0.2f, 1f, callback, 0f), Is.True);
        Assert.That(Register("pais-1/ordens", 1, "Operacional", 0.2f, 1f, callback, 0f), Is.True);
        Assert.That(Register("pais-1/ordens", 2, "Operacional", 0.2f, 1f, callback, 0f), Is.False);
        Assert.That((int)schedulerType.GetProperty("TarefasRegistradas").GetValue(scheduler), Is.EqualTo(1));
    }

    [Test]
    public void RemocaoEIdEstavelNaoDependemDoInstanceId()
    {
        Assert.That(Register("team:17/unit:truck-04", 17, "Adormecida", 1f, 2f, now => true, 0f), Is.True);
        Assert.That((bool)Invoke("Remover", "team:17/unit:truck-04"), Is.True);
        Assert.That((int)schedulerType.GetProperty("TarefasRegistradas").GetValue(scheduler), Is.Zero);
    }

    [Test]
    public void DistribuicaoRoundRobinExecutaTodosSemDuplicar()
    {
        var ordem = new List<string>();
        Register("a", 1, "Operacional", 0f, 1f, now => { ordem.Add("a"); return true; }, 0f);
        Register("b", 2, "Operacional", 0f, 1f, now => { ordem.Add("b"); return true; }, 0f);
        Register("c", 3, "Operacional", 0f, 1f, now => { ordem.Add("c"); return true; }, 0f);
        Invoke("ExecutarAgoraParaTeste", 0f);
        Assert.That(ordem, Is.EquivalentTo(new[] { "a", "b", "c" }));
        Assert.That(ordem.Count, Is.EqualTo(3));
    }

    [Test]
    public void DespertarImediatoDuplicadoEAgrupado()
    {
        Register("selected-unit", 4, "Adormecida", 1f, 2f, now => true, 50f);
        Assert.That((bool)Invoke("SolicitarTickImediato", "selected-unit"), Is.True);
        Assert.That((bool)Invoke("SolicitarTickImediato", "selected-unit"), Is.True);
        object snapshot = Invoke("ObterSnapshot");
        Assert.That((int)snapshot.GetType().GetField("DespertaresImediatos").GetValue(snapshot), Is.EqualTo(1));
        Assert.That((int)snapshot.GetType().GetField("DespertaresAgrupados").GetValue(snapshot), Is.EqualTo(1));
    }

    [Test]
    public void FrequenciaNaoGeraCatchUpStorm()
    {
        int calls = 0;
        Register("economia:atlas", 1, "Estrategica", 1f, 2f, now => { calls++; return true; }, 0f);
        Invoke("ExecutarAgoraParaTeste", 0f);
        Invoke("ExecutarAgoraParaTeste", 0.1f);
        Invoke("ExecutarAgoraParaTeste", 0.2f);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void PausaNaoAcumulaTicksAoRetornar()
    {
        int calls = 0;
        Register("naval:tanker-2", 2, "Operacional", 0.2f, 0.5f, now => { calls++; return true; }, 0f);
        Invoke("ExecutarAgoraParaTeste", 0f);
        Invoke("DefinirPausado", true);
        Invoke("DefinirPausado", false);
        Invoke("ExecutarAgoraParaTeste", 100f);
        Assert.That(calls, Is.EqualTo(2));
    }

    private bool Register(string id, int dono, string camada, float frequencia, float prazo,
        Func<float, bool> callback, float agora)
    {
        MethodInfo method = schedulerType.GetMethod("Registrar", new[]
        {
            typeof(string), typeof(int), camadaType, typeof(float), typeof(float), typeof(Func<float, bool>), typeof(float)
        });
        return (bool)method.Invoke(scheduler, new object[] { id, dono, Enum.Parse(camadaType, camada), frequencia, prazo, callback, agora });
    }

    private object Invoke(string name, params object[] args)
    {
        return schedulerType.GetMethod(name).Invoke(scheduler, args);
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }
}
