using UnityEngine;

[System.Serializable]
public class EstadoIndustrialPais
{
    public string paisId;
    public int teamId;
    public string nomePais;
    public float eficienciaIndustrial;
    public float energiaDisponivel;
    public float estabilidadeNacional;
    public float investimentoIndustrial;
    public float capacidadeIndustrial;
    public int nivelFabrica;
    public int linhasDisponiveis;
    public int linhasOcupadas;
    public int ordensAtivas;
    public float producaoDiariaTotal;
    public double valorEstoqueTotal;
    public float dependenciaImportacoes;
    public bool emGuerra;
    public bool sancionado;
    public float felicidade;
    public string resumo;

    public void Atualizar(DadosPaisGoverno pais, int nivelFabrica, int linhasDisponiveis, int linhasOcupadas, int ordensAtivas, float producaoDiariaTotal, double valorEstoqueTotal, float dependenciaImportacoes)
    {
        if (pais == null)
        {
            paisId = string.Empty;
            nomePais = string.Empty;
            return;
        }

        paisId = pais.teamId.ToString();
        teamId = pais.teamId;
        nomePais = pais.nomePais;
        eficienciaIndustrial = Mathf.Clamp01(pais.nivelIndustrial / 100f);
        energiaDisponivel = Mathf.Clamp01(pais.energia / 200f);
        estabilidadeNacional = Mathf.Clamp01(pais.estabilidade / 100f);
        investimentoIndustrial = Mathf.Clamp01((pais.saldo / 50000f) + (pais.nivelEconomico / 150f));
        capacidadeIndustrial = Mathf.Clamp01(pais.nivelIndustrial / 100f);
        this.nivelFabrica = nivelFabrica;
        this.linhasDisponiveis = linhasDisponiveis;
        this.linhasOcupadas = linhasOcupadas;
        this.ordensAtivas = ordensAtivas;
        this.producaoDiariaTotal = producaoDiariaTotal;
        this.valorEstoqueTotal = valorEstoqueTotal;
        this.dependenciaImportacoes = Mathf.Clamp01(dependenciaImportacoes);
        emGuerra = pais.emGuerra;
        sancionado = pais.sancionado;
        felicidade = pais.felicidade;
        resumo = $"{nomePais}: eficiência {eficienciaIndustrial:P0}, linhas {linhasOcupadas}/{linhasDisponiveis}, produção {producaoDiariaTotal:N0} t";
    }
}
