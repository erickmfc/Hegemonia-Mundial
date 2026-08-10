#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hegemonia.AI.IA01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converte os marcadores temporarios "Local" do IA01CityLayout em slots nomeados
/// da abertura. Os marcadores continuam editaveis na cena: basta move-los depois
/// para reposicionar a infraestrutura sem alterar o roteiro da IA.
/// </summary>
public static class IA01LocalInfrastructureSetup
{
    private const string PlanPath = "Assets/Prefabs/IA01Controller_IA01Build.asset";
    private const string LegacyGeneratedPlanPath = "Assets/IA01/BuildPlans/IA01Controller_IA01BuildPlan.asset";

    private sealed class LocalDefinition
    {
        public string SlotId;
        public string Name;
        public string AssetPath;
        public IA01StrategicRole Role;
        public IA01BuildDomain Domain;
        public bool Airport;
        public bool Naval;
        public string StepId;

        public LocalDefinition(string slotId, string name, string assetPath, IA01StrategicRole role, IA01BuildDomain domain, string stepId, bool airport = false, bool naval = false)
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
        new LocalDefinition("ia01.local.tenda", "IA01 Create - Tenda Militar", "Assets/Prefabs/Construtor de Veiculos/Tenda/Construcao_Tenda.asset", IA01StrategicRole.MilitaryProduction, IA01BuildDomain.Land, "abertura.militar.tenda"),
        new LocalDefinition("ia01.local.casa", "IA01 Create - Casa", "Assets/Prefabs/Imobiliario/casa/Casa.asset", IA01StrategicRole.Residential, IA01BuildDomain.Land, "abertura.residencial.casa"),
        new LocalDefinition("ia01.local.apartamento_medio", "IA01 Create - Apartamento Medio", "Assets/Prefabs/Imobiliario/Pred Medio/Predio Medio.asset", IA01StrategicRole.Residential, IA01BuildDomain.Land, "abertura.residencial.apartamento_medio"),
        new LocalDefinition("ia01.local.apartamento_alto", "IA01 Create - Apartamento Alto", "Assets/Prefabs/Imobiliario/Perd Hard/Pred Hard.asset", IA01StrategicRole.Residential, IA01BuildDomain.Land, "abertura.residencial.apartamento_alto"),
        new LocalDefinition("ia01.local.construtor_veiculos", "IA01 Create - Construtor de Veiculos", "Assets/Prefabs/Construtor de Veiculos/Construtor de Veiculos.asset", IA01StrategicRole.MilitaryProduction, IA01BuildDomain.Land, "abertura.militar.construtor_veiculos"),
        new LocalDefinition("ia01.local.aeroporto_militar", "IA01 Create - Aeroporto Militar", "Assets/Prefabs/Aeroporto/Aeroporto militar.asset", IA01StrategicRole.Airfield, IA01BuildDomain.Airfield, "abertura.aereo.aeroporto_militar", true),
        new LocalDefinition("ia01.local.aeroporto_comercial", "IA01 Create - Aeroporto Comercial", "Assets/Prefabs/Aeroporto/Aeroporto comercial/Aeroporto comercial.asset", IA01StrategicRole.Airfield, IA01BuildDomain.Airfield, "abertura.aereo.aeroporto_comercial", true),
        new LocalDefinition("ia01.local.estaleiro", "IA01 Create - Estaleiro Naval", "Assets/Prefabs/Estaleiro Marinho/Estaleiro_Naval.asset", IA01StrategicRole.Shipyard, IA01BuildDomain.Coastal, "naval.naval.estaleiro", false, true)
    };

    private static readonly LocalDefinition PierDefinition =
        new LocalDefinition("ia01.local.pier", "IA01 Create - Pier Naval", "Assets/Prefabs/Marinha/Pier_marinha.asset", IA01StrategicRole.Pier, IA01BuildDomain.Coastal, "naval.pier");

    private static readonly LocalDefinition PlatformDefinition =
        new LocalDefinition(string.Empty, "IA01 Create - Plataforma Offshore", "Assets/Prefabs/Marinha/PLataforma.asset", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, "naval.plataforma");

    // Create opcional para o quartel. Usa a ficha/prefab da tenda militar,
    // que ja possui Fabrica.ehQuartel, mas com um slot proprio editavel.
    private static readonly LocalDefinition QuartelDefinition =
        new LocalDefinition("ia01.local.quartel", "IA01 Create - Quartel Militar", "Assets/Prefabs/Construtor de Veiculos/Tenda/Construcao_Tenda.asset", IA01StrategicRole.MilitaryProduction, IA01BuildDomain.Land, "abertura.militar.quartel");

