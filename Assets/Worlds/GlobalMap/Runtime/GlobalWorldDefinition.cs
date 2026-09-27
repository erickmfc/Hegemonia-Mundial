using System;
using UnityEngine;

/// <summary>
/// Geografia da cena de mundo global. A máscara de fase.png é a única fonte
/// de silhueta; a vista superior fornece apenas alturas e pesos de bioma.
/// Coordenadas de propriedade política não são armazenadas neste asset.
/// </summary>
[CreateAssetMenu(menuName = "Hegemonia/World/Global World Definition")]
public sealed class GlobalWorldDefinition : ScriptableObject
{
    [Header("Referências oficiais")]
    public Texture2D authorityMap;
    public Texture2D appearanceMap;

    [Header("Escala lógica (metros)")]
    public Vector2 worldSize = new Vector2(512000f, 512000f);
    [Min(1f)] public float mapFootprintHeight = 288000f;
    [Min(1f)] public float terrainTileSize = 8000f;
    [Min(33)] public int globalHeightResolution = 2049;
    [Min(33)] public int localHeightResolution = 513;
    [Min(16)] public int alphamapResolution = 512;
    [Min(16)] public int localAlphamapResolution = 512;

    [Header("Mar e relevo")]
    public float seaLevel = 0f;
    [Min(0f)] public float minimumLandClearance = 4f;
    public float terrainBaseY = -200f;
    [Min(500f)] public float terrainVerticalSize = 2200f;
    [Min(0f)] public float beachWidth = 1600f;
    [Min(1f)] public float coastalElevationBlendWidth = 26000f;
    [Min(0f)] public float maximumMountainHeight = 1250f;

    [Header("Camadas: vegetação, solo, areia e rocha")]
    public TerrainLayer[] terrainLayers = new TerrainLayer[6];

    [Header("Vegetação instanciada nos tiles próximos")]
    public GameObject[] treePrefabs = Array.Empty<GameObject>();
    [Min(0)] public int forestClusterPrototypeCount = 4;
    [Min(20f)] public float treeCandidateSpacing = 48f;
    [Min(100f)] public float forestTreesPerSquareKilometre = 1200f;

    [NonSerialized] private bool initialized;
    [NonSerialized] private int mapWidth;
    [NonSerialized] private int mapHeight;
    [NonSerialized] private bool[] landMask;
    [NonSerialized] private float[] signedCoastDistance;
    [NonSerialized] private Color32[] appearancePixels;
    [NonSerialized] private int appearanceWidth;
    [NonSerialized] private int appearanceHeight;

    public float MapMinX => -worldSize.x * 0.5f;
    public float MapMinZ => -mapFootprintHeight * 0.5f;
    public float MapMaxX => worldSize.x * 0.5f;
    public float MapMaxZ => mapFootprintHeight * 0.5f;

