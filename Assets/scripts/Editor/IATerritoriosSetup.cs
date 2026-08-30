#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hegemonia.AI.IA01;
using Hegemonia.AI.IA02;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantém as duas IAs em territórios distintos e verificáveis.
/// IA01 pertence ao Pais2 e IA02 pertence ao pais3. O utilitário só move as
/// raízes das IAs e os marcadores de infraestrutura; não altera o território
/// do jogador nem desativa os terrenos existentes.
/// </summary>
public static class IATerritoriosSetup
{
    private const string DemoScenePath = "Assets/_Recovery/demo1.unity";
    private const string CampaignScenePath = "Assets/Scenes/cena19).unity";
    private const float LandMargin = 180f;

    [MenuItem("Hegemonia/Demo/Posicionar e validar IA01 e IA02 nos paises", priority = 14)]
    public static void PosicionarEValidar()
    {
        // A configuração anterior da IA02 já sabe localizar a costa real de
        // pais3. Execute-a primeiro para que demo e campanha fiquem iguais;
        // depois corrigimos a IA01 em cada cena.
        DemoMapIA02LayoutSetup.ConfigurarDemoEIA02();
        ProcessarCena(DemoScenePath);
        ProcessarCena(CampaignScenePath);
        AbrirCena(DemoScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA/Territorios] Configuração concluída nas duas cenas. IA01=Pais2, IA02=pais3; território do jogador preservado.");
    }

    [MenuItem("Hegemonia/Demo/Validar somente territórios das IAs", priority = 15)]
    public static void ValidarSomente()
    {
        ValidarCena(DemoScenePath);
        ValidarCena(CampaignScenePath);
        AbrirCena(DemoScenePath);
    }

    private static void ProcessarCena(string path)
    {
        Scene scene = AbrirCena(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[IA/Territorios] Não foi possível abrir " + path + ".");
            return;
        }

        Terrain pais2 = EncontrarTerrain("Pais2");
        Terrain pais3 = EncontrarTerrain("pais3");
        if (pais2 == null || pais2.terrainData == null)
        {
            Debug.LogError("[IA/Territorios] Pais2 não encontrado ou sem Terrain em " + path + ".");
        }
        else
        {
            PosicionarIA01(pais2);
        }

        if (pais3 == null || pais3.terrainData == null)
        {
            Debug.LogError("[IA/Territorios] pais3 não encontrado ou sem Terrain em " + path + ".");
        }
        else
        {
            PosicionarIA02(pais3);
        }

        ValidarCenaAberta(path, pais2, pais3);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ValidarCena(string path)
    {
        Scene scene = AbrirCena(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[IA/Territorios] Cena inválida durante validação: " + path);
            return;
        }

        ValidarCenaAberta(path, EncontrarTerrain("Pais2"), EncontrarTerrain("pais3"));
    }

    private static void ValidarCenaAberta(string path, Terrain pais2, Terrain pais3)
    {
        IA01Controller ia01 = EncontrarIA01();
        IA02Controller ia02 = EncontrarIA02();
        ValidarIA01(path, ia01, pais2);
        ValidarIA02(path, ia02, pais3);
    }

    private static void PosicionarIA01(Terrain pais2)
    {
        IA01Controller controller = EncontrarIA01();
        if (controller == null || controller.CityLayout == null)
        {
            Debug.LogError("[IA/Territorios] IA01Controller ou IA01CityLayout ausente na cena " + pais2.gameObject.scene.path + ".");
            return;
        }

        IA01CityLayout layout = controller.CityLayout;
        layout.EnsureRuntimeReady();
        Transform root = EncontrarRaizIA01(controller);
        IA01BuildSlot capital = layout.CapitalSlot;
        if (root == null || capital == null)
        {
            Debug.LogError("[IA/Territorios] IA01 sem raiz ou slot de Prefeitura.");
            return;
        }

        Bounds bounds = ObterBounds(pais2.gameObject);
        List<IA01BuildSlot> slots = new List<IA01BuildSlot>(layout.GetComponentsInChildren<IA01BuildSlot>(true));
        Vector3 anchor;
        if (!TryFindValidLandAnchor(pais2, bounds, root, capital, slots, out anchor))
        {
            anchor = new Vector3(bounds.center.x, SampleTerrainHeight(pais2, bounds.center), bounds.center.z);
            Debug.LogWarning("[IA/Territorios] Não encontrei amostra de terra inequívoca para IA01; usando o centro de Pais2 e mantendo a falha explícita na validação.");
        }

        Vector3 capitalOffset = capital.transform.position - root.position;
        root.position = anchor - capitalOffset;

        int landMoved = 0;
        int navalMoved = 0;
        Bounds waterBounds = ObterBoundsAgua();
        Vector3 waterAnchor;
        bool hasWaterAnchor = TryFindWaterAnchorNearCountry(bounds, waterBounds, out waterAnchor);
        int navalIndex = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            IA01BuildSlot slot = slots[i];
            if (slot == null) continue;

            bool naval = slot.AllowedDomain == IA01BuildDomain.Coastal || slot.AllowedDomain == IA01BuildDomain.Water;
            if (naval)
            {
                if (hasWaterAnchor)
                {
                    Vector3 offset = new Vector3((navalIndex % 3) * 220f, 0f, (navalIndex / 3) * 220f);
                    Vector3 desired = waterAnchor + offset;
                    if (!TryFindWaterNear(desired, waterBounds, out Vector3 waterPoint))
                        waterPoint = waterAnchor;
                    waterPoint.y = NavalPlacementResolver.ResolveSeaLevel();
                    slot.transform.position = waterPoint;
                    EditorUtility.SetDirty(slot);
                    navalMoved++;
                }
                navalIndex++;
                continue;
            }

            Vector3 position = slot.transform.position;
            position.y = SampleTerrainHeight(pais2, position);
            slot.transform.position = position;
            EditorUtility.SetDirty(slot);
            landMoved++;
        }

        layout.ConfigureOwner(controller.TeamId, controller.NationId);
        EditorUtility.SetDirty(layout);
        EditorUtility.SetDirty(root.gameObject);
        Debug.Log("[IA/Territorios] IA01 organizada em Pais2: capital=" + capital.transform.position.ToString("F1")
            + ", slots terrestres=" + landMoved + ", slots navais reposicionados=" + navalMoved
            + ", bounds=" + DescreverBounds(bounds));
    }