    [MenuItem("Tools/IA01/Configurar Locais para Infraestrutura Inicial")]
    private static void Configure()
    {
        if (!NormalizeScene(SceneManager.GetActiveScene()))
        {
            EditorUtility.DisplayDialog("IA01", "Nenhum IA01CityLayout foi encontrado na cena aberta.", "OK");
        }
    }

    [MenuItem("Tools/IA01/Normalizar e reparar creates da IA01")]
    private static void NormalizeActiveScene()
    {
        if (!NormalizeScene(SceneManager.GetActiveScene()))
        {
            EditorUtility.DisplayDialog("IA01", "Nenhum IA01CityLayout foi encontrado na cena aberta.", "OK");
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
            Debug.LogError("[IA01] IA01CityLayout ausente em " + scenePath);
            return;
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA01] Creates da campanha normalizados e cena salva: " + scenePath);
    }

    private static bool NormalizeScene(Scene scene)
    {
        if (!scene.IsValid()) return false;
        IA01CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null) return false;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Normalizar creates da infraestrutura IA01");
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

        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Armazenamento 01", "ia01.local.armazenamento.01", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(-48f, 0f, 52f));
        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Armazenamento 02", "ia01.local.armazenamento.02", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(0f, 0f, 66f));
        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Armazenamento 03", "ia01.local.armazenamento.03", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(48f, 0f, 52f));
        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Plataforma Offshore A", "ia01.local.plataforma.a", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(120f, 0f, 200f));
        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Plataforma Offshore B", "ia01.local.plataforma.b", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(-180f, 0f, 260f));
        NormalizeAuxiliarySlot(layout.transform, "IA01 Create - Plataforma Offshore C", "ia01.local.plataforma.c", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(300f, 0f, 320f));

