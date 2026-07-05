using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class AnimadorUnidade : MonoBehaviour
{
    [SerializeField] private string parametroVelocidade = "velocidade";

    private Animator anim;
    private NavMeshAgent agent;
    private int velocidadeHash;
    private bool temParametroValido;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>(true);

        InicializarParametro();
    }

    private void InicializarParametro()
    {
        temParametroValido = false;
        if (anim == null || anim.runtimeAnimatorController == null)
            return;

        // Procura pelo nome especificado ou variações de maiúsculas/minúsculas
        string nomeEncontrado = null;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == parametroVelocidade)
            {
                nomeEncontrado = param.name;
                break;
            }
        }

        // Fallback: se não encontrou com a capitalização padrão, tenta a alternativa
        if (nomeEncontrado == null)
        {
            string alternativa = (parametroVelocidade == "velocidade") ? "Velocidade" : "velocidade";
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == alternativa)
                {
                    nomeEncontrado = param.name;
                    break;
                }
            }
        }

        if (nomeEncontrado != null)
        {
            velocidadeHash = Animator.StringToHash(nomeEncontrado);
            temParametroValido = true;
        }
    }

    void Update()
    {
        // Só tenta atualizar se o Animator tiver um Controller ativo e inicializado
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            if (!temParametroValido)
            {
                InicializarParametro();
            }

            if (temParametroValido && agent != null)
            {
                float velocidade = agent.velocity.magnitude;
                if (velocidade < 0.05f)
                {
                    // Em algumas cenas o agente está parado mas o deck/parent se move;
                    // use a mudança de posição local para evitar "deslizar parado".
                    Vector3 delta = transform.position - _ultimaPosicao;
                    velocidade = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                }

                anim.SetFloat(velocidadeHash, velocidade);
                _ultimaPosicao = transform.position;
            }
        }
    }

    private Vector3 _ultimaPosicao;

    void OnEnable()
    {
        _ultimaPosicao = transform.position;
    }
}
