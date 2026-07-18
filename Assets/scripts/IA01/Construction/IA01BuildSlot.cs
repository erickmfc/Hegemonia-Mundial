using System;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DisallowMultipleComponent]
    public sealed class IA01BuildSlot : MonoBehaviour
    {
        [SerializeField] private string slotId;
        [SerializeField] private string slotGroupId;
        [SerializeField] private IA01StrategicRole allowedRole = IA01StrategicRole.None;
        [SerializeField] private IA01BuildDomain allowedDomain = IA01BuildDomain.Land;
        [SerializeField] private bool required;
        [SerializeField] private bool exactPosition = true;
        [SerializeField] private bool allowAlternativeSlot = true;
        [SerializeField] private Transform buildingPoint;
        [SerializeField] private Transform unitSpawnPoint;
        [SerializeField] private Transform exitDirection;
        [SerializeField] private Vector2 reservedFootprint = new Vector2(12f, 12f);
        [SerializeField] private float safetyMargin = 1f;
        [SerializeField] private int ownerTeamId;
        [SerializeField] private IA01BuildSlotState state = IA01BuildSlotState.Available;
        [SerializeField] private string reservedCommandId = string.Empty;
        [SerializeField] private int ownerNationId;
        [SerializeField] private string constructedItemId = string.Empty;
        [SerializeField] private float reservedAt;
        [SerializeField] private string blockReason = string.Empty;
        [SerializeField] private int layoutVersion = 1;

        private IA01CityLayout layout;
        private string inferredGroupId = string.Empty;

        public string SlotId => string.IsNullOrWhiteSpace(slotId) ? name : slotId.Trim();
        public string SlotGroupId => !string.IsNullOrWhiteSpace(slotGroupId) ? slotGroupId.Trim() : inferredGroupId;
        public IA01StrategicRole AllowedRole => allowedRole;
        public IA01BuildDomain AllowedDomain => allowedDomain;
        public bool Required => required;
        public bool ExactPosition => exactPosition;
        public bool AllowAlternativeSlot => allowAlternativeSlot;
        public Transform BuildingPoint => buildingPoint != null ? buildingPoint : transform;
        public Transform UnitSpawnPoint => unitSpawnPoint;
        public Transform ExitDirection => exitDirection;
        public Vector2 ReservedFootprint => reservedFootprint;
        public float SafetyMargin => Mathf.Max(0f, safetyMargin);
        public int OwnerTeamId => ownerTeamId;
        public IA01BuildSlotState State => state;
        public string ReservedCommandId => reservedCommandId;
        public string ConstructedItemId => constructedItemId;
        public string BlockReason => blockReason;
        public int LayoutVersion => Mathf.Max(1, layoutVersion);

        private void OnEnable()
        {
            ResolveLayout();
            layout?.RegisterSlot(this);
        }

        private void OnDisable()
        {
            layout?.UnregisterSlot(this);
        }

        private void OnValidate()
        {
            reservedFootprint.x = Mathf.Max(0.1f, reservedFootprint.x);
            reservedFootprint.y = Mathf.Max(0.1f, reservedFootprint.y);
            layoutVersion = Mathf.Max(1, layoutVersion);
        }

        internal void AttachLayout(IA01CityLayout value)
        {
            layout = value;
        }

        internal void ConfigureOwner(int teamId, int nationId, int version)
        {
            ownerTeamId = teamId;
            ownerNationId = nationId;
            layoutVersion = Mathf.Max(1, version);
        }

        public bool IsCompatible(IA01BuildDefinition definition, int teamId, out string reason)
        {
            if (definition == null)
            {
                reason = "definicao de construcao ausente";
                return false;
            }

            if (ownerTeamId > 0 && teamId > 0 && ownerTeamId != teamId)
            {
                reason = "slot pertence a outro time";
                return false;
            }

            if (state != IA01BuildSlotState.Available)
            {
                reason = "slot em estado " + state
                    + (string.IsNullOrWhiteSpace(blockReason) ? string.Empty : ": " + blockReason);
                return false;
            }

            if (allowedRole != IA01StrategicRole.None
                && definition.StrategicRole != allowedRole
                && !(allowedRole == IA01StrategicRole.Capital && definition.Archetype == IA01BuildArchetype.Command))
            {
                reason = "papel estrategico incompativel";
                return false;
            }

            if (allowedDomain != definition.Domain
                && !(allowedDomain == IA01BuildDomain.Coastal && definition.Domain == IA01BuildDomain.Water))
            {
                reason = "dominio incompativel";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public IA01BuildLot CreateLot(IA01BuildDefinition definition)
        {
            Transform point = BuildingPoint;
            Vector2 footprint = definition != null ? definition.Footprint : reservedFootprint;
            footprint.x = Mathf.Max(footprint.x, reservedFootprint.x + SafetyMargin * 2f);
            footprint.y = Mathf.Max(footprint.y, reservedFootprint.y + SafetyMargin * 2f);
            return new IA01BuildLot
            {
                Position = point.position,
                Rotation = point.rotation,
                Footprint = footprint,
                Key = "slot:" + SlotId,
                State = IA01LotState.Free
            };
        }

        public bool TryReserve(string commandId, int nationId, string itemId, float now, out string reason)
        {
            if (state != IA01BuildSlotState.Available)
            {
                reason = "slot em estado " + state;
                return false;
            }

            state = IA01BuildSlotState.Reserved;
            reservedCommandId = commandId ?? string.Empty;
            ownerNationId = nationId;
            constructedItemId = itemId ?? string.Empty;
            reservedAt = now;
            blockReason = string.Empty;
            reason = string.Empty;
            return true;
        }

        public void MarkUnderConstruction(string commandId)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.Equals(commandId, reservedCommandId, StringComparison.OrdinalIgnoreCase)) return;
            state = IA01BuildSlotState.UnderConstruction;
        }

        public void MarkOccupied(string commandId, string itemId)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.Equals(commandId, reservedCommandId, StringComparison.OrdinalIgnoreCase)) return;
            state = IA01BuildSlotState.Occupied;
            constructedItemId = itemId ?? constructedItemId;
            reservedCommandId = string.Empty;
            blockReason = string.Empty;
        }

        public void Release(string commandId, bool invalid, string reason)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.Equals(commandId, reservedCommandId, StringComparison.OrdinalIgnoreCase)) return;
            state = invalid ? IA01BuildSlotState.Invalid : IA01BuildSlotState.Available;
            reservedCommandId = string.Empty;
            reservedAt = 0f;
            blockReason = reason ?? string.Empty;
            if (!invalid) constructedItemId = string.Empty;
        }

        public void MarkBlocked(string reason)
        {
            state = IA01BuildSlotState.Blocked;
            reservedCommandId = string.Empty;
            blockReason = reason ?? string.Empty;
        }

        public SaveIA01BuildSlotState CaptureSaveState()
        {
            return new SaveIA01BuildSlotState
            {
                slotId = SlotId,
                state = state,
                commandId = reservedCommandId,
                ownerNationId = ownerNationId,
                constructionId = constructedItemId,
                reservedAt = reservedAt,
                blockReason = blockReason,
                layoutVersion = LayoutVersion
            };
        }

        public void RestoreSaveState(SaveIA01BuildSlotState saved)
        {
            if (saved == null || !string.Equals(saved.slotId, SlotId, StringComparison.OrdinalIgnoreCase)) return;
            state = saved.state;
            reservedCommandId = saved.commandId ?? string.Empty;
            ownerNationId = saved.ownerNationId;
            constructedItemId = saved.constructionId ?? string.Empty;
            reservedAt = saved.reservedAt;
            blockReason = saved.blockReason ?? string.Empty;
            layoutVersion = Mathf.Max(layoutVersion, saved.layoutVersion);
        }

        private void ResolveLayout()
        {
            if (layout == null) layout = GetComponentInParent<IA01CityLayout>();
            if (string.IsNullOrWhiteSpace(inferredGroupId))
            {
                IA01BuildSlotGroup group = GetComponentInParent<IA01BuildSlotGroup>();
                inferredGroupId = group != null ? group.GroupId : string.Empty;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform point = BuildingPoint;
            Gizmos.color = state == IA01BuildSlotState.Available ? Color.green
                : state == IA01BuildSlotState.Reserved ? Color.blue
                : state == IA01BuildSlotState.Occupied ? Color.yellow : Color.red;
            Gizmos.matrix = Matrix4x4.TRS(point.position, point.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(reservedFootprint.x + SafetyMargin * 2f, 1f, reservedFootprint.y + SafetyMargin * 2f));
        }
    }
}
