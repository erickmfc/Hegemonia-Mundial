using Hegemonia.AI.BrainMaster;
using UnityEngine;

/// <summary>
/// Shared timing and identification rules for the Ares anti-air purchase used by IA01 and IA02.
/// </summary>
public static class IA_AntiAirPurchasePolicy
{
    public const string AresItemId = "defesa.ares_ar";
    public const string AresAmmoId = "municao_ares_ar";
    public const long AresAmmoUnitPrice = 220000L;
    public const int MaximumPerTeam = 1;

    public static int GetUnlockDay(int teamId)
    {
        // Keep the AI nations staggered while ensuring the purchase happens on days 4–7.
        int safeTeamId = Mathf.Max(1, teamId);
        return 4 + (safeTeamId % 4);
    }

    public static bool IsAvailable(int teamId, int currentDay)
    {
        return Mathf.Max(1, currentDay) >= GetUnlockDay(teamId);
    }

    public static int GetCurrentDay()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            return Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
        }

        return Mathf.Max(1, Mathf.FloorToInt(Time.time / 30f) + 1);
    }

    public static bool IsAres(DadosConstrucao item)
    {
        if (item == null) return false;
        string id = IA_Text.Normalize(item.GetStableId());
        string name = IA_Text.Normalize(item.GetDisplayName() + " " + item.aliases);
        return id == IA_Text.Normalize(AresItemId)
            || name.Contains("ares ar")
            || name.Contains("ares antiaereo")
            || name.Contains("ares anti aereo");
    }
}
