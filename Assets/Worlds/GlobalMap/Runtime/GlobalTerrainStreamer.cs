using System.Collections;
using System.Collections.Generic;
using System.Text;
using PackageNavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

/// <summary>
/// Keeps a coarse, collidable world Terrain available everywhere and streams
/// higher-resolution Terrain tiles around the active camera near the ground.
/// The camera is also the anchor while it follows a selected unit.
/// </summary>
public sealed class GlobalTerrainStreamer : MonoBehaviour
{
    private const int LocalNavMeshAgentTypeId = 0;
    private const int LocalNavMeshTileSize = 256;
    private const float LocalNavMeshVoxelSize = 4f;
    private const int LandTileDetectionSamplesPerAxis = 64;
    private const int MaximumCachedTerrainTiles = 9;
    private const int MaximumTerrainRowsPerFrame = 8;
    private const float AltitudeModeHysteresis = 1000f;

    private static readonly ProfilerMarker BuildDesiredSetMarker = new ProfilerMarker("GlobalTerrainStreamer.BuildDesiredSet");
    private static readonly ProfilerMarker ApplyVisibleTilesMarker = new ProfilerMarker("GlobalTerrainStreamer.ApplyVisibleTiles");
    private static readonly ProfilerMarker ScheduleNavMeshUpdateMarker = new ProfilerMarker("GlobalTerrainStreamer.ScheduleNavMeshUpdate");
    private static readonly ProfilerMarker SwapNavMeshDataMarker = new ProfilerMarker("GlobalTerrainStreamer.SwapNavMeshData");
    private static readonly ProfilerMarker ValidateNavMeshMarker = new ProfilerMarker("GlobalTerrainStreamer.ValidateNavMesh");
    private static readonly ProfilerMarker GenerateHeightRowsMarker = new ProfilerMarker("GlobalTerrainStreamer.GenerateHeightRows");
    private static readonly ProfilerMarker ApplyHeightRowsMarker = new ProfilerMarker("GlobalTerrainStreamer.ApplyHeightRows");
    private static readonly ProfilerMarker SyncHeightmapMarker = new ProfilerMarker("GlobalTerrainStreamer.SyncHeightmap");
    private static readonly ProfilerMarker GenerateAlphamapRowsMarker = new ProfilerMarker("GlobalTerrainStreamer.GenerateAlphamapRows");
    private static readonly ProfilerMarker ApplyAlphamapRowsMarker = new ProfilerMarker("GlobalTerrainStreamer.ApplyAlphamapRows");
    private static readonly ProfilerMarker GenerateVegetationRowMarker = new ProfilerMarker("GlobalTerrainStreamer.GenerateVegetationRow");
    private static readonly ProfilerMarker BuildVegetationInstancesMarker = new ProfilerMarker("GlobalTerrainStreamer.BuildVegetationInstances");
    private static readonly ProfilerMarker ApplyVegetationInstancesMarker = new ProfilerMarker("GlobalTerrainStreamer.ApplyVegetationInstances");
    private static readonly ProfilerMarker CreateTerrainObjectMarker = new ProfilerMarker("GlobalTerrainStreamer.CreateTerrainObject");
    private static readonly ProfilerMarker ConfigureTerrainObjectMarker = new ProfilerMarker("GlobalTerrainStreamer.ConfigureTerrainObject");
    private static readonly ProfilerMarker RemoveStaleTilesMarker = new ProfilerMarker("GlobalTerrainStreamer.RemoveStaleTiles");
    private static readonly ProfilerMarker BuildTileSetSignatureMarker = new ProfilerMarker("GlobalTerrainStreamer.BuildTileSetSignature");
    private static readonly ProfilerMarker PrepareNavMeshUpdateMarker = new ProfilerMarker("GlobalTerrainStreamer.PrepareNavMeshUpdate");
    private static readonly ProfilerMarker InitializeTerrainDataMarker = new ProfilerMarker("GlobalTerrainStreamer.InitializeTerrainData");
    private static readonly ProfilerMarker AllocateHeightBufferMarker = new ProfilerMarker("GlobalTerrainStreamer.AllocateHeightBuffer");
    private static readonly ProfilerMarker AllocateHeightRowBufferMarker = new ProfilerMarker("GlobalTerrainStreamer.AllocateHeightRowBuffer");
    private static readonly ProfilerMarker AllocateAlphamapRowBufferMarker = new ProfilerMarker("GlobalTerrainStreamer.AllocateAlphamapRowBuffer");
    private static readonly ProfilerMarker PrepareVegetationPrototypesMarker = new ProfilerMarker("GlobalTerrainStreamer.PrepareVegetationPrototypes");

    public GlobalWorldDefinition world;
    public Terrain globalTerrain;
    public Camera cameraOverride;
    [Min(1000f)] public float detailTerrainBelowAltitude = 20000f;
    [Range(1, 2)] public int tileRadius = 1;
    [Min(0.1f)] public float refreshInterval = 0.5f;
    [Range(1, 4)] public int tilesCreatedPerFrame = 1;
    [Range(1, 64)] public int terrainRowsPerFrame = 2;
    [Range(1, 64)] public int alphamapRowsPerFrame = 1;
    [Range(1, 16)] public int vegetationRowsPerFrame = 1;
    public bool showDiagnostics = true;

    // A tela de carregamento pode dedicar um pouco mais de CPU para preparar
    // o terreno inicial; depois da prontidão, o streaming volta ao limite normal.
    private const double MaxBudgetMsPerFrame = 3.5;
    private const double InitialLoadMaxBudgetMsPerFrame = 8.0;

    private double CurrentWorkBudgetMsPerFrame => initialStartupTimingActive
        ? InitialLoadMaxBudgetMsPerFrame
        : MaxBudgetMsPerFrame;

    private readonly Dictionary<Vector2Int, Terrain> loadedTiles = new Dictionary<Vector2Int, Terrain>(16);
    private readonly Dictionary<Vector2Int, int> tileLastUsedOrder = new Dictionary<Vector2Int, int>(16);
    private readonly HashSet<Vector2Int> desiredTiles = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> removalBuffer = new List<Vector2Int>(16);
    private readonly List<Vector2Int> tileSignatureBuffer = new List<Vector2Int>(16);
    private readonly float[] alphaWeights = new float[6];
    private float[,] heightBufferScratch;
    private float[,] fullHeightRowBuffer;
    private float[,] trailingHeightRowBuffer;
    private float[,,] fullAlphaRowBuffer;
    private float[,,] trailingAlphaRowBuffer;
    private Coroutine streamingLoop;
    private Camera farClipCamera;
    private float farClipBeforeLocalStreaming;
    private bool localFarClipLimited;
    private int tileUseOrder;
    private int lastGenerationMilliseconds;
    private int lastLocalNavMeshBuildMilliseconds;
    private bool initialStartupTimingActive;
    private int initialStartupTilesCreated;
    private long initialStartupTileElapsedTicks;
    private long initialStartupTileWorkTicks;
    private PackageNavMeshSurface localNavMeshSurface;
    private TerrainCollider globalTerrainCollider;
    private NavMeshData runtimeLocalNavMeshData;
    private string committedNavMeshTileSet = string.Empty;
    private string failedNavMeshTileSet;
    private bool altitudeModeInitialized;
    private bool macroTerrainMode;
    private bool initialStreamingViewResolved;
    private bool initialNavigationBuildResolved;
    private bool initialNavigationBuildSucceeded;
    private bool localTerrainSuspended;
    private GerenteSelecao gerenteSelecaoCache;
    private float proximaBuscaGerenteSelecao;

