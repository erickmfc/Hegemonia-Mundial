using System;
using UnityEngine;

public enum TipoEstruturaEconomica
{
    Casa,
    Industria,
    Petroleo,
    Comercio,
    Farm,
    Energia,
    PesquisaMilitar,
    UsinaSolar,
    CasaPopular,
    PredioResidencial,
    ComercioPequeno,
    Shopping,
    IndustriaLeve,
    IndustriaPesada,
    Refinaria,
    PortoComercial,
    AeroportoCivil,
    UsinaTermicaPequena,
    UsinaTermicaGrande,
    UsinaNuclear,
    UsinaHidreletrica,
    BaseMilitarPequena,
    BaseMilitarMedia,
    GrandeBaseMilitar,
    BaseAerea,
    BaseNaval
}

public enum StatusEstruturaEconomica
{
    Ativa,
    Inativa,
    Danificada,
    SemEnergia,
    SemTrabalhadores,
    Destruida
}

[Serializable]
public class DadosEconomiaPais
{
    public int teamId;
    public int estruturasContadas;
    public int casas;
    public int industrias;
    public int pocosPetroleo;
    public int comercios;
    public int farms;
    public int usinas;
    public int estruturasSemEnergia;

    public int moradiaTotal;
    public int populacaoTotal;
    public int empregosDisponiveis;
    public int empregosOcupados;

    public float comidaProduzida;
    public float petroleoProduzido;
    public float industriaProduzida;
    public float energiaProduzida;
    public float energiaConsumida;
    public float combustivelConsumido;
    public int militaresNecessarios;
    public float dinheiroGerado;
    public float receitaMoradia;
    public float receitaIndustria;
    public float receitaComercio;
    public float receitaEnergia;
    public float custoManutencao;
    public float saldoOperacional;

    public float deficitComida;
    public float deficitEnergia;
    public float deficitPetroleo;
    public float deficitEmprego;
    public float qualidadeVida = 50f;
    public float pressaoPopulacional;
    public float eficienciaMedia = 1f;
    public float exportacaoTotal;
    public float importacaoTotal;

    public float CoberturaMoradia
    {
        get { return moradiaTotal <= 0 ? 0f : Mathf.Clamp01(populacaoTotal / (float)moradiaTotal); }
    }

    public float TaxaEmprego
    {
        get { return populacaoTotal <= 0 ? 1f : Mathf.Clamp01(empregosOcupados / (float)Mathf.Max(1, populacaoTotal)); }
    }

    public string DeficitPrincipal
    {
        get
        {
            if (deficitEnergia > 0.5f) return "Energia";
            if (deficitComida > 0.5f) return "Comida";
            if (deficitPetroleo > 0.5f) return "Petroleo";
            if (deficitEmprego > 0.5f) return "Emprego";
            return "Nenhum";
        }
    }

    public string ProducaoPrincipal
    {
        get
        {
            float maior = comidaProduzida;
            string nome = "Comida";
            if (petroleoProduzido > maior) { maior = petroleoProduzido; nome = "Petroleo"; }
            if (industriaProduzida > maior) { maior = industriaProduzida; nome = "Industria"; }
            if (energiaProduzida > maior) { nome = "Energia"; }
            return nome;
        }
    }

    public float ReceitaBruta
    {
        get { return receitaMoradia + receitaIndustria + receitaComercio + receitaEnergia; }
    }
}
