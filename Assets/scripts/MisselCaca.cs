using UnityEngine;
using System.Collections;

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

    private Vector3 pontoAlvo;
    private Transform alvoTransform;
    private bool lancado = false;
    private bool motorLigado = false;
    private float velocidadeAtual = 0f;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false; 
        rb.isKinematic = false;
        rb.freezeRotation = true; 

        if (sistemaFumaca != null) sistemaFumaca.Stop();
    }

    public void IniciarAtaque(Vector3 alvo, Vector3 velocidadeInicialAviao, Transform alvoT = null)
    {
        pontoAlvo = alvo;
        alvoTransform = alvoT;
        lancado = true;
        
        // Mantém a inércia do avião inicialmente
        velocidadeAtual = velocidadeInicialAviao.magnitude;
        rb.linearVelocity = velocidadeInicialAviao;
        
        // Aponta para a frente inicialmente mantendo a inclinação
        
        StartCoroutine(SequenciaDeVoo());
    }

    IEnumerator SequenciaDeVoo()
    {
        // FASE 1: Cai do avião graciosamente (Queda Livre)
        rb.useGravity = true;
        // Empurra levemente pra baixo para desgrudar da asa
        rb.AddForce(Vector3.down * forcaQuedaAdicional, ForceMode.Impulse);
        
        yield return new WaitForSeconds(tempoQuedaLivre);

        // FASE 2: Liga motor
        rb.useGravity = false;
        motorLigado = true;
        if (sistemaFumaca != null) sistemaFumaca.Play();
    }

    void FixedUpdate()
    {
        if (!lancado) return;

        if (alvoTransform != null) pontoAlvo = alvoTransform.position;

        if (motorLigado)
        {
            Vector3 vetorParaAlvo = pontoAlvo - transform.position;
            
            // Gira para o alvo de forma suave (tracking)
            if (vetorParaAlvo != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorParaAlvo.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, forcaRotacao * Time.fixedDeltaTime);
            }

            velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeMaxima, Time.fixedDeltaTime * 2f);
            rb.linearVelocity = transform.forward * velocidadeAtual;

            if (vetorParaAlvo.magnitude < 5f)
            {
                Explodir();
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignora avião e colisor de gatilhos amigos
        if (other.CompareTag("Player")) return; 
        Explodir();
    }

    void Explodir()
    {
        if (efeitoExplosaoPrefab != null)
        {
            GameObject fx = Instantiate(efeitoExplosaoPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaVisualExplosao;
            Destroy(fx, 5f); 
        }

        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMissilCaca");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = volumeSom;
            source.spatialBlend = 1f; 
            source.minDistance = 20f;
            source.maxDistance = 500f; 
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }

        Collider[] atingidos = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (Collider col in atingidos)
        {
            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
            {
                alvoVida.ReceberDano(dano);
            }
        }

        Destroy(gameObject);
    }
}
