using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-10000)]
public class MenuInicialController : MonoBehaviour
{
    private const float VelocidadeRotacaoPlataforma = 6.5f;
    private const float VelocidadePasseAviao = 14f;
    private const float DistanciaPasseAviao = 85f;
    private const float VelocidadeJato = 0.24f;
    private const float VelocidadeNavio = 0.38f;
    private const float IntervaloDisparoTorreta = 1.2f;

    private readonly Color corPainel = new Color(0.025f, 0.055f, 0.075f, 0.96f);
    private readonly Color corPainelTopo = new Color(0.035f, 0.13f, 0.18f, 0.98f);
    private readonly Color corBorda = new Color(0.27f, 0.78f, 0.88f, 0.55f);
    private readonly Color corBotao = new Color(0.045f, 0.095f, 0.125f, 0.96f);
    private readonly Color corBotaoHover = new Color(0.08f, 0.24f, 0.29f, 0.98f);
    private readonly Color corBotaoDestaque = new Color(0.02f, 0.39f, 0.49f, 0.98f);
    private readonly Color corBotaoBloqueado = new Color(0.055f, 0.07f, 0.08f, 0.8f);
    private readonly Color corBotaoSair = new Color(0.28f, 0.075f, 0.09f, 0.96f);
    private readonly Color corTexto = new Color(0.91f, 0.98f, 0.98f, 1f);
    private readonly Color corTextoSuave = new Color(0.59f, 0.76f, 0.79f, 1f);
    private readonly Color corTextoDesabilitado = new Color(0.68f, 0.74f, 0.78f, 0.72f);
    private readonly Color corTextoAlerta = new Color(1f, 0.74f, 0.68f, 1f);
    private readonly Vector3 posicaoCameraFallback = new Vector3(13.5f, 7.4f, -18.5f);
    private readonly Vector3 alvoCameraFallback = new Vector3(8.5f, 2.7f, 4f);
    private readonly Vector3 inicioJatoFallback = new Vector3(3f, 12.5f, 20f);
    private readonly Vector3 fimJatoFallback = new Vector3(33f, 10.5f, -6f);
    private readonly Vector3 inicioJatoCenaMenu = new Vector3(-18f, 10.2f, 32f);
    private readonly Vector3 fimJatoCenaMenu = new Vector3(18f, 9.2f, 10f);
    private readonly Vector3 baseNavioFallback = new Vector3(24f, 0.55f, 28f);
    private readonly Vector3 baseNavioCenaMenu = new Vector3(16.4f, 0.2f, 30f);
    private const float CampoDeVisaoMenuPadrao = 60f;

    private Camera cameraDiorama;
    private SistemaSaveGame sistemaSave;
    private Font fontePadrao;
    private Text statusText;
    private Button botaoCarregar;
    private Canvas canvasMenuPrincipal;

    private Transform plataformaGiratoria;
    private Transform jatoPassando;
    private Transform navioDistante;
    private Transform torretaBase;
    private Transform canoTorreta;
    private Transform bocaTorreta;
    private Light flashTorreta;
    private LineRenderer linhaDisparo;
    private readonly List<Transform> avioesG18 = new List<Transform>();
    private readonly List<Vector3> posicoesOriginaisG18 = new List<Vector3>();
    private readonly List<WindZone> zonasVento = new List<WindZone>();
    private readonly List<MonoBehaviour> comportamentosSuspensos = new List<MonoBehaviour>();
    private Vector3 inicioJatoAtual;
    private Vector3 fimJatoAtual;
    private Vector3 baseNavioAtual;
    private bool usarCenaDeFundoExistente;
    private float ventoBase;

