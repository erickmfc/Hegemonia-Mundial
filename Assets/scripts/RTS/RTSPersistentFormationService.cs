using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.RTS
{
    public enum RTSFormationDomain
    {
        Naval = 0,
        Air = 1
    }

    [Serializable]
    public sealed class RTSPersistentFormationMemberData
    {
        public string unitId;
        public Vector3 localOffset;
    }

    [Serializable]
    public sealed class RTSPersistentFormationSaveData
    {
        public string formationId;
        public int teamId;
        public RTSFormationDomain domain;
        public string leaderUnitId;
        public List<RTSPersistentFormationMemberData> members = new List<RTSPersistentFormationMemberData>();
    }

    /// <summary>
    /// Persists player-issued naval and air group movement using existing
    /// ControleUnidade orders. It deliberately does not issue combat orders.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class RTSPersistentFormationService : MonoBehaviour
    {
        private const int MaximumFormations = 64;
        private const int MaximumMembersPerFormation = 32;
        private const float UpdateIntervalSeconds = 0.75f;
        private const float DestinationRefreshDistance = 12f;

        private sealed class RuntimeMember
        {
            public string UnitId;
            public ControleUnidade Unit;
            public Vector3 LocalOffset;
        }

        private sealed class RuntimeFormation
        {
            public RTSPersistentFormationSaveData Data;
            public readonly List<RuntimeMember> Members = new List<RuntimeMember>(8);
            public ControleUnidade Leader;
            public float NextUpdateTime;
        }

        private sealed class Candidate
        {
            public ControleUnidade Unit;
            public EstadoControleUnidadeSnapshot State;
            public string UnitId;
        }

        public static RTSPersistentFormationService Instance { get; private set; }

        private readonly List<RuntimeFormation> formations = new List<RuntimeFormation>(16);
        private readonly List<Candidate> navalCandidates = new List<Candidate>(16);
        private readonly List<Candidate> airCandidates = new List<Candidate>(16);
        private readonly HashSet<string> candidateIds = new HashSet<string>(StringComparer.Ordinal);

        public int FormationCount => formations.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GarantirInstancia();
        }

        public static RTSPersistentFormationService GarantirInstancia()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject root = new GameObject("RTSPersistentFormationService");
            return root.AddComponent<RTSPersistentFormationService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                formations.Clear();
            }
        }

        /// <summary>
        /// Called after the existing selection controller has issued a normal
        /// group move. Special return-to-base and strategic-weapon commands
        /// stay outside this formation path.
        /// </summary>
        public void RegistrarOrdemDeGrupo(IList<ControleUnidade> selectedUnits)
        {
            if (selectedUnits == null || selectedUnits.Count < 2)
            {
                return;
            }

            navalCandidates.Clear();
            airCandidates.Clear();
            candidateIds.Clear();

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                ControleUnidade unit = selectedUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                EstadoControleUnidadeSnapshot state = unit.ObterEstadoControle();
                if (!state.possuiDestinoOrdenado
                    || (state.ordemAtual != OrdemControleUnidade.Movendo
                        && state.ordemAtual != OrdemControleUnidade.Recuando))
                {
                    continue;
                }

                SaveableEntity saveable = SaveableEntity.Garantir(unit.gameObject);
                if (saveable == null || string.IsNullOrWhiteSpace(saveable.UniqueId)
                    || !candidateIds.Add(saveable.UniqueId))
                {
                    continue;
                }

                Candidate candidate = new Candidate
                {
                    Unit = unit,
                    State = state,
                    UnitId = saveable.UniqueId
                };

                if (unit.EhUnidadeNaval())
                {
                    navalCandidates.Add(candidate);
                }
                else if (unit.EhUnidadeAerea() || unit.TemControleAviao || unit.TemHelicopteroExterno)
                {
                    airCandidates.Add(candidate);
                }
            }

            RegistrarDominio(navalCandidates, RTSFormationDomain.Naval);
            RegistrarDominio(airCandidates, RTSFormationDomain.Air);
        }

        private void RegistrarDominio(List<Candidate> candidates, RTSFormationDomain domain)
        {
            if (candidates.Count < 2)
            {
                return;
            }

            int count = Mathf.Min(candidates.Count, MaximumMembersPerFormation);
            Candidate leaderCandidate = candidates[0];
            Vector3 leaderDestination = leaderCandidate.State.ultimoDestino;
            Vector3 heading = leaderDestination - leaderCandidate.Unit.transform.position;
            if (heading.sqrMagnitude < 1f)
            {
                heading = leaderCandidate.Unit.transform.forward;
            }
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f)
            {
                heading = Vector3.forward;
            }

            Quaternion frame = Quaternion.LookRotation(heading.normalized, Vector3.up);
            Quaternion inverseFrame = Quaternion.Inverse(frame);
            bool hasDistinctSlots = false;
            for (int i = 1; i < count; i++)
            {
                if ((candidates[i].State.ultimoDestino - leaderDestination).sqrMagnitude > 4f)
                {
                    hasDistinctSlots = true;
                    break;
                }
            }

            RuntimeFormation runtime = new RuntimeFormation();
            runtime.Data = new RTSPersistentFormationSaveData
            {
                formationId = Guid.NewGuid().ToString("N"),
                teamId = ObterTeamId(leaderCandidate.Unit),
                domain = domain,
                leaderUnitId = leaderCandidate.UnitId,
                members = new List<RTSPersistentFormationMemberData>(count)
            };
            runtime.Leader = leaderCandidate.Unit;

            for (int i = 0; i < count; i++)
            {
                Candidate candidate = candidates[i];
                Vector3 localOffset = inverseFrame * (candidate.State.ultimoDestino - leaderDestination);
                if (!hasDistinctSlots && i > 0)
                {
                    localOffset = CriarOffsetEmV(i, domain == RTSFormationDomain.Naval ? 90f : 55f);
                }

                runtime.Members.Add(new RuntimeMember
                {
                    UnitId = candidate.UnitId,
                    Unit = candidate.Unit,
                    LocalOffset = localOffset
                });
                runtime.Data.members.Add(new RTSPersistentFormationMemberData
                {
                    unitId = candidate.UnitId,
                    localOffset = localOffset
                });
            }

            RemoverFormacoesSobrepostas(runtime);
            if (formations.Count >= MaximumFormations)
            {
                DiagnosticoDesempenhoJogo.DefinirContadorMetrica("persistent_formations", formations.Count);
                return;
            }

            formations.Add(runtime);
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("persistent_formations", formations.Count);
        }

        private static Vector3 CriarOffsetEmV(int index, float spacing)
        {
            int rank = (index + 1) / 2;
            float side = index % 2 == 1 ? -1f : 1f;
            return new Vector3(side * rank * spacing, 0f, -rank * spacing * 0.65f);
        }

        private static int ObterTeamId(ControleUnidade unit)
        {
            IdentidadeUnidade identity = unit != null ? unit.GetComponent<IdentidadeUnidade>() : null;
            return identity != null ? identity.teamID : 0;
        }

        private void RemoverFormacoesSobrepostas(RuntimeFormation incoming)
        {
            for (int i = formations.Count - 1; i >= 0; i--)
            {
                RuntimeFormation existing = formations[i];
                bool overlaps = false;
                for (int a = 0; a < incoming.Members.Count && !overlaps; a++)
                {
                    string id = incoming.Members[a].UnitId;
                    for (int b = 0; b < existing.Members.Count; b++)
                    {
                        if (string.Equals(id, existing.Members[b].UnitId, StringComparison.Ordinal))
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }

                if (overlaps)
                {
                    formations.RemoveAt(i);
                }
            }
        }

        private void Update()
        {
            if (RTSSimulationClock.SimulacaoPausada) return;
            float now = Time.unscaledTime;
            for (int i = formations.Count - 1; i >= 0; i--)
            {
                RuntimeFormation formation = formations[i];
                if (formation == null || formation.Members == null)
                {
                    formations.RemoveAt(i);
                    continue;
                }

                if (now < formation.NextUpdateTime)
                {
                    continue;
                }

                formation.NextUpdateTime = now + UpdateIntervalSeconds;
                PruneMembers(formation);
                if (formation.Members.Count < 2 || formation.Leader == null)
                {
                    formations.RemoveAt(i);
                    continue;
                }

                CorrigirSeguidores(formation);
            }
        }

        private static void PruneMembers(RuntimeFormation formation)
        {
            for (int i = formation.Members.Count - 1; i >= 0; i--)
            {
                RuntimeMember member = formation.Members[i];
                if (member == null || member.Unit == null || !member.Unit.gameObject.scene.IsValid())
                {
                    formation.Members.RemoveAt(i);
                }
            }

            if (formation.Leader != null)
            {
                return;
            }

            formation.Leader = formation.Members.Count > 0 ? formation.Members[0].Unit : null;
            if (formation.Leader == null)
            {
                return;
            }

            string newLeaderId = formation.Members[0].UnitId;
            Vector3 heading = ResolveHeading(formation.Leader);
            Quaternion inverse = Quaternion.Inverse(Quaternion.LookRotation(heading, Vector3.up));
            Vector3 leaderPosition = formation.Leader.transform.position;
            for (int i = 0; i < formation.Members.Count; i++)
            {
                RuntimeMember member = formation.Members[i];
                member.LocalOffset = inverse * (member.Unit.transform.position - leaderPosition);
            }

            formation.Members[0].LocalOffset = Vector3.zero;
            formation.Data.leaderUnitId = newLeaderId;
            AtualizarOffsetsSalvos(formation);
        }

        private static void CorrigirSeguidores(RuntimeFormation formation)
        {
            Vector3 heading = ResolveHeading(formation.Leader);
            Quaternion frame = Quaternion.LookRotation(heading, Vector3.up);
            Vector3 leaderPosition = formation.Leader.transform.position;

            for (int i = 0; i < formation.Members.Count; i++)
            {
                RuntimeMember member = formation.Members[i];
                if (member.Unit == null || member.Unit == formation.Leader || !member.Unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 destination = leaderPosition + frame * member.LocalOffset;
                EstadoControleUnidadeSnapshot state = member.Unit.ObterEstadoControle();
                if (!state.possuiDestinoOrdenado
                    || Vector3.Distance(state.ultimoDestino, destination) >= DestinationRefreshDistance)
                {
                    member.Unit.EmitirOrdemMover(destination);
                }
            }
        }

        private static Vector3 ResolveHeading(ControleUnidade leader)
        {
            if (leader == null)
            {
                return Vector3.forward;
            }

            EstadoControleUnidadeSnapshot state = leader.ObterEstadoControle();
            Vector3 heading = state.possuiDestinoOrdenado
                ? state.ultimoDestino - leader.transform.position
                : leader.transform.forward;
            heading.y = 0f;
            return heading.sqrMagnitude > 0.01f ? heading.normalized : leader.transform.forward;
        }

        private static void AtualizarOffsetsSalvos(RuntimeFormation formation)
        {
            if (formation.Data.members == null)
            {
                formation.Data.members = new List<RTSPersistentFormationMemberData>();
            }

            for (int i = 0; i < formation.Members.Count; i++)
            {
                RuntimeMember runtime = formation.Members[i];
                for (int j = 0; j < formation.Data.members.Count; j++)
                {
                    RTSPersistentFormationMemberData saved = formation.Data.members[j];
                    if (saved != null && string.Equals(saved.unitId, runtime.UnitId, StringComparison.Ordinal))
                    {
                        saved.localOffset = runtime.LocalOffset;
                        break;
                    }
                }
            }
        }

        public List<RTSPersistentFormationSaveData> CapturarEstado(Scene scene)
        {
            var result = new List<RTSPersistentFormationSaveData>(formations.Count);
            for (int i = 0; i < formations.Count; i++)
            {
                RuntimeFormation runtime = formations[i];
                if (runtime == null || runtime.Members.Count < 2)
                {
                    continue;
                }

                var snapshot = new RTSPersistentFormationSaveData
                {
                    formationId = runtime.Data.formationId,
                    teamId = runtime.Data.teamId,
                    domain = runtime.Data.domain,
                    leaderUnitId = runtime.Data.leaderUnitId,
                    members = new List<RTSPersistentFormationMemberData>(runtime.Members.Count)
                };

                for (int j = 0; j < runtime.Members.Count; j++)
                {
                    RuntimeMember member = runtime.Members[j];
                    if (member.Unit == null || member.Unit.gameObject.scene != scene)
                    {
                        continue;
                    }

                    snapshot.members.Add(new RTSPersistentFormationMemberData
                    {
                        unitId = member.UnitId,
                        localOffset = member.LocalOffset
                    });
                }

                if (snapshot.members.Count >= 2)
                {
                    result.Add(snapshot);
                }
            }

            return result;
        }

        public void RestaurarEstado(
            IList<RTSPersistentFormationSaveData> saved,
            IDictionary<string, SaveableEntity> saveablesById)
        {
            formations.Clear();
            if (saved == null || saveablesById == null)
            {
                return;
            }

            int formationLimit = Mathf.Min(saved.Count, MaximumFormations);
            for (int i = 0; i < formationLimit; i++)
            {
                RTSPersistentFormationSaveData data = saved[i];
                if (data == null || data.members == null || data.members.Count < 2
                    || (data.domain != RTSFormationDomain.Naval && data.domain != RTSFormationDomain.Air))
                {
                    continue;
                }

                var runtime = new RuntimeFormation
                {
                    Data = data
                };
                var seen = new HashSet<string>(StringComparer.Ordinal);
                int memberLimit = Mathf.Min(data.members.Count, MaximumMembersPerFormation);
                for (int memberIndex = 0; memberIndex < memberLimit; memberIndex++)
                {
                    RTSPersistentFormationMemberData memberData = data.members[memberIndex];
                    if (memberData == null || string.IsNullOrWhiteSpace(memberData.unitId)
                        || !seen.Add(memberData.unitId)
                        || !saveablesById.TryGetValue(memberData.unitId, out SaveableEntity saveable)
                        || saveable == null)
                    {
                        continue;
                    }

                    ControleUnidade unit = saveable.GetComponent<ControleUnidade>();
                    if (unit == null)
                    {
                        continue;
                    }

                    runtime.Members.Add(new RuntimeMember
                    {
                        UnitId = memberData.unitId,
                        Unit = unit,
                        LocalOffset = memberData.localOffset
                    });
                    if (string.Equals(memberData.unitId, data.leaderUnitId, StringComparison.Ordinal))
                    {
                        runtime.Leader = unit;
                    }
                }

                if (runtime.Members.Count < 2)
                {
                    continue;
                }

                if (runtime.Leader == null)
                {
                    runtime.Leader = runtime.Members[0].Unit;
                    runtime.Data.leaderUnitId = runtime.Members[0].UnitId;
                }

                formations.Add(runtime);
            }

            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("persistent_formations", formations.Count);
        }
    }
}
