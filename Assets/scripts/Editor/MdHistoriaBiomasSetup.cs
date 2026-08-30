#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configuração visual e física da cena Md Historia.
/// A ferramenta é idempotente: só substitui os TerrainData que ela própria copia
/// e só recria objetos com o prefixo LimiteMdHistoria_.
/// </summary>
public static class MdHistoriaBiomasSetup
{
    private const string CenaAlvo = "Assets/_Recovery/Md Historia.unity";
    private const string PastaBase = "Assets/MapThemes/MdHistoria";
    private const string PastaTexturas = PastaBase + "/Texturas";
    private const string PastaCamadas = PastaBase + "/Camadas";
    private const string PastaMalhas = PastaBase + "/Malhas";
    private const string RaizLimites = "LimitesMapa_MdHistoria";
    private const string PrefixoParede = "LimiteMdHistoria_";
    private const string RaizExtensoes = "ExtensoesFronteira_Neutras";
    private const string PrefixoZona = "ZonaExpansaoFronteira_";

    private enum Bioma
    {
        Agua,
        Tropical,
        Arido,
        Neve,
        Temperado,
        Montanhoso,
        Floresta,
        Fronteira
    }

    private readonly struct Paleta
    {
        public readonly Color Escuro;
        public readonly Color Claro;

        public Paleta(Color escuro, Color claro)
        {
            Escuro = escuro;
            Claro = claro;
        }
    }

