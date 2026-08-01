using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.Cartel;

/// <summary>
/// Configura o controlador do cartel e liga os Creates da cena ativa uma unica vez.
/// A execucao e armada pelo arquivo Assets/CartelSetup.install.
/// </summary>
[InitializeOnLoad]
public static class CartelSetupInstaller
{
    private const string MarkerPath = "Assets/CartelSetup.install";
    private const string CountryId = "Pais01";
    private const string ControllerName = "CartelAI_Pais01";
    private const string PrefabFolder = "Assets/Prefabs/Cartel";

    static CartelSetupInstaller()
    {
        EditorApplication.delayCall += TryInstall;
    }

    [MenuItem("Hegemonia/Cartel/Configurar controlador e prefabs")]
    public static void InstallFromMenu()
    {
        Install();
    }

    private static void TryInstall()
    {
        if (!File.Exists(MarkerPath)) return;
        try
        {
            Install();
        }
        finally
        {
            AssetDatabase.DeleteAsset(MarkerPath);
            AssetDatabase.Refresh();
        }
    }

    private static void Install()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[CartelSetupInstaller] Nenhuma cena valida esta aberta.");
            return;
        }

        EnsureFolder("Assets/Prefabs", "Cartel");
        GameObject basePrefab = CopyOrLoadPrefab(
            "Assets/Prefabs/Quartel_General/MilitaryBase.prefab", "CartelBase.prefab");
        GameObject groundMemberPrefab = CopyOrLoadPrefab(
            "Assets/Prefabs/Soldado_Rifle/Bandido.prefab", "CartelTerrestre.prefab");
        GameObject groundVehiclePrefab = CopyOrLoadPrefab(
            "Assets/Prefabs/Veiculos/Hamer/Hamer.prefab", "CartelVeiculo.prefab");
        GameObject maritimeMemberPrefab = CopyOrLoadPrefab(
            "Assets/Prefabs/Soldado_Rifle/Trabalhador_Conves.prefab", "CartelMaritimo.prefab");
        GameObject pirateBoatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Cartel/Barco cartel.prefab");

        CartelAIController controller = FindOrCreateController();
        controller.CartelTeamId = 9;
        controller.InitialCountryId = CountryId;
        controller.StartAutomatically = false;
        controller.EnableExpansion = false;
        controller.PlacementClearance = 3f;
        controller.PlacementSamples = 32;
        controller.PlacementBlockerLayers = DetectLayers(false);
        controller.WaterLayers = DetectLayers(true);
        if (controller.Prefabs == null) controller.Prefabs = new CartelPrefabSet();
        controller.Prefabs.BasePrefab = basePrefab;
        controller.Prefabs.GroundMemberPrefab = groundMemberPrefab;
        controller.Prefabs.GroundVehiclePrefab = groundVehiclePrefab;
        controller.Prefabs.GroundVehiclePrefabSecondary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Veiculos/Hamer/Hamer.prefab");
        controller.Prefabs.MaritimeMemberPrefab = maritimeMemberPrefab;
        controller.Prefabs.PirateBoatPrefab = pirateBoatPrefab;
        controller.Prefabs.CrewProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Soldado_Rifle/Bala.prefab");

        int createCount = ConfigureCreates();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format(
            "[CartelSetupInstaller] Configurado: controlador={0}, Creates ligados={1}, prefabs na pasta {2}. StartAutomatically ficou desativado ate posicionar os pontos.",
            controller.name, createCount, PrefabFolder));
    }

    private static CartelAIController FindOrCreateController()
    {
        CartelAIController existing = UnityEngine.Object.FindFirstObjectByType<CartelAIController>();
        if (existing != null) return existing;

        GameObject go = new GameObject(ControllerName);
        Undo.RegisterCreatedObjectUndo(go, "Criar controlador do cartel");
        return go.AddComponent<CartelAIController>();
    }

    private static int ConfigureCreates()
    {
        List<CartelManualCreate> creates = CartelManualCreate.GetAll(false);
        int configured = 0;
        for (int i = 0; i < creates.Count; i++)
        {
            CartelManualCreate create = creates[i];
            if (create == null) continue;

            if (string.IsNullOrEmpty(create.CountryId)) create.CountryId = CountryId;
            create.OwnerId = "Cartel";
            create.EnabledForCartel = true;

            int index = ParseSuffix(create.name);
            switch (create.Type)
            {
                case CartelCreateType.CartelBaseCreate:
                case CartelCreateType.CartelBaseAreaCreate:
                case CartelCreateType.CartelTerrestreSpawnCreate:
                case CartelCreateType.CartelBaseExitCreate:
                case CartelCreateType.CartelVehicleParkingCreate:
                case CartelCreateType.CartelFuelStorageCreate:
                case CartelCreateType.CartelDefensePositionCreate:
                case CartelCreateType.CartelReinforcementCreate:
                case CartelCreateType.CartelMaritimeSpawnCreate:
                case CartelCreateType.CartelMaritimeExitCreate:
                case CartelCreateType.CartelBoatParkingCreate:
                    create.LinkId = "Base_" + Mathf.Clamp(index, 1, 4).ToString("00", CultureInfo.InvariantCulture);
                    break;

                case CartelCreateType.CartelIslandSupportCreate:
                case CartelCreateType.CartelIslandArrivalCreate:
                    create.LinkId = "Island_" + Mathf.Clamp(index, 1, 4).ToString("00", CultureInfo.InvariantCulture);
                    break;

                case CartelCreateType.CartelGroundTargetCreate:
                case CartelCreateType.CartelTargetArrivalCreate:
                    create.LinkId = "Target_" + Mathf.Clamp(index, 1, 4).ToString("00", CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(create.TargetType)) create.TargetType = "Banco";
                    create.TargetCountryId = CountryId;
                    break;

                case CartelCreateType.CartelTerrestreRouteCreate:
                    create.RouteSetId = CountryId + "_RotaTerrestre";
                    create.RouteSequence = Mathf.Max(0, index - 1);
                    create.RouteKind = CartelRouteKind.Segura;
                    break;

                case CartelCreateType.CartelMaritimePatrolCreate:
                    create.RouteSetId = CountryId + "_PatrulhaMaritima";
                    create.RouteSequence = Mathf.Max(0, index - 1);
                    create.RouteKind = CartelRouteKind.Costeira;
                    break;
            }

            EditorUtility.SetDirty(create);
            configured++;
        }

        return configured;
    }

    private static int ParseSuffix(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return 1;
        int separator = objectName.LastIndexOf('_');
        if (separator >= 0 && int.TryParse(objectName.Substring(separator + 1), out int parsed)) return parsed;
        return 1;
    }

    private static GameObject CopyOrLoadPrefab(string source, string destinationName)
    {
        string destination = PrefabFolder + "/" + destinationName;
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
        if (existing != null) return existing;

        if (!File.Exists(source))
        {
            Debug.LogWarning("[CartelSetupInstaller] Prefab nao encontrado: " + source);
            return null;
        }

        if (!AssetDatabase.CopyAsset(source, destination))
        {
            Debug.LogWarning("[CartelSetupInstaller] Nao foi possivel copiar: " + source);
            return null;
        }

        AssetDatabase.ImportAsset(destination);
        return AssetDatabase.LoadAssetAtPath<GameObject>(destination);
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static LayerMask DetectLayers(bool water)
    {
        int mask = 0;
        for (int i = 0; i < 32; i++)
        {
            string layer = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layer)) continue;
            string normalized = layer.ToLowerInvariant();
            bool isWater = normalized.Contains("water") || normalized.Contains("agua") || normalized.Contains("mar") || normalized.Contains("oceano");
            bool isBlocker = normalized.Contains("building") || normalized.Contains("predio") || normalized.Contains("pred") || normalized.Contains("road") || normalized.Contains("estrad") || normalized.Contains("obst");
            if ((water && isWater) || (!water && isBlocker)) mask |= 1 << i;
        }
        return mask;
    }
}
