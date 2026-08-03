using UnityEngine;
using UnityEngine.SceneManagement;

public static class FluxoInicialJogo
{
    private static string cenaAutorizada;
    private static bool callbacksRegistrados;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void GarantirEntradaPeloMenu()
    {
        if (!callbacksRegistrados)
        {
            SceneManager.sceneLoaded += AoCarregarCena;
            callbacksRegistrados = true;
        }

        string cenaAtual = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(cenaAtual) || ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        if (ConsumirAutorizacao(cenaAtual))
        {
            return;
        }

        // No Unity Editor, o Play deve respeitar a cena que está aberta.
        // A build continua entrando pelo menu quando inicia sem autorização.
        if (Application.isEditor)
        {
            return;
        }

        string cenaMenu = ConfiguracaoCenasJogo.ResolverCenaMenuPrincipal();

        if (ConfiguracaoCenasJogo.CenaExiste(cenaMenu))
        {
            SceneManager.LoadScene(cenaMenu);
        }
    }

    public static void AutorizarCarga(string nomeCena)
    {
        cenaAutorizada = nomeCena;
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cena.name))
        {
            if (Object.FindFirstObjectByType<MenuInicialController>() == null)
            {
                new GameObject("MenuPrincipalBootstrap").AddComponent<MenuInicialController>();
            }

            return;
        }

        if (Object.FindFirstObjectByType<MenuPausaController>() == null)
        {
            new GameObject("MenuPausaController").AddComponent<MenuPausaController>();
        }
    }

    private static bool ConsumirAutorizacao(string nomeCena)
    {
        if (string.IsNullOrWhiteSpace(cenaAutorizada))
        {
            return false;
        }

        if (cenaAutorizada != nomeCena)
        {
            return false;
        }

        cenaAutorizada = null;
        return true;
    }
}
