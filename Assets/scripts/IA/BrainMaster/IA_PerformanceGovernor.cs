using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_PerformanceGovernor
    {
        private readonly IA_PerformanceStateData _state = new IA_PerformanceStateData();
        private int _criticalConsecutiveSeconds;
        private int _healthyConsecutiveSeconds;

        public IA_PerformanceStateData State
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

        public IA_PerformanceStateData CreateStateSnapshot()
        {
            return CreateStateSnapshot(null);
        }

        public IA_PerformanceStateData CreateStateSnapshot(IA_PerformanceStateData reusable)
        {
            if (reusable == null)
            {
                reusable = new IA_PerformanceStateData();
            }

            reusable.FpsSmoothed = _state.FpsSmoothed;
            reusable.CpuMainSmoothed = _state.CpuMainSmoothed;
            reusable.GcPressure = _state.GcPressure;
            reusable.Band = _state.Band;
            reusable.StableHealthySeconds = _state.StableHealthySeconds;
            reusable.LastUpdatedTime = _state.LastUpdatedTime;
            return reusable;
        }

        public IA_EngagementBudget CreateEngagementBudget()
        {
            return CreateEngagementBudget(null);
        }

        public IA_EngagementBudget CreateEngagementBudget(IA_EngagementBudget reusable)
        {
            if (reusable == null)
            {
                reusable = new IA_EngagementBudget();
            }

            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    reusable.TotalPoints = 20;
                    reusable.LandPoints = 12;
                    reusable.AirPoints = 8;
                    reusable.NavalPoints = 8;
                    break;

                case IA_PerformanceGovernorBand.Pressao:
                    reusable.TotalPoints = 36;
                    reusable.LandPoints = 20;
                    reusable.AirPoints = 14;
                    reusable.NavalPoints = 16;
                    break;

                default:
                    reusable.TotalPoints = 56;
                    reusable.LandPoints = 32;
                    reusable.AirPoints = 20;
                    reusable.NavalPoints = 24;
                    break;
            }

            reusable.ResetUsage();
            return reusable;
        }

        public IA_BattleGovernorDecision CreateBattleDecision(int activeBrains)
        {
            return CreateBattleDecision(activeBrains, null);
        }

        public IA_BattleGovernorDecision CreateBattleDecision(int activeBrains, IA_BattleGovernorDecision reusable)
        {
            if (reusable == null)
            {
                reusable = new IA_BattleGovernorDecision();
            }

            reusable.Band = _state.Band;

            switch (_state.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    reusable.AllowBuild = false;
                    reusable.AllowProduce = true;
                    reusable.AllowHeavyBuild = false;
                    reusable.SuppressEconomicExpansion = true;
                    reusable.MaxActiveFronts = 1;
                    reusable.MaxAirPackages = 1;
                    reusable.MaxNavalPackages = 1;
                    reusable.MaxLandAttackers = 16;
                    reusable.MaxAirAttackers = 6;
                    reusable.MaxNavalAttackers = 4;
                    reusable.MaxProductionCommandsPerCycle = 1;
                    reusable.ProductionCooldownSeconds = 4f;
                    reusable.RetargetCooldownMultiplier = 2f;
                    reusable.PathReplanCooldownMultiplier = 2f;
                    break;

                case IA_PerformanceGovernorBand.Pressao:
                    // Em pressão, ainda permitimos builds leves/essenciais; o que trava o jogo é build pesado
                    // e spam de expansão no meio da batalha.
                    reusable.AllowBuild = true;
                    reusable.AllowProduce = true;
                    reusable.AllowHeavyBuild = false;
                    reusable.SuppressEconomicExpansion = true;
                    reusable.MaxActiveFronts = 1;
                    reusable.MaxAirPackages = 1;
                    reusable.MaxNavalPackages = 1;
                    reusable.MaxLandAttackers = 24;
                    reusable.MaxAirAttackers = 8;
                    reusable.MaxNavalAttackers = 6;
                    reusable.MaxProductionCommandsPerCycle = 1;
                    reusable.ProductionCooldownSeconds = 1.5f;
                    reusable.RetargetCooldownMultiplier = 1.45f;
                    reusable.PathReplanCooldownMultiplier = 1.5f;
                    break;

                default:
                    reusable.AllowBuild = true;
                    reusable.AllowProduce = true;
                    reusable.AllowHeavyBuild = activeBrains <= 2;
                    reusable.SuppressEconomicExpansion = false;
                    reusable.MaxActiveFronts = activeBrains >= 4 ? 1 : 2;
                    reusable.MaxAirPackages = activeBrains >= 5 ? 1 : 2;
                    reusable.MaxNavalPackages = activeBrains >= 5 ? 1 : 2;
                    reusable.MaxLandAttackers = 48;
                    reusable.MaxAirAttackers = 16;
                    reusable.MaxNavalAttackers = 12;
                    reusable.MaxProductionCommandsPerCycle = 2;
                    reusable.ProductionCooldownSeconds = 0f;
                    reusable.RetargetCooldownMultiplier = 1f;
                    reusable.PathReplanCooldownMultiplier = 1f;
                    break;
            }

            return reusable;
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
