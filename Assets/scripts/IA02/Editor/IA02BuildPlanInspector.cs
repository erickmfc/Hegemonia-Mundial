#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA02.Editor
{
    [CustomEditor(typeof(IA02BuildPlan))]
    public sealed class IA02BuildPlanInspector : UnityEditor.Editor
    {
        private List<string> lastMessages = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawLocalizedFields();
            serializedObject.ApplyModifiedProperties();

            IA02BuildPlan plan = (IA02BuildPlan)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Importar Fichas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Arraste aqui as fichas DadosConstrucao ja criadas. O editor adiciona passos no plano e tenta inferir papel e posicionamento basico.", MessageType.Info);

            if (IA02BuildPlanEditorSupport.DrawBlueprintDropZone("Solte fichas DadosConstrucao aqui", "Os passos serao criados ou atualizados neste Build Plan.", out List<DadosConstrucao> dropped))
            {
                lastMessages = IA02BuildPlanEditorSupport.AppendBlueprintsToPlan(plan, null, dropped);
            }

            IA02BuildPlanEditorSupport.DrawResultMessages(lastMessages);
        }

        private void DrawLocalizedFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("planId"), new GUIContent("Id do plano"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layoutVersion"), new GUIContent("Versao do layout"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steps"), new GUIContent("Passos do plano"), true);
        }
    }
}
#endif
