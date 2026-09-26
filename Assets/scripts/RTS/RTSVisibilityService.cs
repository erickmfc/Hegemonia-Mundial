using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.BrainMaster;

namespace Hegemonia.RTS
{
    public enum RTSDetectionSource
    {
        DirectVision,
        Radar,
        Sonar,
        AirRecon,
        Satellite,
        Manual
    }

    [Serializable]
    public sealed class RTSVisibilityContact
    {
        public int observerTeamId;
        public int targetInstanceId;
        public int targetSceneHandle;
        public int targetTeamId;
        public Vector3 lastKnownPosition;
        public float lastSeenAt;
        public float expiresAt;
        public float lastKnownExpiresAt;
        public RTSDetectionSource source;
        public bool currentlyVisible;
    }

    /// <summary>
    /// Fonte comum para neblina, minimapa, mapa estrategico e percepcao da IA.
    /// A primeira implementacao fornece visao direta segura e APIs para sensores
    /// especializados migrarem sem alterar as telas.
    /// </summary>
    [DefaultExecutionOrder(-7000)]
    public sealed class RTSVisibilityService : MonoBehaviour
    {
        public static RTSVisibilityService Instancia { get; private set; }

        [SerializeField, Min(0.05f)] private float scanInterval = 0.35f;
        [SerializeField, Min(1f)] private float directVisionRange = 120f;
        [SerializeField, Min(1)] private int maxUnitsPerScan = 512;
        [SerializeField, Min(0.1f)] private float memoryDuration = 18f;
        [SerializeField, Min(1f)] private float alliedRadarShareRange = 1800f;

        private readonly Dictionary<int, Dictionary<int, RTSVisibilityContact>> contactsByTeam = new Dictionary<int, Dictionary<int, RTSVisibilityContact>>();
        private readonly List<int> expiredContactIds = new List<int>(64);
        // A visibilidade direta usa o mesmo alcance para qualquer observador.
        // Organizar as unidades por celula evita comparar cada unidade com
        // todas as outras quando a partida ja acumulou muitos spawns.
        private const float DirectVisionCellSize = 120f;
        private readonly List<IdentidadeUnidade> unitsBuffer = new List<IdentidadeUnidade>(512);
        private readonly Dictionary<long, List<IdentidadeUnidade>> unitsByCell = new Dictionary<long, List<IdentidadeUnidade>>(128);
        private readonly List<RadarUnidadeTatica> radarUnitsBuffer = new List<RadarUnidadeTatica>(256);
        private readonly List<int> nearbyAlliedTeamsBuffer = new List<int>(8);
        private readonly List<int> teamsDetectingEmissionBuffer = new List<int>(8);
        private readonly Dictionary<long, float> nextRadarCounterstrikeAt = new Dictionary<long, float>(32);
        private readonly RaycastHit[] terrainRaycastHits = new RaycastHit[24];
        private float nextScanAt;

        public event Action<RTSVisibilityContact> OnContactUpdated;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanAt)
            {
                return;
            }

            nextScanAt = Time.unscaledTime + scanInterval;
            RefreshDirectContacts();
            RefreshRadarContacts();
            ExpireContacts();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (Instancia == this)
            {
                Instancia = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            contactsByTeam.Clear();
            nextRadarCounterstrikeAt.Clear();
            nextScanAt = 0f;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            foreach (Dictionary<int, RTSVisibilityContact> teamContacts in contactsByTeam.Values)
            {
                if (teamContacts == null) continue;
                expiredContactIds.Clear();
                foreach (KeyValuePair<int, RTSVisibilityContact> pair in teamContacts)
                {
                    if (pair.Value == null || pair.Value.targetSceneHandle == scene.handle)
                        expiredContactIds.Add(pair.Key);
                }

                for (int i = 0; i < expiredContactIds.Count; i++)
                    teamContacts.Remove(expiredContactIds[i]);
            }

            nextScanAt = 0f;
        }

        public bool IsVisibleToTeam(int observerTeamId, IdentidadeUnidade target)
        {
            if (target == null || target.teamID <= 0 || observerTeamId <= 0)
            {
                return false;
            }

            if (target.teamID == observerTeamId)
            {
                return true;
            }

            RTSVisibilityContact contact;
            return TryGetContact(observerTeamId, target.GetInstanceID(), out contact)
                && contact.currentlyVisible
                && contact.expiresAt >= Time.unscaledTime;
        }

