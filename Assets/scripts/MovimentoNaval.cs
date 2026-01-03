using UnityEngine;
using UnityEngine.AI;

public class MovimentoNaval : MonoBehaviour
{
    [Header("Configurações Visuais")]
    public TrailRenderer Rastro_Agua; // Arraste o objeto 'Rastro_Agua' aqui
    public Transform modelo3D;       // O objeto filho (o desenho do barco)
    public float forcaInclinacao = 3.0f; // Quanto ele inclina na curva

    private NavMeshAgent agente;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        
        // Se você esqueceu de arrastar, ele tenta achar sozinho
        if (Rastro_Agua == null) Rastro_Agua = GetComponentInChildren<TrailRenderer>();
    }

    void Update()
    {
        if (agente == null) return;

        // 1. CONTROLE DO RASTRO (SÓ LIGA SE ESTIVER ANDANDO RÁPIDO)
        // Se a velocidade for maior que 1, liga o rastro.
        if (Rastro_Agua != null)
        {
            if (agente.velocity.magnitude > 1.0f)
            {
                Rastro_Agua.emitting = true;
            }
            else
            {
                Rastro_Agua.emitting = false;
            }
        }

        // 2. CONTROLE DE INCLINAÇÃO (BANKING)
        // Calcula a curva baseada na rotação
        if (modelo3D != null)
        {
            // Pega o quanto o navio está girando (Cross Product entre frente e vetor de movimento)
            Vector3 direcao = agente.steeringTarget - transform.position;
            float curva = Vector3.SignedAngle(transform.forward, direcao, Vector3.up);
            
            // Suaviza a inclinação (Clamp para não virar o barco de cabeça pra baixo)
            float anguloAlvo = Mathf.Clamp(curva * -1, -15f, 15f); // Máximo 15 graus
            
            // Aplica a rotação suave no eixo Z do modelo filho
            Quaternion novaRotacao = Quaternion.Euler(0, 0, anguloAlvo);
            modelo3D.localRotation = Quaternion.Slerp(modelo3D.localRotation, novaRotacao, Time.deltaTime * 2f);
        }
    }
}
