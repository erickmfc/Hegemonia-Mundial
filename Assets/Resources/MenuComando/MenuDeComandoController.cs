using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ============================================================
// INTERFACE que TODO menu do jogo deve implementar.
// Isso permite que o GerenciadorMenus feche qualquer menu
// sem precisar saber o tipo concreto.
// ============================================================
public interface IMenuJogo
{
    bool EstaAberto { get; }
    void FecharMenu();
}

// ============================================================
// GERENCIADOR CENTRAL DE MENUS (singleton estático)
// Coloque este script num GameObject persistente ou deixe o
// próprio MenuDeComandoController registrar a si mesmo.
// Qualquer menu que chame GerenciadorMenus.AbrirMenu(this)
// garante que todos os outros serão fechados primeiro.
// ============================================================
public static class GerenciadorMenus
{
    private static readonly List<IMenuJogo> menus = new List<IMenuJogo>();

    public static void Registrar(IMenuJogo menu)
    {
        if (!menus.Contains(menu))
            menus.Add(menu);
    }

    public static void Desregistrar(IMenuJogo menu)
    {
        menus.Remove(menu);
    }

    /// <summary>
    /// Chame este método ANTES de abrir qualquer menu.
    /// Ele fecha todos os outros menus registrados.
    /// </summary>
    public static void AbrirMenu(IMenuJogo quemAbre)
    {
        foreach (var m in menus)
        {
            if (m != quemAbre && m.EstaAberto)
                m.FecharMenu();
        }
    }
}

// ============================================================
// CONTROLLER PRINCIPAL
// ============================================================
[RequireComponent(typeof(UIDocument))]
public class MenuDeComandoController : MonoBehaviour, IMenuJogo
{
    // ── Singleton ────────────────────────────────────────────
    private static MenuDeComandoController _instance;

    // ── UI ───────────────────────────────────────────────────
    private UIDocument     uiDocument;
    private VisualElement  rootContainer;
    private VisualElement  mapViewport;
    private VisualElement  mapOverlay;
    private VisualElement  cameraViewport;
    private Label          lblNoSignal;
    private Label          lblUnitName;
    private Label          lblUnitType;
    private Label          lblUnitSpeedAlt;
    private Label          lblUnitFuelHp;
    private Label          lblUnitTarget;
    private Button         btnClose;
    private Button         btnFollow;
    private Button         btnStop;

    // ── Cameras / RenderTextures ─────────────────────────────
    private Camera         cameraMapaTatico;
    private Camera         unitPreviewCamera;
    private RenderTexture  rtMapaTatico;
    private RenderTexture  rtUnitCam;

    // ── Configuração ─────────────────────────────────────────
    private float tamanhoTerrenoTatico = 4000f;

    // ── Estado ───────────────────────────────────────────────
    private bool menuAberto          = false;
    public  bool EstaAberto          => menuAberto;        // implementa IMenuJogo
    private bool isFollowingCamera   = false;
    private Vector3 cameraOffset     = new Vector3(0f, 50f, -60f);

    // ── Referências de jogo ──────────────────────────────────
    private GerenteSelecao  gerenteSelecao;
    private ControleUnidade targetUnit;

    // ── Timer overlay ────────────────────────────────────────
    private float timerMapOverlay            = 0f;
    private const float INTERVALO_MAP_OVERLAY = 0.2f;

    // ─────────────────────────────────────────────────────────
    // AUTO-INICIALIZAÇÃO
    // ─────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInicializar()
    {
        if (FindObjectOfType<MenuDeComandoController>() != null) return;

        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("MenuComando/menucomando");
        if (uxml == null)
        {
            Debug.LogError("[MenuComando] UXML não encontrado em Resources/MenuComando/menucomando. " +
                           "Verifique se o arquivo está na pasta certa e que a pasta 'Resources' existe.");
            return;
        }

        PanelSettings ps = Resources.Load<PanelSettings>("PanelSettings");
        if (ps == null)
        {
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.sortingOrder = 90;
        }

        GameObject go = new GameObject("[HUD_MenuComando]");
        DontDestroyOnLoad(go);

        UIDocument doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = uxml;

        go.AddComponent<MenuDeComandoController>();
    }

    // ─────────────────────────────────────────────────────────
    // AWAKE
    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Destrói objeto legado de cena, se existir
        GameObject cenaAntiga = GameObject.Find("menu de comando");
        if (cenaAntiga != null) Destroy(cenaAntiga);

