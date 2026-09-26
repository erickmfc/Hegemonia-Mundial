using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador exclusivo do avião de transporte pesado.
/// Caças, bombardeiros e as demais aeronaves continuam usando os próprios
/// controladores. O C700 só pousa em MiniPistaLogistica ou no aeroporto de
/// origem, que possui sua própria infraestrutura de pista.
/// </summary>
public class C700TransporteAereo : MonoBehaviour
{
    public enum EstadoC700
    {
        Solo,
        Taxiando,
        Decolando,
        EmVoo,
        Aproximando,
        Alinhando,
        Pousando,
        Estacionado,
        Carregando,
        FalhaMissao
    }

    [Serializable]
    public class EntradaManifesto
    {
        public string nome = "Soldados";
        public TipoUnidade tipoUnidade = TipoUnidade.Infantaria;
        public GameObject prefabDesembarque;
        public int quantidade;
        public int ajusteRapido = 10;
        public int ajustePesado = 50;
        public int quantidadeMaxima = 200;
    }

    [Serializable]
    private sealed class CargaFisica
    {
        public GameObject unidade;
    }

    [Header("Estado")]
    public EstadoC700 estadoAtual = EstadoC700.Solo;
    public bool debugLogs;

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

    [Header("Seguranca")]
    [Min(5f)] public float timeoutPorPontoAereo = 45f;
    [Min(0.05f)] public float deslocamentoMinimoAereo = 0.25f;
    [Min(1)] public int maxTentativasPouso = 3;

    [Header("Visual")]
    public Transform modeloVisual;
    public float bankMaximo = 28f;
    public float pitchMaximo = 14f;
    public float suavizacaoVisual = 3.5f;
    public bool modeloInvertido180;

    [Header("Carga")]
    public Transform[] pontosCarga;
    [Min(1)] public int capacidadeMaxima = 30;
    public bool ocultarCargaInterna = true;
    [Min(1f)] public float raioBuscaCarga = 100f;
    public float atrasoEntreEmbarques = 0.75f;
    public float distanciaDesembarque = 50f;
    public float raioBloqueioPortaAvioes = 25f;

    [Header("Manifesto")]
    public List<EntradaManifesto> manifestoConfigurado = new List<EntradaManifesto>();
    public float espacamentoManifestoInfantaria = 3.2f;
    public float espacamentoManifestoVeiculos = 7.5f;
    public int spawnsPorQuadroManifesto = 8;
    public float pausaEntreLotesManifesto = 0.02f;

    [Header("Combate")]
    public bool desabilitarArmasAoIniciar = true;

    [Header("Marcadores")]
    public float alturaMarcadorMissao = 18f;
    public float escalaMarcadorMissao = 2.8f;
    public float espessuraLinhaMissao = 0.22f;
    public Color corPousoMissao = new Color(0.15f, 0.95f, 1f, 0.45f);
    public Color corParadaMissao = new Color(1f, 0.85f, 0.2f, 0.45f);

    private readonly List<CargaFisica> cargaFisica = new List<CargaFisica>(32);
    private ControleUnidade controleUnidade;
    private ControleAviao controleAviaoLegado;
    private CombustivelUnidade combustivel;
    private TransportLandingController landingController;
    private TransportMission missao;
    private MiniPistaLogistica pistaDestino;
    private MiniPistaLogistica pistaOperacional;
    private bool aguardandoDestinoAereo;
    private bool retornoBase;
    private bool arremetida;
    private bool taxiExterno;
    private bool emRotaSaida;
    private MiniPistaLogistica pistaSaidaAtual;
    private bool temDestinoVisual;
    private Vector3 destinoVisualAtual;
    private Vector3 destinoBase;
    private Vector3 alvoDecolagem;
    private float inicioFase;
    private float prazoFase;
    private int tropasArmazenadas;
    private Coroutine rotinaCarga;
    private Quaternion rotacaoModeloBase = Quaternion.identity;

