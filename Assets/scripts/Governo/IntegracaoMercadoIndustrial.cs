using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hegemonia.AI.BrainMaster;

public static class IntegracaoMercadoIndustrial
{
    private static readonly HashSet<string> itensIndustriais = new HashSet<string>(
        IndustriaIds.TodosOsMateriais.Concat(new[] { "aco", "uranio", "petroleo" }),
        StringComparer.OrdinalIgnoreCase);

    public static bool EhItemIndustrial(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && itensIndustriais.Contains(itemId);
    }

    public static void GarantirCatalogoNoMercado(SistemaMercadoGlobal mercado)
    {
        if (mercado == null)
        {
            return;
        }

        SistemaIndustrialNacional sistema = SistemaIndustrialNacional.Instancia;
        if (sistema != null && sistema.RecursosCatalogo != null && sistema.RecursosCatalogo.Count > 0)
        {
            GarantirCatalogoIndustrial(mercado, sistema.RecursosCatalogo);
            return;
        }

        // Legacy fallback while the national catalog is still loading.
        RegistrarOuAtualizar(mercado, IndustriaIds.MinerioFerro, "Minerio de ferro", RecursoMercado.MinerioFerro, "Industrial - Bruto", 78, 50000, 72f, 60f, 0.07f, IndustriaIds.MinerioFerro);
        RegistrarOuAtualizar(mercado, IndustriaIds.MinerioCobre, "Minerio de cobre", RecursoMercado.MinerioCobre, "Industrial - Bruto", 118, 32000, 70f, 62f, 0.08f, IndustriaIds.MinerioCobre);
        RegistrarOuAtualizar(mercado, IndustriaIds.Bauxita, "Bauxita", RecursoMercado.Bauxita, "Industrial - Bruto", 95, 26000, 68f, 58f, 0.08f, IndustriaIds.Bauxita);
        RegistrarOuAtualizar(mercado, IndustriaIds.MinerioTitanio, "Minerio de titanio", RecursoMercado.MinerioTitanio, "Industrial - Estrategico", 260, 12000, 42f, 68f, 0.12f, IndustriaIds.MinerioTitanio);
        RegistrarOuAtualizar(mercado, IndustriaIds.CobreEletrolitico, "Cobre eletrolitico", RecursoMercado.CobreEletrolitico, "Industrial - Refinado", 240, 18000, 76f, 54f, 0.08f, IndustriaIds.CobreEletrolitico);
        RegistrarOuAtualizar(mercado, IndustriaIds.Duraluminio, "Duraluminio", RecursoMercado.Duraluminio, "Industrial - Refinado", 320, 12000, 68f, 58f, 0.09f, IndustriaIds.Duraluminio);
        RegistrarOuAtualizar(mercado, IndustriaIds.LigaTitanio, "Liga de titanio", RecursoMercado.LigaTitanio, "Industrial - Estrategico", 720, 5000, 38f, 72f, 0.13f, IndustriaIds.LigaTitanio);
        RegistrarOuAtualizar(mercado, IndustriaIds.ComponentesEletronicos, "Componentes eletronicos", RecursoMercado.ComponentesEletronicos, "Industrial - Estrategico", 980, 3200, 35f, 74f, 0.15f, IndustriaIds.ComponentesEletronicos);
        RegistrarOuAtualizar(mercado, IndustriaIds.UranioEnriquecido, "Uranio enriquecido", RecursoMercado.UranioEnriquecido, "Nuclear - Estrategico", 5200, 500, 18f, 82f, 0.20f, IndustriaIds.UranioEnriquecido);
    }

