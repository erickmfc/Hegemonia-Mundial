#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.Cartel;

/// <summary>
/// Posiciona o pacote de Creates do cartel em uma distribuicao inicial segura.
/// O designer ainda pode ajustar qualquer ponto depois no Scene View.
/// </summary>
public static class CartelCreateAutoPositioner
{
    private const string RootName = "CartelManualCreates_Pais01";

    [MenuItem("Hegemonia/Cartel/Posicionar Creates na Cena Atual")]
    public static void PositionCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[CartelCreateAutoPositioner] Nenhuma cena valida esta aberta.");
            return;
        }

        GameObject root = FindSceneObject(RootName);
        if (root == null)
        {
            Debug.LogWarning("[CartelCreateAutoPositioner] Raiz nao encontrada: " + RootName);
            return;
        }

        Terrain[] terrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Terrain activeTerrain = null;
        for (int i = 0; i < terrain.Length; i++)
        {
            Terrain candidate = terrain[i];
            if (candidate == null || candidate.terrainData == null || !candidate.gameObject.scene.IsValid()) continue;
            Vector3 size = Vector3.Scale(candidate.terrainData.size, candidate.transform.lossyScale);
            if (size.x <= 1f || size.z <= 1f) continue;
            activeTerrain = candidate;
            break;
        }

        if (activeTerrain == null)
        {
            Debug.LogWarning("[CartelCreateAutoPositioner] Terrain valido nao encontrado.");
            return;
        }

        Vector3 terrainSize = Vector3.Scale(activeTerrain.terrainData.size, activeTerrain.transform.lossyScale);
        Vector3 terrainMin = activeTerrain.transform.position;
        Vector3 terrainCenter = terrainMin + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);

        List<CartelManualCreate> creates = new List<CartelManualCreate>();
        CartelManualCreate[] discovered = UnityEngine.Object.FindObjectsByType<CartelManualCreate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < discovered.Length; i++)
        {
            CartelManualCreate create = discovered[i];
            if (create == null || !create.gameObject.scene.IsValid()) continue;
            if (create.transform.IsChildOf(root.transform)) creates.Add(create);
        }

        Vector3[] bases =
        {
            terrainCenter + new Vector3(-terrainSize.x * 0.24f, 0f, terrainSize.z * 0.18f),
            terrainCenter + new Vector3(-terrainSize.x * 0.08f, 0f, terrainSize.z * 0.22f),
            terrainCenter + new Vector3( terrainSize.x * 0.10f, 0f, terrainSize.z * 0.18f),
            terrainCenter + new Vector3( terrainSize.x * 0.24f, 0f, terrainSize.z * 0.22f)
        };

        Vector3 coast = terrainCenter + new Vector3(0f, 0f, -terrainSize.z * 0.54f);
        Vector3[] coastal =
        {
            coast + new Vector3(-terrainSize.x * 0.18f, 0f, 0f),
            coast + new Vector3(-terrainSize.x * 0.06f, 0f, -35f),
            coast + new Vector3( terrainSize.x * 0.08f, 0f, 25f)
        };

        Vector3 island = terrainCenter + new Vector3(terrainSize.x * 0.16f, 0f, -terrainSize.z * 0.78f);
        Vector3[] maritime =
        {
            island + new Vector3(-180f, 0f, -120f),
            island + new Vector3(-60f, 0f, 80f),
            island + new Vector3(80f, 0f, -40f),
            island + new Vector3(210f, 0f, 110f)
        };

        Vector3[] targets =
        {
            terrainCenter + new Vector3(-terrainSize.x * 0.14f, 0f, terrainSize.z * 0.04f),
            terrainCenter + new Vector3(-terrainSize.x * 0.02f, 0f, terrainSize.z * 0.08f),
            terrainCenter + new Vector3( terrainSize.x * 0.12f, 0f, terrainSize.z * 0.02f),
            terrainCenter + new Vector3( terrainSize.x * 0.20f, 0f, terrainSize.z * 0.10f)
        };

        for (int i = 0; i < creates.Count; i++)
        {
            CartelManualCreate create = creates[i];
            string name = create.name;
            int index = ParseSuffix(name) - 1;
            Vector3 position = ResolvePosition(create.Type, index, bases, coastal, maritime, targets, coast, island, terrainCenter, terrainSize);
            SetWorldPosition(create.transform, position);

            Vector3 look = ResolveLookTarget(create.Type, index, bases, coastal, maritime, targets, coast);
            look.y = position.y;
            Vector3 direction = look - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.1f)
                create.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            create.gameObject.SetActive(true);
            EditorUtility.SetDirty(create);
        }

        root.SetActive(true);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(string.Format("[CartelCreateAutoPositioner] {0} Creates posicionados na cena {1}. Terreno: centro={2} tamanho={3}.",
            creates.Count, scene.name, terrainCenter.ToString("F1"), terrainSize.ToString("F1")));
    }

    private static Vector3 ResolvePosition(CartelCreateType type, int index, Vector3[] bases, Vector3[] coastal,
        Vector3[] maritime, Vector3[] targets, Vector3 coast, Vector3 island, Vector3 terrainCenter, Vector3 terrainSize)
    {
        index = Mathf.Clamp(index, 0, 7);
        switch (type)
        {
            case CartelCreateType.CartelBaseCreate:
            case CartelCreateType.CartelBaseAreaCreate:
                return Ground(bases[Mathf.Min(index, bases.Length - 1)]);
            case CartelCreateType.CartelTerrestreSpawnCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(35f, 0f, -20f));
            case CartelCreateType.CartelBaseExitCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(0f, 0f, -90f));
            case CartelCreateType.CartelVehicleParkingCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(45f, 0f, -45f));
            case CartelCreateType.CartelFuelStorageCreate:
                return Ground(bases[0] + new Vector3(-35f, 0f, 35f));
            case CartelCreateType.CartelDefensePositionCreate:
                return Ground(bases[Mathf.Min(index / 2, 3)] + Polar(index * 45f, 125f));
            case CartelCreateType.CartelReinforcementCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(-45f, 0f, 35f));
            case CartelCreateType.CartelTerrestreRouteCreate:
                return Ground(Vector3.Lerp(bases[0] + new Vector3(0f, 0f, -90f), coast, (index + 1f) / 5f));
            case CartelCreateType.CartelTerrestrialEscapeCreate:
            case CartelCreateType.CartelTerrestrialHideCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(260f + index * 45f, 0f, -180f));
            case CartelCreateType.CartelGroundTargetCreate:
                return Ground(targets[Mathf.Min(index, 3)]);
            case CartelCreateType.CartelTargetArrivalCreate:
                return Ground(targets[Mathf.Min(index, 3)] + new Vector3(-45f, 0f, -35f));
            case CartelCreateType.CartelAttackPositionCreate:
                return Ground(targets[Mathf.Min(index / 2, 3)] + Polar(index * 90f, 65f));
            case CartelCreateType.CartelAttackEscapeCreate:
                return Ground(targets[0] + new Vector3(0f, 0f, -120f));
            case CartelCreateType.CartelCoastalMeetingCreate:
                return Water(coastal[Mathf.Min(index, coastal.Length - 1)]);
            case CartelCreateType.CartelIslandSupportCreate:
                return Water(island);
            case CartelCreateType.CartelIslandArrivalCreate:
                return Water(island + new Vector3(-100f, 0f, 0f));
            case CartelCreateType.CartelMaritimeSpawnCreate:
            case CartelCreateType.CartelBoatParkingCreate:
                return Water(maritime[Mathf.Min(index, maritime.Length - 1)]);
            case CartelCreateType.CartelMaritimeExitCreate:
                return Water(island + new Vector3(0f, 0f, 180f));
            case CartelCreateType.CartelMaritimePatrolCreate:
                return Water(terrainCenter + new Vector3((index - 1.5f) * terrainSize.x * 0.16f, 0f, -terrainSize.z * (0.60f + index * 0.08f)));
            case CartelCreateType.CartelRobberyAreaCreate:
                return Water(terrainCenter + new Vector3(-terrainSize.x * 0.10f, 0f, -terrainSize.z * 0.62f));
            case CartelCreateType.OilPlatformExitCreate:
                return Water(terrainCenter + new Vector3(-terrainSize.x * 0.10f, 0f, -terrainSize.z * 0.58f));
            case CartelCreateType.CartelMaritimeEscapeCreate:
            case CartelCreateType.CartelMaritimeHideCreate:
                return Water(island + new Vector3((index - 1.5f) * 260f, 0f, (index % 2 == 0 ? -300f : 300f)));
            case CartelCreateType.CartelHideCreate:
                return Ground(bases[Mathf.Min(index, 3)] + new Vector3(-300f, 0f, 220f));
            case CartelCreateType.CartelExpansionCreate:
                return Ground(terrainCenter + new Vector3(terrainSize.x * 0.42f, 0f, terrainSize.z * 0.35f));
            case CartelCreateType.CartelCountryEntryCreate:
            case CartelCreateType.CartelLandEntryCreate:
                return Ground(terrainCenter + new Vector3(terrainSize.x * 0.44f, 0f, -terrainSize.z * 0.15f));
            case CartelCreateType.CartelSeaEntryCreate:
                return Water(coast + new Vector3(terrainSize.x * 0.35f, 0f, -120f));
            default:
                return Ground(terrainCenter);
        }
    }

    private static Vector3 ResolveLookTarget(CartelCreateType type, int index, Vector3[] bases, Vector3[] coastal,
        Vector3[] maritime, Vector3[] targets, Vector3 coast)
    {
        if (type == CartelCreateType.CartelTerrestreRouteCreate) return index < 3 ? coastal[0] : bases[0];
        if (type == CartelCreateType.CartelMaritimePatrolCreate || type == CartelCreateType.CartelMaritimeEscapeCreate) return maritime[Mathf.Min(index, maritime.Length - 1)];
        if (type == CartelCreateType.CartelBaseExitCreate || type == CartelCreateType.CartelTerrestrialEscapeCreate) return coast;
        if (type == CartelCreateType.CartelTargetArrivalCreate || type == CartelCreateType.CartelAttackPositionCreate) return targets[Mathf.Min(index / 2, targets.Length - 1)];
        return Vector3.zero;
    }

    private static Vector3 Ground(Vector3 position)
    {
        Terrain terrain = null;
        Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Terrain candidate in terrains)
        {
            if (candidate == null || candidate.terrainData == null) continue;
            Vector3 candidateSize = Vector3.Scale(candidate.terrainData.size, candidate.transform.lossyScale);
            if (candidateSize.x > 1f && candidateSize.z > 1f)
            {
                terrain = candidate;
                break;
            }
        }
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 min = terrain.transform.position;
            Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
            if (position.x >= min.x && position.x <= min.x + size.x && position.z >= min.z && position.z <= min.z + size.z)
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.15f;
            else position.y = 0f;
        }
        else position.y = 0f;
        return position;
    }

    private static Vector3 Water(Vector3 position)
    {
        position.y = 0f;
        return position;
    }

    private static Vector3 Polar(float degrees, float radius)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
    }

    private static int ParseSuffix(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return 1;
        int separator = objectName.LastIndexOf('_');
        return separator >= 0 && int.TryParse(objectName.Substring(separator + 1), out int result) ? result : 1;
    }

    private static void SetWorldPosition(Transform transform, Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go != null && go.name == objectName && go.scene.IsValid()) return go;
        }
        return null;
    }
}
#endif
