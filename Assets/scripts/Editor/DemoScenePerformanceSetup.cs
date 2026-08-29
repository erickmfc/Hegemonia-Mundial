#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configuração idempotente da demo. Mantém a cena 19 intacta: todos os
/// componentes são criados e gravados apenas em demo1.
/// </summary>
public static class DemoScenePerformanceSetup
{
    private const string DemoScenePath = "Assets/_Recovery/demo1.unity";
    private const string PerformanceObjectName = "Demo Performance - Neblina Frontal";

    [MenuItem("Hegemonia/Demo/Configurar IA02, mapa completo e neblina", priority = 10)]
    public static void ConfigurarTudo()
    {
        Scene scene = OpenDemo();
        ConfigurarPerformance(scene);

        // O setup da IA02 é compartilhado com a campanha, mas a cena alvo é
        // passada pela própria entrada pública de tutorial. Não há cópia de
        // IA01 nem alteração de objetos do jogador.
        Hegemonia.AI.IA02.EditorTools.IA02CampaignSetup.ConfigureTutorial();
        scene = SceneManager.GetActiveScene();
        ConfigurarMapa(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Demo] IA02, enquadramento completo do mapa e neblina configurados em " + DemoScenePath + ".");
    }

    [MenuItem("Hegemonia/Demo/Configurar somente mapa e neblina", priority = 11)]
    public static void ConfigurarSomentePerformance()
    {
        Scene scene = OpenDemo();
        ConfigurarPerformance(scene);
        ConfigurarMapa(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Demo] Mapa e neblina configurados sem alterar IA01/IA02.");
    }

    private static Scene OpenDemo()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(active.path, DemoScenePath, StringComparison.OrdinalIgnoreCase))
        {
            active = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        return active;
    }

    private static void ConfigurarPerformance(Scene scene)
    {
        GameObject root = GameObject.Find(PerformanceObjectName);
        if (root == null)
        {
            root = new GameObject(PerformanceObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Criar performance da demo");
        }

        NeblinaFrontalPerformance performance = root.GetComponent<NeblinaFrontalPerformance>();
        if (performance == null) performance = Undo.AddComponent<NeblinaFrontalPerformance>(root);

        Camera mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = GameObject.Find("Main Camera")?.GetComponent<Camera>();

        SerializedObject serialized = new SerializedObject(performance);
        SetObject(serialized, "cameraAlvo", mainCamera);
        SetBool(serialized, "usarOcclusionCulling", true);
        SetFloat(serialized, "distanciaCulling", 14000f);
        SetLayerMask(serialized, "camadasSemCulling", 0);
        SetBool(serialized, "preservarFarClipDaCamera", true);
        SetBool(serialized, "aplicarNeblinaDaCena", true);
        SetBool(serialized, "preservarCorDaCena", true);
        SetInt(serialized, "modoNeblina", (int)FogMode.Linear);
        SetFloat(serialized, "inicioNeblina", 6500f);
        SetFloat(serialized, "fimNeblina", 14000f);
        SetFloat(serialized, "densidadeNeblina", 0.00004f);
        SetBool(serialized, "restaurarAoDesativar", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(performance);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigurarMapa(Scene scene)
    {
        MapaGeralController[] controllers = UnityEngine.Object.FindObjectsByType<MapaGeralController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        MapaGeralController escolhido = null;
        for (int i = 0; i < controllers.Length; i++)
        {
            MapaGeralController candidato = controllers[i];
            if (candidato == null || !candidato.gameObject.activeSelf) continue;
            escolhido = candidato;
            break;
        }

        if (escolhido == null)
        {
            Debug.LogWarning("[Demo] Nenhum MapaGeralController ativo foi encontrado.");
            return;
        }

        SerializedObject serialized = new SerializedObject(escolhido);
        SetBool(serialized, "detectarLimitesReaisDoMapa", true);
        SetFloat(serialized, "margemMapa", 350f);
        SetBool(serialized, "enquadrarCoberturaCompletaAoAbrir", true);
        SetFloat(serialized, "margemEnquadramentoInicial", 1.08f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(escolhido);
        EditorSceneManager.MarkSceneDirty(scene);
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

    private static void SetInt(SerializedObject serialized, string path, int value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null) property.intValue = value;
    }

    private static void SetLayerMask(SerializedObject serialized, string path, int value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null) property.intValue = value;
    }
}
#endif
