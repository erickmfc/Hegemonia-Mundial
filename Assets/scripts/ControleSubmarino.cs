using System.Collections;
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

    private bool emMovimento = false;
    private float ultimoMovimento = -4f;
    private bool[] misseisUsados;
    private int totalLocaisValidos = 0;
    private Vector3 pontoAlvoAtual;

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

    private GameObject cristalIdentificacao;
    private Renderer cristalRenderer;
    private readonly Collider[] bufferAlvos = new Collider[64];

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

        misseisUsados = new bool[locaisDisparo.Length];
        misseisDisponiveis = totalLocaisValidos;

        CriarCristalIdentificacao();
        AplicarEstadoInicial();
        AtualizarCristalIdentificacao(true);
        AtualizarCursorMira(false);

        Debug.Log($"[USS Leviathan] {totalLocaisValidos} locais de lancamento detectados.", this);
    }

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
        float tempoDesdeUltimoMovimento = Time.time - ultimoMovimento;

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (estaSubmerso)
                {
                    StartCoroutine(Subir());
                }
                else
                {
                    Debug.Log("[USS Leviathan] Ja esta na superficie!");
                }
            }
            else
            {
                Debug.Log($"[USS Leviathan] Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s antes de mover novamente.");
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (!estaSubmerso)
                {
                    StartCoroutine(Descer());
                }
                else
                {
                    Debug.Log("[USS Leviathan] Ja esta submerso!");
                }
            }
            else
            {
                Debug.Log($"[USS Leviathan] Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s antes de mover novamente.");
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

        if (modoAtual == ModoOperacao.Manual && cursorMiraAtivo)
        {
            velocidadeAtualSimulada = 0f;
            agente.velocity = Vector3.zero;
            agente.ResetPath();
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

    private void MostrarStatusSubmarino()
    {
        Debug.Log(
            $"[USS Leviathan] STATUS | Modo={modoAtual} | Profundidade={(estaSubmerso ? "SUBMERSO" : "SUPERFICIE")} | Misseis={misseisDisponiveis}/{totalLocaisValidos} | Comandos: I=Modo, O=Status, U=Subir, P=Descer",
            this);
    }

    private void ProcessarMiraManual()
    {
        if (!Input.GetMouseButtonDown(1))
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
            DispararMisselIA(alvo.position);
        }
    }

    private Transform EncontrarMelhorAlvoAutomatico()
    {
        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, alcanceMisseis, bufferAlvos);
        float melhorDistancia = float.MaxValue;
        Transform melhor = null;
        int meuTime = minhaIdentidade != null ? minhaIdentidade.teamID : 1;

        for (int i = 0; i < quantidade; i++)
        {
            Collider col = bufferAlvos[i];
            if (col == null)
            {
                continue;
            }

            Transform alvo = col.transform.root;
            if (alvo == null || alvo == transform.root)
            {
                bufferAlvos[i] = null;
                continue;
            }

            IdentidadeUnidade idAlvo = alvo.GetComponent<IdentidadeUnidade>();
            if (idAlvo == null)
            {
                idAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();
            }

            if (idAlvo == null || idAlvo.teamID == 0 || idAlvo.teamID == meuTime)
            {
                bufferAlvos[i] = null;
                continue;
            }

            SistemaDeDanos danos = alvo.GetComponent<SistemaDeDanos>();
            if (danos == null)
            {
                danos = alvo.GetComponentInChildren<SistemaDeDanos>();
            }

            if (danos != null && danos.vidaAtual <= 0f)
            {
                bufferAlvos[i] = null;
                continue;
            }

            float dist = Vector3.Distance(transform.position, alvo.position);
            if (dist < melhorDistancia)
            {
                melhorDistancia = dist;
                melhor = alvo;
            }

            bufferAlvos[i] = null;
        }

        return melhor;
    }

    private void DispararMissel(Vector3 alvo)
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

            GameObject missel = Instantiate(prefabMisselSubmarino, locaisDisparo[i].position, locaisDisparo[i].rotation);
            MisselSubmarino scriptMissel = missel.GetComponent<MisselSubmarino>();
            if (scriptMissel != null)
            {
                scriptMissel.IniciarLancamento(alvo, estaSubmerso);
                MissileThreatTracker.RegistrarLancamento(missel, this, alvo, null, MissileThreatTracker.EstimarVelocidade(missel));
            }

            misseisUsados[i] = true;
            misseisDisponiveis--;
            Debug.Log($"[USS Leviathan] Missil do slot {i + 1} disparado! ({misseisDisponiveis} restantes) -> Alvo: {alvo}", this);
            return;
        }

        Debug.LogWarning("[USS Leviathan] Nenhum slot de missil disponivel foi encontrado.", this);
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
        float duracao = velocidadeMovimento > 0.01f ? distancia / velocidadeMovimento : 0f;
        float tempoDecorrido = 0f;

        if (duracao > 0.1f)
        {
            while (tempoDecorrido < duracao)
            {
                tempoDecorrido += Time.deltaTime;
                agente.baseOffset = Mathf.Lerp(offsetInicial, offsetDesejado, tempoDecorrido / duracao);
                AtualizarTransformY(navMeshY + agente.baseOffset);
                yield return null;
            }
        }

        agente.baseOffset = offsetDesejado;
        AtualizarTransformY(navMeshY + agente.baseOffset);
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

    public void DispararMisselIA(Vector3 alvo)
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
        DispararMissel(alvo);
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
        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.SetDestination(destino);
            agente.isStopped = false;
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

        rastroAgua.emitting = !estaSubmerso && velocidadeAtualSimulada > 1.0f;
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
