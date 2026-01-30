using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavegacaoInteligenteNaval : MonoBehaviour
{
    [Header("Configurações de Navegação")]
    [Tooltip("Ângulo em graus para considerar que o destino está 'atrás' (90 = meio do navio, 180 = popa total)")]
    [Range(90f, 180f)]
    public float anguloParaMarchaRe = 130f; // Aumentei um pouco para forçar mais curvas de frente
    
    [Tooltip("Distância máxima para usar marcha à ré (em metros)")]
    [Range(5f, 300f)]
    public float distanciaMaximaRe = 300f; 
    
    [Tooltip("Velocidade de marcha à ré (% da velocidade normal)")]
    [Range(0.3f, 1.0f)]
    public float velocidadeRe = 0.5f; 

    [Header("Física do Navio (Realismo)")]
    [Tooltip("Velocidade máxima de rotação do leme (graus por segundo).")]
    public float velocidadeGiroMax = 10f; // Reduzido de 25 para 10 (60% menos)
    
    [Tooltip("Quanto tempo o navio demora para acelerar totalmente (inércia).")]
    public float aceleracao = 2.0f;

    [Header("Configurações Visuais")]
    public TrailRenderer rastroAgua;
    public Transform modelo3D;
    public float forcaInclinacao = 5.0f;
    
    [Header("Debug Visual")]
    public bool mostrarDebugVisual = true;
    public Color corSetaFrente = Color.green;
    public Color corSetaRe = Color.red;
    
    private NavMeshAgent agente;
    private bool emMarchaRe = false;
    private Vector3 destinoAtual;
    private float velocidadeOriginal;
    private bool temDestino = false;
    private float velocidadeAtualSimulada = 0f; // Controlamos a velocidade manualmente para inércia
    private float lemeAtual = 0f; // O estado atual do leme (-1 a 1)

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        velocidadeOriginal = agente.speed;

        // Desliga tudo que é automático do NavMeshAgent
        agente.updateRotation = false; 
        agente.updatePosition = true; // Mantém true para ele colar no navmesh
        agente.acceleration = 9999; // Tiramos a aceleração interna dele para usarmos a nossa lógica
        
        if (rastroAgua == null) 
            rastroAgua = GetComponentInChildren<TrailRenderer>();
    }
    
    void Update()
    {
        if (agente == null || !agente.enabled) return;
        
        // TECLA "I" = PARADA DE EMERGÊNCIA (Desliga o motor na hora)
        // Só funciona se o navio estiver selecionado
        if (Input.GetKeyDown(KeyCode.I))
        {
            var controleUnidade = GetComponent<ControleUnidade>();
            if (controleUnidade != null && controleUnidade.selecionado)
            {
                agente.ResetPath(); // Cancela o caminho
                agente.velocity = Vector3.zero;
                velocidadeAtualSimulada = 0f;
                lemeAtual = 0f; // Reseta o leme
                emMarchaRe = false;
                temDestino = false;
                Debug.Log("[Navio] PARADA DE EMERGÊNCIA! Motor desligado.");
                return;
            }
        }
        
        // Verifica destino
        if (agente.hasPath && agente.remainingDistance > agente.stoppingDistance)
        {
            Vector3 destinoReal = agente.destination;
            
            // Lógica para decidir se troca o destino (evita recalculos desnecessários)
            if (Vector3.Distance(destinoAtual, destinoReal) > 1.0f)
            {
                destinoAtual = destinoReal;
                DecidirEstrategiaNavegacao(destinoReal);
                temDestino = true;
            }
            
            if (emMarchaRe)
            {
                ExecutarMarchaRe();
            }
            else
            {
                ExecutarMarchaFrenteRealista();
            }
        }
        else
        {
            // Freio suave (inércia na água)
            velocidadeAtualSimulada = Mathf.Lerp(velocidadeAtualSimulada, 0f, Time.deltaTime * 0.5f);
            agente.velocity = transform.forward * velocidadeAtualSimulada;
            
            if (velocidadeAtualSimulada < 0.1f)
            {
                emMarchaRe = false;
                temDestino = false;
            }
        }
        
        AtualizarRastroAgua();
        AtualizarInclinacaoNavio();
    }
    
    void DecidirEstrategiaNavegacao(Vector3 destino)
    {
        Vector3 direcaoDestino = destino - transform.position;
        float distanciaDestino = direcaoDestino.magnitude;
        
        // Se estiver muito longe, sempre vai de frente fazendo a curva
        if (distanciaDestino > distanciaMaximaRe)
        {
            emMarchaRe = false;
            return;
        }
        
        float anguloDestino = Vector3.Angle(transform.forward, direcaoDestino);
        
        // Só dá ré se estiver MUITO de costas e perto
        if (anguloDestino >= anguloParaMarchaRe)
        {
            emMarchaRe = true;
        }
        else
        {
            emMarchaRe = false;
        }
    }
    
    /// <summary>
    /// A mágica acontece aqui. O navio se move para onde o nariz aponta, não para onde o NavMesh quer.
    /// O NavMesh serve apenas para dizer "O alvo está para a direita", e nós viramos o leme.
    /// </summary>
    void ExecutarMarchaFrenteRealista()
    {
        // 1. ONDE O NAVMESH QUER IR?
        Vector3 direcaoDesejada = (agente.steeringTarget - transform.position).normalized;
        direcaoDesejada.y = 0;

        // 2. CÁLCULO DO LEME 
        Vector3 produtoVetorial = Vector3.Cross(transform.forward, direcaoDesejada);
        
        // Alvo do Leme: -1 (Esquerda) a 1 (Direita)
        // Reduzido multiplicador de 5.0 para 2.0 para menor sensibilidade
        float lemeAlvo = produtoVetorial.y * 2.0f; 
        
        // Se o alvo está atrás, força o leme todo
        if (Vector3.Dot(transform.forward, direcaoDesejada) < 0)
        {
            lemeAlvo = Mathf.Sign(produtoVetorial.y); 
            if (lemeAlvo == 0) lemeAlvo = 1f;
        }

        lemeAlvo = Mathf.Clamp(lemeAlvo, -1f, 1f);

        // 3. INÉRCIA DO LEME (O Segredo do Arco Suave)
        // SUPER LENTO: 0.08f significa que leva ~12 segundos para virar todo o leme (0 a 1)
        // Isso força o navio a andar em linha reta inicialmente e ir curvando BEM devagar
        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 0.08f);

        // 4. APLICA A ROTAÇÃO
        // Aceleramos o navio gradualmente
        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, Time.deltaTime * aceleracao);
        
        // REMOVIDA a aceleração de eficiência por velocidade
        // Agora a rotação é constante e lenta, sem "acelerar" conforme ganha velocidade
        // Só exige velocidade mínima para começar a girar
        float eficienciaLeme = velocidadeAtualSimulada > 1.0f ? 1.0f : 0.3f; 

        // Rotação baseada no leme atual (que muda MUITO devagar)
        float giroReal = lemeAtual * velocidadeGiroMax * Time.deltaTime * eficienciaLeme;
        transform.Rotate(0, giroReal, 0);

        // 5. MOVIMENTO (Sempre para frente)
        agente.velocity = transform.forward * velocidadeAtualSimulada;
    }

    void ExecutarMarchaRe()
    {
        // Lógica simplificada de ré (navios manobram mal de ré)
        Vector3 vetorParaDestino = agente.steeringTarget - transform.position;
        vetorParaDestino.y = 0;

        // Queremos alinhar a traseira (-forward) com o destino
        Quaternion rotacaoAlvo = Quaternion.LookRotation(-vetorParaDestino);
        
        // Ré gira bem devagar (reduzido de 15 para 6)
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, Time.deltaTime * 6f);

        // Acelera para trás
        float velocidadeAlvoRe = velocidadeOriginal * velocidadeRe;
        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeAlvoRe, Time.deltaTime);

        // Move para trás (navio anda de ré na direção oposta ao nariz)
        agente.velocity = -transform.forward * velocidadeAtualSimulada;
    }
    
    void AtualizarRastroAgua()
    {
        if (rastroAgua == null) return;
        rastroAgua.emitting = velocidadeAtualSimulada > 0.5f;
    }
    
    void AtualizarInclinacaoNavio()
    {
        if (modelo3D == null) return;
        
        // Inclinação baseada na força centrífuga (giro * velocidade)
        // Navios inclinam para FORA da curva geralmente, lanchas para DENTRO.
        // Assumindo lancha/navio rápido (inclina para dentro da curva):
        
        // Calcula a velocidade angular aproximada
        float giroFrame = Vector3.SignedAngle(transform.forward, agente.velocity.normalized, Vector3.up);
        
        // Suaviza a inclinação
        float anguloAlvo = -giroFrame * forcaInclinacao; // O negativo inverte para inclinar pro lado certo
        anguloAlvo = Mathf.Clamp(anguloAlvo, -15f, 15f);
        
        Quaternion novaRotacao = Quaternion.Euler(0, 0, anguloAlvo);
        modelo3D.localRotation = Quaternion.Slerp(modelo3D.localRotation, novaRotacao, Time.deltaTime * 2.0f);
    }
    
    void OnDrawGizmos()
    {
        if (!mostrarDebugVisual || !Application.isPlaying) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f);
        
        if (agente != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, agente.steeringTarget); // Para onde o NavMesh quer ir
        }
    }

    /// <summary>
    /// Desenha informações de debug na Scene view (quando selecionado)
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!mostrarDebugVisual) return;
        
        Gizmos.color = Color.yellow;
        
        // Desenha cone de marcha à ré aproximado
        Quaternion rotacaoLimiteDir = Quaternion.Euler(0, anguloParaMarchaRe, 0);
        Quaternion rotacaoLimiteEsq = Quaternion.Euler(0, -anguloParaMarchaRe, 0);
        
        Vector3 dirEsquerda = rotacaoLimiteEsq * -transform.forward * distanciaMaximaRe;
        Vector3 dirDireita = rotacaoLimiteDir * -transform.forward * distanciaMaximaRe;
        
        Gizmos.DrawLine(transform.position, transform.position + dirEsquerda);
        Gizmos.DrawLine(transform.position, transform.position + dirDireita);
        
        // Arco simples
        Vector3 anterior = transform.position + dirEsquerda;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            float ang = Mathf.Lerp(-anguloParaMarchaRe, anguloParaMarchaRe, t);
            Vector3 dir = Quaternion.Euler(0, ang, 0) * -transform.forward * distanciaMaximaRe;
            Vector3 atual = transform.position + dir;
            Gizmos.DrawLine(anterior, atual);
            anterior = atual;
        }
    }
    
    /// <summary>
    /// Método público para definir, usado pelo ControleUnidade e outros scripts.
    /// </summary>
    public void DefinirDestino(Vector3 novoDestino)
    {
        if (agente != null && agente.enabled)
        {
            agente.SetDestination(novoDestino);
            // Ao definir novo destino, reseta flags se necessário
            // O Update cuidará da lógica de decisão
        }
    }
    
    /// <summary>
    /// Retorna se o navio está operando em marcha à ré.
    /// Útil para animações ou lógica externa.
    /// </summary>
    public bool EstaEmMarchaRe()
    {
        return emMarchaRe;
    }
}