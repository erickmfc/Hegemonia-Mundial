using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_PerformanceGovernor
    {
        private readonly IA_PerformanceGovernorState _state = new IA_PerformanceGovernorState();
        private int _criticalConsecutiveSeconds;
        private int _healthyConsecutiveSeconds;

        public IA_PerformanceGovernorState State
        {
            get { return _state; }
        }

        public void RefreshFromRuntime()
        {
            float fps;
            float cpuMainMs;
            bool gcPressure;
            bool warmup;
            if (!DiagnosticoDesempenhoJogo.TryObterSnapshotRuntime(out fps, out cpuMainMs, out gcPressure, out warmup))
            {
                float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                fps = 1f / dt;
                cpuMainMs = dt * 1000f;
                gcPressure = false;
                warmup = false;
            }

            if (warmup)
            {
                return;
            }

            _state.FpsSmoothed = _state.LastUpdatedTime <= 0f
                ? Mathf.Max(1f, fps)
                : Mathf.Lerp(_state.FpsSmoothed, Mathf.Max(1f, fps), 0.35f);
            _state.CpuMainSmoothed = _state.LastUpdatedTime <= 0f
                ? Mathf.Max(0f, cpuMainMs)
                : Mathf.Lerp(_state.CpuMainSmoothed, Mathf.Max(0f, cpuMainMs), 0.35f);
            _state.GcPressure = gcPressure;
            _state.LastUpdatedTime = Time.unscaledTime;

            bool criticalNow = gcPressure || _state.FpsSmoothed < 20f || _state.CpuMainSmoothed > 40f;
            bool healthyNow = !gcPressure && _state.FpsSmoothed > 35f && _state.CpuMainSmoothed < 25f;

            if (criticalNow)
            {
                _criticalConsecutiveSeconds++;
                _healthyConsecutiveSeconds = 0;
            }
            else
            {
                _criticalConsecutiveSeconds = 0;
                _healthyConsecutiveSeconds = healthyNow ? (_healthyConsecutiveSeconds + 1) : 0;
            }

            _state.StableHealthySeconds = _healthyConsecutiveSeconds;

            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    if (_healthyConsecutiveSeconds >= 4)
                    {
                        _state.Band = healthyNow
                            ? IA_PerformanceGovernorBand.Saudavel
                            : IA_PerformanceGovernorBand.Pressao;
                    }
                    break;

                case IA_PerformanceGovernorBand.Pressao:
                    if (_criticalConsecutiveSeconds >= 2)
                    {
                        _state.Band = IA_PerformanceGovernorBand.Critico;
                    }
                    else if (_healthyConsecutiveSeconds >= 10)
                    {
                        _state.Band = IA_PerformanceGovernorBand.Saudavel;
                    }
                    break;

                default:
                    if (_criticalConsecutiveSeconds >= 2)
                    {
                        _state.Band = IA_PerformanceGovernorBand.Critico;
                    }
                    else if (!healthyNow)
                    {
                        _state.Band = IA_PerformanceGovernorBand.Pressao;
                    }
                    break;
            }
        }

        public IA_PerformanceGovernorState CreateStateSnapshot()
        {
            return _state.Clone();
        }

        public IA_EngagementBudget CreateEngagementBudget()
        {
            IA_EngagementBudget budget = new IA_EngagementBudget();
            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    budget.TotalPoints = 10;
                    budget.LandPoints = 6;
                    budget.AirPoints = 4;
                    budget.NavalPoints = 4;
                    break;

                case IA_PerformanceGovernorBand.Pressao:
                    budget.TotalPoints = 18;
                    budget.LandPoints = 10;
                    budget.AirPoints = 7;
                    budget.NavalPoints = 8;
                    break;

                default:
                    budget.TotalPoints = 28;
                    budget.LandPoints = 16;
                    budget.AirPoints = 10;
                    budget.NavalPoints = 12;
                    break;
            }

            budget.ResetUsage();
            return budget;
        }

        public IA_BattleGovernorDecision CreateBattleDecision(int activeBrains)
        {
            IA_BattleGovernorDecision decision = new IA_BattleGovernorDecision
            {
                Band = _state.Band
            };

            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    decision.AllowBuild = false;
                    decision.AllowProduce = true;
                    decision.AllowHeavyBuild = false;
                    decision.SuppressEconomicExpansion = true;
                    decision.MaxActiveFronts = 1;
                    decision.MaxAirPackages = 1;
                    decision.MaxNavalPackages = 1;
                    decision.MaxLandAttackers = 8;
                    decision.MaxAirAttackers = 3;
                    decision.MaxNavalAttackers = 2;
                    decision.MaxProductionCommandsPerCycle = 1;
                    decision.ProductionCooldownSeconds = 4f;
                    decision.RetargetCooldownMultiplier = 2f;
                    decision.PathReplanCooldownMultiplier = 2f;
                    break;

                case IA_PerformanceGovernorBand.Pressao:
                    // Em pressão, ainda permitimos builds leves/essenciais; o que trava o jogo é build pesado
                    // e spam de expansão no meio da batalha.
                    decision.AllowBuild = true;
                    decision.AllowProduce = true;
                    decision.AllowHeavyBuild = false;
                    decision.SuppressEconomicExpansion = true;
                    decision.MaxActiveFronts = 1;
                    decision.MaxAirPackages = 1;
                    decision.MaxNavalPackages = 1;
                    decision.MaxLandAttackers = 12;
                    decision.MaxAirAttackers = 4;
                    decision.MaxNavalAttackers = 3;
                    decision.MaxProductionCommandsPerCycle = 1;
                    decision.ProductionCooldownSeconds = 1.5f;
                    decision.RetargetCooldownMultiplier = 1.45f;
                    decision.PathReplanCooldownMultiplier = 1.5f;
                    break;

                default:
                    decision.AllowBuild = true;
                    decision.AllowProduce = true;
                    decision.AllowHeavyBuild = activeBrains <= 2;
                    decision.SuppressEconomicExpansion = false;
                    decision.MaxActiveFronts = activeBrains >= 4 ? 1 : 2;
                    decision.MaxAirPackages = activeBrains >= 5 ? 1 : 2;
                    decision.MaxNavalPackages = activeBrains >= 5 ? 1 : 2;
                    decision.MaxLandAttackers = 24;
                    decision.MaxAirAttackers = 8;
                    decision.MaxNavalAttackers = 6;
                    decision.MaxProductionCommandsPerCycle = 2;
                    decision.ProductionCooldownSeconds = 0f;
                    decision.RetargetCooldownMultiplier = 1f;
                    decision.PathReplanCooldownMultiplier = 1f;
                    break;
            }

            return decision;
        }

        public float GetBudgetMultiplier()
        {
            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    return 0.65f;
                case IA_PerformanceGovernorBand.Pressao:
                    return 0.82f;
                default:
                    return 1f;
            }
        }

        public int GetHeavySlotsCap(int configuredHeavySlots)
        {
            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    return 0;
                case IA_PerformanceGovernorBand.Pressao:
                    return Mathf.Min(configuredHeavySlots, 1);
                default:
                    return Mathf.Max(1, configuredHeavySlots);
            }
        }

        public int AdjustModuleBudget(int baseValue)
        {
            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    return Mathf.Max(1, baseValue - 2);
                case IA_PerformanceGovernorBand.Pressao:
                    return Mathf.Max(1, baseValue - 1);
                default:
                    return Mathf.Max(1, baseValue);
            }
        }
    }
}
