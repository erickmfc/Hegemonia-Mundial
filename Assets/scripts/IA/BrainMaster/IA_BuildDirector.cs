using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_BuildDirector : IIAUpdateModule
    {
        private enum ManualBuildCandidateStatus
        {
            None,
            Found,
            Blocked
        }

        private struct CoastalAnchorCacheEntry
        {
            public bool Resolved;
            public Vector3 Anchor;
            public float ValidUntil;
        }

        private struct NavalSearchBackoffEntry
        {
            public float ValidUntil;
            public string Reason;
        }

        private const float BootstrapPrefeituraTime = 5f;
        private const float BootstrapAeroportoTime = 10f;
        private const float BootstrapVehicleFactoryTime = 15f;
        private const float BootstrapSupportHangarTime = 20f;
        private const float BootstrapTentTime = 25f;
        private const float BootstrapAnalysisDuration = 5f;
        private const float BootstrapShipyardHoldDuration = 5f;
        private const float NavalEdgeSafetyMargin = 145f;
        private const float NavalLaunchSafetyMargin = 95f;
        private const float CoastalAnchorCacheCellSize = 96f;
        private const int MaxBootstrapAnchorRefinements = 4;
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private int _lastKnownStructureCount = -1;
        private float _lastProgressTime;
        private float _nextRecoveryAttemptTime;
        private float _nextCoastScanTime;
        private float _nextNavalAttemptTime;
        private float _nextNavalExpansionAttemptTime;
        private float _nextRareNavalExpansionWindowTime;
        private int _recoveryLevel;
        private int _bootstrapNavalAttemptCursor;
        private int _bootstrapNavalNoCoastFailures;
        private Vector3 _cachedCoastAnchor;
        private bool _cachedCoastAvailable;
        private readonly Dictionary<string, float> _missingItemCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _placementRetryCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _profilingCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _warningCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, CoastalAnchorCacheEntry> _coastalAnchorCache = new Dictionary<string, CoastalAnchorCacheEntry>();
        private readonly Dictionary<string, NavalSearchBackoffEntry> _navalSearchBackoffUntil = new Dictionary<string, NavalSearchBackoffEntry>();
        private readonly Dictionary<string, int> _placementFailureStreakByKey = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _navalFailureStreakByKey = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _navalFailureLastReasonByKey = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _navalAutoPlacementDisabledReasonByItem = new Dictionary<string, string>();
        private readonly List<Estaleiro> _registeredShipyardBuffer = new List<Estaleiro>();
        private readonly List<PierMarinha> _registeredPierBuffer = new List<PierMarinha>();
        private readonly Collider[] _legacySpaceHits = new Collider[128];
        private IA_ManualBuildPoint _pendingManualBuildPoint;
        private int _cachedApproxCombatUnitCount = -1;
        private int _cachedApproxCombatSourceCount = -1;
        private float _cachedApproxCombatUnitUntil;
        private bool _cachedTerritoryAnchorResolved;
        private Vector3 _cachedTerritoryLandAnchor;
        private Vector3 _cachedTerritoryCoastAnchor;
        private int _cachedTerritoryStructureCount = -1;
        private float _cachedTerritoryAnchorsUntil;
        private string _lastSlowSectionSummary = string.Empty;

        public IA_BuildDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_BuildDirector"; }
        }

        public float Interval
        {
            get { return 1.20f; }
        }

        public float BudgetMs
        {
            get { return 0.45f; }
        }

        public string LastProfilingSummary
        {
            get { return _lastSlowSectionSummary; }
        }

        public float LastNavalFailureTime { get; private set; }

        public float LastNavalRetryDelaySeconds { get; private set; }

        public bool CombatNavalBuildLocked { get; private set; }

        public string CombatNavalBuildLockReason { get; private set; }

        public bool NavalAutoPlacementDisabled { get; private set; }

        public string NavalAutoPlacementDisabledReason { get; private set; }

        public string LastNavalGeometryFailureReason { get; private set; }

        public int LastNavalGeometryFailureCount { get; private set; }

        private static long BeginTimingScope()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        private void EndTimingScope(string section, string detail, long startTimestamp, float thresholdMs)
        {
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (section == "TryFindDirectNavalCandidate")
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("naval_candidate_ms", elapsedMs);
            }
            else if (section == "TryResolveFriendlyTerritoryCoastalAnchor"
                     || section == "TryFindDirectCoastalAnchor")
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("coast_scan_ms", elapsedMs);
            }

            if (elapsedMs < thresholdMs)
            {
                return;
            }

            _lastSlowSectionSummary = section + "=" + elapsedMs.ToString("0.00") + "ms"
                + (string.IsNullOrEmpty(detail) ? string.Empty : " | " + detail);

            float now = Time.time;
            float cooldownSeconds = elapsedMs >= 500f
                ? 0.10f
                : (elapsedMs >= 100f ? 0.60f : (elapsedMs >= 20f ? 1.20f : 3.50f));
            float cooldownUntil;
            if (elapsedMs < 250f
                && _profilingCooldownUntil.TryGetValue(section, out cooldownUntil)
                && cooldownUntil > now)
            {
                return;
            }

            _profilingCooldownUntil[section] = now + cooldownSeconds;
            DiagnosticoDesempenhoJogo.RegistrarEvento(
                "IA_BuildDirector",
                section + " levou " + elapsedMs.ToString("0.00") + " ms"
                + (string.IsNullOrEmpty(detail) ? string.Empty : " | " + detail));
        }

        private float ResolveDecisionDelay()
        {
            if (ShouldRespectRuntimeLock() && DiagnosticoDesempenhoJogo.RuntimeSaturado())
            {
                return 3.60f;
            }

            if (ShouldRespectRuntimeLock() && DiagnosticoDesempenhoJogo.RuntimeSobPressao())
            {
                return 2.20f;
            }

            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 0.95f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 1.85f;
                case EstadoCargaIA.EmCombate:
                    return 1.35f;
                default:
                    return 0.95f;
            }
        }

        private bool ShouldLockHeavyNavalBuild(float now, out string reason)
        {
            reason = string.Empty;
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return false;
            }

            if (pressure.Estado == EstadoCargaIA.Saturado)
            {
                reason = "combate saturado";
                return true;
            }

            if (pressure.EnemyVisible && pressure.HasMixedNavalAirLoad())
            {
                reason = "combate naval e aereo ativo";
                return true;
            }

            if (pressure.IsCombatRecent(35f) && (pressure.ActiveMissiles >= 6 || pressure.ActiveProjectiles >= 28))
            {
                reason = "janela calma ainda nao atingida";
                return true;
            }

            return false;
        }

        private bool ShouldLockNonEssentialRuntimeBuild(out string reason)
        {
            reason = string.Empty;
            if (!ShouldRespectRuntimeLock() || !DiagnosticoDesempenhoJogo.RuntimeSobPressao())
            {
                return false;
            }

            reason = DiagnosticoDesempenhoJogo.ObterRazaoLockRuntime();
            if (string.IsNullOrEmpty(reason))
            {
                reason = DiagnosticoDesempenhoJogo.RuntimeSaturado()
                    ? "runtime saturado"
                    : "runtime sob pressao";
            }

            return true;
        }

        private float GetCombatLockCooldownSeconds()
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 30f;
            }

            return pressure.Estado == EstadoCargaIA.Saturado ? 45f : 30f;
        }

        private bool ShouldAllowAutomaticNavalExpansion(float now, int estaleiros, int piers)
        {
            if (estaleiros + piers <= 0)
            {
                return true;
            }

            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return now >= _nextRareNavalExpansionWindowTime;
            }

            if (pressure.Estado != EstadoCargaIA.Normal
                || pressure.EnemyVisible
                || pressure.IsCombatRecent(45f)
                || _context.CommandQueue.PendingCount > 4)
            {
                return false;
            }

            return now >= _nextRareNavalExpansionWindowTime;
        }

        private bool IsNavalAutoPlacementDisabledForItem(string itemKey, out string reason)
        {
            reason = string.Empty;
            string normalized = IA_Text.Normalize(itemKey);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (_navalAutoPlacementDisabledReasonByItem.TryGetValue(normalized, out reason))
            {
                NavalAutoPlacementDisabled = true;
                NavalAutoPlacementDisabledReason = reason;
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("naval_auto_disabled_reason", reason);
                return true;
            }

            NavalAutoPlacementDisabled = false;
            NavalAutoPlacementDisabledReason = string.Empty;
            return false;
        }

        private static bool IsHeavyAutomaticNavalItem(string itemKey)
        {
            string normalized = IA_Text.Normalize(itemKey);
            return normalized.Contains("estaleiro")
                   || normalized.Contains("pier")
                   || normalized.Contains("plataforma");
        }

        private bool CanUseDirectNavalFallback(string itemKey, float now)
        {
            if (HasManualBuildOverrideForItem(itemKey))
            {
                return false;
            }

            string disabledReason;
            if (IsNavalAutoPlacementDisabledForItem(itemKey, out disabledReason))
            {
                return false;
            }

            IA_BrainMaster brain = _context.Brain;
            if (brain != null
                && brain.IsBootstrapActive
                && brain.BootstrapStage == IA_BrainMaster.IA_BootstrapStage.BuildShipyard)
            {
                return true;
            }

            return now >= _nextRareNavalExpansionWindowTime
                   && _context.CommandQueue.PendingCount <= 4
                   && !ShouldLockHeavyNavalBuild(now, out disabledReason)
                   && (_context.CombatPressure == null || _context.CombatPressure.IsCombatRecent(45f) == false);
        }

        public void Tick(float now, float deltaTime)
        {
            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + ResolveDecisionDelay();
            if (_context.CommandQueue.PendingCount > 8)
            {
                return;
            }

            int cityHall;
            int hq;
            int barracks;
            int factories;
            int radars;
            int sentries;
            int ciws;
            int airports;
            int heliports;
            int estaleiros;
            int piers;
            int plataformas;
            int walls;
            int missiles;
            int warehouses;
            CollectStructureCounts(
                out cityHall,
                out hq,
                out barracks,
                out factories,
                out radars,
                out sentries,
                out ciws,
                out airports,
                out heliports,
                out estaleiros,
                out piers,
                out plataformas,
                out walls,
                out missiles,
                out warehouses);
            UpdateProgressTracker(now);

            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            long resolveLandAnchorStart = BeginTimingScope();
            Vector3 landAnchor = ResolveLandAnchor(baseCenter);
            EndTimingScope("ResolveLandAnchor", "base=" + baseCenter + " | land=" + landAnchor, resolveLandAnchorStart, 1.00f);

            long strategyInputsStart = BeginTimingScope();
            IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
            float localThreat = _context.ThreatAnalyzer.EvaluateThreat(landAnchor, IA_Domain.Land);
            int visibleEnemies = _context.WorldState.VisibleEnemies.Count;
            int developedStructures = _context.WorldState.OwnStructures.Count;
            int ownCombatCount = Mathf.Max(_context.WorldState.OwnCombatUnits.Count, CountApproxCombatUnits(now));
            EndTimingScope(
                "TickStrategyInputs",
                "threat=" + localThreat.ToString("0.0") + " | enemies=" + visibleEnemies + " | combat=" + ownCombatCount,
                strategyInputsStart,
                1.00f);
            bool structuresStableForTimedNaval = now - _lastProgressTime >= 6f;
            bool timedNavalOpening = (now >= 20f && ownCombatCount >= 15 && structuresStableForTimedNaval)
                                     || (now >= 35f && ownCombatCount >= 15);
            bool estaleiroManualOverride = estaleiros < 1 && HasManualBuildOverrideForItem("Estaleiro Naval");
            bool pierManualOverride = piers < 1 && HasManualBuildOverrideForItem("pier");
            bool plataformaManualOverride = plataformas < 1 && HasManualBuildOverrideForItem("PLataforma");
            string navalCombatLockReason;
            bool combatLocksHeavyNaval = ShouldLockHeavyNavalBuild(now, out navalCombatLockReason);
            bool runtimeLocksNonEssentialBuild = ShouldLockNonEssentialRuntimeBuild(out _);
            bool allowAutomaticNavalExpansion = ShouldAllowAutomaticNavalExpansion(now, estaleiros, piers);
            CombatNavalBuildLocked = combatLocksHeavyNaval;
            CombatNavalBuildLockReason = combatLocksHeavyNaval ? navalCombatLockReason : string.Empty;
            Vector3 navalAnchor = landAnchor;
            bool coastAvailable = false;
            bool needCoastScan = (estaleiros < 1 && !estaleiroManualOverride)
                                 || (piers < 1 && !pierManualOverride)
                                 || (plataformas < 1 && !plataformaManualOverride)
                                 || counter.ReinforceCoast
                                 || counter.NavalWeight > 0.20f;
            if (!allowAutomaticNavalExpansion && estaleiros + piers > 0 && !estaleiroManualOverride && !pierManualOverride && !plataformaManualOverride)
            {
                needCoastScan = false;
            }
            if (runtimeLocksNonEssentialBuild)
            {
                needCoastScan = false;
                _nextCoastScanTime = Mathf.Max(_nextCoastScanTime, now + 22f);
            }
            if (needCoastScan && !combatLocksHeavyNaval)
            {
                if (now >= _nextCoastScanTime)
                {
                    long coastScanStart = BeginTimingScope();
                    _cachedCoastAvailable = TryResolveFriendlyTerritoryCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                    if (!_cachedCoastAvailable)
                    {
                        _cachedCoastAvailable = TryFindCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                    }
                    if (!_cachedCoastAvailable && _context.Brain != null)
                    {
                        Vector3 brainAnchor = _context.Brain.transform.position;
                        if (brainAnchor != Vector3.zero)
                        {
                            _cachedCoastAvailable = TryResolveFriendlyTerritoryCoastalAnchor(brainAnchor, out _cachedCoastAnchor);
                            if (!_cachedCoastAvailable)
                            {
                                _cachedCoastAvailable = TryFindCoastalAnchor(brainAnchor, out _cachedCoastAnchor);
                            }
                        }
                    }

                    EndTimingScope(
                        "TickCoastScan",
                        "found=" + _cachedCoastAvailable + " | anchor=" + _cachedCoastAnchor,
                        coastScanStart,
                        2.50f);
                    _nextCoastScanTime = now + (_cachedCoastAvailable ? 12f : 35f);
                }

                coastAvailable = _cachedCoastAvailable;
                navalAnchor = coastAvailable ? _cachedCoastAnchor : landAnchor;
            }
            else if (needCoastScan)
            {
                _nextCoastScanTime = Mathf.Max(_nextCoastScanTime, now + GetCombatLockCooldownSeconds());
                coastAvailable = _cachedCoastAvailable;
                navalAnchor = coastAvailable ? _cachedCoastAnchor : landAnchor;
            }

            long bootstrapStart = BeginTimingScope();
            bool bootstrapHandled = HandleScriptedBootstrap(
                now,
                baseCenter,
                landAnchor,
                coastAvailable,
                navalAnchor,
                cityHall,
                airports,
                factories,
                heliports,
                warehouses,
                barracks,
                estaleiros,
                piers);
            EndTimingScope(
                "HandleScriptedBootstrap",
                "handled=" + bootstrapHandled + (_context.Brain != null ? " | stage=" + _context.Brain.BootstrapStage : string.Empty),
                bootstrapStart,
                1.50f);
            if (bootstrapHandled)
            {
                return;
            }

            if (cityHall == 0)
            {
                if (QueueBuildAtLand("Prefeitura", IA_ZoneType.Core, baseCenter, 0f, 55f, 1000, 1.0f))
                {
                    return;
                }

                TryEmergencyBuild(baseCenter, IA_ZoneType.Core, IA_TerrainType.Land, 0f, 220f, "Prefeitura", "governo", "capital");

                return;
            }

            bool hasCriticalGap = barracks == 0
                                  || factories == 0
                                  || warehouses == 0;
            if (hasCriticalGap
                && ShouldUseRecovery(now, cityHall))
            {
                TryEmergencyRecoveryBuild(now, landAnchor, localThreat, hq, barracks, factories, radars, sentries, warehouses, airports, heliports, estaleiros, piers, coastAvailable, navalAnchor);
                return;
            }

            if (barracks == 0 && QueueBuildAtLand("quartel", IA_ZoneType.Military, landAnchor, 25f, 90f, 95, 8f))
            {
                return;
            }

            if (factories == 0 && QueueBuildAtLand("fabrica", IA_ZoneType.Military, landAnchor, 35f, 120f, 92, 8f))
            {
                return;
            }

            if (hq == 0 && CanTryBuildItem("Quartel General", now) && QueueBuildAtLand("Quartel General", IA_ZoneType.Core, landAnchor, 45f, 130f, 90, 12f))
            {
                return;
            }

            bool earlyNavalOpening = factories > 0
                                     && (barracks > 0 || developedStructures >= 3)
                                     && (coastAvailable || now >= 12f);
            bool shouldOpenNaval = (estaleiros + piers <= 0 || allowAutomaticNavalExpansion || estaleiroManualOverride || pierManualOverride)
                                   && (earlyNavalOpening
                                   || timedNavalOpening
                                   || (factories > 0
                                   && (developedStructures >= 4
                                       || counter.ReinforceCoast
                                       || counter.NavalWeight > 0.10f)));
            if (shouldOpenNaval && now >= _nextNavalAttemptTime)
            {
                if (combatLocksHeavyNaval)
                {
                    _nextNavalAttemptTime = now + GetCombatLockCooldownSeconds();
                }
                else
                {
                    if (!coastAvailable && estaleiros < 1 && piers < 1 && !estaleiroManualOverride && !pierManualOverride)
                    {
                        _nextNavalAttemptTime = now + 16f;
                    }
                    else
                    {
                        Vector3 navalSearchAnchor = coastAvailable ? navalAnchor : landAnchor;
                        float navalMinRadius = coastAvailable
                            ? (earlyNavalOpening ? 8f : 12f)
                            : (timedNavalOpening ? 45f : 75f);
                        float navalMaxRadius = coastAvailable
                            ? (earlyNavalOpening ? 320f : (timedNavalOpening ? 360f : 280f))
                            : (timedNavalOpening ? 1600f : 1200f);
                        int estaleiroPriority = earlyNavalOpening ? 97 : (timedNavalOpening ? 98 : 91);
                        int pierPriority = timedNavalOpening ? 96 : 88;
                        bool shouldBuildPierNow = timedNavalOpening
                                                  || airports > 0
                                                  || (estaleiros > 0 && now >= 18f)
                                                  || counter.ReinforceCoast
                                                  || counter.NavalWeight > 0.18f;
                        if (estaleiros < 1 && !IsNavalAutoPlacementDisabledForItem("Estaleiro Naval", out navalCombatLockReason))
                        {
                            bool queuedEstaleiro = QueueBuildAtWater("Estaleiro Naval", IA_ZoneType.Naval, navalSearchAnchor, navalMinRadius, navalMaxRadius, estaleiroPriority, earlyNavalOpening ? 8f : 14f);
                            _nextNavalAttemptTime = now + (queuedEstaleiro ? 8f : (coastAvailable ? 12f : 18f));
                            if (allowAutomaticNavalExpansion && estaleiros + piers > 0)
                            {
                                _nextRareNavalExpansionWindowTime = now + 90f;
                            }
                            return;
                        }

                        if (piers < 1
                            && shouldBuildPierNow
                            && (coastAvailable || pierManualOverride || estaleiros > 0)
                            && !IsNavalAutoPlacementDisabledForItem("pier", out navalCombatLockReason))
                        {
                            bool queuedPier = QueueBuildAtWater("pier", IA_ZoneType.Naval, navalSearchAnchor, Mathf.Max(20f, navalMinRadius - 24f), navalMaxRadius, pierPriority, 16f);
                            _nextNavalAttemptTime = now + (queuedPier ? 10f : 16f);
                            if (allowAutomaticNavalExpansion && estaleiros + piers > 0)
                            {
                                _nextRareNavalExpansionWindowTime = now + 90f;
                            }
                            return;
                        }
                    }
                }
            }

            if (airports < 1 && factories > 0 && QueueBuildAtLand("aeroporto", IA_ZoneType.Air, landAnchor, 320f, 980f, 94, 18f))
            {
                return;
            }

            if (heliports < 1 && airports > 0 && counter.AirWeight > 0.55f && QueueBuildAtLand("heliporto", IA_ZoneType.Air, landAnchor, 80f, 145f, 84, 16f))
            {
                return;
            }

            if (radars < 1 && QueueBuildAtLand("radar", IA_ZoneType.Defense, landAnchor, 55f, 120f, 86, 12f))
            {
                return;
            }

            if (sentries < 1 && (counter.AntiRush || visibleEnemies > 0 || localThreat > 65f) && QueueBuildAtChoke("torreta", landAnchor, 84, 16f))
            {
                return;
            }

            bool needsCiws = ciws < 1
                             && (counter.AirWeight > 0.45f
                                 || (visibleEnemies > 0 && localThreat > 95f)
                                 || (_context.WorldState.OwnStructures.Count >= 8 && missiles > 0));
            if (needsCiws && QueueBuildAtLand("CIWS", IA_ZoneType.Defense, landAnchor, 45f, 110f, 88, 12f))
            {
                return;
            }

            if (warehouses < 1 && QueueBuildAtLand("armazem", IA_ZoneType.Economy, landAnchor, 35f, 150f, 74, 12f))
            {
                return;
            }

            bool coastNeeded = shouldOpenNaval
                               || counter.ReinforceCoast
                               || counter.NavalWeight > 0.20f
                               || (developedStructures >= 6 && factories > 0);
            if (!combatLocksHeavyNaval
                && allowAutomaticNavalExpansion
                && coastNeeded
                && (coastAvailable || estaleiros > 0 || piers > 0 || plataformaManualOverride)
                && !IsNavalAutoPlacementDisabledForItem("PLataforma", out navalCombatLockReason))
            {
                Vector3 coastalBuildAnchor = coastAvailable ? navalAnchor : landAnchor;
                float platformMinRadius = 300f;
                float platformMaxRadius = coastAvailable ? 900f : 1200f;
                if (plataformas < 1 && QueueBuildAtWater("PLataforma", IA_ZoneType.Naval, coastalBuildAnchor, platformMinRadius, platformMaxRadius, 78, 18f))
                {
                    if (estaleiros + piers > 0)
                    {
                        _nextRareNavalExpansionWindowTime = now + 90f;
                    }
                    return;
                }
            }

            bool shouldFortify = visibleEnemies > 0 && developedStructures >= 6 && (counter.AntiRush || localThreat > 75f);
            if (walls < 4 && shouldFortify && QueueBuildAtChoke("Muro de Concreto", landAnchor, 70, 12f))
            {
                return;
            }

            if (missiles < 1
                && visibleEnemies > 0
                && developedStructures >= 8
                && localThreat > 120f
                && _context.Brain.Credits > 12000
                && QueueBuildAtLand("Lancador de Misseis", IA_ZoneType.Defense, landAnchor, 90f, 200f, 66, 40f))
            {
                return;
            }
        }

        private void CollectStructureCounts(
            out int cityHall,
            out int hq,
            out int barracks,
            out int factories,
            out int radars,
            out int sentries,
            out int ciws,
            out int airports,
            out int heliports,
            out int estaleiros,
            out int piers,
            out int plataformas,
            out int walls,
            out int missiles,
            out int warehouses)
        {
            long profileStart = BeginTimingScope();

            cityHall = 0;
            hq = 0;
            barracks = 0;
            factories = 0;
            radars = 0;
            sentries = 0;
            ciws = 0;
            airports = 0;
            heliports = 0;
            estaleiros = 0;
            piers = 0;
            plataformas = 0;
            walls = 0;
            missiles = 0;
            warehouses = 0;

            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(structure.name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name.Contains("prefeitura"))
                {
                    cityHall++;
                }

                if (name.Contains("quartel general") || name.Contains("quartel_general"))
                {
                    hq++;
                }

                if (name.Contains("tenda") || name.Contains("barraca"))
                {
                    barracks++;
                }

                if (name.Contains("construtor de veiculos") || name.Contains("construtor") || name.Contains("fabrica"))
                {
                    factories++;
                }

                if (name.Contains("radar"))
                {
                    radars++;
                }

                if (name.Contains("torre") || name.Contains("sentinela") || name.Contains("metralh") || name.Contains("torreta"))
                {
                    sentries++;
                }

                if (name.Contains("ciws") || name.Contains("phalanx") || name.Contains("antia"))
                {
                    ciws++;
                }

                if (name.Contains("aeroporto") || name.Contains("base aerea") || name.Contains("airport") || name.Contains("pista"))
                {
                    airports++;
                }

                if (name.Contains("heliporto"))
                {
                    heliports++;
                }

                if (name.Contains("estaleiro"))
                {
                    estaleiros++;
                }

                if (name.Contains("pier"))
                {
                    piers++;
                }

                if (name.Contains("plataforma"))
                {
                    plataformas++;
                }

                if (name.Contains("muro") || name.Contains("wall"))
                {
                    walls++;
                }

                if (name.Contains("lancador") || name.Contains("missil") || name.Contains("silo"))
                {
                    missiles++;
                }

                if (name.Contains("armazem") || name.Contains("galpao"))
                {
                    warehouses++;
                }
            }

            // O registro vivo de estruturas fica disponível antes do próximo snapshot do WorldState.
            // Isso evita a IA "não enxergar" o estaleiro/pier recém-criado e travar a fase naval.
            estaleiros = Mathf.Max(estaleiros, CountRegisteredShipyards());
            piers = Mathf.Max(piers, CountRegisteredPiers());

            EndTimingScope("CollectStructureCounts", "structures=" + _context.WorldState.OwnStructures.Count, profileStart, 0.75f);
        }

        private int CountRegisteredShipyards()
        {
            RegistroEntidadesJogo.FillEstaleiros(_registeredShipyardBuffer);
            int count = 0;
            for (int i = 0; i < _registeredShipyardBuffer.Count; i++)
            {
                Estaleiro estaleiro = _registeredShipyardBuffer[i];
                if (estaleiro != null && _context.Backend.BelongsToTeam(estaleiro))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountRegisteredPiers()
        {
            RegistroEntidadesJogo.FillPiers(_registeredPierBuffer);
            int count = 0;
            for (int i = 0; i < _registeredPierBuffer.Count; i++)
            {
                PierMarinha pier = _registeredPierBuffer[i];
                if (pier != null && _context.Backend.BelongsToTeam(pier))
                {
                    count++;
                }
            }

            return count;
        }

        private bool QueueBuildAtLand(string itemKey, IA_ZoneType zone, Vector3 anchor, float minRadius, float maxRadius, int priority, float cooldown)
        {
            return QueueBuildInternal(itemKey, zone, anchor, IA_TerrainType.Land, minRadius, maxRadius, priority, cooldown);
        }

        private bool QueueBuildAtWater(string itemKey, IA_ZoneType zone, Vector3 anchor, float minRadius, float maxRadius, int priority, float cooldown)
        {
            return QueueBuildInternal(itemKey, zone, anchor, IA_TerrainType.Water, minRadius, maxRadius, priority, cooldown);
        }

        private bool QueueBuildAtChoke(string itemKey, Vector3 anchor, int priority, float cooldown)
        {
            float now = Time.time;
            bool hasManualOverride = HasManualBuildOverrideForItem(itemKey);
            if (!CanTryBuildItem(itemKey, now) || (!hasManualOverride && !CanRetryPlacementSearch(itemKey, IA_TerrainType.Choke, now)))
            {
                return false;
            }

            Vector3 candidate;
            if (!TryFindValidatedCandidate(itemKey, IA_ZoneType.Defense, anchor, IA_TerrainType.Choke, 55f, 190f, out candidate))
            {
                MarkPlacementSearchFailure(itemKey, IA_TerrainType.Choke, now);
                return false;
            }

            ClearPlacementSearchFailure(itemKey, IA_TerrainType.Choke);
            IA_ManualBuildPoint manualPoint = ConsumePendingManualBuildPoint();
            return QueueBuild(itemKey, candidate, IA_ZoneType.Defense, priority, cooldown, manualPoint);
        }

        private Vector3 ResolveLandAnchor(Vector3 fallback)
        {
            Vector3 brainPos = _context.Brain != null ? _context.Brain.transform.position : fallback;
            Vector3 territoryLandAnchor;
            Vector3 territoryCoastAnchor;
            if (TryResolveFriendlyTerritorySurfaceAnchors(out territoryLandAnchor, out territoryCoastAnchor))
            {
                return EnsureDryLandAnchor(territoryLandAnchor != Vector3.zero ? territoryLandAnchor : territoryCoastAnchor);
            }

            Vector3 cityHallPos;
            if (TryFindBestStructurePosition(out cityHallPos, brainPos, "prefeitura", "governo", "capital"))
            {
                return EnsureDryLandAnchor(cityHallPos);
            }

            Vector3 corePos;
            if (TryFindBestStructurePosition(out corePos, brainPos, "quartel general", "quartel_general", "tenda", "barraca", "construtor de veiculos", "fabrica", "armazem"))
            {
                return EnsureDryLandAnchor(corePos);
            }

            return EnsureDryLandAnchor(fallback != Vector3.zero ? fallback : brainPos);
        }

        private bool TryFindBestStructurePosition(out Vector3 position, Vector3 reference, params string[] hints)
        {
            position = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(structure.name);
                bool match = false;
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && normalized.Contains(hint))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                float dist = (Flatten(structure.transform.position) - Flatten(reference)).sqrMagnitude;
                if (!found || dist < best)
                {
                    best = dist;
                    position = structure.transform.position;
                    found = true;
                }
            }

            return found;
        }

        private bool QueueBuildInternal(
            string itemKey,
            IA_ZoneType zone,
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown)
        {
            float now = Time.time;
            bool hasManualOverride = HasManualBuildOverrideForItem(itemKey);
            if (!CanTryBuildItem(itemKey, now) || (!hasManualOverride && !CanRetryPlacementSearch(itemKey, desiredTerrain, now)))
            {
                return false;
            }

            Vector3 candidate;
            if (!TryFindValidatedCandidate(itemKey, zone, anchor, desiredTerrain, minRadius, maxRadius, out candidate))
            {
                MarkPlacementSearchFailure(itemKey, desiredTerrain, now);
                return false;
            }

            ClearPlacementSearchFailure(itemKey, desiredTerrain);
            if (desiredTerrain == IA_TerrainType.Water)
            {
                ClearNavalSearchBackoff(itemKey, anchor);
            }

            IA_ManualBuildPoint manualPoint = ConsumePendingManualBuildPoint();
            return QueueBuild(itemKey, candidate, zone, priority, cooldown, manualPoint);
        }

        private IA_ManualBuildPoint ConsumePendingManualBuildPoint()
        {
            IA_ManualBuildPoint point = _pendingManualBuildPoint;
            _pendingManualBuildPoint = null;
            return point;
        }

        private void ClearPendingManualBuildPoint()
        {
            _pendingManualBuildPoint = null;
        }

        private bool HasManualBuildOverrideForItem(string itemKey)
        {
            IA_BrainMaster brain = _context.Brain;
            if (brain == null || !brain.UseManualBuildPoints)
            {
                return false;
            }

            IA_ManualBuildPoint[] manualPoints = brain.GetComponentsInChildren<IA_ManualBuildPoint>(true);
            for (int i = 0; i < manualPoints.Length; i++)
            {
                IA_ManualBuildPoint point = manualPoints[i];
                if (point == null || !point.TargetsItem(brain, itemKey))
                {
                    continue;
                }

                if (!point.AllowInactiveObject && (!point.gameObject.activeInHierarchy || !point.enabled))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private ManualBuildCandidateStatus TryResolveManualBuildCandidate(
            string itemKey,
            Vector3 reference,
            out Vector3 candidate,
            out IA_ManualBuildPoint manualPoint,
            out string reason)
        {
            candidate = reference;
            manualPoint = null;
            reason = string.Empty;

            IA_BrainMaster brain = _context.Brain;
            if (brain == null || !brain.UseManualBuildPoints)
            {
                return ManualBuildCandidateStatus.None;
            }

            IA_ManualBuildPoint[] manualPoints = brain.GetComponentsInChildren<IA_ManualBuildPoint>(true);
            if (manualPoints == null || manualPoints.Length == 0)
            {
                return ManualBuildCandidateStatus.None;
            }

            Vector3 searchReference = reference;
            if (searchReference == Vector3.zero)
            {
                searchReference = brain.transform.position;
            }

            Vector3 flatReference = Flatten(searchReference);
            float bestDistance = float.MaxValue;
            bool hasManualOverride = false;
            for (int i = 0; i < manualPoints.Length; i++)
            {
                IA_ManualBuildPoint point = manualPoints[i];
                if (point == null || !point.TargetsItem(brain, itemKey))
                {
                    continue;
                }

                if (!point.AllowInactiveObject && (!point.gameObject.activeInHierarchy || !point.enabled))
                {
                    continue;
                }

                hasManualOverride = true;
                if (!point.IsCurrentlyAvailable(brain, _context.WorldState))
                {
                    continue;
                }

                float distance = Vector3.Distance(flatReference, Flatten(point.transform.position));
                if (manualPoint != null && distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                manualPoint = point;
                candidate = point.transform.position;
            }

            if (manualPoint != null)
            {
                return ManualBuildCandidateStatus.Found;
            }

            if (hasManualOverride)
            {
                // Se encontrou que devia usar manual, mas não estava livre (por colisão com torre, etc.), 
                // é melhor não travar a IA de vez (Blocked), mas sim tentar de novo (fallback automático) para se salvar.
                // Mas repassa o "reason" em caso de log
                reason = "ponto manual configurado, mas área estava bloqueada";
                return ManualBuildCandidateStatus.None;
            }

            return ManualBuildCandidateStatus.None;
        }

        private bool TryFindValidatedCandidate(
            string itemKey,
            IA_ZoneType zone,
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            out Vector3 candidate)
        {
            string ignoredReason;
            return TryFindValidatedCandidate(itemKey, zone, anchor, desiredTerrain, minRadius, maxRadius, out candidate, out ignoredReason);
        }

        private bool TryFindValidatedCandidate(
            string itemKey,
            IA_ZoneType zone,
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            out Vector3 candidate,
            out string failureReason)
        {
            candidate = anchor;
            failureReason = "nenhum ponto valido";
            ClearPendingManualBuildPoint();

            if (anchor == Vector3.zero && _context.Brain != null)
            {
                anchor = _context.Brain.transform.position;
            }

            IA_ManualBuildPoint manualPoint;
            string manualReason;
            ManualBuildCandidateStatus manualStatus = TryResolveManualBuildCandidate(itemKey, anchor, out candidate, out manualPoint, out manualReason);
            if (manualStatus == ManualBuildCandidateStatus.Found)
            {
                bool trackNavalDiagnostics = ShouldTrackNavalDiagnostic(itemKey, desiredTerrain);
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("ponto manual | item=" + itemKey + " | marcador=" + manualPoint.GetDisplayLabel());
                    NavalDiagnosticPoint(candidate, "manual: " + manualPoint.GetDisplayLabel(), new Color(1f, 0.82f, 0.15f, 1f), 3.6f, false);
                }

                _pendingManualBuildPoint = manualPoint;
                failureReason = string.Empty;
                return true;
            }
            else if (manualStatus == ManualBuildCandidateStatus.Blocked)
            {
                bool trackNavalDiagnostics = ShouldTrackNavalDiagnostic(itemKey, desiredTerrain);
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("ponto manual bloqueou fallback automatico | item=" + itemKey + " | motivo=" + manualReason);
                }

                failureReason = manualReason;
                return false;
            }

            if (desiredTerrain == IA_TerrainType.Water)
            {
                string navalBackoffReason;
                if (TryGetNavalSearchBackoff(itemKey, anchor, Time.time, out navalBackoffReason))
                {
                    failureReason = navalBackoffReason;
                    return false;
                }
            }

            string reason;
            Vector3 seededCandidate = _context.MapAnalyzer.FindPointInTerrain(anchor, desiredTerrain, minRadius, maxRadius, 10);
            if (seededCandidate != anchor
                && TryFindValidatedCandidateInternal(itemKey, zone, seededCandidate, desiredTerrain, 0f, 48f, 2, 8, out candidate, out reason))
            {
                failureReason = string.Empty;
                return true;
            }

            if (TryFindValidatedCandidateInternal(itemKey, zone, anchor, desiredTerrain, minRadius, maxRadius, 3, 10, out candidate, out reason))
            {
                failureReason = string.Empty;
                return true;
            }

            if (desiredTerrain == IA_TerrainType.Water && CanUseDirectNavalFallback(itemKey, Time.time))
            {
                float directMax = Mathf.Max(maxRadius + 240f, minRadius + 120f);
                if (TryFindDirectNavalCandidate(itemKey, zone, anchor, minRadius, directMax, out candidate, out reason))
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            float expandedMin = Mathf.Max(0f, minRadius + 12f);
            float expandedMax = Mathf.Max(maxRadius + 55f, expandedMin + 24f);
            if (TryFindValidatedCandidateInternal(itemKey, zone, anchor, desiredTerrain, expandedMin, expandedMax, 4, 12, out candidate, out reason))
            {
                failureReason = string.Empty;
                return true;
            }

            if (desiredTerrain == IA_TerrainType.Water)
            {
                float waterMin = Mathf.Max(0f, minRadius - 24f);
                float waterMax = Mathf.Max(expandedMax + 780f, maxRadius + 480f);
                if (TryFindValidatedCandidateInternal(itemKey, zone, anchor, IA_TerrainType.Water, waterMin, waterMax, 4, 12, out candidate, out reason))
                {
                    failureReason = string.Empty;
                    return true;
                }

                if (CanUseDirectNavalFallback(itemKey, Time.time)
                    && TryFindDirectNavalCandidate(itemKey, zone, anchor, waterMin, waterMax, out candidate, out reason))
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            if (desiredTerrain == IA_TerrainType.Choke)
            {
                bool foundChokeFallback = TryFindValidatedCandidateInternal(itemKey, zone, anchor, IA_TerrainType.Land, minRadius, expandedMax, 3, 12, out candidate, out reason);
                if (!foundChokeFallback)
                {
                    LogVerboseWarning("candidate:" + IA_Text.Normalize(itemKey) + ":choke:" + reason, "[IA_BuildDirector] Sem ponto valido para " + itemKey + " | motivo=" + reason, 18f);
                }

                failureReason = reason;
                return foundChokeFallback;
            }

            if (desiredTerrain == IA_TerrainType.Land
                && ShouldUseDeepLandFallback(itemKey)
                && TryFindLegacyCandidate(itemKey, anchor, IA_TerrainType.Land, minRadius, expandedMax, out candidate, out reason))
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = reason;
            if (desiredTerrain == IA_TerrainType.Water)
            {
                MarkNavalSearchBackoff(itemKey, anchor, reason, Time.time);
            }

            LogVerboseWarning("candidate:" + IA_Text.Normalize(itemKey) + ":" + desiredTerrain + ":" + reason, "[IA_BuildDirector] Sem ponto valido para " + itemKey + " | motivo=" + reason, desiredTerrain == IA_TerrainType.Water ? 24f : 18f);

            return false;
        }

        private bool HandleScriptedBootstrap(
            float now,
            Vector3 baseCenter,
            Vector3 landAnchor,
            bool coastAvailable,
            Vector3 navalAnchor,
            int cityHall,
            int airports,
            int factories,
            int heliports,
            int warehouses,
            int barracks,
            int estaleiros,
            int piers)
        {
            IA_BrainMaster brain = _context.Brain;
            if (brain == null || !brain.IsBootstrapActive)
            {
                return false;
            }

            float elapsed = brain.GetBootstrapElapsed(now);
            switch (brain.BootstrapStage)
            {
                case IA_BrainMaster.IA_BootstrapStage.BuildPrefeitura:
                    if (cityHall > 0)
                    {
                        return AdvanceBootstrapAfterTime(
                            elapsed,
                            BootstrapAeroportoTime,
                            IA_BrainMaster.IA_BootstrapStage.BuildAeroporto,
                            "prefeitura pronta; aguardando t=10s para aeroporto",
                            "abrindo fase do aeroporto");
                    }

                    if (elapsed < BootstrapPrefeituraTime)
                    {
                        brain.SetBootstrapStatus("aguardando t=5s para iniciar prefeitura");
                        return true;
                    }

                    return TryBootstrapMandatoryLandBuild(
                        "prefeitura",
                        landAnchor != Vector3.zero ? landAnchor : baseCenter,
                        IA_ZoneType.Core,
                        0f,
                        65f,
                        1000,
                        1.5f,
                        "Prefeitura",
                        "governo",
                        "capital");

                case IA_BrainMaster.IA_BootstrapStage.BuildAeroporto:
                    if (airports > 0)
                    {
                        return AdvanceBootstrapAfterTime(
                            elapsed,
                            BootstrapVehicleFactoryTime,
                            IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory,
                            "aeroporto pronto; aguardando t=15s para construtor de veiculos",
                            "abrindo fase do construtor de veiculos");
                    }

                    if (elapsed < BootstrapAeroportoTime)
                    {
                        brain.SetBootstrapStatus("aguardando t=10s para iniciar aeroporto");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(now) >= 30f)
                    {
                        brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory,
                            "aeroporto adiado; seguindo bootstrap e retomando depois");
                        return true;
                    }

                    return TryBootstrapMandatoryLandBuild(
                        "aeroporto",
                        landAnchor,
                        IA_ZoneType.Air,
                        40f,
                        1600f,
                        980,
                        4f,
                        "aeroporto",
                        "base aerea",
                        "airport",
                        "pista");

                case IA_BrainMaster.IA_BootstrapStage.BuildVehicleFactory:
                    if (factories > 0)
                    {
                        return AdvanceBootstrapAfterTime(
                            elapsed,
                            BootstrapSupportHangarTime,
                            IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar,
                            "construtor de veiculos pronto; aguardando t=20s para hangar de apoio",
                            "abrindo fase do hangar de apoio");
                    }

                    if (elapsed < BootstrapVehicleFactoryTime)
                    {
                        brain.SetBootstrapStatus("aguardando t=15s para iniciar construtor de veiculos");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(now) >= 16f)
                    {
                        brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar,
                            "construtor de veiculos adiado; seguindo bootstrap e retomando depois");
                        return true;
                    }

                    return TryBootstrapMandatoryLandBuild(
                        "construtor de veiculos",
                        landAnchor,
                        IA_ZoneType.Military,
                        40f,
                        140f,
                        970,
                        4f,
                        "Construtor de Veiculos",
                        "construtor de veiculos",
                        "construtor",
                        "fabrica");

                case IA_BrainMaster.IA_BootstrapStage.BuildSupportHangar:
                    if (heliports > 0 || warehouses > 0)
                    {
                        return AdvanceBootstrapAfterTime(
                            elapsed,
                            BootstrapTentTime,
                            IA_BrainMaster.IA_BootstrapStage.BuildTent,
                            "hangar de apoio pronto; aguardando t=25s para tenda",
                            "abrindo fase da tenda");
                    }

                    if (elapsed < BootstrapSupportHangarTime)
                    {
                        brain.SetBootstrapStatus("aguardando t=20s para iniciar hangar de apoio");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(now) >= 16f)
                    {
                        brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.BuildTent,
                            "hangar de apoio adiado; seguindo bootstrap e retomando depois");
                        return true;
                    }

                    return TryBootstrapSupportHangarBuild(landAnchor);

                case IA_BrainMaster.IA_BootstrapStage.BuildTent:
                    if (barracks > 0)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.AnalyzeTerrain, "estruturas iniciais prontas; analisando terreno");
                        return true;
                    }

                    if (elapsed < BootstrapTentTime)
                    {
                        brain.SetBootstrapStatus("aguardando t=25s para iniciar tenda");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(now) >= 16f)
                    {
                        brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.AnalyzeTerrain,
                            "tenda adiada; seguindo bootstrap e retomando depois");
                        return true;
                    }

                    return TryBootstrapMandatoryLandBuild(
                        "tenda",
                        landAnchor,
                        IA_ZoneType.Military,
                        30f,
                        120f,
                        960,
                        4f,
                        "tenda militar",
                        "tenda",
                        "barraca",
                        "quartel");

                case IA_BrainMaster.IA_BootstrapStage.AnalyzeTerrain:
                    brain.SetBootstrapStatus("analisando terreno, costa e pontos seguros");
                    if (now >= _nextCoastScanTime)
                    {
                        _cachedCoastAvailable = TryResolveFriendlyTerritoryCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                        if (!_cachedCoastAvailable)
                        {
                            _cachedCoastAvailable = TryFindCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                        }
                        _nextCoastScanTime = now + (_cachedCoastAvailable ? 15f : 30f);
                    }

                    _context.MapAnalyzer.FindPointInTerrain(landAnchor, IA_TerrainType.Land, 20f, 180f, 10);
                    _context.MapAnalyzer.FindPointInTerrain(landAnchor, IA_TerrainType.Water, 30f, 1200f, 10);
                    if (brain.GetBootstrapStageElapsed(now) >= BootstrapAnalysisDuration)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.ProduceGroundUnits, "analise concluida; produzindo unidades terrestres");
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.BuildShipyard:
                    if (estaleiros > 0 || piers > 0)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.HoldShipyard, "estaleiro pronto; aguardando 5s antes do navio");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(now) >= 18f || _bootstrapNavalNoCoastFailures >= 5)
                    {
                        brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.Completed,
                            "estaleiro adiado; IA liberada e tentativa naval segue em segundo plano");
                        return true;
                    }

                    if (now < _nextNavalAttemptTime)
                    {
                        brain.SetBootstrapStatus("aguardando nova janela para tentar o estaleiro na agua");
                        return true;
                    }

                    return TryBootstrapNavalBase(now, landAnchor, coastAvailable, navalAnchor);

                case IA_BrainMaster.IA_BootstrapStage.HoldShipyard:
                    brain.SetBootstrapStatus("estaleiro instalado; aguardando janela para produzir o primeiro navio");
                    if (brain.GetBootstrapStageElapsed(now) >= BootstrapShipyardHoldDuration)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.ProduceShip, "liberando producao do primeiro navio");
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.HoldShipLaunch:
                    brain.SetBootstrapStatus("navio produzido; aguardando saida segura para o mar");
                    return true;

                case IA_BrainMaster.IA_BootstrapStage.ProduceGroundUnits:
                case IA_BrainMaster.IA_BootstrapStage.HoldGroundUnits:
                case IA_BrainMaster.IA_BootstrapStage.ProduceAircraft:
                case IA_BrainMaster.IA_BootstrapStage.ProduceShip:
                    return true;

                case IA_BrainMaster.IA_BootstrapStage.Completed:
                case IA_BrainMaster.IA_BootstrapStage.Disabled:
                    return false;

                default:
                    return true;
            }
        }

        private bool AdvanceBootstrapAfterTime(
            float elapsed,
            float targetTime,
            IA_BrainMaster.IA_BootstrapStage nextStage,
            string waitStatus,
            string nextStatus)
        {
            if (_context.Brain == null)
            {
                return false;
            }

            if (elapsed >= targetTime)
            {
                _context.Brain.SetBootstrapStage(nextStage, nextStatus);
            }
            else
            {
                _context.Brain.SetBootstrapStatus(waitStatus);
            }

            return true;
        }

        private bool TryBootstrapMandatoryLandBuild(
            string label,
            Vector3 anchor,
            IA_ZoneType zone,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown,
            params string[] keys)
        {
            string reason = "nenhuma ancora terrestre valida";
            List<Vector3> anchors = BuildBootstrapLandAnchors(anchor);
            for (int i = 0; i < anchors.Count; i++)
            {
                if (TryBootstrapBuildByKeys(zone, IA_TerrainType.Land, anchors[i], minRadius, maxRadius, priority, cooldown, out reason, keys))
                {
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus(label + " enfileirado");
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(reason)
                && reason != "item em cooldown"
                && reason != "busca em cooldown"
                && reason != "duplicada em fila")
            {
                _context.Brain.ReportBootstrapError(label + ": " + reason);
                _context.Brain.SetBootstrapStatus("repetindo " + label + " | motivo=" + reason);
            }
            else
            {
                _context.Brain.SetBootstrapStatus("aguardando janela para " + label);
            }

            return true;
        }

        private bool TryBootstrapSupportHangarBuild(Vector3 anchor)
        {
            string reason = "nenhuma ancora terrestre valida";
            List<Vector3> anchors = BuildBootstrapLandAnchors(anchor);
            for (int i = 0; i < anchors.Count; i++)
            {
                if (TryBootstrapBuildByKeys(
                    IA_ZoneType.Air,
                    IA_TerrainType.Land,
                    anchors[i],
                    65f,
                    180f,
                    965,
                    5f,
                    out reason,
                    "Hangar",
                    "hangar",
                    "Heliporto",
                    "heliporto"))
                {
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("hangar de apoio enfileirado");
                    return true;
                }

                if (TryBootstrapBuildByKeys(
                    IA_ZoneType.Economy,
                    IA_TerrainType.Land,
                    anchors[i],
                    35f,
                    160f,
                    964,
                    5f,
                    out reason,
                    "Armazem",
                    "Armazem_Recursos",
                    "armazem",
                    "galpao"))
                {
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("hangar de apoio enfileirado");
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(reason)
                && reason != "item em cooldown"
                && reason != "busca em cooldown"
                && reason != "duplicada em fila")
            {
                _context.Brain.ReportBootstrapError("hangar de apoio: " + reason);
                _context.Brain.SetBootstrapStatus("repetindo hangar de apoio | motivo=" + reason);
            }
            else
            {
                _context.Brain.SetBootstrapStatus("aguardando janela para hangar de apoio");
            }

            return true;
        }

        private bool TryBootstrapNavalBase(float now, Vector3 landAnchor, bool coastAvailable, Vector3 navalAnchor)
        {
            if (_context.Brain == null)
            {
                return false;
            }

            IA_NavalBuildDiagnostics.Begin(
                _context.Brain,
                "Bootstrap do estaleiro",
                "coastAvailable=" + coastAvailable + " | stage=" + _context.Brain.BootstrapStage);
            NavalDiagnosticLine("inicio tentativa naval | land=" + landAnchor + " | naval=" + navalAnchor);
            NavalDiagnosticPoint(landAnchor, "ancora terra", new Color(0.6f, 0.35f, 0.1f, 1f), 4.8f);
            if (navalAnchor != Vector3.zero)
            {
                NavalDiagnosticPoint(navalAnchor, coastAvailable ? "ancora costa" : "ancora naval fallback", new Color(0.1f, 0.8f, 1f, 1f), 4.6f);
            }

            string itemKey = ResolveFirstAvailableKey(
                "Estaleiros navais",
                "Estaleiro Naval",
                "estaleiros navais",
                "estaleiro naval",
                "estaleiro",
                "estaleiros");
            if (string.IsNullOrEmpty(itemKey))
            {
                IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "item naval ausente no catalogo");
                NavalDiagnosticLine("item nao encontrado no catalogo");
                _context.Brain.ReportBootstrapError("estaleiro naval: item nao encontrado");
                _context.Brain.SetBootstrapStatus("estaleiro naval indisponivel no catalogo");
                return true;
            }

            NavalDiagnosticLine("item resolvido=" + itemKey);

            string reason = "nenhuma tentativa executada";
            if (HasManualBuildOverrideForItem(itemKey))
            {
                Vector3 manualReference = navalAnchor != Vector3.zero ? navalAnchor : landAnchor;
                Vector3 manualCandidate;
                IA_ManualBuildPoint manualPoint;
                string manualReason;
                ManualBuildCandidateStatus manualStatus = TryResolveManualBuildCandidate(itemKey, manualReference, out manualCandidate, out manualPoint, out manualReason);

                if (manualStatus == ManualBuildCandidateStatus.Found && manualPoint != null)
                {
                    NavalDiagnosticLine("ponto manual do estaleiro encontrado; pulando busca costeira");
                    NavalDiagnosticPoint(manualCandidate, "manual bootstrap: " + manualPoint.GetDisplayLabel(), new Color(1f, 0.82f, 0.15f, 1f), 4f, false);

                    if (ExecuteBuildImmediately(itemKey, manualCandidate, IA_ZoneType.Naval, manualPoint))
                    {
                        _nextNavalAttemptTime = now + 4f;
                        _bootstrapNavalNoCoastFailures = 0;
                        IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "estaleiro construido imediatamente no ponto manual");
                        _context.Brain.ReportBootstrapError(string.Empty);
                        _context.Brain.SetBootstrapStage(
                            IA_BrainMaster.IA_BootstrapStage.HoldShipyard,
                            "estaleiro naval construido imediatamente no ponto manual; aguardando 5s antes do navio");
                        return true;
                    }

                    reason = "falha ao executar construcao imediata no ponto manual";
                    _nextNavalAttemptTime = now + 2f;
                    IA_NavalBuildDiagnostics.SetStatus(_context.Brain, reason);
                    _context.Brain.ReportBootstrapError("estaleiro naval: " + reason);
                    _context.Brain.SetBootstrapStatus("repetindo estaleiro no ponto manual");
                    return true;
                }

                reason = string.IsNullOrEmpty(manualReason)
                    ? "ponto manual do estaleiro indisponivel"
                    : manualReason;
                _nextNavalAttemptTime = now + 3.5f;
                IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "ponto manual indisponivel: " + reason);
                NavalDiagnosticLine("ponto manual existe; busca costeira pulada | motivo=" + reason);
                _context.Brain.ReportBootstrapError("estaleiro naval: " + reason);
                _context.Brain.SetBootstrapStatus("aguardando ponto manual do estaleiro | motivo=" + reason);
                return true;
            }

            var anchors = new List<Vector3>();
            AddSearchAnchor(anchors, navalAnchor);
            AddSearchAnchor(anchors, landAnchor);
            AddBootstrapNavalSearchAnchors(anchors, landAnchor);
            AddBootstrapNavalSearchAnchors(anchors, navalAnchor);
            if (_context.Brain != null)
            {
                AddSearchAnchor(anchors, _context.Brain.transform.position);
                AddBootstrapNavalSearchAnchors(anchors, _context.Brain.transform.position);
            }

            Vector3 directCoast;
            if (TryFindDirectCoastalAnchor(landAnchor, out directCoast))
            {
                AddSearchAnchor(anchors, directCoast);
                NavalDiagnosticPoint(directCoast, "costa direta", new Color(0f, 0.9f, 1f, 1f), 3.8f);
            }

            Vector3 fallbackWater = _context.MapAnalyzer.FindPointInTerrain(landAnchor, IA_TerrainType.Water, 20f, 520f, 10);
            AddSearchAnchor(anchors, fallbackWater);
            NavalDiagnosticPoint(fallbackWater, "agua fallback", new Color(0.2f, 0.6f, 1f, 1f), 3.2f);
            Vector3 wideWaterAnchor;
            if (TryFindWideWaterSearchAnchor(landAnchor, 30f, 520f, out wideWaterAnchor))
            {
                AddSearchAnchor(anchors, wideWaterAnchor);
                NavalDiagnosticPoint(wideWaterAnchor, "agua ampla", new Color(0.2f, 0.45f, 1f, 1f), 3.2f);
            }

            for (int i = 0; i < _context.WorldState.OwnStructures.Count && anchors.Count < 8; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                AddBootstrapNavalSearchAnchors(anchors, structure.transform.position);
            }

            var refinedAnchors = new List<Vector3>();
            int refinementBudget = Mathf.Min(anchors.Count, MaxBootstrapAnchorRefinements);
            for (int i = 0; i < refinementBudget; i++)
            {
                Vector3 refinedAnchor;
                if (TryFindDirectCoastalAnchor(anchors[i], out refinedAnchor) || TryFindCoastalAnchor(anchors[i], out refinedAnchor))
                {
                    AddSearchAnchor(refinedAnchors, refinedAnchor);
                    NavalDiagnosticPoint(refinedAnchor, "ancora refinada " + (refinedAnchors.Count), new Color(0f, 1f, 0.9f, 1f), 3.6f);
                }
            }

            if (refinedAnchors.Count > 0)
            {
                anchors = refinedAnchors;
            }

            NavalDiagnosticLine("ancoras apos refinamento=" + anchors.Count);

            if (landAnchor != Vector3.zero)
            {
                Vector3 flatLandAnchor = Flatten(landAnchor);
                anchors.RemoveAll(anchor => Vector3.Distance(Flatten(anchor), flatLandAnchor) > 850f);
            }

            NavalDiagnosticLine("ancoras apos filtro de distancia=" + anchors.Count);

            var territoryRejects = new List<string>();
            var preferredAnchors = new List<Vector3>();
            var fallbackAnchors = new List<Vector3>();
            for (int i = 0; i < anchors.Count; i++)
            {
                string territoryReason;
                if (_context.Backend.BuildService.ValidateTerritoryProbe(itemKey, anchors[i], out territoryReason))
                {
                    AddSearchAnchor(preferredAnchors, anchors[i]);
                    NavalDiagnosticPoint(anchors[i], "territorio ok " + (preferredAnchors.Count), new Color(0.1f, 1f, 0.35f, 1f), 3.3f, false);
                    continue;
                }

                if (territoryRejects.Count < 3)
                {
                    territoryRejects.Add(anchors[i] + " => " + territoryReason);
                }

                NavalDiagnosticPoint(anchors[i], "territorio falhou: " + territoryReason, new Color(1f, 0.2f, 0.2f, 1f), 4.1f);
                AddSearchAnchor(fallbackAnchors, anchors[i]);
            }

            if (preferredAnchors.Count > 0)
            {
                anchors = preferredAnchors;
                NavalDiagnosticLine("usando ancoras preferidas=" + anchors.Count);
            }
            else if (fallbackAnchors.Count > 0)
            {
                anchors = fallbackAnchors;
                NavalDiagnosticLine("sem ancora preferida; usando fallback=" + anchors.Count);
            }

            if (anchors.Count == 0)
            {
                _bootstrapNavalNoCoastFailures++;
                IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "sem ancoras costeiras validas");
                NavalDiagnosticLine("falha total de ancoras | tentativas_sem_costa=" + _bootstrapNavalNoCoastFailures);

                string expansionReason = string.Empty;
                if (_bootstrapNavalNoCoastFailures >= 3
                    && TryBootstrapTerritoryExpansionTowardsCoast(now, landAnchor, out expansionReason))
                {
                    _nextNavalAttemptTime = now + 10f;
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("bandeira enfileirada para reivindicar a costa do estaleiro");
                    return true;
                }

                _nextNavalAttemptTime = now + 6f;
                _nextCoastScanTime = Mathf.Max(_nextCoastScanTime, now + 6f);
                string territoryDetail = territoryRejects.Count > 0
                    ? " | " + string.Join(" | ", territoryRejects.ToArray())
                    : string.Empty;
                _context.Brain.ReportBootstrapError("estaleiro naval: sem costa dentro do territorio" + territoryDetail);
                _context.Brain.SetBootstrapStatus(
                    "sem costa propria para estaleiro; aguardando expansao territorial"
                    + (!string.IsNullOrEmpty(expansionReason) ? " | expansao=" + expansionReason : string.Empty));
                return true;
            }

            _bootstrapNavalNoCoastFailures = 0;
            int attemptIndex = Mathf.Abs(_bootstrapNavalAttemptCursor++);
            int anchorIndex = attemptIndex % anchors.Count;
            bool useDirectFallback = ((attemptIndex / anchors.Count) % 2) == 1;
            Vector3 selectedAnchor = anchors[anchorIndex];
            NavalDiagnosticPoint(selectedAnchor, "ancora selecionada", new Color(1f, 1f, 1f, 1f), 4.8f);
            NavalDiagnosticLine("ancora selecionada=" + (anchorIndex + 1) + "/" + anchors.Count + " | modo=" + (useDirectFallback ? "fallback_direto" : "busca_validada"));
            _context.Brain.TraceBootstrapStep(
                "tentando estaleiro | modo=" + (useDirectFallback ? "fallback_direto" : "busca_validada")
                + " | ancora=" + (anchorIndex + 1) + "/" + anchors.Count
                + " | pos=" + selectedAnchor);

            if (!useDirectFallback)
            {
                if (TryBootstrapQueueSpecificBuild(
                    itemKey,
                    IA_ZoneType.Naval,
                    IA_TerrainType.Water,
                    selectedAnchor,
                    coastAvailable ? 4f : 12f,
                    coastAvailable ? 680f : 1850f,
                    995,
                    4.5f,
                    out reason))
                {
                    _nextNavalAttemptTime = now + 4f;
                    _bootstrapNavalNoCoastFailures = 0;
                    IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "estaleiro enfileirado com busca validada");
                    NavalDiagnosticLine("sucesso via busca validada");
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("estaleiro naval enfileirado na agua");
                    return true;
                }
            }
            else
            {
                Vector3 candidate;
                IA_ManualBuildPoint manualPoint;
                string manualReason;
                ManualBuildCandidateStatus manualStatus = TryResolveManualBuildCandidate(itemKey, selectedAnchor, out candidate, out manualPoint, out manualReason);
                bool queuedManual = manualStatus == ManualBuildCandidateStatus.Found
                    && QueueBuild(itemKey, candidate, IA_ZoneType.Naval, 996, 4.5f, manualPoint);
                bool queuedFallback = manualStatus == ManualBuildCandidateStatus.None
                    && TryFindDirectNavalCandidate(itemKey, IA_ZoneType.Naval, selectedAnchor, 0f, coastAvailable ? 920f : 2100f, out candidate, out reason)
                    && QueueBuild(itemKey, candidate, IA_ZoneType.Naval, 996, 4.5f);

                if (manualStatus == ManualBuildCandidateStatus.Blocked)
                {
                    reason = manualReason;
                }

                if (queuedManual || queuedFallback)
                {
                    ClearNavalSearchBackoff(itemKey, selectedAnchor);
                    _nextNavalAttemptTime = now + 4f;
                    _bootstrapNavalNoCoastFailures = 0;
                    IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "estaleiro enfileirado via fallback direto");
                    NavalDiagnosticLine("sucesso via fallback direto");
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("estaleiro naval enfileirado via fallback direto");
                    return true;
                }
            }

            // O bootstrap do estaleiro tem uma janela curta (18s). Se a primeira falha
            // empurrar a proxima tentativa para 120s, a IA sai dessa fase antes de tentar
            // outro ponto costeiro e parece "parar" de construir o estaleiro.
            float retryDelay = ResolveBootstrapNavalRetryDelay(useDirectFallback, reason);

            _nextNavalAttemptTime = now + retryDelay;
            _nextCoastScanTime = Mathf.Max(_nextCoastScanTime, now + Mathf.Max(6f, retryDelay * 0.55f));
            IA_NavalBuildDiagnostics.SetStatus(_context.Brain, "tentativa naval falhou: " + reason);
            NavalDiagnosticLine("tentativa falhou | motivo=" + reason + " | retry=" + retryDelay.ToString("0.0") + "s");
            _context.Brain.ReportBootstrapError("estaleiro naval: " + reason);
            _context.Brain.SetBootstrapStatus(
                "tentativa " + (attemptIndex + 1) + " do estaleiro falhou | ancora "
                + (anchorIndex + 1) + "/" + anchors.Count
                + " | motivo=" + reason);
            return true;
        }

        private bool TryBootstrapTerritoryExpansionTowardsCoast(float now, Vector3 landAnchor, out string reason)
        {
            reason = string.Empty;
            if (now < _nextNavalExpansionAttemptTime)
            {
                reason = "expansao em cooldown";
                NavalDiagnosticLine("expansao costeira em cooldown");
                return false;
            }

            string itemKey = ResolveFirstAvailableKey("Bandeira", "bandeira", "Flag", "flag");
            if (string.IsNullOrEmpty(itemKey))
            {
                _nextNavalExpansionAttemptTime = now + 25f;
                reason = "bandeira indisponivel";
                NavalDiagnosticLine("expansao costeira indisponivel: bandeira ausente");
                return false;
            }

            Vector3 coastTarget;
            if (!TryFindNeutralCoastForExpansion(landAnchor, out coastTarget, out reason))
            {
                _nextNavalExpansionAttemptTime = now + 12f;
                NavalDiagnosticLine("expansao costeira sem alvo: " + reason);
                return false;
            }

            NavalDiagnosticPoint(coastTarget, "alvo de expansao costeira", new Color(1f, 0.8f, 0.15f, 1f), 4.2f);
            NavalDiagnosticLine("alvo de expansao encontrado=" + coastTarget);

            string buildReason;
            if (TryBootstrapQueueSpecificBuild(
                itemKey,
                IA_ZoneType.Core,
                IA_TerrainType.Land,
                coastTarget,
                35f,
                220f,
                994,
                14f,
                out buildReason))
            {
                _nextNavalExpansionAttemptTime = now + 24f;
                reason = string.Empty;
                NavalDiagnosticLine("bandeira enfileirada para abrir costa");
                return true;
            }

            _nextNavalExpansionAttemptTime = now + (buildReason == "busca em cooldown" ? 18f : 10f);
            reason = buildReason;
            NavalDiagnosticLine("expansao costeira falhou: " + buildReason);
            return false;
        }

        private bool TryFindNeutralCoastForExpansion(Vector3 center, out Vector3 coastalAnchor, out string reason)
        {
            coastalAnchor = center;
            reason = "nenhuma costa neutra encontrada";

            GerenteDeTerritorio territory = GerenteDeTerritorio.Instancia;
            Vector3 reference = ResolveStrategicReference(center);
            bool foundBest = false;
            float bestScore = float.MinValue;
            List<Vector3> anchors = BuildBootstrapLandAnchors(center);
            if (anchors.Count == 0)
            {
                anchors.Add(EnsureDryLandAnchor(center));
            }

            for (int a = 0; a < anchors.Count; a++)
            {
                Vector3 searchCenter = anchors[a];
                float[] radii = { 90f, 140f, 220f, 320f, 460f, 620f, 820f, 1100f };
                for (int r = 0; r < radii.Length; r++)
                {
                    float radius = radii[r];
                    for (int i = 0; i < 20; i++)
                    {
                        float angle = (360f / 20f) * i * Mathf.Deg2Rad;
                        Vector3 probe = searchCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                        if (cell == null
                            || (cell.Terrain != IA_TerrainType.Coast && cell.Terrain != IA_TerrainType.Water))
                        {
                            continue;
                        }

                        Vector3 candidatePosition = new Vector3(probe.x, cell.Height, probe.z);
                        if (territory != null && territory.ObterDonoDoPonto(candidatePosition) != 0)
                        {
                            continue;
                        }

                        if (NavalPlacementResolver.DistanceToMapEdge(candidatePosition) < 60f)
                        {
                            continue;
                        }

                        Vector3 inlandAnchor = EnsureDryLandAnchor(candidatePosition);
                        if (!IsDryLandAnchor(inlandAnchor))
                        {
                            continue;
                        }

                        float score = -Vector3.Distance(Flatten(candidatePosition), Flatten(reference));
                        score -= Vector3.Distance(Flatten(inlandAnchor), Flatten(searchCenter)) * 0.35f;
                        if (!foundBest || score > bestScore)
                        {
                            foundBest = true;
                            bestScore = score;
                            coastalAnchor = candidatePosition;
                        }
                    }
                }
            }

            if (!foundBest)
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryBootstrapBuildByKeys(
            IA_ZoneType zone,
            IA_TerrainType terrain,
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown,
            out string reason,
            params string[] keys)
        {
            reason = "item bootstrap nao encontrado";
            if (keys == null)
            {
                return false;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                string itemKey = ResolveFirstAvailableKey(keys[i]);
                if (string.IsNullOrEmpty(itemKey))
                {
                    continue;
                }

                if (TryBootstrapQueueSpecificBuild(itemKey, zone, terrain, anchor, minRadius, maxRadius, priority, cooldown, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBootstrapQueueSpecificBuild(
            string itemKey,
            IA_ZoneType zone,
            IA_TerrainType terrain,
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            int priority,
            float cooldown,
            out string reason)
        {
            float now = Time.time;
            reason = string.Empty;
            bool trackNavalDiagnostics = ShouldTrackNavalDiagnostic(itemKey, terrain);
            if (trackNavalDiagnostics)
            {
                NavalDiagnosticLine(
                    "busca especifica | item=" + itemKey
                    + " | terreno=" + terrain
                    + " | ancora=" + anchor
                    + " | raio=" + minRadius.ToString("0") + "-" + maxRadius.ToString("0"));
            }

            if (!CanTryBuildItem(itemKey, now))
            {
                reason = "item em cooldown";
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("item em cooldown");
                }

                return false;
            }

            bool hasManualOverride = HasManualBuildOverrideForItem(itemKey);
            if (!hasManualOverride && !CanRetryPlacementSearch(itemKey, terrain, now))
            {
                reason = "busca em cooldown";
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("busca em cooldown");
                }

                return false;
            }

            Vector3 candidate;
            if (!TryFindValidatedCandidate(itemKey, zone, anchor, terrain, minRadius, maxRadius, out candidate, out reason))
            {
                MarkPlacementSearchFailure(itemKey, terrain, now);
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("busca falhou: " + reason);
                }

                return false;
            }

            ClearPlacementSearchFailure(itemKey, terrain);
            if (terrain == IA_TerrainType.Water)
            {
                ClearNavalSearchBackoff(itemKey, anchor);
            }

            IA_ManualBuildPoint manualPoint = ConsumePendingManualBuildPoint();
            if (!QueueBuild(itemKey, candidate, zone, priority, cooldown, manualPoint))
            {
                reason = "duplicada em fila";
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("fila rejeitou item duplicado");
                }

                return false;
            }

            reason = string.Empty;
            if (trackNavalDiagnostics)
            {
                NavalDiagnosticLine("busca especifica concluiu com fila aceita");
            }

            return true;
        }

        private bool TryFindValidatedCandidateInternal(
            string itemKey,
            IA_ZoneType zone,
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            int rings,
            int samplesPerRing,
            out Vector3 candidate,
            out string reason)
        {
            long profileStart = BeginTimingScope();
            candidate = anchor;
            reason = "nenhum ponto valido";
            bool trackNavalDiagnostics = ShouldTrackNavalDiagnostic(itemKey, desiredTerrain);
            int probes = 0;
            int terrainRejected = 0;
            int territoryRejected = 0;
            int placementRejected = 0;

            int totalRings = Mathf.Max(1, rings);
            int totalSamples = Mathf.Max(8, samplesPerRing);
            float baseAngleOffset = (Time.frameCount % totalSamples) * (360f / totalSamples);

            for (int ring = 0; ring < totalRings; ring++)
            {
                float t = totalRings == 1 ? 0f : ring / (float)(totalRings - 1);
                float radius = Mathf.Lerp(minRadius, maxRadius, t);

                for (int i = 0; i < totalSamples; i++)
                {
                    float angleDeg = baseAngleOffset + ((360f / totalSamples) * i) + (ring * 7f);
                    float angle = angleDeg * Mathf.Deg2Rad;
                    Vector3 probe = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    probes++;
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);

                    if (!IsTerrainCandidateAccepted(cell, desiredTerrain))
                    {
                        terrainRejected++;
                        continue;
                    }

                    float candidateY = cell.Height;
                    if (desiredTerrain == IA_TerrainType.Water)
                    {
                        candidateY = NavalPlacementResolver.ResolveSeaLevel();
                    }
                    else
                    {
                        float groundHeight;
                        if (RegistroSuperficieMapa.TryGetAltura(probe, TipoSuperficieMapa.Chao, out groundHeight))
                        {
                            candidateY = groundHeight;
                        }
                    }

                    Vector3 candidatePosition = new Vector3(probe.x, candidateY, probe.z);

                    if (!_context.Backend.BuildService.ValidateTerritoryProbe(itemKey, candidatePosition, out reason))
                    {
                        territoryRejected++;
                        if (trackNavalDiagnostics)
                        {
                            NavalDiagnosticPoint(candidatePosition, "territorio: " + reason, new Color(1f, 0.2f, 0.2f, 1f), 2.4f);
                        }

                        continue;
                    }

                    if (_context.Backend.BuildService.ValidatePlacement(
                        itemKey,
                        candidatePosition,
                        zone,
                        _context.WorldState,
                        _context.MapAnalyzer,
                        _context.ThreatAnalyzer,
                        out reason))
                    {
                        candidate = candidatePosition;
                        if (trackNavalDiagnostics)
                        {
                            NavalDiagnosticPoint(candidatePosition, "placement ok", new Color(0.1f, 1f, 0.35f, 1f), 2.2f, false);
                        }

                        EndTimingScope(
                            "TryFindValidatedCandidateInternal",
                            "item=" + itemKey + " | terrain=" + desiredTerrain + " | probes=" + probes + " | terrainReject=" + terrainRejected + " | territoryReject=" + territoryRejected + " | placementReject=" + placementRejected + " | success=true",
                            profileStart,
                            2.25f);
                        return true;
                    }

                    placementRejected++;
                    if (trackNavalDiagnostics)
                    {
                        NavalDiagnosticPoint(candidatePosition, "placement: " + reason, new Color(1f, 0.55f, 0.1f, 1f), 2.6f);
                    }
                }
            }

            EndTimingScope(
                "TryFindValidatedCandidateInternal",
                "item=" + itemKey + " | terrain=" + desiredTerrain + " | probes=" + probes + " | terrainReject=" + terrainRejected + " | territoryReject=" + territoryRejected + " | placementReject=" + placementRejected + " | success=false",
                profileStart,
                2.25f);
            return false;
        }

        private static bool IsTerrainCandidateAccepted(IA_MapCell cell, IA_TerrainType desiredTerrain)
        {
            if (cell == null)
            {
                return false;
            }

            if (desiredTerrain == IA_TerrainType.Water)
            {
                return cell.Terrain == IA_TerrainType.Water || cell.Terrain == IA_TerrainType.Coast;
            }

            if (desiredTerrain == IA_TerrainType.Land)
            {
                return cell.Terrain != IA_TerrainType.Water && cell.BuildableLand;
            }

            return cell.Terrain == desiredTerrain && cell.BuildableLand;
        }

        private bool QueueBuild(string itemKey, Vector3 candidate, IA_ZoneType zone, int priority, float cooldown, IA_ManualBuildPoint manualPoint = null)
        {
            bool trackNavalDiagnostics = ShouldTrackNavalDiagnostic(itemKey, zone);
            if (trackNavalDiagnostics)
            {
                NavalDiagnosticPoint(candidate, "fila: candidato bruto", new Color(0.95f, 0.95f, 0.95f, 1f), 2.6f, false);
            }

            bool forceManualPlacement = manualPoint != null && manualPoint.ForceExactPlacement;
            Quaternion rotation = manualPoint != null ? manualPoint.transform.rotation : Quaternion.identity;
            string reason;
            if (!forceManualPlacement && !TryResolveBuildPose(itemKey, ref candidate, ref rotation, out reason))
            {
                if (trackNavalDiagnostics)
                {
                    NavalDiagnosticLine("pose falhou | item=" + itemKey + " | motivo=" + reason);
                    NavalDiagnosticPoint(candidate, "pose falhou: " + reason, new Color(1f, 0.25f, 0.25f, 1f), 4.4f);
                }

                return false;
            }

            if (trackNavalDiagnostics)
            {
                string poseLabel = forceManualPlacement ? "manual exato" : "pose resolvida";
                NavalDiagnosticLine(poseLabel + " | item=" + itemKey + " | pos=" + candidate);
                NavalDiagnosticPoint(candidate, poseLabel, new Color(0.2f, 0.9f, 1f, 1f), 3f, false);
            }

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = itemKey,
                Position = candidate,
                Rotation = rotation,
                Zone = zone,
                ForceManualPlacement = forceManualPlacement,
                ManualPointLabel = manualPoint != null ? manualPoint.GetDisplayLabel() : string.Empty
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Build,
                Priority = priority,
                DedupKey = "build:" + IA_Text.Normalize(itemKey),
                CooldownSeconds = cooldown,
                Payload = payload
            };

            bool queued = _context.CommandQueue.Enqueue(request, Time.time, out reason);
            if (trackNavalDiagnostics)
            {
                NavalDiagnosticLine("fila " + (queued ? "ok" : "falhou") + " | item=" + itemKey + (string.IsNullOrEmpty(reason) ? string.Empty : " | motivo=" + reason));
                NavalDiagnosticPoint(
                    candidate,
                    queued ? "fila ok" : "fila falhou: " + reason,
                    queued ? new Color(0.15f, 1f, 0.25f, 1f) : new Color(1f, 0.1f, 0.1f, 1f),
                    queued ? 3.2f : 4.6f,
                    !queued);
            }

            if (queued)
            {
                DiagnosticoDesempenhoJogo.RegistrarConstrucao(itemKey, candidate);
            }

            return queued;
        }

        private bool ShouldTrackNavalDiagnostic(string itemKey, IA_TerrainType terrain)
        {
            string normalized = IA_Text.Normalize(itemKey);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (normalized.Contains("estaleiro")
                || normalized.Contains("pier")
                || normalized.Contains("plataforma"))
            {
                return true;
            }

            if (normalized.Contains("bandeira") || normalized.Contains("flag"))
            {
                return _context.Brain != null
                       && _context.Brain.BootstrapStage == IA_BrainMaster.IA_BootstrapStage.BuildShipyard;
            }

            return terrain == IA_TerrainType.Water;
        }

        private bool ShouldTrackNavalDiagnostic(string itemKey, IA_ZoneType zone)
        {
            string normalized = IA_Text.Normalize(itemKey);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return zone == IA_ZoneType.Naval
                   || normalized.Contains("estaleiro")
                   || normalized.Contains("pier")
                   || normalized.Contains("plataforma")
                   || ((_context.Brain != null
                        && _context.Brain.BootstrapStage == IA_BrainMaster.IA_BootstrapStage.BuildShipyard)
                       && (normalized.Contains("bandeira") || normalized.Contains("flag")));
        }

        private void NavalDiagnosticLine(string line)
        {
            IA_NavalBuildDiagnostics.AddLine(_context.Brain, line);
        }

        private void NavalDiagnosticPoint(Vector3 position, string label, Color color, float size = 3.5f, bool wire = true)
        {
            IA_NavalBuildDiagnostics.AddPoint(_context.Brain, position, label, color, size, wire);
        }

        private void UpdateProgressTracker(float now)
        {
            int currentStructures = _context.WorldState.OwnStructures.Count;
            if (_lastKnownStructureCount < 0)
            {
                _lastKnownStructureCount = currentStructures;
                _lastProgressTime = now;
                return;
            }

            if (currentStructures != _lastKnownStructureCount)
            {
                _lastKnownStructureCount = currentStructures;
                _lastProgressTime = now;
                _recoveryLevel = 0;
                _coastalAnchorCache.Clear();
                _navalSearchBackoffUntil.Clear();
                _cachedTerritoryStructureCount = -1;
                _cachedTerritoryAnchorsUntil = 0f;
            }
        }

        private bool ShouldUseRecovery(float now, int cityHall)
        {
            if (cityHall <= 0)
            {
                return false;
            }

            if (now < _nextRecoveryAttemptTime)
            {
                return false;
            }

            if (_context.CommandQueue.PendingCount > 0)
            {
                return false;
            }

            return now - _lastProgressTime >= 18f;
        }

        private bool TryEmergencyRecoveryBuild(
            float now,
            Vector3 baseCenter,
            float localThreat,
            int hq,
            int barracks,
            int factories,
            int radars,
            int sentries,
            int warehouses,
            int airports,
            int heliports,
            int estaleiros,
            int piers,
            bool coastAvailable,
            Vector3 navalAnchor)
        {
            long profileStart = BeginTimingScope();
            _nextRecoveryAttemptTime = now + 5f;
            _recoveryLevel = Mathf.Min(_recoveryLevel + 1, 5);

            bool built = false;
            if (barracks == 0)
            {
                built = TryEmergencyBuild(baseCenter, IA_ZoneType.Military, IA_TerrainType.Land, 85f, 220f, "quartel", "tenda militar", "tenda", "barraca");
            }
            else if (factories == 0)
            {
                built = TryEmergencyBuild(baseCenter, IA_ZoneType.Military, IA_TerrainType.Land, 120f, 260f, "fabrica", "construtor de veiculos", "construtor");
            }
            else if (warehouses == 0)
            {
                built = TryEmergencyBuild(baseCenter, IA_ZoneType.Economy, IA_TerrainType.Land, 90f, 240f, "armazem", "galpao", "refinaria");
            }
            else if (sentries < 1 && (_context.WorldState.VisibleEnemies.Count > 0 || localThreat > 70f))
            {
                built = TryEmergencyBuild(baseCenter, IA_ZoneType.Defense, IA_TerrainType.Choke, 160f, 300f, "torreta", "torre sentinela", "artilharia");
            }

            if (!built)
            {
                LogVerboseWarning(
                    "recovery:none",
                    "[IA_BuildDirector] Recovery mode ativo sem sucesso. Estruturas=" + _context.WorldState.OwnStructures.Count + " | nivel=" + _recoveryLevel,
                    20f);
            }

            EndTimingScope(
                "TryEmergencyRecoveryBuild",
                "built=" + built + " | level=" + _recoveryLevel + " | threat=" + localThreat.ToString("0.0"),
                profileStart,
                2.00f);
            return built;
        }

        private bool TryEmergencyBuild(
            Vector3 anchor,
            IA_ZoneType zone,
            IA_TerrainType terrain,
            float minRadius,
            float maxRadius,
            params string[] resolveKeys)
        {
            long profileStart = BeginTimingScope();
            string itemKey = ResolveFirstAvailableKey(resolveKeys);
            if (string.IsNullOrEmpty(itemKey))
            {
                EndTimingScope("TryEmergencyBuild", "item=<none> | success=false | reason=itemMissing", profileStart, 2.00f);
                return false;
            }

            Vector3 candidate;
            float boostedMaxRadius = maxRadius + (_recoveryLevel * 45f);
            bool hasManualOverride = HasManualBuildOverrideForItem(itemKey);
            if (!TryFindValidatedCandidate(itemKey, zone, anchor, terrain, minRadius, boostedMaxRadius, out candidate))
            {
                bool legacySuccess = !hasManualOverride
                    && TryLegacyEmergencyBuild(itemKey, anchor, zone, terrain, minRadius, boostedMaxRadius);
                EndTimingScope(
                    "TryEmergencyBuild",
                    "item=" + itemKey + " | success=" + legacySuccess + " | manual=" + hasManualOverride + " | legacy=true",
                    profileStart,
                    2.00f);
                return legacySuccess;
            }

            IA_ManualBuildPoint manualPoint = ConsumePendingManualBuildPoint();
            if (ExecuteBuildImmediately(itemKey, candidate, zone, manualPoint))
            {
                EndTimingScope(
                    "TryEmergencyBuild",
                    "item=" + itemKey + " | success=true | manual=" + (manualPoint != null),
                    profileStart,
                    2.00f);
                return true;
            }

            bool fallbackSuccess = !hasManualOverride
                && TryLegacyEmergencyBuild(itemKey, anchor, zone, terrain, minRadius, boostedMaxRadius);
            EndTimingScope(
                "TryEmergencyBuild",
                "item=" + itemKey + " | success=" + fallbackSuccess + " | manual=" + hasManualOverride + " | legacy=true",
                profileStart,
                2.00f);
            return fallbackSuccess;
        }

        private string ResolveFirstAvailableKey(params string[] keys)
        {
            if (keys == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                DadosConstrucao data;
                if (_context.Backend.TryResolveItem(keys[i], out data) && data != null)
                {
                    return keys[i];
                }
            }

            return null;
        }

        private bool ExecuteBuildImmediately(string itemKey, Vector3 candidate, IA_ZoneType zone, IA_ManualBuildPoint manualPoint = null)
        {
            long profileStart = BeginTimingScope();
            if (_context.Brain != null && _context.Brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                EndTimingScope("ExecuteBuildImmediately", "item=" + itemKey + " | success=false | mode=ShadowReadOnly", profileStart, 2.00f);
                return false;
            }

            bool forceManualPlacement = manualPoint != null && manualPoint.ForceExactPlacement;
            Quaternion rotation = manualPoint != null ? manualPoint.transform.rotation : Quaternion.identity;
            string poseReason;
            if (!forceManualPlacement && !TryResolveBuildPose(itemKey, ref candidate, ref rotation, out poseReason))
            {
                EndTimingScope("ExecuteBuildImmediately", "item=" + itemKey + " | success=false | reason=" + poseReason, profileStart, 2.00f);
                return false;
            }

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = itemKey,
                Position = candidate,
                Rotation = rotation,
                Zone = zone,
                ForceManualPlacement = forceManualPlacement,
                ManualPointLabel = manualPoint != null ? manualPoint.GetDisplayLabel() : string.Empty
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Build,
                Priority = 999,
                DedupKey = "recovery_build:" + IA_Text.Normalize(itemKey) + ":" + Time.frameCount,
                CooldownSeconds = 0f,
                Payload = payload
            };

            string message;
            bool success = _context.Backend.CommandService.Execute(request, _context, out message);
            if (success)
            {
                _lastProgressTime = Time.time;
                if (_context.Brain != null && _context.Brain.EnableVerboseLogs && !Application.isEditor)
                {
                    Debug.Log("[IA_BuildDirector] Recovery build OK: " + itemKey + " @ " + candidate);
                }
            }
            else if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
            {
                LogVerboseWarning("recovery:buildfail:" + IA_Text.Normalize(itemKey) + ":" + message, "[IA_BuildDirector] Recovery build falhou: " + itemKey + " | " + message, 15f);
            }

            EndTimingScope(
                "ExecuteBuildImmediately",
                "item=" + itemKey + " | success=" + success + (string.IsNullOrEmpty(message) ? string.Empty : " | msg=" + message),
                profileStart,
                2.00f);
            return success;
        }

        private bool TryLegacyEmergencyBuild(
            string itemKey,
            Vector3 anchor,
            IA_ZoneType zone,
            IA_TerrainType terrain,
            float minRadius,
            float maxRadius)
        {
            long profileStart = BeginTimingScope();
            DadosConstrucao item;
            if (!_context.Backend.TryResolveItem(itemKey, out item) || item == null || item.prefabDaUnidade == null)
            {
                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=itemMissing", profileStart, 2.00f);
                return false;
            }

            Vector3 candidate;
            string reason;
            if (!TryFindLegacyCandidate(itemKey, anchor, terrain, minRadius, maxRadius, out candidate, out reason))
            {
                if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    LogVerboseWarning("legacy:nopoint:" + IA_Text.Normalize(itemKey) + ":" + reason, "[IA_BuildDirector] Legacy recovery sem ponto para " + itemKey + " | " + reason, 18f);
                }

                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            Quaternion rotation = Quaternion.identity;
            if (!TryResolveBuildPose(itemKey, ref candidate, ref rotation, out reason))
            {
                if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    LogVerboseWarning("legacy:nopose:" + IA_Text.Normalize(itemKey) + ":" + reason, "[IA_BuildDirector] Legacy recovery sem pose valida para " + itemKey + " | " + reason, 18f);
                }

                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            if (!_context.Backend.BuildService.ValidatePlacement(
                itemKey,
                candidate,
                rotation,
                zone,
                _context.WorldState,
                _context.MapAnalyzer,
                _context.ThreatAnalyzer,
                out reason))
            {
                if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    LogVerboseWarning("legacy:rejected:" + IA_Text.Normalize(itemKey) + ":" + reason, "[IA_BuildDirector] Legacy recovery validacao final recusou " + itemKey + " | " + reason, 18f);
                }

                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            if (!_context.Brain.TrySpend(item.preco))
            {
                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=credits", profileStart, 2.00f);
                return false;
            }

            GameObject built = null;
            Construtor construtor = Object.FindFirstObjectByType<Construtor>();
            if (construtor != null)
            {
                built = construtor.ConstruirEstruturaIA(item.prefabDaUnidade, candidate, rotation);
            }
            else
            {
                built = Object.Instantiate(item.prefabDaUnidade, candidate, rotation);
            }

            if (built == null)
            {
                _context.Brain.Refund(item.preco);
                EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=false | reason=instantiateFailed", profileStart, 2.00f);
                return false;
            }

            _context.Backend.EnsureIdentity(built);
            _context.WorldState.MarkDirty();
            _lastProgressTime = Time.time;

            if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
            {
                LogVerboseWarning("legacy:built:" + IA_Text.Normalize(itemKey), "[IA_BuildDirector] Legacy recovery construiu " + itemKey + " em " + candidate, 10f);
            }

            EndTimingScope("TryLegacyEmergencyBuild", "item=" + itemKey + " | success=true", profileStart, 2.00f);
            return true;
        }

        private bool TryFindLegacyCandidate(
            string itemKey,
            Vector3 anchor,
            IA_TerrainType desiredTerrain,
            float minRadius,
            float maxRadius,
            out Vector3 candidate,
            out string reason)
        {
            long profileStart = BeginTimingScope();
            candidate = anchor;
            reason = "nenhum ponto legado";
            int probes = 0;
            int terrainRejected = 0;
            int spaceRejected = 0;

            if (anchor == Vector3.zero && _context.Brain != null)
            {
                anchor = _context.Brain.transform.position;
            }

            float safeRadius = EstimateLegacySafeRadius(itemKey);
            int rings = desiredTerrain == IA_TerrainType.Water
                ? 6 + Mathf.Min(2, _recoveryLevel)
                : 4 + Mathf.Min(2, _recoveryLevel);
            int samples = desiredTerrain == IA_TerrainType.Water
                ? 14 + (_recoveryLevel * 2)
                : 10 + (_recoveryLevel * 2);

            for (int ring = 0; ring < rings; ring++)
            {
                float t = rings <= 1 ? 0f : ring / (float)(rings - 1);
                float radius = Mathf.Lerp(minRadius, maxRadius + 120f, t);

                for (int i = 0; i < samples; i++)
                {
                    float angleDeg = ((360f / samples) * i) + (ring * 11f);
                    float angle = angleDeg * Mathf.Deg2Rad;
                    Vector3 probe = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    probes++;
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);

                    if (!IsTerrainCandidateAccepted(cell, desiredTerrain))
                    {
                        terrainRejected++;
                        continue;
                    }

                    Vector3 pos = cell.Center;
                    if (!LegacySpaceFree(pos, safeRadius))
                    {
                        reason = "espaco legado ocupado";
                        spaceRejected++;
                        continue;
                    }

                    Vector3 resolved = pos;
                    Quaternion rotation = Quaternion.identity;
                    if (!TryResolveBuildPose(itemKey, ref resolved, ref rotation, out reason))
                    {
                        continue;
                    }

                    if (!_context.Backend.BuildService.ValidateTerritoryProbe(itemKey, resolved, out reason))
                    {
                        continue;
                    }

                    candidate = resolved;
                    EndTimingScope(
                        "TryFindLegacyCandidate",
                        "item=" + itemKey + " | terrain=" + desiredTerrain + " | probes=" + probes + " | terrainReject=" + terrainRejected + " | spaceReject=" + spaceRejected + " | success=true",
                        profileStart,
                        2.00f);
                    return true;
                }
            }

            EndTimingScope(
                "TryFindLegacyCandidate",
                "item=" + itemKey + " | terrain=" + desiredTerrain + " | probes=" + probes + " | terrainReject=" + terrainRejected + " | spaceReject=" + spaceRejected + " | success=false",
                profileStart,
                2.00f);
            return false;
        }

        private bool TryFindDirectNavalCandidate(
            string itemKey,
            IA_ZoneType zone,
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            out Vector3 candidate,
            out string reason)
        {
            long profileStart = BeginTimingScope();
            candidate = anchor;
            reason = "nenhum ponto naval direto";

            string disabledReason;
            if (IsHeavyAutomaticNavalItem(itemKey)
                && IsNavalAutoPlacementDisabledForItem(itemKey, out disabledReason)
                && !HasManualBuildOverrideForItem(itemKey))
            {
                reason = disabledReason;
                EndTimingScope("TryFindDirectNavalCandidate", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.prefabDaUnidade == null)
            {
                reason = "item naval nao encontrado";
                EndTimingScope("TryFindDirectNavalCandidate", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            float now = Time.time;
            if (!(_context.Brain != null
                && _context.Brain.IsBootstrapActive
                && _context.Brain.BootstrapStage == IA_BrainMaster.IA_BootstrapStage.BuildShipyard)
                && IsHeavyAutomaticNavalItem(itemKey))
            {
                _nextRareNavalExpansionWindowTime = Mathf.Max(_nextRareNavalExpansionWindowTime, now + 90f);
            }

            string backoffReason;
            if (TryGetNavalSearchBackoff(itemKey, anchor, now, out backoffReason))
            {
                reason = backoffReason;
                EndTimingScope("TryFindDirectNavalCandidate", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 2.00f);
                return false;
            }

            bool requiresCoast = NavalPlacementResolver.RequiresCoastalPlacement(data.prefabDaUnidade);
            var searchAnchors = new List<Vector3>();
            TryAddNavalSearchAnchor(searchAnchors, anchor, requiresCoast);

            for (int i = 0; i < _context.WorldState.OwnStructures.Count && searchAnchors.Count < 2; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                TryAddNavalSearchAnchor(searchAnchors, structure.transform.position, requiresCoast);

                if (searchAnchors.Count >= 2)
                {
                    break;
                }
            }

            if (searchAnchors.Count == 0)
            {
                reason = requiresCoast ? "sem ancora costeira viavel" : "sem ancora naval viavel";
                MarkNavalSearchBackoff(itemKey, anchor, reason, now);
                EndTimingScope(
                    "TryFindDirectNavalCandidate",
                    "item=" + itemKey + " | anchors=0 | success=false | reason=" + reason,
                    profileStart,
                    2.25f);
                return false;
            }

            for (int i = 0; i < searchAnchors.Count; i++)
            {
                Vector3 searchAnchor = searchAnchors[i];
                if (TryFindDirectNavalCandidateFromAnchor(itemKey, data, zone, searchAnchor, minRadius, maxRadius, out candidate, out reason))
                {
                    ClearNavalSearchBackoff(itemKey, anchor);
                    EndTimingScope(
                        "TryFindDirectNavalCandidate",
                        "item=" + itemKey + " | anchors=" + searchAnchors.Count + " | success=true",
                        profileStart,
                        2.25f);
                    return true;
                }
            }

            MarkNavalSearchBackoff(itemKey, anchor, reason, now);
            EndTimingScope(
                "TryFindDirectNavalCandidate",
                "item=" + itemKey + " | anchors=" + searchAnchors.Count + " | success=false | reason=" + reason,
                profileStart,
                2.25f);
            return false;
        }

        private bool TryFindDirectNavalCandidateFromAnchor(
            string itemKey,
            DadosConstrucao data,
            IA_ZoneType zone,
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            out Vector3 candidate,
            out string reason)
        {
            candidate = anchor;
            reason = "sem ponto naval direto";

            bool requiresCoast = NavalPlacementResolver.RequiresCoastalPlacement(data.prefabDaUnidade);
            float startRadius = Mathf.Max(0f, minRadius);
            float endRadius = Mathf.Max(startRadius + 24f, maxRadius);
            
            // REDUZIDO: Limite agressivo de anéis para mitigar lags
            int rings = requiresCoast
                ? Mathf.Clamp(Mathf.CeilToInt((endRadius - startRadius) / 250f) + 1, 4, 10)
                : Mathf.Clamp(Mathf.CeilToInt((endRadius - startRadius) / 300f) + 1, 3, 8);
            float coastRadiusStep = Mathf.Clamp((endRadius - startRadius) / 10f, 40f, 95f);

            for (int ring = 0; ring < rings; ring++)
            {
                float t = rings <= 1 ? 0f : ring / (float)(rings - 1);
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                
                // REDUZIDO: Menos subdivisões angulares de busca para evitar 2000ms+ de CPU
                int samplesPerRing = radius <= 0.01f
                    ? 1
                    : (requiresCoast
                        ? (radius < 400f ? 8 : 12)
                        : (radius < 400f ? 6 : 9));

                for (int i = 0; i < samplesPerRing; i++)
                {
                    float angleDeg = ((360f / samplesPerRing) * i) + (ring * 11f);
                    float angle = angleDeg * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    
                    // REDUZIDO: de 3 passos radiais concêntricos para apenas 1 por ângulo
                    int passes = 1;
                    for (int pass = 0; pass < passes; pass++)
                    {
                        float radiusOffset = 0f;
                        if (passes > 1)
                        {
                            if (pass == 1)
                            {
                                radiusOffset = -Mathf.Min(coastRadiusStep, radius * 0.35f);
                            }
                            else if (pass == 2)
                            {
                                radiusOffset = Mathf.Min(coastRadiusStep, Mathf.Max(24f, (endRadius - startRadius) * 0.08f));
                            }
                        }

                        float sampleRadius = Mathf.Clamp(radius + radiusOffset, startRadius, endRadius);
                        Vector3 probe = anchor + (direction * sampleRadius);
                        Vector3 placement = probe;

                        if (requiresCoast)
                        {
                            Vector3 forward = placement - anchor;
                            if (forward.sqrMagnitude < 0.01f)
                            {
                                forward = direction.sqrMagnitude < 0.01f ? Vector3.forward : direction;
                            }

                            NavalPlacementResolver.StructurePose pose;
                            Quaternion fallbackRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                            if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, probe, fallbackRotation, out pose))
                            {
                                reason = pose.Reason;
                                continue;
                            }

                            placement = pose.Position;
                        }
                        else
                        {
                            // SOLICITADO: Nao busca mais o WaterSpawn, o estaleiro pode ficar na terra e soltar os navios.
                            placement = probe;
                        }

                        if (!_context.Backend.BuildService.ValidateTerritoryProbe(itemKey, placement, out reason))
                        {
                            continue;
                        }

                        if (_context.Backend.BuildService.ValidatePlacement(
                            itemKey,
                            placement,
                            zone,
                            _context.WorldState,
                            _context.MapAnalyzer,
                            _context.ThreatAnalyzer,
                            out reason))
                        {
                            candidate = placement;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool LegacySpaceFree(Vector3 position, float radius)
        {
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                float required = radius + (EstimateLegacySafeRadius(structure.name) * 0.8f);
                if (Vector3.Distance(Flatten(position), Flatten(structure.transform.position)) < required)
                {
                    return false;
                }
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(10f, radius * 0.75f),
                _legacySpaceHits,
                ~0,
                QueryTriggerInteraction.Collide);

            if (hitCount >= _legacySpaceHits.Length)
            {
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _legacySpaceHits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                string n = IA_Text.Normalize(hit.name);
                if (n.Contains("terrain")
                    || n.Contains("terra")
                    || n.Contains("agua")
                    || n.Contains("water")
                    || n.Contains("ocean")
                    || n.Contains("sea")
                    || n.Contains("mar")
                    || n.Contains("oceano")
                    || n.Contains("suimono")
                    || n.Contains("shore"))
                {
                    continue;
                }

                if (hit.GetComponentInParent<IdentidadeUnidade>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LegacyGroundReachable(Vector3 position)
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(position, out hit, 18f, NavMesh.AllAreas);
        }

        private static float EstimateLegacySafeRadius(string key)
        {
            string normalized = IA_Text.Normalize(key);
            if (normalized.Contains("aeroporto") || normalized.Contains("hangar"))
            {
                return 180f;
            }

            if (normalized.Contains("prefeitura") || normalized.Contains("complexo"))
            {
                return 80f;
            }

            if (normalized.Contains("estaleiro") || normalized.Contains("pier") || normalized.Contains("plataforma"))
            {
                return 75f;
            }

            if (normalized.Contains("fabrica") || normalized.Contains("construtor"))
            {
                return 70f;
            }

            if (normalized.Contains("quartel") || normalized.Contains("tenda") || normalized.Contains("barraca"))
            {
                return 48f;
            }

            if (normalized.Contains("refinaria") || normalized.Contains("mina") || normalized.Contains("petroleo") || normalized.Contains("armazem"))
            {
                return 45f;
            }

            if (normalized.Contains("radar"))
            {
                return 40f;
            }

            if (normalized.Contains("torreta") || normalized.Contains("sentinela") || normalized.Contains("defesa") || normalized.Contains("canhao"))
            {
                return 30f;
            }

            if (normalized.Contains("antiaerea") || normalized.Contains("ares") || normalized.Contains("ciws"))
            {
                return 34f;
            }

            return 35f;
        }

        private bool TryResolveBuildPose(string itemKey, ref Vector3 position, ref Quaternion rotation, out string reason)
        {
            long profileStart = BeginTimingScope();
            reason = string.Empty;

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.prefabDaUnidade == null)
            {
                reason = "item nao encontrado";
                EndTimingScope("TryResolveBuildPose", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 1.50f);
                return false;
            }

            if (!NavalPlacementResolver.RequiresCoastalPlacement(data.prefabDaUnidade))
            {
                EndTimingScope("TryResolveBuildPose", "item=" + itemKey + " | success=true | coastal=false", profileStart, 1.50f);
                return true;
            }

            NavalPlacementResolver.StructurePose pose;
            if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, position, rotation, out pose))
            {
                reason = string.IsNullOrEmpty(pose.Reason) ? "costa invalida" : pose.Reason;
                EndTimingScope("TryResolveBuildPose", "item=" + itemKey + " | success=false | reason=" + reason, profileStart, 1.50f);
                return false;
            }

            position = pose.Position;
            rotation = pose.Rotation;
            EndTimingScope("TryResolveBuildPose", "item=" + itemKey + " | success=true | coastal=true", profileStart, 1.50f);
            return true;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private Vector3 ResolveStrategicReference(Vector3 fallback)
        {
            if (_context.WorldState != null && _context.WorldState.BaseCenter != Vector3.zero)
            {
                return _context.WorldState.BaseCenter;
            }

            if (_context.Brain != null)
            {
                return _context.Brain.transform.position;
            }

            return fallback;
        }

        private List<Vector3> BuildBootstrapLandAnchors(Vector3 primaryAnchor)
        {
            long profileStart = BeginTimingScope();
            var anchors = new List<Vector3>();
            AddSearchAnchor(anchors, EnsureDryLandAnchor(primaryAnchor));
            AddSearchAnchor(anchors, EnsureDryLandAnchor(_context.WorldState != null ? _context.WorldState.BaseCenter : Vector3.zero));
            AddSearchAnchor(anchors, EnsureDryLandAnchor(_context.Brain != null ? _context.Brain.transform.position : Vector3.zero));

            Vector3 territoryLandAnchor;
            Vector3 territoryCoastAnchor;
            if (TryResolveFriendlyTerritorySurfaceAnchors(out territoryLandAnchor, out territoryCoastAnchor))
            {
                AddSearchAnchor(anchors, EnsureDryLandAnchor(territoryLandAnchor));
                AddSearchAnchor(anchors, EnsureDryLandAnchor(territoryCoastAnchor));
            }

            for (int i = 0; i < _context.WorldState.OwnStructures.Count && anchors.Count < 8; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(structure.name);
                if (!normalized.Contains("prefeitura")
                    && !normalized.Contains("governo")
                    && !normalized.Contains("capital")
                    && !normalized.Contains("bandeira")
                    && !normalized.Contains("flag")
                    && !normalized.Contains("quartel")
                    && !normalized.Contains("armazem")
                    && structure.GetComponentInChildren<MarcadorTerritorio>() == null)
                {
                    continue;
                }

                AddSearchAnchor(anchors, EnsureDryLandAnchor(structure.transform.position));
            }

            EndTimingScope("BuildBootstrapLandAnchors", "anchors=" + anchors.Count + " | structures=" + _context.WorldState.OwnStructures.Count, profileStart, 1.25f);
            return anchors;
        }

        private bool TryResolveFriendlyTerritorySurfaceAnchors(out Vector3 landAnchor, out Vector3 coastAnchor)
        {
            int structureCount = _context.WorldState != null ? _context.WorldState.OwnStructures.Count : 0;
            float now = Time.time;
            if (_cachedTerritoryStructureCount == structureCount
                && now < _cachedTerritoryAnchorsUntil)
            {
                landAnchor = _cachedTerritoryLandAnchor;
                coastAnchor = _cachedTerritoryCoastAnchor;
                return _cachedTerritoryAnchorResolved;
            }

            long profileStart = BeginTimingScope();
            landAnchor = Vector3.zero;
            coastAnchor = Vector3.zero;

            Vector3 reference = ResolveStrategicReference(Vector3.zero);
            bool foundLand = false;
            bool foundCoast = false;
            float bestLandScore = float.MinValue;
            float bestCoastScore = float.MinValue;

            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                MarcadorTerritorio marker = structure.GetComponentInChildren<MarcadorTerritorio>();
                bool isGovernment = structure.GetComponent<ComplexoGovernamental>() != null;
                if (marker == null && !isGovernment)
                {
                    string normalized = IA_Text.Normalize(structure.name);
                    isGovernment = normalized.Contains("prefeitura") || normalized.Contains("governo") || normalized.Contains("capital");
                    if (!isGovernment)
                    {
                        continue;
                    }
                }

                Vector3 center = EnsureDryLandAnchor(structure.transform.position);
                float markerRadius = marker != null ? marker.raioDeDominio : (isGovernment ? 300f : 140f);
                float bonus = marker != null && marker.ehPrefeitura ? 160f : (isGovernment ? 120f : 0f);

                if (IsDryLandAnchor(center))
                {
                    float landScore = bonus - Vector3.Distance(Flatten(center), Flatten(reference)) * 0.15f;
                    if (!foundLand || landScore > bestLandScore)
                    {
                        foundLand = true;
                        bestLandScore = landScore;
                        landAnchor = center;
                    }
                }

                Vector3 waterCandidate = _context.MapAnalyzer.FindPointInTerrain(
                    center,
                    IA_TerrainType.Water,
                    Mathf.Max(20f, markerRadius * 0.45f),
                    Mathf.Max(220f, markerRadius + 260f),
                    16);
                if (waterCandidate != center)
                {
                    float coastScore;
                    if (TryScoreCoastalAnchorCandidate(reference, waterCandidate, waterCandidate - center, out coastScore))
                    {
                        coastScore += bonus;
                        if (!foundCoast || coastScore > bestCoastScore)
                        {
                            foundCoast = true;
                            bestCoastScore = coastScore;
                            coastAnchor = waterCandidate;
                        }
                    }
                }
            }

            bool resolved = foundLand || foundCoast;
            _cachedTerritoryStructureCount = structureCount;
            _cachedTerritoryLandAnchor = landAnchor;
            _cachedTerritoryCoastAnchor = coastAnchor;
            _cachedTerritoryAnchorResolved = resolved;
            _cachedTerritoryAnchorsUntil = now + (resolved ? 4f : 1.5f);

            EndTimingScope(
                "TryResolveFriendlyTerritorySurfaceAnchors",
                "structures=" + structureCount + " | foundLand=" + foundLand + " | foundCoast=" + foundCoast,
                profileStart,
                1.50f);
            return resolved;
        }

        private bool TryResolveFriendlyTerritoryCoastalAnchor(Vector3 reference, out Vector3 coastalAnchor)
        {
            long profileStart = BeginTimingScope();
            coastalAnchor = Vector3.zero;
            Vector3 landAnchor;
            bool foundAny = TryResolveFriendlyTerritorySurfaceAnchors(out landAnchor, out coastalAnchor) && coastalAnchor != Vector3.zero;
            if (!foundAny)
            {
                EndTimingScope("TryResolveFriendlyTerritoryCoastalAnchor", "found=false", profileStart, 1.50f);
                return false;
            }

            Vector3 bestAnchor = coastalAnchor;
            float bestScore = float.MinValue;
            bool foundBetter = false;
            List<Vector3> anchors = BuildBootstrapLandAnchors(reference != Vector3.zero ? reference : landAnchor);
            int anchorsTried = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 candidate;
                if (!(TryFindDirectCoastalAnchor(anchors[i], out candidate) || TryFindCoastalAnchor(anchors[i], out candidate)))
                {
                    continue;
                }

                anchorsTried++;
                float score;
                if (!TryScoreCoastalAnchorCandidate(reference != Vector3.zero ? reference : landAnchor, candidate, candidate - anchors[i], out score))
                {
                    continue;
                }

                if (!foundBetter || score > bestScore)
                {
                    foundBetter = true;
                    bestScore = score;
                    bestAnchor = candidate;
                }
            }

            coastalAnchor = foundBetter ? bestAnchor : coastalAnchor;
            bool resolved = coastalAnchor != Vector3.zero;
            EndTimingScope(
                "TryResolveFriendlyTerritoryCoastalAnchor",
                "anchors=" + anchors.Count + " | tried=" + anchorsTried + " | resolved=" + resolved,
                profileStart,
                2.00f);
            return resolved;
        }

        private Vector3 EnsureDryLandAnchor(Vector3 anchor)
        {
            if (anchor == Vector3.zero)
            {
                return anchor;
            }

            if (TryProjectToDryLand(anchor, out Vector3 projected))
            {
                return projected;
            }

            float[] radii = { 18f, 36f, 60f, 90f, 130f, 180f, 260f, 380f, 520f };
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                int samples = radius < 70f ? 12 : 20;
                for (int i = 0; i < samples; i++)
                {
                    float angle = ((360f / samples) * i) * Mathf.Deg2Rad;
                    Vector3 probe = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (TryProjectToDryLand(probe, out projected))
                    {
                        return projected;
                    }
                }
            }

            Vector3 fallbackLand = _context.MapAnalyzer.FindPointInTerrain(anchor, IA_TerrainType.Land, 18f, 520f, 16);
            if (TryProjectToDryLand(fallbackLand, out projected))
            {
                return projected;
            }

            return anchor;
        }

        private bool TryProjectToDryLand(Vector3 probe, out Vector3 projected)
        {
            projected = probe;

            ClassificacaoSuperficieMapa surface;
            float surfaceHeight;
            if (RegistroSuperficieMapa.TryClassify(probe, out surface, out surfaceHeight))
            {
                if (surface == ClassificacaoSuperficieMapa.Chao)
                {
                    projected = new Vector3(probe.x, surfaceHeight, probe.z);
                    return true;
                }

                if (surface == ClassificacaoSuperficieMapa.Agua || surface == ClassificacaoSuperficieMapa.Costa)
                {
                    return false;
                }
            }

            IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
            if (cell == null)
            {
                return false;
            }

            if (!cell.BuildableLand || cell.Terrain == IA_TerrainType.Water || cell.Terrain == IA_TerrainType.Coast)
            {
                return false;
            }

            projected = cell.Center;
            return true;
        }

        private bool IsDryLandAnchor(Vector3 probe)
        {
            return TryProjectToDryLand(probe, out _);
        }

        private bool TryConsiderCoastalAnchorCandidate(
            Vector3 reference,
            Vector3 candidate,
            Vector3 preferredForward,
            ref bool foundBest,
            ref float bestScore,
            ref Vector3 bestAnchor)
        {
            float score;
            if (!TryScoreCoastalAnchorCandidate(reference, candidate, preferredForward, out score))
            {
                return false;
            }

            if (!foundBest || score > bestScore)
            {
                foundBest = true;
                bestScore = score;
                bestAnchor = candidate;
            }

            return true;
        }

        private bool TryScoreCoastalAnchorCandidate(Vector3 reference, Vector3 candidate, Vector3 preferredForward, out float score)
        {
            score = float.MinValue;

            float edgeDistance = NavalPlacementResolver.DistanceToMapEdge(candidate);
            if (edgeDistance < NavalEdgeSafetyMargin)
            {
                return false;
            }

            preferredForward.y = 0f;
            if (preferredForward.sqrMagnitude < 0.01f)
            {
                preferredForward = candidate - reference;
            }

            if (preferredForward.sqrMagnitude < 0.01f)
            {
                preferredForward = Vector3.forward;
            }

            string corridorReason;
            if (!NavalPlacementResolver.HasSafeLaunchCorridor(candidate, preferredForward, 34f, 210f, NavalLaunchSafetyMargin, out corridorReason))
            {
                return false;
            }

            float distanceFromReference = Vector3.Distance(Flatten(candidate), Flatten(reference));
            score = (Mathf.Min(260f, edgeDistance) * 0.9f) - (distanceFromReference * 0.35f);
            return true;
        }

        private int CountStructures(params string[] hints)
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(structure.name);
                bool match = false;
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && name.Contains(hint))
                    {
                        match = true;
                        break;
                    }
                }

                if (match)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountApproxCombatUnits(float now)
        {
            int unitCount = _context.WorldState.OwnUnits.Count;
            if (_cachedApproxCombatUnitCount >= 0
                && _cachedApproxCombatSourceCount == unitCount
                && now < _cachedApproxCombatUnitUntil)
            {
                return _cachedApproxCombatUnitCount;
            }

            long profileStart = BeginTimingScope();
            int count = 0;
            for (int i = 0; i < unitCount; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                bool isGroundTransport = unit.GetComponent<TransporteTerrestre>() != null
                                         || name.Contains("truck")
                                         || name.Contains("caminhao")
                                         || (name.Contains("transporte")
                                             && !name.Contains("aereo")
                                             && !name.Contains("aviao")
                                             && !name.Contains("jet")
                                             && !name.Contains("heli")
                                             && !name.Contains("ray")
                                             && !name.Contains("vans")
                                             && !name.Contains("navio")
                                             && !name.Contains("barco")
                                             && !name.Contains("lancha"));
                if (isGroundTransport)
                {
                    continue;
                }

                bool looksCombat =
                    name.Contains("sold")
                    || name.Contains("rifle")
                    || name.Contains("infan")
                    || name.Contains("tank")
                    || name.Contains("mbt")
                    || name.Contains("south")
                    || name.Contains("arthur")
                    || name.Contains("c1")
                    || name.Contains("leonc")
                    || name.Contains("hack")
                    || name.Contains("artilh")
                    || name.Contains("hover")
                    || name.Contains("navio")
                    || name.Contains("corveta")
                    || name.Contains("destroy")
                    || name.Contains("sub")
                    || name.Contains("mako")
                    || name.Contains("wraith")
                    || name.Contains("leviathan")
                    || name.Contains("arrowhead")
                    || name.Contains("lancha")
                    || name.Contains("ww")
                    || name.Contains("fa1")
                    || name.Contains("caca")
                    || name.Contains("aviao")
                    || name.Contains("jet")
                    || name.Contains("heli")
                    || name.Contains("ray")
                    || name.Contains("vans");

                if (looksCombat)
                {
                    count++;
                }
            }

            _cachedApproxCombatUnitCount = count;
            _cachedApproxCombatSourceCount = unitCount;
            _cachedApproxCombatUnitUntil = now + 1.5f;
            EndTimingScope("CountApproxCombatUnits", "units=" + unitCount + " | combat=" + count, profileStart, 1.00f);
            return count;
        }

        private bool TryFindCoastalAnchor(Vector3 center, out Vector3 coastalAnchor)
        {
            float now = Time.time;
            bool cachedResolved;
            if (TryGetCachedCoastalAnchor("broad", center, now, out coastalAnchor, out cachedResolved))
            {
                return cachedResolved;
            }

            long profileStart = BeginTimingScope();
            var watchdog = System.Diagnostics.Stopwatch.StartNew();
            const float MaxBudgetMs = 25f; // Timeout rígido — jamais deve travar mais do que isso

            coastalAnchor = center;
            DadosConstrucao coastalStructure = _context.Backend.FindFirstAvailable("estaleiros navais", "estaleiro naval", "estaleiro", "estaleiros", "pier");
            string coastalKey = coastalStructure != null && coastalStructure.prefabDaUnidade != null
                ? (string.IsNullOrEmpty(coastalStructure.nomeItem) ? coastalStructure.prefabDaUnidade.name : coastalStructure.nomeItem)
                : "estaleiro naval";
            Vector3 reference = ResolveStrategicReference(center);
            bool foundBest = false;
            float bestScore = float.MinValue;
            int sampledWaterPoints = 0;
            int territoryApproved = 0;
            // Reduzido de 6 raios x 16 amostras para 4 raios x 12 amostras
            float[] radii = { 120f, 280f, 480f, 680f };
            for (int r = 0; r < radii.Length; r++)
            {
                if (watchdog.Elapsed.TotalMilliseconds > MaxBudgetMs) { break; }
                float radius = radii[r];
                for (int i = 0; i < 12; i++)
                {
                    float angle = (360f / 12f) * i * Mathf.Deg2Rad;
                    Vector3 probe = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                    if (cell != null
                        && (cell.Terrain == IA_TerrainType.Coast || cell.Terrain == IA_TerrainType.Water))
                    {
                        sampledWaterPoints++;
                        Vector3 candidatePosition = new Vector3(probe.x, cell.Height, probe.z);
                        string territoryReason;
                        if (_context.Backend.BuildService.ValidateTerritoryProbe(coastalKey, candidatePosition, out territoryReason))
                        {
                            territoryApproved++;
                            TryConsiderCoastalAnchorCandidate(reference, candidatePosition, candidatePosition - center, ref foundBest, ref bestScore, ref coastalAnchor);
                        }
                    }
                }
            }

            Vector3 directAnchor;
            if (!foundBest && watchdog.Elapsed.TotalMilliseconds < MaxBudgetMs && TryFindDirectCoastalAnchor(center, out directAnchor))
            {
                TryConsiderCoastalAnchorCandidate(reference, directAnchor, directAnchor - center, ref foundBest, ref bestScore, ref coastalAnchor);
            }

            // Scan de estruturas limitado a 5 estruturas e 3 anéis x 12 amostras (era 4 anéis x 20 x N estruturas)
            int structureLimit = Mathf.Min(_context.WorldState.OwnStructures.Count, 5);
            for (int i = 0; i < structureLimit; i++)
            {
                if (watchdog.Elapsed.TotalMilliseconds > MaxBudgetMs) { break; }
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null) { continue; }

                Vector3 probeCenter = structure.transform.position;
                for (int ring = 0; ring < 3; ring++)
                {
                    if (watchdog.Elapsed.TotalMilliseconds > MaxBudgetMs) { break; }
                    float radius = 180f + (ring * 200f);
                    for (int sample = 0; sample < 12; sample++)
                    {
                        float angle = ((360f / 12f) * sample + (ring * 15f)) * Mathf.Deg2Rad;
                        Vector3 probe = probeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                        if (cell != null
                            && (cell.Terrain == IA_TerrainType.Coast || cell.Terrain == IA_TerrainType.Water))
                        {
                            sampledWaterPoints++;
                            Vector3 candidatePosition = new Vector3(probe.x, cell.Height, probe.z);
                            string territoryReason;
                            if (_context.Backend.BuildService.ValidateTerritoryProbe(coastalKey, candidatePosition, out territoryReason))
                            {
                                territoryApproved++;
                                TryConsiderCoastalAnchorCandidate(reference, candidatePosition, candidatePosition - probeCenter, ref foundBest, ref bestScore, ref coastalAnchor);
                            }
                        }
                    }
                }
            }

            CacheCoastalAnchorResult("broad", center, foundBest, coastalAnchor, now);
            EndTimingScope(
                "TryFindCoastalAnchor",
                "sampled=" + sampledWaterPoints + " | territoryOk=" + territoryApproved + " | found=" + foundBest,
                profileStart,
                2.25f);
            return foundBest;
        }

        private bool TryFindDirectCoastalAnchor(Vector3 center, out Vector3 coastalAnchor)
        {
            float now = Time.time;
            bool cachedResolved;
            if (TryGetCachedCoastalAnchor("direct", center, now, out coastalAnchor, out cachedResolved))
            {
                return cachedResolved;
            }

            long profileStart = BeginTimingScope();
            var watchdog = System.Diagnostics.Stopwatch.StartNew();
            const float MaxBudgetMs = 30f; // Timeout rígido — jamais deve travar mais do que isso

            coastalAnchor = center;

            DadosConstrucao coastalStructure = _context.Backend.FindFirstAvailable("estaleiros navais", "estaleiro naval", "estaleiro", "estaleiros", "pier");
            if (coastalStructure == null || coastalStructure.prefabDaUnidade == null)
            {
                EndTimingScope("TryFindDirectCoastalAnchor", "itemMissing=true", profileStart, 2.00f);
                return false;
            }

            Vector3 reference = ResolveStrategicReference(center);
            bool foundBest = false;
            float bestScore = float.MinValue;
            int poseResolved = 0;
            int territoryApproved = 0;
            // Reduzido de 8 anéis x 16 amostras para 5 anéis x 8 amostras — de 96 para 40 poses
            const float maxSearchRadius = 900f;
            const int rings = 5;
            for (int r = 0; r < rings; r++)
            {
                if (watchdog.Elapsed.TotalMilliseconds > MaxBudgetMs) { break; }
                float radius = rings <= 1 ? 0f : Mathf.Lerp(0f, maxSearchRadius, r / (float)(rings - 1));
                int samples = radius <= 0.01f ? 1 : 8;
                for (int i = 0; i < samples; i++)
                {
                    if (watchdog.Elapsed.TotalMilliseconds > MaxBudgetMs) { break; }
                    Vector3 probe = center;
                    if (radius > 0.01f)
                    {
                        float angle = (((360f / samples) * i) + (r * 13f)) * Mathf.Deg2Rad;
                        probe += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    }

                    NavalPlacementResolver.StructurePose pose;
                    Vector3 forward = probe - center;
                    if (forward.sqrMagnitude < 0.01f)
                    {
                        forward = Vector3.forward;
                    }

                    Quaternion fallbackRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                    if (NavalPlacementResolver.TryResolveStructurePose(coastalStructure.prefabDaUnidade, probe, fallbackRotation, out pose))
                    {
                        poseResolved++;
                        string territoryReason;
                        string probeKey = string.IsNullOrEmpty(coastalStructure.nomeItem) ? coastalStructure.prefabDaUnidade.name : coastalStructure.nomeItem;
                        if (_context.Backend.BuildService.ValidateTerritoryProbe(probeKey, pose.Position, out territoryReason))
                        {
                            territoryApproved++;
                            TryConsiderCoastalAnchorCandidate(reference, pose.Position, pose.Rotation * Vector3.forward, ref foundBest, ref bestScore, ref coastalAnchor);
                        }
                    }
                }
            }

            CacheCoastalAnchorResult("direct", center, foundBest, coastalAnchor, now);
            EndTimingScope(
                "TryFindDirectCoastalAnchor",
                "poseOk=" + poseResolved + " | territoryOk=" + territoryApproved + " | found=" + foundBest,
                profileStart,
                2.25f);
            return foundBest;
        }

        private bool TryAddNavalSearchAnchor(List<Vector3> anchors, Vector3 value, bool requiresCoast)
        {
            if (value == Vector3.zero)
            {
                return false;
            }

            if (!requiresCoast || HasWaterAround(value))
            {
                AddSearchAnchor(anchors, value);
                return true;
            }

            Vector3 coastalAnchor;
            if (TryFindDirectCoastalAnchor(value, out coastalAnchor) || TryFindCoastalAnchor(value, out coastalAnchor))
            {
                AddSearchAnchor(anchors, coastalAnchor);
                return true;
            }

            return false;
        }

        private static void AddSearchAnchor(List<Vector3> anchors, Vector3 value)
        {
            if (value == Vector3.zero)
            {
                return;
            }

            Vector3 flatValue = Flatten(value);
            for (int i = 0; i < anchors.Count; i++)
            {
                if (Vector3.Distance(Flatten(anchors[i]), flatValue) <= 20f)
                {
                    return;
                }
            }

            anchors.Add(value);
        }

        private void AddBootstrapNavalSearchAnchors(List<Vector3> anchors, Vector3 center)
        {
            if (center == Vector3.zero)
            {
                return;
            }

            Vector3 coastalAnchor;
            if (TryFindDirectCoastalAnchor(center, out coastalAnchor))
            {
                AddSearchAnchor(anchors, coastalAnchor);
            }

            Vector3 waterAnchor;
            if (TryFindWideWaterSearchAnchor(center, 30f, 520f, out waterAnchor))
            {
                AddSearchAnchor(anchors, waterAnchor);
            }
        }

        private bool TryFindWideWaterSearchAnchor(Vector3 center, float minRadius, float maxRadius, out Vector3 waterAnchor)
        {
            waterAnchor = center;
            float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
            int rings = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(maxRadius, minRadius + 24f) - Mathf.Max(0f, minRadius)) / 220f) + 2, 4, 14);

            for (int ring = 0; ring < rings; ring++)
            {
                float t = rings <= 1 ? 0f : ring / (float)(rings - 1);
                float radius = Mathf.Lerp(Mathf.Max(0f, minRadius), Mathf.Max(maxRadius, minRadius + 24f), t);
                int samples = radius <= 0.01f ? 1 : (radius < 280f ? 10 : (radius < 1200f ? 14 : 18));
                for (int i = 0; i < samples; i++)
                {
                    float angle = (((360f / samples) * i) + (ring * 9f)) * Mathf.Deg2Rad;
                    Vector3 probe = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    probe.y = seaLevel;
                    if (NavalPlacementResolver.IsWaterAtPosition(probe, seaLevel))
                    {
                        waterAnchor = probe;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ShouldUseDeepLandFallback(string itemKey)
        {
            if (_context.WorldState.OwnStructures.Count == 0 || _recoveryLevel > 0)
            {
                return true;
            }

            string normalized = IA_Text.Normalize(itemKey);
            return normalized.Contains("prefeitura")
                   || normalized.Contains("governo")
                   || normalized.Contains("capital")
                   || normalized.Contains("aeroporto")
                   || normalized.Contains("airport")
                   || normalized.Contains("base aerea")
                   || normalized.Contains("quartel general")
                   || normalized.Contains("quartel_general")
                   || normalized == "hq";
        }

        private bool CanTryBuildItem(string itemKey, float now)
        {
            string normalized = IA_Text.Normalize(itemKey);
            bool bootstrapActive = _context != null && _context.Brain != null && _context.Brain.IsBootstrapActive;
            if (!bootstrapActive
                && ShouldRespectRuntimeLock()
                && DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                && IsHeavyNonEssentialRuntimeBuild(normalized))
            {
                return false;
            }

            float cooldownUntil;
            if (_missingItemCooldownUntil.TryGetValue(normalized, out cooldownUntil) && cooldownUntil > now)
            {
                return false;
            }

            DadosConstrucao data;
            if (_context.Backend.TryResolveItem(itemKey, out data) && data != null)
            {
                _missingItemCooldownUntil.Remove(normalized);
                return true;
            }

            _missingItemCooldownUntil[normalized] = now + 60f;
            return false;
        }

        private static bool IsHeavyNonEssentialRuntimeBuild(string normalizedItemKey)
        {
            if (string.IsNullOrEmpty(normalizedItemKey))
            {
                return false;
            }

            return normalizedItemKey.Contains("quartel general")
                   || normalizedItemKey.Contains("quartel_general")
                   || normalizedItemKey == "hq"
                   || normalizedItemKey.Contains("radar")
                   || normalizedItemKey.Contains("plataforma")
                   || normalizedItemKey.Contains("fabrica")
                   || normalizedItemKey.Contains("aeroporto")
                   || normalizedItemKey.Contains("airport")
                   || normalizedItemKey.Contains("heliporto")
                   || normalizedItemKey.Contains("armazem")
                   || normalizedItemKey.Contains("estaleiro")
                   || normalizedItemKey.Contains("pier");
        }

        private static bool ShouldRespectRuntimeLock()
        {
            return Application.isPlaying && Time.timeSinceLevelLoad >= 20f;
        }

        private void LogVerboseWarning(string key, string message, float cooldownSeconds)
        {
            if (_context.Brain == null || !_context.Brain.EnableVerboseLogs)
            {
                return;
            }

            float now = Time.time;
            string normalizedKey = IA_Text.Normalize(key);
            float cooldownUntil;
            if (_warningCooldownUntil.TryGetValue(normalizedKey, out cooldownUntil) && cooldownUntil > now)
            {
                return;
            }

            _warningCooldownUntil[normalizedKey] = now + Mathf.Max(2f, cooldownSeconds);
            if (!Application.isEditor)
            {
                Debug.LogWarning(message);
            }
        }

        private bool CanRetryPlacementSearch(string itemKey, IA_TerrainType desiredTerrain, float now)
        {
            string retryKey = BuildPlacementRetryKey(itemKey, desiredTerrain);
            float cooldownUntil;
            return !_placementRetryCooldownUntil.TryGetValue(retryKey, out cooldownUntil) || cooldownUntil <= now;
        }

        private void MarkPlacementSearchFailure(string itemKey, IA_TerrainType desiredTerrain, float now)
        {
            string retryKey = BuildPlacementRetryKey(itemKey, desiredTerrain);
            int streak = 0;
            _placementFailureStreakByKey.TryGetValue(retryKey, out streak);
            streak++;
            _placementFailureStreakByKey[retryKey] = streak;

            float retryDelay = desiredTerrain == IA_TerrainType.Water
                ? ResolveEscalatedRetryDelay(streak)
                : 7f;
            _placementRetryCooldownUntil[retryKey] = now + retryDelay;
        }

        private void ClearPlacementSearchFailure(string itemKey, IA_TerrainType desiredTerrain)
        {
            string retryKey = BuildPlacementRetryKey(itemKey, desiredTerrain);
            _placementRetryCooldownUntil.Remove(retryKey);
            _placementFailureStreakByKey.Remove(retryKey);
        }

        private static string BuildPlacementRetryKey(string itemKey, IA_TerrainType desiredTerrain)
        {
            return IA_Text.Normalize(itemKey) + ":" + desiredTerrain;
        }

        private bool TryGetCachedCoastalAnchor(string scope, Vector3 center, float now, out Vector3 coastalAnchor, out bool resolved)
        {
            string key = scope + ":" + BuildAnchorCellKey(center);
            CoastalAnchorCacheEntry entry;
            if (_coastalAnchorCache.TryGetValue(key, out entry))
            {
                if (entry.ValidUntil > now)
                {
                    coastalAnchor = entry.Anchor;
                    resolved = entry.Resolved;
                    return true;
                }

                _coastalAnchorCache.Remove(key);
            }

            coastalAnchor = center;
            resolved = false;
            return false;
        }

        private void CacheCoastalAnchorResult(string scope, Vector3 center, bool resolved, Vector3 coastalAnchor, float now)
        {
            float ttl = resolved
                ? 9f
                : (scope == "direct" ? 18f : 24f);
            _coastalAnchorCache[scope + ":" + BuildAnchorCellKey(center)] = new CoastalAnchorCacheEntry
            {
                Resolved = resolved,
                Anchor = resolved ? coastalAnchor : center,
                ValidUntil = now + ttl
            };
        }

        private bool TryGetNavalSearchBackoff(string itemKey, Vector3 anchor, float now, out string reason)
        {
            string normalizedItem = IA_Text.Normalize(itemKey);
            if (TryGetNavalSearchBackoffInternal(normalizedItem + ":global", now, out reason))
            {
                return true;
            }

            return TryGetNavalSearchBackoffInternal(normalizedItem + ":" + BuildAnchorCellKey(anchor), now, out reason);
        }

        private bool TryGetNavalSearchBackoffInternal(string key, float now, out string reason)
        {
            NavalSearchBackoffEntry entry;
            if (_navalSearchBackoffUntil.TryGetValue(key, out entry))
            {
                if (entry.ValidUntil > now)
                {
                    reason = string.IsNullOrEmpty(entry.Reason)
                        ? "falha naval recente em cooldown"
                        : "falha naval recente em cooldown | " + entry.Reason;
                    return true;
                }

                _navalSearchBackoffUntil.Remove(key);
            }

            reason = string.Empty;
            return false;
        }

        private void MarkNavalSearchBackoff(string itemKey, Vector3 anchor, string reason, float now)
        {
            string normalizedReason = IA_Text.Normalize(reason);
            float cooldownSeconds = ResolveNavalSearchBackoffSeconds(normalizedReason);
            string normalizedItem = IA_Text.Normalize(itemKey);
            string key = normalizedItem + ":" + (IsTerritoryNavalFailureReason(normalizedReason) ? "global" : BuildAnchorCellKey(anchor));
            int streak = 0;
            _navalFailureStreakByKey.TryGetValue(key, out streak);
            string lastReason;
            if (_navalFailureLastReasonByKey.TryGetValue(key, out lastReason) && lastReason == normalizedReason)
            {
                streak++;
            }
            else
            {
                streak = 1;
            }
            _navalFailureStreakByKey[key] = streak;
            _navalFailureLastReasonByKey[key] = normalizedReason;
            LastNavalGeometryFailureReason = normalizedReason;
            LastNavalGeometryFailureCount = streak;

            if (IsHeavyAutomaticNavalItem(itemKey)
                && IsGeometryNavalFailureReason(normalizedReason)
                && streak >= 3
                && !HasManualBuildOverrideForItem(itemKey))
            {
                string disableReason = "auto-placement naval desativado: " + reason;
                _navalAutoPlacementDisabledReasonByItem[normalizedItem] = disableReason;
                NavalAutoPlacementDisabled = true;
                NavalAutoPlacementDisabledReason = disableReason;
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("naval_auto_disabled_reason", disableReason);
                DiagnosticoDesempenhoJogo.RegistrarEvento("IA_BuildDirector", normalizedItem + " auto-placement desativado | motivo=" + reason);
            }

            float escalatedDelay = ResolveEscalatedRetryDelay(streak);
            if (cooldownSeconds > 0f)
            {
                escalatedDelay = Mathf.Max(escalatedDelay, cooldownSeconds);
            }

            if (escalatedDelay <= 0f)
            {
                return;
            }

            _navalSearchBackoffUntil[key] = new NavalSearchBackoffEntry
            {
                ValidUntil = now + escalatedDelay,
                Reason = reason
            };
            LastNavalFailureTime = now;
            LastNavalRetryDelaySeconds = escalatedDelay;
        }

        private void ClearNavalSearchBackoff(string itemKey, Vector3 anchor)
        {
            string normalizedItem = IA_Text.Normalize(itemKey);
            string globalKey = normalizedItem + ":global";
            string localKey = normalizedItem + ":" + BuildAnchorCellKey(anchor);
            _navalSearchBackoffUntil.Remove(globalKey);
            _navalSearchBackoffUntil.Remove(localKey);
            _navalFailureStreakByKey.Remove(globalKey);
            _navalFailureStreakByKey.Remove(localKey);
            _navalFailureLastReasonByKey.Remove(globalKey);
            _navalFailureLastReasonByKey.Remove(localKey);
        }

        private static float ResolveNavalSearchBackoffSeconds(string normalizedReason)
        {
            if (string.IsNullOrEmpty(normalizedReason))
            {
                return 0f;
            }

            if (IsTerritoryNavalFailureReason(normalizedReason))
            {
                return 28f;
            }

            if (normalizedReason.Contains("sem ancora costeira viavel"))
            {
                return 18f;
            }

            if (IsGeometryNavalFailureReason(normalizedReason))
            {
                return 20f;
            }

            return 0f;
        }

        private float ResolveEscalatedRetryDelay(int streak)
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            bool underCombatPressure = pressure != null && pressure.Estado != EstadoCargaIA.Normal;
            if (!underCombatPressure)
            {
                return streak <= 1 ? 12f : (streak == 2 ? 18f : 30f);
            }

            if (streak <= 1)
            {
                return 15f;
            }

            if (streak == 2)
            {
                return 30f;
            }

            return 60f;
        }

        private static float ResolveBootstrapNavalRetryDelay(bool useDirectFallback, string reason)
        {
            string normalizedReason = IA_Text.Normalize(reason);
            if (normalizedReason == "busca em cooldown")
            {
                return 8.5f;
            }

            if (normalizedReason.Contains("falha naval recente em cooldown"))
            {
                return useDirectFallback ? 16f : 12f;
            }

            if (IsTerritoryNavalFailureReason(normalizedReason))
            {
                return useDirectFallback ? 22f : 18f;
            }

            if (normalizedReason.Contains("sem ancora costeira viavel"))
            {
                return useDirectFallback ? 18f : 14f;
            }

            if (IsGeometryNavalFailureReason(normalizedReason))
            {
                return useDirectFallback ? 18f : 14f;
            }

            float retryDelay = useDirectFallback ? 4.5f : 3.5f;
            if (normalizedReason.Contains("costa"))
            {
                retryDelay += 1.5f;
            }

            return retryDelay;
        }

        private static bool IsTerritoryNavalFailureReason(string normalizedReason)
        {
            return !string.IsNullOrEmpty(normalizedReason)
                   && (normalizedReason.Contains("territorio costeiro nao reivindicado")
                       || normalizedReason.Contains("sem fronteira amiga proxima")
                       || normalizedReason.Contains("costa neutra distante")
                       || normalizedReason.Contains("costa sob pressao inimiga"));
        }

        private static bool IsGeometryNavalFailureReason(string normalizedReason)
        {
            return !string.IsNullOrEmpty(normalizedReason)
                   && (normalizedReason.Contains("sem costa valida")
                       || normalizedReason.Contains("frente sem agua")
                       || normalizedReason.Contains("traseira sem terra")
                       || normalizedReason.Contains("praia muito funda"));
        }

        private static string BuildAnchorCellKey(Vector3 value)
        {
            int x = Mathf.RoundToInt(value.x / CoastalAnchorCacheCellSize);
            int z = Mathf.RoundToInt(value.z / CoastalAnchorCacheCellSize);
            return x + ":" + z;
        }

        private bool HasWaterAround(Vector3 center)
        {
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(angle) * 170f, 0f, Mathf.Sin(angle) * 170f);
                IA_MapCell cell = _context.MapAnalyzer.SampleCell(p);
                if (cell.Terrain == IA_TerrainType.Water || cell.Terrain == IA_TerrainType.Coast)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
