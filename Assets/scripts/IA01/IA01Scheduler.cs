using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01Scheduler
    {
        private sealed class SchedulerState
        {
            public float NextDueAt;
            public float LastRunAt = -999f;
            public int FailureCount;
            public int LastOperations;
            public int LastEvents;
            public float LastBudgetMs;
            public float LastAllocatedBudgetMs;
            public string LastReason = string.Empty;
        }

        private sealed class Candidate
        {
            public IA01Controller Controller;
            public SchedulerState State;
            public float Score;
            public bool Dirty;
        }

        private readonly Dictionary<int, SchedulerState> states = new Dictionary<int, SchedulerState>();
        private readonly List<Candidate> candidates = new List<Candidate>(16);
        public int RegisteredCount => states.Count;
        public float LastPlanFrameBudgetMs { get; private set; }
        public int LastPlanCount { get; private set; }
        public string LastPlanSummary { get; private set; } = string.Empty;

        public void Register(IA01Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!states.ContainsKey(controller.InstanceId))
            {
                states[controller.InstanceId] = new SchedulerState();
            }
        }

        public void Unregister(int instanceId)
        {
            if (instanceId <= 0)
            {
                return;
            }

            states.Remove(instanceId);
        }

        public void Reset()
        {
            states.Clear();
            candidates.Clear();
            LastPlanFrameBudgetMs = 0f;
            LastPlanCount = 0;
            LastPlanSummary = string.Empty;
        }

        public IA01SchedulerPlan BuildPlan(IReadOnlyList<IA01Controller> controllers, float now, float frameBudgetMs)
        {
            IA01SchedulerPlan plan = new IA01SchedulerPlan
            {
                FrameBudgetMs = Mathf.Max(0f, frameBudgetMs),
                RemainingBudgetMs = Mathf.Max(0f, frameBudgetMs)
            };

            if (controllers == null || controllers.Count == 0 || plan.FrameBudgetMs <= 0f)
            {
                LastPlanFrameBudgetMs = plan.FrameBudgetMs;
                LastPlanCount = 0;
                LastPlanSummary = "empty";
                plan.Summary = LastPlanSummary;
                return plan;
            }

            candidates.Clear();
            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller == null || !controller.isActiveAndEnabled || controller.Context == null || controller.Profile == null)
                {
                    continue;
                }

                Register(controller);
                SchedulerState state = states[controller.InstanceId];
                bool dirty = controller.Context.IsDirty;
                // Mudancas ainda ganham prioridade no score, mas respeitam o
                // respiro imposto a uma slice cara. Sem isso, um contexto que se
                // mantem dirty poderia ignorar o cooldown e executar novamente
                // em todos os frames.
                bool due = now >= state.NextDueAt;
                if (!due)
                {
                    continue;
                }

                float score = ResolveScore(controller, state, now, dirty);
                candidates.Add(new Candidate
                {
                    Controller = controller,
                    State = state,
                    Score = score,
                    Dirty = dirty
                });
            }

            if (candidates.Count == 0)
            {
                Candidate maintenance = SelectMaintenanceCandidate(controllers, now);
                if (maintenance != null)
                {
                    candidates.Add(maintenance);
                }
            }

            candidates.Sort(CompareCandidates);

            for (int i = 0; i < candidates.Count && plan.RemainingBudgetMs > 0f; i++)
            {
                Candidate candidate = candidates[i];
                IA01Controller controller = candidate.Controller;
                IA01NationProfile profile = controller.Profile;
                IA01NationIdentity identity = controller.Context.GetIdentitySnapshot();

                float sliceMs = Mathf.Min(plan.RemainingBudgetMs, profile.ResolveSliceBudgetMs(identity.ExecutionMode, identity.CurrentStage, identity.NationMode));
                if (sliceMs <= 0.01f)
                {
                    sliceMs = Mathf.Min(plan.RemainingBudgetMs, profile.MinSliceMilliseconds);
                }

                if (sliceMs <= 0.01f)
                {
                    continue;
                }

                IA01WorkBudget budget = IA01WorkBudget.Create(
                    sliceMs,
                    profile.ResolveOperationBudget(identity.ExecutionMode),
                    profile.ResolveEventBudget(identity.ExecutionMode),
                    identity.ExecutionMode == IA01ExecutionMode.Full || (identity.ExecutionMode == IA01ExecutionMode.Hybrid && profile.AllowSaveIntegration),
                    false);

                plan.Slices.Add(new IA01ScheduledSlice
                {
                    Controller = controller,
                    Budget = budget,
                    DueAt = now,
                    Priority = Mathf.RoundToInt(candidate.Score),
                    Reason = candidate.Dirty ? "dirty" : "maintenance"
                });

                plan.RemainingBudgetMs -= sliceMs;
                plan.ScheduledCount++;
                candidate.State.LastAllocatedBudgetMs = sliceMs;
                candidate.State.LastReason = plan.Slices[plan.Slices.Count - 1].Reason;
            }

            plan.ReadyCount = candidates.Count;
            plan.Summary = "ready=" + plan.ReadyCount + ",scheduled=" + plan.ScheduledCount + ",budgetMs=" + plan.FrameBudgetMs.ToString("0.000");
            LastPlanFrameBudgetMs = plan.FrameBudgetMs;
            LastPlanCount = plan.ScheduledCount;
            LastPlanSummary = plan.Summary;
            return plan;
        }

        public void ReportExecution(IA01Controller controller, IA01WorkResult result, float now)
        {
            if (controller == null)
            {
                return;
            }

            if (!states.TryGetValue(controller.InstanceId, out SchedulerState state))
            {
                state = new SchedulerState();
                states[controller.InstanceId] = state;
            }

            IA01NationIdentity identity = controller.Context != null ? controller.Context.GetIdentitySnapshot() : null;
            IA01NationProfile profile = controller.Profile;

            state.LastRunAt = now;
            state.LastOperations = result.Operations;
            state.LastEvents = result.Events;
            state.LastBudgetMs = result.ConsumedMilliseconds;
            state.FailureCount = result.Completed ? 0 : Mathf.Min(8, state.FailureCount + 1);

            float cadence = profile != null && identity != null
                ? profile.ResolveCadence(identity.ExecutionMode, identity.CurrentStage, identity.NationMode)
                : 0.65f;

            float backoff = 1f + (state.FailureCount * 0.50f);
            float allocatedBudget = Mathf.Max(0.10f, state.LastAllocatedBudgetMs);
            float executionPressure = result.ConsumedMilliseconds / allocatedBudget;
            bool exceededBudget = executionPressure > 1.25f;
            if (exceededBudget)
            {
                // Alguns modulos ainda possuem trabalho atomico maior que o slice.
                // Nao cancelamos a decisao em andamento; apenas damos um respiro
                // proporcional antes da proxima rodada para evitar picos em serie.
                backoff *= Mathf.Clamp(executionPressure, 1.25f, 5f);
            }
            else if (result.Changed)
            {
                backoff *= 0.85f;
            }

            float cooldownForHeavySlice = result.ConsumedMilliseconds >= 50f ? 1.25f
                : result.ConsumedMilliseconds >= 15f ? 0.45f
                : 0f;
            state.NextDueAt = now + Mathf.Max(0.05f, cadence * backoff, cooldownForHeavySlice);
            state.LastReason = result.LastMessage ?? string.Empty;
        }

        public float ResolveNextDueAt(int instanceId)
        {
            return states.TryGetValue(instanceId, out SchedulerState state) ? state.NextDueAt : 0f;
        }

        private Candidate SelectMaintenanceCandidate(IReadOnlyList<IA01Controller> controllers, float now)
        {
            if (controllers == null || controllers.Count == 0)
            {
                return null;
            }

            IA01Controller bestController = null;
            SchedulerState bestState = null;
            float bestDueAt = float.MaxValue;

            for (int i = 0; i < controllers.Count; i++)
            {
                IA01Controller controller = controllers[i];
                if (controller == null || !controller.isActiveAndEnabled || controller.Context == null || controller.Profile == null)
                {
                    continue;
                }

                Register(controller);
                SchedulerState state = states[controller.InstanceId];
                if (state.NextDueAt < bestDueAt)
                {
                    bestDueAt = state.NextDueAt;
                    bestController = controller;
                    bestState = state;
                }
            }

            if (bestController == null)
            {
                return null;
            }

            return new Candidate
            {
                Controller = bestController,
                State = bestState,
                Score = ResolveScore(bestController, bestState, now, false) - 200f,
                Dirty = false
            };
        }

        private float ResolveScore(IA01Controller controller, SchedulerState state, float now, bool dirty)
        {
            IA01NationIdentity identity = controller.Context.GetIdentitySnapshot();
            float score = dirty ? 1000f : 0f;
            score += Mathf.Clamp((now - state.LastRunAt) * 10f, 0f, 120f);

            switch (identity.CurrentStage)
            {
                case IA01NationStage.Initialization:
                    score += 220f;
                    break;
                case IA01NationStage.Reconnaissance:
                    score += 200f;
                    break;
                case IA01NationStage.Survival:
                    score += 180f;
                    break;
                case IA01NationStage.Stabilization:
                    score += 170f;
                    break;
                case IA01NationStage.UrbanDevelopment:
                    score += 150f;
                    break;
                case IA01NationStage.Industrialization:
                    score += 130f;
                    break;
                case IA01NationStage.Specialization:
                    score += 120f;
                    break;
                case IA01NationStage.RegionalProjection:
                    score += 100f;
                    break;
                case IA01NationStage.GlobalPower:
                    score += 90f;
                    break;
                case IA01NationStage.Recovering:
                    score += 240f;
                    break;
                case IA01NationStage.Emergency:
                    score += 260f;
                    break;
                case IA01NationStage.FailedSafe:
                    score += 300f;
                    break;
            }

            switch (identity.ExecutionMode)
            {
                case IA01ExecutionMode.ObserverDebug:
                    score += 20f;
                    break;
                case IA01ExecutionMode.Manual:
                    score += 10f;
                    break;
                case IA01ExecutionMode.Hybrid:
                    score += 35f;
                    break;
                case IA01ExecutionMode.Full:
                    score += 50f;
                    break;
            }

            switch (identity.NationMode)
            {
                case IA01NationMode.Peace:
                    score += 5f;
                    break;
                case IA01NationMode.Normal:
                    score += 15f;
                    break;
                case IA01NationMode.War:
                    score += 45f;
                    break;
            }

            if (controller.Context.IsDirty)
            {
                score += 80f;
            }

            return score;
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            if (a == null && b == null)
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            int byNation = a.Controller.NationId.CompareTo(b.Controller.NationId);
            if (byNation != 0)
            {
                return byNation;
            }

            return a.Controller.InstanceId.CompareTo(b.Controller.InstanceId);
        }
    }
}
