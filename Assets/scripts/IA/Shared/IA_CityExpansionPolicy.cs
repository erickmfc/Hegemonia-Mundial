using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

/// <summary>
/// Regra compartilhada de preparação urbana. IA01, IA02 e o construtor do
/// jogador consultam esta mesma política; os executores continuam sendo os
/// responsáveis por materializar a construção.
/// </summary>
public static class IA_CityExpansionPolicy
{
    public const int PreparationDays = 10;
    public const float FuelReserveFraction = 0.10f;
    public const float FoodReserveFraction = 0.15f;

    public sealed class Assessment
    {
        public bool IsCity;
        public bool Ready;
        public bool PreparationComplete;
        public bool FoodReady;
        public bool FuelReady;
        public bool HealthReady;
        public bool ProductionReady;
        public bool BoughtResources;
        public int CurrentDay;
        public int PreparationStartDay;
        public int DaysRemaining;
        public int ProjectedPopulation;
        public float PredictedFoodNeed;
        public float PredictedFuelNeed;
        public float FoodReserveTarget;
        public float FuelReserveTarget;
        public float FoodAvailable;
        public float FuelAvailable;
        public float HealthCapacity;
        public string Decision = string.Empty;

        public string ShortReason
        {
            get
            {
                if (!PreparationComplete) return "preparacao obrigatoria: " + DaysRemaining + " dia(s) restante(s)";
                if (!FoodReady) return "comida abaixo de 15% da necessidade prevista";
                if (!FuelReady) return "combustivel abaixo de 10% da necessidade prevista";
                if (!ProductionReady) return "sem estoque, producao ou compra de comida disponivel";
                if (!HealthReady) return "capacidade hospitalar insuficiente para a expansao";
                return "condicoes atendidas";
            }
        }
    }

    private sealed class PreparationState
    {
        public int StartDay;
        public int LastPurchaseDay = -1;
    }

    private static readonly Dictionary<int, PreparationState> States = new Dictionary<int, PreparationState>();

    public static bool IsCityConstruction(DadosConstrucao data)
    {
        if (data == null) return false;
        if (string.Equals(data.GetStableId(), "urbana.cidade_egito", StringComparison.OrdinalIgnoreCase)) return true;
        return IsCityPrefab(data.PrefabDaUnidade);
    }

    public static bool IsCityPrefab(GameObject prefab)
    {
        if (prefab == null) return false;
        try
        {
            if (prefab.GetComponentInChildren<CidadeComplexoUrbano>(true) != null) return true;
        }
        catch (MissingReferenceException)
        {
            return false;
        }

        string text = IA_Text.Normalize(prefab.name);
        return text.Contains("cidade") || text.Contains("city") || text.Contains("egito") || text.Contains("egyto");
    }

    public static int EstimatePopulation(GameObject prefab)
    {
        if (prefab != null)
        {
            CidadeComplexoUrbano city = prefab.GetComponentInChildren<CidadeComplexoUrbano>(true);
            if (city != null) return Mathf.Max(1, city.capacidadeHabitacional);
        }

        return 400000;
    }

    public static string GetPreparationStateToken(int teamId)
    {
        PreparationState state;
        if (!States.TryGetValue(Mathf.Max(1, teamId), out state)) return "cityprep:none";
        return "cityprep:" + state.StartDay + ":" + GetCurrentDay();
    }

