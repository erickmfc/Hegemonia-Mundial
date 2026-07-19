using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    // Confirma no WorldState os efeitos que o backend informou ter iniciado.
    public sealed class IA_CommandConfirmationService
    {
        private sealed class PendingConfirmation
        {
            public IA_CommandRequest Request;
            public int BaselineCount;
            public float AcceptedAt;
        }

        private readonly Dictionary<string, PendingConfirmation> _pending = new Dictionary<string, PendingConfirmation>();
        private const float MinimumObservationDelay = 0.20f;
        private const float ConfirmationTimeout = 8f;
        private const int MaxRetryAttempts = 3;

        public int PendingCount { get { return _pending.Count; } }

        public void CaptureBaseline(IA_CommandRequest request, IA_WorldState world)
        {
            if (request == null || world == null) return;
            _pending[request.Id] = new PendingConfirmation
            {
                Request = request,
                BaselineCount = CountMatching(request, world),
                AcceptedAt = -1f
            };
        }

        public void TrackAccepted(IA_CommandRequest request, IA_CommandQueue queue, float now, string message)
        {
            PendingConfirmation pending;
            if (request == null || !_pending.TryGetValue(request.Id, out pending)) return;
            pending.AcceptedAt = now;
            queue.AwaitConfirmation(request, now, message + " | aguardando WorldState");
            IA_RuntimeTextTrace.LogCommand(queue != null ? queue.TraceTeamId : -1, "CommandConfirm", "AWAIT", request, message + " | aguardando WorldState");
        }

        public void Cancel(string requestId)
        {
            if (!string.IsNullOrEmpty(requestId)) _pending.Remove(requestId);
        }

        public void Tick(IA_CommandQueue queue, IA_WorldState world, float now)
        {
            if (queue == null || world == null || _pending.Count == 0) return;
            List<string> completed = new List<string>();
            foreach (KeyValuePair<string, PendingConfirmation> pair in _pending)
            {
                PendingConfirmation pending = pair.Value;
                if (pending.AcceptedAt < 0f) continue;
                if (now - pending.AcceptedAt < MinimumObservationDelay) continue;

                if (CountMatching(pending.Request, world) > pending.BaselineCount)
                {
                    queue.Complete(pending.Request, true, now, "confirmado no WorldState");
                    IA_RuntimeTextTrace.LogCommand(queue.TraceTeamId, "CommandConfirm", "CONFIRM_OK", pending.Request, "confirmado no WorldState");
                    completed.Add(pair.Key);
                    continue;
                }

                if (now - pending.AcceptedAt < ConfirmationTimeout) continue;
                if (pending.Request.AttemptCount < MaxRetryAttempts)
                {
                    queue.Requeue(pending.Request, now, RetryDelay(pending.Request.AttemptCount), "sem confirmacao; retry " + pending.Request.AttemptCount);
                    IA_RuntimeTextTrace.LogCommand(queue.TraceTeamId, "CommandConfirm", "RETRY", pending.Request, "sem confirmacao; retry " + pending.Request.AttemptCount);
                }
                else
                {
                    queue.Complete(pending.Request, false, now, "sem confirmacao no WorldState apos retries");
                    IA_RuntimeTextTrace.LogCommand(queue.TraceTeamId, "CommandConfirm", "CONFIRM_FAIL", pending.Request, "sem confirmacao no WorldState apos retries");
                }
                completed.Add(pair.Key);
            }

            for (int i = 0; i < completed.Count; i++) _pending.Remove(completed[i]);
        }

        private static float RetryDelay(int attempts)
        {
            return Mathf.Min(6f, 0.5f * Mathf.Pow(2f, Mathf.Max(0, attempts - 1)));
        }

        private static int CountMatching(IA_CommandRequest request, IA_WorldState world)
        {
            string key = ResolveItemKey(request);
            if (string.IsNullOrEmpty(key)) return 0;
            List<GameObject> source = request.Type == IA_CommandType.Build ? world.OwnStructures : world.OwnUnits;
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                GameObject obj = source[i];
                if (obj == null) continue;
                IA_ConstructionMetadata metadata = obj.GetComponent<IA_ConstructionMetadata>();
                string name = metadata != null
                    ? IA_Text.Normalize(obj.name + " " + metadata.ItemId + " " + metadata.DisplayName + " " + metadata.Aliases + " " + metadata.SourcePrefabName)
                    : IA_Text.Normalize(obj.name);
                if (name.Contains(key) || key.Contains(name)) count++;
            }
            return count;
        }

        private static string ResolveItemKey(IA_CommandRequest request)
        {
            IA_BuildOrderData build = request.Payload as IA_BuildOrderData;
            if (build != null) return IA_Text.Normalize(build.ItemKey);
            IA_ProduceOrderData produce = request.Payload as IA_ProduceOrderData;
            return produce != null ? IA_Text.Normalize(produce.ItemKey) : string.Empty;
        }
    }
}
