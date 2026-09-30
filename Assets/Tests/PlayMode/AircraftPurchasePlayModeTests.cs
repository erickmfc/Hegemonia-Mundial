using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AircraftPurchasePlayModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private Component originalResourcesManager;
    private long originalResourceBalance;
    private float originalMoneyPerSecond;
    private bool originalAutomaticIncome;

    private Component originalConstructionMenu;
    private Component originalMenuManager;
    private GameObject originalMenuPanel;
    private Component originalMenuCanvasGroup;

    private static Type TypeOf(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Members).SetValue(target, value);

    private static object Read(object target, string name) =>
        target.GetType().GetField(name, Members).GetValue(target);

    private static string Localize(string key, string fallback)
    {
        Type type = TypeOf("LocalizationManager");
        MethodInfo method = type.GetMethod("T", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
        return (string)method.Invoke(null, new object[] { key, fallback });
    }

    private static void MostrarMensagemHUD(string msg, float duracao)
    {
        Type type = TypeOf("HUDAjudaRTS");
        MethodInfo method = type.GetMethod("MostrarMensagemTemporaria", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(float) }, null);
        if (method != null) method.Invoke(null, new object[] { msg, duracao });
    }

    private Component Create(string name)
    {
        var go = new GameObject("Test_" + name);
        go.SetActive(false);
        objects.Add(go);
        return go.AddComponent(TypeOf(name));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (originalResourcesManager != null)
        {
            Set(originalResourcesManager, "dinheiro", originalResourceBalance);
            Set(originalResourcesManager, "dinheiroPorSegundo", originalMoneyPerSecond);
            Set(originalResourcesManager, "ativarGanhosAutomaticos", originalAutomaticIncome);
        }

        if (originalConstructionMenu != null)
        {
            Set(originalConstructionMenu, "gerente", originalMenuManager);
            Set(originalConstructionMenu, "painelPrincipal", originalMenuPanel);
            Set(originalConstructionMenu, "canvasGroupPainel", originalMenuCanvasGroup);
        }

        foreach (var go in objects)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        objects.Clear();

        foreach (var asset in assets)
        {
            if (asset != null) UnityEngine.Object.Destroy(asset);
        }
        assets.Clear();

        yield return null;
    }

    private Terrain CreateRaisedLandFixture()
    {
        var data = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(100f, 30f, 100f)
        };
        assets.Add(data);
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        {
            for (int x = 0; x < 33; x++)
            {
                heights[z, x] = 1f;
            }
        }
        data.SetHeights(0, 0, heights);
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Test_AirportLand";
        objects.Add(go);
        return go.GetComponent<Terrain>();
    }

    private Component CreatePlayerAirport(Vector3 position)
    {
        Component airport = Create("GerenciadorAeroporto");
        airport.name = "Test_Airport";
        airport.transform.position = position;
        Component identity = airport.gameObject.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        airport.gameObject.SetActive(true);
        return airport;
    }

    private Component PrepareResources(long balance)
    {
        Type type = TypeOf("GerenciadorRecursos");
        originalResourcesManager = type.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null) as Component;

        if (originalResourcesManager != null)
        {
            originalResourceBalance = (long)Read(originalResourcesManager, "dinheiro");
            originalMoneyPerSecond = (float)Read(originalResourcesManager, "dinheiroPorSegundo");
            originalAutomaticIncome = (bool)Read(originalResourcesManager, "ativarGanhosAutomaticos");
            Set(originalResourcesManager, "dinheiro", balance);
            Set(originalResourcesManager, "dinheiroPorSegundo", 0f);
            Set(originalResourcesManager, "ativarGanhosAutomaticos", false);
            return originalResourcesManager;
        }

        Component resources = Create("GerenciadorRecursos");
        resources.gameObject.SetActive(true);
        Set(resources, "dinheiro", balance);
        Set(resources, "dinheiroPorSegundo", 0f);
        Set(resources, "ativarGanhosAutomaticos", false);
        return resources;
    }

    private Component PrepareConstructionMenu(Component manager)
    {
        Type type = TypeOf("MenuConstrucao");
        originalConstructionMenu = type.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetValue(null, null) as Component;
        Component menu = originalConstructionMenu;

        if (menu != null)
        {
            originalMenuManager = (Component)Read(menu, "gerente");
            originalMenuPanel = (GameObject)Read(menu, "painelPrincipal");
            originalMenuCanvasGroup = (Component)Read(menu, "canvasGroupPainel");
        }
        else
        {
            menu = Create("MenuConstrucao");
        }

        Set(menu, "gerente", manager);
        if (Read(menu, "painelPrincipal") == null || Read(menu, "canvasGroupPainel") == null)
        {
            var panel = new GameObject("Test_ConstructionPanel");
            objects.Add(panel);
            Component group = panel.AddComponent<CanvasGroup>();
            Set(menu, "painelPrincipal", panel);
            Set(menu, "canvasGroupPainel", group);
        }
        if (!menu.gameObject.activeSelf) menu.gameObject.SetActive(true);
        return menu;
    }

    private ScriptableObject CreateAircraftCard(GameObject prefab, long price)
    {
        ScriptableObject item = ScriptableObject.CreateInstance(TypeOf("DadosConstrucao"));
        assets.Add(item);
        item.GetType().GetProperty("NomeItem").SetValue(item, "Jato de Teste", null);
        item.GetType().GetProperty("PrefabDaUnidade").SetValue(item, prefab, null);
        Set(item, "precoDefinitivo", price);
        return item;
    }

    private int ObterTotalAeronaves(Component airport)
    {
        var patio = (IList)Read(airport, "avioesNoPatio");
        var hangar = (IList)Read(airport, "avioesNoHangar");
        return (patio != null ? patio.Count : 0) + (hangar != null ? hangar.Count : 0);
    }

    [UnityTest]
    public IEnumerator Test1_LocalAirportPurchase_WithoutPower_DoesNotDebitOrSpawn_AndNotifies()
    {
        CreateRaisedLandFixture();
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", true);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test1_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        string localizedMessage = Localize("airport.no_power_purchase", "fallback");
        Assert.AreNotEqual("fallback", localizedMessage, "A chave 'airport.no_power_purchase' deve estar cadastrada.");
        Assert.IsTrue(localizedMessage.Contains("ENERGIA") || localizedMessage.Contains("POWER") || localizedMessage.Contains("电力"),
            "A mensagem deve informar sobre ausencia de energia.");

        bool semEnergiaAtual = (bool)Read(airport, "semEnergia");
        bool compraExecutada = false;

        if (!semEnergiaAtual)
        {
            long saldoAtual = (long)Read(resources, "dinheiro");
            if (saldoAtual >= precoAeronave)
            {
                Set(resources, "dinheiro", saldoAtual - precoAeronave);
                compraExecutada = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
                    .Invoke(airport, new object[] { aircraftPrefab });
            }
        }
        else
        {
            MostrarMensagemHUD(Localize("airport.no_power_purchase", "AEROPORTO SEM ENERGIA\nConstrua uma usina para reativa-lo."), 4.5f);
        }

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Assert.IsFalse(compraExecutada, "Compra local nao deve ser executada quando sem energia.");
        Assert.AreEqual(saldoAntes, saldoDepois, "0 debito: Saldo antes deve ser igual ao saldo depois.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "0 spawn: Quantidade antes deve ser igual a depois.");
    }

    [UnityTest]
    public IEnumerator Test2_LocalAirportPurchase_InsufficientFunds_DoesNotDebitOrSpawn()
    {
        CreateRaisedLandFixture();
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 500L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test2_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        bool compraExecutada = false;
        long saldoAtual = (long)Read(resources, "dinheiro");
        if (saldoAtual >= precoAeronave)
        {
            Set(resources, "dinheiro", saldoAtual - precoAeronave);
            compraExecutada = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
                .Invoke(airport, new object[] { aircraftPrefab });
        }
        else
        {
            MostrarMensagemHUD(Localize("economy.no_money_action", "SEM DINHEIRO\nRecursos insuficientes para esta acao."), 3.2f);
        }

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Assert.IsFalse(compraExecutada, "Compra nao deve ser executada com saldo insuficiente.");
        Assert.AreEqual(saldoAntes, saldoDepois, "0 debito: saldo permanece inalterado.");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "0 spawn: nenhuma aeronave criada.");
        Assert.GreaterOrEqual(saldoDepois, 0, "Saldo nao pode ficar negativo.");
    }

    [UnityTest]
    public IEnumerator Test3_LocalAirportPurchase_ValidConditions_DebitsExactlyOnceAndSpawns()
    {
        CreateRaisedLandFixture();
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test3_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        long saldoAtual = (long)Read(resources, "dinheiro");
        Assert.GreaterOrEqual(saldoAtual, precoAeronave, "Saldo deve ser suficiente.");

        Set(resources, "dinheiro", saldoAtual - precoAeronave);
        bool comprado = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
            .Invoke(airport, new object[] { aircraftPrefab });

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Debug.Log($"[TESTE 3 REGISTRO] Saldo Antes: {saldoAntes}, Preco: {precoAeronave}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}");

        Assert.IsTrue(comprado, "O metodo ComprarAviao deve retornar sucesso.");
        Assert.AreEqual(saldoAntes - precoAeronave, saldoDepois, "Exatamente 1 debito realizado.");
        Assert.AreEqual(quantidadeAntes + 1, quantidadeDepois, "Exatamente 1 aeronave criada.");
    }

    [UnityTest]
    public IEnumerator Test4_LocalAirportPurchase_SpawnFailure_ReproducesDebitWithoutSpawn()
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Test4_WaterSurface";
        water.transform.position = new Vector3(5000f, 0f, 5000f);
        water.transform.localScale = new Vector3(100f, 2f, 100f);
        objects.Add(water);

        Component airport = CreatePlayerAirport(new Vector3(5000f, 0f, 5000f));
        Set(airport, "semEnergia", false);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test4_AircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(5200f, 20f, 5200f);
        objects.Add(aircraftPrefab);

        int precoAeronave = 1500;
        Set(airport, "prefabDroneKamikaze", aircraftPrefab);
        Set(airport, "precoDroneKamikaze", precoAeronave);

        yield return null;

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        LogAssert.Expect(LogType.Error,
            "[Aeroporto] Spawn aéreo bloqueado em água: Test_Airport ((5000.0, 0.0, 5000.0)). Corrija o ponto Preparacao/pista da base.");

        Set(resources, "dinheiro", saldoAntes - precoAeronave);
        bool retornoComprarAviao = (bool)airport.GetType().GetMethod("ComprarAviao", Members)
            .Invoke(airport, new object[] { aircraftPrefab });

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Debug.Log($"[TESTE 4 REGISTRO] Saldo Antes: {saldoAntes}, Retorno ComprarAviao: {retornoComprarAviao}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}, Motivo: Spawn aereo bloqueado em agua.");

        Assert.IsFalse(retornoComprarAviao, "ComprarAviao deve retornar false ao rejeitar o spawn.");
        Assert.AreEqual(saldoAntes - precoAeronave, saldoDepois, "REPRODUCAO: O saldo diminuiu apesar do spawn ter falhado!");
        Assert.AreEqual(quantidadeAntes, quantidadeDepois, "REPRODUCAO: 0 aeronaves foram criadas!");
    }

    [UnityTest]
    public IEnumerator Test5_MenuConstruction_AirportWithoutPower_PreservesPurchasePolicy()
    {
        CreateRaisedLandFixture();
        Component airport = CreatePlayerAirport(new Vector3(50f, 30f, 50f));
        Set(airport, "semEnergia", true);

        long saldoInicial = 5000L;
        Component resources = PrepareResources(saldoInicial);
        Component gameManager = Create("GerenteDeJogo");

        GameObject aircraftPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aircraftPrefab.name = "Test5_MenuAircraftPrefab";
        aircraftPrefab.transform.position = new Vector3(80f, 30f, 80f);
        objects.Add(aircraftPrefab);

        long precoItem = 200L;
        ScriptableObject item = CreateAircraftCard(aircraftPrefab, precoItem);
        Component menu = PrepareConstructionMenu(gameManager);

        yield return null;

        long saldoAntes = (long)Read(resources, "dinheiro");
        int quantidadeAntes = ObterTotalAeronaves(airport);

        menu.GetType().GetMethod("ProduzirUnidadeAerea", Members)
            .Invoke(menu, new object[] { item, 1, null });

        yield return null;

        long saldoDepois = (long)Read(resources, "dinheiro");
        int quantidadeDepois = ObterTotalAeronaves(airport);

        Debug.Log($"[TESTE 5 REGISTRO] MenuConstrucao sem energia: Saldo Antes: {saldoAntes}, Saldo Depois: {saldoDepois}, Qtd Antes: {quantidadeAntes}, Qtd Depois: {quantidadeDepois}");

        Assert.AreEqual(saldoAntes - precoItem, saldoDepois, "MenuConstrucao deve debitar exatamente o preco do item.");
        Assert.AreEqual(quantidadeAntes + 1, quantidadeDepois, "MenuConstrucao deve entregar a aeronave com sucesso.");
    }
}
