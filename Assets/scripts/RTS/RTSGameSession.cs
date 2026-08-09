using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.RTS
{
    public enum RTSSessionPhase
    {
        Menu,
        Loading,
        Playing,
        Paused,
        Finished
    }

    public enum RTSMatchResult
    {
        None,
        Victory,
        Defeat,
        Draw
    }

    /// <summary>
    /// Autoridade leve para o ciclo de vida da partida. Os sistemas antigos
    /// continuam funcionando, mas passam a ter uma fonte comum para saber se
    /// existe uma partida RTS ativa e qual e o resultado dela.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RTSGameSession : MonoBehaviour
    {
        public static RTSGameSession Instancia { get; private set; }

        public event Action<RTSSessionPhase> OnPhaseChanged;
        public event Action<RTSMatchResult, string> OnMatchFinished;

        public RTSSessionPhase Phase { get; private set; } = RTSSessionPhase.Loading;
        public RTSMatchResult Result { get; private set; } = RTSMatchResult.None;
        public int PlayerTeamId { get; private set; } = 1;
        public int PrimaryAiTeamId { get; private set; } = 2;
        public int Difficulty { get; private set; } = 1;
        public float ElapsedSeconds { get; private set; }
        public bool IsGameplay => Phase == RTSSessionPhase.Playing || Phase == RTSSessionPhase.Paused;
        public bool IsFinished => Phase == RTSSessionPhase.Finished;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetPhase(IsGameplayScene(SceneManager.GetActiveScene().name)
                ? RTSSessionPhase.Playing
                : RTSSessionPhase.Menu);
        }

        private void Update()
        {
            if (Phase == RTSSessionPhase.Playing)
            {
                ElapsedSeconds += Time.unscaledDeltaTime;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instancia == this)
            {
                Instancia = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameplayScene(scene.name))
            {
                if (Result != RTSMatchResult.None)
                {
                    ResetForNewMatch(PlayerTeamId, PrimaryAiTeamId, Difficulty);
                }
                else
                {
                    SetPhase(RTSSessionPhase.Playing);
                }
            }
            else
            {
                SetPhase(RTSSessionPhase.Menu);
            }
        }

        public void BeginGameplay(int playerTeamId = 1, int primaryAiTeamId = 2, int difficulty = 1)
        {
            PlayerTeamId = Mathf.Max(1, playerTeamId);
            PrimaryAiTeamId = Mathf.Max(1, primaryAiTeamId);
            Difficulty = Mathf.Clamp(difficulty, 0, 2);
            Result = RTSMatchResult.None;
            ElapsedSeconds = 0f;
            SetPhase(RTSSessionPhase.Playing);
        }

        public void EnterGameplay(int playerTeamId = 1, int primaryAiTeamId = 2, int difficulty = 1)
        {
            PlayerTeamId = Mathf.Max(1, playerTeamId);
            PrimaryAiTeamId = Mathf.Max(1, primaryAiTeamId);
            Difficulty = Mathf.Clamp(difficulty, 0, 2);
            if (Result == RTSMatchResult.None)
            {
                SetPhase(RTSSessionPhase.Playing);
            }
        }

        public void RestoreState(int playerTeamId, int primaryAiTeamId, int difficulty, float elapsedSeconds, RTSMatchResult result)
        {
            PlayerTeamId = Mathf.Max(1, playerTeamId);
            PrimaryAiTeamId = Mathf.Max(1, primaryAiTeamId);
            Difficulty = Mathf.Clamp(difficulty, 0, 2);
            ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            Result = result;
            SetPhase(result == RTSMatchResult.None ? RTSSessionPhase.Playing : RTSSessionPhase.Finished);
        }

        public void ResetForNewMatch(int playerTeamId = 1, int primaryAiTeamId = 2, int difficulty = 1)
        {
            BeginGameplay(playerTeamId, primaryAiTeamId, difficulty);
        }

        public void SetPaused(bool paused)
        {
            if (Phase == RTSSessionPhase.Finished || Phase == RTSSessionPhase.Menu)
            {
                return;
            }

            SetPhase(paused ? RTSSessionPhase.Paused : RTSSessionPhase.Playing);
        }

        public bool ReportMatchResult(RTSMatchResult result, string reason = null)
        {
            if (result == RTSMatchResult.None || IsFinished)
            {
                return false;
            }

            Result = result;
            SetPhase(RTSSessionPhase.Finished);
            OnMatchFinished?.Invoke(result, reason ?? string.Empty);
            return true;
        }

        private void SetPhase(RTSSessionPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private static bool IsGameplayScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName)
                && !ConfiguracaoCenasJogo.EhCenaDeMenu(sceneName);
        }
    }
}
