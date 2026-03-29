using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_CommandQueue
    {
        private readonly List<IA_CommandRequest> _pending = new List<IA_CommandRequest>();
        private readonly Dictionary<string, float> _cooldownUntilByDedup = new Dictionary<string, float>();
        private readonly HashSet<string> _dedupInQueue = new HashSet<string>();
        private readonly Dictionary<string, IA_CommandStatus> _statusById = new Dictionary<string, IA_CommandStatus>();
        private readonly Queue<IA_CommandRecord> _history = new Queue<IA_CommandRecord>();

        public int PendingCount
        {
            get { return _pending.Count; }
        }

        public int CooldownCount
        {
            get { return _cooldownUntilByDedup.Count; }
        }

        public bool Enqueue(IA_CommandRequest request, float now, out string reason)
        {
            reason = string.Empty;
            if (request == null)
            {
                reason = "request nula";
                return false;
            }

            request.Id = string.IsNullOrEmpty(request.Id) ? System.Guid.NewGuid().ToString("N") : request.Id;
            request.DedupKey = IA_Text.Normalize(request.DedupKey);
            request.EnqueueTime = now;

            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                if (_dedupInQueue.Contains(request.DedupKey))
                {
                    reason = "duplicada em fila";
                    return false;
                }

                float cooldownUntil;
                if (_cooldownUntilByDedup.TryGetValue(request.DedupKey, out cooldownUntil) && cooldownUntil > now)
                {
                    reason = "em cooldown";
                    return false;
                }
            }

            _pending.Add(request);
            _pending.Sort(CompareRequests);

            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                _dedupInQueue.Add(request.DedupKey);
            }

            _statusById[request.Id] = IA_CommandStatus.Queued;
            PushHistory(request, IA_CommandStatus.Queued, now, "enfileirado");
            return true;
        }

        public bool TryDequeue(float now, out IA_CommandRequest request)
        {
            CleanupCooldowns(now);
            if (_pending.Count == 0)
            {
                request = null;
                return false;
            }

            request = _pending[0];
            _pending.RemoveAt(0);

            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                _dedupInQueue.Remove(request.DedupKey);
            }

            _statusById[request.Id] = IA_CommandStatus.Running;
            PushHistory(request, IA_CommandStatus.Running, now, "executando");
            return true;
        }

        public void Complete(IA_CommandRequest request, bool success, float now, string message)
        {
            if (request == null)
            {
                return;
            }

            IA_CommandStatus finalStatus = success ? IA_CommandStatus.Success : IA_CommandStatus.Failed;
            _statusById[request.Id] = finalStatus;
            PushHistory(request, finalStatus, now, message);

            if (!string.IsNullOrEmpty(request.DedupKey) && request.CooldownSeconds > 0f)
            {
                _cooldownUntilByDedup[request.DedupKey] = now + Mathf.Max(0.1f, request.CooldownSeconds);
                PushHistory(request, IA_CommandStatus.CoolingDown, now, "cooldown");
            }
        }

        public IA_CommandStatus GetStatus(string id)
        {
            IA_CommandStatus status;
            if (_statusById.TryGetValue(id, out status))
            {
                return status;
            }

            return IA_CommandStatus.Cancelled;
        }

        public List<IA_CommandRecord> GetRecentHistory(int maxItems)
        {
            return _history.Reverse().Take(Mathf.Max(1, maxItems)).ToList();
        }

        private void CleanupCooldowns(float now)
        {
            if (_cooldownUntilByDedup.Count == 0)
            {
                return;
            }

            List<string> expired = null;
            foreach (var pair in _cooldownUntilByDedup)
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
                _cooldownUntilByDedup.Remove(expired[i]);
            }
        }

        private void PushHistory(IA_CommandRequest request, IA_CommandStatus status, float now, string message)
        {
            _history.Enqueue(new IA_CommandRecord
            {
                Id = request.Id,
                DedupKey = request.DedupKey,
                Type = request.Type,
                Status = status,
                Timestamp = now,
                Message = message
            });

            while (_history.Count > 128)
            {
                _history.Dequeue();
            }
        }

        private static int CompareRequests(IA_CommandRequest a, IA_CommandRequest b)
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return a.EnqueueTime.CompareTo(b.EnqueueTime);
        }
    }
}
