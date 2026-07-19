using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

[Serializable]
public enum CategoriaProduto
{
    Desconhecido = 0,
    Agricultura = 1,
    Agroindustria = 2,
    Mineral = 3,
    Combustivel = 4,
    MaterialRefinado = 5,
    Componente = 6,
    Estrutura = 7,
    Infraestrutura = 8,
    UnidadeTerrestre = 9,
    UnidadeNaval = 10,
    UnidadeAerea = 11,
    Municao = 12,
    Missil = 13,
    Bomba = 14,
    Pesquisa = 15,
    Estrategico = 16,
    Servico = 17
}

[Serializable]
public sealed class IngredienteProduto
{
    public string recursoId = string.Empty;
    public double quantidade;
}

[Serializable]
public sealed class CatalogoProdutoUnificadoItem
{
    public string id = string.Empty;
    public string nome = string.Empty;
    [TextArea(2, 6)] public string descricao = string.Empty;
    public CategoriaProduto categoria = CategoriaProduto.Desconhecido;
    public string unidade = string.Empty;
    public List<IngredienteProduto> materiais = new List<IngredienteProduto>();
    public List<string> estruturasNecessarias = new List<string>();
    public List<string> pesquisasNecessarias = new List<string>();
    public double dinheiroNecessario;
    public double energiaNecessaria;
    public int diasProducao;
    public string linhaIndustrial = string.Empty;
    public string prefabId = string.Empty;
    public List<string> combustiveisAceitos = new List<string>();
    public List<string> municoesAceitas = new List<string>();
    public bool permiteProducaoAutomatica = true;
    public bool permiteCompraAutomatica = true;
    public bool permiteVendaAutomatica = true;
    public double estoqueMinimo;
    public double estoqueAlvo;
    public int prioridade = 1;
    [TextArea(2, 6)] public string dicaDesbloqueio = string.Empty;
    [TextArea(2, 4)] public string dicaMaterialFaltante = string.Empty;
    [TextArea(2, 4)] public string dicaEstruturaFaltante = string.Empty;
    [TextArea(2, 4)] public string dicaPesquisaFaltante = string.Empty;
    public List<string> aliases = new List<string>();

    public void Normalizar()
    {
        id = IA_Text.Normalize(id);
        nome = NormalizarTexto(nome);
        descricao = NormalizarTexto(descricao);
        unidade = NormalizarTexto(unidade);
        linhaIndustrial = IA_Text.Normalize(linhaIndustrial);
        prefabId = IA_Text.Normalize(prefabId);
        dicaDesbloqueio = NormalizarTexto(dicaDesbloqueio);
        dicaMaterialFaltante = NormalizarTexto(dicaMaterialFaltante);
        dicaEstruturaFaltante = NormalizarTexto(dicaEstruturaFaltante);
        dicaPesquisaFaltante = NormalizarTexto(dicaPesquisaFaltante);
        materiais = NormalizarMateriais(materiais);
        estruturasNecessarias = NormalizarLista(estruturasNecessarias);
        pesquisasNecessarias = NormalizarLista(pesquisasNecessarias);
        combustiveisAceitos = NormalizarLista(combustiveisAceitos);
        municoesAceitas = NormalizarLista(municoesAceitas);
        aliases = NormalizarLista(aliases);
    }

    private static string NormalizarTexto(string texto)
    {
        return string.IsNullOrWhiteSpace(texto) ? string.Empty : texto.Trim();
    }

    private static List<string> NormalizarLista(List<string> valores)
    {
        List<string> resultado = new List<string>();
        if (valores == null)
        {
            return resultado;
        }

        HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < valores.Count; i++)
        {
            string valor = IA_Text.Normalize(valores[i]);
            if (string.IsNullOrEmpty(valor) || !vistos.Add(valor))
            {
                continue;
            }

            resultado.Add(valor);
        }

        return resultado;
    }

    private static List<IngredienteProduto> NormalizarMateriais(List<IngredienteProduto> materiais)
    {
        List<IngredienteProduto> resultado = new List<IngredienteProduto>();
        if (materiais == null)
        {
            return resultado;
        }

        for (int i = 0; i < materiais.Count; i++)
        {
            IngredienteProduto ingrediente = materiais[i];
            if (ingrediente == null)
            {
                continue;
            }

            resultado.Add(new IngredienteProduto
            {
                recursoId = IA_Text.Normalize(ingrediente.recursoId),
                quantidade = ingrediente.quantidade
            });
        }

        return resultado;
    }
}

[CreateAssetMenu(fileName = "CatalogoProdutoUnificado", menuName = "Hegemonia/Catalogo/Produto Unificado")]
public sealed class CatalogoProdutoUnificadoSO : ScriptableObject
{
    [SerializeField] private List<CatalogoProdutoUnificadoItem> itens = new List<CatalogoProdutoUnificadoItem>();

    public IReadOnlyList<CatalogoProdutoUnificadoItem> Itens
    {
        get { return itens; }
    }

    public void Normalizar()
    {
        if (itens == null)
        {
            itens = new List<CatalogoProdutoUnificadoItem>();
            return;
        }

        for (int i = 0; i < itens.Count; i++)
        {
            CatalogoProdutoUnificadoItem item = itens[i];
            if (item == null)
            {
                continue;
            }

            item.Normalizar();
        }
    }
}
