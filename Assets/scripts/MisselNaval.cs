using UnityEngine;
using System.Collections;

public class MisselNaval : MonoBehaviour
{
    [Header("Fase 1: Ejeção (Lançamento Frio)")]
    public float velocidadeEjecao = 5f;
    public float tempoEjecao = 2.5f;

    [Header("Fase 2: Boost Vertical")]
    public float tempoBoostVertical = 4.0f;
    public float aceleracaoBoost = 35f; // Aceleração forte
    public ParticleSystem sistemaFumaca;

    [Header("Fase 3: Cruzeiro e Mergulho")]
    public float velocidadeCruzeiro = 90f;
    public float velocidadeMergulho = 180f;
    public float distanciaInicioMergulho = 120f;
    public float forcaRotacaoCruzeiro = 1.5f; // Curva suave e bonita
    public float forcaRotacaoMergulho = 5.0f; // Curva agressiva no final

    [Header("Explosão")]
    public float dano = 200f;
    public float raioExplosao = 20f;
    public float escalaVisualExplosao = 1.0f;
    [Range(0f, 1f)]
    public float volumeSom = 1.0f;
    public GameObject efeitoExplosaoPrefab;
    public AudioClip somExplosao;

    [Header("Visual da Fumaça")]
    public Color corFumaca = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Cinza Claro e levemente transparente
    public float tamanhoFumaca = 1.5f;

    // Estado interno
    private Vector3 pontoAlvo;
    private Transform alvoTransform; // Referência para alvo móvel
    private bool lancado = false;
    private bool emNavegacao = false; // Só navega após subir
    private float velocidadeAtual = 0f;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false; 
        rb.isKinematic = false;
        rb.freezeRotation = true; // Controlamos a rotação via script

        // Configura a fumaça via script para evitar "névoa preta" feia
        if(sistemaFumaca != null) 
        {
            sistemaFumaca.Stop();
            var main = sistemaFumaca.main;
            main.startColor = corFumaca;
            main.startSize = tamanhoFumaca;
        }
    }

    // Sobrecarga para suportar alvo móvel
    public void IniciarAtaque(Vector3 alvo, Transform alvoT = null)
    {
        pontoAlvo = alvo;
        alvoTransform = alvoT;
        lancado = true;
        
        // Aponta para cima inicialmente
        transform.rotation = Quaternion.LookRotation(Vector3.up);
        
        StartCoroutine(SequenciaDeVoo());
    }

    IEnumerator SequenciaDeVoo()
    {
        // --- FASE 1: EJEÇÃO LENTA ---
        float tempo = 0f;
        velocidadeAtual = velocidadeEjecao;

        while (tempo < tempoEjecao)
        {
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.up), Time.deltaTime * 5f);
            tempo += Time.deltaTime;
            yield return null;
        }

        // --- FASE 2: BOOST VERTICAL ---
        if (sistemaFumaca != null) sistemaFumaca.Play();
        
        tempo = 0f;
        while (tempo < tempoBoostVertical)
        {
            velocidadeAtual += aceleracaoBoost * Time.deltaTime;
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.up), Time.deltaTime * 5f);
            tempo += Time.deltaTime;
            yield return null;
        }

        // --- FASE 3: INICIAR NAVEGAÇÃO ---
        emNavegacao = true;
    }

    void FixedUpdate()
    {
        if (!lancado || !emNavegacao) return;

        // SEGUIR ALVO MÓVEL (IMPORTANTE)
        if (alvoTransform != null)
        {
            pontoAlvo = alvoTransform.position;
        }

        Vector3 vetorParaAlvo = pontoAlvo - transform.position;
        float distanciaHorizontal = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(pontoAlvo.x, pontoAlvo.z));
        float distanciaTotal = vetorParaAlvo.magnitude;

        // Lógica de Velocidade e Rotação
        if (distanciaHorizontal > distanciaInicioMergulho)
        {
            // MODO CRUZEIRO
            Vector3 direcaoHorizontal = new Vector3(vetorParaAlvo.x, 0, vetorParaAlvo.z).normalized;
            Vector3 direcaoDesejada = direcaoHorizontal;

            if (transform.position.y > pontoAlvo.y + 100f)
            {
                 direcaoDesejada = (direcaoHorizontal - Vector3.up * 0.1f).normalized;
            }
            
            GirarPara(direcaoDesejada, forcaRotacaoCruzeiro * 3f);
            velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeCruzeiro, Time.fixedDeltaTime * 0.8f);
        }
        else
        {
            // MODO MERGULHO
            GirarPara(vetorParaAlvo.normalized, forcaRotacaoMergulho);
            velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeMergulho, Time.fixedDeltaTime * 2f);
        }

        rb.linearVelocity = transform.forward * velocidadeAtual;

        if (distanciaTotal < 10f)
        {
            Explodir();
        }
    }

    void GirarPara(Vector3 direcao, float velocidadeGiro)
    {
        if (direcao == Vector3.zero) return;
        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeGiro * Time.fixedDeltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return; 
        Explodir();
    }

    void Explodir()
    {
        // 1. Efeitos Visuais com Escala
        if (efeitoExplosaoPrefab != null)
        {
            GameObject fx = Instantiate(efeitoExplosaoPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaVisualExplosao;
            Destroy(fx, 5f); 
        }

        // 2. Som com Volume Controlado
        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMissil");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = volumeSom;
            source.spatialBlend = 1f; // Som 3D
            source.minDistance = 10f;
            source.maxDistance = 600f; // Auditoria de longe
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }

        // 3. Dano em Área
        Collider[] atingidos = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (Collider col in atingidos)
        {
            // CORDIALIDADE: Busca no pai também, caso o colisor esteja numa malha filha (comum em prefabs importados)
            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
            {
                alvoVida.ReceberDano(dano);
            }
        }

        // 4. Destruir o míssil
        Destroy(gameObject);
    }
}
