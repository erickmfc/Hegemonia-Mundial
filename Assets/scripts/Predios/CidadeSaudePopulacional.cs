using System.Collections.Generic;
using UnityEngine;

public struct SaudePopulacionalResumo
{
    public int capacidadeHospitalar;
    public int populacaoAtendida;
    public int doentes;
    public float riscoDoenca;
    public float fatorCrescimento;
    public float mortalidadeExtra;
    public float penalidadeFelicidade;
    public float custoDiario;
}

/// <summary>
/// Doenças e cobertura hospitalar por cidade. É aditivo e não destrói a
/// cidade: a falta de saúde reduz crescimento, felicidade e população ao longo
/// do tempo, enquanto hospitais fornecem capacidade e geram manutenção.
/// </summary>
[DisallowMultipleComponent]
public sealed class CidadeSaudePopulacional : MonoBehaviour
{
    public int teamId = 1;
    public int capacidadeBase;
    public int doentesAtuais;
    [Range(0f, 1f)] public float riscoDoenca;
    public float custoDiario;

    private static readonly HashSet<CidadeSaudePopulacional> Cidades = new HashSet<CidadeSaudePopulacional>();
    private float proximaAtualizacao;

    private void Awake()
    {
        CidadeComplexoUrbano cidade = GetComponent<CidadeComplexoUrbano>();
        if (cidade != null)
        {
            teamId = cidade.teamId;
            if (capacidadeBase <= 0) capacidadeBase = Mathf.Max(0, cidade.capacidadeHospitalarInicial);
        }
    }

    private void OnEnable()
    {
        Cidades.Add(this);
    }

    private void OnDisable()
    {
        Cidades.Remove(this);
    }

    private void Update()
    {
        if (Time.unscaledTime < proximaAtualizacao) return;
        proximaAtualizacao = Time.unscaledTime + 5f;
        AtualizarIndicadores();
    }

    public void AtualizarIndicadores()
    {
        CidadeComplexoUrbano cidade = GetComponent<CidadeComplexoUrbano>();
        if (cidade == null) return;
        teamId = cidade.teamId;
        int populacao = Mathf.Max(0, cidade.populacaoResidente);
        int capacidade = Mathf.Max(0, capacidadeBase) + HospitalSaude.ObterCapacidadeTeam(teamId);
        float cobertura = populacao > 0 ? Mathf.Clamp01(capacidade / (float)populacao) : 1f;
        riscoDoenca = Mathf.Clamp01((1f - cobertura) * 0.85f + Mathf.Max(0f, populacao - capacidade) / Mathf.Max(1f, populacao) * 0.25f);
        doentesAtuais = Mathf.RoundToInt(populacao * riscoDoenca * 0.08f);
        custoDiario = HospitalSaude.ObterManutencaoTeam(teamId);
    }

    public static SaudePopulacionalResumo ObterResumo(int id, int populacaoPais)
    {
        SaudePopulacionalResumo resumo = new SaudePopulacionalResumo();
        int capacidade = HospitalSaude.ObterCapacidadeTeam(id);
        int doentes = 0;
        float riscoMaior = 0f;
        float custo = HospitalSaude.ObterManutencaoTeam(id);

        foreach (CidadeSaudePopulacional cidade in Cidades)
        {
            if (cidade == null || cidade.teamId != id) continue;
            cidade.AtualizarIndicadores();
            capacidade += Mathf.Max(0, cidade.capacidadeBase);
            doentes += Mathf.Max(0, cidade.doentesAtuais);
            riscoMaior = Mathf.Max(riscoMaior, cidade.riscoDoenca);
            custo += Mathf.Max(0f, cidade.custoDiario);
        }

        int populacao = Mathf.Max(0, populacaoPais);
        float cobertura = populacao > 0 ? Mathf.Clamp01(capacidade / (float)populacao) : 1f;
        float risco = Mathf.Clamp01(Mathf.Max(riscoMaior, (1f - cobertura) * 0.85f));
        resumo.capacidadeHospitalar = capacidade;
        resumo.populacaoAtendida = Mathf.Min(populacao, capacidade);
        resumo.doentes = Mathf.Max(doentes, Mathf.RoundToInt(populacao * risco * 0.08f));
        resumo.riscoDoenca = risco;
        resumo.fatorCrescimento = Mathf.Clamp(1f - risco * 0.65f, 0.20f, 1f);
        resumo.mortalidadeExtra = risco * 0.00035f;
        resumo.penalidadeFelicidade = risco * 8f;
        resumo.custoDiario = custo;
        return resumo;
    }
}
