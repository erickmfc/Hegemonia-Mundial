using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private object itemEquipamentoAdicionado;
    private int relationValue;
    private bool relationTrade;
    private bool relationWar;
    private bool relationSanction;
    private int oldDay;
    private long buyerBalance;
    private long sellerBalance;
    private int buyerFoodStock;
    private int sellerFoodStock;
    private int buyerAgriculturalStock;
    private int sellerAgriculturalStock;
    private int buyerAgriculturalCapacity;
    private int sellerAgriculturalCapacity;
    private float buyerFoodBudgetPercent;
    private bool buyerDailyFoodPurchaseEnabled;
    private int buyerLastFoodPurchaseDay;
    private float[] buyerFoodMixSnapshot;
    private object playerFoodWarehouse;
    private object playerFoodWarehouseStock;
    private object playerFoodWarehouseCapacity;
    private int historyCount;
    private int orderCount;
    private int pendingDeliveryCount;
    private int researchCount;
    private IList researchList;
    private object cashOnResourcesManager;
    private Component resourcesManager;
    private object originalConstructionCatalog;
    private bool constructionCatalogOverridden;
    private readonly List<UnityEngine.Object> temporaryAssets = new List<UnityEngine.Object>();
    private object temporaryBuyerCountry;
    private object temporarySellerCountry;
    private object temporaryTradeRelation;
    private bool originalGovernmentEnabled;
    private Action dateChangedListener;
    private int dateChangedEventsDuringLoad;
    private float originalLogisticsInterval;
    private float originalLoadingTime;
    private float originalUnloadingTime;
    private object saveSystem;
    private object originalSaveSystemInstance;
    private object originalSaveData;
    private string originalSavePath;
    private string originalSelectedSaveId;
    private bool originalLoadedFromSave;
    private bool originalRestorePending;
    private bool saveSystemCreated;
    private string temporarySavePath;

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
        originalGovernmentEnabled = ((Behaviour)governo).enabled;

        originalLogisticsInterval = Convert.ToSingle(Read(logistica, "intervaloProcessamento"));
        originalLoadingTime = Convert.ToSingle(Read(logistica, "tempoEmbarque"));
        originalUnloadingTime = Convert.ToSingle(Read(logistica, "tempoDesembarque"));

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
        buyerFoodStock = (int)Read(comprador, "comida");
        sellerFoodStock = (int)Read(vendedor, "comida");
        buyerAgriculturalStock = (int)Read(comprador, "estoqueAgricolaTotal");
        sellerAgriculturalStock = (int)Read(vendedor, "estoqueAgricolaTotal");
        buyerAgriculturalCapacity = (int)Read(comprador, "capacidadeArmazenamentoAgricola");
        sellerAgriculturalCapacity = (int)Read(vendedor, "capacidadeArmazenamentoAgricola");
        buyerFoodBudgetPercent = Convert.ToSingle(Read(comprador, "compraAlimentarPercentualCaixaDiario"));
        buyerDailyFoodPurchaseEnabled = Convert.ToBoolean(Read(comprador, "compraAlimentarDiariaAtiva"));
        buyerLastFoodPurchaseDay = Convert.ToInt32(Read(comprador, "ultimoDiaCompraAlimentarProcessado"));
        float[] currentFoodMix = Read(comprador, "participacaoCompraAlimentar") as float[];
        buyerFoodMixSnapshot = currentFoodMix != null ? (float[])currentFoodMix.Clone() : Array.Empty<float>();
        relationValue = (int)Read(relacao, "valor");
        relationTrade = (bool)Read(relacao, "tratadoComercial");
        relationWar = (bool)Read(relacao, "guerraDeclarada");
        relationSanction = (bool)Read(relacao, "sancaoAtiva");
        historyCount = ((IList)Read(mercado, "historico")).Count;
        orderCount = ((IList)Read(logistica, "pedidos")).Count;
        pendingDeliveryCount = ((IList)Read(logistica, "entregasPendentes")).Count;
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

        IList countriesForStockSnapshot = Read(governo, "paises") as IList;
        if (countriesForStockSnapshot != null)
        {
            foreach (object country in countriesForStockSnapshot)
            {
                int teamId = (int)Read(country, "teamId");
                for (int i = 0; i < StockIds.Length; i++)
                {
                    string key = teamId + ":" + StockIds[i];
                    if (!stockSnapshot.ContainsKey(key))
                        stockSnapshot[key] = (int)Call(governo, "ObterEstoque", teamId, StockIds[i]);
                }
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
        Component warehouseManager = GetStatic(TypeOf("GerenciadorArmazens"), "Instancia") as Component;
        playerFoodWarehouse = warehouseManager != null ? Read(warehouseManager, "armazemRecursos") : null;
        if (playerFoodWarehouse != null)
        {
            playerFoodWarehouseStock = Read(playerFoodWarehouse, "alimentos");
            playerFoodWarehouseCapacity = Read(playerFoodWarehouse, "alimentosMaximo");
        }

        Set(relacao, "valor", 75);
        Set(relacao, "tratadoComercial", true);
        Set(relacao, "guerraDeclarada", false);
        Set(relacao, "sancaoAtiva", false);
    }

    [UnityTearDown]
    public IEnumerator RestoreRuntimeState()
    {
        if (dateChangedListener != null && tempo != null)
        {
            EventInfo dateChangedEvent = tempo.GetType().GetEvent("OnDataAlterada", Members);
            dateChangedEvent?.RemoveEventHandler(tempo, dateChangedListener);
            dateChangedListener = null;
        }

        if (governo != null)
        {
            comprador = Call(governo, "ObterPais", BuyerTeam);
            vendedor = Call(governo, "ObterPais", SellerTeam);
            relacao = Call(governo, "ObterRelacao", BuyerTeam, SellerTeam);
            researchList = comprador != null ? Read(comprador, "pesquisas") as IList : null;
        }

        if (saveSystem != null)
        {
            Type saveType = TypeOf("SistemaSaveGame");
            FieldInfo instanceField = saveType.GetField("Instancia", BindingFlags.Public | BindingFlags.Static);
            if (saveSystemCreated)
            {
                Component saveComponent = saveSystem as Component;
                if (saveComponent != null)
                {
                    GameObject saveObject = saveComponent.gameObject;
                    UnityEngine.Object.Destroy(saveObject);
                    yield return null;
                }
                instanceField.SetValue(null, originalSaveSystemInstance);
            }
            else
            {
                Set(saveSystem, "dadosAtuais", originalSaveData);
                Set(saveSystem, "caminhoDoArquivo", originalSavePath);
                Set(saveSystem, "saveSelecionadoId", originalSelectedSaveId);
                Set(saveSystem, "carregouDeSave", originalLoadedFromSave);
                Set(saveSystem, "restauracaoPendente", originalRestorePending);
            }

            if (!string.IsNullOrEmpty(temporarySavePath) && File.Exists(temporarySavePath))
                File.Delete(temporarySavePath);
            saveSystem = null;
            temporarySavePath = null;
            saveSystemCreated = false;
        }

        if (constructionCatalogOverridden)
        {
            Type menuType = TypeOf("MenuConstrucao");
            menuType.GetField("catalogoGlobal", Members).SetValue(null, originalConstructionCatalog);
            constructionCatalogOverridden = false;
            originalConstructionCatalog = null;
        }

        if (logistica != null)
        {
            Set(logistica, "intervaloProcessamento", originalLogisticsInterval);
            Set(logistica, "tempoEmbarque", originalLoadingTime);
            Set(logistica, "tempoDesembarque", originalUnloadingTime);
        }

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

            Set(comprador, "comida", buyerFoodStock);
            Set(vendedor, "comida", sellerFoodStock);
            Set(comprador, "estoqueAgricolaTotal", buyerAgriculturalStock);
            Set(vendedor, "estoqueAgricolaTotal", sellerAgriculturalStock);
            Set(comprador, "capacidadeArmazenamentoAgricola", buyerAgriculturalCapacity);
            Set(vendedor, "capacidadeArmazenamentoAgricola", sellerAgriculturalCapacity);
            Set(comprador, "compraAlimentarPercentualCaixaDiario", buyerFoodBudgetPercent);
            Set(comprador, "compraAlimentarDiariaAtiva", buyerDailyFoodPurchaseEnabled);
            Set(comprador, "ultimoDiaCompraAlimentarProcessado", buyerLastFoodPurchaseDay);
            float[] foodMixToRestore = Read(comprador, "participacaoCompraAlimentar") as float[];
            if (foodMixToRestore != null && buyerFoodMixSnapshot != null)
            {
                if (foodMixToRestore.Length != buyerFoodMixSnapshot.Length)
                    foodMixToRestore = (float[])buyerFoodMixSnapshot.Clone();
                else
                    Array.Copy(buyerFoodMixSnapshot, foodMixToRestore, buyerFoodMixSnapshot.Length);
                Set(comprador, "participacaoCompraAlimentar", foodMixToRestore);
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

            if (itemEquipamentoAdicionado != null)
                ((IList)Read(mercado, "itens")).Remove(itemEquipamentoAdicionado);
            itemEquipamentoAdicionado = null;
        }

        if (logistica != null)
        {
            IList orders = (IList)Read(logistica, "pedidos");
            while (orders.Count > orderCount) orders.RemoveAt(orders.Count - 1);

            IList pending = (IList)Read(logistica, "entregasPendentes");
            while (pending.Count > pendingDeliveryCount) pending.RemoveAt(pending.Count - 1);
        }

        if (tempo != null) Set(tempo, "totalDias", oldDay);
        if (resourcesManager != null) Set(resourcesManager, "dinheiro", cashOnResourcesManager);
        if (playerFoodWarehouse != null)
        {
            Set(playerFoodWarehouse, "alimentos", playerFoodWarehouseStock);
            Set(playerFoodWarehouse, "alimentosMaximo", playerFoodWarehouseCapacity);
        }
        if (researchList != null)
            while (researchList.Count > researchCount) researchList.RemoveAt(researchList.Count - 1);
        for (int i = 0; i < objects.Count; i++)
            if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
        objects.Clear();
        for (int i = 0; i < temporaryAssets.Count; i++)
            if (temporaryAssets[i] != null) UnityEngine.Object.Destroy(temporaryAssets[i]);
        temporaryAssets.Clear();
        yield return null;

        if (governo != null)
        {
            IList countries = (IList)Read(governo, "paises");
            int temporaryBuyerTeamId = temporaryBuyerCountry != null ? (int)Read(temporaryBuyerCountry, "teamId") : int.MinValue;
            int temporarySellerTeamId = temporarySellerCountry != null ? (int)Read(temporarySellerCountry, "teamId") : int.MinValue;
            for (int i = countries.Count - 1; i >= 0; i--)
            {
                object country = countries[i];
                int teamId = (int)Read(country, "teamId");
                if (teamId == temporaryBuyerTeamId || teamId == temporarySellerTeamId)
                    countries.RemoveAt(i);
            }
            IList relations = (IList)Read(governo, "relacoes");
            for (int i = relations.Count - 1; i >= 0; i--)
            {
                object relation = relations[i];
                int teamA = (int)Read(relation, "teamA");
                int teamB = (int)Read(relation, "teamB");
                if ((teamA == temporaryBuyerTeamId && teamB == temporarySellerTeamId)
                    || (teamA == temporarySellerTeamId && teamB == temporaryBuyerTeamId))
                    relations.RemoveAt(i);
            }
        }
        temporaryBuyerCountry = null;
        temporarySellerCountry = null;
        temporaryTradeRelation = null;
        if (governo is Behaviour governmentBehaviour)
            governmentBehaviour.enabled = originalGovernmentEnabled;
    }

    [UnityTest]
    public IEnumerator DailyCashFoodBasketDebitsOnceAndUsesImmediateSupplyWithoutAQualifiedCountrySupplier()
    {
        Component agriculture = ((Component)governo).GetComponent(TypeOf("AgriculturaNacional"));
        Assert.That(agriculture, Is.Not.Null);
        Assert.That(playerFoodWarehouse, Is.Not.Null, "O armazenamento alimentar real do Team 1 precisa estar inicializado.");

        foreach (KeyValuePair<string, int> stock in stockSnapshot)
        {
            string[] parts = stock.Key.Split(':');
            if (!parts[1].StartsWith("comida_", StringComparison.OrdinalIgnoreCase)) continue;
            int teamId = int.Parse(parts[0]);
            int atual = (int)Call(governo, "ObterEstoque", teamId, parts[1]);
            if (atual > 0) Call(governo, "RemoverEstoque", teamId, parts[1], atual);
        }

        Call(governo, "RemoverEstoque", BuyerTeam, Enum.Parse(TypeOf("RecursoMercado"), "Comida"),
            Math.Max(0, (int)Call(governo, "ObterEstoque", BuyerTeam, Enum.Parse(TypeOf("RecursoMercado"), "Comida"))));
        Set(comprador, "comida", 0);
        Set(comprador, "estoqueAgricolaTotal", 0);
        Set(comprador, "capacidadeArmazenamentoAgricola", 100000);
        Set(playerFoodWarehouse, "alimentos", 0);
        Set(playerFoodWarehouse, "alimentosMaximo", 100000);
        Set(comprador, "saldo", 100000000L);
        if (resourcesManager != null) Set(resourcesManager, "dinheiro", 100000000L);

        int diaAtual = Math.Max(1, oldDay);
        Set(tempo, "totalDias", diaAtual);
        bool mixConfigurado = (bool)Call(agriculture, "DefinirParticipacaoCompraAlimentar", BuyerTeam, 0, 100);
        Assert.That(mixConfigurado, Is.True);
        bool percentualConfigurado = (bool)Call(agriculture, "DefinirPercentualCompraAlimentar", BuyerTeam, 1f);
        Assert.That(percentualConfigurado, Is.True);
        Assert.That(AgriculturaNacional.CalcularOrcamentoCompraAlimentar(100000000L, 1f), Is.EqualTo(1000000L));

        long saldoInicial = Convert.ToInt64(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam));
        int estoqueMilhoAntes = (int)Call(governo, "ObterEstoque", BuyerTeam, "comida_milho");
        int historicoAntes = ((IList)Read(mercado, "historico")).Count;
        int pedidosAntes = ((IList)Read(logistica, "pedidos")).Count;
        bool ativou = (bool)CallWithOut(agriculture, "DefinirCompraAlimentarDiaria", BuyerTeam, true, out string mensagemAtivacao);
        Assert.That(ativou, Is.True, mensagemAtivacao);

        long saldoAposCompraHoje = Convert.ToInt64(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam));
        long gastoHoje = saldoInicial - saldoAposCompraHoje;
        Assert.That(gastoHoje, Is.GreaterThan(0L), "A ativacao deve comprar imediatamente com o percentual do caixa.");
        Assert.That(gastoHoje, Is.LessThanOrEqualTo(1000000L), "Itens e frete nao podem ultrapassar 1% do caixa.");
        Assert.That(Call(governo, "ObterEstoque", BuyerTeam, "comida_milho"), Is.GreaterThan(estoqueMilhoAntes));
        IList historico = (IList)Read(mercado, "historico");
        Assert.That(historico.Count, Is.EqualTo(historicoAntes + 1));
        object transacao = historico[0];
        Assert.That(Read(transacao, "vendedorTeamId"), Is.EqualTo(0), "Sem estoque de nenhum pais, a cesta usa o Mercado automatico.");
        Assert.That(Read(transacao, "status").ToString(), Is.EqualTo("ENTREGUE"));
        Assert.That(((IList)Read(logistica, "pedidos")).Count, Is.EqualTo(pedidosAntes),
            "Abastecimento virtual deve ser instantaneo e nao pode deixar um pedido maritimo sem origem.");

        MethodInfo processarCompra = agriculture.GetType().GetMethod("ProcessarCompraAlimentarDiaria", Members);
        Assert.That(processarCompra, Is.Not.Null);
        processarCompra.Invoke(agriculture, new[] { comprador, (object)diaAtual });
        Assert.That(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam), Is.EqualTo(saldoAposCompraHoje),
            "O mesmo dia nao pode debitar a compra diaria duas vezes.");

        processarCompra.Invoke(agriculture, new[] { comprador, (object)(diaAtual + 1) });
        long saldoAposProximoDia = Convert.ToInt64(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam));
        Assert.That(saldoAposProximoDia, Is.LessThan(saldoAposCompraHoje),
            "Na proxima virada, a rotina precisa recalcular 1% sobre o saldo atualizado.");
        Assert.That(saldoAposCompraHoje - saldoAposProximoDia, Is.LessThanOrEqualTo(
            AgriculturaNacional.CalcularOrcamentoCompraAlimentar(saldoAposCompraHoje, 1f)));
    }

    [UnityTest]
    public IEnumerator DailyCashFoodBasketUsesAnAlliedSupplierAndArrivesByCargoShip()
    {
        Component agriculture = ((Component)governo).GetComponent(TypeOf("AgriculturaNacional"));
        Assert.That(agriculture, Is.Not.Null);

        IList countries = Read(governo, "paises") as IList;
        Assert.That(countries, Is.Not.Null);
        foreach (object country in countries)
        {
            int teamId = (int)Read(country, "teamId");
            foreach (string foodId in StockIds.Where(value => value.StartsWith("comida_", StringComparison.OrdinalIgnoreCase)))
            {
                int stock = (int)Call(governo, "ObterEstoque", teamId, foodId);
                if (stock > 0) Call(governo, "RemoverEstoque", teamId, foodId, stock);
            }
            Call(governo, "ReconciliarEstoqueComidaCulturas", teamId);
        }

        Set(comprador, "saldo", 100000000L);
        Set(vendedor, "saldo", 100000000L);
        Set(comprador, "comida", 0);
        Set(comprador, "estoqueAgricolaTotal", 0);
        Set(comprador, "capacidadeArmazenamentoAgricola", 100000);
        if (resourcesManager != null) Set(resourcesManager, "dinheiro", 100000000L);
        if (playerFoodWarehouse != null)
        {
            Set(playerFoodWarehouse, "alimentos", 0);
            Set(playerFoodWarehouse, "alimentosMaximo", 100000);
        }

        const string foodId = "comida_milho";
        const int supplierStock = 5000;
        AddStock(SellerTeam, foodId, supplierStock);
        Call(governo, "ReconciliarEstoqueComidaCulturas", SellerTeam);
        object corn = Call(mercado, "ObterItem", foodId);
        Assert.That(corn, Is.Not.Null);
        Set(corn, "estoqueGlobal", supplierStock);

        Set(relacao, "valor", 75);
        Set(relacao, "tratadoComercial", true);
        Set(relacao, "guerraDeclarada", false);
        Set(relacao, "sancaoAtiva", false);

        GameObject sellerPierObject = new GameObject("FoodBasket_SellerPier");
        sellerPierObject.transform.position = Vector3.zero;
        objects.Add(sellerPierObject);
        Component sellerPier = sellerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(sellerPier, "OwnerTeamId", SellerTeam);

        GameObject buyerPierObject = new GameObject("FoodBasket_BuyerPier");
        buyerPierObject.transform.position = new Vector3(60f, 0f, 0f);
        objects.Add(buyerPierObject);
        Component buyerPier = buyerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(buyerPier, "OwnerTeamId", BuyerTeam);

        Set(logistica, "intervaloProcessamento", 0.25f);
        Set(logistica, "tempoEmbarque", 0.1f);
        Set(logistica, "tempoDesembarque", 0.1f);
        GameObject cargoObject = new GameObject("FoodBasket_MarketCargo");
        cargoObject.transform.position = sellerPierObject.transform.position;
        objects.Add(cargoObject);
        Component cargo = cargoObject.AddComponent(TypeOf("NavioCargaMercado"));
        Set(cargo, "velocidadeCruzeiro", 500f);
        Call(cargo, "Inicializar", BuyerTeam, false);

        bool mixConfigurado = (bool)Call(agriculture, "DefinirParticipacaoCompraAlimentar", BuyerTeam, 0, 100);
        Assert.That(mixConfigurado, Is.True);
        bool percentualConfigurado = (bool)Call(agriculture, "DefinirPercentualCompraAlimentar", BuyerTeam, 1f);
        Assert.That(percentualConfigurado, Is.True);

        // O sistema mantém um cache curto dos piers; aguarde sua renovação para
        // garantir que o teste valide a rota recém-configurada, não um cache velho.
        yield return new WaitForSecondsRealtime(1.1f);

        long saldoAntes = Convert.ToInt64(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam));
        int foodBefore = (int)Call(governo, "ObterEstoque", BuyerTeam, foodId);
        int ordersBefore = ((IList)Read(logistica, "pedidos")).Count;
        int historyBefore = ((IList)Read(mercado, "historico")).Count;
        MethodInfo executeBasket = agriculture.GetType().GetMethod("ExecutarCompraAlimentarPercentualCaixa", Members);
        Assert.That(executeBasket, Is.Not.Null);
        object[] arguments = { BuyerTeam, null, 0L };
        bool purchased = (bool)executeBasket.Invoke(agriculture, arguments);
        string purchaseMessage = (string)arguments[1];
        long reportedSpend = Convert.ToInt64(arguments[2]);

        Assert.That(purchased, Is.True, purchaseMessage);
        long balanceAfterPurchase = Convert.ToInt64(Call(governo, "ObterSaldoDisponivelParaOperacao", BuyerTeam));
        long actualSpend = saldoAntes - balanceAfterPurchase;
        Assert.That(actualSpend, Is.GreaterThan(0L));
        Assert.That(actualSpend, Is.LessThanOrEqualTo(1000000L), "A compra deve respeitar 1% do caixa de 100 milhões.");
        Assert.That(reportedSpend, Is.EqualTo(actualSpend), "O valor informado pela cesta deve refletir o débito real, incluindo frete.");
        Assert.That(Call(governo, "ObterEstoque", BuyerTeam, foodId), Is.EqualTo(foodBefore),
            "Com fornecedor e rota disponíveis, a comida só entra no estoque após a viagem.");

        IList orders = (IList)Read(logistica, "pedidos");
        Assert.That(orders.Count, Is.EqualTo(ordersBefore + 1));
        object order = orders[orders.Count - 1];
        Assert.That(Read(order, "vendedorTeamId"), Is.EqualTo(SellerTeam));
        Assert.That(Read(order, "compradorTeamId"), Is.EqualTo(BuyerTeam));
        Assert.That(Read(order, "itemId").ToString(), Is.EqualTo(foodId));
        Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO EMBARQUE"));

        IList history = (IList)Read(mercado, "historico");
        Assert.That(history.Count, Is.EqualTo(historyBefore + 1));
        object transaction = history[0];
        Assert.That(Read(transaction, "vendedorTeamId"), Is.EqualTo(SellerTeam));
        Assert.That(Read(transaction, "status").ToString(), Is.EqualTo("AGUARDANDO EMBARQUE"));

        float deliveryDeadline = Time.realtimeSinceStartup + 15f;
        while (!string.Equals(Read(order, "status").ToString(), "ENTREGUE", StringComparison.OrdinalIgnoreCase)
            && Time.realtimeSinceStartup < deliveryDeadline)
            yield return null;

        Assert.That(Read(order, "status").ToString(), Is.EqualTo("ENTREGUE"),
            "A compra de alimentos de um fornecedor real precisa completar embarque, viagem e desembarque.");
        Assert.That(Vector3.Distance(cargoObject.transform.position, buyerPierObject.transform.position), Is.LessThan(8f));
        Assert.That(Call(governo, "ObterEstoque", BuyerTeam, foodId), Is.EqualTo(foodBefore + (int)Read(order, "quantidade")));
    }

    [UnityTest]
    public IEnumerator ValdoriaFoodSurplusIsSoldToTeamOneThroughTheMarketWithoutADiplomaticProposal()
    {
        foreach (string id in StockIds.Where(value => value.StartsWith("comida_", StringComparison.OrdinalIgnoreCase)))
        {
            int atual = (int)Call(governo, "ObterEstoque", SellerTeam, id);
            if (atual > 0) Call(governo, "RemoverEstoque", SellerTeam, id, atual);
        }
        Call(governo, "AdicionarEstoque", SellerTeam, "comida_milho", 500);
        Call(governo, "ReconciliarEstoqueComidaCulturas", SellerTeam);
        Set(comprador, "saldo", 100000000L);
        if (resourcesManager != null) Set(resourcesManager, "dinheiro", 100000000L);
        Set(relacao, "valor", 75);
        Set(relacao, "tratadoComercial", true);
        Set(relacao, "guerraDeclarada", false);
        Set(relacao, "sancaoAtiva", false);

        Type bridgeType = TypeOf("Hegemonia.AI.DEUSA.IA_DeusaGovernoBridge");
        object bridge = Activator.CreateInstance(bridgeType);
        int propostasAntes = ((IEnumerable)Call(governo, "ObterPropostasPendentesPara", BuyerTeam)).Cast<object>().Count();
        int pedidosAntes = ((IList)Read(logistica, "pedidos")).Count;
        int transacoesAntes = ((IList)Read(mercado, "historico")).Count;
        bool vendeu = (bool)CallWithOut(bridge, "TentarVenderRecurso", SellerTeam,
            Enum.Parse(TypeOf("RecursoMercado"), "Comida"), 100, true, false, out string mensagem);

        Assert.That(vendeu, Is.True, mensagem);
        Assert.That(((IEnumerable)Call(governo, "ObterPropostasPendentesPara", BuyerTeam)).Cast<object>().Count(),
            Is.EqualTo(propostasAntes), "A venda alimentar automatica da Valdoria nao deve ficar esperando aceite diplomático.");
        Assert.That(((IList)Read(logistica, "pedidos")).Count, Is.EqualTo(pedidosAntes + 1),
            "A venda da IA03 deve entrar na logistica real de cargueiros.");
        Assert.That(((IList)Read(mercado, "historico")).Count, Is.EqualTo(transacoesAntes + 1));
        object transacao = ((IList)Read(mercado, "historico"))[0];
        Assert.That(Read(transacao, "vendedorTeamId"), Is.EqualTo(SellerTeam));
        Assert.That(Read(transacao, "compradorTeamId"), Is.EqualTo(BuyerTeam));
        Assert.That(Read(transacao, "itemId").ToString(), Does.StartWith("comida_"));
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

    [UnityTest]
    public IEnumerator C700MarketDeliveryWithoutParkingDoesNotSpawnOrMobilizeAircraft()
    {
        const int buyerTeamId = 876545;
        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador C-700 sem estacionamento");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 100);
        Set(temporaryBuyerCountry, "populacaoMaxima", 100);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 0);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        int clonesBefore = CountSceneObjectsNamed("Integration_C700_NoParking (Clone)");
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");

        GameObject prefab = new GameObject("Integration_C700_NoParking");
        prefab.SetActive(false);
        objects.Add(prefab);
        Component prefabIdentity = prefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        prefab.AddComponent(TypeOf("C700TransporteAereo"));

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", prefab.name);
        SetProperty(card, "PrefabDaUnidade", prefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_c700_without_parking");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "aeronave");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject airportObject = new GameObject("Integration_C700_BuyerAirport_NoParking");
        airportObject.SetActive(false);
        objects.Add(airportObject);
        airportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component airportIdentity = airportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(airportIdentity, "teamID", buyerTeamId);
        airportObject.SetActive(true);

        MethodInfo deliver = TypeOf("SistemaLogisticaMercado").GetMethod("EntregarEquipamento", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(deliver, Is.Not.Null);
        bool delivered = (bool)deliver.Invoke(logistica, new object[] { offer, buyerTeamId, 1 });
        Assert.That(delivered, Is.False, "Sem vaga para C-700, a entrega deve ser recusada antes do spawn.");
        Assert.That(CountSceneObjectsNamed(prefab.name + " (Clone)"), Is.EqualTo(clonesBefore),
            "A tentativa sem destino físico não pode criar um C-700 oculto.");
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(manpowerBefore),
            "A tentativa sem vaga não pode mobilizar tripulação.");

        long sellerBalanceBefore = (long)Read(vendedor, "saldo");
        object order = Activator.CreateInstance(TypeOf("PedidoMercadoLogistico"));
        Set(order, "id", "integration_c700_without_parking_order");
        Set(order, "compradorTeamId", buyerTeamId);
        Set(order, "vendedorTeamId", SellerTeam);
        Set(order, "itemId", (string)Read(offer, "id"));
        Set(order, "quantidade", 1);
        Set(order, "precoUnitario", 100);
        Set(order, "total", 100);
        Set(order, "status", "AGUARDANDO DESTINO");
        ((IList)Read(logistica, "pedidos")).Add(order);
        Type logisticsType = TypeOf("SistemaLogisticaMercado");
        logisticsType.GetMethod("FinalizarPedido", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(logistica, new object[] { order });
        Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO DESTINO"));
        Assert.That((long)Read(vendedor, "saldo"), Is.EqualTo(sellerBalanceBefore),
            "A venda não pode pagar o vendedor antes do estacionamento do C-700.");
        Assert.That(((IList)Read(logistica, "entregasPendentes")).Contains(order), Is.True,
            "O pedido precisa aguardar um ponto de estacionamento válido.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator AsyncRejectedC700DeliveryRollsBackManpowerAndDestroysTemporaryUnit()
    {
        const int buyerTeamId = 876546;
        const int sellerTeamId = 876547;

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador C-700 recusado na chegada");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        GameObject aircraftPrefab = new GameObject("Integration_AsyncReject_C700");
        objects.Add(aircraftPrefab);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        Component c700Prefab = aircraftPrefab.AddComponent(TypeOf("C700TransporteAereo"));
        if (c700Prefab is Behaviour c700Behaviour) c700Behaviour.enabled = false;

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", aircraftPrefab.name);
        SetProperty(card, "PrefabDaUnidade", aircraftPrefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_async_reject_c700");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "aeronave");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject sourceAirportObject = new GameObject("Integration_AsyncReject_SourceAirport");
        sourceAirportObject.SetActive(false);
        sourceAirportObject.transform.position = Vector3.zero;
        objects.Add(sourceAirportObject);
        sourceAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component sourceIdentity = sourceAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(sourceIdentity, "teamID", sellerTeamId);
        sourceAirportObject.SetActive(true);

        GameObject destinationAirportObject = new GameObject("Integration_AsyncReject_DestinationAirport");
        destinationAirportObject.SetActive(false);
        destinationAirportObject.transform.position = new Vector3(4f, 0f, 0f);
        objects.Add(destinationAirportObject);
        destinationAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component destinationIdentity = destinationAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(destinationIdentity, "teamID", buyerTeamId);
        destinationAirportObject.SetActive(true);

        string cloneName = aircraftPrefab.name + "(Clone)";
        int clonesBefore = CountSceneObjectsNamed(cloneName);
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        MethodInfo send = TypeOf("EntregaMercadoMilitar").GetMethod("Enviar", BindingFlags.Static | BindingFlags.Public);
        Assert.That(send, Is.Not.Null);
        object[] args = { offer, sellerTeamId, buyerTeamId, 1, null };
        bool started = (bool)send.Invoke(null, args);
        Assert.That(started, Is.True, args[4] as string);
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 1));
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.GreaterThan(manpowerBefore),
            "A unidade precisa reservar efetivo antes da viagem para que a recusa assíncrona possa revertê-lo.");

        float deadline = Time.realtimeSinceStartup + 5f;
        while (CountSceneObjectsNamed(cloneName) > clonesBefore
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore),
            "A recusa de estacionamento depois da viagem deve destruir a unidade temporária.");
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(manpowerBefore),
            "A recusa assíncrona deve devolver o efetivo sem registrar baixa.");
    }

    [UnityTest]
    public IEnumerator AsyncDeliveryWithDestroyedDestinationRollsBackManpowerAndTemporaryUnit()
    {
        const int buyerTeamId = 876552;
        const int sellerTeamId = 876553;

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador C-700 destino destruido");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        GameObject aircraftPrefab = new GameObject("Integration_DestroyedDestination_C700");
        aircraftPrefab.transform.position = new Vector3(1000f, 0f, 0f);
        objects.Add(aircraftPrefab);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        Component c700Prefab = aircraftPrefab.AddComponent(TypeOf("C700TransporteAereo"));
        if (c700Prefab is Behaviour c700Behaviour) c700Behaviour.enabled = false;

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", aircraftPrefab.name);
        SetProperty(card, "PrefabDaUnidade", aircraftPrefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_destroyed_destination_c700");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "aeronave");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject sourceAirportObject = new GameObject("Integration_DestroyedDestination_SourceAirport");
        sourceAirportObject.SetActive(false);
        sourceAirportObject.transform.position = Vector3.zero;
        objects.Add(sourceAirportObject);
        sourceAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component sourceIdentity = sourceAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(sourceIdentity, "teamID", sellerTeamId);
        sourceAirportObject.SetActive(true);

        GameObject destinationAirportObject = new GameObject("Integration_DestroyedDestination_Airport");
        destinationAirportObject.transform.position = new Vector3(40f, 0f, 0f);
        objects.Add(destinationAirportObject);
        destinationAirportObject.SetActive(false);
        destinationAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component destinationIdentity = destinationAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(destinationIdentity, "teamID", buyerTeamId);
        destinationAirportObject.SetActive(true);

        string cloneName = aircraftPrefab.name + "(Clone)";
        int clonesBefore = CountSceneObjectsNamed(cloneName);
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        MethodInfo send = TypeOf("EntregaMercadoMilitar").GetMethod("Enviar", BindingFlags.Static | BindingFlags.Public);
        Assert.That(send, Is.Not.Null);
        object[] args = { offer, sellerTeamId, buyerTeamId, 1, null };
        bool started = (bool)send.Invoke(null, args);
        Assert.That(started, Is.True, args[4] as string);
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 1));
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.GreaterThan(manpowerBefore));

        UnityEngine.Object.Destroy(destinationAirportObject);
        yield return null;

        float deadline = Time.realtimeSinceStartup + 6f;
        while (CountSceneObjectsNamed(cloneName) > clonesBefore && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore),
            "Sem destino durante a viagem, a unidade temporária deve ser destruída.");
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(manpowerBefore),
            "Sem destino durante a viagem, o manpower deve ser devolvido sem baixa.");
    }

    [UnityTest]
    public IEnumerator AsyncNavalDeliveryWithDestroyedDestinationRollsBackManpowerAndTemporaryUnit()
    {
        const int buyerTeamId = 876556;
        const int sellerTeamId = 876557;

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador naval destino destruido");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        GameObject navalPrefab = new GameObject("Integration_DestroyedDestination_Naval");
        navalPrefab.transform.position = new Vector3(1000f, 0f, 0f);
        objects.Add(navalPrefab);
        Component prefabIdentity = navalPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Naval"));
        Set(prefabIdentity, "militaresConsumidos", 4);

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", navalPrefab.name);
        SetProperty(card, "PrefabDaUnidade", navalPrefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_destroyed_destination_naval");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "navio");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject sourceShipyardObject = new GameObject("Integration_DestroyedDestination_SourceShipyard");
        sourceShipyardObject.transform.position = Vector3.zero;
        objects.Add(sourceShipyardObject);
        Component sourceShipyard = sourceShipyardObject.AddComponent(TypeOf("Estaleiro"));
        SetProperty(sourceShipyard, "OwnerTeamId", sellerTeamId);

        GameObject destinationShipyardObject = new GameObject("Integration_DestroyedDestination_Shipyard");
        destinationShipyardObject.transform.position = new Vector3(40f, 0f, 0f);
        objects.Add(destinationShipyardObject);
        Component destinationShipyard = destinationShipyardObject.AddComponent(TypeOf("Estaleiro"));
        SetProperty(destinationShipyard, "OwnerTeamId", buyerTeamId);

        string cloneName = navalPrefab.name + "(Clone)";
        int clonesBefore = CountSceneObjectsNamed(cloneName);
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        MethodInfo send = TypeOf("EntregaMercadoMilitar").GetMethod("Enviar", BindingFlags.Static | BindingFlags.Public);
        Assert.That(send, Is.Not.Null);
        object[] args = { offer, sellerTeamId, buyerTeamId, 1, null };
        bool started = (bool)send.Invoke(null, args);
        Assert.That(started, Is.True, args[4] as string);
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 1));
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.GreaterThan(manpowerBefore));

        UnityEngine.Object.Destroy(destinationShipyardObject);
        yield return null;

        float deadline = Time.realtimeSinceStartup + 6f;
        while (CountSceneObjectsNamed(cloneName) > clonesBefore && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore),
            "Sem estaleiro durante a viagem, o navio temporario deve ser destruido.");
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(manpowerBefore),
            "Sem estaleiro durante a viagem, o manpower naval deve ser devolvido sem baixa.");
    }

    [UnityTest]
    public IEnumerator AsyncNavalDeliveryRegistersAcceptedShipAtBuyerShipyard()
    {
        const int buyerTeamId = 876558;
        const int sellerTeamId = 876559;

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador naval entrega aceita");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        GameObject navalPrefab = new GameObject("Integration_AcceptedDestination_Naval");
        navalPrefab.transform.position = new Vector3(1000f, 0f, 0f);
        objects.Add(navalPrefab);
        Component prefabIdentity = navalPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Naval"));
        Set(prefabIdentity, "militaresConsumidos", 4);

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", navalPrefab.name);
        SetProperty(card, "PrefabDaUnidade", navalPrefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_accepted_destination_naval");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "navio");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject sourceShipyardObject = new GameObject("Integration_AcceptedDestination_SourceShipyard");
        sourceShipyardObject.transform.position = Vector3.zero;
        objects.Add(sourceShipyardObject);
        Component sourceShipyard = sourceShipyardObject.AddComponent(TypeOf("Estaleiro"));
        SetProperty(sourceShipyard, "OwnerTeamId", sellerTeamId);

        GameObject destinationShipyardObject = new GameObject("Integration_AcceptedDestination_Shipyard");
        destinationShipyardObject.transform.position = new Vector3(40f, 0f, 0f);
        objects.Add(destinationShipyardObject);
        Component destinationShipyard = destinationShipyardObject.AddComponent(TypeOf("Estaleiro"));
        SetProperty(destinationShipyard, "OwnerTeamId", buyerTeamId);

        string cloneName = navalPrefab.name + "(Clone)";
        int clonesBefore = CountSceneObjectsNamed(cloneName);
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        MethodInfo send = TypeOf("EntregaMercadoMilitar").GetMethod("Enviar", BindingFlags.Static | BindingFlags.Public);
        Assert.That(send, Is.Not.Null);
        object[] args = { offer, sellerTeamId, buyerTeamId, 1, null };
        bool started = (bool)send.Invoke(null, args);
        Assert.That(started, Is.True, args[4] as string);
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 1));
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.GreaterThan(manpowerBefore));

        GameObject delivered = null;
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline)
        {
            delivered = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(candidate => candidate != null
                    && candidate.scene.IsValid()
                    && candidate.name == cloneName
                    && candidate.GetComponent(TypeOf("EntregaMercadoMilitar")) == null);
            if (delivered != null) break;
            yield return null;
        }

        Assert.That(delivered, Is.Not.Null, "O navio deve concluir a viagem e remover somente o componente de entrega.");
        Component deliveredIdentity = delivered.GetComponent(TypeOf("IdentidadeUnidade"));
        Assert.That(deliveredIdentity, Is.Not.Null);
        Assert.That((int)Read(deliveredIdentity, "teamID"), Is.EqualTo(buyerTeamId));
        Assert.That(Read(deliveredIdentity, "tipoUnidade").ToString(), Is.EqualTo("Naval"));
        Assert.That((int)Read(deliveredIdentity, "militaresConsumidos"), Is.EqualTo(4));
        Assert.That(delivered.GetComponent(TypeOf("ControleUnidade")), Is.Not.Null,
            "A integração naval deve garantir o controlador operacional no destino.");
        Transform expectedExit = Read(destinationShipyard, "pontoDeSaida") as Transform;
        Assert.That(expectedExit, Is.Not.Null, "O estaleiro deve expor o ponto de saída usado pela integração naval.");
        Assert.That(Mathf.Abs(delivered.transform.position.x - expectedExit.position.x), Is.LessThan(0.1f));
        Assert.That(Mathf.Abs(delivered.transform.position.z - expectedExit.position.z), Is.LessThan(0.1f));
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.GreaterThan(manpowerBefore),
            "A entrega aceita mantém o efetivo mobilizado.");
    }

    [UnityTest]
    public IEnumerator AsyncBatchC700DeliveryKeepsOneAcceptedUnitAndRollsBackTheRejectedUnit()
    {
        const int buyerTeamId = 876548;
        const int sellerTeamId = 876549;

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador C-700 lote parcial");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);

        GameObject aircraftPrefab = new GameObject("Integration_AsyncBatch_C700");
        aircraftPrefab.transform.position = new Vector3(1000f, 0f, 0f);
        objects.Add(aircraftPrefab);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        Component c700Prefab = aircraftPrefab.AddComponent(TypeOf("C700TransporteAereo"));
        if (c700Prefab is Behaviour c700Behaviour) c700Behaviour.enabled = false;
        aircraftPrefab.AddComponent<BoxCollider>();

        ScriptableObject card = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(card);
        SetProperty(card, "NomeItem", aircraftPrefab.name);
        SetProperty(card, "PrefabDaUnidade", aircraftPrefab);
        string prefabId = (string)card.GetType().GetMethod("GetStableId", Members).Invoke(card, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(card);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object offer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(offer, "id", "integration_async_batch_c700");
        Set(offer, "prefabId", prefabId);
        Set(offer, "tipoEntrega", "aeronave");
        Set(offer, "equipamentoMilitar", true);
        ((IList)Read(mercado, "itens")).Add(offer);
        itemEquipamentoAdicionado = offer;

        GameObject sourceAirportObject = new GameObject("Integration_AsyncBatch_SourceAirport");
        sourceAirportObject.SetActive(false);
        sourceAirportObject.transform.position = Vector3.zero;
        objects.Add(sourceAirportObject);
        sourceAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component sourceIdentity = sourceAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(sourceIdentity, "teamID", sellerTeamId);
        sourceAirportObject.SetActive(true);

        GameObject destinationAirportObject = new GameObject("Integration_AsyncBatch_DestinationAirport");
        destinationAirportObject.SetActive(false);
        destinationAirportObject.transform.position = new Vector3(4f, 0f, 0f);
        objects.Add(destinationAirportObject);
        Component destinationAirport = destinationAirportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component destinationIdentity = destinationAirportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(destinationIdentity, "teamID", buyerTeamId);
        GameObject patioObject = new GameObject("Patio_Militar");
        patioObject.transform.SetParent(destinationAirportObject.transform, false);
        GameObject slotObject = new GameObject("Parada_grande");
        slotObject.transform.SetParent(patioObject.transform, false);
        slotObject.transform.localPosition = Vector3.zero;
        Set(destinationAirport, "patio", patioObject.transform);
        destinationAirportObject.SetActive(true);

        string cloneName = aircraftPrefab.name + "(Clone)";
        int clonesBefore = CountSceneObjectsNamed(cloneName);
        int manpowerBefore = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        MethodInfo send = TypeOf("EntregaMercadoMilitar").GetMethod("Enviar", BindingFlags.Static | BindingFlags.Public);
        Assert.That(send, Is.Not.Null);
        object[] args = { offer, sellerTeamId, buyerTeamId, 2, null };
        bool started = (bool)send.Invoke(null, args);
        Assert.That(started, Is.True, args[4] as string);
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 2));

        int manpowerAfterSend = (int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva");
        int manpowerPerUnit = (manpowerAfterSend - manpowerBefore) / 2;
        Assert.That(manpowerPerUnit, Is.GreaterThan(0), "O lote deve mobilizar efetivo para as duas unidades antes da chegada.");

        IList received = (IList)Read(destinationAirport, "transportesC700NoPatio");
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((received.Count < 1 || CountSceneObjectsNamed(cloneName) > clonesBefore + 1)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        for (int frame = 0; frame < 2; frame++) yield return null;
        Assert.That(received.Count, Is.EqualTo(1),
            "Apenas a primeira unidade deve ocupar o único ponto de estacionamento.");
        Assert.That(CountSceneObjectsNamed(cloneName), Is.EqualTo(clonesBefore + 1),
            "A unidade recusada não pode permanecer no mundo após a chegada.");
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(manpowerBefore + manpowerPerUnit),
            "O rollback do segundo item deve devolver somente o efetivo da unidade recusada.");
    }

    [UnityTest]
    public IEnumerator MarketDoesNotAssignAirportWithoutTeamIdentityToPlayer()
    {
        const int playerTeamWithoutIdentity = 987654;
        int previousPlayerTeam = (int)Read(governo, "teamJogador");
        Set(governo, "teamJogador", playerTeamWithoutIdentity);

        GameObject airportObject = new GameObject("Integration_Market_Airport_WithoutTeamIdentity");
        objects.Add(airportObject);
        airportObject.AddComponent(TypeOf("GerenciadorAeroporto"));

        try
        {
            MethodInfo findDestination = TypeOf("SistemaLogisticaMercado").GetMethod(
                "EncontrarDestinoComAeroporto", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(findDestination, Is.Not.Null);

            object[] args = { "aeronave", playerTeamWithoutIdentity, null };
            Transform destination = (Transform)findDestination.Invoke(logistica, args);

            Assert.That(destination, Is.Null,
                "A base sem IdentidadeUnidade não pode ser presumida como pertencente ao jogador.");
            Assert.That(args[2], Is.Null,
                "A logística não deve reter uma base sem dono comprovado.");
        }
        finally
        {
            Set(governo, "teamJogador", previousPlayerTeam);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator InternationalAircraftPurchaseRejectsInsufficientFundsThenShipsAndRegistersAtBuyerAirport()
    {
        const int buyerTeamId = 876543;
        const int sellerTeamId = 876544;
        const int aircraftPrice = 1500;
        const string itemId = "integration_market_aircraft_route";

        Type countryType = TypeOf("DadosPaisGoverno");
        temporaryBuyerCountry = Activator.CreateInstance(countryType);
        Set(temporaryBuyerCountry, "teamId", buyerTeamId);
        Set(temporaryBuyerCountry, "nomePais", "Comprador de teste da rota aerea");
        Set(temporaryBuyerCountry, "saldo", 10000L);
        Set(temporaryBuyerCountry, "populacao", 1000);
        Set(temporaryBuyerCountry, "populacaoMaxima", 1000);
        Set(temporaryBuyerCountry, "populacaoCivil", 100);
        Set(temporaryBuyerCountry, "populacaoMilitarAtiva", 0);
        Set(temporaryBuyerCountry, "reservistas", 0);
        Set(temporaryBuyerCountry, "alistaveis", 20);

        temporarySellerCountry = Activator.CreateInstance(countryType);
        Set(temporarySellerCountry, "teamId", sellerTeamId);
        Set(temporarySellerCountry, "nomePais", "Fornecedor de teste da rota aerea");
        Set(temporarySellerCountry, "saldo", 10000L);
        ((IList)Read(governo, "paises")).Add(temporaryBuyerCountry);
        ((IList)Read(governo, "paises")).Add(temporarySellerCountry);

        temporaryTradeRelation = Call(governo, "ObterRelacao", buyerTeamId, sellerTeamId);
        Set(temporaryTradeRelation, "valor", 75);
        Set(temporaryTradeRelation, "tratadoComercial", true);
        Set(temporaryTradeRelation, "guerraDeclarada", false);
        Set(temporaryTradeRelation, "sancaoAtiva", false);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Integration_Market_Aircraft";
        aircraftPrefab.SetActive(false);
        objects.Add(aircraftPrefab);
        Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(prefabIdentity, "teamID", buyerTeamId);
        Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
        Set(prefabIdentity, "militaresConsumidos", 0);
        aircraftPrefab.AddComponent(TypeOf("ControleAviao"));

        ScriptableObject aircraftCard = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        temporaryAssets.Add(aircraftCard);
        SetProperty(aircraftCard, "NomeItem", aircraftPrefab.name);
        SetProperty(aircraftCard, "PrefabDaUnidade", aircraftPrefab);
        string prefabId = (string)aircraftCard.GetType().GetMethod("GetStableId", Members).Invoke(aircraftCard, null);

        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
        originalConstructionCatalog = catalogField.GetValue(null);
        IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
        if (originalConstructionCatalog is IList oldCatalog)
        {
            foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
        }
        testCatalog.Add(aircraftCard);
        catalogField.SetValue(null, testCatalog);
        constructionCatalogOverridden = true;

        object aircraftOffer = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(aircraftOffer, "id", itemId);
        Set(aircraftOffer, "nome", "Aeronave de teste");
        Set(aircraftOffer, "categoria", "Equipamento militar");
        Set(aircraftOffer, "recurso", Enum.Parse(TypeOf("RecursoMercado"), "Armamentos"));
        Set(aircraftOffer, "recursoId", itemId);
        Set(aircraftOffer, "equipamentoMilitar", true);
        Set(aircraftOffer, "prefabId", prefabId);
        Set(aircraftOffer, "tipoEntrega", "aeronave");
        Set(aircraftOffer, "precoAtual", aircraftPrice);
        Set(aircraftOffer, "precoBase", aircraftPrice);
        Set(aircraftOffer, "estoqueGlobal", 1);
        ((IList)Read(mercado, "itens")).Add(aircraftOffer);
        itemEquipamentoAdicionado = aircraftOffer;

        GameObject sellerPierObject = new GameObject("Integration_Aircraft_SellerPier");
        sellerPierObject.transform.position = Vector3.zero;
        objects.Add(sellerPierObject);
        Component sellerPier = sellerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(sellerPier, "OwnerTeamId", sellerTeamId);

        GameObject buyerPierObject = new GameObject("Integration_Aircraft_BuyerPier");
        buyerPierObject.transform.position = new Vector3(80f, 0f, 0f);
        objects.Add(buyerPierObject);
        Component buyerPier = buyerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(buyerPier, "OwnerTeamId", buyerTeamId);

        Set(logistica, "intervaloProcessamento", 0.25f);
        Set(logistica, "tempoEmbarque", 0.1f);
        Set(logistica, "tempoDesembarque", 0.1f);
        GameObject cargoObject = new GameObject("Integration_Aircraft_MarketCargo");
        cargoObject.transform.position = sellerPierObject.transform.position;
        objects.Add(cargoObject);
        Component cargo = cargoObject.AddComponent(TypeOf("NavioCargaMercado"));
        Set(cargo, "velocidadeCruzeiro", 500f);
        Call(cargo, "Inicializar", buyerTeamId, false);

        IList orders = (IList)Read(logistica, "pedidos");
        IList history = (IList)Read(mercado, "historico");
        IList journeys = (IList)Read(logistica, "viagens");
        int ordersBeforeRejection = orders.Count;
        int historyBeforeRejection = history.Count;
        int journeysBeforeRejection = journeys.Count;
        int clonesBeforeRejection = CountSceneObjectsNamed(aircraftPrefab.name + " (Clone)");
        Set(temporaryBuyerCountry, "saldo", 100L);
        bool rejectedForFunds = (bool)CallWithOut(
            mercado,
            "Comprar",
            buyerTeamId,
            sellerTeamId,
            itemId,
            1,
            out string insufficientFundsMessage);
        Assert.That(rejectedForFunds, Is.False, "A compra deve ser recusada antes da fila quando falta dinheiro para produto e frete.");
        Assert.That(insufficientFundsMessage, Does.Contain("Dinheiro insuficiente"));
        Assert.That((long)Read(temporaryBuyerCountry, "saldo"), Is.EqualTo(100L), "A recusa não pode debitar o comprador.");
        Assert.That((int)Read(aircraftOffer, "estoqueGlobal"), Is.EqualTo(1), "A recusa não pode consumir estoque do mercado.");
        Assert.That(orders.Count, Is.EqualTo(ordersBeforeRejection), "A recusa não pode criar pedido logístico.");
        Assert.That(history.Count, Is.EqualTo(historyBeforeRejection), "A recusa não pode registrar transação concluída.");
        Assert.That(journeys.Count, Is.EqualTo(journeysBeforeRejection), "A recusa não pode iniciar viagem.");
        Assert.That(CountSceneObjectsNamed(aircraftPrefab.name + " (Clone)"), Is.EqualTo(clonesBeforeRejection),
            "A recusa por saldo não pode instanciar aeronave.");

        Set(temporaryBuyerCountry, "saldo", 10000L);
        long buyerBalanceBefore = (long)Read(temporaryBuyerCountry, "saldo");
        long sellerBalanceBefore = (long)Read(temporarySellerCountry, "saldo");
        bool purchased = (bool)CallWithOut(mercado, "Comprar", buyerTeamId, sellerTeamId, itemId, 1, out string purchaseMessage);
        Assert.That(purchased, Is.True, purchaseMessage);

        object order = orders[orders.Count - 1];
        Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO EMBARQUE"));
        Assert.That((long)Read(temporaryBuyerCountry, "saldo"), Is.EqualTo(buyerBalanceBefore - aircraftPrice - 75L),
            "O comprador deve pagar uma vez pelo equipamento e pelo frete de 5%.");
        Assert.That((long)Read(temporarySellerCountry, "saldo"), Is.EqualTo(sellerBalanceBefore),
            "O vendedor só recebe quando o pedido chega.");

        float pendingDeadline = Time.realtimeSinceStartup + 20f;
        PropertyInfo cargoTravel = cargo.GetType().GetProperty("EmViagem", Members);
        while ((!string.Equals(Read(order, "status").ToString(), "AGUARDANDO DESTINO", StringComparison.OrdinalIgnoreCase)
                || journeys.Count > 0
                || (bool)cargoTravel.GetValue(cargo, null))
            && Time.realtimeSinceStartup < pendingDeadline)
        {
            yield return null;
        }

        Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO DESTINO"),
            "Sem aeroporto do comprador, a carga deve ficar pendente no destino após a viagem marítima.");
        Assert.That(journeys.Count, Is.Zero, "O cargueiro deve concluir o retorno antes do save.");
        Assert.That((long)Read(temporaryBuyerCountry, "saldo"), Is.EqualTo(buyerBalanceBefore - aircraftPrice - 75L));
        Assert.That((long)Read(temporarySellerCountry, "saldo"), Is.EqualTo(sellerBalanceBefore),
            "O vendedor ainda não recebe enquanto o aeroporto não existe.");
        Assert.That(CountSceneObjectsNamed(aircraftPrefab.name + " (Clone)"), Is.EqualTo(0),
            "A entrega pendente não pode gerar aeronave antes de localizar o aeroporto.");

        long buyerBalanceAtSave = (long)Read(temporaryBuyerCountry, "saldo");
        Behaviour governmentBehaviour = (Behaviour)governo;
        governmentBehaviour.enabled = false;

        Component save = CriarSistemaSaveTemporario();
        temporarySavePath = Path.Combine(Application.temporaryCachePath,
            "market-pending-destination-" + Guid.NewGuid().ToString("N") + ".json");
        Set(save, "caminhoDoArquivo", temporarySavePath);
        Set(save, "dadosAtuais", Activator.CreateInstance(TypeOf("DadosDoJogo")));
        Call(save, "SalvarJogo");
        Assert.That(File.Exists(temporarySavePath), Is.True, "O save real deve gravar no arquivo temporário do teste.");

        object dataReadFromFile = JsonUtility.FromJson(File.ReadAllText(temporarySavePath), TypeOf("DadosDoJogo"));
        object governmentDataFromFile = Read(dataReadFromFile, "governoMundial");
        IList countriesFromFile = (IList)Read(governmentDataFromFile, "paises");
        object buyerFromFile = countriesFromFile.Cast<object>().FirstOrDefault(country =>
            (int)Read(country, "teamId") == buyerTeamId);
        Assert.That(buyerFromFile, Is.Not.Null);
        Assert.That((long)Read(buyerFromFile, "saldo"), Is.EqualTo(buyerBalanceAtSave),
            "O JSON deve persistir o saldo exato do comprador antes do load.");

        object saveData = Read(save, "dadosAtuais");
        object logisticsData = Read(saveData, "logisticaMercado");
        IList savedOrders = (IList)Read(logisticsData, "pedidos");
        object savedPendingOrder = savedOrders.Cast<object>().FirstOrDefault(saved =>
            string.Equals(Read(saved, "id").ToString(), Read(order, "id").ToString(), StringComparison.Ordinal));
        Assert.That(savedPendingOrder, Is.Not.Null);
        Assert.That(Read(savedPendingOrder, "status").ToString(), Is.EqualTo("AGUARDANDO DESTINO"));

        string orderId = Read(order, "id").ToString();
        Set(temporaryBuyerCountry, "saldo", 17L);
        Set(temporarySellerCountry, "saldo", 23L);
        EventInfo dateChangedEvent = tempo.GetType().GetEvent("OnDataAlterada", Members);
        Assert.That(dateChangedEvent, Is.Not.Null);
        dateChangedEventsDuringLoad = 0;
        dateChangedListener = () => dateChangedEventsDuringLoad++;
        dateChangedEvent.AddEventHandler(tempo, dateChangedListener);
        Call(save, "CarregarJogo");
        Assert.That((bool)Read(save, "restauracaoPendente"), Is.True,
            "O arquivo com pedido logístico precisa agendar a restauração adiada.");
        object loadedGovernmentData = Read(Read(save, "dadosAtuais"), "governoMundial");
        object loadedBuyer = ((IList)Read(loadedGovernmentData, "paises")).Cast<object>().FirstOrDefault(country =>
            (int)Read(country, "teamId") == buyerTeamId);
        Assert.That(loadedBuyer, Is.Not.Null);
        Assert.That((long)Read(loadedBuyer, "saldo"), Is.EqualTo(buyerBalanceAtSave),
            "CarregarJogo deve ler o saldo gravado antes da restauração de cena.");

        MethodInfo afterSceneLoad = save.GetType().GetMethod("AoCarregarCena", Members);
        Assert.That(afterSceneLoad, Is.Not.Null);
        afterSceneLoad.Invoke(save, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });

        object restoredOrder = null;
        float restoreDeadline = Time.realtimeSinceStartup + 10f;
        while (restoredOrder == null && Time.realtimeSinceStartup < restoreDeadline)
        {
            IList currentOrders = (IList)Read(logistica, "pedidos");
            restoredOrder = currentOrders.Cast<object>().FirstOrDefault(saved =>
                string.Equals(Read(saved, "id").ToString(), orderId, StringComparison.Ordinal));
            if (restoredOrder == null) yield return null;
        }

        Assert.That(restoredOrder, Is.Not.Null, "A coroutine da cena deve restaurar o pedido salvo.");
        for (int frame = 0; frame < 3; frame++) yield return null;
        Assert.That(Read(restoredOrder, "status").ToString(), Is.EqualTo("AGUARDANDO EMBARQUE"));
        Assert.That(Read(restoredOrder, "navioId").ToString(), Is.Empty);
        Assert.That(((IList)Read(logistica, "pedidos")).Cast<object>().Count(saved =>
            string.Equals(Read(saved, "id").ToString(), orderId, StringComparison.Ordinal)), Is.EqualTo(1));

        temporaryBuyerCountry = Call(governo, "ObterPais", buyerTeamId);
        temporarySellerCountry = Call(governo, "ObterPais", sellerTeamId);
        temporaryTradeRelation = Call(governo, "ObterRelacao", buyerTeamId, sellerTeamId);
        Assert.That(dateChangedEventsDuringLoad, Is.Zero,
            "Restaurar o save não pode emitir evento como se um novo dia tivesse passado.");
        Assert.That((long)Read(temporaryBuyerCountry, "saldo"), Is.EqualTo(buyerBalanceAtSave),
            "Recarregar o pedido não pode cobrar o comprador novamente.");
        Assert.That((long)Read(temporarySellerCountry, "saldo"), Is.EqualTo(sellerBalanceBefore),
            "O vendedor só recebe depois da entrega.");

        // O teste chama o callback de cena sem executar uma troca real de cena.
        // O restore limpa SaveableEntity da cena; recrie aqui os pontos estaticos
        // que seriam instanciados novamente pela cena carregada.
        GameObject restoredSellerPierObject = new GameObject("Integration_Aircraft_SellerPier_AfterRestore");
        restoredSellerPierObject.transform.position = Vector3.zero;
        objects.Add(restoredSellerPierObject);
        Component restoredSellerPier = restoredSellerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(restoredSellerPier, "OwnerTeamId", sellerTeamId);

        GameObject restoredBuyerPierObject = new GameObject("Integration_Aircraft_BuyerPier_AfterRestore");
        restoredBuyerPierObject.transform.position = new Vector3(80f, 0f, 0f);
        objects.Add(restoredBuyerPierObject);
        Component restoredBuyerPier = restoredBuyerPierObject.AddComponent(TypeOf("PierMarinha"));
        SetProperty(restoredBuyerPier, "OwnerTeamId", buyerTeamId);

        MethodInfo findPier = TypeOf("SistemaLogisticaMercado").GetMethod(
            "EncontrarPier", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(findPier, Is.Not.Null);
        Assert.That(findPier.Invoke(null, new object[] { sellerTeamId }), Is.Not.Null,
            "O pier de origem precisa estar disponível depois de simular o retorno da cena.");
        Assert.That(findPier.Invoke(null, new object[] { buyerTeamId }), Is.Not.Null,
            "O pier de destino precisa estar disponível depois de simular o retorno da cena.");

        MethodInfo findAvailableCargo = TypeOf("SistemaLogisticaMercado").GetMethod(
            "EncontrarNavioDisponivel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(findAvailableCargo, Is.Not.Null);
        Component cargoAfterRestore = findAvailableCargo.Invoke(logistica, new object[] { buyerTeamId }) as Component;
        if (cargoAfterRestore == null)
        {
            GameObject cargoAfterRestoreObject = new GameObject("Integration_Aircraft_MarketCargo_AfterRestore");
            cargoAfterRestoreObject.transform.position = restoredSellerPierObject.transform.position;
            objects.Add(cargoAfterRestoreObject);
            cargoAfterRestore = cargoAfterRestoreObject.AddComponent(TypeOf("NavioCargaMercado"));
            Set(cargoAfterRestore, "velocidadeCruzeiro", 500f);
            Call(cargoAfterRestore, "Inicializar", buyerTeamId, false);
            Call(logistica, "RegistrarNavio", cargoAfterRestore);
        }
        PropertyInfo cargoTravelAfterRestore = cargoAfterRestore.GetType().GetProperty("EmViagem", Members);

        GameObject airportObject = new GameObject("Integration_Market_Airport_AfterRestore");
        airportObject.SetActive(false);
        airportObject.transform.position = new Vector3(40f, 0f, 0f);
        objects.Add(airportObject);
        airportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
        Component airportIdentity = airportObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(airportIdentity, "teamID", buyerTeamId);
        airportObject.SetActive(true);
        Component airport = airportObject.GetComponent(TypeOf("GerenciadorAeroporto"));

        float deliveryDeadline = Time.realtimeSinceStartup + 20f;
        while ((!string.Equals(Read(restoredOrder, "status").ToString(), "ENTREGUE", StringComparison.OrdinalIgnoreCase)
                || journeys.Count > 0
                || (bool)cargoTravelAfterRestore.GetValue(cargoAfterRestore, null))
            && Time.realtimeSinceStartup < deliveryDeadline)
        {
            yield return null;
        }

        Assert.That(Read(restoredOrder, "status").ToString(), Is.EqualTo("ENTREGUE"),
            "Depois do restore e da criação do aeroporto, a carga deve refazer a rota e chegar.");
        Assert.That(journeys.Count, Is.Zero, "O cargueiro deve concluir o retorno antes do teardown do teste.");
        Assert.That((long)Read(temporaryBuyerCountry, "saldo"), Is.EqualTo(buyerBalanceAtSave));
        Assert.That((long)Read(temporarySellerCountry, "saldo"), Is.EqualTo(sellerBalanceBefore + aircraftPrice),
            "O vendedor deve receber o preço uma única vez, sem receber o frete.");
        Assert.That((int)Read(aircraftOffer, "estoqueGlobal"), Is.Zero);
        Assert.That((int)Read(temporaryBuyerCountry, "populacaoMilitarAtiva"), Is.EqualTo(2),
            "A chegada deve mobilizar o efetivo real da aeronave.");

        IList patio = (IList)Read(airport, "avioesNoPatio");
        IList hangar = (IList)Read(airport, "avioesNoHangar");
        Assert.That(patio.Count + hangar.Count, Is.EqualTo(1),
            "A chegada por frete deve registrar exatamente uma aeronave na base compradora.");
        Component deliveredAircraft = patio.Count > 0 ? (Component)patio[0] : (Component)hangar[0];
        objects.Add(deliveredAircraft.gameObject);
        Assert.That(Read(deliveredAircraft, "aeroportoOrigem"), Is.SameAs(airport));
        Assert.That(Read(deliveredAircraft, "baseEstacionamentoAtual"), Is.SameAs(airport),
            "A aeronave recebida precisa conhecer a base em que foi estacionada.");
        Assert.That(Read(deliveredAircraft.GetComponent(TypeOf("IdentidadeUnidade")), "teamID"), Is.EqualTo(buyerTeamId));

        Call(logistica, "FinalizarPedido", restoredOrder);
        Assert.That((long)Read(temporarySellerCountry, "saldo"), Is.EqualTo(sellerBalanceBefore + aircraftPrice),
            "Finalizar novamente um pedido terminal não pode pagar o vendedor duas vezes.");
        Assert.That(patio.Count + hangar.Count, Is.EqualTo(1), "Finalizar novamente não pode duplicar a aeronave.");
    }

    [UnityTest]
    public IEnumerator MilitaryEquipmentSaleTransfersExistingUnitWithoutDuplicatingIt()
    {
        const string equipmentId = "integration_test_tank_market";
        const int unitPrice = 1;
        object equipmentItem = Activator.CreateInstance(TypeOf("DadosItemMercado"));
        Set(equipmentItem, "id", equipmentId);
        Set(equipmentItem, "nome", "Tanque temporario de teste");
        Set(equipmentItem, "categoria", "Equipamento militar");
        Set(equipmentItem, "recurso", Enum.Parse(TypeOf("RecursoMercado"), "Armamentos"));
        Set(equipmentItem, "recursoId", equipmentId);
        Set(equipmentItem, "equipamentoMilitar", true);
        Set(equipmentItem, "prefabId", equipmentId);
        Set(equipmentItem, "tipoEntrega", "terra");
        Set(equipmentItem, "podeVender", true);
        Set(equipmentItem, "precoAtual", unitPrice);
        ((IList)Read(mercado, "itens")).Add(equipmentItem);
        itemEquipamentoAdicionado = equipmentItem;

        GameObject existingUnit = new GameObject(equipmentId);
        objects.Add(existingUnit);
        Component identity = existingUnit.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", SellerTeam);
        Set(identity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Veiculo"));
        Set(comprador, "saldo", 100L);
        yield return null;

        Assert.That((int)Call(mercado, "ObterEstoqueEquipamento", equipmentItem, SellerTeam), Is.EqualTo(1),
            "A unidade existente precisa aparecer no estoque vendável do país vendedor.");
        Assert.That((int)Call(mercado, "ObterEstoqueEquipamento", equipmentItem, BuyerTeam), Is.Zero);

        bool sold = (bool)CallWithOut(mercado, "Vender", SellerTeam, BuyerTeam, equipmentId, 1, out string message);
        Assert.That(sold, Is.True, message);
        Assert.That(Read(identity, "teamID"), Is.EqualTo(BuyerTeam), "A venda deve transferir a identidade da unidade existente.");
        Assert.That((int)Call(mercado, "ObterEstoqueEquipamento", equipmentItem, SellerTeam), Is.Zero);
        Assert.That((int)Call(mercado, "ObterEstoqueEquipamento", equipmentItem, BuyerTeam), Is.EqualTo(1));
        Assert.That((long)Read(comprador, "saldo"), Is.EqualTo(100L - unitPrice));
        Assert.That((long)Read(vendedor, "saldo"), Is.EqualTo(sellerBalance + unitPrice));

        Type unitType = TypeOf("IdentidadeUnidade");
        IList registeredUnits = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));
        Type registryType = TypeOf("RegistroEntidadesJogo");
        registryType.GetMethod("FillUnidades", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { registeredUnits });
        int matchingUnits = registeredUnits.Cast<object>()
            .Count(unit => ((Component)unit).gameObject.name == equipmentId);
        Assert.That(matchingUnits, Is.EqualTo(1), "A venda não pode criar uma cópia da unidade militar.");
    }

    [Test]
    public void MarketAircraftDeliveryRespectsPlayersNationalAircraftLimit()
    {
        Type governorType = TypeOf("GovernadorGameplayRTS");
        Component governor = GetStatic(governorType, "Instancia") as Component;
        bool createdGovernor = governor == null;
        if (createdGovernor)
        {
            GameObject governorObject = new GameObject("Market aircraft limit regression governor");
            objects.Add(governorObject);
            governor = governorObject.AddComponent(governorType);
        }

        string[] governorFields = {
            "limiteTotal", "limiteInfantaria", "limiteVeiculos", "limiteAereos", "limiteNavais",
            "limiteEstruturas", "bloquearProducaoQuandoSaturado"
        };
        object[] originalGovernorValues = new object[governorFields.Length];
        for (int i = 0; i < governorFields.Length; i++)
            originalGovernorValues[i] = Read(governor, governorFields[i]);

        object playerCountry = Call(governo, "ObterPais", BuyerTeam);
        int originalPlayerManpower = (int)Read(playerCountry, "alistaveis");
        int originalPlayerTeam = (int)Read(governo, "teamJogador");

        try
        {
            Set(governo, "teamJogador", BuyerTeam);
            Set(playerCountry, "alistaveis", 100000);

            GameObject airportObject = new GameObject("Market aircraft limit buyer airport");
            airportObject.SetActive(false);
            objects.Add(airportObject);
            airportObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component airportIdentity = airportObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(airportIdentity, "teamID", BuyerTeam);
            Set(airportIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Estrutura"));
            airportObject.SetActive(true);
            Component airport = airportObject.GetComponent(TypeOf("GerenciadorAeroporto"));

            GameObject fieldAircraft = new GameObject("Market aircraft limit existing player aircraft");
            objects.Add(fieldAircraft);
            Component fieldIdentity = fieldAircraft.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(fieldIdentity, "teamID", BuyerTeam);
            Set(fieldIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));

            GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aircraftPrefab.name = "Market_Aircraft_Over_Player_Limit";
            objects.Add(aircraftPrefab);
            Component prefabIdentity = aircraftPrefab.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(prefabIdentity, "teamID", 99);
            Set(prefabIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Aereo"));
            Set(prefabIdentity, "militaresConsumidos", 0);
            aircraftPrefab.AddComponent(TypeOf("ControleAviao"));

            ScriptableObject aircraftCard = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
            temporaryAssets.Add(aircraftCard);
            SetProperty(aircraftCard, "NomeItem", aircraftPrefab.name);
            SetProperty(aircraftCard, "PrefabDaUnidade", aircraftPrefab);
            Set(aircraftCard, "categoria", Enum.Parse(TypeOf("DadosConstrucao+CategoriaItem"), "Aeronautica"));
            string prefabId = (string)aircraftCard.GetType().GetMethod("GetStableId", Members).Invoke(aircraftCard, null);

            Type menuType = TypeOf("MenuConstrucao");
            FieldInfo catalogField = menuType.GetField("catalogoGlobal", Members);
            originalConstructionCatalog = catalogField.GetValue(null);
            IList testCatalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TypeOf("DadosConstrucao")));
            if (originalConstructionCatalog is IList oldCatalog)
            {
                foreach (object existingCard in oldCatalog) testCatalog.Add(existingCard);
            }
            testCatalog.Add(aircraftCard);
            catalogField.SetValue(null, testCatalog);
            constructionCatalogOverridden = true;

            object marketItem = Activator.CreateInstance(TypeOf("DadosItemMercado"));
            Set(marketItem, "id", "market_aircraft_over_player_limit");
            Set(marketItem, "prefabId", prefabId);
            Set(marketItem, "tipoEntrega", "aeronave");
            Set(marketItem, "equipamentoMilitar", true);
            ((IList)Read(mercado, "itens")).Add(marketItem);
            itemEquipamentoAdicionado = marketItem;

            Type unitIdentityType = TypeOf("IdentidadeUnidade");
            IList units = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitIdentityType));
            TypeOf("RegistroEntidadesJogo").GetMethod("FillUnidades", Members).Invoke(null, new object[] { units });
            int playerAircraftCount = units.Cast<object>().Count(unit => unit != null
                && (int)Read(unit, "teamID") == BuyerTeam
                && Read(unit, "tipoUnidade").ToString() == "Aereo");
            Assert.That(playerAircraftCount, Is.GreaterThan(0), "A fixture precisa ter uma aeronave do jogador em campo.");

            Set(governor, "limiteTotal", 10000);
            Set(governor, "limiteInfantaria", 10000);
            Set(governor, "limiteVeiculos", 10000);
            Set(governor, "limiteAereos", playerAircraftCount);
            Set(governor, "limiteNavais", 10000);
            Set(governor, "limiteEstruturas", 10000);
            Set(governor, "bloquearProducaoQuandoSaturado", false);

            object[] gateArgs = { aircraftCard, 1, null };
            bool normalPlayerGateAllows = (bool)governorType.GetMethod("PermitirProducao", Members).Invoke(null, gateArgs);
            Assert.That(normalPlayerGateAllows, Is.False,
                "A fixture precisa demonstrar que o gate nacional recusa outra aeronave no limite. Motivo: " + gateArgs[2]);

            object order = Activator.CreateInstance(TypeOf("PedidoMercadoLogistico"));
            Set(order, "id", "market_aircraft_pending_at_player_limit");
            Set(order, "compradorTeamId", BuyerTeam);
            Set(order, "vendedorTeamId", SellerTeam);
            Set(order, "itemId", "market_aircraft_over_player_limit");
            Set(order, "quantidade", 1);
            Set(order, "total", 1234);
            Set(order, "frete", 50);
            Set(order, "status", "DESCARREGANDO");

            Type logisticsType = TypeOf("SistemaLogisticaMercado");
            MethodInfo finalize = logisticsType.GetMethod("FinalizarPedido", Members);
            MethodInfo processPending = logisticsType.GetMethod("ProcessarEntregasPendentes", Members);
            Assert.That(finalize, Is.Not.Null);
            Assert.That(processPending, Is.Not.Null);
            long sellerBalanceBeforeDelivery = (long)Read(vendedor, "saldo");
            finalize.Invoke(logistica, new[] { order });
            IList patio = (IList)Read(airport, "avioesNoPatio");
            IList hangar = (IList)Read(airport, "avioesNoHangar");
            foreach (object entry in patio.Cast<object>().Concat(hangar.Cast<object>()))
            {
                Component aircraft = entry as Component;
                if (aircraft != null && aircraft.gameObject.name.StartsWith(aircraftPrefab.name, StringComparison.Ordinal))
                    objects.Add(aircraft.gameObject);
            }

            Assert.That(Read(order, "status").ToString(), Is.EqualTo("AGUARDANDO DESTINO"),
                "O pedido pago precisa aguardar capacidade quando o jogador já atingiu o limite nacional.");
            Assert.That((long)Read(vendedor, "saldo"), Is.EqualTo(sellerBalanceBeforeDelivery),
                "O vendedor só recebe quando a carga puder ser entregue.");
            Assert.That(patio.Count + hangar.Count, Is.Zero,
                "Uma aeronave acima do limite não pode ser registrada no aeroporto do jogador.");

            Set(governor, "limiteAereos", playerAircraftCount + 1);
            processPending.Invoke(logistica, null);

            Assert.That(Read(order, "status").ToString(), Is.EqualTo("ENTREGUE"),
                "A fila deve entregar o pedido assim que houver vaga no limite nacional.");
            Assert.That((long)Read(vendedor, "saldo"), Is.EqualTo(sellerBalanceBeforeDelivery + 1234),
                "A liquidação do pedido deve ocorrer uma vez, após a entrega.");
            Assert.That(patio.Count + hangar.Count, Is.EqualTo(1),
                "A aeronave deve ser registrada no aeroporto após liberar capacidade.");

            foreach (object entry in patio.Cast<object>().Concat(hangar.Cast<object>()))
            {
                Component aircraft = entry as Component;
                if (aircraft != null && aircraft.gameObject.name.StartsWith(aircraftPrefab.name, StringComparison.Ordinal))
                    objects.Add(aircraft.gameObject);
            }
        }
        finally
        {
            Set(governo, "teamJogador", originalPlayerTeam);
            Set(playerCountry, "alistaveis", originalPlayerManpower);
            if (!createdGovernor)
            {
                for (int i = 0; i < governorFields.Length; i++)
                    Set(governor, governorFields[i], originalGovernorValues[i]);
            }
        }
    }

    private void AddStock(int teamId, string id, int quantity) => Call(governo, "AdicionarEstoque", teamId, id, quantity);

    private Component CriarSistemaSaveTemporario()
    {
        Type saveType = TypeOf("SistemaSaveGame");
        FieldInfo instanceField = saveType.GetField("Instancia", BindingFlags.Public | BindingFlags.Static);
        originalSaveSystemInstance = instanceField.GetValue(null);
        Component existing = originalSaveSystemInstance as Component;
        if (existing != null)
        {
            saveSystem = existing;
            originalSaveData = Read(saveSystem, "dadosAtuais");
            originalSavePath = (string)Read(saveSystem, "caminhoDoArquivo");
            originalSelectedSaveId = (string)Read(saveSystem, "saveSelecionadoId");
            originalLoadedFromSave = (bool)Read(saveSystem, "carregouDeSave");
            originalRestorePending = (bool)Read(saveSystem, "restauracaoPendente");
            return existing;
        }

        GameObject saveObject = new GameObject("Integration_TemporarySaveSystem");
        saveSystem = saveObject.AddComponent(saveType);
        saveSystemCreated = true;
        return (Component)saveSystem;
    }

    private static int CountSceneObjectsNamed(string objectName)
    {
        GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;
        for (int i = 0; i < gameObjects.Length; i++)
        {
            GameObject gameObject = gameObjects[i];
            if (gameObject != null && gameObject.scene.IsValid() && gameObject.name == objectName) count++;
        }
        return count;
    }

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
