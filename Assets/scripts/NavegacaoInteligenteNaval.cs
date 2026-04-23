using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("")]
public class NavegacaoInteligenteNaval : MonoBehaviour
{
    [Header("Compatibilidade Legada")]
    public float velocidadeMaxima = 12f;

    private bool avisoEmitido = false;

    void Awake()
    {
        enabled = false;
        EmitirAviso();
    }

    void OnEnable()
    {
        enabled = false;
        EmitirAviso();
    }

    public void DefinirDestino(Vector3 novoDestino)
    {
        EmitirAviso();

        ControleNavioRealista navioRealista = GetComponent<ControleNavioRealista>();
        if (navioRealista != null)
        {
            navioRealista.DefinirDestino(novoDestino);
            return;
        }

        ControleSubmarino submarino = GetComponent<ControleSubmarino>();
        if (submarino != null)
        {
            submarino.DefinirDestino(novoDestino);
            return;
        }

        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle != null)
        {
            controle.EmitirOrdemMover(novoDestino);
            return;
        }

        NavMeshAgent agente = GetComponent<NavMeshAgent>();
        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.isStopped = false;
            agente.SetDestination(novoDestino); // CONTROL_PATH_TRANSITIONAL_FALLBACK
        }
    }

    private void EmitirAviso()
    {
        if (avisoEmitido)
        {
            return;
        }

        avisoEmitido = true;
        Debug.LogWarning("[NavegacaoInteligenteNaval] Esta trilha foi desativada. Migre o objeto para ControleNavioRealista.", this);
    }
}
