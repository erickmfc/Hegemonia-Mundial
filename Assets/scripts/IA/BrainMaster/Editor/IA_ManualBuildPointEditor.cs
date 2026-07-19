using System;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    // Inspector dedicado para pontos manuais do BrainMaster.
    [CustomEditor(typeof(IA_ManualBuildPoint))]
    public sealed class IA_ManualBuildPointEditor : Editor
    {
        private SerializedProperty _itemFilters;
        private SerializedProperty _manualRole;
        private SerializedProperty _allowInactiveObject;
        private SerializedProperty _forceExactPlacement;
        private SerializedProperty _reusePoint;
        private SerializedProperty _occupiedRadius;
        private SerializedProperty _restrictToBootstrapStage;
        private SerializedProperty _bootstrapStage;
        private SerializedProperty _gizmoColor;
        private SerializedProperty _gizmoRadius;

        private void OnEnable()
        {
            _itemFilters = serializedObject.FindProperty("ItemFilters");
            _manualRole = serializedObject.FindProperty("ManualRole");
            _allowInactiveObject = serializedObject.FindProperty("AllowInactiveObject");
            _forceExactPlacement = serializedObject.FindProperty("ForceExactPlacement");
            _reusePoint = serializedObject.FindProperty("ReusePoint");
            _occupiedRadius = serializedObject.FindProperty("OccupiedRadius");
            _restrictToBootstrapStage = serializedObject.FindProperty("RestrictToBootstrapStage");
            _bootstrapStage = serializedObject.FindProperty("BootstrapStage");
            _gizmoColor = serializedObject.FindProperty("GizmoColor");
            _gizmoRadius = serializedObject.FindProperty("GizmoRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Manual Build", EditorStyles.boldLabel);
            DrawRolePopup();
            EditorGUILayout.PropertyField(_itemFilters);
            EditorGUILayout.PropertyField(_allowInactiveObject);
            EditorGUILayout.PropertyField(_forceExactPlacement);
            EditorGUILayout.PropertyField(_reusePoint);
            EditorGUILayout.PropertyField(_occupiedRadius);
            EditorGUILayout.PropertyField(_restrictToBootstrapStage);
            DrawBootstrapStagePopup();
            DrawResolutionHelp();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gizmoColor);
            EditorGUILayout.PropertyField(_gizmoRadius);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRolePopup()
        {
            IA_ManualBuildPoint.OperationalRole[] values = (IA_ManualBuildPoint.OperationalRole[])Enum.GetValues(typeof(IA_ManualBuildPoint.OperationalRole));
            string[] labels = new string[values.Length];
            int currentIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = IA_ManualBuildPoint.GetPortugueseOperationalRoleLabel(values[i]);
                if ((int)values[i] == _manualRole.intValue)
                {
                    currentIndex = i;
                }
            }

            int nextIndex = EditorGUILayout.Popup("Papel do ponto", currentIndex, labels);
            _manualRole.intValue = (int)values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        }

        private void DrawBootstrapStagePopup()
        {
            IA_BrainMaster.IA_BootstrapStage[] values = (IA_BrainMaster.IA_BootstrapStage[])Enum.GetValues(typeof(IA_BrainMaster.IA_BootstrapStage));
            string[] labels = new string[values.Length];
            int currentIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = IA_ManualBuildPoint.GetPortugueseBootstrapStageLabel(values[i]);
                if ((int)values[i] == _bootstrapStage.intValue)
                {
                    currentIndex = i;
                }
            }

            int nextIndex = EditorGUILayout.Popup("Bootstrap Stage", currentIndex, labels);
            _bootstrapStage.intValue = (int)values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        }

        private void DrawResolutionHelp()
        {
            IA_ManualBuildPoint point = (IA_ManualBuildPoint)target;
            string defaultRoleFilters = IA_ManualBuildPoint.GetDefaultFiltersForRole(point.ManualRole);
            string defaultFilters = IA_ManualBuildPoint.GetDefaultFiltersForStage(point.BootstrapStage);
            bool hasExplicitFilters = !string.IsNullOrWhiteSpace(point.ItemFilters);
            bool hasRoleFilters = !string.IsNullOrWhiteSpace(defaultRoleFilters);
            bool hasStageFilters = !string.IsNullOrWhiteSpace(defaultFilters);

            if (hasExplicitFilters)
            {
                EditorGUILayout.HelpBox("Este marcador vai casar pelo Item Filters.", MessageType.Info);
                return;
            }

            if (hasRoleFilters)
            {
                EditorGUILayout.HelpBox("Sem Item Filters, este marcador vai usar o papel '" + IA_ManualBuildPoint.GetPortugueseOperationalRoleLabel(point.ManualRole) + "': " + defaultRoleFilters, MessageType.Info);
                return;
            }

            if (hasStageFilters)
            {
                EditorGUILayout.HelpBox("Sem Item Filters, este marcador vai usar automaticamente os aliases da fase: " + defaultFilters, MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("Sem Item Filters e sem uma fase de construcao associada, este marcador nao vai casar com nenhum item automatico.", MessageType.Warning);
        }
    }
}
