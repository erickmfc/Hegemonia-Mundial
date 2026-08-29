using System;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    public enum IA02MilitaryAssetKind
    {
        Other,
        Infantry,
        Tank,
        Fighter,
        Naval,
        OilTanker,
        AntiAir
    }

    /// <summary>
    /// Compatibilidade para os diretores existentes. Toda reserva passa pelo
    /// registro central, que acompanha id, finalidade e estado da ordem.
    /// </summary>
    public static class IA02MilitaryProductionGuard
    {
        public static IA02MilitaryAssetKind Classify(DadosConstrucao data)
        {
            if (data == null) return IA02MilitaryAssetKind.Other;
            string text = IA_Text.Normalize(data.GetDisplayName() + " " + data.name + " " + data.aliases);
            if (text.Contains("petroleiro") || text.Contains("petrolifero") || text.Contains("oil tanker") || text.Contains("tanker"))
                return IA02MilitaryAssetKind.OilTanker;
            if (data.categoria == DadosConstrucao.CategoriaItem.Marinha
                || data.HasCapability(IA_ConstructionCapability.Naval)
                || text.Contains("navio") || text.Contains("corveta") || text.Contains("fragata") || text.Contains("destroy"))
                return IA02MilitaryAssetKind.Naval;
            if (text.Contains("tank") || text.Contains("tanque") || text.Contains("mbt") || text.Contains("blindado"))
                return IA02MilitaryAssetKind.Tank;
            if (text.Contains("ares_ar") || text.Contains("ares ar") || text.Contains("antiaereo") || text.Contains("anti aereo"))
                return IA02MilitaryAssetKind.AntiAir;
            if (data.HasCapability(IA_ConstructionCapability.FighterAircraft)
                || text.Contains("caca") || text.Contains("fighter") || text.Contains("su11") || text.Contains("g15")
                || text.Contains("b260") || text.Contains("jet"))
                return IA02MilitaryAssetKind.Fighter;
            if (text.Contains("soldado") || text.Contains("soldier") || text.Contains("infantaria") || text.Contains("rifle") || text.Contains("fuzil"))
                return IA02MilitaryAssetKind.Infantry;
            return IA02MilitaryAssetKind.Other;
        }

        public static bool TryReserve(int teamId, IA02MilitaryAssetKind kind, int desired, int current, float now, float ttlSeconds = 45f)
        {
            return TryReserve(teamId, kind, desired, current, now, ttlSeconds, out _);
        }

        public static bool TryReserve(int teamId, IA02MilitaryAssetKind kind, int desired, int current, float now, float ttlSeconds, out string orderId)
        {
            return TryReserveProduction(teamId, kind, "military", desired, current, now, ttlSeconds, out orderId);
        }

        /// <summary>Usado pelo ProductionDirector para limitar uma ordem por tipo.</summary>
        public static bool TryReserveSingle(int teamId, IA02MilitaryAssetKind kind, int current, float now, float ttlSeconds = 45f)
        {
            return TryReserveSingle(teamId, kind, current, now, ttlSeconds, out _);
        }

        public static bool TryReserveSingle(int teamId, IA02MilitaryAssetKind kind, int current, float now, float ttlSeconds, out string orderId)
        {
            if (kind == IA02MilitaryAssetKind.Other) { orderId = string.Empty; return true; }
            return TryReserveProduction(teamId, kind, "military", current + 1, current, now, ttlSeconds, out orderId);
        }

        public static void Cancel(int teamId, IA02MilitaryAssetKind kind, float now)
        {
            string orderId = IAAutoProductionRegistry.FindActiveOrder(teamId, kind.ToString(), "military");
            if (!string.IsNullOrEmpty(orderId)) IAAutoProductionRegistry.Release(orderId, now);
        }

        public static bool TryReserveProduction(int teamId, IA02MilitaryAssetKind kind, string purpose, int desired, int current, float now, float ttlSeconds, out string orderId)
        {
            return IAAutoProductionRegistry.TryReserveProduction(teamId, kind.ToString(), purpose, desired, current, out orderId, now, Mathf.Max(8f, ttlSeconds));
        }

        public static void ConfirmQueued(string orderId, int producerInstanceId = 0, float now = -1f)
        {
            IAAutoProductionRegistry.ConfirmQueued(orderId, producerInstanceId, now);
        }

        public static void ConfirmConstructionStarted(string orderId, int producerInstanceId = 0, float now = -1f)
        {
            IAAutoProductionRegistry.ConfirmConstructionStarted(orderId, producerInstanceId, now);
        }

        public static void Complete(string orderId, float now = -1f)
        {
            IAAutoProductionRegistry.Complete(orderId, now);
        }

        public static void Release(string orderId, float now = -1f)
        {
            IAAutoProductionRegistry.Release(orderId, now);
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

    }
}
