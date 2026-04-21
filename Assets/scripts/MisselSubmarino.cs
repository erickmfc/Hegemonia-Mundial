using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;

public class MisselSubmarino : MonoBehaviour
{
    [Header("Configuração de Voo")]
    [Tooltip("Velocidade máxima de cruzeiro")]
    public float velocidadeMaxima = 80f; 
    
    [Tooltip("Velocidade máxima no modo Turbo")]
    public float velocidadeTurbo = 150f;

    [Tooltip("Aceleração inicial")]
    public float aceleracao = 15f; 
    
    [Tooltip("Aceleração após o tempo de delay (Turbo)")]
    public float aceleracaoTurbo = 60f;

    [Tooltip("Tempo após sair da água para ativar o modo Turbo")]
    public float delayParaTurbo = 4.0f;

    [Tooltip("Altura de cruzeiro que o míssil deve manter")]
    public float alturaVoo = 300f; 
    
    [Tooltip("Distância horizontal do alvo para começar o mergulho final")]
    public float distanciaInicioMergulho = 100f;

    [Tooltip("Força de rotação para manobras")]
    public float forcaRotacao = 3f;
    
    [Header("Dano")]
    public float dano = 150f;
    public float raioExplosao = 15f;
    
    [Header("Efeitos")]
    [Tooltip("Rastro de fumaça do míssil")]
    public ParticleSystem sistemaFumaca;
    public float tempoMaximoVida = 28f;
    
    // Estado
    private Vector3 pontoAlvo;
    private Transform alvoTransform; // Seguimento em tempo real
    private float tempoLancamento;
    private bool estaSubmerso = true;
    private float velocidadeAtual = 0f;
    private bool atingiuAlturaVoo = false;
    private bool lancado = false;
    private bool modoTurboAtivo = false;
    private float tempoDesdeSuperficie = 0f;
    private bool naSuperficie = false;
    private Rigidbody rb;
    private static readonly Collider[] bufferExplosao = new Collider[48];
    private bool jaExplodiu = false;
    private float tempoExpirar;

    void OnEnable()
    {
        IA_CombatTelemetry.RegisterMissile();
        ResetarEstado();
        tempoExpirar = Time.time + tempoMaximoVida;
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
        GarantirComponentes();
    }
    
    public void IniciarLancamento(Vector3 alvo, bool submarinSubmerso, Transform alvoT = null)
    {
        GarantirComponentes();
        StopAllCoroutines();
        ResetarEstado();

        pontoAlvo = alvo;
        alvoTransform = alvoT;
        estaSubmerso = submarinSubmerso;
        lancado = true;
        tempoLancamento = Time.time;
        tempoExpirar = Time.time + tempoMaximoVida;
        
        // Força rotação para cima no início
        transform.rotation = Quaternion.LookRotation(Vector3.up);
        
        // Inicia sequência de lançamento
        StartCoroutine(SequenciaLancamento());
    }
    
    void Start()
    {
        GarantirComponentes();
    }

