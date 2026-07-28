#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.Cartel;

public static class CartelSecondCountrySetup
{
    [MenuItem("Hegemonia/Cartel/Preparar Pais02 e Ilha Pirata")]
    public static void Prepare()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        CartelCreateGenerator.CreatePackageSilently("Pais02", false, true);
        GameObject root = FindSceneObject("CartelManualCreates_Pais02");
        if (root == null) return;

        Terrain terrain = FindValidTerrain();
        if (terrain == null) return;
        Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
        Vector3 center = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        Vector3 basePoint = Ground(center + new Vector3(size.x * 0.30f, 0f, size.z * 0.22f), terrain);
        Vector3 coast = center + new Vector3(size.x * 0.30f, 0f, -size.z * 0.54f);
        Vector3 island = FindPirateIsland();
        if (island == Vector3.zero) island = center + new Vector3(size.x * 0.16f, 0f, -size.z * 0.78f);

        CartelManualCreate[] creates = root.GetComponentsInChildren<CartelManualCreate>(true);
        for (int i = 0; i < creates.Length; i++)
        {
            CartelManualCreate create = creates[i];
            int index = Mathf.Max(0, ParseSuffix(create.name) - 1);
            Vector3 p = Resolve(create.Type, index, basePoint, coast, island, center, size, terrain);
            create.transform.position = p;
            create.transform.rotation = Quaternion.LookRotation((coast - p).WithY(0f).normalized, Vector3.up);
            create.EnabledForCartel = true;
            create.gameObject.SetActive(true);
            EditorUtility.SetDirty(create);
        }

        root.SetActive(true);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CartelSecondCountrySetup] Pais02 criado e posicionado longe da fronteira do Pais01. Ilha de apoio: " + island);
    }

    private static Vector3 Resolve(CartelCreateType type, int index, Vector3 basePoint, Vector3 coast, Vector3 island, Vector3 center, Vector3 size, Terrain terrain)
    {
        if (type == CartelCreateType.CartelBaseCreate || type == CartelCreateType.CartelBaseAreaCreate)
            return Ground(basePoint + new Vector3((index % 4) * 120f, 0f, (index % 2) * 100f), terrain);
        if (type == CartelCreateType.CartelTerrestreSpawnCreate || type == CartelCreateType.CartelVehicleParkingCreate)
            return Ground(basePoint + new Vector3(50f + index * 18f, 0f, -45f), terrain);
        if (type == CartelCreateType.CartelFuelStorageCreate || type == CartelCreateType.CartelReinforcementCreate)
            return Ground(basePoint + new Vector3(-45f, 0f, 35f), terrain);
        if (type == CartelCreateType.CartelBaseExitCreate)
            return Ground(basePoint + new Vector3(0f, 0f, -100f), terrain);
        if (type == CartelCreateType.CartelDefensePositionCreate)
            return Ground(basePoint + Polar(index * 45f, 145f), terrain);
        if (type == CartelCreateType.CartelTerrestreRouteCreate)
            return Ground(Vector3.Lerp(basePoint + new Vector3(0f, 0f, -100f), coast, (index + 1f) / 5f), terrain);
        if (type == CartelCreateType.CartelGroundTargetCreate || type == CartelCreateType.CartelTargetArrivalCreate)
            return Ground(basePoint + new Vector3(-250f + index * 110f, 0f, 250f), terrain);
        if (type == CartelCreateType.CartelAttackPositionCreate)
            return Ground(basePoint + new Vector3(-250f, 0f, 250f) + Polar(index * 90f, 65f), terrain);
        if (type == CartelCreateType.CartelAttackEscapeCreate || type == CartelCreateType.CartelTerrestrialEscapeCreate || type == CartelCreateType.CartelTerrestrialHideCreate)
            return Ground(basePoint + new Vector3(350f + index * 35f, 0f, -260f), terrain);
        if (type == CartelCreateType.CartelCoastalMeetingCreate)
            return Water(coast + new Vector3((index - 1) * 160f, 0f, index * 35f));
        if (type == CartelCreateType.CartelIslandSupportCreate || type == CartelCreateType.CartelIslandArrivalCreate)
            return Water(island + new Vector3(-100f + index * 40f, 0f, 0f));
        if (type == CartelCreateType.CartelMaritimeSpawnCreate || type == CartelCreateType.CartelBoatParkingCreate)
            return Water(island + Polar(index * 90f, 180f));
        if (type == CartelCreateType.CartelMaritimeExitCreate)
            return Water(island + new Vector3(0f, 0f, 220f));
        if (type == CartelCreateType.CartelMaritimePatrolCreate || type == CartelCreateType.CartelRobberyAreaCreate || type == CartelCreateType.OilPlatformExitCreate)
            return Water(center + new Vector3((index - 1.5f) * size.x * 0.16f, 0f, -size.z * (0.60f + index * 0.06f)));
        if (type == CartelCreateType.CartelMaritimeEscapeCreate || type == CartelCreateType.CartelMaritimeHideCreate)
            return Water(island + new Vector3((index - 1.5f) * 260f, 0f, index % 2 == 0 ? -300f : 300f));
        if (type == CartelCreateType.CartelExpansionCreate || type == CartelCreateType.CartelCountryEntryCreate || type == CartelCreateType.CartelLandEntryCreate)
            return Ground(basePoint + new Vector3(500f, 0f, -500f), terrain);
        if (type == CartelCreateType.CartelSeaEntryCreate)
            return Water(coast + new Vector3(500f, 0f, -160f));
        return Ground(basePoint, terrain);
    }

    private static Vector3 FindPirateIsland()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null || !objects[i].scene.IsValid()) continue;
            string n = objects[i].name.ToLowerInvariant();
            if (n.Contains("ilha pirata") || n.Contains("ilha")) return objects[i].transform.position;
        }
        return Vector3.zero;
    }

    private static Terrain FindValidTerrain()
    {
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++)
            if (terrains[i] != null && terrains[i].terrainData != null && terrains[i].terrainData.size.x > 1f && terrains[i].terrainData.size.z > 1f)
                return terrains[i];
        return null;
    }

    private static Vector3 Ground(Vector3 p, Terrain t)
    {
        Vector3 min = t.transform.position;
        Vector3 size = Vector3.Scale(t.terrainData.size, t.transform.lossyScale);
        if (p.x >= min.x && p.x <= min.x + size.x && p.z >= min.z && p.z <= min.z + size.z)
            p.y = t.SampleHeight(p) + t.transform.position.y + 0.15f;
        else p.y = 0f;
        return p;
    }

    private static Vector3 Water(Vector3 p) { p.y = 0f; return p; }
    private static Vector3 Polar(float degrees, float radius) { float r = degrees * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(r) * radius, 0f, Mathf.Sin(r) * radius); }
    private static int ParseSuffix(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out int v) ? v : 1; }
    private static GameObject FindSceneObject(string n)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].name == n && all[i].scene.IsValid()) return all[i];
        return null;
    }
}

internal static class CartelVectorExtensions
{
    public static Vector3 WithY(this Vector3 value, float y) { value.y = y; return value; }
}
#endif
