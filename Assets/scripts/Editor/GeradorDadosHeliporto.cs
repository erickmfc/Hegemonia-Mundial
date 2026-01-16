using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad] // Faz rodar assim que compila
public class GeradorDadosHeliporto
{
    static GeradorDadosHeliporto()
    {
        // Agenda a execução para o próximo frame do editor para garantir segurança
        EditorApplication.delayCall += GerarDados;
    }

    public static void GerarDados()
    {
        // Caminho exato da pasta mostrada na imagem
        string pastaPath = "Assets/Prefabs/Heliporto";
        
        // Garante que a pasta existe
        if (!System.IO.Directory.Exists(Application.dataPath + "/Prefabs/Heliporto"))
        {
            // Se a pasta não existir (estranho, pois a imagem mostra que existe), cria
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Prefabs/Heliporto");
            AssetDatabase.Refresh();
        }

        string arquivoPath = pastaPath + "/Dados_Heliporto.asset";

        // Verifica se já existe para não zerar configurações que você possa ter mudado
        DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(arquivoPath);
        
        bool novoArquivo = false;
        if (dados == null)
        {
            dados = ScriptableObject.CreateInstance<DadosConstrucao>();
            AssetDatabase.CreateAsset(dados, arquivoPath);
            novoArquivo = true;
        }

        // Configura os valores apenas se for novo ou se estiver sem nome
        if (novoArquivo || string.IsNullOrEmpty(dados.nomeItem))
        {
            dados.nomeItem = "Heliporto";
            dados.descricao = "Base para operações aéreas. Permite compra e reabastecimento de helicópteros.";
            dados.preco = 500;
            dados.categoria = DadosConstrucao.CategoriaItem.Infraestrutura;
            
            // Tenta achar o prefab HeliPad nessa mesma pasta ou no projeto
            if (dados.prefabDaUnidade == null)
            {
                string[] guids = AssetDatabase.FindAssets("HeliPad t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    dados.prefabDaUnidade = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Debug.Log("✅ [Gerador] Prefab 'HeliPad' vinculado automaticamente ao novo dado.");
                }
            }

            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            
            if(novoArquivo) Debug.Log("✅ [Gerador] Ficha 'Dados_Heliporto' criada com sucesso em: " + arquivoPath);
        }
    }
}
#endif
