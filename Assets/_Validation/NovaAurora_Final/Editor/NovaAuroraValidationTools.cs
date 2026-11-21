using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class NovaAuroraValidationTools
{
    private const string CandidatePath = "Assets/_Validation/NovaAurora_Final/Nova_Aurora_Final_Texturizada.glb";
    private const string ProductionPrefabPath = "Assets/Library/Prefabs/Estaleiro Marinho/Cidade Nova Aurora.prefab";
    private const string PreviewScenePath = "Assets/_Validation/NovaAurora_Final/NovaAurora_Preview.unity";

    [MenuItem("Tools/Hegemonia/Nova Aurora/Criar cena isolada de validação")]
    private static void CreatePreviewScene()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath);
        if (model == null)
        {
            Debug.LogError("GLB de validação ainda não foi importado: " + CandidatePath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);
        GameObject city = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (city == null)
        {
            Debug.LogError("Unity não conseguiu instanciar o GLB de validação.");
            return;
        }

        city.name = "Cidade Nova Aurora (Validação)";
        city.transform.localPosition = new Vector3(-864.79004f, 0f, 1429.0712f);
        city.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        city.transform.localScale = new Vector3(150f, 170f, 200f);
        city.AddComponent<NovaAuroraPredialLighting>();

        Bounds bounds = GetRendererBounds(city);
        float span = Mathf.Max(bounds.size.x, bounds.size.z);
        GameObject cameraObject = new GameObject("Câmera de validação Nova Aurora");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = Mathf.Max(10000f, span * 4f);
        camera.transform.position = bounds.center + new Vector3(span * 0.85f, span * 0.72f, -span * 1.1f);
        camera.transform.LookAt(bounds.center);
        camera.tag = "MainCamera";

        GameObject lightObject = new GameObject("Sol de validação Nova Aurora");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        Light sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.68f, 0.79f, 0.96f, 1f);
        sun.intensity = 0.58f;
        RenderSettings.sun = sun;

        GameObject cycleObject = new GameObject("CicloDiaNoite de validação (23h)");
        SceneManager.MoveGameObjectToScene(cycleObject, scene);
        CicloDiaNoite cycle = cycleObject.AddComponent<CicloDiaNoite>();
        SerializedObject serializedCycle = new SerializedObject(cycle);
        serializedCycle.FindProperty("luzPrincipal").objectReferenceValue = sun;
        serializedCycle.FindProperty("modo").enumValueIndex = 3;
        serializedCycle.FindProperty("horaAtual").floatValue = 23f;
        serializedCycle.FindProperty("horaFixaNoite").floatValue = 23f;
        serializedCycle.ApplyModifiedPropertiesWithoutUndo();

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.LookAt(bounds.center, Quaternion.Euler(28f, 42f, 0f), span * 1.35f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
        {
            Debug.LogError("Não foi possível salvar a cena isolada: " + PreviewScenePath);
            return;
        }

        Debug.Log("[Nova Aurora] Cena isolada criada com o GLB candidato, a escala/rotação do prefab ativo e o ciclo fixo às 23h. Cena=" + PreviewScenePath + "; limites=" + bounds.size + ".");
    }

    [MenuItem("Tools/Hegemonia/Nova Aurora/Adicionar luz noturna ao prefab ativo")]
    private static void AddLightingToProductionPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ProductionPrefabPath);
        if (root == null)
        {
            Debug.LogError("Prefab ativo não encontrado: " + ProductionPrefabPath);
            return;
        }

        try
        {
            if (root.GetComponent<NovaAuroraPredialLighting>() == null)
            {
                root.AddComponent<NovaAuroraPredialLighting>();
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ProductionPrefabPath);
            if (saved == null)
            {
                Debug.LogError("Unity não conseguiu salvar o componente no prefab ativo.");
                return;
            }

            Debug.Log("[Nova Aurora] Controlador de iluminação vinculado ao prefab ativo: " + ProductionPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Bounds GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("O GLB de validação não contém Renderer.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
