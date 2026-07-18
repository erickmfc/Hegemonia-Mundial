using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>Single bridge from IA01 commands to the same construction service used by the game.</summary>
    public sealed class IA01BuildExecutor
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01BackendBridge backend;
        private readonly IA01CityPlanner city;
        private readonly IA01WorldState world;

        public IA01BuildExecutor(IA01Controller controller, IA01RuntimeContext context, IA01BackendBridge backend, IA01CityPlanner city, IA01WorldState world)
        {
            this.controller = controller;
            this.context = context;
            this.backend = backend;
            this.city = city;
            this.world = world;
        }

        public bool TryExecute(IA01BuildDefinition definition, IA01BuildLot lot, string commandId, string prefabId, bool allowFoundationFundingOverride, out GameObject built)
        {
            built = null;
            if (definition == null || lot == null || backend == null || !backend.TryPay(definition.Cost, allowFoundationFundingOverride)) return false;
            if (definition.Item == null || !definition.Item.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                backend.Refund(definition.Cost);
                return false;
            }

            built = backend.CreateStructure(prefab, lot.Position, lot.Rotation);
            if (built == null)
            {
                backend.Refund(definition.Cost);
                return false;
            }

            if (!built.activeSelf) built.SetActive(true);
            IdentidadeUnidade identity = built.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = built.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = context.NationName;
            identity.tipoUnidade = TipoUnidade.Estrutura;
            SaveableEntity.Garantir(built, prefab.name);

            if (definition.Archetype == IA01BuildArchetype.Command || definition.StrategicRole == IA01StrategicRole.Capital
                || definition.StrategicRole == IA01StrategicRole.Government || definition.StrategicRole == IA01StrategicRole.Command)
            {
                city?.RegisterCapital(built);
            }

            IA01Manager manager = controller != null ? controller.Manager : null;
            manager?.WorldRegistry.Register(new IA01WorldEntityRecord
            {
                EntityId = "ia01.structure:" + context.TeamId + ":" + definition.ItemId + ":" + built.GetInstanceID(),
                CommandId = commandId,
                StructureId = definition.ItemId,
                PrefabId = prefabId,
                LotId = lot.Key,
                InstanceId = built.GetInstanceID(),
                NationId = context.NationId,
                TeamId = context.TeamId,
                StrategicRole = definition.StrategicRole,
                DisplayName = definition.DisplayName,
                Kind = IA01WorldEntityKind.Structure,
                Domain = IA01WorldDomain.Infrastructure,
                Category = definition.Archetype.ToString(),
                RegionKey = "capital:" + context.TeamId,
                Position = built.transform.position,
                Operational = built.activeInHierarchy,
                Version = world != null ? world.Version : 0,
                NativeObject = built,
                Source = "IA01BuildExecutor"
            });
            lot.State = IA01LotState.UnderConstruction;
            return true;
        }
    }
}
