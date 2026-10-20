using Hegemonia.AI.IA03;
using UnityEngine;

/// <summary>
/// Marca uma vaga específica do convés que pode receber helicópteros.
/// O serviço só é habilitado quando o Creaty está explicitamente conectado
/// e pertence à hierarquia do mesmo navio.
/// </summary>
[DisallowMultipleComponent]
public sealed class PontoPousoHelicopteroNaval : MonoBehaviour
{
    [Tooltip("Creaty estratégico conectado a esta posição de pouso no convés.")]
    [SerializeField] private CreatyEstrategico creatyConectado;

    public CreatyEstrategico CreatyConectado => creatyConectado;

    public bool EstaConectadoAoNavio(GerenciadorPortaAvioes navio)
    {
        return navio != null && EstaConectadoAoNavio(navio.transform, ObterTeam(navio.gameObject));
    }

    public bool EstaConectadoAoNavio(ControleNavioRealista navio)
    {
        return navio != null && EstaConectadoAoNavio(navio.transform, ObterTeam(navio.gameObject));
    }

    private bool EstaConectadoAoNavio(Transform raizNavio, int teamId)
    {
        if (raizNavio == null || teamId <= 0 || !isActiveAndEnabled || creatyConectado == null || !creatyConectado.Ativo)
            return false;

        bool pontoNoNavio = transform == raizNavio || transform.IsChildOf(raizNavio);
        bool creatyNoNavio = creatyConectado.transform == raizNavio || creatyConectado.transform.IsChildOf(raizNavio);
        return pontoNoNavio && creatyNoNavio && teamId == creatyConectado.PaisProprietarioTeamId;
    }

    private static int ObterTeam(GameObject navio)
    {
        IdentidadeUnidade identidade = navio.GetComponent<IdentidadeUnidade>()
            ?? navio.GetComponentInParent<IdentidadeUnidade>()
            ?? navio.GetComponentInChildren<IdentidadeUnidade>(true);
        return identidade != null ? identidade.teamID : -1;
    }
}
