using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy automatic production component. Only legacy factories remain active;
/// food production is owned by AgriculturaNacional.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProducaoAutomaticaEdificio : MonoBehaviour
{
    public enum TipoInstalacao
    {
        Fazenda,
        Fabrica
    }

    [SerializeField] private TipoInstalacao tipo;
    [SerializeField] private float intervaloFabrica = 24f;
    [SerializeField] private bool mostrarLogs;

    private int teamId;
    private System.Random aleatorio;
    private bool avisouGovernoAusente;
    private bool avisouIndustriaAusente;
    private int ciclosConcluidos;
    private string ultimoDestaque = "-";
    private int ultimaQuantidade;

    public TipoInstalacao Tipo => tipo;
    public int TeamId => teamId;
    public int CiclosConcluidos => ciclosConcluidos;
    public string UltimoDestaque => ultimoDestaque;
    public int UltimaQuantidade => ultimaQuantidade;

    public static ProducaoAutomaticaEdificio Garantir(GameObject alvo, TipoInstalacao novoTipo)
    {
        if (alvo == null || Construtor.CriandoPreviewConstrucao)
        {
            return null;
        }

        ProducaoAutomaticaEdificio controller = alvo.GetComponent<ProducaoAutomaticaEdificio>();
        if (controller == null)
        {
            controller = alvo.AddComponent<ProducaoAutomaticaEdificio>();
        }

        controller.tipo = novoTipo;
        controller.teamId = controller.ResolverTeamId();
        return controller;
    }

    private void Awake()
    {
        aleatorio = new System.Random(GetInstanceID() ^ DateTime.UtcNow.Millisecond);
        teamId = ResolverTeamId();
    }

    private void Start()
    {
        teamId = ResolverTeamId();
        if (tipo == TipoInstalacao.Fazenda) return;
        float intervalo = intervaloFabrica;
        float primeiroCiclo = Mathf.Clamp(intervalo * 0.35f, 2f, 8f);
        float repeticao = Mathf.Max(5f, intervalo);
        InvokeRepeating(nameof(ProcessarCiclo), primeiroCiclo, repeticao);
    }

    private void ProcessarCiclo()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || tipo == TipoInstalacao.Fazenda)
        {
            return;
        }

        teamId = ResolverTeamId();
        if (teamId <= 0)
        {
            return;
        }

        ProduzirFabrica();
    }

    private void ProduzirFabrica()
    {
        SistemaIndustrialNacional industria = SistemaIndustrialNacional.Instancia;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (industria == null)
        {
            AvisarUmaVez(ref avisouIndustriaAusente, "sistema industrial ainda nao esta pronto");
            return;
        }

        string[] materiais = IndustriaIds.TodosOsMateriais;
        int dominante = aleatorio.Next(0, materiais.Length);
        List<QuantidadeRecursoIndustrial> lote = new List<QuantidadeRecursoIndustrial>(materiais.Length);
        int total = 0;
        for (int i = 0; i < materiais.Length; i++)
        {
            float fator = Mathf.Lerp(0.45f, 1.30f, (float)aleatorio.NextDouble());
            if (i == dominante)
            {
                fator *= Mathf.Lerp(2.00f, 2.70f, (float)aleatorio.NextDouble());
            }

            int quantidade = Mathf.Max(1, Mathf.RoundToInt(2f * fator));
            lote.Add(new QuantidadeRecursoIndustrial(materiais[i], quantidade));
            total += quantidade;
            AtualizarOfertaMercado(mercado, materiais[i], quantidade);
        }

        industria.AdicionarProducaoAutomatica(teamId, lote);
        ciclosConcluidos++;
        ultimoDestaque = materiais[dominante];
        ultimaQuantidade = total;
        RegistrarResumo("fabrica", total);
    }

    private static void AtualizarOfertaMercado(SistemaMercadoGlobal mercado, string itemId, int quantidade)
    {
        if (mercado == null || quantidade <= 0) return;
        DadosItemMercado item = mercado.ObterItem(itemId);
        if (item == null) return;
        item.estoqueGlobal = Mathf.Clamp(item.estoqueGlobal + quantidade, 0, 2000000000);
        item.oferta = Mathf.Clamp(item.oferta + quantidade * 0.02f, 0f, 160f);
    }

    private int ResolverTeamId()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>()
            ?? GetComponentInParent<IdentidadeUnidade>()
            ?? GetComponentInChildren<IdentidadeUnidade>(true);
        if (identidade != null && identidade.teamID > 0) return identidade.teamID;

        EstruturaEconomica estrutura = GetComponent<EstruturaEconomica>();
        if (estrutura != null && estrutura.teamId > 0) return estrutura.teamId;

        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        return governo != null ? Mathf.Max(1, governo.teamJogador) : 1;
    }

    private void AvisarUmaVez(ref bool avisou, string motivo)
    {
        if (avisou) return;
        avisou = true;
        Debug.LogWarning("[ProducaoAutomatica] " + name + ": " + motivo + ".");
    }

    private void RegistrarResumo(string nome, int total)
    {
        if (!mostrarLogs) return;
        Debug.Log("[ProducaoAutomatica] " + nome + " team=" + teamId + " total=" + total + " destaque=" + ultimoDestaque);
    }
}
