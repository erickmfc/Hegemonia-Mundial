using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_PerformanceScheduler
    {
        public int TraceTeamId { get; set; } = -1;
        private sealed class Slot
        {
            public IIAUpdateModule Module;
            public float NextTick;
            public float LastCostMs;
            public float PeakCostMs;
            public int RunCount;
            public int OverBudgetCount;
        }

        public struct Snapshot
        {
            public string Name;
            public float LastCostMs;
            public float PeakCostMs;
            public int RunCount;
            public int OverBudgetCount;
            public float NextTick;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Stopwatch _timer = new Stopwatch();
        private int _roundRobinIndex;

        public float GlobalFrameBudgetMs = 4.5f;
        public int MaxModulesPerFrame = 6;
        public float MinBackoffSeconds = 0.05f;
        public float PhaseOffsetSeconds = 0f;
        /// <summary>
        /// Quando false, modulos de analise espacial pesada sao pulados
        /// neste frame. Definido pelo IA_GlobalBrainCoordinator via round-robin entre IAs.
        /// </summary>
        public bool HeavyModulesAllowed = true;

        public void Register(IIAUpdateModule module, float now, float startDelay = 0f)
        {
            if (module == null || _slots.Any(s => s.Module == module))
            {
                return;
            }

            _slots.Add(new Slot
            {
                Module = module,
                NextTick = now + Mathf.Max(0f, PhaseOffsetSeconds) + Mathf.Max(0f, startDelay)
            });
        }

        public void Tick(float now, float deltaTime)
        {
            if (_slots.Count == 0)
            {
                return;
            }

            _timer.Reset();
            _timer.Start();

            int modulesExecuted = 0;
            int startIndex = _slots.Count > 0 ? Mathf.Abs(_roundRobinIndex) % _slots.Count : 0;
            for (int offset = 0; offset < _slots.Count; offset++)
            {
                int i = (startIndex + offset) % _slots.Count;
                Slot slot = _slots[i];
                if (now < slot.NextTick)
                {
                    continue;
                }

                float moduleStart = (float)_timer.Elapsed.TotalMilliseconds;

                // Modules pesados sao pulados quando o heavy token e de outra IA neste frame
                if (!HeavyModulesAllowed && IsHeavyModule(slot.Module.Name))
                {
                    // Postpone levemente para nao tentar de novo no proximo frame
                    slot.NextTick = now + Mathf.Max(MinBackoffSeconds, slot.Module.Interval * 0.5f);
                    IA_RuntimeTextTrace.LogModule(TraceTeamId, slot.Module.Name, "SKIP_HEAVY", 0f, slot.Module.BudgetMs, "heavy token indisponivel");
                    continue;
                }

                slot.Module.Tick(now, deltaTime);
                float moduleEnd = (float)_timer.Elapsed.TotalMilliseconds;

                slot.LastCostMs = Mathf.Max(0f, moduleEnd - moduleStart);
                slot.PeakCostMs = Mathf.Max(slot.PeakCostMs, slot.LastCostMs);
                slot.RunCount++;
                IA_RuntimeTextTrace.LogModule(TraceTeamId, slot.Module.Name, "RUN", slot.LastCostMs, slot.Module.BudgetMs, "next=" + (now + Mathf.Max(MinBackoffSeconds, slot.Module.Interval)).ToString("0.000", CultureInfo.InvariantCulture));

                bool moduleOverBudget = slot.LastCostMs > Mathf.Max(0.1f, slot.Module.BudgetMs);
                if (moduleOverBudget)
                {
                    slot.OverBudgetCount++;
                    DiagnosticoDesempenhoJogo.RegistrarEvento(
                        "IA",
                        string.Format(
                            "Modulo {0} excedeu o budget: {1:0.00} ms (budget {2:0.00} ms).",
                            slot.Module.Name,
                            slot.LastCostMs,
                            slot.Module.BudgetMs));
                }

                float interval = Mathf.Max(MinBackoffSeconds, slot.Module.Interval);
                if (moduleOverBudget)
                {
                    interval += Mathf.Min(0.35f, interval * 0.5f);
                }

                slot.NextTick = now + interval;
                modulesExecuted++;
                _roundRobinIndex = (i + 1) % _slots.Count;

                if (modulesExecuted >= Mathf.Max(1, MaxModulesPerFrame))
                {
                    break;
                }

                if (_timer.Elapsed.TotalMilliseconds >= Mathf.Max(0.25f, GlobalFrameBudgetMs))
                {
                    break;
                }
            }

            if (modulesExecuted == 0 && _slots.Count > 0)
            {
                _roundRobinIndex = (_roundRobinIndex + 1) % _slots.Count;
            }
        }

        public List<Snapshot> GetSnapshot()
        {
            var output = new List<Snapshot>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                output.Add(new Snapshot
                {
                    Name = slot.Module.Name,
                    LastCostMs = slot.LastCostMs,
                    PeakCostMs = slot.PeakCostMs,
                    RunCount = slot.RunCount,
                    OverBudgetCount = slot.OverBudgetCount,
                    NextTick = slot.NextTick
                });
            }

            return output;
        }

        /// <summary>
        /// Retorna true para modulos que realizam operacoes de busca spatial ou varredura de mapa caras.
        /// Esses modulos serao pulados quando o heavy token for de outra IA no mesmo frame.
        /// </summary>
        private static bool IsHeavyModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            // BuildDirector precisa continuar rodando para criar ordens basicas e fazer o
            // bootstrap. As buscas caras dele possuem locks e backoffs proprios.
            return moduleName == "IA_SemanticMapPlanner"
                   || moduleName == "IA_NavalDirector"
                   || moduleName == "IA_ThreatAnalyzer"
                   || moduleName == "IA_ZonePlanner";
        }
    }
}
