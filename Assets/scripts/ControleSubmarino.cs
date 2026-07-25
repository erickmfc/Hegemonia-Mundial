using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controle do Submarino USS Leviathan com disparo manual, automatico e cristal de identificacao.
/// </summary>
public class ControleSubmarino : MonoBehaviour
{
    public enum ModoOperacao
    {
        Passivo,
        Manual,
        Automatico
    }

    [Header("Configuracao de Spawn")]
    [Tooltip("Quando ativo, o submarino nasce na superficie ao sair do estaleiro.")]
    public bool iniciarNaSuperficie = true;

    [Header("Configuracao de Profundidade")]
    [Tooltip("Profundidade quando submerso (valor relativo ao nivel da agua).")]
    public float profundidadeSubmersao = -15f;

    [Tooltip("Altura quando na superficie (valor relativo ao nivel da agua).")]
    public float alturaSuperificie = 0f;

    [Header("Ajuste de Altura")]
    [Tooltip("Ajuste fino do casco em relacao a agua. Negativo afunda mais, positivo levanta mais.")]
    public float offsetAlturaAgua = 0f;
    [Tooltip("Quando ativo, corrige automaticamente a linha d'agua do casco ao entrar na superficie.")]
    public bool calibrarAlturaAutomaticamente = true;
    [Range(0.2f, 0.85f)]
    [Tooltip("Percentual da altura visual do casco que deve ficar abaixo da agua quando na superficie.")]
    public float percentualCascoSubmersoNaSuperficie = 0.58f;
    [Tooltip("Margem minima do topo do casco acima da agua quando em superficie.")]
    public float margemTopoCascoAcimaDaAgua = 0.35f;

    [Tooltip("Velocidade de subida/descida.")]
    public float velocidadeMovimento = 2f;

    [Header("Sistema de Misseis")]
    [Tooltip("Locais de disparo de misseis (nulos sao ignorados automaticamente).")]
    public Transform[] locaisDisparo = new Transform[22];

    [Tooltip("Prefab do missil submarino.")]
    public GameObject prefabMisselSubmarino;

    [Header("Sistema de Torpedos")]
    [Tooltip("Locais de lançamento de torpedos (tubes).")]
    public Transform[] tubosTorpedo = new Transform[4];
    [Tooltip("Prefab do torpedo.")]
    public GameObject prefabTorpedo;
    [Tooltip("Número de torpedos disponíveis.")]
    public int torpedosDisponiveis = 8;
    [Tooltip("Alcance máximo dos torpedos em unidades.")]
    public float alcanceTorpedos = 800f;
    [Tooltip("Cooldown entre lançamentos de torpedo.")]
    public float cooldownTorpedo = 3f;

    [Header("Alcance de Ataque")]
    [Tooltip("Alcance maximo dos misseis em unidades.")]
    public float alcanceMisseis = 500f;

    [Header("Comando")]
    public ModoOperacao modoAtual = ModoOperacao.Passivo;
    [Tooltip("Intervalo entre scans do modo automatico.")]
    public float intervaloBuscaAutomatica = 0.75f;

    [Header("Status")]
    public bool estaSubmerso = false;
    public int misseisDisponiveis = 22;

    [Header("IA Naval")]
    public float cooldownAtaqueIA = 20f;

    [Header("Cristal de Identificacao")]
    public bool gerarCristalIdentificacao = true;
    public bool mostrarCristalSomenteParaJogador = true;
    public float alturaCristalSobreAgua = 10f;
    public Vector3 escalaCristal = new Vector3(1.1f, 1.1f, 1.1f);
    public Color corCristal = new Color(0.1f, 0.95f, 1f, 0.92f);

    [Header("Fisica de Navegacao")]
    [Tooltip("Velocidade maxima de rotacao do leme (graus por segundo).")]
    public float velocidadeGiroMax = 15f;
    [Tooltip("Quanto tempo o submarino demora para acelerar totalmente (inercia).")]
    public float aceleracao = 1.5f;
    [Tooltip("Inclinacao visual nas curvas.")]
    public float forcaInclinacao = 3.0f;
    public Transform modelo3D;
    public TrailRenderer rastroAgua;

    [Header("Debug")]
    public bool mostrarLogsNoConsole = false;

    private bool emMovimento = false;
    private float ultimoMovimento = -4f;
    private bool[] misseisUsados;
    private int totalLocaisValidos = 0;
    private Vector3 pontoAlvoAtual;
    private bool[] tubosTorpedoUsados;
    private int totalTubosValidos = 0;
    private float proximoLancamentoTorpedo = 0f;

    private ControleUnidade meuControle;
    private IdentidadeUnidade minhaIdentidade;
    private NavMeshAgent agente;
    private float velocidadeOriginal;
    private float velocidadeAtualSimulada = 0f;
    private float lemeAtual = 0f;
    private Camera cameraPrincipal;
    private float proximoAtaqueIA = 0f;
    private float proximaBuscaAutomatica = 0f;
    private bool selecaoAnterior = false;
    private bool cursorMiraAtivo = false;
    private Vector3 destinoFallback;
    private bool temDestinoFallback = false;
    private float proximaTentativaNavMesh = 0f;

    private GameObject cristalIdentificacao;
    private Renderer cristalRenderer;
    private readonly Collider[] bufferAlvos = new Collider[128];
    private readonly List<Transform> alvosAutomaticos = new List<Transform>(32);
    private static readonly List<IdentidadeUnidade> unidadesRegistradasRadar = new List<IdentidadeUnidade>(256);
    private static MiniMapa miniMapaCache;
    private static float proximaBuscaMiniMapa;

