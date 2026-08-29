#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantém as duas cenas jogáveis com uma única instância do Enterprise.
/// O prefab continua sendo a fonte de configuração do sistema V2; a cena só
/// guarda a instância, a pose e o vínculo de prefab.
/// </summary>
public static class SincronizadorCenasPortaAvioesV2Editor
{
    private const string PrefabPath = "Assets/Prefabs/Navios_Guerra/Porta avioes/Uss Enterprise.prefab";
    private static readonly string[] ScenePaths =
    {
        "Assets/_Recovery/demo1.unity",
        "Assets/Scenes/cena19).unity"
    };

    [MenuItem("Tools/Porta-aviões V2/Sincronizar Enterprise em demo1 e cena19")]
    public static void SincronizarEnterpriseNasCenas()
    {
        GarantirCenasJogaveisNoBuildSettings();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Prefab do USS Enterprise não encontrado em " + PrefabPath);

        Scene cenaOriginal = SceneManager.GetActiveScene();
        string caminhoOriginal = cenaOriginal.IsValid() ? cenaOriginal.path : string.Empty;

        try
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), scenePath)))
                {
                    Debug.LogWarning("[PortaAvioes V2] Cena não encontrada: " + scenePath);
                    continue;
                }

                Scene cena = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject[] encontrados = EncontrarEnterprises(cena, prefab);
                if (encontrados.Length > 1)
                {
                    Debug.LogWarning("[PortaAvioes V2] " + scenePath + " possui " + encontrados.Length
                        + " instâncias do Enterprise. Nenhuma foi apagada automaticamente.");
                    continue;
                }

                GameObject enterprise;
                if (encontrados.Length == 0)
                {
                    enterprise = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                    if (enterprise == null)
                        throw new InvalidOperationException("Não foi possível instanciar o prefab na cena " + scenePath);
                    Debug.Log("[PortaAvioes V2] USS Enterprise adicionado à cena " + scenePath, enterprise);
                }
                else
                {
                    enterprise = encontrados[0];
                    Debug.Log("[PortaAvioes V2] USS Enterprise já existente; configuração preservada em " + scenePath, enterprise);
                }

                NormalizarInstancia(enterprise, prefab);
                VerificarAutoridadeV2(enterprise, scenePath);
                EditorSceneManager.MarkSceneDirty(cena);
                EditorSceneManager.SaveScene(cena);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(caminhoOriginal) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), caminhoOriginal)))
                EditorSceneManager.OpenScene(caminhoOriginal, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log("[PortaAvioes V2] Sincronização concluída: demo1 e cena19 usam o mesmo prefab V2 do Enterprise.");
    }

    [MenuItem("Tools/Porta-aviões V2/Validar Enterprise em demo1 e cena19")]
    public static void ValidarEnterpriseNasCenas()
    {
        GarantirCenasJogaveisNoBuildSettings();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Prefab do USS Enterprise não encontrado em " + PrefabPath);

        Scene cenaOriginal = SceneManager.GetActiveScene();
        string caminhoOriginal = cenaOriginal.IsValid() ? cenaOriginal.path : string.Empty;
        try
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), scenePath)))
                {
                    Debug.LogError("[PortaAvioes V2] Cena ausente: " + scenePath);
                    continue;
                }

                Scene cena = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject[] encontrados = EncontrarEnterprises(cena, prefab);
                if (encontrados.Length != 1)
                {
                    Debug.LogError("[PortaAvioes V2] " + scenePath + " deve ter exatamente um Enterprise; encontrados: " + encontrados.Length);
                    continue;
                }

                ValidarInstancia(encontrados[0], prefab, scenePath);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(caminhoOriginal) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), caminhoOriginal)))
                EditorSceneManager.OpenScene(caminhoOriginal, OpenSceneMode.Single);
        }
    }

    private static void GarantirCenasJogaveisNoBuildSettings()
    {
        List<EditorBuildSettingsScene> cenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0]);
        bool alterado = false;

        foreach (string scenePath in ScenePaths)
        {
            int indice = cenas.FindIndex(cena => cena != null && string.Equals(cena.path, scenePath, StringComparison.OrdinalIgnoreCase));
            if (indice < 0)
            {
                cenas.Add(new EditorBuildSettingsScene(scenePath, true));
                alterado = true;
            }
            else if (!cenas[indice].enabled)
            {
                cenas[indice] = new EditorBuildSettingsScene(scenePath, true);
                alterado = true;
            }
        }

        if (alterado)
        {
            EditorBuildSettings.scenes = cenas.ToArray();
            Debug.Log("[PortaAvioes V2] demo1 e cena19 foram habilitadas no Build Settings.");
        }
    }

    private static GameObject[] EncontrarEnterprises(Scene cena, GameObject prefab)
    {
        List<GameObject> resultado = new List<GameObject>();
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            Transform[] transforms = raiz.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item == null || item.parent != null) continue;
                GameObject objeto = item.gameObject;
                GameObject fonte = PrefabUtility.GetCorrespondingObjectFromSource(objeto);
                bool eEnterprise = fonte == prefab ||
                    string.Equals(objeto.name, "Uss Enterprise", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objeto.name, "USS Enterprise", StringComparison.OrdinalIgnoreCase);
                if (eEnterprise) resultado.Add(objeto);
            }
        }
        return resultado.ToArray();
    }

    private static void NormalizarInstancia(GameObject enterprise, GameObject prefab)
    {
        // As cenas podem ter sido gravadas antes da última correção do
        // prefab. Reverter somente esta instância elimina overrides e
        // referências antigas de componentes, sem tocar nos demais objetos
        // da cena nem criar uma segunda autoridade.
        if (PrefabUtility.GetCorrespondingObjectFromSource(enterprise) == prefab)
            PrefabUtility.RevertPrefabInstance(enterprise, InteractionMode.AutomatedAction);

        enterprise.name = "USS Enterprise";
        enterprise.SetActive(true);

        // O prefab já contém a pose ajustada pelo artista. Copiá-la somente
        // ao criar/normalizar a instância evita coordenadas antigas de cena.
        enterprise.transform.SetPositionAndRotation(prefab.transform.position, prefab.transform.rotation);
        enterprise.transform.localScale = prefab.transform.localScale;

        if (Array.IndexOf(InternalEditorUtility.tags, "Navio") >= 0)
            enterprise.tag = "Navio";
    }

    private static void VerificarAutoridadeV2(GameObject enterprise, string scenePath)
    {
        GerenciadorOperacoesPortaAvioesV2[] gerenciadores = enterprise.GetComponentsInChildren<GerenciadorOperacoesPortaAvioesV2>(true);
        LayoutConvesPortaAvioesV2[] layouts = enterprise.GetComponentsInChildren<LayoutConvesPortaAvioesV2>(true);
        if (gerenciadores.Length != 1 || layouts.Length != 1)
            Debug.LogWarning("[PortaAvioes V2] " + scenePath + " precisa de exatamente um gerenciador e um layout V2; encontrados: gerenciadores="
                + gerenciadores.Length + ", layouts=" + layouts.Length, enterprise);
        else if (!gerenciadores[0].usarSistemaOperacoesV2)
            Debug.LogWarning("[PortaAvioes V2] Sistema V2 desativado no Enterprise de " + scenePath, gerenciadores[0]);
    }

    private static void ValidarInstancia(GameObject enterprise, GameObject prefab, string scenePath)
    {
        GameObject fonte = PrefabUtility.GetCorrespondingObjectFromSource(enterprise);
        bool prefabLigado = fonte == prefab;
        bool ativo = enterprise.activeSelf;
        GerenciadorOperacoesPortaAvioesV2[] gerenciadores = enterprise.GetComponentsInChildren<GerenciadorOperacoesPortaAvioesV2>(true);
        LayoutConvesPortaAvioesV2[] layouts = enterprise.GetComponentsInChildren<LayoutConvesPortaAvioesV2>(true);
        Debug.Log("[PortaAvioes V2] " + scenePath + " | Enterprise único=" + ativo
            + " | prefab ligado=" + prefabLigado
            + " | gerenciadores=" + gerenciadores.Length
            + " | layouts=" + layouts.Length, enterprise);
        if (!prefabLigado || !ativo || gerenciadores.Length != 1 || layouts.Length != 1)
            Debug.LogError("[PortaAvioes V2] Instância inconsistente em " + scenePath, enterprise);
    }
}
#endif
