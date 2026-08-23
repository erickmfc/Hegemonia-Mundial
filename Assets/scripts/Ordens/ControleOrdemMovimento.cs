using System;
using UnityEngine;

/// <summary>
/// Estados comuns às ordens de deslocamento. A estratégia continua fora deste
/// tipo: ele apenas controla a execução, o progresso e o encerramento local.
/// </summary>
public enum EstadoOrdemMovimento
{
    Pendente,
    Validando,
    Executando,
    Monitorando,
    EsperandoNovaTentativa,
    Recalculando,
    Concluida,
    Falhou,
    Cancelada
}

public enum TipoOrdemMovimento
{
    Terrestre,
    Aerea,
    Naval,
    Logistica,
    Patrulha
}

/// <summary>
/// Dados de uma ordem individual. A classe é deliberadamente simples para
/// poder ser usada por terra, ar, mar, logística e patrulha sem introduzir um
/// orquestrador global.
/// </summary>
[Serializable]
public sealed class OrdemMovimento
{
    public string Id = string.Empty;
    public string Dono = string.Empty;
    public GameObject Unidade;
    public Transform Objetivo;
    public Vector3 Destino;
    public float HorarioCriacao;
    public EstadoOrdemMovimento Estado = EstadoOrdemMovimento.Pendente;
    public TipoOrdemMovimento Tipo = TipoOrdemMovimento.Terrestre;
    public int Tentativas;
    public float UltimoMomentoDeProgresso;
    public float ProximaTentativaEm;
    public string MotivoFalhaOuCancelamento = string.Empty;
    public bool RecalculoRealizado;

    public bool Terminada
    {
        get
        {
            return Estado == EstadoOrdemMovimento.Concluida
                || Estado == EstadoOrdemMovimento.Falhou
                || Estado == EstadoOrdemMovimento.Cancelada;
        }
    }
}

/// <summary>
/// Máquina de estados local de uma unidade. Não agenda ordens de outras
/// unidades e não decide estratégia; apenas garante que uma ordem desta
/// unidade tenha um único dono, seja idempotente e termine de forma controlada.
/// </summary>
public sealed class ControleOrdemMovimentoRuntime
{
    public const int MaxTentativasPorOrdem = 2;

    private readonly float intervaloEntreTentativas;

    public OrdemMovimento Atual { get; private set; }

    public bool PossuiOrdemAtiva => Atual != null && !Atual.Terminada;
    public EstadoOrdemMovimento EstadoAtual => Atual != null ? Atual.Estado : EstadoOrdemMovimento.Cancelada;
    public float IntervaloEntreTentativas => intervaloEntreTentativas;

    public event Action<OrdemMovimento, EstadoOrdemMovimento, EstadoOrdemMovimento> EstadoAlterado;

    public ControleOrdemMovimentoRuntime(float intervaloEntreTentativas = 2f)
    {
        this.intervaloEntreTentativas = Mathf.Max(0.1f, intervaloEntreTentativas);
    }

    /// <summary>
    /// Inicia uma ordem ou reutiliza a ordem atual quando ID, unidade, dono e
    /// destino são os mesmos. Reemissões idênticas não voltam a executar o
    /// executor.
    /// </summary>
    public bool TentarIniciar(
        string id,
        string dono,
        GameObject unidade,
        Vector3 destino,
        TipoOrdemMovimento tipo,
        float agora,
        out bool foiIdempotente)
    {
        foiIdempotente = false;
        id = string.IsNullOrWhiteSpace(id) ? "ordem-sem-id" : id.Trim();
        dono = string.IsNullOrWhiteSpace(dono) ? "dono-desconhecido" : dono.Trim();

        if (Atual != null && string.Equals(Atual.Id, id, StringComparison.Ordinal))
        {
            bool mesmaUnidade = Atual.Unidade == unidade;
            bool mesmoDono = string.Equals(Atual.Dono, dono, StringComparison.Ordinal);
            bool mesmoDestino = Vector3.Distance(Atual.Destino, destino) <= 0.01f;
            if (!mesmaUnidade || !mesmoDono || !mesmoDestino)
            {
                return false;
            }

            foiIdempotente = true;
            return true;
        }

        if (PossuiOrdemAtiva)
        {
            CancelarInterno("substituida por uma nova ordem", agora);
        }

        Atual = new OrdemMovimento
        {
            Id = id,
            Dono = dono,
            Unidade = unidade,
            Destino = destino,
            Tipo = tipo,
            HorarioCriacao = agora,
            UltimoMomentoDeProgresso = agora,
            Estado = EstadoOrdemMovimento.Pendente
        };

        MudarEstado(EstadoOrdemMovimento.Validando);
        return true;
    }