    public static void SincronizarEstoquesNoMercado(SistemaIndustrialNacional sistema, SistemaMercadoGlobal mercado)
    {
        if (sistema == null || mercado == null || SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        GarantirCatalogoNoMercado(mercado);

        foreach (RecursoIndustrialSO recurso in sistema.RecursosCatalogo)
        {
            if (recurso == null || string.IsNullOrWhiteSpace(recurso.id))
            {
                continue;
            }

            AtualizarEstoqueGlobal(mercado, recurso.id, sistema.ObterQuantidadeTotal(recurso.id));
        }
    }

    public static void ProcessarTransacao(TransacaoMercado transacao)
    {
        if (transacao == null || !EhItemIndustrial(transacao.itemId) || SistemaGovernoMundial.Instancia == null)
        {
            return;
        }

        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        DadosItemMercado item = mercado != null ? mercado.ObterItem(transacao.itemId) : null;
        if (item == null)
        {
            return;
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return;
        }

        DadosPaisGoverno comprador = SistemaGovernoMundial.Instancia.ObterPais(transacao.compradorTeamId);
        DadosPaisGoverno vendedor = SistemaGovernoMundial.Instancia.ObterPais(transacao.vendedorTeamId);
        string recursoId = ObterRecursoIdEfetivo(item);

        if (comprador != null && transacao.quantidade > 0)
        {
            bool ameaca = comprador.sancionado || comprador.estabilidade < 45f || comprador.emGuerra;
            industrial.ImpactoPublico.RegistrarCompra(
                comprador,
                recursoId,
                transacao.quantidade,
                transacao.total,
                comprador.emGuerra,
                ameaca,
                transacao.mensagem);
        }

        if (vendedor != null && transacao.quantidade > 0)
        {
            bool abaixoReserva = industrial.EstoqueAbaixoReservaMinima(vendedor.teamId, recursoId);
            bool ameaca = vendedor.sancionado || vendedor.estabilidade < 45f || vendedor.emGuerra;
            industrial.ImpactoPublico.RegistrarVenda(
                vendedor,
                recursoId,
                transacao.quantidade,
                transacao.total,
                abaixoReserva,
                vendedor.emGuerra,
                ameaca,
                transacao.mensagem);
        }
    }

    public static string IdInternoDoMercado(RecursoMercado recurso)
    {
        switch (recurso)
        {
            case RecursoMercado.Comida: return "comida";
            case RecursoMercado.Petroleo: return IndustriaIds.PetroleoBruto;
            case RecursoMercado.Energia: return "energia";
            case RecursoMercado.Aco: return IndustriaIds.AcoEstrutural;
            case RecursoMercado.Armamentos: return "armamentos";
            case RecursoMercado.Uranio: return IndustriaIds.UranioBruto;
            case RecursoMercado.MinerioFerro: return IndustriaIds.MinerioFerro;
            case RecursoMercado.MinerioCobre: return IndustriaIds.MinerioCobre;
            case RecursoMercado.Bauxita: return IndustriaIds.Bauxita;
            case RecursoMercado.MinerioTitanio: return IndustriaIds.MinerioTitanio;
            case RecursoMercado.CobreEletrolitico: return IndustriaIds.CobreEletrolitico;
            case RecursoMercado.Duraluminio: return IndustriaIds.Duraluminio;
            case RecursoMercado.LigaTitanio: return IndustriaIds.LigaTitanio;
            case RecursoMercado.ComponentesEletronicos: return IndustriaIds.ComponentesEletronicos;
            case RecursoMercado.UranioEnriquecido: return IndustriaIds.UranioEnriquecido;
            default: return string.Empty;
        }
    }

    public static double ReservaMinimaPadrao(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return 0d;
        }

        string chave = IA_Text.Normalize(recursoId);

        if (string.Equals(chave, "aco", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chave, IndustriaIds.AcoEstrutural, StringComparison.OrdinalIgnoreCase))
        {
            return 750d;
        }

        if (string.Equals(chave, "uranio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chave, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
        {
            return 500d;
        }

        if (string.Equals(chave, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return 1d;
        }

        if (IndustriaIds.EhCombustivelIndustrial(chave))
        {
            return 1500d;
        }

        if (IndustriaIds.EhComponenteIndustrial(chave))
        {
            return 250d;
        }

        if (IndustriaIds.EhMaterialRefinado(chave))
        {
            return 1200d;
        }

        if (IndustriaIds.EhRecursoBruto(chave))
        {
            return 2500d;
        }

        return 5000d;
    }

    private static void GarantirCatalogoIndustrial(SistemaMercadoGlobal mercado, IEnumerable<RecursoIndustrialSO> recursos)
    {
        if (mercado == null || recursos == null)
        {
            return;
        }

        foreach (RecursoIndustrialSO recurso in recursos)
        {
            if (recurso == null)
            {
                continue;
            }

            string id = IA_Text.Normalize(recurso.id);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string nome = string.IsNullOrWhiteSpace(recurso.nome) ? recurso.id : recurso.nome;
            RecursoMercado recursoMercado = ConverterRecursoMercado(id);
            string categoria = CategoriaDoRecurso(id, recurso);
            int precoBase = Mathf.Max(1, recurso.precoBase);
            int estoqueBase = CalcularEstoqueBase(id, recurso);
            float ofertaBase = CalcularOfertaBase(id, recurso);
            float demandaBase = CalcularDemandaBase(id, recurso);
            float volatilidadeBase = CalcularVolatilidadeBase(id, recurso);

            RegistrarOuAtualizar(mercado, id, nome, recursoMercado, categoria, precoBase, estoqueBase, ofertaBase, demandaBase, volatilidadeBase, id);
        }
    }

    private static string CategoriaDoRecurso(string recursoId, RecursoIndustrialSO recurso)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return "Industrial";
        }

        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return "Industrial - Bruto";
        }

        if (IndustriaIds.EhCombustivelIndustrial(recursoId))
        {
            return "Industrial - Combustivel";
        }

        if (IndustriaIds.EhComponenteIndustrial(recursoId))
        {
            return "Industrial - Componentes";
        }

        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return "Nuclear - Estrategico";
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            return recurso != null && recurso.estrategico ? "Industrial - Estrategico" : "Industrial - Refinado";
        }

