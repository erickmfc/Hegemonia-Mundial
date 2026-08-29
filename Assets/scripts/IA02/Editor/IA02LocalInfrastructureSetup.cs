#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hegemonia.AI.IA02;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converte os marcadores temporarios "Local" do IA02CityLayout em slots nomeados
/// da abertura. Os marcadores continuam editaveis na cena: basta move-los depois
/// para reposicionar a infraestrutura sem alterar o roteiro da IA.
/// </summary>
public static class IA02LocalInfrastructureSetup
{
    private const int IA02TeamId = 3;
    private const string PlanPath = "Assets/IA02/BuildPlans/IA02BuildPlan.asset";
    private const string LegacyGeneratedPlanPath = "Assets/IA02/BuildPlans/IA02Controller_IA02BuildPlan.asset";

    private sealed class LocalDefinition
    {
        public string SlotId;
        public string Name;
        public string AssetPath;
        public IA02StrategicRole Role;
        public IA02BuildDomain Domain;
        public bool Airport;
        public bool Naval;
        public string StepId;

        public LocalDefinition(string slotId, string name, string assetPath, IA02StrategicRole role, IA02BuildDomain domain, string stepId, bool airport = false, bool naval = false)
        {
            SlotId = slotId;
            Name = name;
            AssetPath = assetPath;
            Role = role;
            Domain = domain;
            StepId = stepId;
            Airport = airport;
            Naval = naval;
        }
    }

    private static readonly LocalDefinition[] Definitions =
    {
        new LocalDefinition("ia02.local.tenda", "IA02 Create - Tenda Militar", "Assets/Prefabs/Construtor de Veiculos/Tenda/Construcao_Tenda.asset", IA02StrategicRole.MilitaryProduction, IA02BuildDomain.Land, "abertura.militar.tenda"),
        new LocalDefinition("ia02.local.casa", "IA02 Create - Casa", "Assets/Prefabs/Imobiliario/casa/Casa.asset", IA02StrategicRole.Residential, IA02BuildDomain.Land, "abertura.residencial.casa"),
        new LocalDefinition("ia02.local.apartamento_medio", "IA02 Create - Apartamento Medio", "Assets/Prefabs/Imobiliario/Pred Medio/Predio Medio.asset", IA02StrategicRole.Residential, IA02BuildDomain.Land, "abertura.residencial.apartamento_medio"),
        new LocalDefinition("ia02.local.apartamento_alto", "IA02 Create - Apartamento Alto", "Assets/Prefabs/Imobiliario/Perd Hard/Pred Hard.asset", IA02StrategicRole.Residential, IA02BuildDomain.Land, "abertura.residencial.apartamento_alto"),
        new LocalDefinition("ia02.local.construtor_veiculos", "IA02 Create - Construtor de Veiculos", "Assets/Prefabs/Construtor de Veiculos/Construtor de Veiculos.asset", IA02StrategicRole.MilitaryProduction, IA02BuildDomain.Land, "abertura.militar.construtor_veiculos"),
        new LocalDefinition("ia02.local.aeroporto_militar", "IA02 Create - Aeroporto Militar", "Assets/Prefabs/Aeroporto/Aeroporto militar.asset", IA02StrategicRole.Airfield, IA02BuildDomain.Airfield, "abertura.aereo.aeroporto_militar", true),
        new LocalDefinition("ia02.local.aeroporto_comercial", "IA02 Create - Aeroporto Comercial", "Assets/Prefabs/Aeroporto/Aeroporto comercial/Aeroporto comercial.asset", IA02StrategicRole.Airfield, IA02BuildDomain.Airfield, "abertura.aereo.aeroporto_comercial", true),
        new LocalDefinition("ia02.local.estaleiro", "IA02 Create - Estaleiro Naval", "Assets/Prefabs/Estaleiro Marinho/Estaleiro_Naval.asset", IA02StrategicRole.Shipyard, IA02BuildDomain.Coastal, "naval.naval.estaleiro", false, true)
    };

