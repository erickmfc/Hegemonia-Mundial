using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante que todos os terrenos jogáveis da cena sejam reconhecidos como chão.
/// Terrains auxiliares do mapa inimigo continuam invisíveis no mundo 3D.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class InicializadorSuperficiesMapa : MonoBehaviour
{
    private const string NomeObjeto = "[InicializadorSuperficiesMapa]";
    private const string MaterialTerrainResource = "CodexCampaignTerrainURP";
    private const float MargemRecorteCamera = 500f;
    // A cena canônica possui tiles ativos que ficam além de 14 km da
    // câmera inicial. O limite anterior cortava a borda direita do mapa.
    private const float RecorteMaximoSeguro = 20000f;
    private static readonly Dictionary<int, Material> MateriaisTerrainRuntime = new Dictionary<int, Material>();
    private static Texture2D TexturaTerrainFallback;
    private static Texture2D ControleTerrainFallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<InicializadorSuperficiesMapa>() != null)
        {
            return;
        }

        GameObject go = new GameObject(NomeObjeto);
        DontDestroyOnLoad(go);
        go.AddComponent<InicializadorSuperficiesMapa>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CorrigirCenaAtual();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CorrigirCenaAtual();
    }

    private void CorrigirCenaAtual()
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int terrenosJogaveis = 0;
        int terrenosReativados = 0;
        int marcadoresCriados = 0;
        int materiaisCorrigidos = 0;
        int instancingDesativado = 0;
        int escalasNormalizadas = 0;

        int camadaChao = LayerMask.NameToLayer("Chao");
        Material materialTerrain = Resources.Load<Material>(MaterialTerrainResource);

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || !terrain.gameObject.scene.IsValid())
            {
                continue;
            }

            if (false && EhTerrenoAuxiliarInimigo(terrain))
            {
                // O mapa auxiliar continua disponível para colisão/navegação,
                // mas não pode aparecer como uma segunda superfície no mundo.
                terrain.enabled = false;
                continue;
            }

            // Terrains inativos da cena podem ser mapas auxiliares ou
            // rascunhos posicionados sobre o combate. O "Terrain" antigo da
            // cena teste é um rascunho sem TerrainLayers e precisa continuar
            // desativado; reativá-lo cria o piso branco/manchado sobre a IA.
            bool permitirTerrenoPrincipalInativo = false;
            bool ehTerrenoIA = EhTerrenoAuxiliarInimigo(terrain);
            if (!terrain.gameObject.activeInHierarchy && !permitirTerrenoPrincipalInativo && !ehTerrenoIA)
            {
                continue;
            }

            if (!terrain.gameObject.activeSelf)
            {
                terrain.gameObject.SetActive(true);
                terrenosReativados++;
            }

            if (ehTerrenoIA && (Mathf.Abs(terrain.transform.localScale.x) < 0.001f ||
                                Mathf.Abs(terrain.transform.localScale.y) < 0.001f ||
                                Mathf.Abs(terrain.transform.localScale.z) < 0.001f))
            {
                terrain.transform.localScale = Vector3.one;
                escalasNormalizadas++;
            }

            terrenosJogaveis++;

            terrain.enabled = true;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.enabled = true;
            }

            // Toda superfície Terrain da partida usa o mesmo material URP
            // validado. Isso cobre também Terrains adicionados na cena e
            // evita que o fallback padrão fique invisível na build.
            Material materialParaTerreno = ConfigurarMaterialTerrain(terrain, materialTerrain);
            if (materialParaTerreno != null && terrain.materialTemplate != materialParaTerreno)
            {
                terrain.materialTemplate = materialParaTerreno;
                materiaisCorrigidos++;
            }

            if (terrain.drawInstanced)
            {
                terrain.drawInstanced = false;
                instancingDesativado++;
            }

            // Preserve o material já configurado no asset. Nesta configuração
            // URP, remover o override faz o Terrain cair em um shader padrão
            // ausente e a superfície fica magenta. A correção de material deve
            // ser feita no asset pelo Editor quando houver um shader Terrain
            // compatível; o runtime não substitui a aparência funcional.

            if (camadaChao >= 0 && terrain.gameObject.layer != camadaChao)
            {
                terrain.gameObject.layer = camadaChao;
            }

            MarcadorSuperficieMapa marcador = terrain.GetComponent<MarcadorSuperficieMapa>();
            if (marcador == null)
            {
                marcador = terrain.gameObject.AddComponent<MarcadorSuperficieMapa>();
                marcadoresCriados++;
            }

            marcador.DefinirTipo(TipoSuperficieMapa.Chao);
        }

        AjustarRecorteDasCameras(terrains);

        if (terrenosReativados > 0 || escalasNormalizadas > 0 || marcadoresCriados > 0 || materiaisCorrigidos > 0 || instancingDesativado > 0)
        {
            Debug.Log($"[Mapa] superfícies corrigidas: terrenos={terrenosJogaveis}, reativados={terrenosReativados}, marcadores={marcadoresCriados}");
        }
    }

    private static Material ConfigurarMaterialTerrain(Terrain terrain, Material materialBase)
    {
        if (terrain == null || terrain.terrainData == null || materialBase == null)
        {
            return terrain != null ? terrain.materialTemplate : null;
        }

        TerrainLayer[] camadas = terrain.terrainData.terrainLayers;

        int terrainDataId = terrain.terrainData.GetInstanceID();
        Material materialRuntime;
        if (MateriaisTerrainRuntime.TryGetValue(terrainDataId, out materialRuntime) && materialRuntime != null)
        {
            return materialRuntime;
        }

        string nomeTerreno = terrain.name.ToLowerInvariant();
        // O terreno auxiliar da IA precisa usar a mesma superfície estável da
        // Ilha. O material Terrain customizado depende das TerrainLayers e
        // pode renderizar branco/manchado na build quando essas camadas não
        // são incluídas ou não são compatíveis com o pipeline ativo.
        bool ehTerrenoIA = nomeTerreno.Contains("ilha") || nomeTerreno.Contains("mapa inimigo");
        if (ehTerrenoIA)
        {
            Shader shaderSolo = Shader.Find("Universal Render Pipeline/Lit");
            if (shaderSolo != null)
            {
                materialRuntime = new Material(shaderSolo)
                {
                    name = nomeTerreno.Contains("mapa inimigo") ? "[Runtime] Mapa inimigo Ground" : "[Runtime] Ilha IA Ground"
                };
                materialRuntime.SetTexture("_BaseMap", ObterTexturaTerrainFallback());
                materialRuntime.SetColor("_BaseColor", Color.white);
                materialRuntime.SetFloat("_Smoothness", 0.15f);
                materialRuntime.SetTextureScale("_BaseMap", new Vector2(32f, 32f));
                MateriaisTerrainRuntime[terrainDataId] = materialRuntime;
                Debug.Log($"[Mapa] Terrain IA usando material de solo estavel: nome={terrain.name}");
                return materialRuntime;
            }
        }

        materialRuntime = new Material(materialBase)
        {
            name = "[Runtime] " + terrain.name + " Terrain"
        };

        int quantidadeCamadas = Mathf.Clamp(camadas != null ? camadas.Length : 0, 1, 4);
        if (materialRuntime.HasProperty("_NumLayersCount"))
        {
            materialRuntime.SetFloat("_NumLayersCount", quantidadeCamadas);
        }

        Texture2D[] controles = terrain.terrainData.alphamapTextures;
        if (controles != null && controles.Length > 0 && materialRuntime.HasProperty("_Control"))
        {
            materialRuntime.SetTexture("_Control", controles[0]);
        }

        if (camadas == null || camadas.Length == 0)
        {
            // A cena teste foi criada sem TerrainLayers externas. Gere uma
            // textura simples e estável para que a área da IA não dependa de
            // um material comum ou de um shader incompatível na build.
            if (materialRuntime.HasProperty("_Control"))
            {
                materialRuntime.SetTexture("_Control", ObterControleTerrainFallback());
            }

            if (materialRuntime.HasProperty("_Splat0"))
            {
                materialRuntime.SetTexture("_Splat0", ObterTexturaTerrainFallback());
            }

            if (materialRuntime.HasProperty("_BaseColor"))
            {
                materialRuntime.SetColor("_BaseColor", Color.white);
            }

            ConfigurarParametrosTerrain(materialRuntime, 0, null, terrain.terrainData);
        }

        for (int i = 0; i < quantidadeCamadas; i++)
        {
            if (camadas == null || i >= camadas.Length)
            {
                break;
            }

            TerrainLayer camada = camadas[i];
            if (camada == null)
            {
                continue;
            }

            if (materialRuntime.HasProperty("_Splat" + i))
            {
                materialRuntime.SetTexture("_Splat" + i, camada.diffuseTexture);
            }

            if (materialRuntime.HasProperty("_Normal" + i))
            {
                materialRuntime.SetTexture("_Normal" + i, camada.normalMapTexture);
            }

            if (materialRuntime.HasProperty("_Mask" + i))
            {
                materialRuntime.SetTexture("_Mask" + i, camada.maskMapTexture);
            }

            if (materialRuntime.HasProperty("_Metallic" + i))
            {
                materialRuntime.SetFloat("_Metallic" + i, camada.metallic);
            }

            if (materialRuntime.HasProperty("_Smoothness" + i))
            {
                materialRuntime.SetFloat("_Smoothness" + i, camada.smoothness);
            }

            ConfigurarParametrosTerrain(materialRuntime, i, camada, terrain.terrainData);
        }

        MateriaisTerrainRuntime[terrainDataId] = materialRuntime;
        Debug.Log($"[Mapa] Terrain configurado: nome={terrain.name} camadas={(camadas != null ? camadas.Length : 0)} controles={(controles != null ? controles.Length : 0)} fallback={(camadas == null || camadas.Length == 0)}");
        return materialRuntime;
    }

    private static void ConfigurarParametrosTerrain(Material material, int indice, TerrainLayer camada, TerrainData dados)
    {
        string sufixo = indice.ToString();
        Vector4 remapMin = camada != null ? camada.diffuseRemapMin : Vector4.zero;
        Vector4 remapMax = camada != null ? camada.diffuseRemapMax : Vector4.one;
        Vector4 remapScale = remapMax - remapMin;

        if (material.HasProperty("_DiffuseRemapScale" + sufixo))
        {
            material.SetVector("_DiffuseRemapScale" + sufixo, remapScale);
        }

        if (material.HasProperty("_DiffuseHasAlpha" + sufixo))
        {
            material.SetFloat("_DiffuseHasAlpha" + sufixo, 0f);
        }

        if (material.HasProperty("_LayerHasMask" + sufixo))
        {
            material.SetFloat("_LayerHasMask" + sufixo, camada != null && camada.maskMapTexture != null ? 1f : 0f);
        }

        if (material.HasProperty("_NormalScale" + sufixo))
        {
            material.SetFloat("_NormalScale" + sufixo, camada != null ? camada.normalScale : 1f);
        }

        if (material.HasProperty("_MaskMapRemapOffset" + sufixo))
        {
            material.SetVector("_MaskMapRemapOffset" + sufixo, camada != null ? camada.maskMapRemapMin : Vector4.zero);
        }

        if (material.HasProperty("_MaskMapRemapScale" + sufixo))
        {
            material.SetVector("_MaskMapRemapScale" + sufixo,
                camada != null ? camada.maskMapRemapMax - camada.maskMapRemapMin : Vector4.one);
        }

        if (camada != null && dados != null && camada.tileSize.x > 0f && camada.tileSize.y > 0f)
        {
            Vector2 repeticao = new Vector2(dados.size.x / camada.tileSize.x, dados.size.z / camada.tileSize.y);
            if (material.HasProperty("_Splat" + indice))
            {
                material.SetTextureScale("_Splat" + indice, repeticao);
                material.SetTextureOffset("_Splat" + indice, camada.tileOffset);
            }

            if (material.HasProperty("_Normal" + indice))
            {
                material.SetTextureScale("_Normal" + indice, repeticao);
                material.SetTextureOffset("_Normal" + indice, camada.tileOffset);
            }

            if (material.HasProperty("_Mask" + indice))
            {
                material.SetTextureScale("_Mask" + indice, repeticao);
                material.SetTextureOffset("_Mask" + indice, camada.tileOffset);
            }
        }
    }

    private static Texture2D ObterControleTerrainFallback()
    {
        if (ControleTerrainFallback != null)
        {
            return ControleTerrainFallback;
        }

        ControleTerrainFallback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
        {
            name = "[Runtime] Terrain Control Fallback",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        ControleTerrainFallback.SetPixel(0, 0, Color.red);
        ControleTerrainFallback.Apply(false, true);
        return ControleTerrainFallback;
    }

    private static Texture2D ObterTexturaTerrainFallback()
    {
        if (TexturaTerrainFallback != null)
        {
            return TexturaTerrainFallback;
        }

        const int tamanho = 64;
        TexturaTerrainFallback = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false, false)
        {
            name = "[Runtime] Terrain Ground Fallback",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[tamanho * tamanho];
        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                float ondulacao = (Mathf.Sin(x * 0.31f) + Mathf.Cos(y * 0.27f) + Mathf.Sin((x + y) * 0.11f)) * 0.5f;
                float vegetacao = Mathf.Clamp01(0.42f + ondulacao * 0.16f);
                Color corSolo = Color.Lerp(new Color(0.58f, 0.44f, 0.27f), new Color(0.24f, 0.38f, 0.18f), vegetacao);
                pixels[y * tamanho + x] = corSolo;
            }
        }

        TexturaTerrainFallback.SetPixels32(pixels);
        TexturaTerrainFallback.Apply(false, true);
        return TexturaTerrainFallback;
    }

    private static bool EhTerrenoAuxiliarInimigo(Terrain terrain)
    {
        string nome = terrain.name.ToLowerInvariant();
        return nome.Contains("mapa inimigo") || nome.Contains("mapa_inimigo") || nome.Contains("enemy map");
    }

    private static void AjustarRecorteDasCameras(Terrain[] terrains)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int c = 0; c < cameras.Length; c++)
        {
            Camera camera = cameras[c];
            if (camera == null || !camera.enabled || !camera.gameObject.scene.IsValid())
            {
                continue;
            }

            float distanciaNecessaria = camera.farClipPlane;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy ||
                    terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 escala = terrain.transform.lossyScale;
                Vector3 tamanho = Vector3.Scale(terrain.terrainData.size, new Vector3(
                    Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
                Vector3 centro = terrain.GetPosition() + tamanho * 0.5f;
                float raio = tamanho.magnitude * 0.5f;
                distanciaNecessaria = Mathf.Max(distanciaNecessaria,
                    Vector3.Distance(camera.transform.position, centro) + raio + MargemRecorteCamera);
            }

            camera.farClipPlane = Mathf.Clamp(distanciaNecessaria, camera.farClipPlane, RecorteMaximoSeguro);
        }
    }
}
