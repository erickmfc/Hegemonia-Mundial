using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ProductionDirector : IIAUpdateModule
    {
        private const bool EnableHelicopterProduction = true;
        private const float TimedAirKickoffSeconds = 8f;
        private const float TimedNavalKickoffSeconds = 9f;
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
        private float _nextRuntimeProductionQueueTime;
        private readonly List<Estaleiro> _registeredShipyardBuffer = new List<Estaleiro>();
        private readonly List<PierMarinha> _registeredPierBuffer = new List<PierMarinha>();

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

            IA_BrainMaster brain = _context.Brain;
            if (ShouldHoldForRuntimePressure(now) && (brain == null || !brain.IsBootstrapActive))
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
                IA_BattleGovernorDecision decision = GetBattleDecision();

                int infantryCount = snapshot.InfantryUnits;
                int tankCount = snapshot.TankUnits;
                int artyCount = snapshot.ArtilleryUnits;
                int helicopterCount = snapshot.Helicopters;
                int fighterCount = snapshot.FixedWingAircraft;
                int commercialAircraftCount = snapshot.CommercialAircraft;
                int airCount = helicopterCount + fighterCount;
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("commercial_aircraft_count", commercialAircraftCount);
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("fighters_ready", snapshot.ReadyFighters);
                int navalCount = snapshot.NavalUnits;
                int submarineCount = snapshot.Submarines;
                int oilTankerCount = snapshot.OilTankers;
                int truckCount = snapshot.GroundTransports;
                int hoverCount = snapshot.HoverTransports;
                bool hasBarracks = snapshot.HasBarracks;
                bool hasFactory = snapshot.HasFactory;
                bool hasMilitaryAirport = snapshot.HasMilitaryAirport;
                bool hasHeliport = snapshot.HasHeliport;
                bool hasNavalBase = snapshot.HasNavalBase || HasImmediateNavalBase();
                int structureCount = snapshot.TotalOwnStructures;
                int fleetCombatCount = navalCount + submarineCount;
                UpdateStructureTracker(now, structureCount);

                Vector3 baseCenter = ResolveBaseCenter();
                IA_TransportPlan transportPlan = BuildTransportPlan(
                    now,
                    snapshot,
                    baseCenter,
                    infantryCount,
                    tankCount,
                    artyCount,
                    helicopterCount,
                    fighterCount,
                    fleetCombatCount);
                _context.TransportPlan = transportPlan;
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica(
                    "transport_capacity_ready",
                    transportPlan != null && transportPlan.Ready ? transportPlan.AvailableCapacity : 0);

                bool suppressExpansion = decision != null && decision.SuppressEconomicExpansion;
                // Só vira "obrigatório" preparar transporte quando existe uma invasão real para fazer.
                // Caso contrário, a IA pode continuar evoluindo normalmente (sem travar a produção).
                bool mustPrepareTransport = transportPlan != null
                                           && !transportPlan.HasLandRoute
                                           && !transportPlan.Ready
                                           && transportPlan.RequiredCapacity >= 8;
                bool shouldPauseGroundMass = ShouldPauseOffensiveGroundMass(transportPlan, infantryCount, tankCount, artyCount, decision);

                int infantryTarget = 16 + (counter.AntiRush ? 10 : 0) + Mathf.RoundToInt(counter.LandWeight * 7f);
                int tankTarget = 7 + Mathf.RoundToInt(counter.LandWeight * 7f);
                int artyTarget = 2 + (counter.ReinforceCenter ? 1 : 0);
                int helicopterTarget = EnableHelicopterProduction && hasHeliport
                    ? Mathf.Clamp(2 + Mathf.RoundToInt(counter.AirWeight * 3f), 2, 5)
                    : 0;
                int fighterTarget = hasMilitaryAirport ? Mathf.Clamp(8 + Mathf.RoundToInt(counter.AirWeight * 8f) + (int)(now / 75f), 8, 24) : 0;
                int patrolShipTarget = hasNavalBase ? 1 : 0;
                int navalTarget = hasNavalBase
                    ? Mathf.Clamp(3 + Mathf.RoundToInt(counter.NavalWeight * 5f), 3, 12)
                    : 0;
                int oilTankerTarget = hasNavalBase && snapshot.PlatformCount > 0 && snapshot.PierCount > 0 ? 1 : 0;
                if (brain != null)
                {
                    fighterTarget = hasMilitaryAirport ? Mathf.Max(fighterTarget, brain.TargetAircraft) : 0;
                    navalTarget = hasNavalBase ? Mathf.Max(navalTarget, brain.TargetFleet) : 0;
                    oilTankerTarget = hasNavalBase && snapshot.PlatformCount > 0 && snapshot.PierCount > 0
                        ? Mathf.Max(oilTankerTarget, brain.TargetOilTankers)
                        : 0;
                        
                    if (brain.ActiveImperialPlan == "invasao_anfibia_combinada")
                    {
                        helicopterTarget = Mathf.Max(helicopterTarget, 4);
                        navalTarget = Mathf.Max(navalTarget, 6);
                        infantryTarget = Mathf.Max(infantryTarget, 20);
                        tankTarget = Mathf.Max(tankTarget, 6);
                    }
                }
                int subTarget = hasNavalBase && fleetCombatCount >= 1 && (counter.ReinforceCoast || counter.NavalWeight > 0.18f)
                    ? 1
                    : 0;
                int truckTarget = infantryCount >= 8 ? 1 : 0;
                int hoverTarget = hasNavalBase
                    && fleetCombatCount >= 2
                    && infantryCount >= 10
                    && counter.ReinforceCoast
                    ? 1
                    : 0;
                if (_context.Brain != null && _context.Brain.WarPosture == IA_WarPosture.BalancedAggression)
                {
                    if (now < 300f)
                    {
                        infantryTarget = Mathf.Max(infantryTarget, 6);
                        tankTarget = Mathf.Max(tankTarget, hasFactory ? 2 : 0);
                        fighterTarget = Mathf.Max(fighterTarget, hasMilitaryAirport ? 2 : 0);
                        navalTarget = Mathf.Max(navalTarget, hasNavalBase ? 1 : 0);
                    }
                    else if (now < 900f)
                    {
                        infantryTarget = Mathf.Max(infantryTarget, 16);
                        tankTarget = Mathf.Max(tankTarget, hasFactory ? 6 : 0);
                        fighterTarget = Mathf.Max(fighterTarget, hasMilitaryAirport ? 8 : 0);
                        navalTarget = Mathf.Max(navalTarget, hasNavalBase ? 4 : 0);
                    }
                    else
                    {
                        infantryTarget = Mathf.Max(infantryTarget, 20);
                        tankTarget = Mathf.Max(tankTarget, hasFactory ? 8 : 0);
                        fighterTarget = Mathf.Max(fighterTarget, hasMilitaryAirport ? 12 : 0);
                        navalTarget = Mathf.Max(navalTarget, hasNavalBase ? 5 : 0);
                    }
                }
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("fighter_target", fighterTarget);
                int armyCount = Mathf.Max(
                    snapshot.TotalCombatUnits,
                    infantryCount + tankCount + artyCount + airCount + navalCount + submarineCount + hoverCount);
                bool structuresStable = now - _lastStructureChangeTime >= StructureStabilitySeconds;

                if (suppressExpansion)
                {
                    infantryTarget = Mathf.Min(infantryTarget, 10);
                    tankTarget = Mathf.Min(tankTarget, 3);
                    artyTarget = Mathf.Min(artyTarget, 1);
                    helicopterTarget = Mathf.Min(helicopterTarget, hasHeliport ? 2 : 0);
                    bool recoveryOverride = brain != null && brain.WeakEmpireRecoveryActive;
                    fighterTarget = recoveryOverride ? fighterTarget : Mathf.Min(fighterTarget, hasMilitaryAirport ? 8 : 0);
                    navalTarget = recoveryOverride ? navalTarget : Mathf.Min(navalTarget, hasNavalBase ? 6 : 0);
                }

                if (mustPrepareTransport)
                {
                    infantryTarget = Mathf.Min(infantryTarget, Mathf.Max(6, transportPlan.AvailableCapacity + 2));
                    tankTarget = Mathf.Min(tankTarget, transportPlan.AvailableCapacity >= 8 ? 2 : 1);
                    artyTarget = Mathf.Min(artyTarget, transportPlan.AvailableCapacity >= 10 ? 1 : 0);
                }

                if (!_timedAirKickoffTriggered && now >= TimedAirKickoffSeconds && hasMilitaryAirport && fighterCount >= TimedAirKickoffTarget)
                {
                    _timedAirKickoffTriggered = true;
                }

                if (!_timedNavalKickoffTriggered && now >= TimedNavalKickoffSeconds && navalCount >= 1)
                {
                    _timedNavalKickoffTriggered = true;
                }

                if (!_timedAirKickoffTriggered
                    && now >= TimedAirKickoffSeconds
                    && hasMilitaryAirport
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

                if (mustPrepareTransport || (brain != null && brain.ActiveImperialPlan == "invasao_anfibia_combinada"))
                {
                    if (hasNavalBase && (!transportPlan.EscortReady || navalCount < 4) && QueueSurfaceFleetStep(navalCount, 97, 4.5f))
                    {
                        return;
                    }

                    if (!transportPlan.AirCoverReady || (brain != null && brain.ActiveImperialPlan == "invasao_anfibia_combinada" && helicopterCount < 3))
                    {
                        if (hasMilitaryAirport && fighterCount < Mathf.Max(2, fighterTarget) && QueuePreferredAircraft(96, 5.5f))
                        {
                            return;
                        }

                        if (EnableHelicopterProduction
                            && hasHeliport
                            && helicopterCount < Mathf.Max(1, helicopterTarget)
                            && QueueProduceBest(95, 4f, "vans", "helicoptero de combate", "helicoptero ray", "ray", "helicoptero"))
                        {
                            return;
                        }
                    }

                    if (hasNavalBase
                        && transportPlan.AvailableCapacity < transportPlan.RequiredCapacity
                        && QueueTransportLift(94, 10f))
                    {
                        return;
                    }
                }

                if (hasNavalBase && navalCount < 1 && QueueSurfaceFleetStep(navalCount, 93, 5.5f))
                {
                    return;
                }

                if (hasNavalBase
                    && oilTankerCount < oilTankerTarget
                    && QueueOilTanker(96, 5.0f))
                {
                    return;
                }

                // Avioes tem prioridade quando a frota aerea esta abaixo do minimo
                if (hasMilitaryAirport && fighterCount < 6 && QueuePreferredAircraft(95, 2.6f))
                {
                    return;
                }

                if (hasBarracks && infantryCount < 4 && QueueProduceBest(94, 5f, "tropa navy", "soldado rifle", "soldado", "infantaria", "rifle"))
                {
                    return;
                }

                if (hasNavalBase && navalCount < 2 && QueueSurfaceFleetStep(navalCount, 91, 4.6f))
                {
                    return;
                }

                // Se não temos base naval ainda, não pode travar o resto da produção:
                // a preparação de invasão depende do BuildDirector criar a infraestrutura primeiro.
                if (mustPrepareTransport && hasNavalBase)
                {
                    return;
                }

                // Garante que o aeroporto NUNCA fique vazio, reposição constante com alta prioridade
                if (hasMilitaryAirport && fighterCount < 8 && QueuePreferredAircraft(93, 3.0f))
                {
                    return;
                }

                if (hasNavalBase
                    && oilTankerCount < oilTankerTarget
                    && QueueOilTanker(89, 7.0f))
                {
                    return;
                }

                if (shouldPauseGroundMass)
                {
                    if (truckCount < truckTarget && hasFactory && QueueProduceBest(80, 10f, "caminhao de transporte", "transporte", "truck"))
                    {
                        return;
                    }

                    return;
                }

                if (hasBarracks && infantryCount < infantryTarget && QueueProduceBest(90, 3.0f, "tropa navy", "soldado rifle", "soldado", "infantaria", "rifle"))
                {
                    return;
                }

                if (hasFactory && tankCount < tankTarget && QueueProduceBest(86, 3.6f, "tank mbt", "mbt", "tank south", "tank c1", "tank arthur"))
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

                if (hasNavalBase
                    && transportPlan != null
                    && transportPlan.AvailableCapacity < transportPlan.RequiredCapacity
                    && QueueTransportLift(76, 12f))
                {
                    return;
                }

                if (hoverCount < hoverTarget && infantryCount >= 14 && fleetCombatCount >= 4 && QueueProduceBest(76, 12f, "hover"))
                {
                    return;
                }

                if (EnableHelicopterProduction && hasHeliport && helicopterCount < helicopterTarget && QueueProduceBest(83, 4.5f, "vans", "helicoptero de combate", "helicoptero ray", "ray", "helicoptero"))
                {
                    return;
                }

                // Mantém a produção rodando até bater o teto dinâmico (buffer extra), não mais bloqueado por AirWeight
                if (hasMilitaryAirport && fighterCount < fighterTarget && airCount < helicopterTarget + fighterTarget && QueuePreferredAircraft(89, 2.8f))
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

                    if (!suppressExpansion && navalCount < navalTarget && QueueSurfaceFleetStep(navalCount, 80, 7f))
                    {
                        return;
                    }

                    if (oilTankerCount < oilTankerTarget && QueueOilTanker(82, 10f))
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
            float governorDelay = 0f;
            IA_BattleGovernorDecision decision = GetBattleDecision();
            if (decision != null && decision.ProductionCooldownSeconds > 0f)
            {
                governorDelay = decision.ProductionCooldownSeconds * 0.65f;
            }

            if (ShouldRespectRuntimeLock() && DiagnosticoDesempenhoJogo.RuntimeSaturado())
            {
                return Mathf.Max(4.00f, governorDelay);
            }

            if (ShouldRespectRuntimeLock() && DiagnosticoDesempenhoJogo.RuntimeSobPressao())
            {
                return Mathf.Max(2.00f, governorDelay);
            }

            IA_CombatPressure pressure = _context != null ? _context.CombatPressure : null;
            if (pressure == null)
            {
                return Mathf.Max(0.75f, governorDelay);
            }

            switch (pressure.Estado)
            {
                case EstadoCargaIA.Saturado:
                    return Mathf.Max(4.00f, governorDelay);
                case EstadoCargaIA.EmCombate:
                    return Mathf.Max(2.00f, governorDelay);
                default:
                    return Mathf.Max(0.75f, governorDelay);
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
                ArmRuntimeQueueCooldown();
            }

            return enqueued;
        }

        private bool QueueOilTanker(int priority, float cooldown)
        {
            return QueueProduceBest(priority, cooldown, "petroleiro", "navio petroleiro", "navio petrolifero", "oil tanker", "tanker");
        }

        private bool QueuePreferredAircraft(int priority, float cooldown)
        {
            DadosConstrucao data = ChoosePreferredAircraftVariant();
            if (data == null)
            {
                return QueueProduceBest(priority, cooldown, "b260", "supra", "su11", "a_20", "a10", "warthog", "g15", "super tuk", "g_18m", "g18m", "fa1", "caca", "fighter");
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
                ArmRuntimeQueueCooldown();
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
            GameObject prefab;
            if (data == null || !data.TryGetPrefab(out prefab))
            {
                return string.Empty;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.name + " " + prefab.name);
            if (joined.Contains("b260") || joined.Contains("b-260") || joined.Contains("b 260"))
            {
                return "b260";
            }

            if (joined.Contains("supra"))
            {
                return "supra";
            }

            if (joined.Contains("g15") || joined.Contains("garciag15"))
            {
                return "g15";
            }

            if (joined.Contains("a_20") || joined.Contains("a20") || joined.Contains("a 20"))
            {
                return "a20";
            }

            if (joined.Contains("a10") || joined.Contains("a-10") || joined.Contains("warthog") || joined.Contains("thunderbolt"))
            {
                return "a10";
            }

            if (joined.Contains("super tuk") || joined.Contains("supertuk") || joined.Contains("super_tuk") || joined.Contains("tuk"))
            {
                return "super_tuk";
            }

            if (joined.Contains("su11") || joined.Contains("su-11"))
            {
                return "su11";
            }

            // Qualquer aeronave militar cadastrada pode entrar na rotacao. O ID
            // estavel evita que variantes com nomes parecidos sejam agrupadas.
            IA_ConstructionCapability capabilities = data.GetResolvedCapabilities();
            bool isCommercial = (capabilities & IA_ConstructionCapability.CommercialAircraft) != 0
                                || prefab.GetComponent<ControleAviaoComercial>() != null;
            bool isFixedWing = (capabilities & IA_ConstructionCapability.Aircraft) != 0
                               || (capabilities & IA_ConstructionCapability.FighterAircraft) != 0
                               || prefab.GetComponent<ControleAviao>() != null
                               || prefab.GetComponent<ControleAviaoCaca>() != null;
            bool isHelicopter = (capabilities & IA_ConstructionCapability.Helicopter) != 0
                                || prefab.GetComponent<Helicoptero>() != null;
            bool isCombatAircraft = (capabilities & IA_ConstructionCapability.FighterAircraft) != 0
                                    || prefab.GetComponent<ControleAviaoCaca>() != null
                                    || joined.Contains("caca")
                                    || joined.Contains("fighter")
                                    || joined.Contains("bombardeiro")
                                    || joined.Contains("bomber")
                                    || joined.Contains("jet")
                                    || joined.Contains("supra")
                                    || joined.Contains("su11")
                                    || joined.Contains("g15")
                                    || joined.Contains("g18")
                                    || joined.Contains("super tuk")
                                    || joined.Contains("b260")
                                    || joined.Contains("a_20")
                                    || joined.Contains("a20")
                                    || joined.Contains("a10")
                                    || joined.Contains("warthog")
                                    || joined.Contains("nara");

            if (isFixedWing && !isCommercial && !isHelicopter && isCombatAircraft)
            {
                return "aircraft:" + data.GetStableId();
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
                return QueueProduceBest(priority, cooldown, "f200", "fragata", "wall", "navio wall", "uss arrowhead", "arrowhead", "corveta sam", "destroy", "destroyer", "vindicator", "lancha", "ww", "corveta");
            }

            if (navalCount == 2)
            {
                return QueueProduceBest(priority, cooldown, "f200", "uss ironclad", "ironclad", "uss sovereign", "sovereign", "vindicator", "destroy", "destroyer", "dominion", "liberty");
            }

            if (navalCount < 15)
            {
                return QueueProduceBest(priority, cooldown, "f200", "uss sovereign", "sovereign", "uss ironclad", "ironclad", "dominion", "liberty", "vindicator", "destroy", "destroyer", "porta");
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
                bool hasMilitaryAirport = snapshot.HasMilitaryAirport;
                bool hasNavalBase = snapshot.HasNavalBase || HasImmediateNavalBase();

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
                    if (fighterCount >= 2)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.BuildShipyard, "dois cacas prontos; liberando construcao obrigatoria do estaleiro");
                        return true;
                    }

                    if (!hasMilitaryAirport)
                    {
                        brain.ReportBootstrapError("avioes: aeroporto militar indisponivel");
                        return true;
                    }

                    if (fighterCount < 2 && QueuePreferredAircraft(996, 2.6f))
                    {
                        return true;
                    }

                    brain.ReportBootstrapError("avioes: aguardando catalogo, fila ou vaga do aeroporto");
                    return true;

                case IA_BrainMaster.IA_BootstrapStage.ProduceOilTanker:
                    brain.SetBootstrapStatus("bootstrap: produzindo navio petroleiro");
                    if (!hasNavalBase)
                    {
                        brain.ReportBootstrapError("petroleiro: estaleiro/pier indisponivel");
                        return true;
                    }

                    if (snapshot.OilTankers >= 1)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.ProduceShip, "petroleiro pronto; abrindo fase de frota");
                        return true;
                    }

                    if (QueueOilTanker(996, 5f))
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
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.InvasionPrep, "iniciando preparacao de transporte");
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.InvasionPrep:
                    brain.SetBootstrapStatus("bootstrap: preparando transporte naval para ataque");
                    if (!hasNavalBase)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.MobilizeBase, "sem base naval; pulando prep de invasao");
                        return true;
                    }

                    if (snapshot.NavalUnits >= 2 && snapshot.InfantryUnits >= 8 && snapshot.NavalTransports >= 1)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.MobilizeBase, "prep concluida; mobilizando base");
                        return true;
                    }

                    if (snapshot.NavalTransports < 1 && QueueTransportLift(995, 7f))
                    {
                        return true;
                    }

                    if (snapshot.NavalUnits < 2 && QueueSurfaceFleetStep(snapshot.NavalUnits, 994, 6f))
                    {
                        return true;
                    }

                    if (snapshot.InfantryUnits < 8 && QueueProduceBest(993, 3f, "soldado rifle", "soldado", "infantaria"))
                    {
                        return true;
                    }

                    return true;

                case IA_BrainMaster.IA_BootstrapStage.MobilizeBase:
                    return TickMobilizationProduction(now, brain, snapshot, hasBarracks, hasFactory, hasMilitaryAirport, hasNavalBase);

                default:
                    return true;
            }
        }

        private bool TickMobilizationProduction(
            float now,
            IA_BrainMaster brain,
            IA_ForceSnapshot snapshot,
            bool hasBarracks,
            bool hasFactory,
            bool hasMilitaryAirport,
            bool hasNavalBase)
        {
            float elapsed = brain.GetBootstrapElapsed(now);
            float remaining = Mathf.Max(0f, brain.GetBootstrapMobilizationSeconds() - elapsed);
            if (remaining <= 0f)
            {
                brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.Completed, "mobilizacao concluida; IA liberada para jogar");
                return true;
            }

            int infantryTarget = Mathf.Clamp(10 + Mathf.FloorToInt(elapsed / 30f) * 3, 10, 32);
            int tankTarget = hasFactory ? Mathf.Clamp(3 + Mathf.FloorToInt(elapsed / 60f) * 2, 3, 10) : 0;
            int artyTarget = hasFactory ? Mathf.Clamp(1 + Mathf.FloorToInt(elapsed / 90f), 1, 4) : 0;
            int fighterTarget = hasMilitaryAirport ? Mathf.Clamp(4 + Mathf.FloorToInt(elapsed / 45f), 4, 12) : 0;
            int navalTarget = hasNavalBase ? Mathf.Clamp(1 + Mathf.FloorToInt(elapsed / 90f), 1, 4) : 0;

            brain.SetBootstrapStatus(
                "mobilizacao defensiva "
                + Mathf.CeilToInt(remaining) + "s | alvo T"
                + infantryTarget + "/B" + tankTarget + "/A" + artyTarget + "/Av" + fighterTarget + "/N" + navalTarget);

            if (hasBarracks
                && snapshot.InfantryUnits < infantryTarget
                && QueueProduceBest(999, 3.0f, "tropa navy", "soldado rifle", "soldado", "infantaria", "rifle"))
            {
                return true;
            }

            if (hasFactory
                && snapshot.TankUnits < tankTarget
                && QueueProduceBest(998, 4.5f, "tank mbt", "mbt", "tank south", "tank c1", "tank arthur"))
            {
                return true;
            }

            if (hasMilitaryAirport
                && snapshot.FixedWingAircraft < fighterTarget
                && QueuePreferredAircraft(997, 3.0f))
            {
                return true;
            }

            if (hasFactory
                && snapshot.ArtilleryUnits < artyTarget
                && QueueProduceBest(996, 6.5f, "hack", "artilharia", "lancador"))
            {
                return true;
            }

            if (hasNavalBase
                && snapshot.NavalUnits < navalTarget
                && QueueSurfaceFleetStep(snapshot.NavalUnits, 995, 8f))
            {
                return true;
            }

            return true;
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

        private IA_BattleGovernorDecision GetBattleDecision()
        {
            if (_context != null && _context.BattleDecision != null)
            {
                return _context.BattleDecision;
            }

            return new IA_BattleGovernorDecision();
        }

        private Vector3 ResolveBaseCenter()
        {
            if (_context != null && _context.WorldState != null)
            {
                Vector3 baseCenter = _context.WorldState.BaseCenter;
                if (baseCenter != Vector3.zero)
                {
                    return baseCenter;
                }
            }

            if (_context != null && _context.Brain != null)
            {
                return _context.Brain.transform.position;
            }

            return Vector3.zero;
        }

        private IA_TransportPlan BuildTransportPlan(
            float now,
            IA_ForceSnapshot snapshot,
            Vector3 baseCenter,
            int infantryCount,
            int tankCount,
            int artyCount,
            int helicopterCount,
            int fighterCount,
            int fleetCombatCount)
        {
            IA_TransportPlan plan = _context != null && _context.TransportPlan != null
                ? _context.TransportPlan
                : new IA_TransportPlan();

            plan.LastUpdatedTime = now;
            plan.TargetAnchor = Vector3.zero;
            plan.HasLandRoute = true;
            plan.RequiredCapacity = 0;
            plan.AvailableCapacity = CountStrategicTransportCapacity();
            plan.EscortReady = fleetCombatCount >= 2;
            plan.AirCoverReady = (helicopterCount + fighterCount) >= 2;

            if (_context == null || _context.WorldState == null || _context.MapAnalyzer == null || baseCenter == Vector3.zero)
            {
                return plan;
            }

            Vector3 enemyAnchor;
            if (!_context.WorldState.TryGetEnemyStrategicAnchor(baseCenter, out enemyAnchor))
            {
                return plan;
            }

            plan.TargetAnchor = enemyAnchor;
            plan.HasLandRoute = _context.MapAnalyzer.HasLandRouteCached(baseCenter, enemyAnchor, 12f);
            if (plan.HasLandRoute)
            {
                plan.EscortReady = true;
                plan.AirCoverReady = true;
                return plan;
            }

            int offensiveGroundStrength = Mathf.Max(0, infantryCount + (tankCount * 2) + (artyCount * 2));
            if (offensiveGroundStrength <= 0)
            {
                return plan;
            }

            int desiredAssaultCapacity = offensiveGroundStrength < 6
                ? offensiveGroundStrength
                : Mathf.Clamp(8 + Mathf.Clamp((offensiveGroundStrength - 6) / 4, 0, 6), 8, 14);
            plan.RequiredCapacity = desiredAssaultCapacity;

            int transportLiftUnits = CountStrategicTransportUnits();
            int requiredEscorts = transportLiftUnits > 0
                ? Mathf.Clamp(Mathf.CeilToInt(transportLiftUnits * 0.8f), 1, 3)
                : 1;

            plan.EscortReady = fleetCombatCount >= requiredEscorts;
            plan.AirCoverReady = (helicopterCount + fighterCount) >= (transportLiftUnits > 0 ? 1 : 0);
            return plan;
        }

        private int CountStrategicTransportCapacity()
        {
            if (_context == null || _context.WorldState == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(unit.name);
                if (!IA_BattleGovernorUtils.IsNavalTransport(unit, normalized)
                    && !IA_BattleGovernorUtils.IsHoverTransport(unit, normalized)
                    && !IsStrategicAirLift(unit, normalized))
                {
                    continue;
                }

                total += IA_BattleGovernorUtils.EstimateTransportCapacity(unit);
            }

            return total;
        }

        private int CountStrategicTransportUnits()
        {
            if (_context == null || _context.WorldState == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < _context.WorldState.OwnUnits.Count; i++)
            {
                GameObject unit = _context.WorldState.OwnUnits[i];
                if (unit == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(unit.name);
                if (IA_BattleGovernorUtils.IsNavalTransport(unit, normalized)
                    || IA_BattleGovernorUtils.IsHoverTransport(unit, normalized)
                    || IsStrategicAirLift(unit, normalized))
                {
                    total++;
                }
            }

            return total;
        }

        private static bool IsStrategicAirLift(GameObject unit, string normalized)
        {
            if (!IA_BattleGovernorUtils.IsAirTransport(unit, normalized))
            {
                return false;
            }

            return normalized.Contains("transport")
                   || normalized.Contains("transporte")
                   || normalized.Contains("cargo")
                   || normalized.Contains("vans");
        }

        private bool QueueTransportLift(int priority, float cooldown)
        {
            if (QueueProduceBest(priority, cooldown, "uss liberty prime", "liberty", "barco ww transporte", "navio transporte", "transporte anfibio"))
            {
                return true;
            }

            if (CountUnits("hover", "houver", "hovercraft") >= 2)
            {
                return false;
            }

            return QueueProduceBest(priority, cooldown + 1.5f, "hover", "houver", "hovercraft");
        }

        private bool ShouldPauseOffensiveGroundMass(
            IA_TransportPlan transportPlan,
            int infantryCount,
            int tankCount,
            int artyCount,
            IA_BattleGovernorDecision decision)
        {
            int offensiveGroundStrength = Mathf.Max(0, infantryCount + (tankCount * 2) + (artyCount * 2));
            if (decision != null
                && decision.Band == IA_PerformanceGovernorBand.Critico
                && offensiveGroundStrength >= 8)
            {
                return true;
            }

            if (transportPlan == null || transportPlan.HasLandRoute || transportPlan.Ready)
            {
                return false;
            }

            return offensiveGroundStrength >= Mathf.Max(6, transportPlan.AvailableCapacity + 2);
        }

        private bool ShouldHoldForRuntimePressure(float now)
        {
            if (!ShouldRespectRuntimeLock())
            {
                return false;
            }

            IA_BattleGovernorDecision decision = GetBattleDecision();
            if (decision != null
                && decision.ProductionCooldownSeconds > 0f
                && now < _nextRuntimeProductionQueueTime)
            {
                return true;
            }

            return DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                   && now < _nextRuntimeProductionQueueTime;
        }

        private void ArmRuntimeQueueCooldown()
        {
            if (!ShouldRespectRuntimeLock())
            {
                return;
            }

            float now = Time.time;
            float cooldown = DiagnosticoDesempenhoJogo.RuntimeSaturado()
                ? 4f
                : (DiagnosticoDesempenhoJogo.RuntimeSobPressao() ? 2f : 0f);
            IA_BattleGovernorDecision decision = GetBattleDecision();
            if (decision != null && decision.ProductionCooldownSeconds > 0f)
            {
                cooldown = Mathf.Max(cooldown, decision.ProductionCooldownSeconds);
            }

            if (cooldown > 0f)
            {
                _nextRuntimeProductionQueueTime = Mathf.Max(_nextRuntimeProductionQueueTime, now + cooldown);
            }
        }

        private static bool ShouldRespectRuntimeLock()
        {
            return Application.isPlaying && Time.timeSinceLevelLoad >= 20f;
        }

        private static void RegistrarTempoProducao(long profileStart)
        {
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - profileStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMs > 0f)
            {
                DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("production_ms", elapsedMs);
            }
        }

        private bool HasImmediateNavalBase()
        {
            RegistroEntidadesJogo.FillEstaleiros(_registeredShipyardBuffer);
            for (int i = 0; i < _registeredShipyardBuffer.Count; i++)
            {
                Estaleiro estaleiro = _registeredShipyardBuffer[i];
                if (estaleiro != null && _context.Backend.BelongsToTeam(estaleiro))
                {
                    return true;
                }
            }

            RegistroEntidadesJogo.FillPiers(_registeredPierBuffer);
            for (int i = 0; i < _registeredPierBuffer.Count; i++)
            {
                PierMarinha pier = _registeredPierBuffer[i];
                if (pier != null && _context.Backend.BelongsToTeam(pier))
                {
                    return true;
                }
            }

            return false;
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
                if ((unit.GetComponent<ControleAviao>() != null
                    || unit.GetComponent<ControleAviaoCaca>() != null
                    || name.Contains("fa1")
                    || name.Contains("caca")
                    || name.Contains("jet")
                    || name.Contains("a_20")
                    || name.Contains("g_18m")
                    || name.Contains("g18m")
                    || name.Contains("b260")
                    || name.Contains("b-260")
                    || name.Contains("supra")
                    || name.Contains("su11")
                    || name.Contains("g15")
                    || name.Contains("super tuk")
                    || name.Contains("supertuk"))
                    && unit.GetComponent<ControleAviaoComercial>() == null)
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
