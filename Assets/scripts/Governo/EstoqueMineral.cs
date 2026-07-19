using System;
using UnityEngine;

/// <summary>
/// Armazém virtual de minerais de um país.
/// Separado em matéria-prima bruta e materiais refinados.
/// Não existe no mapa — é apenas dados pertencentes ao país.
/// </summary>
[Serializable]
public class EstoqueMineral
{
    public int teamId;

    // ── MATÉRIA-PRIMA BRUTA (toneladas) ──────────────────────────────────
    [Header("Matéria-Prima Bruta (toneladas)")]
    public float minerioFerro       = 0f;
    public float minerioCobre       = 0f;
    public float bauxita            = 0f;
    public float minerioTitanio     = 0f;
    public float uranioBruto        = 0f;

    // ── MATERIAIS REFINADOS (toneladas) ──────────────────────────────────
    [Header("Materiais Refinados (toneladas)")]
    public float acoEstrutural              = 0f;
    public float cobreEletrolitico          = 0f;
    public float duraluminio               = 0f;
    public float ligaTitanio               = 0f;
    public float componentesEletronicos    = 0f;
    public float uranioEnriquecido         = 0f;

    // ── API DE ESTOQUE BRUTO ─────────────────────────────────────────────

    public float ObterBruto(RecursoMineral recurso)
    {
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   return minerioFerro;
            case RecursoMineral.MinerioCobre:   return minerioCobre;
            case RecursoMineral.Bauxita:        return bauxita;
            case RecursoMineral.MinerioTitanio: return minerioTitanio;
            case RecursoMineral.UranioBruto:    return uranioBruto;
            default: return 0f;
        }
    }

    public void AdicionarBruto(RecursoMineral recurso, float quantidade)
    {
        if (quantidade <= 0f) return;
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   minerioFerro    += quantidade; break;
            case RecursoMineral.MinerioCobre:   minerioCobre    += quantidade; break;
            case RecursoMineral.Bauxita:        bauxita         += quantidade; break;
            case RecursoMineral.MinerioTitanio: minerioTitanio  += quantidade; break;
            case RecursoMineral.UranioBruto:    uranioBruto     += quantidade; break;
        }
    }

    /// <returns>true se havia estoque suficiente e foi consumido.</returns>
    public bool ConsumirBruto(RecursoMineral recurso, float quantidade)
    {
        if (quantidade <= 0f) return true;
        float atual = ObterBruto(recurso);
        if (atual < quantidade) return false;
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   minerioFerro    -= quantidade; break;
            case RecursoMineral.MinerioCobre:   minerioCobre    -= quantidade; break;
            case RecursoMineral.Bauxita:        bauxita         -= quantidade; break;
            case RecursoMineral.MinerioTitanio: minerioTitanio  -= quantidade; break;
            case RecursoMineral.UranioBruto:    uranioBruto     -= quantidade; break;
        }
        return true;
    }

    // ── API DE MATERIAL REFINADO ─────────────────────────────────────────

    public float ObterRefinado(MaterialRefinado material)
    {
        switch (material)
        {
            case MaterialRefinado.AcoEstrutural:          return acoEstrutural;
            case MaterialRefinado.CobreEletrolitico:      return cobreEletrolitico;
            case MaterialRefinado.Duraluminio:            return duraluminio;
            case MaterialRefinado.LigaTitanio:            return ligaTitanio;
            case MaterialRefinado.ComponentesEletronicos: return componentesEletronicos;
            case MaterialRefinado.UranioEnriquecido:      return uranioEnriquecido;
            default: return 0f;
        }
    }

    public void AdicionarRefinado(MaterialRefinado material, float quantidade)
    {
        if (quantidade <= 0f) return;
        switch (material)
        {
            case MaterialRefinado.AcoEstrutural:          acoEstrutural           += quantidade; break;
            case MaterialRefinado.CobreEletrolitico:      cobreEletrolitico       += quantidade; break;
            case MaterialRefinado.Duraluminio:            duraluminio             += quantidade; break;
            case MaterialRefinado.LigaTitanio:            ligaTitanio             += quantidade; break;
            case MaterialRefinado.ComponentesEletronicos: componentesEletronicos  += quantidade; break;
            case MaterialRefinado.UranioEnriquecido:      uranioEnriquecido       += quantidade; break;
        }
    }

    /// <returns>true se havia estoque suficiente e foi consumido.</returns>
    public bool ConsumirRefinado(MaterialRefinado material, float quantidade)
    {
        if (quantidade <= 0f) return true;
        float atual = ObterRefinado(material);
        if (atual < quantidade) return false;
        switch (material)
        {
            case MaterialRefinado.AcoEstrutural:          acoEstrutural           -= quantidade; break;
            case MaterialRefinado.CobreEletrolitico:      cobreEletrolitico       -= quantidade; break;
            case MaterialRefinado.Duraluminio:            duraluminio             -= quantidade; break;
            case MaterialRefinado.LigaTitanio:            ligaTitanio             -= quantidade; break;
            case MaterialRefinado.ComponentesEletronicos: componentesEletronicos  -= quantidade; break;
            case MaterialRefinado.UranioEnriquecido:      uranioEnriquecido       -= quantidade; break;
        }
        return true;
    }

    /// <summary>Garante que nenhum valor fique negativo (segurança).</summary>
    public void Validar()
    {
        minerioFerro            = Mathf.Max(0f, minerioFerro);
        minerioCobre            = Mathf.Max(0f, minerioCobre);
        bauxita                 = Mathf.Max(0f, bauxita);
        minerioTitanio          = Mathf.Max(0f, minerioTitanio);
        uranioBruto             = Mathf.Max(0f, uranioBruto);
        acoEstrutural           = Mathf.Max(0f, acoEstrutural);
        cobreEletrolitico       = Mathf.Max(0f, cobreEletrolitico);
        duraluminio             = Mathf.Max(0f, duraluminio);
        ligaTitanio             = Mathf.Max(0f, ligaTitanio);
        componentesEletronicos  = Mathf.Max(0f, componentesEletronicos);
        uranioEnriquecido       = Mathf.Max(0f, uranioEnriquecido);
    }

    /// <summary>Retorna resumo textual do estoque para debug e UI.</summary>
    public string DescreverEstoque()
    {
        return $"[BRUTO] Fe={minerioFerro:N0}t Cu={minerioCobre:N0}t Al={bauxita:N0}t Ti={minerioTitanio:N0}t U={uranioBruto:N0}t | " +
               $"[REFINADO] Aço={acoEstrutural:N0}t CuRef={cobreEletrolitico:N0}t Dural={duraluminio:N0}t LiTi={ligaTitanio:N0}t Eletr={componentesEletronicos:N0}t UEn={uranioEnriquecido:N0}t";
    }
}
