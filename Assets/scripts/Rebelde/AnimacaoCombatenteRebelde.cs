using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Aciona a pose de combate do combatente rebelde quando o SistemaDeTiro mantém
/// um alvo válido. A locomoção continua sendo dirigida pelo NavMeshAgent.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimacaoCombatenteRebelde : MonoBehaviour
{
    [SerializeField] private SistemaDeTiro sistemaDeTiro;
    [SerializeField] private string parametroDisparo = "atirando";
    [SerializeField] private string parametroVelocidade = "velocidade";
    [SerializeField, Min(0.1f)] private float velocidadeMaximaAnimacao = 3.8f;

    private Animator animator;
    private NavMeshAgent agente;
    private int parametroDisparoHash;
    private int parametroVelocidadeHash;
    private bool possuiParametroDisparo;
    private bool possuiParametroVelocidade;
    private bool estadoDisparoAplicado;
    private float velocidadeAplicada = float.NaN;
    private Vector3 posicaoAnterior;

    private void Awake()
    {
        animator = BuscarAnimatorComControlador();
        if (sistemaDeTiro == null)
            sistemaDeTiro = GetComponent<SistemaDeTiro>() ?? GetComponentInParent<SistemaDeTiro>()
                ?? GetComponentInChildren<SistemaDeTiro>(true);
        agente = GetComponent<NavMeshAgent>() ?? GetComponentInParent<NavMeshAgent>()
            ?? GetComponentInChildren<NavMeshAgent>(true);
        posicaoAnterior = transform.position;
        InicializarParametrosAnimator();
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AtualizarLocomocao();

        bool disparando = sistemaDeTiro != null && !sistemaDeTiro.modoPassivo
            && (sistemaDeTiro.alvoAtual != null || sistemaDeTiro.alvoPrioritario != null);
        if (possuiParametroDisparo && disparando != estadoDisparoAplicado)
        {
            estadoDisparoAplicado = disparando;
            animator.SetBool(parametroDisparoHash, disparando);
        }
    }

    private Animator BuscarAnimatorComControlador()
    {
        Animator raiz = GetComponent<Animator>();
        if (raiz != null && raiz.runtimeAnimatorController != null) return raiz;

        Animator[] animadores = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animadores.Length; i++)
            if (animadores[i] != null && animadores[i].runtimeAnimatorController != null)
                return animadores[i];

        return raiz != null ? raiz : (animadores.Length > 0 ? animadores[0] : null);
    }

    private void InicializarParametrosAnimator()
    {
        possuiParametroDisparo = false;
        possuiParametroVelocidade = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimatorControllerParameter[] parametros = animator.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];
            if (parametro.type == AnimatorControllerParameterType.Bool
                && string.Equals(parametro.name, parametroDisparo, StringComparison.OrdinalIgnoreCase))
            {
                parametroDisparoHash = Animator.StringToHash(parametro.name);
                possuiParametroDisparo = true;
            }
            else if (parametro.type == AnimatorControllerParameterType.Float
                && string.Equals(parametro.name, parametroVelocidade, StringComparison.OrdinalIgnoreCase))
            {
                parametroVelocidadeHash = Animator.StringToHash(parametro.name);
                possuiParametroVelocidade = true;
            }
        }
    }

    private void AtualizarLocomocao()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float velocidade = (transform.position - posicaoAnterior).magnitude / deltaTime;
        if (agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh)
            velocidade = Mathf.Max(velocidade, agente.velocity.magnitude);
        posicaoAnterior = transform.position;

        if (!possuiParametroVelocidade) return;
        float velocidadeReferencia = agente != null && agente.speed > 0.1f
            ? agente.speed : Mathf.Max(0.1f, velocidadeMaximaAnimacao);
        float normalizada = Mathf.Clamp01(velocidade / velocidadeReferencia);
        if (float.IsNaN(velocidadeAplicada) || Mathf.Abs(normalizada - velocidadeAplicada) >= 0.025f)
        {
            animator.SetFloat(parametroVelocidadeHash, normalizada);
            velocidadeAplicada = normalizada;
        }
    }
}
