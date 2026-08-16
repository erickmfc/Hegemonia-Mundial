using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.Shared
{
    public enum IAProductionOrderState
    {
        Reserved,
        Queued,
        Constructing,
        Delivered,
        Released
    }

    [Serializable]
    public sealed class IAProductionOrderSaveData
    {
        public string orderId = string.Empty;
        public int teamId;
        public string unitType = string.Empty;
        public string purpose = string.Empty;
        public IAProductionOrderState state;
        public float createdAt;
        public float updatedAt;
        public float expiresAt;
        public int producerInstanceId;
    }

    [Serializable]
    public sealed class IAAutoProductionSaveData
    {
        public int nextSequence = 1;
        public List<IAProductionOrderSaveData> orders = new List<IAProductionOrderSaveData>();
    }

    public struct IAProductionDiagnostics
    {
        public int Alive;
        public int Reserved;
        public int Queued;
        public int Constructing;
        public int Desired;
        public int NetDemand;

        public override string ToString()
        {
            return string.Format("vivos={0} reservados={1} enfileirados={2} em_construcao={3} meta={4} demanda_liquida={5}",
                Alive, Reserved, Queued, Constructing, Desired, NetDemand);
        }
    }

    /// <summary>
    /// Única autoridade para reservas de produção automática. A classe não
    /// cria unidades: ela torna a intenção idempotente e acompanha a unidade
    /// até a entrega, evitando que cada planejador conte o mesmo déficit.
    /// </summary>
    public static class IAAutoProductionRegistry
    {
        private sealed class Order
        {
            public string Id;
            public int TeamId;
            public string UnitType;
            public string Purpose;
            public IAProductionOrderState State;
            public float CreatedAt;
            public float UpdatedAt;
            public float ExpiresAt;
            public int ProducerInstanceId;
        }

        private static readonly Dictionary<string, Order> Orders = new Dictionary<string, Order>();
        private static readonly Dictionary<int, int> NextSequenceByTeam = new Dictionary<int, int>();
        private static int NextSequenceFloor = 1;
        private const float DefaultReservationTtl = 180f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Clear();
        }

        public static bool TryReserveProduction(
            int teamId,
            string unitType,
            string purpose,
            int desired,
            int alive,
            out string orderId,
            float now = -1f,
            float ttlSeconds = DefaultReservationTtl)
        {
            orderId = string.Empty;
            if (teamId <= 0 || desired <= 0 || string.IsNullOrWhiteSpace(unitType))
            {
                return false;
            }

            float currentTime = ResolveNow(now);
            Cleanup(currentTime);
            string typeKey = Normalize(unitType);
            string purposeKey = Normalize(string.IsNullOrWhiteSpace(purpose) ? "default" : purpose);

            IAProductionDiagnostics diagnostics = GetDiagnostics(teamId, typeKey, desired, alive);
            if (diagnostics.NetDemand <= 0)
            {
                return false;
            }

            // Uma mesma finalidade não pode ter duas ordens equivalentes. O
            // déficit de outras finalidades continua independente.
            foreach (Order order in Orders.Values)
            {
                if (order.TeamId == teamId
                    && order.UnitType == typeKey
                    && order.Purpose == purposeKey
                    && IsActive(order.State))
                {
                    return false;
                }
            }

            int sequence = NextSequenceByTeam.TryGetValue(teamId, out int next)
                ? Mathf.Max(1, next)
                : 1;
            sequence = Mathf.Max(sequence, NextSequenceFloor);
            NextSequenceByTeam[teamId] = sequence + 1;
            NextSequenceFloor = sequence + 1;
            orderId = string.Format("ia-prod-{0}-{1}-{2}", teamId, sequence, typeKey);
            Orders[orderId] = new Order
            {
                Id = orderId,
                TeamId = teamId,
                UnitType = typeKey,
                Purpose = purposeKey,
                State = IAProductionOrderState.Reserved,
                CreatedAt = currentTime,
                UpdatedAt = currentTime,
                ExpiresAt = currentTime + Mathf.Max(8f, ttlSeconds),
                ProducerInstanceId = 0
            };
            return true;
        }

        public static bool ConfirmQueued(string orderId, int producerInstanceId = 0, float now = -1f)
        {
            return Transition(orderId, IAProductionOrderState.Queued, producerInstanceId, now);
        }

        public static bool ConfirmConstructionStarted(string orderId, int producerInstanceId = 0, float now = -1f)
        {
            return Transition(orderId, IAProductionOrderState.Constructing, producerInstanceId, now);
        }

        public static bool Complete(string orderId, float now = -1f)
        {
            if (!TryGetActive(orderId, out Order order)) return false;
            order.State = IAProductionOrderState.Delivered;
            order.UpdatedAt = ResolveNow(now);
            return true;
        }

        public static bool Release(string orderId, float now = -1f)
        {
            if (!TryGetActive(orderId, out Order order)) return false;
            order.State = IAProductionOrderState.Released;
            order.UpdatedAt = ResolveNow(now);
            return true;
        }

        public static string FindActiveOrder(int teamId, string unitType, string purpose = "default")
        {
            Cleanup(ResolveNow(-1f));
            string typeKey = Normalize(unitType);
            string purposeKey = Normalize(string.IsNullOrWhiteSpace(purpose) ? "default" : purpose);
            Order found = null;
            foreach (Order order in Orders.Values)
            {
                if (order.TeamId == teamId && order.UnitType == typeKey && order.Purpose == purposeKey && IsActive(order.State)
                    && (found == null || order.CreatedAt > found.CreatedAt))
                {
                    found = order;
                }
            }
            return found != null ? found.Id : string.Empty;
        }

        public static int Count(int teamId, string unitType, IAProductionOrderState state)
        {
            Cleanup(ResolveNow(-1f));
            string typeKey = Normalize(unitType);
            int count = 0;
            foreach (Order order in Orders.Values)
            {
                if (order.TeamId == teamId && order.UnitType == typeKey && order.State == state) count++;
            }
            return count;
        }

        public static IAProductionDiagnostics GetDiagnostics(int teamId, string unitType, int desired, int alive)
        {
            Cleanup(ResolveNow(-1f));
            string typeKey = Normalize(unitType);
            IAProductionDiagnostics result = new IAProductionDiagnostics
            {
                Alive = Mathf.Max(0, alive),
                Desired = Mathf.Max(0, desired)
            };
            foreach (Order order in Orders.Values)
            {
                if (order.TeamId != teamId || order.UnitType != typeKey) continue;
                if (order.State == IAProductionOrderState.Reserved) result.Reserved++;
                else if (order.State == IAProductionOrderState.Queued) result.Queued++;
                else if (order.State == IAProductionOrderState.Constructing) result.Constructing++;
            }
            result.NetDemand = result.Desired - (result.Alive + result.Reserved + result.Queued + result.Constructing);
            return result;
        }

        public static List<IAProductionOrderSaveData> CaptureSaveState()
        {
            Cleanup(ResolveNow(-1f));
            List<IAProductionOrderSaveData> result = new List<IAProductionOrderSaveData>();
            foreach (Order order in Orders.Values)
            {
                if (!IsActive(order.State)) continue;
                result.Add(new IAProductionOrderSaveData
                {
                    orderId = order.Id,
                    teamId = order.TeamId,
                    unitType = order.UnitType,
                    purpose = order.Purpose,
                    state = order.State,
                    createdAt = order.CreatedAt,
                    updatedAt = order.UpdatedAt,
                    expiresAt = order.ExpiresAt,
                    producerInstanceId = order.ProducerInstanceId
                });
            }
            result.Sort((a, b) => string.CompareOrdinal(a.orderId, b.orderId));
            return result;
        }

        public static IAAutoProductionSaveData CaptureSaveData()
        {
            int next = NextSequenceFloor;
            foreach (int value in NextSequenceByTeam.Values) next = Mathf.Max(next, value);
            return new IAAutoProductionSaveData { nextSequence = next, orders = CaptureSaveState() };
        }

        public static void RestoreSaveData(IAAutoProductionSaveData data)
        {
            Clear();
            if (data == null) return;
            NextSequenceFloor = Mathf.Max(1, data.nextSequence);
            if (data.orders == null) data.orders = new List<IAProductionOrderSaveData>();
            for (int i = 0; i < data.orders.Count; i++)
            {
                IAProductionOrderSaveData saved = data.orders[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.orderId) || saved.teamId <= 0 || !IsActive(saved.state)) continue;
                Orders[saved.orderId] = new Order
                {
                    Id = saved.orderId,
                    TeamId = saved.teamId,
                    UnitType = Normalize(saved.unitType),
                    Purpose = Normalize(string.IsNullOrWhiteSpace(saved.purpose) ? "default" : saved.purpose),
                    State = saved.state,
                    CreatedAt = saved.createdAt,
                    UpdatedAt = saved.updatedAt,
                    ExpiresAt = saved.expiresAt,
                    ProducerInstanceId = saved.producerInstanceId
                };
                int sequence = ExtractSequence(saved.orderId);
                int current = NextSequenceByTeam.TryGetValue(saved.teamId, out int previous) ? previous : 1;
                NextSequenceByTeam[saved.teamId] = Mathf.Max(current, sequence + 1);
                NextSequenceFloor = Mathf.Max(NextSequenceFloor, sequence + 1);
            }
        }

        public static void Clear()
        {
            Orders.Clear();
            NextSequenceByTeam.Clear();
            NextSequenceFloor = 1;
        }

        private static bool Transition(string orderId, IAProductionOrderState state, int producerInstanceId, float now)
        {
            if (!TryGetActive(orderId, out Order order)) return false;
            if (state == IAProductionOrderState.Queued && order.State != IAProductionOrderState.Reserved) return false;
            if (state == IAProductionOrderState.Constructing
                && order.State != IAProductionOrderState.Reserved
                && order.State != IAProductionOrderState.Queued) return false;
            order.State = state;
            order.ProducerInstanceId = producerInstanceId;
            order.UpdatedAt = ResolveNow(now);
            order.ExpiresAt = order.UpdatedAt + DefaultReservationTtl;
            return true;
        }

        private static bool TryGetActive(string orderId, out Order order)
        {
            if (!string.IsNullOrWhiteSpace(orderId) && Orders.TryGetValue(orderId, out order) && IsActive(order.State)) return true;
            order = null;
            return false;
        }

        private static void Cleanup(float now)
        {
            List<string> expired = null;
            foreach (KeyValuePair<string, Order> pair in Orders)
            {
                Order order = pair.Value;
                if (IsActive(order.State) && order.ExpiresAt > 0f && order.ExpiresAt <= now)
                {
                    if (expired == null) expired = new List<string>();
                    expired.Add(pair.Key);
                }
            }
            if (expired == null) return;
            for (int i = 0; i < expired.Count; i++) Orders.Remove(expired[i]);
        }

        private static bool IsActive(IAProductionOrderState state)
        {
            return state == IAProductionOrderState.Reserved
                || state == IAProductionOrderState.Queued
                || state == IAProductionOrderState.Constructing;
        }

        private static float ResolveNow(float now)
        {
            return now >= 0f ? now : (Application.isPlaying ? Time.time : 0f);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace(" ", "_");
        }

        private static int ExtractSequence(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            string[] parts = id.Split('-');
            if (parts.Length < 4) return 0;
            return int.TryParse(parts[3], out int sequence) ? sequence : 0;
        }
    }
}