        ConfigurePlan();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA01] Creates unicos configurados por slotId, com duplicatas removidas.", layout);
        return true;
    }

    [MenuItem("Tools/IA01/Criar locais navais e de logistica")]
    private static void CreateNavalAndLogisticsLocals()
    {
        IA01CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null)
        {
            EditorUtility.DisplayDialog("IA01", "Nenhum IA01CityLayout foi encontrado na cena aberta.", "OK");
            return;
        }

        CreateAuxiliarySlot(layout.transform, "IA01 Local - Armazenamento 01", "ia01.local.armazenamento.01", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(-48f, 0f, 52f));
        CreateAuxiliarySlot(layout.transform, "IA01 Local - Armazenamento 02", "ia01.local.armazenamento.02", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(0f, 0f, 66f));
        CreateAuxiliarySlot(layout.transform, "IA01 Local - Armazenamento 03", "ia01.local.armazenamento.03", IA01StrategicRole.Storage, IA01BuildDomain.Land, new Vector3(48f, 0f, 52f));
        CreateAuxiliarySlot(layout.transform, QuartelDefinition.Name, QuartelDefinition.SlotId, QuartelDefinition.Role, QuartelDefinition.Domain, new Vector3(72f, 0f, 118f));
        Transform pier = CreateAuxiliarySlot(layout.transform, "IA01 Local - Pier Naval", "ia01.local.pier", IA01StrategicRole.Pier, IA01BuildDomain.Coastal, new Vector3(-90f, 0f, 135f));
        ConfigureNavalAuxiliary(pier);
        CreateAuxiliarySlot(layout.transform, "IA01 Local - Plataforma de Petróleo A", "ia01.local.plataforma.a", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(120f, 0f, 200f));
        CreateAuxiliarySlot(layout.transform, "IA01 Local - Plataforma de Petróleo B", "ia01.local.plataforma.b", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(-180f, 0f, 260f));
        CreateAuxiliarySlot(layout.transform, "IA01 Local - Plataforma de Petróleo C", "ia01.local.plataforma.c", IA01StrategicRole.NavalBase, IA01BuildDomain.Coastal, new Vector3(300f, 0f, 320f));

        ConfigurePlan();

        Transform shipyard = FindSlotById(layout.transform, "ia01.local.estaleiro");
        if (shipyard != null)
        {
            CreatePatrolZone(shipyard, "IA01 Patrulha Naval - Área A", new Vector3(120f, 0f, 160f));
            CreatePatrolZone(shipyard, "IA01 Patrulha Naval - Área B", new Vector3(-140f, 0f, 220f));
            CreatePatrolZone(shipyard, "IA01 Patrulha Naval - Área C", new Vector3(250f, 0f, 300f));
            CreateWarAdvanceZone(shipyard, "IA01 WarAdvanceZone Naval A", new Vector3(180f, 0f, 220f), IA01WarAdvanceZone.Dominio.Naval);
            CreateWarAdvanceZone(shipyard, "IA01 WarAdvanceZone Naval B", new Vector3(-220f, 0f, 300f), IA01WarAdvanceZone.Dominio.Naval);
            CreateExtractionZone(shipyard, "IA01 ExtractionZone Naval", new Vector3(-40f, 0f, 150f));
        }

        Transform airport = FindSlotById(layout.transform, "ia01.local.aeroporto_militar");
        if (airport != null)
        {
            CreateAirPatrolZone(airport, "IA01 Patrulha Aerea - Área Inicial", new Vector3(0f, 0f, 280f));
            CreateWarAdvanceZone(airport, "IA01 WarAdvanceZone Aerea", new Vector3(0f, 100f, 320f), IA01WarAdvanceZone.Dominio.Aereo);
        }

        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        Debug.Log("[IA01] Criados os 3 locais de armazém, pier, 3 locais de plataforma e 3 áreas de patrulha naval. Posicione os marcadores sobre a água antes de salvar.", layout);
    }

    [MenuItem("Tools/IA01/Criar local de quartel militar")]
    private static void CreateQuartelLocal()
    {
        IA01CityLayout layout = FindLayoutWithLocals(out _);
        if (layout == null)
        {
            EditorUtility.DisplayDialog("IA01", "Nenhum IA01CityLayout foi encontrado na cena aberta.", "OK");
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
        Debug.Log("[IA01] Create de quartel criado: " + QuartelDefinition.SlotId + ". Mova o marcador para o local desejado e salve a cena.", layout);
    }

    private static Transform CreateAuxiliarySlot(Transform parent, string name, string slotId, IA01StrategicRole role, IA01BuildDomain domain, Vector3 localPosition)
    {
        Transform marker = FindSlotById(parent, slotId) ?? FindChildRecursive(parent, name);
        if (marker == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar local IA01");
            marker = go.transform;
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
        }

        IA01BuildSlot slot = marker.GetComponent<IA01BuildSlot>();
        if (slot == null) slot = Undo.AddComponent<IA01BuildSlot>(marker.gameObject);
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
        so.FindProperty("reservedFootprint").vector2Value = role == IA01StrategicRole.Storage ? new Vector2(34f, 28f) : new Vector2(75f, 55f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(slot);
        return marker;
    }

    private static void NormalizeAuxiliarySlot(Transform parent, string name, string slotId, IA01StrategicRole role, IA01BuildDomain domain, Vector3 localPosition)
    {
        LocalDefinition definition = new LocalDefinition(slotId, name, string.Empty, role, domain, string.Empty);
        Transform marker = EnsureCanonicalSlot(parent, definition, localPosition);
        ConfigureSlot(marker, definition);
        IA01BuildSlot slot = marker != null ? marker.GetComponent<IA01BuildSlot>() : null;
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
        IA01BuildSlot[] slots = parent.GetComponentsInChildren<IA01BuildSlot>(true);
        List<Transform> matches = new List<Transform>();
        Transform canonical = null;
        for (int i = 0; i < slots.Length; i++)
        {
            IA01BuildSlot slot = slots[i];
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
            Undo.RegisterCreatedObjectUndo(go, "Criar create IA01");
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

        Undo.RecordObject(canonical.gameObject, "Renomear create IA01");
        canonical.name = definition.Name;
        return canonical;
    }

    private static bool MatchesDefinition(Transform marker, LocalDefinition definition)
    {
        if (marker == null || definition == null) return false;
        IA01BuildSlot slot = marker.GetComponent<IA01BuildSlot>();
        if (slot != null && !string.IsNullOrWhiteSpace(definition.SlotId)
            && string.Equals(slot.SlotId, definition.SlotId, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(marker.name, definition.Name, StringComparison.OrdinalIgnoreCase)) return true;
        return marker.name.StartsWith("IA01 Local - " + definition.Name.Replace("IA01 Create - ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindSlotById(Transform parent, string slotId)
    {
        if (parent == null || string.IsNullOrWhiteSpace(slotId)) return null;
        IA01BuildSlot[] slots = parent.GetComponentsInChildren<IA01BuildSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && string.Equals(slots[i].SlotId, slotId, StringComparison.OrdinalIgnoreCase)) return slots[i].transform;
        }
        return null;
    }

    private static void ConfigureNavalAuxiliary(Transform marker)
    {
        if (marker == null) return;
        IA01BuildSlot slot = marker.GetComponent<IA01BuildSlot>();
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
            Undo.RegisterCreatedObjectUndo(go, "Criar área de patrulha naval IA01");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        if (zone.GetComponent<IA01NavalPatrolZone>() == null)
            Undo.AddComponent<IA01NavalPatrolZone>(zone.gameObject);
    }

    private static void CreateAirPatrolZone(Transform parent, string name, Vector3 localPosition)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar área de patrulha aérea IA01");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        if (zone.GetComponent<IA01AirPatrolZone>() == null)
            Undo.AddComponent<IA01AirPatrolZone>(zone.gameObject);
    }

    private static void CreateWarAdvanceZone(Transform parent, string name, Vector3 localPosition, IA01WarAdvanceZone.Dominio dominio)
    {
        Transform zone = parent.Find(name);
        if (zone == null)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar zona de avancao de guerra IA01");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        IA01WarAdvanceZone component = zone.GetComponent<IA01WarAdvanceZone>();
        if (component == null) component = Undo.AddComponent<IA01WarAdvanceZone>(zone.gameObject);
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("teamId").intValue = 2;
        so.FindProperty("dominio").enumValueIndex = (int)dominio;
        so.FindProperty("raio").floatValue = dominio == IA01WarAdvanceZone.Dominio.Aereo ? 260f : 180f;
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
            Undo.RegisterCreatedObjectUndo(go, "Criar zona de extracao IA01");
            zone = go.transform;
            zone.SetParent(parent, false);
            zone.localPosition = localPosition;
        }
        IA01ExtractionZone component = zone.GetComponent<IA01ExtractionZone>();
        if (component == null) component = Undo.AddComponent<IA01ExtractionZone>(zone.gameObject);
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("teamId").intValue = 2;
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
            IA01BuildSlot slot = child.GetComponent<IA01BuildSlot>();
            if (child.name.StartsWith("IA01 Local", StringComparison.OrdinalIgnoreCase)
                || child.name.StartsWith("IA01 Create", StringComparison.OrdinalIgnoreCase)
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

    private static IA01CityLayout FindLayoutWithLocals(out List<Transform> locals)
    {
        IA01CityLayout[] layouts = Resources.FindObjectsOfTypeAll<IA01CityLayout>();
        IA01CityLayout fallback = null;
        locals = new List<Transform>();
        for (int i = 0; i < layouts.Length; i++)
        {
            IA01CityLayout candidate = layouts[i];
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
        IA01AirportBuildSlot[] aeroportosAntigos = marker.GetComponents<IA01AirportBuildSlot>();
        for (int i = 0; i < aeroportosAntigos.Length; i++)
        {
            if (aeroportosAntigos[i] != null) Undo.DestroyObjectImmediate(aeroportosAntigos[i]);
        }

        IA01NavalBuildSlot[] navaisAntigos = marker.GetComponents<IA01NavalBuildSlot>();
        for (int i = 0; i < navaisAntigos.Length; i++)
        {
            if (navaisAntigos[i] != null) Undo.DestroyObjectImmediate(navaisAntigos[i]);
        }

        Undo.RecordObject(marker.gameObject, "Nomear local IA01");
        marker.name = definition.Name;
        IA01BuildSlot slot = marker.GetComponent<IA01BuildSlot>();
        if (slot == null) slot = Undo.AddComponent<IA01BuildSlot>(marker.gameObject);

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

    private static void ConfigureAirport(Transform marker, IA01BuildSlot slot, Transform spawn, Transform exit)
    {
        IA01AirportBuildSlot airport = marker.GetComponent<IA01AirportBuildSlot>();
        if (airport == null) airport = Undo.AddComponent<IA01AirportBuildSlot>(marker.gameObject);
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

    private static void ConfigureNaval(Transform marker, IA01BuildSlot slot, Transform spawn, Transform exit)
    {
        IA01NavalBuildSlot naval = marker.GetComponent<IA01NavalBuildSlot>();
        if (naval == null) naval = Undo.AddComponent<IA01NavalBuildSlot>(marker.gameObject);
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
        Undo.RegisterCreatedObjectUndo(child, "Criar marcador auxiliar IA01");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        return child.transform;
    }

    private static void ConfigurePlan()
    {
        IA01BuildPlan plan = AssetDatabase.LoadAssetAtPath<IA01BuildPlan>(PlanPath);
        if (plan == null)
        {
            plan = AssetDatabase.LoadAssetAtPath<IA01BuildPlan>(LegacyGeneratedPlanPath);
        }
        if (plan == null)
        {
            Debug.LogError("[IA01] Plano de construcao nao encontrado: " + PlanPath);
            return;
        }

        IA01Controller controller = UnityEngine.Object.FindFirstObjectByType<IA01Controller>();
        if (controller != null)
        {
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("fighterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Aeroporto/Su11/Su11.prefab");
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EnsureStep(plan, Definitions[0], IA01StrategicRole.MilitaryProduction);
        EnsureStep(plan, Definitions[1], IA01StrategicRole.Residential);
        EnsureStep(plan, Definitions[2], IA01StrategicRole.Residential);
        EnsureStep(plan, Definitions[3], IA01StrategicRole.Residential);
        EnsureStep(plan, Definitions[4], IA01StrategicRole.MilitaryProduction);
        EnsureStep(plan, Definitions[5], IA01StrategicRole.Airfield);
        EnsureStep(plan, Definitions[6], IA01StrategicRole.Airfield);
        EnsureStep(plan, Definitions[7], IA01StrategicRole.Shipyard);
        EnsureStep(plan, QuartelDefinition, IA01StrategicRole.MilitaryProduction);
        EnsureStep(plan, PierDefinition, IA01StrategicRole.Pier);
        EnsureStep(plan, PlatformDefinition, IA01StrategicRole.NavalBase);

        SerializedObject so = new SerializedObject(plan);
        SerializedProperty steps = so.FindProperty("steps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            if (step.FindPropertyRelative("stepId").stringValue == "naval.naval.estaleiro")
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(Definitions[7].AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA01StrategicRole.Shipyard;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA01PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = Definitions[7].SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "abertura_inicial";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA01FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == PierDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(PierDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA01StrategicRole.Pier;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA01PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = PierDefinition.SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "infraestrutura_estrategica";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA01FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == PlatformDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(PlatformDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA01StrategicRole.NavalBase;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA01PlacementMode.SlotGroup;
                step.FindPropertyRelative("primarySlotId").stringValue = string.Empty;
                step.FindPropertyRelative("slotGroupId").stringValue = "plataformas_offshore";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA01FailurePolicy.Wait;
            }
            else if (step.FindPropertyRelative("stepId").stringValue == QuartelDefinition.StepId)
            {
                step.FindPropertyRelative("constructionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(QuartelDefinition.AssetPath);
                step.FindPropertyRelative("requiredRole").enumValueIndex = (int)IA01StrategicRole.MilitaryProduction;
                step.FindPropertyRelative("placementMode").enumValueIndex = (int)IA01PlacementMode.ExactSlot;
                step.FindPropertyRelative("primarySlotId").stringValue = QuartelDefinition.SlotId;
                step.FindPropertyRelative("slotGroupId").stringValue = "infraestrutura_estrategica";
                step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA01FailurePolicy.Wait;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(plan);
    }

    private static void EnsureStep(IA01BuildPlan plan, LocalDefinition definition, IA01StrategicRole role)
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
        step.FindPropertyRelative("placementMode").enumValueIndex = (int)(groupedPlatform ? IA01PlacementMode.SlotGroup : IA01PlacementMode.ExactSlot);
        step.FindPropertyRelative("primarySlotId").stringValue = definition.SlotId;
        step.FindPropertyRelative("slotGroupId").stringValue = groupedPlatform ? "plataformas_offshore" : "abertura_inicial";
        step.FindPropertyRelative("autonomousZoneId").stringValue = string.Empty;
        step.FindPropertyRelative("required").boolValue = false;
        step.FindPropertyRelative("minimumStage").intValue = 0;
        step.FindPropertyRelative("maximumCount").intValue = 1;
        step.FindPropertyRelative("cooldownAfterCompletion").floatValue = 0f;
        SerializedProperty condition = step.FindPropertyRelative("condition");
        condition.FindPropertyRelative("type").enumValueIndex = (int)IA01BuildConditionType.Always;
        condition.FindPropertyRelative("target").floatValue = 1f;
        condition.FindPropertyRelative("role").enumValueIndex = (int)IA01StrategicRole.None;
        step.FindPropertyRelative("failurePolicy").enumValueIndex = (int)IA01FailurePolicy.Wait;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
