using System;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public static class CatalogoProdutoCompartilhado
{
    private static readonly Dictionary<string, CatalogoProdutoUnificadoItem> itensPorId = new Dictionary<string, CatalogoProdutoUnificadoItem>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CatalogoProdutoUnificadoItem> itensPorChave = new Dictionary<string, CatalogoProdutoUnificadoItem>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<CatalogoProdutoUnificadoItem> itensOrdenados = new List<CatalogoProdutoUnificadoItem>(256);
    private static bool inicializado;
    private static CatalogoProdutoUnificadoSO fonteScriptable;

    public static IReadOnlyList<CatalogoProdutoUnificadoItem> Itens
    {
        get
        {
            GarantirInicializado();
            return itensOrdenados;
        }
    }

    public static void GarantirInicializado()
    {
        if (inicializado)
        {
            return;
        }

        Recarregar();
    }

    public static void Recarregar()
    {
        itensPorId.Clear();
        itensPorChave.Clear();
        itensOrdenados.Clear();

        fonteScriptable = Resources.Load<CatalogoProdutoUnificadoSO>("CatalogoProdutoUnificado");
        if (fonteScriptable != null)
        {
            fonteScriptable.Normalizar();
            IReadOnlyList<CatalogoProdutoUnificadoItem> itensFonte = fonteScriptable.Itens;
            for (int i = 0; i < itensFonte.Count; i++)
            {
                RegistrarOuMesclar(Copiar(itensFonte[i]), "scriptable");
            }
        }

        inicializado = true;
        SincronizarFontesVivas();
    }

    public static void SincronizarFontesVivas()
    {
        GarantirInicializado();

        if (MenuConstrucao.catalogoGlobal != null)
        {
            RegistrarConstrucoes(MenuConstrucao.catalogoGlobal);
        }

        if (SistemaMercadoGlobal.Instancia != null)
        {
            RegistrarMercado(SistemaMercadoGlobal.Instancia.itens);
        }

        if (SistemaIndustrialNacional.Instancia != null)
        {
            RegistrarIndustrial(
                SistemaIndustrialNacional.Instancia.RecursosCatalogo,
                SistemaIndustrialNacional.Instancia.ReceitasCatalogo);
        }
    }

    public static IEnumerable<CatalogoProdutoUnificadoItem> Enumerar()
    {
        GarantirInicializado();
        return itensOrdenados;
    }

    public static bool TentarObter(string id, out CatalogoProdutoUnificadoItem item)
    {
        GarantirInicializado();
        string chave = IA_Text.Normalize(id);
        if (string.IsNullOrEmpty(chave))
        {
            item = null;
            return false;
        }

        if (itensPorId.TryGetValue(chave, out item))
        {
            return true;
        }

        if (itensPorChave.TryGetValue(chave, out item))
        {
            return true;
        }

        item = null;
        return false;
    }

    public static CatalogoProdutoUnificadoItem Obter(string id)
    {
        CatalogoProdutoUnificadoItem item;
        return TentarObter(id, out item) ? item : null;
    }

    public static CatalogoProdutoUnificadoItem RegistrarOuMesclar(CatalogoProdutoUnificadoItem item, string origem = null)
    {
        GarantirInicializado();
        if (item == null)
        {
            return null;
        }

        item.Normalizar();
        if (string.IsNullOrEmpty(item.id))
        {
            return null;
        }

        CatalogoProdutoUnificadoItem existente;
        if (itensPorId.TryGetValue(item.id, out existente))
        {
            Mesclar(existente, item);
            Indexar(existente);
            return existente;
        }

        CatalogoProdutoUnificadoItem copia = Copiar(item);
        itensPorId[copia.id] = copia;
        itensOrdenados.Add(copia);
        Indexar(copia);
        itensOrdenados.Sort(Comparar);

        if (!string.IsNullOrEmpty(origem) && Debug.isDebugBuild)
        {
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("catalogo_produto_" + origem, copia.id);
        }

        return copia;
    }

    public static void RegistrarConstrucoes(IEnumerable<DadosConstrucao> fichas)
    {
        GarantirInicializado();
        if (fichas == null)
        {
            return;
        }

        foreach (DadosConstrucao ficha in fichas)
        {
            CatalogoProdutoUnificadoItem item = ConverterConstrucao(ficha);
            if (item != null)
            {
                RegistrarOuMesclar(item, "construcao");
            }
        }
    }

    public static void RegistrarMercado(IEnumerable<DadosItemMercado> itensMercado)
    {
        GarantirInicializado();
        if (itensMercado == null)
        {
            return;
        }

        foreach (DadosItemMercado item in itensMercado)
        {
            CatalogoProdutoUnificadoItem convertido = ConverterMercado(item);
            if (convertido != null)
            {
                RegistrarOuMesclar(convertido, "mercado");
            }
        }
    }

    public static void RegistrarIndustrial(IEnumerable<RecursoIndustrialSO> recursos, IEnumerable<ReceitaIndustrialSO> receitas)
    {
        GarantirInicializado();

        if (recursos != null)
        {
            foreach (RecursoIndustrialSO recurso in recursos)
            {
                CatalogoProdutoUnificadoItem convertido = ConverterRecursoIndustrial(recurso);
                if (convertido != null)
                {
                    RegistrarOuMesclar(convertido, "industrial_recurso");
                }
            }
        }

        if (receitas != null)
        {
            foreach (ReceitaIndustrialSO receita in receitas)
            {
                CatalogoProdutoUnificadoItem convertido = ConverterReceitaIndustrial(receita);
                if (convertido != null)
                {
                    RegistrarOuMesclar(convertido, "industrial_receita");
                }
            }
        }
    }

    private static void Indexar(CatalogoProdutoUnificadoItem item)
    {
        if (item == null)
        {
            return;
        }

        RegistrarChave(item.id, item);
        RegistrarChave(item.nome, item);
        RegistrarChave(item.prefabId, item);
        RegistrarChave(item.linhaIndustrial, item);

        if (item.aliases != null)
        {
            for (int i = 0; i < item.aliases.Count; i++)
            {
                RegistrarChave(item.aliases[i], item);
            }
        }
    }

    private static void RegistrarChave(string chave, CatalogoProdutoUnificadoItem item)
    {
        string normalizada = IA_Text.Normalize(chave);
        if (string.IsNullOrEmpty(normalizada) || item == null)
        {
            return;
        }

        CatalogoProdutoUnificadoItem existente;
        if (itensPorChave.TryGetValue(normalizada, out existente))
        {
            if (existente == item)
            {
                return;
            }

            return;
        }

        itensPorChave[normalizada] = item;
    }

    private static void Mesclar(CatalogoProdutoUnificadoItem destino, CatalogoProdutoUnificadoItem origem)
    {
        if (destino == null || origem == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(destino.nome))
        {
            destino.nome = origem.nome;
        }

        if (string.IsNullOrEmpty(destino.descricao))
        {
            destino.descricao = origem.descricao;
        }

        if (destino.categoria == CategoriaProduto.Desconhecido)
        {
            destino.categoria = origem.categoria;
        }

        if (string.IsNullOrEmpty(destino.unidade))
        {
            destino.unidade = origem.unidade;
        }

        destino.materiais = MesclarMateriais(destino.materiais, origem.materiais);
        destino.estruturasNecessarias = MesclarListas(destino.estruturasNecessarias, origem.estruturasNecessarias);
        destino.pesquisasNecessarias = MesclarListas(destino.pesquisasNecessarias, origem.pesquisasNecessarias);
        destino.combustiveisAceitos = MesclarListas(destino.combustiveisAceitos, origem.combustiveisAceitos);
        destino.municoesAceitas = MesclarListas(destino.municoesAceitas, origem.municoesAceitas);
        destino.aliases = MesclarListas(destino.aliases, origem.aliases);

        if (destino.dinheiroNecessario <= 0d)
        {
            destino.dinheiroNecessario = origem.dinheiroNecessario;
        }

        if (destino.energiaNecessaria <= 0d)
        {
            destino.energiaNecessaria = origem.energiaNecessaria;
        }

        if (destino.diasProducao <= 0)
        {
            destino.diasProducao = origem.diasProducao;
        }

        if (string.IsNullOrEmpty(destino.linhaIndustrial))
        {
            destino.linhaIndustrial = origem.linhaIndustrial;
        }

        if (string.IsNullOrEmpty(destino.prefabId))
        {
            destino.prefabId = origem.prefabId;
        }

        destino.permiteProducaoAutomatica |= origem.permiteProducaoAutomatica;
        destino.permiteCompraAutomatica |= origem.permiteCompraAutomatica;
        destino.permiteVendaAutomatica |= origem.permiteVendaAutomatica;

        if (destino.estoqueMinimo <= 0d)
        {
            destino.estoqueMinimo = origem.estoqueMinimo;
        }

        if (destino.estoqueAlvo <= 0d)
        {
            destino.estoqueAlvo = origem.estoqueAlvo;
        }

        destino.prioridade = Mathf.Max(destino.prioridade, origem.prioridade);

        if (string.IsNullOrEmpty(destino.dicaDesbloqueio))
        {
            destino.dicaDesbloqueio = origem.dicaDesbloqueio;
        }

        if (string.IsNullOrEmpty(destino.dicaMaterialFaltante))
        {
            destino.dicaMaterialFaltante = origem.dicaMaterialFaltante;
        }

        if (string.IsNullOrEmpty(destino.dicaEstruturaFaltante))
        {
            destino.dicaEstruturaFaltante = origem.dicaEstruturaFaltante;
        }

        if (string.IsNullOrEmpty(destino.dicaPesquisaFaltante))
        {
            destino.dicaPesquisaFaltante = origem.dicaPesquisaFaltante;
        }
    }

    private static List<string> MesclarListas(List<string> destino, List<string> origem)
    {
        List<string> resultado = new List<string>();
        HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AdicionarTodos(resultado, vistos, destino);
        AdicionarTodos(resultado, vistos, origem);

        return resultado;
    }

    private static void AdicionarTodos(List<string> destino, HashSet<string> vistos, List<string> origem)
    {
        if (origem == null)
        {
            return;
        }

        for (int i = 0; i < origem.Count; i++)
        {
            string valor = IA_Text.Normalize(origem[i]);
            if (string.IsNullOrEmpty(valor) || !vistos.Add(valor))
            {
                continue;
            }

            destino.Add(valor);
        }
    }

    private static List<IngredienteProduto> MesclarMateriais(List<IngredienteProduto> destino, List<IngredienteProduto> origem)
    {
        Dictionary<string, IngredienteProduto> mapa = new Dictionary<string, IngredienteProduto>(StringComparer.OrdinalIgnoreCase);

        AdicionarMateriais(mapa, destino);
        AdicionarMateriais(mapa, origem);

        return mapa.Values.ToList();
    }

    private static void AdicionarMateriais(Dictionary<string, IngredienteProduto> mapa, List<IngredienteProduto> origem)
    {
        if (origem == null)
        {
            return;
        }

        for (int i = 0; i < origem.Count; i++)
        {
            IngredienteProduto ingrediente = origem[i];
            if (ingrediente == null)
            {
                continue;
            }

            string id = IA_Text.Normalize(ingrediente.recursoId);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            IngredienteProduto existente;
            if (!mapa.TryGetValue(id, out existente))
            {
                existente = new IngredienteProduto { recursoId = id, quantidade = 0d };
                mapa[id] = existente;
            }

            existente.quantidade += ingrediente.quantidade;
        }
    }

    private static CatalogoProdutoUnificadoItem Copiar(CatalogoProdutoUnificadoItem item)
    {
        if (item == null)
        {
            return null;
        }

        CatalogoProdutoUnificadoItem copia = new CatalogoProdutoUnificadoItem
        {
            id = item.id,
            nome = item.nome,
            descricao = item.descricao,
            categoria = item.categoria,
            unidade = item.unidade,
            materiais = item.materiais != null ? new List<IngredienteProduto>(item.materiais.Select(m => new IngredienteProduto { recursoId = m.recursoId, quantidade = m.quantidade })) : new List<IngredienteProduto>(),
            estruturasNecessarias = item.estruturasNecessarias != null ? new List<string>(item.estruturasNecessarias) : new List<string>(),
            pesquisasNecessarias = item.pesquisasNecessarias != null ? new List<string>(item.pesquisasNecessarias) : new List<string>(),
            dinheiroNecessario = item.dinheiroNecessario,
            energiaNecessaria = item.energiaNecessaria,
            diasProducao = item.diasProducao,
            linhaIndustrial = item.linhaIndustrial,
            prefabId = item.prefabId,
            combustiveisAceitos = item.combustiveisAceitos != null ? new List<string>(item.combustiveisAceitos) : new List<string>(),
            municoesAceitas = item.municoesAceitas != null ? new List<string>(item.municoesAceitas) : new List<string>(),
            permiteProducaoAutomatica = item.permiteProducaoAutomatica,
            permiteCompraAutomatica = item.permiteCompraAutomatica,
            permiteVendaAutomatica = item.permiteVendaAutomatica,
            estoqueMinimo = item.estoqueMinimo,
            estoqueAlvo = item.estoqueAlvo,
            prioridade = item.prioridade,
            dicaDesbloqueio = item.dicaDesbloqueio,
            dicaMaterialFaltante = item.dicaMaterialFaltante,
            dicaEstruturaFaltante = item.dicaEstruturaFaltante,
            dicaPesquisaFaltante = item.dicaPesquisaFaltante,
            aliases = item.aliases != null ? new List<string>(item.aliases) : new List<string>()
        };

        copia.Normalizar();
        return copia;
    }

    private static int Comparar(CatalogoProdutoUnificadoItem a, CatalogoProdutoUnificadoItem b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int porPrioridade = b.prioridade.CompareTo(a.prioridade);
        if (porPrioridade != 0)
        {
            return porPrioridade;
        }

        return string.Compare(a.nome, b.nome, StringComparison.OrdinalIgnoreCase);
    }

    private static CatalogoProdutoUnificadoItem ConverterConstrucao(DadosConstrucao construcao)
    {
        if (construcao == null)
        {
            return null;
        }

        GameObject prefab;
        bool temPrefab = construcao.TryGetPrefabBasico(out prefab);
        IA_ConstructionCapability capabilities = construcao.GetResolvedCapabilities();
        CatalogoProdutoUnificadoItem item = new CatalogoProdutoUnificadoItem
        {
            id = construcao.GetStableId(),
            nome = construcao.GetDisplayName(),
            descricao = construcao.descricao,
            categoria = ConverterCategoriaConstrucao(construcao, capabilities),
            unidade = "un",
            prefabId = temPrefab && prefab != null ? IA_Text.Normalize(prefab.name) : IA_Text.Normalize(construcao.name),
            permiteProducaoAutomatica = false,
            permiteCompraAutomatica = false,
            permiteVendaAutomatica = false,
            prioridade = 1,
            dicaDesbloqueio = construcao.descricao,
            dicaMaterialFaltante = string.Empty,
            dicaEstruturaFaltante = string.Empty,
            dicaPesquisaFaltante = string.Empty
        };

        item.aliases.Add(construcao.nomeItem);
        item.aliases.Add(construcao.name);
        if (temPrefab && prefab != null)
        {
            item.aliases.Add(prefab.name);
        }

        foreach (string alias in construcao.GetExplicitAliases())
        {
            item.aliases.Add(alias);
        }

        return item;
    }

    private static CategoriaProduto ConverterCategoriaConstrucao(DadosConstrucao construcao, IA_ConstructionCapability capabilities)
    {
        if (construcao == null)
        {
            return CategoriaProduto.Desconhecido;
        }

        if ((capabilities & IA_ConstructionCapability.Unit) != 0)
        {
            if ((capabilities & IA_ConstructionCapability.Air) != 0)
            {
                return CategoriaProduto.UnidadeAerea;
            }

            if ((capabilities & IA_ConstructionCapability.Naval) != 0)
            {
                return CategoriaProduto.UnidadeNaval;
            }

            return CategoriaProduto.UnidadeTerrestre;
        }

        if ((capabilities & IA_ConstructionCapability.Airport) != 0
            || (capabilities & IA_ConstructionCapability.Pier) != 0
            || (capabilities & IA_ConstructionCapability.Shipyard) != 0
            || (capabilities & IA_ConstructionCapability.Platform) != 0
            || (capabilities & IA_ConstructionCapability.Power) != 0
            || (capabilities & IA_ConstructionCapability.Warehouse) != 0)
        {
            return CategoriaProduto.Infraestrutura;
        }

        if ((capabilities & IA_ConstructionCapability.Radar) != 0
            || (capabilities & IA_ConstructionCapability.Defense) != 0)
        {
            return CategoriaProduto.Estrutura;
        }

        switch (construcao.categoria)
        {
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return CategoriaProduto.Estrutura;
            case DadosConstrucao.CategoriaItem.Marinha:
                return CategoriaProduto.Estrutura;
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return CategoriaProduto.Pesquisa;
            case DadosConstrucao.CategoriaItem.Energia:
                return CategoriaProduto.Infraestrutura;
            case DadosConstrucao.CategoriaItem.Urbana:
                return CategoriaProduto.Estrutura;
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return CategoriaProduto.Infraestrutura;
            case DadosConstrucao.CategoriaItem.Exercito:
            default:
                return CategoriaProduto.Estrutura;
        }
    }

    private static CatalogoProdutoUnificadoItem ConverterMercado(DadosItemMercado item)
    {
        if (item == null)
        {
            return null;
        }

        string id = IA_Text.Normalize(!string.IsNullOrWhiteSpace(item.id) ? item.id : item.recursoId);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        CatalogoProdutoUnificadoItem resultado = new CatalogoProdutoUnificadoItem
        {
            id = id,
            nome = string.IsNullOrWhiteSpace(item.nome) ? item.id : item.nome,
            descricao = "Item de mercado.",
            categoria = ConverterCategoriaMercado(item, id),
            unidade = "un",
            dinheiroNecessario = item.precoBase,
            energiaNecessaria = 0d,
            diasProducao = 0,
            linhaIndustrial = string.Empty,
            prefabId = string.Empty,
            permiteProducaoAutomatica = false,
            permiteCompraAutomatica = item.podeComprar,
            permiteVendaAutomatica = item.podeVender,
            estoqueMinimo = 0d,
            estoqueAlvo = Mathf.Max(0, item.estoqueGlobal),
            prioridade = item.recurso == RecursoMercado.Uranio || item.recurso == RecursoMercado.UranioEnriquecido ? 5 : 1,
            dicaDesbloqueio = "Mercado habilitado.",
            dicaMaterialFaltante = string.Empty,
            dicaEstruturaFaltante = string.Empty,
            dicaPesquisaFaltante = string.Empty
        };

        resultado.aliases.Add(item.id);
        resultado.aliases.Add(item.nome);
        if (!string.IsNullOrWhiteSpace(item.recursoId))
        {
            resultado.aliases.Add(item.recursoId);
        }

        return resultado;
    }

    private static CategoriaProduto ConverterCategoriaMercado(DadosItemMercado item, string id)
    {
        if (item == null)
        {
            return CategoriaProduto.Desconhecido;
        }

        switch (item.recurso)
        {
            case RecursoMercado.Comida:
                return CategoriaProduto.Agricultura;
            case RecursoMercado.Petroleo:
                return CategoriaProduto.Combustivel;
            case RecursoMercado.Energia:
                return CategoriaProduto.Servico;
            case RecursoMercado.Aco:
            case RecursoMercado.CobreEletrolitico:
            case RecursoMercado.Duraluminio:
            case RecursoMercado.LigaTitanio:
                return CategoriaProduto.MaterialRefinado;
            case RecursoMercado.Armamentos:
                return CategoriaProduto.Municao;
            case RecursoMercado.Uranio:
            case RecursoMercado.MinerioFerro:
            case RecursoMercado.MinerioCobre:
            case RecursoMercado.Bauxita:
            case RecursoMercado.MinerioTitanio:
                return CategoriaProduto.Mineral;
            case RecursoMercado.ComponentesEletronicos:
                return CategoriaProduto.Componente;
            case RecursoMercado.UranioEnriquecido:
                return CategoriaProduto.Estrategico;
            default:
                break;
        }

        if (!string.IsNullOrEmpty(id))
        {
            if (id.Contains("comida"))
            {
                return CategoriaProduto.Agricultura;
            }

            if (id.Contains("petroleo") || id.Contains("gas") || id.Contains("etanol") || id.Contains("biodiesel") || id.Contains("biogas") || id.Contains("combustivel") || id.Contains("lubrificante"))
            {
                return CategoriaProduto.Combustivel;
            }

            if (id.Contains("municao") || id.Contains("bomb") || id.Contains("missil"))
            {
                return CategoriaProduto.Municao;
            }

            if (id.Contains("componentes") || id.Contains("circuit") || id.Contains("sensor") || id.Contains("bateria") || id.Contains("motor") || id.Contains("turbina") || id.Contains("chassi") || id.Contains("blindagem") || id.Contains("avionic") || id.Contains("radar") || id.Contains("sonar") || id.Contains("modulo") || id.Contains("equipamento") || id.Contains("maquina") || id.Contains("guindaste") || id.Contains("cabos"))
            {
                return CategoriaProduto.Componente;
            }

            if (id.Contains("minerio") || id.Contains("litio") || id.Contains("terra") || id.Contains("niquel") || id.Contains("manganes") || id.Contains("silica") || id.Contains("calcario") || id.Contains("areia") || id.Contains("fosfato") || id.Contains("carvao") || id.Contains("petroleo") || id.Contains("gas"))
            {
                return CategoriaProduto.Mineral;
            }

            if (id.Contains("industrial") || id.Contains("aco") || id.Contains("aluminio") || id.Contains("cimento") || id.Contains("vidro") || id.Contains("borracha") || id.Contains("plastico") || id.Contains("fertilizante"))
            {
                return CategoriaProduto.MaterialRefinado;
            }
        }

        return CategoriaProduto.Desconhecido;
    }

    private static CatalogoProdutoUnificadoItem ConverterRecursoIndustrial(RecursoIndustrialSO recurso)
    {
        if (recurso == null)
        {
            return null;
        }

        string id = IA_Text.Normalize(recurso.id);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        CatalogoProdutoUnificadoItem item = new CatalogoProdutoUnificadoItem
        {
            id = id,
            nome = string.IsNullOrWhiteSpace(recurso.nome) ? recurso.id : recurso.nome,
            descricao = recurso.descricao,
            categoria = ConverterCategoriaRecurso(id, recurso.nome, recurso.categoria),
            unidade = string.IsNullOrWhiteSpace(recurso.unidade) ? "un" : recurso.unidade,
            dinheiroNecessario = recurso.precoBase,
            energiaNecessaria = 0d,
            diasProducao = 0,
            linhaIndustrial = string.Empty,
            prefabId = string.Empty,
            permiteProducaoAutomatica = false,
            permiteCompraAutomatica = recurso.podeComprar,
            permiteVendaAutomatica = recurso.podeVender,
            estoqueMinimo = 0d,
            estoqueAlvo = 0d,
            prioridade = recurso.estrategico ? 5 : 1,
            dicaDesbloqueio = recurso.descricao,
            dicaMaterialFaltante = string.Empty,
            dicaEstruturaFaltante = string.Empty,
            dicaPesquisaFaltante = string.Empty
        };

        item.aliases.Add(recurso.id);
        item.aliases.Add(recurso.nome);
        return item;
    }

    private static CategoriaProduto ConverterCategoriaRecurso(string id, string nome, CategoriaRecursoIndustrial categoria)
    {
        string chave = IA_Text.Normalize(id + " " + nome);
        if (chave.Contains("etanol") || chave.Contains("biodiesel") || chave.Contains("biogas") ||
            chave.Contains("gasolina") || chave.Contains("diesel") || chave.Contains("combustivel") ||
            chave.Contains("lubrificante"))
        {
            return CategoriaProduto.Combustivel;
        }

        switch (categoria)
        {
            case CategoriaRecursoIndustrial.MateriaPrima:
                return CategoriaProduto.Mineral;
            case CategoriaRecursoIndustrial.Refinado:
                return CategoriaProduto.MaterialRefinado;
            case CategoriaRecursoIndustrial.Estrategico:
                return CategoriaProduto.Estrategico;
            case CategoriaRecursoIndustrial.Componente:
                return CategoriaProduto.Componente;
            case CategoriaRecursoIndustrial.MilitarFuturo:
                return CategoriaProduto.Estrategico;
            default:
                return CategoriaProduto.Desconhecido;
        }
    }

    private static CatalogoProdutoUnificadoItem ConverterReceitaIndustrial(ReceitaIndustrialSO receita)
    {
        if (receita == null)
        {
            return null;
        }

        string id = IA_Text.Normalize(receita.id);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        CatalogoProdutoUnificadoItem item = new CatalogoProdutoUnificadoItem
        {
            id = id,
            nome = string.IsNullOrWhiteSpace(receita.nome) ? receita.id : receita.nome,
            descricao = "Receita industrial.",
            categoria = ConverterCategoriaReceita(receita),
            unidade = string.IsNullOrWhiteSpace(receita.unidadeResultado) ? "un" : receita.unidadeResultado,
            materiais = ConverterMateriaisReceita(receita.materiaisNecessarios),
            estruturasNecessarias = new List<string>(),
            pesquisasNecessarias = string.IsNullOrWhiteSpace(receita.pesquisaExigida)
                ? new List<string>()
                : new List<string>(receita.pesquisaExigida.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(IA_Text.Normalize)),
            dinheiroNecessario = receita.dinheiroNecessario,
            energiaNecessaria = receita.energiaNecessaria,
            diasProducao = receita.diasNecessarios,
            linhaIndustrial = IA_Text.Normalize(receita.produtoFinalId),
            prefabId = IA_Text.Normalize(receita.produtoFinalId),
            permiteProducaoAutomatica = true,
            permiteCompraAutomatica = false,
            permiteVendaAutomatica = false,
            estoqueMinimo = 0d,
            estoqueAlvo = receita.quantidadeProduzida,
            prioridade = receita.nivelIndustrialExigido,
            dicaDesbloqueio = receita.pesquisaExigida,
            dicaMaterialFaltante = string.Empty,
            dicaEstruturaFaltante = string.Empty,
            dicaPesquisaFaltante = receita.pesquisaExigida
        };

        item.aliases.Add(receita.id);
        item.aliases.Add(receita.nome);
        item.aliases.Add(receita.produtoFinalId);
        return item;
    }

    private static CategoriaProduto ConverterCategoriaReceita(ReceitaIndustrialSO receita)
    {
        if (receita == null)
        {
            return CategoriaProduto.Desconhecido;
        }

        string produto = IA_Text.Normalize(receita.produtoFinalId);
        if (string.IsNullOrEmpty(produto))
        {
            produto = IA_Text.Normalize(receita.id);
        }

        if (produto.Contains("municao"))
        {
            return CategoriaProduto.Municao;
        }

        if (produto.Contains("missil"))
        {
            return CategoriaProduto.Missil;
        }

        if (produto.Contains("bomba"))
        {
            return CategoriaProduto.Bomba;
        }

        if (produto.Contains("componentes"))
        {
            return CategoriaProduto.Componente;
        }

        if (produto.Contains("cabos") || produto.Contains("circuit") || produto.Contains("sensor") || produto.Contains("bateria") || produto.Contains("motor") || produto.Contains("turbina") || produto.Contains("pneu") || produto.Contains("esteira") || produto.Contains("blindagem") || produto.Contains("avionic") || produto.Contains("radar") || produto.Contains("sonar") || produto.Contains("modulo") || produto.Contains("equipamento") || produto.Contains("maquina") || produto.Contains("guindaste"))
        {
            return CategoriaProduto.Componente;
        }

        if (produto.Contains("combustivel") || produto.Contains("etanol") || produto.Contains("biodiesel") || produto.Contains("biogas") || produto.Contains("gasolina") || produto.Contains("diesel") || produto.Contains("lubrificante"))
        {
            return CategoriaProduto.Combustivel;
        }

        if (produto.Contains("estrutura") || produto.Contains("cimento") || produto.Contains("vidro") || produto.Contains("plastico") || produto.Contains("borracha") || produto.Contains("aco") || produto.Contains("duraluminio") || produto.Contains("liga") || produto.Contains("aluminio") || produto.Contains("fertilizante"))
        {
            return CategoriaProduto.MaterialRefinado;
        }

        if (receita.materialEstrategico || receita.requerLaboratorioNuclear)
        {
            return CategoriaProduto.Estrategico;
        }

        return CategoriaProduto.MaterialRefinado;
    }

    private static List<IngredienteProduto> ConverterMateriaisReceita(List<MaterialNecessarioIndustrial> materiais)
    {
        List<IngredienteProduto> resultado = new List<IngredienteProduto>();
        if (materiais == null)
        {
            return resultado;
        }

        for (int i = 0; i < materiais.Count; i++)
        {
            MaterialNecessarioIndustrial materia = materiais[i];
            if (materia == null)
            {
                continue;
            }

            resultado.Add(new IngredienteProduto
            {
                recursoId = IA_Text.Normalize(materia.recursoId),
                quantidade = materia.quantidade
            });
        }

        return resultado;
    }

}
