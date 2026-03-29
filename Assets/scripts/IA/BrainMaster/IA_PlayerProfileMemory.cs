using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_PlayerProfileMemory : IIAUpdateModule
    {
        private sealed class Sample
        {
            public float Time;
            public IA_Domain Domain;
            public Vector3 Position;
            public bool IsStructure;
            public string Name;
        }

        private readonly IA_WorldState _world;
        private readonly List<Sample> _samples = new List<Sample>();
        private readonly HashSet<int> _seenThisTick = new HashSet<int>();

        private float _landUsage;
        private float _navalUsage;
        private float _airUsage;
        private float _economyFocus;
        private float _aggressionFocus;
        private float _rushScore;
        private float _flankFrequency;
        private float _centerFrequency;
        private float _coastPressure;
        private float _heliUsage;
        private float _tankUsage;
        private float _infantryUsage;
        private float _fleetUsage;

        public IA_PlayerProfileMemory(IA_WorldState world)
        {
            _world = world;
        }

        public string Name
        {
            get { return "IA_PlayerProfileMemory"; }
        }

        public float Interval
        {
            get { return 1.10f; }
        }

        public float BudgetMs
        {
            get { return 0.55f; }
        }

        public void Tick(float now, float deltaTime)
        {
            _seenThisTick.Clear();

            for (int i = 0; i < _world.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation obs = _world.VisibleEnemies[i];
                if (obs == null)
                {
                    continue;
                }

                if (_seenThisTick.Contains(obs.InstanceId))
                {
                    continue;
                }

                _samples.Add(new Sample
                {
                    Time = now,
                    Domain = obs.Domain,
                    Position = obs.Position,
                    IsStructure = obs.IsStructure,
                    Name = IA_Text.Normalize(obs.UnitName)
                });
                _seenThisTick.Add(obs.InstanceId);
            }

            CleanupOldSamples(now, 150f);
            RebuildMetrics(now);
        }

        public IA_CounterPlan BuildCounterPlan()
        {
            return new IA_CounterPlan
            {
                LandWeight = Mathf.Clamp01(_landUsage + (_tankUsage * 0.4f)),
                NavalWeight = Mathf.Clamp01(_navalUsage + (_fleetUsage * 0.45f)),
                AirWeight = Mathf.Clamp01(_airUsage + (_heliUsage * 0.35f)),
                AntiRush = _rushScore > 0.40f || _aggressionFocus > 0.70f,
                ReinforceCoast = _coastPressure > 0.42f || _navalUsage > 0.34f,
                ReinforceCenter = _centerFrequency > 0.50f,
                ReinforceFlanks = _flankFrequency > 0.38f
            };
        }

        public float AggressionFocus
        {
            get { return _aggressionFocus; }
        }

        public float EconomyFocus
        {
            get { return _economyFocus; }
        }

        public float RushScore
        {
            get { return _rushScore; }
        }

        public float AirUsage
        {
            get { return _airUsage; }
        }

        public float NavalUsage
        {
            get { return _navalUsage; }
        }

        private void CleanupOldSamples(float now, float maxAge)
        {
            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                if (now - _samples[i].Time > maxAge)
                {
                    _samples.RemoveAt(i);
                }
            }
        }

        private void RebuildMetrics(float now)
        {
            int land = 0;
            int naval = 0;
            int air = 0;
            int structures = 0;
            int combat = 0;
            int flankHits = 0;
            int centerHits = 0;
            int coastHits = 0;
            int heli = 0;
            int tank = 0;
            int infantry = 0;
            int fleet = 0;
            int rushEvents = 0;

            Vector3 baseCenter = _world.BaseCenter;
            Vector3 baseToEnemyAxis = GetEnemyAxis(baseCenter);

            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                if (sample.IsStructure)
                {
                    structures++;
                }
                else
                {
                    combat++;
                }

                if (sample.Domain == IA_Domain.Air) air++;
                else if (sample.Domain == IA_Domain.Naval) naval++;
                else land++;

                if (sample.Name.Contains("heli") || sample.Name.Contains("ray") || sample.Name.Contains("vans"))
                {
                    heli++;
                }
                if (sample.Name.Contains("tank") || sample.Name.Contains("mbt") || sample.Name.Contains("south") || sample.Name.Contains("arthur") || sample.Name.Contains("c1"))
                {
                    tank++;
                }
                if (sample.Name.Contains("sold") || sample.Name.Contains("rifle") || sample.Name.Contains("infan"))
                {
                    infantry++;
                }
                if (sample.Name.Contains("navio") || sample.Name.Contains("destroy") || sample.Name.Contains("corveta") || sample.Name.Contains("sub"))
                {
                    fleet++;
                }

                Vector3 toSample = Flatten(sample.Position) - Flatten(baseCenter);
                float angle = Vector3.SignedAngle(baseToEnemyAxis, toSample, Vector3.up);
                if (Mathf.Abs(angle) <= 20f)
                {
                    centerHits++;
                }
                else if (Mathf.Abs(angle) >= 45f)
                {
                    flankHits++;
                }

                if (sample.Domain == IA_Domain.Naval || sample.Name.Contains("coast") || sample.Name.Contains("pier") || sample.Name.Contains("plataforma"))
                {
                    coastHits++;
                }

                bool earlyWindow = sample.Time <= now - 20f && sample.Time >= now - 120f;
                float distToBase = Vector3.Distance(Flatten(sample.Position), Flatten(baseCenter));
                if (!sample.IsStructure && earlyWindow && distToBase < 220f)
                {
                    rushEvents++;
                }
            }

            int total = Mathf.Max(1, land + naval + air);
            _landUsage = land / (float)total;
            _navalUsage = naval / (float)total;
            _airUsage = air / (float)total;

            int econDenom = Mathf.Max(1, structures + combat);
            _economyFocus = structures / (float)econDenom;
            _aggressionFocus = combat / (float)econDenom;
            _rushScore = Mathf.Clamp01(rushEvents / 16f);

            int angles = Mathf.Max(1, flankHits + centerHits);
            _flankFrequency = flankHits / (float)angles;
            _centerFrequency = centerHits / (float)angles;

            int coastDenom = Mathf.Max(1, combat);
            _coastPressure = coastHits / (float)coastDenom;

            int styleDenom = Mathf.Max(1, combat);
            _heliUsage = heli / (float)styleDenom;
            _tankUsage = tank / (float)styleDenom;
            _infantryUsage = infantry / (float)styleDenom;
            _fleetUsage = fleet / (float)styleDenom;
        }

        private Vector3 GetEnemyAxis(Vector3 baseCenter)
        {
            List<IA_EnemyObservation> memory = _world.GetEnemyMemory(80f);
            if (memory.Count == 0)
            {
                return Vector3.forward;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < memory.Count; i++)
            {
                IA_EnemyObservation obs = memory[i];
                if (obs == null)
                {
                    continue;
                }

                sum += (Flatten(obs.Position) - Flatten(baseCenter));
                count++;
            }

            if (count == 0)
            {
                return Vector3.forward;
            }

            Vector3 axis = sum / count;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f)
            {
                return Vector3.forward;
            }

            return axis.normalized;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
