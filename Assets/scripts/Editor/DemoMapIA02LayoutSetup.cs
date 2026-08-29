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
/// Corrige a continuidade mar-terra da demo e posiciona somente a
/// infraestrutura paralela da IA02 em superfícies compatíveis.
///
/// A ferramenta é idempotente: não troca mais Agua (1) com pais3, não remove
/// objetos existentes e apenas atualiza as pontes e os slots da IA02 pelo
/// Bounds real calculado pelo Unity.
/// </summary>
public static class DemoMapIA02LayoutSetup
{
    private const string DemoScenePath = "Assets/_Recovery/demo1.unity";
    private const string CampaignScenePath = "Assets/Scenes/cena19).unity";
    private const string ContinuityName = "Continuidade_Mar_Demo";
    private const string WaterName = "Agua";
    private const string WaterOneName = "Agua (1)";
    private const string CountryThreeName = "pais3";
    private const string WallName = "paredao inimigo";
    private const string IA02RuntimeName = "IA02 Runtime - Uniao Carmesim";
    private const string IA02LayoutName = "IA02CityLayout - Uniao Carmesim";

    [MenuItem("Hegemonia/Demo/Alinhar agua, paredao e IA02", priority = 12)]
    public static void ConfigurarDemoEIA02()
    {
        Scene demo = AbrirCena(DemoScenePath);
        if (!ConfigurarCena(demo, true)) return;
        EditorSceneManager.SaveScene(demo);

        Scene campanha = AbrirCena(CampaignScenePath);
        if (campanha.IsValid() && campanha.isLoaded)
        {
            ConfigurarCena(campanha, false);
            EditorSceneManager.SaveScene(campanha);
        }

        AbrirCena(DemoScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Demo/IA02] Agua, paredao e infraestrutura da IA02 configurados na demo e na campanha. IA01 preservada.");
    }

    [MenuItem("Hegemonia/Demo/Validar agua, paredao e IA02", priority = 13)]
    public static void ValidarDemoEIA02()
    {
        Scene scene = AbrirCena(DemoScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Demo/IA02] Nao foi possivel abrir " + DemoScenePath + ".");
            return;
        }

        GameObject agua = EncontrarObjeto(WaterName);
        GameObject agua1 = EncontrarAgua1();
        GameObject pais3 = EncontrarObjeto(CountryThreeName);
        GameObject paredao = EncontrarObjeto(WallName);
        IA02Controller controller = EncontrarIA02Controller();

        Bounds boundsAgua = ObterBounds(agua);
        Bounds boundsAgua1 = ObterBounds(agua1);
        Bounds boundsPais3 = ObterBounds(pais3);
        Bounds boundsParedao = ObterBounds(paredao);

        Debug.Log("[Demo/IA02] Agua=" + DescreverBounds(boundsAgua));
        Debug.Log("[Demo/IA02] Agua (1)=" + DescreverBounds(boundsAgua1));
        Debug.Log("[Demo/IA02] pais3=" + DescreverBounds(boundsPais3));
        Debug.Log("[Demo/IA02] paredao=" + DescreverBounds(boundsParedao)
            + " | ativo=" + (paredao != null && paredao.activeInHierarchy)
            + " | terrain=" + (paredao != null && paredao.GetComponent<Terrain>() != null && paredao.GetComponent<Terrain>().enabled));

        ValidarConexao("Agua -> Agua (1)", EncontrarObjetoFilho(ContinuityName, "Agua_Conexao_Normal_Agua1"), boundsAgua, boundsAgua1);
        ValidarConexao("Agua (1) -> pais3", EncontrarObjetoFilho(ContinuityName, "Agua_Conexao_Agua1_Pais3"), boundsAgua1, boundsPais3);
        ValidarConexao("Agua -> paredao", EncontrarObjetoFilho(ContinuityName, "Agua_Conexao_Paredao_Inimigo"), boundsAgua, boundsParedao);

        if (controller == null || controller.CityLayout == null)
        {
            Debug.LogError("[Demo/IA02] IA02 sem controller ou layout.");
            return;
        }

