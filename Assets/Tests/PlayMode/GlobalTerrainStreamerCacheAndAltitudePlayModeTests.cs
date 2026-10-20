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

    private GameObject streamerObject;
    private GameObject cameraObject;
    private readonly List<TerrainData> createdTerrainData = new List<TerrainData>();

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

        yield return null;

        for (int i = 0; i < createdTerrainData.Count; i++)
            if (createdTerrainData[i] != null)
                UnityEngine.Object.DestroyImmediate(createdTerrainData[i]);
        createdTerrainData.Clear();
        streamerObject = null;
        cameraObject = null;
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
