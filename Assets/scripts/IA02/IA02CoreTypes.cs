using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    public enum IA02ExecutionMode
    {
        ObserverDebug = 0,
        Manual = 1,
        Hybrid = 2,
        Full = 3
    }

    public enum IA02NationMode
    {
        Peace = 0,
        Normal = 1,
        War = 2
    }

    public enum IA02NationStage
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

    public enum IA02NationPosture
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

    public enum IA02NationPersonality
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

    public enum IA02NationDoctrine
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

    public enum IA02DirtyReason
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

    public enum IA02WorldEntityKind
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

    public enum IA02WorldDomain
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

    public enum IA02EventSeverity
    {
        Info = 0,
        Notice = 1,
        Warning = 2,
        Critical = 3
    }

    [Serializable]
    public sealed class IA02NationIdentity
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
        public IA02ExecutionMode ExecutionMode;
        public IA02NationMode NationMode;
        public IA02NationStage CurrentStage;
        public IA02NationPosture CurrentPosture;

        public IA02NationIdentity Clone()
        {
            return new IA02NationIdentity
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

    public sealed class IA02RuntimeEvent
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
        public IA02EventSeverity Severity = IA02EventSeverity.Info;
    }

    [Serializable]
    public struct IA02WorkBudget
    {
        public float MaxMilliseconds;
        public int MaxOperations;
        public int MaxEvents;
        public bool AllowWriteActions;
        public bool AllowExternalCalls;

        public static IA02WorkBudget Create(float maxMilliseconds, int maxOperations, int maxEvents, bool allowWriteActions = true, bool allowExternalCalls = false)
        {
            return new IA02WorkBudget
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
    public struct IA02WorkResult
    {
        public bool Completed;
        // Trabalho interrompido voluntariamente pelo orcamento. Nao e falha e
        // deve voltar ao agendador logo no proximo frame disponivel.
        public bool Deferred;
        public bool Changed;
        public int Operations;
        public int Events;
        public float ConsumedMilliseconds;
        public string LastMessage;

        public static IA02WorkResult Empty(string message)
        {
            return new IA02WorkResult
            {
                Completed = false,
                Deferred = false,
                Changed = false,
                Operations = 0,
                Events = 0,
                ConsumedMilliseconds = 0f,
                LastMessage = message ?? string.Empty
            };
        }

        public static IA02WorkResult From(bool completed, bool changed, int operations, int events, float consumedMilliseconds, string message, bool deferred = false)
        {
            return new IA02WorkResult
            {
                Completed = completed,
                Deferred = deferred,
                Changed = changed,
                Operations = Mathf.Max(0, operations),
                Events = Mathf.Max(0, events),
                ConsumedMilliseconds = Mathf.Max(0f, consumedMilliseconds),
                LastMessage = message ?? string.Empty
            };
        }
    }

    public sealed class IA02ScheduledSlice
    {
        public IA02Controller Controller;
        public IA02WorkBudget Budget;
        public float DueAt;
        public int Priority;
        public string Reason = string.Empty;
    }

    public sealed class IA02SchedulerPlan
    {
        public float FrameBudgetMs;
        public float RemainingBudgetMs;
        public int ReadyCount;
        public int ScheduledCount;
        public string Summary = string.Empty;
        public List<IA02ScheduledSlice> Slices = new List<IA02ScheduledSlice>(8);
    }

    [Serializable]
    public sealed class IA02NationTelemetrySnapshot
    {
        public int InstanceId;
        public int NationId;
        public int TeamId;
        public string NationName = string.Empty;
        public IA02ExecutionMode ExecutionMode;
        public IA02NationMode NationMode;
        public IA02NationStage Stage;
        public IA02NationPosture Posture;
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
    public sealed class IA02TelemetrySnapshot
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
        public List<IA02NationTelemetrySnapshot> Nations = new List<IA02NationTelemetrySnapshot>(8);
    }

    [Serializable]
    public sealed class IA02WorldEntityRecord
    {
        public string EntityId = string.Empty;
        public string CommandId = string.Empty;
        public string StructureId = string.Empty;
        public string PrefabId = string.Empty;
        public string LotId = string.Empty;
        public int InstanceId;
        public int NationId;
        public int TeamId;
        public IA02StrategicRole StrategicRole = IA02StrategicRole.None;
        public string DisplayName = string.Empty;
        public IA02WorldEntityKind Kind = IA02WorldEntityKind.Unknown;
        public IA02WorldDomain Domain = IA02WorldDomain.Unknown;
        public string Category = string.Empty;
        public string RegionKey = string.Empty;
        public Vector3 Position = Vector3.zero;
        public bool Operational = true;
        public int Version;
        public string State = string.Empty;
        public string Source = string.Empty;

        [NonSerialized] public UnityEngine.Object NativeObject;

        public IA02WorldEntityRecord Clone()
        {
            return new IA02WorldEntityRecord
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
    public sealed class IA02ResourceRecord
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
    public sealed class IA02PopulationRecord
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
    public sealed class IA02DomainRecord
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
    public sealed class IA02CacheEntry
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
    public sealed class IA02TimerEntry
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
    /// Indica quando a IA02 é a autoridade de infraestrutura de um time.
    /// O BrainMaster continua podendo produzir unidades e emitir ordens táticas.
    /// </summary>
    public static class IA02ConstructionAuthority
    {
        public static bool IsOwner(int teamId)
        {
            if (teamId <= 0)
            {
                return false;
            }

            IA02Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA02Controller>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                IA02Controller controller = controllers[i];
                if (controller == null || !controller.isActiveAndEnabled || controller.TeamId != teamId)
                {
                    continue;
                }

                if (controller.ExecutionMode == IA02ExecutionMode.Full
                    || controller.ExecutionMode == IA02ExecutionMode.Hybrid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
