using UnityEngine;
using System.Collections;

public class MisselSubmarino : MonoBehaviour
{
    [Header("Configuração de Voo")]
    [Tooltip("Velocidade máxima do míssil")]
    public float velocidadeMaxima = 80f; // Aumentado para compensar a altitude maior
    
    [Tooltip("Aceleração do míssil (aumenta velocidade ao longo do tempo)")]
    public float aceleracao = 15f; // Aumentado levemente para chegar à velocidade máxima mais rápido
    
    [Tooltip("Altura que o míssil deve alcançar antes de ir ao alvo")]
    public float alturaVoo = 300f; // Aumentado significativamente conforme solicitado (era 100f)
    
    [Tooltip("Força de rotação para seguir o alvo")]
    public float forcaRotacao = 3f;
    
    [Header("Dano")]
    public float dano = 150f;
    public float raioExplosao = 15f;
    
    [Header("Efeitos")]
    [Tooltip("Rastro de fumaça do míssil")]
    public ParticleSystem sistemaFumaca;
    
    // Estado
    private Vector3 pontoAlvo;
    private bool estaSubmerso = true;
    private float velocidadeAtual = 0f;
    private bool atingiuAlturaVoo = false;
    private bool lancado = false;
    private Rigidbody rb;
    
    public void IniciarLancamento(Vector3 alvo, bool submarinSubmerso)
    {
        pontoAlvo = alvo;
        estaSubmerso = submarinSubmerso;
        lancado = true;
        
        // Força rotação para cima no início
        transform.rotation = Quaternion.LookRotation(Vector3.up);
        
        // Inicia sequência de lançamento
        StartCoroutine(SequenciaLancamento());
    }
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        rb.useGravity = false;
        rb.isKinematic = false;
        
