#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA02.Editor
{
    [CustomPropertyDrawer(typeof(IA02BuildPlanStep))]
    public sealed class IA02BuildPlanStepDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            string titulo = "Passo do plano";
            SerializedProperty constructionData = property.FindPropertyRelative("constructionData");
            if (constructionData != null && constructionData.objectReferenceValue != null)
            {
                DadosConstrucao ficha = constructionData.objectReferenceValue as DadosConstrucao;
                if (ficha != null)
                {
                    titulo = ficha.GetDisplayName();
                }
            }

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, titulo, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = foldoutRect.yMax + Spacing;
            y = DrawField(position, property, "stepId", "Id do passo", y);
            y = DrawField(position, property, "constructionData", "Ficha de construcao", y);
            y = DrawField(position, property, "requiredRole", "Papel exigido", y);
            y = DrawField(position, property, "placementMode", "Modo de posicionamento", y);
            y = DrawField(position, property, "primarySlotId", "Id do slot principal", y);
            y = DrawField(position, property, "slotGroupId", "Id do grupo de slots", y);
            y = DrawField(position, property, "autonomousZoneId", "Id da zona autonoma", y);
            y = DrawField(position, property, "required", "Obrigatorio", y);
            y = DrawField(position, property, "minimumStage", "Estagio minimo", y);
            y = DrawField(position, property, "maximumCount", "Quantidade maxima", y);
            y = DrawField(position, property, "cooldownAfterCompletion", "Cooldown apos concluir", y);
            y = DrawConditionField(position, property, y);
            DrawField(position, property, "failurePolicy", "Politica de falha", y);
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            height += Spacing;
            height += GetFieldHeight(property, "stepId");
            height += GetFieldHeight(property, "constructionData");
            height += GetFieldHeight(property, "requiredRole");
            height += GetFieldHeight(property, "placementMode");
            height += GetFieldHeight(property, "primarySlotId");
            height += GetFieldHeight(property, "slotGroupId");
            height += GetFieldHeight(property, "autonomousZoneId");
            height += GetFieldHeight(property, "required");
            height += GetFieldHeight(property, "minimumStage");
            height += GetFieldHeight(property, "maximumCount");
            height += GetFieldHeight(property, "cooldownAfterCompletion");
            height += GetConditionHeight(property);
            height += GetFieldHeight(property, "failurePolicy");
            return height;
        }

        private static float DrawField(Rect position, SerializedProperty root, string relativeName, string label, float y)
        {
            SerializedProperty child = root.FindPropertyRelative(relativeName);
            if (child == null)
            {
                return y;
            }

            float height = IsTranslatedEnum(child) ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(child, true);
            Rect rect = new Rect(position.x, y, position.width, height);
            if (IsTranslatedEnum(child))
            {
                DrawTranslatedEnum(rect, child, label);
            }
            else
            {
                EditorGUI.PropertyField(rect, child, new GUIContent(label), true);
            }

            return rect.yMax + Spacing;
        }

        private static float GetFieldHeight(SerializedProperty root, string relativeName)
        {
            SerializedProperty child = root.FindPropertyRelative(relativeName);
            if (child == null)
            {
                return 0f;
            }

            return (IsTranslatedEnum(child) ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(child, true)) + Spacing;
        }

        private static float DrawConditionField(Rect position, SerializedProperty root, float y)
        {
            SerializedProperty condition = root.FindPropertyRelative("condition");
            if (condition == null)
            {
                return y;
            }

            Rect titleRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, "Condicao", EditorStyles.boldLabel);
            y = titleRect.yMax + Spacing;

            EditorGUI.indentLevel++;
            y = DrawField(position, condition, "type", "Tipo da condicao", y);
            y = DrawField(position, condition, "target", "Meta", y);
            y = DrawField(position, condition, "role", "Papel da condicao", y);
            EditorGUI.indentLevel--;
            return y;
        }

        private static float GetConditionHeight(SerializedProperty root)
        {
            SerializedProperty condition = root.FindPropertyRelative("condition");
            if (condition == null)
            {
                return 0f;
            }

            return EditorGUIUtility.singleLineHeight + Spacing
                   + GetFieldHeight(condition, "type")
                   + GetFieldHeight(condition, "target")
                   + GetFieldHeight(condition, "role");
        }

        private static bool IsTranslatedEnum(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return false;
            }

            return property.name == "requiredRole"
                   || property.name == "role"
                   || property.name == "placementMode"
                   || property.name == "type"
                   || property.name == "failurePolicy";
        }

        private static void DrawTranslatedEnum(Rect rect, SerializedProperty property, string label)
        {
            string[] labels = GetPortugueseLabels(property.name, property.enumNames);
            int nextIndex = EditorGUI.Popup(rect, label, Mathf.Clamp(property.enumValueIndex, 0, labels.Length - 1), labels);
            property.enumValueIndex = Mathf.Clamp(nextIndex, 0, property.enumNames.Length - 1);
        }

        private static string[] GetPortugueseLabels(string propertyName, string[] enumNames)
        {
            string[] labels = new string[enumNames.Length];
            for (int i = 0; i < enumNames.Length; i++)
            {
                labels[i] = GetPortugueseLabel(propertyName, enumNames[i]);
            }

            return labels;
        }

        private static string GetPortugueseLabel(string propertyName, string enumName)
        {
            switch (propertyName)
            {
                case "requiredRole":
                case "role":
                    return GetRoleLabel(enumName);
                case "placementMode":
                    return GetPlacementLabel(enumName);
                case "type":
                    return GetConditionLabel(enumName);
                case "failurePolicy":
                    return GetFailurePolicyLabel(enumName);
                default:
                    return ObjectNames.NicifyVariableName(enumName);
            }
        }

        private static string GetRoleLabel(string enumName)
        {
            switch (enumName)
            {
                case "None": return "Nenhum";
                case "Residential": return "Residencial";
                case "FoodProduction": return "Producao de comida";
                case "EnergyProduction": return "Producao de energia";
                case "Storage": return "Armazenamento";
                case "Logistics": return "Logistica";
                case "FixedDefense": return "Defesa fixa";
                case "AntiAirDefense": return "Defesa antiaerea";
                case "CoastalDefense": return "Defesa costeira";
                case "MilitaryProduction": return "Producao militar";
                case "Airfield": return "Aerodromo";
                case "NavalBase": return "Base naval";
                case "Command": return "Comando";
                case "Industrial": return "Industrial";
                case "Research": return "Pesquisa";
                case "Capital": return "Capital";
                case "Government": return "Governo";
                case "Pier": return "Pier";
                case "Port": return "Porto";
                case "Shipyard": return "Estaleiro";
                case "Airport": return "Aeroporto";
                default: return ObjectNames.NicifyVariableName(enumName);
            }
        }

        private static string GetPlacementLabel(string enumName)
        {
            switch (enumName)
            {
                case "ExactSlot": return "Slot exato";
                case "SlotGroup": return "Grupo de slots";
                case "AutonomousZone": return "Zona autonoma";
                default: return ObjectNames.NicifyVariableName(enumName);
            }
        }

        private static string GetConditionLabel(string enumName)
        {
            switch (enumName)
            {
                case "Always": return "Sempre";
                case "CapitalMissing": return "Capital ausente";
                case "RoleMissing": return "Papel ausente";
                case "HousingDeficit": return "Deficit habitacional";
                case "FoodBelowTarget": return "Comida abaixo da meta";
                case "EnergyBelowTarget": return "Energia abaixo da meta";
                case "StorageRequired": return "Armazenamento necessario";
                case "Threatened": return "Sob ameaca";
                case "MinimumStage": return "Estagio minimo";
                default: return ObjectNames.NicifyVariableName(enumName);
            }
        }

        private static string GetFailurePolicyLabel(string enumName)
        {
            switch (enumName)
            {
                case "Wait": return "Aguardar";
                case "TryAlternativeSlot": return "Tentar slot alternativo";
                case "UseAutonomousZone": return "Usar zona autonoma";
                case "SkipOptionalStep": return "Pular passo opcional";
                case "BlockMandatoryStep": return "Bloquear passo obrigatorio";
                default: return ObjectNames.NicifyVariableName(enumName);
            }
        }
    }
}
#endif
