using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [CreateAssetMenu(fileName = "IA01BuildPlan", menuName = "Hegemonia/IA01/Plano de Construcao")]
    public sealed class IA01BuildPlan : ScriptableObject
    {
        [SerializeField] private string planId = "ia01.plan";
        [SerializeField] private int layoutVersion = 1;
        [SerializeField] private List<IA01BuildPlanStep> steps = new List<IA01BuildPlanStep>();

        public string PlanId => string.IsNullOrWhiteSpace(planId) ? name : planId.Trim();
        public int LayoutVersion => Mathf.Max(1, layoutVersion);
        public IReadOnlyList<IA01BuildPlanStep> Steps => steps;
    }

    [Serializable]
    public sealed class IA01BuildPlanStep
    {
        [Tooltip("Id estavel usado por save/load e diagnosticos.")]
        public string stepId = "new.step";
        public DadosConstrucao constructionData;
        public IA01StrategicRole requiredRole = IA01StrategicRole.None;
        public IA01PlacementMode placementMode = IA01PlacementMode.SlotGroup;
        public string primarySlotId = string.Empty;
        public string slotGroupId = string.Empty;
        public string autonomousZoneId = string.Empty;
        public bool required;
        public int minimumStage;
        [Min(1)] public int maximumCount = 1;
        [Min(0f)] public float cooldownAfterCompletion;
        public IA01BuildCondition condition = new IA01BuildCondition();
        public IA01FailurePolicy failurePolicy = IA01FailurePolicy.Wait;

        public string StepId => string.IsNullOrWhiteSpace(stepId) ? "unnamed.step" : stepId.Trim();
    }

    [Serializable]
    public sealed class IA01BuildCondition
    {
        public IA01BuildConditionType type = IA01BuildConditionType.Always;
        [Min(0f)] public float target = 1f;
        public IA01StrategicRole role = IA01StrategicRole.None;
    }

    [Serializable]
    public sealed class SaveIA01BuildSlotState
    {
        public string slotId = string.Empty;
        public IA01BuildSlotState state;
        public string commandId = string.Empty;
        public int ownerNationId;
        public string constructionId = string.Empty;
        public float reservedAt;
        public string blockReason = string.Empty;
        public int layoutVersion;
    }

    [Serializable]
    public sealed class SaveIA01BuildPlanState
    {
        public string planId = string.Empty;
        public string layoutId = string.Empty;
        public int planVersion;
        public List<string> completedSteps = new List<string>();
        public List<string> blockedSteps = new List<string>();
        public List<SaveIA01BuildSlotState> slots = new List<SaveIA01BuildSlotState>();
        public string pendingCommandId = string.Empty;
        public string pendingStepId = string.Empty;
        public List<SaveIA01BuildCooldownState> cooldowns = new List<SaveIA01BuildCooldownState>();
    }

    [Serializable]
    public sealed class SaveIA01BuildCooldownState
    {
        public string stepId = string.Empty;
        public float until;
    }
}
