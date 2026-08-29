using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    public sealed class IA02EventBus
    {
        private readonly Dictionary<string, List<Action<IA02RuntimeEvent>>> topicSubscribers = new Dictionary<string, List<Action<IA02RuntimeEvent>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<Action<IA02RuntimeEvent>>> nationSubscribers = new Dictionary<int, List<Action<IA02RuntimeEvent>>>();
        private readonly List<IA02RuntimeEvent> history = new List<IA02RuntimeEvent>(64);
        private readonly int maxHistory;

        public IA02EventBus(int maxHistory = 64)
        {
            this.maxHistory = Mathf.Max(1, maxHistory);
        }

        public int PublishedCount { get; private set; }
        public float LastPublishedAt { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public IReadOnlyList<IA02RuntimeEvent> History => history;

        public void Subscribe(string topic, Action<IA02RuntimeEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            string key = NormalizeTopic(topic);
            if (!topicSubscribers.TryGetValue(key, out List<Action<IA02RuntimeEvent>> list))
            {
                list = new List<Action<IA02RuntimeEvent>>(4);
                topicSubscribers[key] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void SubscribeNation(int nationId, Action<IA02RuntimeEvent> handler)
        {
            if (nationId <= 0 || handler == null)
            {
                return;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA02RuntimeEvent>> list))
            {
                list = new List<Action<IA02RuntimeEvent>>(4);
                nationSubscribers[nationId] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void Unsubscribe(string topic, Action<IA02RuntimeEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            string key = NormalizeTopic(topic);
            if (!topicSubscribers.TryGetValue(key, out List<Action<IA02RuntimeEvent>> list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                topicSubscribers.Remove(key);
            }
        }

        public void UnsubscribeNation(int nationId, Action<IA02RuntimeEvent> handler)
        {
            if (nationId <= 0 || handler == null)
            {
                return;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA02RuntimeEvent>> list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                nationSubscribers.Remove(nationId);
            }
        }

        public int Publish(IA02RuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null)
            {
                return 0;
            }

            runtimeEvent.Topic = NormalizeTopic(runtimeEvent.Topic);
            runtimeEvent.TimeStamp = runtimeEvent.TimeStamp > 0f ? runtimeEvent.TimeStamp : Time.unscaledTime;

            history.Add(runtimeEvent);
            while (history.Count > maxHistory)
            {
                history.RemoveAt(0);
            }

            PublishedCount++;
            LastPublishedAt = runtimeEvent.TimeStamp;
            LastError = string.Empty;

            int delivered = 0;
            delivered += DeliverTopic(runtimeEvent.Topic, runtimeEvent);
            delivered += DeliverNation(runtimeEvent.NationId, runtimeEvent);
            return delivered;
        }

        public int Publish(int nationId, string topic, string message, object payload = null, IA02EventSeverity severity = IA02EventSeverity.Info, int sourceInstanceId = 0, int teamId = 0, float timestamp = 0f)
        {
            IA02RuntimeEvent runtimeEvent = new IA02RuntimeEvent
            {
                NationId = nationId,
                TeamId = teamId,
                SourceInstanceId = sourceInstanceId,
                Topic = topic ?? string.Empty,
                Message = message ?? string.Empty,
                Payload = payload,
                PayloadText = payload != null ? payload.ToString() : string.Empty,
                Severity = severity,
                TimeStamp = timestamp > 0f ? timestamp : Time.unscaledTime
            };

            return Publish(runtimeEvent);
        }

        public void Clear()
        {
            topicSubscribers.Clear();
            nationSubscribers.Clear();
            history.Clear();
            PublishedCount = 0;
            LastPublishedAt = 0f;
            LastError = string.Empty;
        }

        private int DeliverTopic(string topic, IA02RuntimeEvent runtimeEvent)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return 0;
            }

            if (!topicSubscribers.TryGetValue(topic, out List<Action<IA02RuntimeEvent>> handlers) || handlers.Count == 0)
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                Action<IA02RuntimeEvent> handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler(runtimeEvent);
                    delivered++;
                }
                catch (Exception exception)
                {
                    LastError = exception.Message;
                }
            }

            return delivered;
        }

        private int DeliverNation(int nationId, IA02RuntimeEvent runtimeEvent)
        {
            if (nationId <= 0)
            {
                return 0;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA02RuntimeEvent>> handlers) || handlers.Count == 0)
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                Action<IA02RuntimeEvent> handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler(runtimeEvent);
                    delivered++;
                }
                catch (Exception exception)
                {
                    LastError = exception.Message;
                }
            }

            return delivered;
        }

        private static string NormalizeTopic(string topic)
        {
            return string.IsNullOrWhiteSpace(topic) ? string.Empty : topic.Trim().ToLowerInvariant();
        }
    }
}
