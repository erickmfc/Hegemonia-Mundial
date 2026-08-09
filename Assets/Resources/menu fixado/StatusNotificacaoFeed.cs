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

    public StatusNotificacao(string categoria, string titulo, string mensagem, StatusNotificacaoSeveridade severidade)
    {
        Categoria = categoria;
        Titulo = titulo;
        Mensagem = mensagem;
        Severidade = severidade;
        Horario = DateTime.Now.ToString("HH:mm");
    }
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

    public static void Publicar(string categoria, string titulo, string mensagem, StatusNotificacaoSeveridade severidade = StatusNotificacaoSeveridade.Info)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(mensagem))
        {
            return;
        }

        _itens.Insert(0, new StatusNotificacao(
            string.IsNullOrWhiteSpace(categoria) ? "GERAL" : categoria.ToUpperInvariant(),
            titulo,
            mensagem,
            severidade));

        if (_itens.Count > LimiteItens)
        {
            _itens.RemoveRange(LimiteItens, _itens.Count - LimiteItens);
        }

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
