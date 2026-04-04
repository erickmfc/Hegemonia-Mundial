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

    [Header("Visual")]
    public Transform modeloVisual;
    public float bankMaximo = 28f;
    public float pitchMaximo = 14f;
    public float suavizacaoVisual = 3.5f;

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

    private readonly List<SlotCarga> slots = new List<SlotCarga>();
    private ControleUnidade controleUnidade;
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
    private bool retornoMissaoProgramado;

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
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (modeloVisual == null && transform.childCount > 0)
        {
            modeloVisual = transform.GetChild(0);
        }

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

    private void Update()
    {
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

        Rect area = new Rect(Screen.width - 430f, Screen.height * 0.08f, 400f, 650f);
        GUI.Box(area, "C700 - Transporte");
        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 28f, area.width - 24f, area.height - 40f));
        scrollMenuCarga = GUILayout.BeginScrollView(scrollMenuCarga);

        GUILayout.Label("Estado: " + estadoAtual);
        GUILayout.Label("Carga real: " + QuantidadeCargas() + "/" + slots.Count);
        GUILayout.Label("Manifesto: " + QuantidadeManifestoTotal);

        if (temDestinoVisual)
        {
            GUILayout.Label(string.Format("Destino: X {0:0} / Z {1:0}", destinoVisualAtual.x, destinoVisualAtual.z));
        }

        string textoOrdem = temDestinoMissaoProgramado
            ? "Destino travado. O C700 vai terminar o taxi e decolar sozinho."
            : aguardandoDestinoAereo
                ? "Modo aereo ativo. Clique direito no mapa para definir a missao."
                : "Clique direito no chao: taxi. Z ou botao abaixo: missao aerea.";
        GUILayout.Label(textoOrdem);

        GUI.enabled = EstaNoSolo;
        if (GUILayout.Button("Preparar decolagem / escolher destino", GUILayout.Height(34f)))
        {
            PrepararMissaoAerea();
        }
        GUI.enabled = EstaNoSolo && rotinaCarga == null;
        if (GUILayout.Button("Puxar unidades proximas (I)", GUILayout.Height(34f)))
        {
            PuxarUnidadesProximas();
        }

        GUI.enabled = EstaNoSolo;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desembarcar carga", GUILayout.Height(32f)))
        {
            DesembarcarTudo();
        }
        if (GUILayout.Button("Desembarcar manifesto", GUILayout.Height(32f)))
        {
            DesembarcarManifestoConfigurado();
        }
        GUILayout.EndHorizontal();

        GUI.enabled = EstaNoSolo;
        if (GUILayout.Button("Limpar manifesto", GUILayout.Height(30f)))
        {
            LimparManifestoConfigurado();
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

        GUILayout.Space(10f);
        GUILayout.Label("Manifesto configuravel");
        DesenharManifestoConfiguravel();

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
        estadoAtual = EstadoC700.Solo;
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
        estadoAtual = EstadoC700.Solo;
        LimparMissaoProgramada();
        LimparDestinoVisual();
    }

    public void ReceberOrdemMover(Vector3 destino)
    {
        if (aguardandoDestinoAereo)
        {
            OrdenarVoo(destino, false);
            return;
        }

        if (!EstaNoSolo)
        {
            OrdenarVoo(destino, false);
            return;
        }

        OrdenarTaxiSolo(destino);
    }

    public void OrdenarTaxiSolo(Vector3 destino)
    {
        if (!EstaNoSolo)
        {
            return;
        }

        RegistrarDestinoVisual(destino);
        IniciarRotinaMovimento(TaxiarAtePosicao(destino, velocidadeTaxi));
    }

    public void OrdenarRetornoAoAeroporto()
    {
        aguardandoDestinoAereo = false;

        Vector3 destinoRetorno = ObterDestinoDeRetorno();
        if (destinoRetorno == Vector3.zero)
        {
            MostrarMensagem("Sem aeroporto de retorno configurado.");
            LogDebug("Sem aeroporto ou ponto de retorno configurado.");
            return;
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
        MostrarMensagem("Missao armada. Clique com o botao direito no destino.");
        IniciarRotinaMovimento(RotinaPreparacaoParaDecolagem());
    }

    public void PuxarUnidadesProximas()
    {
        if (!EstaNoSolo || rotinaCarga != null)
        {
            return;
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
        if (DestinoEmPortaAvioes(destinoFinal))
        {
            MostrarMensagem("Destino invalido: porta-avioes bloqueado.");
            LogDebug("Destino cancelado: porta-avioes nao e ponto valido para o C700.");
            return;
        }

        aguardandoDestinoAereo = false;
        RegistrarDestinoVisual(destinoFinal);

        if (EstaNoSolo && rotinaMovimento != null && !prontoParaDecolarNaPista)
        {
            destinoMissaoProgramado = destinoFinal;
            temDestinoMissaoProgramado = true;
            retornoMissaoProgramado = retornoAoAeroporto;
            MostrarMensagem("Destino confirmado. Taxiando para decolar.");
            return;
        }

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

        if (EstaNoSolo)
        {
            yield return StartCoroutine(RotinaDecolagem());
        }

        Vector3 direcaoAproximacao = destinoSolo - transform.position;
        direcaoAproximacao.y = 0f;
        if (direcaoAproximacao.sqrMagnitude < 4f)
        {
            direcaoAproximacao = transform.forward;
        }
        direcaoAproximacao.Normalize();

        float alturaAproximacao = Mathf.Max(altitudeCruzeiro * 0.45f, 32f);
        Vector3 pontoAlto = destinoSolo - direcaoAproximacao * distanciaAproximacao + Vector3.up * alturaAproximacao;
        Vector3 pontoBaixo = destinoSolo - direcaoAproximacao * distanciaDescida + Vector3.up * Mathf.Max(alturaToqueSolo + 6f, 12f);
        Vector3 pontoToque = destinoSolo - direcaoAproximacao * distanciaRolagem + Vector3.up * alturaToqueSolo;

        estadoAtual = EstadoC700.EmVoo;
        yield return StartCoroutine(VoarAtePonto(pontoAlto, velocidadeCruzeiro, 18f));

        estadoAtual = EstadoC700.Aproximando;
        yield return StartCoroutine(VoarAtePonto(pontoBaixo, velocidadeCruzeiro * 0.72f, 12f));
        yield return StartCoroutine(VoarAtePonto(pontoToque, velocidadeDecolagem * 0.95f, 5.5f));

        estadoAtual = EstadoC700.Pousando;
        transform.position = new Vector3(transform.position.x, destinoSolo.y + alturaToqueSolo, transform.position.z);
        velocidadeSoloAtual = Mathf.Max(velocidadeTaxi * 1.8f, velocidadeDecolagem * 0.85f);

        yield return StartCoroutine(TaxiarAtePosicao(destinoSolo, velocidadeTaxi, velocidadeSoloAtual, retornoAoAeroporto));

        if (retornoAoAeroporto && pontoEstacionamentoPreferencial != null)
        {
            transform.rotation = pontoEstacionamentoPreferencial.rotation;
        }

        velocidadeAereaAtual = 0f;
        velocidadeSoloAtual = 0f;
        estadoAtual = EstadoC700.Solo;
        AtualizarDestinoVisualAoChegar(destinoSolo);
        rotinaMovimento = null;
    }

    private IEnumerator RotinaPreparacaoParaDecolagem()
    {
        if (aeroportoOrigem == null || aeroportoOrigem.waypointsDecolagem == null || aeroportoOrigem.waypointsDecolagem.Count == 0)
        {
            aguardandoDestinoAereo = true;
            MostrarMensagem("Sem pista configurada. Clique no mapa para tentar decolar daqui.");
            if (temDestinoMissaoProgramado)
            {
                Vector3 destinoAuto = destinoMissaoProgramado;
                bool retornoAuto = retornoMissaoProgramado;
                LimparMissaoProgramada();
                aguardandoDestinoAereo = false;
                yield return StartCoroutine(RotinaMissaoAerea(destinoAuto, retornoAuto));
            }
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
            MostrarMensagem("Pista incompleta. Clique no mapa para tentar decolar.");
            if (temDestinoMissaoProgramado)
            {
                Vector3 destinoAuto = destinoMissaoProgramado;
                bool retornoAuto = retornoMissaoProgramado;
                LimparMissaoProgramada();
                aguardandoDestinoAereo = false;
                yield return StartCoroutine(RotinaMissaoAerea(destinoAuto, retornoAuto));
            }
            rotinaMovimento = null;
            yield break;
        }

        for (int i = 0; i < indiceVoo; i++)
        {
            Transform wpTaxi = aeroportoOrigem.waypointsDecolagem[i];
            if (wpTaxi == null)
            {
                continue;
            }

            yield return StartCoroutine(TaxiarAtePosicao(wpTaxi.position, velocidadeTaxi + (i * 3f), -1f, false));
        }

        Transform wpVoo = aeroportoOrigem.waypointsDecolagem[indiceVoo];
        if (wpVoo != null)
        {
            Vector3 direcao = wpVoo.position - transform.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude > 0.2f)
            {
                transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
            }
        }

        estadoAtual = EstadoC700.Solo;
        aguardandoDestinoAereo = true;
        prontoParaDecolarNaPista = true;
        MostrarMensagem("C700 na pista. Clique com o botao direito no destino.");

        if (temDestinoMissaoProgramado)
        {
            Vector3 destinoAuto = destinoMissaoProgramado;
            bool retornoAuto = retornoMissaoProgramado;
            LimparMissaoProgramada();
            aguardandoDestinoAereo = false;
            yield return StartCoroutine(RotinaMissaoAerea(destinoAuto, retornoAuto));
        }

        rotinaMovimento = null;
    }

    private IEnumerator RotinaDecolagem()
    {
        bool usouPistaDoAeroporto = false;
        if (aeroportoOrigem != null && aeroportoOrigem.waypointsDecolagem != null && aeroportoOrigem.waypointsDecolagem.Count > 0)
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
                    for (int i = 0; i < indiceVoo; i++)
                    {
                        Transform wpTaxi = aeroportoOrigem.waypointsDecolagem[i];
                        if (wpTaxi == null)
                        {
                            continue;
                        }

                        yield return StartCoroutine(TaxiarAtePosicao(wpTaxi.position, velocidadeTaxi + (i * 3f), -1f, false));
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
            yield return StartCoroutine(CorridaDecolagemPara(transform.position + transform.forward * distanciaCorridaDecolagem));
        }

        estadoAtual = EstadoC700.EmVoo;
    }

    private IEnumerator CorridaDecolagemPara(Vector3 destinoSolo)
    {
        estadoAtual = EstadoC700.Decolando;
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
        while (transform.position.y < alturaAlvo - 1f || distanciaHorizontal < 65f)
        {
            Vector3 alvoSubida = transform.position + transform.forward * 85f + Vector3.up * 40f;
            AtualizarMovimentoAereo(alvoSubida, velocidadeCruzeiro * 0.72f);
            distanciaHorizontal = Vector3.Distance(new Vector3(inicioCorrida.x, 0f, inicioCorrida.z), new Vector3(transform.position.x, 0f, transform.position.z));
            yield return null;
        }
    }

    private IEnumerator VoarAtePonto(Vector3 alvo, float velocidadeAlvo, float distanciaParada)
    {
        float distanciaParadaSqr = distanciaParada * distanciaParada;
        while (true)
        {
            Vector3 dif = alvo - transform.position;
            if (dif.sqrMagnitude <= distanciaParadaSqr)
            {
                break;
            }

            AtualizarMovimentoAereo(alvo, velocidadeAlvo);
            yield return null;
        }
    }

    private void AtualizarMovimentoAereo(Vector3 alvo, float velocidadeAlvo)
    {
        Vector3 direcao = alvo - transform.position;
        if (direcao.sqrMagnitude > 0.05f)
        {
            Quaternion rotAlvo = Quaternion.LookRotation(direcao.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, giroVoo * Time.deltaTime);
        }

        velocidadeAereaAtual = Mathf.MoveTowards(velocidadeAereaAtual, velocidadeAlvo, aceleracaoVoo * Time.deltaTime);
        transform.position += transform.forward * velocidadeAereaAtual * Time.deltaTime;
    }

    private IEnumerator TaxiarAtePosicao(Vector3 destino, float velocidadeMaxima, float velocidadeInicial = -1f, bool atualizarEstacionamento = true)
    {
        estadoAtual = EstadoC700.Taxiando;
        if (velocidadeInicial >= 0f)
        {
            velocidadeSoloAtual = velocidadeInicial;
        }

        Vector3 destinoSolo = AjustarPosicaoAoSolo(destino);
        float raioSqr = raioChegadaSolo * raioChegadaSolo;

        while (true)
        {
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
        estadoAtual = EstadoC700.Solo;
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

    private IEnumerator RotinaPuxarUnidades()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, raioBuscaCarga, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        List<GameObject> fila = new List<GameObject>();

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

        NavMeshAgent agente = unidade.GetComponent<NavMeshAgent>();
        if (agente != null)
        {
            if (agente.isOnNavMesh)
            {
                agente.ResetPath();
            }
            agente.enabled = false;
        }

        Rigidbody rbUnidade = unidade.GetComponent<Rigidbody>();
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

        NavMeshAgent agente = unidade.GetComponent<NavMeshAgent>();
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
        modeloVisual.localRotation = rotacaoModeloBase * Quaternion.Euler(pitchVisualAtual, 0f, rollVisualAtual);
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

    private void RegistrarDestinoVisual(Vector3 destino)
    {
        destinoVisualAtual = AjustarPosicaoAoSolo(destino);
        temDestinoVisual = true;
    }

    private void LimparDestinoVisual()
    {
        temDestinoVisual = false;
        destinoVisualAtual = Vector3.zero;
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
        retornoMissaoProgramado = false;
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
        if (controle != null)
        {
            return controle.gameObject;
        }

        NavMeshAgent agente = hit.GetComponentInParent<NavMeshAgent>();
        if (agente != null)
        {
            return agente.gameObject;
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

        if (unidade.GetComponent<C700TransporteAereo>() != null)
        {
            return false;
        }

        IdentidadeUnidade minhaIdentidade = GetComponent<IdentidadeUnidade>();
        IdentidadeUnidade identidadeUnidade = unidade.GetComponent<IdentidadeUnidade>();
        if (minhaIdentidade != null && identidadeUnidade != null && identidadeUnidade.teamID != minhaIdentidade.teamID)
        {
            return false;
        }

        if (unidade.GetComponent<ControleAviao>() != null ||
            unidade.GetComponent<ControleAviaoCaca>() != null ||
            unidade.GetComponent<Helicoptero>() != null ||
            unidade.GetComponent<ControleNavioRealista>() != null ||
            unidade.GetComponent<ControleSubmarino>() != null ||
            unidade.GetComponent<HovercraftTransporte>() != null)
        {
            return false;
        }

        string nome = unidade.name.ToLowerInvariant();
        if (nome.Contains("uss") || nome.Contains("ship") || nome.Contains("porta") || nome.Contains("navio"))
        {
            return false;
        }

        SistemaDeDanos danos = unidade.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.unidadeBiologica)
        {
            return true;
        }

        if (unidade.GetComponent<NavMeshAgent>() != null)
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
        ComportamentoSeguir seguir = unidade.GetComponent<ComportamentoSeguir>();
        if (seguir != null)
        {
            Destroy(seguir);
        }

        ComportamentoSeguirUniversal seguirUniversal = unidade.GetComponent<ComportamentoSeguirUniversal>();
        if (seguirUniversal != null)
        {
            Destroy(seguirUniversal);
        }

        ComportamentoPatrulha patrulha = unidade.GetComponent<ComportamentoPatrulha>();
        if (patrulha != null)
        {
            Destroy(patrulha);
        }

        ComportamentoPatrulhaUniversal patrulhaUniversal = unidade.GetComponent<ComportamentoPatrulhaUniversal>();
        if (patrulhaUniversal != null)
        {
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