    void Start()
    {
        cameraPrincipal = Camera.main;
        meuControle = GetComponent<ControleUnidade>();
        minhaIdentidade = GetComponent<IdentidadeUnidade>();
        if (minhaIdentidade == null)
        {
            minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        }

        agente = GetComponent<NavMeshAgent>();
        if (agente != null)
        {
            velocidadeOriginal = agente.speed;
            agente.updateRotation = false;
            agente.acceleration = 9999f;
        }

        if (rastroAgua == null)
        {
            rastroAgua = GetComponentInChildren<TrailRenderer>();
        }

        totalLocaisValidos = 0;
        for (int i = 0; i < locaisDisparo.Length; i++)
        {
            if (locaisDisparo[i] != null)
            {
                totalLocaisValidos++;
            }
        }

        PoolDeObjetosCombate.Prewarm(prefabMisselSubmarino, Mathf.Clamp(totalLocaisValidos > 0 ? totalLocaisValidos / 2 : 2, 2, 6));

        misseisUsados = new bool[locaisDisparo.Length];
        misseisDisponiveis = totalLocaisValidos;

        // Inicializar tubos de torpedo
        totalTubosValidos = 0;
        for (int i = 0; i < tubosTorpedo.Length; i++)
        {
            if (tubosTorpedo[i] != null) totalTubosValidos++;
        }
        tubosTorpedoUsados = new bool[tubosTorpedo.Length];
        if (prefabTorpedo != null)
            PoolDeObjetosCombate.Prewarm(prefabTorpedo, Mathf.Clamp(totalTubosValidos > 0 ? totalTubosValidos : 2, 2, 4));

        CriarCristalIdentificacao();
        AplicarEstadoInicial();
        StartCoroutine(PrepararAgenteNaval());
        AtualizarCristalIdentificacao(true);
        AtualizarCursorMira(false);

        if (mostrarLogsNoConsole)
        {
            Debug.Log($"[USS Leviathan] {totalLocaisValidos} locais de lancamento detectados.", this);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MissilePrefabAutoBinder.BindControleSubmarino(this);
    }

    [ContextMenu("Auto configurar misseis")]
    private void AutoConfigurarMisseisEditor()
    {
        MissilePrefabAutoBinder.BindControleSubmarino(this, true);
    }
#endif

    void Update()
    {
        if (cameraPrincipal == null)
        {
            cameraPrincipal = Camera.main;
        }

        bool estaSelecionado = meuControle != null && meuControle.selecionado;
        if (estaSelecionado && !selecaoAnterior)
        {
            MostrarStatusSubmarino();
        }
        selecaoAnterior = estaSelecionado;

        AtualizarMovimento();
        AtualizarInclinacaoNavio();
        AtualizarRastroAgua();
        AtualizarCristalIdentificacao(false);
        AtualizarCursorMira(estaSelecionado && modoAtual == ModoOperacao.Manual);

        if (modoAtual == ModoOperacao.Automatico)
        {
            TentarAtaqueAutomatico();
        }

        if (!estaSelecionado)
        {
            return;
        }

        ProcessarComandosSelecionado();

        if (modoAtual == ModoOperacao.Manual)
        {
            ProcessarMiraManual();
        }
    }

    private void ProcessarComandosSelecionado()
    {
        if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
        
        float tempoDesdeUltimoMovimento = Time.time - ultimoMovimento;

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (emMovimento)
            {
                Debug.Log("[USS Leviathan] Manobra em curso, aguarde...");
            }
            else if (tempoDesdeUltimoMovimento < 4f)
            {
                Debug.Log($"[USS Leviathan] Sistemas de pressao recarregando. Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s.");
            }
            else if (estaSubmerso)
            {
                StartCoroutine(Subir());
            }
            else
            {
                Debug.Log("[USS Leviathan] Ja esta na superficie!");
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (emMovimento)
            {
                Debug.Log("[USS Leviathan] Manobra em curso, aguarde...");
            }
            else if (tempoDesdeUltimoMovimento < 4f)
            {
                Debug.Log($"[USS Leviathan] Sistemas de pressao recarregando. Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s.");
            }
            else if (!estaSubmerso)
            {
                StartCoroutine(Descer());
            }
            else
            {
                Debug.Log("[USS Leviathan] Ja esta submerso!");
            }
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            MostrarStatusSubmarino();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            CiclarModoOperacao();
        }
    }

    private void AtualizarMovimento()
    {
        if (agente == null || !agente.enabled)
        {
            return;
        }

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        if (!agente.isOnNavMesh)
        {
            if (Time.time >= proximaTentativaNavMesh)
            {
                proximaTentativaNavMesh = Time.time + 1f;
                TentarColocarAgenteNaAgua();
            }

            if (!agente.isOnNavMesh)
            {
                AtualizarMovimentoFallback();
                return;
            }
        }

        if (modoAtual == ModoOperacao.Manual && cursorMiraAtivo)
        {
            velocidadeAtualSimulada = 0f;
            if (agente.isOnNavMesh)
            {
                agente.velocity = Vector3.zero;
                agente.ResetPath();
            }
            return;
        }

        if (agente.hasPath && agente.remainingDistance > agente.stoppingDistance)
        {
            ExecutarMarchaFrenteRealista();
        }
        else
        {
            velocidadeAtualSimulada = Mathf.Lerp(velocidadeAtualSimulada, 0f, Time.deltaTime * 0.5f);
            agente.velocity = transform.forward * velocidadeAtualSimulada;
        }
    }

    private IEnumerator PrepararAgenteNaval()
    {
        // A NavMeshSurface pode terminar de assar depois do Start dos prefabs.
        // Tenta em alguns frames para não deixar o Leviatã permanentemente parado.
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            if (TentarColocarAgenteNaAgua())
            {
                yield break;
            }
        }
    }

