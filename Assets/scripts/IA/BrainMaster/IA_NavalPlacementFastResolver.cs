using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public static class IA_NavalPlacementFastResolver
    {
        public struct ResolveResult
        {
            public bool Success;
            public Vector3 Position;
            public string Reason;
        }

        public static ResolveResult TryResolveShipyard(
            IA_Context context,
            string itemKey,
            Vector3 anchor,
            float minRadius,
            float maxRadius)
        {
            return TryResolve(context, itemKey, anchor, minRadius, maxRadius, IA_NavalPlacementField.NavalPlacementKind.Shipyard);
        }

        public static ResolveResult TryResolvePier(
            IA_Context context,
            string itemKey,
            Vector3 anchor,
            float minRadius,
            float maxRadius)
        {
            return TryResolve(context, itemKey, anchor, minRadius, maxRadius, IA_NavalPlacementField.NavalPlacementKind.Pier);
        }

        public static ResolveResult TryResolvePlatform(
            IA_Context context,
            string itemKey,
            Vector3 anchor,
            float minRadius,
            float maxRadius)
        {
            return TryResolve(context, itemKey, anchor, minRadius, maxRadius, IA_NavalPlacementField.NavalPlacementKind.Platform);
        }

        private static ResolveResult TryResolve(
            IA_Context context,
            string itemKey,
            Vector3 anchor,
            float minRadius,
            float maxRadius,
            IA_NavalPlacementField.NavalPlacementKind kind)
        {
            ResolveResult result = new ResolveResult
            {
                Success = false,
                Position = anchor,
                Reason = "sem candidato rapido"
            };

            if (context == null || context.Backend == null || context.Backend.BuildService == null)
            {
                result.Reason = "contexto/buildservice ausente";
                return result;
            }

            Vector3 candidate;
            bool found = IA_NavalPlacementField.TryGetBestCandidate(
                anchor,
                minRadius,
                maxRadius,
                kind,
                point => CheapFilter(context, itemKey, kind, point),
                out candidate);

            if (!found)
            {
                result.Reason = "nenhuma celula naval valida no cache";
                return result;
            }

            string territoryReason;
            if (!context.Backend.BuildService.ValidateTerritoryProbe(itemKey, candidate, out territoryReason))
            {
                IA_NavalPlacementField.MarkCellTemporarilyBlocked(candidate, territoryReason, 35f);
                result.Reason = territoryReason;
                return result;
            }

            result.Success = true;
            result.Position = candidate;
            result.Reason = string.Empty;
            return result;
        }

        private static bool CheapFilter(
            IA_Context context,
            string itemKey,
            IA_NavalPlacementField.NavalPlacementKind kind,
            Vector3 point)
        {
            if (context == null)
            {
                return false;
            }

            if (context.Brain != null)
            {
                Vector3 brain = context.Brain.transform.position;
                float maxTeamDistance = kind == IA_NavalPlacementField.NavalPlacementKind.Platform ? 2200f : 1400f;
                if (Vector3.Distance(Flatten(brain), Flatten(point)) > maxTeamDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 Flatten(Vector3 p)
        {
            p.y = 0f;
            return p;
        }
    }
}