    private static void PosicionarIA02(Terrain pais3)
    {
        IA02Controller controller = EncontrarIA02();
        if (controller == null || controller.CityLayout == null)
        {
            Debug.LogError("[IA/Territorios] IA02Controller ou IA02CityLayout ausente na cena " + pais3.gameObject.scene.path + ".");
            return;
        }

        // O porto da IA02 fica na água adjacente ao país. Em mapas grandes,
        // a distância entre a Prefeitura e a costa pode passar do limite de
        // segurança usado pelo prefab. Mantemos a validação por envelope,
        // equipe e território, mas ampliamos somente o raio desta instância
        // para que o estaleiro costeiro oficial não seja recusado no runtime.
        SerializedObject controllerData = new SerializedObject(controller);
        SerializedProperty maxConstructionDistance = controllerData.FindProperty("maxConstructionDistanceFromController");
        if (maxConstructionDistance != null)
        {
            maxConstructionDistance.floatValue = 7000f;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        IA02CityLayout layout = controller.CityLayout;
        layout.EnsureRuntimeReady();
        Transform root = controller.transform.parent != null ? controller.transform.parent : controller.transform;
        IA02BuildSlot capital = layout.CapitalSlot;
        if (capital == null)
        {
            Debug.LogError("[IA/Territorios] IA02 sem slot de Prefeitura em " + pais3.gameObject.scene.path + ".");
            return;
        }

        Bounds bounds = ObterBounds(pais3.gameObject);
        List<IA02BuildSlot> slots = new List<IA02BuildSlot>(layout.GetComponentsInChildren<IA02BuildSlot>(true));
        Bounds waterBounds = ObterBoundsAgua();
        bool hasWaterAnchor = TryFindWaterAnchorNearCountry(bounds, waterBounds, out Vector3 waterAnchor);
        if (!TryFindValidLandAnchorIA02(pais3, bounds, root, capital, slots, hasWaterAnchor, waterAnchor, out Vector3 anchor))
        {
            Debug.LogError("[IA/Territorios] Não encontrei uma âncora terrestre válida para IA02 dentro de pais3 em " + pais3.gameObject.scene.path + ".");
            return;
        }

        Vector3 capitalOffset = capital.transform.position - root.position;
        root.SetPositionAndRotation(anchor - capitalOffset, Quaternion.identity);

        int landMoved = 0;
        int landCorrected = 0;
        int navalMoved = 0;
        int navalIndex = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null) continue;

            bool naval = slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water;
            if (naval)
            {
                if (hasWaterAnchor)
                {
                    Vector3 offset = new Vector3((navalIndex % 3) * 220f, 0f, (navalIndex / 3) * 220f);
                    Vector3 desired = waterAnchor + offset;
                    if (!TryFindWaterNear(desired, waterBounds, out Vector3 waterPoint))
                        waterPoint = waterAnchor;
                    waterPoint.y = NavalPlacementResolver.ResolveSeaLevel();
                    slot.transform.position = waterPoint;
                    EditorUtility.SetDirty(slot);
                    navalMoved++;
                }
                navalIndex++;
                continue;
            }

            Vector3 position = slot.transform.position;
            position.y = SampleTerrainHeight(pais3, position);
            if (!ContainsXZ(bounds, position, 1f)
                || NavalPlacementResolver.IsWaterAtPosition(position, NavalPlacementResolver.ResolveSeaLevel()))
            {
                if (TryFindLandPointNear(pais3, bounds, position, out Vector3 correctedPoint))
                {
                    position = correctedPoint;
                    landCorrected++;
                }
            }
            slot.transform.position = position;
            EditorUtility.SetDirty(slot);
            landMoved++;
        }

