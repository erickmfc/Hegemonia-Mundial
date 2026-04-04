using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public enum IA_Domain
    {
        Land,
        Naval,
        Air
    }

    public enum IA_TerrainType
    {
        Unknown,
        Land,
        Water,
        City,
        Open,
        Coast,
        Choke
    }

    public enum IA_CommandType
    {
        Build,
        Produce,
        Move,
        Attack,
        Patrol,
        Ability
    }

    public enum IA_CommandStatus
    {
        Queued,
        Running,
        Success,
        Failed,
        CoolingDown,
        Cancelled
    }

    public enum IA_SquadRole
    {
        Recon,
        LocalDefense,
        BorderPatrol,
        ArmoredAssault,
        Amphibious,
        NavalEscort,
        NavalHeavy,
        Submarine,
        AirIntercept,
        AirTacticalTransport
    }

    public enum IA_ZoneType
    {
        Core,
        Economy,
        Military,
        Defense,
        Air,
        Naval,
        Frontline,
        Coast
    }

    public enum EstadoCargaIA
    {
        Normal,
        EmCombate,
        Saturado
    }

    public interface IIAUpdateModule
    {
        string Name { get; }
        float Interval { get; }
        float BudgetMs { get; }
        void Tick(float now, float deltaTime);
    }

    [Serializable]
    public sealed class IA_CommandRequest
    {
        public string Id;
        public IA_CommandType Type;
        public int Priority;
        public string DedupKey;
        public float CooldownSeconds;
        public object Payload;
        public float EnqueueTime;
    }

    [Serializable]
    public sealed class IA_CommandRecord
    {
        public string Id;
        public string DedupKey;
        public IA_CommandType Type;
        public IA_CommandStatus Status;
        public float Timestamp;
        public string Message;
    }

    [Serializable]
    public sealed class IA_EnemyObservation
    {
        public int InstanceId;
        public Transform Transform;
        public Vector3 Position;
        public string UnitName;
        public IA_Domain Domain;
        public float ThreatScore;
        public float LastSeenTime;
        public bool IsStructure;
    }

    [Serializable]
    public sealed class IA_VisibilityProvider
    {
        public Transform Source;
        public float Radius;
    }

    [Serializable]
    public sealed class IA_BuildOrderData
    {
        public string ItemKey;
        public Vector3 Position;
        public Quaternion Rotation;
        public IA_ZoneType Zone;
        public bool ForceManualPlacement;
        public string ManualPointLabel;
    }

    [Serializable]
    public sealed class IA_ProduceOrderData
    {
        public string ItemKey;
        public int Quantity;
    }

    [Serializable]
    public sealed class IA_MoveOrderData
    {
        public List<GameObject> Units = new List<GameObject>();
        public Vector3 Destination;
    }

    [Serializable]
    public sealed class IA_AttackOrderData
    {
        public List<GameObject> Units = new List<GameObject>();
        public Transform Target;
        public Vector3 TargetPosition;
    }

    [Serializable]
    public sealed class IA_PatrolOrderData
    {
        public List<GameObject> Units = new List<GameObject>();
        public Vector3 PointA;
        public Vector3 PointB;
    }

    [Serializable]
    public sealed class IA_AbilityOrderData
    {
        public GameObject Caster;
        public string AbilityKey;
        public Vector3 TargetPosition;
        public Transform Target;
    }

    [Serializable]
    public sealed class IA_MapCell
    {
        public Vector3 Center;
        public IA_TerrainType Terrain;
        public float Height;
        public float Slope;
        public bool BuildableLand;
        public bool BuildableWater;
        public float ObstacleDensity;
        public IA_ZoneType Zone;
    }

    [Serializable]
    public sealed class IA_SquadData
    {
        public string Id;
        public IA_SquadRole Role;
        public readonly List<GameObject> Units = new List<GameObject>();
        public Vector3 LastObjective;
        public float LastCommandTime;
    }

    [Serializable]
    public sealed class IA_CounterPlan
    {
        public float LandWeight;
        public float NavalWeight;
        public float AirWeight;
        public bool AntiRush;
        public bool ReinforceCoast;
        public bool ReinforceCenter;
        public bool ReinforceFlanks;
    }

    [Serializable]
    public sealed class IA_CombatPressure
    {
        public EstadoCargaIA Estado = EstadoCargaIA.Normal;
        public bool EnemyVisible;
        public int NavalUnitsActive;
        public int AirUnitsActive;
        public float RecentCombatSeconds = 999f;
        public int ActiveMissiles;
        public int ActiveProjectiles;
        public float LastUpdatedTime;

        public bool IsCombatRecent(float calmWindowSeconds)
        {
            return RecentCombatSeconds <= Mathf.Max(1f, calmWindowSeconds);
        }

        public bool HasMixedNavalAirLoad()
        {
            return NavalUnitsActive > 0 && AirUnitsActive > 0;
        }
    }

    [Serializable]
    public sealed class IA_ForceSnapshot
    {
        public float LastUpdatedTime;
        public int TotalOwnUnits;
        public int TotalOwnStructures;
        public int TotalCombatUnits;
        public int InfantryUnits;
        public int TankUnits;
        public int ArtilleryUnits;
        public int Helicopters;
        public int FixedWingAircraft;
        public int AirUnits;
        public int NavalUnits;
        public int Submarines;
        public int GroundTransports;
        public int HoverTransports;
        public int NavalTransports;
        public int VisibleEnemies;
        public int VisibleEnemyStructures;
        public int ActiveMissiles;
        public int ActiveProjectiles;
        public int BarracksCount;
        public int FactoryCount;
        public int AirportCount;
        public int HeliportCount;
        public int ShipyardCount;
        public int PierCount;
        public int PlatformCount;
        public int WarehouseCount;
        public int RadarCount;
        public bool EnemyVisible;
        public float RecentCombatSeconds = 999f;

        public bool HasBarracks
        {
            get { return BarracksCount > 0; }
        }

        public bool HasFactory
        {
            get { return FactoryCount > 0; }
        }

        public bool HasAirport
        {
            get { return AirportCount > 0; }
        }

        public bool HasHeliport
        {
            get { return HeliportCount > 0; }
        }

        public bool HasNavalBase
        {
            get { return ShipyardCount > 0 || PierCount > 0; }
        }

        public bool HasOperationalNavalBase
        {
            get { return HasNavalBase; }
        }

        public int FleetCombatCount
        {
            get { return NavalUnits + Submarines; }
        }
    }

    public static class IA_Text
    {
        private static readonly Dictionary<string, string> _normalizeCache = new Dictionary<string, string>();
        private const int MaxCacheSize = 512;

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result;
            if (_normalizeCache.TryGetValue(value, out result))
            {
                return result;
            }

            string normalized = value.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
            string withoutDiacritics = normalized.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new StringBuilder(withoutDiacritics.Length);
            for (int i = 0; i < withoutDiacritics.Length; i++)
            {
                char c = withoutDiacritics[i];
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark
                    || category == UnicodeCategory.SpacingCombiningMark
                    || category == UnicodeCategory.EnclosingMark)
                {
                    continue;
                }

                builder.Append(c);
            }

            normalized = builder.ToString().Normalize(NormalizationForm.FormC);
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            if (_normalizeCache.Count >= MaxCacheSize)
            {
                _normalizeCache.Clear(); // Simples flush quando lotar
            }

            _normalizeCache[value] = normalized;
            return normalized;
        }
    }
}
