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
        AirportMilitary,
        AirportCommercial,
        Shipyard,
        Platform,
        NavalTransport,
        Carrier,
        Fighter,
        OilShip,
        Power,
        Farm,
        House,
        Commercial,
        NavalPatrol
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
            ResolveRole(IA_CatalogRole.AirportMilitary, "aeroporto militar", "base aerea militar", "military airport");
            ResolveRole(IA_CatalogRole.AirportCommercial, "aeroporto comercial", "commercial airport");
            ResolveRole(IA_CatalogRole.Shipyard, "estaleiro", "estaleiro naval", "pier", "dock");
            ResolveRole(IA_CatalogRole.Platform, "plataforma", "plataforma petrolifera");
            ResolveRole(IA_CatalogRole.NavalTransport, "navio transporte", "transporte naval");
            ResolveRole(IA_CatalogRole.Carrier, "porta avioes", "carrier");
            ResolveRole(IA_CatalogRole.Fighter, "b260", "supra", "su11", "caca", "aviao de caca", "fighter");
            ResolveRole(IA_CatalogRole.OilShip, "navio petrolifero", "petroleiro");
            ResolveRole(IA_CatalogRole.Power, "usina", "energia", "solar", "nuclear");
            ResolveRole(IA_CatalogRole.Farm, "fazenda", "farm", "comida");
            ResolveRole(IA_CatalogRole.House, "residencial", "predio", "casa popular", "house");
            ResolveRole(IA_CatalogRole.Commercial, "comercial", "shopping", "loja");
            ResolveRole(IA_CatalogRole.NavalPatrol, "fragata", "corveta", "destroyer", "navio de guerra");
        }

        private static void ResolveRole(IA_CatalogRole role, params string[] aliases)
        {
            for (int j = 0; j < _catalog.Count; j++)
            {
                DadosConstrucao item = _catalog[j];
                if (item == null || item.prefabDaUnidade == null)
                {
                    continue;
                }

                if (MatchesRoleByCapability(role, item))
                {
                    _resolved[role] = item.GetDisplayName();
                    return;
                }
            }

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
                        _resolved[role] = item.GetDisplayName();
                        return;
                    }
                }
            }
        }

        private static bool MatchesRoleByCapability(IA_CatalogRole role, DadosConstrucao item)
        {
            if (item == null)
            {
                return false;
            }

            switch (role)
            {
                case IA_CatalogRole.Core:
                    return item.HasCapability(IA_ConstructionCapability.Core);
                case IA_CatalogRole.Barracks:
                    return item.HasCapability(IA_ConstructionCapability.Barracks);
                case IA_CatalogRole.Factory:
                    return item.HasCapability(IA_ConstructionCapability.Factory);
                case IA_CatalogRole.Warehouse:
                    return item.HasCapability(IA_ConstructionCapability.Warehouse);
                case IA_CatalogRole.Radar:
                    return item.HasCapability(IA_ConstructionCapability.Radar);
                case IA_CatalogRole.Ciws:
                    return item.HasCapability(IA_ConstructionCapability.Defense) && IA_Text.Normalize(item.GetDisplayName()).Contains("ciws");
                case IA_CatalogRole.Turret:
                    return item.HasCapability(IA_ConstructionCapability.Defense) && IA_Text.Normalize(item.GetDisplayName()).Contains("tor");
                case IA_CatalogRole.Airport:
                    return item.HasCapability(IA_ConstructionCapability.Airport)
                           && !item.HasCapability(IA_ConstructionCapability.Heliport)
                           && !item.HasCapability(IA_ConstructionCapability.CommercialAirport);
                case IA_CatalogRole.AirportMilitary:
                    return item.HasCapability(IA_ConstructionCapability.MilitaryAirport)
                           && !item.HasCapability(IA_ConstructionCapability.Heliport);
                case IA_CatalogRole.AirportCommercial:
                    return item.HasCapability(IA_ConstructionCapability.CommercialAirport)
                           && !item.HasCapability(IA_ConstructionCapability.Heliport);
                case IA_CatalogRole.Shipyard:
                    return item.HasCapability(IA_ConstructionCapability.Shipyard) || item.HasCapability(IA_ConstructionCapability.Pier);
                case IA_CatalogRole.Platform:
                    return item.HasCapability(IA_ConstructionCapability.Platform);
                case IA_CatalogRole.NavalTransport:
                    return item.HasCapability(IA_ConstructionCapability.NavalTransport);
                case IA_CatalogRole.Carrier:
                    return IA_Text.Normalize(item.GetDisplayName()).Contains("porta avioes") || IA_Text.Normalize(item.GetDisplayName()).Contains("carrier");
                case IA_CatalogRole.Fighter:
                    return item.HasCapability(IA_ConstructionCapability.FighterAircraft) || IA_Text.Normalize(item.GetDisplayName()).Contains("caca");
                case IA_CatalogRole.OilShip:
                    return item.HasCapability(IA_ConstructionCapability.OilTanker);
                case IA_CatalogRole.Power:
                    return item.HasCapability(IA_ConstructionCapability.Power);
                case IA_CatalogRole.Farm:
                    return item.HasCapability(IA_ConstructionCapability.Civil) && IA_Text.Normalize(item.GetDisplayName()).Contains("farm");
                case IA_CatalogRole.House:
                    return item.HasCapability(IA_ConstructionCapability.Civil);
                case IA_CatalogRole.Commercial:
                    return item.HasCapability(IA_ConstructionCapability.Commercial);
                case IA_CatalogRole.NavalPatrol:
                    return item.HasCapability(IA_ConstructionCapability.Naval) && item.HasCapability(IA_ConstructionCapability.Unit);
                default:
                    return false;
            }
        }
    }
}
