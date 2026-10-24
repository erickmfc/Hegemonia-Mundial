using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class PedidoMercadoLogistico
{
    public string id;
    public int compradorTeamId;
    public int vendedorTeamId;
    public string itemId;
    public int quantidade;
    public int precoUnitario;
    public int total;
    public int frete;
    public string status = "AGUARDANDO EMBARQUE";
    public string mensagem;
    public int diaCriacao;
    public int diaEntrega;
    public string navioId;
    public bool repeticaoAutomatica;
}

[Serializable]
public sealed class RepeticaoMercado
{
    public string chave;
    public string itemId;
    public int teamId;
    public int quantidade;
    public bool comprar;
    public bool ativa;
    public int ultimoDiaProcessado = -1;
}

[Serializable]
public sealed class SistemaLogisticaMercadoSaveData
{
    public List<PedidoMercadoLogistico> pedidos = new List<PedidoMercadoLogistico>();
    public List<RepeticaoMercado> repeticoes = new List<RepeticaoMercado>();
}

/// <summary>
/// Entrega maritima de compras internacionais. Mantem a transacao economica
/// separada da viagem visual para impedir transferencias instantaneas e para
/// limitar o numero de navios ativos.
/// </summary>
public sealed class SistemaLogisticaMercado : MonoBehaviour
{
    private const int MaxPedidosTerminaisRetidos = 64;

    private sealed class Viagem
    {
        public NavioCargaMercado navio;
        public PierMarinha origem;
        public PierMarinha destino;
        public Vector3 baseNavio;
        public List<PedidoMercadoLogistico> pedidos = new List<PedidoMercadoLogistico>();
        public bool carregando;
    }

    public static SistemaLogisticaMercado Instancia { get; private set; }
    public const float FretePercentual = 0.05f;

    [Header("Configuracao")]
    // A logistica nao precisa disputar o mesmo frame com a simulacao taticamente.
    // Um ciclo de 1 s mantem a entrega responsiva e evita reprocessamento excessivo.
    public float intervaloProcessamento = 1f;
    public float tempoEmbarque = 1.25f;
    public float tempoDesembarque = 1.25f;
    public int intervaloRepeticaoDias = 2;
    public GameObject prefabNavioCarga;

    [Header("Estado")]
    public List<PedidoMercadoLogistico> pedidos = new List<PedidoMercadoLogistico>();
    public List<RepeticaoMercado> repeticoes = new List<RepeticaoMercado>();

    private readonly List<NavioCargaMercado> navios = new List<NavioCargaMercado>(16);
    private readonly List<Viagem> viagens = new List<Viagem>(16);
    private readonly List<PedidoMercadoLogistico> entregasPendentes = new List<PedidoMercadoLogistico>(8);
    private readonly Dictionary<string, List<PedidoMercadoLogistico>> gruposBuffer = new Dictionary<string, List<PedidoMercadoLogistico>>(StringComparer.Ordinal);
    private readonly List<string> chavesGruposBuffer = new List<string>(16);
    private readonly Dictionary<int, PierMarinha> cachePiersPorTeam = new Dictionary<int, PierMarinha>();
    private float proximoProcessamento;
    private float proximoLogSemPorto;
    private float proximaAtualizacaoCachePiers = -1f;
    private int ultimoDiaRepeticao = -1;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;
        SistemaLogisticaMercado existente = FindFirstObjectByType<SistemaLogisticaMercado>();
        if (existente != null)
        {
            Instancia = existente;
            existente.RegistrarNaviosDaCena();
            return;
        }

