using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MaterialNecessarioIndustrial
{
    public string recursoId = IndustriaIds.MinerioFerro;
    public double quantidade = 1000d;
}

[CreateAssetMenu(fileName = "ReceitaIndustrial", menuName = "Hegemonia/Industrial/Receita Industrial")]
public class ReceitaIndustrialSO : ScriptableObject
{
    [Header("Resultado")]
    public string id = IndustriaIds.AcoEstrutural;
    public string nome = "Aço estrutural";
    public string produtoFinalId = IndustriaIds.AcoEstrutural;
    public string unidadeResultado = "t";
    public double quantidadeProduzida = 750d;

    [Header("Entrada")]
    public List<MaterialNecessarioIndustrial> materiaisNecessarios = new List<MaterialNecessarioIndustrial>();

    [Header("Custos")]
    public int dinheiroNecessario = 500;
    public int energiaNecessaria = 120;
    public int diasNecessarios = 2;

    [Header("Requisitos")]
    public string pesquisaExigida = string.Empty;
    public int nivelIndustrialExigido = 1;

    [Header("Meta")]
    public bool requerLaboratorioNuclear;
    public bool materialEstrategico;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(produtoFinalId))
        {
            produtoFinalId = id;
        }
    }
}
