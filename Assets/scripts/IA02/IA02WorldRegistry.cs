using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    public sealed class IA02WorldRegistry
    {
        private readonly Dictionary<string, IA02WorldEntityRecord> entitiesById = new Dictionary<string, IA02WorldEntityRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HashSet<string>> entitiesByNation = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<int, HashSet<string>> entitiesByTeam = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<IA02WorldEntityKind, HashSet<string>> entitiesByKind = new Dictionary<IA02WorldEntityKind, HashSet<string>>();
        private readonly Dictionary<IA02WorldDomain, HashSet<string>> entitiesByDomain = new Dictionary<IA02WorldDomain, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> entitiesByCategory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> entitiesByRegion = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IA02WorldEntityRecord> scratch = new List<IA02WorldEntityRecord>(64);

        public int Version { get; private set; } = 1;
        public int Count => entitiesById.Count;
        public string LastMutationReason { get; private set; } = string.Empty;

        public int CountByNation(int nationId)
        {
            return entitiesByNation.TryGetValue(nationId, out HashSet<string> set) ? set.Count : 0;
        }

        public int CountByTeam(int teamId)
        {
            return entitiesByTeam.TryGetValue(teamId, out HashSet<string> set) ? set.Count : 0;
        }

        public int CountByKind(IA02WorldEntityKind kind)
        {
            return entitiesByKind.TryGetValue(kind, out HashSet<string> set) ? set.Count : 0;
        }

        public int CountByDomain(IA02WorldDomain domain)
        {
            return entitiesByDomain.TryGetValue(domain, out HashSet<string> set) ? set.Count : 0;
        }

        public bool Register(IA02WorldEntityRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.EntityId))
            {
                return false;
            }

            IA02WorldEntityRecord clone = record.Clone();
            clone.NativeObject = record.NativeObject;

            if (entitiesById.TryGetValue(clone.EntityId, out IA02WorldEntityRecord existing))
            {
                RemoveFromIndexes(existing);
            }

            entitiesById[clone.EntityId] = clone;
            AddToIndexes(clone);
            Version++;
            LastMutationReason = clone.Source ?? string.Empty;
            return true;
        }

        public bool Update(IA02WorldEntityRecord record)
        {
            return Register(record);
        }

        public bool Remove(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            if (!entitiesById.TryGetValue(entityId, out IA02WorldEntityRecord record))
            {
                return false;
            }

            entitiesById.Remove(entityId);
            RemoveFromIndexes(record);
            Version++;
            LastMutationReason = "remove";
            return true;
        }

        public bool TryGet(string entityId, out IA02WorldEntityRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            if (!entitiesById.TryGetValue(entityId, out IA02WorldEntityRecord stored) || stored == null)
            {
                return false;
            }

            record = stored.Clone();
            record.NativeObject = stored.NativeObject;
            return true;
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByNation(int nationId)
        {
            return CollectByIdSet(entitiesByNation.TryGetValue(nationId, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByTeam(int teamId)
        {
            return CollectByIdSet(entitiesByTeam.TryGetValue(teamId, out HashSet<string> set) ? set : null);
        }

        public int CountStructuresByStrategicRole(int teamId, IA02StrategicRole role)
        {
            if (!entitiesByTeam.TryGetValue(teamId, out HashSet<string> ids) || ids == null)
            {
                return 0;
            }

            int count = 0;
            foreach (string id in ids)
            {
                if (entitiesById.TryGetValue(id, out IA02WorldEntityRecord record)
                    && record != null
                    && record.Kind == IA02WorldEntityKind.Structure
                    && record.StrategicRole == role)
                {
                    count++;
                }
            }

            return count;
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByKind(IA02WorldEntityKind kind)
        {
            return CollectByIdSet(entitiesByKind.TryGetValue(kind, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByDomain(IA02WorldDomain domain)
        {
            return CollectByIdSet(entitiesByDomain.TryGetValue(domain, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByCategory(string category)
        {
            return CollectByIdSet(entitiesByCategory.TryGetValue(category ?? string.Empty, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA02WorldEntityRecord> GetByRegion(string regionKey)
        {
            return CollectByIdSet(entitiesByRegion.TryGetValue(regionKey ?? string.Empty, out HashSet<string> set) ? set : null);
        }

        public void Clear()
        {
            entitiesById.Clear();
            entitiesByNation.Clear();
            entitiesByTeam.Clear();
            entitiesByKind.Clear();
            entitiesByDomain.Clear();
            entitiesByCategory.Clear();
            entitiesByRegion.Clear();
            scratch.Clear();
            Version++;
            LastMutationReason = "clear";
        }

        public IA02WorldEntityRecord CreateRecordFromController(IA02Controller controller)
        {
            if (controller == null)
            {
                return null;
            }

            IA02RuntimeContext context = controller.Context;
            IA02NationIdentity identity = context != null ? context.GetIdentitySnapshot() : null;
            return new IA02WorldEntityRecord
            {
                EntityId = controller.UniqueEntityId,
                InstanceId = controller.InstanceId,
                NationId = identity != null ? identity.NationId : controller.NationId,
                TeamId = identity != null ? identity.TeamId : controller.TeamId,
                DisplayName = identity != null ? identity.NationName : controller.name,
                Kind = IA02WorldEntityKind.Controller,
                Domain = IA02WorldDomain.Command,
                Category = "controller",
                RegionKey = "nation:" + (identity != null ? identity.NationId.ToString() : controller.NationId.ToString()),
                Position = controller.transform != null ? controller.transform.position : Vector3.zero,
                Operational = controller.isActiveAndEnabled,
                Version = Version,
                State = context != null ? context.BuildDebugSummary() : string.Empty,
                Source = "IA02Manager"
            };
        }

        private void AddToIndexes(IA02WorldEntityRecord record)
        {
            AddToIndex(entitiesByNation, record.NationId, record.EntityId);
            AddToIndex(entitiesByTeam, record.TeamId, record.EntityId);
            AddToIndex(entitiesByKind, record.Kind, record.EntityId);
            AddToIndex(entitiesByDomain, record.Domain, record.EntityId);
            AddToIndex(entitiesByCategory, record.Category, record.EntityId);
            AddToIndex(entitiesByRegion, record.RegionKey, record.EntityId);
        }

        private void RemoveFromIndexes(IA02WorldEntityRecord record)
        {
            RemoveFromIndex(entitiesByNation, record.NationId, record.EntityId);
            RemoveFromIndex(entitiesByTeam, record.TeamId, record.EntityId);
            RemoveFromIndex(entitiesByKind, record.Kind, record.EntityId);
            RemoveFromIndex(entitiesByDomain, record.Domain, record.EntityId);
            RemoveFromIndex(entitiesByCategory, record.Category, record.EntityId);
            RemoveFromIndex(entitiesByRegion, record.RegionKey, record.EntityId);
        }

        private IReadOnlyList<IA02WorldEntityRecord> CollectByIdSet(HashSet<string> set)
        {
            scratch.Clear();
            if (set == null || set.Count == 0)
            {
                return scratch;
            }

            foreach (string id in set)
            {
                if (!entitiesById.TryGetValue(id, out IA02WorldEntityRecord record) || record == null)
                {
                    continue;
                }

                IA02WorldEntityRecord clone = record.Clone();
                clone.NativeObject = record.NativeObject;
                scratch.Add(clone);
            }

            return scratch;
        }

        private static void AddToIndex<TKey>(Dictionary<TKey, HashSet<string>> index, TKey key, string entityId)
        {
            if (index == null || entityId == null)
            {
                return;
            }

            if (!index.TryGetValue(key, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[key] = set;
            }

            set.Add(entityId);
        }

        private static void RemoveFromIndex<TKey>(Dictionary<TKey, HashSet<string>> index, TKey key, string entityId)
        {
            if (index == null || entityId == null)
            {
                return;
            }

            if (!index.TryGetValue(key, out HashSet<string> set))
            {
                return;
            }

            set.Remove(entityId);
            if (set.Count == 0)
            {
                index.Remove(key);
            }
        }
    }
}
