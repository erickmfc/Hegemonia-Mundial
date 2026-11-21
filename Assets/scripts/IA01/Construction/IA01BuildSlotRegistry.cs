using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DisallowMultipleComponent]
    public sealed class IA01BuildSlotRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, IA01BuildSlot> byId = new Dictionary<string, IA01BuildSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<IA01BuildSlot>> byGroup = new Dictionary<string, List<IA01BuildSlot>>(StringComparer.OrdinalIgnoreCase);

        public int SlotCount => byId.Count;

        public void Register(IA01BuildSlot slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId)) return;
            byId[slot.SlotId] = slot;
            if (string.IsNullOrWhiteSpace(slot.SlotGroupId)) return;
            if (!byGroup.TryGetValue(slot.SlotGroupId, out List<IA01BuildSlot> slots))
            {
                slots = new List<IA01BuildSlot>();
                byGroup.Add(slot.SlotGroupId, slots);
            }

            if (!slots.Contains(slot)) slots.Add(slot);
        }

        public void Unregister(IA01BuildSlot slot)
        {
            if (slot == null) return;
            if (byId.TryGetValue(slot.SlotId, out IA01BuildSlot indexed) && indexed == slot) byId.Remove(slot.SlotId);
            if (!string.IsNullOrWhiteSpace(slot.SlotGroupId) && byGroup.TryGetValue(slot.SlotGroupId, out List<IA01BuildSlot> slots))
            {
                slots.Remove(slot);
                if (slots.Count == 0) byGroup.Remove(slot.SlotGroupId);
            }
        }

        public bool TryGetSlot(string slotId, out IA01BuildSlot slot)
        {
            slot = null;
            return !string.IsNullOrWhiteSpace(slotId) && byId.TryGetValue(slotId.Trim(), out slot) && slot != null;
        }

        public bool TryGetAvailableGroupSlot(string groupId, IA01BuildDefinition definition, int teamId, out IA01BuildSlot slot, out string reason)
        {
            slot = null;
            reason = "grupo de slots ausente";
            if (string.IsNullOrWhiteSpace(groupId) || !byGroup.TryGetValue(groupId.Trim(), out List<IA01BuildSlot> slots)) return false;
            string lastRejection = string.Empty;
            for (int i = 0; i < slots.Count; i++)
            {
                IA01BuildSlot candidate = slots[i];
                if (candidate == null) continue;
                if (candidate.IsCompatible(definition, teamId, out reason))
                {
                    slot = candidate;
                    reason = string.Empty;
                    return true;
                }

                lastRejection = candidate.SlotId + ": " + reason;
            }

            reason = "nenhum slot compativel no grupo " + groupId
                + (string.IsNullOrWhiteSpace(lastRejection) ? string.Empty : " (" + lastRejection + ")");
            return false;
        }

        public IReadOnlyCollection<IA01BuildSlot> GetAllSlots() => byId.Values;
    }
}
