#if UNITY_EDITOR
using System;
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
    // O Play Mode inicia pelo menu, mas a cena original e restaurada ao sair.
    private const bool ForcarMenuAoDarPlay = true;
    private const string CaminhoMenuPrincipal = "Assets/Scenes/Menu cena.unity";
    private const string ChaveCenaAntesDoPlay = "Hegemonia.CenaAntesDoPlay";
    private static bool reinicioAgendado;
    private static bool restaurarCenaPendente;
    private static string caminhoCenaAntesDoPlay;

    static EntrarNoMenuAoDarPlay()
    {
        caminhoCenaAntesDoPlay = SessionState.GetString(ChaveCenaAntesDoPlay, string.Empty);
        EditorApplication.playModeStateChanged += AoMudarEstadoDoPlay;
    }

    private static void AoMudarEstadoDoPlay(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingPlayMode && !reinicioAgendado
            && !string.IsNullOrWhiteSpace(caminhoCenaAntesDoPlay))
        {
            restaurarCenaPendente = true;
            return;
        }

        if (estado == PlayModeStateChange.EnteredEditMode && restaurarCenaPendente)
        {
            restaurarCenaPendente = false;
            EditorApplication.delayCall += RestaurarCenaAntesDoPlay;
            return;
        }

        if (!ForcarMenuAoDarPlay || estado != PlayModeStateChange.ExitingEditMode || reinicioAgendado)
        {
            return;
        }

        Scene cenaAtual = SceneManager.GetActiveScene();
        if (EhCenaDeBootstrapDeTeste(cenaAtual))
        {
            caminhoCenaAntesDoPlay = null;
            SessionState.EraseString(ChaveCenaAntesDoPlay);
            return;
        }

        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual.name))
        {
            caminhoCenaAntesDoPlay = null;
            SessionState.EraseString(ChaveCenaAntesDoPlay);
            return;
        }

        caminhoCenaAntesDoPlay = cenaAtual.path;
        SessionState.SetString(ChaveCenaAntesDoPlay, caminhoCenaAntesDoPlay);

        if (cenaAtual.isDirty)
        {
            // O jogo entra pelo Menu cena e carrega a campanha por caminho.
            // Sem salvar aqui, a troca de cena descartava exatamente o que foi
            // movido/removido no Hierarchy e fazia parecer que o prefab voltou
            // para a origem. Como este gancho so roda ao iniciar o Play, salvar
            // os overrides da cena e a opcao esperada para o fluxo de teste.
            if (!EditorSceneManager.SaveScene(cenaAtual))
            {
                Debug.LogError("Nao foi possivel salvar a cena de campanha antes do Play; entrada cancelada para preservar as alteracoes.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EntrarNoMenuAoDarPlay] Cena salva automaticamente antes do Play: " + cenaAtual.path);
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

    private static void RestaurarCenaAntesDoPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrWhiteSpace(caminhoCenaAntesDoPlay))
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(caminhoCenaAntesDoPlay) != null)
        {
            EditorSceneManager.OpenScene(caminhoCenaAntesDoPlay, OpenSceneMode.Single);
            Debug.Log("[EntrarNoMenuAoDarPlay] Cena restaurada apos sair do Play: " + caminhoCenaAntesDoPlay);
        }

        caminhoCenaAntesDoPlay = null;
        SessionState.EraseString(ChaveCenaAntesDoPlay);
    }

    private static bool EhCenaDeBootstrapDeTeste(Scene cena)
    {
        string caminho = cena.path != null ? cena.path.Replace('\\', '/') : string.Empty;
        return caminho.StartsWith("Assets/Tests/PlayMode/", StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith("Assets/InitTestScene", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
