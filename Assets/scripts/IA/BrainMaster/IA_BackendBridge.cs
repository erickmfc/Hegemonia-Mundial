using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_BackendBridge
    {
        private readonly int _teamId;
        private readonly Dictionary<string, DadosConstrucao> _catalogByKey = new Dictionary<string, DadosConstrucao>();
        private readonly List<DadosConstrucao> _catalog = new List<DadosConstrucao>();

        public BuildService BuildService { get; private set; }
        public ProductionService ProductionService { get; private set; }
        public SquadService SquadService { get; private set; }
        public AbilityService AbilityService { get; private set; }
        public CommandService CommandService { get; private set; }

        public IA_BackendBridge(int teamId)
        {
            _teamId = teamId;
            BuildService = new BuildService(this, _teamId);
            ProductionService = new ProductionService(this, _teamId);
            SquadService = new SquadService();
            AbilityService = new AbilityService();
            CommandService = new CommandService(this);
        }

        public void RefreshCatalog()
        {
            _catalog.Clear();
            _catalogByKey.Clear();

            if (MenuConstrucao.catalogoGlobal != null)
            {
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                {
                    AddCatalogItem(MenuConstrucao.catalogoGlobal[i]);
                }
            }

            var fallback = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
            for (int i = 0; i < fallback.Length; i++)
            {
                AddCatalogItem(fallback[i]);
            }

#if UNITY_EDITOR
            if (_catalog.Count == 0)
            {
                string[] preferredFolders = { "Assets/Prefabs", "Assets/Resources" };
                var existingFolders = new List<string>();
                for (int i = 0; i < preferredFolders.Length; i++)
                {
                    if (AssetDatabase.IsValidFolder(preferredFolders[i]))
                    {
                        existingFolders.Add(preferredFolders[i]);
                    }
                }

                string[] guids = existingFolders.Count > 0
                    ? AssetDatabase.FindAssets("t:DadosConstrucao", existingFolders.ToArray())
                    : AssetDatabase.FindAssets("t:DadosConstrucao");

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    DadosConstrucao asset = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
                    AddCatalogItem(asset);
                }
            }
#endif

            if (_catalog.Count == 0)
            {
                Debug.LogWarning("[IA_BackendBridge] Catalogo de DadosConstrucao vazio. A IA nao conseguira construir nem produzir.");
            }
        }

        public IReadOnlyList<DadosConstrucao> Catalog
        {
            get { return _catalog; }
        }

        public bool TryResolveItem(string key, out DadosConstrucao item)
        {
            item = null;
            if (_catalogByKey.Count == 0)
            {
                RefreshCatalog();
            }

            string normalized = IA_Text.Normalize(key);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            string canonical = CanonicalizeKey(normalized);

            if (_catalogByKey.TryGetValue(normalized, out item))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(canonical) && _catalogByKey.TryGetValue(canonical, out item))
            {
                return true;
            }

            for (int i = 0; i < _catalog.Count; i++)
            {
                DadosConstrucao candidate = _catalog[i];
                if (candidate == null)
                {
                    continue;
                }

                string byName = IA_Text.Normalize(candidate.nomeItem);
                string byAsset = IA_Text.Normalize(candidate.name);
                string byPrefab = candidate.prefabDaUnidade != null ? IA_Text.Normalize(candidate.prefabDaUnidade.name) : string.Empty;
                if (byName.Contains(normalized)
                    || normalized.Contains(byName)
                    || byAsset.Contains(normalized)
                    || byPrefab.Contains(normalized)
                    || (!string.IsNullOrEmpty(canonical)
                        && (byName.Contains(canonical) || byAsset.Contains(canonical) || byPrefab.Contains(canonical))))
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        public DadosConstrucao FindFirstAvailable(params string[] keys)
        {
            if (keys == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                DadosConstrucao data;
                if (TryResolveItem(keys[i], out data))
                {
                    return data;
                }
            }

            return null;
        }

        public bool BelongsToTeam(Component component)
        {
            if (component == null)
            {
                return false;
            }

            IdentidadeUnidade id = component.GetComponent<IdentidadeUnidade>();
            if (id == null)
            {
                id = component.GetComponentInParent<IdentidadeUnidade>();
            }

            return id != null && id.teamID == _teamId;
        }

        public void EnsureIdentity(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            IdentidadeUnidade id = obj.GetComponent<IdentidadeUnidade>();
            if (id == null)
            {
                id = obj.AddComponent<IdentidadeUnidade>();
            }

            id.teamID = _teamId;
        }

        private void AddCatalogItem(DadosConstrucao item)
        {
            if (item == null || item.prefabDaUnidade == null)
            {
                return;
            }

            if (_catalog.Contains(item))
            {
                return;
            }

            _catalog.Add(item);
            AddKey(IA_Text.Normalize(item.nomeItem), item);
            AddKey(IA_Text.Normalize(item.name), item);
            AddKey(IA_Text.Normalize(item.prefabDaUnidade.name), item);

            string[] aliases = BuildAliases(item);
            for (int i = 0; i < aliases.Length; i++)
            {
                AddKey(aliases[i], item);
            }
        }

        private void AddKey(string key, DadosConstrucao item)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!_catalogByKey.ContainsKey(key))
            {
                _catalogByKey.Add(key, item);
            }
        }

        private static string CanonicalizeKey(string key)
        {
            string normalized = IA_Text.Normalize(key);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (normalized.Contains("prefeitura") || normalized.Contains("governo") || normalized.Contains("capital"))
            {
                return "prefeitura";
            }

            if (normalized.Contains("quartel general") || normalized.Contains("quartel_general") || normalized == "hq")
            {
                return "quartel general";
            }

            if (normalized == "quartel")
            {
                return "tenda militar";
            }

            if (normalized.Contains("tenda") || normalized.Contains("barraca") || normalized.Contains("infantaria"))
            {
                return "tenda militar";
            }

            if (normalized.Contains("construtor de veiculos") || normalized.Contains("fabrica") || normalized.Contains("factory"))
            {
                return "construtor de veiculos";
            }

            if (normalized.Contains("armazem") || normalized.Contains("galpao"))
            {
                return "armazem";
            }

            if (normalized.Contains("radar"))
            {
                return "torre de radar";
            }

            if (normalized.Contains("torreta") || normalized.Contains("sentinela"))
            {
                return "torreta";
            }

            if (normalized.Contains("ciws") || normalized.Contains("phalanx") || normalized.Contains("antia"))
            {
                return "ciws";
            }

            if (normalized.Contains("muro") || normalized.Contains("wall"))
            {
                return "muro de concreto";
            }

            if (normalized.Contains("estaleiro"))
            {
                return "estaleiro naval";
            }

            if (normalized.Contains("aeroporto")
                || normalized.Contains("airport")
                || normalized.Contains("base aerea")
                || normalized.Contains("pista"))
            {
                return "aeroporto";
            }

            if (normalized.Contains("heliporto"))
            {
                return "heliporto";
            }

            if (normalized.Contains("plataforma"))
            {
                return "plataforma";
            }

            if (normalized.Contains("lancador") || normalized.Contains("missil") || normalized.Contains("silo"))
            {
                return "lancador de misseis";
            }

            return normalized;
        }

        private static string[] BuildAliases(DadosConstrucao item)
        {
            List<string> aliases = new List<string>();
            string joined = IA_Text.Normalize(item.nomeItem + " " + item.name + " " + item.prefabDaUnidade.name);
            bool isAirport = item.prefabDaUnidade.GetComponent<GerenciadorAeroporto>() != null;
            bool isHeliport = item.prefabDaUnidade.GetComponent<Heliporto>() != null;

            if (joined.Contains("prefeitura"))
            {
                aliases.Add("prefeitura");
                aliases.Add("governo");
                aliases.Add("capital");
            }

            if (joined.Contains("quartel_general") || joined.Contains("quartel general"))
            {
                aliases.Add("quartel general");
                aliases.Add("quartel_general");
                aliases.Add("hq");
            }

            if (joined.Contains("tenda"))
            {
                aliases.Add("tenda militar");
                aliases.Add("tenda");
                aliases.Add("barraca");
                aliases.Add("infantaria");
            }

            if (joined.Contains("construtor de veiculos"))
            {
                aliases.Add("construtor de veiculos");
                aliases.Add("fabrica");
                aliases.Add("factory");
            }

            if (joined.Contains("armazem") || joined.Contains("armazem_recursos"))
            {
                aliases.Add("armazem");
                aliases.Add("galpao");
            }

            if (joined.Contains("radar"))
            {
                aliases.Add("radar");
                aliases.Add("torre de radar");
            }

            if (joined.Contains("torreta") || joined.Contains("sentinela") || joined.Contains("artilharia"))
            {
                aliases.Add("torreta");
                aliases.Add("torre sentinela");
                aliases.Add("sentinela");
            }

            if (joined.Contains("ciws"))
            {
                aliases.Add("ciws");
                aliases.Add("phalanx");
                aliases.Add("antia");
            }

            if (joined.Contains("muro"))
            {
                aliases.Add("muro");
                aliases.Add("muro de concreto");
                aliases.Add("wall");
            }

            if (joined.Contains("estaleiro"))
            {
                aliases.Add("estaleiro");
                aliases.Add("estaleiro naval");
            }

            if (isAirport || joined.Contains("aeroporto") || joined.Contains("airport") || joined.Contains("pista"))
            {
                aliases.Add("aeroporto");
                aliases.Add("airport");
                aliases.Add("base aerea");
                aliases.Add("pista");
            }

            if (isHeliport || joined.Contains("heliporto"))
            {
                aliases.Add("heliporto");
            }

            if (joined.Contains("plataforma"))
            {
                aliases.Add("plataforma");
            }

            if (joined.Contains("lancador") || joined.Contains("misseis"))
            {
                aliases.Add("lancador");
                aliases.Add("lancador de misseis");
                aliases.Add("missil");
                aliases.Add("silo");
            }

            return aliases.ToArray();
        }
    }

    public sealed class BuildService
    {
        private struct BuildReservation
        {
            public Vector3 Position;
            public float Radius;
            public float Until;
        }

        private readonly IA_BackendBridge _bridge;
        private readonly int _teamId;
        private readonly List<BuildReservation> _reservations = new List<BuildReservation>();

        public BuildService(IA_BackendBridge bridge, int teamId)
        {
            _bridge = bridge;
            _teamId = teamId;
        }

        public bool ValidatePlacement(
            string itemKey,
            Vector3 position,
            IA_ZoneType zone,
            IA_WorldState world,
            IA_MapAnalyzer map,
            IA_ThreatAnalyzer threat,
            out string reason)
        {
            return ValidatePlacement(itemKey, position, Quaternion.identity, zone, world, map, threat, out reason);
        }

        public bool ValidatePlacement(
            string itemKey,
            Vector3 position,
            Quaternion requestedRotation,
            IA_ZoneType zone,
            IA_WorldState world,
            IA_MapAnalyzer map,
            IA_ThreatAnalyzer threat,
            out string reason)
        {
            reason = string.Empty;

            DadosConstrucao data;
            if (!_bridge.TryResolveItem(itemKey, out data))
            {
                reason = "item nao encontrado";
                return false;
            }

            Quaternion resolvedRotation = requestedRotation;
            if (RequiresCoastalPlacement(data))
            {
                NavalPlacementResolver.StructurePose pose;
                if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, position, resolvedRotation, out pose))
                {
                    reason = string.IsNullOrEmpty(pose.Reason) ? "costa invalida" : pose.Reason;
                    return false;
                }

                position = pose.Position;
                resolvedRotation = pose.Rotation;
                if (!ValidateCoastalPlacementOnMap(data.prefabDaUnidade, position, resolvedRotation, map, out reason))
                {
                    return false;
                }
            }

            if (!ValidateTerritoryRules(data, position, out reason))
            {
                return false;
            }

            CleanupReservations(Time.time);

            IA_MapCell cell = map.SampleCell(position);
            bool isNaval = IsNaval(data);
            bool directNavalTerrainAccepted = false;
            if (isNaval && cell.Terrain != IA_TerrainType.Water && cell.Terrain != IA_TerrainType.Coast)
            {
                directNavalTerrainAccepted = IsNavalPlacementAcceptedDirectly(data, position);
                if (!directNavalTerrainAccepted)
                {
                    reason = "terreno invalido";
                    return false;
                }
            }

            if (!isNaval && cell.Terrain == IA_TerrainType.Water)
            {
                reason = "terreno invalido";
                return false;
            }

            if (!isNaval && (!cell.BuildableLand || (RequiresDryLand(data, zone) && cell.Terrain == IA_TerrainType.Coast)))
            {
                reason = "terreno seco invalido";
                return false;
            }

            if (!directNavalTerrainAccepted && !map.IsZoneCompatible(zone, cell.Terrain, isNaval))
            {
                reason = "zona invalida";
                return false;
            }

            if (isNaval)
            {
                float minEdgeDistance = RequiresCoastalPlacement(data) ? 145f : 105f;
                if (NavalPlacementResolver.IsNearMapEdge(position, minEdgeDistance))
                {
                    reason = "muito perto da borda do mapa";
                    return false;
                }
            }

            Vector2 halfExtents = map.EstimateFootprint(data.prefabDaUnidade, 10f);
            if (!IsFootprintFree(position, halfExtents))
            {
                reason = "footprint ocupado";
                return false;
            }

            if (RequiresDryLand(data, zone) && !IsDryLandFootprintValid(position, halfExtents, map, data, zone))
            {
                reason = "footprint invade agua ou costa";
                return false;
            }

            float spacingRadius = Mathf.Max(Mathf.Max(12f, halfExtents.magnitude), GetMinimumStructureSpacing(data, zone));
            if (!HasMinimumDistance(position, world, spacingRadius))
            {
                reason = "distancia minima violada";
                return false;
            }

            if (IsAirport(data, zone) && !HasAirportClearance(position, world, Mathf.Max(220f, spacingRadius + 60f)))
            {
                reason = "aeroporto muito perto do nucleo ou de imoveis";
                return false;
            }

            if (IsReserved(position, Mathf.Max(10f, spacingRadius)))
            {
                reason = "area reservada";
                return false;
            }

            if (!isNaval && map.WouldBlockRoute(position, halfExtents, world.BaseCenter))
            {
                reason = "bloqueio de rota";
                return false;
            }

            float threatScore = threat.EvaluateThreat(position, isNaval ? IA_Domain.Naval : IA_Domain.Land);
            float maxThreat = IsDefense(data) ? 220f : 95f;
            if (threatScore > maxThreat)
            {
                reason = "seguranca tatica insuficiente";
                return false;
            }

            return true;
        }

        public bool TryBuild(
            string itemKey,
            Vector3 position,
            Quaternion rotation,
            IA_ZoneType zone,
            IA_WorldState world,
            IA_MapAnalyzer map,
            IA_ThreatAnalyzer threat,
            out GameObject created,
            out string reason)
        {
            created = null;
            DadosConstrucao data;
            if (!_bridge.TryResolveItem(itemKey, out data))
            {
                reason = "item nao encontrado";
                return false;
            }

            if (RequiresCoastalPlacement(data))
            {
                NavalPlacementResolver.StructurePose pose;
                if (!NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, position, rotation, out pose))
                {
                    reason = string.IsNullOrEmpty(pose.Reason) ? "costa invalida" : pose.Reason;
                    return false;
                }

                position = pose.Position;
                rotation = pose.Rotation;
            }

            if (!ValidatePlacement(itemKey, position, rotation, zone, world, map, threat, out reason))
            {
                return false;
            }

            Construtor construtor = Construtor.Instancia != null ? Construtor.Instancia : Object.FindFirstObjectByType<Construtor>();
            if (construtor != null)
            {
                created = construtor.ConstruirEstruturaIA(data.prefabDaUnidade, position, rotation);
            }
            else
            {
                created = Object.Instantiate(data.prefabDaUnidade, position, rotation);
            }

            if (created == null)
            {
                reason = "falha ao instanciar";
                return false;
            }

            Estaleiro estaleiro = created.GetComponent<Estaleiro>();
            if (estaleiro != null)
            {
                estaleiro.AtualizarReferenciasLitoraneas();
            }

            _bridge.EnsureIdentity(created);
            float reserveRadius = Mathf.Max(
                map.EstimateFootprint(data.prefabDaUnidade, 12f).magnitude,
                GetMinimumStructureSpacing(data, zone));
            Reserve(position, reserveRadius, Time.time + 20f);
            world.MarkDirty();
            return true;
        }

        public bool ValidateTerritoryProbe(string itemKey, Vector3 position, out string reason)
        {
            reason = string.Empty;

            DadosConstrucao data;
            if (!_bridge.TryResolveItem(itemKey, out data) || data == null)
            {
                reason = "item nao encontrado";
                return false;
            }

            return ValidateTerritoryRules(data, position, out reason);
        }

        private bool IsNaval(DadosConstrucao data)
        {
            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return data.categoria == DadosConstrucao.CategoriaItem.Marinha
                   || joined.Contains("nav")
                   || joined.Contains("sub")
                   || joined.Contains("pier")
                   || joined.Contains("estaleiro")
                   || joined.Contains("plataforma");
        }

        private static bool RequiresCoastalPlacement(DadosConstrucao data)
        {
            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("estaleiro") || joined.Contains("pier");
        }

        private static bool IsNavalPlacementAcceptedDirectly(DadosConstrucao data, Vector3 position)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
            Vector3 probe = new Vector3(position.x, seaLevel, position.z);
            if (RequiresCoastalPlacement(data))
            {
                NavalPlacementResolver.StructurePose pose;
                return NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, probe, Quaternion.identity, out pose);
            }

            if (NavalPlacementResolver.IsWaterAtPosition(probe, seaLevel))
            {
                return true;
            }

            Vector3 waterPoint;
            string reason;
            return NavalPlacementResolver.TryResolveWaterSpawn(probe, Vector3.forward, 0f, 36f, out waterPoint, out seaLevel, out reason);
        }

        private bool IsDefense(DadosConstrucao data)
        {
            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("torre")
                   || joined.Contains("radar")
                   || joined.Contains("ciws")
                   || joined.Contains("antia")
                   || joined.Contains("muro")
                   || joined.Contains("missil");
        }

        private bool RequiresDryLand(DadosConstrucao data, IA_ZoneType zone)
        {
            if (zone == IA_ZoneType.Air || zone == IA_ZoneType.Core || zone == IA_ZoneType.Economy || zone == IA_ZoneType.Military)
            {
                return true;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("aeroporto")
                   || joined.Contains("airport")
                   || joined.Contains("heliporto")
                   || joined.Contains("prefeitura")
                   || joined.Contains("quartel general")
                   || joined.Contains("quartel_general")
                   || joined.Contains("construtor")
                   || joined.Contains("fabrica")
                   || joined.Contains("armazem");
        }

        private bool ValidateTerritoryRules(DadosConstrucao data, Vector3 position, out string reason)
        {
            reason = string.Empty;

            GerenteDeTerritorio territory = EnsureTerritoryManager();
            if (territory == null)
            {
                return true;
            }

            int owner = territory.ObterDonoDoPonto(position);
            if (IsCityHall(data))
            {
                if (owner != 0 && owner != _teamId)
                {
                    reason = "territorio inimigo";
                    return false;
                }

                if (!territory.PodeConstruirPrefeitura(position))
                {
                    reason = "prefeitura proibida";
                    return false;
                }

                return true;
            }

            if (IsTerritoryMarker(data))
            {
                if (owner != 0 && owner != _teamId)
                {
                    reason = "jurisdicao inimiga";
                    return false;
                }

                return true;
            }

            if (IsOffshorePlatform(data) || IsNaval(data) || RequiresCoastalPlacement(data))
            {
                return ValidateCoastalTerritoryRules(position, territory, owner, out reason);
            }

            if (owner != _teamId)
            {
                reason = owner == 0 ? "territorio nao reivindicado" : "territorio inimigo";
                return false;
            }

            return true;
        }

        private bool ValidateCoastalTerritoryRules(
            Vector3 position,
            GerenteDeTerritorio territory,
            int owner,
            out string reason)
        {
            reason = string.Empty;

            if (owner == _teamId)
            {
                return true;
            }

            if (owner != 0)
            {
                reason = "jurisdicao inimiga";
                return false;
            }

            float nearestFriendlyDistance;
            float nearestEnemyDistance;
            bool hasFriendlyTerritory = TryFindNearbyTerritory(territory, position, true, 420f, out nearestFriendlyDistance);
            bool hasEnemyTerritory = TryFindNearbyTerritory(territory, position, false, 420f, out nearestEnemyDistance);

            if (hasFriendlyTerritory && (!hasEnemyTerritory || nearestFriendlyDistance + 8f < nearestEnemyDistance))
            {
                return true;
            }

            if (hasEnemyTerritory && (!hasFriendlyTerritory || nearestEnemyDistance <= nearestFriendlyDistance + 8f))
            {
                reason = "costa sob pressao inimiga | inimigo=" + Mathf.RoundToInt(nearestEnemyDistance) + "m";
                return false;
            }

            if (hasFriendlyTerritory)
            {
                reason = "costa neutra distante da nossa fronteira | costa_propria=" + Mathf.RoundToInt(nearestFriendlyDistance) + "m";
                return false;
            }

            reason = "territorio costeiro nao reivindicado | sem fronteira amiga proxima";
            return false;
        }

        private bool TryFindNearbyTerritory(
            GerenteDeTerritorio territory,
            Vector3 center,
            bool friendly,
            float maxRadius,
            out float nearestDistance)
        {
            nearestDistance = float.MaxValue;
            if (territory == null)
            {
                return false;
            }

            float[] radii = { 0f, 16f, 32f, 48f, 72f, 96f, 128f, 160f, 192f, 224f, 256f, 320f, 384f, 420f };
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

                    int probeOwner = territory.ObterDonoDoPonto(probe);
                    bool matches = friendly
                        ? probeOwner == _teamId
                        : probeOwner != 0 && probeOwner != _teamId;
                    if (!matches)
                    {
                        continue;
                    }

                    Vector3 delta = probe - center;
                    delta.y = 0f;
                    float distance = delta.magnitude;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                    }
                }
            }

            return nearestDistance < float.MaxValue;
        }

        private static GerenteDeTerritorio EnsureTerritoryManager()
        {
            if (GerenteDeTerritorio.Instancia != null)
            {
                return GerenteDeTerritorio.Instancia;
            }

            GameObject managerObject = GameObject.Find("GerenteDeTerritorio_Sistema");
            if (managerObject == null)
            {
                managerObject = new GameObject("GerenteDeTerritorio_Sistema");
            }

            GerenteDeTerritorio manager = managerObject.GetComponent<GerenteDeTerritorio>();
            if (manager == null)
            {
                manager = managerObject.AddComponent<GerenteDeTerritorio>();
            }

            return manager;
        }

        private static bool ValidateCoastalPlacementOnMap(GameObject prefab, Vector3 position, Quaternion rotation, IA_MapAnalyzer map, out string reason)
        {
            reason = string.Empty;

            if (prefab == null)
            {
                return true;
            }

            if (map == null)
            {
                return NavalPlacementResolver.IsStructurePoseValid(prefab, position, rotation, out reason);
            }

            float frontDistance;
            float backDistance;
            NavalPlacementResolver.ResolveCoastalOffsets(prefab, out frontDistance, out backDistance);

            Vector3 forward = rotation * Vector3.forward;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            forward.y = 0f;
            forward.Normalize();

            Vector3 frontProbe = position + (forward * Mathf.Max(12f, Mathf.Abs(frontDistance)));
            Vector3 backProbe = position + (forward * (backDistance > -2f ? -Mathf.Max(10f, Mathf.Abs(backDistance)) : backDistance));

            IA_MapCell frontCell = map.SampleCell(frontProbe);
            IA_MapCell backCell = map.SampleCell(backProbe);
            bool mapAccepted = false;
            string mapReason = "coast map invalido";
            if (frontCell != null && backCell != null)
            {
                bool frontWater = frontCell.Terrain == IA_TerrainType.Water || frontCell.Terrain == IA_TerrainType.Coast;
                bool backLand = backCell.Terrain != IA_TerrainType.Water && backCell.BuildableLand;
                if (!frontWater)
                {
                    mapReason = "frente sem mar";
                }
                else if (!backLand)
                {
                    mapReason = "traseira fora de terra";
                }
                else
                {
                    mapAccepted = true;
                }
            }

            bool coastalAccepted = mapAccepted;
            string poseReason = string.Empty;
            if (!coastalAccepted)
            {
                coastalAccepted = NavalPlacementResolver.IsStructurePoseValid(prefab, position, rotation, out poseReason);
            }

            if (!coastalAccepted)
            {
                reason = string.IsNullOrEmpty(poseReason) ? mapReason : poseReason;
                return false;
            }

            if (NavalPlacementResolver.IsNearMapEdge(position, 145f))
            {
                reason = "costa muito perto da borda do mapa";
                return false;
            }

            string corridorReason;
            if (!NavalPlacementResolver.HasSafeLaunchCorridor(
                position,
                forward,
                Mathf.Max(32f, Mathf.Abs(frontDistance) * 0.8f),
                Mathf.Max(185f, Mathf.Abs(frontDistance) + 150f),
                95f,
                out corridorReason))
            {
                reason = corridorReason;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsCityHall(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            if (data.prefabDaUnidade.GetComponent<ComplexoGovernamental>() != null)
            {
                return true;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("prefeitura")
                   || joined.Contains("governo")
                   || joined.Contains("capital")
                   || joined.Contains("complexo");
        }

        private static bool IsTerritoryMarker(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            if (data.prefabDaUnidade.GetComponent<MarcadorTerritorio>() != null)
            {
                return true;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("bandeira") || joined.Contains("flag");
        }

        private static bool IsOffshorePlatform(DadosConstrucao data)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("plataforma")
                   || joined.Contains("petroleo")
                   || joined.Contains("petrol");
        }

        private float GetMinimumStructureSpacing(DadosConstrucao data, IA_ZoneType zone)
        {
            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            if (joined.Contains("aeroporto") || joined.Contains("airport") || zone == IA_ZoneType.Air)
            {
                return 220f;
            }

            if (joined.Contains("prefeitura") || joined.Contains("quartel general") || joined.Contains("quartel_general") || zone == IA_ZoneType.Core)
            {
                return 80f;
            }

            if (joined.Contains("construtor") || joined.Contains("fabrica") || zone == IA_ZoneType.Military)
            {
                return 70f;
            }

            if (joined.Contains("armazem") || zone == IA_ZoneType.Economy)
            {
                return 55f;
            }

            if (joined.Contains("heliporto"))
            {
                return 65f;
            }

            return 0f;
        }

        private static bool IsIgnorableWorldCollider(string normalizedName)
        {
            return normalizedName.Contains("terrain")
                   || normalizedName.Contains("terra")
                   || normalizedName.Contains("agua")
                   || normalizedName.Contains("water")
                   || normalizedName.Contains("ocean")
                   || normalizedName.Contains("sea")
                   || normalizedName.Contains("mar")
                   || normalizedName.Contains("oceano")
                   || normalizedName.Contains("suimono")
                   || normalizedName.Contains("shore");
        }

        private bool IsFootprintFree(Vector3 position, Vector2 halfExtents)
        {
            Vector3 extents = new Vector3(Mathf.Max(2f, halfExtents.x), 10f, Mathf.Max(2f, halfExtents.y));
            Collider[] hits = Physics.OverlapBox(position, extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                string n = IA_Text.Normalize(hit.name);
                if (IsIgnorableWorldCollider(n))
                {
                    continue;
                }

                if (hit.GetComponentInParent<IdentidadeUnidade>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasMinimumDistance(Vector3 position, IA_WorldState world, float minDistance)
        {
            for (int i = 0; i < world.OwnStructures.Count; i++)
            {
                GameObject structure = world.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                float d = Vector3.Distance(Flatten(structure.transform.position), Flatten(position));
                if (d < minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsDryLandFootprintValid(Vector3 position, Vector2 halfExtents, IA_MapAnalyzer map, DadosConstrucao data, IA_ZoneType zone)
        {
            bool rejectCoast = RequiresDryLand(data, zone);
            bool airport = IsAirport(data, zone);
            float padding = airport ? 12f : 3f;
            float extentX = Mathf.Max(10f, halfExtents.x + padding);
            float extentZ = Mathf.Max(10f, halfExtents.y + padding);
            int sampleCountX = Mathf.Clamp(Mathf.CeilToInt((extentX * 2f) / (airport ? 16f : 24f)) + 1, 3, airport ? 9 : 7);
            int sampleCountZ = Mathf.Clamp(Mathf.CeilToInt((extentZ * 2f) / (airport ? 16f : 24f)) + 1, 3, airport ? 9 : 7);

            for (int ix = 0; ix < sampleCountX; ix++)
            {
                float tx = sampleCountX <= 1 ? 0.5f : ix / (float)(sampleCountX - 1);
                float offsetX = Mathf.Lerp(-extentX, extentX, tx);

                for (int iz = 0; iz < sampleCountZ; iz++)
                {
                    float tz = sampleCountZ <= 1 ? 0.5f : iz / (float)(sampleCountZ - 1);
                    float offsetZ = Mathf.Lerp(-extentZ, extentZ, tz);
                    Vector3 samplePosition = position + new Vector3(offsetX, 0f, offsetZ);

                    if (map != null)
                    {
                        IA_MapCell sample = map.SampleCell(samplePosition);
                        if (sample == null || !sample.BuildableLand)
                        {
                            return false;
                        }

                        if (sample.Terrain == IA_TerrainType.Water)
                        {
                            return false;
                        }

                        if (rejectCoast && sample.Terrain == IA_TerrainType.Coast)
                        {
                            return false;
                        }
                    }

                    if (!IsPhysicallyDryLandPoint(samplePosition, rejectCoast))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsPhysicallyDryLandPoint(Vector3 position, bool rejectCoast)
        {
            ClassificacaoSuperficieMapa classificacaoMarcada;
            float alturaMarcada;
            if (RegistroSuperficieMapa.TryClassify(position, out classificacaoMarcada, out alturaMarcada))
            {
                if (classificacaoMarcada == ClassificacaoSuperficieMapa.Agua)
                {
                    return false;
                }

                if (classificacaoMarcada == ClassificacaoSuperficieMapa.Costa)
                {
                    return !rejectCoast;
                }

                if (classificacaoMarcada == ClassificacaoSuperficieMapa.Chao)
                {
                    return true;
                }
            }

            float seaLevel = NavalPlacementResolver.ResolveSeaLevel();
            Vector3 probe = new Vector3(position.x, seaLevel, position.z);
            return !NavalPlacementResolver.IsWaterAtPosition(probe, seaLevel);
        }

        private bool HasAirportClearance(Vector3 position, IA_WorldState world, float minDistanceFromCore)
        {
            for (int i = 0; i < world.OwnStructures.Count; i++)
            {
                GameObject structure = world.OwnStructures[i];
                if (structure == null)
                {
                    continue;
                }

                string normalized = IA_Text.Normalize(structure.name);
                bool isCore = normalized.Contains("prefeitura")
                              || normalized.Contains("governo")
                              || normalized.Contains("capital")
                              || normalized.Contains("quartel general")
                              || normalized.Contains("quartel_general")
                              || normalized.Contains("hq");
                if (!isCore)
                {
                    continue;
                }

                float d = Vector3.Distance(Flatten(structure.transform.position), Flatten(position));
                if (d < minDistanceFromCore)
                {
                    return false;
                }
            }

            Imovel[] imoveis = Object.FindObjectsByType<Imovel>(FindObjectsSortMode.None);
            for (int i = 0; i < imoveis.Length; i++)
            {
                Imovel imovel = imoveis[i];
                if (imovel == null)
                {
                    continue;
                }

                float d = Vector3.Distance(Flatten(imovel.transform.position), Flatten(position));
                if (d < 200f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAirport(DadosConstrucao data, IA_ZoneType zone)
        {
            if (data == null || data.prefabDaUnidade == null)
            {
                return false;
            }

            if (zone == IA_ZoneType.Air && data.prefabDaUnidade.GetComponent<GerenciadorAeroporto>() != null)
            {
                return true;
            }

            string joined = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return joined.Contains("aeroporto")
                   || joined.Contains("airport")
                   || joined.Contains("base aerea")
                   || joined.Contains("pista");
        }

        private void Reserve(Vector3 position, float radius, float until)
        {
            _reservations.Add(new BuildReservation
            {
                Position = position,
                Radius = Mathf.Max(6f, radius),
                Until = until
            });
        }

        private bool IsReserved(Vector3 position, float radius)
        {
            for (int i = 0; i < _reservations.Count; i++)
            {
                BuildReservation reservation = _reservations[i];
                if (reservation.Until <= Time.time)
                {
                    continue;
                }

                float threshold = reservation.Radius + radius;
                if (Vector3.Distance(Flatten(reservation.Position), Flatten(position)) <= threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupReservations(float now)
        {
            for (int i = _reservations.Count - 1; i >= 0; i--)
            {
                if (_reservations[i].Until <= now)
                {
                    _reservations.RemoveAt(i);
                }
            }
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }

    public sealed class ProductionService
    {
        private readonly IA_BackendBridge _bridge;
        private readonly int _teamId;

        public ProductionService(IA_BackendBridge bridge, int teamId)
        {
            _bridge = bridge;
            _teamId = teamId;
        }

        public bool TryProduce(string itemKey, int quantity, IA_WorldState world, out int produced, out string reason)
        {
            produced = 0;
            reason = string.Empty;

            DadosConstrucao data;
            if (!_bridge.TryResolveItem(itemKey, out data))
            {
                reason = "item nao encontrado";
                return false;
            }

            for (int i = 0; i < Mathf.Max(1, quantity); i++)
            {
                GameObject unit = TryProduceSingle(data, world, out reason);
                if (unit != null)
                {
                    produced++;
                    world.MarkDirty();
                }
                else
                {
                    break;
                }
            }

            return produced > 0;
        }

        private GameObject TryProduceSingle(DadosConstrucao data, IA_WorldState world, out string reason)
        {
            reason = string.Empty;
            if (data == null || data.prefabDaUnidade == null)
            {
                reason = "prefab invalido";
                return null;
            }

            if (IsStructure(data))
            {
                reason = "item estrutural deve usar BuildService";
                return null;
            }

            if (IsNaval(data))
            {
                return ProduceNaval(data, out reason);
            }

            if (IsFighter(data))
            {
                return ProduceAircraft(data, out reason);
            }

            if (IsHelicopter(data))
            {
                return ProduceHelicopter(data, out reason);
            }

            return ProduceLandUnit(data, out reason);
        }

        private GameObject ProduceLandUnit(DadosConstrucao data, out string reason)
        {
            reason = string.Empty;
            Fabrica[] factories = Object.FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
            bool needsBarracks = IsInfantry(data);

            for (int i = 0; i < factories.Length; i++)
            {
                Fabrica factory = factories[i];
                if (factory == null || !_bridge.BelongsToTeam(factory))
                {
                    continue;
                }

                if (needsBarracks != factory.ehQuartel)
                {
                    continue;
                }

                GameObject unit = factory.ProduzirUnidade(data.prefabDaUnidade);
                if (unit != null)
                {
                    _bridge.EnsureIdentity(unit);
                    return unit;
                }
            }

            reason = "fabrica adequada nao encontrada";
            return null;
        }

        private GameObject ProduceNaval(DadosConstrucao data, out string reason)
        {
            reason = string.Empty;
            string lastReason = "estaleiro/pier indisponivel";
            Estaleiro[] estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            for (int i = 0; i < estaleiros.Length; i++)
            {
                Estaleiro e = estaleiros[i];
                if (e == null || !_bridge.BelongsToTeam(e))
                {
                    continue;
                }

                if (e.ConstruirUnidade(data.prefabDaUnidade))
                {
                    return data.prefabDaUnidade;
                }

                lastReason = "estaleiro sem agua, sem costa ou sem vaga";
            }

            PierMarinha[] piers = Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
            for (int i = 0; i < piers.Length; i++)
            {
                PierMarinha p = piers[i];
                if (p == null || !_bridge.BelongsToTeam(p))
                {
                    continue;
                }

                if (p.ConstruirNavio(data.prefabDaUnidade))
                {
                    return data.prefabDaUnidade;
                }

                lastReason = "pier sem agua ou mal posicionado";
            }

            reason = lastReason;
            return null;
        }

        private GameObject ProduceAircraft(DadosConstrucao data, out string reason)
        {
            reason = string.Empty;
            GerenciadorAeroporto[] airports = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                if (airport == null || !_bridge.BelongsToTeam(airport))
                {
                    continue;
                }

                airport.ComprarAviao(data.prefabDaUnidade);
                return data.prefabDaUnidade;
            }

            reason = "aeroporto indisponivel";
            return null;
        }

        private GameObject ProduceHelicopter(DadosConstrucao data, out string reason)
        {
            reason = string.Empty;
            Heliporto[] heliports = Object.FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
            for (int i = 0; i < heliports.Length; i++)
            {
                Heliporto heliport = heliports[i];
                if (heliport == null || !_bridge.BelongsToTeam(heliport))
                {
                    continue;
                }

                if (!heliport.TemEspacoParaPousar())
                {
                    continue;
                }

                GameObject unit = Object.Instantiate(data.prefabDaUnidade, heliport.ObterPontoDePousoMundial(), heliport.transform.rotation);
                _bridge.EnsureIdentity(unit);
                return unit;
            }

            reason = "heliporto indisponivel";
            return null;
        }

        private static bool IsStructure(DadosConstrucao data)
        {
            string n = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return data.categoria == DadosConstrucao.CategoriaItem.Infraestrutura
                   || data.categoria == DadosConstrucao.CategoriaItem.Energia
                   || data.categoria == DadosConstrucao.CategoriaItem.Tecnologia
                   || data.categoria == DadosConstrucao.CategoriaItem.Urbana
                   || n.Contains("torre")
                   || n.Contains("muro")
                   || n.Contains("estaleiro")
                   || n.Contains("plataforma")
                   || n.Contains("quartel")
                   || n.Contains("prefeitura");
        }

        private static bool IsNaval(DadosConstrucao data)
        {
            string n = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return data.categoria == DadosConstrucao.CategoriaItem.Marinha
                   || n.Contains("navio")
                   || n.Contains("sub")
                   || n.Contains("corveta")
                   || n.Contains("destroy")
                   || n.Contains("frigata")
                   || n.Contains("lancha");
        }

        private static bool IsHelicopter(DadosConstrucao data)
        {
            string n = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return data.prefabDaUnidade.GetComponent<Helicoptero>() != null
                   || n.Contains("heli")
                   || n.Contains("ray")
                   || n.Contains("vans");
        }

        private static bool IsFighter(DadosConstrucao data)
        {
            string n = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return data.prefabDaUnidade.GetComponent<ControleAviao>() != null
                   || n.Contains("caca")
                   || n.Contains("fa1")
                   || n.Contains("jet")
                   || n.Contains("aviao");
        }

        private static bool IsInfantry(DadosConstrucao data)
        {
            string n = IA_Text.Normalize(data.nomeItem + " " + data.prefabDaUnidade.name);
            return n.Contains("sold")
                   || n.Contains("infan")
                   || n.Contains("rifle")
                   || n.Contains("fuzil");
        }
    }

    public sealed class SquadService
    {
        private readonly Dictionary<string, IA_SquadData> _squads = new Dictionary<string, IA_SquadData>();

        public IA_SquadData UpsertSquad(string squadId, IA_SquadRole role, List<GameObject> units)
        {
            IA_SquadData squad;
            if (!_squads.TryGetValue(squadId, out squad))
            {
                squad = new IA_SquadData
                {
                    Id = squadId,
                    Role = role
                };
                _squads.Add(squadId, squad);
            }

            squad.Role = role;
            squad.Units.Clear();
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    GameObject unit = units[i];
                    if (unit != null && !squad.Units.Contains(unit))
                    {
                        squad.Units.Add(unit);
                    }
                }
            }

            return squad;
        }

        public List<IA_SquadData> GetAll()
        {
            return _squads.Values.ToList();
        }

        public List<IA_SquadData> GetByRole(IA_SquadRole role)
        {
            var output = new List<IA_SquadData>();
            foreach (var squad in _squads.Values)
            {
                if (squad.Role == role)
                {
                    output.Add(squad);
                }
            }

            return output;
        }

        public void CleanupDeadUnits()
        {
            foreach (var squad in _squads.Values)
            {
                squad.Units.RemoveAll(u => u == null || !u.activeInHierarchy);
            }
        }
    }

    public sealed class AbilityService
    {
        public bool TryUseAbility(IA_AbilityOrderData order, out string reason)
        {
            reason = string.Empty;
            if (order == null || order.Caster == null)
            {
                reason = "caster invalido";
                return false;
            }

            // Ponto unico para habilidades futuras, sem usar UI.
            order.Caster.SendMessage("ExecutarHabilidadeIA", order, SendMessageOptions.DontRequireReceiver);
            order.Caster.SendMessage(order.AbilityKey, SendMessageOptions.DontRequireReceiver);
            return true;
        }
    }

    public sealed class CommandService
    {
        private readonly IA_BackendBridge _bridge;
        private readonly Dictionary<int, Vector3> _lastDestinationByUnit = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> _lastOrderTimeByUnit = new Dictionary<int, float>();

        public CommandService(IA_BackendBridge bridge)
        {
            _bridge = bridge;
        }

        public bool Execute(IA_CommandRequest request, IA_Context context, out string message)
        {
            message = string.Empty;
            if (request == null)
            {
                message = "request nula";
                return false;
            }

            switch (request.Type)
            {
                case IA_CommandType.Build:
                    return ExecuteBuild(request, context, out message);
                case IA_CommandType.Produce:
                    return ExecuteProduce(request, context, out message);
                case IA_CommandType.Move:
                    return ExecuteMove(request, out message);
                case IA_CommandType.Attack:
                    return ExecuteAttack(request, out message);
                case IA_CommandType.Patrol:
                    return ExecutePatrol(request, out message);
                case IA_CommandType.Ability:
                    return ExecuteAbility(request, out message);
                default:
                    message = "tipo nao suportado";
                    return false;
            }
        }

        private bool ExecuteBuild(IA_CommandRequest request, IA_Context context, out string message)
        {
            IA_BuildOrderData payload = request.Payload as IA_BuildOrderData;
            if (payload == null)
            {
                message = "payload invalido";
                return false;
            }

            DadosConstrucao item;
            if (!_bridge.TryResolveItem(payload.ItemKey, out item))
            {
                message = "item nao encontrado";
                return false;
            }

            if (!context.Brain.TrySpend(item.preco))
            {
                message = "credito insuficiente";
                return false;
            }

            GameObject built;
            bool ok = _bridge.BuildService.TryBuild(
                payload.ItemKey,
                payload.Position,
                payload.Rotation,
                payload.Zone,
                context.WorldState,
                context.MapAnalyzer,
                context.ThreatAnalyzer,
                out built,
                out message);

            if (!ok)
            {
                context.Brain.Refund(item.preco);
            }

            return ok;
        }

        private bool ExecuteProduce(IA_CommandRequest request, IA_Context context, out string message)
        {
            IA_ProduceOrderData payload = request.Payload as IA_ProduceOrderData;
            if (payload == null)
            {
                message = "payload invalido";
                return false;
            }

            DadosConstrucao item;
            if (!_bridge.TryResolveItem(payload.ItemKey, out item))
            {
                message = "item nao encontrado";
                return false;
            }

            int amount = Mathf.Max(1, payload.Quantity);
            int totalCost = item.preco * amount;
            if (!context.Brain.TrySpend(totalCost))
            {
                message = "credito insuficiente";
                return false;
            }

            int produced;
            bool ok = _bridge.ProductionService.TryProduce(payload.ItemKey, payload.Quantity, context.WorldState, out produced, out message);
            if (!ok || produced <= 0)
            {
                context.Brain.Refund(totalCost);
                return false;
            }

            if (produced < amount)
            {
                int refund = (amount - produced) * item.preco;
                context.Brain.Refund(refund);
            }

            message = "produzido=" + produced;
            return true;
        }

        private bool ExecuteMove(IA_CommandRequest request, out string message)
        {
            message = string.Empty;
            IA_MoveOrderData payload = request.Payload as IA_MoveOrderData;
            if (payload == null || payload.Units.Count == 0)
            {
                message = "sem unidades";
                return false;
            }

            int moved = 0;
            int total = payload.Units.Count;
            for (int i = 0; i < payload.Units.Count; i++)
            {
                GameObject unit = payload.Units[i];
                if (unit == null)
                {
                    continue;
                }

                Vector3 slotDestination = ComputeFormationDestination(unit, payload.Destination, i, total);
                if (!TryIssueMove(unit, slotDestination))
                {
                    continue;
                }

                moved++;
            }

            message = "movidas=" + moved;
            return moved > 0;
        }

        private bool ExecuteAttack(IA_CommandRequest request, out string message)
        {
            IA_AttackOrderData payload = request.Payload as IA_AttackOrderData;
            if (payload == null || payload.Units.Count == 0)
            {
                message = "sem unidades";
                return false;
            }

            Vector3 target = payload.Target != null ? payload.Target.position : payload.TargetPosition;
            int moved = 0;
            int total = payload.Units.Count;
            for (int i = 0; i < payload.Units.Count; i++)
            {
                GameObject unit = payload.Units[i];
                if (unit == null)
                {
                    continue;
                }

                Vector3 slotDestination = ComputeFormationDestination(unit, target, i, total);
                PrepareUnitForAttack(unit, payload.Target, target);
                TryIssueMove(unit, slotDestination);
                SistemaDeTiro weapon = unit.GetComponentInChildren<SistemaDeTiro>();
                if (weapon != null && payload.Target != null)
                {
                    weapon.alvoAtual = payload.Target;
                }

                moved++;
            }

            message = "atacantes=" + moved;
            return moved > 0;
        }

        private bool ExecutePatrol(IA_CommandRequest request, out string message)
        {
            IA_PatrolOrderData payload = request.Payload as IA_PatrolOrderData;
            if (payload == null || payload.Units.Count == 0)
            {
                message = "sem unidades";
                return false;
            }

            int ordered = 0;
            int total = payload.Units.Count;
            for (int i = 0; i < payload.Units.Count; i++)
            {
                GameObject unit = payload.Units[i];
                if (unit == null)
                {
                    continue;
                }

                Vector3 slotDestination = ComputeFormationDestination(unit, payload.PointA, i, total);
                if (!TryIssueMove(unit, slotDestination))
                {
                    continue;
                }

                ordered++;
            }

            message = "patrulha=" + ordered;
            return ordered > 0;
        }

        private bool ExecuteAbility(IA_CommandRequest request, out string message)
        {
            IA_AbilityOrderData payload = request.Payload as IA_AbilityOrderData;
            bool ok = _bridge.AbilityService.TryUseAbility(payload, out message);
            return ok;
        }

        private bool TryIssueMove(GameObject unit, Vector3 destination)
        {
            if (unit == null)
            {
                return false;
            }

            int id = unit.GetInstanceID();
            float now = Time.time;
            float nearThreshold = GetNearThreshold(unit);
            Vector3 flatDestination = Flatten(destination);
            if (Vector3.Distance(Flatten(unit.transform.position), flatDestination) <= nearThreshold)
            {
                return false;
            }

            Vector3 lastDestination;
            float lastOrderTime = 0f;
            bool hasRecentOrder = _lastDestinationByUnit.TryGetValue(id, out lastDestination);
            if (hasRecentOrder)
            {
                hasRecentOrder = _lastOrderTimeByUnit.TryGetValue(id, out lastOrderTime);
            }
            if (hasRecentOrder
                && now - lastOrderTime <= 1.15f
                && Vector3.Distance(Flatten(lastDestination), flatDestination) <= Mathf.Max(4f, nearThreshold * 0.9f))
            {
                return false;
            }

            if (TryIssueSpecializedMove(unit, destination))
            {
                _lastDestinationByUnit[id] = destination;
                _lastOrderTimeByUnit[id] = now;
                return true;
            }

            unit.SendMessage("MoverParaPonto", destination, SendMessageOptions.DontRequireReceiver);
            NavMeshAgent nav = unit.GetComponent<NavMeshAgent>();
            if (nav != null && nav.enabled && nav.isOnNavMesh)
            {
                nav.isStopped = false;
                nav.SetDestination(destination);
            }

            _lastDestinationByUnit[id] = destination;
            _lastOrderTimeByUnit[id] = now;
            return true;
        }

        private static void PrepareUnitForAttack(GameObject unit, Transform target, Vector3 targetPosition)
        {
            if (unit == null)
            {
                return;
            }

            Vector3 desired = target != null ? target.position : targetPosition;

            LancadorMisselCaca airLauncher = unit.GetComponent<LancadorMisselCaca>();
            if (airLauncher != null)
            {
                airLauncher.modoPassivo = false;
            }

            Helicoptero helicopter = unit.GetComponent<Helicoptero>();
            if (helicopter != null)
            {
                helicopter.modoCombateAtivo = true;
            }

            ControleAviao modernAircraft = unit.GetComponent<ControleAviao>();
            if (modernAircraft != null)
            {
                if (desired.y < 60f)
                {
                    desired.y = 60f;
                }

                modernAircraft.alvoPrioritarioIA = true;
                modernAircraft.centroDaPatrulha = desired;
                modernAircraft.alvoGPSVoo = desired;
            }
        }

        private static bool TryIssueSpecializedMove(GameObject unit, Vector3 destination)
        {
            ControleAviao modernAircraft = unit.GetComponent<ControleAviao>();
            if (modernAircraft != null)
            {
                Vector3 airDestination = destination;
                if (airDestination.y < 60f)
                {
                    airDestination.y = 60f;
                }

                modernAircraft.aguardandoCliqueRadar = false;
                modernAircraft.alvoPrioritarioIA = true;
                modernAircraft.centroDaPatrulha = airDestination;
                modernAircraft.alvoGPSVoo = airDestination;

                if (modernAircraft.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    modernAircraft.IniciarMissaoCompleta(airDestination);
                }

                LancadorMisselCaca airLauncher = unit.GetComponent<LancadorMisselCaca>();
                if (airLauncher != null)
                {
                    airLauncher.modoPassivo = false;
                }

                return true;
            }

            Helicoptero helicopter = unit.GetComponent<Helicoptero>();
            if (helicopter != null)
            {
                helicopter.modoCombateAtivo = true;
                helicopter.Decolar(destination);
                return true;
            }

            ControleAviaoCaca legacyAircraft = unit.GetComponent<ControleAviaoCaca>();
            if (legacyAircraft != null)
            {
                Vector3 airDestination = destination;
                if (airDestination.y < 40f)
                {
                    airDestination.y = 40f;
                }

                legacyAircraft.DefinirDestino(airDestination);
                return true;
            }

            return false;
        }

        private static Vector3 ComputeFormationDestination(GameObject unit, Vector3 anchor, int index, int total)
        {
            if (unit == null || total <= 1)
            {
                return anchor;
            }

            float spacing = GetFormationSpacing(unit);
            int ring = 0;
            int slotInRing = index;
            int capacity = 1;
            while (slotInRing >= capacity)
            {
                slotInRing -= capacity;
                ring++;
                capacity = Mathf.Max(6, ring * 6);
            }

            if (ring == 0)
            {
                return anchor;
            }

            float angleStep = 360f / Mathf.Max(1, capacity);
            float stableAngle = Mathf.Abs(unit.GetInstanceID() * 0.137f) % 360f;
            float angle = stableAngle + (slotInRing * angleStep);
            float radius = ring * spacing;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
            return anchor + offset;
        }

        private static float GetFormationSpacing(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsAirUnit(unit, n))
            {
                return 18f;
            }

            if (IsNavalUnit(unit, n))
            {
                return 24f;
            }

            if (n.Contains("truck") || n.Contains("caminhao") || n.Contains("transporte") || n.Contains("hover"))
            {
                return 12f;
            }

            if (n.Contains("tank") || n.Contains("mbt") || n.Contains("south") || n.Contains("arthur") || n.Contains("c1") || n.Contains("hack"))
            {
                return 10f;
            }

            return 5.5f;
        }

        private static float GetNearThreshold(GameObject unit)
        {
            string n = IA_Text.Normalize(unit.name);
            if (IsAirUnit(unit, n))
            {
                return 16f;
            }

            if (IsNavalUnit(unit, n))
            {
                return 18f;
            }

            if (n.Contains("truck") || n.Contains("caminhao") || n.Contains("transporte") || n.Contains("hover"))
            {
                return 8f;
            }

            if (n.Contains("tank") || n.Contains("mbt") || n.Contains("south") || n.Contains("arthur") || n.Contains("c1"))
            {
                return 7f;
            }

            return 4.5f;
        }

        private static bool IsAirUnit(GameObject unit, string normalizedName)
        {
            return unit.GetComponent<ControleAviao>() != null
                   || unit.GetComponent<ControleAviaoCaca>() != null
                   || unit.GetComponent<Helicoptero>() != null
                   || normalizedName.Contains("heli")
                   || normalizedName.Contains("ray")
                   || normalizedName.Contains("vans")
                   || normalizedName.Contains("fa1")
                   || normalizedName.Contains("caca")
                   || normalizedName.Contains("aviao");
        }

        private static bool IsNavalUnit(GameObject unit, string normalizedName)
        {
            return unit.GetComponent<ControleNavioRealista>() != null
                   || unit.GetComponent<ControleSubmarino>() != null
                   || normalizedName.Contains("navio")
                   || normalizedName.Contains("sub")
                   || normalizedName.Contains("corveta")
                   || normalizedName.Contains("destroy")
                   || normalizedName.Contains("lancha")
                   || normalizedName.Contains("arrowhead");
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
