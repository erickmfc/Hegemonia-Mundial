#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SistemaLogisticaMercadoSaveEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject logisticaObject;
    private Component logistica;

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        logisticaObject = new GameObject("Teste_LogisticaMercado_Save");
        logistica = logisticaObject.AddComponent(ResolveType("SistemaLogisticaMercado"));
    }

    [TearDown]
    public void TearDown()
    {
        if (logisticaObject != null) UnityEngine.Object.DestroyImmediate(logisticaObject);
    }

    [Test]
    public void SaveRoundTripRequeuesActiveOrderAndPreservesRepetition()
    {
        Type transacaoType = ResolveType("TransacaoMercado");
        object transacao = Activator.CreateInstance(transacaoType);
        Set(transacao, "id", "pedido-logistico-save");
        Set(transacao, "compradorTeamId", 2);
        Set(transacao, "vendedorTeamId", 3);
        Set(transacao, "itemId", "diesel");
        Set(transacao, "quantidade", 40);
        Set(transacao, "precoUnitario", 5);
        Set(transacao, "total", 200);

        object[] argumentos = { transacao, 10, false, null };
        bool aceito = (bool)Invocar(logistica, "Enfileirar", argumentos);
        Assert.That(aceito, Is.True);
        object pedido = argumentos[3];
        Set(pedido, "status", "EM TRANSITO");
        Set(pedido, "navioId", "navio-da-sessao-anterior");
        Set(pedido, "diaCriacao", 7);
        Set(pedido, "diaEntrega", 9);

        Type pedidoType = ResolveType("PedidoMercadoLogistico");
        object terminal = Activator.CreateInstance(pedidoType);
        Set(terminal, "id", "pedido-terminal");
        Set(terminal, "status", "ENTREGUE");
        ((IList)Read(logistica, "pedidos")).Add(terminal);

        Type repeticaoType = ResolveType("RepeticaoMercado");
        object repeticao = Activator.CreateInstance(repeticaoType);
        Set(repeticao, "chave", "diesel|comprar");
        Set(repeticao, "itemId", "diesel");
        Set(repeticao, "quantidade", 40);
        Set(repeticao, "comprar", true);
        Set(repeticao, "ativa", true);
        Set(repeticao, "ultimoDiaProcessado", 7);
        ((IList)Read(logistica, "repeticoes")).Add(repeticao);

        object snapshot = Invocar(logistica, "CriarSaveData");
        Type snapshotType = ResolveType("SistemaLogisticaMercadoSaveData");
        string json = JsonUtility.ToJson(snapshot);
        object roundTrip = JsonUtility.FromJson(json, snapshotType);
        IList pedidosSalvos = (IList)Read(roundTrip, "pedidos");
        Assert.That(pedidosSalvos.Count, Is.EqualTo(1), "Pedidos terminais não precisam voltar ao mundo.");

        Invocar(logistica, "RestaurarSaveData", roundTrip);
        IList pedidosRestaurados = (IList)Read(logistica, "pedidos");
        Assert.That(pedidosRestaurados.Count, Is.EqualTo(1));
        object pedidoRestaurado = pedidosRestaurados[0];
        Assert.That(Read<string>(pedidoRestaurado, "id"), Is.EqualTo("pedido-logistico-save"));
        Assert.That(Read<string>(pedidoRestaurado, "status"), Is.EqualTo("AGUARDANDO EMBARQUE"));
        Assert.That(Read<string>(pedidoRestaurado, "navioId"), Is.Empty);
        Assert.That(Read<int>(pedidoRestaurado, "compradorTeamId"), Is.EqualTo(2));
        Assert.That(Read<int>(pedidoRestaurado, "vendedorTeamId"), Is.EqualTo(3));
        Assert.That(Read<string>(pedidoRestaurado, "itemId"), Is.EqualTo("diesel"));
        Assert.That(Read<int>(pedidoRestaurado, "quantidade"), Is.EqualTo(40));
        Assert.That(Read<int>(pedidoRestaurado, "total"), Is.EqualTo(200));
        Assert.That(Read<int>(pedidoRestaurado, "frete"), Is.EqualTo(10));
        Assert.That(Read<int>(pedidoRestaurado, "diaCriacao"), Is.EqualTo(7));
        Assert.That(Read<int>(pedidoRestaurado, "diaEntrega"), Is.EqualTo(9));

        IList repeticoesRestauradas = (IList)Read(logistica, "repeticoes");
        Assert.That(repeticoesRestauradas.Count, Is.EqualTo(1));
        Assert.That(Read<string>(repeticoesRestauradas[0], "itemId"), Is.EqualTo("diesel"));
        Assert.That(Read<int>(repeticoesRestauradas[0], "quantidade"), Is.EqualTo(40));
        Assert.That(Read<bool>(repeticoesRestauradas[0], "comprar"), Is.True);
        Assert.That(Read<bool>(repeticoesRestauradas[0], "ativa"), Is.True);
        Assert.That(Read<int>(repeticoesRestauradas[0], "ultimoDiaProcessado"), Is.EqualTo(7));
    }

    [Test]
    public void SaveRoundTripRequeuesAwaitingDestinationOrderExactlyOnce()
    {
        Type pedidoType = ResolveType("PedidoMercadoLogistico");
        object pedido = Activator.CreateInstance(pedidoType);
        Set(pedido, "id", "pedido-aguardando-destino-save");
        Set(pedido, "compradorTeamId", 2);
        Set(pedido, "vendedorTeamId", 3);
        Set(pedido, "itemId", "aeronave-save-test");
        Set(pedido, "quantidade", 1);
        Set(pedido, "precoUnitario", 1200);
        Set(pedido, "total", 1200);
        Set(pedido, "frete", 60);
        Set(pedido, "status", "AGUARDANDO DESTINO");
        Set(pedido, "navioId", "navio-da-entrega-anterior");
        ((IList)Read(logistica, "pedidos")).Add(pedido);

        object snapshot = Invocar(logistica, "CriarSaveData");
        Type snapshotType = ResolveType("SistemaLogisticaMercadoSaveData");
        string json = JsonUtility.ToJson(snapshot);
        object roundTrip = JsonUtility.FromJson(json, snapshotType);

        IList pedidosSalvos = (IList)Read(roundTrip, "pedidos");
        Assert.That(pedidosSalvos.Count, Is.EqualTo(1));
        Assert.That(Read<string>(pedidosSalvos[0], "status"), Is.EqualTo("AGUARDANDO DESTINO"));

        Invocar(logistica, "RestaurarSaveData", roundTrip);

        IList pedidosRestaurados = (IList)Read(logistica, "pedidos");
        Assert.That(pedidosRestaurados.Count, Is.EqualTo(1), "O pedido pendente deve ser restaurado uma única vez.");
        object restaurado = pedidosRestaurados[0];
        Assert.That(Read<string>(restaurado, "id"), Is.EqualTo("pedido-aguardando-destino-save"));
        Assert.That(Read<string>(restaurado, "status"), Is.EqualTo("AGUARDANDO EMBARQUE"));
        Assert.That(Read<string>(restaurado, "navioId"), Is.Empty);
        Assert.That(Read<int>(restaurado, "compradorTeamId"), Is.EqualTo(2));
        Assert.That(Read<int>(restaurado, "vendedorTeamId"), Is.EqualTo(3));
        Assert.That(Read<string>(restaurado, "itemId"), Is.EqualTo("aeronave-save-test"));
        Assert.That(Read<int>(restaurado, "total"), Is.EqualTo(1200));
        Assert.That(Read<int>(restaurado, "frete"), Is.EqualTo(60));
    }

    [Test]
    public void LogisticsOnlySaveSchedulesWorldRestoration()
    {
        Type saveType = ResolveType("DadosDoJogo");
        object dados = Activator.CreateInstance(saveType);
        Set(dados, "saveVersion", 22);
        ((IList)Read(dados, "entidades")).Clear();
        ((IList)Read(dados, "estadosIA01")).Clear();
        ((IList)Read(dados, "estadosIA02")).Clear();
        ((IList)Read(dados, "cidadesAbstratas")).Clear();

        object logisticaSalva = Read(dados, "logisticaMercado");
        IList pedidos = (IList)Read(logisticaSalva, "pedidos");
        IList repeticoes = (IList)Read(logisticaSalva, "repeticoes");
        Type repeticaoType = ResolveType("RepeticaoMercado");
        object repeticao = Activator.CreateInstance(repeticaoType);
        Set(repeticao, "itemId", "diesel");
        Set(repeticao, "quantidade", 10);
        Set(repeticao, "comprar", true);
        Set(repeticao, "ativa", true);
        repeticoes.Add(repeticao);

        MethodInfo predicate = ResolveType("SistemaSaveGame").GetMethod("PossuiEstadoRestauravel", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(predicate, Is.Not.Null);
        Assert.That((bool)predicate.Invoke(null, new[] { dados }), Is.True);

        repeticoes.Clear();
        Type pedidoType = ResolveType("PedidoMercadoLogistico");
        object pedido = Activator.CreateInstance(pedidoType);
        Set(pedido, "id", "pedido-pendente");
        Set(pedido, "status", "EM TRANSITO");
        pedidos.Add(pedido);
        Assert.That((bool)predicate.Invoke(null, new[] { dados }), Is.True);

        pedidos.Clear();
        Assert.That((bool)predicate.Invoke(null, new[] { dados }), Is.False);
    }

    private static object Invocar(object alvo, string nome, params object[] argumentos)
    {
        MethodInfo metodo = alvo.GetType().GetMethod(nome, Members);
        Assert.That(metodo, Is.Not.Null, "Método não encontrado: " + nome);
        return metodo.Invoke(alvo, argumentos);
    }

    private static Type ResolveType(string nome)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type tipo = assembly.GetType(nome);
            if (tipo != null) return tipo;
        }
        Assert.Fail("Tipo não encontrado: " + nome);
        return null;
    }

    private static void Set(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, Members);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private static object Read(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, Members);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }

    private static T Read<T>(object alvo, string nome)
    {
        return (T)Read(alvo, nome);
    }
}
#endif
