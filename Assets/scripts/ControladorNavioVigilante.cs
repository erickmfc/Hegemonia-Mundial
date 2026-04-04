using UnityEngine;
using UnityEngine.AI;

public class ControladorNavioVigilante : MonoBehaviour
{
    [Header("Configuracoes de Combate")]
    public float alcanceAtaque = 15f;
    public float cadenciaTiro = 0.5f;
    public GameObject projetilPrefab;
    public Transform[] pontosDisparo;

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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
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
            Atirar(alvoAtual.position);
            cronometroTiro = 0f;
        }
    }

    void AtualizarAlvoMaisProximo(float alcanceQuadrado)
    {
        alvoAtual = null;
        float menorDistanciaQuadrada = alcanceQuadrado;
        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, alcanceAtaque, bufferRadar);

        for (int i = 0; i < quantidade; i++)
        {
            Collider col = bufferRadar[i];
            if (col == null || !TagSafe.Matches(col, "Inimigo"))
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

            Vector3 delta = candidato.position - transform.position;
            delta.y = 0f;
            float distanciaQuadrada = delta.sqrMagnitude;
            if (distanciaQuadrada < menorDistanciaQuadrada)
            {
                menorDistanciaQuadrada = distanciaQuadrada;
                alvoAtual = candidato;
            }
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

            Instantiate(projetilPrefab, ponto.position, Quaternion.LookRotation(direcao));
        }

        if (debugDisparo)
        {
            Debug.Log("[Marinha] Navio Vigilante disparando de multiplos canhoes!");
        }
    }
}
