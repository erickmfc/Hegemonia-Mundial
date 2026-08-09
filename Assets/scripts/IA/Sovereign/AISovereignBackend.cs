using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    public sealed class AISovereignBackend
    {
        private readonly int _teamId;
        private readonly Dictionary<string, DadosConstrucao> _catalogByKey = new Dictionary<string, DadosConstrucao>();
        private readonly List<DadosConstrucao> _catalog = new List<DadosConstrucao>(256);
        private readonly List<GerenciadorAeroporto> _airports = new List<GerenciadorAeroporto>(16);
        private readonly List<Heliporto> _heliports = new List<Heliporto>(8);
        private readonly List<Estaleiro> _shipyards = new List<Estaleiro>(8);
        private readonly List<PierMarinha> _piers = new List<PierMarinha>(8);
        private readonly List<Fabrica> _factories = new List<Fabrica>(16);
        private readonly List<IdentidadeUnidade> _unitBuffer = new List<IdentidadeUnidade>(256);
        private readonly Collider[] _spacingHits = new Collider[48];
        private float _nextCatalogRefreshTime;

        public AISovereignBackend(int teamId)
        {
            _teamId = teamId;
        }

        public bool TryBuild(AISovereignCatalogRole role, Vector3 anchor, AISovereignPerception perception, out string reason)
        {
            reason = string.Empty;
            if (!TryResolveRole(role, out DadosConstrucao data))
            {
                reason = "catalogo_sem_item";
                return false;
            }

            Vector3 position = anchor;
            Quaternion rotation = Quaternion.identity;
            if (!TryResolveBuildPose(data, perception, ref position, ref rotation, out reason))
            {
                return false;
            }

            if (!TryValidateBuildTerritory(data, role, position, out reason))
            {
                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("ia_build_territory_blocked");
                DiagnosticoDesempenhoJogo.RegistrarEvento(
                    "IA_Soberana_BuildBloqueado",
                    "team=" + _teamId + " role=" + role + " pos=" + position.ToString("F1") + " motivo=" + reason);
                return false;
            }

            float spacing = ResolveStructureSpacing(role, data);
            if (IsCrowded(position, spacing, true))
            {
                if (!TryFindRelaxedPoint(position, spacing, out Vector3 relaxed) || IsCrowded(relaxed, spacing, true))
                {
                    reason = "area_ocupada";
                    return false;
                }
                position = relaxed;

                if (!TryValidateBuildTerritory(data, role, position, out reason))
                {
                    DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("ia_build_territory_blocked");
                    DiagnosticoDesempenhoJogo.RegistrarEvento(
                        "IA_Soberana_BuildBloqueado",
                        "team=" + _teamId + " role=" + role + " pos=" + position.ToString("F1") + " motivo=" + reason);
                    return false;
                }
            }

            GameObject created = null;
            if (Construtor.Instancia != null && data.PrefabDaUnidade != null)
            {
                created = Construtor.Instancia.ConstruirEstruturaIA(data.PrefabDaUnidade, position, rotation);
            }
            if (created == null && data.PrefabDaUnidade != null)
            {
                created = Object.Instantiate(data.PrefabDaUnidade, position, rotation);
            }

            if (created == null)
            {
                reason = "falha_instanciacao";
                return false;
            }

            EnsureIdentity(created);
            Estaleiro createdShipyard = created.GetComponent<Estaleiro>();
            if (createdShipyard != null) createdShipyard.OwnerTeamId = _teamId;
            PierMarinha createdPier = created.GetComponent<PierMarinha>();
            if (createdPier != null) createdPier.OwnerTeamId = _teamId;
            IA_BackendBridge.AttachConstructionMetadata(created, data);
            DiagnosticoDesempenhoJogo.RegistrarConstrucao(data.GetDisplayName(), position, "IA_Soberana_Build");
            return true;
        }

        public bool TryProduce(AISovereignCatalogRole role, AISovereignEnvelope envelope, AISovereignPerception perception, out string reason)
        {
            reason = string.Empty;
            if (!TryResolveRole(role, out DadosConstrucao data))
            {
                reason = "catalogo_sem_item";
                return false;
            }

            bool allowEmergencySpawn = envelope >= AISovereignEnvelope.SemiTrapaca;
            if (TryProduceViaInfrastructure(data, out GameObject produced))
            {
                if (produced != null)
                {
                    EnsureIdentity(produced);
                }
                DiagnosticoDesempenhoJogo.RegistrarProducao(data.GetDisplayName(), "IA_Soberana_Prod");
                return true;
            }

            if (!allowEmergencySpawn || data.PrefabDaUnidade == null)
            {
                reason = "infra_indisponivel";
                return false;
            }

            Vector3 spawn = ResolveFallbackSpawnPoint(perception);
            if (IsCrowded(spawn, 8f, false) && !TryFindRelaxedPoint(spawn, 8f, out spawn))
            {
                reason = "spawn_sem_espaco";
                return false;
            }

            GameObject emergencial = Object.Instantiate(data.PrefabDaUnidade, spawn, Quaternion.identity);
            if (emergencial == null)
            {
                reason = "spawn_emergencial_falhou";
                return false;
            }

            EnsureIdentity(emergencial);
            IA_BackendBridge.AttachConstructionMetadata(emergencial, data);
            DiagnosticoDesempenhoJogo.RegistrarProducao(data.GetDisplayName(), "IA_Soberana_Emergencia");
            return true;
        }

        public int IssueCombatPackage(AICombatPackage package, AISovereignPerception perception, AIPresidentProfile profile, AISovereignSeverity severity, out string reason)
        {
            reason = string.Empty;
            if (package == null || perception == null)
            {
                reason = "package_invalido";
                return 0;
            }

            int maxUnits = Mathf.Max(1, AdjustUnitsForSeverity(package.MaxUnits, severity));
            List<GameObject> selected = SelectUnitsForDomain(package.Domain, perception, maxUnits);
            if (selected.Count == 0)
            {
                reason = "sem_unidades";
                return 0;
            }

            int issued = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                GameObject unit = selected[i];
                if (unit == null)
                {
                    continue;
                }

                ControleUnidade controle = unit.GetComponent<ControleUnidade>();
                if (controle == null)
                {
                    continue;
                }

                bool ok = false;
                switch (package.Type)
                {
                    case AICombatPackageType.Recon:
                    case AICombatPackageType.PressurePatrol:
                        ok = TryIssuePatrolOrRecon(controle, package);
                        break;

                    case AICombatPackageType.LocalDefense:
                        controle.DefinirModoCombate(true);
                        ok = controle.EmitirOrdemMover(package.TargetPoint);
                        break;

                    case AICombatPackageType.AirStrike:
                    case AICombatPackageType.SensorSuppression:
                        ok = package.Domain == AISovereignDomain.Air
                            ? controle.EmitirMissaoAereaOfensiva(package.TargetPoint, package.TargetTransform)
                            : controle.EmitirOrdemMover(package.TargetPoint);
                        break;

                    case AICombatPackageType.NavalStrike:
                    case AICombatPackageType.LogisticsRaid:
                        ok = package.Domain == AISovereignDomain.Naval
                            ? controle.EmitirMissaoNavalOfensiva(package.TargetPoint, package.TargetTransform, true, true)
                            : controle.EmitirOrdemMover(package.TargetPoint);
                        break;

                    case AICombatPackageType.AmphibiousAssault:
                        ok = TryIssueAmphibious(controle, unit, package);
                        break;

                    default:
                        controle.DefinirModoCombate(true);
                        ok = controle.EmitirOrdemMover(package.TargetPoint);
                        if (ok && package.TargetTransform != null)
                        {
                            controle.DefinirAlvoPrioritario(package.TargetTransform);
                        }
                        break;
                }

                if (!ok)
                {
                    continue;
                }

                if (package.MaintainCombatMode)
                {
                    controle.DefinirModoCombate(true);
                }
                issued++;
            }

            if (issued <= 0)
            {
                reason = "ordens_recusadas";
            }
            return issued;
        }

        private bool TryResolveRole(AISovereignCatalogRole role, out DadosConstrucao data)
        {
            data = null;
            RefreshCatalogIfNeeded();

            foreach (DadosConstrucao item in _catalog)
            {
                if (item == null || item.PrefabDaUnidade == null)
                {
                    continue;
                }

                if (MatchesRole(role, item))
                {
                    data = item;
                    return true;
                }
            }

            return false;
        }

        private void RefreshCatalogIfNeeded()
        {
            if (Time.unscaledTime < _nextCatalogRefreshTime && _catalog.Count > 0)
            {
                return;
            }

            _nextCatalogRefreshTime = Time.unscaledTime + 15f;
            _catalog.Clear();
            _catalogByKey.Clear();

            if (MenuConstrucao.catalogoGlobal != null)
            {
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                {
                    AddCatalogItem(MenuConstrucao.catalogoGlobal[i]);
                }
            }

            DadosConstrucao[] fallback = Resources.LoadAll<DadosConstrucao>(string.Empty);
            for (int i = 0; i < fallback.Length; i++)
            {
                AddCatalogItem(fallback[i]);
            }
        }

        private void AddCatalogItem(DadosConstrucao item)
        {
            if (item == null)
            {
                return;
            }

            if (!_catalog.Contains(item))
            {
                _catalog.Add(item);
            }

            string key = item.GetStableId();
            if (!string.IsNullOrWhiteSpace(key) && !_catalogByKey.ContainsKey(key))
            {
                _catalogByKey.Add(key, item);
            }
        }

        private static bool MatchesRole(AISovereignCatalogRole role, DadosConstrucao item)
        {
            string normalized = IA_Text.Normalize(item.GetDisplayName() + " " + item.name + " " + item.PrefabDaUnidade.name);
            switch (role)
            {
                case AISovereignCatalogRole.Core:
                    return item.HasCapability(IA_ConstructionCapability.Core) || normalized.Contains("prefeitura") || normalized.Contains("capital");
                case AISovereignCatalogRole.Barracks:
                    return item.HasCapability(IA_ConstructionCapability.Barracks) || normalized.Contains("quartel") || normalized.Contains("tenda");
                case AISovereignCatalogRole.Factory:
                    return item.HasCapability(IA_ConstructionCapability.Factory) || normalized.Contains("fabrica") || normalized.Contains("construtor");
                case AISovereignCatalogRole.Warehouse:
                    return item.HasCapability(IA_ConstructionCapability.Warehouse) || normalized.Contains("armazem");
                case AISovereignCatalogRole.Radar:
                    return item.HasCapability(IA_ConstructionCapability.Radar) || normalized.Contains("radar");
                case AISovereignCatalogRole.Ciws:
                    return item.HasCapability(IA_ConstructionCapability.Defense) && normalized.Contains("ciws");
                case AISovereignCatalogRole.Turret:
                    return item.HasCapability(IA_ConstructionCapability.Defense) && (normalized.Contains("torreta") || normalized.Contains("sentinela") || normalized.Contains("antia"));
                case AISovereignCatalogRole.Airport:
                    return item.HasCapability(IA_ConstructionCapability.MilitaryAirport) || (item.HasCapability(IA_ConstructionCapability.Airport) && !item.HasCapability(IA_ConstructionCapability.Heliport));
                case AISovereignCatalogRole.Shipyard:
                    return item.HasCapability(IA_ConstructionCapability.Shipyard) || normalized.Contains("estaleiro");
                case AISovereignCatalogRole.Platform:
                    return item.HasCapability(IA_ConstructionCapability.Platform) || normalized.Contains("plataforma");
                case AISovereignCatalogRole.Fighter:
                    return item.HasCapability(IA_ConstructionCapability.FighterAircraft) || normalized.Contains("caca") || normalized.Contains("fighter");
                case AISovereignCatalogRole.NavalPatrol:
                    return item.HasCapability(IA_ConstructionCapability.Naval) && item.HasCapability(IA_ConstructionCapability.Unit) && !item.HasCapability(IA_ConstructionCapability.NavalTransport);
                case AISovereignCatalogRole.NavalTransport:
                    return item.HasCapability(IA_ConstructionCapability.NavalTransport) || normalized.Contains("transporte");
                case AISovereignCatalogRole.Carrier:
                    return normalized.Contains("porta avioes") || normalized.Contains("carrier");
                case AISovereignCatalogRole.OilShip:
                    return item.HasCapability(IA_ConstructionCapability.OilTanker) || normalized.Contains("petroleiro") || normalized.Contains("tanker");
                case AISovereignCatalogRole.Power:
                    return item.HasCapability(IA_ConstructionCapability.Power) || normalized.Contains("energia") || normalized.Contains("usina");
                case AISovereignCatalogRole.Farm:
                    return normalized.Contains("fazenda") || normalized.Contains("farm") || normalized.Contains("comida");
                default:
                    return false;
            }
        }

        private bool TryProduceViaInfrastructure(DadosConstrucao data, out GameObject produced)
        {
            produced = null;

            string normalized = IA_Text.Normalize(data.GetDisplayName() + " " + data.name + " " + data.PrefabDaUnidade.name);
            if (data.HasCapability(IA_ConstructionCapability.Naval) && !data.HasCapability(IA_ConstructionCapability.Structure))
            {
                RegistroEntidadesJogo.FillEstaleiros(_shipyards);
                for (int i = 0; i < _shipyards.Count; i++)
                {
                    Estaleiro shipyard = _shipyards[i];
                    if (shipyard == null || !BelongsToTeam(shipyard.gameObject))
                    {
                        continue;
                    }

                    if (shipyard.ConstruirUnidade(data.PrefabDaUnidade))
                    {
                        return true;
                    }
                }

            }

            if (data.HasCapability(IA_ConstructionCapability.Aircraft) || data.HasCapability(IA_ConstructionCapability.FighterAircraft) || normalized.Contains("aviao"))
            {
                RegistroEntidadesJogo.FillAeroportos(_airports);
                for (int i = 0; i < _airports.Count; i++)
                {
                    GerenciadorAeroporto airport = _airports[i];
                    if (airport == null || !BelongsToTeam(airport.gameObject))
                    {
                        continue;
                    }

                    airport.ComprarAviao(data.PrefabDaUnidade);
                    return true;
                }

                RegistroEntidadesJogo.FillHeliportos(_heliports);
                for (int i = 0; i < _heliports.Count; i++)
                {
                    Heliporto heliport = _heliports[i];
                    if (heliport == null || !BelongsToTeam(heliport.gameObject) || !heliport.TemEspacoParaPousar())
                    {
                        continue;
                    }

                    produced = Object.Instantiate(data.PrefabDaUnidade, heliport.ObterPontoDePousoMundial(), heliport.transform.rotation);
                    return produced != null;
                }
            }

            RegistroEntidadesJogo.FillFabricas(_factories);
            for (int i = 0; i < _factories.Count; i++)
            {
                Fabrica factory = _factories[i];
                if (factory == null || !BelongsToTeam(factory.gameObject))
                {
                    continue;
                }

                produced = factory.ProduzirUnidade(data.PrefabDaUnidade);
                if (produced != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveBuildPose(DadosConstrucao data, AISovereignPerception perception, ref Vector3 position, ref Quaternion rotation, out string reason)
        {
            reason = string.Empty;
            if (data == null || data.PrefabDaUnidade == null)
            {
                reason = "prefab_invalido";
                return false;
            }

            if (position == Vector3.zero && perception != null)
            {
                position = perception.BaseCenter;
            }

            if (position == Vector3.zero)
            {
                reason = "sem_base_propria_valida";
                return false;
            }

            if (RequiresCoastalPlacement(data))
            {
                if (!NavalPlacementResolver.TryResolveStructurePose(data.PrefabDaUnidade, position, rotation, out NavalPlacementResolver.StructurePose pose))
                {
                    reason = string.IsNullOrEmpty(pose.Reason) ? "costa_invalida" : pose.Reason;
                    return false;
                }

                position = pose.Position;
                rotation = pose.Rotation;
                return true;
            }

            if (IsNavalStructure(data))
            {
                if (!NavalPlacementResolver.TryResolveWaterSpawn(position, Vector3.forward, 0f, 60f, out Vector3 spawnPoint, out _, out reason))
                {
                    return false;
                }

                position = spawnPoint;
                return true;
            }

            Vector3 rayStart = new Vector3(position.x, position.y + 500f, position.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1200f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                return true;
            }

            return true;
        }

        private bool TryValidateBuildTerritory(
            DadosConstrucao data,
            AISovereignCatalogRole role,
            Vector3 position,
            out string reason)
        {
            reason = string.Empty;
            int playerTeam = SistemaGovernoMundial.Instancia != null
                ? SistemaGovernoMundial.Instancia.teamJogador
                : 1;

            if (_teamId <= 0 || _teamId == playerTeam)
            {
                reason = "team_ia_invalido_ou_jogador";
                return false;
            }

            GerenteDeTerritorio territory = EnsureTerritoryManager();
            if (territory == null)
            {
                reason = "gerente_territorio_aguardando";
                return false;
            }

            int owner = territory.ObterDonoDoPonto(position);
            if (owner == _teamId)
            {
                return true;
            }

            // Bandeiras/marcadores continuam sendo o mecanismo de expansao.
            // Estruturas comuns nao podem usar essa excecao.
            if (owner == 0 && IsTerritoryExpansionMarker(data))
            {
                return true;
            }

            // Pontos de agua nao recebem dono geometrico. Para nao quebrar
            // estaleiros/plataformas, exigimos uma fronteira terrestre propria
            // proxima; isso nao libera construcao em costa neutra distante.
            if (owner == 0 && IsCoastalStructure(data, role) && HasFriendlyTerritoryNearby(territory, _teamId, position, 420f))
            {
                return true;
            }

            reason = owner == 0
                ? "territorio_nao_reivindicado"
                : (owner == playerTeam ? "territorio_do_jogador" : "territorio_de_outra_ia_" + owner);
            return false;
        }

        private static GerenteDeTerritorio EnsureTerritoryManager()
        {
            if (GerenteDeTerritorio.Instancia != null)
            {
                return GerenteDeTerritorio.Instancia;
            }

            GerenteDeTerritorio existing = Object.FindFirstObjectByType<GerenteDeTerritorio>();
            if (existing != null)
            {
                return existing;
            }

            GameObject managerObject = new GameObject("GerenteDeTerritorio_Sistema");
            return managerObject.AddComponent<GerenteDeTerritorio>();
        }

        private static bool IsTerritoryExpansionMarker(DadosConstrucao data)
        {
            if (data == null || data.PrefabDaUnidade == null)
            {
                return false;
            }

            string text = IA_Text.Normalize(data.GetDisplayName() + " " + data.PrefabDaUnidade.name);
            return text.Contains("bandeira")
                || text.Contains("flag")
                || data.PrefabDaUnidade.GetComponent<MarcadorTerritorio>() != null;
        }

        private static bool IsCoastalStructure(DadosConstrucao data, AISovereignCatalogRole role)
        {
            return role == AISovereignCatalogRole.Shipyard
                || role == AISovereignCatalogRole.Platform
                || (data != null && (data.HasCapability(IA_ConstructionCapability.Shipyard)
                    || data.HasCapability(IA_ConstructionCapability.Pier)
                    || data.HasCapability(IA_ConstructionCapability.Platform)));
        }

        private static bool HasFriendlyTerritoryNearby(GerenteDeTerritorio territory, int teamId, Vector3 center, float maxRadius)
        {
            if (territory == null)
            {
                return false;
            }

            float[] radii = { 0f, 32f, 72f, 128f, 192f, 256f, 320f, 420f };
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                if (radius > maxRadius)
                {
                    continue;
                }

                int samples = radius <= 0.01f ? 1 : 16;
                for (int i = 0; i < samples; i++)
                {
                    Vector3 probe = center;
                    if (samples > 1)
                    {
                        float angle = ((360f / samples) * i) * Mathf.Deg2Rad;
                        probe += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    }

                    if (territory.ObterDonoDoPonto(probe) == teamId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool RequiresCoastalPlacement(DadosConstrucao data)
        {
            return data.HasCapability(IA_ConstructionCapability.Shipyard)
                   || data.HasCapability(IA_ConstructionCapability.Pier)
                   || IA_Text.Normalize(data.GetDisplayName()).Contains("estaleiro")
                   || IA_Text.Normalize(data.GetDisplayName()).Contains("pier");
        }

        private static bool IsNavalStructure(DadosConstrucao data)
        {
            return data.HasCapability(IA_ConstructionCapability.Platform)
                   || IA_Text.Normalize(data.GetDisplayName()).Contains("plataforma");
        }

        private static float ResolveStructureSpacing(AISovereignCatalogRole role, DadosConstrucao data)
        {
            switch (role)
            {
                case AISovereignCatalogRole.Airport:
                    return 220f;
                case AISovereignCatalogRole.Core:
                    return 80f;
                case AISovereignCatalogRole.Factory:
                case AISovereignCatalogRole.Shipyard:
                    return 70f;
                case AISovereignCatalogRole.Warehouse:
                    return 55f;
                default:
                    return data != null && data.HasCapability(IA_ConstructionCapability.Platform) ? 120f : 42f;
            }
        }

        private static int AdjustUnitsForSeverity(int baseValue, AISovereignSeverity severity)
        {
            switch (severity)
            {
                case AISovereignSeverity.Emergency:
                    return Mathf.Max(1, baseValue / 3);
                case AISovereignSeverity.Throttled:
                    return Mathf.Max(1, baseValue / 2);
                case AISovereignSeverity.Watch:
                    return Mathf.Max(1, baseValue - 1);
                default:
                    return Mathf.Max(1, baseValue);
            }
        }

        private List<GameObject> SelectUnitsForDomain(AISovereignDomain domain, AISovereignPerception perception, int maxUnits)
        {
            var selected = new List<GameObject>(maxUnits);
            IReadOnlyList<GameObject> source = perception.OwnUnits;
            for (int i = 0; i < source.Count && selected.Count < maxUnits; i++)
            {
                GameObject unit = source[i];
                if (unit == null)
                {
                    continue;
                }

                IdentidadeUnidade id = unit.GetComponent<IdentidadeUnidade>();
                if (id == null)
                {
                    continue;
                }

                if (domain == AISovereignDomain.Air && id.tipoUnidade != TipoUnidade.Aereo) continue;
                if (domain == AISovereignDomain.Naval && id.tipoUnidade != TipoUnidade.Naval) continue;
                if (domain == AISovereignDomain.Land && id.tipoUnidade == TipoUnidade.Aereo) continue;
                if (domain == AISovereignDomain.Land && id.tipoUnidade == TipoUnidade.Naval) continue;

                selected.Add(unit);
            }

            return selected;
        }

        private bool TryIssuePatrolOrRecon(ControleUnidade controle, AICombatPackage package)
        {
            if (controle == null)
            {
                return false;
            }

            Vector3 start = controle.transform.position;
            Vector3 end = package.TargetPoint == Vector3.zero ? package.StagingPoint : package.TargetPoint;
            if (end == Vector3.zero)
            {
                end = start + controle.transform.forward * 120f;
            }

            var rota = new List<Vector3>(2)
            {
                start,
                end
            };
            return controle.EmitirOrdemPatrulha(rota);
        }

        private bool TryIssueAmphibious(ControleUnidade controle, GameObject unit, AICombatPackage package)
        {
            if (controle == null)
            {
                return false;
            }

            if (unit.GetComponent<NavioTransporteTropas>() != null || unit.GetComponent<TransporteAnfibio>() != null)
            {
                bool moved = controle.EmitirMissaoNavalOfensiva(package.TargetPoint, package.TargetTransform, true, true);
                if (moved && DistanceFlat(unit.transform.position, package.TargetPoint) <= 110f)
                {
                    unit.SendMessage("DesembarcarTudo", SendMessageOptions.DontRequireReceiver);
                }
                return moved;
            }

            return controle.EmitirOrdemMover(package.StagingPoint != Vector3.zero ? package.StagingPoint : package.TargetPoint);
        }

        private bool TryFindRelaxedPoint(Vector3 center, float radius, out Vector3 point)
        {
            point = center;
            float[] rings = { radius * 0.45f, radius * 0.80f, radius * 1.10f };
            for (int r = 0; r < rings.Length; r++)
            {
                float ring = rings[r];
                for (int i = 0; i < 8; i++)
                {
                    float angle = (45f * i) * Mathf.Deg2Rad;
                    Vector3 probe = center + new Vector3(Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring);
                    if (!IsCrowded(probe, radius, true))
                    {
                        point = probe;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsCrowded(Vector3 position, float radius, bool structuresOnly)
        {
            int count = Physics.OverlapSphereNonAlloc(position, Mathf.Max(3f, radius), _spacingHits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _spacingHits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                IdentidadeUnidade id = hit.GetComponentInParent<IdentidadeUnidade>();
                if (id == null)
                {
                    continue;
                }

                if (structuresOnly && id.tipoUnidade != TipoUnidade.Estrutura)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private Vector3 ResolveFallbackSpawnPoint(AISovereignPerception perception)
        {
            Vector3 anchor = perception != null && perception.BaseCenter != Vector3.zero ? perception.BaseCenter : Vector3.zero;
            if (anchor == Vector3.zero)
            {
                RegistroEntidadesJogo.FillUnidades(_unitBuffer);
                for (int i = 0; i < _unitBuffer.Count; i++)
                {
                    IdentidadeUnidade id = _unitBuffer[i];
                    if (id != null && id.teamID == _teamId)
                    {
                        anchor = id.transform.position;
                        break;
                    }
                }
            }

            if (anchor == Vector3.zero)
            {
                anchor = new Vector3(_teamId * 40f, 0f, _teamId * 40f);
            }

            return anchor + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f));
        }

        private bool BelongsToTeam(GameObject obj)
        {
            return IA_SharedRuntimeSupport.BelongsToTeam(obj, _teamId);
        }

        private void EnsureIdentity(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            IdentidadeUnidade id = instance.GetComponent<IdentidadeUnidade>();
            if (id == null)
            {
                id = instance.AddComponent<IdentidadeUnidade>();
            }

            id.teamID = _teamId;
            instance.SetActive(true);
        }

        private static float DistanceFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
