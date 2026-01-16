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
    private HelicopterController heliController;

    void Start()
    {
        // Define o centro como onde ele estava quando começou a patrulhar
        centroPatrulha = transform.position;
        navAgent = GetComponent<NavMeshAgent>();
        heliController = GetComponent<HelicopterController>();
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
            // O heli tem um método 'DefinirDestino' que fizemos privado,
            // mas podemos mandar mensagem ou mudar propriedade se ele expor.
            // Para ser rápido e não mudar o heli de novo agora, vamos usar SendMessage ou simular clique.
            // O ideal é o HelicopterController ter um método publico "VoarPara(Vector3)".
            // Como mudamos o HelicopterController antes, vamos ver se conseguimos mover ele.
            
            // Hack para mover o helicóptero diretamente se ele permitir, 
            // ou vamos assumir que este script controla a translação se o heli estiver em modo passivo.
            // Porém, o heli tem seu próprio Update de voo.
            
            // SOLUÇÃO ELEGANTE: O Comando deve chamar um método público no Heli.
            // Mas como estamos criando um componete genérico, vamos tentar usar o NavMeshAgent se tiver,
            // ou mover transform se for o heli novo.
            
             // Se o HeliController estiver controlando a posição, precisamos dizer a ele o destino.
             // Vamos usar SendMessage para "DefinirDestino" que criamos no passo anterior (era private, mas Invoke funciona se precisar, ou mudamos para public).
             // Vamos tentar mover direto via Transform se não tiver conflitanto.
             
             // Melhor: Vamos ajustar o alvo Y para a altura do heli
             alvo.y = transform.position.y; 
             
             // Gira para olhar
             transform.LookAt(alvo);
             // Move para frente
             transform.position = Vector3.MoveTowards(transform.position, alvo, velocidade * Time.deltaTime);
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
