using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01WorldRegistry
    {
        private readonly Dictionary<string, IA01WorldEntityRecord> entitiesById = new Dictionary<string, IA01WorldEntityRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HashSet<string>> entitiesByNation = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<int, HashSet<string>> entitiesByTeam = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<IA01WorldEntityKind, HashSet<string>> entitiesByKind = new Dictionary<IA01WorldEntityKind, HashSet<string>>();
        private readonly Dictionary<IA01WorldDomain, HashSet<string>> entitiesByDomain = new Dictionary<IA01WorldDomain, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> entitiesByCategory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> entitiesByRegion = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IA01WorldEntityRecord> scratch = new List<IA01WorldEntityRecord>(64);

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

        public int CountByKind(IA01WorldEntityKind kind)
        {
            return entitiesByKind.TryGetValue(kind, out HashSet<string> set) ? set.Count : 0;
        }

        public int CountByDomain(IA01WorldDomain domain)
        {
            return entitiesByDomain.TryGetValue(domain, out HashSet<string> set) ? set.Count : 0;
        }

        public bool Register(IA01WorldEntityRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.EntityId))
            {
                return false;
            }

            IA01WorldEntityRecord clone = record.Clone();
            clone.NativeObject = record.NativeObject;

            if (entitiesById.TryGetValue(clone.EntityId, out IA01WorldEntityRecord existing))
            {
                RemoveFromIndexes(existing);
            }

            entitiesById[clone.EntityId] = clone;
            AddToIndexes(clone);
            Version++;
            LastMutationReason = clone.Source ?? string.Empty;
            return true;
        }

        public bool Update(IA01WorldEntityRecord record)
        {
            return Register(record);
        }

        public bool Remove(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            if (!entitiesById.TryGetValue(entityId, out IA01WorldEntityRecord record))
            {
                return false;
            }

            entitiesById.Remove(entityId);
            RemoveFromIndexes(record);
            Version++;
            LastMutationReason = "remove";
            return true;
        }

        public bool TryGet(string entityId, out IA01WorldEntityRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            if (!entitiesById.TryGetValue(entityId, out IA01WorldEntityRecord stored) || stored == null)
            {
                return false;
            }

            record = stored.Clone();
            record.NativeObject = stored.NativeObject;
            return true;
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByNation(int nationId)
        {
            return CollectByIdSet(entitiesByNation.TryGetValue(nationId, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByTeam(int teamId)
        {
            return CollectByIdSet(entitiesByTeam.TryGetValue(teamId, out HashSet<string> set) ? set : null);
        }

        public int CountStructuresByStrategicRole(int teamId, IA01StrategicRole role)
        {
            if (!entitiesByTeam.TryGetValue(teamId, out HashSet<string> ids) || ids == null)
            {
                return 0;
            }

            int count = 0;
            foreach (string id in ids)
            {
                if (entitiesById.TryGetValue(id, out IA01WorldEntityRecord record)
                    && record != null
                    && record.Kind == IA01WorldEntityKind.Structure
                    && record.StrategicRole == role)
                {
                    count++;
                }
            }

            return count;
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByKind(IA01WorldEntityKind kind)
        {
            return CollectByIdSet(entitiesByKind.TryGetValue(kind, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByDomain(IA01WorldDomain domain)
        {
            return CollectByIdSet(entitiesByDomain.TryGetValue(domain, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByCategory(string category)
        {
            return CollectByIdSet(entitiesByCategory.TryGetValue(category ?? string.Empty, out HashSet<string> set) ? set : null);
        }

        public IReadOnlyList<IA01WorldEntityRecord> GetByRegion(string regionKey)
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

        public IA01WorldEntityRecord CreateRecordFromController(IA01Controller controller)
        {
            if (controller == null)
            {
                return null;
            }

            IA01RuntimeContext context = controller.Context;
            IA01NationIdentity identity = context != null ? context.GetIdentitySnapshot() : null;
            return new IA01WorldEntityRecord
            {
                EntityId = controller.UniqueEntityId,
                InstanceId = controller.InstanceId,
                NationId = identity != null ? identity.NationId : controller.NationId,
                TeamId = identity != null ? identity.TeamId : controller.TeamId,
                DisplayName = identity != null ? identity.NationName : controller.name,
                Kind = IA01WorldEntityKind.Controller,
                Domain = IA01WorldDomain.Command,
                Category = "controller",
                RegionKey = "nation:" + (identity != null ? identity.NationId.ToString() : controller.NationId.ToString()),
                Position = controller.transform != null ? controller.transform.position : Vector3.zero,
                Operational = controller.isActiveAndEnabled,
                Version = Version,
                State = context != null ? context.BuildDebugSummary() : string.Empty,
                Source = "IA01Manager"
            };
        }

        private void AddToIndexes(IA01WorldEntityRecord record)
        {
            AddToIndex(entitiesByNation, record.NationId, record.EntityId);
            AddToIndex(entitiesByTeam, record.TeamId, record.EntityId);
            AddToIndex(entitiesByKind, record.Kind, record.EntityId);
            AddToIndex(entitiesByDomain, record.Domain, record.EntityId);
            AddToIndex(entitiesByCategory, record.Category, record.EntityId);
            AddToIndex(entitiesByRegion, record.RegionKey, record.EntityId);
        }

        private void RemoveFromIndexes(IA01WorldEntityRecord record)
        {
            RemoveFromIndex(entitiesByNation, record.NationId, record.EntityId);
            RemoveFromIndex(entitiesByTeam, record.TeamId, record.EntityId);
            RemoveFromIndex(entitiesByKind, record.Kind, record.EntityId);
            RemoveFromIndex(entitiesByDomain, record.Domain, record.EntityId);
            RemoveFromIndex(entitiesByCategory, record.Category, record.EntityId);
            RemoveFromIndex(entitiesByRegion, record.RegionKey, record.EntityId);
        }

        private IReadOnlyList<IA01WorldEntityRecord> CollectByIdSet(HashSet<string> set)
        {
            scratch.Clear();
            if (set == null || set.Count == 0)
            {
                return scratch;
            }

            foreach (string id in set)
            {
                if (!entitiesById.TryGetValue(id, out IA01WorldEntityRecord record) || record == null)
                {
                    continue;
                }

                IA01WorldEntityRecord clone = record.Clone();
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
