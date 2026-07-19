using System;
using UnityEngine;

public enum EstadoOrdemExtracaoIndustrial
{
    Bloqueada,
    Aguardando,
    Ativa,
    Pausada,
    SemEnergia,
    SemVerba,
    ConcluindoCiclo
}

[Serializable]
public class OrdemExtracaoIndustrial
{
    public string id;
    public int teamId;
    public string paisId;
    public string recursoId;
    public string nomeRecurso;
    public EstadoOrdemExtracaoIndustrial estado = EstadoOrdemExtracaoIndustrial.Aguardando;
    public bool continua = true;
    public int diasObjetivo = 1;
    public int diasRestantes = 1;
    public double quantidadeAlvo;
    public double quantidadeRestante;
    public double estoqueAlvo;
    public double totalProduzido;
    public float custoDinheiro = 400f;
    public float custoEnergia = 50f;
    public float producaoBase = 500f;
    public float producaoUltimoDia;
    public bool exigeAutorizacao;
    public bool autorizada;
    public string motivoBloqueio;
    public int ultimaDataProcessada;

    public void Inicializar()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        }

        if (diasObjetivo <= 0)
        {
            diasObjetivo = 1;
        }

        if (diasRestantes <= 0)
        {
            diasRestantes = diasObjetivo;
        }
    }

    public bool EstaBloqueada => estado == EstadoOrdemExtracaoIndustrial.Bloqueada;
    public bool EstaPausada => estado == EstadoOrdemExtracaoIndustrial.Pausada;
    public bool EstaAtiva => estado == EstadoOrdemExtracaoIndustrial.Ativa;
    public bool PrecisaAutorizacao => exigeAutorizacao && !autorizada;

    public void DefinirBloqueio(string motivo)
    {
        motivoBloqueio = motivo;
        estado = EstadoOrdemExtracaoIndustrial.Bloqueada;
    }

    public void Liberar()
    {
        motivoBloqueio = string.Empty;
        estado = EstadoOrdemExtracaoIndustrial.Aguardando;
    }

    public void Pausar()
    {
        estado = EstadoOrdemExtracaoIndustrial.Pausada;
    }

    public void Retomar()
    {
        if (estado == EstadoOrdemExtracaoIndustrial.Pausada ||
            estado == EstadoOrdemExtracaoIndustrial.SemEnergia ||
            estado == EstadoOrdemExtracaoIndustrial.SemVerba)
        {
            estado = EstadoOrdemExtracaoIndustrial.Aguardando;
        }
    }

    public void RegistrarEntrega(double quantidade)
    {
        if (quantidade <= 0d) return;
        totalProduzido += quantidade;
        quantidadeRestante = Math.Max(0d, quantidadeRestante - quantidade);
    }

    public bool DeveEncerrarPorQuantidade()
    {
        return quantidadeAlvo > 0d && quantidadeRestante <= 0d;
    }

    public bool DeveEncerrarPorEstoque(double estoqueAtual)
    {
        return estoqueAlvo > 0d && estoqueAtual >= estoqueAlvo;
    }

    public void MarcarCicloConcluido()
    {
        if (diasRestantes > 0)
        {
            diasRestantes--;
        }

        if (diasRestantes <= 0)
        {
            estado = EstadoOrdemExtracaoIndustrial.ConcluindoCiclo;
        }
    }

    public void ReiniciarCiclo()
    {
        diasRestantes = Mathf.Max(1, diasObjetivo);
        if (!EstaBloqueada && !EstaPausada)
        {
            estado = EstadoOrdemExtracaoIndustrial.Aguardando;
        }
    }
}
