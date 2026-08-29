using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DisallowMultipleComponent]
    public sealed class IA02CityLayout : MonoBehaviour
    {
        [SerializeField] private string layoutId = "ia02.layout";
        [SerializeField] private int layoutVersion = 1;
        [SerializeField] private IA02BuildSlotRegistry slotRegistry;
        [SerializeField] private List<IA02BuildAutonomousZone> autonomousZones = new List<IA02BuildAutonomousZone>();
        [SerializeField] private IA02BuildSlot capitalSlot;

        private readonly List<IA02BuildSlot> registeredSlots = new List<IA02BuildSlot>();
        private int ownerTeamId;
        private int ownerNationId;

        public string LayoutId => string.IsNullOrWhiteSpace(layoutId) ? name : layoutId.Trim();
        public int LayoutVersion => Mathf.Max(1, layoutVersion);
        public IA02BuildSlotRegistry SlotRegistry => slotRegistry;
        public IA02BuildSlot CapitalSlot => capitalSlot;
        public int OwnerTeamId => ownerTeamId;
        public int OwnerNationId => ownerNationId;
        public int RegisteredSlotCount => registeredSlots.Count;
        public bool IsRuntimeReady => slotRegistry != null && registeredSlots.Count > 0 && slotRegistry.SlotCount > 0;

        private void Awake()
        {
            EnsureRuntimeReady();
        }

        private void OnValidate()
        {
            layoutVersion = Mathf.Max(1, layoutVersion);
            if (slotRegistry == null) slotRegistry = GetComponent<IA02BuildSlotRegistry>();
            if (capitalSlot == null)
            {
                IA02BuildSlot[] childSlots = GetComponentsInChildren<IA02BuildSlot>(true);
                for (int i = 0; i < childSlots.Length; i++)
                {
                    IA02BuildSlot candidate = childSlots[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.AllowedRole == IA02StrategicRole.Capital
                        || candidate.AllowedRole == IA02StrategicRole.Government
                        || candidate.AllowedRole == IA02StrategicRole.Command)
                    {
                        capitalSlot = candidate;
                        break;
                    }
                }
            }

            autonomousZones.RemoveAll(zone => zone == null);
            IA02BuildAutonomousZone[] childZones = GetComponentsInChildren<IA02BuildAutonomousZone>(true);
            for (int i = 0; i < childZones.Length; i++)
            {
                IA02BuildAutonomousZone zone = childZones[i];
                if (zone != null && !autonomousZones.Contains(zone))
                {
                    autonomousZones.Add(zone);
                }
            }
        }

        public void ConfigureOwner(int teamId, int nationId)
        {
            EnsureRuntimeReady();
            ownerTeamId = teamId;
            ownerNationId = nationId;
            for (int i = 0; i < registeredSlots.Count; i++)
            {
                registeredSlots[i]?.ConfigureOwner(teamId, nationId, LayoutVersion);
            }
        }

        public void RegisterSlot(IA02BuildSlot slot)
        {
            if (slot == null) return;
            EnsureRegistry();
            if (!registeredSlots.Contains(slot)) registeredSlots.Add(slot);
            slot.AttachLayout(this);
            slot.ConfigureOwner(ownerTeamId, ownerNationId, LayoutVersion);
            slotRegistry.Register(slot);
        }

        public void UnregisterSlot(IA02BuildSlot slot)
        {
            if (slot == null) return;
            registeredSlots.Remove(slot);
            slotRegistry?.Unregister(slot);
        }

        public void RegisterAutonomousZone(IA02BuildAutonomousZone zone)
        {
            if (zone != null && !autonomousZones.Contains(zone)) autonomousZones.Add(zone);
        }

        public void UnregisterAutonomousZone(IA02BuildAutonomousZone zone)
        {
            if (zone != null) autonomousZones.Remove(zone);
        }

        public bool TryGetSlot(string slotId, out IA02BuildSlot slot)
        {
            slot = null;
            return slotRegistry != null && slotRegistry.TryGetSlot(slotId, out slot);
        }

        public bool TryGetAvailableGroupSlot(string groupId, IA02BuildDefinition definition, out IA02BuildSlot slot, out string reason)
        {
            slot = null;
            if (slotRegistry == null)
            {
                reason = "registry de slots ausente";
                return false;
            }

            return slotRegistry.TryGetAvailableGroupSlot(groupId, definition, ownerTeamId, out slot, out reason);
        }

        public bool TryGetAutonomousZone(string zoneId, out IA02BuildAutonomousZone zone)
        {
            zone = null;
            for (int i = 0; i < autonomousZones.Count; i++)
            {
                IA02BuildAutonomousZone candidate = autonomousZones[i];
                if (candidate != null && string.Equals(candidate.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    zone = candidate;
                    return true;
                }
            }

            return false;
        }

        public List<SaveIA02BuildSlotState> CaptureSlotSaveState()
        {
            List<SaveIA02BuildSlotState> result = new List<SaveIA02BuildSlotState>(registeredSlots.Count);
            for (int i = 0; i < registeredSlots.Count; i++)
            {
                IA02BuildSlot slot = registeredSlots[i];
                if (slot != null) result.Add(slot.CaptureSaveState());
            }

            return result;
        }

        public void RestoreSlotSaveState(IReadOnlyList<SaveIA02BuildSlotState> states)
        {
            if (states == null || slotRegistry == null) return;
            for (int i = 0; i < states.Count; i++)
            {
                SaveIA02BuildSlotState state = states[i];
                if (state != null && slotRegistry.TryGetSlot(state.slotId, out IA02BuildSlot slot)) slot.RestoreSaveState(state);
            }
        }

        /// <summary>
        /// Garante que o registro de slots exista antes de qualquer diretor de
        /// construção tentar planejar. Isso fecha a corrida entre Awake do
        /// controller e Awake deste layout em uma build fria.
        /// </summary>
        public bool EnsureRuntimeReady()
        {
            EnsureRegistry();
            if (!IsRuntimeReady) RegisterChildSlotsOnce();
            return IsRuntimeReady;
        }

        private void EnsureRegistry()
        {
            if (slotRegistry == null) slotRegistry = GetComponent<IA02BuildSlotRegistry>();
            if (slotRegistry == null) slotRegistry = gameObject.AddComponent<IA02BuildSlotRegistry>();
        }

        private void RegisterChildSlotsOnce()
        {
            IA02BuildSlot[] childSlots = GetComponentsInChildren<IA02BuildSlot>(true);
            for (int i = 0; i < childSlots.Length; i++) RegisterSlot(childSlots[i]);
            IA02BuildAutonomousZone[] zones = GetComponentsInChildren<IA02BuildAutonomousZone>(true);
            for (int i = 0; i < zones.Length; i++) RegisterAutonomousZone(zones[i]);
        }
    }

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

    [DisallowMultipleComponent]
    public sealed class IA02BuildSlotGroup : MonoBehaviour
    {
        [SerializeField] private string groupId;
        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId.Trim();
    }

    [DisallowMultipleComponent]
    public sealed class IA02BuildAutonomousZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField] private IA02BuildDomain allowedDomain = IA02BuildDomain.Land;
        [SerializeField] private Vector3 localSize = new Vector3(120f, 40f, 120f);

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId.Trim();
        public IA02BuildDomain AllowedDomain => allowedDomain;
        public Bounds WorldBounds => new Bounds(transform.TransformPoint(Vector3.zero), new Vector3(Mathf.Abs(localSize.x * transform.lossyScale.x), Mathf.Abs(localSize.y * transform.lossyScale.y), Mathf.Abs(localSize.z * transform.lossyScale.z)));

        public bool IsCompatible(IA02BuildDefinition definition)
        {
            return definition != null && (definition.Domain == allowedDomain || allowedDomain == IA02BuildDomain.Coastal && definition.Domain == IA02BuildDomain.Water);
        }

        private void OnEnable()
        {
            IA02CityLayout layout = GetComponentInParent<IA02CityLayout>();
            layout?.RegisterAutonomousZone(this);
        }

        private void OnDisable()
        {
            IA02CityLayout layout = GetComponentInParent<IA02CityLayout>();
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
