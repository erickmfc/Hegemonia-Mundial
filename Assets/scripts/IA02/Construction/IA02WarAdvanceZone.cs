using UnityEngine;
using System.Collections.Generic;
using Hegemonia.AI.Shared;

namespace Hegemonia.AI.IA02
{
    /// <summary>
    /// Create manual de avanço em guerra. Coloque este objeto na água, no ar
    /// ou em terra e atribua o TeamId da IA. As ordens de guerra podem usar os
    /// pontos gerados sem procurar posições aleatórias no mapa.
    /// </summary>
    public sealed class IA02WarAdvanceZone : MonoBehaviour, IWarAdvanceZone
    {
        public enum Dominio { Naval, Aereo, Terrestre }

        [SerializeField] private int teamId = 3;
        [SerializeField] private Dominio dominio = Dominio.Naval;
        [SerializeField] private WarAdvanceZoneType tipoZona = WarAdvanceZoneType.Defensiva;
        [SerializeField] private int donoDaRegiao;
        [SerializeField, Min(20f)] private float raio = 180f;
        [SerializeField, Min(1)] private int pontos = 3;
        [SerializeField, Min(1)] private int incidentesMantidos = 8;
        [SerializeField, Min(1f)] private float duracaoBaixa = 60f;
        [SerializeField, Min(1f)] private float duracaoMedia = 120f;
        [SerializeField, Min(1f)] private float duracaoAlta = 240f;
        [SerializeField, Min(1f)] private float duracaoCritica = 600f;
        [SerializeField, Min(1f)] private float janelaAgregacaoSegundos = 180f;
        [Header("Reação Aérea")]
        [SerializeField] private bool responderAtaques = true;
        [SerializeField, Range(0, 100)] private int prioridadeBase = 50;
        [SerializeField, Min(1f)] private float tempoMemoria = 180f;
        [SerializeField, Min(1f)] private float raioInvestigacao = 3000f;
        [SerializeField, Min(1)] private int escaladaObservacao = 1;
        [SerializeField, Min(1)] private int escaladaInterceptacao = 2;
        [SerializeField, Min(1)] private int escaladaCombate = 4;
        [SerializeField, Min(1)] private int reforcoMaximo = 8;
        [SerializeField, Range(0f, 1f)] private float percentualPatrulhaPreservada = 0.33f;
        [SerializeField, Range(0f, 1f)] private float percentualProtecaoEstrategica = 0.16f;
        [SerializeField, Min(0)] private int reservaMinimaAeronaves = 2;
        [Header("Comportamento")]
        [SerializeField] private bool investigarPrimeiro = true;
        [SerializeField] private bool confirmarHostilidade = true;
        [SerializeField] private bool atualizarUltimaPosicao = true;
        [SerializeField] private bool permitirReforcos = true;
        [SerializeField] private bool permitirRecuo = true;
        [SerializeField] private bool retornarPatrulhaDepois = true;
        [Header("Reação Naval")]
        [SerializeField, Min(1)] private int naviosObservacao = 1;
        [SerializeField, Min(1)] private int naviosInterceptacao = 2;
        [SerializeField, Min(1)] private int naviosCombate = 4;
        [SerializeField, Min(1)] private int reforcoNavalMaximo = 6;
        [SerializeField, Range(0f, 1f)] private float percentualPatrulhaNavalPreservada = 0.33f;
        [SerializeField, Range(0f, 1f)] private float percentualProtecaoNaval = 0.16f;
        [SerializeField, Min(0)] private int reservaMinimaNavios = 1;
        private readonly List<WarAdvanceIncident> incidentes = new List<WarAdvanceIncident>(8);

        public int TeamId => teamId;
        public Dominio Tipo => dominio;
        public bool AceitaIncidentesNavais => dominio == Dominio.Naval;
        public WarAdvanceZoneType TipoZona => tipoZona;
        public int DonoDaRegiao => donoDaRegiao;
        public float Raio => Mathf.Max(20f, raio);
        public Vector3 Position => transform.position;
        public IReadOnlyList<WarAdvanceIncident> IncidentesAtivos => incidentes;
        public bool ResponderAtaques => responderAtaques && (dominio == Dominio.Aereo || dominio == Dominio.Naval);
        public int PrioridadeBase => prioridadeBase;
        public float TempoMemoria => Mathf.Max(1f, tempoMemoria);
        public float RaioInvestigacao => Mathf.Max(1f, raioInvestigacao);
        public bool InvestigarPrimeiro => investigarPrimeiro;
        public bool ConfirmarHostilidade => confirmarHostilidade;
        public bool AtualizarUltimaPosicao => atualizarUltimaPosicao;
        public bool PermitirReforcos => permitirReforcos;
        public bool PermitirRecuo => permitirRecuo;
        public bool RetornarPatrulhaDepois => retornarPatrulhaDepois;

        private void OnEnable() => WarAdvanceZoneRegistry.Register(this);
        private void OnDisable() => WarAdvanceZoneRegistry.Unregister(this);

