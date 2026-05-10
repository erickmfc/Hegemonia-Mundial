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
        private IA_PerformanceGovernorBand _lastReportedBand = (IA_PerformanceGovernorBand)(-1);

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

        public IA_PerformanceStateData GetGovernorStateSnapshot()
        {
            return _performanceGovernor.CreateStateSnapshot();
        }

        public IA_BattleGovernorDecision BuildBattleDecision()
        {
            IA_BattleGovernorDecision decision = _performanceGovernor.CreateBattleDecision(ActiveCount);
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            if (perfil != null)
            {
                decision.MaxLandAttackers = Mathf.Max(1, Mathf.RoundToInt(decision.MaxLandAttackers * perfil.MultiplicadorEngajamentoIA));
                decision.MaxAirAttackers = Mathf.Max(1, Mathf.RoundToInt(decision.MaxAirAttackers * perfil.MultiplicadorEngajamentoIA));
                decision.MaxNavalAttackers = Mathf.Max(1, Mathf.RoundToInt(decision.MaxNavalAttackers * perfil.MultiplicadorEngajamentoIA));
                decision.MaxProductionCommandsPerCycle = perfil.AjustarComandos(decision.MaxProductionCommandsPerCycle);
                decision.ProductionCooldownSeconds = Mathf.Max(0f, decision.ProductionCooldownSeconds * perfil.MultiplicadorCooldownProducaoIA);
                decision.MaxActiveFronts = Mathf.Clamp(decision.MaxActiveFronts + Mathf.Max(0, perfil.BonusComandosIA), 1, 4);
                decision.MaxAirPackages = Mathf.Clamp(decision.MaxAirPackages + Mathf.Max(0, perfil.BonusComandosIA), 1, 4);
                decision.MaxNavalPackages = Mathf.Clamp(decision.MaxNavalPackages + Mathf.Max(0, perfil.BonusComandosIA), 1, 4);
            }

            return decision;
        }

        public IA_EngagementBudget BuildEngagementBudget()
        {
            IA_EngagementBudget budget = _performanceGovernor.CreateEngagementBudget();
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            if (perfil != null)
            {
                budget.TotalPoints = Mathf.Max(1, Mathf.RoundToInt(budget.TotalPoints * perfil.MultiplicadorEngajamentoIA));
                budget.LandPoints = Mathf.Max(1, Mathf.RoundToInt(budget.LandPoints * perfil.MultiplicadorEngajamentoIA));
                budget.AirPoints = Mathf.Max(1, Mathf.RoundToInt(budget.AirPoints * perfil.MultiplicadorEngajamentoIA));
                budget.NavalPoints = Mathf.Max(1, Mathf.RoundToInt(budget.NavalPoints * perfil.MultiplicadorEngajamentoIA));
            }

            return budget;
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
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            float budgetGlobal = _globalBudgetMs * (perfil != null ? perfil.MultiplicadorOrcamentoIA : 1f);
            double remaining = budgetGlobal - _frameAccumulatedMs;
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

            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            int configuredHeavySlots = perfil != null ? perfil.AjustarHeavySlots(_heavySlotsPerFrame) : _heavySlotsPerFrame;
            int effectiveHeavySlots = _performanceGovernor.GetHeavySlotsCap(configuredHeavySlots);
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
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            float total = _globalBudgetMs
                          * _performanceGovernor.GetBudgetMultiplier()
                          * (perfil != null ? perfil.MultiplicadorOrcamentoIA : 1f);

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
                int adjusted = _performanceGovernor.AdjustModuleBudget(baseValue);
                PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
                return perfil != null ? perfil.AjustarModulosPorFrame(adjusted) : adjusted;
            }

            // Com muitas IAs ativas, reduz módulos por frame de cada uma
            baseValue = Mathf.Clamp(5 - (count - 1), 1, 4);
            int adjustedNormal = _performanceGovernor.AdjustModuleBudget(baseValue);
            PerfilDificuldadeJogo perfilNormal = GameDifficultyManager.PerfilAtual;
            return perfilNormal != null ? perfilNormal.AjustarModulosPorFrame(adjustedNormal) : adjustedNormal;
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

            IA_PerformanceGovernorBand band = _performanceGovernor.State.Band;
            if (band != _lastReportedBand)
            {
                _lastReportedBand = band;
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("governor_band", BandToLabel(band));
            }

            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            if (perfil != null)
            {
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("dificuldade", perfil.Codigo);
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("difficulty_stability_target_min", perfil.MetaEstabilidadeMinutos);
            }

            _frameAccumulatedMs = 0d;
            _frameHeavyCount = 0;

            // Avança o heavy slot para o próximo brain no round-robin
            int count = Mathf.Max(1, _registeredTeamIds.Count);
            _heavySlotIndex = (_heavySlotIndex + 1) % count;
        }

        private static string BandToLabel(IA_PerformanceGovernorBand band)
        {
            switch (band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    return "Critico";
                case IA_PerformanceGovernorBand.Pressao:
                    return "Pressao";
                default:
                    return "Saudavel";
            }
        }
    }
}
