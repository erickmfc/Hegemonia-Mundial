using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public enum IA01ExecutionMode
    {
        ObserverDebug = 0,
        Manual = 1,
        Hybrid = 2,
        Full = 3
    }

    public enum IA01NationMode
    {
        Peace = 0,
        Normal = 1,
        War = 2
    }

    public enum IA01NationStage
    {
        Initialization = 0,
        Reconnaissance = 1,
        Survival = 2,
        Stabilization = 3,
        UrbanDevelopment = 4,
        Industrialization = 5,
        Specialization = 6,
        RegionalProjection = 7,
        GlobalPower = 8,
        Recovering = 100,
        Emergency = 101,
        FailedSafe = 102
    }

    public enum IA01NationPosture
    {
        Development = 0,
        Peace = 1,
        Alert = 2,
        Preparation = 3,
        Defense = 4,
        LimitedAttack = 5,
        War = 6,
        Retreat = 7,
        Recovery = 8
    }

    public enum IA01NationPersonality
    {
        Balanced = 0,
        Aggressive = 1,
        Cautious = 2,
        Commercial = 3,
        Diplomatic = 4,
        Militaristic = 5,
        SelfSufficient = 6,
        Expansionist = 7,
        Opportunistic = 8
    }

    public enum IA01NationDoctrine
    {
        Balanced = 0,
        Land = 1,
        Air = 2,
        Naval = 3,
        Defensive = 4,
        Economic = 5,
        Industrial = 6,
        Agricultural = 7,
        Technological = 8
    }

    public enum IA01DirtyReason
    {
        Bootstrap = 0,
        IdentityChanged = 1,
        GovernmentChanged = 2,
        WorldChanged = 3,
        EconomyChanged = 4,
        PopulationChanged = 5,
        LogisticsChanged = 6,
        SecurityChanged = 7,
        SaveLoaded = 8,
        ExternalEvent = 9,
        CacheInvalidated = 10,
        ProfileChanged = 11,
        ManualRefresh = 12,
        TimerFired = 13,
        RegistryChanged = 14,
        ServiceSnapshotChanged = 15
    }

    public enum IA01WorldEntityKind
    {
        Unknown = 0,
        Controller = 1,
        Nation = 2,
        City = 3,
        Structure = 4,
        Unit = 5,
        Objective = 6,
        Mission = 7,
        Order = 8,
        ResourceNode = 9,
        Relationship = 10,
        IntelSource = 11,
        Cache = 12,
        Other = 13
    }

    public enum IA01WorldDomain
    {
        Unknown = 0,
        Land = 1,
        Naval = 2,
        Air = 3,
        Economy = 4,
        Diplomacy = 5,
        Command = 6,
        Infrastructure = 7,
        Intelligence = 8
    }

    public enum IA01EventSeverity
    {
        Info = 0,
        Notice = 1,
        Warning = 2,
        Critical = 3
    }

    [Serializable]
    public sealed class IA01NationIdentity
    {
        public int InstanceId;
        public int NationId;
        public int TeamId;
        public string NationName = string.Empty;
        public string PresidentName = string.Empty;
        public string CurrencyName = string.Empty;
        public string CurrencySymbol = string.Empty;
        public string CountryProfile = string.Empty;
        public string DifficultyProfile = string.Empty;
        public int RandomSeed;
        public IA01ExecutionMode ExecutionMode;
        public IA01NationMode NationMode;
        public IA01NationStage CurrentStage;
        public IA01NationPosture CurrentPosture;

        public IA01NationIdentity Clone()
        {
            return new IA01NationIdentity
            {
                InstanceId = InstanceId,
                NationId = NationId,
                TeamId = TeamId,
                NationName = NationName,
                PresidentName = PresidentName,
                CurrencyName = CurrencyName,
                CurrencySymbol = CurrencySymbol,
                CountryProfile = CountryProfile,
                DifficultyProfile = DifficultyProfile,
                RandomSeed = RandomSeed,
                ExecutionMode = ExecutionMode,
                NationMode = NationMode,
                CurrentStage = CurrentStage,
                CurrentPosture = CurrentPosture
            };
        }
    }

    public sealed class IA01RuntimeEvent
    {
        public string EventId = Guid.NewGuid().ToString("N");
        public int NationId;
        public int TeamId;
        public int SourceInstanceId;
        public string Topic = string.Empty;
        public string Message = string.Empty;
        public string PayloadText = string.Empty;
        public object Payload;
        public float TimeStamp;
        public IA01EventSeverity Severity = IA01EventSeverity.Info;
    }

    [Serializable]
    public struct IA01WorkBudget
    {
        public float MaxMilliseconds;
        public int MaxOperations;
        public int MaxEvents;
        public bool AllowWriteActions;
        public bool AllowExternalCalls;

        public static IA01WorkBudget Create(float maxMilliseconds, int maxOperations, int maxEvents, bool allowWriteActions = true, bool allowExternalCalls = false)
        {
            return new IA01WorkBudget
            {
                MaxMilliseconds = Mathf.Max(0f, maxMilliseconds),
                MaxOperations = Mathf.Max(0, maxOperations),
                MaxEvents = Mathf.Max(0, maxEvents),
                AllowWriteActions = allowWriteActions,
                AllowExternalCalls = allowExternalCalls
            };
        }
    }

    [Serializable]
    public struct IA01WorkResult
    {
        public bool Completed;
        public bool Changed;
        public int Operations;
        public int Events;
        public float ConsumedMilliseconds;
        public string LastMessage;

        public static IA01WorkResult Empty(string message)
        {
            return new IA01WorkResult
            {
                Completed = false,
                Changed = false,
                Operations = 0,
                Events = 0,
                ConsumedMilliseconds = 0f,
                LastMessage = message ?? string.Empty
            };
        }

        public static IA01WorkResult From(bool completed, bool changed, int operations, int events, float consumedMilliseconds, string message)
        {
            return new IA01WorkResult
            {
                Completed = completed,
                Changed = changed,
                Operations = Mathf.Max(0, operations),
                Events = Mathf.Max(0, events),
                ConsumedMilliseconds = Mathf.Max(0f, consumedMilliseconds),
                LastMessage = message ?? string.Empty
            };
        }
    }

    public sealed class IA01ScheduledSlice
    {
        public IA01Controller Controller;
        public IA01WorkBudget Budget;
        public float DueAt;
        public int Priority;
        public string Reason = string.Empty;
    }

    public sealed class IA01SchedulerPlan
    {
        public float FrameBudgetMs;
        public float RemainingBudgetMs;
        public int ReadyCount;
        public int ScheduledCount;
        public string Summary = string.Empty;
        public List<IA01ScheduledSlice> Slices = new List<IA01ScheduledSlice>(8);
    }

    [Serializable]
    public sealed class IA01NationTelemetrySnapshot
    {
        public int InstanceId;
        public int NationId;
        public int TeamId;
        public string NationName = string.Empty;
        public IA01ExecutionMode ExecutionMode;
        public IA01NationMode NationMode;
        public IA01NationStage Stage;
        public IA01NationPosture Posture;
        public float LastSliceMs;
        public float AverageSliceMs;
        public int SliceCount;
        public int DirtyCount;
        public int EventCount;
        public int RegistryEntries;
        public string LastResult = string.Empty;
        public string LastServiceReport = string.Empty;
    }

    [Serializable]
    public sealed class IA01TelemetrySnapshot
    {
        public float CaptureTime;
        public float LastFrameMs;
        public float AverageFrameMs;
        public float PeakFrameMs;
        public int FrameCount;
        public int SliceCount;
        public int EventCount;
        public int ControllerCount;
        public string ServiceReport = string.Empty;
        public List<IA01NationTelemetrySnapshot> Nations = new List<IA01NationTelemetrySnapshot>(8);
    }

    [Serializable]
    public sealed class IA01WorldEntityRecord
    {
        public string EntityId = string.Empty;
        public string CommandId = string.Empty;
        public string StructureId = string.Empty;
        public string PrefabId = string.Empty;
        public string LotId = string.Empty;
        public int InstanceId;
        public int NationId;
        public int TeamId;
        public IA01StrategicRole StrategicRole = IA01StrategicRole.None;
        public string DisplayName = string.Empty;
        public IA01WorldEntityKind Kind = IA01WorldEntityKind.Unknown;
        public IA01WorldDomain Domain = IA01WorldDomain.Unknown;
        public string Category = string.Empty;
        public string RegionKey = string.Empty;
        public Vector3 Position = Vector3.zero;
        public bool Operational = true;
        public int Version;
        public string State = string.Empty;
        public string Source = string.Empty;

        [NonSerialized] public UnityEngine.Object NativeObject;

        public IA01WorldEntityRecord Clone()
        {
            return new IA01WorldEntityRecord
            {
                EntityId = EntityId,
                CommandId = CommandId,
                StructureId = StructureId,
                PrefabId = PrefabId,
                LotId = LotId,
                InstanceId = InstanceId,
                NationId = NationId,
                TeamId = TeamId,
                StrategicRole = StrategicRole,
                DisplayName = DisplayName,
                Kind = Kind,
                Domain = Domain,
                Category = Category,
                RegionKey = RegionKey,
                Position = Position,
                Operational = Operational,
                Version = Version,
                State = State,
                Source = Source
            };
        }
    }

    [Serializable]
    public sealed class IA01ResourceRecord
    {
        public string ResourceId = string.Empty;
        public int NationId;
        public int TeamId;
        public float Amount;
        public float Reserved;
        public float Capacity;
        public int Version;
        public float LastUpdated;
        public string Source = string.Empty;
    }

    [Serializable]
    public sealed class IA01PopulationRecord
    {
        public int NationId;
        public int TeamId;
        public int Total;
        public int Civilian;
        public int Military;
        public int Reservists;
        public int Available;
        public int Workforce;
        public int HousingCapacity;
        public float Stability;
        public float Happiness;
        public int Version;
    }

    [Serializable]
    public sealed class IA01DomainRecord
    {
        public string Id = string.Empty;
        public int NationId;
        public int TeamId;
        public string Kind = string.Empty;
        public string State = string.Empty;
        public string Target = string.Empty;
        public string Category = string.Empty;
        public string RegionKey = string.Empty;
        public string PayloadText = string.Empty;
        public float Priority;
        public float Urgency;
        public float Confidence = 1f;
        public float CreatedAt;
        public float ExpiresAt = -1f;
        public bool Operational = true;
        public int Version;
    }

    [Serializable]
    public sealed class IA01CacheEntry
    {
        public string Key = string.Empty;
        public int Version;
        public float Timestamp;
        public float Expiration;
        public string InvalidationReason = string.Empty;
        public string SourceRegion = string.Empty;
        public bool Dirty;
        public string ValueText = string.Empty;

        [NonSerialized] public object Value;
    }

    [Serializable]
    public sealed class IA01TimerEntry
    {
        public string Key = string.Empty;
        public float IntervalSeconds;
        public float NextDueAt;
        public float LastFiredAt;
        public int FiredCount;
        public bool Paused;
        public int Version;
    }

    /// <summary>
    /// Indica quando a IA01 é a autoridade de infraestrutura de um time.
    /// O BrainMaster continua podendo produzir unidades e emitir ordens táticas.
    /// </summary>
    public static class IA01ConstructionAuthority
    {
        public static bool IsOwner(int teamId)
        {
            if (teamId <= 0)
            {
                return false;
            }

            IA01Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA01Controller>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller == null || !controller.isActiveAndEnabled || controller.TeamId != teamId)
                {
                    continue;
                }

                if (controller.ExecutionMode == IA01ExecutionMode.Full
                    || controller.ExecutionMode == IA01ExecutionMode.Hybrid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
