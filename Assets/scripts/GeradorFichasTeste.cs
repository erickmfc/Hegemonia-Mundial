using UnityEngine;
using UnityEditor; 
using System.IO;

#if UNITY_EDITOR
public class GeradorFichasTeste : MonoBehaviour
{
    // Adiciona um botão no menu do topo do Unity: "Hegemonia"
    [MenuItem("Hegemonia/Gerar Fichas na Pasta Teste")]
    public static void GerarFichas()
    {
        // 1. Cria a pasta 'teste' se não existir
        if (!Directory.Exists("Assets/teste"))
        {
            AssetDatabase.CreateFolder("Assets", "teste");
        }

        // 2. Lista do que vamos criar
        // Militar
        Criar("Tanque de Batalha", 500, DadosConstrucao.CategoriaItem.Militar, "Tanque pesado principal.");
        Criar("Jipe de Recon", 250, DadosConstrucao.CategoriaItem.Militar, "Rápido mas frágil.");
        Criar("Artilharia", 700, DadosConstrucao.CategoriaItem.Militar, "Ataque de longa distância.");
        
        // Logística / Base
        Criar("Quartel General", 0, DadosConstrucao.CategoriaItem.Infraestrutura, "Centro de comando.");
        Criar("Hangar de Veiculos", 800, DadosConstrucao.CategoriaItem.Infraestrutura, "Permite criar tanques.");
        Criar("Torre de Radar", 400, DadosConstrucao.CategoriaItem.Infraestrutura, "Revela inimigos no mapa.");
        Criar("Muralha de Concreto", 50, DadosConstrucao.CategoriaItem.Infraestrutura, "Proteção passiva.");
        Criar("Torreta Terrestre", 450, DadosConstrucao.CategoriaItem.Infraestrutura, "Defesa de base com mísseis.");

        // 3. Atualiza o projeto para mostrar os arquivos
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Sucesso", "Fichas criadas na pasta 'Assets/teste'!\n\nAGORA: Selecione elas e arraste os Prefabs (modelos 3D) correspondentes.", "Entendido");
    }

    static void Criar(string nome, int preco, DadosConstrucao.CategoriaItem categoria, string desc)
    {
        // Cria uma nova instância da ficha na memória
        DadosConstrucao novaFicha = ScriptableObject.CreateInstance<DadosConstrucao>();
        novaFicha.nomeItem = nome;
        novaFicha.preco = preco;
        novaFicha.categoria = categoria;
        novaFicha.descricao = desc;

        // Salva no disco
        string caminho = "Assets/teste/" + nome.Replace(" ", "_") + ".asset";
        
        // Garante nome único
        caminho = AssetDatabase.GenerateUniqueAssetPath(caminho);

        AssetDatabase.CreateAsset(novaFicha, caminho);
    }
}
#endif
