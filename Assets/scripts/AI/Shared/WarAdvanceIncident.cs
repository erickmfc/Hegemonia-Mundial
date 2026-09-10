using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.Shared
{
    public enum WarAdvanceZoneType
    {
        Defensiva,
        AtaqueDeGuerra,
        Expansao
    }

    public enum WarAdvanceIncidentSeverity
    {
        Baixa,
        Media,
        Alta,
        Critica
    }

    public enum WarAdvanceIncidentState
    {
        Suspeito,
        Detectado,
        Confirmado,
        Hostil,
        Guerra
    }

    public enum WarAdvanceResponseMode
    {
        Observacao,
        Investigacao,
        Reforco,
        MobilizacaoMaxima
    }

    public enum WarAdvanceMissionState
    {
        Patrol,
        Recon,
        Intercept,
        Combat,
        Return
    }

    [Serializable]
    public sealed class WarAdvanceIncident
    {
        public Vector3 Position;
        public Vector3 EnemyLastKnownPosition;
        public int AttackerTeamId;
        public WarAdvanceIncidentSeverity Severity;
        public WarAdvanceIncidentState State = WarAdvanceIncidentState.Detectado;
        public float FirstDetectionTime;
        public float LastDetectionTime;
        public int AttackCount;
        public int ShotsDetected;
        public int MissilesDetected;
        public float DamageCaused;
        public string InfrastructureHit;
        public bool TargetDestroyed;
        public bool AttackerStillPresent;
        public bool Confirmed;
        // Evita que o primeiro ciclo de resposta pule diretamente para
        // interceptação/combate quando o radar já marcou o agressor como
        // presente. A zona continua guardando tudo apenas em memória.
        public bool ObservationDispatched;
        public float DurationSeconds = 60f;

        public float ExpirationTime => LastDetectionTime + Mathf.Max(1f, DurationSeconds);

        public WarAdvanceResponseMode ResponseMode
        {
            get
            {
                switch (State)
                {
                    case WarAdvanceIncidentState.Suspeito:
                    case WarAdvanceIncidentState.Detectado: return WarAdvanceResponseMode.Observacao;
                    case WarAdvanceIncidentState.Confirmado: return WarAdvanceResponseMode.Investigacao;
                    case WarAdvanceIncidentState.Hostil: return WarAdvanceResponseMode.Reforco;
                    default: return WarAdvanceResponseMode.MobilizacaoMaxima;
                }
            }
        }

        public WarAdvanceMissionState MissionState
        {
            get
            {
                if (State == WarAdvanceIncidentState.Hostil || State == WarAdvanceIncidentState.Guerra)
                    return WarAdvanceMissionState.Combat;
                if (State == WarAdvanceIncidentState.Confirmado && AttackerStillPresent)
                    return WarAdvanceMissionState.Intercept;
                if (State == WarAdvanceIncidentState.Detectado || State == WarAdvanceIncidentState.Confirmado)
                    return WarAdvanceMissionState.Recon;
                return WarAdvanceMissionState.Patrol;
            }
        }

        public int SuggestedAircraft
        {
            get
            {
                switch (Severity)
                {
                    case WarAdvanceIncidentSeverity.Baixa: return 1;
                    case WarAdvanceIncidentSeverity.Media: return 2;
                    case WarAdvanceIncidentSeverity.Alta: return 4;
                    default: return 8;
                }
            }
        }

        public bool IsActive(float now) => now <= ExpirationTime;
    }

    public interface IWarAdvanceZone
    {
        int TeamId { get; }
        Vector3 Position { get; }
        float Raio { get; }
        bool AceitaIncidentesNavais { get; }
        bool Contains(Vector3 point);
        void RegisterIncident(Vector3 position, Vector3 enemyLastKnownPosition, int attackerTeamId, int shotsDetected, int missilesDetected,
            float damage, string infrastructureHit, bool targetDestroyed, bool attackerStillPresent, bool confirmed, float now);
        bool AtualizarContato(int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent, float now);
        bool UpdateContact(int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent, float now);
    }

    /// <summary>
    /// Registro leve em memória. As zonas se registram quando entram na cena;
    /// incidentes não criam GameObjects e expiram automaticamente.
    /// </summary>
    public static class WarAdvanceZoneRegistry
    {
        private static readonly List<IWarAdvanceZone> zones = new List<IWarAdvanceZone>(32);

        public static void Register(IWarAdvanceZone zone)
        {
            if (zone != null && !zones.Contains(zone)) zones.Add(zone);
        }

        public static void Unregister(IWarAdvanceZone zone)
        {
            if (zone != null) zones.Remove(zone);
        }

        public static void RegisterIncident(int defenderTeamId, Vector3 position, Vector3 enemyLastKnownPosition, int attackerTeamId,
            int shotsDetected, int missilesDetected, float damage, string infrastructureHit,
            bool targetDestroyed = false, bool attackerStillPresent = false, bool confirmed = true)
        {
            float now = Time.unscaledTime;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                IWarAdvanceZone zone = zones[i];
                if (zone == null)
                {
                    zones.RemoveAt(i);
                    continue;
                }
                if (zone.TeamId == defenderTeamId && zone.Contains(position))
                {
                    zone.RegisterIncident(position, enemyLastKnownPosition, attackerTeamId, shotsDetected, missilesDetected, damage,
                        infrastructureHit, targetDestroyed, attackerStillPresent, confirmed, now);
                }
            }
        }

        public static void RegisterNavalIncident(int defenderTeamId, Vector3 position, Vector3 enemyLastKnownPosition, int attackerTeamId,
            int shotsDetected, int missilesDetected, float damage, string infrastructureHit,
            bool targetDestroyed = false, bool attackerStillPresent = false, bool confirmed = true)
        {
            float now = Time.unscaledTime;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                IWarAdvanceZone zone = zones[i];
                if (zone == null)
                {
                    zones.RemoveAt(i);
                    continue;
                }
                if (zone.TeamId == defenderTeamId && zone.AceitaIncidentesNavais && zone.Contains(position))
                {
                    zone.RegisterIncident(position, enemyLastKnownPosition, attackerTeamId, shotsDetected, missilesDetected, damage,
                        infrastructureHit, targetDestroyed, attackerStillPresent, confirmed, now);
                }
            }
        }

        public static void UpdateContact(int defenderTeamId, int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent)
        {
            float now = Time.unscaledTime;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                IWarAdvanceZone zone = zones[i];
                if (zone == null) { zones.RemoveAt(i); continue; }
                if (zone.TeamId == defenderTeamId)
                    zone.UpdateContact(attackerTeamId, lastKnownPosition, attackerStillPresent, now);
            }
        }
    }
}
