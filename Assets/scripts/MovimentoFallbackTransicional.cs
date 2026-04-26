using UnityEngine;
using UnityEngine.AI;

public static class MovimentoFallbackTransicional
{
    public static bool TrySetNavDestination(GameObject unidade, Vector3 destino, float warpRaio = 25f)
    {
        if (unidade == null)
        {
            return false;
        }

        NavMeshAgent nav = unidade.GetComponent<NavMeshAgent>();
        if (nav == null || !nav.enabled)
        {
            return false;
        }

        if (!nav.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(unidade.transform.position, out NavMeshHit hit, warpRaio, NavMesh.AllAreas))
            {
                nav.Warp(hit.position);
            }
        }

        if (!nav.isOnNavMesh)
        {
            return false;
        }

        nav.isStopped = false;
        nav.SetDestination(destino);
        return true;
    }
}
