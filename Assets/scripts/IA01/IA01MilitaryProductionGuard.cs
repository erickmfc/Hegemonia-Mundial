using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public enum IA01MilitaryAssetKind
    {
        Other,
        Infantry,
        Tank,
        Fighter,
        Naval,
        OilTanker
    }

    /// <summary>
    /// Coordena os dois diretores que podem publicar producao para a mesma
    /// nação. A guarda nao produz nada e nao altera a fila: apenas impede que
    /// duas intenções simultâneas contem o mesmo deficit duas vezes.
    /// </summary>
    public static class IA01MilitaryProductionGuard
    {
        private sealed class TeamState
        {
            public readonly Dictionary<IA01MilitaryAssetKind, List<float>> Pending =
                new Dictionary<IA01MilitaryAssetKind, List<float>>();
            public readonly Dictionary<IA01MilitaryAssetKind, int> LastObserved =
                new Dictionary<IA01MilitaryAssetKind, int>();
        }

        private static readonly Dictionary<int, TeamState> States = new Dictionary<int, TeamState>();

        public static IA01MilitaryAssetKind Classify(DadosConstrucao data)
        {
            if (data == null) return IA01MilitaryAssetKind.Other;
            string text = IA_Text.Normalize(data.GetDisplayName() + " " + data.name + " " + data.aliases);
            if (text.Contains("petroleiro") || text.Contains("petrolifero") || text.Contains("oil tanker") || text.Contains("tanker"))
                return IA01MilitaryAssetKind.OilTanker;
            if (data.categoria == DadosConstrucao.CategoriaItem.Marinha
                || data.HasCapability(IA_ConstructionCapability.Naval)
                || text.Contains("navio") || text.Contains("corveta") || text.Contains("fragata") || text.Contains("destroy"))
                return IA01MilitaryAssetKind.Naval;
            if (text.Contains("tank") || text.Contains("tanque") || text.Contains("mbt") || text.Contains("blindado"))
                return IA01MilitaryAssetKind.Tank;
            if (data.HasCapability(IA_ConstructionCapability.FighterAircraft)
                || text.Contains("caca") || text.Contains("fighter") || text.Contains("su11") || text.Contains("g15")
                || text.Contains("b260") || text.Contains("jet"))
                return IA01MilitaryAssetKind.Fighter;
            if (text.Contains("soldado") || text.Contains("soldier") || text.Contains("infantaria") || text.Contains("rifle") || text.Contains("fuzil"))
                return IA01MilitaryAssetKind.Infantry;
            return IA01MilitaryAssetKind.Other;
        }

        public static bool TryReserve(int teamId, IA01MilitaryAssetKind kind, int desired, int current, float now, float ttlSeconds = 45f)
        {
            if (teamId <= 0 || kind == IA01MilitaryAssetKind.Other || current >= desired) return false;
            TeamState state = GetState(teamId);
            Reconcile(state, kind, current, now);
            List<float> pending = GetPending(state, kind);
            if (current + pending.Count >= desired) return false;
            pending.Add(now + Mathf.Max(8f, ttlSeconds));
            return true;
        }

        /// <summary>Usado pelo ProductionDirector para limitar uma ordem por tipo.</summary>
        public static bool TryReserveSingle(int teamId, IA01MilitaryAssetKind kind, int current, float now, float ttlSeconds = 45f)
        {
            if (teamId <= 0 || kind == IA01MilitaryAssetKind.Other) return true;
            TeamState state = GetState(teamId);
            Reconcile(state, kind, current, now);
            List<float> pending = GetPending(state, kind);
            if (pending.Count > 0) return false;
            pending.Add(now + Mathf.Max(8f, ttlSeconds));
            return true;
        }

        public static void Cancel(int teamId, IA01MilitaryAssetKind kind, float now)
        {
            if (!States.TryGetValue(teamId, out TeamState state)) return;
            Reconcile(state, kind, state.LastObserved.ContainsKey(kind) ? state.LastObserved[kind] : 0, now);
            List<float> pending = GetPending(state, kind);
            if (pending.Count > 0) pending.RemoveAt(pending.Count - 1);
        }

        public static int CountOwnedUnique(int teamId, TipoUnidade type, Func<IdentidadeUnidade, bool> filter = null)
        {
            if (teamId <= 0) return 0;
            HashSet<int> keys = new HashSet<int>();
            IdentidadeUnidade[] identities = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                IdentidadeUnidade identity = identities[i];
                if (identity == null || !identity.gameObject.activeInHierarchy || identity.teamID != teamId || identity.tipoUnidade != type) continue;
                if (filter != null && !filter(identity)) continue;
                if (keys.Add(CanonicalKey(identity))) continue;
            }
            return keys.Count;
        }

        public static int CanonicalKey(IdentidadeUnidade identity)
        {
            if (identity == null) return 0;
            ControleUnidade control = identity.GetComponent<ControleUnidade>()
                ?? identity.GetComponentInParent<ControleUnidade>()
                ?? identity.GetComponentInChildren<ControleUnidade>(true);
            if (control != null) return control.GetInstanceID();
            return identity.transform.root != null ? identity.transform.root.GetInstanceID() : identity.GetInstanceID();
        }

        private static TeamState GetState(int teamId)
        {
            if (!States.TryGetValue(teamId, out TeamState state))
            {
                state = new TeamState();
                States.Add(teamId, state);
            }
            return state;
        }

        private static List<float> GetPending(TeamState state, IA01MilitaryAssetKind kind)
        {
            if (!state.Pending.TryGetValue(kind, out List<float> pending))
            {
                pending = new List<float>(4);
                state.Pending.Add(kind, pending);
            }
            return pending;
        }

        private static void Reconcile(TeamState state, IA01MilitaryAssetKind kind, int current, float now)
        {
            List<float> pending = GetPending(state, kind);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i] <= now) pending.RemoveAt(i);
            }

            if (state.LastObserved.TryGetValue(kind, out int previous) && current > previous)
            {
                int completed = Mathf.Min(current - previous, pending.Count);
                if (completed > 0) pending.RemoveRange(0, completed);
            }
            state.LastObserved[kind] = Mathf.Max(0, current);
        }
    }
}
