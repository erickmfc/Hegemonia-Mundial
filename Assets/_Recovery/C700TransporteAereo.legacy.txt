using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class C700TransporteAereo : MonoBehaviour
{
    public enum EstadoC700
    {
        Solo,
        Taxiando,
        Decolando,
        EmVoo,
        Aproximando,
        Pousando
    }

    [System.Serializable]
    private class SlotCarga
    {
        public Transform ancora;
        public GameObject unidade;
    }

    [System.Serializable]
    public class EntradaManifesto
    {
        public string nome = "Soldados";
        public TipoUnidade tipoUnidade = TipoUnidade.Infantaria;
        public GameObject prefabDesembarque;
        public int quantidade = 0;
        public int ajusteRapido = 10;
        public int ajustePesado = 50;
        public int quantidadeMaxima = 200;
    }

    [Header("Estado")]
    public EstadoC700 estadoAtual = EstadoC700.Solo;

    private void DefinirEstado(EstadoC700 novoEstado)
    {
        estadoAtual = novoEstado;
        bool motorLigado = novoEstado != EstadoC700.Solo;
        AudioRuntime.DefinirMotorAereo(gameObject, motorLigado);
    }
    public bool debugLogs = false;

    [Header("Aeroporto")]
    public GerenciadorAeroporto aeroportoOrigem;
    public Transform pontoEstacionamentoPreferencial;
    public string nomePontoParadaGrande = "Parada_grande";

    [Header("Taxi no solo")]
    public float velocidadeTaxi = 22f;
    public float aceleracaoSolo = 20f;
    public float desaceleracaoSolo = 28f;
    public float giroSolo = 90f;
    public float raioChegadaSolo = 3.5f;
    public float distanciaFreioSolo = 28f;
    public float offsetAlturaSolo = 0.2f;

    [Header("Voo e pouso")]
    public float velocidadeDecolagem = 42f;
    public float velocidadeCruzeiro = 130f;
    public float aceleracaoVoo = 42f;
    public float giroVoo = 65f;
    public float altitudeCruzeiro = 85f;
    public float distanciaAproximacao = 220f;
    public float distanciaDescida = 90f;
    public float distanciaRolagem = 45f;
    public float alturaToqueSolo = 1.6f;
    public float distanciaCorridaDecolagem = 90f;
    [Range(0.12f, 0.50f)] public float reservaRetornoPercentual = 0.30f;

    [Header("Seguranca de navegacao aerea")]
    [Min(5f)] public float timeoutPorPontoAereo = 45f;
    [Min(0.05f)] public float deslocamentoMinimoAereo = 0.25f;

    [Header("Visual")]
    public Transform modeloVisual;
    public float bankMaximo = 28f;
    public float pitchMaximo = 14f;
    public float suavizacaoVisual = 3.5f;
    [Tooltip("Marque se o modelo 3D foi importado invertido (de costas)")]
    public bool modeloInvertido180 = false;

    [Header("Carga")]
    public Transform[] pontosCarga;
    public int capacidadeMaxima = 8;
    public bool ocultarCargaInterna = true;
    public float raioBuscaCarga = 45f;
    public float atrasoEntreEmbarques = 0.25f;
    public float distanciaDesembarque = 18f;
    public float raioBloqueioPortaAvioes = 25f;

    [Header("Manifesto configuravel")]
    public List<EntradaManifesto> manifestoConfigurado = new List<EntradaManifesto>();
    public float espacamentoManifestoInfantaria = 3.2f;
    public float espacamentoManifestoVeiculos = 7.5f;
    public int spawnsPorQuadroManifesto = 8;
    public float pausaEntreLotesManifesto = 0.02f;

    [Header("Combate")]
    public bool desabilitarArmasAoIniciar = true;

    [Header("Marcadores de Missao")]
    public float alturaMarcadorMissao = 18f;
    public float escalaMarcadorMissao = 2.8f;
    public float espessuraLinhaMissao = 0.22f;
    public Color corPousoMissao = new Color(0.15f, 0.95f, 1f, 0.45f);
    public Color corParadaMissao = new Color(1f, 0.85f, 0.2f, 0.45f);

    private readonly List<SlotCarga> slots = new List<SlotCarga>();
    private readonly List<ControleUnidade> controlesRegistradosCarga = new List<ControleUnidade>(128);
    private ControleUnidade controleUnidade;
    private ControleAviao controleAviaoLegado;
    private Rigidbody rb;
    private Coroutine rotinaMovimento;
    private Coroutine rotinaCarga;
    private bool menuCargaAberto;
    private bool aguardandoDestinoAereo;
    private bool prontoParaDecolarNaPista;
    private float tempoMensagemOrdem;
    private string mensagemOrdem = string.Empty;
    private float velocidadeSoloAtual;
    private float velocidadeAereaAtual;
    private Vector3 ultimaPosicao;
    private Quaternion rotacaoModeloBase = Quaternion.identity;
    private float rollVisualAtual;
    private float pitchVisualAtual;
    private Coroutine rotinaDesembarqueManifesto;
    private Vector2 scrollMenuCarga;
    private Vector3 destinoVisualAtual;
    private bool temDestinoVisual;
    private Vector3 destinoMissaoProgramado;
    private bool temDestinoMissaoProgramado;
    private bool retornoAutomaticoEmAndamento;
    private GameObject marcadorPousoMissao;
    private GameObject marcadorParadaMissao;
    private LineRenderer linhaMissao;

    private bool EstaSelecionado => controleUnidade != null && controleUnidade.selecionado;
    public bool EstaNoSolo => estadoAtual == EstadoC700.Solo || estadoAtual == EstadoC700.Taxiando;
    public bool AguardandoDestinoAereo => aguardandoDestinoAereo;
    public List<EntradaManifesto> ManifestoConfigurado => manifestoConfigurado;
    public int QuantidadeCargaAtual => QuantidadeCargas();
    public int CapacidadeCargaAtual => slots.Count;
    public int QuantidadeManifestoTotal => CalcularQuantidadeManifestoTotal();
    public bool TemDestinoVisual => temDestinoVisual;
    public Vector3 DestinoVisualAtual => destinoVisualAtual;

    private void LogDebug(string mensagem)
    {
        if (debugLogs)
        {
            Debug.Log("[C700] " + mensagem);
        }
    }

    private void Awake()
    {
        controleUnidade = GetComponent<ControleUnidade>();
        controleAviaoLegado = GetComponent<ControleAviao>();
        rb = GetComponent<Rigidbody>();

        // O C700 possui uma máquina de voo própria. O prefab antigo também
        // carregava ControleAviao, o que permitia que dois Updates alterassem
        // posição/rotação no mesmo frame. Isso causava voo de lado, perda do
        // destino e pousos que nunca terminavam.
        if (controleAviaoLegado != null)
        {
            controleAviaoLegado.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // O prefab C700 tem as rodas como filhos do objeto raiz e o mesh da
        // aeronave no próprio raiz. Nunca use a primeira criança como visual:
        // isso fazia "Rodas frente" receber banking/pitch durante o voo.
        // Quando houver um modelo visual separado, ele deve ser ligado no
        // Inspector explicitamente.
        if (modeloVisual != null)
        {
            rotacaoModeloBase = modeloVisual.localRotation;
        }

        PrepararSlotsDeCarga();
        GarantirManifestoPadrao();

        if (desabilitarArmasAoIniciar)
        {
            DesabilitarComponentesDeCombate();
        }
    }

    private void Start()
    {
        AderirAoSolo();
        ultimaPosicao = transform.position;
    }

    private void OnDisable()
    {
        LimparIndicadoresMissao();
    }

    private void OnDestroy()
    {
        LimparIndicadoresMissao();
    }

    private void Update()
    {
        // Alguns gerenciadores antigos reativam o controlador genérico ao
        // registrar a aeronave. Reafirma aqui a autoridade única do C700.
        if (controleAviaoLegado != null && controleAviaoLegado.enabled)
        {
            controleAviaoLegado.enabled = false;
        }

        AvaliarRetornoSeguro();

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            AtualizarVisualVoo();
            FixarCargaVisivel();
            return;
        }

        AtualizarVisualVoo();
        FixarCargaVisivel();

        bool selecionado = EstaSelecionado;
        if (!selecionado)
        {
            if (menuCargaAberto)
            {
                menuCargaAberto = false;
            }
            return;
        }

        if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;

        if (Input.GetKeyDown(KeyCode.O))
        {
            menuCargaAberto = !menuCargaAberto;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (!EstaNoSolo)
            {
                MostrarMensagem("Retornando ao aeroporto.");
                OrdenarRetornoAoAeroporto();
            }
            else if (aguardandoDestinoAereo)
            {
                aguardandoDestinoAereo = false;
                MostrarMensagem("Ordem aerea cancelada. Voltando ao aeroporto.");
                OrdenarRetornoAoAeroporto();
            }
            else
            {
                aguardandoDestinoAereo = true;
                menuCargaAberto = false;
                MostrarMensagem("Modo aereo ativo. Clique com o botao direito no destino.");
                LogDebug("Modo de voo armado. Proximo clique direito manda decolar.");
            }
        }

        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.U))
        {
            PuxarUnidadesProximas();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            DesembarcarTudo();
        }
    }

    private void OnGUI()
    {
        if (!menuCargaAberto || !EstaSelecionado)
        {
            return;
        }

        Rect area = new Rect(Screen.width - 430f - (Screen.width * 0.20f), Screen.height * 0.08f, 400f, 650f);
        GUI.Box(area, "C700 - Transporte");
        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 28f, area.width - 24f, area.height - 40f));
        scrollMenuCarga = GUILayout.BeginScrollView(scrollMenuCarga);

        GUILayout.Label("Estado: " + estadoAtual);
        GUILayout.Label("Carga real: " + QuantidadeCargas() + "/" + slots.Count);
        GUILayout.Label("Manifesto: " + QuantidadeManifestoTotal);
        GUILayout.Label("Modo: " + (aguardandoDestinoAereo ? "aguardando destino" : temDestinoMissaoProgramado ? "missao programada" : EstaNoSolo ? "base/patio" : "em voo"));

        if (temDestinoVisual)
        {
            GUILayout.Label(string.Format("Destino: X {0:0} / Z {1:0}", destinoVisualAtual.x, destinoVisualAtual.z));
        }

        string textoOrdem = temDestinoMissaoProgramado
            ? "Destino travado. O C700 vai decolar, voar e pousar no local marcado."
            : aguardandoDestinoAereo
                ? "Modo aereo ativo. Clique direito no mapa para definir a missao."
                : EstaNoSolo
                    ? "Sem controle livre no solo. Use o menu para armar voo, transportar tropas ou retornar."
                    : "Em voo. Use o menu para mandar voltar ou concluir a missao.";
        GUILayout.Label(textoOrdem);

        GUI.enabled = EstaNoSolo;
        if (GUILayout.Button("Armar voo / escolher destino", GUILayout.Height(34f)))
        {
            PrepararMissaoAerea();
        }
        GUI.enabled = EstaNoSolo && rotinaCarga == null;
        if (GUILayout.Button("Levar tropas / puxar proximas (I)", GUILayout.Height(34f)))
        {
            PuxarUnidadesProximas();
        }

        GUI.enabled = EstaNoSolo;
        if (GUILayout.Button("Desembarcar carga", GUILayout.Height(32f)))
        {
            DesembarcarTudo();
        }

        GUI.enabled = aeroportoOrigem != null;
        if (GUILayout.Button("Retornar ao aeroporto", GUILayout.Height(34f)))
        {
            OrdenarRetornoAoAeroporto();
        }
        GUI.enabled = true;

        GUILayout.Space(10f);
        GUILayout.Label("Carga interna");

        bool temCarga = false;
        for (int i = 0; i < slots.Count; i++)
        {
            SlotCarga slot = slots[i];
            if (slot == null || slot.unidade == null)
            {
                continue;
            }

            temCarga = true;
            GUILayout.BeginHorizontal("box");
            GUILayout.Label(slot.unidade.name.Replace("(Clone)", "").Trim(), GUILayout.Width(210f));
            GUI.enabled = EstaNoSolo;
            if (GUILayout.Button("Desembarcar", GUILayout.Height(28f)))
            {
                DesembarcarSlot(i);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if (!temCarga)
        {
            GUILayout.Label("Sem unidades embarcadas.");
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (EstaSelecionado && tempoMensagemOrdem > Time.time && !string.IsNullOrEmpty(mensagemOrdem))
        {
            Rect aviso = new Rect(area.x, area.y - 34f, area.width, 28f);
            GUI.Box(aviso, mensagemOrdem);
        }
    }

    public void DefinirAeroportoOrigem(GerenciadorAeroporto novoAeroporto)
    {
        aeroportoOrigem = novoAeroporto;
        if (aeroportoOrigem == null)
        {
            return;
        }

        Transform paradaGrande = aeroportoOrigem.ObterParadaGrandePreferencial(false);
        if (paradaGrande == null)
        {
            paradaGrande = aeroportoOrigem.ObterParadaGrandePreferencial(true);
        }
        if (paradaGrande != null)
        {
            pontoEstacionamentoPreferencial = paradaGrande;
        }
    }

    public void RegistrarPontoEstacionamento(Transform ponto)
    {
        if (ponto != null)
        {
            pontoEstacionamentoPreferencial = ponto;
        }
    }

    public IEnumerator TaxiarAteTransform(Transform ponto)
    {
        if (ponto == null)
        {
            yield break;
        }

        yield return StartCoroutine(TaxiarAtePosicao(ponto.position, velocidadeTaxi, -1f, false));
        transform.rotation = ponto.rotation;
        DefinirEstado(EstadoC700.Solo);
    }

    public void FinalizarPosicionamentoNoPatio(Transform ponto)
    {
        if (ponto == null)
        {
            return;
        }

        RegistrarPontoEstacionamento(ponto);
        transform.position = AjustarPosicaoAoSolo(ponto.position);
        transform.rotation = ponto.rotation;
        velocidadeSoloAtual = 0f;
        velocidadeAereaAtual = 0f;
        DefinirEstado(EstadoC700.Solo);
        retornoAutomaticoEmAndamento = false;
        LimparMissaoProgramada();
        LimparDestinoVisual();
    }

    public void ReceberOrdemMover(Vector3 destino)
    {
        if (!PontoValido(destino))
        {
            MostrarMensagem("Destino invalido.");
            return;
        }

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        if (aguardandoDestinoAereo || temDestinoMissaoProgramado || !EstaNoSolo)
        {
            OrdenarVoo(destino, false);
            return;
        }

        // Ordem normal de movimento de uma unidade selecionada: para o
        // transporte, o destino sempre representa uma pista/área de pouso.
        // Antes era apenas uma mensagem, então o avião parecia ignorar o
        // clique quando o modo aéreo não tinha sido armado pelo menu Z/O.
        OrdenarVoo(destino, false);
    }

    public void OrdenarTaxiSolo(Vector3 destino)
    {
        MostrarMensagem("O C700 nao aceita taxi manual. Use o menu para armar voo ou retornar.");
    }

    public void OrdenarRetornoAoAeroporto()
    {
        aguardandoDestinoAereo = false;

        Vector3 destinoRetorno = ObterDestinoDeRetorno();
        if (destinoRetorno == Vector3.zero)
        {
            retornoAutomaticoEmAndamento = false;
            MostrarMensagem("Sem aeroporto de retorno configurado.");
            LogDebug("Sem aeroporto ou ponto de retorno configurado.");
            return;
        }

        retornoAutomaticoEmAndamento = true;

        if (EstaNoSolo)
        {
            float distanciaRetorno = Vector3.Distance(AjustarPosicaoAoSolo(transform.position), AjustarPosicaoAoSolo(destinoRetorno));
            if (distanciaRetorno <= 350f)
            {
                prontoParaDecolarNaPista = false;
                LimparMissaoProgramada();
                RegistrarDestinoVisual(destinoRetorno);
                IniciarRotinaMovimento(TaxiarAtePosicao(destinoRetorno, velocidadeTaxi, -1f, true));
                MostrarMensagem("Taxiando de volta ao aeroporto.");
                return;
            }
        }

        OrdenarVoo(destinoRetorno, true);
    }

    public void ArmarModoAereo()
    {
        aguardandoDestinoAereo = true;
        prontoParaDecolarNaPista = false;
        menuCargaAberto = false;
        LimparMissaoProgramada();
        MostrarMensagem("Modo aereo ativo. Clique com o botao direito no destino.");
    }

    public void CancelarModoAereo()
    {
        aguardandoDestinoAereo = false;
        prontoParaDecolarNaPista = false;
        LimparMissaoProgramada();
        LimparDestinoVisual();
        MostrarMensagem("Ordem aerea cancelada.");

        // Garantir que a selecao nao fique presa neste avião ao cancelar por UI do aeroporto
        GerenteSelecao gerenteSelecao = Object.FindFirstObjectByType<GerenteSelecao>();
        ControleUnidade meuControle = GetComponent<ControleUnidade>();
        if (gerenteSelecao != null && meuControle != null && gerenteSelecao.unidadesSelecionadas.Contains(meuControle))
        {
            gerenteSelecao.DeselecionarTudo();
        }

        if (EstaNoSolo && rotinaMovimento != null)
        {
            Vector3 retorno = ObterDestinoDeRetorno();
            if (retorno != Vector3.zero)
            {
                IniciarRotinaMovimento(TaxiarAtePosicao(retorno, velocidadeTaxi));
            }
        }
    }

    public void PrepararMissaoAerea()
    {
        menuCargaAberto = false;

        if (!EstaNoSolo)
        {
            MostrarMensagem("C700 ja esta em operacao.");
            return;
        }

        if (aguardandoDestinoAereo || temDestinoMissaoProgramado)
        {
            MostrarMensagem("C700 ja esta aguardando destino.");
            return;
        }

        aguardandoDestinoAereo = true;
        prontoParaDecolarNaPista = false;
        LimparMissaoProgramada();
        LimparDestinoVisual();
        MostrarMensagem("Missao armada. Clique com o botao direito no destino.");
    }

    public void PuxarUnidadesProximas()
    {
        if (!EstaNoSolo || rotinaCarga != null)
        {
            LogDebug("Embarque recusado. Estado=" + estadoAtual + " rotinaCargaAtiva=" + (rotinaCarga != null));
            return;
        }

        if (aguardandoDestinoAereo || temDestinoMissaoProgramado)
        {
            aguardandoDestinoAereo = false;
            prontoParaDecolarNaPista = false;
            LimparMissaoProgramada();
            LimparDestinoVisual();
            MostrarMensagem("Embarque iniciado. Missao aerea cancelada.");
        }

        rotinaCarga = StartCoroutine(RotinaPuxarUnidades());
    }

    public void DesembarcarTudo()
    {
        if (!EstaNoSolo)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].unidade != null)
            {
                DesembarcarSlot(i);
            }
        }
    }

    private void OrdenarVoo(Vector3 destinoFinal, bool retornoAoAeroporto)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        if (DestinoEmPortaAvioes(destinoFinal))
        {
            MostrarMensagem("Destino invalido: porta-avioes bloqueado.");
            LogDebug("Destino cancelado: porta-avioes nao e ponto valido para o C700.");
            return;
        }

        aguardandoDestinoAereo = false;
        retornoAutomaticoEmAndamento = retornoAoAeroporto;
        RegistrarDestinoVisual(destinoFinal);

        LimparMissaoProgramada();
        IniciarRotinaMovimento(RotinaMissaoAerea(destinoFinal, retornoAoAeroporto));
    }

    private void IniciarRotinaMovimento(IEnumerator rotina)
    {
        if (rotinaMovimento != null)
        {
            StopCoroutine(rotinaMovimento);
        }

        rotinaMovimento = StartCoroutine(rotina);
    }

    private IEnumerator RotinaMissaoAerea(Vector3 destinoFinal, bool retornoAoAeroporto)
    {
        aguardandoDestinoAereo = false;
        LimparMissaoProgramada();
        Vector3 destinoSolo = AjustarPosicaoAoSolo(destinoFinal);
        AtualizarIndicadoresMissao(destinoSolo);

        if (EstaNoSolo)
        {
            yield return StartCoroutine(RotinaDecolagem());
        }

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            yield break;
        }

        // A altura do terreno pode ter mudado durante o taxi/decolagem e o
        // alvo precisa ser recalculado antes da aproximação final.
        destinoSolo = AjustarPosicaoAoSolo(destinoFinal);

        Vector3 direcaoParaAlvo = destinoSolo - transform.position;
        direcaoParaAlvo.y = 0f;
        float distAtualHorizontalSqr = direcaoParaAlvo.sqrMagnitude;

        if (distAtualHorizontalSqr < 100f) // Muito perto pra calcular vetor (10m)
        {
            direcaoParaAlvo = transform.forward;
            direcaoParaAlvo.y = 0f;
        }

        direcaoParaAlvo.Normalize();
        Vector3 direcaoRetaPouso = direcaoParaAlvo;

        // Se estiver pousando no aeroporto, pega o angulo da pista para alinhar o bico certinho no asfalto
        if (retornoAoAeroporto && aeroportoOrigem != null && aeroportoOrigem.waypointsDecida != null && aeroportoOrigem.waypointsDecida.Count > 1)
        {
            Vector3 vp = aeroportoOrigem.waypointsDecida[0].position - aeroportoOrigem.waypointsDecida[aeroportoOrigem.waypointsDecida.Count - 1].position;
            vp.y = 0;
            if (vp.sqrMagnitude > 1f)
            {
                direcaoRetaPouso = vp.normalized;
            }
        }

        // --- ILS GLIDESLOPE ---
        // A entrada é proporcional à distância real. O algoritmo anterior
        // mandava aviões próximos para um desvio fixo de 1200 m somando
        // transform.right + transform.forward; esse vetor lateral era a
        // origem do “sair de lado” visto no jogo.
        float distanciaEntrada = Mathf.Clamp(
            Mathf.Max(distanciaAproximacao, Mathf.Sqrt(distAtualHorizontalSqr) * 0.65f),
            distanciaAproximacao,
            900f);
        Vector3 pontoIaf1000m = destinoSolo - direcaoRetaPouso * distanciaEntrada
            + Vector3.up * Mathf.Max(100f, altitudeCruzeiro);
        
        // Ponto 2: Reta Final (100 metros do alvo, 10m de altura descendo rasante)
        Vector3 pontoFa100m = destinoSolo - direcaoRetaPouso * 100f + Vector3.up * 10f;
        
        // Ponto 3: Toque na Rampa
        Vector3 pontoTouchdown = destinoSolo - direcaoRetaPouso * 10f + Vector3.up * alturaToqueSolo;

        DefinirEstado(EstadoC700.EmVoo);

        // 1. Voa até o início da aproximação, sempre com direção horizontal
        // calculada a partir do destino; não há fuga diagonal nem curva
        // lateral oculta.
        yield return StartCoroutine(VoarAtePonto(pontoIaf1000m, velocidadeCruzeiro, 50f));

        if (estadoAtual == EstadoC700.Solo || !PontoValido(transform.position))
        {
            yield break;
        }

        DefinirEstado(EstadoC700.Aproximando);
        
        // 2. Desce o plano inclinado em linha reta perdendo 10 metros de altura a cada 100m andados
        yield return StartCoroutine(VoarAtePonto(pontoFa100m, velocidadeCruzeiro * 0.75f, 25f));

        if (estadoAtual == EstadoC700.Solo || !PontoValido(transform.position))
        {
            yield break;
        }

        DefinirEstado(EstadoC700.Pousando);
        
        // 3. Flare (Ultimos 100m até tocar com as rodas)
        yield return StartCoroutine(VoarAtePonto(pontoTouchdown, velocidadeDecolagem * 0.9f, 6f));

        // 4. Cola no chão fisicamente no final do toque
        transform.position = new Vector3(transform.position.x, destinoSolo.y + alturaToqueSolo, transform.position.z);
        velocidadeSoloAtual = Mathf.Max(velocidadeTaxi * 2f, velocidadeDecolagem * 0.85f);

        // Taxia os ultimos 10 metros suave pelo asfalto ate o ponto especifico que vc clicou
        yield return StartCoroutine(TaxiarAtePosicao(destinoSolo, velocidadeTaxi, velocidadeSoloAtual, retornoAoAeroporto));

        if (retornoAoAeroporto && pontoEstacionamentoPreferencial != null)
        {
            transform.rotation = pontoEstacionamentoPreferencial.rotation;
        }

        velocidadeAereaAtual = 0f;
        velocidadeSoloAtual = 0f;
        retornoAutomaticoEmAndamento = false;
        DefinirEstado(EstadoC700.Solo);
        AtualizarDestinoVisualAoChegar(destinoSolo);
        LimparDestinoVisual();
        rotinaMovimento = null;
    }

    private IEnumerator RotinaPreparacaoParaDecolagem()
    {
        if (aeroportoOrigem == null || aeroportoOrigem.waypointsDecolagem == null || aeroportoOrigem.waypointsDecolagem.Count == 0)
        {
            aguardandoDestinoAereo = true;
            LimparMissaoProgramada();
            MostrarMensagem("Sem pista configurada. Clique no mapa para tentar decolar daqui.");
            rotinaMovimento = null;
            yield break;
        }

        int indiceVoo = -1;
        for (int i = 0; i < aeroportoOrigem.waypointsDecolagem.Count; i++)
        {
            Transform wp = aeroportoOrigem.waypointsDecolagem[i];
            if (wp == null)
            {
                continue;
            }

            if (wp.name.ToLowerInvariant().Contains("voo"))
            {
                indiceVoo = i;
                break;
            }
        }

        if (indiceVoo <= 0)
        {
            aguardandoDestinoAereo = true;
            LimparMissaoProgramada();
            MostrarMensagem("Pista incompleta. Clique no mapa para tentar decolar.");
            rotinaMovimento = null;
            yield break;
        }

        // Otimização inteligente: Em vez de voltar pro waypoint 0, procura de qual waypoint está mais perto
        int indiceInicial = 0;
        float menorDistancia = float.MaxValue;
        
        for (int i = 0; i < indiceVoo; i++)
        {
            if (aeroportoOrigem.waypointsDecolagem[i] != null)
            {
                float dist = Vector3.Distance(transform.position, aeroportoOrigem.waypointsDecolagem[i].position);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    indiceInicial = i;
                }
            }
        }

        // Se ele passar do waypoint mais próximo mas ainda não chegou no próximo (ex: tá entre o 2 e o 3), vai direto pro 3
        if (indiceInicial < indiceVoo - 1 && menorDistancia < 15f)
        {
            indiceInicial++;
        }

        for (int i = indiceInicial; i < indiceVoo; i++)
        {
            Transform wpTaxi = aeroportoOrigem.waypointsDecolagem[i];
            if (wpTaxi == null)
            {
                continue;
            }

            yield return StartCoroutine(TaxiarAtePosicao(wpTaxi.position, velocidadeTaxi + (i * 3f), -1f, false));

            if (wpTaxi.name.ToLowerInvariant().Contains("alinhamento"))
            {
                yield return new WaitForSeconds(2f);
                if (i + 1 <= indiceVoo)
                {
                    Transform proxWp = aeroportoOrigem.waypointsDecolagem[i + 1];
                    if (proxWp != null)
                    {
                        Vector3 dir = proxWp.position - transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.05f)
                        {
                            Quaternion rotAlvo = Quaternion.LookRotation(dir.normalized);
                            while (Quaternion.Angle(transform.rotation, rotAlvo) > 1.5f)
                            {
                                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroSolo * Time.deltaTime);
                                yield return null;
                            }
                        }
                    }
                }
            }
        }

        // REMOVIDO SNAP ESTÁTICO DE ROTAÇÃO E ROBÓTICO. Deixa o avião virar realisticamente de onde estiver!
        
        DefinirEstado(EstadoC700.Solo);
        aguardandoDestinoAereo = true;
        prontoParaDecolarNaPista = true;
        MostrarMensagem("C700 na pista. Clique com o botao direito no destino.");

        // A missão termina aqui. Se o jogador quiser seguir viagem, precisa dar nova ordem.
        LimparMissaoProgramada();

        rotinaMovimento = null;
    }

    private IEnumerator RotinaDecolagem()
    {
        bool usouPistaDoAeroporto = false;
        bool pertoDaPista = false;
        
        if (aeroportoOrigem != null && aeroportoOrigem.waypointsDecolagem != null && aeroportoOrigem.waypointsDecolagem.Count > 0)
        {
            if (Vector3.Distance(transform.position, aeroportoOrigem.waypointsDecolagem[0].position) < 350f)
            {
                pertoDaPista = true;
            }
        }

        if (pertoDaPista)
        {
            int indiceVoo = -1;
            for (int i = 0; i < aeroportoOrigem.waypointsDecolagem.Count; i++)
            {
                Transform wp = aeroportoOrigem.waypointsDecolagem[i];
                if (wp == null)
                {
                    continue;
                }

                string nome = wp.name.ToLowerInvariant();
                if (nome.Contains("voo"))
                {
                    indiceVoo = i;
                    break;
                }
            }

            if (indiceVoo >= 0)
            {
                usouPistaDoAeroporto = true;
                if (!prontoParaDecolarNaPista)
                {
                    int indiceInicialTaxi = 0;
                    float menorTaxDist = float.MaxValue;
                    for (int i = 0; i < indiceVoo; i++)
                    {
                        if (aeroportoOrigem.waypointsDecolagem[i] != null)
                        {
                            float d = Vector3.Distance(transform.position, aeroportoOrigem.waypointsDecolagem[i].position);
                            if (d < menorTaxDist)
                            {
                                menorTaxDist = d;
                                indiceInicialTaxi = i;
                            }
                        }
                    }
                    if (indiceInicialTaxi < indiceVoo - 1 && menorTaxDist < 15f) indiceInicialTaxi++;

                    for (int i = indiceInicialTaxi; i < indiceVoo; i++)
                    {
                        Transform wpTaxi = aeroportoOrigem.waypointsDecolagem[i];
                        if (wpTaxi == null)
                        {
                            continue;
                        }

                        yield return StartCoroutine(TaxiarAtePosicao(wpTaxi.position, velocidadeTaxi + (i * 3f), -1f, false));

                        if (wpTaxi.name.ToLowerInvariant().Contains("alinhamento"))
                        {
                            yield return new WaitForSeconds(2f);
                            if (i + 1 <= indiceVoo)
                            {
                                Transform proxWp = aeroportoOrigem.waypointsDecolagem[i + 1];
                                if (proxWp != null)
                                {
                                    Vector3 dir = proxWp.position - transform.position;
                                    dir.y = 0f;
                                    if (dir.sqrMagnitude > 0.05f)
                                    {
                                        Quaternion rotAlvo = Quaternion.LookRotation(dir.normalized);
                                        while (Quaternion.Angle(transform.rotation, rotAlvo) > 1.5f)
                                        {
                                            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroSolo * Time.deltaTime);
                                            yield return null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Transform wpVoo = aeroportoOrigem.waypointsDecolagem[indiceVoo];
                if (wpVoo != null)
                {
                    yield return StartCoroutine(CorridaDecolagemPara(wpVoo.position));
                }
            }
        }

        prontoParaDecolarNaPista = false;
        if (!usouPistaDoAeroporto)
        {
            yield return StartCoroutine(CorridaDecolagemTempoParaLocalAberto());
        }

        DefinirEstado(EstadoC700.EmVoo);

        // IMPORTANTE: Continua a missão aérea (se existir) após decolar! (corrige bug de ficar voando solto)
        rotinaMovimento = null;
    }

    private IEnumerator CorridaDecolagemTempoParaLocalAberto()
    {
        DefinirEstado(EstadoC700.Decolando);
        Vector3 inicioCorrida = transform.position;
        Vector3 direcaoSaidaCerta = transform.forward;
        direcaoSaidaCerta.y = 0f;
        if (direcaoSaidaCerta.sqrMagnitude < 0.05f) direcaoSaidaCerta = Vector3.forward;
        direcaoSaidaCerta.Normalize();

        float tempoT = 0f;
        while (tempoT < 7f)
        {
            tempoT += Time.deltaTime;
            velocidadeAereaAtual = Mathf.MoveTowards(velocidadeAereaAtual, velocidadeDecolagem, aceleracaoVoo * Time.deltaTime);
            transform.position += direcaoSaidaCerta * velocidadeAereaAtual * Time.deltaTime;
            AderirAoSolo();
            yield return null;
        }

        float distanciaHorizontal = 0f;
        float alturaAlvo = Mathf.Max(altitudeCruzeiro, transform.position.y + 45f);
        
        while (transform.position.y < alturaAlvo - 1f || distanciaHorizontal < 65f)
        {
            Vector3 alvoSubida = transform.position + direcaoSaidaCerta * 85f + Vector3.up * 25f;
            AtualizarMovimentoAereo(alvoSubida, velocidadeCruzeiro * 0.72f);
            distanciaHorizontal = Vector3.Distance(new Vector3(inicioCorrida.x, 0f, inicioCorrida.z), new Vector3(transform.position.x, 0f, transform.position.z));
            yield return null;
        }
    }

    private IEnumerator CorridaDecolagemPara(Vector3 destinoSolo)
    {
        DefinirEstado(EstadoC700.Decolando);
        Vector3 inicioCorrida = transform.position;
        Vector3 destino = AjustarPosicaoAoSolo(destinoSolo);

        while (true)
        {
            Vector3 restante = destino - transform.position;
            restante.y = 0f;
            float dist = restante.magnitude;
            if (dist <= 2.5f)
            {
                break;
            }

            if (restante.sqrMagnitude > 0.5f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(restante.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroSolo * Time.deltaTime);
            }

            velocidadeAereaAtual = Mathf.MoveTowards(velocidadeAereaAtual, velocidadeDecolagem, aceleracaoVoo * Time.deltaTime);
            transform.position += transform.forward * velocidadeAereaAtual * Time.deltaTime;
            AderirAoSolo();
            yield return null;
        }

        float distanciaHorizontal = 0f;
        float alturaAlvo = Mathf.Max(altitudeCruzeiro, transform.position.y + 45f);
        Vector3 direcaoSaidaCerta = transform.forward;
        direcaoSaidaCerta.y = 0f;
        if (direcaoSaidaCerta.sqrMagnitude < 0.05f) direcaoSaidaCerta = Vector3.forward;
        direcaoSaidaCerta.Normalize();

        while (transform.position.y < alturaAlvo - 1f || distanciaHorizontal < 65f)
        {
            // Substituimos o transform.forward que criava feedback loop por uma direcao fixa horizontal.
            Vector3 alvoSubida = transform.position + direcaoSaidaCerta * 85f + Vector3.up * 25f;
            AtualizarMovimentoAereo(alvoSubida, velocidadeCruzeiro * 0.72f);
            distanciaHorizontal = Vector3.Distance(new Vector3(inicioCorrida.x, 0f, inicioCorrida.z), new Vector3(transform.position.x, 0f, transform.position.z));
            yield return null;
        }
    }

    private IEnumerator VoarAtePonto(Vector3 alvo, float velocidadeAlvo, float distanciaParada)
    {
        float distanciaParadaSqr = distanciaParada * distanciaParada;
        float tempoDecorrido = 0f;
        float proximoLog = 0f;
        float distanciaInicial = Vector3.Distance(transform.position, alvo);
        float tempoLimite = Mathf.Max(
            timeoutPorPontoAereo,
            timeoutPorPontoAereo * 0.5f + distanciaInicial / Mathf.Max(velocidadeAlvo, 1f) * 3f);

        while (true)
        {
            if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
            {
                PararPorFaltaDeCombustivel();
                yield break;
            }

            tempoDecorrido += Time.deltaTime;
            if (debugLogs && tempoDecorrido >= proximoLog)
            {
                proximoLog = tempoDecorrido + 1f;
                LogDebug("Voo para ponto " + alvo + " | posicao=" + transform.position
                    + " | distancia=" + Vector3.Distance(transform.position, alvo)
                    + " | velocidade=" + velocidadeAereaAtual);
            }
            if (tempoDecorrido >= tempoLimite)
            {
                // Falha controlada: nunca teleporta para o waypoint e nunca
                // deixa uma coroutine perseguindo um ponto que ja nao e
                // alcancavel. A missao permanece no ponto seguro atual.
                velocidadeAereaAtual = 0f;
                LogDebug("Timeout de aproximacao aerea; mantendo posicao atual em " + transform.position);
                yield break;
            }

            Vector3 dif = alvo - transform.position;
            if (dif.sqrMagnitude <= distanciaParadaSqr)
            {
                break;
            }

            AtualizarMovimentoAereo(alvo, velocidadeAlvo, distanciaParada);
            yield return null;
        }
    }

    private void AtualizarMovimentoAereo(Vector3 alvo, float velocidadeAlvo, float distanciaParada = 0f)
    {
        Vector3 direcao = alvo - transform.position;
        float distancia = direcao.magnitude;
        if (direcao.sqrMagnitude > 0.05f)
        {
            // --- BLOQUEIO DE MERGULHO (NOSEDIVE) ---
            // Limita o nariz do avião para ele nunca embicar 90 graus p/ baixo
            Vector3 direcaoH = new Vector3(direcao.x, 0f, direcao.z);
            if (direcaoH.sqrMagnitude > 0.01f)
            {
                float inclincacaoMax = direcaoH.magnitude * 0.45f; // Limita a ~24 graus
                direcao.y = Mathf.Clamp(direcao.y, -inclincacaoMax, inclincacaoMax);
            }
            else
            {
                // Se estiver exatamente em cima do alvo, o aviao continua voando rasante pra frente p/ fazer a curva suave
                direcao = transform.forward;
                direcao.y = -0.45f; // Descida suave constante
            }

            Quaternion rotAlvo = Quaternion.LookRotation(direcao.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroVoo * Time.deltaTime);
        }

        // A velocidade precisa respeitar a distancia de parada. Sem essa
        // desaceleracao o C700 passava reto pelo IAF e entrava em orbita,
        // permanecendo em EmVoo para sempre. O limite por curva reduz a
        // velocidade ao aproximar-se sem um snap ou teleporte.
        float velocidadeSegura = velocidadeAlvo;
        if (distanciaParada > 0f)
        {
            float distanciaFrenagem = Mathf.Max(distanciaParada, distanciaParada * 1.5f);
            float velocidadePorFrenagem = Mathf.Sqrt(
                Mathf.Max(0f, 2f * Mathf.Max(1f, aceleracaoVoo) * Mathf.Max(0f, distancia - distanciaParada)));
            velocidadeSegura = Mathf.Min(velocidadeSegura, velocidadePorFrenagem);

            float raioCurvaSeguro = Mathf.Max(8f, distanciaParada * 0.8f);
            float velocidadePorCurva = Mathf.Max(8f, giroVoo * Mathf.Deg2Rad * raioCurvaSeguro * 0.75f);
            if (distancia <= distanciaFrenagem * 2f)
            {
                velocidadeSegura = Mathf.Min(velocidadeSegura, velocidadePorCurva);
            }

            // A reta final do pouso tem poucos metros. Mesmo com uma curva
            // suave, manter 30+ m/s cria uma orbita em torno do toque porque
            // o raio de curva fica maior que a distancia restante. Reduza a
            // velocidade progressivamente apenas nessa zona curta; as fases
            // de cruzeiro e aproximacao continuam na velocidade normal.
            if (distanciaParada <= 12f && distancia <= 30f)
            {
                float tFinal = Mathf.InverseLerp(distanciaParada, 30f, distancia);
                float velocidadeFinal = Mathf.Lerp(4f, Mathf.Min(12f, velocidadeAlvo), tFinal);
                velocidadeSegura = Mathf.Min(velocidadeSegura, velocidadeFinal);
            }
        }

        velocidadeAereaAtual = Mathf.MoveTowards(velocidadeAereaAtual, velocidadeSegura, aceleracaoVoo * Time.deltaTime);
        float deslocamento = velocidadeAereaAtual * Time.deltaTime;
        float limiteDeslocamento = Mathf.Max(0f, distancia - deslocamentoMinimoAereo);
        if (limiteDeslocamento > 0f)
        {
            deslocamento = Mathf.Min(deslocamento, limiteDeslocamento);
            transform.position += transform.forward * deslocamento;
        }
    }

    private IEnumerator TaxiarAtePosicao(Vector3 destino, float velocidadeMaxima, float velocidadeInicial = -1f, bool atualizarEstacionamento = true)
    {
        DefinirEstado(EstadoC700.Taxiando);
        if (velocidadeInicial >= 0f)
        {
            velocidadeSoloAtual = velocidadeInicial;
        }

        Vector3 destinoSolo = AjustarPosicaoAoSolo(destino);
        float raioSqr = raioChegadaSolo * raioChegadaSolo;

        while (true)
        {
            if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
            {
                PararPorFaltaDeCombustivel();
                yield break;
            }

            destinoSolo = AjustarPosicaoAoSolo(destino);
            Vector3 dif = destinoSolo - transform.position;
            dif.y = 0f;

            if (dif.sqrMagnitude <= raioSqr)
            {
                break;
            }

            Vector3 direcao = dif.normalized;
            if (direcao.sqrMagnitude > 0.001f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(direcao, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroSolo * Time.deltaTime);
            }

            float distancia = dif.magnitude;
            float velDesejada = velocidadeMaxima;
            if (distancia < distanciaFreioSolo)
            {
                float t = Mathf.Clamp01(distancia / Mathf.Max(1f, distanciaFreioSolo));
                velDesejada = Mathf.Lerp(3f, velocidadeMaxima, t);
            }

            float taxa = velocidadeSoloAtual <= velDesejada ? aceleracaoSolo : desaceleracaoSolo;
            velocidadeSoloAtual = Mathf.MoveTowards(velocidadeSoloAtual, velDesejada, taxa * Time.deltaTime);
            transform.position += transform.forward * velocidadeSoloAtual * Time.deltaTime;
            AderirAoSolo();
            yield return null;
        }

        transform.position = destinoSolo;
        AderirAoSolo();
        velocidadeSoloAtual = 0f;
        DefinirEstado(EstadoC700.Solo);
        if (!aguardandoDestinoAereo && !temDestinoMissaoProgramado)
        {
            AtualizarDestinoVisualAoChegar(destinoSolo);
        }

        if (atualizarEstacionamento && aeroportoOrigem != null && pontoEstacionamentoPreferencial != null)
        {
            float distParada = (pontoEstacionamentoPreferencial.position - transform.position).sqrMagnitude;
            if (distParada <= 49f)
            {
                transform.rotation = pontoEstacionamentoPreferencial.rotation;
            }
        }
    }

    public void PararPorFaltaDeCombustivel()
    {
        if (rotinaMovimento != null)
        {
            StopCoroutine(rotinaMovimento);
            rotinaMovimento = null;
        }

        velocidadeSoloAtual = 0f;
        velocidadeAereaAtual = 0f;
        aguardandoDestinoAereo = false;
        prontoParaDecolarNaPista = false;
        retornoAutomaticoEmAndamento = false;
        LimparMissaoProgramada();
        LimparDestinoVisual();

        if (EstaNoSolo)
        {
            DefinirEstado(EstadoC700.Solo);
            AderirAoSolo();
            return;
        }

        FalhaAereaFisica.Ativar(gameObject, rb, Mathf.Max(velocidadeCruzeiro, velocidadeDecolagem) * 0.55f, 4.5f, false);
    }

    private void AvaliarRetornoSeguro()
    {
        if (estadoAtual != EstadoC700.EmVoo || aeroportoOrigem == null)
        {
            return;
        }

        CombustivelUnidade combustivel = GetComponent<CombustivelUnidade>();
        if (combustivel == null || !combustivel.usaCombustivel)
        {
            return;
        }

        Vector3 retorno = ObterDestinoDeRetorno();
        if (retorno == Vector3.zero)
        {
            return;
        }

        float distancia = Vector3.Distance(transform.position, retorno);
        float consumoRetorno = combustivel.EstimarConsumoParaDistancia(distancia, Mathf.Max(45f, velocidadeCruzeiro));
        float reserva = Mathf.Max(combustivel.Capacidade * reservaRetornoPercentual, consumoRetorno * 0.40f);

        if (!retornoAutomaticoEmAndamento && combustivel.CombustivelAtual <= consumoRetorno + reserva)
        {
            retornoAutomaticoEmAndamento = true;
            OrdenarRetornoAoAeroporto();
        }
    }

    private IEnumerator RotinaPuxarUnidades()
    {
        // Unidades recém-criadas ou reposicionadas podem ainda não ter
        // atualizado o broadphase da física no mesmo frame do comando.
        // Sincronizamos uma única vez por embarque, nunca dentro de Update.
        Physics.SyncTransforms();
        // Não dependa da layer Default: unidades de cena podem estar em
        // IgnoreRaycast ou em uma layer própria. O filtro de tipo abaixo
        // impede que o C700 capture cenário, navios ou outra aeronave.
        Collider[] hits = Physics.OverlapSphere(transform.position, raioBuscaCarga, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        List<GameObject> fila = new List<GameObject>();
        LogDebug("Busca de embarque: hits=" + hits.Length + " raio=" + raioBuscaCarga);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject unidade = ResolverUnidade(hits[i]);
            if (unidade == null || fila.Contains(unidade))
            {
                continue;
            }

            if (EhValidaParaCarga(unidade))
            {
                fila.Add(unidade);
            }
        }

        // Fallback estruturado para tropas sem collider ou com collider em
        // layer excluída pelo projeto. O registro é consultado somente no
        // início da operação, nunca a cada frame.
        controlesRegistradosCarga.Clear();
        RegistroEntidadesJogo.FillControlesUnidade(controlesRegistradosCarga);
        float raioBuscaSqr = Mathf.Max(1f, raioBuscaCarga) * Mathf.Max(1f, raioBuscaCarga);
        for (int i = 0; i < controlesRegistradosCarga.Count; i++)
        {
            ControleUnidade controle = controlesRegistradosCarga[i];
            if (controle == null || !controle.gameObject.activeInHierarchy)
            {
                continue;
            }

            GameObject unidade = controle.transform.root != null
                ? controle.transform.root.gameObject
                : controle.gameObject;
            if (unidade == null || fila.Contains(unidade))
            {
                continue;
            }

            Vector3 delta = unidade.transform.position - transform.position;
            if (delta.sqrMagnitude > raioBuscaSqr)
            {
                continue;
            }

            if (EhValidaParaCarga(unidade))
            {
                fila.Add(unidade);
            }
        }

        LogDebug("Fila de embarque validada=" + fila.Count + " espacos=" + slots.Count);

        if (fila.Count == 0)
        {
            MostrarMensagem("Nenhuma unidade aliada valida no raio de embarque.");
        }

        for (int i = 0; i < fila.Count; i++)
        {
            if (!TemEspacoLivre())
            {
                break;
            }

            if (fila[i] == null)
            {
                continue;
            }

            EmbarcarUnidade(fila[i]);
            yield return new WaitForSeconds(atrasoEntreEmbarques);
        }

        rotinaCarga = null;
    }

    private void EmbarcarUnidade(GameObject unidade)
    {
        SlotCarga slotLivre = ObterPrimeiroSlotLivre();
        if (slotLivre == null || unidade == null)
        {
            return;
        }

        slotLivre.unidade = unidade;

        RemoverComportamentosSeguir(unidade);

        NavMeshAgent agente = ObterComponenteUnidade<NavMeshAgent>(unidade);
        if (agente != null)
        {
            if (agente.isOnNavMesh)
            {
                agente.ResetPath();
            }
            agente.enabled = false;
        }

        Rigidbody rbUnidade = ObterComponenteUnidade<Rigidbody>(unidade);
        if (rbUnidade != null)
        {
            rbUnidade.isKinematic = true;
            rbUnidade.detectCollisions = false;
        }

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        unidade.transform.SetParent(slotLivre.ancora, true);
        unidade.transform.position = slotLivre.ancora.position;
        unidade.transform.rotation = slotLivre.ancora.rotation;

        if (ocultarCargaInterna)
        {
            unidade.SetActive(false);
        }

        LogDebug("Embarcou: " + unidade.name);
        MostrarMensagem(unidade.name.Replace("(Clone)", "").Trim() + " embarcou.");
    }

    private void DesembarcarSlot(int indice)
    {
        if (indice < 0 || indice >= slots.Count || slots[indice].unidade == null)
        {
            return;
        }

        GameObject unidade = slots[indice].unidade;
        if (unidade == null)
        {
            slots[indice].unidade = null;
            return;
        }
        slots[indice].unidade = null;

        Vector3 destino = CalcularPontoDesembarque(indice);
        Vector3 destinoSolo = AjustarPosicaoAoSolo(destino);

        unidade.SetActive(true);
        unidade.transform.SetParent(null, true);
        unidade.transform.position = destinoSolo;
        unidade.transform.rotation = transform.rotation;

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }

        Rigidbody rbUnidade = unidade.GetComponent<Rigidbody>();
        if (rbUnidade != null)
        {
            rbUnidade.isKinematic = false;
            rbUnidade.detectCollisions = true;
        }

        NavMeshAgent agente = ObterComponenteUnidade<NavMeshAgent>(unidade);
        if (agente != null)
        {
            agente.enabled = true;
            if (NavMesh.SamplePosition(destinoSolo, out NavMeshHit hit, 16f, NavMesh.AllAreas))
            {
                agente.Warp(hit.position);
                agente.SetDestination(hit.position);
            }
            else
            {
                agente.Warp(destinoSolo);
            }
        }

        LogDebug("Desembarcou: " + unidade.name);
        MostrarMensagem(unidade.name.Replace("(Clone)", "").Trim() + " desembarcou.");
    }

    private void MostrarMensagem(string texto, float duracao = 2.5f)
    {
        mensagemOrdem = texto;
        tempoMensagemOrdem = Time.time + duracao;
    }

    private void DesenharManifestoConfiguravel()
    {
        if (manifestoConfigurado == null || manifestoConfigurado.Count == 0)
        {
            GUILayout.Label("Sem entradas de manifesto.");
            return;
        }

        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            EntradaManifesto entrada = manifestoConfigurado[i];
            if (entrada == null)
            {
                continue;
            }

            int ajusteRapido = Mathf.Max(1, entrada.ajusteRapido);
            int ajustePesado = Mathf.Max(ajusteRapido, entrada.ajustePesado);

            GUILayout.BeginVertical("box");
            GUILayout.Label(entrada.nome + ": " + entrada.quantidade);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-" + ajustePesado, GUILayout.Height(26f)))
            {
                AjustarManifesto(i, -ajustePesado);
            }
            if (GUILayout.Button("-" + ajusteRapido, GUILayout.Height(26f)))
            {
                AjustarManifesto(i, -ajusteRapido);
            }
            if (GUILayout.Button("+" + ajusteRapido, GUILayout.Height(26f)))
            {
                AjustarManifesto(i, ajusteRapido);
            }
            if (GUILayout.Button("+" + ajustePesado, GUILayout.Height(26f)))
            {
                AjustarManifesto(i, ajustePesado);
            }
            GUILayout.EndHorizontal();

            if (entrada.prefabDesembarque == null)
            {
                GUILayout.Label("Prefab de desembarque nao configurado.");
            }
            GUILayout.EndVertical();
        }
    }

    private void FixarCargaVisivel()
    {
        if (ocultarCargaInterna)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            SlotCarga slot = slots[i];
            if (slot == null || slot.unidade == null || slot.ancora == null)
            {
                continue;
            }

            slot.unidade.transform.position = Vector3.Lerp(slot.unidade.transform.position, slot.ancora.position, Time.deltaTime * 18f);
            slot.unidade.transform.rotation = Quaternion.Lerp(slot.unidade.transform.rotation, slot.ancora.rotation, Time.deltaTime * 12f);
        }
    }

    private void AtualizarVisualVoo()
    {
        if (modeloVisual == null)
        {
            ultimaPosicao = transform.position;
            return;
        }

        Vector3 velocidade = (transform.position - ultimaPosicao) / Mathf.Max(Time.deltaTime, 0.0001f);
        ultimaPosicao = transform.position;

        Vector3 velocidadeLocal = transform.InverseTransformDirection(velocidade);
        float rollAlvo = Mathf.Clamp(-velocidadeLocal.x * 0.18f, -bankMaximo, bankMaximo);
        float pitchAlvo = Mathf.Clamp(velocidadeLocal.y * 0.15f, -pitchMaximo, pitchMaximo);

        if (EstaNoSolo)
        {
            rollAlvo *= 0.25f;
            pitchAlvo *= 0.3f;
        }

        rollVisualAtual = Mathf.Lerp(rollVisualAtual, rollAlvo, Time.deltaTime * suavizacaoVisual);
        pitchVisualAtual = Mathf.Lerp(pitchVisualAtual, pitchAlvo, Time.deltaTime * suavizacaoVisual);
        
        float extraYaw = modeloInvertido180 ? 180f : 0f;
        modeloVisual.localRotation = rotacaoModeloBase * Quaternion.Euler(pitchVisualAtual, extraYaw, rollVisualAtual);
    }

    private void DesabilitarComponentesDeCombate()
    {
        DesabilitarComponente<SistemaDeTiro>();
        DesabilitarComponente<SistemaAntiMissil>();
        DesabilitarComponente<LancadorMisselCaca>();
        DesabilitarComponente<LancadorMultiplo>();
        DesabilitarComponente<LancadorSimples>();
        DesabilitarComponente<LancadorMisseis>();
        DesabilitarComponente<LancadorMLRS>();
        DesabilitarComponente<LancadorNaval>();
        DesabilitarComponente<SistemaArmamentoHelice>();
        DesabilitarComponente<ControleTorreta>();
        DesabilitarComponente<ControleTorretaModular>();
    }

    private void DesabilitarComponente<T>() where T : Behaviour
    {
        T componente = GetComponent<T>();
        if (componente != null)
        {
            componente.enabled = false;
        }
    }

    private void PrepararSlotsDeCarga()
    {
        slots.Clear();

        if (pontosCarga == null || pontosCarga.Length == 0)
        {
            CriarPontosDeCargaPadrao();
        }

        for (int i = 0; i < pontosCarga.Length; i++)
        {
            if (pontosCarga[i] == null)
            {
                continue;
            }

            slots.Add(new SlotCarga
            {
                ancora = pontosCarga[i],
                unidade = null
            });
        }
    }

    private void GarantirManifestoPadrao()
    {
        if (manifestoConfigurado != null && manifestoConfigurado.Count > 0)
        {
            return;
        }

        manifestoConfigurado = new List<EntradaManifesto>
        {
            new EntradaManifesto
            {
                nome = "Soldados",
                tipoUnidade = TipoUnidade.Infantaria,
                quantidade = 0,
                ajusteRapido = 10,
                ajustePesado = 50,
                quantidadeMaxima = 300
            },
            new EntradaManifesto
            {
                nome = "Veiculos",
                tipoUnidade = TipoUnidade.Veiculo,
                quantidade = 0,
                ajusteRapido = 5,
                ajustePesado = 25,
                quantidadeMaxima = 120
            }
        };
    }

    private void CriarPontosDeCargaPadrao()
    {
        int total = Mathf.Max(1, capacidadeMaxima);
        Transform raiz = new GameObject("PontosCarga_C700").transform;
        raiz.SetParent(transform, false);

        pontosCarga = new Transform[total];
        for (int i = 0; i < total; i++)
        {
            Transform ponto = new GameObject("Carga_" + i).transform;
            ponto.SetParent(raiz, false);

            int coluna = i % 2;
            int linha = i / 2;
            float x = coluna == 0 ? -2.8f : 2.8f;
            float y = 2.3f;
            float z = 9f - (linha * 5.6f);

            ponto.localPosition = new Vector3(x, y, z);
            ponto.localRotation = Quaternion.identity;
            pontosCarga[i] = ponto;
        }
    }

    private int QuantidadeCargas()
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].unidade != null)
            {
                total++;
            }
        }
        return total;
    }

    private int CalcularQuantidadeManifestoTotal()
    {
        if (manifestoConfigurado == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            EntradaManifesto entrada = manifestoConfigurado[i];
            if (entrada == null)
            {
                continue;
            }

            total += Mathf.Max(0, entrada.quantidade);
        }

        return total;
    }

    public void AjustarManifesto(int indice, int delta)
    {
        if (manifestoConfigurado == null || indice < 0 || indice >= manifestoConfigurado.Count)
        {
            return;
        }

        EntradaManifesto entrada = manifestoConfigurado[indice];
        if (entrada == null)
        {
            return;
        }

        int limite = Mathf.Max(0, entrada.quantidadeMaxima);
        entrada.quantidade = Mathf.Clamp(entrada.quantidade + delta, 0, limite);
    }

    public void LimparManifestoConfigurado()
    {
        if (manifestoConfigurado == null)
        {
            return;
        }

        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            if (manifestoConfigurado[i] != null)
            {
                manifestoConfigurado[i].quantidade = 0;
            }
        }

        MostrarMensagem("Manifesto limpo.");
    }

    public void DesembarcarManifestoConfigurado()
    {
        if (!EstaNoSolo)
        {
            MostrarMensagem("O C700 precisa estar no solo.");
            return;
        }

        if (rotinaDesembarqueManifesto != null)
        {
            return;
        }

        if (CalcularQuantidadeManifestoTotal() <= 0)
        {
            MostrarMensagem("Manifesto vazio.");
            return;
        }

        rotinaDesembarqueManifesto = StartCoroutine(RotinaDesembarqueManifestoConfigurado());
    }

    private IEnumerator RotinaDesembarqueManifestoConfigurado()
    {
        int indiceGlobal = 0;
        int totalInstanciado = 0;
        int totalIgnorado = 0;
        int limitePorQuadro = Mathf.Max(1, spawnsPorQuadroManifesto);
        float pausaLote = Mathf.Max(0f, pausaEntreLotesManifesto);

        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            EntradaManifesto entrada = manifestoConfigurado[i];
            if (entrada == null || entrada.quantidade <= 0)
            {
                continue;
            }

            int quantidadeParaCriar = entrada.quantidade;
            entrada.quantidade = 0;

            if (entrada.prefabDesembarque == null)
            {
                totalIgnorado += quantidadeParaCriar;
                continue;
            }

            float espacamento = ObterEspacamentoManifesto(entrada);
            for (int n = 0; n < quantidadeParaCriar; n++)
            {
                Vector3 pontoSpawn = CalcularPontoManifesto(indiceGlobal, espacamento);
                GameObject novaUnidade = Instantiate(entrada.prefabDesembarque, pontoSpawn, Quaternion.LookRotation(-transform.forward, Vector3.up));
                ConfigurarUnidadeDesembarcada(novaUnidade, entrada, pontoSpawn);

                indiceGlobal++;
                totalInstanciado++;

                if ((totalInstanciado % limitePorQuadro) == 0)
                {
                    if (pausaLote > 0f)
                    {
                        yield return new WaitForSeconds(pausaLote);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }

        if (totalIgnorado > 0)
        {
            MostrarMensagem(totalInstanciado + " unidade(s) desembarcadas. " + totalIgnorado + " ignoradas sem prefab.");
        }
        else
        {
            MostrarMensagem(totalInstanciado + " unidade(s) desembarcadas do manifesto.");
        }

        rotinaDesembarqueManifesto = null;
    }

    private float ObterEspacamentoManifesto(EntradaManifesto entrada)
    {
        if (entrada == null)
        {
            return espacamentoManifestoInfantaria;
        }

        return entrada.tipoUnidade == TipoUnidade.Infantaria
            ? Mathf.Max(1.5f, espacamentoManifestoInfantaria)
            : Mathf.Max(3.5f, espacamentoManifestoVeiculos);
    }

    private Vector3 CalcularPontoManifesto(int indice, float espacamento)
    {
        const int colunas = 6;
        int linha = indice / colunas;
        int coluna = indice % colunas;
        float larguraTotal = (colunas - 1) * 0.5f;
        float offsetLateral = (coluna - larguraTotal) * espacamento;
        float offsetProfundidade = 10f + (linha * espacamento);

        Vector3 baseSaida = transform.position - transform.forward * offsetProfundidade;
        Vector3 ponto = baseSaida + (transform.right * offsetLateral);
        return AjustarPosicaoAoSolo(ponto);
    }

    private void ConfigurarUnidadeDesembarcada(GameObject unidade, EntradaManifesto entrada, Vector3 pontoBase)
    {
        if (unidade == null)
        {
            return;
        }

        unidade.SetActive(true);
        unidade.transform.SetParent(null, true);
        unidade.transform.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);

        Vector3 pontoFinal = AjustarPosicaoAoSolo(pontoBase);
        NavMeshAgent agente = unidade.GetComponent<NavMeshAgent>();
        if (agente != null)
        {
            if (!agente.enabled)
            {
                agente.enabled = true;
            }

            if (NavMesh.SamplePosition(pontoBase, out NavMeshHit hit, 24f, NavMesh.AllAreas))
            {
                pontoFinal = hit.position;
            }

            unidade.transform.position = pontoFinal;
            agente.Warp(pontoFinal);
        }
        else
        {
            unidade.transform.position = pontoFinal;
        }

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }

        Rigidbody rbUnidade = unidade.GetComponent<Rigidbody>();
        if (rbUnidade != null)
        {
            rbUnidade.isKinematic = false;
            rbUnidade.detectCollisions = true;
        }

        IdentidadeUnidade minhaIdentidade = GetComponent<IdentidadeUnidade>();
        IdentidadeUnidade identidadeNova = unidade.GetComponent<IdentidadeUnidade>();
        if (minhaIdentidade != null && identidadeNova != null)
        {
            identidadeNova.teamID = minhaIdentidade.teamID;
            identidadeNova.nomeDoPais = minhaIdentidade.nomeDoPais;
            identidadeNova.tipoUnidade = entrada != null ? entrada.tipoUnidade : identidadeNova.tipoUnidade;
        }

        ControleUnidade controleNovaUnidade = unidade.GetComponent<ControleUnidade>();
        if (controleNovaUnidade != null)
        {
            controleNovaUnidade.DefinirSelecao(false);
        }
    }

    private void AtualizarIndicadoresMissao(Vector3 destinoFinal)
    {
        Vector3 destinoSolo = AjustarPosicaoAoSolo(destinoFinal);
        Vector3 referencia = transform.position;
        Vector3 direcao = destinoSolo - referencia;
        direcao.y = 0f;

        if (direcao.sqrMagnitude < 0.01f)
        {
            direcao = transform.forward;
            direcao.y = 0f;
        }

        if (direcao.sqrMagnitude < 0.01f)
        {
            direcao = Vector3.forward;
        }

        direcao.Normalize();

        Vector3 pontoPouso = destinoSolo - direcao * 12f + Vector3.up * alturaToqueSolo;
        Vector3 pontoParada = destinoSolo;

        marcadorPousoMissao = AtualizarMarcadorMissao(marcadorPousoMissao, pontoPouso, corPousoMissao, "C700_Marcador_Pouso");
        marcadorParadaMissao = AtualizarMarcadorMissao(marcadorParadaMissao, pontoParada, corParadaMissao, "C700_Marcador_Parada");
        AtualizarLinhaMissao(pontoPouso, pontoParada);
    }

    private GameObject AtualizarMarcadorMissao(GameObject marcadorExistente, Vector3 posicao, Color cor, string nome)
    {
        if (marcadorExistente != null)
        {
            marcadorExistente.transform.position = posicao + Vector3.up * alturaMarcadorMissao;
            Renderer existente = marcadorExistente.GetComponent<Renderer>();
            if (existente != null && existente.material != null)
            {
                existente.material.color = cor;
            }
            return marcadorExistente;
        }

        GameObject marcador = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marcador.name = nome;
        Destroy(marcador.GetComponent<Collider>());
        marcador.transform.position = posicao + Vector3.up * alturaMarcadorMissao;
        marcador.transform.localScale = new Vector3(escalaMarcadorMissao, alturaMarcadorMissao, escalaMarcadorMissao);

        Renderer rend = marcador.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = cor;
        }
        return marcador;
    }

    private void AtualizarLinhaMissao(Vector3 inicio, Vector3 fim)
    {
        if (linhaMissao == null)
        {
            GameObject linhaObj = new GameObject("C700_Linha_Missao");
            linhaObj.transform.SetParent(transform, true);
            linhaMissao = linhaObj.AddComponent<LineRenderer>();
            linhaMissao.useWorldSpace = true;
            linhaMissao.startWidth = espessuraLinhaMissao;
            linhaMissao.endWidth = espessuraLinhaMissao;
            linhaMissao.material = new Material(Shader.Find("Sprites/Default"));
            linhaMissao.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linhaMissao.positionCount = 2;
        }

        linhaMissao.gameObject.SetActive(true);
        linhaMissao.SetPosition(0, inicio + Vector3.up * 1.2f);
        linhaMissao.SetPosition(1, fim + Vector3.up * 1.2f);
        linhaMissao.startColor = corPousoMissao;
        linhaMissao.endColor = corParadaMissao;
    }

    private void LimparIndicadoresMissao()
    {
        if (marcadorPousoMissao != null)
        {
            Destroy(marcadorPousoMissao);
            marcadorPousoMissao = null;
        }

        if (marcadorParadaMissao != null)
        {
            Destroy(marcadorParadaMissao);
            marcadorParadaMissao = null;
        }

        if (linhaMissao != null)
        {
            Destroy(linhaMissao.gameObject);
            linhaMissao = null;
        }
    }

    private void RegistrarDestinoVisual(Vector3 destino)
    {
        destinoVisualAtual = AjustarPosicaoAoSolo(destino);
        temDestinoVisual = true;
        AtualizarIndicadoresMissao(destinoVisualAtual);
    }

    private void LimparDestinoVisual()
    {
        temDestinoVisual = false;
        destinoVisualAtual = Vector3.zero;
        LimparIndicadoresMissao();
    }

    private void AtualizarDestinoVisualAoChegar(Vector3 destino)
    {
        if (!temDestinoVisual)
        {
            return;
        }

        Vector3 destinoSolo = AjustarPosicaoAoSolo(destino);
        if (Vector3.Distance(transform.position, destinoSolo) <= Mathf.Max(raioChegadaSolo + 2f, 6f))
        {
            LimparDestinoVisual();
        }
    }

    private void LimparMissaoProgramada()
    {
        temDestinoMissaoProgramado = false;
        destinoMissaoProgramado = Vector3.zero;
    }

    private bool TemEspacoLivre()
    {
        return ObterPrimeiroSlotLivre() != null;
    }

    private SlotCarga ObterPrimeiroSlotLivre()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].unidade == null)
            {
                return slots[i];
            }
        }
        return null;
    }

    private GameObject ResolverUnidade(Collider hit)
    {
        if (hit == null)
        {
            return null;
        }

        ControleUnidade controle = hit.GetComponentInParent<ControleUnidade>();
        if (controle == null)
        {
            controle = hit.GetComponentInChildren<ControleUnidade>(true);
        }
        if (controle != null)
        {
            return controle.transform.root != null ? controle.transform.root.gameObject : controle.gameObject;
        }

        NavMeshAgent agente = hit.GetComponentInParent<NavMeshAgent>();
        if (agente == null)
        {
            agente = hit.GetComponentInChildren<NavMeshAgent>(true);
        }
        if (agente != null)
        {
            return agente.transform.root != null ? agente.transform.root.gameObject : agente.gameObject;
        }

        return hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
    }

    private bool EhValidaParaCarga(GameObject unidade)
    {
        if (unidade == null || unidade == gameObject)
        {
            return false;
        }

        if (JaEstaEmbarcada(unidade))
        {
            return false;
        }

        if (ObterComponenteUnidade<C700TransporteAereo>(unidade) != null)
        {
            return false;
        }

        IdentidadeUnidade minhaIdentidade = GetComponent<IdentidadeUnidade>();
        IdentidadeUnidade identidadeUnidade = ObterComponenteUnidade<IdentidadeUnidade>(unidade);
        if (minhaIdentidade != null && identidadeUnidade != null && identidadeUnidade.teamID != minhaIdentidade.teamID)
        {
            return false;
        }

        if (ObterComponenteUnidade<ControleAviao>(unidade) != null ||
            ObterComponenteUnidade<ControleAviaoCaca>(unidade) != null ||
            ObterComponenteUnidade<Helicoptero>(unidade) != null ||
            ObterComponenteUnidade<ControleNavioRealista>(unidade) != null ||
            ObterComponenteUnidade<ControleSubmarino>(unidade) != null ||
            ObterComponenteUnidade<HovercraftTransporte>(unidade) != null)
        {
            return false;
        }

        string nome = unidade.name.ToLowerInvariant();
        if (nome.Contains("uss") || nome.Contains("ship") || nome.Contains("porta") || nome.Contains("navio"))
        {
            return false;
        }

        SistemaDeDanos danos = ObterComponenteUnidade<SistemaDeDanos>(unidade);
        if (danos != null && danos.unidadeBiologica)
        {
            return true;
        }

        if (ObterComponenteUnidade<NavMeshAgent>(unidade) != null)
        {
            return true;
        }

        return nome.Contains("soldado") ||
               nome.Contains("soldier") ||
               nome.Contains("infant") ||
               nome.Contains("tank") ||
               nome.Contains("tanque") ||
               nome.Contains("blindado") ||
               nome.Contains("truck") ||
               nome.Contains("caminhao") ||
               nome.Contains("jeep") ||
               nome.Contains("humvee") ||
               nome.Contains("lancador");
    }

    private static T ObterComponenteUnidade<T>(GameObject unidade) where T : Component
    {
        if (unidade == null) return null;
        T componente = unidade.GetComponent<T>();
        if (componente != null) return componente;
        componente = unidade.GetComponentInParent<T>();
        if (componente != null) return componente;
        return unidade.GetComponentInChildren<T>(true);
    }

    private bool JaEstaEmbarcada(GameObject unidade)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].unidade == unidade)
            {
                return true;
            }
        }
        return false;
    }

    private void RemoverComportamentosSeguir(GameObject unidade)
    {
        ControleUnidade controle = ObterComponenteUnidade<ControleUnidade>(unidade);
        if (controle != null)
        {
            controle.CancelarOrdemEspecial();
            return;
        }

        ComportamentoSeguirUniversal seguirUniversal = ObterComponenteUnidade<ComportamentoSeguirUniversal>(unidade);
        if (seguirUniversal != null)
        {
            seguirUniversal.enabled = false;
            Destroy(seguirUniversal);
        }

        ComportamentoPatrulhaUniversal patrulhaUniversal = ObterComponenteUnidade<ComportamentoPatrulhaUniversal>(unidade);
        if (patrulhaUniversal != null)
        {
            patrulhaUniversal.enabled = false;
            Destroy(patrulhaUniversal);
        }
    }

    private Vector3 CalcularPontoDesembarque(int indice)
    {
        int linha = indice / 2;
        int lado = (indice % 2 == 0) ? 1 : -1;
        if (indice == 0)
        {
            lado = 0;
        }

        Vector3 baseSaida = transform.position - transform.forward * Mathf.Max(8f, distanciaDesembarque * 0.35f);
        Vector3 lateral = transform.right * (lado * (6f + linha * 4f));
        Vector3 profundidade = -transform.forward * (linha * 4f);
        return baseSaida + lateral + profundidade;
    }

    private Vector3 ObterDestinoDeRetorno()
    {
        if (pontoEstacionamentoPreferencial != null)
        {
            return pontoEstacionamentoPreferencial.position;
        }

        if (aeroportoOrigem == null)
        {
            return Vector3.zero;
        }

        Transform paradaGrande = aeroportoOrigem.ObterParadaGrandePreferencial(false);
        if (paradaGrande == null)
        {
            paradaGrande = aeroportoOrigem.ObterParadaGrandePreferencial(true);
        }
        if (paradaGrande != null)
        {
            pontoEstacionamentoPreferencial = paradaGrande;
            return paradaGrande.position;
        }

        Transform vaga = aeroportoOrigem.ObterPrimeiraVagaLivre();
        if (vaga != null)
        {
            pontoEstacionamentoPreferencial = vaga;
            return vaga.position;
        }

        return aeroportoOrigem.transform.position;
    }

    private bool DestinoEmPortaAvioes(Vector3 destino)
    {
        Collider[] hits = Physics.OverlapSphere(destino + Vector3.up * 5f, raioBloqueioPortaAvioes, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponentInParent<GerenciadorPortaAvioes>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private Vector3 AjustarPosicaoAoSolo(Vector3 posicao)
    {
        float altura = ObterAlturaSolo(posicao, posicao.y);
        posicao.y = altura + offsetAlturaSolo;
        return posicao;
    }

    private static bool PontoValido(Vector3 ponto)
    {
        return !float.IsNaN(ponto.x) && !float.IsNaN(ponto.y) && !float.IsNaN(ponto.z)
            && !float.IsInfinity(ponto.x) && !float.IsInfinity(ponto.y) && !float.IsInfinity(ponto.z);
    }

    private void AderirAoSolo()
    {
        transform.position = AjustarPosicaoAoSolo(transform.position);
    }

    private float ObterAlturaSolo(Vector3 posicao, float fallback)
    {
        Ray ray = new Ray(posicao + Vector3.up * 400f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float menorDistancia = float.MaxValue;
        float melhorAltura = fallback;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == null)
            {
                continue;
            }

            if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].distance < menorDistancia)
            {
                menorDistancia = hits[i].distance;
                melhorAltura = hits[i].point.y;
            }
        }

        return menorDistancia < float.MaxValue ? melhorAltura : fallback;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, raioBuscaCarga);

        if (pontosCarga == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < pontosCarga.Length; i++)
        {
            if (pontosCarga[i] != null)
            {
                Gizmos.DrawWireCube(pontosCarga[i].position, new Vector3(2f, 2f, 3f));
            }
        }
    }
}
