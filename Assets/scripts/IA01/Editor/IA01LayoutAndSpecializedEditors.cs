#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA01.Editor
{
    [CustomEditor(typeof(IA01CityLayout))]
    public sealed class IA01CityLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA01CityLayout layout = (IA01CityLayout)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Este layout aceita componentes da cena: IA01BuildSlot, IA01BuildSlotRegistry e IA01BuildAutonomousZone. Fichas DadosConstrucao devem ser arrastadas para o IA01Controller ou para um IA01BuildPlan.", MessageType.Info);

            if (GUILayout.Button("Auto conectar layout"))
            {
                Undo.RecordObject(layout, "Auto conectar IA01CityLayout");
                SerializedObject so = new SerializedObject(layout);
                so.FindProperty("slotRegistry").objectReferenceValue = layout.GetComponent<IA01BuildSlotRegistry>();
                so.FindProperty("capitalSlot").objectReferenceValue = FindCapitalSlot(layout);
                SerializedProperty zones = so.FindProperty("autonomousZones");
                IA01BuildAutonomousZone[] childZones = layout.GetComponentsInChildren<IA01BuildAutonomousZone>(true);
                zones.ClearArray();
                for (int i = 0; i < childZones.Length; i++)
                {
                    zones.InsertArrayElementAtIndex(zones.arraySize);
                    zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = childZones[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(layout);
            }
        }

        private static IA01BuildSlot FindCapitalSlot(IA01CityLayout layout)
        {
            IA01BuildSlot[] slots = layout.GetComponentsInChildren<IA01BuildSlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                IA01BuildSlot slot = slots[i];
                if (slot != null && (slot.AllowedRole == IA01StrategicRole.Capital || slot.AllowedRole == IA01StrategicRole.Command || slot.AllowedRole == IA01StrategicRole.Government))
                {
                    return slot;
                }
            }

            return null;
        }
    }

    [CustomEditor(typeof(IA01NavalBuildSlot))]
    public sealed class IA01NavalBuildSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA01NavalBuildSlot naval = (IA01NavalBuildSlot)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Build Slot, Spawn e Direcao esperam componentes/Transforms da cena. A ficha DadosConstrucao nao entra aqui; ela entra no Build Plan/Controller da IA.", MessageType.Info);

            if (GUILayout.Button("Auto conectar slot naval"))
            {
                SerializedObject so = new SerializedObject(naval);
                IA01BuildSlot slot = naval.GetComponent<IA01BuildSlot>();
                so.FindProperty("buildSlot").objectReferenceValue = slot;
                if (slot != null)
                {
                    if (so.FindProperty("navalSpawnPoint").objectReferenceValue == null)
                    {
                        so.FindProperty("navalSpawnPoint").objectReferenceValue = slot.UnitSpawnPoint;
                    }
                    if (so.FindProperty("exitDirection").objectReferenceValue == null)
                    {
                        so.FindProperty("exitDirection").objectReferenceValue = slot.ExitDirection;
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(naval);
            }
        }
    }

    [CustomEditor(typeof(IA01AirportBuildSlot))]
    public sealed class IA01AirportBuildSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA01AirportBuildSlot airport = (IA01AirportBuildSlot)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Os campos deste componente recebem referencias da cena. Para a ficha de construcao, arraste DadosConstrucao no IA01Controller ou no Build Plan.", MessageType.Info);

            if (GUILayout.Button("Auto conectar slot de aeroporto"))
            {
                SerializedObject so = new SerializedObject(airport);
                IA01BuildSlot slot = airport.GetComponent<IA01BuildSlot>();
                so.FindProperty("buildSlot").objectReferenceValue = slot;
                if (slot != null)
                {
                    if (so.FindProperty("aircraftSpawn").objectReferenceValue == null)
                    {
                        so.FindProperty("aircraftSpawn").objectReferenceValue = slot.UnitSpawnPoint;
                    }
                    if (so.FindProperty("approachDirection").objectReferenceValue == null)
                    {
                        so.FindProperty("approachDirection").objectReferenceValue = slot.ExitDirection;
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(airport);
            }
        }
    }
}
#endif
