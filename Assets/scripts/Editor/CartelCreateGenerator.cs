#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Hegemonia.Cartel;

/// <summary>
/// Gera todos os Creates do cartel com nomes, tipos e LinkId padronizados.
/// Os pontos sao criados desativados e precisam ser posicionados pelo designer.
/// </summary>
public sealed class CartelCreateGeneratorWindow : EditorWindow
{
    private string countryId = "Pais01";
    private bool keepCreatedObjectsDisabled = true;
    private bool createReferencePoints = true;

    [MenuItem("Hegemonia/Cartel/Gerador de Creates Manuais")]
    public static void Open()
    {
        GetWindow<CartelCreateGeneratorWindow>("Creates do Cartel");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Pacote completo de Creates", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "O gerador cria os nomes e componentes, mas nao conhece as coordenadas do seu mapa. "
            + "Depois de gerar, mova os pontos, configure os prefabs e ative os objetos.",
            MessageType.Info);

        countryId = EditorGUILayout.TextField("Pais", countryId);
        keepCreatedObjectsDisabled = EditorGUILayout.ToggleLeft("Criar pontos desativados", keepCreatedObjectsDisabled);
        createReferencePoints = EditorGUILayout.ToggleLeft("Criar referencias de cidade/policia/exercito/estrada", createReferencePoints);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("O pacote inclui:");
        EditorGUILayout.LabelField("Bases: 4 candidatos, 4 areas, 4 spawns, 4 saidas, 4 estacionamentos");
        EditorGUILayout.LabelField("Maritimo: 4 spawns, 4 patrulhas, 3 encontros, 4 fugas, 4 estacionamentos");
        EditorGUILayout.LabelField("Terrestre: 4 rotas, 4 fugas, 4 alvos, 4 posicoes de ataque");
        EditorGUILayout.LabelField("Ilhas, armazenamento, entradas, defesa, reforcos e expansao");

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(countryId)))
        {
            if (GUILayout.Button("Criar ou atualizar pacote completo", GUILayout.Height(32f)))
            {
                CartelCreateGenerator.CreatePackage(countryId.Trim(), keepCreatedObjectsDisabled, createReferencePoints);
            }
        }
    }
}

public static class CartelCreateGenerator
{
    private sealed class Definition
    {
        public string Name;
        public CartelCreateType Type;
        public int Count;
        public float Radius;
        public string LinkPrefix;
        public string RoutePrefix;
        public bool RequiresCountry = true;
        public string TargetType;
    }

    public static void CreatePackage(string countryId, bool disabled, bool createReferencePoints)
    {
        CreatePackageInternal(countryId, disabled, createReferencePoints, true);
    }

    public static void CreatePackageSilently(string countryId, bool disabled, bool createReferencePoints)
    {
        CreatePackageInternal(countryId, disabled, createReferencePoints, false);
    }