    public bool EstaNoSolo => estadoAtual == EstadoC700.Solo
        || estadoAtual == EstadoC700.Estacionado
        || estadoAtual == EstadoC700.Carregando;
    public bool AguardandoDestinoAereo => aguardandoDestinoAereo;
    public List<EntradaManifesto> ManifestoConfigurado => manifestoConfigurado;
    public int QuantidadeCargaAtual => cargaFisica.Count + tropasArmazenadas;
    public int CapacidadeCargaAtual => Mathf.Max(1, capacidadeMaxima);
    public int QuantidadeManifestoTotal => CalcularQuantidadeManifestoTotal();
    public bool TemDestinoVisual => temDestinoVisual;
    public Vector3 DestinoVisualAtual => destinoVisualAtual;
    public MiniPistaLogistica PistaDestinoAtual => pistaDestino;
    public MiniPistaLogistica PistaOperacionalAtual => pistaOperacional;
    public int TropasArmazenadas => tropasArmazenadas;
    public bool RunwayReserved => landingController != null && landingController.RunwayReserved;

    private void Awake()
    {
        controleUnidade = GetComponent<ControleUnidade>();
        controleAviaoLegado = GetComponent<ControleAviao>();
        combustivel = CombustivelUnidade.Garantir(gameObject, true);
        landingController = GetComponent<TransportLandingController>();
        if (landingController == null) landingController = gameObject.AddComponent<TransportLandingController>();
        landingController.maxTentativas = maxTentativasPouso;

        if (controleAviaoLegado != null) controleAviaoLegado.enabled = false;
        if (modeloVisual != null) rotacaoModeloBase = modeloVisual.localRotation;
        GarantirManifestoPadrao();
    }

    private void Start()
    {
        if (aeroportoOrigem == null) aeroportoOrigem = GetComponentInParent<GerenciadorAeroporto>();
        DefinirEstado(EstadoC700.Solo);
    }

    private void OnDisable()
    {
        landingController?.Release();
        pistaOperacional?.ReleaseParking(this);
    }

    private void Update()
    {
        if (controleAviaoLegado != null && controleAviaoLegado.enabled) controleAviaoLegado.enabled = false;
        if (combustivel == null) combustivel = GetComponent<CombustivelUnidade>();
        if (combustivel != null && !combustivel.PodeOperar)
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        switch (estadoAtual)
        {
            case EstadoC700.Decolando:
            case EstadoC700.EmVoo:
            case EstadoC700.Aproximando:
            case EstadoC700.Alinhando:
            case EstadoC700.Pousando:
                ProcessarVoo();
                break;
            case EstadoC700.Taxiando:
                if (!taxiExterno) ProcessarTaxi();
                break;
            case EstadoC700.Estacionado:
                ProcessarPistaEstacionado();
                break;
        }

        AtualizarVisualVoo();
    }

    public void DefinirAeroportoOrigem(GerenciadorAeroporto novoAeroporto)
    {
        aeroportoOrigem = novoAeroporto;
    }

    public void RegistrarPontoEstacionamento(Transform ponto)
    {
        pontoEstacionamentoPreferencial = ponto;
        if (ponto != null)
        {
            transform.position = ponto.position;
            transform.rotation = ponto.rotation;
        }
    }

    public void FinalizarPosicionamentoNoPatio(Transform ponto)
    {
        RegistrarPontoEstacionamento(ponto);
        DefinirEstado(EstadoC700.Solo);
        retornoBase = false;
    }

    public IEnumerator TaxiarAteTransform(Transform ponto)
    {
        if (ponto == null) yield break;
        taxiExterno = true;
        DefinirEstado(EstadoC700.Taxiando);
        while (ponto != null && Vector3.Distance(transform.position, ponto.position) > raioChegadaSolo)
        {
            MoverSolo(ponto.position);
            yield return null;
        }
        if (ponto != null)
        {
            transform.position = ponto.position;
            transform.rotation = ponto.rotation;
        }
        taxiExterno = false;
        DefinirEstado(EstadoC700.Solo);
    }

