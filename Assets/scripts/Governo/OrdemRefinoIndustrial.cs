using System;
using System.Collections.Generic;
using UnityEngine;

public enum EstadoOrdemRefinoIndustrial
{
    Aguardando,
    ReservandoRecursos,
    Produzindo,
    PausadaSemEnergia,
    PausadaSemVerba,
    Concluida,
    Cancelada
}

[Serializable]
public class OrdemRefinoIndustrial
{
    public string id;
    public int teamId;
    public string paisId;
    public string receitaId;
    public string produtoId;
    public EstadoOrdemRefinoIndustrial estado = EstadoOrdemRefinoIndustrial.Aguardando;
    public string linhaId;
    public float progresso;
    public int diasTotais = 1;
    public int diasRestantes = 1;
    public double quantidadeEntrada;
    public double quantidadeProduzida;
    public double quantidadeResultadoPrevista;
    public double dinheiroReservado;
    public double energiaReservada;
    public List<QuantidadeRecursoIndustrial> materiaisReservados = new List<QuantidadeRecursoIndustrial>();
    public int inicioDia;
    public int ultimaDataProcessada;
    public string pesquisaExigida;
    public int nivelIndustrialExigido;
    public string motivoBloqueio;

    public void Inicializar()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        }

        if (diasTotais <= 0)
        {
            diasTotais = 1;
        }

        if (diasRestantes <= 0)
        {
            diasRestantes = diasTotais;
        }
    }

    public void DefinirEstado(EstadoOrdemRefinoIndustrial novoEstado)
    {
        estado = novoEstado;
    }

    public void AdicionarMaterialReservado(string recursoId, double quantidade)
    {
        if (string.IsNullOrWhiteSpace(recursoId) || quantidade <= 0d)
        {
            return;
        }

        QuantidadeRecursoIndustrial item = materiaisReservados.Find(m => string.Equals(m.recursoId, recursoId, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            item = new QuantidadeRecursoIndustrial(recursoId, 0d);
            materiaisReservados.Add(item);
        }

        item.quantidade += quantidade;
    }

    public double ObterQuantidadeReservada(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return 0d;
        }

        QuantidadeRecursoIndustrial item = materiaisReservados.Find(m => string.Equals(m.recursoId, recursoId, StringComparison.OrdinalIgnoreCase));
        return item != null ? item.quantidade : 0d;
    }

    public double TotalReservado()
    {
        double total = 0d;
        for (int i = 0; i < materiaisReservados.Count; i++)
        {
            total += materiaisReservados[i] != null ? materiaisReservados[i].quantidade : 0d;
        }
        return total;
    }

    public void RegistrarProgresso(float deltaNormalizado)
    {
        progresso = Mathf.Clamp01(progresso + deltaNormalizado);
    }

    public void RegistrarConclusao(double quantidadeSaida)
    {
        quantidadeProduzida += quantidadeSaida;
        progresso = 1f;
        estado = EstadoOrdemRefinoIndustrial.Concluida;
    }

    public void ReiniciarCiclo()
    {
        progresso = 0f;
        diasRestantes = Mathf.Max(1, diasTotais);
        if (estado != EstadoOrdemRefinoIndustrial.Cancelada)
        {
            estado = EstadoOrdemRefinoIndustrial.Aguardando;
        }
    }

    public void MarcarPausaSemEnergia()
    {
        estado = EstadoOrdemRefinoIndustrial.PausadaSemEnergia;
    }

    public void MarcarPausaSemVerba()
    {
        estado = EstadoOrdemRefinoIndustrial.PausadaSemVerba;
    }
}