        public bool TryGetLastKnownPosition(int observerTeamId, IdentidadeUnidade target, out Vector3 position)
        {
            position = Vector3.zero;
            if (target == null || observerTeamId <= 0)
            {
                return false;
            }

            RTSVisibilityContact contact;
            if (!TryGetContact(observerTeamId, target.GetInstanceID(), out contact))
            {
                return false;
            }

            position = contact.lastKnownPosition;
            return Time.unscaledTime <= contact.lastKnownExpiresAt;
        }

        public bool TryGetContactForTeam(int observerTeamId, IdentidadeUnidade target, out RTSVisibilityContact contact)
        {
            return TryGetContactForTeam(observerTeamId, target != null ? target.GetInstanceID() : 0, out contact);
        }

        public bool TryGetContactForTeam(int observerTeamId, int targetInstanceId, out RTSVisibilityContact contact)
        {
            contact = null;
            return targetInstanceId != 0 && observerTeamId > 0
                && TryGetContact(observerTeamId, targetInstanceId, out contact);
        }

        public void ReportContact(int observerTeamId, IdentidadeUnidade target, RTSDetectionSource source, float duration = -1f)
        {
            if (observerTeamId <= 0 || target == null || target.teamID <= 0 || target.teamID == observerTeamId)
            {
                return;
            }

            Dictionary<int, RTSVisibilityContact> contacts = GetOrCreateTeamContacts(observerTeamId);
            int targetId = target.GetInstanceID();
            RTSVisibilityContact contact;
            if (!contacts.TryGetValue(targetId, out contact) || contact == null)
            {
                contact = new RTSVisibilityContact
                {
                    observerTeamId = observerTeamId,
                    targetInstanceId = targetId,
                    targetSceneHandle = target.gameObject.scene.handle,
                    targetTeamId = target.teamID
                };
                contacts[targetId] = contact;
            }

            contact.targetTeamId = target.teamID;
            contact.targetSceneHandle = target.gameObject.scene.handle;
            contact.lastKnownPosition = target.transform.position;
            contact.lastSeenAt = Time.unscaledTime;
            float tempoMemoria = duration > 0f ? duration : memoryDuration;
            float tempoVisivel = source == RTSDetectionSource.DirectVision || source == RTSDetectionSource.Radar || source == RTSDetectionSource.Sonar
                ? Mathf.Max(0.5f, scanInterval * 1.5f)
                : tempoMemoria;
            contact.expiresAt = Time.unscaledTime + tempoVisivel;
            contact.lastKnownExpiresAt = Time.unscaledTime + tempoMemoria;
            contact.source = source;
            contact.currentlyVisible = true;
            OnContactUpdated?.Invoke(contact);
        }

        public int GetVisibleContactCount(int observerTeamId)
        {
            Dictionary<int, RTSVisibilityContact> contacts;
            if (!contactsByTeam.TryGetValue(observerTeamId, out contacts) || contacts == null)
            {
                return 0;
            }

            int count = 0;
            foreach (RTSVisibilityContact contact in contacts.Values)
            {
                if (contact != null && contact.currentlyVisible && contact.expiresAt >= Time.unscaledTime)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshDirectContacts()
        {
            long scanStartedAt = InfraPerformanceGameplay.MarcarInicioMedicao();
            RegistroEntidadesJogo.FillUnidades(unitsBuffer);
            int count = Mathf.Min(unitsBuffer.Count, maxUnitsPerScan);
            float rangeSqr = directVisionRange * directVisionRange;

            foreach (List<IdentidadeUnidade> cell in unitsByCell.Values)
            {
                if (cell != null)
                {
                    cell.Clear();
                }
            }

            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade unit = unitsBuffer[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.teamID <= 0)
                {
                    continue;
                }

                int cellX = Mathf.FloorToInt(unit.transform.position.x / DirectVisionCellSize);
                int cellZ = Mathf.FloorToInt(unit.transform.position.z / DirectVisionCellSize);
                long key = ComposeCellKey(cellX, cellZ);
                if (!unitsByCell.TryGetValue(key, out List<IdentidadeUnidade> cell) || cell == null)
                {
                    cell = new List<IdentidadeUnidade>(8);
                    unitsByCell[key] = cell;
                }

                cell.Add(unit);
            }

            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade observer = unitsBuffer[i];
                if (observer == null || observer.teamID <= 0 || !observer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 observerPosition = observer.transform.position;
                int observerCellX = Mathf.FloorToInt(observerPosition.x / DirectVisionCellSize);
                int observerCellZ = Mathf.FloorToInt(observerPosition.z / DirectVisionCellSize);
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                    {
                        if (!unitsByCell.TryGetValue(ComposeCellKey(observerCellX + offsetX, observerCellZ + offsetZ), out List<IdentidadeUnidade> cell)
                            || cell == null)
                        {
                            continue;
                        }

                        for (int j = 0; j < cell.Count; j++)
                        {
                            IdentidadeUnidade target = cell[j];
                            if (target == null || target == observer || target.teamID <= 0 || target.teamID == observer.teamID)
                            {
                                continue;
                            }

                            if (TeamsAtWar(observer.teamID, target.teamID)
                                && (observerPosition - target.transform.position).sqrMagnitude <= rangeSqr)
                            {
                                ReportContactToAllies(observer.teamID, target, RTSDetectionSource.DirectVision, memoryDuration);
                            }
                        }
                    }
                }
            }

            InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Sensor, scanStartedAt);
            if (scanStartedAt != 0L)
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - scanStartedAt;
                if (elapsed > 0L)
                {
                    DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(
                        "visibility_scan_ms",
                        (float)(elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                }
            }
        }

