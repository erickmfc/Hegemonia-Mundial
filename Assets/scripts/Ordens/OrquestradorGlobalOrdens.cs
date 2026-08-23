using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global das ordens de movimento da partida.
///
/// Este componente coordena identidade, propriedade e ciclo de vida das
/// ordens, mas não executa movimento. Cada executor existente continua sendo
/// responsável pelo seu domínio: NavMesh em terra, controladores aéreos no ar
/// e controladores aquáticos no mar.
/// </summary>
public static class OrquestradorGlobalOrdens
{
    public sealed class Registro
    {
        public string Id { get; internal set; } = string.Empty;
        public string Dono { get; internal set; } = string.Empty;
        public GameObject Unidade { get; internal set; }
        public int UnidadeInstanceId { get; internal set; }
        public Vector3 Destino { get; internal set; }
        public TipoOrdemMovimento Tipo { get; internal set; }
        public EstadoOrdemMovimento Estado { get; internal set; }
        public float HorarioCriacao { get; internal set; }
        public float UltimaAtualizacao { get; internal set; }
        public int Tentativas { get; internal set; }
        public float UltimoMomentoDeProgresso { get; internal set; }
        public string MotivoFalhaOuCancelamento { get; internal set; } = string.Empty;

        public bool Terminada => Estado == EstadoOrdemMovimento.Concluida
            || Estado == EstadoOrdemMovimento.Falhou
            || Estado == EstadoOrdemMovimento.Cancelada;
    }

    private static readonly Dictionary<string, Registro> registrosPorId =
        new Dictionary<string, Registro>(StringComparer.Ordinal);
    private static readonly Dictionary<int, string> ordemAtivaPorUnidade =
        new Dictionary<int, string>();

    /// <summary>
    /// Disparado uma única vez quando uma ordem chega ao estado Concluida.
    /// </summary>
    public static event Action<Registro> OrdemConcluida;

    /// <summary>
    /// Disparado somente na primeira entrada de um estado terminal.
    /// </summary>
    public static event Action<Registro> OrdemEncerrada;

