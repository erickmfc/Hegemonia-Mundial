using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.IA01;
using Hegemonia.Cartel;

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
        // No Editor, nunca troque a cena aberta pelo usuario. Isso permite
        // testar qualquer cena diretamente sem redirecionamento automatico.
        if (Application.isEditor) return;

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
        if (EhCenaDeTestePlayMode(cena))
        {
            return;
        }

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

        GarantirSistemasDeCampanha(cena.name);
    }

    /// <summary>
    /// A cena de campanha já possui os componentes oficiais da IA e do Cartel.
    /// O menu, porém, pode ter deixado um componente persistente suspenso ou o
    /// registro pode ocorrer antes da troca efetiva da cena. Reaplica somente o
    /// ciclo de vida dos componentes existentes, sem criar unidades ou alterar
    /// o layout do mapa.
    /// </summary>
    private static void GarantirSistemasDeCampanha(string nomeCena)
    {
        IA01Manager manager = null;
        // O runner e o retorno ao menu podem deixar um IA01Manager persistente
        // enquanto a cena nova ainda desserializa o seu manager local. Sempre
        // reutilize o singleton primeiro; registrar controllers no duplicado
        // local faz a IA perder a fila quando esse objeto é destruído.
        if (Application.isPlaying)
        {
            manager = IA01Manager.Instancia;
        }

        if (manager == null)
        {
#if UNITY_2023_1_OR_NEWER
            manager = Object.FindFirstObjectByType<IA01Manager>(FindObjectsInactive.Include);
#else
            manager = Object.FindObjectOfType<IA01Manager>();
#endif
        }

        if (manager != null)
        {
            if (!manager.enabled)
            {
                manager.enabled = true;
            }

            IA01Controller[] controllers = Object.FindObjectsByType<IA01Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int controllersRegistered = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller == null) continue;
                if (!controller.enabled)
                {
                    controller.enabled = true;
                }

                manager.RegisterController(controller);
                controllersRegistered++;
            }

            CartelAIController cartel = Object.FindFirstObjectByType<CartelAIController>(FindObjectsInactive.Include);
            if (cartel != null && !cartel.enabled)
            {
                cartel.enabled = true;
            }

            Debug.Log("[Gameplay Bootstrap] cena=" + nomeCena
                + " IA01Manager=" + (manager.enabled ? "ativo" : "inativo")
                + " controllers=" + controllersRegistered
                + " registrados=" + manager.Controllers.Count
                + " Cartel=" + (cartel != null ? (cartel.enabled ? "ativo" : "inativo") : "ausente"));
            for (int i = 0; i < manager.Controllers.Count; i++)
            {
                IA01Controller bound = manager.Controllers[i];
                if (bound != null)
                {
                    Debug.Log("[Gameplay Bootstrap] controller=" + bound.name
                        + " team=" + bound.TeamId
                        + " layout=" + (bound.CityLayout != null ? bound.CityLayout.LayoutId : "ausente"));
                }
            }
            return;
        }

        CartelAIController cartelSemManager = Object.FindFirstObjectByType<CartelAIController>(FindObjectsInactive.Include);
        if (cartelSemManager != null && !cartelSemManager.enabled)
        {
            cartelSemManager.enabled = true;
        }

        Debug.LogWarning("[Gameplay Bootstrap] cena=" + nomeCena + " sem IA01Manager; Cartel=" + (cartelSemManager != null ? "presente" : "ausente"));
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

    private static bool EhCenaDeTestePlayMode(Scene cena)
    {
        string caminho = cena.path != null ? cena.path.Replace('\\', '/') : string.Empty;
        return caminho.StartsWith("Assets/Tests/PlayMode/", System.StringComparison.OrdinalIgnoreCase)
            || caminho.StartsWith("Assets/InitTestScene", System.StringComparison.OrdinalIgnoreCase);
    }
}
