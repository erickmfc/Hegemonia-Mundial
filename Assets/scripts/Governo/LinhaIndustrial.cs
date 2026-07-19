using System;
using UnityEngine;

public enum EstadoLinhaIndustrial
{
    Livre,
    ReservandoRecursos,
    Produzindo,
    PausadaSemEnergia,
    PausadaSemVerba,
    Concluida,
    Cancelada
}

[Serializable]
public class LinhaIndustrial
{
    public string id;
    public int teamId;
    public int indice;
    public EstadoLinhaIndustrial estado = EstadoLinhaIndustrial.Livre;
    public string ordemRefinoId;
    public string receitaId;
    public float progresso;
    public int diasTotais = 1;
    public int diasRestantes = 1;
    public string motivoBloqueio;

    public bool EstaLivre => estado == EstadoLinhaIndustrial.Livre;
    public bool EstaOcupada => estado == EstadoLinhaIndustrial.Produzindo || estado == EstadoLinhaIndustrial.ReservandoRecursos;

    public void Inicializar(int teamId, int indice)
    {
        this.teamId = teamId;
        this.indice = indice;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"{teamId}-L{indice + 1}";
        }
    }

    public void AtribuirOrdem(string ordemRefinoId, string receitaId, int diasTotais)
    {
        this.ordemRefinoId = ordemRefinoId;
        this.receitaId = receitaId;
        this.diasTotais = Mathf.Max(1, diasTotais);
        diasRestantes = this.diasTotais;
        progresso = 0f;
        estado = EstadoLinhaIndustrial.ReservandoRecursos;
        motivoBloqueio = string.Empty;
    }

    public void AtualizarEstado(EstadoLinhaIndustrial novoEstado)
    {
        estado = novoEstado;
    }

    public void AvancarDia(float progressoAdicional)
    {
        if (diasRestantes > 0)
        {
            diasRestantes--;
        }

        progresso = Mathf.Clamp01(progresso + progressoAdicional);
        if (diasRestantes <= 0)
        {
            estado = EstadoLinhaIndustrial.Concluida;
        }
    }

    public void Limpar()
    {
        ordemRefinoId = string.Empty;
        receitaId = string.Empty;
        progresso = 0f;
        diasTotais = 1;
        diasRestantes = 1;
        motivoBloqueio = string.Empty;
        estado = EstadoLinhaIndustrial.Livre;
    }
}
