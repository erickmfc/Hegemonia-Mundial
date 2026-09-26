using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a coarse, collidable world Terrain available everywhere and streams
/// higher-resolution Terrain tiles around the active camera near the ground.
/// The camera is also the anchor while it follows a selected unit.
/// </summary>
public sealed class GlobalTerrainStreamer : MonoBehaviour
{
    public GlobalWorldDefinition world;
    public Terrain globalTerrain;
    public Camera cameraOverride;
    [Min(1000f)] public float detailTerrainBelowAltitude = 20000f;
    [Range(1, 2)] public int tileRadius = 1;
    [Min(0.1f)] public float refreshInterval = 0.5f;
    [Range(1, 4)] public int tilesCreatedPerFrame = 1;
    public bool showDiagnostics = true;

    private readonly Dictionary<Vector2Int, Terrain> loadedTiles = new Dictionary<Vector2Int, Terrain>(16);
    private readonly HashSet<Vector2Int> desiredTiles = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> removalBuffer = new List<Vector2Int>(16);
    private readonly float[] alphaWeights = new float[6];
    private Coroutine streamingLoop;
    private int totalTreeInstances;
    private int lastGenerationMilliseconds;

    public int ActiveTileCount => loadedTiles.Count;
    public int DesiredTileCount => desiredTiles.Count;
    public int ActiveTreeCount => totalTreeInstances;
    public int LastGenerationMilliseconds => lastGenerationMilliseconds;

    private void OnEnable()
    {
        streamingLoop = StartCoroutine(StreamContinuously());
    }

    private void OnDisable()
    {
        if (streamingLoop != null)
        {
            StopCoroutine(streamingLoop);
            streamingLoop = null;
        }
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
                BuildDesiredSet(camera.transform.position);
                nextRefresh = Time.unscaledTime + refreshInterval;
            }

            int created = 0;
            foreach (Vector2Int coordinate in desiredTiles)
            {
                if (loadedTiles.ContainsKey(coordinate))
                    continue;

                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                Terrain terrain = CreateLocalTile(coordinate);
                lastGenerationMilliseconds = (int)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                if (terrain != null)
                    loadedTiles.Add(coordinate, terrain);

                created++;
                if (created >= Mathf.Max(1, tilesCreatedPerFrame))
                    break;
            }

            if (AllDesiredTilesLoaded())
                CommitVisibleSet();
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

    private void CommitVisibleSet()
    {
        foreach (KeyValuePair<Vector2Int, Terrain> pair in loadedTiles)
        {
            bool visible = desiredTiles.Contains(pair.Key);
            Terrain terrain = pair.Value;
            if (terrain == null) continue;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.enabled = visible;
            terrain.enabled = visible;
        }

        globalTerrain.enabled = desiredTiles.Count == 0;
        RemoveStaleTiles();
    }

    private Terrain CreateLocalTile(Vector2Int coordinate)
    {
        float size = world.terrainTileSize;
        float minX = world.MapMinX + coordinate.x * size;
        float minZ = world.MapMinZ + coordinate.y * size;
        TerrainData data = new TerrainData
        {
            name = "GlobalMap_TerrainData_" + coordinate.x + "_" + coordinate.y,
            heightmapResolution = world.localHeightResolution,
            alphamapResolution = world.localAlphamapResolution,
            size = new Vector3(size, world.terrainVerticalSize, size),
            terrainLayers = world.terrainLayers
        };

        int resolution = data.heightmapResolution;
        float[,] heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            float worldZ = minZ + size * z / (resolution - 1f);
            for (int x = 0; x < resolution; x++)
            {
                float worldX = minX + size * x / (resolution - 1f);
                float height = world.HeightAtWorld(worldX, worldZ);
                heights[z, x] = Mathf.Clamp01((height - world.terrainBaseY) / world.terrainVerticalSize);
            }
        }
        data.SetHeights(0, 0, heights);

