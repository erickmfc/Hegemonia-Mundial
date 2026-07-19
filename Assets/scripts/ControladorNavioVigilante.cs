using UnityEngine;
using UnityEngine.AI;

public class ControladorNavioVigilante : MonoBehaviour
{
    [Header("Configuracoes de Combate")]
    public float alcanceAtaque = 150f;
    public float cadenciaTiro = 0.5f;
    public GameObject projetilPrefab;
    public Transform[] pontosDisparo;
    public Transform baseTorreta;
    public Transform canoElevacao;
    public float velocidadeGiroTorreta = 3.5f;
    public float toleranciaDisparoGraus = 7f;
    [Range(-10f, 45f)] public float elevacaoMinima = -4f;
    [Range(0f, 80f)] public float elevacaoMaxima = 30f;

    [Header("Estabilidade (Antygaviti)")]
    public float antygaviti = 5f;

    [Header("Debug")]
    public bool debugDisparo = false;

    private NavMeshAgent agent;
    private float cronometroTiro;
    private readonly Collider[] bufferRadar = new Collider[32];
    private Transform alvoAtual;
    private float proximaBuscaAlvo = 0f;
    private const float IntervaloBuscaAlvo = 0.2f;
    private bool torretaAlinhada;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        AutoConfigurarTorreta();
    }

    void Update()
    {
        EstabilizarNavio();
        ProcurarEAtacarInimigos();
    }

    void EstabilizarNavio()
    {
        if (transform.position.y != 0f)
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, 0f, Time.deltaTime * antygaviti);
            transform.position = pos;
        }
    }

    void ProcurarEAtacarInimigos()
    {
        cronometroTiro += Time.deltaTime;
        float alcanceQuadrado = alcanceAtaque * alcanceAtaque;

        if (Time.time >= proximaBuscaAlvo)
        {
            proximaBuscaAlvo = Time.time + IntervaloBuscaAlvo;
            AtualizarAlvoMaisProximo(alcanceQuadrado);
        }

        if (alvoAtual != null)
        {
            if (!alvoAtual.gameObject.activeInHierarchy || !ControleSubmarino.PodeSerAlvoConvencional(alvoAtual))
            {
                alvoAtual = null;
                return;
            }

            Vector3 delta = alvoAtual.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > alcanceQuadrado)
            {
                alvoAtual = null;
            }
        }

        if (alvoAtual != null && cronometroTiro >= cadenciaTiro)
        {
            AtualizarMiraTorreta(alvoAtual.position);
            if (!torretaAlinhada)
            {
                return;
            }

            Atirar(alvoAtual.position);
            cronometroTiro = 0f;
        }
        else if (alvoAtual != null)
        {
            AtualizarMiraTorreta(alvoAtual.position);
        }
    }

    void AutoConfigurarTorreta()
    {
        if (pontosDisparo != null && pontosDisparo.Length > 0 && pontosDisparo[0] != null)
        {
            Transform candidatoBase = pontosDisparo[0].parent;
            if (candidatoBase != null)
            {
                if (canoElevacao == null)
                {
                    canoElevacao = candidatoBase;
                }

                if (baseTorreta == null && candidatoBase.parent != null)
                {
                    baseTorreta = candidatoBase.parent;
                }
            }
        }

        if (baseTorreta == null)
        {
            baseTorreta = transform;
        }

        if (canoElevacao == null)
        {
            canoElevacao = baseTorreta;
        }
    }

    void AtualizarMiraTorreta(Vector3 posicaoAlvo)
    {
        AutoConfigurarTorreta();

        Vector3 origemMira = canoElevacao != null ? canoElevacao.position : transform.position;
        Vector3 direcaoMundo = posicaoAlvo - origemMira;
        if (direcaoMundo.sqrMagnitude < 0.01f)
        {
            torretaAlinhada = false;
            return;
        }

        if (baseTorreta != null)
        {
            Vector3 direcaoBase = posicaoAlvo - baseTorreta.position;
            direcaoBase.y = 0f;
            if (direcaoBase.sqrMagnitude > 0.01f)
            {
                Quaternion rotacaoBaseAlvo = Quaternion.LookRotation(direcaoBase.normalized, Vector3.up);
                baseTorreta.rotation = Quaternion.Slerp(baseTorreta.rotation, rotacaoBaseAlvo, Time.deltaTime * velocidadeGiroTorreta);
            }
        }

        if (canoElevacao != null && canoElevacao != baseTorreta)
        {
            Vector3 direcaoLocal = canoElevacao.parent != null
                ? canoElevacao.parent.InverseTransformDirection(direcaoMundo.normalized)
                : direcaoMundo.normalized;

            float pitch = -Mathf.Atan2(direcaoLocal.y, new Vector2(direcaoLocal.x, direcaoLocal.z).magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, elevacaoMinima, elevacaoMaxima);

            Quaternion rotacaoLocalAlvo = Quaternion.Euler(pitch, 0f, 0f);
            canoElevacao.localRotation = Quaternion.Slerp(canoElevacao.localRotation, rotacaoLocalAlvo, Time.deltaTime * velocidadeGiroTorreta);
        }

        Transform referencia = pontosDisparo != null && pontosDisparo.Length > 0 && pontosDisparo[0] != null
            ? pontosDisparo[0]
            : canoElevacao;

        if (referencia == null)
        {
            torretaAlinhada = false;
            return;
        }

        Vector3 direcaoAtual = referencia.forward;
        Vector3 direcaoDesejada = (posicaoAlvo - referencia.position).normalized;
        torretaAlinhada = Vector3.Angle(direcaoAtual, direcaoDesejada) <= toleranciaDisparoGraus;
    }

    void AtualizarAlvoMaisProximo(float alcanceQuadrado)
    {
        alvoAtual = null;
        float menorDistanciaQuadrada = alcanceQuadrado;
        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, alcanceAtaque, bufferRadar, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < quantidade; i++)
        {
            Collider col = bufferRadar[i];
            if (col == null)
            {
                continue;
            }

            Transform candidato = col.transform.root != null ? col.transform.root : col.transform;
            if (candidato == transform)
            {
                continue;
            }

            if (!ControleSubmarino.PodeSerAlvoConvencional(candidato))
            {
                continue;
            }

            IdentidadeUnidade idCandidato = candidato.GetComponentInParent<IdentidadeUnidade>();
            IdentidadeUnidade idProprio = GetComponent<IdentidadeUnidade>();
            if (idCandidato != null && idProprio != null && idCandidato.teamID == idProprio.teamID)
            {
                continue;
            }

            SistemaDeDanos vida = candidato.GetComponentInParent<SistemaDeDanos>();
            if (vida != null && vida.vidaAtual <= 0f)
            {
                continue;
            }

            Vector3 delta = candidato.position - transform.position;
            delta.y = 0f;
            float distanciaQuadrada = delta.sqrMagnitude;
            if (distanciaQuadrada < menorDistanciaQuadrada)
            {
                menorDistanciaQuadrada = distanciaQuadrada;
                alvoAtual = candidato;
            }
        }

        if (debugDisparo && alvoAtual == null)
        {
            Debug.Log($"[Marinha] {name} nao encontrou alvo valido em {alcanceAtaque:F0}m.");
        }
    }

    void Atirar(Vector3 posicaoAlvo)
    {
        if (projetilPrefab == null || pontosDisparo == null)
        {
            return;
        }

        for (int i = 0; i < pontosDisparo.Length; i++)
        {
            Transform ponto = pontosDisparo[i];
            if (ponto == null)
            {
                continue;
            }

            Vector3 direcao = posicaoAlvo - ponto.position;
            if (direcao == Vector3.zero)
            {
                continue;
            }

            Quaternion rotacaoDisparo = Quaternion.LookRotation(direcao.normalized, Vector3.up);
            GameObject projetil = PoolDeObjetosCombate.Spawn(projetilPrefab, ponto.position, rotacaoDisparo);
            if (projetil == null)
            {
                continue;
            }

            Projetil scriptProjetil = projetil.GetComponent<Projetil>();
            if (scriptProjetil != null)
            {
                scriptProjetil.SetDono(transform.root.gameObject);
                scriptProjetil.SetDirecao(direcao.normalized);
            }
        }

        if (debugDisparo)
        {
            Debug.Log("[Marinha] Navio Vigilante disparando de multiplos canhoes!");
        }
    }
}