    public static bool TryPrepareForCity(
        int teamId,
        DadosPaisGoverno country,
        DadosEconomiaPais economy,
        GameObject prefab,
        bool allowMarketPurchases,
        out Assessment assessment)
    {
        assessment = new Assessment { IsCity = IsCityPrefab(prefab) };
        if (!assessment.IsCity) return true;

        int safeTeamId = Mathf.Max(1, teamId);
        int currentDay = GetCurrentDay();
        PreparationState state = GetOrCreateState(safeTeamId, currentDay);
        int projectedPopulation = EstimatePopulation(prefab);
        int militaryPopulation = country != null ? Mathf.Max(0, country.populacaoMilitarAtiva) : 0;

        assessment.CurrentDay = currentDay;
        assessment.PreparationStartDay = state.StartDay;
        assessment.DaysRemaining = Mathf.Max(0, state.StartDay + PreparationDays - currentDay);
        assessment.PreparationComplete = currentDay >= state.StartDay + PreparationDays;
        assessment.ProjectedPopulation = projectedPopulation;
        assessment.PredictedFoodNeed = Mathf.Max(1f, projectedPopulation * 0.01f + militaryPopulation * 0.02f);
        assessment.PredictedFuelNeed = Mathf.Max(100f, projectedPopulation * 0.0025f + (economy != null ? economy.combustivelConsumido * 10f : 0f));
        assessment.FoodReserveTarget = Mathf.Max(1f, assessment.PredictedFoodNeed * FoodReserveFraction);
        assessment.FuelReserveTarget = Mathf.Max(1f, assessment.PredictedFuelNeed * FuelReserveFraction);

        if (country != null && allowMarketPurchases && state.LastPurchaseDay != currentDay)
        {
            state.LastPurchaseDay = currentDay;
            assessment.BoughtResources |= TryBuyMissingResource(safeTeamId, "comida", Mathf.CeilToInt(assessment.FoodReserveTarget), country.comida);
            assessment.BoughtResources |= TryBuyMissingResource(safeTeamId, "petroleo", Mathf.CeilToInt(assessment.FuelReserveTarget), country.petroleo);
        }

        assessment.FoodAvailable = (country != null ? country.comida : 0f)
            + Mathf.Max(0f, economy != null ? economy.comidaProduzida * 10f : 0f);
        assessment.FuelAvailable = (country != null ? country.petroleo : 0f)
            + Mathf.Max(0f, economy != null ? economy.petroleoProduzido * 10f : 0f);
        assessment.FoodReady = assessment.FoodAvailable >= assessment.FoodReserveTarget;
        assessment.FuelReady = assessment.FuelAvailable >= assessment.FuelReserveTarget;
        assessment.ProductionReady = assessment.FoodReady
            || (economy != null && economy.comidaProduzida > 0.01f)
            || (SistemaMercadoGlobal.Instancia != null && SistemaMercadoGlobal.Instancia.ObterItem("comida") != null);

        assessment.HealthCapacity = EstimateInitialHealthCapacity(prefab, safeTeamId);
        float hospitalTarget = projectedPopulation * 0.15f;
        assessment.HealthReady = assessment.HealthCapacity >= hospitalTarget;
        assessment.Ready = assessment.PreparationComplete
            && assessment.FoodReady
            && assessment.FuelReady
            && assessment.ProductionReady
            && assessment.HealthReady;
        assessment.Decision = assessment.Ready
            ? "cidade liberada: comida, combustivel, producao, saude e preparacao aprovados"
            : assessment.ShortReason;
        return assessment.Ready;
    }

    private static PreparationState GetOrCreateState(int teamId, int currentDay)
    {
        PreparationState state;
        if (!States.TryGetValue(teamId, out state))
        {
            state = new PreparationState { StartDay = currentDay };
            States.Add(teamId, state);
        }

        return state;
    }

    private static bool TryBuyMissingResource(int teamId, string itemId, int target, int currentStock)
    {
        int missing = Mathf.Max(0, target - currentStock);
        if (missing <= 0 || SistemaMercadoGlobal.Instancia == null) return false;
        string message;
        return SistemaMercadoGlobal.Instancia.ComprarAutomaticamente(teamId, itemId, missing, out message);
    }

    private static float EstimateInitialHealthCapacity(GameObject prefab, int teamId)
    {
        float capacity = 0f;
        if (prefab != null)
        {
            CidadeComplexoUrbano city = prefab.GetComponentInChildren<CidadeComplexoUrbano>(true);
            if (city != null) capacity += Mathf.Max(0, city.capacidadeHospitalarInicial);
        }

        return capacity + HospitalSaude.ObterCapacidadeTeam(teamId);
    }

    private static int GetCurrentDay()
    {
        if (GerenciadorTempo.Instancia != null) return Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
        return Mathf.Max(1, Mathf.FloorToInt(Time.time / 30f) + 1);
    }
}