        private void RefreshRadarContacts()
        {
            radarUnitsBuffer.Clear();
            int count = Mathf.Min(unitsBuffer.Count, maxUnitsPerScan);
            int timeJogador = SistemaGovernoMundial.Instancia != null
                ? SistemaGovernoMundial.Instancia.teamJogador
                : 1;

            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade identidade = unitsBuffer[i];
                if (identidade == null || !identidade.gameObject.activeInHierarchy || identidade.teamID <= 0)
                    continue;

                RadarUnidadeTatica radar = identidade.GetComponent<RadarUnidadeTatica>();
                IA_ConstructionMetadata dados = identidade.GetComponent<IA_ConstructionMetadata>();
                bool unidadeMovel = identidade.tipoUnidade != TipoUnidade.Estrutura
                    || (dados != null && dados.IsRadar)
                    || identidade.GetComponent<ControleAviao>() != null
                    || identidade.GetComponent<ControleAviaoCaca>() != null
                    || identidade.GetComponent<Helicoptero>() != null
                    || identidade.GetComponent<VooHelicoptero>() != null
                    || identidade.GetComponent<ControleNavioRealista>() != null
                    || identidade.GetComponent<ControleSubmarino>() != null
                    || identidade.GetComponent<IdentidadeNaval>() != null
                    || identidade.GetComponent<C700TransporteAereo>() != null;
                if (!unidadeMovel) continue;

                if (radar == null)
                    radar = identidade.gameObject.AddComponent<RadarUnidadeTatica>();
                radar.AtualizarAlcance(identidade);
                radar.AtualizarDecisaoIA(identidade, unitsBuffer, timeJogador);
                if (radar.RadarLigado)
                    radar.AtualizarCustoEnergia();
                if (radar.RadarLigado) radarUnitsBuffer.Add(radar);
            }

