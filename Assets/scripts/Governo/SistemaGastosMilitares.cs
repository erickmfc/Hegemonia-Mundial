using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TipoGastoMilitar
{
    CompraUnidade,
    CompraMunicao,
    Disparo,
    FabricacaoMunicao,
    PesquisaMilitar,
    TecnologiaMilitar,
    Mobilizacao,
    Manutencao
}

[Serializable]
public class DefinicaoMunicaoMilitar
{
    public string id = string.Empty;
    public string nome = string.Empty;
    public string categoria = "Municao";
    public string descricao = string.Empty;
    public string pesquisaId = string.Empty;
    public string prefabNome = string.Empty;
    public int valorUnitario = 200;
    public int capacidadeCartucho = 10;
    public float tempoReabastecimento = 8f;
    public bool ativo = true;
    public int totalComprado;
    public int totalDisparado;
    public int totalFabricado;
    public int estoqueArmazenado;
}

[Serializable]
public class EstoqueMunicaoMilitar
{
    public int teamId;
    public string municaoId = string.Empty;
    public int quantidade;
}

[Serializable]
public class RegistroGastoMilitar
{
    public int teamId;
    public TipoGastoMilitar tipo;
    public string itemId = string.Empty;
    public string itemNome = string.Empty;
    public string categoria = string.Empty;
    public string unidade = string.Empty;
    public int quantidade;
    public int valorUnitario;
    public int valorTotal;
    public string origem = string.Empty;
    public string data = string.Empty;
    public float tempo;
}

/// <summary>
/// Registro financeiro militar centralizado. Guarda compras, fabricacao,
/// pesquisas e cada disparo de municao usado durante a partida.
/// </summary>
public sealed class SistemaGastosMilitares : MonoBehaviour
{
    public static SistemaGastosMilitares Instancia { get; private set; }

    public List<DefinicaoMunicaoMilitar> catalogoMunicoes = new List<DefinicaoMunicaoMilitar>();
    public List<EstoqueMunicaoMilitar> estoques = new List<EstoqueMunicaoMilitar>();
    public List<RegistroGastoMilitar> registros = new List<RegistroGastoMilitar>();

    public event Action OnAtualizado;

    private SistemaMercadoGlobal mercadoConectado;
    private int historicoMercadoProcessado;

    public static void GarantirInstancia()
    {
        if (Instancia != null)
        {
            Instancia.GarantirCatalogoInicial();
            return;
        }

        SistemaGastosMilitares existente = FindFirstObjectByType<SistemaGastosMilitares>();
        if (existente != null)
        {
            Instancia = existente;
            Instancia.GarantirCatalogoInicial();
            return;
        }

        GameObject go = new GameObject("SistemaGastosMilitares_Runtime");
        Instancia = go.AddComponent<SistemaGastosMilitares>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        GarantirCatalogoInicial();
    }

    private void Update()
    {
        ConectarMercado();
    }

    private void OnDestroy()
    {
        if (mercadoConectado != null)
            mercadoConectado.OnTransacaoExecutada -= AoExecutarTransacaoMercado;
        if (Instancia == this) Instancia = null;
    }

