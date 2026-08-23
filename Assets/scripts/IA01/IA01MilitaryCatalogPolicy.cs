using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>
    /// Fonte de verdade dos assets militares que a IA01 pode produzir.
    ///
    /// A lista configurada no controller tem prioridade. O conjunto padrão
    /// existe apenas para controllers criados em runtime (por exemplo, a
    /// partir do governo) que não possuem uma lista serializada na cena.
    /// Nunca usamos descoberta global de ScriptableObjects como fallback.
    /// </summary>
    public static class IA01MilitaryCatalogPolicy
    {
        private static readonly HashSet<string> DefaultAllowedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "soldado_rifle",
            "soldier",
            "tank_c1_verde",
            "tankc1",
            "tank_c1",
            "c!",
            "ares_ar",
            "ares",
            "su11",
            "f200",
            "petroleiro",
            "nac_petroleo",
            "navio_petroleiro"
        };

        public static bool IsAllowed(DadosConstrucao item, IReadOnlyList<DadosConstrucao> configured)
        {
            if (item == null || item.PrefabDaUnidade == null)
            {
                return false;
            }

            if (configured != null && configured.Count > 0)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    DadosConstrucao allowed = configured[i];
                    if (allowed == null)
                    {
                        continue;
                    }

                    if (ReferenceEquals(item, allowed)
                        || string.Equals(Normalize(item.GetStableId()), Normalize(allowed.GetStableId()), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            return DefaultAllowedIds.Contains(Normalize(item.GetStableId()));
        }

        public static bool IsAllowedSavedEntity(
            int teamId,
            TipoUnidade type,
            string prefabKey,
            bool isIa01Entity,
            IReadOnlyList<DadosConstrucao> configured)
        {
            if (!isIa01Entity || teamId <= 1 || type == TipoUnidade.Estrutura)
            {
                return true;
            }

            string key = Normalize(prefabKey);
            if (configured != null && configured.Count > 0)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    DadosConstrucao item = configured[i];
                    if (item == null || item.PrefabDaUnidade == null)
                    {
                        continue;
                    }

                    if (string.Equals(key, Normalize(item.PrefabDaUnidade.name), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, Normalize(item.GetStableId()), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return DefaultAllowedIds.Contains(key);
        }

        public static bool IsAllowedPrefab(GameObject prefab, IReadOnlyList<DadosConstrucao> configured)
        {
            if (prefab == null)
            {
                return false;
            }

            if (configured != null && configured.Count > 0)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    DadosConstrucao item = configured[i];
                    if (item != null && item.PrefabDaUnidade == prefab)
                    {
                        return true;
                    }
                }

                return false;
            }

            return DefaultAllowedIds.Contains(Normalize(prefab.name));
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim()
                .Replace("(Clone)", string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
        }
    }
}