            const float memoriaRadar = 120f;
            for (int i = 0; i < radarUnitsBuffer.Count; i++)
            {
                RadarUnidadeTatica emissor = radarUnitsBuffer[i];
                if (emissor == null) continue;
                IdentidadeUnidade observador = emissor.GetComponent<IdentidadeUnidade>();
                if (observador == null || !observador.gameObject.activeInHierarchy) continue;

                Vector3 origem = observador.transform.position;
                float alcanceSqr = emissor.AlcanceRadar * emissor.AlcanceRadar;
                RegistrarEmissaoDetectavel(observador, emissor.AlcanceRadar, memoriaRadar);
                ColetarAliadosComUnidadesProximas(observador.teamID, origem);
                for (int j = 0; j < count; j++)
                {
                    IdentidadeUnidade alvo = unitsBuffer[j];
                    if (alvo == null || alvo == observador || !alvo.gameObject.activeInHierarchy
                        || alvo.teamID <= 0 || !TeamsAtWar(observador.teamID, alvo.teamID))
                        continue;

                    Vector3 delta = alvo.transform.position - origem;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= alcanceSqr && RadarTemLinhaDeVisao(origem, observador, alvo))
                    {
                        ReportContactParaAliadosProximos(observador.teamID, alvo, memoriaRadar);
                    }
                }
            }
        }

        private void RegistrarEmissaoDetectavel(IdentidadeUnidade emissor, float alcanceRadar, float duracaoMemoria)
        {
            if (emissor == null) return;

            // A emissão ativa pode ser localizada por forças inimigas próximas,
            // mesmo quando elas ainda não detectaram visualmente o emissor.
            float alcanceInterceptacao = Mathf.Max(1400f, alcanceRadar * 1.25f);
            float alcanceSqr = alcanceInterceptacao * alcanceInterceptacao;
            Vector3 origem = emissor.transform.position;
            teamsDetectingEmissionBuffer.Clear();
            int count = Mathf.Min(unitsBuffer.Count, maxUnitsPerScan);
            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade unidadeInimiga = unitsBuffer[i];
                if (unidadeInimiga == null || unidadeInimiga == emissor
                    || !unidadeInimiga.gameObject.activeInHierarchy
                    || !TeamsAtWar(emissor.teamID, unidadeInimiga.teamID))
                    continue;

                Vector3 delta = unidadeInimiga.transform.position - origem;
                delta.y = 0f;
                if (delta.sqrMagnitude > alcanceSqr || teamsDetectingEmissionBuffer.Contains(unidadeInimiga.teamID))
                    continue;

                teamsDetectingEmissionBuffer.Add(unidadeInimiga.teamID);
            }

            for (int i = 0; i < teamsDetectingEmissionBuffer.Count; i++)
            {
                int equipeInimiga = teamsDetectingEmissionBuffer[i];
                ReportContact(equipeInimiga, emissor, RTSDetectionSource.Radar, duracaoMemoria);
                TentarContraAtaquePorCoordenada(equipeInimiga, emissor);
            }
        }

        private void TentarContraAtaquePorCoordenada(int equipeInimiga, IdentidadeUnidade emissor)
        {
            if (emissor == null || !TeamsAtWar(equipeInimiga, emissor.teamID)) return;

            long chave = ((long)equipeInimiga << 32) ^ (uint)emissor.GetInstanceID();
            float agora = Time.unscaledTime;
            if (nextRadarCounterstrikeAt.TryGetValue(chave, out float proximaTentativa)
                && agora < proximaTentativa)
                return;

            // Evita procurar armamentos a cada varredura quando a IA ainda não
            // possui unidade pronta. Uma resposta bem-sucedida fica em recarga
            // por 120 s para não transformar a emissão contínua em salvas infinitas.
            nextRadarCounterstrikeAt[chave] = agora + 3f;
            bool lancouMissil = TentarLancarMissilEstrategico(equipeInimiga, emissor);
            bool despachouAviao = TentarDespacharAviaoDeResposta(equipeInimiga, emissor);
            if (lancouMissil || despachouAviao)
                nextRadarCounterstrikeAt[chave] = agora + 120f;
        }

        private bool TentarLancarMissilEstrategico(int equipe, IdentidadeUnidade alvo)
        {
            float menorDistancia = float.PositiveInfinity;
            LancadorMisseis escolhido = null;
            for (int i = 0; i < unitsBuffer.Count; i++)
            {
                IdentidadeUnidade unidade = unitsBuffer[i];
                if (unidade == null || unidade.teamID != equipe || !unidade.gameObject.activeInHierarchy) continue;
                LancadorMisseis lancador = unidade.GetComponent<LancadorMisseis>()
                    ?? unidade.GetComponentInChildren<LancadorMisseis>(true);
                if (lancador == null || lancador.municaoAtual <= 0) continue;

                float distancia = (lancador.transform.position - alvo.transform.position).sqrMagnitude;
                if (distancia >= menorDistancia) continue;
                menorDistancia = distancia;
                escolhido = lancador;
            }

            if (escolhido != null
                && escolhido.TentarLancarCoordenado(alvo.transform.position, alvo.transform, false, out _))
                return true;

            // Se o mais próximo estiver recarregando ou sem prefab, tenta os
            // outros lançadores válidos da mesma equipe, mas para no primeiro tiro.
            for (int i = 0; i < unitsBuffer.Count; i++)
            {
                IdentidadeUnidade unidade = unitsBuffer[i];
                if (unidade == null || unidade.teamID != equipe || !unidade.gameObject.activeInHierarchy) continue;
                LancadorMisseis lancador = unidade.GetComponent<LancadorMisseis>()
                    ?? unidade.GetComponentInChildren<LancadorMisseis>(true);
                if (lancador == null || lancador == escolhido || lancador.municaoAtual <= 0) continue;
                if (lancador.TentarLancarCoordenado(alvo.transform.position, alvo.transform, false, out _)) return true;
            }

            return false;
        }

        private bool TentarDespacharAviaoDeResposta(int equipe, IdentidadeUnidade alvo)
        {
            float menorDistancia = float.PositiveInfinity;
            ControleUnidade controleEscolhido = null;
            ControleAviao aviaoEscolhido = null;
            for (int i = 0; i < unitsBuffer.Count; i++)
            {
                IdentidadeUnidade unidade = unitsBuffer[i];
                if (unidade == null || unidade.teamID != equipe || unidade.tipoUnidade != TipoUnidade.Aereo
                    || !unidade.gameObject.activeInHierarchy)
                    continue;

                ControleAviao aviao = unidade.GetComponent<ControleAviao>()
                    ?? unidade.GetComponentInChildren<ControleAviao>(true);
                ControleUnidade controle = unidade.GetComponent<ControleUnidade>()
                    ?? unidade.GetComponentInParent<ControleUnidade>()
                    ?? unidade.GetComponentInChildren<ControleUnidade>(true);
                bool temArmamentoDeAtaque = unidade.GetComponent<LancadorMisselCaca>() != null
                    || unidade.GetComponentInChildren<LancadorMisselCaca>(true) != null
                    || unidade.GetComponent<AviaoBombardeiro>() != null
                    || unidade.GetComponentInChildren<AviaoBombardeiro>(true) != null
                    || unidade.GetComponent<ControleAviaoCaca>() != null
                    || unidade.GetComponentInChildren<ControleAviaoCaca>(true) != null;
                if (aviao == null || controle == null
                    || !temArmamentoDeAtaque
                    || aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio
                    || aviao.aeroportoOrigem == null || !aviao.aeroportoOrigem.EhAeroportoMilitar()
                    || !aviao.PodeExecutarMissaoComRetorno(alvo.transform.position))
                    continue;

                float distancia = (aviao.transform.position - alvo.transform.position).sqrMagnitude;
                if (distancia >= menorDistancia) continue;
                menorDistancia = distancia;
                controleEscolhido = controle;
                aviaoEscolhido = aviao;
            }

            return controleEscolhido != null
                && aviaoEscolhido != null
                && controleEscolhido.EmitirMissaoAereaOfensiva(alvo.transform.position, alvo.transform);
        }

        private bool RadarTemLinhaDeVisao(Vector3 origem, IdentidadeUnidade observador, IdentidadeUnidade alvo)
        {
            Vector3 inicio = origem + Vector3.up * 2f;
            Vector3 fim = alvo.transform.position + Vector3.up * 2f;
            Vector3 delta = fim - inicio;
            float distancia = delta.magnitude;
            if (distancia <= 0.01f) return true;

            int quantidade = Physics.RaycastNonAlloc(
                inicio,
                delta / distancia,
                terrainRaycastHits,
                distancia,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < quantidade; i++)
            {
                Collider colisor = terrainRaycastHits[i].collider;
                if (colisor == null
                    || colisor.transform == observador.transform
                    || colisor.transform.IsChildOf(observador.transform)
                    || colisor.transform == alvo.transform
                    || colisor.transform.IsChildOf(alvo.transform))
                    continue;

                // Terreno alto bloqueia o radar entre unidades terrestres.
                // Objetos e unidades no caminho não ocultam outros contatos.
                if (colisor is TerrainCollider || colisor.GetComponentInParent<Terrain>() != null)
                    return false;
            }

            return true;
        }

        private void ReportContactToAllies(int observerTeamId, IdentidadeUnidade target, RTSDetectionSource source, float duration)
        {
            ReportContact(observerTeamId, target, source, duration);
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || governo.Relacoes == null) return;

            IReadOnlyList<RelacaoPaisGoverno> relacoes = governo.Relacoes;
            for (int i = 0; i < relacoes.Count; i++)
            {
                RelacaoPaisGoverno relacao = relacoes[i];
                if (relacao == null || !relacao.pactoMilitar
                    || (relacao.teamA != observerTeamId && relacao.teamB != observerTeamId)) continue;
                int aliado = relacao.Outro(observerTeamId);
                if (aliado > 0 && TeamsAtWar(aliado, target.teamID))
                    ReportContact(aliado, target, source, duration);
            }
        }

        private void ColetarAliadosComUnidadesProximas(int observerTeamId, Vector3 emitterPosition)
        {
            nearbyAlliedTeamsBuffer.Clear();
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || governo.Relacoes == null) return;

            IReadOnlyList<RelacaoPaisGoverno> relacoes = governo.Relacoes;
            for (int i = 0; i < relacoes.Count; i++)
            {
                RelacaoPaisGoverno relacao = relacoes[i];
                if (relacao == null || !relacao.pactoMilitar
                    || (relacao.teamA != observerTeamId && relacao.teamB != observerTeamId)) continue;
                int aliado = relacao.Outro(observerTeamId);
                if (aliado > 0 && HasAlliedUnitNearby(aliado, emitterPosition))
                    nearbyAlliedTeamsBuffer.Add(aliado);
            }
        }

        private void ReportContactParaAliadosProximos(int observerTeamId, IdentidadeUnidade target, float duration)
        {
            ReportContact(observerTeamId, target, RTSDetectionSource.Radar, duration);
            for (int i = 0; i < nearbyAlliedTeamsBuffer.Count; i++)
            {
                int aliado = nearbyAlliedTeamsBuffer[i];
                if (TeamsAtWar(aliado, target.teamID))
                    ReportContact(aliado, target, RTSDetectionSource.Radar, duration);
            }
        }

        private bool HasAlliedUnitNearby(int teamId, Vector3 position)
        {
            float alcanceSqr = alliedRadarShareRange * alliedRadarShareRange;
            int count = Mathf.Min(unitsBuffer.Count, maxUnitsPerScan);
            for (int i = 0; i < count; i++)
            {
                IdentidadeUnidade unidade = unitsBuffer[i];
                if (unidade == null || !unidade.gameObject.activeInHierarchy || unidade.teamID != teamId)
                    continue;

                Vector3 delta = unidade.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= alcanceSqr)
                    return true;
            }

            return false;
        }

        public static bool TeamsAtWar(int teamA, int teamB)
        {
            if (teamA <= 0 || teamB <= 0 || teamA == teamB) return false;
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null) return true;

            IReadOnlyList<RelacaoPaisGoverno> relacoes = governo.Relacoes;
            for (int i = 0; i < relacoes.Count; i++)
            {
                RelacaoPaisGoverno relacao = relacoes[i];
                if (relacao != null && relacao.Envolve(teamA, teamB))
                    return relacao.guerraDeclarada;
            }

            DadosPaisGoverno paisA = governo.ObterPais(teamA);
            DadosPaisGoverno paisB = governo.ObterPais(teamB);
            return (paisA != null && paisA.emGuerra && paisA.rivalTeamId == teamB)
                || (paisB != null && paisB.emGuerra && paisB.rivalTeamId == teamA);
        }

        private static long ComposeCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private void ExpireContacts()
        {
            float now = Time.unscaledTime;
            foreach (Dictionary<int, RTSVisibilityContact> teamContacts in contactsByTeam.Values)
            {
                if (teamContacts == null) continue;
                expiredContactIds.Clear();
                foreach (KeyValuePair<int, RTSVisibilityContact> pair in teamContacts)
                {
                    RTSVisibilityContact contact = pair.Value;
                    if (contact == null) continue;
                    contact.currentlyVisible = contact.expiresAt >= now;
                    if (contact.lastKnownExpiresAt < now)
                        expiredContactIds.Add(pair.Key);
                }

                for (int i = 0; i < expiredContactIds.Count; i++)
                    teamContacts.Remove(expiredContactIds[i]);
            }
        }

        private RTSVisibilityContact TryGetContact(int observerTeamId, int targetInstanceId)
        {
            return TryGetContact(observerTeamId, targetInstanceId, out RTSVisibilityContact contact) ? contact : null;
        }

        private bool TryGetContact(int observerTeamId, int targetInstanceId, out RTSVisibilityContact contact)
        {
            contact = null;
            Dictionary<int, RTSVisibilityContact> contacts;
            return contactsByTeam.TryGetValue(observerTeamId, out contacts)
                && contacts != null
                && contacts.TryGetValue(targetInstanceId, out contact)
                && contact != null;
        }

        private Dictionary<int, RTSVisibilityContact> GetOrCreateTeamContacts(int teamId)
        {
            Dictionary<int, RTSVisibilityContact> contacts;
            if (!contactsByTeam.TryGetValue(teamId, out contacts) || contacts == null)
            {
                contacts = new Dictionary<int, RTSVisibilityContact>();
                contactsByTeam[teamId] = contacts;
            }

            return contacts;
        }
    }
}
