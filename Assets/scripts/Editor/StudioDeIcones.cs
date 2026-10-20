using UnityEngine;
using UnityEditor;
using System.IO;

public class StudioDeIcones : EditorWindow
{
    [MenuItem("Hegemonia/Studio de Icones")]
    public static void MostrarJanela()
    {
        GetWindow<StudioDeIcones>("Studio de Icones");
    }

    public string pastaSaida = "Assets/Art/IconesGerados";
    public Color corFundo = new Color(0, 0, 0, 0); // Transparente
    
    // Configurações de Câmera
    public Vector3 anguloCamera = new Vector3(30, 45, 0); // Isométrico Clássico
    public float zoomMultiplier = 0.8f;
    public int resolucao = 512;

    void OnGUI()
    {
        GUILayout.Label("Gerador de Ícones Premium", EditorStyles.boldLabel);

        pastaSaida = EditorGUILayout.TextField("Pasta de Saída", pastaSaida);
        corFundo = EditorGUILayout.ColorField("Cor de Fundo", corFundo);
        anguloCamera = EditorGUILayout.Vector3Field("Ângulo Câmera", anguloCamera);
        zoomMultiplier = EditorGUILayout.Slider("Zoom (0.5 - 2.0)", zoomMultiplier, 0.5f, 2.0f);
        resolucao = EditorGUILayout.IntSlider("Resolução", resolucao, 128, 1024);

        EditorGUILayout.Space();

        if (GUILayout.Button("Gerar Ícones para TODO O CATÁLOGO"))
        {
            GerarTodos();
        }

        if (GUILayout.Button("Preencher somente fotos ausentes"))
        {
            GerarIconesFaltantesCatalogo();
        }

        EditorGUILayout.HelpBox("O preenchimento seguro mantém os ícones existentes e renderiza apenas as fichas sem foto a partir do prefab.", MessageType.Info);
    }

    [MenuItem("Hegemonia/Studio de Icones/Preencher somente fotos ausentes")]
    public static void GerarIconesFaltantesCatalogo()
    {
        // Primeiro associa PNGs que já foram renderizados em um passe anterior.
        AssociarFotosJaGeradas();
        StudioDeIcones studio = CreateInstance<StudioDeIcones>();
        try
        {
            studio.GerarIconesFaltantes();
        }
        finally
        {
            DestroyImmediate(studio);
        }
    }

