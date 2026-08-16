using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public static class CorretorHeliporto
{
    private const string HeliportoPrefabPath = "Assets/Prefabs/Heliporto/Heliporto.prefab";
    private const string HeliportoDadosPath = "Assets/Prefabs/Heliporto/Dados_Heliporto.asset";
    private const string UsinaCarvaoPrefabPath = "Assets/Prefabs/Energia/Usina Carvao.prefab";

    [MenuItem("Hegemonia/Corrigir Conteudo de Heliporto e Usina")]
    public static void Corrigir()
    {
        CorrigirHeliporto();
        CorrigirUsinaCarvao();
        CorrigirFichaHeliporto();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Corretor] Heliporto e Usina de Carvao corrigidos.");
    }

    private static void CorrigirHeliporto()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HeliportoPrefabPath);
        if (root == null)
        {
            Debug.LogError("[Corretor] Prefab do Heliporto nao encontrado: " + HeliportoPrefabPath);
            return;
        }

        try
        {
            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider>();
            }

            if (collider.size == Vector3.zero)
            {
                collider.center = new Vector3(0f, 0.2f, 0f);
                collider.size = new Vector3(10f, 0.5f, 10f);
            }

            if (root.GetComponent<Heliporto>() == null)
            {
                root.AddComponent<Heliporto>();
            }

            if (root.GetComponent<SaveableEntity>() == null)
            {
                root.AddComponent<SaveableEntity>();
            }

            if (root.GetComponent<SistemaDeDanos>() == null)
            {
                SistemaDeDanos danos = root.AddComponent<SistemaDeDanos>();
                danos.ehEstrutura = true;
            }

            root.layer = 0;
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, HeliportoPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CorrigirUsinaCarvao()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(UsinaCarvaoPrefabPath);
        if (root == null)
        {
            Debug.LogError("[Corretor] Prefab da Usina de Carvao nao encontrado: " + UsinaCarvaoPrefabPath);
            return;
        }

        try
        {
            if (root.GetComponent<SistemaDeDanos>() == null)
            {
                SistemaDeDanos danos = root.AddComponent<SistemaDeDanos>();
                danos.ehEstrutura = true;
            }

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, UsinaCarvaoPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CorrigirFichaHeliporto()
    {
        DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(HeliportoDadosPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeliportoPrefabPath);
        if (dados == null || prefab == null)
        {
            Debug.LogError("[Corretor] Ficha ou prefab do Heliporto nao encontrado.");
            return;
        }

        if (dados.PrefabDaUnidade != prefab)
        {
            dados.PrefabDaUnidade = prefab;
            EditorUtility.SetDirty(dados);
        }
    }
}
#endif
