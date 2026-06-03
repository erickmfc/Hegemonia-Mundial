using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Usina))]
[CanEditMultipleObjects]
public class UsinaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Lista de propriedades que queremos ESCONDER do inspector,
        // já que a Usina calcula esses valores automaticamente via código
        string[] propriedadesParaOcultar = new string[]
        {
            "m_Script",
            "producaoDinheiro",
            "producaoPetroleo",
            "producaoAco",
            "producaoEnergia"
        };

        // Desenha todas as outras propriedades normais que não estão na lista acima
        DrawPropertiesExcluding(serializedObject, propriedadesParaOcultar);

        serializedObject.ApplyModifiedProperties();
    }
}
