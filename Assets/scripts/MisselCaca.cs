using UnityEngine;
using System.Collections;
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

    // --- CACHE: Buffer reutilizável para OverlapSphere (reduz GC) ---
    private static readonly Collider[] _explosaoBuffer = new Collider[32];
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

        if (!motorLigado) return;

        Vector3 vetorParaAlvo = pontoAlvo - transform.position;
        
        // Gira para o alvo de forma suave (tracking)
        if (vetorParaAlvo.sqrMagnitude > 0.01f) // Evita normalização de vetor zero
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorParaAlvo.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, forcaRotacao * Time.fixedDeltaTime);
        }

        velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeMaxima, Time.fixedDeltaTime * 2f);
        rb.linearVelocity = transform.forward * velocidadeAtual;

        // sqrMagnitude < 25 equivale a magnitude < 5 (evita sqrt)
        if (vetorParaAlvo.sqrMagnitude < 25f)
            Explodir();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!lancado || !motorLigado) return; // Apenas arma após a queda livre
        if (collision.collider.CompareTag("Missel")) return;
        Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!lancado || !motorLigado) return; // Apenas arma após a queda livre
        if (other.isTrigger) return;          // Ignora colisores de radar/áreas
        
        string strTag = other.tag;
        if (strTag == "Player" || strTag == "Missel" || strTag == "IgnorarExplosao") return; 

        Explodir();
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
                source.volume = volumeSom;
                source.spatialBlend = 1f; 
                source.minDistance = 20f;
                source.maxDistance = 500f; 
                source.Play();
                Destroy(audioObj, somExplosao.length + 0.1f);
            }
        }

        // OverlapSphereNonAlloc: O(1) em GC

        int numHits = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, _explosaoBuffer);
        for (int i = 0; i < numHits; i++)
        {
            Collider col = _explosaoBuffer[i];
            if (col == null) continue;
            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
                alvoVida.ReceberDano(dano);
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
        // Removido o new WaitForSeconds aqui para zerar alocações (GC) - Já é cacheado no Awake.
    }
}
