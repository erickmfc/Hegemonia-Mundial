#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class EntrarNoMenuAoDarPlay
{
    // A campanha oficial deve sempre entrar pelo menu, inclusive no Editor.
    // Isso mantém o fluxo de teste igual ao da build e evita iniciar a IA01
    // diretamente enquanto a cena de campanha ainda esta aberta.
    private const bool ForcarMenuAoDarPlay = true;
    private const string CaminhoMenuPrincipal = "Assets/Scenes/Menu cena.unity";
    private static bool reinicioAgendado;

    static EntrarNoMenuAoDarPlay()
    {
        EditorApplication.playModeStateChanged += AoMudarEstadoDoPlay;
    }

    private static void AoMudarEstadoDoPlay(PlayModeStateChange estado)
    {
        if (!ForcarMenuAoDarPlay || estado != PlayModeStateChange.ExitingEditMode || reinicioAgendado)
        {
            return;
        }

        Scene cenaAtual = SceneManager.GetActiveScene();
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual.name))
        {
            return;
        }

        if (cenaAtual.isDirty)
        {
            Debug.LogWarning("A cena de campanha possui alteracoes nao salvas; o Play continuara nela para evitar perda de dados.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CaminhoMenuPrincipal) == null)
        {
            Debug.LogWarning("Nao foi possivel localizar a cena Menu cena para iniciar o Play.");
            return;
        }

        reinicioAgendado = true;
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += AbrirMenuEIniciarPlay;
    }

    private static void AbrirMenuEIniciarPlay()
    {
        reinicioAgendado = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorSceneManager.OpenScene(CaminhoMenuPrincipal, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
#endif
