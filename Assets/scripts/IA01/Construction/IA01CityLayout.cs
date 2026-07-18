using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DisallowMultipleComponent]
    public sealed class IA01CityLayout : MonoBehaviour
    {
        [SerializeField] private string layoutId = "ia01.layout";
        [SerializeField] private int layoutVersion = 1;
        [SerializeField] private IA01BuildSlotRegistry slotRegistry;
        [SerializeField] private List<IA01BuildAutonomousZone> autonomousZones = new List<IA01BuildAutonomousZone>();
        [SerializeField] private IA01BuildSlot capitalSlot;

        private readonly List<IA01BuildSlot> registeredSlots = new List<IA01BuildSlot>();
        private int ownerTeamId;
        private int ownerNationId;

        public string LayoutId => string.IsNullOrWhiteSpace(layoutId) ? name : layoutId.Trim();
        public int LayoutVersion => Mathf.Max(1, layoutVersion);
        public IA01BuildSlotRegistry SlotRegistry => slotRegistry;
        public IA01BuildSlot CapitalSlot => capitalSlot;
        public int OwnerTeamId => ownerTeamId;
        public int OwnerNationId => ownerNationId;

        private void Awake()
        {
            EnsureRegistry();
            RegisterChildSlotsOnce();
        }

        private void OnValidate()
        {
            layoutVersion = Mathf.Max(1, layoutVersion);
            if (slotRegistry == null) slotRegistry = GetComponent<IA01BuildSlotRegistry>();
            if (capitalSlot == null)
            {
                IA01BuildSlot[] childSlots = GetComponentsInChildren<IA01BuildSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    IA01BuildSlot candidate = childSlots[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.AllowedRole == IA01StrategicRole.Capital
                        || candidate.AllowedRole == IA01StrategicRole.Government
                        || candidate.AllowedRole == IA01StrategicRole.Command)
                    {
                        capitalSlot = candidate;
                        break;
                    }
                }
            }

            autonomousZones.RemoveAll(zone => zone == null);
            IA01BuildAutonomousZone[] childZones = GetComponentsInChildren<IA01BuildAutonomousZone>(true);
            for (int i = 0; i < childZones.Length; i++)
            {
                IA01BuildAutonomousZone zone = childZones[i];
                if (zone != null && !autonomousZones.Contains(zone))
                {
                    autonomousZones.Add(zone);
                }
            }
        }

        public void ConfigureOwner(int teamId, int nationId)
        {
            ownerTeamId = teamId;
            ownerNationId = nationId;
            for (int i = 0; i < registeredSlots.Count; i++)
            {
                registeredSlots[i]?.ConfigureOwner(teamId, nationId, LayoutVersion);
            }
        }

        public void RegisterSlot(IA01BuildSlot slot)
        {
            if (slot == null) return;
            EnsureRegistry();
            if (!registeredSlots.Contains(slot)) registeredSlots.Add(slot);
            slot.AttachLayout(this);
            slot.ConfigureOwner(ownerTeamId, ownerNationId, LayoutVersion);
            slotRegistry.Register(slot);
        }

        public void UnregisterSlot(IA01BuildSlot slot)
        {
            if (slot == null) return;
            registeredSlots.Remove(slot);
            slotRegistry?.Unregister(slot);
        }

        public void RegisterAutonomousZone(IA01BuildAutonomousZone zone)
        {
            if (zone != null && !autonomousZones.Contains(zone)) autonomousZones.Add(zone);
        }

        public void UnregisterAutonomousZone(IA01BuildAutonomousZone zone)
        {
            if (zone != null) autonomousZones.Remove(zone);
        }

        public bool TryGetSlot(string slotId, out IA01BuildSlot slot)
        {
            slot = null;
            return slotRegistry != null && slotRegistry.TryGetSlot(slotId, out slot);
        }

        public bool TryGetAvailableGroupSlot(string groupId, IA01BuildDefinition definition, out IA01BuildSlot slot, out string reason)
        {
            slot = null;
            if (slotRegistry == null)
            {
                reason = "registry de slots ausente";
                return false;
            }

            return slotRegistry.TryGetAvailableGroupSlot(groupId, definition, ownerTeamId, out slot, out reason);
        }

        public bool TryGetAutonomousZone(string zoneId, out IA01BuildAutonomousZone zone)
        {
            zone = null;
            for (int i = 0; i < autonomousZones.Count; i++)
            {
                IA01BuildAutonomousZone candidate = autonomousZones[i];
                if (candidate != null && string.Equals(candidate.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    zone = candidate;
                    return true;
                }
            }

            return false;
        }

        public List<SaveIA01BuildSlotState> CaptureSlotSaveState()
        {
            List<SaveIA01BuildSlotState> result = new List<SaveIA01BuildSlotState>(registeredSlots.Count);
            for (int i = 0; i < registeredSlots.Count; i++)
            {
                IA01BuildSlot slot = registeredSlots[i];
                if (slot != null) result.Add(slot.CaptureSaveState());
            }

            return result;
        }

        public void RestoreSlotSaveState(IReadOnlyList<SaveIA01BuildSlotState> states)
        {
            if (states == null || slotRegistry == null) return;
            for (int i = 0; i < states.Count; i++)
            {
                SaveIA01BuildSlotState state = states[i];
                if (state != null && slotRegistry.TryGetSlot(state.slotId, out IA01BuildSlot slot)) slot.RestoreSaveState(state);
            }
        }

        private void EnsureRegistry()
        {
            if (slotRegistry == null) slotRegistry = GetComponent<IA01BuildSlotRegistry>();
            if (slotRegistry == null) slotRegistry = gameObject.AddComponent<IA01BuildSlotRegistry>();
        }

        private void RegisterChildSlotsOnce()
        {
            IA01BuildSlot[] childSlots = GetComponentsInChildren<IA01BuildSlot>(true);
            for (int i = 0; i < childSlots.Length; i++) RegisterSlot(childSlots[i]);
            IA01BuildAutonomousZone[] zones = GetComponentsInChildren<IA01BuildAutonomousZone>(true);
            for (int i = 0; i < zones.Length; i++) RegisterAutonomousZone(zones[i]);
        }
    }

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

    [DisallowMultipleComponent]
    public sealed class IA01BuildSlotGroup : MonoBehaviour
    {
        [SerializeField] private string groupId;
        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId.Trim();
    }

    [DisallowMultipleComponent]
    public sealed class IA01BuildAutonomousZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField] private IA01BuildDomain allowedDomain = IA01BuildDomain.Land;
        [SerializeField] private Vector3 localSize = new Vector3(120f, 40f, 120f);

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId.Trim();
        public IA01BuildDomain AllowedDomain => allowedDomain;
        public Bounds WorldBounds => new Bounds(transform.TransformPoint(Vector3.zero), new Vector3(Mathf.Abs(localSize.x * transform.lossyScale.x), Mathf.Abs(localSize.y * transform.lossyScale.y), Mathf.Abs(localSize.z * transform.lossyScale.z)));

        public bool IsCompatible(IA01BuildDefinition definition)
        {
            return definition != null && (definition.Domain == allowedDomain || allowedDomain == IA01BuildDomain.Coastal && definition.Domain == IA01BuildDomain.Water);
        }

        private void OnEnable()
        {
            IA01CityLayout layout = GetComponentInParent<IA01CityLayout>();
            layout?.RegisterAutonomousZone(this);
        }

        private void OnDisable()
        {
            IA01CityLayout layout = GetComponentInParent<IA01CityLayout>();
            layout?.UnregisterAutonomousZone(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, localSize);
        }
    }
}
