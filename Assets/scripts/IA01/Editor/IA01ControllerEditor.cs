#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hegemonia.AI.IA01.Editor
{
    [CustomEditor(typeof(IA01Controller))]
    public sealed class IA01ControllerEditor : UnityEditor.Editor
    {
        private List<string> lastMessages = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawLocalizedInspector();
            serializedObject.ApplyModifiedProperties();

            IA01Controller controller = (IA01Controller)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Plano de Construcao IA", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Agora voce pode arrastar varias fichas DadosConstrucao direto para a lista 'Fichas de construcao' abaixo ou soltar na area grande. Depois sincronize com o plano.", MessageType.Info);

            if (IA01BuildPlanEditorSupport.DrawBlueprintDropZone("Solte fichas DadosConstrucao aqui", "As fichas entram no Build Plan da IA e tentam se conectar aos slots/zones do layout.", out List<DadosConstrucao> dropped))
            {
                IA01BuildPlanEditorSupport.AppendBlueprintsToController(controller, dropped);
                IA01BuildPlan plan = IA01BuildPlanEditorSupport.EnsurePlanAsset(controller);
                lastMessages = IA01BuildPlanEditorSupport.AppendBlueprintsToPlan(plan, controller.CityLayout, dropped);
                if (plan != null)
                {
                    EditorGUIUtility.PingObject(plan);
                }
            }

            if (GUILayout.Button("Auto conectar layout e plano"))
            {
                Undo.RecordObject(controller.gameObject, "Auto conectar layout IA01");
                IA01BuildPlan plan = IA01BuildPlanEditorSupport.EnsurePlanAsset(controller);
                if (plan != null && controller.CityLayout != null)
                {
                    List<DadosConstrucao> fichas = IA01BuildPlanEditorSupport.CollectControllerBlueprints(controller);
                    if (fichas.Count == 0)
                    {
                        fichas = CollectPlanBlueprints(plan);
                    }
                    lastMessages = IA01BuildPlanEditorSupport.AppendBlueprintsToPlan(plan, controller.CityLayout, fichas);
                }
                else
                {
                    lastMessages = new List<string> { "Conecte um IA01CityLayout e pelo menos uma ficha no Build Plan para sincronizar." };
                }
            }

            if (GUILayout.Button("Sincronizar fichas com o plano"))
            {
                IA01BuildPlan plan = IA01BuildPlanEditorSupport.EnsurePlanAsset(controller);
                List<DadosConstrucao> fichas = IA01BuildPlanEditorSupport.CollectControllerBlueprints(controller);
                if (plan != null && fichas.Count > 0)
                {
                    lastMessages = IA01BuildPlanEditorSupport.AppendBlueprintsToPlan(plan, controller.CityLayout, fichas);
                    EditorGUIUtility.PingObject(plan);
                }
                else
                {
                    lastMessages = new List<string> { "Arraste pelo menos uma ficha DadosConstrucao para 'Fichas de construcao'." };
                }
            }

            IA01BuildPlanEditorSupport.DrawResultMessages(lastMessages);
        }

        private void DrawLocalizedInspector()
        {
            DrawSection("Identidade", new[]
            {
                Field("nationId", "Id da nacao"),
                Field("teamId", "Id do time"),
                Field("matchSeed", "Semente da partida"),
                Field("nationNameOverride", "Nome da nacao"),
                Field("presidentNameOverride", "Nome do presidente"),
                Field("currencyNameOverride", "Nome da moeda"),
                Field("currencySymbolOverride", "Simbolo da moeda"),
                Field("countryProfileOverride", "Perfil do pais"),
                Field("difficultyProfileOverride", "Perfil de dificuldade")
            });

            DrawSection("Perfil", new[]
            {
                Field("profileAsset", "Asset de perfil"),
                Field("createRuntimeProfileWhenMissing", "Criar perfil de runtime")
            });

            DrawSection("Modo", new[]
            {
                Field("executionModeOverride", "Modo de execucao"),
                Field("nationModeOverride", "Modo da nacao"),
                Field("stageOverride", "Estagio"),
                Field("postureOverride", "Postura")
            });

            DrawSection("Fundacao", new[]
            {
                Field("prefeituraAnchor", "Ancora da prefeitura"),
                Field("capitalBlueprint", "Ficha da capital")
            });

            DrawSection("Plano de construcao", new[]
            {
                Field("buildPlan", "Plano de construcao"),
                Field("cityLayout", "Layout da cidade"),
                Field("fichasDeConstrucao", "Fichas de construcao"),
                Field("useScriptedOpening", "Usar abertura roteirizada"),
                Field("usePreparedSlots", "Usar slots preparados"),
                Field("allowAutonomousExpansion", "Permitir expansao autonoma")
            });

            DrawSection("Runtime", new[]
            {
                Field("autoRegisterWithManager", "Registrar no manager automaticamente"),
                Field("autoApplyGovernmentSnapshot", "Aplicar snapshot do governo automaticamente"),
                Field("fallbackCadenceSeconds", "Cadencia de fallback"),
                Field("runtimeSummary", "Resumo do runtime")
            });
        }

        private void DrawSection(string title, SerializedFieldBinding[] fields)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(fields[i].PropertyName);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(fields[i].Label), true);
                }
            }
            EditorGUILayout.Space(4f);
        }

        private static SerializedFieldBinding Field(string propertyName, string label)
        {
            return new SerializedFieldBinding(propertyName, label);
        }

        private static List<DadosConstrucao> CollectPlanBlueprints(IA01BuildPlan plan)
        {
            List<DadosConstrucao> result = new List<DadosConstrucao>();
            if (plan == null || plan.Steps == null)
            {
                return result;
            }

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                IA01BuildPlanStep step = plan.Steps[i];
                if (step != null && step.constructionData != null && !result.Contains(step.constructionData))
                {
                    result.Add(step.constructionData);
                }
            }

            return result;
        }

        private readonly struct SerializedFieldBinding
        {
            public string PropertyName { get; }
            public string Label { get; }

            public SerializedFieldBinding(string propertyName, string label)
            {
                PropertyName = propertyName;
                Label = label;
            }
        }
    }
}
#endif
