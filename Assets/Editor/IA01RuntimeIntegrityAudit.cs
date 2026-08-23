using System;
using System.Collections.Generic;
using System.Text;
using Hegemonia.AI.IA01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IA01RuntimeIntegrityAudit
{
    private const string CampaignScenePath = "Assets/Scenes/cena19).unity";

    [MenuItem("Hegemonia/Diagnostics/Run IA01 Campaign Audit")]
    public static void RunCampaignAudit()
    {
        StringBuilder report = new StringBuilder(8192);
        report.AppendLine("[Hegemonia] IA01 Campaign Audit");
        report.AppendLine("Cena: " + CampaignScenePath);

        Scene scene = EditorSceneManager.OpenScene(CampaignScenePath, OpenSceneMode.Additive);
        try
        {
            AuditSceneObjects(scene, report);
            AuditIA01Controllers(scene, report);
            AuditConstructionCatalog(report);
            AuditProductionAuthorities(scene, report);
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        Debug.Log(report.ToString());
    }

    private static void AuditSceneObjects(Scene scene, StringBuilder report)
    {
        int identities = 0;
        int operationalIdentities = 0;
        int visualIdentities = 0;
        int markers = 0;
        int militaryVisualsWithoutGameplay = 0;
        int printedIdentities = 0;
        int printedMilitaryVisuals = 0;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            IdentidadeUnidade[] foundIdentities = roots[i].GetComponentsInChildren<IdentidadeUnidade>(true);
            for (int j = 0; j < foundIdentities.Length; j++)
            {
                IdentidadeUnidade identity = foundIdentities[j];
                if (identity == null)
                {
                    continue;
                }

                identities++;
                bool operational = identity.GetComponentInChildren<ControleUnidade>(true) != null
                    || identity.GetComponentInChildren<IdentidadeNaval>(true) != null
                    || identity.GetComponentInChildren<SaveableEntity>(true) != null;
                if (operational)
                {
                    operationalIdentities++;
                }
                else
                {
                    visualIdentities++;
                }

                if (printedIdentities < 120)
                {
                    report.AppendLine(string.Format(
                        "IdentidadeUnidade: {0} | team={1} tipo={2} | operacional={3} | ativo={4}",
                        GetTransformPath(identity.transform), identity.teamID, identity.tipoUnidade, operational, identity.gameObject.activeInHierarchy));
                    printedIdentities++;
                }
            }

            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                MonoBehaviour behaviour = behaviours[j];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (IsSceneMarkerType(typeName))
                {
                    markers++;
                }
            }

            Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                if (renderer == null || !LooksLikeMilitaryVisual(renderer.gameObject))
                {
                    continue;
                }

                GameObject visual = renderer.gameObject;
                bool hasGameplay = visual.GetComponentInParent<IdentidadeUnidade>() != null
                    || visual.GetComponentInParent<ControleUnidade>() != null
                    || visual.GetComponentInParent<IdentidadeNaval>() != null
                    || visual.GetComponentInParent<SaveableEntity>() != null;
                if (!hasGameplay)
                {
                    militaryVisualsWithoutGameplay++;
                    if (printedMilitaryVisuals < 120)
                    {
                        report.AppendLine("Visual militar sem gameplay: " + GetTransformPath(visual.transform));
                        printedMilitaryVisuals++;
                    }
                }
            }
        }

        report.AppendLine(string.Format(
            "Resumo IdentidadeUnidade: total={0} operacional={1} visual-sem-operacao={2}.",
            identities, operationalIdentities, visualIdentities));
        report.AppendLine("Resumo marcadores/spawns: " + markers + " componente(s) identificado(s) por tipo.");
        report.AppendLine("Visuais militares sem identidade/controle/save: " + militaryVisualsWithoutGameplay + " candidato(s) para revisão manual.");
        if (printedIdentities == 120 && identities > printedIdentities)
        {
            report.AppendLine("Aviso: a listagem de identidades foi limitada às primeiras 120 entradas; o total acima continua completo.");
        }
    }

    private static void AuditIA01Controllers(Scene scene, StringBuilder report)
    {
        int controllers = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            IA01Controller[] found = roots[i].GetComponentsInChildren<IA01Controller>(true);
            for (int j = 0; j < found.Length; j++)
            {
                IA01Controller controller = found[j];
                if (controller == null)
                {
                    continue;
                }

                controllers++;
                report.AppendLine(string.Format(
                    "IA01Controller: {0} | team={1} nation={2} | construcao={3} militarPermitido={4} | autoridade=IA01",
                    GetTransformPath(controller.transform), controller.TeamId, controller.NationName,
                    CountNonNull(controller.FichasDeConstrucao), CountNonNull(controller.FichasMilitaresPermitidas)));

                AppendCatalogEntries(report, "  construcao", controller.FichasDeConstrucao);
                AppendCatalogEntries(report, "  militar", controller.FichasMilitaresPermitidas);
            }
        }

        report.AppendLine("Controllers IA01 na cena: " + controllers + ".");
        if (controllers == 0)
        {
            report.AppendLine("ERRO: a cena de campanha nao possui IA01Controller serializado.");
        }
    }

    private static void AuditConstructionCatalog(StringBuilder report)
    {
        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int total = 0;
        int missingPrefab = 0;
        int missingScripts = 0;
        HashSet<string> referencedPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DadosConstrucao item = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
            if (item == null)
            {
                continue;
            }

            total++;
            GameObject prefab;
            if (!TryGetPrefabReference(item, out prefab))
            {
                missingPrefab++;
                report.AppendLine("DadosConstrucao com referencia de prefab quebrada: " + path + " | id=" + SafeStableId(item));
                continue;
            }

            string prefabPath = prefab != null ? AssetDatabase.GetAssetPath(prefab) : string.Empty;
            if (prefab == null || string.IsNullOrWhiteSpace(prefabPath))
            {
                missingPrefab++;
                report.AppendLine("DadosConstrucao sem prefab: " + path + " | id=" + SafeStableId(item));
                continue;
            }

            referencedPrefabs.Add(prefabPath);
            int prefabMissingScripts = CountMissingScriptsRecursive(prefab);
            missingScripts += prefabMissingScripts;
            report.AppendLine(string.Format(
                "DadosConstrucao: {0} | id={1} | categoria={2} | prefab={3} | missingScripts={4}",
                path, SafeStableId(item), item.categoria, prefabPath, prefabMissingScripts));
        }

        report.AppendLine(string.Format(
            "Auditoria DadosConstrucao: total={0} | semPrefab={1} | scriptsAusentesNosRoots={2} | prefabsUnicosReferenciados={3}.",
            total, missingPrefab, missingScripts, referencedPrefabs.Count));
    }

    private static void AuditProductionAuthorities(Scene scene, StringBuilder report)
    {
        int competingControllers = 0;
        int cartelControllers = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                MonoBehaviour behaviour = behaviours[j];
                if (behaviour == null)
                {
                    continue;
                }

                string fullName = behaviour.GetType().FullName ?? string.Empty;
                if (fullName.IndexOf("IA_BrainMaster", StringComparison.Ordinal) >= 0
                    || fullName.IndexOf("IA_MasterController", StringComparison.Ordinal) >= 0
                    || fullName.IndexOf("AISovereignController", StringComparison.Ordinal) >= 0
                    || fullName.EndsWith("CerebroIA", StringComparison.Ordinal))
                {
                    competingControllers++;
                    report.AppendLine("ERRO: controller de producao concorrente na cena: " + GetTransformPath(behaviour.transform) + " | " + fullName);
                }

                if (fullName.IndexOf("CartelAIController", StringComparison.Ordinal) >= 0)
                {
                    cartelControllers++;
                }
            }
        }

        report.AppendLine("Autoridade de producao: IA01 exclusiva para a faccao IA01; concorrentes encontrados=" + competingControllers + ".");
        report.AppendLine("Sistemas Cartel separados encontrados=" + cartelControllers + " (nao sao classificados como IA01).");
    }

    private static void AppendCatalogEntries(StringBuilder report, string label, IReadOnlyList<DadosConstrucao> entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            DadosConstrucao item = entries[i];
            if (item == null)
            {
                report.AppendLine(label + "[" + i + "] = NULL");
                continue;
            }

            GameObject prefab;
            string prefabPath = TryGetPrefabReference(item, out prefab) && prefab != null
                ? AssetDatabase.GetAssetPath(prefab)
                : "SEM PREFAB/REFERENCIA QUEBRADA";
            report.AppendLine(string.Format("{0}[{1}] = {2} | id={3} | prefab={4}", label, i, AssetDatabase.GetAssetPath(item), SafeStableId(item), prefabPath));
        }
    }

    private static int CountNonNull(IReadOnlyList<DadosConstrucao> entries)
    {
        if (entries == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSceneMarkerType(string typeName)
    {
        return typeName.IndexOf("BuildSlot", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("PatrolZone", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("ManualCreate", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("WarAdvanceZone", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("ExtractionZone", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeMilitaryVisual(GameObject objectInScene)
    {
        string name = objectInScene.name.ToLowerInvariant();
        string[] tokens =
        {
            "soldado", "soldier", "tank", "tanque", "blindado", "artilharia", "ares", "su11",
            "sr71", "fighter", "bomber", "aviao", "aircraft", "destroyer", "submarine", "submarino",
            "carrier", "portaavioes", "navio", "ship", "f200"
        };

        for (int i = 0; i < tokens.Length; i++)
        {
            if (name.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTransformPath(Transform current)
    {
        if (current == null)
        {
            return "<null>";
        }

        List<string> names = new List<string>();
        Transform cursor = current;
        while (cursor != null)
        {
            names.Add(cursor.name);
            cursor = cursor.parent;
        }

        names.Reverse();
        return string.Join("/", names.ToArray());
    }

    private static int CountMissingScriptsRecursive(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        try
        {
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
        catch (MissingReferenceException)
        {
            // Alguns assets antigos mantêm uma referência Unity quebrada; a auditoria
            // deve registrá-los sem abortar o restante do relatório.
            return 0;
        }
        catch (ArgumentException)
        {
            return 0;
        }
    }

    private static bool TryGetPrefabReference(DadosConstrucao item, out GameObject prefab)
    {
        prefab = null;
        if (item == null)
        {
            return false;
        }

        try
        {
            prefab = item.PrefabDaUnidade;
            return true;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SafeStableId(DadosConstrucao item)
    {
        if (item == null)
        {
            return "<null>";
        }

        try
        {
            return item.GetStableId();
        }
        catch (MissingReferenceException)
        {
            return "<referencia quebrada>";
        }
        catch (Exception)
        {
            return "<erro ao ler id>";
        }
    }
}
