#if UNITY_EDITOR
using System.Collections.Generic;
using Hegemonia.AI.IA01;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA01.Editor
{
    public sealed class IA01BuildPlanValidatorWindow : EditorWindow
    {
        private IA01BuildPlan plan;
        private IA01CityLayout layout;
        private Vector2 scroll;
        private readonly List<string> results = new List<string>();

        [MenuItem("Window/Hegemonia/IA01 Validador de Plano")]
        private static void Open()
        {
            GetWindow<IA01BuildPlanValidatorWindow>("IA01 Validador");
        }

        private void OnEnable()
        {
            AutoAssignFromScene(false);
        }

        private void OnFocus()
        {
            AutoAssignFromScene(false);
        }

        private void OnGUI()
        {
            if (plan == null || layout == null)
            {
                AutoAssignFromScene(false);
            }

            plan = (IA01BuildPlan)EditorGUILayout.ObjectField("Plano de construcao", plan, typeof(IA01BuildPlan), false);
            layout = (IA01CityLayout)EditorGUILayout.ObjectField("Layout da cidade", layout, typeof(IA01CityLayout), true);
            if (GUILayout.Button("Auto preencher pela IA01 da cena"))
            {
                AutoAssignFromScene(true);
            }

            using (new EditorGUI.DisabledScope(plan == null || layout == null))
            {
                if (GUILayout.Button("Validar")) ValidatePlan();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < results.Count; i++) EditorGUILayout.HelpBox(results[i], ResolveMessageType(results[i]));
            EditorGUILayout.EndScrollView();
        }

        private void AutoAssignFromScene(bool report)
        {
            bool changed = false;
            IA01Controller[] controllers = Resources.FindObjectsOfTypeAll<IA01Controller>();
            for (int i = 0; i < controllers.Length; i++)
            {
                IA01Controller controller = controllers[i];
                if (!IsSceneObject(controller))
                {
                    continue;
                }

                if (plan == null && controller.BuildPlan != null)
                {
                    plan = controller.BuildPlan;
                    changed = true;
                }

                if (layout == null)
                {
                    layout = controller.CityLayout != null
                        ? controller.CityLayout
                        : controller.GetComponentInChildren<IA01CityLayout>(true);
                    changed |= layout != null;
                }

                if (plan != null && layout != null)
                {
                    break;
                }
            }

            if (layout == null)
            {
                IA01CityLayout[] layouts = Resources.FindObjectsOfTypeAll<IA01CityLayout>();
                for (int i = 0; i < layouts.Length; i++)
                {
                    if (IsSceneObject(layouts[i]))
                    {
                        layout = layouts[i];
                        changed = true;
                        break;
                    }
                }
            }

            if (report)
            {
                results.Clear();
                if (plan != null && layout != null)
                {
                    results.Add("OK: referencias preenchidas pela IA01 da cena.");
                }
                else
                {
                    results.Add("AVISO: nao encontrei Build Plan e City Layout completos na cena aberta.");
                }
            }

            if (changed)
            {
                Repaint();
            }
        }

        private static bool IsSceneObject(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.scene.IsValid()
                && !EditorUtility.IsPersistent(component);
        }

        private void ValidatePlan()
        {
            results.Clear();
            IA01BuildSlot[] slots = layout.GetComponentsInChildren<IA01BuildSlot>(true);
            IA01BuildAutonomousZone[] zones = layout.GetComponentsInChildren<IA01BuildAutonomousZone>(true);
            Dictionary<string, IA01BuildSlot> ids = new Dictionary<string, IA01BuildSlot>(System.StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IA01BuildAutonomousZone> zoneIds = new Dictionary<string, IA01BuildAutonomousZone>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slots.Length; i++)
            {
                IA01BuildSlot slot = slots[i];
                if (slot == null) continue;
                if (ShouldIgnoreSlot(slot)) continue;
                if (ids.ContainsKey(slot.SlotId)) results.Add("ERRO: id de slot duplicado '" + slot.SlotId + "'.");
                else ids.Add(slot.SlotId, slot);
                if (slot.ReservedFootprint.x <= 0f || slot.ReservedFootprint.y <= 0f) results.Add("ERRO: slot '" + slot.SlotId + "' tem footprint invalido.");
                if (slot.GetComponentInParent<IA01CityLayout>() != layout) results.Add("ERRO: slot '" + slot.SlotId + "' esta orfao deste layout.");
            }
            for (int i = 0; i < zones.Length; i++)
            {
                IA01BuildAutonomousZone zone = zones[i];
                if (zone == null) continue;
                if (zoneIds.ContainsKey(zone.ZoneId)) results.Add("ERRO: id de zona autonoma duplicado '" + zone.ZoneId + "'.");
                else zoneIds.Add(zone.ZoneId, zone);
                if (zone.WorldBounds.size.x <= 0f || zone.WorldBounds.size.z <= 0f) results.Add("ERRO: zona autonoma '" + zone.ZoneId + "' tem limites invalidos.");
            }

            IReadOnlyList<IA01BuildPlanStep> steps = plan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA01BuildPlanStep step = steps[i];
                if (step == null) { results.Add("ERRO: passo nulo no indice " + i + "."); continue; }
                if (step.constructionData == null) results.Add("ERRO: passo '" + step.StepId + "' sem DadosConstrucao.");
                if (step.placementMode == IA01PlacementMode.ExactSlot)
                {
                    if (!ids.TryGetValue(step.primarySlotId ?? string.Empty, out IA01BuildSlot slot)) results.Add("ERRO: slot exato ausente para o passo '" + step.StepId + "'.");
                    else if (!IsRoleCompatible(step, slot)) results.Add("ERRO: papel incompativel entre passo '" + step.StepId + "' e slot '" + slot.SlotId + "'.");
                    else ValidateSpecializedSlot(step, slot);
                }
                else if (step.placementMode == IA01PlacementMode.SlotGroup) ValidateGroup(step, slots);
                else if (step.placementMode == IA01PlacementMode.AutonomousZone)
                {
                    if (string.IsNullOrWhiteSpace(step.autonomousZoneId)) results.Add("ERRO: zona autonoma ausente para o passo '" + step.StepId + "'.");
                    else if (!zoneIds.ContainsKey(step.autonomousZoneId)) results.Add("ERRO: zona autonoma '" + step.autonomousZoneId + "' ausente para o passo '" + step.StepId + "'.");
                }
            }

            ValidateOverlaps(slots);
            if (results.Count == 0) results.Add("OK: plano, slots e footprints basicos estao validos.");
        }

        private void ValidateOverlaps(IA01BuildSlot[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                Bounds a = new Bounds(slots[i].BuildingPoint.position, new Vector3(slots[i].ReservedFootprint.x, 2f, slots[i].ReservedFootprint.y));
                for (int j = i + 1; j < slots.Length; j++)
                {
                    if (slots[j] == null) continue;
                    if (ShouldIgnoreSlot(slots[j])) continue;
                    Bounds b = new Bounds(slots[j].BuildingPoint.position, new Vector3(slots[j].ReservedFootprint.x, 2f, slots[j].ReservedFootprint.y));
                    if (a.Intersects(b)) results.Add("AVISO: slots '" + slots[i].SlotId + "' e '" + slots[j].SlotId + "' se sobrepoem.");
                }
            }
        }

        private void ValidateSpecializedSlot(IA01BuildPlanStep step, IA01BuildSlot slot)
        {
            if (step == null || slot == null || step.constructionData == null) return;
            IA01BuildDomain domain = slot.AllowedDomain;
            if ((domain == IA01BuildDomain.Coastal || domain == IA01BuildDomain.Water) && slot.GetComponent<IA01NavalBuildSlot>() == null)
                results.Add("ERRO: slot naval '" + slot.SlotId + "' precisa de IA01NavalBuildSlot.");
            if ((step.requiredRole == IA01StrategicRole.Airfield || step.requiredRole == IA01StrategicRole.Airport) && slot.GetComponent<IA01AirportBuildSlot>() == null)
                results.Add("ERRO: slot de aeroporto '" + slot.SlotId + "' precisa de IA01AirportBuildSlot.");
        }

        private void ValidateGroup(IA01BuildPlanStep step, IA01BuildSlot[] slots)
        {
            if (string.IsNullOrWhiteSpace(step.slotGroupId))
            {
                results.Add("ERRO: grupo de slots ausente para o passo '" + step.StepId + "'.");
                return;
            }

            bool groupFound = false;
            bool compatibleFound = false;
            bool specializedFound = !RequiresSpecializedSlot(step);
            for (int i = 0; i < slots.Length; i++)
            {
                IA01BuildSlot slot = slots[i];
                if (slot == null || !string.Equals(slot.SlotGroupId, step.slotGroupId, System.StringComparison.OrdinalIgnoreCase)) continue;
                groupFound = true;
                if (!IsRoleCompatible(step, slot)) continue;
                compatibleFound = true;
                if (HasRequiredSpecializedComponent(step, slot)) specializedFound = true;
            }

            if (!groupFound) results.Add("ERRO: grupo de slots '" + step.slotGroupId + "' ausente para o passo '" + step.StepId + "'.");
            else if (!compatibleFound) results.Add("ERRO: nenhum slot compativel existe no grupo '" + step.slotGroupId + "' para o passo '" + step.StepId + "'.");
            else if (!specializedFound) results.Add("ERRO: nenhum slot especializado compativel existe no grupo '" + step.slotGroupId + "' para o passo '" + step.StepId + "'.");
        }

        private static bool IsRoleCompatible(IA01BuildPlanStep step, IA01BuildSlot slot)
        {
            return step == null || slot == null || step.requiredRole == IA01StrategicRole.None || slot.AllowedRole == IA01StrategicRole.None || slot.AllowedRole == step.requiredRole;
        }

        private static bool RequiresSpecializedSlot(IA01BuildPlanStep step)
        {
            return step != null && (step.requiredRole == IA01StrategicRole.Airfield || step.requiredRole == IA01StrategicRole.Airport);
        }

        private static bool HasRequiredSpecializedComponent(IA01BuildPlanStep step, IA01BuildSlot slot)
        {
            if (step == null || slot == null) return false;
            if ((slot.AllowedDomain == IA01BuildDomain.Coastal || slot.AllowedDomain == IA01BuildDomain.Water) && slot.GetComponent<IA01NavalBuildSlot>() == null) return false;
            return !RequiresSpecializedSlot(step) || slot.GetComponent<IA01AirportBuildSlot>() != null;
        }

        private static MessageType ResolveMessageType(string result)
        {
            if (result.StartsWith("ERRO")) return MessageType.Error;
            if (result.StartsWith("AVISO")) return MessageType.Warning;
            return MessageType.Info;
        }

        private static bool ShouldIgnoreSlot(IA01BuildSlot slot)
        {
            return slot != null
                && slot.AllowedRole == IA01StrategicRole.None
                && string.IsNullOrWhiteSpace(slot.SlotGroupId)
                && slot.GetComponent<IA01BuildSlotRegistry>() != null;
        }
    }
}
#endif
