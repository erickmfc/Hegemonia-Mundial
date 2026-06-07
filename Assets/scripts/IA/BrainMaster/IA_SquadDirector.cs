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
        private readonly List<GameObject> _selectedBuffer = new List<GameObject>(32);
        private readonly HashSet<int> _usedBuffer = new HashSet<int>();
        private float _nextDecisionTime;

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
            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + ResolveDecisionDelay();
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

            UpdateRole(IA_SquadRole.Recon, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.Recon, 4), IsReconUnit);
            UpdateRole(IA_SquadRole.LocalDefense, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.LocalDefense, 8), IsLocalDefenseUnit);
            UpdateRole(IA_SquadRole.BorderPatrol, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.BorderPatrol, 8), IsPatrolUnit);
            UpdateRole(IA_SquadRole.ArmoredAssault, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.ArmoredAssault, 10), IsArmoredUnit);
            UpdateRole(IA_SquadRole.Amphibious, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.Amphibious, 6), IsAmphibiousUnit);
            UpdateRole(IA_SquadRole.NavalEscort, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.NavalEscort, 5), IsNavalEscortUnit);
            UpdateRole(IA_SquadRole.NavalHeavy, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.NavalHeavy, 4), IsNavalHeavyUnit);
            UpdateRole(IA_SquadRole.Submarine, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.Submarine, 3), IsSubmarineUnit);
            UpdateRole(IA_SquadRole.AirIntercept, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.AirIntercept, 6), IsAirInterceptUnit);
            UpdateRole(IA_SquadRole.AirTacticalTransport, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.AirTacticalTransport, 4), IsAirTransportUnit);
            UpdateRole(IA_SquadRole.NavalTransport, _candidatesBuffer, _usedBuffer, ResolveTargetSize(IA_SquadRole.NavalTransport, 3), IsNavalTransportUnit);
            PublishSquadMetrics();

            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("formation_update_ms", elapsedMs);
            }
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
            _selectedBuffer.Clear();
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

                _selectedBuffer.Add(unit);
                used.Add(id);
                if (_selectedBuffer.Count >= targetSize)
                {
                    break;
                }
            }

            string squadId = "squad_" + role;
            IA_SquadData squad = _context.Backend.SquadService.UpsertSquad(squadId, role, _selectedBuffer);
            squad.Tier = ResolveTier(role);
            squad.EngagementCost = EstimateSquadCost(squad.Units);
            _byRole[role] = squad;
        }

        private int ResolveTargetSize(IA_SquadRole role, int baseSize)
        {
            IA_BattleGovernorDecision decision = _context != null ? _context.BattleDecision : null;
            if (decision == null)
            {
                return baseSize;
            }

            bool offensiveRole = role == IA_SquadRole.ArmoredAssault
                                 || role == IA_SquadRole.NavalEscort
                                 || role == IA_SquadRole.NavalHeavy
                                 || role == IA_SquadRole.Submarine
                                 || role == IA_SquadRole.AirIntercept;
            switch (decision.Band)
            {
                case IA_PerformanceGovernorBand.Critico:
                    return offensiveRole
                        ? Mathf.Clamp(Mathf.CeilToInt(baseSize * 0.35f), 1, baseSize)
                        : Mathf.Clamp(Mathf.CeilToInt(baseSize * 0.50f), 1, baseSize);
                case IA_PerformanceGovernorBand.Pressao:
                    return offensiveRole
                        ? Mathf.Clamp(Mathf.CeilToInt(baseSize * 0.60f), 1, baseSize)
                        : Mathf.Clamp(Mathf.CeilToInt(baseSize * 0.75f), 1, baseSize);
                default:
                    return baseSize;
            }
        }

        private IA_SimulationTier ResolveTier(IA_SquadRole role)
        {
            switch (role)
            {
                case IA_SquadRole.ArmoredAssault:
                case IA_SquadRole.NavalEscort:
                case IA_SquadRole.NavalHeavy:
                case IA_SquadRole.Submarine:
                case IA_SquadRole.AirIntercept:
                    return IA_SimulationTier.Combat;
                case IA_SquadRole.Amphibious:
                case IA_SquadRole.AirTacticalTransport:
                case IA_SquadRole.LocalDefense:
                case IA_SquadRole.BorderPatrol:
                case IA_SquadRole.Recon:
                    return IA_SimulationTier.Support;
                default:
                    return IA_SimulationTier.Reserve;
            }
        }

        private static int EstimateSquadCost(List<GameObject> units)
        {
            int total = 0;
            if (units == null)
            {
                return total;
            }

            for (int i = 0; i < units.Count; i++)
            {
                total += IA_BattleGovernorUtils.GetEngagementCost(units[i]);
            }

            return total;
        }

        private void PublishSquadMetrics()
        {
            int engagedUnits = CountRoleUnits(
                IA_SquadRole.ArmoredAssault,
                IA_SquadRole.NavalEscort,
                IA_SquadRole.NavalHeavy,
                IA_SquadRole.Submarine,
                IA_SquadRole.AirIntercept);
            int supportUnits = CountRoleUnits(
                IA_SquadRole.Recon,
                IA_SquadRole.LocalDefense,
                IA_SquadRole.BorderPatrol,
                IA_SquadRole.Amphibious,
                IA_SquadRole.AirTacticalTransport);
            int reserveUnits = Mathf.Max(0, _context.WorldState.OwnCombatUnits.Count - engagedUnits - supportUnits);

            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("engaged_units", engagedUnits);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("support_units", supportUnits);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("reserve_units", reserveUnits);
            if (_context != null && _context.PerformanceGovernorState != null)
            {
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("governor_band", _context.PerformanceGovernorState.Band.ToString());
            }
        }

        private int CountRoleUnits(params IA_SquadRole[] roles)
        {
            int count = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                IA_SquadData squad;
                if (_byRole.TryGetValue(roles[i], out squad) && squad != null && squad.Units != null)
                {
                    count += squad.Units.Count;
                }
            }

            return count;
        }

        private float ResolveDecisionDelay()
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 1.80f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 3.40f;
                case EstadoCargaIA.EmCombate:
                    return 2.40f;
                default:
                    return 1.80f;
            }
        }

        private static bool IsReconUnit(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsGroundTransport(unit, n) || IsHovercraftTransport(unit, n))
            {
                return false;
            }

            return n.Contains("humvee")
                   || n.Contains("hamer")
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
            if (IsGroundTransport(unit, n) || IsHovercraftTransport(unit, n))
            {
                return false;
            }

            return n.Contains("humvee")
                   || n.Contains("tank")
                   || n.Contains("soldado");
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
            return IsHovercraftTransport(unit, n)
                   || n.Contains("amphi");
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
                   || n.Contains("a10")
                   || n.Contains("a-10")
                   || n.Contains("warthog")
                   || n.Contains("thunderbolt")
                   || n.Contains("supra")
                   || n.Contains("b260")
                   || n.Contains("g_18m")
                   || n.Contains("g18m")
                   || n.Contains("g15")
                   || n.Contains("su11")
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

        private static bool IsHovercraftTransport(GameObject unit, string normalizedName)
        {
            return unit.GetComponent<HovercraftTransporte>() != null
                   || normalizedName.Contains("hovercraft")
                   || normalizedName.Contains("hover");
        }

        private static bool IsNavalTransportUnit(GameObject unit)
        {
            if (unit.GetComponent<NavioTransporteTropas>() != null)
            {
                return true;
            }

            string n = IA_Text.Normalize(unit.name);
            return n.Contains("transporte") && (n.Contains("navio") || n.Contains("mar") || n.Contains("ship"));
        }
    }
}
