using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// O assembly de testes deste projeto é isolado e não referencia Assembly-CSharp.
/// Estes testes usam reflexão para validar o runtime real sem mudar essa
/// configuração histórica apenas para a ETAPA 2.
/// </summary>
public sealed class ControleOrdemMovimentoEditModeTests
{
    private GameObject unidade;
    private Type runtimeType;
    private Type tipoOrdemType;

    [SetUp]
    public void SetUp()
    {
        unidade = new GameObject("UnidadeOrdemTeste");
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "Assembly-CSharp");
        Assert.That(assembly, Is.Not.Null);
        runtimeType = assembly.GetType("ControleOrdemMovimentoRuntime");
        Assert.That(runtimeType, Is.Not.Null);
        tipoOrdemType = runtimeType.Assembly.GetType("TipoOrdemMovimento");
        Assert.That(tipoOrdemType, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        if (unidade != null)
        {
            UnityEngine.Object.DestroyImmediate(unidade);
        }
    }

    [Test]
    public void OrdemDuplicadaComMesmoIdDonoEDestinoEIdempotente()
    {
        object runtime = CriarRuntime(2f);
        Vector3 destino = new Vector3(10f, 0f, 20f);

        object[] args = { "ordem-1", "ControleUnidade", unidade, destino, EnumOrdem("Terrestre"), 1f, false };
        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.True);
        Assert.That((bool)args[6], Is.False);
        Assert.That(InvocarBool(runtime, "TentarIniciarTentativa", 1f), Is.True);
        Assert.That(InvocarBool(runtime, "ComecarMonitoramento", 1f), Is.True);

        args = new object[] { "ordem-1", "ControleUnidade", unidade, destino, EnumOrdem("Terrestre"), 2f, false };
        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.True);
        Assert.That((bool)args[6], Is.True);
        Assert.That((int)Membro(Atual(runtime), "Tentativas"), Is.EqualTo(1));
        Assert.That(Membro(Atual(runtime), "Estado").ToString(), Is.EqualTo("Monitorando"));
    }

    [Test]
    public void OrdemComSemProgressoTemNoMaximoDuasTentativas()
    {
        object runtime = CriarRuntime(2f);
        Vector3 destino = new Vector3(10f, 0f, 20f);
        object[] args = { "ordem-2", "ExecutorTeste", unidade, destino, EnumOrdem("Naval"), 0f, false };

        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.True);
        Assert.That(InvocarBool(runtime, "TentarIniciarTentativa", 0f), Is.True);
        Assert.That(InvocarBool(runtime, "ComecarMonitoramento", 0f), Is.True);
        Assert.That(InvocarBool(runtime, "AgendarNovaTentativa", 8f, "sem progresso"), Is.True);
        Assert.That(Membro(Atual(runtime), "Estado").ToString(), Is.EqualTo("EsperandoNovaTentativa"));
        Assert.That(InvocarBool(runtime, "PrepararRecalculo", 10f), Is.True);
        Assert.That(InvocarBool(runtime, "TentarIniciarTentativa", 10f), Is.True);
        Assert.That(InvocarBool(runtime, "ComecarMonitoramento", 10f), Is.True);
        Assert.That(InvocarBool(runtime, "AgendarNovaTentativa", 18f, "sem progresso novamente"), Is.False);
        Assert.That(Membro(Atual(runtime), "Estado").ToString(), Is.EqualTo("Recalculando"));
        Assert.That(InvocarBool(runtime, "Falhar", "tentativas esgotadas", 18f), Is.True);
        Assert.That((int)Membro(Atual(runtime), "Tentativas"), Is.EqualTo(2));
        Assert.That(Membro(Atual(runtime), "Estado").ToString(), Is.EqualTo("Falhou"));
    }

    [Test]
    public void DonoDiferenteNaoPodeExecutarAMesmaOrdem()
    {
        object runtime = CriarRuntime(2f);
        Vector3 destino = new Vector3(2f, 0f, 3f);
        object[] args = { "ordem-3", "ExecutorA", unidade, destino, EnumOrdem("Aerea"), 0f, false };

        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.True);
        args = new object[] { "ordem-3", "ExecutorB", unidade, destino, EnumOrdem("Aerea"), 1f, false };
        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.False);
        Assert.That(Membro(Atual(runtime), "Dono"), Is.EqualTo("ExecutorA"));
    }

    [Test]
    public void ConclusaoNaoPodeSerEmitidaDuasVezes()
    {
        object runtime = CriarRuntime(2f);
        object[] args = { "ordem-4", "ExecutorTeste", unidade, Vector3.one, EnumOrdem("Patrulha"), 0f, false };

        Assert.That(InvocarBool(runtime, "TentarIniciar", args), Is.True);
        Assert.That(InvocarBool(runtime, "TentarIniciarTentativa", 0f), Is.True);
        Assert.That(InvocarBool(runtime, "ComecarMonitoramento", 0f), Is.True);
        Assert.That(InvocarBool(runtime, "Concluir", 4f), Is.True);
        Assert.That(InvocarBool(runtime, "Concluir", 5f), Is.False);
        Assert.That(Membro(Atual(runtime), "Estado").ToString(), Is.EqualTo("Concluida"));
    }

    private object CriarRuntime(float intervalo)
    {
        return Activator.CreateInstance(runtimeType, new object[] { intervalo });
    }

    private object EnumOrdem(string nome)
    {
        return Enum.Parse(tipoOrdemType, nome);
    }

    private object Atual(object runtime)
    {
        return runtimeType.GetProperty("Atual").GetValue(runtime);
    }

    private static object Membro(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome);
        if (campo != null) return campo.GetValue(alvo);
        return alvo.GetType().GetProperty(nome).GetValue(alvo);
    }

    private bool InvocarBool(object alvo, string nome, params object[] argumentos)
    {
        return (bool)runtimeType.GetMethod(nome).Invoke(alvo, argumentos);
    }
}
