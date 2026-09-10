using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marcador opcional para qualquer prédio hospitalar já existente ou criado
/// pelo usuário. O sistema de saúde só soma capacidade; não altera o prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class HospitalSaude : MonoBehaviour
{
    public int teamId = 1;
    public int capacidadeAtendimento = 150000;
    public float manutencaoPorDia = 180f;

    private static readonly HashSet<HospitalSaude> Ativos = new HashSet<HospitalSaude>();

    private void Awake()
    {
        InferirTeamId();
    }

    private void OnEnable()
    {
        InferirTeamId();
        Ativos.Add(this);
    }

    private void OnDisable()
    {
        Ativos.Remove(this);
    }

    public static int ObterCapacidadeTeam(int id)
    {
        int total = 0;
        foreach (HospitalSaude hospital in Ativos)
        {
            if (hospital != null && hospital.teamId == id) total += Mathf.Max(0, hospital.capacidadeAtendimento);
        }
        return total;
    }

    public static float ObterManutencaoTeam(int id)
    {
        float total = 0f;
        foreach (HospitalSaude hospital in Ativos)
        {
            if (hospital != null && hospital.teamId == id) total += Mathf.Max(0f, hospital.manutencaoPorDia);
        }
        return total;
    }

    private void InferirTeamId()
    {
        IdentidadeUnidade identity = GetComponentInParent<IdentidadeUnidade>();
        if (identity != null && identity.teamID > 0) teamId = identity.teamID;
        IdentidadeIA identityIA = GetComponentInParent<IdentidadeIA>();
        if (identityIA != null && identityIA.teamID > 0) teamId = identityIA.teamID;
    }
}
