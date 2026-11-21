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
        public int RegisteredSlotCount => registeredSlots.Count;
        public bool IsRuntimeReady => slotRegistry != null && registeredSlots.Count > 0 && slotRegistry.SlotCount > 0;

        private void Awake()
        {
            EnsureRuntimeReady();
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
            EnsureRuntimeReady();
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
            if (slotRegistry != null)
            {
                slotRegistry.Register(slot);
            }
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
}
