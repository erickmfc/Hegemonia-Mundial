using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds Terrain-compatible tree prefabs with a real source mesh nearby and
/// a small, authored-from-the-same-family foliage impostor at distance. The
/// impostor texture is generated deterministically from simple shapes; it is
/// not an AI image and does not modify the source environment prefabs.
/// </summary>
public static class GlobalTreeLodBuilder
{
    private const string RootFolder = "Assets/Worlds/GlobalMap/Vegetation";

    public static GameObject[] BuildTreePrefabs(GameObject[] sources)
    {
        if (sources == null || sources.Length == 0)
            throw new ArgumentException("A lista de árvores de origem está vazia.", nameof(sources));

        EnsureFolder("Assets/Worlds/GlobalMap");
        EnsureFolder(RootFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            throw new InvalidOperationException("O shader URP/Lit não está disponível para os impostores de árvore.");

        GameObject[] generated = new GameObject[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            GameObject source = sources[i];
            if (source == null)
                throw new InvalidOperationException("Há um prefab de árvore de origem ausente no índice " + i + ".");
            generated[i] = BuildTreePrefab(source, i, shader);
        }

        AssetDatabase.SaveAssets();
        return generated;
    }

    private static GameObject BuildTreePrefab(GameObject source, int variant, Shader shader)
    {
        string prefix = RootFolder + "/GlobalTreeLod_" + Sanitize(source.name);
        string texturePath = prefix + "_Impostor.png";
        string materialPath = prefix + "_Impostor.mat";
        string meshPath = prefix + "_Impostor.asset";
        string prefabPath = prefix + ".prefab";

        Texture2D texture = CreateFoliageTexture(texturePath, variant);
        Material material = CreateImpostorMaterial(materialPath, shader, texture);
        Bounds sourceBounds = CalculateLocalBounds(source);
        Mesh mesh = CreateImpostorMesh(meshPath, sourceBounds);

        GameObject root = new GameObject("GlobalTreeLod_" + source.name);
        try
        {
            GameObject nearModel = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (nearModel == null)
                throw new InvalidOperationException("Não foi possível instanciar o prefab de árvore " + source.name + ".");
            nearModel.name = "LOD0_Original";
            nearModel.transform.SetParent(root.transform, false);
            nearModel.transform.localPosition = Vector3.zero;
            nearModel.transform.localRotation = Quaternion.identity;
            nearModel.transform.localScale = Vector3.one;
            Renderer[] nearRenderers = nearModel.GetComponentsInChildren<Renderer>(true);
            if (nearRenderers.Length == 0)
                throw new InvalidOperationException("O prefab " + source.name + " não possui Renderer para LOD0.");

            GameObject farModel = new GameObject("LOD1_Impostor", typeof(MeshFilter), typeof(MeshRenderer));
            farModel.transform.SetParent(root.transform, false);
            MeshFilter meshFilter = farModel.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer = farModel.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            LODGroup group = root.AddComponent<LODGroup>();
            group.localReferencePoint = sourceBounds.center;
            group.size = Mathf.Max(sourceBounds.size.x, sourceBounds.size.y, sourceBounds.size.z);
            group.fadeMode = LODFadeMode.CrossFade;
            group.SetLODs(new[]
            {
                new LOD(0.035f, nearRenderers),
                new LOD(0.0015f, new Renderer[] { meshRenderer })
            });
            group.RecalculateBounds();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (saved == null)
                throw new IOException("O Unity não conseguiu salvar o prefab LOD " + prefabPath + ".");
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Texture2D CreateFoliageTexture(string path, int variant)
    {
        const int size = 256;
        Color[] pixels = new Color[size * size];
        float seed = variant * 17.31f + 0.47f;
        Color[] palettes =
        {
            new Color(0.28f, 0.38f, 0.16f, 1f),
            new Color(0.25f, 0.34f, 0.14f, 1f),
            new Color(0.31f, 0.39f, 0.18f, 1f),
            new Color(0.24f, 0.33f, 0.15f, 1f),
            new Color(0.29f, 0.37f, 0.16f, 1f),
            new Color(0.27f, 0.36f, 0.15f, 1f),
            new Color(0.32f, 0.39f, 0.19f, 1f)
        };
        Color foliage = palettes[variant % palettes.Length];

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float nearestEllipse = 10f;
                for (int cluster = 0; cluster < 9; cluster++)
                {
                    float angle = cluster * 2.3999632f + seed * 0.13f;
                    float ring = cluster == 0 ? 0f : 0.105f + 0.025f * (cluster % 3);
                    float cx = 0.5f + Mathf.Cos(angle) * ring;
                    float cy = 0.64f + Mathf.Sin(angle) * ring * 0.72f;
                    float rx = cluster == 0 ? 0.205f : 0.142f + 0.008f * ((cluster + variant) % 3);
                    float ry = cluster == 0 ? 0.29f : 0.17f + 0.012f * ((cluster + variant) % 2);
                    float dx = (u - cx) / rx;
                    float dy = (v - cy) / ry;
                    nearestEllipse = Mathf.Min(nearestEllipse, Mathf.Sqrt(dx * dx + dy * dy));
                }

                float edgeNoise = (Mathf.PerlinNoise(u * 37f + seed, v * 41f - seed) - 0.5f) * 0.14f;
                bool trunk = v < 0.48f && Mathf.Abs(u - 0.5f) < Mathf.Lerp(0.012f, 0.022f, v / 0.48f);
                float silhouette = trunk ? -1f : nearestEllipse - 1f - edgeNoise;
                if (silhouette > 0.04f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float broad = Mathf.PerlinNoise(u * 11f + seed * 2f, v * 13f + seed);
                float leaf = Mathf.PerlinNoise(u * 71f - seed, v * 67f + seed * 3f);
                float mottling = Mathf.Lerp(0.72f, 1.19f, broad) * Mathf.Lerp(0.90f, 1.08f, leaf);
                Color color = trunk
                    ? new Color(0.24f, 0.19f, 0.12f, 1f) * Mathf.Lerp(0.78f, 1.08f, broad)
                    : foliage * mottling;
                float softEdge = 1f - Mathf.SmoothStep(0.01f, 0.04f, silhouette);
                color.a = trunk ? 1f : Mathf.Max(0.72f, softEdge);
                pixels[y * size + x] = color;
            }
        }

        Texture2D generated = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
        {
            name = Path.GetFileNameWithoutExtension(path),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        generated.SetPixels(pixels);
        generated.Apply(true, false);
        File.WriteAllBytes(path, generated.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(generated);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("A textura do impostor não foi importada: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = size;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new InvalidOperationException("A textura do impostor não pode ser recarregada: " + path);
        return texture;
    }

    private static Material CreateImpostorMaterial(string path, Shader shader, Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.35f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.enableInstancing = true;
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh CreateImpostorMesh(string path, Bounds bounds)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Mesh generated = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        float halfWidth = Mathf.Max(bounds.size.x, bounds.size.z) * 0.56f;
        float bottom = bounds.min.y;
        float top = bounds.max.y;
        Vector3 center = bounds.center;
        Vector3[] vertices = new Vector3[8];
        Vector2[] uv = new Vector2[8];

        AddPlane(vertices, uv, 0, center.x - halfWidth, center.x + halfWidth, center.x, center.z, bottom, top, false);
        AddPlane(vertices, uv, 4, center.x - halfWidth, center.x + halfWidth, center.x, center.z, bottom, top, true);
        generated.vertices = vertices;
        generated.uv = uv;
        generated.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        generated.RecalculateNormals();
        generated.RecalculateBounds();

        if (mesh == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            mesh = generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, mesh);
            mesh.name = generated.name;
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    private static void AddPlane(
        Vector3[] vertices,
        Vector2[] uv,
        int start,
        float left,
        float right,
        float centerX,
        float z,
        float bottom,
        float top,
        bool diagonal)
    {
        Vector3 bl = new Vector3(left, bottom, z);
        Vector3 br = new Vector3(right, bottom, z);
        Vector3 tr = new Vector3(right, top, z);
        Vector3 tl = new Vector3(left, top, z);
        if (diagonal)
        {
            const float sin = 0.70710678f;
            Vector3 center = new Vector3(centerX, 0f, z);
            Vector3 axis = new Vector3(sin, 0f, sin) * ((right - left) * 0.5f);
            bl = center - axis + Vector3.up * bottom;
            br = center + axis + Vector3.up * bottom;
            tr = center + axis + Vector3.up * top;
            tl = center - axis + Vector3.up * top;
        }

        vertices[start] = bl;
        vertices[start + 1] = br;
        vertices[start + 2] = tr;
        vertices[start + 3] = tl;
        uv[start] = new Vector2(0f, 0f);
        uv[start + 1] = new Vector2(1f, 0f);
        uv[start + 2] = new Vector2(1f, 1f);
        uv[start + 3] = new Vector2(0f, 1f);
    }

    private static Bounds CalculateLocalBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        Matrix4x4 rootFromWorld = prefab.transform.worldToLocalMatrix;
        foreach (Renderer renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 rendererToRoot = rootFromWorld * renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 transformed = rendererToRoot.MultiplyPoint3x4(point);
                if (!hasBounds)
                {
                    result = new Bounds(transformed, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(transformed);
                }
            }
        }

        if (!hasBounds || result.size.y < 0.1f)
            throw new InvalidOperationException("O prefab não possui limites de malha válidos: " + prefab.name);
        return result;
    }

    private static string Sanitize(string value)
    {
        return value.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            throw new DirectoryNotFoundException("A pasta pai dos LODs ainda não existe: " + parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
