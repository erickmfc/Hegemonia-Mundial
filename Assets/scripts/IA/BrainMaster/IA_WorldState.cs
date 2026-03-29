using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_WorldState : IIAUpdateModule
    {
        private const int MaxMobileVisibilityProviders = 24;
        private const int MaxStructureVisibilityProviders = 8;
        private readonly int _teamId;
        private readonly List<IdentidadeUnidade> _globalIdentityCache = new List<IdentidadeUnidade>();
        private readonly Dictionary<int, IA_EnemyObservation> _enemyMemoryById = new Dictionary<int, IA_EnemyObservation>();
        private Transform _baseProbe;
        private Vector3 _fallbackCenter;
        private bool _forceRefresh;
        private float _nextGlobalScanTime;

        public readonly List<GameObject> OwnUnits = new List<GameObject>();
        public readonly List<GameObject> OwnStructures = new List<GameObject>();
        public readonly List<GameObject> OwnCombatUnits = new List<GameObject>();
        public readonly List<IA_VisibilityProvider> VisibilityProviders = new List<IA_VisibilityProvider>();
        public readonly List<IA_EnemyObservation> VisibleEnemies = new List<IA_EnemyObservation>();

        public Vector3 BaseCenter { get; private set; }
        public float LastScanTime { get; private set; }

        public IA_WorldState(int teamId)
        {
            _teamId = teamId;
            _fallbackCenter = Vector3.zero;
        }

        public string Name
        {
            get { return "IA_WorldState"; }
        }

        public float Interval
        {
            get { return 0.80f; }
        }

        public float BudgetMs
        {
            get { return 0.60f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (_forceRefresh || now >= _nextGlobalScanTime)
            {
                RefreshOwnedAndGlobalCache(now);
                _nextGlobalScanTime = now + 5.75f;
                _forceRefresh = false;
            }

            RefreshVisibleEnemies(now);
            CleanupMemory(now, 120f);
        }

        public void MarkDirty()
        {
            _forceRefresh = true;
        }

        public void SetFallbackCenter(Vector3 center)
        {
            _fallbackCenter = center;
        }

        public List<IA_EnemyObservation> GetEnemyMemory(float maxAgeSeconds)
        {
            float now = Time.time;
            var output = new List<IA_EnemyObservation>();
            foreach (var pair in _enemyMemoryById)
            {
                IA_EnemyObservation obs = pair.Value;
                if (obs != null && now - obs.LastSeenTime <= maxAgeSeconds)
                {
                    output.Add(obs);
                }
            }

            return output;
        }

        public Transform GetNearestVisibleEnemy(Vector3 fromPosition, IA_Domain preferredDomain)
        {
            float bestDistance = float.MaxValue;
            Transform best = null;
            for (int i = 0; i < VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = VisibleEnemies[i];
                if (obs == null || obs.Transform == null)
                {
                    continue;
                }

                float scoreDistance = Vector3.Distance(Flatten(fromPosition), Flatten(obs.Position));
                if (preferredDomain != obs.Domain)
                {
                    scoreDistance *= 1.15f;
                }

                if (scoreDistance < bestDistance)
                {
                    bestDistance = scoreDistance;
                    best = obs.Transform;
                }
            }

            return best;
        }

        public bool TryGetEnemyStrategicAnchor(Vector3 fromPosition, out Vector3 position)
        {
            position = Vector3.zero;
            float bestScore = float.MinValue;

            for (int i = 0; i < _globalIdentityCache.Count; i++)
            {
                IdentidadeUnidade id = _globalIdentityCache[i];
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID == 0 || id.teamID == _teamId)
                {
                    continue;
                }

                GameObject obj = id.gameObject;
                bool isStructure = IsStructure(obj);
                IA_Domain domain = ClassifyDomain(obj);
                string name = IA_Text.Normalize(obj.name);
                float distance = Vector3.Distance(Flatten(fromPosition), Flatten(obj.transform.position));

                float score = isStructure ? 120f : 35f;
                if (domain == IA_Domain.Land)
                {
                    score += 10f;
                }

                if (name.Contains("prefeitura") || name.Contains("capital") || name.Contains("governo"))
                {
                    score += 180f;
                }
                else if (name.Contains("quartel general") || name.Contains("quartel_general") || name.Contains("hq"))
                {
                    score += 120f;
                }
                else if (name.Contains("aeroporto") || name.Contains("airport") || name.Contains("fabrica") || name.Contains("construtor") || name.Contains("estaleiro") || name.Contains("pier"))
                {
                    score += 70f;
                }

                score -= distance * 0.015f;
                if (score > bestScore)
                {
                    bestScore = score;
                    position = obj.transform.position;
                }
            }

            return bestScore > float.MinValue;
        }

        public int CountOwnByHint(params string[] hints)
        {
            if (hints == null || hints.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < OwnUnits.Count; i++)
            {
                GameObject unit = OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                bool matched = false;
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && name.Contains(hint))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshOwnedAndGlobalCache(float now)
        {
            OwnUnits.Clear();
            OwnStructures.Clear();
            OwnCombatUnits.Clear();
            VisibilityProviders.Clear();
            _globalIdentityCache.Clear();
            int mobileVisibilityBudget = MaxMobileVisibilityProviders;
            int structureVisibilityBudget = MaxStructureVisibilityProviders;

            IdentidadeUnidade[] identities = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _globalIdentityCache.Add(id);

                if (id.teamID != _teamId)
                {
                    continue;
                }

                bool structure = IsStructure(id.gameObject);
                if (structure)
                {
                    OwnStructures.Add(id.gameObject);
                }
                else
                {
                    OwnUnits.Add(id.gameObject);
                    if (!IsTransport(id.gameObject))
                    {
                        OwnCombatUnits.Add(id.gameObject);
                    }
                }

                if (!ShouldRegisterVisibilityProvider(id.gameObject, structure, ref mobileVisibilityBudget, ref structureVisibilityBudget))
                {
                    continue;
                }

                VisibilityProviders.Add(new IA_VisibilityProvider
                {
                    Source = id.transform,
                    Radius = ComputeVisionRadius(id.gameObject, structure)
                });
            }

            BaseCenter = ComputeBaseCenter();
            if (_baseProbe == null)
            {
                _baseProbe = CreateVirtualBaseProbe();
            }
            _baseProbe.position = BaseCenter;
            VisibilityProviders.Add(new IA_VisibilityProvider { Source = _baseProbe, Radius = 260f });

            LastScanTime = now;
        }

        private void RefreshVisibleEnemies(float now)
        {
            VisibleEnemies.Clear();
            if (VisibilityProviders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _globalIdentityCache.Count; i++)
            {
                IdentidadeUnidade id = _globalIdentityCache[i];
                if (id == null || !id.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (id.teamID == 0 || id.teamID == _teamId)
                {
                    continue;
                }

                if (!IsVisible(id.transform.position))
                {
                    continue;
                }

                int instanceId = id.GetInstanceID();
                IA_EnemyObservation obs;
                if (!_enemyMemoryById.TryGetValue(instanceId, out obs))
                {
                    obs = new IA_EnemyObservation
                    {
                        InstanceId = instanceId
                    };
                    _enemyMemoryById.Add(instanceId, obs);
                }

                obs.Transform = id.transform;
                obs.Position = id.transform.position;
                obs.UnitName = id.name;
                obs.Domain = ClassifyDomain(id.gameObject);
                obs.IsStructure = IsStructure(id.gameObject);
                obs.ThreatScore = EstimateThreat(obs.UnitName, obs.Domain, obs.IsStructure);
                obs.LastSeenTime = now;
                VisibleEnemies.Add(obs);
            }
        }

        private void CleanupMemory(float now, float maxAge)
        {
            List<int> stale = null;
            foreach (var pair in _enemyMemoryById)
            {
                if (now - pair.Value.LastSeenTime > maxAge)
                {
                    if (stale == null)
                    {
                        stale = new List<int>();
                    }

                    stale.Add(pair.Key);
                }
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                _enemyMemoryById.Remove(stale[i]);
            }
        }

        private bool IsVisible(Vector3 position)
        {
            Vector3 flatTarget = Flatten(position);
            for (int i = 0; i < VisibilityProviders.Count; i++)
            {
                IA_VisibilityProvider provider = VisibilityProviders[i];
                if (provider == null || provider.Source == null)
                {
                    continue;
                }

                float radius = Mathf.Max(20f, provider.Radius);
                Vector3 delta = Flatten(provider.Source.position) - flatTarget;
                if (delta.sqrMagnitude <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldRegisterVisibilityProvider(GameObject obj, bool structure, ref int mobileBudget, ref int structureBudget)
        {
            if (obj == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(obj.name);
            bool radar = n.Contains("radar");
            bool highValueMobile = obj.GetComponent<ControleAviao>() != null
                                   || obj.GetComponent<ControleAviaoCaca>() != null
                                   || obj.GetComponent<Helicoptero>() != null
                                   || obj.GetComponent<ControleNavioRealista>() != null
                                   || obj.GetComponent<ControleSubmarino>() != null;

            if (radar)
            {
                return true;
            }

            if (structure)
            {
                if (structureBudget <= 0)
                {
                    return false;
                }

                structureBudget--;
                return true;
            }

            if (highValueMobile)
            {
                if (mobileBudget <= 0)
                {
                    return false;
                }

                mobileBudget--;
                return true;
            }

            if (mobileBudget <= 0 || (obj.GetInstanceID() & 1) != 0)
            {
                return false;
            }

            mobileBudget--;
            return true;
        }

        private float ComputeVisionRadius(GameObject obj, bool structure)
        {
            string n = IA_Text.Normalize(obj.name);
            if (n.Contains("radar"))
            {
                return 260f;
            }

            if (structure)
            {
                return 110f;
            }

            if (obj.GetComponent<ControleAviao>() != null || obj.GetComponent<ControleAviaoCaca>() != null)
            {
                return 230f;
            }

            if (obj.GetComponent<Helicoptero>() != null)
            {
                return 200f;
            }

            if (obj.GetComponent<ControleNavioRealista>() != null || obj.GetComponent<ControleSubmarino>() != null)
            {
                return 210f;
            }

            return 190f;
        }

        private Vector3 ComputeBaseCenter()
        {
            if (OwnStructures.Count == 0)
            {
                if (OwnUnits.Count > 0 && OwnUnits[0] != null)
                {
                    return OwnUnits[0].transform.position;
                }

                return _fallbackCenter;
            }

            Vector3 preferred;
            if (TryFindStructureAnchor(out preferred, "prefeitura", "governo", "capital", "quartel general", "quartel_general", "hq"))
            {
                return ComputeClusterCenter(preferred, 380f);
            }

            if (TryFindStructureAnchor(out preferred, "tenda", "barraca", "construtor", "fabrica", "armazem", "radar"))
            {
                return ComputeClusterCenter(preferred, 340f);
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                sum += structure.transform.position;
                count++;
            }

            if (count == 0)
            {
                return _fallbackCenter;
            }

            return sum / count;
        }

        private bool TryFindStructureAnchor(out Vector3 position, params string[] hints)
        {
            position = Vector3.zero;
            Vector3 reference = GetAnchorReference();
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string n = IA_Text.Normalize(structure.name);
                bool match = false;
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && n.Contains(hint))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                float dist = (Flatten(structure.transform.position) - Flatten(reference)).sqrMagnitude;
                if (!found || dist < best)
                {
                    best = dist;
                    position = structure.transform.position;
                    found = true;
                }
            }

            return found;
        }

        private Vector3 ComputeClusterCenter(Vector3 anchor, float radius)
        {
            float radiusSqr = radius * radius;
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < OwnStructures.Count; i++)
            {
                GameObject structure = OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                Vector3 pos = structure.transform.position;
                if ((Flatten(pos) - Flatten(anchor)).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                sum += pos;
                count++;
            }

            if (count == 0)
            {
                return anchor;
            }

            return sum / count;
        }

        private Vector3 GetAnchorReference()
        {
            if (_fallbackCenter != Vector3.zero)
            {
                return _fallbackCenter;
            }

            if (OwnUnits.Count > 0 && OwnUnits[0] != null)
            {
                return OwnUnits[0].transform.position;
            }

            if (OwnStructures.Count > 0 && OwnStructures[0] != null)
            {
                return OwnStructures[0].transform.position;
            }

            return Vector3.zero;
        }

        private Transform CreateVirtualBaseProbe()
        {
            GameObject holder = new GameObject("IA_WorldState_Probe");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = BaseCenter;
            return holder.transform;
        }

        private static IA_Domain ClassifyDomain(GameObject obj)
        {
            string n = IA_Text.Normalize(obj.name);
            if (obj.GetComponent<ControleAviao>() != null || obj.GetComponent<ControleAviaoCaca>() != null || obj.GetComponent<Helicoptero>() != null || n.Contains("heli") || n.Contains("aviao") || n.Contains("fa1"))
            {
                return IA_Domain.Air;
            }

            if (obj.GetComponent<ControleNavioRealista>() != null || obj.GetComponent<ControleSubmarino>() != null || n.Contains("navio") || n.Contains("sub") || n.Contains("corveta"))
            {
                return IA_Domain.Naval;
            }

            return IA_Domain.Land;
        }

        private static bool IsStructure(GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            string n = IA_Text.Normalize(obj.name);
            bool hasAgent = obj.GetComponent<NavMeshAgent>() != null;
            bool mobileByScript = obj.GetComponent<ControleAviao>() != null
                                  || obj.GetComponent<ControleAviaoCaca>() != null
                                  || obj.GetComponent<Helicoptero>() != null
                                  || obj.GetComponent<ControleNavioRealista>() != null
                                  || obj.GetComponent<ControleSubmarino>() != null
                                  || obj.GetComponent<ControleUnidade>() != null;
            bool explicitStructure = n.Contains("prefeitura")
                                     || n.Contains("quartel")
                                     || n.Contains("fabrica")
                                     || n.Contains("refinaria")
                                     || n.Contains("torre")
                                     || n.Contains("radar")
                                     || n.Contains("muro")
                                     || n.Contains("estaleiro")
                                     || n.Contains("pier")
                                     || n.Contains("plataforma")
                                     || n.Contains("aeroporto")
                                     || n.Contains("heliporto");
            return explicitStructure || (!hasAgent && !mobileByScript);
        }

        private static bool IsTransport(GameObject obj)
        {
            string n = IA_Text.Normalize(obj.name);
            return n.Contains("transporte")
                   || n.Contains("truck")
                   || n.Contains("caminhao")
                   || n.Contains("hover");
        }

        private static float EstimateThreat(string unitName, IA_Domain domain, bool structure)
        {
            float value = structure ? 35f : 15f;
            string name = IA_Text.Normalize(unitName);

            if (name.Contains("tank") || name.Contains("mbt") || name.Contains("south"))
            {
                value += 30f;
            }
            if (name.Contains("artilh") || name.Contains("hack"))
            {
                value += 25f;
            }
            if (name.Contains("destroy") || name.Contains("ironclad") || name.Contains("vindicator"))
            {
                value += 40f;
            }
            if (name.Contains("sub"))
            {
                value += 35f;
            }
            if (name.Contains("fa1") || name.Contains("caca"))
            {
                value += 28f;
            }

            if (domain == IA_Domain.Air)
            {
                value += 8f;
            }
            else if (domain == IA_Domain.Naval)
            {
                value += 12f;
            }

            return value;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
