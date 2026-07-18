using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01EventBus
    {
        private readonly Dictionary<string, List<Action<IA01RuntimeEvent>>> topicSubscribers = new Dictionary<string, List<Action<IA01RuntimeEvent>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<Action<IA01RuntimeEvent>>> nationSubscribers = new Dictionary<int, List<Action<IA01RuntimeEvent>>>();
        private readonly List<IA01RuntimeEvent> history = new List<IA01RuntimeEvent>(64);
        private readonly int maxHistory;

        public IA01EventBus(int maxHistory = 64)
        {
            this.maxHistory = Mathf.Max(1, maxHistory);
        }

        public int PublishedCount { get; private set; }
        public float LastPublishedAt { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public IReadOnlyList<IA01RuntimeEvent> History => history;

        public void Subscribe(string topic, Action<IA01RuntimeEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            string key = NormalizeTopic(topic);
            if (!topicSubscribers.TryGetValue(key, out List<Action<IA01RuntimeEvent>> list))
            {
                list = new List<Action<IA01RuntimeEvent>>(4);
                topicSubscribers[key] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void SubscribeNation(int nationId, Action<IA01RuntimeEvent> handler)
        {
            if (nationId <= 0 || handler == null)
            {
                return;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA01RuntimeEvent>> list))
            {
                list = new List<Action<IA01RuntimeEvent>>(4);
                nationSubscribers[nationId] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void Unsubscribe(string topic, Action<IA01RuntimeEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            string key = NormalizeTopic(topic);
            if (!topicSubscribers.TryGetValue(key, out List<Action<IA01RuntimeEvent>> list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                topicSubscribers.Remove(key);
            }
        }

        public void UnsubscribeNation(int nationId, Action<IA01RuntimeEvent> handler)
        {
            if (nationId <= 0 || handler == null)
            {
                return;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA01RuntimeEvent>> list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                nationSubscribers.Remove(nationId);
            }
        }

        public int Publish(IA01RuntimeEvent runtimeEvent)
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

        public int Publish(int nationId, string topic, string message, object payload = null, IA01EventSeverity severity = IA01EventSeverity.Info, int sourceInstanceId = 0, int teamId = 0, float timestamp = 0f)
        {
            IA01RuntimeEvent runtimeEvent = new IA01RuntimeEvent
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

        private int DeliverTopic(string topic, IA01RuntimeEvent runtimeEvent)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return 0;
            }

            if (!topicSubscribers.TryGetValue(topic, out List<Action<IA01RuntimeEvent>> handlers) || handlers.Count == 0)
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                Action<IA01RuntimeEvent> handler = handlers[i];
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

        private int DeliverNation(int nationId, IA01RuntimeEvent runtimeEvent)
        {
            if (nationId <= 0)
            {
                return 0;
            }

            if (!nationSubscribers.TryGetValue(nationId, out List<Action<IA01RuntimeEvent>> handlers) || handlers.Count == 0)
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                Action<IA01RuntimeEvent> handler = handlers[i];
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
