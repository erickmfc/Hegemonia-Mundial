using System.Collections.Generic;
using System.Reflection;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.IA01;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class IAProductionDirectorN2ReplacementEditorTests
{
    private const int TestTeamId = 2941937;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void LosingTankUpdatesLiveSnapshotAndQueuesOneReplacementWithoutSpawningDirectly()
    {
        var createdObjects = new List<GameObject>();
        IA_WorldState world = null;
        IA_BackendBridge backend = null;
        DadosConstrucao tankData = null;
        string productionOrderId = string.Empty;

        try
        {
            GameObject factory = CreateEntity("Factory N2 replacement fixture", false, createdObjects);
            GameObject militaryAirport = CreateEntity("Military Airport N2 replacement fixture", false, createdObjects);
            GameObject commercialAirport = CreateEntity("Commercial Airport N2 replacement fixture", false, createdObjects);
            GameObject shipyard = CreateEntity("Shipyard N2 replacement fixture", false, createdObjects);
            factory.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Estrutura;
            militaryAirport.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Estrutura;
            commercialAirport.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Estrutura;
            shipyard.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Estrutura;

            GameObject survivingTank = CreateEntity("Tank South N2 survivor", true, createdObjects);
            GameObject lostTank = CreateEntity("Tank South N2 combat loss", true, createdObjects);
            survivingTank.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Veiculo;
            lostTank.GetComponent<IdentidadeUnidade>().tipoUnidade = TipoUnidade.Veiculo;
            world = new IA_WorldState(TestTeamId);
            world.Tick(100f, 0.1f);

            Assert.That(world.ForceSnapshot.TankUnits, Is.EqualTo(2), "A força inicial da fixture deve conter dois tanques reais registrados.");
            Assert.That(world.ForceSnapshot.FactoryCount, Is.EqualTo(1));
            Assert.That(world.ForceSnapshot.MilitaryAirportCount, Is.EqualTo(1));
            Assert.That(world.ForceSnapshot.CommercialAirportCount, Is.EqualTo(1));
            Assert.That(world.ForceSnapshot.ShipyardCount, Is.EqualTo(1));

            IdentidadeUnidade lostIdentity = lostTank.GetComponent<IdentidadeUnidade>();
            RegistroEntidadesJogo.Unregister(lostIdentity);
            IA_WorldState.Unregister(lostIdentity);
            Object.DestroyImmediate(lostTank);
            createdObjects.Remove(lostTank);
            world.Tick(101f, 0.1f);

            IA_ForceSnapshot snapshot = world.ForceSnapshot;
            Assert.That(snapshot.TankUnits, Is.EqualTo(1), "A perda deve aparecer na contagem recalculada do WorldState.");

            // Other force counts satisfy higher-priority production lanes so this
            // regression isolates the N2 tank replacement decision.
            snapshot.InfantryUnits = 12;
            snapshot.ArtilleryUnits = 1;
            snapshot.FixedWingAircraft = 8;
            snapshot.NavalUnits = 15;
            snapshot.Submarines = 1;
            snapshot.GroundTransports = 1;

            GameObject brainObject = new GameObject("Inactive BrainMaster for N2 replacement test");
            createdObjects.Add(brainObject);
            brainObject.SetActive(false);
            IA_BrainMaster brain = brainObject.AddComponent<IA_BrainMaster>();
            brain.enabled = false;
            brain.TeamId = TestTeamId;

            var queue = new IA_CommandQueue { TraceTeamId = TestTeamId };
            backend = new IA_BackendBridge(TestTeamId);
            tankData = ScriptableObject.CreateInstance<DadosConstrucao>();
            tankData.name = "Tank South N2 replacement test data";
            tankData.NomeItem = "Tank South";
            tankData.aliases = "tank mbt, mbt";
            tankData.categoria = DadosConstrucao.CategoriaItem.Exercito;
            GameObject catalogPrefab = new GameObject("Tank South catalog marker");
            createdObjects.Add(catalogPrefab);
            tankData.PrefabDaUnidade = catalogPrefab;

            MethodInfo addCatalogItem = typeof(IA_BackendBridge).GetMethod("AddCatalogItem", InstanceMembers);
            Assert.That(addCatalogItem, Is.Not.Null, "O backend deve manter o registro central de catálogo usado pela produção.");
            addCatalogItem.Invoke(backend, new object[] { tankData });

            var context = new IA_Context
            {
                Brain = brain,
                WorldState = world,
                ForceSnapshot = snapshot,
                PlayerProfileMemory = new IA_PlayerProfileMemory(world),
                CommandQueue = queue,
                Backend = backend
            };
            var director = new IA_ProductionDirector(context);

            director.Tick(200f, 0.65f);
            Assert.That(queue.PendingCount, Is.EqualTo(1), "O déficit pós-perda deve gerar uma ordem pela fila central.");

            director.Tick(201f, 0.65f);
            Assert.That(queue.PendingCount, Is.EqualTo(1), "A perda ainda pendente não pode gerar pedidos duplicados em ticks seguintes.");

            IA_CommandRequest request;
            Assert.That(queue.TryDequeue(Time.time, out request), Is.True);
            Assert.That(request.Type, Is.EqualTo(IA_CommandType.Produce));
            Assert.That(request.Origin, Is.EqualTo("IA_ProductionDirector"));
            Assert.That(request.Payload, Is.TypeOf<IA_ProduceOrderData>());

            var payload = (IA_ProduceOrderData)request.Payload;
            Assert.That(payload.ItemKey, Is.EqualTo("Tank South"));
            Assert.That(payload.Quantity, Is.EqualTo(1));
            Assert.That(payload.ProductionOrderId, Is.Not.Empty);
            productionOrderId = payload.ProductionOrderId;

            Assert.That(survivingTank, Is.Not.Null, "O pedido não deve criar/spawnar uma unidade diretamente.");
            Assert.That(snapshot.TankUnits, Is.EqualTo(1), "A quantidade só muda quando a rota de produção materializa a unidade.");
        }
        finally
        {
            IA01MilitaryProductionGuard.Cancel(TestTeamId, IA01MilitaryAssetKind.Tank, Time.time);
            if (!string.IsNullOrEmpty(productionOrderId)) IA01MilitaryProductionGuard.Release(productionOrderId, Time.time);

            if (world != null)
            {
                var baseProbe = typeof(IA_WorldState).GetField("_baseProbe", InstanceMembers)?.GetValue(world) as Transform;
                if (baseProbe != null)
                {
                    Object.DestroyImmediate(baseProbe.gameObject);
                }
            }

            if (tankData != null)
            {
                Object.DestroyImmediate(tankData);
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    IdentidadeUnidade identity = createdObjects[i].GetComponent<IdentidadeUnidade>();
                    if (identity != null)
                    {
                        RegistroEntidadesJogo.Unregister(identity);
                        IA_WorldState.Unregister(identity);
                    }

                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
        }
    }

    private static GameObject CreateEntity(string name, bool mobile, List<GameObject> createdObjects)
    {
        var entity = new GameObject(name);
        createdObjects.Add(entity);
        IdentidadeUnidade identity = entity.AddComponent<IdentidadeUnidade>();
        identity.teamID = TestTeamId;
        RegistroEntidadesJogo.Register(identity);
        IA_WorldState.Register(identity);

        if (mobile)
        {
            NavMeshAgent agent = entity.AddComponent<NavMeshAgent>();
            agent.enabled = false;
        }

        return entity;
    }
}
