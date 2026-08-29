using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// [MÍSSIL INTELIGENTE LEOPARD V1]
// Adaptado do Míssil Submarino para uso em MLRS Terrestre (Anti-Tudo)
// Funciona contra: Aeronaves, Navios, Tanques.

public class MisselLeopardAutomatico : MonoBehaviour
{
    [Header("Configuração de Voo")]
    [Tooltip("Velocidade normal de cruzeiro")]
    public float velocidadeMaxima = 60f; 
    
    [Tooltip("Velocidade quando entra em modo de ataque final")]
    public float velocidadeTurbo = 120f;

    [Tooltip("Aceleração")]
    public float aceleracao = 20f; 
    
    [Tooltip("Altura que ele tenta subir antes de mergulhar no alvo")]
    public float alturaVoo = 40f; 
    
    [Tooltip("Quão rápido ele vira para seguir o alvo")]
    public float forcaRotacao = 5f;
    
    [Header("Dano")]
    public float dano = 750f;
    public float raioExplosao = 70f;
    
    [Header("Efeitos")]
    public ParticleSystem sistemaFumaca;
    
    // Estado
    private Transform alvoFocado; // O alvo vivo
    private Vector3 ultimaPosicaoConhecida; // Caso perca o alvo
    private float velocidadeAtual = 0f;
    private bool lancado = false;
    private bool emMergulho = false;
    private Rigidbody rb;
    private Vector3 ultimaPosicaoGuiagem;
    private bool possuiUltimaPosicaoGuiagem;
    
    public void DefinirAlvo(Transform alvo)
    {
        alvoFocado = alvo;
        lancado = true;
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
        
        // Ativa fumaça se tiver
        if (sistemaFumaca != null) sistemaFumaca.Play();
        
        // Impulso inicial para cima (para sair do tubo bonito)
        if(rb) rb.AddForce(transform.forward * 10f + Vector3.up * 5f, ForceMode.VelocityChange);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false; // Míssil voa, não cai
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Script controla rotação
        
        // Som inicial se quiser (opcional)
    }

    void FixedUpdate()
    {
        if (!lancado) return;

        // 1. ATUALIZA POSIÇÃO DO ALVO
        Vector3 destinoFinal;
        
        if (alvoFocado != null)
        {
            ultimaPosicaoConhecida = alvoFocado.position;
            // A antecipação só orienta o míssil. A posição conhecida continua
            // sendo a posição real para que uma perda de contato não crie um
            // impacto em uma previsão antiga.
            destinoFinal = GuidagemAlvoMovel.ObterPontoDeMira(
                alvoFocado,
                transform.position,
                Mathf.Max(velocidadeAtual, velocidadeMaxima),
                1.5f);
        }
        else
        {
            destinoFinal = ultimaPosicaoConhecida; // Vai até a última posição se alvo morrer
            // Se chegou na última posição conhecida e não explodiu, explode
            if(Vector3.Distance(transform.position, destinoFinal) < 5f)
            {
                Explodir();
                return;
            }
        }

        // 2. LÓGICA DE VOO (Arco Balístico Inteligente)
        float distancia = Vector3.Distance(transform.position, destinoFinal);
        Vector3 posicaoAlvoReal = alvoFocado != null ? alvoFocado.position : ultimaPosicaoConhecida;
        float distanciaAlvoReal = Vector3.Distance(transform.position, posicaoAlvoReal);
        
        Vector3 direcaoDesejada;

        // Se estiver longe, tenta subir para "Altura de Voo" para evitar obstáculos
        if (distancia > 50f && !emMergulho)
        {
            // Ponto intermediário: Em cima do alvo na altura definida
            Vector3 pontoAlto = destinoFinal;
            pontoAlto.y += alturaVoo;
            
            // Interpola entre "Ir reto" e "Ir para o alto"
            // Se estiver muito baixo, prioriza subir
            if(transform.position.y < alturaVoo)
            {
                direcaoDesejada = (pontoAlto - transform.position).normalized;
            }
            else
            {
                // Já está alto, mira direto no alvo
                direcaoDesejada = (destinoFinal - transform.position).normalized;
                emMergulho = true; // Começa a descer
            }
        }
        else
        {
            // Perto do alvo: Ataque Direto (Kamikaze)
            emMergulho = true;
            direcaoDesejada = (destinoFinal - transform.position).normalized;
        }

        // 3. MOVIMENTO
        // Acelera
        float alvoVel = emMergulho ? velocidadeTurbo : velocidadeMaxima;
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, alvoVel, aceleracao * Time.fixedDeltaTime);
        
