using System;
using UnityEngine;

public enum EstadoOperacaoPortaAvioesV2
{
    EmVoo, SolicitandoPouso, AguardandoAutorizacao, CircuitoDeEspera,
    AproximacaoLonga, AproximacaoIntermediaria, AproximacaoFinal,
    ToqueNoConves, FrenagemOuCaboDeRetencao, TaxiandoParaSaida,
    TaxiandoParaVaga, EstacionadoNoConves, AguardandoServico, Reabastecendo,
    Rearmando, ProntoNoConves, AguardandoElevador, TaxiandoParaElevador,
    AlinhandoNoElevador, ElevadorDescendo, EntrandoNoHangar,
    ArmazenadoNoHangar, PreparandoSaidaDoHangar, ElevadorSubindo,
    SaindoDoElevador, AguardandoCatapulta, TaxiandoParaCatapulta,
    AlinhandoNaCatapulta, PreparandoDecolagem, Lancamento, SubidaInicial,
    EmMissao, OperacaoCancelada, FalhaControlada, PortaAvioesIndisponivel,
    SemVaga, SemElevador, SemCatapulta
}

public enum TipoAeronavePortaAvioesV2 { Qualquer, Caca, Transporte, Patrulha, Helicoptero }
public enum EstadoVagaPortaAvioesV2 { Livre, Reservada, Ocupada, Bloqueada }

[Serializable]
public sealed class RegistroAeronavePortaAvioesV2
{
    public string id;
    public TipoAeronavePortaAvioesV2 tipo;
    public string paisOuTime;
    public string portaAvioesAtual;
    public EstadoOperacaoPortaAvioesV2 estado = EstadoOperacaoPortaAvioesV2.EmVoo;
    public float combustivel;
    public float municao;
    public float integridade = 1f;
    public string vagaReservada;
    public string vagaOcupada;
    public string elevadorReservado;
    public string catapultaReservada;
    public string missaoAtual;
    public string operacaoAtual;
    public float momentoUltimaTransicao;
    public string motivoFalha;
}

public sealed class AeronaveEmbarcadaV2 : MonoBehaviour
{
    [SerializeField] private RegistroAeronavePortaAvioesV2 registro = new RegistroAeronavePortaAvioesV2();
    [SerializeField] private string donoMovimento;
    [SerializeField] private int tokenAutoridade;
    public RegistroAeronavePortaAvioesV2 Registro => registro;
    public string DonoMovimento => donoMovimento;
    public int TokenAutoridade => tokenAutoridade;

    public event Action<EstadoOperacaoPortaAvioesV2, EstadoOperacaoPortaAvioesV2> EstadoAlterado;

    public void GarantirIdentidade()
    {
        if (registro == null) registro = new RegistroAeronavePortaAvioesV2();
        if (string.IsNullOrEmpty(registro.id)) registro.id = Guid.NewGuid().ToString("N");
        registro.integridade = Mathf.Clamp01(registro.integridade <= 0f ? 1f : registro.integridade);
    }

    public bool TentarAssumirAutoridade(string autoridade, out int novoToken)
    {
        GarantirIdentidade();
        novoToken = tokenAutoridade;
        if (!string.IsNullOrEmpty(donoMovimento) && donoMovimento != autoridade) return false;
        donoMovimento = autoridade;
        novoToken = ++tokenAutoridade;
        return true;
    }

    public void LiberarAutoridade(string autoridade)
    {
        if (donoMovimento == autoridade) donoMovimento = string.Empty;
    }

    public bool TentarTransicionar(EstadoOperacaoPortaAvioesV2 proximo, float agora, string motivo = "")
    {
        GarantirIdentidade();
        if (!TransicaoPermitida(registro.estado, proximo)) return false;
        EstadoOperacaoPortaAvioesV2 anterior = registro.estado;
        registro.estado = proximo;
        registro.momentoUltimaTransicao = agora;
        registro.motivoFalha = motivo ?? string.Empty;
        EstadoAlterado?.Invoke(anterior, proximo);
        return true;
    }

    public void ForcarEstadoSeguro(EstadoOperacaoPortaAvioesV2 estado, string motivo)
    {
        GarantirIdentidade();
        var anterior = registro.estado;
        registro.estado = estado;
        registro.momentoUltimaTransicao = Time.time;
        registro.motivoFalha = motivo ?? string.Empty;
        if (anterior != estado) EstadoAlterado?.Invoke(anterior, estado);
    }