    private static readonly LocalDefinition PierDefinition =
        new LocalDefinition("ia02.local.pier", "IA02 Create - Pier Naval", "Assets/Prefabs/Marinha/Pier_marinha.asset", IA02StrategicRole.Pier, IA02BuildDomain.Coastal, "naval.pier");

    private static readonly LocalDefinition PlatformDefinition =
        new LocalDefinition(string.Empty, "IA02 Create - Plataforma Offshore", "Assets/Prefabs/Marinha/PLataforma.asset", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, "naval.plataforma");

    // Create opcional para o quartel. Usa a ficha/prefab da tenda militar,
    // que ja possui Fabrica.ehQuartel, mas com um slot proprio editavel.
    private static readonly LocalDefinition QuartelDefinition =
        new LocalDefinition("ia02.local.quartel", "IA02 Create - Quartel Militar", "Assets/Prefabs/Construtor de Veiculos/Tenda/Construcao_Tenda.asset", IA02StrategicRole.MilitaryProduction, IA02BuildDomain.Land, "abertura.militar.quartel");

    [MenuItem("Tools/IA02/Configurar Locais para Infraestrutura Inicial")]
    private static void Configure()
    {
        if (!NormalizeScene(SceneManager.GetActiveScene()))
        {
            EditorUtility.DisplayDialog("IA02", "Nenhum IA02CityLayout foi encontrado na cena aberta.", "OK");
        }
    }

    [MenuItem("Tools/IA02/Normalizar e reparar creates da IA02")]
    private static void NormalizeActiveScene()
    {
        if (!NormalizeScene(SceneManager.GetActiveScene()))
        {
            EditorUtility.DisplayDialog("IA02", "Nenhum IA02CityLayout foi encontrado na cena aberta.", "OK");
        }
    }

