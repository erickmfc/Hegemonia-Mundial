using System;
using UnityEngine;

public enum RecursoMercado
{
    Nenhum,
    Comida,
    Petroleo,
    Energia,
    Aco,
    Armamentos,
    Uranio
}

[Serializable]
public class DadosItemMercado
{
    public string id = "item";
    public string nome = "Item";
    public string categoria = "Recurso";
    public RecursoMercado recurso = RecursoMercado.Nenhum;
    public int precoBase = 100;
    public int precoAtual = 100;
    public int estoqueGlobal = 1000;
    public float oferta = 70f;
    public float demanda = 60f;
    public float volatilidade = 0.08f;
    public float variacaoPercentual;
    public bool podeComprar = true;
    public bool podeVender = true;

    public string NomeFormatado => string.IsNullOrEmpty(nome) ? id : nome;

    public int CalcularQuantidadePadrao()
    {
        if (recurso == RecursoMercado.Armamentos || recurso == RecursoMercado.Uranio) return 50;
        return 100;
    }
}
