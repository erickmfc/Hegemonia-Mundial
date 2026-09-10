using System;
using System.Collections.Generic;
using UnityEngine;

public enum RescueIncidentType
{
    AeronaveAbatida,
    NavioAfundado,
    HelicopteroDestruido,
    VeiculoPerdido,
    FaltaDeCombustivel,
    PousoDeEmergencia,
    UnidadeIncapacitada,
    UnidadeDeResgateDestruida
}

public enum RescueIncidentState
{
    Ativo,
    Concluido,
    SinalPerdido,
    Encerrado
}

/// <summary>
/// Dados puros de uma ocorrência de busca e salvamento. Não cria marcador ou
/// GameObject no mundo; o Quartel pode apenas ler esta informação depois.
/// </summary>
[Serializable]
public sealed class RescueIncident
{
    public string IncidentId;
    public int OwnerTeamId;
    public int OwnerNationId;
    public Vector3 Position;
    public float SearchAreaSize = 50f;

    public int OriginalPersonnel;
    public int MissingPersonnel;
    public int RescuedPersonnel;
    public int ReservedForPickup;
    public int OnboardRescueUnits;
    public int ReturnedPersonnel;
    public int LostPersonnel;

    public int CreatedAtGameDay;
    public float CreatedAtRealtime;
    public int LastUpdatedGameDay;
    public RescueIncidentType IncidentType;
    public int ThreatLevel;
    public RescueIncidentState State = RescueIncidentState.Ativo;
    public bool IsSignalActive = true;
    public bool IsCompleted;
    public string SourceName;
    public string LastReason;
    public List<string> AssignedUnits = new List<string>();

    public int AgeDays(int currentDay)
    {
        return Mathf.Max(0, currentDay - Mathf.Max(1, CreatedAtGameDay));
    }

    public int MaxRecoverable(int currentDay)
    {
        int age = AgeDays(currentDay);
        if (age <= 3) return Mathf.Max(0, OriginalPersonnel);
        if (age <= 5) return OriginalPersonnel > 0 ? Mathf.Max(1, Mathf.FloorToInt(OriginalPersonnel * 0.50f)) : 0;
        if (age <= 10) return OriginalPersonnel > 0 ? Mathf.Max(1, Mathf.FloorToInt(OriginalPersonnel / 3f)) : 0;
        return 0;
    }

    public int AvailableForReservation(int currentDay)
    {
        int remainingByWindow = Mathf.Max(0, MaxRecoverable(currentDay) - RescuedPersonnel);
        return Mathf.Max(0, Mathf.Min(MissingPersonnel - ReservedForPickup, remainingByWindow));
    }

    public bool IsActiveAt(int currentDay)
    {
        return State == RescueIncidentState.Ativo && IsSignalActive && AgeDays(currentDay) <= 10;
    }

    public string WindowLabel(int currentDay)
    {
        int age = AgeDays(currentDay);
        if (age <= 3) return "RECUPERACAO MAXIMA";
        if (age <= 5) return "RECUPERACAO ATE 50%";
        if (age <= 10) return "RECUPERACAO ATE 1/3";
        return "SINAL PERDIDO";
    }
}