    public void ReceberOrdemMover(Vector3 destino)
    {
        if (EstaNaFaseProtegida())
        {
            MostrarMensagem("Ordem ignorada: o C700 esta em procedimento de pouso/decolagem protegido.");
            return;
        }
        int teamId = ObterTeamId();
        MiniPistaLogistica pista = MiniPistaLogistica.LocalizarMaisProxima(destino, 300f, teamId, false);
        if (pista == null || !pista.PodeReceber(this, destino))
        {
            MostrarMensagem("C700 recusou o destino: nenhuma Mini Pista Logistica operacional foi encontrada.");
            return;
        }

        missao = null;
        retornoBase = false;
        pistaSaidaAtual = pistaOperacional;
        pistaOperacional?.ReleaseParking(this);
        pistaOperacional = null;
        pistaDestino = pista;
        temDestinoVisual = true;
        destinoVisualAtual = pista.parkingPoint.position;
        if (!IniciarVooParaPista(pista))
        {
            pistaDestino = null;
            pistaSaidaAtual = null;
            temDestinoVisual = false;
            DefinirEstado(EstadoC700.Solo);
        }
    }

    public bool IniciarTransportMission(MiniPistaLogistica pickup, MiniPistaLogistica delivery)
    {
        if (EstaNaFaseProtegida())
        {
            MostrarMensagem("TransportMission recusada: o C700 esta em uma fase protegida.");
            return false;
        }
        if (pickup == null || delivery == null || pickup == delivery)
        {
            MostrarMensagem("TransportMission recusada: pista de embarque ou destino invalido.");
            return false;
        }

        missao = new TransportMission(pickup, delivery);
        if (!pickup.PistaValida || !delivery.PistaValida)
        {
            MostrarMensagem("TransportMission recusada: uma das pistas esta invalida.");
            missao = null;
            return false;
        }
        pistaOperacional?.ReleaseParking(this);
        pistaSaidaAtual = pistaOperacional;
        pistaOperacional = null;
        pistaDestino = pickup;
        retornoBase = false;
        temDestinoVisual = true;
        destinoVisualAtual = pickup.parkingPoint.position;
        if (!IniciarVooParaPista(pickup))
        {
            missao = null;
            pistaDestino = null;
            pistaSaidaAtual = null;
            temDestinoVisual = false;
            DefinirEstado(EstadoC700.Solo);
            return false;
        }
        return true;
    }

    public void OrdenarTaxiSolo(Vector3 destino)
    {
        if (!EstaNoSolo) return;
        StopAllCoroutines();
        StartCoroutine(TaxiarParaPosicao(destino));
    }

    public void OrdenarRetornoAoAeroporto()
    {
        landingController?.Release();
        pistaOperacional?.ReleaseParking(this);
        pistaOperacional = null;
        missao = null;
        pistaDestino = null;
        retornoBase = true;
        aguardandoDestinoAereo = false;
        destinoBase = ObterDestinoBase();
        temDestinoVisual = true;
        destinoVisualAtual = destinoBase;

        if (Vector3.Distance(transform.position, destinoBase) <= 350f)
        {
            transform.position = destinoBase;
            DefinirEstado(EstadoC700.Solo);
            return;
        }

        DefinirEstado(EstadoC700.Decolando);
        MarcarFase(ObterDestinoBase(), velocidadeCruzeiro);
    }

    public void ArmarModoAereo()
    {
        if (!EstaNoSolo) return;
        aguardandoDestinoAereo = true;
        MostrarMensagem("Selecione uma Mini Pista Logistica operacional.");
    }

    public void CancelarModoAereo()
    {
        aguardandoDestinoAereo = false;
        if (estadoAtual == EstadoC700.Aproximando || estadoAtual == EstadoC700.Alinhando || estadoAtual == EstadoC700.Pousando)
        {
            landingController?.AbortLanding();
            DefinirEstado(EstadoC700.Estacionado);
        }
    }

    public void PrepararMissaoAerea()
    {
        ArmarModoAereo();
    }

    public void PuxarUnidadesProximas()
    {
        if (!EstaNoSolo || rotinaCarga != null) return;
        rotinaCarga = StartCoroutine(RotinaPuxarUnidades());
    }

