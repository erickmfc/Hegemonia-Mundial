using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ProductionDirector : IIAUpdateModule
    {
        private const bool EnableHelicopterProduction = true;
        private const float TimedAirKickoffSeconds = 9f;
        private const float TimedNavalKickoffSeconds = 10f;
        private const float StructureStabilitySeconds = 2.5f;
        private const int TimedAirKickoffTarget = 1;
        private const int TimedNavalMinArmy = 0;
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private int _lastKnownStructureCount = -1;
        private float _lastStructureChangeTime;
        private bool _timedAirKickoffTriggered;
        private bool _timedNavalKickoffTriggered;
        private string _lastPreferredAircraftVariant = string.Empty;
        private IA_BrainMaster.IA_BootstrapStage _lastBootstrapStage = IA_BrainMaster.IA_BootstrapStage.Disabled;
        private int _bootstrapShipGoalCount = -1;

        public IA_ProductionDirector(IA_Context context)
        {
            _context = context;
        }

        public string Name
        {
            get { return "IA_ProductionDirector"; }
        }

        public float Interval
        {
            get { return 0.85f; }
        }

        public float BudgetMs
        {
            get { return 1.50f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (now < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = now + ResolveDecisionDelay();
            if (_context.CommandQueue.PendingCount > 14)
            {
                return;
            }

            long profileStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (TickBootstrapProduction(now))
                {
                    return;
                }

                IA_CounterPlan counter = _context.PlayerProfileMemory.BuildCounterPlan();
                IA_ForceSnapshot snapshot = GetSnapshot();

                int infantryCount = snapshot.InfantryUnits;
                int tankCount = snapshot.TankUnits;
                int artyCount = snapshot.ArtilleryUnits;
                int helicopterCount = snapshot.Helicopters;
                int fighterCount = snapshot.FixedWingAircraft;
                int airCount = helicopterCount + fighterCount;
                int navalCount = snapshot.NavalUnits;
                int submarineCount = snapshot.Submarines;
                int truckCount = snapshot.GroundTransports;
                int hoverCount = snapshot.HoverTransports;
                bool hasBarracks = snapshot.HasBarracks;
                bool hasFactory = snapshot.HasFactory;
                bool hasAirport = snapshot.HasAirport;
                bool hasHeliport = snapshot.HasHeliport;
                bool hasNavalBase = snapshot.HasNavalBase;
                int structureCount = snapshot.TotalOwnStructures;
                UpdateStructureTracker(now, structureCount);

            int infantryTarget = 16 + (counter.AntiRush ? 10 : 0) + Mathf.RoundToInt(counter.LandWeight * 7f);
            int tankTarget = 7 + Mathf.RoundToInt(counter.LandWeight * 7f);
            int artyTarget = 2 + (counter.ReinforceCenter ? 1 : 0);
            int helicopterTarget = EnableHelicopterProduction && hasHeliport
                ? Mathf.Clamp(2 + Mathf.RoundToInt(counter.AirWeight * 3f), 2, 4)
                : 0;
            int fighterTarget = hasAirport ? Mathf.Clamp(2 + Mathf.RoundToInt(counter.AirWeight * 4f), 2, 5) : 0;
            int fleetCombatCount = navalCount + submarineCount;
            int patrolShipTarget = hasNavalBase ? 2 : 0;
            int navalTarget = hasNavalBase
                ? Mathf.Clamp(3 + Mathf.RoundToInt(counter.NavalWeight * 3f), 3, 5)
                : 0;
            int subTarget = hasNavalBase && fleetCombatCount >= 2 && (counter.ReinforceCoast || counter.NavalWeight > 0.18f)
                ? 1
                : 0;
            int truckTarget = infantryCount >= 8 ? 1 : 0;
            int hoverTarget = hasNavalBase
                && fleetCombatCount >= 4
                && infantryCount >= 14
                && counter.ReinforceCoast
                ? 1
                : 0;
            int armyCount = Mathf.Max(
                snapshot.TotalCombatUnits,
                infantryCount + tankCount + artyCount + airCount + navalCount + submarineCount + hoverCount);
            bool structuresStable = now - _lastStructureChangeTime >= StructureStabilitySeconds;

            if (!_timedAirKickoffTriggered && now >= TimedAirKickoffSeconds && hasAirport && fighterCount >= TimedAirKickoffTarget)
            {
                _timedAirKickoffTriggered = true;
            }

            if (!_timedNavalKickoffTriggered && now >= TimedNavalKickoffSeconds && navalCount >= 1)
            {
                _timedNavalKickoffTriggered = true;
            }

            if (!_timedAirKickoffTriggered
                && now >= TimedAirKickoffSeconds
                && hasAirport
                && fighterCount < TimedAirKickoffTarget
                && QueuePreferredAircraft(99, 2.5f))
            {
                return;
            }

            if (!_timedNavalKickoffTriggered
                && now >= TimedNavalKickoffSeconds
                && hasNavalBase
                && structuresStable
                && armyCount >= TimedNavalMinArmy
                && navalCount < 1
                && QueueSurfaceFleetStep(navalCount, 98, 4.5f))
            {
                _timedNavalKickoffTriggered = true;
                return;
            }

            if (hasNavalBase && navalCount < 1 && QueueSurfaceFleetStep(navalCount, 93, 5.5f))
            {
                return;
            }

            if (hasBarracks && infantryCount < 4 && QueueProduceBest(94, 5f, "soldado rifle", "soldado", "infantaria", "rifle"))
            {
                return;
            }

            if (hasAirport && fighterCount == 0 && QueuePreferredAircraft(92, 5.5f))
            {
                return;
            }

            if (hasNavalBase && navalCount < 2 && QueueSurfaceFleetStep(navalCount, 91, 6.5f))
            {
                return;
            }

            if (hasAirport && fighterCount < 2 && QueuePreferredAircraft(91, 4f))
            {
                return;
            }

            if (hasBarracks && infantryCount < infantryTarget && QueueProduceBest(90, 4f, "soldado rifle", "soldado", "infantaria", "rifle"))
            {
                return;
            }

            if (hasFactory && tankCount < tankTarget && QueueProduceBest(86, 5f, "tank mbt", "mbt", "tank south", "tank c1", "tank arthur"))
            {
                return;
            }

            if (hasFactory && artyCount < artyTarget && QueueProduceBest(82, 6f, "hack", "artilharia", "lancador"))
            {
                return;
            }

            if (truckCount < truckTarget && infantryCount >= 8 && hasFactory && QueueProduceBest(77, 10f, "caminhao de transporte", "transporte", "truck"))
            {
                return;
            }

            if (hoverCount < hoverTarget && infantryCount >= 14 && fleetCombatCount >= 4 && QueueProduceBest(76, 12f, "hover"))
            {
                return;
            }

            if (EnableHelicopterProduction && hasHeliport && helicopterCount < helicopterTarget && QueueProduceBest(83, 6f, "vans", "helicoptero de combate", "helicoptero ray", "ray", "helicoptero"))
            {
                return;
            }

            if (hasAirport && fighterCount < fighterTarget && airCount < helicopterTarget + fighterTarget && counter.AirWeight > 0.20f && QueuePreferredAircraft(88, 7f))
            {
                return;
            }

                if (hasNavalBase)
                {
                    if (navalCount < patrolShipTarget && QueueSurfaceFleetStep(navalCount, 85, 7f))
                    {
                        return;
                    }

                    if (submarineCount < subTarget && QueueProduceBest(84, 14f, "uss mako", "submarino", "uss wraith", "uss leviathan"))
                    {
                        return;
                    }

                    if (navalCount < navalTarget && QueueSurfaceFleetStep(navalCount, 80, 7f))
                    {
                        return;
                    }
                }
            }
            finally
            {
                RegistrarTempoProducao(profileStart);
            }
        }

        private float ResolveDecisionDelay()
        {
            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return 0.75f;
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return 4.00f;
                case EstadoCargaIA.EmCombate:
                    return 2.00f;
                default:
                    return 0.75f;
            }
        }

        private bool QueueProduceBest(int priority, float cooldown, params string[] keys)
        {
            DadosConstrucao data = _context.Backend.FindFirstAvailable(keys);
            if (data == null)
            {
                return false;
            }

            IA_ProduceOrderData payload = new IA_ProduceOrderData
            {
                ItemKey = data.nomeItem,
                Quantity = 1
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Produce,
                Priority = priority,
                DedupKey = "produce:" + IA_Text.Normalize(data.nomeItem),
                CooldownSeconds = cooldown,
                Payload = payload
            };

            string reason;
            bool enqueued = _context.CommandQueue.Enqueue(request, Time.time, out reason);
            if (enqueued)
            {
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.nomeItem);
            }

            return enqueued;
        }

        private bool QueuePreferredAircraft(int priority, float cooldown)
        {
            DadosConstrucao data = ChoosePreferredAircraftVariant();
            if (data == null)
            {
                return QueueProduceBest(priority, cooldown, "a_20", "g15", "super tuk", "g_18m", "g18m", "fa1", "caca", "aviao");
            }

            IA_ProduceOrderData payload = new IA_ProduceOrderData
            {
                ItemKey = data.nomeItem,
                Quantity = 1
            };

            IA_CommandRequest request = new IA_CommandRequest
            {
                Type = IA_CommandType.Produce,
                Priority = priority,
                DedupKey = "produce:" + IA_Text.Normalize(data.nomeItem),
                CooldownSeconds = cooldown,
                Payload = payload
            };

            string reason;
            bool enqueued = _context.CommandQueue.Enqueue(request, Time.time, out reason);
            if (enqueued)
            {
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.nomeItem, "IA_Prod_Air");
            }

            return enqueued;
        }

        private DadosConstrucao ChoosePreferredAircraftVariant()
        {
            if (_context.Backend.Catalog == null || _context.Backend.Catalog.Count == 0)
            {
                _context.Backend.RefreshCatalog();
            }

            var grouped = new Dictionary<string, List<DadosConstrucao>>();
            IReadOnlyList<DadosConstrucao> catalog = _context.Backend.Catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                DadosConstrucao data = catalog[i];
                string variant = GetPreferredAircraftVariant(data);
                if (string.IsNullOrEmpty(variant))
                {
                    continue;
                }

                List<DadosConstrucao> items;
                if (!grouped.TryGetValue(variant, out items))
                {
                    items = new List<DadosConstrucao>();
                    grouped.Add(variant, items);
                }

                items.Add(data);
            }

            if (grouped.Count == 0)
            {
                return null;
            }

            var variants = new List<string>(grouped.Keys);
            if (variants.Count > 1 && !string.IsNullOrEmpty(_lastPreferredAircraftVariant))
            {
                variants.Remove(_lastPreferredAircraftVariant);
                if (variants.Count == 0)
                {
                    variants = new List<string>(grouped.Keys);
                }
            }

            string selectedVariant = variants[Random.Range(0, variants.Count)];
            List<DadosConstrucao> options = grouped[selectedVariant];
            DadosConstrucao selected = options[Random.Range(0, options.Count)];
            _lastPreferredAircraftVariant = selectedVariant;
            return selected;
        }

        private static string GetPreferredAircraftVariant(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return string.Empty;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + data.prefabDaUnidade.name);
            if (joined.Contains("g15") || joined.Contains("garciag15"))
            {
                return "g15";
            }

            if (joined.Contains("a_20") || joined.Contains("a20") || joined.Contains("a 20"))
            {
                return "a20";
            }

            if (joined.Contains("super tuk") || joined.Contains("supertuk") || joined.Contains("super_tuk") || joined.Contains("tuk"))
            {
                return "super_tuk";
            }

            return string.Empty;
        }

        private bool QueueSurfaceFleetStep(int navalCount, int priority, float cooldown)
        {
            if (navalCount <= 0)
            {
                return QueueProduceBest(priority, cooldown, "corveta sam", "uss arrowhead", "arrowhead", "wall", "navio wall", "lancha", "ww", "corveta");
            }

            if (navalCount == 1)
            {
                return QueueProduceBest(priority, cooldown, "wall", "navio wall", "uss arrowhead", "arrowhead", "corveta sam", "destroy", "destroyer", "vindicator", "lancha", "ww", "corveta");
            }

            if (navalCount == 2)
            {
                return QueueProduceBest(priority, cooldown, "uss ironclad", "ironclad", "uss sovereign", "sovereign", "vindicator", "destroy", "destroyer", "dominion", "liberty");
            }

            if (navalCount < 5)
            {
                return QueueProduceBest(priority, cooldown, "uss sovereign", "sovereign", "uss ironclad", "ironclad", "dominion", "liberty", "vindicator", "destroy", "destroyer", "porta");
            }

            return false;
        }

        private bool TickBootstrapProduction(float now)
        {
            IA_BrainMaster brain = _context.Brain;
            if (brain == null || !brain.IsBootstrapActive)
            {
                _lastBootstrapStage = IA_BrainMaster.IA_BootstrapStage.Completed;
                _bootstrapShipGoalCount = -1;
                return false;
            }

            IA_BrainMaster.IA_BootstrapStage stage = brain.BootstrapStage;
            if (stage != _lastBootstrapStage)
            {
                _lastBootstrapStage = stage;
                if (stage == IA_BrainMaster.IA_BootstrapStage.ProduceShip)
                {
                    _bootstrapShipGoalCount = GetSnapshot().NavalUnits + 1;
                }
            }

            IA_ForceSnapshot snapshot = GetSnapshot();
            int infantryCount = snapshot.InfantryUnits;
            int tankCount = snapshot.TankUnits;
            int artyCount = snapshot.ArtilleryUnits;
            int fighterCount = snapshot.FixedWingAircraft;
            int navalCount = snapshot.NavalUnits;
            bool hasBarracks = snapshot.HasBarracks;
            bool hasFactory = snapshot.HasFactory;
            bool hasAirport = snapshot.HasAirport;
            bool hasNavalBase = snapshot.HasNavalBase;

            switch (stage)
            {
                case IA_BrainMaster.IA_BootstrapStage.ProduceGroundUnits:
                    brain.SetBootstrapStatus("bootstrap: produzindo exercito terrestre por 10s");
                    if (brain.GetBootstrapStageElapsed(now) >= 10f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.HoldGroundUnits, "pausa de 5s para organizar tropas no patio");
                        return true;
                    }

                    if (hasBarracks && infantryCount < 6 && QueueProduceBest(999, 2.2f, "soldado rifle", "soldado", "infantaria", "rifle"))
                    {
                        return true;
                    }

                    if (hasFactory && tankCount < 2 && QueueProduceBest(998, 2.8f, "tank mbt", "mbt", "tank south", "tank c1", "tank arthur"))
                    {
                        return true;
                    }

                    if (hasFactory && artyCount < 1 && QueueProduceBest(997, 3.2f, "hack", "artilharia", "lancador"))
                    {
                        return true;
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.HoldGroundUnits:
                    brain.SetBootstrapStatus("bootstrap: tropas em espera, sem movimento, aguardando fase aerea");
                    if (brain.GetBootstrapStageElapsed(now) >= 5f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.ProduceAircraft, "liberando compra de avioes");
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.ProduceAircraft:
                    brain.SetBootstrapStatus("bootstrap: produzindo avioes e mantendo no patio");
                    if (brain.GetBootstrapStageElapsed(now) >= 5f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.BuildShipyard, "liberando construcao obrigatoria do estaleiro");
                        return true;
                    }

                    if (!hasAirport)
                    {
                        brain.ReportBootstrapError("avioes: aeroporto indisponivel");
                        return true;
                    }

                    if (fighterCount < 2 && QueuePreferredAircraft(996, 2.6f))
                    {
                        return true;
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.ProduceShip:
                    brain.SetBootstrapStatus("bootstrap: produzindo primeiro navio");
                    if (!hasNavalBase)
                    {
                        brain.ReportBootstrapError("navio: estaleiro/pier indisponivel");
                        return true;
                    }

                    if (_bootstrapShipGoalCount < 0)
                    {
                        _bootstrapShipGoalCount = navalCount + 1;
                    }

                    if (navalCount >= _bootstrapShipGoalCount)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.HoldShipLaunch, "primeiro navio pronto; aguardando 10s para liberar jogo");
                        return true;
                    }

                    if (QueueSurfaceFleetStep(navalCount, 995, 4.5f))
                    {
                        return true;
                    }

                    brain.ReportBootstrapError("navio: falha ao enfileirar producao naval");
                    return true;

                case IA_BrainMaster.IA_BootstrapStage.HoldShipLaunch:
                    brain.SetBootstrapStatus("bootstrap: segurando ordens, aguardando navio sair em seguranca");
                    if (brain.GetBootstrapStageElapsed(now) >= 10f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.Completed, "bootstrap concluido; IA liberada para jogar");
                    }

                    return true;

                default:
                    return true;
            }
        }

        private IA_ForceSnapshot GetSnapshot()
        {
            if (_context != null && _context.ForceSnapshot != null)
            {
                return _context.ForceSnapshot;
            }

            if (_context != null && _context.WorldState != null && _context.WorldState.ForceSnapshot != null)
            {
                return _context.WorldState.ForceSnapshot;
            }

            return new IA_ForceSnapshot();
        }

        private static void RegistrarTempoProducao(long profileStart)
        {
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - profileStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("production_ms", elapsedMs);
            }
        }

        private int CountUnits(params string[] hints)
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && name.Contains(hint))
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private int CountHelicopters()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                if (unit.GetComponent<Helicoptero>() != null
                    || name.Contains("heli")
                    || name.Contains("ray")
                    || name.Contains("vans"))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountFixedWingAircraft()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                if (unit.GetComponent<ControleAviao>() != null
                    || unit.GetComponent<ControleAviaoCaca>() != null
                    || name.Contains("fa1")
                    || name.Contains("caca")
                    || name.Contains("jet")
                    || name.Contains("aviao")
                    || name.Contains("a_20")
                    || name.Contains("g_18m")
                    || name.Contains("g18m")
                    || name.Contains("g15")
                    || name.Contains("super tuk")
                    || name.Contains("supertuk"))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountNavalShips()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null || unit.GetComponent<ControleSubmarino>() != null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                if (unit.GetComponent<ControleNavioRealista>() != null
                    || name.Contains("navio")
                    || name.Contains("corveta")
                    || name.Contains("destroy")
                    || name.Contains("ironclad")
                    || name.Contains("dominion")
                    || name.Contains("vindicator")
                    || name.Contains("arrowhead")
                    || name.Contains("lancha")
                    || name.Contains("ww")
                    || name.Contains("wall"))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountSubmarines()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                if (unit.GetComponent<ControleSubmarino>() != null
                    || name.Contains("sub")
                    || name.Contains("mako")
                    || name.Contains("wraith")
                    || name.Contains("leviathan"))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountGroundTransports()
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.GetComponent<ControleNavioRealista>() != null || unit.GetComponent<ControleSubmarino>() != null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                bool isGroundTransport = unit.GetComponent<TransporteTerrestre>() != null
                                         || name.Contains("truck")
                                         || name.Contains("caminhao")
                                         || (name.Contains("transporte")
                                             && !name.Contains("aereo")
                                             && !name.Contains("heli")
                                             && !name.Contains("ray")
                                             && !name.Contains("vans"));
                if (isGroundTransport)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountStructures(params string[] hints)
        {
            int count = 0;
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(structure.name);
                for (int h = 0; h < hints.Length; h++)
                {
                    string hint = IA_Text.Normalize(hints[h]);
                    if (!string.IsNullOrEmpty(hint) && name.Contains(hint))
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private bool HasOwnedStructureComponent<T>() where T : Component
        {
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                if (structure.GetComponent<T>() != null || structure.GetComponentInChildren<T>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateStructureTracker(float now, int structureCount)
        {
            if (_lastKnownStructureCount < 0)
            {
                _lastKnownStructureCount = structureCount;
                _lastStructureChangeTime = now;
                return;
            }

            if (structureCount != _lastKnownStructureCount)
            {
                _lastKnownStructureCount = structureCount;
                _lastStructureChangeTime = now;
            }
        }
    }
}
