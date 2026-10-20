using System;
using UnityEngine;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Relogio de ticks da simulacao. Mantem o ritmo dos sistemas RTS alinhado
    /// a velocidade global do jogo sem alterar a atualizacao da interface.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class RTSSimulationClock : MonoBehaviour
    {
        public static RTSSimulationClock Instancia { get; private set; }

        [SerializeField, Min(0.02f)] private float tickInterval = 0.10f;
        [SerializeField, Range(1f, 32f)] private float simulationSpeed = 1f;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 4;

        public event Action<int, float> OnTick;
        public float SimulationTime { get; private set; }
        public int Tick { get; private set; }
        public bool Paused { get; private set; }
        public float SimulationSpeed => simulationSpeed;
        public float TickInterval => tickInterval;

        private float accumulator;

        // Os presets visuais 1x/2x/4x aplicam escala real 1x/6,75x/32x.
        // O nível 2x fica 1,5x acima dos 4,5x atuais e o 4x dobra os 16x atuais.
        private static readonly float[] SpeedLevels = { 1f, 6.75f, 32f };
        private static readonly string[] SpeedLevelLabels = { "1x", "2x", "4x" };

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            // Converte níveis antigos serializados (2x/4x) para os níveis novos.
            simulationSpeed = NormalizarVelocidade(simulationSpeed);
            DontDestroyOnLoad(gameObject);
            if (RTSGameSession.Instancia != null)
            {
                RTSGameSession.Instancia.OnPhaseChanged += HandlePhaseChanged;
                HandlePhaseChanged(RTSGameSession.Instancia.Phase);
            }
            else
            {
                AplicarEstadoTempo();
            }
        }

        private void Update()
        {
            RTSGameSession session = RTSGameSession.Instancia;
            SincronizarPausaOrquestrador(session);
            if (session == null || session.Phase != RTSSessionPhase.Playing || Paused)
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
            RTSGameSession session = RTSGameSession.Instancia;
            if (session != null)
            {
                if (session.Phase == RTSSessionPhase.Finished
                    || session.Phase == RTSSessionPhase.Menu
                    || session.Phase == RTSSessionPhase.Loading)
                {
                    return;
                }

                session.SetPaused(paused);
                return;
            }

            Paused = paused;
            SincronizarPausaOrquestrador(null);
            AplicarEstadoTempo();
        }

        public void SetSimulationSpeed(float speed)
        {
            float velocidadeSolicitada = NormalizarVelocidade(speed);
            if (velocidadeSolicitada > 1f
                && GovernadorGameplayRTS.BloquearAceleracaoTempo(out string motivoBloqueio))
            {
                velocidadeSolicitada = 1f;
                HUDAjudaRTS.MostrarMensagemTemporaria(motivoBloqueio, 2.8f);
            }

            simulationSpeed = velocidadeSolicitada;
            AplicarEstadoTempo();
        }

        internal static float NormalizarVelocidade(float velocidade)
        {
            if (float.IsNaN(velocidade) || float.IsInfinity(velocidade))
            {
                return SpeedLevels[0];
            }

            float solicitada = Mathf.Clamp(velocidade, SpeedLevels[0], SpeedLevels[2]);
            // Mantém valores já salvos nos presets anteriores nos níveis equivalentes:
            // 3x/4,5x eram o nível médio; 4x/8x/16x eram o nível máximo.
            if (Mathf.Approximately(solicitada, SpeedLevels[1])
                || Mathf.Approximately(solicitada, 3f)
                || Mathf.Approximately(solicitada, 4.5f)
                || Mathf.Approximately(solicitada, 2f)) return SpeedLevels[1];
            if (Mathf.Approximately(solicitada, 8f)
                || Mathf.Approximately(solicitada, 16f)
                || Mathf.Approximately(solicitada, SpeedLevels[2])) return SpeedLevels[2];
            if (solicitada >= 4f) return SpeedLevels[2];
            if (solicitada >= 2f) return SpeedLevels[1];
            return SpeedLevels[0];
        }

        internal static float ProximaVelocidade(float velocidadeAtual, int direcao)
        {
            if (direcao == 0) return NormalizarVelocidade(velocidadeAtual);

            float velocidadeNormalizada = NormalizarVelocidade(velocidadeAtual);
            int indice = velocidadeNormalizada >= SpeedLevels[2] ? 2
                : velocidadeNormalizada >= SpeedLevels[1] ? 1
                : 0;
            indice = Mathf.Clamp(indice + (direcao > 0 ? 1 : -1), 0, SpeedLevels.Length - 1);
            return SpeedLevels[indice];
        }

        internal static string RotuloVelocidade(float velocidade)
        {
            float velocidadeNormalizada = NormalizarVelocidade(velocidade);
            int indice = velocidadeNormalizada >= SpeedLevels[2] ? 2
                : velocidadeNormalizada >= SpeedLevels[1] ? 1
                : 0;
            return SpeedLevelLabels[indice];
        }

        public void AdjustSimulationSpeed(int direction)
        {
            if (direction == 0) return;

            SetSimulationSpeed(ProximaVelocidade(simulationSpeed, direction));
        }

        public void ResetSimulationSpeed()
        {
            SetSimulationSpeed(1f);
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
            SincronizarPausaOrquestrador(RTSGameSession.Instancia);
            AplicarEstadoTempo(phase);
        }

        private void SincronizarPausaOrquestrador(RTSGameSession session)
        {
            OrquestradorGlobalSimulacao.Instancia?.DefinirPausado(
                session == null || session.Phase != RTSSessionPhase.Playing || Paused);
        }

        private void AplicarEstadoTempo(RTSSessionPhase? faseForcada = null)
        {
            RTSGameSession session = RTSGameSession.Instancia;
            RTSSessionPhase fase = faseForcada ?? (session != null ? session.Phase : RTSSessionPhase.Playing);

            if (Paused || fase == RTSSessionPhase.Paused)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                return;
            }

            AudioListener.pause = false;
            if (fase == RTSSessionPhase.Finished)
            {
                Time.timeScale = 0f;
            }
            else if (fase == RTSSessionPhase.Playing)
            {
                Time.timeScale = simulationSpeed;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
    }
}
