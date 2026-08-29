using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;

public class MisselCaca : MonoBehaviour
{
    [Header("Fase 1: Queda Livre (Armamento e Desprender do Avião)")]
    public float tempoQuedaLivre = 0.5f;
    public float forcaQuedaAdicional = 2f;
    
    [Header("Fase 2: Motor Ligado (Boost e Navegação)")]
    public float aceleracaoBoost = 50f;
    public float velocidadeMaxima = 200f;
    public float forcaRotacao = 5f;
    public ParticleSystem sistemaFumaca;

    [Header("Explosão")]
    public float dano = 150f;
    public float raioExplosao = 15f;
    public float escalaVisualExplosao = 1.0f;
    [Range(0f, 1f)]
    public float volumeSom = 1.0f;
    public GameObject efeitoExplosaoPrefab;
    public AudioClip somExplosao;
    public float tempoMaximoVida = 18f;

    private Vector3 pontoAlvo;
    private Transform alvoTransform;
    private bool lancado = false;
    private bool motorLigado = false;
    private float velocidadeAtual = 0f;
    private Rigidbody rb;
    private bool jaExplodiu = false; // Impede explosão dupla
    private Vector3 ultimaPosicaoGuiagem;
    private bool possuiUltimaPosicaoGuiagem;

    // --- CACHE: Buffer reutilizável para OverlapSphere (reduz GC) ---
    private static readonly Collider[] _explosaoBuffer = new Collider[32];
    private static readonly HashSet<int> _alvosJaProcessados = new HashSet<int>();
    // --- CACHE: WaitForSeconds reutilizável ---
    private WaitForSeconds _esperaQuedaLivre;
    private float _tempoExpirar;

    void OnEnable()
    {
        IA_CombatTelemetry.RegisterMissile();
        ResetarEstado();
        _tempoExpirar = Time.time + tempoMaximoVida;
    }

    void OnDisable()
    {
        IA_CombatTelemetry.UnregisterMissile();
        StopAllCoroutines();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (sistemaFumaca != null)
        {
            sistemaFumaca.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false; 
        rb.isKinematic = false;
        rb.freezeRotation = true; 

        if (sistemaFumaca != null) sistemaFumaca.Stop();
        
        _esperaQuedaLivre = new WaitForSeconds(tempoQuedaLivre);
    }

    public void IniciarAtaque(Vector3 alvo, Vector3 velocidadeInicialAviao, Transform alvoT = null)
    {
        StopAllCoroutines();
        pontoAlvo = alvo;
        alvoTransform = alvoT;
        lancado = true;
        jaExplodiu = false;
        motorLigado = false;
        _tempoExpirar = Time.time + tempoMaximoVida;
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
        
        velocidadeAtual = velocidadeInicialAviao.magnitude;
        rb.linearVelocity = velocidadeInicialAviao;
        
        StartCoroutine(SequenciaDeVoo());
    }

    IEnumerator SequenciaDeVoo()
    {
        // FASE 1: Cai do avião graciosamente (Queda Livre)
        rb.useGravity = true;
        rb.AddForce(Vector3.down * forcaQuedaAdicional, ForceMode.Impulse);
        
        yield return _esperaQuedaLivre;

        // FASE 2: Liga motor
        rb.useGravity = false;
        motorLigado = true;
        if (sistemaFumaca != null) sistemaFumaca.Play();
    }

    void FixedUpdate()
    {
        if (!lancado) return;

        if (Time.time >= _tempoExpirar)
        {
            Explodir();
            return;
        }

        if (alvoTransform != null) pontoAlvo = alvoTransform.position;

        Vector3 pontoDeMira = alvoTransform != null
            ? GuidagemAlvoMovel.ObterPontoDeMira(alvoTransform, transform.position, velocidadeAtual, 1.5f)
            : pontoAlvo;

        if (!motorLigado)
        {
            // A queda livre não faz parte da solução de interceptação.
            ultimaPosicaoGuiagem = transform.position;
            possuiUltimaPosicaoGuiagem = true;
            return;
        }

        Vector3 vetorParaAlvo = pontoAlvo - transform.position;
        
        // Gira para o alvo de forma suave (tracking)
        if (vetorParaAlvo.sqrMagnitude > 0.01f) // Evita normalização de vetor zero
        {
            Vector3 vetorDeMira = pontoDeMira - transform.position;
            Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorDeMira.sqrMagnitude > 0.01f
                ? vetorDeMira.normalized
                : vetorParaAlvo.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, forcaRotacao * Time.fixedDeltaTime);
        }

        velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeMaxima, Time.fixedDeltaTime * 2f);
        rb.linearVelocity = transform.forward * velocidadeAtual;

