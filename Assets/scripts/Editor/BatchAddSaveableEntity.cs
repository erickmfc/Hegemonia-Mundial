using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BatchAddSaveableEntity : EditorWindow
{
    [MenuItem("Tools/Otimização/Adicionar SaveableEntity (Em Todas as Unidades e Imóveis)")]
    public static void ShowWindow()
    {
        AdicionarEmTodasAsUnidades();
    }

    private static void AdicionarEmTodasAsUnidades()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int adicionados = 0;
        int jaPossui = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                bool isEntidadeSalvavel = prefab.GetComponent<IdentidadeUnidade>() != null
                                          || prefab.GetComponent<ControleUnidade>() != null
                                          || prefab.GetComponent<IdentidadeNaval>() != null
                                          || prefab.GetComponent<Imovel>() != null
                                          || prefab.GetComponent<Fabrica>() != null
                                          || prefab.GetComponent<Estaleiro>() != null
                                          || prefab.GetComponent<PierMarinha>() != null
                                          || prefab.GetComponent<GerenciadorAeroporto>() != null
                                          || prefab.GetComponent<GerenciadorQuartel>() != null
                                          || prefab.GetComponent<PlataformaOffshore>() != null
                                          || prefab.GetComponent<SiloLancadorEstrategico>() != null;

                if (isEntidadeSalvavel)
                {
                    SaveableEntity saveScript = prefab.GetComponent<SaveableEntity>();

                    if (saveScript == null)
                    {
                        // Instancia o prefab, remove scripts ausentes para evitar erros ao salvar, adiciona o script e salva
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        RemoveMissingScriptsRecursively(instance);
                        instance.AddComponent<SaveableEntity>();
                        
                        PrefabUtility.SaveAsPrefabAsset(instance, path);
                        DestroyImmediate(instance);
                        adicionados++;
                    }
                    else
                    {
                        jaPossui++;
                    }
                }
            }
        }

        EditorUtility.DisplayDialog("SaveableEntity", 
            $"Processo Concluído!\n\nScripts Adicionados: {adicionados}\nJá possuíam o script: {jaPossui}", 
            "OK");
    }

    private static void RemoveMissingScriptsRecursively(GameObject go)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        for (int i = 0; i < go.transform.childCount; i++)
        {
            RemoveMissingScriptsRecursively(go.transform.GetChild(i).gameObject);
        }
    }
}