        public void Configurar(int equipe, Dominio tipo, float raioZona, int totalPontos)
        {
            teamId = Mathf.Max(1, equipe);
            dominio = tipo;
            raio = Mathf.Max(20f, raioZona);
            pontos = Mathf.Max(1, totalPontos);
        }

        public int CalcularLimiteResposta(WarAdvanceIncident incidente, int totalAeronaves)
        {
            if (incidente == null || totalAeronaves <= 0 || !ResponderAtaques) return 0;
            int desejadas;
            switch (incidente.Severity)
            {
                case WarAdvanceIncidentSeverity.Media: desejadas = escaladaInterceptacao; break;
                case WarAdvanceIncidentSeverity.Alta: desejadas = escaladaCombate; break;
                case WarAdvanceIncidentSeverity.Critica: desejadas = reforcoMaximo; break;
                default: desejadas = escaladaObservacao; break;
            }
            int protegidas = Mathf.CeilToInt(totalAeronaves * (percentualPatrulhaPreservada + percentualProtecaoEstrategica));
            int reserva = Mathf.Max(reservaMinimaAeronaves, Mathf.CeilToInt(totalAeronaves * 0.16f));
            int limiteSeguro = Mathf.Max(1, totalAeronaves - protegidas - reserva);
            return Mathf.Clamp(Mathf.Min(desejadas, limiteSeguro), 0, Mathf.Max(1, reforcoMaximo));
        }

        public int CalcularLimiteRespostaNaval(WarAdvanceIncident incidente, int totalNavios)
        {
            if (incidente == null || totalNavios <= 0 || !ResponderAtaques || !AceitaIncidentesNavais) return 0;
            int desejados;
            switch (incidente.Severity)
            {
                case WarAdvanceIncidentSeverity.Media: desejados = Mathf.Max(1, naviosInterceptacao); break;
                case WarAdvanceIncidentSeverity.Alta: desejados = Mathf.Max(1, naviosCombate); break;
                case WarAdvanceIncidentSeverity.Critica: desejados = Mathf.Max(1, reforcoNavalMaximo); break;
                default: desejados = Mathf.Max(1, naviosObservacao); break;
            }
            int protegidos = Mathf.CeilToInt(totalNavios * (percentualPatrulhaNavalPreservada + percentualProtecaoNaval));
            int reserva = Mathf.Max(reservaMinimaNavios, Mathf.CeilToInt(totalNavios * 0.16f));
            int limiteSeguro = Mathf.Max(1, totalNavios - protegidos - reserva);
            return Mathf.Clamp(Mathf.Min(desejados, limiteSeguro), 0, Mathf.Max(1, reforcoNavalMaximo));
        }

        public Vector3 ObterPonto(int indice)
        {
            LimparIncidentesExpirados(Time.unscaledTime);
            if (incidentes.Count > 0)
            {
                WarAdvanceIncident incidente = incidentes[indice < 0 ? 0 : indice % incidentes.Count];
                Vector3 origem = incidente.EnemyLastKnownPosition != Vector3.zero ? incidente.EnemyLastKnownPosition : incidente.Position;
                Vector3 resposta = origem + new Vector3((indice % 3 - 1) * 55f, 0f, (indice % 2) * 70f);
                if (dominio == Dominio.Aereo) resposta.y = Mathf.Max(resposta.y + 100f, 100f);
                return resposta;
            }
            int total = Mathf.Max(1, pontos);
            float angulo = (indice % total) * (360f / total) * Mathf.Deg2Rad;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * (Raio * 0.72f);
            if (dominio == Dominio.Aereo) p.y = Mathf.Max(transform.position.y + 100f, 100f);
            else if (dominio == Dominio.Naval)
            {
                p.y = NavalPlacementResolver.ResolveSeaLevel();
                if (!NavalPlacementResolver.IsWaterAtPosition(p)
                    && NavalPlacementResolver.TryResolveWaterSpawn(p, p - transform.position, 0f, Raio, out Vector3 agua, out _, out _))
                    p = agua;
            }
            return p;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 delta = point - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= Raio * Raio;
        }