    public int ActiveTileCount
    {
        get
        {
            int count = 0;
            foreach (Vector2Int coordinate in desiredTiles)
            {
                if (loadedTiles.TryGetValue(coordinate, out Terrain terrain)
                    && terrain != null && terrain.enabled && terrain.gameObject.activeInHierarchy)
                    count++;
            }
            return count;
        }
    }
    public int DesiredTileCount => desiredTiles.Count;
    /// <summary>
    /// True when either the coarse terrain is visible in macro/fallback view,
    /// or the initial local tile set around the camera is active.
    /// This is a visual startup signal; NavMesh and background systems may
    /// continue their own work afterwards.
    /// </summary>
    public bool InitialWorldSurfaceReady
    {
        get
        {
            if (globalTerrain == null || !initialStreamingViewResolved)
                return false;

            bool coarseTerrainVisible = globalTerrain.enabled
                && globalTerrain.gameObject.activeInHierarchy;

            if (macroTerrainMode || desiredTiles.Count == 0)
                return coarseTerrainVisible;

            bool initialLocalTilesVisible = AllDesiredTilesLoaded()
                && ActiveTileCount == desiredTiles.Count;
            if (initialLocalTilesVisible)
                return true;

            // A failed local NavMesh build hides those tile renderers and
            // restores the coarse terrain. That visual fallback is sufficient
            // for the loading screen; gameplay navigation can recover later.
            return coarseTerrainVisible
                && !string.IsNullOrEmpty(failedNavMeshTileSet)
                && failedNavMeshTileSet == BuildDesiredTileSetSignature();
        }
    }
    /// <summary>
    /// True once the initial local navigation build has completed, or the
    /// existing coarse-terrain fallback has been selected. This is separate
    /// from visual readiness so the loading flow does not release units while
    /// their starting NavMesh is still being generated.
    /// </summary>
    public bool InitialNavigationBuildResolved
    {
        get
        {
            if (!initialStreamingViewResolved)
                return false;

            return macroTerrainMode || desiredTiles.Count == 0 || initialNavigationBuildResolved;
        }
    }
    public bool InitialNavigationAvailable
    {
        get
        {
            if (!initialStreamingViewResolved)
                return false;

            return macroTerrainMode || desiredTiles.Count == 0
                || (initialNavigationBuildResolved && initialNavigationBuildSucceeded);
        }
    }
    public int CachedTileCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<Vector2Int, Terrain> pair in loadedTiles)
            {
                Terrain terrain = pair.Value;
                if (!desiredTiles.Contains(pair.Key) && terrain != null
                    && !terrain.gameObject.activeInHierarchy)
                    count++;
            }
            return count;
        }
    }
    public bool IsMacroTerrainMode => macroTerrainMode;
    public int ActiveTreeCount
    {
        get
        {
            int count = 0;
            foreach (Vector2Int coordinate in desiredTiles)
            {
                if (loadedTiles.TryGetValue(coordinate, out Terrain terrain)
                    && terrain != null && terrain.enabled && terrain.gameObject.activeInHierarchy
                    && terrain.terrainData != null)
                    count += terrain.terrainData.treeInstanceCount;
            }
            return count;
        }
    }
    public static GlobalTerrainStreamer Instance { get; private set; }

    public bool TrySampleWorldHeight(Vector3 position, out float surfaceHeight)
    {
        surfaceHeight = position.y;
        if (world == null
            || position.x < world.MapMinX || position.x > world.MapMaxX
            || position.z < world.MapMinZ || position.z > world.MapMaxZ)
        {
            return false;
        }

        surfaceHeight = world.HeightAtWorld(position.x, position.z);
        return true;
    }

    public bool TrySampleLandHeight(Vector3 position, out float surfaceHeight)
    {
        surfaceHeight = position.y;
        if (world == null
            || position.x < world.MapMinX || position.x > world.MapMaxX
            || position.z < world.MapMinZ || position.z > world.MapMaxZ)
        {
            return false;
        }

        if (!world.IsLandAtWorld(position.x, position.z))
        {
            return false;
        }

        surfaceHeight = world.HeightAtWorld(position.x, position.z);
        return true;
    }

    public int LastGenerationMilliseconds => lastGenerationMilliseconds;
    public int LastLocalNavMeshBuildMilliseconds => lastLocalNavMeshBuildMilliseconds;

    /// <summary>
    /// Classifies the global map from its authoritative land mask instead of
    /// comparing the overlapping ocean collider and terrain heights.
    /// </summary>
    public bool TryClassifyWorldSurface(
        Vector3 position,
        out ClassificacaoSuperficieMapa classification,
        out float surfaceHeight,
        float coastTolerance = 1.5f)
    {
        classification = ClassificacaoSuperficieMapa.Desconhecida;
        surfaceHeight = position.y;
        if (world == null
            || position.x < world.MapMinX || position.x > world.MapMaxX
            || position.z < world.MapMinZ || position.z > world.MapMaxZ)
        {
            return false;
        }

        float coastDistance = world.SignedCoastDistanceAtWorld(position.x, position.z);
        if (coastDistance < 0f)
            classification = ClassificacaoSuperficieMapa.Agua;
        else if (coastDistance <= Mathf.Max(0f, coastTolerance))
            classification = ClassificacaoSuperficieMapa.Costa;
        else
            classification = ClassificacaoSuperficieMapa.Chao;

        surfaceHeight = world.HeightAtWorld(position.x, position.z);
        return true;
    }

    /// <summary>
    /// Tells map click handling to ignore the hidden coarse Terrain collider
    /// when a visible streamed tile covers the same world point. The coarse
    /// collider remains available as a fallback outside the local tile set.
    /// </summary>
    public bool ShouldSkipGlobalTerrainRaycastHit(Collider collider, Vector3 worldPoint)
    {
        if (world == null || globalTerrain == null || collider == null || globalTerrain.enabled)
            return false;

        if (globalTerrainCollider == null || globalTerrainCollider.gameObject != globalTerrain.gameObject)
            globalTerrainCollider = globalTerrain.GetComponent<TerrainCollider>();

        if (globalTerrainCollider == null || collider != globalTerrainCollider)
            return false;

        foreach (Vector2Int coordinate in desiredTiles)
        {
            if (!loadedTiles.TryGetValue(coordinate, out Terrain localTerrain)
                || localTerrain == null || !localTerrain.enabled || !localTerrain.gameObject.activeInHierarchy)
            {
                continue;
            }

            TerrainCollider localCollider = localTerrain.GetComponent<TerrainCollider>();
            if (localCollider == null || !localCollider.enabled)
                continue;

            Bounds bounds = localCollider.bounds;
            if (worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
                && worldPoint.z >= bounds.min.z && worldPoint.z <= bounds.max.z)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        Instance = this;
        altitudeModeInitialized = false;
        macroTerrainMode = false;
        initialStreamingViewResolved = false;
        localTerrainSuspended = false;
        initialStartupTimingActive = true;
        initialStartupTilesCreated = 0;
        initialStartupTileElapsedTicks = 0;
        initialStartupTileWorkTicks = 0;
        if (Application.isPlaying)
            EnsureLocalNavMeshSurface();

        streamingLoop = StartCoroutine(StreamContinuously());
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (streamingLoop != null)
        {
            StopCoroutine(streamingLoop);
            streamingLoop = null;
        }

        // Tiles are runtime children and must not survive a play-mode or
        // script-domain restart, where this component's dictionaries reset.
        if (Application.isPlaying)
        {
            RemoveAllTiles();
            if (globalTerrain != null)
                globalTerrain.enabled = true;
        }
        initialStartupTimingActive = false;
        heightBufferScratch = null;
        fullHeightRowBuffer = null;
        trailingHeightRowBuffer = null;
        fullAlphaRowBuffer = null;
        trailingAlphaRowBuffer = null;
        altitudeModeInitialized = false;
        macroTerrainMode = false;
        localTerrainSuspended = false;

        if (localFarClipLimited && farClipCamera != null)
            farClipCamera.farClipPlane = farClipBeforeLocalStreaming;
        localFarClipLimited = false;
        farClipCamera = null;
    }

    private void LateUpdate()
    {
        Camera camera = cameraOverride != null ? cameraOverride : Camera.main;
        if (camera == null || world == null)
        {
            if (localFarClipLimited && farClipCamera != null)
                farClipCamera.farClipPlane = farClipBeforeLocalStreaming;
            localFarClipLimited = false;
            farClipCamera = null;
            return;
        }

        UpdateAltitudeMode(camera);
        if (macroTerrainMode)
        {
            // CameraController recalculates its global far plane as the camera
            // crosses this threshold. Leave that value untouched in macro view.
            localFarClipLimited = false;
            farClipCamera = camera;
            return;
        }

        // The visual camera may be away from the selected moving unit whose
        // nearby tiles are currently streamed for navigation.
        AtualizarVisibilidadeTerrenoGlobal(camera);
        AtualizarDistanciaVegetacaoLocal(camera);

        if (farClipCamera != camera)
        {
            if (localFarClipLimited && farClipCamera != null)
                farClipCamera.farClipPlane = farClipBeforeLocalStreaming;

            farClipCamera = camera;
            localFarClipLimited = false;
        }

        if (!localFarClipLimited)
        {
            farClipBeforeLocalStreaming = camera.farClipPlane;
            localFarClipLimited = true;
        }

        float tileSize = Mathf.Max(100f, world.terrainTileSize);
        float cellX = Mathf.Floor((camera.transform.position.x - world.MapMinX) / tileSize);
        float cellZ = Mathf.Floor((camera.transform.position.z - world.MapMinZ) / tileSize);
        float coverageMinX = world.MapMinX + (cellX - tileRadius) * tileSize;
        float coverageMaxX = world.MapMinX + (cellX + tileRadius + 1f) * tileSize;
        float coverageMinZ = world.MapMinZ + (cellZ - tileRadius) * tileSize;
        float coverageMaxZ = world.MapMinZ + (cellZ + tileRadius + 1f) * tileSize;
        float nearestTileEdge = Mathf.Min(
            camera.transform.position.x - coverageMinX,
            coverageMaxX - camera.transform.position.x,
            camera.transform.position.z - coverageMinZ,
            coverageMaxZ - camera.transform.position.z);
        // The loaded-tile boundary is horizontal distance in XZ, while the
        // camera far plane measures distance along its view ray. Divide by
        // the ray's horizontal component so a steep aerial view can reach the
        // ground inside the same 3x3 streamed area instead of clipping above it.
        Vector3 horizontalForward = camera.transform.forward;
        horizontalForward.y = 0f;
        float horizontalViewFraction = Mathf.Max(0.15f, horizontalForward.magnitude);
        float coverageLimit = Mathf.Max(3500f, nearestTileEdge * 0.96f / horizontalViewFraction);
        float altitudeLimit = Mathf.Max(3500f, camera.transform.position.y * 1.45f + tileSize * 0.45f);
        float desiredFarClip = Mathf.Min(coverageLimit, altitudeLimit);
        camera.farClipPlane = Mathf.Min(farClipBeforeLocalStreaming, desiredFarClip);
    }

    private void UpdateAltitudeMode(Camera camera)
    {
        if (camera == null)
            return;

        bool previousMode = macroTerrainMode;
        float altitude = camera.transform.position.y;
        if (!altitudeModeInitialized)
        {
            macroTerrainMode = altitude >= detailTerrainBelowAltitude;
            altitudeModeInitialized = true;
        }
        else if (macroTerrainMode)
        {
            if (altitude <= detailTerrainBelowAltitude - AltitudeModeHysteresis)
                macroTerrainMode = false;
        }
        else if (altitude >= detailTerrainBelowAltitude + AltitudeModeHysteresis)
        {
            macroTerrainMode = true;
        }

        if (previousMode != macroTerrainMode)
            localTerrainSuspended = false;
    }

    private IEnumerator StreamContinuously()
    {
        // Let the existing surface initializer inspect the scene while the
        // coarse Terrain is inactive; its rectangular bounds cover seabed too.
        yield return null;

        if (world == null || globalTerrain == null || globalTerrain.terrainData == null)
        {
            Debug.LogError("[GlobalMap] World definition ou Terrain global ausente.", this);
            yield break;
        }

        try
        {
            world.InitializeRuntimeData();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            yield break;
        }

        ConfigureTerrainRendering(globalTerrain, false);
        float nextRefresh = 0f;
        while (isActiveAndEnabled)
        {
            Camera camera = cameraOverride != null ? cameraOverride : Camera.main;
            if (camera == null)
            {
                globalTerrain.gameObject.SetActive(true);
                globalTerrain.enabled = true;
                initialStreamingViewResolved = true;
                if (!localTerrainSuspended)
                {
                    SuspendLocalTerrainForMacroView();
                    localTerrainSuspended = true;
                }
                RegistrarTempoInicialSePronto();
                yield return new WaitForSecondsRealtime(refreshInterval);
                continue;
            }

            UpdateAltitudeMode(camera);
            if (macroTerrainMode)
            {
                globalTerrain.gameObject.SetActive(true);
                globalTerrain.enabled = true;
                initialStreamingViewResolved = true;
                if (!localTerrainSuspended)
                {
                    SuspendLocalTerrainForMacroView();
                    localTerrainSuspended = true;
                }
                RegistrarTempoInicialSePronto();
                yield return new WaitForSecondsRealtime(refreshInterval);
                continue;
            }

            localTerrainSuspended = false;

            globalTerrain.gameObject.SetActive(true);

            if (Time.unscaledTime >= nextRefresh)
            {
                Vector3 streamingAnchor = ResolverAncoraDeStreaming(camera);
                using (BuildDesiredSetMarker.Auto())
                    BuildDesiredSet(streamingAnchor);
                initialStreamingViewResolved = true;
                nextRefresh = Time.unscaledTime + refreshInterval;
            }

            int created = 0;
            foreach (Vector2Int coordinate in desiredTiles)
            {
                if (loadedTiles.ContainsKey(coordinate))
                    continue;

                Terrain terrain = null;
                long generationWorkTicks = 0;
                long tileStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                yield return CreateLocalTileIncremental(
                    coordinate,
                    createdTerrain => terrain = createdTerrain,
                    workTicks => generationWorkTicks += workTicks);
                long tileElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - tileStartedTicks;
                lastGenerationMilliseconds = (int)(generationWorkTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                if (initialStartupTimingActive)
                {
                    initialStartupTilesCreated++;
                    initialStartupTileElapsedTicks += tileElapsedTicks;
                    initialStartupTileWorkTicks += generationWorkTicks;
                    Debug.Log("[GlobalTerrainStreamer][StartupTiming] tile=" + coordinate
                        + " elapsedMs=" + (tileElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F1")
                        + " workMs=" + lastGenerationMilliseconds
                        + " success=" + (terrain != null));
                }
                if (terrain != null)
                {
                    loadedTiles.Add(coordinate, terrain);
                    TouchTile(coordinate);
                }

                created++;
                if (created >= Mathf.Max(1, tilesCreatedPerFrame))
                    break;
            }

            if (AllDesiredTilesLoaded())
            {
                yield return CommitVisibleSet();
                AtualizarVisibilidadeTerrenoGlobal(camera);
                RegistrarTempoInicialSePronto();
            }
            else
            {
                globalTerrain.gameObject.SetActive(true);
                globalTerrain.enabled = true;
            }

            yield return null;
        }
    }

    private void RegistrarTempoInicialSePronto()
    {
        if (!initialStartupTimingActive
            || !InitialWorldSurfaceReady
            || !InitialNavigationBuildResolved)
        {
            return;
        }

        initialStartupTimingActive = false;
        double tilesElapsedMs = initialStartupTileElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        double tilesWorkMs = initialStartupTileWorkTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Debug.Log("[GlobalTerrainStreamer][StartupTiming] initialReady=true tilesCreated=" + initialStartupTilesCreated
            + " tilesElapsedMs=" + tilesElapsedMs.ToString("F1")
            + " tilesWorkMs=" + tilesWorkMs.ToString("F1")
            + " lastNavMeshMs=" + lastLocalNavMeshBuildMilliseconds
            + " active=" + ActiveTileCount + "/" + DesiredTileCount
            + " navMesh=" + (InitialNavigationAvailable ? "available" : "fallback"));
    }

    private void BuildDesiredSet(Vector3 cameraPosition)
    {
        initialNavigationBuildResolved = false;
        initialNavigationBuildSucceeded = false;
        desiredTiles.Clear();
        float size = Mathf.Max(100f, world.terrainTileSize);
        int centerX = Mathf.FloorToInt((cameraPosition.x - world.MapMinX) / size);
        int centerZ = Mathf.FloorToInt((cameraPosition.z - world.MapMinZ) / size);
        int maxX = Mathf.CeilToInt(world.worldSize.x / size) - 1;
        int maxZ = Mathf.CeilToInt(world.mapFootprintHeight / size) - 1;

        for (int z = centerZ - tileRadius; z <= centerZ + tileRadius; z++)
        {
            if (z < 0 || z > maxZ) continue;
            for (int x = centerX - tileRadius; x <= centerX + tileRadius; x++)
            {
                if (x < 0 || x > maxX) continue;
                float minX = world.MapMinX + x * size;
                float minZ = world.MapMinZ + z * size;
                // The coarse map can show narrow coastal land which a sparse
                // tile test misses. Such a tile has no local collider/NavMesh,
                // so ground units cannot reach points that visibly look like
                // land. Sample at 125 m intervals for the 8 km world tiles.
                if (!world.ContainsLand(minX, minZ, minX + size, minZ + size, LandTileDetectionSamplesPerAxis))
                    continue;
                Vector2Int coordinate = new Vector2Int(x, z);
                desiredTiles.Add(coordinate);
                if (loadedTiles.ContainsKey(coordinate))
                    TouchTile(coordinate);
            }
        }
    }

    private bool AllDesiredTilesLoaded()
    {
        foreach (Vector2Int coordinate in desiredTiles)
        {
            if (!loadedTiles.ContainsKey(coordinate))
                return false;
        }
        return true;
    }

    private IEnumerator CommitVisibleSet()
    {
        string tileSet = BuildDesiredTileSetSignature();

        if (desiredTiles.Count == 0)
        {
            if (tileSet == committedNavMeshTileSet)
            {
                initialNavigationBuildResolved = true;
                yield break;
            }

            ClearLocalNavMesh();
            globalTerrain.enabled = true;
            RemoveStaleTiles();
            committedNavMeshTileSet = tileSet;
            failedNavMeshTileSet = null;
            initialNavigationBuildResolved = true;
            yield break;
        }

        if (tileSet == committedNavMeshTileSet && localNavMeshSurface != null
            && localNavMeshSurface.navMeshData != null)
        {
            initialNavigationBuildResolved = true;
            initialNavigationBuildSucceeded = true;
            yield break;
        }

        if (tileSet == failedNavMeshTileSet)
        {
            globalTerrain.enabled = true;
            initialNavigationBuildResolved = true;
            initialNavigationBuildSucceeded = false;
            yield break;
        }

        using (ApplyVisibleTilesMarker.Auto())
        {
            foreach (KeyValuePair<Vector2Int, Terrain> pair in loadedTiles)
            {
                bool visible = desiredTiles.Contains(pair.Key);
                Terrain terrain = pair.Value;
                if (terrain == null) continue;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null) collider.enabled = visible;
                terrain.enabled = visible;
                terrain.gameObject.SetActive(visible);
            }
        }

        // The streamed tiles now provide the local visual and colliders.
        // TerrainCollider is a separate component, so hiding this coarse
        // Terrain renderer avoids two overlapping surfaces while NavMesh builds.
        AtualizarVisibilidadeTerrenoGlobal(cameraOverride != null ? cameraOverride : Camera.main);

        bool navMeshReady = false;
        yield return BuildLocalNavMeshAsync(tileSet, ready => navMeshReady = ready);
        if (navMeshReady)
        {
            committedNavMeshTileSet = tileSet;
            failedNavMeshTileSet = null;
            initialNavigationBuildResolved = true;
            initialNavigationBuildSucceeded = true;
            AtualizarVisibilidadeTerrenoGlobal(cameraOverride != null ? cameraOverride : Camera.main);
        }
        else
        {
            ClearLocalNavMesh();
            HideDesiredTerrainRenderers();
            failedNavMeshTileSet = tileSet;
            globalTerrain.enabled = true;
            initialNavigationBuildResolved = true;
            initialNavigationBuildSucceeded = false;
        }

        RemoveStaleTiles();
    }

    private void EnsureLocalNavMeshSurface()
    {
        if (localNavMeshSurface == null)
        {
            localNavMeshSurface = GetComponent<PackageNavMeshSurface>();
            if (localNavMeshSurface == null)
                localNavMeshSurface = gameObject.AddComponent<PackageNavMeshSurface>();
        }

        int groundLayer = LayerMask.NameToLayer("Chao");
        localNavMeshSurface.agentTypeID = LocalNavMeshAgentTypeId;
        localNavMeshSurface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
        localNavMeshSurface.layerMask = 1 << (groundLayer >= 0 ? groundLayer : 0);
        localNavMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        localNavMeshSurface.defaultArea = 0;
        localNavMeshSurface.ignoreNavMeshAgent = true;
        localNavMeshSurface.ignoreNavMeshObstacle = true;
        localNavMeshSurface.minRegionArea = 2f;
        localNavMeshSurface.overrideTileSize = true;
        localNavMeshSurface.tileSize = LocalNavMeshTileSize;
        // The world tiles are 8 km across. A 4 m voxel keeps the streamed
        // 3x3 build responsive while preserving broad vehicle routes; 2 m
        // produced a 12 s build for the same loaded area in the runtime smoke test.
        localNavMeshSurface.overrideVoxelSize = true;
        localNavMeshSurface.voxelSize = LocalNavMeshVoxelSize;
    }

    private Vector3 ResolverAncoraDeStreaming(Camera camera)
    {
        Vector3 cameraPosition = camera.transform.position;
        if (world == null)
            return cameraPosition;

        if (gerenteSelecaoCache == null || Time.unscaledTime >= proximaBuscaGerenteSelecao)
        {
            gerenteSelecaoCache = FindFirstObjectByType<GerenteSelecao>();
            proximaBuscaGerenteSelecao = Time.unscaledTime + 1f;
        }

        if (gerenteSelecaoCache == null || gerenteSelecaoCache.unidadesSelecionadas == null)
            return cameraPosition;

        for (int i = 0; i < gerenteSelecaoCache.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecaoCache.unidadesSelecionadas[i];
            if (unidade == null || !unidade.isActiveAndEnabled || !unidade.PossuiOrdemMovimentoAtiva)
                continue;

            NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
            if (agent == null || agent.agentTypeID != LocalNavMeshAgentTypeId)
                continue;

            Vector3 unitPosition = unidade.transform.position;
            if (!world.IsLandAtWorld(unitPosition.x, unitPosition.z))
                continue;

            // Keep local ground and NavMesh tiles under a selected moving unit
            // even when the player leaves the camera in a different area.
            return unitPosition;
        }

        return cameraPosition;
    }

    private void AtualizarVisibilidadeTerrenoGlobal(Camera camera)
    {
        if (globalTerrain == null)
            return;

        if (camera == null || world == null)
        {
            globalTerrain.enabled = true;
            return;
        }

        float size = Mathf.Max(100f, world.terrainTileSize);
        int cameraX = Mathf.FloorToInt((camera.transform.position.x - world.MapMinX) / size);
        int cameraZ = Mathf.FloorToInt((camera.transform.position.z - world.MapMinZ) / size);
        Vector2Int cameraTile = new Vector2Int(cameraX, cameraZ);
        bool localTerrainVisible = desiredTiles.Contains(cameraTile)
            && loadedTiles.TryGetValue(cameraTile, out Terrain terrain)
            && terrain != null
            && terrain.enabled
            && terrain.gameObject.activeInHierarchy;
        globalTerrain.enabled = !localTerrainVisible;
    }

    private string BuildDesiredTileSetSignature()
    {
        using (BuildTileSetSignatureMarker.Auto())
        {
            tileSignatureBuffer.Clear();
            foreach (Vector2Int coordinate in desiredTiles)
                tileSignatureBuffer.Add(coordinate);
            tileSignatureBuffer.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            StringBuilder signature = new StringBuilder(tileSignatureBuffer.Count * 8);
            for (int i = 0; i < tileSignatureBuffer.Count; i++)
                signature.Append(tileSignatureBuffer[i].x).Append(',').Append(tileSignatureBuffer[i].y).Append(';');
            return signature.ToString();
        }
    }

    private IEnumerator BuildLocalNavMeshAsync(string tileSet, System.Action<bool> completed)
    {
        bool reusingExistingData = runtimeLocalNavMeshData != null;
        NavMeshData nextData;
        using (PrepareNavMeshUpdateMarker.Auto())
        {
            EnsureLocalNavMeshSurface();
            // NavMeshBuilder hashes the prior sources stored in this data and
            // can rebuild only changed regions. A fresh NavMeshData forces a
            // full build every time the streamed tile set changes.
            nextData = reusingExistingData
                ? runtimeLocalNavMeshData
                : new NavMeshData(LocalNavMeshAgentTypeId);
        }
        AsyncOperation buildOperation = null;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            using (ScheduleNavMeshUpdateMarker.Auto())
                buildOperation = localNavMeshSurface.UpdateNavMesh(nextData);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (buildOperation == null)
        {
            if (!reusingExistingData)
                Destroy(nextData);
            Debug.LogError("[GlobalMap] Não foi possível iniciar o NavMesh local para " + tileSet + ".", this);
            completed(false);
            yield break;
        }

        while (!buildOperation.isDone)
            yield return null;

        lastLocalNavMeshBuildMilliseconds = (int)(
            (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0
            / System.Diagnostics.Stopwatch.Frequency);

        using (SwapNavMeshDataMarker.Auto())
        {
            if (!reusingExistingData)
            {
                NavMeshData previousGeneratedData = runtimeLocalNavMeshData;
                if (localNavMeshSurface.enabled)
                    localNavMeshSurface.RemoveData();

                localNavMeshSurface.navMeshData = nextData;
                if (!localNavMeshSurface.enabled)
                    localNavMeshSurface.enabled = true;
                else
                    localNavMeshSurface.AddData();

                runtimeLocalNavMeshData = nextData;
                if (previousGeneratedData != null && previousGeneratedData != nextData)
                    Destroy(previousGeneratedData);
            }
        }

        // Let NavMeshSurface register the completed data before checking it.
        yield return null;
        bool hasNavigableLand;
        using (ValidateNavMeshMarker.Auto())
            hasNavigableLand = HasNavigableLand();
        if (!hasNavigableLand)
            Debug.LogError("[GlobalMap] O NavMesh local terminou vazio para " + tileSet + ".", this);
        else
            ReconnectStrandedGroundAgents();

        completed(hasNavigableLand);
    }

    private void ReconnectStrandedGroundAgents()
    {
        if (world == null)
            return;

        ControleUnidade[] unidades = FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            ControleUnidade unidade = unidades[i];
            if (unidade == null || !unidade.gameObject.activeInHierarchy || unidade.EhUnidadeNaval() || unidade.EhUnidadeAerea())
                continue;

            NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
            if (agent == null || agent.agentTypeID != LocalNavMeshAgentTypeId)
                continue;

            if (agent.enabled && agent.isOnNavMesh)
                continue;

            Vector3 pos = unidade.transform.position;
            if (!TrySampleLandHeight(pos, out float landHeight))
                continue;

            Vector3 sampleOrigin = new Vector3(pos.x, landHeight, pos.z);
            int mask = agent.areaMask != 0 ? agent.areaMask : NavMesh.AllAreas;
            if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, 24f, mask))
            {
                unidade.transform.position = hit.position;
                agent.enabled = true;
                if (!agent.isOnNavMesh)
                    agent.Warp(hit.position);
            }
        }
    }

    private bool HasNavigableLand()
    {
        if (world == null || desiredTiles.Count == 0)
            return false;

        // Most tile sets contain broad land, so retain the cheap center grid
        // first. If it misses a narrow shoreline, pick a raster-aligned land
        // point in each streamed tile rather than rejecting the whole set.
        return HasNavigableLandAtResolution(5)
            || HasNavigableLandAtRasterPoint();
    }

    private bool HasNavigableLandAtResolution(int samplesPerAxis)
    {
        float size = Mathf.Max(100f, world.terrainTileSize);
        foreach (Vector2Int coordinate in desiredTiles)
        {
            float minX = world.MapMinX + coordinate.x * size;
            float minZ = world.MapMinZ + coordinate.y * size;
            for (int z = 0; z < samplesPerAxis; z++)
            {
                for (int x = 0; x < samplesPerAxis; x++)
                {
                    float worldX = minX + (x + 0.5f) * size / samplesPerAxis;
                    float worldZ = minZ + (z + 0.5f) * size / samplesPerAxis;
                    if (!world.IsLandAtWorld(worldX, worldZ))
                        continue;

                    float height = world.HeightAtWorld(worldX, worldZ);
                    if (height <= world.seaLevel + 2f)
                        continue;

                    Vector3 candidate = new Vector3(worldX, height, worldZ);
                    if (NavMesh.SamplePosition(candidate, out _, 20f, NavMesh.AllAreas))
                        return true;
                }
            }
        }

        return false;
    }

    private bool HasNavigableLandAtRasterPoint()
    {
        Texture2D mask = world != null ? world.authorityMap : null;
        if (mask == null || mask.width < 2 || mask.height < 2
            || world.worldSize.x <= 0f || world.mapFootprintHeight <= 0f)
        {
            return false;
        }

        float size = Mathf.Max(100f, world.terrainTileSize);
        float pixelsPerWorldX = (mask.width - 1f) / world.worldSize.x;
        float pixelsPerWorldZ = (mask.height - 1f) / world.mapFootprintHeight;
        foreach (Vector2Int coordinate in desiredTiles)
        {
            float minX = world.MapMinX + coordinate.x * size;
            float minZ = world.MapMinZ + coordinate.y * size;
            int minPixelX = Mathf.Clamp(Mathf.CeilToInt((minX - world.MapMinX) * pixelsPerWorldX), 0, mask.width - 1);
            int maxPixelX = Mathf.Clamp(Mathf.FloorToInt((minX + size - world.MapMinX) * pixelsPerWorldX), 0, mask.width - 1);
            int minPixelZ = Mathf.Clamp(Mathf.CeilToInt((minZ - world.MapMinZ) * pixelsPerWorldZ), 0, mask.height - 1);
            int maxPixelZ = Mathf.Clamp(Mathf.FloorToInt((minZ + size - world.MapMinZ) * pixelsPerWorldZ), 0, mask.height - 1);

            Vector3 bestLandPoint = Vector3.zero;
            float bestCoastDistance = 0f;
            for (int pixelZ = minPixelZ; pixelZ <= maxPixelZ; pixelZ++)
            {
                float worldZ = world.MapMinZ + pixelZ / (float)(mask.height - 1) * world.mapFootprintHeight;
                for (int pixelX = minPixelX; pixelX <= maxPixelX; pixelX++)
                {
                    float worldX = world.MapMinX + pixelX / (float)(mask.width - 1) * world.worldSize.x;
                    float coastDistance = world.SignedCoastDistanceAtWorld(worldX, worldZ);
                    if (coastDistance <= bestCoastDistance)
                        continue;

                    bestCoastDistance = coastDistance;
                    bestLandPoint = new Vector3(worldX, 0f, worldZ);
                }
            }

            if (bestCoastDistance <= 0f)
                continue;

            bestLandPoint.y = world.HeightAtWorld(bestLandPoint.x, bestLandPoint.z);
            if (NavMesh.SamplePosition(bestLandPoint, out _, 20f, NavMesh.AllAreas))
                return true;
        }

        return false;
    }

    private void ClearLocalNavMesh()
    {
        if (localNavMeshSurface == null)
            localNavMeshSurface = GetComponent<PackageNavMeshSurface>();

        if (localNavMeshSurface != null)
        {
            localNavMeshSurface.RemoveData();
            localNavMeshSurface.navMeshData = null;
            localNavMeshSurface.enabled = false;
        }

        if (runtimeLocalNavMeshData != null)
            Destroy(runtimeLocalNavMeshData);
        runtimeLocalNavMeshData = null;
        committedNavMeshTileSet = string.Empty;
        failedNavMeshTileSet = null;
    }

    private float[,] RentHeightBuffer(int resolution)
    {
        if (heightBufferScratch == null
            || heightBufferScratch.GetLength(0) != resolution
            || heightBufferScratch.GetLength(1) != resolution)
        {
            heightBufferScratch = new float[resolution, resolution];
        }

        return heightBufferScratch;
    }

    private float[,] RentHeightRowBuffer(int rowCount, int resolution, int fullRowsPerFrame)
    {
        if (rowCount == fullRowsPerFrame)
        {
            if (fullHeightRowBuffer == null
                || fullHeightRowBuffer.GetLength(0) != rowCount
                || fullHeightRowBuffer.GetLength(1) != resolution)
            {
                fullHeightRowBuffer = new float[rowCount, resolution];
            }

            return fullHeightRowBuffer;
        }

        if (trailingHeightRowBuffer == null
            || trailingHeightRowBuffer.GetLength(0) != rowCount
            || trailingHeightRowBuffer.GetLength(1) != resolution)
        {
            trailingHeightRowBuffer = new float[rowCount, resolution];
        }

        return trailingHeightRowBuffer;
    }

    private float[,,] RentAlphaRowBuffer(int rowCount, int alphaResolution, int layerCount, int fullRowsPerFrame)
    {
        if (rowCount == fullRowsPerFrame)
        {
            if (fullAlphaRowBuffer == null
                || fullAlphaRowBuffer.GetLength(0) != rowCount
                || fullAlphaRowBuffer.GetLength(1) != alphaResolution
                || fullAlphaRowBuffer.GetLength(2) != layerCount)
            {
                fullAlphaRowBuffer = new float[rowCount, alphaResolution, layerCount];
            }

            return fullAlphaRowBuffer;
        }

        if (trailingAlphaRowBuffer == null
            || trailingAlphaRowBuffer.GetLength(0) != rowCount
            || trailingAlphaRowBuffer.GetLength(1) != alphaResolution
            || trailingAlphaRowBuffer.GetLength(2) != layerCount)
        {
            trailingAlphaRowBuffer = new float[rowCount, alphaResolution, layerCount];
        }

        return trailingAlphaRowBuffer;
    }

    private IEnumerator CreateLocalTileIncremental(
        Vector2Int coordinate,
        System.Action<Terrain> onComplete,
        System.Action<long> addWorkTicks)
    {
        float size = world.terrainTileSize;
        float minX = world.MapMinX + coordinate.x * size;
        float minZ = world.MapMinZ + coordinate.y * size;
        TerrainData data;
        using (InitializeTerrainDataMarker.Auto())
        {
            data = new TerrainData
            {
                name = "GlobalMap_TerrainData_" + coordinate.x + "_" + coordinate.y,
                heightmapResolution = world.localHeightResolution,
                alphamapResolution = world.localAlphamapResolution,
                // 8 km tiles at 1024 px keep the medium-range base map near 8 m/texel.
                baseMapResolution = 1024,
                size = new Vector3(size, world.terrainVerticalSize, size),
                terrainLayers = world.terrainLayers
            };
        }

        bool dataTransferredToTerrain = false;
        GameObject tileObject = null;
        Terrain terrainComponent = null;
        try
        {
            long terrainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using (CreateTerrainObjectMarker.Auto())
                tileObject = Terrain.CreateTerrainGameObject(data);
            using (ConfigureTerrainObjectMarker.Auto())
            {
                tileObject.name = "TerrainTile_" + coordinate.x.ToString("D2") + "_" + coordinate.y.ToString("D2");
                tileObject.transform.SetParent(transform, true);
                tileObject.transform.position = new Vector3(minX, world.terrainBaseY, minZ);
                tileObject.layer = LayerMask.NameToLayer("Chao") >= 0 ? LayerMask.NameToLayer("Chao") : 0;

                terrainComponent = tileObject.GetComponent<Terrain>();
                TerrainCollider colliderComponent = tileObject.GetComponent<TerrainCollider>();
                terrainComponent.drawInstanced = true;
                terrainComponent.materialTemplate = globalTerrain.materialTemplate;
                // Keep the initial radius conservative until LateUpdate applies
                // the camera-altitude target. Nearby trees stay visible while
                // their authored prefab LODGroup supplies the distant impostor.
                // Keep Unity's generated billboard at the cull edge to avoid
                // its blue fallback.
                terrainComponent.treeDistance = 250f;
                terrainComponent.treeBillboardDistance = 250f;
                terrainComponent.treeCrossFadeLength = 0f;
                terrainComponent.detailObjectDistance = 0f;
                // Keep local field/forest terrain layers active through the requested 5–20 km band.
                terrainComponent.basemapDistance = 20000f;
                terrainComponent.heightmapPixelError = 8f;
                terrainComponent.enabled = false;
                if (colliderComponent != null)
                {
                    colliderComponent.enabled = false;
                }
            }
            addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - terrainStarted);

            System.Diagnostics.Stopwatch budgetStopwatch = new System.Diagnostics.Stopwatch();
            budgetStopwatch.Start();

            int resolution = data.heightmapResolution;
            float[,] heights;
            using (AllocateHeightBufferMarker.Auto())
                heights = RentHeightBuffer(resolution);
            // Keep authored scene values intact while limiting the amount of
            // terrain sampling performed in one frame during streamed loads.
            int rowsPerFrame = Mathf.Clamp(terrainRowsPerFrame, 1, MaximumTerrainRowsPerFrame);
            for (int rowStart = 0; rowStart < resolution; rowStart += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, resolution - rowStart);
                float[,] heightRows;
                using (AllocateHeightRowBufferMarker.Auto())
                    heightRows = RentHeightRowBuffer(rowCount, resolution, rowsPerFrame);
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                using (GenerateHeightRowsMarker.Auto())
                {
                    for (int localZ = 0; localZ < rowCount; localZ++)
                    {
                        int z = rowStart + localZ;
                        float worldZ = minZ + size * z / (resolution - 1f);
                        for (int x = 0; x < resolution; x++)
                        {
                            float worldX = minX + size * x / (resolution - 1f);
                            float height = world.HeightAtWorld(worldX, worldZ);
                            float normalizedHeight = Mathf.Clamp01((height - world.terrainBaseY) / world.terrainVerticalSize);
                            heights[z, x] = normalizedHeight;
                            heightRows[localZ, x] = normalizedHeight;
                        }
                    }
                }
                using (ApplyHeightRowsMarker.Auto())
                    data.SetHeightsDelayLOD(0, rowStart, heightRows);
                addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - started);

                if (budgetStopwatch.Elapsed.TotalMilliseconds >= CurrentWorkBudgetMsPerFrame)
                {
                    budgetStopwatch.Restart();
                    yield return null;
                }
            }
            long syncStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using (SyncHeightmapMarker.Auto())
                data.SyncHeightmap();
            addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - syncStarted);

            int alphaResolution = data.alphamapResolution;
            int layerCount = world.terrainLayers.Length;
            int alphaRowsPerFrame = Mathf.Clamp(alphamapRowsPerFrame, 1, MaximumTerrainRowsPerFrame);
            for (int rowStart = 0; rowStart < alphaResolution; rowStart += alphaRowsPerFrame)
            {

                int rowCount = Mathf.Min(alphaRowsPerFrame, alphaResolution - rowStart);
                float[,,] alphaRows;
                using (AllocateAlphamapRowBufferMarker.Auto())
                    alphaRows = RentAlphaRowBuffer(rowCount, alphaResolution, layerCount, alphaRowsPerFrame);
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                using (GenerateAlphamapRowsMarker.Auto())
                {
                    for (int localZ = 0; localZ < rowCount; localZ++)
                    {
                        int z = rowStart + localZ;
                        float worldZ = minZ + size * z / (alphaResolution - 1f);
                        float normalizedZ = z / (alphaResolution - 1f);
                        int heightZ = Mathf.RoundToInt(normalizedZ * (resolution - 1));
                        for (int x = 0; x < alphaResolution; x++)
                        {
                            float worldX = minX + size * x / (alphaResolution - 1f);
                            float normalizedX = x / (alphaResolution - 1f);
                            int heightX = Mathf.RoundToInt(normalizedX * (resolution - 1));
                            float surfaceHeight = heights[heightZ, heightX] * world.terrainVerticalSize
                                + world.terrainBaseY - world.seaLevel;
                            float slope = data.GetSteepness(normalizedX, normalizedZ);
                            world.BiomeWeightsAtWorld(worldX, worldZ, alphaWeights, slope, surfaceHeight);
                            for (int layer = 0; layer < layerCount; layer++)
                                alphaRows[localZ, x, layer] = alphaWeights[layer];
                        }
                    }
                }
                using (ApplyAlphamapRowsMarker.Auto())
                    data.SetAlphamaps(0, rowStart, alphaRows);
                addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - started);

                if (budgetStopwatch.Elapsed.TotalMilliseconds >= CurrentWorkBudgetMsPerFrame)
                {
                    budgetStopwatch.Restart();
                    yield return null;
                }
            }
            yield return ConfigureVegetationIncremental(data, minX, minZ, size, addWorkTicks);

            MarcadorSuperficieMapa marker = tileObject.AddComponent<MarcadorSuperficieMapa>();
            marker.DefinirTipo(TipoSuperficieMapa.Chao);
            dataTransferredToTerrain = true;
            onComplete(terrainComponent);
        }
        finally
        {
            if (!dataTransferredToTerrain && data != null)
            {
                if (tileObject != null)
                    Destroy(tileObject);
                Destroy(data);
            }
        }
    }

    private IEnumerator ConfigureVegetationIncremental(
        TerrainData data,
        float minX,
        float minZ,
        float size,
        System.Action<long> addWorkTicks)
    {
        if (world.treePrefabs == null || world.treePrefabs.Length == 0 || world.forestTreesPerSquareKilometre <= 0f)
            yield break;

        List<TreePrototype> prototypes;
        int clusterPrototypeCount = 0;
        using (PrepareVegetationPrototypesMarker.Auto())
        {
            prototypes = new List<TreePrototype>();
            for (int i = 0; i < world.treePrefabs.Length; i++)
            {
                if (world.treePrefabs[i] == null) continue;
                prototypes.Add(new TreePrototype { prefab = world.treePrefabs[i], bendFactor = 0.04f });
                if (i < world.forestClusterPrototypeCount)
                    clusterPrototypeCount++;
            }
            if (prototypes.Count > 0)
                data.treePrototypes = prototypes.ToArray();
        }
        if (prototypes.Count == 0) yield break;

        // Forest patches use a world-anchored lattice. The mask decides which
        // cells grow vegetation, so field/desert tiles stay open and adjacent
        // terrain tiles share the same pattern at their boundaries.
        int seedX = Mathf.FloorToInt(minX / size);
        int seedZ = Mathf.FloorToInt(minZ / size);
        int tileSeed = unchecked(seedX * 73856093 ^ seedZ * 19349663);
        // Derive the grove lattice from the requested in-core tree density.
        // The Terrain tree count therefore scales with forest mask area rather
        // than a fixed, arbitrary per-tile instance cap.
        // Keep the requested trees-per-square-kilometre budget, but place it
        // in larger groves so the forest reads as connected masses from the
        // aircraft and strategic views instead of evenly spaced mini-clumps.
        const float averageTreesPerGrove = 36f;
        float cell = Mathf.Max(
            world.treeCandidateSpacing * 0.5f,
            Mathf.Sqrt(averageTreesPerGrove * 1000000f / world.forestTreesPerSquareKilometre));
        int firstIndividualPrototype = Mathf.Clamp(clusterPrototypeCount, 0, prototypes.Count);
        int minCellX = Mathf.FloorToInt(minX / cell) - 1;
        int maxCellX = Mathf.CeilToInt((minX + size) / cell) + 1;
        int minCellZ = Mathf.FloorToInt(minZ / cell) - 1;
        int maxCellZ = Mathf.CeilToInt((minZ + size) / cell) + 1;
        List<TreeClusterPlacement> placements = new List<TreeClusterPlacement>();
        int totalCandidateTrees = 0;
        float forestEquivalentAreaSquareKilometres = 0f;
        float strongestForestWeight = 0f;
        float fallbackWorldX = 0f;
        float fallbackWorldZ = 0f;

        for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
        {
            long rowStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using (GenerateVegetationRowMarker.Auto())
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int cellSeed = unchecked(cellX * 73856093 ^ cellZ * 19349663 ^ 0x2C9277B5);
                    System.Random random = new System.Random(cellSeed);
                    float worldX = (cellX + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.58f) * cell;
                    float worldZ = (cellZ + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.58f) * cell;
                    if (worldX < minX || worldX >= minX + size || worldZ < minZ || worldZ >= minZ + size)
                        continue;

                    float forest = world.ForestWeightAtWorld(worldX, worldZ);
                    if (forest < 0.045f)
                        continue;
                    forestEquivalentAreaSquareKilometres += cell * cell * Mathf.Clamp01(forest) / 1000000f;
                    if (forest > strongestForestWeight)
                    {
                        strongestForestWeight = forest;
                        fallbackWorldX = worldX;
                        fallbackWorldZ = worldZ;
                    }

                    // Dense forest cells become groves of individual tree meshes.
                    // The oversized backdrop patch meshes read as giant umbrellas
                    // at gameplay height, so keep them out of the local vegetation.
                    float densityChance = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.58f, forest));
                    if ((float)random.NextDouble() > densityChance)
                        continue;

                    bool usePatch = clusterPrototypeCount > 0
                        && (firstIndividualPrototype == prototypes.Count
                            || forest >= 0.28f
                            || (float)random.NextDouble() < Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.38f, forest)));
                    int prototypeIndex = usePatch
                        ? random.Next(clusterPrototypeCount)
                        : (firstIndividualPrototype < prototypes.Count
                            ? random.Next(firstIndividualPrototype, prototypes.Count)
                            : random.Next(prototypes.Count));

                    int members = forest >= 0.25f ? random.Next(30, 43) : 1;
                    List<TreeInstance> cluster = new List<TreeInstance>(members);
                    for (int member = 0; member < members; member++)
                    {
                        float memberX = worldX;
                        float memberZ = worldZ;
                        if (member > 0)
                        {
                            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                            float radius = Mathf.Lerp(18f, 58f, Mathf.Sqrt((float)random.NextDouble()));
                            memberX += Mathf.Cos(angle) * radius;
                            memberZ += Mathf.Sin(angle) * radius;
                        }

                        float memberForest = member == 0 ? forest : world.ForestWeightAtWorld(memberX, memberZ);
                        if (memberForest < 0.045f)
                            continue;

                        float normalizedX = Mathf.Clamp01((memberX - minX) / size);
                        float normalizedZ = Mathf.Clamp01((memberZ - minZ) / size);
                        float worldHeight = world.HeightAtWorld(memberX, memberZ);
                        float normalizedY = Mathf.Clamp01((worldHeight - world.terrainBaseY) / world.terrainVerticalSize);
                        bool patchPrototype = member == 0 && usePatch;
                        int memberPrototypeIndex = patchPrototype
                            ? prototypeIndex
                            : (members > 1 && firstIndividualPrototype < prototypes.Count
                                ? random.Next(firstIndividualPrototype, prototypes.Count)
                                : prototypeIndex);
                        cluster.Add(new TreeInstance
                        {
                            position = new Vector3(normalizedX, normalizedY, normalizedZ),
                            prototypeIndex = memberPrototypeIndex,
                            widthScale = patchPrototype
                                ? Mathf.Lerp(1.8f, 2.35f, (float)random.NextDouble())
                                : Mathf.Lerp(1.35f, 1.9f, (float)random.NextDouble()),
                            heightScale = patchPrototype
                                ? Mathf.Lerp(1.05f, 1.3f, (float)random.NextDouble())
                                : Mathf.Lerp(1f, 1.35f, (float)random.NextDouble()),
                            rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                            color = Color.white,
                            lightmapColor = Color.white
                        });
                    }

                    if (cluster.Count == 0)
                        continue;
                    totalCandidateTrees += cluster.Count;
                    placements.Add(new TreeClusterPlacement(cellSeed, cluster.ToArray()));
                }
            }

            addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - rowStarted);
            if ((cellZ - minCellZ + 1) % Mathf.Max(1, vegetationRowsPerFrame) == 0)
                yield return null;
        }

        // If an unusually dense tile exceeds its instance budget, sample whole
        // packs by a stable hash. This retains clumps and avoids scan-order bias.
        List<TreeInstance> trees;
        using (BuildVegetationInstancesMarker.Auto())
        {
            placements.Sort();
            int treeBudget = Mathf.CeilToInt(
                forestEquivalentAreaSquareKilometres * world.forestTreesPerSquareKilometre * 1.1f);
            trees = new List<TreeInstance>(Mathf.Min(totalCandidateTrees, treeBudget));
            for (int i = 0; i < placements.Count; i++)
            {
                TreeInstance[] cluster = placements[i].instances;
                if (trees.Count + cluster.Length > treeBudget)
                    continue;
                trees.AddRange(cluster);
            }

            // A forest cell can be missed by chance on a small island; keep its
            // strongest valid masked point represented without adding desert trees.
            if (trees.Count == 0 && strongestForestWeight >= 0.045f)
            {
                int prototypeIndex = clusterPrototypeCount > 0 ? 0 : 0;
                float height = world.HeightAtWorld(fallbackWorldX, fallbackWorldZ);
                trees.Add(new TreeInstance
                {
                    position = new Vector3(
                        Mathf.Clamp01((fallbackWorldX - minX) / size),
                        Mathf.Clamp01((height - world.terrainBaseY) / world.terrainVerticalSize),
                        Mathf.Clamp01((fallbackWorldZ - minZ) / size)),
                    prototypeIndex = prototypeIndex,
                    widthScale = 1f,
                    heightScale = 1f,
                    rotation = 0f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
        }

        long setTreesStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        using (ApplyVegetationInstancesMarker.Auto())
            data.SetTreeInstances(trees.ToArray(), true);
        addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - setTreesStarted);
    }

    private struct TreeClusterPlacement : System.IComparable<TreeClusterPlacement>
    {
        public readonly int hash;
        public readonly TreeInstance[] instances;

        public TreeClusterPlacement(int hash, TreeInstance[] instances)
        {
            this.hash = hash;
            this.instances = instances;
        }

        public int CompareTo(TreeClusterPlacement other)
        {
            return hash.CompareTo(other.hash);
        }
    }

    private void RemoveStaleTiles()
    {
        using (RemoveStaleTilesMarker.Auto())
        {
            removalBuffer.Clear();
            foreach (KeyValuePair<Vector2Int, Terrain> pair in loadedTiles)
                if (!desiredTiles.Contains(pair.Key)) removalBuffer.Add(pair.Key);

            for (int i = 0; i < removalBuffer.Count; i++)
            {
                if (loadedTiles.TryGetValue(removalBuffer[i], out Terrain terrain))
                    SetTerrainTileActive(terrain, false);
            }

            // Keep only a small LRU window so nearby revisits reuse TerrainData
            // and vegetation without allowing the runtime cache to grow forever.
            while (removalBuffer.Count > MaximumCachedTerrainTiles)
            {
                int oldestIndex = 0;
                int oldestUseOrder = int.MaxValue;
                for (int i = 0; i < removalBuffer.Count; i++)
                {
                    int useOrder = tileLastUsedOrder.TryGetValue(removalBuffer[i], out int lastUsed)
                        ? lastUsed
                        : 0;
                    if (useOrder >= oldestUseOrder) continue;
                    oldestUseOrder = useOrder;
                    oldestIndex = i;
                }

                Vector2Int coordinate = removalBuffer[oldestIndex];
                DestroyTerrainTile(coordinate);
                removalBuffer.RemoveAt(oldestIndex);
            }
        }
    }

    private void TouchTile(Vector2Int coordinate)
    {
        tileUseOrder++;
        tileLastUsedOrder[coordinate] = tileUseOrder;
    }

    private void SetTerrainTileActive(Terrain terrain, bool active)
    {
        if (terrain == null)
            return;

        if (!active)
        {
            TerrainCollider inactiveCollider = terrain.GetComponent<TerrainCollider>();
            if (inactiveCollider != null) inactiveCollider.enabled = false;
            terrain.enabled = false;
            terrain.gameObject.SetActive(false);
            return;
        }

        terrain.gameObject.SetActive(true);
        terrain.enabled = true;
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null) collider.enabled = true;
    }

    private void HideDesiredTerrainRenderers()
    {
        foreach (Vector2Int coordinate in desiredTiles)
        {
            if (!loadedTiles.TryGetValue(coordinate, out Terrain terrain) || terrain == null)
                continue;
            // Keep the local TerrainCollider available while restoring the
            // coarse renderer after a failed NavMesh update.
            terrain.enabled = false;
        }
    }

    private void SuspendLocalTerrainForMacroView()
    {
        desiredTiles.Clear();
        foreach (Terrain terrain in loadedTiles.Values)
            SetTerrainTileActive(terrain, false);
        ClearLocalNavMesh();
        RemoveStaleTiles();
    }

    private void DestroyTerrainTile(Vector2Int coordinate)
    {
        if (loadedTiles.TryGetValue(coordinate, out Terrain terrain) && terrain != null)
        {
            TerrainData data = terrain.terrainData;
            Destroy(terrain.gameObject);
            if (data != null) Destroy(data);
        }
        loadedTiles.Remove(coordinate);
        tileLastUsedOrder.Remove(coordinate);
    }

    private void RemoveAllTiles()
    {
        ClearLocalNavMesh();

        HashSet<GameObject> tileObjects = new HashSet<GameObject>();
        HashSet<TerrainData> tileData = new HashSet<TerrainData>();
        foreach (Terrain terrain in loadedTiles.Values)
        {
            if (terrain == null) continue;
            tileObjects.Add(terrain.gameObject);
            if (terrain.terrainData != null)
                tileData.Add(terrain.terrainData);
        }

        // Recover runtime tiles left behind by an interrupted coroutine or a
        // domain reload that cleared loadedTiles before OnDisable ran.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("TerrainTile_", System.StringComparison.Ordinal))
                continue;

            Terrain terrain = child.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
                tileData.Add(terrain.terrainData);
            tileObjects.Add(child.gameObject);
        }

        foreach (GameObject tileObject in tileObjects)
            if (tileObject != null) Destroy(tileObject);
        foreach (TerrainData data in tileData)
            if (data != null) Destroy(data);

        loadedTiles.Clear();
        tileLastUsedOrder.Clear();
        desiredTiles.Clear();
        tileUseOrder = 0;
    }

    private static void ConfigureTerrainRendering(Terrain terrain, bool vegetation)
    {
        if (terrain == null) return;
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 8f;
        terrain.treeDistance = vegetation ? 250f : 0f;
        terrain.treeBillboardDistance = vegetation ? 250f : 3500f;
        terrain.treeCrossFadeLength = 0f;
        terrain.detailObjectDistance = 0f;
        terrain.basemapDistance = 20000f;
    }

    private void AtualizarDistanciaVegetacaoLocal(Camera camera)
    {
        if (camera == null || loadedTiles.Count == 0)
            return;

        // Tree instances use their own prefab LODGroup (mesh near, authored
        // impostor far). Cull that inexpensive impostor sooner as altitude
        // rises; the terrain biome textures carry the broad forest masses.
        // Keep Unity's generated Terrain billboard at the cull edge because
        // its fallback tint was visibly blue in the previous Play capture.
        float altitudeBlend = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(500f, 3000f, camera.transform.position.y));
        float treeDistance = Mathf.Lerp(250f, 125f, altitudeBlend);
        float billboardDistance = treeDistance;
        foreach (Terrain terrain in loadedTiles.Values)
        {
            if (terrain == null)
                continue;
            if (Mathf.Abs(terrain.treeDistance - treeDistance) > 50f)
                terrain.treeDistance = treeDistance;
            if (Mathf.Abs(terrain.treeBillboardDistance - billboardDistance) > 50f)
                terrain.treeBillboardDistance = billboardDistance;
            if (terrain.treeCrossFadeLength > 0.01f)
                terrain.treeCrossFadeLength = 0f;
        }
    }
}