        uiDocument = GetComponent<UIDocument>();

        // Registra no gerenciador central de menus
        GerenciadorMenus.Registrar(this);

        // Cria as câmeras programaticamente
        ConfigurarCameras();
    }

    // ─────────────────────────────────────────────────────────
    // CONFIGURAÇÃO DAS CÂMERAS
    // CORREÇÃO: câmeras só são criadas uma vez e guardadas como
    // filhas do próprio GO para sobreviver ao DontDestroyOnLoad.
    // ─────────────────────────────────────────────────────────
    private void ConfigurarCameras()
    {
        // --- MAPA TÁTICO (ortográfica, top-down) ---
        GameObject mapCamGO = new GameObject("Cam_MapaTatico");
        mapCamGO.transform.SetParent(transform);          // ← filho do menu GO
        cameraMapaTatico                     = mapCamGO.AddComponent<Camera>();
        cameraMapaTatico.orthographic        = true;
        cameraMapaTatico.clearFlags          = CameraClearFlags.SolidColor;
        cameraMapaTatico.backgroundColor     = new Color(0.08f, 0.12f, 0.18f, 1f);
        cameraMapaTatico.farClipPlane        = 5000f;
        cameraMapaTatico.nearClipPlane       = 0.3f;
        cameraMapaTatico.depth               = -10;

        // Remove camadas que não interessam
        ExcluirLayer(cameraMapaTatico, "UI");
        ExcluirLayer(cameraMapaTatico, "Efeitos");
        ExcluirLayer(cameraMapaTatico, "Water");

        // Posição padrão; será ajustada no Start() quando tivermos o GerenciadorQuartel
        cameraMapaTatico.transform.position  = new Vector3(0f, 1500f, 0f);
        cameraMapaTatico.transform.rotation  = Quaternion.Euler(90f, 0f, 0f);
        cameraMapaTatico.orthographicSize    = tamanhoTerrenoTatico / 2f;

        rtMapaTatico = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        rtMapaTatico.Create();
        cameraMapaTatico.targetTexture       = rtMapaTatico;
        cameraMapaTatico.enabled             = false;       // liga só quando o menu abre

        // --- CÂMERA DE UNIDADE (perspectiva, segue a unidade selecionada) ---
        GameObject unitCamGO = new GameObject("Cam_Unidade");
        unitCamGO.transform.SetParent(transform);           // ← filho do menu GO
        unitPreviewCamera                    = unitCamGO.AddComponent<Camera>();
        unitPreviewCamera.clearFlags         = CameraClearFlags.SolidColor;
        unitPreviewCamera.backgroundColor    = new Color(0.48f, 0.56f, 0.63f, 1f);
        unitPreviewCamera.farClipPlane       = 2000f;
        unitPreviewCamera.nearClipPlane      = 0.3f;
        unitPreviewCamera.fieldOfView        = 60f;
        unitPreviewCamera.depth              = -4;
        ExcluirLayer(unitPreviewCamera, "UI");

        rtUnitCam = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        rtUnitCam.Create();
        unitPreviewCamera.targetTexture      = rtUnitCam;
        unitPreviewCamera.enabled            = false;
    }

    // ─────────────────────────────────────────────────────────
    // START
    // ─────────────────────────────────────────────────────────
    private void Start()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[MenuComando] UIDocument ou rootVisualElement é null em Start().");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // ── Vincula elementos da UI ──────────────────────────
        rootContainer   = root.Q<VisualElement>("root-container");
        mapViewport     = root.Q<VisualElement>("map-viewport");
        mapOverlay      = root.Q<VisualElement>("map-overlay");
        cameraViewport  = root.Q<VisualElement>("camera-viewport");
        lblNoSignal     = root.Q<Label>("lbl-no-signal");
        lblUnitName     = root.Q<Label>("lbl-unit-name");
        lblUnitType     = root.Q<Label>("lbl-unit-type");
        lblUnitSpeedAlt = root.Q<Label>("lbl-unit-speed-alt");
        lblUnitFuelHp   = root.Q<Label>("lbl-unit-fuel-hp");
        lblUnitTarget   = root.Q<Label>("lbl-unit-target");
        btnClose        = root.Q<Button>("btn-close");
        btnFollow       = root.Q<Button>("btn-follow");
        btnStop         = root.Q<Button>("btn-stop");

        // ── Verifica elementos obrigatórios ──────────────────
        VerificarElemento(rootContainer,   "root-container");
        VerificarElemento(mapViewport,     "map-viewport");
        VerificarElemento(mapOverlay,      "map-overlay");
        VerificarElemento(cameraViewport,  "camera-viewport");

        // ── Garante que o menu começa fechado ────────────────
        if (rootContainer != null)
            rootContainer.style.display = DisplayStyle.None;

        // ── CORREÇÃO: vincula RenderTextures aos viewports ───
        // Isso DEVE acontecer depois que a UI foi construída (Start),
        // não em Awake, porque os VisualElements ainda não existem lá.
        if (mapViewport != null && rtMapaTatico != null)
            mapViewport.style.backgroundImage = Background.FromRenderTexture(rtMapaTatico);
        else
            Debug.LogWarning("[MenuComando] Não foi possível vincular rtMapaTatico ao map-viewport.");

        if (cameraViewport != null && rtUnitCam != null)
            cameraViewport.style.backgroundImage = Background.FromRenderTexture(rtUnitCam);
        else
            Debug.LogWarning("[MenuComando] Não foi possível vincular rtUnitCam ao camera-viewport.");

        // ── Botões ───────────────────────────────────────────
        if (btnClose  != null) btnClose.clicked  += FecharMenu;
        if (btnFollow != null) btnFollow.clicked += OnFollowClicked;
        if (btnStop   != null) btnStop.clicked   += OnStopClicked;

        // ── Ajusta câmera do mapa conforme o terreno ─────────
        AjustarCameraMapaTatico();

        // ── Referências de jogo ──────────────────────────────
        gerenteSelecao = FindObjectOfType<GerenteSelecao>();
        if (gerenteSelecao == null)
            Debug.LogWarning("[MenuComando] GerenteSelecao não encontrado na cena. Tente de novo em Update.");
    }

    // ─────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────
    private void Update()
    {
        // Tenta encontrar gerenteSelecao se ainda não tiver
        if (gerenteSelecao == null)
            gerenteSelecao = FindObjectOfType<GerenteSelecao>();

        // ── Tecla 2 → abre/fecha este menu ──────────────────
        // CORREÇÃO: usa KeyCode.Alpha2 para teclado alfanumérico
        // e Keypad2 para teclado numérico lateral.
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (menuAberto) FecharMenu();
            else            AbrirMenu();
        }

        if (!menuAberto) return;

        // ── Atualiza info da unidade a cada frame ────────────
        AtualizarInfoUnidade();

        // ── Atualiza ícones do mapa em intervalo fixo ────────
        timerMapOverlay += Time.deltaTime;
        if (timerMapOverlay >= INTERVALO_MAP_OVERLAY)
        {
            timerMapOverlay = 0f;
            AtualizarIconesMapa();
        }

        // ── Interrompe follow se o jogador mover a câmera ───
        if (isFollowingCamera)
        {
            bool jogadorMoveu = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                                Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (jogadorMoveu) PararFollow();
        }
    }

    // ─────────────────────────────────────────────────────────
    // LATE UPDATE
    // ─────────────────────────────────────────────────────────
    private void LateUpdate()
    {
        // ── Follow mode: câmera principal segue a unidade ────
        if (isFollowingCamera)
        {
            if (targetUnit != null)
            {
                Camera main = Camera.main;
                if (main != null)
                {
                    Vector3 desired = targetUnit.transform.position + cameraOffset;
                    main.transform.position = Vector3.Lerp(main.transform.position, desired, Time.deltaTime * 6f);
                    main.transform.LookAt(targetUnit.transform.position + Vector3.up * 2f);
                }
            }
            else
            {
                PararFollow();
            }
        }

        // ── Câmera de preview da unidade ─────────────────────
        if (menuAberto && unitPreviewCamera != null && unitPreviewCamera.enabled && targetUnit != null)
        {
            Vector3 tp = targetUnit.transform.position;
            // CORREÇÃO: offset em espaço local relativo à direção da unidade,
            // assim a câmera sempre fica atrás/acima dela.
            Vector3 trас = targetUnit.transform.TransformPoint(new Vector3(0f, 18f, -28f));
            unitPreviewCamera.transform.position = Vector3.Lerp(
                unitPreviewCamera.transform.position, trас, Time.deltaTime * 8f);
            unitPreviewCamera.transform.LookAt(tp + Vector3.up * 2f);
        }
    }

    // ─────────────────────────────────────────────────────────
    // ABRIR / FECHAR MENU
    // ─────────────────────────────────────────────────────────
    private void AbrirMenu()
    {
        if (rootContainer == null || menuAberto) return;

        // CORREÇÃO: fecha todos os outros menus antes de abrir
        GerenciadorMenus.AbrirMenu(this);

        menuAberto = true;
        rootContainer.style.display = DisplayStyle.Flex;

        if (cameraMapaTatico != null) cameraMapaTatico.enabled = true;

        AtualizarInfoUnidade();
        AtualizarIconesMapa();

        bool temUnidade = (targetUnit != null);
        if (unitPreviewCamera != null) unitPreviewCamera.enabled = temUnidade;
        if (lblNoSignal != null)
            lblNoSignal.style.display = temUnidade ? DisplayStyle.None : DisplayStyle.Flex;

        // CORREÇÃO: esconde o minimapa do jogo enquanto o menu de comando está aberto
        ToggleMinimapa(false);
    }

    public void FecharMenu()   // público → implementa IMenuJogo
    {
        if (rootContainer == null) return;
        menuAberto = false;
        rootContainer.style.display = DisplayStyle.None;

        if (cameraMapaTatico  != null) cameraMapaTatico.enabled  = false;
        if (unitPreviewCamera != null) unitPreviewCamera.enabled = false;

        PararFollow();

        // CORREÇÃO: reexibe o minimapa quando o menu de comando fecha
        ToggleMinimapa(true);
    }

    // ─────────────────────────────────────────────────────────
    // ATUALIZA INFO DA UNIDADE
    // ─────────────────────────────────────────────────────────
    private void AtualizarInfoUnidade()
    {
        // ── Descobre a unidade atualmente selecionada ────────
        ControleUnidade novaUnidade = null;
        if (gerenteSelecao != null &&
            gerenteSelecao.unidadesSelecionadas != null &&
            gerenteSelecao.unidadesSelecionadas.Count > 0)
        {
            novaUnidade = gerenteSelecao.unidadesSelecionadas[0];
        }

        // ── Troca de unidade? Reconfigura câmera ─────────────
        if (novaUnidade != targetUnit)
        {
            targetUnit = novaUnidade;

            if (targetUnit != null && unitPreviewCamera != null && menuAberto)
            {
                unitPreviewCamera.enabled = true;
                if (lblNoSignal != null) lblNoSignal.style.display = DisplayStyle.None;

                // Reinicia o offset de follow em relação à nova unidade
                if (isFollowingCamera)
                {
                    Camera main = Camera.main;
                    if (main != null)
                        cameraOffset = main.transform.position - targetUnit.transform.position;
                }
            }
            else if (targetUnit == null)
            {
                if (unitPreviewCamera != null) unitPreviewCamera.enabled = false;
                if (lblNoSignal != null) lblNoSignal.style.display = DisplayStyle.Flex;
                PararFollow();
            }
        }

        // ── Sem unidade → limpa UI ───────────────────────────
        if (targetUnit == null)
        {
            if (lblUnitName     != null) lblUnitName.text     = "NENHUMA UNIDADE";
            if (lblUnitType     != null) lblUnitType.text     = "—";
            if (lblUnitSpeedAlt != null) lblUnitSpeedAlt.text = "Speed — km/h    Altitude — m";
            if (lblUnitFuelHp   != null) lblUnitFuelHp.text   = "Fuel —    Health —";
            if (lblUnitTarget   != null) lblUnitTarget.text   = "⊙ Sem alvo";
            return;
        }

        // ── Nome ─────────────────────────────────────────────
        if (lblUnitName != null)
            lblUnitName.text = targetUnit.gameObject.name.Replace("(Clone)", "").Trim().ToUpper();

        // ── Tipo (via IdentidadeUnidade) ─────────────────────
        // CORREÇÃO: log de erro se o componente não for encontrado,
        // para diagnóstico mais fácil.
        if (lblUnitType != null)
        {
            IdentidadeUnidade id = targetUnit.GetComponent<IdentidadeUnidade>();
            if (id != null)
                lblUnitType.text = id.tipoUnidade.ToString();
            else
            {
                lblUnitType.text = "Tipo desconhecido";
                Debug.LogWarning($"[MenuComando] {targetUnit.gameObject.name} não tem IdentidadeUnidade.");
            }
        }

        // ── Velocidade e altitude ────────────────────────────
        if (lblUnitSpeedAlt != null)
        {
            float speedMs = ObterVelocidade(targetUnit);
            int speedKm   = Mathf.RoundToInt(speedMs * 3.6f);
            int altM      = Mathf.RoundToInt(targetUnit.transform.position.y);
            lblUnitSpeedAlt.text = $"Speed {speedKm} km/h    Altitude {altM} m";
        }

        // ── Combustível e HP ─────────────────────────────────
        if (lblUnitFuelHp != null)
        {
            string fuelStr = "N/A";
            CombustivelUnidade fuel = targetUnit.GetComponent<CombustivelUnidade>();
            if (fuel != null && fuel.usaCombustivel && fuel.Capacidade > 0f)
                fuelStr = $"{Mathf.RoundToInt(fuel.Percentual * 100f)}%";

            string hpStr = "100%";
            SistemaDeDanos vida = targetUnit.GetComponent<SistemaDeDanos>();
            if (vida != null && vida.vidaMaxima > 0f)
                hpStr = $"{Mathf.RoundToInt((vida.vidaAtual / vida.vidaMaxima) * 100f)}%";

            lblUnitFuelHp.text = $"Fuel {fuelStr}    Health {hpStr}";
        }

        // ── Alvo / estado ────────────────────────────────────
        if (lblUnitTarget != null)
        {
            try
            {
                var estado = targetUnit.ObterEstadoControle();
                lblUnitTarget.text = estado.possuiDestinoOrdenado ? "⊙ Em Movimento" : "⊙ Aguardando";
            }
            catch
            {
                lblUnitTarget.text = "⊙ Status OK";
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // ATUALIZA ÍCONES DO MAPA
    // CORREÇÃO PRINCIPAL: FindObjectsOfType é caro; usa cache e
    // adiciona log de diagnóstico quando nada é encontrado.
    // ─────────────────────────────────────────────────────────
    private void AtualizarIconesMapa()
    {
        if (mapOverlay == null || cameraMapaTatico == null) return;
        mapOverlay.Clear();

        Vector3 centro   = cameraMapaTatico.transform.position;
        float   halfSize = cameraMapaTatico.orthographicSize;

        // CORREÇÃO: FindObjectsOfType retorna array vazio se o componente não existir
        // na cena. Use o log abaixo para confirmar que seus prefabs têm IdentidadeUnidade.
        IdentidadeUnidade[] todasUnidades = FindObjectsOfType<IdentidadeUnidade>();

        if (todasUnidades.Length == 0)
        {
            // Este aviso aparecerá no Console do Unity enquanto o menu estiver aberto.
            // Se aparecer, o problem é que seus prefabs NÃO têm o componente IdentidadeUnidade.
            Debug.LogWarning("[MenuComando] Nenhuma IdentidadeUnidade encontrada na cena! " +
                             "Verifique se seus prefabs de unidade têm esse componente adicionado.");
            return;
        }

        foreach (var idUnit in todasUnidades)
        {
            if (idUnit == null || !idUnit.gameObject.activeInHierarchy) continue;

            Vector3 worldPos = idUnit.transform.position;

            // Normaliza posição em relação à área vista pela câmera ortográfica
            // CORREÇÃO: usa cameraMapaTatico.transform.position.x/z como centro,
            // não hardcoded (0,0). Importante para terrenos deslocados da origem.
            float normX = (worldPos.x - (centro.x - halfSize)) / (halfSize * 2f);
            float normZ = (worldPos.z - (centro.z - halfSize)) / (halfSize * 2f);

            // Descarta unidades fora do campo de visão do mapa
            if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) continue;

            VisualElement icon = new VisualElement();
            icon.AddToClassList("map-icon");

            ControleUnidade cu = idUnit.GetComponent<ControleUnidade>();

            if (targetUnit != null && cu == targetUnit)
            {
                icon.AddToClassList("map-icon-selected");
            }
            else
            {
                // CORREÇÃO: teamID == 1 → amigo, qualquer outro → inimigo.
                // Ajuste os valores conforme seu sistema de equipes.
                // Se seus prefabs amigos usam teamID diferente de 1, mude aqui.
                bool ehAmigo = (idUnit.teamID == 1);
                icon.AddToClassList(ehAmigo ? "map-icon-friendly" : "map-icon-enemy");
            }

            // Posiciona o ícone: normZ é invertido porque Y da tela cresce para baixo
            icon.style.left = Length.Percent(normX * 100f);
            icon.style.top  = Length.Percent((1f - normZ) * 100f);

            mapOverlay.Add(icon);
        }
    }

    // ─────────────────────────────────────────────────────────
    // MINIMAPA: OCULTAR/EXIBIR
    // CORREÇÃO: o minimapa do jogo continuava visível porque
    // nenhum código o ocultava. Adapte o nome do componente
    // conforme o seu projeto (MinimapController, MinimapHUD, etc.)
    // ─────────────────────────────────────────────────────────
    private void ToggleMinimapa(bool exibir)
    {
        // Opção B: se o minimapa é um GameObject com uma câmera e um Canvas/UI,
        // encontre pelo nome e ative/desative.
        GameObject minimapGO = GameObject.Find("Minimapa") ??
                               GameObject.Find("MiniMap")  ??
                               GameObject.Find("UI_Minimapa");
        if (minimapGO != null)
        {
            minimapGO.SetActive(exibir);
        }
        // Se nenhuma das opções funcionar, descomente o log abaixo para diagnóstico:
        // else Debug.LogWarning("[MenuComando] Minimapa não encontrado. Ajuste ToggleMinimapa().");
    }

    // ─────────────────────────────────────────────────────────
    // AJUSTE DA CÂMERA DO MAPA AO TERRENO
    // ─────────────────────────────────────────────────────────
    private void AjustarCameraMapaTatico()
    {
        GerenciadorQuartel gq = FindObjectOfType<GerenciadorQuartel>();
        if (gq != null)
        {
            tamanhoTerrenoTatico = gq.raioDeCobertura * 2f;
            cameraMapaTatico.transform.position = new Vector3(
                gq.transform.position.x, 1500f, gq.transform.position.z);
        }
        else
        {
            Debug.LogWarning("[MenuComando] GerenciadorQuartel não encontrado. " +
                             "Câmera do mapa centralizada em (0, 1500, 0).");
        }
        cameraMapaTatico.orthographicSize = tamanhoTerrenoTatico / 2f;
    }

    // ─────────────────────────────────────────────────────────
    // FOLLOW CÂMERA
    // ─────────────────────────────────────────────────────────
    private void OnFollowClicked()
    {
        if (targetUnit == null) return;

        isFollowingCamera = !isFollowingCamera;

        if (isFollowingCamera)
        {
            Camera main = Camera.main;
            if (main != null)
                cameraOffset = main.transform.position - targetUnit.transform.position;

            if (btnFollow != null)
            {
                btnFollow.text = "SEGUINDO";
                btnFollow.AddToClassList("active");
            }
        }
        else
        {
            PararFollow();
        }
    }

    private void PararFollow()
    {
        isFollowingCamera = false;
        if (btnFollow != null)
        {
            btnFollow.text = "FOCAR CÂMERA";
            btnFollow.RemoveFromClassList("active");
        }
    }

    // ─────────────────────────────────────────────────────────
    // PARAR UNIDADE
    // ─────────────────────────────────────────────────────────
    private void OnStopClicked()
    {
        if (targetUnit != null)
            targetUnit.EmitirOrdemParar();
    }

    // ─────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────
    private float ObterVelocidade(ControleUnidade cu)
    {
        Rigidbody rb = cu.GetComponent<Rigidbody>();
        if (rb != null) return rb.linearVelocity.magnitude;

        UnityEngine.AI.NavMeshAgent agent = cu.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            return agent.velocity.magnitude;

        return 0f;
    }

    private void ExcluirLayer(Camera cam, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) cam.cullingMask &= ~(1 << layer);
    }

    private void VerificarElemento(VisualElement el, string nome)
    {
        if (el == null)
            Debug.LogError($"[MenuComando] Elemento '{nome}' não encontrado no UXML. " +
                           "Verifique se o name=\"{nome}\" existe no arquivo .uxml.");
    }

    // ─────────────────────────────────────────────────────────
    // CLEANUP
    // ─────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        GerenciadorMenus.Desregistrar(this);

        if (_instance == this) _instance = null;
        if (rtMapaTatico  != null) { rtMapaTatico.Release();  Destroy(rtMapaTatico);  }
        if (rtUnitCam     != null) { rtUnitCam.Release();     Destroy(rtUnitCam);     }
        // Câmeras são filhas do GO e serão destruídas junto com ele
    }
}
