using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_CommandQueue
    {
        public int TraceTeamId { get; set; } = -1;
        private readonly List<IA_CommandRequest> _pending = new List<IA_CommandRequest>();
        private readonly Dictionary<string, float> _cooldownUntilByDedup = new Dictionary<string, float>();
        private readonly HashSet<string> _dedupInQueue = new HashSet<string>();
        private readonly Dictionary<string, IA_CommandStatus> _statusById = new Dictionary<string, IA_CommandStatus>();
        private readonly Dictionary<string, IA_CommandRequest> _awaitingConfirmation = new Dictionary<string, IA_CommandRequest>();
        private readonly Queue<IA_CommandRecord> _history = new Queue<IA_CommandRecord>();

        public int PendingCount
        {
            get { return _pending.Count; }
        }

        public int CooldownCount
        {
            get { return _cooldownUntilByDedup.Count; }
        }

        public int AwaitingConfirmationCount
        {
            get { return _awaitingConfirmation.Count; }
        }

        public int CompletedSuccessCount { get; private set; }
        public int CompletedFailureCount { get; private set; }

        public bool Enqueue(IA_CommandRequest request, float now, out string reason)
        {
            reason = string.Empty;
            if (request == null)
            {
                reason = "request nula";
                return false;
            }

            request.Id = string.IsNullOrEmpty(request.Id) ? System.Guid.NewGuid().ToString("N") : request.Id;
            request.Origin = NormalizeMetaField(request.Origin, "desconhecido");
            request.Domain = NormalizeMetaField(request.Domain, request.Type.ToString());
            request.Reason = NormalizeMetaField(request.Reason, "sem motivo declarado");
            request.Family = NormalizeMetaField(request.Family, request.Type.ToString().ToLowerInvariant());
            request.Family = IA_CommandFactory.NormalizeFamily(request.Family, request.Type);
            request.DedupKey = IA_SharedRuntimeSupport.BuildCommandDedupKey(
                request.Family,
                string.IsNullOrWhiteSpace(request.DedupKey) ? request.Type.ToString() : request.DedupKey,
                request.Payload);
            request.EnqueueTime = now;
            request.AttemptCount = 0;
            request.FirstAttemptTime = 0f;

            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                if (_dedupInQueue.Contains(request.DedupKey))
                {
                    reason = "duplicada em fila";
                    _statusById[request.Id] = IA_CommandStatus.Cancelled;
                    PushHistory(request, IA_CommandStatus.Cancelled, now, reason);
                    IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "REJECT_DUP", request, reason);
                    return false;
                }

                float cooldownUntil;
                if (_cooldownUntilByDedup.TryGetValue(request.DedupKey, out cooldownUntil) && cooldownUntil > now)
                {
                    reason = "em cooldown";
                    _statusById[request.Id] = IA_CommandStatus.Cancelled;
                    PushHistory(request, IA_CommandStatus.Cancelled, now, reason);
                    IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "REJECT_COOLDOWN", request, reason);
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
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_emitted");
            IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "ENQUEUE", request, "enfileirado");
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

            int readyIndex = -1;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].EnqueueTime <= now)
                {
                    readyIndex = i;
                    break;
                }
            }

            if (readyIndex < 0)
            {
                request = null;
                return false;
            }

            request = _pending[readyIndex];
            _pending.RemoveAt(readyIndex);

            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                _dedupInQueue.Remove(request.DedupKey);
            }

            _statusById[request.Id] = IA_CommandStatus.Running;
            request.AttemptCount++;
            if (request.FirstAttemptTime <= 0f) request.FirstAttemptTime = now;
            PushHistory(request, IA_CommandStatus.Running, now, "executando");
            IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "DEQUEUE", request, "executando");
            return true;
        }

        public void AwaitConfirmation(IA_CommandRequest request, float now, string message)
        {
            if (request == null) return;
            _awaitingConfirmation[request.Id] = request;
            _statusById[request.Id] = IA_CommandStatus.AwaitingConfirmation;
            PushHistory(request, IA_CommandStatus.AwaitingConfirmation, now, message);
            IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "AWAIT_CONFIRM", request, message);
        }

        public List<IA_CommandRequest> GetAwaitingConfirmations()
        {
            return new List<IA_CommandRequest>(_awaitingConfirmation.Values);
        }

        public bool Requeue(IA_CommandRequest request, float now, float delaySeconds, string reason)
        {
            if (request == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.DedupKey) && _dedupInQueue.Contains(request.DedupKey))
            {
                return false;
            }

            request.EnqueueTime = now + Mathf.Max(0.01f, delaySeconds);
            _pending.Add(request);
            _pending.Sort(CompareRequests);
            if (!string.IsNullOrEmpty(request.DedupKey))
            {
                _dedupInQueue.Add(request.DedupKey);
            }

            _statusById[request.Id] = IA_CommandStatus.Queued;
            PushHistory(request, IA_CommandStatus.Queued, now, reason ?? "recolocado na fila");
            IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "REQUEUE", request, reason ?? "recolocado na fila");
            return true;
        }

        public void Complete(IA_CommandRequest request, bool success, float now, string message)
        {
            if (request == null)
            {
                return;
            }

            IA_CommandStatus finalStatus = success ? IA_CommandStatus.Success : IA_CommandStatus.Failed;
            if (success) CompletedSuccessCount++;
            else CompletedFailureCount++;
            _awaitingConfirmation.Remove(request.Id);
            _statusById[request.Id] = finalStatus;
            PushHistory(request, finalStatus, now, message);
            IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", success ? "COMPLETE_OK" : "COMPLETE_FAIL", request, message);

            if (!string.IsNullOrEmpty(request.DedupKey) && request.CooldownSeconds > 0f)
            {
                _cooldownUntilByDedup[request.DedupKey] = now + Mathf.Max(0.1f, request.CooldownSeconds);
                PushHistory(request, IA_CommandStatus.CoolingDown, now, "cooldown");
                IA_RuntimeTextTrace.LogCommand(TraceTeamId, "CommandQueue", "COOLDOWN", request, "cooldown");
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
                Origin = request.Origin,
                Domain = request.Domain,
                Reason = request.Reason,
                Family = request.Family,
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

        private static string NormalizeMetaField(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return string.IsNullOrEmpty(normalized) ? fallback : normalized;
        }
    }
}
