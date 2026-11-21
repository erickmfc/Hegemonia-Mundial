using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DisallowMultipleComponent]
    public sealed class IA02BuildAutonomousZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField] private IA02BuildDomain allowedDomain = IA02BuildDomain.Land;
        [SerializeField] private Vector3 localSize = new Vector3(120f, 40f, 120f);

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId.Trim();
        public IA02BuildDomain AllowedDomain => allowedDomain;
        public Bounds WorldBounds => new Bounds(transform.TransformPoint(Vector3.zero), new Vector3(Mathf.Abs(localSize.x * transform.lossyScale.x), Mathf.Abs(localSize.y * transform.lossyScale.y), Mathf.Abs(localSize.z * transform.lossyScale.z)));

        public bool IsCompatible(IA02BuildDefinition definition)
        {
            return definition != null && (definition.Domain == allowedDomain || allowedDomain == IA02BuildDomain.Coastal && definition.Domain == IA02BuildDomain.Water);
        }

        private void OnEnable()
        {
            IA02CityLayout layout = GetComponentInParent<IA02CityLayout>();
            layout?.RegisterAutonomousZone(this);
        }

        private void OnDisable()
        {
            IA02CityLayout layout = GetComponentInParent<IA02CityLayout>();
            layout?.UnregisterAutonomousZone(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, localSize);
        }
    }
}
