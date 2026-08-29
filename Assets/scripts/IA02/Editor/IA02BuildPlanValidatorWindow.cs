#if UNITY_EDITOR
using System.Collections.Generic;
using Hegemonia.AI.IA02;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA02.Editor
{
    public sealed class IA02BuildPlanValidatorWindow : EditorWindow
    {
        private IA02BuildPlan plan;
        private IA02CityLayout layout;
        private Vector2 scroll;
        private readonly List<string> results = new List<string>();

        [MenuItem("Window/Hegemonia/IA02 Validador de Plano")]
        private static void Open()
        {
            GetWindow<IA02BuildPlanValidatorWindow>("IA02 Validador");
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

            plan = (IA02BuildPlan)EditorGUILayout.ObjectField("Plano de construcao", plan, typeof(IA02BuildPlan), false);
            layout = (IA02CityLayout)EditorGUILayout.ObjectField("Layout da cidade", layout, typeof(IA02CityLayout), true);
            if (GUILayout.Button("Auto preencher pela IA02 da cena"))
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
            IA02Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA02Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                IA02Controller controller = controllers[i];
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
                        : controller.GetComponentInChildren<IA02CityLayout>(true);
                    changed |= layout != null;
                }

                if (plan != null && layout != null)
                {
                    break;
                }
            }

            if (layout == null)
            {
                IA02CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA02CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                    results.Add("OK: referencias preenchidas pela IA02 da cena.");
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
            IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
            IA02BuildAutonomousZone[] zones = layout.GetComponentsInChildren<IA02BuildAutonomousZone>(true);
            Dictionary<string, IA02BuildSlot> ids = new Dictionary<string, IA02BuildSlot>(System.StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IA02BuildAutonomousZone> zoneIds = new Dictionary<string, IA02BuildAutonomousZone>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slots.Length; i++)
            {
                IA02BuildSlot slot = slots[i];
                if (slot == null) continue;
                if (ShouldIgnoreSlot(slot)) continue;
                if (ids.ContainsKey(slot.SlotId)) results.Add("ERRO: id de slot duplicado '" + slot.SlotId + "'.");
                else ids.Add(slot.SlotId, slot);
                if (slot.ReservedFootprint.x <= 0f || slot.ReservedFootprint.y <= 0f) results.Add("ERRO: slot '" + slot.SlotId + "' tem footprint invalido.");
                if (slot.GetComponentInParent<IA02CityLayout>() != layout) results.Add("ERRO: slot '" + slot.SlotId + "' esta orfao deste layout.");
            }
            for (int i = 0; i < zones.Length; i++)
            {
                IA02BuildAutonomousZone zone = zones[i];
                if (zone == null) continue;
                if (zoneIds.ContainsKey(zone.ZoneId)) results.Add("ERRO: id de zona autonoma duplicado '" + zone.ZoneId + "'.");
                else zoneIds.Add(zone.ZoneId, zone);
                if (zone.WorldBounds.size.x <= 0f || zone.WorldBounds.size.z <= 0f) results.Add("ERRO: zona autonoma '" + zone.ZoneId + "' tem limites invalidos.");
            }

            IReadOnlyList<IA02BuildPlanStep> steps = plan.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                IA02BuildPlanStep step = steps[i];
                if (step == null) { results.Add("ERRO: passo nulo no indice " + i + "."); continue; }
                if (step.constructionData == null) results.Add("ERRO: passo '" + step.StepId + "' sem DadosConstrucao.");
                if (step.placementMode == IA02PlacementMode.ExactSlot)
                {
                    if (!ids.TryGetValue(step.primarySlotId ?? string.Empty, out IA02BuildSlot slot)) results.Add("ERRO: slot exato ausente para o passo '" + step.StepId + "'.");
                    else if (!IsRoleCompatible(step, slot)) results.Add("ERRO: papel incompativel entre passo '" + step.StepId + "' e slot '" + slot.SlotId + "'.");
                    else ValidateSpecializedSlot(step, slot);
                }
                else if (step.placementMode == IA02PlacementMode.SlotGroup) ValidateGroup(step, slots);
                else if (step.placementMode == IA02PlacementMode.AutonomousZone)
                {
                    if (string.IsNullOrWhiteSpace(step.autonomousZoneId)) results.Add("ERRO: zona autonoma ausente para o passo '" + step.StepId + "'.");
                    else if (!zoneIds.ContainsKey(step.autonomousZoneId)) results.Add("ERRO: zona autonoma '" + step.autonomousZoneId + "' ausente para o passo '" + step.StepId + "'.");
                }
            }

            ValidateOverlaps(slots);
            if (results.Count == 0) results.Add("OK: plano, slots e footprints basicos estao validos.");
        }

        private void ValidateOverlaps(IA02BuildSlot[] slots)
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

        private void ValidateSpecializedSlot(IA02BuildPlanStep step, IA02BuildSlot slot)
        {
            if (step == null || slot == null || step.constructionData == null) return;
            IA02BuildDomain domain = slot.AllowedDomain;
            if ((domain == IA02BuildDomain.Coastal || domain == IA02BuildDomain.Water) && slot.GetComponent<IA02NavalBuildSlot>() == null)
                results.Add("ERRO: slot naval '" + slot.SlotId + "' precisa de IA02NavalBuildSlot.");
            if ((step.requiredRole == IA02StrategicRole.Airfield || step.requiredRole == IA02StrategicRole.Airport) && slot.GetComponent<IA02AirportBuildSlot>() == null)
                results.Add("ERRO: slot de aeroporto '" + slot.SlotId + "' precisa de IA02AirportBuildSlot.");
        }

        private void ValidateGroup(IA02BuildPlanStep step, IA02BuildSlot[] slots)
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
                IA02BuildSlot slot = slots[i];
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

        private static bool IsRoleCompatible(IA02BuildPlanStep step, IA02BuildSlot slot)
        {
            return step == null || slot == null || step.requiredRole == IA02StrategicRole.None || slot.AllowedRole == IA02StrategicRole.None || slot.AllowedRole == step.requiredRole;
        }

        private static bool RequiresSpecializedSlot(IA02BuildPlanStep step)
        {
            return step != null && (step.requiredRole == IA02StrategicRole.Airfield || step.requiredRole == IA02StrategicRole.Airport);
        }

        private static bool HasRequiredSpecializedComponent(IA02BuildPlanStep step, IA02BuildSlot slot)
        {
            if (step == null || slot == null) return false;
            if ((slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water) && slot.GetComponent<IA02NavalBuildSlot>() == null) return false;
            return !RequiresSpecializedSlot(step) || slot.GetComponent<IA02AirportBuildSlot>() != null;
        }

        private static MessageType ResolveMessageType(string result)
        {
            if (result.StartsWith("ERRO")) return MessageType.Error;
            if (result.StartsWith("AVISO")) return MessageType.Warning;
            return MessageType.Info;
        }

        private static bool ShouldIgnoreSlot(IA02BuildSlot slot)
        {
            return slot != null
                && slot.AllowedRole == IA02StrategicRole.None
                && string.IsNullOrWhiteSpace(slot.SlotGroupId)
                && slot.GetComponent<IA02BuildSlotRegistry>() != null;
        }
    }
}
#endif
