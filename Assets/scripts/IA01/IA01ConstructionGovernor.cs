using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01ConstructionGovernor
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01NationProfile profile;
        private readonly StringBuilder roleSummary = new StringBuilder(256);
        private float nextDiagnosticAt;
        private int lastWorldVersion = -1;
        private int lastCatalogVersion = -1;
        private IA01ConstructionMode mode = IA01ConstructionMode.Active;
        private string freezeReason = "Nenhum";
        private string nextUnfreezeCondition = "Nenhuma";
        private string currentSector = "capital";
        private string buildingsByRole = "n/d";
        private string housingNeed = "n/d";
        private string foodCoverage = "n/d";
        private string energyCoverage = "n/d";
        private string storageOccupancy = "n/d";
        private string emergencyReserve = "n/d";
        private string availableConstructionFunds = "n/d";
        private string cityCoveragePercent = "n/d";
        private int lastStructureCount;
        private long availableConstructionFundsAmount;
        private int consecutivePerformanceWarnings;

        public IA01ConstructionGovernor(IA01Controller controller, IA01RuntimeContext context, IA01NationProfile profile)
        {
            this.controller = controller;
            this.context = context;
            this.profile = profile;
        }

        public IA01ConstructionMode ConstructionMode => mode;
        public string ConstructionFreezeReason => freezeReason;
        public string NextUnfreezeCondition => nextUnfreezeCondition;
        public string CurrentConstructionState { get; private set; } = IA01ConstructionState.Idle.ToString();
        public string ActiveConstructionCommand { get; private set; } = "n/d";
        public string BuildingsByRole => buildingsByRole;
        public string HousingNeed => housingNeed;
        public string FoodCoverage => foodCoverage;
        public string EnergyCoverage => energyCoverage;
        public string StorageOccupancy => storageOccupancy;
        public string EmergencyReserve => emergencyReserve;
        public string AvailableConstructionFunds => availableConstructionFunds;
        public long AvailableConstructionFundsAmount => availableConstructionFundsAmount;
        public string CityCoveragePercent => cityCoveragePercent;
        public string CurrentSector => currentSector;
        public int BuildingsTotal => lastStructureCount;
        public int MaxCandidatesPerSlice { get; private set; } = 8;
        public int MaxPhysicsChecksPerSlice { get; private set; } = 16;
        public int CatalogIndexBuilds { get; private set; }
        public int CatalogIntentQueries { get; private set; }
        public int CatalogCandidateReads { get; private set; }
        public int CandidatesEvaluated { get; private set; }
        public int PhysicsChecks { get; private set; }
        public int FixedDefenseCount { get; private set; }
        public int MaxFixedDefenses { get; private set; }

        public void Refresh(float now, DadosPaisGoverno country, IA01WorldState world, IA01BuildCatalogAdapter catalog, IA01BuildDirector buildDirector)
        {
            bool catalogChanged = catalog != null && catalog.CatalogVersion != lastCatalogVersion;
            bool worldChanged = world != null && world.Version != lastWorldVersion;
            if (now < nextDiagnosticAt && !catalogChanged && !worldChanged)
            {
                SyncConstructionState(buildDirector);
                return;
            }

            nextDiagnosticAt = now + 0.5f;
            lastWorldVersion = world != null ? world.Version : lastWorldVersion;
            lastCatalogVersion = catalog != null ? catalog.CatalogVersion : lastCatalogVersion;
            CatalogIndexBuilds = catalog != null ? catalog.IndexBuildCount : 0;
            CatalogIntentQueries = catalog != null ? catalog.IntentQueryCount : 0;
            CatalogCandidateReads = catalog != null ? catalog.CandidateReadCount : 0;
            CandidatesEvaluated = buildDirector != null ? buildDirector.CandidatesEvaluated : 0;
            PhysicsChecks = buildDirector != null ? buildDirector.PhysicsChecks : 0;
            SyncConstructionState(buildDirector);

            IA01ConstructionGovernorSettings settings = profile != null ? profile.ConstructionGovernor : null;
            IA01ConstructionPhaseLimit phaseLimit = settings != null ? settings.ResolvePhaseLimit(context.CurrentStage) : null;
            MaxCandidatesPerSlice = settings != null ? settings.MaxCandidatesPerSlice : 8;
            MaxPhysicsChecksPerSlice = settings != null ? settings.MaxPhysicsChecksPerSlice : 16;
            long treasury = country != null ? country.saldo : 0L;
            int food = country != null ? country.comida : 0;
            int energy = country != null ? country.energia : 0;
            bool threatened = IA01OperationalRules.IsCapitalThreatened(world, controller != null && buildDirector != null ? buildDirector.CapitalMarker : null, country);
            bool atWar = country != null && country.emGuerra;
            IA01PopulationRecord population = context.GetPopulationSnapshot();
            int structureCount = world != null ? world.OwnedStructures.Count : 0;
            lastStructureCount = structureCount;
            MaxFixedDefenses = phaseLimit != null ? Mathf.Max(0, phaseLimit.maxDefense) : 0;
            IA01Manager manager = controller != null ? controller.Manager : null;
            FixedDefenseCount = manager != null && manager.WorldRegistry != null
                ? manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.FixedDefense)
                    + manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.AntiAirDefense)
                    + manager.WorldRegistry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.CoastalDefense)
                : 0;

            int housingNeedCount = Mathf.Max(0, population.Total - population.HousingCapacity);
            float projectedFoodNeed = Mathf.Max(1f, population.Total * 2f);
            float projectedEnergyNeed = Mathf.Max(1f, population.Total * 10f);
            float foodRatio = food / projectedFoodNeed;
            float energyRatio = energy / projectedEnergyNeed;
            emergencyReserve = settings != null ? settings.EmergencyReserve.ToString() : "n/d";
            int minimumConstructionReserve = settings != null ? settings.MinimumConstructionReserve : 0;
            int reservedOperationCosts = 0;
            int reservedFoodCosts = 0;
            int reservedMilitaryCosts = 0;
            long availableFunds = treasury - minimumConstructionReserve - reservedOperationCosts - reservedFoodCosts - reservedMilitaryCosts;
            if (settings != null)
            {
                availableFunds = Math.Max(0L, treasury - settings.EmergencyReserve - reservedOperationCosts - reservedFoodCosts - reservedMilitaryCosts);
            }

            // CapitalMarker pode existir durante uma obra ou em um estado parcial.
            // A reserva normal so pode voltar a valer depois que a CityPlanner
            // confirmou a prefeitura para esta nacao.
            bool foundationPending = controller == null || !controller.HasConfirmedCapital;
            bool openingInfrastructurePending = !foundationPending
                && (IsOpeningInfrastructurePending() || structureCount < 13);
            // A protecao da abertura permite concluir a infraestrutura inicial,
            // mas nunca deve transformar saldo zero em construcao ilimitada.
            bool openingInfrastructureFunded = openingInfrastructurePending
                && (settings == null || treasury >= settings.MinimumConstructionReserve);
            if (foundationPending)
            {
                // A reserva normal não pode impedir a primeira prefeitura.
                availableFunds = Math.Max(0L, treasury);
            }

            availableConstructionFundsAmount = availableFunds;
            availableConstructionFunds = availableFunds.ToString();
            housingNeed = housingNeedCount.ToString();
            foodCoverage = foodRatio.ToString("0.00") + "x";
            energyCoverage = energyRatio.ToString("0.00") + "x";
            storageOccupancy = ResolveStorageOccupancy();
            cityCoveragePercent = ResolveCoveragePercent(structureCount, phaseLimit);
            currentSector = "capital:" + context.TeamId;
            buildingsByRole = BuildRoleSummary(world);

            bool frozen = false;
            string reason = string.Empty;
            string unfreeze = string.Empty;

            if (!foundationPending && !openingInfrastructureFunded && settings != null && treasury < settings.MinimumConstructionReserve)
            {
                frozen = true;
                reason = "Treasury abaixo da reserva minima.";
                unfreeze = "Aguardar saldo acima de " + settings.MinimumConstructionReserve + ".";
            }

            if (!frozen && !foundationPending && !openingInfrastructureFunded && settings != null && treasury < settings.EmergencyReserve)
            {
                frozen = true;
                reason = "Treasury abaixo da reserva de emergencia.";
                unfreeze = "Aguardar saldo acima de " + settings.EmergencyReserve + ".";
            }

            bool economyCanExpand = treasury >= Mathf.Max(14000, (settings != null ? settings.EmergencyReserve : 0) * 2)
                && food > 0 && energy > 0;
            if (!frozen && !openingInfrastructurePending && context.CurrentPosture == IA01NationPosture.Recovery && !threatened && !atWar && !economyCanExpand)
            {
                frozen = true;
                reason = "Recovery sem ameaca imediata.";
                unfreeze = "Aumentar caixa, comida e energia ou surgir ameaca real.";
            }

            if (!frozen && !openingInfrastructurePending && phaseLimit != null && structureCount > phaseLimit.maxTotalStructures && !economyCanExpand)
            {
                frozen = true;
                reason = "Estruturas acima do limite da fase.";
                unfreeze = "Aumentar caixa/comida/energia para liberar expansao ou mudar a fase.";
            }

            // Falha de catálogo/lote pertence à etapa atual. Nunca congela a
            // nação inteira: o diretor da sequência aplica cooldown ou pula
            // somente a etapa que não possui item válido.

            float fpsMedio;
            float cpuMainMs;
            bool gcPressure;
            bool warmup;
            if (!frozen && DiagnosticoDesempenhoJogo.TryObterSnapshotRuntime(out fpsMedio, out cpuMainMs, out gcPressure, out warmup))
            {
                bool performanceBlocked = false;
                string performanceReason = string.Empty;
                string performanceUnfreeze = string.Empty;
                if (!foundationPending && !warmup && settings != null && fpsMedio > 0f && fpsMedio < settings.MinimumAcceptableFps)
                {
                    float minimumFrameBudgetMs = 1000f / Mathf.Max(1f, settings.MinimumAcceptableFps);
                    if (cpuMainMs >= minimumFrameBudgetMs)
                    {
                        performanceBlocked = true;
                        performanceReason = "FPS abaixo do limite de seguranca.";
                        performanceUnfreeze = "FPS voltar acima de " + settings.MinimumAcceptableFps.ToString("0.0") + ".";
                    }
                }

                if (!performanceBlocked && !foundationPending && !warmup && settings != null)
                {
                    float lastIaSliceMs = controller != null ? controller.LastExecutionResult.ConsumedMilliseconds : 0f;
                    if (lastIaSliceMs > settings.MaxIaFrameBudgetMs * 2f)
                    {
                        performanceBlocked = true;
                        performanceReason = "Slice da IA acima do budget.";
                        performanceUnfreeze = "Aguardar slices menores da IA.";
                    }
                }

                if (!performanceBlocked && !foundationPending && !warmup && gcPressure)
                {
                    performanceBlocked = true;
                    performanceReason = "Pressao de GC detectada.";
                    performanceUnfreeze = "Aguardar estabilizacao de memoria.";
                }

                if (performanceBlocked)
                {
                    consecutivePerformanceWarnings++;
                    if (consecutivePerformanceWarnings >= 3)
                    {
                        frozen = true;
                        reason = performanceReason;
                        unfreeze = performanceUnfreeze;
                    }
                }
                else
                {
                    consecutivePerformanceWarnings = 0;
                }
            }

            if (!frozen && !foundationPending && !openingInfrastructurePending && buildDirector != null && buildDirector.LastPlanningMilliseconds > (settings != null ? settings.MaxBuildPlannerBudgetMs * 2f : 5.0f))
            {
                frozen = true;
                reason = "Planner acima do budget.";
                unfreeze = "Diminuir candidatos ou pausar planejamento.";
            }

            if (frozen)
            {
                mode = IA01ConstructionMode.Frozen;
                freezeReason = reason;
                nextUnfreezeCondition = string.IsNullOrEmpty(unfreeze) ? "Aguardar mudanca relevante." : unfreeze;
            }
            else
            {
                mode = IA01ConstructionMode.Active;
                freezeReason = "Nenhum";
                nextUnfreezeCondition = "Nenhuma";
            }
        }

        public bool ShouldCancelCommand(string commandId)
        {
            return mode == IA01ConstructionMode.Frozen
                && !string.IsNullOrWhiteSpace(commandId)
                && commandId.StartsWith("build:", System.StringComparison.OrdinalIgnoreCase);
        }

        public void SetConstructionState(IA01ConstructionState state, string commandId = null)
        {
            CurrentConstructionState = state.ToString();
            ActiveConstructionCommand = string.IsNullOrWhiteSpace(commandId) ? "n/d" : commandId;
        }

        private void SyncConstructionState(IA01BuildDirector buildDirector)
        {
            if (buildDirector == null)
            {
                CurrentConstructionState = IA01ConstructionState.Idle.ToString();
                ActiveConstructionCommand = "n/d";
                return;
            }

            CurrentConstructionState = buildDirector.CurrentConstructionState.ToString();
            ActiveConstructionCommand = string.IsNullOrWhiteSpace(buildDirector.ActiveConstructionCommand) ? "n/d" : buildDirector.ActiveConstructionCommand;
        }

        private string ResolveCoveragePercent(int structureCount, IA01ConstructionPhaseLimit phaseLimit)
        {
            if (phaseLimit == null || phaseLimit.maxTotalStructures <= 0)
            {
                return "n/d";
            }

            float coverage = Mathf.Clamp01((float)structureCount / phaseLimit.maxTotalStructures);
            return (coverage * 100f).ToString("0.0") + "%";
        }

        private string ResolveStorageOccupancy()
        {
            IA01ResourceRecord storage;
            if (context.TryGetResource("storage", out storage) && storage != null && storage.Capacity > 0f)
            {
                float occupancy = Mathf.Clamp01(storage.Amount / storage.Capacity);
                return (occupancy * 100f).ToString("0.0") + "%";
            }

            return "n/d";
        }

        private bool IsOpeningInfrastructurePending()
        {
            IA01Manager manager = controller != null ? controller.Manager : null;
            IA01WorldRegistry registry = manager != null ? manager.WorldRegistry : null;
            if (registry == null || context == null)
            {
                return false;
            }

            bool hasTent = HasStructureMatching(registry, IA01StrategicRole.MilitaryProduction, "tenda", "tent");
            bool hasVehicleConstructor = HasStructureMatching(registry, IA01StrategicRole.MilitaryProduction, "construtor", "veiculo", "veículo", "vehicle");
            bool hasMilitaryAirport = HasStructureMatching(registry, IA01StrategicRole.Airfield, "militar", "military", "aeroporto_militar");
            bool hasCommercialAirport = HasStructureMatching(registry, IA01StrategicRole.Airfield, "comercial", "commercial", "aeroporto_comercial");
            bool hasShipyard = registry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.NavalBase) > 0
                || registry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.Shipyard) > 0
                || registry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.Port) > 0
                || registry.CountStructuresByStrategicRole(context.TeamId, IA01StrategicRole.Pier) > 0;

            return !hasTent || !hasVehicleConstructor || !hasMilitaryAirport || !hasCommercialAirport || !hasShipyard;
        }

        private bool HasStructureMatching(IA01WorldRegistry registry, IA01StrategicRole role, params string[] tokens)
        {
            IReadOnlyList<IA01WorldEntityRecord> records = registry.GetByTeam(context.TeamId);
            for (int i = 0; i < records.Count; i++)
            {
                IA01WorldEntityRecord record = records[i];
                if (record == null || record.Kind != IA01WorldEntityKind.Structure || record.StrategicRole != role)
                {
                    continue;
                }

                string text = NormalizeToken((record.StructureId ?? string.Empty) + " " + (record.DisplayName ?? string.Empty));
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(tokens[tokenIndex]) && text.Contains(NormalizeToken(tokens[tokenIndex])))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant().Replace('í', 'i').Replace('é', 'e').Replace('ã', 'a').Replace('á', 'a').Replace('ç', 'c');
        }

        private string BuildRoleSummary(IA01WorldState world)
        {
            roleSummary.Clear();
            if (world == null)
            {
                return "n/d";
            }

            int capital = 0;
            int residential = 0;
            int food = 0;
            int energy = 0;
            int storage = 0;
            int logistics = 0;
            int industrial = 0;
            int defense = 0;
            int unknown = 0;
            IA01Manager manager = controller != null ? controller.Manager : null;
            IReadOnlyList<IA01WorldEntityRecord> records = manager != null && manager.WorldRegistry != null
                ? manager.WorldRegistry.GetByTeam(context.TeamId)
                : null;
            if (records == null)
            {
                return "n/d";
            }

            for (int i = 0; i < records.Count; i++)
            {
                IA01WorldEntityRecord record = records[i];
                if (record == null || record.Kind != IA01WorldEntityKind.Structure)
                {
                    continue;
                }

                IA01StrategicRole role = record.StrategicRole;
                if (role == IA01StrategicRole.None && !string.IsNullOrWhiteSpace(record.Category))
                {
                    switch (record.Category.Trim())
                    {
                        case "Command":
                            role = IA01StrategicRole.Command;
                            break;
                        case "Residential":
                            role = IA01StrategicRole.Residential;
                            break;
                        case "Agricultural":
                            role = IA01StrategicRole.FoodProduction;
                            break;
                        case "Industrial":
                            role = IA01StrategicRole.Industrial;
                            break;
                        case "Energy":
                            role = IA01StrategicRole.EnergyProduction;
                            break;
                        case "Storage":
                            role = IA01StrategicRole.Storage;
                            break;
                        case "Logistics":
                            role = IA01StrategicRole.Logistics;
                            break;
                        case "Military":
                            role = IA01StrategicRole.MilitaryProduction;
                            break;
                        case "Defense":
                            role = IA01StrategicRole.FixedDefense;
                            break;
                        case "Air":
                            role = IA01StrategicRole.Airfield;
                            break;
                        case "Naval":
                            role = IA01StrategicRole.NavalBase;
                            break;
                        case "Research":
                            role = IA01StrategicRole.Research;
                            break;
                    }
                }

                switch (role)
                {
                    case IA01StrategicRole.Capital:
                    case IA01StrategicRole.Government:
                    case IA01StrategicRole.Command:
                        capital++;
                        break;
                    case IA01StrategicRole.Residential:
                        residential++;
                        break;
                    case IA01StrategicRole.FoodProduction:
                        food++;
                        break;
                    case IA01StrategicRole.EnergyProduction:
                        energy++;
                        break;
                    case IA01StrategicRole.Storage:
                        storage++;
                        break;
                    case IA01StrategicRole.Logistics:
                        logistics++;
                        break;
                    case IA01StrategicRole.Industrial:
                    case IA01StrategicRole.MilitaryProduction:
                        industrial++;
                        break;
                    case IA01StrategicRole.FixedDefense:
                    case IA01StrategicRole.AntiAirDefense:
                    case IA01StrategicRole.CoastalDefense:
                        defense++;
                        break;
                    default:
                        unknown++;
                        break;
                }
            }

            roleSummary.Append("Capital=").Append(capital);
            roleSummary.Append(" Residential=").Append(residential);
            roleSummary.Append(" Food=").Append(food);
            roleSummary.Append(" Energy=").Append(energy);
            roleSummary.Append(" Storage=").Append(storage);
            roleSummary.Append(" Logistics=").Append(logistics);
            roleSummary.Append(" Industrial=").Append(industrial);
            roleSummary.Append(" Defense=").Append(defense);
            if (unknown > 0)
            {
                roleSummary.Append(" Unknown=").Append(unknown);
            }
            return roleSummary.ToString();
        }
    }
}
