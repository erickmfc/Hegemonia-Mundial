using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante que o primeiro carregamento do jogo passe pelo menu principal.
/// Depois que a primeira cena foi validada, as transicoes solicitadas pelo
/// proprio menu (campanha, tutorial ou save) seguem normalmente.
/// </summary>
public static class InicioMenuAplicacao
{
    private static bool primeiraCenaValidada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirMenuNaEntrada()
    {
        if (primeiraCenaValidada) return;
        primeiraCenaValidada = true;

        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual)) return;

        // No Editor, o Play deve respeitar a cena que o designer deixou aberta.
        // A entrada pelo menu continua sendo responsabilidade da primeira cena
        // do executavel e do FluxoInicialJogo quando estiver fora do Editor.
        if (Application.isEditor) return;

        string menu = ConfiguracaoCenasJogo.ResolverCenaMenuPrincipal();
        if (string.IsNullOrWhiteSpace(menu) || menu == cenaAtual) return;

        Debug.Log("[Inicio] Entrada fora do menu detectada; carregando " + menu + ".");
        SceneManager.LoadScene(menu, LoadSceneMode.Single);
    }
}
