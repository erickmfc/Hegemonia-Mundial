using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Cartel
{
    /// <summary>
    /// Categoria de um Create colocado manualmente no mapa.
    /// Os nomes seguem a especificacao do cartel para que o designer possa
    /// configurar a cena sem depender de coordenadas hard-coded.
    /// </summary>
    public enum CartelCreateType
    {
        CartelBaseCreate,
        CartelBaseAreaCreate,
        CartelTerrestreSpawnCreate,
        CartelBaseExitCreate,
        CartelTerrestreRouteCreate,
        CartelCoastalMeetingCreate,
        CartelIslandSupportCreate,
        CartelIslandArrivalCreate,
        CartelMaritimeSpawnCreate,
        CartelMaritimeExitCreate,
        CartelMaritimePatrolCreate,
        CartelRobberyAreaCreate,
        OilPlatformExitCreate,
        CartelMaritimeEscapeCreate,
        CartelTerrestrialEscapeCreate,
        CartelHideCreate,
        CartelMaritimeHideCreate,
        CartelTerrestrialHideCreate,
        CartelBoatParkingCreate,
        CartelVehicleParkingCreate,
        CartelFuelStorageCreate,
        CartelGroundTargetCreate,
        CartelTargetArrivalCreate,
        CartelAttackPositionCreate,
        CartelAttackEscapeCreate,
        CartelExpansionCreate,
        CartelCountryEntryCreate,
        CartelSeaEntryCreate,
        CartelLandEntryCreate,
        CartelDefensePositionCreate,
        CartelReinforcementCreate,

        // Referencias opcionais usadas apenas para pontuar locais de base.
        CityReference,
        PoliceReference,
        MilitaryReference,
        BusyRoadReference,

        CartelBoatCrewCreate,
        CartelBoatCargoCreate
    }

    public enum CartelRouteKind
    {
        Normal,
        Segura,
        Costeira,
        Urbana,
        Fuga,
        Transporte
    }

    [AddComponentMenu("Hegemonia/Cartel/Create Manual")]
    public sealed class CartelManualCreate : MonoBehaviour
    {
        private static readonly List<CartelManualCreate> Registry = new List<CartelManualCreate>();

        [Header("Identificacao")]
        public CartelCreateType Type = CartelCreateType.CartelHideCreate;
        [Tooltip("Pais ao qual o ponto pertence. Use o mesmo identificador dos Creates do pais.")]
        public string CountryId = string.Empty;
        [Tooltip("Grupo/base proprietario. Deixe vazio para um ponto compartilhado.")]
        public string OwnerId = string.Empty;
        [Tooltip("Identificador da base ou ilha a que este Create esta ligado.")]
        public string LinkId = string.Empty;
        public bool EnabledForCartel = true;

        [Header("Area e ocupacao")]
        [Min(0.5f)] public float Radius = 8f;
        [Min(0)] public int MaxOccupants = 0;
        public bool AllowReuse = true;
        public bool RequiresSafeArea = false;
        public bool AvoidWater = false;
        public bool AvoidBuildings = false;
        public bool AvoidRoads = false;

        [Header("Rotas")]
        public CartelRouteKind RouteKind = CartelRouteKind.Normal;
        [Min(0)] public int RouteSequence = 0;
        [Tooltip("Se preenchido, o controlador usa este valor para escolher o conjunto de rota.")]
        public string RouteSetId = string.Empty;

        [Header("Pontuacao de base")]
        [Tooltip("Peso manual adicional. Valores maiores tornam o candidato mais atraente.")]
        public float BasePreference = 0f;
        public float CityDistanceWeight = 1f;
        public float PoliceDistanceWeight = 0.6f;
        public float MilitaryDistanceWeight = 0.8f;
        public float BusyRoadDistanceWeight = 0.35f;
        [Min(0f)] public float CoastalAccessBonus = 0f;
        [Min(0f)] public float MinimumDistanceToOtherBase = 250f;

        [Header("Alvo terrestre")]
        public string TargetType = string.Empty;
        public string TargetCountryId = string.Empty;
        [Min(0f)] public float EconomicValue = 0f;
        [Range(0, 10)] public int SecurityLevel = 0;
        public bool AllowsRobbery = true;
        public bool DestroyOnCompletion = false;

        [Header("Visualizacao")]
        public bool HideRendererAtRuntime = true;
        public Color GizmoColor = new Color(0.9f, 0.15f, 0.8f, 0.9f);
        public bool DrawGizmo = true;

        [NonSerialized] private readonly HashSet<int> occupants = new HashSet<int>();

        public Vector3 Position { get { return transform.position; } }
        public bool IsArea { get { return Radius > 0.5f; } }

        private void OnEnable()
        {
            if (!Registry.Contains(this))
            {
                Registry.Add(this);
            }

            if (HideRendererAtRuntime && Application.isPlaying)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            Registry.Remove(this);
            occupants.Clear();
        }

        public bool IsUsable()
        {
            return EnabledForCartel && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 a = transform.position;
            Vector3 b = point;
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude <= Radius * Radius;
        }

        public float DistanceTo(Vector3 point)
        {
            Vector3 a = transform.position;
            Vector3 b = point;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public bool TryReserve(GameObject occupant)
        {
            if (occupant == null || !IsUsable())
            {
                return false;
            }

            int id = occupant.GetInstanceID();
            if (occupants.Contains(id))
            {
                return true;
            }

            if (MaxOccupants > 0 && occupants.Count >= MaxOccupants)
            {
                return false;
            }

            occupants.Add(id);
            return true;
        }

        public void Release(GameObject occupant)
        {
            if (occupant != null)
            {
                occupants.Remove(occupant.GetInstanceID());
            }
        }

        public void ClearOccupants()
        {
            occupants.Clear();
        }

        public int OccupantCount()
        {
            return occupants.Count;
        }

        public string GetStableKey()
        {
            return string.IsNullOrEmpty(gameObject.name) ? Type.ToString() : gameObject.name;
        }

        public static List<CartelManualCreate> GetAll(bool includeInactive)
        {
            List<CartelManualCreate> result = new List<CartelManualCreate>();
            for (int i = 0; i < Registry.Count; i++)
            {
                CartelManualCreate create = Registry[i];
                if (create == null || (!includeInactive && !create.IsUsable()))
                {
                    continue;
                }

                result.Add(create);
            }

            if (result.Count == 0)
            {
                CartelManualCreate[] discovered = Resources.FindObjectsOfTypeAll<CartelManualCreate>();
                for (int i = 0; i < discovered.Length; i++)
                {
                    CartelManualCreate create = discovered[i];
                    if (create == null || (!includeInactive && !create.IsUsable()))
                    {
                        continue;
                    }

                    if (!result.Contains(create))
                    {
                        result.Add(create);
                    }
                }
            }

            return result;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!DrawGizmo)
            {
                return;
            }

            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.5f, Radius));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * Mathf.Max(2f, Radius * 0.75f));
            UnityEditor.Handles.color = GizmoColor;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, Type + " / " + gameObject.name);
        }
#endif
    }
}
