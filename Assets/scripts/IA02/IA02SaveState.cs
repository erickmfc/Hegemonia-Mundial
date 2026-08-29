using System;
using System.Collections.Generic;

namespace Hegemonia.AI.IA02
{
    [Serializable]
    public sealed class SaveIA02ResourceData
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
    public sealed class SaveIA02PopulationData
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
    public sealed class SaveIA02DomainData
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
    public sealed class SaveIA02CacheData
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
    public sealed class SaveIA02TimerData
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
    public sealed class SaveIA02MetricData
    {
        public string key = string.Empty;
        public float value;
    }

    [Serializable]
    public sealed class SaveIA02NationState
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
        public IA02ExecutionMode executionMode;
        public IA02NationMode nationMode;
        public IA02NationStage currentStage;
        public IA02NationPosture currentPosture;
        public IA02NationProfileSnapshot profileSnapshot = new IA02NationProfileSnapshot();
        public SaveIA02PopulationData population = new SaveIA02PopulationData();
        public List<SaveIA02ResourceData> resources = new List<SaveIA02ResourceData>();
        public List<SaveIA02DomainData> cities = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> structures = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> units = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> objectives = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> relationships = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> intents = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> missions = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> orders = new List<SaveIA02DomainData>();
        public List<SaveIA02DomainData> memory = new List<SaveIA02DomainData>();
        public List<SaveIA02CacheData> caches = new List<SaveIA02CacheData>();
        public List<SaveIA02TimerData> timers = new List<SaveIA02TimerData>();
        public List<SaveIA02MetricData> metrics = new List<SaveIA02MetricData>();
        public string foundationSequenceStep = string.Empty;
        public List<string> foundationSkippedSteps = new List<string>();
        public bool foundationFundingGranted;
        public SaveIA02BuildPlanState buildPlanState = new SaveIA02BuildPlanState();
        public string serviceReport = string.Empty;
        public float lastTelemetryMs;
        public float averageTelemetryMs;
        public int version = 1;
    }
}
