using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OrquestradorGlobalOrdensEditModeTests
{
    private GameObject unidade;
    private Type orquestradorType;
    private Type ordemType;
    private Type tipoOrdemType;
    private Type estadoOrdemType;
    private int conclusoes;
    private Delegate listenerConclusao;
    private bool listenerConclusaoRegistrado;

    [SetUp]
    public void SetUp()
    {
        unidade = new GameObject("OrquestradorGlobalOrdensTestUnit");
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "Assembly-CSharp");
        Assert.That(assembly, Is.Not.Null);
        orquestradorType = assembly.GetType("OrquestradorGlobalOrdens");
        ordemType = assembly.GetType("OrdemMovimento");
        tipoOrdemType = assembly.GetType("TipoOrdemMovimento");
        estadoOrdemType = assembly.GetType("EstadoOrdemMovimento");
        Assert.That(orquestradorType, Is.Not.Null);
        Assert.That(ordemType, Is.Not.Null);
        Assert.That(tipoOrdemType, Is.Not.Null);
        Assert.That(estadoOrdemType, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        RemoverListenerConclusao();
        if (unidade != null)
        {
            Invocar(
                "LiberarUnidade",
                unidade,
                "limpeza do teste",
                10f);
            UnityEngine.Object.DestroyImmediate(unidade);
        }
    }

    [Test]
    public void ReemissaoIdenticaEhIdempotenteEOutroControladorNaoAssume()
    {
        string id = "teste-global-" + Guid.NewGuid().ToString("N");
        object[] args = ArgumentosRegistro(
            id,
            "ControladorTerrestre",
            new Vector3(10f, 0f, 20f),
            "Terrestre",
            1f);

        Assert.IsTrue((bool)Invocar("TentarRegistrar", args));
        Assert.IsFalse((bool)args[6]);

        args = ArgumentosRegistro(
            id,
            "ControladorTerrestre",
            new Vector3(10f, 0f, 20f),
            "Terrestre",
            2f);
        Assert.IsTrue((bool)Invocar("TentarRegistrar", args));
        Assert.IsTrue((bool)args[6]);

        args = ArgumentosRegistro(
            id,
            "OutroControlador",
            new Vector3(10f, 0f, 20f),
            "Terrestre",
            3f);
        Assert.IsFalse((bool)Invocar("TentarRegistrar", args));
        StringAssert.Contains("ID de ordem", (string)args[7]);
    }

    [Test]
    public void NovaOrdemSubstituiAAnteriorComCancelamentoExplicito()
    {
        string primeiraId = "teste-substituicao-a-" + Guid.NewGuid().ToString("N");
        string segundaId = "teste-substituicao-b-" + Guid.NewGuid().ToString("N");

        Assert.IsTrue((bool)Invocar(
            "TentarRegistrar",
            ArgumentosRegistro(
                primeiraId,
                "ControladorA",
                Vector3.right,
                "Logistica",
                1f)));
        Assert.IsTrue((bool)Invocar(
            "TentarRegistrar",
            ArgumentosRegistro(
                segundaId,
                "ControladorB",
                Vector3.forward,
                "Patrulha",
                2f)));

        object anterior = ObterRegistro(primeiraId);
        Assert.AreEqual("Cancelada", Ler(anterior, "Estado").ToString());
        StringAssert.Contains("substituida", (string)Ler(anterior, "MotivoFalhaOuCancelamento"));

        object[] consulta = { unidade, null };
        Assert.IsTrue((bool)Invocar("UnidadePossuiOrdemAtiva", consulta));
        Assert.AreEqual(segundaId, Ler(consulta[1], "Id"));
    }

    [Test]
    public void ConclusaoPublicaEventoUmaUnicaVezMesmoComNotificacaoDuplicada()
    {
        string id = "teste-conclusao-" + Guid.NewGuid().ToString("N");
        Assert.IsTrue((bool)Invocar(
            "TentarRegistrar",
            ArgumentosRegistro(
                id,
                "ControleAereo",
                new Vector3(4f, 5f, 6f),
                "Aerea",
                1f)));

        AdicionarListenerConclusao();
        object ordem = Activator.CreateInstance(ordemType);
        Definir(ordem, "Id", id);
        Definir(ordem, "Dono", "ControleAereo");
        Definir(ordem, "Unidade", unidade);
        Definir(ordem, "Destino", new Vector3(4f, 5f, 6f));
        Definir(ordem, "Tipo", Enum.Parse(tipoOrdemType, "Aerea"));
        Definir(ordem, "Estado", Enum.Parse(estadoOrdemType, "Concluida"));
        Definir(ordem, "Tentativas", 1);
        Definir(ordem, "HorarioCriacao", 1f);
        Definir(ordem, "UltimoMomentoDeProgresso", 5f);

        Invocar(
            "NotificarEstado",
            ordem,
            Enum.Parse(estadoOrdemType, "Monitorando"),
            Enum.Parse(estadoOrdemType, "Concluida"),
            5f);
        Invocar(
            "NotificarEstado",
            ordem,
            Enum.Parse(estadoOrdemType, "Monitorando"),
            Enum.Parse(estadoOrdemType, "Concluida"),
            6f);

        Assert.AreEqual(1, conclusoes);
        Assert.AreEqual("Concluida", Ler(ObterRegistro(id), "Estado").ToString());
    }

    [Test]
    public void UnidadeLiberadaCancelaOrdemAtivaSemDeixarRegistroAtivo()
    {
        string id = "teste-liberacao-" + Guid.NewGuid().ToString("N");
        Assert.IsTrue((bool)Invocar(
            "TentarRegistrar",
            ArgumentosRegistro(
                id,
                "CaminhaoCombustivel",
                new Vector3(8f, 0f, 9f),
                "Logistica",
                1f)));

        Assert.IsTrue((bool)Invocar(
            "LiberarUnidade",
            unidade,
            "unidade destruida",
            4f));
        object[] consulta = { unidade, null };
        Assert.IsFalse((bool)Invocar("UnidadePossuiOrdemAtiva", consulta));
        Assert.AreEqual("Cancelada", Ler(ObterRegistro(id), "Estado").ToString());
        Assert.AreEqual("unidade destruida", Ler(ObterRegistro(id), "MotivoFalhaOuCancelamento"));
    }

    private object[] ArgumentosRegistro(
        string id,
        string dono,
        Vector3 destino,
        string tipo,
        float agora)
    {
        return new object[]
        {
            id,
            dono,
            unidade,
            destino,
            Enum.Parse(tipoOrdemType, tipo),
            agora,
            false,
            string.Empty
        };
    }

    private object ObterRegistro(string id)
    {
        object[] args = { id, null };
        Assert.IsTrue((bool)Invocar("TentarObter", args));
        return args[1];
    }

    private object Invocar(string nome, params object[] args)
    {
        MethodInfo metodo = orquestradorType.GetMethod(nome, BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null, nome);
        return metodo.Invoke(null, args);
    }

    private static object Ler(object alvo, string nome)
    {
        PropertyInfo propriedade = alvo.GetType().GetProperty(nome);
        Assert.That(propriedade, Is.Not.Null, nome);
        return propriedade.GetValue(alvo);
    }

    private static void Definir(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome);
        if (campo != null)
        {
            campo.SetValue(alvo, valor);
            return;
        }

        PropertyInfo propriedade = alvo.GetType().GetProperty(nome);
        Assert.That(propriedade, Is.Not.Null, nome);
        propriedade.SetValue(alvo, valor);
    }

    private void AdicionarListenerConclusao()
    {
        EventInfo evento = orquestradorType.GetEvent("OrdemConcluida", BindingFlags.Public | BindingFlags.Static);
        Assert.That(evento, Is.Not.Null);
        Type argumento = evento.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType;
        ParameterExpression parametro = Expression.Parameter(argumento, "registro");
        MethodInfo callback = typeof(OrquestradorGlobalOrdensEditModeTests).GetMethod(
            nameof(RegistrarConclusao),
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodCallExpression chamada = Expression.Call(
            Expression.Constant(this),
            callback,
            Expression.Convert(parametro, typeof(object)));
        listenerConclusao = Expression.Lambda(
            evento.EventHandlerType,
            chamada,
            parametro).Compile();
        evento.AddEventHandler(null, listenerConclusao);
        listenerConclusaoRegistrado = true;
    }

    private void RemoverListenerConclusao()
    {
        EventInfo evento = orquestradorType?.GetEvent("OrdemConcluida", BindingFlags.Public | BindingFlags.Static);
        if (evento == null || !listenerConclusaoRegistrado) return;
        evento.RemoveEventHandler(null, listenerConclusao);
        listenerConclusao = null;
        listenerConclusaoRegistrado = false;
    }

    private void RegistrarConclusao(object _)
    {
        conclusoes++;
    }
}
