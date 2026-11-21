using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GlobalTerrainStreamerCacheAndAltitudePlayModeTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject streamerObject;
    private GameObject cameraObject;
    private readonly List<TerrainData> createdTerrainData = new List<TerrainData>();
    private readonly List<GameObject> createdTerrainObjects = new List<GameObject>();

    [UnityTest]
    public IEnumerator InitialSurfaceIsReadyWhenLocalTilesReplaceTheCoarseTerrain()
    {
        Component streamer = CreateReadinessFixture(true, out Terrain coarseTerrain);
        coarseTerrain.enabled = false;

        PropertyInfo ready = streamer.GetType().GetProperty("InitialWorldSurfaceReady");
        Assert.That(ready, Is.Not.Null);
        Assert.That((bool)ready.GetValue(streamer), Is.True,
            "Visible local terrain tiles are the active surface after the coarse renderer is hidden.");

        yield return null;
    }

    [UnityTest]
    public IEnumerator InitialNavigationReadinessWaitsForBuildOrFallbackDecision()
    {
        Component streamer = CreateReadinessFixture(true, out _);
        Type streamerType = streamer.GetType();
        PropertyInfo resolved = streamerType.GetProperty("InitialNavigationBuildResolved");
        PropertyInfo available = streamerType.GetProperty("InitialNavigationAvailable");
        FieldInfo resolvedField = streamerType.GetField("initialNavigationBuildResolved", PrivateInstance);
        FieldInfo succeededField = streamerType.GetField("initialNavigationBuildSucceeded", PrivateInstance);

        Assert.That(resolved, Is.Not.Null);
        Assert.That(available, Is.Not.Null);
        Assert.That(resolvedField, Is.Not.Null);
        Assert.That(succeededField, Is.Not.Null);
        Assert.That((bool)resolved.GetValue(streamer), Is.False,
            "Visible tiles do not mean their startup NavMesh is ready yet.");
        Assert.That((bool)available.GetValue(streamer), Is.False);

        resolvedField.SetValue(streamer, true);
        succeededField.SetValue(streamer, true);
        Assert.That((bool)resolved.GetValue(streamer), Is.True);
        Assert.That((bool)available.GetValue(streamer), Is.True,
            "The loading flow can release after a successful initial NavMesh build.");

        succeededField.SetValue(streamer, false);
        Assert.That((bool)resolved.GetValue(streamer), Is.True,
            "The existing coarse terrain fallback is a resolved outcome, not an indefinite wait.");
        Assert.That((bool)available.GetValue(streamer), Is.False,
            "The fallback must remain visible in diagnostics as a navigation limitation.");

        yield return null;
    }

    [UnityTest]
    public IEnumerator InitialSurfaceIsReadyOnCoarseTerrainAfterLocalNavMeshFailure()
    {
        Component streamer = CreateReadinessFixture(false, out Terrain coarseTerrain);
        MethodInfo buildSignature = streamer.GetType().GetMethod("BuildDesiredTileSetSignature", PrivateInstance);
        FieldInfo failedTileSet = streamer.GetType().GetField("failedNavMeshTileSet", PrivateInstance);
        PropertyInfo ready = streamer.GetType().GetProperty("InitialWorldSurfaceReady");
        Assert.That(buildSignature, Is.Not.Null);
        Assert.That(failedTileSet, Is.Not.Null);
        Assert.That(ready, Is.Not.Null);

        failedTileSet.SetValue(streamer, buildSignature.Invoke(streamer, null));
        coarseTerrain.enabled = true;

        Assert.That((bool)ready.GetValue(streamer), Is.True,
            "The visible coarse surface is a valid startup fallback when local NavMesh creation fails.");

        yield return null;
    }

    [UnityTest]
    public IEnumerator CacheIsBoundedToNineTilesAndAltitudeUsesHysteresis()
    {
        streamerObject = new GameObject("GlobalTerrainStreamerCacheTest");
        streamerObject.SetActive(false);
        Type streamerType = FindProjectType("GlobalTerrainStreamer");
        Assert.That(streamerType, Is.Not.Null, "The project terrain streamer type must be loaded.");
        Component streamer = streamerObject.AddComponent(streamerType);

        cameraObject = new GameObject("GlobalTerrainStreamerCacheTestCamera");
        Camera testCamera = cameraObject.AddComponent<Camera>();

        PropertyInfo macroMode = streamerType.GetProperty("IsMacroTerrainMode");
        MethodInfo updateAltitude = streamerType.GetMethod("UpdateAltitudeMode", PrivateInstance);
        Assert.That(macroMode, Is.Not.Null);
        Assert.That(updateAltitude, Is.Not.Null);

        SetAltitude(testCamera, 19000f);
        updateAltitude.Invoke(streamer, new object[] { testCamera });
        Assert.That((bool)macroMode.GetValue(streamer), Is.False);

        SetAltitude(testCamera, 20500f);
        updateAltitude.Invoke(streamer, new object[] { testCamera });
        Assert.That((bool)macroMode.GetValue(streamer), Is.False,
            "Climbing 500 m above the threshold must not switch terrain modes.");

        SetAltitude(testCamera, 21001f);
        updateAltitude.Invoke(streamer, new object[] { testCamera });
        Assert.That((bool)macroMode.GetValue(streamer), Is.True,
            "The macro terrain mode should begin after crossing the upper hysteresis boundary.");

        SetAltitude(testCamera, 19500f);
        updateAltitude.Invoke(streamer, new object[] { testCamera });
        Assert.That((bool)macroMode.GetValue(streamer), Is.True,
            "Descending 500 m below the threshold must not switch terrain modes.");

        SetAltitude(testCamera, 18999f);
        updateAltitude.Invoke(streamer, new object[] { testCamera });
        Assert.That((bool)macroMode.GetValue(streamer), Is.False,
            "The local terrain mode should resume after crossing the lower hysteresis boundary.");

        FieldInfo loadedTilesField = streamerType.GetField("loadedTiles", PrivateInstance);
        var loadedTiles = loadedTilesField.GetValue(streamer) as IDictionary;
        PropertyInfo cachedTileCount = streamerType.GetProperty("CachedTileCount");
        MethodInfo touchTile = streamerType.GetMethod("TouchTile", PrivateInstance);
        MethodInfo removeStaleTiles = streamerType.GetMethod("RemoveStaleTiles", PrivateInstance);
        Assert.That(loadedTiles, Is.Not.Null);
        Assert.That(cachedTileCount, Is.Not.Null);
        Assert.That(touchTile, Is.Not.Null);
        Assert.That(removeStaleTiles, Is.Not.Null);

        for (int i = 0; i < 12; i++)
        {
            Vector2Int coordinate = new Vector2Int(i, 0);
            TerrainData data = new TerrainData { size = new Vector3(32f, 8f, 32f) };
            createdTerrainData.Add(data);
            GameObject tileObject = Terrain.CreateTerrainGameObject(data);
            tileObject.name = "TerrainTile_CacheTest_" + i;
            tileObject.transform.SetParent(streamerObject.transform, false);
            loadedTiles.Add(coordinate, tileObject.GetComponent<Terrain>());
            touchTile.Invoke(streamer, new object[] { coordinate });
        }

        removeStaleTiles.Invoke(streamer, null);

        Assert.That(loadedTiles.Count, Is.EqualTo(9),
            "The runtime cache must retain no more than one default 3x3 tile set.");
        Assert.That((int)cachedTileCount.GetValue(streamer), Is.EqualTo(9));
        for (int i = 0; i < 3; i++)
            Assert.That(loadedTiles.Contains(new Vector2Int(i, 0)), Is.False,
                "Least recently used tiles should be evicted first.");
        for (int i = 3; i < 12; i++)
            Assert.That(loadedTiles.Contains(new Vector2Int(i, 0)), Is.True);

        Assert.That((int)cachedTileCount.GetValue(streamer), Is.EqualTo(9));

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator CleanupRuntimeTerrain()
    {
        if (streamerObject != null)
            UnityEngine.Object.Destroy(streamerObject);
        if (cameraObject != null)
            UnityEngine.Object.Destroy(cameraObject);
        for (int i = 0; i < createdTerrainObjects.Count; i++)
            if (createdTerrainObjects[i] != null)
                UnityEngine.Object.Destroy(createdTerrainObjects[i]);

        yield return null;

        for (int i = 0; i < createdTerrainData.Count; i++)
            if (createdTerrainData[i] != null)
                UnityEngine.Object.DestroyImmediate(createdTerrainData[i]);
        createdTerrainData.Clear();
        createdTerrainObjects.Clear();
        streamerObject = null;
        cameraObject = null;
    }

    private Component CreateReadinessFixture(bool localTileReady, out Terrain coarseTerrain)
    {
        streamerObject = new GameObject("GlobalTerrainStreamerReadinessTest");
        streamerObject.SetActive(false);
        Type streamerType = FindProjectType("GlobalTerrainStreamer");
        Assert.That(streamerType, Is.Not.Null, "The project terrain streamer type must be loaded.");
        Component streamer = streamerObject.AddComponent(streamerType);

        TerrainData coarseData = new TerrainData { size = new Vector3(32f, 8f, 32f) };
        createdTerrainData.Add(coarseData);
        GameObject coarseObject = Terrain.CreateTerrainGameObject(coarseData);
        coarseObject.name = "GlobalTerrainStreamerReadinessCoarse";
        createdTerrainObjects.Add(coarseObject);
        coarseTerrain = coarseObject.GetComponent<Terrain>();

        const int tileX = 0;
        const int tileY = 0;
        const string initialViewFieldName = "initialStreamingViewResolved";
        streamerType.GetField("globalTerrain", AllInstance).SetValue(streamer, coarseTerrain);
        streamerType.GetField(initialViewFieldName, PrivateInstance).SetValue(streamer, true);

        var desiredTiles = streamerType.GetField("desiredTiles", PrivateInstance).GetValue(streamer) as HashSet<Vector2Int>;
        var loadedTiles = streamerType.GetField("loadedTiles", PrivateInstance).GetValue(streamer) as IDictionary;
        Assert.That(desiredTiles, Is.Not.Null);
        Assert.That(loadedTiles, Is.Not.Null);
        Vector2Int coordinate = new Vector2Int(tileX, tileY);
        desiredTiles.Add(coordinate);

        if (localTileReady)
        {
            TerrainData localData = new TerrainData { size = new Vector3(32f, 8f, 32f) };
            createdTerrainData.Add(localData);
            GameObject localObject = Terrain.CreateTerrainGameObject(localData);
            localObject.name = "GlobalTerrainStreamerReadinessLocal";
            createdTerrainObjects.Add(localObject);
            loadedTiles.Add(coordinate, localObject.GetComponent<Terrain>());
        }

        return streamer;
    }

    private static void SetAltitude(Camera camera, float altitude)
    {
        Vector3 position = camera.transform.position;
        position.y = altitude;
        camera.transform.position = position;
    }

    private static Type FindProjectType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
                return type;
        }
        return null;
    }
}
