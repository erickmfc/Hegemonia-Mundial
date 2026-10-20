using System;
using System.Collections.Generic;
using System.Text;
using Hegemonia.RTS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tactical Display no canto inferior esquerdo. Mantem a API historica de MiniMapa,
/// mas apresenta apenas a cartografia estavel e os contatos autorizados pelo
/// RTSTacticalContactAdapter; nao usa uma camera que possa renderizar unidades ocultas.
/// </summary>
public class MiniMapa : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Transform acompanhado pelo mapa quando o modo Centro está ativo.")]
    public Transform alvoJogador;

    [Tooltip("Campo legado mantido para preservar cenas e prefabs serializados.")]
    public float alturaCamera = 120f;

    [Tooltip("Campo legado mantido para preservar cenas e prefabs serializados.")]
    public float tamanhoOrtografico = 150f;

    [Tooltip("Tamanho de referência do Tactical Display em pixels/unidades de UI.")]
    public int tamanhoUI = 220;

    [Tooltip("Margem do canto da tela.")]
    public int margemBorda = 20;

    [Header("Desempenho")]
    [Tooltip("Campo legado mantido por compatibilidade; o mapa não cria RenderTexture.")]
    [Range(128, 512)] public int resolucaoRender = 256;
    [Tooltip("Campo legado mantido por compatibilidade; os dados são atualizados em ticks.")]
    [Min(0.02f)] public float intervaloRenderCamera = 0.1f;
    [Tooltip("Intervalo de reconciliação de unidades próprias registradas.")]
    [Min(0.25f)] public float intervaloReconciliacaoSeguranca = 2f;

    [Header("Visual")]
    public Color corBorda = new Color(0.18f, 0.15f, 0.12f, 1f);
    public Color corFundoRadar = new Color(0.85f, 0.78f, 0.60f, 0.10f);
    public float espessuraBorda = 8f;

    [Header("Indicador do Jogador")]
    public Color corTrianguloJogador = Color.red;
    public float tamanhoTriangulo = 14f;

    [Header("Filtros de contato")]
    public bool mostrarInimigos = true;
    public bool mostrarAliados = true;
    [Tooltip("Campo legado mantido para preservar cenas e prefabs serializados.")]
    public float raioDeteccao = 400f;

    private const int MaxVisibleMarkers = 256;
    private const float DefaultViewWidthMeters = 40000f;
    private const float MinimumViewWidthMeters = 2000f;
    private const float FallbackWorldWidthMeters = 512000f;
    private const float FallbackWorldHeightMeters = 288000f;
    private const float ContactRefreshInterval = 0.25f;
    private const float AdapterResolveInterval = 1.25f;

    private sealed class MarkerView
    {
        public RectTransform rect;
        public Image background;
        public Text glyph;
        public Button button;
        public RectTransform rangeRect;
        public Image rangeImage;
    }

    private struct MarkerData
    {
        public bool isFriendlyUnit;
        public IdentidadeUnidade friendlyIdentity;
        public ControleUnidade friendlyControl;
        public RTSTacticalContactSnapshot contact;
        public Vector3 position;
        public RTSTacticalContactCategory category;
        public RTSTacticalContactRelation relation;
        public bool isEstimated;
    }

    private readonly List<IdentidadeUnidade> _registeredUnits = new List<IdentidadeUnidade>(256);
    private readonly List<RTSTacticalContactSnapshot> _contactSnapshots = new List<RTSTacticalContactSnapshot>(2048);
    private readonly List<MarkerData> _markers = new List<MarkerData>(MaxVisibleMarkers);
    private readonly List<MarkerView> _markerViews = new List<MarkerView>(MaxVisibleMarkers);
    private readonly StringBuilder _detailBuilder = new StringBuilder(192);

    private RTSTacticalContactAdapter _contactAdapter;
    private GerenteDeTerritorio _territoryManager;
    private GlobalWorldDefinition _worldDefinition;
    private GerenteSelecao _selectionManager;
    private Canvas _uiCanvas;
    private EventSystem _eventSystem;
    private GameObject _uiRoot;
    private GameObject _panelObject;
    private GameObject _ownedEventSystemObject;
    private RectTransform _panelRect;
    private RectTransform _controlsRect;
    private RectTransform _mapViewport;
    private RectTransform _scaleBarRect;
    private RawImage _mapImage;
    private Text _titleText;
    private Text _rangeText;
    private Text _scaleText;
    private Text _detailsText;
    private Text _playerGlyph;
    private Text _alliesButtonText;
    private Text _contactsButtonText;
    private Button _alliesButton;
    private Button _contactsButton;
    private Material _chartMaterial;
    private Texture2D _fallbackMapTexture;

    private int _playerTeamId = 1;
    private int _lastRegistryVersion = -1;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private bool _initialized;
    private bool _createdUiRoot;
    private bool _createdEventSystem;
    private bool _visibleByHotkey = true;
    private bool _suppressedByCommandMenu;
    private bool _suppressedByOtherUi;
    private bool _followPlayer = true;
    private bool _hasMapCenter;
    private bool _hasSelectedContact;
    private bool _hasSelectedUnit;
    private float _nextRefreshAt;
    private float _nextReconcileAt;
    private float _nextAdapterResolveAt;
    private float _nextPlayerResolveAt;
    private float _nextVisibilityCheckAt;
    private float _worldWidthMeters = FallbackWorldWidthMeters;
    private float _worldHeightMeters = FallbackWorldHeightMeters;
    private float _viewWidthMeters = DefaultViewWidthMeters;
    private float _viewUMin;
    private float _viewVMin;
    private float _viewUSpan;
    private float _viewVSpan;
    private Vector2 _viewCenterUv = new Vector2(0.5f, 0.5f);
    private string _selectedContactId;
    private RTSTacticalContactSnapshot _selectedContact;
    private IdentidadeUnidade _selectedUnit;
    private Vector3 _lastPlayerPosition;
    private Camera _disabledLegacyCamera;

    private static readonly Color ColorPanel = new Color32(15, 23, 28, 244);
    private static readonly Color ColorHeader = new Color32(34, 45, 51, 255);
    private static readonly Color ColorMuted = new Color32(153, 171, 177, 255);
    private static readonly Color ColorFriendly = new Color32(67, 219, 205, 255);
    private static readonly Color ColorHostile = new Color32(238, 80, 80, 255);
    private static readonly Color ColorNeutral = new Color32(241, 185, 91, 255);
    private static readonly Color ColorCivilian = new Color32(225, 225, 205, 255);
    private static readonly Color ColorUnknown = new Color32(218, 155, 76, 255);
    private static Sprite _rangeRingSprite;
    private Button _rangeFilterButton;
    private Text _rangeFilterText;
    private bool _showAllFriendlyRanges;

    private void OnEnable()
    {
        InitializeIfNeeded();
        ResolveContactAdapter();
        SetRuntimeVisible(ShouldBeVisible());
    }

    private void Start()
    {
        InitializeIfNeeded();
    }

    private void OnDisable()
    {
        if (_uiRoot != null)
            _uiRoot.SetActive(false);
        UnbindContactAdapter();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.N) || _suppressedByCommandMenu
            || IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto)
            return;

        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && (selected.GetComponentInParent<InputField>() != null
            || HasNamedComponentInParents(selected.transform, "TMP_InputField")))
            return;

        _visibleByHotkey = !_visibleByHotkey;
        SetRuntimeVisible(ShouldBeVisible());
    }

    private void LateUpdate()
    {
        if (!_initialized)
            InitializeIfNeeded();

        if (!_initialized)
            return;

        float visibilityNow = Time.unscaledTime;
        if (visibilityNow >= _nextVisibilityCheckAt)
        {
            _nextVisibilityCheckAt = visibilityNow + 0.15f;
            _suppressedByOtherUi = !_suppressedByCommandMenu
                && IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto;
            SetRuntimeVisible(ShouldBeVisible());
        }

        if (!_initialized || !_visibleByHotkey || _uiRoot == null || !_uiRoot.activeSelf)
            return;

        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            UpdateResponsiveLayout();

        float now = Time.unscaledTime;
        if (now < _nextRefreshAt)
            return;

        float interval = DiagnosticoDesempenhoJogo.RuntimeSaturado()
            ? 0.60f
            : DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                ? 0.40f
                : ContactRefreshInterval;
        _nextRefreshAt = now + interval;

        ResolvePlayerTarget();
        ResolveContactAdapter();
        ResolvePlayerTeam();
        RefreshOwnedUnits(now);
        RefreshContactSnapshots();
        RebuildMarkerData();
        UpdateMapViewport();
        UpdateMarkerViews();
        UpdatePlayerMarker();
        UpdateScaleReadout();
        UpdateSelectionDetails();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        ResolvePlayerTarget();
        ResolveWorldDefinition();
        ResolvePlayerTeam();
        DisableLegacyWorldCamera();
        CreateDisplayUi();
        BindContactAdapter();

        _viewWidthMeters = Mathf.Clamp(DefaultViewWidthMeters, MinimumViewWidthMeters, GetMaximumViewWidth());
        if (alvoJogador != null)
            SetMapCenterFromWorld(alvoJogador.position);
        else
            _hasMapCenter = true;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _initialized = true;
        UpdateResponsiveLayout();
        SetRuntimeVisible(isActiveAndEnabled && _visibleByHotkey);
    }

    private void ResolvePlayerTarget()
    {
        if (alvoJogador != null)
            return;
        if (Time.unscaledTime < _nextPlayerResolveAt)
            return;
        _nextPlayerResolveAt = Time.unscaledTime + AdapterResolveInterval;

        Camera main = Camera.main;
        if (main != null && main != _disabledLegacyCamera)
        {
            alvoJogador = main.transform;
            return;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate == _disabledLegacyCamera)
                continue;

            alvoJogador = candidate.transform;
            return;
        }
    }

    private void ResolveWorldDefinition()
    {
        if (_worldDefinition == null)
        {
            GlobalTerrainStreamer[] streamers = FindObjectsByType<GlobalTerrainStreamer>(FindObjectsSortMode.None);
            for (int i = 0; i < streamers.Length; i++)
            {
                if (streamers[i] != null && streamers[i].world != null)
                {
                    _worldDefinition = streamers[i].world;
                    break;
                }
            }
        }

        _territoryManager = GerenteDeTerritorio.Instancia;
        if (_worldDefinition != null)
        {
            _worldWidthMeters = Mathf.Max(1f, _worldDefinition.worldSize.x);
            _worldHeightMeters = Mathf.Max(1f, _worldDefinition.mapFootprintHeight);
        }
        else if (_territoryManager != null)
        {
            Vector3 topLeft = _territoryManager.MapUvToWorld(Vector2.zero);
            Vector3 topRight = _territoryManager.MapUvToWorld(Vector2.right);
            Vector3 bottomLeft = _territoryManager.MapUvToWorld(Vector2.up);
            _worldWidthMeters = Mathf.Max(1f, Vector3.Distance(topLeft, topRight));
            _worldHeightMeters = Mathf.Max(1f, Vector3.Distance(topLeft, bottomLeft));
        }
        else
        {
            _worldWidthMeters = FallbackWorldWidthMeters;
            _worldHeightMeters = FallbackWorldHeightMeters;
        }
    }

    private void ResolvePlayerTeam()
    {
        if (GerenciadorDePartida.Instancia != null && GerenciadorDePartida.Instancia.idJogador > 0)
            _playerTeamId = GerenciadorDePartida.Instancia.idJogador;
        else if (SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.teamJogador > 0)
            _playerTeamId = SistemaGovernoMundial.Instancia.teamJogador;
    }

    private void ResolveContactAdapter()
    {
        if (Time.unscaledTime < _nextAdapterResolveAt && _contactAdapter != null)
            return;

        _nextAdapterResolveAt = Time.unscaledTime + AdapterResolveInterval;
        RTSTacticalContactAdapter current = RTSTacticalContactAdapter.Instancia;
        if (current == _contactAdapter)
            return;

        UnbindContactAdapter();
        _contactAdapter = current;
        if (_contactAdapter != null)
            _contactAdapter.OnContactChanged += HandleContactChanged;
    }

    private void BindContactAdapter()
    {
        _contactAdapter = RTSTacticalContactAdapter.Instancia;
        if (_contactAdapter != null)
            _contactAdapter.OnContactChanged += HandleContactChanged;
        _nextAdapterResolveAt = Time.unscaledTime + AdapterResolveInterval;
    }

    private void UnbindContactAdapter()
    {
        if (_contactAdapter != null)
            _contactAdapter.OnContactChanged -= HandleContactChanged;
        _contactAdapter = null;
    }

    private void HandleContactChanged(RTSTacticalContactSnapshot ignored)
    {
        // A leitura é feita em lote no próximo tick; o evento não aloca UI nem percorre a cena.
        _nextRefreshAt = 0f;
    }

    private void DisableLegacyWorldCamera()
    {
        GameObject legacyCameraObject = GameObject.Find("Cam_MiniMapa");
        if (legacyCameraObject == null)
            return;

        Camera legacyCamera = legacyCameraObject.GetComponent<Camera>();
        if (legacyCamera == null)
            return;

        _disabledLegacyCamera = legacyCamera;
        legacyCamera.enabled = false;
        legacyCamera.targetTexture = null;
    }

    private void CreateDisplayUi()
    {
        Transform hierarchyRoot = transform.root;
        // A Screen Space Overlay cannot inherit a zero scale from the scene UI.
        // Keep a usable canvas hierarchy when it has a valid scale, and detach
        // only when an ancestor would make the minimap invisible.
        Transform uiParent = HasUsableOverlayScale(hierarchyRoot) ? hierarchyRoot : null;
        Transform existing = uiParent != null ? uiParent.Find("Canvas_MiniMapa") : null;
        if (existing == null)
        {
            GameObject found = GameObject.Find("Canvas_MiniMapa");
            if (found != null && found.transform != transform && !found.transform.IsChildOf(transform))
                existing = found.transform;
        }

        if (existing != null)
        {
            _uiRoot = existing.gameObject;
            if (_uiRoot.transform != uiParent && (uiParent == null || !_uiRoot.transform.IsChildOf(uiParent)))
                _uiRoot.transform.SetParent(uiParent, false);
        }
        else
        {
            _uiRoot = new GameObject("Canvas_MiniMapa", typeof(RectTransform));
            _uiRoot.transform.SetParent(uiParent, false);
            _createdUiRoot = true;
        }

        _uiRoot.name = "Canvas_MiniMapa";
        RectTransform rootRect = _uiRoot.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = _uiRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;

        _uiCanvas = _uiRoot.GetComponent<Canvas>();
        if (_uiCanvas == null)
            _uiCanvas = _uiRoot.AddComponent<Canvas>();
        _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _uiCanvas.overrideSorting = true;
        _uiCanvas.sortingOrder = 50;

        CanvasScaler scaler = _uiRoot.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = _uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (_uiRoot.GetComponent<GraphicRaycaster>() == null)
            _uiRoot.AddComponent<GraphicRaycaster>();

        Transform oldContainer = _uiRoot.transform.Find("MiniMapa_Container");
        if (oldContainer != null)
            Destroy(oldContainer.gameObject);

        EnsureEventSystem();
        CreatePanel();
        InitializeChartMaterial();
    }

    private void EnsureEventSystem()
    {
        _eventSystem = EventSystem.current;
        if (_eventSystem != null)
            return;

        _ownedEventSystemObject = new GameObject("TacticalDisplay_EventSystem",
            typeof(EventSystem), typeof(StandaloneInputModule));
        _eventSystem = _ownedEventSystemObject.GetComponent<EventSystem>();
        _createdEventSystem = true;
    }

    private void CreatePanel()
    {
        _panelObject = new GameObject("MiniMapa_Container", typeof(RectTransform), typeof(Image));
        _panelObject.transform.SetParent(_uiRoot.transform, false);
        _panelRect = _panelObject.GetComponent<RectTransform>();
        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.zero;
        _panelRect.pivot = Vector2.zero;

        Image panelBackground = _panelObject.GetComponent<Image>();
        panelBackground.color = ColorPanel;
        panelBackground.raycastTarget = false;

        GameObject edge = CreateUiObject("Frame", _panelObject.transform, typeof(Image));
        RectTransform edgeRect = edge.GetComponent<RectTransform>();
        Stretch(edgeRect, 0f, 0f, 0f, 0f);
        Image edgeImage = edge.GetComponent<Image>();
        edgeImage.color = Color.Lerp(corBorda, new Color32(89, 108, 116, 255), 0.45f);
        edgeImage.raycastTarget = false;
        float frameThickness = Mathf.Clamp(espessuraBorda, 0f, 8f);
        edgeImage.enabled = frameThickness > 0f;

        GameObject inner = CreateUiObject("Inner", edge.transform, typeof(Image));
        RectTransform innerRect = inner.GetComponent<RectTransform>();
        Stretch(innerRect, frameThickness, frameThickness, frameThickness, frameThickness);
        Image innerImage = inner.GetComponent<Image>();
        innerImage.color = ColorPanel;
        innerImage.raycastTarget = false;

        GameObject header = CreateUiObject("Header", inner.transform, typeof(Image));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -34f);
        headerRect.offsetMax = Vector2.zero;
        Image headerImage = header.GetComponent<Image>();
        headerImage.color = ColorHeader;
        headerImage.raycastTarget = false;

        _titleText = CreateText("Title", header.transform, "TACTICAL DISPLAY", 15, ColorFriendly,
            TextAnchor.MiddleLeft);
        RectTransform titleRect = _titleText.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(10f, 0f);
        titleRect.offsetMax = new Vector2(-108f, 0f);
        _titleText.fontStyle = FontStyle.Bold;

        _rangeText = CreateText("NorthRange", header.transform, "N ↑  40 KM", 11, ColorMuted,
            TextAnchor.MiddleRight);
        RectTransform rangeRect = _rangeText.rectTransform;
        rangeRect.anchorMin = Vector2.zero;
        rangeRect.anchorMax = Vector2.one;
        rangeRect.offsetMin = new Vector2(0f, 0f);
        rangeRect.offsetMax = new Vector2(-9f, 0f);

        CreateFilterControls(inner.transform);
        CreateMapViewport(inner.transform);
        CreateDetailsFooter(inner.transform);
    }

    private void CreateFilterControls(Transform parent)
    {
        GameObject row = CreateUiObject("Controls", parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        _controlsRect = rowRect;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(8f, -68f);
        rowRect.offsetMax = new Vector2(-8f, -36f);

        float x = 0f;
        _alliesButton = CreateButton("AlliesFilter", row.transform, "ALIADOS", 52f, x, -1f,
            out _alliesButtonText, ToggleAlliesFilter);
        x += 56f;
        _contactsButton = CreateButton("ContactsFilter", row.transform, "TRILHAS", 58f, x, -1f,
            out _contactsButtonText, ToggleContactsFilter);
        x += 62f;
        CreateButton("ZoomOut", row.transform, "−", 24f, x, -1f, out _, ZoomOut);
        x += 28f;
        CreateButton("ZoomIn", row.transform, "+", 24f, x, -1f, out _, ZoomIn);
        x += 28f;
        CreateButton("Recenter", row.transform, "CENTRO", 48f, x, -1f, out _, RecenterOnPlayer);
        x += 52f;
        _rangeFilterButton = CreateButton("AttackRangeFilter", row.transform, "RAIOS ○", 58f, x, -1f, out _rangeFilterText, ToggleFriendlyRanges);
        UpdateFilterButtons();
    }

    private void CreateMapViewport(Transform parent)
    {
        GameObject mapObject = CreateUiObject("MapViewport", parent, typeof(Image), typeof(RectMask2D));
        _mapViewport = mapObject.GetComponent<RectTransform>();
        _mapViewport.anchorMin = Vector2.zero;
        _mapViewport.anchorMax = Vector2.one;
        _mapViewport.offsetMin = new Vector2(8f, 49f);
        _mapViewport.offsetMax = new Vector2(-8f, -73f);

        Image mapBackground = mapObject.GetComponent<Image>();
        mapBackground.color = corFundoRadar;
        mapBackground.raycastTarget = false;

        GameObject textureObject = CreateUiObject("ChartBase", mapObject.transform, typeof(RawImage));
        RectTransform textureRect = textureObject.GetComponent<RectTransform>();
        Stretch(textureRect, 0f, 0f, 0f, 0f);
        _mapImage = textureObject.GetComponent<RawImage>();
        _mapImage.raycastTarget = false;

        GameObject clickArea = CreateUiObject("MapRecenterArea", mapObject.transform,
            typeof(Image), typeof(Button));
        RectTransform clickRect = clickArea.GetComponent<RectTransform>();
        Stretch(clickRect, 0f, 0f, 0f, 0f);
        Image clickImage = clickArea.GetComponent<Image>();
        clickImage.color = new Color(1f, 1f, 1f, 0f);
        clickImage.raycastTarget = true;
        Button clickButton = clickArea.GetComponent<Button>();
        clickButton.targetGraphic = clickImage;
        clickButton.transition = Selectable.Transition.None;
        SetNoNavigation(clickButton);
        clickButton.onClick.AddListener(CenterOnClickedMapPoint);

        _playerGlyph = CreateText("PlayerPosition", mapObject.transform, "▲",
            Mathf.RoundToInt(Mathf.Clamp(tamanhoTriangulo, 10f, 20f)), corTrianguloJogador,
            TextAnchor.MiddleCenter);
        _playerGlyph.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _playerGlyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _playerGlyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _playerGlyph.rectTransform.sizeDelta = new Vector2(22f, 22f);
        _playerGlyph.raycastTarget = false;
        _playerGlyph.fontStyle = FontStyle.Bold;

        _scaleText = CreateText("ScaleText", mapObject.transform, "10 km", 10, Color.white,
            TextAnchor.MiddleLeft);
        _scaleText.rectTransform.anchorMin = Vector2.zero;
        _scaleText.rectTransform.anchorMax = Vector2.zero;
        _scaleText.rectTransform.pivot = new Vector2(0f, 0f);
        _scaleText.rectTransform.anchoredPosition = new Vector2(9f, 5f);
        _scaleText.rectTransform.sizeDelta = new Vector2(70f, 14f);
        _scaleText.raycastTarget = false;
        _scaleText.fontStyle = FontStyle.Bold;

        GameObject scaleBar = CreateUiObject("ScaleBar", mapObject.transform, typeof(Image));
        _scaleBarRect = scaleBar.GetComponent<RectTransform>();
        _scaleBarRect.anchorMin = Vector2.zero;
        _scaleBarRect.anchorMax = Vector2.zero;
        _scaleBarRect.pivot = new Vector2(0f, 0f);
        _scaleBarRect.anchoredPosition = new Vector2(9f, 3f);
        _scaleBarRect.sizeDelta = new Vector2(52f, 2f);
        Image scaleImage = scaleBar.GetComponent<Image>();
        scaleImage.color = Color.white;
        scaleImage.raycastTarget = false;

        Text north = CreateText("NorthIndicator", mapObject.transform, "N", 11, ColorFriendly,
            TextAnchor.MiddleRight);
        north.rectTransform.anchorMin = Vector2.one;
        north.rectTransform.anchorMax = Vector2.one;
        north.rectTransform.pivot = Vector2.one;
        north.rectTransform.anchoredPosition = new Vector2(-7f, -5f);
        north.rectTransform.sizeDelta = new Vector2(18f, 16f);
        north.fontStyle = FontStyle.Bold;
        north.raycastTarget = false;
    }

    private void CreateDetailsFooter(Transform parent)
    {
        _detailsText = CreateText("ContactDetails", parent,
            "SEM SELEÇÃO  ·  CONTATOS CONFORME DETECÇÃO", 10, ColorMuted, TextAnchor.MiddleLeft);
        RectTransform detailsRect = _detailsText.rectTransform;
        detailsRect.anchorMin = Vector2.zero;
        detailsRect.anchorMax = new Vector2(1f, 0f);
        detailsRect.pivot = new Vector2(0.5f, 0f);
        detailsRect.offsetMin = new Vector2(9f, 5f);
        detailsRect.offsetMax = new Vector2(-9f, 42f);
        _detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _detailsText.verticalOverflow = VerticalWrapMode.Truncate;
        _detailsText.raycastTarget = false;
    }

    private void InitializeChartMaterial()
    {
        if (_mapImage == null)
            return;

        Texture2D authority = _worldDefinition != null ? _worldDefinition.authorityMap : null;
        Texture2D appearance = _worldDefinition != null ? _worldDefinition.appearanceMap : null;
        if (authority == null)
            authority = Resources.Load<Texture2D>("fase");

        Shader shader = Resources.Load<Shader>("MapaMBaseMilitar");
        if (shader != null && shader.isSupported && authority != null && appearance != null)
        {
            _chartMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _chartMaterial.SetTexture("_AuthorityTex", authority);
            _chartMaterial.SetTexture("_AppearanceTex", appearance);
            _mapImage.material = _chartMaterial;
            _mapImage.texture = Texture2D.whiteTexture;
            _mapImage.color = Color.white;
            return;
        }

        _fallbackMapTexture = authority;
        _mapImage.material = null;
        _mapImage.texture = _fallbackMapTexture != null ? _fallbackMapTexture : Texture2D.blackTexture;
        _mapImage.color = new Color(0.45f, 0.63f, 0.69f, 1f);
    }

    private void UpdateResponsiveLayout()
    {
        if (_panelRect == null || _uiCanvas == null)
            return;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        float scale = Mathf.Max(0.1f, _uiCanvas.scaleFactor);
        Rect layout = CalculateResponsivePanelLayout(Screen.width, Screen.height, scale, margemBorda);
        _panelRect.sizeDelta = new Vector2(layout.width, layout.height);
        _panelRect.localScale = Vector3.one * 0.5f;
        _panelRect.anchoredPosition = new Vector2(layout.x, layout.y);

        if (_controlsRect != null)
        {
            const float controlsPreferredWidth = 284f;
            float availableWidth = Mathf.Max(1f, layout.width - 16f);
            float controlsScale = Mathf.Min(1f, availableWidth / controlsPreferredWidth);
            _controlsRect.localScale = Vector3.one * controlsScale;
        }

        if (_mapViewport != null)
        {
            _mapViewport.offsetMin = new Vector2(8f, 49f);
            _mapViewport.offsetMax = new Vector2(-8f, -73f);
        }
    }

    private static Rect CalculateResponsivePanelLayout(float screenWidth, float screenHeight, float canvasScale, float borderMargin)
    {
        float scale = Mathf.Max(0.1f, canvasScale);
        float canvasWidth = screenWidth / scale;
        float canvasHeight = screenHeight / scale;
        // O Tactical Display permanece responsivo, mas reduzido à metade para
        // não competir com a área de jogo e com o HUD inferior.
        float width = Mathf.Clamp(canvasWidth * 0.21f, 190f, 420f);
        float height = Mathf.Clamp(canvasHeight * 0.33f, 165f, 360f);
        width = Mathf.Min(width, Mathf.Max(160f, canvasWidth - borderMargin * 2f));
        height = Mathf.Min(height, Mathf.Max(150f, canvasHeight - borderMargin * 2f));

        float margin = Mathf.Max(8f, borderMargin / scale);
        float hudHeight = Mathf.Min(Mathf.Clamp(canvasHeight * 0.19f, 145f, 190f), canvasHeight * 0.25f);
        float bottom = margin + hudHeight + 22f;
        float visualHeight = height * 0.5f;
        if (bottom + visualHeight > canvasHeight - margin)
            bottom = Mathf.Max(margin, canvasHeight - margin - visualHeight);
        // Anchors remain at the canvas origin for compatibility with existing
        // UI layout consumers; the computed offset places the scaled panel at
        // the lower-right edge without relying on a right-anchored pivot.
        float right = Mathf.Max(margin, canvasWidth - margin - width * 0.5f);
        return new Rect(right, bottom, width, height);
    }

    private void SetRuntimeVisible(bool visible)
    {
        if (_uiRoot != null)
            _uiRoot.SetActive(visible && isActiveAndEnabled);
    }

    private static bool HasUsableOverlayScale(Transform target)
    {
        if (target == null)
            return false;

        Vector3 scale = target.lossyScale;
        return Mathf.Abs(scale.x) > 0.0001f && Mathf.Abs(scale.y) > 0.0001f;
    }

    /// <summary>Oculta o Tactical Display sem desativar este componente.</summary>
    public void SuprimirPorMenuComando(bool suprimir)
    {
        _suppressedByCommandMenu = suprimir;
        _suppressedByOtherUi = !suprimir && IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto;
        SetRuntimeVisible(ShouldBeVisible());
    }

    private bool ShouldBeVisible()
    {
        return isActiveAndEnabled && _visibleByHotkey
            && !_suppressedByCommandMenu && !_suppressedByOtherUi;
    }

    private void RefreshOwnedUnits(float now)
    {
        int version = RegistroEntidadesJogo.Version;
        if (version == _lastRegistryVersion && now < _nextReconcileAt)
            return;

        _nextReconcileAt = now + Mathf.Max(0.25f, intervaloReconciliacaoSeguranca);
        _lastRegistryVersion = version;
        RegistroEntidadesJogo.FillUnidades(_registeredUnits);
    }

    private void RefreshContactSnapshots()
    {
        if (_contactAdapter == null || _playerTeamId <= 0 || (!mostrarInimigos && !mostrarAliados))
        {
            _contactSnapshots.Clear();
            return;
        }

        _contactAdapter.CopiarContatos(_playerTeamId, _contactSnapshots);
        _contactSnapshots.Sort(CompareContactPriority);
    }

    private void RebuildMarkerData()
    {
        _markers.Clear();
        if (mostrarAliados)
        {
            for (int i = 0; i < _registeredUnits.Count && _markers.Count < MaxVisibleMarkers; i++)
            {
                IdentidadeUnidade identity = _registeredUnits[i];
                if (identity == null || !identity.isActiveAndEnabled || identity.teamID != _playerTeamId)
                    continue;

                _markers.Add(new MarkerData
                {
                    isFriendlyUnit = true,
                    friendlyIdentity = identity,
                    friendlyControl = identity.GetComponentInParent<ControleUnidade>(),
                    position = identity.transform.position,
                    category = CategoryForUnit(identity.tipoUnidade),
                    relation = RTSTacticalContactRelation.Friendly,
                    isEstimated = false
                });
            }
        }

        for (int i = 0; i < _contactSnapshots.Count && _markers.Count < MaxVisibleMarkers; i++)
        {
            RTSTacticalContactSnapshot contact = _contactSnapshots[i];
            if (!contact.HasPosition || contact.State == RTSTacticalContactState.Expired)
                continue;

            // Unidade própria já aparece pelo registro local. A verificação da equipe só é
            // feita quando o snapshot declara explicitamente identidade revelada.
            if (contact.IdentityRevealed && contact.TargetTeamId == _playerTeamId)
                continue;
            if (contact.Relation == RTSTacticalContactRelation.Friendly && !mostrarAliados)
                continue;
            if (contact.Relation != RTSTacticalContactRelation.Friendly && !mostrarInimigos)
                continue;

            _markers.Add(new MarkerData
            {
                isFriendlyUnit = false,
                contact = contact,
                position = contact.Position,
                category = contact.Category,
                relation = contact.Relation,
                isEstimated = contact.PositionIsEstimated || contact.State != RTSTacticalContactState.Current
            });
        }

        UpdateSelectedContactSnapshot();
    }

    private void UpdateMapViewport()
    {
        if (_mapImage == null || _mapViewport == null || _mapViewport.rect.width <= 1f || _mapViewport.rect.height <= 1f)
            return;

        if (_followPlayer && alvoJogador != null)
            SetMapCenterFromWorld(alvoJogador.position);
        if (!_hasMapCenter)
        {
            _viewCenterUv = new Vector2(0.5f, 0.5f);
            _hasMapCenter = true;
        }

        float aspect = Mathf.Max(0.2f, _mapViewport.rect.width / _mapViewport.rect.height);
        float maxWidth = GetMaximumViewWidth(aspect);
        _viewWidthMeters = Mathf.Clamp(_viewWidthMeters, MinimumViewWidthMeters, maxWidth);
        _viewUSpan = Mathf.Clamp01(_viewWidthMeters / _worldWidthMeters);
        _viewVSpan = Mathf.Clamp01((_viewWidthMeters / aspect) / _worldHeightMeters);
        float halfU = _viewUSpan * 0.5f;
        float halfV = _viewVSpan * 0.5f;
        _viewCenterUv.x = Mathf.Clamp(_viewCenterUv.x, halfU, 1f - halfU);
        _viewCenterUv.y = Mathf.Clamp(_viewCenterUv.y, halfV, 1f - halfV);
        _viewUMin = _viewCenterUv.x - halfU;
        _viewVMin = _viewCenterUv.y - halfV;

        // O mundo usa UV com origem no canto superior esquerdo; RawImage usa origem inferior.
        _mapImage.uvRect = new Rect(_viewUMin, 1f - (_viewVMin + _viewVSpan), _viewUSpan, _viewVSpan);
    }

    private void UpdateMarkerViews()
    {
        int required = Mathf.Min(_markers.Count, MaxVisibleMarkers);
        while (_markerViews.Count < required)
            _markerViews.Add(CreateMarkerView(_markerViews.Count));

        for (int i = 0; i < _markerViews.Count; i++)
        {
            MarkerView view = _markerViews[i];
            if (i >= required)
            {
                view.rect.gameObject.SetActive(false);
                view.rangeRect.gameObject.SetActive(false);
                continue;
            }

            MarkerData marker = _markers[i];
            ApplyMarkerStyle(view, marker);
            if (!TryWorldToMapUv(marker.position, out Vector2 uv)
                || uv.x < _viewUMin || uv.x > _viewUMin + _viewUSpan
                || uv.y < _viewVMin || uv.y > _viewVMin + _viewVSpan)
            {
                view.rangeRect.gameObject.SetActive(false);
                view.rect.gameObject.SetActive(false);
                continue;
            }

            float x = Mathf.InverseLerp(_viewUMin, _viewUMin + _viewUSpan, uv.x);
            float y = 1f - Mathf.InverseLerp(_viewVMin, _viewVMin + _viewVSpan, uv.y);
            view.rect.anchoredPosition = new Vector2(
                (x - 0.5f) * _mapViewport.rect.width,
                (y - 0.5f) * _mapViewport.rect.height);
            view.rect.gameObject.SetActive(true);

            bool showRange = marker.isFriendlyUnit && marker.friendlyIdentity != null
                && marker.friendlyIdentity.teamID == _playerTeamId
                && marker.friendlyControl != null
                && (_showAllFriendlyRanges || IsFriendlyUnitSelected(marker.friendlyControl));
            float attackRange = showRange ? marker.friendlyControl.ObterAlcanceAtaqueMaximo() : 0f;
            bool hasVisibleRange = attackRange > 0.01f && _viewWidthMeters > 1f;
            view.rangeRect.gameObject.SetActive(hasVisibleRange);
            if (hasVisibleRange)
            {
                float radiusPixels = attackRange / _viewWidthMeters * _mapViewport.rect.width;
                float diameter = Mathf.Clamp(radiusPixels * 2f, 2f, Mathf.Max(_mapViewport.rect.width, _mapViewport.rect.height) * 2f);
                view.rangeRect.sizeDelta = new Vector2(diameter, diameter);
            }
        }
    }

    private MarkerView CreateMarkerView(int index)
    {
        GameObject markerObject = CreateUiObject("ContactMarker_" + index, _mapViewport,
            typeof(Image), typeof(Button));
        RectTransform rect = markerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(18f, 18f);

        Image background = markerObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.08f, 0.10f, 0.92f);
        background.raycastTarget = true;

        Button button = markerObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        SetNoNavigation(button);
        button.onClick.AddListener(() => SelectMarker(index));

        GameObject rangeObject = CreateUiObject("AttackRange", markerObject.transform, typeof(Image));
        RectTransform rangeRect = rangeObject.GetComponent<RectTransform>();
        rangeRect.anchorMin = new Vector2(0.5f, 0.5f);
        rangeRect.anchorMax = new Vector2(0.5f, 0.5f);
        rangeRect.pivot = new Vector2(0.5f, 0.5f);
        rangeRect.sizeDelta = Vector2.zero;
        Image rangeImage = rangeObject.GetComponent<Image>();
        rangeImage.sprite = GetRangeRingSprite();
        rangeImage.color = new Color(0.30f, 0.92f, 0.84f, 0.42f);
        rangeImage.raycastTarget = false;

        Text glyph = CreateText("Glyph", markerObject.transform, "◇", 12, ColorUnknown,
            TextAnchor.MiddleCenter);
        Stretch(glyph.rectTransform, 0f, 0f, 0f, 0f);
        glyph.fontStyle = FontStyle.Bold;
        glyph.raycastTarget = false;

        return new MarkerView { rect = rect, background = background, glyph = glyph, button = button, rangeRect = rangeRect, rangeImage = rangeImage };
    }

    private void ApplyMarkerStyle(MarkerView view, MarkerData marker)
    {
        Color color;
        if (marker.isFriendlyUnit || marker.relation == RTSTacticalContactRelation.Friendly)
            color = ColorFriendly;
        else if (marker.relation == RTSTacticalContactRelation.Hostile)
            color = ColorHostile;
        else if (marker.relation == RTSTacticalContactRelation.Neutral)
            color = ColorNeutral;
        else if (marker.relation == RTSTacticalContactRelation.Civilian)
            color = ColorCivilian;
        else
            color = ColorUnknown;

        if (marker.isEstimated)
            color.a = 0.62f;

        view.background.color = new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 0.96f);
        view.glyph.color = color;
        view.glyph.text = GlyphForCategory(marker.category);
    }

    private void UpdatePlayerMarker()
    {
        if (_playerGlyph == null || alvoJogador == null)
        {
            if (_playerGlyph != null) _playerGlyph.gameObject.SetActive(false);
            return;
        }

        if (!TryWorldToMapUv(alvoJogador.position, out Vector2 uv)
            || uv.x < _viewUMin || uv.x > _viewUMin + _viewUSpan
            || uv.y < _viewVMin || uv.y > _viewVMin + _viewVSpan)
        {
            _playerGlyph.gameObject.SetActive(false);
            return;
        }

        float x = Mathf.InverseLerp(_viewUMin, _viewUMin + _viewUSpan, uv.x);
        float y = 1f - Mathf.InverseLerp(_viewVMin, _viewVMin + _viewVSpan, uv.y);
        _playerGlyph.rectTransform.anchoredPosition = new Vector2(
            (x - 0.5f) * _mapViewport.rect.width,
            (y - 0.5f) * _mapViewport.rect.height);
        _playerGlyph.gameObject.SetActive(true);
    }

    private void UpdateScaleReadout()
    {
        if (_scaleText == null || _mapViewport == null)
            return;

        float scaleKm = _viewWidthMeters * 0.22f / 1000f;
        _scaleText.text = FormatKilometres(scaleKm);
        if (_scaleBarRect != null)
            _scaleBarRect.sizeDelta = new Vector2(Mathf.Max(24f, _mapViewport.rect.width * 0.22f), 2f);
        if (_rangeText != null)
            _rangeText.text = "N ↑  " + FormatKilometres(_viewWidthMeters / 1000f);
    }

    private void UpdateSelectionDetails()
    {
        if (_detailsText == null)
            return;

        _detailBuilder.Length = 0;
        if (_hasSelectedUnit && _selectedUnit != null)
        {
            string unitName = !string.IsNullOrWhiteSpace(_selectedUnit.nomeDeBatismo)
                ? _selectedUnit.nomeDeBatismo
                : _selectedUnit.gameObject.name;
            _detailBuilder.Append("AMIGO  ·  ").Append(unitName).Append("  ·  ")
                .Append(CategoryLabel(CategoryForUnit(_selectedUnit.tipoUnidade)));
        }
        else if (_hasSelectedContact)
        {
            AppendContactDetails(_selectedContact);
        }
        else
        {
            _detailBuilder.Append("SEM SELEÇÃO  ·  CONTATOS CONFORME DETECÇÃO");
        }

        _detailsText.text = _detailBuilder.ToString();
    }

    private void AppendContactDetails(RTSTacticalContactSnapshot contact)
    {
        bool mayShowIdentity = contact.IdentityRevealed && !string.IsNullOrWhiteSpace(contact.IdentificationLabel);
        _detailBuilder.Append(mayShowIdentity ? contact.IdentificationLabel : CategoryLabel(contact.Category));
        _detailBuilder.Append("  ·  ").Append(RelationLabel(contact.Relation));
        _detailBuilder.Append("  ·  ").Append(StateLabel(contact.State));

        Vector3 referencePosition = alvoJogador != null ? alvoJogador.position : _lastPlayerPosition;
        if (contact.HasPosition && (alvoJogador != null || _lastPlayerPosition != Vector3.zero))
        {
            Vector3 delta = contact.Position - referencePosition;
            delta.y = 0f;
            float distanceKm = delta.magnitude / 1000f;
            float bearing = Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360f);
            _detailBuilder.Append("\n").Append(distanceKm.ToString("0.0"))
                .Append(" km  ·  BRG ").Append(bearing.ToString("000"));
        }

        if (contact.HasEstimatedVelocity)
        {
            Vector3 velocity = contact.EstimatedVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.01f)
                _detailBuilder.Append("  ·  ").Append((velocity.magnitude * 1.94384f).ToString("0"))
                    .Append(" kt");
        }

        if (contact.HasVerticalMeasurement)
        {
            _detailBuilder.Append("  ·  ")
                .Append(contact.VerticalKind == RTSTacticalVerticalKind.Depth ? "DEP " : "ALT ")
                .Append(contact.VerticalValueMeters.ToString("0"))
                .Append(" m");
        }

        if (!mayShowIdentity && contact.Identification != RTSTacticalContactIdentification.Unknown)
            _detailBuilder.Append("  ·  ").Append(contact.Identification == RTSTacticalContactIdentification.Probable
                ? "PROVÁVEL" : "IDENTIFICADO");
        if (contact.PositionIsEstimated || contact.State != RTSTacticalContactState.Current)
            _detailBuilder.Append("  ·  POSIÇÃO ESTIMADA");
    }

    private void UpdateSelectedContactSnapshot()
    {
        if (!_hasSelectedContact || string.IsNullOrEmpty(_selectedContactId))
            return;

        for (int i = 0; i < _contactSnapshots.Count; i++)
        {
            if (!string.Equals(_contactSnapshots[i].ContactId, _selectedContactId, StringComparison.Ordinal))
                continue;

            _selectedContact = _contactSnapshots[i];
            return;
        }

        _hasSelectedContact = false;
        _selectedContactId = null;
    }

    private void SelectMarker(int index)
    {
        if (index < 0 || index >= _markers.Count)
            return;

        MarkerData marker = _markers[index];
        if (marker.isFriendlyUnit)
        {
            _selectedUnit = marker.friendlyIdentity;
            _hasSelectedUnit = _selectedUnit != null;
            _hasSelectedContact = false;
            _selectedContactId = null;

            if (_selectedUnit != null)
            {
                ControleUnidade control = _selectedUnit.GetComponentInParent<ControleUnidade>();
                if (control != null)
                {
                    if (_selectionManager == null)
                        _selectionManager = FindFirstObjectByType<GerenteSelecao>();
                    if (_selectionManager != null)
                    {
                        _selectionManager.DeselecionarTudo();
                        _selectionManager.AdicionarSelecao(control);
                    }
                }
            }
        }
        else
        {
            _selectedContact = marker.contact;
            _selectedContactId = marker.contact.ContactId;
            _hasSelectedContact = !string.IsNullOrEmpty(_selectedContactId);
            _hasSelectedUnit = false;
            _selectedUnit = null;
        }

        UpdateSelectionDetails();
    }

    private Sprite GetRangeRingSprite()
    {
        if (_rangeRingSprite != null) return _rangeRingSprite;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.name = "MiniMapa_RangeRing";
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.48f;
        float inner = size * 0.45f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            byte alpha = distance >= inner && distance <= outer ? (byte)255 : (byte)0;
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _rangeRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _rangeRingSprite.name = "MiniMapa_RangeRing";
        _rangeRingSprite.hideFlags = HideFlags.HideAndDontSave;
        return _rangeRingSprite;
    }

    private bool IsFriendlyUnitSelected(ControleUnidade control)
    {
        if (control == null) return false;
        if (_selectionManager == null) _selectionManager = FindFirstObjectByType<GerenteSelecao>();
        if (_selectionManager == null || _selectionManager.unidadesSelecionadas == null) return false;
        return _selectionManager.unidadesSelecionadas.Contains(control);
    }

    private void ToggleFriendlyRanges()
    {
        _showAllFriendlyRanges = !_showAllFriendlyRanges;
        UpdateFilterButtons();
        _nextRefreshAt = 0f;
    }

    private void ToggleAlliesFilter()
    {
        mostrarAliados = !mostrarAliados;
        UpdateFilterButtons();
        _nextRefreshAt = 0f;
    }

    private void ToggleContactsFilter()
    {
        mostrarInimigos = !mostrarInimigos;
        UpdateFilterButtons();
        _nextRefreshAt = 0f;
    }

    private void ZoomIn()
    {
        _followPlayer = false;
        _viewWidthMeters = Mathf.Max(MinimumViewWidthMeters, _viewWidthMeters * 0.72f);
        _nextRefreshAt = 0f;
    }

    private void ZoomOut()
    {
        _followPlayer = false;
        _viewWidthMeters = Mathf.Min(GetMaximumViewWidth(), _viewWidthMeters / 0.72f);
        _nextRefreshAt = 0f;
    }

    private void RecenterOnPlayer()
    {
        _followPlayer = true;
        if (alvoJogador != null)
            SetMapCenterFromWorld(alvoJogador.position);
        _nextRefreshAt = 0f;
    }

    private void CenterOnClickedMapPoint()
    {
        if (_mapViewport == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _mapViewport, Input.mousePosition, null, out Vector2 localPoint))
            return;

        float x = Mathf.Clamp01(localPoint.x / Mathf.Max(1f, _mapViewport.rect.width) + 0.5f);
        float y = Mathf.Clamp01(localPoint.y / Mathf.Max(1f, _mapViewport.rect.height) + 0.5f);
        _viewCenterUv = new Vector2(_viewUMin + x * _viewUSpan,
            _viewVMin + (1f - y) * _viewVSpan);
        _hasMapCenter = true;
        _followPlayer = false;
        _nextRefreshAt = 0f;
    }

    private void UpdateFilterButtons()
    {
        if (_alliesButtonText != null)
            _alliesButtonText.text = mostrarAliados ? "ALIADOS ●" : "ALIADOS ○";
        if (_contactsButtonText != null)
            _contactsButtonText.text = mostrarInimigos ? "TRILHAS ●" : "TRILHAS ○";
        if (_alliesButton != null)
            _alliesButton.interactable = true;
        if (_contactsButton != null)
            _contactsButton.interactable = true;
        if (_rangeFilterText != null)
            _rangeFilterText.text = _showAllFriendlyRanges ? "RAIOS ●" : "RAIOS ○";
        if (_rangeFilterButton != null)
            _rangeFilterButton.interactable = true;
    }

    private void SetMapCenterFromWorld(Vector3 position)
    {
        _lastPlayerPosition = position;
        if (TryWorldToMapUv(position, out Vector2 uv))
        {
            _viewCenterUv = uv;
            _hasMapCenter = true;
        }
    }

    private bool TryWorldToMapUv(Vector3 position, out Vector2 uv)
    {
        if (_worldDefinition != null)
        {
            bool inside;
            return _worldDefinition.TryWorldToMapUv(position, out uv, out inside) && inside;
        }

        if (_territoryManager == null)
            _territoryManager = GerenteDeTerritorio.Instancia;
        if (_territoryManager != null)
            return _territoryManager.TryWorldToMapUv(position, out uv, out _);

        uv = new Vector2(position.x / _worldWidthMeters + 0.5f,
            0.5f - position.z / _worldHeightMeters);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private float GetMaximumViewWidth(float aspect = 1.5f)
    {
        float horizontalLimit = Mathf.Max(MinimumViewWidthMeters, _worldWidthMeters);
        float verticalLimit = Mathf.Max(MinimumViewWidthMeters, _worldHeightMeters * Mathf.Max(0.2f, aspect));
        return Mathf.Max(MinimumViewWidthMeters, Mathf.Min(horizontalLimit, verticalLimit));
    }

    private static int CompareContactPriority(RTSTacticalContactSnapshot a, RTSTacticalContactSnapshot b)
    {
        int state = ContactStatePriority(b.State).CompareTo(ContactStatePriority(a.State));
        if (state != 0) return state;
        int confidence = b.Confidence.CompareTo(a.Confidence);
        if (confidence != 0) return confidence;
        return b.LastSeenAt.CompareTo(a.LastSeenAt);
    }

    private static int ContactStatePriority(RTSTacticalContactState state)
    {
        switch (state)
        {
            case RTSTacticalContactState.Current: return 3;
            case RTSTacticalContactState.Estimated: return 2;
            case RTSTacticalContactState.LastKnown: return 1;
            default: return 0;
        }
    }

    private static RTSTacticalContactCategory CategoryForUnit(TipoUnidade type)
    {
        switch (type)
        {
            case TipoUnidade.Naval: return RTSTacticalContactCategory.Naval;
            case TipoUnidade.Aereo: return RTSTacticalContactCategory.Aerial;
            case TipoUnidade.Estrutura: return RTSTacticalContactCategory.Structure;
            default: return RTSTacticalContactCategory.Ground;
        }
    }

    private static string GlyphForCategory(RTSTacticalContactCategory category)
    {
        switch (category)
        {
            case RTSTacticalContactCategory.Naval: return "▲";
            case RTSTacticalContactCategory.Submarine: return "▼";
            case RTSTacticalContactCategory.Aerial: return "●";
            case RTSTacticalContactCategory.Missile: return "✦";
            case RTSTacticalContactCategory.Ground: return "■";
            case RTSTacticalContactCategory.Structure: return "▣";
            default: return "◇";
        }
    }

    private static string CategoryLabel(RTSTacticalContactCategory category)
    {
        switch (category)
        {
            case RTSTacticalContactCategory.Naval: return "NAVAL";
            case RTSTacticalContactCategory.Submarine: return "SUBMARINO";
            case RTSTacticalContactCategory.Aerial: return "AÉREO";
            case RTSTacticalContactCategory.Ground: return "TERRESTRE";
            case RTSTacticalContactCategory.Structure: return "ESTRUTURA";
            case RTSTacticalContactCategory.Missile: return "MÍSSIL";
            case RTSTacticalContactCategory.SensorReport: return "RELATO SENSORIAL";
            default: return "DESCONHECIDO";
        }
    }

    private static string RelationLabel(RTSTacticalContactRelation relation)
    {
        switch (relation)
        {
            case RTSTacticalContactRelation.Friendly: return "AMIGO";
            case RTSTacticalContactRelation.Hostile: return "HOSTIL";
            case RTSTacticalContactRelation.Neutral: return "NEUTRO";
            case RTSTacticalContactRelation.Civilian: return "CIVIL";
            default: return "NÃO IDENTIFICADO";
        }
    }

    private static string StateLabel(RTSTacticalContactState state)
    {
        switch (state)
        {
            case RTSTacticalContactState.Current: return "ATUAL";
            case RTSTacticalContactState.Estimated: return "ESTIMADO";
            case RTSTacticalContactState.LastKnown: return "ÚLTIMA POSIÇÃO";
            default: return "EXPIRADO";
        }
    }

    private static string FormatKilometres(float kilometres)
    {
        return kilometres >= 100f ? kilometres.ToString("0") + " km" : kilometres.ToString("0.0") + " km";
    }

    private Button CreateButton(string name, Transform parent, string label, float width, float x, float y,
        out Text labelText, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 26f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(35, 51, 58, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        SetNoNavigation(button);
        button.onClick.AddListener(callback);

        labelText = CreateText("Label", buttonObject.transform, label, 9, ColorFriendly,
            TextAnchor.MiddleCenter);
        Stretch(labelText.rectTransform, 3f, 1f, 3f, 1f);
        labelText.fontStyle = FontStyle.Bold;
        labelText.raycastTarget = false;
        return button;
    }

    private static void SetNoNavigation(Selectable selectable)
    {
        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.None;
        selectable.navigation = navigation;
    }

    private static bool HasNamedComponentInParents(Transform current, string componentName)
    {
        while (current != null)
        {
            if (current.GetComponent(componentName) != null)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        if (components != null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                Type component = components[i];
                if (component == null || component == typeof(RectTransform))
                    continue;
                obj.AddComponent(component);
            }
        }
        return obj;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize,
        Color color, TextAnchor alignment)
    {
        GameObject obj = CreateUiObject(name, parent, typeof(RectTransform), typeof(Text));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public void RegistrarUnidadeNoMapa(Transform unidade, bool ehInimigo)
    {
        // API legada para LancadorNaval/ControleSubmarino. Nunca exibimos a
        // Transform recebida: para contatos não próprios, o adaptador é a fonte segura.
        if (unidade == null)
            return;
        _nextRefreshAt = 0f;
    }

    private void OnDestroy()
    {
        UnbindContactAdapter();

        if (_chartMaterial != null)
        {
            Destroy(_chartMaterial);
            _chartMaterial = null;
        }

        if (_createdEventSystem && _ownedEventSystemObject != null)
            Destroy(_ownedEventSystemObject);

        if (_createdUiRoot && _uiRoot != null)
            Destroy(_uiRoot);
        else if (_panelObject != null)
            Destroy(_panelObject);
    }
}
