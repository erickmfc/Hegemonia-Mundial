using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavegacaoInteligenteNaval : MonoBehaviour
{
    [Header("Configurações de Navegação")]
    [Tooltip("Ângulo em graus para considerar que o destino está 'atrás' (90 = meio do navio, 180 = popa total)")]
    [Range(90f, 180f)]
    public float anguloParaMarchaRe = 130f; 
    
    [Tooltip("Distância máxima para usar marcha à ré (em metros)")]
    [Range(5f, 300f)]
    public float distanciaMaximaRe = 300f; 
    
    [Tooltip("Velocidade de marcha à ré (% da velocidade normal)")]
    [Range(0.3f, 1.0f)]
    public float velocidadeRe = 0.5f; 

    [Header("Física do Navio (Realismo)")]
    [Tooltip("Velocidade máxima de rotação do leme (graus por segundo).")]
    public float velocidadeGiroMax = 10f; 
    
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
    private float velocidadeAtualSimulada = 0f; 
    private float lemeAtual = 0f; 
    
    // NOVO: Estado de ancorgem para travar movimento
    private bool modoAncorado = false;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        velocidadeOriginal = agente.speed;

        // Desliga tudo que é automático do NavMeshAgent
        agente.updateRotation = false; 
        agente.updatePosition = true; // Mantém true para ele colar no navmesh
        agente.acceleration = 9999; 
        
        if (rastroAgua == null) 
            rastroAgua = GetComponentInChildren<TrailRenderer>();
    }
    
    void Update()
    {
        if (agente == null || !agente.enabled) return;
        
        // TECLA "I" = TOGGLE ANCORAR / DESANCORAR
        // Só funciona se o navio estiver selecionado
        if (Input.GetKeyDown(KeyCode.I))
        {
            var controleUnidade = GetComponent<ControleUnidade>();
            if (controleUnidade != null && controleUnidade.selecionado)
            {
                modoAncorado = !modoAncorado; // Inverte o estado

                if (modoAncorado)
                {
                    // PARADA DE EMERGÊNCIA / ANCORAGEM
                    agente.ResetPath(); // Cancela o caminho atual
                    agente.velocity = Vector3.zero;
                    velocidadeAtualSimulada = 0f;
                    lemeAtual = 0f;
                    emMarchaRe = false;
                    temDestino = false;
                    Debug.Log("[Navio] ANCORADO! Motores desligados. Nenhum clique funcionará até apertar 'I' novamente.");
                }
                else
                {
                    Debug.Log("[Navio] MOTORES LIBERADOS. Pronto para receber ordens.");
                }
                return;
            }
        }

        // Se estiver ancorado, forca parada total e ignora resto do update de movimento
        if (modoAncorado)
        {
            velocidadeAtualSimulada = 0f;
            agente.velocity = Vector3.zero;
            return;
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
        
        if (distanciaDestino > distanciaMaximaRe)
        {
            emMarchaRe = false;
            return;
        }
        
        float anguloDestino = Vector3.Angle(transform.forward, direcaoDestino);
        
        if (anguloDestino >= anguloParaMarchaRe)
        {
            emMarchaRe = true;
        }
        else
        {
            emMarchaRe = false;
        }
    }
    
    void ExecutarMarchaFrenteRealista()
    {
        Vector3 direcaoDesejada = (agente.steeringTarget - transform.position).normalized;
        direcaoDesejada.y = 0;

        Vector3 produtoVetorial = Vector3.Cross(transform.forward, direcaoDesejada);
        
        float lemeAlvo = produtoVetorial.y * 2.0f; 
        
        if (Vector3.Dot(transform.forward, direcaoDesejada) < 0)
        {
            lemeAlvo = Mathf.Sign(produtoVetorial.y); 
            if (lemeAlvo == 0) lemeAlvo = 1f;
        }

        lemeAlvo = Mathf.Clamp(lemeAlvo, -1f, 1f);

        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 0.08f);

        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, Time.deltaTime * aceleracao);
        
        float eficienciaLeme = velocidadeAtualSimulada > 1.0f ? 1.0f : 0.3f; 

        float giroReal = lemeAtual * velocidadeGiroMax * Time.deltaTime * eficienciaLeme;
        transform.Rotate(0, giroReal, 0);

        agente.velocity = transform.forward * velocidadeAtualSimulada;
    }

    void ExecutarMarchaRe()
    {
        Vector3 vetorParaDestino = agente.steeringTarget - transform.position;
        vetorParaDestino.y = 0;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(-vetorParaDestino);
        
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, Time.deltaTime * 6f);

        float velocidadeAlvoRe = velocidadeOriginal * velocidadeRe;
        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeAlvoRe, Time.deltaTime);

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
        
        float giroFrame = Vector3.SignedAngle(transform.forward, agente.velocity.normalized, Vector3.up);
        
        float anguloAlvo = -giroFrame * forcaInclinacao; 
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
            Gizmos.DrawLine(transform.position, agente.steeringTarget); 
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!mostrarDebugVisual) return;
        
        Gizmos.color = Color.yellow;
        
        Quaternion rotacaoLimiteDir = Quaternion.Euler(0, anguloParaMarchaRe, 0);
        Quaternion rotacaoLimiteEsq = Quaternion.Euler(0, -anguloParaMarchaRe, 0);
        
        Vector3 dirEsquerda = rotacaoLimiteEsq * -transform.forward * distanciaMaximaRe;
        Vector3 dirDireita = rotacaoLimiteDir * -transform.forward * distanciaMaximaRe;
        
        Gizmos.DrawLine(transform.position, transform.position + dirEsquerda);
        Gizmos.DrawLine(transform.position, transform.position + dirDireita);
        
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
        // BLOQUEIO: Se estiver ancorado, ignora qualquer comando externo de movimento
        if (modoAncorado)
        {
             // Opcional: Efeito sonoro de "erro" ou feedback visual
            return;
        }

        if (agente != null && agente.enabled)
        {
            agente.SetDestination(novoDestino);
        }
    }
    
    public bool EstaEmMarchaRe()
    {
        return emMarchaRe;
    }
}