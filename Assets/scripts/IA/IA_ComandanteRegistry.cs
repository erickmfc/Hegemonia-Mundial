using System.Collections.Generic;
using UnityEngine;

public static class IA_ComandanteRegistry
{
    private static readonly Dictionary<int, IA_Comandante> ComandantePorTime = new Dictionary<int, IA_Comandante>();

    public static IEnumerable<IA_Comandante> AllCommanders
    {
        get { return ComandantePorTime.Values; }
    }

    public static void Register(IA_Comandante comandante)
    {
        if (comandante == null)
        {
            return;
        }

        int teamId = ResolveTeamId(comandante);
        if (teamId <= 1)
        {
            return;
        }

        ComandantePorTime[teamId] = comandante;
    }

    public static void Unregister(IA_Comandante comandante)
    {
        if (comandante == null)
        {
            return;
        }

        int teamId = ResolveTeamId(comandante);
        IA_Comandante current;
        if (teamId > 1 && ComandantePorTime.TryGetValue(teamId, out current) && current == comandante)
        {
            ComandantePorTime.Remove(teamId);
        }
    }

    public static IA_Comandante GetCommanderByTeam(int teamId)
    {
        if (teamId <= 1)
        {
            return null;
        }

        IA_Comandante comandante;
        if (ComandantePorTime.TryGetValue(teamId, out comandante) && comandante != null)
        {
            return comandante;
        }

        IA_Comandante[] encontrados = Object.FindObjectsByType<IA_Comandante>(FindObjectsSortMode.None);
        for (int i = 0; i < encontrados.Length; i++)
        {
            IA_Comandante candidato = encontrados[i];
            if (candidato == null)
            {
                continue;
            }

            Register(candidato);
        }

        ComandantePorTime.TryGetValue(teamId, out comandante);
        return comandante;
    }

    public static IA_General_Pro GetGeneralByTeam(int teamId)
    {
        IA_Comandante comandante = GetCommanderByTeam(teamId);
        return comandante != null ? comandante.cerebroGeneral : null;
    }

    private static int ResolveTeamId(IA_Comandante comandante)
    {
        if (comandante == null)
        {
            return -1;
        }

        if (comandante.identidade != null)
        {
            return comandante.identidade.teamID;
        }

        IdentidadeIA identidade = comandante.GetComponent<IdentidadeIA>();
        if (identidade == null)
        {
            identidade = comandante.GetComponentInParent<IdentidadeIA>();
        }

        if (identidade != null)
        {
            return identidade.teamID;
        }

        return comandante.TeamID;
    }
}
