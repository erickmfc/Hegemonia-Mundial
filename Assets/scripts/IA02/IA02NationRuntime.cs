using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hegemonia.AI.BrainMaster;
using Hegemonia.RTS;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.IA02
{
    /// <summary>Composition root for one nation. The MonoBehaviour only hosts identity and lifecycle.</summary>
    public sealed class IA02NationRuntime
    {
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02NationProfile profile;

        public IA02WorldState WorldState { get; }
        public IA02IntentBoard IntentBoard { get; }
        public IA02CommandQueue CommandQueue { get; }
        public IA02BuildReservationGrid Reservations { get; }
        public IA02BuildFailureMemory FailureMemory { get; }
        public IA02ZonePlanner ZonePlanner { get; }
        public IA02LotPlanner LotPlanner { get; }
        public IA02BackendBridge BackendBridge { get; }
        public IA02MissionDirector MissionDirector { get; }
        public IA02BuildCatalogAdapter Catalog { get; }
        public IA02EconomyDirector Economy { get; }
        public IA02EconomicModel EconomicModel { get; }
        public IA02CityPlanner CityPlanner { get; }
        public IA02BuildPlanRuntime BuildPlanRuntime { get; }
        public IA02ConstructionGovernor ConstructionGovernor { get; }
        public IA02StrategyArbiter Strategy { get; }
        public IA02BuildDirector BuildDirector { get; }
        public IA02WarDirector WarDirector { get; }
        public int WarEscalationLevel => WarDirector != null ? WarDirector.EscalationLevel : 0;
        public IA02NationalEconomyDirector NationalEconomy { get; }
        public IA02MilitaryDirector MilitaryDirector { get; }
        public IA02PlanningAdvisor PlanningAdvisor { get; }

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
        public string ConstructionCommandStatus
        {
            get
            {
                string command = BuildDirector != null ? BuildDirector.ActiveConstructionCommand : string.Empty;
                return string.IsNullOrWhiteSpace(command) ? "Nenhum" : command;
            }
        }
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
        private long currentTreasury;
        private bool foundationDiagnosticLogged;
        private bool foundationCommandDiagnosticLogged;

        private enum RuntimePhase
        {
            World,
            Economy,
            Planning,
            Market,
            War,
            Military,
            Construction,
            Build,
            Commands,
            Finalize
        }

        private RuntimePhase pendingPhase = RuntimePhase.World;
        private DadosPaisGoverno pendingCountry;
        private bool pendingRestoredFromSave;
        private bool pendingWorldChanged;
        private bool pendingImmediateLossRecovery;
        private string pendingLostEntityName = string.Empty;
        private bool pendingConstructionIntent;
        private bool pendingThreatened;
        private bool pendingMarketChanged;
        private float pendingCycleStartedAt;

        /// <summary>True when the current nation cycle was yielded by its time budget.</summary>
        public bool LastExecuteDeferred { get; private set; }

        public IA02NationRuntime(IA02Controller controller, IA02RuntimeContext context, IA02NationProfile profile)
        {
            this.controller = controller;
            this.context = context;
            this.profile = profile;
            WorldState = new IA02WorldState(controller, context);
            IntentBoard = new IA02IntentBoard();
            CommandQueue = new IA02CommandQueue();
            Reservations = new IA02BuildReservationGrid();
            FailureMemory = new IA02BuildFailureMemory();
            ZonePlanner = new IA02ZonePlanner(controller);
            LotPlanner = new IA02LotPlanner(controller, context, WorldState, Reservations, FailureMemory);
            BackendBridge = new IA02BackendBridge(context);
            MissionDirector = new IA02MissionDirector(CommandQueue);
            Catalog = new IA02BuildCatalogAdapter(controller.CapitalBlueprint, controller.BuildPlan);
            Economy = new IA02EconomyDirector(context, profile);
            EconomicModel = new IA02EconomicModel(profile);
            CityPlanner = new IA02CityPlanner(controller, context, WorldState, Catalog);
            BuildPlanRuntime = new IA02BuildPlanRuntime(controller, context, WorldState, Catalog, CityPlanner);
            ConstructionGovernor = new IA02ConstructionGovernor(controller, context, profile);
            Strategy = new IA02StrategyArbiter(IntentBoard);
            BuildDirector = new IA02BuildDirector(controller, context, WorldState, ConstructionGovernor, Catalog, Reservations, FailureMemory, CommandQueue, CityPlanner, ZonePlanner, LotPlanner, BackendBridge, BuildPlanRuntime);
            WarDirector = new IA02WarDirector(controller, context, WorldState, CityPlanner, MissionDirector);
            MilitaryDirector = new IA02MilitaryDirector(controller, context, BuildDirector);
            NationalEconomy = new IA02NationalEconomyDirector(context);
            PlanningAdvisor = new IA02PlanningAdvisor(context, WorldState, controller.EnablePlanningAdvisor);
        }

        public void RegisterHostileAggression(int attackerTeamId, Vector3 attackerPosition, float damage)
        {
            WarDirector?.RegisterHostileAggression(attackerTeamId, attackerPosition, damage);
        }

        public bool FoundationFundingGranted => Economy != null && Economy.FoundationFundingGranted;

        public void RestoreFoundationState(SaveIA02NationState state)
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

        /// <summary>
        /// Executes at most a few atomic phases. Each phase keeps its state on
        /// this runtime so an expensive nation never monopolizes a frame.
        /// </summary>
        public int Execute(float now, int maxOperations, bool restoredFromSave, float maxMilliseconds)
        {
            if (maxOperations <= 0)
            {
                LastExecuteDeferred = pendingPhase != RuntimePhase.World;
                return 0;
            }

            float startedAt = Time.realtimeSinceStartup;
            float budgetSeconds = Mathf.Max(0.0001f, maxMilliseconds * 0.001f);
            int operations = 0;
            LastExecuteDeferred = false;

            while (operations < maxOperations && Time.realtimeSinceStartup - startedAt < budgetSeconds)
            {
                float moduleStartedAt = Time.realtimeSinceStartup;
                switch (pendingPhase)
                {
                    case RuntimePhase.World:
                        pendingCycleStartedAt = moduleStartedAt;
                        mostExpensiveModule = "n/d";
                        mostExpensiveModuleMilliseconds = 0f;
                        pendingCountry = SistemaGovernoMundial.Instancia != null
                            ? SistemaGovernoMundial.Instancia.ObterPais(context.TeamId)
                            : null;
                        pendingRestoredFromSave = restoredFromSave;
                        pendingWorldChanged = WorldState.Refresh(now);
                        pendingImmediateLossRecovery = false;
                        pendingLostEntityName = string.Empty;
                        if (pendingWorldChanged && WorldState.TryConsumeOwnStructureLoss(out pendingLostEntityName))
                        {
                            IA02IntentType ignored;
                            pendingImmediateLossRecovery = TryResolveLossRecoveryIntent(pendingLostEntityName, out ignored);
                        }
                        TrackModule("WorldState", moduleStartedAt);
                        pendingPhase = RuntimePhase.Economy;
                        operations++;
                        break;

                    case RuntimePhase.Economy:
                        Economy.TryApplyInitialTreasury(pendingRestoredFromSave);
                        IntentBoard.Clear();
                        currentTreasury = pendingCountry != null ? pendingCountry.saldo : 0;
                        EconomicModel.Refresh(pendingCountry);
                        CityPlanner.RefreshCapital(now);
                        int capitalCost = 0;
                        if (CityPlanner.Capital == null && Catalog.TryGetCapital(out IA02BuildDefinition capitalDefinition))
                        {
                            capitalCost = capitalDefinition.Cost;
                        }
                        Economy.EnsureFoundationFunding(CityPlanner.Capital != null, capitalCost, pendingRestoredFromSave);
                        currentTreasury = pendingCountry != null ? pendingCountry.saldo : currentTreasury;
                        UpdateOperationalStatus(now, pendingCountry);
                        TrackModule("Economy", moduleStartedAt);
                        pendingPhase = RuntimePhase.Planning;
                        operations++;
                        break;

                    case RuntimePhase.Planning:
                        if (PlanningAdvisor != null)
                        {
                            PlanningAdvisor.Refresh(now, pendingCountry, Economy.IsEmergencyReserveRequired);
                        }
                        TrackModule("PlanningAdvisor", moduleStartedAt);
                        CityPlanner.PublishNeeds(IntentBoard, now, profile, pendingCountry, Economy.IsEmergencyReserveRequired);
                        if (pendingImmediateLossRecovery)
                        {
                            IA02IntentType recoveryIntent;
                            if (TryResolveLossRecoveryIntent(pendingLostEntityName, out recoveryIntent))
                            {
                                IntentBoard.Publish(recoveryIntent, 3200, "Reposicao imediata apos perda", now);
                                context.SetMetric("ia02.immediate_loss_recovery", 1d);
                            }
                        }
                        pendingConstructionIntent = false;
                        foreach (IA02Intent candidate in IntentBoard.All)
                        {
                            if (candidate != null && IsConstructionIntent(candidate.Type))
                            {
                                pendingConstructionIntent = true;
                                break;
                            }
                        }
                        pendingThreatened = IA02OperationalRules.IsCapitalThreatened(WorldState, CityPlanner.Capital, pendingCountry);
                        TrackModule("CityPlanner", moduleStartedAt);
                        pendingPhase = RuntimePhase.Market;
                        operations++;
                        break;

                    case RuntimePhase.Market:
                        pendingMarketChanged = NationalEconomy.Plan(now, IntentBoard, Economy.IsEmergencyReserveRequired, pendingConstructionIntent, pendingThreatened);
                        TrackModule("Market", moduleStartedAt);
                        pendingPhase = RuntimePhase.War;
                        operations++;
                        break;

                    case RuntimePhase.War:
                        WarDirector.Plan(now, IntentBoard, Economy.IsEmergencyReserveRequired);
                        TrackModule("WarDirector", moduleStartedAt);
                        pendingPhase = RuntimePhase.Military;
                        operations++;
                        break;

                    case RuntimePhase.Military:
                        if (MilitaryDirector != null)
                        {
                            MilitaryDirector.Tick(now);
                        }
                        TrackModule("MilitaryDirector", moduleStartedAt);
                        pendingPhase = RuntimePhase.Construction;
                        operations++;
                        break;

                    case RuntimePhase.Construction:
                        ConstructionGovernor.Refresh(now, pendingCountry, WorldState, Catalog, BuildDirector);
                        Strategy.Arbitrate(now, Economy.IsEmergencyReserveRequired);
                        UpdateObjectiveStatus(IntentBoard, pendingCountry, now);
                        TrackModule("ConstructionGovernor", moduleStartedAt);
                        pendingPhase = RuntimePhase.Build;
                        operations++;
                        break;

                    case RuntimePhase.Build:
                        bool foundationPending = CityPlanner != null && CityPlanner.Capital == null;
                        if (!pendingMarketChanged || foundationPending || pendingImmediateLossRecovery)
                        {
                            if (pendingImmediateLossRecovery && BuildDirector != null)
                            {
                                BuildDirector.ImmediateRecoveryRequested = true;
                            }
                            bool planAccepted = BuildDirector != null && BuildDirector.Plan(now, IntentBoard);
                            if (foundationPending && !foundationDiagnosticLogged && DiagnosticoDesempenhoJogo.CapturaAtiva)
                            {
                                DiagnosticoDesempenhoJogo.RegistrarEvento("IA02Build", "fundacao planejada=" + planAccepted);
                                foundationDiagnosticLogged = true;
                            }
                        }
                        TrackModule("BuildDirector", moduleStartedAt);
                        pendingPhase = RuntimePhase.Commands;
                        operations++;
                        break;

                    case RuntimePhase.Commands:
                        bool hasFoundationPending = CityPlanner != null && CityPlanner.Capital == null;
                        bool processQueuedCommand = BuildDirector == null || !BuildDirector.HasPendingConstruction || now >= BuildDirector.ConfirmationReadyAt;
                        if (processQueuedCommand)
                        {
                            bool cancelConstructionCommands = (ConstructionGovernor != null && ConstructionGovernor.ConstructionMode == IA02ConstructionMode.Frozen)
                                || (BuildDirector != null && BuildDirector.CancelQueuedConstructionCommand);
                            bool commandProcessed = CommandQueue.ProcessOne(now, cancelConstructionCommands);
                            if (hasFoundationPending && BuildDirector != null && BuildDirector.HasPendingConstruction
                                && !foundationCommandDiagnosticLogged && DiagnosticoDesempenhoJogo.CapturaAtiva)
                            {
                                DiagnosticoDesempenhoJogo.RegistrarEvento("IA02Build", "fila fundacao processada=" + commandProcessed);
                                foundationCommandDiagnosticLogged = true;
                            }
                        }
                        pendingPhase = RuntimePhase.Finalize;
                        operations++;
                        break;

                    default:
                        currentTreasury = pendingCountry != null ? pendingCountry.saldo : currentTreasury;
                        context.SetMetric("ia02.city.lots_reserved", Reservations.ReservedCount);
                        context.SetMetric("ia02.commands.pending", CommandQueue.PendingCount);
                        context.SetMetric("ia02.world.version", WorldState.Version);
                        lastRuntimeSliceMilliseconds = (Time.realtimeSinceStartup - pendingCycleStartedAt) * 1000f;
                        PublishDiagnostics(now);
                        pendingPhase = RuntimePhase.World;
                        pendingCountry = null;
                        pendingRestoredFromSave = false;
                        operations++;
                        break;
                }
            }

            LastExecuteDeferred = pendingPhase != RuntimePhase.World;
            return operations;
        }

        // Mantido como referencia para saves/diagnosticos antigos; o caminho de
        // execucao usa a sobrecarga com orcamento real acima.
        private int ExecuteLegacy(float now, int maxOperations, bool restoredFromSave)
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
            bool worldChanged = WorldState.Refresh(now);
            bool immediateLossRecovery = false;
            string lostEntityName = string.Empty;
            if (worldChanged && WorldState.TryConsumeOwnStructureLoss(out lostEntityName))
            {
                IA02IntentType recoveryIntent;
                if (TryResolveLossRecoveryIntent(lostEntityName, out recoveryIntent))
                {
                    immediateLossRecovery = true;
                }
            }
            operations += worldChanged ? 1 : 0;
            TrackModule("WorldState", moduleStartedAt);
            operations += Economy.TryApplyInitialTreasury(restoredFromSave) ? 1 : 0;
            IntentBoard.Clear();
            moduleStartedAt = Time.realtimeSinceStartup;
            currentTreasury = country != null ? country.saldo : 0;
            EconomicModel.Refresh(country);
            CityPlanner.RefreshCapital(now);
            int capitalCost = 0;
            if (CityPlanner.Capital == null && Catalog.TryGetCapital(out IA02BuildDefinition capitalDefinition))
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
            if (immediateLossRecovery)
            {
                IA02IntentType recoveryIntent;
                if (TryResolveLossRecoveryIntent(lostEntityName, out recoveryIntent))
                {
                    IntentBoard.Publish(
                        recoveryIntent,
                        3200,
                        "Reposicao imediata apos perda: " + (string.IsNullOrWhiteSpace(lostEntityName) ? "estrutura" : lostEntityName),
                        now);
                    context.SetMetric("ia02.immediate_loss_recovery", 1d);
                }
            }
            TrackModule("CityPlanner", moduleStartedAt);
            bool constructionIntentPending = false;
            foreach (IA02Intent candidate in IntentBoard.All)
            {
                if (candidate != null && IsConstructionIntent(candidate.Type))
                {
                    constructionIntentPending = true;
                    break;
                }
            }
            bool threatened = IA02OperationalRules.IsCapitalThreatened(WorldState, CityPlanner.Capital, country);
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

            // A prefeitura is the opening dependency of the whole nation. The
            // market/war/military modules can consume the small slice budget
            // before construction is reached, leaving the IA alive but with no
            // capital and no structures. Give only this first foundation step
            // a narrow priority; after the capital exists the normal budget is
            // unchanged and the five-second construction cadence still applies.
            bool foundationPending = CityPlanner != null && CityPlanner.Capital == null;
            if ((!marketChanged || foundationPending || immediateLossRecovery)
                && (operations < maxOperations || foundationPending || immediateLossRecovery))
            {
                moduleStartedAt = Time.realtimeSinceStartup;
                if (immediateLossRecovery && BuildDirector != null)
                {
                    BuildDirector.ImmediateRecoveryRequested = true;
                }
                bool planAccepted = BuildDirector != null && BuildDirector.Plan(now, IntentBoard);
                if (foundationPending && !foundationDiagnosticLogged)
                {
                    StringBuilder intentSummary = new StringBuilder();
                    foreach (IA02Intent intent in IntentBoard.All)
                    {
                        if (intent == null || !intent.Approved) continue;
                        if (intentSummary.Length > 0) intentSummary.Append(",");
                        intentSummary.Append(intent.Type);
                    }
                    Debug.Log("[IA02 Build] Diagnostico abertura: ops=" + operations
                        + "/" + maxOperations + " marketChanged=" + marketChanged
                        + " plan=" + planAccepted
                        + " director=" + (BuildDirector != null ? BuildDirector.Status : "null")
                        + " intents=" + intentSummary);
                    foundationDiagnosticLogged = true;
                }
                operations += planAccepted ? 1 : 0;
                TrackModule("BuildDirector", moduleStartedAt);
            }

            bool processQueuedCommand = BuildDirector == null
                || !BuildDirector.HasPendingConstruction
                || now >= BuildDirector.ConfirmationReadyAt;
            if ((operations < maxOperations || foundationPending) && processQueuedCommand)
            {
                bool cancelConstructionCommands = (ConstructionGovernor != null && ConstructionGovernor.ConstructionMode == IA02ConstructionMode.Frozen)
                    || (BuildDirector != null && BuildDirector.CancelQueuedConstructionCommand);
                bool commandProcessed = CommandQueue.ProcessOne(now, cancelConstructionCommands);
                if (foundationPending && BuildDirector != null && BuildDirector.HasPendingConstruction
                    && !foundationCommandDiagnosticLogged)
                {
                    Debug.Log("[IA02 Build] Diagnostico fila prefeitura: processado=" + commandProcessed
                        + " cancelado=" + cancelConstructionCommands
                        + " pendentes=" + CommandQueue.PendingCount
                        + " estado=" + BuildDirector.CurrentConstructionState
                        + " status=" + BuildDirector.Status);
                    foundationCommandDiagnosticLogged = true;
                }
                operations += commandProcessed ? 1 : 0;
            }

            currentTreasury = country != null ? country.saldo : currentTreasury;
            context.SetMetric("ia02.city.lots_reserved", Reservations.ReservedCount);
            context.SetMetric("ia02.commands.pending", CommandQueue.PendingCount);
            context.SetMetric("ia02.world.version", WorldState.Version);
            lastRuntimeSliceMilliseconds = (Time.realtimeSinceStartup - sliceStartedAt) * 1000f;
            PublishDiagnostics(now);
            return operations;
        }

        private bool TryResolveLossRecoveryIntent(string lostEntityName, out IA02IntentType intent)
        {
            intent = IA02IntentType.BuildLogistics;
            string normalized = IA_Text.Normalize(lostEntityName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized.Contains("prefeitura") || normalized.Contains("capital") || normalized.Contains("governo"))
            {
                intent = IA02IntentType.EstablishCapital;
            }
            else if (normalized.Contains("aeroporto") || normalized.Contains("airport") || normalized.Contains("base aerea"))
            {
                intent = normalized.Contains("comercial") || normalized.Contains("commercial")
                    ? IA02IntentType.BuildCommercialAirport
                    : IA02IntentType.BuildMilitaryAirport;
            }
            else if (normalized.Contains("estaleiro") || normalized.Contains("shipyard") || normalized.Contains("naval yard"))
            {
                intent = IA02IntentType.BuildShipyard;
            }
            else if (normalized.Contains("pier"))
            {
                intent = IA02IntentType.BuildPier;
            }
            else if (normalized.Contains("plataforma") || normalized.Contains("offshore"))
            {
                intent = IA02IntentType.BuildOffshorePlatform;
            }
            else if (normalized.Contains("energia") || normalized.Contains("usina") || normalized.Contains("gerador") || normalized.Contains("power"))
            {
                intent = IA02IntentType.BuildEnergy;
            }
            else if (normalized.Contains("fazenda") || normalized.Contains("farm") || normalized.Contains("comida") || normalized.Contains("food"))
            {
                intent = IA02IntentType.BuildFoodProduction;
            }
            else if (normalized.Contains("armazem") || normalized.Contains("warehouse") || normalized.Contains("storage"))
            {
                intent = IA02IntentType.BuildStorage;
            }
            else if (normalized.Contains("defesa") || normalized.Contains("torreta") || normalized.Contains("turret") || normalized.Contains("ciws"))
            {
                intent = IA02IntentType.BuildDefense;
            }
            else if (normalized.Contains("construtor") || normalized.Contains("vehicle constructor") || normalized.Contains("veiculo"))
            {
                intent = IA02IntentType.BuildVehicleConstructor;
            }
            else if (normalized.Contains("fabrica") || normalized.Contains("industria") || normalized.Contains("factory") || normalized.Contains("industrial"))
            {
                intent = IA02IntentType.BuildIndustry;
            }
            else if (normalized.Contains("quartel") || normalized.Contains("barraca") || normalized.Contains("barracks") || normalized.Contains("tent"))
            {
                intent = IA02IntentType.BuildMilitaryTent;
            }
            else if (normalized.Contains("casa") || normalized.Contains("apartamento") || normalized.Contains("resid") || normalized.Contains("house"))
            {
                intent = IA02IntentType.BuildResidentialCapacity;
            }
            else if (normalized.Contains("estrada") || normalized.Contains("rua") || normalized.Contains("logistica") || normalized.Contains("road"))
            {
                intent = IA02IntentType.BuildLogistics;
            }
            else
            {
                return false;
            }

            return true;
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

            // A lista abaixo monta dezenas de strings de depuracao. Fora de uma
            // captura elas nem sao consumidas pelo gravador, portanto nao devem
            // criar lixo de memoria durante a partida normal.
            bool captureActive = DiagnosticoDesempenhoJogo.CapturaAtiva;
            nextDiagnosticsAt = now + (captureActive ? 2f : 5f);
            if (!captureActive)
            {
                return;
            }

            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_progress", ProgressionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_objective", NextObjectiveStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_construction", ConstructionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_combat", CombatStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_military_reserve", MilitaryStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_planning_advisor", PlanningStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_market", MarketStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_capital_source", CapitalSourceStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_capital_item", CapitalItemIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_capital_prefab", CapitalPrefabStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_capital_diagnostic", CapitalDiagnosticStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_construction_mode", ConstructionModeStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_construction_state", ConstructionStateStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_construction_command", ConstructionCommandStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_active_command", ActiveCommandIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_pending_structure", PendingStructureIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_confirmation_deadline", ConfirmationDeadlineStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_treasury", TreasuryStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_buildings_total", BuildingsTotalStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_buildings_by_role", BuildingsByRoleStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_buildings_by_strategic_role", BuildingsByStrategicRoleStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_fixed_defense_count", ConstructionGovernor != null ? ConstructionGovernor.FixedDefenseCount.ToString(CultureInfo.InvariantCulture) : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_fixed_defense_limit", ConstructionGovernor != null ? ConstructionGovernor.MaxFixedDefenses.ToString(CultureInfo.InvariantCulture) : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_housing_need", HousingNeedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_food_coverage", FoodCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_energy_coverage", EnergyCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_storage_occupancy", StorageOccupancyStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_emergency_reserve", EmergencyReserveStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_available_construction_funds", AvailableConstructionFundsStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_city_coverage", CityCoverageStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_current_sector", CurrentSectorStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_current_need", CurrentNeedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_sequence_step", FoundationSequenceStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_need_score", NeedScoreStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_current_lot", CurrentLotIdStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_last_construction_completed_at", LastConstructionCompletedAtStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_construction_freeze_reason", ConstructionFreezeReasonStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_next_unfreeze_condition", NextUnfreezeConditionStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_blocked_intent", BuildDirector.BlockedIntentStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_block_reason", BuildDirector.BlockReasonStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_failures", BuildDirector.FailureCountStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_last_failure_code", LastFailureCodeStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_last_failure_detail", LastFailureDetailStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_cooldown", BuildDirector.GetCooldownStatus(now));
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_unblock", BuildDirector.NextUnblockCondition);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_foundation_funding_granted", FoundationFundingGrantedStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_foundation_capital_cost", FoundationCapitalCostStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_foundation_available_funds", FoundationAvailableFundsStatus);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_expensive_module", mostExpensiveModule + " " + mostExpensiveModuleMilliseconds.ToString("0.00") + " ms");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_last_slice", lastRuntimeSliceMilliseconds.ToString("0.00") + " ms");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_catalog_index_builds", Catalog.IndexBuildCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_catalog_queries", Catalog.IntentQueryCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_catalog_intent_queries", Catalog.IntentQueryCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_catalog_candidates", Catalog.CandidateReadCount.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_physics_checks", BuildDirector.PhysicsChecks.ToString());
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_build_plan", controller != null && controller.BuildPlan != null ? controller.BuildPlan.PlanId : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_build_step", BuildPlanRuntime != null ? BuildPlanRuntime.CurrentStepId : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_placement_mode", BuildPlanRuntime != null ? BuildPlanRuntime.PlacementModeStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_requested_role", BuildPlanRuntime != null ? BuildPlanRuntime.RequestedRoleStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_selected_slot", BuildPlanRuntime != null ? BuildPlanRuntime.SelectedSlotStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_slot_state", BuildPlanRuntime != null ? BuildPlanRuntime.SlotStateStatus : "n/d");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_alternative_slots", BuildPlanRuntime != null ? BuildPlanRuntime.AlternativeSlotsStatus : "0");
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia02_slot_validation", BuildPlanRuntime != null ? BuildPlanRuntime.SlotValidationResult : "n/d");
        }

        private static bool IsConstructionIntent(IA02IntentType type)
        {
            return type == IA02IntentType.EstablishCapital
                || type == IA02IntentType.BuildEnergy
                || type == IA02IntentType.BuildFoodProduction
                || type == IA02IntentType.BuildResidentialCapacity
                || type == IA02IntentType.BuildStorage
                || type == IA02IntentType.BuildLogistics
                || type == IA02IntentType.BuildIndustry
                || type == IA02IntentType.BuildDefense;
        }

        private void UpdateOperationalStatus(float now, DadosPaisGoverno country)
        {
            bool hasCapital = CityPlanner.Capital != null;
            int structures = WorldState.OwnedStructures.Count;
            int treasury = country != null ? (int)Math.Min(int.MaxValue, Math.Max(int.MinValue, country.saldo)) : 0;
            int energy = country != null ? country.energia : 0;
            int food = country != null ? country.comida : 0;
            bool threatened = IA02OperationalRules.IsCapitalThreatened(WorldState, CityPlanner.Capital, country);
            bool atWar = country != null && country.emGuerra;
            bool emergencyReserve = Economy.IsEmergencyReserveRequired;

            IA02NationStage resolvedStage = profile != null
                ? profile.ResolveOperationalStage(context.CurrentStage, hasCapital, structures, treasury, energy, food, threatened, atWar, emergencyReserve)
                : context.CurrentStage;
            IA02NationPosture resolvedPosture = profile != null
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
            context.SetMetric("ia02.progress.stage", (double)resolvedStage);
            context.SetMetric("ia02.progress.posture", (double)resolvedPosture);
            context.SetMetric("ia02.progress.structures", structures);
            context.SetMetric("ia02.progress.capital", hasCapital ? 1d : 0d);
            context.SetMetric("ia02.progress.threatened", threatened ? 1d : 0d);
        }

        private void UpdateObjectiveStatus(IA02IntentBoard board, DadosPaisGoverno country, float now)
        {
            IA02PopulationRecord population = context.GetPopulationSnapshot();
            if (CityPlanner != null && CityPlanner.Capital != null && population.Total > population.HousingCapacity)
            {
                NextObjectiveStatus = "Objetivo: ampliar moradia.";
                return;
            }

            IA02Intent intent = board.GetBestApproved(candidate => BuildDirector == null || BuildDirector.AllowsIntent(candidate, now));
            if (intent == null)
            {
                NextObjectiveStatus = BuildFallbackObjectiveStatus(country);
                return;
            }

            switch (intent.Type)
            {
                case IA02IntentType.EstablishCapital:
                    NextObjectiveStatus = "Objetivo: fundar a prefeitura.";
                    break;
                case IA02IntentType.BuildEnergy:
                    NextObjectiveStatus = "Objetivo: ampliar energia.";
                    break;
                case IA02IntentType.BuildFoodProduction:
                    NextObjectiveStatus = "Objetivo: garantir comida.";
                    break;
                case IA02IntentType.BuildResidentialCapacity:
                case IA02IntentType.BuildStarterHouse:
                case IA02IntentType.BuildMediumApartment:
                case IA02IntentType.BuildHighApartment:
                    NextObjectiveStatus = "Objetivo: ampliar moradia.";
                    break;
                case IA02IntentType.BuildStorage:
                    NextObjectiveStatus = "Objetivo: ampliar armazenamento.";
                    break;
                case IA02IntentType.BuildLogistics:
                    NextObjectiveStatus = "Objetivo: fortalecer logistica.";
                    break;
                case IA02IntentType.BuildRoad:
                    NextObjectiveStatus = "Objetivo: construir a rua de acesso.";
                    break;
                case IA02IntentType.BuildMilitaryAirport:
                    NextObjectiveStatus = "Objetivo: construir o aeroporto militar.";
                    break;
                case IA02IntentType.BuildCommercialAirport:
                    NextObjectiveStatus = "Objetivo: construir o aeroporto comercial.";
                    break;
                case IA02IntentType.BuildShipyard:
                    NextObjectiveStatus = "Objetivo: construir o estaleiro.";
                    break;
                case IA02IntentType.BuildIndustry:
                    NextObjectiveStatus = "Objetivo: ampliar industria.";
                    break;
                case IA02IntentType.BuildDefense:
                    NextObjectiveStatus = "Objetivo: reforcar defesa fixa.";
                    break;
                case IA02IntentType.DefendCapital:
                    NextObjectiveStatus = "Objetivo: defender a prefeitura.";
                    break;
                case IA02IntentType.CampaignAgainstCapital:
                    NextObjectiveStatus = "Objetivo: pressionar a prefeitura inimiga.";
                    break;
                case IA02IntentType.BuyResource:
                    NextObjectiveStatus = "Objetivo: comprar recursos essenciais.";
                    break;
                case IA02IntentType.SellResource:
                    NextObjectiveStatus = "Objetivo: vender excedente seguro.";
                    break;
                case IA02IntentType.Communicate:
                    NextObjectiveStatus = "Objetivo: comunicar e negociar.";
                    break;
                default:
                    NextObjectiveStatus = "Objetivo: avaliar nova ordem.";
                    break;
            }
        }

        private string BuildProgressionStatus(IA02NationStage stage, IA02NationPosture posture, bool hasCapital, int structures, int treasury, int energy, int food, bool threatened, bool atWar, bool emergencyReserve)
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
                case IA02NationStage.Initialization:
                case IA02NationStage.Reconnaissance:
                    return "Objetivo: iniciar infraestrutura basica.";
                case IA02NationStage.Survival:
                    return "Objetivo: fechar energia, comida e moradia.";
                case IA02NationStage.Stabilization:
                    return "Objetivo: reforcar armazenamento e logistica.";
                case IA02NationStage.UrbanDevelopment:
                case IA02NationStage.Industrialization:
                    return "Objetivo: expandir a cidade e a producao.";
                case IA02NationStage.Specialization:
                case IA02NationStage.RegionalProjection:
                case IA02NationStage.GlobalPower:
                    return "Objetivo: projetar poder e manter suporte.";
                case IA02NationStage.Recovering:
                    return "Objetivo: recuperar economia e servicos.";
                case IA02NationStage.Emergency:
                    return "Objetivo: responder a emergencia.";
                default:
                    return "Objetivo: avaliar proximo passo.";
            }
        }
    }

    public sealed class IA02WorldState
    {
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly List<IdentidadeUnidade> ownedStructures = new List<IdentidadeUnidade>(32);
        private readonly List<IdentidadeUnidade> enemyUnits = new List<IdentidadeUnidade>(32);
        private readonly List<MarcadorTerritorio> enemyCapitals = new List<MarcadorTerritorio>(8);
        private readonly List<IdentidadeUnidade> registeredIdentities = new List<IdentidadeUnidade>(96);
        private readonly List<MarcadorTerritorio> registeredMarkers = new List<MarcadorTerritorio>(16);
        private readonly Dictionary<int, string> ownStructureSnapshot = new Dictionary<int, string>(32);
        private readonly Dictionary<int, string> currentOwnStructureSnapshot = new Dictionary<int, string>(32);
        private float nextRefreshAt;
        private bool ownStructureSnapshotInitialized;
        private bool ownStructureLossPending;
        private string ownStructureLossName = string.Empty;

        public int Version { get; private set; }
        public IReadOnlyList<IdentidadeUnidade> OwnedStructures => ownedStructures;
        public IReadOnlyList<IdentidadeUnidade> EnemyUnits => enemyUnits;
        public IReadOnlyList<MarcadorTerritorio> EnemyCapitals => enemyCapitals;

        public bool TryConsumeOwnStructureLoss(out string entityName)
        {
            entityName = ownStructureLossName;
            if (!ownStructureLossPending)
            {
                entityName = string.Empty;
                return false;
            }

            ownStructureLossPending = false;
            ownStructureLossName = string.Empty;
            return true;
        }

        public IA02WorldState(IA02Controller controller, IA02RuntimeContext context)
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
            currentOwnStructureSnapshot.Clear();
            int ownTeam = context.TeamId;

            RegistroEntidadesJogo.FillUnidades(registeredIdentities);
            for (int i = 0; i < registeredIdentities.Count; i++)
            {
                IdentidadeUnidade identity = registeredIdentities[i];
                if (identity == null || identity.teamID <= 0)
                {
                    continue;
                }

                if (identity.teamID == ownTeam && identity.tipoUnidade == TipoUnidade.Estrutura)
                {
                    ownedStructures.Add(identity);
                    currentOwnStructureSnapshot[identity.GetInstanceID()] = identity.name;
                }
                else if (identity.teamID != ownTeam && identity.tipoUnidade != TipoUnidade.Estrutura)
                {
                    enemyUnits.Add(identity);
                }
            }

            if (ownStructureSnapshotInitialized && currentOwnStructureSnapshot.Count > 0 && !ownStructureLossPending)
            {
                foreach (KeyValuePair<int, string> previous in ownStructureSnapshot)
                {
                    if (currentOwnStructureSnapshot.ContainsKey(previous.Key))
                    {
                        continue;
                    }

                    ownStructureLossPending = true;
                    ownStructureLossName = previous.Value ?? string.Empty;
                    break;
                }
            }

            ownStructureSnapshot.Clear();
            foreach (KeyValuePair<int, string> current in currentOwnStructureSnapshot)
            {
                ownStructureSnapshot[current.Key] = current.Value;
            }
            ownStructureSnapshotInitialized = true;

            RegistroEntidadesJogo.FillMarcadoresTerritorio(registeredMarkers);
            for (int i = 0; i < registeredMarkers.Count; i++)
            {
                MarcadorTerritorio marker = registeredMarkers[i];
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

    public sealed class IA02IntentBoard
    {
        private readonly Dictionary<IA02IntentType, IA02Intent> intents = new Dictionary<IA02IntentType, IA02Intent>();

        public void Clear()
        {
            intents.Clear();
        }

        public void Publish(IA02IntentType type, int priority, string reason, float now)
        {
            if (!intents.TryGetValue(type, out IA02Intent intent))
            {
                intent = new IA02Intent { Id = "ia02.intent." + type, Type = type, CreatedAt = now };
                intents[type] = intent;
            }

            intent.Priority = Mathf.Max(intent.Priority, priority);
            intent.Reason = reason ?? string.Empty;
        }

        public IA02Intent GetBestApproved(System.Predicate<IA02Intent> filter = null)
        {
            IA02Intent best = null;
            foreach (IA02Intent intent in intents.Values)
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

        public void Complete(IA02IntentType type)
        {
            intents.Remove(type);
        }

        public IEnumerable<IA02Intent> All => intents.Values;
    }

    public sealed class IA02StrategyArbiter
    {
        private readonly IA02IntentBoard board;

        public IA02StrategyArbiter(IA02IntentBoard board)
        {
            this.board = board;
        }

        public void Arbitrate(float now, bool emergencyReserve)
        {
            foreach (IA02Intent intent in board.All)
            {
                intent.Approved = !emergencyReserve
                    || intent.Type == IA02IntentType.EstablishCapital
                    || intent.Type == IA02IntentType.DefendCapital
                    || intent.Type == IA02IntentType.BuyResource;
            }
        }
    }

    public sealed class IA02EconomyDirector
    {
        private readonly IA02RuntimeContext context;
        private readonly IA02NationProfile profile;
        private bool treasuryApplied;
        private bool foundationFundingGranted;
        private bool restoredFromSave;
        private int lastFoundationCapitalCost;
        private long lastFoundationTarget;
        private long lastFoundationAvailableFunds;

        public bool IsEmergencyReserveRequired { get; private set; }
        public bool FoundationFundingGranted => foundationFundingGranted;
        public int LastFoundationCapitalCost => lastFoundationCapitalCost;
        public long LastFoundationTarget => lastFoundationTarget;
        public long LastFoundationAvailableFunds => lastFoundationAvailableFunds;

        public IA02EconomyDirector(IA02RuntimeContext context, IA02NationProfile profile)
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

            long delta = profile.InitialTreasury - country.saldo;
            if (delta != 0)
            {
                government.AdicionarSaldo(context.TeamId, delta);
            }

            treasuryApplied = true;
            context.SetMetric("ia02.initial_treasury", profile.InitialTreasury);
            return true;
        }

        public bool EnsureFoundationFunding(bool capitalConfirmed, int capitalCost, bool restoredFromSave)
        {
            lastFoundationCapitalCost = capitalCost;
            if (capitalConfirmed || profile == null)
            {
                SistemaGovernoMundial.Instancia?.LiberarReservaFundacao(context.TeamId);
                lastFoundationAvailableFunds = 0;
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            long target = Math.Max(profile.InitialTreasury, Math.Max(5000L, capitalCost) + 2500L);
            lastFoundationTarget = target;
            if (government == null || country == null)
            {
                lastFoundationAvailableFunds = 0;
                return false;
            }

            // A reserva existe antes do primeiro comando para impedir que
            // economia, mercado ou outro diretor consuma o caixa entre o grant
            // e a confirmacao da prefeitura. Uma campanha nova pode herdar o
            // marcador de restore de um controlador persistente; esse marcador
            // nunca pode pular a garantia da primeira prefeitura.
            government.DefinirReservaFundacao(context.TeamId, target);
            lastFoundationAvailableFunds = Math.Max(country.saldo, (long)capitalCost);

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
            if (RTSResourceLedgerService.Instancia != null)
            {
                RTSResourceLedgerService.Instancia.TryProtectFoundation(context.TeamId, target, out lastFoundationAvailableFunds);
            }
            else
            {
                government.AdicionarSaldo(context.TeamId, target - country.saldo);
                lastFoundationAvailableFunds = country.saldo;
            }
            context.SetMetric("ia02.foundation_funds_protected", target);
            context.SetMetric("ia02.foundation_capital_cost", capitalCost);
            foundationFundingGranted = true;
            lastFoundationAvailableFunds = target;
            return true;
        }

        public void Refresh(DadosPaisGoverno country)
        {
            IsEmergencyReserveRequired = country != null && country.emGuerra && country.saldo < 500;
        }
    }

    public sealed class IA02CityPlanner
    {
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly IA02BuildCatalogAdapter catalog;
        private readonly HashSet<IA02IntentType> unavailableSequenceSteps = new HashSet<IA02IntentType>();
        private int unavailableSequenceCatalogVersion = -1;
        private MarcadorTerritorio capital;
        private float nextCapitalCheckAt;
        private int lastCapitalCatalogVersion = -1;
        private ComplexoGovernamental lastCapitalAnchor;
        private int lastFoodExpansionDay = -1;
        private int lastEnergyExpansionDay = -1;

        private static readonly IA02IntentType[] FoundationSequence =
        {
            IA02IntentType.BuildEnergy,
            // A casa inicial so pode ser posicionada com seguranca depois que
            // existe uma via conectavel. A rua vem antes da moradia para a
            // abertura nao deixar casas isoladas ou sem pavimento.
            IA02IntentType.BuildRoad,
            IA02IntentType.BuildStarterHouse,
            IA02IntentType.BuildFoodProduction,
            IA02IntentType.BuildResidentialCapacity,
            IA02IntentType.BuildMediumApartment,
            IA02IntentType.BuildHighApartment,
            IA02IntentType.BuildMilitaryTent,
            IA02IntentType.BuildVehicleConstructor,
            IA02IntentType.BuildStorage,
            IA02IntentType.BuildRoad,
            IA02IntentType.BuildMilitaryAirport,
            IA02IntentType.BuildCommercialAirport,
            IA02IntentType.BuildShipyard,
            IA02IntentType.BuildPier,
            IA02IntentType.BuildOffshorePlatform,
            IA02IntentType.BuildIndustry
        };

        public MarcadorTerritorio Capital => capital;
        public string Status { get; private set; } = "Aguardando prefeitura propria.";
        public string CapitalSource { get; private set; } = "Missing";
        public string CapitalDiagnostic { get; private set; } = "Capital ainda nao validada.";

        public IA02CityPlanner(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02BuildCatalogAdapter catalog)
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

            if (catalog.TryGetCapital(out IA02BuildDefinition capitalDefinition))
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

        public bool IsFoundationSequenceIntent(IA02IntentType intent)
        {
            return intent == IA02IntentType.EstablishCapital || Array.IndexOf(FoundationSequence, intent) >= 0;
        }

        public void MarkSequenceCatalogUnavailable(IA02IntentType intent, string diagnostic)
        {
            if (!IsFoundationSequenceIntent(intent) || intent == IA02IntentType.EstablishCapital)
            {
                return;
            }

            unavailableSequenceSteps.Add(intent);
            FoundationSequenceStatus = intent + " indisponivel: " + (diagnostic ?? "catalogo sem item");
            context.SetMetric("ia02.sequence.catalog_skipped", 1d);
            context.SetMetric("ia02.sequence.catalog_skipped_step", (int)intent);
        }

        public List<string> CaptureUnavailableSequenceSteps()
        {
            List<string> result = new List<string>(unavailableSequenceSteps.Count);
            foreach (IA02IntentType step in unavailableSequenceSteps)
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
                if (Enum.TryParse(value, true, out IA02IntentType step) && IsFoundationSequenceIntent(step))
                {
                    unavailableSequenceSteps.Add(step);
                }
            }
        }

        private bool HasOwnedStructure(IA02StrategicRole role)
        {
            IA02Manager manager = controller != null ? controller.Manager : null;
            return manager != null
                && manager.WorldRegistry != null
                && manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, role) > 0;
        }

        private bool HasOwnedStructureMatching(IA02StrategicRole role, params string[] tokens)
        {
            IA02Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null)
            {
                return false;
            }

            IReadOnlyList<IA02WorldEntityRecord> records = manager.WorldRegistry.GetByTeam(context.TeamId);
            for (int i = 0; i < records.Count; i++)
            {
                IA02WorldEntityRecord record = records[i];
                if (record == null || record.Kind != IA02WorldEntityKind.Structure || record.StrategicRole != role)
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

        private bool IsFoundationStepComplete(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.BuildEnergy:
                    return HasOwnedStructure(IA02StrategicRole.EnergyProduction);
                case IA02IntentType.BuildFoodProduction:
                    return HasOwnedStructure(IA02StrategicRole.FoodProduction);
                case IA02IntentType.BuildResidentialCapacity:
                    return HasOwnedStructure(IA02StrategicRole.Residential);
                case IA02IntentType.BuildStarterHouse:
                    return HasOwnedStructureMatching(IA02StrategicRole.Residential, "casa", "house");
                case IA02IntentType.BuildMediumApartment:
                    return HasOwnedStructureMatching(IA02StrategicRole.Residential, "medio", "médio", "apartamento", "apartment");
                case IA02IntentType.BuildHighApartment:
                    return HasOwnedStructureMatching(IA02StrategicRole.Residential, "hard", "alto", "high", "torre");
                case IA02IntentType.BuildMilitaryTent:
                    return HasOwnedStructureMatching(IA02StrategicRole.MilitaryProduction, "tenda", "tent", "quartel", "barracks");
                case IA02IntentType.BuildVehicleConstructor:
                    return HasOwnedStructureMatching(IA02StrategicRole.MilitaryProduction, "construtor", "veiculo", "veículo", "vehicle");
                case IA02IntentType.BuildStorage:
                    return HasOwnedStructure(IA02StrategicRole.Storage);
                case IA02IntentType.BuildRoad:
                    return HasOwnedStructure(IA02StrategicRole.Logistics);
                case IA02IntentType.BuildMilitaryAirport:
                    return HasOwnedStructureMatching(IA02StrategicRole.Airfield, "militar", "military", "aeroporto_militar");
                case IA02IntentType.BuildCommercialAirport:
                    return HasOwnedStructureMatching(IA02StrategicRole.Airfield, "comercial", "commercial", "aeroporto_comercial");
                case IA02IntentType.BuildShipyard:
                    return HasOwnedStructure(IA02StrategicRole.NavalBase)
                        || HasOwnedStructure(IA02StrategicRole.Shipyard)
                        || HasOwnedStructure(IA02StrategicRole.Port)
                        || HasOwnedStructure(IA02StrategicRole.Pier);
                case IA02IntentType.BuildPier:
                    return HasOwnedStructure(IA02StrategicRole.Pier);
                case IA02IntentType.BuildOffshorePlatform:
                    return HasOwnedStructureMatching(IA02StrategicRole.NavalBase, "plataforma", "offshore");
                case IA02IntentType.BuildIndustry:
                    return HasOwnedStructure(IA02StrategicRole.Industrial);
                default:
                    return intent == IA02IntentType.EstablishCapital && capital != null;
            }
        }

        private string ResolveFoundationReason(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.BuildEnergy: return "Energia inicial";
                case IA02IntentType.BuildFoodProduction: return "Comida inicial";
                case IA02IntentType.BuildResidentialCapacity: return "Moradia inicial";
                case IA02IntentType.BuildStarterHouse: return "Casa inicial";
                case IA02IntentType.BuildMediumApartment: return "Apartamento medio";
                case IA02IntentType.BuildHighApartment: return "Apartamento alto";
                case IA02IntentType.BuildMilitaryTent: return "Tenda militar";
                case IA02IntentType.BuildVehicleConstructor: return "Construtor de veiculos";
                case IA02IntentType.BuildStorage: return "Armazenamento inicial";
                case IA02IntentType.BuildRoad: return "Rua de acesso";
                case IA02IntentType.BuildMilitaryAirport: return "Aeroporto militar";
                case IA02IntentType.BuildCommercialAirport: return "Aeroporto comercial";
                case IA02IntentType.BuildShipyard: return "Estaleiro naval";
                case IA02IntentType.BuildPier: return "Pier naval";
                case IA02IntentType.BuildOffshorePlatform: return "Plataforma offshore";
                case IA02IntentType.BuildIndustry: return "Industria";
                default: return intent.ToString();
            }
        }

        public void PublishNeeds(IA02IntentBoard board, float now, IA02NationProfile profile, DadosPaisGoverno country, bool emergencyReserve)
        {
            if (capital == null)
            {
                board.Publish(IA02IntentType.EstablishCapital, 1000, Status, now);
                FoundationSequenceStatus = "Prefeitura";
                return;
            }

            IA02PopulationRecord population = context.GetPopulationSnapshot();

            if (controller != null && controller.UseScriptedOpening && population.Total > population.HousingCapacity)
            {
                FoundationSequenceStatus = "Moradia urgente";
                board.Publish(IA02IntentType.BuildResidentialCapacity, 2200, FoundationSequenceStatus, now);
                Status = "Sequencia de fundacao: " + FoundationSequenceStatus + ".";
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
                    IA02IntentType step = FoundationSequence[i];
                    bool residentialStep = step == IA02IntentType.BuildStarterHouse
                        || step == IA02IntentType.BuildMediumApartment
                        || step == IA02IntentType.BuildHighApartment;
                    if (residentialStep && population.Total <= population.HousingCapacity)
                    {
                        continue;
                    }

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

            bool threatened = IA02OperationalRules.IsCapitalThreatened(world, capital, country);
            bool atWar = country != null && country.emGuerra;
            IA02NationStage stage = context.CurrentStage;
            IA02NationPosture posture = context.CurrentPosture;
            int structures = world.OwnedStructures.Count;
            int energy = country != null ? country.energia : 0;
            int food = country != null ? country.comida : 0;

            PublishBuildNeed(board, now, profile, IA02IntentType.BuildEnergy, stage, posture, structures, threatened, atWar, DeveConstruirEnergia(country), "Energia inicial");
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildFoodProduction, stage, posture, structures, threatened, atWar, DeveConstruirComida(country), "Producao de alimentos");
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildResidentialCapacity, stage, posture, structures, threatened, atWar, population.Total > population.HousingCapacity, "Moradia inicial");
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildStorage, stage, posture, structures, threatened, atWar, DeveConstruirArmazenamento(), "Reserva e armazenamento");
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildLogistics, stage, posture, structures, threatened, atWar, structures < 6 || stage >= IA02NationStage.UrbanDevelopment, "Acesso e logistica");
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildIndustry, stage, posture, structures, threatened, atWar, structures >= 5 && stage >= IA02NationStage.Industrialization, "Base industrial");
            bool shouldPublishDefense = threatened
                || atWar
                || (stage != IA02NationStage.Recovering
                    && posture != IA02NationPosture.Recovery
                    && structures >= 6
                    && profile != null
                    && (profile.DefenseWeight >= 0.45f || profile.MilitaryWeight >= 0.45f));
            PublishBuildNeed(board, now, profile, IA02IntentType.BuildDefense, stage, posture, structures, threatened, atWar, shouldPublishDefense, "Defesa territorial");

            if (structures >= 6)
            {
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildEnergy, stage, posture, structures, threatened, atWar, DeveConstruirEnergia(country), "Reforco energetico");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildFoodProduction, stage, posture, structures, threatened, atWar, DeveConstruirComida(country), "Seguranca alimentar");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildResidentialCapacity, stage, posture, structures, threatened, atWar, profile == null || profile.CautionWeight >= 0.45f, "Expansao residencial");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildStorage, stage, posture, structures, threatened, atWar, DeveConstruirArmazenamento(), "Suporte industrial");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildLogistics, stage, posture, structures, threatened, atWar, profile == null || profile.ExpansionWeight >= 0.45f || stage >= IA02NationStage.RegionalProjection, "Rede logistica");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildIndustry, stage, posture, structures, threatened, atWar, stage >= IA02NationStage.Industrialization && (profile == null || profile.IndustryWeight >= 0.40f), "Expansao industrial");
                PublishBuildNeed(board, now, profile, IA02IntentType.BuildDefense, stage, posture, structures, threatened, atWar, threatened || atWar || profile == null || profile.DefenseWeight >= 0.45f, "Seguranca da cidade");
            }

            Status = BuildStatus(stage, posture, threatened, atWar, emergencyReserve, structures);
        }

        /// <summary>
        /// Armazém só é pedido quando há capacidade realmente pressionada. O limite
        /// absoluto é três, definido pelas três âncoras de logística da IA02.
        /// </summary>
        private bool DeveConstruirArmazenamento()
        {
            IA02Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null || context == null)
            {
                return false;
            }

            int existentes = manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, IA02StrategicRole.Storage);
            if (existentes >= 3)
            {
                return false;
            }

            if (existentes == 0)
            {
                return true;
            }

            if (context.TryGetResource("storage", out IA02ResourceRecord armazenamento)
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
                IA02StrategicRole.FoodProduction,
                country != null ? country.comida : 0,
                800,
                6,
                ref lastFoodExpansionDay);
        }

        private bool DeveConstruirEnergia(DadosPaisGoverno country)
        {
            return DeveExpandirInfraestrutura(
                IA02StrategicRole.EnergyProduction,
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
        private bool DeveExpandirInfraestrutura(IA02StrategicRole role, int estoque, int minimo, int limite, ref int ultimoDia)
        {
            IA02Manager manager = controller != null ? controller.Manager : null;
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

        private bool PublishBuildNeed(IA02IntentBoard board, float now, IA02NationProfile profile, IA02IntentType intent, IA02NationStage stage, IA02NationPosture posture, int structures, bool threatened, bool atWar, bool shouldPublish, string reason)
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

        private static bool IsDevelopmentStage(IA02NationStage stage)
        {
            return stage >= IA02NationStage.UrbanDevelopment && stage <= IA02NationStage.GlobalPower;
        }

        private int ResolveFallbackPriority(IA02IntentType intent, IA02NationStage stage, IA02NationPosture posture, int structures, bool threatened, bool atWar)
        {
            int priority = intent == IA02IntentType.BuildEnergy ? 520
                : intent == IA02IntentType.BuildFoodProduction ? 500
                : intent == IA02IntentType.BuildResidentialCapacity ? 480
                : intent == IA02IntentType.BuildStorage ? 460
                : intent == IA02IntentType.BuildLogistics ? 440
                : intent == IA02IntentType.BuildIndustry ? 540
                : intent == IA02IntentType.BuildDefense ? 510
                : 0;

            if (priority <= 0)
            {
                return 0;
            }

            priority += structures <= 1 ? 70 : structures == 2 ? 50 : structures == 3 ? 35 : structures == 4 ? 25 : 15;

            switch (stage)
            {
                case IA02NationStage.Initialization:
                case IA02NationStage.Reconnaissance:
                    priority += intent == IA02IntentType.BuildEnergy ? 120 : intent == IA02IntentType.BuildFoodProduction ? 90 : intent == IA02IntentType.BuildResidentialCapacity ? 50 : intent == IA02IntentType.BuildStorage ? 15 : 10;
                    break;
                case IA02NationStage.Survival:
                    priority += intent == IA02IntentType.BuildEnergy ? 90 : intent == IA02IntentType.BuildFoodProduction ? 110 : intent == IA02IntentType.BuildResidentialCapacity ? 80 : intent == IA02IntentType.BuildStorage ? 25 : 15;
                    break;
                case IA02NationStage.Stabilization:
                    priority += intent == IA02IntentType.BuildEnergy ? 45 : intent == IA02IntentType.BuildFoodProduction ? 55 : intent == IA02IntentType.BuildResidentialCapacity ? 60 : intent == IA02IntentType.BuildStorage ? 85 : 75;
                    break;
                case IA02NationStage.UrbanDevelopment:
                    priority += intent == IA02IntentType.BuildEnergy ? 25 : intent == IA02IntentType.BuildFoodProduction ? 25 : intent == IA02IntentType.BuildResidentialCapacity ? 45 : intent == IA02IntentType.BuildStorage ? 95 : 105;
                    break;
                case IA02NationStage.Industrialization:
                    priority += intent == IA02IntentType.BuildEnergy ? 20 : intent == IA02IntentType.BuildFoodProduction ? 10 : intent == IA02IntentType.BuildResidentialCapacity ? 20 : intent == IA02IntentType.BuildStorage ? 115 : 110;
                    break;
                case IA02NationStage.Specialization:
                    priority += intent == IA02IntentType.BuildEnergy ? 10 : intent == IA02IntentType.BuildFoodProduction ? 5 : intent == IA02IntentType.BuildResidentialCapacity ? 10 : intent == IA02IntentType.BuildStorage ? 125 : 125;
                    break;
                case IA02NationStage.RegionalProjection:
                    priority += intent == IA02IntentType.BuildEnergy ? 5 : intent == IA02IntentType.BuildFoodProduction ? 5 : intent == IA02IntentType.BuildResidentialCapacity ? 5 : intent == IA02IntentType.BuildStorage ? 120 : 135;
                    break;
                case IA02NationStage.GlobalPower:
                    priority += intent == IA02IntentType.BuildEnergy ? 5 : intent == IA02IntentType.BuildFoodProduction ? 5 : intent == IA02IntentType.BuildResidentialCapacity ? 5 : intent == IA02IntentType.BuildStorage ? 110 : 140;
                    break;
                case IA02NationStage.Recovering:
                    priority += intent == IA02IntentType.BuildEnergy ? 120 : intent == IA02IntentType.BuildFoodProduction ? 120 : intent == IA02IntentType.BuildResidentialCapacity ? 100 : intent == IA02IntentType.BuildStorage ? 45 : 30;
                    break;
                case IA02NationStage.Emergency:
                    priority += intent == IA02IntentType.BuildEnergy ? 20 : intent == IA02IntentType.BuildFoodProduction ? 20 : intent == IA02IntentType.BuildResidentialCapacity ? 20 : intent == IA02IntentType.BuildStorage ? 10 : 5;
                    break;
            }

            switch (posture)
            {
                case IA02NationPosture.Development:
                    priority += intent == IA02IntentType.BuildEnergy ? 20 : intent == IA02IntentType.BuildFoodProduction ? 20 : intent == IA02IntentType.BuildResidentialCapacity ? 15 : 0;
                    break;
                case IA02NationPosture.Peace:
                    priority += intent == IA02IntentType.BuildResidentialCapacity ? 20 : intent == IA02IntentType.BuildStorage ? 10 : 0;
                    break;
                case IA02NationPosture.Alert:
                    priority += intent == IA02IntentType.BuildStorage ? 20 : intent == IA02IntentType.BuildLogistics ? 25 : 0;
                    break;
                case IA02NationPosture.Preparation:
                    priority += intent == IA02IntentType.BuildStorage ? 25 : intent == IA02IntentType.BuildLogistics ? 30 : intent == IA02IntentType.BuildEnergy ? 10 : 0;
                    break;
                case IA02NationPosture.Defense:
                    priority += intent == IA02IntentType.BuildStorage ? 20 : intent == IA02IntentType.BuildResidentialCapacity ? 10 : 0;
                    break;
                case IA02NationPosture.LimitedAttack:
                    priority += intent == IA02IntentType.BuildLogistics ? 30 : intent == IA02IntentType.BuildStorage ? 20 : 0;
                    break;
                case IA02NationPosture.War:
                    priority += intent == IA02IntentType.BuildLogistics ? 35 : intent == IA02IntentType.BuildStorage ? 25 : 0;
                    break;
                case IA02NationPosture.Retreat:
                case IA02NationPosture.Recovery:
                    priority += intent == IA02IntentType.BuildEnergy ? 20 : intent == IA02IntentType.BuildFoodProduction ? 20 : intent == IA02IntentType.BuildResidentialCapacity ? 20 : 0;
                    break;
            }

            if (threatened || atWar)
            {
                priority = Mathf.RoundToInt(priority * 0.75f);
            }

            return Mathf.Clamp(priority, 0, 999);
        }

        private string BuildStatus(IA02NationStage stage, IA02NationPosture posture, bool threatened, bool atWar, bool emergencyReserve, int structures)
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
                case IA02NationStage.Initialization:
                case IA02NationStage.Reconnaissance:
                    return "Planejamento inicial: fundacao e energia.";
                case IA02NationStage.Survival:
                    return "Planejamento de sobrevivencia: comida, moradia e energia.";
                case IA02NationStage.Stabilization:
                    return "Planejamento de estabilizacao: armazenamento e logistica.";
                case IA02NationStage.UrbanDevelopment:
                    return "Planejamento urbano: consolidando infraestrutura.";
                case IA02NationStage.Industrialization:
                case IA02NationStage.Specialization:
                    return "Planejamento industrial: ampliando producao e suporte.";
                case IA02NationStage.RegionalProjection:
                case IA02NationStage.GlobalPower:
                    return "Planejamento expansivo: projetando poder.";
                case IA02NationStage.Recovering:
                    return "Planejamento de recuperacao: reerguendo a cidade.";
                case IA02NationStage.Emergency:
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

    public sealed class IA02BuildCatalogAdapter
    {
        private static readonly List<DadosConstrucao> EmptyCatalog = new List<DadosConstrucao>(0);
        private readonly DadosConstrucao explicitCapital;
        private readonly IA02BuildPlan buildPlan;
        private readonly List<DadosConstrucao> cachedCatalog = new List<DadosConstrucao>(128);
        private readonly List<IA02BuildDefinition> cachedDefinitions = new List<IA02BuildDefinition>(128);
        private readonly Dictionary<string, IA02BuildDefinition> itemsById = new Dictionary<string, IA02BuildDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IA02StrategicRole, List<IA02BuildDefinition>> itemsByRole = new Dictionary<IA02StrategicRole, List<IA02BuildDefinition>>();
        private readonly Dictionary<IA02BuildArchetype, List<IA02BuildDefinition>> itemsByArchetype = new Dictionary<IA02BuildArchetype, List<IA02BuildDefinition>>();
        private readonly Dictionary<IA02BuildDomain, List<IA02BuildDefinition>> itemsByDomain = new Dictionary<IA02BuildDomain, List<IA02BuildDefinition>>();
        private List<DadosConstrucao> cachedSource;
        private DadosConstrucao cachedExplicitCapital;
        private IA02BuildPlan cachedBuildPlan;
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

        public IA02BuildCatalogAdapter(DadosConstrucao explicitCapital, IA02BuildPlan buildPlan)
        {
            this.explicitCapital = explicitCapital;
            this.buildPlan = buildPlan;
        }

        public IA02BuildCatalogAdapter(DadosConstrucao explicitCapital)
            : this(explicitCapital, null)
        {
        }

        public bool TryGetCapital(out IA02BuildDefinition definition)
        {
            intentQueryCount++;
            ResetDiagnostic();
            EnsureIndex();
            if (explicitCapital != null && TryCreate(explicitCapital, IA02BuildArchetype.Command, out definition))
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

            if (itemsByRole.TryGetValue(IA02StrategicRole.Capital, out List<IA02BuildDefinition> capitalItems))
            {
                for (int i = 0; i < capitalItems.Count; i++)
                {
                    candidateReadCount++;
                    IA02BuildDefinition candidate = capitalItems[i];
                    if (candidate != null && candidate.Item != null)
                    {
                        definition = candidate;
                        MarkCapital("StrategicRole.Capital", definition);
                        return true;
                    }
                }
            }

            if (itemsByRole.TryGetValue(IA02StrategicRole.Government, out List<IA02BuildDefinition> governmentItems))
            {
                for (int i = 0; i < governmentItems.Count; i++)
                {
                    candidateReadCount++;
                    IA02BuildDefinition candidate = governmentItems[i];
                    if (candidate != null && candidate.Item != null)
                    {
                        definition = candidate;
                        MarkCapital("StrategicRole.Government", definition);
                        return true;
                    }
                }
            }

            if (itemsByRole.TryGetValue(IA02StrategicRole.Command, out List<IA02BuildDefinition> commandItems))
            {
                for (int i = 0; i < commandItems.Count; i++)
                {
                    candidateReadCount++;
                    IA02BuildDefinition candidate = commandItems[i];
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

        public bool TryGetForBlueprint(DadosConstrucao item, out IA02BuildDefinition definition)
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

        private void MarkCapital(string source, IA02BuildDefinition definition)
        {
            MarkExact(source, definition);
            CapitalItemIdStatus = definition.ItemId;
            CapitalPrefabStatus = definition.Item != null && definition.Item.PrefabDaUnidade != null ? definition.Item.PrefabDaUnidade.name : "n/d";
        }

        public bool TryGetForIntent(IA02IntentType intent, DadosPaisGoverno country, IA02NationStage stage, out IA02BuildDefinition definition)
        {
            return TryGetForIntent(intent, country, stage, false, out definition);
        }

        public bool TryGetForIntent(IA02IntentType intent, DadosPaisGoverno country, IA02NationStage stage, bool allowFoundationBudgetOverride, out IA02BuildDefinition definition)
        {
            intentQueryCount++;
            ResetDiagnostic();
            EnsureIndex();
            if (IsForcedOpeningIntent(intent) && TryGetForcedOpeningDefinition(intent, out definition))
            {
                MarkExact("BuildPlan abertura", definition);
                return true;
            }

            IA02BuildDefinition bestExact = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < cachedDefinitions.Count; i++)
            {
                candidateReadCount++;
                IA02BuildDefinition candidate = cachedDefinitions[i];
                if (!IsCandidateAllowedForIntent(candidate, intent, stage))
                {
                    continue;
                }

                if (!allowFoundationBudgetOverride && country != null && candidate.Cost > country.saldo)
                {
                    continue;
                }

                int priority = intent == IA02IntentType.BuildDefense
                    ? (candidate.StrategicRole == IA02StrategicRole.AntiAirDefense ? 100 : 10)
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

        private static int ResolveInfrastructurePriority(IA02IntentType intent, IA02BuildDefinition candidate)
        {
            if (candidate == null || candidate.Item == null) return 0;
            string text = IA_Text.Normalize(candidate.Item.GetDisplayName() + " " + candidate.Item.name + " " + candidate.Item.aliases);
            if (intent == IA02IntentType.BuildEnergy)
            {
                // Uma fonte de alta capacidade reduz a quantidade de usinas e
                // preserva área do mapa; nuclear só entra se a IA puder pagá-la.
                if (text.Contains("nuclear") || text.Contains("nucleo") || text.Contains("reator") || text.Contains("reator")) return 300;
                if (text.Contains("hidro") || text.Contains("hydro") || text.Contains("termica") || text.Contains("thermal")) return 180;
                return 40;
            }
            if (intent == IA02IntentType.BuildFoodProduction)
            {
                return text.Contains("fazenda") || text.Contains("farm") ? 120 : 20;
            }
            return 0;
        }

        private static bool IsForcedOpeningIntent(IA02IntentType intent)
        {
            return intent == IA02IntentType.BuildMilitaryAirport
                || intent == IA02IntentType.BuildCommercialAirport
                || intent == IA02IntentType.BuildShipyard
                || intent == IA02IntentType.BuildMilitaryTent
                || intent == IA02IntentType.BuildVehicleConstructor;
        }

        private bool TryGetForcedOpeningDefinition(IA02IntentType intent, out IA02BuildDefinition definition)
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
                    IA02BuildDefinition candidate = cachedDefinitions[i];
                    if (candidate == null || candidate.Item == null
                        || !string.Equals(candidate.Item.GetStableId(), preferredItemId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (TryCreateForcedOpeningDefinition(intent, candidate.Item, out definition)) return true;
                }
            }

            for (int i = 0; i < cachedDefinitions.Count; i++)
            {
                IA02BuildDefinition candidate = cachedDefinitions[i];
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
                    definition.MinimumStage = IA02NationStage.Initialization;
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

            IReadOnlyList<IA02BuildPlanStep> steps = buildPlan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
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

        private static string PreferredForcedOpeningItemId(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.BuildMilitaryTent: return "militar.tenda";
                case IA02IntentType.BuildVehicleConstructor: return "militar.fabrica_veiculos";
                case IA02IntentType.BuildShipyard: return "naval.estaleiro";
                case IA02IntentType.BuildMilitaryAirport: return "aeroporto_militar";
                case IA02IntentType.BuildCommercialAirport: return "aeroporto_comercial";
                default: return string.Empty;
            }
        }

        private static bool TryCreateForcedOpeningDefinition(IA02IntentType intent, DadosConstrucao item, out IA02BuildDefinition definition)
        {
            definition = null;
            if (item == null || !item.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                return false;
            }

            IA02BuildArchetype archetype = ResolveArchetype(intent);
            IA02BuildDomain domain = ResolveDomain(intent, item);
            IA02StrategicRole role = ResolveStrategicRole(intent);
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA02BuildDefinition
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
                RequiresNavalExit = domain == IA02BuildDomain.Coastal || domain == IA02BuildDomain.Water,
                RequiresPower = false,
                IsFixedDefense = false,
                MaximumRecommendedCount = 1,
                MinimumStage = IA02NationStage.Initialization,
                CatalogResolution = "BuildPlan abertura forcada"
            };
            return true;
        }

        private static bool TryCreateBuildPlanDefinition(DadosConstrucao item, out IA02BuildDefinition definition)
        {
            definition = null;
            if (item == null || !item.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                return false;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            IA02BuildArchetype archetype = InferArchetype(item, prefab);
            IA02BuildDomain domain = InferDomain(capabilities, prefab);
            // Defesa antiaerea e uma instalacao terrestre. Alguns prefabs antigos
            // carregam a capability Air por causa do alvo que defendem; isso nao
            // deve transforma-los em aeroportos nem exigir slot de pista.
            if (IsAntiAirItem(item, capabilities))
            {
                archetype = IA02BuildArchetype.Defense;
                domain = IA02BuildDomain.Land;
            }
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA02BuildDefinition
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
                RequiresNavalExit = domain == IA02BuildDomain.Coastal || domain == IA02BuildDomain.Water,
                StrategicRole = (IA02StrategicRole)(int)item.StrategicRole,
                MinimumStage = ResolveMinimumStage((IA02StrategicRole)(int)item.StrategicRole)
            };
            definition.StrategicRole = ResolveStrategicRole(definition, item);
            definition.MinimumStage = ResolveMinimumStage(definition.StrategicRole);
            definition.MinimumTreasury = Mathf.Max(0, definition.Cost);
            definition.RequiresPower = definition.StrategicRole == IA02StrategicRole.EnergyProduction;
            definition.IsFixedDefense = definition.StrategicRole == IA02StrategicRole.FixedDefense
                || definition.StrategicRole == IA02StrategicRole.AntiAirDefense
                || definition.StrategicRole == IA02StrategicRole.CoastalDefense;
            definition.MaximumRecommendedCount = ResolveMaximumRecommendedCount(definition.StrategicRole);
            return definition.StrategicRole != IA02StrategicRole.None || definition.IsStructure;
        }

        private static IA02BuildDomain ResolveDomain(IA02IntentType intent, DadosConstrucao item)
        {
            switch (intent)
            {
                case IA02IntentType.BuildMilitaryAirport:
                case IA02IntentType.BuildCommercialAirport:
                    return IA02BuildDomain.Airfield;
                case IA02IntentType.BuildShipyard:
                    if (item != null && item.TryGetPrefabBasico(out GameObject prefab) && prefab != null)
                    {
                        return NavalPlacementResolver.RequiresCoastalPlacement(prefab) ? IA02BuildDomain.Coastal : IA02BuildDomain.Water;
                    }

                    return IA02BuildDomain.Coastal;
                case IA02IntentType.BuildPier:
                case IA02IntentType.BuildOffshorePlatform:
                    return IA02BuildDomain.Coastal;
                default:
                    return IA02BuildDomain.Land;
            }
        }

        private static string[] ForcedOpeningTokens(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.BuildMilitaryAirport:
                    return new[] { "aeroporto_militar", "aeroporto militar", "military airport", "base aerea militar" };
                case IA02IntentType.BuildCommercialAirport:
                    return new[] { "aeroporto_comercial", "aeroporto comercial", "commercial airport", "terminal civil" };
                case IA02IntentType.BuildShipyard:
                    return new[] { "estaleiro", "shipyard", "naval yard" };
                case IA02IntentType.BuildPier:
                    return new[] { "pier" };
                case IA02IntentType.BuildOffshorePlatform:
                    return new[] { "plataforma", "offshore" };
                case IA02IntentType.BuildMilitaryTent:
                    return new[] { "tenda", "tent", "quartel", "barracks" };
                case IA02IntentType.BuildVehicleConstructor:
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

        private void MarkExact(string source, IA02BuildDefinition definition)
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
                IReadOnlyList<IA02BuildPlanStep> steps = buildPlan.Steps;
                for (int i = 0; i < steps.Count; i++)
                {
                    IA02BuildPlanStep step = steps[i];
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

            if (!TryCreateAny(item, out IA02BuildDefinition definition))
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
            definition.RequiresPower = definition.StrategicRole == IA02StrategicRole.EnergyProduction;
            definition.IsFixedDefense = definition.StrategicRole == IA02StrategicRole.FixedDefense
                || definition.StrategicRole == IA02StrategicRole.AntiAirDefense
                || definition.StrategicRole == IA02StrategicRole.CoastalDefense;
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

        private static void AddToIndex<TKey>(Dictionary<TKey, List<IA02BuildDefinition>> index, TKey key, IA02BuildDefinition definition)
        {
            if (index == null || definition == null)
            {
                return;
            }

            if (!index.TryGetValue(key, out List<IA02BuildDefinition> list))
            {
                list = new List<IA02BuildDefinition>(8);
                index[key] = list;
            }

            list.Add(definition);
        }

        private static IA02StrategicRole ResolveStrategicRole(IA02BuildDefinition definition, DadosConstrucao item)
        {
            if (definition == null || item == null)
            {
                return IA02StrategicRole.None;
            }

            IA02StrategicRole itemRole = (IA02StrategicRole)(int)item.StrategicRole;
            if (itemRole != IA02StrategicRole.None)
            {
                return itemRole;
            }

            string semanticText = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.NomeItem ?? string.Empty) + " "
                + (item.aliases ?? string.Empty));
            IA_ConstructionCapability semanticCapabilities = item.GetResolvedCapabilities();
            if ((semanticCapabilities & IA_ConstructionCapability.Defense) != 0
                && ContainsAny(semanticText, "antiaerea", "anti aerea", "anti-air", "antiair", "air defense", "defesa aerea"))
            {
                return IA02StrategicRole.AntiAirDefense;
            }
            if (ContainsAny(semanticText, "aeroporto", "airport", "airbase", "base_aerea"))
            {
                return IA02StrategicRole.Airfield;
            }

            if (ContainsAny(semanticText, "estaleiro", "shipyard", "naval", "porto_naval"))
            {
                return IA02StrategicRole.NavalBase;
            }

            if (ContainsAny(semanticText, "farm", "fazenda", "agri", "comida", "food", "cultivo"))
            {
                return IA02StrategicRole.FoodProduction;
            }

            if (ContainsAny(semanticText, "casa", "resid", "moradia", "village", "imovel"))
            {
                return IA02StrategicRole.Residential;
            }

            if (ContainsAny(semanticText, "rua", "road", "estrada", "street", "avenida", "logistica", "logistics"))
            {
                return IA02StrategicRole.Logistics;
            }

            if (ContainsAny(semanticText, "fabrica", "factory", "industria", "industry"))
            {
                return IA02StrategicRole.Industrial;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            bool hasExplicitCapabilities = item.Capacidades != IA_ConstructionCapability.Auto;
            if (hasExplicitCapabilities
                && (capabilities & IA_ConstructionCapability.Defense) != 0
                && (capabilities & IA_ConstructionCapability.Structure) != 0)
            {
                if (definition.Domain == IA02BuildDomain.Coastal || definition.Domain == IA02BuildDomain.Water)
                {
                    return IA02StrategicRole.CoastalDefense;
                }

                if (definition.Archetype == IA02BuildArchetype.Air)
                {
                    return IA02StrategicRole.AntiAirDefense;
                }

                return IA02StrategicRole.FixedDefense;
            }

            if ((capabilities & IA_ConstructionCapability.Power) != 0)
            {
                return IA02StrategicRole.EnergyProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Warehouse) != 0)
            {
                return IA02StrategicRole.Storage;
            }

            if ((capabilities & IA_ConstructionCapability.Factory) != 0)
            {
                return IA02StrategicRole.Industrial;
            }

            if ((capabilities & IA_ConstructionCapability.Barracks) != 0)
            {
                return IA02StrategicRole.MilitaryProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Economy) != 0)
            {
                return IA02StrategicRole.FoodProduction;
            }

            if ((capabilities & IA_ConstructionCapability.Civil) != 0)
            {
                return IA02StrategicRole.Residential;
            }

            if (definition.Archetype == IA02BuildArchetype.Naval)
            {
                return IA02StrategicRole.NavalBase;
            }

            if (definition.Archetype == IA02BuildArchetype.Air)
            {
                return IA02StrategicRole.Airfield;
            }

            if (definition.Archetype == IA02BuildArchetype.Logistics)
            {
                return IA02StrategicRole.Logistics;
            }

            if (definition.Archetype == IA02BuildArchetype.Command)
            {
                return IA02StrategicRole.Command;
            }

            if (definition.Archetype == IA02BuildArchetype.Research)
            {
                return IA02StrategicRole.Research;
            }

            return IA02StrategicRole.None;
        }

        private static IA02NationStage ResolveMinimumStage(IA02StrategicRole role)
        {
            switch (role)
            {
                case IA02StrategicRole.Residential:
                case IA02StrategicRole.FoodProduction:
                case IA02StrategicRole.EnergyProduction:
                case IA02StrategicRole.Storage:
                    return IA02NationStage.Survival;
                case IA02StrategicRole.Logistics:
                    return IA02NationStage.Stabilization;
                case IA02StrategicRole.FixedDefense:
                case IA02StrategicRole.AntiAirDefense:
                case IA02StrategicRole.CoastalDefense:
                case IA02StrategicRole.MilitaryProduction:
                    return IA02NationStage.UrbanDevelopment;
                case IA02StrategicRole.Airfield:
                case IA02StrategicRole.NavalBase:
                case IA02StrategicRole.Industrial:
                    return IA02NationStage.Industrialization;
                case IA02StrategicRole.Research:
                case IA02StrategicRole.Command:
                case IA02StrategicRole.Capital:
                case IA02StrategicRole.Government:
                    return IA02NationStage.Initialization;
                default:
                    return IA02NationStage.Initialization;
            }
        }

        private static int ResolveMaximumRecommendedCount(IA02StrategicRole role)
        {
            switch (role)
            {
                case IA02StrategicRole.FixedDefense:
                case IA02StrategicRole.AntiAirDefense:
                case IA02StrategicRole.CoastalDefense:
                    return 12;
                case IA02StrategicRole.Storage:
                    return 6;
                case IA02StrategicRole.EnergyProduction:
                case IA02StrategicRole.FoodProduction:
                    return 8;
                case IA02StrategicRole.Logistics:
                    return 10;
                default:
                    return 4;
            }
        }

        private static bool IsCandidateAllowedForIntent(IA02BuildDefinition definition, IA02IntentType intent, IA02NationStage stage)
        {
            if (definition == null)
            {
                return false;
            }

            if (!definition.IsStructure)
            {
                return false;
            }

            bool sequenceIntent = intent == IA02IntentType.BuildRoad
                || intent == IA02IntentType.BuildMilitaryAirport
                || intent == IA02IntentType.BuildCommercialAirport
                || intent == IA02IntentType.BuildShipyard
                || intent == IA02IntentType.BuildPier
                || intent == IA02IntentType.BuildOffshorePlatform
                || intent == IA02IntentType.BuildIndustry;
            if (definition.MinimumStage > stage && !sequenceIntent)
            {
                return false;
            }

            switch (intent)
            {
                case IA02IntentType.BuildDefense:
                    return definition.IsFixedDefense;
                case IA02IntentType.BuildEnergy:
                    return definition.StrategicRole == IA02StrategicRole.EnergyProduction;
                case IA02IntentType.BuildFoodProduction:
                    return definition.StrategicRole == IA02StrategicRole.FoodProduction;
                case IA02IntentType.BuildResidentialCapacity:
                    return definition.StrategicRole == IA02StrategicRole.Residential;
                case IA02IntentType.BuildStarterHouse:
                    return definition.StrategicRole == IA02StrategicRole.Residential && IsNamedCandidate(definition, "casa", "house");
                case IA02IntentType.BuildMediumApartment:
                    return definition.StrategicRole == IA02StrategicRole.Residential && IsNamedCandidate(definition, "medio", "médio", "apartamento", "apartment");
                case IA02IntentType.BuildHighApartment:
                    return definition.StrategicRole == IA02StrategicRole.Residential && IsNamedCandidate(definition, "hard", "alto", "high", "torre");
                case IA02IntentType.BuildMilitaryTent:
                    return definition.StrategicRole == IA02StrategicRole.MilitaryProduction && IsNamedCandidate(definition, "tenda", "tent", "quartel", "barracks");
                case IA02IntentType.BuildVehicleConstructor:
                    return definition.StrategicRole == IA02StrategicRole.MilitaryProduction && IsNamedCandidate(definition, "construtor", "veiculo", "veículo", "vehicle");
                case IA02IntentType.BuildStorage:
                    return definition.StrategicRole == IA02StrategicRole.Storage;
                case IA02IntentType.BuildLogistics:
                    return definition.StrategicRole == IA02StrategicRole.Logistics;
                case IA02IntentType.BuildRoad:
                    return definition.StrategicRole == IA02StrategicRole.Logistics
                        && IsRoadCandidate(definition);
                case IA02IntentType.BuildMilitaryAirport:
                    return definition.StrategicRole == IA02StrategicRole.Airfield
                        && IsMilitaryAirportCandidate(definition);
                case IA02IntentType.BuildCommercialAirport:
                    return definition.StrategicRole == IA02StrategicRole.Airfield
                        && IsCommercialAirportCandidate(definition);
                case IA02IntentType.BuildShipyard:
                    return definition.StrategicRole == IA02StrategicRole.NavalBase
                        || definition.StrategicRole == IA02StrategicRole.Shipyard
                        || definition.StrategicRole == IA02StrategicRole.Port
                        || definition.StrategicRole == IA02StrategicRole.Pier;
                case IA02IntentType.BuildPier:
                    return definition.StrategicRole == IA02StrategicRole.Pier
                        || CandidateText(definition).Contains("pier");
                case IA02IntentType.BuildOffshorePlatform:
                    return definition.StrategicRole == IA02StrategicRole.NavalBase
                        && (CandidateText(definition).Contains("plataforma") || CandidateText(definition).Contains("offshore"));
                case IA02IntentType.BuildIndustry:
                    return definition.StrategicRole == IA02StrategicRole.Industrial;
                case IA02IntentType.EstablishCapital:
                    return definition.StrategicRole == IA02StrategicRole.Capital
                        || definition.StrategicRole == IA02StrategicRole.Government
                        || definition.StrategicRole == IA02StrategicRole.Command;
                default:
                    return false;
            }
        }

        private static string CandidateText(IA02BuildDefinition definition)
        {
            if (definition == null || definition.Item == null)
            {
                return string.Empty;
            }

            return IA_Text.Normalize((definition.ItemId ?? string.Empty) + " "
                + (definition.DisplayName ?? string.Empty) + " "
                + (definition.Item.NomeItem ?? string.Empty) + " "
                + (definition.Item.aliases ?? string.Empty));
        }

        private static bool HasCandidateToken(IA02BuildDefinition definition, params string[] tokens)
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

        private static bool IsRoadCandidate(IA02BuildDefinition definition)
        {
            return HasCandidateToken(definition, "rua", "road", "estrada", "street", "avenida", "logistica", "logistics");
        }

        private static bool IsMilitaryAirportCandidate(IA02BuildDefinition definition)
        {
            return HasCandidateToken(definition, "militar", "military", "aeroporto_militar", "airbase", "base_aerea");
        }

        private static bool IsCommercialAirportCandidate(IA02BuildDefinition definition)
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

        private static IA02BuildArchetype ResolveArchetype(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.BuildEnergy: return IA02BuildArchetype.Energy;
                case IA02IntentType.BuildResidentialCapacity: return IA02BuildArchetype.Residential;
                case IA02IntentType.BuildFoodProduction: return IA02BuildArchetype.Agricultural;
                case IA02IntentType.BuildStorage: return IA02BuildArchetype.Storage;
                case IA02IntentType.BuildLogistics: return IA02BuildArchetype.Logistics;
                case IA02IntentType.BuildRoad: return IA02BuildArchetype.Logistics;
                case IA02IntentType.BuildMilitaryAirport:
                case IA02IntentType.BuildCommercialAirport: return IA02BuildArchetype.Air;
                case IA02IntentType.BuildShipyard:
                case IA02IntentType.BuildPier:
                case IA02IntentType.BuildOffshorePlatform: return IA02BuildArchetype.Naval;
                case IA02IntentType.BuildStarterHouse:
                case IA02IntentType.BuildMediumApartment:
                case IA02IntentType.BuildHighApartment: return IA02BuildArchetype.Residential;
                case IA02IntentType.BuildMilitaryTent:
                case IA02IntentType.BuildVehicleConstructor: return IA02BuildArchetype.Military;
                case IA02IntentType.BuildIndustry: return IA02BuildArchetype.Industrial;
                case IA02IntentType.BuildDefense: return IA02BuildArchetype.Defense;
                default: return IA02BuildArchetype.Command;
            }
        }

        private static IA02StrategicRole ResolveStrategicRole(IA02IntentType intent)
        {
            switch (intent)
            {
                case IA02IntentType.EstablishCapital:
                    return IA02StrategicRole.Command;
                case IA02IntentType.BuildEnergy:
                    return IA02StrategicRole.EnergyProduction;
                case IA02IntentType.BuildResidentialCapacity:
                    return IA02StrategicRole.Residential;
                case IA02IntentType.BuildFoodProduction:
                    return IA02StrategicRole.FoodProduction;
                case IA02IntentType.BuildStorage:
                    return IA02StrategicRole.Storage;
                case IA02IntentType.BuildLogistics:
                    return IA02StrategicRole.Logistics;
                case IA02IntentType.BuildRoad:
                    return IA02StrategicRole.Logistics;
                case IA02IntentType.BuildMilitaryAirport:
                case IA02IntentType.BuildCommercialAirport:
                    return IA02StrategicRole.Airfield;
                case IA02IntentType.BuildShipyard:
                    return IA02StrategicRole.Shipyard;
                case IA02IntentType.BuildPier:
                    return IA02StrategicRole.Pier;
                case IA02IntentType.BuildOffshorePlatform:
                    return IA02StrategicRole.NavalBase;
                case IA02IntentType.BuildStarterHouse:
                case IA02IntentType.BuildMediumApartment:
                case IA02IntentType.BuildHighApartment:
                    return IA02StrategicRole.Residential;
                case IA02IntentType.BuildMilitaryTent:
                case IA02IntentType.BuildVehicleConstructor:
                    return IA02StrategicRole.MilitaryProduction;
                case IA02IntentType.BuildIndustry:
                    return IA02StrategicRole.Industrial;
                case IA02IntentType.BuildDefense:
                    return IA02StrategicRole.None;
                default:
                    return IA02StrategicRole.None;
            }
        }

        private static bool TryCreate(DadosConstrucao item, IA02BuildArchetype requested, out IA02BuildDefinition definition)
        {
            return TryCreateInternal(item, requested, true, out definition);
        }

        private static bool TryCreateAny(DadosConstrucao item, out IA02BuildDefinition definition)
        {
            return TryCreateInternal(item, IA02BuildArchetype.Command, false, out definition);
        }

        private static bool TryCreateInternal(DadosConstrucao item, IA02BuildArchetype requested, bool requireRequestedArchetype, out IA02BuildDefinition definition)
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

            IA02BuildArchetype inferred = InferArchetype(item, prefab);
            if (requireRequestedArchetype && requested != IA02BuildArchetype.Command && inferred != requested) return false;

            IA02BuildArchetype archetype = requireRequestedArchetype && requested == IA02BuildArchetype.Command
                ? IA02BuildArchetype.Command
                : inferred;
            IA02BuildDomain domain = requireRequestedArchetype && requested == IA02BuildArchetype.Command
                ? IA02BuildDomain.Land
                : InferDomain(capabilities, prefab);
            if (IsAntiAirItem(item, capabilities))
            {
                archetype = IA02BuildArchetype.Defense;
                domain = IA02BuildDomain.Land;
            }
            Bounds bounds = ResolveBounds(prefab);
            definition = new IA02BuildDefinition
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
                RequiresNavalExit = domain == IA02BuildDomain.Coastal || domain == IA02BuildDomain.Water,
                StrategicRole = (IA02StrategicRole)(int)item.StrategicRole,
                MinimumStage = ResolveMinimumStage((IA02StrategicRole)(int)item.StrategicRole)
            };
            return true;
        }

        private static bool IsCapitalCandidate(DadosConstrucao item)
        {
            if (item == null) return false;
            IA02StrategicRole itemRole = (IA02StrategicRole)(int)item.StrategicRole;
            return itemRole == IA02StrategicRole.Capital
                || itemRole == IA02StrategicRole.Government
                || itemRole == IA02StrategicRole.Command;
        }

        private static int ScoreFallback(IA02IntentType intent, DadosConstrucao item, IA02BuildDefinition candidate)
        {
            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            string key = IA_Text.Normalize(item.GetStableId() + " " + item.GetDisplayName() + " " + item.aliases);
            int score = 0;
            switch (intent)
            {
                case IA02IntentType.BuildEnergy:
                    if ((capabilities & IA_ConstructionCapability.Power) != 0) score += 1200;
                    if (item.categoria == DadosConstrucao.CategoriaItem.Energia) score += 900;
                    if (ContainsAny(key, "usina", "energia", "solar", "nuclear", "power")) score += 600;
                    break;
                case IA02IntentType.BuildFoodProduction:
                    if (ContainsAny(key, "fazenda", "farm", "agri", "comida", "food", "cultivo")) score += 1200;
                    if ((capabilities & IA_ConstructionCapability.Economy) != 0) score += 250;
                    break;
                case IA02IntentType.BuildResidentialCapacity:
                    if ((capabilities & IA_ConstructionCapability.Civil) != 0) score += 1100;
                    if (item.categoria == DadosConstrucao.CategoriaItem.Urbana) score += 350;
                    if (ContainsAny(key, "casa", "resid", "moradia", "imovel", "village")) score += 600;
                    break;
                case IA02IntentType.BuildStorage:
                    if ((capabilities & IA_ConstructionCapability.Warehouse) != 0) score += 1200;
                    if (ContainsAny(key, "armazem", "warehouse", "galpao", "deposito")) score += 650;
                    break;
                case IA02IntentType.BuildLogistics:
                    if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura) score += 800;
                    if (ContainsAny(key, "logistic", "estrada", "road", "ponte", "porto", "pier", "terminal")) score += 650;
                    break;
                case IA02IntentType.BuildIndustry:
                    if ((capabilities & IA_ConstructionCapability.Factory) != 0) score += 1200;
                    if (ContainsAny(key, "fabrica", "factory", "industria", "construtor")) score += 650;
                    break;
                case IA02IntentType.BuildDefense:
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

        private static bool IsNamedCandidate(IA02BuildDefinition definition, params string[] tokens)
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

        private static bool RequiresRoadConnection(IA02BuildDomain domain, IA02BuildArchetype archetype)
        {
            if (domain != IA02BuildDomain.Land) return false;
            return archetype == IA02BuildArchetype.Industrial
                || archetype == IA02BuildArchetype.Logistics
                || archetype == IA02BuildArchetype.Military
                || archetype == IA02BuildArchetype.Defense;
        }

        private static IA02BuildArchetype InferArchetype(DadosConstrucao item, GameObject prefab)
        {
            IA02StrategicRole itemRole = (IA02StrategicRole)(int)item.StrategicRole;
            if (itemRole == IA02StrategicRole.Capital
                || itemRole == IA02StrategicRole.Government
                || itemRole == IA02StrategicRole.Command)
            {
                return IA02BuildArchetype.Command;
            }

            IA_ConstructionCapability capabilities = item.GetResolvedCapabilities();
            if ((capabilities & IA_ConstructionCapability.Power) != 0) return IA02BuildArchetype.Energy;
            if ((capabilities & IA_ConstructionCapability.Warehouse) != 0) return IA02BuildArchetype.Storage;
            if ((capabilities & IA_ConstructionCapability.Factory) != 0) return IA02BuildArchetype.Industrial;
            if ((capabilities & IA_ConstructionCapability.Barracks) != 0) return IA02BuildArchetype.Military;
            if ((capabilities & IA_ConstructionCapability.Defense) != 0) return IA02BuildArchetype.Defense;
            if ((capabilities & IA_ConstructionCapability.Airport) != 0
                || (capabilities & IA_ConstructionCapability.MilitaryAirport) != 0
                || (capabilities & IA_ConstructionCapability.CommercialAirport) != 0)
            {
                return IA02BuildArchetype.Air;
            }
            if ((capabilities & IA_ConstructionCapability.Economy) != 0) return IA02BuildArchetype.Agricultural;
            if ((capabilities & IA_ConstructionCapability.Civil) != 0) return IA02BuildArchetype.Residential;
            if (item.categoria == DadosConstrucao.CategoriaItem.Energia) return IA02BuildArchetype.Energy;
            if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura) return IA02BuildArchetype.Logistics;
            if (item.categoria == DadosConstrucao.CategoriaItem.Marinha) return IA02BuildArchetype.Naval;
            if (item.categoria == DadosConstrucao.CategoriaItem.Exercito) return IA02BuildArchetype.Military;
            if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica) return IA02BuildArchetype.Air;
            return IA02BuildArchetype.Residential;
        }

        private static IA02BuildDomain InferDomain(IA_ConstructionCapability capabilities, GameObject prefab)
        {
            if ((capabilities & IA_ConstructionCapability.Naval) != 0)
            {
                return NavalPlacementResolver.RequiresCoastalPlacement(prefab) ? IA02BuildDomain.Coastal : IA02BuildDomain.Water;
            }
            if ((capabilities & IA_ConstructionCapability.Air) != 0) return IA02BuildDomain.Airfield;
            return IA02BuildDomain.Land;
        }

        private static bool IsAntiAirItem(DadosConstrucao item, IA_ConstructionCapability capabilities)
        {
            if (item == null) return false;
            if ((IA02StrategicRole)(int)item.StrategicRole == IA02StrategicRole.AntiAirDefense) return true;
            string text = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.NomeItem ?? string.Empty) + " "
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

    public sealed class IA02BuildReservationGrid
    {
        private readonly Dictionary<string, IA02LotState> lots = new Dictionary<string, IA02LotState>();
        public int ReservedCount { get; private set; }

        public bool TryReserve(IA02BuildLot lot)
        {
            if (lot == null || string.IsNullOrEmpty(lot.Key) || (lots.TryGetValue(lot.Key, out IA02LotState state) && state != IA02LotState.Free)) return false;
            lots[lot.Key] = IA02LotState.Reserved;
            lot.State = IA02LotState.Reserved;
            ReservedCount++;
            return true;
        }

        public void MarkOccupied(IA02BuildLot lot)
        {
            if (lot == null) return;
            lots[lot.Key] = IA02LotState.Occupied;
            lot.State = IA02LotState.Occupied;
            ReservedCount = Mathf.Max(0, ReservedCount - 1);
        }

        public void Release(IA02BuildLot lot, bool invalid)
        {
            if (lot == null) return;
            lots[lot.Key] = invalid ? IA02LotState.TemporarilyInvalid : IA02LotState.Free;
            lot.State = lots[lot.Key];
            ReservedCount = Mathf.Max(0, ReservedCount - 1);
        }
    }

    public enum IA02IntentBlockReason
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

    public sealed class IA02IntentCooldown
    {
        public float CooldownUntil;
        public float BlockedUntil;
        public int FailureCount;
        public string LastStateToken = string.Empty;
        public IA02FailureCode LastFailureCode = IA02FailureCode.None;
        public IA02IntentBlockReason LastBlockReason = IA02IntentBlockReason.None;
        public string LastKey = string.Empty;
    }

    public sealed class IA02CircuitBreaker
    {
        private readonly IA02IntentCooldown state = new IA02IntentCooldown();

        public int FailureCount => state.FailureCount;
        public string LastStateToken => state.LastStateToken;
        public IA02FailureCode LastFailureCode => state.LastFailureCode;
        public IA02IntentBlockReason LastBlockReason => state.LastBlockReason;

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

        public void RecordFailure(float now, string stateToken, IA02FailureCode failureCode, IA02IntentBlockReason blockReason)
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
            state.LastFailureCode = IA02FailureCode.None;
            state.LastBlockReason = IA02IntentBlockReason.None;
            state.LastKey = string.Empty;
        }
    }

    public class IA02FailureMemory
    {
        private readonly Dictionary<string, IA02IntentCooldown> failures = new Dictionary<string, IA02IntentCooldown>();

        public bool IsCoolingDown(string key, float now)
        {
            return !CanAttempt(key, now, string.Empty);
        }

        public void Record(string key, float now)
        {
            Record(key, now, string.Empty, IA02FailureCode.NoValidLot, IA02IntentBlockReason.Busy);
        }

        public bool CanAttempt(string key, float now, string stateToken)
        {
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }

            if (!failures.TryGetValue(key, out IA02IntentCooldown record))
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

        public void Record(string key, float now, string stateToken, IA02FailureCode failureCode, IA02IntentBlockReason blockReason)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!failures.TryGetValue(key, out IA02IntentCooldown record))
            {
                record = new IA02IntentCooldown();
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

        public bool TryGetCooldown(string key, float now, out int failureCount, out IA02IntentBlockReason reason, out float remainingSeconds, out bool requiresStateChange)
        {
            failureCount = 0;
            reason = IA02IntentBlockReason.None;
            remainingSeconds = 0f;
            requiresStateChange = false;
            if (string.IsNullOrEmpty(key) || !failures.TryGetValue(key, out IA02IntentCooldown record))
            {
                return false;
            }

            failureCount = record.FailureCount;
            reason = record.LastBlockReason;
            requiresStateChange = record.BlockedUntil == float.MaxValue;
            remainingSeconds = requiresStateChange ? -1f : Mathf.Max(0f, record.CooldownUntil - now);
            return true;
        }

        public string BuildIntentKey(IA02IntentType intent, IA02StrategicRole role, string regionKey)
        {
            return intent + "|" + role + "|" + (string.IsNullOrWhiteSpace(regionKey) ? "global" : regionKey);
        }

        public string BuildFailureKey(IA02IntentType intent, IA02StrategicRole role, string regionKey, IA02FailureCode failureCode)
        {
            return BuildIntentKey(intent, role, regionKey) + "|" + failureCode;
        }

        public string BuildStateToken(int catalogVersion, int worldVersion, bool threatened, bool atWar, int treasury, int energy, int food)
        {
            return catalogVersion + "|" + worldVersion + "|" + threatened + "|" + atWar + "|" + treasury + "|" + energy + "|" + food;
        }
    }

    public sealed class IA02BuildFailureMemory : IA02FailureMemory
    {
    }

    public sealed class IA02ZonePlanner
    {
        private readonly IA02Controller controller;
        public IA02ZonePlanner(IA02Controller controller) { this.controller = controller; }

        public bool TryResolvePlanningOrigin(IA02CityPlanner city, IA02BuildDefinition definition, out Vector3 origin, out string reason)
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
                IA02BuildSlot capitalSlot = controller != null ? controller.CapitalSlot : null;
                if (capitalSlot == null)
                {
                    if (controller != null && !controller.UsePreparedSlots)
                    {
                        origin = controller.transform.position;
                        return true;
                    }

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
            if (definition.Archetype == IA02BuildArchetype.Industrial || definition.Archetype == IA02BuildArchetype.Naval) origin += new Vector3(90f, 0f, 0f);
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

    public sealed class IA02LotPlanner
    {
        private readonly IA02BuildValidator validator;
        public IA02LotPlanner(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02BuildReservationGrid reservations, IA02BuildFailureMemory failures)
        {
            validator = new IA02BuildValidator(controller, context, world, reservations, failures);
        }
        public int CandidatesEvaluated => validator != null ? validator.CandidatesEvaluated : 0;
        public int PhysicsChecks => validator != null ? validator.PhysicsChecks : 0;
        public bool TryFindLot(IA02BuildDefinition definition, Vector3 origin, float now, int maxCandidates, int maxPhysicsChecks, out IA02BuildLot lot, out string reason)
        {
            return validator.TryFindLot(definition, origin, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
        }

        public bool TryFindAnchoredLot(IA02BuildDefinition definition, Vector3 position, Quaternion rotation, int maxPhysicsChecks, out IA02BuildLot lot, out string reason)
        {
            lot = new IA02BuildLot
            {
                Position = position,
                Rotation = rotation,
                Footprint = definition != null ? definition.Footprint : Vector3.one,
                Key = "anchor:" + Mathf.RoundToInt(position.x / 2f) + ":" + Mathf.RoundToInt(position.z / 2f),
                State = IA02LotState.Free
            };
            if (validator.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason)) return true;
            lot = null;
            return false;
        }

        public bool TryValidatePreparedLot(IA02BuildDefinition definition, IA02BuildLot lot, int maxPhysicsChecks, out string reason)
        {
            return validator.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason);
        }

        public bool TryFindLotInBounds(IA02BuildDefinition definition, Bounds bounds, float now, int maxCandidates, int maxPhysicsChecks, out IA02BuildLot lot, out string reason)
        {
            return validator.TryFindLotInBounds(definition, bounds, now, maxCandidates, maxPhysicsChecks, out lot, out reason);
        }
    }

    public sealed class IA02BackendBridge
    {
        private readonly IA02RuntimeContext context;
        public IA02BackendBridge(IA02RuntimeContext context) { this.context = context; }

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

    internal static class IA02OperationalRules
    {
        public static bool IsCapitalThreatened(IA02WorldState world, MarcadorTerritorio capital, DadosPaisGoverno country)
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

    public sealed class IA02MissionDirector
    {
        private readonly IA02CommandQueue commands;
        public IA02MissionDirector(IA02CommandQueue commands) { this.commands = commands; }
        public void Queue(string id, Func<bool> execute, Action<bool> confirm) => commands.Enqueue(id, execute, confirm);
    }

    public sealed class IA02CommandQueue
    {
        private sealed class Entry
        {
            public string Id;
            public IA02CommandState State;
            public Func<bool> Execute;
            public Action<bool> Confirm;
        }
        private readonly Queue<Entry> entries = new Queue<Entry>();
        public int PendingCount => entries.Count;

        public void Enqueue(string id, Func<bool> execute, Action<bool> confirm)
        {
            entries.Enqueue(new Entry { Id = id, State = IA02CommandState.Queued, Execute = execute, Confirm = confirm });
        }

        public bool ProcessOne(float now, bool cancelConstructionCommands = false)
        {
            if (entries.Count == 0) return false;
            Entry entry = entries.Dequeue();
            if (cancelConstructionCommands && !string.IsNullOrWhiteSpace(entry.Id) && entry.Id.StartsWith("build:", StringComparison.OrdinalIgnoreCase))
            {
                entry.State = IA02CommandState.Cancelled;
                entry.Confirm?.Invoke(false);
                return true;
            }
            entry.State = IA02CommandState.Validating;
            bool succeeded = entry.Execute != null && entry.Execute();
            entry.State = succeeded ? IA02CommandState.Succeeded : IA02CommandState.Failed;
            entry.Confirm?.Invoke(succeeded);
            return true;
        }
    }

    public sealed class IA02BuildDirector
    {
        private const int PlatformRebuildCooldownDays = 5;
        private const int MilitaryRebuildCooldownDays = 3;
        private const int CivilianRebuildCooldownDays = 5;
        private const float CivilianMilitaryProtectionRadius = 180f;
        private const float PlatformNavalThreatRadius = 900f;
        private const float PlatformNavalAimRange = 1600f;
        private const float PlatformNavalAimDot = 0.62f;

        private sealed class ObservedStructure
        {
            public string Name;
            public Vector3 Position;
            public bool IsPlatform;
            public bool IsMilitary;
            public bool IsCivilian;
            public int MissingObservations;
            public bool CooldownApplied;
        }

        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly IA02ConstructionGovernor governor;
        private readonly IA02BuildCatalogAdapter catalog;
        private readonly IA02BuildReservationGrid reservations;
        private readonly IA02BuildFailureMemory failures;
        private readonly IA02CityPlanner city;
        private readonly IA02CommandQueue commands;
        private readonly IA02ZonePlanner zones;
        private readonly IA02LotPlanner lots;
        private readonly IA02BackendBridge backend;
        private readonly IA02BuildPlanRuntime buildPlan;
        private readonly IA02BuildExecutor executor;
        private readonly List<IdentidadeNaval> registeredNavalUnits = new List<IdentidadeNaval>(32);
        private readonly List<IdentidadeUnidade> registeredUnits = new List<IdentidadeUnidade>(128);
        private bool buildPending;
        private string lastAttemptKey = string.Empty;
        private string lastBlockedIntent = "Nenhuma";
        private string lastBlockReason = "Nenhum";
        private int lastFailureCount;
        private IA02FailureCode lastFailureCode = IA02FailureCode.None;
        private string lastFailureDetail = "n/d";
        private string nextUnblockCondition = "Nova tentativa permitida.";
        private string activeConstructionCommand = string.Empty;
        private IA02ConstructionState currentConstructionState = IA02ConstructionState.Idle;
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
        private float nextNonCapitalConstructionAt;
        private bool nonCapitalCadenceArmed;
        private string lastConstructionDiagnostic = string.Empty;
        private IA02BuildDefinition pendingDefinition;
        private IA02BuildLot pendingLot;
        private IA02Intent pendingIntent;
        private IA02IntentBoard pendingBoard;
        private IA02BuildPlanSelection pendingPlanSelection;
        private float lastPlanningMilliseconds;
        private readonly Dictionary<int, ObservedStructure> observedStructures = new Dictionary<int, ObservedStructure>(128);
        private readonly HashSet<int> currentStructureIds = new HashSet<int>();
        private int platformRebuildAllowedDay = -1;
        private int militaryRebuildAllowedDay = -1;
        private int civilianRebuildAllowedDay = -1;
        private Vector3 lastLostPlatformPosition;
        private float lastPlatformThreatCheckAt = -1f;
        private Vector3 cachedPlatformThreatPosition;
        private bool cachedPlatformThreatResult;

        public string Status { get; private set; } = "Aguardando intencao de construcao.";
        public string BlockedIntentStatus => lastBlockedIntent;
        public string BlockReasonStatus => lastBlockReason;
        public string FailureCountStatus => lastFailureCount.ToString();
        public string LastFailureCodeStatus => lastFailureCode.ToString();
        public string LastFailureDetailStatus => string.IsNullOrWhiteSpace(lastFailureDetail) ? "n/d" : lastFailureDetail;
        public string NextUnblockCondition => nextUnblockCondition;
        public string ActiveConstructionCommand => activeConstructionCommand;
        public IA02ConstructionState CurrentConstructionState => currentConstructionState;
        public bool HasPendingConstruction => buildPending;
        public int PendingCommandCount => commands != null ? commands.PendingCount : 0;
        public float LastPlanningMilliseconds => lastPlanningMilliseconds;
        public bool ImmediateRecoveryRequested { get; set; }
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

        public IA02BuildDirector(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02ConstructionGovernor governor, IA02BuildCatalogAdapter catalog, IA02BuildReservationGrid reservations, IA02BuildFailureMemory failures, IA02CommandQueue commands, IA02CityPlanner city, IA02ZonePlanner zones, IA02LotPlanner lots, IA02BackendBridge backend, IA02BuildPlanRuntime buildPlan)
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
            executor = new IA02BuildExecutor(controller, context, backend, city, world);
        }

        public bool Plan(float now, IA02IntentBoard board)
        {
            float startedAt = Time.realtimeSinceStartup;
            bool bypassNonCapitalCadence = ImmediateRecoveryRequested;
            ImmediateRecoveryRequested = false;
            try
            {
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
                UpdateDestroyedStructureCooldowns(GetCurrentGameDay());
                string timeoutStateToken = failures.BuildStateToken(
                    catalog != null ? catalog.CatalogVersion : 0,
                    world != null ? world.Version : -1,
                    IA02OperationalRules.IsCapitalThreatened(world, city.Capital, country),
                    country != null && country.emGuerra,
                    LegacyTreasuryValue(country),
                    country != null ? country.energia : 0,
                    country != null ? country.comida : 0);

                if (buildPending)
                {
                    currentConstructionState = IA02ConstructionState.WaitingConfirmation;
                    Status = string.IsNullOrWhiteSpace(pendingStructureId)
                        ? "Aguardando confirmacao da obra em andamento."
                        : "Aguardando confirmacao de " + pendingStructureId + " em " + currentLotId + ".";

                    if (now < confirmationReadyAt)
                    {
                        return false;
                    }

                    IA02Manager manager = controller != null ? controller.Manager : null;
                    bool matched = false;
                    if (manager != null)
                    {
                        IReadOnlyList<IA02WorldEntityRecord> teamRecords = manager.WorldRegistry.GetByTeam(context.TeamId);
                        for (int i = 0; i < teamRecords.Count; i++)
                        {
                            IA02WorldEntityRecord record = teamRecords[i];
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
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        Status = "Tempo limite na confirmacao de " + timedOutStructure + " no lote " + timedOutLot + ".";
                    }

                    return false;
                }

                if (governor != null && governor.ConstructionMode == IA02ConstructionMode.Frozen)
                {
                    currentConstructionState = IA02ConstructionState.Idle;
                    Status = "Construcao congelada: " + governor.ConstructionFreezeReason;
                    return false;
                }

                // Um intento de fundacao pode ficar no quadro apos um
                // carregamento ou apos a prefeitura ter sido registrada por
                // outro sistema. Nessa situacao ele nao pode monopolizar a
                // fila e gerar um falso "CatalogMissing".
                if (city != null && city.Capital != null)
                {
                    board.Complete(IA02IntentType.EstablishCapital);

                    // A prefeitura e a unica obra imediata da abertura. Assim que ela
                    // existe (inclusive se veio pronta da cena/save), arma uma fila
                    // simples para as demais estruturas. Sem este limite, cada slice
                    // tenta catalogo, lote e fisica para a proxima obra logo apos a
                    // confirmacao anterior, causando o gargalo da primeira partida.
                    if (!nonCapitalCadenceArmed)
                    {
                        nonCapitalCadenceArmed = true;
                        nextNonCapitalConstructionAt = now + GetNonCapitalConstructionInterval();
                    }

                    if (now < nextNonCapitalConstructionAt && !bypassNonCapitalCadence)
                    {
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        currentNeed = "Abertura em fila";
                        needScore = 0;
                        Status = "Aguardando proxima obra da abertura: " + (nextNonCapitalConstructionAt - now).ToString("0.0", CultureInfo.InvariantCulture) + " s.";
                        context.SetMetric("ia02.construction.opening_wait", 1d);
                        return false;
                    }
                }

                currentConstructionState = IA02ConstructionState.SelectingIntent;
                IA02Intent intent = board.GetBestApproved(candidate => IsIntentAllowed(candidate, now));
                if (intent == null || !IsBuildIntent(intent.Type))
                {
                    currentConstructionState = IA02ConstructionState.Idle;
                    currentNeed = "n/d";
                    needScore = 0;
                    return false;
                }

                currentNeed = string.IsNullOrWhiteSpace(intent.Reason) ? intent.Type.ToString() : intent.Reason;
                needScore = intent.Priority;

                if (intent.Type == IA02IntentType.BuildDefense
                    && governor != null
                    && governor.FixedDefenseCount >= governor.MaxFixedDefenses)
                {
                    currentConstructionState = IA02ConstructionState.Idle;
                    Status = "Defesa fixa no limite da fase (" + governor.FixedDefenseCount + "/" + governor.MaxFixedDefenses + "). Usando unidades e patrulha.";
                    board.Complete(intent.Type);
                    return false;
                }

                IA02BuildDefinition definition;
                string regionKey = BuildRegionKey(country);
                currentSector = regionKey;
                int failureWorldVersion = intent.Type == IA02IntentType.EstablishCapital ? -1 : world.Version;
                string stateToken = timeoutStateToken;
                string attemptKey = failures.BuildIntentKey(intent.Type, IA02StrategicRole.None, regionKey);
                lastAttemptKey = attemptKey;
                if (!failures.CanAttempt(attemptKey, now, stateToken))
                {
                    currentConstructionState = IA02ConstructionState.Cooldown;
                    Status = "Intento em cooldown: " + intent.Type + " na regiao " + regionKey + ".";
                    UpdateBlockStatus(intent.Type, now);
                    context.SetMetric("ia02.construction.cooldown", 1d);
                    return false;
                }

                currentConstructionState = IA02ConstructionState.SelectingCatalogItem;
                IA02BuildPlanSelection planSelection = null;
                bool planHandled = false;
                string planReason = string.Empty;
                bool foundationBudgetOverride = city != null
                    && city.Capital == null
                    && intent.Type == IA02IntentType.EstablishCapital
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
                    found = intent.Type == IA02IntentType.EstablishCapital
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
                    currentConstructionState = IA02ConstructionState.Cooldown;
                    Status = planHandled ? "Roteiro aguardando: " + planReason : catalog.LastDiagnostic;
                    context.SetMetric("ia02.construction.catalog_blocked", 1d);
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.NoValidCatalogItem, IA02IntentBlockReason.CatalogMissing);
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
                    currentConstructionState = IA02ConstructionState.Cooldown;
                    Status = "Saldo insuficiente para " + definition.DisplayName + ".";
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.InsufficientFunds, IA02IntentBlockReason.Funds, Status);
                    return false;
                }

                currentConstructionState = IA02ConstructionState.SearchingLot;
                Vector3 origin;
                if (!controller.TryResolveConstructionAnchor(intent.Type, out origin))
                {
                    string originReason;
                    if (!zones.TryResolvePlanningOrigin(city, definition, out origin, out originReason))
                    {
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        Status = "WorldNotReady para " + definition.DisplayName + ": " + originReason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.NoValidLot, IA02IntentBlockReason.NoLot, originReason);
                        return false;
                    }
                }
                int maxCandidates = governor != null ? governor.MaxCandidatesPerSlice : 4;
                int maxPhysicsChecks = governor != null ? governor.MaxPhysicsChecksPerSlice : 16;
                IA02BuildLot lot;
                string reason;
                Vector3 anchorPosition = Vector3.zero; // default for residential lots
                Quaternion anchorRotation = Quaternion.identity; // default for residential lots
                // Estruturas especiais usam o create fixo. Residencias sao excecao:
                // o create apenas indica a regiao de referencia; cada casa/predio
                // precisa de um lote novo junto da rua para formar um bairro.
                bool residentialIntent = definition.StrategicRole == IA02StrategicRole.Residential;
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
                        if (!RequiresOwnCreate(intent.Type) && intent.Type == IA02IntentType.BuildVehicleConstructor
                            && lots.TryFindLot(definition, anchorPosition, now, maxCandidates, maxPhysicsChecks, out lot, out motivoFallback))
                        {
                            reason = "âncora ocupada; lote local alternativo aprovado: " + lot.Key;
                            Status = "Construtor de veículos deslocado para lote local da âncora.";
                        }
                        else
                        {
                            if (intent.Type == IA02IntentType.BuildVehicleConstructor && !string.IsNullOrWhiteSpace(motivoFallback))
                                reason = reason + " | fallback local: " + motivoFallback;
                            ReportConstructionDiagnostic(intent.Type, definition, reason);
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        Status = "Create fixo invalido para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.NoValidLot, IA02IntentBlockReason.NoLot, reason);
                        return false;
                        }
                    }
                }
                else if (planSelection != null && planSelection.UsesPreparedSlot)
                {
                    lot = planSelection.Lot;
                    if (!lots.TryValidatePreparedLot(definition, lot, maxPhysicsChecks, out reason))
                    {
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        Status = "Slot preparado adiado para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.NoValidLot, IA02IntentBlockReason.NoLot, "Slot preparado invalido: " + reason);
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
                        currentConstructionState = IA02ConstructionState.Cooldown;
                        reason = string.IsNullOrWhiteSpace(reason) ? "nenhum lote dentro da zona autonoma" : reason;
                        Status = "Lote adiado para " + definition.DisplayName + ": " + reason;
                        RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.NoValidLot, IA02IntentBlockReason.NoLot, "Nenhum lote valido: " + reason);
                        if (planSelection != null) buildPlan.Confirm(planSelection, string.Empty, false, reason, now);
                        return false;
                    }
                }

                if (!reservations.TryReserve(lot))
                {
                    currentConstructionState = IA02ConstructionState.Cooldown;
                    Status = "Lote ja reservado para " + definition.DisplayName + ".";
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.LotBlocked, IA02IntentBlockReason.LotBlocked, "Reserva ocupada para " + definition.DisplayName + ".");
                    return false;
                }

                currentConstructionState = IA02ConstructionState.Reserved;
                activeConstructionCommand = restoredPlanCommand ? buildPlan.PendingCommandId : "build:" + definition.ItemId + ":" + lot.Key;
                if (planSelection != null && !restoredPlanCommand && !buildPlan.TryReserve(planSelection, activeConstructionCommand, now, out reason))
                {
                    reservations.Release(lot, false);
                    buildPending = false;
                    activeConstructionCommand = string.Empty;
                    currentConstructionState = IA02ConstructionState.Cooldown;
                    Status = "Reserva do slot falhou: " + reason;
                    RecordFailure(intent.Type, attemptKey, now, stateToken, IA02FailureCode.LotReserved, IA02IntentBlockReason.LotBlocked, "Falha ao reservar slot preparado: " + reason);
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
                // A fundacao e a dependencia que destrava toda a economia. Ela
                // nao deve perder uma fatia de scheduler (ou expirar durante um
                // frame de carregamento pesado) antes de entrar no executor. A
                // confirmacao do registro do mundo continua acontecendo na
                // fatia seguinte; somente o comando de fundacao fica elegivel
                // imediatamente. As demais obras preservam a pequena janela de
                // confirmacao usada para desacoplar planejamento e execucao.
                confirmationReadyAt = city != null && city.Capital == null
                    ? now
                    : now + 0.05f;
                confirmationDeadline = now + 8f;
                Status = definition.UsedCatalogFallback
                    ? "Obra aprovada com fallback: " + definition.DisplayName + ". " + definition.CatalogResolution
                    : "Obra aprovada: " + definition.DisplayName + ".";
                context.SetMetric("ia02.construction.catalog_fallback", definition.UsedCatalogFallback ? 1d : 0d);
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
                currentConstructionState = IA02ConstructionState.WaitingConfirmation;
                return true;
            }
            finally
            {
                lastPlanningMilliseconds = (Time.realtimeSinceStartup - startedAt) * 1000f;
            }
        }

        private static bool IsFixedConstructionIntent(IA02IntentType type)
        {
            return type == IA02IntentType.BuildEnergy
                || type == IA02IntentType.BuildFoodProduction
                || type == IA02IntentType.BuildStorage
                || type == IA02IntentType.BuildMilitaryTent
                || type == IA02IntentType.BuildVehicleConstructor
                || type == IA02IntentType.BuildMilitaryAirport
                || type == IA02IntentType.BuildCommercialAirport
                || type == IA02IntentType.BuildShipyard
                || type == IA02IntentType.BuildPier
                || type == IA02IntentType.BuildOffshorePlatform;
        }

        private static bool RequiresOwnCreate(IA02IntentType type)
        {
            return type == IA02IntentType.BuildEnergy
                || type == IA02IntentType.BuildFoodProduction
                || type == IA02IntentType.BuildStorage
                || type == IA02IntentType.BuildVehicleConstructor
                || type == IA02IntentType.BuildMilitaryAirport
                || type == IA02IntentType.BuildCommercialAirport
                || type == IA02IntentType.BuildShipyard
                || type == IA02IntentType.BuildPier
                || type == IA02IntentType.BuildOffshorePlatform
                || type == IA02IntentType.BuildMilitaryTent;
        }

        public bool AllowsIntent(IA02Intent intent, float now)
        {
            return IsIntentAllowed(intent, now);
        }

        /// <summary>
        /// O diretor militar possui uma abertura roteirizada que pode criar
        /// pier/plataforma diretamente. Ele consulta o mesmo cooldown do
        /// diretor de construção para não contornar a regra de reconstrução.
        /// </summary>
        public bool IsRebuildBlocked(IA02IntentType intentType, float now)
        {
            UpdateDestroyedStructureCooldowns(GetCurrentGameDay());
            return IsRebuildCooldownActive(intentType, now);
        }

        public bool HasRecordedRebuild(IA02IntentType intentType)
        {
            return intentType == IA02IntentType.BuildOffshorePlatform
                ? platformRebuildAllowedDay >= 0
                : IsMilitaryRebuildIntent(intentType)
                    ? militaryRebuildAllowedDay >= 0
                    : IsCivilianRebuildIntent(intentType) && civilianRebuildAllowedDay >= 0;
        }

        public string GetCooldownStatus(float now)
        {
            if (!failures.TryGetCooldown(lastAttemptKey, now, out _, out _, out float remaining, out bool requiresStateChange))
            {
                return "0 s";
            }

            return requiresStateChange ? "aguardando mudanca de estado" : remaining.ToString("0.0") + " s";
        }

        private void RecordFailure(IA02IntentType intent, string key, float now, string stateToken, IA02FailureCode failureCode, IA02IntentBlockReason blockReason, string detail = null)
        {
            failures.Record(key, now, stateToken, failureCode, blockReason);
            lastAttemptKey = key;
            currentConstructionState = IA02ConstructionState.Cooldown;
            lastFailureCode = failureCode;
            lastFailureDetail = string.IsNullOrWhiteSpace(detail) ? blockReason.ToString() : detail;
            UpdateBlockStatus(intent, now);
        }

        private void UpdateBlockStatus(IA02IntentType intent, float now)
        {
            lastBlockedIntent = intent.ToString();
            if (!failures.TryGetCooldown(lastAttemptKey, now, out int failureCount, out IA02IntentBlockReason reason, out _, out bool requiresStateChange))
            {
                lastBlockReason = "Nenhum";
                lastFailureCount = 0;
                lastFailureCode = IA02FailureCode.None;
                lastFailureDetail = "n/d";
                nextUnblockCondition = "Nova tentativa permitida.";
                return;
            }

            lastFailureCount = failureCount;
            lastBlockReason = reason.ToString();
            nextUnblockCondition = requiresStateChange
                ? reason == IA02IntentBlockReason.Funds
                    ? "Funding, tesouraria ou custos precisam mudar."
                    : "Catalogo, mundo, ameaca ou recursos devem mudar."
                : "Aguardar o cooldown terminar.";
        }

        private bool IsIntentAllowed(IA02Intent intent, float now)
        {
            if (intent == null || !intent.Approved || !IsBuildIntent(intent.Type))
            {
                return false;
            }

            if (intent.Type == IA02IntentType.EstablishCapital && city != null && city.Capital != null)
            {
                return false;
            }

            if (governor != null && governor.ConstructionMode == IA02ConstructionMode.Frozen)
            {
                return false;
            }

            if (IsRebuildCooldownActive(intent, now))
            {
                return false;
            }

            DadosPaisGoverno country = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(context.TeamId) : null;
            string regionKey = BuildRegionKey(country);
            int failureWorldVersion = intent.Type == IA02IntentType.EstablishCapital ? -1 : world.Version;
            string token = failures.BuildStateToken(catalog.CatalogVersion, failureWorldVersion, IA02OperationalRules.IsCapitalThreatened(world, city.Capital, country), country != null && country.emGuerra, LegacyTreasuryValue(country), country != null ? country.energia : 0, country != null ? country.comida : 0);
            string key = failures.BuildIntentKey(intent.Type, IA02StrategicRole.None, regionKey);
            return failures.CanAttempt(key, now, token);
        }

        private bool IsRebuildCooldownActive(IA02Intent intent, float now)
        {
            return intent != null && IsRebuildCooldownActive(intent.Type, now);
        }

        private bool IsRebuildCooldownActive(IA02IntentType intentType, float now)
        {
            int currentDay = GetCurrentGameDay();
            if (intentType == IA02IntentType.BuildOffshorePlatform)
            {
                if (platformRebuildAllowedDay > currentDay)
                {
                    nextUnblockCondition = "Reconstrução da plataforma liberada no dia " + platformRebuildAllowedDay + ".";
                    return true;
                }

                if (lastLostPlatformPosition != Vector3.zero && HasEnemyNavalThreatNear(lastLostPlatformPosition, now))
                {
                    nextUnblockCondition = "Plataforma aguardando afastamento do navio hostil que a destruiu.";
                    return true;
                }

                return false;
            }

            if (IsMilitaryRebuildIntent(intentType) && militaryRebuildAllowedDay > currentDay)
            {
                nextUnblockCondition = "Reconstrução militar liberada no dia " + militaryRebuildAllowedDay + ".";
                return true;
            }

            if (IsCivilianRebuildIntent(intentType) && civilianRebuildAllowedDay > currentDay)
            {
                nextUnblockCondition = "Reconstrução civil liberada no dia " + civilianRebuildAllowedDay + ".";
                return true;
            }

            return false;
        }

        private static bool IsMilitaryRebuildIntent(IA02IntentType intentType)
        {
            return intentType == IA02IntentType.BuildMilitaryTent
                || intentType == IA02IntentType.BuildVehicleConstructor
                || intentType == IA02IntentType.BuildMilitaryAirport
                || intentType == IA02IntentType.BuildDefense
                || intentType == IA02IntentType.BuildShipyard
                || intentType == IA02IntentType.BuildPier;
        }

        private static bool IsCivilianRebuildIntent(IA02IntentType intentType)
        {
            return intentType == IA02IntentType.BuildResidentialCapacity
                || intentType == IA02IntentType.BuildStarterHouse
                || intentType == IA02IntentType.BuildMediumApartment
                || intentType == IA02IntentType.BuildHighApartment;
        }

        private int GetCurrentGameDay()
        {
            if (GerenciadorTempo.Instancia != null)
            {
                return Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
            }

            // Fallback somente para cenas de teste que não instanciam o relógio.
            return Mathf.Max(1, Mathf.FloorToInt(Time.time / 30f) + 1);
        }

        private void UpdateDestroyedStructureCooldowns(int currentDay)
        {
            if (world == null || world.OwnedStructures == null)
            {
                return;
            }

            currentStructureIds.Clear();
            for (int i = 0; i < world.OwnedStructures.Count; i++)
            {
                IdentidadeUnidade identity = world.OwnedStructures[i];
                GameObject structure = identity != null ? identity.gameObject : null;
                if (structure == null)
                {
                    continue;
                }

                int instanceId = structure.GetInstanceID();
                currentStructureIds.Add(instanceId);
                ObservedStructure observed;
                if (!observedStructures.TryGetValue(instanceId, out observed))
                {
                    observed = DescribeStructure(structure);
                    observedStructures.Add(instanceId, observed);
                }
                else
                {
                    observed.Position = structure.transform.position;
                    observed.MissingObservations = 0;
                    observed.CooldownApplied = false;
                }
            }

            foreach (KeyValuePair<int, ObservedStructure> pair in observedStructures)
            {
                if (currentStructureIds.Contains(pair.Key))
                {
                    continue;
                }

                ObservedStructure observed = pair.Value;
                if (observed == null || observed.CooldownApplied)
                {
                    continue;
                }

                observed.MissingObservations++;
                if (observed.MissingObservations < 2)
                {
                    continue;
                }

                observed.CooldownApplied = true;
                if (observed.IsPlatform)
                {
                    platformRebuildAllowedDay = Mathf.Max(platformRebuildAllowedDay, currentDay + PlatformRebuildCooldownDays);
                    lastLostPlatformPosition = observed.Position;
                }
                else if (observed.IsMilitary)
                {
                    militaryRebuildAllowedDay = Mathf.Max(militaryRebuildAllowedDay, currentDay + MilitaryRebuildCooldownDays);
                }
                else if (observed.IsCivilian)
                {
                    int delay = HasFriendlyMilitaryNear(observed.Position)
                        ? MilitaryRebuildCooldownDays
                        : CivilianRebuildCooldownDays;
                    civilianRebuildAllowedDay = Mathf.Max(civilianRebuildAllowedDay, currentDay + delay);
                }
            }
        }

        private ObservedStructure DescribeStructure(GameObject structure)
        {
            IA_ConstructionMetadata metadata = structure.GetComponent<IA_ConstructionMetadata>();
            if (metadata == null)
            {
                metadata = structure.GetComponentInChildren<IA_ConstructionMetadata>(true);
            }

            string text = IA_Text.Normalize(structure.name);
            if (metadata != null)
            {
                text += " " + IA_Text.Normalize(metadata.DisplayName + " " + metadata.Aliases + " " + metadata.SourcePrefabName);
            }

            bool hasPlatformComponent = structure.GetComponent<PlataformaOffshore>() != null
                || structure.GetComponentInChildren<PlataformaOffshore>(true) != null;
            bool hasCivilianComponent = structure.GetComponent<Imovel>() != null
                || structure.GetComponentInChildren<Imovel>(true) != null;
            bool hasMilitaryComponent = structure.GetComponent<Estaleiro>() != null
                || structure.GetComponentInChildren<Estaleiro>(true) != null
                || structure.GetComponent<PierMarinha>() != null
                || structure.GetComponentInChildren<PierMarinha>(true) != null
                || structure.GetComponent<GerenciadorAeroporto>() != null
                || structure.GetComponentInChildren<GerenciadorAeroporto>(true) != null;

            bool isPlatform = hasPlatformComponent
                || (metadata != null && metadata.IsPlatform)
                || text.Contains("plataforma")
                || text.Contains("offshore");
            bool isCivilian = hasCivilianComponent
                || (metadata != null && metadata.IsCivil)
                || text.Contains("imovel")
                || text.Contains("casa")
                || text.Contains("moradia")
                || text.Contains("residencia")
                || text.Contains("habitacao")
                || text.Contains("apartamento")
                || text.Contains("village")
                || text.Contains("predio");
            bool isMilitary = !isPlatform && (hasMilitaryComponent
                || (metadata != null && (metadata.IsMilitary || metadata.IsDefense || metadata.IsMilitaryAirport || metadata.IsShipyard || metadata.IsPier))
                || text.Contains("quartel")
                || text.Contains("tenda")
                || text.Contains("barraca")
                || text.Contains("construtor")
                || text.Contains("fabrica")
                || text.Contains("militar")
                || text.Contains("radar")
                || text.Contains("torreta")
                || text.Contains("defesa")
                || text.Contains("estaleiro")
                || text.Contains("pier")
                || text.Contains("lancador")
                || text.Contains("silo"));

            return new ObservedStructure
            {
                Name = structure.name,
                Position = structure.transform.position,
                IsPlatform = isPlatform,
                IsMilitary = isMilitary,
                IsCivilian = !isPlatform && !isMilitary && isCivilian
            };
        }

        private bool HasFriendlyMilitaryNear(Vector3 position)
        {
            if (world == null || world.OwnedStructures == null || position == Vector3.zero)
            {
                return false;
            }

            float radiusSqr = CivilianMilitaryProtectionRadius * CivilianMilitaryProtectionRadius;
            registeredUnits.Clear();
            RegistroEntidadesJogo.FillUnidades(registeredUnits);
            for (int i = 0; i < registeredUnits.Count; i++)
            {
                IdentidadeUnidade unit = registeredUnits[i];
                if (unit != null
                    && unit.teamID == context.TeamId
                    && unit.tipoUnidade != TipoUnidade.Estrutura
                    && unit.gameObject.activeInHierarchy
                    && (unit.transform.position - position).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasEnemyNavalThreatNear(Vector3 position, float now)
        {
            if ((position - cachedPlatformThreatPosition).sqrMagnitude <= 0.01f
                && Mathf.Abs(now - lastPlatformThreatCheckAt) <= 0.001f)
            {
                return cachedPlatformThreatResult;
            }

            cachedPlatformThreatPosition = position;
            lastPlatformThreatCheckAt = now;
            cachedPlatformThreatResult = false;

            if (world != null && world.EnemyUnits != null)
            {
                float radiusSqr = PlatformNavalThreatRadius * PlatformNavalThreatRadius;
                for (int i = 0; i < world.EnemyUnits.Count; i++)
                {
                    IdentidadeUnidade enemy = world.EnemyUnits[i];
                    if (enemy == null
                        || enemy.tipoUnidade != TipoUnidade.Naval
                        || !enemy.gameObject.activeInHierarchy
                        || enemy.GetComponentInParent<NavioPetroleiro>() != null
                        || enemy.GetComponentInChildren<NavioPetroleiro>(true) != null)
                    {
                        continue;
                    }

                    if ((enemy.transform.position - position).sqrMagnitude <= radiusSqr)
                    {
                        cachedPlatformThreatResult = true;
                        return cachedPlatformThreatResult;
                    }
                }
            }

            // O mundo visível cobre a maior parte dos casos. O registro naval
            // cobre navios fora do cone de visão da IA, que ainda podem estar
            // guardando ou apontando para o local recém-destruído.
            registeredNavalUnits.Clear();
            RegistroEntidadesJogo.FillNavios(registeredNavalUnits);
            for (int i = 0; i < registeredNavalUnits.Count; i++)
            {
                IdentidadeNaval navio = registeredNavalUnits[i];
                if (navio == null || !IsEnemyNavalUnit(navio.gameObject))
                {
                    continue;
                }

                if (IsNavalThreateningPosition(navio.transform, position))
                {
                    cachedPlatformThreatResult = true;
                    return cachedPlatformThreatResult;
                }
            }

            ControleNavioRealista[] navios = UnityEngine.Object.FindObjectsByType<ControleNavioRealista>(FindObjectsSortMode.None);
            for (int i = 0; i < navios.Length; i++)
            {
                ControleNavioRealista navio = navios[i];
                if (navio == null || !IsEnemyNavalUnit(navio.gameObject))
                {
                    continue;
                }

                if (IsNavalThreateningPosition(navio.transform, position))
                {
                    cachedPlatformThreatResult = true;
                    return cachedPlatformThreatResult;
                }
            }

            ControleSubmarino[] submarinos = UnityEngine.Object.FindObjectsByType<ControleSubmarino>(FindObjectsSortMode.None);
            for (int i = 0; i < submarinos.Length; i++)
            {
                ControleSubmarino submarino = submarinos[i];
                if (submarino == null || !IsEnemyNavalUnit(submarino.gameObject))
                {
                    continue;
                }

                if (IsNavalThreateningPosition(submarino.transform, position))
                {
                    cachedPlatformThreatResult = true;
                    return cachedPlatformThreatResult;
                }
            }

            return cachedPlatformThreatResult;
        }

        private bool IsEnemyNavalUnit(GameObject unit)
        {
            IdentidadeUnidade identity = SistemaDeDanos.ResolverIdentidade(unit.transform);
            return identity != null && identity.teamID > 0 && identity.teamID != context.TeamId;
        }

        private static bool IsNavalThreateningPosition(Transform navalUnit, Vector3 position)
        {
            Vector3 delta = position - navalUnit.position;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr <= PlatformNavalThreatRadius * PlatformNavalThreatRadius)
            {
                return true;
            }

            if (distanceSqr > PlatformNavalAimRange * PlatformNavalAimRange || distanceSqr <= 0.01f)
            {
                return false;
            }

            delta.Normalize();
            Vector3 forward = navalUnit.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            return Vector3.Dot(forward.normalized, delta) >= PlatformNavalAimDot;
        }

        private static string BuildRegionKey(DadosPaisGoverno country)
        {
            return country != null ? "capital:" + country.teamId : "capital:unknown";
        }

        private static int LegacyTreasuryValue(DadosPaisGoverno country)
        {
            if (country == null) return 0;
            return (int)Math.Min(int.MaxValue, Math.Max(int.MinValue, country.saldo));
        }

        private static bool IsBuildIntent(IA02IntentType type)
        {
            return type == IA02IntentType.EstablishCapital
                || type == IA02IntentType.BuildEnergy
                || type == IA02IntentType.BuildFoodProduction
                || type == IA02IntentType.BuildResidentialCapacity
                || type == IA02IntentType.BuildStarterHouse
                || type == IA02IntentType.BuildMediumApartment
                || type == IA02IntentType.BuildHighApartment
                || type == IA02IntentType.BuildMilitaryTent
                || type == IA02IntentType.BuildVehicleConstructor
                || type == IA02IntentType.BuildStorage
                || type == IA02IntentType.BuildLogistics
                || type == IA02IntentType.BuildRoad
                || type == IA02IntentType.BuildMilitaryAirport
                || type == IA02IntentType.BuildCommercialAirport
                || type == IA02IntentType.BuildShipyard
                || type == IA02IntentType.BuildPier
                || type == IA02IntentType.BuildOffshorePlatform
                || type == IA02IntentType.BuildIndustry
                || type == IA02IntentType.BuildDefense;
        }

        private bool ExecuteBuild(IA02BuildDefinition definition, IA02BuildLot lot, DadosPaisGoverno country, bool foundationBudgetOverride)
        {
            currentConstructionState = IA02ConstructionState.Executing;
            return executor != null && executor.TryExecute(definition, lot, activeConstructionCommand, pendingPrefabId, foundationBudgetOverride, out _);
        }

        private void ReportConstructionDiagnostic(IA02IntentType intent, IA02BuildDefinition definition, string reason)
        {
            string message = intent + " / " + (definition != null ? definition.DisplayName : "sem definicao")
                + ": " + (string.IsNullOrWhiteSpace(reason) ? "motivo desconhecido" : reason);
            if (string.Equals(lastConstructionDiagnostic, message, StringComparison.Ordinal)) return;
            lastConstructionDiagnostic = message;
            Debug.LogWarning("[IA02 Build] Lote recusado: " + message);
        }

        private void ConfirmBuild(bool success, IA02BuildDefinition definition, IA02BuildLot lot, IA02Intent intent, IA02IntentBoard board, float now)
        {
            bool timedOut = confirmationTimeoutArmed;
            string completedCommandId = activeConstructionCommand;
            IA02BuildPlanSelection completedPlanSelection = pendingPlanSelection;
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
            currentConstructionState = IA02ConstructionState.Cooldown;
            if (!success)
            {
                reservations.Release(lot, true);
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
                string failureStateToken = failures.BuildStateToken(
                    catalog != null ? catalog.CatalogVersion : 0,
                    world != null ? world.Version : -1,
                    IA02OperationalRules.IsCapitalThreatened(world, city.Capital, country),
                    country != null && country.emGuerra,
                    LegacyTreasuryValue(country),
                    country != null ? country.energia : 0,
                    country != null ? country.comida : 0);
                RecordFailure(
                    intent.Type,
                    lastAttemptKey,
                    now,
                    failureStateToken,
                    timedOut ? IA02FailureCode.Busy : IA02FailureCode.ExecutionFailed,
                    timedOut ? IA02IntentBlockReason.Cooldown : IA02IntentBlockReason.Busy);
                buildPlan?.Confirm(completedPlanSelection, completedCommandId, false, timedOut ? "tempo limite de confirmacao" : "execucao nao confirmada", now);
                if (timedOut)
                {
                    Status = "Tempo limite confirmado para " + definition.DisplayName + ".";
                }
                else
                {
                    Status = governor != null && governor.ConstructionMode == IA02ConstructionMode.Frozen
                        ? "Obra cancelada pelo governador: " + definition.DisplayName + "."
                        : "Falha confirmada: " + definition.DisplayName + ".";
                }
                return;
            }

            reservations.MarkOccupied(lot);
            buildPlan?.Confirm(completedPlanSelection, completedCommandId, true, string.Empty, now);
            board.Complete(intent.Type);
            context.SetMetric("ia02.construction.last_cost", definition.Cost);
            context.TryGetMetric("ia02.construction.completed", out double completed);
            context.SetMetric("ia02.construction.completed", completed + 1d);
            context.MarkDirty(IA02DirtyReason.ExternalEvent);
            lastConstructionCompletedAt = now;
            if (intent.Type != IA02IntentType.EstablishCapital)
            {
                nonCapitalCadenceArmed = true;
                nextNonCapitalConstructionAt = now + GetNonCapitalConstructionInterval();
            }
            Status = definition.UsedCatalogFallback
                ? "Construido com fallback e confirmado: " + definition.DisplayName + "."
                : "Construido e confirmado: " + definition.DisplayName + ".";
        }

        private float GetNonCapitalConstructionInterval()
        {
            return controller != null ? controller.NonCapitalConstructionIntervalSeconds : 5f;
        }
    }

    public sealed class IA02BuildValidator
    {
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly IA02BuildReservationGrid reservations;
        private readonly IA02BuildFailureMemory failures;
        private int cursor;
        private int candidatesEvaluated;
        private int physicsChecks;

        public int CandidatesEvaluated => candidatesEvaluated;
        public int PhysicsChecks => physicsChecks;

        public IA02BuildValidator(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02BuildReservationGrid reservations, IA02BuildFailureMemory failures)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.reservations = reservations;
            this.failures = failures;
        }

        public bool TryFindLot(IA02BuildDefinition definition, Vector3 origin, float now, int maxCandidates, int maxPhysicsChecks, out IA02BuildLot lot, out string reason)
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

                lot = new IA02BuildLot { Position = position, Rotation = rotation, Footprint = definition.Footprint, Key = key, State = IA02LotState.Free };
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

        public bool TryValidatePreparedLot(IA02BuildDefinition definition, IA02BuildLot lot, int maxPhysicsChecks, out string reason)
        {
            if (definition == null || lot == null)
            {
                reason = "lote preparado ou definicao ausente";
                return false;
            }

            int physicsSpent = 0;
            if (definition.Domain == IA02BuildDomain.Water)
            {
                if (!NavalPlacementResolver.IsWaterAtPosition(lot.Position))
                {
                    reason = "slot naval nao esta na agua navegavel";
                    return false;
                }
            }
            else if (definition.Domain == IA02BuildDomain.Coastal)
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

        private static bool IsOpeningPreparedDefinition(IA02BuildDefinition definition)
        {
            return definition != null
                && (definition.StrategicRole == IA02StrategicRole.Shipyard
                    || definition.StrategicRole == IA02StrategicRole.Port
                    || definition.StrategicRole == IA02StrategicRole.Pier
                    || (definition.MinimumStage == IA02NationStage.Initialization
                        && definition.MaximumRecommendedCount == 1
                        && !string.IsNullOrWhiteSpace(definition.CatalogResolution)
                        && definition.CatalogResolution.ToLowerInvariant().Contains("abertura")));
        }

        public bool TryFindLotInBounds(IA02BuildDefinition definition, Bounds bounds, float now, int maxCandidates, int maxPhysicsChecks, out IA02BuildLot lot, out string reason)
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
                lot = new IA02BuildLot { Position = position, Rotation = rotation, Footprint = definition.Footprint, Key = "zone:" + key, State = IA02LotState.Free };
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

        private bool TryResolveDomain(IA02BuildDefinition definition, Vector3 candidate, ref int physicsSpent, int maxPhysicsChecks, out Vector3 position, out Quaternion rotation, out string reason)
        {
            position = candidate;
            rotation = Quaternion.identity;
            reason = string.Empty;
            if (definition.Domain == IA02BuildDomain.Water || definition.Domain == IA02BuildDomain.Coastal)
            {
                Vector3 waterPoint;
                float seaLevel;
                if (!NavalPlacementResolver.TryResolveWaterSpawn(candidate, Vector3.forward, 25f, 220f, out waterPoint, out seaLevel, out reason))
                {
                    return false;
                }

                if (definition.Domain == IA02BuildDomain.Coastal)
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

        private bool HasClearFootprint(IA02BuildLot lot, IA02BuildDefinition definition, ref int physicsSpent, int maxPhysicsChecks, out string reason)
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

        private static void TryArrangeResidentialLot(IA02BuildDefinition definition, ref Vector3 position, ref Quaternion rotation, int slot)
        {
            if (definition == null || definition.StrategicRole != IA02StrategicRole.Residential) return;
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
            IA02Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null) return true;
            IReadOnlyList<IA02WorldEntityRecord> structures = manager.WorldRegistry.GetByKind(IA02WorldEntityKind.Structure);
            if (structures == null) return true;
            Vector2 flatPosition = new Vector2(position.x, position.z);
            float minSqr = minimumDistance * minimumDistance;
            for (int i = 0; i < structures.Count; i++)
            {
                IA02WorldEntityRecord structure = structures[i];
                if (structure == null) continue;
                Vector2 other = new Vector2(structure.Position.x, structure.Position.z);
        if ((other - flatPosition).sqrMagnitude <= minSqr)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAntiAirDefinition(IA02BuildDefinition definition)
        {
            if (definition == null) return false;
            if (definition.StrategicRole == IA02StrategicRole.AntiAirDefense) return true;
            return definition.Item != null
                && IsAntiAirItem(definition.Item, definition.Item.GetResolvedCapabilities());
        }

        private static bool IsAntiAirItem(DadosConstrucao item, IA_ConstructionCapability capabilities)
        {
            if (item == null) return false;
            if ((IA02StrategicRole)(int)item.StrategicRole == IA02StrategicRole.AntiAirDefense) return true;
            string text = IA_Text.Normalize((item.GetStableId() ?? string.Empty) + " "
                + (item.GetDisplayName() ?? string.Empty) + " "
                + (item.NomeItem ?? string.Empty) + " "
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

    public sealed class IA02WarDirector
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
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly IA02MissionDirector missions;
        private readonly IA02CityPlanner city;
        private readonly NavMeshPath route = new NavMeshPath();
        private float nextCheckAt;
        private float nextOrderAt;
        private float nextPatrolAt;

        public IA02Campaign Campaign { get; } = new IA02Campaign();
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

        public IA02WarDirector(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02CityPlanner city, IA02MissionDirector missions)
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
                Debug.Log("[IA02 Combat] Time " + context.TeamId + " identificou o agressor " + attackerTeamId + " como inimigo.");
            }

            contact.Position = attackerPosition;
            contact.LastSeenAt = now;
            contact.Damage += Mathf.Max(0f, damage);
            Status = "Agressor identificado: time " + attackerTeamId + "; preparando retaliacao.";
        }

        public bool Plan(float now, IA02IntentBoard board, bool emergencyReserve)
        {
            if (now < nextCheckAt) return false;
            nextCheckAt = now + 1f;
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            bool underAttack = IA02OperationalRules.IsCapitalThreatened(world, city.Capital, country);
            if (underAttack)
            {
                board.Publish(IA02IntentType.DefendCapital, 1200, "Prefeitura sob ameaca", now);
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
                board.Publish(IA02IntentType.DefendCapital, 1100, "Reserva de guerra critica", now);
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

            board.Publish(IA02IntentType.CampaignAgainstCapital, 950, "Neutralizar prefeitura inimiga", now);
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
                    ? brain.TryIssueAttack(context.TeamId, "ia02_campaign_capital", target.transform.position, 1000)
                    : brain.TryIssueMovePackage(context.TeamId, "ia02_campaign_corridor", objective, 950),
                success => Status = success ? (finalAttack ? "Campanha atacando prefeitura inimiga." : "Campanha abrindo corredor ate a prefeitura.") : "Ordem de campanha recusada; aguardando replano.");
            return true;
        }

        private bool QueueRetaliation(float now, IA02IntentBoard board, bool emergencyReserve)
        {
            HostileContact contact = ResolveLatestContact(now);
            if (contact == null) return false;

            Vector3 target = ResolveNearbyAggressor(contact);
            board.Publish(IA02IntentType.DefendCapital, emergencyReserve ? 1200 : 1150,
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
                () => brain.TryIssueAttack(context.TeamId, "ia02_retaliacao_" + attackerTeamId, target, 1150),
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
                () => brain.TryIssueMovePackage(context.TeamId, "ia02_defend_capital", city.Capital.transform.position, 1000),
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
                () => brain.TryIssueMovePackage(context.TeamId, "ia02_peace_recon", destination, 650),
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
            IA02WarAdvanceZone[] warZones = UnityEngine.Object.FindObjectsByType<IA02WarAdvanceZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Vector3 configured = target;
            float configuredDistance = float.MaxValue;
            for (int z = 0; z < warZones.Length; z++)
            {
                IA02WarAdvanceZone zone = warZones[z];
                if (zone == null || zone.TeamId != context.TeamId || zone.Tipo == IA02WarAdvanceZone.Dominio.Aereo) continue;
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