    public void DesembarcarTudo()
    {
        Vector3 ponto = pistaDestino != null && pistaDestino.troopUnloadPoint != null
            ? pistaDestino.troopUnloadPoint.position
            : transform.position + transform.forward * Mathf.Max(8f, distanciaDesembarque);

        for (int i = cargaFisica.Count - 1; i >= 0; i--)
        {
            GameObject unidade = cargaFisica[i].unidade;
            if (unidade != null)
            {
                unidade.transform.position = ponto + Vector3.right * (i * 2f);
                unidade.SetActive(true);
            }
            cargaFisica.RemoveAt(i);
        }

        tropasArmazenadas = 0;
        MostrarMensagem("Carga desembarcada.");
    }

    public void DesembarcarManifestoConfigurado()
    {
        Vector3 ponto = pistaDestino != null && pistaDestino.troopUnloadPoint != null
            ? pistaDestino.troopUnloadPoint.position
            : transform.position + transform.forward * Mathf.Max(8f, distanciaDesembarque);
        int indice = 0;

        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            EntradaManifesto entrada = manifestoConfigurado[i];
            if (entrada == null || entrada.prefabDesembarque == null || entrada.quantidade <= 0) continue;
            int quantidade = Mathf.Min(entrada.quantidade, entrada.quantidadeMaxima > 0 ? entrada.quantidadeMaxima : entrada.quantidade);
            for (int n = 0; n < quantidade; n++)
            {
                Vector3 spawn = ponto + transform.right * ((indice % 8) * 3f) + transform.forward * ((indice / 8) * 3f);
                Instantiate(entrada.prefabDesembarque, spawn, transform.rotation);
                indice++;
            }
            entrada.quantidade = 0;
        }