    private static void CreatePackageInternal(string countryId, bool disabled, bool createReferencePoints, bool showDialog)
    {
        if (string.IsNullOrWhiteSpace(countryId))
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Creates do Cartel", "Informe um CountryId valido.", "OK");
            }
            return;
        }

        countryId = countryId.Trim();
        GameObject root = GetOrCreateRoot(countryId);
        List<Definition> definitions = BuildDefinitions();
        int created = 0;
        int updated = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            Definition definition = definitions[i];
            for (int index = 1; index <= definition.Count; index++)
            {
                string objectName = string.Format("{0}_{1:00}", definition.Name, index);
                string linkId = string.IsNullOrEmpty(definition.LinkPrefix)
                    ? string.Empty
                    : string.Format("{0}_{1:00}", definition.LinkPrefix, index);
                string routeSetId = string.IsNullOrEmpty(definition.RoutePrefix) ? string.Empty : countryId + "_" + definition.RoutePrefix;
                bool existed;
                CartelManualCreate create = GetOrCreateCreate(root.transform, objectName, out existed);
                create.Type = definition.Type;
                create.CountryId = definition.RequiresCountry ? countryId : string.Empty;
                create.OwnerId = "Cartel";
                create.LinkId = linkId;
                create.Radius = Mathf.Max(0.5f, definition.Radius);
                create.RouteSequence = index - 1;
                create.RouteSetId = routeSetId;
                create.EnabledForCartel = !disabled;
                create.HideRendererAtRuntime = true;
                create.DrawGizmo = true;
                create.TargetType = definition.TargetType ?? string.Empty;
                if (definition.Type == CartelCreateType.CartelGroundTargetCreate)
                {
                    create.TargetCountryId = countryId;
                    create.AllowsRobbery = true;
                    create.EconomicValue = 100f * index;
                    create.SecurityLevel = Mathf.Clamp(index, 1, 10);
                }

                if (existed) updated++; else created++;
                ApplyCreateDefaults(create, definition.Type, index);
                create.gameObject.SetActive(!disabled);
                SaveObject(create.gameObject);
            }
        }

        if (createReferencePoints)
        {
            CartelCreateType[] referenceTypes =
            {
                CartelCreateType.CityReference,
                CartelCreateType.PoliceReference,
                CartelCreateType.MilitaryReference,
                CartelCreateType.BusyRoadReference
            };

            for (int i = 0; i < referenceTypes.Length; i++)
            {
                string label = referenceTypes[i].ToString();
                bool existed;
                CartelManualCreate create = GetOrCreateCreate(root.transform, countryId + "_" + label + "_01", out existed);
                create.Type = referenceTypes[i];
                create.CountryId = countryId;
                create.OwnerId = "CartelReference";
                create.Radius = 8f;
                create.EnabledForCartel = !disabled;
                create.gameObject.SetActive(!disabled);
                if (existed) updated++; else created++;
                SaveObject(create.gameObject);
            }
        }

        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Creates do Cartel",
                string.Format("Pacote de {0} concluido. Criados: {1}. Atualizados: {2}.\n\nPosicione os objetos e ative os pontos que serao validos.", countryId, created, updated),
                "OK");
        }
        Debug.Log(string.Format("[CartelCreateGenerator] Pais {0}: criados={1}, atualizados={2}.", countryId, created, updated));
    }

    private static List<Definition> BuildDefinitions()
    {
        return new List<Definition>
        {
            Def("BaseCreate", CartelCreateType.CartelBaseCreate, 4, 12f, "Base"),
            Def("BaseAreaCreate", CartelCreateType.CartelBaseAreaCreate, 4, 100f, "Base"),
            Def("TerrestreSpawn", CartelCreateType.CartelTerrestreSpawnCreate, 4, 8f, "Base"),
            Def("BaseExitCreate", CartelCreateType.CartelBaseExitCreate, 4, 8f, "Base"),
            Def("TerrestreRouteCreate", CartelCreateType.CartelTerrestreRouteCreate, 4, 12f, null, "RotaTerrestre"),
            Def("CoastalMeeting", CartelCreateType.CartelCoastalMeetingCreate, 3, 30f),
            Def("IslandSupport", CartelCreateType.CartelIslandSupportCreate, 1, 80f),
            Def("IslandArrival", CartelCreateType.CartelIslandArrivalCreate, 1, 30f),
            Def("MaritimeSpawn", CartelCreateType.CartelMaritimeSpawnCreate, 4, 12f),
            Def("MaritimeExitCreate", CartelCreateType.CartelMaritimeExitCreate, 1, 15f),
            Def("MaritimePatrol", CartelCreateType.CartelMaritimePatrolCreate, 4, 80f, null, "PatrulhaMaritima"),
            Def("RobberyArea", CartelCreateType.CartelRobberyAreaCreate, 1, 300f),
            Def("OilPlatformExit", CartelCreateType.OilPlatformExitCreate, 1, 25f),
            Def("MaritimeEscape", CartelCreateType.CartelMaritimeEscapeCreate, 4, 100f),
            Def("TerrestrialEscape", CartelCreateType.CartelTerrestrialEscapeCreate, 4, 30f),
            Def("Hide", CartelCreateType.CartelHideCreate, 2, 35f),
            Def("MaritimeHide", CartelCreateType.CartelMaritimeHideCreate, 2, 45f),
            Def("TerrestrialHide", CartelCreateType.CartelTerrestrialHideCreate, 2, 35f),
            Def("BoatParking", CartelCreateType.CartelBoatParkingCreate, 4, 12f),
            Def("VehicleParking", CartelCreateType.CartelVehicleParkingCreate, 4, 8f, "Base"),
            Def("FuelStorage", CartelCreateType.CartelFuelStorageCreate, 1, 15f, "Base"),
            Def("GroundTarget", CartelCreateType.CartelGroundTargetCreate, 4, 30f, null, null, "Banco"),
            Def("TargetArrival", CartelCreateType.CartelTargetArrivalCreate, 4, 15f),
            Def("AttackPosition", CartelCreateType.CartelAttackPositionCreate, 4, 8f),
            Def("AttackEscape", CartelCreateType.CartelAttackEscapeCreate, 1, 20f),
            Def("Expansion", CartelCreateType.CartelExpansionCreate, 1, 30f),
            Def("CountryEntry", CartelCreateType.CartelCountryEntryCreate, 1, 25f),
            Def("SeaEntry", CartelCreateType.CartelSeaEntryCreate, 1, 25f),
            Def("LandEntry", CartelCreateType.CartelLandEntryCreate, 1, 25f),
            Def("DefensePosition", CartelCreateType.CartelDefensePositionCreate, 8, 10f, "Base"),
            Def("Reinforcement", CartelCreateType.CartelReinforcementCreate, 4, 12f, "Base")
        };
    }

    private static Definition Def(string name, CartelCreateType type, int count, float radius, string linkPrefix = null, string routePrefix = null, string targetType = null)
    {
        return new Definition
        {
            Name = name,
            Type = type,
            Count = count,
            Radius = radius,
            LinkPrefix = linkPrefix,
            RoutePrefix = routePrefix,
            TargetType = targetType
        };
    }

    private static GameObject GetOrCreateRoot(string countryId)
    {
        string rootName = "CartelManualCreates_" + countryId;
        GameObject root = GameObject.Find(rootName);
        if (root != null) return root;
        root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Criar raiz de Creates do Cartel");
        return root;
    }

    private static CartelManualCreate GetOrCreateCreate(Transform parent, string objectName, out bool existed)
    {
        Transform child = parent.Find(objectName);
        GameObject go;
        if (child != null && child.GetComponent<CartelManualCreate>() != null)
        {
            go = child.gameObject;
            existed = true;
        }
        else
        {
            go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Criar Create do Cartel");
            existed = false;
        }

        CartelManualCreate create = go.GetComponent<CartelManualCreate>();
        if (create == null) create = Undo.AddComponent<CartelManualCreate>(go);
        go.SetActive(false);
        return create;
    }

    private static void ApplyCreateDefaults(CartelManualCreate create, CartelCreateType type, int index)
    {
        create.AvoidWater = type == CartelCreateType.CartelBaseAreaCreate;
        create.AvoidBuildings = type == CartelCreateType.CartelBaseAreaCreate;
        create.AvoidRoads = type == CartelCreateType.CartelBaseAreaCreate;
        create.RequiresSafeArea = type == CartelCreateType.CartelMaritimeEscapeCreate
            || type == CartelCreateType.CartelTerrestrialEscapeCreate
            || type == CartelCreateType.CartelHideCreate
            || type == CartelCreateType.CartelMaritimeHideCreate
            || type == CartelCreateType.CartelTerrestrialHideCreate;
        create.RouteKind = type == CartelCreateType.CartelTerrestrialEscapeCreate
            || type == CartelCreateType.CartelMaritimeEscapeCreate
            ? CartelRouteKind.Fuga
            : create.RouteKind;
        create.MaxOccupants = type == CartelCreateType.CartelBoatParkingCreate
            || type == CartelCreateType.CartelVehicleParkingCreate
            || type == CartelCreateType.CartelAttackPositionCreate
            ? 1
            : create.MaxOccupants;
        create.BasePreference = type == CartelCreateType.CartelBaseCreate ? index * 0.01f : create.BasePreference;
    }

    private static void SaveObject(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
    }
}
#endif