        layout.ConfigureOwner(controller.TeamId, controller.NationId);
        EditorUtility.SetDirty(layout);
        EditorUtility.SetDirty(root.gameObject);
        Debug.Log("[IA/Territorios] IA02 organizada em pais3: capital=" + capital.transform.position.ToString("F1")
            + ", slots terrestres=" + landMoved + ", terrestres corrigidos=" + landCorrected
            + ", slots navais reposicionados=" + navalMoved
            + ", bounds=" + DescreverBounds(bounds));
    }

    private static bool TryFindLandPointNear(Terrain terrain, Bounds bounds, Vector3 desired, out Vector3 point)
    {
        point = desired;
        float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
        const float step = 160f;
        const int maxRing = 12;
        float margin = Mathf.Max(60f, LandMargin * 0.25f);

        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int x = -ring; x <= ring; x++)
            {
                for (int z = -ring; z <= ring; z++)
                {
                    if (ring > 0 && Mathf.Abs(x) != ring && Mathf.Abs(z) != ring) continue;
                    Vector3 candidate = desired + new Vector3(x * step, 0f, z * step);
                    if (!ContainsXZ(bounds, candidate, margin)) continue;
                    candidate.y = SampleTerrainHeight(terrain, candidate);
                    if (NavalPlacementResolver.IsWaterAtPosition(candidate, seaLevel)) continue;
                    point = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindValidLandAnchorIA02(Terrain terrain, Bounds bounds, Transform root,
        IA02BuildSlot capital, List<IA02BuildSlot> slots, bool hasWaterAnchor, Vector3 waterAnchor, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
        float margin = Mathf.Clamp(LandMargin, 40f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.2f);
        float[] samples = { 0.15f, 0.3f, 0.5f, 0.7f, 0.85f };
        float bestScore = float.MinValue;
        bool found = false;

        for (int x = 0; x < samples.Length; x++)
        {
            for (int z = 0; z < samples.Length; z++)
            {
                Vector3 candidate = new Vector3(
                    Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, samples[x]),
                    0f,
                    Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, samples[z]));
                candidate.y = SampleTerrainHeight(terrain, candidate);
                if (NavalPlacementResolver.IsWaterAtPosition(candidate, seaLevel)) continue;

                int landInside = 0;
                int landSlotCount = 0;
                bool allLandValid = true;
                bool capitalInside = false;
                Vector3 capitalRelative = capital.transform.position - root.position;
                for (int i = 0; i < slots.Count; i++)
                {
                    IA02BuildSlot slot = slots[i];
                    if (slot == null || slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water) continue;
                    landSlotCount++;
                    Vector3 predicted = candidate + (slot.transform.position - root.position) - capitalRelative;
                    bool inside = ContainsXZ(bounds, predicted, margin * 0.25f);
                    bool onLand = !NavalPlacementResolver.IsWaterAtPosition(predicted, seaLevel);
                    if (inside && onLand) landInside++;
                    else allLandValid = false;
                    if (slot == capital) capitalInside = inside && onLand;
                }

                if (!capitalInside || !allLandValid || landInside != landSlotCount) continue;
                float centerBias = 1f - Vector2.Distance(
                    new Vector2(candidate.x, candidate.z), new Vector2(bounds.center.x, bounds.center.z))
                    / Mathf.Max(1f, new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
                // A infraestrutura naval precisa ficar próxima da raiz da IA
                // para passar também pela validação de território preparado em
                // runtime. Quando existe uma costa válida, prefira uma âncora
                // terrestre próxima dela sem sacrificar os slots da cidade.
                float score = landInside * 1000f + centerBias * 10f;
                if (hasWaterAnchor)
                {
                    float coastDistance = Vector2.Distance(
                        new Vector2(candidate.x, candidate.z),
                        new Vector2(waterAnchor.x, waterAnchor.z));
                    score -= coastDistance * 0.25f;
                }
                if (!found || score > bestScore)
                {
                    bestScore = score;
                    anchor = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private static bool TryFindValidLandAnchor(Terrain terrain, Bounds bounds, Transform root,
        IA01BuildSlot capital, List<IA01BuildSlot> slots, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
        float margin = Mathf.Clamp(LandMargin, 40f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.2f);
        float[] samples = { 0.15f, 0.3f, 0.5f, 0.7f, 0.85f };
        float bestScore = float.MinValue;
        bool found = false;

        for (int x = 0; x < samples.Length; x++)
        {
            for (int z = 0; z < samples.Length; z++)
            {
                Vector3 candidate = new Vector3(
                    Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, samples[x]),
                    0f,
                    Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, samples[z]));
                candidate.y = SampleTerrainHeight(terrain, candidate);

                bool water = NavalPlacementResolver.IsWaterAtPosition(candidate, seaLevel);
                if (water) continue;

                int landInside = 0;
                bool capitalInside = true;
                for (int i = 0; i < slots.Count; i++)
                {
                    IA01BuildSlot slot = slots[i];
                    if (slot == null || slot.AllowedDomain == IA01BuildDomain.Coastal || slot.AllowedDomain == IA01BuildDomain.Water) continue;
                    Vector3 predicted = candidate + (slot.transform.position - root.position) - (capital.transform.position - root.position);
                    if (ContainsXZ(bounds, predicted, margin * 0.25f)) landInside++;
                    if (slot == capital) capitalInside = ContainsXZ(bounds, predicted, margin * 0.25f);
                }

                if (!capitalInside) continue;
                float centerBias = 1f - Vector2.Distance(
                    new Vector2(candidate.x, candidate.z), new Vector2(bounds.center.x, bounds.center.z))
                    / Mathf.Max(1f, new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
                float score = landInside * 1000f + centerBias * 10f;
                if (!found || score > bestScore)
                {
                    bestScore = score;
                    anchor = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private static void ValidarIA01(string path, IA01Controller controller, Terrain pais2)
    {
        if (controller == null || controller.CityLayout == null || pais2 == null)
        {
            Debug.LogError("[IA/Territorios] FALHA " + path + ": IA01 ou Pais2 ausente.");
            return;
        }

        Bounds bounds = ObterBounds(pais2.gameObject);
        IA01BuildSlot[] slots = controller.CityLayout.GetComponentsInChildren<IA01BuildSlot>(true);
        int ok = 0;
        List<string> errors = new List<string>();
        for (int i = 0; i < slots.Length; i++)
        {
            IA01BuildSlot slot = slots[i];
            if (slot == null) continue;
            bool naval = slot.AllowedDomain == IA01BuildDomain.Coastal || slot.AllowedDomain == IA01BuildDomain.Water;
            bool valid = naval ? NavalPlacementResolver.IsWaterAtPosition(slot.transform.position)
                : ContainsXZ(bounds, slot.transform.position, 1f) && !NavalPlacementResolver.IsWaterAtPosition(slot.transform.position);
            if (valid) ok++;
            else errors.Add(slot.SlotId + "=" + slot.transform.position.ToString("F1") + (naval ? " [naval]" : " [terra]"));
        }

        string capital = controller.CityLayout.CapitalSlot != null
            ? controller.CityLayout.CapitalSlot.transform.position.ToString("F1") : "ausente";
        if (errors.Count == 0)
            Debug.Log("[IA/Territorios] OK " + path + ": IA01 team=" + controller.TeamId + " em Pais2; slots válidos=" + ok + "; capital=" + capital + ".");
        else
            Debug.LogWarning("[IA/Territorios] REVISAR " + path + ": IA01 team=" + controller.TeamId + "; válidos=" + ok + "; inválidos=" + string.Join(" | ", errors.ToArray()) + ".");
    }

    private static void ValidarIA02(string path, IA02Controller controller, Terrain pais3)
    {
        if (controller == null || controller.CityLayout == null || pais3 == null)
        {
            Debug.LogError("[IA/Territorios] FALHA " + path + ": IA02 ou pais3 ausente.");
            return;
        }

        Bounds bounds = ObterBounds(pais3.gameObject);
        IA02BuildSlot[] slots = controller.CityLayout.GetComponentsInChildren<IA02BuildSlot>(true);
        int landOk = 0;
        int navalOk = 0;
        List<string> errors = new List<string>();
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null) continue;
            bool naval = slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water;
            bool valid = naval ? NavalPlacementResolver.IsWaterAtPosition(slot.transform.position)
                : ContainsXZ(bounds, slot.transform.position, 1f) && !NavalPlacementResolver.IsWaterAtPosition(slot.transform.position);
            if (valid && !controller.IsPositionInsidePreparedTerritory(slot.transform.position, 220f))
            {
                valid = false;
                errors.Add(slot.SlotId + "=" + slot.transform.position.ToString("F1") + " [territorio preparado]");
            }
            if (valid) { if (naval) navalOk++; else landOk++; }
            else if (valid == false && (errors.Count == 0 || !errors[errors.Count - 1].StartsWith(slot.SlotId + "=")))
                errors.Add(slot.SlotId + "=" + slot.transform.position.ToString("F1") + (naval ? " [naval]" : " [terra]"));
        }

        if (errors.Count == 0)
            Debug.Log("[IA/Territorios] OK " + path + ": IA02 team=" + controller.TeamId + " em pais3; terrestres=" + landOk + ", navais=" + navalOk + ".");
        else
            Debug.LogWarning("[IA/Territorios] REVISAR " + path + ": IA02 team=" + controller.TeamId + "; terrestres=" + landOk + ", navais=" + navalOk + "; inválidos=" + string.Join(" | ", errors.ToArray()) + ".");
    }

    private static Bounds ObterBoundsAgua()
    {
        GameObject agua = EncontrarObjeto("Agua");
        GameObject agua1 = EncontrarObjeto("Agua (1)") ?? EncontrarObjeto("agua1");
        bool found = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        if (agua != null)
        {
            result = ObterBounds(agua);
            found = true;
        }
        if (agua1 != null)
        {
            if (!found) result = ObterBounds(agua1);
            else result.Encapsulate(ObterBounds(agua1));
            found = true;
        }
        return found ? result : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static bool TryFindWaterAnchorNearCountry(Bounds country, Bounds waterBounds, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        if (waterBounds.size.sqrMagnitude < 1f) return false;

        float distance = Mathf.Clamp(Mathf.Min(country.size.x, country.size.z) * 0.025f, 80f, 350f);
        List<Vector3> candidates = new List<Vector3>(64);
        for (int i = 0; i <= 12; i++)
        {
            float t = i / 12f;
            float x = Mathf.Lerp(country.min.x + distance, country.max.x - distance, t);
            float z = Mathf.Lerp(country.min.z + distance, country.max.z - distance, t);
            candidates.Add(new Vector3(x, 0f, country.max.z + distance));
            candidates.Add(new Vector3(x, 0f, country.min.z - distance));
            candidates.Add(new Vector3(country.max.x + distance, 0f, z));
            candidates.Add(new Vector3(country.min.x - distance, 0f, z));
        }

        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!TryFindWaterNear(candidates[i], waterBounds, out Vector3 waterPoint)) continue;
            float candidateDistance = DistanceToBoundsXZ(waterPoint, country);
            if (!found || candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                anchor = waterPoint;
                found = true;
            }
        }

        if (found) return true;

        // Fallback limitado: procura uma grade no próprio volume de água,
        // sem aceitar um ponto dentro do território terrestre.
        for (int x = 0; x <= 12; x++)
        {
            for (int z = 0; z <= 12; z++)
            {
                Vector3 candidate = new Vector3(
                    Mathf.Lerp(waterBounds.min.x, waterBounds.max.x, x / 12f),
                    0f,
                    Mathf.Lerp(waterBounds.min.z, waterBounds.max.z, z / 12f));
                if (ContainsXZ(country, candidate, 0f)) continue;
                if (!TryFindWaterNear(candidate, waterBounds, out anchor)) continue;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindWaterNear(Vector3 desired, Bounds waterBounds, out Vector3 point)
    {
        point = desired;
        float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
        const float step = 160f;
        for (int ring = 0; ring <= 5; ring++)
        {
            for (int x = -ring; x <= ring; x++)
            {
                for (int z = -ring; z <= ring; z++)
                {
                    if (ring > 0 && Mathf.Abs(x) != ring && Mathf.Abs(z) != ring) continue;
                    Vector3 candidate = desired + new Vector3(x * step, 0f, z * step);
                    candidate.y = seaLevel;
                    // A malha de água pode ter Bounds com espessura Y zero ou
                    // começar ligeiramente acima do nível do mar. A validação
                    // de território é horizontal; não descarte o ponto por Y.
                    if (!ContainsXZ(waterBounds, candidate, 0f)) continue;
                    if (!NavalPlacementResolver.IsWaterAtPosition(candidate, seaLevel)) continue;
                    point = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    private static float DistanceToBoundsXZ(Vector3 point, Bounds bounds)
    {
        float dx = point.x < bounds.min.x ? bounds.min.x - point.x : point.x > bounds.max.x ? point.x - bounds.max.x : 0f;
        float dz = point.z < bounds.min.z ? bounds.min.z - point.z : point.z > bounds.max.z ? point.z - bounds.max.z : 0f;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static IA01Controller EncontrarIA01()
    {
        IA01Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA01Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null && controllers[i].TeamId == 2) return controllers[i];
        return controllers.Length > 0 ? controllers[0] : null;
    }

    private static IA02Controller EncontrarIA02()
    {
        IA02Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA02Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null && controllers[i].TeamId == 3) return controllers[i];
        return controllers.Length > 0 ? controllers[0] : null;
    }

    private static Transform EncontrarRaizIA01(IA01Controller controller)
    {
        Transform atual = controller != null ? controller.transform : null;
        while (atual != null)
        {
            if (string.Equals(atual.name, "ia01", StringComparison.OrdinalIgnoreCase)
                || atual.GetComponent<IA01Manager>() != null) return atual;
            atual = atual.parent;
        }
        return controller != null ? controller.transform.parent : null;
    }

    private static Terrain EncontrarTerrain(string name)
    {
        Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++)
            if (terrains[i] != null && string.Equals(terrains[i].name, name, StringComparison.OrdinalIgnoreCase)) return terrains[i];
        return null;
    }

    private static GameObject EncontrarObjeto(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
                if (children[j] != null && string.Equals(children[j].name, name, StringComparison.OrdinalIgnoreCase)) return children[j].gameObject;
        }
        return null;
    }

    private static float SampleTerrainHeight(Terrain terrain, Vector3 world)
    {
        return terrain.transform.position.y + terrain.SampleHeight(world);
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 point, float padding)
    {
        return point.x >= bounds.min.x + padding && point.x <= bounds.max.x - padding
            && point.z >= bounds.min.z + padding && point.z <= bounds.max.z - padding;
    }

    private static Bounds ObterBounds(GameObject objeto)
    {
        Terrain terrain = objeto != null ? objeto.GetComponent<Terrain>() : null;
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 size = Vector3.Scale(terrain.terrainData.size, new Vector3(
                Mathf.Abs(terrain.transform.lossyScale.x), Mathf.Abs(terrain.transform.lossyScale.y), Mathf.Abs(terrain.transform.lossyScale.z)));
            Bounds bounds = new Bounds(terrain.transform.position, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = new Vector3((i & 1) == 0 ? 0f : size.x, (i & 2) == 0 ? 0f : size.y, (i & 4) == 0 ? 0f : size.z);
                bounds.Encapsulate(terrain.transform.TransformPoint(local));
            }
            return bounds;
        }

        Renderer renderer = objeto != null ? objeto.GetComponentInChildren<Renderer>(true) : null;
        return renderer != null ? renderer.bounds : new Bounds(objeto != null ? objeto.transform.position : Vector3.zero, Vector3.one);
    }

    private static string DescreverBounds(Bounds bounds)
    {
        return "min=" + bounds.min.ToString("F0") + " max=" + bounds.max.ToString("F0") + " size=" + bounds.size.ToString("F0");
    }

    private static Scene AbrirCena(string path)
    {
        Scene ativa = SceneManager.GetActiveScene();
        if (!string.Equals(ativa.path, path, StringComparison.OrdinalIgnoreCase))
            ativa = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return ativa;
    }
}
#endif
