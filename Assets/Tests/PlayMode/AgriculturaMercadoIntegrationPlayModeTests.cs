using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AgriculturaMercadoIntegrationPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const int BuyerTeam = 1;
    private const int SellerTeam = 2;
    private static readonly string[] StockIds = {
        "sementes", "agua", "fertilizante_organico", "agrotoxicos", "energia",
        "comida_milho", "comida_batata", "comida_feijao", "comida_trigo", "comida_arroz",
        "comida_cana", "comida_soja", "comida_cafe", "comida_cacau", "comida_mandioca",
        "comida_aveia", "comida_cevada", "comida_tomate", "comida_frutas", "comida_hortalicas"
    };

    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Dictionary<string, int> stockSnapshot = new Dictionary<string, int>();
    private readonly List<object[]> itemSnapshot = new List<object[]>();
    private object governo;
    private object mercado;
    private object logistica;
    private object tempo;
    private object comprador;
    private object vendedor;
    private object relacao;
    private object itemSementes;
    private object itemEnergia;
    private int relationValue;
    private bool relationTrade;
    private bool relationWar;
    private bool relationSanction;
    private int oldDay;
    private long buyerBalance;
    private long sellerBalance;
    private int historyCount;
    private int orderCount;
    private int researchCount;
    private IList researchList;
    private object cashOnResourcesManager;
    private Component resourcesManager;

    [UnitySetUp]
    public IEnumerator SetUpRuntimeSystems()
    {
        Type govType = TypeOf("SistemaGovernoMundial");
        govType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        Type timeType = TypeOf("GerenciadorTempo");
        timeType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        yield return null;

        governo = GetStatic(govType, "Instancia");
        mercado = GetStatic(TypeOf("SistemaMercadoGlobal"), "Instancia");
        logistica = GetStatic(TypeOf("SistemaLogisticaMercado"), "Instancia");
        tempo = GetStatic(timeType, "Instancia");
        Assert.That(governo, Is.Not.Null);
        Assert.That(mercado, Is.Not.Null);
        Assert.That(logistica, Is.Not.Null);
        Assert.That(tempo, Is.Not.Null);

        comprador = Call(governo, "ObterPais", BuyerTeam);
        vendedor = Call(governo, "ObterPais", SellerTeam);
        relacao = Call(governo, "ObterRelacao", BuyerTeam, SellerTeam);
        itemSementes = Call(mercado, "ObterItem", "sementes");
        itemEnergia = Call(mercado, "ObterItem", "energia");
        Assert.That(comprador, Is.Not.Null);
        Assert.That(vendedor, Is.Not.Null);
        Assert.That(relacao, Is.Not.Null);
        Assert.That(itemSementes, Is.Not.Null);
        Assert.That(itemEnergia, Is.Not.Null);

        oldDay = (int)Read(tempo, "totalDias");
        buyerBalance = (long)Read(comprador, "saldo");
        sellerBalance = (long)Read(vendedor, "saldo");
        relationValue = (int)Read(relacao, "valor");
        relationTrade = (bool)Read(relacao, "tratadoComercial");
        relationWar = (bool)Read(relacao, "guerraDeclarada");
        relationSanction = (bool)Read(relacao, "sancaoAtiva");
        historyCount = ((IList)Read(mercado, "historico")).Count;
        orderCount = ((IList)Read(logistica, "pedidos")).Count;
        researchList = (IList)Read(comprador, "pesquisas");
        researchCount = researchList.Count;

        for (int i = 0; i < StockIds.Length; i++)
        {
            for (int team = 1; team <= 5; team++)
            {
                string key = team + ":" + StockIds[i];
                stockSnapshot[key] = (int)Call(governo, "ObterEstoque", team, StockIds[i]);
            }
        }

        IList items = (IList)Read(mercado, "itens");
        for (int i = 0; i < items.Count; i++)
        {
            object marketItem = items[i];
            if (marketItem == null) continue;
            itemSnapshot.Add(new[] { marketItem, Read(marketItem, "estoqueGlobal"), Read(marketItem, "oferta"), Read(marketItem, "demanda") });
        }

        resourcesManager = GetStatic(TypeOf("GerenciadorRecursos"), "Instancia") as Component;
        if (resourcesManager != null)
        {
            cashOnResourcesManager = Read(resourcesManager, "dinheiro");
        }

        Set(relacao, "valor", 75);
        Set(relacao, "tratadoComercial", true);
        Set(relacao, "guerraDeclarada", false);
        Set(relacao, "sancaoAtiva", false);
    }

    [UnityTearDown]
    public IEnumerator RestoreRuntimeState()
    {
        if (governo != null)
        {
            foreach (KeyValuePair<string, int> pair in stockSnapshot)
            {
                string[] parts = pair.Key.Split(':');
                int team = int.Parse(parts[0]);
                int current = (int)Call(governo, "ObterEstoque", team, parts[1]);
                int delta = pair.Value - current;
                if (delta > 0) Call(governo, "AdicionarEstoque", team, parts[1], delta);
                else if (delta < 0) Call(governo, "RemoverEstoque", team, parts[1], -delta);
            }

            Set(comprador, "saldo", buyerBalance);
            Set(vendedor, "saldo", sellerBalance);
            if (relacao != null)
            {
                Set(relacao, "valor", relationValue);
                Set(relacao, "tratadoComercial", relationTrade);
                Set(relacao, "guerraDeclarada", relationWar);
                Set(relacao, "sancaoAtiva", relationSanction);
            }
        }

        if (mercado != null)
        {
            IList history = (IList)Read(mercado, "historico");
            while (history.Count > historyCount) history.RemoveAt(history.Count - 1);
            for (int i = 0; i < itemSnapshot.Count; i++)
            {
                object[] snapshot = itemSnapshot[i];
                Set(snapshot[0], "estoqueGlobal", snapshot[1]);
                Set(snapshot[0], "oferta", snapshot[2]);
                Set(snapshot[0], "demanda", snapshot[3]);
            }
        }

        if (logistica != null)
        {
            IList orders = (IList)Read(logistica, "pedidos");
            while (orders.Count > orderCount) orders.RemoveAt(orders.Count - 1);
        }

        if (tempo != null) Set(tempo, "totalDias", oldDay);
        if (resourcesManager != null) Set(resourcesManager, "dinheiro", cashOnResourcesManager);
        if (researchList != null)
            while (researchList.Count > researchCount) researchList.RemoveAt(researchList.Count - 1);
        for (int i = 0; i < objects.Count; i++)
            if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
        objects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator SeedPurchaseShipsArrivesAndIsConsumedWhileAutomationAndResearchComplete()
    {
        const int quantity = 20;
        AddStock(SellerTeam, "sementes", 100);
        AddStock(BuyerTeam, "agua", 500);
        AddStock(BuyerTeam, "fertilizante_organico", 100);
        AddStock(BuyerTeam, "agrotoxicos", 100);

        GameObject sellerPierObject = new GameObject("Integration_SellerPier");
        sellerPierObject.transform.position = new Vector3(0f, 0f, 0f);
        objects.Add(sellerPierObject);
        Component sellerPier = sellerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(sellerPier, "OwnerTeamId", SellerTeam);

        GameObject buyerPierObject = new GameObject("Integration_BuyerPier");
        buyerPierObject.transform.position = new Vector3(60f, 0f, 0f);
        objects.Add(buyerPierObject);
        Component buyerPier = buyerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(buyerPier, "OwnerTeamId", BuyerTeam);

        Set(logistica, "intervaloProcessamento", 0.25f);
        Set(logistica, "tempoEmbarque", 0.1f);
        Set(logistica, "tempoDesembarque", 0.1f);
        GameObject cargoObject = new GameObject("Integration_MarketCargo");
        cargoObject.transform.position = sellerPierObject.transform.position;
        objects.Add(cargoObject);
        Component cargo = cargoObject.AddComponent(TypeOf("NavioCargaMercado"));
        Set(cargo, "velocidadeCruzeiro", 500f);
        Call(cargo, "Inicializar", BuyerTeam, false);

        bool bought = (bool)CallWithOut(mercado, "Comprar", BuyerTeam, SellerTeam, "sementes", quantity, out string buyMessage);
        Assert.That(bought, Is.True, buyMessage);
        IList orders = (IList)Read(logistica, "pedidos");
        Assert.That(orders.Count, Is.GreaterThan(orderCount));
        object order = orders[orders.Count - 1];
        Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO EMBARQUE"));

        float deliveryDeadline = Time.realtimeSinceStartup + 15f;
        while (!string.Equals(Read(order, "status").ToString(), "ENTREGUE", StringComparison.OrdinalIgnoreCase)
            && Time.realtimeSinceStartup < deliveryDeadline)
            yield return null;

        Assert.That(Read(order, "status").ToString(), Is.EqualTo("ENTREGUE"),
            "A compra deve percorrer embarque, deslocamento do navio e desembarque no pier comprador.");
        Assert.That(Vector3.Distance(cargoObject.transform.position, buyerPierObject.transform.position), Is.LessThan(8f));
        int seedsAfterArrival = (int)Call(governo, "ObterEstoque", BuyerTeam, "sementes");
        Assert.That(seedsAfterArrival, Is.GreaterThan(stockSnapshot[BuyerTeam + ":sementes"]));

        int energyBeforeSale = (int)Call(governo, "ObterEstoque", SellerTeam, "energia");
        AddStock(SellerTeam, "energia", 100);
        bool sold = (bool)CallWithOut(mercado, "VenderAutomaticamente", SellerTeam, "energia", 1, out string saleMessage);
        Assert.That(sold, Is.True, saleMessage);
        Assert.That((int)Call(governo, "ObterEstoque", SellerTeam, "energia"), Is.LessThan(energyBeforeSale + 100),
            "A venda automática da nação IA deve debitar o estoque vendido.");
        Assert.That(Read(itemEnergia, "estoqueGlobal"), Is.GreaterThan(0));

        IList researches = (IList)Read(comprador, "pesquisas");
        Type researchType = TypeOf("PesquisaNacionalEstado");
        object research = Activator.CreateInstance(researchType);
        Set(research, "id", "integration_research_complete");
        Set(research, "nome", "Pesquisa de integração");
        Set(research, "duracaoDias", 1);
        Set(research, "diaInicio", oldDay);
        Set(research, "emAndamento", true);
        researches.Add(research);
        Set(tempo, "totalDias", oldDay + 2);
        governo.GetType().GetMethod("AtualizarSistemasNacionais", Members).Invoke(governo, new[] { comprador });
        Assert.That(Read(research, "concluida"), Is.True,
            "O ciclo nacional deve concluir uma pesquisa após cumprir sua duração.");

        Set(tempo, "totalDias", oldDay);
        int seedsBeforeConsumption = (int)Call(governo, "ObterEstoque", BuyerTeam, "sementes");
        Call(tempo, "RestaurarDias", oldDay + 1);
        int seedsAfterConsumption = (int)Call(governo, "ObterEstoque", BuyerTeam, "sementes");
        Assert.That(seedsAfterConsumption, Is.LessThan(seedsBeforeConsumption),
            "A passagem do dia deve consumir sementes que chegaram pela compra internacional.");
        Assert.That((int)Read(comprador, "producaoAgricolaDiaria"), Is.GreaterThan(0));
    }

    private void AddStock(int teamId, string id, int quantity) => Call(governo, "AdicionarEstoque", teamId, id, quantity);

    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name, false)).First(type => type != null);

    private static object GetStatic(Type type, string property) => type.GetProperty(property, Members).GetValue(null, null);
    private static object Read(object target, string field) => target.GetType().GetField(field, Members).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Members).SetValue(target, value);
    private static void SetProperty(object target, string property, object value) => target.GetType().GetProperty(property, Members).SetValue(target, value, null);
    private static object Call(object target, string method, params object[] args)
    {
        MethodInfo match = target.GetType().GetMethods(Members)
            .First(candidate => candidate.Name == method
                && candidate.GetParameters().Length == args.Length
                && candidate.GetParameters().Select((parameter, index) =>
                    args[index] == null
                        ? !parameter.ParameterType.IsValueType
                        : parameter.ParameterType.IsInstanceOfType(args[index])).All(compatible => compatible));
        return match.Invoke(target, args);
    }

    private static object CallWithOut(object target, string method, int a, int b, string c, int d, out string message)
    {
        object[] args = { a, b, c, d, null };
        object result = target.GetType().GetMethod(method, Members).Invoke(target, args);
        message = (string)args[4];
        return result;
    }

    private static object CallWithOut(object target, string method, int a, string b, int c, out string message)
    {
        object[] args = { a, b, c, null };
        object result = target.GetType().GetMethod(method, Members).Invoke(target, args);
        message = (string)args[3];
        return result;
    }
}