        GameObject go = new GameObject("SistemaLogisticaMercado_Runtime");
        Instancia = go.AddComponent<SistemaLogisticaMercado>();
        DontDestroyOnLoad(go);
        Instancia.RegistrarNaviosDaCena();
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
        if (pedidos == null) pedidos = new List<PedidoMercadoLogistico>();
        if (repeticoes == null) repeticoes = new List<RepeticaoMercado>();
        RegistrarNaviosDaCena();
    }

    private void Update()
    {
        if (Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;
        if (Time.unscaledTime < proximoProcessamento) return;
        proximoProcessamento = Time.unscaledTime + Mathf.Max(0.25f, intervaloProcessamento);
        ProcessarViagens();
        ProcessarEntregasPendentes();
        ProcessarRepeticoes();
        PodarHistoricoTerminado();
    }

    public void RegistrarNavio(NavioCargaMercado navio)
    {
        if (navio == null) return;
        navio.SincronizarEquipeDaIdentidade();
        if (!navios.Contains(navio)) navios.Add(navio);
    }

    private void RegistrarNaviosDaCena()
    {
        NavioCargaMercado[] existentes = FindObjectsByType<NavioCargaMercado>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < existentes.Length; i++) RegistrarNavio(existentes[i]);
    }

    public void DesregistrarNavio(NavioCargaMercado navio)
    {
        navios.Remove(navio);
        for (int i = viagens.Count - 1; i >= 0; i--)
        {
            if (viagens[i].navio == navio)
            {
                RecuperarPedidos(viagens[i], "Navio de carga indisponivel; pedido devolvido ao mercado.");
                viagens.RemoveAt(i);
            }
        }
    }

    public bool Enfileirar(TransacaoMercado transacao, int frete, bool repeticaoAutomatica, out PedidoMercadoLogistico pedido)
    {
        pedido = null;
        if (transacao == null || transacao.compradorTeamId == transacao.vendedorTeamId)
            return false;

        GarantirInstancia();
        pedido = new PedidoMercadoLogistico
        {
            id = string.IsNullOrEmpty(transacao.id) ? Guid.NewGuid().ToString("N") : transacao.id,
            compradorTeamId = transacao.compradorTeamId,
            vendedorTeamId = transacao.vendedorTeamId,
            itemId = transacao.itemId,
            quantidade = transacao.quantidade,
            precoUnitario = transacao.precoUnitario,
            total = transacao.total,
            frete = Mathf.Max(0, frete),
            mensagem = "Compra reservada; aguardando embarque maritimo.",
            diaCriacao = ObterDiaAtual(),
            repeticaoAutomatica = repeticaoAutomatica
        };
        pedidos.Add(pedido);
        transacao.id = pedido.id;
        transacao.status = pedido.status;
        transacao.mensagem = pedido.mensagem;
        return true;
    }

    public bool PossuiRotaDeCarga(int vendedorTeamId, int compradorTeamId)
    {
        if (vendedorTeamId <= 0 || compradorTeamId <= 0 || vendedorTeamId == compradorTeamId)
            return false;
        AtualizarCachePiersSeNecessario();
        if (!cachePiersPorTeam.ContainsKey(vendedorTeamId) || !cachePiersPorTeam.ContainsKey(compradorTeamId))
            return false;

        return EncontrarNavioDisponivel(compradorTeamId) != null || ObterPrefabCarga() != null;
    }

    private void AtualizarCachePiersSeNecessario()
    {
        if (Time.unscaledTime < proximaAtualizacaoCachePiers) return;
        proximaAtualizacaoCachePiers = Time.unscaledTime + 1f;
        cachePiersPorTeam.Clear();
        PierMarinha[] encontrados = FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
        for (int i = 0; i < encontrados.Length; i++)
        {
            PierMarinha pier = encontrados[i];
            if (pier != null && pier.OwnerTeamId > 0 && !cachePiersPorTeam.ContainsKey(pier.OwnerTeamId))
                cachePiersPorTeam.Add(pier.OwnerTeamId, pier);
        }
    }

    public SistemaLogisticaMercadoSaveData CriarSaveData()
    {
        SistemaLogisticaMercadoSaveData salvo = new SistemaLogisticaMercadoSaveData();
        for (int i = 0; i < pedidos.Count; i++)
        {
            PedidoMercadoLogistico pedido = pedidos[i];
            if (pedido == null || PedidoTerminal(pedido)) continue;
            salvo.pedidos.Add(new PedidoMercadoLogistico
            {
                id = pedido.id,
                compradorTeamId = pedido.compradorTeamId,
                vendedorTeamId = pedido.vendedorTeamId,
                itemId = pedido.itemId,
                quantidade = pedido.quantidade,
                precoUnitario = pedido.precoUnitario,
                total = pedido.total,
                frete = pedido.frete,
                status = pedido.status,
                mensagem = pedido.mensagem,
                diaCriacao = pedido.diaCriacao,
                diaEntrega = pedido.diaEntrega,
                navioId = pedido.navioId,
                repeticaoAutomatica = pedido.repeticaoAutomatica
            });
        }

        for (int i = 0; i < repeticoes.Count; i++)
        {
            RepeticaoMercado repeticao = repeticoes[i];
            if (repeticao == null) continue;
            salvo.repeticoes.Add(new RepeticaoMercado
            {
                chave = repeticao.chave,
                itemId = repeticao.itemId,
                teamId = repeticao.teamId,
                quantidade = repeticao.quantidade,
                comprar = repeticao.comprar,
                ativa = repeticao.ativa,
                ultimoDiaProcessado = repeticao.ultimoDiaProcessado
            });
        }

        return salvo;
    }

    public void RestaurarSaveData(SistemaLogisticaMercadoSaveData salvo)
    {
        pedidos.Clear();
        repeticoes.Clear();
        viagens.Clear();
        entregasPendentes.Clear();
        navios.Clear();
        ultimoDiaRepeticao = -1;

        if (salvo != null)
        {
            if (salvo.pedidos != null)
            {
                for (int i = 0; i < salvo.pedidos.Count; i++)
                {
                    PedidoMercadoLogistico pedido = salvo.pedidos[i];
                    if (pedido == null || PedidoTerminal(pedido)) continue;

                    // Viagens e corrotinas não são persistidas. Reenfileirar
                    // preserva a transação já paga sem deixar carga órfã.
                    pedido.status = "AGUARDANDO EMBARQUE";
                    pedido.navioId = string.Empty;
                    pedido.mensagem = "Pedido restaurado; aguardando novo embarque.";
                    pedidos.Add(pedido);
                }
            }

            if (salvo.repeticoes != null)
            {
                for (int i = 0; i < salvo.repeticoes.Count; i++)
                {
                    RepeticaoMercado repeticao = salvo.repeticoes[i];
                    if (repeticao == null || string.IsNullOrWhiteSpace(repeticao.itemId)) continue;
                    repeticoes.Add(new RepeticaoMercado
                    {
                        chave = CriarChave(
                            repeticao.teamId > 0 ? repeticao.teamId : ResolverTeamJogador(),
                            repeticao.itemId,
                            repeticao.comprar),
                        itemId = repeticao.itemId,
                        teamId = repeticao.teamId > 0 ? repeticao.teamId : ResolverTeamJogador(),
                        quantidade = Mathf.Max(1, repeticao.quantidade),
                        comprar = repeticao.comprar,
                        ativa = repeticao.ativa,
                        ultimoDiaProcessado = repeticao.ultimoDiaProcessado
                    });
                }
            }
        }

        RegistrarNaviosDaCena();
    }

    public void LimparEstadoParaNovoJogo()
    {
        RestaurarSaveData(null);
    }

    private void PodarHistoricoTerminado()
    {
        int terminais = 0;
        for (int i = 0; i < pedidos.Count; i++)
        {
            if (pedidos[i] != null && PedidoTerminal(pedidos[i])) terminais++;
        }

        for (int i = 0; i < pedidos.Count && terminais > MaxPedidosTerminaisRetidos;)
        {
            if (PedidoTerminal(pedidos[i]))
            {
                pedidos.RemoveAt(i);
                terminais--;
            }
            else
            {
                i++;
            }
        }
    }

    private static bool PedidoTerminal(PedidoMercadoLogistico pedido)
    {
        return pedido != null
            && (string.Equals(pedido.status, "ENTREGUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pedido.status, "RECUPERADO", StringComparison.OrdinalIgnoreCase));
    }

    public bool TemRepeticao(string itemId, bool comprar)
    {
        return TemRepeticao(ResolverTeamJogador(), itemId, comprar);
    }

    public bool TemRepeticao(int teamId, string itemId, bool comprar)
    {
        NormalizarRepeticoes();
        string chave = CriarChave(teamId, itemId, comprar);
        return repeticoes.Any(x => x != null && x.ativa && x.chave == chave);
    }

    public void ConfigurarRepeticao(string itemId, int quantidade, bool comprar, bool ativa)
    {
        ConfigurarRepeticao(ResolverTeamJogador(), itemId, quantidade, comprar, ativa);
    }

    public void ConfigurarRepeticao(int teamId, string itemId, int quantidade, bool comprar, bool ativa)
    {
        if (teamId <= 0 || string.IsNullOrWhiteSpace(itemId)) return;
        GarantirInstancia();
        NormalizarRepeticoes();
        string chave = CriarChave(teamId, itemId, comprar);
        RepeticaoMercado repeticao = repeticoes.FirstOrDefault(x => x != null && x.chave == chave);
        if (repeticao == null)
        {
            repeticao = new RepeticaoMercado { chave = chave, itemId = itemId, teamId = teamId, comprar = comprar };
            repeticoes.Add(repeticao);
        }
        repeticao.teamId = teamId;
        repeticao.quantidade = Mathf.Max(1, quantidade);
        repeticao.ativa = ativa;
        repeticao.ultimoDiaProcessado = ObterDiaAtual();
    }

    private void ProcessarRepeticoes()
    {
        NormalizarRepeticoes();
        int dia = ObterDiaAtual();
        if (dia == ultimoDiaRepeticao || dia <= 0) return;
        ultimoDiaRepeticao = dia;

        for (int i = 0; i < repeticoes.Count; i++)
        {
            RepeticaoMercado repeticao = repeticoes[i];
            if (repeticao == null || !repeticao.ativa || dia - repeticao.ultimoDiaProcessado < intervaloRepeticaoDias)
                continue;

            repeticao.ultimoDiaProcessado = dia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (mercado == null) continue;

            string mensagem;
            bool ok = repeticao.comprar
                ? mercado.ComprarAutomaticamente(Mathf.Max(1, repeticao.teamId), repeticao.itemId, repeticao.quantidade, out mensagem)
                : mercado.VenderAutomaticamente(Mathf.Max(1, repeticao.teamId), repeticao.itemId, repeticao.quantidade, out mensagem);
            if (!ok && Time.unscaledTime >= proximoLogSemPorto)
            {
                proximoLogSemPorto = Time.unscaledTime + 20f;
                Debug.Log("[LogisticaMercado] Repeticao aguardando: " + mensagem);
            }
        }
    }

    private void ProcessarViagens()
    {
        LimparListas();
        gruposBuffer.Clear();
        chavesGruposBuffer.Clear();

        for (int i = 0; i < pedidos.Count; i++)
        {
            PedidoMercadoLogistico pedido = pedidos[i];
            if (pedido == null || !string.Equals(pedido.status, "AGUARDANDO EMBARQUE", StringComparison.OrdinalIgnoreCase))
                continue;

            string chave = pedido.vendedorTeamId + ":" + pedido.compradorTeamId;
            if (!gruposBuffer.TryGetValue(chave, out List<PedidoMercadoLogistico> grupo))
            {
                grupo = new List<PedidoMercadoLogistico>(4);
                gruposBuffer.Add(chave, grupo);
                chavesGruposBuffer.Add(chave);
            }
            grupo.Add(pedido);
        }

        for (int grupoIndex = 0; grupoIndex < chavesGruposBuffer.Count; grupoIndex++)
        {
            string chaveGrupo = chavesGruposBuffer[grupoIndex];
            List<PedidoMercadoLogistico> grupo = gruposBuffer[chaveGrupo];
            if (grupo == null || grupo.Count == 0 || GrupoJaEmViagem(grupo))
                continue;

            PedidoMercadoLogistico primeiro = grupo[0];
            if (primeiro == null) continue;
            PierMarinha origem = EncontrarPier(primeiro.vendedorTeamId);
            PierMarinha destino = EncontrarPier(primeiro.compradorTeamId);
            if (origem == null || destino == null)
            {
                if (Time.unscaledTime >= proximoLogSemPorto)
                {
                    proximoLogSemPorto = Time.unscaledTime + 20f;
                    Debug.Log("[LogisticaMercado] Aguardando pier de origem/destino para a rota " + chaveGrupo);
                }
                continue;
            }

            NavioCargaMercado navio = EncontrarNavioDisponivel(primeiro.compradorTeamId);
            bool fretado = false;
            if (navio == null)
            {
                navio = CriarFrete(origem);
                fretado = navio != null;
            }
            if (navio == null) continue;

            List<PedidoMercadoLogistico> selecionados = new List<PedidoMercadoLogistico>();
            float carga = 0f;
            for (int pedidoIndex = 0; pedidoIndex < grupo.Count; pedidoIndex++)
            {
                PedidoMercadoLogistico pedido = grupo[pedidoIndex];
                float peso = Mathf.Max(1, pedido.quantidade);
                if (selecionados.Count > 0 && carga + peso > navio.capacidadeCarga) break;
                selecionados.Add(pedido);
                carga += peso;
            }

            if (selecionados.Count == 0) continue;
            Viagem nova = new Viagem
            {
                navio = navio,
                origem = origem,
                destino = destino,
                baseNavio = ResolverBaseDoNavio(navio, destino),
                pedidos = selecionados,
                carregando = false
            };
            for (int i = 0; i < selecionados.Count; i++)
            {
                selecionados[i].status = "INDO A ORIGEM";
                selecionados[i].navioId = navio.GetInstanceID().ToString();
                selecionados[i].mensagem = fretado ? "Frete contratado; navio indo ao fornecedor." : "Navio de carga indo ao fornecedor.";
            }
            viagens.Add(nova);
            IniciarViagem(nova);
        }
    }

    private bool GrupoJaEmViagem(List<PedidoMercadoLogistico> grupo)
    {
        for (int viagemIndex = 0; viagemIndex < viagens.Count; viagemIndex++)
        {
            Viagem viagem = viagens[viagemIndex];
            if (viagem == null || viagem.pedidos == null) continue;
            for (int pedidoIndex = 0; pedidoIndex < viagem.pedidos.Count; pedidoIndex++)
            {
                PedidoMercadoLogistico emViagem = viagem.pedidos[pedidoIndex];
                if (emViagem == null) continue;
                for (int grupoIndex = 0; grupoIndex < grupo.Count; grupoIndex++)
                {
                    if (grupo[grupoIndex] != null && grupo[grupoIndex].id == emViagem.id)
                        return true;
                }
            }
        }
        return false;
    }

    private void IniciarViagem(Viagem viagem)
    {
        if (viagem == null || viagem.navio == null || viagem.origem == null) return;
        Vector3 origem = ResolverPontoPorto(viagem.origem);
        if (Vector3.Distance(viagem.navio.transform.position, origem) <= 6f)
        {
            AoChegarNaOrigem(viagem);
        }
        else
        {
            viagem.navio.Despachar(origem, () => AoChegarNaOrigem(viagem));
        }
    }

    private void AoChegarNaOrigem(Viagem viagem)
    {
        if (viagem == null || viagem.navio == null) return;
        viagem.carregando = true;
        for (int i = 0; i < viagem.pedidos.Count; i++)
        {
            viagem.pedidos[i].status = "CARREGANDO";
            viagem.pedidos[i].mensagem = "Carga sendo embarcada.";
        }
        StartCoroutine(EmbarcarEPartir(viagem));
    }

    private IEnumerator EmbarcarEPartir(Viagem viagem)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, tempoEmbarque));
        if (viagem == null || viagem.navio == null) yield break;
        viagem.carregando = false;
        Vector3 destino = ResolverPontoPorto(viagem.destino);
        for (int j = 0; j < viagem.pedidos.Count; j++)
        {
            viagem.pedidos[j].status = "EM TRANSITO";
            viagem.pedidos[j].mensagem = "Carga em transito maritimo.";
        }
        viagem.navio.Despachar(destino, () => AoChegarNoDestino(viagem));
    }

    private void AoChegarNoDestino(Viagem viagem)
    {
        if (viagem == null || viagem.navio == null) return;
        viagem.carregando = true;
        for (int i = 0; i < viagem.pedidos.Count; i++)
        {
            viagem.pedidos[i].status = "DESCARREGANDO";
            viagem.pedidos[i].mensagem = "Carga chegando ao pier do comprador.";
        }
        StartCoroutine(DesembarcarEFinalizar(viagem));
    }

    private IEnumerator DesembarcarEFinalizar(Viagem viagem)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, tempoDesembarque));
        if (viagem == null || viagem.navio == null) yield break;
        viagem.carregando = false;
        for (int j = 0; j < viagem.pedidos.Count; j++) FinalizarPedido(viagem.pedidos[j]);

        if (viagem.navio.Fretado)
        {
            Destroy(viagem.navio.gameObject, 0.2f);
            viagens.Remove(viagem);
        }
        else
        {
            viagem.navio.Despachar(viagem.baseNavio, () => FinalizarRetorno(viagem));
            for (int j = 0; j < viagem.pedidos.Count; j++) viagem.pedidos[j].mensagem = "Entregue; navio retornando ao pier-base.";
        }
    }

    private void FinalizarRetorno(Viagem viagem)
    {
        if (viagem == null) return;
        if (viagem.navio != null)
        {
            viagem.navio.PararNoPonto(viagem.baseNavio);
        }
        viagens.Remove(viagem);
    }

    private void FinalizarPedido(PedidoMercadoLogistico pedido)
    {
        if (pedido == null || string.Equals(pedido.status, "ENTREGUE", StringComparison.OrdinalIgnoreCase)) return;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosItemMercado item = mercado != null ? mercado.ObterItem(pedido.itemId) : null;
        if (governo == null || item == null)
        {
            RecuperarPedido(pedido, "Item ou governo indisponivel; valores devolvidos.");
            return;
        }

        bool entregue = true;
        if (item.equipamentoMilitar)
        {
            entregue = EntregarEquipamento(item, pedido.compradorTeamId, pedido.quantidade);
        }
        else if (item.municaoMilitar)
        {
            SistemaGastosMilitares.GarantirInstancia();
            if (SistemaGastosMilitares.Instancia != null)
                SistemaGastosMilitares.Instancia.AdicionarEstoqueMunicao(pedido.compradorTeamId, item.idMunicaoMilitar, pedido.quantidade);
        }
        else
        {
            governo.AdicionarEstoque(pedido.compradorTeamId, item.RecursoIdEfetivo, pedido.quantidade);
        }

        if (!entregue)
        {
            pedido.status = "AGUARDANDO DESTINO";
            pedido.mensagem = "Carga no pier; aguardando destino militar válido ou liberação de limite/efetivo do comprador.";
            if (!entregasPendentes.Contains(pedido)) entregasPendentes.Add(pedido);
            return;
        }

        governo.AdicionarSaldo(pedido.vendedorTeamId, pedido.total);
        pedido.status = "ENTREGUE";
        pedido.diaEntrega = ObterDiaAtual();
        pedido.mensagem = "Carga entregue no destino.";
        TransacaoMercado transacao = mercado.historico.FirstOrDefault(x => x != null && x.id == pedido.id);
        if (transacao != null)
        {
            transacao.status = "ENTREGUE";
            transacao.mensagem = pedido.mensagem;
        }
    }

    private void ProcessarEntregasPendentes()
    {
        for (int i = entregasPendentes.Count - 1; i >= 0; i--)
        {
            PedidoMercadoLogistico pedido = entregasPendentes[i];
            if (pedido == null || pedido.status == "ENTREGUE" || pedido.status == "RECUPERADO")
            {
                entregasPendentes.RemoveAt(i);
                continue;
            }

            FinalizarPedido(pedido);
            if (pedido.status == "ENTREGUE" || pedido.status == "RECUPERADO")
                entregasPendentes.RemoveAt(i);
        }
    }

    private bool EntregarEquipamento(DadosItemMercado item, int teamId, int quantidade)
    {
        DadosConstrucao ficha = EncontrarFicha(item.prefabId);
        if (ficha == null || !ficha.TryGetPrefabBasico(out GameObject prefab)) return false;

        int lote = Mathf.Max(1, quantidade);
        // Avalie o limite antes de procurar destinos na cena. Pedidos que ainda
        // nao podem ser entregues continuam pendentes, mas evitam buscas globais
        // de aeroporto a cada ciclo do scheduler.
        if (teamId == 1 && !GovernadorGameplayRTS.PermitirProducao(ficha, lote, out _))
        {
            return false;
        }

        GerenciadorAeroporto aeroportoDestino;
        Transform destino = EncontrarDestinoComAeroporto(item.tipoEntrega, teamId, out aeroportoDestino);
        if (destino == null) return false;
        string tipoEntrega = item.tipoEntrega ?? string.Empty;
        bool entregaAerea = string.Equals(tipoEntrega, "aeronave", StringComparison.OrdinalIgnoreCase);
        C700TransporteAereo prefabC700 = entregaAerea
            ? prefab.GetComponentInChildren<C700TransporteAereo>(true)
            : null;
        if (entregaAerea
            && (aeroportoDestino == null
                || (prefabC700 == null
                    && prefab.GetComponentInChildren<Helicoptero>(true) == null
                    && prefab.GetComponentInChildren<ControleAviao>(true) == null)))
        {
            return false;
        }

        Transform paradaC700 = null;
        if (prefabC700 != null)
        {
            paradaC700 = aeroportoDestino.ObterParadaRecebimentoTransporteAereo();
            if (paradaC700 == null) return false;
        }

        TipoUnidade tipoUnidadeEntrega = tipoEntrega.IndexOf("aeronave", StringComparison.OrdinalIgnoreCase) >= 0
            ? TipoUnidade.Aereo
            : tipoEntrega.IndexOf("navio", StringComparison.OrdinalIgnoreCase) >= 0
                ? TipoUnidade.Naval : TipoUnidade.Veiculo;

        int efetivoNecessario = SistemaGovernoMundial.ObterEfetivoNecessario(prefab, tipoUnidadeEntrega);
        SistemaGovernoMundial governoEfetivo = efetivoNecessario > 0 ? SistemaGovernoMundial.Instancia : null;
        if (efetivoNecessario > 0
            && (governoEfetivo == null
                || !governoEfetivo.PodeProduzirUnidadeComEfetivo(teamId, prefab, lote, tipoUnidadeEntrega)))
        {
            return false;
        }

        List<GameObject> unidadesCriadas = new List<GameObject>(lote);
        for (int i = 0; i < lote; i++)
        {
            GameObject unidade = Instantiate(prefab, destino.position + Vector3.up * (1.2f + i * 0.35f), destino.rotation);
            unidadesCriadas.Add(unidade);
            IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>() ?? unidade.AddComponent<IdentidadeUnidade>();
            identidade.teamID = teamId;
            identidade.tipoUnidade = tipoUnidadeEntrega;
            governoEfetivo = governoEfetivo != null ? governoEfetivo : SistemaGovernoMundial.Instancia;
            if (SistemaGovernoMundial.ObterEfetivoNecessario(unidade, tipoUnidadeEntrega) > 0
                && (governoEfetivo == null || !governoEfetivo.TentarMobilizarEfetivoDaUnidade(teamId, unidade)))
            {
                ReverterUnidadesCriadas(unidadesCriadas, teamId, governoEfetivo);
                return false;
            }
            ServicoAbastecimento.AbastecerUnidadeProduzida(unidade);

            if (entregaAerea)
            {
                C700TransporteAereo c700 = unidade.GetComponentInChildren<C700TransporteAereo>(true);
                if (c700 != null)
                {
                    if (!aeroportoDestino.RegistrarTransporteAereoRecebido(c700))
                    {
                        Debug.LogError($"[Mercado] Aeroporto {aeroportoDestino.name} recusou o C-700 após validar a vaga; pedido permanece pendente.");
                        ReverterUnidadesCriadas(unidadesCriadas, teamId, governoEfetivo);
                        return false;
                    }
                    continue;
                }

                Helicoptero helicoptero = unidade.GetComponentInChildren<Helicoptero>(true);
                if (helicoptero != null)
                {
                    if (!aeroportoDestino.RegistrarHelicopteroRecebido(helicoptero))
                    {
                        ReverterUnidadesCriadas(unidadesCriadas, teamId, governoEfetivo);
                        return false;
                    }
                    continue;
                }

                ControleAviao aviao = unidade.GetComponentInChildren<ControleAviao>(true);
                if (aviao != null)
                {
                    if (!aeroportoDestino.RegistrarAeronaveRecebida(aviao))
                    {
                        ReverterUnidadesCriadas(unidadesCriadas, teamId, governoEfetivo);
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static void ReverterUnidadesCriadas(List<GameObject> unidades, int teamId, SistemaGovernoMundial governo)
    {
        if (unidades == null) return;
        for (int i = 0; i < unidades.Count; i++)
        {
            GameObject unidade = unidades[i];
            if (unidade == null) continue;
            governo?.LiberarEfetivoMobilizadoSemBaixa(teamId, unidade);
            Destroy(unidade);
        }
        unidades.Clear();
    }

    private Transform EncontrarDestino(string tipo, int teamId)
    {
        return EncontrarDestinoComAeroporto(tipo, teamId, out _);
    }

    private Transform EncontrarDestinoComAeroporto(string tipo, int teamId, out GerenciadorAeroporto aeroportoDestino)
    {
        aeroportoDestino = null;
        if (string.Equals(tipo, "aeronave", StringComparison.OrdinalIgnoreCase))
        {
            GerenciadorAeroporto[] aeroportos = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            for (int i = 0; i < aeroportos.Length; i++)
            {
                GerenciadorAeroporto candidato = aeroportos[i];
                if (candidato == null) continue;

                IdentidadeUnidade identidade = candidato.GetComponentInParent<IdentidadeUnidade>();
                // Sem identidade não há como provar a quem a base pertence.
                // Falhar fechado evita entregar aeronaves na base de outro país.
                if (identidade != null && identidade.teamID == teamId)
                {
                    aeroportoDestino = candidato;
                    break;
                }
            }

            return aeroportoDestino != null
                ? (aeroportoDestino.hangarAviao != null ? aeroportoDestino.hangarAviao : aeroportoDestino.transform)
                : null;
        }

        if (string.Equals(tipo, "navio", StringComparison.OrdinalIgnoreCase))
        {
            Estaleiro estaleiro = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None)
                .FirstOrDefault(x => x.OwnerTeamId == teamId);
            if (estaleiro != null) return estaleiro.pontoDeSaida != null ? estaleiro.pontoDeSaida : estaleiro.transform;
            PierMarinha pier = EncontrarPier(teamId);
            return pier != null ? (pier.pontoEntrada != null ? pier.pontoEntrada : pier.transform) : null;
        }

        GerenciadorQuartel quartel = FindObjectsByType<GerenciadorQuartel>(FindObjectsSortMode.None)
            .FirstOrDefault(x => ObterTeam(x) == teamId);
        return quartel != null ? quartel.transform : EncontrarPier(teamId)?.transform;
    }

    private static int ObterTeam(Component componente)
    {
        IdentidadeUnidade id = componente != null ? componente.GetComponentInParent<IdentidadeUnidade>() : null;
        return id != null ? id.teamID : 0;
    }

    private void RecuperarPedidos(Viagem viagem, string motivo)
    {
        if (viagem == null) return;
        for (int i = 0; i < viagem.pedidos.Count; i++) RecuperarPedido(viagem.pedidos[i], motivo);
    }

    private void RecuperarPedido(PedidoMercadoLogistico pedido, string motivo)
    {
        if (pedido == null || pedido.status == "RECUPERADO") return;
        entregasPendentes.Remove(pedido);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        DadosItemMercado item = mercado != null ? mercado.ObterItem(pedido.itemId) : null;
        if (governo != null && item != null && item.municaoMilitar)
        {
            SistemaGastosMilitares.GarantirInstancia();
            SistemaGastosMilitares.Instancia?.AdicionarEstoqueMunicao(pedido.vendedorTeamId, item.idMunicaoMilitar, pedido.quantidade);
        }
        else if (governo != null && item != null && !item.equipamentoMilitar)
        {
            governo.AdicionarEstoque(pedido.vendedorTeamId, item.RecursoIdEfetivo, pedido.quantidade);
        }
        if (governo != null)
        {
            governo.AdicionarSaldo(pedido.compradorTeamId, pedido.total + pedido.frete);
        }
        if (item != null) item.estoqueGlobal += pedido.quantidade;
        pedido.status = "RECUPERADO";
        pedido.mensagem = motivo;
        TransacaoMercado transacao = mercado != null ? mercado.historico.FirstOrDefault(x => x != null && x.id == pedido.id) : null;
        if (transacao != null)
        {
            transacao.status = "RECUPERADO";
            transacao.mensagem = motivo;
        }
    }

    private NavioCargaMercado EncontrarNavioDisponivel(int teamId)
    {
        return navios.FirstOrDefault(x => x != null && x.Disponivel && x.OwnerTeamId == teamId);
    }

    private NavioCargaMercado CriarFrete(PierMarinha origem)
    {
        GameObject prefab = ObterPrefabCarga();
        if (prefab == null || origem == null) return null;
        Vector3 ponto = ResolverPontoPorto(origem);
        GameObject go = Instantiate(prefab, ponto, origem.transform.rotation);
        NavioCargaMercado navio = go.GetComponent<NavioCargaMercado>() ?? go.AddComponent<NavioCargaMercado>();
        navio.Inicializar(0, true);
        RegistrarNavio(navio);
        return navio;
    }

    private GameObject ObterPrefabCarga()
    {
        if (prefabNavioCarga != null) return prefabNavioCarga;
        if (MenuConstrucao.catalogoGlobal != null)
        {
            DadosConstrucao ficha = MenuConstrucao.catalogoGlobal.FirstOrDefault(x => x != null &&
                x.GetDisplayName().IndexOf("navio de carga", StringComparison.OrdinalIgnoreCase) >= 0);
            if (ficha != null && ficha.TryGetPrefabBasico(out GameObject prefab))
            {
                prefabNavioCarga = prefab;
                return prefab;
            }
        }

        // Cenas antigas podem iniciar a logistica antes de o catalogo da UI
        // terminar o bootstrap. O caminho Resources permite manter uma ultima
        // reserva para builds que tenham o prefab incluido nessa pasta.
        prefabNavioCarga = Resources.Load<GameObject>("Navio de carga");
        if (prefabNavioCarga != null) return prefabNavioCarga;
        return null;
    }

    private Vector3 ResolverBaseDoNavio(NavioCargaMercado navio, PierMarinha destino)
    {
        PierMarinha basePier = EncontrarPier(navio.OwnerTeamId);
        return basePier != null ? ResolverPontoPorto(basePier) : destino.transform.position;
    }

    private static PierMarinha EncontrarPier(int teamId)
    {
        return FindObjectsByType<PierMarinha>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x != null && x.OwnerTeamId == teamId);
    }

    private static Vector3 ResolverPontoPorto(PierMarinha pier)
    {
        if (pier == null) return Vector3.zero;
        if (pier.pontoEntrada != null) return pier.pontoEntrada.position;
        if (pier.saida_petro != null) return pier.saida_petro.position;
        if (pier.pontosDeSaida != null && pier.pontosDeSaida.Length > 0 && pier.pontosDeSaida[0] != null)
            return pier.pontosDeSaida[0].position;
        return pier.transform.position;
    }

    private static DadosConstrucao EncontrarFicha(string id)
    {
        if (MenuConstrucao.catalogoGlobal != null)
            return MenuConstrucao.catalogoGlobal.FirstOrDefault(x => x != null && string.Equals(x.GetStableId(), id, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    private static string CriarChave(int teamId, string itemId, bool comprar)
        => teamId + ":" + (comprar ? "C:" : "V:") + itemId;

    private static int ResolverTeamJogador()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        return governo != null ? Mathf.Max(1, governo.teamJogador) : 1;
    }

    private void NormalizarRepeticoes()
    {
        if (repeticoes == null) repeticoes = new List<RepeticaoMercado>();
        for (int i = 0; i < repeticoes.Count; i++)
        {
            RepeticaoMercado repeticao = repeticoes[i];
            if (repeticao == null || string.IsNullOrWhiteSpace(repeticao.itemId)) continue;
            if (repeticao.teamId <= 0) repeticao.teamId = ResolverTeamJogador();
            repeticao.chave = CriarChave(repeticao.teamId, repeticao.itemId, repeticao.comprar);
        }
    }

    private static int ObterDiaAtual()
    {
        return GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
    }

    private void LimparListas()
    {
        navios.RemoveAll(x => x == null);
        viagens.RemoveAll(x => x == null || x.navio == null);
        entregasPendentes.RemoveAll(x => x == null || x.status == "ENTREGUE" || x.status == "RECUPERADO");
    }
}
