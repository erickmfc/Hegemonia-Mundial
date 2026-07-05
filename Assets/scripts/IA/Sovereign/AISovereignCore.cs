using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    public enum AISovereignEnvelope
    {
        Auto = 0,
        Justa = 1,
        SemiTrapaca = 2,
        Brutal = 3
    }

    public enum AISovereignSeverity
    {
        Stable = 0,
        Watch = 1,
        Throttled = 2,
        Emergency = 3
    }

    public enum AISovereignDomain
    {
        Land = 0,
        Naval = 1,
        Air = 2
    }

    public enum AISovereignOrderType
    {
        BuildRole = 0,
        ProduceRole = 1,
        CombatPackage = 2,
        MarketBuy = 3,
        MarketSell = 4,
        Proposal = 5,
        DeclareWar = 6
    }

    public enum AISovereignCatalogRole
    {
        Core = 0,
        Barracks = 1,
        Factory = 2,
        Warehouse = 3,
        Radar = 4,
        Ciws = 5,
        Turret = 6,
        Airport = 7,
        Shipyard = 8,
        Platform = 9,
        Fighter = 10,
        NavalPatrol = 11,
        NavalTransport = 12,
        Carrier = 13,
        OilShip = 14,
        Power = 15,
        Farm = 16
    }

    public enum AIPresidentPhase
    {
        BuildUp = 0,
        Opportunist = 1,
        Siege = 2,
        Retaliation = 3,
        Collapse = 4
    }

    public enum AICombatPackageType
    {
        Recon = 0,
        LocalDefense = 1,
        LandAssault = 2,
        NavalStrike = 3,
        AirStrike = 4,
        LogisticsRaid = 5,
        AmphibiousAssault = 6,
        SensorSuppression = 7,
        PressurePatrol = 8
    }

    [Serializable]
    public sealed class AIPresidentProfile
    {
        public string Archetype = "Equilibrado";
        [Range(0f, 1f)] public float Aggression = 0.5f;
        [Range(0f, 1f)] public float Paranoia = 0.5f;
        [Range(0f, 1f)] public float IndustrialFocus = 0.5f;
        [Range(0f, 1f)] public float NavalFocus = 0.5f;
        [Range(0f, 1f)] public float DiplomaticCunning = 0.5f;
        [Range(0f, 1f)] public float Revenge = 0.5f;
        [Range(0f, 1f)] public float LossTolerance = 0.5f;
        public int Seed;
        public AIPresidentPhase Phase = AIPresidentPhase.BuildUp;
        public float LastMutationTime = -999f;

        public static AIPresidentProfile Create(DadosPaisGoverno pais, int seed)
        {
            UnityEngine.Random.State previous = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            var profile = new AIPresidentProfile();
            profile.Seed = seed;
            profile.Archetype = ResolveArchetype(pais);
            profile.Aggression = ResolveAggression(pais, profile.Archetype);
            profile.Paranoia = ResolveParanoia(pais, profile.Archetype);
            profile.IndustrialFocus = ResolveIndustry(pais, profile.Archetype);
            profile.NavalFocus = ResolveNaval(pais, profile.Archetype);
            profile.DiplomaticCunning = ResolveDiplomacy(pais, profile.Archetype);
            profile.Revenge = ResolveRevenge(pais, profile.Archetype);
            profile.LossTolerance = ResolveLossTolerance(pais, profile.Archetype);
            profile.Phase = AIPresidentPhase.BuildUp;
            profile.LastMutationTime = Time.unscaledTime;

            UnityEngine.Random.state = previous;
            return profile;
        }

        public AISovereignEnvelope ResolveEnvelope(AISovereignEnvelope baseEnvelope, PerfilDificuldadeJogo difficulty)
        {
            AISovereignEnvelope resolved = baseEnvelope;
            if (resolved == AISovereignEnvelope.Auto)
            {
                resolved = AISovereignEnvelope.Justa;
                if (difficulty != null && difficulty.Dificuldade == DificuldadeJogo.Dificil)
                {
                    resolved = Aggression >= 0.72f || Revenge >= 0.70f ? AISovereignEnvelope.SemiTrapaca : AISovereignEnvelope.Justa;
                }
                else if (difficulty != null && difficulty.Dificuldade == DificuldadeJogo.Imperial)
                {
                    resolved = NavalFocus >= 0.66f || Aggression >= 0.66f ? AISovereignEnvelope.Brutal : AISovereignEnvelope.SemiTrapaca;
                }
            }

            if (Phase == AIPresidentPhase.Collapse && resolved > AISovereignEnvelope.Justa)
            {
                resolved -= 1;
            }
            else if ((Phase == AIPresidentPhase.Retaliation || Phase == AIPresidentPhase.Siege) && resolved < AISovereignEnvelope.Brutal)
            {
                resolved += 1;
            }

            return resolved;
        }

        public void Mutate(AIStrategicBlackboard blackboard, float now)
        {
            if (blackboard == null || now < LastMutationTime + 45f)
            {
                return;
            }

            LastMutationTime = now;

            if (blackboard.UnderThreat || blackboard.Stability < 38f)
            {
                Phase = blackboard.WarReadiness >= 0.55f ? AIPresidentPhase.Siege : AIPresidentPhase.Collapse;
            }
            else if (blackboard.DominantThreatWeight >= 0.60f || blackboard.PlayerPressure > 0.50f)
            {
                Phase = AIPresidentPhase.Retaliation;
            }
            else if (blackboard.CanExpand && blackboard.EconomyHealth >= 0.60f)
            {
                Phase = Aggression >= 0.58f ? AIPresidentPhase.Opportunist : AIPresidentPhase.BuildUp;
            }
            else
            {
                Phase = AIPresidentPhase.BuildUp;
            }

            float aggressionDelta = Phase == AIPresidentPhase.Retaliation ? 0.05f : (Phase == AIPresidentPhase.Collapse ? -0.04f : 0.01f);
            float navalDelta = blackboard.EnemyAcrossOcean ? 0.03f : -0.01f;
            float industryDelta = blackboard.CriticalNeed == RecursoMercado.Aco || blackboard.CriticalNeed == RecursoMercado.Armamentos ? 0.03f : 0f;

            Aggression = Mathf.Clamp01(Aggression + aggressionDelta);
            NavalFocus = Mathf.Clamp01(NavalFocus + navalDelta);
            IndustrialFocus = Mathf.Clamp01(IndustrialFocus + industryDelta);
            DiplomaticCunning = Mathf.Clamp01(DiplomaticCunning + (blackboard.CriticalNeed != RecursoMercado.Nenhum ? 0.02f : -0.01f));
        }

        private static string ResolveArchetype(DadosPaisGoverno pais)
        {
            string presidente = pais != null ? (pais.nomePresidente ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
            if (presidente.Contains("almir") || presidente.Contains("sea") || presidente.Contains("nav"))
            {
                return "Talassocrata";
            }
            if (presidente.Contains("marshal") || presidente.Contains("general") || presidente.Contains("war"))
            {
                return "Junta";
            }
            if (presidente.Contains("merc") || presidente.Contains("trade") || presidente.Contains("bank"))
            {
                return "Mercantil";
            }

            if (pais != null)
            {
                switch (pais.perfilIA)
                {
                    case PerfilPaisIA.Militarista:
                        return "Junta";
                    case PerfilPaisIA.ProdutorPetroleo:
                        return "Petroestado";
                    case PerfilPaisIA.Industrial:
                        return "Industrial";
                    case PerfilPaisIA.Aliado:
                        return "Diplomatico";
                    case PerfilPaisIA.Rival:
                        return "Revanchista";
                }
            }

            return "Equilibrado";
        }

        private static float ResolveAggression(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = pais != null ? Mathf.Clamp01(pais.pesoAgressividade) : 0.35f;
            if (archetype == "Junta" || archetype == "Revanchista")
            {
                baseValue += 0.20f;
            }
            if (archetype == "Diplomatico")
            {
                baseValue -= 0.10f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.08f, 0.12f));
        }

        private static float ResolveParanoia(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = pais != null ? Mathf.Clamp01(pais.pesoOdioRivais) : 0.45f;
            if (archetype == "Petroestado" || archetype == "Revanchista")
            {
                baseValue += 0.14f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.06f, 0.12f));
        }

        private static float ResolveIndustry(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = pais != null ? Mathf.Clamp01(pais.pesoIndustria) : 0.50f;
            if (archetype == "Industrial" || archetype == "Petroestado")
            {
                baseValue += 0.18f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.08f, 0.10f));
        }

        private static float ResolveNaval(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = archetype == "Talassocrata" ? 0.78f : 0.42f;
            if (pais != null && pais.perfilIA == PerfilPaisIA.ProdutorPetroleo)
            {
                baseValue += 0.12f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.10f, 0.14f));
        }

        private static float ResolveDiplomacy(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = pais != null ? Mathf.Clamp01(pais.pesoDiplomacia) : 0.50f;
            if (archetype == "Diplomatico" || archetype == "Mercantil")
            {
                baseValue += 0.16f;
            }
            if (archetype == "Junta")
            {
                baseValue -= 0.10f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.07f, 0.10f));
        }

        private static float ResolveRevenge(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = pais != null ? Mathf.Clamp01(pais.pesoOdioRivais) : 0.45f;
            if (archetype == "Revanchista" || archetype == "Junta")
            {
                baseValue += 0.18f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.06f, 0.12f));
        }

        private static float ResolveLossTolerance(DadosPaisGoverno pais, string archetype)
        {
            float baseValue = archetype == "Junta" ? 0.72f : 0.45f;
            if (pais != null && pais.perfilIA == PerfilPaisIA.Pequeno)
            {
                baseValue -= 0.14f;
            }
            return Mathf.Clamp01(baseValue + UnityEngine.Random.Range(-0.10f, 0.10f));
        }
    }

    [Serializable]
    public sealed class AIStrategicBlackboard
    {
        public int TeamId;
        public int PlayerTeamId;
        public int RivalTeamId;
        public string StrategicPlan = "Equilibrio";
        public Vector3 BaseCenter;
        public Vector3 EnemyAnchor;
        public bool UnderThreat;
        public bool CanExpand;
        public bool EnemyAcrossOcean;
        public bool AtWar;
        public float Stability;
        public float EconomyHealth;
        public float WarReadiness;
        public float PlayerPressure;
        public float DominantThreatWeight;
        public RecursoMercado CriticalNeed = RecursoMercado.Nenhum;
        public RecursoMercado BestSurplus = RecursoMercado.Nenhum;
        public AISovereignDomain DominantThreatDomain = AISovereignDomain.Land;
        public AISovereignEnvelope Envelope = AISovereignEnvelope.Justa;
        public int OwnLandUnits;
        public int OwnNavalUnits;
        public int OwnAirUnits;
        public int VisibleEnemyLand;
        public int VisibleEnemyNaval;
        public int VisibleEnemyAir;
        public int RadarCount;
        public int AirportCount;
        public int ShipyardCount;
        public int PlatformCount;
        public int FactoryCount;
        public int WarehouseCount;
        public int BarracksCount;
        public int NavalTransportCount;
        public int FighterCount;
        public float LastUpdatedTime;
    }

    [Serializable]
    public sealed class AICombatPackage
    {
        public AICombatPackageType Type;
        public AISovereignDomain Domain;
        public int TargetTeamId;
        public string TargetTag = string.Empty;
        public Vector3 TargetPoint;
        public Vector3 StagingPoint;
        public Transform TargetTransform;
        public bool MaintainCombatMode = true;
        public bool PreferSensorBlind = false;
        public int MaxUnits = 6;
        public float CooldownSeconds = 10f;
        public int Priority = 500;

        public string BuildDedupKey()
        {
            string point = string.Format("{0}:{1}", Mathf.RoundToInt(TargetPoint.x), Mathf.RoundToInt(TargetPoint.z));
            return Type + ":" + Domain + ":" + TargetTag + ":" + TargetTeamId + ":" + point;
        }
    }

    public static class AIControlAuthority
    {
        private struct ClaimEntry
        {
            public string OwnerKey;
            public float ClaimedAt;
        }

        private static readonly Dictionary<int, ClaimEntry> Claims = new Dictionary<int, ClaimEntry>();

        public static bool Claim(int teamId, string ownerKey)
        {
            if (teamId <= 0 || string.IsNullOrWhiteSpace(ownerKey))
            {
                return false;
            }

            ClaimEntry entry;
            if (Claims.TryGetValue(teamId, out entry))
            {
                return string.Equals(entry.OwnerKey, ownerKey, StringComparison.Ordinal);
            }

            Claims[teamId] = new ClaimEntry
            {
                OwnerKey = ownerKey,
                ClaimedAt = Time.unscaledTime
            };
            return true;
        }

        public static void Release(int teamId, string ownerKey)
        {
            ClaimEntry entry;
            if (!Claims.TryGetValue(teamId, out entry))
            {
                return;
            }

            if (!string.Equals(entry.OwnerKey, ownerKey, StringComparison.Ordinal))
            {
                return;
            }

            Claims.Remove(teamId);
        }

        public static bool CanIssue(int teamId, string ownerKey)
        {
            if (teamId <= 0 || string.IsNullOrWhiteSpace(ownerKey))
            {
                return false;
            }

            ClaimEntry entry;
            if (!Claims.TryGetValue(teamId, out entry))
            {
                return false;
            }

            return string.Equals(entry.OwnerKey, ownerKey, StringComparison.Ordinal);
        }
    }

    public sealed class AISovereignRuntime
    {
        private sealed class ControllerState
        {
            public int TeamId;
            public AISovereignSeverity StableSeverity;
            public int EscalateVotes;
            public int RelaxVotes;
        }

        private static AISovereignRuntime _instance;
        public static AISovereignRuntime Instance
        {
            get { return _instance ?? (_instance = new AISovereignRuntime()); }
        }

        private readonly Dictionary<int, ControllerState> _controllers = new Dictionary<int, ControllerState>(16);
        private readonly List<int> _orderedIds = new List<int>(16);
        private readonly Dictionary<int, int> _teamOwners = new Dictionary<int, int>(16);

        public int GlobalAiTarget = 3;
        public int GlobalCommandCap = 24;
        public float TargetMinFps = 55f;

        public void Register(int controllerId, int teamId)
        {
            if (!_controllers.ContainsKey(controllerId))
            {
                _controllers.Add(controllerId, new ControllerState
                {
                    TeamId = teamId,
                    StableSeverity = AISovereignSeverity.Stable
                });
                _orderedIds.Add(controllerId);
            }

            _teamOwners[teamId] = controllerId;
        }

        public void Unregister(int controllerId)
        {
            ControllerState state;
            if (_controllers.TryGetValue(controllerId, out state))
            {
                if (_teamOwners.TryGetValue(state.TeamId, out int owner) && owner == controllerId)
                {
                    _teamOwners.Remove(state.TeamId);
                }
            }

            _controllers.Remove(controllerId);
            _orderedIds.Remove(controllerId);
        }

        public bool HasControllerForTeam(int teamId)
        {
            return _teamOwners.ContainsKey(teamId);
        }

        public int ResolveCommandCap(int requestedByController)
        {
            int count = Mathf.Max(1, _orderedIds.Count);
            int perAiCap = Mathf.Max(2, GlobalCommandCap / count);
            return Mathf.Max(2, Mathf.Min(requestedByController, perAiCap));
        }

        public float ResolveBudgetScale(float smoothedFps)
        {
            int count = Mathf.Max(1, _orderedIds.Count);
            if (count <= GlobalAiTarget && smoothedFps >= TargetMinFps)
            {
                return 1f;
            }

            float overload = count > GlobalAiTarget ? Mathf.Clamp01((count - GlobalAiTarget) / 4f) : 0f;
            float fpsPenalty = smoothedFps < TargetMinFps
                ? Mathf.Clamp01((TargetMinFps - smoothedFps) / Mathf.Max(8f, TargetMinFps))
                : 0f;
            float scale = 1f - (overload * 0.28f) - (fpsPenalty * 0.45f);
            return Mathf.Clamp(scale, 0.30f, 1f);
        }

        public bool ShouldRunHeavy(int controllerId, int frameIndex)
        {
            int count = _orderedIds.Count;
            if (count <= 1)
            {
                return true;
            }

            int slot = _orderedIds.IndexOf(controllerId);
            if (slot < 0)
            {
                return true;
            }

            return (frameIndex % count) == slot;
        }

        public AISovereignSeverity ResolveSeverity(int controllerId, AISovereignSeverity measured, float smoothedFps, float minimumSafeFps)
        {
            ControllerState state;
            if (!_controllers.TryGetValue(controllerId, out state))
            {
                return measured;
            }

            AISovereignSeverity wanted = measured;
            if (smoothedFps < Mathf.Min(TargetMinFps, minimumSafeFps))
            {
                wanted = wanted < AISovereignSeverity.Watch ? AISovereignSeverity.Watch : wanted;
            }

            if (wanted > state.StableSeverity)
            {
                state.EscalateVotes++;
                state.RelaxVotes = 0;
                if (state.EscalateVotes >= 2)
                {
                    state.StableSeverity = wanted;
                    state.EscalateVotes = 0;
                }
            }
            else if (wanted < state.StableSeverity)
            {
                state.RelaxVotes++;
                state.EscalateVotes = 0;
                if (state.RelaxVotes >= 5)
                {
                    state.StableSeverity = wanted;
                    state.RelaxVotes = 0;
                }
            }
            else
            {
                state.EscalateVotes = 0;
                state.RelaxVotes = 0;
            }

            return state.StableSeverity;
        }
    }
}
