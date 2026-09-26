#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class EntrarNoMenuAoDarPlay
{
    // Mantem o fluxo oficial pelo menu sem interromper e reiniciar o Play Mode.
    private const string CaminhoMenuPrincipal = "Assets/_Recovery/Cena menu P.unity";

    static EntrarNoMenuAoDarPlay()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= AtualizarCenaInicialDoPlay;
        EditorSceneManager.activeSceneChangedInEditMode += AtualizarCenaInicialDoPlay;
        AtualizarCenaInicialDoPlay(default(Scene), SceneManager.GetActiveScene());
    }

    private static void AtualizarCenaInicialDoPlay(Scene cenaAnterior, Scene cenaAtual)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (DeveUsarCenaAtualComoInicio(cenaAtual))
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        SceneAsset cenaMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoMenuPrincipal);
        if (cenaMenu == null)
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.LogWarning("[EntrarNoMenuAoDarPlay] Cena do menu nao encontrada; o Play Mode usara a cena aberta.");
            return;
        }

        EditorSceneManager.playModeStartScene = cenaMenu;
    }

    private static bool DeveUsarCenaAtualComoInicio(Scene cena)
    {
        string caminho = cena.path != null ? cena.path.Replace('\\', '/') : string.Empty;
        return !cena.IsValid()
            || string.IsNullOrWhiteSpace(caminho)
            || ConfiguracaoCenasJogo.EhCenaDeMenu(cena.name)
            || caminho.StartsWith("Assets/Tests/PlayMode/", StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith("Assets/InitTestScene", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