        // TRAVA ROTAÇÃO - Só o script pode girar o míssil
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        // Desativa fumaça no início
        if (sistemaFumaca != null)
        {
            sistemaFumaca.Stop();
        }
    }
    
    IEnumerator SequenciaLancamento()
    {
        // Se está submerso, sobe até a superfície primeiro
        if (estaSubmerso)
        {
            yield return StartCoroutine(SubirParaSuperficie());
        }
        
        // Ativa fumaça após sair da água
        AtivarRastro();
        
        // Sobe até altura de voo
        yield return StartCoroutine(SubirParaAltura());
        
        // Agora persegue o alvo
        atingiuAlturaVoo = true;
    }
    
    IEnumerator SubirParaSuperficie()
    {
        Debug.Log("Míssil saindo da água...");
        
        float tempoSubidaReta = 3.0f; // 3 segundos subindo reto
        float velocidadeInicial = 8f; // Velocidade lenta inicial
        float tempoDecorrido = 0f;
        
        // FASE 1: Sobe reto por 3 segundos com velocidade constante baixa
        while (tempoDecorrido < tempoSubidaReta && transform.position.y < 0f)
        {
            velocidadeAtual = velocidadeInicial; // Mantém velocidade constante
            
            Vector3 direcao = Vector3.up;
            rb.linearVelocity = direcao * velocidadeAtual;
            
            tempoDecorrido += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log("Míssil iniciando aceleração...");
        
        // FASE 2: Continua subindo até a superfície, mas agora COM aceleração
        while (transform.position.y < 0f)
        {
            velocidadeAtual += aceleracao * Time.deltaTime;
            velocidadeAtual = Mathf.Min(velocidadeAtual, velocidadeMaxima * 0.5f); // 50% da velocidade máxima na água
            
            Vector3 direcao = Vector3.up;
            rb.linearVelocity = direcao * velocidadeAtual;
            
            yield return null;
        }
        
        Debug.Log("Míssil na superfície!");
    }
    
    IEnumerator SubirParaAltura()
    {
        Debug.Log("Míssil subindo para altitude de cruzeiro...");
        
        // Continua subindo com aceleração
        while (transform.position.y < alturaVoo)
        {
            velocidadeAtual += aceleracao * Time.deltaTime;
            velocidadeAtual = Mathf.Min(velocidadeAtual, velocidadeMaxima);
            
            Vector3 direcao = Vector3.up;
            rb.linearVelocity = direcao * velocidadeAtual;
            
            yield return null;
        }
        
        Debug.Log("Míssil em altitude de cruzeiro!");
    }
    
    void FixedUpdate()
    {
        if (!lancado) return;
        
        if (atingiuAlturaVoo)
        {
            // Persegue o alvo
            Vector3 direcao = (pontoAlvo - transform.position).normalized;
            
            // Acelera se ainda não atingiu velocidade máxima
            if (velocidadeAtual < velocidadeMaxima)
            {
                velocidadeAtual += aceleracao * Time.fixedDeltaTime;
                velocidadeAtual = Mathf.Min(velocidadeAtual, velocidadeMaxima);
            }
            
            // Verifica distância para o alvo
            float distancia = Vector3.Distance(transform.position, pontoAlvo);
            
            // DETONADOR DE PROXIMIDADE: Se chegar muito perto, explode!
            if (distancia < 3.0f)
            {
                Explodir();
                return;
            }

            // Rotaciona suavemente para o alvo (usa transform já que física está travada)
            // Só rotaciona se estiver a uma distância segura para evitar "giro louco"
            if (distancia > 1.0f)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, forcaRotacao * Time.fixedDeltaTime);
            }
            
            // Move na direção que está olhando (se estiver perto, mergulha com tudo)
            rb.linearVelocity = transform.forward * velocidadeAtual;
        }
    }
    
    public void AtivarRastro()
    {
        if (sistemaFumaca != null)
        {
            sistemaFumaca.Play();
            Debug.Log("Rastro de fumaça ativado!");
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Qualquer colisão explode (você pode adicionar filtros de layer aqui se quiser)
        Explodir();
    }
    
    [Header("Efeitos da Explosão")]
    public GameObject efeitoVisualExplosao; // Prefab do efeito visual da explosão
    public float escalaVisualExplosao = 1.0f; // Tamanho do efeito visual
    public float tempoDuracaoExplosao = 5.0f; // Tempo que a explosão fica na tela
    public AudioClip somExplosao; // Som da explosão
    public float volumeSom = 1.0f; // Volume do som
    
    void Explodir()
    {
        Debug.Log("Míssil explodiu!");
        
        // 1. Cria o Efeito Visual
        if (efeitoVisualExplosao != null)
        {
            GameObject fx = Instantiate(efeitoVisualExplosao, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaVisualExplosao;
            
            // Tenta ajustar a duração das partículas para elas durarem mais, se possível
            var par = fx.GetComponent<ParticleSystem>();
            if (par != null)
            {
               var main = par.main;
               // Se o tempo pedido for muito longo, aumenta a duração da partícula também
               if (tempoDuracaoExplosao > main.duration) main.duration = tempoDuracaoExplosao;
            }

            Destroy(fx, tempoDuracaoExplosao); // Usa o tempo configurado
        }
        else if (GerenciadorFXGlobal.Instancia != null)
        {
            // Fallback para o gerenciador global se não tiver efeito específico
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, escalaVisualExplosao);
        }
        
        // 2. Toca o Som
        if (somExplosao != null)
        {
            // Cria um objeto temporário para tocar o som (para não cortar se o míssil for destruído)
            GameObject audioObj = new GameObject("SomExplosaoMissil");
            audioObj.transform.position = transform.position;
            
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = volumeSom;
            source.spatialBlend = 1.0f; // 3D
            source.minDistance = 10f;
            source.maxDistance = 500f;
            source.Play();
            
            Destroy(audioObj, somExplosao.length + 0.5f);
        }
        
        // 3. Aplica dano em área
        Collider[] objetosNaArea = Physics.OverlapSphere(transform.position, raioExplosao);
        
        foreach (Collider obj in objetosNaArea)
        {
            SistemaDeDanos sistemaDano = obj.GetComponent<SistemaDeDanos>();
            if (sistemaDano != null)
            {
                // Calcula dano baseado na distância (mais perto = mais dano)
                float distancia = Vector3.Distance(transform.position, obj.transform.position);
                float multiplicadorDistancia = 1f - Mathf.Clamp01(distancia / raioExplosao);
                float danoFinal = dano * multiplicadorDistancia;
                
                sistemaDano.ReceberDano(danoFinal);
            }
            
            // Empurra objetos físicos
            Rigidbody rbAlvo = obj.GetComponent<Rigidbody>();
            if (rbAlvo != null)
            {
                 rbAlvo.AddExplosionForce(2000f, transform.position, raioExplosao);
            }
        }
        
        // Destroi o míssil
        Destroy(gameObject);
    }
    
    void OnDrawGizmosSelected()
    {
        // Desenha raio de explosão no editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
        
        // Desenha linha até o alvo
        if (lancado)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, pontoAlvo);
        }
    }
}
