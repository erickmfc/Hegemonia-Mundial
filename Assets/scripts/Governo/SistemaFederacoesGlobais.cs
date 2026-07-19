using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class EmprestimoFederativoEstado
{
    public string id = string.Empty;
    public int credorTeamId;
    public int devedorTeamId;
    public string federacao = string.Empty;
    public float principal;
    public float saldoDevedor;
    public float jurosPorTick = 0.02f;
    public bool ajusteEstrutural;
    public bool creditoMilitar;
    public bool inadimplente;
    public int ticksRestantes = 30;
}

/// <summary>Federações globais, legitimidade e crédito. O estado é por país e não usa singleton para a IA.</summary>
public sealed class SistemaFederacoesGlobais : MonoBehaviour
{
    public enum TipoFederacao { CooperacaoGlobal, AliancaDefesa }
    public static SistemaFederacoesGlobais Instancia { get; private set; }
    public float intervaloTick = 3f;
    public float multaTrocaFederacao = 12f;
    private float proximoTick;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;
        SistemaFederacoesGlobais existente = FindFirstObjectByType<SistemaFederacoesGlobais>();
        if (existente != null) { Instancia = existente; return; }
        GameObject go = new GameObject("SistemaFederacoesGlobais_Runtime");
        Instancia = go.AddComponent<SistemaFederacoesGlobais>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
        GarantirFiliacoes();
    }

    private void Update()
    {
        if (Time.unscaledTime < proximoTick) return;
        proximoTick = Time.unscaledTime + Mathf.Max(1f, intervaloTick);
        GarantirFiliacoes();
        ProcessarEmprestimos();
    }

    public void GarantirFiliacoes()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null || governo.paises == null) return;
        int cooperacao = governo.paises.Count(p => p != null && p.federacaoGlobal == TipoFederacao.CooperacaoGlobal.ToString());
        int defesa = governo.paises.Count(p => p != null && p.federacaoGlobal == TipoFederacao.AliancaDefesa.ToString());
        foreach (DadosPaisGoverno pais in governo.paises.OrderBy(p => p != null ? p.teamId : int.MaxValue))
        {
            if (pais == null || !string.IsNullOrWhiteSpace(pais.federacaoGlobal)) continue;
            bool entraCooperacao = cooperacao <= defesa;
            pais.federacaoGlobal = entraCooperacao ? TipoFederacao.CooperacaoGlobal.ToString() : TipoFederacao.AliancaDefesa.ToString();
            pais.legitimidadeGlobal = Mathf.Clamp(pais.legitimidadeGlobal <= 0f ? 70f : pais.legitimidadeGlobal, 0f, 100f);
            if (entraCooperacao) cooperacao++; else defesa++;
            governo.RegistrarNoticia(pais.nomePais + " filiou-se a " + NomeFederacao(pais.federacaoGlobal) + ".");
        }
    }

    public bool TrocarFederacao(int teamId, string destino, out string mensagem)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
        if (pais == null || string.IsNullOrWhiteSpace(destino)) { mensagem = "pais ou federacao invalida"; return false; }
        if (pais.federacaoGlobal == destino) { mensagem = "pais ja pertence a esta federacao"; return false; }
        pais.federacaoGlobal = destino;
        pais.legitimidadeGlobal = Mathf.Max(0f, pais.legitimidadeGlobal - multaTrocaFederacao);
        mensagem = "filiacao alterada; legitimidade reduzida";
        return true;
    }

    public bool SolicitarEmprestimo(int credorTeamId, int devedorTeamId, float valor, bool creditoMilitar, out string mensagem)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno credor = governo != null ? governo.ObterPais(credorTeamId) : null;
        DadosPaisGoverno devedor = governo != null ? governo.ObterPais(devedorTeamId) : null;
        if (credor == null || devedor == null || credorTeamId == devedorTeamId || valor <= 0f) { mensagem = "emprestimo invalido"; return false; }
        if (creditoMilitar && devedor.federacaoGlobal != TipoFederacao.AliancaDefesa.ToString()) { mensagem = "credito militar reservado a Alianca de Defesa"; return false; }
        if (credor.saldo < valor) { mensagem = "credor sem fundos"; return false; }
        credor.saldo -= Mathf.RoundToInt(valor);
        devedor.saldo += Mathf.RoundToInt(valor);
        if (devedor.emprestimos == null) devedor.emprestimos = new List<EmprestimoFederativoEstado>();
        devedor.emprestimos.Add(new EmprestimoFederativoEstado
        {
            id = "loan:" + credorTeamId + ":" + devedorTeamId + ":" + Mathf.FloorToInt(Time.unscaledTime),
            credorTeamId = credorTeamId,
            devedorTeamId = devedorTeamId,
            federacao = devedor.federacaoGlobal,
            principal = valor,
            saldoDevedor = valor,
            creditoMilitar = creditoMilitar,
            ajusteEstrutural = !creditoMilitar,
            ticksRestantes = 30
        });
        mensagem = "emprestimo aprovado";
        return true;
    }

    public bool QuitarEmprestimo(int teamId, string loanId, out string mensagem)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
        EmprestimoFederativoEstado loan = pais != null && pais.emprestimos != null ? pais.emprestimos.FirstOrDefault(x => x != null && x.id == loanId) : null;
        if (pais == null || loan == null) { mensagem = "emprestimo nao encontrado"; return false; }
        if (pais.saldo < Mathf.CeilToInt(loan.saldoDevedor)) { mensagem = "saldo insuficiente"; return false; }
        pais.saldo -= Mathf.CeilToInt(loan.saldoDevedor);
        pais.emprestimos.Remove(loan);
        mensagem = "emprestimo quitado";
        return true;
    }

    private void ProcessarEmprestimos()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return;
        foreach (DadosPaisGoverno pais in governo.paises)
        {
            if (pais == null || pais.emprestimos == null) continue;
            foreach (EmprestimoFederativoEstado loan in pais.emprestimos.ToList())
            {
                if (loan == null) continue;
                loan.saldoDevedor += loan.saldoDevedor * loan.jurosPorTick;
                loan.ticksRestantes--;
                if (loan.ajusteEstrutural) pais.pesoMilitarismo = Mathf.Max(0f, pais.pesoMilitarismo - 0.002f);
                if (loan.ticksRestantes <= 0 && pais.saldo <= 0) { loan.inadimplente = true; pais.legitimidadeGlobal = Mathf.Max(0f, pais.legitimidadeGlobal - 8f); pais.sancionado = true; }
            }
        }
    }

    public static string NomeFederacao(string id)
    {
        return id == TipoFederacao.AliancaDefesa.ToString() ? "Coalizao Aegis" : "Concordia Global";
    }
}