    void GarantirComponentes()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (sistemaFumaca != null && sistemaFumaca.isPlaying)
        {
            sistemaFumaca.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    
    IEnumerator SequenciaLancamento()
    {
        // FASE 1: Se está submerso, sobe até a superfície
        if (estaSubmerso)
        {
            yield return StartCoroutine(SubirParaSuperficie());
        }
        else
        {
            // Se já nasceu fora da água, considera que saiu agora
            naSuperficie = true; 
        }
        
        // Ativa fumaça
        AtivarRastro();
        
        // FASE 2: Sobe até altura de cruzeiro
        yield return StartCoroutine(SubirParaAltura());
        
        // FASE 3: Cruzeiro (gerenciado no FixedUpdate)
        atingiuAlturaVoo = true;
    }
    
    IEnumerator SubirParaSuperficie()
    {
        Debug.Log("Míssil saindo da água...");

        if (rb == null)
        {
            GarantirComponentes();
        }
        
        float tempoSubidaReta = 3.0f; // Sobe reto por um tempo (efeito dramático)
        float velocidadeInicial = 8f; 
        float tempoDecorrido = 0f;
        
        // Sobe com velocidade constante inicial
        while (tempoDecorrido < tempoSubidaReta && transform.position.y < 0f)
        {
            velocidadeAtual = velocidadeInicial;
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            tempoDecorrido += Time.deltaTime;
            yield return null;
        }
        
        // Acelera até a superfície
        while (transform.position.y < 0f)
        {
            velocidadeAtual += aceleracao * Time.deltaTime;
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            yield return null;
        }
        
        Debug.Log("Míssil na superfície!");
        naSuperficie = true;
        tempoDesdeSuperficie = 0f; // Reseta timer do turbo
    }
    
    IEnumerator SubirParaAltura()
    {
        Debug.Log("Míssil subindo para altitude de cruzeiro...");

        if (rb == null)
        {
            GarantirComponentes();
        }
        
        // Continua subindo até atingir a altura configurada
        // Nota: O FixedUpdate já vai começar a contar o tempo para o Turbo
        while (transform.position.y < alturaVoo)
        {
            // Atualiza velocidade
            AtualizarVelocidade(Time.deltaTime); // Usa aceleração normal ou turbo
            
            // Move para cima
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            
            yield return null;
        }
        
        Debug.Log("Míssil em altitude de cruzeiro! Iniciando navegação horizontal.");
    }

    void AtualizarVelocidade(float dt)
    {
        // Verifica se deve ativar o turbo
        if (naSuperficie && !modoTurboAtivo)
        {
            tempoDesdeSuperficie += dt;
            if (tempoDesdeSuperficie >= delayParaTurbo)
            {
                modoTurboAtivo = true;
                Debug.Log("MODO TURBO ATIVADO! Aceleração máxima!");
            }
        }

        // Define alvo de velocidade e aceleração baseados no modo
        float alvoVelocidade = modoTurboAtivo ? velocidadeTurbo : velocidadeMaxima;
        float accAtual = modoTurboAtivo ? aceleracaoTurbo : aceleracao;

        if (velocidadeAtual < alvoVelocidade)
        {
            velocidadeAtual += accAtual * dt;
            velocidadeAtual = Mathf.Min(velocidadeAtual, alvoVelocidade);
        }
    }
    
    void FixedUpdate()
    {
        if (!lancado) return;

        if (Time.time >= tempoExpirar)
        {
            Explodir();
            return;
        }

        if (rb == null)
        {
            GarantirComponentes();
            if (rb == null) return;
        }

        if (alvoTransform != null) pontoAlvo = alvoTransform.position;
        
        // Timer do turbo
        if (naSuperficie && !modoTurboAtivo && !atingiuAlturaVoo)
        {
            tempoDesdeSuperficie += Time.fixedDeltaTime;
        }

        if (atingiuAlturaVoo)
        {
            AtualizarVelocidade(Time.fixedDeltaTime);
            
            // --- NOVA LÓGICA DE NAVEGAÇÃO ROBUSTA ---
            Vector3 vetorParaAlvoGlobal = pontoAlvo - transform.position;
            float distanciaTotal = vetorParaAlvoGlobal.magnitude;
            
            Vector3 posicaoHorizontal = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 alvoHorizontal = new Vector3(pontoAlvo.x, 0, pontoAlvo.z);
            float distanciaHorizontal = Vector3.Distance(posicaoHorizontal, alvoHorizontal);

            Vector3 direcaoDesejada;
            float forcaRotacaoAtual = forcaRotacao;

            // 1. MODO MERGULHO FINAL
            // Se estiver perto horizontalmente OU se já estiver muito perto do alvo em geral
            if (distanciaHorizontal < distanciaInicioMergulho || distanciaTotal < distanciaInicioMergulho)
            {
                direcaoDesejada = vetorParaAlvoGlobal.normalized;
                
                // CRÍTICO: Aumenta DRASTICAMENTE a rotação no mergulho para evitar orbitar
                // Quanto mais perto, mais agressivo vira
                // Se estiver muito rápido (Turbo), precisa virar muito mais rápido
                float fatorProximidade = Mathf.Clamp01(150f / Mathf.Max(distanciaTotal, 1f)); // Aumenta conforme chega perto
                forcaRotacaoAtual = Mathf.Lerp(forcaRotacao, forcaRotacao * 8.0f, fatorProximidade);
                
                // Se estiver MUITO perto (< 20m) força LookAt imediato para garantir acerto
                if(distanciaTotal < 30f)
                {
                    forcaRotacaoAtual = 200f; // Rotação instantânea praticamente
                }
            }
            // 2. MODO CRUZEIRO
            else
            {
                // Ajuste inteligente: se o alvo é aéreo e muito mais alto que a altura de voo padrão, o míssil já sobe direto para ele.
                float alturaCruzeiroReal = Mathf.Max(alturaVoo, pontoAlvo.y);
                
                Vector3 destinoCruzeiro = new Vector3(pontoAlvo.x, alturaCruzeiroReal, pontoAlvo.z);
                Vector3 vetorCruzeiro = destinoCruzeiro - transform.position;
                direcaoDesejada = vetorCruzeiro.normalized;
                
                // Mantém rotação firme para não perder alvos ágeis
                forcaRotacaoAtual = forcaRotacao * 3f;
            }

            // Aplica Rotação usando RotateTowards para controle preciso de graus/segundo
            if (direcaoDesejada != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoDesejada);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, forcaRotacaoAtual * 50f * Time.fixedDeltaTime);
            }
            
            // Move para frente
            rb.linearVelocity = transform.forward * velocidadeAtual;

            // 3. DETONADOR DE PROXIMIDADE
            // Aumentado levemente para evitar missed hits por frame skipping em alta velocidade
            if (distanciaTotal < 5.0f || (distanciaTotal < 10.0f && velocidadeAtual > 100f))
            {
                Explodir();
            }
        }
    }
    
    public void AtivarRastro()
    {
        if (sistemaFumaca != null)
        {
            sistemaFumaca.Play();
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < tempoLancamento + 1.2f) return; // Delay de armamento para evitar explosão na ponta do cano
        if (collision.collider.CompareTag("Missel")) return;

        Explodir();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (Time.time < tempoLancamento + 1.2f) return;
        if (other.isTrigger) return;
        
        string strTag = other.tag;
        if (strTag == "Player" || strTag == "Missel" || strTag == "IgnorarExplosao") return;

        Explodir();
    }
    
    [Header("Efeitos da Explosão")]
    public GameObject efeitoVisualExplosao; // Principal
    public GameObject[] efeitosVisuaisExtras; // Extras (vários efeitos)
    public float escalaVisualExplosao = 1.0f; 
    public float tempoDuracaoExplosao = 7.0f; // Tempo garantido
    public AudioClip somExplosao; 
    public float volumeSom = 1.0f; 
    
    void Explodir()
    {
        if (jaExplodiu)
        {
            return;
        }

        jaExplodiu = true;

        // Debug eliminado para performance se necessário, mas mantido por enquanto
        // Debug.Log("Míssil explodiu!");
        
        // 1. Cria TODOS os efeitos visuais
        List<GameObject> efeitosParaCriar = new List<GameObject>();
        if (efeitoVisualExplosao != null) efeitosParaCriar.Add(efeitoVisualExplosao);
        if (efeitosVisuaisExtras != null) efeitosParaCriar.AddRange(efeitosVisuaisExtras);

        if (efeitosParaCriar.Count > 0)
        {
            foreach (var prefabFx in efeitosParaCriar)
            {
                if (prefabFx != null)
                {
                    PoolDeObjetosCombate.SpawnTemporario(
                        prefabFx,
                        transform.position,
                        Quaternion.identity,
                        tempoDuracaoExplosao,
                        Vector3.one * escalaVisualExplosao);
                }
            }
        }
        else if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, escalaVisualExplosao);
        }
        
        // 2. Toca o Som
        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMissil");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = volumeSom;
            source.spatialBlend = 1.0f;
            source.minDistance = 10f;
            source.maxDistance = 500f;
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }
        
        // 3. Aplica dano
        int objetosNaArea = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao);
        for (int i = 0; i < objetosNaArea; i++)
        {
            Collider obj = bufferExplosao[i];
            if (obj == null)
            {
                continue;
            }

            SistemaDeDanos sistemaDano = obj.GetComponent<SistemaDeDanos>();
            if (sistemaDano != null)
            {
                float distancia = Vector3.Distance(transform.position, obj.transform.position);
                float multiplicador = 1f - Mathf.Clamp01(distancia / raioExplosao);
                sistemaDano.ReceberDano(dano * multiplicador);
            }
            
            Rigidbody rbAlvo = obj.GetComponent<Rigidbody>();
            if (rbAlvo != null)
            {
                 rbAlvo.AddExplosionForce(2000f, transform.position, raioExplosao);
            }

            bufferExplosao[i] = null;
        }
        
        PoolDeObjetosCombate.Release(gameObject);
    }

    private void ResetarEstado()
    {
        pontoAlvo = Vector3.zero;
        alvoTransform = null;
        estaSubmerso = true;
        velocidadeAtual = 0f;
        atingiuAlturaVoo = false;
        lancado = false;
        modoTurboAtivo = false;
        tempoDesdeSuperficie = 0f;
        naSuperficie = false;
        jaExplodiu = false;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
        Gizmos.color = Color.yellow;
        if (lancado) Gizmos.DrawLine(transform.position, pontoAlvo);
        
        Vector3 posAltura = new Vector3(transform.position.x, alturaVoo, transform.position.z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, posAltura);
        Gizmos.DrawSphere(posAltura, 1.0f);
    }
}
