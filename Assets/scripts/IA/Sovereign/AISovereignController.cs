using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    [DefaultExecutionOrder(-910)]
    public sealed class AISovereignController : MonoBehaviour
    {
        [Header("Identidade")]
        [SerializeField] private int teamId = 2;
        [SerializeField] private bool autoClaimAuthority = true;

        [Header("Dificuldade")]
        [SerializeField] private AISovereignEnvelope envelopeBase = AISovereignEnvelope.Auto;
        [SerializeField] private bool permitirSpawnEmergencial = true;

        [Header("Ritmo")]
        [SerializeField] private float perceptionInterval = 1.00f;
        [SerializeField] private float strategyInterval = 2.50f;
        [SerializeField] private float economyInterval = 3.00f;
        [SerializeField] private float diplomacyInterval = 7.00f;
        [SerializeField] private float warInterval = 2.40f;
        [SerializeField] private float flushInterval = 0.70f;
        [SerializeField] private float heavyInterval = 6.00f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;
        [TextArea(4, 12)] [SerializeField] private string runtimeSummary = string.Empty;

        private readonly AIStrategicBlackboard _blackboard = new AIStrategicBlackboard();
        private readonly float[] _hostility = new float[3];
        private AISovereignPerception _perception;
        private AISovereignBackend _backend;
        private AISovereignOrderBus _orders;
        private AIPresidentProfile _presidentProfile;
        private AILegacyObserverAdapter _legacyAdapter;
        private AISovereignSeverity _severity;
        private int[] _alliedTeams = System.Array.Empty<int>();
        private float _smoothedFps = 60f;
        private float _nextPerceptionTime;
        private float _nextStrategyTime;
        private float _nextEconomyTime;
        private float _nextDiplomacyTime;
        private float _nextWarTime;
        private float _nextFlushTime;
        private float _nextHeavyTime;
        private float _nextAlliesSyncTime;
        private string _ownerKey;
        private bool _authorityActive;
        private int _runtimeTeamId = int.MinValue;

        public int TeamId => teamId;

        private void Awake()
        {
            EnsureRuntimeWiring();
            EnsureNationalData();
            BuildPresidentProfile();
        }

        private void OnEnable()
        {
            EnsureRuntimeWiring();
            EnsureNationalData();
            if (!EnsureModeEligible())
            {
                return;
            }
            if (autoClaimAuthority)
            {
                _authorityActive = AIControlAuthority.Claim(teamId, _ownerKey, IA_SharedRuntimeSupport.SovereignAuthorityPriority);
            }

            AISovereignRuntime.Instance.Register(GetInstanceID(), teamId);
            _legacyAdapter.Apply(_authorityActive);
        }

        private void OnDisable()
        {
            AISovereignRuntime.Instance.Unregister(GetInstanceID());
            if (_authorityActive)
            {
                AIControlAuthority.Release(teamId, _ownerKey);
                _authorityActive = false;
            }
            _legacyAdapter.Restore();
        }

        private void Update()
        {
            if (teamId <= 1)
            {
                return;
            }

            UpdateFrameHealth();
            EnsureNationalData();
            SyncAlliesIfNeeded();

            if (!EnsureModeEligible())
            {
                return;
            }

            _authorityActive = AIControlAuthority.CanIssue(teamId, _ownerKey);
            _legacyAdapter.Apply(_authorityActive);
            if (!_authorityActive)
            {
                return;
            }

            float now = Time.time;
            if (now >= _nextPerceptionTime)
            {
                _perception.ConfigureAllies(_alliedTeams);
                _perception.Refresh(now);
                _nextPerceptionTime = now + ResolveInterval(perceptionInterval);
            }

            if (now >= _nextStrategyTime)
            {
                RefreshBlackboard(now);
                _presidentProfile.Mutate(_blackboard, Time.unscaledTime);
                _nextStrategyTime = now + ResolveInterval(strategyInterval);
            }

            if (now >= _nextEconomyTime)
            {
                EvaluateEconomy(now);
                _nextEconomyTime = now + ResolveInterval(economyInterval);
            }

            if (now >= _nextDiplomacyTime)
            {
                EvaluateDiplomacy(now);
                _nextDiplomacyTime = now + ResolveInterval(diplomacyInterval);
            }

            if (now >= _nextWarTime)
            {
                EvaluateWar(now);
                _nextWarTime = now + ResolveInterval(warInterval);
            }

            if (now >= _nextHeavyTime && AISovereignRuntime.Instance.ShouldRunHeavy(GetInstanceID(), Time.frameCount))
            {
                EvaluateHeavyStrategy(now);
                _nextHeavyTime = now + ResolveInterval(heavyInterval);
            }

            if (now >= _nextFlushTime)
            {
                FlushOrders(now);
                _nextFlushTime = now + ResolveInterval(flushInterval);
            }

            runtimeSummary = BuildSummary();
        }

        public void ConfigureRuntimeTeam(int newTeamId)
        {
            if (newTeamId <= 0)
            {
                return;
            }

            if (teamId == newTeamId && _runtimeTeamId == newTeamId && _perception != null && _backend != null && _orders != null && _legacyAdapter != null)
            {
                return;
            }

            bool wasRegistered = false;
            if (_runtimeTeamId > 0 && _runtimeTeamId != newTeamId)
            {
                if (_authorityActive)
                {
                    AIControlAuthority.Release(_runtimeTeamId, _ownerKey);
                    _authorityActive = false;
                }

                if (_legacyAdapter != null)
                {
                    _legacyAdapter.Restore();
                }

                AISovereignRuntime.Instance.Unregister(GetInstanceID());
                wasRegistered = true;
            }

            teamId = newTeamId;
            EnsureRuntimeWiring(true);
            EnsureNationalData();
            BuildPresidentProfile();
            _nextAlliesSyncTime = 0f;

            if (!EnsureModeEligible())
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!wasRegistered)
            {
                AISovereignRuntime.Instance.Unregister(GetInstanceID());
            }

            if (autoClaimAuthority)
            {
                _authorityActive = AIControlAuthority.Claim(teamId, _ownerKey, IA_SharedRuntimeSupport.SovereignAuthorityPriority);
            }

            AISovereignRuntime.Instance.Register(GetInstanceID(), teamId);
            _legacyAdapter.Apply(_authorityActive);
        }

        private void EnsureRuntimeWiring(bool force = false)
        {
            if (!force
                && _runtimeTeamId == teamId
                && _perception != null
                && _backend != null
                && _orders != null
                && _legacyAdapter != null)
            {
                return;
            }

            _runtimeTeamId = teamId;
            _ownerKey = "AISovereign:" + teamId + ":" + GetInstanceID();
            _perception = new AISovereignPerception(teamId);
            _backend = new AISovereignBackend(teamId);
            _orders = new AISovereignOrderBus();
            _legacyAdapter = new AILegacyObserverAdapter(teamId, _ownerKey);
        }

        private void EnsureNationalData()
        {
            SistemaGovernoMundial.GarantirInstancia();
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            DadosPaisGoverno pais = gov.ObterPais(teamId);
            if (pais == null)
            {
                gov.GarantirPaisIA(teamId, "Soberania " + teamId, "Moeda " + teamId, "S$" + teamId, PerfilPaisIA.Neutro, ModoInicialPaisIA.Crescimento);
            }
        }

        private bool EnsureModeEligible()
        {
            if (IA_SharedRuntimeSupport.IsStackAllowedInCurrentMode(GetType().FullName))
            {
                return true;
            }

            if (_authorityActive)
            {
                AIControlAuthority.Release(teamId, _ownerKey);
                _authorityActive = false;
            }

            if (_legacyAdapter != null)
            {
                _legacyAdapter.Apply(false);
            }

            return false;
        }

        private void BuildPresidentProfile()
        {
            DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
            int seed = teamId * 997;
            if (pais != null)
            {
                seed ^= (pais.nomePresidente ?? string.Empty).GetHashCode();
                seed ^= pais.nomePais.GetHashCode();
            }

            _presidentProfile = AIPresidentProfile.Create(pais, seed);
        }

        private void RefreshBlackboard(float now)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            DadosPaisGoverno pais = gov.ObterPais(teamId);
            if (pais == null)
            {
                return;
            }

            PerfilDificuldadeJogo difficulty = GameDifficultyManager.PerfilAtual;
            RecursoMercado criticalNeed = IA_EconomyDirector.ResolveCriticalNeed(pais);
            RecursoMercado bestSurplus = IA_EconomyDirector.ResolveBestSurplus(pais);

            _blackboard.TeamId = teamId;
            _blackboard.PlayerTeamId = gov.teamJogador;
            _blackboard.RivalTeamId = pais.rivalTeamId > 0 ? pais.rivalTeamId : gov.teamJogador;
            _blackboard.BaseCenter = _perception.BaseCenter != Vector3.zero ? _perception.BaseCenter : transform.position;
            _blackboard.EnemyAnchor = _perception.LastKnownEnemyAnchor;
            _blackboard.UnderThreat = _perception.UnderThreat;
            _blackboard.CanExpand = !_perception.UnderThreat && _perception.FactoryCount > 0 && _perception.BarracksCount > 0;
            _blackboard.EnemyAcrossOcean = _perception.EnemyAcrossOcean;
            _blackboard.AtWar = pais.emGuerra;
            _blackboard.Stability = pais.estabilidade;
            _blackboard.CriticalNeed = criticalNeed;
            _blackboard.BestSurplus = bestSurplus;
            _blackboard.RadarCount = _perception.RadarCount;
            _blackboard.AirportCount = _perception.AirportCount;
            _blackboard.ShipyardCount = _perception.ShipyardCount;
            _blackboard.PlatformCount = _perception.PlatformCount;
            _blackboard.FactoryCount = _perception.FactoryCount;
            _blackboard.WarehouseCount = _perception.WarehouseCount;
            _blackboard.BarracksCount = _perception.BarracksCount;
            _blackboard.NavalTransportCount = _perception.NavalTransportCount;
            _blackboard.FighterCount = _perception.FighterCount;
            _blackboard.OwnLandUnits = _perception.OwnLandUnits;
            _blackboard.OwnNavalUnits = _perception.OwnNavalUnits;
            _blackboard.OwnAirUnits = _perception.OwnAirUnits;
            _blackboard.VisibleEnemyLand = _perception.VisibleEnemyLand;
            _blackboard.VisibleEnemyNaval = _perception.VisibleEnemyNaval;
            _blackboard.VisibleEnemyAir = _perception.VisibleEnemyAir;
            _blackboard.EconomyHealth = Mathf.Clamp01((pais.PontuacaoEconomica() + Mathf.Max(0f, pais.qualidadeVida - 40f)) / 160f);
            _blackboard.WarReadiness = Mathf.Clamp01((pais.armamentos / 1200f) + (_perception.OwnLandUnits + _perception.OwnNavalUnits + _perception.OwnAirUnits) / 24f);
            _blackboard.PlayerPressure = Mathf.Clamp01((_perception.VisibleEnemyLand + _perception.VisibleEnemyNaval + _perception.VisibleEnemyAir) / 12f);
            _blackboard.Envelope = _presidentProfile.ResolveEnvelope(envelopeBase, difficulty);
            _blackboard.DominantThreatDomain = ResolveThreatDomain();
            _blackboard.DominantThreatWeight = _hostility[(int)_blackboard.DominantThreatDomain];
            _blackboard.LastUpdatedTime = now;
            _blackboard.StrategicPlan = ResolveStrategicPlan(pais);
        }

        private void EvaluateEconomy(float now)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            DadosPaisGoverno pais = gov.ObterPais(teamId);
            if (pais == null)
            {
                return;
            }

            if (_blackboard.RadarCount <= 0 && _blackboard.UnderThreat)
            {
                QueueBuild(AISovereignCatalogRole.Radar, _blackboard.BaseCenter, 960, 18f, "economia:radar_critico");
            }

            if (_blackboard.CriticalNeed == RecursoMercado.Comida)
            {
                QueueBuild(AISovereignCatalogRole.Farm, _blackboard.BaseCenter, 910, 16f, "economia:farm");
            }
            else if (_blackboard.CriticalNeed == RecursoMercado.Aco || _blackboard.CriticalNeed == RecursoMercado.Armamentos)
            {
                QueueBuild(AISovereignCatalogRole.Factory, _blackboard.BaseCenter, 900, 18f, "economia:fabrica");
            }
            else if (_blackboard.CriticalNeed == RecursoMercado.Petroleo)
            {
                QueueBuild(AISovereignCatalogRole.Platform, _blackboard.BaseCenter, 905, 22f, "economia:plataforma");
                QueueProduce(AISovereignCatalogRole.OilShip, 860, 18f, "economia:petroleiro");
            }

            if (_blackboard.FactoryCount <= 0)
            {
                QueueBuild(AISovereignCatalogRole.Factory, _blackboard.BaseCenter, 880, 18f, "core:fabrica");
            }
            if (_blackboard.BarracksCount <= 0)
            {
                QueueBuild(AISovereignCatalogRole.Barracks, _blackboard.BaseCenter, 875, 18f, "core:quartel");
            }
            if (_blackboard.WarehouseCount <= 0)
            {
                QueueBuild(AISovereignCatalogRole.Warehouse, _blackboard.BaseCenter, 870, 16f, "core:armazem");
            }
            if (_blackboard.AirportCount <= 0 && (_presidentProfile.Aggression >= 0.42f || _blackboard.OwnAirUnits <= 0))
            {
                QueueBuild(AISovereignCatalogRole.Airport, _blackboard.BaseCenter, 865, 22f, "core:aeroporto");
            }
            if (_blackboard.ShipyardCount <= 0 && (_blackboard.EnemyAcrossOcean || _presidentProfile.NavalFocus >= 0.55f))
            {
                QueueBuild(AISovereignCatalogRole.Shipyard, _blackboard.BaseCenter, 850, 24f, "core:estaleiro");
            }
            if (_blackboard.PlatformCount <= 0 && pais.petroleo < 850 && _blackboard.ShipyardCount > 0)
            {
                QueueBuild(AISovereignCatalogRole.Platform, _blackboard.BaseCenter, 840, 24f, "core:plataforma");
            }
            if (_blackboard.FighterCount < Mathf.Max(2, Mathf.RoundToInt(2f + _presidentProfile.Aggression * 3f)))
            {
                QueueProduce(AISovereignCatalogRole.Fighter, 830, 10f, "prod:cacas");
            }
        }

        private void EvaluateDiplomacy(float now)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            if (gov == null || mercado == null)
            {
                return;
            }

            DadosPaisGoverno pais = gov.ObterPais(teamId);
            if (pais == null)
            {
                return;
            }

            if (_blackboard.CriticalNeed != RecursoMercado.Nenhum)
            {
                QueueBuyNeed(_blackboard.CriticalNeed, pais, gov, mercado);
            }

            if (_blackboard.BestSurplus != RecursoMercado.Nenhum)
            {
                QueueSellSurplus(_blackboard.BestSurplus, pais, gov, mercado);
            }

            RelacaoPaisGoverno relJogador = gov.ObterRelacao(teamId, gov.teamJogador);
            if (_blackboard.PlayerPressure > 0.40f && relJogador.valor <= -35 && !relJogador.sancaoAtiva && _presidentProfile.DiplomaticCunning >= 0.45f)
            {
                var order = new AISovereignOrderBus.Order
                {
                    Type = AISovereignOrderType.Proposal,
                    ProposalType = TipoPropostaInternacional.Sancao,
                    CounterpartyTeamId = gov.teamJogador,
                    Resource = RecursoMercado.Nenhum,
                    Quantity = 1,
                    UnitPrice = 1,
                    Priority = 620,
                    CooldownSeconds = 60f,
                    DedupKey = "dip:sancao:" + teamId,
                    Reason = "pressao diplomatica"
                };
                _orders.Enqueue(order, now);
            }
        }

        private void EvaluateWar(float now)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                return;
            }

            RelacaoPaisGoverno relJogador = gov.ObterRelacao(teamId, gov.teamJogador);
            if (_blackboard.PlayerPressure > 0.55f && relJogador.valor <= -55 && !_blackboard.AtWar)
            {
                var warOrder = new AISovereignOrderBus.Order
                {
                    Type = AISovereignOrderType.DeclareWar,
                    CounterpartyTeamId = gov.teamJogador,
                    Priority = 980,
                    CooldownSeconds = 90f,
                    DedupKey = "war:" + teamId,
                    Reason = "revide_automatico"
                };
                _orders.Enqueue(warOrder, now);
            }

            if (_blackboard.UnderThreat)
            {
                QueueDefensePackages(now);
                QueueBuild(AISovereignCatalogRole.Turret, _blackboard.BaseCenter, 900, 22f, "defesa:torreta");
                QueueBuild(AISovereignCatalogRole.Ciws, _blackboard.BaseCenter, 885, 22f, "defesa:ciws");
            }

            if (_perception.TryGetBestTarget(_presidentProfile, out AISovereignPerception.EnemyContact target))
            {
                QueueStrikePackage(target, now);
            }
            else if (_blackboard.EnemyAnchor != Vector3.zero)
            {
                QueuePressureOrRecon(now, _blackboard.EnemyAnchor);
            }
        }

        private void EvaluateHeavyStrategy(float now)
        {
            if (_blackboard.EnemyAcrossOcean && _blackboard.NavalTransportCount <= 0)
            {
                QueueProduce(AISovereignCatalogRole.NavalTransport, 780, 24f, "heavy:transporte_naval");
            }

            if (_blackboard.EnemyAcrossOcean && _presidentProfile.NavalFocus >= 0.64f && _blackboard.OwnNavalUnits < 3)
            {
                QueueProduce(AISovereignCatalogRole.NavalPatrol, 770, 18f, "heavy:patrulha_naval");
            }

            if (_blackboard.EnemyAcrossOcean && _blackboard.NavalTransportCount > 0 && _perception.TryGetEnemyStrategicAnchor(out Vector3 anchor))
            {
                var package = new AICombatPackage
                {
                    Type = AICombatPackageType.AmphibiousAssault,
                    Domain = AISovereignDomain.Naval,
                    TargetTeamId = _blackboard.RivalTeamId,
                    TargetPoint = anchor,
                    StagingPoint = Vector3.Lerp(_blackboard.BaseCenter, anchor, 0.35f),
                    Priority = 760,
                    CooldownSeconds = 26f,
                    MaxUnits = 4,
                    TargetTag = "amphibio"
                };
                QueuePackage(package, now);
            }
        }

        private void QueueDefensePackages(float now)
        {
            Vector3 hotspot = _blackboard.EnemyAnchor != Vector3.zero ? _blackboard.EnemyAnchor : _blackboard.BaseCenter;
            var defense = new AICombatPackage
            {
                Type = AICombatPackageType.LocalDefense,
                Domain = _blackboard.DominantThreatDomain == AISovereignDomain.Air ? AISovereignDomain.Air : AISovereignDomain.Land,
                TargetTeamId = _blackboard.RivalTeamId,
                TargetPoint = hotspot,
                StagingPoint = _blackboard.BaseCenter,
                Priority = 940,
                CooldownSeconds = 9f,
                MaxUnits = _blackboard.DominantThreatDomain == AISovereignDomain.Air ? 4 : 8,
                TargetTag = "defesa_local"
            };
            QueuePackage(defense, now);
        }

        private void QueueStrikePackage(AISovereignPerception.EnemyContact target, float now)
        {
            if (target == null)
            {
                return;
            }

            string normalized = IA_Text.Normalize(target.Name);
            AICombatPackageType packageType = AICombatPackageType.LandAssault;
            AISovereignDomain packageDomain = target.Domain;

            if (normalized.Contains("radar") || normalized.Contains("aeroporto") || normalized.Contains("airport") || normalized.Contains("pista"))
            {
                packageType = AICombatPackageType.SensorSuppression;
                packageDomain = _blackboard.OwnAirUnits > 0 ? AISovereignDomain.Air : target.Domain;
            }
            else if (normalized.Contains("plataforma") || normalized.Contains("petro") || normalized.Contains("armazem") || normalized.Contains("log"))
            {
                packageType = AICombatPackageType.LogisticsRaid;
                packageDomain = _blackboard.EnemyAcrossOcean || target.Domain == AISovereignDomain.Naval ? AISovereignDomain.Naval : AISovereignDomain.Air;
            }
            else if (_blackboard.EnemyAcrossOcean)
            {
                packageType = AICombatPackageType.NavalStrike;
                packageDomain = AISovereignDomain.Naval;
            }
            else if (_blackboard.DominantThreatDomain == AISovereignDomain.Air && _blackboard.OwnAirUnits > 0)
            {
                packageType = AICombatPackageType.AirStrike;
                packageDomain = AISovereignDomain.Air;
            }
            else if (_blackboard.DominantThreatDomain == AISovereignDomain.Naval && _blackboard.OwnNavalUnits > 0)
            {
                packageType = AICombatPackageType.NavalStrike;
                packageDomain = AISovereignDomain.Naval;
            }

            var package = new AICombatPackage
            {
                Type = packageType,
                Domain = packageDomain,
                TargetTeamId = target.TeamId,
                TargetTag = normalized,
                TargetPoint = target.Position,
                TargetTransform = target.Transform,
                StagingPoint = Vector3.Lerp(_blackboard.BaseCenter, target.Position, 0.30f),
                Priority = packageType == AICombatPackageType.SensorSuppression ? 920 : 860,
                CooldownSeconds = packageType == AICombatPackageType.SensorSuppression ? 18f : 14f,
                MaxUnits = packageDomain == AISovereignDomain.Air ? 4 : (packageDomain == AISovereignDomain.Naval ? 5 : 8),
                PreferSensorBlind = packageType == AICombatPackageType.SensorSuppression
            };
            QueuePackage(package, now);
        }

        private void QueuePressureOrRecon(float now, Vector3 anchor)
        {
            var recon = new AICombatPackage
            {
                Type = _blackboard.WarReadiness >= 0.62f ? AICombatPackageType.PressurePatrol : AICombatPackageType.Recon,
                Domain = _blackboard.OwnAirUnits > 0 ? AISovereignDomain.Air : AISovereignDomain.Land,
                TargetTeamId = _blackboard.RivalTeamId,
                TargetPoint = anchor,
                StagingPoint = Vector3.Lerp(_blackboard.BaseCenter, anchor, 0.25f),
                Priority = 720,
                CooldownSeconds = 16f,
                MaxUnits = _blackboard.OwnAirUnits > 0 ? 2 : 4,
                TargetTag = "recon_pressao"
            };
            QueuePackage(recon, now);
        }

        private void QueueBuyNeed(RecursoMercado need, DadosPaisGoverno pais, SistemaGovernoMundial gov, SistemaMercadoGlobal mercado)
        {
            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(need));
            if (item == null)
            {
                return;
            }

            DadosPaisGoverno vendedor = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < gov.Paises.Count; i++)
            {
                DadosPaisGoverno candidate = gov.Paises[i];
                if (candidate == null || candidate.teamId == teamId)
                {
                    continue;
                }

                int stock = gov.ObterEstoque(candidate.teamId, need);
                if (stock <= item.CalcularQuantidadePadrao())
                {
                    continue;
                }

                RelacaoPaisGoverno rel = gov.ObterRelacao(teamId, candidate.teamId);
                int score = stock + rel.valor * 6 + (rel.pactoMilitar ? 200 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    vendedor = candidate;
                }
            }

            if (vendedor == null)
            {
                return;
            }

            var order = new AISovereignOrderBus.Order
            {
                Type = AISovereignOrderType.MarketBuy,
                CounterpartyTeamId = vendedor.teamId,
                Resource = need,
                Quantity = Mathf.Clamp(item.CalcularQuantidadePadrao(), 20, Mathf.Max(20, pais.saldo / Mathf.Max(1, item.precoAtual))),
                UnitPrice = item.precoAtual,
                Priority = 690,
                CooldownSeconds = 18f,
                DedupKey = "market:buy:" + need,
                Reason = "deficit_critico"
            };
            _orders.Enqueue(order, Time.time);
        }

        private void QueueSellSurplus(RecursoMercado surplus, DadosPaisGoverno pais, SistemaGovernoMundial gov, SistemaMercadoGlobal mercado)
        {
            DadosItemMercado item = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(surplus));
            if (item == null)
            {
                return;
            }

            DadosPaisGoverno comprador = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < gov.Paises.Count; i++)
            {
                DadosPaisGoverno candidate = gov.Paises[i];
                if (candidate == null || candidate.teamId == teamId)
                {
                    continue;
                }

                if (IA_EconomyDirector.ResolveCriticalNeed(candidate) != surplus)
                {
                    continue;
                }

                RelacaoPaisGoverno rel = gov.ObterRelacao(teamId, candidate.teamId);
                int score = candidate.saldo + rel.valor * 8 + (rel.pactoMilitar ? 120 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    comprador = candidate;
                }
            }

            if (comprador == null)
            {
                return;
            }

            var order = new AISovereignOrderBus.Order
            {
                Type = AISovereignOrderType.MarketSell,
                CounterpartyTeamId = comprador.teamId,
                Resource = surplus,
                Quantity = Mathf.Clamp(gov.ObterEstoque(teamId, surplus) / 5, item.CalcularQuantidadePadrao(), item.CalcularQuantidadePadrao() * 3),
                UnitPrice = item.precoAtual,
                Priority = 640,
                CooldownSeconds = 20f,
                DedupKey = "market:sell:" + surplus,
                Reason = "excedente"
            };
            _orders.Enqueue(order, Time.time);
        }

        private void FlushOrders(float now)
        {
            int maxCommands = AISovereignRuntime.Instance.ResolveCommandCap(_blackboard.Envelope >= AISovereignEnvelope.Brutal ? 5 : 3);
            float budgetMs = Mathf.Max(0.12f, 0.95f * AISovereignRuntime.Instance.ResolveBudgetScale(_smoothedFps));
            float start = Time.realtimeSinceStartup * 1000f;
            int executed = 0;

            while (executed < maxCommands && (Time.realtimeSinceStartup * 1000f) - start <= budgetMs)
            {
                if (!_orders.TryDequeue(now, out AISovereignOrderBus.Order order))
                {
                    break;
                }

                bool success = ExecuteOrder(order, out string reason);
                _orders.Complete(order, success, now);
                if (verboseLogs && !success)
                {
                    Debug.Log("[AISovereign] Falha " + teamId + " ordem=" + order.Type + " motivo=" + reason);
                }
                executed++;
            }
        }

        private bool ExecuteOrder(AISovereignOrderBus.Order order, out string reason)
        {
            reason = string.Empty;
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;

            switch (order.Type)
            {
                case AISovereignOrderType.BuildRole:
                    return _backend.TryBuild(order.Role, order.Anchor, _perception, out reason);

                case AISovereignOrderType.ProduceRole:
                    return _backend.TryProduce(order.Role, permitirSpawnEmergencial ? _blackboard.Envelope : AISovereignEnvelope.Justa, _perception, out reason);

                case AISovereignOrderType.CombatPackage:
                    int issued = _backend.IssueCombatPackage(order.Package, _perception, _presidentProfile, _severity, out reason);
                    return issued > 0;

                case AISovereignOrderType.MarketBuy:
                    if (gov == null || mercado == null)
                    {
                        reason = "mercado_indisponivel";
                        return false;
                    }
                    DadosItemMercado itemBuy = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(order.Resource));
                    if (itemBuy == null)
                    {
                        reason = "item_mercado_invalido";
                        return false;
                    }
                    return mercado.Comprar(teamId, order.CounterpartyTeamId, itemBuy.id, order.Quantity, out reason);

                case AISovereignOrderType.MarketSell:
                    if (gov == null || mercado == null)
                    {
                        reason = "mercado_indisponivel";
                        return false;
                    }
                    DadosItemMercado itemSell = mercado.ObterItem(SistemaGovernoMundial.IdRecurso(order.Resource));
                    if (itemSell == null)
                    {
                        reason = "item_mercado_invalido";
                        return false;
                    }
                    return mercado.Vender(teamId, order.CounterpartyTeamId, itemSell.id, order.Quantity, out reason);

                case AISovereignOrderType.Proposal:
                    if (gov == null)
                    {
                        reason = "governo_indisponivel";
                        return false;
                    }
                    return gov.TentarCriarProposta(new PropostaInternacional
                    {
                        origemTeamId = teamId,
                        alvoTeamId = order.CounterpartyTeamId,
                        tipo = order.ProposalType,
                        recurso = order.Resource,
                        quantidade = Mathf.Max(1, order.Quantity),
                        precoUnitario = Mathf.Max(1, order.UnitPrice),
                        prioridade = Mathf.Max(50, order.Priority / 10),
                        motivo = string.IsNullOrWhiteSpace(order.Reason) ? "pressao diplomatica" : order.Reason,
                        expiraEm = Time.unscaledTime + 95f,
                        dedupKey = order.DedupKey
                    });

                case AISovereignOrderType.DeclareWar:
                    if (gov == null)
                    {
                        reason = "governo_indisponivel";
                        return false;
                    }
                    gov.NotificarGuerra(teamId);
                    return true;
            }

            reason = "ordem_sem_executor";
            return false;
        }

        private void SyncAlliesIfNeeded()
        {
            if (Time.unscaledTime < _nextAlliesSyncTime)
            {
                return;
            }

            _nextAlliesSyncTime = Time.unscaledTime + 10f;
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            if (gov == null)
            {
                _alliedTeams = System.Array.Empty<int>();
                return;
            }

            var allies = new List<int>(6);
            foreach (DadosPaisGoverno ally in gov.ObterAliados(teamId))
            {
                if (ally != null)
                {
                    allies.Add(ally.teamId);
                }
            }
            _alliedTeams = allies.ToArray();
        }

        private void UpdateFrameHealth()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float fps = 1f / dt;
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.25f);

            AISovereignSeverity measured = AISovereignSeverity.Stable;
            if (DiagnosticoDesempenhoJogo.RuntimeSaturado() || _smoothedFps < 18f)
            {
                measured = AISovereignSeverity.Emergency;
            }
            else if (DiagnosticoDesempenhoJogo.RuntimeSobPressao() || _smoothedFps < 28f)
            {
                measured = AISovereignSeverity.Throttled;
            }
            else if (_smoothedFps < 42f)
            {
                measured = AISovereignSeverity.Watch;
            }

            _severity = AISovereignRuntime.Instance.ResolveSeverity(GetInstanceID(), measured, _smoothedFps, 30f);
        }

        private float ResolveInterval(float baseInterval)
        {
            switch (_severity)
            {
                case AISovereignSeverity.Emergency:
                    return baseInterval * 2.35f;
                case AISovereignSeverity.Throttled:
                    return baseInterval * 1.65f;
                case AISovereignSeverity.Watch:
                    return baseInterval * 1.25f;
                default:
                    return baseInterval;
            }
        }

        private AISovereignDomain ResolveThreatDomain()
        {
            float airPressure = Mathf.Lerp(_hostility[(int)AISovereignDomain.Air], Mathf.Clamp01(_perception.VisibleEnemyAir / 4f), 0.35f);
            float navalPressure = Mathf.Lerp(_hostility[(int)AISovereignDomain.Naval], Mathf.Clamp01(_perception.VisibleEnemyNaval / 4f), 0.35f);
            float landPressure = Mathf.Lerp(_hostility[(int)AISovereignDomain.Land], Mathf.Clamp01(_perception.VisibleEnemyLand / 6f), 0.35f);

            _hostility[(int)AISovereignDomain.Air] = airPressure;
            _hostility[(int)AISovereignDomain.Naval] = navalPressure;
            _hostility[(int)AISovereignDomain.Land] = landPressure;

            AISovereignDomain domain = AISovereignDomain.Land;
            float best = landPressure;
            if (navalPressure > best)
            {
                best = navalPressure;
                domain = AISovereignDomain.Naval;
            }
            if (airPressure > best)
            {
                domain = AISovereignDomain.Air;
            }
            return domain;
        }

        private string ResolveStrategicPlan(DadosPaisGoverno pais)
        {
            if (_blackboard.UnderThreat)
            {
                return "DefesaAtiva";
            }
            if (_blackboard.CriticalNeed != RecursoMercado.Nenhum)
            {
                return "RecuperacaoEconomica";
            }
            if (_blackboard.EnemyAcrossOcean)
            {
                return "ExpedicaoMaritima";
            }
            if (_blackboard.WarReadiness >= 0.68f && _presidentProfile.Aggression >= 0.52f)
            {
                return "PressaoMilitar";
            }
            if (pais != null && pais.sancionado)
            {
                return "SobreviverSancoes";
            }
            return "EquilibrioDuradouro";
        }

        private void QueueBuild(AISovereignCatalogRole role, Vector3 anchor, int priority, float cooldown, string dedup)
        {
            _orders.Enqueue(new AISovereignOrderBus.Order
            {
                Type = AISovereignOrderType.BuildRole,
                Role = role,
                Anchor = anchor,
                Priority = priority,
                CooldownSeconds = cooldown,
                DedupKey = dedup
            }, Time.time);
        }

        private void QueueProduce(AISovereignCatalogRole role, int priority, float cooldown, string dedup)
        {
            _orders.Enqueue(new AISovereignOrderBus.Order
            {
                Type = AISovereignOrderType.ProduceRole,
                Role = role,
                Priority = priority,
                CooldownSeconds = cooldown,
                DedupKey = dedup
            }, Time.time);
        }

        private void QueuePackage(AICombatPackage package, float now)
        {
            if (package == null)
            {
                return;
            }

            _orders.Enqueue(new AISovereignOrderBus.Order
            {
                Type = AISovereignOrderType.CombatPackage,
                Package = package,
                Priority = package.Priority,
                CooldownSeconds = package.CooldownSeconds,
                DedupKey = package.BuildDedupKey()
            }, now);
        }

        private string BuildSummary()
        {
            return "IA Soberana"
                   + " | team=" + teamId
                   + " | auth=" + _authorityActive
                   + " | fps=" + _smoothedFps.ToString("0.0")
                   + " | sev=" + _severity
                   + " | env=" + _blackboard.Envelope
                   + " | plan=" + _blackboard.StrategicPlan
                   + " | phase=" + (_presidentProfile != null ? _presidentProfile.Phase.ToString() : "n/d")
                   + " | need=" + _blackboard.CriticalNeed
                   + " | surplus=" + _blackboard.BestSurplus
                   + " | own(L/N/A)=" + _blackboard.OwnLandUnits + "/" + _blackboard.OwnNavalUnits + "/" + _blackboard.OwnAirUnits
                   + " | enemy(L/N/A)=" + _blackboard.VisibleEnemyLand + "/" + _blackboard.VisibleEnemyNaval + "/" + _blackboard.VisibleEnemyAir
                   + " | pending=" + _orders.PendingCount;
        }
    }
}
