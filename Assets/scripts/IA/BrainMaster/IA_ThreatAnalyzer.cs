using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ThreatAnalyzer : IIAUpdateModule
    {
        private readonly IA_WorldState _world;
        private readonly IA_MapAnalyzer _map;
        private readonly Dictionary<Vector2Int, float> _threatBySector = new Dictionary<Vector2Int, float>();
        private readonly List<Transform> _priorityTargets = new List<Transform>();

        public float SectorSize = 30f;

        public IA_ThreatAnalyzer(IA_WorldState world, IA_MapAnalyzer map)
        {
            _world = world;
            _map = map;
        }

        public string Name
        {
            get { return "IA_ThreatAnalyzer"; }
        }

        public float Interval
        {
            get { return 1.10f; }
        }

        public float BudgetMs
        {
            get { return 0.40f; }
        }

        public void Tick(float now, float deltaTime)
        {
            _threatBySector.Clear();
            _priorityTargets.Clear();

            for (int i = 0; i < _world.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _world.VisibleEnemies[i];
                if (obs == null)
                {
                    continue;
                }

                Vector2Int sector = ToSector(obs.Position);
                float value;
                if (!_threatBySector.TryGetValue(sector, out value))
                {
                    value = 0f;
                }

                value += obs.ThreatScore;
                _threatBySector[sector] = value;

                if (obs.Transform != null && (obs.IsStructure || obs.ThreatScore >= 40f))
                {
                    _priorityTargets.Add(obs.Transform);
                }
            }

        }

        public float EvaluateThreat(Vector3 worldPosition, IA_Domain domain)
        {
            if (_world.VisibleEnemies.Count == 0)
            {
                return 0f;
            }

            Vector2Int sector = ToSector(worldPosition);
            float sectorThreat;
            if (!_threatBySector.TryGetValue(sector, out sectorThreat))
            {
                sectorThreat = 0f;
            }

            IA_MapCell cell = _map.SampleCell(worldPosition);
            float terrainMultiplier = 1f;
            if (domain == IA_Domain.Land && cell.Terrain == IA_TerrainType.City)
            {
                terrainMultiplier = 1.15f;
            }
            else if (domain == IA_Domain.Naval && cell.Terrain == IA_TerrainType.Coast)
            {
                terrainMultiplier = 1.20f;
            }
            else if (domain == IA_Domain.Air && cell.Terrain == IA_TerrainType.Choke)
            {
                terrainMultiplier = 0.85f;
            }

            float localThreat = 0f;
            for (int i = 0; i < _world.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _world.VisibleEnemies[i];
                if (obs == null)
                {
                    continue;
                }

                Vector3 delta = Flatten(worldPosition) - Flatten(obs.Position);
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance > 32400f)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(sqrDistance);
                float weight = Mathf.Clamp01(1f - (distance / 180f));
                float domainFactor = domain == obs.Domain ? 1.2f : 1f;
                localThreat += obs.ThreatScore * weight * domainFactor;
            }

            return (sectorThreat * 0.65f + localThreat * 0.35f) * terrainMultiplier;
        }

        public Vector3 GetHighestThreatSectorCenter()
        {
            float maxThreat = 0f;
            Vector2Int bestSector = Vector2Int.zero;
            foreach (var pair in _threatBySector)
            {
                if (pair.Value > maxThreat)
                {
                    maxThreat = pair.Value;
                    bestSector = pair.Key;
                }
            }

            return new Vector3(bestSector.x * SectorSize, _world.BaseCenter.y, bestSector.y * SectorSize);
        }

        public List<Transform> GetPriorityTargets(int maxTargets)
        {
            var output = new List<Transform>();
            int limit = Mathf.Clamp(maxTargets, 1, 16);
            for (int i = 0; i < _priorityTargets.Count && output.Count < limit; i++)
            {
                Transform target = _priorityTargets[i];
                if (target != null && !output.Contains(target))
                {
                    output.Add(target);
                }
            }

            return output;
        }

        private Vector2Int ToSector(Vector3 worldPosition)
        {
            float size = Mathf.Max(4f, SectorSize);
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x / size), Mathf.RoundToInt(worldPosition.z / size));
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
