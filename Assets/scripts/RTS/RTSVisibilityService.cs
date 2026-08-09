using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum RTSDetectionSource
    {
        DirectVision,
        Radar,
        Sonar,
        AirRecon,
        Satellite,
        Manual
    }

    [Serializable]
    public sealed class RTSVisibilityContact
    {
        public int observerTeamId;
        public int targetInstanceId;
        public int targetTeamId;
        public Vector3 lastKnownPosition;
        public float lastSeenAt;
        public float expiresAt;
        public RTSDetectionSource source;
        public bool currentlyVisible;
    }

    /// <summary>
    /// Fonte comum para neblina, minimapa, mapa estrategico e percepcao da IA.
    /// A primeira implementacao fornece visao direta segura e APIs para sensores
    /// especializados migrarem sem alterar as telas.
    /// </summary>
    [DefaultExecutionOrder(-7000)]
    public sealed class RTSVisibilityService : MonoBehaviour
    {
        public static RTSVisibilityService Instancia { get; private set; }

        [SerializeField, Min(0.05f)] private float scanInterval = 0.35f;
        [SerializeField, Min(1f)] private float directVisionRange = 120f;
        [SerializeField, Min(1)] private int maxUnitsPerScan = 512;
        [SerializeField, Min(0.1f)] private float memoryDuration = 18f;

        private readonly Dictionary<int, Dictionary<int, RTSVisibilityContact>> contactsByTeam = new Dictionary<int, Dictionary<int, RTSVisibilityContact>>();
        private float nextScanAt;

        public event Action<RTSVisibilityContact> OnContactUpdated;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanAt)
            {
                return;
            }

            nextScanAt = Time.unscaledTime + scanInterval;
            RefreshDirectContacts();
            ExpireContacts();
        }

        private void OnDestroy()
        {
            if (Instancia == this)
            {
                Instancia = null;
            }
        }

        public bool IsVisibleToTeam(int observerTeamId, IdentidadeUnidade target)
        {
            if (target == null || target.teamID <= 0 || observerTeamId <= 0)
            {
                return false;
            }

            if (target.teamID == observerTeamId)
            {
                return true;
            }

            RTSVisibilityContact contact;
            return TryGetContact(observerTeamId, target.GetInstanceID(), out contact)
                && contact.currentlyVisible
                && contact.expiresAt >= Time.unscaledTime;
        }

        public bool TryGetLastKnownPosition(int observerTeamId, IdentidadeUnidade target, out Vector3 position)
        {
            position = Vector3.zero;
            if (target == null || observerTeamId <= 0)
            {
                return false;
            }

            RTSVisibilityContact contact;
            if (!TryGetContact(observerTeamId, target.GetInstanceID(), out contact))
            {
                return false;
            }

            position = contact.lastKnownPosition;
            return Time.unscaledTime <= contact.lastSeenAt + memoryDuration;
        }

        public void ReportContact(int observerTeamId, IdentidadeUnidade target, RTSDetectionSource source, float duration = -1f)
        {
            if (observerTeamId <= 0 || target == null || target.teamID <= 0 || target.teamID == observerTeamId)
            {
                return;
            }

            Dictionary<int, RTSVisibilityContact> contacts = GetOrCreateTeamContacts(observerTeamId);
            int targetId = target.GetInstanceID();
            RTSVisibilityContact contact;
            if (!contacts.TryGetValue(targetId, out contact) || contact == null)
            {
                contact = new RTSVisibilityContact
                {
                    observerTeamId = observerTeamId,
                    targetInstanceId = targetId,
                    targetTeamId = target.teamID
                };
                contacts[targetId] = contact;
            }

            contact.targetTeamId = target.teamID;
            contact.lastKnownPosition = target.transform.position;
            contact.lastSeenAt = Time.unscaledTime;
            contact.expiresAt = Time.unscaledTime + (duration > 0f ? duration : memoryDuration);
            contact.source = source;
            contact.currentlyVisible = true;
            OnContactUpdated?.Invoke(contact);
        }

        public int GetVisibleContactCount(int observerTeamId)
        {
            Dictionary<int, RTSVisibilityContact> contacts;
            if (!contactsByTeam.TryGetValue(observerTeamId, out contacts) || contacts == null)
            {
                return 0;
            }

            int count = 0;
            foreach (RTSVisibilityContact contact in contacts.Values)
            {
                if (contact != null && contact.currentlyVisible && contact.expiresAt >= Time.unscaledTime)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshDirectContacts()
        {
            IdentidadeUnidade[] units = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            int count = Mathf.Min(units != null ? units.Length : 0, maxUnitsPerScan);
            float rangeSqr = directVisionRange * directVisionRange;

            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade observer = units[i];
                if (observer == null || observer.teamID <= 0 || !observer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                for (int j = 0; j < count; j++)
                {
                    IdentidadeUnidade target = units[j];
                    if (target == null || target == observer || target.teamID <= 0 || target.teamID == observer.teamID
                        || !target.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if ((observer.transform.position - target.transform.position).sqrMagnitude <= rangeSqr)
                    {
                        ReportContact(observer.teamID, target, RTSDetectionSource.DirectVision);
                    }
                }
            }
        }

        private void ExpireContacts()
        {
            float now = Time.unscaledTime;
            foreach (Dictionary<int, RTSVisibilityContact> teamContacts in contactsByTeam.Values)
            {
                if (teamContacts == null) continue;
                foreach (RTSVisibilityContact contact in teamContacts.Values)
                {
                    if (contact == null) continue;
                    contact.currentlyVisible = contact.expiresAt >= now;
                }
            }
        }

        private RTSVisibilityContact TryGetContact(int observerTeamId, int targetInstanceId)
        {
            return TryGetContact(observerTeamId, targetInstanceId, out RTSVisibilityContact contact) ? contact : null;
        }

        private bool TryGetContact(int observerTeamId, int targetInstanceId, out RTSVisibilityContact contact)
        {
            contact = null;
            Dictionary<int, RTSVisibilityContact> contacts;
            return contactsByTeam.TryGetValue(observerTeamId, out contacts)
                && contacts != null
                && contacts.TryGetValue(targetInstanceId, out contact)
                && contact != null;
        }

        private Dictionary<int, RTSVisibilityContact> GetOrCreateTeamContacts(int teamId)
        {
            Dictionary<int, RTSVisibilityContact> contacts;
            if (!contactsByTeam.TryGetValue(teamId, out contacts) || contacts == null)
            {
                contacts = new Dictionary<int, RTSVisibilityContact>();
                contactsByTeam[teamId] = contacts;
            }

            return contacts;
        }
    }
}
