using UnityEngine;
using UnityEditor;
using System.IO;

public class GeradorUsinas : MonoBehaviour
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    [MenuItem("Hegemonia/Gerar Usinas de Energia")]
    public static void Gerar()
    {
        if (EditorPrefs.GetBool("UsinasGeradasV2", false)) return;
        EditorPrefs.SetBool("UsinasGeradasV2", true);
        string dirDestino = "Assets/Prefabs/Usinas";
        if (!Directory.Exists(dirDestino))
        {
            Directory.CreateDirectory(dirDestino);
        }

        GameObject prefabOriginal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Energia/Energia.prefab");
        DadosConstrucao fichaOriginal = AssetDatabase.LoadAssetAtPath<DadosConstrucao>("Assets/Prefabs/Energia/Energia.asset");

        if (prefabOriginal == null || fichaOriginal == null)
        {
            Debug.LogError("Prefab ou ficha original 'Energia' não encontrados em Assets/Prefabs/Energia/");
            return;
        }

        CriarUsina(dirDestino, prefabOriginal, fichaOriginal, "Usina Pequena", 500, 20f);
        CriarUsina(dirDestino, prefabOriginal, fichaOriginal, "Usina Media", 1200, 60f);
        CriarUsina(dirDestino, prefabOriginal, fichaOriginal, "Usina Grande", 3500, 200f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Usinas geradas com sucesso em " + dirDestino);
    }

    private static void CriarUsina(string dir, GameObject prefabOrig, DadosConstrucao fichaOrig, string nome, int preco, float producaoEnergia)
    {
        string nomeArquivo = nome.Replace(" ", "_");
        string caminhoPrefab = $"{dir}/{nomeArquivo}.prefab";
        string caminhoFicha = $"{dir}/{nomeArquivo}.asset";

        AssetDatabase.DeleteAsset(caminhoPrefab);
        AssetDatabase.DeleteAsset(caminhoFicha);

        // Cria/Copia o Prefab
        bool sucesso;
        GameObject novoPrefab = PrefabUtility.SaveAsPrefabAsset(prefabOrig, caminhoPrefab, out sucesso);
        if (!sucesso)
        {
            Debug.LogError("Falha ao salvar prefab: " + caminhoPrefab);
            return;
        }

        // Modificar o Prefab (adicionar/modificar componente EstruturaEconomica)
        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(caminhoPrefab))
        {
            GameObject go = editingScope.prefabContentsRoot;
            go.name = nome;

            EstruturaEconomica eco = go.GetComponent<EstruturaEconomica>();
            if (eco == null) eco = go.AddComponent<EstruturaEconomica>();

            eco.tipo = TipoEstruturaEconomica.Energia;
            eco.energiaProduzida = producaoEnergia;
            eco.energiaConsumida = 0f; // Usina não consome, apenas produz
            
            // Garantir IdentidadeUnidade também
            IdentidadeUnidade id = go.GetComponent<IdentidadeUnidade>();
            if (id != null)
            {
                id.energiaConsumida = 0f;
            }
        }

        // Carrega o prefab recém modificado
        GameObject prefabFinal = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);

        // Cria/Copia o ScriptableObject (DadosConstrucao)
        DadosConstrucao novaFicha = ScriptableObject.CreateInstance<DadosConstrucao>();
        novaFicha.NomeItem = nome;
        novaFicha.descricao = $"Gera {producaoEnergia} MW de energia.";
        novaFicha.icone = fichaOrig.icone;
        novaFicha.PrefabDaUnidade = prefabFinal;
        novaFicha.preco = preco;
        novaFicha.categoria = DadosConstrucao.CategoriaItem.Energia; // Categoria Energia

        AssetDatabase.CreateAsset(novaFicha, caminhoFicha);
    }
#endif
}
