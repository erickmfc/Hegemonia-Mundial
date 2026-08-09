using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum RTSObjectiveKind
    {
        DestroyEnemyCapital,
        ProtectOwnCapital,
        ControlTerritory,
        EconomicMilestone,
        Custom
    }

    [Serializable]
    public sealed class RTSObjectiveStatus
    {
        public string id;
        public string title;
        public RTSObjectiveKind kind;
        public float progress;
        public bool completed;
        public bool failed;
        public string detail;
    }

    [Serializable]
    public sealed class RTSObjectiveSaveData
    {
        public string id;
        public string title;
        public RTSObjectiveKind kind;
        public float progress;
        public bool completed;
        public bool failed;
        public string detail;
    }

    /// <summary>
    /// Camada inicial de objetivos. A eliminacao da capital continua compativel
    /// com ComplexoGovernamental, mas agora existe um ponto unico para HUD,
    /// save e cenarios futuros consultarem o progresso.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RTSObjectiveService : MonoBehaviour
    {
        public static RTSObjectiveService Instancia { get; private set; }

        private readonly List<RTSObjectiveStatus> objectives = new List<RTSObjectiveStatus>();
        private float nextEvaluationAt;
        private float lastObservedSessionTime = -1f;
        private bool sawBothCapitals;

        public event Action<RTSObjectiveStatus> OnObjectiveChanged;
        public IReadOnlyList<RTSObjectiveStatus> Objectives => objectives;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            RTSGameSession session = RTSGameSession.Instancia;
            if (session == null || !session.IsGameplay || session.IsFinished || Time.unscaledTime < nextEvaluationAt)
            {
                return;
            }

            if (lastObservedSessionTime >= 0f && session.ElapsedSeconds + 0.01f < lastObservedSessionTime)
            {
                ResetRuntimeObjectives();
            }

            lastObservedSessionTime = session.ElapsedSeconds;
            nextEvaluationAt = Time.unscaledTime + 0.5f;
            EvaluateCapitalObjectives(session.PlayerTeamId);
        }

        private void OnDestroy()
        {
            if (Instancia == this)
            {
                Instancia = null;
            }
        }

        public RTSObjectiveStatus Register(string id, string title, RTSObjectiveKind kind, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < objectives.Count; i++)
            {
                if (objectives[i] != null && string.Equals(objectives[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return objectives[i];
                }
            }

            RTSObjectiveStatus objective = new RTSObjectiveStatus
            {
                id = id.Trim(),
                title = string.IsNullOrWhiteSpace(title) ? id.Trim() : title.Trim(),
                kind = kind,
                progress = 0f,
                detail = detail ?? string.Empty
            };
            objectives.Add(objective);
            OnObjectiveChanged?.Invoke(objective);
            return objective;
        }

        public bool SetProgress(string id, float progress, bool completed = false, bool failed = false, string detail = null)
        {
            RTSObjectiveStatus objective = Find(id);
            if (objective == null)
            {
                return false;
            }

            objective.progress = Mathf.Clamp01(progress);
            objective.completed = completed;
            objective.failed = failed;
            if (detail != null)
            {
                objective.detail = detail;
            }
            OnObjectiveChanged?.Invoke(objective);
            return true;
        }

        public List<RTSObjectiveSaveData> CaptureState()
        {
            List<RTSObjectiveSaveData> saved = new List<RTSObjectiveSaveData>(objectives.Count);
            for (int i = 0; i < objectives.Count; i++)
            {
                RTSObjectiveStatus objective = objectives[i];
                if (objective == null) continue;

                saved.Add(new RTSObjectiveSaveData
                {
                    id = objective.id,
                    title = objective.title,
                    kind = objective.kind,
                    progress = objective.progress,
                    completed = objective.completed,
                    failed = objective.failed,
                    detail = objective.detail
                });
            }

            return saved;
        }

        public void RestoreState(IList<RTSObjectiveSaveData> saved)
        {
            if (saved == null || saved.Count == 0)
            {
                return;
            }

            objectives.Clear();
            for (int i = 0; i < saved.Count; i++)
            {
                RTSObjectiveSaveData data = saved[i];
                if (data == null || string.IsNullOrWhiteSpace(data.id)) continue;

                RTSObjectiveStatus objective = new RTSObjectiveStatus
                {
                    id = data.id,
                    title = string.IsNullOrWhiteSpace(data.title) ? data.id : data.title,
                    kind = data.kind,
                    progress = Mathf.Clamp01(data.progress),
                    completed = data.completed,
                    failed = data.failed,
                    detail = data.detail ?? string.Empty
                };
                objectives.Add(objective);
                OnObjectiveChanged?.Invoke(objective);
            }
        }

        private void EvaluateCapitalObjectives(int playerTeamId)
        {
            ComplexoGovernamental[] capitals = FindObjectsByType<ComplexoGovernamental>(FindObjectsSortMode.None);
            bool playerCapital = false;
            bool enemyCapital = false;
            for (int i = 0; i < capitals.Length; i++)
            {
                ComplexoGovernamental capital = capitals[i];
                if (capital == null || !capital.isActiveAndEnabled)
                {
                    continue;
                }

                IdentidadeUnidade identity = capital.GetComponent<IdentidadeUnidade>();
                int team = identity != null ? identity.teamID : (capital.ehDoJogador ? playerTeamId : 2);
                if (team == playerTeamId) playerCapital = true;
                else if (team > 0) enemyCapital = true;
            }

            if (playerCapital && enemyCapital)
            {
                sawBothCapitals = true;
                Register("destroy-enemy-capital", "Destruir a capital inimiga", RTSObjectiveKind.DestroyEnemyCapital, "Neutralize o complexo governamental rival.");
                Register("protect-own-capital", "Proteger a propria capital", RTSObjectiveKind.ProtectOwnCapital, "Mantenha sua sede governamental ativa.");
                SetProgress("destroy-enemy-capital", 0f);
                SetProgress("protect-own-capital", 1f);
                return;
            }

            if (!sawBothCapitals)
            {
                return;
            }

            if (!enemyCapital)
            {
                SetProgress("destroy-enemy-capital", 1f, true, false, "Capital inimiga neutralizada.");
                SistemaFimDeJogo.RegistrarResultado(TipoObjetivoFinal.Prefeitura, false, "Nacao inimiga", "Capital inimiga");
            }
            else if (!playerCapital)
            {
                SetProgress("protect-own-capital", 0f, false, true, "Sua capital foi destruida.");
                SistemaFimDeJogo.RegistrarResultado(TipoObjetivoFinal.Prefeitura, true, "Sua nacao", "Capital propria");
            }
        }

        private RTSObjectiveStatus Find(string id)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                if (objectives[i] != null && string.Equals(objectives[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return objectives[i];
                }
            }

            return null;
        }

        private void ResetRuntimeObjectives()
        {
            sawBothCapitals = false;
            for (int i = 0; i < objectives.Count; i++)
            {
                RTSObjectiveStatus objective = objectives[i];
                if (objective == null) continue;

                objective.progress = 0f;
                objective.completed = false;
                objective.failed = false;
                OnObjectiveChanged?.Invoke(objective);
            }
        }
    }
}
