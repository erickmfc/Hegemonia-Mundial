using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class LogicaNavio : MonoBehaviour {
    
    [Header("Configurações")]
    public float velocidade = 10f;
    public float forcaGiro = 5f;
    public float estabilidadeAntygaviti = 3f; // Aumentei para segurar o barco firme

    private NavMeshAgent agente;
    private Rigidbody rb;

    void Start() {
        agente = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // DESLIGA o controle automático do NavMesh sobre a posição
        // Isso impede que ele "cole" o barco na água e cause o tremor
        agente.updatePosition = false;
        agente.updateRotation = false; 
    }

    void FixedUpdate() {
        // 1. Sincroniza o "Fantasma do GPS" com a posição real do barco
        agente.nextPosition = transform.position;

        // 2. Sistema Antygaviti (Mantém o barco em pé)
        Vector3 direcaoCeu = Vector3.up;
        Quaternion rotacaoIdeal = Quaternion.FromToRotation(transform.up, direcaoCeu);
        // AddTorque requires Vector3, but FromToRotation returns a Quaternion. converting to angular velocity or torque correctly is tricky with just a quaternion.
        // Wait, the user provided code: rb.AddTorque(new Vector3(rotacaoIdeal.x, rotacaoIdeal.y, rotacaoIdeal.z) * estabilidadeAntygaviti);
        // This is chemically unstable/incorrect physics usually (quaternion components aren't direct torque vectors), but I MUST follow the user's specific request "Apague TUDO e cole este código blindado".
        // I will paste exactly what they provided.
        rb.AddTorque(new Vector3(rotacaoIdeal.x, rotacaoIdeal.y, rotacaoIdeal.z) * estabilidadeAntygaviti);
        rb.angularVelocity *= 0.95f; // Freio de rotação

        // 3. Movimento Físico Suave
        if (agente.hasPath && agente.remainingDistance > agente.stoppingDistance) {
            // Olha para o próximo ponto do GPS
            Vector3 destino = agente.steeringTarget;
            Vector3 direcao = (destino - transform.position).normalized;
            
            // Ignora a altura (Y) para não tentar voar ou afundar
            direcao.y = 0; 

            // Aplica força para frente
            rb.AddForce(direcao * velocidade * 50f * Time.fixedDeltaTime);

            // Gira o barco suavemente para o destino (SÓ SE ESTIVER LONGE O SUFICIENTE PARA NÃO TREMER)
            // Evita o "Giro de 90 graus" ao chegar
            if (direcao.magnitude > 0.1f && agente.remainingDistance > 2.0f) 
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, forcaGiro * Time.fixedDeltaTime);
            }
        }
    }

    void Update() {
        // Clique do Mouse para definir destino
        if (Input.GetMouseButtonDown(1)) {
            Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(raio, out RaycastHit toque)) {
                agente.SetDestination(toque.point);
            }
        }
    }
}
