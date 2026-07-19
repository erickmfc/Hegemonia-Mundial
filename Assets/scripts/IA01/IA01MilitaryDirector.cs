using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>
    /// Garante a reserva militar mínima da IA01. A construção civil continua sendo
    /// decidida pelo plano normal; este diretor só impede que uma nação fique sem
    /// unidades para executar ordens enquanto a infraestrutura ainda está crescendo.
    /// </summary>
    public sealed class IA01MilitaryDirector
    {
        private readonly IA01Controller controller;
        private readonly IA01RuntimeContext context;
        private readonly List<DadosConstrucao> catalog = new List<DadosConstrucao>(128);
        private float nextTickAt;
        private float nextPatrolAt;
        private int lastNavalPatrolDay = -1;
        private int lastAirPatrolDay = -1;
        private int issuedFighters;
        private int issuedNaval;
        private float lastFighterOrderAt = -999f;
        private float nextShipyardRecoveryAt;
        private float nextPierRecoveryAt;
        private float nextPlatformRecoveryAt;
        private float nextNavalStagingAt;
        private string status = "Reserva militar aguardando infraestrutura.";

        private const int MinSoldiers = 6;
        private const int MinTanks = 3;
        private const int MinFighters = 2;
        private const int MinNaval = 1;

        public string Status => status;

        public IA01MilitaryDirector(IA01Controller controller, IA01RuntimeContext context)
        {
            this.controller = controller;
            this.context = context;
        }

        public bool Tick(float now)
        {
            if (now < nextTickAt || context == null || context.TeamId <= 0)
            {
                return false;
            }

            nextTickAt = now + 2.25f;
            RefreshCatalog();
            NormalizeOwnedNavalIdentities();
            int soldiers = CountUnits(TipoUnidade.Infantaria);
            int tanks = CountTanks();
            int fighters = CountFighters();
            int naval = CountUnits(TipoUnidade.Naval);
            ResolveTargets(out int targetSoldiers, out int targetTanks, out int targetFighters, out int targetNaval);
            bool changed = false;

            // Uma fila de aeroporto que nunca liberou a aeronave não pode
            // bloquear as próximas compras da IA indefinidamente.
            if (fighters == 0 && issuedFighters > 0 && now - lastFighterOrderAt > 10f)
                issuedFighters = 0;

            // No máximo duas ordens por ciclo para não sobrecarregar o frame.
            int actions = 0;
            if (soldiers < targetSoldiers && actions < 2)
            {
                bool produced = TryProduceLand(FindSoldier(), "soldados");
                changed |= produced;
                if (produced) actions++;
            }
            if (tanks < targetTanks && actions < 2)
            {
                bool produced = TryProduceLand(FindTank(), "tanques");
                changed |= produced;
                if (produced) actions++;
            }
            // A compra no aeroporto/estaleiro é assíncrona. Considera as ordens
            // já emitidas para não comprar duplicado antes da contagem atualizar.
            if (fighters + issuedFighters < targetFighters && actions < 2)
            {
                bool produced = TryProduceFighter(FindFighter(), now);
                changed |= produced;
                if (produced) actions++;
            }

            // Assim que existir estaleiro/pier, a IA coloca pelo menos uma unidade naval.
            if (naval + issuedNaval < targetNaval && HasOwnNavalInfrastructure() && actions < 2)
            {
                bool produced = TryProduceNaval(FindNaval(), "navios");
                changed |= produced;
                if (produced) actions++;
            }

            if (fighters >= issuedFighters) issuedFighters = 0;
            if (naval >= issuedNaval) issuedNaval = 0;

            // Se a abertura ainda não conseguiu erguer a fábrica/aeroporto em 12 s,
            // cria a reserva em ponto seguro do próprio território. Isso mantém a IA
            // jogável sem espalhar construções ou unidades para o mapa adversário.
            EnsurePierThenPlatform(now);
            ApplyNavalPatrols(now);
            ApplyNavalStaging(now);
            ApplyAirPatrols(now);
            status = string.Format("Reserva militar: soldados={0}/{1} tanques={2}/{3} cacas={4}/{5} navios={6}/{7}",
                soldiers, targetSoldiers, tanks, targetTanks, fighters, targetFighters, naval, targetNaval);
            if (changed)
            {
                Debug.Log("[IA01 Military] " + status);
            }
            return changed;
        }

        private void ResolveTargets(out int soldiers, out int tanks, out int fighters, out int naval)
        {
            soldiers = MinSoldiers; tanks = MinTanks; fighters = MinFighters; naval = MinNaval;
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            if (country == null) return;

            // Crescimento gradual: somente aumenta a reserva quando a economia
            // consegue pagar operacao e reposicao sem entrar em crise.
            bool economyHealthy = country.saldo >= 14000 && country.comida > 0 && country.energia > 0;
            if (!economyHealthy) return;
            int expansionTier = Mathf.Clamp((country.saldo - 14000) / 9000, 0, 3);
            soldiers += expansionTier * 2;
            tanks += expansionTier;
            fighters += expansionTier;
            naval += expansionTier > 1 ? 1 : 0;
        }

        private void ApplyNavalPatrols(float now)
        {
            if (now < nextPatrolAt) return;
            IA01NavalPatrolZone[] zones = controller != null
                ? controller.GetComponentsInChildren<IA01NavalPatrolZone>(true)
                : Array.Empty<IA01NavalPatrolZone>();
            // Os creates navais ficam no layout da cena e nem sempre são
            // filhos diretos do controlador. Recupera-os globalmente para
            // que a patrulha não dependa da hierarquia do editor.
            if (zones.Length == 0)
            {
                zones = UnityEngine.Object.FindObjectsByType<IA01NavalPatrolZone>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            if (zones.Length == 0) return;
            if (zones.Length == 0) return;

            int currentDay = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            int interval = Mathf.Max(1, zones[0].IntervaloDias);
            if (currentDay > 0)
            {
                if (lastNavalPatrolDay >= 0 && currentDay - lastNavalPatrolDay < interval) return;
                lastNavalPatrolDay = currentDay;
            }
            else
            {
                // No primeiro dia a patrulha começa assim que a primeira
                // embarcação sai do estaleiro; depois o intervalo do create
                // controla as novas passagens. Evita deixar o navio parado
                // por quase um minuto após a construção.
                nextPatrolAt = now + 5f;
            }
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            int assigned = 0;
            int candidates = 0;
            int alreadyPatrolling = 0;
            int rejected = 0;
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Naval) continue;
                candidates++;
                // Creates antigos podem deixar a IdentidadeUnidade em um filho
                // enquanto o controle fica no objeto raiz (ou vice-versa).
                // Procura nos três níveis para não contar o navio sem conseguir
                // entregar a ordem de patrulha.
                ControleUnidade control = id.GetComponent<ControleUnidade>();
                if (control == null) control = id.GetComponentInParent<ControleUnidade>();
                if (control == null) control = id.GetComponentInChildren<ControleUnidade>(true);
                if (control == null)
                {
                    rejected++;
                    Debug.LogWarning("[IA01 Military] Navio sem ControleUnidade para patrulha: " + id.name);
                    continue;
                }
                if (control.OrdemAtual == OrdemControleUnidade.Patrulhando)
                {
                    alreadyPatrolling++;
                    continue;
                }
                // Um navio abre a patrulha contínua. Os demais são escalonados
                // em pontos costeiros leves para não concentrar custo de rota.
                if (assigned > 0)
                {
                    continue;
                }
                IA01NavalPatrolZone zone = zones[assigned % zones.Length];
                Vector3[] route = zone.CriarRota(assigned);
                assigned++;
                if (control.EmitirOrdemPatrulha(route))
                {
                    DiagnosticoDesempenhoJogo.RegistrarEvento("IA01_NavalPatrol", id.name + " patrulha " + zone.name);
                }
                else
                {
                    rejected++;
                    Debug.LogWarning("[IA01 Military] Patrulha recusada: " + id.name + " ordem=" + control.OrdemAtual);
                }
            }
            if (candidates > 0)
            {
                Debug.Log(string.Format("[IA01 Military] Patrulha naval: candidatos={0} atribuídos={1} jáPatrulhando={2} recusados={3}",
                    candidates, assigned, alreadyPatrolling, rejected));
            }
            else
            {
                int navalIdentities = 0;
                for (int i = 0; i < identities.Length; i++)
                {
                    IdentidadeUnidade id = identities[i];
                    if (id != null && id.tipoUnidade == TipoUnidade.Naval)
                    {
                        navalIdentities++;
                    }
                }
                if (navalIdentities > 0)
                {
                    Debug.LogWarning(string.Format("[IA01 Military] Navios encontrados sem vínculo com o time {0}: identidades navais={1}",
                        context.TeamId, navalIdentities));
                }
            }
        }

        private void ApplyAirPatrols(float now)
        {
            IA01AirPatrolZone[] zones = controller != null
                ? controller.GetComponentsInChildren<IA01AirPatrolZone>(true)
                : Array.Empty<IA01AirPatrolZone>();
            if (zones.Length == 0)
            {
                zones = UnityEngine.Object.FindObjectsByType<IA01AirPatrolZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            if (zones.Length == 0)
            {
                IA01AirPatrolZone created = EnsureAirPatrolZone();
                if (created != null) zones = new[] { created };
            }
            if (zones.Length == 0) return;

            int currentDay = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            int interval = Mathf.Max(1, zones[0].IntervaloDias);
            if (currentDay > 0)
            {
                if (lastAirPatrolDay >= 0 && currentDay - lastAirPatrolDay < interval) return;
                lastAirPatrolDay = currentDay;
            }
            else if (now < 3f)
            {
                return;
            }

            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            int candidates = 0;
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Aereo) continue;
                candidates++;
                ControleUnidade control = id.GetComponent<ControleUnidade>()
                    ?? id.GetComponentInParent<ControleUnidade>()
                    ?? id.GetComponentInChildren<ControleUnidade>(true);
                if (control == null || control.OrdemAtual == OrdemControleUnidade.Patrulhando) continue;
                Vector3[] route = zones[0].CriarRota(0);
                if (control.EmitirOrdemPatrulha(route))
                {
                    DiagnosticoDesempenhoJogo.RegistrarEvento("IA01_AirPatrol", id.name + " patrulha " + zones[0].name);
                    Debug.Log("[IA01 Military] Patrulha aerea: " + id.name + " -> " + zones[0].name + " centro=" + zones[0].transform.position.ToString("F2"));
                }
                // Apenas um caça inicia a patrulha; a expansão pode liberar os demais.
                break;
            }
            if (candidates > 0)
            {
                Debug.Log("[IA01 Military] Patrulha aerea: candidatos=" + candidates + " | limite inicial=1");
            }
        }

        private IA01AirPatrolZone EnsureAirPatrolZone()
        {
            GerenciadorAeroporto[] airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                if (airport == null || !BelongsToOwnAirport(airport)) continue;
                Transform existing = airport.transform.Find("IA01 Patrulha Aerea - Área Inicial");
                GameObject go = existing != null ? existing.gameObject : new GameObject("IA01 Patrulha Aerea - Área Inicial");
                if (existing == null) go.transform.SetParent(airport.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 280f);
                IA01AirPatrolZone zone = go.GetComponent<IA01AirPatrolZone>();
                if (zone == null) zone = go.AddComponent<IA01AirPatrolZone>();
                Debug.Log("[IA01 Military] Create de patrulha aerea garantido: " + go.name + " pos=" + go.transform.position.ToString("F2"));
                return zone;
            }
            return null;
        }

        private void ApplyNavalStaging(float now)
        {
            if (now < nextNavalStagingAt) return;
            nextNavalStagingAt = now + 30f;
            IA01NavalPatrolZone[] zones = UnityEngine.Object.FindObjectsByType<IA01NavalPatrolZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (zones.Length == 0) return;
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            int staged = 0;
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Naval) continue;
                ControleUnidade control = id.GetComponent<ControleUnidade>()
                    ?? id.GetComponentInParent<ControleUnidade>()
                    ?? id.GetComponentInChildren<ControleUnidade>(true);
                if (control == null || control.OrdemAtual == OrdemControleUnidade.Patrulhando) continue;
                ControleNavioRealista navio = id.GetComponent<ControleNavioRealista>()
                    ?? id.GetComponentInParent<ControleNavioRealista>()
                    ?? id.GetComponentInChildren<ControleNavioRealista>(true);
                if (navio != null && navio.TemDestinoAtivo) continue;
                Vector3 ponto = zones[(i + 1) % zones.Length].transform.position;
                if (NavalPlacementResolver.TryResolveWaterSpawn(ponto, Vector3.right, 0f, 180f, out Vector3 agua, out _, out _))
                    ponto = agua;
                if (control.EmitirOrdemMover(ponto, true))
                {
                    staged++;
                    Debug.Log("[IA01 Military] Navio em ponto costeiro otimizado: " + id.name + " -> " + ponto.ToString("F2"));
                    if (staged >= 2) break;
                }
            }
        }

        private void EnsurePierThenPlatform(float now)
        {
            if (controller == null) return;
            PierMarinha[] piers = UnityEngine.Object.FindObjectsByType<PierMarinha>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool ownPier = false;
            for (int i = 0; i < piers.Length; i++)
            {
                if (piers[i] != null && BelongsToTeam(piers[i].gameObject)) { ownPier = true; break; }
            }

            if (!ownPier && now >= nextPierRecoveryAt)
            {
                nextPierRecoveryAt = now + 18f;
                BuildCoastalStructure(IA01IntentType.BuildPier, "pier", "dock");
                return;
            }
            if (!ownPier || now < nextPlatformRecoveryAt) return;

            PlataformaOffshore[] platforms = UnityEngine.Object.FindObjectsByType<PlataformaOffshore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
            {
                if (platforms[i] != null && BelongsToTeam(platforms[i].gameObject)) return;
            }
            nextPlatformRecoveryAt = now + 35f;
            BuildCoastalStructure(IA01IntentType.BuildOffshorePlatform, "plataforma", "offshore");
        }

        private bool BuildCoastalStructure(IA01IntentType intent, params string[] blueprintTokens)
        {
            if (!controller.TryResolveConstructionAnchor(intent, out Vector3 anchor, out Quaternion rotation)) return false;
            DadosConstrucao blueprint = FindStructureBlueprint(blueprintTokens);
            if (blueprint == null || !blueprint.TryGetPrefabBasico(out GameObject prefab) || prefab == null) return false;
            Construtor builder = Construtor.Instancia != null ? Construtor.Instancia : UnityEngine.Object.FindFirstObjectByType<Construtor>();
            GameObject built = builder != null ? builder.ConstruirEstruturaIA(prefab, anchor, rotation) : UnityEngine.Object.Instantiate(prefab, anchor, rotation);
            if (built == null) return false;
            built.transform.SetPositionAndRotation(anchor, rotation);
            IdentidadeUnidade identity = built.GetComponent<IdentidadeUnidade>() ?? built.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller.NationName;
            identity.tipoUnidade = TipoUnidade.Estrutura;
            Debug.Log("[IA01 Military] " + intent + " criado no create: " + built.name + " pos=" + built.transform.position.ToString("F2"));
            return true;
        }

        private void NormalizeOwnedNavalIdentities()
        {
            Estaleiro[] shipyards = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            List<Vector3> ownedPorts = new List<Vector3>();
            for (int i = 0; i < shipyards.Length; i++)
            {
                Estaleiro shipyard = shipyards[i];
                if (shipyard != null && BelongsToTeam(shipyard.gameObject)) ownedPorts.Add(shipyard.transform.position);
            }
            PierMarinha[] piers = UnityEngine.Object.FindObjectsByType<PierMarinha>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < piers.Length; i++)
            {
                if (piers[i] != null && BelongsToTeam(piers[i].gameObject)) ownedPorts.Add(piers[i].transform.position);
            }
            if (ownedPorts.Count == 0) return;

            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.tipoUnidade != TipoUnidade.Naval || id.teamID == context.TeamId) continue;
                bool nearOwnedPort = false;
                for (int p = 0; p < ownedPorts.Count; p++)
                {
                    Vector3 delta = id.transform.position - ownedPorts[p];
                    delta.y = 0f;
                    // O estaleiro libera a embarcação alguns centenas de metros
                    // mar adentro; a identidade pode estar além do create visual.
                    if (delta.sqrMagnitude <= 2600f * 2600f) { nearOwnedPort = true; break; }
                }
                // Navios liberados pelo estaleiro sem identidade herdada recebem o
                // time da IA antes da contagem e da ordem de patrulha.
                // O Estaleiro instancia alguns prefabs com a identidade original
                // do asset (que pode ser qualquer time). Como este navio acabou de
                // sair de um estaleiro da IA, a proximidade do porto proprio é a
                // fonte de verdade para a posse.
                if (nearOwnedPort)
                {
                    id.teamID = context.TeamId;
                    id.nomeDoPais = controller != null ? controller.NationName : "IA01";
                }
            }
        }

        private void RefreshCatalog()
        {
            if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
            {
                MenuConstrucao menu = MenuConstrucao.Instancia;
                if (menu != null)
                {
                    menu.GarantirCatalogoParaIA();
                }
            }

            catalog.Clear();
            if (MenuConstrucao.catalogoGlobal != null)
            {
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                    AddCandidate(MenuConstrucao.catalogoGlobal[i]);
            }

            DadosConstrucao[] resources = Resources.LoadAll<DadosConstrucao>(string.Empty);
            for (int i = 0; i < resources.Length; i++)
                AddCandidate(resources[i]);
        }

        private void AddCandidate(DadosConstrucao item)
        {
            if (item == null || catalog.Contains(item)) return;
            try
            {
                GameObject prefab;
                if (!item.TryGetPrefabBasico(out prefab) || prefab == null) return;
                // Fichas antigas ainda não têm capacidades preenchidas. A categoria
                // e o nome continuam sendo a fonte compatível para a reserva militar.
                if (item.categoria != DadosConstrucao.CategoriaItem.Exercito
                    && item.categoria != DadosConstrucao.CategoriaItem.Aeronautica
                    && item.categoria != DadosConstrucao.CategoriaItem.Marinha) return;
                catalog.Add(item);
            }
            catch (Exception)
            {
                // Prefab com script ausente não pode entrar na reserva militar.
            }
        }

        private DadosConstrucao FindSoldier()
        {
            return FindUnit(item => item.categoria == DadosConstrucao.CategoriaItem.Exercito
                && Contains(item, "soldado", "soldier", "infantaria", "infantry", "rifle", "fuzil")
                && !Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro"));
        }

        private DadosConstrucao FindTank()
        {
            return FindUnit(item => item.categoria == DadosConstrucao.CategoriaItem.Exercito
                && Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro"));
        }

        private DadosConstrucao FindFighter()
        {
            return FindUnit(item => item.HasCapability(IA_ConstructionCapability.FighterAircraft)
                || (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica && Contains(item, "caca", "fighter", "su11", "g15", "falcon")));
        }

        private DadosConstrucao FindNaval()
        {
            return FindUnit(item => item.categoria == DadosConstrucao.CategoriaItem.Marinha
                && !item.HasCapability(IA_ConstructionCapability.NavalTransport)
                && !Contains(item, "petroleiro", "tanker", "transporte"));
        }

        private DadosConstrucao FindUnit(Func<DadosConstrucao, bool> predicate)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                DadosConstrucao item = catalog[i];
                if (item != null && predicate(item)) return item;
            }
            return null;
        }

        private bool TryProduceLand(DadosConstrucao item, string label)
        {
            if (item == null || item.prefabDaUnidade == null) return false;
            Fabrica[] factories = UnityEngine.Object.FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
            for (int i = 0; i < factories.Length; i++)
            {
                Fabrica factory = factories[i];
                if (!BelongsToTeam(factory != null ? factory.gameObject : null)) continue;
                if (!IsAppropriateFactory(factory, item)) continue;
                NormalizeFactorySpawnPoints(factory);
                GameObject produced = factory.ProduzirUnidade(item.prefabDaUnidade);
                if (produced != null)
                {
                    EnsureOwnedIdentity(produced, item);
                    return true;
                }

                // Fichas/creates antigos podem ter uma Fabrica sem identidade ou
                // com ponto de saída inválido. Ainda assim, a ordem deve gerar a
                // unidade no próprio create da fábrica, nunca no território rival.
                if (TryEmergencySpawn(item, label, factory.transform)) return true;
            }
            return false;
        }

        private bool TryProduceFighter(DadosConstrucao item, float now)
        {
            GerenciadorAeroporto[] airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // A abertura antiga já tinha o aeroporto militar antes da primeira
            // compra. Se o plano civil ainda estiver preso em outro passo,
            // recupera o aeroporto no create fixo da IA e usa a mesma fila do
            // jogador (ComprarAviao), em vez de mandar o caça para um ponto
            // genérico do mapa.
            bool ownAirportFound = false;
            for (int i = 0; i < airports.Length; i++)
            {
                if (BelongsToOwnAirport(airports[i]))
                {
                    ownAirportFound = true;
                    break;
                }
            }
            if (!ownAirportFound)
            {
                GerenciadorAeroporto recovered = EnsureOwnMilitaryAirport();
                if (recovered != null)
                    airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            if (airports.Length == 0)
            {
                return SpawnFighterAtMilitaryAirport(item);
            }
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                // Nunca usar a fila de uma base de outra nação. Caso nenhum
                // aeroporto pertencente à IA seja localizado, o fallback abaixo
                // cria o caça no create militar oficial dela.
                if (!BelongsToOwnAirport(airport)) continue;
                airport.gameObject.SetActive(true);
                airport.enabled = true;
                airport.SetarSemEnergia(false);
                EnsureAirportIdentity(airport);
                // Nem toda ficha de caca esta no catalogo global em runtime. O
                // aeroporto militar ja possui a mesma referencia usada pelo
                // jogador, portanto ela é o fallback correto e estaciona no patio.
                GameObject aircraft = item != null ? item.prefabDaUnidade : null;
                if (aircraft == null && item != null) item.TryGetPrefabBasico(out aircraft);
                if (aircraft == null) aircraft = airport.prefabSu11;
                if (aircraft == null)
                {
                    status = "Aeroporto militar sem prefab de caca configurado.";
                    continue;
                }
                airport.ComprarAviaoIAImediato(aircraft);
                // A fila é a mesma usada pelo jogador: ela pode liberar a aeronave
                // no próximo frame, já na vaga de estacionamento do aeroporto.
                issuedFighters++;
                lastFighterOrderAt = Time.time;
                Debug.Log("[IA01 Military] Caca estacionado no aeroporto proprio: " + airport.name + " -> " + aircraft.name);
                return true;

                // Aeroporto militar sem IdentidadeUnidade não propaga o team para
                // o avião. Corrige isso criando a aeronave no ponto do aeroporto.
                // Sem fila/identidade valida, aguarda a proxima compra. Nunca
                // cria o caca em um ponto terrestre generico.
            }

            // Não use um aeroporto de outra nação como motivo para a IA ficar
            // sem aviação. Se nenhum componente encontrado for o dela, o create
            // militar é a fonte segura: a aeronave nasce no próprio pátio.
            return SpawnFighterAtMilitaryAirport(item);
        }

        private GerenciadorAeroporto EnsureOwnMilitaryAirport()
        {
            if (controller == null) return null;
            Vector3 anchor;
            Quaternion rotation;
            if (!controller.TryResolveConstructionAnchor(IA01IntentType.BuildMilitaryAirport, out anchor, out rotation))
            {
                Debug.LogWarning("[IA01 Military] Create de aeroporto militar nao resolvido para a IA01.");
                return null;
            }

            DadosConstrucao blueprint = FindStructureBlueprint("aeroporto militar", "military airport");
            GameObject prefab;
            if (blueprint == null || !blueprint.TryGetPrefabBasico(out prefab) || prefab == null)
            {
                Debug.LogWarning("[IA01 Military] Ficha/prefab do aeroporto militar nao encontrado.");
                return null;
            }

            Construtor builder = Construtor.Instancia != null
                ? Construtor.Instancia
                : UnityEngine.Object.FindFirstObjectByType<Construtor>();
            GameObject built = builder != null
                ? builder.ConstruirEstruturaIA(prefab, anchor, rotation)
                : UnityEngine.Object.Instantiate(prefab, anchor, rotation);
            if (built == null) return null;

            // A estrutura usa o create oficial do aeroporto. Apenas os pontos
            // internos de spawn/saida podem ser ajustados pelo construtor.
            built.transform.SetPositionAndRotation(anchor, rotation);

            IdentidadeUnidade identity = built.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = built.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller.NationName;
            identity.tipoUnidade = TipoUnidade.Estrutura;

            GerenciadorAeroporto airport = built.GetComponent<GerenciadorAeroporto>();
            if (airport != null)
            {
                airport.gameObject.SetActive(true);
                airport.enabled = true;
                // O create militar pode nascer antes da primeira usina. A base
                // continua usando a fila oficial do aeroporto, mas não fica
                // permanentemente travada pelo sinal inicial de apagão durante
                // a mobilização mínima da IA.
                airport.SetarSemEnergia(false);
                EnsureAirportIdentity(airport);
                Debug.Log("[IA01 Military] Aeroporto militar criado no create: " + built.name + " pos=" + built.transform.position.ToString("F2"));
            }
            return airport;
        }

        private bool SpawnFighterAtMilitaryAirport(DadosConstrucao item)
        {
            // Proteção para prefab configurado sem o componente de serviço ou
            // para cenas com aeroportos de outros países: nasce no create do
            // aeroporto da IA, nunca em ponto genérico do mapa.
            GameObject fallback = item != null ? item.prefabDaUnidade : null;
            if (fallback == null && controller != null) fallback = controller.FighterPrefab;
            Vector3 anchor = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool hasAnchor = false;
            if (controller != null)
            {
                hasAnchor = controller.TryResolveConstructionAnchor(IA01IntentType.BuildMilitaryAirport, out anchor, out rotation);
            }
            if (!hasAnchor && controller != null)
            {
                Transform[] markers = controller.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < markers.Length; i++)
                {
                    Transform marker = markers[i];
                    if (marker != null && marker.name.IndexOf("Aeroporto Militar", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        anchor = marker.position;
                        rotation = marker.rotation;
                        hasAnchor = true;
                        break;
                    }
                }
            }
            if (!hasAnchor)
            {
                IA01BuildSlot[] slots = UnityEngine.Object.FindObjectsByType<IA01BuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < slots.Length; i++)
                {
                    IA01BuildSlot slot = slots[i];
                    if (slot != null && slot.name.IndexOf("Aeroporto Militar", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Transform point = slot.BuildingPoint != null ? slot.BuildingPoint : slot.transform;
                        anchor = point.position;
                        rotation = point.rotation;
                        hasAnchor = true;
                        break;
                    }
                }
            }
            if (fallback == null || controller == null || !hasAnchor)
            {
                status = "Reserva militar aguardando create do aeroporto militar.";
                return false;
            }

            GameObject unit = UnityEngine.Object.Instantiate(fallback, anchor, rotation);
            if (unit == null) return false;
            IdentidadeUnidade identity = unit.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = unit.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller.NationName;
            identity.tipoUnidade = TipoUnidade.Aereo;
            CombustivelUnidade.Garantir(unit, true);
            issuedFighters++;
            lastFighterOrderAt = Time.time;
            Debug.Log("[IA01 Military] Caca liberado no create do aeroporto militar: " + fallback.name);
            return true;
        }

        private bool BelongsToOwnAirport(GerenciadorAeroporto airport)
        {
            if (airport == null) return false;
            if (BelongsToTeam(airport.gameObject)) return true;
            if (controller == null) return false;

            Vector3 anchor;
            Quaternion rotation;
            if (controller.TryResolveConstructionAnchor(IA01IntentType.BuildMilitaryAirport, out anchor, out rotation)
                && (airport.transform.position - anchor).sqrMagnitude <= 600f * 600f)
            {
                return true;
            }
            if (controller.TryResolveConstructionAnchor(IA01IntentType.BuildCommercialAirport, out anchor, out rotation)
                && (airport.transform.position - anchor).sqrMagnitude <= 600f * 600f)
            {
                return true;
            }
            return false;
        }

        private bool TryProduceNaval(DadosConstrucao item, string label)
        {
            if (item == null || item.prefabDaUnidade == null) return false;
            Estaleiro[] shipyards = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            for (int i = 0; i < shipyards.Length; i++)
            {
                Estaleiro shipyard = shipyards[i];
                if (BelongsToTeam(shipyard != null ? shipyard.gameObject : null) && shipyard.ConstruirUnidade(item.prefabDaUnidade))
                {
                    Debug.Log("[IA01 Military] Navio enfileirado no estaleiro proprio: " + shipyard.name + " -> " + item.GetDisplayName());
                    issuedNaval++;
                    return true;
                }
            }

            // O plano civil pode ficar aguardando uma ficha terrestre inválida e
            // nunca chegar ao passo do estaleiro. Recupera somente o create naval
            // definido pelo próprio país, usando o mesmo prefab/componente do
            // jogador; assim a produção não cai em um porto aleatório.
            if (Time.time >= nextShipyardRecoveryAt)
            {
                nextShipyardRecoveryAt = Time.time + 10f;
                Estaleiro recovered = EnsureOwnShipyard();
                if (recovered != null && recovered.ConstruirUnidade(item.prefabDaUnidade))
                {
                    Debug.Log("[IA01 Military] Estaleiro recuperado no create e navio enfileirado: " + recovered.name);
                    issuedNaval++;
                    return true;
                }
            }
            return false;
        }

        private Estaleiro EnsureOwnShipyard()
        {
            Estaleiro[] existing = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && BelongsToTeam(existing[i].gameObject)) return existing[i];
            }
            if (controller == null) return null;

            Vector3 anchor = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool hasAnchor = controller.TryResolveConstructionAnchor(IA01IntentType.BuildShipyard, out anchor, out rotation);
            if (!hasAnchor)
            {
                Debug.LogWarning("[IA01 Military] Create de estaleiro naval nao resolvido para a IA01.");
                return null;
            }

            DadosConstrucao blueprint = FindStructureBlueprint("estaleiro", "shipyard", "naval yard");
            GameObject prefab;
            if (blueprint == null || !blueprint.TryGetPrefabBasico(out prefab) || prefab == null)
            {
                Debug.LogWarning("[IA01 Military] Ficha/prefab do estaleiro naval nao encontrado.");
                return null;
            }

            Construtor builder = Construtor.Instancia != null
                ? Construtor.Instancia
                : UnityEngine.Object.FindFirstObjectByType<Construtor>();
            GameObject built = builder != null
                ? builder.ConstruirEstruturaIA(prefab, anchor, rotation)
                : UnityEngine.Object.Instantiate(prefab, anchor, rotation);
            if (built == null) return null;

            // O root da estrutura deve permanecer exatamente no create do
            // estaleiro. O construtor ajusta apenas os pontos de spawn navais;
            // nunca deve reposicionar a estrutura em outro create.
            built.transform.SetPositionAndRotation(anchor, rotation);

            IdentidadeUnidade identity = built.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = built.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller.NationName;
            identity.tipoUnidade = TipoUnidade.Estrutura;
            Estaleiro shipyard = built.GetComponent<Estaleiro>();
            if (shipyard != null)
            {
                shipyard.GarantirSlotsExistentes();
                shipyard.AtualizarReferenciasLitoraneas();
                Debug.Log("[IA01 Military] Estaleiro naval criado no create: " + built.name + " pos=" + built.transform.position.ToString("F2"));
            }
            return shipyard;
        }

        private DadosConstrucao FindStructureBlueprint(params string[] tokens)
        {
            DadosConstrucao[] loaded = UnityEngine.Resources.FindObjectsOfTypeAll<DadosConstrucao>();
            for (int i = 0; i < loaded.Length; i++)
            {
                DadosConstrucao candidate = loaded[i];
                if (candidate == null || candidate.prefabDaUnidade == null) continue;
                if (Contains(candidate, tokens)) return candidate;
            }
            if (MenuConstrucao.catalogoGlobal != null)
            {
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                {
                    DadosConstrucao candidate = MenuConstrucao.catalogoGlobal[i];
                    if (candidate != null && candidate.prefabDaUnidade != null && Contains(candidate, tokens)) return candidate;
                }
            }
            return null;
        }

        private bool TryEmergencySpawn(DadosConstrucao item, string label, Transform anchor = null)
        {
            if (item == null || item.prefabDaUnidade == null) return false;
            if (anchor == null) return false;
            Vector3 origin = anchor != null ? anchor.position : (controller != null ? controller.transform.position : Vector3.zero);
            Vector3 position = origin + new Vector3(UnityEngine.Random.Range(-18f, 18f), 0f, UnityEngine.Random.Range(-18f, 18f));
            if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 25f, UnityEngine.AI.NavMesh.AllAreas))
                position = hit.position;
            GameObject unit = UnityEngine.Object.Instantiate(item.prefabDaUnidade, position, Quaternion.identity);
            if (unit == null) return false;
            IdentidadeUnidade identity = unit.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = unit.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller != null ? controller.NationName : "IA01";
            identity.tipoUnidade = item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
                ? TipoUnidade.Aereo
                : (item.categoria == DadosConstrucao.CategoriaItem.Marinha ? TipoUnidade.Naval : TipoUnidade.Veiculo);
            if (item.categoria == DadosConstrucao.CategoriaItem.Exercito && !Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro"))
                identity.tipoUnidade = TipoUnidade.Infantaria;
            if (identity.tipoUnidade == TipoUnidade.Aereo) issuedFighters++;
            CombustivelUnidade.Garantir(unit, true);
            DiagnosticoDesempenhoJogo.RegistrarProducao(item.GetDisplayName(), "IA01_ReservaMilitar");
            return true;
        }

        private void EnsureOwnedIdentity(GameObject unit, DadosConstrucao item)
        {
            if (unit == null || item == null || context == null) return;
            IdentidadeUnidade identity = unit.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = unit.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller != null ? controller.NationName : "IA01";
            identity.tipoUnidade = item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
                ? TipoUnidade.Aereo
                : (item.categoria == DadosConstrucao.CategoriaItem.Marinha ? TipoUnidade.Naval : TipoUnidade.Veiculo);
            if (item.categoria == DadosConstrucao.CategoriaItem.Exercito
                && !Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro"))
            {
                identity.tipoUnidade = TipoUnidade.Infantaria;
            }
            CombustivelUnidade.Garantir(unit, true);
        }

        private void EnsureAirportIdentity(GerenciadorAeroporto airport)
        {
            if (airport == null || context == null) return;
            IdentidadeUnidade identity = airport.GetComponent<IdentidadeUnidade>();
            if (identity == null) identity = airport.gameObject.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller != null ? controller.NationName : "IA01";
        }

        private int CountUnits(TipoUnidade type)
        {
            int count = 0;
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
                if (identities[i] != null && identities[i].teamID == context.TeamId && identities[i].tipoUnidade == type) count++;
            return count;
        }

        private int CountTanks()
        {
            int count = 0;
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Veiculo) continue;
                string name = IA_Text.Normalize(id.gameObject.name);
                if (Contains(name, "tank", "tanque", "blindado", "vehicle", "veiculo", "carro")) count++;
            }
            return count;
        }

        private int CountFighters()
        {
            int count = 0;
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Aereo) continue;
                if (id.GetComponentInChildren<ControleAviaoCaca>(true) != null || Contains(id.gameObject.name, "caca", "fighter", "su11", "g15", "falcon")) count++;
            }
            return count;
        }

        private bool HasOwnNavalInfrastructure()
        {
            Estaleiro[] shipyards = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            for (int i = 0; i < shipyards.Length; i++)
                if (BelongsToTeam(shipyards[i] != null ? shipyards[i].gameObject : null)) return true;
            if (Time.time >= nextShipyardRecoveryAt)
            {
                nextShipyardRecoveryAt = Time.time + 10f;
                return EnsureOwnShipyard() != null;
            }
            return false;
        }

        private bool BelongsToTeam(GameObject go)
        {
            IdentidadeUnidade identity = go != null ? go.GetComponentInParent<IdentidadeUnidade>() : null;
            if (identity != null && identity.teamID == context.TeamId) return true;
            if (go == null || context == null) return false;

            // Creates antigos de fábrica/aeroporto/estaleiro nem sempre carregam
            // IdentidadeUnidade. O registro territorial da própria IA continua
            // sendo uma fonte confiável para reconhecer essas estruturas.
            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager != null && manager.WorldRegistry != null)
            {
                IReadOnlyList<IA01WorldEntityRecord> records = manager.WorldRegistry.GetByTeam(context.TeamId);
                for (int i = 0; i < records.Count; i++)
                {
                    IA01WorldEntityRecord record = records[i];
                    if (record != null && record.Kind == IA01WorldEntityKind.Structure
                        && Vector3.Distance(record.Position, go.transform.position) <= 35f)
                    {
                        return true;
                    }
                }
            }

            // Fallback para a abertura, antes do primeiro refresh do registro.
            return controller != null
                && Vector3.Distance(controller.transform.position, go.transform.position) <= 150f;
        }

        private bool IsAppropriateFactory(Fabrica factory, DadosConstrucao item)
        {
            if (factory == null || item == null) return false;
            bool infantry = item.categoria == DadosConstrucao.CategoriaItem.Exercito
                && !Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro");
            string name = IA_Text.Normalize(factory.gameObject.name + " " + factory.transform.root.name);
            if (infantry)
            {
                return factory.ehQuartel || Contains(name, "tenda", "quartel", "barraca", "militar");
            }

            bool vehicle = item.categoria == DadosConstrucao.CategoriaItem.Exercito;
            if (vehicle)
            {
                return !factory.ehQuartel && Contains(name, "construtor", "veiculo", "fabrica militar", "hangar");
            }

            return !factory.ehQuartel;
        }

        private void NormalizeFactorySpawnPoints(Fabrica factory)
        {
            if (factory == null) return;
            // Os pontos precisam pertencer ao proprio create. Uma referencia externa
            // (como uma farm da cena) faz a unidade nascer ou sair no imovel errado.
            if (factory.pontoNascimento != null && !factory.pontoNascimento.IsChildOf(factory.transform))
            {
                factory.pontoNascimento = factory.transform;
            }
            if (factory.pontoSaida != null && !factory.pontoSaida.IsChildOf(factory.transform))
            {
                factory.pontoSaida = factory.transform;
            }
            if (factory.pontosSaidaExtras == null) factory.pontosSaidaExtras = new List<Transform>();
            factory.pontosSaidaExtras.RemoveAll(point => point == null
                || !point.IsChildOf(factory.transform));
        }

        private bool BelongsToOwnedStructure(GameObject go)
        {
            if (go == null || context == null) return false;
            IdentidadeUnidade identity = go.GetComponentInParent<IdentidadeUnidade>();
            if (identity != null) return identity.teamID == context.TeamId;

            IA01Manager manager = controller != null ? controller.Manager : null;
            if (manager == null || manager.WorldRegistry == null) return false;
            IReadOnlyList<IA01WorldEntityRecord> records = manager.WorldRegistry.GetByTeam(context.TeamId);
            for (int i = 0; i < records.Count; i++)
            {
                IA01WorldEntityRecord record = records[i];
                if (record == null || record.Kind != IA01WorldEntityKind.Structure) continue;
                GameObject nativeObject = record.NativeObject as GameObject;
                if (nativeObject != null && nativeObject == go) return true;
                if (Vector3.Distance(record.Position, go.transform.position) <= 8f) return true;
            }
            return false;
        }

        // Pontos de criacao sao sempre validados contra a estrutura dona.
        private static bool Contains(DadosConstrucao item, params string[] tokens)
        {
            return Contains(IA_Text.Normalize(item.GetDisplayName() + " " + item.name + " " + item.aliases), tokens);
        }

        private static bool Contains(string text, params string[] tokens)
        {
            string normalized = IA_Text.Normalize(text);
            for (int i = 0; i < tokens.Length; i++)
                if (!string.IsNullOrEmpty(tokens[i]) && normalized.Contains(IA_Text.Normalize(tokens[i]))) return true;
            return false;
        }
    }
}
