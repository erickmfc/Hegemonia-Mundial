using System;
using System.Collections.Generic;
using UnityEngine;

public enum InteractionOwner
{
    None = 0,
    Construction = 10,
    SelectionBox = 20,
    Demolition = 30,
    Patrol = 40,
    Follow = 50,
    AirportOrder = 60,
    CarrierOrder = 70,
    ManualFire = 80,
    MenuComando = 90,
    FactoryIndustryPanel = 95,
    GovernmentMenu = 96,
    Attack = 100
}

[System.Serializable]
public struct InteractionPolicy
{
    public bool bloqueiaSelecao;
    public bool bloqueiaOrdemMundo;
    public bool bloqueiaRotacaoCamera;
    public bool consomeLMB;
    public bool consomeRMB;

    public static InteractionPolicy Nenhuma
    {
        get
        {
            return new InteractionPolicy();
        }
    }
}

public struct InteractionModeSnapshot
{
    public InteractionOwner Owner;
    public InteractionPolicy Policy;
    public string Reason;
    public float SinceTime;

    public bool HasOwner
    {
        get
        {
            return Owner != InteractionOwner.None;
        }
    }
}

public static class InteractionModeService
{
    private struct RequestKey : IEquatable<RequestKey>
    {
        public InteractionOwner Owner;
        public int SourceId;

        public bool Equals(RequestKey other)
        {
            return Owner == other.Owner && SourceId == other.SourceId;
        }

        public override bool Equals(object obj)
        {
            return obj is RequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Owner * 397) ^ SourceId;
            }
        }
    }

    private struct RequestState
    {
        public InteractionPolicy Policy;
        public string Reason;
        public int Sequence;
        public float SinceTime;
        public int SourceId;
    }

    private static readonly Dictionary<RequestKey, RequestState> Requests = new Dictionary<RequestKey, RequestState>();
    private static InteractionModeSnapshot _currentSnapshot;
    private static int _sequence;
    private static string _lastReportedRejection = string.Empty;
    private static float _lastReportedRejectionTime = -999f;

    public static void Request(InteractionOwner owner, InteractionPolicy policy)
    {
        Request(null, owner, policy, string.Empty);
    }

    public static void Request(InteractionOwner owner, InteractionPolicy policy, string reason)
    {
        Request(null, owner, policy, reason);
    }

    public static void Request(object source, InteractionOwner owner, InteractionPolicy policy)
    {
        Request(source, owner, policy, string.Empty);
    }

    public static void Request(object source, InteractionOwner owner, InteractionPolicy policy, string reason)
    {
        if (owner == InteractionOwner.None)
        {
            return;
        }

        int sourceId = ResolveSourceId(source);
        RequestKey key = new RequestKey
        {
            Owner = owner,
            SourceId = sourceId
        };

        RequestState state = new RequestState
        {
            Policy = policy,
            Reason = reason ?? string.Empty,
            Sequence = ++_sequence,
            SinceTime = Application.isPlaying ? Time.unscaledTime : 0f,
            SourceId = sourceId
        };

        Requests[key] = state;
        RefreshSnapshot();
    }

    public static void Release(InteractionOwner owner)
    {
        Release(null, owner);
    }

    public static void Release(object source, InteractionOwner owner)
    {
        if (owner == InteractionOwner.None)
        {
            return;
        }

        bool removed = false;
        if (source == null)
        {
            List<RequestKey> keysToRemove = null;
            foreach (KeyValuePair<RequestKey, RequestState> pair in Requests)
            {
                if (pair.Key.Owner != owner)
                {
                    continue;
                }

                if (keysToRemove == null)
                {
                    keysToRemove = new List<RequestKey>();
                }

                keysToRemove.Add(pair.Key);
            }

            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    removed |= Requests.Remove(keysToRemove[i]);
                }
            }
        }
        else
        {
            RequestKey key = new RequestKey
            {
                Owner = owner,
                SourceId = ResolveSourceId(source)
            };
            removed = Requests.Remove(key);
        }

        if (removed)
        {
            RefreshSnapshot();
        }
    }

    public static bool IsActive(InteractionOwner owner)
    {
        return _currentSnapshot.Owner == owner;
    }

    public static bool IsActive(object source, InteractionOwner owner)
    {
        if (_currentSnapshot.Owner != owner)
        {
            return false;
        }

        return ResolveSourceId(source) == ResolveCurrentSourceId();
    }

    public static bool CanConsumeLeft(InteractionOwner owner)
    {
        return _currentSnapshot.Owner == owner && _currentSnapshot.Policy.consomeLMB;
    }

    public static bool CanConsumeRight(InteractionOwner owner)
    {
        return _currentSnapshot.Owner == owner && _currentSnapshot.Policy.consomeRMB;
    }

    public static InteractionModeSnapshot CurrentSnapshot()
    {
        return _currentSnapshot;
    }

    public static void ReportBlockedInput(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        if (_lastReportedRejection == description && now - _lastReportedRejectionTime < 0.5f)
        {
            return;
        }

        _lastReportedRejection = description;
        _lastReportedRejectionTime = now;
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("input_lock_reason", description);
    }

    private static void RefreshSnapshot()
    {
        InteractionModeSnapshot nextSnapshot = new InteractionModeSnapshot
        {
            Owner = InteractionOwner.None,
            Policy = InteractionPolicy.Nenhuma,
            Reason = string.Empty,
            SinceTime = 0f
        };

        int bestSequence = int.MinValue;
        foreach (KeyValuePair<RequestKey, RequestState> pair in Requests)
        {
            if (pair.Value.Sequence < bestSequence)
            {
                continue;
            }

            bestSequence = pair.Value.Sequence;
            nextSnapshot.Owner = pair.Key.Owner;
            nextSnapshot.Policy = pair.Value.Policy;
            nextSnapshot.Reason = pair.Value.Reason;
            nextSnapshot.SinceTime = pair.Value.SinceTime;
        }

        bool ownerChanged = _currentSnapshot.Owner != nextSnapshot.Owner || _currentSnapshot.Reason != nextSnapshot.Reason;
        _currentSnapshot = nextSnapshot;

        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("input_owner", _currentSnapshot.Owner.ToString());
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica(
            "input_lock_reason",
            string.IsNullOrWhiteSpace(_currentSnapshot.Reason) ? "livre" : _currentSnapshot.Reason);

        if (ownerChanged && _currentSnapshot.Owner != InteractionOwner.None)
        {
            string detail = string.IsNullOrWhiteSpace(_currentSnapshot.Reason)
                ? _currentSnapshot.Owner.ToString()
                : _currentSnapshot.Owner + ": " + _currentSnapshot.Reason;
            DiagnosticoDesempenhoJogo.RegistrarEvento("InputLock", detail);
        }
    }

    private static int ResolveSourceId(object source)
    {
        if (source == null)
        {
            return 0;
        }

        UnityEngine.Object unityObject = source as UnityEngine.Object;
        if (unityObject != null)
        {
            return unityObject.GetInstanceID();
        }

        return source.GetHashCode();
    }

    private static int ResolveCurrentSourceId()
    {
        int bestSequence = int.MinValue;
        int sourceId = 0;
        foreach (KeyValuePair<RequestKey, RequestState> pair in Requests)
        {
            if (pair.Value.Sequence < bestSequence)
            {
                continue;
            }

            bestSequence = pair.Value.Sequence;
            sourceId = pair.Value.SourceId;
        }

        return sourceId;
    }
}
