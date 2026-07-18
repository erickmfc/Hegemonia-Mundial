using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01RuntimeContext
    {
        private IA01NationIdentity identity;
        private readonly Dictionary<string, IA01ResourceRecord> resources = new Dictionary<string, IA01ResourceRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly IA01PopulationRecord population = new IA01PopulationRecord();
        private readonly Dictionary<string, IA01DomainRecord> cities = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> structures = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> units = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> objectives = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> relationships = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> intents = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> missions = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> orders = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01DomainRecord> memory = new Dictionary<string, IA01DomainRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01CacheEntry> caches = new Dictionary<string, IA01CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IA01TimerEntry> timers = new Dictionary<string, IA01TimerEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<IA01DirtyReason> dirtyReasons = new HashSet<IA01DirtyReason>();
        private readonly StringBuilder summaryBuilder = new StringBuilder(512);
        private readonly Stopwatch maintenanceStopwatch = new Stopwatch();
        private System.Random random = new System.Random(1);

        private string serviceReport = string.Empty;
        private float lastUpdatedAt;
        private float lastMaintenanceMs;
        private float totalMaintenanceMs;
        private int maintenanceSampleCount;
        private int version = 1;

        public IA01RuntimeContext()
        {
            identity = new IA01NationIdentity
            {
                NationName = "Nation",
                PresidentName = "President",
                CurrencyName = "Credit",
                CurrencySymbol = "$",
                CountryProfile = "Neutral",
                DifficultyProfile = "normal",
                ExecutionMode = IA01ExecutionMode.Manual,
                NationMode = IA01NationMode.Normal,
                CurrentStage = IA01NationStage.Initialization,
                CurrentPosture = IA01NationPosture.Development
            };
        }

        public int Version => version;
        public int InstanceId => identity != null ? identity.InstanceId : 0;
        public int NationId => identity != null ? identity.NationId : 0;
        public int TeamId => identity != null ? identity.TeamId : 0;
        public string NationName => identity != null ? identity.NationName : string.Empty;
        public string PresidentName => identity != null ? identity.PresidentName : string.Empty;
        public string CurrencyName => identity != null ? identity.CurrencyName : string.Empty;
        public string CurrencySymbol => identity != null ? identity.CurrencySymbol : string.Empty;
        public string CountryProfile => identity != null ? identity.CountryProfile : string.Empty;
        public string DifficultyProfile => identity != null ? identity.DifficultyProfile : string.Empty;
        public int RandomSeed => identity != null ? identity.RandomSeed : 0;
        public IA01ExecutionMode ExecutionMode => identity != null ? identity.ExecutionMode : IA01ExecutionMode.Manual;
        public IA01NationMode NationMode => identity != null ? identity.NationMode : IA01NationMode.Normal;
        public IA01NationStage CurrentStage => identity != null ? identity.CurrentStage : IA01NationStage.Initialization;
        public IA01NationPosture CurrentPosture => identity != null ? identity.CurrentPosture : IA01NationPosture.Development;
        public float LastUpdatedAt => lastUpdatedAt;
        public string ServiceReport => serviceReport;
        public bool IsDirty => dirtyReasons.Count > 0;
        public int DirtyCount => dirtyReasons.Count;

        public int ResourceCount => resources.Count;
        public int CityCount => cities.Count;
        public int StructureCount => structures.Count;
        public int UnitCount => units.Count;
        public int ObjectiveCount => objectives.Count;
        public int RelationshipCount => relationships.Count;
        public int IntentCount => intents.Count;
        public int MissionCount => missions.Count;
        public int OrderCount => orders.Count;
        public int MemoryCount => memory.Count;
        public int CacheCount => caches.Count;
        public int TimerCount => timers.Count;
        public int MetricCount => metrics.Count;

        public IA01NationIdentity GetIdentitySnapshot()
        {
            return identity != null ? identity.Clone() : null;
        }

        public void ApplyIdentity(IA01NationIdentity newIdentity)
        {
            IA01NationIdentity resolvedIdentity = newIdentity != null ? newIdentity.Clone() : new IA01NationIdentity();
            if (resolvedIdentity.InstanceId <= 0)
            {
                resolvedIdentity.InstanceId = resolvedIdentity.NationId > 0 ? resolvedIdentity.NationId : 1;
            }

            if (resolvedIdentity.NationId <= 0)
            {
                resolvedIdentity.NationId = resolvedIdentity.InstanceId;
            }

            if (resolvedIdentity.TeamId <= 0)
            {
                resolvedIdentity.TeamId = resolvedIdentity.NationId;
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.NationName))
            {
                resolvedIdentity.NationName = "Nation " + resolvedIdentity.NationId;
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.PresidentName))
            {
                resolvedIdentity.PresidentName = "President " + resolvedIdentity.NationId;
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.CurrencyName))
            {
                resolvedIdentity.CurrencyName = "Credit";
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.CurrencySymbol))
            {
                resolvedIdentity.CurrencySymbol = "$";
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.CountryProfile))
            {
                resolvedIdentity.CountryProfile = "Neutral";
            }

            if (string.IsNullOrWhiteSpace(resolvedIdentity.DifficultyProfile))
            {
                resolvedIdentity.DifficultyProfile = "normal";
            }

            if (IdentityEquals(identity, resolvedIdentity))
            {
                return;
            }

            identity = resolvedIdentity;
            random = new System.Random(identity.RandomSeed == 0 ? identity.NationId : identity.RandomSeed);
            MarkDirty(IA01DirtyReason.IdentityChanged);
        }

        public void SetExecutionMode(IA01ExecutionMode mode)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            if (identity.ExecutionMode != mode)
            {
                identity.ExecutionMode = mode;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetNationMode(IA01NationMode mode)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            if (identity.NationMode != mode)
            {
                identity.NationMode = mode;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetCurrentStage(IA01NationStage stage)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            if (identity.CurrentStage != stage)
            {
                identity.CurrentStage = stage;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetCurrentPosture(IA01NationPosture posture)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            if (identity.CurrentPosture != posture)
            {
                identity.CurrentPosture = posture;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetCountryProfile(string profile)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            profile = string.IsNullOrWhiteSpace(profile) ? string.Empty : profile.Trim();
            if (!string.Equals(identity.CountryProfile, profile, StringComparison.Ordinal))
            {
                identity.CountryProfile = profile;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetDifficultyProfile(string difficulty)
        {
            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            difficulty = string.IsNullOrWhiteSpace(difficulty) ? string.Empty : difficulty.Trim();
            if (!string.Equals(identity.DifficultyProfile, difficulty, StringComparison.Ordinal))
            {
                identity.DifficultyProfile = difficulty;
                MarkDirty(IA01DirtyReason.ProfileChanged);
            }
        }

        public void SetServiceReport(string report)
        {
            report = report ?? string.Empty;
            if (!string.Equals(serviceReport, report, StringComparison.Ordinal))
            {
                serviceReport = report;
                StoreMemory("service.report", report, "IA01ServiceDiagnostics");
                MarkDirty(IA01DirtyReason.ServiceSnapshotChanged);
            }
        }

        public void MarkDirty(IA01DirtyReason reason)
        {
            if (dirtyReasons.Add(reason))
            {
                version++;
            }
        }

        public List<IA01DirtyReason> ConsumeDirtyReasons()
        {
            List<IA01DirtyReason> consumed = new List<IA01DirtyReason>(dirtyReasons);
            dirtyReasons.Clear();
            return consumed;
        }

        public void ClearDirtyReasons()
        {
            dirtyReasons.Clear();
        }

        public void SetResource(string resourceId, float amount, float reserved = 0f, float capacity = 0f, string source = "")
        {
            SetResourceInternal(resourceId, amount, reserved, capacity, source, true);
        }

        // External snapshots should only wake planners when the observed value changed.
        public bool SetResourceSnapshot(string resourceId, float amount, float reserved = 0f, float capacity = 0f, string source = "")
        {
            return SetResourceInternal(resourceId, amount, reserved, capacity, source, false);
        }

        private bool SetResourceInternal(string resourceId, float amount, float reserved, float capacity, string source, bool forceDirty)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            string key = resourceId.Trim();
            float resolvedAmount = Mathf.Max(0f, amount);
            float resolvedReserved = Mathf.Max(0f, reserved);
            float resolvedCapacity = Mathf.Max(0f, capacity);
            string resolvedSource = source ?? string.Empty;
            IA01ResourceRecord record;
            bool created = !resources.TryGetValue(key, out record) || record == null;
            if (created)
            {
                record = new IA01ResourceRecord
                {
                    ResourceId = key,
                    NationId = NationId,
                    TeamId = TeamId,
                    Version = version
                };
                resources[record.ResourceId] = record;
            }

            bool changed = created || !Mathf.Approximately(record.Amount, resolvedAmount) ||
                !Mathf.Approximately(record.Reserved, resolvedReserved) ||
                !Mathf.Approximately(record.Capacity, resolvedCapacity) ||
                !string.Equals(record.Source, resolvedSource, StringComparison.Ordinal);
            if (!changed && !forceDirty)
            {
                return false;
            }

            record.Amount = resolvedAmount;
            record.Reserved = resolvedReserved;
            record.Capacity = resolvedCapacity;
            record.Version = version;
            record.LastUpdated = Time.unscaledTime;
            record.Source = resolvedSource;
            MarkDirty(IA01DirtyReason.EconomyChanged);
            return changed;
        }

        public void AdjustResource(string resourceId, float delta, string source = "")
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return;
            }

            string key = resourceId.Trim();
            IA01ResourceRecord record;
            if (!TryGetResourceInternal(key, out record))
            {
                record = new IA01ResourceRecord
                {
                    ResourceId = key,
                    NationId = NationId,
                    TeamId = TeamId
                };
                resources[record.ResourceId] = record;
            }

            record.Amount = Mathf.Max(0f, record.Amount + delta);
            record.Version = version;
            record.LastUpdated = Time.unscaledTime;
            record.Source = source ?? string.Empty;
            MarkDirty(IA01DirtyReason.EconomyChanged);
        }

        public bool TryGetResource(string resourceId, out IA01ResourceRecord record)
        {
            if (!TryGetResourceInternal(resourceId, out record))
            {
                return false;
            }

            record = CloneResourceRecord(record);
            return true;
        }

        public float GetResourceAmount(string resourceId)
        {
            IA01ResourceRecord record;
            return TryGetResourceInternal(resourceId, out record) ? record.Amount : 0f;
        }

        public void SetPopulation(int total, int civilian, int military, int reservists, int available, int workforce, int housingCapacity, float stability, float happiness)
        {
            population.NationId = NationId;
            population.TeamId = TeamId;
            population.Total = Mathf.Max(0, total);
            population.Civilian = Mathf.Max(0, civilian);
            population.Military = Mathf.Max(0, military);
            population.Reservists = Mathf.Max(0, reservists);
            population.Available = Mathf.Max(0, available);
            population.Workforce = Mathf.Max(0, workforce);
            population.HousingCapacity = Mathf.Max(0, housingCapacity);
            population.Stability = stability;
            population.Happiness = happiness;
            population.Version = version;
            MarkDirty(IA01DirtyReason.PopulationChanged);
        }

        public IA01PopulationRecord GetPopulationSnapshot()
        {
            return new IA01PopulationRecord
            {
                NationId = population.NationId,
                TeamId = population.TeamId,
                Total = population.Total,
                Civilian = population.Civilian,
                Military = population.Military,
                Reservists = population.Reservists,
                Available = population.Available,
                Workforce = population.Workforce,
                HousingCapacity = population.HousingCapacity,
                Stability = population.Stability,
                Happiness = population.Happiness,
                Version = population.Version
            };
        }

        public void SetCityRecord(IA01DomainRecord record) => UpsertDomainRecord(cities, record, IA01DirtyReason.WorldChanged);
        public void SetStructureRecord(IA01DomainRecord record) => UpsertDomainRecord(structures, record, IA01DirtyReason.WorldChanged);
        public void SetUnitRecord(IA01DomainRecord record) => UpsertDomainRecord(units, record, IA01DirtyReason.WorldChanged);
        public void SetObjectiveRecord(IA01DomainRecord record) => UpsertDomainRecord(objectives, record, IA01DirtyReason.ExternalEvent);
        public void SetRelationshipRecord(IA01DomainRecord record) => UpsertDomainRecord(relationships, record, IA01DirtyReason.ExternalEvent);
        public void SetIntentRecord(IA01DomainRecord record) => UpsertDomainRecord(intents, record, IA01DirtyReason.ExternalEvent);
        public void SetMissionRecord(IA01DomainRecord record) => UpsertDomainRecord(missions, record, IA01DirtyReason.ExternalEvent);
        public void SetOrderRecord(IA01DomainRecord record) => UpsertDomainRecord(orders, record, IA01DirtyReason.ExternalEvent);
        public void StoreMemoryRecord(IA01DomainRecord record) => UpsertDomainRecord(memory, record, IA01DirtyReason.ExternalEvent);

        public void StoreMemory(string key, string value, string source = "")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            IA01DomainRecord record = new IA01DomainRecord
            {
                Id = key.Trim(),
                NationId = NationId,
                TeamId = TeamId,
                Kind = "memory",
                State = "stored",
                PayloadText = value ?? string.Empty,
                Category = source ?? string.Empty,
                CreatedAt = Time.unscaledTime,
                Version = version,
                Operational = true
            };

            StoreMemoryRecord(record);
        }

        public bool TryGetMemory(string key, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            IA01DomainRecord record;
            if (!memory.TryGetValue(key.Trim(), out record) || record == null)
            {
                return false;
            }

            value = record.PayloadText;
            return true;
        }

        public void SetCache(string key, object value, int versionStamp, float ttlSeconds, string invalidationReason = "", string sourceRegion = "")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            IA01CacheEntry entry;
            if (!caches.TryGetValue(key, out entry) || entry == null)
            {
                entry = new IA01CacheEntry
                {
                    Key = key.Trim()
                };
                caches[entry.Key] = entry;
            }

            entry.Value = value;
            entry.ValueText = value != null ? value.ToString() : string.Empty;
            entry.Version = versionStamp;
            entry.Timestamp = Time.unscaledTime;
            entry.Expiration = ttlSeconds > 0f ? Time.unscaledTime + ttlSeconds : -1f;
            entry.InvalidationReason = invalidationReason ?? string.Empty;
            entry.SourceRegion = sourceRegion ?? string.Empty;
            entry.Dirty = false;
            MarkDirty(IA01DirtyReason.CacheInvalidated);
        }

        public bool TryGetCache(string key, out IA01CacheEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!caches.TryGetValue(key.Trim(), out IA01CacheEntry stored) || stored == null)
            {
                return false;
            }

            entry = CloneCacheEntry(stored);
            return true;
        }

        public int CleanupExpiredCaches(float now)
        {
            List<string> expired = null;
            foreach (KeyValuePair<string, IA01CacheEntry> pair in caches)
            {
                IA01CacheEntry entry = pair.Value;
                if (entry == null || entry.Expiration < 0f || entry.Expiration > now)
                {
                    continue;
                }

                if (expired == null)
                {
                    expired = new List<string>();
                }

                expired.Add(pair.Key);
            }

            if (expired == null)
            {
                return 0;
            }

            for (int i = 0; i < expired.Count; i++)
            {
                caches.Remove(expired[i]);
            }

            if (expired.Count > 0)
            {
                MarkDirty(IA01DirtyReason.CacheInvalidated);
            }

            return expired.Count;
        }

        public void SetTimer(string key, float intervalSeconds, float nextDueAt, bool paused = false)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            IA01TimerEntry timer;
            if (!timers.TryGetValue(key, out timer) || timer == null)
            {
                timer = new IA01TimerEntry
                {
                    Key = key.Trim()
                };
                timers[timer.Key] = timer;
            }

            timer.IntervalSeconds = Mathf.Max(0.01f, intervalSeconds);
            timer.NextDueAt = nextDueAt;
            timer.Paused = paused;
            timer.Version = version;
        }

        public bool TryGetTimer(string key, out IA01TimerEntry timer)
        {
            timer = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!timers.TryGetValue(key.Trim(), out IA01TimerEntry stored) || stored == null)
            {
                return false;
            }

            timer = CloneTimerEntry(stored);
            return true;
        }

        public int TickTimers(float now)
        {
            int fired = 0;
            foreach (KeyValuePair<string, IA01TimerEntry> pair in timers)
            {
                IA01TimerEntry timer = pair.Value;
                if (timer == null || timer.Paused || timer.IntervalSeconds <= 0f || now < timer.NextDueAt)
                {
                    continue;
                }

                timer.LastFiredAt = now;
                timer.FiredCount++;
                timer.NextDueAt = now + timer.IntervalSeconds;
                timer.Version = version;
                fired++;
            }

            if (fired > 0)
            {
                MarkDirty(IA01DirtyReason.TimerFired);
            }

            return fired;
        }

        public void SetMetric(string key, double value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            metrics[key.Trim()] = value;
        }

        public bool TryGetMetric(string key, out double value)
        {
            value = 0d;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return metrics.TryGetValue(key.Trim(), out value);
        }

        public int AdvanceMaintenance(float now)
        {
            maintenanceStopwatch.Restart();
            int expiredCaches = CleanupExpiredCaches(now);
            int timerFires = TickTimers(now);
            lastUpdatedAt = now;
            maintenanceStopwatch.Stop();
            lastMaintenanceMs = (float)maintenanceStopwatch.Elapsed.TotalMilliseconds;
            totalMaintenanceMs += lastMaintenanceMs;
            maintenanceSampleCount++;
            if (expiredCaches > 0 || timerFires > 0)
            {
                MarkDirty(IA01DirtyReason.ManualRefresh);
            }

            return expiredCaches + timerFires;
        }

        public void ApplyGovernmentSnapshot(global::DadosPaisGoverno country)
        {
            if (country == null)
            {
                return;
            }

            if (identity == null)
            {
                identity = new IA01NationIdentity();
            }

            identity.NationId = country.teamId;
            identity.TeamId = country.teamId;
            if (string.IsNullOrWhiteSpace(identity.NationName))
            {
                identity.NationName = country.nomePais;
            }
            if (string.IsNullOrWhiteSpace(identity.PresidentName))
            {
                identity.PresidentName = country.nomePresidente;
            }
            if (string.IsNullOrWhiteSpace(identity.CurrencyName))
            {
                identity.CurrencyName = country.nomeMoeda;
            }
            if (string.IsNullOrWhiteSpace(identity.CurrencySymbol))
            {
                identity.CurrencySymbol = country.simboloMoeda;
            }
            identity.CountryProfile = country.perfilIA.ToString();
            if (string.IsNullOrWhiteSpace(identity.DifficultyProfile))
            {
                identity.DifficultyProfile = country.modoInicialIA.ToString();
            }

            SetResourceSnapshot("food", country.comida, 0f, Mathf.Max(country.comida, 1), "government");
            SetResourceSnapshot("oil", country.petroleo, 0f, Mathf.Max(country.petroleo, 1), "government");
            SetResourceSnapshot("energy", country.energia, 0f, Mathf.Max(country.energia, 1), "government");
            SetResourceSnapshot("steel", country.aco, 0f, Mathf.Max(country.aco, 1), "government");
            SetResourceSnapshot("armaments", country.armamentos, 0f, Mathf.Max(country.armamentos, 1), "government");
            SetPopulation(
                country.populacao,
                country.populacaoCivil,
                country.populacaoMilitarAtiva,
                country.reservistas,
                country.alistaveis,
                country.populacaoCivil + country.reservistas + country.alistaveis,
                country.populacaoMaxima,
                country.estabilidade,
                country.felicidade);

            SetMetric("economy.score", country.PontuacaoEconomica());
            SetMetric("employment", country.emprego);
            SetMetric("stability", country.estabilidade);
            SetMetric("production", country.producao);
            MarkDirty(IA01DirtyReason.GovernmentChanged);
        }

        public void ApplyServiceSnapshot(IA01ServiceDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            SetServiceReport(snapshot.Report);
            if (!string.IsNullOrWhiteSpace(snapshot.DifficultyCode))
            {
                SetDifficultyProfile(snapshot.DifficultyCode);
            }
            SetMetric("services.available", snapshot.AvailableServices.Count);
            SetMetric("services.missing", snapshot.MissingServices.Count);
        }

        public SaveIA01NationState CaptureSaveState()
        {
            SaveIA01NationState state = new SaveIA01NationState
            {
                instanceId = InstanceId,
                nationId = NationId,
                teamId = TeamId,
                nationName = NationName,
                presidentName = PresidentName,
                currencyName = CurrencyName,
                currencySymbol = CurrencySymbol,
                countryProfile = CountryProfile,
                difficultyProfile = DifficultyProfile,
                randomSeed = RandomSeed,
                executionMode = ExecutionMode,
                nationMode = NationMode,
                currentStage = CurrentStage,
                currentPosture = CurrentPosture,
                population = new SaveIA01PopulationData
                {
                    nationId = population.NationId,
                    teamId = population.TeamId,
                    total = population.Total,
                    civilian = population.Civilian,
                    military = population.Military,
                    reservists = population.Reservists,
                    available = population.Available,
                    workforce = population.Workforce,
                    housingCapacity = population.HousingCapacity,
                    stability = population.Stability,
                    happiness = population.Happiness,
                    version = population.Version
                },
                serviceReport = serviceReport,
                lastTelemetryMs = lastMaintenanceMs,
                averageTelemetryMs = maintenanceSampleCount > 0 ? totalMaintenanceMs / maintenanceSampleCount : lastMaintenanceMs
            };

            CopyResources(state.resources);
            CopyDomains(cities, state.cities);
            CopyDomains(structures, state.structures);
            CopyDomains(units, state.units);
            CopyDomains(objectives, state.objectives);
            CopyDomains(relationships, state.relationships);
            CopyDomains(intents, state.intents);
            CopyDomains(missions, state.missions);
            CopyDomains(orders, state.orders);
            CopyDomains(memory, state.memory);
            CopyCaches(state.caches);
            CopyTimers(state.timers);
            CopyMetrics(state.metrics);
            state.version = version;
            return state;
        }

        public void RestoreFromSaveState(SaveIA01NationState state)
        {
            if (state == null)
            {
                return;
            }

            ApplyIdentity(new IA01NationIdentity
            {
                InstanceId = state.instanceId,
                NationId = state.nationId,
                TeamId = state.teamId,
                NationName = state.nationName,
                PresidentName = state.presidentName,
                CurrencyName = state.currencyName,
                CurrencySymbol = state.currencySymbol,
                CountryProfile = state.countryProfile,
                DifficultyProfile = state.difficultyProfile,
                RandomSeed = state.randomSeed,
                ExecutionMode = state.executionMode,
                NationMode = state.nationMode,
                CurrentStage = state.currentStage,
                CurrentPosture = state.currentPosture
            });

            ClearDomainBags();
            resources.Clear();
            caches.Clear();
            timers.Clear();
            metrics.Clear();

            if (state.resources != null)
            {
                for (int i = 0; i < state.resources.Count; i++)
                {
                    SaveIA01ResourceData saved = state.resources[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.resourceId))
                    {
                        continue;
                    }

                    resources[saved.resourceId] = new IA01ResourceRecord
                    {
                        ResourceId = saved.resourceId,
                        NationId = saved.nationId,
                        TeamId = saved.teamId,
                        Amount = saved.amount,
                        Reserved = saved.reserved,
                        Capacity = saved.capacity,
                        Version = saved.version,
                        LastUpdated = saved.lastUpdated,
                        Source = saved.source ?? string.Empty
                    };
                }
            }

            if (state.population != null)
            {
                population.NationId = state.population.nationId;
                population.TeamId = state.population.teamId;
                population.Total = state.population.total;
                population.Civilian = state.population.civilian;
                population.Military = state.population.military;
                population.Reservists = state.population.reservists;
                population.Available = state.population.available;
                population.Workforce = state.population.workforce;
                population.HousingCapacity = state.population.housingCapacity;
                population.Stability = state.population.stability;
                population.Happiness = state.population.happiness;
                population.Version = state.population.version;
            }

            RestoreDomains(cities, state.cities);
            RestoreDomains(structures, state.structures);
            RestoreDomains(units, state.units);
            RestoreDomains(objectives, state.objectives);
            RestoreDomains(relationships, state.relationships);
            RestoreDomains(intents, state.intents);
            RestoreDomains(missions, state.missions);
            RestoreDomains(orders, state.orders);
            RestoreDomains(memory, state.memory);
            RestoreCaches(state.caches);
            RestoreTimers(state.timers);
            RestoreMetrics(state.metrics);

            serviceReport = state.serviceReport ?? string.Empty;
            version = Mathf.Max(1, state.version);
            if (!string.IsNullOrWhiteSpace(serviceReport))
            {
                StoreMemory("service.report", serviceReport, "restore");
            }
        }

        public string BuildDebugSummary()
        {
            summaryBuilder.Clear();
            summaryBuilder.Append("nation=").Append(NationName);
            summaryBuilder.Append(" id=").Append(NationId);
            summaryBuilder.Append(" team=").Append(TeamId);
            summaryBuilder.Append(" stage=").Append(CurrentStage);
            summaryBuilder.Append(" posture=").Append(CurrentPosture);
            summaryBuilder.Append(" mode=").Append(ExecutionMode);
            summaryBuilder.Append(" nationMode=").Append(NationMode);
            summaryBuilder.Append(" resources=").Append(ResourceCount);
            summaryBuilder.Append(" cities=").Append(CityCount);
            summaryBuilder.Append(" structures=").Append(StructureCount);
            summaryBuilder.Append(" units=").Append(UnitCount);
            summaryBuilder.Append(" objectives=").Append(ObjectiveCount);
            summaryBuilder.Append(" relationships=").Append(RelationshipCount);
            summaryBuilder.Append(" intents=").Append(IntentCount);
            summaryBuilder.Append(" missions=").Append(MissionCount);
            summaryBuilder.Append(" orders=").Append(OrderCount);
            summaryBuilder.Append(" memory=").Append(MemoryCount);
            summaryBuilder.Append(" caches=").Append(CacheCount);
            summaryBuilder.Append(" timers=").Append(TimerCount);
            summaryBuilder.Append(" dirty=").Append(DirtyCount);
            return summaryBuilder.ToString();
        }

        public int AdvanceRandomInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxExclusive);
        }

        public float AdvanceRandomFloat()
        {
            return (float)random.NextDouble();
        }

        public bool AdvanceRandomChance(float chance)
        {
            return AdvanceRandomFloat() <= Mathf.Clamp01(chance);
        }

        public double AdvanceRandomRange(double minInclusive, double maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + (random.NextDouble() * (maxInclusive - minInclusive));
        }

        private static bool IdentityEquals(IA01NationIdentity left, IA01NationIdentity right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.InstanceId == right.InstanceId &&
                left.NationId == right.NationId &&
                left.TeamId == right.TeamId &&
                left.RandomSeed == right.RandomSeed &&
                left.ExecutionMode == right.ExecutionMode &&
                left.NationMode == right.NationMode &&
                left.CurrentStage == right.CurrentStage &&
                left.CurrentPosture == right.CurrentPosture &&
                string.Equals(left.NationName, right.NationName, StringComparison.Ordinal) &&
                string.Equals(left.PresidentName, right.PresidentName, StringComparison.Ordinal) &&
                string.Equals(left.CurrencyName, right.CurrencyName, StringComparison.Ordinal) &&
                string.Equals(left.CurrencySymbol, right.CurrencySymbol, StringComparison.Ordinal) &&
                string.Equals(left.CountryProfile, right.CountryProfile, StringComparison.Ordinal) &&
                string.Equals(left.DifficultyProfile, right.DifficultyProfile, StringComparison.Ordinal);
        }

        private bool TryGetResourceInternal(string resourceId, out IA01ResourceRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            return resources.TryGetValue(resourceId.Trim(), out record) && record != null;
        }

        private static IA01ResourceRecord CloneResourceRecord(IA01ResourceRecord source)
        {
            if (source == null)
            {
                return null;
            }

            return new IA01ResourceRecord
            {
                ResourceId = source.ResourceId,
                NationId = source.NationId,
                TeamId = source.TeamId,
                Amount = source.Amount,
                Reserved = source.Reserved,
                Capacity = source.Capacity,
                Version = source.Version,
                LastUpdated = source.LastUpdated,
                Source = source.Source
            };
        }

        private static IA01CacheEntry CloneCacheEntry(IA01CacheEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new IA01CacheEntry
            {
                Key = source.Key,
                Version = source.Version,
                Timestamp = source.Timestamp,
                Expiration = source.Expiration,
                InvalidationReason = source.InvalidationReason,
                SourceRegion = source.SourceRegion,
                Dirty = source.Dirty,
                ValueText = source.ValueText,
                Value = source.Value
            };
        }

        private static IA01TimerEntry CloneTimerEntry(IA01TimerEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new IA01TimerEntry
            {
                Key = source.Key,
                IntervalSeconds = source.IntervalSeconds,
                NextDueAt = source.NextDueAt,
                LastFiredAt = source.LastFiredAt,
                FiredCount = source.FiredCount,
                Paused = source.Paused,
                Version = source.Version
            };
        }

        private void UpsertDomainRecord(Dictionary<string, IA01DomainRecord> bag, IA01DomainRecord record, IA01DirtyReason dirtyReason)
        {
            if (bag == null || record == null || string.IsNullOrWhiteSpace(record.Id))
            {
                return;
            }

            IA01DomainRecord clone = CloneDomainRecord(record);
            bag[clone.Id] = clone;
            MarkDirty(dirtyReason);
        }

        private static IA01DomainRecord CloneDomainRecord(IA01DomainRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new IA01DomainRecord
            {
                Id = record.Id,
                NationId = record.NationId,
                TeamId = record.TeamId,
                Kind = record.Kind,
                State = record.State,
                Target = record.Target,
                Category = record.Category,
                RegionKey = record.RegionKey,
                PayloadText = record.PayloadText,
                Priority = record.Priority,
                Urgency = record.Urgency,
                Confidence = record.Confidence,
                CreatedAt = record.CreatedAt,
                ExpiresAt = record.ExpiresAt,
                Operational = record.Operational,
                Version = record.Version
            };
        }

        private void ClearDomainBags()
        {
            cities.Clear();
            structures.Clear();
            units.Clear();
            objectives.Clear();
            relationships.Clear();
            intents.Clear();
            missions.Clear();
            orders.Clear();
            memory.Clear();
        }

        private void RestoreDomains(Dictionary<string, IA01DomainRecord> bag, List<SaveIA01DomainData> savedList)
        {
            bag.Clear();
            if (savedList == null)
            {
                return;
            }

            for (int i = 0; i < savedList.Count; i++)
            {
                SaveIA01DomainData saved = savedList[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.id))
                {
                    continue;
                }

                bag[saved.id] = new IA01DomainRecord
                {
                    Id = saved.id,
                    NationId = saved.nationId,
                    TeamId = saved.teamId,
                    Kind = saved.kind ?? string.Empty,
                    State = saved.state ?? string.Empty,
                    Target = saved.target ?? string.Empty,
                    Category = saved.category ?? string.Empty,
                    RegionKey = saved.regionKey ?? string.Empty,
                    PayloadText = saved.payloadText ?? string.Empty,
                    Priority = saved.priority,
                    Urgency = saved.urgency,
                    Confidence = saved.confidence,
                    CreatedAt = saved.createdAt,
                    ExpiresAt = saved.expiresAt,
                    Operational = saved.operational,
                    Version = saved.version
                };
            }
        }

        private void CopyDomains(Dictionary<string, IA01DomainRecord> source, List<SaveIA01DomainData> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, IA01DomainRecord> pair in source)
            {
                IA01DomainRecord record = pair.Value;
                if (record == null)
                {
                    continue;
                }

                destination.Add(new SaveIA01DomainData
                {
                    id = record.Id,
                    nationId = record.NationId,
                    teamId = record.TeamId,
                    kind = record.Kind,
                    state = record.State,
                    target = record.Target,
                    category = record.Category,
                    regionKey = record.RegionKey,
                    payloadText = record.PayloadText,
                    priority = record.Priority,
                    urgency = record.Urgency,
                    confidence = record.Confidence,
                    createdAt = record.CreatedAt,
                    expiresAt = record.ExpiresAt,
                    operational = record.Operational,
                    version = record.Version
                });
            }
        }

        private void CopyResources(List<SaveIA01ResourceData> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, IA01ResourceRecord> pair in resources)
            {
                IA01ResourceRecord record = pair.Value;
                if (record == null)
                {
                    continue;
                }

                destination.Add(new SaveIA01ResourceData
                {
                    resourceId = record.ResourceId,
                    nationId = record.NationId,
                    teamId = record.TeamId,
                    amount = record.Amount,
                    reserved = record.Reserved,
                    capacity = record.Capacity,
                    version = record.Version,
                    lastUpdated = record.LastUpdated,
                    source = record.Source
                });
            }
        }

        private void CopyCaches(List<SaveIA01CacheData> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, IA01CacheEntry> pair in caches)
            {
                IA01CacheEntry entry = pair.Value;
                if (entry == null)
                {
                    continue;
                }

                destination.Add(new SaveIA01CacheData
                {
                    key = entry.Key,
                    version = entry.Version,
                    timestamp = entry.Timestamp,
                    expiration = entry.Expiration,
                    invalidationReason = entry.InvalidationReason,
                    sourceRegion = entry.SourceRegion,
                    dirty = entry.Dirty,
                    valueText = entry.ValueText
                });
            }
        }

        private void RestoreCaches(List<SaveIA01CacheData> savedList)
        {
            caches.Clear();
            if (savedList == null)
            {
                return;
            }

            for (int i = 0; i < savedList.Count; i++)
            {
                SaveIA01CacheData saved = savedList[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.key))
                {
                    continue;
                }

                caches[saved.key] = new IA01CacheEntry
                {
                    Key = saved.key,
                    Version = saved.version,
                    Timestamp = saved.timestamp,
                    Expiration = saved.expiration,
                    InvalidationReason = saved.invalidationReason,
                    SourceRegion = saved.sourceRegion,
                    Dirty = saved.dirty,
                    ValueText = saved.valueText
                };
            }
        }

        private void CopyTimers(List<SaveIA01TimerData> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, IA01TimerEntry> pair in timers)
            {
                IA01TimerEntry timer = pair.Value;
                if (timer == null)
                {
                    continue;
                }

                destination.Add(new SaveIA01TimerData
                {
                    key = timer.Key,
                    intervalSeconds = timer.IntervalSeconds,
                    nextDueAt = timer.NextDueAt,
                    lastFiredAt = timer.LastFiredAt,
                    firedCount = timer.FiredCount,
                    paused = timer.Paused,
                    version = timer.Version
                });
            }
        }

        private void RestoreTimers(List<SaveIA01TimerData> savedList)
        {
            timers.Clear();
            if (savedList == null)
            {
                return;
            }

            for (int i = 0; i < savedList.Count; i++)
            {
                SaveIA01TimerData saved = savedList[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.key))
                {
                    continue;
                }

                timers[saved.key] = new IA01TimerEntry
                {
                    Key = saved.key,
                    IntervalSeconds = saved.intervalSeconds,
                    NextDueAt = saved.nextDueAt,
                    LastFiredAt = saved.lastFiredAt,
                    FiredCount = saved.firedCount,
                    Paused = saved.paused,
                    Version = saved.version
                };
            }
        }

        private void CopyMetrics(List<SaveIA01MetricData> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, double> pair in metrics)
            {
                destination.Add(new SaveIA01MetricData
                {
                    key = pair.Key,
                    value = (float)pair.Value
                });
            }
        }

        private void RestoreMetrics(List<SaveIA01MetricData> savedList)
        {
            metrics.Clear();
            if (savedList == null)
            {
                return;
            }

            for (int i = 0; i < savedList.Count; i++)
            {
                SaveIA01MetricData saved = savedList[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.key))
                {
                    continue;
                }

                metrics[saved.key] = saved.value;
            }
        }
    }
}
