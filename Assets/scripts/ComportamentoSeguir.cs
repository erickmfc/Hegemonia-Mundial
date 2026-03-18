using UnityEngine;
using UnityEngine.AI;

public class ComportamentoSeguir : MonoBehaviour
{
    private Transform alvo;
    private NavMeshAgent agente;
    private ControleUnidade controle;
    private float tempoAtualizacao = 0.5f;
    private float proximaAtualizacao = 0f;

    public void ConfigurarSeguir(Transform novoAlvo)
    {
        alvo = novoAlvo;
        agente = GetComponent<NavMeshAgent>();
        controle = GetComponent<ControleUnidade>();
        
        // Remove patrulhas anteriores se houver
        var patrulha = GetComponent<ComportamentoPatrulhaCaminho>();
        if (patrulha != null) Destroy(patrulha);
    }

    void Update()
    {
        if (alvo == null) return;

        if (Time.time > proximaAtualizacao)
        {
            if (controle != null) controle.MoverParaPonto(alvo.position, false);
            else if (agente != null && agente.isActiveAndEnabled) agente.SetDestination(alvo.position);
            
            proximaAtualizacao = Time.time + tempoAtualizacao;
        }

        // Se o alvo for destruído ou desativado, para de seguir
        if (!alvo.gameObject.activeInHierarchy)
        {
            alvo = null;
        }
    }
}
