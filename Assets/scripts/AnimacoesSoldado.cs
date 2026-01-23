using UnityEngine;
using UnityEngine.AI; // Necessário para conversar com o NavMesh

public class AnimacoesSoldado : MonoBehaviour
{
    private Animator animador;
    private NavMeshAgent agenteMovimento;

    // Variável para testarmos manualmente no Inspector
    [Header("Teste Manual")]
    public bool estaAtirando = false;

    void Start()
    {
        animador = GetComponent<Animator>();
        agenteMovimento = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 1. Controla a animação de Andar
        // Se a velocidade do agente for maior que 0.1, ele está andando
        if (agenteMovimento.velocity.magnitude > 0.1f)
        {
            // Substitua "IsWalking" pelo nome exato do seu parâmetro de andar, se tiver
            // Se não tiver, me avise que criamos. Por enquanto, vou focar no tiro.
            // Para sua referência: se você usar o ControleUnidade, ele usa "Velocidade" (float)
        }

        // 2. Controla a animação de Atirar
        // Envia o valor da nossa variável para o Animator
        animador.SetBool("Atacando", estaAtirando);
    }
    
    // Função pública para ser chamada por outros scripts (Cérebro da IA)
    public void DefinirAtaque(bool estado)
    {
        estaAtirando = estado;
    }
}
