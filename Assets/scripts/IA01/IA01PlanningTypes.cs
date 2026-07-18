using System;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public enum IA01BuildDomain
    {
        [InspectorName("Terrestre")]
        Land,
        [InspectorName("Costeiro")]
        Coastal,
        [InspectorName("Aquatico")]
        Water,
        [InspectorName("Aerodromo")]
        Airfield,
        [InspectorName("Subterraneo")]
        Underground
    }

    public enum IA01BuildArchetype
    {
        [InspectorName("Comando")]
        Command,
        [InspectorName("Residencial")]
        Residential,
        [InspectorName("Agricola")]
        Agricultural,
        [InspectorName("Industrial")]
        Industrial,
        [InspectorName("Energia")]
        Energy,
        [InspectorName("Armazenamento")]
        Storage,
        [InspectorName("Logistica")]
        Logistics,
        [InspectorName("Militar")]
        Military,
        [InspectorName("Aereo")]
        Air,
        [InspectorName("Naval")]
        Naval,
        [InspectorName("Pesquisa")]
        Research,
        [InspectorName("Defesa")]
        Defense
    }

    public enum IA01StrategicRole
    {
        [InspectorName("Nenhum")]
        None = 0,
        [InspectorName("Residencial")]
        Residential = 1,
        [InspectorName("Producao de Comida")]
        FoodProduction = 2,
        [InspectorName("Producao de Energia")]
        EnergyProduction = 3,
        [InspectorName("Armazenamento")]
        Storage = 4,
        [InspectorName("Logistica")]
        Logistics = 5,
        [InspectorName("Defesa Fixa")]
        FixedDefense = 6,
        [InspectorName("Defesa Antiaerea")]
        AntiAirDefense = 7,
        [InspectorName("Defesa Costeira")]
        CoastalDefense = 8,
        [InspectorName("Producao Militar")]
        MilitaryProduction = 9,
        [InspectorName("Aerodromo")]
        Airfield = 10,
        [InspectorName("Base Naval")]
        NavalBase = 11,
        [InspectorName("Comando")]
        Command = 12,
        [InspectorName("Industrial")]
        Industrial = 13,
        [InspectorName("Pesquisa")]
        Research = 14,
        [InspectorName("Capital")]
        Capital = 15,
        [InspectorName("Governo")]
        Government = 16,
        [InspectorName("Pier")]
        Pier = 17,
        [InspectorName("Porto")]
        Port = 18,
        [InspectorName("Estaleiro")]
        Shipyard = 19,
        [InspectorName("Aeroporto")]
        Airport = 20
    }

    public enum IA01FailureCode
    {
        None = 0,
        NoValidCatalogItem = 1,
        NoValidLot = 2,
        LotBlocked = 3,
        LotReserved = 3,
        InsufficientFunds = 4,
        ExecutionFailed = 5,
        Busy = 6,
        Cooldown = 7
    }

    public enum IA01IntentType
    {
        EstablishCapital,
        BuildEnergy,
        BuildResidentialCapacity,
        BuildFoodProduction,
        BuildStorage,
        BuildLogistics,
        DefendCapital,
        CampaignAgainstCapital,
        BuildIndustry,
        BuildDefense,
        BuyResource,
        SellResource,
        Communicate,
        // Etapas de infraestrutura da fundacao. Mantidas no final para preservar
        // os valores serializados dos intents existentes.
        BuildRoad,
        BuildMilitaryAirport,
        BuildCommercialAirport,
        BuildShipyard,
        // Etapas explicitas da abertura. Adicionadas no final para manter a
        // compatibilidade dos saves e dos planos ja serializados.
        BuildStarterHouse,
        BuildMediumApartment,
        BuildHighApartment,
        BuildMilitaryTent,
        BuildVehicleConstructor
    }

    public enum IA01CommandState
    {
        Queued,
        Validating,
        Accepted,
        Executing,
        WaitingConfirmation,
        Succeeded,
        Failed,
        Cancelled,
        Expired
    }

    public enum IA01ConstructionMode
    {
        Active = 0,
        Frozen = 1
    }

    public enum IA01ConstructionState
    {
        Idle = 0,
        SelectingIntent = 1,
        SelectingCatalogItem = 2,
        SearchingLot = 3,
        Reserved = 4,
        Executing = 5,
        WaitingConfirmation = 6,
        Cooldown = 7
    }

    public enum IA01LotState
    {
        Free,
        Reserved,
        UnderConstruction,
        Occupied,
        Blocked,
        TemporarilyInvalid
    }

    /// <summary>How a build-plan step obtains its physical placement.</summary>
    public enum IA01PlacementMode
    {
        [InspectorName("Slot exato")]
        ExactSlot,
        [InspectorName("Grupo de slots")]
        SlotGroup,
        [InspectorName("Zona autonoma")]
        AutonomousZone
    }

    /// <summary>Persistent state of a prepared map slot.</summary>
    public enum IA01BuildSlotState
    {
        [InspectorName("Disponivel")]
        Available,
        [InspectorName("Reservado")]
        Reserved,
        [InspectorName("Em construcao")]
        UnderConstruction,
        [InspectorName("Ocupado")]
        Occupied,
        [InspectorName("Bloqueado")]
        Blocked,
        [InspectorName("Invalido")]
        Invalid
    }

    public enum IA01BuildConditionType
    {
        [InspectorName("Sempre")]
        Always,
        [InspectorName("Capital ausente")]
        CapitalMissing,
        [InspectorName("Papel ausente")]
        RoleMissing,
        [InspectorName("Deficit habitacional")]
        HousingDeficit,
        [InspectorName("Comida abaixo da meta")]
        FoodBelowTarget,
        [InspectorName("Energia abaixo da meta")]
        EnergyBelowTarget,
        [InspectorName("Armazenamento necessario")]
        StorageRequired,
        [InspectorName("Sob ameaca")]
        Threatened,
        [InspectorName("Estagio minimo")]
        MinimumStage
    }

    public enum IA01FailurePolicy
    {
        [InspectorName("Aguardar")]
        Wait,
        [InspectorName("Tentar slot alternativo")]
        TryAlternativeSlot,
        [InspectorName("Usar zona autonoma")]
        UseAutonomousZone,
        [InspectorName("Pular passo opcional")]
        SkipOptionalStep,
        [InspectorName("Bloquear passo obrigatorio")]
        BlockMandatoryStep
    }

    public sealed class IA01Intent
    {
        public string Id;
        public IA01IntentType Type;
        public int Priority;
        public float CreatedAt;
        public string Reason;
        public bool Approved;
    }

    public sealed class IA01BuildDefinition
    {
        public DadosConstrucao Item;
        public string ItemId;
        public string DisplayName;
        public IA01BuildArchetype Archetype;
        public IA01StrategicRole StrategicRole;
        public IA01BuildDomain Domain;
        public bool IsStructure;
        public int Cost;
        public int MinimumTreasury;
        public Vector2 Footprint;
        public bool RequiresRoad;
        public bool RequiresNavalExit;
        public bool RequiresPower;
        public bool IsFixedDefense;
        public int MaximumRecommendedCount;
        public IA01NationStage MinimumStage;
        public bool UsedCatalogFallback;
        public string CatalogResolution;
    }

    public sealed class IA01BuildLot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Footprint;
        public string Key;
        public IA01LotState State;
    }

    public sealed class IA01Campaign
    {
        public Transform FinalTarget;
        public Transform CurrentObjective;
        public int TargetTeamId;
        public int RouteVersion;
        public float LastReplanAt;
        public Vector3 PreferredRoutePoint;
        public string ReplanReason = string.Empty;
        public bool DefendingCapital;
    }
}
