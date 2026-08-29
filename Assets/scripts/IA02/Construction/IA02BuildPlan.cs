using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [CreateAssetMenu(fileName = "IA02BuildPlan", menuName = "Hegemonia/IA02/Plano de Construcao")]
    public sealed class IA02BuildPlan : ScriptableObject
    {
        [SerializeField] private string planId = "ia02.plan";
        [SerializeField] private int layoutVersion = 1;
        [SerializeField] private List<IA02BuildPlanStep> steps = new List<IA02BuildPlanStep>();

        public string PlanId => string.IsNullOrWhiteSpace(planId) ? name : planId.Trim();
        public int LayoutVersion => Mathf.Max(1, layoutVersion);
        public IReadOnlyList<IA02BuildPlanStep> Steps => steps;
    }

    [Serializable]
    public sealed class IA02BuildPlanStep
    {
        [Tooltip("Id estavel usado por save/load e diagnosticos.")]
        public string stepId = "new.step";
        public DadosConstrucao constructionData;
        public IA02StrategicRole requiredRole = IA02StrategicRole.None;
        public IA02PlacementMode placementMode = IA02PlacementMode.SlotGroup;
        public string primarySlotId = string.Empty;
        public string slotGroupId = string.Empty;
        public string autonomousZoneId = string.Empty;
        public bool required;
        public int minimumStage;
        [Min(1)] public int maximumCount = 1;
        [Min(0f)] public float cooldownAfterCompletion;
        public IA02BuildCondition condition = new IA02BuildCondition();
        public IA02FailurePolicy failurePolicy = IA02FailurePolicy.Wait;

        public string StepId => string.IsNullOrWhiteSpace(stepId) ? "unnamed.step" : stepId.Trim();
    }

    [Serializable]
    public sealed class IA02BuildCondition
    {
        public IA02BuildConditionType type = IA02BuildConditionType.Always;
        [Min(0f)] public float target = 1f;
        public IA02StrategicRole role = IA02StrategicRole.None;
    }

    [Serializable]
    public sealed class SaveIA02BuildSlotState
    {
        public string slotId = string.Empty;
        public IA02BuildSlotState state;
        public string commandId = string.Empty;
        public int ownerNationId;
        public string constructionId = string.Empty;
        public float reservedAt;
        public string blockReason = string.Empty;
        public int layoutVersion;
    }

    [Serializable]
    public sealed class SaveIA02BuildPlanState
    {
        public string planId = string.Empty;
        public string layoutId = string.Empty;
        public int planVersion;
        public List<string> completedSteps = new List<string>();
        public List<string> blockedSteps = new List<string>();
        public List<SaveIA02BuildSlotState> slots = new List<SaveIA02BuildSlotState>();
        public string pendingCommandId = string.Empty;
        public string pendingStepId = string.Empty;
        public List<SaveIA02BuildCooldownState> cooldowns = new List<SaveIA02BuildCooldownState>();
    }

    [Serializable]
    public sealed class SaveIA02BuildCooldownState
    {
        public string stepId = string.Empty;
        public float until;
    }
}
