using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    public sealed class AISovereignPerception
    {
        public sealed class EnemyContact
        {
            public int TeamId;
            public int InstanceId;
            public string Name = string.Empty;
            public Transform Transform;
            public Vector3 Position;
            public AISovereignDomain Domain;
            public bool IsStructure;
            public float ThreatScore;
            public float LastSeenTime;
        }

        private readonly int _teamId;
        private int[] _alliedTeams = System.Array.Empty<int>();
        private readonly List<IdentidadeUnidade> _globalUnits = new List<IdentidadeUnidade>(256);
        private readonly List<EnemyContact> _visibleEnemies = new List<EnemyContact>(128);
        private readonly Dictionary<int, EnemyContact> _enemyMemory = new Dictionary<int, EnemyContact>(256);
        private readonly List<int> _staleMemory = new List<int>(64);
        private readonly List<GameObject> _ownUnits = new List<GameObject>(128);
        private readonly List<GameObject> _ownStructures = new List<GameObject>(128);
        private readonly List<Transform> _visibilityProviders = new List<Transform>(32);
        private readonly List<float> _visibilityRadii = new List<float>(32);

        public IReadOnlyList<GameObject> OwnUnits => _ownUnits;
        public IReadOnlyList<GameObject> OwnStructures => _ownStructures;
        public IReadOnlyList<EnemyContact> VisibleEnemies => _visibleEnemies;
        public Vector3 BaseCenter { get; private set; }
        public Vector3 LastKnownEnemyAnchor { get; private set; }
        public bool UnderThreat { get; private set; }
        public bool EnemyAcrossOcean { get; private set; }
        public int OwnLandUnits { get; private set; }
        public int OwnNavalUnits { get; private set; }
        public int OwnAirUnits { get; private set; }
        public int VisibleEnemyLand { get; private set; }
        public int VisibleEnemyNaval { get; private set; }
        public int VisibleEnemyAir { get; private set; }
        public int RadarCount { get; private set; }
        public int AirportCount { get; private set; }
        public int ShipyardCount { get; private set; }
        public int PlatformCount { get; private set; }
        public int FactoryCount { get; private set; }
        public int WarehouseCount { get; private set; }
        public int BarracksCount { get; private set; }
        public int NavalTransportCount { get; private set; }
        public int FighterCount { get; private set; }

        public AISovereignPerception(int teamId)
        {
            _teamId = teamId;
        }

        public void ConfigureAllies(int[] alliedTeams)
        {
            _alliedTeams = alliedTeams ?? System.Array.Empty<int>();
        }

        public void Refresh(float now)
        {
            RegistroEntidadesJogo.FillUnidades(_globalUnits);
            _ownUnits.Clear();
            _ownStructures.Clear();
            _visibilityProviders.Clear();
            _visibilityRadii.Clear();
            _visibleEnemies.Clear();

            BaseCenter = Vector3.zero;
            OwnLandUnits = 0;
            OwnNavalUnits = 0;
            OwnAirUnits = 0;
            VisibleEnemyLand = 0;
            VisibleEnemyNaval = 0;
            VisibleEnemyAir = 0;
            RadarCount = 0;
            AirportCount = 0;
            ShipyardCount = 0;
            PlatformCount = 0;
            FactoryCount = 0;
            WarehouseCount = 0;
            BarracksCount = 0;
            NavalTransportCount = 0;
            FighterCount = 0;

            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < _globalUnits.Count; i++)
            {
                IdentidadeUnidade id = _globalUnits[i];
                if (id == null || id.gameObject == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID == _teamId)
                {
                    ClassifyOwned(id);
                    sum += id.transform.position;
                    count++;
                }
            }

            BaseCenter = count > 0 ? (sum / count) : Vector3.zero;

            for (int i = 0; i < _globalUnits.Count; i++)
            {
                IdentidadeUnidade id = _globalUnits[i];
                if (id == null || id.gameObject == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID <= 0 || id.teamID == _teamId || IsAllied(id.teamID))
                {
                    continue;
                }

                if (!IsVisible(id.transform.position))
                {
                    continue;
                }

                EnemyContact contact = BuildEnemyContact(id, now);
                _visibleEnemies.Add(contact);
                _enemyMemory[contact.InstanceId] = contact;

                switch (contact.Domain)
                {
                    case AISovereignDomain.Air:
                        VisibleEnemyAir++;
                        break;
                    case AISovereignDomain.Naval:
                        VisibleEnemyNaval++;
                        break;
                    default:
                        VisibleEnemyLand++;
                        break;
                }
            }

            CleanupMemory(now, 150f);

            UnderThreat = false;
            EnemyAcrossOcean = false;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < _visibleEnemies.Count; i++)
            {
                EnemyContact contact = _visibleEnemies[i];
                float distance = DistanceFlat(BaseCenter, contact.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    LastKnownEnemyAnchor = contact.Position;
                }
                if (distance <= 480f)
                {
                    UnderThreat = true;
                }
            }

            if (_visibleEnemies.Count == 0)
            {
                TryGetEnemyStrategicAnchor(out Vector3 anchor);
                LastKnownEnemyAnchor = anchor;
            }

            if (LastKnownEnemyAnchor != Vector3.zero && BaseCenter != Vector3.zero)
            {
                EnemyAcrossOcean = IsLikelyAcrossOcean(BaseCenter, LastKnownEnemyAnchor, 900f);
            }
        }

        public bool TryGetEnemyStrategicAnchor(out Vector3 anchor)
        {
            anchor = LastKnownEnemyAnchor;
            float bestScore = float.MinValue;
            bool found = false;

            foreach (KeyValuePair<int, EnemyContact> pair in _enemyMemory)
            {
                EnemyContact contact = pair.Value;
                if (contact == null)
                {
                    continue;
                }

                float score = contact.ThreatScore + (contact.IsStructure ? 22f : 6f);
                if (score > bestScore)
                {
                    bestScore = score;
                    anchor = contact.Position;
                    found = true;
                }
            }

            return found;
        }

        public bool TryGetBestTarget(AIPresidentProfile profile, out EnemyContact target)
        {
            target = null;
            float bestScore = float.MinValue;

            foreach (KeyValuePair<int, EnemyContact> pair in _enemyMemory)
            {
                EnemyContact contact = pair.Value;
                if (contact == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(contact.Name);
                float score = contact.ThreatScore;
                if (normalized.Contains("radar")) score += 35f + (profile != null ? profile.Aggression * 8f : 0f);
                if (normalized.Contains("aeroporto") || normalized.Contains("airport") || normalized.Contains("pista")) score += 32f;
                if (normalized.Contains("estaleiro") || normalized.Contains("shipyard") || normalized.Contains("pier")) score += 30f;
                if (normalized.Contains("plataforma") || normalized.Contains("petro") || normalized.Contains("tanker")) score += 28f;
                if (normalized.Contains("armazem") || normalized.Contains("log") || normalized.Contains("transporte")) score += 24f;
                if (normalized.Contains("prefeitura") || normalized.Contains("governo") || normalized.Contains("capital") || normalized.Contains("hq")) score += 26f;

                if (score > bestScore)
                {
                    bestScore = score;
                    target = contact;
                }
            }

            return target != null;
        }

        private void ClassifyOwned(IdentidadeUnidade id)
        {
            GameObject obj = id.gameObject;
            IA_ConstructionMetadata metadata = obj.GetComponent<IA_ConstructionMetadata>();
            bool isStructure = id.tipoUnidade == TipoUnidade.Estrutura || (metadata != null && metadata.IsStructure);
            if (isStructure)
            {
                _ownStructures.Add(obj);
            }
            else
            {
                _ownUnits.Add(obj);
            }

            if (metadata != null)
            {
                if (metadata.IsRadar) RadarCount++;
                if (metadata.IsAirport || metadata.IsMilitaryAirport || metadata.IsCommercialAirport) AirportCount++;
                if (metadata.IsShipyard || metadata.IsPier) ShipyardCount++;
                if (metadata.IsPlatform) PlatformCount++;
                if (metadata.IsFactory) FactoryCount++;
                if (metadata.IsWarehouse) WarehouseCount++;
                if (metadata.IsBarracks) BarracksCount++;
                if (metadata.IsNavalTransport) NavalTransportCount++;
                if (metadata.IsFighterAircraft) FighterCount++;
            }
            else
            {
                string normalized = IA_Text.Normalize(obj.name);
                if (normalized.Contains("radar")) RadarCount++;
                if (normalized.Contains("aeroporto") || normalized.Contains("airport")) AirportCount++;
                if (normalized.Contains("estaleiro") || normalized.Contains("pier")) ShipyardCount++;
                if (normalized.Contains("plataforma")) PlatformCount++;
                if (normalized.Contains("fabrica") || normalized.Contains("construtor")) FactoryCount++;
                if (normalized.Contains("armazem")) WarehouseCount++;
                if (normalized.Contains("quartel") || normalized.Contains("tenda")) BarracksCount++;
                if (normalized.Contains("transporte") && normalized.Contains("nav")) NavalTransportCount++;
                if (normalized.Contains("caca") || normalized.Contains("fighter")) FighterCount++;
            }

            AISovereignDomain domain = ResolveDomain(id, metadata);
            switch (domain)
            {
                case AISovereignDomain.Air:
                    OwnAirUnits++;
                    break;
                case AISovereignDomain.Naval:
                    OwnNavalUnits++;
                    break;
                default:
                    OwnLandUnits++;
                    break;
            }

            if (RadarCount < 8 && (metadata != null && metadata.IsRadar || (!isStructure && domain != AISovereignDomain.Land)))
            {
                _visibilityProviders.Add(obj.transform);
                _visibilityRadii.Add(ResolveVisionRadius(id, metadata));
            }
            else if (_visibilityProviders.Count < 24 && !isStructure)
            {
                _visibilityProviders.Add(obj.transform);
                _visibilityRadii.Add(ResolveVisionRadius(id, metadata));
            }
        }

        private EnemyContact BuildEnemyContact(IdentidadeUnidade id, float now)
        {
            IA_ConstructionMetadata metadata = id.GetComponent<IA_ConstructionMetadata>();
            EnemyContact contact = new EnemyContact
            {
                TeamId = id.teamID,
                InstanceId = id.GetInstanceID(),
                Name = id.name,
                Transform = id.transform,
                Position = id.transform.position,
                Domain = ResolveDomain(id, metadata),
                IsStructure = id.tipoUnidade == TipoUnidade.Estrutura || (metadata != null && metadata.IsStructure),
                LastSeenTime = now,
                ThreatScore = EstimateThreat(id, metadata)
            };
            return contact;
        }

        private static AISovereignDomain ResolveDomain(IdentidadeUnidade id, IA_ConstructionMetadata metadata)
        {
            if (metadata != null)
            {
                if (metadata.IsAirDomain || metadata.IsAircraft || metadata.IsHelicopter)
                {
                    return AISovereignDomain.Air;
                }
                if (metadata.IsNavalDomain || metadata.IsShipyard || metadata.IsPlatform || metadata.IsPier || metadata.IsOilTanker || metadata.IsNavalTransport)
                {
                    return AISovereignDomain.Naval;
                }
            }

            switch (id.tipoUnidade)
            {
                case TipoUnidade.Aereo:
                    return AISovereignDomain.Air;
                case TipoUnidade.Naval:
                    return AISovereignDomain.Naval;
                default:
                    return AISovereignDomain.Land;
            }
        }

        private static float EstimateThreat(IdentidadeUnidade id, IA_ConstructionMetadata metadata)
        {
            float threat = 10f;
            if (id == null)
            {
                return threat;
            }

            switch (id.tipoUnidade)
            {
                case TipoUnidade.Aereo:
                    threat += 26f;
                    break;
                case TipoUnidade.Naval:
                    threat += 24f;
                    break;
                case TipoUnidade.Veiculo:
                    threat += 18f;
                    break;
                case TipoUnidade.Estrutura:
                    threat += 12f;
                    break;
                default:
                    threat += 8f;
                    break;
            }

            if (metadata != null)
            {
                if (metadata.IsRadar) threat += 18f;
                if (metadata.IsMilitaryAirport || metadata.IsAirport) threat += 20f;
                if (metadata.IsShipyard || metadata.IsPier) threat += 18f;
                if (metadata.IsPlatform) threat += 22f;
                if (metadata.IsWarehouse || metadata.IsFactory) threat += 14f;
                if (metadata.IsCore) threat += 16f;
            }

            string normalized = IA_Text.Normalize(id.name);
            if (normalized.Contains("carrier") || normalized.Contains("porta avioes")) threat += 18f;
            if (normalized.Contains("sub")) threat += 16f;
            if (normalized.Contains("tank")) threat += 8f;
            return threat;
        }

        private bool IsVisible(Vector3 position)
        {
            if (_visibilityProviders.Count == 0)
            {
                return false;
            }

            Vector3 flatPosition = Flatten(position);
            for (int i = 0; i < _visibilityProviders.Count; i++)
            {
                Transform provider = _visibilityProviders[i];
                if (provider == null)
                {
                    continue;
                }

                float radius = _visibilityRadii[i];
                if (Vector3.Distance(Flatten(provider.position), flatPosition) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveVisionRadius(IdentidadeUnidade id, IA_ConstructionMetadata metadata)
        {
            if (metadata != null)
            {
                if (metadata.IsRadar) return 650f;
                if (metadata.IsAirport || metadata.IsShipyard || metadata.IsPlatform) return 340f;
            }

            switch (id.tipoUnidade)
            {
                case TipoUnidade.Aereo:
                    return 340f;
                case TipoUnidade.Naval:
                    return 280f;
                case TipoUnidade.Estrutura:
                    return 210f;
                default:
                    return 180f;
            }
        }

        private void CleanupMemory(float now, float maxAge)
        {
            _staleMemory.Clear();
            foreach (KeyValuePair<int, EnemyContact> pair in _enemyMemory)
            {
                EnemyContact contact = pair.Value;
                if (contact == null || now - contact.LastSeenTime > maxAge)
                {
                    _staleMemory.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleMemory.Count; i++)
            {
                _enemyMemory.Remove(_staleMemory[i]);
            }
        }

        private bool IsAllied(int teamId)
        {
            for (int i = 0; i < _alliedTeams.Length; i++)
            {
                if (_alliedTeams[i] == teamId)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistanceFlat(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(Flatten(a), Flatten(b));
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static bool IsLikelyAcrossOcean(Vector3 from, Vector3 to, float minimumDistance)
        {
            float distance = DistanceFlat(from, to);
            if (distance < minimumDistance)
            {
                return false;
            }

            int steps = Mathf.Clamp(Mathf.RoundToInt(distance / 48f), 8, 48);
            int waterHits = 0;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 sample = Vector3.Lerp(from, to, t);
                if (RegistroSuperficieMapa.TryClassify(sample, out ClassificacaoSuperficieMapa classificacao, out _))
                {
                    if (classificacao == ClassificacaoSuperficieMapa.Agua || classificacao == ClassificacaoSuperficieMapa.Costa)
                    {
                        waterHits++;
                    }
                }
            }

            return waterHits > steps * 0.35f;
        }
    }
}
