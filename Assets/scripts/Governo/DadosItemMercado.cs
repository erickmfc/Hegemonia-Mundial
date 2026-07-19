using System;
using UnityEngine;
using Hegemonia.AI.BrainMaster;

public enum RecursoMercado
{
    Nenhum,
    Comida,
    Petroleo,
    Energia,
    Aco,
    Armamentos,
    Uranio,
    MinerioFerro,
    MinerioCobre,
    Bauxita,
    MinerioTitanio,
    CobreEletrolitico,
    Duraluminio,
    LigaTitanio,
    ComponentesEletronicos,
    UranioEnriquecido
}

[Serializable]
public class DadosItemMercado
{
    public string id = "item";
    public string nome = "Item";
    public string categoria = "Recurso";
    public RecursoMercado recurso = RecursoMercado.Nenhum;
    public string recursoId = string.Empty;
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
    public string RecursoIdEfetivo
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(recursoId))
            {
                return IA_Text.Normalize(recursoId);
            }

            string recursoInterno = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
            if (!string.IsNullOrWhiteSpace(recursoInterno))
            {
                return IA_Text.Normalize(recursoInterno);
            }

            return IA_Text.Normalize(id);
        }
    }

    public int CalcularQuantidadePadrao()
    {
        string recursoInterno = RecursoIdEfetivo;
        if (IndustriaIds.EhIndustrial(recursoInterno))
        {
            if (IndustriaIds.EhRecursoBruto(recursoInterno) || IndustriaIds.EhCombustivelIndustrial(recursoInterno))
            {
                return 100;
            }

            if (IndustriaIds.EhComponenteIndustrial(recursoInterno) || IndustriaIds.EhMaterialRefinado(recursoInterno))
            {
                return 50;
            }
        }

        if (recurso == RecursoMercado.Armamentos ||
            recurso == RecursoMercado.Uranio ||
            recurso == RecursoMercado.UranioEnriquecido ||
            recurso == RecursoMercado.LigaTitanio ||
            recurso == RecursoMercado.ComponentesEletronicos)
        {
            return 50;
        }

        return 100;
    }
}