    public void InitializeRuntimeData()
    {
        if (initialized)
            return;

        if (authorityMap == null || appearanceMap == null || !authorityMap.isReadable || !appearanceMap.isReadable)
            throw new InvalidOperationException("As referências de geografia e aparência devem estar atribuídas e legíveis.");

        mapWidth = authorityMap.width;
        mapHeight = authorityMap.height;
        appearanceWidth = appearanceMap.width;
        appearanceHeight = appearanceMap.height;

        Color32[] source = authorityMap.GetPixels32();
        landMask = new bool[source.Length];
        for (int i = 0; i < source.Length; i++)
            landMask[i] = IsLandColor(source[i]);

        // A linha escura desenhada no litoral pertence ao contorno da costa.
        // Incorporá-la fecha pequenas fendas sem expandir a máscara além dela.
        bool[] closedMask = (bool[])landMask.Clone();
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int index = y * mapWidth + x;
                if (landMask[index] || MaxChannel(source[index]) > 55)
                    continue;

                for (int oy = -1; oy <= 1 && !closedMask[index]; oy++)
                {
                    int sy = y + oy;
                    if (sy < 0 || sy >= mapHeight) continue;
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = x + ox;
                        if (sx >= 0 && sx < mapWidth && landMask[sy * mapWidth + sx])
                        {
                            closedMask[index] = true;
                            break;
                        }
                    }
                }
            }
        }
        landMask = closedMask;

        float[] distanceToWater = BuildDistanceField(landMask, false);
        float[] distanceToLand = BuildDistanceField(landMask, true);
        signedCoastDistance = new float[landMask.Length];
        float metresPerPixel = Mathf.Sqrt((worldSize.x / mapWidth) * (mapFootprintHeight / mapHeight));
        for (int i = 0; i < signedCoastDistance.Length; i++)
        {
            float pixelDistance = landMask[i] ? distanceToWater[i] : distanceToLand[i];
            signedCoastDistance[i] = landMask[i]
                ? Mathf.Max(0f, pixelDistance - 0.5f) * metresPerPixel
                : -Mathf.Max(0f, pixelDistance - 0.5f) * metresPerPixel;
        }

        appearancePixels = appearanceMap.GetPixels32();
        initialized = true;
    }

    public void ResetRuntimeData()
    {
        initialized = false;
        mapWidth = 0;
        mapHeight = 0;
        landMask = null;
        signedCoastDistance = null;
        appearancePixels = null;
        appearanceWidth = 0;
        appearanceHeight = 0;
    }

    public bool IsLandAtWorld(float worldX, float worldZ)
    {
        EnsureInitialized();
        return SignedCoastDistanceAtWorld(worldX, worldZ) > 0f;
    }

    public bool ContainsLand(float minX, float minZ, float maxX, float maxZ, int samplesPerAxis = 12)
    {
        EnsureInitialized();
        samplesPerAxis = Mathf.Max(2, samplesPerAxis);
        for (int z = 0; z < samplesPerAxis; z++)
        {
            float worldZ = Mathf.Lerp(minZ, maxZ, (z + 0.5f) / samplesPerAxis);
            for (int x = 0; x < samplesPerAxis; x++)
            {
                float worldX = Mathf.Lerp(minX, maxX, (x + 0.5f) / samplesPerAxis);
                if (IsLandAtWorld(worldX, worldZ))
                    return true;
            }
        }
        return false;
    }

    public float SignedCoastDistanceAtWorld(float worldX, float worldZ)
    {
        EnsureInitialized();
        float u = (worldX - MapMinX) / worldSize.x;
        float v = (worldZ - MapMinZ) / mapFootprintHeight;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return -Mathf.Min(Mathf.Abs(u - 0.5f) * worldSize.x, Mathf.Abs(v - 0.5f) * mapFootprintHeight);
        return SampleScalar(signedCoastDistance, mapWidth, mapHeight, u, v);
    }

    public float HeightAtWorld(float worldX, float worldZ)
    {
        EnsureInitialized();
        float distance = SignedCoastDistanceAtWorld(worldX, worldZ);
        if (distance <= 0f)
        {
            // A seabed rises into a submerged shelf before reaching the
            // shoreline. The water surface stays at seaLevel; this avoids a
            // 179 m terrain discontinuity directly under the visible coast.
            float offshore = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-distance / 6500f));
            return seaLevel - Mathf.Lerp(12f, 175f, offshore);
        }

        float u = Mathf.InverseLerp(MapMinX, MapMaxX, worldX);
        float v = Mathf.InverseLerp(MapMinZ, MapMaxZ, worldZ);
        Color appearance = SampleAppearance(u, v);
        GetAppearanceWeights(appearance, out float green, out float arid, out float rock);

        // Broaden and vary the coastal rise by region. The silhouette still
        // comes exclusively from fase.png; this only shapes the height field.
        float coastWidthNoise = Mathf.PerlinNoise(worldX / 78000f + 13.7f, worldZ / 78000f - 5.2f);
        float coastWidth = Mathf.Max(1f, coastalElevationBlendWidth) * Mathf.Lerp(0.72f, 1.28f, coastWidthNoise);
        float coastBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / coastWidth));

        // Domain-warped low frequency fields form regional uplands and
        // depressions. Centering noise around zero prevents the old uniform
        // positive uplift that made whole continents read as tables.
        float warpX = (Mathf.PerlinNoise(worldX / 96000f + 8.1f, worldZ / 96000f + 31.4f) - 0.5f) * 42000f;
        float warpZ = (Mathf.PerlinNoise(worldX / 88000f - 21.2f, worldZ / 88000f + 4.6f) - 0.5f) * 36000f;
        float broad = Mathf.PerlinNoise((worldX + warpX) / 76000f + 19.3f, (worldZ + warpZ) / 68000f - 7.1f);
        float mid = Mathf.PerlinNoise((worldX + warpX * 0.35f) / 18500f - 3.7f, (worldZ + warpZ * 0.35f) / 15500f + 22.6f);
        float valleyField = Mathf.PerlinNoise((worldX + warpZ * 0.2f) / 36000f + 3.2f, (worldZ - warpX * 0.2f) / 31000f - 17.5f);
        float valley = SmoothRange(valleyField, 0.57f, 0.82f) * 95f;

        // Ridges are conditioned by the appearance reference's highland signal
        // and broken into chains by broad regional noise, rather than spread
        // uniformly over every land pixel.
        float ridgeNoise = Mathf.PerlinNoise((worldX + warpX) / 22500f + 2.4f, (worldZ + warpZ) / 19500f - 11.9f);
        float ridged = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
        float ridgeShape = SmoothRange(ridged, 0.48f, 0.88f);
        float rangeBreakup = Mathf.PerlinNoise((worldX + warpZ) / 61000f - 14.2f, (worldZ - warpX) / 57000f + 6.8f);
        float rockPotential = SmoothRange(rock + (broad - 0.5f) * 0.18f, 0.08f, 0.56f);
        float highlandStrength = rockPotential * Mathf.Lerp(0.45f, 1f, rangeBreakup);
        float mountainMask = highlandStrength * ridgeShape;
        float mountainFade = SmoothRange(coastBlend, 0.52f, 0.88f);

        float regionalRelief = (broad - 0.5f) * 260f;
        float rollingRelief = (mid - 0.5f) * 185f;
        float fineRelief = (Mathf.PerlinNoise(worldX / 7900f + 31.1f, worldZ / 7900f - 15.8f) - 0.5f) * 32f;
        float inland = Mathf.Max(55f, 125f + regionalRelief + rollingRelief + fineRelief - valley);
        float foothills = highlandStrength * (1f - ridgeShape) * 180f * mountainFade;
        float mountainRelief = Mathf.Pow(mountainMask, 1.35f) * maximumMountainHeight * mountainFade;
        float elevation = minimumLandClearance + coastBlend * (inland + foothills) + mountainRelief;
        return seaLevel + Mathf.Max(minimumLandClearance, elevation);
    }

    /// <summary>Weights correspond to green grass, dry grass, soil, sand, rock, and forest cover.</summary>
    public void BiomeWeightsAtWorld(
        float worldX,
        float worldZ,
        float[] weights,
        float terrainSlopeDegrees = -1f,
        float surfaceHeightMeters = float.NaN)
    {
        EnsureInitialized();
        if (weights == null || weights.Length < 6)
            throw new ArgumentException("São necessários seis pesos de camada.", nameof(weights));

        float distance = SignedCoastDistanceAtWorld(worldX, worldZ);
        if (distance <= 0f)
        {
            // The base terrain is submerged here. Sand keeps its transition
            // coherent if a terrain tile extends beneath the existing ocean.
            for (int i = 0; i < 6; i++) weights[i] = 0f;
            weights[3] = 1f;
            return;
        }

        Color appearance = SampleAppearance(
            Mathf.InverseLerp(MapMinX, MapMaxX, worldX),
            Mathf.InverseLerp(MapMinZ, MapMaxZ, worldZ));
        GetAppearanceWeights(appearance, out float green, out float arid, out float rock);
        float moisture = 0.35f + 0.65f * Mathf.PerlinNoise(worldX / 39000f + 12.8f, worldZ / 39000f - 3.3f);
        float broadDryness = Mathf.PerlinNoise(worldX / 28500f - 8.4f, worldZ / 28500f + 14.9f);
        float fieldNoise = 0.58f * Mathf.PerlinNoise(worldX / 7200f + 16.2f, worldZ / 7200f - 2.7f)
            + 0.42f * Mathf.PerlinNoise(worldX / 3400f - 27.5f, worldZ / 3400f + 10.1f);
        float fieldPatch = SmoothRange(fieldNoise, 0.34f, 0.74f);
        float coast = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / Mathf.Max(1f, beachWidth)));

        float surfaceHeight = float.IsNaN(surfaceHeightMeters)
            ? HeightAtWorld(worldX, worldZ) - seaLevel
            : surfaceHeightMeters;
        if (terrainSlopeDegrees < 0f)
            terrainSlopeDegrees = SlopeAtWorld(worldX, worldZ, distance);
        float slopeRock = SlopeRockWeight(terrainSlopeDegrees);
        float highlandRock = SmoothRange(surfaceHeight, 360f, 1050f) * 0.55f;
        float rockWeight = Mathf.Clamp01(rock * 0.68f + slopeRock * 0.95f + highlandRock);
        float fertile = Mathf.Clamp01(green * (1f - rockWeight * 0.84f));
        float dryRegion = Mathf.Clamp01(arid * 0.88f + (1f - moisture) * green * 0.28f + broadDryness * arid * 0.18f);

        // The appearance image gives broad biome identity; independent,
        // kilometre-scale fields add soft crop/soil patches without making
        // political boundaries or straight biome borders.
        weights[0] = fertile * (0.52f + moisture * 0.32f + fieldPatch * 0.12f);
        weights[1] = (fertile * (0.16f + (1f - moisture) * 0.34f)
            + dryRegion * (0.18f + fieldPatch * 0.18f)) * (1f - rockWeight * 0.72f);
        weights[2] = (fertile * (0.06f + fieldPatch * 0.34f)
            + dryRegion * (0.08f + fieldPatch * 0.18f)) * (1f - rockWeight * 0.78f);
        float coastVariation = Mathf.PerlinNoise(worldX / 18000f + 3.6f, worldZ / 18000f - 12.1f);
        float localBeach = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(distance / (Mathf.Max(1f, beachWidth) * Mathf.Lerp(0.65f, 1.5f, coastVariation))));
        float rockyShore = localBeach * rock * 0.45f;
        weights[3] = dryRegion * (0.36f + (1f - fieldPatch) * 0.34f) + localBeach * Mathf.Lerp(0.95f, 0.35f, rock);
        weights[4] = Mathf.Max(0.015f, rockWeight);
        weights[4] += rockyShore;

        float forestCover = ForestCoverageAtWorld(worldX, worldZ, green, arid, rock, distance);
        // The broad mask makes forests readable from the 5–20 km band; the
        // instanced tree layer adds individual silhouettes only within 5 km.
        weights[5] = Mathf.Min(0.9f, forestCover * 0.9f);
        float remainingGround = 1f - weights[5];
        for (int i = 0; i < 5; i++) weights[i] *= remainingGround;
        NormalizeSurfaceWeights(weights, 6);
    }

    /// <summary>
    /// Produces the very broad albedo used by the coarse, global-view Terrain.
    /// The reference image contributes biome identity only; warped procedural
    /// fields create the visible regional patches, without drawing map borders.
    /// Local streamed tiles continue to use the six detailed TerrainLayers.
    /// </summary>
    public Color MacroBiomeColorAtWorld(float worldX, float worldZ)
    {
        EnsureInitialized();

        Color appearance = SampleAppearance(
            Mathf.InverseLerp(MapMinX, MapMaxX, worldX),
            Mathf.InverseLerp(MapMinZ, MapMaxZ, worldZ));
        GetAppearanceWeights(appearance, out float green, out float arid, out float sourceRock);

        float warpX = (Mathf.PerlinNoise(worldX / 92000f + 18.4f, worldZ / 92000f - 6.2f) - 0.5f) * 21000f;
        float warpZ = (Mathf.PerlinNoise(worldX / 81000f - 11.7f, worldZ / 81000f + 16.3f) - 0.5f) * 17500f;
        float regional = 0.62f * Mathf.PerlinNoise((worldX + warpX) / 51000f + 4.1f, (worldZ + warpZ) / 47000f - 8.3f)
            + 0.38f * Mathf.PerlinNoise((worldX - warpZ) / 27000f - 19.2f, (worldZ + warpX) / 25000f + 3.7f);
        float fieldNoise = 0.58f * Mathf.PerlinNoise((worldX + warpX * 0.3f) / 17500f + 14.1f, (worldZ + warpZ * 0.3f) / 15500f - 21.6f)
            + 0.42f * Mathf.PerlinNoise(worldX / 9200f - 3.8f, worldZ / 9200f + 12.9f);

        float elevation = Mathf.Max(0f, HeightAtWorld(worldX, worldZ) - seaLevel);
        float foothills = SmoothRange(elevation, 260f, 700f);
        float mountainChain = SmoothRange(elevation, 560f, maximumMountainHeight);
        float rock = Mathf.Clamp01(sourceRock * 0.42f + foothills * 0.18f + mountainChain * 0.72f);
        float dry = Mathf.Clamp01(arid + (1f - green - arid) * Mathf.Lerp(0.16f, 0.32f, regional));
        float fertile = Mathf.Clamp01(green * (1f - rock * 0.48f));
        float neutral = Mathf.Max(0f, 1f - dry - fertile - rock * 0.52f);
        float total = dry + fertile + rock * 0.52f + neutral;
        if (total > 0.0001f)
        {
            dry /= total;
            fertile /= total;
            rock = rock * 0.52f / total;
            neutral /= total;
        }

        Color color = new Color(0.285f, 0.365f, 0.205f) * fertile
            + new Color(0.485f, 0.425f, 0.285f) * dry
            + new Color(0.425f, 0.405f, 0.355f) * rock
            + new Color(0.405f, 0.345f, 0.245f) * neutral;

        float fieldPatch = SmoothRange(fieldNoise, 0.36f, 0.73f);
        color = Color.Lerp(color, new Color(0.355f, 0.39f, 0.235f), fieldPatch * fertile * 0.28f);

        float forestSignal = 0.66f * Mathf.PerlinNoise((worldX + warpX) / 29500f + 23.8f, (worldZ + warpZ) / 28500f - 2.1f)
            + 0.34f * Mathf.PerlinNoise((worldX - warpZ) / 14200f - 15.4f, (worldZ + warpX) / 13800f + 7.6f);
        float forestMass = SmoothRange(forestSignal, 0.49f, 0.68f)
            * SmoothRange(green, 0.2f, 0.62f)
            * (1f - SmoothRange(arid, 0.08f, 0.62f) * 0.98f)
            * (1f - sourceRock * 0.66f);
        color = Color.Lerp(color, new Color(0.135f, 0.235f, 0.125f), forestMass * 0.82f);

        float ridgeShade = Mathf.PerlinNoise((worldX + warpX) / 11800f + 8.5f, (worldZ + warpZ) / 8600f - 18.2f);
        color *= Mathf.Lerp(0.93f, 1.055f, ridgeShade);
        return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
    }

    public float ForestWeightAtWorld(float worldX, float worldZ)
    {
        EnsureInitialized();
        float coastDistance = SignedCoastDistanceAtWorld(worldX, worldZ);
        if (coastDistance < 700f)
            return 0f;

        Color appearance = SampleAppearance(
            Mathf.InverseLerp(MapMinX, MapMaxX, worldX),
            Mathf.InverseLerp(MapMinZ, MapMaxZ, worldZ));
        GetAppearanceWeights(appearance, out float green, out float arid, out float rock);
        return ForestCoverageAtWorld(worldX, worldZ, green, arid, rock, coastDistance);
    }

    private float ForestCoverageAtWorld(float worldX, float worldZ, float green, float arid, float rock, float coastDistance)
    {
        if (coastDistance < 700f)
            return 0f;

        float moisture = 0.35f + 0.65f * Mathf.PerlinNoise(worldX / 47000f + 12.8f, worldZ / 47000f - 3.3f);
        float warpX = (Mathf.PerlinNoise(worldX / 51000f + 7.2f, worldZ / 51000f - 18.5f) - 0.5f) * 9500f;
        float warpZ = (Mathf.PerlinNoise(worldX / 56000f - 11.6f, worldZ / 56000f + 22.1f) - 0.5f) * 8500f;
        float canopySignal = 0.64f * Mathf.PerlinNoise((worldX + warpX) / 10200f + 9.1f, (worldZ + warpZ) / 10200f - 17.4f)
            + 0.36f * Mathf.PerlinNoise((worldX - warpZ) / 5100f - 6.8f, (worldZ + warpX) / 5100f + 21.3f);
        float forestCore = SmoothRange(canopySignal, 0.44f, 0.62f);
        float aridExclusion = 1f - SmoothRange(arid, 0.06f, 0.62f) * 0.99f;
        float biomeSuitability = SmoothRange(green, 0.20f, 0.62f)
            * aridExclusion
            * (1f - rock * 0.72f);
        float clusteredCoverage = Mathf.Lerp(0.025f, 1f, forestCore) * (0.78f + moisture * 0.22f);
        return Mathf.Clamp01(biomeSuitability * clusteredCoverage);
    }

    private float SlopeAtWorld(float worldX, float worldZ, float coastDistance)
    {
        if (coastDistance < 550f)
            return 0f;

        const float sampleOffset = 180f;
        float slopeX = (HeightAtWorld(worldX + sampleOffset, worldZ) - HeightAtWorld(worldX - sampleOffset, worldZ)) / (sampleOffset * 2f);
        float slopeZ = (HeightAtWorld(worldX, worldZ + sampleOffset) - HeightAtWorld(worldX, worldZ - sampleOffset)) / (sampleOffset * 2f);
        return Mathf.Atan(Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ)) * Mathf.Rad2Deg;
    }

    private static float SlopeRockWeight(float slopeAngle)
    {
        return SmoothRange(slopeAngle, 24f, 47f);
    }

    private static float SmoothRange(float value, float minimum, float maximum)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
    }

    private static void NormalizeSurfaceWeights(float[] weights, int count)
    {
        float total = 0f;
        for (int i = 0; i < count; i++) total += weights[i];
        if (total <= 0.0001f)
        {
            for (int i = 0; i < count; i++) weights[i] = i == 0 ? 1f : 0f;
            return;
        }

        for (int i = 0; i < count; i++) weights[i] /= total;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
            InitializeRuntimeData();
    }

    private Color SampleAppearance(float u, float v)
    {
        u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
        return SampleColor(appearancePixels, appearanceWidth, appearanceHeight, u, v);
    }

    private static float SampleScalar(float[] pixels, int width, int height, float u, float v)
    {
        float x = Mathf.Clamp01(u) * (width - 1);
        float y = Mathf.Clamp01(v) * (height - 1);
        int x0 = Mathf.FloorToInt(x); int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(width - 1, x0 + 1); int y1 = Mathf.Min(height - 1, y0 + 1);
        float a = Mathf.Lerp(pixels[y0 * width + x0], pixels[y0 * width + x1], x - x0);
        float b = Mathf.Lerp(pixels[y1 * width + x0], pixels[y1 * width + x1], x - x0);
        return Mathf.Lerp(a, b, y - y0);
    }

    private static Color SampleColor(Color32[] pixels, int width, int height, float u, float v)
    {
        float x = Mathf.Clamp01(u) * (width - 1);
        float y = Mathf.Clamp01(v) * (height - 1);
        int x0 = Mathf.FloorToInt(x); int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(width - 1, x0 + 1); int y1 = Mathf.Min(height - 1, y0 + 1);
        Color a = Color.Lerp(pixels[y0 * width + x0], pixels[y0 * width + x1], x - x0);
        Color b = Color.Lerp(pixels[y1 * width + x0], pixels[y1 * width + x1], x - x0);
        return Color.Lerp(a, b, y - y0);
    }

    private static void GetAppearanceWeights(Color c, out float green, out float arid, out float rock)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        float saturation = max <= 0.001f ? 0f : (max - min) / max;
        float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        green = Mathf.Clamp01((c.g - c.r + 0.035f) * 4.5f) * Mathf.Clamp01((c.g - c.b + 0.015f) * 2.6f);
        arid = Mathf.Clamp01((c.r - c.g + 0.055f) * 4f) * SmoothRange(luminance, 0.28f, 0.72f);
        rock = Mathf.Clamp01((0.42f - saturation) * 2.6f) * SmoothRange(luminance, 0.2f, 0.52f);
        float remaining = Mathf.Clamp01(1f - Mathf.Max(green, arid));
        green += remaining * 0.48f;
        float sum = green + arid + rock;
        if (sum > 1f)
        {
            green /= sum; arid /= sum; rock /= sum;
        }
    }

    private static bool IsLandColor(Color32 c)
    {
        int max = MaxChannel(c); int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        bool magentaBoundary = c.r > 150 && c.b > 95 && c.g < 185 && c.r > c.g + 25;
        bool blueOcean = c.r < 60 && c.g > 75 && c.b > 145 && c.b > c.g;
        bool neutralIsland = min > 90 && max - min <= 22;
        return max > 55 && (max - min > 22 || neutralIsland) && !magentaBoundary && !blueOcean;
    }

    private static int MaxChannel(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

    private float[] BuildDistanceField(bool[] mask, bool targetLand)
    {
        const float diagonal = 1.41421356f;
        const float far = 1000000f;
        float[] distance = new float[mask.Length];
        for (int i = 0; i < mask.Length; i++)
            distance[i] = mask[i] == targetLand ? 0f : far;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int i = y * mapWidth + x;
                if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i - 1] + 1f);
                if (y > 0) distance[i] = Mathf.Min(distance[i], distance[i - mapWidth] + 1f);
                if (x > 0 && y > 0) distance[i] = Mathf.Min(distance[i], distance[i - mapWidth - 1] + diagonal);
                if (x + 1 < mapWidth && y > 0) distance[i] = Mathf.Min(distance[i], distance[i - mapWidth + 1] + diagonal);
            }
        }
        for (int y = mapHeight - 1; y >= 0; y--)
        {
            for (int x = mapWidth - 1; x >= 0; x--)
            {
                int i = y * mapWidth + x;
                if (x + 1 < mapWidth) distance[i] = Mathf.Min(distance[i], distance[i + 1] + 1f);
                if (y + 1 < mapHeight) distance[i] = Mathf.Min(distance[i], distance[i + mapWidth] + 1f);
                if (x + 1 < mapWidth && y + 1 < mapHeight) distance[i] = Mathf.Min(distance[i], distance[i + mapWidth + 1] + diagonal);
                if (x > 0 && y + 1 < mapHeight) distance[i] = Mathf.Min(distance[i], distance[i + mapWidth - 1] + diagonal);
            }
        }
        return distance;
    }
}
