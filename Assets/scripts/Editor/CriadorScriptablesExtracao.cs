using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility: cria automaticamente os ScriptableObjects de minérios padrão.
/// Menu: Hegemonia > Extração > Criar Minérios Padrão
/// </summary>
public static class CriadorScriptablesExtracao
{
    private const string PASTA = "Assets/Dados/Extrações";

    [MenuItem("Hegemonia/Extração/Criar Minérios Padrão")]
    public static void CriarMinériosPadrão()
    {
        // Garante que a pasta existe
        if (!AssetDatabase.IsValidFolder("Assets/Dados"))
            AssetDatabase.CreateFolder("Assets", "Dados");
        if (!AssetDatabase.IsValidFolder(PASTA))
            AssetDatabase.CreateFolder("Assets/Dados", "Extrações");

        CriarOuAtualizar("Minerio_Ferro",     "Ferro",       TipoRecursoExtracao.Ferro,
            custoDinheiro: 400,  custoEnergia: 50,
            producaoMin: 5200f,  producaoMax: 7800f,
            exigeAutorizacao: false,
            restricao: "");

        CriarOuAtualizar("Minerio_Cobre",     "Cobre",       TipoRecursoExtracao.Cobre,
            custoDinheiro: 650,  custoEnergia: 70,
            producaoMin: 1300f,  producaoMax: 3100f,
            exigeAutorizacao: false,
            restricao: "");

        CriarOuAtualizar("Minerio_Bauxita",   "Bauxita",     TipoRecursoExtracao.Bauxita,
            custoDinheiro: 850,  custoEnergia: 90,
            producaoMin: 800f,   producaoMax: 2200f,
            exigeAutorizacao: false,
            restricao: "");

        CriarOuAtualizar("Minerio_Titanio",   "Titânio",     TipoRecursoExtracao.Titanio,
            custoDinheiro: 2000, custoEnergia: 180,
            producaoMin: 500f,   producaoMax: 1400f,
            exigeAutorizacao: false,
            restricao: "");

        CriarOuAtualizar("Minerio_Uranio",    "Urânio Bruto", TipoRecursoExtracao.Uranio,
            custoDinheiro: 5000, custoEnergia: 350,
            producaoMin: 50f,    producaoMax: 300f,
            exigeAutorizacao: true,
            restricao: "Exige autorização governamental");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "✅ Minérios Criados",
            $"Os 5 ScriptableObjects de minério foram criados/atualizados em:\n{PASTA}",
            "OK");

        Debug.Log($"[CriadorScriptablesExtracao] ✅ ScriptableObjects criados em: {PASTA}");
    }

    static void CriarOuAtualizar(string nomeArquivo, string nomeRecurso, TipoRecursoExtracao tipo,
        int custoDinheiro, int custoEnergia,
        float producaoMin, float producaoMax,
        bool exigeAutorizacao, string restricao)
    {
        string caminho = $"{PASTA}/{nomeArquivo}.asset";
        DadosTipoMinerio asset = AssetDatabase.LoadAssetAtPath<DadosTipoMinerio>(caminho);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DadosTipoMinerio>();
            AssetDatabase.CreateAsset(asset, caminho);
            Debug.Log($"[CriadorScriptablesExtracao] Criado: {caminho}");
        }
        else
        {
            Debug.Log($"[CriadorScriptablesExtracao] Atualizado: {caminho}");
        }

        asset.nomeRecurso        = nomeRecurso;
        asset.tipoExtracao       = tipo;
        asset.custoDinheiro      = custoDinheiro;
        asset.custoEnergia       = custoEnergia;
        asset.duracaoEmDias      = 1;
        asset.producaoMinima     = producaoMin;
        asset.producaoMaxima     = producaoMax;
        asset.exigeAutorizacao   = exigeAutorizacao;
        asset.descricaoRestricao = restricao;

        EditorUtility.SetDirty(asset);
    }
}
#endif
