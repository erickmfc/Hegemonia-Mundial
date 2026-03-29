using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_BuildDirector : IIAUpdateModule
    {
        private const float BootstrapPrefeituraTime = 5f;
        private const float BootstrapAeroportoTime = 10f;
        private const float BootstrapVehicleFactoryTime = 15f;
        private const float BootstrapSupportHangarTime = 20f;
        private const float BootstrapTentTime = 25f;
        private const float BootstrapAnalysisDuration = 5f;
        private const float BootstrapShipyardHoldDuration = 5f;
        private const float NavalEdgeSafetyMargin = 145f;
        private const float NavalLaunchSafetyMargin = 95f;
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private int _lastKnownStructureCount = -1;
        private float _lastProgressTime;
        private float _nextRecoveryAttemptTime;
        private float _nextCoastScanTime;
        private float _nextNavalAttemptTime;
        private int _recoveryLevel;
        private int _bootstrapNavalAttemptCursor;
        private Vector3 _cachedCoastAnchor;
        private bool _cachedCoastAvailable;
        private readonly Dictionary<string, float> _missingItemCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _placementRetryCooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _warningCooldownUntil = new Dictionary<string, float>();

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

        public void Tick(float now, float deltaTime)
        {
            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + 0.95f;
            if (_context.CommandQueue.PendingCount > 8)
            {
                return;
            }

            int cityHall = CountStructures("prefeitura");
            int hq = CountStructures("quartel general", "quartel_general");
            int barracks = CountStructures("tenda", "barraca");
            int factories = CountStructures("construtor de veiculos", "construtor", "fabrica");
            int radars = CountStructures("radar");
            int sentries = CountStructures("torre", "sentinela", "metralh", "torreta");
            int ciws = CountStructures("ciws", "phalanx", "antia");
            int airports = CountStructures("aeroporto", "base aerea", "airport", "pista");
            int heliports = CountStructures("heliporto");
            int estaleiros = CountStructures("estaleiro");
            int piers = CountStructures("pier");
            int plataformas = CountStructures("plataforma");
            int walls = CountStructures("muro", "wall");
            int missiles = CountStructures("lancador", "missil", "silo");
            int warehouses = CountStructures("armazem", "galpao");
            UpdateProgressTracker(now);

            Vector3 baseCenter = _context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && _context.Brain != null)
            {
                baseCenter = _context.Brain.transform.position;
            }
            Vector3 landAnchor = ResolveLandAnchor(baseCenter);
            IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
            float localThreat = _context.ThreatAnalyzer.EvaluateThreat(landAnchor, IA_Domain.Land);
            int visibleEnemies = _context.WorldState.VisibleEnemies.Count;
            int developedStructures = _context.WorldState.OwnStructures.Count;
            int ownCombatCount = Mathf.Max(_context.WorldState.OwnCombatUnits.Count, CountApproxCombatUnits());
            bool structuresStableForTimedNaval = now - _lastProgressTime >= 6f;
            bool timedNavalOpening = (now >= 20f && ownCombatCount >= 15 && structuresStableForTimedNaval)
                                     || (now >= 35f && ownCombatCount >= 15);
            Vector3 navalAnchor = landAnchor;
            bool coastAvailable = false;
            bool needCoastScan = estaleiros < 1
                                 || piers < 1
                                 || plataformas < 1
                                 || counter.ReinforceCoast
                                 || counter.NavalWeight > 0.20f;
            if (needCoastScan)
            {
                if (now >= _nextCoastScanTime)
                {
                    _cachedCoastAvailable = TryFindCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                    if (!_cachedCoastAvailable && _context.Brain != null)
                    {
                        Vector3 brainAnchor = _context.Brain.transform.position;
                        if (brainAnchor != Vector3.zero)
                        {
                            _cachedCoastAvailable = TryFindCoastalAnchor(brainAnchor, out _cachedCoastAnchor);
                        }
                    }

                    _nextCoastScanTime = now + (_cachedCoastAvailable ? 10f : 14f);
                }

                coastAvailable = _cachedCoastAvailable;
                navalAnchor = coastAvailable ? _cachedCoastAnchor : landAnchor;
            }

            if (HandleScriptedBootstrap(
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
                piers))
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
            bool shouldOpenNaval = earlyNavalOpening
                                   || timedNavalOpening
                                   || (factories > 0
                                   && (developedStructures >= 4
                                       || counter.ReinforceCoast
                                       || counter.NavalWeight > 0.10f));
            if (shouldOpenNaval && now >= _nextNavalAttemptTime)
            {
                if (!coastAvailable && estaleiros < 1 && piers < 1)
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

                    if (estaleiros < 1)
                    {
                        bool queuedEstaleiro = QueueBuildAtWater("Estaleiro Naval", IA_ZoneType.Naval, navalSearchAnchor, navalMinRadius, navalMaxRadius, estaleiroPriority, earlyNavalOpening ? 8f : 14f);
                        _nextNavalAttemptTime = now + (queuedEstaleiro ? 8f : (coastAvailable ? 12f : 18f));
                        return;
                    }

                    if (piers < 1 && shouldBuildPierNow)
                    {
                        bool queuedPier = QueueBuildAtWater("pier", IA_ZoneType.Naval, navalSearchAnchor, Mathf.Max(20f, navalMinRadius - 24f), navalMaxRadius, pierPriority, 16f);
                        _nextNavalAttemptTime = now + (queuedPier ? 10f : 16f);
                        return;
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
            if (coastNeeded && (coastAvailable || estaleiros > 0 || piers > 0))
            {
                Vector3 coastalBuildAnchor = coastAvailable ? navalAnchor : landAnchor;
                float platformMaxRadius = coastAvailable ? 260f : 900f;
                if (plataformas < 1 && QueueBuildAtWater("PLataforma", IA_ZoneType.Naval, coastalBuildAnchor, 35f, platformMaxRadius, 78, 18f))
                {
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
            if (!CanTryBuildItem(itemKey, now) || !CanRetryPlacementSearch(itemKey, IA_TerrainType.Choke, now))
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
            return QueueBuild(itemKey, candidate, IA_ZoneType.Defense, priority, cooldown);
        }

        private Vector3 ResolveLandAnchor(Vector3 fallback)
        {
            Vector3 brainPos = _context.Brain != null ? _context.Brain.transform.position : fallback;

            Vector3 cityHallPos;
            if (TryFindBestStructurePosition(out cityHallPos, brainPos, "prefeitura", "governo", "capital"))
            {
                return cityHallPos;
            }

            Vector3 corePos;
            if (TryFindBestStructurePosition(out corePos, brainPos, "quartel general", "quartel_general", "tenda", "barraca", "construtor de veiculos", "fabrica", "armazem"))
            {
                return corePos;
            }

            return fallback != Vector3.zero ? fallback : brainPos;
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
            if (!CanTryBuildItem(itemKey, now) || !CanRetryPlacementSearch(itemKey, desiredTerrain, now))
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
            return QueueBuild(itemKey, candidate, zone, priority, cooldown);
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

            if (anchor == Vector3.zero && _context.Brain != null)
            {
                anchor = _context.Brain.transform.position;
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

            if (desiredTerrain == IA_TerrainType.Water)
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

                if (TryFindDirectNavalCandidate(itemKey, zone, anchor, waterMin, waterMax, out candidate, out reason))
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

                    return TryBootstrapMandatoryLandBuild(
                        "aeroporto",
                        landAnchor,
                        IA_ZoneType.Air,
                        240f,
                        900f,
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
                        _cachedCoastAvailable = TryFindCoastalAnchor(landAnchor, out _cachedCoastAnchor);
                        _nextCoastScanTime = now + 2.5f;
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
            string reason;
            if (TryBootstrapBuildByKeys(zone, IA_TerrainType.Land, anchor, minRadius, maxRadius, priority, cooldown, out reason, keys))
            {
                _context.Brain.ReportBootstrapError(string.Empty);
                _context.Brain.SetBootstrapStatus(label + " enfileirado");
                return true;
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
            string reason;
            if (TryBootstrapBuildByKeys(
                IA_ZoneType.Air,
                IA_TerrainType.Land,
                anchor,
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
                anchor,
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

            string itemKey = ResolveFirstAvailableKey(
                "Estaleiros navais",
                "Estaleiro Naval",
                "estaleiros navais",
                "estaleiro naval",
                "estaleiro",
                "estaleiros");
            if (string.IsNullOrEmpty(itemKey))
            {
                _context.Brain.ReportBootstrapError("estaleiro naval: item nao encontrado");
                _context.Brain.SetBootstrapStatus("estaleiro naval indisponivel no catalogo");
                return true;
            }

            string reason = "nenhuma tentativa executada";
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
            }

            Vector3 fallbackWater = _context.MapAnalyzer.FindPointInTerrain(landAnchor, IA_TerrainType.Water, 20f, 520f, 10);
            AddSearchAnchor(anchors, fallbackWater);
            Vector3 wideWaterAnchor;
            if (TryFindWideWaterSearchAnchor(landAnchor, 30f, 520f, out wideWaterAnchor))
            {
                AddSearchAnchor(anchors, wideWaterAnchor);
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
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 refinedAnchor;
                if (TryFindDirectCoastalAnchor(anchors[i], out refinedAnchor) || TryFindCoastalAnchor(anchors[i], out refinedAnchor))
                {
                    AddSearchAnchor(refinedAnchors, refinedAnchor);
                }
            }

            anchors = refinedAnchors;

            if (landAnchor != Vector3.zero)
            {
                Vector3 flatLandAnchor = Flatten(landAnchor);
                anchors.RemoveAll(anchor => Vector3.Distance(Flatten(anchor), flatLandAnchor) > 850f);
            }

            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                string territoryReason;
                if (!_context.Backend.BuildService.ValidateTerritoryProbe(itemKey, anchors[i], out territoryReason))
                {
                    anchors.RemoveAt(i);
                }
            }

            if (anchors.Count == 0)
            {
                _nextNavalAttemptTime = now + 2f;
                _context.Brain.ReportBootstrapError("estaleiro naval: sem costa dentro do territorio");
                _context.Brain.SetBootstrapStatus("sem costa propria para estaleiro; aguardando expansao territorial");
                return true;
            }

            int attemptIndex = Mathf.Abs(_bootstrapNavalAttemptCursor++);
            int anchorIndex = attemptIndex % anchors.Count;
            bool useDirectFallback = ((attemptIndex / anchors.Count) % 2) == 1;
            Vector3 selectedAnchor = anchors[anchorIndex];
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
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("estaleiro naval enfileirado na agua");
                    return true;
                }
            }
            else
            {
                Vector3 candidate;
                if (TryFindDirectNavalCandidate(itemKey, IA_ZoneType.Naval, selectedAnchor, 0f, coastAvailable ? 920f : 2100f, out candidate, out reason)
                    && QueueBuild(itemKey, candidate, IA_ZoneType.Naval, 996, 4.5f))
                {
                    _nextNavalAttemptTime = now + 4f;
                    _context.Brain.ReportBootstrapError(string.Empty);
                    _context.Brain.SetBootstrapStatus("estaleiro naval enfileirado via fallback direto");
                    return true;
                }
            }

            float retryDelay = useDirectFallback ? 4.5f : 3.5f;
            if (reason == "busca em cooldown")
            {
                retryDelay = 8.5f;
            }
            else if (reason.Contains("costa"))
            {
                retryDelay += 1.5f;
            }

            _nextNavalAttemptTime = now + retryDelay;
            _context.Brain.ReportBootstrapError("estaleiro naval: " + reason);
            _context.Brain.SetBootstrapStatus(
                "tentativa " + (attemptIndex + 1) + " do estaleiro falhou | ancora "
                + (anchorIndex + 1) + "/" + anchors.Count
                + " | motivo=" + reason);
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
            if (!CanTryBuildItem(itemKey, now))
            {
                reason = "item em cooldown";
                return false;
            }

            if (!CanRetryPlacementSearch(itemKey, terrain, now))
            {
                reason = "busca em cooldown";
                return false;
            }

            Vector3 candidate;
            if (!TryFindValidatedCandidate(itemKey, zone, anchor, terrain, minRadius, maxRadius, out candidate, out reason))
            {
                MarkPlacementSearchFailure(itemKey, terrain, now);
                return false;
            }

            ClearPlacementSearchFailure(itemKey, terrain);
            if (!QueueBuild(itemKey, candidate, zone, priority, cooldown))
            {
                reason = "duplicada em fila";
                return false;
            }

            reason = string.Empty;
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
            candidate = anchor;
            reason = "nenhum ponto valido";

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
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);

                    if (!IsTerrainCandidateAccepted(cell, desiredTerrain))
                    {
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
                        return true;
                    }
                }
            }

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

        private bool QueueBuild(string itemKey, Vector3 candidate, IA_ZoneType zone, int priority, float cooldown)
        {
            Quaternion rotation = Quaternion.identity;
            string reason;
            if (!TryResolveBuildPose(itemKey, ref candidate, ref rotation, out reason))
            {
                return false;
            }

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = itemKey,
                Position = candidate,
                Rotation = rotation,
                Zone = zone
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
            return queued;
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
            string itemKey = ResolveFirstAvailableKey(resolveKeys);
            if (string.IsNullOrEmpty(itemKey))
            {
                return false;
            }

            Vector3 candidate;
            float boostedMaxRadius = maxRadius + (_recoveryLevel * 45f);
            if (!TryFindValidatedCandidate(itemKey, zone, anchor, terrain, minRadius, boostedMaxRadius, out candidate))
            {
                return TryLegacyEmergencyBuild(itemKey, anchor, zone, terrain, minRadius, boostedMaxRadius);
            }

            if (ExecuteBuildImmediately(itemKey, candidate, zone))
            {
                return true;
            }

            return TryLegacyEmergencyBuild(itemKey, anchor, zone, terrain, minRadius, boostedMaxRadius);
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

        private bool ExecuteBuildImmediately(string itemKey, Vector3 candidate, IA_ZoneType zone)
        {
            if (_context.Brain != null && _context.Brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                return false;
            }

            Quaternion rotation = Quaternion.identity;
            string poseReason;
            if (!TryResolveBuildPose(itemKey, ref candidate, ref rotation, out poseReason))
            {
                return false;
            }

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = itemKey,
                Position = candidate,
                Rotation = rotation,
                Zone = zone
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
            DadosConstrucao item;
            if (!_context.Backend.TryResolveItem(itemKey, out item) || item == null || item.prefabDaUnidade == null)
            {
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

                return false;
            }

            Quaternion rotation = Quaternion.identity;
            if (!TryResolveBuildPose(itemKey, ref candidate, ref rotation, out reason))
            {
                if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
                {
                    LogVerboseWarning("legacy:nopose:" + IA_Text.Normalize(itemKey) + ":" + reason, "[IA_BuildDirector] Legacy recovery sem pose valida para " + itemKey + " | " + reason, 18f);
                }

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

                return false;
            }

            if (!_context.Brain.TrySpend(item.preco))
            {
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
                return false;
            }

            _context.Backend.EnsureIdentity(built);
            _context.WorldState.MarkDirty();
            _lastProgressTime = Time.time;

            if (_context.Brain != null && _context.Brain.EnableVerboseLogs)
            {
                LogVerboseWarning("legacy:built:" + IA_Text.Normalize(itemKey), "[IA_BuildDirector] Legacy recovery construiu " + itemKey + " em " + candidate, 10f);
            }

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
            candidate = anchor;
            reason = "nenhum ponto legado";

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
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);

                    if (!IsTerrainCandidateAccepted(cell, desiredTerrain))
                    {
                        continue;
                    }

                    Vector3 pos = cell.Center;
                    if (!LegacySpaceFree(pos, safeRadius))
                    {
                        reason = "espaco legado ocupado";
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
                    return true;
                }
            }

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
            candidate = anchor;
            reason = "nenhum ponto naval direto";

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.prefabDaUnidade == null)
            {
                reason = "item naval nao encontrado";
                return false;
            }

            var searchAnchors = new List<Vector3>();
            AddSearchAnchor(searchAnchors, anchor);

            Vector3 coastalAnchor;
            if (TryFindDirectCoastalAnchor(anchor, out coastalAnchor))
            {
                AddSearchAnchor(searchAnchors, coastalAnchor);
            }

            for (int i = 0; i < _context.WorldState.OwnStructures.Count && searchAnchors.Count < 3; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                AddSearchAnchor(searchAnchors, structure.transform.position);

                if (searchAnchors.Count >= 3)
                {
                    break;
                }
            }

            for (int i = 0; i < searchAnchors.Count; i++)
            {
                Vector3 searchAnchor = searchAnchors[i];
                if (TryFindDirectNavalCandidateFromAnchor(itemKey, data, zone, searchAnchor, minRadius, maxRadius, out candidate, out reason))
                {
                    return true;
                }
            }

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
            int rings = requiresCoast
                ? Mathf.Clamp(Mathf.CeilToInt((endRadius - startRadius) / 180f) + 2, 5, 16)
                : Mathf.Clamp(Mathf.CeilToInt((endRadius - startRadius) / 220f) + 2, 4, 12);
            float coastRadiusStep = Mathf.Clamp((endRadius - startRadius) / 18f, 24f, 72f);

            for (int ring = 0; ring < rings; ring++)
            {
                float t = rings <= 1 ? 0f : ring / (float)(rings - 1);
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                int samplesPerRing = radius <= 0.01f
                    ? 1
                    : (requiresCoast
                        ? (radius < 280f ? 10 : (radius < 900f ? 14 : 18))
                        : (radius < 360f ? 8 : 12));

                for (int i = 0; i < samplesPerRing; i++)
                {
                    float angleDeg = ((360f / samplesPerRing) * i) + (ring * 11f);
                    float angle = angleDeg * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int passes = requiresCoast && radius > 48f ? 3 : 1;
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

            Collider[] hits = Physics.OverlapSphere(position, Mathf.Max(10f, radius * 0.75f), ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
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
            reason = string.Empty;

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.prefabDaUnidade == null)
            {
                reason = "item nao encontrado";
                return false;
            }

            if (!NavalPlacementResolver.RequiresCoastalPlacement(data.prefabDaUnidade))
            {
                return true;
            }

            NavalPlacementResolver.StructurePose pose;
            if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, position, rotation, out pose))
            {
                reason = string.IsNullOrEmpty(pose.Reason) ? "costa invalida" : pose.Reason;
                return false;
            }

            position = pose.Position;
            rotation = pose.Rotation;
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

        private int CountApproxCombatUnits()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
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

            return count;
        }

        private bool TryFindCoastalAnchor(Vector3 center, out Vector3 coastalAnchor)
        {
            coastalAnchor = center;
            DadosConstrucao coastalStructure = _context.Backend.FindFirstAvailable("estaleiros navais", "estaleiro naval", "estaleiro", "estaleiros", "pier");
            string coastalKey = coastalStructure != null && coastalStructure.prefabDaUnidade != null
                ? (string.IsNullOrEmpty(coastalStructure.nomeItem) ? coastalStructure.prefabDaUnidade.name : coastalStructure.nomeItem)
                : "estaleiro naval";
            Vector3 reference = ResolveStrategicReference(center);
            bool foundBest = false;
            float bestScore = float.MinValue;
            float[] radii = { 120f, 240f, 360f, 520f, 680f, 840f };
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                for (int i = 0; i < 16; i++)
                {
                    float angle = (360f / 16f) * i * Mathf.Deg2Rad;
                    Vector3 probe = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                    if (cell != null
                        && (cell.Terrain == IA_TerrainType.Coast || cell.Terrain == IA_TerrainType.Water))
                    {
                        Vector3 candidatePosition = new Vector3(probe.x, cell.Height, probe.z);
                        string territoryReason;
                        if (_context.Backend.BuildService.ValidateTerritoryProbe(coastalKey, candidatePosition, out territoryReason))
                        {
                            TryConsiderCoastalAnchorCandidate(reference, candidatePosition, candidatePosition - center, ref foundBest, ref bestScore, ref coastalAnchor);
                        }
                    }
                }
            }

            Vector3 directAnchor;
            if (TryFindDirectCoastalAnchor(center, out directAnchor))
            {
                TryConsiderCoastalAnchorCandidate(reference, directAnchor, directAnchor - center, ref foundBest, ref bestScore, ref coastalAnchor);
            }

            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                Vector3 probeCenter = structure.transform.position;
                for (int ring = 0; ring < 4; ring++)
                {
                    float radius = 180f + (ring * 180f);
                    for (int sample = 0; sample < 20; sample++)
                    {
                        float angle = ((360f / 20f) * sample + (ring * 9f)) * Mathf.Deg2Rad;
                        Vector3 probe = probeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        IA_MapCell cell = _context.MapAnalyzer.SampleCell(probe);
                        if (cell != null
                            && (cell.Terrain == IA_TerrainType.Coast || cell.Terrain == IA_TerrainType.Water))
                        {
                            Vector3 candidatePosition = new Vector3(probe.x, cell.Height, probe.z);
                            string territoryReason;
                            if (_context.Backend.BuildService.ValidateTerritoryProbe(coastalKey, candidatePosition, out territoryReason))
                            {
                                TryConsiderCoastalAnchorCandidate(reference, candidatePosition, candidatePosition - probeCenter, ref foundBest, ref bestScore, ref coastalAnchor);
                            }
                        }
                    }
                }

            }

            Vector3 fallback = _context.MapAnalyzer.FindPointInTerrain(center, IA_TerrainType.Water, 40f, 520f, 10);
            IA_MapCell fallbackCell = _context.MapAnalyzer.SampleCell(fallback);
            if (fallbackCell != null
                && (fallbackCell.Terrain == IA_TerrainType.Coast || fallbackCell.Terrain == IA_TerrainType.Water))
            {
                Vector3 fallbackPosition = new Vector3(fallback.x, fallbackCell.Height, fallback.z);
                string fallbackReason;
                if (_context.Backend.BuildService.ValidateTerritoryProbe(coastalKey, fallbackPosition, out fallbackReason))
                {
                    TryConsiderCoastalAnchorCandidate(reference, fallbackPosition, fallbackPosition - center, ref foundBest, ref bestScore, ref coastalAnchor);
                }
            }

            return foundBest;
        }

        private bool TryFindDirectCoastalAnchor(Vector3 center, out Vector3 coastalAnchor)
        {
            coastalAnchor = center;

            DadosConstrucao coastalStructure = _context.Backend.FindFirstAvailable("estaleiros navais", "estaleiro naval", "estaleiro", "estaleiros", "pier");
            if (coastalStructure == null || coastalStructure.prefabDaUnidade == null)
            {
                return false;
            }

            Vector3 reference = ResolveStrategicReference(center);
            bool foundBest = false;
            float bestScore = float.MinValue;
            const float maxSearchRadius = 900f;
            const int rings = 8;
            for (int r = 0; r < rings; r++)
            {
                float radius = rings <= 1 ? 0f : Mathf.Lerp(0f, maxSearchRadius, r / (float)(rings - 1));
                int samples = radius <= 0.01f ? 1 : (radius < 260f ? 8 : (radius < 1100f ? 12 : 16));
                for (int i = 0; i < samples; i++)
                {
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
                        string territoryReason;
                        string probeKey = string.IsNullOrEmpty(coastalStructure.nomeItem) ? coastalStructure.prefabDaUnidade.name : coastalStructure.nomeItem;
                        if (_context.Backend.BuildService.ValidateTerritoryProbe(probeKey, pose.Position, out territoryReason))
                        {
                            TryConsiderCoastalAnchorCandidate(reference, pose.Position, pose.Rotation * Vector3.forward, ref foundBest, ref bestScore, ref coastalAnchor);
                        }
                    }
                }
            }

            return foundBest;
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
                   || normalized.Contains("quartel general")
                   || normalized.Contains("quartel_general")
                   || normalized == "hq";
        }

        private bool CanTryBuildItem(string itemKey, float now)
        {
            string normalized = IA_Text.Normalize(itemKey);
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
            float retryDelay = desiredTerrain == IA_TerrainType.Water ? 12f : 7f;
            _placementRetryCooldownUntil[BuildPlacementRetryKey(itemKey, desiredTerrain)] = now + retryDelay;
        }

        private void ClearPlacementSearchFailure(string itemKey, IA_TerrainType desiredTerrain)
        {
            _placementRetryCooldownUntil.Remove(BuildPlacementRetryKey(itemKey, desiredTerrain));
        }

        private static string BuildPlacementRetryKey(string itemKey, IA_TerrainType desiredTerrain)
        {
            return IA_Text.Normalize(itemKey) + ":" + desiredTerrain;
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
