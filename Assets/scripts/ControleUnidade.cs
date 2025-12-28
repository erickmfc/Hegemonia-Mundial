using UnityEngine;
using UnityEngine.AI;

public class ControleUnidade : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator; // Referência para as animações
    
    public GameObject anelSelecao; 
    public bool selecionado = false;

    // O Awake roda NA HORA que o objeto nasce, antes de receber ordens.
    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); // Pega o Animator do próprio objeto
    }

    void Start()
    {
        // Garante que comece deselecionado visualmente
        if(anelSelecao != null) anelSelecao.SetActive(selecionado);
    }

    void Update()
    {
        // 1. Controle de Animação
        if (agente != null && animator != null)
        {
            // Pega a velocidade real (0 a 3.5, etc)
            float velocidade = agente.velocity.magnitude;
            // Manda para o Animator decidir se corre ou para
            animator.SetFloat("Velocidade", velocidade);
        }

        // 2. Controle de Movimento pelo Mouse (se selecionado)
        // DESLIGADO AGORA: O GerenteSelecao controla o movimento em grupo!
        /*
        if (Input.GetMouseButtonDown(1) && selecionado)
        {
            MoverParaMouse();
        }
        */
    }

    void MoverParaMouse()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit pontoDeColisao;

        if (Physics.Raycast(raio, out pontoDeColisao))
        {
            if (agente != null)
                agente.SetDestination(pontoDeColisao.point);
        }
    }

    // COMANDO AUTOMÁTICO (Usado pela fábrica)
    public void MoverParaPonto(Vector3 destino)
    {
        // Segurança extra. Se o agente ainda não foi pego, pega agora.
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        if (agente != null)
        {
            // CORREÇÃO CRÍTICA: O NavMeshAgent precisa estar ativo e NO CHÃO para receber ordens.
            // Se ele acabou de nascer no ar ou foi desativado, essa verificação evita o erro.
            if (agente.isOnNavMesh && agente.isActiveAndEnabled)
            {
                agente.SetDestination(destino);
            }
            else
            {
                // Opcional: Tenta "Warp" para a posição atual para forçar conexão se estiver muito perto
                // mas geralmente é melhor apenas esperar o próximo frame.
                // Debug.LogWarning($"Unidade {name} ignorou movimento pois não está no NavMesh.");
            }
        }
    }

    public void DefinirSelecao(bool estado)
    {
        selecionado = estado;
        if (anelSelecao != null)
        {
            anelSelecao.SetActive(estado);
        }
    }
}
