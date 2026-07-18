using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>Âncoras pertencentes a uma nação. Duplicar o prefab duplica este estado.</summary>
    public sealed class IA01ConstructionAnchors : MonoBehaviour
    {
        [Header("Pontos fixos do país")]
        [SerializeField] private Transform vehicleConstructor;
        [SerializeField] private Transform militaryAirport;
        [SerializeField] private Transform commercialAirport;
        [SerializeField] private Transform shipyard;
        [Header("Zona livre")]
        [SerializeField] private Collider housingZone;

        public Transform HousingZone => housingZone != null ? housingZone.transform : null;

        public bool TryResolve(IA01IntentType intent, out Vector3 position)
        {
            return TryResolve(intent, out position, out _);
        }

        public bool TryResolve(IA01IntentType intent, out Vector3 position, out Quaternion rotation)
        {
            Transform target = null;
            switch (intent)
            {
                case IA01IntentType.BuildVehicleConstructor: target = vehicleConstructor; break;
                case IA01IntentType.BuildMilitaryAirport: target = militaryAirport; break;
                case IA01IntentType.BuildCommercialAirport: target = commercialAirport; break;
                case IA01IntentType.BuildShipyard: target = shipyard; break;
            }
            if (target != null) { position = target.position; rotation = target.rotation; return true; }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Draw(vehicleConstructor); Draw(militaryAirport); Draw(commercialAirport); Draw(shipyard);
        }

        private static void Draw(Transform point)
        {
            if (point != null) Gizmos.DrawWireSphere(point.position, 6f);
        }
    }
}