        int alphaResolution = data.alphamapResolution;
        float[,,] alphamaps = new float[alphaResolution, alphaResolution, world.terrainLayers.Length];
        for (int z = 0; z < alphaResolution; z++)
        {
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
                for (int layer = 0; layer < world.terrainLayers.Length; layer++)
                    alphamaps[z, x, layer] = alphaWeights[layer];
            }
        }
        data.SetAlphamaps(0, 0, alphamaps);
        ConfigureVegetation(data, minX, minZ, size);

        GameObject tileObject = Terrain.CreateTerrainGameObject(data);
        tileObject.name = "TerrainTile_" + coordinate.x.ToString("D2") + "_" + coordinate.y.ToString("D2");
        tileObject.transform.SetParent(transform, true);
        tileObject.transform.position = new Vector3(minX, world.terrainBaseY, minZ);
        tileObject.layer = LayerMask.NameToLayer("Chao") >= 0 ? LayerMask.NameToLayer("Chao") : 0;

        Terrain terrainComponent = tileObject.GetComponent<Terrain>();
        TerrainCollider colliderComponent = tileObject.GetComponent<TerrainCollider>();
        terrainComponent.drawInstanced = true;
        terrainComponent.materialTemplate = globalTerrain.materialTemplate;
        terrainComponent.treeDistance = 5000f;
        terrainComponent.treeBillboardDistance = 900f;
        terrainComponent.treeCrossFadeLength = 160f;
        terrainComponent.detailObjectDistance = 0f;
        terrainComponent.basemapDistance = 8000f;
        terrainComponent.enabled = false;
        if (colliderComponent != null) colliderComponent.enabled = false;

        MarcadorSuperficieMapa marker = tileObject.AddComponent<MarcadorSuperficieMapa>();
        marker.DefinirTipo(TipoSuperficieMapa.Chao);
        return terrainComponent;
    }

    private void ConfigureVegetation(TerrainData data, float minX, float minZ, float size)
    {
        if (world.treePrefabs == null || world.treePrefabs.Length == 0 || world.maximumTreesPerTile == 0)
            return;

        List<TreePrototype> prototypes = new List<TreePrototype>();
        int clusterPrototypeCount = 0;
        for (int i = 0; i < world.treePrefabs.Length; i++)
        {
            if (world.treePrefabs[i] == null) continue;
            prototypes.Add(new TreePrototype { prefab = world.treePrefabs[i], bendFactor = 0.04f });
            if (i < world.forestClusterPrototypeCount)
                clusterPrototypeCount++;
        }
        if (prototypes.Count == 0) return;
        data.treePrototypes = prototypes.ToArray();

        // Forest patches use a world-anchored lattice. The mask decides which
        // cells grow vegetation, so field/desert tiles stay open and adjacent
        // terrain tiles share the same pattern at their boundaries.
        int seedX = Mathf.FloorToInt(minX / size);
        int seedZ = Mathf.FloorToInt(minZ / size);
        int tileSeed = unchecked(seedX * 73856093 ^ seedZ * 19349663);
        float cell = Mathf.Max(128f, world.treeCandidateSpacing * 4f);
        int firstIndividualPrototype = Mathf.Clamp(clusterPrototypeCount, 0, prototypes.Count);
        int minCellX = Mathf.FloorToInt(minX / cell) - 1;
        int maxCellX = Mathf.CeilToInt((minX + size) / cell) + 1;
        int minCellZ = Mathf.FloorToInt(minZ / cell) - 1;
        int maxCellZ = Mathf.CeilToInt((minZ + size) / cell) + 1;
        List<TreeClusterPlacement> placements = new List<TreeClusterPlacement>();
        int totalCandidateTrees = 0;
        float strongestForestWeight = 0f;
        float fallbackWorldX = 0f;
        float fallbackWorldZ = 0f;

        for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
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
                if (forest > strongestForestWeight)
                {
                    strongestForestWeight = forest;
                    fallbackWorldX = worldX;
                    fallbackWorldZ = worldZ;
                }

                // High cover gets a forest-patch prototype. Low cover can only
                // add an occasional single tree, leaving fields mostly open.
                float densityChance = Mathf.SmoothStep(0.06f, 0.58f, forest);
                if ((float)random.NextDouble() > densityChance)
                    continue;

                bool usePatch = clusterPrototypeCount > 0
                    && (firstIndividualPrototype == prototypes.Count
                        || forest >= 0.28f
                        || (float)random.NextDouble() < Mathf.SmoothStep(0.12f, 0.38f, forest));
                int prototypeIndex = usePatch
                    ? random.Next(clusterPrototypeCount)
                    : (firstIndividualPrototype < prototypes.Count
                        ? random.Next(firstIndividualPrototype, prototypes.Count)
                        : random.Next(prototypes.Count));

                List<TreeInstance> cluster = new List<TreeInstance>(4);
                int members = usePatch && forest > 0.62f ? random.Next(2, 5) : 1;
                for (int member = 0; member < members; member++)
                {
                    float memberX = worldX;
                    float memberZ = worldZ;
                    if (member > 0)
                    {
                        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                        float radius = Mathf.Lerp(18f, 52f, Mathf.Sqrt((float)random.NextDouble()));
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
                    bool patchPrototype = prototypeIndex < clusterPrototypeCount;
                    cluster.Add(new TreeInstance
                    {
                        position = new Vector3(normalizedX, normalizedY, normalizedZ),
                        prototypeIndex = patchPrototype ? prototypeIndex : random.Next(firstIndividualPrototype, prototypes.Count),
                        widthScale = patchPrototype
                            ? Mathf.Lerp(0.9f, 1.2f, (float)random.NextDouble())
                            : Mathf.Lerp(0.95f, 1.4f, (float)random.NextDouble()),
                        heightScale = patchPrototype
                            ? Mathf.Lerp(0.9f, 1.2f, (float)random.NextDouble())
                            : Mathf.Lerp(1f, 1.5f, (float)random.NextDouble()),
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

        // If an unusually dense tile exceeds its instance budget, sample whole
        // packs by a stable hash. This retains clumps and avoids scan-order bias.
        placements.Sort();
        List<TreeInstance> trees = new List<TreeInstance>(Mathf.Min(totalCandidateTrees, world.maximumTreesPerTile));
        for (int i = 0; i < placements.Count; i++)
        {
            TreeInstance[] cluster = placements[i].instances;
            if (trees.Count + cluster.Length > world.maximumTreesPerTile)
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

        data.SetTreeInstances(trees.ToArray(), true);
        totalTreeInstances += trees.Count;
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

    private void RemoveAllTiles()
    {
        foreach (Terrain terrain in loadedTiles.Values)
        {
            if (terrain == null) continue;
            TerrainData data = terrain.terrainData;
            Destroy(terrain.gameObject);
            if (data != null) Destroy(data);
        }
        loadedTiles.Clear();
        desiredTiles.Clear();
        totalTreeInstances = 0;
    }

    private static void ConfigureTerrainRendering(Terrain terrain, bool vegetation)
    {
        if (terrain == null) return;
        terrain.drawInstanced = true;
        terrain.treeDistance = vegetation ? 5000f : 0f;
        terrain.treeBillboardDistance = 900f;
        terrain.treeCrossFadeLength = 160f;
        terrain.detailObjectDistance = 0f;
        terrain.basemapDistance = 50000f;
    }
}
