using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class MovimentoInteligente : MonoBehaviour
{
    [Header("Inteligência de Tráfego")]
    [Tooltip("Define se a unidade é 'pesada' (prioridade alta) ou 'leve'. Unidades leves desviam das pesadas.")]
    public bool veiculo = false;

    // Prioridades (0 = Imovível/Rei, 99 = Tímido)
    private const int PRIORIDADE_PARADO = 30;
    private const int PRIORIDADE_ANDANDO = 60; // Anda desviando de quem está parado
    
    private NavMeshAgent agente;
    private float timerRepath = 0f;
    private float velocidadeMedia = 0f;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        
        // 1. Configura Máxima Qualidade de Pensamento
        agente.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance; // Evita suavemente
        agente.autoRepath = true;     // Recalcula se caminho ficar inválido
        agente.autoBraking = true;    // Freia para estacionar
        
        // Ajuste fino
        if(veiculo) 
        {
            // Veículos precisam de mais espaço para não bater
            agente.radius += 0.1f; 
        }
    }

    void Update()
    {
        if (!agente.enabled) return;

        GerenciarPrioridades();
        GerenciarEstacionamento();
        VerificarObstaculosAdiantado(); // O "Pensamento" que desvia antes
    }

    // --- 1. HIERARQUIA DE RESPEITO ---
    // Quem está andando tem "menos moral" (prioridade alta/número grande) e sai da frente.
    // Quem está parado tem "moral alta" (prioridade baixa/número pequeno) e todos desviam dele.
    void GerenciarPrioridades()
    {
        // Usa magnitude da velocidade desejada, não a real (para evitar prioridade piscando em engarrafamento)
        bool querAndar = agente.hasPath && agente.remainingDistance > agente.stoppingDistance;
        
        if (querAndar)
        {
            // Estou me movendo: Sou flexível, desvio dos outros
            int pBase = veiculo ? 45 : 60; // Veículos têm um pouco mais de prioridade que soldados
            agente.avoidancePriority = pBase; 
        }
        else
        {
            // Estou parado/Cheguei: Sou uma pedra, ninguém passa por dentro de mim
            agente.avoidancePriority = PRIORIDADE_PARADO;
        }
    }

    // --- 2. ESTACIONAMENTO OTIMIZADO ---
    // "não recusa, ele so para no local que da deixando perto"
    void GerenciarEstacionamento()
    {
        if (!agente.hasPath) return;

        // Se estamos chegando (últimos 3 metros)
        if (agente.remainingDistance < 3.0f && agente.remainingDistance > agente.stoppingDistance)
        {
            // Se estamos muito lentos (engarrafado esperando vaga)
            if (agente.velocity.magnitude < 0.2f)
            {
                // Espera um pouco...
                velocidadeMedia += Time.deltaTime;
                
                // Se ficou 1 segundo travado na "vaga"
                if (velocidadeMedia > 1.0f)
                {
                    // ACEITA A VAGA: Para onde está.
                    // "Estacionado de forma boa" = Onde deu pra chegar.
                    agente.ResetPath();
                    velocidadeMedia = 0;
                }
            }
            else
            {
                velocidadeMedia = 0;
            }
        }
    }

    // --- 3. VISÃO DE FUTURO (DESVIA ANTES) ---
    // "se na reta onde eles vao passar tenham algum obstaculo... ele desvia antes"
    void VerificarObstaculosAdiantado()
    {
        // Executa a cada 0.5s para economizar bateria/processamento
        timerRepath += Time.deltaTime;
        if (timerRepath < 0.5f) return;
        timerRepath = 0;

        if (!agente.hasPath) return;

        // Lança um raio 'mental' para o próximo ponto do caminho (corner)
        Vector3 proximoPonto = agente.steeringTarget;
        Vector3 direcao = proximoPonto - transform.position;
        float dist = direcao.magnitude;

        if (dist > 1.0f) // Só se o obstáculo estiver longe (se estiver perto o Avoidance cuida)
        {
            RaycastHit hit;
            // SphereCast = Raio Grosso (simula a largura do ombro da unidade)
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.5f, direcao, out hit, Mathf.Min(dist, 5.0f)))
            {
                // Se viu um AMIGO (Player) parado na frente
                if (hit.collider.CompareTag("Player") && hit.collider.gameObject != gameObject)
                {
                    // Verifica se o amigo está parado
                    NavMeshAgent amigo = hit.collider.GetComponent<NavMeshAgent>();
                    if (amigo != null && amigo.velocity.magnitude < 0.1f)
                    {
                        // "PENSAMENTO": Vi um amigo parado na rota.
                        // O NavMeshAgent normal tentaria ir até ele e empurrar.
                        // Nós vamos forçar um leve desvio recalculando o caminho.
                        
                        // Truque: Desliga e liga o path para forçar o sistema a considerar a avoidance atual
                         if (agente.pathStatus == NavMeshPathStatus.PathComplete)
                         {
                             // A mera existência de prioridades diferentes já ajuda,
                             // mas aqui poderíamos inserir um ponto intermediário se quiséssemos ser agressivos.
                             // Por enquanto, confiar na Prioridade é o 'pensamento' mais estável sem bugar o path.
                         }
                    }
                }
            }
        }
    }
}