    public static bool TransicaoPermitida(EstadoOperacaoPortaAvioesV2 a, EstadoOperacaoPortaAvioesV2 b)
    {
        if (a == b) return true;
        if (b == EstadoOperacaoPortaAvioesV2.OperacaoCancelada || b == EstadoOperacaoPortaAvioesV2.FalhaControlada) return true;
        if (a == EstadoOperacaoPortaAvioesV2.EmVoo) return b == EstadoOperacaoPortaAvioesV2.SolicitandoPouso;
        if (a == EstadoOperacaoPortaAvioesV2.SolicitandoPouso) return b == EstadoOperacaoPortaAvioesV2.AguardandoAutorizacao || b == EstadoOperacaoPortaAvioesV2.CircuitoDeEspera;
        if (a == EstadoOperacaoPortaAvioesV2.AguardandoAutorizacao || a == EstadoOperacaoPortaAvioesV2.CircuitoDeEspera) return b == EstadoOperacaoPortaAvioesV2.AproximacaoLonga;
        if (a == EstadoOperacaoPortaAvioesV2.AproximacaoLonga) return b == EstadoOperacaoPortaAvioesV2.AproximacaoIntermediaria;
        if (a == EstadoOperacaoPortaAvioesV2.AproximacaoIntermediaria) return b == EstadoOperacaoPortaAvioesV2.AproximacaoFinal;
        if (a == EstadoOperacaoPortaAvioesV2.AproximacaoFinal) return b == EstadoOperacaoPortaAvioesV2.ToqueNoConves;
        if (a == EstadoOperacaoPortaAvioesV2.ToqueNoConves) return b == EstadoOperacaoPortaAvioesV2.FrenagemOuCaboDeRetencao;
        if (a == EstadoOperacaoPortaAvioesV2.FrenagemOuCaboDeRetencao) return b == EstadoOperacaoPortaAvioesV2.TaxiandoParaSaida;
        if (a == EstadoOperacaoPortaAvioesV2.TaxiandoParaSaida) return b == EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga;
        if (a == EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga) return b == EstadoOperacaoPortaAvioesV2.EstacionadoNoConves;
        if (a == EstadoOperacaoPortaAvioesV2.EstacionadoNoConves || a == EstadoOperacaoPortaAvioesV2.AguardandoServico || a == EstadoOperacaoPortaAvioesV2.ProntoNoConves) return b == EstadoOperacaoPortaAvioesV2.AguardandoServico || b == EstadoOperacaoPortaAvioesV2.Reabastecendo || b == EstadoOperacaoPortaAvioesV2.AguardandoElevador || b == EstadoOperacaoPortaAvioesV2.AguardandoCatapulta || b == EstadoOperacaoPortaAvioesV2.ProntoNoConves;
        if (a == EstadoOperacaoPortaAvioesV2.Reabastecendo) return b == EstadoOperacaoPortaAvioesV2.ProntoNoConves || b == EstadoOperacaoPortaAvioesV2.EstacionadoNoConves;
        if (a == EstadoOperacaoPortaAvioesV2.AguardandoElevador || a == EstadoOperacaoPortaAvioesV2.TaxiandoParaElevador || a == EstadoOperacaoPortaAvioesV2.AlinhandoNoElevador || a == EstadoOperacaoPortaAvioesV2.ElevadorDescendo || a == EstadoOperacaoPortaAvioesV2.EntrandoNoHangar) return b == EstadoOperacaoPortaAvioesV2.TaxiandoParaElevador || b == EstadoOperacaoPortaAvioesV2.AlinhandoNoElevador || b == EstadoOperacaoPortaAvioesV2.ElevadorDescendo || b == EstadoOperacaoPortaAvioesV2.EntrandoNoHangar || b == EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar;
        if (a == EstadoOperacaoPortaAvioesV2.ArmazenadoNoHangar) return b == EstadoOperacaoPortaAvioesV2.Reabastecendo || b == EstadoOperacaoPortaAvioesV2.PreparandoSaidaDoHangar || b == EstadoOperacaoPortaAvioesV2.ElevadorSubindo || b == EstadoOperacaoPortaAvioesV2.SaindoDoElevador || b == EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga;
        if (a == EstadoOperacaoPortaAvioesV2.PreparandoSaidaDoHangar || a == EstadoOperacaoPortaAvioesV2.ElevadorSubindo || a == EstadoOperacaoPortaAvioesV2.SaindoDoElevador) return b == EstadoOperacaoPortaAvioesV2.PreparandoSaidaDoHangar || b == EstadoOperacaoPortaAvioesV2.ElevadorSubindo || b == EstadoOperacaoPortaAvioesV2.SaindoDoElevador || b == EstadoOperacaoPortaAvioesV2.TaxiandoParaVaga;
        if (a == EstadoOperacaoPortaAvioesV2.AguardandoCatapulta || a == EstadoOperacaoPortaAvioesV2.TaxiandoParaCatapulta || a == EstadoOperacaoPortaAvioesV2.AlinhandoNaCatapulta || a == EstadoOperacaoPortaAvioesV2.PreparandoDecolagem || a == EstadoOperacaoPortaAvioesV2.Lancamento || a == EstadoOperacaoPortaAvioesV2.SubidaInicial) return b == EstadoOperacaoPortaAvioesV2.TaxiandoParaCatapulta || b == EstadoOperacaoPortaAvioesV2.AlinhandoNaCatapulta || b == EstadoOperacaoPortaAvioesV2.PreparandoDecolagem || b == EstadoOperacaoPortaAvioesV2.Lancamento || b == EstadoOperacaoPortaAvioesV2.SubidaInicial || b == EstadoOperacaoPortaAvioesV2.EmMissao;
        return false;
    }

    private void Awake() { GarantirIdentidade(); }
}
