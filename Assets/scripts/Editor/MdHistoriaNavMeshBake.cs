#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using PackageNavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using PackageCollectObjects = Unity.AI.Navigation.CollectObjects;

/// <summary>
/// Configura e assa a navegação terrestre da Md Historia usando os Terrains
/// dos países que já existem na cena. Cada país recebe uma superfície própria,
/// evitando criar uma arquitetura de movimentação paralela.
/// </summary>
public static class MdHistoriaNavMeshBake
{
    private const string CenaAlvo = "Assets/_Recovery/Md Historia.unity";
    private const string PastaDados = "Assets/MapThemes/MdHistoria/NavMeshData";
    private const string PrefixoAsset = "NavMesh-Veiculos-";

    [MenuItem("Hegemonia/Mapa/Md Historia/Configurar e assar NavMesh de veículos", priority = 40)]
    public static void ConfigurarEAssarMdHistoria()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.path.Equals(CenaAlvo, StringComparison.OrdinalIgnoreCase))
        {
            cena = EditorSceneManager.OpenScene(CenaAlvo, OpenSceneMode.Single);
        }

        if (!cena.IsValid())
        {
            Debug.LogError("[Md Historia NavMesh] Não foi possível abrir a cena alvo: " + CenaAlvo);
            return;
        }

        int camadaChao = LayerMask.NameToLayer("Chao");
        if (camadaChao < 0)
        {
            Debug.LogError("[Md Historia NavMesh] A camada Chao não existe no TagManager.");
            return;
        }

        Directory.CreateDirectory(PastaDados);
        AssetDatabase.Refresh();

        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<PackageNavMeshSurface> superficies = new List<PackageNavMeshSurface>();
        int ignorados = 0;

        foreach (Terrain terreno in terrenos)
        {
            if (terreno == null || !EhTerrenoDePais(terreno.name))
            {
                ignorados++;
                continue;
            }

            terreno.gameObject.layer = camadaChao;

            PackageNavMeshSurface superficie = terreno.GetComponent<PackageNavMeshSurface>();
            if (superficie == null)
            {
                superficie = terreno.gameObject.AddComponent<PackageNavMeshSurface>();
            }

            superficie.agentTypeID = 0;
            superficie.collectObjects = PackageCollectObjects.Children;
            superficie.layerMask = 1 << camadaChao;
            superficie.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            superficie.defaultArea = 0;
            // A versão atual de com.unity.ai.navigation não expõe mais
            // NavMeshSurface.generateLinks. A geração de links é controlada
            // pelos componentes NavMeshLink da cena.
            superficie.ignoreNavMeshAgent = true;
            superficie.ignoreNavMeshObstacle = true;
            superficie.overrideTileSize = false;
            superficie.overrideVoxelSize = false;
            superficie.minRegionArea = 2f;

            string caminhoAsset = PastaDados + "/" + PrefixoAsset + NomeSeguro(terreno.name) + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(caminhoAsset) != null)
            {
                AssetDatabase.DeleteAsset(caminhoAsset);
            }

            superficie.BuildNavMesh();
            if (superficie.navMeshData == null)
            {
                Debug.LogError("[Md Historia NavMesh] Bake sem dados para " + terreno.name + ".");
                continue;
            }

            superficie.navMeshData.name = PrefixoAsset + NomeSeguro(terreno.name);
            AssetDatabase.CreateAsset(superficie.navMeshData, caminhoAsset);
            EditorUtility.SetDirty(superficie);
            superficies.Add(superficie);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.Refresh();

        Debug.Log("[Md Historia NavMesh] Bake concluído: países/ilhas=" + superficies.Count
            + ", terrenos ignorados=" + ignorados
            + ", camada=Chao, agente=Humanoid, geometria=RenderMeshes, links=desativados.");
    }

    public static void ConfigurarEAssarMdHistoriaEmLote()
    {
        ConfigurarEAssarMdHistoria();
    }

    private static bool EhTerrenoDePais(string nome)
    {
        string normalizado = (nome ?? string.Empty).Trim().ToLowerInvariant();
        return normalizado.Contains("pais1")
            || normalizado.Contains("pais2")
            || normalizado.Contains("pais3")
            || normalizado.Contains("pais4")
            || normalizado.Contains("pais5")
            || normalizado.Contains("pais6")
            || normalizado.Contains("ilha");
    }

    private static string NomeSeguro(string nome)
    {
        string valor = (nome ?? "Terreno").Trim();
        foreach (char invalido in Path.GetInvalidFileNameChars())
        {
            valor = valor.Replace(invalido, '_');
        }

        return string.IsNullOrEmpty(valor) ? "Terreno" : valor;
    }
}
#endif