    [MenuItem("Hegemonia/Studio de Icones/Associar fotos já geradas")]
    public static void AssociarFotosJaGeradas()
    {
        string pasta = "Assets/Art/IconesGerados";
        int associados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:DadosConstrucao"))
        {
            string fichaPath = AssetDatabase.GUIDToAssetPath(guid);
            DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(fichaPath);
            if (dados == null || guid.Length < 8) continue;

            string nome = SanitizarNomeArquivo(dados.name) + "_" + guid.Substring(0, 8) + "_Icon.png";
            string spritePath = pasta + "/" + nome;
            if (!File.Exists(Path.Combine(Application.dataPath, spritePath.Substring("Assets/".Length)))) continue;

            ImportAssetOptions importacaoSincrona = ImportAssetOptions.ForceUpdate
                | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.ImportAsset(spritePath, importacaoSincrona);
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Sprite sprite = CarregarSpriteImportado(spritePath);
            if (sprite == null || dados.icone == sprite) continue;
            dados.icone = sprite;
            EditorUtility.SetDirty(dados);
            associados++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[StudioDeIcones] Fotos associadas: " + associados + ".");
    }

    private void GerarIconesFaltantes()
    {
        if (!Directory.Exists(pastaSaida))
        {
            Directory.CreateDirectory(pastaSaida);
        }

        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int gerados = 0;
        int ignorados = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
                if (dados == null || (dados.icone != null
                    && !dados.icone.name.ToLowerInvariant().Contains("bkwv7tbkwv7tbkwv")))
                {
                    ignorados++;
                    continue;
                }

                GameObject prefab;
                if (!dados.TryGetPrefabBasico(out prefab) || prefab == null)
                {
                    ignorados++;
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Preenchendo fotos do menu",
                    "Renderizando " + dados.GetDisplayName(),
                    (float)i / Mathf.Max(1, guids.Length));

                string assetGuid = AssetDatabase.AssetPathToGUID(path);
                string nomeArquivo = SanitizarNomeArquivo(dados.name);
                if (assetGuid.Length >= 8)
                {
                    nomeArquivo += "_" + assetGuid.Substring(0, 8);
                }

                // Reaproveita uma imagem já renderizada nesta pasta. Isso
                // permite concluir a associação mesmo quando o primeiro
                // passe terminou enquanto o AssetDatabase ainda importava o PNG.
                string caminhoIcone = pastaSaida + "/" + nomeArquivo + "_Icon.png";
                if (File.Exists(Path.Combine(Application.dataPath, caminhoIcone.Replace("Assets/", ""))))
                {
                    AssetDatabase.ImportAsset(caminhoIcone, ImportAssetOptions.ForceSynchronousImport);
                    TextureImporter importerExistente = AssetImporter.GetAtPath(caminhoIcone) as TextureImporter;
                    if (importerExistente != null)
                    {
                        importerExistente.textureType = TextureImporterType.Sprite;
                        importerExistente.spriteImportMode = SpriteImportMode.Single;
                        importerExistente.alphaIsTransparency = true;
                        importerExistente.SaveAndReimport();
                    }
                    Sprite existente = CarregarSpriteImportado(caminhoIcone);
                    if (existente != null)
                    {
                        dados.icone = existente;
                        EditorUtility.SetDirty(dados);
                        gerados++;
                        continue;
                    }
                }

                try
                {
                    Sprite novoIcone = GerarIcone(prefab, nomeArquivo);
                    if (novoIcone != null)
                    {
                        dados.icone = novoIcone;
                        EditorUtility.SetDirty(dados);
                        gerados++;
                    }
                    else
                    {
                        ignorados++;
                    }
                }
                catch (System.Exception ex)
                {
                    ignorados++;
                    Debug.LogWarning("[StudioDeIcones] Não foi possível gerar a foto de "
                        + dados.GetDisplayName() + ": " + ex, dados);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StudioDeIcones] Fotos novas: " + gerados + "; fichas preservadas ou ignoradas: " + ignorados + ".");
    }

    private static string SanitizarNomeArquivo(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "FichaConstrucao";
        }

        foreach (char invalido in Path.GetInvalidFileNameChars())
        {
            nome = nome.Replace(invalido, '_');
        }

        return nome.Replace('/', '_').Replace('\\', '_').Trim();
    }