        tropasArmazenadas = 0;
        MostrarMensagem("Manifesto desembarcado.");
    }

    public void AjustarManifesto(int indice, int delta)
    {
        if (indice < 0 || indice >= manifestoConfigurado.Count) return;
        EntradaManifesto entrada = manifestoConfigurado[indice];
        entrada.quantidade = Mathf.Clamp(entrada.quantidade + delta, 0, Mathf.Max(0, entrada.quantidadeMaxima));
    }

    public void LimparManifestoConfigurado()
    {
        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            if (manifestoConfigurado[i] != null) manifestoConfigurado[i].quantidade = 0;
        }
    }

    public void PararPorFaltaDeCombustivel()
    {
        landingController?.Release();
        aguardandoDestinoAereo = false;
        retornoBase = false;
        missao = null;
        pistaDestino = null;
        DefinirEstado(EstadoC700.FalhaMissao);
        MostrarMensagem("C700 parado: combustivel insuficiente.");
    }

    private bool IniciarVooParaPista(MiniPistaLogistica pista)
    {
        if (pista == null || !pista.PistaValida)
        {
            MostrarMensagem("Mini Pista invalida ou inoperante.");
            return false;
        }
        if (!landingController.TryReserve(pista, this))
        {
            MostrarMensagem("Mini Pista ocupada por outro aviao.");
            return false;
        }
        if (!PossuiCombustivelParaPista(pista))
        {
            landingController.Release();
            MostrarMensagem("C700 nao possui combustivel para ida, pouso e reserva de retorno.");
            return false;
        }
        if (combustivel != null && !combustivel.PodeOperar)
        {
            landingController.Release();
            MostrarMensagem("C700 sem combustivel suficiente para decolar.");
            return false;
        }

        aguardandoDestinoAereo = false;
        arremetida = false;
        landingController.ResetAttempts();
        if (pistaSaidaAtual != null && !pistaSaidaAtual.PistaValida)
        {
            pistaSaidaAtual = null;
        }
        emRotaSaida = pistaSaidaAtual != null;
        alvoDecolagem = emRotaSaida
            ? pistaSaidaAtual.takeoffPoint.position + Vector3.up * Mathf.Max(altitudeCruzeiro, 40f)
            : transform.position + Vector3.up * Mathf.Max(altitudeCruzeiro, 40f);
        MarcarFase(alvoDecolagem, velocidadeDecolagem);
        DefinirEstado(EstadoC700.Decolando);
        return true;
    }

    private void ProcessarVoo()
    {
        if (retornoBase)
        {
            ProcessarRetornoBase();
            return;
        }
        if (pistaDestino == null || !pistaDestino.PistaValida)
        {
            FalharMissaoERetornar();
            return;
        }

        if (Time.time - inicioFase > prazoFase)
        {
            AbortLanding();
            return;
        }

        if (estadoAtual == EstadoC700.Decolando)
        {
            MoverAereo(alvoDecolagem, velocidadeDecolagem);
            if (Vector3.Distance(transform.position, alvoDecolagem) <= 12f)
            {
                DefinirEstado(EstadoC700.EmVoo);
                Vector3 alvoEmVoo = emRotaSaida && pistaSaidaAtual != null
                    ? pistaSaidaAtual.exitPoint.position
                    : pistaDestino.approachPoint.position;
                MarcarFase(alvoEmVoo, velocidadeCruzeiro);
            }
            return;
        }

        if (estadoAtual == EstadoC700.EmVoo)
        {
            Vector3 alvo = arremetida
                ? pistaDestino.goAroundPoint.position
                : emRotaSaida && pistaSaidaAtual != null
                    ? pistaSaidaAtual.exitPoint.position
                    : pistaDestino.approachPoint.position;
            MoverAereo(alvo, velocidadeCruzeiro);
            if (Vector3.Distance(transform.position, alvo) <= Mathf.Max(8f, distanciaAproximacao * 0.18f))
            {
                if (arremetida)
                {
                    arremetida = false;
                    if (!landingController.TryReserve(pistaDestino, this))
                    {
                        FalharMissaoERetornar();
                        return;
                    }
                }
                if (emRotaSaida)
                {
                    emRotaSaida = false;
                    pistaSaidaAtual = null;
                    MarcarFase(pistaDestino.approachPoint.position, velocidadeCruzeiro);
                    return;
                }
                DefinirEstado(EstadoC700.Aproximando);
                MarcarFase(pistaDestino.landingPoint.position, Mathf.Max(velocidadeCruzeiro * 0.65f, 25f));
            }
            return;
        }

        if (estadoAtual == EstadoC700.Aproximando)
        {
            MoverAereo(pistaDestino.landingPoint.position, Mathf.Max(velocidadeCruzeiro * 0.65f, 25f));
            if (Vector3.Distance(transform.position, pistaDestino.landingPoint.position) <= 10f)
            {
                DefinirEstado(EstadoC700.Alinhando);
                MarcarFase(pistaDestino.runwayStart.position + Vector3.up * alturaToqueSolo, Mathf.Max(velocidadeDecolagem * 0.75f, 20f));
            }
            return;
        }

        if (estadoAtual == EstadoC700.Alinhando)
        {
            Vector3 alvo = pistaDestino.runwayStart.position + Vector3.up * alturaToqueSolo;
            MoverAereo(alvo, Mathf.Max(velocidadeDecolagem * 0.75f, 20f));
            if (Vector3.Distance(transform.position, alvo) <= 7f)
            {
                DefinirEstado(EstadoC700.Pousando);
                MarcarFase(pistaDestino.runwayEnd.position + Vector3.up * offsetAlturaSolo, Mathf.Max(velocidadeTaxi, 8f));
            }
            return;
        }

        if (estadoAtual == EstadoC700.Pousando)
        {
            Vector3 alvo = pistaDestino.runwayEnd.position + Vector3.up * offsetAlturaSolo;
            MoverAereo(alvo, Mathf.Max(velocidadeTaxi, 8f));
            if (Vector3.Distance(transform.position, alvo) <= raioChegadaSolo)
            {
                DefinirEstado(EstadoC700.Taxiando);
                MarcarFase(pistaDestino.parkingPoint.position, velocidadeTaxi);
            }
        }
    }

    private void ProcessarTaxi()
    {
        if (pistaDestino == null || pistaDestino.parkingPoint == null)
        {
            DefinirEstado(EstadoC700.Solo);
            return;
        }
        MoverSolo(pistaDestino.parkingPoint.position);
        if (Vector3.Distance(transform.position, pistaDestino.parkingPoint.position) <= raioChegadaSolo)
        {
            if (!pistaDestino.TryReserveParking(this))
            {
                AbortLanding();
                return;
            }
            transform.position = pistaDestino.parkingPoint.position;
            DefinirEstado(EstadoC700.Estacionado);
            MarcarFase(pistaDestino.parkingPoint.position, velocidadeTaxi);
        }
    }

    private void ProcessarPistaEstacionado()
    {
        if (pistaOperacional == pistaDestino) return;
        pistaOperacional = pistaDestino;
        landingController.Release();

        if (missao != null && missao.pickupPista == pistaDestino && missao.carregarNaOrigem)
        {
            DefinirEstado(EstadoC700.Carregando);
            CarregarDaPista(pistaDestino);
            if (missao.deliveryPista != null)
            {
                pistaDestino.ReleaseParking(this);
                pistaDestino = missao.deliveryPista;
                destinoVisualAtual = pistaDestino.parkingPoint.position;
                if (!IniciarVooParaPista(pistaDestino))
                {
                    missao = null;
                    pistaDestino = null;
                    temDestinoVisual = false;
                    DefinirEstado(EstadoC700.Solo);
                }
            }
            return;
        }

        if (missao != null && missao.deliveryPista == pistaDestino && missao.descarregarNoDestino)
        {
            DesembarcarTudo();
            missao = null;
            pistaDestino.ReleaseParking(this);
            OrdenarRetornoAoAeroporto();
        }
    }

    private void ProcessarRetornoBase()
    {
        Vector3 alvoVoo = destinoBase + Vector3.up * Mathf.Max(altitudeCruzeiro, 40f);
        if (estadoAtual == EstadoC700.Decolando)
        {
            MoverAereo(alvoVoo, velocidadeCruzeiro);
            if (Vector3.Distance(transform.position, alvoVoo) <= 15f)
            {
                DefinirEstado(EstadoC700.EmVoo);
                MarcarFase(destinoBase, velocidadeCruzeiro);
            }
            return;
        }

        MoverAereo(alvoVoo, velocidadeCruzeiro);
        if (Vector3.Distance(transform.position, alvoVoo) <= 15f)
        {
            transform.position = destinoBase;
            DefinirEstado(EstadoC700.Solo);
            retornoBase = false;
            temDestinoVisual = false;
        }
    }

    private void AbortLanding()
    {
        if (landingController == null || landingController.RegisterLandingAttempt())
        {
            landingController?.Release();
            arremetida = true;
            DefinirEstado(EstadoC700.EmVoo);
            MarcarFase(pistaDestino != null ? pistaDestino.goAroundPoint.position : transform.position + Vector3.up * 50f, velocidadeCruzeiro);
            MostrarMensagem("Pouso abortado; fazendo arremetida para nova aproximacao.");
            return;
        }

        MostrarMensagem("Tres tentativas de pouso falharam; retornando ao aeroporto.");
        FalharMissaoERetornar();
    }

    private void FalharMissaoERetornar()
    {
        landingController?.AbortLanding();
        missao = null;
        pistaDestino = null;
        OrdenarRetornoAoAeroporto();
    }

    private void MoverAereo(Vector3 alvo, float velocidade)
    {
        Vector3 antes = transform.position;
        float multiplicador = controleUnidade != null ? controleUnidade.MultiplicadorVelocidadeComandoHud : 1f;
        transform.position = Vector3.MoveTowards(transform.position, alvo, Mathf.Max(1f, velocidade * multiplicador) * Time.deltaTime);
        Vector3 delta = transform.position - antes;
        if (delta.sqrMagnitude > deslocamentoMinimoAereo * deslocamentoMinimoAereo)
        {
            Quaternion desejada = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desejada, giroVoo * Time.deltaTime);
        }
    }

    private void MoverSolo(Vector3 alvo)
    {
        Vector3 destino = new Vector3(alvo.x, alvo.y + offsetAlturaSolo, alvo.z);
        Vector3 delta = destino - transform.position;
        float multiplicador = controleUnidade != null ? controleUnidade.MultiplicadorVelocidadeComandoHud : 1f;
        transform.position = Vector3.MoveTowards(transform.position, destino, Mathf.Max(1f, velocidadeTaxi * multiplicador) * Time.deltaTime);
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.01f)
        {
            Quaternion desejada = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desejada, giroSolo * Time.deltaTime);
        }
    }

    private void MarcarFase(Vector3 alvo, float velocidade)
    {
        inicioFase = Time.time;
        float distancia = Vector3.Distance(transform.position, alvo);
        float tempoEstimado = distancia / Mathf.Max(1f, velocidade);
        prazoFase = Mathf.Max(timeoutPorPontoAereo, tempoEstimado * 2f + 10f);
    }

    private bool PossuiCombustivelParaPista(MiniPistaLogistica pista)
    {
        if (combustivel == null || !combustivel.usaCombustivel || combustivel.combustivelInfinito) return true;
        if (pista == null || pista.approachPoint == null || pista.parkingPoint == null) return false;

        float distancia = Vector3.Distance(transform.position, pista.approachPoint.position)
            + Vector3.Distance(pista.approachPoint.position, pista.landingPoint.position)
            + Vector3.Distance(pista.landingPoint.position, pista.runwayStart.position)
            + Vector3.Distance(pista.runwayStart.position, pista.runwayEnd.position)
            + Vector3.Distance(pista.runwayEnd.position, pista.parkingPoint.position);
        MiniPistaLogistica origem = pistaSaidaAtual != null ? pistaSaidaAtual : pistaOperacional;
        if (origem != null && origem.PistaValida && origem.takeoffPoint != null && origem.exitPoint != null)
        {
            distancia = Vector3.Distance(transform.position, origem.takeoffPoint.position)
                + Vector3.Distance(origem.takeoffPoint.position, origem.exitPoint.position)
                + Vector3.Distance(origem.exitPoint.position, pista.approachPoint.position)
                + Vector3.Distance(pista.approachPoint.position, pista.landingPoint.position)
                + Vector3.Distance(pista.landingPoint.position, pista.runwayStart.position)
                + Vector3.Distance(pista.runwayStart.position, pista.runwayEnd.position)
                + Vector3.Distance(pista.runwayEnd.position, pista.parkingPoint.position);
        }
        float consumoEstimado = combustivel.EstimarConsumoParaDistancia(distancia, Mathf.Max(1f, velocidadeCruzeiro));
        float distanciaRetorno = Vector3.Distance(pista.parkingPoint.position, ObterDestinoBase());
        float consumoRetorno = combustivel.EstimarConsumoParaDistancia(distanciaRetorno, Mathf.Max(1f, velocidadeCruzeiro));
        float reserva = Mathf.Max(combustivel.Capacidade * reservaRetornoPercentual, consumoRetorno);
        return combustivel.CombustivelAtual >= consumoEstimado + reserva;
    }

    private IEnumerator TaxiarParaPosicao(Vector3 destino)
    {
        taxiExterno = true;
        DefinirEstado(EstadoC700.Taxiando);
        while (Vector3.Distance(transform.position, destino) > raioChegadaSolo)
        {
            MoverSolo(destino);
            yield return null;
        }
        taxiExterno = false;
        DefinirEstado(EstadoC700.Solo);
    }

    private IEnumerator RotinaPuxarUnidades()
    {
        ControleUnidade[] unidades = FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length && QuantidadeCargaAtual < CapacidadeCargaAtual; i++)
        {
            ControleUnidade unidade = unidades[i];
            if (unidade == null || unidade.gameObject == gameObject || !unidade.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(transform.position, unidade.transform.position) > raioBuscaCarga) continue;
            IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
            if (identidade != null && identidade.teamID != ObterTeamId()) continue;
            cargaFisica.Add(new CargaFisica { unidade = unidade.gameObject });
            unidade.gameObject.SetActive(false);
            yield return new WaitForSeconds(Mathf.Max(0f, atrasoEntreEmbarques));
        }
        rotinaCarga = null;
        MostrarMensagem("Embarque concluido: " + QuantidadeCargaAtual + "/" + CapacidadeCargaAtual);
    }

    private void CarregarDaPista(MiniPistaLogistica pista)
    {
        if (pista == null) return;
        int limite = Mathf.Max(0, CapacidadeCargaAtual - QuantidadeCargaAtual);
        tropasArmazenadas += pista.RetirarTropas(limite);
        MostrarMensagem("Tropas aguardando na pista embarcadas: " + tropasArmazenadas);
    }

    private void PrepararManifestoPadraoInterno()
    {
        if (manifestoConfigurado == null) manifestoConfigurado = new List<EntradaManifesto>();
        if (manifestoConfigurado.Count > 0) return;
        manifestoConfigurado.Add(new EntradaManifesto { nome = "Soldados", tipoUnidade = TipoUnidade.Infantaria, quantidadeMaxima = 300 });
        manifestoConfigurado.Add(new EntradaManifesto { nome = "Veiculos", tipoUnidade = TipoUnidade.Veiculo, quantidadeMaxima = 120 });
    }

    private void GarantirManifestoPadrao()
    {
        PrepararManifestoPadraoInterno();
    }

    private int CalcularQuantidadeManifestoTotal()
    {
        int total = 0;
        if (manifestoConfigurado == null) return 0;
        for (int i = 0; i < manifestoConfigurado.Count; i++)
        {
            if (manifestoConfigurado[i] != null) total += Mathf.Max(0, manifestoConfigurado[i].quantidade);
        }
        return total;
    }

    private int ObterTeamId()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        return identidade != null ? identidade.teamID : 1;
    }

    private Vector3 ObterDestinoBase()
    {
        if (aeroportoOrigem != null)
        {
            if (aeroportoOrigem.wpPronto != null) return aeroportoOrigem.wpPronto.position;
            if (aeroportoOrigem.hangarAviao != null) return aeroportoOrigem.hangarAviao.position;
            return aeroportoOrigem.transform.position;
        }
        return transform.position;
    }

    private void DefinirEstado(EstadoC700 novoEstado)
    {
        estadoAtual = novoEstado;
        AudioRuntime.DefinirMotorAereo(gameObject, novoEstado != EstadoC700.Solo && novoEstado != EstadoC700.Estacionado && novoEstado != EstadoC700.FalhaMissao);
    }

    private void AtualizarVisualVoo()
    {
        if (modeloVisual == null) return;
        bool noAr = estadoAtual == EstadoC700.Decolando || estadoAtual == EstadoC700.EmVoo || estadoAtual == EstadoC700.Aproximando || estadoAtual == EstadoC700.Alinhando || estadoAtual == EstadoC700.Pousando;
        if (!noAr)
        {
            modeloVisual.localRotation = rotacaoModeloBase;
            return;
        }
        float bank = Mathf.Clamp(Vector3.Dot(transform.right, Vector3.forward) * bankMaximo, -bankMaximo, bankMaximo);
        modeloVisual.localRotation = rotacaoModeloBase * Quaternion.Euler(0f, 0f, bank);
    }

    private void MostrarMensagem(string mensagem)
    {
        if (debugLogs) Debug.Log("[C700] " + mensagem, this);
    }

    private bool EstaNaFaseProtegida()
    {
        return estadoAtual == EstadoC700.Decolando
            || estadoAtual == EstadoC700.EmVoo
            || estadoAtual == EstadoC700.Aproximando
            || estadoAtual == EstadoC700.Alinhando
            || estadoAtual == EstadoC700.Pousando
            || estadoAtual == EstadoC700.Taxiando;
    }
}
