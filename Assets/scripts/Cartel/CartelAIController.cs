using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Cartel
{
    public enum CartelControllerState
    {
        Disabled,
        SelectingBase,
        BuildingBase,
        Operational,
        WaitingForManualCreate
    }

    public enum CartelOperationPhase
    {
        None,
        LeaveBase,
        Route,
        Patrol,
        ApproachingTanker,
        Robbery,
        Escape,
        IslandArrival,
        CoastalMeeting,
        TargetArrival,
        AttackDeploy,
        Attack,
        AttackEscape,
        ReturnBase,
        Storage,
        Parking
    }

    [Serializable]
    public sealed class CartelPrefabSet
    {
        [Header("Base")]
        public GameObject BasePrefab;

        [Header("Terrestre")]
        public GameObject GroundMemberPrefab;
        public GameObject GroundVehiclePrefab;

        [Header("Maritimo")]
        public GameObject MaritimeMemberPrefab;
        public GameObject PirateBoatPrefab;
    }

    [Serializable]
    public sealed class CartelBaseRuntime
    {
        public string BaseId;
        public string CountryId;
        public CartelManualCreate Candidate;
        public CartelManualCreate Area;
        public CartelManualCreate FuelStorage;
        public CartelManualCreate BaseExit;
        public CartelManualCreate GroundSpawn;
        public CartelManualCreate MaritimeSpawn;
        public GameObject Root;
        public float FuelStock;
        public bool InitialUnitsCreated;
        public int CompletedMissions;
        public readonly List<GameObject> GroundMembers = new List<GameObject>();
        public readonly List<GameObject> GroundVehicles = new List<GameObject>();
        public readonly List<GameObject> MaritimeMembers = new List<GameObject>();
        public readonly List<GameObject> Boats = new List<GameObject>();
    }

    /// <summary>
    /// Controlador do cartel. Todas as decisoes espaciais passam por
    /// CartelManualCreate; este componente nunca gera um destino aleatorio
    /// fora de um Create configurado.
    /// </summary>
    [AddComponentMenu("Hegemonia/Cartel/IA do Cartel")]
    public sealed class CartelAIController : MonoBehaviour
    {
        [Header("Identidade")]
        [Min(1)] public int CartelTeamId = 9;
        public string InitialCountryId = "Pais01";
        public bool StartAutomatically = true;
        public bool EnableExpansion = true;

        [Header("Ritmo da IA")]
        [Min(0.25f)] public float DecisionInterval = 1.5f;
        [Min(1)] public int CartelLevel = 1;
        [Min(1)] public int MissionsBeforeExpansion = 3;
        [Min(1)] public int MaxBases = 8;
        [Min(1)] public int MaxGroundMembersPerBase = 32;
        [Min(1)] public int MaxGroundVehiclesPerBase = 16;
        [Min(1)] public int MaxMaritimeMembersPerBase = 32;
        [Min(1)] public int MaxBoatsPerBase = 16;
        [Min(0.5f)] public float AttackDuration = 18f;
        [Min(0f)] public float ThreatRadius = 180f;

        [Header("Niveis: membros / veiculos")]
        public int[] GroundMembersByLevel = { 4, 8, 16, 32 };
        public int[] GroundVehiclesByLevel = { 2, 4, 8, 16 };
        public int[] MaritimeMembersByLevel = { 4, 8, 16, 32 };
        public int[] BoatsByLevel = { 2, 4, 8, 16 };

        [Header("Prefabs opcionais")]
        public CartelPrefabSet Prefabs = new CartelPrefabSet();

        [Header("Validacao de construcao")]
        public LayerMask PlacementBlockerLayers;
        public LayerMask WaterLayers;
        [Min(1f)] public float PlacementClearance = 3f;
        [Min(4)] public int PlacementSamples = 24;

        [Header("Diagnostico")]
        public CartelControllerState State = CartelControllerState.Disabled;
        [TextArea(2, 5)] public string StatusDebug = string.Empty;
        public int CompletedMissions;
        public int RobberiesCompleted;

        private sealed class CartelOperation
        {
            public bool Naval;
            public GameObject Unit;
            public CartelBaseRuntime Base;
            public CartelOperationPhase Phase;
            public CartelManualCreate CurrentPoint;
            public CartelManualCreate TargetCreate;
            public CartelManualCreate MeetingCreate;
            public NavioPetroleiro TargetTanker;
            public List<CartelManualCreate> Route = new List<CartelManualCreate>();
            public int RouteIndex;
            public float Cargo;
            public float Deadline;
            public readonly List<GameObject> Members = new List<GameObject>();
        }

        private readonly List<CartelManualCreate> creates = new List<CartelManualCreate>();
        private readonly List<CartelBaseRuntime> bases = new List<CartelBaseRuntime>();
        private readonly HashSet<int> tankersThatLeftPlatform = new HashSet<int>();
        private CartelOperation maritimeOperation;
        private CartelOperation terrestrialOperation;
        private float nextDecisionTime;
        private bool initialized;

        public IReadOnlyList<CartelBaseRuntime> Bases { get { return bases; } }

        private void Start()
        {
            if (StartAutomatically)
            {
                Initialize();
            }
        }

        private void Update()
        {
            if (!initialized || State == CartelControllerState.Disabled || Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + Mathf.Max(0.25f, DecisionInterval);
            TickAI();
        }

        public void Initialize()
        {
            initialized = true;
            RefreshCreates();
            State = CartelControllerState.SelectingBase;
            EnsureInitialBase();
            if (bases.Count == 0)
            {
                State = CartelControllerState.WaitingForManualCreate;
                StatusDebug = "Aguardando CartelBaseCreate e CartelBaseAreaCreate validos.";
                return;
            }

            for (int i = 0; i < bases.Count; i++)
            {
                SpawnInitialUnits(bases[i]);
            }

            State = CartelControllerState.Operational;
            StatusDebug = "Cartel operacional usando apenas Creates manuais.";
        }

        public void RebuildCreateCache()
        {
            RefreshCreates();
        }

        private void TickAI()
        {
            RefreshCreates();
            UpdateTankerExitFlags();

            if (bases.Count == 0)
            {
                EnsureInitialBase();
                State = bases.Count == 0 ? CartelControllerState.WaitingForManualCreate : CartelControllerState.Operational;
                return;
            }

            for (int i = 0; i < bases.Count; i++)
            {
                SpawnInitialUnits(bases[i]);
                CleanUnitLists(bases[i]);
            }

            UpdateMaritimeGroup();
            UpdateTerrestrialGroup();
            TryExpand();
        }

        private void RefreshCreates()
        {
            creates.Clear();
            List<CartelManualCreate> all = CartelManualCreate.GetAll(false);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !creates.Contains(all[i]))
                {
                    creates.Add(all[i]);
                }
            }
        }

        private void EnsureInitialBase()
        {
            if (bases.Count >= MaxBases)
            {
                return;
            }

            string country = InitialCountryId;
            if (string.IsNullOrEmpty(country))
            {
                country = FirstConfiguredCountry();
            }

            if (!string.IsNullOrEmpty(country))
            {
                TryBuildBase(country);
            }
        }

        private bool TryBuildBase(string countryId)
        {
            if (string.IsNullOrEmpty(countryId) || bases.Count >= MaxBases || HasBaseInCountry(countryId))
            {
                return false;
            }

            State = CartelControllerState.SelectingBase;
            CartelManualCreate candidate = SelectBestBaseCandidate(countryId);
            if (candidate == null)
            {
                StatusDebug = "Nenhum CartelBaseCreate passou pela analise de seguranca.";
                return false;
            }

            CartelManualCreate area = ResolveRelatedCreate(CartelCreateType.CartelBaseAreaCreate, candidate, countryId);
            Vector3 placement;
            if (!TryFindPlacement(candidate, area, out placement))
            {
                StatusDebug = "CartelBaseCreate encontrado, mas CartelBaseAreaCreate nao possui local valido.";
                return false;
            }

            State = CartelControllerState.BuildingBase;
            CartelBaseRuntime runtime = new CartelBaseRuntime();
            runtime.CountryId = countryId;
            runtime.Candidate = candidate;
            runtime.Area = area;
            runtime.BaseId = countryId + "_" + candidate.GetStableKey();
            runtime.FuelStorage = ResolveRelatedCreate(CartelCreateType.CartelFuelStorageCreate, candidate, countryId);
            runtime.BaseExit = ResolveRelatedCreate(CartelCreateType.CartelBaseExitCreate, candidate, countryId);
            runtime.GroundSpawn = ResolveRelatedCreate(CartelCreateType.CartelTerrestreSpawnCreate, candidate, countryId);
            runtime.MaritimeSpawn = ResolveRelatedCreate(CartelCreateType.CartelMaritimeSpawnCreate, candidate, countryId);

            if (Prefabs != null && Prefabs.BasePrefab != null)
            {
                runtime.Root = Instantiate(Prefabs.BasePrefab, placement, candidate.transform.rotation);
                runtime.Root.name = "CartelBase_" + countryId;
            }
            else
            {
                runtime.Root = new GameObject("CartelBase_" + countryId);
                runtime.Root.transform.SetPositionAndRotation(placement, candidate.transform.rotation);
            }

            bases.Add(runtime);
            StatusDebug = "Base criada em " + candidate.gameObject.name + " dentro da area manual.";
            return true;
        }

        private CartelManualCreate SelectBestBaseCandidate(string countryId)
        {
            CartelManualCreate best = null;
            float bestScore = float.NegativeInfinity;
            List<CartelManualCreate> candidates = GetCreates(CartelCreateType.CartelBaseCreate, countryId);
            for (int i = 0; i < candidates.Count; i++)
            {
                CartelManualCreate candidate = candidates[i];
                if (!candidate.IsUsable() || HasNearbyBase(candidate.Position, candidate.MinimumDistanceToOtherBase))
                {
                    continue;
                }

                float score = candidate.BasePreference;
                score += ScoreDistanceFromReferences(candidate, CartelCreateType.CityReference, candidate.CityDistanceWeight, countryId);
                score += ScoreDistanceFromReferences(candidate, CartelCreateType.PoliceReference, candidate.PoliceDistanceWeight, countryId);
                score += ScoreDistanceFromReferences(candidate, CartelCreateType.MilitaryReference, candidate.MilitaryDistanceWeight, countryId);
                score += ScoreDistanceFromReferences(candidate, CartelCreateType.BusyRoadReference, candidate.BusyRoadDistanceWeight, countryId);

                CartelManualCreate coast = FindNearestCreate(CartelCreateType.CartelCoastalMeetingCreate, candidate.Position, countryId);
                if (coast != null)
                {
                    score += candidate.CoastalAccessBonus / (1f + coast.DistanceTo(candidate.Position));
                }

                if (best == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private float ScoreDistanceFromReferences(CartelManualCreate candidate, CartelCreateType type, float weight, string countryId)
        {
            if (weight <= 0f)
            {
                return 0f;
            }

            float nearest = float.PositiveInfinity;
            List<CartelManualCreate> references = GetCreates(type, countryId);
            for (int i = 0; i < references.Count; i++)
            {
                nearest = Mathf.Min(nearest, references[i].DistanceTo(candidate.Position));
            }

            return float.IsPositiveInfinity(nearest) ? 0f : nearest * weight;
        }

        private bool TryFindPlacement(CartelManualCreate candidate, CartelManualCreate area, out Vector3 placement)
        {
            Vector3 center = area != null ? area.Position : candidate.Position;
            float radius = area != null ? Mathf.Max(2f, area.Radius) : Mathf.Max(2f, candidate.Radius);
            for (int i = 0; i <= Mathf.Max(1, PlacementSamples); i++)
            {
                float angle = i * 2.399963f;
                float distance = i == 0 ? 0f : radius * Mathf.Sqrt(i / (float)Mathf.Max(1, PlacementSamples));
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (IsValidPlacement(point, area, candidate))
                {
                    placement = point;
                    return true;
                }
            }

            placement = Vector3.zero;
            return false;
        }

        private bool IsValidPlacement(Vector3 point, CartelManualCreate area, CartelManualCreate candidate)
        {
            if (WaterLayers.value != 0 && Physics.CheckSphere(point, PlacementClearance, WaterLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (PlacementBlockerLayers.value != 0 && Physics.CheckSphere(point, PlacementClearance, PlacementBlockerLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (area != null && !area.Contains(point))
            {
                return false;
            }

            if (HasNearbyReference(point, CartelCreateType.CityReference, 20f)
                || HasNearbyReference(point, CartelCreateType.BusyRoadReference, 12f))
            {
                return false;
            }

            if (candidate.AvoidWater && WaterLayers.value != 0
                && Physics.CheckSphere(point, PlacementClearance, WaterLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        private void SpawnInitialUnits(CartelBaseRuntime runtime)
        {
            if (runtime == null || runtime.InitialUnitsCreated)
            {
                return;
            }

            int levelIndex = Mathf.Clamp(CartelLevel - 1, 0, 3);
            int groundMembers = ValueAtLevel(GroundMembersByLevel, levelIndex, 4);
            int groundVehicles = ValueAtLevel(GroundVehiclesByLevel, levelIndex, 2);
            int maritimeMembers = ValueAtLevel(MaritimeMembersByLevel, levelIndex, 4);
            int boats = ValueAtLevel(BoatsByLevel, levelIndex, 2);

            for (int i = 0; i < groundMembers && runtime.GroundMembers.Count < MaxGroundMembersPerBase; i++)
            {
                GameObject unit = SpawnUnit(Prefabs == null ? null : Prefabs.GroundMemberPrefab,
                    runtime.GroundSpawn, runtime, "CartelTerrestre");
                if (unit != null) runtime.GroundMembers.Add(unit);
            }

            for (int i = 0; i < groundVehicles && runtime.GroundVehicles.Count < MaxGroundVehiclesPerBase; i++)
            {
                GameObject unit = SpawnUnit(Prefabs == null ? null : Prefabs.GroundVehiclePrefab,
                    runtime.GroundSpawn, runtime, "CartelVeiculo");
                if (unit != null) runtime.GroundVehicles.Add(unit);
            }

            for (int i = 0; i < maritimeMembers && runtime.MaritimeMembers.Count < MaxMaritimeMembersPerBase; i++)
            {
                GameObject unit = SpawnUnit(Prefabs == null ? null : Prefabs.MaritimeMemberPrefab,
                    runtime.MaritimeSpawn, runtime, "CartelMaritimo");
                if (unit != null) runtime.MaritimeMembers.Add(unit);
            }

            for (int i = 0; i < boats && runtime.Boats.Count < MaxBoatsPerBase; i++)
            {
                GameObject unit = SpawnUnit(Prefabs == null ? null : Prefabs.PirateBoatPrefab,
                    runtime.MaritimeSpawn, runtime, "CartelBarco");
                if (unit != null) runtime.Boats.Add(unit);
            }

            runtime.InitialUnitsCreated = true;
        }

        private GameObject SpawnUnit(GameObject prefab, CartelManualCreate spawn, CartelBaseRuntime runtime, string label)
        {
            if (prefab == null || spawn == null || !spawn.IsUsable())
            {
                return null;
            }

            Vector3 position = spawn.Position + new Vector3((spawn.OccupantCount() % 3) * 3f, 0f, (spawn.OccupantCount() / 3) * 3f);
            GameObject unit = Instantiate(prefab, position, spawn.transform.rotation);
            unit.name = label + "_" + runtime.CountryId + "_" + unit.GetInstanceID();
            spawn.TryReserve(unit);
            ConfigureIdentity(unit, label.IndexOf("Maritimo", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Barco", StringComparison.OrdinalIgnoreCase) >= 0
                ? TipoUnidade.Naval
                : (label.IndexOf("Veiculo", StringComparison.OrdinalIgnoreCase) >= 0 ? TipoUnidade.Veiculo : TipoUnidade.Infantaria));
            return unit;
        }

        private void ConfigureIdentity(GameObject unit, TipoUnidade type)
        {
            if (unit == null) return;
            IdentidadeUnidade identity = unit.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = unit.AddComponent<IdentidadeUnidade>();
            identity.teamID = CartelTeamId;
            identity.tipoUnidade = type;
            identity.nomeDoPais = "Cartel";
        }

        private void UpdateTankerExitFlags()
        {
            NavioPetroleiro[] tankers = FindObjectsByType<NavioPetroleiro>(FindObjectsSortMode.None);
            List<CartelManualCreate> exits = GetCreates(CartelCreateType.OilPlatformExitCreate, string.Empty);
            for (int i = 0; i < tankers.Length; i++)
            {
                if (tankers[i] == null) continue;
                for (int j = 0; j < exits.Count; j++)
                {
                    if (exits[j].Contains(tankers[i].transform.position))
                    {
                        tankersThatLeftPlatform.Add(tankers[i].GetInstanceID());
                        break;
                    }
                }
            }
        }

        private void UpdateMaritimeGroup()
        {
            if (maritimeOperation == null || maritimeOperation.Unit == null)
            {
                maritimeOperation = CreateMaritimeOperation();
                if (maritimeOperation == null) return;
            }

            CartelOperation op = maritimeOperation;
            switch (op.Phase)
            {
                case CartelOperationPhase.LeaveBase:
                    if (Arrived(op.Unit, op.CurrentPoint))
                    {
                        op.Route = GetCreates(CartelCreateType.CartelMaritimePatrolCreate, op.Base.CountryId);
                        op.RouteIndex = 0;
                        op.CurrentPoint = FirstOrNull(op.Route);
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.Parking : CartelOperationPhase.Patrol;
                    }
                    else SendTo(op.Unit, op.CurrentPoint);
                    break;

                case CartelOperationPhase.Patrol:
                    if (HasThreatNear(op.Unit))
                    {
                        SetMaritimeEscape(op);
                        break;
                    }

                    if (op.CurrentPoint == null)
                    {
                        op.Phase = CartelOperationPhase.Parking;
                        break;
                    }

                    if (!Arrived(op.Unit, op.CurrentPoint))
                    {
                        SendTo(op.Unit, op.CurrentPoint);
                    }
                    else
                    {
                        NavioPetroleiro tanker = FindValidTanker(op.CurrentPoint);
                        if (tanker != null)
                        {
                            op.TargetTanker = tanker;
                            op.Phase = CartelOperationPhase.ApproachingTanker;
                        }
                        else
                        {
                            op.RouteIndex = (op.RouteIndex + 1) % Mathf.Max(1, op.Route.Count);
                            op.CurrentPoint = op.Route[op.RouteIndex];
                        }
                    }
                    break;

                case CartelOperationPhase.ApproachingTanker:
                    if (!IsValidTanker(op.TargetTanker, op.CurrentPoint))
                    {
                        op.TargetTanker = null;
                        op.Phase = CartelOperationPhase.Patrol;
                        break;
                    }

                    SendToPosition(op.Unit, op.TargetTanker.transform.position, true);
                    if (Vector3.Distance(op.Unit.transform.position, op.TargetTanker.transform.position) <= 14f)
                    {
                        op.Phase = CartelOperationPhase.Robbery;
                    }
                    break;

                case CartelOperationPhase.Robbery:
                    if (!IsValidTanker(op.TargetTanker, op.CurrentPoint))
                    {
                        op.TargetTanker = null;
                        op.Phase = CartelOperationPhase.Patrol;
                        break;
                    }

                    op.Cargo = Mathf.Max(0, op.TargetTanker.petroleoCarregado);
                    op.TargetTanker.petroleoCarregado = 0;
                    RobberiesCompleted++;
                    SetMaritimeEscape(op);
                    break;

                case CartelOperationPhase.Escape:
                    if (op.CurrentPoint == null)
                    {
                        op.Phase = CartelOperationPhase.Parking;
                    }
                    else if (!Arrived(op.Unit, op.CurrentPoint))
                    {
                        SendTo(op.Unit, op.CurrentPoint);
                    }
                    else
                    {
                        op.CurrentPoint = FindSafeIslandArrival(op.Base.CountryId, op.Unit.transform.position);
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.CoastalMeeting : CartelOperationPhase.IslandArrival;
                    }
                    break;

                case CartelOperationPhase.IslandArrival:
                    if (HasThreatNear(op.Unit))
                    {
                        op.CurrentPoint = FindSafeIslandArrival(op.Base.CountryId, op.Unit.transform.position);
                    }
                    else if (op.CurrentPoint != null && !Arrived(op.Unit, op.CurrentPoint))
                    {
                        SendTo(op.Unit, op.CurrentPoint);
                    }
                    else
                    {
                        op.CurrentPoint = FindNearestCreate(CartelCreateType.CartelCoastalMeetingCreate, op.Unit.transform.position, op.Base.CountryId);
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.Parking : CartelOperationPhase.CoastalMeeting;
                    }
                    break;

                case CartelOperationPhase.CoastalMeeting:
                    if (op.CurrentPoint == null)
                    {
                        op.Phase = CartelOperationPhase.Parking;
                    }
                    else if (!Arrived(op.Unit, op.CurrentPoint))
                    {
                        SendTo(op.Unit, op.CurrentPoint);
                    }
                    else
                    {
                        EnsureMeetingVehicle(op);
                        TryTransferAtMeeting(op);
                    }
                    break;

                case CartelOperationPhase.Parking:
                    CartelManualCreate parking = FindNearestCreate(CartelCreateType.CartelBoatParkingCreate, op.Unit.transform.position, op.Base.CountryId);
                    if (parking == null || Arrived(op.Unit, parking))
                    {
                        maritimeOperation = null;
                    }
                    else SendTo(op.Unit, parking);
                    break;
            }
        }

        private CartelOperation CreateMaritimeOperation()
        {
            CartelBaseRuntime baseRuntime = FirstBaseWithBoats();
            if (baseRuntime == null) return null;
            GameObject boat = FirstAlive(baseRuntime.Boats);
            CartelManualCreate exit = ResolveRelatedCreate(CartelCreateType.CartelMaritimeExitCreate, baseRuntime.Candidate, baseRuntime.CountryId);
            if (boat == null || exit == null) return null;
            CartelOperation op = new CartelOperation { Naval = true, Unit = boat, Base = baseRuntime, CurrentPoint = exit, Phase = CartelOperationPhase.LeaveBase };
            return op;
        }

        private void SetMaritimeEscape(CartelOperation op)
        {
            CartelManualCreate escape = FindFarthestUsable(CartelCreateType.CartelMaritimeEscapeCreate, op.Base.CountryId, op.Unit.transform.position);
            if (escape == null) escape = FindNearestCreate(CartelCreateType.CartelMaritimeHideCreate, op.Unit.transform.position, op.Base.CountryId);
            op.CurrentPoint = escape;
            op.Phase = escape == null ? CartelOperationPhase.Parking : CartelOperationPhase.Escape;
        }

        private void UpdateTerrestrialGroup()
        {
            if (terrestrialOperation == null || terrestrialOperation.Unit == null)
            {
                terrestrialOperation = CreateTerrestrialOperation();
                if (terrestrialOperation == null) return;
            }

            CartelOperation op = terrestrialOperation;
            switch (op.Phase)
            {
                case CartelOperationPhase.LeaveBase:
                    if (!Arrived(op.Unit, op.CurrentPoint))
                    {
                        SendTo(op.Unit, op.CurrentPoint);
                    }
                    else
                    {
                        op.Route = GetCreates(CartelCreateType.CartelTerrestreRouteCreate, op.Base.CountryId);
                        op.RouteIndex = 0;
                        op.CurrentPoint = FirstOrNull(op.Route);
                        op.Phase = op.Route.Count == 0 ? (op.MeetingCreate != null ? CartelOperationPhase.CoastalMeeting : CartelOperationPhase.TargetArrival) : CartelOperationPhase.Route;
                    }
                    break;

                case CartelOperationPhase.Route:
                    if (!Arrived(op.Unit, op.CurrentPoint)) SendTo(op.Unit, op.CurrentPoint);
                    else if (op.RouteIndex + 1 < op.Route.Count)
                    {
                        op.RouteIndex++;
                        op.CurrentPoint = op.Route[op.RouteIndex];
                    }
                    else op.Phase = op.MeetingCreate != null ? CartelOperationPhase.CoastalMeeting : CartelOperationPhase.TargetArrival;
                    break;

                case CartelOperationPhase.CoastalMeeting:
                    if (HasThreatNear(op.Unit))
                    {
                        op.CurrentPoint = FindNearestCreate(CartelCreateType.CartelTerrestrialEscapeCreate, op.Unit.transform.position, op.Base.CountryId);
                        if (op.CurrentPoint != null) op.Phase = CartelOperationPhase.Escape;
                    }
                    else if (!Arrived(op.Unit, op.MeetingCreate)) SendTo(op.Unit, op.MeetingCreate);
                    else TryTransferAtMeeting(maritimeOperation);
                    break;

                case CartelOperationPhase.TargetArrival:
                    if (op.TargetCreate == null)
                    {
                        CompleteTerrestrialOperation(op);
                    }
                    else if (!Arrived(op.Unit, op.TargetCreate)) SendTo(op.Unit, op.TargetCreate);
                    else
                    {
                        op.Phase = CartelOperationPhase.AttackDeploy;
                        op.Deadline = Time.time + AttackDuration;
                        DeployAttackers(op);
                    }
                    break;

                case CartelOperationPhase.AttackDeploy:
                    if (Time.time >= op.Deadline) op.Phase = CartelOperationPhase.Attack;
                    break;

                case CartelOperationPhase.Attack:
                    if (Time.time >= op.Deadline + AttackDuration)
                    {
                        op.CurrentPoint = FindNearestCreate(CartelCreateType.CartelAttackEscapeCreate, op.Unit.transform.position, op.Base.CountryId);
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.ReturnBase : CartelOperationPhase.AttackEscape;
                    }
                    break;

                case CartelOperationPhase.AttackEscape:
                    if (!Arrived(op.Unit, op.CurrentPoint)) SendTo(op.Unit, op.CurrentPoint);
                    else
                    {
                        op.CurrentPoint = FindNearestCreate(CartelCreateType.CartelTerrestrialEscapeCreate, op.Unit.transform.position, op.Base.CountryId);
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.ReturnBase : CartelOperationPhase.Escape;
                    }
                    break;

                case CartelOperationPhase.Escape:
                    if (HasThreatNear(op.Unit))
                    {
                        CartelManualCreate hide = FindNearestCreate(CartelCreateType.CartelTerrestrialHideCreate, op.Unit.transform.position, op.Base.CountryId);
                        if (hide != null) op.CurrentPoint = hide;
                    }
                    else if (!Arrived(op.Unit, op.CurrentPoint)) SendTo(op.Unit, op.CurrentPoint);
                    else
                    {
                        op.CurrentPoint = op.Base.BaseExit;
                        op.Phase = CartelOperationPhase.ReturnBase;
                    }
                    break;

                case CartelOperationPhase.ReturnBase:
                    if (!Arrived(op.Unit, op.CurrentPoint)) SendTo(op.Unit, op.CurrentPoint);
                    else
                    {
                        op.CurrentPoint = op.Base.FuelStorage;
                        op.Phase = op.CurrentPoint == null ? CartelOperationPhase.Parking : CartelOperationPhase.Storage;
                    }
                    break;

                case CartelOperationPhase.Storage:
                    if (!Arrived(op.Unit, op.CurrentPoint)) SendTo(op.Unit, op.CurrentPoint);
                    else CompleteTerrestrialOperation(op);
                    break;
            }
        }

        private CartelOperation CreateTerrestrialOperation()
        {
            CartelBaseRuntime baseRuntime = FirstBaseWithVehicles();
            if (baseRuntime == null) return null;
            GameObject vehicle = FirstAlive(baseRuntime.GroundVehicles);
            if (vehicle == null || baseRuntime.BaseExit == null) return null;

            if (maritimeOperation != null && maritimeOperation.Phase == CartelOperationPhase.CoastalMeeting && maritimeOperation.Cargo > 0f)
            {
                CartelOperation meeting = new CartelOperation
                {
                    Unit = vehicle,
                    Base = baseRuntime,
                    CurrentPoint = baseRuntime.BaseExit,
                    MeetingCreate = maritimeOperation.CurrentPoint,
                    Phase = CartelOperationPhase.LeaveBase
                };
                return meeting;
            }

            if (CompletedMissions <= 0)
            {
                return null;
            }

            CartelManualCreate target = SelectGroundTarget(baseRuntime.CountryId);
            if (target == null) return null;
            CartelManualCreate arrival = ResolveRelatedCreate(CartelCreateType.CartelTargetArrivalCreate, target, baseRuntime.CountryId);
            CartelOperation attack = new CartelOperation
            {
                Unit = vehicle,
                Base = baseRuntime,
                CurrentPoint = baseRuntime.BaseExit,
                TargetCreate = arrival,
                Phase = CartelOperationPhase.LeaveBase
            };

            int maxMembers = Mathf.Min(baseRuntime.GroundMembers.Count, 4 + CartelLevel * 2);
            for (int i = 0; i < maxMembers; i++)
            {
                if (baseRuntime.GroundMembers[i] != null) attack.Members.Add(baseRuntime.GroundMembers[i]);
            }
            return attack;
        }

        private void EnsureMeetingVehicle(CartelOperation maritime)
        {
            if (terrestrialOperation == null && maritime != null && maritime.Cargo > 0f)
            {
                terrestrialOperation = CreateTerrestrialOperation();
            }
        }

        private void TryTransferAtMeeting(CartelOperation maritime)
        {
            if (maritime == null || maritime.Cargo <= 0f || terrestrialOperation == null || terrestrialOperation.MeetingCreate == null)
            {
                return;
            }

            CartelOperation ground = terrestrialOperation;
            if (ground.MeetingCreate != maritime.CurrentPoint
                || !Arrived(ground.Unit, ground.MeetingCreate)
                || !Arrived(maritime.Unit, maritime.CurrentPoint))
            {
                return;
            }

            ground.Cargo += maritime.Cargo;
            CaminhaoTanqueAbastecimento truck = ground.Unit.GetComponent<CaminhaoTanqueAbastecimento>();
            if (truck != null)
            {
                float transferCapacity = Mathf.Max(0f, truck.EspacoCarga);
                float transfer = Mathf.Min(ground.Cargo, transferCapacity);
                float loaded = truck.CarregarSemCusto(transfer);
                ground.Cargo = loaded;
                maritime.Cargo = Mathf.Max(0f, maritime.Cargo - loaded);
            }
            else
            {
                maritime.Cargo = 0f;
            }

            if (maritime.Cargo <= 0.01f)
            {
                maritime.CurrentPoint = FindNearestCreate(CartelCreateType.CartelBoatParkingCreate, maritime.Unit.transform.position, maritime.Base.CountryId);
                maritime.Phase = CartelOperationPhase.Parking;
            }
            else
            {
                // Um barco pode fazer mais de uma entrega se a carga roubada
                // exceder a capacidade do veiculo terrestre.
                maritime.Phase = CartelOperationPhase.CoastalMeeting;
            }
            ground.CurrentPoint = ground.Base.BaseExit;
            ground.Phase = CartelOperationPhase.ReturnBase;
            StatusDebug = "Combustivel transferido no " + maritime.MeetingCreate.gameObject.name + ".";
        }

        private void DeployAttackers(CartelOperation op)
        {
            if (op == null || op.TargetCreate == null) return;
            List<CartelManualCreate> positions = GetCreates(CartelCreateType.CartelAttackPositionCreate, op.Base.CountryId);
            int count = Mathf.Min(op.Members.Count, positions.Count);
            for (int i = 0; i < count; i++)
            {
                SendTo(op.Members[i], positions[i]);
            }
        }

        private void CompleteTerrestrialOperation(CartelOperation op)
        {
            if (op == null) return;
            if (op.Cargo > 0f)
            {
                op.Base.FuelStock += op.Cargo;
                op.Cargo = 0f;
            }
            op.Base.CompletedMissions++;
            CompletedMissions++;
            terrestrialOperation = null;
            StatusDebug = "Missao terrestre concluida; combustivel na base: " + op.Base.FuelStock.ToString("0");
        }

        private NavioPetroleiro FindValidTanker(CartelManualCreate patrol)
        {
            NavioPetroleiro[] tankers = FindObjectsByType<NavioPetroleiro>(FindObjectsSortMode.None);
            NavioPetroleiro best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < tankers.Length; i++)
            {
                if (!IsValidTanker(tankers[i], patrol)) continue;
                float distance = Vector3.Distance(transform.position, tankers[i].transform.position);
                if (distance < bestDistance)
                {
                    best = tankers[i];
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool IsValidTanker(NavioPetroleiro tanker, CartelManualCreate patrol)
        {
            if (tanker == null || tanker.petroleoCarregado <= 0 || !tankersThatLeftPlatform.Contains(tanker.GetInstanceID())) return false;
            if (patrol == null) return false;
            CartelManualCreate robbery = FindNearestCreate(CartelCreateType.CartelRobberyAreaCreate, tanker.transform.position, string.Empty);
            if (robbery == null || !robbery.Contains(tanker.transform.position)) return false;
            if (!patrol.Contains(tanker.transform.position) && !robbery.Contains(tanker.transform.position)) return false;

            switch (tanker.estadoAtual)
            {
                case NavioPetroleiro.EstadoPetroleiro.CARREGANDO:
                case NavioPetroleiro.EstadoPetroleiro.ACOPLANDO_PLATAFORMA:
                case NavioPetroleiro.EstadoPetroleiro.INDO_PLATAFORMA:
                case NavioPetroleiro.EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA:
                    return false;
                default:
                    return true;
            }
        }

        private void TryExpand()
        {
            if (!EnableExpansion || bases.Count >= MaxBases || CompletedMissions < MissionsBeforeExpansion)
            {
                return;
            }

            List<CartelManualCreate> expansionPoints = GetCreates(CartelCreateType.CartelExpansionCreate, string.Empty);
            for (int i = 0; i < expansionPoints.Count; i++)
            {
                string country = expansionPoints[i].CountryId;
                if (string.IsNullOrEmpty(country) || HasBaseInCountry(country)) continue;
                if (TryBuildBase(country))
                {
                    SpawnInitialUnits(bases[bases.Count - 1]);
                    CompletedMissions = 0;
                    break;
                }
            }
        }

        private CartelManualCreate SelectGroundTarget(string countryId)
        {
            List<CartelManualCreate> targets = GetCreates(CartelCreateType.CartelGroundTargetCreate, countryId);
            CartelManualCreate best = null;
            float score = float.NegativeInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                CartelManualCreate target = targets[i];
                if (!target.AllowsRobbery) continue;
                float current = target.EconomicValue - target.SecurityLevel * 10f;
                if (best == null || current > score) { best = target; score = current; }
            }
            return best;
        }

        private void SendTo(GameObject unit, CartelManualCreate point)
        {
            if (point != null) SendToPosition(unit, point.Position, IsNaval(unit));
        }

        private void SendToPosition(GameObject unit, Vector3 position, bool naval)
        {
            if (unit == null) return;
            if (naval)
            {
                NavegacaoInteligenteNaval navalNavigation = unit.GetComponent<NavegacaoInteligenteNaval>();
                if (navalNavigation != null) { navalNavigation.DefinirDestino(position); return; }
            }

            ControleUnidade control = unit.GetComponent<ControleUnidade>();
            if (control != null) { control.EmitirOrdemMover(position); return; }

            // O movimento terrestre precisa passar pela trilha oficial do projeto
            // (ControleUnidade). Nao usar NavMeshAgent.SetDestination diretamente,
            // pois isso conflita com a auditoria e com o controle das outras IAs.
        }

        private bool Arrived(GameObject unit, CartelManualCreate point)
        {
            return unit != null && point != null && point.Contains(unit.transform.position);
        }

        private bool IsNaval(GameObject unit)
        {
            return unit != null && (unit.GetComponent<ControleNavioRealista>() != null
                || unit.GetComponent<ControleSubmarino>() != null
                || unit.GetComponent<NavioPetroleiro>() != null
                || unit.GetComponent<IdentidadeNaval>() != null);
        }

        private bool HasThreatNear(GameObject unit)
        {
            if (unit == null || ThreatRadius <= 0f) return false;
            Collider[] colliders = Physics.OverlapSphere(unit.transform.position, ThreatRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject other = colliders[i].gameObject;
                if (other == unit || other.transform.IsChildOf(unit.transform)) continue;
                IdentidadeUnidade identity = other.GetComponentInParent<IdentidadeUnidade>();
                if (identity != null && identity.teamID > 0 && identity.teamID != CartelTeamId) return true;
            }
            return false;
        }

        private bool HasNearbyBase(Vector3 position, float distance)
        {
            for (int i = 0; i < bases.Count; i++)
            {
                if (bases[i] != null && bases[i].Root != null && Vector3.Distance(position, bases[i].Root.transform.position) < Mathf.Max(1f, distance)) return true;
            }
            return false;
        }

        private bool HasNearbyReference(Vector3 position, CartelCreateType type, float distance)
        {
            return FindNearestCreate(type, position, string.Empty) != null
                && FindNearestCreate(type, position, string.Empty).DistanceTo(position) <= distance;
        }

        private bool HasBaseInCountry(string countryId)
        {
            for (int i = 0; i < bases.Count; i++) if (bases[i] != null && string.Equals(bases[i].CountryId, countryId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private string FirstConfiguredCountry()
        {
            for (int i = 0; i < creates.Count; i++) if (creates[i].Type == CartelCreateType.CartelBaseCreate && !string.IsNullOrEmpty(creates[i].CountryId)) return creates[i].CountryId;
            return string.Empty;
        }

        private CartelBaseRuntime FirstBaseWithBoats() { for (int i = 0; i < bases.Count; i++) if (FirstAlive(bases[i].Boats) != null) return bases[i]; return null; }
        private CartelBaseRuntime FirstBaseWithVehicles() { for (int i = 0; i < bases.Count; i++) if (FirstAlive(bases[i].GroundVehicles) != null) return bases[i]; return null; }
        private static GameObject FirstAlive(List<GameObject> list) { for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].activeInHierarchy) return list[i]; return null; }
        private static int ValueAtLevel(int[] values, int index, int fallback) { return values != null && values.Length > 0 ? Mathf.Max(0, values[Mathf.Clamp(index, 0, values.Length - 1)]) : fallback; }
        private static CartelManualCreate FirstOrNull(List<CartelManualCreate> list) { return list != null && list.Count > 0 ? list[0] : null; }

        private void CleanUnitLists(CartelBaseRuntime runtime)
        {
            runtime.GroundMembers.RemoveAll(item => item == null);
            runtime.GroundVehicles.RemoveAll(item => item == null);
            runtime.MaritimeMembers.RemoveAll(item => item == null);
            runtime.Boats.RemoveAll(item => item == null);
        }

        private List<CartelManualCreate> GetCreates(CartelCreateType type, string countryId)
        {
            List<CartelManualCreate> result = new List<CartelManualCreate>();
            for (int i = 0; i < creates.Count; i++)
            {
                CartelManualCreate create = creates[i];
                if (create.Type != type || !create.IsUsable()) continue;
                if (!string.IsNullOrEmpty(countryId) && !string.IsNullOrEmpty(create.CountryId) && !string.Equals(create.CountryId, countryId, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(create);
            }
            result.Sort((a, b) => a.RouteSequence.CompareTo(b.RouteSequence));
            return result;
        }

        private CartelManualCreate FindNearestCreate(CartelCreateType type, Vector3 position, string countryId)
        {
            List<CartelManualCreate> points = GetCreates(type, countryId);
            CartelManualCreate best = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < points.Count; i++) { float candidateDistance = points[i].DistanceTo(position); if (candidateDistance < distance) { best = points[i]; distance = candidateDistance; } }
            return best;
        }

        private CartelManualCreate FindFarthestUsable(CartelCreateType type, string countryId, Vector3 position)
        {
            List<CartelManualCreate> points = GetCreates(type, countryId);
            CartelManualCreate best = null;
            float distance = float.NegativeInfinity;
            for (int i = 0; i < points.Count; i++) { float candidateDistance = points[i].DistanceTo(position); if (candidateDistance > distance) { best = points[i]; distance = candidateDistance; } }
            return best;
        }

        private CartelManualCreate FindSafeIslandArrival(string countryId, Vector3 position)
        {
            List<CartelManualCreate> islands = GetCreates(CartelCreateType.CartelIslandArrivalCreate, countryId);
            CartelManualCreate best = null;
            float score = float.PositiveInfinity;
            for (int i = 0; i < islands.Count; i++)
            {
                if (HasThreatNearAt(islands[i].Position)) continue;
                float current = islands[i].DistanceTo(position);
                if (current < score) { score = current; best = islands[i]; }
            }
            return best;
        }

        private bool HasThreatNearAt(Vector3 position)
        {
            if (ThreatRadius <= 0f) return false;
            Collider[] colliders = Physics.OverlapSphere(position, ThreatRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++) { IdentidadeUnidade id = colliders[i].GetComponentInParent<IdentidadeUnidade>(); if (id != null && id.teamID > 0 && id.teamID != CartelTeamId) return true; }
            return false;
        }

        private CartelManualCreate ResolveRelatedCreate(CartelCreateType type, CartelManualCreate anchor, string countryId)
        {
            List<CartelManualCreate> points = GetCreates(type, countryId);
            if (points.Count == 0) return null;
            if (anchor != null && !string.IsNullOrEmpty(anchor.LinkId)) for (int i = 0; i < points.Count; i++) if (string.Equals(points[i].LinkId, anchor.LinkId, StringComparison.OrdinalIgnoreCase)) return points[i];
            return FindNearestCreate(type, anchor == null ? transform.position : anchor.Position, countryId);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.1f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, ThreatRadius);
        }
    }
}
