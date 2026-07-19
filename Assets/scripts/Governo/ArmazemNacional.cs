using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class ArmazemNacional
{
    private readonly Dictionary<string, Dictionary<string, double>> estoques =
        new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<string, double>> reservas =
        new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

    public double capacidadeMaximaPorPais = 0d;
    public double capacidadeMaximaPorRecurso = 0d;

    public bool Possui(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        return ObterDisponivel(paisId, recursoId) >= quantidade;
    }

    public bool TentarConsumir(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return false;
        }

        double disponivel = ObterDisponivel(chavePais, chaveRecurso);
        if (disponivel < quantidade)
        {
            return false;
        }

        AjustarEstoque(chavePais, chaveRecurso, -quantidade);
        LimparSeVazio(chavePais, chaveRecurso);
        return true;
    }

    public void Adicionar(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d)
        {
            return;
        }

        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return;
        }

        AjustarEstoque(chavePais, chaveRecurso, quantidade);
    }

    public double ObterQuantidade(string paisId, string recursoId)
    {
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return 0d;
        }

        if (!estoques.TryGetValue(chavePais, out Dictionary<string, double> recursos))
        {
            return 0d;
        }

        return recursos.TryGetValue(chaveRecurso, out double quantidade) ? quantidade : 0d;
    }

    public double ObterReservado(string paisId, string recursoId)
    {
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return 0d;
        }

        if (!reservas.TryGetValue(chavePais, out Dictionary<string, double> recursos))
        {
            return 0d;
        }

        return recursos.TryGetValue(chaveRecurso, out double quantidade) ? quantidade : 0d;
    }

    public double ObterDisponivel(string paisId, string recursoId)
    {
        return Math.Max(0d, ObterQuantidade(paisId, recursoId) - ObterReservado(paisId, recursoId));
    }

    public bool PodeArmazenar(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        if (capacidadeMaximaPorPais <= 0d && capacidadeMaximaPorRecurso <= 0d)
        {
            return true;
        }

        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return false;
        }

        double totalPais = 0d;
        if (estoques.TryGetValue(chavePais, out Dictionary<string, double> recursosPais))
        {
            totalPais = recursosPais.Values.Sum();
        }

        if (capacidadeMaximaPorPais > 0d && totalPais + quantidade > capacidadeMaximaPorPais)
        {
            return false;
        }

        if (capacidadeMaximaPorRecurso > 0d && ObterQuantidade(chavePais, chaveRecurso) + quantidade > capacidadeMaximaPorRecurso)
        {
            return false;
        }

        return true;
    }

    public bool Reservar(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return false;
        }

        if (ObterDisponivel(chavePais, chaveRecurso) < quantidade)
        {
            return false;
        }

        AjustarEstoque(chavePais, chaveRecurso, -quantidade);
        AjustarReserva(chavePais, chaveRecurso, quantidade);
        LimparSeVazio(chavePais, chaveRecurso);
        return true;
    }

    public bool LiberarReserva(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return false;
        }

        if (ObterReservado(chavePais, chaveRecurso) < quantidade)
        {
            return false;
        }

        AjustarReserva(chavePais, chaveRecurso, -quantidade);
        AjustarEstoque(chavePais, chaveRecurso, quantidade);
        LimparSeVazio(chavePais, chaveRecurso);
        return true;
    }

    public bool ConsumirReserva(string paisId, string recursoId, double quantidade)
    {
        if (quantidade <= 0d) return true;
        string chavePais = NormalizarPais(paisId);
        string chaveRecurso = NormalizarRecurso(recursoId);
        if (string.IsNullOrWhiteSpace(chavePais) || string.IsNullOrWhiteSpace(chaveRecurso))
        {
            return false;
        }

        if (ObterReservado(chavePais, chaveRecurso) < quantidade)
        {
            return false;
        }

        AjustarReserva(chavePais, chaveRecurso, -quantidade);
        LimparSeVazio(chavePais, chaveRecurso);
        return true;
    }

    public List<QuantidadeRecursoIndustrial> ObterEstoquePais(string paisId)
    {
        string chavePais = NormalizarPais(paisId);
        if (string.IsNullOrWhiteSpace(chavePais) || !estoques.TryGetValue(chavePais, out Dictionary<string, double> recursos))
        {
            return new List<QuantidadeRecursoIndustrial>();
        }

        return recursos
            .Where(kvp => kvp.Value > 0d)
            .Select(kvp => new QuantidadeRecursoIndustrial(kvp.Key, kvp.Value))
            .ToList();
    }

    public List<QuantidadeRecursoIndustrial> ObterReservasPais(string paisId)
    {
        string chavePais = NormalizarPais(paisId);
        if (string.IsNullOrWhiteSpace(chavePais) || !reservas.TryGetValue(chavePais, out Dictionary<string, double> recursos))
        {
            return new List<QuantidadeRecursoIndustrial>();
        }

        return recursos
            .Where(kvp => kvp.Value > 0d)
            .Select(kvp => new QuantidadeRecursoIndustrial(kvp.Key, kvp.Value))
            .ToList();
    }

    public void AplicarSnapshot(string paisId, IEnumerable<QuantidadeRecursoIndustrial> estoque, IEnumerable<QuantidadeRecursoIndustrial> reservasPais)
    {
        string chavePais = NormalizarPais(paisId);
        if (string.IsNullOrWhiteSpace(chavePais))
        {
            return;
        }

        estoques[chavePais] = ConverterMapa(estoque);
        reservas[chavePais] = ConverterMapa(reservasPais);
    }

    public SaveEstoqueIndustrial CriarSnapshot(string paisId)
    {
        string chavePais = NormalizarPais(paisId);
        SaveEstoqueIndustrial snapshot = new SaveEstoqueIndustrial
        {
            paisId = chavePais
        };

        snapshot.estoques.AddRange(ObterEstoquePais(chavePais));
        snapshot.reservas.AddRange(ObterReservasPais(chavePais));
        return snapshot;
    }

    private static string NormalizarPais(string paisId)
    {
        return string.IsNullOrWhiteSpace(paisId) ? string.Empty : paisId.Trim();
    }

    private static string NormalizarRecurso(string recursoId)
    {
        return string.IsNullOrWhiteSpace(recursoId) ? string.Empty : recursoId.Trim().ToLowerInvariant();
    }

    private Dictionary<string, double> ObterOuCriarMapa(Dictionary<string, Dictionary<string, double>> origem, string paisId)
    {
        if (!origem.TryGetValue(paisId, out Dictionary<string, double> mapa))
        {
            mapa = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            origem[paisId] = mapa;
        }

        return mapa;
    }

    private void AjustarEstoque(string paisId, string recursoId, double delta)
    {
        Dictionary<string, double> mapa = ObterOuCriarMapa(estoques, paisId);
        mapa.TryGetValue(recursoId, out double atual);
        atual += delta;
        if (atual <= 0d)
        {
            mapa.Remove(recursoId);
        }
        else
        {
            mapa[recursoId] = atual;
        }
    }

    private void AjustarReserva(string paisId, string recursoId, double delta)
    {
        Dictionary<string, double> mapa = ObterOuCriarMapa(reservas, paisId);
        mapa.TryGetValue(recursoId, out double atual);
        atual += delta;
        if (atual <= 0d)
        {
            mapa.Remove(recursoId);
        }
        else
        {
            mapa[recursoId] = atual;
        }
    }

    private void LimparSeVazio(string paisId, string recursoId)
    {
        if (estoques.TryGetValue(paisId, out Dictionary<string, double> mapaEstoque) && mapaEstoque.Count == 0)
        {
            estoques.Remove(paisId);
        }

        if (reservas.TryGetValue(paisId, out Dictionary<string, double> mapaReserva) && mapaReserva.Count == 0)
        {
            reservas.Remove(paisId);
        }
    }

    private static Dictionary<string, double> ConverterMapa(IEnumerable<QuantidadeRecursoIndustrial> valores)
    {
        Dictionary<string, double> mapa = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (valores == null)
        {
            return mapa;
        }

        foreach (QuantidadeRecursoIndustrial item in valores)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.recursoId) || item.quantidade <= 0d)
            {
                continue;
            }

            string recursoId = NormalizarRecurso(item.recursoId);
            if (mapa.TryGetValue(recursoId, out double atual))
            {
                mapa[recursoId] = atual + item.quantidade;
            }
            else
            {
                mapa[recursoId] = item.quantidade;
            }
        }

        return mapa;
    }
}