    /// <summary>
    /// Ponte de despertar para o agendador global. O registro de ordens não
    /// executa o tick: apenas informa que uma ordem relevante merece atenção.
    /// </summary>
    public static event Action<string> DespertarSolicitado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        registrosPorId.Clear();
        ordemAtivaPorUnidade.Clear();
        OrdemConcluida = null;
        OrdemEncerrada = null;
        DespertarSolicitado = null;
    }

    /// <summary>
    /// Tenta reservar a identidade global da ordem antes de o executor local
    /// receber o destino. Reemissões equivalentes são idempotentes. O mesmo ID
    /// nunca pode apontar para outra unidade, dono ou destino.
    /// </summary>
    public static bool TentarRegistrar(
        string id,
        string dono,
        GameObject unidade,
        Vector3 destino,
        TipoOrdemMovimento tipo,
        float agora,
        out bool foiIdempotente,
        out string motivo)
    {
        foiIdempotente = false;
        motivo = string.Empty;

        id = Normalizar(id, "ordem-sem-id");
        dono = Normalizar(dono, "dono-desconhecido");
        if (unidade == null)
        {
            motivo = "unidade ausente";
            return false;
        }

        if (!PosicaoValida(destino))
        {
            motivo = "destino invalido";
            return false;
        }

        if (registrosPorId.TryGetValue(id, out Registro existente))
        {
            bool mesmaUnidade = existente.Unidade == unidade
                && existente.UnidadeInstanceId == unidade.GetInstanceID();
            bool mesmoDono = string.Equals(existente.Dono, dono, StringComparison.Ordinal);
            bool mesmoDestino = Vector3.Distance(existente.Destino, destino) <= 0.01f;
            bool mesmoTipo = existente.Tipo == tipo;

            if (!mesmaUnidade || !mesmoDono || !mesmoDestino || !mesmoTipo)
            {
                motivo = "ID de ordem ja pertence a outra execucao";
                return false;
            }

            // Um ID terminal continua sendo conhecido para impedir que a
            // mesma ordem seja executada novamente depois de concluida.
            foiIdempotente = true;
            return true;
        }

        int unidadeId = unidade.GetInstanceID();
        if (ordemAtivaPorUnidade.TryGetValue(unidadeId, out string ordemAnteriorId)
            && registrosPorId.TryGetValue(ordemAnteriorId, out Registro ordemAnterior)
            && !ordemAnterior.Terminada)
        {
            EncerrarRegistro(
                ordemAnterior,
                EstadoOrdemMovimento.Cancelada,
                agora,
                "substituida por uma nova ordem");
        }

        Registro novo = new Registro
        {
            Id = id,
            Dono = dono,
            Unidade = unidade,
            UnidadeInstanceId = unidadeId,
            Destino = destino,
            Tipo = tipo,
            Estado = EstadoOrdemMovimento.Pendente,
            HorarioCriacao = agora,
            UltimaAtualizacao = agora,
            Tentativas = 0,
            UltimoMomentoDeProgresso = agora
        };

        registrosPorId[id] = novo;
        ordemAtivaPorUnidade[unidadeId] = id;
        DespertarSolicitado?.Invoke(id);
        return true;
    }

    /// <summary>
    /// Atualiza somente o destino de uma ordem viva. Isso é usado por
    /// perseguidores navais: o alvo pode se mover sem que a identidade da
    /// ordem seja substituída e o estado global publique um cancelamento.
    /// </summary>
    public static bool AtualizarDestino(
        string id,
        string dono,
        GameObject unidade,
        Vector3 destino,
        TipoOrdemMovimento tipo,
        float agora,
        out string motivo)
    {
        motivo = string.Empty;
        id = Normalizar(id, "ordem-sem-id");
        dono = Normalizar(dono, "dono-desconhecido");
        if (unidade == null || !PosicaoValida(destino))
        {
            motivo = "unidade ou destino invalido";
            return false;
        }

        if (!registrosPorId.TryGetValue(id, out Registro registro)
            || registro.Terminada)
        {
            motivo = "ordem ausente ou encerrada";
            return false;
        }

        if (registro.Unidade != unidade
            || registro.UnidadeInstanceId != unidade.GetInstanceID()
            || !string.Equals(registro.Dono, dono, StringComparison.Ordinal)
            || registro.Tipo != tipo)
        {
            motivo = "proprietario da ordem divergente";
            return false;
        }

        registro.Destino = destino;
        registro.UltimaAtualizacao = agora;
        DespertarSolicitado?.Invoke(registro.Id);
        return true;
    }

    /// <summary>
    /// Sincroniza o registro global com a máquina de estados local da unidade.
    /// O método não dispara trabalho periódico e não chama nenhum executor.
    /// </summary>
    public static void NotificarEstado(
        OrdemMovimento ordem,
        EstadoOrdemMovimento estadoAnterior,
        EstadoOrdemMovimento novoEstado,
        float agora)
    {
        if (ordem == null || ordem.Unidade == null)
        {
            return;
        }

        string id = Normalizar(ordem.Id, "ordem-sem-id");
        if (!registrosPorId.TryGetValue(id, out Registro registro))
        {
            bool foiIdempotente;
            string motivo;
            if (!TentarRegistrar(
                    id,
                    ordem.Dono,
                    ordem.Unidade,
                    ordem.Destino,
                    ordem.Tipo,
                    ordem.HorarioCriacao,
                    out foiIdempotente,
                    out motivo))
            {
                return;
            }

            registro = registrosPorId[id];
        }

        // Uma ordem terminal não pode ser reaberta por uma reemissão atrasada,
        // nem deve publicar o mesmo encerramento duas vezes quando o executor
        // local apenas confirma uma transição já registrada globalmente.
        if (registro.Terminada)
        {
            return;
        }

        registro.Estado = novoEstado;
        registro.UltimaAtualizacao = agora;
        registro.Tentativas = Mathf.Max(registro.Tentativas, ordem.Tentativas);
        registro.UltimoMomentoDeProgresso = ordem.UltimoMomentoDeProgresso;
        registro.Destino = ordem.Destino;
        registro.MotivoFalhaOuCancelamento = ordem.MotivoFalhaOuCancelamento ?? string.Empty;
        DespertarSolicitado?.Invoke(registro.Id);

        if (EhEstadoTerminal(novoEstado))
        {
            if (ordemAtivaPorUnidade.TryGetValue(registro.UnidadeInstanceId, out string ativa)
                && string.Equals(ativa, registro.Id, StringComparison.Ordinal))
            {
                ordemAtivaPorUnidade.Remove(registro.UnidadeInstanceId);
            }

            if (!EhEstadoTerminal(estadoAnterior))
            {
                OrdemEncerrada?.Invoke(registro);
                if (novoEstado == EstadoOrdemMovimento.Concluida)
                {
                    OrdemConcluida?.Invoke(registro);
                }
            }
        }
        else
        {
            ordemAtivaPorUnidade[registro.UnidadeInstanceId] = registro.Id;
        }
    }

    /// <summary>
    /// Cancela a ordem ativa de uma unidade. É usado pelo ciclo de vida de
    /// ControleUnidade para evitar registros pendurados quando a unidade é
    /// desativada ou destruída.
    /// </summary>
    public static bool LiberarUnidade(GameObject unidade, string motivo, float agora)
    {
        if (unidade == null)
        {
            return false;
        }

        int unidadeId = unidade.GetInstanceID();
        if (!ordemAtivaPorUnidade.TryGetValue(unidadeId, out string id)
            || !registrosPorId.TryGetValue(id, out Registro registro)
            || registro.Terminada)
        {
            ordemAtivaPorUnidade.Remove(unidadeId);
            return false;
        }

        EncerrarRegistro(
            registro,
            EstadoOrdemMovimento.Cancelada,
            agora,
            string.IsNullOrWhiteSpace(motivo) ? "unidade liberada" : motivo);
        return true;
    }

    public static bool TentarObter(string id, out Registro registro)
    {
        return registrosPorId.TryGetValue(Normalizar(id, "ordem-sem-id"), out registro);
    }

    public static bool UnidadePossuiOrdemAtiva(GameObject unidade, out Registro registro)
    {
        registro = null;
        if (unidade == null
            || !ordemAtivaPorUnidade.TryGetValue(unidade.GetInstanceID(), out string id)
            || !registrosPorId.TryGetValue(id, out registro)
            || registro.Terminada)
        {
            registro = null;
            return false;
        }

        return true;
    }

    public static int TotalRegistradas => registrosPorId.Count;

    private static void EncerrarRegistro(
        Registro registro,
        EstadoOrdemMovimento estado,
        float agora,
        string motivo)
    {
        if (registro == null || registro.Terminada)
        {
            return;
        }

        registro.Estado = estado;
        registro.UltimaAtualizacao = agora;
        registro.MotivoFalhaOuCancelamento = motivo ?? string.Empty;
        if (ordemAtivaPorUnidade.TryGetValue(registro.UnidadeInstanceId, out string ativa)
            && string.Equals(ativa, registro.Id, StringComparison.Ordinal))
        {
            ordemAtivaPorUnidade.Remove(registro.UnidadeInstanceId);
        }

        OrdemEncerrada?.Invoke(registro);
        DespertarSolicitado?.Invoke(registro.Id);
    }

    private static bool EhEstadoTerminal(EstadoOrdemMovimento estado)
    {
        return estado == EstadoOrdemMovimento.Concluida
            || estado == EstadoOrdemMovimento.Falhou
            || estado == EstadoOrdemMovimento.Cancelada;
    }

    private static string Normalizar(string valor, string padrao)
    {
        return string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();
    }

    private static bool PosicaoValida(Vector3 posicao)
    {
        return !float.IsNaN(posicao.x) && !float.IsInfinity(posicao.x)
            && !float.IsNaN(posicao.y) && !float.IsInfinity(posicao.y)
            && !float.IsNaN(posicao.z) && !float.IsInfinity(posicao.z);
    }
}
