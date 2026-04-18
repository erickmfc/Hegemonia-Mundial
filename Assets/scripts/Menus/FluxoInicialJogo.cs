using UnityEngine;
using UnityEngine.SceneManagement;

public static class FluxoInicialJogo
{
    private const string CenaMenuPrincipal = "Menu cena";
    private const string CenaMenuFallback = "MenuPrincipal";

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
        if (string.IsNullOrWhiteSpace(cenaAtual) || EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        if (ConsumirAutorizacao(cenaAtual))
        {
            return;
        }

        string cenaMenu = Application.CanStreamedLevelBeLoaded(CenaMenuPrincipal)
            ? CenaMenuPrincipal
            : CenaMenuFallback;

        if (Application.CanStreamedLevelBeLoaded(cenaMenu))
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
        if (EhCenaDeMenu(cena.name))
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

    private static bool EhCenaDeMenu(string nomeCena)
    {
        return nomeCena == CenaMenuPrincipal || nomeCena == CenaMenuFallback;
    }
}
