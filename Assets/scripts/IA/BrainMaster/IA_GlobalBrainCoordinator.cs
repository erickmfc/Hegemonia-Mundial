using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    /// <summary>
    /// Coordenador global de IAs — evita que múltiplas IAs rodem módulos pesados no mesmo frame.
    ///
    /// COMO FUNCIONA:
    ///   • Cada IA_BrainMaster se registra aqui ao acordar e recebe um índice de slot (0, 1, 2, ...).
    ///   • A cada frame, IA_BrainMaster consulta CanTickThisFrame() antes de chamar _scheduler.Tick().
    ///   • Apenas 1 IA por frame executa módulos pesados (BuildDirector, CoastScan, etc.).
    ///   • As demais IAs ainda executam módulos leves (ProductionDirector, WorldState) se o
    ///     budget global do frame ainda não foi consumido.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class IA_GlobalBrainCoordinator : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------
        private static IA_GlobalBrainCoordinator _instance;

        public static IA_GlobalBrainCoordinator Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[IA_GlobalBrainCoordinator]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<IA_GlobalBrainCoordinator>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        // -----------------------------------------------------------------------
        // Configuração (ajustável no Inspector se o prefab existir na cena)
        // -----------------------------------------------------------------------
        [Header("Budget Global (ms/frame para TODAS as IAs)]")]
        [Tooltip("Máximo de ms que o conjunto de todas as IAs pode consumir na main thread por frame.")]
        [SerializeField] private float _globalBudgetMs = 3.5f;

        [Tooltip("Quantas IAs podem rodar módulos pesados no mesmo frame (heavy slot). 1 = round-robin.")]
        [SerializeField] private int _heavySlotsPerFrame = 1;

        // -----------------------------------------------------------------------
        // Estado interno
        // -----------------------------------------------------------------------
        private readonly List<int> _registeredTeamIds = new List<int>(8);
        // ms consumidos neste frame por todas as IAs (acumulado)
        private double _frameAccumulatedMs;
        private int _frameHeavyCount;
        private int _heavySlotIndex; // qual slot ganhou o heavy token este frame
        private int _lastFrameCount = -1;
        private readonly IA_PerformanceGovernor _performanceGovernor = new IA_PerformanceGovernor();

        // -----------------------------------------------------------------------
        // API pública
        // -----------------------------------------------------------------------

        /// <summary>Registra uma IA e retorna o índice de slot dela (determinístico por teamId).</summary>
        public int Register(int teamId)
        {
            if (!_registeredTeamIds.Contains(teamId))
            {
                _registeredTeamIds.Add(teamId);
            }

            return _registeredTeamIds.IndexOf(teamId);
        }

        public void Unregister(int teamId)
        {
            _registeredTeamIds.Remove(teamId);
        }

        public int ActiveCount
        {
            get { return _registeredTeamIds.Count; }
        }

        public IA_PerformanceGovernorState GetGovernorStateSnapshot()
        {
            return _performanceGovernor.CreateStateSnapshot();
        }

        public IA_BattleGovernorDecision BuildBattleDecision()
        {
            return _performanceGovernor.CreateBattleDecision(ActiveCount);
        }

        public IA_EngagementBudget BuildEngagementBudget()
        {
            return _performanceGovernor.CreateEngagementBudget();
        }

        /// <summary>
        /// Chamado por IA_BrainMaster no início do Update().
        /// Retorna o budget de ms que ESTA IA pode consumir neste frame.
        /// 0 = não deve executar módulos (budget global esgotado).
        /// </summary>
        public float GetBudgetForBrain(int slotIndex)
        {
            EnsureFrameReset();

            int count = Mathf.Max(1, _registeredTeamIds.Count);

            // Budget restante disponível para esta IA
            double remaining = _globalBudgetMs - _frameAccumulatedMs;
            if (remaining <= 0.05)
            {
                return 0f;
            }

            // Divide o orçamento restante igualmente entre as IAs que ainda não rodaram
            // (estimativa conservadora: assume que todas as demais também vão gastar)
            float share = (float)(remaining / Mathf.Max(1, count));
            return Mathf.Max(0.15f, share);
        }

        /// <summary>
        /// Retorna true se esta IA pode executar módulos PESADOS neste frame.
        /// Apenas _heavySlotsPerFrame IAs têm permissão por frame.
        /// </summary>
        public bool CanRunHeavyModules(int slotIndex)
        {
            EnsureFrameReset();

            int effectiveHeavySlots = _performanceGovernor.GetHeavySlotsCap(_heavySlotsPerFrame);
            if (effectiveHeavySlots <= 0)
            {
                return false;
            }

            if (_frameHeavyCount >= effectiveHeavySlots)
            {
                return false;
            }

            int count = Mathf.Max(1, _registeredTeamIds.Count);
            // Distribui o heavy slot em round-robin entre os slots registrados
            bool isHeavySlot = (slotIndex % count) == (_heavySlotIndex % count);
            return isHeavySlot;
        }

        /// <summary>Chamado por IA_BrainMaster após _scheduler.Tick() para reportar o custo.</summary>
        public void ReportFrameCost(int slotIndex, float costMs, bool ranHeavy)
        {
            _frameAccumulatedMs += Mathf.Max(0f, costMs);
            if (ranHeavy)
            {
                _frameHeavyCount++;
            }
        }

        /// <summary>Retorna o budget global por IA ajustado para o número de IAs ativas.</summary>
        public float ComputePerBrainBudgetMs(bool bootstrapActive)
        {
            int count = Mathf.Max(1, _registeredTeamIds.Count);
            float total = _globalBudgetMs * _performanceGovernor.GetBudgetMultiplier();

            if (bootstrapActive)
            {
                // Durante bootstrap, é mais agressivo — constrói logo
                float share = (total * 0.55f) / count;
                return Mathf.Clamp(share, 0.30f, 1.20f);
            }
            else
            {
                float share = total / count;
                return Mathf.Clamp(share, 0.40f, 1.80f);
            }
        }

        /// <summary>Quantos módulos por frame cada IA pode executar, dado o total de IAs.</summary>
        public int ComputeMaxModulesPerFrame(bool bootstrapActive)
        {
            int count = Mathf.Max(1, _registeredTeamIds.Count);
            int baseValue;

            if (bootstrapActive)
            {
                baseValue = Mathf.Clamp(4 - (count - 1), 1, 3);
                return _performanceGovernor.AdjustModuleBudget(baseValue);
            }

            // Com muitas IAs ativas, reduz módulos por frame de cada uma
            baseValue = Mathf.Clamp(5 - (count - 1), 1, 4);
            return _performanceGovernor.AdjustModuleBudget(baseValue);
        }

        // -----------------------------------------------------------------------
        // Unity
        // -----------------------------------------------------------------------
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // -----------------------------------------------------------------------
        // Internos
        // -----------------------------------------------------------------------
        private void EnsureFrameReset()
        {
            int frame = Time.frameCount;
            if (frame == _lastFrameCount)
            {
                return;
            }

            _lastFrameCount = frame;
            _performanceGovernor.RefreshFromRuntime();
            _frameAccumulatedMs = 0d;
            _frameHeavyCount = 0;

            // Avança o heavy slot para o próximo brain no round-robin
            int count = Mathf.Max(1, _registeredTeamIds.Count);
            _heavySlotIndex = (_heavySlotIndex + 1) % count;
        }
    }
}
