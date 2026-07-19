using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [Serializable]
    public sealed class IA01NationProfileSnapshot
    {
        public string profileKey = string.Empty;
        public IA01NationPersonality personality = IA01NationPersonality.Balanced;
        public IA01NationDoctrine doctrine = IA01NationDoctrine.Balanced;
        public IA01ExecutionMode defaultExecutionMode = IA01ExecutionMode.Full;
        public IA01NationMode defaultNationMode = IA01NationMode.Normal;
        public IA01NationStage defaultStage = IA01NationStage.Initialization;
        public IA01NationPosture defaultPosture = IA01NationPosture.Development;
        public float aggressionWeight = 0.5f;
        public float cautionWeight = 0.5f;
        public float commercialWeight = 0.5f;
        public float diplomacyWeight = 0.5f;
        public float militaryWeight = 0.5f;
        public float selfSufficiencyWeight = 0.5f;
        public float expansionWeight = 0.5f;
        public float opportunismWeight = 0.5f;
        public float landWeight = 0.5f;
        public float airWeight = 0.5f;
        public float navalWeight = 0.5f;
        public float defenseWeight = 0.5f;
        public float economyWeight = 0.5f;
        public float industryWeight = 0.5f;
        public float agricultureWeight = 0.5f;
        public float technologyWeight = 0.5f;
        public float riskTolerance = 50f;
        public float militaryPriority = 0.34f;
        public float economyPriority = 0.33f;
        public float diplomacyPriority = 0.33f;
        public string preferredFederation = string.Empty;
        public int initialTreasury = 10000;
        public float baseCadenceSeconds = 0.65f;
        public float minSliceMilliseconds = 0.08f;
        public float maxSliceMilliseconds = 0.75f;
        public int maxOperationsPerSlice = 8;
        public int maxEventsPerSlice = 12;
        public bool allowObserverWriteBacks;
        public bool preferDeterministicBoot = true;
        public bool allowSaveIntegration = true;
        public bool allowAutoBootstrap = true;
        public int emergencyReserve = 2500;
        public int minimumConstructionReserve = 1000;
        public float maximumConstructionBudgetPercent = 0.25f;
        public float maximumMaintenancePercent = 0.25f;
        public float minimumAcceptableFps = 20f;
        public float maxIaFrameBudgetMs = 6.0f;
        public float maxBuildPlannerBudgetMs = 2.5f;
        public int maxCandidatesPerSlice = 8;
        // Oito candidatos podem consumir até três verificações cada (solo,
        // ocupação e estrada). O limite anterior interrompia a busca antes
        // de avaliar todos os locais válidos e deixava a IA em NoValidLot.
        public int maxPhysicsChecksPerSlice = 32;
    }

    [Serializable]
    public sealed class IA01ConstructionPhaseLimit
    {
        public IA01NationStage stage = IA01NationStage.Initialization;
        public int maxTotalStructures = 2;
        public int maxCapital = 1;
        public int maxResidential = 1;
        public int maxFoodProduction = 1;
        public int maxEnergyProduction = 1;
        public int maxStorage = 0;
        public int maxLogistics = 0;
        public int maxIndustrial = 0;
        public int maxDefense = 0;
        public float maxCoveragePercent = 0.35f;
        public float minimumSpacing = 12f;
        public int reservedOpenSpacePercent = 70;
        public int roadSpacePercent = 0;
    }

    [Serializable]
    public sealed class IA01ConstructionGovernorSettings
    {
        private const int DefaultEmergencyReserve = 2500;
        private const int DefaultMinimumConstructionReserve = 1000;
        private const float DefaultMaximumConstructionBudgetPercent = 0.25f;
        private const float DefaultMaximumMaintenancePercent = 0.25f;
        private const float DefaultMinimumAcceptableFps = 20f;
        private const float DefaultMaxIaFrameBudgetMs = 6.0f;
        private const float DefaultMaxBuildPlannerBudgetMs = 2.5f;
        private const int DefaultMaxCandidatesPerSlice = 8;
        private const int DefaultMaxPhysicsChecksPerSlice = 32;

        [SerializeField] private int emergencyReserve = 2500;
        [SerializeField] private int minimumConstructionReserve = 1000;
        [Range(0f, 1f)] [SerializeField] private float maximumConstructionBudgetPercent = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float maximumMaintenancePercent = 0.25f;
        [Range(1f, 120f)] [SerializeField] private float minimumAcceptableFps = 20f;
        [Range(0.01f, 16f)] [SerializeField] private float maxIaFrameBudgetMs = 6.0f;
        [Range(0.01f, 16f)] [SerializeField] private float maxBuildPlannerBudgetMs = 2.5f;
        [SerializeField, Min(1)] private int maxCandidatesPerSlice = 8;
        [SerializeField, Min(1)] private int maxPhysicsChecksPerSlice = 32;
        [SerializeField] private List<IA01ConstructionPhaseLimit> phaseLimits = new List<IA01ConstructionPhaseLimit>
        {
            new IA01ConstructionPhaseLimit
            {
                stage = IA01NationStage.Initialization,
                maxTotalStructures = 2,
                maxCapital = 1,
                maxResidential = 1,
                maxFoodProduction = 1,
                maxEnergyProduction = 1,
                maxStorage = 0,
                maxLogistics = 0,
                maxIndustrial = 0,
                maxDefense = 0,
                maxCoveragePercent = 0.25f,
                minimumSpacing = 18f,
                reservedOpenSpacePercent = 70,
                roadSpacePercent = 0
            },
            new IA01ConstructionPhaseLimit
            {
                stage = IA01NationStage.Stabilization,
                maxTotalStructures = 8,
                maxCapital = 1,
                maxResidential = 3,
                maxFoodProduction = 2,
                maxEnergyProduction = 2,
                maxStorage = 2,
                maxLogistics = 1,
                maxIndustrial = 0,
                maxDefense = 0,
                maxCoveragePercent = 0.45f,
                minimumSpacing = 14f,
                reservedOpenSpacePercent = 40,
                roadSpacePercent = 10
            },
            new IA01ConstructionPhaseLimit
            {
                stage = IA01NationStage.UrbanDevelopment,
                maxTotalStructures = 16,
                maxCapital = 1,
                maxResidential = 6,
                maxFoodProduction = 4,
                maxEnergyProduction = 4,
                // Depósitos são logísticos, não uma fonte de expansão urbana.
                // A IA01 tem três âncoras específicas para eles e nunca deve
                // cobrir a cidade de armazéns.
                maxStorage = 3,
                maxLogistics = 3,
                maxIndustrial = 2,
                maxDefense = 1,
                maxCoveragePercent = 0.60f,
                minimumSpacing = 12f,
                reservedOpenSpacePercent = 25,
                roadSpacePercent = 15
            },
            new IA01ConstructionPhaseLimit
            {
                stage = IA01NationStage.Recovering,
                maxTotalStructures = 0,
                maxCapital = 1,
                maxResidential = 0,
                maxFoodProduction = 0,
                maxEnergyProduction = 0,
                maxStorage = 0,
                maxLogistics = 0,
                maxIndustrial = 0,
                maxDefense = 0,
                maxCoveragePercent = 0.0f,
                minimumSpacing = 16f,
                reservedOpenSpacePercent = 100,
                roadSpacePercent = 0
            }
        };

        public int EmergencyReserve => emergencyReserve > 0 ? emergencyReserve : DefaultEmergencyReserve;
        public int MinimumConstructionReserve => minimumConstructionReserve > 0 ? minimumConstructionReserve : DefaultMinimumConstructionReserve;
        public float MaximumConstructionBudgetPercent => maximumConstructionBudgetPercent > 0f ? Mathf.Clamp01(maximumConstructionBudgetPercent) : DefaultMaximumConstructionBudgetPercent;
        public float MaximumMaintenancePercent => maximumMaintenancePercent > 0f ? Mathf.Clamp01(maximumMaintenancePercent) : DefaultMaximumMaintenancePercent;
        public float MinimumAcceptableFps => minimumAcceptableFps > 0f ? minimumAcceptableFps : DefaultMinimumAcceptableFps;
        public float MaxIaFrameBudgetMs => maxIaFrameBudgetMs > 0f ? maxIaFrameBudgetMs : DefaultMaxIaFrameBudgetMs;
        public float MaxBuildPlannerBudgetMs => maxBuildPlannerBudgetMs > 0f ? maxBuildPlannerBudgetMs : DefaultMaxBuildPlannerBudgetMs;
        public int MaxCandidatesPerSlice => maxCandidatesPerSlice > 0 ? maxCandidatesPerSlice : DefaultMaxCandidatesPerSlice;
        public int MaxPhysicsChecksPerSlice => maxPhysicsChecksPerSlice > 0 ? maxPhysicsChecksPerSlice : DefaultMaxPhysicsChecksPerSlice;
        public IReadOnlyList<IA01ConstructionPhaseLimit> PhaseLimits => phaseLimits;

        public IA01ConstructionPhaseLimit ResolvePhaseLimit(IA01NationStage stage)
        {
            IA01ConstructionPhaseLimit best = null;
            if (phaseLimits == null)
            {
                return null;
            }

            for (int i = 0; i < phaseLimits.Count; i++)
            {
                IA01ConstructionPhaseLimit candidate = phaseLimits[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.stage == stage)
                {
                    return candidate;
                }

                if (candidate.stage <= stage && (best == null || candidate.stage > best.stage))
                {
                    best = candidate;
                }
            }

            return best != null ? best : (phaseLimits.Count > 0 ? phaseLimits[0] : null);
        }

        public void ApplySnapshot(IA01NationProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            emergencyReserve = snapshot.emergencyReserve > 0 ? snapshot.emergencyReserve : DefaultEmergencyReserve;
            minimumConstructionReserve = snapshot.minimumConstructionReserve > 0 ? snapshot.minimumConstructionReserve : DefaultMinimumConstructionReserve;
            maximumConstructionBudgetPercent = snapshot.maximumConstructionBudgetPercent > 0f ? Mathf.Clamp01(snapshot.maximumConstructionBudgetPercent) : DefaultMaximumConstructionBudgetPercent;
            maximumMaintenancePercent = snapshot.maximumMaintenancePercent > 0f ? Mathf.Clamp01(snapshot.maximumMaintenancePercent) : DefaultMaximumMaintenancePercent;
            minimumAcceptableFps = snapshot.minimumAcceptableFps > 0f ? snapshot.minimumAcceptableFps : DefaultMinimumAcceptableFps;
            maxIaFrameBudgetMs = snapshot.maxIaFrameBudgetMs > 0f ? snapshot.maxIaFrameBudgetMs : DefaultMaxIaFrameBudgetMs;
            maxBuildPlannerBudgetMs = snapshot.maxBuildPlannerBudgetMs > 0f ? snapshot.maxBuildPlannerBudgetMs : DefaultMaxBuildPlannerBudgetMs;
            maxCandidatesPerSlice = snapshot.maxCandidatesPerSlice > 0 ? snapshot.maxCandidatesPerSlice : DefaultMaxCandidatesPerSlice;
            maxPhysicsChecksPerSlice = snapshot.maxPhysicsChecksPerSlice > 0 ? snapshot.maxPhysicsChecksPerSlice : DefaultMaxPhysicsChecksPerSlice;
        }
    }

    [CreateAssetMenu(fileName = "IA01NationProfile", menuName = "Hegemonia/IA01/Nation Profile")]
    public sealed class IA01NationProfile : ScriptableObject
    {
        [Header("Identity Defaults")]
        [SerializeField] private string profileKey = "default";
        [SerializeField] private int nationIdHint;
        [SerializeField] private int teamIdHint;
        [SerializeField] private string nationName = "Nation";
        [SerializeField] private string presidentName = "President";
        [SerializeField] private string currencyName = "Credit";
        [SerializeField] private string currencySymbol = "$";
        [SerializeField] private string countryProfile = "Neutral";
        [SerializeField] private string difficultyProfile = "normal";
        [SerializeField] private int seedOffset;

        [Header("National Shape")]
        [SerializeField] private IA01NationPersonality personality = IA01NationPersonality.Balanced;
        [SerializeField] private IA01NationDoctrine doctrine = IA01NationDoctrine.Balanced;
        [SerializeField] private IA01ExecutionMode defaultExecutionMode = IA01ExecutionMode.Full;
        [SerializeField] private IA01NationMode defaultNationMode = IA01NationMode.Normal;
        [SerializeField] private IA01NationStage defaultStage = IA01NationStage.Initialization;
        [SerializeField] private IA01NationPosture defaultPosture = IA01NationPosture.Development;

        [Header("Weights")]
        [Range(0f, 1f)] [SerializeField] private float aggressionWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float cautionWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float commercialWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float diplomacyWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float militaryWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float selfSufficiencyWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float expansionWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float opportunismWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float landWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float airWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float navalWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float defenseWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float economyWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float industryWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float agricultureWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float technologyWeight = 0.5f;
        [Range(0f, 100f)] [SerializeField] private float riskTolerance = 50f;
        [Range(0f, 1f)] [SerializeField] private float militaryPriority = 0.34f;
        [Range(0f, 1f)] [SerializeField] private float economyPriority = 0.33f;
        [Range(0f, 1f)] [SerializeField] private float diplomacyPriority = 0.33f;
        [SerializeField] private string preferredFederation = string.Empty;

        [Header("Scheduler")]
        [SerializeField, Min(0)] private int initialTreasury = 30000;
        [SerializeField] private float baseCadenceSeconds = 0.65f;
        [SerializeField] private float minSliceMilliseconds = 0.08f;
        [SerializeField] private float maxSliceMilliseconds = 0.75f;
        [SerializeField] private int maxOperationsPerSlice = 8;
        [SerializeField] private int maxEventsPerSlice = 12;
        [SerializeField] private bool allowObserverWriteBacks;
        [SerializeField] private bool preferDeterministicBoot = true;
        [SerializeField] private bool allowSaveIntegration = true;
        [SerializeField] private bool allowAutoBootstrap = true;
        [Header("Construction Governor")]
        [SerializeField] private IA01ConstructionGovernorSettings constructionGovernor = new IA01ConstructionGovernorSettings();

        [NonSerialized] private IA01NationIdentity runtimeIdentity;

        public string ProfileKey => profileKey;
        public int NationIdHint => nationIdHint;
        public int TeamIdHint => teamIdHint;
        public string NationName => nationName;
        public string PresidentName => presidentName;
        public string CurrencyName => currencyName;
        public string CurrencySymbol => currencySymbol;
        public string CountryProfile => countryProfile;
        public string DifficultyProfile => difficultyProfile;
        public int SeedOffset => seedOffset;
        public IA01NationPersonality Personality => personality;
        public IA01NationDoctrine Doctrine => doctrine;
        public IA01ExecutionMode DefaultExecutionMode => defaultExecutionMode;
        public IA01NationMode DefaultNationMode => defaultNationMode;
        public IA01NationStage DefaultStage => defaultStage;
        public IA01NationPosture DefaultPosture => defaultPosture;
        public float AggressionWeight => aggressionWeight;
        public float CautionWeight => cautionWeight;
        public float CommercialWeight => commercialWeight;
        public float DiplomacyWeight => diplomacyWeight;
        public float MilitaryWeight => militaryWeight;
        public float SelfSufficiencyWeight => selfSufficiencyWeight;
        public float ExpansionWeight => expansionWeight;
        public float OpportunismWeight => opportunismWeight;
        public float LandWeight => landWeight;
        public float AirWeight => airWeight;
        public float NavalWeight => navalWeight;
        public float DefenseWeight => defenseWeight;
        public float EconomyWeight => economyWeight;
        public float IndustryWeight => industryWeight;
        public float AgricultureWeight => agricultureWeight;
        public float TechnologyWeight => technologyWeight;
        public float RiskTolerance => Mathf.Clamp(riskTolerance, 0f, 100f);
        public float MilitaryPriority => militaryPriority;
        public float EconomyPriority => economyPriority;
        public float DiplomacyPriority => diplomacyPriority;
        public string PreferredFederation => preferredFederation;
        public float BaseCadenceSeconds => baseCadenceSeconds > 0f ? baseCadenceSeconds : 0.65f;
        public int InitialTreasury => initialTreasury > 0 ? initialTreasury : 30000;
        public float MinSliceMilliseconds => minSliceMilliseconds > 0f ? minSliceMilliseconds : 0.08f;
        public float MaxSliceMilliseconds => maxSliceMilliseconds > 0f ? Mathf.Max(MinSliceMilliseconds, maxSliceMilliseconds) : 0.75f;
        public int MaxOperationsPerSlice => maxOperationsPerSlice > 0 ? maxOperationsPerSlice : 8;
        public int MaxEventsPerSlice => maxEventsPerSlice > 0 ? maxEventsPerSlice : 12;
        public bool AllowObserverWriteBacks => allowObserverWriteBacks;
        public bool PreferDeterministicBoot => preferDeterministicBoot;
        public bool AllowSaveIntegration => allowSaveIntegration;
        public bool AllowAutoBootstrap => allowAutoBootstrap;
        public IA01ConstructionGovernorSettings ConstructionGovernor => constructionGovernor;
        public bool IsRuntimeBound => runtimeIdentity != null;

        public IA01NationIdentity RuntimeIdentity
        {
            get
            {
                return runtimeIdentity != null ? runtimeIdentity.Clone() : null;
            }
        }

        private void OnValidate()
        {
            aggressionWeight = Mathf.Clamp01(aggressionWeight);
            cautionWeight = Mathf.Clamp01(cautionWeight);
            commercialWeight = Mathf.Clamp01(commercialWeight);
            diplomacyWeight = Mathf.Clamp01(diplomacyWeight);
            militaryWeight = Mathf.Clamp01(militaryWeight);
            selfSufficiencyWeight = Mathf.Clamp01(selfSufficiencyWeight);
            expansionWeight = Mathf.Clamp01(expansionWeight);
            opportunismWeight = Mathf.Clamp01(opportunismWeight);
            landWeight = Mathf.Clamp01(landWeight);
            airWeight = Mathf.Clamp01(airWeight);
            navalWeight = Mathf.Clamp01(navalWeight);
            defenseWeight = Mathf.Clamp01(defenseWeight);
            economyWeight = Mathf.Clamp01(economyWeight);
            industryWeight = Mathf.Clamp01(industryWeight);
            agricultureWeight = Mathf.Clamp01(agricultureWeight);
            technologyWeight = Mathf.Clamp01(technologyWeight);
            riskTolerance = Mathf.Clamp(riskTolerance, 0f, 100f);
            float priorityTotal = militaryPriority + economyPriority + diplomacyPriority;
            if (priorityTotal <= 0.001f) { militaryPriority = 0.34f; economyPriority = 0.33f; diplomacyPriority = 0.33f; }
            else { militaryPriority /= priorityTotal; economyPriority /= priorityTotal; diplomacyPriority /= priorityTotal; }
            initialTreasury = initialTreasury > 0 ? initialTreasury : 10000;
            baseCadenceSeconds = baseCadenceSeconds > 0f ? baseCadenceSeconds : 0.65f;
            minSliceMilliseconds = minSliceMilliseconds > 0f ? minSliceMilliseconds : 0.08f;
            maxSliceMilliseconds = maxSliceMilliseconds > 0f ? Mathf.Max(minSliceMilliseconds, maxSliceMilliseconds) : 0.75f;
            maxOperationsPerSlice = maxOperationsPerSlice > 0 ? maxOperationsPerSlice : 8;
            maxEventsPerSlice = maxEventsPerSlice > 0 ? maxEventsPerSlice : 12;

            if (constructionGovernor == null)
            {
                constructionGovernor = new IA01ConstructionGovernorSettings();
            }

            constructionGovernor.ApplySnapshot(CaptureSnapshot());
        }

        public IA01NationProfileSnapshot CaptureSnapshot()
        {
            return new IA01NationProfileSnapshot
            {
                profileKey = profileKey ?? string.Empty,
                personality = personality,
                doctrine = doctrine,
                defaultExecutionMode = defaultExecutionMode,
                defaultNationMode = defaultNationMode,
                defaultStage = defaultStage,
                defaultPosture = defaultPosture,
                aggressionWeight = aggressionWeight,
                cautionWeight = cautionWeight,
                commercialWeight = commercialWeight,
                diplomacyWeight = diplomacyWeight,
                militaryWeight = militaryWeight,
                selfSufficiencyWeight = selfSufficiencyWeight,
                expansionWeight = expansionWeight,
                opportunismWeight = opportunismWeight,
                landWeight = landWeight,
                airWeight = airWeight,
                navalWeight = navalWeight,
                defenseWeight = defenseWeight,
                economyWeight = economyWeight,
                industryWeight = industryWeight,
                agricultureWeight = agricultureWeight,
                technologyWeight = technologyWeight,
                riskTolerance = riskTolerance,
                militaryPriority = militaryPriority,
                economyPriority = economyPriority,
                diplomacyPriority = diplomacyPriority,
                preferredFederation = preferredFederation ?? string.Empty,
                initialTreasury = initialTreasury,
                baseCadenceSeconds = baseCadenceSeconds,
                minSliceMilliseconds = minSliceMilliseconds,
                maxSliceMilliseconds = maxSliceMilliseconds,
                maxOperationsPerSlice = maxOperationsPerSlice,
                maxEventsPerSlice = maxEventsPerSlice,
                allowObserverWriteBacks = allowObserverWriteBacks,
                preferDeterministicBoot = preferDeterministicBoot,
                allowSaveIntegration = allowSaveIntegration,
                allowAutoBootstrap = allowAutoBootstrap,
                emergencyReserve = constructionGovernor != null ? constructionGovernor.EmergencyReserve : 2500,
                minimumConstructionReserve = constructionGovernor != null ? constructionGovernor.MinimumConstructionReserve : 1000,
                maximumConstructionBudgetPercent = constructionGovernor != null ? constructionGovernor.MaximumConstructionBudgetPercent : 0.25f,
                maximumMaintenancePercent = constructionGovernor != null ? constructionGovernor.MaximumMaintenancePercent : 0.25f,
                minimumAcceptableFps = constructionGovernor != null ? constructionGovernor.MinimumAcceptableFps : 20f,
                maxIaFrameBudgetMs = constructionGovernor != null ? constructionGovernor.MaxIaFrameBudgetMs : 6.0f,
                maxBuildPlannerBudgetMs = constructionGovernor != null ? constructionGovernor.MaxBuildPlannerBudgetMs : 2.5f,
                maxCandidatesPerSlice = constructionGovernor != null ? constructionGovernor.MaxCandidatesPerSlice : 8,
                maxPhysicsChecksPerSlice = constructionGovernor != null ? constructionGovernor.MaxPhysicsChecksPerSlice : 16
            };
        }

        public void ApplySnapshot(IA01NationProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            profileKey = snapshot.profileKey ?? profileKey;
            personality = snapshot.personality;
            doctrine = snapshot.doctrine;
            defaultExecutionMode = snapshot.defaultExecutionMode;
            defaultNationMode = snapshot.defaultNationMode;
            defaultStage = snapshot.defaultStage;
            defaultPosture = snapshot.defaultPosture;
            aggressionWeight = Mathf.Clamp01(snapshot.aggressionWeight);
            cautionWeight = Mathf.Clamp01(snapshot.cautionWeight);
            commercialWeight = Mathf.Clamp01(snapshot.commercialWeight);
            diplomacyWeight = Mathf.Clamp01(snapshot.diplomacyWeight);
            militaryWeight = Mathf.Clamp01(snapshot.militaryWeight);
            selfSufficiencyWeight = Mathf.Clamp01(snapshot.selfSufficiencyWeight);
            expansionWeight = Mathf.Clamp01(snapshot.expansionWeight);
            opportunismWeight = Mathf.Clamp01(snapshot.opportunismWeight);
            landWeight = Mathf.Clamp01(snapshot.landWeight);
            airWeight = Mathf.Clamp01(snapshot.airWeight);
            navalWeight = Mathf.Clamp01(snapshot.navalWeight);
            defenseWeight = Mathf.Clamp01(snapshot.defenseWeight);
            economyWeight = Mathf.Clamp01(snapshot.economyWeight);
            industryWeight = Mathf.Clamp01(snapshot.industryWeight);
            agricultureWeight = Mathf.Clamp01(snapshot.agricultureWeight);
            technologyWeight = Mathf.Clamp01(snapshot.technologyWeight);
            riskTolerance = Mathf.Clamp(snapshot.riskTolerance, 0f, 100f);
            militaryPriority = Mathf.Clamp01(snapshot.militaryPriority);
            economyPriority = Mathf.Clamp01(snapshot.economyPriority);
            diplomacyPriority = Mathf.Clamp01(snapshot.diplomacyPriority);
            preferredFederation = snapshot.preferredFederation ?? preferredFederation;
            initialTreasury = Mathf.Max(0, snapshot.initialTreasury);
            baseCadenceSeconds = Mathf.Max(0.01f, snapshot.baseCadenceSeconds);
            minSliceMilliseconds = Mathf.Max(0.01f, snapshot.minSliceMilliseconds);
            maxSliceMilliseconds = Mathf.Max(minSliceMilliseconds, snapshot.maxSliceMilliseconds);
            maxOperationsPerSlice = Mathf.Max(1, snapshot.maxOperationsPerSlice);
            maxEventsPerSlice = Mathf.Max(1, snapshot.maxEventsPerSlice);
            allowObserverWriteBacks = snapshot.allowObserverWriteBacks;
            preferDeterministicBoot = snapshot.preferDeterministicBoot;
            allowSaveIntegration = snapshot.allowSaveIntegration;
            allowAutoBootstrap = snapshot.allowAutoBootstrap;
            if (constructionGovernor == null)
            {
                constructionGovernor = new IA01ConstructionGovernorSettings();
            }
            constructionGovernor.ApplySnapshot(snapshot);
        }

        public IA01NationIdentity BuildIdentity(int instanceId, int nationId, int teamId, int matchSeed, IA01ExecutionMode? executionMode = null, IA01NationMode? nationMode = null, IA01NationStage? stage = null, IA01NationPosture? posture = null, string nationNameOverride = null, string presidentNameOverride = null, string currencyNameOverride = null, string currencySymbolOverride = null, string countryProfileOverride = null, string difficultyProfileOverride = null)
        {
            int resolvedNationId = nationId > 0 ? nationId : (nationIdHint > 0 ? nationIdHint : instanceId);
            int resolvedTeamId = teamId > 0 ? teamId : (teamIdHint > 0 ? teamIdHint : resolvedNationId);

            return new IA01NationIdentity
            {
                InstanceId = instanceId,
                NationId = resolvedNationId,
                TeamId = resolvedTeamId,
                NationName = string.IsNullOrWhiteSpace(nationNameOverride) ? nationName : nationNameOverride,
                PresidentName = string.IsNullOrWhiteSpace(presidentNameOverride) ? presidentName : presidentNameOverride,
                CurrencyName = string.IsNullOrWhiteSpace(currencyNameOverride) ? currencyName : currencyNameOverride,
                CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbolOverride) ? currencySymbol : currencySymbolOverride,
                CountryProfile = string.IsNullOrWhiteSpace(countryProfileOverride) ? countryProfile : countryProfileOverride,
                DifficultyProfile = string.IsNullOrWhiteSpace(difficultyProfileOverride) ? difficultyProfile : difficultyProfileOverride,
                RandomSeed = unchecked(matchSeed + resolvedNationId + seedOffset),
                ExecutionMode = executionMode ?? defaultExecutionMode,
                NationMode = nationMode ?? defaultNationMode,
                CurrentStage = stage ?? defaultStage,
                CurrentPosture = posture ?? defaultPosture
            };
        }

        public IA01NationProfile CloneForRuntime(IA01NationIdentity identity)
        {
            IA01NationProfile clone = Instantiate(this);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.runtimeIdentity = identity != null ? identity.Clone() : null;
            if (clone.runtimeIdentity != null)
            {
                clone.nationIdHint = clone.runtimeIdentity.NationId;
                clone.teamIdHint = clone.runtimeIdentity.TeamId;
                clone.nationName = clone.runtimeIdentity.NationName;
                clone.presidentName = clone.runtimeIdentity.PresidentName;
                clone.currencyName = clone.runtimeIdentity.CurrencyName;
                clone.currencySymbol = clone.runtimeIdentity.CurrencySymbol;
                clone.countryProfile = clone.runtimeIdentity.CountryProfile;
                clone.difficultyProfile = clone.runtimeIdentity.DifficultyProfile;
            }

            return clone;
        }

        public void BindRuntimeIdentity(IA01NationIdentity identity)
        {
            runtimeIdentity = identity != null ? identity.Clone() : null;
        }

        public void ApplyGovernmentBias(DadosPaisGoverno country, string difficultyCode = null)
        {
            if (country == null)
            {
                return;
            }

            nationIdHint = country.teamId;
            teamIdHint = country.teamId;
            nationName = string.IsNullOrWhiteSpace(country.nomePais) ? nationName : country.nomePais;
            presidentName = string.IsNullOrWhiteSpace(country.nomePresidente) ? presidentName : country.nomePresidente;
            currencyName = string.IsNullOrWhiteSpace(country.nomeMoeda) ? currencyName : country.nomeMoeda;
            currencySymbol = string.IsNullOrWhiteSpace(country.simboloMoeda) ? currencySymbol : country.simboloMoeda;
            countryProfile = country.perfilIA.ToString();
            if (!string.IsNullOrWhiteSpace(difficultyCode))
            {
                difficultyProfile = difficultyCode;
            }

            switch (country.perfilIA)
            {
                case PerfilPaisIA.Militarista:
                    personality = IA01NationPersonality.Militaristic;
                    doctrine = IA01NationDoctrine.Defensive;
                    militaryWeight = 0.82f;
                    defenseWeight = 0.86f;
                    aggressionWeight = 0.74f;
                    cautionWeight = 0.42f;
                    break;
                case PerfilPaisIA.ProdutorPetroleo:
                    personality = IA01NationPersonality.SelfSufficient;
                    doctrine = IA01NationDoctrine.Industrial;
                    selfSufficiencyWeight = 0.84f;
                    industryWeight = 0.76f;
                    economyWeight = 0.69f;
                    aggressionWeight = 0.48f;
                    break;
                case PerfilPaisIA.Industrial:
                    personality = IA01NationPersonality.Commercial;
                    doctrine = IA01NationDoctrine.Industrial;
                    commercialWeight = 0.72f;
                    industryWeight = 0.82f;
                    economyWeight = 0.74f;
                    break;
                case PerfilPaisIA.Aliado:
                    personality = IA01NationPersonality.Diplomatic;
                    doctrine = IA01NationDoctrine.Economic;
                    diplomacyWeight = 0.88f;
                    commercialWeight = 0.70f;
                    cautionWeight = 0.66f;
                    break;
                case PerfilPaisIA.Rival:
                    personality = IA01NationPersonality.Opportunistic;
                    doctrine = IA01NationDoctrine.Defensive;
                    aggressionWeight = 0.66f;
                    opportunismWeight = 0.80f;
                    diplomacyWeight = 0.28f;
                    break;
                case PerfilPaisIA.Pequeno:
                    personality = IA01NationPersonality.Cautious;
                    doctrine = IA01NationDoctrine.Economic;
                    cautionWeight = 0.82f;
                    economyWeight = 0.72f;
                    selfSufficiencyWeight = 0.64f;
                    break;
                default:
                    personality = IA01NationPersonality.Balanced;
                    doctrine = IA01NationDoctrine.Balanced;
                    break;
            }

            switch (country.modoInicialIA)
            {
                case ModoInicialPaisIA.Paz:
                    defaultExecutionMode = IA01ExecutionMode.Manual;
                    defaultNationMode = IA01NationMode.Peace;
                    defaultStage = IA01NationStage.Initialization;
                    defaultPosture = IA01NationPosture.Peace;
                    break;
                case ModoInicialPaisIA.Comercial:
                    defaultExecutionMode = IA01ExecutionMode.Hybrid;
                    defaultNationMode = IA01NationMode.Normal;
                    defaultStage = IA01NationStage.Survival;
                    defaultPosture = IA01NationPosture.Development;
                    commercialWeight = Mathf.Max(commercialWeight, 0.72f);
                    break;
                case ModoInicialPaisIA.Crescimento:
                    defaultExecutionMode = IA01ExecutionMode.Hybrid;
                    defaultNationMode = IA01NationMode.Normal;
                    defaultStage = IA01NationStage.Stabilization;
                    defaultPosture = IA01NationPosture.Development;
                    break;
                case ModoInicialPaisIA.Crise:
                    defaultExecutionMode = IA01ExecutionMode.Hybrid;
                    defaultNationMode = IA01NationMode.Peace;
                    defaultStage = IA01NationStage.Recovering;
                    defaultPosture = IA01NationPosture.Recovery;
                    cautionWeight = Mathf.Max(cautionWeight, 0.72f);
                    break;
                case ModoInicialPaisIA.GuerraFria:
                    defaultExecutionMode = IA01ExecutionMode.Hybrid;
                    defaultNationMode = IA01NationMode.Normal;
                    defaultStage = IA01NationStage.Reconnaissance;
                    defaultPosture = IA01NationPosture.Alert;
                    defenseWeight = Mathf.Max(defenseWeight, 0.72f);
                    break;
                case ModoInicialPaisIA.Mobilizacao:
                    defaultExecutionMode = IA01ExecutionMode.Hybrid;
                    defaultNationMode = IA01NationMode.War;
                    defaultStage = IA01NationStage.Emergency;
                    defaultPosture = IA01NationPosture.Preparation;
                    militaryWeight = Mathf.Max(militaryWeight, 0.74f);
                    break;
                case ModoInicialPaisIA.GuerraTotal:
                    defaultExecutionMode = IA01ExecutionMode.Full;
                    defaultNationMode = IA01NationMode.War;
                    defaultStage = IA01NationStage.Emergency;
                    defaultPosture = IA01NationPosture.War;
                    aggressionWeight = Mathf.Max(aggressionWeight, 0.82f);
                    militaryWeight = Mathf.Max(militaryWeight, 0.82f);
                    break;
                case ModoInicialPaisIA.AgressivoContraJogador:
                    defaultExecutionMode = IA01ExecutionMode.Full;
                    defaultNationMode = IA01NationMode.War;
                    defaultStage = IA01NationStage.Emergency;
                    defaultPosture = IA01NationPosture.LimitedAttack;
                    aggressionWeight = Mathf.Max(aggressionWeight, 0.86f);
                    opportunismWeight = Mathf.Max(opportunismWeight, 0.78f);
                    break;
            }

            if (country.emGuerra)
            {
                defaultNationMode = IA01NationMode.War;
                defaultPosture = IA01NationPosture.War;
                defaultStage = IA01NationStage.Emergency;
            }

            if (country.sancionado)
            {
                cautionWeight = Mathf.Max(cautionWeight, 0.72f);
                diplomacyWeight = Mathf.Max(0.15f, diplomacyWeight * 0.8f);
            }

            if (ResolveGovernmentStability(country) < 45f)
            {
                defaultStage = IA01NationStage.Recovering;
                defaultPosture = IA01NationPosture.Recovery;
            }

            if (country.petroleo > 1500)
            {
                selfSufficiencyWeight = Mathf.Max(selfSufficiencyWeight, 0.72f);
                industryWeight = Mathf.Max(industryWeight, 0.66f);
            }

            if (country.comida < 700)
            {
                agricultureWeight = Mathf.Max(agricultureWeight, 0.72f);
                cautionWeight = Mathf.Max(cautionWeight, 0.66f);
            }
        }

        public float ResolveCadence(IA01ExecutionMode executionMode, IA01NationStage stage, IA01NationMode nationMode)
        {
            float cadence = BaseCadenceSeconds;
            switch (executionMode)
            {
                case IA01ExecutionMode.ObserverDebug:
                    cadence *= 1.25f;
                    break;
                case IA01ExecutionMode.Manual:
                    cadence *= 1.75f;
                    break;
                case IA01ExecutionMode.Hybrid:
                    cadence *= 1.0f;
                    break;
                case IA01ExecutionMode.Full:
                    cadence *= 0.80f;
                    break;
            }

            switch (stage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    cadence *= 0.65f;
                    break;
                case IA01NationStage.Survival:
                case IA01NationStage.Stabilization:
                    cadence *= 0.80f;
                    break;
                case IA01NationStage.Industrialization:
                case IA01NationStage.Specialization:
                    cadence *= 0.95f;
                    break;
                case IA01NationStage.RegionalProjection:
                case IA01NationStage.GlobalPower:
                    cadence *= 1.10f;
                    break;
                case IA01NationStage.Recovering:
                case IA01NationStage.Emergency:
                    cadence *= 0.55f;
                    break;
                case IA01NationStage.FailedSafe:
                    cadence *= 2.0f;
                    break;
            }

            if (nationMode == IA01NationMode.War)
            {
                cadence *= 0.70f;
            }
            else if (nationMode == IA01NationMode.Peace)
            {
                cadence *= 1.15f;
            }

            return Mathf.Clamp(cadence, 0.05f, 5f);
        }

        public float ResolveSliceBudgetMs(IA01ExecutionMode executionMode, IA01NationStage stage, IA01NationMode nationMode)
        {
            float budget = MaxSliceMilliseconds;
            switch (executionMode)
            {
                case IA01ExecutionMode.ObserverDebug:
                    budget *= 0.55f;
                    break;
                case IA01ExecutionMode.Manual:
                    budget *= 0.25f;
                    break;
                case IA01ExecutionMode.Hybrid:
                    budget *= 0.75f;
                    break;
                case IA01ExecutionMode.Full:
                    budget *= 1.0f;
                    break;
            }

            if (stage == IA01NationStage.Initialization || stage == IA01NationStage.Emergency)
            {
                budget *= 0.90f;
            }
            else if (stage == IA01NationStage.GlobalPower)
            {
                budget *= 1.10f;
            }

            if (nationMode == IA01NationMode.War)
            {
                budget *= 1.10f;
            }

            return Mathf.Clamp(budget, MinSliceMilliseconds, MaxSliceMilliseconds);
        }

        public int ResolveOperationBudget(IA01ExecutionMode executionMode)
        {
            int budget = MaxOperationsPerSlice;
            switch (executionMode)
            {
                case IA01ExecutionMode.ObserverDebug:
                    budget = Mathf.Max(1, Mathf.RoundToInt(budget * 0.50f));
                    break;
                case IA01ExecutionMode.Manual:
                    budget = 1;
                    break;
                case IA01ExecutionMode.Hybrid:
                    budget = Mathf.Max(2, Mathf.RoundToInt(budget * 0.75f));
                    break;
                case IA01ExecutionMode.Full:
                    budget = Mathf.Max(3, budget);
                    break;
            }

            return Mathf.Clamp(budget, 1, 32);
        }

        public int ResolveEventBudget(IA01ExecutionMode executionMode)
        {
            int budget = MaxEventsPerSlice;
            if (executionMode == IA01ExecutionMode.ObserverDebug)
            {
                budget = Mathf.Max(1, Mathf.RoundToInt(budget * 0.75f));
            }
            else if (executionMode == IA01ExecutionMode.Manual)
            {
                budget = Mathf.Max(1, Mathf.RoundToInt(budget * 0.5f));
            }

            return Mathf.Clamp(budget, 1, 64);
        }

        public IA01NationStage ResolveOperationalStage(IA01NationStage currentStage, bool hasCapital, int structureCount, int treasury, int energy, int food, bool threatened, bool atWar, bool emergencyReserve)
        {
            if (!hasCapital)
            {
                return IA01NationStage.Initialization;
            }

            if (threatened || atWar)
            {
                return IA01NationStage.Emergency;
            }

            IA01NationStage targetStage;
            if (structureCount <= 1)
            {
                targetStage = IA01NationStage.Survival;
            }
            else if (structureCount <= 2)
            {
                targetStage = IA01NationStage.Stabilization;
            }
            else if (structureCount <= 3)
            {
                targetStage = IA01NationStage.UrbanDevelopment;
            }
            else if (structureCount <= 4)
            {
                targetStage = IA01NationStage.Industrialization;
            }
            else if (structureCount <= 6)
            {
                targetStage = IA01NationStage.Specialization;
            }
            else if (structureCount <= 8)
            {
                targetStage = IA01NationStage.RegionalProjection;
            }
            else
            {
                targetStage = IA01NationStage.GlobalPower;
            }

            if (emergencyReserve || treasury < 250 || energy < 250 || food < 250)
            {
                // A nation must be able to step back from an advanced stage when its
                // basic economy collapses; otherwise a saved GlobalPower state never recovers.
                return structureCount <= 2 ? IA01NationStage.Survival : IA01NationStage.Stabilization;
            }

            return targetStage;
        }

        public IA01NationPosture ResolveOperationalPosture(IA01NationStage stage, bool hasCapital, int structureCount, int treasury, int energy, int food, bool threatened, bool atWar, bool emergencyReserve)
        {
            if (!hasCapital)
            {
                return IA01NationPosture.Development;
            }

            if (threatened)
            {
                return atWar ? IA01NationPosture.War : IA01NationPosture.Defense;
            }

            if (atWar)
            {
                float warBias = aggressionWeight + militaryWeight + opportunismWeight;
                float defenseBias = defenseWeight + cautionWeight;
                return warBias >= defenseBias ? IA01NationPosture.LimitedAttack : IA01NationPosture.Defense;
            }

            if (emergencyReserve || treasury < 250 || energy < 250 || food < 250)
            {
                return IA01NationPosture.Recovery;
            }

            float offensive = aggressionWeight + militaryWeight + opportunismWeight;
            float defensive = defenseWeight + cautionWeight;
            float economic = economyWeight + commercialWeight + selfSufficiencyWeight;

            switch (stage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    return economic >= defensive ? IA01NationPosture.Development : IA01NationPosture.Peace;
                case IA01NationStage.Survival:
                    return defensive > economic ? IA01NationPosture.Alert : IA01NationPosture.Peace;
                case IA01NationStage.Stabilization:
                    return economic >= defensive ? IA01NationPosture.Peace : IA01NationPosture.Alert;
                case IA01NationStage.UrbanDevelopment:
                    return defensive >= offensive ? IA01NationPosture.Preparation : IA01NationPosture.Alert;
                case IA01NationStage.Industrialization:
                    return offensive > defensive ? IA01NationPosture.Preparation : IA01NationPosture.Alert;
                case IA01NationStage.Specialization:
                    return offensive >= defensive ? IA01NationPosture.LimitedAttack : IA01NationPosture.Preparation;
                case IA01NationStage.RegionalProjection:
                    return offensive >= defensive ? IA01NationPosture.LimitedAttack : IA01NationPosture.Preparation;
                case IA01NationStage.GlobalPower:
                    return offensive >= defensive + 0.25f ? IA01NationPosture.War : IA01NationPosture.LimitedAttack;
                case IA01NationStage.Recovering:
                    return IA01NationPosture.Recovery;
                case IA01NationStage.Emergency:
                    return defensive >= offensive ? IA01NationPosture.Defense : IA01NationPosture.War;
                default:
                    return IA01NationPosture.Development;
            }
        }

        public int ResolveIntentPriority(IA01IntentType intent, IA01NationStage stage, IA01NationPosture posture, int structureCount, bool threatened, bool atWar)
        {
            if (intent == IA01IntentType.EstablishCapital)
            {
                return 1000;
            }

            float priority = GetBasePriority(intent);
            if (priority <= 0f)
            {
                return 0;
            }

            priority += GetWeightBonus(intent);
            priority += GetStageBonus(intent, stage);
            priority += GetPostureBonus(intent, posture);
            priority += GetStructureBonus(intent, structureCount);

            if (threatened || atWar)
            {
                priority *= 0.75f;
            }

            if (stage == IA01NationStage.Emergency)
            {
                priority *= 0.85f;
            }

            return Mathf.Clamp(Mathf.RoundToInt(priority), 0, 999);
        }

        public static IA01NationProfile CreateRuntimeFromGovernment(DadosPaisGoverno country, int matchSeed, string difficultyCode = null)
        {
            IA01NationProfile profile = CreateInstance<IA01NationProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.profileKey = string.IsNullOrWhiteSpace(country != null ? country.nomePais : null) ? "runtime" : country.nomePais;
            if (country != null)
            {
                profile.ApplyGovernmentBias(country, difficultyCode);
            }

            profile.seedOffset = matchSeed;
            return profile;
        }

        private static float ResolveGovernmentStability(DadosPaisGoverno country)
        {
            if (country == null)
            {
                return 0f;
            }

            const string fieldName = "estabilidade";
            System.Reflection.FieldInfo field = typeof(DadosPaisGoverno).GetField(fieldName);
            if (field != null && field.FieldType == typeof(float))
            {
                object value = field.GetValue(country);
                if (value is float stability)
                {
                    return stability;
                }
            }

            return 0f;
        }

        private float GetBasePriority(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildEnergy:
                    return 520f;
                case IA01IntentType.BuildFoodProduction:
                    return 500f;
                case IA01IntentType.BuildResidentialCapacity:
                    return 480f;
                case IA01IntentType.BuildStorage:
                    return 460f;
                case IA01IntentType.BuildLogistics:
                    return 440f;
                case IA01IntentType.BuildIndustry:
                    return 540f;
                case IA01IntentType.BuildDefense:
                    return 510f;
                case IA01IntentType.DefendCapital:
                    return 1200f;
                case IA01IntentType.CampaignAgainstCapital:
                    return 950f;
                default:
                    return 0f;
            }
        }

        private float GetWeightBonus(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildEnergy:
                    return economyWeight * 120f + industryWeight * 80f + selfSufficiencyWeight * 40f;
                case IA01IntentType.BuildFoodProduction:
                    return agricultureWeight * 130f + economyWeight * 60f + selfSufficiencyWeight * 80f;
                case IA01IntentType.BuildResidentialCapacity:
                    return economyWeight * 70f + cautionWeight * 60f;
                case IA01IntentType.BuildStorage:
                    return industryWeight * 100f + economyWeight * 70f + cautionWeight * 20f;
                case IA01IntentType.BuildLogistics:
                    return expansionWeight * 100f + industryWeight * 60f + opportunismWeight * 40f + airWeight * 45f + navalWeight * 45f;
                case IA01IntentType.BuildIndustry:
                    return industryWeight * 190f + economyWeight * 70f + selfSufficiencyWeight * 50f + airWeight * 30f + navalWeight * 30f;
                case IA01IntentType.BuildDefense:
                    return defenseWeight * 190f + militaryWeight * 120f + cautionWeight * 50f;
                default:
                    return 0f;
            }
        }

        private float GetStageBonus(IA01IntentType intent, IA01NationStage stage)
        {
            switch (stage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    return intent == IA01IntentType.BuildEnergy ? 120f
                        : intent == IA01IntentType.BuildFoodProduction ? 90f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 50f
                        : intent == IA01IntentType.BuildStorage ? 15f
                        : intent == IA01IntentType.BuildLogistics ? 10f
                        : 0f;
                case IA01NationStage.Survival:
                    return intent == IA01IntentType.BuildEnergy ? 90f
                        : intent == IA01IntentType.BuildFoodProduction ? 110f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 80f
                        : intent == IA01IntentType.BuildStorage ? 25f
                        : intent == IA01IntentType.BuildLogistics ? 15f
                        : 0f;
                case IA01NationStage.Stabilization:
                    return intent == IA01IntentType.BuildEnergy ? 45f
                        : intent == IA01IntentType.BuildFoodProduction ? 55f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 60f
                        : intent == IA01IntentType.BuildStorage ? 85f
                        : intent == IA01IntentType.BuildLogistics ? 75f
                        : intent == IA01IntentType.BuildIndustry ? 40f
                        : intent == IA01IntentType.BuildDefense ? 55f
                        : 0f;
                case IA01NationStage.UrbanDevelopment:
                    return intent == IA01IntentType.BuildEnergy ? 25f
                        : intent == IA01IntentType.BuildFoodProduction ? 25f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 45f
                        : intent == IA01IntentType.BuildStorage ? 95f
                        : intent == IA01IntentType.BuildLogistics ? 105f
                        : intent == IA01IntentType.BuildIndustry ? 40f
                        : intent == IA01IntentType.BuildDefense ? 70f
                        : 0f;
                case IA01NationStage.Industrialization:
                    return intent == IA01IntentType.BuildEnergy ? 20f
                        : intent == IA01IntentType.BuildFoodProduction ? 10f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 20f
                        : intent == IA01IntentType.BuildStorage ? 115f
                        : intent == IA01IntentType.BuildLogistics ? 110f
                        : intent == IA01IntentType.BuildIndustry ? 190f
                        : intent == IA01IntentType.BuildDefense ? 100f
                        : 0f;
                case IA01NationStage.Specialization:
                    return intent == IA01IntentType.BuildEnergy ? 10f
                        : intent == IA01IntentType.BuildFoodProduction ? 5f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 10f
                        : intent == IA01IntentType.BuildStorage ? 125f
                        : intent == IA01IntentType.BuildLogistics ? 125f
                        : intent == IA01IntentType.BuildIndustry ? 160f
                        : intent == IA01IntentType.BuildDefense ? 155f
                        : 0f;
                case IA01NationStage.RegionalProjection:
                    return intent == IA01IntentType.BuildEnergy ? 5f
                        : intent == IA01IntentType.BuildFoodProduction ? 5f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 5f
                        : intent == IA01IntentType.BuildStorage ? 120f
                        : intent == IA01IntentType.BuildLogistics ? 135f
                        : intent == IA01IntentType.BuildIndustry ? 130f
                        : intent == IA01IntentType.BuildDefense ? 170f
                        : 0f;
                case IA01NationStage.GlobalPower:
                    return intent == IA01IntentType.BuildEnergy ? 5f
                        : intent == IA01IntentType.BuildFoodProduction ? 5f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 5f
                        : intent == IA01IntentType.BuildStorage ? 110f
                        : intent == IA01IntentType.BuildLogistics ? 140f
                        : intent == IA01IntentType.BuildIndustry ? 120f
                        : intent == IA01IntentType.BuildDefense ? 180f
                        : 0f;
                case IA01NationStage.Recovering:
                    return intent == IA01IntentType.BuildEnergy ? 120f
                        : intent == IA01IntentType.BuildFoodProduction ? 120f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 100f
                        : intent == IA01IntentType.BuildStorage ? 45f
                        : intent == IA01IntentType.BuildLogistics ? 30f
                        : 0f;
                case IA01NationStage.Emergency:
                    return intent == IA01IntentType.BuildEnergy ? 20f
                        : intent == IA01IntentType.BuildFoodProduction ? 20f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 20f
                        : intent == IA01IntentType.BuildStorage ? 10f
                        : intent == IA01IntentType.BuildLogistics ? 5f
                        : intent == IA01IntentType.BuildIndustry ? 20f
                        : intent == IA01IntentType.BuildDefense ? 100f
                        : 0f;
                default:
                    return 0f;
            }
        }

        private float GetPostureBonus(IA01IntentType intent, IA01NationPosture posture)
        {
            switch (posture)
            {
                case IA01NationPosture.Development:
                    return intent == IA01IntentType.BuildEnergy ? 20f
                        : intent == IA01IntentType.BuildFoodProduction ? 20f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 15f
                        : 0f;
                case IA01NationPosture.Peace:
                    return intent == IA01IntentType.BuildResidentialCapacity ? 20f
                        : intent == IA01IntentType.BuildStorage ? 10f
                        : intent == IA01IntentType.BuildIndustry ? 35f
                        : 0f;
                case IA01NationPosture.Alert:
                    return intent == IA01IntentType.BuildStorage ? 20f
                        : intent == IA01IntentType.BuildLogistics ? 25f
                        : intent == IA01IntentType.BuildIndustry ? 60f
                        : intent == IA01IntentType.BuildDefense ? 90f
                        : 0f;
                case IA01NationPosture.Preparation:
                    return intent == IA01IntentType.BuildStorage ? 25f
                        : intent == IA01IntentType.BuildLogistics ? 30f
                        : intent == IA01IntentType.BuildEnergy ? 10f
                        : intent == IA01IntentType.BuildIndustry ? 75f
                        : intent == IA01IntentType.BuildDefense ? 110f
                        : 0f;
                case IA01NationPosture.Defense:
                    return intent == IA01IntentType.BuildStorage ? 20f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 10f
                        : intent == IA01IntentType.BuildDefense ? 180f
                        : 0f;
                case IA01NationPosture.LimitedAttack:
                    return intent == IA01IntentType.BuildLogistics ? 30f
                        : intent == IA01IntentType.BuildStorage ? 20f
                        : intent == IA01IntentType.BuildIndustry ? 50f
                        : intent == IA01IntentType.BuildDefense ? 70f
                        : 0f;
                case IA01NationPosture.War:
                    return intent == IA01IntentType.BuildLogistics ? 35f
                        : intent == IA01IntentType.BuildStorage ? 25f
                        : intent == IA01IntentType.BuildIndustry ? 50f
                        : intent == IA01IntentType.BuildDefense ? 100f
                        : 0f;
                case IA01NationPosture.Retreat:
                case IA01NationPosture.Recovery:
                    return intent == IA01IntentType.BuildEnergy ? 20f
                        : intent == IA01IntentType.BuildFoodProduction ? 20f
                        : intent == IA01IntentType.BuildResidentialCapacity ? 20f
                        : 0f;
                default:
                    return 0f;
            }
        }

        private float GetStructureBonus(IA01IntentType intent, int structureCount)
        {
            if (structureCount <= 1)
            {
                return intent == IA01IntentType.BuildEnergy ? 70f
                    : intent == IA01IntentType.BuildFoodProduction ? 30f
                    : intent == IA01IntentType.BuildResidentialCapacity ? 15f
                    : intent == IA01IntentType.BuildStorage ? 5f
                    : 0f;
            }

            if (structureCount == 2)
            {
                return intent == IA01IntentType.BuildFoodProduction ? 70f
                    : intent == IA01IntentType.BuildResidentialCapacity ? 30f
                    : intent == IA01IntentType.BuildStorage ? 15f
                    : intent == IA01IntentType.BuildLogistics ? 5f
                    : intent == IA01IntentType.BuildEnergy ? 20f
                    : 0f;
            }

            if (structureCount == 3)
            {
                return intent == IA01IntentType.BuildResidentialCapacity ? 70f
                    : intent == IA01IntentType.BuildStorage ? 30f
                    : intent == IA01IntentType.BuildLogistics ? 15f
                    : intent == IA01IntentType.BuildEnergy ? 15f
                    : intent == IA01IntentType.BuildFoodProduction ? 10f
                    : 0f;
            }

            if (structureCount == 4)
            {
                return intent == IA01IntentType.BuildIndustry ? 20f
                    : intent == IA01IntentType.BuildStorage ? 70f
                    : intent == IA01IntentType.BuildLogistics ? 55f
                    : intent == IA01IntentType.BuildEnergy ? 15f
                    : intent == IA01IntentType.BuildFoodProduction ? 10f
                    : intent == IA01IntentType.BuildResidentialCapacity ? 20f
                    : 0f;
            }

            if (structureCount == 5)
            {
                return intent == IA01IntentType.BuildIndustry ? 35f
                    : intent == IA01IntentType.BuildLogistics ? 80f
                    : intent == IA01IntentType.BuildStorage ? 40f
                    : intent == IA01IntentType.BuildEnergy ? 10f
                    : intent == IA01IntentType.BuildFoodProduction ? 10f
                    : intent == IA01IntentType.BuildResidentialCapacity ? 15f
                    : 0f;
            }

            if (structureCount <= 8)
            {
                return intent == IA01IntentType.BuildDefense ? 80f
                    : intent == IA01IntentType.BuildIndustry ? 50f
                    : intent == IA01IntentType.BuildLogistics ? 60f
                    : intent == IA01IntentType.BuildStorage ? 35f
                    : 0f;
            }

            return intent == IA01IntentType.BuildDefense ? 100f
                : intent == IA01IntentType.BuildIndustry ? 70f
                : intent == IA01IntentType.BuildLogistics ? 80f
                : 0f;
        }
    }
}
