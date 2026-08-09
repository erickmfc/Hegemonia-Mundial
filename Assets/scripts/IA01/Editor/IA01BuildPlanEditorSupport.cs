#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA01.Editor
{
    internal static class IA01BuildPlanEditorSupport
    {
        private const string GeneratedPlanFolder = "Assets/IA01/BuildPlans";

        internal static bool DrawBlueprintDropZone(string title, string subtitle, out List<DadosConstrucao> blueprints)
        {
            blueprints = null;
            Rect dropRect = GUILayoutUtility.GetRect(0f, 68f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, title + "\n" + subtitle, EditorStyles.helpBox);

            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
            {
                return false;
            }

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return false;
            }

            List<DadosConstrucao> dragged = ExtractBlueprints(DragAndDrop.objectReferences);
            DragAndDrop.visualMode = dragged.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform && dragged.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                blueprints = dragged;
                evt.Use();
                return true;
            }

            evt.Use();
            return false;
        }

        internal static IA01BuildPlan EnsurePlanAsset(IA01Controller controller)
        {
            if (controller == null)
            {
                return null;
            }

            SerializedObject controllerObject = new SerializedObject(controller);
            SerializedProperty planProperty = controllerObject.FindProperty("buildPlan");
            IA01BuildPlan plan = planProperty != null ? planProperty.objectReferenceValue as IA01BuildPlan : null;
            if (plan != null)
            {
                return plan;
            }

            EnsureFolder(GeneratedPlanFolder);
            string safeName = SanitizeAssetName(controller.name);
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedPlanFolder + "/" + safeName + "_IA01BuildPlan.asset");
            plan = ScriptableObject.CreateInstance<IA01BuildPlan>();
            AssetDatabase.CreateAsset(plan, path);
            AssetDatabase.SaveAssets();

            if (planProperty != null)
            {
                planProperty.objectReferenceValue = plan;
                controllerObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }

            return plan;
        }

        internal static List<DadosConstrucao> CollectControllerBlueprints(IA01Controller controller)
        {
            List<DadosConstrucao> result = new List<DadosConstrucao>();
            if (controller == null)
            {
                return result;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("fichasDeConstrucao");
            if (list == null || !list.isArray)
            {
                return result;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                DadosConstrucao data = list.GetArrayElementAtIndex(i).objectReferenceValue as DadosConstrucao;
                if (data != null && !result.Contains(data))
                {
                    result.Add(data);
                }
            }

            return result;
        }

        internal static void AppendBlueprintsToController(IA01Controller controller, IReadOnlyList<DadosConstrucao> blueprints)
        {
            if (controller == null || blueprints == null || blueprints.Count == 0)
            {
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("fichasDeConstrucao");
            if (list == null || !list.isArray)
            {
                return;
            }

            for (int i = 0; i < blueprints.Count; i++)
            {
                DadosConstrucao data = blueprints[i];
                if (data == null || ContainsObjectReference(list, data))
                {
                    continue;
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            }

            TryAssignCapitalBlueprint(so, blueprints);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        internal static List<string> AppendBlueprintsToPlan(IA01BuildPlan plan, IA01CityLayout layout, IReadOnlyList<DadosConstrucao> blueprints)
        {
            List<string> results = new List<string>();
            if (plan == null || blueprints == null)
            {
                return results;
            }

            SerializedObject planObject = new SerializedObject(plan);
            SerializedProperty steps = planObject.FindProperty("steps");
            if (steps == null || !steps.isArray)
            {
                results.Add("Plano sem lista serializada de passos.");
                return results;
            }

            RemoveInvalidSteps(steps);

            for (int i = 0; i < blueprints.Count; i++)
            {
                DadosConstrucao data = blueprints[i];
                if (data == null)
                {
                    continue;
                }

                IA01StrategicRole role = ResolveRole(data);
                IA01BuildDomain domain = ResolveDomain(data, role);
                PlacementSuggestion placement = SuggestPlacement(layout, role, domain);
                if (!placement.IsValid)
                {
                    results.Add("Ignorado: " + data.GetDisplayName() + " -> " + placement.Summary);
                    continue;
                }

                int stepIndex = FindStepIndex(steps, data);
                bool isNew = stepIndex < 0;
                if (isNew)
                {
                    stepIndex = steps.arraySize;
                    steps.arraySize++;
                }

                SerializedProperty step = steps.GetArrayElementAtIndex(stepIndex);
                ResetStep(step);
                ConfigureStep(step, data, role, placement, isNew, out string placementSummary);
                results.Add((isNew ? "Adicionado: " : "Atualizado: ") + data.GetDisplayName() + " -> " + placementSummary);
            }

            planObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(plan);
            AssetDatabase.SaveAssets();
            return results;
        }

        internal static void DrawResultMessages(IReadOnlyList<string> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                EditorGUILayout.HelpBox(messages[i], MessageType.Info);
            }
        }

        private static void ConfigureStep(SerializedProperty step, DadosConstrucao data, IA01StrategicRole role, PlacementSuggestion placement, bool isNew, out string placementSummary)
        {
            SetString(step, "stepId", BuildStepId(data, role));
            SetObject(step, "constructionData", data);
            SetEnum(step, "requiredRole", role);
            SetEnum(step, "placementMode", placement.Mode);
            SetString(step, "primarySlotId", placement.PrimarySlotId);
            SetString(step, "slotGroupId", placement.SlotGroupId);
            SetString(step, "autonomousZoneId", placement.AutonomousZoneId);
            if (isNew)
            {
                SetBool(step, "required", role == IA01StrategicRole.Capital || role == IA01StrategicRole.Command || role == IA01StrategicRole.Government);
                SetInt(step, "minimumStage", 0);
                SetInt(step, "maximumCount", Mathf.Max(1, ResolveMaximumCount(role)));
                SetFloat(step, "cooldownAfterCompletion", 0f);
                SetEnum(FindRelative(step, "condition"), "type", IA01BuildConditionType.Always);
                SetEnum(step, "failurePolicy", IA01FailurePolicy.Wait);
            }

            placementSummary = placement.Summary;
        }

        private static PlacementSuggestion SuggestPlacement(IA01CityLayout layout, IA01StrategicRole role, IA01BuildDomain domain)
        {
            PlacementSuggestion suggestion = new PlacementSuggestion
            {
                Mode = IA01PlacementMode.SlotGroup,
                SlotGroupId = role != IA01StrategicRole.None ? role.ToString() : string.Empty,
                Summary = "grupo " + (role != IA01StrategicRole.None ? role.ToString() : "manual"),
                IsValid = true
            };

            if (layout == null)
            {
                suggestion.Summary = "sem layout conectado; revise o slot/grupo manualmente";
                return suggestion;
            }

            if ((role == IA01StrategicRole.Capital || role == IA01StrategicRole.Command || role == IA01StrategicRole.Government)
                && layout.CapitalSlot != null
                && IsSlotCompatible(layout.CapitalSlot, role, domain))
            {
                suggestion.Mode = IA01PlacementMode.ExactSlot;
                suggestion.PrimarySlotId = layout.CapitalSlot.SlotId;
                suggestion.SlotGroupId = layout.CapitalSlot.SlotGroupId;
                suggestion.Summary = "slot exato " + layout.CapitalSlot.SlotId;
                return suggestion;
            }

            IA01BuildSlot[] slots = layout.GetComponentsInChildren<IA01BuildSlot>(true);
            List<IA01BuildSlot> compatible = new List<IA01BuildSlot>();
            for (int i = 0; i < slots.Length; i++)
            {
                IA01BuildSlot slot = slots[i];
                if (slot != null && IsSlotCompatible(slot, role, domain))
                {
                    compatible.Add(slot);
                }
            }

            if (compatible.Count == 1)
            {
                suggestion.Mode = IA01PlacementMode.ExactSlot;
                suggestion.PrimarySlotId = compatible[0].SlotId;
                suggestion.SlotGroupId = compatible[0].SlotGroupId;
                suggestion.Summary = "slot exato " + compatible[0].SlotId;
                return suggestion;
            }

            string dominantGroup = FindDominantGroup(compatible);
            if (!string.IsNullOrWhiteSpace(dominantGroup))
            {
                suggestion.Mode = IA01PlacementMode.SlotGroup;
                suggestion.SlotGroupId = dominantGroup;
                suggestion.PrimarySlotId = string.Empty;
                suggestion.Summary = "grupo " + dominantGroup;
                return suggestion;
            }

            IA01BuildAutonomousZone[] zones = layout.GetComponentsInChildren<IA01BuildAutonomousZone>(true);
            for (int i = 0; i < zones.Length; i++)
            {
                IA01BuildAutonomousZone zone = zones[i];
                if (zone != null && IsZoneCompatible(zone, domain))
                {
                    suggestion.Mode = IA01PlacementMode.AutonomousZone;
                    suggestion.AutonomousZoneId = zone.ZoneId;
                    suggestion.PrimarySlotId = string.Empty;
                    suggestion.SlotGroupId = string.Empty;
                    suggestion.Summary = "zona autonoma " + zone.ZoneId;
                    return suggestion;
                }
            }

            if (compatible.Count > 0)
            {
                suggestion.Mode = IA01PlacementMode.ExactSlot;
                suggestion.PrimarySlotId = compatible[0].SlotId;
                suggestion.SlotGroupId = compatible[0].SlotGroupId;
                suggestion.Summary = "slot exato " + compatible[0].SlotId;
                return suggestion;
            }

            suggestion.IsValid = false;
            suggestion.Summary = "sem slot compativel no layout atual";
            return suggestion;
        }

        private static void RemoveInvalidSteps(SerializedProperty steps)
        {
            for (int i = steps.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                SerializedProperty constructionData = FindRelative(step, "constructionData");
                if (constructionData == null || constructionData.objectReferenceValue == null)
                {
                    steps.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static void ResetStep(SerializedProperty step)
        {
            SetString(step, "stepId", string.Empty);
            SetObject(step, "constructionData", null);
            SetEnum(step, "requiredRole", IA01StrategicRole.None);
            SetEnum(step, "placementMode", IA01PlacementMode.ExactSlot);
            SetString(step, "primarySlotId", string.Empty);
            SetString(step, "slotGroupId", string.Empty);
            SetString(step, "autonomousZoneId", string.Empty);
            SetBool(step, "required", false);
            SetInt(step, "minimumStage", 0);
            SetInt(step, "maximumCount", 1);
            SetFloat(step, "cooldownAfterCompletion", 0f);
            SetEnum(FindRelative(step, "condition"), "type", IA01BuildConditionType.Always);
            SetFloat(FindRelative(step, "condition"), "target", 1f);
            SetEnum(FindRelative(step, "condition"), "role", IA01StrategicRole.None);
            SetEnum(step, "failurePolicy", IA01FailurePolicy.Wait);
        }

        private static bool IsSlotCompatible(IA01BuildSlot slot, IA01StrategicRole role, IA01BuildDomain domain)
        {
            if (slot == null)
            {
                return false;
            }

            bool roleMatch = slot.AllowedRole == IA01StrategicRole.None
                || slot.AllowedRole == role
                || (slot.AllowedRole == IA01StrategicRole.Capital
                    && (role == IA01StrategicRole.Capital || role == IA01StrategicRole.Command || role == IA01StrategicRole.Government))
                || (slot.AllowedRole == IA01StrategicRole.NavalBase
                    && (role == IA01StrategicRole.NavalBase || role == IA01StrategicRole.Port || role == IA01StrategicRole.Pier || role == IA01StrategicRole.Shipyard))
                || (slot.AllowedRole == IA01StrategicRole.Airfield
                    && (role == IA01StrategicRole.Airfield || role == IA01StrategicRole.Airport));
            if (!roleMatch)
            {
                return false;
            }

            return slot.AllowedDomain == domain
                || (slot.AllowedDomain == IA01BuildDomain.Coastal && domain == IA01BuildDomain.Water)
                || (slot.AllowedDomain == IA01BuildDomain.Coastal && domain == IA01BuildDomain.Coastal)
                || (slot.AllowedDomain == IA01BuildDomain.Airfield && domain == IA01BuildDomain.Airfield);
        }

        private static bool IsZoneCompatible(IA01BuildAutonomousZone zone, IA01BuildDomain domain)
        {
            if (zone == null)
            {
                return false;
            }

            return zone.AllowedDomain == domain
                || (zone.AllowedDomain == IA01BuildDomain.Coastal && domain == IA01BuildDomain.Water);
        }

        private static string FindDominantGroup(List<IA01BuildSlot> slots)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slots.Count; i++)
            {
                string groupId = slots[i] != null ? slots[i].SlotGroupId : string.Empty;
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    continue;
                }

                counts.TryGetValue(groupId, out int count);
                counts[groupId] = count + 1;
            }

            string bestGroup = string.Empty;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    bestGroup = pair.Key;
                }
            }

            return bestGroup;
        }

        private static IA01StrategicRole ResolveRole(DadosConstrucao data)
        {
            if (data == null)
            {
                return IA01StrategicRole.None;
            }

            if (data.StrategicRole != IA01StrategicRole.None)
            {
                return data.StrategicRole;
            }

            IA_ConstructionCapability capabilities = data.GetResolvedCapabilities();
            string semantic = IA_Text.Normalize((data.GetStableId() ?? string.Empty) + " " + (data.GetDisplayName() ?? string.Empty) + " " + (data.aliases ?? string.Empty));
            if (ContainsAny(semantic, "prefeitura", "governo", "capital", "city_hall", "town_hall"))
            {
                return IA01StrategicRole.Capital;
            }
            if ((capabilities & IA_ConstructionCapability.Power) != 0) return IA01StrategicRole.EnergyProduction;
            if ((capabilities & IA_ConstructionCapability.Warehouse) != 0) return IA01StrategicRole.Storage;
            if ((capabilities & IA_ConstructionCapability.Factory) != 0) return IA01StrategicRole.Industrial;
            if ((capabilities & IA_ConstructionCapability.Barracks) != 0) return IA01StrategicRole.MilitaryProduction;
            if ((capabilities & IA_ConstructionCapability.Shipyard) != 0) return IA01StrategicRole.Shipyard;
            if ((capabilities & IA_ConstructionCapability.Pier) != 0) return IA01StrategicRole.Pier;
            if ((capabilities & IA_ConstructionCapability.Airport) != 0) return IA01StrategicRole.Airport;
            if ((capabilities & IA_ConstructionCapability.Civil) != 0) return IA01StrategicRole.Residential;
            if ((capabilities & IA_ConstructionCapability.Economy) != 0) return IA01StrategicRole.FoodProduction;
            if ((capabilities & IA_ConstructionCapability.Defense) != 0) return IA01StrategicRole.FixedDefense;

            switch (data.categoria)
            {
                case DadosConstrucao.CategoriaItem.Energia: return IA01StrategicRole.EnergyProduction;
                case DadosConstrucao.CategoriaItem.Urbana: return IA01StrategicRole.Residential;
                case DadosConstrucao.CategoriaItem.Infraestrutura: return IA01StrategicRole.Logistics;
                case DadosConstrucao.CategoriaItem.Marinha: return IA01StrategicRole.NavalBase;
                case DadosConstrucao.CategoriaItem.Aeronautica: return IA01StrategicRole.Airfield;
                default: return IA01StrategicRole.None;
            }
        }

        private static IA01BuildDomain ResolveDomain(DadosConstrucao data, IA01StrategicRole role)
        {
            if (data == null)
            {
                return IA01BuildDomain.Land;
            }

            IA_ConstructionCapability capabilities = data.GetResolvedCapabilities();
            if (role == IA01StrategicRole.Airfield || role == IA01StrategicRole.Airport || (capabilities & IA_ConstructionCapability.Airport) != 0)
            {
                return IA01BuildDomain.Airfield;
            }
            if (role == IA01StrategicRole.NavalBase
                || role == IA01StrategicRole.Port
                || role == IA01StrategicRole.Pier
                || role == IA01StrategicRole.Shipyard
                || (capabilities & IA_ConstructionCapability.Shipyard) != 0
                || (capabilities & IA_ConstructionCapability.Pier) != 0)
            {
                return IA01BuildDomain.Coastal;
            }
            if ((capabilities & IA_ConstructionCapability.Naval) != 0 && data.categoria == DadosConstrucao.CategoriaItem.Marinha)
            {
                return IA01BuildDomain.Coastal;
            }
            return IA01BuildDomain.Land;
        }

        private static int ResolveMaximumCount(IA01StrategicRole role)
        {
            switch (role)
            {
                case IA01StrategicRole.Capital:
                case IA01StrategicRole.Command:
                case IA01StrategicRole.Government:
                    return 1;
                case IA01StrategicRole.Storage:
                    return 3;
                default:
                    return 1;
            }
        }

        private static string BuildStepId(DadosConstrucao data, IA01StrategicRole role)
        {
            string stableId = data != null ? data.GetStableId() : "novo";
            string roleToken = role != IA01StrategicRole.None ? role.ToString().ToLowerInvariant() : "estrutura";
            return roleToken + "." + stableId;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = IA_Text.Normalize(tokens[i]);
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindStepIndex(SerializedProperty steps, DadosConstrucao data)
        {
            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                SerializedProperty dataProperty = FindRelative(step, "constructionData");
                if (dataProperty != null && dataProperty.objectReferenceValue == data)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ContainsObjectReference(SerializedProperty arrayProperty, UnityEngine.Object value)
        {
            if (arrayProperty == null || !arrayProperty.isArray || value == null)
            {
                return false;
            }

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryAssignCapitalBlueprint(SerializedObject controllerObject, IReadOnlyList<DadosConstrucao> blueprints)
        {
            SerializedProperty capitalProperty = controllerObject.FindProperty("capitalBlueprint");
            if (capitalProperty == null || capitalProperty.objectReferenceValue != null)
            {
                return;
            }

            for (int i = 0; i < blueprints.Count; i++)
            {
                DadosConstrucao data = blueprints[i];
                IA01StrategicRole role = ResolveRole(data);
                if (role == IA01StrategicRole.Capital || role == IA01StrategicRole.Command || role == IA01StrategicRole.Government)
                {
                    capitalProperty.objectReferenceValue = data;
                    return;
                }
            }
        }

        private static List<DadosConstrucao> ExtractBlueprints(UnityEngine.Object[] objects)
        {
            List<DadosConstrucao> result = new List<DadosConstrucao>();
            if (objects == null)
            {
                return result;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                DadosConstrucao data = objects[i] as DadosConstrucao;
                if (data != null && !result.Contains(data))
                {
                    result.Add(data);
                }
            }

            return result;
        }

        private static void EnsureFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "IA01";
            }

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string cleaned = value.Trim();
            for (int i = 0; i < invalid.Length; i++)
            {
                cleaned = cleaned.Replace(invalid[i], '_');
            }

            return cleaned.Replace(' ', '_');
        }

        private static SerializedProperty FindRelative(SerializedProperty property, string name)
        {
            return property != null ? property.FindPropertyRelative(name) : null;
        }

        private static void SetString(SerializedProperty property, string name, string value)
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.stringValue = value ?? string.Empty;
            }
        }

        private static void SetBool(SerializedProperty property, string name, bool value)
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.boolValue = value;
            }
        }

        private static void SetInt(SerializedProperty property, string name, int value)
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.intValue = value;
            }
        }

        private static void SetFloat(SerializedProperty property, string name, float value)
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.floatValue = value;
            }
        }

        private static void SetObject(SerializedProperty property, string name, UnityEngine.Object value)
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.objectReferenceValue = value;
            }
        }

        private static void SetEnum<TEnum>(SerializedProperty property, string name, TEnum value) where TEnum : Enum
        {
            SerializedProperty target = FindRelative(property, name);
            if (target != null)
            {
                target.enumValueIndex = Convert.ToInt32(value);
            }
        }

        private struct PlacementSuggestion
        {
            public IA01PlacementMode Mode;
            public string PrimarySlotId;
            public string SlotGroupId;
            public string AutonomousZoneId;
            public string Summary;
            public bool IsValid;
        }
    }
}
#endif
