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
    [Range(1, 16)] public int vegetationRowsPerFrame = 1;
    public bool showDiagnostics = true;

    private readonly Dictionary<Vector2Int, Terrain> loadedTiles = new Dictionary<Vector2Int, Terrain>(16);
    private readonly HashSet<Vector2Int> desiredTiles = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> removalBuffer = new List<Vector2Int>(16);
    private readonly float[] alphaWeights = new float[6];
    private Coroutine streamingLoop;
    private Camera farClipCamera;
    private float farClipBeforeLocalStreaming;
    private bool localFarClipLimited;
    private int totalTreeInstances;
    private int lastGenerationMilliseconds;
    private int lastLocalNavMeshBuildMilliseconds;
    private PackageNavMeshSurface localNavMeshSurface;
    private NavMeshData runtimeLocalNavMeshData;
    private string committedNavMeshTileSet = string.Empty;
    private string failedNavMeshTileSet;

    public int ActiveTileCount => loadedTiles.Count;
    public int DesiredTileCount => desiredTiles.Count;
    public int ActiveTreeCount => totalTreeInstances;
    public int LastGenerationMilliseconds => lastGenerationMilliseconds;
    public int LastLocalNavMeshBuildMilliseconds => lastLocalNavMeshBuildMilliseconds;

    private void OnEnable()
    {
        if (Application.isPlaying)
            EnsureLocalNavMeshSurface();

        streamingLoop = StartCoroutine(StreamContinuously());
    }

    private void OnDisable()
    {
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

        if (camera.transform.position.y >= detailTerrainBelowAltitude)
        {
            // CameraController recalculates its global far plane as the camera
            // crosses this threshold. Leave that value untouched in macro view.
            localFarClipLimited = false;
            farClipCamera = camera;
            return;
        }

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
                yield return new WaitForSecondsRealtime(refreshInterval);
                continue;
            }

            if (camera.transform.position.y >= detailTerrainBelowAltitude)
            {
                globalTerrain.gameObject.SetActive(true);
                globalTerrain.enabled = true;
                RemoveAllTiles();
                yield return new WaitForSecondsRealtime(refreshInterval);
                continue;
            }

            globalTerrain.gameObject.SetActive(true);

            if (Time.unscaledTime >= nextRefresh)
            {
                using (BuildDesiredSetMarker.Auto())
                    BuildDesiredSet(camera.transform.position);
                nextRefresh = Time.unscaledTime + refreshInterval;
            }

            int created = 0;
            foreach (Vector2Int coordinate in desiredTiles)
            {
                if (loadedTiles.ContainsKey(coordinate))
                    continue;

                Terrain terrain = null;
                long generationWorkTicks = 0;
                yield return CreateLocalTileIncremental(
                    coordinate,
                    createdTerrain => terrain = createdTerrain,
                    workTicks => generationWorkTicks += workTicks);
                lastGenerationMilliseconds = (int)(generationWorkTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                if (terrain != null)
                    loadedTiles.Add(coordinate, terrain);

                created++;
                if (created >= Mathf.Max(1, tilesCreatedPerFrame))
                    break;
            }

            if (AllDesiredTilesLoaded())
                yield return CommitVisibleSet();
            else
            {
                globalTerrain.gameObject.SetActive(true);
                globalTerrain.enabled = true;
            }

            yield return null;
        }
    }

    private void BuildDesiredSet(Vector3 cameraPosition)
    {
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
                if (!world.ContainsLand(minX, minZ, minX + size, minZ + size, 10))
                    continue;
                desiredTiles.Add(new Vector2Int(x, z));
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
                yield break;

            ClearLocalNavMesh();
            globalTerrain.enabled = true;
            RemoveStaleTiles();
            committedNavMeshTileSet = tileSet;
            failedNavMeshTileSet = null;
            yield break;
        }

        if (tileSet == committedNavMeshTileSet && localNavMeshSurface != null
            && localNavMeshSurface.navMeshData != null)
        {
            yield break;
        }

        if (tileSet == failedNavMeshTileSet)
        {
            globalTerrain.enabled = true;
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

        // Keep the coarse Terrain as the visual/collider fallback while the
        // streamed local navmesh is built on a worker thread.
        globalTerrain.enabled = true;

        bool navMeshReady = false;
        yield return BuildLocalNavMeshAsync(tileSet, ready => navMeshReady = ready);
        if (navMeshReady)
        {
            committedNavMeshTileSet = tileSet;
            failedNavMeshTileSet = null;
            globalTerrain.enabled = false;
        }
        else
        {
            ClearLocalNavMesh();
            failedNavMeshTileSet = tileSet;
            globalTerrain.enabled = true;
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

    private string BuildDesiredTileSetSignature()
    {
        using (BuildTileSetSignatureMarker.Auto())
        {
            List<Vector2Int> sorted = new List<Vector2Int>(desiredTiles);
            sorted.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            StringBuilder signature = new StringBuilder(sorted.Count * 8);
            for (int i = 0; i < sorted.Count; i++)
                signature.Append(sorted[i].x).Append(',').Append(sorted[i].y).Append(';');
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

        completed(hasNavigableLand);
    }

    private bool HasNavigableLand()
    {
        if (world == null || desiredTiles.Count == 0)
            return false;

        float size = Mathf.Max(100f, world.terrainTileSize);
        const int samplesPerAxis = 5;
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
        try
        {
            int resolution = data.heightmapResolution;
            float[,] heights;
            using (AllocateHeightBufferMarker.Auto())
                heights = new float[resolution, resolution];
            int rowsPerFrame = Mathf.Max(1, terrainRowsPerFrame);
            for (int rowStart = 0; rowStart < resolution; rowStart += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, resolution - rowStart);
                float[,] heightRows;
                using (AllocateHeightRowBufferMarker.Auto())
                    heightRows = new float[rowCount, resolution];
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
                yield return null;
            }
            long syncStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using (SyncHeightmapMarker.Auto())
                data.SyncHeightmap();
            addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - syncStarted);

            int alphaResolution = data.alphamapResolution;
            int layerCount = world.terrainLayers.Length;
            for (int rowStart = 0; rowStart < alphaResolution; rowStart += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, alphaResolution - rowStart);
                float[,,] alphaRows;
                using (AllocateAlphamapRowBufferMarker.Auto())
                    alphaRows = new float[rowCount, alphaResolution, layerCount];
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
                yield return null;
            }
            yield return ConfigureVegetationIncremental(data, minX, minZ, size, addWorkTicks);

            long terrainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            GameObject tileObject;
            using (CreateTerrainObjectMarker.Auto())
                tileObject = Terrain.CreateTerrainGameObject(data);
            Terrain terrainComponent;
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
                terrainComponent.enabled = false;
                if (colliderComponent != null) colliderComponent.enabled = false;

                MarcadorSuperficieMapa marker = tileObject.AddComponent<MarcadorSuperficieMapa>();
                marker.DefinirTipo(TipoSuperficieMapa.Chao);
            }
            dataTransferredToTerrain = true;
            addWorkTicks(System.Diagnostics.Stopwatch.GetTimestamp() - terrainStarted);
            onComplete(terrainComponent);
        }
        finally
        {
            if (!dataTransferredToTerrain && data != null)
                Destroy(data);
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
        totalTreeInstances += trees.Count;
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
                Terrain terrain = loadedTiles[removalBuffer[i]];
                if (terrain != null)
                {
                    totalTreeInstances -= terrain.terrainData != null ? terrain.terrainData.treeInstanceCount : 0;
                    TerrainData data = terrain.terrainData;
                    Destroy(terrain.gameObject);
                    if (data != null) Destroy(data);
                }
                loadedTiles.Remove(removalBuffer[i]);
            }
            totalTreeInstances = Mathf.Max(0, totalTreeInstances);
        }
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
        desiredTiles.Clear();
        totalTreeInstances = 0;
    }

    private static void ConfigureTerrainRendering(Terrain terrain, bool vegetation)
    {
        if (terrain == null) return;
        terrain.drawInstanced = true;
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
