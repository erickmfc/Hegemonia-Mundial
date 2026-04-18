using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ManualBuildPoint : MonoBehaviour
    {
        [Header("Manual Build")]
        [Tooltip("Se vazio, usa o nome do objeto. Aceita varios filtros separados por virgula, ; ou quebra de linha.")]
        [TextArea(1, 3)] public string ItemFilters = string.Empty;
        [Tooltip("Quando desligado, o ponto so e usado se o GameObject estiver ativo na cena.")]
        public bool AllowInactiveObject = false;
        [Tooltip("Quando ligado, a IA tenta construir exatamente nesta posicao/rotacao em vez de resolver outro ponto sozinha.")]
        public bool ForceExactPlacement = true;
        [Tooltip("Se desligado, o ponto deixa de ser elegivel quando ja existe uma estrutura propria ocupando a area.")]
        public bool ReusePoint = false;
        [Min(4f)] public float OccupiedRadius = 18f;
        public bool RestrictToBootstrapStage = false;
        public IA_BrainMaster.IA_BootstrapStage BootstrapStage = IA_BrainMaster.IA_BootstrapStage.Disabled;

        [Header("Debug")]
        public Color GizmoColor = new Color(1f, 0.82f, 0.15f, 1f);
        [Min(1f)] public float GizmoRadius = 5f;

        public bool Matches(IA_BrainMaster brain, IA_WorldState worldState, string itemKey)
        {
            if (!TargetsItem(brain, itemKey))
            {
                return false;
            }

            return IsCurrentlyAvailable(brain, worldState);
        }

        public bool TargetsItem(IA_BrainMaster brain, string itemKey)
        {
            if (RestrictToBootstrapStage
                && (brain == null || brain.BootstrapStage != BootstrapStage))
            {
                return false;
            }

            if (!MatchesItem(itemKey))
            {
                return false;
            }

            return true;
        }

        public bool IsCurrentlyAvailable(IA_BrainMaster brain, IA_WorldState worldState)
        {
            if (!AllowInactiveObject && (!gameObject.activeInHierarchy || !enabled))
            {
                return false;
            }

            if (RestrictToBootstrapStage
                && (brain == null || brain.BootstrapStage != BootstrapStage))
            {
                return false;
            }

            if (ReusePoint || worldState == null)
            {
                return true;
            }

            Vector3 flatPoint = Flatten(transform.position);
            // Para navios e estaleiros é normal ter coisas em volta (como defesas). Diminuimos o raio ocupado mínimo sugerido de 18 para algo que evite bugs.
            float minDistance = ReusePoint ? 1f : Mathf.Max(OccupiedRadius * 0.5f, 1f);
            for (int i = 0; i < worldState.OwnStructures.Count; i++)
            {
                GameObject structure = worldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                if (Vector3.Distance(flatPoint, Flatten(structure.transform.position)) <= minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        public string GetDisplayLabel()
        {
            string source = GetEffectiveFilterSource();
            if (!string.IsNullOrWhiteSpace(source))
            {
                return source.Trim();
            }

            if (BootstrapStage != IA_BrainMaster.IA_BootstrapStage.Disabled)
            {
                return "Bootstrap: " + GetPortugueseBootstrapStageLabel(BootstrapStage);
            }

            return name;
        }

        public static string GetPortugueseBootstrapStageLabel(IA_BrainMaster.IA_BootstrapStage stage)
        {
            switch (stage)
            {
                case IA_BrainMaster.IA_BootstrapStage.Disabled: return "Desativado";
                case IA_BrainMaster.IA_BootstrapStage.BuildPrefeitura: return "Construir Prefeitura";
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto: return "Construir Aeroporto";
                case IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory: return "Construir Fabrica de Veiculos";
                case IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar: return "Construir Hangar de Apoio";
                case IA_BrainMaster.IA_BootstrapStage.BuildTent: return "Construir Tenda Militar";
                case IA_BrainMaster.IA_BootstrapStage.AnalyzeTerrain: return "Analisar Terreno";
                case IA_BrainMaster.IA_BootstrapStage.ProduceGroundUnits: return "Produzir Unidades Terrestres";
                case IA_BrainMaster.IA_BootstrapStage.HoldGroundUnits: return "Aguardar Unidades Terrestres";
                case IA_BrainMaster.IA_BootstrapStage.ProduceAircraft: return "Produzir Aeronaves";
                case IA_BrainMaster.IA_BootstrapStage.BuildShipyard: return "Construir Estaleiro";
                case IA_BrainMaster.IA_BootstrapStage.HoldShipyard: return "Aguardar Estaleiro";
                case IA_BrainMaster.IA_BootstrapStage.ProduceShip: return "Produzir Navio";
                case IA_BrainMaster.IA_BootstrapStage.HoldShipLaunch: return "Aguardar Lancamento Naval";
                case IA_BrainMaster.IA_BootstrapStage.Completed: return "Concluido";
                default: return stage.ToString();
            }
        }

        public static string GetDefaultFiltersForStage(IA_BrainMaster.IA_BootstrapStage stage)
        {
            switch (stage)
            {
                case IA_BrainMaster.IA_BootstrapStage.BuildPrefeitura:
                    return "prefeitura, governo, capital";
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto:
                    return "aeroporto, airport";
                case IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory:
                    return "construtor de veiculos, construtor, fabrica";
                case IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar:
                    return "hangar, heliporto, armazem";
                case IA_BrainMaster.IA_BootstrapStage.BuildTent:
                    return "quartel, tenda, barraca";
                case IA_BrainMaster.IA_BootstrapStage.BuildShipyard:
                    return "estaleiro, estaleiros, pier";
                default:
                    return string.Empty;
            }
        }

        private bool MatchesItem(string itemKey)
        {
            string normalizedItem = IA_Text.Normalize(itemKey);
            if (string.IsNullOrEmpty(normalizedItem))
            {
                return false;
            }

            string source = GetEffectiveFilterSource();
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string[] filters = source.Split(',', ';', '\n');
            for (int i = 0; i < filters.Length; i++)
            {
                string filter = IA_Text.Normalize(filters[i]);
                if (string.IsNullOrEmpty(filter))
                {
                    continue;
                }

                // Corrige problemas quando o objeto tem sufixos no nome, tipo "Estaleiro_Team2" 
                // Se o nome contiver "estaleiro" e o itemKey for "estaleiro naval", agora ele entende e aceita.
                if (normalizedItem.Contains(filter) || filter.Contains(normalizedItem) || 
                    (normalizedItem.Contains("estaleiro") && filter.Contains("estaleiro")) ||
                    (normalizedItem.Contains("pier") && filter.Contains("pier")) ||
                    (normalizedItem.Contains("quartel general") && filter.Contains("quartel general")))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetEffectiveFilterSource()
        {
            if (!string.IsNullOrWhiteSpace(ItemFilters))
            {
                return ItemFilters;
            }

            string normalizedObjectName = IA_Text.Normalize(gameObject.name);
            if (!LooksLikeGenericObjectName(normalizedObjectName))
            {
                return gameObject.name;
            }

            return GetDefaultFiltersForStage(BootstrapStage);
        }

        private static bool LooksLikeGenericObjectName(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName))
            {
                return true;
            }

            return normalizedName == "gameobject"
                   || normalizedName.StartsWith("gameobject ")
                   || normalizedName == "empty"
                   || normalizedName.StartsWith("empty ");
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Vector3 drawPosition = transform.position + Vector3.up * 1.4f;
            Gizmos.DrawWireSphere(drawPosition, Mathf.Max(1f, GizmoRadius));
            Gizmos.DrawLine(transform.position, drawPosition);

#if UNITY_EDITOR
            Handles.color = GizmoColor;
            Handles.Label(drawPosition + Vector3.up * 0.75f, "IA Manual: " + GetDisplayLabel());
#endif
        }
    }
}
