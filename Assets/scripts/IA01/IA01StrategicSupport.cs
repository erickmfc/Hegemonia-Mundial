using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    // Módulo estrutural: permanece inerte quando as opções de ativação estão desligadas.
    public enum IA01BallisticMissileType
    {
        Conventional = 0,
        Nuclear = 1
    }

    public enum IA01BallisticWarheadType
    {
        Conventional = 0,
        Nuclear = 1
    }

    public enum IA01LeaderVictoryMode
    {
        Disabled = 0,
        CapitalOnly = 1,
        PresidentOnly = 2,
        PresidentOrCapital = 3,
        PresidentAndCapital = 4,
        GovernmentCollapseScore = 5
    }

    public enum IA01LeaderObjectiveState
    {
        Unavailable = 0,
        Suspected = 1,
        Located = 2,
        Tracking = 3,
        PlanningIncursion = 4,
        GatheringForces = 5,
        IncursionActive = 6,
        CaptureAttempt = 7,
        Extraction = 8,
        Captured = 9,
        Eliminated = 10,
        Escaped = 11,
        LostContact = 12,
        Completed = 13,
        Failed = 14,
        Expired = 15
    }

    public enum IA01LeaderEventType
    {
        LeaderRegistered = 0,
        LeaderMoved = 1,
        LeaderEnteredBuilding = 2,
        LeaderEnteredVehicle = 3,
        LeaderStartedTravel = 4,
        LeaderVisitedCountry = 5,
        LeaderReturnedHome = 6,
        LeaderAttacked = 7,
        LeaderEvacuated = 8,
        LeaderLocated = 9,
        LeaderLostContact = 10,
        LeaderCaptured = 11,
        LeaderReleased = 12,
        LeaderRescued = 13,
        LeaderKilled = 14
    }

    public enum IA01CountryTransferPhase
    {
        Idle = 0,
        VictoryConfirmed = 1,
        GovernmentCollapsed = 2,
        CombatSuspended = 3,
        TransferQueued = 4,
        RegionsTransferred = 5,
        CitiesTransferred = 6,
        StructuresTransferred = 7,
        ResourcesTransferred = 8,
        UnitsResolved = 9,
        DiplomacyUpdated = 10,
        DefeatedCountryDeactivated = 11,
        Completed = 12
    }

    [Serializable]
    public sealed class IA01StrategicOptions
    {
        [Header("Ativação segura")]
        public bool EnableBallisticThreatEscalation;
        public bool EnableNuclearThreatEscalation;
        public bool EnableStrategicLeaderIntegration;
        public bool EnableLeaderProtection;
        public bool EnableLeaderWarObjective;
        public bool EnableLeaderVictoryCondition;
        public bool EnableCountryTransferOperation;

        [Header("Guerra estratégica")]
        [Min(0.1f)] public float BallisticWarDurationMultiplier = 1.35f;
        [Min(0.1f)] public float NuclearWarDurationMultiplier = 2.5f;
        [Min(1f)] public float StrategicIncursionMinimumDuration = 30f;
        [Min(1f)] public float StrategicIncursionMaximumDuration = 900f;
        [Min(1f)] public float WarObjectiveReevaluationInterval = 12f;
        [Min(1f)] public float StrategicThreatExpirationTime = 180f;
        [Range(0.05f, 0.8f)] public float MaximumWartimeMilitaryBudgetShare = 0.8f;
        [Range(0f, 1f)] public float BallisticMobilizationMultiplier = 1.25f;
        [Range(0f, 1f)] public float NuclearMobilizationMultiplier = 1.75f;
        [Range(0f, 1f)] public float MinimumEssentialEconomyShare = 0.2f;
        [Min(0.01f)] public float PostWarDemobilizationSpeed = 0.08f;

        [Header("Objetivo presidencial")]
        public IA01LeaderVictoryMode LeaderVictoryMode = IA01LeaderVictoryMode.CapitalOnly;
        [Range(0f, 1f)] public float LeaderPositionConfidenceDecay = 0.08f;
        [Min(1f)] public float LeaderSearchExpiration = 120f;
        [Min(1)] public int MaximumLeaderObjectivesPerCampaign = 1;
        [Min(1)] public int MaximumOwnershipTransfersPerSlice = 12;
        [Min(0.1f)] public float CountryTransferFrameBudgetMs = 0.5f;
        [Min(1)] public int MaximumBallisticEventsProcessedPerSlice = 4;

        public IA01StrategicOptions Clone()
        {
            return (IA01StrategicOptions)MemberwiseClone();
        }

        public bool BallisticEnabled => EnableBallisticThreatEscalation || EnableNuclearThreatEscalation;
    }

    [Serializable]
    public sealed class IA01StrategicLeaderReference
    {
        public string LeaderId = string.Empty;
        public int CountryId;
        public bool IsRegistered;
        public bool IsAlive = true;
        public bool IsCaptured;
        public bool IsMissing;
        public bool IsTravelling;
        public bool IsVisitingForeignCountry;
        public bool IsInsideCapital;
        public bool IsInsideGovernmentBuilding;
        public string CurrentRegionId = string.Empty;
        public string CurrentBuildingId = string.Empty;
        public string CurrentVehicleId = string.Empty;
        public Vector3 LastConfirmedPosition;
        public Vector3 LastKnownPosition;
        [Range(0f, 1f)] public float PositionConfidence;
        public int CapturingCountryId;
        public float LastStatusUpdateTime;
        public IA01LeaderObjectiveState ObjectiveState = IA01LeaderObjectiveState.Unavailable;

        public bool Exists => IsRegistered && !string.IsNullOrEmpty(LeaderId);

        public void Clear()
        {
            LeaderId = string.Empty;
            IsRegistered = false;
            IsAlive = true;
            IsCaptured = false;
            IsMissing = false;
            IsTravelling = false;
            IsVisitingForeignCountry = false;
            IsInsideCapital = false;
            IsInsideGovernmentBuilding = false;
            CurrentRegionId = string.Empty;
            CurrentBuildingId = string.Empty;
            CurrentVehicleId = string.Empty;
            LastConfirmedPosition = Vector3.zero;
            LastKnownPosition = Vector3.zero;
            PositionConfidence = 0f;
            CapturingCountryId = 0;
            LastStatusUpdateTime = 0f;
            ObjectiveState = IA01LeaderObjectiveState.Unavailable;
        }
    }

    [Serializable]
    public sealed class IA01BallisticThreatRecord
    {
        public string EventId = Guid.NewGuid().ToString("N");
        public int VictimCountryId;
        public int SuspectedCountryId;
        public int ConfirmedCountryId;
        public Vector3 ImpactPosition;
        public Vector3 PredictedTargetPosition;
        public Vector3 DetectedArrivalDirection;
        public Vector3 ProbableLaunchArea;
        public Vector3 KnownLaunchPosition;
        public bool LaunchPositionKnown;
        public IA01BallisticMissileType MissileType;
        public IA01BallisticWarheadType WarheadType;
        public int LaunchCount = 1;
        public float Damage;
        public string InfrastructureHit = string.Empty;
        public float AuthorshipConfidence;
        public int SuggestedWarLevel = 4;
        public float CreatedAt;
        public float ExpiresAt;

        public bool IsNuclear => MissileType == IA01BallisticMissileType.Nuclear || WarheadType == IA01BallisticWarheadType.Nuclear;
        public bool IsConfirmed => ConfirmedCountryId > 0;
    }

    /// <summary>
    /// Registro por eventos. Ele não procura objetos na cena e não presume a
    /// base de lançamento sem evidência suficiente.
    /// </summary>
    public sealed class IA01BallisticThreatTracker
    {
        private readonly List<IA01BallisticThreatRecord> active = new List<IA01BallisticThreatRecord>(8);
        private readonly int countryId;
        private readonly IA01StrategicOptions options;

        public IReadOnlyList<IA01BallisticThreatRecord> Active => active;

        public IA01BallisticThreatTracker(int countryId, IA01StrategicOptions options)
        {
            this.countryId = countryId;
            this.options = options ?? new IA01StrategicOptions();
        }

        public IA01BallisticThreatRecord Register(
            Vector3 impactPosition,
            Vector3 predictedTargetPosition,
            Vector3 arrivalDirection,
            Vector3 probableLaunchArea,
            IA01BallisticMissileType missileType,
            IA01BallisticWarheadType warheadType,
            int launchCount,
            float damage,
            string infrastructureHit,
            int suspectedCountryId = 0,
            int confirmedCountryId = 0,
            Vector3 knownLaunchPosition = default(Vector3),
            bool launchPositionKnown = false,
            float authorshipConfidence = 0f,
            float now = 0f)
        {
            if (!options.BallisticEnabled) return null;

            bool nuclear = missileType == IA01BallisticMissileType.Nuclear || warheadType == IA01BallisticWarheadType.Nuclear;
            if (nuclear && !options.EnableNuclearThreatEscalation) return null;

            IA01BallisticThreatRecord record = new IA01BallisticThreatRecord
            {
                VictimCountryId = countryId,
                SuspectedCountryId = suspectedCountryId,
                ConfirmedCountryId = confirmedCountryId,
                ImpactPosition = impactPosition,
                PredictedTargetPosition = predictedTargetPosition,
                DetectedArrivalDirection = arrivalDirection,
                ProbableLaunchArea = probableLaunchArea,
                KnownLaunchPosition = knownLaunchPosition,
                LaunchPositionKnown = launchPositionKnown && (confirmedCountryId > 0 || authorshipConfidence >= 0.8f),
                MissileType = missileType,
                WarheadType = warheadType,
                LaunchCount = Mathf.Max(1, launchCount),
                Damage = Mathf.Max(0f, damage),
                InfrastructureHit = infrastructureHit ?? string.Empty,
                AuthorshipConfidence = Mathf.Clamp01(authorshipConfidence),
                SuggestedWarLevel = ResolveWarLevel(nuclear, confirmedCountryId > 0, launchCount, damage, infrastructureHit),
                CreatedAt = now,
                ExpiresAt = now + Mathf.Max(1f, options.StrategicThreatExpirationTime * (nuclear ? options.NuclearWarDurationMultiplier : options.BallisticWarDurationMultiplier))
            };

            active.Add(record);
            return record;
        }

        public int Expire(float now, int maxItems)
        {
            int removed = 0;
            for (int i = active.Count - 1; i >= 0 && removed < Mathf.Max(1, maxItems); i--)
            {
                if (active[i] == null || active[i].ExpiresAt > 0f && active[i].ExpiresAt <= now)
                {
                    active.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        private static int ResolveWarLevel(bool nuclear, bool confirmed, int launchCount, float damage, string infrastructureHit)
        {
            if (nuclear && confirmed) return 1;
            bool grave = launchCount >= 3 || damage >= 1000f || ContainsStrategicTarget(infrastructureHit);
            if (grave && confirmed) return 1;
            if (confirmed || grave) return 2;
            return 4;
        }

        private static bool ContainsStrategicTarget(string value)
        {
            string text = (value ?? string.Empty).ToLowerInvariant();
            return text.Contains("capital") || text.Contains("prefeitura") || text.Contains("aeroporto") || text.Contains("base") || text.Contains("usina") || text.Contains("estaleiro");
        }
    }

    public sealed class IA01CountryTransferOperation
    {
        private readonly IA01StrategicOptions options;
        private int remainingRegions;
        private int remainingCities;
        private int remainingStructures;
        private int remainingResources;
        private int remainingUnits;

        public IA01CountryTransferPhase Phase { get; private set; } = IA01CountryTransferPhase.Idle;
        public int WinnerCountryId { get; private set; }
        public int DefeatedCountryId { get; private set; }
        public bool IsActive => Phase != IA01CountryTransferPhase.Idle && Phase != IA01CountryTransferPhase.Completed;
        public bool IsCompleted => Phase == IA01CountryTransferPhase.Completed;

        public IA01CountryTransferOperation(IA01StrategicOptions options)
        {
            this.options = options ?? new IA01StrategicOptions();
        }

        public bool Begin(int winnerCountryId, int defeatedCountryId, int regions, int cities, int structures, int resources, int units)
        {
            if (!options.EnableCountryTransferOperation || IsActive || winnerCountryId <= 0 || defeatedCountryId <= 0 || winnerCountryId == defeatedCountryId) return false;
            WinnerCountryId = winnerCountryId;
            DefeatedCountryId = defeatedCountryId;
            remainingRegions = Mathf.Max(0, regions);
            remainingCities = Mathf.Max(0, cities);
            remainingStructures = Mathf.Max(0, structures);
            remainingResources = Mathf.Max(0, resources);
            remainingUnits = Mathf.Max(0, units);
            Phase = IA01CountryTransferPhase.VictoryConfirmed;
            return true;
        }

        public int ProcessSlice(int maxItems)
        {
            if (!IsActive) return 0;
            int budget = Mathf.Min(Mathf.Max(1, maxItems), Mathf.Max(1, options.MaximumOwnershipTransfersPerSlice));
            int processed = 0;
            while (processed < budget && Phase != IA01CountryTransferPhase.Completed)
            {
                switch (Phase)
                {
                    case IA01CountryTransferPhase.VictoryConfirmed: Phase = IA01CountryTransferPhase.GovernmentCollapsed; break;
                    case IA01CountryTransferPhase.GovernmentCollapsed: Phase = IA01CountryTransferPhase.CombatSuspended; break;
                    case IA01CountryTransferPhase.CombatSuspended: Phase = IA01CountryTransferPhase.TransferQueued; break;
                    case IA01CountryTransferPhase.TransferQueued: Phase = IA01CountryTransferPhase.RegionsTransferred; break;
                    case IA01CountryTransferPhase.RegionsTransferred: if (Consume(ref remainingRegions, ref processed, budget)) Phase = IA01CountryTransferPhase.CitiesTransferred; break;
                    case IA01CountryTransferPhase.CitiesTransferred: if (Consume(ref remainingCities, ref processed, budget)) Phase = IA01CountryTransferPhase.StructuresTransferred; break;
                    case IA01CountryTransferPhase.StructuresTransferred: if (Consume(ref remainingStructures, ref processed, budget)) Phase = IA01CountryTransferPhase.ResourcesTransferred; break;
                    case IA01CountryTransferPhase.ResourcesTransferred: if (Consume(ref remainingResources, ref processed, budget)) Phase = IA01CountryTransferPhase.UnitsResolved; break;
                    case IA01CountryTransferPhase.UnitsResolved: if (Consume(ref remainingUnits, ref processed, budget)) Phase = IA01CountryTransferPhase.DiplomacyUpdated; break;
                    case IA01CountryTransferPhase.DiplomacyUpdated: Phase = IA01CountryTransferPhase.DefeatedCountryDeactivated; break;
                    case IA01CountryTransferPhase.DefeatedCountryDeactivated: Phase = IA01CountryTransferPhase.Completed; break;
                    default: Phase = IA01CountryTransferPhase.Completed; break;
                }
            }
            return processed;
        }

        private static bool Consume(ref int remaining, ref int processed, int budget)
        {
            if (remaining <= 0) return true;
            int amount = Mathf.Min(remaining, budget - processed);
            remaining -= amount;
            processed += amount;
            return remaining <= 0;
        }
    }

    public sealed class IA01StrategicSupport
    {
        private readonly IA01StrategicOptions options;
        private readonly int countryId;
        private readonly List<IA01LeaderEventType> leaderEvents = new List<IA01LeaderEventType>(4);

        public IA01StrategicOptions Options => options;
        public IA01StrategicLeaderReference Leader { get; } = new IA01StrategicLeaderReference();
        public IA01BallisticThreatTracker BallisticThreats { get; }
        public IA01CountryTransferOperation CountryTransfer { get; }
        public string Status { get; private set; } = "Suporte estratégico desativado.";

        public IA01StrategicSupport(int countryId, IA01StrategicOptions sourceOptions)
        {
            this.countryId = countryId;
            options = sourceOptions != null ? sourceOptions.Clone() : new IA01StrategicOptions();
            BallisticThreats = new IA01BallisticThreatTracker(countryId, options);
            CountryTransfer = new IA01CountryTransferOperation(options);
            RefreshStatus();
        }

        public IA01BallisticThreatRecord RegisterBallisticImpact(Vector3 impact, Vector3 predictedTarget, Vector3 arrivalDirection, Vector3 probableLaunchArea, IA01BallisticMissileType missileType, IA01BallisticWarheadType warheadType, int launches, float damage, string infrastructure, int suspectedCountryId = 0, int confirmedCountryId = 0, Vector3 knownLaunchPosition = default(Vector3), bool launchPositionKnown = false, float confidence = 0f, float now = 0f)
        {
            IA01BallisticThreatRecord record = BallisticThreats.Register(impact, predictedTarget, arrivalDirection, probableLaunchArea, missileType, warheadType, launches, damage, infrastructure, suspectedCountryId, confirmedCountryId, knownLaunchPosition, launchPositionKnown, confidence, now);
            if (record != null) Status = "Ameaça balística registrada: nível " + record.SuggestedWarLevel + ".";
            return record;
        }

        public bool RegisterLeaderEvent(IA01LeaderEventType eventType, string leaderId, Vector3 position, float confidence, float now, int relatedCountryId = 0, string regionId = null, string buildingId = null, string vehicleId = null)
        {
            if (!options.EnableStrategicLeaderIntegration) return false;
            if (eventType == IA01LeaderEventType.LeaderRegistered)
            {
                Leader.LeaderId = leaderId ?? string.Empty;
                Leader.CountryId = countryId;
                Leader.IsRegistered = !string.IsNullOrEmpty(Leader.LeaderId);
                Leader.IsAlive = true;
            }
            if (!Leader.Exists) return false;
            Leader.LastStatusUpdateTime = now;
            Leader.LastKnownPosition = position;
            Leader.PositionConfidence = Mathf.Clamp01(confidence);
            Leader.CurrentRegionId = regionId ?? Leader.CurrentRegionId;
            Leader.CurrentBuildingId = buildingId ?? Leader.CurrentBuildingId;
            Leader.CurrentVehicleId = vehicleId ?? Leader.CurrentVehicleId;
            Leader.IsVisitingForeignCountry = relatedCountryId > 0 && relatedCountryId != countryId;
            ApplyLeaderEvent(eventType, relatedCountryId);
            leaderEvents.Add(eventType);
            if (leaderEvents.Count > 8) leaderEvents.RemoveAt(0);
            return true;
        }

        public bool BeginCountryTransfer(int winnerCountryId, int defeatedCountryId, int regions, int cities, int structures, int resources, int units)
        {
            bool started = CountryTransfer.Begin(winnerCountryId, defeatedCountryId, regions, cities, structures, resources, units);
            if (started) Status = "Transferência territorial iniciada por etapas.";
            return started;
        }

        public int ProcessSlice(float now, int maxOperations)
        {
            int operations = BallisticThreats.Expire(now, options.MaximumBallisticEventsProcessedPerSlice);
            operations += CountryTransfer.ProcessSlice(Mathf.Min(maxOperations, options.MaximumOwnershipTransfersPerSlice));
            if (options.EnableStrategicLeaderIntegration && Leader.Exists && Leader.PositionConfidence > 0f)
                Leader.PositionConfidence = Mathf.Clamp01(Leader.PositionConfidence - options.LeaderPositionConfidenceDecay * Mathf.Max(0f, Time.unscaledDeltaTime));
            RefreshStatus();
            return operations;
        }

        private void ApplyLeaderEvent(IA01LeaderEventType eventType, int relatedCountryId)
        {
            switch (eventType)
            {
                case IA01LeaderEventType.LeaderAttacked: Leader.ObjectiveState = IA01LeaderObjectiveState.Located; break;
                case IA01LeaderEventType.LeaderEvacuated: Leader.IsMissing = false; Leader.IsTravelling = true; break;
                case IA01LeaderEventType.LeaderLostContact: Leader.IsMissing = true; Leader.ObjectiveState = IA01LeaderObjectiveState.LostContact; break;
                case IA01LeaderEventType.LeaderCaptured: Leader.IsCaptured = true; Leader.CapturingCountryId = relatedCountryId; Leader.ObjectiveState = IA01LeaderObjectiveState.Captured; break;
                case IA01LeaderEventType.LeaderReleased:
                case IA01LeaderEventType.LeaderRescued: Leader.IsCaptured = false; Leader.CapturingCountryId = 0; Leader.ObjectiveState = IA01LeaderObjectiveState.Completed; break;
                case IA01LeaderEventType.LeaderKilled: Leader.IsAlive = false; Leader.IsCaptured = false; Leader.ObjectiveState = IA01LeaderObjectiveState.Eliminated; break;
                case IA01LeaderEventType.LeaderLocated: Leader.IsMissing = false; Leader.ObjectiveState = IA01LeaderObjectiveState.Located; break;
            }
        }

        private void RefreshStatus()
        {
            if (CountryTransfer.IsActive) return;
            if (options.EnableStrategicLeaderIntegration && Leader.Exists) return;
            if (BallisticThreats.Active.Count == 0) Status = options.BallisticEnabled ? "Suporte estratégico pronto; sem ameaças ativas." : "Suporte estratégico desativado.";
        }
    }
}
