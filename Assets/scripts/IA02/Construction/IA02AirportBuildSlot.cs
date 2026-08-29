using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>Valida a pista e o corredor de aproximação de um create de aeroporto.</summary>
    [DisallowMultipleComponent]
    public sealed class IA02AirportBuildSlot : MonoBehaviour
    {
        [SerializeField] private IA02BuildSlot buildSlot;
        [SerializeField] private Transform runwayStart;
        [SerializeField] private Transform runwayEnd;
        [SerializeField] private Transform aircraftSpawn;
        [SerializeField] private Transform approachDirection;
        [SerializeField] private float minimumRunwayLength = 70f;
        [SerializeField] private bool cachedValid;
        [SerializeField] private string cachedReason = "Ainda não validado.";
        [SerializeField] private int cachedLayoutVersion = -1;

        public Transform RunwayStart => runwayStart;
        public Transform RunwayEnd => runwayEnd;
        public Transform AircraftSpawn => aircraftSpawn;
        public Transform ApproachDirection => approachDirection;

        public bool TryValidateCached(out string reason)
        {
            if (buildSlot == null) buildSlot = GetComponent<IA02BuildSlot>();
            int version = buildSlot != null ? buildSlot.LayoutVersion : 0;
            if (cachedLayoutVersion == version)
            {
                reason = cachedReason;
                return cachedValid;
            }

            cachedLayoutVersion = version;
            if (runwayStart == null || runwayEnd == null || aircraftSpawn == null || approachDirection == null)
            {
                cachedValid = false;
                cachedReason = "pista, spawn ou corredor de aproximação ausente";
                reason = cachedReason;
                return false;
            }
            if ((runwayEnd.position - runwayStart.position).sqrMagnitude < minimumRunwayLength * minimumRunwayLength)
            {
                cachedValid = false;
                cachedReason = "pista menor que o mínimo configurado";
                reason = cachedReason;
                return false;
            }

            cachedValid = true;
            cachedReason = string.Empty;
            reason = string.Empty;
            return true;
        }

        public void InvalidateCache() => cachedLayoutVersion = -1;

        private void OnValidate()
        {
            if (buildSlot == null) buildSlot = GetComponent<IA02BuildSlot>();
            if (buildSlot != null)
            {
                if (aircraftSpawn == null) aircraftSpawn = buildSlot.UnitSpawnPoint;
                if (approachDirection == null) approachDirection = buildSlot.ExitDirection;
            }

            if (runwayStart == null || runwayEnd == null)
            {
                Transform[] filhos = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < filhos.Length; i++)
                {
                    Transform filho = filhos[i];
                    if (filho == null) continue;
                    string nome = filho.name.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
                    if (runwayStart == null && (nome == "pistainicio" || nome == "runwaystart")) runwayStart = filho;
                    if (runwayEnd == null && (nome == "pistafim" || nome == "runwayend")) runwayEnd = filho;
                }
            }

            minimumRunwayLength = Mathf.Max(8f, minimumRunwayLength);
            InvalidateCache();
        }
    }
}