    private bool TentarColocarAgenteNaAgua()
    {
        if (agente == null || !agente.enabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (agente.isOnNavMesh)
        {
            return true;
        }

        int areaMask = agente.areaMask == 0 ? (1 << 3) : agente.areaMask;
        NavMeshHit hit;
        Vector3 origem = transform.position;
        origem.y = ResolverNivelAgua();

        if (!NavalPlacementResolver.TryResolveWaterSpawn(origem, transform.forward, 0f, 220f, out Vector3 pontoAgua, out _, out _))
        {
            pontoAgua = origem;
        }

        if (!NavMesh.SamplePosition(pontoAgua, out hit, 45f, areaMask))
        {
            return false;
        }

        bool reposicionado = agente.Warp(hit.position);
        if (reposicionado)
        {
            agente.baseOffset = CalcularOffsetParaEstado(estaSubmerso, hit.position.y);
            AtualizarTransformY(hit.position.y + agente.baseOffset);
        }
        return reposicionado && agente.isOnNavMesh;
    }

    private void AtualizarMovimentoFallback()
    {
        if (!temDestinoFallback)
        {
            velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, 0f, aceleracao * Time.deltaTime);
            return;
        }

        Vector3 direcao = destinoFallback - transform.position;
        direcao.y = 0f;
        float distancia = direcao.magnitude;
        if (distancia <= 4f)
        {
            temDestinoFallback = false;
            velocidadeAtualSimulada = 0f;
            return;
        }

        direcao.Normalize();
        float angulo = Vector3.SignedAngle(transform.forward, direcao, Vector3.up);
        float lemeAlvo = Mathf.Clamp(angulo / 30f, -1f, 1f);
        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 2f);
        transform.Rotate(Vector3.up, lemeAtual * velocidadeGiroMax * Time.deltaTime);

        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, aceleracao * Time.deltaTime);
        Vector3 proximaPosicao = transform.position + transform.forward * (velocidadeAtualSimulada * Time.deltaTime);
        proximaPosicao.y = transform.position.y;
        if (NavalPlacementResolver.IsWaterAtPosition(proximaPosicao))
        {
            transform.position = proximaPosicao;
        }
        else
        {
            velocidadeAtualSimulada = 0f;
            temDestinoFallback = false;
        }
    }

    private void AplicarEstadoInicial()
    {
        AplicarEstadoProfundidade(!iniciarNaSuperficie, true);
    }

    private void CriarCristalIdentificacao()
    {
        if (!gerarCristalIdentificacao || cristalIdentificacao != null)
        {
            return;
        }

        cristalIdentificacao = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(cristalIdentificacao.GetComponent<Collider>());
        cristalIdentificacao.name = "CristalIdentificacaoSubmarino";
        cristalIdentificacao.transform.SetParent(transform, true);
        cristalIdentificacao.transform.localScale = escalaCristal;
        cristalIdentificacao.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);

        cristalRenderer = cristalIdentificacao.GetComponent<Renderer>();
        if (cristalRenderer != null)
        {
            cristalRenderer.material = new Material(Shader.Find("Sprites/Default"));
            cristalRenderer.material.color = corCristal;
            cristalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cristalRenderer.receiveShadows = false;
        }
    }

    private void AtualizarCristalIdentificacao(bool forcar)
    {
        if (cristalIdentificacao == null)
        {
            return;
        }

        bool deveMostrar = !mostrarCristalSomenteParaJogador || minhaIdentidade == null || minhaIdentidade.teamID == 1;
        if (cristalRenderer != null)
        {
            cristalRenderer.enabled = deveMostrar;
        }

        if (!deveMostrar)
        {
            return;
        }

        float nivelAgua = ResolverNivelAgua();
        Vector3 posicao = new Vector3(transform.position.x, nivelAgua + alturaCristalSobreAgua, transform.position.z);
        cristalIdentificacao.transform.position = posicao;
        cristalIdentificacao.transform.Rotate(0f, (forcar ? 0f : 90f) * Time.deltaTime, 0f, Space.World);
    }

    private void AtualizarCursorMira(bool ativo)
    {
        if (cursorMiraAtivo == ativo)
        {
            return;
        }

        cursorMiraAtivo = ativo;
        if (cursorMiraAtivo)
        {
            if (VisualMiraSubmarino.Instancia != null)
            {
                VisualMiraSubmarino.Instancia.AtivarMira();
            }
        }
        else
        {
            if (VisualMiraSubmarino.Instancia != null)
            {
                VisualMiraSubmarino.Instancia.DesativarMira();
            }
        }
    }

    private void CiclarModoOperacao()
    {
        ModoOperacao novoModo = modoAtual;
        switch (modoAtual)
        {
            case ModoOperacao.Passivo:
                novoModo = ModoOperacao.Manual;
                break;
            case ModoOperacao.Manual:
                novoModo = ModoOperacao.Automatico;
                break;
            default:
                novoModo = ModoOperacao.Passivo;
                break;
        }

        DefinirModoOperacao(novoModo, true);
    }

    public void DefinirModoOperacao(ModoOperacao novoModo, bool logar)
    {
        modoAtual = novoModo;
        proximaBuscaAutomatica = 0f;

        if (logar)
        {
            Debug.Log($"[USS Leviathan] Modo alterado para: {modoAtual}", this);
            MostrarStatusSubmarino();
        }
    }

    /// <summary>Alterna o mesmo ciclo usado pela tecla I e pelo menu tático.</summary>
    public string AlternarEstadoOperacional()
    {
        CiclarModoOperacao();
        return modoAtual.ToString().ToUpperInvariant();
    }

    private void MostrarStatusSubmarino()
    {
        Debug.Log(
            $"[USS Leviathan] STATUS | Modo={modoAtual} | Profundidade={(estaSubmerso ? "SUBMERSO" : "SUPERFICIE")} | Misseis={misseisDisponiveis}/{totalLocaisValidos} | Comandos: I=Modo, O=Status, U=Subir, P=Descer",
            this);
    }

    private void ProcessarMiraManual()
    {
        bool disparoSolicitado =
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Space);

        if (!disparoSolicitado)
        {
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (misseisDisponiveis <= 0)
        {
            Debug.Log("[USS Leviathan] Sem misseis disponiveis!", this);
            return;
        }

        if (cameraPrincipal == null)
        {
            return;
        }

        Ray ray = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit))
        {
            return;
        }

        float distancia = Vector3.Distance(transform.position, hit.point);
        if (distancia > alcanceMisseis)
        {
            Debug.Log($"[USS Leviathan] Alvo fora de alcance! ({distancia:F0}m / max {alcanceMisseis:F0}m)", this);
            return;
        }

        pontoAlvoAtual = hit.point;
        DispararMissel(pontoAlvoAtual);
    }

    public bool TentarDisparoManual(Vector3 pontoAlvo, Transform alvoT = null)
    {
        if (modoAtual != ModoOperacao.Manual)
        {
            return false;
        }

        if (misseisDisponiveis <= 0)
        {
            Debug.Log("[USS Leviathan] Sem misseis disponiveis!", this);
            return false;
        }

        float distancia = Vector3.Distance(transform.position, pontoAlvo);
        if (distancia > alcanceMisseis)
        {
            Debug.Log($"[USS Leviathan] Alvo fora de alcance! ({distancia:F0}m / max {alcanceMisseis:F0}m)", this);
            return false;
        }

        pontoAlvoAtual = pontoAlvo;
        DispararMissel(pontoAlvoAtual, alvoT);
        return true;
    }

    private void TentarAtaqueAutomatico()
    {
        if (Time.time < proximaBuscaAutomatica || !PodeAtacarIA())
        {
            return;
        }

        proximaBuscaAutomatica = Time.time + Mathf.Max(0.2f, intervaloBuscaAutomatica);
        Transform alvo = EncontrarMelhorAlvoAutomatico();
        if (alvo != null)
        {
            DispararMisselIA(alvo.position, alvo);
        }
    }

    private Transform EncontrarMelhorAlvoAutomatico()
    {
        int meuTime = minhaIdentidade != null ? minhaIdentidade.teamID : 1;
        alvosAutomaticos.Clear();

        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, alcanceMisseis, bufferAlvos, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < quantidade; i++)
        {
            Collider col = bufferAlvos[i];
            if (col == null)
            {
                continue;
            }

            TentarRegistrarAlvoAutomatico(col.transform, meuTime);
            bufferAlvos[i] = null;
        }

        RegistrarAlvosDoRegistroGlobal(meuTime);

        Transform melhorAlvo = null;
        int melhorPrioridade = int.MaxValue;
        float melhorDistanciaSqr = float.MaxValue;

        for (int i = 0; i < alvosAutomaticos.Count; i++)
        {
            Transform alvo = alvosAutomaticos[i];
            if (alvo == null)
            {
                continue;
            }

            int prioridade = ObterPrioridadeAlvo(alvo);
            float distanciaSqr = (alvo.position - transform.position).sqrMagnitude;
            if (prioridade < melhorPrioridade || (prioridade == melhorPrioridade && distanciaSqr < melhorDistanciaSqr))
            {
                melhorPrioridade = prioridade;
                melhorDistanciaSqr = distanciaSqr;
                melhorAlvo = alvo;
            }
        }

        return melhorAlvo;
    }

    private bool TentarRegistrarAlvoAutomatico(Transform candidato, int meuTime)
    {
        if (candidato == null)
        {
            return false;
        }

        Transform minhaRaiz = transform.root != null ? transform.root : transform;
        if (candidato == minhaRaiz || candidato.IsChildOf(minhaRaiz))
        {
            return false;
        }

        IdentidadeUnidade idAlvo = candidato.GetComponentInParent<IdentidadeUnidade>();
        if (idAlvo == null) idAlvo = candidato.GetComponentInChildren<IdentidadeUnidade>();
        if (idAlvo == null || idAlvo.teamID == 0 || idAlvo.teamID == meuTime)
        {
            return false;
        }

        Transform alvoResolvido = ResolverTransformAlvo(idAlvo.transform);
        if (alvoResolvido == null || alvoResolvido == minhaRaiz || alvoResolvido.IsChildOf(minhaRaiz))
        {
            return false;
        }

        float distanciaSqr = (alvoResolvido.position - transform.position).sqrMagnitude;
        if (distanciaSqr > alcanceMisseis * alcanceMisseis)
        {
            return false;
        }

        SistemaDeDanos danos = ObterSistemaDeDanos(alvoResolvido);
        if (danos != null && danos.vidaAtual <= 0f)
        {
            return false;
        }

        if (danos != null)
        {
            alvoResolvido = ResolverTransformAlvo(danos.transform);
        }

        if (alvoResolvido == null || alvosAutomaticos.Contains(alvoResolvido))
        {
            return false;
        }

        alvosAutomaticos.Add(alvoResolvido);
        RegistrarAlvoNoMiniMapa(alvoResolvido);
        return true;
    }

    private Transform ResolverTransformAlvo(Transform candidato)
    {
        if (candidato == null)
        {
            return null;
        }

        SistemaDeDanos danos = candidato.GetComponentInParent<SistemaDeDanos>();
        if (danos == null) danos = candidato.GetComponentInChildren<SistemaDeDanos>();
        if (danos != null) return danos.transform;

        ControleAviao aviao = candidato.GetComponentInParent<ControleAviao>();
        if (aviao == null) aviao = candidato.GetComponentInChildren<ControleAviao>();
        if (aviao != null) return aviao.transform;

        ControleAviaoCaca caca = candidato.GetComponentInParent<ControleAviaoCaca>();
        if (caca == null) caca = candidato.GetComponentInChildren<ControleAviaoCaca>();
        if (caca != null) return caca.transform;

        AviaoBombardeiro bombardeiro = candidato.GetComponentInParent<AviaoBombardeiro>();
        if (bombardeiro == null) bombardeiro = candidato.GetComponentInChildren<AviaoBombardeiro>();
        if (bombardeiro != null) return bombardeiro.transform;

        Helicoptero helicoptero = candidato.GetComponentInParent<Helicoptero>();
        if (helicoptero == null) helicoptero = candidato.GetComponentInChildren<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        IdentidadeUnidade identidade = candidato.GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null) identidade = candidato.GetComponentInChildren<IdentidadeUnidade>();
        if (identidade != null) return identidade.transform;

        return candidato.root != null ? candidato.root : candidato;
    }

    private SistemaDeDanos ObterSistemaDeDanos(Transform alvo)
    {
        if (alvo == null)
        {
            return null;
        }

        SistemaDeDanos danos = alvo.GetComponentInParent<SistemaDeDanos>();
        if (danos == null) danos = alvo.GetComponentInChildren<SistemaDeDanos>();
        return danos;
    }

    private int ObterPrioridadeAlvo(Transform alvo)
    {
        if (EhBombardeiro(alvo))
        {
            return 0;
        }

        if (EhAlvoAereo(alvo))
        {
            return 1;
        }

        return 2;
    }

    private bool EhBombardeiro(Transform alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        string nomeAlvo = alvo.name.ToLowerInvariant();
        return alvo.GetComponentInParent<AviaoBombardeiro>() != null
               || nomeAlvo.Contains("bombard")
               || nomeAlvo.Contains("bombardeiro")
               || nomeAlvo.Contains("bomber")
               || nomeAlvo.Contains("b52")
               || nomeAlvo.Contains("b2");
    }

    private bool EhAlvoAereo(Transform alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        string nomeAlvo = alvo.name.ToLowerInvariant();

        return alvo.position.y > 8f
               || alvo.GetComponentInParent<ControleAviao>() != null
               || alvo.GetComponentInParent<ControleAviaoCaca>() != null
               || alvo.GetComponentInParent<AviaoBombardeiro>() != null
               || alvo.GetComponentInParent<Helicoptero>() != null
               || (identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo)
               || nomeAlvo.Contains("aviao")
               || nomeAlvo.Contains("heli")
               || nomeAlvo.Contains("caca")
               || nomeAlvo.Contains("jato")
               || nomeAlvo.Contains("drone")
               || nomeAlvo.Contains("bombard")
               || nomeAlvo.Contains("bombardeiro")
               || nomeAlvo.Contains("bomber")
               || TagSafe.Matches(alvo, "Areo")
               || TagSafe.Matches(alvo, "Aereo");
    }

    private void RegistrarAlvosDoRegistroGlobal(int meuTime)
    {
        RegistroEntidadesJogo.FillUnidades(unidadesRegistradasRadar);
        float alcanceSqr = alcanceMisseis * alcanceMisseis;

        for (int i = 0; i < unidadesRegistradasRadar.Count; i++)
        {
            IdentidadeUnidade unidade = unidadesRegistradasRadar[i];
            if (unidade == null || !unidade.gameObject.activeInHierarchy)
            {
                continue;
            }

            if ((unidade.transform.position - transform.position).sqrMagnitude > alcanceSqr)
            {
                continue;
            }

            TentarRegistrarAlvoAutomatico(unidade.transform, meuTime);
        }

        unidadesRegistradasRadar.Clear();
    }

    private void RegistrarAlvoNoMiniMapa(Transform alvo)
    {
        MiniMapa miniMapa = ObterMiniMapa();
        if (miniMapa != null && miniMapa.mostrarInimigos)
        {
            miniMapa.RegistrarUnidadeNoMapa(alvo, true);
        }
    }

    private static MiniMapa ObterMiniMapa()
    {
        if (miniMapaCache != null)
        {
            return miniMapaCache;
        }

        if (Time.time < proximaBuscaMiniMapa)
        {
            return null;
        }

        proximaBuscaMiniMapa = Time.time + 1f;
        miniMapaCache = UnityEngine.Object.FindFirstObjectByType<MiniMapa>();
        return miniMapaCache;
    }

    private void DispararMissel(Vector3 alvo, Transform alvoT = null)
    {
        if (misseisDisponiveis <= 0)
        {
            Debug.Log("[USS Leviathan] Sem misseis disponiveis!", this);
            return;
        }

        if (prefabMisselSubmarino == null)
        {
            Debug.LogError("[USS Leviathan] Prefab do missil nao esta configurado!", this);
            return;
        }

        for (int i = 0; i < locaisDisparo.Length; i++)
        {
            if (locaisDisparo[i] == null || misseisUsados[i])
            {
                continue;
            }

            GameObject missel = PoolDeObjetosCombate.Spawn(prefabMisselSubmarino, locaisDisparo[i].position, locaisDisparo[i].rotation);
            MisselSubmarino scriptMissel = missel.GetComponent<MisselSubmarino>();
            if (scriptMissel != null)
            {
                scriptMissel.IniciarLancamento(alvo, estaSubmerso, alvoT);
                MissileThreatTracker.RegistrarLancamento(missel, this, alvo, alvoT, MissileThreatTracker.EstimarVelocidade(missel));
            }

            misseisUsados[i] = true;
            misseisDisponiveis--;
            Debug.Log($"[USS Leviathan] Missil do slot {i + 1} disparado! ({misseisDisponiveis} restantes) -> Alvo: {alvo}", this);
            return;
        }

        Debug.LogWarning("[USS Leviathan] Nenhum slot de missil disponivel foi encontrado.", this);
    }

    public bool PodeLancarTorpedo()
    {
        return torpedosDisponiveis > 0 && Time.time >= proximoLancamentoTorpedo && totalTubosValidos > 0;
    }

    public void LancarTorpedo(Vector3 alvo, Transform alvoT = null)
    {
        if (!PodeLancarTorpedo())
        {
            Debug.Log("[USS Leviathan] Torpedo nao pronto para lancamento (cooldown ou sem municao).", this);
            return;
        }

        if (prefabTorpedo == null)
        {
            Debug.LogError("[USS Leviathan] Prefab do torpedo nao esta configurado!", this);
            return;
        }

        // Encontrar tubo livre
        for (int i = 0; i < tubosTorpedo.Length; i++)
        {
            if (tubosTorpedo[i] == null || tubosTorpedoUsados[i]) continue;

            GameObject torpedo = PoolDeObjetosCombate.Spawn(prefabTorpedo, tubosTorpedo[i].position, tubosTorpedo[i].rotation);
            Torpedo scriptTorpedo = torpedo.GetComponent<Torpedo>();
            if (scriptTorpedo != null)
            {
                scriptTorpedo.DefinirAlvo(alvoT);
                int meuTime = minhaIdentidade != null ? minhaIdentidade.teamID : -1;
                scriptTorpedo.DefinirLancador(transform, meuTime);
                
                // Registrar ameaça para sistemas de defesa
                MissileThreatTracker.RegistrarLancamento(torpedo, this, alvo, alvoT, scriptTorpedo.velocidade);
            }

            tubosTorpedoUsados[i] = true;
            torpedosDisponiveis--;
            proximoLancamentoTorpedo = Time.time + cooldownTorpedo;
            
            Debug.Log($"[USS Leviathan] Torpedo lancado do tubo {i + 1}! ({torpedosDisponiveis} restantes) -> Alvo: {alvo}", this);
            return;
        }

        Debug.LogWarning("[USS Leviathan] Nenhum tubo de torpedo disponivel.", this);
    }

    public void RecarregarTorpedos()
    {
        for (int i = 0; i < tubosTorpedoUsados.Length; i++)
        {
            tubosTorpedoUsados[i] = false;
        }
        int maxTorpedos = totalTubosValidos * 2; // 2 torpedos por tubo
        torpedosDisponiveis = maxTorpedos;
        Debug.Log($"[USS Leviathan] Torpedos recarregados! {torpedosDisponiveis} disponiveis.", this);
    }

    private IEnumerator Subir()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        Debug.Log("[USS Leviathan] Subindo para superficie...", this);

        yield return MoverProfundidade(false);

        emMovimento = false;
        Debug.Log("[USS Leviathan] Na superficie!", this);
    }

    private IEnumerator Descer()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        Debug.Log("[USS Leviathan] Descendo...", this);

        yield return MoverProfundidade(true);

        emMovimento = false;
        Debug.Log("[USS Leviathan] Submerso!", this);
    }

    private IEnumerator MoverProfundidade(bool submerso)
    {
        if (agente == null)
        {
            AplicarEstadoProfundidade(submerso, true);
            yield break;
        }

        float navMeshY = transform.position.y - agente.baseOffset;
        float offsetInicial = agente.baseOffset;
        float offsetDesejado = CalcularOffsetParaEstado(submerso, navMeshY);
        float distancia = Mathf.Abs(offsetDesejado - offsetInicial);
        float duracao = velocidadeMovimento > 0.01f ? distancia / velocidadeMovimento : 0.1f;
        float tempoDecorrido = 0f;

        if (duracao > 0.05f)
        {
            while (tempoDecorrido < duracao)
            {
                tempoDecorrido += Time.deltaTime;
                float t = Mathf.Clamp01(tempoDecorrido / duracao);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Recalcular navMeshY dinamicamente caso o submarino esteja se movendo
                float currentNavMeshY = transform.position.y - agente.baseOffset;
                float currentOffsetDesejado = ResolverAlturaMundo(submerso) - currentNavMeshY;

                agente.baseOffset = Mathf.Lerp(offsetInicial, currentOffsetDesejado, smoothT);
                AtualizarTransformY(currentNavMeshY + agente.baseOffset);

                // Atualizar estado lógico 'estaSubmerso' no meio da transição para sistemas de combate
                if (submerso && !estaSubmerso && t > 0.5f)
                {
                    estaSubmerso = true;
                }
                else if (!submerso && estaSubmerso && t > 0.7f)
                {
                    estaSubmerso = false;
                }

                yield return null;
            }
        }

        // Garantir posição final exata
        float finalNavMeshY = transform.position.y - agente.baseOffset;
        agente.baseOffset = CalcularOffsetParaEstado(submerso, finalNavMeshY);
        AtualizarTransformY(finalNavMeshY + agente.baseOffset);
        estaSubmerso = submerso;
    }

    private void AplicarEstadoProfundidade(bool submerso, bool instantaneo)
    {
        estaSubmerso = submerso;
        float alturaDesejada = ResolverAlturaMundo(submerso);

        if (agente != null)
        {
            float navMeshY = transform.position.y - agente.baseOffset;
            agente.baseOffset = alturaDesejada - navMeshY;
            if (instantaneo)
            {
                AtualizarTransformY(alturaDesejada);
            }
        }
        else if (instantaneo)
        {
            AtualizarTransformY(alturaDesejada);
        }
    }

    private float CalcularOffsetParaEstado(bool submerso, float navMeshY)
    {
        return ResolverAlturaMundo(submerso) - navMeshY;
    }

    private float ResolverAlturaMundo(bool submerso)
    {
        float nivelAgua = ResolverNivelAgua();
        if (submerso)
        {
            return nivelAgua + profundidadeSubmersao + offsetAlturaAgua;
        }

        float alturaManual = nivelAgua + alturaSuperificie + offsetAlturaAgua;
        if (!calibrarAlturaAutomaticamente)
        {
            return alturaManual;
        }

        return CalcularAlturaSuperficieCalibrada(alturaManual, nivelAgua);
    }

    private float ResolverNivelAgua()
    {
        return NavalPlacementResolver.ResolveSeaLevel();
    }

    private void AtualizarTransformY(float yDesejado)
    {
        Vector3 pos = transform.position;
        pos.y = yDesejado;
        transform.position = pos;
    }

    private float CalcularAlturaSuperficieCalibrada(float alturaManual, float nivelAgua)
    {
        Bounds cascoBounds;
        if (!TryGetBoundsDoCascoPrincipal(out cascoBounds))
        {
            return alturaManual;
        }

        float alturaCasco = Mathf.Max(0.5f, cascoBounds.size.y);
        float fundoDesejado = nivelAgua - (alturaCasco * Mathf.Clamp(percentualCascoSubmersoNaSuperficie, 0.2f, 0.85f));
        float topoMinimo = nivelAgua + Mathf.Max(0.1f, margemTopoCascoAcimaDaAgua);
        float fundoLocal = cascoBounds.min.y - transform.position.y;
        float topoLocal = cascoBounds.max.y - transform.position.y;

        float alturaMinima = topoMinimo - topoLocal;
        float alturaMaxima = fundoDesejado - fundoLocal;

        if (alturaMinima > alturaMaxima)
        {
            return alturaManual;
        }

        return Mathf.Clamp(alturaManual, alturaMinima, alturaMaxima);
    }

    private bool TryGetBoundsDoCascoPrincipal(out Bounds cascoBounds)
    {
        Renderer[] renderers = modelo3D != null
            ? modelo3D.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        cascoBounds = new Bounds(transform.position, Vector3.zero);
        bool encontrou = false;
        float melhorScore = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null)
            {
                continue;
            }

            if (cristalRenderer != null && rendererAtual == cristalRenderer)
            {
                continue;
            }

            Bounds bounds = rendererAtual.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            float score = bounds.size.x * bounds.size.y * bounds.size.z;
            if (!encontrou || score > melhorScore)
            {
                melhorScore = score;
                cascoBounds = bounds;
                encontrou = true;
            }
        }

        return encontrou;
    }

    // --- API PUBLICA ---

    public int GetMisseisDisponiveis()
    {
        return misseisDisponiveis;
    }

    public bool EstaSubmerso()
    {
        return estaSubmerso;
    }

    public static ControleSubmarino ObterSubmarinoDoAlvo(Transform alvo)
    {
        if (alvo == null)
        {
            return null;
        }

        ControleSubmarino submarino = alvo.GetComponentInParent<ControleSubmarino>();
        if (submarino != null)
        {
            return submarino;
        }

        Transform raiz = alvo.root != null ? alvo.root : alvo;
        return raiz != null ? raiz.GetComponent<ControleSubmarino>() : null;
    }

    public static bool EstaOcultoParaCombateConvencional(Transform alvo)
    {
        ControleSubmarino submarino = ObterSubmarinoDoAlvo(alvo);
        return submarino != null && submarino.EstaSubmerso();
    }

    public static bool PodeSerAlvoConvencional(Transform alvo)
    {
        if (alvo == null || EstaOcultoParaCombateConvencional(alvo)) return false;
        
        // Aviões comerciais são ignorados por radares e sistemas de tiro convencionais
        if (alvo.GetComponentInParent<ControleAviaoComercial>() != null || alvo.GetComponentInChildren<ControleAviaoComercial>() != null)
        {
            return false;
        }

        return true;
    }

    public bool PodeAtacarIA()
    {
        return misseisDisponiveis > 0 && Time.time >= proximoAtaqueIA;
    }

    public bool EmModoManualDisparo()
    {
        return modoAtual == ModoOperacao.Manual;
    }

    public void ForcarEstadoSuperficieImediato()
    {
        StopAllCoroutines();
        emMovimento = false;
        AplicarEstadoProfundidade(false, true);
    }

    public void DispararMisselIA(Vector3 alvo, Transform alvoT = null)
    {
        if (!PodeAtacarIA())
        {
            return;
        }

        float distancia = Vector3.Distance(transform.position, alvo);
        if (distancia > alcanceMisseis)
        {
            return;
        }

        proximoAtaqueIA = Time.time + Mathf.Max(2f, cooldownAtaqueIA);
        DispararMissel(alvo, alvoT);
    }

    [ContextMenu("Recarregar Todos os Misseis")]
    public void RecarregarMisseis()
    {
        for (int i = 0; i < misseisUsados.Length; i++)
        {
            misseisUsados[i] = false;
        }

        misseisDisponiveis = totalLocaisValidos;
        Debug.Log($"[USS Leviathan] Todos os {totalLocaisValidos} misseis recarregados!", this);
    }

    /// <summary>Recebe destino via clique do jogador ou IA.</summary>
    public void DefinirDestino(Vector3 destino)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        if (!NavalPlacementResolver.IsWaterAtPosition(destino))
        {
            Debug.LogWarning($"[USS Leviathan] Ordem recusada: destino fora da água ({destino.x:F0}, {destino.z:F0}).", this);
            return;
        }

        destino.y = ResolverNivelAgua();
        destinoFallback = destino;
        temDestinoFallback = true;

        if (agente != null && agente.enabled && !agente.isOnNavMesh)
        {
            TentarColocarAgenteNaAgua();
        }

        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(destino, out NavMeshHit hitDestino, 30f, agente.areaMask == 0 ? (1 << 3) : agente.areaMask))
            {
                destino = hitDestino.position;
                destinoFallback = destino;
            }

            temDestinoFallback = false;
            agente.SetDestination(destino);
            agente.isStopped = false;
        }
    }

    public void PararPorFaltaDeCombustivel()
    {
        velocidadeAtualSimulada = 0f;
        lemeAtual = 0f;
        emMovimento = false;
        temDestinoFallback = false;

        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.ResetPath();
            agente.isStopped = true;
            agente.velocity = Vector3.zero;
        }
    }

    // --- NAVEGACAO REALISTA ---

    private void ExecutarMarchaFrenteRealista()
    {
        Vector3 direcaoDesejada = (agente.steeringTarget - transform.position).normalized;
        direcaoDesejada.y = 0f;

        float angulo = Vector3.SignedAngle(transform.forward, direcaoDesejada, Vector3.up);
        float lemeAlvo = Mathf.Clamp(angulo / 30.0f, -1f, 1f);
        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 2.0f);

        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, Time.deltaTime * aceleracao);

        float fluxoAgua = Mathf.Abs(velocidadeAtualSimulada) + 2f;
        float eficienciaLeme = Mathf.Clamp01(fluxoAgua / 2.0f);

        float giroReal = lemeAtual * velocidadeGiroMax * Time.deltaTime * eficienciaLeme;
        transform.Rotate(0f, giroReal, 0f);

        agente.velocity = transform.forward * velocidadeAtualSimulada;
    }

    private void AtualizarInclinacaoNavio()
    {
        if (modelo3D == null)
        {
            return;
        }

        float giroFrame = lemeAtual * velocidadeGiroMax;
        float anguloAlvo = -giroFrame * (forcaInclinacao / 10f);
        anguloAlvo = Mathf.Clamp(anguloAlvo, -10f, 10f);

        Vector3 rotAtual = modelo3D.localEulerAngles;
        float zAtual = rotAtual.z > 180f ? rotAtual.z - 360f : rotAtual.z;
        float zNovo = Mathf.Lerp(zAtual, anguloAlvo, Time.deltaTime * 2.0f);
        modelo3D.localEulerAngles = new Vector3(rotAtual.x, rotAtual.y, zNovo);
    }

    private void AtualizarRastroAgua()
    {
        if (rastroAgua == null)
        {
            return;
        }

        float nivelAgua = ResolverNivelAgua();
        // O rastro so deve aparecer se o casco estiver cortando a superficie
        // Usamos uma margem para que o rastro suma logo que o topo do submarino mergulha
        bool naSuperficie = transform.position.y > nivelAgua - 2.0f;
        rastroAgua.emitting = naSuperficie && velocidadeAtualSimulada > 1.0f;
    }

    // --- GIZMOS ---

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Vector3 posSubmersa = new Vector3(transform.position.x, ResolverNivelAgua() + profundidadeSubmersao, transform.position.z);
        Gizmos.DrawWireSphere(posSubmersa, 2f);

        Gizmos.color = Color.cyan;
        Vector3 posSuperficie = new Vector3(transform.position.x, ResolverNivelAgua() + alturaSuperificie, transform.position.z);
        Gizmos.DrawWireSphere(posSuperficie, 2f);
        Gizmos.DrawLine(posSubmersa, posSuperficie);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        DrawCircle(transform.position, alcanceMisseis, 100);

        if (locaisDisparo != null)
        {
            for (int i = 0; i < locaisDisparo.Length; i++)
            {
                if (locaisDisparo[i] == null)
                {
                    continue;
                }

                bool usado = misseisUsados != null && i < misseisUsados.Length && misseisUsados[i];
                Gizmos.color = usado ? Color.red : Color.green;
                Gizmos.DrawWireSphere(locaisDisparo[i].position, 0.5f);
            }
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    void OnGUI()
    {
        if (meuControle == null || !meuControle.selecionado)
        {
            return;
        }

        if (MenuConstrucao.EstaAberto || MenuPier.EstaAberto || Camera.main == null)
        {
            return;
        }

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z <= 0f)
        {
            return;
        }

        float y = Screen.height - screenPos.y;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperCenter;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 14;

        string texto;
        switch (modoAtual)
        {
            case ModoOperacao.Passivo:
                style.normal.textColor = Color.gray;
                texto = $"[{modoAtual}]\nMisseis: {misseisDisponiveis}/{totalLocaisValidos}";
                break;
            case ModoOperacao.Manual:
                style.normal.textColor = Color.yellow;
                texto = $"[{modoAtual}]\nMisseis: {misseisDisponiveis}/{totalLocaisValidos}";
                break;
            default:
                style.normal.textColor = Color.red;
                texto = $"[{modoAtual}]\nMisseis: {misseisDisponiveis}/{totalLocaisValidos}";
                break;
        }

        GUI.color = Color.black;
        GUI.Label(new Rect(screenPos.x - 51, y - 61, 120, 50), texto, style);

        GUI.color = Color.white;
        GUI.Label(new Rect(screenPos.x - 50, y - 60, 120, 50), texto, style);
    }
}
