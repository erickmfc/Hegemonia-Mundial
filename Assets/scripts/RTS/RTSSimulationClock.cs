using System;
using UnityEngine;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Relogio da simulacao independente de Time.timeScale. Sistemas de IA,
    /// economia e salvamento podem usar ticks previsiveis sem congelar a UI.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class RTSSimulationClock : MonoBehaviour
    {
        public static RTSSimulationClock Instancia { get; private set; }

        [SerializeField, Min(0.02f)] private float tickInterval = 0.10f;
        [SerializeField, Range(0.1f, 4f)] private float simulationSpeed = 1f;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 4;

        public event Action<int, float> OnTick;
        public float SimulationTime { get; private set; }
        public int Tick { get; private set; }
        public bool Paused { get; private set; }
        public float SimulationSpeed => simulationSpeed;
        public float TickInterval => tickInterval;

        private float accumulator;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
            if (RTSGameSession.Instancia != null)
            {
                RTSGameSession.Instancia.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void Update()
        {
            RTSGameSession session = RTSGameSession.Instancia;
            if (session == null || !session.IsGameplay || Paused)
            {
                return;
            }

            accumulator += Time.unscaledDeltaTime * Mathf.Max(0.1f, simulationSpeed);
            int ticks = 0;
            while (accumulator >= tickInterval && ticks < Mathf.Max(1, maxTicksPerFrame))
            {
                accumulator -= tickInterval;
                SimulationTime += tickInterval;
                Tick++;
                ticks++;
                OnTick?.Invoke(Tick, SimulationTime);
            }
        }

        private void OnDestroy()
        {
            if (RTSGameSession.Instancia != null)
            {
                RTSGameSession.Instancia.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (Instancia == this)
            {
                Instancia = null;
            }
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
        }

        public void SetSimulationSpeed(float speed)
        {
            simulationSpeed = Mathf.Clamp(speed, 0.1f, 4f);
        }

        public void ResetClock()
        {
            accumulator = 0f;
            SimulationTime = 0f;
            Tick = 0;
        }

        public void RestoreState(float simulationTime, int tick)
        {
            accumulator = 0f;
            SimulationTime = Mathf.Max(0f, simulationTime);
            Tick = Mathf.Max(0, tick);
        }

        private void HandlePhaseChanged(RTSSessionPhase phase)
        {
            if (phase == RTSSessionPhase.Playing && RTSGameSession.Instancia != null
                && RTSGameSession.Instancia.ElapsedSeconds <= 0.001f)
            {
                ResetClock();
            }

            Paused = phase == RTSSessionPhase.Paused;
        }
    }
}
