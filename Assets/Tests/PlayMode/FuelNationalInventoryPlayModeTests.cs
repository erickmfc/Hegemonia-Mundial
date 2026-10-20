using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class FuelNationalInventoryPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private object governo;
    private object industrial;
    private object saveAntes;
    private string idPetroleoBruto;
    private string idAco;
    private string idDiesel;
    private string idCombustivelAviacao;
    private string idCombustivelNaval;
    private Scene cenaGlobalCriada;

    [UnitySetUp]
    public IEnumerator SetUpRuntimeSystems()
    {
        Type governoType = TypeOf("SistemaGovernoMundial");
        governoType.GetMethod("GarantirInstancia", StaticMembers).Invoke(null, null);
        yield return null;

        governo = GetStatic(governoType, "Instancia");
        industrial = GetStatic(TypeOf("SistemaIndustrialNacional"), "Instancia");
        Assert.That(governo, Is.Not.Null);
        Assert.That(industrial, Is.Not.Null);

        Type industriaIds = TypeOf("IndustriaIds");
        idPetroleoBruto = (string)industriaIds.GetField("PetroleoBruto", StaticMembers).GetValue(null);
        idAco = (string)industriaIds.GetField("AcoEstrutural", StaticMembers).GetValue(null);
        idDiesel = (string)industriaIds.GetField("Diesel", StaticMembers).GetValue(null);
        idCombustivelAviacao = (string)industriaIds.GetField("CombustivelAviacao", StaticMembers).GetValue(null);
        idCombustivelNaval = (string)industriaIds.GetField("CombustivelNaval", StaticMembers).GetValue(null);
        Invoke(industrial, "GarantirPaisExistentes");

        object paisJogador = Invoke(governo, "ObterPais", 1);
        object paisIA = Invoke(governo, "ObterPais", 2);
        Assert.That(paisJogador, Is.Not.Null);
        Assert.That(paisIA, Is.Not.Null);
        Assert.That(Quantidade(1, idPetroleoBruto), Is.EqualTo(Convert.ToDouble(Read(paisJogador, "petroleo"))).Within(0.01d));
        Assert.That(Quantidade(2, idPetroleoBruto), Is.EqualTo(Convert.ToDouble(Read(paisIA, "petroleo"))).Within(0.01d));

        saveAntes = Invoke(industrial, "CriarSaveData");
    }

    [UnityTearDown]
    public IEnumerator RestoreIndustrialInventory()
    {
        if (cenaGlobalCriada.IsValid() && cenaGlobalCriada.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(cenaGlobalCriada);
            if (unload != null) yield return unload;
            cenaGlobalCriada = default(Scene);
        }

        if (industrial != null && saveAntes != null)
        {
            Invoke(industrial, "RestaurarSaveData", saveAntes);
            object mercado = GetStatic(TypeOf("SistemaMercadoGlobal"), "Instancia");
            if (mercado != null)
            {
                CallStatic(TypeOf("IntegracaoMercadoIndustrial"), "SincronizarEstoquesNoMercado", industrial, mercado);
            }
        }

        foreach (GameObject go in objects)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        objects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FreshGlobalMapEntryCreatesInitialNationalFuelWithoutSanitizingScenarioUnits()
    {
        Type saveType = TypeOf("SistemaSaveGame");
        object save = GetStatic(saveType, "Instancia");
        Assert.That(save, Is.Not.Null, "O bootstrap RTS deve criar o sistema de save antes da entrada no mapa.");

        object[] paises = new object[5];
        object[] saldos = new object[5];
        for (int teamId = 1; teamId <= paises.Length; teamId++)
        {
            paises[teamId - 1] = Invoke(governo, "ObterPais", teamId);
            if (paises[teamId - 1] != null)
                saldos[teamId - 1] = Read(paises[teamId - 1], "saldo");
        }

        bool saveCarregadoAntes = Convert.ToBoolean(Read(save, "carregouDeSave"));
        FieldInfo marcadorPartidaNova = saveType.GetField("<partidaNovaRecemIniciada>k__BackingField", Members);
        Assert.That(marcadorPartidaNova, Is.Not.Null);
        bool marcadorAntes = Convert.ToBoolean(marcadorPartidaNova.GetValue(save));
        bool economiaInicializadaAntes = Convert.ToBoolean(Read(save, "economiaMapaGlobalInicializada"));

        Scene mapa = SceneManager.GetSceneByName("GlobalMapRTS");
        if (!mapa.IsValid())
        {
            mapa = SceneManager.CreateScene("GlobalMapRTS");
            cenaGlobalCriada = mapa;
        }

        try
        {
            Set(save, "carregouDeSave", false);
            marcadorPartidaNova.SetValue(save, false);
            Set(save, "economiaMapaGlobalInicializada", false);

            MethodInfo aoCarregarCena = saveType.GetMethod("AoCarregarCena", Members);
            Assert.That(aoCarregarCena, Is.Not.Null);
            aoCarregarCena.Invoke(save, new object[] { mapa, LoadSceneMode.Single });

            Assert.That(Quantidade(1, idDiesel), Is.EqualTo(20000d).Within(0.01d));
            Assert.That(Quantidade(1, idCombustivelAviacao), Is.EqualTo(20000d).Within(0.01d));
            Assert.That(Quantidade(1, idCombustivelNaval), Is.EqualTo(50000d).Within(0.01d));
            Assert.That(Convert.ToBoolean(Read(save, "economiaMapaGlobalInicializada")), Is.True,
                "A primeira entrada direta no GlobalMapRTS sem save deve provisionar o estoque uma única vez.");
            Assert.That(Convert.ToBoolean(marcadorPartidaNova.GetValue(save)), Is.False,
                "A entrada direta não deve acionar a sanitização/limpeza de uma nova campanha.");

            GameObject unidadeDeCenario = new GameObject("Unidade Valdoria inicial do mapa global");
            SceneManager.MoveGameObjectToScene(unidadeDeCenario, mapa);
            Component identidade = unidadeDeCenario.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidade, "teamID", 2);
            objects.Add(unidadeDeCenario);

            marcadorPartidaNova.SetValue(save, true);
            MethodInfo sanitizar = saveType.GetMethod("SanitizarCenaDePartidaNova", Members);
            Assert.That(sanitizar, Is.Not.Null);
            sanitizar.Invoke(save, new object[] { mapa });
            yield return null;

            Assert.That(unidadeDeCenario, Is.Not.Null,
                "A preparação de novo jogo no mapa global deve preservar as unidades de cenário serializadas.");
            Assert.That(Convert.ToBoolean(marcadorPartidaNova.GetValue(save)), Is.False);
        }
        finally
        {
            Set(save, "carregouDeSave", saveCarregadoAntes);
            marcadorPartidaNova.SetValue(save, marcadorAntes);
            Set(save, "economiaMapaGlobalInicializada", economiaInicializadaAntes);
            for (int i = 0; i < paises.Length; i++)
            {
                if (paises[i] != null && saldos[i] != null)
                    Set(paises[i], "saldo", saldos[i]);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayerResourceListenerExceptionDoesNotInterruptSpend()
    {
        Type resourceType = TypeOf("GerenciadorRecursos");
        PropertyInfo singletonProperty = resourceType.GetProperty("Instancia", StaticMembers);
        object previousResourceManager = singletonProperty.GetValue(null, null);
        Type ledgerType = TypeOf("Hegemonia.RTS.RTSResourceLedgerService");
        object resourceLedger = GetStatic(ledgerType, "Instancia");
        Assert.That(resourceLedger, Is.Not.Null);
        int transactionCountBefore = Convert.ToInt32(ReadProperty(resourceLedger, "TransactionCount"));
        string lastTransactionBefore = Convert.ToString(ReadProperty(resourceLedger, "LastTransaction"));
        PropertyInfo transactionCountProperty = ledgerType.GetProperty("TransactionCount", Members);
        PropertyInfo lastTransactionProperty = ledgerType.GetProperty("LastTransaction", Members);
        Component resourceManager = previousResourceManager as Component;
        GameObject createdResourceRoot = null;
        long originalBalance = 0L;
        bool originalAutomaticIncome = false;
        bool capturedOriginalState = false;
        EventInfo resourcesUpdated = resourceType.GetEvent("OnRecursosAtualizados", Members);
        Assert.That(resourcesUpdated, Is.Not.Null);

        if (resourceManager == null)
        {
            createdResourceRoot = new GameObject("Player resource callback test manager");
            createdResourceRoot.SetActive(false);
            resourceManager = createdResourceRoot.AddComponent(resourceType);
            singletonProperty.GetSetMethod(true).Invoke(null, new object[] { resourceManager });
        }

        originalBalance = Convert.ToInt64(Read(resourceManager, "dinheiro"));
        originalAutomaticIncome = Convert.ToBoolean(Read(resourceManager, "ativarGanhosAutomaticos"));
        capturedOriginalState = true;

        Action throwingListener = () =>
            throw new InvalidOperationException("Falha controlada do listener de recursos do jogador.");
        int laterListenerInvocations = 0;
        Action laterListener = () => laterListenerInvocations++;
        resourcesUpdated.AddEventHandler(resourceManager, throwingListener);
        resourcesUpdated.AddEventHandler(resourceManager, laterListener);

        try
        {
            Set(resourceManager, "dinheiro", 1000L);
            Set(resourceManager, "ativarGanhosAutomaticos", false);
            LogAssert.Expect(LogType.Warning, new Regex(
                @"\[GerenciadorRecursos\] Listener de OnRecursosAtualizados falhou: .*: InvalidOperationException: Falha controlada do listener de recursos do jogador\."));

            Exception spendException = null;
            bool spent = false;
            try
            {
                spent = (bool)Invoke(resourceManager, "TentarGastar", 50L, 0, 0, 0);
            }
            catch (TargetInvocationException exception)
            {
                spendException = exception.InnerException ?? exception;
            }

            Assert.That(Convert.ToInt64(Read(resourceManager, "dinheiro")), Is.EqualTo(950L),
                "O teste deve observar o débito antes da falha de notificação.");
            Assert.That(spendException, Is.Null,
                "A falha em OnRecursosAtualizados não deve escapar nem interromper a transação do jogador; "
                + "listenerPosterior=" + laterListenerInvocations);
            Assert.That(spent, Is.True);
            Assert.That(laterListenerInvocations, Is.EqualTo(1),
                "Os callbacks posteriores devem receber a atualização do débito.");
            Assert.That(Convert.ToInt32(ReadProperty(resourceLedger, "TransactionCount")), Is.EqualTo(transactionCountBefore + 1),
                "O ledger deve registrar o débito após notificar os consumidores.");

            Assert.DoesNotThrow(() => Invoke(resourceManager, "AdicionarRecursos", 25L, 0, 0, 0),
                "A mesma proteção deve permitir crédito/estorno mesmo quando um listener falha.");
            Assert.That(Convert.ToInt64(Read(resourceManager, "dinheiro")), Is.EqualTo(975L));
            Assert.That(laterListenerInvocations, Is.EqualTo(2),
                "Os callbacks posteriores também devem receber a atualização do crédito.");
            Assert.That(Convert.ToInt32(ReadProperty(resourceLedger, "TransactionCount")), Is.EqualTo(transactionCountBefore + 2),
                "O ledger deve registrar o crédito depois da notificação.");
        }
        finally
        {
            resourcesUpdated.RemoveEventHandler(resourceManager, laterListener);
            resourcesUpdated.RemoveEventHandler(resourceManager, throwingListener);
            transactionCountProperty.GetSetMethod(true).Invoke(resourceLedger, new object[] { transactionCountBefore });
            lastTransactionProperty.GetSetMethod(true).Invoke(resourceLedger, new object[] { lastTransactionBefore });
            if (capturedOriginalState)
            {
                Set(resourceManager, "dinheiro", originalBalance);
                Set(resourceManager, "ativarGanhosAutomaticos", originalAutomaticIncome);
            }

            if (createdResourceRoot != null)
            {
                singletonProperty.GetSetMethod(true).Invoke(null, new[] { previousResourceManager });
                UnityEngine.Object.DestroyImmediate(createdResourceRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RefuelingUsesOnlyTheOwningTeamAndTheRefinedFuelForItsDomain()
    {
        DefinirEstoque(1, idPetroleoBruto, 100d);
        DefinirEstoque(1, idDiesel, 100d);
        DefinirEstoque(1, idCombustivelAviacao, 80d);
        DefinirEstoque(1, idCombustivelNaval, 70d);
        DefinirEstoque(2, idPetroleoBruto, 100d);
        DefinirEstoque(2, idDiesel, 100d);
        DefinirEstoque(2, idCombustivelAviacao, 80d);
        DefinirEstoque(2, idCombustivelNaval, 70d);
        DefinirEstoque(3, idPetroleoBruto, 0d);
        DefinirEstoque(3, idDiesel, 0d);

        Component combustivelJogador = CriarTanqueDeTeste("Fuel_Player", 1);
        Component combustivelIA = CriarTanqueDeTeste("Fuel_AI", 2);
        Component combustivelSemDono = CriarTanqueDeTeste("Fuel_UnknownOwner", 0);
        Component combustivelAereo = CriarTanqueDeTeste("Fuel_Aircraft", 1, "Aerea");
        Component combustivelNaval = CriarTanqueDeTeste("Fuel_Ship", 1, "Naval");

        float abastecidoJogador = Abastecer(combustivelJogador, 40f);
        float abastecidoIA = Abastecer(combustivelIA, 40f);
        Assert.That(abastecidoJogador, Is.EqualTo(40f).Within(0.01f));
        Assert.That(abastecidoIA, Is.EqualTo(40f).Within(0.01f));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(60d).Within(0.01d));
        Assert.That(Quantidade(2, idDiesel), Is.EqualTo(60d).Within(0.01d));
        Assert.That(Convert.ToSingle(Read(combustivelJogador, "combustivelAtual")), Is.EqualTo(40f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(combustivelIA, "combustivelAtual")), Is.EqualTo(40f).Within(0.01f));
        Assert.That(Quantidade(1, idPetroleoBruto), Is.EqualTo(100d).Within(0.01d), "Petróleo bruto não pode ser consumido diretamente como combustível.");

        Assert.That(Abastecer(combustivelAereo, 25f), Is.EqualTo(25f).Within(0.01f));
        Assert.That(Abastecer(combustivelNaval, 30f), Is.EqualTo(30f).Within(0.01f));
        Assert.That(Quantidade(1, idCombustivelAviacao), Is.EqualTo(55d).Within(0.01d));
        Assert.That(Quantidade(1, idCombustivelNaval), Is.EqualTo(40d).Within(0.01d));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(60d).Within(0.01d), "O abastecimento de outra classe não pode debitar diesel.");

        Assert.That(Abastecer(combustivelSemDono, 10f), Is.EqualTo(0f));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(60d).Within(0.01d), "Uma unidade sem identidade não pode debitar o estoque do jogador.");
        DefinirEstoque(1, idDiesel, 0d);
        Assert.That(Abastecer(combustivelJogador, 10f), Is.EqualTo(0f), "Sem estoque nacional, o serviço deve bloquear o reabastecimento.");
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(0d).Within(0.01d));
        Assert.That(Quantidade(3, idDiesel), Is.EqualTo(0d).Within(0.01d));

        yield return null;
    }

    [UnityTest]
    public IEnumerator PierDeliveryAddsOnlyCrudeOilAndIndustrialSaveRestoresBothStocks()
    {
        DefinirEstoque(2, idPetroleoBruto, 0d);
        DefinirEstoque(2, idDiesel, 21d);
        int entregue = (int)CallStatic(TypeOf("RecursosPorTime"), "AdicionarPetroleoBruto", 2, 37, true);
        Assert.That(entregue, Is.EqualTo(37));
        Assert.That(Quantidade(2, idPetroleoBruto), Is.EqualTo(37d).Within(0.01d));
        Assert.That(Quantidade(2, idDiesel), Is.EqualTo(21d).Within(0.01d), "A entrega de bruto não deve alterar o estoque de diesel refinado.");

        object saveComEntrega = Invoke(industrial, "CriarSaveData");
        object saveJson = JsonUtility.FromJson(JsonUtility.ToJson(saveComEntrega), saveComEntrega.GetType());
        DefinirEstoque(2, idPetroleoBruto, 0d);
        DefinirEstoque(2, idDiesel, 0d);
        Invoke(industrial, "RestaurarSaveData", saveJson);

        Assert.That(Quantidade(2, idPetroleoBruto), Is.EqualTo(37d).Within(0.01d), "O snapshot industrial deve persistir petróleo bruto.");
        Assert.That(Quantidade(2, idDiesel), Is.EqualTo(21d).Within(0.01d), "O save JSON deve preservar a separação entre bruto e combustível refinado.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator OffshorePlatformAndTankerDeliveryUpdatesOwnedNationalCrudeStock()
    {
        const int teamId = 2;
        const int carga = 37;
        object recursosTime = CallStatic(TypeOf("RecursosPorTime"), "Obter", teamId);
        object petroleoBrutoAntes = Read(recursosTime, "petroleoBruto");
        object petroleoEntregueAntes = Read(recursosTime, "petroleoEntregue");
        object ultimaAtualizacaoAntes = Read(recursosTime, "ultimaAtualizacao");

        try
        {
            DefinirEstoque(teamId, idPetroleoBruto, 0d);
            DefinirEstoque(1, idPetroleoBruto, 0d);

            GameObject plataformaObject = new GameObject("Fuel_Offshore_Transfer_Test");
            plataformaObject.SetActive(false);
            objects.Add(plataformaObject);
            Component identidadePlataforma = plataformaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePlataforma, "teamID", teamId);
            Component plataforma = plataformaObject.AddComponent(TypeOf("PlataformaOffshore"));
            Set(plataforma, "petroleoArmazenado", carga);

            GameObject pierObject = new GameObject("Fuel_Pier_Transfer_Test");
            pierObject.SetActive(false);
            objects.Add(pierObject);
            Component identidadePier = pierObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePier, "teamID", teamId);
            Component pier = pierObject.AddComponent(TypeOf("PierMarinha"));

            GameObject petroleiroObject = new GameObject("Fuel_Offshore_Tanker_Transfer_Test");
            petroleiroObject.SetActive(false);
            objects.Add(petroleiroObject);
            Component identidadePetroleiro = petroleiroObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePetroleiro, "teamID", teamId);
            Component petroleiro = petroleiroObject.AddComponent(TypeOf("NavioPetroleiro"));
            Invoke(petroleiro, "DefinirEquipeOperacao", teamId);
            Set(petroleiro, "plataformaAlvo", plataforma);
            Set(petroleiro, "pierAlvo", pier);
            Set(petroleiro, "capacidadeMaxima", carga);
            Set(petroleiro, "taxaTransferencia", 1000);
            Set(petroleiro, "petroleoCarregado", 0);

            Type estadoType = TypeOf("NavioPetroleiro+EstadoPetroleiro");
            object proximoEstado = Enum.Parse(estadoType, "SAINDO_PLATAFORMA");
            for (int tentativa = 0; tentativa < carga
                && Convert.ToInt32(Read(petroleiro, "petroleoCarregado")) < carga; tentativa++)
            {
                Invoke(petroleiro, "ExecutarOperacaoLogistica", true, proximoEstado);
            }

            Assert.That(Convert.ToInt32(Read(plataforma, "petroleoArmazenado")), Is.EqualTo(0),
                "O carregamento precisa drenar o estoque local da plataforma.");
            Assert.That(Convert.ToInt32(Read(petroleiro, "petroleoCarregado")), Is.EqualTo(carga),
                "O bruto drenado precisa permanecer como carga no petroleiro.");
            Assert.That(Quantidade(teamId, idPetroleoBruto), Is.EqualTo(0d).Within(0.01d),
                "A carga só deve entrar no estoque nacional ao ser entregue no píer.");

            object estadoRetorno = Enum.Parse(estadoType, "RE_DO_PIER");
            for (int tentativa = 0; tentativa < carga
                && Convert.ToInt32(Read(petroleiro, "petroleoCarregado")) > 0; tentativa++)
            {
                Invoke(petroleiro, "ExecutarOperacaoLogistica", false, estadoRetorno);
            }

            Assert.That(Convert.ToInt32(Read(petroleiro, "petroleoCarregado")), Is.EqualTo(0),
                "O petroleiro precisa descarregar a carga antes de deixar o píer.");
            Assert.That(Quantidade(teamId, idPetroleoBruto), Is.EqualTo(carga).Within(0.01d),
                "A descarga física precisa creditar petróleo bruto ao estoque nacional do time dono.");
            Assert.That(Quantidade(1, idPetroleoBruto), Is.EqualTo(0d).Within(0.01d),
                "A entrega não pode ser creditada ao país errado.");
        }
        finally
        {
            Set(recursosTime, "petroleoBruto", petroleoBrutoAntes);
            Set(recursosTime, "petroleoEntregue", petroleoEntregueAntes);
            Set(recursosTime, "ultimaAtualizacao", ultimaAtualizacaoAntes);
        }

        yield return null;
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator GlobalOilPrefabsProduceAndDeliverScaledQuantities()
    {
        const int teamId = 2;
        const int cargaEsperada = 250000;
        const int producaoMinimaEsperada = 275000;
        const int producaoMaximaEsperada = 2750000;
        const int armazenamentoEsperado = 5500000;
        const string caminhoPlataforma = "Assets/Prefabs/Marinha/Plataforma.prefab";
        const string caminhoPetroleiro = "Assets/Prefabs/Marinha/Navio Petroleiro.prefab";

        GameObject prefabPlataforma = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPlataforma);
        GameObject prefabPetroleiro = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPetroleiro);
        Assert.That(prefabPlataforma, Is.Not.Null, "A plataforma validada deve ser o prefab usado pelo jogo.");
        Assert.That(prefabPetroleiro, Is.Not.Null, "O petroleiro validado deve ser o prefab usado pelo jogo.");

        Component plataformaPrefab = prefabPlataforma.GetComponent(TypeOf("PlataformaOffshore"));
        Component petroleiroPrefab = prefabPetroleiro.GetComponent(TypeOf("NavioPetroleiro"));
        Assert.That(Convert.ToInt32(Read(plataformaPrefab, "producaoMinima")), Is.EqualTo(producaoMinimaEsperada));
        Assert.That(Convert.ToInt32(Read(plataformaPrefab, "producaoMaxima")), Is.EqualTo(producaoMaximaEsperada));
        Assert.That(Convert.ToInt32(Read(plataformaPrefab, "capacidadeArmazenamento")), Is.EqualTo(armazenamentoEsperado));
        Assert.That(Convert.ToInt32(Read(petroleiroPrefab, "capacidadeMaxima")), Is.EqualTo(cargaEsperada));
        Assert.That(Convert.ToInt32(Read(petroleiroPrefab, "taxaTransferencia")), Is.EqualTo(50000));
        Assert.That(Convert.ToInt32(Read(petroleiroPrefab, "tempoDeCarregamento")), Is.EqualTo(5));

        object recursosTime = CallStatic(TypeOf("RecursosPorTime"), "Obter", teamId);
        object petroleoBrutoAntes = Read(recursosTime, "petroleoBruto");
        object petroleoEntregueAntes = Read(recursosTime, "petroleoEntregue");
        object ultimaAtualizacaoAntes = Read(recursosTime, "ultimaAtualizacao");
        Component plataformaIdentidade = null;
        Component petroleiroIdentidade = null;
        Component pierIdentidade = null;
        Component plataforma = null;
        Component petroleiro = null;
        Component pier = null;
        float timeScaleAntes = Time.timeScale;

        try
        {
            DefinirEstoque(teamId, idPetroleoBruto, 0d);
            DefinirEstoque(1, idPetroleoBruto, 0d);

            GameObject plataformaObject = UnityEngine.Object.Instantiate(prefabPlataforma);
            objects.Add(plataformaObject);
            plataformaObject.SetActive(false);
            plataforma = plataformaObject.GetComponent(TypeOf("PlataformaOffshore"));
            plataformaIdentidade = plataformaObject.GetComponent(TypeOf("IdentidadeUnidade"));
            if (plataformaIdentidade == null) plataformaIdentidade = plataformaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(plataformaIdentidade, "teamID", teamId);
            Set(plataforma, "ownerTeamId", teamId);
            Set(plataforma, "petroleoArmazenado", 0);
            plataformaObject.SetActive(true);

            yield return new WaitForSecondsRealtime(1.1f);

            int producaoReal = Convert.ToInt32(Read(plataforma, "producaoAtualDestaPlataforma"));
            int estoquePlataforma = Convert.ToInt32(Read(plataforma, "petroleoArmazenado"));
            Assert.That(producaoReal, Is.InRange(producaoMinimaEsperada, producaoMaximaEsperada),
                "A plataforma deve calcular uma produção por segundo dentro da faixa 55x configurada.");
            Assert.That(estoquePlataforma, Is.GreaterThanOrEqualTo(producaoReal),
                "Após um ciclo, o tanque da plataforma deve conter pelo menos um ciclo de produção.");
            Assert.That(Convert.ToInt32(Read(plataforma, "capacidadeArmazenamento")), Is.EqualTo(armazenamentoEsperado));

            GameObject pierObject = new GameObject("Oil_Scale_Pier_Team2");
            pierObject.SetActive(false);
            objects.Add(pierObject);
            pierIdentidade = pierObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(pierIdentidade, "teamID", teamId);
            pier = pierObject.AddComponent(TypeOf("PierMarinha"));

            GameObject petroleiroObject = new GameObject("Oil_Scale_Tanker_Team2");
            petroleiroObject.SetActive(false);
            objects.Add(petroleiroObject);
            petroleiroIdentidade = petroleiroObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(petroleiroIdentidade, "teamID", teamId);
            petroleiro = petroleiroObject.AddComponent(TypeOf("NavioPetroleiro"));
            Assert.That(Convert.ToInt32(Read(petroleiro, "capacidadeMaxima")), Is.EqualTo(cargaEsperada));
            Assert.That(Convert.ToInt32(Read(petroleiro, "taxaTransferencia")), Is.EqualTo(50000));
            Set(petroleiro, "plataformaAlvo", plataforma);
            Set(petroleiro, "pierAlvo", pier);
            Set(petroleiro, "petroleoCarregado", 0);
            Invoke(petroleiro, "DefinirEquipeOperacao", teamId);
            int estoquePlataformaParaCarga = Mathf.Max(estoquePlataforma, cargaEsperada);
            Set(plataforma, "petroleoArmazenado", estoquePlataformaParaCarga);
            Time.timeScale = 1f;

            Type estadoType = TypeOf("NavioPetroleiro+EstadoPetroleiro");
            object saindoDaPlataforma = Enum.Parse(estadoType, "SAINDO_PLATAFORMA");
            for (int frame = 0; frame < 400
                && Convert.ToInt32(Read(petroleiro, "petroleoCarregado")) < cargaEsperada; frame++)
            {
                Invoke(petroleiro, "ExecutarOperacaoLogistica", 0.02f, true, saindoDaPlataforma);
            }

            Assert.That(Convert.ToInt32(Read(petroleiro, "petroleoCarregado")), Is.EqualTo(cargaEsperada),
                "Um ciclo do petroleiro deve transportar 50x a carga antiga de 5.000 unidades.");
            Assert.That(Convert.ToInt32(Read(plataforma, "petroleoArmazenado")), Is.EqualTo(estoquePlataformaParaCarga - cargaEsperada),
                "O carregamento deve retirar exatamente a carga do tanque da plataforma.");

            object retornandoDoPier = Enum.Parse(estadoType, "RE_DO_PIER");
            for (int frame = 0; frame < 400
                && Convert.ToInt32(Read(petroleiro, "petroleoCarregado")) > 0; frame++)
            {
                Invoke(petroleiro, "ExecutarOperacaoLogistica", 0.02f, false, retornandoDoPier);
            }

            Assert.That(Convert.ToInt32(Read(petroleiro, "petroleoCarregado")), Is.Zero);
            Assert.That(Quantidade(teamId, idPetroleoBruto), Is.EqualTo(cargaEsperada).Within(0.01d),
                "A carga ampliada deve chegar integralmente ao estoque nacional bruto da Valdoria/Team 2.");
            Assert.That(Quantidade(1, idPetroleoBruto), Is.Zero,
                "A entrega ampliada não pode ser creditada ao Team 1.");
        }
        finally
        {
            Time.timeScale = timeScaleAntes;
            Set(recursosTime, "petroleoBruto", petroleoBrutoAntes);
            Set(recursosTime, "petroleoEntregue", petroleoEntregueAntes);
            Set(recursosTime, "ultimaAtualizacao", ultimaAtualizacaoAntes);
        }
    }
#endif

    [UnityTest]
    public IEnumerator OffshoreTankerNavigatesShortPlatformToPierRouteAndDeliversCrude()
    {
        const int teamId = 987654;
        const int carga = 37;
        object recursosTime = CallStatic(TypeOf("RecursosPorTime"), "Obter", teamId);
        object petroleoBrutoAntes = Read(recursosTime, "petroleoBruto");
        object petroleoEntregueAntes = Read(recursosTime, "petroleoEntregue");
        object ultimaAtualizacaoAntes = Read(recursosTime, "ultimaAtualizacao");
        Component plataforma = null;
        Component pier = null;
        Component petroleiro = null;

        try
        {
            DefinirEstoque(teamId, idPetroleoBruto, 0d);

            GameObject plataformaObject = new GameObject("Fuel_Route_Offshore_Test");
            plataformaObject.SetActive(false);
            objects.Add(plataformaObject);
            Component identidadePlataforma = plataformaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePlataforma, "teamID", teamId);
            plataforma = plataformaObject.AddComponent(TypeOf("PlataformaOffshore"));
            Set(plataforma, "petroleoArmazenado", carga);
            plataformaObject.SetActive(true);

            GameObject pierObject = new GameObject("Fuel_Route_Pier_Test");
            pierObject.transform.position = new Vector3(8f, 0f, 18f);
            pierObject.SetActive(false);
            objects.Add(pierObject);
            Component identidadePier = pierObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePier, "teamID", teamId);
            pier = pierObject.AddComponent(TypeOf("PierMarinha"));

            GameObject saidaObject = new GameObject("Fuel_Route_Pier_Exit");
            saidaObject.transform.SetParent(pierObject.transform, false);
            saidaObject.transform.localPosition = new Vector3(-8f, 0f, 0f);
            Set(pier, "saida_petro", saidaObject.transform);
            GameObject atracagemObject = new GameObject("Fuel_Route_Pier_Berth");
            atracagemObject.transform.SetParent(pierObject.transform, false);
            atracagemObject.transform.localPosition = Vector3.zero;
            Set(pier, "Atraca_petro", atracagemObject.transform);
            pierObject.SetActive(true);

            GameObject petroleiroObject = new GameObject("Fuel_Route_Tanker_Test");
            petroleiroObject.SetActive(false);
            objects.Add(petroleiroObject);
            Component identidadePetroleiro = petroleiroObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidadePetroleiro, "teamID", teamId);
            petroleiro = petroleiroObject.AddComponent(TypeOf("NavioPetroleiro"));
            petroleiroObject.SetActive(true);
            Invoke(petroleiro, "DefinirEquipeOperacao", teamId);
            Set(petroleiro, "capacidadeMaxima", carga);
            Set(petroleiro, "taxaTransferencia", 100000);
            Set(petroleiro, "velocidadeManobra", 1000f);
            Set(petroleiro, "velocidadeAcoplagem", 1000f);
            petroleiroObject.transform.position = ((Transform)Read(plataforma, "pontoChegada")).position;

            Type estadoType = TypeOf("NavioPetroleiro+EstadoPetroleiro");
            Invoke(petroleiro, "MudarEstado", Enum.Parse(estadoType, "INDO_PLATAFORMA"));
            HashSet<string> estadosVisitados = new HashSet<string>();
            for (int tentativa = 0; tentativa < 512; tentativa++)
            {
                string estadoAtual = Read(petroleiro, "estadoAtual").ToString();
                estadosVisitados.Add(estadoAtual);
                if (estadoAtual == "RE_DO_PIER" && Quantidade(teamId, idPetroleoBruto) >= carga)
                {
                    break;
                }

                Invoke(petroleiro, "Update");
            }

            Assert.That(Read(petroleiro, "estadoAtual").ToString(), Is.EqualTo("RE_DO_PIER"),
                "O petroleiro precisa completar a rota curta e terminar a descarga antes de retornar.");
            Assert.That(Convert.ToInt32(Read(plataforma, "petroleoArmazenado")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(Read(petroleiro, "petroleoCarregado")), Is.EqualTo(0));
            Assert.That(Quantidade(teamId, idPetroleoBruto), Is.EqualTo(carga).Within(0.01d));
            foreach (string estadoEsperado in new[]
            {
                "INDO_PLATAFORMA",
                "ACOPLANDO_PLATAFORMA",
                "CARREGANDO",
                "SAINDO_PLATAFORMA",
                "INDO_PIER",
                "ACOPLANDO_PIER",
                "DESCARREGANDO",
                "RE_DO_PIER"
            })
            {
                Assert.That(estadosVisitados, Does.Contain(estadoEsperado),
                    "A rota logística deve percorrer todos os estados físicos, inclusive " + estadoEsperado + ".");
            }
        }
        finally
        {
            if (petroleiro != null) ((Behaviour)petroleiro).enabled = false;
            if (pier != null) ((Behaviour)pier).enabled = false;
            if (plataforma != null) ((Behaviour)plataforma).enabled = false;
            Set(recursosTime, "petroleoBruto", petroleoBrutoAntes);
            Set(recursosTime, "petroleoEntregue", petroleoEntregueAntes);
            Set(recursosTime, "ultimaAtualizacao", ultimaAtualizacaoAntes);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator ExistingIndustrialRefiningPipelineProducesDieselThatCanRefuelAUnit()
    {
        object pais = Invoke(governo, "ObterPais", 2);
        object saldoAntes = Read(pais, "saldo");
        object energiaAntes = Read(pais, "energia");
        object estoqueAntes = Invoke(industrial, "CriarSaveData");
        try
        {
            Set(pais, "saldo", Convert.ChangeType(10000, Read(pais, "saldo").GetType()));
            Set(pais, "energia", Convert.ChangeType(1000, Read(pais, "energia").GetType()));
            DefinirEstoque(2, idPetroleoBruto, 500d);
            DefinirEstoque(2, idDiesel, 0d);

            object ordem = Invoke(industrial, "CriarOrdemRefino", 2, "refino_diesel");
            Assert.That(ordem, Is.Not.Null);
            Assert.That(Read(ordem, "estado").ToString(), Is.EqualTo("Produzindo"));
            Assert.That(Quantidade(2, idPetroleoBruto), Is.EqualTo(0d).Within(0.01d), "A receita reserva o bruto enquanto refina.");
            Assert.That(Quantidade(2, idDiesel), Is.EqualTo(0d).Within(0.01d));

            Invoke(industrial, "ProcessarRefinoPais", pais, 101);
            Invoke(industrial, "ProcessarRefinoPais", pais, 102);

            Assert.That(Quantidade(2, idDiesel), Is.EqualTo(500d).Within(0.01d), "A conclusão do refino deve gravar o diesel no armazém nacional.");
            Component combustivel = CriarTanqueDeTeste("Fuel_Refined_Output", 2);
            Assert.That(Abastecer(combustivel, 35f), Is.EqualTo(35f).Within(0.01f));
            Assert.That(Quantidade(2, idDiesel), Is.EqualTo(465d).Within(0.01d));
        }
        finally
        {
            Invoke(industrial, "RestaurarSaveData", estoqueAntes);
            Set(pais, "saldo", saldoAntes);
            Set(pais, "energia", energiaAntes);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator MilitaryMarketImportsRefinedDieselIntoTheBuyerCountry()
    {
        object comprador = Invoke(governo, "ObterPais", 1);
        object vendedor = Invoke(governo, "ObterPais", 2);
        object relacao = Invoke(governo, "ObterRelacao", 1, 2);
        object saldoCompradorAntes = Read(comprador, "saldo");
        object guerraCompradorAntes = Read(comprador, "emGuerra");
        object guerraVendedorAntes = Read(vendedor, "emGuerra");
        object guerraRelacaoAntes = Read(relacao, "guerraDeclarada");
        object sancaoRelacaoAntes = Read(relacao, "sancaoAtiva");
        double petroleoBrutoCompradorAntes = Quantidade(1, idPetroleoBruto);
        object mercado = null;
        IList pedidosLogisticos = null;
        IList historicoMercado = null;
        int pedidosAntes = 0;
        int historicoAntes = 0;
        try
        {
            Set(comprador, "saldo", Convert.ChangeType(20000, Read(comprador, "saldo").GetType()));
            Set(comprador, "emGuerra", false);
            Set(vendedor, "emGuerra", false);
            Set(relacao, "guerraDeclarada", false);
            Set(relacao, "sancaoAtiva", false);
            DefinirEstoque(1, idDiesel, 0d);
            DefinirEstoque(2, idDiesel, 3000d);

            Type marketType = TypeOf("SistemaMercadoGlobal");
            mercado = GetStatic(marketType, "Instancia");
            Assert.That(mercado, Is.Not.Null);
            CallStatic(TypeOf("IntegracaoMercadoIndustrial"), "SincronizarEstoquesNoMercado", industrial, mercado);

            object item = Invoke(mercado, "ObterItem", idDiesel);
            Assert.That(Read(item, "recurso").ToString(), Is.EqualTo("Diesel"), "O catálogo de mercado deve identificar o produto refinado correto.");
            object logistica = GetStatic(TypeOf("SistemaLogisticaMercado"), "Instancia");
            Assert.That(logistica, Is.Not.Null);
            pedidosLogisticos = (IList)Read(logistica, "pedidos");
            historicoMercado = (IList)Read(mercado, "historico");
            pedidosAntes = pedidosLogisticos.Count;
            historicoAntes = historicoMercado.Count;

            object[] argumentos = { 1, 2, idDiesel, 5, string.Empty };
            MethodInfo comprar = marketType.GetMethods(Members)
                .First(m => m.Name == "Comprar" && m.GetParameters().Length == argumentos.Length);
            bool sucesso = (bool)comprar.Invoke(mercado, argumentos);

            Assert.That(sucesso, Is.True, Convert.ToString(argumentos[4]));
            Assert.That(Quantidade(1, idDiesel), Is.EqualTo(0d).Within(0.01d), "O combustível importado só entra no armazém depois do frete marítimo.");
            Assert.That(Quantidade(2, idDiesel), Is.EqualTo(2995d).Within(0.01d));
            Assert.That(Quantidade(1, idPetroleoBruto), Is.EqualTo(petroleoBrutoCompradorAntes).Within(0.01d), "Importar diesel refinado não pode alterar o petróleo bruto do comprador.");
            Assert.That(pedidosLogisticos.Count, Is.EqualTo(pedidosAntes + 1));
            object pedido = pedidosLogisticos[pedidosLogisticos.Count - 1];
            Assert.That(Read(pedido, "itemId"), Is.EqualTo(idDiesel));
            Assert.That(Read(pedido, "status"), Is.EqualTo("AGUARDANDO EMBARQUE"));
        }
        finally
        {
            RemoverNovosItens(historicoMercado, historicoAntes);
            RemoverNovosItens(pedidosLogisticos, pedidosAntes);
            Set(comprador, "saldo", saldoCompradorAntes);
            Set(comprador, "emGuerra", guerraCompradorAntes);
            Set(vendedor, "emGuerra", guerraVendedorAntes);
            Set(relacao, "guerraDeclarada", guerraRelacaoAntes);
            Set(relacao, "sancaoAtiva", sancaoRelacaoAntes);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator IA03MarksMissingFuelForAnActiveForceDomainAsLowReadiness()
    {
        object pais = Invoke(governo, "ObterPais", 2);
        string[] campos = { "saldo", "comida", "petroleo", "energia", "deficitComida", "deficitPetroleo", "deficitEnergia" };
        object[] valoresAntes = new object[campos.Length];
        for (int i = 0; i < campos.Length; i++) valoresAntes[i] = Read(pais, campos[i]);

        GameObject brainRoot = new GameObject("IA03 fuel-readiness test brain");
        brainRoot.SetActive(false);
        objects.Add(brainRoot);
        ScriptableObject perfil = ScriptableObject.CreateInstance(TypeOf("Hegemonia.AI.IA03.PerfilPaisSO"));
        try
        {
            Set(pais, "saldo", Convert.ChangeType(20000, Read(pais, "saldo").GetType()));
            Set(pais, "comida", Convert.ChangeType(1000, Read(pais, "comida").GetType()));
            Set(pais, "petroleo", Convert.ChangeType(5000, Read(pais, "petroleo").GetType()));
            Set(pais, "energia", Convert.ChangeType(1000, Read(pais, "energia").GetType()));
            Set(pais, "deficitComida", Convert.ChangeType(0, Read(pais, "deficitComida").GetType()));
            Set(pais, "deficitPetroleo", Convert.ChangeType(0, Read(pais, "deficitPetroleo").GetType()));
            Set(pais, "deficitEnergia", Convert.ChangeType(0, Read(pais, "deficitEnergia").GetType()));

            DefinirEstoque(2, idDiesel, 0d);
            DefinirEstoque(2, idAco, 1000d);
            DefinirEstoque(2, idCombustivelAviacao, 2000d);
            DefinirEstoque(2, idCombustivelNaval, 2000d);

            Component brain = brainRoot.AddComponent(TypeOf("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            object context = Activator.CreateInstance(TypeOf("Hegemonia.AI.BrainMaster.IA_Context"));
            object forca = Activator.CreateInstance(TypeOf("Hegemonia.AI.BrainMaster.IA_ForceSnapshot"));
            Set(forca, "TankUnits", 1);
            Set(context, "ForceSnapshot", forca);
            brain.GetType().GetProperty("Context", Members).GetSetMethod(true).Invoke(brain, new[] { context });

            object economia = Activator.CreateInstance(TypeOf("Hegemonia.AI.IA03.GestorEconomiaIA03"));
            Type resumoType = TypeOf("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
            object[] argumentos = { governo, brain, perfil, 2, true, Activator.CreateInstance(resumoType) };
            MethodInfo aplicar = economia.GetType().GetMethod("TentarAplicarPesos", Members);
            Assert.That(aplicar.Invoke(economia, argumentos), Is.EqualTo(true));

            object resumo = argumentos[5];
            Assert.That(Field(resumo, "CombustivelTerrestre"), Is.EqualTo(0));
            Assert.That(Field(resumo, "CombustivelOperacionalBaixo"), Is.EqualTo(true));
            Assert.That(Field(resumo, "EstoqueEssencialBaixo"), Is.EqualTo(true));

            DefinirEstoque(2, idDiesel, 1500d);
            argumentos[5] = Activator.CreateInstance(resumoType);
            Assert.That(aplicar.Invoke(economia, argumentos), Is.EqualTo(true));
            Assert.That(Field(argumentos[5], "CombustivelOperacionalBaixo"), Is.EqualTo(false));
        }
        finally
        {
            for (int i = 0; i < campos.Length; i++) Set(pais, campos[i], valoresAntes[i]);
            UnityEngine.Object.DestroyImmediate(perfil);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator BrainMasterMarketDemandSelectsEachMissingMilitaryFuelByProductId()
    {
        object pais = Invoke(governo, "ObterPais", 2);
        string[] campos = { "saldo", "comida", "petroleo", "energia", "aco", "armamentos", "emGuerra" };
        object[] valoresAntes = new object[campos.Length];
        for (int i = 0; i < campos.Length; i++) valoresAntes[i] = Read(pais, campos[i]);

        object economiaPais = null;
        string[] camposDeficit = { "deficitEnergia", "deficitComida", "deficitPetroleo" };
        object[] deficitsAntes = null;
        try
        {
            Set(pais, "saldo", Convert.ChangeType(20000, Read(pais, "saldo").GetType()));
            Set(pais, "comida", Convert.ChangeType(1800, Read(pais, "comida").GetType()));
            Set(pais, "petroleo", Convert.ChangeType(5000, Read(pais, "petroleo").GetType()));
            Set(pais, "energia", Convert.ChangeType(1000, Read(pais, "energia").GetType()));
            Set(pais, "aco", Convert.ChangeType(1000, Read(pais, "aco").GetType()));
            Set(pais, "armamentos", Convert.ChangeType(2000, Read(pais, "armamentos").GetType()));
            Set(pais, "emGuerra", true);
            DefinirEstoque(2, idAco, 1000d);

            Type economiaSistemaType = TypeOf("SistemaEconomiaImoveis");
            object economiaSistema = GetStatic(economiaSistemaType, "Instancia");
            if (economiaSistema != null)
            {
                economiaPais = Invoke(economiaSistema, "ObterEconomia", 2);
                if (economiaPais != null)
                {
                    deficitsAntes = new object[camposDeficit.Length];
                    for (int i = 0; i < camposDeficit.Length; i++)
                    {
                        deficitsAntes[i] = Read(economiaPais, camposDeficit[i]);
                        Set(economiaPais, camposDeficit[i], Convert.ChangeType(0, deficitsAntes[i].GetType()));
                    }
                }
            }

            DefinirEstoque(2, idDiesel, 0d);
            DefinirEstoque(2, idCombustivelAviacao, 0d);
            DefinirEstoque(2, idCombustivelNaval, 2000d);
            Type diretorType = TypeOf("Hegemonia.AI.BrainMaster.IA_EconomyDirector");
            MethodInfo resolver = diretorType.GetMethod("ResolveCriticalNeed", StaticMembers);
            Assert.That(resolver.Invoke(null, new[] { pais }).ToString(), Is.EqualTo("Diesel"));

            DefinirEstoque(2, idDiesel, 1500d);
            Assert.That(resolver.Invoke(null, new[] { pais }).ToString(), Is.EqualTo("CombustivelAviacao"));

            DefinirEstoque(2, idCombustivelAviacao, 1500d);
            DefinirEstoque(2, idCombustivelNaval, 0d);
            Assert.That(resolver.Invoke(null, new[] { pais }).ToString(), Is.EqualTo("CombustivelNaval"));
        }
        finally
        {
            for (int i = 0; i < campos.Length; i++) Set(pais, campos[i], valoresAntes[i]);
            if (economiaPais != null && deficitsAntes != null)
            {
                for (int i = 0; i < camposDeficit.Length; i++) Set(economiaPais, camposDeficit[i], deficitsAntes[i]);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator ProducedTankerPaysInitialTankAndCargoOnceAndLaterConfigurationCannotRefillForFree()
    {
        DefinirEstoque(1, idDiesel, 120d);
        GameObject caminhao = CriarCaminhaoProduzidoDeTeste();
        Component combustivel = caminhao.GetComponent(TypeOf("CombustivelUnidade"));

        float cargaPaga = Convert.ToSingle(CallStatic(TypeOf("ServicoAbastecimento"), "AbastecerUnidadeProduzida", caminhao));
        Assert.That(cargaPaga, Is.EqualTo(120f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(combustivel, "combustivelAtual")), Is.EqualTo(100f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(caminhao.GetComponent(TypeOf("CaminhaoTanqueAbastecimento")), "cargaAtual")), Is.EqualTo(20f).Within(0.01f));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(0d).Within(0.01d));

        Invoke(combustivel, "ConfigurarSeNecessario", true);
        Assert.That(Convert.ToSingle(Read(combustivel, "combustivelAtual")), Is.EqualTo(100f).Within(0.01f),
            "Um Garantir/configuração tardia não pode repor a carga já liquidada.");
        Assert.That(Convert.ToSingle(CallStatic(TypeOf("ServicoAbastecimento"), "AbastecerUnidadeProduzida", caminhao)), Is.EqualTo(0f).Within(0.01f),
            "A mesma unidade não deve pagar nem receber carga inicial duas vezes.");

        yield return null;
        Assert.That(Convert.ToSingle(Read(combustivel, "combustivelAtual")), Is.EqualTo(100f).Within(0.01f),
            "O Start do tanque não deve reabastecer gratuitamente após a cobrança inicial.");
        Assert.That(Convert.ToSingle(Read(caminhao.GetComponent(TypeOf("CaminhaoTanqueAbastecimento")), "cargaAtual")), Is.EqualTo(20f).Within(0.01f));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(0d).Within(0.01d));
    }

    [UnityTest]
    public IEnumerator ProducedUnitWithNoNationalFuelStaysEmptyAndInoperable()
    {
        DefinirEstoque(1, idDiesel, 0d);
        GameObject unidade = CriarCaminhaoProduzidoDeTeste();
        Component combustivel = unidade.GetComponent(TypeOf("CombustivelUnidade"));

        float abastecidoDoEstoque = Convert.ToSingle(CallStatic(
            TypeOf("ServicoAbastecimento"), "AbastecerUnidadeProduzida", unidade));

        Assert.That(abastecidoDoEstoque, Is.EqualTo(0f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(combustivel, "combustivelAtual")), Is.EqualTo(0f).Within(0.01f),
            "Sem estoque refinado, a carga inicial configurada no prefab não pode contornar o abastecimento nacional pago.");
        Assert.That(ReadProperty(combustivel, "PodeOperar"), Is.EqualTo(false),
            "A unidade sem combustível nacional disponível deve permanecer inoperável até ser abastecida.");

        yield return null;

        Assert.That(Convert.ToSingle(Read(combustivel, "combustivelAtual")), Is.EqualTo(0f).Within(0.01f),
            "O Start do componente não deve repor gratuitamente a carga removida antes da produção paga.");
        Assert.That(ReadProperty(combustivel, "PodeOperar"), Is.EqualTo(false));
        Assert.That(Quantidade(1, idDiesel), Is.EqualTo(0d).Within(0.01d),
            "A falta de combustível não deve alterar o estoque nacional nem conceder carga grátis.");
    }

    [UnityTest]
    public IEnumerator RestoringSavedFuelDoesNotRefillTheTankOnStartOrLateConfiguration()
    {
        const string prefabKey = "Fuel_Save_Restore_Test";
        const string uniqueId = "fuel-save-restore-test-unit";

        GameObject prefab = new GameObject(prefabKey);
        prefab.SetActive(false);
        objects.Add(prefab);

        Component identity = prefab.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        Set(identity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Veiculo"));
        Component prefabFuel = prefab.AddComponent(TypeOf("CombustivelUnidade"));
        Set(prefabFuel, "classe", Enum.Parse(TypeOf("ClasseCombustivelUnidade"), "Terrestre"));
        Set(prefabFuel, "usaCombustivel", true);
        Set(prefabFuel, "preencherAoIniciar", true);
        Set(prefabFuel, "capacidade", 100f);
        Set(prefabFuel, "combustivelAtual", -1f);

        GameObject saveObject = new GameObject("Fuel_Save_Restore_Test_System");
        saveObject.SetActive(false);
        objects.Add(saveObject);
        Component saveSystem = saveObject.AddComponent(TypeOf("SistemaSaveGame"));
        Set(saveSystem, "dadosAtuais", Activator.CreateInstance(TypeOf("DadosDoJogo")));

        IDictionary prefabs = (IDictionary)Read(saveSystem, "prefabsPorChaveSave");
        prefabs.Add(prefabKey, prefab);
        object saved = Activator.CreateInstance(TypeOf("SaveEntityData"));
        Set(saved, "uniqueId", uniqueId);
        Set(saved, "prefabKey", prefabKey);
        Set(saved, "nomeCena", prefabKey);
        Set(saved, "ativo", true);
        Set(saved, "posicao", Activator.CreateInstance(TypeOf("SaveVector3"), Vector3.zero));
        Set(saved, "rotacao", Activator.CreateInstance(TypeOf("SaveQuaternion"), Quaternion.identity));
        Set(saved, "escala", Activator.CreateInstance(TypeOf("SaveVector3"), Vector3.one));
        Set(saved, "teamID", 1);
        Set(saved, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Veiculo"));
        Set(saved, "possuiCombustivel", true);
        Set(saved, "capacidadeCombustivel", 100f);
        Set(saved, "combustivelAtual", 17f);

        object operationalSaved = Activator.CreateInstance(TypeOf("SaveEntityData"));
        Set(operationalSaved, "uniqueId", uniqueId + "-operational");
        Set(operationalSaved, "prefabKey", prefabKey);
        Set(operationalSaved, "nomeCena", prefabKey + "_Operational");
        Set(operationalSaved, "escala", Activator.CreateInstance(TypeOf("SaveVector3"), Vector3.one));
        Set(operationalSaved, "teamID", 1);
        Set(operationalSaved, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Veiculo"));
        Set(operationalSaved, "possuiCombustivel", true);
        Set(operationalSaved, "capacidadeCombustivel", 100f);
        Set(operationalSaved, "combustivelAtual", 17f);

        Invoke(saveSystem, "InstanciarEntidadeSalva", saved);
        IDictionary saveables = (IDictionary)Read(saveSystem, "saveablesPorId");
        Component restoredSaveable = (Component)saveables[uniqueId];
        Assert.That(restoredSaveable, Is.Not.Null);
        objects.Add(restoredSaveable.gameObject);

        Component restoredFuel = restoredSaveable.GetComponent(TypeOf("CombustivelUnidade"));
        Assert.That(restoredFuel, Is.Not.Null);

        GameObject restoredOperational = (GameObject)Invoke(
            saveSystem,
            "MaterializarEntidadeOperacional",
            operationalSaved,
            Vector3.zero,
            Quaternion.identity);
        Assert.That(restoredOperational, Is.Not.Null);
        objects.Add(restoredOperational);
        restoredOperational.SetActive(true);
        Component operationalFuel = restoredOperational.GetComponent(TypeOf("CombustivelUnidade"));
        Assert.That(operationalFuel, Is.Not.Null);

        yield return null;
        yield return null;

        Assert.That(Convert.ToSingle(Read(restoredFuel, "combustivelAtual")), Is.EqualTo(17f).Within(0.01f),
            "Start não deve substituir o combustível salvo pelo tanque cheio do prefab.");
        Assert.That(Convert.ToSingle(Read(operationalFuel, "combustivelAtual")), Is.EqualTo(17f).Within(0.01f),
            "A materialização operacional também deve preservar o combustível salvo no Start.");

        Invoke(restoredFuel, "ConfigurarSeNecessario", true);
        Invoke(operationalFuel, "ConfigurarSeNecessario", true);
        Assert.That(Convert.ToSingle(Read(restoredFuel, "combustivelAtual")), Is.EqualTo(17f).Within(0.01f),
            "Uma configuração tardia do ControleUnidade não deve repor combustível sem custo.");
        Assert.That(Convert.ToSingle(Read(operationalFuel, "combustivelAtual")), Is.EqualTo(17f).Within(0.01f),
            "A configuração tardia também não deve recarregar uma unidade materializada.");
    }

    [UnityTest]
    public IEnumerator BrainMasterNavalProductionUsesTreasuryCrewAndNationalFuelUntilShipLaunches()
    {
        const int teamId = 2941927;
        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        Type identityType = TypeOf("IdentidadeUnidade");
        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type menuType = TypeOf("MenuConstrucao");
        FieldInfo sharedCatalogField = menuType.GetField("catalogoGlobal", StaticMembers);
        Assert.That(sharedCatalogField, Is.Not.Null);
        object previousSharedCatalog = sharedCatalogField.GetValue(null);
        object previousFinancialReserve = Invoke(governo, "ObterReservaFinanceiraEstrategica", teamId);
        IList countries = (IList)Read(governo, "paises");
        object testCountry = null;
        GameObject yardRoot = null;
        GameObject brainRoot = null;
        GameObject producedShip = null;
        EconomyScheduleSnapshot economySchedule = default(EconomyScheduleSnapshot);
        long unitCost = 0L;
        int requiredCrew = 0;
        double navalFuelBefore = 0d;
        string productionOrderId = string.Empty;

        try
        {
            economySchedule = CongelarEconomiaGlobal(governo);
            testCountry = Activator.CreateInstance(countryType);
            Set(testCountry, "teamId", teamId);
            Set(testCountry, "saldo", 0L);
            Set(testCountry, "alistaveis", 0);
            Set(testCountry, "populacaoCivil", 0);
            Set(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);
            Invoke(industrial, "GarantirPaisExistentes");
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, 0L);

            // Force the same Editor asset catalog fallback that BrainMaster uses
            // when the active scene has no MenuConstrucao.
            sharedCatalogField.SetValue(null, null);
            object backend = Activator.CreateInstance(backendType, new object[] { teamId });
            Invoke(backend, "RefreshCatalog");
            IEnumerable catalog = (IEnumerable)backendType.GetProperty("Catalog", Members).GetValue(backend, null);

            object navalItem = null;
            GameObject navalPrefab = null;
            float configuredTankCapacity = 0f;
            foreach (object item in catalog)
            {
                if (item == null || !string.Equals(Read(item, "categoria").ToString(), "Marinha", StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject prefab = TypeOf("DadosConstrucao")
                    .GetProperty("PrefabDaUnidade", Members).GetValue(item, null) as GameObject;
                if (prefab == null)
                {
                    continue;
                }

                bool hasNavalController = prefab.GetComponentInChildren(TypeOf("ControleNavioRealista"), true) != null
                    || prefab.GetComponentInChildren(TypeOf("ControleSubmarino"), true) != null;
                Component fuel = prefab.GetComponentInChildren(TypeOf("CombustivelUnidade"), true);
                if (!hasNavalController || fuel == null || !string.Equals(Read(fuel, "classe").ToString(), "Naval", StringComparison.Ordinal))
                {
                    continue;
                }

                long price = Convert.ToInt64(Invoke(item, "ObterPrecoEfetivo"));
                int crew = Convert.ToInt32(CallStatic(governo.GetType(), "ObterEfetivoNecessario", prefab));
                float capacity = Convert.ToSingle(fuel.GetType().GetProperty("Capacidade", Members).GetValue(fuel, null));
                if (price <= 0L || crew <= 0 || capacity <= 0f)
                {
                    continue;
                }

                if (navalItem == null || price < unitCost)
                {
                    navalItem = item;
                    navalPrefab = prefab;
                    unitCost = price;
                    requiredCrew = crew;
                    configuredTankCapacity = capacity;
                }
            }

            Assert.That(navalItem, Is.Not.Null,
                "O catálogo atual precisa conter um navio real com preço legado, tripulação central e tanque naval para validar a rota sem inventar valores.");

            long balanceBefore = unitCost + 1000000L;
            Set(testCountry, "saldo", balanceBefore);
            double navalFuelStock = Math.Max(1d, configuredTankCapacity * 4d);
            DefinirEstoque(teamId, idCombustivelNaval, navalFuelStock);
            navalFuelBefore = Quantidade(teamId, idCombustivelNaval);
            Assert.That(navalFuelBefore, Is.GreaterThan(0d));

            yardRoot = new GameObject("BrainMaster naval economic integration yard");
            yardRoot.SetActive(false);
            Component yardIdentity = yardRoot.AddComponent(identityType);
            Set(yardIdentity, "teamID", teamId);
            Set(yardIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Estrutura"));
            Component yard = yardRoot.AddComponent(TypeOf("Estaleiro"));
            yard.GetType().GetProperty("OwnerTeamId", Members).SetValue(yard, teamId, null);
            // Shorten only this test fixture's existing yard timer; no unit recipe
            // or production time asset is changed.
            Set(yard, "tempoDeConstrucao", 0.05f);
            yardRoot.SetActive(true);
            yield return null;
            Assert.That(yard.GetType().GetProperty("PossuiEnergiaOperacional", Members).GetValue(yard, null), Is.EqualTo(true));

            brainRoot = new GameObject("BrainMaster naval economic integration brain");
            brainRoot.SetActive(false);
            Component brain = brainRoot.AddComponent(TypeOf("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            Set(brain, "TeamId", teamId);
            Set(brain, "Credits", 0);
            Invoke(brain, "TickEconomy", 1f);

            Type worldStateType = TypeOf("Hegemonia.AI.BrainMaster.IA_WorldState");
            object worldState = Activator.CreateInstance(worldStateType, new object[] { teamId });
            Type contextType = TypeOf("Hegemonia.AI.BrainMaster.IA_Context");
            object context = Activator.CreateInstance(contextType);
            Set(context, "Brain", brain);
            Set(context, "Backend", backend);
            Set(context, "WorldState", worldState);
            Type commandServiceType = TypeOf("Hegemonia.AI.BrainMaster.CommandService");
            object commandService = Activator.CreateInstance(commandServiceType, new[] { backend });
            MethodInfo executeCommand = commandServiceType.GetMethod("Execute", Members);
            Assert.That(executeCommand, Is.Not.Null, "O dispatcher público de comandos do BrainMaster deve estar disponível.");

            object blockedRequest = CriarPedidoProducaoNaval(Invoke(navalItem, "GetDisplayName").ToString());
            object[] blockedArguments = { blockedRequest, context, null };
            bool startedWithoutCrew = (bool)executeCommand.Invoke(commandService, blockedArguments);
            Assert.That(startedWithoutCrew, Is.False, "Sem manpower, o estaleiro deve recusar a fila naval.");
            Assert.That(Convert.ToInt64(Read(testCountry, "saldo")), Is.EqualTo(balanceBefore),
                "A cobrança do BrainMaster deve ser estornada quando o pedido naval não entra na fila.");
            Assert.That(Quantidade(teamId, idCombustivelNaval), Is.EqualTo(navalFuelBefore).Within(0.01d));

            Set(testCountry, "alistaveis", requiredCrew);
            Invoke(brain, "TickEconomy", 1f);
            object acceptedRequest = CriarPedidoProducaoNaval(Invoke(navalItem, "GetDisplayName").ToString());
            object[] acceptedArguments = { acceptedRequest, context, null };
            bool queued = (bool)executeCommand.Invoke(commandService, acceptedArguments);
            Assert.That(queued, Is.True, Convert.ToString(acceptedArguments[2]));
            productionOrderId = Convert.ToString(Read(Read(acceptedRequest, "Payload"), "ProductionOrderId"));
            Assert.That(productionOrderId, Is.Not.Empty, "A ordem aceita deve ser vinculada à construção no estaleiro.");
            long balanceAfterOrder = Convert.ToInt64(Read(testCountry, "saldo"));
            Assert.That(balanceAfterOrder, Is.EqualTo(balanceBefore - unitCost),
                "O preço monetário efetivo da ficha deve ser debitado uma vez do tesouro nacional.");

            // Simulate another mobilization consuming the last crew after this
            // accepted, paid order. The shipyard must retain the order, not spawn
            // freely or discard the paid work.
            Set(testCountry, "alistaveis", 0);
            yield return new WaitForSecondsRealtime(0.2f);
            Array slots = (Array)Read(yard, "slots");
            object occupiedSlot = null;
            foreach (object slot in slots)
            {
                if (slot != null && Convert.ToBoolean(Read(slot, "estaOcupado")))
                {
                    occupiedSlot = slot;
                    break;
                }
            }

            Assert.That(occupiedSlot, Is.Not.Null, "Sem tripulação no lançamento, o navio pago deve permanecer no slot do estaleiro.");
            Assert.That(Convert.ToString(Read(occupiedSlot, "productionOrderId")), Is.EqualTo(productionOrderId),
                "O slot pronto deve continuar apontando para a mesma ordem paga enquanto aguarda efetivo.");
            Assert.That(Convert.ToInt64(Read(testCountry, "saldo")), Is.EqualTo(balanceAfterOrder),
                "A ordem aceita não deve ser cobrada novamente nem perder o saldo enquanto aguarda manpower.");
            Assert.That(GameObject.Find(navalPrefab.name + "(Clone)"), Is.Null,
                "Não deve existir spawn naval sem efetivo mobilizável.");

            Type productionRegistryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
            MethodInfo cleanupReservations = productionRegistryType.GetMethod("Cleanup", StaticMembers);
            Assert.That(cleanupReservations, Is.Not.Null);
            cleanupReservations.Invoke(null, new object[] { Time.time + 181f });
            Assert.That(Convert.ToBoolean(Read(occupiedSlot, "estaOcupado")), Is.True,
                "A simulação do TTL não deve alterar o slot físico ainda mantido pelo estaleiro.");
            string activeOrderAfterTtl = (string)productionRegistryType.GetMethod("FindActiveOrder", StaticMembers)
                .Invoke(null, new object[] { teamId, "Naval", "command:Naval production economy PlayMode test" });
            Assert.That(activeOrderAfterTtl, Is.EqualTo(productionOrderId),
                "A reserva Constructing não pode expirar enquanto a construção paga continua ocupando o estaleiro.");

            Set(testCountry, "alistaveis", requiredCrew);
            yield return new WaitForSecondsRealtime(0.2f);
            producedShip = GameObject.Find(navalPrefab.name + "(Clone)");
            Assert.That(producedShip, Is.Not.Null, "O estaleiro deve liberar a unidade real quando o manpower volta a estar disponível.");
            objects.Add(producedShip);

            Component producedIdentity = producedShip.GetComponent(identityType);
            Assert.That(Convert.ToInt32(Read(producedIdentity, "teamID")), Is.EqualTo(teamId));
            Assert.That(Read(producedIdentity, "tipoUnidade").ToString(), Is.EqualTo("Naval"));
            Assert.That(Convert.ToInt32(Read(producedIdentity, "militaresConsumidos")), Is.EqualTo(requiredCrew));
            Assert.That(Convert.ToInt32(Read(testCountry, "alistaveis")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(Read(testCountry, "populacaoMilitarAtiva")), Is.EqualTo(requiredCrew));

            Component producedFuel = producedShip.GetComponentInChildren(TypeOf("CombustivelUnidade"), true);
            Assert.That(producedFuel, Is.Not.Null);
            float loadedNavalFuel = Convert.ToSingle(Read(producedFuel, "combustivelAtual"));
            double navalFuelAfter = Quantidade(teamId, idCombustivelNaval);
            Assert.That(loadedNavalFuel, Is.GreaterThan(0f), "O tanque inicial deve vir do estoque nacional refinado.");
            Assert.That(navalFuelBefore - navalFuelAfter, Is.EqualTo(loadedNavalFuel).Within(0.1d),
                "A quantidade carregada no navio deve ser debitada do combustível naval do mesmo país.");
            Assert.That(Convert.ToInt64(Read(testCountry, "saldo")), Is.EqualTo(balanceAfterOrder),
                "A conclusão física não deve cobrar o preço da ordem uma segunda vez.");
            string activeOrderAfterLaunch = (string)TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry")
                .GetMethod("FindActiveOrder", StaticMembers)
                .Invoke(null, new object[] { teamId, "Naval", "command:Naval production economy PlayMode test" });
            Assert.That(activeOrderAfterLaunch, Is.Empty,
                "Depois de lançar o navio, a ordem antiga deve sair da demanda ativa.");
        }
        finally
        {
            if (producedShip != null)
            {
                Component producedIdentity = producedShip.GetComponent(identityType);
                if (producedIdentity != null) Set(producedIdentity, "militaresConsumidos", 0);
                UnityEngine.Object.DestroyImmediate(producedShip);
            }
            if (yardRoot != null) UnityEngine.Object.DestroyImmediate(yardRoot);
            if (brainRoot != null) UnityEngine.Object.DestroyImmediate(brainRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, previousFinancialReserve);
            RestaurarEconomiaGlobal(governo, economySchedule);
            sharedCatalogField.SetValue(null, previousSharedCatalog);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator BrainMasterProductionRejectsMissingWorldStateBeforeChargingOrQueuing()
    {
        const int teamId = 2952781;
        Type governmentType = TypeOf("SistemaGovernoMundial");
        Type countryType = TypeOf("DadosPaisGoverno");
        Type identityType = TypeOf("IdentidadeUnidade");
        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object previousFinancialReserve = Invoke(governo, "ObterReservaFinanceiraEstrategica", teamId);
        IList countries = (IList)Read(governo, "paises");
        object testCountry = null;
        GameObject yardRoot = null;
        GameObject brainRoot = null;
        ScriptableObject navalItem = null;
        EconomyScheduleSnapshot economySchedule = default(EconomyScheduleSnapshot);
        long unitCost = 0L;
        int requiredCrew = 0;
        string productionOrderId = string.Empty;

        try
        {
            economySchedule = CongelarEconomiaGlobal(governo);
            testCountry = Activator.CreateInstance(countryType);
            Set(testCountry, "teamId", teamId);
            Set(testCountry, "saldo", 1000000L);
            Set(testCountry, "alistaveis", 5000);
            Set(testCountry, "populacaoCivil", 10000);
            Set(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);
            Invoke(industrial, "GarantirPaisExistentes");
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, 0L);

            // Load one authored prefab directly. Avoid RefreshCatalog's full
            // AssetDatabase scan so this regression stays quick and isolated.
            Type assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(assetDatabaseType, Is.Not.Null, "O teste PlayMode deve rodar no Editor para carregar o prefab real.");
            MethodInfo loadPrefab = Array.Find(assetDatabaseType.GetMethods(StaticMembers), method =>
                method.Name == "LoadAssetAtPath" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1);
            Assert.That(loadPrefab, Is.Not.Null);
            GameObject navalPrefab = loadPrefab.MakeGenericMethod(typeof(GameObject)).Invoke(null, new object[]
            {
                "Assets/Prefabs/Navios_Guerra/costeira guarda/Nav Guard costeira.prefab"
            }) as GameObject;
            Assert.That(navalPrefab, Is.Not.Null, "A fixture precisa carregar o prefab naval real.");
            Assert.That(navalPrefab.GetComponentInChildren(TypeOf("ControleNavioRealista"), true), Is.Not.Null);
            Component navalFuel = navalPrefab.GetComponentInChildren(TypeOf("CombustivelUnidade"), true);
            Assert.That(navalFuel, Is.Not.Null);
            Assert.That(Read(navalFuel, "classe").ToString(), Is.EqualTo("Naval"));

            Type constructionType = TypeOf("DadosConstrucao");
            navalItem = ScriptableObject.CreateInstance(constructionType);
            constructionType.GetProperty("NomeItem", Members).SetValue(navalItem, "IA03 rollback test naval unit", null);
            constructionType.GetProperty("PrefabDaUnidade", Members).SetValue(navalItem, navalPrefab, null);
            Set(navalItem, "categoria", Enum.Parse(constructionType.GetNestedType("CategoriaItem"), "Marinha"));
            Set(navalItem, "precoDefinitivo", 1250L);
            unitCost = Convert.ToInt64(Invoke(navalItem, "ObterPrecoEfetivo"));
            requiredCrew = Convert.ToInt32(CallStatic(governmentType, "ObterEfetivoNecessario", navalPrefab));
            Assert.That(unitCost, Is.GreaterThan(0L));
            Assert.That(requiredCrew, Is.GreaterThan(0));
            Set(testCountry, "saldo", unitCost + 1000000L);
            Set(testCountry, "alistaveis", requiredCrew);

            object backend = Activator.CreateInstance(backendType, new object[] { teamId });
            backendType.GetMethod("AddCatalogItem", Members).Invoke(backend, new object[] { navalItem });
            string itemKey = Invoke(navalItem, "GetDisplayName").ToString();
            long balanceBefore = Convert.ToInt64(Read(testCountry, "saldo"));

            yardRoot = new GameObject("BrainMaster missing world-state test yard");
            yardRoot.SetActive(false);
            Component yardIdentity = yardRoot.AddComponent(identityType);
            Set(yardIdentity, "teamID", teamId);
            Set(yardIdentity, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Estrutura"));
            Component yard = yardRoot.AddComponent(TypeOf("Estaleiro"));
            yard.GetType().GetProperty("OwnerTeamId", Members).SetValue(yard, teamId, null);
            Set(yard, "tempoDeConstrucao", 1f);
            yardRoot.SetActive(true);
            yield return null;
            Assert.That(yard.GetType().GetProperty("PossuiEnergiaOperacional", Members).GetValue(yard, null), Is.EqualTo(true));

            brainRoot = new GameObject("BrainMaster missing world-state test brain");
            brainRoot.SetActive(false);
            Component brain = brainRoot.AddComponent(TypeOf("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            Set(brain, "TeamId", teamId);
            Set(brain, "Credits", 0);
            Invoke(brain, "TickEconomy", 1f);

            Type contextType = TypeOf("Hegemonia.AI.BrainMaster.IA_Context");
            object context = Activator.CreateInstance(contextType);
            Set(context, "Brain", brain);
            Set(context, "Backend", backend);
            Type commandServiceType = TypeOf("Hegemonia.AI.BrainMaster.CommandService");
            object commandService = Activator.CreateInstance(commandServiceType, new[] { backend });
            MethodInfo executeCommand = commandServiceType.GetMethod("Execute", Members);
            object request = CriarPedidoProducaoNaval(itemKey);
            Exception commandException = null;
            bool accepted = false;
            object[] commandArgs = { request, context, null };
            try
            {
                accepted = (bool)executeCommand.Invoke(commandService, commandArgs);
            }
            catch (TargetInvocationException exception)
            {
                commandException = exception.InnerException ?? exception;
            }

            Assert.That(commandException, Is.Null,
                "Um contexto sem WorldState deve ser recusado antes que a ordem seja enviada ao estaleiro.");
            Assert.That(accepted, Is.False, Convert.ToString(commandArgs[2]));
            Assert.That(Convert.ToInt64(Read(testCountry, "saldo")), Is.EqualTo(balanceBefore),
                "O BrainMaster deve estornar o débito quando a produção não pode ser executada.");
            Assert.That(Convert.ToInt32(Read(testCountry, "alistaveis")), Is.EqualTo(requiredCrew));
            Assert.That(Convert.ToInt32(Read(testCountry, "populacaoMilitarAtiva")), Is.Zero);

            Array slots = (Array)Read(yard, "slots");
            foreach (object slot in slots)
                Assert.That(Convert.ToBoolean(Read(slot, "estaOcupado")), Is.False,
                    "A validação deve acontecer antes de reservar uma vaga no estaleiro.");

            Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
            MethodInfo findActive = registryType.GetMethod("FindActiveOrder", StaticMembers);
            productionOrderId = (string)findActive.Invoke(null, new object[] { teamId, "Naval", "command:legacy" });
            Assert.That(productionOrderId, Is.Empty, "A reserva de produção inválida deve ser liberada.");
        }
        finally
        {
            Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
            if (string.IsNullOrEmpty(productionOrderId))
            {
                productionOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                    .Invoke(null, new object[] { teamId, "Naval", "command:legacy" });
            }
            if (!string.IsNullOrEmpty(productionOrderId))
                registryType.GetMethod("Release", StaticMembers).Invoke(null, new object[] { productionOrderId, Time.time });
            if (yardRoot != null) UnityEngine.Object.DestroyImmediate(yardRoot);
            if (brainRoot != null) UnityEngine.Object.DestroyImmediate(brainRoot);
            if (navalItem != null) UnityEngine.Object.DestroyImmediate(navalItem);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, previousFinancialReserve);
            RestaurarEconomiaGlobal(governo, economySchedule);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator BrainMasterProductionRejectsMissingContextWithoutLeakingReservation()
    {
        const int teamId = 2952782;
        const string purpose = "command:Naval production economy PlayMode test";
        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type constructionType = TypeOf("DadosConstrucao");
        Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        GameObject prefab = null;
        ScriptableObject navalItem = null;
        string activeOrderId = string.Empty;

        try
        {
            prefab = new GameObject("Missing context naval production fixture");
            prefab.SetActive(false);
            navalItem = ScriptableObject.CreateInstance(constructionType);
            constructionType.GetProperty("NomeItem", Members).SetValue(navalItem, "Missing context naval production fixture", null);
            constructionType.GetProperty("PrefabDaUnidade", Members).SetValue(navalItem, prefab, null);
            Set(navalItem, "categoria", Enum.Parse(constructionType.GetNestedType("CategoriaItem"), "Marinha"));
            Set(navalItem, "precoDefinitivo", 1250L);

            object backend = Activator.CreateInstance(backendType, new object[] { teamId });
            backendType.GetMethod("AddCatalogItem", Members).Invoke(backend, new object[] { navalItem });
            string itemKey = Invoke(navalItem, "GetDisplayName").ToString();
            object commandService = Activator.CreateInstance(TypeOf("Hegemonia.AI.BrainMaster.CommandService"), new[] { backend });
            object request = CriarPedidoProducaoNaval(itemKey);
            object[] args = { request, null, null };
            Exception commandException = null;
            bool accepted = false;

            try
            {
                accepted = (bool)commandService.GetType().GetMethod("Execute", Members).Invoke(commandService, args);
            }
            catch (TargetInvocationException exception)
            {
                commandException = exception.InnerException ?? exception;
            }

            Assert.That(commandException, Is.Null,
                "Um contexto ausente deve ser rejeitado antes de reservar ou cobrar a produção.");
            Assert.That(accepted, Is.False, Convert.ToString(args[2]));
            activeOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                .Invoke(null, new object[] { teamId, "Naval", purpose });
            Assert.That(activeOrderId, Is.Empty,
                "A recusa por contexto ausente não pode deixar uma reserva automática ativa.");
        }
        finally
        {
            if (string.IsNullOrEmpty(activeOrderId))
            {
                activeOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                    .Invoke(null, new object[] { teamId, "Naval", purpose });
            }
            if (!string.IsNullOrEmpty(activeOrderId))
                registryType.GetMethod("Release", StaticMembers).Invoke(null, new object[] { activeOrderId, Time.time });
            if (navalItem != null) UnityEngine.Object.DestroyImmediate(navalItem);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator BrainMasterProductionExceptionBeforeProducerAcceptanceRefundsAndReleasesOrder()
    {
        const int teamId = 2952783;
        const long startingBalance = 10000L;
        const long unitCost = 1250L;
        Type countryType = TypeOf("DadosPaisGoverno");
        Type brainType = TypeOf("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type backendType = TypeOf("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type productionServiceType = TypeOf("Hegemonia.AI.BrainMaster.ProductionService");
        Type registryType = TypeOf("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        Type constructionType = TypeOf("DadosConstrucao");
        IList countries = (IList)Read(governo, "paises");
        object previousFinancialReserve = Invoke(governo, "ObterReservaFinanceiraEstrategica", teamId);
        object country = null;
        GameObject brainRoot = null;
        GameObject prefab = null;
        ScriptableObject item = null;
        string activeOrderId = string.Empty;

        try
        {
            country = Activator.CreateInstance(countryType);
            Set(country, "teamId", teamId);
            Set(country, "saldo", startingBalance);
            Set(country, "alistaveis", 1000);
            Set(country, "populacaoCivil", 10000);
            countries.Add(country);
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, 0L);

            prefab = new GameObject("BrainMaster exception-before-producer naval fixture");
            prefab.SetActive(false);
            item = ScriptableObject.CreateInstance(constructionType);
            constructionType.GetProperty("NomeItem", Members).SetValue(item, prefab.name, null);
            constructionType.GetProperty("PrefabDaUnidade", Members).SetValue(item, prefab, null);
            Set(item, "categoria", Enum.Parse(constructionType.GetNestedType("CategoriaItem"), "Marinha"));
            Set(item, "precoDefinitivo", unitCost);

            object backend = Activator.CreateInstance(backendType, new object[] { teamId });
            backendType.GetMethod("AddCatalogItem", Members).Invoke(backend, new object[] { item });
            object productionService = backendType.GetProperty("ProductionService", Members).GetValue(backend, null);
            FieldInfo shipyardBuffer = productionServiceType.GetField("_shipyardBuffer", Members);
            Assert.That(shipyardBuffer, Is.Not.Null);
            object originalBuffer = shipyardBuffer.GetValue(productionService);
            Assert.That(originalBuffer, Is.Not.Null);

            brainRoot = new GameObject("BrainMaster exception-before-producer test brain");
            brainRoot.SetActive(false);
            Component brain = brainRoot.AddComponent(brainType);
            Set(brain, "TeamId", teamId);

            Type contextType = TypeOf("Hegemonia.AI.BrainMaster.IA_Context");
            object context = Activator.CreateInstance(contextType);
            Set(context, "Brain", brain);
            Set(context, "Backend", backend);
            Set(context, "WorldState", Activator.CreateInstance(
                TypeOf("Hegemonia.AI.BrainMaster.IA_WorldState"), new object[] { teamId }));

            object request = CriarPedidoProducaoNaval(Invoke(item, "GetDisplayName").ToString());
            object commandService = backendType.GetProperty("CommandService", Members).GetValue(backend, null);
            MethodInfo execute = commandService.GetType().GetMethod("Execute", Members);
            object[] arguments = { request, context, null };
            Exception commandException = null;
            bool accepted = false;
            long balanceBefore = Convert.ToInt64(Read(country, "saldo"));

            // This simulates a corrupt internal infrastructure cache before any
            // factory, airport, or shipyard can accept a physical production.
            shipyardBuffer.SetValue(productionService, null);
            try
            {
                accepted = (bool)execute.Invoke(commandService, arguments);
            }
            catch (TargetInvocationException exception)
            {
                commandException = exception.InnerException ?? exception;
            }
            finally
            {
                shipyardBuffer.SetValue(productionService, originalBuffer);
            }

            Assert.That(commandException, Is.Null,
                "Uma exceção anterior à aceitação física deve ser convertida em rejeição controlada.");
            Assert.That(accepted, Is.False, Convert.ToString(arguments[2]));
            Assert.That(Convert.ToInt64(Read(country, "saldo")), Is.EqualTo(balanceBefore),
                "A falha prévia à produção não pode consumir o tesouro nacional.");
            activeOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                .Invoke(null, new object[] { teamId, "Naval", "command:Naval production economy PlayMode test" });
            Assert.That(activeOrderId, Is.Empty,
                "A falha prévia à aceitação física deve liberar a reserva da fila.");

            EventInfo governmentUpdated = governo.GetType().GetEvent("OnGovernoAtualizado", Members);
            Assert.That(governmentUpdated, Is.Not.Null);
            Action throwingListener = () =>
                throw new InvalidOperationException("Falha controlada do listener de governo.");
            int laterListenerInvocations = 0;
            Action laterListener = () => laterListenerInvocations++;
            governmentUpdated.AddEventHandler(governo, throwingListener);
            governmentUpdated.AddEventHandler(governo, laterListener);
            try
            {
                object paymentNotificationRequest = CriarPedidoProducaoNaval(Invoke(item, "GetDisplayName").ToString());
                object[] paymentNotificationArguments = { paymentNotificationRequest, context, null };
                Exception paymentNotificationException = null;
                bool acceptedDespiteMissingYard = false;
                long balanceBeforeNotification = Convert.ToInt64(Read(country, "saldo"));

                try
                {
                    acceptedDespiteMissingYard = (bool)execute.Invoke(
                        commandService,
                        paymentNotificationArguments);
                }
                catch (TargetInvocationException exception)
                {
                    paymentNotificationException = exception.InnerException ?? exception;
                }

                Assert.That(paymentNotificationException, Is.Null,
                    "Uma exceção em listener após o débito não deve escapar nem deixar a transação pela metade.");
                Assert.That(acceptedDespiteMissingYard, Is.False,
                    "Sem estaleiro próprio, a ordem deve ser recusada mesmo se um listener falhar.");
                Assert.That(Convert.ToInt64(Read(country, "saldo")), Is.EqualTo(balanceBeforeNotification),
                    "O rollback deve restaurar o saldo mesmo quando um listener do governo falha no débito e no estorno.");
                activeOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                    .Invoke(null, new object[] { teamId, "Naval", "command:Naval production economy PlayMode test" });
                Assert.That(activeOrderId, Is.Empty,
                    "A falha do listener não deve deixar uma reserva BrainMaster ativa.");
                Assert.That(laterListenerInvocations, Is.GreaterThanOrEqualTo(2),
                    "Os listeners após o callback defeituoso também devem receber o débito e o estorno.");
            }
            finally
            {
                governmentUpdated.RemoveEventHandler(governo, laterListener);
                governmentUpdated.RemoveEventHandler(governo, throwingListener);
            }

            Action throwingManpowerListener = () =>
                throw new InvalidOperationException("Falha controlada do listener de efetivo.");
            int laterManpowerListenerInvocations = 0;
            Action laterManpowerListener = () => laterManpowerListenerInvocations++;
            governmentUpdated.AddEventHandler(governo, throwingManpowerListener);
            governmentUpdated.AddEventHandler(governo, laterManpowerListener);
            try
            {
                int activeBeforeMobilization = Convert.ToInt32(Read(country, "populacaoMilitarAtiva"));
                int enlistableBeforeMobilization = Convert.ToInt32(Read(country, "alistaveis"));
                Exception manpowerNotificationException = null;
                bool mobilized = false;

                try
                {
                    mobilized = (bool)Invoke(governo, "TentarMobilizarEfetivo", teamId, 5);
                }
                catch (TargetInvocationException exception)
                {
                    manpowerNotificationException = exception.InnerException ?? exception;
                }

                Assert.That(manpowerNotificationException, Is.Null,
                    "A falha de notificação não deve escapar depois de mobilizar efetivo.");
                Assert.That(mobilized, Is.True);
                Assert.That(Convert.ToInt32(Read(country, "populacaoMilitarAtiva")),
                    Is.EqualTo(activeBeforeMobilization + 5));
                Assert.That(Convert.ToInt32(Read(country, "alistaveis")),
                    Is.EqualTo(enlistableBeforeMobilization - 5));
                Assert.That(laterManpowerListenerInvocations, Is.EqualTo(1),
                    "O listener posterior deve receber a notificação da mobilização.");
            }
            finally
            {
                governmentUpdated.RemoveEventHandler(governo, laterManpowerListener);
                governmentUpdated.RemoveEventHandler(governo, throwingManpowerListener);
            }

            int foodBeforeStockUpdate = Convert.ToInt32(Read(country, "comida"));
            int laterStockListenerInvocations = 0;
            Action throwingStockListener = () =>
                throw new InvalidOperationException("Falha controlada do listener durante alteracao de estoque.");
            Action laterStockListener = () => laterStockListenerInvocations++;
            governmentUpdated.AddEventHandler(governo, throwingStockListener);
            governmentUpdated.AddEventHandler(governo, laterStockListener);
            try
            {
                Exception stockNotificationException = null;
                try
                {
                    Invoke(governo, "AdicionarEstoque", teamId,
                        Enum.Parse(TypeOf("RecursoMercado"), "Comida"), 7);
                }
                catch (TargetInvocationException exception)
                {
                    stockNotificationException = exception.InnerException ?? exception;
                }

                Assert.That(Convert.ToInt32(Read(country, "comida")), Is.EqualTo(foodBeforeStockUpdate + 7),
                    "O estoque deve ser alterado antes da notificação do governo.");
                Assert.That(stockNotificationException, Is.Null,
                    "A falha no listener não deve escapar depois da alteração do estoque.");
                Assert.That(laterStockListenerInvocations, Is.EqualTo(1),
                    "O listener posterior deve receber a atualização do estoque.");
            }
            finally
            {
                governmentUpdated.RemoveEventHandler(governo, laterStockListener);
                governmentUpdated.RemoveEventHandler(governo, throwingStockListener);
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(activeOrderId))
            {
                activeOrderId = (string)registryType.GetMethod("FindActiveOrder", StaticMembers)
                    .Invoke(null, new object[] { teamId, "Naval", "command:Naval production economy PlayMode test" });
            }
            if (!string.IsNullOrEmpty(activeOrderId))
                registryType.GetMethod("Release", StaticMembers).Invoke(null, new object[] { activeOrderId, Time.time });
            if (brainRoot != null) UnityEngine.Object.DestroyImmediate(brainRoot);
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (countries != null && country != null) countries.Remove(country);
            Invoke(governo, "DefinirReservaFinanceiraEstrategica", teamId, previousFinancialReserve);
        }

        yield return null;
    }

    private static object CriarPedidoProducaoNaval(string itemKey)
    {
        Type payloadType = TypeOf("Hegemonia.AI.BrainMaster.IA_ProduceOrderData");
        object payload = Activator.CreateInstance(payloadType);
        Set(payload, "ItemKey", itemKey);
        Set(payload, "Quantity", 1);
        Set(payload, "ProductionOrderId", string.Empty);

        Type requestType = TypeOf("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        object request = Activator.CreateInstance(requestType);
        Set(request, "Type", Enum.Parse(TypeOf("Hegemonia.AI.BrainMaster.IA_CommandType"), "Produce"));
        Set(request, "Origin", "Naval production economy PlayMode test");
        Set(request, "Payload", payload);
        return request;
    }

    private void DefinirEstoque(int teamId, string recursoId, double quantidade)
    {
        double atual = Quantidade(teamId, recursoId);
        if (atual > 0d)
        {
            Invoke(industrial, "TentarConsumir", teamId.ToString(), recursoId, atual);
        }

        if (quantidade > 0d)
        {
            Invoke(industrial, "TentarAdicionar", teamId.ToString(), recursoId, quantidade);
        }
    }

    private double Quantidade(int teamId, string recursoId)
    {
        return Convert.ToDouble(Invoke(industrial, "ObterQuantidadePais", teamId, recursoId));
    }

    private Component CriarTanqueDeTeste(string nome, int teamId, string classe = "Terrestre")
    {
        GameObject go = new GameObject(nome);
        objects.Add(go);
        go.SetActive(false);

        Component identidade = go.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identidade, "teamID", teamId);

        Component combustivel = go.AddComponent(TypeOf("CombustivelUnidade"));
        Type classeType = TypeOf("ClasseCombustivelUnidade");
        Set(combustivel, "classe", Enum.Parse(classeType, classe));
        Set(combustivel, "usaCombustivel", true);
        Set(combustivel, "preencherAoIniciar", false);
        Set(combustivel, "preservarCapacidadeConfigurada", true);
        Set(combustivel, "capacidade", 100f);
        Set(combustivel, "combustivelAtual", 0f);

        go.SetActive(true);
        return combustivel;
    }

    [UnityTest]
    public IEnumerator AircraftAndNavalFuelCapacityBonusesApplyOnceAndPreserveTankPercentage()
    {
        Type fuelType = TypeOf("CombustivelUnidade");
        Type fuelClassType = TypeOf("ClasseCombustivelUnidade");

        GameObject aircraft = new GameObject("Fuel_AircraftCapacityBonus_Test");
        aircraft.SetActive(false);
        objects.Add(aircraft);
        Component aircraftFuel = aircraft.AddComponent(fuelType);
        Set(aircraftFuel, "classe", Enum.Parse(fuelClassType, "Aerea"));
        Set(aircraftFuel, "capacidade", 100f);
        Set(aircraftFuel, "combustivelAtual", 50f);
        Set(aircraftFuel, "consumoPorSegundoMovendo", 2.5f);
        Set(aircraftFuel, "preservarCapacidadeConfigurada", true);
        Set(aircraftFuel, "preencherAoIniciar", false);
        Set(aircraftFuel, "mostrarIndicadorMundo", false);
        aircraft.SetActive(true);

        GameObject ship = new GameObject("Fuel_NavalCapacityBonus_Test");
        ship.SetActive(false);
        objects.Add(ship);
        Component shipFuel = ship.AddComponent(fuelType);
        Set(shipFuel, "classe", Enum.Parse(fuelClassType, "Naval"));
        Set(shipFuel, "capacidade", 100f);
        Set(shipFuel, "combustivelAtual", 40f);
        Set(shipFuel, "consumoPorSegundoMovendo", 3f);
        Set(shipFuel, "preservarCapacidadeConfigurada", true);
        Set(shipFuel, "preencherAoIniciar", false);
        Set(shipFuel, "mostrarIndicadorMundo", false);
        ship.SetActive(true);

        yield return null;

        Assert.That(Convert.ToSingle(Read(aircraftFuel, "capacidade")), Is.EqualTo(115f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(aircraftFuel, "combustivelAtual")), Is.EqualTo(57.5f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(aircraftFuel, "consumoPorSegundoMovendo")), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(Convert.ToSingle(Read(shipFuel, "capacidade")), Is.EqualTo(112f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(shipFuel, "combustivelAtual")), Is.EqualTo(44.8f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(shipFuel, "consumoPorSegundoMovendo")), Is.EqualTo(3f).Within(0.001f));

        Invoke(aircraftFuel, "ConfigurarSeNecessario", false);
        Invoke(shipFuel, "ConfigurarSeNecessario", false);
        Assert.That(Convert.ToSingle(Read(aircraftFuel, "capacidade")), Is.EqualTo(115f).Within(0.01f));
        Assert.That(Convert.ToSingle(Read(shipFuel, "capacidade")), Is.EqualTo(112f).Within(0.01f));
    }

    private GameObject CriarCaminhaoProduzidoDeTeste()
    {
        GameObject go = new GameObject("Fuel_Produced_Tanker");
        objects.Add(go);
        go.SetActive(false);

        Component identidade = go.AddComponent(TypeOf("IdentidadeUnidade"));
        Set(identidade, "teamID", 1);
        Set(identidade, "tipoUnidade", Enum.Parse(TypeOf("TipoUnidade"), "Veiculo"));

        Component combustivel = go.AddComponent(TypeOf("CombustivelUnidade"));
        Set(combustivel, "classe", Enum.Parse(TypeOf("ClasseCombustivelUnidade"), "Terrestre"));
        Set(combustivel, "usaCombustivel", true);
        Set(combustivel, "preencherAoIniciar", true);
        Set(combustivel, "preservarCapacidadeConfigurada", true);
        Set(combustivel, "capacidade", 100f);
        Set(combustivel, "combustivelAtual", -1f);

        Component caminhao = go.AddComponent(TypeOf("CaminhaoTanqueAbastecimento"));
        Set(caminhao, "capacidadeCarga", 80f);
        Set(caminhao, "cargaAtual", 50f);

        go.SetActive(true);
        return go;
    }

    private static float Abastecer(Component combustivel, float quantidade)
    {
        object[] argumentos = { combustivel, quantidade, 0f };
        bool sucesso = (bool)CallStatic(TypeOf("ServicoAbastecimento"), "TentarAbastecer", argumentos);
        return sucesso ? Convert.ToSingle(argumentos[2]) : 0f;
    }

    private static Type TypeOf(string nome)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome))
            .First(tipo => tipo != null);
    }

    private static object GetStatic(Type tipo, string nome)
    {
        PropertyInfo propriedade = tipo.GetProperty(nome, StaticMembers);
        return propriedade != null
            ? propriedade.GetValue(null, null)
            : tipo.GetField(nome, StaticMembers).GetValue(null);
    }

    private static object Invoke(object alvo, string nome, params object[] argumentos)
    {
        MethodInfo metodo = alvo.GetType().GetMethods(Members)
            .First(m => m.Name == nome
                && m.GetParameters().Length == argumentos.Length
                && AssinaturaCompativel(m.GetParameters(), argumentos));
        return metodo.Invoke(alvo, argumentos);
    }

    private static object CallStatic(Type tipo, string nome, params object[] argumentos)
    {
        MethodInfo metodo = tipo.GetMethods(StaticMembers)
            .First(m => m.Name == nome
                && m.GetParameters().Length == argumentos.Length
                && AssinaturaCompativel(m.GetParameters(), argumentos));
        return metodo.Invoke(null, argumentos);
    }

    private static bool AssinaturaCompativel(ParameterInfo[] parametros, object[] argumentos)
    {
        for (int i = 0; i < parametros.Length; i++)
        {
            if (argumentos[i] == null || !parametros[i].ParameterType.IsInstanceOfType(argumentos[i]))
            {
                return false;
            }
        }

        return true;
    }

    private struct EconomyScheduleSnapshot
    {
        public bool Valid;
        public float IntervaloEconomia;
        public float ProximoTick;
    }

    private static EconomyScheduleSnapshot CongelarEconomiaGlobal(object governo)
    {
        FieldInfo nextTick = governo.GetType().GetField("proximoTick", Members);
        Assert.That(nextTick, Is.Not.Null, "O scheduler economico precisa ser localizavel para isolar a fixture.");
        EconomyScheduleSnapshot snapshot = new EconomyScheduleSnapshot
        {
            Valid = true,
            IntervaloEconomia = Convert.ToSingle(Read(governo, "intervaloEconomia")),
            ProximoTick = Convert.ToSingle(nextTick.GetValue(governo))
        };
        Set(governo, "intervaloEconomia", 999999f);
        nextTick.SetValue(governo, Time.unscaledTime + 999999f);
        return snapshot;
    }

    private static void RestaurarEconomiaGlobal(object governo, EconomyScheduleSnapshot snapshot)
    {
        if (!snapshot.Valid)
        {
            return;
        }

        FieldInfo nextTick = governo.GetType().GetField("proximoTick", Members);
        Set(governo, "intervaloEconomia", snapshot.IntervaloEconomia);
        if (nextTick != null)
        {
            nextTick.SetValue(governo, snapshot.ProximoTick);
        }
    }

    private static object Read(object alvo, string nome) => alvo.GetType().GetField(nome, Members).GetValue(alvo);
    private static object ReadProperty(object alvo, string nome) => alvo.GetType().GetProperty(nome, Members).GetValue(alvo);
    private static object Field(object alvo, string nome) => alvo.GetType().GetField(nome, Members).GetValue(alvo);
    private static void Set(object alvo, string nome, object valor) => alvo.GetType().GetField(nome, Members).SetValue(alvo, valor);

    private static void RemoverNovosItens(IList lista, int contagemAnterior)
    {
        if (lista == null) return;
        while (lista.Count > contagemAnterior) lista.RemoveAt(lista.Count - 1);
    }
}