        // sqrMagnitude < 25 equivale a magnitude < 5 (evita sqrt)
        bool cruzouAlvo = GuidagemAlvoMovel.TentarObterPontoMaisProximoNoSegmento(
            possuiUltimaPosicaoGuiagem ? ultimaPosicaoGuiagem : transform.position,
            transform.position,
            pontoAlvo,
            out Vector3 pontoImpacto,
            Mathf.Max(5f, velocidadeAtual * Time.fixedDeltaTime));
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
        if (vetorParaAlvo.sqrMagnitude < 25f || cruzouAlvo)
        {
            if (cruzouAlvo) transform.position = pontoImpacto;
            Explodir();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!lancado || !motorLigado) return; // Apenas arma após a queda livre
        if (collision.collider.CompareTag("Missel")) return;
        if (PodeDetonarAoColidir(collision.collider)) Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!lancado || !motorLigado) return; // Apenas arma após a queda livre
        string strTag = other.tag;
        if (strTag == "Player" || strTag == "Missel" || strTag == "IgnorarExplosao") return; 

        if (PodeDetonarAoColidir(other)) Explodir();
    }

    private bool PodeDetonarAoColidir(Collider other)
    {
        if (other == null) return false;
        string tag = other.tag;
        if (tag == "Player" || tag == "Missel" || tag == "IgnorarExplosao") return false;

        if (alvoTransform != null)
        {
            Transform raizAlvo = alvoTransform.root != null ? alvoTransform.root : alvoTransform;
            Transform raizColisor = other.transform.root != null ? other.transform.root : other.transform;
            if (raizColisor == raizAlvo || other.transform.IsChildOf(raizAlvo)) return true;
        }

        if (other.isTrigger) return false;
        // Só uma colisão próxima da coordenada ordenada pode encerrar o voo.
        return Vector3.Distance(other.ClosestPoint(transform.position), pontoAlvo) <= 3f;
    }

    void Explodir()
    {
        // Guard: impede explosão dupla (pode ser chamado por colisão + proximidade no mesmo frame)
        if (jaExplodiu) return;
        jaExplodiu = true;

        if (efeitoExplosaoPrefab != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosaoPrefab,
                transform.position,
                Quaternion.identity,
                5f,
                Vector3.one * escalaVisualExplosao);
        }

        if (somExplosao != null && Camera.main != null)
        {
            float distCamSqr = (transform.position - Camera.main.transform.position).sqrMagnitude;
            if (distCamSqr < 40000f) // Não cria áudio pesadíssimo se estiver mt longe (>200m)
            {
                GameObject audioObj = new GameObject("SomExplosaoCaca");
                audioObj.transform.position = transform.position;
                AudioSource source = audioObj.AddComponent<AudioSource>();
                source.clip = somExplosao;
                source.volume = Mathf.Min(Mathf.Clamp01(volumeSom), 0.8f);
                source.spatialBlend = 1f; 
                source.minDistance = 3f;
                source.maxDistance = 300f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.Play();
                Destroy(audioObj, somExplosao.length + 0.1f);
            }
        }

        // OverlapSphereNonAlloc: O(1) em GC

        _alvosJaProcessados.Clear();
        int numHits = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, _explosaoBuffer);
        for (int i = 0; i < numHits; i++)
        {
            Collider col = _explosaoBuffer[i];
            if (col == null || col.isTrigger) continue;
            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
            {
                int id = alvoVida.GetInstanceID();
                if (!_alvosJaProcessados.Add(id)) continue;
                alvoVida.ReceberDano(dano);
            }
        }

        PoolDeObjetosCombate.Release(gameObject);
    }

    private void ResetarEstado()
    {
        pontoAlvo = Vector3.zero;
        alvoTransform = null;
        lancado = false;
        motorLigado = false;
        velocidadeAtual = 0f;
        jaExplodiu = false;
        ultimaPosicaoGuiagem = Vector3.zero;
        possuiUltimaPosicaoGuiagem = false;
        // Removido o new WaitForSeconds aqui para zerar alocações (GC) - Já é cacheado no Awake.
    }
}
