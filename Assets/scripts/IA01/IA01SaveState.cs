using System;
using System.Collections.Generic;

namespace Hegemonia.AI.IA01
{
    [Serializable]
    public sealed class SaveIA01ResourceData
    {
        public string resourceId = string.Empty;
        public int nationId;
        public int teamId;
        public float amount;
        public float reserved;
        public float capacity;
        public int version;
        public float lastUpdated;
        public string source = string.Empty;
    }

    [Serializable]
    public sealed class SaveIA01PopulationData
    {
        public int nationId;
        public int teamId;
        public int total;
        public int civilian;
        public int military;
        public int reservists;
        public int available;
        public int workforce;
        public int housingCapacity;
        public float stability;
        public float happiness;
        public int version;
    }

    [Serializable]
    public sealed class SaveIA01DomainData
    {
        public string id = string.Empty;
        public int nationId;
        public int teamId;
        public string kind = string.Empty;
        public string state = string.Empty;
        public string target = string.Empty;
        public string category = string.Empty;
        public string regionKey = string.Empty;
        public string payloadText = string.Empty;
        public float priority;
        public float urgency;
        public float confidence = 1f;
        public float createdAt;
        public float expiresAt = -1f;
        public bool operational = true;
        public int version;
    }

    [Serializable]
    public sealed class SaveIA01CacheData
    {
        public string key = string.Empty;
        public int version;
        public float timestamp;
        public float expiration;
        public string invalidationReason = string.Empty;
        public string sourceRegion = string.Empty;
        public bool dirty;
        public string valueText = string.Empty;
    }

    [Serializable]
    public sealed class SaveIA01TimerData
    {
        public string key = string.Empty;
        public float intervalSeconds;
        public float nextDueAt;
        public float lastFiredAt;
        public int firedCount;
        public bool paused;
        public int version;
    }

    [Serializable]
    public sealed class SaveIA01MetricData
    {
        public string key = string.Empty;
        public float value;
    }

    [Serializable]
    public sealed class SaveIA01NationState
    {
        public int instanceId;
        public int nationId;
        public int teamId;
        public string nationName = string.Empty;
        public string presidentName = string.Empty;
        public string currencyName = string.Empty;
        public string currencySymbol = string.Empty;
        public string countryProfile = string.Empty;
        public string difficultyProfile = string.Empty;
        public int randomSeed;
        public IA01ExecutionMode executionMode;
        public IA01NationMode nationMode;
        public IA01NationStage currentStage;
        public IA01NationPosture currentPosture;
        public IA01NationProfileSnapshot profileSnapshot = new IA01NationProfileSnapshot();
        public SaveIA01PopulationData population = new SaveIA01PopulationData();
        public List<SaveIA01ResourceData> resources = new List<SaveIA01ResourceData>();
        public List<SaveIA01DomainData> cities = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> structures = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> units = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> objectives = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> relationships = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> intents = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> missions = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> orders = new List<SaveIA01DomainData>();
        public List<SaveIA01DomainData> memory = new List<SaveIA01DomainData>();
        public List<SaveIA01CacheData> caches = new List<SaveIA01CacheData>();
        public List<SaveIA01TimerData> timers = new List<SaveIA01TimerData>();
        public List<SaveIA01MetricData> metrics = new List<SaveIA01MetricData>();
        public string foundationSequenceStep = string.Empty;
        public List<string> foundationSkippedSteps = new List<string>();
        public bool foundationFundingGranted;
        public SaveIA01BuildPlanState buildPlanState = new SaveIA01BuildPlanState();
        public string serviceReport = string.Empty;
        public float lastTelemetryMs;
        public float averageTelemetryMs;
        public int version = 1;
    }
}
