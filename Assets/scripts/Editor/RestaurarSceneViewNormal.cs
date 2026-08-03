#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Recupera automaticamente o Scene View quando ele fica preso em um modo de
/// diagnóstico/iluminação que causa imagem estourada ou repintura instável.
/// Não altera a cena nem os assets de materiais.
/// </summary>
[InitializeOnLoad]
public static class RestaurarSceneViewNormal
{
    static RestaurarSceneViewNormal()
    {
        EditorApplication.delayCall += Aplicar;
    }

    [MenuItem("Tools/Diagnostico/Restaurar Scene View normal")]
    public static void Aplicar()
    {
        EditorApplication.delayCall -= Aplicar;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            return;
        }

        view.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
        view.sceneLighting = false;
        view.Repaint();
    }
}
#endif
