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
        agent = GetComponent<NavMeshAgent>();
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
                anim.SetFloat(velocidadeHash, agent.velocity.magnitude);
            }
        }
    }
}