    [MenuItem("Hegemonia/Mapa/Md Historia/Aplicar biomas leves e paredões", priority = 30)]
    public static void AplicarBiomasEParedoes()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!ValidarCenaAtiva(cena))
        {
            return;
        }

        GarantirPastas();
        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrenos.Length == 0)
        {
            Debug.LogError("[Md Historia] Nenhum Terrain encontrado na cena ativa.");
            return;
        }

        int paises = 0;
        int aguas = 0;
        int fronteiras = 0;
        foreach (Terrain terreno in terrenos)
        {
            Bioma bioma = DeterminarBioma(terreno.gameObject.name);
            if (bioma == Bioma.Agua)
            {
                aguas++;
            }
            else if (bioma == Bioma.Fronteira)
            {
                fronteiras++;
            }
            else
            {
                paises++;
            }

            ConfigurarTerreno(terreno, bioma);
        }

        Bounds limites = CalcularLimites(terrenos);
        CriarParedoes(limites);
        CriarSeaMdHistoria(terrenos, 0f);
        CriarRegistroMapa(limites);
        CriarExtensoesFronteira(terrenos);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Md Historia] Biomas aplicados: paises=" + paises
            + ", fronteiras=" + fronteiras
            + ", tiles de agua=" + aguas
            + ", terrenos=" + terrenos.Length
            + ". Sea/OceanAdvanced configurado e limites físicos mantidos sem renderização.");
    }

    public static void AplicarMdHistoriaEmLote()
    {
        Scene cena = EditorSceneManager.OpenScene(CenaAlvo, OpenSceneMode.Single);
        if (!cena.IsValid())
        {
            Debug.LogError("[Md Historia] Não foi possível abrir a cena alvo em modo lote.");
            return;
        }

        AplicarBiomasEParedoes();
    }

    [MenuItem("Hegemonia/Mapa/Md Historia/Aplicar Sea e limites sem reconfigurar terrenos", priority = 29)]
    public static void AplicarSeaELimitesMdHistoria()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!ValidarCenaAtiva(cena))
        {
            return;
        }

        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Bounds limites = CalcularLimites(terrenos);
        if (limites.size == Vector3.zero)
        {
            Debug.LogError("[Md Historia] Não há terrenos ativos para dimensionar o Sea.");
            return;
        }

        CriarParedoes(limites);
        CriarSeaMdHistoria(terrenos, 0f);
        CriarRegistroMapa(limites);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Md Historia] Sea e limites aplicados sem reconfigurar TerrainData.");
    }

    public static void AplicarSeaELimitesMdHistoriaEmLote()
    {
        Scene cena = EditorSceneManager.OpenScene(CenaAlvo, OpenSceneMode.Single);
        if (!cena.IsValid())
        {
            Debug.LogError("[Md Historia] Não foi possível abrir a cena alvo em modo lote para aplicar o Sea.");
            return;
        }

        AplicarSeaELimitesMdHistoria();
    }

    [MenuItem("Hegemonia/Mapa/Md Historia/Validar biomas e limites", priority = 31)]
    public static void ValidarBiomasELimites()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!ValidarCenaAtiva(cena))
        {
            return;
        }

        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Transform limites = GameObject.Find(RaizLimites)?.transform;
        Transform extensoes = GameObject.Find(RaizExtensoes)?.transform;
        int aguas = 0;
        int paises = 0;
        int problemas = 0;

        foreach (Terrain terreno in terrenos)
        {
            Bioma esperado = DeterminarBioma(terreno.gameObject.name);
            bool agua = esperado == Bioma.Agua;
            bool temCamada = terreno.terrainData != null
                && terreno.terrainData.terrainLayers != null
                && terreno.terrainData.terrainLayers.Length == 1;
            if (agua)
            {
                aguas++;
            }
            else
            {
                paises++;
            }

            // Terrenos de água ficam sob o Sea.prefab e podem permanecer sem
            // TerrainLayer temática; a camada única é exigida apenas para
            // terrenos que realmente aparecem como terra.
            if (!agua && !temCamada)
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Terreno sem camada temática única: " + terreno.name);
            }

            TerrainCollider collider = terreno.GetComponent<TerrainCollider>();
            if (collider == null || collider.terrainData != terreno.terrainData)
            {
                problemas++;
                Debug.LogWarning("[Md Historia] TerrainCollider divergente do Terrain: " + terreno.name);
            }

            if (agua && terreno.terrainData != null && !AlturaUniforme(terreno.terrainData))
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Tile de água ainda possui relevo variável: " + terreno.name);
            }
        }

        string[] nomes = { "Norte", "Sul", "Leste", "Oeste" };
        foreach (string nome in nomes)
        {
            GameObject parede = GameObject.Find(PrefixoParede + nome);
            if (parede == null || parede.GetComponent<BoxCollider>() == null)
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Paredão ausente ou sem collider: " + nome);
            }
        }

        if (limites == null || limites.GetComponent<MdHistoriaMapaRuntime>() == null)
        {
            problemas++;
            Debug.LogWarning("[Md Historia] Registro de limites MdHistoriaMapaRuntime ausente.");
        }

        ValidarExtensoesFronteira(terrenos, extensoes, ref problemas);

        string resultado = problemas == 0 ? "OK" : "COM PROBLEMAS";
        Debug.Log("[Md Historia] Validação " + resultado
            + ": terrenos de países/fronteira/ilha=" + paises
            + ", tiles de água=" + aguas
            + ", zonas de expansão=" + (extensoes != null ? extensoes.GetComponentsInChildren<ZonaFronteiraExpansionavel>(true).Length : 0)
            + ", problemas=" + problemas + ".");
    }

    [MenuItem("Hegemonia/Mapa/Md Historia/Recriar extensoes de fronteira", priority = 32)]
    public static void RecriarExtensoesDeFronteira()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!ValidarCenaAtiva(cena)) return;

        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        CriarExtensoesFronteira(terrenos);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Debug.Log("[Md Historia] Extensões de fronteira recriadas. As parcelas começam livres e neutras.");
    }

    private static bool ValidarCenaAtiva(Scene cena)
    {
        if (!cena.IsValid() || !cena.isLoaded)
        {
            Debug.LogError("[Md Historia] A cena ativa não é válida ou não está carregada.");
            return false;
        }

        if (!string.Equals(cena.path, CenaAlvo, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[Md Historia] Abra primeiro a cena " + CenaAlvo
                + ". Nenhuma outra cena será alterada por esta ferramenta.");
            return false;
        }

        return true;
    }

    private static Bioma DeterminarBioma(string nome)
    {
        string normalizado = (nome ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizado == "pais1" || normalizado.Contains("pais1"))
        {
            return Bioma.Tropical;
        }
        if (normalizado == "pais2" || normalizado.Contains("pais2"))
        {
            return Bioma.Arido;
        }
        if (normalizado == "pais3" || normalizado.Contains("pais3"))
        {
            return Bioma.Neve;
        }
        if (normalizado == "pais4" || normalizado.Contains("pais4"))
        {
            return Bioma.Temperado;
        }
        if (normalizado == "pais5" || normalizado.Contains("pais5"))
        {
            return Bioma.Montanhoso;
        }
        if (normalizado == "pais6" || normalizado.Contains("pais6"))
        {
            return Bioma.Floresta;
        }
        if (normalizado.Contains("ilha"))
        {
            return Bioma.Tropical;
        }
        if (normalizado.Contains("fronteira"))
        {
            return Bioma.Fronteira;
        }

        // Tiles nomeados apenas como Terrain/coordenadas são água, conforme a regra do mapa.
        return Bioma.Agua;
    }

    private static void ConfigurarTerreno(Terrain terreno, Bioma bioma)
    {
        if (terreno == null || terreno.terrainData == null)
        {
            return;
        }

        TerrainData dados = DuplicarTerrainDataSeNecessario(terreno);
        if (dados == null)
        {
            return;
        }

        Texture2D textura = ObterTextura(bioma);
        TerrainLayer camada = ObterCamada(bioma, textura);
        dados.terrainLayers = new[] { camada };
        AplicarAlphamapUnico(dados);

        terreno.drawInstanced = true;
        terreno.heightmapPixelError = bioma == Bioma.Agua ? 60f : 35f;
        terreno.detailObjectDistance = bioma == Bioma.Agua ? 0f : 80f;
        terreno.treeDistance = bioma == Bioma.Agua ? 0f : 260f;

        if (bioma == Bioma.Agua)
        {
            NivelarParaAgua(terreno, dados, 0f);
        }

        SincronizarTerrainEColisor(terreno, dados);
    }

    private static void SincronizarTerrainEColisor(Terrain terreno, TerrainData dados)
    {
        // Atribuição normal mantém o objeto em memória correto.
        terreno.terrainData = dados;
        EditorUtility.SetDirty(terreno);

        TerrainCollider collider = terreno.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            return;
        }

        collider.terrainData = dados;

        // Unity pode conservar o valor antigo no YAML do componente quando o
        // TerrainData foi trocado durante a edição. Grave explicitamente o
        // campo serializado para manter visual e colisão idênticos após reload.
        SerializedObject serializedCollider = new SerializedObject(collider);
        SerializedProperty serializedData = serializedCollider.FindProperty("m_TerrainData");
        if (serializedData != null)
        {
            serializedData.objectReferenceValue = dados;
            serializedCollider.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(collider);
    }

    private static TerrainData DuplicarTerrainDataSeNecessario(Terrain terreno)
    {
        string origem = AssetDatabase.GetAssetPath(terreno.terrainData);
        if (string.IsNullOrEmpty(origem))
        {
            Debug.LogWarning("[Md Historia] TerrainData sem caminho de asset: " + terreno.name);
            return terreno.terrainData;
        }

        string id = IdentificadorSeguro(terreno.gameObject.name)
            + "_" + Mathf.RoundToInt(terreno.transform.position.x)
            + "_" + Mathf.RoundToInt(terreno.transform.position.z);
        string destino = PastaBase + "/TerrainData_" + id + ".asset";
        if (origem.Replace('\\', '/') != destino)
        {
            if (!File.Exists(destino))
            {
                AssetDatabase.CopyAsset(origem, destino);
                AssetDatabase.ImportAsset(destino);
            }

            TerrainData copia = AssetDatabase.LoadAssetAtPath<TerrainData>(destino);
            if (copia != null)
            {
                terreno.terrainData = copia;
                return copia;
            }
        }

        return terreno.terrainData;
    }

    private static void NivelarParaAgua(Terrain terreno, TerrainData dados, float nivelAgua)
    {
        int resolucao = dados.heightmapResolution;
        float valor = Mathf.Clamp01((nivelAgua - terreno.transform.position.y) / Mathf.Max(0.01f, dados.size.y));
        float[,] alturas = new float[resolucao, resolucao];
        for (int y = 0; y < resolucao; y++)
        {
            for (int x = 0; x < resolucao; x++)
            {
                alturas[y, x] = valor;
            }
        }

        dados.SetHeights(0, 0, alturas);
    }

    private static bool AlturaUniforme(TerrainData dados)
    {
        int resolucao = Mathf.Min(dados.heightmapResolution, 33);
        float referencia = dados.GetHeight(0, 0);
        for (int y = 0; y < resolucao; y++)
        {
            for (int x = 0; x < resolucao; x++)
            {
                float px = x / (float)Mathf.Max(1, resolucao - 1) * (dados.heightmapResolution - 1);
                float py = y / (float)Mathf.Max(1, resolucao - 1) * (dados.heightmapResolution - 1);
                if (Mathf.Abs(dados.GetHeight(Mathf.RoundToInt(px), Mathf.RoundToInt(py)) - referencia) > 0.05f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AplicarAlphamapUnico(TerrainData dados)
    {
        int largura = dados.alphamapWidth;
        int altura = dados.alphamapHeight;
        float[,,] alpha = new float[altura, largura, 1];
        for (int y = 0; y < altura; y++)
        {
            for (int x = 0; x < largura; x++)
            {
                alpha[y, x, 0] = 1f;
            }
        }

        dados.SetAlphamaps(0, 0, alpha);
    }

    private static Texture2D ObterTextura(Bioma bioma)
    {
        string nome = "MdHistoria_" + bioma;
        string caminho = PastaTexturas + "/" + nome + ".asset";
        Texture2D existente = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        if (existente != null)
        {
            return existente;
        }

        Paleta paleta = PaletaDoBioma(bioma);
        Texture2D textura = new Texture2D(64, 64, TextureFormat.RGBA32, true, false)
        {
            name = nome,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 2
        };

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float ruido = Mathf.PerlinNoise((x + (int)bioma * 17) * 0.11f, (y + 13) * 0.11f);
                float detalhe = Mathf.PerlinNoise((x + 73) * 0.42f, (y + (int)bioma * 23) * 0.42f) * 0.12f;
                Color cor = Color.Lerp(paleta.Escuro, paleta.Claro, Mathf.Clamp01(ruido * 0.82f + detalhe));
                textura.SetPixel(x, y, cor);
            }
        }

        textura.Apply(true, true);
        AssetDatabase.CreateAsset(textura, caminho);
        return textura;
    }

    private static TerrainLayer ObterCamada(Bioma bioma, Texture2D textura)
    {
        string caminho = PastaCamadas + "/MdHistoria_" + bioma + ".terrainlayer";
        TerrainLayer existente = AssetDatabase.LoadAssetAtPath<TerrainLayer>(caminho);
        if (existente != null)
        {
            existente.diffuseTexture = textura;
            existente.tileSize = new Vector2(96f, 96f);
            EditorUtility.SetDirty(existente);
            return existente;
        }

        TerrainLayer camada = new TerrainLayer
        {
            name = "MdHistoria_" + bioma,
            diffuseTexture = textura,
            tileSize = new Vector2(96f, 96f),
            tileOffset = Vector2.zero
        };
        AssetDatabase.CreateAsset(camada, caminho);
        return camada;
    }

    private static Paleta PaletaDoBioma(Bioma bioma)
    {
        switch (bioma)
        {
            case Bioma.Agua: return new Paleta(Cor("123B50"), Cor("2C7E8D"));
            case Bioma.Tropical: return new Paleta(Cor("3B713F"), Cor("92AD58"));
            case Bioma.Arido: return new Paleta(Cor("9A6B3C"), Cor("D3B477"));
            case Bioma.Neve: return new Paleta(Cor("AFC4D0"), Cor("F2F6F4"));
            case Bioma.Temperado: return new Paleta(Cor("506E3D"), Cor("A2AE62"));
            case Bioma.Montanhoso: return new Paleta(Cor("5C564E"), Cor("A99B83"));
            case Bioma.Floresta: return new Paleta(Cor("315A46"), Cor("7A9866"));
            default: return new Paleta(Cor("6E604C"), Cor("B49A70"));
        }
    }

    private static Color Cor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color cor);
        return cor;
    }

    private static Bounds CalcularLimites(IReadOnlyList<Terrain> terrenos)
    {
        Bounds limites = new Bounds();
        bool iniciou = false;
        foreach (Terrain terreno in terrenos)
        {
            if (terreno == null || terreno.terrainData == null)
            {
                continue;
            }

            Vector3 posicao = terreno.transform.position;
            Vector3 tamanho = terreno.terrainData.size;
            Bounds atual = new Bounds(posicao + tamanho * 0.5f, tamanho);
            if (!iniciou)
            {
                limites = atual;
                iniciou = true;
            }
            else
            {
                limites.Encapsulate(atual);
            }
        }

        return limites;
    }

    private static void CriarParedoes(Bounds limites)
    {
        GameObject raiz = GameObject.Find(RaizLimites);
        if (raiz == null)
        {
            raiz = new GameObject(RaizLimites);
        }

        List<GameObject> antigos = new List<GameObject>();
        for (int i = raiz.transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = raiz.transform.GetChild(i);
            if (filho.name.StartsWith(PrefixoParede, StringComparison.Ordinal))
            {
                antigos.Add(filho.gameObject);
            }
        }

        foreach (GameObject antigo in antigos)
        {
            UnityEngine.Object.DestroyImmediate(antigo);
        }

        Material material = ObterMaterialParede();
        float baseY = limites.min.y - 40f;
        float altura = Mathf.Max(420f, limites.size.y + 260f);
        float espessura = 140f;
        CriarParede(raiz.transform, "Norte", new Vector3(limites.center.x, baseY, limites.max.z), limites.size.x, espessura, altura, true, material);
        CriarParede(raiz.transform, "Sul", new Vector3(limites.center.x, baseY, limites.min.z), limites.size.x, espessura, altura, true, material);
        CriarParede(raiz.transform, "Leste", new Vector3(limites.max.x, baseY, limites.center.z), limites.size.z, espessura, altura, false, material);
        CriarParede(raiz.transform, "Oeste", new Vector3(limites.min.x, baseY, limites.center.z), limites.size.z, espessura, altura, false, material);
    }

    private static void CriarParede(Transform pai, string nome, Vector3 centro, float comprimento, float espessura, float altura, bool horizontal, Material material)
    {
        GameObject objeto = new GameObject(PrefixoParede + nome);
        objeto.transform.SetParent(pai, true);
        objeto.transform.position = centro;

        Mesh mesh = CriarMalhaMontanha(PrefixoParede + nome + "_Malha", comprimento, espessura, altura, horizontal);
        MeshFilter filtro = objeto.AddComponent<MeshFilter>();
        filtro.sharedMesh = mesh;
        MeshRenderer renderer = objeto.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        // O paredão continua com BoxCollider para bloquear a saída do mapa,
        // mas não fica visível na câmera de jogo/satélite.
        renderer.enabled = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;

        BoxCollider collider = objeto.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, altura * 0.5f, 0f);
        collider.size = horizontal
            ? new Vector3(comprimento, altura, espessura)
            : new Vector3(espessura, altura, comprimento);
    }

    private static Mesh CriarMalhaMontanha(string nome, float comprimento, float espessura, float altura, bool horizontal)
    {
        string caminho = PastaMalhas + "/" + nome + ".asset";
        Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
        if (existente != null)
        {
            return existente;
        }

        const int segmentos = 24;
        const int verticesPorLinha = segmentos + 1;
        Vector3[] vertices = new Vector3[verticesPorLinha * 4];
        int[] triangulos = new int[segmentos * 8 * 3];
        for (int i = 0; i <= segmentos; i++)
        {
            float t = i / (float)segmentos;
            float eixo = Mathf.Lerp(-comprimento * 0.5f, comprimento * 0.5f, t);
            float pico = altura * (0.62f + 0.24f * Mathf.PerlinNoise(t * 4.5f + 0.3f, horizontal ? 0.2f : 1.7f));
            int frente = i;
            int tras = verticesPorLinha + i;
            if (horizontal)
            {
                vertices[frente] = new Vector3(eixo, 0f, -espessura * 0.5f);
                vertices[tras] = new Vector3(eixo, 0f, espessura * 0.5f);
                vertices[verticesPorLinha * 2 + i] = new Vector3(eixo, pico, -espessura * 0.5f);
                vertices[verticesPorLinha * 3 + i] = new Vector3(eixo, pico, espessura * 0.5f);
            }
            else
            {
                vertices[frente] = new Vector3(-espessura * 0.5f, 0f, eixo);
                vertices[tras] = new Vector3(espessura * 0.5f, 0f, eixo);
                vertices[verticesPorLinha * 2 + i] = new Vector3(-espessura * 0.5f, pico, eixo);
                vertices[verticesPorLinha * 3 + i] = new Vector3(espessura * 0.5f, pico, eixo);
            }
        }

        int tri = 0;
        for (int i = 0; i < segmentos; i++)
        {
            int a = i;
            int b = i + 1;
            int c = verticesPorLinha + i;
            int d = verticesPorLinha + i + 1;
            int e = verticesPorLinha * 2 + i;
            int f = verticesPorLinha * 2 + i + 1;
            int g = verticesPorLinha * 3 + i;
            int h = verticesPorLinha * 3 + i + 1;
            AdicionarQuad(triangulos, ref tri, a, b, f, e);
            AdicionarQuad(triangulos, ref tri, d, c, g, h);
            AdicionarQuad(triangulos, ref tri, e, f, h, g);
            AdicionarQuad(triangulos, ref tri, a, e, g, c);
        }

        Mesh mesh = new Mesh { name = nome };
        mesh.vertices = vertices;
        mesh.triangles = triangulos;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, caminho);
        return mesh;
    }

    private static void AdicionarQuad(int[] triangulos, ref int indice, int a, int b, int c, int d)
    {
        triangulos[indice++] = a;
        triangulos[indice++] = b;
        triangulos[indice++] = c;
        triangulos[indice++] = a;
        triangulos[indice++] = c;
        triangulos[indice++] = d;
    }

    private static Material ObterMaterialParede()
    {
        string caminho = PastaBase + "/MdHistoria_Paredao.mat";
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (existente != null)
        {
            return existente;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "MdHistoria_Paredao" };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.19f, 0.22f, 0.22f, 1f));
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.19f, 0.22f, 0.22f, 1f));
        }
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    private static void CriarSeaMdHistoria(IReadOnlyList<Terrain> terrenos, float nivelAgua)
    {
        const string raizNome = "MdHistoria_SeaSystem";
        const string prefixoTile = "MdHistoria_SeaTile_";
        const string nomeAgua = "Agua";
        const string caminhoPrefab = "Assets/Mar_Feito/Models/Sea.prefab";
        const string caminhoMaterialOrigem = "Assets/Mar_Feito/Models/Sea/Sea.mat";
        const string caminhoMaterialOcean = "Assets/Mar_Feito/Models/Sea/Mar_Novo.mat";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);
        Material materialOrigem = AssetDatabase.LoadAssetAtPath<Material>(caminhoMaterialOrigem);
        Material materialOcean = AssetDatabase.LoadAssetAtPath<Material>(caminhoMaterialOcean);
        if (prefab == null || materialOrigem == null || materialOcean == null)
        {
            Debug.LogError("[Md Historia] Sea.prefab, Sea.mat ou Mar_Novo.mat não encontrado; água avançada não foi criada.");
            return;
        }

        GameObject raiz = GameObject.Find(raizNome);
        if (raiz == null)
        {
            raiz = new GameObject(raizNome);
        }

        for (int i = raiz.transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = raiz.transform.GetChild(i);
            if (filho != null && (filho.name.StartsWith(prefixoTile, StringComparison.Ordinal) || filho.name == nomeAgua))
            {
                UnityEngine.Object.DestroyImmediate(filho.gameObject);
            }
        }

        // A mesma topologia usada nas cenas de referência é um único objeto
        // Agua com o Sea.prefab escalado para a área navegável. Isso evita
        // emendas/gaps entre tiles e mantém a simulação OceanAdvanced em um
        // único controlador, sem criar uma cópia alterada de Sea.mat.
        OceanAdvanced controladorAntigo = raiz.GetComponent<OceanAdvanced>();
        if (controladorAntigo != null)
        {
            UnityEngine.Object.DestroyImmediate(controladorAntigo);
        }

        GameObject agua = PrefabUtility.InstantiatePrefab(prefab, raiz.transform) as GameObject;
        if (agua == null)
        {
            Debug.LogError("[Md Historia] Não foi possível instanciar o Sea.prefab.");
            return;
        }

        agua.name = nomeAgua;
        Bounds limites = CalcularLimites(terrenos);
        MeshFilter filtroModelo = agua.GetComponent<MeshFilter>();
        Mesh malha = filtroModelo != null ? filtroModelo.sharedMesh : null;
        Vector3 tamanhoMalha = malha != null ? malha.bounds.size : new Vector3(100f, 1f, 100f);
        float larguraMalha = Mathf.Max(1f, tamanhoMalha.x);
        float profundidadeMalha = Mathf.Max(1f, tamanhoMalha.z);
        agua.transform.position = new Vector3(limites.center.x, nivelAgua + 1.3f, limites.center.z);
        agua.transform.localScale = new Vector3(
            Mathf.Max(0.01f, limites.size.x / larguraMalha * 1.002f),
            1f,
            Mathf.Max(0.01f, limites.size.z / profundidadeMalha * 1.002f));

        MeshRenderer renderer = agua.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = materialOrigem;
            // Preserva as configurações do prefab de referência: o Sea recebe
            // sombras/reflexões normalmente e o shader cuida de transparência,
            // ondas, espuma e refração.
            EditorUtility.SetDirty(renderer);
        }

        MeshCollider collider = agua.GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = agua.AddComponent<MeshCollider>();
        }
        collider.sharedMesh = malha;
        collider.convex = false;

        // O Sea cobre visualmente toda a extensão do mapa, mas não deve ser
        // registrado como uma única área de Água: esse bounds global ficaria
        // acima dos Terrains e classificaria pontos de terra como água. Os
        // próprios tiles Terrain (mar/país/fronteira) são a fonte de verdade
        // da classificação usada pelo controlador naval.
        MarcadorSuperficieMapa marcador = agua.GetComponent<MarcadorSuperficieMapa>();
        if (marcador != null)
        {
            UnityEngine.Object.DestroyImmediate(marcador);
        }

        OceanAdvanced ocean = agua.GetComponent<OceanAdvanced>();
        if (ocean == null)
        {
            ocean = agua.AddComponent<OceanAdvanced>();
        }
        ocean.ocean = materialOcean;
        Light[] luzes = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < luzes.Length; i++)
        {
            if (luzes[i] != null && luzes[i].type == LightType.Directional)
            {
                ocean.sun = luzes[i];
                break;
            }
        }

        // Mantém o mesmo metadado de navegação naval usado pelo Agua em Demon
        // e Sena 19. Sem isso, uma reaplicação da ferramenta recriaria o Sea
        // visual/físico, mas deixaria a malha fora da área Agua do NavMesh.
        NavMeshModifier modificadorNaval = agua.GetComponent<NavMeshModifier>();
        if (modificadorNaval == null)
        {
            modificadorNaval = agua.AddComponent<NavMeshModifier>();
        }
        modificadorNaval.overrideArea = true;
        modificadorNaval.area = UnityEngine.AI.NavMesh.GetAreaFromName("Agua");
        modificadorNaval.overrideGenerateLinks = false;
        modificadorNaval.generateLinks = false;
        modificadorNaval.ignoreFromBuild = false;
        modificadorNaval.applyToChildren = true;

        EditorUtility.SetDirty(agua);
        Debug.Log("[Md Historia] Sea avançado configurado: objeto=Agua, material=Sea.mat, controlador=OceanAdvanced, oceano=Mar_Novo.mat.");
    }

    private static void CriarRegistroMapa(Bounds limites)
    {
        GameObject raiz = GameObject.Find(RaizLimites);
        MdHistoriaMapaRuntime registro = raiz.GetComponent<MdHistoriaMapaRuntime>();
        if (registro == null)
        {
            registro = raiz.AddComponent<MdHistoriaMapaRuntime>();
        }

        registro.Configurar(limites, 0f, Mathf.Max(420f, limites.size.y + 260f));
        EditorUtility.SetDirty(registro);
    }

    private static void CriarExtensoesFronteira(IReadOnlyList<Terrain> terrenos)
    {
        GameObject raizObjeto = GameObject.Find(RaizExtensoes);
        if (raizObjeto == null)
        {
            raizObjeto = new GameObject(RaizExtensoes);
        }

        GerenciadorExpansaoFronteira gerenciador = raizObjeto.GetComponent<GerenciadorExpansaoFronteira>();
        if (gerenciador == null)
        {
            gerenciador = raizObjeto.AddComponent<GerenciadorExpansaoFronteira>();
        }

        for (int i = raizObjeto.transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = raizObjeto.transform.GetChild(i);
            if (filho.name.StartsWith(PrefixoZona, StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(filho.gameObject);
            }
        }

        Material material = ObterMaterialExtensao();
        int indiceGlobal = 1;
        int indiceFronteira = 1;
        for (int i = 0; i < terrenos.Count; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || DeterminarBioma(terreno.gameObject.name) != Bioma.Fronteira) continue;

            Bounds limites = LimitesDoTerrain(terreno);
            float largura = Mathf.Clamp(limites.size.x * 0.28f, 600f, 2200f);
            float profundidade = Mathf.Clamp(limites.size.z * 0.28f, 600f, 2200f);
            float margemX = limites.size.x * 0.25f;
            float margemZ = limites.size.z * 0.25f;
            Vector2[] fatores =
            {
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f),
                new Vector2(-1f, 1f),
                new Vector2(1f, 1f)
            };

            for (int parcela = 0; parcela < fatores.Length; parcela++)
            {
                Vector3 centro = new Vector3(
                    limites.center.x + fatores[parcela].x * margemX,
                    SampleTerrainHeight(terreno, new Vector3(limites.center.x, 0f, limites.center.z)),
                    limites.center.z + fatores[parcela].y * margemZ);
                centro.y = SampleTerrainHeight(terreno, centro) + 0.08f;

                GameObject zonaObjeto = new GameObject(PrefixoZona + indiceGlobal.ToString("00"));
                zonaObjeto.transform.SetParent(raizObjeto.transform, true);
                zonaObjeto.transform.position = centro;

                ZonaFronteiraExpansionavel zona = zonaObjeto.AddComponent<ZonaFronteiraExpansionavel>();
                string id = "fronteira.expansao." + indiceGlobal.ToString("00");
                string nome = "Expansão neutra " + indiceGlobal.ToString("00") + " - fronteira " + indiceFronteira.ToString("00");
                zona.ConfigurarEditor(id, nome, new Vector2(largura, profundidade));

                LineRenderer linha = zonaObjeto.AddComponent<LineRenderer>();
                linha.useWorldSpace = false;
                linha.loop = true;
                linha.positionCount = 5;
                linha.startWidth = 8f;
                linha.endWidth = 8f;
                linha.numCapVertices = 2;
                linha.numCornerVertices = 2;
                linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                linha.receiveShadows = false;
                linha.sharedMaterial = material;
                linha.SetPosition(0, new Vector3(-largura * 0.5f, 0f, -profundidade * 0.5f));
                linha.SetPosition(1, new Vector3(-largura * 0.5f, 0f, profundidade * 0.5f));
                linha.SetPosition(2, new Vector3(largura * 0.5f, 0f, profundidade * 0.5f));
                linha.SetPosition(3, new Vector3(largura * 0.5f, 0f, -profundidade * 0.5f));
                linha.SetPosition(4, new Vector3(-largura * 0.5f, 0f, -profundidade * 0.5f));
                zona.AtualizarVisual();

                BoxCollider area = zonaObjeto.AddComponent<BoxCollider>();
                area.isTrigger = true;
                area.center = new Vector3(0f, 0f, 0f);
                area.size = new Vector3(largura, 2f, profundidade);
                EditorUtility.SetDirty(zonaObjeto);
                indiceGlobal++;
            }

            indiceFronteira++;
        }

        gerenciador.ReconstruirCache();
        EditorUtility.SetDirty(gerenciador);
        EditorUtility.SetDirty(raizObjeto);
    }

    private static void ValidarExtensoesFronteira(IReadOnlyList<Terrain> terrenos, Transform raiz, ref int problemas)
    {
        if (raiz == null)
        {
            problemas++;
            Debug.LogWarning("[Md Historia] Raiz de extensões de fronteira ausente.");
            return;
        }

        GerenciadorExpansaoFronteira gerenciador = raiz.GetComponent<GerenciadorExpansaoFronteira>();
        if (gerenciador == null)
        {
            problemas++;
            Debug.LogWarning("[Md Historia] GerenciadorExpansaoFronteira ausente.");
        }

        ZonaFronteiraExpansionavel[] zonas = raiz.GetComponentsInChildren<ZonaFronteiraExpansionavel>(true);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int fronteiras = 0;
        for (int i = 0; i < terrenos.Count; i++)
        {
            if (terrenos[i] != null && DeterminarBioma(terrenos[i].gameObject.name) == Bioma.Fronteira) fronteiras++;
        }

        if (zonas.Length != fronteiras * 4)
        {
            problemas++;
            Debug.LogWarning("[Md Historia] Número inesperado de zonas: " + zonas.Length + "; esperado=" + (fronteiras * 4) + ".");
        }

        for (int i = 0; i < zonas.Length; i++)
        {
            ZonaFronteiraExpansionavel zona = zonas[i];
            if (zona == null || string.IsNullOrWhiteSpace(zona.IdZona) || !ids.Add(zona.IdZona))
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Zona de fronteira ausente ou ID duplicado.");
                continue;
            }

            if (zona.GetComponent<LineRenderer>() == null || zona.GetComponent<BoxCollider>() == null)
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Zona sem linha visual ou área de interação: " + zona.name);
            }

            bool dentroDeFronteira = false;
            for (int t = 0; t < terrenos.Count; t++)
            {
                Terrain terreno = terrenos[t];
                if (terreno == null || DeterminarBioma(terreno.gameObject.name) != Bioma.Fronteira) continue;
                if (LimitesDoTerrain(terreno).Contains(zona.transform.position))
                {
                    dentroDeFronteira = true;
                    break;
                }
            }

            if (!dentroDeFronteira)
            {
                problemas++;
                Debug.LogWarning("[Md Historia] Zona fora de terreno de fronteira: " + zona.name);
            }
        }
    }

    private static Bounds LimitesDoTerrain(Terrain terreno)
    {
        Vector3 tamanho = terreno.terrainData != null ? terreno.terrainData.size : Vector3.zero;
        return new Bounds(terreno.transform.position + tamanho * 0.5f, tamanho);
    }

    private static float SampleTerrainHeight(Terrain terreno, Vector3 mundo)
    {
        return terreno.transform.position.y + terreno.SampleHeight(mundo);
    }

    private static Material ObterMaterialExtensao()
    {
        string caminho = PastaBase + "/MdHistoria_FronteiraExpansao.mat";
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (existente != null) return existente;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { name = "MdHistoria_FronteiraExpansao" };
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.1f, 0.9f, 0.9f, 0.9f));
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.9f, 0.9f));
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    private static void GarantirPastas()
    {
        CriarPastaSeNecessario("Assets", "MapThemes");
        CriarPastaSeNecessario("Assets/MapThemes", "MdHistoria");
        CriarPastaSeNecessario(PastaBase, "Texturas");
        CriarPastaSeNecessario(PastaBase, "Camadas");
        CriarPastaSeNecessario(PastaBase, "Malhas");
    }

    private static void CriarPastaSeNecessario(string pai, string nome)
    {
        string caminho = pai + "/" + nome;
        if (!AssetDatabase.IsValidFolder(caminho))
        {
            AssetDatabase.CreateFolder(pai, nome);
        }
    }

    private static string IdentificadorSeguro(string valor)
    {
        string resultado = string.IsNullOrEmpty(valor) ? "Terrain" : valor;
        foreach (char invalido in Path.GetInvalidFileNameChars())
        {
            resultado = resultado.Replace(invalido, '_');
        }

        return resultado.Replace(' ', '_').Replace('(', '_').Replace(')', '_').Replace(',', '_').Replace('.', '_');
    }

}
#endif
