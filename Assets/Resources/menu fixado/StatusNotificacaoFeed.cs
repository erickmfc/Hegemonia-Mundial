using System;
using System.Collections.Generic;

public enum StatusNotificacaoSeveridade
{
    Info,
    Success,
    Warning,
    Critical
}

public sealed class StatusNotificacao
{
    public string Categoria { get; private set; }
    public string Titulo { get; private set; }
    public string Mensagem { get; private set; }
    public string Horario { get; private set; }
    public StatusNotificacaoSeveridade Severidade { get; private set; }
    public string AcaoTexto { get; private set; }
    public Action Acao { get; private set; }
    public bool Descartada { get; internal set; }

    public string Chave
    {
        get { return (Categoria ?? string.Empty) + "|" + (Titulo ?? string.Empty); }
    }

    public StatusNotificacao(string categoria, string titulo, string mensagem, StatusNotificacaoSeveridade severidade)
        : this(categoria, titulo, mensagem, severidade, string.Empty, null)
    {
    }

    public StatusNotificacao(string categoria, string titulo, string mensagem,
        StatusNotificacaoSeveridade severidade, string acaoTexto, Action acao)
    {
        Categoria = categoria;
        Titulo = titulo;
        Mensagem = mensagem;
        Severidade = severidade;
        AcaoTexto = acaoTexto ?? string.Empty;
        Acao = acao;
        Horario = DateTime.Now.ToString("HH:mm");
    }

    internal void Atualizar(string mensagem, StatusNotificacaoSeveridade severidade, string acaoTexto, Action acao)
    {
        Mensagem = mensagem;
        Severidade = severidade;
        AcaoTexto = acaoTexto ?? string.Empty;
        Acao = acao;
        Horario = DateTime.Now.ToString("HH:mm");
    }

    public bool TemAcao => Acao != null && !string.IsNullOrWhiteSpace(AcaoTexto);
}

/// <summary>
/// Feed leve e desacoplado para acontecimentos importantes do jogo.
/// Sistemas de governo, economia, diplomacia e unidades podem publicar aqui.
/// </summary>
public static class StatusNotificacaoFeed
{
    private const int LimiteItens = 64;
    private static readonly List<StatusNotificacao> _itens = new List<StatusNotificacao>(LimiteItens);

    public static event Action OnAlterado;
    public static IList<StatusNotificacao> Itens { get { return _itens; } }

    public static bool PossuiNovidadeNaoDescartada
    {
        get
        {
            for (int i = 0; i < _itens.Count; i++)
            {
                StatusNotificacao item = _itens[i];
                if (item != null && !item.Descartada && item.Severidade == StatusNotificacaoSeveridade.Critical)
                    return true;
            }
            return false;
        }
    }

    public static void Publicar(string categoria, string titulo, string mensagem, StatusNotificacaoSeveridade severidade = StatusNotificacaoSeveridade.Info)
    {
        Publicar(categoria, titulo, mensagem, severidade, string.Empty, null);
    }

    public static void Publicar(string categoria, string titulo, string mensagem,
        StatusNotificacaoSeveridade severidade, string acaoTexto, Action acao)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(mensagem))
        {
            return;
        }

        string categoriaNormalizada = string.IsNullOrWhiteSpace(categoria) ? "GERAL" : categoria.ToUpperInvariant();
        string chave = categoriaNormalizada + "|" + titulo;
        bool resolvido = severidade == StatusNotificacaoSeveridade.Info
            || severidade == StatusNotificacaoSeveridade.Success;
        if (resolvido)
        {
            // Um estado normalizado libera novamente os avisos antigos da
            // mesma categoria caso o problema volte em outro momento.
            for (int i = 0; i < _itens.Count; i++)
            {
                if (_itens[i] != null && string.Equals(_itens[i].Categoria, categoriaNormalizada, StringComparison.Ordinal))
                    _itens[i].Descartada = false;
            }
        }
        StatusNotificacao existente = _itens.Find(item => item != null && string.Equals(item.Chave, chave, StringComparison.Ordinal));
        if (existente != null)
        {
            existente.Descartada = resolvido ? false : existente.Descartada;
            existente.Atualizar(mensagem, severidade, acaoTexto, acao);
            OnAlterado?.Invoke();
            return;
        }

        _itens.Insert(0, new StatusNotificacao(
            categoriaNormalizada,
            titulo,
            mensagem,
            severidade,
            acaoTexto,
            acao));

        if (_itens.Count > LimiteItens)
        {
            _itens.RemoveRange(LimiteItens, _itens.Count - LimiteItens);
        }

        OnAlterado?.Invoke();
    }

    public static void Descartar(StatusNotificacao item)
    {
        if (item == null) return;
        item.Descartada = true;
        OnAlterado?.Invoke();
    }

    public static void Limpar()
    {
        if (_itens.Count == 0)
        {
            return;
        }

        _itens.Clear();
        OnAlterado?.Invoke();
    }
}
