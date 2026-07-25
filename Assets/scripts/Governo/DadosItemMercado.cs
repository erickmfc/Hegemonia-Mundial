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

    // Preenchido para ofertas de equipamento militar. O item continua no
    // mesmo mercado global, mas a liquidacao cria a unidade e a entrega.
    public bool equipamentoMilitar;
    public string prefabId = string.Empty;
    public string tipoEntrega = string.Empty;

    // Oferta de munição produzida/armazenada por países. Diferente de um
    // equipamento, a liquidação apenas transfere cartuchos para o estoque.
    public bool municaoMilitar;
    public string idMunicaoMilitar = string.Empty;

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
        if (municaoMilitar)
        {
            return 10;
        }
        if (equipamentoMilitar)
        {
            return 1;
        }
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
