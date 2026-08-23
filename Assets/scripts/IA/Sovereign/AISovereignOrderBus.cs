using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    public sealed class AISovereignOrderBus
    {
        public sealed class Order
        {
            public AISovereignOrderType Type;
            public AISovereignCatalogRole Role;
            public AICombatPackage Package;
            public RecursoMercado Resource;
            public TipoPropostaInternacional ProposalType;
            public int CounterpartyTeamId;
            public int Quantity;
            public int UnitPrice;
            public int Priority;
            public float CooldownSeconds;
            public float EnqueuedAt;
            public string DedupKey = string.Empty;
            public string Reason = string.Empty;
            public Vector3 Anchor;
        }

        private readonly List<Order> _pending = new List<Order>(64);
        private readonly HashSet<string> _dedupInQueue = new HashSet<string>();
        private readonly Dictionary<string, float> _cooldownUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _failureStreak = new Dictionary<string, int>();

        public int PendingCount => _pending.Count;

        public bool Enqueue(Order order, float now)
        {
            if (order == null)
            {
                return false;
            }

            order.EnqueuedAt = now;
            order.DedupKey = string.IsNullOrWhiteSpace(order.DedupKey) ? order.Type + ":" + order.Role : order.DedupKey.Trim();

            if (!string.IsNullOrEmpty(order.DedupKey))
            {
                if (_dedupInQueue.Contains(order.DedupKey))
                {
                    return false;
                }

                if (_cooldownUntil.TryGetValue(order.DedupKey, out float cooldownUntil) && cooldownUntil > now)
                {
                    return false;
                }
            }

            _pending.Add(order);
            _pending.Sort(CompareOrders);
            if (!string.IsNullOrEmpty(order.DedupKey))
            {
                _dedupInQueue.Add(order.DedupKey);
            }
            return true;
        }

        public bool TryDequeue(float now, out Order order)
        {
            CleanupCooldown(now);
            if (_pending.Count == 0)
            {
                order = null;
                return false;
            }

            order = _pending[0];
            _pending.RemoveAt(0);
            if (!string.IsNullOrEmpty(order.DedupKey))
            {
                _dedupInQueue.Remove(order.DedupKey);
            }
            return true;
        }

        public void Complete(Order order, bool success, float now)
        {
            Complete(order, success, now, string.Empty);
        }

        public void Complete(Order order, bool success, float now, string reason)
        {
            if (order == null || string.IsNullOrEmpty(order.DedupKey))
            {
                return;
            }

            if (success)
            {
                _failureStreak.Remove(order.DedupKey);
                if (order.CooldownSeconds > 0f)
                {
                    _cooldownUntil[order.DedupKey] = now + Mathf.Max(0.1f, order.CooldownSeconds);
                }
            }
            else if (!success)
            {
                _failureStreak.TryGetValue(order.DedupKey, out int streak);
                streak = Mathf.Min(8, streak + 1);
                _failureStreak[order.DedupKey] = streak;

                float baseCooldown = Mathf.Max(8f, order.CooldownSeconds);
                if (!string.IsNullOrEmpty(reason)
                    && reason.IndexOf("territorio", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseCooldown = Mathf.Max(baseCooldown, 12f);
                }

                float retryCooldown = baseCooldown * Mathf.Pow(2f, Mathf.Min(3, streak - 1));
                _cooldownUntil[order.DedupKey] = now + Mathf.Min(60f, retryCooldown);
            }
        }

        private void CleanupCooldown(float now)
        {
            if (_cooldownUntil.Count == 0)
            {
                return;
            }

            List<string> expired = null;
            foreach (KeyValuePair<string, float> pair in _cooldownUntil)
            {
                if (pair.Value <= now)
                {
                    if (expired == null)
                    {
                        expired = new List<string>();
                    }
                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            for (int i = 0; i < expired.Count; i++)
            {
                _cooldownUntil.Remove(expired[i]);
            }
        }

        private static int CompareOrders(Order a, Order b)
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return a.EnqueuedAt.CompareTo(b.EnqueuedAt);
        }
    }
}
