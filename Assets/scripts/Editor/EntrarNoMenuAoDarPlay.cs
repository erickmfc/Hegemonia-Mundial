#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class EntrarNoMenuAoDarPlay
{
    // Mantem o fluxo oficial pelo menu sem interromper e reiniciar o Play Mode.
    private const string CaminhoMenuPrincipal = ConfiguracaoCenasJogo.CaminhoCenaMenuPrincipalCanonica;
    private const string CaminhoMapaGlobalRTS = "Assets/Scenes/GlobalMapRTS.unity";
    private const string CaminhoPreviewEnterprise = "Assets/Scenes/EnterpriseFlightDeckPreview.unity";

    static EntrarNoMenuAoDarPlay()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= AtualizarCenaInicialDoPlay;
        EditorSceneManager.activeSceneChangedInEditMode += AtualizarCenaInicialDoPlay;
        AtualizarCenaInicialDoPlay(default(Scene), SceneManager.GetActiveScene());
        EditorApplication.delayCall -= GarantirCenaMenuInicialNoBuild;
        EditorApplication.delayCall += GarantirCenaMenuInicialNoBuild;
    }

    private static void GarantirCenaMenuInicialNoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoMenuPrincipal) == null)
        {
            return;
        }

        List<EditorBuildSettingsScene> cenas = new List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>());

        bool primeiraJaCanonica = cenas.Count > 0
            && cenas[0] != null
            && cenas[0].enabled
            && string.Equals(cenas[0].path, CaminhoMenuPrincipal, StringComparison.OrdinalIgnoreCase);

        bool possuiLegadaNoBuild = false;
        bool possuiDuplicataCanonica = false;
        for (int i = 0; i < cenas.Count; i++)
        {
            EditorBuildSettingsScene cena = cenas[i];
            if (cena == null || string.IsNullOrWhiteSpace(cena.path))
            {
                continue;
            }

            if (string.Equals(cena.path, ConfiguracaoCenasJogo.CaminhoCenaMenuLegada, StringComparison.OrdinalIgnoreCase))
            {
                possuiLegadaNoBuild = true;
            }
            else if (i > 0 && string.Equals(cena.path, CaminhoMenuPrincipal, StringComparison.OrdinalIgnoreCase))
            {
                possuiDuplicataCanonica = true;
            }
        }

        if (primeiraJaCanonica && !possuiLegadaNoBuild && !possuiDuplicataCanonica)
        {
            return;
        }

        cenas.RemoveAll(cena => cena != null && (
            string.Equals(cena.path, CaminhoMenuPrincipal, StringComparison.OrdinalIgnoreCase)
            || string.Equals(cena.path, ConfiguracaoCenasJogo.CaminhoCenaMenuLegada, StringComparison.OrdinalIgnoreCase)));
        cenas.Insert(0, new EditorBuildSettingsScene(CaminhoMenuPrincipal, true));
        EditorBuildSettings.scenes = cenas.ToArray();
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
            || string.Equals(caminho, CaminhoMapaGlobalRTS, StringComparison.OrdinalIgnoreCase)
            || string.Equals(caminho, CaminhoPreviewEnterprise, StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith("Assets/Tests/PlayMode/", StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith("Assets/InitTestScene", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