    private static Sprite CarregarSpriteImportado(string caminho)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (sprite != null)
        {
            return sprite;
        }

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(caminho);
        for (int i = 0; i < subAssets.Length; i++)
        {
            sprite = subAssets[i] as Sprite;
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    void GerarTodos()
    {
        if (!Directory.Exists(pastaSaida))
        {
            Directory.CreateDirectory(pastaSaida);
        }

        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int total = guids.Length;

        for (int i = 0; i < total; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);

            if (dados != null && dados.PrefabDaUnidade != null)
            {
            EditorUtility.DisplayProgressBar("Gerando Ícones", $"Renderizando {dados.NomeItem}...", (float)i / total);
                
                Sprite novoIcone = GerarIcone(dados.PrefabDaUnidade, dados.name);
                if (novoIcone != null)
                {
                    dados.icone = novoIcone;
                    EditorUtility.SetDirty(dados);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
        Debug.Log($"<color=green>Sucesso!</color> Ícones gerados para {total} itens.");
    }

    // Método Estático para ser usado pelo Criador de Dados
    public static Sprite GerarIconeEstatico(GameObject prefab, string nomeBase)
    {
        var instancia = ScriptableObject.CreateInstance<StudioDeIcones>();
        return instancia.GerarIcone(prefab, nomeBase);
    }

    public Sprite GerarIcone(GameObject prefab, string nomeArquivo)
    {
        if (prefab == null) return null;

        // 1. Setup Studio Scene (Invisible)
        GameObject studio = new GameObject("Icon_Studio");
        GameObject sujeito = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (sujeito == null)
        {
            sujeito = UnityEngine.Object.Instantiate<GameObject>(prefab);
        }

        if (sujeito == null)
        {
            DestroyImmediate(studio);
            Debug.LogWarning("[StudioDeIcones] Não foi possível instanciar o prefab: " + prefab.name, prefab);
            return null;
        }

        sujeito.transform.SetParent(studio.transform, false);
        sujeito.transform.localPosition = Vector3.zero;
        sujeito.transform.localRotation = Quaternion.identity;
        
        // Normaliza Rotação do Sujeito
        sujeito.transform.rotation = Quaternion.Euler(0, -45, 0); // Ajuste fino para isométrico parecer frente

        // 2. Setup Limpo das Layers e Renderers
        LimparObjeto(sujeito);

        // 3. Calcula Bounds
        Bounds bounds = CalcularBounds(sujeito);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim < 0.001f)
        {
            DestroyImmediate(studio);
            Debug.LogWarning("[StudioDeIcones] Prefab sem Renderer visível: " + prefab.name, prefab);
            return null;
        }

        // 4. Setup Camera
        Camera cam = new GameObject("Cam").AddComponent<Camera>();
        cam.transform.SetParent(studio.transform);
        cam.clearFlags = CameraClearFlags.Color; // Usa cor sólida (que pode ser transparente)
        cam.backgroundColor = corFundo; // Define o fundo (transparente por padrão)
        cam.orthographic = true;
        cam.orthographicSize = (maxDim / 2f) / zoomMultiplier;
        
        // Posiciona Câmera Isométrica
        Quaternion rot = Quaternion.Euler(anguloCamera);
        Vector3 dir = rot * Vector3.forward;
        cam.transform.position = bounds.center - (dir * 50f);
        cam.transform.rotation = rot;

        // 5. Setup Iluminação "Premium"
        CriarLuz(studio.transform, new Vector3(50, 100, -50), 1.3f, new Color(1f, 0.98f, 0.9f)); // Key Light Quente
        CriarLuz(studio.transform, new Vector3(-50, 20, -50), 0.5f, new Color(0.4f, 0.5f, 0.7f)); // Fill Light Fria
        CriarLuz(studio.transform, new Vector3(0, -50, 50), 0.5f, new Color(1f, 1f, 1f));     // Rim Light (Back)

        // 6. Render
        RenderTexture rt = RenderTexture.GetTemporary(resolucao, resolucao, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        // 7. Salva Textura Pura (com Alpha correto)
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, resolucao, resolucao), 0, 0);
        tex.Apply(); // Essencial para salvar

        // Se a cor de fundo for transparente, garantimos que o alpha do fundo seja 0
        // (A câmera já faz isso se clearFlags=SolidColor e backgroundColor.a = 0)

        byte[] bytes = tex.EncodeToPNG();
        string pathRelativo = $"{pastaSaida}/{nomeArquivo}_Icon.png";
        string fullPath = Path.Combine(Application.dataPath, pathRelativo.Replace("Assets/", ""));

        // Garante diretório
        string dirPath = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

        File.WriteAllBytes(fullPath, bytes);

        // Cleanup
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(studio);

        // 8. Import e Configuração
        // Importa de forma síncrona para que o Sprite esteja disponível antes
        // de atribuí-lo à ficha. Um ImportAsset isolado não descobre um PNG
        // recém-criado; o Refresh precisa acontecer primeiro.
        ImportAssetOptions importacaoSincrona = ImportAssetOptions.ForceUpdate
            | ImportAssetOptions.ForceSynchronousImport;
        AssetDatabase.Refresh(importacaoSincrona);
        AssetDatabase.ImportAsset(pathRelativo, importacaoSincrona);

        TextureImporter importer = AssetImporter.GetAtPath(pathRelativo) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }

        Sprite sprite = CarregarSpriteImportado(pathRelativo);
        if (sprite == null)
        {
            // Releitura defensiva para instalações em que o primeiro ciclo de
            // importação ainda não publicou o sub-asset Sprite.
            AssetDatabase.Refresh(importacaoSincrona);
            AssetDatabase.ImportAsset(pathRelativo, importacaoSincrona);
            sprite = CarregarSpriteImportado(pathRelativo);
        }

        return sprite;
    }

    void LimparObjeto(GameObject obj)
    {
        // Remove scripts para evitar erros durante renderização
        foreach (var c in obj.GetComponentsInChildren<MonoBehaviour>()) DestroyImmediate(c);
        // Desativa Canvas flutuantes (HP Bars)
        foreach (var c in obj.GetComponentsInChildren<Canvas>()) c.gameObject.SetActive(false);
    }

    Bounds CalcularBounds(GameObject obj)
    {
        Bounds b = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        bool first = true;
        foreach (var r in rends)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void CriarLuz(Transform parent, Vector3 pos, float startIntensity, Color col)
    {
        GameObject l = new GameObject("Light");
        l.transform.parent = parent;
        l.transform.position = pos;
        l.transform.LookAt(Vector3.zero);
        Light light = l.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = startIntensity;
        light.color = col;
    }
}
