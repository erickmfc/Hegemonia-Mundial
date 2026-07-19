using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ImpactoPublicoIndustrial
{
    private readonly List<SaveImpactoPublicoIndustrial> pendentes = new List<SaveImpactoPublicoIndustrial>();

    public IReadOnlyList<SaveImpactoPublicoIndustrial> Pendentes => pendentes;

    public void RegistrarCompra(DadosPaisGoverno pais, string recursoId, double quantidade, double valorTotal, bool emGuerra, bool emAmeaca, string mensagem)
    {
        if (pais == null || quantidade <= 0d || string.IsNullOrWhiteSpace(recursoId))
        {
            return;
        }

        recursoId = NormalizarRecursoId(recursoId);
        float impacto = CalcularImpactoCompra(pais, recursoId, quantidade, valorTotal, emGuerra, emAmeaca);
        if (Mathf.Approximately(impacto, 0f))
        {
            return;
        }

        pendentes.Add(new SaveImpactoPublicoIndustrial
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant(),
            teamId = pais.teamId,
            recursoId = recursoId,
            quantidade = quantidade,
            deltaFelicidade = impacto,
            deltaEstabilidade = CalcularEstabilidadeCompra(recursoId, impacto),
            compra = true,
            venda = false,
            mensagem = mensagem,
            diaCriacao = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1,
            diaAplicacao = (GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1) + 1,
            aplicado = false
        });
    }

    public void RegistrarVenda(DadosPaisGoverno pais, string recursoId, double quantidade, double valorTotal, bool deixouSemReserva, bool emGuerra, bool emAmeaca, string mensagem)
    {
        if (pais == null || quantidade <= 0d || string.IsNullOrWhiteSpace(recursoId))
        {
            return;
        }

        recursoId = NormalizarRecursoId(recursoId);
        float impacto = CalcularImpactoVenda(pais, recursoId, quantidade, valorTotal, deixouSemReserva, emGuerra, emAmeaca);
        if (Mathf.Approximately(impacto, 0f))
        {
            return;
        }

        pendentes.Add(new SaveImpactoPublicoIndustrial
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant(),
            teamId = pais.teamId,
            recursoId = recursoId,
            quantidade = quantidade,
            deltaFelicidade = impacto,
            deltaEstabilidade = CalcularEstabilidadeVenda(recursoId, impacto, deixouSemReserva),
            compra = false,
            venda = true,
            mensagem = mensagem,
            diaCriacao = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1,
            diaAplicacao = (GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1) + 1,
            aplicado = false
        });
    }

    public void ProcessarPendentes(int diaAtual, SistemaGovernoMundial governo)
    {
        if (governo == null || pendentes.Count == 0)
        {
            return;
        }

        Dictionary<int, AcumuladoImpactoDia> acumulados = new Dictionary<int, AcumuladoImpactoDia>();

        for (int i = 0; i < pendentes.Count; i++)
        {
            SaveImpactoPublicoIndustrial impacto = pendentes[i];
            if (impacto == null || impacto.aplicado || impacto.diaAplicacao > diaAtual)
            {
                continue;
            }

            if (!acumulados.TryGetValue(impacto.teamId, out AcumuladoImpactoDia acumulado))
            {
                acumulado = new AcumuladoImpactoDia();
                acumulados[impacto.teamId] = acumulado;
            }

            acumulado.Adicionar(impacto);
            impacto.aplicado = true;
        }

        foreach (KeyValuePair<int, AcumuladoImpactoDia> par in acumulados)
        {
            DadosPaisGoverno pais = governo.ObterPais(par.Key);
            if (pais == null)
            {
                continue;
            }

            float deltaCompra = Mathf.Clamp(par.Value.deltaCompra, -5f, 0f);
            float deltaVenda = Mathf.Clamp(par.Value.deltaVenda, 0f, 2f);
            float deltaFelicidade = deltaCompra + deltaVenda;
            float deltaEstabilidade = par.Value.deltaEstabilidade;

            pais.felicidade = Mathf.Clamp(pais.felicidade + deltaFelicidade, 0f, 100f);
            pais.estabilidade = Mathf.Clamp(pais.estabilidade + deltaEstabilidade, 0f, 100f);
        }

        pendentes.RemoveAll(impacto => impacto == null || impacto.aplicado);
    }

    public List<SaveImpactoPublicoIndustrial> CriarSnapshot()
    {
        List<SaveImpactoPublicoIndustrial> copia = new List<SaveImpactoPublicoIndustrial>(pendentes.Count);
        for (int i = 0; i < pendentes.Count; i++)
        {
            SaveImpactoPublicoIndustrial item = pendentes[i];
            if (item == null)
            {
                continue;
            }

            copia.Add(new SaveImpactoPublicoIndustrial
            {
                id = item.id,
                teamId = item.teamId,
                recursoId = item.recursoId,
                quantidade = item.quantidade,
                deltaFelicidade = item.deltaFelicidade,
                deltaEstabilidade = item.deltaEstabilidade,
                compra = item.compra,
                venda = item.venda,
                mensagem = item.mensagem,
                diaCriacao = item.diaCriacao,
                diaAplicacao = item.diaAplicacao,
                aplicado = item.aplicado
            });
        }
        return copia;
    }

    public void AplicarSnapshot(IEnumerable<SaveImpactoPublicoIndustrial> snapshot)
    {
        pendentes.Clear();
        if (snapshot == null)
        {
            return;
        }

        foreach (SaveImpactoPublicoIndustrial item in snapshot)
        {
            if (item == null)
            {
                continue;
            }
            pendentes.Add(item);
        }
    }

    private static float CalcularImpactoCompra(DadosPaisGoverno pais, string recursoId, double quantidade, double valorTotal, bool emGuerra, bool emAmeaca)
    {
        float impactoBase = CalcularImpactoPorVolume(valorTotal, pais);
        float fatorEstrategico = FatorEstrategicoCompra(recursoId);
        float impacto = -(impactoBase + fatorEstrategico);

        if (emGuerra || emAmeaca)
        {
            impacto *= 0.5f;
        }

        return Mathf.Clamp(impacto, -4f, 0f);
    }

    private static float CalcularImpactoVenda(DadosPaisGoverno pais, string recursoId, double quantidade, double valorTotal, bool deixouSemReserva, bool emGuerra, bool emAmeaca)
    {
        float impactoBase = CalcularImpactoPositivoPorVolume(valorTotal, pais);
        float bonus = FatorEstrategicoVenda(recursoId);
        float impacto = impactoBase + bonus;

        if (deixouSemReserva)
        {
            impacto -= 2f;
        }

        if (emGuerra || emAmeaca)
        {
            impacto *= 0.5f;
        }

        return Mathf.Clamp(impacto, -4f, 2f);
    }

    private static float CalcularEstabilidadeCompra(string recursoId, float impactoFelicidade)
    {
        recursoId = NormalizarRecursoId(recursoId);
        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return -1f;
        }

        if (Mathf.Abs(impactoFelicidade) <= 1f)
        {
            return 0f;
        }

        return impactoFelicidade < -1f ? -0.25f : 0f;
    }

    private static float CalcularEstabilidadeVenda(string recursoId, float impactoFelicidade, bool deixouSemReserva)
    {
        recursoId = NormalizarRecursoId(recursoId);
        if (deixouSemReserva)
        {
            return -1f;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return 0.5f;
        }

        return impactoFelicidade > 0f ? 0.25f : 0f;
    }

    private static float CalcularImpactoPorVolume(double valorTotal, DadosPaisGoverno pais)
    {
        float saldoBase = Mathf.Max(1f, pais != null ? pais.saldo + (float)valorTotal : (float)valorTotal);
        float percentual = Mathf.Clamp01((float)valorTotal / saldoBase);
        if (percentual < 0.05f)
        {
            return 0f;
        }

        if (percentual < 0.12f)
        {
            return 1f;
        }

        return 2f;
    }

    private static float CalcularImpactoPositivoPorVolume(double valorTotal, DadosPaisGoverno pais)
    {
        float saldoBase = Mathf.Max(1f, pais != null ? pais.saldo + (float)valorTotal : (float)valorTotal);
        float percentual = Mathf.Clamp01((float)valorTotal / saldoBase);
        if (percentual < 0.06f)
        {
            return 0f;
        }

        if (percentual < 0.18f)
        {
            return 0.5f;
        }

        return 1f;
    }

    private static float FatorEstrategicoCompra(string recursoId)
    {
        recursoId = NormalizarRecursoId(recursoId);
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return 0f;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return 4f;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
        {
            return 2f;
        }

        if (string.Equals(recursoId, IndustriaIds.MinerioTitanio, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recursoId, IndustriaIds.LigaTitanio, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recursoId, IndustriaIds.ComponentesEletronicos, StringComparison.OrdinalIgnoreCase))
        {
            return 1.5f;
        }

        return 0f;
    }

    private static float FatorEstrategicoVenda(string recursoId)
    {
        recursoId = NormalizarRecursoId(recursoId);
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return 0f;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return 1f;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
        {
            return 0.5f;
        }

        if (string.Equals(recursoId, IndustriaIds.ComponentesEletronicos, StringComparison.OrdinalIgnoreCase))
        {
            return 0.5f;
        }

        return 0.25f;
    }

    private static string NormalizarRecursoId(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return string.Empty;
        }

        if (string.Equals(recursoId, "aco", StringComparison.OrdinalIgnoreCase))
        {
            return IndustriaIds.AcoEstrutural;
        }

        if (string.Equals(recursoId, "uranio", StringComparison.OrdinalIgnoreCase))
        {
            return IndustriaIds.UranioBruto;
        }

        return recursoId.Trim();
    }

    private class AcumuladoImpactoDia
    {
        public float deltaCompra;
        public float deltaVenda;
        public float deltaEstabilidade;

        public void Adicionar(SaveImpactoPublicoIndustrial impacto)
        {
            if (impacto == null)
            {
                return;
            }

            if (impacto.compra)
            {
                deltaCompra += impacto.deltaFelicidade;
            }

            if (impacto.venda)
            {
                deltaVenda += impacto.deltaFelicidade;
            }

            deltaEstabilidade += impacto.deltaEstabilidade;
        }
    }
}
