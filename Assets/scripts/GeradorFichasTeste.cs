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

        // ============================================
        // EXÉRCITO (Tropas Terrestres)
        // ============================================
        Criar("Tanque de Batalha", 500, DadosConstrucao.CategoriaItem.Exercito, "Tanque pesado principal.");
        Criar("Jipe de Recon", 250, DadosConstrucao.CategoriaItem.Exercito, "Rápido mas frágil.");
        Criar("Artilharia", 700, DadosConstrucao.CategoriaItem.Exercito, "Ataque de longa distância.");
        Criar("Soldado Rifle", 100, DadosConstrucao.CategoriaItem.Exercito, "Infantaria básica.");
        
        // ============================================
        // MARINHA (Navios e Submarinos)
        // ============================================
        Criar("USS Sovereign", 5000, DadosConstrucao.CategoriaItem.Marinha, "Porta-aviões de classe soberana.");
        Criar("USS Dominion", 3500, DadosConstrucao.CategoriaItem.Marinha, "Cruzador de batalha pesado.");
        Criar("USS Vindicator", 2500, DadosConstrucao.CategoriaItem.Marinha, "Destroyer de escolta.");
        Criar("USS Liberty Prime", 2000, DadosConstrucao.CategoriaItem.Marinha, "Fragata de ataque rápido.");
        Criar("USS Mako", 3000, DadosConstrucao.CategoriaItem.Marinha, "Submarino de ataque furtivo.");
        Criar("USS Ironclad", 4000, DadosConstrucao.CategoriaItem.Marinha, "Encouraçado blindado.");
        Criar("USS Arrowhead", 1200, DadosConstrucao.CategoriaItem.Marinha, "Lancha de patrulha rápida.");
        Criar("USS Leviathan", 4500, DadosConstrucao.CategoriaItem.Marinha, "Submarino nuclear estratégico.");
        Criar("USS Wraith", 3800, DadosConstrucao.CategoriaItem.Marinha, "Submarino furtivo de elite.");
        Criar("Corveta Harry", 1500, DadosConstrucao.CategoriaItem.Marinha, "Corveta de patrulha costeira.");
        
        // ============================================
        // AERONÁUTICA (Aviões e Helicópteros)
        // ============================================
        Criar("Helicóptero de Ataque", 1800, DadosConstrucao.CategoriaItem.Aeronautica, "Helicóptero artilhado.");
        Criar("UAV Reconhecimento", 800, DadosConstrucao.CategoriaItem.Aeronautica, "Drone de vigilância.");
        
        // ============================================
        // INFRAESTRUTURA (Prédios de Base)
        // ============================================
        Criar("Quartel General", 0, DadosConstrucao.CategoriaItem.Infraestrutura, "Centro de comando.");
        Criar("Hangar de Veículos", 800, DadosConstrucao.CategoriaItem.Infraestrutura, "Permite criar tanques.");
        Criar("Estaleiro Naval", 2500, DadosConstrucao.CategoriaItem.Infraestrutura, "Permite criar navios.");
        Criar("Tenda Militar", 300, DadosConstrucao.CategoriaItem.Infraestrutura, "Permite treinar soldados.");
        Criar("Torre de Radar", 400, DadosConstrucao.CategoriaItem.Infraestrutura, "Revela inimigos no mapa.");
        Criar("Muro Reto", 50, DadosConstrucao.CategoriaItem.Infraestrutura, "Proteção passiva.");
        Criar("Muro Lateral", 50, DadosConstrucao.CategoriaItem.Infraestrutura, "Proteção passiva em curva.");
        Criar("Torreta Terrestre", 450, DadosConstrucao.CategoriaItem.Infraestrutura, "Defesa de base com mísseis.");
        
        // ============================================
        // TECNOLOGIA (Pesquisas e Upgrades)
        // ============================================
        Criar("Blindagem Aprimorada", 1000, DadosConstrucao.CategoriaItem.Tecnologia, "Aumenta a resistência das unidades.");
        Criar("Munição Perfurante", 800, DadosConstrucao.CategoriaItem.Tecnologia, "Aumenta o dano de projéteis.");
        
        // 3. Atualiza o projeto para mostrar os arquivos
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Sucesso", "Fichas atualizadas na pasta 'Assets/teste'!\n\nAGORA: Selecione elas e arraste os Prefabs (modelos 3D) correspondentes.", "Entendido");
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
        
        // Garante nome único (não sobrescreve existentes)
        caminho = AssetDatabase.GenerateUniqueAssetPath(caminho);

        AssetDatabase.CreateAsset(novaFicha, caminho);
    }
}
#endif
