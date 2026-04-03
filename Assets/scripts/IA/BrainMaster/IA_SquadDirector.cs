using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_SquadDirector : IIAUpdateModule
    {
        private readonly IA_Context _context;
        private readonly Dictionary<IA_SquadRole, IA_SquadData> _byRole = new Dictionary<IA_SquadRole, IA_SquadData>();
        // Reutilizados entre ticks para evitar alocacoes desnecessarias de GC
        private readonly List<GameObject> _candidatesBuffer = new List<GameObject>(128);
        private readonly HashSet<int> _usedBuffer = new HashSet<int>();

        public IA_SquadDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_SquadDirector"; }
        }

        public float Interval
        {
            get { return 1.80f; }
        }

        public float BudgetMs
        {
            get { return 2.20f; }
        }

        public void Tick(float now, float deltaTime)
        {
            _context.Backend.SquadService.CleanupDeadUnits();

            // Reutiliza os buffers para evitar alocacoes por tick
            _candidatesBuffer.Clear();
            _usedBuffer.Clear();
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit != null && unit.activeInHierarchy)
                {
                    _candidatesBuffer.Add(unit);
                }
            }

            UpdateRole(IA_SquadRole.Recon, _candidatesBuffer, _usedBuffer, 4, IsReconUnit);
            UpdateRole(IA_SquadRole.LocalDefense, _candidatesBuffer, _usedBuffer, 8, IsLocalDefenseUnit);
            UpdateRole(IA_SquadRole.BorderPatrol, _candidatesBuffer, _usedBuffer, 8, IsPatrolUnit);
            UpdateRole(IA_SquadRole.ArmoredAssault, _candidatesBuffer, _usedBuffer, 10, IsArmoredUnit);
            UpdateRole(IA_SquadRole.Amphibious, _candidatesBuffer, _usedBuffer, 6, IsAmphibiousUnit);
            UpdateRole(IA_SquadRole.NavalEscort, _candidatesBuffer, _usedBuffer, 5, IsNavalEscortUnit);
            UpdateRole(IA_SquadRole.NavalHeavy, _candidatesBuffer, _usedBuffer, 4, IsNavalHeavyUnit);
            UpdateRole(IA_SquadRole.Submarine, _candidatesBuffer, _usedBuffer, 3, IsSubmarineUnit);
            UpdateRole(IA_SquadRole.AirIntercept, _candidatesBuffer, _usedBuffer, 6, IsAirInterceptUnit);
            UpdateRole(IA_SquadRole.AirTacticalTransport, _candidatesBuffer, _usedBuffer, 4, IsAirTransportUnit);
        }

        public IA_SquadData GetSquad(IA_SquadRole role)
        {
            IA_SquadData squad;
            _byRole.TryGetValue(role, out squad);
            return squad;
        }

        public List<IA_SquadData> GetAllSquads()
        {
            return _context.Backend.SquadService.GetAll();
        }

        private void UpdateRole(
            IA_SquadRole role,
            List<GameObject> candidates,
            HashSet<int> used,
            int targetSize,
            System.Func<GameObject, bool> predicate)
        {
            List<GameObject> selected = new List<GameObject>();
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject unit = candidates[i];
                if (unit == null)
                {
                    continue;
                }

                int id = unit.GetInstanceID();
                if (used.Contains(id))
                {
                    continue;
                }

                if (!predicate(unit))
                {
                    continue;
                }

                selected.Add(unit);
                used.Add(id);
                if (selected.Count >= targetSize)
                {
                    break;
                }
            }

            string squadId = "squad_" + role;
            IA_SquadData squad = _context.Backend.SquadService.UpsertSquad(squadId, role, selected);
            _byRole[role] = squad;
        }

        private static bool IsReconUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsGroundTransport(unit, n))
            {
                return false;
            }

            return n.Contains("humvee")
                   || n.Contains("hamer")
                   || n.Contains("hover")
                   || n.Contains("recon")
                   || n.Contains("soldado");
        }

        private static bool IsLocalDefenseUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsGroundTransport(unit, n))
            {
                return false;
            }

            return n.Contains("soldado")
                   || n.Contains("rifle")
                   || n.Contains("mbt")
                   || n.Contains("tank")
                   || n.Contains("ciws")
                   || n.Contains("antia");
        }

        private static bool IsPatrolUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsGroundTransport(unit, n))
            {
                return false;
            }

            return n.Contains("humvee")
                   || n.Contains("tank")
                   || n.Contains("soldado")
                   || n.Contains("hover");
        }

        private static bool IsArmoredUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsGroundTransport(unit, n))
            {
                return false;
            }

            return n.Contains("tank")
                   || n.Contains("mbt")
                   || n.Contains("south")
                   || n.Contains("arthur")
                   || n.Contains("c1")
                   || n.Contains("hack")
                   || n.Contains("artilh");
        }

        private static bool IsAmphibiousUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            return n.Contains("hover")
                   || n.Contains("amphi")
                   || n.Contains("soldado");
        }

        private static bool IsNavalEscortUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (unit.GetComponent<ControleSubmarino>() != null)
            {
                return false;
            }

            bool heavyName = n.Contains("destroy")
                             || n.Contains("vindicator")
                             || n.Contains("ironclad")
                             || n.Contains("dominion")
                             || n.Contains("liberty")
                             || n.Contains("porta")
                             || n.Contains("sovereign");
            if (heavyName)
            {
                return false;
            }

            return unit.GetComponent<ControleNavioRealista>() != null
                   || n.Contains("corveta")
                   || n.Contains("escort")
                   || n.Contains("ww")
                   || n.Contains("arrowhead")
                   || n.Contains("lancha")
                   || n.Contains("wall")
                   || n.Contains("sam");
        }

        private static bool IsNavalHeavyUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            return (unit.GetComponent<ControleNavioRealista>() != null
                    && (n.Contains("destroy")
                        || n.Contains("vindicator")
                        || n.Contains("ironclad")
                        || n.Contains("dominion")
                        || n.Contains("liberty")
                        || n.Contains("porta")
                        || n.Contains("sovereign")))
                   || n.Contains("destroy")
                   || n.Contains("vindicator")
                   || n.Contains("ironclad")
                   || n.Contains("dominion")
                   || n.Contains("liberty")
                   || n.Contains("porta")
                   || n.Contains("sovereign");
        }

        private static bool IsSubmarineUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            return unit.GetComponent<ControleSubmarino>() != null
                   || n.Contains("sub")
                   || n.Contains("mako")
                   || n.Contains("wraith")
                   || n.Contains("leviathan");
        }

        private static bool IsAirInterceptUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            return unit.GetComponent<ControleAviao>() != null
                   || unit.GetComponent<ControleAviaoCaca>() != null
                   || n.Contains("fa1")
                   || n.Contains("caca")
                   || n.Contains("jet")
                   || n.Contains("aviao")
                   || n.Contains("a_20")
                   || n.Contains("g_18m")
                   || n.Contains("g18m")
                   || n.Contains("g15")
                   || n.Contains("super tuk")
                   || n.Contains("supertuk");
        }

        private static bool IsAirTransportUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            return unit.GetComponent<Helicoptero>() != null
                   || n.Contains("heli")
                   || n.Contains("ray")
                   || n.Contains("vans")
                   || n.Contains("guincho")
                   || (n.Contains("transport") && (n.Contains("aereo") || n.Contains("air")));
        }

        private static bool IsGroundTransport(GameObject unit, string normalizedName)
        {
            if (unit.GetComponent<TransporteTerrestre>() != null)
            {
                return true;
            }

            if (unit.GetComponent<ControleNavioRealista>() != null || unit.GetComponent<ControleSubmarino>() != null)
            {
                return false;
            }

            return normalizedName.Contains("truck")
                   || normalizedName.Contains("caminhao")
                   || (normalizedName.Contains("transporte")
                       && !normalizedName.Contains("aereo")
                       && !normalizedName.Contains("air")
                       && !normalizedName.Contains("heli")
                       && !normalizedName.Contains("ray")
                       && !normalizedName.Contains("vans"));
        }
    }
}
