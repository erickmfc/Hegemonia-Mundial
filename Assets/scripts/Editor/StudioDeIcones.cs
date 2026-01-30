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

        EditorGUILayout.HelpBox("Isso irá scanear todas as fichas de construção (scripts DadosConstrucao) e gerar novos ícones para elas baseados nos seus prefabs.", MessageType.Info);
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

            if (dados != null && dados.prefabDaUnidade != null)
            {
                EditorUtility.DisplayProgressBar("Gerando Ícones", $"Renderizando {dados.nomeItem}...", (float)i / total);
                
                Sprite novoIcone = GerarIcone(dados.prefabDaUnidade, dados.name);
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
        GameObject sujeito = Instantiate(prefab, Vector3.zero, Quaternion.identity, studio.transform);
        
        // Normaliza Rotação do Sujeito
        sujeito.transform.rotation = Quaternion.Euler(0, -45, 0); // Ajuste fino para isométrico parecer frente

        // 2. Setup Limpo das Layers e Renderers
        LimparObjeto(sujeito);

        // 3. Calcula Bounds
        Bounds bounds = CalcularBounds(sujeito);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

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
        AssetDatabase.Refresh(); // Importa o novo arquivo

        TextureImporter importer = AssetImporter.GetAtPath(pathRelativo) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pathRelativo);
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
