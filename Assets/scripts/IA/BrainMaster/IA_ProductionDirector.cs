using System.Collections.Generic;
using Hegemonia.AI.IA01;
using Hegemonia.AI.IA03;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ProductionDirector : IIAUpdateModule
    {
        private const int MinimumTankTargetPerCountry = 4;
        private const int MaxBombersPerAirport = 1;
        private const bool EnableHelicopterProduction = true;
        // A força pode crescer com a capacidade do país, mas nunca apenas
        // porque o relógio avançou. Estes são os pisos de uma nação média;
        // a capacidade adicional vem de economia, indústria, território e
        // infraestrutura militar.
        private const int BaseAircraftCapacity = 10;
        private const int BaseNavalCapacity = 10;
        private const float CapacityRefreshInterval = 2.5f;
        private const float TimedAirKickoffSeconds = 12f;
        private const float TimedNavalKickoffSeconds = 15f;
        private const float StructureStabilitySeconds = 3.5f;
        private const int TimedAirKickoffTarget = 1;
        private const int TimedNavalMinArmy = 0;
        private readonly IA_Context _context;
        private float _nextDecisionTime;
        private int _lastKnownStructureCount = -1;
        private float _lastStructureChangeTime;
        private bool _timedAirKickoffTriggered;
        private bool _timedNavalKickoffTriggered;
        private string _lastPreferredAircraftVariant = string.Empty;
        private string _lastQueuedNavalItem = string.Empty;
        private int _groundUnitRotationIndex;
        private int _infantryRotationIndex;
        private int _navalUnitRotationIndex;
        private IA_BrainMaster.IA_BootstrapStage _lastBootstrapStage = IA_BrainMaster.IA_BootstrapStage.Disabled;
        private int _bootstrapShipGoalCount = -1;
        private float _nextRuntimeProductionQueueTime;
        private float _nextTransportPlanRefreshTime;
        private float _nextCapacityRefreshTime;
        private float _nextValdoriaAirportBlockLogTime;
        private float _nextGroundFactoryBlockLogTime;
        private int _cachedAircraftCapacity = BaseAircraftCapacity;
        private int _cachedNavalCapacity = BaseNavalCapacity;
        private readonly List<Estaleiro> _registeredShipyardBuffer = new List<Estaleiro>();
        private readonly List<PierMarinha> _registeredPierBuffer = new List<PierMarinha>();
        private readonly List<MarcadorTerritorio> _territoryMarkerBuffer = new List<MarcadorTerritorio>(16);

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
            get { return 1.85f; }
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
                // A IA01 e a autoridade de infraestrutura do time. Nenhuma etapa
                // de bootstrap militar pode consumir a fila antes de a cadeia
                // fundacional estar pronta.
                IA_ForceSnapshot foundationSnapshot = GetSnapshot();
                bool hasAnyMilitaryProductionFacility = foundationSnapshot.HasBarracks
                    || foundationSnapshot.HasFactory
                    || foundationSnapshot.HasMilitaryAirport
                    || foundationSnapshot.HasHeliport
                    || foundationSnapshot.ShipyardCount > 0
                    || foundationSnapshot.PierCount > 0;
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica(
                    "ia_any_military_production_facility_ready",
                    hasAnyMilitaryProductionFacility ? 1 : 0);
                if (!hasAnyMilitaryProductionFacility)
                {
                    return;
                }

                if (brain != null && brain.ContarUnidadesTotaisDoTime(brain.TeamId) >= brain.LimiteMaximoUnidades)
                {
                    return;
                }

                // A IA03 começa com uma força pequena, utilizável e barata.
                // Os navios mais caros continuam sujeitos às metas dinâmicas
                // do BrainMaster, que já consideram ameaça, renda e indústria.
                if (TryQueueIA03OpeningAssets(foundationSnapshot))
                {
                    return;
                }

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

                bool foundationInfrastructureStillReady = hasMilitaryAirport
                    && snapshot.HasCommercialAirport
                    && snapshot.ShipyardCount > 0
                    && hasFactory;
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("ia_foundation_infrastructure_ready", foundationInfrastructureStillReady ? 1 : 0);
                // As ordens abaixo verificam a instalação necessária para
                // cada unidade. Não bloqueia caças pelo estaleiro, navios pelo
                // aeroporto comercial nem todas as forças por uma fábrica.

                Vector3 baseCenter = ResolveBaseCenter();
                IA_TransportPlan transportPlan = _context.TransportPlan;
                bool transportPlanExpired = transportPlan == null
                    || now >= _nextTransportPlanRefreshTime
                    || snapshot.LastUpdatedTime > transportPlan.LastUpdatedTime + 0.01f;
                if (transportPlanExpired)
                {
                    transportPlan = BuildTransportPlan(
                        now,
                        snapshot,
                        baseCenter,
                        infantryCount,
                        tankCount,
                        artyCount,
                        helicopterCount,
                        fighterCount,
                        fleetCombatCount);
                    _nextTransportPlanRefreshTime = now + ResolveStrategicCacheInterval();
                }
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
                ResolveProductionCapacities(now, snapshot, out int aircraftCapacity, out int navalCapacity);

                int infantryTarget = 6 + (counter.AntiRush ? 4 : 0) + Mathf.RoundToInt(counter.LandWeight * 4f);
                int tankTarget = 3 + Mathf.RoundToInt(counter.LandWeight * 4f);
                int artyTarget = 1 + (counter.ReinforceCenter ? 1 : 0);
                int helicopterTarget = EnableHelicopterProduction && hasHeliport
                    ? Mathf.Clamp(1 + Mathf.RoundToInt(counter.AirWeight * 3f), 1, 4)
                    : 0;
                int fighterBaseline = 2 + Mathf.RoundToInt(counter.AirWeight * 3f);
                int fighterTarget = hasMilitaryAirport ? Mathf.Max(2, fighterBaseline) : 0;
                int patrolShipTarget = hasNavalBase ? 1 : 0;
                int navalBaseline = 1 + Mathf.RoundToInt(counter.NavalWeight * 4f);
                int navalTarget = hasNavalBase ? Mathf.Max(1, navalBaseline) : 0;
                int oilTankerTarget = hasNavalBase ? 1 : 0;
                if (brain != null)
                {
                    fighterTarget = hasMilitaryAirport ? Mathf.Max(fighterTarget, brain.TargetAircraft) : 0;
                    navalTarget = hasNavalBase ? Mathf.Max(navalTarget, brain.TargetFleet) : 0;
                    oilTankerTarget = hasNavalBase
                        ? (snapshot.PlatformCount > 0 && snapshot.PierCount > 0
                            ? Mathf.Max(oilTankerTarget, brain.TargetOilTankers)
                            : 1)
                        : 0;
                        
                    if (brain.ActiveImperialPlan == "invasao_anfibia_combinada")
                    {
                        helicopterTarget = Mathf.Max(helicopterTarget, 4);
                        navalTarget = Mathf.Max(navalTarget, 6);
                        infantryTarget = Mathf.Max(infantryTarget, 20);
                        tankTarget = Mathf.Max(tankTarget, 6);
                    }
                }
                IA03EstrategaNacional ia03 = brain != null
                    ? brain.GetComponent<IA03EstrategaNacional>()
                    : null;
                if (brain != null && brain.UsaCronogramaIA03)
                {
                    infantryTarget = Mathf.Max(infantryTarget, 5);
                    tankTarget = Mathf.Max(tankTarget, 5);

                    if (!brain.IA03AereoLiberado)
                    {
                        fighterTarget = 0;
                    }

                    if (!brain.IA03EstaleiroEPetroleiroLiberado)
                    {
                        oilTankerTarget = 0;
                    }
                    else
                    {
                        oilTankerTarget = hasNavalBase
                            ? (snapshot.PlatformCount > 0 && snapshot.PierCount > 0
                                ? Mathf.Max(1, brain.IA03LimitePetroleiros)
                                : 1)
                            : 0;
                    }

                    if (!brain.IA03NavioMilitarLiberado)
                    {
                        patrolShipTarget = 0;
                        navalTarget = 0;
                    }
                    else
                    {
                        navalTarget = hasNavalBase ? Mathf.Max(2, navalTarget) : 0;
                    }
                }
                else if (ia03 != null && ia03.isActiveAndEnabled && ia03.TemPerfilConfigurado)
                {
                    // Este pacote pede um petroleiro nacional de cada vez; a
                    // meta maior da IA continua aplicável às demais classes.
                    oilTankerTarget = hasNavalBase
                        ? (snapshot.PlatformCount > 0 && snapshot.PierCount > 0 ? Mathf.Min(1, oilTankerTarget) : 1)
                        : 0;
                }
                int subTarget = (brain == null || brain.IA03NavioMilitarLiberado)
                    && hasNavalBase && fleetCombatCount >= 1 && (counter.ReinforceCoast || counter.NavalWeight > 0.18f)
                    ? 1
                    : 0;
                int truckTarget = infantryCount >= 8 ? 1 : 0;
                int hoverTarget = hasNavalBase
                    && fleetCombatCount >= 2
                    && infantryCount >= 10
                    && counter.ReinforceCoast
                    ? 1
                    : 0;
                // As decisões acima continuam respeitando a doutrina e a
                // ameaça, mas a fila não pode ultrapassar a capacidade real
                // do país. O clamp fica depois de todas as prioridades para
                // que nenhum caminho alternativo escape do limite.
                fighterTarget = hasMilitaryAirport ? Mathf.Min(fighterTarget, aircraftCapacity) : 0;
                navalTarget = hasNavalBase ? Mathf.Min(navalTarget, navalCapacity) : 0;
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("air_production_capacity", aircraftCapacity);
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("naval_production_capacity", navalCapacity);
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
                    fighterTarget = recoveryOverride ? fighterTarget : Mathf.Min(fighterTarget, hasMilitaryAirport ? 10 : 0);
                    navalTarget = recoveryOverride ? navalTarget : Mathf.Min(navalTarget, hasNavalBase ? 6 : 0);
                }

                if (mustPrepareTransport)
                {
                    infantryTarget = Mathf.Min(infantryTarget, Mathf.Max(6, transportPlan.AvailableCapacity + 2));
                    tankTarget = Mathf.Min(tankTarget, transportPlan.AvailableCapacity >= 8 ? 2 : 1);
                    artyTarget = Mathf.Min(artyTarget, transportPlan.AvailableCapacity >= 10 ? 1 : 0);
                }

                // A meta de blindados cresce gradualmente conforme o estágio da partida
                // (4 inicial -> 8 após 6 min -> 12 após 12 min), evitando criar 100 tanques de imediato.
                if (hasFactory && !mustPrepareTransport)
                {
                    int stagedTankFloor = now >= 720f ? 12 : (now >= 360f ? 8 : MinimumTankTargetPerCountry);
                    tankTarget = Mathf.Clamp(Mathf.Max(tankTarget, stagedTankFloor), 2, 16);
                }
                infantryTarget = Mathf.Clamp(infantryTarget, 4, now >= 600f ? 18 : 10);
                if (brain != null)
                {
                    tankTarget = Mathf.Min(tankTarget, brain.LimiteMaximoTanques);
                    infantryTarget = Mathf.Min(infantryTarget, brain.LimiteMaximoUnidades);
                    fighterTarget = Mathf.Min(fighterTarget, brain.LimiteMaximoUnidades);
                    navalTarget = Mathf.Min(navalTarget, brain.LimiteMaximoUnidades);
                    helicopterTarget = Mathf.Min(helicopterTarget, brain.LimiteMaximoUnidades);
                }
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("tank_target", tankTarget);

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

                bool oilTankerAvailable = HasOilTankerCatalog();
                bool canQueueAdditionalTanker = snapshot.PlatformCount > 0 && snapshot.PierCount > 0;
                bool queuedFirstOilTankerThisTick = hasNavalBase
                                                    && oilTankerTarget > 0
                                                    && (oilTankerCount < 1 || canQueueAdditionalTanker)
                                                    && oilTankerCount < oilTankerTarget
                                                    && QueueOilTanker(99, 5.0f);

                if (queuedFirstOilTankerThisTick)
                {
                    return;
                }

                bool awaitingFirstOilTanker = hasNavalBase
                                              && oilTankerTarget > 0
                                              && oilTankerCount < 1
                                              && oilTankerAvailable;

                if (!_timedNavalKickoffTriggered
                    && !awaitingFirstOilTanker
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
                    if (hasNavalBase && !awaitingFirstOilTanker && (!transportPlan.EscortReady || navalCount < 4) && QueueSurfaceFleetStep(navalCount, 97, 4.5f))
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
                        && !awaitingFirstOilTanker
                        && transportPlan.AvailableCapacity < transportPlan.RequiredCapacity
                        && QueueTransportLift(94, 10f))
                    {
                        return;
                    }
                }

                if (hasNavalBase && !awaitingFirstOilTanker && navalCount < 1 && QueueSurfaceFleetStep(navalCount, 93, 5.5f))
                {
                    return;
                }

                // Avioes tem prioridade quando a frota aerea esta abaixo do minimo
                if (hasMilitaryAirport && fighterCount < 4 && QueuePreferredAircraft(95, 5.5f))
                {
                    return;
                }

                if (hasBarracks && infantryCount < 4 && QueueVariedInfantry(94, 5.5f))
                {
                    return;
                }

                if (hasNavalBase && !awaitingFirstOilTanker && navalCount < 2 && QueueSurfaceFleetStep(navalCount, 91, 6.0f))
                {
                    return;
                }

                // Se não temos base naval ainda, não pode travar o resto da produção:
                // a preparação de invasão depende do BuildDirector criar a infraestrutura primeiro.
                if (mustPrepareTransport && hasNavalBase)
                {
                    return;
                }

                // Avalia cada dominio produtivo de forma independente (naval, aereo e terrestre)
                // para evitar que infanteria/tanques/cacas impecam eternamente o Estaleiro de produzir navios.
                if (hasNavalBase)
                {
                    if ((oilTankerCount < 1 || (snapshot.PlatformCount > 0 && snapshot.PierCount > 0))
                        && oilTankerCount < oilTankerTarget
                        && QueueOilTanker(89, 8.0f))
                    {
                    }
                    else if (!awaitingFirstOilTanker && navalCount < patrolShipTarget && QueueSurfaceFleetStep(navalCount, 85, 5.0f))
                    {
                    }
                    else if (!awaitingFirstOilTanker && !suppressExpansion && navalCount < navalTarget && QueueSurfaceFleetStep(navalCount, 80, 5.0f))
                    {
                    }
                    else if (!awaitingFirstOilTanker && submarineCount < subTarget && QueueProduceBest(84, 14f, "uss mako", "submarino", "uss wraith", "uss leviathan"))
                    {
                    }
                    else if (!awaitingFirstOilTanker
                             && transportPlan != null
                             && transportPlan.AvailableCapacity < transportPlan.RequiredCapacity
                             && QueueTransportLift(76, 12f))
                    {
                    }
                }

                // Mantem o aeroporto ativo sem bloquear o restante da producao
                if (hasMilitaryAirport && fighterCount < 6)
                {
                    QueuePreferredAircraft(93, 6.5f);
                }
                else if (EnableHelicopterProduction
                         && hasHeliport
                         && helicopterCount < helicopterTarget
                         && QueueProduceBest(83, 3.2f, "vans", "helicoptero de combate", "helicoptero ray", "ray", "helicoptero"))
                {
                }
                else if (hasMilitaryAirport && fighterCount < fighterTarget && airCount < helicopterTarget + fighterTarget)
                {
                    QueuePreferredAircraft(89, 2.1f);
                }

                if (shouldPauseGroundMass)
                {
                    if (truckCount < truckTarget && hasFactory)
                    {
                        QueueProduceBest(80, 12f, "caminhao de transporte", "transporte", "truck");
                    }

                    return;
                }

                if (hasBarracks && infantryCount < infantryTarget && QueueVariedInfantry(90, 6.0f))
                {
                    return;
                }

                if (hasFactory && tankCount < tankTarget && QueueVariedGroundVehicle(86, 6.5f))
                {
                    return;
                }

                if (hasFactory && artyCount < artyTarget && QueueProduceBest(82, 8.5f, "hack", "artilharia", "lancador"))
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

        private bool TryQueueIA03OpeningAssets(IA_ForceSnapshot snapshot)
        {
            IA_BrainMaster brain = _context != null ? _context.Brain : null;
            IA03EstrategaNacional estratega = brain != null
                ? brain.GetComponent<IA03EstrategaNacional>()
                : null;
            if (snapshot == null
                || estratega == null
                || !estratega.isActiveAndEnabled
                || !estratega.TemPerfilConfigurado)
            {
                return false;
            }

            // Uma aeronave inicial permite à IA03 patrulhar e cumprir as
            // missões aéreas que já estão configuradas para o país.
            if (brain.IA03AereoLiberado
                && snapshot.HasMilitaryAirport
                && snapshot.FixedWingAircraft < 1
                && QueuePreferredAircraft(1000, 3.0f))
            {
                return true;
            }

            if (!brain.IA03EstaleiroEPetroleiroLiberado)
            {
                return false;
            }

            // Só publique compras navais depois de haver um estaleiro próprio;
            // antes disso o serviço de produção as recusaria e queimaria a
            // janela de tentativa da IA. O petroleiro DEVE ser o primeiro navio
            // produzido assim que o estaleiro existir, antes de navios militares e cargueiros.
            bool hasNavalBase = (snapshot.HasNavalBase || HasImmediateNavalBase())
                                && snapshot.ShipyardCount > 0;
            int targetTankers = brain.UsaCronogramaIA03 ? Mathf.Max(1, brain.IA03LimitePetroleiros) : 1;
            bool oilTankerAvailable = HasOilTankerCatalog();
            if (hasNavalBase && targetTankers > 0 && snapshot.OilTankers < 1 && oilTankerAvailable)
            {
                return QueueOilTanker(999, 5.0f);
            }

            if (hasNavalBase
                && snapshot.PierCount > 0
                && snapshot.PlatformCount > 0
                && snapshot.OilTankers < targetTankers
                && QueueOilTanker(999, 5.0f))
            {
                return true;
            }

            if (brain.IA03NavioMilitarLiberado
                && hasNavalBase
                && snapshot.NavalUnits < 2
                && QueueSurfaceFleetStep(snapshot.NavalUnits, 998, 3.5f))
            {
                return true;
            }

            return snapshot.ShipyardCount > 0
                   && snapshot.PierCount > 0
                   && snapshot.CargoShips < 1
                   && QueueCargoShip(997, 8.0f);
        }

        private bool HasOilTankerCatalog()
        {
            if (_context == null || _context.Backend == null)
            {
                return false;
            }

            DadosConstrucao unused;
            return _context.Backend.TryResolveItem("petroleiro", out unused)
                   || _context.Backend.TryResolveItem("navio petroleiro", out unused)
                   || _context.Backend.TryResolveItem("navio petrolifero", out unused)
                   || _context.Backend.TryResolveItem("oil tanker", out unused)
                   || _context.Backend.TryResolveItem("tanker", out unused);
        }

        private bool QueueCargoShip(int priority, float cooldown)
        {
            if (_context == null || _context.Brain == null || _context.Backend == null)
            {
                return false;
            }

            DadosConstrucao data = _context.Backend.FindFirstAvailable(
                "navio_carga_mercado",
                "navio de carga",
                "navio cargueiro",
                "cargueiro");
            if (data == null)
            {
                IA_RuntimeTextTrace.LogText(_context.Brain.TeamId, "IA_ProductionDirector", "CARGO_PROD_NO_CANDIDATE", "ficha navio_carga_mercado indisponível");
                return false;
            }

            if (!_context.Brain.PodeComprarUnidadeMilitar(data, out _))
            {
                return false;
            }

            IA_ForceSnapshot snapshot = GetSnapshot();
            int teamId = _context.Brain.TeamId;
            if (!IAAutoProductionRegistry.TryReserveProduction(
                    teamId,
                    "CargoShip",
                    "ia03-logistics",
                    1,
                    snapshot.CargoShips,
                    out string orderId,
                    Time.time,
                    180f))
            {
                return false;
            }

            IA_ProduceOrderData payload = new IA_ProduceOrderData
            {
                ItemKey = data.NomeItem,
                Quantity = 1,
                ProductionOrderId = orderId
            };
            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Produce,
                "IA_ProductionDirector",
                "production",
                "cargueiro logístico IA03",
                priority,
                "production",
                "produce:" + IA_Text.Normalize(data.NomeItem),
                cooldown,
                payload);

            string reason;
            bool enqueued = PublishProductionIntent(request, data, priority, "cargueiro logístico IA03", out reason);
            if (enqueued)
            {
                IAAutoProductionRegistry.ConfirmQueued(orderId, 0, Time.time);
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.NomeItem, "IA03_Logistica");
                IA_RuntimeTextTrace.LogText(teamId, "IA_ProductionDirector", "CARGO_PROD_OK", "item=" + data.NomeItem + " | meta=1 cargueiro próprio");
                ArmRuntimeQueueCooldown();
            }
            else
            {
                IAAutoProductionRegistry.Release(orderId, Time.time);
                IA_RuntimeTextTrace.LogText(teamId, "IA_ProductionDirector", "CARGO_PROD_FAIL", "item=" + data.NomeItem + (string.IsNullOrEmpty(reason) ? string.Empty : " | motivo=" + reason));
            }

            return enqueued;
        }

        private bool QueueProduceBest(int priority, float cooldown, params string[] keys)
        {
            DadosConstrucao data = _context.Backend.FindFirstAvailable(keys);
            if (data == null)
            {
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "PROD_NO_CANDIDATE", "sem item para chaves=" + string.Join(",", keys));
                return false;
            }

            if (_context != null && _context.Brain != null && !_context.Brain.PodeComprarUnidadeMilitar(data, out _))
            {
                return false;
            }

            if (_context != null && _context.Backend != null && _context.Backend.ProductionService != null)
            {
                string factoryBlockReason;
                if (!_context.Backend.ProductionService.CanProduceGroundUnitAtFactory(data, out factoryBlockReason))
                {
                    if (Time.time >= _nextGroundFactoryBlockLogTime)
                    {
                        _nextGroundFactoryBlockLogTime = Time.time + 15f;
                        IA_RuntimeTextTrace.LogText(
                            _context.Brain != null ? _context.Brain.TeamId : -1,
                            "IA_ProductionDirector",
                            "PROD_FACTORY_HOLD",
                            "item=" + data.NomeItem + " | motivo=" + factoryBlockReason);
                    }

                    return false;
                }
            }

            IA01MilitaryAssetKind assetKind = IA01MilitaryProductionGuard.Classify(data);
            string orderId = string.Empty;
            bool reserved = assetKind == IA01MilitaryAssetKind.Other
                || IA01MilitaryProductionGuard.TryReserveSingle(
                    _context.Brain != null ? _context.Brain.TeamId : 0,
                    assetKind,
                    CurrentCountFor(assetKind, GetSnapshot()),
                    Time.time,
                    assetKind == IA01MilitaryAssetKind.Naval || assetKind == IA01MilitaryAssetKind.OilTanker ? 180f : 45f,
                    out orderId);
            if (!reserved)
            {
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "PROD_GUARD", "ordem ja pendente para " + assetKind);
                return false;
            }

            IA_ProduceOrderData payload = new IA_ProduceOrderData
            {
                ItemKey = data.NomeItem,
                Quantity = 1,
                ProductionOrderId = orderId
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Produce,
                "IA_ProductionDirector",
                "production",
                "fila de producao otimizada",
                priority,
                "production",
                "produce:" + IA_Text.Normalize(data.NomeItem),
                cooldown,
                payload);

            string reason;
            bool enqueued = PublishProductionIntent(request, data, priority, "fila de producao otimizada", out reason);
            if (enqueued)
            {
                IA01MilitaryProductionGuard.ConfirmQueued(orderId, 0, Time.time);
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.NomeItem);
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "PROD_OK", "item=" + data.NomeItem + " | motivo=fila de producao otimizada");
                ArmRuntimeQueueCooldown();
            }
            else
            {
                if (assetKind != IA01MilitaryAssetKind.Other)
                    IA01MilitaryProductionGuard.Cancel(_context.Brain != null ? _context.Brain.TeamId : 0, assetKind, Time.time);
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "PROD_FAIL", "item=" + data.NomeItem + (string.IsNullOrEmpty(reason) ? string.Empty : " | motivo=" + reason));
            }

            return enqueued;
        }

        private bool QueueOilTanker(int priority, float cooldown)
        {
            return QueueProduceBest(priority, cooldown, "petroleiro", "navio petroleiro", "navio petrolifero", "oil tanker", "tanker");
        }

        private bool QueueVariedInfantry(int priority, float cooldown)
        {
            int slot = Mathf.Abs(_infantryRotationIndex++) % 3;
            switch (slot)
            {
                case 0:
                    return QueueProduceBest(priority, cooldown, "soldado rifle", "tropa navy", "soldado", "infantaria", "rifle");
                case 1:
                    return QueueProduceBest(priority, cooldown, "tropa navy", "soldado bazuca", "soldado rifle", "infantaria");
                default:
                    return QueueProduceBest(priority, cooldown, "soldado", "soldado rifle", "tropa navy", "infantaria", "rifle");
            }
        }

        private bool QueueVariedGroundVehicle(int priority, float cooldown)
        {
            int slot = Mathf.Abs(_groundUnitRotationIndex++) % 5;
            switch (slot)
            {
                case 0:
                    return QueueProduceBest(priority, cooldown, "tank arthur", "arthur", "tank c1", "tank south", "tank mbt", "mbt");
                case 1:
                    return QueueProduceBest(priority, cooldown, "tank c1", "c1", "tank south", "tank arthur", "tank mbt", "mbt");
                case 2:
                    return QueueProduceBest(priority, cooldown, "tank south", "south", "tank leonc", "leonc", "tank mbt", "tank arthur");
                case 3:
                    return QueueProduceBest(priority, cooldown, "tank mbt", "mbt", "king", "leon", "tank arthur", "tank c1");
                default:
                    return QueueProduceBest(priority, cooldown, "leonc", "blindado", "apc", "tank south", "tank c1", "tank arthur", "mbt");
            }
        }

        private int CountExistingBombers()
        {
            if (_context == null || _context.WorldState == null || _context.WorldState.OwnCombatUnits == null)
            {
                return 0;
            }

            int bombers = 0;
            List<GameObject> units = _context.WorldState.OwnCombatUnits;
            for (int i = 0; i < units.Count; i++)
            {
                GameObject unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                string name = IA_Text.Normalize(unit.name);
                if (name.Contains("b260") || name.Contains("b-260") || name.Contains("b 260") || name.Contains("bombardeiro"))
                {
                    bombers++;
                }
            }

            return bombers;
        }

        private bool QueuePreferredAircraft(int priority, float cooldown)
        {
            if (_context != null && _context.Brain != null && _context.Brain.TeamId == 2)
            {
                string airportBlockReason;
                if (!HasOperationalValdoriaAirport(out airportBlockReason))
                {
                    if (Time.time >= _nextValdoriaAirportBlockLogTime)
                    {
                        _nextValdoriaAirportBlockLogTime = Time.time + 20f;
                        string message = "[IA03][Valdoria] compra aérea aguardando: " + airportBlockReason;
                        Debug.LogWarning(message, _context.Brain);
                        IA_RuntimeTextTrace.LogText(2, "IA_ProductionDirector", "AIR_PROD_BLOCKED", airportBlockReason);
                    }

                    return false;
                }
            }

            DadosConstrucao data = ChoosePreferredAircraftVariant();
            if (data == null)
            {
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "AIR_PROD_VARIANT_FALLBACK", "variante preferida nao encontrada");
                return QueueProduceBest(priority, cooldown, "fa1", "g_18m", "g18m", "su11", "supra", "a_20", "a10", "warthog", "g15", "super tuk", "caca", "fighter", "b260");
            }

            if (_context != null && _context.Brain != null && !_context.Brain.PodeComprarUnidadeMilitar(data, out _))
            {
                return false;
            }

            int teamId = _context.Brain != null ? _context.Brain.TeamId : 0;
            string orderId;
            if (!IA01MilitaryProductionGuard.TryReserveSingle(teamId, IA01MilitaryAssetKind.Fighter, GetSnapshot().FixedWingAircraft, Time.time, 120f, out orderId))
            {
                IA_RuntimeTextTrace.LogText(teamId, "IA_ProductionDirector", "AIR_PROD_GUARD", "caca ja pendente");
                return false;
            }

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Produce,
                "IA_ProductionDirector",
                "production",
                "preferencia aerea",
                priority,
                "production",
                "produce:" + IA_Text.Normalize(data.NomeItem),
                cooldown,
                new IA_ProduceOrderData
                {
                    ItemKey = data.NomeItem,
                    Quantity = 1,
                    ProductionOrderId = orderId
                });

            string reason;
            bool enqueued = PublishProductionIntent(request, data, priority, "preferencia aerea", out reason);
            if (enqueued)
            {
                IA01MilitaryProductionGuard.ConfirmQueued(orderId, 0, Time.time);
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.NomeItem, "IA_Prod_Air");
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "AIR_PROD_OK", "item=" + data.NomeItem + " | motivo=preferencia aerea");
                ArmRuntimeQueueCooldown();
            }
            else
            {
                IA01MilitaryProductionGuard.Cancel(teamId, IA01MilitaryAssetKind.Fighter, Time.time);
                IA_RuntimeTextTrace.LogText(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", "AIR_PROD_FAIL", "item=" + data.NomeItem + (string.IsNullOrEmpty(reason) ? string.Empty : " | motivo=" + reason));
            }

            return enqueued;
        }

        private bool HasOperationalValdoriaAirport(out string reason)
        {
            reason = "aeroporto militar próprio ainda não foi construído";
            if (_context == null || _context.WorldState == null)
            {
                reason = "estado do país indisponível";
                return false;
            }

            bool foundMilitaryAirport = false;
            bool foundDryAirport = false;
            for (int i = 0; i < _context.WorldState.OwnStructures.Count; i++)
            {
                GameObject structure = _context.WorldState.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                GerenciadorAeroporto airport = structure.GetComponent<GerenciadorAeroporto>()
                    ?? structure.GetComponentInChildren<GerenciadorAeroporto>(true);
                if (airport == null || airport is GerenciadorAeroportoComercial)
                {
                    continue;
                }

                foundMilitaryAirport = true;
                if (NavalPlacementResolver.IsWaterAtPosition(structure.transform.position))
                {
                    continue;
                }

                foundDryAirport = true;
                if (airport.semEnergia)
                {
                    reason = "aeroporto em terra está sem energia";
                    continue;
                }

                reason = string.Empty;
                return true;
            }

            if (foundMilitaryAirport && !foundDryAirport)
            {
                reason = "aeroporto militar próprio está sobre água";
            }
            else if (foundDryAirport)
            {
                reason = "aeroporto militar próprio está sem energia";
            }

            return false;
        }

        private static int CurrentCountFor(IA01MilitaryAssetKind kind, IA_ForceSnapshot snapshot)
        {
            if (snapshot == null) return 0;
            switch (kind)
            {
                case IA01MilitaryAssetKind.Infantry: return snapshot.InfantryUnits;
                case IA01MilitaryAssetKind.Tank: return snapshot.TankUnits;
                case IA01MilitaryAssetKind.Fighter: return snapshot.FixedWingAircraft;
                case IA01MilitaryAssetKind.Naval: return snapshot.NavalUnits;
                case IA01MilitaryAssetKind.OilTanker: return snapshot.OilTankers;
                default: return 0;
            }
        }

        private bool PublishProductionIntent(IA_CommandRequest request, DadosConstrucao data, int priority, string reasonText, out string reason)
        {
            if (_context != null && _context.Brain != null && !_context.Brain.PodeComprarUnidadeMilitar(data, out reason))
            {
                IA_RuntimeTextTrace.LogCommand(_context.Brain.TeamId, "IA_ProductionDirector", "PROD_CAP_BLOCK", request, reason);
                return false;
            }

            if (_context.IntentBoard == null)
            {
                bool direct = _context.CommandQueue.Enqueue(request, Time.time, out reason);
                IA_RuntimeTextTrace.LogCommand(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", direct ? "PROD_ENQUEUE" : "PROD_REJECT", request, direct ? "enfileirado diretamente" : reason);
                return direct;
            }

            IA_NationalIntent intent = new IA_NationalIntent
            {
                DedupKey = request.DedupKey,
                Origin = "IA_ProductionDirector",
                Reason = reasonText,
                Kind = IA_IntentKind.Produce,
                Priority = priority,
                Urgency = priority,
                EstimatedCost = data != null ? data.ObterPrecoEfetivo() : 0L,
                ExpectedBenefit = 35f,
                ValidUntil = Time.time + 8f,
                Command = request
            };
            bool queued = _context.IntentBoard.Publish(intent, Time.time, out reason);
            IA_RuntimeTextTrace.LogCommand(_context != null && _context.Brain != null ? _context.Brain.TeamId : -1, "IA_ProductionDirector", queued ? "PROD_INTENT" : "PROD_INTENT_REJECT", request, queued ? "intencao de producao publicada" : reason);
            return queued;
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
            // Limita bombardeiros B-260 no aeroporto para garantir caças e atacantes variados
            if (variants.Count > 1 && CountExistingBombers() >= MaxBombersPerAirport)
            {
                variants.Remove("b260");
                if (variants.Count == 0)
                {
                    variants = new List<string>(grouped.Keys);
                }
            }
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
            if (data == null || !data.TryGetPrefabBasico(out prefab))
            {
                return string.Empty;
            }

            string joined = IA_Text.Normalize(data.NomeItem + " " + data.name + " " + prefab.name);
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
            IA_ForceSnapshot snapshot = GetSnapshot();
            if (snapshot != null
                && snapshot.ShipyardCount > 0
                && snapshot.OilTankers < 1
                && HasOilTankerCatalog())
            {
                return QueueOilTanker(priority, cooldown);
            }

            IA03EstrategaNacional estratega = _context != null && _context.Brain != null
                ? _context.Brain.GetComponent<IA03EstrategaNacional>()
                : null;
            if (estratega != null && estratega.isActiveAndEnabled && estratega.TemPerfilConfigurado)
            {
                return QueueIA03SurfaceFleetStep(estratega, navalCount, priority, cooldown);
            }

            return QueueVariedPowerfulWarship(navalCount, priority, cooldown);
        }

        private bool QueueIA03SurfaceFleetStep(
            IA03EstrategaNacional estratega,
            int navalCount,
            int priority,
            float cooldown)
        {
            IA_ForceSnapshot snapshot = GetSnapshot();
            if (snapshot != null
                && snapshot.ShipyardCount > 0
                && snapshot.OilTankers < 1
                && HasOilTankerCatalog())
            {
                return QueueOilTanker(priority, cooldown);
            }

            IA_BrainMaster brain = _context != null ? _context.Brain : null;
            int targetFleet = brain != null ? Mathf.Max(4, brain.TargetFleet) : 6;
            if (navalCount >= targetFleet)
            {
                return false;
            }

            return QueueVariedPowerfulWarship(navalCount, priority, cooldown);
        }

        private bool QueueVariedPowerfulWarship(int navalCount, int priority, float cooldown)
        {
            int slot = Mathf.Abs(navalCount + (_navalUnitRotationIndex++)) % 5;
            switch (slot)
            {
                case 0:
                    return QueueProduceDistinctNaval(
                        priority,
                        cooldown,
                        "F200",
                        "Des_Wall",
                        "F201",
                        "Destroyer Nagoya",
                        "Corveta Fortaleza",
                        "Corveta sam",
                        "Nav_Vigia");
                case 1:
                    return QueueProduceDistinctNaval(
                        priority,
                        cooldown,
                        "F201",
                        "Destroyer Nagoya",
                        "Nav Michele",
                        "Des_Wall",
                        "F200",
                        "USS Ironclad",
                        "Corveta Fortaleza");
                case 2:
                    return QueueProduceDistinctNaval(
                        priority,
                        cooldown,
                        "Destroyer Nagoya",
                        "USS Ironclad",
                        "Nav Michele",
                        "USS Vindicator",
                        "F201",
                        "F200",
                        "Des_Wall");
                case 3:
                    return QueueProduceDistinctNaval(
                        priority,
                        cooldown,
                        "USS Ironclad",
                        "USS Vindicator",
                        "Nav Michele",
                        "USS Sovereign",
                        "Destroyer Nagoya",
                        "F201",
                        "F200");
                default:
                    return QueueProduceDistinctNaval(
                        priority,
                        cooldown,
                        "USS Sovereign",
                        "USS Enterprise",
                        "Nav Global",
                        "USS America",
                        "USS Ironclad",
                        "USS Vindicator",
                        "Destroyer Nagoya",
                        "F201");
            }
        }

        private bool QueueProduceDistinctNaval(int priority, float cooldown, params string[] keys)
        {
            if (_context == null || _context.Backend == null || keys == null || keys.Length == 0)
            {
                return false;
            }

            // 1ª passagem: escolhe um navio disponível que a IA ainda não possui em campo
            // e que não seja repetição imediata do último pedido naval.
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (string.IsNullOrEmpty(key)) continue;
                DadosConstrucao candidate = _context.Backend.FindFirstAvailable(key);
                if (candidate == null) continue;
                string normalizedItem = IA_Text.Normalize(candidate.NomeItem);
                if (!string.IsNullOrEmpty(_lastQueuedNavalItem) && normalizedItem == _lastQueuedNavalItem)
                {
                    continue;
                }

                if (CountUnits(candidate.NomeItem, key) == 0)
                {
                    if (QueueProduceBest(priority, cooldown, candidate.NomeItem, key))
                    {
                        _lastQueuedNavalItem = normalizedItem;
                        return true;
                    }
                }
            }

            // 2ª passagem: escolhe qualquer navio disponível diferente do último enfileirado.
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (string.IsNullOrEmpty(key)) continue;
                DadosConstrucao candidate = _context.Backend.FindFirstAvailable(key);
                if (candidate == null) continue;
                string normalizedItem = IA_Text.Normalize(candidate.NomeItem);
                if (!string.IsNullOrEmpty(_lastQueuedNavalItem) && normalizedItem == _lastQueuedNavalItem)
                {
                    continue;
                }

                if (QueueProduceBest(priority, cooldown, candidate.NomeItem, key))
                {
                    _lastQueuedNavalItem = normalizedItem;
                    return true;
                }
            }

            bool fallback = QueueProduceBest(priority, cooldown, keys);
            if (fallback)
            {
                DadosConstrucao first = _context.Backend.FindFirstAvailable(keys);
                if (first != null)
                {
                    _lastQueuedNavalItem = IA_Text.Normalize(first.NomeItem);
                }
            }
            return fallback;
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

                    if (!hasMilitaryAirport && brain.GetBootstrapStageElapsed(Time.time) >= 12f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.BuildShipyard, "aeroporto militar pendente; liberando construcao do estaleiro");
                        return true;
                    }

                    if (brain.GetBootstrapStageElapsed(Time.time) >= 25f)
                    {
                        brain.SetBootstrapStage(IA_BrainMaster.IA_BootstrapStage.BuildShipyard, "tempo limite de producao de cacas; liberando construcao do estaleiro");
                        return true;
                    }

                    if (!hasMilitaryAirport)
                    {
                        brain.ReportBootstrapError("avioes: aeroporto militar indisponivel");
                        // A fase inicial registra o problema, mas nao pode
                        // monopolizar o diretor quando a infraestrutura ainda
                        // nao existe; outras categorias continuam produzindo.
                        return false;
                    }

                    if (fighterCount < 2 && QueuePreferredAircraft(996, 2.6f))
                    {
                        return true;
                    }

                    brain.ReportBootstrapError("avioes: aguardando catalogo, fila ou vaga do aeroporto");
                    return false;

                case IA_BrainMaster.IA_BootstrapStage.ProduceOilTanker:
                    brain.SetBootstrapStatus("bootstrap: produzindo navio petroleiro");
                    if (!hasNavalBase)
                    {
                        brain.ReportBootstrapError("petroleiro: estaleiro/pier indisponivel");
                        return false;
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

                    return false;

                case IA_BrainMaster.IA_BootstrapStage.ProduceShip:
                    brain.SetBootstrapStatus("bootstrap: produzindo primeiro navio");
                    if (!hasNavalBase)
                    {
                        brain.ReportBootstrapError("navio: estaleiro/pier indisponivel");
                        return false;
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
                    return false;

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
            int fighterTarget = hasMilitaryAirport ? Mathf.Clamp(5 + Mathf.FloorToInt(elapsed / 40f), 5, 14) : 0;
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

        private float ResolveStrategicCacheInterval()
        {
            if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
            {
                return 2.0f;
            }

            if (DiagnosticoDesempenhoJogo.RuntimeSobPressao())
            {
                return 1.6f;
            }

            return 1.1f;
        }

        private void ResolveProductionCapacities(
            float now,
            IA_ForceSnapshot snapshot,
            out int aircraftCapacity,
            out int navalCapacity)
        {
            if (now < _nextCapacityRefreshTime)
            {
                aircraftCapacity = _cachedAircraftCapacity;
                navalCapacity = _cachedNavalCapacity;
                return;
            }

            int teamId = _context != null && _context.Brain != null ? _context.Brain.TeamId : 0;
            DadosPaisGoverno country = SistemaGovernoMundial.Instancia != null && teamId > 0
                ? SistemaGovernoMundial.Instancia.ObterPais(teamId)
                : null;

            float operationalFunds = country != null
                ? Mathf.Max(0f, (float)country.saldo + Mathf.Max(0f, country.rendaPorSegundo - country.gastosPorSegundo) * 180f)
                : (_context != null && _context.Brain != null ? Mathf.Max(0f, _context.Brain.Credits) : 0f);
            float economyFactor = Mathf.Clamp01(operationalFunds / 60000f);
            float industrialFactor = country != null
                ? Mathf.Clamp01(country.nivelIndustrial / 100f)
                : 0.5f;

            int ownedTerritoryMarkers = CountOwnedTerritoryMarkers(teamId);
            int structureTerritoryScore = snapshot != null
                ? Mathf.Clamp(snapshot.TotalOwnStructures / 4, 0, 8)
                : 0;
            int territoryScore = Mathf.Clamp(ownedTerritoryMarkers + structureTerritoryScore, 0, 12);
            int territoryBonus = Mathf.Clamp(territoryScore / 2, 0, 8);

            int economicBonus = Mathf.RoundToInt(economyFactor * 8f);
            int industrialBonus = Mathf.RoundToInt(industrialFactor * 10f);
            int airportBonus = snapshot != null
                ? snapshot.MilitaryAirportCount * 4 + snapshot.HeliportCount * 2
                : 0;
            int navalInfrastructureBonus = snapshot != null
                ? snapshot.ShipyardCount * 4 + snapshot.PierCount * 2
                : 0;

            DificuldadeJogo difficulty = DificuldadeJogo.Normal;
            PerfilDificuldadeJogo difficultyProfile = GameDifficultyManager.PerfilAtual;
            if (difficultyProfile != null)
            {
                difficulty = difficultyProfile.Dificuldade;
            }

            int maximumAircraft = 36;
            int maximumNaval = 30;
            switch (difficulty)
            {
                case DificuldadeJogo.Facil:
                    maximumAircraft = 30;
                    maximumNaval = 24;
                    break;
                case DificuldadeJogo.Dificil:
                    maximumAircraft = 46;
                    maximumNaval = 40;
                    break;
                case DificuldadeJogo.Imperial:
                    maximumAircraft = 58;
                    maximumNaval = 52;
                    break;
            }

            _cachedAircraftCapacity = Mathf.Clamp(
                BaseAircraftCapacity + economicBonus + industrialBonus + territoryBonus + airportBonus,
                BaseAircraftCapacity,
                maximumAircraft);
            _cachedNavalCapacity = Mathf.Clamp(
                BaseNavalCapacity + economicBonus + industrialBonus + territoryBonus + navalInfrastructureBonus,
                BaseNavalCapacity,
                maximumNaval);
            _nextCapacityRefreshTime = now + (DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 3f : CapacityRefreshInterval);

            aircraftCapacity = _cachedAircraftCapacity;
            navalCapacity = _cachedNavalCapacity;
        }

        private int CountOwnedTerritoryMarkers(int teamId)
        {
            if (teamId <= 0)
            {
                return 0;
            }

            RegistroEntidadesJogo.FillMarcadoresTerritorio(_territoryMarkerBuffer);
            int count = 0;
            for (int i = 0; i < _territoryMarkerBuffer.Count; i++)
            {
                MarcadorTerritorio marker = _territoryMarkerBuffer[i];
                if (marker == null || !marker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int markerTeam = marker.teamID;
                if (markerTeam <= 0)
                {
                    IdentidadeUnidade identity = marker.GetComponent<IdentidadeUnidade>();
                    markerTeam = identity != null ? identity.teamID : markerTeam;
                }

                if (markerTeam == teamId)
                {
                    count++;
                }
            }

            return count;
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
                    || unit.GetComponent<GerenciadorPortaAvioes>() != null
                    || name.Contains("navio")
                    || name.Contains("nav_")
                    || name.Contains("nav ")
                    || name.Contains("vigia")
                    || name.Contains("corveta")
                    || name.Contains("fragata")
                    || name.Contains("f200")
                    || name.Contains("f201")
                    || name.Contains("michele")
                    || name.Contains("nagoya")
                    || name.Contains("destroy")
                    || name.Contains("ironclad")
                    || name.Contains("dominion")
                    || name.Contains("sovereign")
                    || name.Contains("enterprise")
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
