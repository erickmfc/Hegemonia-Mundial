using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01BuildPlanSelection
    {
        public IA01BuildPlanStep Step;
        public IA01BuildDefinition Definition;
        public IA01BuildSlot Slot;
        public IA01BuildAutonomousZone Zone;
        public IA01BuildLot Lot;
        public bool UsesPreparedSlot;
    }

    /// <summary>
    /// Resolves a prepared plan into one build candidate. It owns only plan and slot
    /// state; affordability, command serialization and confirmation stay in IA01BuildDirector.
    /// </summary>
    public sealed class IA01BuildPlanRuntime
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly IA01WorldState world;
        private readonly IA01BuildCatalogAdapter catalog;
        private readonly IA01CityPlanner city;
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> forceAutonomous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IA01IntentType, List<IA01BuildPlanStep>> stepsByIntent = new Dictionary<IA01IntentType, List<IA01BuildPlanStep>>();

        private static readonly IA01IntentType[] ConstructionIntents =
        {
            IA01IntentType.EstablishCapital, IA01IntentType.BuildEnergy, IA01IntentType.BuildFoodProduction,
            IA01IntentType.BuildResidentialCapacity, IA01IntentType.BuildStorage, IA01IntentType.BuildLogistics,
            IA01IntentType.BuildRoad, IA01IntentType.BuildMilitaryAirport, IA01IntentType.BuildCommercialAirport,
            IA01IntentType.BuildShipyard, IA01IntentType.BuildPier, IA01IntentType.BuildOffshorePlatform, IA01IntentType.BuildIndustry, IA01IntentType.BuildDefense,
            IA01IntentType.BuildStarterHouse, IA01IntentType.BuildMediumApartment, IA01IntentType.BuildHighApartment,
            IA01IntentType.BuildMilitaryTent, IA01IntentType.BuildVehicleConstructor
        };

        private string pendingCommandId = string.Empty;
        private string pendingStepId = string.Empty;
        private IA01BuildPlan indexedPlan;
        private int indexedStepCount = -1;

        public string CurrentStepId { get; private set; } = "n/d";
        public string PlacementModeStatus { get; private set; } = "n/d";
        public string RequestedRoleStatus { get; private set; } = "n/d";
        public string SelectedSlotStatus { get; private set; } = "n/d";
        public string SlotStateStatus { get; private set; } = "n/d";
        public string AlternativeSlotsStatus { get; private set; } = "0";
        public string SlotValidationResult { get; private set; } = "n/d";
        public string PendingCommandId => pendingCommandId;

        public IA01BuildPlanRuntime(IA01Controller controller, IA01RuntimeContext context, IA01WorldState world, IA01BuildCatalogAdapter catalog, IA01CityPlanner city)
        {
            this.controller = controller;
            this.context = context;
            this.world = world;
            this.catalog = catalog;
            this.city = city;
        }

        public bool TrySelect(IA01Intent intent, float now, out IA01BuildPlanSelection selection, out bool handled, out string reason)
        {
            selection = null;
            handled = false;
            reason = "roteiro inativo";
            ResetDiagnostics();
            IA01BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA01CityLayout layout = controller != null ? controller.CityLayout : null;
            if (controller == null || plan == null || layout == null) return false;
            ReconcileRestoredOccupancy(layout);
            EnsurePlanIndex(plan);
            if (!stepsByIntent.TryGetValue(intent.Type, out List<IA01BuildPlanStep> steps))
            {
                steps = BuildDirectStepList(plan, intent.Type);
                if (steps.Count == 0) return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
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
                if (step.constructionData == null || !catalog.TryGetForBlueprint(step.constructionData, out IA01BuildDefinition definition))
                {
                    reason = "ficha DadosConstrucao invalida ou nao estrutural";
                    SlotValidationResult = reason;
                    return false;
                }
                // Alguns prefabs navais antigos chegam do catalogo como NavalBase
                // generico. O roteiro preserva a funcao especifica do create.
                if (step.requiredRole != IA01StrategicRole.None)
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
                if (step.failurePolicy == IA01FailurePolicy.TryAlternativeSlot)
                {
                    handled = false;
                }
                return false;
            }

            if (handled && string.IsNullOrWhiteSpace(reason)) reason = "nenhum passo elegivel";
            return false;
        }

        public bool TryGetRestoredPending(IA01Intent intent, out IA01BuildPlanSelection selection, out string reason)
        {
            selection = null;
            reason = string.Empty;
            if (intent == null || string.IsNullOrWhiteSpace(pendingCommandId) || string.IsNullOrWhiteSpace(pendingStepId)) return false;
            IA01BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA01CityLayout layout = controller != null ? controller.CityLayout : null;
            if (plan == null || layout == null) return false;
            IReadOnlyList<IA01BuildPlanStep> steps = plan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
                if (step == null || !string.Equals(step.StepId, pendingStepId, StringComparison.OrdinalIgnoreCase) || !MatchesIntent(step, intent)) continue;
                if (step.constructionData == null || !catalog.TryGetForBlueprint(step.constructionData, out IA01BuildDefinition definition))
                {
                    reason = "ficha pendente ausente ou invalida";
                    return false;
                }
                IA01BuildSlot slot = null;
                if (!string.IsNullOrWhiteSpace(step.primarySlotId)) layout.TryGetSlot(step.primarySlotId, out slot);
                if (slot == null && layout.SlotRegistry != null)
                {
                    foreach (IA01BuildSlot candidate in layout.SlotRegistry.GetAllSlots())
                    {
                        if (candidate != null && string.Equals(candidate.ReservedCommandId, pendingCommandId, StringComparison.OrdinalIgnoreCase))
                        {
                            slot = candidate;
                            break;
                        }
                    }
                }
                if (slot == null || slot.State != IA01BuildSlotState.Reserved
                    || !string.Equals(slot.ReservedCommandId, pendingCommandId, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "reserva pendente nao pode ser restaurada";
                    return false;
                }
                selection = new IA01BuildPlanSelection { Step = step, Definition = definition, Slot = slot, Lot = slot.CreateLot(definition), UsesPreparedSlot = true };
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

        public bool TryReserve(IA01BuildPlanSelection selection, string commandId, float now, out string reason)
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

        public void MarkExecuting(IA01BuildPlanSelection selection, string commandId)
        {
            if (selection != null && selection.UsesPreparedSlot && selection.Slot != null)
            {
                selection.Slot.MarkUnderConstruction(commandId);
                SlotStateStatus = selection.Slot.State.ToString();
            }
        }

        public void Confirm(IA01BuildPlanSelection selection, string commandId, bool success, string reason, float now)
        {
            if (selection == null || selection.Step == null) return;
            IA01BuildPlanStep step = selection.Step;
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
                bool invalidate = step.failurePolicy == IA01FailurePolicy.TryAlternativeSlot;
                selection.Slot.Release(commandId, invalidate, reason);
                if (invalidate) selection.Slot.MarkBlocked(reason);
                SlotStateStatus = selection.Slot.State.ToString();
            }

            switch (step.failurePolicy)
            {
                case IA01FailurePolicy.UseAutonomousZone:
                    forceAutonomous.Add(step.StepId);
                    break;
                case IA01FailurePolicy.SkipOptionalStep:
                    if (!step.required) completed.Add(step.StepId);
                    break;
                case IA01FailurePolicy.BlockMandatoryStep:
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

        public SaveIA01BuildPlanState CaptureSaveState()
        {
            IA01BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA01CityLayout layout = controller != null ? controller.CityLayout : null;
            SaveIA01BuildPlanState saved = new SaveIA01BuildPlanState
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
                saved.cooldowns.Add(new SaveIA01BuildCooldownState { stepId = pair.Key, until = pair.Value });
            }
            if (layout != null) saved.slots = layout.CaptureSlotSaveState();
            return saved;
        }

        public void RestoreSaveState(SaveIA01BuildPlanState saved)
        {
            if (saved == null) return;
            IA01BuildPlan plan = controller != null ? controller.BuildPlan : null;
            IA01CityLayout layout = controller != null ? controller.CityLayout : null;
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
                    SaveIA01BuildCooldownState cooldown = saved.cooldowns[i];
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

        private bool TryResolvePlacement(IA01BuildPlanStep step, IA01BuildDefinition definition, IA01CityLayout layout, out IA01BuildPlanSelection selection, out string reason)
        {
            selection = null;
            bool useAutonomous = forceAutonomous.Contains(step.StepId) || step.placementMode == IA01PlacementMode.AutonomousZone;
            if (useAutonomous)
            {
                if (!controller.AllowAutonomousExpansion)
                {
                    reason = "expansao autonoma desativada";
                    return false;
                }
                if (!layout.TryGetAutonomousZone(step.autonomousZoneId, out IA01BuildAutonomousZone zone) || !zone.IsCompatible(definition))
                {
                    reason = "zona autonoma ausente ou incompativel";
                    return false;
                }
                selection = new IA01BuildPlanSelection { Step = step, Definition = definition, Zone = zone, UsesPreparedSlot = false };
                SelectedSlotStatus = "zone:" + zone.ZoneId;
                SlotStateStatus = "Autonomous";
                reason = string.Empty;
                return true;
            }

            if (!controller.UsePreparedSlots)
            {
                reason = "slots preparados desativados";
                return false;
            }

            IA01BuildSlot slot = null;
            if (step.placementMode == IA01PlacementMode.ExactSlot)
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

            selection = new IA01BuildPlanSelection
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

        private bool EvaluateCondition(IA01BuildCondition condition, IA01BuildDefinition definition)
        {
            if (condition == null || condition.type == IA01BuildConditionType.Always) return true;
            IA01PopulationRecord population = context.GetPopulationSnapshot();
            DadosPaisGoverno country = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(context.TeamId) : null;
            switch (condition.type)
            {
                case IA01BuildConditionType.CapitalMissing: return city == null || city.Capital == null;
                case IA01BuildConditionType.RoleMissing: return CountStructures(condition.role == IA01StrategicRole.None ? definition.StrategicRole : condition.role, null) == 0;
                case IA01BuildConditionType.HousingDeficit: return population.Total > population.HousingCapacity;
                case IA01BuildConditionType.FoodBelowTarget: return country != null && country.comida < population.Total * Mathf.Max(1f, condition.target);
                case IA01BuildConditionType.EnergyBelowTarget: return country != null && country.energia < population.Total * Mathf.Max(1f, condition.target);
                case IA01BuildConditionType.StorageRequired: return CountStructures(IA01StrategicRole.Storage, null) == 0;
                case IA01BuildConditionType.Threatened: return IA01OperationalRules.IsCapitalThreatened(world, city != null ? city.Capital : null, country);
                case IA01BuildConditionType.MinimumStage: return (int)context.CurrentStage >= Mathf.RoundToInt(condition.target);
                default: return true;
            }
        }

        private int CountStructures(IA01StrategicRole role, string itemId)
        {
            IA01WorldRegistry registry = controller != null && controller.Manager != null ? controller.Manager.WorldRegistry : null;
            if (registry == null) return 0;
            if (string.IsNullOrWhiteSpace(itemId)) return registry.CountStructuresByStrategicRole(context.TeamId, role);
            IReadOnlyList<IA01WorldEntityRecord> records = registry.GetByTeam(context.TeamId);
            int count = 0;
            for (int i = 0; i < records.Count; i++)
            {
                IA01WorldEntityRecord record = records[i];
                if (record != null && record.Kind == IA01WorldEntityKind.Structure && string.Equals(record.StructureId, itemId, StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count;
        }

        private void ReconcileRestoredOccupancy(IA01CityLayout layout)
        {
            IA01BuildSlotRegistry registry = layout != null ? layout.SlotRegistry : null;
            IA01WorldRegistry worldRegistry = controller != null && controller.Manager != null ? controller.Manager.WorldRegistry : null;
            if (registry == null || worldRegistry == null) return;
            IReadOnlyCollection<IA01BuildSlot> slots = registry.GetAllSlots();
            foreach (IA01BuildSlot slot in slots)
            {
                if (slot == null || slot.State != IA01BuildSlotState.UnderConstruction || string.IsNullOrWhiteSpace(slot.ConstructedItemId)) continue;
                if (CountStructures(IA01StrategicRole.None, slot.ConstructedItemId) > 0)
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

        private static bool MatchesIntent(IA01BuildPlanStep step, IA01Intent intent)
        {
            if (step == null || intent == null) return false;
            IA01StrategicRole role = ResolveStepRole(step);
            switch (intent.Type)
            {
                case IA01IntentType.EstablishCapital: return role == IA01StrategicRole.Capital || role == IA01StrategicRole.Command || role == IA01StrategicRole.Government;
                case IA01IntentType.BuildEnergy: return role == IA01StrategicRole.EnergyProduction;
                case IA01IntentType.BuildFoodProduction: return role == IA01StrategicRole.FoodProduction;
                case IA01IntentType.BuildResidentialCapacity: return role == IA01StrategicRole.Residential;
                case IA01IntentType.BuildStorage: return role == IA01StrategicRole.Storage;
                case IA01IntentType.BuildLogistics:
                case IA01IntentType.BuildRoad: return role == IA01StrategicRole.Logistics;
                case IA01IntentType.BuildIndustry: return role == IA01StrategicRole.Industrial;
                case IA01IntentType.BuildDefense: return role == IA01StrategicRole.FixedDefense || role == IA01StrategicRole.AntiAirDefense || role == IA01StrategicRole.CoastalDefense;
                case IA01IntentType.BuildMilitaryAirport:
                    return (role == IA01StrategicRole.Airfield || role == IA01StrategicRole.Airport)
                        && StepHasAnyToken(step, "militar", "military", "base aerea", "airbase");
                case IA01IntentType.BuildCommercialAirport:
                    return (role == IA01StrategicRole.Airfield || role == IA01StrategicRole.Airport)
                        && StepHasAnyToken(step, "comercial", "commercial", "civil", "terminal");
                case IA01IntentType.BuildShipyard: return role == IA01StrategicRole.NavalBase || role == IA01StrategicRole.Shipyard || role == IA01StrategicRole.Port || role == IA01StrategicRole.Pier;
                case IA01IntentType.BuildPier: return role == IA01StrategicRole.Pier;
                case IA01IntentType.BuildOffshorePlatform: return role == IA01StrategicRole.NavalBase;
                case IA01IntentType.BuildStarterHouse: return role == IA01StrategicRole.Residential && StepHasAnyToken(step, "casa", "house");
                case IA01IntentType.BuildMediumApartment: return role == IA01StrategicRole.Residential && StepHasAnyToken(step, "medio", "médio", "apartment", "apartamento");
                case IA01IntentType.BuildHighApartment: return role == IA01StrategicRole.Residential && StepHasAnyToken(step, "hard", "alto", "high", "torre");
                case IA01IntentType.BuildMilitaryTent: return role == IA01StrategicRole.MilitaryProduction && StepHasAnyToken(step, "tenda", "tent");
                case IA01IntentType.BuildVehicleConstructor: return role == IA01StrategicRole.MilitaryProduction && StepHasAnyToken(step, "construtor", "veiculo", "veículo", "vehicle");
                default: return false;
            }
        }

        private static List<IA01BuildPlanStep> BuildDirectStepList(IA01BuildPlan plan, IA01IntentType intent)
        {
            List<IA01BuildPlanStep> matches = new List<IA01BuildPlanStep>();
            IReadOnlyList<IA01BuildPlanStep> steps = plan != null ? plan.Steps : null;
            if (steps == null) return matches;
            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
                if (StepLooksLikeIntent(step, intent))
                {
                    matches.Add(step);
                }
            }

            return matches;
        }

        private static bool StepLooksLikeIntent(IA01BuildPlanStep step, IA01IntentType intent)
        {
            if (step == null) return false;
            string text = BuildStepSearchText(step);
            switch (intent)
            {
                case IA01IntentType.BuildMilitaryAirport:
                    return text.Contains("aeroporto_militar") || text.Contains("aeroporto militar") || text.Contains("military airport") || text.Contains("base aerea militar");
                case IA01IntentType.BuildCommercialAirport:
                    return text.Contains("aeroporto_comercial") || text.Contains("aeroporto comercial") || text.Contains("commercial airport") || text.Contains("terminal civil");
                case IA01IntentType.BuildShipyard:
                    return text.Contains("estaleiro") || text.Contains("shipyard") || text.Contains("naval yard");
                case IA01IntentType.BuildPier:
                    return text.Contains("pier");
                case IA01IntentType.BuildOffshorePlatform:
                    return text.Contains("plataforma") || text.Contains("offshore");
                case IA01IntentType.BuildMilitaryTent:
                    return text.Contains("tenda") || text.Contains("tent") || text.Contains("barracks");
                case IA01IntentType.BuildVehicleConstructor:
                    return text.Contains("construtor") || text.Contains("veiculo") || text.Contains("vehicle");
                default:
                    return false;
            }
        }

        private static bool StepHasAnyToken(IA01BuildPlanStep step, params string[] tokens)
        {
            if (step == null || step.constructionData == null || tokens == null) return false;
            string text = BuildStepSearchText(step);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[i]) && text.Contains(tokens[i].ToLowerInvariant())) return true;
            }
            return false;
        }

        private static string BuildStepSearchText(IA01BuildPlanStep step)
        {
            DadosConstrucao data = step != null ? step.constructionData : null;
            return ((step != null ? step.StepId : string.Empty) + " "
                + (step != null ? step.primarySlotId : string.Empty) + " "
                + (data != null ? data.GetStableId() : string.Empty) + " "
                + (data != null ? data.GetDisplayName() : string.Empty) + " "
                + (data != null ? data.aliases : string.Empty)).ToLowerInvariant();
        }

        private static IA01StrategicRole ResolveStepRole(IA01BuildPlanStep step)
        {
            return step.requiredRole != IA01StrategicRole.None ? step.requiredRole : step.constructionData != null ? step.constructionData.strategicRole : IA01StrategicRole.None;
        }

        private void EnsurePlanIndex(IA01BuildPlan plan)
        {
            IReadOnlyList<IA01BuildPlanStep> steps = plan.Steps;
            if (ReferenceEquals(indexedPlan, plan) && indexedStepCount == steps.Count) return;
            stepsByIntent.Clear();
            indexedPlan = plan;
            indexedStepCount = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
                if (step == null) continue;
                for (int intentIndex = 0; intentIndex < ConstructionIntents.Length; intentIndex++)
                {
                    IA01IntentType intent = ConstructionIntents[intentIndex];
                    if (!MatchesIntent(step, new IA01Intent { Type = intent })) continue;
                    if (!stepsByIntent.TryGetValue(intent, out List<IA01BuildPlanStep> bucket))
                    {
                        bucket = new List<IA01BuildPlanStep>();
                        stepsByIntent.Add(intent, bucket);
                    }
                    bucket.Add(step);
                }
            }
        }

        private static bool ValidateSpecializedSlot(IA01BuildSlot slot, IA01BuildDefinition definition, out string reason)
        {
            reason = string.Empty;
            if (slot == null || definition == null) return false;
            if (definition.Domain == IA01BuildDomain.Coastal || definition.Domain == IA01BuildDomain.Water)
            {
                IA01NavalBuildSlot naval = slot.GetComponent<IA01NavalBuildSlot>();
                if (naval == null)
                {
                    reason = "slot naval especializado ausente";
                    return false;
                }
                if (!naval.TryValidateCached(out reason)) return false;
            }
            if (definition.StrategicRole == IA01StrategicRole.Airfield || definition.StrategicRole == IA01StrategicRole.Airport)
            {
                IA01AirportBuildSlot airport = slot.GetComponent<IA01AirportBuildSlot>();
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
