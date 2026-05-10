using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HegemoniaIntegrityAudit
{
    private const string MenuPrincipalPath = "Assets/Scenes/MenuPrincipal.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EstaleiroPrefabPath = "Assets/Prefabs/Estaleiro Marinho/Estaleiros navais.prefab";

    [MenuItem("Hegemonia/Diagnostics/Run Integrity Audit")]
    public static void RunIntegrityAudit()
    {
        StringBuilder report = new StringBuilder(1024);
        report.AppendLine("[Hegemonia] Integrity Audit");

        AuditBuildScenes(report);
        AuditEstaleiroPrefab(report);
        AuditSceneMissingScripts(MenuPrincipalPath, report);
        AuditSceneMissingScripts(SampleScenePath, report);
        AuditPrefabMissingScripts(EstaleiroPrefabPath, report);
        AuditConstructionCatalog(report);

        Debug.Log(report.ToString());
    }

    private static void AuditBuildScenes(StringBuilder report)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int recoveryScenes = 0;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled)
            {
                continue;
            }

            if (scenes[i].path.Contains("_Recovery"))
            {
                recoveryScenes++;
                report.AppendLine("BuildScene recovery ativo: " + scenes[i].path);
            }
        }

        if (recoveryScenes == 0)
        {
            report.AppendLine("BuildScene OK: nenhuma cena _Recovery habilitada.");
        }
    }

    private static void AuditEstaleiroPrefab(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EstaleiroPrefabPath);
        try
        {
            Estaleiro estaleiro = root != null ? root.GetComponent<Estaleiro>() : null;
            if (estaleiro == null)
            {
                report.AppendLine("Estaleiro prefab ausente ou sem componente Estaleiro.");
                return;
            }

            bool slotsValidos = estaleiro.slots != null
                                && estaleiro.slots.Length >= 2
                                && estaleiro.slots[0] != null
                                && estaleiro.slots[1] != null
                                && estaleiro.slots[0].pontoDeConstrucao != null
                                && estaleiro.slots[1].pontoDeConstrucao != null
                                && estaleiro.slots[0].nomeSlot == "Atracagem"
                                && estaleiro.slots[1].nomeSlot == "Atracagem_Grande";

            report.AppendLine(slotsValidos
                ? "Estaleiro OK: slots Atracagem e Atracagem_Grande consistentes."
                : "Estaleiro ERRO: slots de atracagem inconsistentes.");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AuditSceneMissingScripts(string scenePath, StringBuilder report)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        int missing = 0;

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                missing += CountMissingScriptsRecursive(roots[i]);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        report.AppendLine(string.Format("{0}: missing scripts = {1}", scenePath, missing));
    }

    private static void AuditPrefabMissingScripts(string prefabPath, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int missing = CountMissingScriptsRecursive(root);
            report.AppendLine(string.Format("{0}: missing scripts = {1}", prefabPath, missing));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AuditConstructionCatalog(StringBuilder report)
    {
        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int semPrefab = 0;
        int semIcone = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DadosConstrucao item = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
            if (item == null)
            {
                continue;
            }

            if (item.prefabDaUnidade == null)
            {
                semPrefab++;
                report.AppendLine("Catalogo sem prefab: " + path);
            }

            if (item.icone == null)
            {
                semIcone++;
            }
        }

        report.AppendLine(string.Format("Catalogo: {0} item(ns) sem prefab | {1} item(ns) sem icone explicito.", semPrefab, semIcone));
    }

    private static int CountMissingScriptsRecursive(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.gameObject == root)
            {
                continue;
            }

            total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }

        return total;
    }
}