        public void RegisterIncident(Vector3 position, Vector3 enemyLastKnownPosition, int attackerTeamId, int shotsDetected, int missilesDetected,
            float damage, string infrastructureHit, bool targetDestroyed, bool attackerStillPresent, bool confirmed, float now)
        {
            LimparIncidentesExpirados(now);
            WarAdvanceIncident existente = null;
            for (int i = 0; i < incidentes.Count; i++)
            {
                if (incidentes[i].AttackerTeamId == attackerTeamId
                    && now - incidentes[i].LastDetectionTime <= Mathf.Min(Mathf.Max(1f, janelaAgregacaoSegundos), TempoMemoria)) { existente = incidentes[i]; break; }
            }
            if (existente == null)
            {
                Vector3 primeiraPosicaoConhecida = enemyLastKnownPosition != Vector3.zero ? enemyLastKnownPosition : position;
                existente = new WarAdvanceIncident { Position = position, EnemyLastKnownPosition = primeiraPosicaoConhecida, FirstDetectionTime = now, AttackCount = 1 };
                incidentes.Add(existente);
            }
            else existente.AttackCount++;
            existente.Position = position;
            if (atualizarUltimaPosicao && enemyLastKnownPosition != Vector3.zero)
                existente.EnemyLastKnownPosition = enemyLastKnownPosition;
            existente.AttackerTeamId = attackerTeamId;
            existente.LastDetectionTime = now;
            existente.ShotsDetected += Mathf.Max(0, shotsDetected);
            existente.MissilesDetected += Mathf.Max(0, missilesDetected);
            existente.DamageCaused += Mathf.Max(0f, damage);
            existente.InfrastructureHit = infrastructureHit ?? string.Empty;
            existente.TargetDestroyed |= targetDestroyed;
            existente.AttackerStillPresent |= attackerStillPresent;
            existente.Confirmed |= confirmed;
            existente.Severity = ResolverSeveridade(existente);
            existente.State = ResolverEstado(existente);
            existente.DurationSeconds = Duracao(existente.Severity);
            while (incidentes.Count > Mathf.Max(1, incidentesMantidos)) incidentes.RemoveAt(0);
        }

        private float Duracao(WarAdvanceIncidentSeverity severity)
        {
            switch (severity)
            {
                case WarAdvanceIncidentSeverity.Media: return duracaoMedia;
                case WarAdvanceIncidentSeverity.Alta: return duracaoAlta;
                case WarAdvanceIncidentSeverity.Critica: return duracaoCritica;
                default: return duracaoBaixa;
            }
        }

        public void LimparIncidentesExpirados(float now)
        {
            for (int i = incidentes.Count - 1; i >= 0; i--)
                if (!incidentes[i].IsActive(now)) incidentes.RemoveAt(i);
        }

        public bool AtualizarContato(int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent, float now)
        {
            WarAdvanceIncident incidente = null;
            float melhor = float.MaxValue;
            for (int i = 0; i < incidentes.Count; i++)
            {
                float distancia = (incidentes[i].Position - lastKnownPosition).sqrMagnitude;
                if (incidentes[i].AttackerTeamId == attackerTeamId && distancia <= melhor)
                {
                    melhor = distancia;
                    incidente = incidentes[i];
                }
            }
            if (incidente == null) return false;
            if (atualizarUltimaPosicao)
            {
                incidente.Position = lastKnownPosition;
                incidente.EnemyLastKnownPosition = lastKnownPosition;
            }
            incidente.AttackerStillPresent = attackerStillPresent;
            incidente.LastDetectionTime = now;
            if (attackerStillPresent)
                incidente.Confirmed = true;
            incidente.State = ResolverEstado(incidente);
            incidente.Severity = ResolverSeveridade(incidente);
            incidente.DurationSeconds = Duracao(incidente.Severity);
            return true;
        }

        public bool UpdateContact(int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent, float now)
        {
            return AtualizarContato(attackerTeamId, lastKnownPosition, attackerStillPresent, now);
        }

        bool IWarAdvanceZone.UpdateContact(int attackerTeamId, Vector3 lastKnownPosition, bool attackerStillPresent, float now)
        {
            return AtualizarContato(attackerTeamId, lastKnownPosition, attackerStillPresent, now);
        }

        private static WarAdvanceIncidentSeverity ResolverSeveridade(WarAdvanceIncident incidente)
        {
            if (incidente.TargetDestroyed || incidente.DamageCaused >= 100f) return WarAdvanceIncidentSeverity.Critica;
            if (incidente.AttackerStillPresent && incidente.AttackCount >= 2 || incidente.DamageCaused >= 35f || incidente.MissilesDetected >= 3) return WarAdvanceIncidentSeverity.Alta;
            if (incidente.MissilesDetected > 0 || incidente.DamageCaused > 0f || incidente.ShotsDetected >= 3 || incidente.AttackCount >= 2) return WarAdvanceIncidentSeverity.Media;
            return WarAdvanceIncidentSeverity.Baixa;
        }

        private static WarAdvanceIncidentState ResolverEstado(WarAdvanceIncident incidente)
        {
            if (incidente.TargetDestroyed || incidente.DamageCaused >= 100f) return WarAdvanceIncidentState.Guerra;
            if (incidente.AttackerStillPresent && incidente.AttackCount >= 2) return WarAdvanceIncidentState.Hostil;
            if (incidente.AttackerStillPresent || incidente.AttackCount >= 2) return WarAdvanceIncidentState.Confirmado;
            return incidente.Confirmed ? WarAdvanceIncidentState.Confirmado : WarAdvanceIncidentState.Detectado;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, Raio);
            Gizmos.DrawLine(transform.position, ObterPonto(0));
        }
    }
}
