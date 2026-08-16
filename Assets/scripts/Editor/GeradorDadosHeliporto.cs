using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public class GeradorDadosHeliporto
{
    static GeradorDadosHeliporto()
    {
        EditorApplication.delayCall += GerarDados;
    }

    public static void GerarDados()
    {
        const string pastaPath = "Assets/Prefabs/Heliporto";
        const string arquivoPath = pastaPath + "/Dados_Heliporto.asset";
        const string prefabPath = pastaPath + "/Heliporto.prefab";

        if (!System.IO.Directory.Exists(Application.dataPath + "/Prefabs/Heliporto"))
        {
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Prefabs/Heliporto");
            AssetDatabase.Refresh();
        }

        DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(arquivoPath);
        bool novoArquivo = false;
        if (dados == null)
        {
            dados = ScriptableObject.CreateInstance<DadosConstrucao>();
            AssetDatabase.CreateAsset(dados, arquivoPath);
            novoArquivo = true;
        }

        if (novoArquivo || string.IsNullOrEmpty(dados.NomeItem))
        {
            dados.NomeItem = "Heliporto";
            dados.descricao = "Base para operacoes aereas. Permite compra e reabastecimento de helicopteros.";
            dados.preco = 500;
            dados.categoria = DadosConstrucao.CategoriaItem.Infraestrutura;
        }

        GameObject prefabFuncional = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabFuncional != null && dados.PrefabDaUnidade != prefabFuncional)
        {
            dados.PrefabDaUnidade = prefabFuncional;
            EditorUtility.SetDirty(dados);
        }

        AssetDatabase.SaveAssets();
        if (novoArquivo)
        {
            Debug.Log("[Gerador] Ficha do Heliporto criada e vinculada ao prefab funcional.");
        }
    }
}
#endif
