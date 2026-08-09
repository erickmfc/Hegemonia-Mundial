using System;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    [Flags]
    public enum IA_ConstructionCapability
    {
        Auto = 0,
        Structure = 1 << 0,
        Unit = 1 << 1,
        Land = 1 << 2,
        Naval = 1 << 3,
        Air = 1 << 4,
        Core = 1 << 5,
        Economy = 1 << 6,
        Military = 1 << 7,
        Defense = 1 << 8,
        Airport = 1 << 9,
        MilitaryAirport = 1 << 10,
        CommercialAirport = 1 << 11,
        Heliport = 1 << 12,
        Shipyard = 1 << 13,
        Pier = 1 << 14,
        Platform = 1 << 15,
        Factory = 1 << 16,
        Barracks = 1 << 17,
        Warehouse = 1 << 18,
        Power = 1 << 19,
        Radar = 1 << 20,
        Transport = 1 << 21,
        Oil = 1 << 22,
        Commercial = 1 << 23,
        Civil = 1 << 24,
        Aircraft = 1 << 25,
        FighterAircraft = 1 << 26,
        CommercialAircraft = 1 << 27,
        Helicopter = 1 << 28,
        NavalTransport = 1 << 29,
        OilTanker = 1 << 30
    }

    [DisallowMultipleComponent]
    public sealed class IA_ConstructionMetadata : MonoBehaviour
    {
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _aliases = string.Empty;
        [SerializeField] private string _sourcePrefabName = string.Empty;
        [SerializeField] private DadosConstrucao.CategoriaItem _category;
        [SerializeField] private IA_ConstructionCapability _capabilities = IA_ConstructionCapability.Auto;

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public string Aliases => _aliases;
        public string SourcePrefabName => _sourcePrefabName;
        public DadosConstrucao.CategoriaItem Category => _category;
        public IA_ConstructionCapability Capabilities => _capabilities;

        public bool IsStructure => HasCapability(IA_ConstructionCapability.Structure);
        public bool IsUnit => HasCapability(IA_ConstructionCapability.Unit);
        public bool IsAirport => HasCapability(IA_ConstructionCapability.Airport);
        public bool IsMilitaryAirport => HasCapability(IA_ConstructionCapability.MilitaryAirport);
        public bool IsCommercialAirport => HasCapability(IA_ConstructionCapability.CommercialAirport);
        public bool IsHeliport => HasCapability(IA_ConstructionCapability.Heliport);
        public bool IsShipyard => HasCapability(IA_ConstructionCapability.Shipyard);
        public bool IsPier => HasCapability(IA_ConstructionCapability.Pier);
        public bool IsPlatform => HasCapability(IA_ConstructionCapability.Platform);
        public bool IsFactory => HasCapability(IA_ConstructionCapability.Factory);
        public bool IsBarracks => HasCapability(IA_ConstructionCapability.Barracks);
        public bool IsWarehouse => HasCapability(IA_ConstructionCapability.Warehouse);
        public bool IsPower => HasCapability(IA_ConstructionCapability.Power);
        public bool IsRadar => HasCapability(IA_ConstructionCapability.Radar);
        public bool IsDefense => HasCapability(IA_ConstructionCapability.Defense);
        public bool IsCommercial => HasCapability(IA_ConstructionCapability.Commercial);
        public bool IsCivil => HasCapability(IA_ConstructionCapability.Civil);
        public bool IsAirDomain => HasCapability(IA_ConstructionCapability.Air);
        public bool IsNavalDomain => HasCapability(IA_ConstructionCapability.Naval);
        public bool IsLandDomain => HasCapability(IA_ConstructionCapability.Land);
        public bool IsTransport => HasCapability(IA_ConstructionCapability.Transport);
        public bool IsOil => HasCapability(IA_ConstructionCapability.Oil);
        public bool IsAircraft => HasCapability(IA_ConstructionCapability.Aircraft);
        public bool IsFighterAircraft => HasCapability(IA_ConstructionCapability.FighterAircraft);
        public bool IsCommercialAircraft => HasCapability(IA_ConstructionCapability.CommercialAircraft);
        public bool IsHelicopter => HasCapability(IA_ConstructionCapability.Helicopter);
        public bool IsNavalTransport => HasCapability(IA_ConstructionCapability.NavalTransport);
        public bool IsOilTanker => HasCapability(IA_ConstructionCapability.OilTanker);
        public bool IsMilitary => HasCapability(IA_ConstructionCapability.Military);
        public bool IsCore => HasCapability(IA_ConstructionCapability.Core);
        public bool IsEconomy => HasCapability(IA_ConstructionCapability.Economy);
        public bool IsCityHall => IsCore && MatchesIdentity("prefeitura", "governo", "capital", "city hall", "town hall");
        public bool IsHeadquarters => IsCore && MatchesIdentity("quartel general", "quartel_general", "hq", "headquarters");

        public bool HasCapability(IA_ConstructionCapability capability)
        {
            if (capability == IA_ConstructionCapability.Auto)
            {
                return false;
            }

            return (_capabilities & capability) == capability;
        }

        public void ApplyFrom(DadosConstrucao data)
        {
            if (data == null)
            {
                _itemId = string.Empty;
                _displayName = string.Empty;
                _aliases = string.Empty;
                _sourcePrefabName = string.Empty;
                _category = default(DadosConstrucao.CategoriaItem);
                _capabilities = IA_ConstructionCapability.Auto;
                return;
            }

            _itemId = data.GetStableId();
            _displayName = data.GetDisplayName();
            _aliases = data.aliases ?? string.Empty;
            _sourcePrefabName = data.PrefabDaUnidade != null ? data.PrefabDaUnidade.name : string.Empty;
            _category = data.categoria;
            _capabilities = data.GetResolvedCapabilities();
        }

        private bool MatchesIdentity(params string[] needles)
        {
            string normalized = IA_Text.Normalize(_displayName + " " + _aliases + " " + _sourcePrefabName + " " + _itemId);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            for (int i = 0; i < needles.Length; i++)
            {
                string needle = IA_Text.Normalize(needles[i]);
                if (!string.IsNullOrEmpty(needle) && normalized.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
