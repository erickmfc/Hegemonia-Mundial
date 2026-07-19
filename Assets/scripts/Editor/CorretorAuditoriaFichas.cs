using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Script de Editor para corrigir automaticamente os erros detectados pela AuditoriaConteudoJogo.
/// </summary>
public class CorretorAuditoriaFichas : EditorWindow
{
    [MenuItem("Hegemonia/Ferramentas/Corrigir Erros de Auditoria (Fichas e Prefabs)")]
    public static void CorrigirErros()
    {
        Debug.Log("Iniciando correção automática de prefabs com base nas Fichas...");

        // Encontra todas as Fichas (DadosConstrucao) no projeto
        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int prefabsCorrigidos = 0;
        int errosFaltandoPrefab = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DadosConstrucao ficha = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);

            if (ficha == null) continue;

            string nome = string.IsNullOrWhiteSpace(ficha.nomeItem) ? ficha.name : ficha.nomeItem;
            
            GameObject prefab = null;
            bool hasPrefab = ficha.TryGetPrefab(out prefab);

            if (!hasPrefab || prefab == null)
            {
                Debug.LogError($"[Correção] A ficha '{nome}' não tem um prefab associado ou ele está corrompido. Corrija manualmente.");
                errosFaltandoPrefab++;
                continue;
            }

            bool modificado = false;
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            
            // Abre o prefab para edição
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);

            bool materialPersistente = ficha.categoria != DadosConstrucao.CategoriaItem.Tecnologia;
            bool ehMilitar = ficha.categoria == DadosConstrucao.CategoriaItem.Exercito || 
                             ficha.categoria == DadosConstrucao.CategoriaItem.Marinha || 
                             ficha.categoria == DadosConstrucao.CategoriaItem.Aeronautica;

            // 1. Verificação de Collider
            if (materialPersistente && ehMilitar)
            {
                if (contentsRoot.GetComponentInChildren<Collider>(true) == null)
                {
                    contentsRoot.AddComponent<BoxCollider>();
                    Debug.Log($"[Correção] BoxCollider adicionado ao prefab: {prefab.name} (Ficha: {nome})");
                    modificado = true;
                }
            }

            // 2. Verificação de IdentidadeUnidade
            if (ehMilitar)
            {
                if (contentsRoot.GetComponentInChildren<IdentidadeUnidade>(true) == null)
                {
                    contentsRoot.AddComponent<IdentidadeUnidade>();
                    Debug.Log($"[Correção] IdentidadeUnidade adicionado ao prefab: {prefab.name} (Ficha: {nome})");
                    modificado = true;
                }
            }

            // 3. Verificação de SistemaDeDanos
            if (materialPersistente && ehMilitar)
            {
                if (contentsRoot.GetComponentInChildren<SistemaDeDanos>(true) == null)
                {
                    contentsRoot.AddComponent<SistemaDeDanos>();
                    Debug.Log($"[Correção] SistemaDeDanos adicionado ao prefab: {prefab.name} (Ficha: {nome})");
                    modificado = true;
                }
            }

            if (modificado)
            {
                // Salva o prefab
                PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
                prefabsCorrigidos++;
            }

            PrefabUtility.UnloadPrefabContents(contentsRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Correção Concluída] {prefabsCorrigidos} prefabs foram corrigidos automaticamente.");
        if (errosFaltandoPrefab > 0)
        {
            Debug.LogWarning($"Aviso: {errosFaltandoPrefab} fichas estão com o campo de prefab vazio e precisam de correção manual.");
        }
        
        EditorUtility.DisplayDialog(
            "Correção Concluída", 
            $"{prefabsCorrigidos} prefabs foram corrigidos com sucesso (Collider, IdentidadeUnidade, SistemaDeDanos).\n\n" +
            (errosFaltandoPrefab > 0 ? $"{errosFaltandoPrefab} fichas precisam ter o prefab atribuído manualmente." : ""), 
            "OK");
    }
}
