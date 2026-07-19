using System.Collections.Generic;
using UnityEngine;
using Hegemonia.AI.Shared;

namespace Hegemonia.AI.Master
{
    /// <summary>
    /// Coordena orçamento global de IA para manter estabilidade de FPS quando múltiplos países rodam juntos.
    /// </summary>
    public sealed class IA_RuntimeCoordinator
    {
        private static IA_RuntimeCoordinator _instance;
        public static IA_RuntimeCoordinator Instance => _instance ?? (_instance = new IA_RuntimeCoordinator());

        private readonly Dictionary<int, IA_ControllerState<IA_MasterController.RuntimeSeverity>> _controllers = new Dictionary<int, IA_ControllerState<IA_MasterController.RuntimeSeverity>>(16);
        private readonly List<int> _orderedIds = new List<int>(16);

        private IA_RuntimeCoordinator()
        {
        }

        public int GlobalAiTarget = 3;
        public int GlobalCommandCap = 24;
        public float TargetMinFps = 55f;

        public void Register(int controllerId, int teamId)
        {
            if (!_controllers.ContainsKey(controllerId))
            {
                _controllers.Add(controllerId, new IA_ControllerState<IA_MasterController.RuntimeSeverity> { TeamId = teamId });
                _orderedIds.Add(controllerId);
            }
        }

        public void Unregister(int controllerId)
        {
            _controllers.Remove(controllerId);
            _orderedIds.Remove(controllerId);
        }

        public int ActiveControllers => _orderedIds.Count;

        public int ResolveCommandCap(int requestedByController)
        {
            int count = Mathf.Max(1, ActiveControllers);
            int perAiCap = Mathf.Max(2, GlobalCommandCap / count);
            return Mathf.Max(2, Mathf.Min(requestedByController, perAiCap));
        }

        public float ResolveBudgetScale(float smoothedFps)
        {
            int count = Mathf.Max(1, ActiveControllers);
            if (count <= GlobalAiTarget && smoothedFps >= TargetMinFps)
            {
                return 1f;
            }

            float overload = count > GlobalAiTarget
                ? Mathf.Clamp01((count - GlobalAiTarget) / 4f)
                : 0f;

            float fpsPenalty = smoothedFps < TargetMinFps
                ? Mathf.Clamp01((TargetMinFps - smoothedFps) / Mathf.Max(8f, TargetMinFps))
                : 0f;

            float scale = 1f - (overload * 0.30f) - (fpsPenalty * 0.45f);
            return Mathf.Clamp(scale, 0.35f, 1f);
        }

        public bool ShouldRunHeavy(int controllerId, int frameIndex)
        {
            int count = _orderedIds.Count;
            if (count <= 1)
            {
                return true;
            }

            int slot = _orderedIds.IndexOf(controllerId);
            if (slot < 0)
            {
                return true;
            }

            return (frameIndex % count) == slot;
        }

        public bool ShouldRunGrid(int controllerId, int frameIndex, IA_MasterController.RuntimeSeverity severity)
        {
            if (severity <= IA_MasterController.RuntimeSeverity.Watch)
            {
                return true;
            }

            int count = _orderedIds.Count;
            if (count <= 1)
            {
                return true;
            }

            int slot = _orderedIds.IndexOf(controllerId);
            if (slot < 0)
            {
                return true;
            }

            int divisor = severity == IA_MasterController.RuntimeSeverity.Throttled ? 2 : 3;
            return (frameIndex % (count * divisor)) == slot;
        }

        public IA_MasterController.RuntimeSeverity ResolveSeverity(
            int controllerId,
            IA_MasterController.RuntimeSeverity measured,
            float smoothedFps,
            float minimumSafeFps)
        {
            IA_ControllerState<IA_MasterController.RuntimeSeverity> state;
            if (!_controllers.TryGetValue(controllerId, out state))
            {
                return measured;
            }

            IA_MasterController.RuntimeSeverity wanted = measured;
            if (smoothedFps < Mathf.Min(TargetMinFps, minimumSafeFps))
            {
                if (wanted < IA_MasterController.RuntimeSeverity.Watch)
                {
                    wanted = IA_MasterController.RuntimeSeverity.Watch;
                }
            }

            if (wanted > state.StableSeverity)
            {
                state.EscalateVotes++;
                state.RelaxVotes = 0;
                if (state.EscalateVotes >= 2)
                {
                    state.StableSeverity = wanted;
                    state.EscalateVotes = 0;
                }
            }
            else if (wanted < state.StableSeverity)
            {
                state.RelaxVotes++;
                state.EscalateVotes = 0;
                if (state.RelaxVotes >= 5)
                {
                    state.StableSeverity = wanted;
                    state.RelaxVotes = 0;
                }
            }
            else
            {
                state.EscalateVotes = 0;
                state.RelaxVotes = 0;
            }

            return state.StableSeverity;
        }
    }
}
