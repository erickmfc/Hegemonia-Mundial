using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Navio logístico que transfere combustível para a frota naval militar.
/// Toda aproximação, perseguição e volta ao pier passam pelo controlador naval
/// compartilhado, em vez de mover o Transform diretamente.
/// </summary>
public class NavioAbastecimento : MonoBehaviour
{
    public enum ModoOperacaoAbastecimento
    {
        Manual,
        Automatico
    }

    private enum EstadoOperacao
    {
        Parado,
        IndoAlvo,
        Abastecendo,
        IndoPier,
        AguardandoPier,
        AncorandoPier
    }

    [Header("Configurações de Combustível")]
    [Tooltip("Reserva disponível para abastecer outros navios.")]
    public float combustivelTotal = 10000f;
    [Tooltip("Quantidade de combustível transferida por segundo.")]
    public float taxaAbastecimento = 50f;
    [Tooltip("Fallback para alvos antigos que ainda não possuem capacidade registrada.")]
    public float metaTransferenciaPorNavio = 500f;

    [Header("Operação Manual / Automática")]
    public ModoOperacaoAbastecimento modoOperacao = ModoOperacaoAbastecimento.Manual;
    [Range(0.05f, 0.95f)]
    [Tooltip("No automático, só navios militares abaixo deste percentual entram na fila.")]
    public float limiteAtendimentoAutomatico = 0.60f;
    [Range(0.05f, 0.95f)]
    [Tooltip("Sem alvos pendentes, abaixo deste percentual o abastecedor procura ancorar no pier.")]
    public float limiteRetornoAoPier = 0.30f;
    [Tooltip("Capacidade da reserva do próprio abastecedor. Se -1, usa o estoque inicial.")]
    public float capacidadeReservaMaxima = -1f;
    [Tooltip("Velocidade de recarga da reserva enquanto o navio está atracado.")]
    public float taxaRecargaNoPier = 120f;
    [Tooltip("Distância aproximada do pier para aguardar quando não precisa atracar.")]
    public float distanciaEsperaPier = 300f;
    public float intervaloBuscaAutomatica = 2f;
    public float intervaloReplanejamentoAlvo = 0.8f;

    [Header("Configurações do Radar Manual")]
    [Tooltip("O radar só alimenta a lista da operação manual. O automático pesquisa a frota inteira.")]
    public float raioRadar = 500f;
    [Tooltip("Layer usada para encontrar alvos próximos no modo manual.")]
    public LayerMask layerNavios = ~0;

    [Header("Configurações do Cano (Mangueira)")]
    public Transform cano;
    public bool pivotNoCentro = true;
    public float comprimentoPadraoCano = 2f;
    public float espessuraCano = 0.4f;
    public Transform pontoOrigemEsquerda;
    public Transform pontoOrigemDireita;
    public string nomePontoEngateAlvo = "CreateEngate";

    [Header("Ajustes de Movimento")]
    [Tooltip("Distância lateral desejada durante o abastecimento. A velocidade é do controlador naval.")]
    public float velocidadeAproximacao = 15f;
    public float distanciaIdealEmparelhamento = 30f;
    public float velocidadeRotacao = 3f;

    [Header("Ajustes de Modelo do Navio")]
    public Vector3 escalaNavio = Vector3.one;
    public float offsetAlturaY = 0f;

    [Header("Menu")]
    public bool mostrarPainelDebug = true;

    private struct NavioRadarInfo
    {
        public Transform raiz;
        public string nome;
        public float distancia;
        public float percentual;
    }

    private readonly List<NavioRadarInfo> listaNaviosRadar = new List<NavioRadarInfo>();
    private ControleUnidade controleUnidade;
    private ControleNavioRealista controleNavio;
    private IdentidadeNaval identidadeNaval;
    private string mensagemStatusMenu = string.Empty;
    private float sumirMensagemTempo;
    private Vector2 scrollPosition;

    private EstadoOperacao estadoOperacao = EstadoOperacao.Parado;
    private bool estaAbastecendo;
    private bool estaAproximando;
    private bool retornoParaAncorar;
    private bool navegacaoParadaDuranteAbastecimento;
    private Transform alvoAtual;
    private CombustivelUnidade combustivelAlvoComp;
    private float combustivelTransferidoAlvo;
    private float tempoInicioAbastecimento;
    private float metaTransferenciaAtual;
    private float capacidadeReservaRuntime;
    private float proximaBuscaAutomatica;
    private float proximoReplanejamento;
    private float proximaTentativaPier;
    private string idOrdemNavalAtual;
    private PierMarinha pierDestino;
    private PierMarinha.VagaDeAtracagem vagaPierAtual;
    private Vector3 destinoPierAtual;
    private float alturaOriginalY;