    private float progressoJato;
    private float faseNavio;
    private float tempoDisparo;
    private float duracaoDisparo;
    private float proximaLimpezaDiorama;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarBootstrapMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtiva.name))
        {
            return;
        }

        if (Object.FindFirstObjectByType<MenuInicialController>() != null)
        {
            return;
        }

        new GameObject("MenuPrincipalBootstrap").AddComponent<MenuInicialController>();
    }

    private void Awake()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtiva.name))
        {
            DesativarInterfaceEstaticaDoMenu();
            enabled = false;
            return;
        }

        usarCenaDeFundoExistente = cenaAtiva.name == ConfiguracaoCenasJogo.CenaMenuPrincipalCanonica;
        sistemaSave = SistemaSaveGame.GarantirInstancia();
        fontePadrao = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.pause = false;
        Time.timeScale = 1f;

        if (usarCenaDeFundoExistente)
        {
            DesativarControladoresCameraDaCenaMenu();
            InicializarAnimacaoCenaExistente();
            DesativarAgentesDaCenaMenu();
            // O cenário do menu usa prefabs da campanha como diorama. Eles precisam
            // permanecer visíveis, mas sem lógica de combate/movimento para não iniciar
            // uma guerra enquanto o jogador ainda está escolhendo a campanha.
            DesativarScriptsDaCenaMenu();
            PararEfeitosVisuaisDaCenaMenu();
            // RemoverMenusDeComportamentoDaCenaMenu();
            // DesativarCanvasesDaCenaMenu();
        }
    }

    private void DesativarInterfaceEstaticaDoMenu()
    {
        GameObject interfaceObject = GameObject.Find("Interface");
        if (interfaceObject != null)
        {
            // Em campanha, "Interface" nao e o menu inicial: ele contem o
            // HUD e os menus de gameplay (construcao, governo, comando,
            // pausa, mapa e atalhos). Desliga-lo fazia a build criar apenas
            // um MenuConstrucao_Auto sem referencias de catalogo.
            interfaceObject.SetActive(true);
            RectTransform rect = interfaceObject.GetComponent<RectTransform>();
            if (rect != null && rect.localScale.sqrMagnitude < 0.01f)
            {
                rect.localScale = Vector3.one;
            }
        }

        GameObject menusUi = GameObject.Find("menus ui");
        if (menusUi != null)
        {
            // Este canvas contem o menu de governo UI Toolkit e o HUD RTS.
            menusUi.SetActive(true);
        }

        GameObject menuFixo = GameObject.Find("menufixo");
        if (menuFixo != null)
        {
            menuFixo.SetActive(false);
        }
    }

    private void Start()
    {
        if (!enabled)
        {
            return;
        }

        Scene cenaAtual = SceneManager.GetActiveScene();
        if (!usarCenaDeFundoExistente && cenaAtual.name == ConfiguracaoCenasJogo.CenaMenuFallback)
        {
            string cenaMenuCanonica = ConfiguracaoCenasJogo.ResolverCenaMenuPrincipal();
            if (!string.IsNullOrWhiteSpace(cenaMenuCanonica)
                && cenaMenuCanonica != cenaAtual.name
                && ConfiguracaoCenasJogo.CenaExiste(cenaMenuCanonica))
            {
                SceneManager.LoadScene(cenaMenuCanonica);
                return;
            }
        }

        GarantirEventSystem();

        if (usarCenaDeFundoExistente)
        {
            GarantirCameraExistente();
        }
        else
        {
            GarantirCameraFallback();
            ConfigurarIluminacaoFallback();
            ConstruirDioramaFallback();
        }

        ConstruirInterface();
        AtualizarEstadoDoSave();
        DefinirStatus(LocalizationManager.T("menu.main.ready", "Campanha pronta para iniciar."), false);
    }

    private void Update()
    {
        if (!enabled)
        {
            return;
        }

        if (usarCenaDeFundoExistente)
        {
            LimparCanvasesEstranhosDaCenaMenu();
            // O diorama do menu permanece estatico; a campanha inicia os
            // controladores reais quando a cena de jogo for carregada.
            if (Time.unscaledTime >= proximaLimpezaDiorama)
            {
                DesativarScriptsDaCenaMenu();
                PararEfeitosVisuaisDaCenaMenu();
                proximaLimpezaDiorama = Time.unscaledTime + 0.5f;
            }
        }
    }

    private void OnDestroy()
    {
        RestaurarScriptsSuspensos();

        // O canvas do menu e criado em runtime como objeto raiz. Ao trocar
        // para a campanha, destrua-o explicitamente para que a interface do
        // menu nao fique sobreposta a cena de jogo.
        if (canvasMenuPrincipal != null)
        {
            Destroy(canvasMenuPrincipal.gameObject);
            canvasMenuPrincipal = null;
            botaoCarregar = null;
            statusText = null;
        }

        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void Btn_NovaCampanha()
    {
        string cenaCampanha = ConfiguracaoCenasJogo.ResolverCenaCampanhaPadrao();
        sistemaSave.IniciarNovoJogo(cenaCampanha);
        DefinirStatus(LocalizationManager.T("menu.main.loading_new", "Iniciando campanha principal..."), false);
        CarregarCena(cenaCampanha);
    }

    public void Btn_Tutorial()
    {
        string cenaTutorial = ConfiguracaoCenasJogo.ResolverCenaTutorial();
        sistemaSave.IniciarNovoJogo(cenaTutorial);
        DefinirStatus(LocalizationManager.T("menu.main.loading_tutorial", "Iniciando tutorial..."), false);
        CarregarCena(cenaTutorial);
    }

    public void Btn_CarregarJogo()
    {
        if (!sistemaSave.PossuiSave())
        {
            AtualizarEstadoDoSave();
            DefinirStatus(LocalizationManager.T("menu.main.no_save", "Nenhum save encontrado para carregar."), true);
            return;
        }

        PainelSavesUI.Abrir(canvasMenuPrincipal.transform, sistemaSave, false, CarregarSaveSelecionado);
    }

    private void CarregarSaveSelecionado(string saveId)
    {
        if (!sistemaSave.TentarCarregarSave(saveId))
        {
            DefinirStatus("Nao foi possivel carregar a partida selecionada.", true);
            return;
        }

        string cenaDestino = sistemaSave.ObterCenaSalvaOuPadrao(ConfiguracaoCenasJogo.ResolverCenaCampanhaPadrao());
        DefinirStatus(LocalizationManager.T("menu.main.loading_save", "Carregando campanha salva..."), false);
        CarregarCena(cenaDestino);
    }

    public void Btn_AlternarIdioma()
    {
        LocalizationManager.Instancia.ProximoIdioma();
        RecriarInterface();
        AtualizarEstadoDoSave();
        DefinirStatus(LocalizationManager.T("menu.main.ready", "Campanha pronta para iniciar."), false);
    }

    public void Btn_AlternarDificuldade()
    {
        GameDifficultyManager.Instancia.ProximaDificuldade();
        RecriarInterface();
        AtualizarEstadoDoSave();
        DefinirStatus(
            string.Format(
                LocalizationManager.T("menu.main.difficulty_status", "Dificuldade: {0}"),
                GameDifficultyManager.Instancia.NomeDificuldadeAtual()),
            false);
    }

    public void Btn_ModoIndisponivel(string nomeModo)
    {
        DefinirStatus(nomeModo + " ainda esta em desenvolvimento.", true);
    }

    public void Btn_Sair()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CarregarCena(string nomeCena)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        RestaurarScriptsSuspensos();

        if (!ConfiguracaoCenasJogo.CenaExiste(nomeCena))
        {
            DefinirStatus("A cena '" + nomeCena + "' nao esta no Build Settings.", true);
            return;
        }

        DestruirInterfaceMenuAntesDaTroca();
        FluxoInicialJogo.AutorizarCarga(nomeCena);
        SceneManager.LoadScene(nomeCena);
    }

    private static void DestruirInterfaceMenuAntesDaTroca()
    {
        string[] nomesInterfaces = { "CanvasMenuPrincipal", "Interface", "menus ui", "menufixo" };
        for (int i = 0; i < nomesInterfaces.Length; i++)
        {
            GameObject objeto = GameObject.Find(nomesInterfaces[i]);
            if (objeto != null)
            {
                Destroy(objeto);
            }
        }
    }

    private void GarantirEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private void GarantirCameraExistente()
    {
        cameraDiorama = ObterCameraDaCena();

        if (cameraDiorama == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraDiorama = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        AplicarConfiguracaoCameraMenu(cameraDiorama);
    }

    private void GarantirCameraFallback()
    {
        cameraDiorama = ObterCameraDaCena();

        if (cameraDiorama == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraDiorama = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        cameraDiorama.transform.position = posicaoCameraFallback;
        cameraDiorama.transform.rotation = Quaternion.LookRotation(alvoCameraFallback - posicaoCameraFallback);
        cameraDiorama.fieldOfView = CampoDeVisaoMenuPadrao;
        AplicarConfiguracaoCameraMenu(cameraDiorama);
    }

    private void ConfigurarIluminacaoFallback()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.68f, 0.73f, 0.8f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.75f, 0.8f, 0.87f, 1f);
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 75f;

        CriarLuzDirecional("SunKey", new Vector3(30f, -28f, 0f), new Color(1f, 0.93f, 0.82f, 1f), 1.15f);
        CriarLuzDirecional("FillSky", new Vector3(40f, 152f, 0f), new Color(0.56f, 0.76f, 0.98f, 1f), 0.65f);
    }

    private void CriarLuzDirecional(string nome, Vector3 rotacao, Color cor, float intensidade)
    {
        GameObject objetoLuz = new GameObject(nome);
        Light luz = objetoLuz.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.color = cor;
        luz.intensity = intensidade;
        luz.shadows = LightShadows.Soft;
        objetoLuz.transform.rotation = Quaternion.Euler(rotacao);
    }

    private void DesativarScriptsDaCenaMenu()
    {
        SuspenderGerenciadorIA01();
        Scene cenaAtiva = SceneManager.GetActiveScene();
        MonoBehaviour[] comportamentos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < comportamentos.Length; i++)
        {
            MonoBehaviour comportamento = comportamentos[i];
            if (comportamento == null || comportamento == this)
            {
                continue;
            }

            bool pertenceAoMenu = comportamento.gameObject.scene == cenaAtiva;
            if (!pertenceAoMenu && !EhControladorPersistenteDoDiorama(comportamento))
            {
                continue;
            }

            if (comportamento is EventSystem || comportamento is BaseInputModule)
            {
                continue;
            }

            string ns = comportamento.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("Unity.") || ns.StartsWith("TMPro"))
            {
                continue;
            }

            if (comportamento.enabled)
            {
                comportamento.enabled = false;
                if (!comportamentosSuspensos.Contains(comportamento))
                {
                    comportamentosSuspensos.Add(comportamento);
                }
            }
        }
    }

    private void SuspenderGerenciadorIA01()
    {
        if (!Hegemonia.AI.IA01.IA01Manager.TryGetInstance(out Hegemonia.AI.IA01.IA01Manager manager)
            || manager == null
            || !manager.enabled)
        {
            return;
        }

        manager.enabled = false;
        if (!comportamentosSuspensos.Contains(manager))
        {
            comportamentosSuspensos.Add(manager);
        }
    }

    private bool EhControladorPersistenteDoDiorama(MonoBehaviour comportamento)
    {
        string nome = comportamento.GetType().Name.ToLowerInvariant();
        return nome.Contains("ia01")
            || nome.Contains("controleunidade")
            || nome.Contains("controlenavio")
            || nome.Contains("controletorreta")
            || nome.Contains("comportamentopatrulha")
            || nome.Contains("sistemadedanos")
            || nome.Contains("projetil")
            || nome.Contains("combate")
            || nome.Contains("patrulha")
            || nome.Contains("motorunidade")
            || nome.Contains("movimento");
    }

    private void RestaurarScriptsSuspensos()
    {
        for (int i = 0; i < comportamentosSuspensos.Count; i++)
        {
            MonoBehaviour comportamento = comportamentosSuspensos[i];
            if (comportamento != null)
            {
                comportamento.enabled = true;
            }
        }

        comportamentosSuspensos.Clear();
    }

    private void DesativarAgentesDaCenaMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        NavMeshAgent[] agentes = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < agentes.Length; i++)
        {
            NavMeshAgent agente = agentes[i];
            if (agente == null || agente.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            agente.enabled = false;
        }
    }

    private void PararEfeitosVisuaisDaCenaMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();

        Animator[] animadores = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < animadores.Length; i++)
        {
            Animator animador = animadores[i];
            if (animador != null && animador.gameObject.scene == cenaAtiva)
            {
                animador.Rebind();
                animador.Update(0f);
                animador.enabled = false;
            }
        }

        ParticleSystem[] particulas = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < particulas.Length; i++)
        {
            ParticleSystem particula = particulas[i];
            if (particula != null && particula.gameObject.scene == cenaAtiva)
            {
                particula.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particula.Clear(true);
                particula.gameObject.SetActive(false);
            }
        }

        TrailRenderer[] rastros = FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rastros.Length; i++)
        {
            TrailRenderer rastro = rastros[i];
            if (rastro != null && rastro.gameObject.scene == cenaAtiva)
            {
                rastro.emitting = false;
                rastro.Clear();
            }
        }

        LineRenderer[] linhas = FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < linhas.Length; i++)
        {
            LineRenderer linha = linhas[i];
            if (linha != null && linha.gameObject.scene == cenaAtiva)
            {
                linha.enabled = false;
            }
        }

        Rigidbody[] corpos = FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < corpos.Length; i++)
        {
            Rigidbody corpo = corpos[i];
            if (corpo != null && corpo.gameObject.scene == cenaAtiva)
            {
                if (!corpo.isKinematic)
                {
                    corpo.linearVelocity = Vector3.zero;
                    corpo.angularVelocity = Vector3.zero;
                }
                corpo.isKinematic = true;
            }
        }
    }

    private void DesativarControladoresCameraDaCenaMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        CameraController[] controladores = FindObjectsByType<CameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controladores.Length; i++)
        {
            CameraController controlador = controladores[i];
            if (controlador != null && controlador.gameObject.scene == cenaAtiva)
            {
                controlador.enabled = false;
            }
        }
    }

    private void AplicarConfiguracaoCameraMenu(Camera cameraMenu)
    {
        if (cameraMenu == null)
        {
            return;
        }

        Scene cenaAtiva = SceneManager.GetActiveScene();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            bool cameraEscolhida = camera == cameraMenu;
            camera.enabled = cameraEscolhida;

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = cameraEscolhida;
            }
        }

        cameraMenu.tag = "MainCamera";
        cameraMenu.targetDisplay = 0;
        cameraMenu.depth = 0f;

        if (cameraMenu.fieldOfView < 35f || cameraMenu.fieldOfView > 95f)
        {
            cameraMenu.fieldOfView = CampoDeVisaoMenuPadrao;
        }
    }

    private void DesativarCanvasesDaCenaMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            canvas.gameObject.SetActive(false);
        }
    }

    private void RemoverMenusDeComportamentoDaCenaMenu()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        MenuComportamento[] menus = FindObjectsByType<MenuComportamento>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            MenuComportamento menu = menus[i];
            if (menu == null || menu.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            Object.Destroy(menu.gameObject);
        }

        GameObject painelComportamento = GameObject.Find("Painel_Comportamento");
        if (painelComportamento != null && painelComportamento.scene == cenaAtiva)
        {
            Object.Destroy(painelComportamento);
        }
    }

    private Camera ObterCameraDaCena()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera primeiraCamera = null;
        Camera cameraCenaMenu = null;
        Camera cameraMainTag = null;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != cenaAtiva)
            {
                continue;
            }

            if (primeiraCamera == null)
            {
                primeiraCamera = camera;
            }

            // Prioridade máxima: a câmera da cena de menu pelo nome
            if (camera.gameObject.name == "Camera cena menu" || camera.gameObject.name.ToLower().Contains("cena menu"))
            {
                cameraCenaMenu = camera;
            }

            if (camera.CompareTag("MainCamera"))
            {
                cameraMainTag = camera;
            }
        }

        // Ordem de preferência: nome específico > tag MainCamera > primeira encontrada
        return cameraCenaMenu ?? cameraMainTag ?? primeiraCamera;
    }

    private void InicializarAnimacaoCenaExistente()
    {
        avioesG18.Clear();
        posicoesOriginaisG18.Clear();
        zonasVento.Clear();

        Transform[] todos = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform transform = todos[i];
            if (transform == null)
            {
                continue;
            }

            string nome = transform.name.ToLowerInvariant();
            if (nome.Contains("g18"))
            {
                avioesG18.Add(transform);
                posicoesOriginaisG18.Add(transform.position);
            }
        }

        WindZone[] ventos = FindObjectsByType<WindZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < ventos.Length; i++)
        {
            WindZone vento = ventos[i];
            if (vento != null)
            {
                zonasVento.Add(vento);
            }
        }

        if (zonasVento.Count == 0)
        {
            GameObject ventoGo = new GameObject("MenuWindZone");
            ventoGo.transform.SetParent(null, false);
            WindZone vento = ventoGo.AddComponent<WindZone>();
            vento.mode = WindZoneMode.Directional;
            vento.windMain = 0.16f;
            vento.windTurbulence = 0.14f;
            vento.windPulseMagnitude = 0.05f;
            vento.windPulseFrequency = 0.08f;
            vento.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
            zonasVento.Add(vento);
        }

        ventoBase = 0.16f;
    }

    private void AnimarCenaExistente()
    {
        float tempo = Time.unscaledTime;
        Camera cameraAtual = cameraDiorama != null ? cameraDiorama : ObterCameraDaCena();
        Vector3 forwardCamera = cameraAtual != null ? cameraAtual.transform.forward : Vector3.forward;
        forwardCamera.y = 0f;
        if (forwardCamera.sqrMagnitude < 0.001f)
        {
            forwardCamera = Vector3.forward;
        }
        forwardCamera.Normalize();

        Vector3 rightCamera = cameraAtual != null ? cameraAtual.transform.right : Vector3.right;
        rightCamera.y = 0f;
        if (rightCamera.sqrMagnitude < 0.001f)
        {
            rightCamera = Vector3.right;
        }
        rightCamera.Normalize();
        Vector3 cameraPos = cameraAtual != null ? cameraAtual.transform.position : Vector3.zero;

        for (int i = 0; i < avioesG18.Count; i++)
        {
            Transform aviao = avioesG18[i];
            if (aviao == null)
            {
                continue;
            }

            Vector3 basePos = i < posicoesOriginaisG18.Count ? posicoesOriginaisG18[i] : aviao.position;
            float lateral = Vector3.Dot(basePos - cameraPos, rightCamera);
            float altura = basePos.y;
            float ciclo = Mathf.Repeat(tempo * (VelocidadePasseAviao / (DistanciaPasseAviao * 2f)) + i * 0.2f, 1f);
            Vector3 pontoInicial = cameraPos + forwardCamera * DistanciaPasseAviao + rightCamera * lateral;
            Vector3 pontoFinal = cameraPos - forwardCamera * DistanciaPasseAviao + rightCamera * lateral;
            pontoInicial.y = altura;
            pontoFinal.y = altura;
            aviao.position = Vector3.Lerp(pontoInicial, pontoFinal, ciclo);

            Vector3 direcao = pontoFinal - pontoInicial;
            if (direcao.sqrMagnitude > 0.001f)
            {
                aviao.rotation = Quaternion.LookRotation(direcao.normalized) * Quaternion.Euler(0f, -90f, 0f);
            }
        }

        float ventoAtual = ventoBase + Mathf.Sin(tempo * 0.45f) * 0.05f;
        for (int i = 0; i < zonasVento.Count; i++)
        {
            WindZone vento = zonasVento[i];
            if (vento == null)
            {
                continue;
            }

            vento.windMain = ventoAtual;
            vento.windTurbulence = 0.12f + Mathf.Sin(tempo * 0.33f) * 0.03f;
            vento.windPulseMagnitude = 0.03f + Mathf.Sin(tempo * 0.5f) * 0.01f;
        }
    }

    private void ConstruirDecoracaoLeve()
    {
        return;
    }

    private void ConstruirDioramaFallback()
    {
        GameObject raiz = GameObject.Find("DioramaMenuFallback");
        if (raiz != null)
        {
            Destroy(raiz);
        }

        raiz = new GameObject("DioramaMenuFallback");
        raiz.transform.position = Vector3.zero;

        baseNavioAtual = baseNavioFallback;
        inicioJatoAtual = inicioJatoFallback;
        fimJatoAtual = fimJatoFallback;

        CriarPiso(raiz.transform);
        CriarAguaEBarco(raiz.transform);
        CriarPlataformaETanque(raiz.transform);
        CriarHangar(raiz.transform, new Vector3(-7.8f, 0f, 10.5f), new Vector3(4.8f, 3.3f, 8.6f));
        CriarTorreControle(raiz.transform, new Vector3(-2.8f, 0f, 13.2f));
        CriarJatoPassando(raiz.transform);
        CriarTorreta(raiz.transform, new Vector3(18f, 0f, 24f));
    }

    private void CriarPiso(Transform parent)
    {
        GameObject pista = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pista.name = "PistaBase";
        pista.transform.SetParent(parent, false);
        pista.transform.localScale = new Vector3(4.4f, 1f, 3.6f);
        pista.GetComponent<Renderer>().material.color = new Color(0.47f, 0.49f, 0.54f, 1f);

        CriarFaixaLuminosa(parent, new Vector3(8f, 0.03f, -1.8f), new Vector3(16f, 0.035f, 0.12f));
        CriarFaixaLuminosa(parent, new Vector3(16f, 0.03f, 8.5f), new Vector3(11f, 0.035f, 0.12f));
        CriarFaixaLuminosa(parent, new Vector3(19.5f, 0.03f, 13f), new Vector3(7.5f, 0.035f, 0.12f));
    }

    private void CriarAguaEBarco(Transform parent)
    {
        if (!usarCenaDeFundoExistente)
        {
            GameObject agua = GameObject.CreatePrimitive(PrimitiveType.Plane);
            agua.name = "AguaAoFundo";
            agua.transform.SetParent(parent, false);
            agua.transform.position = new Vector3(25f, -0.2f, 28f);
            agua.transform.localScale = new Vector3(2f, 1f, 1f);
            agua.GetComponent<Renderer>().material.color = new Color(0.2f, 0.38f, 0.5f, 1f);
        }

        navioDistante = new GameObject("NavioDistante").transform;
        navioDistante.SetParent(parent, false);
        navioDistante.position = baseNavioAtual;
        navioDistante.rotation = Quaternion.Euler(0f, -18f, 0f);

        CriarBloco(navioDistante, "Casco", Vector3.zero, new Vector3(4.4f, 0.5f, 1.1f), new Color(0.28f, 0.33f, 0.37f, 1f));
        CriarBloco(navioDistante, "Conves", new Vector3(0.4f, 0.45f, 0f), new Vector3(2.2f, 0.42f, 0.85f), new Color(0.46f, 0.5f, 0.56f, 1f));
        CriarBloco(navioDistante, "Cabine", new Vector3(1.1f, 0.92f, 0f), new Vector3(0.9f, 0.55f, 0.7f), new Color(0.69f, 0.72f, 0.75f, 1f));
    }

    private void CriarPlataformaETanque(Transform parent)
    {
        plataformaGiratoria = new GameObject("PlataformaGiratoria").transform;
        plataformaGiratoria.SetParent(parent, false);
        plataformaGiratoria.position = new Vector3(9.5f, 0f, 3.5f);

        GameObject baseExterna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseExterna.name = "BaseExterna";
        baseExterna.transform.SetParent(plataformaGiratoria, false);
        baseExterna.transform.localScale = new Vector3(3.45f, 0.24f, 3.45f);
        baseExterna.GetComponent<Renderer>().material.color = new Color(0.26f, 0.3f, 0.36f, 1f);

        GameObject baseInterna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseInterna.name = "BaseInterna";
        baseInterna.transform.SetParent(plataformaGiratoria, false);
        baseInterna.transform.localPosition = new Vector3(0f, 0.32f, 0f);
        baseInterna.transform.localScale = new Vector3(2.8f, 0.17f, 2.8f);
        baseInterna.GetComponent<Renderer>().material.color = new Color(0.34f, 0.4f, 0.47f, 1f);

        CriarFaixaLuminosa(plataformaGiratoria, new Vector3(0f, 0.54f, 2.4f), new Vector3(3.6f, 0.05f, 0.12f));
        CriarFaixaLuminosa(plataformaGiratoria, new Vector3(0f, 0.54f, -2.4f), new Vector3(3.6f, 0.05f, 0.12f));

        Transform tanque = new GameObject("TanqueHeroi").transform;
        tanque.SetParent(plataformaGiratoria, false);
        tanque.localPosition = new Vector3(0f, 0.95f, 0f);
        tanque.localRotation = Quaternion.Euler(0f, -24f, 0f);

        CriarBloco(tanque, "Corpo", Vector3.zero, new Vector3(2.7f, 0.65f, 1.55f), new Color(0.38f, 0.45f, 0.35f, 1f));
        CriarBloco(tanque, "Torre", new Vector3(0.15f, 0.55f, 0f), new Vector3(1.25f, 0.52f, 1.05f), new Color(0.44f, 0.5f, 0.39f, 1f));
        CriarCilindro(tanque, "Canhao", new Vector3(1.7f, 0.6f, 0f), new Vector3(0.11f, 1.18f, 0.11f), new Vector3(0f, 0f, 90f), new Color(0.26f, 0.29f, 0.25f, 1f));
        CriarBloco(tanque, "EsteiraEsquerda", new Vector3(0f, -0.42f, -0.82f), new Vector3(2.2f, 0.42f, 0.36f), new Color(0.16f, 0.18f, 0.18f, 1f));
        CriarBloco(tanque, "EsteiraDireita", new Vector3(0f, -0.42f, 0.82f), new Vector3(2.2f, 0.42f, 0.36f), new Color(0.16f, 0.18f, 0.18f, 1f));
        CriarBloco(tanque, "FarolEsquerdo", new Vector3(1.05f, -0.04f, -0.46f), new Vector3(0.14f, 0.14f, 0.14f), corBorda);
        CriarBloco(tanque, "FarolDireito", new Vector3(1.05f, -0.04f, 0.46f), new Vector3(0.14f, 0.14f, 0.14f), corBorda);
        CriarLuzPonto(tanque, new Vector3(1.2f, 0.05f, -0.44f), corBorda, 1.2f, 9f);
        CriarLuzPonto(tanque, new Vector3(1.2f, 0.05f, 0.44f), corBorda, 1.2f, 9f);
    }

    private void CriarHangar(Transform parent, Vector3 posicao, Vector3 escala)
    {
        GameObject corpo = CriarBloco(parent, "Hangar", posicao + new Vector3(0f, escala.y * 0.5f, 0f), escala, new Color(0.43f, 0.47f, 0.53f, 1f));
        CriarBloco(corpo.transform, "Porta", new Vector3(0f, -0.25f, -escala.z * 0.48f), new Vector3(escala.x * 0.62f, escala.y * 0.7f, 0.15f), new Color(0.22f, 0.26f, 0.3f, 1f));
        CriarFaixaLuminosa(parent, posicao + new Vector3(0f, escala.y * 0.78f, -escala.z * 0.5f), new Vector3(escala.x * 0.5f, 0.05f, 0.08f));
    }

    private void CriarTorreControle(Transform parent, Vector3 posicao)
    {
        CriarBloco(parent, "BaseTorre", posicao + new Vector3(0f, 1.9f, 0f), new Vector3(1.35f, 3.8f, 1.35f), new Color(0.58f, 0.61f, 0.67f, 1f));
        CriarBloco(parent, "CabineTorre", posicao + new Vector3(0f, 4.4f, 0f), new Vector3(2.2f, 1.05f, 2.2f), new Color(0.37f, 0.42f, 0.48f, 1f));
        CriarFaixaLuminosa(parent, posicao + new Vector3(0f, 4.75f, 0.98f), new Vector3(1.1f, 0.05f, 0.08f));
    }

    private void CriarJatoPassando(Transform parent)
    {
        jatoPassando = new GameObject("JatoPassando").transform;
        jatoPassando.SetParent(parent, false);
        jatoPassando.position = inicioJatoAtual;
        jatoPassando.rotation = Quaternion.Euler(4f, -130f, 0f);

        CriarBloco(jatoPassando, "Fuselagem", Vector3.zero, new Vector3(2.8f, 0.25f, 0.38f), new Color(0.75f, 0.79f, 0.84f, 1f));
        CriarBloco(jatoPassando, "Asa", new Vector3(-0.1f, 0f, 0f), new Vector3(1.15f, 0.06f, 2.8f), new Color(0.69f, 0.73f, 0.78f, 1f));
        CriarBloco(jatoPassando, "Cauda", new Vector3(-1.08f, 0.35f, 0f), new Vector3(0.12f, 0.65f, 0.18f), new Color(0.67f, 0.7f, 0.75f, 1f));
        CriarBloco(jatoPassando, "Rastro", new Vector3(-1.95f, 0f, 0f), new Vector3(1.5f, 0.04f, 0.08f), new Color(0.92f, 0.95f, 0.98f, 0.8f));
    }

    private void CriarTorreta(Transform parent, Vector3 posicao)
    {
        torretaBase = new GameObject("TorretaDecorativa").transform;
        torretaBase.SetParent(parent, false);
        torretaBase.position = posicao;
        torretaBase.rotation = Quaternion.Euler(0f, -140f, 0f);

        CriarBloco(torretaBase, "Base", Vector3.zero, new Vector3(1f, 0.35f, 1f), new Color(0.22f, 0.24f, 0.28f, 1f));

        canoTorreta = new GameObject("CanoTorreta").transform;
        canoTorreta.SetParent(torretaBase, false);
        canoTorreta.localPosition = new Vector3(0f, 0.42f, 0f);

        CriarBloco(canoTorreta, "Cabecote", Vector3.zero, new Vector3(0.8f, 0.3f, 0.8f), new Color(0.31f, 0.34f, 0.38f, 1f));
        CriarCilindro(canoTorreta, "CanhaoEsquerdo", new Vector3(0.66f, 0.05f, -0.13f), new Vector3(0.05f, 0.72f, 0.05f), new Vector3(0f, 0f, 90f), new Color(0.18f, 0.2f, 0.21f, 1f));
        CriarCilindro(canoTorreta, "CanhaoDireito", new Vector3(0.66f, 0.05f, 0.13f), new Vector3(0.05f, 0.72f, 0.05f), new Vector3(0f, 0f, 90f), new Color(0.18f, 0.2f, 0.21f, 1f));

        bocaTorreta = new GameObject("BocaTorreta").transform;
        bocaTorreta.SetParent(canoTorreta, false);
        bocaTorreta.localPosition = new Vector3(1.34f, 0.05f, 0f);

        flashTorreta = CriarLuzPonto(bocaTorreta, Vector3.zero, new Color(1f, 0.72f, 0.28f, 1f), 0f, 7f);
        linhaDisparo = bocaTorreta.gameObject.AddComponent<LineRenderer>();
        linhaDisparo.enabled = false;
        linhaDisparo.positionCount = 2;
        linhaDisparo.widthMultiplier = 0.08f;
        linhaDisparo.useWorldSpace = true;
        linhaDisparo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linhaDisparo.receiveShadows = false;
        linhaDisparo.startColor = new Color(1f, 0.78f, 0.32f, 0.95f);
        linhaDisparo.endColor = new Color(1f, 0.36f, 0.16f, 0.1f);
        linhaDisparo.alignment = LineAlignment.View;
    }

    private void ConstruirInterface()
    {
        canvasMenuPrincipal = new GameObject("CanvasMenuPrincipal").AddComponent<Canvas>();
        canvasMenuPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasMenuPrincipal.sortingOrder = 5000;

        CanvasScaler scaler = canvasMenuPrincipal.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasMenuPrincipal.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform vinheta = CriarPainel("Vinheta", canvasMenuPrincipal.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0.025f, 0.04f, 0.12f));
        vinheta.offsetMin = Vector2.zero;
        vinheta.offsetMax = Vector2.zero;

        RectTransform painel = CriarPainel("PainelLateral", canvasMenuPrincipal.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(520f, 0f), corPainel);
        CriarPainel("FaixaTopo", painel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, 174f), corPainelTopo);
        CriarPainel("BrilhoBorda", painel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), new Vector2(3f, 0f), corBorda);
        CriarPainel("LinhaTopo", painel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 3f), new Color(0.25f, 0.9f, 0.96f, 0.95f));

        RectTransform coluna = new GameObject("ColunaMenu").AddComponent<RectTransform>();
        coluna.SetParent(painel, false);
        coluna.anchorMin = new Vector2(0f, 0f);
        coluna.anchorMax = new Vector2(1f, 1f);
        coluna.offsetMin = new Vector2(34f, 28f);
        coluna.offsetMax = new Vector2(-34f, -28f);

        CriarTexto("Selo", coluna, "CENTRO DE COMANDO  //  HGM-01", 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.35f, 0.91f, 0.94f, 0.98f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 22f));
        CriarTexto("Titulo", coluna, "HEGEMONIA\nGLOBAL", 43, FontStyle.Bold, TextAnchor.UpperLeft, corTexto, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 104f));
        CriarTexto("Subtitulo", coluna, LocalizationManager.T("menu.main.subtitle", "Estratégia, território e poder"), 16, FontStyle.Normal, TextAnchor.UpperLeft, corTextoSuave, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -122f), new Vector2(0f, 24f));
        CriarPainel("DivisorAcoes", coluna, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -158f), new Vector2(0f, 1f), new Color(0.24f, 0.68f, 0.72f, 0.32f));
        CriarTexto("SecaoAcoes", coluna, "OPERAÇÕES  /  SELECIONE UMA DIRETRIZ", 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.53f, 0.84f, 0.86f, 0.95f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -170f), new Vector2(0f, 22f));

        RectTransform grupoBotoes = new GameObject("GrupoBotoes").AddComponent<RectTransform>();
        grupoBotoes.SetParent(coluna, false);
        grupoBotoes.anchorMin = new Vector2(0f, 1f);
        grupoBotoes.anchorMax = new Vector2(1f, 1f);
        grupoBotoes.pivot = new Vector2(0.5f, 1f);
        grupoBotoes.anchoredPosition = new Vector2(0f, -202f);
        grupoBotoes.sizeDelta = new Vector2(0f, 548f);

        float posicaoY = 0f;
        CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.new", "Nova Campanha"), "CP", corBotaoDestaque, true, Btn_NovaCampanha, ref posicaoY);
        CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.tutorial", "Tutorial"), "TR", corBotao, true, Btn_Tutorial, ref posicaoY);
        CriarBotao(grupoBotoes, "Escaramuça", "SK", corBotao, false, () => Btn_ModoIndisponivel("Escaramuça"), ref posicaoY);
        CriarBotao(grupoBotoes, "Multijogador", "MP", corBotao, false, () => Btn_ModoIndisponivel("Multijogador"), ref posicaoY);
        botaoCarregar = CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.load", "Carregar Jogo"), "LD", corBotao, true, Btn_CarregarJogo, ref posicaoY);
        CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.language", "Idioma") + ": " + LocalizationManager.Instancia.NomeIdiomaAtual(), "LG", corBotao, true, Btn_AlternarIdioma, ref posicaoY);
        CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.difficulty", "Dificuldade") + ": " + GameDifficultyManager.Instancia.NomeDificuldadeAtual(), "DF", corBotao, true, Btn_AlternarDificuldade, ref posicaoY);
        CriarBotao(grupoBotoes, LocalizationManager.T("menu.main.exit", "Sair"), "EX", corBotaoSair, true, Btn_Sair, ref posicaoY);

        RectTransform statusBox = CriarPainel("StatusBox", coluna, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 112f), new Vector2(0f, 68f), new Color(0.035f, 0.13f, 0.15f, 0.96f));
        statusText = CriarTexto("Status", statusBox, string.Empty, 15, FontStyle.Bold, TextAnchor.MiddleLeft, corTextoSuave, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-32f, 0f));
        CriarTexto("Rodape", coluna, "SISTEMA ONLINE  /  ESC para pausar  /  F1 para ajuda", 11, FontStyle.Normal, TextAnchor.LowerLeft, new Color(0.42f, 0.62f, 0.65f, 0.9f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 20f), new Vector2(0f, 28f));

        CriarPainel("HUDSuperiorDireito", canvasMenuPrincipal.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(330f, 82f), new Color(0.02f, 0.07f, 0.09f, 0.72f));
        CriarTexto("Setor", canvasMenuPrincipal.transform, "ATLANTIC THEATER  //  SECTOR 07", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.5f, 0.88f, 0.88f, 0.95f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -38f), new Vector2(300f, 22f));
        CriarTexto("EstadoRede", canvasMenuPrincipal.transform, "COMMAND NETWORK   -   ONLINE", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.76f, 0.88f, 0.84f, 0.88f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -66f), new Vector2(300f, 20f));
    }

    private void RecriarInterface()
    {
        if (canvasMenuPrincipal != null)
        {
            Destroy(canvasMenuPrincipal.gameObject);
            canvasMenuPrincipal = null;
            botaoCarregar = null;
            statusText = null;
        }

        ConstruirInterface();
    }

    private RectTransform CriarPainel(string nome, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color cor)
    {
        GameObject painel = new GameObject(nome);
        painel.transform.SetParent(parent, false);

        RectTransform rect = painel.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image imagem = painel.AddComponent<Image>();
        imagem.color = cor;
        return rect;
    }

    private Text CriarTexto(string nome, Transform parent, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textoObject = new GameObject(nome);
        textoObject.transform.SetParent(parent, false);

        RectTransform rect = textoObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(
            Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
            Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text texto = textoObject.AddComponent<Text>();
        texto.text = conteudo;
        texto.font = fontePadrao;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;

        Shadow sombra = textoObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.28f);
        sombra.effectDistance = new Vector2(1f, -1f);
        return texto;
    }

    private Button CriarBotao(Transform parent, string titulo, string icone, Color corBase, bool interativo, UnityEngine.Events.UnityAction acao, ref float posicaoY)
    {
        GameObject botaoObject = new GameObject(titulo.Replace(" ", string.Empty) + "Button");
        botaoObject.transform.SetParent(parent, false);

        RectTransform rect = botaoObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -posicaoY);
        rect.sizeDelta = new Vector2(0f, 60f);

        Image fundo = botaoObject.AddComponent<Image>();
        fundo.color = interativo ? corBase : corBotaoBloqueado;
        fundo.raycastTarget = true;

        Outline borda = botaoObject.AddComponent<Outline>();
        borda.effectColor = new Color(corBorda.r, corBorda.g, corBorda.b, interativo ? 0.28f : 0.08f);
        borda.effectDistance = new Vector2(1f, -1f);

        Button botao = botaoObject.AddComponent<Button>();
        ColorBlock cores = botao.colors;
        cores.normalColor = interativo ? corBase : corBotaoBloqueado;
        cores.highlightedColor = interativo ? corBotaoHover : corBotaoBloqueado;
        cores.pressedColor = interativo ? new Color(corBase.r * 0.84f, corBase.g * 0.84f, corBase.b * 0.84f, corBase.a) : corBotaoBloqueado;
        cores.selectedColor = interativo ? corBotaoHover : corBotaoBloqueado;
        cores.disabledColor = corBotaoBloqueado;
        cores.fadeDuration = 0.08f;
        botao.colors = cores;
        botao.interactable = interativo;
        botao.onClick.AddListener(acao);

        Color corRotulo = interativo ? corTexto : corTextoDesabilitado;
        RectTransform barra = CriarPainel("Accent", botaoObject.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 0f), interativo ? new Color(0.28f, 0.91f, 0.94f, 0.95f) : new Color(0.34f, 0.38f, 0.41f, 0.55f));
        barra.SetAsFirstSibling();

        Text label = CriarTexto("Label", botaoObject.transform, titulo.ToUpper(), 17, FontStyle.Bold, TextAnchor.MiddleLeft, corRotulo, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 0f), new Vector2(-100f, 0f));
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 13;
        label.resizeTextMaxSize = 17;

        RectTransform iconBadge = CriarPainel("IconBadge", botaoObject.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(54f, 32f), new Color(0.32f, 0.85f, 0.86f, interativo ? 0.12f : 0.04f));
        Text iconText = CriarTexto("Icon", iconBadge, icone, 16, FontStyle.Bold, TextAnchor.MiddleCenter, corRotulo, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        iconText.resizeTextForBestFit = true;
        iconText.resizeTextMinSize = 12;
        iconText.resizeTextMaxSize = 16;

        posicaoY += 68f;
        return botao;
    }

    private GameObject CriarBloco(Transform parent, string nome, Vector3 posicaoLocal, Vector3 escalaLocal, Color cor)
    {
        GameObject bloco = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bloco.name = nome;
        bloco.transform.SetParent(parent, false);
        bloco.transform.localPosition = posicaoLocal;
        bloco.transform.localScale = escalaLocal;
        bloco.GetComponent<Renderer>().material.color = cor;
        return bloco;
    }

    private GameObject CriarCilindro(Transform parent, string nome, Vector3 posicaoLocal, Vector3 escalaLocal, Vector3 rotacaoLocal, Color cor)
    {
        GameObject cilindro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cilindro.name = nome;
        cilindro.transform.SetParent(parent, false);
        cilindro.transform.localPosition = posicaoLocal;
        cilindro.transform.localScale = escalaLocal;
        cilindro.transform.localRotation = Quaternion.Euler(rotacaoLocal);
        cilindro.GetComponent<Renderer>().material.color = cor;
        return cilindro;
    }

    private void CriarFaixaLuminosa(Transform parent, Vector3 posicaoLocal, Vector3 escalaLocal)
    {
        GameObject faixa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        faixa.name = "FaixaLuminosa";
        faixa.transform.SetParent(parent, false);
        faixa.transform.localPosition = posicaoLocal;
        faixa.transform.localScale = escalaLocal;
        faixa.GetComponent<Renderer>().material.color = new Color(0.38f, 0.82f, 0.98f, 1f);
    }

    private Light CriarLuzPonto(Transform parent, Vector3 posicaoLocal, Color cor, float intensidade, float alcance)
    {
        GameObject objetoLuz = new GameObject("LuzPonto");
        objetoLuz.transform.SetParent(parent, false);
        objetoLuz.transform.localPosition = posicaoLocal;

        Light luz = objetoLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = cor;
        luz.intensity = intensidade;
        luz.range = alcance;
        return luz;
    }

    private void AnimarPlataforma()
    {
        if (plataformaGiratoria != null)
        {
            plataformaGiratoria.Rotate(0f, VelocidadeRotacaoPlataforma * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }

    private void AnimarJato()
    {
        if (jatoPassando == null)
        {
            return;
        }

        progressoJato += Time.unscaledDeltaTime * VelocidadeJato;
        if (progressoJato > 1f)
        {
            progressoJato = 0f;
        }

        Vector3 posicao = Vector3.Lerp(inicioJatoAtual, fimJatoAtual, progressoJato);
        posicao.y += Mathf.Sin(progressoJato * Mathf.PI * 2f) * 0.35f;
        jatoPassando.position = posicao;

        Vector3 direcao = (fimJatoAtual - inicioJatoAtual).normalized;
        jatoPassando.rotation = Quaternion.LookRotation(direcao) * Quaternion.Euler(0f, -90f, 8f);
    }

    private void AnimarNavio()
    {
        if (navioDistante == null)
        {
            return;
        }

        faseNavio += Time.unscaledDeltaTime * VelocidadeNavio;
        float deslocamentoX = Mathf.Sin(faseNavio) * 2.4f;
        float oscilacaoY = Mathf.Sin(faseNavio * 1.7f) * 0.08f;
        navioDistante.position = baseNavioAtual + new Vector3(deslocamentoX, oscilacaoY, 0f);
        navioDistante.rotation = Quaternion.Euler(0f, -18f + Mathf.Sin(faseNavio * 0.7f) * 3f, 0f);
    }

    private void AnimarTorreta()
    {
        if (torretaBase == null || canoTorreta == null || bocaTorreta == null)
        {
            return;
        }

        float tempo = Time.unscaledTime;
        float oscilacao = Mathf.Sin(tempo * 0.9f) * 26f;
        torretaBase.rotation = Quaternion.Euler(0f, -140f + oscilacao, 0f);
        canoTorreta.localRotation = Quaternion.Euler(-6f + Mathf.Sin(tempo * 1.4f) * 4f, 0f, 0f);

        tempoDisparo += Time.unscaledDeltaTime;
        if (tempoDisparo >= IntervaloDisparoTorreta)
        {
            tempoDisparo = 0f;
            duracaoDisparo = 0.08f;
        }

        if (duracaoDisparo > 0f)
        {
            duracaoDisparo -= Time.unscaledDeltaTime;

            if (flashTorreta != null)
            {
                flashTorreta.intensity = 4.5f;
            }

            if (linhaDisparo != null)
            {
                linhaDisparo.enabled = true;
                linhaDisparo.SetPosition(0, bocaTorreta.position);
                linhaDisparo.SetPosition(1, bocaTorreta.position + torretaBase.forward * 15f + Vector3.up * 1.8f);
            }
        }
        else
        {
            if (flashTorreta != null)
            {
                flashTorreta.intensity = 0f;
            }

            if (linhaDisparo != null)
            {
                linhaDisparo.enabled = false;
            }
        }
    }

    private void AtualizarEstadoDoSave()
    {
        if (botaoCarregar == null)
        {
            return;
        }

        bool possuiSave = sistemaSave.PossuiSave();
        botaoCarregar.interactable = possuiSave;

        ColorBlock cores = botaoCarregar.colors;
        cores.normalColor = possuiSave ? corBotao : corBotaoBloqueado;
        cores.highlightedColor = possuiSave ? corBotaoHover : corBotaoBloqueado;
        cores.pressedColor = possuiSave ? new Color(corBotao.r * 0.84f, corBotao.g * 0.84f, corBotao.b * 0.84f, corBotao.a) : corBotaoBloqueado;
        cores.selectedColor = possuiSave ? corBotaoHover : corBotaoBloqueado;
        cores.disabledColor = corBotaoBloqueado;
        botaoCarregar.colors = cores;

        Text[] textos = botaoCarregar.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            textos[i].color = possuiSave ? corTexto : corTextoDesabilitado;
        }
    }

    private void LimparCanvasesEstranhosDaCenaMenu()
    {
        return;
    }

    private void DefinirStatus(string mensagem, bool alerta)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = mensagem;
        statusText.color = alerta ? corTextoAlerta : corTextoSuave;
    }
}
