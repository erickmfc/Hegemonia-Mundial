using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class ComportamentoPatrulhaCaminho : MonoBehaviour
{
    private List<Vector3> pontos = new List<Vector3>();
    private int indiceAtual = 0;
    private bool estaVoltando = false;
    private NavMeshAgent agente;
    private ControleUnidade controle;

    public void ConfigurarPatrulha(List<Vector3> novosPontos)
    {
        pontos = new List<Vector3>(novosPontos);
        indiceAtual = 0;
        estaVoltando = false;
        
        agente = GetComponent<NavMeshAgent>();
        controle = GetComponent<ControleUnidade>();

        // Remove seguir anterior se houver
        var seg = GetComponent<ComportamentoSeguir>();
        if (seg != null) Destroy(seg);

        if (pontos.Count > 0)
        {
            MoverParaProximoPonto();
        }
    }

    void Update()
    {
        if (pontos.Count == 0) return;

        bool chegou = false;

        // Se tem NavMesh (Terrestre)
        if (agente != null && agente.enabled)
        {
            if (!agente.pathPending && agente.remainingDistance < 2.0f) chegou = true;
        }
        else
        {
            // Se NÃO tem NavMesh (Aéreo ou Navio Realista/Inteligente)
            float dist = Vector3.Distance(transform.position, pontos[indiceAtual]);
            
            // Ignora altura se for aéreo no cálculo de "chegada"
            if (controle != null) 
            {
                 Vector3 diff = transform.position - pontos[indiceAtual];
                 diff.y = 0; 
                 // Navios e Aviões precisam de mais folga (curva larga)
                 if (diff.magnitude < 10.0f) chegou = true; 
            }
            else if (dist < 10.0f) chegou = true;
        }

        if (chegou)
        {
            AvancarIndice();
            MoverParaProximoPonto();
        }
    }

    void AvancarIndice()
    {
        if (!estaVoltando)
        {
            indiceAtual++;
            if (indiceAtual >= pontos.Count)
            {
                indiceAtual = pontos.Count - 2;
                estaVoltando = true;
                if (indiceAtual < 0) indiceAtual = 0;
            }
        }
        else
        {
            indiceAtual--;
            if (indiceAtual < 0)
            {
                indiceAtual = 1;
                estaVoltando = false;
                if (indiceAtual >= pontos.Count) indiceAtual = 0;
            }
        }
    }

    void MoverParaProximoPonto()
    {
        if (indiceAtual >= 0 && indiceAtual < pontos.Count)
        {
            if (controle != null) controle.MoverParaPonto(pontos[indiceAtual], false);
            else if (agente != null) agente.SetDestination(pontos[indiceAtual]);
        }
    }
}
