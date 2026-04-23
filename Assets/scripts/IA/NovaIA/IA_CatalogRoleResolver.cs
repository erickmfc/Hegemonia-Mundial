using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.Master
{
    public enum IA_CatalogRole
    {
        Core,
        Barracks,
        Factory,
        Warehouse,
        Radar,
        Ciws,
        Turret,
        Airport,
        Shipyard,
        Platform,
        NavalTransport,
        Carrier,
        Fighter,
        OilShip
    }

    public static class IA_CatalogRoleResolver
    {
        private static float _nextRefreshTime;
        private static readonly Dictionary<IA_CatalogRole, string> _resolved = new Dictionary<IA_CatalogRole, string>();
        private static readonly List<DadosConstrucao> _catalog = new List<DadosConstrucao>(256);

        public static string ResolveOrFallback(IA_CatalogRole role, string fallback)
        {
            if (Time.time >= _nextRefreshTime || _resolved.Count == 0)
            {
                Refresh();
            }

            string value;
            if (_resolved.TryGetValue(role, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return fallback;
        }

        private static void Refresh()
        {
            _nextRefreshTime = Time.time + 8f;
            _catalog.Clear();
            _resolved.Clear();

            if (MenuConstrucao.catalogoGlobal != null)
            {
                _catalog.AddRange(MenuConstrucao.catalogoGlobal);
            }

            DadosConstrucao[] fallback = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
            for (int i = 0; i < fallback.Length; i++)
            {
                DadosConstrucao item = fallback[i];
                if (item != null && !_catalog.Contains(item))
                {
                    _catalog.Add(item);
                }
            }

            ResolveRole(IA_CatalogRole.Core, "prefeitura", "capital", "governo");
            ResolveRole(IA_CatalogRole.Barracks, "quartel", "tenda militar", "tenda");
            ResolveRole(IA_CatalogRole.Factory, "fabrica", "construtor de veiculos");
            ResolveRole(IA_CatalogRole.Warehouse, "armazem", "galpao");
            ResolveRole(IA_CatalogRole.Radar, "radar", "torre de radar");
            ResolveRole(IA_CatalogRole.Ciws, "ciws", "phalanx");
            ResolveRole(IA_CatalogRole.Turret, "torreta", "sentinela");
            ResolveRole(IA_CatalogRole.Airport, "aeroporto", "airport", "base aerea");
            ResolveRole(IA_CatalogRole.Shipyard, "estaleiro", "estaleiro naval", "pier");
            ResolveRole(IA_CatalogRole.Platform, "plataforma", "plataforma petrolifera");
            ResolveRole(IA_CatalogRole.NavalTransport, "navio transporte", "transporte naval");
            ResolveRole(IA_CatalogRole.Carrier, "porta avioes", "carrier");
            ResolveRole(IA_CatalogRole.Fighter, "caca", "aviao de caca", "fighter");
            ResolveRole(IA_CatalogRole.OilShip, "navio petrolifero", "petroleiro");
        }

        private static void ResolveRole(IA_CatalogRole role, params string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = IA_Text.Normalize(aliases[i]);
                if (string.IsNullOrEmpty(alias))
                {
                    continue;
                }

                for (int j = 0; j < _catalog.Count; j++)
                {
                    DadosConstrucao item = _catalog[j];
                    if (item == null || item.prefabDaUnidade == null)
                    {
                        continue;
                    }

                    string joined = IA_Text.Normalize(item.nomeItem + " " + item.name + " " + item.prefabDaUnidade.name);
                    if (joined.Contains(alias))
                    {
                        _resolved[role] = item.nomeItem;
                        return;
                    }
                }
            }
        }
    }
}
