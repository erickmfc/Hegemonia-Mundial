using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hegemonia.AI.BrainMaster;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.IA01
{
    /// <summary>Composition root for one nation. The MonoBehaviour only hosts identity and lifecycle.</summary>
    public sealed class IA01NationRuntime
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01NationProfile profile;

        public IA01WorldState WorldState { get; }
        public IA01IntentBoard IntentBoard { get; }
        public IA01CommandQueue CommandQueue { get; }
        public IA01BuildReservationGrid Reservations { get; }
        public IA01BuildFailureMemory FailureMemory { get; }
        public IA01ZonePlanner ZonePlanner { get; }
        public IA01LotPlanner LotPlanner { get; }
        public IA01BackendBridge BackendBridge { get; }
        public IA01MissionDirector MissionDirector { get; }
        public IA01BuildCatalogAdapter Catalog { get; }
        public IA01EconomyDirector Economy { get; }
        public IA01EconomicModel EconomicModel { get; }
        public IA01CityPlanner CityPlanner { get; }
        public IA01BuildPlanRuntime BuildPlanRuntime { get; }
        public IA01ConstructionGovernor ConstructionGovernor { get; }
        public IA01StrategyArbiter Strategy { get; }
        public IA01BuildDirector BuildDirector { get; }
        public IA01WarDirector WarDirector { get; }
        public int WarEscalationLevel => WarDirector != null ? WarDirector.EscalationLevel : 0;
        public IA01NationalEconomyDirector NationalEconomy { get; }
        public IA01MilitaryDirector MilitaryDirector { get; }
        public IA01PlanningAdvisor PlanningAdvisor { get; }

        public string ConstructionStatus => BuildDirector.Status;
        public string CombatStatus => WarDirector.Status;
        public string MilitaryStatus => MilitaryDirector != null ? MilitaryDirector.Status : "Reserva militar aguardando inicializacao.";
        public string PlanningStatus => PlanningAdvisor != null ? PlanningAdvisor.Status : "Planejador aguardando inicializacao.";
        public string ProgressionStatus { get; private set; } = "Aguardando inicializacao.";
        public string NextObjectiveStatus { get; private set; } = "Aguardando inicializacao.";
        public string MarketStatus => NationalEconomy.Status;
        public string CapitalSourceStatus => CityPlanner.CapitalSource;
        public string CapitalItemIdStatus => Catalog.CapitalItemIdStatus;
        public string CapitalPrefabStatus => Catalog.CapitalPrefabStatus;
        public string CapitalDiagnosticStatus => CityPlanner.CapitalDiagnostic;
        public string ConstructionModeStatus => ConstructionGovernor != null ? ConstructionGovernor.ConstructionMode.ToString() : "n/d";
        public string ConstructionStateStatus => BuildDirector != null ? BuildDirector.CurrentConstructionState.ToString() : "n/d";
        public string ConstructionCommandStatus => BuildDirector != null ? BuildDirector.ActiveConstructionCommand : "n/d";
        public string ActiveCommandIdStatus => ConstructionCommandStatus;
        public string PendingStructureIdStatus => BuildDirector != null ? BuildDirector.PendingStructureIdStatus : "n/d";
        public string ConfirmationDeadlineStatus => BuildDirector != null ? BuildDirector.ConfirmationDeadlineStatus : "n/d";
        public string TreasuryStatus => currentTreasury.ToString(CultureInfo.InvariantCulture);
        public string BuildingsTotalStatus => ConstructionGovernor != null ? ConstructionGovernor.BuildingsTotal.ToString(CultureInfo.InvariantCulture) : "0";
        public string BuildingsByRoleStatus => ConstructionGovernor != null ? ConstructionGovernor.BuildingsByRole : "n/d";
        public string BuildingsByStrategicRoleStatus => BuildingsByRoleStatus;
        public string HousingNeedStatus => ConstructionGovernor != null ? ConstructionGovernor.HousingNeed : "n/d";
        public string FoodCoverageStatus => ConstructionGovernor != null ? ConstructionGovernor.FoodCoverage : "n/d";
        public string EnergyCoverageStatus => ConstructionGovernor != null ? ConstructionGovernor.EnergyCoverage : "n/d";
        public string StorageOccupancyStatus => ConstructionGovernor != null ? ConstructionGovernor.StorageOccupancy : "n/d";
        public string EmergencyReserveStatus => ConstructionGovernor != null ? ConstructionGovernor.EmergencyReserve : "n/d";
        public string AvailableConstructionFundsStatus => ConstructionGovernor != null ? ConstructionGovernor.AvailableConstructionFunds : "n/d";
        public string CityCoverageStatus => ConstructionGovernor != null ? ConstructionGovernor.CityCoveragePercent : "n/d";
        public string CurrentSectorStatus => ConstructionGovernor != null ? ConstructionGovernor.CurrentSector : "n/d";
        public string CurrentNeedStatus => BuildDirector != null ? BuildDirector.CurrentNeedStatus : "n/d";
        public string FoundationSequenceStatus => CityPlanner != null ? CityPlanner.FoundationSequenceStatus : "n/d";
        public string NeedScoreStatus => BuildDirector != null ? BuildDirector.NeedScoreStatus : "0";
        public string CurrentLotIdStatus => BuildDirector != null ? BuildDirector.CurrentLotIdStatus : "n/d";
        public string LastConstructionCompletedAtStatus => BuildDirector != null ? BuildDirector.LastConstructionCompletedAtStatus : "n/d";
        public string ConstructionFreezeReasonStatus => ConstructionGovernor != null ? ConstructionGovernor.ConstructionFreezeReason : "n/d";
        public string NextUnfreezeConditionStatus => ConstructionGovernor != null ? ConstructionGovernor.NextUnfreezeCondition : "n/d";
        public string LastFailureCodeStatus => BuildDirector != null ? BuildDirector.LastFailureCodeStatus : "None";
        public string LastFailureDetailStatus => BuildDirector != null ? BuildDirector.LastFailureDetailStatus : "n/d";
        public string FoundationFundingGrantedStatus => FoundationFundingGranted ? "true" : "false";
        public string FoundationCapitalCostStatus => Economy != null ? Economy.LastFoundationCapitalCost.ToString(CultureInfo.InvariantCulture) : "0";
        public string FoundationAvailableFundsStatus => Economy != null ? Economy.LastFoundationAvailableFunds.ToString(CultureInfo.InvariantCulture) : "0";
        public string CatalogIndexBuildsStatus => ConstructionGovernor != null ? ConstructionGovernor.CatalogIndexBuilds.ToString() : "0";
        public string CatalogIntentQueriesStatus => ConstructionGovernor != null ? ConstructionGovernor.CatalogIntentQueries.ToString() : "0";
        public string CatalogCandidateReadsStatus => ConstructionGovernor != null ? ConstructionGovernor.CatalogCandidateReads.ToString() : "0";
        public string PhysicsChecksStatus => ConstructionGovernor != null ? ConstructionGovernor.PhysicsChecks.ToString() : "0";
        private float nextDiagnosticsAt;
        private float lastRuntimeSliceMilliseconds;
        private string mostExpensiveModule = "n/d";
        private float mostExpensiveModuleMilliseconds;
        private int currentTreasury;

        public IA01NationRuntime(IA01Controller controller, IA01RuntimeContext context, IA01NationProfile profile)
        {
            this.controller = controller;
            this.context = context;
            this.profile = profile;
            WorldState = new IA01WorldState(controller, context);
            IntentBoard = new IA01IntentBoard();
            CommandQueue = new IA01CommandQueue();
            Reservations = new IA01BuildReservationGrid();
            FailureMemory = new IA01BuildFailureMemory();
            ZonePlanner = new IA01ZonePlanner(controller);
            LotPlanner = new IA01LotPlanner(controller, context, WorldState, Reservations, FailureMemory);
            BackendBridge = new IA01BackendBridge(context);
            MissionDirector = new IA01MissionDirector(CommandQueue);
            Catalog = new IA01BuildCatalogAdapter(controller.CapitalBlueprint, controller.BuildPlan);
            Economy = new IA01EconomyDirector(context, profile);
            EconomicModel = new IA01EconomicModel(profile);
            CityPlanner = new IA01CityPlanner(controller, context, WorldState, Catalog);
            BuildPlanRuntime = new IA01BuildPlanRuntime(controller, context, WorldState, Catalog, CityPlanner);
            ConstructionGovernor = new IA01ConstructionGovernor(controller, context, profile);
            Strategy = new IA01StrategyArbiter(IntentBoard);
            BuildDirector = new IA01BuildDirector(controller, context, WorldState, ConstructionGovernor, Catalog, Reservations, FailureMemory, CommandQueue, CityPlanner, ZonePlanner, LotPlanner, BackendBridge, BuildPlanRuntime);
            WarDirector = new IA01WarDirector(controller, context, WorldState, CityPlanner, MissionDirector);
            MilitaryDirector = new IA01MilitaryDirector(controller, context);
            NationalEconomy = new IA01NationalEconomyDirector(context);
            PlanningAdvisor = new IA01PlanningAdvisor(context, WorldState, controller.EnablePlanningAdvisor);
        }

        public void RegisterHostileAggression(int attackerTeamId, Vector3 attackerPosition, float damage)
        {
            WarDirector?.RegisterHostileAggression(attackerTeamId, attackerPosition, damage);
        }

        public bool FoundationFundingGranted => Economy != null && Economy.FoundationFundingGranted;

        public void RestoreFoundationState(SaveIA01NationState state)
        {
            if (state == null)
            {
                return;
            }

            if (Economy != null)
            {
                Economy.RestoreFoundationFunding(state.foundationFundingGranted);
                Economy.MarkRestoredFromSave();
            }

            if (CityPlanner != null)
            {
                CityPlanner.RestoreUnavailableSequenceSteps(state.foundationSkippedSteps);
            }
            BuildPlanRuntime?.RestoreSaveState(state.buildPlanState);
        }

        public int Execute(float now, int maxOperations, bool restoredFromSave)
        {
            if (maxOperations <= 0)
            {
                return 0;
            }

            float sliceStartedAt = Time.realtimeSinceStartup;
            mostExpensiveModule = "n/d";
            mostExpensiveModuleMilliseconds = 0f;
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            int operations = 0;
            float moduleStartedAt = Time.realtimeSinceStartup;
            operations += WorldState.Refresh(now) ? 1 : 0;
            TrackModule("WorldState", moduleStartedAt);
            operations += Economy.TryApplyInitialTreasury(restoredFromSave) ? 1 : 0;
            IntentBoard.Clear();
            moduleStartedAt = Time.realtimeSinceStartup;
            currentTreasury = country != null ? country.saldo : 0;
            EconomicModel.Refresh(country);
            CityPlanner.RefreshCapital(now);
            int capitalCost = 0;
            if (CityPlanner.Capital == null && Catalog.TryGetCapital(out IA01BuildDefinition capitalDefinition))
            {
                capitalCost = capitalDefinition.Cost;
            }
            operations += Economy.EnsureFoundationFunding(CityPlanner.Capital != null, capitalCost, restoredFromSave) ? 1 : 0;
            currentTreasury = country != null ? country.saldo : currentTreasury;
            UpdateOperationalStatus(now, country);
            moduleStartedAt = Time.realtimeSinceStartup;
            operations += PlanningAdvisor != null && PlanningAdvisor.Refresh(now, country, Economy.IsEmergencyReserveRequired) ? 1 : 0;
            TrackModule("PlanningAdvisor", moduleStartedAt);
            CityPlanner.PublishNeeds(IntentBoard, now, profile, country, Economy.IsEmergencyReserveRequired);
            TrackModule("CityPlanner", moduleStartedAt);
            bool constructionIntentPending = false;
            foreach (IA01Intent candidate in IntentBoard.All)
            {
                if (candidate != null && IsConstructionIntent(candidate.Type))
                {
                    constructionIntentPending = true;
                    break;
                }
            }
            bool threatened = IA01OperationalRules.IsCapitalThreatened(WorldState, CityPlanner.Capital, country);
            moduleStartedAt = Time.realtimeSinceStartup;
            bool marketChanged = NationalEconomy.Plan(now, IntentBoard, Economy.IsEmergencyReserveRequired, constructionIntentPending, threatened);
            TrackModule("Market", moduleStartedAt);
            operations += marketChanged ? 1 : 0;
            moduleStartedAt = Time.realtimeSinceStartup;
            operations += WarDirector.Plan(now, IntentBoard, Economy.IsEmergencyReserveRequired) ? 1 : 0;
            TrackModule("WarDirector", moduleStartedAt);
            moduleStartedAt = Time.realtimeSinceStartup;
            operations += MilitaryDirector != null && MilitaryDirector.Tick(now) ? 1 : 0;
            TrackModule("MilitaryDirector", moduleStartedAt);
            moduleStartedAt = Time.realtimeSinceStartup;
            ConstructionGovernor.Refresh(now, country, WorldState, Catalog, BuildDirector);
            TrackModule("ConstructionGovernor", moduleStartedAt);
            Strategy.Arbitrate(now, Economy.IsEmergencyReserveRequired);
            UpdateObjectiveStatus(IntentBoard, country, now);

            if (!marketChanged && operations < maxOperations)
            {
                moduleStartedAt = Time.realtimeSinceStartup;
                operations += BuildDirector.Plan(now, IntentBoard) ? 1 : 0;
                TrackModule("BuildDirector", moduleStartedAt);
            }

            bool processQueuedCommand = BuildDirector == null
                || !BuildDirector.HasPendingConstruction
                || now >= BuildDirector.ConfirmationReadyAt;
            if (operations < maxOperations && processQueuedCommand)
            {
                bool cancelConstructionCommands = (ConstructionGovernor != null && ConstructionGovernor.ConstructionMode == IA01ConstructionMode.Frozen)
                    || (BuildDirector != null && BuildDirector.CancelQueuedConstructionCommand);
                operations += CommandQueue.ProcessOne(now, cancelConstructionCommands) ? 1 : 0;
            }

            currentTreasury = country != null ? country.saldo : currentTreasury;
            context.SetMetric("ia01.city.lots_reserved", Reservations.ReservedCount);
            context.SetMetric("ia01.commands.pending", CommandQueue.PendingCount);
            context.SetMetric("ia01.world.version", WorldState.Version);
            lastRuntimeSliceMilliseconds = (Time.realtimeSinceStartup - sliceStartedAt) * 1000f;
            PublishDiagnostics(now);
            return operations;
        }

        private void TrackModule(string module, float startedAt)
        {
            float elapsedMilliseconds = (Time.realtimeSinceStartup - startedAt) * 1000f;
            if (elapsedMilliseconds > mostExpensiveModuleMilliseconds)
            {
                mostExpensiveModuleMilliseconds = elapsedMilliseconds;
                mostExpensiveModule = module;
            }
        }

        private void PublishDiagnostics(float now)
        {
            if (now < nextDiagnosticsAt)
            {
                return;
            }

            nextDiagnosticsAt = now + 1f;
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_progress", ProgressionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_objective", NextObjectiveStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_construction", ConstructionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_combat", CombatStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_military_reserve", MilitaryStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_planning_advisor", PlanningStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_market", MarketStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_capital_source", CapitalSourceStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_capital_item", CapitalItemIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_capital_prefab", CapitalPrefabStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_capital_diagnostic", CapitalDiagnosticStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_construction_mode", ConstructionModeStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_construction_state", ConstructionStateStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_construction_command", ConstructionCommandStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_active_command", ActiveCommandIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_pending_structure", PendingStructureIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_confirmation_deadline", ConfirmationDeadlineStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_treasury", TreasuryStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_buildings_total", BuildingsTotalStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_buildings_by_role", BuildingsByRoleStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_buildings_by_strategic_role", BuildingsByStrategicRoleStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_fixed_defense_count", ConstructionGovernor != null ? ConstructionGovernor.FixedDefenseCount.ToString(CultureInfo.InvariantCulture) : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_fixed_defense_limit", ConstructionGovernor != null ? ConstructionGovernor.MaxFixedDefenses.ToString(CultureInfo.InvariantCulture) : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_housing_need", HousingNeedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_food_coverage", FoodCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_energy_coverage", EnergyCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_storage_occupancy", StorageOccupancyStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_emergency_reserve", EmergencyReserveStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_available_construction_funds", AvailableConstructionFundsStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_city_coverage", CityCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_current_sector", CurrentSectorStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_current_need", CurrentNeedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_sequence_step", FoundationSequenceStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_need_score", NeedScoreStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_current_lot", CurrentLotIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_last_construction_completed_at", LastConstructionCompletedAtStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_construction_freeze_reason", ConstructionFreezeReasonStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_next_unfreeze_condition", NextUnfreezeConditionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_blocked_intent", BuildDirector.BlockedIntentStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_block_reason", BuildDirector.BlockReasonStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_failures", BuildDirector.FailureCountStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_last_failure_code", LastFailureCodeStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_last_failure_detail", LastFailureDetailStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_cooldown", BuildDirector.GetCooldownStatus(now));
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_unblock", BuildDirector.NextUnblockCondition);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_foundation_funding_granted", FoundationFundingGrantedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_foundation_capital_cost", FoundationCapitalCostStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_foundation_available_funds", FoundationAvailableFundsStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_expensive_module", mostExpensiveModule + " " + mostExpensiveModuleMilliseconds.ToString("0.00") + " ms");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_last_slice", lastRuntimeSliceMilliseconds.ToString("0.00") + " ms");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_catalog_index_builds", Catalog.IndexBuildCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_catalog_queries", Catalog.IntentQueryCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_catalog_intent_queries", Catalog.IntentQueryCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_catalog_candidates", Catalog.CandidateReadCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_physics_checks", BuildDirector.PhysicsChecks.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_build_plan", controller != null && controller.BuildPlan != null ? controller.BuildPlan.PlanId : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_build_step", BuildPlanRuntime != null ? BuildPlanRuntime.CurrentStepId : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_placement_mode", BuildPlanRuntime != null ? BuildPlanRuntime.PlacementModeStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_requested_role", BuildPlanRuntime != null ? BuildPlanRuntime.RequestedRoleStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_selected_slot", BuildPlanRuntime != null ? BuildPlanRuntime.SelectedSlotStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_slot_state", BuildPlanRuntime != null ? BuildPlanRuntime.SlotStateStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_alternative_slots", BuildPlanRuntime != null ? BuildPlanRuntime.AlternativeSlotsStatus : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01_slot_validation", BuildPlanRuntime != null ? BuildPlanRuntime.SlotValidationResult : "n/d");
        }

        private static bool IsConstructionIntent(IA01IntentType type)
        {
            return type == IA01IntentType.EstablishCapital
                || type == IA01IntentType.BuildEnergy
                || type == IA01IntentType.BuildFoodProduction
                || type == IA01IntentType.BuildResidentialCapacity
                || type == IA01IntentType.BuildStorage
                || type == IA01IntentType.BuildLogistics
                || type == IA01IntentType.BuildIndustry
                || type == IA01IntentType.BuildDefense;
        }

        private void UpdateOperationalStatus(float now, DadosPaisGoverno country)
        {
            bool hasCapital = CityPlanner.Capital != null;
            int structures = WorldState.OwnedStructures.Count;
            int treasury = country != null ? country.saldo : 0;
            int energy = country != null ? country.energia : 0;
            int food = country != null ? country.comida : 0;
            bool threatened = IA01OperationalRules.IsCapitalThreatened(WorldState, CityPlanner.Capital, country);
            bool atWar = country != null && country.emGuerra;
            bool emergencyReserve = Economy.IsEmergencyReserveRequired;

            IA01NationStage resolvedStage = profile != null
                ? profile.ResolveOperationalStage(context.CurrentStage, hasCapital, structures, treasury, energy, food, threatened, atWar, emergencyReserve)
                : context.CurrentStage;
            IA01NationPosture resolvedPosture = profile != null
                ? profile.ResolveOperationalPosture(resolvedStage, hasCapital, structures, treasury, energy, food, threatened, atWar, emergencyReserve)
                : context.CurrentPosture;

            if (context.CurrentStage != resolvedStage)
            {
                context.SetCurrentStage(resolvedStage);
            }

            if (context.CurrentPosture != resolvedPosture)
            {
                context.SetCurrentPosture(resolvedPosture);
            }

            ProgressionStatus = BuildProgressionStatus(resolvedStage, resolvedPosture, hasCapital, structures, treasury, energy, food, threatened, atWar, emergencyReserve);
            context.SetMetric("ia01.progress.stage", (double)resolvedStage);
            context.SetMetric("ia01.progress.posture", (double)resolvedPosture);
            context.SetMetric("ia01.progress.structures", structures);
            context.SetMetric("ia01.progress.capital", hasCapital ? 1d : 0d);
            context.SetMetric("ia01.progress.threatened", threatened ? 1d : 0d);
        }

        private void UpdateObjectiveStatus(IA01IntentBoard board, DadosPaisGoverno country, float now)
        {
            IA01Intent intent = board.GetBestApproved(candidate => BuildDirector == null || BuildDirector.AllowsIntent(candidate, now));
            if (intent == null)
            {
                NextObjectiveStatus = BuildFallbackObjectiveStatus(country);
                return;
            }

            switch (intent.Type)
            {
                case IA01IntentType.EstablishCapital:
                    NextObjectiveStatus = "Objetivo: fundar a prefeitura.";
                    break;
                case IA01IntentType.BuildEnergy:
                    NextObjectiveStatus = "Objetivo: ampliar energia.";
                    break;
                case IA01IntentType.BuildFoodProduction:
                    NextObjectiveStatus = "Objetivo: garantir comida.";
                    break;
                case IA01IntentType.BuildResidentialCapacity:
                    NextObjectiveStatus = "Objetivo: ampliar moradia.";
                    break;
                case IA01IntentType.BuildStorage:
                    NextObjectiveStatus = "Objetivo: ampliar armazenamento.";
                    break;
                case IA01IntentType.BuildLogistics:
                    NextObjectiveStatus = "Objetivo: fortalecer logistica.";
                    break;
                case IA01IntentType.BuildRoad:
                    NextObjectiveStatus = "Objetivo: construir a rua de acesso.";
                    break;
                case IA01IntentType.BuildMilitaryAirport:
                    NextObjectiveStatus = "Objetivo: construir o aeroporto militar.";
                    break;
                case IA01IntentType.BuildCommercialAirport:
                    NextObjectiveStatus = "Objetivo: construir o aeroporto comercial.";
                    break;
                case IA01IntentType.BuildShipyard:
                    NextObjectiveStatus = "Objetivo: construir o estaleiro.";
                    break;
                case IA01IntentType.BuildIndustry:
                    NextObjectiveStatus = "Objetivo: ampliar industria.";
                    break;
                case IA01IntentType.BuildDefense:
                    NextObjectiveStatus = "Objetivo: reforcar defesa fixa.";
                    break;
                case IA01IntentType.DefendCapital:
                    NextObjectiveStatus = "Objetivo: defender a prefeitura.";
                    break;
                case IA01IntentType.CampaignAgainstCapital:
                    NextObjectiveStatus = "Objetivo: pressionar a prefeitura inimiga.";
                    break;
                case IA01IntentType.BuyResource:
                    NextObjectiveStatus = "Objetivo: comprar recursos essenciais.";
                    break;
                case IA01IntentType.SellResource:
                    NextObjectiveStatus = "Objetivo: vender excedente seguro.";
                    break;
                case IA01IntentType.Communicate:
                    NextObjectiveStatus = "Objetivo: comunicar e negociar.";
                    break;
                default:
                    NextObjectiveStatus = "Objetivo: avaliar nova ordem.";
                    break;
            }
        }

        private string BuildProgressionStatus(IA01NationStage stage, IA01NationPosture posture, bool hasCapital, int structures, int treasury, int energy, int food, bool threatened, bool atWar, bool emergencyReserve)
        {
            StringBuilder builder = new StringBuilder(192);
            // Foundation is a transient presentation phase: it keeps save-compatible
            // operational stages while making the capital-to-infrastructure handoff clear.
            string displayStage = hasCapital && structures <= 1 && !threatened && !atWar
                ? "Foundation"
                : stage.ToString();
            builder.Append("fase=").Append(displayStage);
            builder.Append(" postura=").Append(posture);
            builder.Append(" capital=").Append(hasCapital ? "ok" : "pendente");
            builder.Append(" estruturas=").Append(structures);
            builder.Append(" saldo=").Append(treasury);
            builder.Append(" energia=").Append(energy);
            builder.Append(" comida=").Append(food);
            if (threatened)
            {
                builder.Append(" ameaca=sim");
            }
            if (atWar)
            {
                builder.Append(" guerra=sim");
            }
            if (emergencyReserve)
            {
                builder.Append(" reserva=critica");
            }
            return builder.ToString();
        }

        private string BuildFallbackObjectiveStatus(DadosPaisGoverno country)
        {
            if (CityPlanner.Capital == null)
            {
                return "Objetivo: fundar a prefeitura.";
            }

            if (country != null && country.emGuerra)
            {
                return "Objetivo: consolidar defesa e campanha.";
            }

            switch (context.CurrentStage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    return "Objetivo: iniciar infraestrutura basica.";
                case IA01NationStage.Survival:
                    return "Objetivo: fechar energia, comida e moradia.";
                case IA01NationStage.Stabilization:
                    return "Objetivo: reforcar armazenamento e logistica.";
                case IA01NationStage.UrbanDevelopment:
                case IA01NationStage.Industrialization:
                    return "Objetivo: expandir a cidade e a producao.";
                case IA01NationStage.Specialization:
                case IA01NationStage.RegionalProjection:
                case IA01NationStage.GlobalPower:
                    return "Objetivo: projetar poder e manter suporte.";
                case IA01NationStage.Recovering:
                    return "Objetivo: recuperar economia e servicos.";
                case IA01NationStage.Emergency:
                    return "Objetivo: responder a emergencia.";
                default:
                    return "Objetivo: avaliar proximo passo.";
            }
        }
    }

    public sealed class IA01WorldState
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly List<IdentidadeUnidade> ownedStructures = new List<IdentidadeUnidade>(32);
        private readonly List<IdentidadeUnidade> enemyUnits = new List<IdentidadeUnidade>(32);
        private readonly List<MarcadorTerritorio> enemyCapitals = new List<MarcadorTerritorio>(8);
        private float nextRefreshAt;

        public int Version { get; private set; }
        public IReadOnlyList<IdentidadeUnidade> OwnedStructures => ownedStructures;
        public IReadOnlyList<IdentidadeUnidade> EnemyUnits => enemyUnits;
        public IReadOnlyList<MarcadorTerritorio> EnemyCapitals => enemyCapitals;

        public IA01WorldState(IA01Controller controller, IA01RuntimeContext context)
        {
            this.controller = controller;
            this.context = context;
        }

        public bool Refresh(float now)
        {
            if (now < nextRefreshAt)
            {
                return false;
            }

            nextRefreshAt = now + 1f;
            ownedStructures.Clear();
            enemyUnits.Clear();
            enemyCapitals.Clear();
            int ownTeam = context.TeamId;

            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade identity = identities[i];
                if (identity == null || identity.teamID <= 0)
                {
                    continue;
                }

                if (identity.teamID == ownTeam && identity.tipoUnidade == TipoUnidade.Estrutura)
                {
                    ownedStructures.Add(identity);
                }
                else if (identity.teamID != ownTeam && identity.tipoUnidade != TipoUnidade.Estrutura)
                {
                    enemyUnits.Add(identity);
                }
            }

            MarcadorTerritorio[] markers = UnityEngine.Object.FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                MarcadorTerritorio marker = markers[i];
                IdentidadeUnidade identity = marker != null ? marker.GetComponent<IdentidadeUnidade>() : null;
                if (marker != null && marker.ehPrefeitura && identity != null && identity.teamID > 0 && identity.teamID != ownTeam)
                {
                    enemyCapitals.Add(marker);
                }
            }

            Version++;
            return true;
        }

        public bool IsOwnedByNation(IdentidadeUnidade identity)
        {
            return identity != null && identity.teamID == context.TeamId;
        }

        public MarcadorTerritorio FindEnemyCapital(int preferredTeamId)
        {
            MarcadorTerritorio best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < enemyCapitals.Count; i++)
            {
                MarcadorTerritorio capital = enemyCapitals[i];
                IdentidadeUnidade owner = capital != null ? capital.GetComponent<IdentidadeUnidade>() : null;
                if (owner == null || owner.teamID <= 0 || owner.teamID == context.TeamId)
                {
                    continue;
                }

                float score = owner.teamID == preferredTeamId ? 10000f : 0f;
                score -= (capital.transform.position - controller.transform.position).sqrMagnitude * 0.0001f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = capital;
                }
            }

            return best;
        }
    }

    public sealed class IA01IntentBoard
    {
        private readonly Dictionary<IA01IntentType, IA01Intent> intents = new Dictionary<IA01IntentType, IA01Intent>();

        public void Clear()
        {
            intents.Clear();
        }

        public void Publish(IA01IntentType type, int priority, string reason, float now)
        {
            if (!intents.TryGetValue(type, out IA01Intent intent))
            {
                intent = new IA01Intent { Id = "ia01.intent." + type, Type = type, CreatedAt = now };
                intents[type] = intent;
            }

            intent.Priority = Mathf.Max(intent.Priority, priority);
            intent.Reason = reason ?? string.Empty;
        }

        public IA01Intent GetBestApproved(System.Predicate<IA01Intent> filter = null)
        {
            IA01Intent best = null;
            foreach (IA01Intent intent in intents.Values)
            {
                if (!intent.Approved)
                {
                    continue;
                }

                if (filter != null && !filter(intent))
                {
                    continue;
                }

                if (best == null || intent.Priority > best.Priority)
                {
                    best = intent;
                }
            }
            return best;
        }

        public void Complete(IA01IntentType type)
        {
            intents.Remove(type);
        }

        public IEnumerable<IA01Intent> All => intents.Values;
    }

    public sealed class IA01StrategyArbiter
    {
        private readonly IA01IntentBoard board;

        public IA01StrategyArbiter(IA01IntentBoard board)
        {
            this.board = board;
        }

        public void Arbitrate(float now, bool emergencyReserve)
        {
            foreach (IA01Intent intent in board.All)
            {
                intent.Approved = !emergencyReserve
                    || intent.Type == IA01IntentType.EstablishCapital
                    || intent.Type == IA01IntentType.DefendCapital
                    || intent.Type == IA01IntentType.BuyResource;
            }
        }
    }

    public sealed class IA01EconomyDirector
    {
        private readonly IA01RuntimeContext context;
        private readonly IA01NationProfile profile;
        private bool treasuryApplied;
        private bool foundationFundingGranted;
        private bool restoredFromSave;
        private int lastFoundationCapitalCost;
        private int lastFoundationTarget;
        private int lastFoundationAvailableFunds;

        public bool IsEmergencyReserveRequired { get; private set; }
        public bool FoundationFundingGranted => foundationFundingGranted;
        public int LastFoundationCapitalCost => lastFoundationCapitalCost;
        public int LastFoundationTarget => lastFoundationTarget;
        public int LastFoundationAvailableFunds => lastFoundationAvailableFunds;

        public IA01EconomyDirector(IA01RuntimeContext context, IA01NationProfile profile)
        {
            this.context = context;
            this.profile = profile;
        }

        public void MarkRestoredFromSave()
        {
            restoredFromSave = true;
        }

        public void RestoreFoundationFunding(bool granted)
        {
            foundationFundingGranted = granted;
        }

        public bool TryApplyInitialTreasury(bool restoredFromSave)
        {
            if (treasuryApplied || restoredFromSave || this.restoredFromSave || profile == null)
            {
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            if (country == null)
            {
                return false;
            }

            int delta = profile.InitialTreasury - country.saldo;
            if (delta != 0)
            {
                government.AdicionarSaldo(context.TeamId, delta);
            }

            treasuryApplied = true;
            context.SetMetric("ia01.initial_treasury", profile.InitialTreasury);
            return true;
        }

        public bool EnsureFoundationFunding(bool capitalConfirmed, int capitalCost, bool restoredFromSave)
        {
            lastFoundationCapitalCost = capitalCost;
            if (capitalConfirmed || profile == null || restoredFromSave || this.restoredFromSave)
            {
                lastFoundationAvailableFunds = 0;
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            int target = Mathf.Max(profile.InitialTreasury, Mathf.Max(5000, capitalCost) + 2500);
            lastFoundationTarget = target;
            if (government == null || country == null)
            {
                lastFoundationAvailableFunds = 0;
                return false;
            }

            lastFoundationAvailableFunds = Mathf.Max(country.saldo, capitalCost);

            if (foundationFundingGranted && country.saldo >= Mathf.Max(1, capitalCost))
            {
                lastFoundationAvailableFunds = country.saldo;
                return false;
            }

            if (country.saldo >= target)
            {
                foundationFundingGranted = true;
                lastFoundationAvailableFunds = country.saldo;
                return false;
            }

            // A prefeitura e a primeira obra dependem do saldo inicial. Enquanto a sede
            // nao existe, uma sincronizacao externa nao pode consumir esse capital de partida.
            government.AdicionarSaldo(context.TeamId, target - country.saldo);
            context.SetMetric("ia01.foundation_funds_protected", target);
            context.SetMetric("ia01.foundation_capital_cost", capitalCost);
            foundationFundingGranted = true;
            lastFoundationAvailableFunds = target;
            return true;
        }

        public void Refresh(DadosPaisGoverno country)
        {
            IsEmergencyReserveRequired = country != null && country.emGuerra && country.saldo < 500;
        }
    }

    public sealed class IA01CityPlanner
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01WorldState world;
        private readonly IA01BuildCatalogAdapter catalog;
        private readonly HashSet<IA01IntentType> unavailableSequenceSteps = new HashSet<IA01IntentType>();
        private int unavailableSequenceCatalogVersion = -1;
        private MarcadorTerritorio capital;
        private float nextCapitalCheckAt;
        private int lastCapitalCatalogVersion = -1;
        private ComplexoGovernamental lastCapitalAnchor;
        private int lastFoodExpansionDay = -1;
        private int lastEnergyExpansionDay = -1;

        private static readonly IA01IntentType[] FoundationSequence =
        {
            IA01IntentType.BuildEnergy,
            IA01IntentType.BuildFoodProduction,
            IA01IntentType.BuildStarterHouse,
            IA01IntentType.BuildMediumApartment,
            IA01IntentType.BuildHighApartment,
            IA01IntentType.BuildMilitaryTent,
            IA01IntentType.BuildVehicleConstructor,
            IA01IntentType.BuildStorage,
            IA01IntentType.BuildRoad,
            IA01IntentType.BuildMilitaryAirport,
            IA01IntentType.BuildCommercialAirport,
            IA01IntentType.BuildShipyard,
            IA01IntentType.BuildPier,
            IA01IntentType.BuildOffshorePlatform,
            IA01IntentType.BuildIndustry
        };

        public MarcadorTerritorio Capital => capital;
        public string Status { get; private set; } = "Aguardando prefeitura propria.";
        public string CapitalSource { get; private set; } = "Missing";
        public string CapitalDiagnostic { get; private set; } = "Capital ainda nao validada.";

        public IA01CityPlanner(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01BuildCatalogAdapter catalog)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.catalog = catalog;
        }

        public void RefreshCapital(float now)
        {
            int catalogVersion = catalog.CatalogVersion;
            ComplexoGovernamental currentAnchor = controller.PrefeituraAnchor;
            if (capital == null && now < nextCapitalCheckAt && catalogVersion == lastCapitalCatalogVersion && ReferenceEquals(currentAnchor, lastCapitalAnchor))
            {
                return;
            }
            nextCapitalCheckAt = now + 1f;
            lastCapitalCatalogVersion = catalogVersion;
            lastCapitalAnchor = currentAnchor;

            if (IsOwnCapital(capital))
            {
                return;
            }

            ComplexoGovernamental explicitAnchor = controller.PrefeituraAnchor;
            if (TryUseOwnCapital(explicitAnchor, out MarcadorTerritorio explicitMarker))
            {
                capital = explicitMarker;
                CapitalSource = "ExplicitAnchor";
                CapitalDiagnostic = "Anchor validada para o TeamId " + context.TeamId + ".";
                Status = "Prefeitura configurada validada.";
                return;
            }

            if (explicitAnchor != null)
            {
                CapitalSource = "Missing";
                CapitalDiagnostic = "PrefeituraAnchor rejeitada: TeamId incompativel ou identidade ausente.";
            }

            ComplexoGovernamental[] localCapitals = controller.GetComponentsInChildren<ComplexoGovernamental>(true);
            for (int i = 0; i < localCapitals.Length; i++)
            {
                if (TryUseOwnCapital(localCapitals[i], out MarcadorTerritorio marker))
                {
                    capital = marker;
                    CapitalSource = "ExplicitAnchor";
                    CapitalDiagnostic = "Prefeitura encontrada na hierarquia do controlador.";
                    Status = "Prefeitura da propria hierarquia validada.";
                    return;
                }
            }

            if (catalog.TryGetCapital(out IA01BuildDefinition capitalDefinition))
            {
                CapitalSource = controller.CapitalBlueprint != null ? "ExplicitBlueprint" : "CatalogRole";
                CapitalDiagnostic = "Capital valida: itemId=" + capitalDefinition.ItemId + " prefab=" + capitalDefinition.DisplayName + ".";
                Status = "Prefeitura sera criada pelo catalogo oficial.";
                return;
            }

            CapitalSource = "Missing";
            CapitalDiagnostic = catalog.LastDiagnostic;
            Status = "Sem prefeitura valida: " + CapitalDiagnostic;
            nextCapitalCheckAt = now + 15f;
        }

        public void RegisterCapital(GameObject built)
        {
            if (built == null)
            {
                return;
            }

            MarcadorTerritorio marker = built.GetComponent<MarcadorTerritorio>();
            if (marker == null)
            {
                marker = built.AddComponent<MarcadorTerritorio>();
            }
            marker.ConfigureOwnership(context.TeamId, true, 300f);
            capital = marker;
            if (GerenteDeTerritorio.Instancia != null) GerenteDeTerritorio.Instancia.RegistrarMarcador(marker);
            GerenciadorDivisaoTerritorial.GarantirInstancia();
            if (GerenciadorDivisaoTerritorial.Instancia != null) GerenciadorDivisaoTerritorial.Instancia.RegistrarCidade(marker);
            CapitalSource = "Built";
            CapitalDiagnostic = "Prefeitura construida e registrada para o TeamId " + context.TeamId + ".";
            Status = "Prefeitura criada e registrada.";
        }

        public string FoundationSequenceStatus { get; private set; } = "Prefeitura";

        public bool IsFoundationSequenceIntent(IA01IntentType intent)
        {
            return intent == IA01IntentType.EstablishCapital || Array.IndexOf(FoundationSequence, intent) >= 0;
        }

        public void MarkSequenceCatalogUnavailable(IA01IntentType intent, string diagnostic)
        {
            if (!IsFoundationSequenceIntent(intent) || intent == IA01IntentType.EstablishCapital)
            {
                return;
            }

            unavailableSequenceSteps.Add(intent);
            FoundationSequenceStatus = intent + " indisponivel: " + (diagnostic ?? "catalogo sem item");
            context.SetMetric("ia01.sequence.catalog_skipped", 1d);
            context.SetMetric("ia01.sequence.catalog_skipped_step", (int)intent);
        }

        public List<string> CaptureUnavailableSequenceSteps()
        {
            List<string> result = new List<string>(unavailableSequenceSteps.Count);
            foreach (IA01IntentType step in unavailableSequenceSteps)
            {
                result.Add(step.ToString());
            }
            return result;
        }

        public void RestoreUnavailableSequenceSteps(IEnumerable<string> steps)
        {
            unavailableSequenceSteps.Clear();
            if (steps == null)
            {
                return;
            }

            foreach (string value in steps)
            {
                if (Enum.TryParse(value, true, out IA01IntentType step) && IsFoundationSequenceIntent(step))
                {
                    unavailableSequenceSteps.Add(step);
                }
            }
        }

        private bool HasOwnedStructure(IA01StrategicRole role)
        {
            IA01Manager manager = controller != null ? controller.Manager : null;
            return manager != null
                && manager.WorldRegistry != null
                && manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, role) > 0;
        }

        private bool HasOwnedStructureMatching(IA01StrategicRole role, params string[] tokens)
        {
            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null)
            {
                return false;
            }

            IReadOnlyList<IA01WorldEntityRecord> records = manager.WorldRegistry.GetByTeam(context.TeamId);
            for (int i = 0; i < records.Count; i++)
            {
                IA01WorldEntityRecord record = records[i];
                if (record == null || record.Kind != IA01WorldEntityKind.Structure || record.StrategicRole != role)
                {
                    continue;
                }

                string text = IA_Text.Normalize((record.StructureId ?? string.Empty) + " " + (record.DisplayName ?? string.Empty));
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    if (!string.IsNullOrEmpty(tokens[tokenIndex]) && text.Contains(IA_Text.Normalize(tokens[tokenIndex])))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsFoundationStepComplete(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildEnergy:
                    return HasOwnedStructure(IA01StrategicRole.EnergyProduction);
                case IA01IntentType.BuildFoodProduction:
                    return HasOwnedStructure(IA01StrategicRole.FoodProduction);
                case IA01IntentType.BuildResidentialCapacity:
                    return HasOwnedStructure(IA01StrategicRole.Residential);
                case IA01IntentType.BuildStarterHouse:
                    return HasOwnedStructureMatching(IA01StrategicRole.Residential, "casa", "house");
                case IA01IntentType.BuildMediumApartment:
                    return HasOwnedStructureMatching(IA01StrategicRole.Residential, "medio", "médio", "apartamento", "apartment");
                case IA01IntentType.BuildHighApartment:
                    return HasOwnedStructureMatching(IA01StrategicRole.Residential, "hard", "alto", "high", "torre");
                case IA01IntentType.BuildMilitaryTent:
                    return HasOwnedStructureMatching(IA01StrategicRole.MilitaryProduction, "tenda", "tent", "quartel", "barracks");
                case IA01IntentType.BuildVehicleConstructor:
                    return HasOwnedStructureMatching(IA01StrategicRole.MilitaryProduction, "construtor", "veiculo", "veículo", "vehicle");
                case IA01IntentType.BuildStorage:
                    return HasOwnedStructure(IA01StrategicRole.Storage);
                case IA01IntentType.BuildRoad:
                    return HasOwnedStructure(IA01StrategicRole.Logistics);
                case IA01IntentType.BuildMilitaryAirport:
                    return HasOwnedStructureMatching(IA01StrategicRole.Airfield, "militar", "military", "aeroporto_militar");
                case IA01IntentType.BuildCommercialAirport:
                    return HasOwnedStructureMatching(IA01StrategicRole.Airfield, "comercial", "commercial", "aeroporto_comercial");
                case IA01IntentType.BuildShipyard:
                    return HasOwnedStructure(IA01StrategicRole.NavalBase)
                        || HasOwnedStructure(IA01StrategicRole.Shipyard)
                        || HasOwnedStructure(IA01StrategicRole.Port)
                        || HasOwnedStructure(IA01StrategicRole.Pier);
                case IA01IntentType.BuildPier:
                    return HasOwnedStructure(IA01StrategicRole.Pier);
                case IA01IntentType.BuildOffshorePlatform:
                    return HasOwnedStructureMatching(IA01StrategicRole.NavalBase, "plataforma", "offshore");
                case IA01IntentType.BuildIndustry:
                    return HasOwnedStructure(IA01StrategicRole.Industrial);
                default:
                    return intent == IA01IntentType.EstablishCapital && capital != null;
            }
        }

        private string ResolveFoundationReason(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildEnergy: return "Energia inicial";
                case IA01IntentType.BuildFoodProduction: return "Comida inicial";
                case IA01IntentType.BuildResidentialCapacity: return "Moradia inicial";
                case IA01IntentType.BuildStarterHouse: return "Casa inicial";
                case IA01IntentType.BuildMediumApartment: return "Apartamento medio";
                case IA01IntentType.BuildHighApartment: return "Apartamento alto";
                case IA01IntentType.BuildMilitaryTent: return "Tenda militar";
                case IA01IntentType.BuildVehicleConstructor: return "Construtor de veiculos";
                case IA01IntentType.BuildStorage: return "Armazenamento inicial";
                case IA01IntentType.BuildRoad: return "Rua de acesso";
                case IA01IntentType.BuildMilitaryAirport: return "Aeroporto militar";
                case IA01IntentType.BuildCommercialAirport: return "Aeroporto comercial";
                case IA01IntentType.BuildShipyard: return "Estaleiro naval";
                case IA01IntentType.BuildPier: return "Pier naval";
                case IA01IntentType.BuildOffshorePlatform: return "Plataforma offshore";
                case IA01IntentType.BuildIndustry: return "Industria";
                default: return intent.ToString();
            }
        }

        public void PublishNeeds(IA01IntentBoard board, float now, IA01NationProfile profile, DadosPaisGoverno country, bool emergencyReserve)
        {
            if (capital == null)
            {
                board.Publish(IA01IntentType.EstablishCapital, 1000, Status, now);
                FoundationSequenceStatus = "Prefeitura";
                return;
            }

            // A abertura roteirizada e opcional. Sem ela, a IA nao pode enfileirar
            // quartel, fabrica, aeroportos ou estruturas navais por conta propria
            // logo no carregamento da partida.
            if (controller != null && controller.UseScriptedOpening)
            {
                if (catalog.CatalogVersion != unavailableSequenceCatalogVersion)
                {
                    unavailableSequenceSteps.Clear();
                    unavailableSequenceCatalogVersion = catalog.CatalogVersion;
                }

                for (int i = 0; i < FoundationSequence.Length; i++)
                {
                    IA01IntentType step = FoundationSequence[i];
                    if (IsFoundationStepComplete(step) || unavailableSequenceSteps.Contains(step))
                    {
                        continue;
                    }

                    FoundationSequenceStatus = ResolveFoundationReason(step);
                    board.Publish(step, 2000 - i, FoundationSequenceStatus, now);
                    Status = "Sequencia de fundacao: " + FoundationSequenceStatus + ".";
                    return;
                }
            }
            else
            {
                FoundationSequenceStatus = "Abertura roteirizada desativada.";
            }

            bool threatened = IA01OperationalRules.IsCapitalThreatened(world, capital, country);
            bool atWar = country != null && country.emGuerra;
            IA01NationStage stage = context.CurrentStage;
            IA01NationPosture posture = context.CurrentPosture;
            int structures = world.OwnedStructures.Count;
            int energy = country != null ? country.energia : 0;
            int food = country != null ? country.comida : 0;

            PublishBuildNeed(board, now, profile, IA01IntentType.BuildEnergy, stage, posture, structures, threatened, atWar, DeveConstruirEnergia(country), "Energia inicial");
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildFoodProduction, stage, posture, structures, threatened, atWar, DeveConstruirComida(country), "Producao de alimentos");
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildResidentialCapacity, stage, posture, structures, threatened, atWar, structures < 4 || stage == IA01NationStage.Survival || stage == IA01NationStage.Stabilization, "Moradia inicial");
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildStorage, stage, posture, structures, threatened, atWar, DeveConstruirArmazenamento(), "Reserva e armazenamento");
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildLogistics, stage, posture, structures, threatened, atWar, structures < 6 || stage >= IA01NationStage.UrbanDevelopment, "Acesso e logistica");
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildIndustry, stage, posture, structures, threatened, atWar, structures >= 5 && stage >= IA01NationStage.Industrialization, "Base industrial");
            bool shouldPublishDefense = threatened
                || atWar
                || (stage != IA01NationStage.Recovering
                    && posture != IA01NationPosture.Recovery
                    && structures >= 6
                    && profile != null
                    && (profile.DefenseWeight >= 0.45f || profile.MilitaryWeight >= 0.45f));
            PublishBuildNeed(board, now, profile, IA01IntentType.BuildDefense, stage, posture, structures, threatened, atWar, shouldPublishDefense, "Defesa territorial");

            if (structures >= 6)
            {
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildEnergy, stage, posture, structures, threatened, atWar, DeveConstruirEnergia(country), "Reforco energetico");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildFoodProduction, stage, posture, structures, threatened, atWar, DeveConstruirComida(country), "Seguranca alimentar");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildResidentialCapacity, stage, posture, structures, threatened, atWar, profile == null || profile.CautionWeight >= 0.45f, "Expansao residencial");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildStorage, stage, posture, structures, threatened, atWar, DeveConstruirArmazenamento(), "Suporte industrial");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildLogistics, stage, posture, structures, threatened, atWar, profile == null || profile.ExpansionWeight >= 0.45f || stage >= IA01NationStage.RegionalProjection, "Rede logistica");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildIndustry, stage, posture, structures, threatened, atWar, stage >= IA01NationStage.Industrialization && (profile == null || profile.IndustryWeight >= 0.40f), "Expansao industrial");
                PublishBuildNeed(board, now, profile, IA01IntentType.BuildDefense, stage, posture, structures, threatened, atWar, threatened || atWar || profile == null || profile.DefenseWeight >= 0.45f, "Seguranca da cidade");
            }

            Status = BuildStatus(stage, posture, threatened, atWar, emergencyReserve, structures);
        }

        /// <summary>
        /// Armazém só é pedido quando há capacidade realmente pressionada. O limite
        /// absoluto é três, definido pelas três âncoras de logística da IA01.
        /// </summary>
        private bool DeveConstruirArmazenamento()
        {
            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null || context == null)
            {
                return false;
            }

            int existentes = manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.Storage);
            if (existentes >= 3)
            {
                return false;
            }

            if (existentes == 0)
            {
                return true;
            }

            if (context.TryGetResource("storage", out IA01ResourceRecord armazenamento)
                && armazenamento != null
                && armazenamento.Capacity > 0f)
            {
                return armazenamento.Amount / armazenamento.Capacity >= 0.85f;
            }

            return false;
        }

        private bool DeveConstruirComida(DadosPaisGoverno country)
        {
            return DeveExpandirInfraestrutura(
                IA01StrategicRole.FoodProduction,
                country != null ? country.comida : 0,
                800,
                6,
                ref lastFoodExpansionDay);
        }

        private bool DeveConstruirEnergia(DadosPaisGoverno country)
        {
            return DeveExpandirInfraestrutura(
                IA01StrategicRole.EnergyProduction,
                country != null ? country.energia : 0,
                1400,
                3,
                ref lastEnergyExpansionDay);
        }

        /// <summary>
        /// Fazenda e usina são expansões lentas: uma por dia de jogo somente
        /// quando o recurso correspondente está baixo. Impede a avalanche de
        /// dezenas de prédios no mesmo frame.
        /// </summary>
        private bool DeveExpandirInfraestrutura(IA01StrategicRole role, int estoque, int minimo, int limite, ref int ultimoDia)
        {
            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null || context == null) return false;
            int existentes = manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, role);
            if (existentes >= limite) return false;
            if (existentes == 0) return true;
            if (estoque >= minimo) return false;

            int dia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            if (dia <= 0)
            {
                return ultimoDia < 0;
            }
            if (ultimoDia == dia) return false;
            ultimoDia = dia;
            return true;
        }

        private bool PublishBuildNeed(IA01IntentBoard board, float now, IA01NationProfile profile, IA01IntentType intent, IA01NationStage stage, IA01NationPosture posture, int structures, bool threatened, bool atWar, bool shouldPublish, string reason)
        {
            if (!shouldPublish)
            {
                return false;
            }

            int priority = profile != null
                ? profile.ResolveIntentPriority(intent, stage, posture, structures, threatened, atWar)
                : ResolveFallbackPriority(intent, stage, posture, structures, threatened, atWar);

            if (priority <= 0)
            {
                return false;
            }

            board.Publish(intent, priority, reason, now);
            return true;
        }

        private static bool IsDevelopmentStage(IA01NationStage stage)
        {
            return stage >= IA01NationStage.UrbanDevelopment && stage <= IA01NationStage.GlobalPower;
        }

        private int ResolveFallbackPriority(IA01IntentType intent, IA01NationStage stage, IA01NationPosture posture, int structures, bool threatened, bool atWar)
        {
            int priority = intent == IA01IntentType.BuildEnergy ? 520
                : intent == IA01IntentType.BuildFoodProduction ? 500
                : intent == IA01IntentType.BuildResidentialCapacity ? 480
                : intent == IA01IntentType.BuildStorage ? 460
                : intent == IA01IntentType.BuildLogistics ? 440
                : intent == IA01IntentType.BuildIndustry ? 540
                : intent == IA01IntentType.BuildDefense ? 510
                : 0;

            if (priority <= 0)
            {
                return 0;
            }

            priority += structures <= 1 ? 70 : structures == 2 ? 50 : structures == 3 ? 35 : structures == 4 ? 25 : 15;

            switch (stage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    priority += intent == IA01IntentType.BuildEnergy ? 120 : intent == IA01IntentType.BuildFoodProduction ? 90 : intent == IA01IntentType.BuildResidentialCapacity ? 50 : intent == IA01IntentType.BuildStorage ? 15 : 10;
                    break;
                case IA01NationStage.Survival:
                    priority += intent == IA01IntentType.BuildEnergy ? 90 : intent == IA01IntentType.BuildFoodProduction ? 110 : intent == IA01IntentType.BuildResidentialCapacity ? 80 : intent == IA01IntentType.BuildStorage ? 25 : 15;
                    break;
                case IA01NationStage.Stabilization:
                    priority += intent == IA01IntentType.BuildEnergy ? 45 : intent == IA01IntentType.BuildFoodProduction ? 55 : intent == IA01IntentType.BuildResidentialCapacity ? 60 : intent == IA01IntentType.BuildStorage ? 85 : 75;
                    break;
                case IA01NationStage.UrbanDevelopment:
                    priority += intent == IA01IntentType.BuildEnergy ? 25 : intent == IA01IntentType.BuildFoodProduction ? 25 : intent == IA01IntentType.BuildResidentialCapacity ? 45 : intent == IA01IntentType.BuildStorage ? 95 : 105;
                    break;
                case IA01NationStage.Industrialization:
                    priority += intent == IA01IntentType.BuildEnergy ? 20 : intent == IA01IntentType.BuildFoodProduction ? 10 : intent == IA01IntentType.BuildResidentialCapacity ? 20 : intent == IA01IntentType.BuildStorage ? 115 : 110;
                    break;
                case IA01NationStage.Specialization:
                    priority += intent == IA01IntentType.BuildEnergy ? 10 : intent == IA01IntentType.BuildFoodProduction ? 5 : intent == IA01IntentType.BuildResidentialCapacity ? 10 : intent == IA01IntentType.BuildStorage ? 125 : 125;
                    break;
                case IA01NationStage.RegionalProjection:
                    priority += intent == IA01IntentType.BuildEnergy ? 5 : intent == IA01IntentType.BuildFoodProduction ? 5 : intent == IA01IntentType.BuildResidentialCapacity ? 5 : intent == IA01IntentType.BuildStorage ? 120 : 135;
                    break;
                case IA01NationStage.GlobalPower:
                    priority += intent == IA01IntentType.BuildEnergy ? 5 : intent == IA01IntentType.BuildFoodProduction ? 5 : intent == IA01IntentType.BuildResidentialCapacity ? 5 : intent == IA01IntentType.BuildStorage ? 110 : 140;
                    break;
                case IA01NationStage.Recovering:
                    priority += intent == IA01IntentType.BuildEnergy ? 120 : intent == IA01IntentType.BuildFoodProduction ? 120 : intent == IA01IntentType.BuildResidentialCapacity ? 100 : intent == IA01IntentType.BuildStorage ? 45 : 30;
                    break;
                case IA01NationStage.Emergency:
                    priority += intent == IA01IntentType.BuildEnergy ? 20 : intent == IA01IntentType.BuildFoodProduction ? 20 : intent == IA01IntentType.BuildResidentialCapacity ? 20 : intent == IA01IntentType.BuildStorage ? 10 : 5;
                    break;
            }

            switch (posture)
            {
                case IA01NationPosture.Development:
                    priority += intent == IA01IntentType.BuildEnergy ? 20 : intent == IA01IntentType.BuildFoodProduction ? 20 : intent == IA01IntentType.BuildResidentialCapacity ? 15 : 0;
                    break;
                case IA01NationPosture.Peace:
                    priority += intent == IA01IntentType.BuildResidentialCapacity ? 20 : intent == IA01IntentType.BuildStorage ? 10 : 0;
                    break;
                case IA01NationPosture.Alert:
                    priority += intent == IA01IntentType.BuildStorage ? 20 : intent == IA01IntentType.BuildLogistics ? 25 : 0;
                    break;
                case IA01NationPosture.Preparation:
                    priority += intent == IA01IntentType.BuildStorage ? 25 : intent == IA01IntentType.BuildLogistics ? 30 : intent == IA01IntentType.BuildEnergy ? 10 : 0;
                    break;
                case IA01NationPosture.Defense:
                    priority += intent == IA01IntentType.BuildStorage ? 20 : intent == IA01IntentType.BuildResidentialCapacity ? 10 : 0;
                    break;
                case IA01NationPosture.LimitedAttack:
                    priority += intent == IA01IntentType.BuildLogistics ? 30 : intent == IA01IntentType.BuildStorage ? 20 : 0;
                    break;
                case IA01NationPosture.War:
                    priority += intent == IA01IntentType.BuildLogistics ? 35 : intent == IA01IntentType.BuildStorage ? 25 : 0;
                    break;
                case IA01NationPosture.Retreat:
                case IA01NationPosture.Recovery:
                    priority += intent == IA01IntentType.BuildEnergy ? 20 : intent == IA01IntentType.BuildFoodProduction ? 20 : intent == IA01IntentType.BuildResidentialCapacity ? 20 : 0;
                    break;
            }

            if (threatened || atWar)
            {
                priority = Mathf.RoundToInt(priority * 0.75f);
            }

            return Mathf.Clamp(priority, 0, 999);
        }

        private string BuildStatus(IA01NationStage stage, IA01NationPosture posture, bool threatened, bool atWar, bool emergencyReserve, int structures)
        {
            if (threatened)
            {
                return "Cidade sob ameaca: priorizando defesa e apoio.";
            }

            if (atWar)
            {
                return "Cidade em guerra: reforcando suporte operacional.";
            }

            if (emergencyReserve)
            {
                return "Reserva critica: priorizando sobrevivencia.";
            }

            switch (stage)
            {
                case IA01NationStage.Initialization:
                case IA01NationStage.Reconnaissance:
                    return "Planejamento inicial: fundacao e energia.";
                case IA01NationStage.Survival:
                    return "Planejamento de sobrevivencia: comida, moradia e energia.";
                case IA01NationStage.Stabilization:
                    return "Planejamento de estabilizacao: armazenamento e logistica.";
                case IA01NationStage.UrbanDevelopment:
                    return "Planejamento urbano: consolidando infraestrutura.";
                case IA01NationStage.Industrialization:
                case IA01NationStage.Specialization:
                    return "Planejamento industrial: ampliando producao e suporte.";
                case IA01NationStage.RegionalProjection:
                case IA01NationStage.GlobalPower:
                    return "Planejamento expansivo: projetando poder.";
                case IA01NationStage.Recovering:
                    return "Planejamento de recuperacao: reerguendo a cidade.";
                case IA01NationStage.Emergency:
                    return "Planejamento de emergencia: resposta rapida.";
                default:
                    return "Planejamento ativo: estruturas=" + structures + " postura=" + posture + ".";
            }
        }

        private bool IsOwnCapital(MarcadorTerritorio marker)
        {
            IdentidadeUnidade identity = marker != null ? marker.GetComponent<IdentidadeUnidade>() : null;
            return marker != null && marker.ehPrefeitura && identity != null && identity.teamID == context.TeamId;
        }

        private bool TryUseOwnCapital(ComplexoGovernamental candidate, out MarcadorTerritorio marker)
        {
            marker = null;
            IdentidadeUnidade identity = candidate != null ? candidate.GetComponent<IdentidadeUnidade>() : null;
            if (identity == null || identity.teamID != context.TeamId)
            {
                return false;
            }

            marker = candidate.GetComponent<MarcadorTerritorio>();
            if (marker == null) marker = candidate.gameObject.AddComponent<MarcadorTerritorio>();
            marker.ConfigureOwnership(context.TeamId, true, 300f);
            if (GerenteDeTerritorio.Instancia != null) GerenteDeTerritorio.Instancia.RegistrarMarcador(marker);
            GerenciadorDivisaoTerritorial.GarantirInstancia();
            if (GerenciadorDivisaoTerritorial.Instancia != null) GerenciadorDivisaoTerritorial.Instancia.RegistrarCidade(marker);
            return true;
        }
    }

    public sealed class IA01BuildCatalogAdapter
    {
        private static readonly List<DadosConstrucao> EmptyCatalog = new List<DadosConstrucao>(0);
        private readonly DadosConstrucao explicitCapital;
        private readonly IA01BuildPlan buildPlan;
        private readonly List<DadosConstrucao> cachedCatalog = new List<DadosConstrucao>(128);
        private readonly List<IA01BuildDefinition> cachedDefinitions = new List<IA01BuildDefinition>(128);
        private readonly Dictionary<string, IA01BuildDefinition> itemsById = new Dictionary<string, IA01BuildDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IA01StrategicRole, List<IA01BuildDefinition>> itemsByRole = new Dictionary<IA01StrategicRole, List<IA01BuildDefinition>>();
        private readonly Dictionary<IA01BuildArchetype, List<IA01BuildDefinition>> itemsByArchetype = new Dictionary<IA01BuildArchetype, List<IA01BuildDefinition>>();
        private readonly Dictionary<IA01BuildDomain, List<IA01BuildDefinition>> itemsByDomain = new Dictionary<IA01BuildDomain, List<IA01BuildDefinition>>();
        private List<DadosConstrucao> cachedSource;
        private DadosConstrucao cachedExplicitCapital;
        private IA01BuildPlan cachedBuildPlan;
        private int cachedSourceCount = -1;
        private int cachedBuildPlanStepCount = -1;
        private bool cacheValid;
        private int catalogVersion;
        private int indexBuildCount;
        private int intentQueryCount;
        private int candidateReadCount;

        public int CatalogVersion
        {
            get
            {
                EnsureIndex();
                return catalogVersion;
            }
        }

        public string LastDiagnostic { get; private set; } = "Catalogo ainda nao consultado.";
        public bool LastUsedFallback { get; private set; }
        public int QueryCount => intentQueryCount;
        public int IndexBuildCount => indexBuildCount;
        public int IntentQueryCount => intentQueryCount;
        public int CandidateReadCount => candidateReadCount;
        public string CapitalItemIdStatus { get; private set; } = "n/d";
        public string CapitalPrefabStatus { get; private set; } = "n/d";

        public IA01BuildCatalogAdapter(DadosConstrucao explicitCapital, IA01BuildPlan buildPlan)
        {
            this.explicitCapital = explicitCapital;
            this.buildPlan = buildPlan;
        }

        public bool TryGetCapital(out IA01BuildDefinition definition)
        {
            intentQueryCount++;
            ResetDiagnostic();
            EnsureIndex();
            if (explicitCapital != null && TryCreate(explicitCapital, IA01BuildArchetype.Command, out definition))
            {
                if (IsCapitalCandidate(explicitCapital))
                {
                    MarkCapital("blueprint explicito", definition);
                    return true;
                }

                definition = null;
                LastDiagnostic = "CatalogMissing: CapitalBlueprint sem StrategicRole Capital/Government/Command.";
                return false;
            }

            if (itemsByRole.TryGetValue(IA01StrategicRole.Capital, out List<IA01BuildDefinition> capitalItems))
            {
                for (int i = 0; i < capitalItems.Count; i++)
                {
                    candidateReadCount++;
                    IA01BuildDefinition candidate = capitalItems[i];
                    if (candidate != null && candidate.Item != null)
                    {
                        definition = candidate;
                        MarkCapital("StrategicRole.Capital", definition);
                        return true;
                    }
                }
            }

            if (itemsByRole.TryGetValue(IA01StrategicRole.Government, out List<IA01BuildDefinition> governmentItems))
            {
                for (int i = 0; i < governmentItems.Count; i++)
                {
                    candidateReadCount++;
                    IA01BuildDefinition candidate = governmentItems[i];
                    if (candidate != null && candidate.Item != null)
                    {
                        definition = candidate;
                        MarkCapital("StrategicRole.Government", definition);
                        return true;
                    }
                }
            }

            if (itemsByRole.TryGetValue(IA01StrategicRole.Command, out List<IA01BuildDefinition> commandItems))
            {
                for (int i = 0; i < commandItems.Count; i++)
                {
                    candidateReadCount++;
                    IA01BuildDefinition candidate = commandItems[i];
                    if (candidate != null && candidate.Item != null && IsCapitalCandidate(candidate.Item))
                    {
                        definition = candidate;
                        MarkCapital("StrategicRole.Command", definition);
                        return true;
                    }
                }
            }

            definition = null;
            LastDiagnostic = cachedCatalog.Count == 0
                ? "Catalogo vazio: nao foi possivel localizar a prefeitura."
                : "Catalogo sem prefeitura valida. Configure Capital Blueprint ou marque um item com prefeitura/governo/capital.";
            return false;
        }

        public bool TryGetForBlueprint(DadosConstrucao item, out IA01BuildDefinition definition)
        {
            definition = null;
            if (item == null)
            {
                LastDiagnostic = "Ficha de construcao ausente no BuildPlan.";
                return false;
            }

            if (!TryCreateAny(item, out definition) && !TryCreateBuildPlanDefinition(item, out definition))
            {
                LastDiagnostic = "Ficha '" + item.name + "' nao representa uma estrutura valida.";
                return false;
            }

            definition.CatalogResolution = "BuildPlan";
            return true;
        }

        private void MarkCapital(string source, IA01BuildDefinition definition)
        {
            MarkExact(source, definition);
            CapitalItemIdStatus = definition.ItemId;
            CapitalPrefabStatus = definition.Item != null && definition.Item.prefabDaUnidade != null ? definition.Item.prefabDaUnidade.name : "n/d";
        }

        public bool TryGetForIntent(IA01IntentType intent, DadosPaisGoverno country, IA01NationStage stage, out IA01BuildDefinition definition)
        {
            return TryGetForIntent(intent, country, stage, false, out definition);
        }

        public bool TryGetForIntent(IA01IntentType intent, DadosPaisGoverno country, IA01NationStage stage, bool allowFoundationBudgetOverride, out IA01BuildDefinition definition)
        {
            intentQueryCount++;
            ResetDiagnostic();
            EnsureIndex();
            if (IsForcedOpeningIntent(intent) && TryGetForcedOpeningDefinition(intent, out definition))
            {
                MarkExact("BuildPlan abertura", definition);
                return true;
            }

            IA01BuildDefinition bestExact = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < cachedDefinitions.Count; i++)
            {
                candidateReadCount++;
                IA01BuildDefinition candidate = cachedDefinitions[i];
                if (!IsCandidateAllowedForIntent(candidate, intent, stage))
                {
                    continue;
                }

                if (!allowFoundationBudgetOverride && country != null && candidate.Cost > country.saldo)
                {
                    continue;
                }

                int priority = intent == IA01IntentType.BuildDefense
                    ? (candidate.StrategicRole == IA01StrategicRole.AntiAirDefense ? 100 : 10)
                    : ResolveInfrastructurePriority(intent, candidate);
                if (bestExact == null
                    || priority > bestPriority
                    || (priority == bestPriority && candidate.Cost < bestExact.Cost))
                {
                    bestExact = candidate;
                    bestPriority = priority;
                }
            }

            if (bestExact != null)
            {
                definition = bestExact;
                MarkExact("item compativel", definition);
                return true;
            }

            definition = null;
            LastDiagnostic = cachedCatalog.Count == 0
                ? "NoValidCatalogItem: catalogo vazio para " + intent + "."
                : "NoValidCatalogItem: catalogo sem item compatível para " + intent + ".";
            return false;
        }

        private static int ResolveInfrastructurePriority(IA01IntentType intent, IA01BuildDefinition candidate)
        {
            if (candidate == null || candidate.Item == null) return 0;
            string text = IA_Text.Normalize(candidate.Item.GetDisplayName() + " " + candidate.Item.name + " " + candidate.Item.aliases);
            if (intent == IA01IntentType.BuildEnergy)
            {
                // Uma fonte de alta capacidade reduz a quantidade de usinas e
                // preserva área do mapa; nuclear só entra se a IA puder pagá-la.
                if (text.Contains("nuclear") || text.Contains("nucleo") || text.Contains("reator") || text.Contains("reator")) return 300;
                if (text.Contains("hidro") || text.Contains("hydro") || text.Contains("termica") || text.Contains("thermal")) return 180;
                return 40;
            }
            if (intent == IA01IntentType.BuildFoodProduction)
            {
                return text.Contains("fazenda") || text.Contains("farm") ? 120 : 20;
            }
            return 0;
        }

        private static bool IsForcedOpeningIntent(IA01IntentType intent)
        {
            return intent == IA01IntentType.BuildMilitaryAirport
                || intent == IA01IntentType.BuildCommercialAirport
                || intent == IA01IntentType.BuildShipyard
                || intent == IA01IntentType.BuildMilitaryTent
                || intent == IA01IntentType.BuildVehicleConstructor;
        }

        private bool TryGetForcedOpeningDefinition(IA01IntentType intent, out IA01BuildDefinition definition)
        {
            definition = null;
            string[] tokens = ForcedOpeningTokens(intent);
            if (tokens == null || tokens.Length == 0)
            {
                return false;
            }

            // Alguns itens antigos reutilizam o nome "Tenda Militar" em
            // fichas de defesa. Na abertura, o ID estavel e a unica forma
            // segura de nao transformar uma torreta ou um alias em quartel,
            // nem uma unidade aerea em construtor terrestre.
            string preferredItemId = PreferredForcedOpeningItemId(intent);
            if (!string.IsNullOrWhiteSpace(preferredItemId))
            {
                for (int i = 0; i < cachedDefinitions.Count; i++)
                {
                    IA01BuildDefinition candidate = cachedDefinitions[i];
                    if (candidate == null || candidate.Item == null
                        || !string.Equals(candidate.Item.GetStableId(), preferredItemId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (TryCreateForcedOpeningDefinition(intent, candidate.Item, out definition)) return true;
                }
            }

            for (int i = 0; i < cachedDefinitions.Count; i++)
            {
                IA01BuildDefinition candidate = cachedDefinitions[i];
                if (candidate != null && HasCandidateToken(candidate, tokens))
                {
                    if (TryCreateForcedOpeningDefinition(intent, candidate.Item, out definition))
                    {
                        return true;
                    }

                    definition = candidate;
                    definition.Archetype = ResolveArchetype(intent);
                    definition.Domain = ResolveDomain(intent, definition.Item);
                    definition.StrategicRole = ResolveStrategicRole(intent);
                    definition.MinimumStage = IA01NationStage.Initialization;
                    definition.MinimumTreasury = 0;
                    definition.MaximumRecommendedCount = 1;
                    definition.CatalogResolution = "BuildPlan abertura forcada";
                    return true;
                }
            }

            if (buildPlan == null || buildPlan.Steps == null)
            {
                LastDiagnostic = "NoValidCatalogItem: BuildPlan ausente para abertura " + intent + ".";
                return false;
            }

            IReadOnlyList<IA01BuildPlanStep> steps = buildPlan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
                DadosConstrucao item = step != null ? step.constructionData : null;
                if (item == null)
                {
                    continue;
                }

                string text = IA_Text.Normalize((step.StepId ?? string.Empty) + " "
                    + (item.GetStableId() ?? string.Empty) + " "
                    + (item.GetDisplayName() ?? string.Empty) + " "
                    + (item.aliases ?? string.Empty));
                if (!ContainsAny(text, tokens) || !TryCreateForcedOpeningDefinition(intent, item, out definition))
                {
                    continue;
                }

                definition.CatalogResolution = "BuildPlan abertura direta";
                return true;
            }

            LastDiagnostic = "NoValidCatalogItem: BuildPlan sem ficha direta para abertura " + intent + ".";
            return false;
        }

        private static string PreferredForcedOpeningItemId(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildMilitaryTent: return "militar.tenda";
                case IA01IntentType.BuildVehicleConstructor: return "militar.fabrica_veiculos";
                case IA01IntentType.BuildShipyard: return "naval.estaleiro";
                case IA01IntentType.BuildMilitaryAirport: return "aeroporto_militar";
                case IA01IntentType.BuildCommercialAirport: return "aeroporto_comercial";
                default: return string.Empty;
            }
        }

        private static bool TryCreateForcedOpeningDefinition(IA01IntentType intent, DadosConstrucao item, out IA01BuildDefinition definition)
        {
            definition = null;
            if (item == null || !item.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                return false;
            }

            IA01BuildArchetype archetype = ResolveArchetype(intent);
            IA01BuildDomain domain = ResolveDomain(intent, item);
            IA01StrategicRole role = ResolveStrategicRole(intent);
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA01BuildDefinition
            {
                Item = item,
                ItemId = item.GetStableId(),
                DisplayName = item.GetDisplayName(),
                Archetype = archetype,
                StrategicRole = role,
                Domain = domain,
                IsStructure = true,
                Cost = Mathf.Max(0, item.preco),
                MinimumTreasury = 0,
                Footprint = new Vector2(Mathf.Max(8f, bounds.size.x), Mathf.Max(8f, bounds.size.z)),
                RequiresRoad = RequiresRoadConnection(domain, archetype),
                RequiresNavalExit = domain == IA01BuildDomain.Coastal || domain == IA01BuildDomain.Water,
                RequiresPower = false,
                IsFixedDefense = false,
                MaximumRecommendedCount = 1,
                MinimumStage = IA01NationStage.Initialization,
                CatalogResolution = "BuildPlan abertura forcada"
            };
            return true;
        }

        private static bool TryCreateBuildPlanDefinition(DadosConstrucao item, out IA01BuildDefinition definition)
        {
            definition = null;
            if (item == null || !item.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                return false;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            IA01BuildArchetype archetype = InferArchetype(item, prefab);
            IA01BuildDomain domain = InferDomain(capabilities, prefab);
            // Defesa antiaerea e uma instalacao terrestre. Alguns prefabs antigos
            // carregam a capability Air por causa do alvo que defendem; isso nao
            // deve transforma-los em aeroportos nem exigir slot de pista.
            if (IsAntiAirItem(item, capabilities))
            {
                archetype = IA01BuildArchetype.Defense;
                domain = IA01BuildDomain.Land;
            }
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA01BuildDefinition
            {
                Item = item,
                ItemId = item.GetStableId(),
                DisplayName = item.GetDisplayName(),
                Archetype = archetype,
                Domain = domain,
                IsStructure = true,
                Cost = Mathf.Max(0, item.preco),
                Footprint = new Vector2(Mathf.Max(8f, bounds.size.x), Mathf.Max(8f, bounds.size.z)),
                RequiresRoad = RequiresRoadConnection(domain, archetype),
                RequiresNavalExit = domain == IA01BuildDomain.Coastal || domain == IA01BuildDomain.Water,
                StrategicRole = item.strategicRole,
                MinimumStage = ResolveMinimumStage(item.strategicRole)
            };
            definition.StrategicRole = ResolveStrategicRole(definition, item);
            definition.MinimumStage = ResolveMinimumStage(definition.StrategicRole);
            definition.MinimumTreasury = Mathf.Max(0, definition.Cost);
            definition.RequiresPower = definition.StrategicRole == IA01StrategicRole.EnergyProduction;
            definition.IsFixedDefense = definition.StrategicRole == IA01StrategicRole.FixedDefense
                || definition.StrategicRole == IA01StrategicRole.AntiAirDefense
                || definition.StrategicRole == IA01StrategicRole.CoastalDefense;
            definition.MaximumRecommendedCount = ResolveMaximumRecommendedCount(definition.StrategicRole);
            return definition.StrategicRole != IA01StrategicRole.None || definition.IsStructure;
        }

        private static IA01BuildDomain ResolveDomain(IA01IntentType intent, DadosConstrucao item)
        {
            switch (intent)
            {
                case IA01IntentType.BuildMilitaryAirport:
                case IA01IntentType.BuildCommercialAirport:
                    return IA01BuildDomain.Airfield;
                case IA01IntentType.BuildShipyard:
                    if (item != null && item.TryGetPrefabBasico(out GameObject prefab) && prefab != null)
                    {
                        return NavalPlacementResolver.RequiresCoastalPlacement(prefab) ? IA01BuildDomain.Coastal : IA01BuildDomain.Water;
                    }

                    return IA01BuildDomain.Coastal;
                case IA01IntentType.BuildPier:
                case IA01IntentType.BuildOffshorePlatform:
                    return IA01BuildDomain.Coastal;
                default:
                    return IA01BuildDomain.Land;
            }
        }

        private static string[] ForcedOpeningTokens(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildMilitaryAirport:
                    return new[] { "aeroporto_militar", "aeroporto militar", "military airport", "base aerea militar" };
                case IA01IntentType.BuildCommercialAirport:
                    return new[] { "aeroporto_comercial", "aeroporto comercial", "commercial airport", "terminal civil" };
                case IA01IntentType.BuildShipyard:
                    return new[] { "estaleiro", "shipyard", "naval yard" };
                case IA01IntentType.BuildPier:
                    return new[] { "pier" };
                case IA01IntentType.BuildOffshorePlatform:
                    return new[] { "plataforma", "offshore" };
                case IA01IntentType.BuildMilitaryTent:
                    return new[] { "tenda", "tent", "quartel", "barracks" };
                case IA01IntentType.BuildVehicleConstructor:
                    return new[] { "construtor", "veiculo", "vehicle factory" };
                default:
                    return null;
            }
        }

        private void ResetDiagnostic()
        {
            LastUsedFallback = false;
            LastDiagnostic = string.Empty;
        }

        private void MarkExact(string source, IA01BuildDefinition definition)
        {
            LastUsedFallback = false;
            definition.UsedCatalogFallback = false;
            LastDiagnostic = "Catalogo: " + source + " -> " + definition.DisplayName + ".";
            definition.CatalogResolution = LastDiagnostic;
        }

        private void EnsureIndex()
        {
            List<DadosConstrucao> source = ResolveCatalog();
            int count = source != null ? source.Count : 0;
            int buildPlanStepCount = buildPlan != null && buildPlan.Steps != null ? buildPlan.Steps.Count : 0;
            if (cacheValid
                && ReferenceEquals(cachedSource, source)
                && cachedSourceCount == count
                && ReferenceEquals(cachedExplicitCapital, explicitCapital)
                && ReferenceEquals(cachedBuildPlan, buildPlan)
                && cachedBuildPlanStepCount == buildPlanStepCount)
            {
                return;
            }

            cachedSource = source;
            cachedSourceCount = count;
            cachedExplicitCapital = explicitCapital;
            cachedBuildPlan = buildPlan;
            cachedBuildPlanStepCount = buildPlanStepCount;
            cacheValid = true;

            cachedCatalog.Clear();
            cachedDefinitions.Clear();
            itemsById.Clear();
            itemsByRole.Clear();
            itemsByArchetype.Clear();
            itemsByDomain.Clear();
            int versionHash = 17;

            if (source == null)
            {
                catalogVersion = 0;
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                TryIndexItem(source[i], ref versionHash);
            }

            if (buildPlan != null && buildPlan.Steps != null)
            {
                IReadOnlyList<IA01BuildPlanStep> steps = buildPlan.Steps;
                for (int i = 0; i < steps.Count; i++)
                {
                    IA01BuildPlanStep step = steps[i];
                    TryIndexItem(step != null ? step.constructionData : null, ref versionHash);
                }
            }

            catalogVersion = versionHash;
            indexBuildCount++;
        }

        private bool TryIndexItem(DadosConstrucao item, ref int versionHash)
        {
            if (item == null)
            {
                return false;
            }

            if (!TryCreateAny(item, out IA01BuildDefinition definition))
            {
                return false;
            }

            string id = definition.ItemId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(id) && itemsById.ContainsKey(id))
            {
                return false;
            }

            definition.StrategicRole = ResolveStrategicRole(definition, item);
            definition.MinimumStage = ResolveMinimumStage(definition.StrategicRole);
            definition.MinimumTreasury = Mathf.Max(0, definition.Cost);
            definition.RequiresPower = definition.StrategicRole == IA01StrategicRole.EnergyProduction;
            definition.IsFixedDefense = definition.StrategicRole == IA01StrategicRole.FixedDefense
                || definition.StrategicRole == IA01StrategicRole.AntiAirDefense
                || definition.StrategicRole == IA01StrategicRole.CoastalDefense;
            definition.MaximumRecommendedCount = ResolveMaximumRecommendedCount(definition.StrategicRole);
            cachedCatalog.Add(item);
            cachedDefinitions.Add(definition);
            versionHash = unchecked(versionHash * 31 + (definition.ItemId != null ? definition.ItemId.GetHashCode() : 0));

            if (!string.IsNullOrWhiteSpace(id))
            {
                itemsById[id] = definition;
            }

            AddToIndex(itemsByRole, definition.StrategicRole, definition);
            AddToIndex(itemsByArchetype, definition.Archetype, definition);
            AddToIndex(itemsByDomain, definition.Domain, definition);
            return true;
        }

        private static void AddToIndex<TKey>(Dictionary<TKey, List<IA01BuildDefinition>> index, TKey key, IA01BuildDefinition definition)
        {
            if (index == null || definition == null)
            {
                return;
            }

            if (!index.TryGetValue(key, out List<IA01BuildDefinition> list))
            {
                list = new List<IA01BuildDefinition>(8);
                index[key] = list;
            }

            list.Add(definition);
        }

        private static IA01StrategicRole ResolveStrategicRole(IA01BuildDefinition definition, DadosConstrucao item)
        {
            if (definition == null || item == null)
            {
                return IA01StrategicRole.None;
            }

            if (item.strategicRole != IA01StrategicRole.None)
            {
                return item.strategicRole;
            }

            string semanticText = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.nomeItem ?? string.Empty) + " "
                + (item.aliases ?? string.Empty));
            IA_ConstructionCapability semanticCapabilities = item.GetResolvedCapabilities();
            if ((semanticCapabilities & IA_ConstructionCapability.Defense) != 0
                && ContainsAny(semanticText, "antiaerea", "anti aerea", "anti-air", "antiair", "air defense", "defesa aerea"))
            {
                return IA01StrategicRole.AntiAirDefense;
            }
            if (ContainsAny(semanticText, "aeroporto", "airport", "airbase", "base_aerea"))
            {
                return IA01StrategicRole.Airfield;
            }

            if (ContainsAny(semanticText, "estaleiro", "shipyard", "naval", "porto_naval"))
            {
                return IA01StrategicRole.NavalBase;
            }

            if (ContainsAny(semanticText, "farm", "fazenda", "agri", "comida", "food", "cultivo"))
            {
                return IA01StrategicRole.FoodProduction;
            }

            if (ContainsAny(semanticText, "casa", "resid", "moradia", "village", "imovel"))
            {
                return IA01StrategicRole.Residential;
            }

            if (ContainsAny(semanticText, "rua", "road", "estrada", "street", "avenida", "logistica", "logistics"))
            {
                return IA01StrategicRole.Logistics;
            }

            if (ContainsAny(semanticText, "fabrica", "factory", "industria", "industry"))
            {
                return IA01StrategicRole.Industrial;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            bool hasExplicitCapabilities = item.capacidades != IA_ConstructionCapability.Auto;
            if (hasExplicitCapabilities
                && (capabilities & IA_ConstructionCapability.Defense) != 0
                && (capabilities & IA_ConstructionCapability.Structure) != 0)
            {
                if (definition.Domain == IA01BuildDomain.Coastal || definition.Domain == IA01BuildDomain.Water)
                {
                    return IA01StrategicRole.CoastalDefense;
                }

                if (definition.Archetype == IA01BuildArchetype.Air)
                {
                    return IA01StrategicRole.AntiAirDefense;
                }

                return IA01StrategicRole.FixedDefense;
            }

            if ((capabilities & IA_ConstructionCapability.Power) != 0)
            {
                return IA01StrategicRole.EnergyProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Warehouse) != 0)
            {
                return IA01StrategicRole.Storage;
            }

            if ((capabilities & IA_ConstructionCapability.Factory) != 0)
            {
                return IA01StrategicRole.Industrial;
            }

            if ((capabilities & IA_ConstructionCapability.Barracks) != 0)
            {
                return IA01StrategicRole.MilitaryProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Economy) != 0)
            {
                return IA01StrategicRole.FoodProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Civil) != 0)
            {
                return IA01StrategicRole.Residential;
            }

            if (definition.Archetype == IA01BuildArchetype.Naval)
            {
                return IA01StrategicRole.NavalBase;
            }

            if (definition.Archetype == IA01BuildArchetype.Air)
            {
                return IA01StrategicRole.Airfield;
            }

            if (definition.Archetype == IA01BuildArchetype.Logistics)
            {
                return IA01StrategicRole.Logistics;
            }

            if (definition.Archetype == IA01BuildArchetype.Command)
            {
                return IA01StrategicRole.Command;
            }

            if (definition.Archetype == IA01BuildArchetype.Research)
            {
                return IA01StrategicRole.Research;
            }

            return IA01StrategicRole.None;
        }

        private static IA01NationStage ResolveMinimumStage(IA01StrategicRole role)
        {
            switch (role)
            {
                case IA01StrategicRole.Residential:
                case IA01StrategicRole.FoodProduction:
                case IA01StrategicRole.EnergyProduction:
                case IA01StrategicRole.Storage:
                    return IA01NationStage.Survival;
                case IA01StrategicRole.Logistics:
                    return IA01NationStage.Stabilization;
                case IA01StrategicRole.FixedDefense:
                case IA01StrategicRole.AntiAirDefense:
                case IA01StrategicRole.CoastalDefense:
                case IA01StrategicRole.MilitaryProduction:
                    return IA01NationStage.UrbanDevelopment;
                case IA01StrategicRole.Airfield:
                case IA01StrategicRole.NavalBase:
                case IA01StrategicRole.Industrial:
                    return IA01NationStage.Industrialization;
                case IA01StrategicRole.Research:
                case IA01StrategicRole.Command:
                case IA01StrategicRole.Capital:
                case IA01StrategicRole.Government:
                    return IA01NationStage.Initialization;
                default:
                    return IA01NationStage.Initialization;
            }
        }

        private static int ResolveMaximumRecommendedCount(IA01StrategicRole role)
        {
            switch (role)
            {
                case IA01StrategicRole.FixedDefense:
                case IA01StrategicRole.AntiAirDefense:
                case IA01StrategicRole.CoastalDefense:
                    return 12;
                case IA01StrategicRole.Storage:
                    return 6;
                case IA01StrategicRole.EnergyProduction:
                case IA01StrategicRole.FoodProduction:
                    return 8;
                case IA01StrategicRole.Logistics:
                    return 10;
                default:
                    return 4;
            }
        }

        private static bool IsCandidateAllowedForIntent(IA01BuildDefinition definition, IA01IntentType intent, IA01NationStage stage)
        {
            if (definition == null)
            {
                return false;
            }

            if (!definition.IsStructure)
            {
                return false;
            }

            bool sequenceIntent = intent == IA01IntentType.BuildRoad
                || intent == IA01IntentType.BuildMilitaryAirport
                || intent == IA01IntentType.BuildCommercialAirport
                || intent == IA01IntentType.BuildShipyard
                || intent == IA01IntentType.BuildPier
                || intent == IA01IntentType.BuildOffshorePlatform
                || intent == IA01IntentType.BuildIndustry;
            if (definition.MinimumStage > stage && !sequenceIntent)
            {
                return false;
            }

            switch (intent)
            {
                case IA01IntentType.BuildDefense:
                    return definition.IsFixedDefense;
                case IA01IntentType.BuildEnergy:
                    return definition.StrategicRole == IA01StrategicRole.EnergyProduction;
                case IA01IntentType.BuildFoodProduction:
                    return definition.StrategicRole == IA01StrategicRole.FoodProduction;
                case IA01IntentType.BuildResidentialCapacity:
                    return definition.StrategicRole == IA01StrategicRole.Residential;
                case IA01IntentType.BuildStarterHouse:
                    return definition.StrategicRole == IA01StrategicRole.Residential && IsNamedCandidate(definition, "casa", "house");
                case IA01IntentType.BuildMediumApartment:
                    return definition.StrategicRole == IA01StrategicRole.Residential && IsNamedCandidate(definition, "medio", "médio", "apartamento", "apartment");
                case IA01IntentType.BuildHighApartment:
                    return definition.StrategicRole == IA01StrategicRole.Residential && IsNamedCandidate(definition, "hard", "alto", "high", "torre");
                case IA01IntentType.BuildMilitaryTent:
                    return definition.StrategicRole == IA01StrategicRole.MilitaryProduction && IsNamedCandidate(definition, "tenda", "tent", "quartel", "barracks");
                case IA01IntentType.BuildVehicleConstructor:
                    return definition.StrategicRole == IA01StrategicRole.MilitaryProduction && IsNamedCandidate(definition, "construtor", "veiculo", "veículo", "vehicle");
                case IA01IntentType.BuildStorage:
                    return definition.StrategicRole == IA01StrategicRole.Storage;
                case IA01IntentType.BuildLogistics:
                    return definition.StrategicRole == IA01StrategicRole.Logistics;
                case IA01IntentType.BuildRoad:
                    return definition.StrategicRole == IA01StrategicRole.Logistics
                        && IsRoadCandidate(definition);
                case IA01IntentType.BuildMilitaryAirport:
                    return definition.StrategicRole == IA01StrategicRole.Airfield
                        && IsMilitaryAirportCandidate(definition);
                case IA01IntentType.BuildCommercialAirport:
                    return definition.StrategicRole == IA01StrategicRole.Airfield
                        && IsCommercialAirportCandidate(definition);
                case IA01IntentType.BuildShipyard:
                    return definition.StrategicRole == IA01StrategicRole.NavalBase
                        || definition.StrategicRole == IA01StrategicRole.Shipyard
                        || definition.StrategicRole == IA01StrategicRole.Port
                        || definition.StrategicRole == IA01StrategicRole.Pier;
                case IA01IntentType.BuildPier:
                    return definition.StrategicRole == IA01StrategicRole.Pier
                        || CandidateText(definition).Contains("pier");
                case IA01IntentType.BuildOffshorePlatform:
                    return definition.StrategicRole == IA01StrategicRole.NavalBase
                        && (CandidateText(definition).Contains("plataforma") || CandidateText(definition).Contains("offshore"));
                case IA01IntentType.BuildIndustry:
                    return definition.StrategicRole == IA01StrategicRole.Industrial;
                case IA01IntentType.EstablishCapital:
                    return definition.StrategicRole == IA01StrategicRole.Capital
                        || definition.StrategicRole == IA01StrategicRole.Government
                        || definition.StrategicRole == IA01StrategicRole.Command;
                default:
                    return false;
            }
        }

        private static string CandidateText(IA01BuildDefinition definition)
        {
            if (definition == null || definition.Item == null)
            {
                return string.Empty;
            }

            return IA_Text.Normalize((definition.ItemId ?? string.Empty) + " "
                + (definition.DisplayName ?? string.Empty) + " "
                + (definition.Item.nomeItem ?? string.Empty) + " "
                + (definition.Item.aliases ?? string.Empty));
        }

        private static bool HasCandidateToken(IA01BuildDefinition definition, params string[] tokens)
        {
            string text = CandidateText(definition);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && text.Contains(IA_Text.Normalize(tokens[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRoadCandidate(IA01BuildDefinition definition)
        {
            return HasCandidateToken(definition, "rua", "road", "estrada", "street", "avenida", "logistica", "logistics");
        }

        private static bool IsMilitaryAirportCandidate(IA01BuildDefinition definition)
        {
            return HasCandidateToken(definition, "militar", "military", "aeroporto_militar", "airbase", "base_aerea");
        }

        private static bool IsCommercialAirportCandidate(IA01BuildDefinition definition)
        {
            return HasCandidateToken(definition, "comercial", "commercial", "aeroporto_comercial", "terminal_aereo");
        }

        private static List<DadosConstrucao> ResolveCatalog()
        {
            MenuConstrucao menu = MenuConstrucao.Instancia;
            if (menu != null)
            {
                menu.GarantirCatalogoParaIA();
            }

            if (MenuConstrucao.catalogoGlobal != null && MenuConstrucao.catalogoGlobal.Count > 0)
            {
                return MenuConstrucao.catalogoGlobal;
            }

            if (menu != null && menu.catalogo != null && menu.catalogo.Count > 0)
            {
                return menu.catalogo;
            }

            CatalogoProdutoCompartilhado.SincronizarFontesVivas();
            return MenuConstrucao.catalogoGlobal != null && MenuConstrucao.catalogoGlobal.Count > 0
                ? MenuConstrucao.catalogoGlobal
                : EmptyCatalog;
        }

        private static IA01BuildArchetype ResolveArchetype(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.BuildEnergy: return IA01BuildArchetype.Energy;
                case IA01IntentType.BuildResidentialCapacity: return IA01BuildArchetype.Residential;
                case IA01IntentType.BuildFoodProduction: return IA01BuildArchetype.Agricultural;
                case IA01IntentType.BuildStorage: return IA01BuildArchetype.Storage;
                case IA01IntentType.BuildLogistics: return IA01BuildArchetype.Logistics;
                case IA01IntentType.BuildRoad: return IA01BuildArchetype.Logistics;
                case IA01IntentType.BuildMilitaryAirport:
                case IA01IntentType.BuildCommercialAirport: return IA01BuildArchetype.Air;
                case IA01IntentType.BuildShipyard:
                case IA01IntentType.BuildPier:
                case IA01IntentType.BuildOffshorePlatform: return IA01BuildArchetype.Naval;
                case IA01IntentType.BuildStarterHouse:
                case IA01IntentType.BuildMediumApartment:
                case IA01IntentType.BuildHighApartment: return IA01BuildArchetype.Residential;
                case IA01IntentType.BuildMilitaryTent:
                case IA01IntentType.BuildVehicleConstructor: return IA01BuildArchetype.Military;
                case IA01IntentType.BuildIndustry: return IA01BuildArchetype.Industrial;
                case IA01IntentType.BuildDefense: return IA01BuildArchetype.Defense;
                default: return IA01BuildArchetype.Command;
            }
        }

        private static IA01StrategicRole ResolveStrategicRole(IA01IntentType intent)
        {
            switch (intent)
            {
                case IA01IntentType.EstablishCapital:
                    return IA01StrategicRole.Command;
                case IA01IntentType.BuildEnergy:
                    return IA01StrategicRole.EnergyProduction;
                case IA01IntentType.BuildResidentialCapacity:
                    return IA01StrategicRole.Residential;
                case IA01IntentType.BuildFoodProduction:
                    return IA01StrategicRole.FoodProduction;
                case IA01IntentType.BuildStorage:
                    return IA01StrategicRole.Storage;
                case IA01IntentType.BuildLogistics:
                    return IA01StrategicRole.Logistics;
                case IA01IntentType.BuildRoad:
                    return IA01StrategicRole.Logistics;
                case IA01IntentType.BuildMilitaryAirport:
                case IA01IntentType.BuildCommercialAirport:
                    return IA01StrategicRole.Airfield;
                case IA01IntentType.BuildShipyard:
                    return IA01StrategicRole.Shipyard;
                case IA01IntentType.BuildPier:
                    return IA01StrategicRole.Pier;
                case IA01IntentType.BuildOffshorePlatform:
                    return IA01StrategicRole.NavalBase;
                case IA01IntentType.BuildStarterHouse:
                case IA01IntentType.BuildMediumApartment:
                case IA01IntentType.BuildHighApartment:
                    return IA01StrategicRole.Residential;
                case IA01IntentType.BuildMilitaryTent:
                case IA01IntentType.BuildVehicleConstructor:
                    return IA01StrategicRole.MilitaryProduction;
                case IA01IntentType.BuildIndustry:
                    return IA01StrategicRole.Industrial;
                case IA01IntentType.BuildDefense:
                    return IA01StrategicRole.None;
                default:
                    return IA01StrategicRole.None;
            }
        }

        private static bool TryCreate(DadosConstrucao item, IA01BuildArchetype requested, out IA01BuildDefinition definition)
        {
            return TryCreateInternal(item, requested, true, out definition);
        }

        private static bool TryCreateAny(DadosConstrucao item, out IA01BuildDefinition definition)
        {
            return TryCreateInternal(item, IA01BuildArchetype.Command, false, out definition);
        }

        private static bool TryCreateInternal(DadosConstrucao item, IA01BuildArchetype requested, bool requireRequestedArchetype, out IA01BuildDefinition definition)
        {
            definition = null;
            if (item == null || !item.TryGetPrefabBasico(out GameObject prefab) || prefab == null) return false;
            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            // Algumas fichas antigas de defesa terrestre pertencem a categoria
            // Exercito e por isso tambem recebem a flag Unit durante a inferencia.
            // Se possuem Structure + Defense, continuam sendo construcoes validas
            // para a IA (a unidade de combate e um componente do prefab).
            if ((capabilities & IA_ConstructionCapability.Structure) == 0
                || ((capabilities & IA_ConstructionCapability.Unit) != 0
                    && (capabilities & IA_ConstructionCapability.Defense) == 0)) return false;

            IA01BuildArchetype inferred = InferArchetype(item, prefab);
            if (requireRequestedArchetype && requested != IA01BuildArchetype.Command && inferred != requested) return false;

            IA01BuildArchetype archetype = requireRequestedArchetype && requested == IA01BuildArchetype.Command
                ? IA01BuildArchetype.Command
                : inferred;
            IA01BuildDomain domain = requireRequestedArchetype && requested == IA01BuildArchetype.Command
                ? IA01BuildDomain.Land
                : InferDomain(capabilities, prefab);
            if (IsAntiAirItem(item, capabilities))
            {
                archetype = IA01BuildArchetype.Defense;
                domain = IA01BuildDomain.Land;
            }
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA01BuildDefinition
            {
                Item = item,
                ItemId = item.GetStableId(),
                DisplayName = item.GetDisplayName(),
                Archetype = archetype,
                Domain = domain,
                IsStructure = (capabilities & IA_ConstructionCapability.Structure) != 0,
                Cost = Mathf.Max(0, item.preco),
                Footprint = new Vector2(Mathf.Max(8f, bounds.size.x), Mathf.Max(8f, bounds.size.z)),
                RequiresRoad = RequiresRoadConnection(domain, archetype),
                RequiresNavalExit = domain == IA01BuildDomain.Coastal || domain == IA01BuildDomain.Water,
                StrategicRole = item.strategicRole,
                MinimumStage = ResolveMinimumStage(item.strategicRole)
            };
            return true;
        }

        private static bool IsCapitalCandidate(DadosConstrucao item)
        {
            if (item == null) return false;
            return item.strategicRole == IA01StrategicRole.Capital
                || item.strategicRole == IA01StrategicRole.Government
                || item.strategicRole == IA01StrategicRole.Command;
        }

        private static int ScoreFallback(IA01IntentType intent, DadosConstrucao item, IA01BuildDefinition candidate)
        {
            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            string key = IA_Text.Normalize(item.GetStableId() + " " + item.GetDisplayName() + " " + item.aliases);
            int score = 0;
            switch (intent)
            {
                case IA01IntentType.BuildEnergy:
                    if ((capabilities & IA_ConstructionCapability.Power) != 0) score += 1200;
                    if (item.categoria == DadosConstrucao.CategoriaItem.Energia) score += 900;
                    if (ContainsAny(key, "usina", "energia", "solar", "nuclear", "power")) score += 600;
                    break;
                case IA01IntentType.BuildFoodProduction:
                    if (ContainsAny(key, "fazenda", "farm", "agri", "comida", "food", "cultivo")) score += 1200;
                    if ((capabilities & IA_ConstructionCapability.Economy) != 0) score += 250;
                    break;
                case IA01IntentType.BuildResidentialCapacity:
                    if ((capabilities & IA_ConstructionCapability.Civil) != 0) score += 1100;
                    if (item.categoria == DadosConstrucao.CategoriaItem.Urbana) score += 350;
                    if (ContainsAny(key, "casa", "resid", "moradia", "imovel", "village")) score += 600;
                    break;
                case IA01IntentType.BuildStorage:
                    if ((capabilities & IA_ConstructionCapability.Warehouse) != 0) score += 1200;
                    if (ContainsAny(key, "armazem", "warehouse", "galpao", "deposito")) score += 650;
                    break;
                case IA01IntentType.BuildLogistics:
                    if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura) score += 800;
                    if (ContainsAny(key, "logistic", "estrada", "road", "ponte", "porto", "pier", "terminal")) score += 650;
                    break;
                case IA01IntentType.BuildIndustry:
                    if ((capabilities & IA_ConstructionCapability.Factory) != 0) score += 1200;
                    if (ContainsAny(key, "fabrica", "factory", "industria", "construtor")) score += 650;
                    break;
                case IA01IntentType.BuildDefense:
                    if ((capabilities & IA_ConstructionCapability.Defense) != 0) score += 1200;
                    if (ContainsAny(key, "bunker", "torre", "sentinela", "radar", "defesa", "antia")) score += 650;
                    break;
            }

            // A generic infrastructure structure is preferable to a stalled city, but only
            // after every specialized candidate has been considered.
            if (score == 0 && item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura)
            {
                score = 100;
            }
            return score;
        }

        private static bool IsNamedCandidate(IA01BuildDefinition definition, params string[] tokens)
        {
            if (definition == null || definition.Item == null || tokens == null) return false;
            string value = IA_Text.Normalize((definition.ItemId ?? string.Empty) + " "
                + (definition.DisplayName ?? string.Empty) + " " + (definition.Item.aliases ?? string.Empty));
            return ContainsAny(value, tokens);
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null) return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && value.Contains(tokens[i])) return true;
            }
            return false;
        }

        private static bool RequiresRoadConnection(IA01BuildDomain domain, IA01BuildArchetype archetype)
        {
            if (domain != IA01BuildDomain.Land) return false;
            return archetype == IA01BuildArchetype.Industrial
                || archetype == IA01BuildArchetype.Logistics
                || archetype == IA01BuildArchetype.Military
                || archetype == IA01BuildArchetype.Defense;
        }

        private static IA01BuildArchetype InferArchetype(DadosConstrucao item, GameObject prefab)
        {
            if (item.strategicRole == IA01StrategicRole.Capital
                || item.strategicRole == IA01StrategicRole.Government
                || item.strategicRole == IA01StrategicRole.Command)
            {
                return IA01BuildArchetype.Command;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            if ((capabilities & IA_ConstructionCapability.Power) != 0) return IA01BuildArchetype.Energy;
            if ((capabilities & IA_ConstructionCapability.Warehouse) != 0) return IA01BuildArchetype.Storage;
            if ((capabilities & IA_ConstructionCapability.Factory) != 0) return IA01BuildArchetype.Industrial;
            if ((capabilities & IA_ConstructionCapability.Barracks) != 0) return IA01BuildArchetype.Military;
            if ((capabilities & IA_ConstructionCapability.Defense) != 0) return IA01BuildArchetype.Defense;
            if ((capabilities & IA_ConstructionCapability.Airport) != 0
                || (capabilities & IA_ConstructionCapability.MilitaryAirport) != 0
                || (capabilities & IA_ConstructionCapability.CommercialAirport) != 0)
            {
                return IA01BuildArchetype.Air;
            }
            if ((capabilities & IA_ConstructionCapability.Economy) != 0) return IA01BuildArchetype.Agricultural;
            if ((capabilities & IA_ConstructionCapability.Civil) != 0) return IA01BuildArchetype.Residential;
            if (item.categoria == DadosConstrucao.CategoriaItem.Energia) return IA01BuildArchetype.Energy;
            if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura) return IA01BuildArchetype.Logistics;
            if (item.categoria == DadosConstrucao.CategoriaItem.Marinha) return IA01BuildArchetype.Naval;
            if (item.categoria == DadosConstrucao.CategoriaItem.Exercito) return IA01BuildArchetype.Military;
            if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica) return IA01BuildArchetype.Air;
            return IA01BuildArchetype.Residential;
        }

        private static IA01BuildDomain InferDomain(IA_ConstructionCapability capabilities, GameObject prefab)
        {
            if ((capabilities & IA_ConstructionCapability.Naval) != 0)
            {
                return NavalPlacementResolver.RequiresCoastalPlacement(prefab) ? IA01BuildDomain.Coastal : IA01BuildDomain.Water;
            }
            if ((capabilities & IA_ConstructionCapability.Air) != 0) return IA01BuildDomain.Airfield;
            return IA01BuildDomain.Land;
        }

        private static bool IsAntiAirItem(DadosConstrucao item, IA_ConstructionCapability capabilities)
        {
            if (item == null) return false;
            if (item.strategicRole == IA01StrategicRole.AntiAirDefense) return true;
            string text = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.nomeItem ?? string.Empty) + " "
                + (item.aliases ?? string.Empty));
            bool defense = (capabilities & IA_ConstructionCapability.Defense) != 0;
            return defense && ContainsAny(text, "antiaerea", "anti aerea", "anti-air", "antiair", "air defense", "defesa aerea");
        }

        private static Bounds ResolveBounds(GameObject prefab)
        {
            Collider collider = prefab.GetComponentInChildren<Collider>(true);
            if (collider != null) return collider.bounds;
            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.bounds : new Bounds(prefab.transform.position, new Vector3(12f, 6f, 12f));
        }
    }

    public sealed class IA01BuildReservationGrid
    {
        private readonly Dictionary<string, IA01LotState> lots = new Dictionary<string, IA01LotState>();
        public int ReservedCount { get; private set; }

        public bool TryReserve(IA01BuildLot lot)
        {
            if (lot == null || string.IsNullOrEmpty(lot.Key) || (lots.TryGetValue(lot.Key, out IA01LotState state) && state != IA01LotState.Free)) return false;
            lots[lot.Key] = IA01LotState.Reserved;
            lot.State = IA01LotState.Reserved;
            ReservedCount++;
            return true;
        }

        public void MarkOccupied(IA01BuildLot lot)
        {
            if (lot == null) return;
            lots[lot.Key] = IA01LotState.Occupied;
            lot.State = IA01LotState.Occupied;
            ReservedCount = Mathf.Max(0, ReservedCount - 1);
        }

        public void Release(IA01BuildLot lot, bool invalid)
        {
            if (lot == null) return;
            lots[lot.Key] = invalid ? IA01LotState.TemporarilyInvalid : IA01LotState.Free;
            lot.State = lots[lot.Key];
            ReservedCount = Mathf.Max(0, ReservedCount - 1);
        }
    }

    public enum IA01IntentBlockReason
    {
        None = 0,
        CatalogMissing = 1,
        NoLot = 2,
        LotBlocked = 3,
        LotReserved = 3,
        Funds = 4,
        Busy = 5,
        Cooldown = 6
    }

    public sealed class IA01IntentCooldown
    {
        public float CooldownUntil;
        public float BlockedUntil;
        public int FailureCount;
        public string LastStateToken = string.Empty;
        public IA01FailureCode LastFailureCode = IA01FailureCode.None;
        public IA01IntentBlockReason LastBlockReason = IA01IntentBlockReason.None;
        public string LastKey = string.Empty;
    }

    public sealed class IA01CircuitBreaker
    {
        private readonly IA01IntentCooldown state = new IA01IntentCooldown();

        public int FailureCount => state.FailureCount;
        public string LastStateToken => state.LastStateToken;
        public IA01FailureCode LastFailureCode => state.LastFailureCode;
        public IA01IntentBlockReason LastBlockReason => state.LastBlockReason;

        public bool CanAttempt(float now, string stateToken)
        {
            if (!string.Equals(state.LastStateToken, stateToken, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (state.BlockedUntil > 0f && now < state.BlockedUntil)
            {
                return false;
            }

            return now >= state.CooldownUntil;
        }

        public void RecordFailure(float now, string stateToken, IA01FailureCode failureCode, IA01IntentBlockReason blockReason)
        {
            if (!string.Equals(state.LastStateToken, stateToken, System.StringComparison.Ordinal))
            {
                state.FailureCount = 0;
                state.BlockedUntil = 0f;
            }

            state.LastStateToken = stateToken ?? string.Empty;
            state.LastFailureCode = failureCode;
            state.LastBlockReason = blockReason;
            state.FailureCount = Mathf.Min(8, state.FailureCount + 1);
            float cooldown = state.FailureCount == 1 ? 6f : state.FailureCount == 2 ? 14f : state.FailureCount == 3 ? 30f : 45f;
            state.CooldownUntil = now + cooldown;
            state.BlockedUntil = state.FailureCount >= 3 ? float.MaxValue : 0f;
        }

        public void Reset()
        {
            state.FailureCount = 0;
            state.CooldownUntil = 0f;
            state.BlockedUntil = 0f;
            state.LastStateToken = string.Empty;
            state.LastFailureCode = IA01FailureCode.None;
            state.LastBlockReason = IA01IntentBlockReason.None;
            state.LastKey = string.Empty;
        }
    }

    public class IA01FailureMemory
    {
        private readonly Dictionary<string, IA01IntentCooldown> failures = new Dictionary<string, IA01IntentCooldown>();

        public bool IsCoolingDown(string key, float now)
        {
            return !CanAttempt(key, now, string.Empty);
        }

        public void Record(string key, float now)
        {
            Record(key, now, string.Empty, IA01FailureCode.NoValidLot, IA01IntentBlockReason.Busy);
        }

        public bool CanAttempt(string key, float now, string stateToken)
        {
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }

            if (!failures.TryGetValue(key, out IA01IntentCooldown record))
            {
                return true;
            }

            if (!string.Equals(record.LastStateToken, stateToken, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (record.BlockedUntil > 0f && now < record.BlockedUntil)
            {
                return false;
            }

            return now >= record.CooldownUntil;
        }

        public void Record(string key, float now, string stateToken, IA01FailureCode failureCode, IA01IntentBlockReason blockReason)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!failures.TryGetValue(key, out IA01IntentCooldown record))
            {
                record = new IA01IntentCooldown();
                failures[key] = record;
            }

            if (!string.Equals(record.LastStateToken, stateToken, System.StringComparison.Ordinal))
            {
                record.FailureCount = 0;
                record.BlockedUntil = 0f;
            }

            record.LastStateToken = stateToken ?? string.Empty;
            record.LastFailureCode = failureCode;
            record.LastBlockReason = blockReason;
            record.LastKey = key + "|" + failureCode;
            record.FailureCount = Mathf.Min(8, record.FailureCount + 1);

            float cooldown = record.FailureCount == 1 ? 6f
                : record.FailureCount == 2 ? 14f
                : record.FailureCount == 3 ? 30f
                : 45f;
            record.CooldownUntil = now + cooldown;
            record.BlockedUntil = record.FailureCount >= 3 ? float.MaxValue : 0f;
        }

        public void Reset(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            failures.Remove(key);
        }

        public void Clear()
        {
            failures.Clear();
        }

        public bool TryGetCooldown(string key, float now, out int failureCount, out IA01IntentBlockReason reason, out float remainingSeconds, out bool requiresStateChange)
        {
            failureCount = 0;
            reason = IA01IntentBlockReason.None;
            remainingSeconds = 0f;
            requiresStateChange = false;
            if (string.IsNullOrEmpty(key) || !failures.TryGetValue(key, out IA01IntentCooldown record))
            {
                return false;
            }

            failureCount = record.FailureCount;
            reason = record.LastBlockReason;
            requiresStateChange = record.BlockedUntil == float.MaxValue;
            remainingSeconds = requiresStateChange ? -1f : Mathf.Max(0f, record.CooldownUntil - now);
            return true;
        }

        public string BuildIntentKey(IA01IntentType intent, IA01StrategicRole role, string regionKey)
        {
            return intent + "|" + role + "|" + (string.IsNullOrWhiteSpace(regionKey) ? "global" : regionKey);
        }

        public string BuildFailureKey(IA01IntentType intent, IA01StrategicRole role, string regionKey, IA01FailureCode failureCode)
        {
            return BuildIntentKey(intent, role, regionKey) + "|" + failureCode;
        }

        public string BuildStateToken(int catalogVersion, int worldVersion, bool threatened, bool atWar, int treasury, int energy, int food)
        {
            return catalogVersion + "|" + worldVersion + "|" + threatened + "|" + atWar + "|" + treasury + "|" + energy + "|" + food;
        }
    }

    public sealed class IA01BuildFailureMemory : IA01FailureMemory
    {
    }

    public sealed class IA01ZonePlanner
    {
        private readonly IA01Controller controller;
        public IA01ZonePlanner(IA01Controller controller) { this.controller = controller; }

        public bool TryResolvePlanningOrigin(IA01CityPlanner city, IA01BuildDefinition definition, out Vector3 origin, out string reason)
        {
            origin = Vector3.zero;
            reason = string.Empty;
            if (definition == null)
            {
                reason = "definição de construção ausente";
                return false;
            }

            if (city != null && city.Capital != null)
            {
                origin = city.Capital.transform.position;
            }
            else
            {
                // Antes o fallback era controller.transform.position. Em uma
                // build fria esse objeto costuma estar perto da câmera e não
                // representa o território da IA. O único fallback permitido é
                // o create oficial da capital já registrado no layout.
                IA01BuildSlot capitalSlot = controller != null ? controller.CapitalSlot : null;
                if (capitalSlot == null)
                {
                    reason = "capital e create oficial da capital ainda não estão prontos";
                    return false;
                }

                Transform point = capitalSlot.BuildingPoint != null ? capitalSlot.BuildingPoint : capitalSlot.transform;
                if (point == null)
                {
                    reason = "create oficial da capital sem ponto de construção";
                    return false;
                }

                origin = point.position;
            }

            // Heavy industry and naval facilities are planned away from the command core.
            if (definition.Archetype == IA01BuildArchetype.Industrial || definition.Archetype == IA01BuildArchetype.Naval) origin += new Vector3(90f, 0f, 0f);
            return true;
        }

        private static bool TrySampleTerrainHeight(Vector3 position, out float height)
        {
            height = 0f;
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null || !terrain.enabled)
                {
                    continue;
                }

                Vector3 minimum = terrain.transform.position;
                Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
                if (position.x < minimum.x || position.x > minimum.x + size.x
                    || position.z < minimum.z || position.z > minimum.z + size.z)
                {
                    continue;
                }

                height = terrain.SampleHeight(position) + terrain.transform.position.y;
                return true;
            }

            return false;
        }
    }

    public sealed class IA01LotPlanner
    {
        private readonly IA01BuildValidator validator;
        public IA01LotPlanner(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01BuildReservationGrid reservations, IA01BuildFailureMemory failures)
        {
            validator = new IA01BuildValidator(controller, context, world, reservations, failures);
        }
        public int CandidatesEvaluated => validator != null ? validator.CandidatesEvaluated : 0;
        public int PhysicsChecks => validator != null ? validator.PhysicsChecks : 0;
        public bool TryFindLot(IA01BuildDefinition definition, Vector3 origin, float now, int maxCandidates, int maxPhysicsChecks, out IA01BuildLot lot, out string reason)
        {
            return validator.TryFindLot(definition, origin, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
        }

        public bool TryFindAnchoredLot(IA01BuildDefinition definition, Vector3 position, Quaternion rotation, int maxPhysicsChecks, out IA01BuildLot lot, out string reason)
        {
            lot = new IA01BuildLot
            {
                Position = position,
                Rotation = rotation,
                Footprint = definition != null ? definition.Footprint : Vector3.one,
                Key = "anchor:" + Mathf.RoundToInt(position.x / 2f) + ":" + Mathf.RoundToInt(position.z / 2f),
                State = IA01LotState.Free
            };
            if (validator.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason)) return true;
            lot = null;
            return false;
        }

        public bool TryValidatePreparedLot(IA01BuildDefinition definition, IA01BuildLot lot, int maxPhysicsChecks, out string reason)
        {
            return validator.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason);
        }

        public bool TryFindLotInBounds(IA01BuildDefinition definition, Bounds bounds, float now, int maxCandidates, int maxPhysicsChecks, out IA01BuildLot lot, out string reason)
        {
            return validator.TryFindLotInBounds(definition, bounds, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
        }
    }

    public sealed class IA01BackendBridge
    {
        private readonly IA01RuntimeContext context;
        public IA01BackendBridge(IA01RuntimeContext context) { this.context = context; }

        public bool TryPay(int cost)
        {
            return TryPay(cost, false);
        }

        public bool TryPay(int cost, bool allowFoundationFundingOverride)
        {
            if (allowFoundationFundingOverride)
            {
                return true;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            return government != null && government.TentarPagar(context.TeamId, cost);
        }

        public void Refund(int cost)
        {
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            if (government != null && cost > 0) government.AdicionarSaldo(context.TeamId, cost);
        }

        public GameObject CreateStructure(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            Construtor builder = UnityEngine.Object.FindFirstObjectByType<Construtor>();
            return builder != null ? builder.ConstruirEstruturaIA(prefab, position, rotation) : UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
    }

    internal static class IA01OperationalRules
    {
        public static bool IsCapitalThreatened(IA01WorldState world, MarcadorTerritorio capital, DadosPaisGoverno country)
        {
            if (world == null || capital == null)
            {
                return false;
            }

            float rangeSqr = 260f * 260f;
            for (int i = 0; i < world.EnemyUnits.Count; i++)
            {
                IdentidadeUnidade enemy = world.EnemyUnits[i];
                if (enemy == null || (country != null && enemy.teamID == country.aliadoPrioritarioTeamId))
                {
                    continue;
                }

                if ((enemy.transform.position - capital.transform.position).sqrMagnitude <= rangeSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class IA01MissionDirector
    {
        private readonly IA01CommandQueue commands;
        public IA01MissionDirector(IA01CommandQueue commands) { this.commands = commands; }
        public void Queue(string id, Func<bool> execute, Action<bool> confirm) => commands.Enqueue(id, execute, confirm);
    }

    public sealed class IA01CommandQueue
    {
        private sealed class Entry
        {
            public string Id;
            public IA01CommandState State;
            public Func<bool> Execute;
            public Action<bool> Confirm;
        }
        private readonly Queue<Entry> entries = new Queue<Entry>();
        public int PendingCount => entries.Count;

        public void Enqueue(string id, Func<bool> execute, Action<bool> confirm)
        {
            entries.Enqueue(new Entry { Id = id, State = IA01CommandState.Queued, Execute = execute, Confirm = confirm });
        }

        public bool ProcessOne(float now, bool cancelConstructionCommands = false)
        {
            if (entries.Count == 0) return false;
            Entry entry = entries.Dequeue();
            if (cancelConstructionCommands && !string.IsNullOrWhiteSpace(entry.Id) && entry.Id.StartsWith("build:", StringComparison.OrdinalIgnoreCase))
            {
                entry.State = IA01CommandState.Cancelled;
                entry.Confirm?.Invoke(false);
                return true;
            }
            entry.State = IA01CommandState.Validating;
            bool succeeded = entry.Execute != null && entry.Execute();
            entry.State = succeeded ? IA01CommandState.Succeeded : IA01CommandState.Failed;
            entry.Confirm?.Invoke(succeeded);
            return true;
        }
    }

    public sealed class IA01BuildDirector
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01WorldState world;
        private readonly IA01ConstructionGovernor governor;
        private readonly IA01BuildCatalogAdapter catalog;
        private readonly IA01BuildReservationGrid reservations;
        private readonly IA01BuildFailureMemory failures;
        private readonly IA01CityPlanner city;
        private readonly IA01CommandQueue commands;
        private readonly IA01ZonePlanner zones;
        private readonly IA01LotPlanner lots;
        private readonly IA01BackendBridge backend;
        private readonly IA01BuildPlanRuntime buildPlan;
        private readonly IA01BuildExecutor executor;
        private bool buildPending;
        private string lastAttemptKey = string.Empty;
        private string lastBlockedIntent = "Nenhuma";
        private string lastBlockReason = "Nenhum";
        private int lastFailureCount;
        private IA01FailureCode lastFailureCode = IA01FailureCode.None;
        private string lastFailureDetail = "n/d";
        private string nextUnblockCondition = "Nova tentativa permitida.";
        private string activeConstructionCommand = string.Empty;
        private IA01ConstructionState currentConstructionState = IA01ConstructionState.Idle;
        private string currentSector = "capital";
        private string pendingStructureId = string.Empty;
        private string pendingPrefabId = string.Empty;
        private string currentNeed = "n/d";
        private int needScore;
        private string currentLotId = string.Empty;
        private float confirmationReadyAt;
        private float confirmationDeadline;
        private bool cancelQueuedConstructionCommand;
        private bool confirmationTimeoutArmed;
        private float lastConstructionCompletedAt = -1f;
        private IA01BuildDefinition pendingDefinition;
        private IA01BuildLot pendingLot;
        private IA01Intent pendingIntent;
        private IA01IntentBoard pendingBoard;
        private IA01BuildPlanSelection pendingPlanSelection;
        private float lastPlanningMilliseconds;

        public string Status { get; private set; } = "Aguardando intencao de construcao.";
        public string BlockedIntentStatus => lastBlockedIntent;
        public string BlockReasonStatus => lastBlockReason;
        public string FailureCountStatus => lastFailureCount.ToString();
        public string LastFailureCodeStatus => lastFailureCode.ToString();
        public string LastFailureDetailStatus => string.IsNullOrWhiteSpace(lastFailureDetail) ? "n/d" : lastFailureDetail;
        public string NextUnblockCondition => nextUnblockCondition;
        public string ActiveConstructionCommand => activeConstructionCommand;
        public IA01ConstructionState CurrentConstructionState => currentConstructionState;
        public bool HasPendingConstruction => buildPending;
        public int PendingCommandCount => commands != null ? commands.PendingCount : 0;
        public float LastPlanningMilliseconds => lastPlanningMilliseconds;
        public float ConfirmationReadyAt => confirmationReadyAt;
        public int CandidatesEvaluated => lots != null ? lots.CandidatesEvaluated : 0;
        public int PhysicsChecks => lots != null ? lots.PhysicsChecks : 0;
        public MarcadorTerritorio CapitalMarker => city != null ? city.Capital : null;
        public string CurrentSector => currentSector;
        public string PendingStructureIdStatus => string.IsNullOrWhiteSpace(pendingStructureId) ? "n/d" : pendingStructureId;
        public string ConfirmationDeadlineStatus => confirmationDeadline > 0f ? confirmationDeadline.ToString("0.0", CultureInfo.InvariantCulture) + "s" : "n/d";
        public string CurrentNeedStatus => string.IsNullOrWhiteSpace(currentNeed) ? "n/d" : currentNeed;
        public string NeedScoreStatus => needScore > 0 ? needScore.ToString(CultureInfo.InvariantCulture) : "0";
        public string CurrentLotIdStatus => string.IsNullOrWhiteSpace(currentLotId) ? "n/d" : currentLotId;
        public string LastConstructionCompletedAtStatus => lastConstructionCompletedAt >= 0f ? lastConstructionCompletedAt.ToString("0.0", CultureInfo.InvariantCulture) + "s" : "n/d";
        public int FailureCount => lastFailureCount;
        public string BlockReason => lastBlockReason;
        public bool CancelQueuedConstructionCommand => cancelQueuedConstructionCommand;

        public IA01BuildDirector(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01ConstructionGovernor governor, IA01BuildCatalogAdapter catalog, IA01BuildReservationGrid reservations, IA01BuildFailureMemory failures, IA01CommandQueue commands, IA01CityPlanner city, IA01ZonePlanner zones, IA01LotPlanner lots, IA01BackendBridge backend, IA01BuildPlanRuntime buildPlan)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.governor = governor;
            this.catalog = catalog;
            this.reservations = reservations;
            this.failures = failures;
            this.commands = commands;
            this.city = city;
            this.zones = zones;
            this.lots = lots;
            this.backend = backend;
            this.buildPlan = buildPlan;
            executor = new IA01BuildExecutor(controller, context, backend, city, world);
        }

        public bool Plan(float now, IA01IntentBoard board)
        {
            float startedAt = Time.realtimeSinceStartup;
            try
            {
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
                string timeoutStateToken = failures.BuildStateToken(
                    catalog != null ? catalog.CatalogVersion : 0,
                    world != null ? world.Version : -1,
                    IA01OperationalRules.IsCapitalThreatened(world, city.Capital, country),
                    country != null && country.emGuerra,
                    country != null ? country.saldo : 0,
                    country != null ? country.energia : 0,
                    country != null ? country.comida : 0);

                if (buildPending)
                {
                    currentConstructionState = IA01ConstructionState.WaitingConfirmation;
                    Status = string.IsNullOrWhiteSpace(pendingStructureId)
                        ? "Aguardando confirmacao da obra em andamento."
                        : "Aguardando confirmacao de " + pendingStructureId + " em " + currentLotId + ".";

                    if (now < confirmationReadyAt)
                    {
                        return false;
                    }

                    IA01Manager manager = controller != null ? controller.Manager : null;
                    bool matched = false;
                    if (manager != null)
                    {
                        IReadOnlyList<IA01WorldEntityRecord> teamRecords = manager.WorldRegistry.GetByTeam(context.TeamId);
                        for (int i = 0; i < teamRecords.Count; i++)
                        {
                            IA01WorldEntityRecord record = teamRecords[i];
                            if (record == null || record.TeamId != context.TeamId)
                            {
                                continue;
                            }

                            if (!string.Equals(record.CommandId, activeConstructionCommand, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!string.Equals(record.StructureId, pendingStructureId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!string.Equals(record.PrefabId, pendingPrefabId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!string.Equals(record.LotId, currentLotId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            matched = true;
                            break;
                        }
                    }

                    if (matched && pendingDefinition != null && pendingLot != null && pendingIntent != null && pendingBoard != null)
                    {
                        ConfirmBuild(true, pendingDefinition, pendingLot, pendingIntent, pendingBoard, now);
                    }
                    else if (now >= confirmationDeadline)
                    {
                        string timedOutStructure = string.IsNullOrWhiteSpace(pendingStructureId) ? "obra pendente" : pendingStructureId;
                        string timedOutLot = string.IsNullOrWhiteSpace(currentLotId) ? "lote desconhecido" : currentLotId;
                        confirmationTimeoutArmed = true;
                        cancelQueuedConstructionCommand = true;
                        currentConstructionState = IA01ConstructionState.Cooldown;
                        Status = "Tempo limite na confirmacao de " + timedOutStructure + " no lote " + timedOutLot + ".";
                    }

                    return false;
                }

                if (governor != null && governor.ConstructionMode == IA01ConstructionMode.Frozen)
                {
                    currentConstructionState = IA01ConstructionState.Idle;
                    Status = "Construcao congelada: " + governor.ConstructionFreezeReason;
                    return false;
                }

                // Um intento de fundacao pode ficar no quadro apos um
                // carregamento ou apos a prefeitura ter sido registrada por
                // outro sistema. Nessa situacao ele nao pode monopolizar a
                // fila e gerar um falso "CatalogMissing".
                if (city != null && city.Capital != null)
                {
                    board.Complete(IA01IntentType.EstablishCapital);
                }

                currentConstructionState = IA01ConstructionState.SelectingIntent;
                IA01Intent intent = board.GetBestApproved(candidate => IsIntentAllowed(candidate, now));
                if (intent == null || !IsBuildIntent(intent.Type))
                {
                    currentConstructionState = IA01ConstructionState.Idle;
                    currentNeed = "n/d";
                    needScore = 0;
                    return false;
                }

                currentNeed = string.IsNullOrWhiteSpace(intent.Reason) ? intent.Type.ToString() : intent.Reason;
                needScore = intent.Priority;

                if (intent.Type == IA01IntentType.BuildDefense
                    && governor != null
                    && governor.FixedDefenseCount >= governor.MaxFixedDefenses)
                {
                    currentConstructionState = IA01ConstructionState.Idle;
                    Status = "Defesa fixa no limite da fase (" + governor.FixedDefenseCount + "/" + governor.MaxFixedDefenses + "). Usando unidades e patrulha.";
                    board.Complete(intent.Type);
                    return false;
                }

                IA01BuildDefinition definition;
                string regionKey = BuildRegionKey(country);
                currentSector = regionKey;
                int failureWorldVersion = intent.Type == IA01IntentType.EstablishCapital ? -1 : world.Version;
                string stateToken = timeoutStateToken;
                string attemptKey = failures.BuildIntentKey(intent.Type, IA01StrategicRole.None, regionKey);
                lastAttemptKey = attemptKey;
                if (!failures.CanAttempt(attemptKey, now, stateToken))
                {
                    currentConstructionState = IA01ConstructionState.Cooldown;
                    Status = "Intento em cooldown: " + intent.Type + " na regiao " + regionKey + ".";
                    UpdateBlockStatus(intent.Type, now);
                    context.SetMetric("ia01.construction.cooldown", 1d);
                    return false;
                }

                currentConstructionState = IA01ConstructionState.SelectingCatalogItem;
                IA01BuildPlanSelection planSelection = null;
                bool planHandled = false;
                string planReason = string.Empty;
                bool foundationBudgetOverride = city != null
                    && city.Capital == null
                    && intent.Type == IA01IntentType.EstablishCapital
                    && controller != null
                    && controller.FoundationFundingGranted;
                bool openingBudgetOverride = city != null && city.IsFoundationSequenceIntent(intent.Type);
                foundationBudgetOverride = foundationBudgetOverride || openingBudgetOverride;
                bool restoredPlanCommand = buildPlan != null && buildPlan.TryGetRestoredPending(intent, out planSelection, out planReason);
                bool found = restoredPlanCommand || buildPlan != null && buildPlan.TrySelect(intent, now, out planSelection, out planHandled, out planReason);
                // Um create preparado é uma ordem de posicionamento, não uma
                // sugestão. Se ele não estiver válido, a IA aguarda e informa o
                // motivo; nunca procura um lote aleatório no mapa.
                bool allowOpeningFallback = city != null
                    && city.IsFoundationSequenceIntent(intent.Type)
                    && !RequiresOwnCreate(intent.Type);
                // Fichas antigas do plano podem não ser reconhecidas pelo catálogo
                // novo. Para itens com create próprio, aceita outra ficha compatível,
                // mas mantém obrigatoriamente a âncora do create na etapa de lote.
                if (!found && (!planHandled || allowOpeningFallback || RequiresOwnCreate(intent.Type)))
                {
                    found = intent.Type == IA01IntentType.EstablishCapital
                        ? catalog.TryGetCapital(out definition)
                        : catalog.TryGetForIntent(intent.Type, country, context.CurrentStage, foundationBudgetOverride, out definition);
                    if (found && allowOpeningFallback)
                    {
                        planHandled = false;
                        planReason = string.Empty;
                    }
                }
                else if (found)
                {
                    definition = planSelection.Definition;
                }
                else
                {
                    definition = null;
                }
                if (!found)
                {
                    currentConstructionState = IA01ConstructionState.Cooldown;
                    Status = planHandled ? "Roteiro aguardando: " + planReason : catalog.LastDiagnostic;
                    context.SetMetric("ia01.construction.catalog_blocked", 1d);
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.NoValidCatalogItem, IA01IntentBlockReason.CatalogMissing);
                    if (!planHandled && city != null && city.IsFoundationSequenceIntent(intent.Type) && lastFailureCount >= 3)
                    {
                        city.MarkSequenceCatalogUnavailable(intent.Type, catalog.LastDiagnostic);
                    }
                    if (!planHandled) board.Complete(intent.Type);
                    return false;
                }

                // O financiamento de fundacao ja foi concedido pelo diretor economico
                // e e a mesma autorizacao consumida pelo executor. Nao volte a exigir
                // aqui que o saldo sincronizado cubra a prefeitura: outros sistemas do
                // jogo podem atualizar esse saldo entre o grant e o planejamento.
                if (country == null || (!foundationBudgetOverride && country.saldo < definition.Cost))
                {
                    currentConstructionState = IA01ConstructionState.Cooldown;
                    Status = "Saldo insuficiente para " + definition.DisplayName + ".";
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.InsufficientFunds, IA01IntentBlockReason.Funds, Status);
                    return false;
                }

                currentConstructionState = IA01ConstructionState.SearchingLot;
                Vector3 origin;
                if (!controller.TryResolveConstructionAnchor(intent.Type, out origin))
                {
                    string originReason;
                    if (!zones.TryResolvePlanningOrigin(city, definition, out origin, out originReason))
                    {
                        currentConstructionState = IA01ConstructionState.Cooldown;
                        Status = "WorldNotReady para " + definition.DisplayName + ": " + originReason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.NoValidLot, IA01IntentBlockReason.NoLot, originReason);
                        return false;
                    }
                }
                int maxCandidates = governor != null ? governor.MaxCandidatesPerSlice : 4;
                int maxPhysicsChecks = governor != null ? governor.MaxPhysicsChecksPerSlice : 16;
                IA01BuildLot lot;
                string reason;
                Vector3 anchorPosition = Vector3.zero; // default for residential lots
                Quaternion anchorRotation = Quaternion.identity; // default for residential lots
                // Estruturas especiais usam o create fixo. Residencias sao excecao:
                // o create apenas indica a regiao de referencia; cada casa/predio
                // precisa de um lote novo junto da rua para formar um bairro.
                bool residentialIntent = definition.StrategicRole == IA01StrategicRole.Residential;
                bool hasAnchor = false;
                if (!residentialIntent)
                {
                    hasAnchor = controller.TryResolveConstructionAnchor(intent.Type, out anchorPosition, out anchorRotation);
                }
                if (hasAnchor)
                {
                    // O create/âncora do próprio país tem prioridade absoluta,
                    // inclusive sobre um slot preparado de outro layout.
                    if (!lots.TryFindAnchoredLot(definition, anchorPosition, anchorRotation, maxPhysicsChecks, out lot, out reason))
                    {
                        string motivoFallback = string.Empty;
                        // O construtor de veículos continua pertencendo à abertura
                        // da própria IA, mas um create pode cair sobre um collider
                        // legado ou sobre uma unidade que foi posicionada depois.
                        // Nesse caso procura um lote próximo da âncora, sem liberar
                        // a construção para outra região do mapa.
                        // Um create ocupado bloqueia a obra; nunca procura um lote
                        // alternativo que possa cair em outra parte do mapa.
                        if (!RequiresOwnCreate(intent.Type) && intent.Type == IA01IntentType.BuildVehicleConstructor
                            && lots.TryFindLot(definition, anchorPosition, now, maxCandidates, maxPhysicsChecks, out lot, out motivoFallback))
                        {
                            reason = "âncora ocupada; lote local alternativo aprovado: " + lot.Key;
                            Status = "Construtor de veículos deslocado para lote local da âncora.";
                        }
                        else
                        {
                            if (intent.Type == IA01IntentType.BuildVehicleConstructor && !string.IsNullOrWhiteSpace(motivoFallback))
                                reason = reason + " | fallback local: " + motivoFallback;
                        currentConstructionState = IA01ConstructionState.Cooldown;
                        Status = "Create fixo invalido para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.NoValidLot, IA01IntentBlockReason.NoLot, reason);
                        return false;
                        }
                    }
                }
                else if (planSelection != null && planSelection.UsesPreparedSlot)
                {
                    lot = planSelection.Lot;
                    if (!lots.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason))
                    {
                        currentConstructionState = IA01ConstructionState.Cooldown;
                        Status = "Slot preparado adiado para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.NoValidLot, IA01IntentBlockReason.NoLot, "Slot preparado invalido: " + reason);
                        buildPlan.Confirm(planSelection, string.Empty, false, reason, now);
                        return false;
                    }
                }
                else
                {
                    bool foundLot;
                    if (IsFixedConstructionIntent(intent.Type) && planSelection == null)
                    {
                        lot = null;
                        reason = "create/ancora obrigatorio nao configurado";
                        foundLot = false;
                    }
                    else if (planSelection != null && planSelection.Zone != null)
                    {
                        foundLot = lots.TryFindLotInBounds(definition, planSelection.Zone.WorldBounds, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
                    }
                    else
                    {
                        foundLot = lots.TryFindLot(definition, origin, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
                    }
                    if (!foundLot)
                    {
                        currentConstructionState = IA01ConstructionState.Cooldown;
                        reason = string.IsNullOrWhiteSpace(reason) ? "nenhum lote dentro da zona autonoma" : reason;
                        Status = "Lote adiado para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.NoValidLot, IA01IntentBlockReason.NoLot, "Nenhum lote valido: " + reason);
                        if (planSelection != null) buildPlan.Confirm(planSelection, string.Empty, false, reason, now);
                        return false;
                    }
                }

                if (!reservations.TryReserve(lot))
                {
                    currentConstructionState = IA01ConstructionState.Cooldown;
                    Status = "Lote ja reservado para " + definition.DisplayName + ".";
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.LotBlocked, IA01IntentBlockReason.LotBlocked, "Reserva ocupada para " + definition.DisplayName + ".");
                    return false;
                }

                currentConstructionState = IA01ConstructionState.Reserved;
                activeConstructionCommand = restoredPlanCommand ? buildPlan.PendingCommandId : "build:" + definition.ItemId + ":" + lot.Key;
                if (planSelection != null && !restoredPlanCommand && !buildPlan.TryReserve(planSelection, activeConstructionCommand, now, out reason))
                {
                    reservations.Release(lot, false);
                    buildPending = false;
                    activeConstructionCommand = string.Empty;
                    currentConstructionState = IA01ConstructionState.Cooldown;
                    Status = "Reserva do slot falhou: " + reason;
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA01FailureCode.LotReserved, IA01IntentBlockReason.LotBlocked, "Falha ao reservar slot preparado: " + reason);
                    return false;
                }
                buildPending = true;
                pendingDefinition = definition;
                pendingLot = lot;
                pendingIntent = intent;
                pendingBoard = board;
                pendingPlanSelection = planSelection;
                pendingStructureId = definition.ItemId;
                pendingPrefabId = definition.ItemId;
                if (definition.Item != null && definition.Item.TryGetPrefabBasico(out GameObject pendingPrefab) && pendingPrefab != null)
                {
                    pendingPrefabId = pendingPrefab.name;
                }
                currentLotId = lot.Key;
                confirmationReadyAt = now + 0.05f;
                confirmationDeadline = now + 8f;
                Status = definition.UsedCatalogFallback
                    ? "Obra aprovada com fallback: " + definition.DisplayName + ". " + definition.CatalogResolution
                    : "Obra aprovada: " + definition.DisplayName + ".";
                context.SetMetric("ia01.construction.catalog_fallback", definition.UsedCatalogFallback ? 1d : 0d);
                commands.Enqueue(activeConstructionCommand,
                    () =>
                    {
                        buildPlan?.MarkExecuting(planSelection, activeConstructionCommand);
                        return ExecuteBuild(definition, lot, country, foundationBudgetOverride);
                    },
                    success =>
                    {
                        // A confirmacao positiva precisa observar o registro do mundo em outra fatia.
                        // Isso evita que uma obra seja considerada pronta no mesmo comando que a criou.
                        if (!success)
                        {
                            ConfirmBuild(false, definition, lot, intent, board, now);
                        }
                    });
                currentConstructionState = IA01ConstructionState.WaitingConfirmation;
                return true;
            }
            finally
            {
                lastPlanningMilliseconds = (Time.realtimeSinceStartup - startedAt) * 1000f;
            }
        }

        private static bool IsFixedConstructionIntent(IA01IntentType type)
        {
            return type == IA01IntentType.BuildEnergy
                || type == IA01IntentType.BuildFoodProduction
                || type == IA01IntentType.BuildStorage
                || type == IA01IntentType.BuildMilitaryTent
                || type == IA01IntentType.BuildVehicleConstructor
                || type == IA01IntentType.BuildMilitaryAirport
                || type == IA01IntentType.BuildCommercialAirport
                || type == IA01IntentType.BuildShipyard
                || type == IA01IntentType.BuildPier
                || type == IA01IntentType.BuildOffshorePlatform;
        }

        private static bool RequiresOwnCreate(IA01IntentType type)
        {
            return type == IA01IntentType.BuildEnergy
                || type == IA01IntentType.BuildFoodProduction
                || type == IA01IntentType.BuildStorage
                || type == IA01IntentType.BuildVehicleConstructor
                || type == IA01IntentType.BuildMilitaryAirport
                || type == IA01IntentType.BuildCommercialAirport
                || type == IA01IntentType.BuildShipyard
                || type == IA01IntentType.BuildPier
                || type == IA01IntentType.BuildOffshorePlatform
                || type == IA01IntentType.BuildMilitaryTent;
        }

        public bool AllowsIntent(IA01Intent intent, float now)
        {
            return IsIntentAllowed(intent, now);
        }

        public string GetCooldownStatus(float now)
        {
            if (!failures.TryGetCooldown(lastAttemptKey, now, out _, out _, out float remaining, out bool requiresStateChange))
            {
                return "0 s";
            }

            return requiresStateChange ? "aguardando mudanca de estado" : remaining.ToString("0.0") + " s";
        }

        private void RecordFailure(IA01IntentType intent, string key, float now, string stateToken, IA01FailureCode failureCode, IA01IntentBlockReason blockReason, string detail = null)
        {
            failures.Record(key, now, stateToken, failureCode, blockReason);
            lastAttemptKey = key;
            currentConstructionState = IA01ConstructionState.Cooldown;
            lastFailureCode = failureCode;
            lastFailureDetail = string.IsNullOrWhiteSpace(detail) ? blockReason.ToString() : detail;
            UpdateBlockStatus(intent, now);
        }

        private void UpdateBlockStatus(IA01IntentType intent, float now)
        {
            lastBlockedIntent = intent.ToString();
            if (!failures.TryGetCooldown(lastAttemptKey, now, out int failureCount, out IA01IntentBlockReason reason, out _, out bool requiresStateChange))
            {
                lastBlockReason = "Nenhum";
                lastFailureCount = 0;
                lastFailureCode = IA01FailureCode.None;
                lastFailureDetail = "n/d";
                nextUnblockCondition = "Nova tentativa permitida.";
                return;
            }

            lastFailureCount = failureCount;
            lastBlockReason = reason.ToString();
            nextUnblockCondition = requiresStateChange
                ? reason == IA01IntentBlockReason.Funds
                    ? "Funding, tesouraria ou custos precisam mudar."
                    : "Catalogo, mundo, ameaca ou recursos devem mudar."
                : "Aguardar o cooldown terminar.";
        }

        private bool IsIntentAllowed(IA01Intent intent, float now)
        {
            if (intent == null || !intent.Approved || !IsBuildIntent(intent.Type))
            {
                return false;
            }

            if (intent.Type == IA01IntentType.EstablishCapital && city != null && city.Capital != null)
            {
                return false;
            }

            if (governor != null && governor.ConstructionMode == IA01ConstructionMode.Frozen)
            {
                return false;
            }

            DadosPaisGoverno country = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(context.TeamId) : null;
            string regionKey = BuildRegionKey(country);
            int failureWorldVersion = intent.Type == IA01IntentType.EstablishCapital ? -1 : world.Version;
            string token = failures.BuildStateToken(catalog.CatalogVersion, failureWorldVersion, IA01OperationalRules.IsCapitalThreatened(world, city.Capital, country), country != null && country.emGuerra, country != null ? country.saldo : 0, country != null ? country.energia : 0, country != null ? country.comida : 0);
            string key = failures.BuildIntentKey(intent.Type, IA01StrategicRole.None, regionKey);
            return failures.CanAttempt(key, now, token);
        }

        private static string BuildRegionKey(DadosPaisGoverno country)
        {
            return country != null ? "capital:" + country.teamId : "capital:unknown";
        }

        private static bool IsBuildIntent(IA01IntentType type)
        {
            return type == IA01IntentType.EstablishCapital
                || type == IA01IntentType.BuildEnergy
                || type == IA01IntentType.BuildFoodProduction
                || type == IA01IntentType.BuildResidentialCapacity
                || type == IA01IntentType.BuildStarterHouse
                || type == IA01IntentType.BuildMediumApartment
                || type == IA01IntentType.BuildHighApartment
                || type == IA01IntentType.BuildMilitaryTent
                || type == IA01IntentType.BuildVehicleConstructor
                || type == IA01IntentType.BuildStorage
                || type == IA01IntentType.BuildLogistics
                || type == IA01IntentType.BuildRoad
                || type == IA01IntentType.BuildMilitaryAirport
                || type == IA01IntentType.BuildCommercialAirport
                || type == IA01IntentType.BuildShipyard
                || type == IA01IntentType.BuildIndustry
                || type == IA01IntentType.BuildDefense;
        }

        private bool ExecuteBuild(IA01BuildDefinition definition, IA01BuildLot lot, DadosPaisGoverno country, bool foundationBudgetOverride)
        {
            currentConstructionState = IA01ConstructionState.Executing;
            return executor != null && executor.TryExecute(definition, lot, activeConstructionCommand, pendingPrefabId, foundationBudgetOverride, out _);
        }

        private void ConfirmBuild(bool success, IA01BuildDefinition definition, IA01BuildLot lot, IA01Intent intent, IA01IntentBoard board, float now)
        {
            bool timedOut = confirmationTimeoutArmed;
            string completedCommandId = activeConstructionCommand;
            IA01BuildPlanSelection completedPlanSelection = pendingPlanSelection;
            buildPending = false;
            activeConstructionCommand = string.Empty;
            pendingDefinition = null;
            pendingLot = null;
            pendingIntent = null;
            pendingBoard = null;
            pendingPlanSelection = null;
            pendingStructureId = string.Empty;
            pendingPrefabId = string.Empty;
            currentNeed = "n/d";
            needScore = 0;
            currentLotId = string.Empty;
            confirmationReadyAt = 0f;
            confirmationDeadline = 0f;
            cancelQueuedConstructionCommand = false;
            confirmationTimeoutArmed = false;
            currentConstructionState = IA01ConstructionState.Cooldown;
            if (!success)
            {
                reservations.Release(lot, true);
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
                string failureStateToken = failures.BuildStateToken(
                    catalog != null ? catalog.CatalogVersion : 0,
                    world != null ? world.Version : -1,
                    IA01OperationalRules.IsCapitalThreatened(world, city.Capital, country),
                    country != null && country.emGuerra,
                    country != null ? country.saldo : 0,
                    country != null ? country.energia : 0,
                    country != null ? country.comida : 0);
                RecordFailure(
                    intent.Type,
                    lastAttemptKey,
                    now,
                    failureStateToken,
                    timedOut ? IA01FailureCode.Busy : IA01FailureCode.ExecutionFailed,
                    timedOut ? IA01IntentBlockReason.Cooldown : IA01IntentBlockReason.Busy);
                buildPlan?.Confirm(completedPlanSelection, completedCommandId, false, timedOut ? "tempo limite de confirmacao" : "execucao nao confirmada", now);
                if (timedOut)
                {
                    Status = "Tempo limite confirmado para " + definition.DisplayName + ".";
                }
                else
                {
                    Status = governor != null && governor.ConstructionMode == IA01ConstructionMode.Frozen
                        ? "Obra cancelada pelo governador: " + definition.DisplayName + "."
                        : "Falha confirmada: " + definition.DisplayName + ".";
                }
                return;
            }

            reservations.MarkOccupied(lot);
            buildPlan?.Confirm(completedPlanSelection, completedCommandId, true, string.Empty, now);
            board.Complete(intent.Type);
            context.SetMetric("ia01.construction.last_cost", definition.Cost);
            context.TryGetMetric("ia01.construction.completed", out double completed);
            context.SetMetric("ia01.construction.completed", completed + 1d);
            context.MarkDirty(IA01DirtyReason.ExternalEvent);
            lastConstructionCompletedAt = now;
            Status = definition.UsedCatalogFallback
                ? "Construido com fallback e confirmado: " + definition.DisplayName + "."
                : "Construido e confirmado: " + definition.DisplayName + ".";
        }
    }

    public sealed class IA01BuildValidator
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01WorldState world;
        private readonly IA01BuildReservationGrid reservations;
        private readonly IA01BuildFailureMemory failures;
        private int cursor;
        private int candidatesEvaluated;
        private int physicsChecks;

        public int CandidatesEvaluated => candidatesEvaluated;
        public int PhysicsChecks => physicsChecks;

        public IA01BuildValidator(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01BuildReservationGrid reservations, IA01BuildFailureMemory failures)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.reservations = reservations;
            this.failures = failures;
        }

        public bool TryFindLot(IA01BuildDefinition definition, Vector3 origin, float now, int maxCandidates, int maxPhysicsChecks, out IA01BuildLot lot, out string reason)
        {
            int candidateBudget = Mathf.Max(1, maxCandidates);
            int physicsBudget = Mathf.Max(1, maxPhysicsChecks);
            int physicsSpent = 0;
            bool antiAir = IsAntiAirDefinition(definition);
            for (int i = 0; i < candidateBudget; i++)
            {
                candidatesEvaluated++;
                int slot = cursor++;
                float angle = (slot * 47f) * Mathf.Deg2Rad;
                // A defesa antiaerea precisa de um anel proprio ao redor do nucleo:
                // nunca e colocada colada a outra estrutura e fica distribuida no
                // perimetro, em vez de ocupar o primeiro lote livre da cidade.
                float radius = antiAir ? 120f + (slot % 8) * 34f : 42f + (slot % 7) * 24f;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!TryResolveDomain(definition, candidate, ref physicsSpent, physicsBudget, out Vector3 position, out Quaternion rotation, out reason))
                {
                    if (reason == "orcamento de fisica excedido")
                    {
                        break;
                    }
                    continue;
                }

                // Residencias ocupam lotes urbanos reais: a frente precisa apontar para
                // uma rua e alternamos os dois lados para formar quarteiroes, em vez de
                // empilhar tudo no mesmo ponto de origem da IA.
                TryArrangeResidentialLot(definition, ref position, ref rotation, slot);

                GerenteDeTerritorio territory = GerenteDeTerritorio.Instancia;
                if (territory != null)
                {
                    int owner = territory.ObterDonoDoPonto(position);
                    if (owner > 0 && owner != context.TeamId)
                    {
                        reason = "lote pertence a outro time";
                        continue;
                    }
                }

                string key = Mathf.RoundToInt(position.x / 4f) + ":" + Mathf.RoundToInt(position.z / 4f);
                if (failures.IsCoolingDown(key, now))
                {
                    continue;
                }

                lot = new IA01BuildLot { Position = position, Rotation = rotation, Footprint = definition.Footprint, Key = key, State = IA01LotState.Free };
                if (!HasClearFootprint(lot, definition, ref physicsSpent, physicsBudget, out reason))
                {
                    if (reason == "orcamento de fisica excedido")
                    {
                        break;
                    }

                    failures.Record(key, now);
                    continue;
                }

                reason = string.Empty;
                return true;
            }

            lot = null;
            reason = physicsSpent >= physicsBudget ? "orcamento de fisica excedido" : "nenhum candidato valido dentro do budget";
            return false;
        }

        public bool TryValidatePreparedLot(IA01BuildDefinition definition, IA01BuildLot lot, int maxPhysicsChecks, out string reason)
        {
            if (definition == null || lot == null)
            {
                reason = "lote preparado ou definicao ausente";
                return false;
            }

            int physicsSpent = 0;
            if (definition.Domain == IA01BuildDomain.Water)
            {
                if (!NavalPlacementResolver.IsWaterAtPosition(lot.Position))
                {
                    reason = "slot naval nao esta na agua navegavel";
                    return false;
                }
            }
            else if (definition.Domain == IA01BuildDomain.Coastal)
            {
                GameObject prefab;
                if (!definition.Item.TryGetPrefabBasico(out prefab)
                    || !NavalPlacementResolver.TryResolveStructurePose(prefab, lot.Position, lot.Rotation, out NavalPlacementResolver.StructurePose pose)
                    || (pose.Position - lot.Position).sqrMagnitude > 16f)
                {
                    if (!IsOpeningPreparedDefinition(definition))
                    {
                        reason = "slot costeiro nao passa na validacao naval";
                        return false;
                    }
                }
            }
            else
            {
                if (!SpendPhysicsCheck(ref physicsSpent, maxPhysicsChecks, out reason)) return false;
                RaycastHit hit;
                bool hitGround = Physics.Raycast(lot.Position + Vector3.up * 1000f, Vector3.down, out hit, 2500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if ((hitGround && NavalPlacementResolver.IsWaterAtPosition(hit.point))
                    || (!hitGround && NavalPlacementResolver.IsWaterAtPosition(lot.Position)))
                {
                    reason = "slot terrestre esta na agua";
                    return false;
                }

                // Os locais configurados manualmente no layout podem estar sobre uma malha
                // visual sem Collider. Para esses slots preparados, a ausencia do Raycast nao
                // deve congelar toda a abertura: o marcador ja representa o solo escolhido no editor.
            }

            if (!HasClearFootprint(lot, definition, ref physicsSpent, maxPhysicsChecks, out reason)) return false;
            reason = string.Empty;
            return true;
        }

        private static bool IsOpeningPreparedDefinition(IA01BuildDefinition definition)
        {
            return definition != null
                && (definition.StrategicRole == IA01StrategicRole.Shipyard
                    || definition.StrategicRole == IA01StrategicRole.Port
                    || definition.StrategicRole == IA01StrategicRole.Pier
                    || (definition.MinimumStage == IA01NationStage.Initialization
                        && definition.MaximumRecommendedCount == 1
                        && !string.IsNullOrWhiteSpace(definition.CatalogResolution)
                        && definition.CatalogResolution.ToLowerInvariant().Contains("abertura")));
        }

        public bool TryFindLotInBounds(IA01BuildDefinition definition, Bounds bounds, float now, int maxCandidates, int maxPhysicsChecks, out IA01BuildLot lot, out string reason)
        {
            int candidateBudget = Mathf.Max(1, maxCandidates);
            int physicsBudget = Mathf.Max(1, maxPhysicsChecks);
            int physicsSpent = 0;
            Vector3 origin = bounds.center;
            for (int i = 0; i < candidateBudget; i++)
            {
                candidatesEvaluated++;
                int slot = cursor++;
                float angle = (slot * 47f) * Mathf.Deg2Rad;
                float radius = 8f + (slot % 7) * 14f;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!bounds.Contains(candidate)) continue;
                if (!TryResolveDomain(definition, candidate, ref physicsSpent, physicsBudget, out Vector3 position, out Quaternion rotation, out reason))
                {
                    if (reason == "orcamento de fisica excedido") break;
                    continue;
                }
                TryArrangeResidentialLot(definition, ref position, ref rotation, slot);
                if (!bounds.Contains(position))
                {
                    reason = "candidato saiu da zona autonoma";
                    continue;
                }
                GerenteDeTerritorio territory = GerenteDeTerritorio.Instancia;
                if (territory != null)
                {
                    int owner = territory.ObterDonoDoPonto(position);
                    if (owner > 0 && owner != context.TeamId)
                    {
                        reason = "lote pertence a outro time";
                        continue;
                    }
                }
                string key = Mathf.RoundToInt(position.x / 4f) + ":" + Mathf.RoundToInt(position.z / 4f);
                if (failures.IsCoolingDown(key, now)) continue;
                lot = new IA01BuildLot { Position = position, Rotation = rotation, Footprint = definition.Footprint, Key = "zone:" + key, State = IA01LotState.Free };
                if (!HasClearFootprint(lot, definition, ref physicsSpent, physicsBudget, out reason))
                {
                    if (reason == "orcamento de fisica excedido") break;
                    failures.Record(key, now);
                    continue;
                }
                reason = string.Empty;
                return true;
            }
            lot = null;
            reason = physicsSpent >= physicsBudget ? "orcamento de fisica excedido" : "nenhum candidato valido dentro da zona autonoma";
            return false;
        }

        private bool TryResolveDomain(IA01BuildDefinition definition, Vector3 candidate, ref int physicsSpent, int maxPhysicsChecks, out Vector3 position, out Quaternion rotation, out string reason)
        {
            position = candidate;
            rotation = Quaternion.identity;
            reason = string.Empty;
            if (definition.Domain == IA01BuildDomain.Water || definition.Domain == IA01BuildDomain.Coastal)
            {
                Vector3 waterPoint;
                float seaLevel;
                if (!NavalPlacementResolver.TryResolveWaterSpawn(candidate, Vector3.forward, 25f, 220f, out waterPoint, out seaLevel, out reason))
                {
                    return false;
                }

                if (definition.Domain == IA01BuildDomain.Coastal)
                {
                    GameObject prefab;
                    if (!definition.Item.TryGetPrefabBasico(out prefab) || !NavalPlacementResolver.TryResolveStructurePose(prefab, waterPoint, Quaternion.identity, out NavalPlacementResolver.StructurePose pose))
                    {
                        reason = "costa ou saida naval invalida";
                        return false;
                    }

                    position = pose.Position;
                    rotation = pose.Rotation;
                    return true;
                }

                position = waterPoint;
                position.y = seaLevel;
                if (!NavalPlacementResolver.IsWaterAtPosition(position, seaLevel))
                {
                    reason = "ponto naval nao esta na agua navegavel";
                    return false;
                }
                return true;
            }

            if (!SpendPhysicsCheck(ref physicsSpent, maxPhysicsChecks, out reason))
            {
                return false;
            }

            RaycastHit hit;
            if (!Physics.Raycast(candidate + Vector3.up * 1000f, Vector3.down, out hit, 2500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                reason = "solo sem colisao";
                return false;
            }

            position = hit.point;
            ClassificacaoSuperficieMapa classification;
            float height;
            if (RegistroSuperficieMapa.TryClassify(position, out classification, out height, 1.5f, 2f) && classification == ClassificacaoSuperficieMapa.Agua)
            {
                reason = "layer/superficie indica agua";
                return false;
            }

            if (NavalPlacementResolver.IsWaterAtPosition(position))
            {
                reason = "ponto terrestre detectado como agua";
                return false;
            }

            return true;
        }

        private bool HasClearFootprint(IA01BuildLot lot, IA01BuildDefinition definition, ref int physicsSpent, int maxPhysicsChecks, out string reason)
        {
            reason = string.Empty;
            if (IsAntiAirDefinition(definition) && !HasAntiAirStrategicClearance(lot.Position))
            {
                reason = "defesa antiaerea precisa ficar a mais de 100m de outra estrutura";
                return false;
            }
            if (!SpendPhysicsCheck(ref physicsSpent, maxPhysicsChecks, out reason))
            {
                return false;
            }

            Collider[] hits = Physics.OverlapBox(lot.Position + Vector3.up * 3f, new Vector3(lot.Footprint.x * 0.5f, 6f, lot.Footprint.y * 0.5f), lot.Rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int waterLayer = LayerMask.NameToLayer("Water");
            int groundLayer = LayerMask.NameToLayer("Chao");
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit is TerrainCollider || hit.gameObject.layer == waterLayer || hit.gameObject.layer == groundLayer)
                {
                    continue;
                }

                if (hit.GetComponentInParent<IdentidadeUnidade>() != null)
                {
                    return false;
                }
            }

            if (definition.RequiresRoad)
            {
                if (!SpendPhysicsCheck(ref physicsSpent, maxPhysicsChecks, out reason))
                {
                    return false;
                }

                Collider[] roadHits = Physics.OverlapSphere(lot.Position, Mathf.Max(lot.Footprint.x, lot.Footprint.y) + 14f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool hasRoad = false;
                for (int i = 0; i < roadHits.Length; i++)
                {
                    if (roadHits[i] != null && roadHits[i].GetComponentInParent<RuaConectora>() != null)
                    {
                        hasRoad = true;
                        break;
                    }
                }

                // A new settlement may not have roads yet. Requiring a connector before
                // the first infrastructure is built would permanently deadlock the city.
                if (!hasRoad && UnityEngine.Object.FindFirstObjectByType<RuaConectora>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void TryArrangeResidentialLot(IA01BuildDefinition definition, ref Vector3 position, ref Quaternion rotation, int slot)
        {
            if (definition == null || definition.StrategicRole != IA01StrategicRole.Residential) return;
            GameObject prefab;
            if (definition.Item == null || !definition.Item.TryGetPrefabBasico(out prefab) || prefab == null) return;
            Imovel imovel = prefab.GetComponent<Imovel>();
            if (imovel == null) return;

            Collider[] hits = Physics.OverlapSphere(position, 180f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RuaConectora nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RuaConectora road = hits[i] != null ? hits[i].GetComponentInParent<RuaConectora>() : null;
                if (road == null) continue;
                Vector3 a = road.ObterConectorInicio().posicao;
                Vector3 b = road.ObterConectorFim().posicao;
                Vector3 ab = b - a;
                ab.y = 0f;
                if (ab.sqrMagnitude < 0.01f) continue;
                Vector3 p = position; p.y = a.y;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector3 projected = a + ab * t;
                float distance = (p - projected).sqrMagnitude;
                if (distance < best) { best = distance; nearest = road; }
            }
            if (nearest == null) return;

            Vector3 start = nearest.ObterConectorInicio().posicao;
            Vector3 end = nearest.ObterConectorFim().posicao;
            Vector3 direction = end - start; direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 flat = position; flat.y = start.y;
            float along = Mathf.Clamp(Vector3.Dot(flat - start, direction), 2f, Vector3.Distance(start, end) - 2f);
            Vector3 onRoad = start + direction * along;
            Vector3 side = ((slot & 1) == 0) ? right : -right;
            position = onRoad + side * (nearest.largura * 0.5f + imovel.distanciaFronteiraRua);
            position.y = onRoad.y;
            rotation = Quaternion.LookRotation(-side, Vector3.up);
        }

        private bool HasAntiAirStrategicClearance(Vector3 position)
        {
            const float minimumDistance = 100f;
            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null) return true;
            IReadOnlyList<IA01WorldEntityRecord> structures = manager.WorldRegistry.GetByKind(IA01WorldEntityKind.Structure);
            if (structures == null) return true;
            Vector2 flatPosition = new Vector2(position.x, position.z);
            float minSqr = minimumDistance * minimumDistance;
            for (int i = 0; i < structures.Count; i++)
            {
                IA01WorldEntityRecord structure = structures[i];
                if (structure == null) continue;
                Vector2 other = new Vector2(structure.Position.x, structure.Position.z);
        if ((other - flatPosition).sqrMagnitude <= minSqr)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAntiAirDefinition(IA01BuildDefinition definition)
        {
            if (definition == null) return false;
            if (definition.StrategicRole == IA01StrategicRole.AntiAirDefense) return true;
            return definition.Item != null
                && IsAntiAirItem(definition.Item, definition.Item.GetResolvedCapabilities());
        }

        private static bool IsAntiAirItem(DadosConstrucao item, IA_ConstructionCapability capabilities)
        {
            if (item == null) return false;
            if (item.strategicRole == IA01StrategicRole.AntiAirDefense) return true;
            string text = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.nomeItem ?? string.Empty) + " "
                + (item.aliases ?? string.Empty));
            return (capabilities & IA_ConstructionCapability.Defense) != 0
                && (text.Contains("antiaerea") || text.Contains("anti aerea") || text.Contains("anti-air")
                    || text.Contains("antiair") || text.Contains("air defense") || text.Contains("defesa aerea"));
        }

        private bool SpendPhysicsCheck(ref int physicsSpent, int maxPhysicsChecks, out string reason)
        {
            reason = string.Empty;
            if (physicsSpent >= Mathf.Max(1, maxPhysicsChecks))
            {
                reason = "orcamento de fisica excedido";
                return false;
            }

            physicsSpent++;
            physicsChecks++;
            return true;
        }
    }

    public sealed class IA01WarDirector
    {
        private sealed class HostileContact
        {
            public int TeamId;
            public Vector3 Position;
            public float LastSeenAt;
            public float Damage;
        }

        private readonly Dictionary<int, HostileContact> hostileContacts = new Dictionary<int, HostileContact>();
        private const float HostileContactLifetime = 90f;
        private const float NearbyAggressorRadius = 180f;
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01WorldState world;
        private readonly IA01MissionDirector missions;
        private readonly IA01CityPlanner city;
        private readonly NavMeshPath route = new NavMeshPath();
        private float nextCheckAt;
        private float nextOrderAt;
        private float nextPatrolAt;

        public IA01Campaign Campaign { get; } = new IA01Campaign();
        public string Status { get; private set; } = "Sem alerta de guerra.";
        public int EscalationLevel
        {
            get
            {
                float total = 0f;
                foreach (HostileContact contact in hostileContacts.Values)
                    if (contact != null) total += contact.Damage;
                return Mathf.Clamp(Mathf.FloorToInt(total / 500f), 0, 6);
            }
        }

        public IA01WarDirector(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01CityPlanner city, IA01MissionDirector missions)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.city = city;
            this.missions = missions;
        }

        public void RegisterHostileAggression(int attackerTeamId, Vector3 attackerPosition, float damage)
        {
            if (attackerTeamId <= 0 || attackerTeamId == context.TeamId) return;

            float now = Time.unscaledTime;
            HostileContact contact;
            if (!hostileContacts.TryGetValue(attackerTeamId, out contact) || contact == null)
            {
                contact = new HostileContact { TeamId = attackerTeamId };
                hostileContacts[attackerTeamId] = contact;
                Debug.Log("[IA01 Combat] Time " + context.TeamId + " identificou o agressor " + attackerTeamId + " como inimigo.");
            }

            contact.Position = attackerPosition;
            contact.LastSeenAt = now;
            contact.Damage += Mathf.Max(0f, damage);
            Status = "Agressor identificado: time " + attackerTeamId + "; preparando retaliacao.";
        }

        public bool Plan(float now, IA01IntentBoard board, bool emergencyReserve)
        {
            if (now < nextCheckAt) return false;
            nextCheckAt = now + 1f;
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            bool underAttack = IA01OperationalRules.IsCapitalThreatened(world, city.Capital, country);
            if (underAttack)
            {
                board.Publish(IA01IntentType.DefendCapital, 1200, "Prefeitura sob ameaca", now);
                Campaign.DefendingCapital = true;
                return QueueDefense(now);
            }

            if (QueueRetaliation(now, board, emergencyReserve))
            {
                return true;
            }

            Campaign.DefendingCapital = false;
            if (country == null || !country.emGuerra)
            {
                return QueuePeacePatrol(now);
            }

            if (emergencyReserve)
            {
                board.Publish(IA01IntentType.DefendCapital, 1100, "Reserva de guerra critica", now);
                Campaign.DefendingCapital = true;
                Status = "Guerra com reserva critica: priorizando a defesa da prefeitura.";
                return QueueDefense(now);
            }

            MarcadorTerritorio target = world.FindEnemyCapital(country.rivalTeamId);
            if (target == null || !IsHostile(target, country))
            {
                Status = "Em guerra sem prefeitura inimiga hostil confirmada.";
                return false;
            }

            board.Publish(IA01IntentType.CampaignAgainstCapital, 950, "Neutralizar prefeitura inimiga", now);
            if (Campaign.FinalTarget != target.transform || now - Campaign.LastReplanAt > 8f)
            {
                Campaign.FinalTarget = target.transform;
                Campaign.TargetTeamId = target.GetComponent<IdentidadeUnidade>().teamID;
                Campaign.CurrentObjective = target.transform;
                Campaign.PreferredRoutePoint = ResolveApproach(target.transform.position);
                Campaign.LastReplanAt = now;
                Campaign.RouteVersion++;
                Campaign.ReplanReason = "novo alvo ou rota expirada";
            }

            if (now < nextOrderAt) return false;
            nextOrderAt = now + 3f;
            IA_BrainMaster brain = FindBrainMaster();
            if (brain == null) { Status = "Campanha aguardando BrainMaster do proprio time."; return false; }
            Vector3 objective = Campaign.PreferredRoutePoint;
            bool finalAttack = (objective - target.transform.position).sqrMagnitude < 1600f;
            missions.Queue("war:" + Campaign.TargetTeamId + ":" + Campaign.RouteVersion,
                () => finalAttack
                    ? brain.TryIssueAttack(context.TeamId, "ia01_campaign_capital", target.transform.position, 1000)
                    : brain.TryIssueMovePackage(context.TeamId, "ia01_campaign_corridor", objective, 950),
                success => Status = success ? (finalAttack ? "Campanha atacando prefeitura inimiga." : "Campanha abrindo corredor ate a prefeitura.") : "Ordem de campanha recusada; aguardando replano.");
            return true;
        }

        private bool QueueRetaliation(float now, IA01IntentBoard board, bool emergencyReserve)
        {
            HostileContact contact = ResolveLatestContact(now);
            if (contact == null) return false;

            Vector3 target = ResolveNearbyAggressor(contact);
            board.Publish(IA01IntentType.DefendCapital, emergencyReserve ? 1200 : 1150,
                "Retaliar contra o agressor identificado", now);
            if (now < nextOrderAt) return false;

            IA_BrainMaster brain = FindBrainMaster();
            if (brain == null)
            {
                Status = "Agressor identificado; aguardando BrainMaster para retaliacao.";
                return false;
            }

            nextOrderAt = now + 4f;
            int attackerTeamId = contact.TeamId;
            missions.Queue("retaliate:" + context.TeamId + ":" + attackerTeamId,
                () => brain.TryIssueAttack(context.TeamId, "ia01_retaliacao_" + attackerTeamId, target, 1150),
                success => Status = success
                    ? "Retaliacao enviada contra o time " + attackerTeamId + "."
                    : "Agressor identificado; retaliacao aguardando unidades disponiveis.");
            return true;
        }

        private HostileContact ResolveLatestContact(float now)
        {
            HostileContact latest = null;
            List<int> expired = null;
            foreach (KeyValuePair<int, HostileContact> pair in hostileContacts)
            {
                HostileContact contact = pair.Value;
                if (contact == null || now - contact.LastSeenAt > HostileContactLifetime)
                {
                    if (expired == null) expired = new List<int>();
                    expired.Add(pair.Key);
                    continue;
                }

                if (latest == null || contact.LastSeenAt > latest.LastSeenAt) latest = contact;
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++) hostileContacts.Remove(expired[i]);
            }
            return latest;
        }

        private Vector3 ResolveNearbyAggressor(HostileContact contact)
        {
            if (contact == null) return controller.transform.position;

            Vector3 target = contact.Position;
            float bestDistance = NearbyAggressorRadius * NearbyAggressorRadius;
            for (int i = 0; i < world.EnemyUnits.Count; i++)
            {
                IdentidadeUnidade enemy = world.EnemyUnits[i];
                if (enemy == null || enemy.teamID != contact.TeamId) continue;

                float distance = (enemy.transform.position - contact.Position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    target = enemy.transform.position;
                }
            }
            return target;
        }

        private bool QueueDefense(float now)
        {
            if (now < nextOrderAt || city.Capital == null) return false;
            nextOrderAt = now + 3f;
            IA_BrainMaster brain = FindBrainMaster();
            if (brain == null) return false;
            missions.Queue("defend:" + context.TeamId,
                () => brain.TryIssueMovePackage(context.TeamId, "ia01_defend_capital", city.Capital.transform.position, 1000),
                success => Status = success ? "Defesas reunidas na prefeitura." : "Defesa aguardando unidades validas.");
            return true;
        }

        private bool QueuePeacePatrol(float now)
        {
            if (city.Capital == null)
            {
                Status = "Reconhecimento aguardando prefeitura propria.";
                return false;
            }

            if (now < nextPatrolAt)
            {
                Status = "Patrulha de paz em andamento ao redor da capital.";
                return false;
            }

            if (now < nextOrderAt)
            {
                return false;
            }

            IA_BrainMaster brain = FindBrainMaster();
            if (brain == null)
            {
                Status = "Reconhecimento aguardando BrainMaster do proprio time.";
                return false;
            }

            Vector3 destination = ResolvePatrolPoint(now);
            nextOrderAt = now + 3f;
            nextPatrolAt = now + 12f;
            missions.Queue("recon:" + context.TeamId + ":" + Mathf.FloorToInt(now / 12f),
                () => brain.TryIssueMovePackage(context.TeamId, "ia01_peace_recon", destination, 650),
                success => Status = success
                    ? "Patrulha de paz enviada para reconhecimento territorial."
                    : "Patrulha aguardando unidades validas para reconhecimento.");
            return true;
        }

        private Vector3 ResolvePatrolPoint(float now)
        {
            Vector3 origin = city.Capital != null ? city.Capital.transform.position : controller.transform.position;
            int sector = Mathf.FloorToInt(now / 12f) % 8;
            float angle = sector * 45f * Mathf.Deg2Rad;
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle) * 140f, 0f, Mathf.Sin(angle) * 140f);
            return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 80f, NavMesh.AllAreas) ? hit.position : candidate;
        }

        private bool IsHostile(MarcadorTerritorio target, DadosPaisGoverno country)
        {
            IdentidadeUnidade identity = target.GetComponent<IdentidadeUnidade>();
            return identity != null && identity.teamID != context.TeamId && identity.teamID != country.aliadoPrioritarioTeamId;
        }

        private Vector3 ResolveApproach(Vector3 target)
        {
            // Se o mapa tiver creates de avanço, a guerra usa esses pontos
            // editáveis antes do fallback de NavMesh. Isso mantém a IA dentro
            // do corredor planejado pelo criador e evita atacar de coordenada
            // aleatória ou atravessar estruturas costeiras.
            IA01WarAdvanceZone[] warZones = UnityEngine.Object.FindObjectsByType<IA01WarAdvanceZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Vector3 configured = target;
            float configuredDistance = float.MaxValue;
            for (int z = 0; z < warZones.Length; z++)
            {
                IA01WarAdvanceZone zone = warZones[z];
                if (zone == null || zone.TeamId != context.TeamId || zone.Tipo == IA01WarAdvanceZone.Dominio.Aereo) continue;
                for (int p = 0; p < 8; p++)
                {
                    Vector3 candidate = zone.ObterPonto(p);
                    float distance = (candidate - target).sqrMagnitude;
                    if (distance < configuredDistance)
                    {
                        configuredDistance = distance;
                        configured = candidate;
                    }
                }
            }
            if (configuredDistance < float.MaxValue) return configured;

            Vector3 origin;
            if (city.Capital != null)
            {
                origin = city.Capital.transform.position;
            }
            else if (controller != null && controller.CapitalSlot != null)
            {
                Transform capitalPoint = controller.CapitalSlot.BuildingPoint != null
                    ? controller.CapitalSlot.BuildingPoint
                    : controller.CapitalSlot.transform;
                origin = capitalPoint != null ? capitalPoint.position : target;
            }
            else
            {
                // Sem capital construída e sem create oficial, não inventa
                // uma origem perto da câmera; usa o alvo já confirmado.
                return target;
            }
            if (!NavMesh.SamplePosition(origin, out NavMeshHit originHit, 80f, NavMesh.AllAreas)) return target;
            Vector3 best = target;
            float bestLength = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 probe = target + new Vector3(Mathf.Cos(angle) * 48f, 0f, Mathf.Sin(angle) * 48f);
                if (!NavMesh.SamplePosition(probe, out NavMeshHit destination, 35f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(originHit.position, destination.position, NavMesh.AllAreas, route) || route.corners.Length < 2) continue;
                float length = 0f;
                for (int c = 1; c < route.corners.Length; c++) length += Vector3.Distance(route.corners[c - 1], route.corners[c]);
                if (length < bestLength) { bestLength = length; best = destination.position; }
            }
            return best;
        }

        private IA_BrainMaster FindBrainMaster()
        {
            IA_BrainMaster[] brains = UnityEngine.Object.FindObjectsByType<IA_BrainMaster>(FindObjectsSortMode.None);
            for (int i = 0; i < brains.Length; i++) if (brains[i] != null && brains[i].isActiveAndEnabled && brains[i].TeamId == context.TeamId) return brains[i];
            return null;
        }
    }
}
