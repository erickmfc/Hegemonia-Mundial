using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DisallowMultipleComponent]
    public sealed class IA01BuildAutonomousZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField] private IA01BuildDomain allowedDomain = IA01BuildDomain.Land;
        [SerializeField] private Vector3 localSize = new Vector3(120f, 40f, 120f);

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId.Trim();
        public IA01BuildDomain AllowedDomain => allowedDomain;
        public Bounds WorldBounds => new Bounds(transform.TransformPoint(Vector3.zero), new Vector3(Mathf.Abs(localSize.x * transform.lossyScale.x), Mathf.Abs(localSize.y * transform.lossyScale.y), Mathf.Abs(localSize.z * transform.lossyScale.z)));

        public bool IsCompatible(IA01BuildDefinition definition)
        {
            return definition != null && (definition.Domain == allowedDomain || allowedDomain == IA01BuildDomain.Coastal && definition.Domain == IA01BuildDomain.Water);
        }

        private void OnEnable()
        {
            IA01CityLayout layout = GetComponentInParent<IA01CityLayout>();
            layout?.RegisterAutonomousZone(this);
        }

        private void OnDisable()
        {
            IA01CityLayout layout = GetComponentInParent<IA01CityLayout>();
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