    public bool TentarIniciarTentativa(float agora)
    {
        if (!PossuiOrdemAtiva || Atual.Tentativas >= MaxTentativasPorOrdem)
        {
            return false;
        }

        Atual.Tentativas++;
        Atual.UltimoMomentoDeProgresso = agora;
        Atual.ProximaTentativaEm = 0f;
        MudarEstado(EstadoOrdemMovimento.Executando);
        return true;
    }

    public bool ComecarMonitoramento(float agora)
    {
        if (!PossuiOrdemAtiva)
        {
            return false;
        }

        Atual.UltimoMomentoDeProgresso = agora;
        MudarEstado(EstadoOrdemMovimento.Monitorando);
        return true;
    }

    public bool RegistrarProgresso(float agora)
    {
        if (!PossuiOrdemAtiva)
        {
            return false;
        }

        Atual.UltimoMomentoDeProgresso = agora;
        if (Atual.Estado == EstadoOrdemMovimento.EsperandoNovaTentativa
            || Atual.Estado == EstadoOrdemMovimento.Recalculando)
        {
            return false;
        }

        MudarEstado(EstadoOrdemMovimento.Monitorando);
        return true;
    }

    public bool AtualizarDestino(string id, Vector3 destino)
    {
        if (!PossuiOrdemAtiva || string.IsNullOrWhiteSpace(id)
            || !string.Equals(Atual.Id, id, StringComparison.Ordinal))
        {
            return false;
        }

        Atual.Destino = destino;
        return true;
    }

    public bool AgendarNovaTentativa(float agora, string motivo)
    {
        if (!PossuiOrdemAtiva)
        {
            return false;
        }

        Atual.MotivoFalhaOuCancelamento = motivo ?? string.Empty;
        if (Atual.Tentativas < MaxTentativasPorOrdem)
        {
            Atual.ProximaTentativaEm = agora + intervaloEntreTentativas;
            MudarEstado(EstadoOrdemMovimento.EsperandoNovaTentativa);
            return true;
        }

        // A segunda tentativa é a última execução automática. A transição
        // explícita por Recalculando deixa o diagnóstico claro sem iniciar um
        // terceiro SetDestination/roteiro.
        Atual.RecalculoRealizado = true;
        MudarEstado(EstadoOrdemMovimento.Recalculando);
        return false;
    }

    public bool PodeTentarNovamente(float agora)
    {
        return PossuiOrdemAtiva
            && Atual.Estado == EstadoOrdemMovimento.EsperandoNovaTentativa
            && agora >= Atual.ProximaTentativaEm;
    }

    public bool PrepararRecalculo(float agora)
    {
        if (!PodeTentarNovamente(agora))
        {
            return false;
        }

        MudarEstado(EstadoOrdemMovimento.Recalculando);
        return true;
    }

    public bool Concluir(float agora)
    {
        if (Atual == null || Atual.Terminada)
        {
            return false;
        }

        Atual.UltimoMomentoDeProgresso = agora;
        Atual.MotivoFalhaOuCancelamento = string.Empty;
        MudarEstado(EstadoOrdemMovimento.Concluida);
        return true;
    }

    public bool Falhar(string motivo, float agora)
    {
        if (Atual == null || Atual.Terminada)
        {
            return false;
        }

        Atual.MotivoFalhaOuCancelamento = string.IsNullOrWhiteSpace(motivo)
            ? "falha nao especificada"
            : motivo;
        Atual.UltimoMomentoDeProgresso = agora;
        MudarEstado(EstadoOrdemMovimento.Falhou);
        return true;
    }

    public bool Cancelar(string motivo, float agora)
    {
        if (Atual == null || Atual.Terminada)
        {
            return false;
        }

        CancelarInterno(string.IsNullOrWhiteSpace(motivo) ? "cancelada" : motivo, agora);
        return true;
    }

    private void CancelarInterno(string motivo, float agora)
    {
        if (Atual == null || Atual.Terminada)
        {
            return;
        }

        Atual.MotivoFalhaOuCancelamento = motivo;
        Atual.UltimoMomentoDeProgresso = agora;
        MudarEstado(EstadoOrdemMovimento.Cancelada);
    }

    private void MudarEstado(EstadoOrdemMovimento novoEstado)
    {
        if (Atual == null || Atual.Estado == novoEstado)
        {
            return;
        }

        EstadoOrdemMovimento anterior = Atual.Estado;
        Atual.Estado = novoEstado;
        EstadoAlterado?.Invoke(Atual, anterior, novoEstado);
    }
}
