using UnityEngine;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Estado temporal de um contato apresentado a uma camada de inteligencia.
    /// O valor nao autoriza combate e nao substitui RTSVisibilityService.
    /// </summary>
    public enum RTSTacticalContactState
    {
        Current,
        Estimated,
        LastKnown,
        Expired
    }

    public enum RTSTacticalContactRelation
    {
        Unknown,
        Friendly,
        Hostile,
        Neutral,
        Civilian
    }

    public enum RTSTacticalContactIdentification
    {
        Unknown,
        Probable,
        Identified
    }

    public enum RTSTacticalContactThreatState
    {
        Unknown,
        None,
        Suspected,
        Confirmed
    }

    public enum RTSTacticalVerticalKind
    {
        Unknown,
        Altitude,
        Depth
    }

    public enum RTSTacticalContactCategory
    {
        Unknown,
        Naval,
        Submarine,
        Aerial,
        Ground,
        Structure,
        Missile,
        SensorReport
    }

    /// <summary>
    /// Snapshot seguro para a futura interface Tactical Display.
    ///
    /// TargetInstanceId, TargetTeamId e IdentificationLabel so sao preenchidos
    /// quando uma observacao atual permite revelar a identidade. Contatos
    /// antigos continuam selecionaveis como memoria, mas nao vazam o alvo real.
    /// </summary>
    public readonly struct RTSTacticalContactSnapshot
    {
        public readonly string ContactId;
        public readonly int ObserverTeamId;
        public readonly RTSTacticalContactState State;
        public readonly RTSTacticalContactRelation Relation;
        public readonly RTSTacticalContactIdentification Identification;
        public readonly RTSTacticalContactThreatState ThreatState;
        public readonly RTSTacticalContactCategory Category;
        public readonly RTSDetectionSource DetectionSource;
        public readonly string DetectionOrigin;
        public readonly float Confidence;

        /// <summary>
        /// Posicao para apresentacao. Pode ser atual, estimada ou a ultima
        /// conhecida conforme State e PositionIsEstimated.
        /// </summary>
        public readonly Vector3 Position;
        public readonly bool HasPosition;
        public readonly bool PositionIsEstimated;
        public readonly Vector3 LastKnownPosition;
        public readonly bool HasLastKnownPosition;
        public readonly Vector3 EstimatedVelocity;
        public readonly bool HasEstimatedVelocity;
        public readonly float UncertaintyRadiusMeters;
        public readonly RTSTacticalVerticalKind VerticalKind;
        public readonly float VerticalValueMeters;
        public readonly bool HasVerticalMeasurement;
        public readonly bool VerticalIsEstimated;

        public readonly float LastSeenAt;
        public readonly float ValidUntil;
        public readonly float LastKnownUntil;

        /// <summary>
        /// Identidade somente durante uma observacao atual permitida pela
        /// fonte. Nunca deve ser usado para autorizar ataque.
        /// </summary>
        public readonly bool IdentityRevealed;
        public readonly int TargetInstanceId;
        public readonly int TargetTeamId;
        public readonly string IdentificationLabel;

        public bool IsDetectedNow => State == RTSTacticalContactState.Current;
        public bool HasLivePosition => HasPosition
            && IsDetectedNow
            && !PositionIsEstimated;

        public RTSTacticalContactSnapshot(
            string contactId,
            int observerTeamId,
            RTSTacticalContactState state,
            RTSTacticalContactRelation relation,
            RTSTacticalContactIdentification identification,
            RTSTacticalContactThreatState threatState,
            RTSTacticalContactCategory category,
            RTSDetectionSource detectionSource,
            string detectionOrigin,
            float confidence,
            Vector3 position,
            bool hasPosition,
            bool positionIsEstimated,
            Vector3 lastKnownPosition,
            bool hasLastKnownPosition,
            Vector3 estimatedVelocity,
            bool hasEstimatedVelocity,
            float uncertaintyRadiusMeters,
            RTSTacticalVerticalKind verticalKind,
            float verticalValueMeters,
            bool hasVerticalMeasurement,
            bool verticalIsEstimated,
            float lastSeenAt,
            float validUntil,
            float lastKnownUntil,
            bool identityRevealed,
            int targetInstanceId,
            int targetTeamId,
            string identificationLabel)
        {
            ContactId = contactId;
            ObserverTeamId = observerTeamId;
            State = state;
            // A relationship/classification may come from a live sensor or IFF
            // report without revealing the target identity. Do not preserve
            // those live assertions after the track itself is stale.
            Relation = state == RTSTacticalContactState.Current
                ? relation
                : RTSTacticalContactRelation.Unknown;
            Identification = state == RTSTacticalContactState.Current
                ? identification
                : RTSTacticalContactIdentification.Unknown;
            ThreatState = state == RTSTacticalContactState.Current
                ? threatState
                : RTSTacticalContactThreatState.Unknown;
            Category = category;
            DetectionSource = detectionSource;
            DetectionOrigin = detectionOrigin ?? string.Empty;
            Confidence = Mathf.Clamp01(confidence);
            Position = position;
            HasPosition = hasPosition;
            PositionIsEstimated = positionIsEstimated;
            LastKnownPosition = lastKnownPosition;
            HasLastKnownPosition = hasLastKnownPosition;
            EstimatedVelocity = estimatedVelocity;
            HasEstimatedVelocity = hasEstimatedVelocity;
            UncertaintyRadiusMeters = Mathf.Max(0f, uncertaintyRadiusMeters);
            HasVerticalMeasurement = hasVerticalMeasurement && verticalKind != RTSTacticalVerticalKind.Unknown;
            VerticalKind = HasVerticalMeasurement
                ? verticalKind
                : RTSTacticalVerticalKind.Unknown;
            VerticalValueMeters = HasVerticalMeasurement ? verticalValueMeters : 0f;
            VerticalIsEstimated = HasVerticalMeasurement && verticalIsEstimated;
            LastSeenAt = lastSeenAt;
            ValidUntil = validUntil;
            LastKnownUntil = lastKnownUntil;
            IdentityRevealed = identityRevealed;
            TargetInstanceId = identityRevealed ? targetInstanceId : 0;
            TargetTeamId = identityRevealed ? targetTeamId : 0;
            IdentificationLabel = identityRevealed
                ? identificationLabel ?? string.Empty
                : string.Empty;
        }
    }
}