    public bool ModoAutomaticoAtivo => modoOperacao == ModoOperacaoAbastecimento.Automatico;
    public string EstadoOperacaoAtual => estadoOperacao.ToString();
    public float PercentualReserva => capacidadeReservaRuntime > 0f
        ? Mathf.Clamp01(combustivelTotal / capacidadeReservaRuntime)
        : 0f;

    private void Start()
    {
        alturaOriginalY = transform.position.y;
        controleNavio = GetComponent<ControleNavioRealista>();
        identidadeNaval = GetComponent<IdentidadeNaval>() ?? GetComponentInChildren<IdentidadeNaval>(true);
        ResolverControleUnidade();

        capacidadeReservaRuntime = capacidadeReservaMaxima > 0f
            ? capacidadeReservaMaxima
            : Mathf.Max(1f, combustivelTotal);
        capacidadeReservaMaxima = capacidadeReservaRuntime;

        if (escalaNavio != Vector3.one)
        {
            transform.localScale = escalaNavio;
        }

        if (offsetAlturaY != 0f)
        {
            Vector3 posicao = transform.position;
            posicao.y += offsetAlturaY;
            transform.position = posicao;
            alturaOriginalY = posicao.y;
        }

        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }

        DefinirStatus(modoOperacao == ModoOperacaoAbastecimento.Automatico
            ? "AUTOMÁTICO"
            : "MANUAL", 4f);
        proximaBuscaAutomatica = 0f;
        StartCoroutine(RotinaRadar());
    }

    private void Update()
    {
        ResolverControleUnidade();

        // O Menu Satélite também encaminha o I para este mesmo método. Quando
        // ele está fechado, o abastecedor ainda precisa responder ao atalho
        // diretamente, desde que esteja selecionado. Assim o comportamento
        // não depende de uma segunda interface e continua sendo Manual <->
        // Automático no próprio controlador logístico.
        if (controleUnidade != null
            && controleUnidade.selecionado
            && Input.GetKeyDown(KeyCode.I))
        {
            MenuComandoController menu = FindFirstObjectByType<MenuComandoController>();
            if (menu == null || !menu.MenuAberto)
            {
                AlternarModoOperacao();
                return;
            }
        }

        if (estaAproximando)
        {
            RotinaAproximacao();
            return;
        }

        if (estaAbastecendo)
        {
            RotinaAbastecendo();
            return;
        }

        if (modoOperacao != ModoOperacaoAbastecimento.Automatico)
        {
            return;
        }

        switch (estadoOperacao)
        {
            case EstadoOperacao.IndoPier:
                RotinaRetornoPier();
                break;
            case EstadoOperacao.AguardandoPier:
                RotinaAguardandoPier();
                break;
            case EstadoOperacao.AncorandoPier:
                RotinaAncorandoPier();
                break;
            default:
                ExecutarBuscaAutomatica();
                break;
        }
    }

    private IEnumerator RotinaRadar()
    {
        while (true)
        {
            if (modoOperacao == ModoOperacaoAbastecimento.Manual
                && !estaAbastecendo
                && !estaAproximando
                && estadoOperacao != EstadoOperacao.AncorandoPier)
            {
                AtualizarListaNaviosProximos();
            }

            yield return new WaitForSeconds(2f);
        }
    }

    /// <summary>
    /// Chamado pelo ControleUnidade, que é o mesmo caminho da tecla I.
    /// </summary>
    public string AlternarModoOperacao()
    {
        ModoOperacaoAbastecimento novoModo = modoOperacao == ModoOperacaoAbastecimento.Manual
            ? ModoOperacaoAbastecimento.Automatico
            : ModoOperacaoAbastecimento.Manual;
        modoOperacao = novoModo;

        if (novoModo == ModoOperacaoAbastecimento.Manual)
        {
            if (estaAproximando || estaAbastecendo || estadoOperacao == EstadoOperacao.IndoPier)
            {
                CancelarOperacao("MANUAL");
            }
            else if (estadoOperacao == EstadoOperacao.AguardandoPier)
            {
                PararNavegacao();
                estadoOperacao = EstadoOperacao.Parado;
            }

            DefinirStatus("MANUAL", 4f);
            return "MANUAL";
        }

        if (estadoOperacao == EstadoOperacao.AguardandoPier)
        {
            estadoOperacao = EstadoOperacao.Parado;
        }
        proximaBuscaAutomatica = 0f;
        DefinirStatus("AUTOMÁTICO", 4f);
        return "AUTOMÁTICO";
    }

    private void ExecutarBuscaAutomatica()
    {
        if (Time.time < proximaBuscaAutomatica)
        {
            return;
        }

        proximaBuscaAutomatica = Time.time + Mathf.Max(0.25f, intervaloBuscaAutomatica);
        if (TentarSelecionarAlvoAutomatico())
        {
            return;
        }

        // Não há alvo abaixo de 60%. A reserva própria decide se deve atracar
        // para recarregar ou aguardar a 300 m do pier.
        if (PercentualReserva < limiteRetornoAoPier)
        {
            IniciarRetornoAoPier(true);
        }
        else if (estadoOperacao == EstadoOperacao.Parado)
        {
            IniciarRetornoAoPier(false);
        }
    }

    private bool TentarSelecionarAlvoAutomatico()
    {
        if (combustivelTotal <= 0.01f)
        {
            return false;
        }

        CombustivelUnidade[] combustiveis = FindObjectsByType<CombustivelUnidade>(FindObjectsSortMode.None);
        Transform melhorRaiz = null;
        CombustivelUnidade melhorCombustivel = null;
        float menorPercentual = float.PositiveInfinity;
        float menorDistancia = float.PositiveInfinity;

        for (int i = 0; i < combustiveis.Length; i++)
        {
            CombustivelUnidade comb = combustiveis[i];
            if (!TryObterNavioMilitarAbastecivel(comb, out Transform raiz))
            {
                continue;
            }

            float percentual = comb.Percentual;
            if (percentual >= limiteAtendimentoAutomatico)
            {
                continue;
            }

            float distancia = DistanciaHorizontal(transform.position, raiz.position);
            if (percentual < menorPercentual - 0.0001f
                || (Mathf.Abs(percentual - menorPercentual) <= 0.0001f && distancia < menorDistancia))
            {
                menorPercentual = percentual;
                menorDistancia = distancia;
                melhorRaiz = raiz;
                melhorCombustivel = comb;
            }
        }

        if (melhorRaiz == null)
        {
            return false;
        }

        if (estadoOperacao == EstadoOperacao.AguardandoPier)
        {
            PararNavegacao();
        }

        return IniciarProcessoAbastecimento(melhorRaiz, melhorCombustivel, true);
    }

    private void AtualizarListaNaviosProximos()
    {
        listaNaviosRadar.Clear();
        LayerMask mascara = layerNavios.value == 0 ? ~0 : layerNavios;
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(1f, raioRadar), mascara);
        HashSet<Transform> adicionados = new HashSet<Transform>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            CombustivelUnidade comb = hit.GetComponentInParent<CombustivelUnidade>()
                ?? hit.GetComponentInChildren<CombustivelUnidade>(true);
            if (comb == null || comb.GetComponentInParent<Estaleiro>() != null)
            {
                continue;
            }

            if (!TryObterNavioMilitarAbastecivel(comb, out Transform raiz)
                || !adicionados.Add(raiz))
            {
                continue;
            }

            listaNaviosRadar.Add(new NavioRadarInfo
            {
                raiz = raiz,
                nome = raiz.name,
                distancia = DistanciaHorizontal(transform.position, raiz.position),
                percentual = comb.Percentual
            });
        }

        listaNaviosRadar.Sort(delegate (NavioRadarInfo a, NavioRadarInfo b)
        {
            return a.distancia.CompareTo(b.distancia);
        });
    }

    private bool TryObterNavioMilitarAbastecivel(CombustivelUnidade comb, out Transform raiz)
    {
        raiz = null;
        if (comb == null || !comb.usaCombustivel || comb.Capacidade <= 0f)
        {
            return false;
        }

        ControleNavioRealista executorNaval = comb.GetComponentInParent<ControleNavioRealista>()
            ?? comb.GetComponentInChildren<ControleNavioRealista>(true);
        ControleSubmarino executorSubmarino = comb.GetComponentInParent<ControleSubmarino>()
            ?? comb.GetComponentInChildren<ControleSubmarino>(true);
        GerenciadorPortaAvioes executorPortaAvioes = comb.GetComponentInParent<GerenciadorPortaAvioes>()
            ?? comb.GetComponentInChildren<GerenciadorPortaAvioes>(true);

        if (executorNaval == null && executorSubmarino == null && executorPortaAvioes == null)
        {
            return false;
        }

        raiz = executorNaval != null
            ? executorNaval.transform
            : executorSubmarino != null
                ? executorSubmarino.transform
                : executorPortaAvioes.transform;

        if (raiz == transform || raiz.root == transform.root)
        {
            return false;
        }

        if (NavalPlacementResolver.IsLogisticsVessel(raiz.gameObject)
            || raiz.GetComponentInParent<Estaleiro>() != null)
        {
            return false;
        }

        IdentidadeUnidade identidadeAlvo = raiz.GetComponent<IdentidadeUnidade>()
            ?? raiz.GetComponentInParent<IdentidadeUnidade>()
            ?? raiz.GetComponentInChildren<IdentidadeUnidade>(true);
        int meuTime = ResolverTeamId(gameObject);
        int timeAlvo = identidadeAlvo != null ? identidadeAlvo.teamID : 0;
        if (meuTime > 0 && timeAlvo > 0 && meuTime != timeAlvo)
        {
            return false;
        }

        return comb.classe == ClasseCombustivelUnidade.Naval
            || identidadeAlvo != null && identidadeAlvo.tipoUnidade == TipoUnidade.Naval;
    }

    private bool IniciarProcessoAbastecimento(Transform alvo, CombustivelUnidade combustivel, bool automatico)
    {
        if (alvo == null || estaAproximando || estaAbastecendo)
        {
            return false;
        }

        if (combustivel == null)
        {
            combustivel = alvo.GetComponent<CombustivelUnidade>()
                ?? alvo.GetComponentInChildren<CombustivelUnidade>(true);
        }

        if (!TryObterNavioMilitarAbastecivel(combustivel, out Transform raiz)
            || raiz == null)
        {
            DefinirStatus("ALVO NÃO É NAVIO MILITAR", 3f);
            return false;
        }

        alvoAtual = raiz;
        combustivelAlvoComp = combustivel;
        estaAproximando = true;
        estaAbastecendo = false;
        estadoOperacao = EstadoOperacao.IndoAlvo;
        navegacaoParadaDuranteAbastecimento = false;
        idOrdemNavalAtual = "abastecimento:" + GetInstanceID() + ":" + alvoAtual.GetInstanceID();
        listaNaviosRadar.Clear();
        DefinirStatus(automatico ? "AUTOMÁTICO: indo ao menor combustível" : "Aproximando...", 10f);
        proximoReplanejamento = 0f;

        if (!EnviarDestinoNaval(ObterPosicaoEmparelhamento()))
        {
            CancelarOperacao("ROTA MARÍTIMA RECUSADA");
            return false;
        }

        return true;
    }

    private void RotinaAproximacao()
    {
        if (!AlvoAindaValido())
        {
            CancelarOperacao("ALVO INDISPONÍVEL");
            return;
        }

        Vector3 pontoEmparelhamento = ObterPosicaoEmparelhamento();
        float distancia = DistanciaHorizontal(transform.position, pontoEmparelhamento);
        if (distancia <= Mathf.Max(7f, distanciaIdealEmparelhamento * 0.35f))
        {
            estaAproximando = false;
            PararNavegacao();
            IniciarAbastecimento();
            return;
        }

        if (Time.time >= proximoReplanejamento)
        {
            proximoReplanejamento = Time.time + Mathf.Max(0.25f, intervaloReplanejamentoAlvo);
            if (!EnviarDestinoNaval(pontoEmparelhamento))
            {
                if (!NavalPlacementResolver.TryResolveNearestWaterPoint(pontoEmparelhamento, 120f, out Vector3 pontoSeguro)
                    || !EnviarDestinoNaval(pontoSeguro))
                {
                    CancelarOperacao("ROTA MARÍTIMA RECUSADA");
                }
            }
        }
    }

    private void IniciarAbastecimento()
    {
        if (!AlvoAindaValido())
        {
            CancelarOperacao("ALVO INDISPONÍVEL");
            return;
        }

        combustivelAlvoComp = combustivelAlvoComp
            ?? alvoAtual.GetComponent<CombustivelUnidade>()
            ?? alvoAtual.GetComponentInChildren<CombustivelUnidade>(true);
        if (combustivelAlvoComp == null)
        {
            CancelarOperacao("ALVO SEM COMBUSTÍVEL");
            return;
        }

        metaTransferenciaAtual = combustivelAlvoComp.Capacidade - combustivelAlvoComp.CombustivelAtual;
        if (metaTransferenciaAtual <= 0.01f)
        {
            FinalizarAbastecimento("ALVO JÁ ABASTECIDO");
            return;
        }

        estaAbastecendo = true;
        estadoOperacao = EstadoOperacao.Abastecendo;
        combustivelTransferidoAlvo = 0f;
        tempoInicioAbastecimento = Time.time;
        navegacaoParadaDuranteAbastecimento = false;
        DefinirStatus("Abastecendo...", 10f);

        if (cano != null)
        {
            cano.gameObject.SetActive(true);
            AtualizarPosicaoCano(ResolverOrigemCano());
        }
    }

    private void RotinaAbastecendo()
    {
        if (!AlvoAindaValido() || combustivelAlvoComp == null)
        {
            FinalizarAbastecimento("ALVO INDISPONÍVEL");
            return;
        }

        Vector3 pontoEmparelhamento = ObterPosicaoEmparelhamento();
        float distancia = DistanciaHorizontal(transform.position, pontoEmparelhamento);
        float limiteReaproximacao = Mathf.Max(18f, distanciaIdealEmparelhamento * 1.75f);

        // O abastecedor acompanha um alvo que se move, mas nunca arrasta o
        // Transform diretamente. O novo destino passa pelo controlador naval.
        if (distancia > limiteReaproximacao)
        {
            navegacaoParadaDuranteAbastecimento = false;
            if (Time.time >= proximoReplanejamento)
            {
                proximoReplanejamento = Time.time + Mathf.Max(0.25f, intervaloReplanejamentoAlvo);
                EnviarDestinoNaval(pontoEmparelhamento);
            }
            return;
        }

        if (distancia <= Mathf.Max(12f, distanciaIdealEmparelhamento * 0.65f)
            && !navegacaoParadaDuranteAbastecimento)
        {
            PararNavegacao();
            navegacaoParadaDuranteAbastecimento = true;
        }

        AtualizarPosicaoCano(ResolverOrigemCano());

        float quantidade = Mathf.Max(0f, taxaAbastecimento) * Time.deltaTime;
        if (combustivelTotal <= 0.01f || combustivelTransferidoAlvo >= metaTransferenciaAtual)
        {
            FinalizarAbastecimento(combustivelTotal <= 0.01f ? "RESERVA VAZIA" : "ABASTECIMENTO COMPLETO");
            return;
        }

        float transferencia = Mathf.Min(
            quantidade,
            combustivelTotal,
            metaTransferenciaAtual - combustivelTransferidoAlvo);
        if (transferencia <= 0f)
        {
            FinalizarAbastecimento("ABASTECIMENTO COMPLETO");
            return;
        }

        combustivelTotal -= transferencia;
        combustivelTransferidoAlvo += transferencia;
        combustivelAlvoComp.Abastecer(transferencia);

        if (combustivelTransferidoAlvo >= metaTransferenciaAtual - 0.01f
            || combustivelTotal <= 0.01f)
        {
            FinalizarAbastecimento(combustivelTotal <= 0.01f ? "RESERVA VAZIA" : "ABASTECIMENTO COMPLETO");
        }
    }

    private void FinalizarAbastecimento(string mensagem)
    {
        estaAbastecendo = false;
        estaAproximando = false;
        alvoAtual = null;
        combustivelAlvoComp = null;
        navegacaoParadaDuranteAbastecimento = false;
        idOrdemNavalAtual = null;
        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }

        estadoOperacao = EstadoOperacao.Parado;
        proximaBuscaAutomatica = Time.time + 0.15f;
        DefinirStatus(mensagem, 4f);
    }

    private void CancelarOperacao(string mensagem)
    {
        PararNavegacao();
        estaAbastecendo = false;
        estaAproximando = false;
        alvoAtual = null;
        combustivelAlvoComp = null;
        navegacaoParadaDuranteAbastecimento = false;
        idOrdemNavalAtual = null;
        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }
        estadoOperacao = EstadoOperacao.Parado;
        DefinirStatus(mensagem, 4f);
    }

    private void IniciarRetornoAoPier(bool ancorar)
    {
        if (estadoOperacao == EstadoOperacao.IndoPier
            || estadoOperacao == EstadoOperacao.AncorandoPier)
        {
            return;
        }

        pierDestino = EncontrarPierProprio();
        if (pierDestino == null)
        {
            estadoOperacao = EstadoOperacao.AguardandoPier;
            DefinirStatus("Nenhum pier próprio encontrado", 5f);
            return;
        }

        retornoParaAncorar = ancorar;
        vagaPierAtual = null;
        destinoPierAtual = ancorar
            ? ObterPontoAproximacaoPier(pierDestino)
            : ObterPontoEsperaPier(pierDestino);
        estadoOperacao = EstadoOperacao.IndoPier;
        idOrdemNavalAtual = "abastecimento:pier:" + GetInstanceID() + ":" + Time.frameCount;
        proximoReplanejamento = 0f;
        DefinirStatus(ancorar ? "Reserva baixa: retornando ao pier" : "Sem alvos: aguardando a 300 m do pier", 8f);

        if (!EnviarDestinoNaval(destinoPierAtual)
            && NavalPlacementResolver.TryResolveNearestWaterPoint(destinoPierAtual, 150f, out Vector3 pontoSeguro))
        {
            destinoPierAtual = pontoSeguro;
            EnviarDestinoNaval(destinoPierAtual);
        }
    }

    private void RotinaRetornoPier()
    {
        if (pierDestino == null)
        {
            estadoOperacao = EstadoOperacao.Parado;
            return;
        }

        if (retornoParaAncorar)
        {
            if (vagaPierAtual == null)
            {
                vagaPierAtual = EncontrarVagaLivre(pierDestino);
            }

            if (vagaPierAtual != null)
            {
                Vector3 pontoAproximacao = vagaPierAtual.pontoDeManobra != null
                    ? vagaPierAtual.pontoDeManobra.position
                    : vagaPierAtual.pontoDeAtracagem.position;
                destinoPierAtual = pontoAproximacao;
            }
        }

        float distancia = DistanciaHorizontal(transform.position, destinoPierAtual);
        if (retornoParaAncorar && vagaPierAtual != null && distancia <= 140f)
        {
            if (TentarAtracarNoPier())
            {
                return;
            }
        }

        if (!retornoParaAncorar && distancia <= 25f)
        {
            PararNavegacao();
            estadoOperacao = EstadoOperacao.AguardandoPier;
            DefinirStatus("Aguardando a 300 m do pier", 5f);
            return;
        }

        if (Time.time >= proximoReplanejamento)
        {
            proximoReplanejamento = Time.time + Mathf.Max(0.25f, intervaloReplanejamentoAlvo);
            if (!EnviarDestinoNaval(destinoPierAtual)
                && NavalPlacementResolver.TryResolveNearestWaterPoint(destinoPierAtual, 150f, out Vector3 pontoSeguro))
            {
                destinoPierAtual = pontoSeguro;
                EnviarDestinoNaval(destinoPierAtual);
            }
        }
    }

    private void RotinaAguardandoPier()
    {
        if (TentarSelecionarAlvoAutomatico())
        {
            return;
        }

        if (PercentualReserva < limiteRetornoAoPier && Time.time >= proximaTentativaPier)
        {
            proximaTentativaPier = Time.time + 5f;
            IniciarRetornoAoPier(true);
        }
    }

    private bool TentarAtracarNoPier()
    {
        if (pierDestino == null || vagaPierAtual == null || identidadeNaval == null)
        {
            return false;
        }

        if (!vagaPierAtual.EstaLivre())
        {
            vagaPierAtual = null;
            destinoPierAtual = ObterPontoEsperaPier(pierDestino);
            retornoParaAncorar = false;
            DefinirStatus("Vaga ocupada: aguardando a 300 m do pier", 5f);
            return false;
        }

        PararNavegacao();
        estadoOperacao = EstadoOperacao.AncorandoPier;
        DefinirStatus("Atracando para recarregar a reserva", 8f);
        pierDestino.AtribuirVaga(vagaPierAtual, identidadeNaval);
        return true;
    }

    private void RotinaAncorandoPier()
    {
        if (pierDestino == null || vagaPierAtual == null)
        {
            estadoOperacao = EstadoOperacao.Parado;
            return;
        }

        if (vagaPierAtual.navioOcupante == null)
        {
            vagaPierAtual = null;
            estadoOperacao = EstadoOperacao.Parado;
            proximaBuscaAutomatica = Time.time + 0.5f;
            return;
        }

        if (!vagaPierAtual.atracagemCompleta)
        {
            return;
        }

        combustivelTotal = Mathf.MoveTowards(
            combustivelTotal,
            capacidadeReservaRuntime,
            Mathf.Max(0f, taxaRecargaNoPier) * Time.deltaTime);
        DefinirStatus("Recarregando no pier", 1f);

        if (combustivelTotal >= capacidadeReservaRuntime - 0.01f)
        {
            pierDestino.LiberarVaga(vagaPierAtual, pierDestino.saida_petro);
            vagaPierAtual = null;
            estadoOperacao = EstadoOperacao.Parado;
            proximaBuscaAutomatica = Time.time + 0.5f;
            DefinirStatus("Reserva recarregada", 4f);
        }
    }

    private bool EnviarDestinoNaval(Vector3 destino)
    {
        if (controleNavio == null || !isActiveAndEnabled)
        {
            return false;
        }

        destino.y = NavalPlacementResolver.ResolveSeaLevel();
        return controleNavio.DefinirDestino(destino, idOrdemNavalAtual);
    }

    private void PararNavegacao()
    {
        ResolverControleUnidade();
        if (controleUnidade != null)
        {
            controleUnidade.EmitirOrdemParar();
        }

        // EmitirOrdemParar limpa a ordem oficial; este método também limpa a
        // rota física quando o navio está no modo aquático direto e o
        // NavMeshAgent está desligado.
        if (controleNavio != null)
        {
            controleNavio.PararPorFaltaDeCombustivel();
        }
    }

    private Vector3 ObterPosicaoEmparelhamento()
    {
        if (alvoAtual == null)
        {
            return transform.position;
        }

        Vector3 lateral = alvoAtual.right;
        lateral.y = 0f;
        if (lateral.sqrMagnitude < 0.001f)
        {
            lateral = Vector3.right;
        }
        lateral.Normalize();

        Vector3 direita = alvoAtual.position + lateral * Mathf.Max(8f, distanciaIdealEmparelhamento);
        Vector3 esquerda = alvoAtual.position - lateral * Mathf.Max(8f, distanciaIdealEmparelhamento);
        Vector3 ponto = DistanciaHorizontal(transform.position, direita)
            < DistanciaHorizontal(transform.position, esquerda)
            ? direita
            : esquerda;
        ponto.y = NavalPlacementResolver.ResolveSeaLevel();

        if (!NavalPlacementResolver.IsWaterAtPosition(ponto)
            && NavalPlacementResolver.TryResolveNearestWaterPoint(ponto, 120f, out Vector3 pontoSeguro))
        {
            ponto = pontoSeguro;
        }

        return ponto;
    }

    private Vector3 ObterPontoAproximacaoPier(PierMarinha pier)
    {
        Transform ponto = null;
        if (pier != null && pier.vagasDisponiveis != null)
        {
            for (int i = 0; i < pier.vagasDisponiveis.Count; i++)
            {
                PierMarinha.VagaDeAtracagem vaga = pier.vagasDisponiveis[i];
                if (vaga == null || !vaga.EstaLivre()) continue;
                ponto = vaga.pontoDeManobra != null ? vaga.pontoDeManobra : vaga.pontoDeAtracagem;
                if (ponto != null) break;
            }
        }

        if (ponto == null && pier != null)
        {
            ponto = pier.saida_petro != null
                ? pier.saida_petro
                : pier.Atraca_petro;
        }

        Vector3 destino = ponto != null ? ponto.position : pier.transform.position;
        destino.y = NavalPlacementResolver.ResolveSeaLevel();
        return destino;
    }

    private Vector3 ObterPontoEsperaPier(PierMarinha pier)
    {
        Vector3 direcao = transform.position - pier.transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f)
        {
            direcao = pier.transform.forward;
            direcao.y = 0f;
        }
        if (direcao.sqrMagnitude < 0.01f)
        {
            direcao = Vector3.forward;
        }
        direcao.Normalize();

        Vector3 solicitado = pier.transform.position + direcao * Mathf.Max(40f, distanciaEsperaPier);
        solicitado.y = NavalPlacementResolver.ResolveSeaLevel();
        if (NavalPlacementResolver.TryResolveNearestWaterPoint(solicitado, 100f, out Vector3 pontoAgua))
        {
            return pontoAgua;
        }

        if (pier.saida_petro != null)
        {
            Vector3 saida = pier.saida_petro.position;
            saida.y = NavalPlacementResolver.ResolveSeaLevel();
            return saida;
        }

        return solicitado;
    }

    private PierMarinha EncontrarPierProprio()
    {
        PierMarinha[] piers = FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
        int meuTime = ResolverTeamId(gameObject);
        PierMarinha melhor = null;
        float menorDistancia = float.PositiveInfinity;

        for (int i = 0; i < piers.Length; i++)
        {
            PierMarinha pier = piers[i];
            if (pier == null || !pier.gameObject.activeInHierarchy)
            {
                continue;
            }

            int timePier = pier.OwnerTeamId;
            if (meuTime > 0 && timePier > 0 && meuTime != timePier)
            {
                continue;
            }

            float distancia = DistanciaHorizontal(transform.position, pier.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhor = pier;
            }
        }

        return melhor;
    }

    private PierMarinha.VagaDeAtracagem EncontrarVagaLivre(PierMarinha pier)
    {
        if (pier == null || identidadeNaval == null || pier.vagasDisponiveis == null)
        {
            return null;
        }

        for (int i = 0; i < pier.vagasDisponiveis.Count; i++)
        {
            PierMarinha.VagaDeAtracagem vaga = pier.vagasDisponiveis[i];
            if (vaga == null || vaga.categoriaAceita != identidadeNaval.categoriaNavio)
            {
                continue;
            }

            if (vaga.EstaLivre() && vaga.pontoDeAtracagem != null)
            {
                return vaga;
            }
        }

        return null;
    }

    private bool AlvoAindaValido()
    {
        return alvoAtual != null
            && alvoAtual.gameObject.activeInHierarchy
            && combustivelAlvoComp != null
            && combustivelAlvoComp.gameObject.activeInHierarchy;
    }

    private Transform ResolverOrigemCano()
    {
        if (pontoOrigemEsquerda == null && pontoOrigemDireita == null)
        {
            return transform;
        }

        if (pontoOrigemEsquerda == null) return pontoOrigemDireita;
        if (pontoOrigemDireita == null) return pontoOrigemEsquerda;

        return DistanciaHorizontal(pontoOrigemEsquerda.position, alvoAtual.position)
            < DistanciaHorizontal(pontoOrigemDireita.position, alvoAtual.position)
            ? pontoOrigemEsquerda
            : pontoOrigemDireita;
    }

    private void AtualizarPosicaoCano(Transform origem)
    {
        if (cano == null || origem == null || alvoAtual == null)
        {
            return;
        }

        Transform pontoDestino = alvoAtual.Find(nomePontoEngateAlvo);
        Vector3 destino = pontoDestino != null ? pontoDestino.position : alvoAtual.position;
        float distancia = Vector3.Distance(origem.position, destino);

        cano.position = pivotNoCentro
            ? (origem.position + destino) * 0.5f
            : origem.position;
        cano.LookAt(destino);

        Vector3 escala = cano.localScale;
        escala.x = espessuraCano;
        escala.y = espessuraCano;
        escala.z = comprimentoPadraoCano > 0.01f ? distancia / comprimentoPadraoCano : 1f;
        cano.localScale = escala;
    }

    private void ResolverControleUnidade()
    {
        if (controleUnidade == null)
        {
            controleUnidade = GetComponent<ControleUnidade>()
                ?? GetComponentInParent<ControleUnidade>()
                ?? GetComponentInChildren<ControleUnidade>(true);
        }
    }

    private static int ResolverTeamId(GameObject objeto)
    {
        if (objeto == null) return 0;
        IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>()
            ?? objeto.GetComponentInParent<IdentidadeUnidade>()
            ?? objeto.GetComponentInChildren<IdentidadeUnidade>(true);
        return identidade != null ? identidade.teamID : 0;
    }

    private static float DistanciaHorizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void DefinirStatus(string mensagem, float duracao)
    {
        mensagemStatusMenu = mensagem ?? string.Empty;
        sumirMensagemTempo = Time.time + Mathf.Max(0.1f, duracao);
    }

    private void OnGUI()
    {
        if (!mostrarPainelDebug)
        {
            return;
        }

        ResolverControleUnidade();
        if (controleUnidade == null || !controleUnidade.selecionado)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20f, Screen.height - 315f, 285f, 295f), GUI.skin.box);
        GUIStyle titulo = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUILayout.Label("NAVIO DE ABASTECIMENTO", titulo);
        GUILayout.Label("Modo: " + (ModoAutomaticoAtivo ? "AUTOMÁTICO" : "MANUAL"), titulo);
        GUILayout.Label("Estado: " + estadoOperacao);
        GUILayout.Label("Reserva: " + combustivelTotal.ToString("F0") + " L (" + (PercentualReserva * 100f).ToString("F0") + "%)");

        if (!string.IsNullOrEmpty(mensagemStatusMenu) && Time.time < sumirMensagemTempo)
        {
            GUILayout.Label("Status: " + mensagemStatusMenu, titulo);
        }

        if (estaAbastecendo)
        {
            GUILayout.Label("Alvo: " + (alvoAtual != null ? alvoAtual.name : "-"));
            GUILayout.Label("Transferido: " + combustivelTransferidoAlvo.ToString("F0") + " / " + metaTransferenciaAtual.ToString("F0") + " L");
            float progresso = metaTransferenciaAtual > 0f
                ? Mathf.Clamp01(combustivelTransferidoAlvo / metaTransferenciaAtual)
                : 0f;
            Rect barra = GUILayoutUtility.GetRect(245f, 15f);
            GUI.Box(barra, GUIContent.none);
            Color cor = GUI.color;
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(barra.x, barra.y, barra.width * progresso, barra.height), Texture2D.whiteTexture);
            GUI.color = cor;
            if (GUILayout.Button("Cancelar operação", GUILayout.Height(28f)))
            {
                CancelarOperacao("CANCELADO");
            }
        }
        else if (modoOperacao == ModoOperacaoAbastecimento.Manual)
        {
            GUILayout.Label("Navios militares próximos:", titulo);
            if (listaNaviosRadar.Count == 0)
            {
                GUILayout.Label("Nenhum navio detectado.", GUILayout.Height(30f));
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(120f));
                for (int i = 0; i < listaNaviosRadar.Count; i++)
                {
                    NavioRadarInfo info = listaNaviosRadar[i];
                    if (info.raiz == null) continue;
                    string texto = info.nome + "\n" + info.distancia.ToString("F0") + " m - " + (info.percentual * 100f).ToString("F0") + "%";
                    if (GUILayout.Button(texto, GUILayout.Height(38f)))
                    {
                        IniciarProcessoAbastecimento(info.raiz, null, false);
                    }
                }
                GUILayout.EndScrollView();
            }
        }
        else if (alvoAtual != null)
        {
            GUILayout.Label("Alvo automático: " + alvoAtual.name);
        }

        GUILayout.EndArea();
    }
}
