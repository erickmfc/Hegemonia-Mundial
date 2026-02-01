using UnityEngine;
using UnityEngine.AI;

// Este script vai no GAMEOBJECT da unidade (Helicóptero ou Tanque)
// Ele controla o movimento físico de patrulha
public class ComportamentoPatrulha : MonoBehaviour
{
    public float raioPatrulha = 15f;
    public float velocidade = 10f;
    private Vector3 centroPatrulha;
    private float anguloAtual = 0f;
    private bool patrulhando = false;

    // Referências opcionais (tenta pegar sozinho)
    private NavMeshAgent navAgent;
    private Helicoptero heliController;

    void Start()
    {
        // Define o centro como onde ele estava quando começou a patrulhar
        centroPatrulha = transform.position;
        navAgent = GetComponent<NavMeshAgent>();
        heliController = GetComponent<Helicoptero>();
        patrulhando = true;
    }

    void Update()
    {
        if (!patrulhando) return;

        anguloAtual += Time.deltaTime * (velocidade / raioPatrulha); // Ajusta velocidade angular
        
        // Calcula nova posição em círculo
        float x = Mathf.Cos(anguloAtual) * raioPatrulha;
        float z = Mathf.Sin(anguloAtual) * raioPatrulha;
        Vector3 alvo = centroPatrulha + new Vector3(x, 0, z);

        // Se for Helicóptero (usa nosso sistema de voo manual)
        if (heliController != null)
        {
            // O heli novo tem um metodo Decolar que recebe o destino
             alvo.y = heliController.altitudeDeVoo; 
             heliController.Decolar(alvo);
        }
        else if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            // Se for Tanque/Soldado com NavMesh
            navAgent.SetDestination(alvo);
        }
    }

    public void PararPatrulha()
    {
        patrulhando = false;
        if(navAgent != null && navAgent.isActiveAndEnabled) navAgent.ResetPath();
        Destroy(this); // Remove o comportamento para parar limpo
    }
}
