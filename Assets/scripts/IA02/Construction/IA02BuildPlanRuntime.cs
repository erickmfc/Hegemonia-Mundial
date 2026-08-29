using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    public sealed class IA02BuildPlanSelection
    {
        public IA02BuildPlanStep Step;
        public IA02BuildDefinition Definition;
        public IA02BuildSlot Slot;
        public IA02BuildAutonomousZone Zone;
        public IA02BuildLot Lot;
        public bool UsesPreparedSlot;
    }

    /// <summary>
    /// Resolves a prepared plan into one build candidate. It owns only plan and slot
    /// state; affordability, command serialization and confirmation stay in IA02BuildDirector.
    /// </summary>
    public sealed class IA02BuildPlanRuntime
    {
        private readonly IA02Controller controller;
        private readonly IA02RuntimeContext context;
        private readonly IA02WorldState world;
        private readonly IA02BuildCatalogAdapter catalog;
        private readonly IA02CityPlanner city;
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> forceAutonomous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IA02IntentType, List<IA02BuildPlanStep>> stepsByIntent = new Dictionary<IA02IntentType, List<IA02BuildPlanStep>>();

        private static readonly IA02IntentType[] ConstructionIntents =
        {
            IA02IntentType.EstablishCapital, IA02IntentType.BuildEnergy, IA02IntentType.BuildFoodProduction,
            IA02IntentType.BuildResidentialCapacity, IA02IntentType.BuildStorage, IA02IntentType.BuildLogistics,
            IA02IntentType.BuildRoad, IA02IntentType.BuildMilitaryAirport, IA02IntentType.BuildCommercialAirport,
            IA02IntentType.BuildShipyard, IA02IntentType.BuildPier, IA02IntentType.BuildOffshorePlatform, IA02IntentType.BuildIndustry, IA02IntentType.BuildDefense,
            IA02IntentType.BuildStarterHouse, IA02IntentType.BuildMediumApartment, IA02IntentType.BuildHighApartment,
            IA02IntentType.BuildMilitaryTent, IA02IntentType.BuildVehicleConstructor
        };

        private string pendingCommandId = string.Empty;
        private string pendingStepId = string.Empty;
        private IA02BuildPlan indexedPlan;
        private int indexedStepCount = -1;

        public string CurrentStepId { get; private set; } = "n/d";
        public string PlacementModeStatus { get; private set; } = "n/d";
        public string RequestedRoleStatus { get; private set; } = "n/d";
        public string SelectedSlotStatus { get; private set; } = "n/d";
        public string SlotStateStatus { get; private set; } = "n/d";
        public string AlternativeSlotsStatus { get; private set; } = "0";
        public string SlotValidationResult { get; private set; } = "n/d";
        public string PendingCommandId => pendingCommandId;

        public IA02BuildPlanRuntime(IA02Controller controller, IA02RuntimeContext context, IA02WorldState world, IA02BuildCatalogAdapter catalog, IA02CityPlanner city)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.catalog = catalog;
            this.city = city;
        }

        public bool TrySelect(IA02Intent intent, float now, out IA02BuildPlanSelection selection, out bool handled, out string reason)
        {
            selection = null;
            handled = false;
            reason = "roteiro inativo";
            ResetDiagnostics();
            IA02BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA02CityLayout layout = controller != null ? controller.CityLayout : null;
            if (controller == null || plan == null || layout == null) return false;
            ReconcileRestoredOccupancy(layout);
            EnsurePlanIndex(plan);
            if (!stepsByIntent.TryGetValue(intent.Type, out List<IA02BuildPlanStep> steps))
            {
                steps = BuildDirectStepList(plan, intent.Type);
                if (steps.Count == 0) return false;
            }

            // O create de quartel tem prioridade quando foi configurado na
            // cena; sem ele, o create antigo da tenda continua sendo usado.
            if (intent.Type == IA02IntentType.BuildMilitaryTent && steps.Count > 1)
            {
                for (int i = 1; i < steps.Count; i++)
                {
                    IA02BuildPlanStep candidate = steps[i];
                    if (candidate != null && string.Equals(candidate.primarySlotId, "ia02.local.quartel", StringComparison.OrdinalIgnoreCase))
                    {
                        List<IA02BuildPlanStep> reordered = new List<IA02BuildPlanStep>(steps);
                        IA02BuildPlanStep first = reordered[0];
                        reordered[0] = candidate;
                        reordered[i] = first;
                        steps = reordered;
                        break;
                    }
                }
            }

            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
                if (step == null || !MatchesIntent(step, intent)) continue;
                handled = true;
                CurrentStepId = step.StepId;
                RequestedRoleStatus = ResolveStepRole(step).ToString();
                PlacementModeStatus = step.placementMode.ToString();

                if (completed.Contains(step.StepId))
                {
                    reason = "passo ja concluido";
                    continue;
                }
                if (blocked.Contains(step.StepId))
                {
                    reason = "passo bloqueado";
                    return false;
                }
                if (cooldownUntil.TryGetValue(step.StepId, out float cooldown) && now < cooldown)
                {
                    reason = "cooldown do passo ativo";
                    return false;
                }
                if (step.constructionData == null || !catalog.TryGetForBlueprint(step.constructionData, out IA02BuildDefinition definition))
                {
                    reason = "ficha DadosConstrucao invalida ou nao estrutural";
                    SlotValidationResult = reason;
                    return false;
                }
                // Alguns prefabs navais antigos chegam do catalogo como NavalBase
                // generico. O roteiro preserva a funcao especifica do create.
                if (step.requiredRole != IA02StrategicRole.None)
                    definition.StrategicRole = step.requiredRole;
                if (step.minimumStage > (int)context.CurrentStage)
                {
                    reason = "fase minima ainda nao atingida";
                    return false;
                }
                if (!EvaluateCondition(step.condition, definition))
                {
                    reason = "condicao do passo ainda nao atendida";
                    return false;
                }
                if (CountStructures(ResolveStepRole(step), definition.ItemId) >= Mathf.Max(1, step.maximumCount))
                {
                    completed.Add(step.StepId);
                    reason = "quantidade maxima do passo atingida";
                    continue;
                }

                if (TryResolvePlacement(step, definition, layout, out selection, out reason))
                {
                    SlotValidationResult = "valido";
                    return true;
                }

                SlotValidationResult = reason;
                // "Tentar slot alternativo" nao deve aprisionar o diretor no roteiro
                // quando todos os slots preparados falharam. Libere o fallback do
                // catalogo/planejador geral para ele procurar um lote valido no mundo.
                if (step.failurePolicy == IA02FailurePolicy.TryAlternativeSlot)
                {
                    handled = false;
                }
                return false;
            }

            if (handled && string.IsNullOrWhiteSpace(reason)) reason = "nenhum passo elegivel";
            return false;
        }

        public bool TryGetRestoredPending(IA02Intent intent, out IA02BuildPlanSelection selection, out string reason)
        {
            selection = null;
            reason = string.Empty;
            if (intent == null || string.IsNullOrWhiteSpace(pendingCommandId) || string.IsNullOrWhiteSpace(pendingStepId)) return false;
            IA02BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA02CityLayout layout = controller != null ? controller.CityLayout : null;
            if (plan == null || layout == null) return false;
            IReadOnlyList<IA02BuildPlanStep> steps = plan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
                if (step == null || !string.Equals(step.StepId, pendingStepId, StringComparison.OrdinalIgnoreCase) || !MatchesIntent(step, intent)) continue;
                if (step.constructionData == null || !catalog.TryGetForBlueprint(step.constructionData, out IA02BuildDefinition definition))
                {
                    reason = "ficha pendente ausente ou invalida";
                    return false;
                }
                IA02BuildSlot slot = null;
                if (!string.IsNullOrWhiteSpace(step.primarySlotId)) layout.TryGetSlot(step.primarySlotId, out slot);
                if (slot == null && layout.SlotRegistry != null)
                {
                    foreach (IA02BuildSlot candidate in layout.SlotRegistry.GetAllSlots())
                    {
                        if (candidate != null && string.Equals(candidate.ReservedCommandId, pendingCommandId, StringComparison.OrdinalIgnoreCase))
                        {
                            slot = candidate;
                            break;
                        }
                    }
                }
                if (slot == null || slot.State != IA02BuildSlotState.Reserved
                    || !string.Equals(slot.ReservedCommandId, pendingCommandId, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "reserva pendente nao pode ser restaurada";
                    return false;
                }
                selection = new IA02BuildPlanSelection { Step = step, Definition = definition, Slot = slot, Lot = slot.CreateLot(definition), UsesPreparedSlot = true };
                CurrentStepId = step.StepId;
                PlacementModeStatus = step.placementMode.ToString();
                RequestedRoleStatus = ResolveStepRole(step).ToString();
                SelectedSlotStatus = slot.SlotId;
                SlotStateStatus = slot.State.ToString();
                reason = string.Empty;
                return true;
            }
            return false;
        }

        public bool TryReserve(IA02BuildPlanSelection selection, string commandId, float now, out string reason)
        {
            reason = string.Empty;
            if (selection == null || !selection.UsesPreparedSlot) return true;
            if (selection.Slot == null)
            {
                reason = "slot preparado ausente";
                return false;
            }

            if (!selection.Slot.TryReserve(commandId, context.NationId, selection.Definition.ItemId, now, out reason)) return false;
            pendingCommandId = commandId ?? string.Empty;
            pendingStepId = selection.Step != null ? selection.Step.StepId : string.Empty;
            SelectedSlotStatus = selection.Slot.SlotId;
            SlotStateStatus = selection.Slot.State.ToString();
            return true;
        }

        public void MarkExecuting(IA02BuildPlanSelection selection, string commandId)
        {
            if (selection != null && selection.UsesPreparedSlot && selection.Slot != null)
            {
                selection.Slot.MarkUnderConstruction(commandId);
                SlotStateStatus = selection.Slot.State.ToString();
            }
        }

        public void Confirm(IA02BuildPlanSelection selection, string commandId, bool success, string reason, float now)
        {
            if (selection == null || selection.Step == null) return;
            IA02BuildPlanStep step = selection.Step;
            pendingCommandId = string.Empty;
            pendingStepId = string.Empty;
            if (success)
            {
                if (selection.UsesPreparedSlot && selection.Slot != null)
                {
                    selection.Slot.MarkOccupied(commandId, selection.Definition.ItemId);
                    SlotStateStatus = selection.Slot.State.ToString();
                }
                cooldownUntil[step.StepId] = now + Mathf.Max(0f, step.cooldownAfterCompletion);
                if (step.maximumCount <= 1) completed.Add(step.StepId);
                return;
            }

            if (selection.UsesPreparedSlot && selection.Slot != null)
            {
                bool invalidate = step.failurePolicy == IA02FailurePolicy.TryAlternativeSlot;
                selection.Slot.Release(commandId, invalidate, reason);
                if (invalidate) selection.Slot.MarkBlocked(reason);
                SlotStateStatus = selection.Slot.State.ToString();
            }

            switch (step.failurePolicy)
            {
                case IA02FailurePolicy.UseAutonomousZone:
                    forceAutonomous.Add(step.StepId);
                    break;
                case IA02FailurePolicy.SkipOptionalStep:
                    if (!step.required) completed.Add(step.StepId);
                    break;
                case IA02FailurePolicy.BlockMandatoryStep:
                    // Falhas de execucao podem ser transitorias (troca de cena,
                    // fisica ainda carregando ou backend momentaneamente ausente).
                    // Um passo obrigatorio nao pode ficar bloqueado para sempre por
                    // uma unica tentativa; mantenha a sequencia parada apenas durante
                    // um cooldown curto e tente novamente quando o mundo estabilizar.
                    if (step.required)
                    {
                        cooldownUntil[step.StepId] = now + Mathf.Max(1f, step.cooldownAfterCompletion);
                    }
                    break;
            }
        }

        public SaveIA02BuildPlanState CaptureSaveState()
        {
            IA02BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA02CityLayout layout = controller != null ? controller.CityLayout : null;
            SaveIA02BuildPlanState saved = new SaveIA02BuildPlanState
            {
                planId = plan != null ? plan.PlanId : string.Empty,
                layoutId = layout != null ? layout.LayoutId : string.Empty,
                planVersion = plan != null ? plan.LayoutVersion : 0,
                pendingCommandId = pendingCommandId,
                pendingStepId = pendingStepId
            };
            foreach (string step in completed) saved.completedSteps.Add(step);
            foreach (string step in blocked) saved.blockedSteps.Add(step);
            foreach (KeyValuePair<string, float> pair in cooldownUntil)
            {
                saved.cooldowns.Add(new SaveIA02BuildCooldownState { stepId = pair.Key, until = pair.Value });
            }
            if (layout != null) saved.slots = layout.CaptureSlotSaveState();
            return saved;
        }

        public void RestoreSaveState(SaveIA02BuildPlanState saved)
        {
            if (saved == null) return;
            IA02BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA02CityLayout layout = controller != null ? controller.CityLayout : null;
            if (plan != null && !string.IsNullOrWhiteSpace(saved.planId)
                && !string.Equals(plan.PlanId, saved.planId, StringComparison.OrdinalIgnoreCase)) return;
            completed.Clear();
            blocked.Clear();
            cooldownUntil.Clear();
            if (saved.completedSteps != null) for (int i = 0; i < saved.completedSteps.Count; i++) completed.Add(saved.completedSteps[i]);
            // `blockedSteps` pertence ao formato antigo, que podia transformar uma
            // falha transitoria em bloqueio permanente. O runtime atual usa cooldown
            // e nao produz novos bloqueios; portanto nenhum bloqueio legado e restaurado.
            if (saved.cooldowns != null)
            {
                for (int i = 0; i < saved.cooldowns.Count; i++)
                {
                    SaveIA02BuildCooldownState cooldown = saved.cooldowns[i];
                    if (cooldown != null && !string.IsNullOrWhiteSpace(cooldown.stepId)) cooldownUntil[cooldown.stepId] = cooldown.until;
                }
            }
            pendingCommandId = saved.pendingCommandId ?? string.Empty;
            pendingStepId = saved.pendingStepId ?? string.Empty;
            if (layout != null && (string.IsNullOrWhiteSpace(saved.layoutId)
                || string.Equals(layout.LayoutId, saved.layoutId, StringComparison.OrdinalIgnoreCase)))
            {
                layout.RestoreSlotSaveState(saved.slots);
            }
        }

        private bool TryResolvePlacement(IA02BuildPlanStep step, IA02BuildDefinition definition, IA02CityLayout layout, out IA02BuildPlanSelection selection, out string reason)
        {
            selection = null;
            bool useAutonomous = forceAutonomous.Contains(step.StepId) || step.placementMode == IA02PlacementMode.AutonomousZone;
            if (useAutonomous)
            {
                // Alguns planos antigos marcam os passos iniciais como
                // AutonomousZone, mas a cena de campanha usa slots preparados
                // e deixa a expansao autonoma desligada. Nao deixe isso travar
                // a abertura: use a zona quando existir e, caso contrario,
                // caia para o slot do mesmo grupo (energia, agricola, etc.).
                if (controller.AllowAutonomousExpansion
                    && layout.TryGetAutonomousZone(step.autonomousZoneId, out IA02BuildAutonomousZone zone)
                    && zone.IsCompatible(definition))
                {
                    selection = new IA02BuildPlanSelection { Step = step, Definition = definition, Zone = zone, UsesPreparedSlot = false };
                    SelectedSlotStatus = "zone:" + zone.ZoneId;
                    SlotStateStatus = "Autonomous";
                    reason = string.Empty;
                    return true;
                }

                IA02BuildSlot preparedSlot = null;
                string preparedReason = string.Empty;
                string specializedReason = string.Empty;
                bool preparedFallback = controller.UsePreparedSlots
                    && !string.IsNullOrWhiteSpace(step.slotGroupId)
                    && layout.TryGetAvailableGroupSlot(step.slotGroupId, definition, out preparedSlot, out preparedReason)
                    && ValidateSpecializedSlot(preparedSlot, definition, out specializedReason);
                if (preparedFallback)
                {
                    selection = new IA02BuildPlanSelection
                    {
                        Step = step,
                        Definition = definition,
                        Slot = preparedSlot,
                        Lot = preparedSlot.CreateLot(definition),
                        UsesPreparedSlot = true
                    };
                    SelectedSlotStatus = preparedSlot.SlotId;
                    SlotStateStatus = preparedSlot.State.ToString();
                    AlternativeSlotsStatus = "preparados:" + step.slotGroupId;
                    reason = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(preparedReason))
                    reason = preparedReason;
                else if (!string.IsNullOrWhiteSpace(specializedReason))
                    reason = specializedReason;
                else if (!controller.AllowAutonomousExpansion)
                    reason = "expansao autonoma desativada e nenhum slot preparado compativel";
                else
                    reason = "zona autonoma ausente/incompativel e nenhum slot preparado compativel";
                return false;
            }

            if (!controller.UsePreparedSlots)
            {
                reason = "slots preparados desativados";
                return false;
            }

            IA02BuildSlot slot = null;
            if (step.placementMode == IA02PlacementMode.ExactSlot)
            {
                if (!layout.TryGetSlot(step.primarySlotId, out slot))
                {
                    reason = "slot exato ausente";
                    return false;
                }
                if (!slot.IsCompatible(definition, context.TeamId, out reason))
                {
                    if (!(slot.AllowAlternativeSlot && !string.IsNullOrWhiteSpace(step.slotGroupId)
                        && layout.TryGetAvailableGroupSlot(step.slotGroupId, definition, out slot, out reason))) return false;
                }
            }
            else if (!layout.TryGetAvailableGroupSlot(step.slotGroupId, definition, out slot, out reason))
            {
                return false;
            }

            if (!ValidateSpecializedSlot(slot, definition, out reason)) return false;

            selection = new IA02BuildPlanSelection
            {
                Step = step,
                Definition = definition,
                Slot = slot,
                Lot = slot.CreateLot(definition),
                UsesPreparedSlot = true
            };
            SelectedSlotStatus = slot.SlotId;
            SlotStateStatus = slot.State.ToString();
            AlternativeSlotsStatus = string.IsNullOrWhiteSpace(step.slotGroupId) ? "0" : "preparados:" + step.slotGroupId;
            return true;
        }

        private bool EvaluateCondition(IA02BuildCondition condition, IA02BuildDefinition definition)
        {
            if (condition == null || condition.type == IA02BuildConditionType.Always) return true;
            IA02PopulationRecord population = context.GetPopulationSnapshot();
            DadosPaisGoverno country = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(context.TeamId) : null;
            switch (condition.type)
            {
                case IA02BuildConditionType.CapitalMissing: return city == null || city.Capital == null;
                case IA02BuildConditionType.RoleMissing: return CountStructures(condition.role == IA02StrategicRole.None ? definition.StrategicRole : condition.role, null) == 0;
                case IA02BuildConditionType.HousingDeficit: return population.Total > population.HousingCapacity;
                case IA02BuildConditionType.FoodBelowTarget: return country != null && country.comida < population.Total * Mathf.Max(1f, condition.target);
                case IA02BuildConditionType.EnergyBelowTarget: return country != null && country.energia < population.Total * Mathf.Max(1f, condition.target);
                case IA02BuildConditionType.StorageRequired: return CountStructures(IA02StrategicRole.Storage, null) == 0;
                case IA02BuildConditionType.Threatened: return IA02OperationalRules.IsCapitalThreatened(world, city != null ? city.Capital : null, country);
                case IA02BuildConditionType.MinimumStage: return (int)context.CurrentStage >= Mathf.RoundToInt(condition.target);
                default: return true;
            }
        }

        private int CountStructures(IA02StrategicRole role, string itemId)
        {
            IA02WorldRegistry registry = controller != null && controller.Manager != null ? controller.Manager.WorldRegistry : null;
            if (registry == null) return 0;
            if (string.IsNullOrWhiteSpace(itemId)) return registry.CountStructuresByStrategicRole(context.TeamId, role);
            IReadOnlyList<IA02WorldEntityRecord> records = registry.GetByTeam(context.TeamId);
            int count = 0;
            for (int i = 0; i < records.Count; i++)
            {
                IA02WorldEntityRecord record = records[i];
                if (record != null && record.Kind == IA02WorldEntityKind.Structure && string.Equals(record.StructureId, itemId, StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count;
        }

        private void ReconcileRestoredOccupancy(IA02CityLayout layout)
        {
            IA02BuildSlotRegistry registry = layout != null ? layout.SlotRegistry : null;
            IA02WorldRegistry worldRegistry = controller != null && controller.Manager != null ? controller.Manager.WorldRegistry : null;
            if (registry == null || worldRegistry == null) return;
            IReadOnlyCollection<IA02BuildSlot> slots = registry.GetAllSlots();
            foreach (IA02BuildSlot slot in slots)
            {
                if (slot == null || slot.State != IA02BuildSlotState.UnderConstruction || string.IsNullOrWhiteSpace(slot.ConstructedItemId)) continue;
                if (CountStructures(IA02StrategicRole.None, slot.ConstructedItemId) > 0)
                {
                    string restoredCommandId = slot.ReservedCommandId;
                    slot.MarkOccupied(restoredCommandId, slot.ConstructedItemId);
                    if (string.Equals(pendingCommandId, restoredCommandId, StringComparison.OrdinalIgnoreCase))
                    {
                        pendingCommandId = string.Empty;
                        pendingStepId = string.Empty;
                    }
                }
            }
        }

        private static bool MatchesIntent(IA02BuildPlanStep step, IA02Intent intent)
        {
            if (step == null || intent == null) return false;
            IA02StrategicRole role = ResolveStepRole(step);
            switch (intent.Type)
            {
                case IA02IntentType.EstablishCapital: return role == IA02StrategicRole.Capital || role == IA02StrategicRole.Command || role == IA02StrategicRole.Government;
                case IA02IntentType.BuildEnergy: return role == IA02StrategicRole.EnergyProduction;
                case IA02IntentType.BuildFoodProduction: return role == IA02StrategicRole.FoodProduction;
                case IA02IntentType.BuildResidentialCapacity: return role == IA02StrategicRole.Residential;
                case IA02IntentType.BuildStorage: return role == IA02StrategicRole.Storage;
                case IA02IntentType.BuildLogistics:
                case IA02IntentType.BuildRoad: return role == IA02StrategicRole.Logistics;
                case IA02IntentType.BuildIndustry: return role == IA02StrategicRole.Industrial;
                case IA02IntentType.BuildDefense: return role == IA02StrategicRole.FixedDefense || role == IA02StrategicRole.AntiAirDefense || role == IA02StrategicRole.CoastalDefense;
                case IA02IntentType.BuildMilitaryAirport:
                    return (role == IA02StrategicRole.Airfield || role == IA02StrategicRole.Airport)
                        && StepHasAnyToken(step, "militar", "military", "base aerea", "airbase");
                case IA02IntentType.BuildCommercialAirport:
                    return (role == IA02StrategicRole.Airfield || role == IA02StrategicRole.Airport)
                        && StepHasAnyToken(step, "comercial", "commercial", "civil", "terminal");
                case IA02IntentType.BuildShipyard: return role == IA02StrategicRole.NavalBase || role == IA02StrategicRole.Shipyard || role == IA02StrategicRole.Port || role == IA02StrategicRole.Pier;
                case IA02IntentType.BuildPier: return role == IA02StrategicRole.Pier;
                case IA02IntentType.BuildOffshorePlatform: return role == IA02StrategicRole.NavalBase;
                case IA02IntentType.BuildStarterHouse: return role == IA02StrategicRole.Residential && StepHasAnyToken(step, "casa", "house");
                case IA02IntentType.BuildMediumApartment: return role == IA02StrategicRole.Residential && StepHasAnyToken(step, "medio", "médio", "apartment", "apartamento");
                case IA02IntentType.BuildHighApartment: return role == IA02StrategicRole.Residential && StepHasAnyToken(step, "hard", "alto", "high", "torre");
                case IA02IntentType.BuildMilitaryTent: return role == IA02StrategicRole.MilitaryProduction && StepHasAnyToken(step, "tenda", "tent", "quartel", "barracks");
                case IA02IntentType.BuildVehicleConstructor: return role == IA02StrategicRole.MilitaryProduction && StepHasAnyToken(step, "construtor", "veiculo", "veículo", "vehicle");
                default: return false;
            }
        }

        private static List<IA02BuildPlanStep> BuildDirectStepList(IA02BuildPlan plan, IA02IntentType intent)
        {
            List<IA02BuildPlanStep> matches = new List<IA02BuildPlanStep>();
            IReadOnlyList<IA02BuildPlanStep> steps = plan != null ? plan.Steps : null;
            if (steps == null) return matches;
            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
                if (StepLooksLikeIntent(step, intent))
                {
                    matches.Add(step);
                }
            }

            return matches;
        }

        private static bool StepLooksLikeIntent(IA02BuildPlanStep step, IA02IntentType intent)
        {
            if (step == null) return false;
            string text = BuildStepSearchText(step);
            switch (intent)
            {
                case IA02IntentType.BuildMilitaryAirport:
                    return text.Contains("aeroporto_militar") || text.Contains("aeroporto militar") || text.Contains("military airport") || text.Contains("base aerea militar");
                case IA02IntentType.BuildCommercialAirport:
                    return text.Contains("aeroporto_comercial") || text.Contains("aeroporto comercial") || text.Contains("commercial airport") || text.Contains("terminal civil");
                case IA02IntentType.BuildShipyard:
                    return text.Contains("estaleiro") || text.Contains("shipyard") || text.Contains("naval yard");
                case IA02IntentType.BuildPier:
                    return text.Contains("pier");
                case IA02IntentType.BuildOffshorePlatform:
                    return text.Contains("plataforma") || text.Contains("offshore");
                case IA02IntentType.BuildMilitaryTent:
                    return text.Contains("tenda") || text.Contains("tent") || text.Contains("quartel") || text.Contains("barracks");
                case IA02IntentType.BuildVehicleConstructor:
                    return text.Contains("construtor") || text.Contains("veiculo") || text.Contains("vehicle");
                default:
                    return false;
            }
        }

        private static bool StepHasAnyToken(IA02BuildPlanStep step, params string[] tokens)
        {
            if (step == null || step.constructionData == null || tokens == null) return false;
            string text = BuildStepSearchText(step);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[i]) && text.Contains(tokens[i].ToLowerInvariant())) return true;
            }
            return false;
        }

        private static string BuildStepSearchText(IA02BuildPlanStep step)
        {
            DadosConstrucao data = step != null ? step.constructionData : null;
            return ((step != null ? step.StepId : string.Empty) + " "
                + (step != null ? step.primarySlotId : string.Empty) + " "
                + (data != null ? data.GetStableId() : string.Empty) + " "
                + (data != null ? data.GetDisplayName() : string.Empty) + " "
                + (data != null ? data.aliases : string.Empty)).ToLowerInvariant();
        }

        private static IA02StrategicRole ResolveStepRole(IA02BuildPlanStep step)
        {
            return step.requiredRole != IA02StrategicRole.None
                ? step.requiredRole
                : step.constructionData != null
                    ? (IA02StrategicRole)(int)step.constructionData.StrategicRole
                    : IA02StrategicRole.None;
        }

        private void EnsurePlanIndex(IA02BuildPlan plan)
        {
            IReadOnlyList<IA02BuildPlanStep> steps = plan.Steps;
            if (ReferenceEquals(indexedPlan, plan) && indexedStepCount == steps.Count) return;
            stepsByIntent.Clear();
            indexedPlan = plan;
            indexedStepCount = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
                if (step == null) continue;
                for (int intentIndex = 0; intentIndex < ConstructionIntents.Length; intentIndex++)
                {
                    IA02IntentType intent = ConstructionIntents[intentIndex];
                    if (!MatchesIntent(step, new IA02Intent { Type = intent })) continue;
                    if (!stepsByIntent.TryGetValue(intent, out List<IA02BuildPlanStep> bucket))
                    {
                        bucket = new List<IA02BuildPlanStep>();
                        stepsByIntent.Add(intent, bucket);
                    }
                    bucket.Add(step);
                }
            }
        }

        private static bool ValidateSpecializedSlot(IA02BuildSlot slot, IA02BuildDefinition definition, out string reason)
        {
            reason = string.Empty;
            if (slot == null || definition == null) return false;
            if (definition.Domain == IA02BuildDomain.Coastal || definition.Domain == IA02BuildDomain.Water)
            {
                IA02NavalBuildSlot naval = slot.GetComponent<IA02NavalBuildSlot>();
                if (naval == null)
                {
                    reason = "slot naval especializado ausente";
                    return false;
                }
                if (!naval.TryValidateCached(out reason)) return false;
            }
            if (definition.StrategicRole == IA02StrategicRole.Airfield || definition.StrategicRole == IA02StrategicRole.Airport)
            {
                IA02AirportBuildSlot airport = slot.GetComponent<IA02AirportBuildSlot>();
                if (airport == null)
                {
                    reason = "slot de aeroporto especializado ausente";
                    return false;
                }
                if (!airport.TryValidateCached(out reason)) return false;
            }
            return true;
        }

        private void ResetDiagnostics()
        {
            CurrentStepId = "n/d";
            PlacementModeStatus = "n/d";
            RequestedRoleStatus = "n/d";
            SelectedSlotStatus = "n/d";
            SlotStateStatus = "n/d";
            AlternativeSlotsStatus = "0";
            SlotValidationResult = "n/d";
        }
    }
}
