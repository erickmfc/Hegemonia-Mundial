#if UNITY_EDITOR
using Hegemonia.AI.IA03;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IA03EstrategaNacional))]
[CanEditMultipleObjects]
public sealed class IA03EstrategaNacionalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Estado em execução", EditorStyles.boldLabel);
        foreach (Object item in targets)
        {
            IA03EstrategaNacional estratega = item as IA03EstrategaNacional;
            if (estratega == null) continue;

            EditorGUILayout.LabelField("País", estratega.DebugNomePais);
            EditorGUILayout.LabelField("Postura", estratega.EstadoNacional + " / N" + (int)estratega.NivelDeConflito);
            EditorGUILayout.LabelField("País alvo", estratega.DebugPaisAlvoTeamId.ToString());
            EditorGUILayout.LabelField("Missão", string.IsNullOrEmpty(estratega.UltimaMissao) ? "nenhuma" : estratega.UltimaMissao);
            EditorGUILayout.LabelField("Estado da missão", estratega.EstadoDaMissao.ToString());
            EditorGUILayout.LabelField("Fila / unidades registradas", estratega.DebugMissoesNaFila + " / " + estratega.DebugUnidadesRegistradas);
            EditorGUILayout.LabelField("Saldo observado", estratega.DebugSaldo.ToString());
            EditorGUILayout.LabelField("Última análise", estratega.UltimaAnaliseEm < 0f ? "ainda não executada" : estratega.UltimaAnaliseEm.ToString("0.0") + " s");
            EditorGUILayout.LabelField("Próxima análise prevista", estratega.ProximaAnalisePrevistaEm.ToString("0.0") + " s");
            EditorGUILayout.LabelField("Última decisão", string.IsNullOrEmpty(estratega.UltimaDecisao) ? "nenhuma" : estratega.UltimaDecisao);
            EditorGUILayout.Space();
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Controles temporários de desenvolvimento", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("O nível forçado vale somente no Editor e fica ativo até Retomar diplomacia.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            DrawLevelButton("Paz", IA03NivelConflito.Paz);
            DrawLevelButton("Forçar N4", IA03NivelConflito.Tensao);
            DrawLevelButton("Forçar N3", IA03NivelConflito.AvancoMilitar);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLevelButton("Forçar N2", IA03NivelConflito.ConflitoLimitado);
            DrawLevelButton("Forçar N1", IA03NivelConflito.GuerraTotal);
            if (GUILayout.Button("Retomar diplomacia"))
            {
                ForEachTarget(estratega => estratega.DebugRetomarDiplomacia());
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawLevelButton(string label, IA03NivelConflito level)
    {
        if (GUILayout.Button(label))
        {
            ForEachTarget(estratega => estratega.DebugForcarNivel(level));
        }
    }

    private void ForEachTarget(System.Action<IA03EstrategaNacional> action)
    {
        foreach (Object item in targets)
        {
            IA03EstrategaNacional estratega = item as IA03EstrategaNacional;
            if (estratega != null) action(estratega);
        }
    }
}
#endif