    // Entry point usado pela validacao da build para garantir que a cena entregue
    // ao executavel ja tenha os creates unicos e os planos sincronizados.
    public static void NormalizeCampaignScene()
    {
        const string scenePath = ConfiguracaoCenasJogo.CaminhoCenaCampanhaCanonica;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!NormalizeScene(scene))
        {
            Debug.LogError("[IA02] IA02CityLayout ausente em " + scenePath);
            return;
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA02] Creates da campanha normalizados e cena salva: " + scenePath);
    }

    private static bool NormalizeScene(Scene scene)
    {
        if (!scene.IsValid()) return false;
        IA02CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null) return false;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Normalizar creates da infraestrutura IA02");
        NormalizeLegacySlotIds(layout.transform);
        for (int i = 0; i < Definitions.Length; i++)
        {
            Transform marker = EnsureCanonicalSlot(layout.transform, Definitions[i], new Vector3((i - 3) * 42f, 0f, 80f + (i % 2) * 42f));
            ConfigureSlot(marker, Definitions[i]);
        }

        Transform quartel = EnsureCanonicalSlot(layout.transform, QuartelDefinition, new Vector3(72f, 0f, 118f));
        ConfigureSlot(quartel, QuartelDefinition);
        Transform pier = EnsureCanonicalSlot(layout.transform, PierDefinition, new Vector3(-90f, 0f, 135f));
        ConfigureSlot(pier, PierDefinition);
        ConfigureNavalAuxiliary(pier);

        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Armazenamento 01", "ia02.local.armazenamento.01", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(-48f, 0f, 52f));
        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Armazenamento 02", "ia02.local.armazenamento.02", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(0f, 0f, 66f));
        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Armazenamento 03", "ia02.local.armazenamento.03", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(48f, 0f, 52f));
        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Plataforma Offshore A", "ia02.local.plataforma.a", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(120f, 0f, 200f));
        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Plataforma Offshore B", "ia02.local.plataforma.b", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(-180f, 0f, 260f));
        NormalizeAuxiliarySlot(layout.transform, "IA02 Create - Plataforma Offshore C", "ia02.local.plataforma.c", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(300f, 0f, 320f));

        ConfigurePlan();
        layout.EnsureRuntimeReady();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA02] Creates unicos configurados por slotId, com duplicatas removidas.", layout);
        return true;
    }

    [MenuItem("Tools/IA02/Criar locais navais e de logistica")]
    private static void CreateNavalAndLogisticsLocals()
    {
        IA02CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null)
        {
            EditorUtility.DisplayDialog("IA02", "Nenhum IA02CityLayout foi encontrado na cena aberta.", "OK");
            return;
        }

        CreateAuxiliarySlot(layout.transform, "IA02 Local - Armazenamento 01", "ia02.local.armazenamento.01", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(-48f, 0f, 52f));
        CreateAuxiliarySlot(layout.transform, "IA02 Local - Armazenamento 02", "ia02.local.armazenamento.02", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(0f, 0f, 66f));
        CreateAuxiliarySlot(layout.transform, "IA02 Local - Armazenamento 03", "ia02.local.armazenamento.03", IA02StrategicRole.Storage, IA02BuildDomain.Land, new Vector3(48f, 0f, 52f));
        CreateAuxiliarySlot(layout.transform, QuartelDefinition.Name, QuartelDefinition.SlotId, QuartelDefinition.Role, QuartelDefinition.Domain, new Vector3(72f, 0f, 118f));
        Transform pier = CreateAuxiliarySlot(layout.transform, "IA02 Local - Pier Naval", "ia02.local.pier", IA02StrategicRole.Pier, IA02BuildDomain.Coastal, new Vector3(-90f, 0f, 135f));
        ConfigureNavalAuxiliary(pier);
        CreateAuxiliarySlot(layout.transform, "IA02 Local - Plataforma de Petróleo A", "ia02.local.plataforma.a", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(120f, 0f, 200f));
        CreateAuxiliarySlot(layout.transform, "IA02 Local - Plataforma de Petróleo B", "ia02.local.plataforma.b", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(-180f, 0f, 260f));
        CreateAuxiliarySlot(layout.transform, "IA02 Local - Plataforma de Petróleo C", "ia02.local.plataforma.c", IA02StrategicRole.NavalBase, IA02BuildDomain.Coastal, new Vector3(300f, 0f, 320f));

        ConfigurePlan();

        Transform shipyard = FindSlotById(layout.transform, "ia02.local.estaleiro");
        if (shipyard != null)
        {
            CreatePatrolZone(shipyard, "IA02 Patrulha Naval - Área A", new Vector3(120f, 0f, 160f));
            CreatePatrolZone(shipyard, "IA02 Patrulha Naval - Área B", new Vector3(-140f, 0f, 220f));
            CreatePatrolZone(shipyard, "IA02 Patrulha Naval - Área C", new Vector3(250f, 0f, 300f));
            CreateWarAdvanceZone(shipyard, "IA02 WarAdvanceZone Naval A", new Vector3(180f, 0f, 220f), IA02WarAdvanceZone.Dominio.Naval);
            CreateWarAdvanceZone(shipyard, "IA02 WarAdvanceZone Naval B", new Vector3(-220f, 0f, 300f), IA02WarAdvanceZone.Dominio.Naval);
            CreateExtractionZone(shipyard, "IA02 ExtractionZone Naval", new Vector3(-40f, 0f, 150f));
        }

        Transform airport = FindSlotById(layout.transform, "ia02.local.aeroporto_militar");
        if (airport != null)
        {
            CreateAirPatrolZone(airport, "IA02 Patrulha Aerea - Área Inicial", new Vector3(0f, 0f, 280f));
            CreateWarAdvanceZone(airport, "IA02 WarAdvanceZone Aerea", new Vector3(0f, 100f, 320f), IA02WarAdvanceZone.Dominio.Aereo);
        }

        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        Debug.Log("[IA02] Criados os 3 locais de armazém, pier, 3 locais de plataforma e 3 áreas de patrulha naval. A validação naval deve confirmar água e profundidade antes da execução.", layout);
    }

    [MenuItem("Tools/IA02/Criar local de quartel militar")]
    private static void CreateQuartelLocal()
    {
        IA02CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null)
        {
            EditorUtility.DisplayDialog("IA02", "Nenhum IA02CityLayout foi encontrado na cena aberta.", "OK");
            return;
        }

        Transform marker = CreateAuxiliarySlot(
            layout.transform,
            QuartelDefinition.Name,
            QuartelDefinition.SlotId,
            QuartelDefinition.Role,
            QuartelDefinition.Domain,
            new Vector3(72f, 0f, 118f));
        ConfigurePlan();
        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeObject = marker != null ? marker.gameObject : layout.gameObject;
        Debug.Log("[IA02] Create de quartel criado: " + QuartelDefinition.SlotId + ". Mova o marcador para o local desejado e salve a cena.", layout);
    }

    private static Transform CreateAuxiliarySlot(Transform parent, string name, string slotId, IA02StrategicRole role, IA02BuildDomain domain, Vector3 localPosition)
    {
        Transform marker = FindSlotById(parent, slotId) ?? FindChildRecursive(parent, name);
        if (marker == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar local IA02");
            marker = go.transform;
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
        }

        IA02BuildSlot slot = marker.GetComponent<IA02BuildSlot>();
        if (slot == null) slot = Undo.AddComponent<IA02BuildSlot>(marker.gameObject);
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("slotId").stringValue = slotId;
        so.FindProperty("slotGroupId").stringValue = slotId.IndexOf("plataforma", StringComparison.OrdinalIgnoreCase) >= 0
            ? "plataformas_offshore"
            : "infraestrutura_estrategica";
        so.FindProperty("allowedRole").enumValueIndex = (int)role;
        so.FindProperty("allowedDomain").enumValueIndex = (int)domain;
        so.FindProperty("exactPosition").boolValue = true;
        so.FindProperty("allowAlternativeSlot").boolValue = false;
        so.FindProperty("buildingPoint").objectReferenceValue = marker;
        so.FindProperty("reservedFootprint").vector2Value = role == IA02StrategicRole.Storage ? new Vector2(34f, 28f) : new Vector2(75f, 55f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(slot);
        return marker;
    }

    private static void NormalizeAuxiliarySlot(Transform parent, string name, string slotId, IA02StrategicRole role, IA02BuildDomain domain, Vector3 localPosition)
    {
        LocalDefinition definition = new LocalDefinition(slotId, name, string.Empty, role, domain, string.Empty);
        Transform marker = EnsureCanonicalSlot(parent, definition, localPosition);
        ConfigureSlot(marker, definition);
        IA02BuildSlot slot = marker != null ? marker.GetComponent<IA02BuildSlot>() : null;
        if (slot != null)
        {
            SerializedObject so = new SerializedObject(slot);
            so.FindProperty("slotGroupId").stringValue = slotId.IndexOf("plataforma", StringComparison.OrdinalIgnoreCase) >= 0
                ? "plataformas_offshore"
                : "infraestrutura_estrategica";
            so.FindProperty("allowAlternativeSlot").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slot);
        }
    }

    private static Transform EnsureCanonicalSlot(Transform parent, LocalDefinition definition, Vector3 localPosition)
    {
        IA02BuildSlot[] slots = parent.GetComponentsInChildren<IA02BuildSlot>(true);
        List<Transform> matches = new List<Transform>();
        Transform canonical = null;
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null || !MatchesDefinition(slot.transform, definition)) continue;
            matches.Add(slot.transform);
            if (canonical == null && string.Equals(slot.transform.name, definition.Name, StringComparison.OrdinalIgnoreCase))
            {
                canonical = slot.transform;
            }
        }

        if (canonical == null && matches.Count > 0) canonical = matches[0];
        if (canonical == null)
        {
            GameObject go = new GameObject(definition.Name);
            Undo.RegisterCreatedObjectUndo(go, "Criar create IA02");
            canonical = go.transform;
            canonical.SetParent(parent, false);
            canonical.localPosition = localPosition;
            canonical.localRotation = Quaternion.identity;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            Transform duplicate = matches[i];
            if (duplicate == null || duplicate == canonical) continue;
            // Marcadores duplicados sao somente pontos de layout; remover o
            // objeto inteiro evita que o registry mantenha dois slots com o
            // mesmo ID e escolha um ponto imprevisivel.
            Undo.DestroyObjectImmediate(duplicate.gameObject);
        }

        Undo.RecordObject(canonical.gameObject, "Renomear create IA02");
        canonical.name = definition.Name;
        return canonical;
    }

    private static bool MatchesDefinition(Transform marker, LocalDefinition definition)
    {
        if (marker == null || definition == null) return false;
        IA02BuildSlot slot = marker.GetComponent<IA02BuildSlot>();
        if (slot != null && !string.IsNullOrWhiteSpace(definition.SlotId)
            && string.Equals(slot.SlotId, definition.SlotId, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(marker.name, definition.Name, StringComparison.OrdinalIgnoreCase)) return true;
        return marker.name.StartsWith("IA02 Local - " + definition.Name.Replace("IA02 Create - ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindSlotById(Transform parent, string slotId)
    {
        if (parent == null || string.IsNullOrWhiteSpace(slotId)) return null;
        IA02BuildSlot[] slots = parent.GetComponentsInChildren<IA02BuildSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && string.Equals(slots[i].SlotId, slotId, StringComparison.OrdinalIgnoreCase)) return slots[i].transform;
        }
        return null;
    }

    private static void ConfigureNavalAuxiliary(Transform marker)
    {
        if (marker == null) return;
        IA02BuildSlot slot = marker.GetComponent<IA02BuildSlot>();
        Transform spawn = EnsureChild(marker, "Spawn_Unidades", new Vector3(0f, 0f, 36f));
        Transform exit = EnsureChild(marker, "Direcao_Saida", new Vector3(0f, 0f, 120f));
        ConfigureNaval(marker, slot, spawn, exit);
    }

    private static void CreatePatrolZone(Transform parent, string name, Vector3 localPosition)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar área de patrulha naval IA02");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        if (zone.GetComponent<IA02NavalPatrolZone>() == null)
            Undo.AddComponent<IA02NavalPatrolZone>(zone.gameObject);
    }

    private static void CreateAirPatrolZone(Transform parent, string name, Vector3 localPosition)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar área de patrulha aérea IA02");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        if (zone.GetComponent<IA02AirPatrolZone>() == null)
            Undo.AddComponent<IA02AirPatrolZone>(zone.gameObject);
    }

    private static void CreateWarAdvanceZone(Transform parent, string name, Vector3 localPosition, IA02WarAdvanceZone.Dominio dominio)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar zona de avancao de guerra IA02");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        IA02WarAdvanceZone component = zone.GetComponent<IA02WarAdvanceZone>();
        if (component == null) component = Undo.AddComponent<IA02WarAdvanceZone>(zone.gameObject);
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("teamId").intValue = IA02TeamId;
        so.FindProperty("dominio").enumValueIndex = (int)dominio;
        so.FindProperty("raio").floatValue = dominio == IA02WarAdvanceZone.Dominio.Aereo ? 260f : 180f;
        so.FindProperty("pontos").intValue = 4;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }

    private static void CreateExtractionZone(Transform parent, string name, Vector3 localPosition)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar zona de extracao IA02");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        IA02ExtractionZone component = zone.GetComponent<IA02ExtractionZone>();
        if (component == null) component = Undo.AddComponent<IA02ExtractionZone>(zone.gameObject);
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("teamId").intValue = IA02TeamId;
        so.FindProperty("raio").floatValue = 80f;
        so.FindProperty("vagas").intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (string.Equals(descendants[i].name, name, StringComparison.OrdinalIgnoreCase)) return descendants[i];
        return null;
    }

    private static List<Transform> FindLocals(Transform root)
    {
        List<Transform> result = new List<Transform>();
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == root) continue;
            IA02BuildSlot slot = child.GetComponent<IA02BuildSlot>();
            if (child.name.StartsWith("IA02 Local", StringComparison.OrdinalIgnoreCase)
                || child.name.StartsWith("IA02 Create", StringComparison.OrdinalIgnoreCase)
                || (slot != null && IsInfrastructureLocal(child.name)))
            {
                result.Add(child);
            }
        }
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    private static bool IsInfrastructureLocal(string name)
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            if (string.Equals(name, Definitions[i].Name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IA02CityLayout FindLayoutWithLocals(out List<Transform> locals)
    {
        IA02CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA02CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        IA02CityLayout fallback = null;
        locals = new List<Transform>();
        for (int i = 0; i < layouts.Length; i++)
        {
            IA02CityLayout candidate = layouts[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            List<Transform> found = FindLocals(candidate.transform);
            if (fallback == null)
            {
                fallback = candidate;
                locals = found;
            }
            if (found.Count >= Definitions.Length)
            {
                locals = found;
                return candidate;
            }
        }
        return fallback;
    }

    private static void ConfigureSlot(Transform marker, LocalDefinition definition)
    {
        if (marker == null || definition == null) return;

        // Alguns marcadores antigos tinham um componente especializado de
        // outro tipo (ou um MonoBehaviour sem script). Isso passa no Inspector,
        // mas pode invalidar a serializacao da cena no player. Cada create deve
        // carregar somente o componente especializado que corresponde ao seu
        // dominio.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(marker.gameObject);
        IA02AirportBuildSlot[] aeroportosAntigos = marker.GetComponents<IA02AirportBuildSlot>();
        for (int i = 0; i < aeroportosAntigos.Length; i++)
        {
            if (aeroportosAntigos[i] != null) Undo.DestroyObjectImmediate(aeroportosAntigos[i]);
        }

        IA02NavalBuildSlot[] navaisAntigos = marker.GetComponents<IA02NavalBuildSlot>();
        for (int i = 0; i < navaisAntigos.Length; i++)
        {
            if (navaisAntigos[i] != null) Undo.DestroyObjectImmediate(navaisAntigos[i]);
        }

        Undo.RecordObject(marker.gameObject, "Nomear local IA02");
        marker.name = definition.Name;
        IA02BuildSlot slot = marker.GetComponent<IA02BuildSlot>();
        if (slot == null) slot = Undo.AddComponent<IA02BuildSlot>(marker.gameObject);

        Transform spawn = EnsureChild(marker, "Spawn_Unidades", new Vector3(0f, 0f, 16f));
        Transform exit = EnsureChild(marker, "Direcao_Saida", new Vector3(0f, 0f, 42f));
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("slotId").stringValue = definition.SlotId;
        so.FindProperty("slotGroupId").stringValue = "abertura_inicial";
        so.FindProperty("allowedRole").enumValueIndex = (int)definition.Role;
        so.FindProperty("allowedDomain").enumValueIndex = (int)definition.Domain;
        so.FindProperty("required").boolValue = false;
        so.FindProperty("exactPosition").boolValue = true;
        so.FindProperty("allowAlternativeSlot").boolValue = false;
        so.FindProperty("buildingPoint").objectReferenceValue = marker;
        so.FindProperty("unitSpawnPoint").objectReferenceValue = spawn;
        so.FindProperty("exitDirection").objectReferenceValue = exit;
        so.FindProperty("reservedFootprint").vector2Value = definition.Airport ? new Vector2(110f, 60f) : definition.Naval ? new Vector2(70f, 48f) : new Vector2(28f, 28f);
        so.FindProperty("safetyMargin").floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (definition.Airport) ConfigureAirport(marker, slot, spawn, exit);
        if (definition.Naval) ConfigureNaval(marker, slot, spawn, exit);
        EditorUtility.SetDirty(slot);
    }

    private static void NormalizeLegacySlotIds(Transform layoutRoot)
    {
        if (layoutRoot == null) return;

        IA02BuildSlot[] slots = layoutRoot.GetComponentsInChildren<IA02BuildSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null) continue;

            string oldId = slot.SlotId;
            string newId = oldId.Trim() switch
            {
                "prefeitura_01" => "ia02.local.prefeitura_01",
                "energia_01" => "ia02.local.energia_01",
                "fazenda_01" => "ia02.local.fazenda_01",
                "casa_01" => "ia02.local.casa_01",
                "armazem_01" => "ia02.local.armazem_01",
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(newId)) continue;

            bool alreadyUsed = false;
            for (int j = 0; j < slots.Length; j++)
            {
                if (slots[j] == null || slots[j] == slot) continue;
                if (string.Equals(slots[j].SlotId, newId, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyUsed = true;
                    break;
                }
            }
            if (alreadyUsed)
            {
                Debug.LogError("[IA02] Não foi possível migrar o slot legado '" + oldId + "': o ID novo já está em uso.", slot);
                continue;
            }

            SerializedObject serialized = new SerializedObject(slot);
            serialized.FindProperty("slotId").stringValue = newId;
            serialized.FindProperty("ownerTeamId").intValue = IA02TeamId;
            serialized.FindProperty("ownerNationId").intValue = IA02TeamId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RecordObject(slot.gameObject, "Isolar ID de slot IA02");
            slot.gameObject.name = newId == "ia02.local.prefeitura_01" ? "IA02 Create - Prefeitura"
                : newId == "ia02.local.energia_01" ? "IA02 Create - Energia"
                : newId == "ia02.local.fazenda_01" ? "IA02 Create - Fazenda"
                : newId == "ia02.local.casa_01" ? "IA02 Create - Casa Base"
                : "IA02 Create - Armazem 01";
            EditorUtility.SetDirty(slot);
            Debug.Log("[IA02] Slot legado migrado: " + oldId + " -> " + newId, slot);
        }
    }

    private static void ConfigureAirport(Transform marker, IA02BuildSlot slot, Transform spawn, Transform exit)
    {
        IA02AirportBuildSlot airport = marker.GetComponent<IA02AirportBuildSlot>();
        if (airport == null) airport = Undo.AddComponent<IA02AirportBuildSlot>(marker.gameObject);
        Transform runwayStart = EnsureChild(marker, "Pista_Inicio", new Vector3(-50f, 0f, 0f));
        Transform runwayEnd = EnsureChild(marker, "Pista_Fim", new Vector3(50f, 0f, 0f));
        SerializedObject so = new SerializedObject(airport);
        so.FindProperty("buildSlot").objectReferenceValue = slot;
        so.FindProperty("runwayStart").objectReferenceValue = runwayStart;
        so.FindProperty("runwayEnd").objectReferenceValue = runwayEnd;
        so.FindProperty("aircraftSpawn").objectReferenceValue = spawn;
        so.FindProperty("approachDirection").objectReferenceValue = exit;
        so.FindProperty("minimumRunwayLength").floatValue = 70f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(airport);
    }

    private static void ConfigureNaval(Transform marker, IA02BuildSlot slot, Transform spawn, Transform exit)
    {
        IA02NavalBuildSlot naval = marker.GetComponent<IA02NavalBuildSlot>();
        if (naval == null) naval = Undo.AddComponent<IA02NavalBuildSlot>(marker.gameObject);
        spawn.localPosition = new Vector3(0f, 0f, 36f);
        exit.localPosition = new Vector3(0f, 0f, 120f);
        SerializedObject so = new SerializedObject(naval);
        so.FindProperty("buildSlot").objectReferenceValue = slot;
        so.FindProperty("navalSpawnPoint").objectReferenceValue = spawn;
        so.FindProperty("exitDirection").objectReferenceValue = exit;
        so.FindProperty("minimumWaterDepth").floatValue = 4f;
        so.FindProperty("minimumExitWidth").floatValue = 18f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(naval);
    }

    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform found = parent.Find(name);
        if (found != null) return found;
        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Criar marcador auxiliar IA02");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        return child.transform;
    }

    private static void ConfigurePlan()
    {
        IA02BuildPlan plan = AssetDatabase.LoadAssetAtPath<IA02BuildPlan>(PlanPath);
        if (plan == null)
        {
            plan = AssetDatabase.LoadAssetAtPath<IA02BuildPlan>(LegacyGeneratedPlanPath);
        }
        if (plan == null)
        {
            Debug.LogError("[IA02] Plano de construcao nao encontrado: " + PlanPath);
            return;
        }

        IA02Controller controller = UnityEngine.Object.FindFirstObjectByType<IA02Controller>();
        if (controller != null)
        {
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("fighterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Aeroporto/Su11/Su11.prefab");
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EnsureStep(plan, Definitions[0], IA02StrategicRole.MilitaryProduction);
        EnsureStep(plan, Definitions[1], IA02StrategicRole.Residential);
        EnsureStep(plan, Definitions[2], IA02StrategicRole.Residential);
        EnsureStep(plan, Definitions[3], IA02StrategicRole.Residential);
        EnsureStep(plan, Definitions[4], IA02StrategicRole.MilitaryProduction);
        EnsureStep(plan, Definitions[5], IA02StrategicRole.Airfield);
        EnsureStep(plan, Definitions[6], IA02StrategicRole.Airfield);
        EnsureStep(plan, Definitions[7], IA02StrategicRole.Shipyard);
        EnsureStep(plan, QuartelDefinition, IA02StrategicRole.MilitaryProduction);
        EnsureStep(plan, PierDefinition, IA02StrategicRole.Pier);
        EnsureStep(plan, PlatformDefinition, IA02StrategicRole.NavalBase);

        SerializedObject so = new SerializedObject(plan);
        SerializedProperty steps = so.FindProperty("steps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            if (step.FindPropertyRelative("stepId").stringValue == "capital.prefeitura")
            {
                step.FindPropertyRelative("primarySlotId").stringValue = "ia02.local.prefeitura_01";
                step.FindPropertyRelative("slotGroupId").stringValue = "capital";
            }
            else if (step.FindPropertyRelative("stepId").stringValue == "naval.naval.estaleiro")
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(Definitions[7].AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA02StrategicRole.Shipyard;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA02PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = Definitions[7].SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "abertura_inicial";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA02FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == PierDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(PierDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA02StrategicRole.Pier;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA02PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = PierDefinition.SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "infraestrutura_estrategica";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA02FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == PlatformDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(PlatformDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA02StrategicRole.NavalBase;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA02PlacementMode.SlotGroup;
                step.FindPropertyRelative("primarySlotId").stringValue = string.Empty;
                step.FindPropertyRelative("slotGroupId").stringValue = "plataformas_offshore";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA02FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == QuartelDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(QuartelDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA02StrategicRole.MilitaryProduction;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA02PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = QuartelDefinition.SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "infraestrutura_estrategica";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA02FailurePolicy.Wait;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(plan);
    }

    private static void EnsureStep(IA02BuildPlan plan, LocalDefinition definition, IA02StrategicRole role)
    {
        SerializedObject so = new SerializedObject(plan);
        SerializedProperty steps = so.FindProperty("steps");
        SerializedProperty step = null;
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty candidate = steps.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("stepId").stringValue == definition.StepId)
            {
                step = candidate;
                break;
            }
        }
        if (step == null)
        {
            steps.InsertArrayElementAtIndex(steps.arraySize);
            step = steps.GetArrayElementAtIndex(steps.arraySize - 1);
        }

        step.FindPropertyRelative("stepId").stringValue = definition.StepId;
        step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(definition.AssetPath);
        step.FindPropertyRelative("requiredRole").enumValueIndex = (int)role;
        bool groupedPlatform = definition == PlatformDefinition;
        step.FindPropertyRelative("placementMode").enumValueIndex = (int)(groupedPlatform ? IA02PlacementMode.SlotGroup : IA02PlacementMode.ExactSlot);
        step.FindPropertyRelative("primarySlotId").stringValue = definition.SlotId;
        step.FindPropertyRelative("slotGroupId").stringValue = groupedPlatform ? "plataformas_offshore" : "abertura_inicial";
        step.FindPropertyRelative("autonomousZoneId").stringValue = string.Empty;
        step.FindPropertyRelative("required").boolValue = false;
        step.FindPropertyRelative("minimumStage").intValue = 0;
        step.FindPropertyRelative("maximumCount").intValue = 1;
        step.FindPropertyRelative("cooldownAfterCompletion").floatValue = 0f;
        SerializedProperty condition = step.FindPropertyRelative("condition");
        condition.FindPropertyRelative("type").enumValueIndex = (int)IA02BuildConditionType.Always;
        condition.FindPropertyRelative("target").floatValue = 1f;
        condition.FindPropertyRelative("role").enumValueIndex = (int)IA02StrategicRole.None;
        step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA02FailurePolicy.Wait;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