    private void ConectarMercado()
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == mercadoConectado) return;

        if (mercadoConectado != null)
            mercadoConectado.OnTransacaoExecutada -= AoExecutarTransacaoMercado;

        mercadoConectado = mercado;
        if (mercadoConectado != null)
            mercadoConectado.OnTransacaoExecutada += AoExecutarTransacaoMercado;
    }

    public void GarantirCatalogoInicial()
    {
        if (catalogoMunicoes == null) catalogoMunicoes = new List<DefinicaoMunicaoMilitar>();
        if (estoques == null) estoques = new List<EstoqueMunicaoMilitar>();
        if (registros == null) registros = new List<RegistroGastoMilitar>();

        DefinicaoMunicaoMilitar ares = catalogoMunicoes.FirstOrDefault(x => x != null && string.Equals(x.id, "municao_ares_ar", StringComparison.OrdinalIgnoreCase));
        if (ares == null)
        {
            ares = new DefinicaoMunicaoMilitar
            {
                id = "municao_ares_ar",
                nome = "Cartucho Ares Ar",
                categoria = "Defesa antiaerea",
                descricao = "Cartucho comprado diretamente pela unidade a cada disparo contra aeronaves.",
                pesquisaId = "pesquisa_ares_ar",
                prefabNome = "Ares_Ar",
                valorUnitario = 220,
                capacidadeCartucho = 10,
                tempoReabastecimento = 8f,
                ativo = true
            };
            catalogoMunicoes.Add(ares);
        }
    }

    public DefinicaoMunicaoMilitar ObterMunicao(string id)
    {
        GarantirCatalogoInicial();
        return catalogoMunicoes.FirstOrDefault(x => x != null &&
            (string.Equals(x.id, id, StringComparison.OrdinalIgnoreCase)
             || string.Equals(x.nome, id, StringComparison.OrdinalIgnoreCase)
             || string.Equals(x.prefabNome, id, StringComparison.OrdinalIgnoreCase)));
    }

    public IReadOnlyList<DefinicaoMunicaoMilitar> ObterMunicoesAtivas()
    {
        GarantirCatalogoInicial();
        return catalogoMunicoes.Where(x => x != null && x.ativo).ToList();
    }

    public bool TentarPagarDisparo(int teamId, string municaoId, string unidadeNome, out string mensagem)
    {
        mensagem = string.Empty;
        DefinicaoMunicaoMilitar municao = ObterMunicao(municaoId);
        if (municao == null || !municao.ativo)
        {
            mensagem = "Municao nao cadastrada.";
            return false;
        }

        int time = Mathf.Max(1, teamId);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        {
            mensagem = "Governo indisponivel para cobrar a municao.";
            return false;
        }

        if (!governo.TentarPagar(time, Mathf.Max(1, municao.valorUnitario)))
        {
            mensagem = "Sem saldo para comprar o cartucho de " + municao.nome + ".";
            return false;
        }

        municao.totalComprado++;
        municao.totalDisparado++;
        Registrar(time, TipoGastoMilitar.Disparo, municao.id, municao.nome, municao.categoria,
            "cartucho", 1, municao.valorUnitario, "Disparo de " + (string.IsNullOrWhiteSpace(unidadeNome) ? "unidade" : unidadeNome));
        mensagem = "Cartucho comprado e disparado: $" + municao.valorUnitario + ".";
        return true;
    }

    public bool ProduzirMunicao(int teamId, string municaoId, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        DefinicaoMunicaoMilitar municao = ObterMunicao(municaoId);
        if (municao == null || quantidade <= 0)
        {
            mensagem = "Lote de municao invalido.";
            return false;
        }

        int custoUnitarioFabricacao = Mathf.Max(1, Mathf.RoundToInt(municao.valorUnitario * 0.60f));
        int custoTotal = custoUnitarioFabricacao * quantidade;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null || !governo.TentarPagar(Mathf.Max(1, teamId), custoTotal))
        {
            mensagem = "Saldo insuficiente para fabricar este lote.";
            return false;
        }

        municao.totalFabricado += quantidade;
        EstoqueMunicaoMilitar estoque = GarantirEstoqueInterno(Mathf.Max(1, teamId), municao.id);
        estoque.quantidade += quantidade;
        Registrar(Mathf.Max(1, teamId), TipoGastoMilitar.FabricacaoMunicao, municao.id, municao.nome,
            municao.categoria, "cartucho", quantidade, custoUnitarioFabricacao, "Fabricacao nacional");
        mensagem = "Fabricado lote de " + quantidade + " cartuchos de " + municao.nome + ".";
        return true;
    }

    public int ObterEstoqueMunicao(int teamId, string municaoId)
    {
        EstoqueMunicaoMilitar estoque = estoques.FirstOrDefault(x => x != null && x.teamId == teamId && string.Equals(x.municaoId, municaoId, StringComparison.OrdinalIgnoreCase));
        return estoque != null ? Mathf.Max(0, estoque.quantidade) : 0;
    }

    public void AdicionarEstoqueMunicao(int teamId, string municaoId, int quantidade)
    {
        if (quantidade <= 0) return;
        GarantirEstoqueInterno(Mathf.Max(1, teamId), municaoId).quantidade += quantidade;
        OnAtualizado?.Invoke();
    }

    public bool RemoverEstoqueMunicao(int teamId, string municaoId, int quantidade)
    {
        if (quantidade <= 0) return false;
        EstoqueMunicaoMilitar estoque = GarantirEstoqueInterno(Mathf.Max(1, teamId), municaoId);
        if (estoque.quantidade < quantidade) return false;
        estoque.quantidade -= quantidade;
        OnAtualizado?.Invoke();
        return true;
    }

    public void GarantirEstoqueInicial(int teamId, string municaoId, int quantidade)
    {
        EstoqueMunicaoMilitar estoque = GarantirEstoqueInterno(Mathf.Max(1, teamId), municaoId);
        if (estoque.quantidade < quantidade) estoque.quantidade = quantidade;
    }

    public void RegistrarPesquisa(int teamId, string id, string nome, int valor, string categoria)
    {
        if (!EhMilitar(nome + " " + categoria + " " + id)) return;
        Registrar(Mathf.Max(1, teamId), TipoGastoMilitar.PesquisaMilitar, id, nome, categoria, "pesquisa", 1, valor, "Pesquisa nacional");
    }

    public IEnumerable<RegistroGastoMilitar> ObterRegistrosDoTime(int teamId)
    {
        return registros.Where(x => x != null && x.teamId == teamId).OrderByDescending(x => x.tempo);
    }

    public int TotalGasto(int teamId)
    {
        return registros.Where(x => x != null && x.teamId == teamId).Sum(x => Mathf.Max(0, x.valorTotal));
    }

    public static bool EhAresAr(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        string normalizado = texto.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalizado.Contains("ares_ar") || normalizado.Contains("aresar");
    }

    public static bool EhMilitar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        string normalizado = texto.ToLowerInvariant();
        return normalizado.Contains("militar") || normalizado.Contains("defesa") || normalizado.Contains("missil")
            || normalizado.Contains("munic") || normalizado.Contains("balistic") || normalizado.Contains("aerea")
            || normalizado.Contains("aérea") || normalizado.Contains("intercept");
    }

    private EstoqueMunicaoMilitar GarantirEstoqueInterno(int teamId, string municaoId)
    {
        EstoqueMunicaoMilitar estoque = estoques.FirstOrDefault(x => x != null && x.teamId == teamId && string.Equals(x.municaoId, municaoId, StringComparison.OrdinalIgnoreCase));
        if (estoque == null)
        {
            estoque = new EstoqueMunicaoMilitar { teamId = teamId, municaoId = municaoId, quantidade = 0 };
            estoques.Add(estoque);
        }
        return estoque;
    }

    private void Registrar(int teamId, TipoGastoMilitar tipo, string itemId, string itemNome, string categoria,
        string unidade, int quantidade, int valorUnitario, string origem)
    {
        RegistroGastoMilitar registro = new RegistroGastoMilitar
        {
            teamId = teamId,
            tipo = tipo,
            itemId = itemId ?? string.Empty,
            itemNome = itemNome ?? itemId ?? "Item militar",
            categoria = categoria ?? "Militar",
            unidade = unidade ?? "unidade",
            quantidade = Mathf.Max(1, quantidade),
            valorUnitario = Mathf.Max(0, valorUnitario),
            valorTotal = Mathf.Max(0, quantidade) * Mathf.Max(0, valorUnitario),
            origem = origem ?? string.Empty,
            data = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            tempo = Time.unscaledTime
        };
        registros.Insert(0, registro);
        while (registros.Count > 300) registros.RemoveAt(registros.Count - 1);
        OnAtualizado?.Invoke();
    }

    private void AoExecutarTransacaoMercado(TransacaoMercado transacao)
    {
        if (transacao == null || transacao.compradorTeamId <= 0) return;
        DadosItemMercado item = mercadoConectado != null ? mercadoConectado.ObterItem(transacao.itemId) : null;
        if (item == null) return;
        TipoGastoMilitar tipo = item.municaoMilitar ? TipoGastoMilitar.CompraMunicao :
            item.equipamentoMilitar ? TipoGastoMilitar.CompraUnidade : (EhMilitar(item.nome + " " + item.categoria) ? TipoGastoMilitar.CompraUnidade : TipoGastoMilitar.Manutencao);
        Registrar(transacao.compradorTeamId, tipo, item.id, item.NomeFormatado, item.categoria,
            item.municaoMilitar ? "cartucho" : "unidade", transacao.quantidade, transacao.precoUnitario, "Mercado internacional");
    }
}
