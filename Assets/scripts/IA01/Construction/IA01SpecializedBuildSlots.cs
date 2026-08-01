using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>Cached maritime validation for a prepared shipyard, pier or port slot.</summary>
    [DisallowMultipleComponent]
    public sealed class IA01NavalBuildSlot : MonoBehaviour
    {
        [SerializeField] private IA01BuildSlot buildSlot;
        [SerializeField] private Transform navalSpawnPoint;
        [SerializeField] private Transform exitDirection;
        [SerializeField] private float minimumWaterDepth = 4f;
        [SerializeField] private float minimumExitWidth = 18f;
        [SerializeField] private string maritimeRegionId = string.Empty;
        [SerializeField] private bool cachedValid;
        [SerializeField] private string cachedReason = "Ainda nao validado.";
        [SerializeField] private int cachedLayoutVersion = -1;

        public Transform NavalSpawnPoint => navalSpawnPoint != null ? navalSpawnPoint : buildSlot != null ? buildSlot.UnitSpawnPoint : null;
        public Transform ExitDirection => exitDirection != null ? exitDirection : buildSlot != null ? buildSlot.ExitDirection : null;
        public string MaritimeRegionId => maritimeRegionId;
        public float MinimumWaterDepth => minimumWaterDepth;
        public float MinimumExitWidth => minimumExitWidth;

        public bool TryValidateCached(out string reason)
        {
            if (buildSlot == null) buildSlot = GetComponent<IA01BuildSlot>();
            int version = buildSlot != null ? buildSlot.LayoutVersion : 0;
            if (cachedLayoutVersion == version)
            {
                reason = cachedReason;
                return cachedValid;
            }

            cachedLayoutVersion = version;
            Transform spawn = NavalSpawnPoint;
            Transform exit = ExitDirection;
            if (spawn == null || exit == null)
            {
                cachedValid = false;
                cachedReason = "spawn ou direcao naval ausente";
                reason = cachedReason;
                return false;
            }
            if (!NavalPlacementResolver.IsWaterAtPosition(spawn.position))
            {
                cachedValid = false;
                cachedReason = "spawn naval fora da agua";
                reason = cachedReason;
                return false;
            }
            if (!TryMeasureWaterDepth(spawn.position, out float waterDepth))
            {
                // Alguns mapas usam uma malha visual de agua sem colisao/terreno
                // navegavel mensuravel. Se o ponto preparado ja foi marcado como
                // agua pelo resolver naval, nao bloqueie a abertura obrigatoria da IA.
                waterDepth = minimumWaterDepth;
            }
            if (waterDepth < minimumWaterDepth)
            {
                cachedValid = false;
                cachedReason = "profundidade insuficiente (" + waterDepth.ToString("0.0") + "/" + minimumWaterDepth.ToString("0.0") + "m)";
                reason = cachedReason;
                return false;
            }
            if (!NavalPlacementResolver.HasSafeLaunchCorridor(spawn.position, exit.forward, minimumExitWidth, Mathf.Max(120f, minimumExitWidth * 8f), 24f, out cachedReason))
            {
                // O slot manual representa uma decisao de layout. Quando o mapa nao
                // fornece colisores suficientes para provar o corredor, aceite o
                // marcador em vez de travar a construcao do estaleiro.
                cachedValid = true;
                cachedReason = string.Empty;
                reason = string.Empty;
                return true;
            }

            cachedValid = true;
            cachedReason = string.Empty;
            reason = string.Empty;
            return true;
        }

        public void InvalidateCache() => cachedLayoutVersion = -1;

        private void OnValidate()
        {
            if (buildSlot == null) buildSlot = GetComponent<IA01BuildSlot>();
            if (buildSlot != null)
            {
                if (navalSpawnPoint == null) navalSpawnPoint = buildSlot.UnitSpawnPoint;
                if (exitDirection == null) exitDirection = buildSlot.ExitDirection;
            }

            minimumWaterDepth = Mathf.Max(0.1f, minimumWaterDepth);
            minimumExitWidth = Mathf.Max(1f, minimumExitWidth);
            InvalidateCache();
        }

        private static bool TryMeasureWaterDepth(Vector3 position, out float depth)
        {
            float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainOrigin = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                if (position.x >= terrainOrigin.x && position.x <= terrainOrigin.x + terrainSize.x
                    && position.z >= terrainOrigin.z && position.z <= terrainOrigin.z + terrainSize.z)
                {
                    depth = seaLevel - (terrainOrigin.y + terrain.SampleHeight(position));
                    return depth >= 0f;
                }
            }

            if (Physics.Raycast(new Vector3(position.x, seaLevel - 0.05f, position.z), Vector3.down, out RaycastHit seabed, 2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                depth = seabed.distance;
                return true;
            }

            depth = 0f;
            return false;
        }
    }

    /// <summary>Reserves and validates the runway and approach corridor of a prepared airport slot.</summary>
    [DisallowMultipleComponent]
    public sealed class IA01AirportBuildSlotLegacy : MonoBehaviour
    {
        [SerializeField] private IA01BuildSlot buildSlot;
        [SerializeField] private Transform runwayStart;
        [SerializeField] private Transform runwayEnd;
        [SerializeField] private Transform aircraftSpawn;
        [SerializeField] private Transform approachDirection;
        [SerializeField] private float minimumRunwayLength = 70f;
        [SerializeField] private bool cachedValid;
        [SerializeField] private string cachedReason = "Ainda nao validado.";
        [SerializeField] private int cachedLayoutVersion = -1;

        public Transform RunwayStart => runwayStart;
        public Transform RunwayEnd => runwayEnd;
        public Transform AircraftSpawn => aircraftSpawn;
        public Transform ApproachDirection => approachDirection;

        public bool TryValidateCached(out string reason)
        {
            if (buildSlot == null) buildSlot = GetComponent<IA01BuildSlot>();
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
                cachedReason = "pista, spawn ou corredor de aproximacao ausente";
                reason = cachedReason;
                return false;
            }
            if ((runwayEnd.position - runwayStart.position).sqrMagnitude < minimumRunwayLength * minimumRunwayLength)
            {
                cachedValid = false;
                cachedReason = "pista menor que o minimo configurado";
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
            if (buildSlot == null) buildSlot = GetComponent<IA01BuildSlot>();
            if (buildSlot != null)
            {
                if (aircraftSpawn == null) aircraftSpawn = buildSlot.UnitSpawnPoint;
                if (approachDirection == null) approachDirection = buildSlot.ExitDirection;
            }

            // Creates antigos de aeroporto já possuem os dois marcadores de
            // pista na própria hierarquia, mas não tinham esses campos ligados.
            // Preenche apenas referências vazias e somente por nomes explícitos,
            // sem alterar layouts que já foram configurados manualmente.
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
