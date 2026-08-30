using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
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
        private readonly IA01BuildDirector buildDirector;
        private readonly List<DadosConstrucao> catalog = new List<DadosConstrucao>(128);
        private readonly List<DadosConstrucao> tierCandidates = new List<DadosConstrucao>(32);
        private float nextTickAt;
        private float nextPatrolAt;
        private int lastNavalPatrolDay = -1;

        private float nextAirPatrolScanAt;
        private int issuedFighters;
        private int issuedNaval;
        private float lastFighterOrderAt = -999f;
        private float nextShipyardRecoveryAt;
        private float nextPierRecoveryAt;
        private float nextPlatformRecoveryAt;
        private float nextNavalStagingAt;
        private float nextUnlinkedNavalWarningAt;
        private float platformConfirmedAt = -1f;
        private float nextTankerAttemptAt;
        private int tankerOrdersIssued;
        private int lastTankerOrderDay = -1;
        private int firstNavalOrderDay = -1;
        private float nextNavalCombatAt;
        private readonly List<IdentidadeUnidade> navalUnitsBuffer = new List<IdentidadeUnidade>(12);
        private readonly List<IdentidadeUnidade> airUnitsBuffer = new List<IdentidadeUnidade>(12);
        private readonly List<IA01AirPatrolZone> airPatrolCreatesBuffer = new List<IA01AirPatrolZone>(4);
        private bool warZonesEnsured;
        private string status = "Reserva militar aguardando infraestrutura.";

        private const int MinSoldiers = 6;
        private const int MinTanks = 3;
        private const int MinFighters = 2;
        private const int MinNaval = 1;
        // Limites fisicos da reserva por escalao. O escalao continua escolhendo
        // a ficha D/C/B/A/S, mas a IA nao acumula unidades indefinidamente so
        // porque possui caixa ou tempo de partida.
        private static readonly int[] MaxSoldiersByTier = { 6, 8, 12, 18, 24 };
        private static readonly int[] MaxTanksByTier = { 3, 4, 6, 8, 12 };
        private static readonly int[] MaxFightersByTier = { 2, 3, 5, 8, 12 };
        private static readonly int[] MaxNavalByTier = { 2, 2, 3, 4, 6 };
        private const float TankerDelayAfterPlatformSeconds = 60f;
        // A compra de caca usa uma fila assincrona. Dez segundos era curto
        // demais: quando o aeroporto demorava a liberar a vaga, a IA emitia
        // outra ordem a cada ciclo e acumulava dezenas de aeronaves.
        private const float FighterOrderTimeoutSeconds = 90f;
        // Os eventos continuam no diagnostico; logs detalhados no Console so
        // devem ser ligados enquanto se investiga uma patrulha especifica.
        private bool EmitirLogsDetalhadosDePatrulha = false;

        public string Status => status;

        private bool ProgressaoEscalaoAtiva => controller == null || controller.ProgressiveMilitaryCatalog;
        private bool PermiteInfraestruturaInicialAutomatica => controller != null && controller.UseScriptedOpening;

        public IA01MilitaryDirector(IA01Controller controller, IA01RuntimeContext context, IA01BuildDirector buildDirector)
        {
            this.controller = controller;
            this.context = context;
            this.buildDirector = buildDirector;
        }

        public bool Tick(float now)
        {
            if (now < nextTickAt || context == null || context.TeamId <= 0)
            {
                return false;
            }

            nextTickAt = now + 2.25f;

            // A Prefeitura e a fundacao da partida. A reserva militar nao deve
            // fabricar dezenas de unidades antes de a IA ter uma sede valida;
            // alem de quebrar a ordem da abertura, isso pesa no primeiro frame.
            if (!TemPrefeituraOperacional())
            {
                status = "Reserva militar aguardando a Prefeitura.";
                return false;
            }

            RefreshCatalog();
            NormalizeOwnedNavalIdentities();
            EnsureOperationalWarZones();
            int soldiers = CountUnits(TipoUnidade.Infantaria);
            int tanks = CountTanks();
            int antiAir = CountAntiAir();
            int fighters = CountFighters();
            int naval = CountUnits(TipoUnidade.Naval);
            ResolveTargets(out int targetSoldiers, out int targetTanks, out int targetFighters, out int targetNaval);
            bool changed = false;

            // Uma fila de aeroporto que nunca liberou a aeronave não pode
            // bloquear as próximas compras da IA indefinidamente.
            if (fighters == 0 && issuedFighters > 0 && now - lastFighterOrderAt > FighterOrderTimeoutSeconds)
                issuedFighters = 0;

            // No máximo duas ordens por ciclo para não sobrecarregar o frame.
            int actions = 0;
            if (soldiers < targetSoldiers && actions < 2)
            {
                string orderId;
                bool reserved = IA01MilitaryProductionGuard.TryReserve(context.TeamId, IA01MilitaryAssetKind.Infantry, targetSoldiers, soldiers, now, 45f, out orderId);
                bool produced = reserved && TryProduceLand(FindSoldier(), "soldados", orderId);
                if (reserved && !produced) IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.Infantry, now);
                changed |= produced;
                if (produced) actions++;
            }
            if (tanks < targetTanks && actions < 2)
            {
                string orderId;
                bool reserved = IA01MilitaryProductionGuard.TryReserve(context.TeamId, IA01MilitaryAssetKind.Tank, targetTanks, tanks, now, 45f, out orderId);
                bool produced = reserved && TryProduceLand(FindTank(), "tanques", orderId);
                if (reserved && !produced) IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.Tank, now);
                changed |= produced;
                if (produced) actions++;
            }
            // A compra no aeroporto/estaleiro é assíncrona. Considera as ordens
            // já emitidas para não comprar duplicado antes da contagem atualizar.
            // Toda IA01 procura manter pelo menos uma defesa antiaerea Ares_Ar
            // quando existe uma ficha valida no catalogo e uma fabrica propria.
            if (antiAir < 1 && actions < 2)
            {
                string orderId;
                bool reserved = IA01MilitaryProductionGuard.TryReserveSingle(context.TeamId, IA01MilitaryAssetKind.AntiAir, antiAir, now, 45f, out orderId);
                bool produced = reserved && TryProduceLand(FindAntiAir(), "Ares_Ar antiaereo", orderId);
                if (reserved && !produced) IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.AntiAir, now);
                changed |= produced;
                if (produced) actions++;
            }

            if (fighters + issuedFighters < targetFighters && actions < 2)
            {
                string orderId;
                bool reserved = IA01MilitaryProductionGuard.TryReserve(context.TeamId, IA01MilitaryAssetKind.Fighter, targetFighters, fighters, now, 120f, out orderId);
                bool produced = reserved && TryProduceFighter(FindFighter(), now, orderId);
                if (reserved && !produced) IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.Fighter, now);
                changed |= produced;
                if (produced) actions++;
            }

            // Assim que existir estaleiro/pier, a IA coloca pelo menos uma unidade naval.
            if (naval + issuedNaval < targetNaval && HasOwnNavalInfrastructure() && actions < 2)
            {
                string orderId;
                bool reserved = IA01MilitaryProductionGuard.TryReserve(context.TeamId, IA01MilitaryAssetKind.Naval, targetNaval, naval, now, 180f, out orderId);
                bool produced = reserved && TryProduceNaval(FindNaval(), "navios", orderId);
                if (reserved && !produced) IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.Naval, now);
                changed |= produced;
                if (produced) actions++;
            }

            if (fighters >= issuedFighters) issuedFighters = 0;
            if (naval >= issuedNaval) issuedNaval = 0;

            // Se a abertura ainda não conseguiu erguer a fábrica/aeroporto em 12 s,
            // cria a reserva em ponto seguro do próprio território. Isso mantém a IA
            // jogável sem espalhar construções ou unidades para o mapa adversário.
            EnsurePierThenPlatform(now);
            EnsureTankerAfterPlatform(now);
            ApplyNavalPatrols(now);
            ApplyNavalStaging(now);
            ApplyNavalCombat(now);
            ApplyAirPatrols(now);
            IAProductionDiagnostics fighterDiagnostics = IAAutoProductionRegistry.GetDiagnostics(
                context.TeamId,
                IA01MilitaryAssetKind.Fighter.ToString(),
                targetFighters,
                fighters);
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia_production_fighter", fighterDiagnostics.ToString());
            status = string.Format("Reserva militar: soldados={0}/{1} tanques={2}/{3} AA={4} cacas={5}/{6} navios={7}/{8} escalao={9}",
                soldiers, targetSoldiers, tanks, targetTanks, antiAir, fighters, targetFighters, naval, targetNaval,
                ProgressaoEscalaoAtiva ? ResolverEtapaEscalao() : -1);
            if (changed)
            {
                Debug.Log("[IA01 Military] " + status);
            }
            return changed;
        }

        private bool TemPrefeituraOperacional()
        {
            if (context == null || context.TeamId <= 0) return false;

            ComplexoGovernamental[] complexos = UnityEngine.Object.FindObjectsByType<ComplexoGovernamental>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < complexos.Length; i++)
            {
                ComplexoGovernamental complexo = complexos[i];
                if (complexo == null) continue;

                IdentidadeUnidade identidade = complexo.GetComponent<IdentidadeUnidade>();
                if (identidade == null)
                    identidade = complexo.GetComponentInChildren<IdentidadeUnidade>(true);

                if (identidade != null && identidade.teamID == context.TeamId)
                    return true;
            }

            return false;
        }

        private void EnsureOperationalWarZones()
        {
            if (warZonesEnsured || controller == null || context == null) return;

            Estaleiro shipyard = null;
            Estaleiro[] shipyards = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < shipyards.Length; i++)
            {
                if (shipyards[i] != null && BelongsToTeam(shipyards[i].gameObject))
                {
                    shipyard = shipyards[i];
                    break;
                }
            }

            if (shipyard != null)
            {
                CriarZonaGuerraSeAusente(shipyard.transform, "IA01 WarAdvanceZone Naval A", new Vector3(180f, 0f, 220f), IA01WarAdvanceZone.Dominio.Naval, 180f);
                CriarZonaGuerraSeAusente(shipyard.transform, "IA01 WarAdvanceZone Naval B", new Vector3(-220f, 0f, 300f), IA01WarAdvanceZone.Dominio.Naval, 180f);
                CriarZonaExtracaoSeAusente(shipyard.transform, "IA01 ExtractionZone Naval", new Vector3(-40f, 0f, 150f));
            }

            bool airportFound = false;
            GerenciadorAeroporto[] airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                if (airport == null || !BelongsToTeam(airport.gameObject)) continue;
                CriarZonaGuerraSeAusente(airport.transform, "IA01 WarAdvanceZone Aerea", new Vector3(0f, 100f, 320f), IA01WarAdvanceZone.Dominio.Aereo, 260f);
                airportFound = true;
                break;
            }

            warZonesEnsured = shipyard != null && airportFound;
        }

        private void CriarZonaGuerraSeAusente(Transform parent, string nome, Vector3 local, IA01WarAdvanceZone.Dominio dominio, float raio)
        {
            Transform child = parent.Find(nome);
            if (child == null)
            {
                GameObject go = new GameObject(nome);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local;
                child = go.transform;
            }
            IA01WarAdvanceZone zona = child.GetComponent<IA01WarAdvanceZone>();
            if (zona == null) zona = child.gameObject.AddComponent<IA01WarAdvanceZone>();
            zona.Configurar(context.TeamId, dominio, raio, 4);
        }

        private void CriarZonaExtracaoSeAusente(Transform parent, string nome, Vector3 local)
        {
            Transform child = parent.Find(nome);
            if (child == null)
            {
                GameObject go = new GameObject(nome);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local;
                child = go.transform;
            }
            IA01ExtractionZone zona = child.GetComponent<IA01ExtractionZone>();
            if (zona == null) zona = child.gameObject.AddComponent<IA01ExtractionZone>();
            zona.Configurar(context.TeamId, 80f, 6);
        }

        private void ResolveTargets(out int soldiers, out int tanks, out int fighters, out int naval)
        {
            soldiers = MinSoldiers; tanks = MinTanks; fighters = MinFighters; naval = MinNaval;
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(context.TeamId) : null;
            if (country == null) return;

            // Crescimento gradual: a reserva minima permanece igual, mas uma
            // nação com população, caixa e doutrina militar pode evoluir para
            // potência. Déficit de comida/energia ou crise nunca é ignorado.
            bool atWar = country.emGuerra
                || country.modoInicialIA == ModoInicialPaisIA.Mobilizacao
                || country.modoInicialIA == ModoInicialPaisIA.GuerraTotal
                || country.modoInicialIA == ModoInicialPaisIA.AgressivoContraJogador;
            bool economyHealthy = country.comida > 0 && country.energia > 0 && country.saldo >= 14000;
            if (!economyHealthy && !atWar)
            {
                int diaEconomiaFraca = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
                if (firstNavalOrderDay >= 0 && diaEconomiaFraca >= firstNavalOrderDay + 2)
                {
                    naval = 2;
                }
                return;
            }

            int populationTier = Mathf.Clamp(country.populacao / 5000, 0, 6);
            int treasuryTier = Mathf.Clamp((int)Math.Max(0L, Math.Min(5L, (country.saldo - 14000L) / 9000L)), 0, 5);
            int militaryTier = Mathf.Clamp(Mathf.RoundToInt(country.nivelMilitar / 25f), 0, 4);
            int doctrineTier = Mathf.Clamp(Mathf.RoundToInt(country.pesoMilitarismo * 3f), 0, 3);
            bool powerDoctrine = country.perfilIA == PerfilPaisIA.Militarista
                || country.perfilIA == PerfilPaisIA.Rival
                || country.nivelMilitar >= 70
                || country.pesoMilitarismo >= 0.70f;
            int expansionTier = Mathf.Clamp(
                Mathf.Max(populationTier / 2, treasuryTier) + militaryTier / 2 + doctrineTier + (atWar ? 2 : 0),
                0,
                powerDoctrine ? 7 : 4);

            // Cada dano sofrido aumenta a pressão de guerra em degraus. A meta
            // cresce por ciclo, mas continua limitada por população e orçamento;
            // isso impede tanto a passividade após perder navios quanto a
            // duplicação desenfreada observada nos prefabs antigos.
            int surge = atWar && controller != null ? Mathf.Clamp(controller.WarEscalationLevel, 0, 6) : 0;
            expansionTier = Mathf.Clamp(expansionTier + surge, 0, powerDoctrine ? 10 : 7);

            soldiers += expansionTier * 2 + (powerDoctrine ? 2 : 0);
            tanks += expansionTier + (powerDoctrine ? 1 : 0);
            fighters += Mathf.Max(0, expansionTier / 2) + (powerDoctrine ? 1 : 0);
            naval += expansionTier >= 3 ? 1 : 0;

            // População e orçamento limitam a expansão física. Isso evita que
            // uma duplicação de identidade vire dezenas de caças na mesma base.
            int mobilizable = Mathf.Max(6, country.populacaoCivil > 0 ? country.populacaoCivil / 900 : country.populacao / 900);
            if (atWar)
            {
                // Guerra muda a postura: mesmo com a populaÃ§Ã£o inicial baixa,
                // a naÃ§Ã£o mobiliza uma reserva de emergÃªncia e repÃµe perdas.
                // O limite continua pequeno e depende do nÃ­vel de escalada, por
                // isso nÃ£o cria dezenas de ordens duplicadas no mesmo frame.
                mobilizable = Mathf.Max(mobilizable, 24 + surge * 8);
                soldiers = Mathf.Max(soldiers, 8 + surge * 3);
                tanks = Mathf.Max(tanks, 4 + surge);
                fighters = Mathf.Max(fighters, 3 + Mathf.CeilToInt(surge * 0.5f));
                naval = Mathf.Max(naval, 2 + Mathf.CeilToInt(surge * 0.5f));
            }
            soldiers = Mathf.Min(soldiers, Mathf.Max(MinSoldiers, mobilizable));
            tanks = Mathf.Min(tanks, Mathf.Max(MinTanks, 3 + mobilizable / 8));
            fighters = Mathf.Min(fighters, Mathf.Max(MinFighters, 2 + mobilizable / 10));
            naval = Mathf.Min(naval, Mathf.Max(MinNaval, 1 + mobilizable / 14 + surge / 2));

            // O segundo navio do escalao inicial so fica elegivel dois dias
            // apos a primeira compra confirmada. Mantemos a reserva inicial
            // de um navio para nao concentrar duas construcoes no mesmo frame.
            int diaAtual = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            if (firstNavalOrderDay >= 0 && diaAtual >= firstNavalOrderDay + 2)
            {
                naval = Mathf.Max(naval, 2);
            }

            int tier = ProgressaoEscalaoAtiva ? Mathf.Clamp(ResolverEtapaEscalao(), 0, 4) : 0;
            soldiers = Mathf.Min(soldiers, MaxSoldiersByTier[tier]);
            tanks = Mathf.Min(tanks, MaxTanksByTier[tier]);
            fighters = Mathf.Min(fighters, MaxFightersByTier[tier]);
            naval = Mathf.Min(naval, MaxNavalByTier[tier]);
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
            int logisticsTankers = 0;
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade id = identities[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Naval) continue;

                // Estaleiros/pieres podem carregar uma IdentidadeUnidade para
                // registro, mas não são navios. O petroleiro tem uma máquina
                // própria de rota plataforma -> píer e não pode receber a
                // patrulha de combate genérica, pois isso interromperia o
                // carregamento/descarregamento de petróleo.
                if (IsNavalStructure(id)) continue;
                if (IsLogisticsTanker(id))
                {
                    logisticsTankers++;
                    continue;
                }

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
                // Consome o dia somente quando ja existe embarcacao elegivel.
                // O ciclo inicial pode ocorrer antes da saida do estaleiro.
                if (currentDay > 0 && (assigned > 0 || alreadyPatrolling > 0))
                {
                    lastNavalPatrolDay = currentDay;
                }
                if (EmitirLogsDetalhadosDePatrulha)
                {
                    Debug.Log(string.Format("[IA01 Military] Patrulha naval: candidatos={0} atribuídos={1} jáPatrulhando={2} recusados={3} petroleirosLogistica={4}",
                        candidates, assigned, alreadyPatrolling, rejected, logisticsTankers));
                }
            }
            else
            {
                int navalIdentities = 0;
                for (int i = 0; i < identities.Length; i++)
                {
                    IdentidadeUnidade id = identities[i];
                    if (id != null
                        && id.tipoUnidade == TipoUnidade.Naval
                        && !IsNavalStructure(id)
                        && !IsLogisticsTanker(id))
                    {
                        navalIdentities++;
                    }
                }
                if (navalIdentities > 0 && now >= nextUnlinkedNavalWarningAt)
                {
                    nextUnlinkedNavalWarningAt = now + 30f;
                    Debug.LogWarning(string.Format("[IA01 Military] Navios encontrados sem vínculo com o time {0}: identidades navais={1}",
                        context.TeamId, navalIdentities));
                }
            }
        }

        private void ApplyNavalCombat(float now)
        {
            if (now < nextNavalCombatAt || context == null) return;
            nextNavalCombatAt = now + ResolveEtapaEscalaoCombatInterval();

            RegistroEntidadesJogo.FillUnidades(navalUnitsBuffer);
            IA01WorldState world = controller != null && controller.Runtime != null
                ? controller.Runtime.WorldState
                : null;
            int limiteEngajamentos = Mathf.Clamp(ResolverEtapaEscalao() + 1, 1, 4);
            int engajados = 0;
            int lancadoresAtivos = 0;
            for (int i = 0; i < navalUnitsBuffer.Count && engajados < limiteEngajamentos; i++)
            {
                IdentidadeUnidade identidade = navalUnitsBuffer[i];
                if (identidade == null || identidade.teamID != context.TeamId
                    || identidade.tipoUnidade != TipoUnidade.Naval
                    || IsNavalStructure(identidade) || IsLogisticsTanker(identidade))
                {
                    continue;
                }

                ControleUnidade controle = identidade.GetComponent<ControleUnidade>()
                    ?? identidade.GetComponentInParent<ControleUnidade>()
                    ?? identidade.GetComponentInChildren<ControleUnidade>(true);
                if (controle == null) continue;

                LancadorNaval lancador = controle.GetComponentInChildren<LancadorNaval>(true)
                    ?? controle.GetComponentInParent<LancadorNaval>();
                if (lancador != null)
                {
                    lancador.ConfigurarPerfilIA();
                    lancadoresAtivos++;
                }

                Transform alvo = EncontrarInimigoNavalMaisProximo(identidade.transform, world);
                if (alvo == null) continue;

                // A ordem oficial de combate liga torpedos e demais sistemas;
                // o launcher naval continua responsável pela seleção de alvo,
                // banco de dano projetado e cadencia de cada missil.
                controle.DefinirModoCombate(true);
                ControleNavioRealista navio = identidade.GetComponent<ControleNavioRealista>()
                    ?? identidade.GetComponentInParent<ControleNavioRealista>()
                    ?? identidade.GetComponentInChildren<ControleNavioRealista>(true);
                if (navio != null)
                {
                    navio.DefinirDestinoAtaqueLateral(alvo.position, 160f, 100f);
                }
                engajados++;
                DiagnosticoDesempenhoJogo.RegistrarEvento("IA01_NavalCombat", identidade.name + " -> " + alvo.name);
            }

            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("ia01_naval_engaged", engajados);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("ia01_naval_launchers_active", lancadoresAtivos);
        }

        private float ResolveEtapaEscalaoCombatInterval()
        {
            // A reserva militar ja e fatiada por ciclo; niveis iniciais usam
            // intervalos mais largos para manter a campanha leve.
            switch (Mathf.Clamp(ResolverEtapaEscalao(), 0, 4))
            {
                case 0: return 3.0f;
                case 1: return 2.5f;
                case 2: return 2.0f;
                default: return 1.6f;
            }
        }

        private Transform EncontrarInimigoNavalMaisProximo(Transform origem, IA01WorldState world)
        {
            if (origem == null || world == null || world.EnemyUnits == null) return null;
            float alcanceSqr = 900f * 900f;
            float melhor = alcanceSqr;
            Transform selecionado = null;
            for (int i = 0; i < world.EnemyUnits.Count; i++)
            {
                IdentidadeUnidade inimigo = world.EnemyUnits[i];
                if (inimigo == null || inimigo.tipoUnidade != TipoUnidade.Naval || !inimigo.gameObject.activeInHierarchy)
                    continue;
                if (inimigo.GetComponentInParent<NavioPetroleiro>() != null
                    || inimigo.GetComponentInChildren<NavioPetroleiro>(true) != null)
                    continue;
                Vector3 delta = inimigo.transform.position - origem.position;
                delta.y = 0f;
                float distancia = delta.sqrMagnitude;
                if (distancia < melhor)
                {
                    melhor = distancia;
                    selecionado = inimigo.transform;
                }
            }
            return selecionado;
        }

        private void ApplyAirPatrols(float now)
        {
            if (now < nextAirPatrolScanAt) return;
            nextAirPatrolScanAt = now + 4f;
            airPatrolCreatesBuffer.Clear();
            if (!TryObterCreatesPatrulhaAerea(airPatrolCreatesBuffer))
            {
                EnsureAirPatrolCreates();
                TryObterCreatesPatrulhaAerea(airPatrolCreatesBuffer);
            }
            if (airPatrolCreatesBuffer.Count < 4) return;

            RegistroEntidadesJogo.FillUnidades(airUnitsBuffer);
            int candidates = 0;
            int assigned = 0;
            for (int i = 0; i < airUnitsBuffer.Count; i++)
            {
                IdentidadeUnidade id = airUnitsBuffer[i];
                if (id == null || id.teamID != context.TeamId || id.tipoUnidade != TipoUnidade.Aereo) continue;
                ControleUnidade control = id.GetComponent<ControleUnidade>()
                    ?? id.GetComponentInParent<ControleUnidade>()
                    ?? id.GetComponentInChildren<ControleUnidade>(true);
                ControleAviao aviao = id.GetComponent<ControleAviao>()
                    ?? id.GetComponentInParent<ControleAviao>()
                    ?? id.GetComponentInChildren<ControleAviao>(true);
                if (control == null || aviao == null || !EhAeroportoMilitar(aviao.aeroportoOrigem)) continue;
                if (control.OrdemAtual == OrdemControleUnidade.Patrulhando) continue;
                if (aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) continue;
                candidates++;

                Vector3[] route = CriarRotaDosCreates(airPatrolCreatesBuffer, assigned % 4);
                if (control.EmitirOrdemPatrulha(route))
                {
                    assigned++;
                    DiagnosticoDesempenhoJogo.RegistrarEvento("IA01_AirPatrol", id.name + " patrulha nos 4 Creates militares");
                    if (EmitirLogsDetalhadosDePatrulha)
                    {
                        Debug.Log("[IA01 Military] Patrulha aerea: " + id.name + " -> ciclo Create 01/02/03/04");
                    }
                }
            }
            if (candidates > 0)
            {
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("ia01_air_patrol_assigned", assigned);
                if (EmitirLogsDetalhadosDePatrulha)
                {
                    Debug.Log("[IA01 Military] Patrulha aerea: candidatos=" + candidates + " | atribuídos=" + assigned + " | rota=Create 01/02/03/04");
                }
            }
        }

        private bool TryObterCreatesPatrulhaAerea(List<IA01AirPatrolZone> destino)
        {
            if (destino == null) return false;
            destino.Clear();
            GerenciadorAeroporto[] airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                if (airport == null || !BelongsToOwnAirport(airport) || !EhAeroportoMilitar(airport)) continue;
                Transform grupo = airport.transform.Find("IA01 Creates Patrulha Aerea");
                if (grupo == null) continue;
                for (int ponto = 1; ponto <= 4; ponto++)
                {
                    Transform create = grupo.Find("Create Patrulha Aerea " + ponto.ToString("00"));
                    IA01AirPatrolZone zone = create != null ? create.GetComponent<IA01AirPatrolZone>() : null;
                    if (zone != null) destino.Add(zone);
                }
                return destino.Count >= 4;
            }

            // A abertura da IA pode ainda estar usando o create de infraestrutura
            // (IA01AirportBuildSlot), antes de o prefab do GerenciadorAeroporto
            // ser instanciado. Os Creates continuam válidos nesse caso e devem
            // ser descobertos no próprio aeroporto da IA, nunca em um aeroporto
            // genérico ou fora do território.
            IA01AirportBuildSlot[] airportSlots = UnityEngine.Object.FindObjectsByType<IA01AirportBuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airportSlots.Length; i++)
            {
                IA01AirportBuildSlot airport = airportSlots[i];
                IA01BuildSlot slot = airport != null ? airport.GetComponent<IA01BuildSlot>() : null;
                if (airport == null || slot == null || slot.OwnerTeamId != context.TeamId || !EhAeroportoMilitar(airport, slot)) continue;
                Transform grupo = airport.transform.Find("IA01 Creates Patrulha Aerea");
                if (grupo == null) continue;
                for (int ponto = 1; ponto <= 4; ponto++)
                {
                    Transform create = grupo.Find("Create Patrulha Aerea " + ponto.ToString("00"));
                    IA01AirPatrolZone zone = create != null ? create.GetComponent<IA01AirPatrolZone>() : null;
                    if (zone != null) destino.Add(zone);
                }
                return destino.Count >= 4;
            }
            return false;
        }

        private int EnsureAirPatrolCreates()
        {
            Vector3[] offsets =
            {
                new Vector3(-620f, 0f, 460f),
                new Vector3(620f, 0f, 460f),
                new Vector3(620f, 0f, -460f),
                new Vector3(-620f, 0f, -460f)
            };
            GerenciadorAeroporto[] airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airports.Length; i++)
            {
                GerenciadorAeroporto airport = airports[i];
                if (airport == null || !BelongsToOwnAirport(airport) || !EhAeroportoMilitar(airport)) continue;

                Transform grupo = airport.transform.Find("IA01 Creates Patrulha Aerea");
                if (grupo == null)
                {
                    grupo = new GameObject("IA01 Creates Patrulha Aerea").transform;
                    grupo.SetParent(airport.transform, false);
                }

                for (int ponto = 1; ponto <= 4; ponto++)
                {
                    string nome = "Create Patrulha Aerea " + ponto.ToString("00");
                    Transform create = grupo.Find(nome);
                    if (create == null)
                    {
                        create = new GameObject(nome).transform;
                        create.SetParent(grupo, false);
                        create.localPosition = offsets[ponto - 1];
                    }
                    IA01AirPatrolZone zone = create.GetComponent<IA01AirPatrolZone>();
                    if (zone == null) zone = create.gameObject.AddComponent<IA01AirPatrolZone>();
                }

                return 4;
            }

            IA01AirportBuildSlot[] airportSlots = UnityEngine.Object.FindObjectsByType<IA01AirportBuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < airportSlots.Length; i++)
            {
                IA01AirportBuildSlot airport = airportSlots[i];
                IA01BuildSlot slot = airport != null ? airport.GetComponent<IA01BuildSlot>() : null;
                if (airport == null || slot == null || slot.OwnerTeamId != context.TeamId || !EhAeroportoMilitar(airport, slot)) continue;

                Transform grupo = airport.transform.Find("IA01 Creates Patrulha Aerea");
                if (grupo == null)
                {
                    grupo = new GameObject("IA01 Creates Patrulha Aerea").transform;
                    grupo.SetParent(airport.transform, false);
                }

                for (int ponto = 1; ponto <= 4; ponto++)
                {
                    string nome = "Create Patrulha Aerea " + ponto.ToString("00");
                    Transform create = grupo.Find(nome);
                    if (create == null)
                    {
                        create = new GameObject(nome).transform;
                        create.SetParent(grupo, false);
                        create.localPosition = offsets[ponto - 1];
                    }
                    IA01AirPatrolZone zone = create.GetComponent<IA01AirPatrolZone>();
                    if (zone == null) create.gameObject.AddComponent<IA01AirPatrolZone>();
                }

                return 4;
            }
            return 0;
        }

        private static Vector3[] CriarRotaDosCreates(IList<IA01AirPatrolZone> creates, int inicio)
        {
            Vector3[] rota = new Vector3[4];
            for (int i = 0; i < rota.Length; i++)
            {
                rota[i] = creates[(inicio + i) % creates.Count].ObterPontoPatrulha();
            }
            return rota;
        }

        private static bool EhAeroportoMilitar(GerenciadorAeroporto airport)
        {
            if (airport == null) return false;
            if (airport.patioMilitar != null || airport.prefabSu11 != null) return true;
            if (airport.transform.Find("Patio_Militar") != null || airport.transform.Find("PatioMilitar") != null) return true;
            return airport.name.IndexOf("militar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EhAeroportoMilitar(IA01AirportBuildSlot airport, IA01BuildSlot slot)
        {
            if (airport == null || slot == null) return false;
            return slot.SlotId.IndexOf("militar", StringComparison.OrdinalIgnoreCase) >= 0
                || airport.name.IndexOf("militar", StringComparison.OrdinalIgnoreCase) >= 0;
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
                if (IsNavalStructure(id) || IsLogisticsTanker(id)) continue;
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
                    if (EmitirLogsDetalhadosDePatrulha)
                    {
                        Debug.Log("[IA01 Military] Navio em ponto costeiro otimizado: " + id.name + " -> " + ponto.ToString("F2"));
                    }
                    if (staged >= 2) break;
                }
            }
        }

        private bool IsNavalStructure(IdentidadeUnidade identity)
        {
            if (identity == null) return false;
            return identity.GetComponentInParent<Estaleiro>() != null
                || identity.GetComponentInParent<PierMarinha>() != null;
        }

        private bool IsLogisticsTanker(IdentidadeUnidade identity)
        {
            if (identity == null) return false;
            return identity.GetComponent<NavioPetroleiro>() != null
                || identity.GetComponentInParent<NavioPetroleiro>() != null
                || identity.GetComponentInChildren<NavioPetroleiro>(true) != null;
        }

        private void EnsurePierThenPlatform(float now)
        {
            if (!PermiteInfraestruturaInicialAutomatica) return;
            PierMarinha[] piers = UnityEngine.Object.FindObjectsByType<PierMarinha>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool ownPier = false;
            for (int i = 0; i < piers.Length; i++)
            {
                if (piers[i] != null && BelongsToTeam(piers[i].gameObject)) { ownPier = true; break; }
            }

            if (!ownPier && now >= nextPierRecoveryAt)
            {
                if (buildDirector != null && buildDirector.IsRebuildBlocked(IA01IntentType.BuildPier, now))
                {
                    nextPierRecoveryAt = now + 5f;
                    return;
                }

                nextPierRecoveryAt = now + 18f;
                BuildCoastalStructure(IA01IntentType.BuildPier, "pier", "dock");
                return;
            }
            if (!ownPier || now < nextPlatformRecoveryAt) return;

            if (buildDirector != null && buildDirector.IsRebuildBlocked(IA01IntentType.BuildOffshorePlatform, now))
            {
                // O local pode continuar sob ameaça naval; não cria outra
                // plataforma no mesmo ponto enquanto o inimigo a guarda.
                nextPlatformRecoveryAt = now + 5f;
                return;
            }

            PlataformaOffshore[] platforms = UnityEngine.Object.FindObjectsByType<PlataformaOffshore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
            {
                if (platforms[i] != null && BelongsToTeam(platforms[i].gameObject)) return;
            }
            nextPlatformRecoveryAt = now + 35f;
            BuildCoastalStructure(IA01IntentType.BuildOffshorePlatform, "plataforma", "offshore");
        }

        /// <summary>
        /// O petroleiro só entra na fila depois que a plataforma offshore da
        /// própria IA foi confirmada. A espera representa um dia de operação
        /// (60 segundos no ritmo atual da partida), evitando que estaleiro,
        /// pier, plataforma e navio nasçam todos no mesmo instante.
        /// </summary>
        private void EnsureTankerAfterPlatform(float now)
        {
            if (!PermiteInfraestruturaInicialAutomatica) return;

            PlataformaOffshore plataformaPropria = null;
            PlataformaOffshore[] plataformas = UnityEngine.Object.FindObjectsByType<PlataformaOffshore>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < plataformas.Length; i++)
            {
                PlataformaOffshore candidata = plataformas[i];
                if (candidata != null && BelongsToTeam(candidata.gameObject))
                {
                    plataformaPropria = candidata;
                    break;
                }
            }

            if (plataformaPropria == null)
            {
                platformConfirmedAt = -1f;
                return;
            }

            if (platformConfirmedAt < 0f)
            {
                platformConfirmedAt = now;
                Debug.Log("[IA01 Military] Plataforma de petróleo confirmada; petroleiro liberado em "
                    + TankerDelayAfterPlatformSeconds.ToString("0") + "s.");
                return;
            }

            NavioPetroleiro[] petroleiros = UnityEngine.Object.FindObjectsByType<NavioPetroleiro>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int petroleirosAtivos = 0;
            for (int i = 0; i < petroleiros.Length; i++)
            {
                if (petroleiros[i] != null && BelongsToTeam(petroleiros[i].gameObject))
                {
                    petroleirosAtivos++;
                }
            }
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("ia01_tankers_active", petroleirosAtivos);

            int petroleirosConhecidos = Mathf.Max(petroleirosAtivos, tankerOrdersIssued);
            if (petroleirosConhecidos >= 4) return;
            int diaAtual = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            if (tankerOrdersIssued == 0 && petroleirosAtivos > 0)
            {
                tankerOrdersIssued = petroleirosAtivos;
                lastTankerOrderDay = diaAtual;
                return;
            }
            if (tankerOrdersIssued == 0)
            {
                if (now - platformConfirmedAt < TankerDelayAfterPlatformSeconds || now < nextTankerAttemptAt)
                    return;
            }
            else if (diaAtual < lastTankerOrderDay + 5)
            {
                return;
            }

            DadosConstrucao fichaPetroleiro = FindTanker();
            if (fichaPetroleiro == null || !fichaPetroleiro.TryGetPrefabBasico(out GameObject prefab) || prefab == null)
            {
                nextTankerAttemptAt = now + 15f;
                Debug.LogWarning("[IA01 Military] Plataforma confirmada, mas ficha de navio petroleiro ainda nao foi localizada.");
                return;
            }

            Estaleiro[] estaleiros = UnityEngine.Object.FindObjectsByType<Estaleiro>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!IA01MilitaryProductionGuard.TryReserveSingle(
                context.TeamId,
                IA01MilitaryAssetKind.OilTanker,
                petroleirosAtivos,
                now,
                90f,
                out string tankerOrderId))
            {
                return;
            }
            for (int i = 0; i < estaleiros.Length; i++)
            {
                Estaleiro estaleiro = estaleiros[i];
                if (estaleiro == null || !BelongsToTeam(estaleiro.gameObject)) continue;
                if (!estaleiro.ConstruirUnidade(prefab, tankerOrderId)) continue;
                IA01MilitaryProductionGuard.ConfirmQueued(tankerOrderId, estaleiro.GetInstanceID(), now);

                tankerOrdersIssued++;
                lastTankerOrderDay = diaAtual;
                Debug.Log("[IA01 Military] Navio petroleiro iniciado em cadencia segura: "
                    + estaleiro.name + " -> " + prefab.name);
                return;
            }

            IA01MilitaryProductionGuard.Cancel(context.TeamId, IA01MilitaryAssetKind.OilTanker, now);
            nextTankerAttemptAt = now + 15f;
        }

        private bool BuildCoastalStructure(IA01IntentType intent, params string[] blueprintTokens)
        {
            if (!PermiteInfraestruturaInicialAutomatica) return false;
            Vector3 anchor;
            Quaternion rotation;
            if (!controller.TryResolveConstructionAnchor(intent, out anchor, out rotation)) return false;
            if (!controller.IsPositionInsidePreparedTerritory(anchor, 260f)) return false;
            DadosConstrucao blueprint = FindStructureBlueprint(blueprintTokens);
            if (blueprint == null || !blueprint.TryGetPrefabBasico(out GameObject prefab) || prefab == null) return false;

            // A abertura roteirizada pode usar o financiamento inicial já
            // existente. Quando isto é uma reconstrução após perda, porém,
            // ela deve respeitar exatamente o mesmo saldo da construção normal.
            bool isRebuild = buildDirector != null && buildDirector.HasRecordedRebuild(intent);
            long rebuildCost = blueprint.ObterPrecoEfetivo();
            bool rebuildPaid = false;
            if (isRebuild && rebuildCost > 0)
            {
                SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
                if (government == null || !government.TentarPagar(context.TeamId, rebuildCost))
                {
                    return false;
                }

                rebuildPaid = true;
            }

            if (prefab.GetComponent<PlataformaOffshore>() != null)
                anchor.y = NavalPlacementResolver.ResolveSeaLevel();
            Construtor builder = Construtor.Instancia != null ? Construtor.Instancia : UnityEngine.Object.FindFirstObjectByType<Construtor>();
            GameObject built = builder != null ? builder.ConstruirEstruturaIA(prefab, anchor, rotation) : UnityEngine.Object.Instantiate(prefab, anchor, rotation);
            if (built == null)
            {
                if (rebuildPaid && SistemaGovernoMundial.Instancia != null)
                {
                    SistemaGovernoMundial.Instancia.AdicionarSaldo(context.TeamId, rebuildCost);
                }

                return false;
            }
            built.transform.SetPositionAndRotation(anchor, rotation);
            IdentidadeUnidade identity = built.GetComponent<IdentidadeUnidade>() ?? built.AddComponent<IdentidadeUnidade>();
            identity.teamID = context.TeamId;
            identity.nomeDoPais = controller.NationName;
            identity.tipoUnidade = TipoUnidade.Estrutura;
            IA01BuildExecutor.NormalizeStructureIdentity(built, context.TeamId, controller.NationName);
            PlataformaOffshore platform = built.GetComponent<PlataformaOffshore>();
            if (platform != null) platform.OwnerTeamId = context.TeamId;
            Estaleiro shipyard = built.GetComponent<Estaleiro>();
            if (shipyard != null) shipyard.OwnerTeamId = context.TeamId;
            PierMarinha pier = built.GetComponent<PierMarinha>();
            if (pier != null) pier.OwnerTeamId = context.TeamId;
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
                // A identidade gravada na unidade e a fonte de verdade da
                // propriedade. Nunca adote uma embarcacao de outro time
                // apenas porque ela saiu perto do porto da IA. Corrija
                // somente identidades realmente ausentes (team 0).
            // Petroleiros sao logistica: nunca podem ser incorporados a
            // patrulha da IA, mesmo quando uma unidade legada ainda estiver
            // sem teamID. A propria rotina NavioPetroleiro resolve a rota
            // plataforma -> pier do seu dono.
            if (id == null || id.tipoUnidade != TipoUnidade.Naval || id.teamID > 0 || IsLogisticsTanker(id)) continue;
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
            IReadOnlyList<DadosConstrucao> configuredMilitary = controller != null
                ? controller.FichasMilitaresPermitidas
                : null;

            if (configuredMilitary != null && configuredMilitary.Count > 0)
            {
                for (int i = 0; i < configuredMilitary.Count; i++)
                    AddCandidate(configuredMilitary[i], configuredMilitary);
            }
            else if (MenuConstrucao.catalogoGlobal != null)
            {
                // Controllers criados em runtime não têm referências
                // serializadas. Ainda assim, só aceitam os IDs da política
                // padrão; o catálogo global não é expandido por Resources.
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                    AddCandidate(MenuConstrucao.catalogoGlobal[i], null);
            }
        }

        private void AddCandidate(DadosConstrucao item, IReadOnlyList<DadosConstrucao> configuredMilitary)
        {
            if (item == null || catalog.Contains(item)) return;
            if (!IA01MilitaryCatalogPolicy.IsAllowed(item, configuredMilitary)) return;
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
                && !IsAircraftDefinition(item)
                && Contains(item, "tanque", "tank", "blindado", "veiculo", "vehicle", "carro"));
        }

        private DadosConstrucao FindAntiAir()
        {
            return FindUnit(item => item != null
                && item.categoria == DadosConstrucao.CategoriaItem.Exercito
                && (SistemaGastosMilitares.EhAresAr(item.GetDisplayName() + " " + item.name + " " + item.aliases)
                    || Contains(item, "antiaereo", "anti aereo", "defesa aerea", "ares")));
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

        private DadosConstrucao FindTanker()
        {
            return FindUnit(item =>
            {
                if (item.categoria != DadosConstrucao.CategoriaItem.Marinha) return false;
                if (Contains(item, "petroleiro", "tanker", "oil tanker")) return true;
                try
                {
                    return item.TryGetPrefabBasico(out GameObject prefab)
                        && prefab != null
                        && (prefab.GetComponent<NavioPetroleiro>() != null
                            || prefab.GetComponentInChildren<NavioPetroleiro>(true) != null);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        private DadosConstrucao FindUnit(Func<DadosConstrucao, bool> predicate)
        {
            if (!ProgressaoEscalaoAtiva)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    DadosConstrucao item = catalog[i];
                    if (item != null && predicate(item)) return item;
                }
                return null;
            }

            tierCandidates.Clear();
            for (int i = 0; i < catalog.Count; i++)
            {
                DadosConstrucao item = catalog[i];
                if (item != null && predicate(item)) tierCandidates.Add(item);
            }
            if (tierCandidates.Count == 0) return null;

            tierCandidates.Sort(CompararEscalaoFracoParaForte);
            int etapa = ResolverEtapaEscalao();
            bool existeClassificado = false;
            for (int i = 0; i < tierCandidates.Count; i++)
            {
                if (tierCandidates[i].escalaPoder != DadosConstrucao.EscalaPoder.NaoClassificado)
                {
                    existeClassificado = true;
                    break;
                }
            }

            // Escolhe o melhor nivel desbloqueado, e nao apenas o item na
            // posicao "etapa" da lista. Assim uma ficha D/C/B ausente nao faz
            // a IA saltar para A/S por acidente; fichas sem classificacao ficam
            // apenas como fallback de catalogos legados.
            DadosConstrucao escolhido = null;
            int melhorRank = -1;
            for (int i = 0; i < tierCandidates.Count; i++)
            {
                DadosConstrucao candidato = tierCandidates[i];
                if (existeClassificado && candidato.escalaPoder == DadosConstrucao.EscalaPoder.NaoClassificado)
                {
                    continue;
                }

                int rank = ResolverRankEscalao(candidato);
                if (rank <= etapa && rank > melhorRank)
                {
                    escolhido = candidato;
                    melhorRank = rank;
                }
            }

            if (escolhido != null) return escolhido;

            // Se o catalogo daquela categoria nao possui uma ficha nos niveis
            // inferiores, usa a menor ficha classificada disponivel; assim a
            // ausencia de D/C nao deixa a reserva vazia nem salta diretamente
            // para a ficha mais forte.
            for (int i = 0; i < tierCandidates.Count; i++)
            {
                if (!existeClassificado || tierCandidates[i].escalaPoder != DadosConstrucao.EscalaPoder.NaoClassificado)
                {
                    return tierCandidates[i];
                }
            }

            return tierCandidates[0];
        }

        private int ResolverEtapaEscalao()
        {
            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null && context != null
                ? government.ObterPais(context.TeamId)
                : null;
            if (country == null) return 0;

            int etapa = 0;
            int dia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 0;
            bool economiaSaudavel = country.saldo >= 18000 && country.comida > 0 && country.energia > 0;
            if (economiaSaudavel && (dia >= 2 || country.saldo >= 28000)) etapa = 1;
            if (economiaSaudavel && country.saldo >= 45000 && country.nivelIndustrial >= 45 && (dia >= 5 || country.nivelEconomico >= 55)) etapa = 2;
            if (economiaSaudavel && country.saldo >= 90000 && country.nivelIndustrial >= 65 && (dia >= 10 || country.nivelEconomico >= 70)) etapa = 3;

            bool emGuerra = country.emGuerra
                || country.modoInicialIA == ModoInicialPaisIA.Mobilizacao
                || country.modoInicialIA == ModoInicialPaisIA.GuerraTotal
                || country.modoInicialIA == ModoInicialPaisIA.AgressivoContraJogador;
            if (emGuerra && controller != null && controller.AllowMilitaryTierAdvancement)
            {
                etapa += Mathf.Clamp(controller.WarEscalationLevel / 2, 0, 2);
            }
            return Mathf.Clamp(etapa, 0, 4);
        }

        private static int CompararEscalaoFracoParaForte(DadosConstrucao a, DadosConstrucao b)
        {
            int rankA = ResolverRankEscalao(a);
            int rankB = ResolverRankEscalao(b);
            int comparacao = rankA.CompareTo(rankB);
            if (comparacao != 0) return comparacao;
            int precoA = a != null ? Mathf.Max(0, a.preco) : int.MaxValue;
            int precoB = b != null ? Mathf.Max(0, b.preco) : int.MaxValue;
            comparacao = precoA.CompareTo(precoB);
            if (comparacao != 0) return comparacao;
            string nomeA = a != null ? a.GetDisplayName() : string.Empty;
            string nomeB = b != null ? b.GetDisplayName() : string.Empty;
            return string.CompareOrdinal(nomeA, nomeB);
        }

        private static int ResolverRankEscalao(DadosConstrucao item)
        {
            if (item == null) return 0;
            switch (item.escalaPoder)
            {
                // A ficha usa S=1 e D=5. Aqui a leitura e invertida para
                // representar a compra natural: D/C/B -> A -> S.
                case DadosConstrucao.EscalaPoder.D: return 0;
                case DadosConstrucao.EscalaPoder.C: return 1;
                case DadosConstrucao.EscalaPoder.B: return 2;
                case DadosConstrucao.EscalaPoder.A: return 3;
                case DadosConstrucao.EscalaPoder.S: return 4;
                default: return 2;
            }
        }

        private bool TryProduceLand(DadosConstrucao item, string label, string orderId = "")
        {
            if (item == null || item.PrefabDaUnidade == null
                || item.categoria != DadosConstrucao.CategoriaItem.Exercito
                || IsAircraftDefinition(item)) return false;
            Fabrica[] factories = UnityEngine.Object.FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
            for (int i = 0; i < factories.Length; i++)
            {
                Fabrica factory = factories[i];
                if (!BelongsToTeam(factory != null ? factory.gameObject : null)) continue;
                if (!IsAppropriateFactory(factory, item)) continue;
                NormalizeFactorySpawnPoints(factory);
                GameObject produced = factory.ProduzirUnidade(item.PrefabDaUnidade);
                if (produced != null)
                {
                    EnsureOwnedIdentity(produced, item);
                    IA01MilitaryProductionGuard.Complete(orderId, Time.time);
                    return true;
                }

                // Fichas/creates antigos podem ter uma Fabrica sem identidade ou
                // com ponto de saída inválido. Ainda assim, a ordem deve gerar a
                // unidade no próprio create da fábrica, nunca no território rival.
                if (TryEmergencySpawn(item, label, factory.transform, orderId)) return true;
            }
            return false;
        }

        private bool TryProduceFighter(DadosConstrucao item, float now, string orderId = "")
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
                if (!PermiteInfraestruturaInicialAutomatica) return false;
                GerenciadorAeroporto recovered = EnsureOwnMilitaryAirport();
                if (recovered != null)
                    airports = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            if (airports.Length == 0)
            {
                return SpawnFighterAtMilitaryAirport(item, orderId);
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
                GameObject aircraft = item != null ? item.PrefabDaUnidade : null;
                if (!IsUsableAircraftPrefab(aircraft) && item != null)
                {
                    item.TryGetPrefabBasico(out aircraft);
                }
                // Fichas antigas de caça podem apontar para um objeto vazio ou
                // para um placeholder. Nesse caso a referência do próprio
                // aeroporto é a mesma usada pela compra do jogador.
                if (!IsUsableAircraftPrefab(aircraft)) aircraft = airport.prefabSu11;
                if (!IsUsableAircraftPrefab(aircraft))
                {
                    status = "Aeroporto militar sem prefab de caca configurado.";
                    continue;
                }
                airport.ComprarAviaoIAImediato(aircraft, orderId);
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
            return PermiteInfraestruturaInicialAutomatica && SpawnFighterAtMilitaryAirport(item, orderId);
        }

        private static bool IsUsableAircraftPrefab(GameObject prefab)
        {
            if (prefab == null || string.IsNullOrWhiteSpace(prefab.name)) return false;
            try
            {
                Component[] components = prefab.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null) return false;
                }
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private GerenciadorAeroporto EnsureOwnMilitaryAirport()
        {
            if (!PermiteInfraestruturaInicialAutomatica) return null;
            Vector3 anchor;
            Quaternion rotation;
            if (!controller.TryResolveConstructionAnchor(IA01IntentType.BuildMilitaryAirport, out anchor, out rotation))
            {
                Debug.LogWarning("[IA01 Military] Create de aeroporto militar nao resolvido para a IA01.");
                return null;
            }
            if (!controller.IsPositionInsidePreparedTerritory(anchor, 220f))
            {
                Debug.LogWarning("[IA01 Military] Anchor de aeroporto fora do territorio preparado; criacao cancelada.");
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
            IA01BuildExecutor.NormalizeStructureIdentity(built, context.TeamId, controller.NationName);

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

        private bool SpawnFighterAtMilitaryAirport(DadosConstrucao item, string orderId = "")
        {
            // Proteção para prefab configurado sem o componente de serviço ou
            // para cenas com aeroportos de outros países: nasce no create do
            // aeroporto da IA, nunca em ponto genérico do mapa.
            GameObject fallback = item != null ? item.PrefabDaUnidade : null;
            if (!IsUsableAircraftPrefab(fallback) && controller != null) fallback = controller.FighterPrefab;
            if (!IsUsableAircraftPrefab(fallback)
                || !IA01MilitaryCatalogPolicy.IsAllowedPrefab(fallback, controller != null ? controller.FichasMilitaresPermitidas : null))
            {
                status = "Reserva militar sem prefab de caca valido.";
                return false;
            }

            // Mesmo o caminho de recuperacao deve usar o servico oficial do
            // aeroporto. O fallback antigo instanciava apenas a identidade no
            // create: o aviao podia receber uma ordem e continuar sem
            // aeroportoOrigem, ficando preso no hangar ou sendo destruido ao
            // iniciar a sequencia de voo.
            GerenciadorAeroporto aeroportoProprio = null;
            GerenciadorAeroporto[] aeroportosAtuais = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < aeroportosAtuais.Length; i++)
            {
                GerenciadorAeroporto candidato = aeroportosAtuais[i];
                if (BelongsToOwnAirport(candidato))
                {
                    aeroportoProprio = candidato;
                    break;
                }
            }
            if (aeroportoProprio == null)
                aeroportoProprio = EnsureOwnMilitaryAirport();

            if (aeroportoProprio != null)
            {
                aeroportoProprio.gameObject.SetActive(true);
                aeroportoProprio.enabled = true;
                aeroportoProprio.SetarSemEnergia(false);
                EnsureAirportIdentity(aeroportoProprio);

                GameObject aeronave = item != null ? item.PrefabDaUnidade : null;
                if (!IsUsableAircraftPrefab(aeronave) && item != null)
                    item.TryGetPrefabBasico(out aeronave);
                if (!IsUsableAircraftPrefab(aeronave))
                    aeronave = aeroportoProprio.prefabSu11;
                if (!IsUsableAircraftPrefab(aeronave))
                    aeronave = fallback;

                if (IsUsableAircraftPrefab(aeronave)
                    && IA01MilitaryCatalogPolicy.IsAllowedPrefab(aeronave, controller != null ? controller.FichasMilitaresPermitidas : null))
                {
                    aeroportoProprio.ComprarAviaoIAImediato(aeronave, orderId);
                    issuedFighters++;
                    lastFighterOrderAt = Time.time;
                    Debug.Log("[IA01 Military] Caca estacionado no aeroporto proprio (recuperacao): "
                        + aeroportoProprio.name + " -> " + aeronave.name);
                    return true;
                }
            }
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
            if (!controller.IsPositionInsidePreparedTerritory(anchor, 220f))
            {
                status = "Reserva militar aguardando anchor seguro do aeroporto militar.";
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
            IA01MilitaryProductionGuard.Complete(orderId, Time.time);
            issuedFighters++;
            lastFighterOrderAt = Time.time;
            Debug.Log("[IA01 Military] Caca liberado no create do aeroporto militar: " + fallback.name);
            return true;
        }

        private bool BelongsToOwnAirport(GerenciadorAeroporto airport)
        {
            if (airport == null) return false;
            return BelongsToTeam(airport.gameObject);
        }

        private bool TryProduceNaval(DadosConstrucao item, string label, string orderId = "")
        {
            if (item == null || item.PrefabDaUnidade == null) return false;
            Estaleiro[] shipyards = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            for (int i = 0; i < shipyards.Length; i++)
            {
                Estaleiro shipyard = shipyards[i];
                if (BelongsToTeam(shipyard != null ? shipyard.gameObject : null) && shipyard.ConstruirUnidade(item.PrefabDaUnidade, orderId))
                {
                    IA01MilitaryProductionGuard.ConfirmQueued(orderId, shipyard.GetInstanceID(), Time.time);
                    Debug.Log("[IA01 Military] Navio enfileirado no estaleiro proprio: " + shipyard.name + " -> " + item.GetDisplayName());
                    RegistrarCompraNaval();
                    return true;
                }
            }

            // O plano civil pode ficar aguardando uma ficha terrestre inválida e
            // nunca chegar ao passo do estaleiro. Recupera somente o create naval
            // definido pelo próprio país, usando o mesmo prefab/componente do
            // jogador; assim a produção não cai em um porto aleatório.
            if (PermiteInfraestruturaInicialAutomatica && Time.time >= nextShipyardRecoveryAt)
            {
                nextShipyardRecoveryAt = Time.time + 10f;
                Estaleiro recovered = EnsureOwnShipyard();
                if (recovered != null && recovered.ConstruirUnidade(item.PrefabDaUnidade, orderId))
                {
                    IA01MilitaryProductionGuard.ConfirmQueued(orderId, recovered.GetInstanceID(), Time.time);
                    Debug.Log("[IA01 Military] Estaleiro recuperado no create e navio enfileirado: " + recovered.name);
                    RegistrarCompraNaval();
                    return true;
                }
            }
            return false;
        }

        private void RegistrarCompraNaval()
        {
            issuedNaval++;
            if (firstNavalOrderDay < 0)
            {
                firstNavalOrderDay = GerenciadorTempo.Instancia != null
                    ? GerenciadorTempo.Instancia.totalDias
                    : 0;
            }
        }

        private Estaleiro EnsureOwnShipyard()
        {
            Estaleiro[] existing = UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && BelongsToTeam(existing[i].gameObject)) return existing[i];
            }
            if (!PermiteInfraestruturaInicialAutomatica) return null;

            Vector3 anchor = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool hasAnchor = controller.TryResolveConstructionAnchor(IA01IntentType.BuildShipyard, out anchor, out rotation);
            if (!hasAnchor)
            {
                Debug.LogWarning("[IA01 Military] Create de estaleiro naval nao resolvido para a IA01.");
                return null;
            }
            if (!controller.IsPositionInsidePreparedTerritory(anchor, 220f))
            {
                Debug.LogWarning("[IA01 Military] Anchor de estaleiro fora do territorio preparado; criacao cancelada.");
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
            IA01BuildExecutor.NormalizeStructureIdentity(built, context.TeamId, controller.NationName);
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
            IReadOnlyList<DadosConstrucao> configured = controller != null ? controller.FichasDeConstrucao : null;
            if (configured != null)
            {
                for (int i = 0; i < configured.Count; i++)
                {
                    DadosConstrucao candidate = configured[i];
                    if (candidate != null && candidate.PrefabDaUnidade != null && Contains(candidate, tokens)) return candidate;
                }
            }

            if (MenuConstrucao.catalogoGlobal != null)
            {
                for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
                {
                    DadosConstrucao candidate = MenuConstrucao.catalogoGlobal[i];
                    if (candidate != null && candidate.PrefabDaUnidade != null && Contains(candidate, tokens)) return candidate;
                }
            }
            return null;
        }

        private bool TryEmergencySpawn(DadosConstrucao item, string label, Transform anchor = null, string orderId = "")
        {
            if (item == null || item.PrefabDaUnidade == null) return false;
            if (anchor == null || controller == null || !controller.IsPositionInsidePreparedTerritory(anchor.position, 240f)) return false;
            Vector3 origin = anchor != null ? anchor.position : (controller != null ? controller.transform.position : Vector3.zero);
            Vector3 position = origin + new Vector3(UnityEngine.Random.Range(-18f, 18f), 0f, UnityEngine.Random.Range(-18f, 18f));
            if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 25f, UnityEngine.AI.NavMesh.AllAreas))
                position = hit.position;
            GameObject unit = UnityEngine.Object.Instantiate(item.PrefabDaUnidade, position, Quaternion.identity);
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
            IA01MilitaryProductionGuard.Complete(orderId, Time.time);
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
            return IA01MilitaryProductionGuard.CountOwnedUnique(context.TeamId, type);
        }

        private int CountTanks()
        {
            return IA01MilitaryProductionGuard.CountOwnedUnique(context.TeamId, TipoUnidade.Veiculo, id =>
                Contains(IA_Text.Normalize(id.gameObject.name), "tank", "tanque", "blindado", "vehicle", "veiculo", "carro"));
        }

        private int CountAntiAir()
        {
            HashSet<int> roots = new HashSet<int>();
            TorretaAntiaerea[] torretas = UnityEngine.Object.FindObjectsByType<TorretaAntiaerea>(FindObjectsSortMode.None);
            for (int i = 0; i < torretas.Length; i++)
            {
                TorretaAntiaerea torreta = torretas[i];
                if (torreta == null || !torreta.gameObject.activeInHierarchy) continue;
                IdentidadeUnidade identity = torreta.GetComponentInParent<IdentidadeUnidade>();
                if (identity == null || identity.teamID != context.TeamId) continue;
                Transform root = torreta.transform.root;
                if (root != null && SistemaGastosMilitares.EhAresAr(root.name)) roots.Add(root.GetInstanceID());
            }
            return roots.Count;
        }

        private int CountFighters()
        {
            return IA01MilitaryProductionGuard.CountOwnedUnique(context.TeamId, TipoUnidade.Aereo, id =>
                id.GetComponentInChildren<ControleAviaoCaca>(true) != null
                || Contains(id.gameObject.name, "caca", "fighter", "su11", "g15", "falcon"));
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
                        && record.NativeObject == go)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsAircraftDefinition(DadosConstrucao item)
        {
            if (item == null) return false;
            if (item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
                || item.HasCapability(IA_ConstructionCapability.Air)
                || item.HasCapability(IA_ConstructionCapability.Aircraft)
                || item.HasCapability(IA_ConstructionCapability.FighterAircraft)
                || item.HasCapability(IA_ConstructionCapability.CommercialAircraft)
                || item.HasCapability(IA_ConstructionCapability.Helicopter)) return true;
            if (Contains(item, "aviao", "aeronave", "fighter", "caca", "helicopter", "helicoptero")) return true;
            try
            {
                return item.PrefabDaUnidade != null
                    && item.PrefabDaUnidade.GetComponentInChildren<ControleAviao>(true) != null;
            }
            catch (MissingReferenceException)
            {
                return true;
            }
        }

        private bool IsAppropriateFactory(Fabrica factory, DadosConstrucao item)
        {
            if (factory == null || item == null || IsAircraftDefinition(item)) return false;
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
