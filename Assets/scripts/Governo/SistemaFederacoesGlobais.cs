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
    private const int PrazoEmprestimoPadraoTicks = 30;
    private const float JurosEmprestimoPadraoPorTick = 0.02f;
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
        if (Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;
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
            if (pais == null || EhFederacaoValida(pais.federacaoGlobal)) continue;
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
        if (pais == null || !EhFederacaoValida(destino)) { mensagem = "pais ou federacao invalida"; return false; }
        if (pais.federacaoGlobal == destino) { mensagem = "pais ja pertence a esta federacao"; return false; }
        pais.federacaoGlobal = destino;
        pais.legitimidadeGlobal = Mathf.Max(0f, pais.legitimidadeGlobal - multaTrocaFederacao);
        SistemaGovernoMundial.Instancia.NotificarGovernoAtualizado();
        mensagem = "filiacao alterada; legitimidade reduzida";
        return true;
    }

    public static bool EhFederacaoValida(string id)
    {
        return Enum.TryParse(id, false, out TipoFederacao tipo)
            && Enum.IsDefined(typeof(TipoFederacao), tipo);
    }

    public bool SolicitarEmprestimo(int credorTeamId, int devedorTeamId, float valor, bool creditoMilitar, out string mensagem)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno credor = governo != null ? governo.ObterPais(credorTeamId) : null;
        DadosPaisGoverno devedor = governo != null ? governo.ObterPais(devedorTeamId) : null;
        if (credor == null || devedor == null || credorTeamId == devedorTeamId
            || float.IsNaN(valor) || float.IsInfinity(valor) || valor <= 0f || valor > int.MaxValue)
        {
            mensagem = "emprestimo invalido";
            return false;
        }

        if (!EhFederacaoValida(credor.federacaoGlobal)
            || !string.Equals(credor.federacaoGlobal, devedor.federacaoGlobal, StringComparison.Ordinal))
        {
            mensagem = "o credor e o devedor precisam pertencer ao mesmo bloco";
            return false;
        }

        if (creditoMilitar && devedor.federacaoGlobal != TipoFederacao.AliancaDefesa.ToString())
        {
            mensagem = "credito militar reservado a Alianca de Defesa";
            return false;
        }

        RelacaoPaisGoverno relacao = governo.relacoes != null
            ? governo.relacoes.FirstOrDefault(r => r != null && r.Envolve(credorTeamId, devedorTeamId))
            : null;
        if (credor.emGuerra && credor.rivalTeamId == devedorTeamId
            || devedor.emGuerra && devedor.rivalTeamId == credorTeamId
            || relacao != null && (relacao.guerraDeclarada || relacao.sancaoAtiva || relacao.valor < -25))
        {
            mensagem = "a situacao diplomatica nao permite novo credito";
            return false;
        }

        if (devedor.emprestimos != null && devedor.emprestimos.Any(loan => loan != null && loan.inadimplente))
        {
            mensagem = "o devedor possui emprestimo inadimplente";
            return false;
        }

        if (!PossuiNecessidadeDeCredito(devedor, creditoMilitar))
        {
            mensagem = "o pais nao apresenta necessidade atual para este credito";
            return false;
        }

        long valorArredondado = Mathf.RoundToInt(valor);
        long reservaCredor = CalcularReservaFinanceira(credor);
        if (credor.saldo - reservaCredor < valorArredondado)
        {
            mensagem = "o credor precisa manter sua reserva financeira";
            return false;
        }

        long capacidadePagamento = CalcularCapacidadePagamento(devedor);
        double saldoPrevisto = valorArredondado * Math.Pow(1d + JurosEmprestimoPadraoPorTick, PrazoEmprestimoPadraoTicks);
        double dividasExistentes = (devedor.divida > 0f ? devedor.divida : 0f)
            + (devedor.emprestimos != null
                ? devedor.emprestimos.Where(loan => loan != null && !loan.inadimplente)
                    .Sum(loan => Math.Max(0f, loan.saldoDevedor))
                : 0f);
        if (saldoPrevisto + dividasExistentes > capacidadePagamento)
        {
            mensagem = "a capacidade estimada de pagamento nao cobre este emprestimo";
            return false;
        }

        int ticks = PrazoEmprestimoPadraoTicks;
        float jurosPorTick = JurosEmprestimoPadraoPorTick;
        string federacao = devedor.federacaoGlobal;
        // Todas as verificacoes acima sao somente leitura. A transferencia so
        // ocorre depois de aprovar o credor, a necessidade e a capacidade.
        credor.saldo -= valorArredondado;
        devedor.saldo += valorArredondado;
        if (devedor.emprestimos == null) devedor.emprestimos = new List<EmprestimoFederativoEstado>();
        devedor.emprestimos.Add(new EmprestimoFederativoEstado
        {
            id = "loan:" + Guid.NewGuid().ToString("N"),
            credorTeamId = credorTeamId,
            devedorTeamId = devedorTeamId,
            federacao = federacao,
            principal = valorArredondado,
            saldoDevedor = valorArredondado,
            jurosPorTick = jurosPorTick,
            creditoMilitar = creditoMilitar,
            ajusteEstrutural = !creditoMilitar,
            ticksRestantes = ticks
        });
        governo.NotificarGovernoAtualizado();
        mensagem = "emprestimo aprovado";
        return true;
    }

    public bool QuitarEmprestimo(int teamId, string loanId, out string mensagem)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
        EmprestimoFederativoEstado loan = pais != null && pais.emprestimos != null ? pais.emprestimos.FirstOrDefault(x => x != null && x.id == loanId) : null;
        if (pais == null || loan == null) { mensagem = "emprestimo nao encontrado"; return false; }
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno credor = governo != null ? governo.ObterPais(loan.credorTeamId) : null;
        if (credor == null) { mensagem = "o credor deste emprestimo nao esta mais registrado"; return false; }
        long valorQuitacao = (long)Math.Ceiling(Math.Max(0f, loan.saldoDevedor));
        if (pais.saldo < valorQuitacao) { mensagem = "saldo insuficiente"; return false; }
        pais.saldo -= valorQuitacao;
        credor.saldo += valorQuitacao;
        pais.emprestimos.Remove(loan);
        governo?.NotificarGovernoAtualizado();
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
                if (loan == null || loan.inadimplente) continue;
                if (loan.ticksRestantes > 0)
                {
                    loan.saldoDevedor += loan.saldoDevedor * Mathf.Max(0f, loan.jurosPorTick);
                    loan.ticksRestantes--;
                }
                if (loan.ajusteEstrutural) pais.pesoMilitarismo = Mathf.Max(0f, pais.pesoMilitarismo - 0.002f);
                if (loan.ticksRestantes > 0) continue;

                long valorVencido = (long)Math.Ceiling(Math.Max(0f, loan.saldoDevedor));
                DadosPaisGoverno credor = governo.ObterPais(loan.credorTeamId);
                if (credor != null && pais.saldo >= valorVencido)
                {
                    pais.saldo -= valorVencido;
                    credor.saldo += valorVencido;
                    pais.emprestimos.Remove(loan);
                    governo.RegistrarNoticia(pais.nomePais + " quitou automaticamente um emprestimo federativo.");
                    continue;
                }

                loan.inadimplente = true;
                pais.legitimidadeGlobal = Mathf.Max(0f, pais.legitimidadeGlobal - 8f);
                pais.sancionado = true;
                governo.RegistrarNoticia(pais.nomePais + " entrou em inadimplencia em um emprestimo federativo.");
            }
        }
    }

    private bool PossuiNecessidadeDeCredito(DadosPaisGoverno pais, bool creditoMilitar)
    {
        if (pais == null) return false;
        if (creditoMilitar)
        {
            return pais.emGuerra || pais.nivelMilitar < 55 || pais.estabilidade < 45f;
        }

        long reserva = CalcularReservaFinanceira(pais);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        long saldoOperacionalDisponivel = governo != null
            ? governo.ObterSaldoDisponivelParaOperacao(pais.teamId)
            : pais.saldo;
        return pais.saldo <= reserva
            || saldoOperacionalDisponivel <= reserva
            || pais.rendaPorSegundo < pais.gastosPorSegundo
            || pais.deficitComida > 0f || pais.deficitEnergia > 0f || pais.deficitPetroleo > 0f
            || pais.estabilidade < 45f || pais.sancionado;
    }

    private long CalcularCapacidadePagamento(DadosPaisGoverno pais)
    {
        if (pais == null) return 0L;
        double reserva = CalcularReservaFinanceira(pais);
        double saldoDisponivel = Math.Max(0d, pais.saldo - reserva);
        double rendaLiquida = Math.Max(0d, pais.rendaPorSegundo - pais.gastosPorSegundo);
        double duracaoSegundos = PrazoEmprestimoPadraoTicks * Math.Max(1f, intervaloTick);
        double capacidade = saldoDisponivel + rendaLiquida * duracaoSegundos;
        return capacidade >= long.MaxValue ? long.MaxValue : (long)Math.Floor(capacidade);
    }

    private long CalcularReservaFinanceira(DadosPaisGoverno pais)
    {
        if (pais == null) return 0L;
        double reservaOperacional = Math.Max(0d, pais.gastosPorSegundo) * Math.Max(1f, intervaloTick) * 5d;
        return (long)Math.Ceiling(Math.Max(500d, reservaOperacional));
    }

    public static string NomeFederacao(string id)
    {
        if (id == TipoFederacao.CooperacaoGlobal.ToString()) return "Concordia Global";
        if (id == TipoFederacao.AliancaDefesa.ToString()) return "Coalizao Aegis";
        return string.IsNullOrWhiteSpace(id) ? "Sem bloco" : id;
    }
}