        // Gira
        if (direcaoDesejada != Vector3.zero)
        {
            // Rotação mais agressiva no final
            float fatorRotacao = emMergulho ? forcaRotacao * 3f : forcaRotacao;
            Quaternion rotAlvo = Quaternion.LookRotation(direcaoDesejada);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, fatorRotacao * 50f * Time.fixedDeltaTime);
        }
        
        // Move
        rb.linearVelocity = transform.forward * velocidadeAtual;

        // 4. DETONAÇÃO DE PROXIMIDADE. O teste do segmento cobre a passagem
        // entre dois FixedUpdates quando o alvo ou o míssil estão rápidos.
        bool cruzouAlvo = GuidagemAlvoMovel.TentarObterPontoMaisProximoNoSegmento(
            possuiUltimaPosicaoGuiagem ? ultimaPosicaoGuiagem : transform.position,
            transform.position,
            posicaoAlvoReal,
            out Vector3 pontoImpacto,
            Mathf.Max(5f, velocidadeAtual * Time.fixedDeltaTime));
        ultimaPosicaoGuiagem = transform.position;
        possuiUltimaPosicaoGuiagem = true;
        if (distanciaAlvoReal < 5.0f || cruzouAlvo)
        {
            if (cruzouAlvo) transform.position = pontoImpacto;
            Explodir();
        }
    }
    
    // --- COLISÕES ---
    void OnCollisionEnter(Collision col)
    {
        if (PodeDetonarAoColidir(col.collider)) Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (PodeDetonarAoColidir(other)) Explodir();
    }

    private bool PodeDetonarAoColidir(Collider other)
    {
        if (!lancado || other == null) return false;
        if (other.CompareTag("Player") || other.CompareTag("Missel") || other.CompareTag("IgnorarExplosao")) return false;

        Transform raizOutro = other.transform.root != null ? other.transform.root : other.transform;
        Transform alvoAtual = alvoFocado != null && alvoFocado.gameObject.activeInHierarchy ? alvoFocado : null;
        if (alvoAtual != null)
        {
            Transform raizAlvo = alvoAtual.root != null ? alvoAtual.root : alvoAtual;
            if (raizOutro == raizAlvo || other.transform.IsChildOf(raizAlvo)) return true;
            if (other.isTrigger) return false;
            return Vector3.Distance(other.ClosestPoint(transform.position), alvoAtual.position) <= 8f;
        }

        if (other.isTrigger) return false;
        return Vector3.Distance(other.ClosestPoint(transform.position), ultimaPosicaoConhecida) <= 8f;
    }

    // --- EXPLOSÃO (Idêntica ao Submarino) ---
    
    [Header("Efeitos da Explosão")]
    public GameObject efeitoVisualExplosao; 
    public GameObject[] efeitosVisuaisExtras;
    public float escalaVisualExplosao = 15f; 
    public float tempoDuracaoExplosao = 8.0f;
    public AudioClip somExplosao; 
    public float volumeSom = 5.0f; 
    
    void Explodir()
    {
        if (!lancado) return;
        lancado = false;

        // 1. Efeitos Visuais
        CriarEfeito(efeitoVisualExplosao);
        if (efeitosVisuaisExtras != null)
        {
            foreach (var fx in efeitosVisuaisExtras) CriarEfeito(fx);
        }

        // 2. Som
        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMLRS");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = Mathf.Min(Mathf.Clamp01(volumeSom), 0.8f);
            source.spatialBlend = 1.0f;
            source.minDistance = 3f;
            source.maxDistance = 300f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }
        
        // 3. Dano em Área
        Collider[] objetosNaArea = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (Collider obj in objetosNaArea)
        {
            // Aplica dano
            SistemaDeDanos sistemaDano = obj.GetComponent<SistemaDeDanos>()
                ?? obj.GetComponentInParent<SistemaDeDanos>();
            if (sistemaDano != null)
            {
                float dist = Vector3.Distance(transform.position, obj.transform.position);
                float mult = 1f - Mathf.Clamp01(dist / raioExplosao);
                sistemaDano.ReceberDano(dano * mult);
            }
            
            // Empurra
            Rigidbody rbAlvo = obj.GetComponent<Rigidbody>();
            if (rbAlvo != null) rbAlvo.AddExplosionForce(2000f, transform.position, raioExplosao);
        }
        
        Destroy(gameObject);
    }

    void CriarEfeito(GameObject prefab)
    {
        if (prefab != null)
        {
            GameObject fx = Instantiate(prefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaVisualExplosao;
            Destroy(fx, tempoDuracaoExplosao);
        }
    }
}
