using System;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DisallowMultipleComponent]
    public sealed class IA02BuildSlot : MonoBehaviour
    {
        [SerializeField] private string slotId;
        [SerializeField] private string slotGroupId;
        [SerializeField] private IA02StrategicRole allowedRole = IA02StrategicRole.None;
        [SerializeField] private IA02BuildDomain allowedDomain = IA02BuildDomain.Land;
        [SerializeField] private bool required;
        [SerializeField] private bool exactPosition = true;
        [SerializeField] private bool allowAlternativeSlot = true;
        [SerializeField] private Transform buildingPoint;
        [SerializeField] private Transform unitSpawnPoint;
        [SerializeField] private Transform exitDirection;
        [SerializeField] private Vector2 reservedFootprint = new Vector2(12f, 12f);
        [SerializeField] private float safetyMargin = 1f;
        [SerializeField] private int ownerTeamId;
        [SerializeField] private IA02BuildSlotState state = IA02BuildSlotState.Available;
        [SerializeField] private string reservedCommandId = string.Empty;
        [SerializeField] private int ownerNationId;
        [SerializeField] private string constructedItemId = string.Empty;
        [SerializeField] private float reservedAt;
        [SerializeField] private string blockReason = string.Empty;
        [SerializeField] private int layoutVersion = 1;

        private IA02CityLayout layout;
        private string inferredGroupId = string.Empty;

        public string SlotId => string.IsNullOrWhiteSpace(slotId) ? name : slotId.Trim();
        public string SlotGroupId => !string.IsNullOrWhiteSpace(slotGroupId) ? slotGroupId.Trim() : inferredGroupId;
        public IA02StrategicRole AllowedRole => allowedRole;
        public IA02BuildDomain AllowedDomain => allowedDomain;
        public bool Required => required;
        public bool ExactPosition => exactPosition;
        public bool AllowAlternativeSlot => allowAlternativeSlot;
        public Transform BuildingPoint => buildingPoint != null ? buildingPoint : transform;
        public Transform UnitSpawnPoint => unitSpawnPoint;
        public Transform ExitDirection => exitDirection;
        public Vector2 ReservedFootprint => reservedFootprint;
        public float SafetyMargin => Mathf.Max(0f, safetyMargin);
        public int OwnerTeamId => ownerTeamId;
        public IA02BuildSlotState State => state;
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

        internal void AttachLayout(IA02CityLayout value)
        {
            layout = value;
        }

        internal void ConfigureOwner(int teamId, int nationId, int version)
        {
            ownerTeamId = teamId;
            ownerNationId = nationId;
            layoutVersion = Mathf.Max(1, version);
        }

        public bool IsCompatible(IA02BuildDefinition definition, int teamId, out string reason)
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

            if (state != IA02BuildSlotState.Available)
            {
                reason = "slot em estado " + state
                    + (string.IsNullOrWhiteSpace(blockReason) ? string.Empty : ": " + blockReason);
                return false;
            }

            if (allowedRole != IA02StrategicRole.None
                && definition.StrategicRole != allowedRole
                && !(allowedRole == IA02StrategicRole.Capital && definition.Archetype == IA02BuildArchetype.Command))
            {
                reason = "papel estrategico incompativel";
                return false;
            }

            if (allowedDomain != definition.Domain
                && !(allowedDomain == IA02BuildDomain.Coastal && definition.Domain == IA02BuildDomain.Water))
            {
                reason = "dominio incompativel";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public IA02BuildLot CreateLot(IA02BuildDefinition definition)
        {
            Transform point = BuildingPoint;
            Vector2 footprint = definition != null ? definition.Footprint : reservedFootprint;
            footprint.x = Mathf.Max(footprint.x, reservedFootprint.x + SafetyMargin * 2f);
            footprint.y = Mathf.Max(footprint.y, reservedFootprint.y + SafetyMargin * 2f);
            return new IA02BuildLot
            {
                Position = point.position,
                Rotation = point.rotation,
                Footprint = footprint,
                Key = "slot:" + SlotId,
                State = IA02LotState.Free
            };
        }

        public bool TryReserve(string commandId, int nationId, string itemId, float now, out string reason)
        {
            if (state != IA02BuildSlotState.Available)
            {
                reason = "slot em estado " + state;
                return false;
            }

            state = IA02BuildSlotState.Reserved;
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
            state = IA02BuildSlotState.UnderConstruction;
        }

        public void MarkOccupied(string commandId, string itemId)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.Equals(commandId, reservedCommandId, StringComparison.OrdinalIgnoreCase)) return;
            state = IA02BuildSlotState.Occupied;
            constructedItemId = itemId ?? constructedItemId;
            reservedCommandId = string.Empty;
            blockReason = string.Empty;
        }

        public void Release(string commandId, bool invalid, string reason)
        {
            if (!string.IsNullOrEmpty(commandId) && !string.Equals(commandId, reservedCommandId, StringComparison.OrdinalIgnoreCase)) return;
            state = invalid ? IA02BuildSlotState.Invalid : IA02BuildSlotState.Available;
            reservedCommandId = string.Empty;
            reservedAt = 0f;
            blockReason = reason ?? string.Empty;
            if (!invalid) constructedItemId = string.Empty;
        }

        public void MarkBlocked(string reason)
        {
            state = IA02BuildSlotState.Blocked;
            reservedCommandId = string.Empty;
            blockReason = reason ?? string.Empty;
        }

        public SaveIA02BuildSlotState CaptureSaveState()
        {
            return new SaveIA02BuildSlotState
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

        public void RestoreSaveState(SaveIA02BuildSlotState saved)
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
            if (layout == null) layout = GetComponentInParent<IA02CityLayout>();
            if (string.IsNullOrWhiteSpace(inferredGroupId))
            {
                IA02BuildSlotGroup group = GetComponentInParent<IA02BuildSlotGroup>();
                inferredGroupId = group != null ? group.GroupId : string.Empty;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform point = BuildingPoint;
            Gizmos.color = state == IA02BuildSlotState.Available ? Color.green
                : state == IA02BuildSlotState.Reserved ? Color.blue
                : state == IA02BuildSlotState.Occupied ? Color.yellow : Color.red;
            Gizmos.matrix = Matrix4x4.TRS(point.position, point.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(reservedFootprint.x + SafetyMargin * 2f, 1f, reservedFootprint.y + SafetyMargin * 2f));
        }
    }
}