        Terrain pais3Terrain = pais3 != null ? pais3.GetComponent<Terrain>() : null;
        ValidarSlots(controller.CityLayout, pais3Terrain, boundsPais3, boundsAgua, boundsAgua1);
    }

    private static bool ConfigurarCena(Scene scene, bool configurarMapa)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Demo/IA02] Cena invalida durante a configuracao.");
            return false;
        }

        GameObject agua = EncontrarObjeto(WaterName);
        GameObject agua1 = EncontrarAgua1();
        GameObject pais3 = EncontrarObjeto(CountryThreeName);
        GameObject paredao = EncontrarObjeto(WallName);
        if (agua == null || pais3 == null)
        {
            Debug.LogWarning("[Demo/IA02] " + scene.path + " nao possui Agua e pais3; infraestrutura IA02 nao reposicionada.");
            return false;
        }

        GarantirParedaoVisivel(paredao);

        Transform terrenos = EncontrarTransform("Terrenos");
        if (terrenos != null && agua1 != null)
        {
            GameObject grupo = GarantirGrupoContinuidade(terrenos);
            Bounds boundsAgua = ObterBounds(agua);
            Bounds boundsAgua1 = ObterBounds(agua1);
            Bounds boundsPais3 = ObterBounds(pais3);
            Bounds boundsParedao = paredao != null ? ObterBounds(paredao) : boundsAgua;
            Material materialAgua = ObterMaterial(agua, agua1);
            float nivelAgua = Mathf.Min(boundsAgua.center.y, boundsAgua1.center.y);

            ConfigurarConexao(grupo.transform, "Agua_Conexao_Normal_Agua1", boundsAgua, boundsAgua1, 150f, nivelAgua, materialAgua);
            ConfigurarConexao(grupo.transform, "Agua_Conexao_Agua1_Pais3", boundsAgua1, boundsPais3, 180f, nivelAgua, materialAgua);
            if (paredao != null)
                ConfigurarConexao(grupo.transform, "Agua_Conexao_Paredao_Inimigo", boundsAgua, boundsParedao, 180f, nivelAgua, materialAgua);

            MarcadorSuperficieMapa marcador = grupo.GetComponent<MarcadorSuperficieMapa>();
            if (marcador == null) marcador = grupo.AddComponent<MarcadorSuperficieMapa>();
            marcador.DefinirTipo(TipoSuperficieMapa.Agua);
            marcador.RecalcularAgora();
            EditorUtility.SetDirty(grupo);
        }
        else if (agua1 == null)
        {
            // Algumas cenas de campanha possuem apenas uma regiao de agua.
            // Nesse caso nao invente uma segunda continuidade: a IA02 ainda
            // pode usar a agua existente para estaleiro, pier e plataformas.
            Debug.Log("[Demo/IA02] " + scene.path + " possui uma unica regiao de agua; continuidade Agua1 ignorada.");
        }

        ConfigurarIA02(pais3, agua, agua1 != null ? agua1 : agua);
        if (configurarMapa) ConfigurarMapaDaDemo();

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static void GarantirParedaoVisivel(GameObject paredao)
    {
        if (paredao == null) return;
        if (!paredao.activeSelf) paredao.SetActive(true);
        Terrain terrain = paredao.GetComponent<Terrain>();
        if (terrain != null) terrain.enabled = true;
        Renderer renderer = paredao.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = true;
        EditorUtility.SetDirty(paredao);
    }

    private static GameObject GarantirGrupoContinuidade(Transform terrenos)
    {
        Transform grupo = terrenos.Find(ContinuityName);
        if (grupo != null) return grupo.gameObject;

        GameObject novo = new GameObject(ContinuityName);
        Undo.RegisterCreatedObjectUndo(novo, "Criar continuidade mar-terra da demo");
        novo.transform.SetParent(terrenos, false);
        return novo;
    }

    private static void ConfigurarConexao(Transform parent, string name, Bounds first, Bounds second,
        float width, float waterLevel, Material material)
    {
        Vector3 start;
        Vector3 end;
        if (!TryFindConnection(first, second, out start, out end))
        {
            // Superficies que ja se sobrepoem nao precisam de um corredor longo.
            // Mantemos a ponte existente, mas a reduzimos ao miolo da faixa
            // compartilhada para nao criar uma falsa parede sobre o terreno.
            Vector3 shared = FindSharedCenter(first, second);
            start = shared - Vector3.right * 70f;
            end = shared + Vector3.right * 70f;
        }

        start.y = waterLevel + 0.025f;
        end.y = waterLevel + 0.025f;
        Transform existing = parent.Find(name);
        GameObject bridge = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Plane);
        if (existing == null) Undo.RegisterCreatedObjectUndo(bridge, "Criar corredor maritimo da demo");
        bridge.name = name;
        bridge.layer = 4;
        bridge.transform.SetParent(parent, true);

        Vector3 delta = end - start;
        delta.y = 0f;
        float length = Mathf.Max(10f, delta.magnitude);
        Vector3 center = (start + end) * 0.5f;
        bridge.transform.SetPositionAndRotation(center, Quaternion.LookRotation(delta.normalized, Vector3.up));
        bridge.transform.localScale = new Vector3(Mathf.Max(10f, width) / 10f, 1f, length / 10f);

        Renderer renderer = bridge.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            if (material != null) renderer.sharedMaterial = material;
        }
        Collider collider = bridge.GetComponent<Collider>();
        if (collider != null) collider.enabled = true;
        EditorUtility.SetDirty(bridge);
    }

    private static bool TryFindConnection(Bounds first, Bounds second, out Vector3 start, out Vector3 end)
    {
        start = first.center;
        end = second.center;
        float overlapX = Mathf.Min(first.max.x, second.max.x) - Mathf.Max(first.min.x, second.min.x);
        float overlapZ = Mathf.Min(first.max.z, second.max.z) - Mathf.Max(first.min.z, second.min.z);
        const float extension = 20f;

        if (first.max.x < second.min.x)
        {
            float z = overlapZ > 0f ? (Mathf.Max(first.min.z, second.min.z) + Mathf.Min(first.max.z, second.max.z)) * 0.5f : (first.center.z + second.center.z) * 0.5f;
            start = new Vector3(first.max.x - extension, 0f, z);
            end = new Vector3(second.min.x + extension, 0f, z);
            return true;
        }
        if (second.max.x < first.min.x)
        {
            float z = overlapZ > 0f ? (Mathf.Max(first.min.z, second.min.z) + Mathf.Min(first.max.z, second.max.z)) * 0.5f : (first.center.z + second.center.z) * 0.5f;
            start = new Vector3(first.min.x + extension, 0f, z);
            end = new Vector3(second.max.x - extension, 0f, z);
            return true;
        }
        if (first.max.z < second.min.z)
        {
            float x = overlapX > 0f ? (Mathf.Max(first.min.x, second.min.x) + Mathf.Min(first.max.x, second.max.x)) * 0.5f : (first.center.x + second.center.x) * 0.5f;
            start = new Vector3(x, 0f, first.max.z - extension);
            end = new Vector3(x, 0f, second.min.z + extension);
            return true;
        }
        if (second.max.z < first.min.z)
        {
            float x = overlapX > 0f ? (Mathf.Max(first.min.x, second.min.x) + Mathf.Min(first.max.x, second.max.x)) * 0.5f : (first.center.x + second.center.x) * 0.5f;
            start = new Vector3(x, 0f, first.min.z + extension);
            end = new Vector3(x, 0f, second.max.z - extension);
            return true;
        }

        return false;
    }

    private static Vector3 FindSharedCenter(Bounds first, Bounds second)
    {
        float minX = Mathf.Max(first.min.x, second.min.x);
        float maxX = Mathf.Min(first.max.x, second.max.x);
        float minZ = Mathf.Max(first.min.z, second.min.z);
        float maxZ = Mathf.Min(first.max.z, second.max.z);
        return new Vector3(
            minX <= maxX ? (minX + maxX) * 0.5f : (first.center.x + second.center.x) * 0.5f,
            Mathf.Min(first.center.y, second.center.y),
            minZ <= maxZ ? (minZ + maxZ) * 0.5f : (first.center.z + second.center.z) * 0.5f);
    }

    private static void ConfigurarIA02(GameObject pais3Object, GameObject aguaObject, GameObject agua1Object)
    {
        IA02Controller controller = EncontrarIA02Controller();
        if (controller == null) return;

        IA02CityLayout layout = controller.CityLayout;
        if (layout == null) layout = EncontrarLayoutIA02();
        if (layout == null) return;

        Terrain pais3 = pais3Object != null ? pais3Object.GetComponent<Terrain>() : null;
        if (pais3 == null || pais3.terrainData == null) return;

        Bounds landBounds = ObterBounds(pais3Object);
        Bounds waterBounds = ObterBounds(aguaObject);
        Bounds waterOneBounds = ObterBounds(agua1Object);
        Transform runtime = EncontrarRuntimeIA02(controller, layout);
        if (runtime == null) return;

        Vector3 oldPosition = runtime.position;
        Vector3 anchor = EncontrarAncoraTerrestre(landBounds, controller, runtime.position);
        runtime.SetPositionAndRotation(anchor, Quaternion.identity);

        IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null) continue;
            string id = slot.SlotId;

            if (string.Equals(id, "ia02.local.estaleiro", StringComparison.OrdinalIgnoreCase))
            {
                PosicionarNaval(slot, CalcularPontoNaval(waterOneBounds, landBounds, -220f), runtime, 90f);
                continue;
            }
            if (string.Equals(id, "ia02.local.pier", StringComparison.OrdinalIgnoreCase))
            {
                PosicionarNaval(slot, CalcularPontoNaval(waterOneBounds, landBounds, 220f), runtime, 90f);
                continue;
            }
            if (id.StartsWith("ia02.local.plataforma.", StringComparison.OrdinalIgnoreCase))
            {
                PosicionarSlot(slot, EscolherPontoPlataforma(id, waterOneBounds, waterBounds), 0f, runtime);
                continue;
            }

            // Aeroportos e toda a infraestrutura terrestre permanecem dentro
            // do terreno pais3, com a altura amostrada no próprio Terrain.
            Vector3 local = slot.transform.localPosition;
            Vector3 world = runtime.TransformPoint(new Vector3(local.x, 0f, local.z));
            world.x = Mathf.Clamp(world.x, landBounds.min.x + 140f, landBounds.max.x - 140f);
            world.z = Mathf.Clamp(world.z, landBounds.min.z + 140f, landBounds.max.z - 140f);
            world.y = SampleTerrainWorldHeight(pais3, world);
            slot.transform.position = world;
            EditorUtility.SetDirty(slot);
        }

        layout.EnsureRuntimeReady();
        layout.ConfigureOwner(3, 3);
        GarantirComponentesEspeciaisAeroportos(layout);
        ConfigurarReferenciasAeroportos(layout);
        EditorUtility.SetDirty(layout);
        EditorUtility.SetDirty(runtime.gameObject);
        Debug.Log("[Demo/IA02] IA02 reposicionada em terra valida de pais3; slots terrestres dentro do terreno e slots navais sobre agua/ponte. antiga=" + oldPosition.ToString("F1") + " nova=" + runtime.position.ToString("F1"));
    }

    private static Vector3 EncontrarAncoraTerrestre(Bounds landBounds, IA02Controller controller, Vector3 fallback)
    {
        Vector3[] candidatos =
        {
            // A raiz da IA02 precisa ficar proxima da costa: o runtime usa
            // esta distancia para validar o estaleiro e o pier. O candidato
            // continua em terra, mas deixa os creates navais dentro do
            // envelope de construcao preparado.
            new Vector3(landBounds.min.x + 500f, 0f, landBounds.center.z - 1000f),
            new Vector3(landBounds.min.x + 1300f, 0f, landBounds.center.z - 1800f),
            new Vector3(landBounds.min.x + 1500f, 0f, landBounds.center.z + 1800f),
            new Vector3(landBounds.max.x - 1300f, 0f, landBounds.center.z - 1800f),
            new Vector3(landBounds.max.x - 1300f, 0f, landBounds.center.z + 1800f),
            new Vector3(landBounds.center.x, 0f, landBounds.center.z)
        };

        Vector3 ia01 = Vector3.zero;
        IA01Controller[] ia01Controllers = UnityEngine.Object.FindObjectsByType<IA01Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (ia01Controllers.Length > 0 && ia01Controllers[0] != null) ia01 = ia01Controllers[0].transform.position;
        GameObject player = null;
        try { player = GameObject.FindGameObjectWithTag("Player"); } catch (UnityException) { }
        Vector3 jogador = player != null ? player.transform.position : Vector3.zero;

        float melhorPontuacao = float.MinValue;
        Vector3 melhor = fallback;
        for (int i = 0; i < candidatos.Length; i++)
        {
            Vector3 candidato = candidatos[i];
            if (!ContainsXZ(landBounds, candidato, 180f)) continue;
            float distanciaIA01 = ia01Controllers.Length > 0 ? DistanciaXZ(candidato, ia01) : 0f;
            float distanciaJogador = player != null ? DistanciaXZ(candidato, jogador) : 0f;
            float pontuacao = Mathf.Min(distanciaIA01, player != null ? distanciaJogador : distanciaIA01);
            // A infraestrutura naval oficial fica na costa. Entre candidatos
            // seguros, priorize a ancora costeira para que o runtime nao
            // rejeite estaleiro/pier por estarem distantes da raiz da IA02.
            if (i == 0 && pontuacao >= 1500f) pontuacao += 100000f;
            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = candidato;
            }
        }
        return melhor;
    }

    private static Vector3 CalcularPontoNaval(Bounds waterOne, Bounds land, float offset)
    {
        // A agua1 termina antes da borda esquerda de pais3. O centro da
        // conexao e o ponto mais seguro para o estaleiro e o pier; o offset
        // separa os dois sem colocar nenhum deles dentro do terreno.
        float gapX = Mathf.Max(waterOne.max.x, land.min.x);
        float endX = Mathf.Min(waterOne.max.x, land.min.x);
        float x = (waterOne.max.x + land.min.x) * 0.5f;
        if (land.min.x <= waterOne.max.x) x = waterOne.max.x - 100f;
        float minZ = Mathf.Max(waterOne.min.z, land.min.z);
        float maxZ = Mathf.Min(waterOne.max.z, land.max.z);
        float z = minZ < maxZ ? (minZ + maxZ) * 0.5f : waterOne.center.z;
        Vector3 result = new Vector3(x, 0f, z + Mathf.Clamp(offset, -320f, 320f));
        if (minZ < maxZ) result.z = Mathf.Clamp(result.z, minZ + 120f, maxZ - 120f);
        return result;
    }

    private static Vector3 EscolherPontoPlataforma(string id, Bounds waterOne, Bounds water)
    {
        Bounds b = waterOne.size.sqrMagnitude > 1f ? waterOne : water;
        float px = id.EndsWith(".a", StringComparison.OrdinalIgnoreCase) ? 0.22f
            : id.EndsWith(".b", StringComparison.OrdinalIgnoreCase) ? 0.52f : 0.80f;
        float pz = id.EndsWith(".a", StringComparison.OrdinalIgnoreCase) ? 0.30f
            : id.EndsWith(".b", StringComparison.OrdinalIgnoreCase) ? 0.54f : 0.76f;
        return new Vector3(Mathf.Lerp(b.min.x, b.max.x, px), 0f, Mathf.Lerp(b.min.z, b.max.z, pz));
    }

    private static void PosicionarNaval(IA02BuildSlot slot, Vector3 world, Transform runtime, float yaw)
    {
        PosicionarSlot(slot, world, 0f, runtime);
        slot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Transform spawn = slot.transform.Find("Spawn_Unidades");
        Transform exit = slot.transform.Find("Direcao_Saida");
        if (spawn != null) spawn.localPosition = new Vector3(0f, 0f, 36f);
        if (exit != null) exit.localPosition = new Vector3(0f, 0f, 120f);
        IA02NavalBuildSlot naval = slot.GetComponent<IA02NavalBuildSlot>();
        if (naval != null)
        {
            SerializedObject serialized = new SerializedObject(naval);
            SetObject(serialized, "buildSlot", slot);
            SetObject(serialized, "navalSpawnPoint", spawn);
            SetObject(serialized, "exitDirection", exit);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            naval.InvalidateCache();
            EditorUtility.SetDirty(naval);
        }
        EditorUtility.SetDirty(slot);
    }

    private static void PosicionarSlot(IA02BuildSlot slot, Vector3 world, float y, Transform runtime)
    {
        world.y = y;
        slot.transform.position = world;
        EditorUtility.SetDirty(slot);
    }

    private static void ConfigurarReferenciasAeroportos(IA02CityLayout layout)
    {
        IA02AirportBuildSlot[] airports = layout.GetComponentsInChildren<IA02AirportBuildSlot>(true);
        for (int i = 0; i < airports.Length; i++)
        {
            if (airports[i] == null) continue;
            airports[i].InvalidateCache();
            EditorUtility.SetDirty(airports[i]);
        }
    }

    private static void GarantirComponentesEspeciaisAeroportos(IA02CityLayout layout)
    {
        if (layout == null) return;

        IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null || slot.AllowedDomain != IA02BuildDomain.Airfield) continue;

            Transform marker = slot.transform;
            Transform spawn = marker.Find("Spawn_Unidades");
            Transform exit = marker.Find("Direcao_Saida");
            Transform runwayStart = marker.Find("Pista_Inicio");
            Transform runwayEnd = marker.Find("Pista_Fim");

            if (spawn == null) spawn = CriarFilhoMarcador(marker, "Spawn_Unidades", new Vector3(0f, 0f, 16f));
            if (exit == null) exit = CriarFilhoMarcador(marker, "Direcao_Saida", new Vector3(0f, 0f, 42f));
            if (runwayStart == null) runwayStart = CriarFilhoMarcador(marker, "Pista_Inicio", new Vector3(-50f, 0f, 0f));
            if (runwayEnd == null) runwayEnd = CriarFilhoMarcador(marker, "Pista_Fim", new Vector3(50f, 0f, 0f));

            IA02AirportBuildSlot airport = marker.GetComponent<IA02AirportBuildSlot>();
            if (airport == null) airport = Undo.AddComponent<IA02AirportBuildSlot>(marker.gameObject);

            SerializedObject serialized = new SerializedObject(airport);
            SetObject(serialized, "buildSlot", slot);
            SetObject(serialized, "runwayStart", runwayStart);
            SetObject(serialized, "runwayEnd", runwayEnd);
            SetObject(serialized, "aircraftSpawn", spawn);
            SetObject(serialized, "approachDirection", exit);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            airport.InvalidateCache();
            EditorUtility.SetDirty(airport);
        }
    }

    private static Transform CriarFilhoMarcador(Transform parent, string name, Vector3 localPosition)
    {
        GameObject childObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(childObject, "Criar ponto de aeroporto IA02");
        Transform child = childObject.transform;
        child.SetParent(parent, false);
        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        EditorUtility.SetDirty(child);
        return child;
    }

    private static void ValidarSlots(IA02CityLayout layout, Terrain pais3, Bounds land, Bounds water, Bounds waterOne)
    {
        IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
        int terraOk = 0;
        int aguaOk = 0;
        List<string> problemas = new List<string>();
        for (int i = 0; i < slots.Length; i++)
        {
            IA02BuildSlot slot = slots[i];
            if (slot == null) continue;
            bool naval = slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water;
            bool ok = naval ? (ContainsXZ(water, slot.transform.position, 1f) || ContainsXZ(waterOne, slot.transform.position, 1f))
                : ContainsXZ(land, slot.transform.position, 1f);
            if (ok)
            {
                if (naval) aguaOk++; else terraOk++;
            }
            else problemas.Add(slot.SlotId + " em dominio incorreto: " + slot.transform.position.ToString("F1"));

            if (slot.AllowedDomain == IA02BuildDomain.Airfield)
            {
                IA02AirportBuildSlot airport = slot.GetComponent<IA02AirportBuildSlot>();
                if (airport == null)
                {
                    problemas.Add(slot.SlotId + " aeroporto invalido: componente ausente");
                }
                else
                {
                    string reason = string.Empty;
                    if (!airport.TryValidateCached(out reason)) problemas.Add(slot.SlotId + " aeroporto invalido: " + reason);
                }
            }
            if (slot.AllowedDomain == IA02BuildDomain.Coastal && slot.GetComponent<IA02NavalBuildSlot>() != null)
            {
                string reason = string.Empty;
                if (!slot.GetComponent<IA02NavalBuildSlot>().TryValidateCached(out reason)) problemas.Add(slot.SlotId + " naval invalido: " + reason);
            }
        }

        if (problemas.Count == 0) Debug.Log("[Demo/IA02] Validacao OK: slots terrestres=" + terraOk + ", slots agua/costeiros=" + aguaOk + ".");
        else Debug.LogWarning("[Demo/IA02] Problemas de slots: " + string.Join("; ", problemas.ToArray()));
    }

    private static void ValidarConexao(string nome, GameObject ponte, Bounds first, Bounds second)
    {
        if (ponte == null)
        {
            Debug.LogError("[Demo/IA02] Conexao ausente: " + nome);
            return;
        }

        Bounds ponteBounds = ObterBounds(ponte);
        bool tocaFirst = ContainsXZ(ponteBounds, first.center, 2500f) || IntersectaXZ(ponteBounds, first);
        bool tocaSecond = ContainsXZ(ponteBounds, second.center, 2500f) || IntersectaXZ(ponteBounds, second);
        Debug.Log("[Demo/IA02] " + nome + " | centro=" + ponte.transform.position.ToString("F1")
            + " | tocaA=" + tocaFirst + " | tocaB=" + tocaSecond
            + " | bounds=" + DescreverBounds(ponteBounds));
    }

    private static bool IntersectaXZ(Bounds a, Bounds b)
    {
        return a.max.x >= b.min.x && a.min.x <= b.max.x && a.max.z >= b.min.z && a.min.z <= b.max.z;
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 point, float padding)
    {
        return point.x >= bounds.min.x - padding && point.x <= bounds.max.x + padding
            && point.z >= bounds.min.z - padding && point.z <= bounds.max.z + padding;
    }

    private static float DistanciaXZ(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    private static float SampleTerrainWorldHeight(Terrain terrain, Vector3 world)
    {
        return terrain.transform.position.y + terrain.SampleHeight(world);
    }

    private static IA02Controller EncontrarIA02Controller()
    {
        IA02Controller[] controllers = UnityEngine.Object.FindObjectsByType<IA02Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null && (controllers[i].TeamId == 3 || controllers[i].name.IndexOf("IA02", StringComparison.OrdinalIgnoreCase) >= 0)) return controllers[i];
        return controllers.Length > 0 ? controllers[0] : null;
    }

    private static IA02CityLayout EncontrarLayoutIA02()
    {
        IA02CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA02CityLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < layouts.Length; i++)
            if (layouts[i] != null && layouts[i].name.IndexOf(IA02LayoutName, StringComparison.OrdinalIgnoreCase) >= 0) return layouts[i];
        return layouts.Length > 0 ? layouts[0] : null;
    }

    private static Transform EncontrarRuntimeIA02(IA02Controller controller, IA02CityLayout layout)
    {
        Transform atual = controller != null ? controller.transform : null;
        while (atual != null)
        {
            if (atual.name.IndexOf(IA02RuntimeName, StringComparison.OrdinalIgnoreCase) >= 0) return atual;
            atual = atual.parent;
        }
        atual = layout != null ? layout.transform.parent : null;
        return atual != null ? atual : controller != null ? controller.transform : null;
    }

    private static Scene AbrirCena(string path)
    {
        Scene ativa = SceneManager.GetActiveScene();
        if (!string.Equals(ativa.path, path, StringComparison.OrdinalIgnoreCase))
            ativa = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return ativa;
    }

    private static Transform EncontrarTransform(string name)
    {
        GameObject obj = EncontrarObjeto(name);
        return obj != null ? obj.transform : null;
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

    private static GameObject EncontrarAgua1()
    {
        // A demo possui variantes legadas do nome. A identificacao por
        // alternativas evita que a configuracao pare antes de recalcular as
        // conexoes quando o objeto foi salvo como "agua1".
        GameObject agua1 = EncontrarObjeto(WaterOneName);
        return agua1 != null ? agua1 : EncontrarObjeto("agua1");
    }

    private static GameObject EncontrarObjetoFilho(string parentName, string childName)
    {
        GameObject parent = EncontrarObjeto(parentName);
        if (parent == null) return null;
        Transform child = parent.transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static Bounds ObterBounds(GameObject objeto)
    {
        bool found = false;
        Bounds result = new Bounds(objeto != null ? objeto.transform.position : Vector3.zero, Vector3.one);
        if (objeto == null) return result;

        Terrain[] terrains = objeto.GetComponentsInChildren<Terrain>(true);
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 size = Vector3.Scale(terrain.terrainData.size, new Vector3(Mathf.Abs(terrain.transform.lossyScale.x), Mathf.Abs(terrain.transform.lossyScale.y), Mathf.Abs(terrain.transform.lossyScale.z)));
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = new Vector3((corner & 1) == 0 ? 0f : size.x, (corner & 2) == 0 ? 0f : size.y, (corner & 4) == 0 ? 0f : size.z);
                Vector3 world = terrain.transform.TransformPoint(local);
                if (!found) { result = new Bounds(world, Vector3.zero); found = true; }
                else result.Encapsulate(world);
            }
        }

        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;
            if (!found) { result = renderer.bounds; found = true; }
            else result.Encapsulate(renderer.bounds);
        }

        Collider[] colliders = objeto.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled) continue;
            if (!found) { result = collider.bounds; found = true; }
            else result.Encapsulate(collider.bounds);
        }

        if (!found) result = new Bounds(objeto.transform.position, new Vector3(100f, 2f, 100f));
        return result;
    }

    private static Material ObterMaterial(GameObject first, GameObject second)
    {
        Renderer renderer = first != null ? first.GetComponentInChildren<Renderer>(true) : null;
        if (renderer == null && second != null) renderer = second.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static string DescreverBounds(Bounds bounds)
    {
        return "min=" + bounds.min.ToString("F1") + " max=" + bounds.max.ToString("F1") + " size=" + bounds.size.ToString("F1");
    }

    private static void ConfigurarMapaDaDemo()
    {
        MapaGeralController[] mapas = UnityEngine.Object.FindObjectsByType<MapaGeralController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < mapas.Length; i++)
        {
            MapaGeralController mapa = mapas[i];
            if (mapa == null || !mapa.gameObject.activeSelf) continue;
            SerializedObject serialized = new SerializedObject(mapa);
            SetBool(serialized, "detectarLimitesReaisDoMapa", true);
            SetFloat(serialized, "margemMapa", 350f);
            SetBool(serialized, "enquadrarCoberturaCompletaAoAbrir", true);
            SetFloat(serialized, "margemEnquadramentoInicial", 1.08f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mapa);
            break;
        }
    }

    private static void SetObject(SerializedObject serialized, string path, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject serialized, string path, bool value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null) property.boolValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string path, float value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null) property.floatValue = value;
    }
}
#endif
