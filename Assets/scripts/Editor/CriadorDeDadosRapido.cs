using UnityEngine;
using UnityEditor;
using System.IO;

public class CriadorDeDadosRapido
{
    // Adiciona item ao menu de contexto dos Assets (Prefabs)
    [MenuItem("Assets/Hegemonia/Criar Ficha de Construcao (Auto)", false, 10)]
    public static void CriarFichaParaPrefab()
    {
        // 1. Obtém o objeto selecionado
        GameObject prefabSelecionado = Selection.activeGameObject;

        if (prefabSelecionado == null)
        {
            Debug.LogError("Selecione um PREFAB primeiro.");
            return;
        }

        string pathPrefab = AssetDatabase.GetAssetPath(prefabSelecionado);
        if (!pathPrefab.EndsWith(".prefab"))
        {
            Debug.LogError("O arquivo selecionado não é um Prefab!");
            return;
        }

        // 2. Define o nome e caminho da nova ficha
        string nomeAsset = "Dados_" + prefabSelecionado.name;
        string pastaDestino = "Assets/Resources/DadosConstrucao"; // Pasta padrão sugerida

        if (!Directory.Exists(Application.dataPath + "/Resources/DadosConstrucao"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources/DadosConstrucao");
            AssetDatabase.Refresh();
        }

        string pathFinal = $"{pastaDestino}/{nomeAsset}.asset";

        // Verifica se já existe
        DadosConstrucao dados = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(pathFinal);
        if (dados != null)
        {
            bool op = EditorUtility.DisplayDialog("Já existe", 
                $"A ficha {nomeAsset} já existe. Deseja sobrescrever (resetar) ou apenas atualizar o ícone?", 
                "Resetar Tudo", "Cancelar");
            if (!op) return;
        }
        else
        {
            dados = ScriptableObject.CreateInstance<DadosConstrucao>();
            AssetDatabase.CreateAsset(dados, pathFinal);
        }

        // 3. Preenche os Dados Automaticamente
        dados.nomeItem = prefabSelecionado.name.Replace("_", " ").Replace("Tank", "Tanque"); // Limpeza básica de nome
        dados.prefabDaUnidade = prefabSelecionado;
        dados.preco = 500; // Valor padrão
        dados.descricao = $"Unidade blindada modelo {prefabSelecionado.name}.";
        
        // Tenta adivinhar a categoria
        string nomeLower = prefabSelecionado.name.ToLower();
        if (nomeLower.Contains("tank") || nomeLower.Contains("c1") || nomeLower.Contains("apc")) 
            dados.categoria = DadosConstrucao.CategoriaItem.Exercito;
        else if (nomeLower.Contains("ship") || nomeLower.Contains("nav") || nomeLower.Contains("boat")) 
            dados.categoria = DadosConstrucao.CategoriaItem.Marinha;
        else if (nomeLower.Contains("heli") || nomeLower.Contains("aviao") || nomeLower.Contains("jet")) 
            dados.categoria = DadosConstrucao.CategoriaItem.Aeronautica;
        else 
            dados.categoria = DadosConstrucao.CategoriaItem.Urbana;

        // 4. Gera o Ícone na hora
        StudioDeIcones studio = ScriptableObject.CreateInstance<StudioDeIcones>();
        // Usa configurações padrão do studio
        Sprite iconeGerado = studio.GerarIcone(prefabSelecionado, prefabSelecionado.name);
        dados.icone = iconeGerado;

        // Salva
        EditorUtility.SetDirty(dados);
        AssetDatabase.SaveAssets();

        // Foca no arquivo criado
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = dados;

        Debug.Log($"<color=green>Sucesso!</color> Ficha '{nomeAsset}' criada e configurada com ícone!");
    }

    // Validação: Só habilita o menu se tiver um GameObject selecionado
    [MenuItem("Assets/Hegemonia/Criar Ficha de Construcao (Auto)", true)]
    static bool ValidateCriarFicha()
    {
        return Selection.activeGameObject != null;
    }
}