        return recurso != null && recurso.estrategico ? "Industrial - Estrategico" : "Industrial - Refinado";
    }

    private static RecursoMercado ConverterRecursoMercado(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return RecursoMercado.Nenhum;
        }

        if (string.Equals(recursoId, IndustriaIds.MinerioFerro, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.MinerioFerro;
        }

        if (string.Equals(recursoId, IndustriaIds.MinerioCobre, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.MinerioCobre;
        }

        if (string.Equals(recursoId, IndustriaIds.Bauxita, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.Bauxita;
        }

        if (string.Equals(recursoId, IndustriaIds.MinerioTitanio, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.MinerioTitanio;
        }

        if (string.Equals(recursoId, IndustriaIds.AcoEstrutural, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.Aco;
        }

        if (string.Equals(recursoId, IndustriaIds.CobreEletrolitico, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.CobreEletrolitico;
        }

        if (string.Equals(recursoId, IndustriaIds.Duraluminio, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.Duraluminio;
        }

        if (string.Equals(recursoId, IndustriaIds.LigaTitanio, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.LigaTitanio;
        }

        if (string.Equals(recursoId, IndustriaIds.ComponentesEletronicos, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.ComponentesEletronicos;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.Uranio;
        }

        if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.UranioEnriquecido;
        }

        if (string.Equals(recursoId, IndustriaIds.PetroleoBruto, StringComparison.OrdinalIgnoreCase))
        {
            return RecursoMercado.Petroleo;
        }

        return RecursoMercado.Nenhum;
    }

    private static int CalcularEstoqueBase(string recursoId, RecursoIndustrialSO recurso)
    {
        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return recurso != null && recurso.estrategico ? 12000 : 50000;
        }

        if (IndustriaIds.EhCombustivelIndustrial(recursoId))
        {
            return 15000;
        }

        if (IndustriaIds.EhComponenteIndustrial(recursoId))
        {
            return recurso != null && recurso.estrategico ? 1200 : 3200;
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            if (string.Equals(recursoId, IndustriaIds.UranioEnriquecido, StringComparison.OrdinalIgnoreCase))
            {
                return 500;
            }

            return recurso != null && recurso.estrategico ? 5000 : 12000;
        }

        return 5000;
    }

    private static float CalcularOfertaBase(string recursoId, RecursoIndustrialSO recurso)
    {
        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return recurso != null && recurso.estrategico ? 42f : 72f;
        }

        if (IndustriaIds.EhCombustivelIndustrial(recursoId))
        {
            return 60f;
        }

        if (IndustriaIds.EhComponenteIndustrial(recursoId))
        {
            return recurso != null && recurso.estrategico ? 32f : 38f;
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            return recurso != null && recurso.estrategico ? 44f : 76f;
        }

        return 50f;
    }

    private static float CalcularDemandaBase(string recursoId, RecursoIndustrialSO recurso)
    {
        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return recurso != null && recurso.estrategico ? 68f : 60f;
        }

        if (IndustriaIds.EhCombustivelIndustrial(recursoId))
        {
            return 66f;
        }

        if (IndustriaIds.EhComponenteIndustrial(recursoId))
        {
            return recurso != null && recurso.estrategico ? 78f : 72f;
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            return recurso != null && recurso.estrategico ? 82f : 54f;
        }

        return 50f;
    }

    private static float CalcularVolatilidadeBase(string recursoId, RecursoIndustrialSO recurso)
    {
        if (IndustriaIds.EhRecursoBruto(recursoId))
        {
            return recurso != null && recurso.estrategico ? 0.12f : 0.08f;
        }

        if (IndustriaIds.EhCombustivelIndustrial(recursoId))
        {
            return 0.11f;
        }

        if (IndustriaIds.EhComponenteIndustrial(recursoId))
        {
            return recurso != null && recurso.estrategico ? 0.16f : 0.14f;
        }

        if (IndustriaIds.EhMaterialRefinado(recursoId))
        {
            return recurso != null && recurso.estrategico ? 0.13f : 0.08f;
        }

        return 0.08f;
    }

    private static void RegistrarOuAtualizar(SistemaMercadoGlobal mercado, string id, string nome, RecursoMercado recurso, string categoria, int precoBase, int estoqueGlobal, float oferta, float demanda, float volatilidade, string recursoId = null)
    {
        DadosItemMercado item = mercado.ObterItem(id);
        if (item == null)
        {
            item = new DadosItemMercado { id = id };
            mercado.RegistrarItem(item);
        }

        item.nome = nome;
        item.categoria = categoria;
        item.recurso = recurso;
        if (!string.IsNullOrWhiteSpace(recursoId))
        {
            item.recursoId = recursoId;
        }
        else if (string.IsNullOrWhiteSpace(item.recursoId))
        {
            item.recursoId = id;
        }

        item.precoBase = precoBase;
        item.precoAtual = Mathf.Max(1, item.precoAtual <= 0 ? precoBase : item.precoAtual);
        item.estoqueGlobal = Mathf.Max(0, estoqueGlobal);
        item.oferta = oferta;
        item.demanda = demanda;
        item.volatilidade = volatilidade;
        item.podeComprar = true;
        item.podeVender = true;
    }

    private static void AtualizarEstoqueGlobal(SistemaMercadoGlobal mercado, string recursoId, double quantidade)
    {
        DadosItemMercado item = mercado.ObterItem(recursoId);
        if (item == null)
        {
            return;
        }

        item.estoqueGlobal = Mathf.Max(0, Mathf.RoundToInt((float)quantidade));
    }

    private static string ObterRecursoIdEfetivo(DadosItemMercado item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.recursoId))
        {
            return IA_Text.Normalize(item.recursoId);
        }

        return IA_Text.Normalize(item.id);
    }
}
