using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public sealed class GovernadorGameplayRTS : MonoBehaviour
{
    private struct ContagemCampo
    {
        public int total;
        public int infantaria;
        public int veiculos;
        public int aereos;
        public int navais;
        public int estruturas;
    }

    public static GovernadorGameplayRTS Instancia { get; private set; }

    [Header("Atualizacao")]
    [SerializeField] private float intervaloRefresh = 1.25f;

    [Header("Orcamento Base")]
    [SerializeField] private int limiteTotal = 180;
    [SerializeField] private int limiteInfantaria = 60;
    [SerializeField] private int limiteVeiculos = 45;
    [SerializeField] private int limiteAereos = 24;
    [SerializeField] private int limiteNavais = 24;
    [SerializeField] private int limiteEstruturas = 70;

    [Header("Resposta a Pressao")]
    [SerializeField] private bool bloquearProducaoQuandoSaturado = true;
    [SerializeField] private bool bloquearAceleracaoQuandoSaturado = true;
    [SerializeField] private float multiplicadorLimiteSobPressao = 0.9f;
    [SerializeField] private float multiplicadorLimiteSaturado = 0.75f;

    private float proximoRefresh;
    private ContagemCampo contagem;

    private static readonly List<IdentidadeUnidade> _bufferUnidades = new List<IdentidadeUnidade>(2048);
    private static readonly List<Estaleiro> _bufferEstaleiros = new List<Estaleiro>(16);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string cenaAtual = SceneManager.GetActiveScene().name;
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(cenaAtual))
        {
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<GovernadorGameplayRTS>() != null)
        {
            return;
        }

        new GameObject("GovernadorGameplayRTS").AddComponent<GovernadorGameplayRTS>();
    }

    private void Awake()
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        AtualizarContagem(true);
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        AtualizarContagem(false);
    }

    public static bool PermitirProducao(DadosConstrucao item, int quantidade, out string motivo)
    {
        motivo = string.Empty;
        if (Instancia == null || item == null || quantidade <= 0)
        {
            return true;
        }

        return Instancia.AvaliarProducao(item.PrefabDaUnidade, Instancia.InferirTipo(item), item, quantidade, out motivo);
    }

    public static bool PermitirProducaoDePrefab(GameObject prefab, TipoUnidade tipo, int quantidade, out string motivo)
    {
        motivo = string.Empty;
        if (Instancia == null || quantidade <= 0)
        {
            return true;
        }

        return Instancia.AvaliarProducao(prefab, tipo, null, quantidade, out motivo);
    }

    public static bool BloquearAceleracaoTempo(out string motivo)
    {
        motivo = string.Empty;
        if (Instancia == null)
        {
            return false;
        }

        return Instancia.DeveBloquearAceleracao(out motivo);
    }

    public static string ObterResumoHud()
    {
        if (Instancia == null)
        {
            return "Campo: aguardando leitura.";
        }

        Instancia.AtualizarContagem(false);

        int limiteTotalAtual = Instancia.ObterLimiteAjustado(Instancia.limiteTotal);
        int limiteTerraAtual = Instancia.ObterLimiteAjustado(Instancia.limiteInfantaria + Instancia.limiteVeiculos);
        return $"Campo {Instancia.contagem.total}/{limiteTotalAtual}  Terra {Instancia.contagem.infantaria + Instancia.contagem.veiculos}/{limiteTerraAtual}  Ar {Instancia.contagem.aereos}/{Instancia.ObterLimiteAjustado(Instancia.limiteAereos)}  Mar {Instancia.contagem.navais}/{Instancia.ObterLimiteAjustado(Instancia.limiteNavais)}  Estruturas {Instancia.contagem.estruturas}/{Instancia.ObterLimiteAjustado(Instancia.limiteEstruturas)}";
    }

    private bool AvaliarProducao(GameObject prefab, TipoUnidade tipo, DadosConstrucao item, int quantidade, out string motivo)
    {
        motivo = string.Empty;
        // A contagem periódica atende HUD e aceleração; o gate de compra precisa
        // enxergar unidades que entraram em campo desde o último snapshot.
        AtualizarContagem(true);

        if (bloquearProducaoQuandoSaturado && DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            motivo = "Produzacao bloqueada: o runtime esta saturado. Segure a linha antes de expandir.";
            return false;
        }

        ContagemCampo contagemProjetada = contagem;
        int pendentesMesmoItem = SomarProducoesPendentes(ref contagemProjetada, prefab, tipo);
        int limiteCategoria = ObterLimiteCategoria(tipo);
        int contagemCategoria = ObterContagemCategoria(contagemProjetada, tipo);

        if (contagemProjetada.total + quantidade > ObterLimiteAjustado(limiteTotal))
        {
            motivo = "Orcamento de batalha cheio: reduza o exercito em campo antes de produzir mais.";
            return false;
        }

        if (contagemCategoria + quantidade > ObterLimiteAjustado(limiteCategoria))
        {
            motivo = "Limite da categoria atingido para este momento da partida.";
            return false;
        }

        DadosBalanceamentoUnidade balanceamento = item != null ? item.balanceamento : null;
        if (balanceamento != null && balanceamento.limiteEmCampo > 0)
        {
            int emCampo = ContarInstanciasDoMesmoItem(prefab) + pendentesMesmoItem;
            if (emCampo + quantidade > balanceamento.limiteEmCampo)
            {
                string nomeItem = item != null ? item.NomeItem : prefab != null ? prefab.name : "unidade";
                motivo = $"Limite tatico atingido para {nomeItem}: {emCampo}/{balanceamento.limiteEmCampo} em campo.";
                return false;
            }
        }

        return true;
    }

    private bool DeveBloquearAceleracao(out string motivo)
    {
        motivo = string.Empty;
        if (!bloquearAceleracaoQuandoSaturado)
        {
            return false;
        }

        AtualizarContagem(false);

        if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            motivo = "Aceleracao desativada: runtime saturado.";
            return true;
        }

        int limiteAtual = ObterLimiteAjustado(limiteTotal);
        if (contagem.total >= Mathf.RoundToInt(limiteAtual * 0.92f))
        {
            motivo = "Aceleracao travada para preservar leitura e desempenho no fim da batalha.";
            return true;
        }

        return false;
    }

    private void AtualizarContagem(bool forcar)
    {
        if (!forcar && Time.unscaledTime < proximoRefresh)
        {
            return;
        }

        proximoRefresh = Time.unscaledTime + Mathf.Max(0.2f, intervaloRefresh);
        contagem = new ContagemCampo();

        RegistroEntidadesJogo.FillUnidades(_bufferUnidades);
        for (int i = 0; i < _bufferUnidades.Count; i++)
        {
            IdentidadeUnidade identidade = _bufferUnidades[i];
            if (identidade == null || identidade.teamID != 1)
            {
                continue;
            }

            contagem.total++;
            switch (identidade.tipoUnidade)
            {
                case TipoUnidade.Infantaria:
                    contagem.infantaria++;
                    break;
                case TipoUnidade.Veiculo:
                    contagem.veiculos++;
                    break;
                case TipoUnidade.Aereo:
                    contagem.aereos++;
                    break;
                case TipoUnidade.Naval:
                    contagem.navais++;
                    break;
                case TipoUnidade.Estrutura:
                    contagem.estruturas++;
                    break;
            }
        }
    }

    private int ObterLimiteAjustado(int limiteBase)
    {
        float multiplicador = 1f;
        if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            multiplicador = multiplicadorLimiteSaturado;
        }
        else if (DiagnosticoDesempenhoJogo.RuntimeSobPressao())
        {
            multiplicador = multiplicadorLimiteSobPressao;
        }

        return Mathf.Max(1, Mathf.RoundToInt(limiteBase * multiplicador));
    }

    private int ObterLimiteCategoria(TipoUnidade tipo)
    {
        switch (tipo)
        {
            case TipoUnidade.Infantaria:
                return limiteInfantaria;
            case TipoUnidade.Veiculo:
                return limiteVeiculos;
            case TipoUnidade.Aereo:
                return limiteAereos;
            case TipoUnidade.Naval:
                return limiteNavais;
            case TipoUnidade.Estrutura:
                return limiteEstruturas;
            default:
                return limiteTotal;
        }
    }

    private int ObterContagemCategoria(ContagemCampo origem, TipoUnidade tipo)
    {
        switch (tipo)
        {
            case TipoUnidade.Infantaria:
                return origem.infantaria;
            case TipoUnidade.Veiculo:
                return origem.veiculos;
            case TipoUnidade.Aereo:
                return origem.aereos;
            case TipoUnidade.Naval:
                return origem.navais;
            case TipoUnidade.Estrutura:
                return origem.estruturas;
            default:
                return origem.total;
        }
    }

    private int SomarProducoesPendentes(ref ContagemCampo destino, GameObject prefabFiltro, TipoUnidade tipoAtual)
    {
        int mesmoItem = 0;
        GerenteDeJogo gerente = GerenteDeJogo.Instancia;
        List<GerenteDeJogo.PedidoDeProducao> pedidos = gerente != null ? gerente.filaProducao : null;

        if (pedidos != null)
        {
            for (int i = 0; i < pedidos.Count; i++)
            {
                GerenteDeJogo.PedidoDeProducao pedido = pedidos[i];
                if (pedido == null || pedido.prefab == null)
                {
                    continue;
                }

                TipoUnidade tipoPedido = InferirTipoPedido(pedido, prefabFiltro, tipoAtual);
                AdicionarPendente(ref destino, tipoPedido);
                if (CorrespondePrefab(pedido.prefab, prefabFiltro))
                {
                    mesmoItem++;
                }
            }
        }

        _bufferEstaleiros.Clear();
        RegistroEntidadesJogo.FillEstaleiros(_bufferEstaleiros);
        for (int i = 0; i < _bufferEstaleiros.Count; i++)
        {
            Estaleiro estaleiro = _bufferEstaleiros[i];
            if (estaleiro == null || estaleiro.OwnerTeamId != 1)
            {
                continue;
            }

            int naviosPendentes = estaleiro.ContarUnidadesPendentes();
            destino.total += naviosPendentes;
            destino.navais += naviosPendentes;
            if (prefabFiltro != null)
            {
                mesmoItem += estaleiro.ContarUnidadesPendentes(prefabFiltro);
            }
        }

        return mesmoItem;
    }

    private static TipoUnidade InferirTipoPedido(GerenteDeJogo.PedidoDeProducao pedido, GameObject prefabFiltro, TipoUnidade tipoAtual)
    {
        if (pedido.ehSoldado)
        {
            return TipoUnidade.Infantaria;
        }

        if (pedido.ehNavio || pedido.ehCarrier)
        {
            return TipoUnidade.Naval;
        }

        if (pedido.ehAviao || pedido.ehHelicoptero)
        {
            return TipoUnidade.Aereo;
        }

        if (CorrespondePrefab(pedido.prefab, prefabFiltro))
        {
            return tipoAtual;
        }

        IdentidadeUnidade identidade = pedido.prefab.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = pedido.prefab.GetComponentInChildren<IdentidadeUnidade>(true);
        }

        return identidade != null ? identidade.tipoUnidade : TipoUnidade.Veiculo;
    }

    private static void AdicionarPendente(ref ContagemCampo destino, TipoUnidade tipo)
    {
        destino.total++;
        switch (tipo)
        {
            case TipoUnidade.Infantaria:
                destino.infantaria++;
                break;
            case TipoUnidade.Veiculo:
                destino.veiculos++;
                break;
            case TipoUnidade.Aereo:
                destino.aereos++;
                break;
            case TipoUnidade.Naval:
                destino.navais++;
                break;
            case TipoUnidade.Estrutura:
                destino.estruturas++;
                break;
        }
    }

    private static bool CorrespondePrefab(GameObject candidato, GameObject filtro)
    {
        if (candidato == null || filtro == null)
        {
            return false;
        }

        return candidato == filtro
            || string.Equals(candidato.name, filtro.name, StringComparison.Ordinal);
    }

    private int ContarInstanciasDoMesmoItem(GameObject prefab)
    {
        if (prefab == null)
        {
            return 0;
        }

        string nomePrefab = prefab.name;
        int total = 0;

        // AvaliarProducao atualiza o buffer antes de chamar este limite por ficha.
        for (int i = 0; i < _bufferUnidades.Count; i++)
        {
            IdentidadeUnidade identidade = _bufferUnidades[i];
            if (identidade == null || identidade.teamID != 1 || identidade.gameObject == null)
            {
                continue;
            }

            string nomeInstancia = identidade.gameObject.name;
            if (nomeInstancia != null && nomeInstancia.StartsWith(nomePrefab, StringComparison.Ordinal))
            {
                total++;
            }
        }

        return total;
    }

    private TipoUnidade InferirTipo(DadosConstrucao item)
    {
        if (item == null)
        {
            return TipoUnidade.Veiculo;
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura
            || item.categoria == DadosConstrucao.CategoriaItem.Energia
            || item.categoria == DadosConstrucao.CategoriaItem.Urbana
            || item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return TipoUnidade.Estrutura;
        }

        GameObject prefab = item.PrefabDaUnidade;
        if (prefab == null)
        {
            switch (item.categoria)
            {
                case DadosConstrucao.CategoriaItem.Aeronautica:
                    return TipoUnidade.Aereo;
                case DadosConstrucao.CategoriaItem.Marinha:
                    return TipoUnidade.Naval;
                default:
                    return TipoUnidade.Veiculo;
            }
        }

        IdentidadeUnidade identidade = prefab.GetComponent<IdentidadeUnidade>() ?? prefab.GetComponentInChildren<IdentidadeUnidade>(true);
        if (identidade != null)
        {
            return identidade.tipoUnidade;
        }

        if (prefab.CompareTag("Imovel")
            || prefab.GetComponent<Fabrica>() != null
            || prefab.GetComponent<Estaleiro>() != null
            || prefab.GetComponent<Heliporto>() != null
            || prefab.GetComponent<GerenciadorAeroporto>() != null
            || prefab.GetComponent<PierMarinha>() != null)
        {
            return TipoUnidade.Estrutura;
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
            || prefab.GetComponent<ControleAviao>() != null
            || prefab.GetComponentInChildren<ControleAviao>(true) != null
            || prefab.GetComponent<Helicoptero>() != null
            || prefab.GetComponentInChildren<Helicoptero>(true) != null)
        {
            return TipoUnidade.Aereo;
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Marinha
            || prefab.GetComponent<IdentidadeNaval>() != null
            || prefab.GetComponentInChildren<IdentidadeNaval>(true) != null
            || prefab.GetComponent<ControleNavioRealista>() != null
            || prefab.GetComponentInChildren<ControleNavioRealista>(true) != null
            || prefab.GetComponent<HovercraftTransporte>() != null
            || prefab.GetComponentInChildren<HovercraftTransporte>(true) != null)
        {
            return TipoUnidade.Naval;
        }

        SistemaDeDanos danos = prefab.GetComponent<SistemaDeDanos>() ?? prefab.GetComponentInChildren<SistemaDeDanos>(true);
        if (danos != null && danos.unidadeBiologica)
        {
            return TipoUnidade.Infantaria;
        }

        return TipoUnidade.Veiculo;
    }
}
