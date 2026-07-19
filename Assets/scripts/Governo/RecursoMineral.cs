using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RECURSOS BRUTOS (extraídos virtualmente das minas)
// ============================================================
public enum RecursoMineral
{
    MinerioFerro,
    MinerioCobre,
    Bauxita,
    MinerioTitanio,
    UranioBruto
}

// ============================================================
//  MATERIAIS REFINADOS (produzidos pelas fábricas/refinarias)
// ============================================================
public enum MaterialRefinado
{
    AcoEstrutural,
    CobreEletrolitico,
    Duraluminio,
    LigaTitanio,
    ComponentesEletronicos,
    UranioEnriquecido
}

// ============================================================
//  NÍVEL DE ABUNDÂNCIA DO DEPÓSITO
// ============================================================
public enum AbundanciaMineralNivel
{
    Inexistente  = 0,
    MuitoEscasso = 1,
    Escasso      = 2,
    Baixo        = 3,
    Medio        = 4,
    Alto         = 5,
    Abundante    = 6
}

// ============================================================
//  PRODUÇÃO BASE POR NÍVEL DE ABUNDÂNCIA
//  (toneladas/dia antes de aplicar multiplicadores)
// ============================================================
public static class TabelaProducaoMineral
{
    // Produção base em toneladas por dia de jogo, por nível
    private static readonly Dictionary<AbundanciaMineralNivel, float> producaoBase =
        new Dictionary<AbundanciaMineralNivel, float>
    {
        { AbundanciaMineralNivel.Inexistente,   0f     },
        { AbundanciaMineralNivel.MuitoEscasso,  500f   },
        { AbundanciaMineralNivel.Escasso,       1000f  },
        { AbundanciaMineralNivel.Baixo,         2000f  },
        { AbundanciaMineralNivel.Medio,         4000f  },
        { AbundanciaMineralNivel.Alto,          6000f  },
        { AbundanciaMineralNivel.Abundante,     8000f  }
    };

    public static float ObterProducaoBase(AbundanciaMineralNivel nivel)
    {
        return producaoBase.TryGetValue(nivel, out float val) ? val : 0f;
    }

    // Limite máximo de produção diária (toneladas)
    public static float LimiteMaximo(RecursoMineral recurso, AbundanciaMineralNivel nivel)
    {
        if (recurso == RecursoMineral.UranioBruto)
        {
            // Apenas países com urânio Alto/Abundante chegam a 10.000 t
            return (nivel >= AbundanciaMineralNivel.Alto) ? 10000f : 2500f;
        }
        return 10000f;
    }

    public const float LimiteMinimo = 500f;
}

// ============================================================
//  RECEITAS DE REFINO
//  Define quanto de matéria-prima bruta é necessário para
//  produzir 1 unidade de material refinado, e o rendimento.
// ============================================================
[Serializable]
public class ReceitaRefino
{
    public MaterialRefinado resultado;
    // Matéria-prima primária
    public RecursoMineral materiaA;
    public float quantidadeA;       // toneladas necessárias de A
    // Matéria-prima secundária opcional (ex: componentes eletrônicos)
    public bool usaSegundaMateria;
    public RecursoMineral materiaB; // usada apenas quando usaSegundaMateria = true (material refinado)
    public float quantidadeB;
    [Range(0.01f, 1f)]
    public float rendimento = 0.7f; // % do input que vira output

    /// <summary>Identifica se a matéria B é um material refinado (não bruto).</summary>
    public bool materiaBasRefinada;
    public MaterialRefinado materiaBRefinada; // usado quando materiaBasRefinada = true
}

public static class TabelaRefino
{
    public static readonly List<ReceitaRefino> Receitas = new List<ReceitaRefino>
    {
        new ReceitaRefino
        {
            resultado   = MaterialRefinado.AcoEstrutural,
            materiaA    = RecursoMineral.MinerioFerro,
            quantidadeA = 1000f,
            rendimento  = 0.70f
        },
        new ReceitaRefino
        {
            resultado   = MaterialRefinado.CobreEletrolitico,
            materiaA    = RecursoMineral.MinerioCobre,
            quantidadeA = 1000f,
            rendimento  = 0.75f
        },
        new ReceitaRefino
        {
            resultado   = MaterialRefinado.Duraluminio,
            materiaA    = RecursoMineral.Bauxita,
            quantidadeA = 1000f,
            rendimento  = 0.60f
        },
        new ReceitaRefino
        {
            resultado   = MaterialRefinado.LigaTitanio,
            materiaA    = RecursoMineral.MinerioTitanio,
            quantidadeA = 1000f,
            rendimento  = 0.50f
        },
        new ReceitaRefino
        {
            // Eletrônicos: mistura Cobre Refinado + Duralumínio (ambos refinados)
            resultado         = MaterialRefinado.ComponentesEletronicos,
            materiaA          = RecursoMineral.MinerioCobre, // placeholder, não usado diretamente
            quantidadeA       = 0f,
            usaSegundaMateria = true,
            materiaBasRefinada= true,
            materiaBRefinada  = MaterialRefinado.CobreEletrolitico,
            quantidadeB       = 500f,
            rendimento        = 0.50f
        },
        new ReceitaRefino
        {
            resultado   = MaterialRefinado.UranioEnriquecido,
            materiaA    = RecursoMineral.UranioBruto,
            quantidadeA = 1000f,
            rendimento  = 0.20f
        }
    };
}
