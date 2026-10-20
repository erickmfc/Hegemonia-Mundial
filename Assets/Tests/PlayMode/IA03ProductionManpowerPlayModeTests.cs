using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IA03ProductionManpowerPlayModeTests
{
    private const int TestTeamId = 2941837;
    private const int TreasuryTestTeamId = 2941842;
    private const int PowerGridTestTeamId = 2941897;
    private const int CasualtyTestTeamId = 2941907;
    private const int EconomyCashFlowTestTeamId = 2941917;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator RuntimeFactoryDoesNotInstallAutomaticIndustrialStockGenerator()
    {
        GameObject factoryRoot = null;

        try
        {
            factoryRoot = new GameObject("Factory without automatic industrial stock test");
            factoryRoot.SetActive(false);
            Component identity = factoryRoot.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identity, "teamID", TestTeamId);
            factoryRoot.AddComponent(ResolveType("Fabrica"));
            factoryRoot.SetActive(true);

            yield return null;

            Assert.That(factoryRoot.GetComponent(ResolveType("ProducaoAutomaticaEdificio")), Is.Null,
                "Fábricas militares devem obter materiais pelo sistema nacional de extração/refino, sem gerar estoques aleatórios gratuitos.");
        }
        finally
        {
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
        }
    }

    [Test]
    public void MilitaryFactoryRequiresFullPowerBeforeSpawningAUnit()
    {
        GameObject factoryRoot = null;
        GameObject prefab = null;
        GameObject spawned = null;

        try
        {
            factoryRoot = new GameObject("Power-gated military factory test");
            factoryRoot.SetActive(false);
            Component economy = factoryRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetField(economy, "tipo", Enum.Parse(ResolveType("TipoEstruturaEconomica"), "InstalacaoProdutiva"));
            SetField(economy, "energiaConsumida", 260f);
            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "SemEnergia"));
            SetField(economy, "eficiencia", 0f);
            Component factory = factoryRoot.AddComponent(ResolveType("Fabrica"));
            factoryRoot.SetActive(true);
            Assert.That(factory.GetComponentInParent(economy.GetType()), Is.SameAs(economy));
            Assert.That((bool)economy.GetType().GetProperty("Ativa").GetValue(economy, null), Is.False);
            Assert.That(Invoke(factory, "PossuiEnergiaParaProduzir"), Is.False,
                "A fábrica deve ler o componente econômico do próprio objeto.");

            prefab = new GameObject("Power-gated unit marker");
            spawned = Invoke(factory, "ProduzirUnidade", prefab) as GameObject;
            Assert.That(spawned, Is.Null,
                "Sem energia, a fábrica não deve materializar a unidade.");

            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetField(economy, "eficiencia", 0.5f);
            spawned = Invoke(factory, "ProduzirUnidade", prefab) as GameObject;
            Assert.That(spawned, Is.Null,
                "Energia parcial não deve liberar um spawn instantâneo como se a fábrica estivesse em plena capacidade.");
        }
        finally
        {
            if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
        }
    }

    [UnityTest]
    public IEnumerator NationalPowerGridUpdatesFactoryGateForFullPartialAndMissingSupply()
    {
        GameObject economyRoot = null;
        GameObject generatorRoot = null;
        GameObject factoryRoot = null;
        Component economySystem = null;
        bool createdEconomySystem = false;

        try
        {
            Type economySystemType = ResolveType("SistemaEconomiaImoveis");
            economySystem = economySystemType.GetProperty("Instancia", StaticMembers).GetValue(null, null) as Component;
            if (economySystem == null)
            {
                economyRoot = new GameObject("National power grid test system");
                economyRoot.SetActive(false);
                economySystem = economyRoot.AddComponent(economySystemType);
                economyRoot.SetActive(true);
                createdEconomySystem = true;
            }

            generatorRoot = new GameObject("Power grid test generator");
            generatorRoot.SetActive(false);
            Component generator = generatorRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetField(generator, "tipo", Enum.Parse(ResolveType("TipoEstruturaEconomica"), "UsinaSolar"));
            SetField(generator, "teamId", PowerGridTestTeamId);
            SetField(generator, "energiaProduzida", 260f);
            SetField(generator, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetField(generator, "eficiencia", 1f);
            generatorRoot.SetActive(true);

            factoryRoot = new GameObject("Power grid test factory");
            factoryRoot.SetActive(false);
            Component economy = factoryRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetField(economy, "tipo", Enum.Parse(ResolveType("TipoEstruturaEconomica"), "InstalacaoProdutiva"));
            SetField(economy, "teamId", PowerGridTestTeamId);
            SetField(economy, "energiaConsumida", 260f);
            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "SemEnergia"));
            SetField(economy, "eficiencia", 0f);
            Component factory = factoryRoot.AddComponent(ResolveType("Fabrica"));
            factoryRoot.SetActive(true);

            yield return null;

            yield return new WaitForSecondsRealtime(0.3f);
            Invoke(economySystem, "Recalcular");
            Assert.That(economy.GetType().GetProperty("Ativa").GetValue(economy, null), Is.EqualTo(true),
                "Geração suficiente deve ativar a instalação produtiva pela distribuição nacional.");
            Assert.That((float)GetField(economy, "eficiencia"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(Invoke(factory, "PossuiEnergiaParaProduzir"), Is.EqualTo(true));

            SetField(generator, "energiaProduzida", 130f);
            yield return new WaitForSecondsRealtime(0.3f);
            Invoke(economySystem, "Recalcular");
            Assert.That((float)GetField(economy, "eficiencia"), Is.EqualTo(0.5f).Within(0.001f),
                "A rede deve propagar a fração de energia disponível para a fábrica.");
            Assert.That(Invoke(factory, "PossuiEnergiaParaProduzir"), Is.EqualTo(false),
                "Energia parcial não deve liberar o spawn instantâneo da fábrica.");

            SetField(generator, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "SemEnergia"));
            yield return new WaitForSecondsRealtime(0.3f);
            Invoke(economySystem, "Recalcular");
            Assert.That(GetField(economy, "status").ToString(), Is.EqualTo("SemEnergia"));
            Assert.That((float)GetField(economy, "eficiencia"), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Invoke(factory, "PossuiEnergiaParaProduzir"), Is.EqualTo(false));

            SetField(generator, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetField(generator, "energiaProduzida", 260f);
            yield return new WaitForSecondsRealtime(0.3f);
            Invoke(economySystem, "Recalcular");
            Assert.That(Invoke(factory, "PossuiEnergiaParaProduzir"), Is.EqualTo(true),
                "Após a geração voltar, a fábrica deve recuperar a capacidade sem reinicialização.");
        }
        finally
        {
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
            if (generatorRoot != null) UnityEngine.Object.DestroyImmediate(generatorRoot);
            if (createdEconomySystem && economyRoot != null) UnityEngine.Object.DestroyImmediate(economyRoot);
        }
    }

    [UnityTest]
    public IEnumerator NavalYardPausesWithoutPowerAndScalesProgressWithPartialPower()
    {
        GameObject yardRoot = null;
        GameObject prefab = null;

        try
        {
            yardRoot = new GameObject("Power-gated naval yard test");
            yardRoot.SetActive(false);
            Component economy = yardRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetField(economy, "tipo", Enum.Parse(ResolveType("TipoEstruturaEconomica"), "InstalacaoProdutiva"));
            SetField(economy, "energiaConsumida", 340f);
            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "SemEnergia"));
            SetField(economy, "eficiencia", 0f);
            Component yard = yardRoot.AddComponent(ResolveType("Estaleiro"));
            SetField(yard, "tempoDeConstrucao", 100f);

            Type slotType = yard.GetType().GetNestedType("SlotConstrucao", BindingFlags.Public);
            object slot = Activator.CreateInstance(slotType);
            SetField(slot, "progresso", 0f);
            prefab = new GameObject("Naval progress marker");
            SetField(slot, "prefabAtual", prefab);

            yield return null;
            yardRoot.SetActive(true);
            Assert.That(yard.GetComponentInParent(economy.GetType()), Is.SameAs(economy));
            Assert.That((float)Invoke(yard, "ObterEficienciaEnergetica"), Is.EqualTo(0f).Within(0.0001f),
                "O estaleiro deve ler o componente econômico do próprio objeto.");

            Invoke(yard, "ProcessarConstrucao", slot);
            Assert.That((float)GetField(slot, "progresso"), Is.EqualTo(0f).Within(0.0001f),
                "Sem energia, o progresso do estaleiro deve ficar pausado.");

            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetField(economy, "eficiencia", 0.5f);
            float deltaTime = Time.deltaTime;
            Invoke(yard, "ProcessarConstrucao", slot);
            float progressoComEnergiaParcial = (float)GetField(slot, "progresso");
            Assert.That(progressoComEnergiaParcial, Is.GreaterThan(0f));
            Assert.That(progressoComEnergiaParcial,
                Is.EqualTo((deltaTime / 100f) * 100f * 0.5f).Within(0.001f),
                "A energia parcial deve reduzir proporcionalmente a velocidade de construção naval.");
        }
        finally
        {
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (yardRoot != null) UnityEngine.Object.DestroyImmediate(yardRoot);
        }
    }

    [UnityTest]
    public IEnumerator NavalYardRejectsUnpoweredOrderBeforeQueueAndAcceptsPartialPower()
    {
        GameObject yardRoot = null;
        GameObject prefab = null;
        GameObject constructionPoint = null;

        try
        {
            yardRoot = new GameObject("Naval order energy gate test");
            yardRoot.SetActive(false);
            Component economy = yardRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "SemEnergia"));
            SetField(economy, "eficiencia", 0f);
            Component yard = yardRoot.AddComponent(ResolveType("Estaleiro"));
            yard.GetType().GetProperty("OwnerTeamId").SetValue(yard, 2, null);
            Type slotType = yard.GetType().GetNestedType("SlotConstrucao", BindingFlags.Public);
            object slot = Activator.CreateInstance(slotType);
            constructionPoint = new GameObject("Naval order construction point");
            constructionPoint.transform.SetParent(yardRoot.transform, false);
            SetField(slot, "pontoDeConstrucao", constructionPoint.transform);
            SetField(slot, "nomeSlot", "TestSlot");
            Array slots = Array.CreateInstance(slotType, 1);
            slots.SetValue(slot, 0);
            SetField(yard, "slots", slots);
            prefab = new GameObject("Naval order energy gate marker");

            yield return null;
            yardRoot.SetActive(true);
            Assert.That((bool)yard.GetType().GetProperty("PossuiEnergiaOperacional").GetValue(yard, null), Is.False);
            Assert.That((bool)Invoke(yard, "ConstruirUnidade", prefab, string.Empty), Is.False,
                "Sem energia, a ordem deve ser recusada antes de ocupar slot ou fila.");
            Assert.That((bool)GetField(slot, "estaOcupado"), Is.False);

            SetField(economy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetField(economy, "eficiencia", 0.5f);
            Assert.That((bool)yard.GetType().GetProperty("PossuiEnergiaOperacional").GetValue(yard, null), Is.True,
                "Energia parcial é operacional para a construção naval, que progride em ritmo reduzido.");
            Assert.That((bool)Invoke(yard, "ConstruirUnidade", prefab, string.Empty), Is.True);
            Assert.That((bool)GetField(slot, "estaOcupado"), Is.True);
        }
        finally
        {
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (yardRoot != null) UnityEngine.Object.DestroyImmediate(yardRoot);
            else if (constructionPoint != null) UnityEngine.Object.DestroyImmediate(constructionPoint);
        }
    }

    [UnityTest]
    public IEnumerator ProductionManagerWaitsForFactoryOrShipyardInsteadOfSpawningAtFallback()
    {
        GameObject managerRoot = null;
        GameObject prefab = null;
        GameObject spawnedLandFallback = null;
        GameObject spawnedNavalFallback = null;

        try
        {
            managerRoot = new GameObject("Production infrastructure gate test manager");
            managerRoot.SetActive(false);
            Component manager = managerRoot.AddComponent(ResolveType("GerenteDeJogo"));

            prefab = new GameObject("Production infrastructure gate marker");
            Component identity = prefab.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Estrutura"));

            Type pedidoType = manager.GetType().GetNestedType("PedidoDeProducao", BindingFlags.Public);
            object landOrder = Activator.CreateInstance(pedidoType);
            SetField(landOrder, "nomeUnidade", prefab.name);
            SetField(landOrder, "prefab", prefab);
            SetField(landOrder, "ehSoldado", false);
            SetField(landOrder, "ehNavio", false);

            Assert.That(Invoke(manager, "FinalizarProducao", landOrder), Is.False,
                "Sem fábrica terrestre real, o pedido deve aguardar em vez de nascer perto da câmera/Base_Ares.");
            spawnedLandFallback = GameObject.Find(prefab.name + "(Clone)");
            Assert.That(spawnedLandFallback, Is.Null,
                "A fila não deve materializar uma unidade terrestre sem fábrica.");

            object navalOrder = Activator.CreateInstance(pedidoType);
            SetField(navalOrder, "nomeUnidade", prefab.name);
            SetField(navalOrder, "prefab", prefab);
            SetField(navalOrder, "ehNavio", true);

            Assert.That(Invoke(manager, "FinalizarProducao", navalOrder), Is.False,
                "Sem estaleiro real, o pedido deve aguardar em vez de nascer perto da câmera.");
            spawnedNavalFallback = GameObject.Find(prefab.name + "(Clone)");
            Assert.That(spawnedNavalFallback, Is.Null,
                "A fila não deve materializar um navio sem estaleiro.");
        }
        finally
        {
            if (spawnedLandFallback != null) UnityEngine.Object.DestroyImmediate(spawnedLandFallback);
            if (spawnedNavalFallback != null) UnityEngine.Object.DestroyImmediate(spawnedNavalFallback);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (managerRoot != null) UnityEngine.Object.DestroyImmediate(managerRoot);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator FactoryRejectsProductionWithoutManpowerAndMobilizesLegacyVehicleCrewWhenAvailable()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type factoryType = ResolveType("Fabrica");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject factoryRoot = null;
        GameObject prefab = null;
        GameObject produced = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            if (government == null)
            {
                governmentRoot = new GameObject("IA03 manpower test government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", TestTeamId);
            SetField(testCountry, "alistaveis", 0);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);
            Assert.That(Invoke(government, "ObterPais", TestTeamId), Is.SameAs(testCountry));

            factoryRoot = new GameObject("IA03 manpower test factory");
            factoryRoot.SetActive(false);
            Component factoryIdentity = factoryRoot.AddComponent(identityType);
            SetField(factoryIdentity, "teamID", TestTeamId);
            Component factory = factoryRoot.AddComponent(factoryType);
            factoryRoot.SetActive(true);

            prefab = new GameObject("IA03 manpower test tank");
            Component prefabIdentity = prefab.AddComponent(identityType);
            SetField(prefabIdentity, "militaresConsumidos", 10);

            object spawned = factoryType.GetMethod("ProduzirUnidade", InstanceMembers)
                .Invoke(factory, new object[] { prefab });
            produced = spawned as GameObject;

            Assert.That(produced, Is.Null,
                "A fábrica criou uma unidade embora o país não tenha alistáveis nem civis disponíveis.");
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(0));

            SetField(testCountry, "alistaveis", 1);
            SetField(prefabIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Veiculo"));
            SetField(prefabIdentity, "militaresConsumidos", 0);

            EventInfo governmentUpdated = governmentType.GetEvent("OnGovernoAtualizado", InstanceMembers);
            Assert.That(governmentUpdated, Is.Not.Null);
            Action throwingListener = () =>
                throw new InvalidOperationException("Falha controlada do listener durante producao de fabrica.");
            int laterListenerInvocations = 0;
            Action laterListener = () => laterListenerInvocations++;
            governmentUpdated.AddEventHandler(government, throwingListener);
            governmentUpdated.AddEventHandler(government, laterListener);
            try
            {
                produced = factoryType.GetMethod("ProduzirUnidade", InstanceMembers)
                    .Invoke(factory, new object[] { prefab }) as GameObject;
            }
            finally
            {
                governmentUpdated.RemoveEventHandler(government, laterListener);
                governmentUpdated.RemoveEventHandler(government, throwingListener);
            }

            Assert.That(produced, Is.Not.Null,
                "A fábrica deve produzir o veículo legado mesmo se um listener de governo falhar durante mobilização.");
            Assert.That(GetField(testCountry, "alistaveis"), Is.EqualTo(0));
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(1));
            Assert.That(laterListenerInvocations, Is.EqualTo(1),
                "A falha de um listener não deve impedir que a fábrica mobilize o efetivo nem notifique os demais.");
            Component producedIdentity = produced.GetComponent(identityType);
            Assert.That(GetField(producedIdentity, "teamID"), Is.EqualTo(TestTeamId));
            Assert.That(GetField(producedIdentity, "militaresConsumidos"), Is.EqualTo(1),
                "A mobilização deve gravar no veículo produzido o efetivo padrão aplicado.");
        }
        finally
        {
            if (produced != null)
            {
                Component producedIdentity = produced.GetComponent(identityType);
                if (producedIdentity != null) SetField(producedIdentity, "militaresConsumidos", 0);
                UnityEngine.Object.DestroyImmediate(produced);
            }
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator FactoryDoesNotSpawnIdentitylessRefuellerWithoutVehicleCrew()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type factoryType = ResolveType("Fabrica");
        Type tankerType = ResolveType("CaminhaoTanqueAbastecimento");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject factoryRoot = null;
        GameObject prefab = null;
        GameObject produced = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            if (government == null)
            {
                governmentRoot = new GameObject("identityless refueller manpower government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", TestTeamId);
            SetField(testCountry, "alistaveis", 0);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);

            factoryRoot = new GameObject("identityless refueller manpower factory");
            factoryRoot.SetActive(false);
            Component factoryIdentity = factoryRoot.AddComponent(identityType);
            SetField(factoryIdentity, "teamID", TestTeamId);
            Component factory = factoryRoot.AddComponent(factoryType);
            factoryRoot.SetActive(true);

            prefab = new GameObject("legacy fuel refueller without serialized identity");
            prefab.AddComponent(tankerType);
            Assert.That(prefab.GetComponent(identityType), Is.Null,
                "The fixture must represent a legacy refueller with no IdentidadeUnidade.");

            produced = factoryType.GetMethod("ProduzirUnidade", InstanceMembers)
                .Invoke(factory, new object[] { prefab }) as GameObject;

            Assert.That(produced, Is.Null,
                "The factory must recognize its existing tanker movement component and reject a free spawn when national manpower is empty.");
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(0));

            SetField(testCountry, "alistaveis", 1);
            produced = factoryType.GetMethod("ProduzirUnidade", InstanceMembers)
                .Invoke(factory, new object[] { prefab }) as GameObject;

            Assert.That(produced, Is.Not.Null,
                "A legacy refueller should still produce when its central vehicle minimum is available.");
            Component producedIdentity = produced.GetComponent(identityType);
            Assert.That(producedIdentity, Is.Not.Null,
                "The produced legacy refueller must receive the identity that stores its mobilized crew.");
            Assert.That(GetField(producedIdentity, "teamID"), Is.EqualTo(TestTeamId));
            Assert.That(GetField(producedIdentity, "tipoUnidade").ToString(), Is.EqualTo("Veiculo"));
            Assert.That(GetField(producedIdentity, "militaresConsumidos"), Is.EqualTo(1));
            Assert.That(GetField(testCountry, "alistaveis"), Is.EqualTo(0));
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(1));
        }
        finally
        {
            if (produced != null)
            {
                Component producedIdentity = produced.GetComponent(identityType);
                if (producedIdentity != null) SetField(producedIdentity, "militaresConsumidos", 0);
                UnityEngine.Object.DestroyImmediate(produced);
            }
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (factoryRoot != null) UnityEngine.Object.DestroyImmediate(factoryRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayerProductionRejectsBatchThatExceedsAvailableManpower()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject managerRoot = null;
        GameObject prefab = null;
        GameObject produced = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            governmentRoot = new GameObject("Player production manpower test government");
            governmentRoot.SetActive(false);
            government = governmentRoot.AddComponent(governmentType);
            setGovernment.Invoke(null, new[] { government });

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", 1);
            SetField(testCountry, "alistaveis", 10);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);

            prefab = new GameObject("player test vehicle");
            Component prefabIdentity = prefab.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(prefabIdentity, "militaresConsumidos", 6);

            managerRoot = new GameObject("Player production manpower test manager");
            managerRoot.SetActive(false);
            Component manager = managerRoot.AddComponent(ResolveType("GerenteDeJogo"));

            Assert.That(Invoke(government, "PodeProduzirUnidadeComEfetivo", 1, prefab, 1), Is.EqualTo(true));
            Assert.That(Invoke(government, "PodeProduzirUnidadeComEfetivo", 1, prefab, 2), Is.EqualTo(false),
                "A validação do lote deve considerar o efetivo agregado das duas unidades.");

            Invoke(manager, "ComprarUnidade", prefab, 500L, 2);
            IList pendingOrders = (IList)GetField(manager, "filaProducao");
            Assert.That(pendingOrders, Is.Empty,
                "O gerente do jogador aceitou um lote que excede o efetivo disponível.");
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(0));

            SetField(testCountry, "alistaveis", 0);
            Type productionOrderType = manager.GetType().GetNestedType("PedidoDeProducao", BindingFlags.Public);
            object pendingOrder = Activator.CreateInstance(productionOrderType);
            SetField(pendingOrder, "nomeUnidade", prefab.name);
            SetField(pendingOrder, "prefab", prefab);
            Assert.That(Invoke(manager, "FinalizarProducao", pendingOrder), Is.EqualTo(false),
                "Um pedido já enfileirado deve esperar se o efetivo ficar indisponível antes do spawn.");

            SetField(testCountry, "alistaveis", 10);
            GameObject producedUnit = new GameObject("player test produced vehicle");
            Component producedIdentity = producedUnit.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(producedIdentity, "teamID", 1);
            SetField(producedIdentity, "militaresConsumidos", 6);
            produced = producedUnit;
            Assert.That(Invoke(government, "TentarMobilizarEfetivoDaUnidade", 1, producedUnit), Is.EqualTo(true));
            Assert.That(GetField(testCountry, "alistaveis"), Is.EqualTo(4));
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(6));
        }
        finally
        {
            if (produced != null) UnityEngine.Object.DestroyImmediate(produced);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (managerRoot != null) UnityEngine.Object.DestroyImmediate(managerRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            setGovernment.Invoke(null, new[] { previousGovernment });
            if (governmentRoot != null) UnityEngine.Object.DestroyImmediate(governmentRoot);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator LegacyMilitaryPrefabsUseNationalCrewDefaultsAndMobilizeThem()
    {
        const int teamId = 2941848;
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type unitType = ResolveType("TipoUnidade");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        MethodInfo getEffectiveByFallback = governmentType.GetMethod(
            "ObterEfetivoNecessario",
            StaticMembers,
            null,
            new[] { typeof(GameObject), unitType },
            null);
        MethodInfo getEffectiveFromIdentity = governmentType.GetMethod(
            "ObterEfetivoNecessario",
            StaticMembers,
            null,
            new[] { typeof(GameObject) },
            null);
        MethodInfo canProduceBatchWithFallback = governmentType.GetMethod(
            "PodeProduzirUnidadeComEfetivo",
            InstanceMembers,
            null,
            new[] { typeof(int), typeof(GameObject), typeof(int), unitType },
            null);
        GameObject governmentRoot = null;
        GameObject prefab = null;
        GameObject produced = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            Assert.That(getEffectiveByFallback, Is.Not.Null);
            Assert.That(getEffectiveFromIdentity, Is.Not.Null);
            Assert.That(canProduceBatchWithFallback, Is.Not.Null);
            if (government == null)
            {
                governmentRoot = new GameObject("legacy manpower defaults test government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", teamId);
            SetField(testCountry, "alistaveis", 10);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);

            prefab = new GameObject("legacy naval prefab without serialized crew");
            Component prefabIdentity = prefab.AddComponent(identityType);
            SetField(prefabIdentity, "militaresConsumidos", 0);
            int[] expectedEffective = { 1, 1, 4, 2, 0 };
            for (int typeValue = 0; typeValue < expectedEffective.Length; typeValue++)
            {
                SetField(prefabIdentity, "tipoUnidade", Enum.ToObject(unitType, typeValue));
                Assert.That(getEffectiveFromIdentity.Invoke(null, new object[] { prefab }), Is.EqualTo(expectedEffective[typeValue]));
            }

            SetField(prefabIdentity, "tipoUnidade", Enum.ToObject(unitType, 2));
            Assert.That(getEffectiveByFallback.Invoke(null, new[] { prefab, Enum.ToObject(unitType, 2) }), Is.EqualTo(4),
                "Um navio legado com efetivo zero deve usar o mínimo naval central.");
            Assert.That(getEffectiveByFallback.Invoke(null, new[] { prefab, Enum.ToObject(unitType, 3) }), Is.EqualTo(2),
                "A rota explícita deve conseguir aplicar o mínimo aéreo quando a categoria de entrega define aeronave.");
            Assert.That(canProduceBatchWithFallback.Invoke(government, new object[] { teamId, prefab, 2, Enum.ToObject(unitType, 2) }), Is.EqualTo(true));
            Assert.That(canProduceBatchWithFallback.Invoke(government, new object[] { teamId, prefab, 3, Enum.ToObject(unitType, 2) }), Is.EqualTo(false),
                "O pré-check de lote deve multiplicar o efetivo mínimo pelo total comprado.");

            produced = new GameObject("legacy naval produced unit");
            Component producedIdentity = produced.AddComponent(identityType);
            SetField(producedIdentity, "teamID", teamId);
            SetField(producedIdentity, "tipoUnidade", Enum.ToObject(unitType, 2));
            SetField(producedIdentity, "militaresConsumidos", 0);
            Assert.That(Invoke(government, "TentarMobilizarEfetivoDaUnidade", teamId, produced), Is.EqualTo(true));
            Assert.That(GetField(testCountry, "alistaveis"), Is.EqualTo(6));
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(4));
            Assert.That(GetField(producedIdentity, "militaresConsumidos"), Is.EqualTo(4));
        }
        finally
        {
            if (produced != null)
            {
                Component identity = produced.GetComponent(identityType);
                if (identity != null) SetField(identity, "militaresConsumidos", 0);
                UnityEngine.Object.DestroyImmediate(produced);
            }
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator BrainProductionBudgetUsesNationalTreasuryInsteadOfMintingCredits()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject brainRoot = null;
        UnityEngine.Object testDefinition = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;
        long previousFinancialReserve = 0L;
        bool financialReserveCaptured = false;

        try
        {
            if (government == null)
            {
                governmentRoot = new GameObject("IA03 treasury test government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            previousFinancialReserve = (long)Invoke(government, "ObterReservaFinanceiraEstrategica", TreasuryTestTeamId);
            financialReserveCaptured = true;
            Invoke(government, "DefinirReservaFinanceiraEstrategica", TreasuryTestTeamId, 2000L);

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", TreasuryTestTeamId);
            SetField(testCountry, "saldo", 5000L);
            countries.Add(testCountry);

            brainRoot = new GameObject("IA03 treasury test brain");
            brainRoot.SetActive(false);
            Component brain = brainRoot.AddComponent(brainType);
            SetField(brain, "TeamId", TreasuryTestTeamId);
            SetField(brain, "Credits", 30000);

            Invoke(brain, "TickEconomy", 1f);
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(3000),
                "O espelho deve excluir a reserva estratégica protegida e não gerar renda própria.");
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(5000L));

            Type intentBoardType = ResolveType("Hegemonia.AI.BrainMaster.IA_NationalIntentBoard");
            Type intentType = ResolveType("Hegemonia.AI.BrainMaster.IA_NationalIntent");
            Type commandType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandRequest");
            Type contextType = ResolveType("Hegemonia.AI.BrainMaster.IA_Context");
            object intentBoard = Activator.CreateInstance(intentBoardType);
            object overBudgetIntent = Activator.CreateInstance(intentType);
            SetField(overBudgetIntent, "DedupKey", "treasury-reserve-over-budget");
            SetField(overBudgetIntent, "Kind", Enum.Parse(ResolveType("Hegemonia.AI.BrainMaster.IA_IntentKind"), "Produce"));
            SetField(overBudgetIntent, "EstimatedCost", 3500L);
            object intentCommand = Activator.CreateInstance(commandType);
            SetField(intentCommand, "Type", Enum.Parse(ResolveType("Hegemonia.AI.BrainMaster.IA_CommandType"), "Move"));
            SetField(intentCommand, "Origin", "treasury reserve test");
            SetField(intentCommand, "DedupKey", "treasury-reserve-over-budget-command");
            SetField(overBudgetIntent, "Command", intentCommand);

            MethodInfo publishIntent = intentBoardType.GetMethod("Publish", InstanceMembers);
            Assert.That(publishIntent, Is.Not.Null);
            object[] publishArguments = { overBudgetIntent, Time.time, string.Empty };
            Assert.That(publishIntent.Invoke(intentBoard, publishArguments), Is.True);

            object intentContext = Activator.CreateInstance(contextType);
            SetField(intentContext, "Brain", brain);
            SetField(intentContext, "IntentBoard", intentBoard);
            SetField(intentContext, "StrategyArbiter", Activator.CreateInstance(ResolveType("Hegemonia.AI.BrainMaster.IA_StrategyArbiter")));
            SetField(intentContext, "CommandQueue", Activator.CreateInstance(ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue")));
            SetField(brain, "Credits", 5000);
            object intentRouter = Activator.CreateInstance(
                ResolveType("Hegemonia.AI.BrainMaster.IA_IntentCommandRouter"),
                intentContext);
            Invoke(intentRouter, "Tick", Time.time, 0f);
            object commandQueue = GetField(intentContext, "CommandQueue");
            int pendingCommands = (int)commandQueue.GetType().GetProperty("PendingCount", InstanceMembers)
                .GetValue(commandQueue, null);
            Assert.That(pendingCommands, Is.EqualTo(0),
                "O roteador deve consultar o tesouro atual e não aprovar uma intenção que invade a reserva protegida.");
            Assert.That(GetField(overBudgetIntent, "Approved"), Is.False);

            Assert.That(Invoke(brain, "TrySpend", 1200L), Is.EqualTo(true));
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(3800L));
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(1800));

            Invoke(brain, "Refund", 300L);
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(4100L));
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(2100));

            Type definitionType = ResolveType("DadosConstrucao");
            testDefinition = ScriptableObject.CreateInstance(definitionType);
            SetField(testDefinition, "precoDefinitivo", 600L);
            Type categoryType = definitionType.GetNestedType("CategoriaItem", BindingFlags.Public);
            SetField(testDefinition, "categoria", Enum.Parse(categoryType, "Tecnologia"));
            Type treasuryType = ResolveType("Hegemonia.AI.Shared.IA_NationalTreasury");
            MethodInfo executePaidProduction = treasuryType.GetMethod("TentarProduzirComTesouroNacional", StaticMembers);
            Assert.That(executePaidProduction, Is.Not.Null);

            object accepted = executePaidProduction.Invoke(null, new object[]
            {
                TreasuryTestTeamId,
                testDefinition,
                new Func<bool>(() => true)
            });
            Assert.That(accepted, Is.EqualTo(true));
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(3500L));
            Invoke(brain, "TickEconomy", 1f);
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(1500),
                "Após a cobrança, o espelho deve atualizar o saldo livre e preservar a reserva.");

            object refused = executePaidProduction.Invoke(null, new object[]
            {
                TreasuryTestTeamId,
                testDefinition,
                new Func<bool>(() => false)
            });
            Assert.That(refused, Is.EqualTo(false));
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(3500L),
                "Uma recusa do produtor deve estornar a cobrança nacional.");
            Invoke(brain, "TickEconomy", 1f);
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(1500));

            SetField(testDefinition, "categoria", Enum.Parse(categoryType, "Exercito"));
            Type capabilityType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability");
            definitionType.GetProperty("Capacidades", InstanceMembers).SetValue(
                testDefinition,
                Enum.Parse(capabilityType, "Unit, Military"),
                null);
            MethodInfo validateRecipe = definitionType.GetMethod("TryValidarReceitaProducao", InstanceMembers);
            Assert.That(validateRecipe, Is.Not.Null);
            Assert.That(validateRecipe.Invoke(testDefinition, new object[] { null }), Is.EqualTo(false),
                "A ficha permanece explicitamente pendente enquanto não houver valores aprovados.");

            bool producerInvoked = false;
            object legacyProduction = executePaidProduction.Invoke(null, new object[]
            {
                TreasuryTestTeamId,
                testDefinition,
                new Func<bool>(() =>
                {
                    producerInvoked = true;
                    return true;
                })
            });
            Assert.That(legacyProduction, Is.EqualTo(true),
                "Uma ficha sem os novos campos deve conservar seu contrato de produção legado válido.");
            Assert.That(producerInvoked, Is.True,
                "A tesouraria deve entregar a produção ao produtor existente mesmo com a nova receita pendente.");
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(2900L),
                "A produção aceita deve cobrar o preço monetário atual da ficha.");
            Invoke(brain, "TickEconomy", 1f);
            Assert.That(GetField(brain, "Credits"), Is.EqualTo(900));
        }
        finally
        {
            if (testDefinition != null) UnityEngine.Object.DestroyImmediate(testDefinition);
            if (brainRoot != null) UnityEngine.Object.DestroyImmediate(brainRoot);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (financialReserveCaptured && government != null)
            {
                Invoke(government, "DefinirReservaFinanceiraEstrategica", TreasuryTestTeamId, previousFinancialReserve);
            }
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator NpcTreasuryAppliesSignedNationalCashflowWithoutMinimumGrant()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        object testCountry = null;
        IList countries = null;

        try
        {
            governmentRoot = new GameObject("NPC cash-flow test government");
            governmentRoot.SetActive(false);
            Component government = governmentRoot.AddComponent(governmentType);
            setGovernment.Invoke(null, new[] { government });
            SetField(government, "teamJogador", 1);

            countries = (IList)GetField(government, "paises");
            countries.Clear();
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", EconomyCashFlowTestTeamId);
            SetField(testCountry, "saldo", 0L);
            SetField(testCountry, "populacao", 0);
            SetField(testCountry, "populacaoCivil", 0);
            SetField(testCountry, "populacaoMaxima", 0);
            SetField(testCountry, "populacaoMilitarAtiva", 0);
            SetField(testCountry, "reservistas", 0);
            SetField(testCountry, "alistaveis", 0);
            countries.Add(testCountry);

            Invoke(government, "ProcessarEconomia");

            Assert.That(GetField(testCountry, "rendaPorSegundo"), Is.EqualTo(0f));
            Assert.That(GetField(testCountry, "gastosPorSegundo"), Is.EqualTo(0f));
            Assert.That(GetField(testCountry, "saldo"), Is.EqualTo(0L),
                "Fluxo nacional zero não deve gerar o crédito mínimo artificial de um ponto por ciclo.");

            SetField(testCountry, "saldo", 1000L);
            SetField(testCountry, "populacaoMilitarAtiva", 50);
            SetField(testCountry, "populacao", 50);
            SetField(testCountry, "populacaoMaxima", 50);

            Invoke(government, "ProcessarEconomia");

            float saldoLiquido = (float)GetField(testCountry, "rendaPorSegundo")
                - (float)GetField(testCountry, "gastosPorSegundo");
            Assert.That(saldoLiquido, Is.LessThan(0f),
                "A fixture precisa representar um fluxo nacional deficitário.");
            Assert.That(GetField(testCountry, "saldo"),
                Is.EqualTo(1000L + Mathf.RoundToInt(saldoLiquido)),
                "O tesouro da IA deve aplicar o fluxo líquido real também quando despesas superam receitas.");
        }
        finally
        {
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            setGovernment.Invoke(null, new[] { previousGovernment });
            if (governmentRoot != null) UnityEngine.Object.DestroyImmediate(governmentRoot);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator NovaIAProductionWithoutOwnedAirportAndThrownInfrastructureFailureRefundAndRelease()
    {
        const int teamId = 2941921;
        const long startingBalance = 5000L;
        const long aircraftPrice = 1200L;

        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type controllerType = ResolveType("Hegemonia.AI.Master.IA_MasterController");
        Type definitionType = ResolveType("DadosConstrucao");
        Type menuType = ResolveType("MenuConstrucao");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = governmentInstance.GetValue(null, null);
        MethodInfo setGovernment = governmentInstance.GetSetMethod(true);
        FieldInfo catalogField = menuType.GetField("catalogoGlobal", StaticMembers);
        object previousCatalog = catalogField.GetValue(null);

        GameObject governmentRoot = null;
        GameObject controllerRoot = null;
        GameObject aircraftPrefab = null;
        ScriptableObject item = null;

        try
        {
            governmentRoot = new GameObject("NovaIA no-airport treasury test government");
            governmentRoot.SetActive(false);
            Component government = governmentRoot.AddComponent(governmentType);
            setGovernment.Invoke(null, new[] { government });

            IList countries = (IList)GetField(government, "paises");
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            SetField(country, "saldo", startingBalance);
            countries.Add(country);

            aircraftPrefab = new GameObject("NovaIA_NoAirport_Fighter");
            item = ScriptableObject.CreateInstance(definitionType);
            item.name = "NovaIA_NoAirport_Fighter";
            SetField(item, "prefabDaUnidade", aircraftPrefab);
            SetField(item, "categoria", Enum.Parse(
                definitionType.GetNestedType("CategoriaItem", BindingFlags.Public),
                "Aeronautica"));
            SetField(item, "precoDefinitivo", aircraftPrice);
            IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(definitionType));
            catalog.Add(item);
            catalogField.SetValue(null, catalog);

            controllerRoot = new GameObject("NovaIA no-airport treasury test controller");
            controllerRoot.SetActive(false);
            Component controller = controllerRoot.AddComponent(controllerType);
            SetField(controller, "_teamId", teamId);

            MethodInfo resolveItem = controllerType.GetMethod("TryResolveCatalogItem", InstanceMembers);
            object[] resolveArguments = { item.name, null };
            Assert.That(resolveItem.Invoke(controller, resolveArguments), Is.EqualTo(true));
            Assert.That(resolveArguments[1], Is.SameAs(item));

            Type productionGuardType = ResolveType("Hegemonia.AI.IA01.IA01MilitaryProductionGuard");
            string unitType = productionGuardType.GetMethod("Classify", StaticMembers)
                .Invoke(null, new object[] { item }).ToString();
            Type registryType = ResolveType("Hegemonia.AI.Shared.IAAutoProductionRegistry");
            Type orderStateType = ResolveType("Hegemonia.AI.Shared.IAProductionOrderState");
            object releasedState = Enum.Parse(orderStateType, "Released");
            MethodInfo countOrders = registryType.GetMethod("Count", StaticMembers);
            object[] countArguments = { teamId, unitType, releasedState };
            int releasedBefore = (int)countOrders.Invoke(null, countArguments);

            MethodInfo queueProduction = controllerType.GetMethod(
                "TryQueueProduction",
                InstanceMembers,
                null,
                new[] { typeof(int), typeof(string), typeof(int) },
                null);
            bool accepted = (bool)queueProduction.Invoke(controller, new object[] { teamId, item.name, 100 });

            Assert.That(accepted, Is.False, "Sem aeroporto do time, a rota NovaIA deve recusar o spawn.");
            Assert.That(GetField(country, "saldo"), Is.EqualTo(startingBalance), "A recusa deve estornar todo o valor cobrado.");
            Assert.That(GameObject.Find(aircraftPrefab.name + " (Clone)"), Is.Null, "A recusa não deve deixar aeronave criada.");
            Assert.That((int)countOrders.Invoke(null, countArguments), Is.EqualTo(releasedBefore + 1),
                "A reserva criada para a tentativa recusada deve ser liberada.");
            MethodInfo findActiveOrder = registryType.GetMethod("FindActiveOrder", StaticMembers);
            Assert.That((string)findActiveOrder.Invoke(null, new object[] { teamId, unitType, "novaia" }), Is.Empty,
                "Nenhuma reserva NovaIA pode permanecer ativa após a recusa.");

            // Exercise an exception after the reservation is created. The registry
            // must roll back with the treasury even when infrastructure throws.
            FieldInfo airportBufferField = controllerType.GetField("_airportBuffer", InstanceMembers);
            Assert.That(airportBufferField, Is.Not.Null);
            object originalAirportBuffer = airportBufferField.GetValue(controller);
            Assert.That(originalAirportBuffer, Is.Not.Null);
            object reservedState = Enum.Parse(orderStateType, "Reserved");
            object[] reservedCountArguments = { teamId, unitType, reservedState };
            int reservedBeforeException = (int)countOrders.Invoke(null, reservedCountArguments);

            try
            {
                airportBufferField.SetValue(controller, null);
                TargetInvocationException infrastructureFailure = Assert.Throws<TargetInvocationException>(
                    () => queueProduction.Invoke(controller, new object[] { teamId, item.name, 100 }));
                Assert.That(infrastructureFailure.InnerException, Is.TypeOf<NullReferenceException>(),
                    "A falha injetada deve ocorrer ao enumerar o cache da infraestrutura, depois da reserva.");
                Assert.That(GetField(country, "saldo"), Is.EqualTo(startingBalance),
                    "Uma exceção de infraestrutura deve devolver o débito do tesouro.");
                Assert.That((string)findActiveOrder.Invoke(null, new object[] { teamId, unitType, "novaia" }), Is.Empty,
                    "A exceção não pode deixar uma reserva NovaIA ativa.");
                Assert.That((int)countOrders.Invoke(null, reservedCountArguments), Is.EqualTo(reservedBeforeException),
                    "A ordem reservada antes da exceção deve ser liberada.");
            }
            finally
            {
                string leakedOrder = (string)findActiveOrder.Invoke(null, new object[] { teamId, unitType, "novaia" });
                if (!string.IsNullOrEmpty(leakedOrder))
                {
                    registryType.GetMethod("Release", StaticMembers).Invoke(null, new object[] { leakedOrder, Time.time });
                }
                airportBufferField.SetValue(controller, originalAirportBuffer);
            }
        }
        finally
        {
            catalogField.SetValue(null, previousCatalog);
            setGovernment.Invoke(null, new[] { previousGovernment });
            if (controllerRoot != null) UnityEngine.Object.DestroyImmediate(controllerRoot);
            if (governmentRoot != null) UnityEngine.Object.DestroyImmediate(governmentRoot);
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
            if (aircraftPrefab != null) UnityEngine.Object.DestroyImmediate(aircraftPrefab);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator C700CatalogPrefabUsesAerialClassificationAndTwoPersonDefault()
    {
        ScriptableObject catalogEntry = Resources.Load<ScriptableObject>("Construcoes/C700");
        Assert.That(catalogEntry, Is.Not.Null,
            "A ficha C700 precisa estar disponível no catálogo de Resources.");

        GameObject prefab = GetField(catalogEntry, "prefabDaUnidade") as GameObject;
        Assert.That(prefab, Is.Not.Null, "A ficha C700 precisa apontar para o prefab de transporte.");

        Component identity = prefab.GetComponent(ResolveType("IdentidadeUnidade"));
        Assert.That(identity, Is.Not.Null, "O prefab C700 precisa ter identidade militar.");
        Assert.That(GetField(identity, "tipoUnidade").ToString(), Is.EqualTo("Aereo"),
            "A pré-validação do aeroporto precisa reconhecer o C700 como aeronave antes do spawn.");

        Type governmentType = ResolveType("SistemaGovernoMundial");
        MethodInfo getEffective = governmentType.GetMethod(
            "ObterEfetivoNecessario",
            StaticMembers,
            null,
            new[] { typeof(GameObject) },
            null);
        Assert.That(getEffective, Is.Not.Null,
            "A identidade do C700 deve ser avaliada pela política central de efetivo.");
        Assert.That(getEffective.Invoke(null, new object[] { prefab }), Is.EqualTo(2),
            "O efetivo padrão aéreo do C700 deve ser contado antes de instanciar o transporte.");

        yield return null;
    }

    [UnityTest]
    public IEnumerator DestroyingMobilizedUnitAddsItsCrewToNationalCasualties()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        object previousGovernment = instanceProperty.GetValue(null, null);
        MethodInfo setGovernment = instanceProperty.GetSetMethod(true);
        GameObject governmentRoot = null;
        GameObject unitRoot = null;
        object government = previousGovernment;
        object testCountry = null;
        IList countries = null;

        try
        {
            if (government == null)
            {
                governmentRoot = new GameObject("IA03 casualty test government");
                governmentRoot.SetActive(false);
                government = governmentRoot.AddComponent(governmentType);
                setGovernment.Invoke(null, new[] { government });
            }

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetField(testCountry, "teamId", CasualtyTestTeamId);
            SetField(testCountry, "populacaoCivil", 100);
            SetField(testCountry, "populacaoMilitarAtiva", 3);
            SetField(testCountry, "reservistas", 2);
            SetField(testCountry, "alistaveis", 1);
            SetField(testCountry, "populacaoMaxima", 1000);
            SetField(testCountry, "populacao", 106);
            SetField(testCountry, "mortosAcumulados", 7);
            countries.Add(testCountry);

            unitRoot = new GameObject("IA03 mobilized unit casualty test");
            unitRoot.SetActive(false);
            Component identity = unitRoot.AddComponent(identityType);
            SetField(identity, "teamID", CasualtyTestTeamId);
            SetField(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Veiculo"));
            SetField(identity, "militaresConsumidos", 3);
            unitRoot.SetActive(true);

            UnityEngine.Object.DestroyImmediate(unitRoot);
            unitRoot = null;

            Assert.That(GetField(testCountry, "mortosAcumulados"), Is.EqualTo(10),
                "A perda deve registrar as três baixas no censo nacional existente.");
            Assert.That(GetField(testCountry, "populacaoMilitarAtiva"), Is.EqualTo(0),
                "O efetivo mobilizado da unidade destruída deve deixar de contar como ativo.");
            Assert.That(GetField(testCountry, "populacao"), Is.EqualTo(103),
                "A população recalculada deve preservar civis, reservistas e alistáveis.");
        }
        finally
        {
            if (unitRoot != null)
            {
                Component identity = unitRoot.GetComponent(identityType);
                if (identity != null) SetField(identity, "militaresConsumidos", 0);
                UnityEngine.Object.DestroyImmediate(unitRoot);
            }

            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator NavalYardKeepsCompletedSlotRetryableUntilManpowerReturns()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type unitType = ResolveType("TipoUnidade");
        Type yardType = ResolveType("Estaleiro");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", StaticMembers);
        MethodInfo setGovernment = governmentInstance.GetSetMethod(true);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject governmentRoot = null;
        GameObject yardRoot = null;
        GameObject prefab = null;
        GameObject point = null;
        object country = null;
        IList countries = null;

        try
        {
            const int teamId = 2941997;
            governmentRoot = new GameObject("IA03 naval retry manpower government");
            governmentRoot.SetActive(false);
            Component government = governmentRoot.AddComponent(governmentType);
            setGovernment.Invoke(null, new[] { government });
            countries = (IList)GetField(government, "paises");
            country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            SetField(country, "alistaveis", 0);
            SetField(country, "populacaoCivil", 0);
            SetField(country, "populacaoMilitarAtiva", 0);
            countries.Add(country);

            yardRoot = new GameObject("IA03 naval retry manpower yard");
            yardRoot.SetActive(false);
            Component identity = yardRoot.AddComponent(identityType);
            SetField(identity, "teamID", teamId);
            Component yard = yardRoot.AddComponent(yardType);
            yardType.GetProperty("OwnerTeamId").SetValue(yard, teamId, null);
            Type slotType = yardType.GetNestedType("SlotConstrucao", BindingFlags.Public);
            object slot = Activator.CreateInstance(slotType);
            point = new GameObject("IA03 naval retry construction point");
            point.transform.SetParent(yardRoot.transform, false);
            prefab = new GameObject("IA03 naval retry ship prefab");
            Component prefabIdentity = prefab.AddComponent(identityType);
            SetField(prefabIdentity, "tipoUnidade", Enum.Parse(unitType, "Naval"));
            SetField(prefabIdentity, "militaresConsumidos", 1);
            SetField(slot, "nomeSlot", "RetrySlot");
            SetField(slot, "pontoDeConstrucao", point.transform);
            SetField(slot, "estaOcupado", true);
            SetField(slot, "progresso", 100f);
            SetField(slot, "prefabAtual", prefab);
            SetField(slot, "productionOrderId", "ia03-naval-manpower-retry");
            Array slots = Array.CreateInstance(slotType, 1);
            slots.SetValue(slot, 0);
            SetField(yard, "slots", slots);

            yardRoot.SetActive(true);
            yield return null;

            Invoke(yard, "FinalizarConstrucao", slot);
            Assert.That(GetField(slot, "estaOcupado"), Is.EqualTo(true));
            Assert.That(GetField(slot, "progresso"), Is.EqualTo(100f));

            SetField(country, "alistaveis", 1);
            Invoke(yard, "FinalizarConstrucao", slot);
            Assert.That(GetField(slot, "estaOcupado"), Is.EqualTo(false));
            Assert.That(GetField(slot, "prefabAtual"), Is.Null);
            Assert.That(GetField(country, "populacaoMilitarAtiva"), Is.EqualTo(1));
        }
        finally
        {
            if (yardRoot != null) UnityEngine.Object.DestroyImmediate(yardRoot);
            if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
            if (point != null) UnityEngine.Object.DestroyImmediate(point);
            if (countries != null && country != null) countries.Remove(country);
            if (governmentRoot != null)
            {
                setGovernment.Invoke(null, new[] { previousGovernment });
                UnityEngine.Object.DestroyImmediate(governmentRoot);
            }
        }
    }

    private static Type ResolveType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }

        Assert.Fail("Tipo não encontrado: " + name);
        return null;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        // Select the reflected overload from the arguments instead of resolving by name alone.
        MethodInfo method = null;
        MethodInfo[] candidates = target.GetType().GetMethods(InstanceMembers);
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            MethodInfo candidate = candidates[candidateIndex];
            if (!string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            bool matches = true;
            for (int argumentIndex = 0; argumentIndex < parameters.Length; argumentIndex++)
            {
                object argument = args[argumentIndex];
                Type parameterType = parameters[argumentIndex].ParameterType;
                if (argument == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        matches = false;
                        break;
                    }
                }
                else if (!parameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            Assert.That(method, Is.Null, "Mais de um overload corresponde à chamada: " + name);
            method = candidate;
        }

        Assert.That(method, Is.Not.Null, "Método compatível não encontrado: " + name);
        return method.Invoke(target, args);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        return field.GetValue(target);
    }
}
