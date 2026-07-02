using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ManualBuildPoint : MonoBehaviour
    {
        public enum OperationalRole
        {
            Nenhum,
            EstacionamentoNaval,
            PatrulhaNaval,
            TransporteTerrestre,
            MobilizacaoTerrestre,
            SortidaAerea,
            PatrulhaAerea,
            ReconAereo,
            AtaqueAereo
        }

        [Header("Manual Build")]
        [Tooltip("Se vazio, usa o nome do objeto. Aceita varios filtros separados por virgula, ; ou quebra de linha.")]
        [TextArea(1, 3)] public string ItemFilters = string.Empty;
        [Tooltip("Papel estrategico do ponto no mapa. Se ItemFilters estiver vazio, este papel define os filtros default.")]
        public OperationalRole ManualRole = OperationalRole.Nenhum;
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
            return TargetsItem(brain, itemKey, null);
        }

        public bool TargetsItem(IA_BrainMaster brain, string itemKey, DadosConstrucao data)
        {
            if (RestrictToBootstrapStage
                && (brain == null || brain.BootstrapStage != BootstrapStage))
            {
                return false;
            }

            if (!MatchesItem(itemKey, data))
            {
                return false;
            }

            return true;
        }

        public bool IsUsableAsAnchor(IA_BrainMaster brain)
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
            string roleLabel = GetPortugueseOperationalRoleLabel(ManualRole);
            if (!string.IsNullOrWhiteSpace(source))
            {
                if (ManualRole != OperationalRole.Nenhum)
                {
                    return roleLabel + ": " + source.Trim();
                }

                return source.Trim();
            }

            if (ManualRole != OperationalRole.Nenhum)
            {
                return roleLabel;
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
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto: return "Construir Aeroporto Militar";
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
                case IA_BrainMaster.IA_BootstrapStage.MobilizeBase: return "Mobilizacao Defensiva";
                case IA_BrainMaster.IA_BootstrapStage.BuildUsina: return "Construir Usina";
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroportoComercial: return "Construir Aeroporto Comercial";
                default: return stage.ToString();
        }
        }

        public static string GetDefaultFiltersForStage(IA_BrainMaster.IA_BootstrapStage stage)
        {
            switch (stage)
            {
                case IA_BrainMaster.IA_BootstrapStage.BuildPrefeitura:
                    return "prefeitura, governo, capital, city hall, town hall";
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto:
                    return "aeroporto militar, base aerea, military airport, airport, pista";
                case IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory:
                    return "construtor de veiculos, construtor, fabrica, factory";
                case IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar:
                    return "hangar, heliporto, heliport, armazem";
                case IA_BrainMaster.IA_BootstrapStage.BuildTent:
                    return "quartel, tenda, barraca, barracks";
                case IA_BrainMaster.IA_BootstrapStage.BuildShipyard:
                    return "estaleiro, estaleiros, pier, shipyard, dock";
                case IA_BrainMaster.IA_BootstrapStage.BuildUsina:
                    return "usina, energia, power plant";
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroportoComercial:
                    return "aeroporto comercial, commercial airport, pista comercial";
                default:
                    return string.Empty;
            }
        }

        public static string GetPortugueseOperationalRoleLabel(OperationalRole role)
        {
            switch (role)
            {
                case OperationalRole.EstacionamentoNaval: return "Estacionamento Naval";
                case OperationalRole.PatrulhaNaval: return "Patrulha Naval";
                case OperationalRole.TransporteTerrestre: return "Transporte Terrestre";
                case OperationalRole.MobilizacaoTerrestre: return "Mobilizacao Terrestre";
                case OperationalRole.SortidaAerea: return "Sortida Aerea";
                case OperationalRole.PatrulhaAerea: return "Patrulha Aerea";
                case OperationalRole.ReconAereo: return "Reconhecimento Aereo";
                case OperationalRole.AtaqueAereo: return "Ataque Aereo";
                default: return "Sem papel";
            }
        }

        public static string GetDefaultFiltersForRole(OperationalRole role)
        {
            switch (role)
            {
                case OperationalRole.EstacionamentoNaval:
                    return "estaleiro, pier, plataforma, navio transporte, transporte naval, hovercraft, liberty";
                case OperationalRole.PatrulhaNaval:
                    return "navio, escolta, submarino, patrulha, destroyer, fragata, corveta, ironclad, vindicator";
                case OperationalRole.TransporteTerrestre:
                    return "transporte, caminhao, truck, transporte terrestre";
                case OperationalRole.MobilizacaoTerrestre:
                    return "quartel, fabrica, soldado, tanque, mobilizacao, concentracao";
                case OperationalRole.SortidaAerea:
                    return "aeroporto, airport, pista, hangar";
                case OperationalRole.PatrulhaAerea:
                    return "aeroporto, airport, caca, aviao, patrulha";
                case OperationalRole.ReconAereo:
                    return "aeroporto, airport, caca, aviao, recon, reconhecimento";
                case OperationalRole.AtaqueAereo:
                    return "aeroporto, airport, bombardeiro, bomber, ataque";
                default:
                    return string.Empty;
            }
        }

        private bool MatchesItem(string itemKey, DadosConstrucao data = null)
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
                    if (!IsStageCompatible(data, normalizedItem))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool IsStageCompatible(DadosConstrucao data, string normalizedItem)
        {
            if (!RestrictToBootstrapStage)
            {
                return true;
            }

            switch (BootstrapStage)
            {
                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto:
                    if (data != null)
                    {
                        bool commercialAirport = data.HasCapability(IA_ConstructionCapability.CommercialAirport);
                        bool militaryAirport = data.HasCapability(IA_ConstructionCapability.MilitaryAirport) || data.HasCapability(IA_ConstructionCapability.Airport);
                        return militaryAirport && !commercialAirport;
                    }

                    return !normalizedItem.Contains("comercial") && !normalizedItem.Contains("commercial");

                case IA_BrainMaster.IA_BootstrapStage.BuildAeroportoComercial:
                    if (data != null)
                    {
                        return data.HasCapability(IA_ConstructionCapability.CommercialAirport)
                               || (data.HasCapability(IA_ConstructionCapability.Commercial) && !data.HasCapability(IA_ConstructionCapability.MilitaryAirport));
                    }

                    return normalizedItem.Contains("comercial") || normalizedItem.Contains("commercial");

                default:
                    return true;
            }
        }

        private string GetEffectiveFilterSource()
        {
            if (!string.IsNullOrWhiteSpace(ItemFilters))
            {
                return ItemFilters;
            }

            string roleFilters = GetDefaultFiltersForRole(ManualRole);
            if (!string.IsNullOrWhiteSpace(roleFilters))
            {
                return roleFilters;
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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Vector3 drawPosition = transform.position + Vector3.up * 1.4f;
            Gizmos.DrawWireSphere(drawPosition, Mathf.Max(1f, GizmoRadius));
            Gizmos.DrawLine(transform.position, drawPosition);

            Handles.color = GizmoColor;
            Handles.Label(drawPosition + Vector3.up * 0.75f, "IA Manual: " + GetDisplayLabel());
        }
#endif
    }
}
