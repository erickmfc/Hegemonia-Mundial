#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA02.Editor
{
    [CustomEditor(typeof(IA02CityLayout))]
    public sealed class IA02CityLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA02CityLayout layout = (IA02CityLayout)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Este layout aceita componentes da cena: IA02BuildSlot, IA02BuildSlotRegistry e IA02BuildAutonomousZone. Fichas DadosConstrucao devem ser arrastadas para o IA02Controller ou para um IA02BuildPlan.", MessageType.Info);

            if (GUILayout.Button("Auto conectar layout"))
            {
                Undo.RecordObject(layout, "Auto conectar IA02CityLayout");
                SerializedObject so = new SerializedObject(layout);
                so.FindProperty("slotRegistry").objectReferenceValue = layout.GetComponent<IA02BuildSlotRegistry>();
                so.FindProperty("capitalSlot").objectReferenceValue = FindCapitalSlot(layout);
                SerializedProperty zones = so.FindProperty("autonomousZones");
                IA02BuildAutonomousZone[] childZones = layout.GetComponentsInChildren<IA02BuildAutonomousZone>(true);
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

        private static IA02BuildSlot FindCapitalSlot(IA02CityLayout layout)
        {
            IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                IA02BuildSlot slot = slots[i];
                if (slot != null && (slot.AllowedRole == IA02StrategicRole.Capital || slot.AllowedRole == IA02StrategicRole.Command || slot.AllowedRole == IA02StrategicRole.Government))
                {
                    return slot;
                }
            }

            return null;
        }
    }

    [CustomEditor(typeof(IA02NavalBuildSlot))]
    public sealed class IA02NavalBuildSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA02NavalBuildSlot naval = (IA02NavalBuildSlot)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Build Slot, Spawn e Direcao esperam componentes/Transforms da cena. A ficha DadosConstrucao nao entra aqui; ela entra no Build Plan/Controller da IA.", MessageType.Info);

            if (GUILayout.Button("Auto conectar slot naval"))
            {
                SerializedObject so = new SerializedObject(naval);
                IA02BuildSlot slot = naval.GetComponent<IA02BuildSlot>();
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

    [CustomEditor(typeof(IA02AirportBuildSlot))]
    public sealed class IA02AirportBuildSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            IA02AirportBuildSlot airport = (IA02AirportBuildSlot)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Os campos deste componente recebem referencias da cena. Para a ficha de construcao, arraste DadosConstrucao no IA02Controller ou no Build Plan.", MessageType.Info);

            if (GUILayout.Button("Auto conectar slot de aeroporto"))
            {
                SerializedObject so = new SerializedObject(airport);
                IA02BuildSlot slot = airport.GetComponent<IA02BuildSlot>();
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
