using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DisallowMultipleComponent]
    public sealed class IA02BuildSlotRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, IA02BuildSlot> byId = new Dictionary<string, IA02BuildSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<IA02BuildSlot>> byGroup = new Dictionary<string, List<IA02BuildSlot>>(StringComparer.OrdinalIgnoreCase);

        public int SlotCount => byId.Count;

        public void Register(IA02BuildSlot slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId)) return;
            byId[slot.SlotId] = slot;
            if (string.IsNullOrWhiteSpace(slot.SlotGroupId)) return;
            if (!byGroup.TryGetValue(slot.SlotGroupId, out List<IA02BuildSlot> slots))
            {
                slots = new List<IA02BuildSlot>();
                byGroup.Add(slot.SlotGroupId, slots);
            }

            if (!slots.Contains(slot)) slots.Add(slot);
        }

        public void Unregister(IA02BuildSlot slot)
        {
            if (slot == null) return;
            if (byId.TryGetValue(slot.SlotId, out IA02BuildSlot indexed) && indexed == slot) byId.Remove(slot.SlotId);
            if (!string.IsNullOrWhiteSpace(slot.SlotGroupId) && byGroup.TryGetValue(slot.SlotGroupId, out List<IA02BuildSlot> slots))
            {
                slots.Remove(slot);
                if (slots.Count == 0) byGroup.Remove(slot.SlotGroupId);
            }
        }

        public bool TryGetSlot(string slotId, out IA02BuildSlot slot)
        {
            slot = null;
            return !string.IsNullOrWhiteSpace(slotId) && byId.TryGetValue(slotId.Trim(), out slot) && slot != null;
        }

        public bool TryGetAvailableGroupSlot(string groupId, IA02BuildDefinition definition, int teamId, out IA02BuildSlot slot, out string reason)
        {
            slot = null;
            reason = "grupo de slots ausente";
            if (string.IsNullOrWhiteSpace(groupId) || !byGroup.TryGetValue(groupId.Trim(), out List<IA02BuildSlot> slots)) return false;
            string lastRejection = string.Empty;
            for (int i = 0; i < slots.Count; i++)
            {
                IA02BuildSlot candidate = slots[i];
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

        public IReadOnlyCollection<IA02BuildSlot> GetAllSlots() => byId.Values;
    }
}
