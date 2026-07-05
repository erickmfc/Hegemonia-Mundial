using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SistemaMercadoGlobal : MonoBehaviour
{
    public static SistemaMercadoGlobal Instancia { get; private set; }

    [Header("Tick")]
    public float intervaloMercado = 3f;
    public bool usarDiretoresBrainMaster = true;

    [Header("Itens")]
    public List<DadosItemMercado> itens = new List<DadosItemMercado>();
    public List<TransacaoMercado> historico = new List<TransacaoMercado>();

    public event Action OnMercadoAtualizado;
    public event Action<TransacaoMercado> OnTransacaoExecutada;

    private float proximoTick;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        InicializarItensPadrao();
    }

    private void Start()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada += AoMudarDeDia;
        }
    }

    private void OnDestroy()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada -= AoMudarDeDia;
        }
    }

    private void AoMudarDeDia()
    {
        SincronizarCatalogoConstrucao();
        SimularMercado();
    }


    public void InicializarItensPadrao()
    {
        if (itens == null) itens = new List<DadosItemMercado>();
        if (itens.Count > 0) return;

        itens.Add(new DadosItemMercado { id = "comida", nome = "Comida", recurso = RecursoMercado.Comida, precoBase = 120, precoAtual = 120, estoqueGlobal = 24850, oferta = 76f, demanda = 58f, volatilidade = 0.07f });
        itens.Add(new DadosItemMercado { id = "petroleo", nome = "Petroleo", recurso = RecursoMercado.Petroleo, precoBase = 185, precoAtual = 185, estoqueGlobal = 18340, oferta = 52f, demanda = 72f, volatilidade = 0.12f });
        itens.Add(new DadosItemMercado { id = "aco", nome = "Aco", recurso = RecursoMercado.Aco, precoBase = 95, precoAtual = 95, estoqueGlobal = 31760, oferta = 81f, demanda = 55f, volatilidade = 0.06f });
        itens.Add(new DadosItemMercado { id = "energia", nome = "Energia", recurso = RecursoMercado.Energia, precoBase = 65, precoAtual = 65, estoqueGlobal = 45000, oferta = 65f, demanda = 50f, volatilidade = 0.05f });
        itens.Add(new DadosItemMercado { id = "armamentos", nome = "Armamentos", recurso = RecursoMercado.Armamentos, precoBase = 420, precoAtual = 420, estoqueGlobal = 7420, oferta = 43f, demanda = 64f, volatilidade = 0.14f });
        itens.Add(new DadosItemMercado { id = "uranio", nome = "Uranio", recurso = RecursoMercado.Uranio, precoBase = 900, precoAtual = 900, estoqueGlobal = 1250, oferta = 30f, demanda = 50f, volatilidade = 0.16f });
    }

    public void RegistrarItem(DadosItemMercado item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return;
        DadosItemMercado existente = ObterItem(item.id);
        if (existente != null) return;
        itens.Add(item);
        OnMercadoAtualizado?.Invoke();
    }

    public void SincronizarCatalogoConstrucao()
    {
        if (MenuConstrucao.catalogoGlobal == null) return;

        foreach (DadosConstrucao ficha in MenuConstrucao.catalogoGlobal)
        {
            if (ficha == null || string.IsNullOrEmpty(ficha.nomeItem)) continue;
            string id = "construcao_" + ficha.nomeItem.ToLowerInvariant().Replace(" ", "_");
            if (ObterItem(id) != null) continue;

            itens.Add(new DadosItemMercado
            {
                id = id,
                nome = ficha.nomeItem,
                categoria = ficha.categoria.ToString(),
                recurso = RecursoMercado.Nenhum,
                precoBase = Mathf.Max(1, ficha.preco),
                precoAtual = Mathf.Max(1, ficha.preco),
                estoqueGlobal = 10,
                oferta = 45f,
                demanda = 35f,
                volatilidade = 0.05f,
                podeComprar = false,
                podeVender = false
            });
        }
    }

    public DadosItemMercado ObterItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return itens.FirstOrDefault(i => i != null && string.Equals(i.id, id, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<DadosItemMercado> ItensOrdenados()
    {
        return itens.Where(i => i != null).OrderBy(i => i.categoria).ThenBy(i => i.nome);
    }

    public void SimularMercado()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        float pressaoGuerra = governo != null ? governo.PressaoGlobalGuerra() : 0f;
        float pressaoSancoes = governo != null ? governo.PressaoGlobalSancoes() : 0f;
        float deficitComida = 0f;
        float deficitEnergia = 0f;
        float deficitPetroleo = 0f;
        float ofertaComida = 0f;
        float ofertaPetroleo = 0f;
        float ofertaIndustria = 0f;
        SistemaEconomiaImoveis economiaImoveis = SistemaEconomiaImoveis.Instancia;
        if (economiaImoveis != null)
        {
            foreach (DadosEconomiaPais economia in economiaImoveis.Economias.Values)
            {
                if (economia == null) continue;
                deficitComida += economia.deficitComida;
                deficitEnergia += economia.deficitEnergia;
                deficitPetroleo += economia.deficitPetroleo;
                ofertaComida += economia.comidaProduzida;
                ofertaPetroleo += economia.petroleoProduzido;
                ofertaIndustria += economia.industriaProduzida;
            }
        }
        float noticiaEco = SistemaNoticiasEconomicas.Instancia != null ? SistemaNoticiasEconomicas.Instancia.ModificadorEconomico : 0f;

        foreach (DadosItemMercado item in itens)
        {
            if (item == null) continue;

            float demanda = item.demanda;
            float oferta = item.oferta;

            if (item.recurso == RecursoMercado.Petroleo)
            {
                demanda += pressaoGuerra * 18f;
                oferta -= pressaoSancoes * 10f;
                demanda += deficitPetroleo * 3f;
                oferta += ofertaPetroleo * 0.35f;
            }
            else if (item.recurso == RecursoMercado.Armamentos || item.recurso == RecursoMercado.Uranio)
            {
                demanda += pressaoGuerra * 28f;
                oferta -= pressaoSancoes * 8f;
                oferta += ofertaIndustria * 0.20f;
                if (deficitEnergia > 0f) oferta -= deficitEnergia * 2f;
            }
            else if (item.recurso == RecursoMercado.Comida)
            {
                demanda += pressaoSancoes * 12f;
                demanda += deficitComida * 4f;
                oferta += ofertaComida * 0.30f;
            }
            else if (item.recurso == RecursoMercado.Aco)
            {
                oferta += ofertaIndustria * 0.25f;
                demanda += deficitEnergia * 1.5f;
            }
            else if (item.recurso == RecursoMercado.Energia)
            {
                demanda += deficitEnergia * 4f;
                oferta += ofertaIndustria * 0.12f;
                oferta += ofertaComida * 0.06f;
            }

            demanda += Mathf.Max(0f, -noticiaEco) * 20f;
            oferta += Mathf.Max(0f, noticiaEco) * 18f;

            if (item.estoqueGlobal < 1000) demanda += 12f;
            if (item.estoqueGlobal > 25000) oferta += 8f;

            float pressao = (demanda - oferta) / 100f;
            float oscilacao = Mathf.Sin(Time.unscaledTime * (0.41f + item.volatilidade) + item.precoBase) * item.volatilidade;
            float novoPreco = item.precoBase * (1f + pressao + oscilacao);
            int precoAnterior = Mathf.Max(1, item.precoAtual);
            item.precoAtual = Mathf.Max(1, Mathf.RoundToInt(novoPreco));
            item.variacaoPercentual = ((item.precoAtual - precoAnterior) / (float)precoAnterior) * 100f;
            item.demanda = Mathf.Clamp(demanda * 0.92f + item.demanda * 0.08f, 5f, 140f);
            item.oferta = Mathf.Clamp(oferta * 0.92f + item.oferta * 0.08f, 5f, 140f);
        }

        if (!usarDiretoresBrainMaster)
        {
            ProcessarComprasDaIA();
        }
        ProcessarAutoVenda();
        OnMercadoAtualizado?.Invoke();
    }

    public bool Comprar(int compradorTeamId, int vendedorTeamId, string itemId, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        DadosItemMercado item = ObterItem(itemId);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (item == null || governo == null || quantidade <= 0)
        {
            mensagem = "Ordem invalida.";
            return false;
        }

        quantidade = Mathf.Min(quantidade, Mathf.Max(0, item.estoqueGlobal));
        int total = quantidade * item.precoAtual;
        DadosPaisGoverno comprador = governo.ObterPais(compradorTeamId);
        DadosPaisGoverno vendedor = governo.ObterPais(vendedorTeamId);
        if (comprador == null || vendedor == null || quantidade <= 0)
        {
            mensagem = "Pais sem oferta disponivel.";
            return false;
        }

        if (!governo.TentarPagar(compradorTeamId, total))
        {
            mensagem = "Dinheiro insuficiente.";
            return false;
        }

        governo.AdicionarSaldo(vendedorTeamId, total);
        governo.AdicionarEstoque(compradorTeamId, item.recurso, quantidade);
        governo.RemoverEstoque(vendedorTeamId, item.recurso, quantidade);

        item.estoqueGlobal = Mathf.Max(0, item.estoqueGlobal - quantidade);
        item.demanda = Mathf.Clamp(item.demanda + quantidade / 120f, 0f, 160f);

        var transacao = new TransacaoMercado
        {
            compradorTeamId = compradorTeamId,
            vendedorTeamId = vendedorTeamId,
            itemId = item.id,
            quantidade = quantidade,
            precoUnitario = item.precoAtual,
            total = total,
            compraDoJogador = compradorTeamId == governo.teamJogador,
            mensagem = comprador.nomePais + " comprou " + quantidade + " de " + item.nome + " de " + vendedor.nomePais
        };

        RegistrarTransacao(transacao);
        mensagem = transacao.mensagem;
        return true;
    }

    public bool Vender(int vendedorTeamId, int compradorTeamId, string itemId, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        DadosItemMercado item = ObterItem(itemId);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (item == null || governo == null || quantidade <= 0)
        {
            mensagem = "Ordem invalida.";
            return false;
        }

        int disponivel = governo.ObterEstoque(vendedorTeamId, item.recurso);
        quantidade = Mathf.Min(quantidade, disponivel);
        if (quantidade <= 0)
        {
            mensagem = "Sem estoque para vender.";
            return false;
        }

        int total = quantidade * item.precoAtual;
        if (!governo.TentarPagar(compradorTeamId, total))
        {
            mensagem = "Comprador sem dinheiro.";
            return false;
        }

        governo.AdicionarSaldo(vendedorTeamId, total);
        governo.RemoverEstoque(vendedorTeamId, item.recurso, quantidade);
        governo.AdicionarEstoque(compradorTeamId, item.recurso, quantidade);

        item.estoqueGlobal += quantidade;
        item.oferta = Mathf.Clamp(item.oferta + quantidade / 100f, 0f, 160f);

        var transacao = new TransacaoMercado
        {
            compradorTeamId = compradorTeamId,
            vendedorTeamId = vendedorTeamId,
            itemId = item.id,
            quantidade = quantidade,
            precoUnitario = item.precoAtual,
            total = total,
            compraDoJogador = compradorTeamId == governo.teamJogador,
            mensagem = vendedorTeamId == governo.teamJogador
                ? "Voce vendeu " + quantidade + " de " + item.nome
                : "Venda internacional de " + item.nome
        };

        RegistrarTransacao(transacao);
        mensagem = transacao.mensagem;
        return true;
    }

    /// <summary>
    /// Vende recursos REAIS do GerenciadorRecursos (petróleo, aço, energia) por dinheiro real.
    /// Este método usa os recursos reais do jogador, não o estoque simulado do governo.
    /// </summary>
    public bool VenderRecursoReal(string itemId, int quantidade, out string mensagem, out int dinheiroRecebido)
    {
        mensagem = string.Empty;
        dinheiroRecebido = 0;

        DadosItemMercado item = ObterItem(itemId);
        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;

        if (item == null || gr == null || quantidade <= 0)
        {
            mensagem = "Ordem inválida.";
            return false;
        }

        // Verifica e retira o recurso real
        bool temRecurso = false;
        switch (item.recurso)
        {
            case RecursoMercado.Petroleo:
                if (gr.petroleo >= quantidade) { gr.RemoverRecurso("Petroleo", quantidade); temRecurso = true; }
                break;
            case RecursoMercado.Aco:
                if (gr.aco >= quantidade) { gr.RemoverRecurso("Aco", quantidade); temRecurso = true; }
                break;
            case RecursoMercado.Comida:
                // Comida: reservada para expansão futura
                temRecurso = false;
                break;
            default:
                // Energia e outros: trata energia como recurso vendável
                if (item.id == "energia" && gr.energia >= quantidade)
                {
                    gr.RemoverRecurso("Energia", quantidade);
                    temRecurso = true;
                }
                break;
        }

        if (!temRecurso && item.recurso == RecursoMercado.Comida && gr.comida >= quantidade)
        {
            gr.RemoverRecurso("Comida", quantidade);
            temRecurso = true;
        }

        if (!temRecurso)
        {
            mensagem = "Recursos insuficientes para vender " + quantidade + " de " + item.nome + ".";
            return false;
        }

        // Calcula e credita o dinheiro real
        dinheiroRecebido = quantidade * item.precoAtual;
        gr.AdicionarRecurso("Dinheiro", dinheiroRecebido);

        // Atualiza o mercado (aumenta oferta, simula venda)
        item.estoqueGlobal += quantidade;
        item.oferta = Mathf.Clamp(item.oferta + quantidade / 100f, 0f, 160f);

        mensagem = "Vendeu " + quantidade + " de " + item.nome + " por $" + dinheiroRecebido;
        OnMercadoAtualizado?.Invoke();
        Debug.Log("[Mercado] " + mensagem);
        return true;
    }

    // ── AUTO-VENDA ────────────────────────────────────────────────────────────────
    // Configurações de auto-venda por tick de mercado

    [Header("Auto-Venda (por tick de mercado)")]
    public bool autoVenderPetroleo = false;
    public int autoVendaQuantidadePetroleo = 50;
    public bool autoVenderAco = false;
    public int autoVendaQuantidadeAco = 50;
    public bool autoVenderEnergia = false;
    public int autoVendaQuantidadeEnergia = 50;
    public bool autoVenderComida = false;
    public int autoVendaQuantidadeComida = 50;

    /// <summary>
    /// Executa auto-venda configurada pelo jogador. Chamado a cada tick de mercado.
    /// </summary>
    public void ProcessarAutoVenda()
    {
        if (autoVenderPetroleo && autoVendaQuantidadePetroleo > 0)
        {
            string msg; int ganho;
            VenderRecursoReal("petroleo", autoVendaQuantidadePetroleo, out msg, out ganho);
            if (ganho > 0) Debug.Log("[AutoVenda] " + msg);
        }

        if (autoVenderAco && autoVendaQuantidadeAco > 0)
        {
            string msg; int ganho;
            VenderRecursoReal("aco", autoVendaQuantidadeAco, out msg, out ganho);
            if (ganho > 0) Debug.Log("[AutoVenda] " + msg);
        }

        if (autoVenderEnergia && autoVendaQuantidadeEnergia > 0)
        {
            string msg; int ganho;
            VenderRecursoReal("energia", autoVendaQuantidadeEnergia, out msg, out ganho);
            if (ganho > 0) Debug.Log("[AutoVenda] " + msg);
        }

        if (autoVenderComida && autoVendaQuantidadeComida > 0)
        {
            string msg; int ganho;
            VenderRecursoReal("comida", autoVendaQuantidadeComida, out msg, out ganho);
            if (ganho > 0) Debug.Log("[AutoVenda] " + msg);
        }
    }

    public DadosItemMercado MelhorCompra()
    {
        return itens.Where(i => i != null && i.podeComprar).OrderBy(i => i.variacaoPercentual).FirstOrDefault();
    }

    public DadosItemMercado MaiorRisco()
    {
        return itens.Where(i => i != null).OrderByDescending(i => Mathf.Abs(i.variacaoPercentual) + i.volatilidade * 10f).FirstOrDefault();
    }

    private void RegistrarTransacao(TransacaoMercado transacao)
    {
        historico.Insert(0, transacao);
        while (historico.Count > 24) historico.RemoveAt(historico.Count - 1);
        OnTransacaoExecutada?.Invoke(transacao);
        OnMercadoAtualizado?.Invoke();
    }

    private void ProcessarComprasDaIA()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return;

        foreach (DadosPaisGoverno pais in governo.Paises)
        {
            if (pais == null || pais.teamId == governo.teamJogador || pais.saldo < 500) continue;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(pais.teamId) : null;
            DadosItemMercado necessidade = null;
            if (pais.energia < 180 || (economia != null && economia.deficitEnergia > 0.5f)) necessidade = ObterItem("energia");
            else if (pais.comida < 260 || (economia != null && economia.deficitComida > 0.5f)) necessidade = ObterItem("comida");
            else if (pais.petroleo < 220) necessidade = ObterItem("petroleo");
            else if (pais.emGuerra && pais.armamentos < 260) necessidade = ObterItem("armamentos");
            if (necessidade == null) continue;

            DadosPaisGoverno vendedor = governo.Paises.FirstOrDefault(p => p != null && p.teamId != pais.teamId && governo.ObterEstoque(p.teamId, necessidade.recurso) > 120);
            if (vendedor == null) continue;

            int quantidade = Mathf.Min(necessidade.CalcularQuantidadePadrao(), Mathf.Max(10, pais.saldo / Mathf.Max(1, necessidade.precoAtual) / 2));
            Comprar(pais.teamId, vendedor.teamId, necessidade.id, quantidade, out _);
        }
    }
}
