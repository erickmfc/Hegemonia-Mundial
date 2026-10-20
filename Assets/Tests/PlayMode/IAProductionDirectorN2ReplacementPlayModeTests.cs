using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

public sealed class IAProductionDirectorN2ReplacementPlayModeTests
{
    private const int TestTeamId = 2941937;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator LosingTankUpdatesLiveSnapshotAndQueuesOneReplacementWithoutSpawningDirectly()
    {
        var createdObjects = new List<GameObject>();
        object world = null;
        object backend = null;
        UnityEngine.Object tankData = null;
        string productionOrderId = string.Empty;

        try
        {
            GameObject factory = CreateEntity("Factory N2 replacement fixture", false, createdObjects);
            GameObject militaryAirport = CreateEntity("Military Airport N2 replacement fixture", false, createdObjects);
            GameObject commercialAirport = CreateEntity("Commercial Airport N2 replacement fixture", false, createdObjects);
            GameObject shipyard = CreateEntity("Shipyard N2 replacement fixture", false, createdObjects);

            GameObject survivingTank = CreateEntity("Tank South N2 survivor", true, createdObjects);
            GameObject lostTank = CreateEntity("Tank South N2 combat loss", true, createdObjects);
            yield return null;

            Type worldType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
            world = Activator.CreateInstance(worldType, new object[] { TestTeamId });
            Invoke(world, "Tick", 100f, 0.1f);

            object snapshot = GetMember(world, "ForceSnapshot");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "TankUnits")), Is.EqualTo(2), "A força inicial da fixture deve conter dois tanques reais registrados.");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "FactoryCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "MilitaryAirportCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "CommercialAirportCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "ShipyardCount")), Is.EqualTo(1));

            UnityEngine.Object.Destroy(lostTank);
            createdObjects.Remove(lostTank);
            yield return null;
            Invoke(world, "Tick", 101f, 0.1f);

            snapshot = GetMember(world, "ForceSnapshot");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "TankUnits")), Is.EqualTo(1), "A perda deve aparecer na contagem recalculada do WorldState.");

            // These support counts satisfy higher-priority production lanes so
            // this regression isolates the post-N2 tank replacement decision.
            SetMember(snapshot, "InfantryUnits", 12);
            SetMember(snapshot, "ArtilleryUnits", 1);
            SetMember(snapshot, "FixedWingAircraft", 8);
            SetMember(snapshot, "NavalUnits", 15);
            SetMember(snapshot, "Submarines", 1);
            SetMember(snapshot, "GroundTransports", 1);

            GameObject brainObject = new GameObject("Inactive BrainMaster for N2 replacement test");
            createdObjects.Add(brainObject);
            brainObject.SetActive(false);
            Component brain = brainObject.AddComponent(ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            SetMember(brain, "enabled", false);
            SetMember(brain, "TeamId", TestTeamId);

            Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
            object queue = Activator.CreateInstance(queueType);
            SetMember(queue, "TraceTeamId", TestTeamId);

            Type backendType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
            backend = Activator.CreateInstance(backendType, new object[] { TestTeamId });
            tankData = ScriptableObject.CreateInstance(ResolveType("DadosConstrucao"));
            SetMember(tankData, "name", "Tank South N2 replacement test data");
            SetMember(tankData, "NomeItem", "Tank South");
            SetMember(tankData, "aliases", "tank mbt, mbt");
            SetMember(tankData, "categoria", Enum.Parse(ResolveType("DadosConstrucao+CategoriaItem"), "Exercito"));
            GameObject catalogPrefab = new GameObject("Tank South catalog marker");
            createdObjects.Add(catalogPrefab);
            SetMember(tankData, "PrefabDaUnidade", catalogPrefab);

            MethodInfo addCatalogItem = backendType.GetMethod("AddCatalogItem", InstanceMembers);
            Assert.That(addCatalogItem, Is.Not.Null, "O backend deve manter o registro central de catálogo usado pela produção.");
            addCatalogItem.Invoke(backend, new[] { tankData });

            Type contextType = ResolveType("Hegemonia.AI.BrainMaster.IA_Context");
            object context = Activator.CreateInstance(contextType);
            SetMember(context, "Brain", brain);
            SetMember(context, "WorldState", world);
            SetMember(context, "ForceSnapshot", snapshot);
            SetMember(context, "PlayerProfileMemory", Activator.CreateInstance(
                ResolveType("Hegemonia.AI.BrainMaster.IA_PlayerProfileMemory"), new[] { world }));
            SetMember(context, "CommandQueue", queue);
            SetMember(context, "Backend", backend);

            object director = Activator.CreateInstance(
                ResolveType("Hegemonia.AI.BrainMaster.IA_ProductionDirector"), new[] { context });

            Invoke(director, "Tick", 200f, 0.65f);
            Assert.That(Convert.ToInt32(GetMember(queue, "PendingCount")), Is.EqualTo(1), "O déficit pós-perda deve gerar uma ordem pela fila central.");

            Invoke(director, "Tick", 201f, 0.65f);
            Assert.That(Convert.ToInt32(GetMember(queue, "PendingCount")), Is.EqualTo(1), "A perda ainda pendente não pode gerar pedidos duplicados em ticks seguintes.");

            MethodInfo tryDequeue = queueType.GetMethod("TryDequeue", InstanceMembers);
            object[] dequeueArgs = { Time.time, null };
            Assert.That(Convert.ToBoolean(tryDequeue.Invoke(queue, dequeueArgs)), Is.True);
            object request = dequeueArgs[1];
            Assert.That(GetMember(request, "Type").ToString(), Is.EqualTo("Produce"));
            Assert.That(GetMember(request, "Origin"), Is.EqualTo("IA_ProductionDirector"));

            object payload = GetMember(request, "Payload");
            Assert.That(payload.GetType().Name, Is.EqualTo("IA_ProduceOrderData"));
            Assert.That(GetMember(payload, "ItemKey"), Is.EqualTo("Tank South"));
            Assert.That(Convert.ToInt32(GetMember(payload, "Quantity")), Is.EqualTo(1));
            Assert.That(GetMember(payload, "ProductionOrderId"), Is.Not.Empty);
            productionOrderId = (string)GetMember(payload, "ProductionOrderId");

            Assert.That(survivingTank, Is.Not.Null, "O pedido não deve criar/spawnar uma unidade diretamente.");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "TankUnits")), Is.EqualTo(1), "A quantidade só muda quando a rota de produção materializa a unidade.");
        }
        finally
        {
            CancelTankReservation();
            if (!string.IsNullOrEmpty(productionOrderId)) ReleaseTankReservation(productionOrderId);

            if (world != null)
            {
                var baseProbe = GetMember(world, "_baseProbe") as Transform;
                if (baseProbe != null) UnityEngine.Object.DestroyImmediate(baseProbe.gameObject);
            }

            if (tankData != null) UnityEngine.Object.DestroyImmediate(tankData);

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
    }

    [UnityTest]
    public IEnumerator N2TankLossQueuesAndProducesOneReplacementThroughNationalEconomy()
    {
        const int teamId = TestTeamId + 1;
        var createdObjects = new List<GameObject>();
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type countryType = ResolveType("DadosPaisGoverno");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type backendType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type menuType = ResolveType("MenuConstrucao");
        FieldInfo sharedCatalogField = menuType.GetField("catalogoGlobal", StaticMembers);
        Assert.That(sharedCatalogField, Is.Not.Null);

        object previousSharedCatalog = sharedCatalogField.GetValue(null);
        object previousGovernment = governmentType.GetProperty("Instancia", StaticMembers).GetValue(null, null);
        object government = previousGovernment;
        object industrial = null;
        object inventoryBefore = null;
        object testCountry = null;
        IList countries = null;
        object backend = null;
        object world = null;
        object tankData = null;
        GameObject producedTank = null;
        string productionOrderId = string.Empty;
        string dieselId = string.Empty;
        bool catalogWasReplaced = false;
        EconomyScheduleSnapshot economySchedule = default(EconomyScheduleSnapshot);
        long previousFinancialReserve = 0L;
        long officialTankPrice = 0L;
        int tankCrew = 0;
        float tankFuelCapacity = 0f;
        double fuelStockBeforeLaunch = 0d;

        try
        {
            governmentType.GetMethod("GarantirInstancia", StaticMembers).Invoke(null, null);
            yield return null;
            government = governmentType.GetProperty("Instancia", StaticMembers).GetValue(null, null);
            Assert.That(government, Is.Not.Null);
            economySchedule = CongelarEconomiaGlobal(government);

            countries = (IList)governmentType.GetField("paises", InstanceMembers).GetValue(government);
            testCountry = Activator.CreateInstance(countryType);
            SetMember(testCountry, "teamId", teamId);
            SetMember(testCountry, "saldo", 0L);
            SetMember(testCountry, "alistaveis", 0);
            SetMember(testCountry, "populacaoCivil", 0);
            SetMember(testCountry, "populacaoMilitarAtiva", 0);
            countries.Add(testCountry);
            previousFinancialReserve = Convert.ToInt64(Invoke(government, "ObterReservaFinanceiraEstrategica", teamId));
            Invoke(government, "DefinirReservaFinanceiraEstrategica", teamId, 0L);

            industrial = ResolveType("SistemaIndustrialNacional").GetProperty("Instancia", StaticMembers).GetValue(null, null);
            Assert.That(industrial, Is.Not.Null);
            Invoke(industrial, "GarantirPaisExistentes");
            inventoryBefore = Invoke(industrial, "CriarSaveData");
            Type industryIdsType = ResolveType("IndustriaIds");
            dieselId = (string)industryIdsType.GetField("Diesel", StaticMembers).GetValue(null);

            // Discover the authored item through the Editor catalog fallback,
            // then give the production bridge only this real catalog entry.
            sharedCatalogField.SetValue(null, null);
            catalogWasReplaced = true;
            object catalogReader = Activator.CreateInstance(backendType, new object[] { teamId });
            Invoke(catalogReader, "RefreshCatalog");
            IEnumerable catalog = (IEnumerable)GetMember(catalogReader, "Catalog");
            UnityEngine.Object authoredTankData = null;
            foreach (object item in catalog)
            {
                if (item == null) continue;
                string displayName = Convert.ToString(Invoke(item, "GetDisplayName"));
                string itemName = Convert.ToString(GetMember(item, "NomeItem"));
                GameObject candidatePrefab = GetMember(item, "PrefabDaUnidade") as GameObject;
                if (candidatePrefab == null) continue;

                bool isTankSouth = string.Equals(displayName, "Tank South", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemName, "Tank South", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidatePrefab.name, "Tank_Unity", StringComparison.OrdinalIgnoreCase);
                if (isTankSouth && GetMember(item, "categoria").ToString() == "Exercito")
                {
                    authoredTankData = item as UnityEngine.Object;
                    break;
                }
            }

            Assert.That(authoredTankData, Is.Not.Null, "O catálogo deve fornecer a ficha real de Tank South para a reposição.");
            tankData = UnityEngine.Object.Instantiate(authoredTankData);
            GameObject authoredPrefab = GetMember(tankData, "PrefabDaUnidade") as GameObject;
            Assert.That(authoredPrefab, Is.Not.Null);
            GameObject prefabStaging = new GameObject("Inactive Tank South replacement prefab staging");
            createdObjects.Add(prefabStaging);
            prefabStaging.SetActive(false);
            GameObject tankPrefab = UnityEngine.Object.Instantiate(authoredPrefab, prefabStaging.transform);
            tankPrefab.name = authoredPrefab.name;
            UnityEngine.AI.NavMeshAgent[] prefabAgents = tankPrefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
            for (int i = 0; i < prefabAgents.Length; i++)
            {
                if (prefabAgents[i] != null) prefabAgents[i].enabled = false;
            }
            SetMember(tankData, "PrefabDaUnidade", tankPrefab);
            officialTankPrice = Convert.ToInt64(Invoke(tankData, "ObterPrecoEfetivo"));
            MethodInfo crewMethod = governmentType.GetMethod(
                "ObterEfetivoNecessario",
                StaticMembers,
                null,
                new[] { typeof(GameObject) },
                null);
            Assert.That(crewMethod, Is.Not.Null);
            tankCrew = Convert.ToInt32(crewMethod.Invoke(null, new object[] { tankPrefab }));
            Component prefabFuel = tankPrefab.GetComponentInChildren(ResolveType("CombustivelUnidade"), true);
            Assert.That(officialTankPrice, Is.GreaterThan(0L));
            Assert.That(tankCrew, Is.GreaterThan(0));
            if (prefabFuel != null)
            {
                tankFuelCapacity = Convert.ToSingle(GetMember(prefabFuel, "Capacidade"));
            }
            else
            {
                // Legacy Tank South has no serialized fuel component; the real
                // production service adds one from the vehicle identity. Read
                // that existing default on a temporary probe for this fixture.
                GameObject fuelProbeRoot = new GameObject("Tank South replacement fuel capacity probe");
                createdObjects.Add(fuelProbeRoot);
                Component probeIdentity = fuelProbeRoot.AddComponent(identityType);
                SetMember(probeIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Veiculo"));
                Component fuelProbe = fuelProbeRoot.AddComponent(ResolveType("CombustivelUnidade"));
                Invoke(fuelProbe, "ConfigurarSeNecessario", false);
                tankFuelCapacity = Convert.ToSingle(GetMember(fuelProbe, "Capacidade"));
            }
            Assert.That(tankFuelCapacity, Is.GreaterThan(0f));

            SetMember(testCountry, "saldo", officialTankPrice);
            SetMember(testCountry, "alistaveis", tankCrew);

            double oldFuel = Convert.ToDouble(Invoke(industrial, "ObterQuantidadePais", teamId, dieselId));
            if (oldFuel > 0d)
            {
                Invoke(industrial, "TentarConsumir", teamId.ToString(), dieselId, oldFuel);
            }
            Invoke(industrial, "TentarAdicionar", teamId.ToString(), dieselId, (double)tankFuelCapacity);
            fuelStockBeforeLaunch = Convert.ToDouble(Invoke(industrial, "ObterQuantidadePais", teamId, dieselId));
            Assert.That(fuelStockBeforeLaunch, Is.EqualTo(tankFuelCapacity).Within(0.01d));

            backend = Activator.CreateInstance(backendType, new object[] { teamId });
            MethodInfo addCatalogItem = backendType.GetMethod("AddCatalogItem", InstanceMembers);
            Assert.That(addCatalogItem, Is.Not.Null);
            addCatalogItem.Invoke(backend, new[] { tankData });

            GameObject factoryRoot = new GameObject("Factory N2 replacement actual producer");
            createdObjects.Add(factoryRoot);
            factoryRoot.SetActive(false);
            Component factoryIdentity = factoryRoot.AddComponent(identityType);
            SetMember(factoryIdentity, "teamID", teamId);
            SetMember(factoryIdentity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Estrutura"));
            Component factoryEconomy = factoryRoot.AddComponent(ResolveType("EstruturaEconomica"));
            SetMember(factoryEconomy, "status", Enum.Parse(ResolveType("StatusEstruturaEconomica"), "Ativa"));
            SetMember(factoryEconomy, "eficiencia", 1f);
            Component factory = factoryRoot.AddComponent(ResolveType("Fabrica"));
            SetMember(factory, "ehQuartel", false);
            factoryRoot.SetActive(true);
            yield return null;
            Assert.That((bool)Invoke(factory, "PossuiEnergiaParaProduzir"), Is.True,
                "A reposição usa uma fábrica que satisfaz o gate de energia operacional.");

            CreateEntity("Military Airport N2 replacement fixture", false, createdObjects, teamId);
            CreateEntity("Commercial Airport N2 replacement fixture", false, createdObjects, teamId);
            CreateEntity("Shipyard N2 replacement fixture", false, createdObjects, teamId);
            GameObject survivingTank = CreateEntity("Tank South N2 survivor", true, createdObjects, teamId);
            GameObject lostTank = CreateEntity("Tank South N2 combat loss", true, createdObjects, teamId);
            SetMember(survivingTank.GetComponent(identityType), "militaresConsumidos", tankCrew);
            SetMember(lostTank.GetComponent(identityType), "militaresConsumidos", tankCrew);
            SetMember(testCountry, "populacaoMilitarAtiva", tankCrew * 2);

            yield return null;
            Type worldType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
            world = Activator.CreateInstance(worldType, new object[] { teamId });
            Invoke(world, "Tick", 100f, 0.1f);
            object snapshot = GetMember(world, "ForceSnapshot");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "TankUnits")), Is.EqualTo(2));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "FactoryCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "MilitaryAirportCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "CommercialAirportCount")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(GetMember(snapshot, "ShipyardCount")), Is.EqualTo(1));

            UnityEngine.Object.Destroy(lostTank);
            createdObjects.Remove(lostTank);
            yield return null;
            Invoke(world, "Tick", 101f, 0.1f);
            snapshot = GetMember(world, "ForceSnapshot");
            Assert.That(Convert.ToInt32(GetMember(snapshot, "TankUnits")), Is.EqualTo(1),
                "A perda da unidade própria durante o cenário N2 precisa chegar ao snapshot ao vivo.");
            Assert.That(Convert.ToInt32(GetMember(testCountry, "mortosAcumulados")), Is.EqualTo(tankCrew));
            Assert.That(Convert.ToInt32(GetMember(testCountry, "populacaoMilitarAtiva")), Is.EqualTo(tankCrew));

            SetMember(snapshot, "InfantryUnits", 12);
            SetMember(snapshot, "ArtilleryUnits", 1);
            SetMember(snapshot, "FixedWingAircraft", 8);
            SetMember(snapshot, "NavalUnits", 15);
            SetMember(snapshot, "Submarines", 1);
            SetMember(snapshot, "GroundTransports", 1);

            GameObject brainObject = new GameObject("Inactive BrainMaster for paid N2 replacement");
            createdObjects.Add(brainObject);
            brainObject.SetActive(false);
            Component brain = brainObject.AddComponent(ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            SetMember(brain, "enabled", false);
            SetMember(brain, "TeamId", teamId);
            SetMember(brain, "Credits", 0);
            Invoke(brain, "TickEconomy", 1f);

            Type queueType = ResolveType("Hegemonia.AI.BrainMaster.IA_CommandQueue");
            object queue = Activator.CreateInstance(queueType);
            SetMember(queue, "TraceTeamId", teamId);
            Type contextType = ResolveType("Hegemonia.AI.BrainMaster.IA_Context");
            object context = Activator.CreateInstance(contextType);
            SetMember(context, "Brain", brain);
            SetMember(context, "WorldState", world);
            SetMember(context, "ForceSnapshot", snapshot);
            SetMember(context, "PlayerProfileMemory", Activator.CreateInstance(
                ResolveType("Hegemonia.AI.BrainMaster.IA_PlayerProfileMemory"), new[] { world }));
            SetMember(context, "CommandQueue", queue);
            SetMember(context, "Backend", backend);
            object director = Activator.CreateInstance(
                ResolveType("Hegemonia.AI.BrainMaster.IA_ProductionDirector"), new[] { context });

            Invoke(director, "Tick", 200f, 0.65f);
            Assert.That(Convert.ToInt32(GetMember(queue, "PendingCount")), Is.EqualTo(1),
                "A baixa N2 deve gerar uma única ordem de reposição pela fila central.");
            Invoke(director, "Tick", 201f, 0.65f);
            Assert.That(Convert.ToInt32(GetMember(queue, "PendingCount")), Is.EqualTo(1),
                "O déficit ainda pendente não pode duplicar o pedido.");

            MethodInfo tryDequeue = queueType.GetMethod("TryDequeue", InstanceMembers);
            object[] dequeueArguments = { Time.time, null };
            Assert.That(Convert.ToBoolean(tryDequeue.Invoke(queue, dequeueArguments)), Is.True);
            object request = dequeueArguments[1];
            object payload = GetMember(request, "Payload");
            productionOrderId = (string)GetMember(payload, "ProductionOrderId");
            Assert.That(GetMember(payload, "ItemKey"), Is.EqualTo(Invoke(tankData, "GetDisplayName")));
            Assert.That(productionOrderId, Is.Not.Empty);

            long availableTreasuryBeforeProduction = Convert.ToInt64(
                Invoke(government, "ObterSaldoDisponivelParaOperacao", teamId));
            long foundationReserveBeforeProduction = Convert.ToInt64(
                Invoke(government, "ObterReservaFundacao", teamId));
            long strategicReserveBeforeProduction = Convert.ToInt64(
                Invoke(government, "ObterReservaFinanceiraEstrategica", teamId));
            Assert.That(Convert.ToInt32(GetMember(brain, "TeamId")), Is.EqualTo(teamId));
            Assert.That(Invoke(government, "ObterPais", teamId), Is.SameAs(testCountry),
                "O pagamento deve consultar o país temporário criado pela fixture.");
            Assert.That(Convert.ToInt32(GetMember(government, "teamJogador")), Is.Not.EqualTo(teamId),
                "O teste deve passar pela conta nacional de IA, não pelo ledger do jogador.");
            Assert.That(availableTreasuryBeforeProduction, Is.GreaterThanOrEqualTo(officialTankPrice),
                "Saldo operacional insuficiente antes do comando: saldo="
                + GetMember(testCountry, "saldo")
                + " preço=" + officialTankPrice
                + " reservaFundacao=" + foundationReserveBeforeProduction
                + " reservaEstrategica=" + strategicReserveBeforeProduction);

            object commandService = GetMember(backend, "CommandService");
            MethodInfo execute = commandService.GetType().GetMethod("Execute", InstanceMembers);
            object[] executeArguments = { request, context, null };
            bool produced = (bool)execute.Invoke(commandService, executeArguments);
            Assert.That(produced, Is.True, Convert.ToString(executeArguments[2]));

            Assert.That(Convert.ToInt64(GetMember(testCountry, "saldo")), Is.EqualTo(0L),
                "O pedido de reposição deve debitar do tesouro nacional exatamente o preço oficial da ficha.");
            Assert.That(Convert.ToInt32(GetMember(testCountry, "alistaveis")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(GetMember(testCountry, "populacaoMilitarAtiva")), Is.EqualTo(tankCrew * 2));
            producedTank = GameObject.Find(tankPrefab.name + "(Clone)");
            Assert.That(producedTank, Is.Not.Null, "A reposição precisa nascer pela fábrica real após a ordem paga.");
            createdObjects.Add(producedTank);

            Component producedIdentity = producedTank.GetComponent(identityType)
                ?? producedTank.GetComponentInChildren(identityType, true);
            Assert.That(Convert.ToInt32(GetMember(producedIdentity, "teamID")), Is.EqualTo(teamId));
            Assert.That(GetMember(producedIdentity, "tipoUnidade").ToString(), Is.EqualTo("Veiculo"));
            Assert.That(Convert.ToInt32(GetMember(producedIdentity, "militaresConsumidos")), Is.EqualTo(tankCrew));
            Assert.That(Convert.ToInt32(GetMember(testCountry, "mortosAcumulados")), Is.EqualTo(tankCrew));

            Component producedFuel = producedTank.GetComponentInChildren(ResolveType("CombustivelUnidade"), true);
            Assert.That(producedFuel, Is.Not.Null);
            yield return null;
            float loadedFuel = Convert.ToSingle(GetMember(producedFuel, "combustivelAtual"));
            double fuelStockAfterLaunch = Convert.ToDouble(Invoke(industrial, "ObterQuantidadePais", teamId, dieselId));
            Assert.That(loadedFuel, Is.GreaterThan(0f));
            Assert.That(fuelStockBeforeLaunch - fuelStockAfterLaunch, Is.EqualTo(loadedFuel).Within(0.1d),
                "O tanque da reposição deve receber combustível retirado do estoque nacional do mesmo país.");

            Invoke(world, "Tick", 202f, 0.1f);
            Assert.That(Convert.ToInt32(GetMember(GetMember(world, "ForceSnapshot"), "TankUnits")), Is.EqualTo(2),
                "O WorldState deve contar o sobrevivente e a reposição materializada.");
        }
        finally
        {
            CancelTankReservation(teamId);
            if (!string.IsNullOrEmpty(productionOrderId)) ReleaseTankReservation(productionOrderId);

            if (producedTank != null)
            {
                Component[] identities = producedTank.GetComponentsInChildren(identityType, true);
                for (int i = 0; i < identities.Length; i++)
                {
                    if (identities[i] != null) SetMember(identities[i], "militaresConsumidos", 0);
                }
                UnityEngine.Object.DestroyImmediate(producedTank);
            }

            if (world != null)
            {
                var baseProbe = GetMember(world, "_baseProbe") as Transform;
                if (baseProbe != null) UnityEngine.Object.DestroyImmediate(baseProbe.gameObject);
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Component[] identities = createdObjects[i].GetComponentsInChildren(identityType, true);
                    for (int j = 0; j < identities.Length; j++)
                    {
                        if (identities[j] != null) SetMember(identities[j], "militaresConsumidos", 0);
                    }
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            if (tankData != null) UnityEngine.Object.DestroyImmediate((UnityEngine.Object)tankData);
            if (industrial != null && inventoryBefore != null) Invoke(industrial, "RestaurarSaveData", inventoryBefore);
            if (countries != null && testCountry != null) countries.Remove(testCountry);
            if (government != null) Invoke(government, "DefinirReservaFinanceiraEstrategica", teamId, previousFinancialReserve);
            RestaurarEconomiaGlobal(government, economySchedule);
            if (catalogWasReplaced) sharedCatalogField.SetValue(null, previousSharedCatalog);
        }

        yield return null;
    }

    private static GameObject CreateEntity(string name, bool mobile, List<GameObject> createdObjects, int teamId = TestTeamId)
    {
        var entity = new GameObject(name);
        entity.SetActive(false);
        createdObjects.Add(entity);
        Component identity = entity.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(identity, "teamID", teamId);
        SetMember(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), mobile ? "Veiculo" : "Estrutura"));

        if (mobile)
        {
            NavMeshAgent agent = entity.AddComponent<NavMeshAgent>();
            agent.enabled = false;
        }

        entity.SetActive(true);
        return entity;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
        Assert.That(method, Is.Not.Null, "Método ausente: " + target.GetType().FullName + "." + methodName);
        return method.Invoke(target, args);
    }

    private static object GetMember(object target, string name, params object[] args)
    {
        if (target is GameObject gameObject && name == "GetComponent")
        {
            return gameObject.GetComponent(args[0] as Type);
        }

        if (target == null) return null;
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null) return property.GetValue(target, null);
        return null;
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) return;
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return;
        }

        Assert.Fail("Campo/propriedade ausente: " + type.FullName + "." + name);
    }

    private static void CancelTankReservation(int teamId = TestTeamId)
    {
        Type guardType = ResolveType("Hegemonia.AI.IA01.IA01MilitaryProductionGuard");
        MethodInfo cancel = guardType.GetMethod("Cancel", StaticMembers);
        object tankKind = Enum.Parse(ResolveType("Hegemonia.AI.IA01.IA01MilitaryAssetKind"), "Tank");
        cancel.Invoke(null, new[] { (object)teamId, tankKind, Time.time });
    }

    private static void ReleaseTankReservation(string orderId)
    {
        Type guardType = ResolveType("Hegemonia.AI.IA01.IA01MilitaryProductionGuard");
        MethodInfo release = guardType.GetMethod("Release", StaticMembers);
        release.Invoke(null, new object[] { orderId, Time.time });
    }

    private struct EconomyScheduleSnapshot
    {
        public bool Valid;
        public float IntervaloEconomia;
        public float ProximoTick;
    }

    private static EconomyScheduleSnapshot CongelarEconomiaGlobal(object governo)
    {
        FieldInfo nextTick = governo.GetType().GetField("proximoTick", InstanceMembers);
        Assert.That(nextTick, Is.Not.Null, "O scheduler economico precisa ser localizavel para isolar a fixture N2.");
        EconomyScheduleSnapshot snapshot = new EconomyScheduleSnapshot
        {
            Valid = true,
            IntervaloEconomia = Convert.ToSingle(GetMember(governo, "intervaloEconomia")),
            ProximoTick = Convert.ToSingle(nextTick.GetValue(governo))
        };
        SetMember(governo, "intervaloEconomia", 999999f);
        nextTick.SetValue(governo, Time.unscaledTime + 999999f);
        return snapshot;
    }

    private static void RestaurarEconomiaGlobal(object governo, EconomyScheduleSnapshot snapshot)
    {
        if (!snapshot.Valid || governo == null)
            return;

        FieldInfo nextTick = governo.GetType().GetField("proximoTick", InstanceMembers);
        SetMember(governo, "intervaloEconomia", snapshot.IntervaloEconomia);
        if (nextTick != null)
            nextTick.SetValue(governo, snapshot.ProximoTick);
    }

    private static Type ResolveType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null) return type;
        }

        Assert.Fail("Tipo de runtime não encontrado: " + fullName);
        return null;
    }
}
