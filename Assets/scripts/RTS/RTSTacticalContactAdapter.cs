using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Camada de compatibilidade entre as fontes atuais de deteccao e o futuro
    /// Tactical Display. Este componente nao movimenta unidades, nao decide
    /// engajamento e nao altera as regras diplomaticas ou de combate.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6900)]
    public sealed class RTSTacticalContactAdapter : MonoBehaviour
    {
        private const float SourceResolveInterval = 0.5f;
        private const float MaintenanceInterval = 0.25f;
        private const int MaxContacts = 2048;

        private readonly struct ContactKey : IEquatable<ContactKey>
        {
            public readonly int ObserverTeamId;
            public readonly int SensorTargetId;
            public readonly bool Anonymous;

            public ContactKey(int observerTeamId, int sensorTargetId, bool anonymous)
            {
                ObserverTeamId = observerTeamId;
                SensorTargetId = sensorTargetId;
                Anonymous = anonymous;
            }

            public bool Equals(ContactKey other)
            {
                return ObserverTeamId == other.ObserverTeamId
                    && SensorTargetId == other.SensorTargetId
                    && Anonymous == other.Anonymous;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactKey && Equals((ContactKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ObserverTeamId;
                    hash = (hash * 397) ^ SensorTargetId;
                    hash = (hash * 397) ^ (Anonymous ? 1 : 0);
                    return hash;
                }
            }
        }

        private sealed class ContactRecord
        {
            public ContactKey Key;
            public string ContactId;
            public int ObserverTeamId;
            public int TargetInstanceId;
            public int TargetTeamId;
            public int TargetSceneHandle;
            public bool HasTargetIdentity;
            public bool HasIndependentAffiliationEvidence;
            public float IdentityValidUntil;
            public bool HasCurrentObservation;
            public Vector3 CurrentPosition;
            public bool HasCurrentPosition;
            public Vector3 LastKnownPosition;
            public bool HasLastKnownPosition;
            public Vector3 EstimatedVelocity;
            public bool HasEstimatedVelocity;
            public bool PositionIsEstimated;
            public float UncertaintyRadiusMeters;
            public float LastSeenAt;
            public float ValidUntil;
            public float LastKnownUntil;
            public float RetentionUntil;
            public float Confidence;
            public RTSTacticalContactRelation Relation;
            public RTSTacticalContactIdentification Identification;
            public RTSTacticalContactThreatState ThreatState;
            public RTSTacticalContactCategory Category;
            public RTSDetectionSource DetectionSource;
            public string DetectionOrigin;
            public string IdentificationLabel;
            public RTSTacticalVerticalKind VerticalKind;
            public float VerticalValueMeters;
            public bool HasVerticalMeasurement;
            public bool VerticalIsEstimated;
            public RTSTacticalContactState LastEmittedState = RTSTacticalContactState.Expired;
        }

        public static RTSTacticalContactAdapter Instancia { get; private set; }

        private readonly Dictionary<ContactKey, ContactRecord> contacts =
            new Dictionary<ContactKey, ContactRecord>(256);
        private readonly List<ContactKey> expiredKeys = new List<ContactKey>(64);

        private RTSVisibilityService subscribedVisibilityService;
        private float nextSourceResolveAt;
        private float nextMaintenanceAt;
        private int nextContactSequence = 1;
        private int nextAnonymousSequence = 1;

        public event Action<RTSTacticalContactSnapshot> OnContactChanged;

        public int ContactCount => contacts.Count;

        /// <summary>
        /// Cria o adapter no mesmo nucleo persistente ja usado pelos servicos
        /// RTS. Nenhuma cena ou prefab precisa ser editado nesta etapa.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntime()
        {
            if (Instancia != null) return;

            GameObject root = GameObject.Find("RTS_CoreRuntime");
            if (root == null)
            {
                root = new GameObject("RTS_CoreRuntime");
                DontDestroyOnLoad(root);
            }

            if (root.GetComponent<RTSTacticalContactAdapter>() == null)
                root.AddComponent<RTSTacticalContactAdapter>();
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            AssinarFontesDisponiveis();
        }

        private void OnEnable()
        {
            BoeingE3Reconhecimento.OnContatoTransmitido += AoReceberContatoE3;
            AssinarFontesDisponiveis();
        }

        private void OnDisable()
        {
            BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoE3;
            DesassinarServicoVisibilidade();
        }

        private void Update()
        {
            float agora = Time.unscaledTime;
            if (agora >= nextSourceResolveAt)
            {
                nextSourceResolveAt = agora + SourceResolveInterval;
                AssinarFontesDisponiveis();
            }

            if (agora < nextMaintenanceAt) return;
            nextMaintenanceAt = agora + MaintenanceInterval;
            AtualizarEstadosEExpirar(agora);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            contacts.Clear();
            nextContactSequence = 1;
            nextAnonymousSequence = 1;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            expiredKeys.Clear();
            foreach (KeyValuePair<ContactKey, ContactRecord> pair in contacts)
            {
                ContactRecord record = pair.Value;
                if (record == null || (!record.Key.Anonymous && record.TargetInstanceId != 0
                    && record.TargetSceneHandle == scene.handle))
                    expiredKeys.Add(pair.Key);
            }

            for (int i = 0; i < expiredKeys.Count; i++)
                contacts.Remove(expiredKeys[i]);
        }

        private void AssinarFontesDisponiveis()
        {
            RTSVisibilityService atual = RTSVisibilityService.Instancia;
            if (atual == subscribedVisibilityService) return;

            DesassinarServicoVisibilidade();
            subscribedVisibilityService = atual;
            if (subscribedVisibilityService != null)
                subscribedVisibilityService.OnContactUpdated += AoReceberContatoVisibilidade;
        }

        private void DesassinarServicoVisibilidade()
        {
            if (subscribedVisibilityService == null) return;
            subscribedVisibilityService.OnContactUpdated -= AoReceberContatoVisibilidade;
            subscribedVisibilityService = null;
        }

        private void AoReceberContatoVisibilidade(RTSVisibilityContact contato)
        {
            if (contato == null || contato.observerTeamId <= 0 || contato.targetInstanceId == 0)
                return;

            ContactKey key = new ContactKey(contato.observerTeamId, contato.targetInstanceId, false);
            ContactRecord record = ObterOuCriarRegistro(key);
            if (record == null) return;

            float agora = Time.unscaledTime;
            bool observadoAgora = contato.currentlyVisible && contato.expiresAt >= agora;
            record.TargetInstanceId = contato.targetInstanceId;
            record.TargetTeamId = contato.targetTeamId;
            record.TargetSceneHandle = contato.targetSceneHandle;
            record.HasTargetIdentity = observadoAgora && PermiteRevelarIdentidade(contato.source);
            record.HasIndependentAffiliationEvidence = false;
            record.IdentityValidUntil = record.HasTargetIdentity ? contato.expiresAt : 0f;
            record.HasCurrentObservation = observadoAgora;
            record.CurrentPosition = contato.lastKnownPosition;
            record.HasCurrentPosition = observadoAgora;
            record.LastKnownPosition = contato.lastKnownPosition;
            record.HasLastKnownPosition = true;
            record.PositionIsEstimated = contato.source != RTSDetectionSource.DirectVision
                && contato.source != RTSDetectionSource.Manual;
            record.UncertaintyRadiusMeters = ObterIncerteza(contato.source);
            record.LastSeenAt = contato.lastSeenAt > 0f ? contato.lastSeenAt : agora;
            record.ValidUntil = contato.expiresAt;
            record.LastKnownUntil = contato.lastKnownExpiresAt;
            record.RetentionUntil = contato.lastKnownExpiresAt;
            record.Confidence = ObterConfianca(contato.source);
            // O servico recebe o ID verdadeiro da unidade mesmo quando o sensor
            // so produziu um retorno de radar. Nao derive afiliacao diplomatica
            // desse dado oculto enquanto a observacao nao autorizar identidade.
            record.Relation = record.HasTargetIdentity
                ? ResolverRelacao(contato.observerTeamId, contato.targetTeamId)
                : RTSTacticalContactRelation.Unknown;
            record.Identification = ResolverIdentificacao(contato.source, observadoAgora);
            record.ThreatState = record.HasTargetIdentity
                ? ResolverAmeaca(record.Relation)
                : RTSTacticalContactThreatState.Unknown;
            record.Category = ResolverCategoria(contato.source);
            record.VerticalKind = RTSTacticalVerticalKind.Unknown;
            record.HasVerticalMeasurement = false;
            record.VerticalValueMeters = 0f;
            record.VerticalIsEstimated = true;
            record.DetectionSource = contato.source;
            record.DetectionOrigin = "RTSVisibilityService/" + contato.source;
            record.IdentificationLabel = string.Empty;

            EmitirSnapshot(record, agora, true);
        }

        private void AoReceberContatoE3(BoeingE3Reconhecimento.ContatoReconhecimento contato)
        {
            if (contato == null || contato.equipeObservadora <= 0 || contato.idAlvo == 0)
                return;

            ContactKey key = new ContactKey(contato.equipeObservadora, contato.idAlvo, false);
            ContactRecord record = ObterOuCriarRegistro(key);
            if (record == null) return;

            float agora = Time.unscaledTime;
            bool informacaoDeSensor = contato.tipo == BoeingE3Reconhecimento.TipoContatoReconhecimento.InformacaoDeSensor;
            bool observadoAgora = !informacaoDeSensor
                && (contato.validadeAte <= 0f || agora <= contato.validadeAte);
            bool observacaoAtualAnterior = record.HasCurrentObservation
                && agora <= record.ValidUntil;
            bool identidadeAutorizadaAnterior = observacaoAtualAnterior
                && record.HasTargetIdentity
                && agora <= record.IdentityValidUntil;

            record.TargetInstanceId = contato.idAlvo;
            record.TargetTeamId = contato.equipeAlvo;
            record.TargetSceneHandle = SceneManager.GetActiveScene().handle;
            record.HasTargetIdentity = identidadeAutorizadaAnterior;
            record.HasIndependentAffiliationEvidence = false;
            if (!identidadeAutorizadaAnterior)
                record.IdentityValidUntil = 0f;
            if (observadoAgora)
            {
                record.HasCurrentObservation = true;
                record.CurrentPosition = contato.ultimaPosicaoConhecida;
                record.HasCurrentPosition = true;
            }
            else if (!observacaoAtualAnterior)
            {
                record.HasTargetIdentity = false;
                record.HasCurrentObservation = false;
                record.HasCurrentPosition = false;
            }

            record.LastKnownPosition = contato.ultimaPosicaoConhecida;
            record.HasLastKnownPosition = true;
            record.EstimatedVelocity = contato.direcao.sqrMagnitude > 0.0001f
                ? contato.direcao.normalized * Mathf.Max(0f, contato.velocidade)
                : Vector3.zero;
            record.HasEstimatedVelocity = record.EstimatedVelocity.sqrMagnitude > 0.0001f;
            record.PositionIsEstimated = true;
            record.UncertaintyRadiusMeters = informacaoDeSensor ? 250f : 100f;
            record.LastSeenAt = contato.ultimaAtualizacao > 0f ? contato.ultimaAtualizacao : agora;
            float validadeContato = contato.validadeAte > 0f ? contato.validadeAte : agora + 1f;
            if (observadoAgora || !observacaoAtualAnterior)
                record.ValidUntil = validadeContato;
            record.LastKnownUntil = Mathf.Max(record.LastKnownUntil, contato.retencaoAte > 0f
                ? contato.retencaoAte
                : validadeContato + 60f);
            record.RetentionUntil = record.LastKnownUntil;
            record.Confidence = observacaoAtualAnterior
                ? Mathf.Max(record.Confidence, ObterConfiancaE3(contato.tipo))
                : ObterConfiancaE3(contato.tipo);
            // Relatorios de sensor e lancamentos podem carregar a equipe real
            // internamente sem comprovar afiliacao para a tela tatico.
            // O relatorio E-3 atual nao possui evidencia IFF: tipo, nome e
            // equipe-alvo podem vir diretamente do estado interno da unidade.
            // Preserve uma identidade proveniente de outra fonte autorizada
            // apenas ate a validade original dessa observacao.
            if (!identidadeAutorizadaAnterior)
                record.Relation = RTSTacticalContactRelation.Unknown;
            if (!identidadeAutorizadaAnterior)
                record.Identification = observadoAgora
                    ? RTSTacticalContactIdentification.Probable
                    : RTSTacticalContactIdentification.Unknown;
            record.ThreatState = ResolverAmeaca(contato, record.Relation);
            RTSTacticalContactCategory categoriaE3 = ResolverCategoria(contato.tipo);
            if (categoriaE3 != RTSTacticalContactCategory.SensorReport
                || record.Category == RTSTacticalContactCategory.Unknown)
                record.Category = categoriaE3;
            record.VerticalKind = RTSTacticalVerticalKind.Unknown;
            record.HasVerticalMeasurement = false;
            record.VerticalValueMeters = 0f;
            record.VerticalIsEstimated = true;
            record.DetectionSource = RTSDetectionSource.AirRecon;
            record.DetectionOrigin = string.IsNullOrWhiteSpace(contato.fonte)
                ? "Boeing E-3"
                : contato.fonte;
            if (!identidadeAutorizadaAnterior)
                record.IdentificationLabel = string.Empty;

            EmitirSnapshot(record, agora, true);
        }

        /// <summary>
        /// Permite que um sensor futuro registre um contato sem identidade.
        /// O ponto recebido e tratado como observacao/estimativa do sensor e
        /// jamais como Transform real de uma unidade. A afiliação só pode ser
        /// publicada quando o produtor confirmar evidência independente atual.
        /// </summary>
        public bool ReportAnonymousContact(
            int observerTeamId,
            int sensorContactId,
            RTSTacticalContactCategory category,
            RTSDetectionSource source,
            Vector3 estimatedPosition,
            Vector3 estimatedVelocity,
            float confidence,
            float validForSeconds,
            float retentionSeconds,
            float uncertaintyRadiusMeters,
            string origin,
            RTSTacticalContactRelation relation = RTSTacticalContactRelation.Unknown,
            RTSTacticalContactIdentification identification = RTSTacticalContactIdentification.Unknown,
            RTSTacticalContactThreatState threatState = RTSTacticalContactThreatState.Unknown,
            RTSTacticalVerticalKind verticalKind = RTSTacticalVerticalKind.Unknown,
            float verticalValueMeters = 0f,
            bool verticalIsEstimated = true,
            bool affiliationConfirmedByIndependentEvidence = false)
        {
            if (observerTeamId <= 0) return false;
            if (sensorContactId == 0) sensorContactId = nextAnonymousSequence++;

            ContactKey key = new ContactKey(observerTeamId, sensorContactId, true);
            ContactRecord record = ObterOuCriarRegistro(key);
            if (record == null) return false;

            float agora = Time.unscaledTime;
            record.HasTargetIdentity = false;
            record.HasIndependentAffiliationEvidence = affiliationConfirmedByIndependentEvidence;
            record.IdentityValidUntil = 0f;
            record.HasCurrentObservation = true;
            record.CurrentPosition = estimatedPosition;
            record.HasCurrentPosition = true;
            record.LastKnownPosition = estimatedPosition;
            record.HasLastKnownPosition = true;
            record.EstimatedVelocity = estimatedVelocity;
            record.HasEstimatedVelocity = estimatedVelocity.sqrMagnitude > 0.0001f;
            record.PositionIsEstimated = true;
            record.UncertaintyRadiusMeters = Mathf.Max(0f, uncertaintyRadiusMeters);
            record.LastSeenAt = agora;
            record.ValidUntil = agora + Mathf.Max(0.25f, validForSeconds);
            record.LastKnownUntil = record.ValidUntil + Mathf.Max(0.25f, retentionSeconds);
            record.RetentionUntil = record.LastKnownUntil;
            record.Confidence = Mathf.Clamp01(confidence);
            // Um contato anônimo pode ter classificação de comportamento ou
            // ameaça, mas a afiliação só é publicada quando a fonte declara
            // evidência independente atual (por exemplo, IFF validado).
            record.Relation = affiliationConfirmedByIndependentEvidence
                ? relation
                : RTSTacticalContactRelation.Unknown;
            record.Identification = identification != RTSTacticalContactIdentification.Unknown
                ? identification
                : confidence >= 0.5f
                ? RTSTacticalContactIdentification.Probable
                : RTSTacticalContactIdentification.Unknown;
            record.ThreatState = threatState;
            record.Category = category;
            record.VerticalKind = verticalKind;
            record.VerticalValueMeters = verticalValueMeters;
            record.HasVerticalMeasurement = verticalKind != RTSTacticalVerticalKind.Unknown;
            record.VerticalIsEstimated = verticalIsEstimated;
            record.DetectionSource = source;
            record.DetectionOrigin = origin ?? string.Empty;
            record.IdentificationLabel = string.Empty;

            EmitirSnapshot(record, agora, true);
            return true;
        }

        public int CopiarContatos(int observerTeamId, List<RTSTacticalContactSnapshot> destino)
        {
            if (destino == null) return 0;
            destino.Clear();
            if (observerTeamId <= 0) return 0;

            float agora = Time.unscaledTime;
            foreach (ContactRecord record in contacts.Values)
            {
                if (record == null || record.ObserverTeamId != observerTeamId) continue;
                if (ResolverEstado(record, agora) == RTSTacticalContactState.Expired) continue;
                destino.Add(CriarSnapshot(record, agora));
            }

            return destino.Count;
        }

        public bool TryObterContato(int observerTeamId, string contactId, out RTSTacticalContactSnapshot snapshot)
        {
            float agora = Time.unscaledTime;
            foreach (ContactRecord record in contacts.Values)
            {
                if (record == null || record.ObserverTeamId != observerTeamId
                    || !string.Equals(record.ContactId, contactId, StringComparison.Ordinal))
                    continue;

                if (ResolverEstado(record, agora) == RTSTacticalContactState.Expired)
                    break;

                snapshot = CriarSnapshot(record, agora);
                return true;
            }

            snapshot = default(RTSTacticalContactSnapshot);
            return false;
        }

        private ContactRecord ObterOuCriarRegistro(ContactKey key)
        {
            if (contacts.TryGetValue(key, out ContactRecord existente) && existente != null)
                return existente;

            if (contacts.Count >= MaxContacts) return null;

            ContactRecord novo = new ContactRecord
            {
                Key = key,
                ContactId = "TAC-" + nextContactSequence.ToString("D6"),
                ObserverTeamId = key.ObserverTeamId,
                Relation = RTSTacticalContactRelation.Unknown,
                Identification = RTSTacticalContactIdentification.Unknown,
                ThreatState = RTSTacticalContactThreatState.Unknown,
                Category = RTSTacticalContactCategory.Unknown,
                VerticalKind = RTSTacticalVerticalKind.Unknown,
                DetectionSource = RTSDetectionSource.Manual,
                DetectionOrigin = string.Empty
            };

            nextContactSequence++;
            if (nextContactSequence <= 0) nextContactSequence = 1;
            contacts.Add(key, novo);
            return novo;
        }

        private void AtualizarEstadosEExpirar(float agora)
        {
            expiredKeys.Clear();
            foreach (KeyValuePair<ContactKey, ContactRecord> pair in contacts)
            {
                ContactRecord record = pair.Value;
                if (record == null)
                {
                    expiredKeys.Add(pair.Key);
                    continue;
                }

                RTSTacticalContactState estado = ResolverEstado(record, agora);
                if (estado != record.LastEmittedState)
                    EmitirSnapshot(record, agora, false);

                if (estado == RTSTacticalContactState.Expired)
                    expiredKeys.Add(pair.Key);
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                ContactKey key = expiredKeys[i];
                if (contacts.TryGetValue(key, out ContactRecord record) && record != null
                    && record.LastEmittedState != RTSTacticalContactState.Expired)
                    EmitirSnapshot(record, agora, false);
                contacts.Remove(key);
            }
        }

        private void EmitirSnapshot(ContactRecord record, float agora, bool force)
        {
            RTSTacticalContactSnapshot snapshot = CriarSnapshot(record, agora);
            if (!force && snapshot.State == record.LastEmittedState) return;
            record.LastEmittedState = snapshot.State;
            OnContactChanged?.Invoke(snapshot);
        }

        private static RTSTacticalContactSnapshot CriarSnapshot(ContactRecord record, float agora)
        {
            RTSTacticalContactState state = ResolverEstado(record, agora);
            bool current = state == RTSTacticalContactState.Current;
            bool hasPosition = false;
            bool positionEstimated = true;
            Vector3 position = Vector3.zero;

            if (current && record.HasCurrentPosition)
            {
                position = record.CurrentPosition;
                hasPosition = true;
                positionEstimated = record.PositionIsEstimated;
            }
            else if (state == RTSTacticalContactState.Estimated && record.HasLastKnownPosition)
            {
                float idade = Mathf.Max(0f, agora - record.LastSeenAt);
                position = record.LastKnownPosition
                    + (record.HasEstimatedVelocity ? record.EstimatedVelocity * idade : Vector3.zero);
                hasPosition = true;
                positionEstimated = true;
            }
            else if (record.HasLastKnownPosition)
            {
                position = record.LastKnownPosition;
                hasPosition = true;
                positionEstimated = false;
            }

            bool identidadeAutorizadaAtual = record.HasTargetIdentity
                && agora <= record.IdentityValidUntil;
            bool revealIdentity = current && identidadeAutorizadaAtual;
            bool relacaoAutorizadaAtual = current
                && (identidadeAutorizadaAtual || record.HasIndependentAffiliationEvidence);
            RTSTacticalContactRelation relation = relacaoAutorizadaAtual
                ? record.Relation
                : RTSTacticalContactRelation.Unknown;
            RTSTacticalContactThreatState threatState = !current
                || (record.DetectionSource == RTSDetectionSource.AirRecon
                    && !identidadeAutorizadaAtual
                    && record.ThreatState != RTSTacticalContactThreatState.Confirmed)
                ? RTSTacticalContactThreatState.Unknown
                : record.ThreatState;
            RTSTacticalContactIdentification identification = !current
                ? RTSTacticalContactIdentification.Unknown
                : record.DetectionSource == RTSDetectionSource.AirRecon && !identidadeAutorizadaAtual
                    ? RTSTacticalContactIdentification.Probable
                    : record.Identification;
            return new RTSTacticalContactSnapshot(
                record.ContactId,
                record.ObserverTeamId,
                state,
                relation,
                identification,
                threatState,
                record.Category,
                record.DetectionSource,
                record.DetectionOrigin,
                record.Confidence,
                position,
                hasPosition,
                positionEstimated,
                record.LastKnownPosition,
                record.HasLastKnownPosition,
                record.EstimatedVelocity,
                record.HasEstimatedVelocity,
                record.UncertaintyRadiusMeters,
                record.VerticalKind,
                record.VerticalValueMeters,
                record.HasVerticalMeasurement,
                record.VerticalIsEstimated,
                record.LastSeenAt,
                record.ValidUntil,
                record.LastKnownUntil,
                revealIdentity,
                record.TargetInstanceId,
                record.TargetTeamId,
                record.IdentificationLabel);
        }

        private static RTSTacticalContactState ResolverEstado(ContactRecord record, float agora)
        {
            if (record == null) return RTSTacticalContactState.Expired;
            if (record.HasCurrentObservation && agora <= record.ValidUntil)
                return RTSTacticalContactState.Current;
            if (agora > record.LastKnownUntil || agora > record.RetentionUntil)
                return RTSTacticalContactState.Expired;
            return record.HasEstimatedVelocity
                ? RTSTacticalContactState.Estimated
                : RTSTacticalContactState.LastKnown;
        }

        private static bool PermiteRevelarIdentidade(RTSDetectionSource source)
        {
            return source == RTSDetectionSource.DirectVision
                || source == RTSDetectionSource.Manual;
        }

        private static RTSTacticalContactRelation ResolverRelacao(int observerTeamId, int targetTeamId)
        {
            if (observerTeamId <= 0 || targetTeamId <= 0) return RTSTacticalContactRelation.Unknown;
            if (observerTeamId == targetTeamId) return RTSTacticalContactRelation.Friendly;

            // Esta consulta apenas classifica a informacao que ja chegou pelo
            // fluxo existente. Ela nao libera armamento, ROE ou ordens.
            return ContextoTerritorialDiplomatico.HaHostilidadeOuGuerra(observerTeamId, targetTeamId)
                ? RTSTacticalContactRelation.Hostile
                : RTSTacticalContactRelation.Unknown;
        }

        private static RTSTacticalContactIdentification ResolverIdentificacao(
            RTSDetectionSource source,
            bool observadoAgora)
        {
            if (!observadoAgora) return RTSTacticalContactIdentification.Probable;
            return source == RTSDetectionSource.DirectVision
                ? RTSTacticalContactIdentification.Identified
                : RTSTacticalContactIdentification.Probable;
        }

        private static RTSTacticalContactThreatState ResolverAmeaca(
            RTSTacticalContactRelation relation)
        {
            if (relation == RTSTacticalContactRelation.Hostile)
                return RTSTacticalContactThreatState.Suspected;
            if (relation == RTSTacticalContactRelation.Friendly
                || relation == RTSTacticalContactRelation.Civilian)
                return RTSTacticalContactThreatState.None;
            return RTSTacticalContactThreatState.Unknown;
        }

        private static RTSTacticalContactThreatState ResolverAmeaca(
            BoeingE3Reconhecimento.ContatoReconhecimento contato,
            RTSTacticalContactRelation relation)
        {
            if (contato != null
                && contato.tipo == BoeingE3Reconhecimento.TipoContatoReconhecimento.LancamentoDeMissil)
                return RTSTacticalContactThreatState.Confirmed;
            return ResolverAmeaca(relation);
        }

        private static RTSTacticalContactCategory ResolverCategoria(RTSDetectionSource source)
        {
            switch (source)
            {
                case RTSDetectionSource.Sonar:
                    return RTSTacticalContactCategory.Submarine;
                default:
                    return RTSTacticalContactCategory.Unknown;
            }
        }

        private static RTSTacticalContactCategory ResolverCategoria(
            BoeingE3Reconhecimento.TipoContatoReconhecimento tipo)
        {
            switch (tipo)
            {
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.SubmarinoNaSuperficie:
                    return RTSTacticalContactCategory.Submarine;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.LancamentoDeMissil:
                    return RTSTacticalContactCategory.Missile;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.InformacaoDeSensor:
                    return RTSTacticalContactCategory.SensorReport;
                default:
                    return RTSTacticalContactCategory.Unknown;
            }
        }

        private static float ObterConfianca(RTSDetectionSource source)
        {
            switch (source)
            {
                case RTSDetectionSource.DirectVision: return 0.95f;
                case RTSDetectionSource.Radar: return 0.65f;
                case RTSDetectionSource.Sonar: return 0.60f;
                case RTSDetectionSource.AirRecon: return 0.80f;
                case RTSDetectionSource.Satellite: return 0.75f;
                case RTSDetectionSource.Manual: return 1f;
                default: return 0.5f;
            }
        }

        private static float ObterConfiancaE3(
            BoeingE3Reconhecimento.TipoContatoReconhecimento tipo)
        {
            switch (tipo)
            {
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.UnidadeAliada: return 0.90f;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.UnidadeInimiga: return 0.85f;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.SubmarinoNaSuperficie: return 0.80f;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.LancamentoDeMissil: return 0.75f;
                case BoeingE3Reconhecimento.TipoContatoReconhecimento.InformacaoDeSensor: return 0.55f;
                default: return 0.5f;
            }
        }

        private static float ObterIncerteza(RTSDetectionSource source)
        {
            switch (source)
            {
                case RTSDetectionSource.DirectVision: return 0f;
                case RTSDetectionSource.Radar: return 75f;
                case RTSDetectionSource.Sonar: return 125f;
                case RTSDetectionSource.AirRecon: return 100f;
                case RTSDetectionSource.Satellite: return 200f;
                case RTSDetectionSource.Manual: return 0f;
                default: return 150f;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoE3;
            DesassinarServicoVisibilidade();
            if (Instancia == this) Instancia = null;
        }
    }
}
