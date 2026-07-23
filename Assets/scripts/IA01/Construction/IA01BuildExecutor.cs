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
            Estaleiro builtShipyard = built.GetComponent<Estaleiro>();
            if (builtShipyard != null) builtShipyard.OwnerTeamId = context.TeamId;
            PierMarinha builtPier = built.GetComponent<PierMarinha>();
            if (builtPier != null) builtPier.OwnerTeamId = context.TeamId;
            PlataformaOffshore builtPlatform = built.GetComponent<PlataformaOffshore>();
            if (builtPlatform != null) builtPlatform.OwnerTeamId = context.TeamId;
            SaveableEntity.Garantir(built, prefab.name);

            // Mantem a mesma regra visual do construtor do jogador: casas da IA
            // recebem calcada/conexao na rua mais proxima apos a criacao.
            Imovel residential = built.GetComponent<Imovel>();
            if (residential != null)
            {
                RuaConectora[] roads = UnityEngine.Object.FindObjectsByType<RuaConectora>(FindObjectsSortMode.None);
                RuaConectora nearestRoad = null;
                Vector3 nearestPoint = built.transform.position;
                float bestSqr = Mathf.Infinity;
                for (int i = 0; i < roads.Length; i++)
                {
                    RuaConectora road = roads[i];
                    if (road == null) continue;
                    Vector3 a = road.ObterConectorInicio().posicao;
                    Vector3 b = road.ObterConectorFim().posicao;
                    Vector3 ab = b - a;
                    ab.y = 0f;
                    if (ab.sqrMagnitude < 0.01f) continue;
                    Vector3 p = built.transform.position; p.y = a.y;
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                    Vector3 projected = a + ab * t;
                    float sqr = (p - projected).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; nearestRoad = road; nearestPoint = projected; }
                }
                if (nearestRoad != null && bestSqr <= 180f * 180f)
                {
                    residential.AtualizarPavimentacao(nearestPoint);
                }
            }

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
