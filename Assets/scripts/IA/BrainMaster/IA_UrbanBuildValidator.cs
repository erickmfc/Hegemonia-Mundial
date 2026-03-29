using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_UrbanBuildValidator
    {
        private readonly IA_Context _context;

        public IA_UrbanBuildValidator(IA_Context context)
        {
            _context = context;
        }

        public bool ValidateLot(string itemKey, IA_ZoneType zone, Vector3 position, Quaternion rotation, out string reason)
        {
            reason = string.Empty;

            if (_context == null || _context.Backend == null || _context.Backend.BuildService == null)
            {
                reason = "validator urbano sem dependencias";
                return false;
            }

            if (_context.SemanticMapPlanner != null && _context.SemanticMapPlanner.TryGetCell(position, out var cell))
            {
                if (cell.Forbidden || !cell.Buildable)
                {
                    reason = "terreno semantico invalido";
                    return false;
                }

                if (cell.Occupied || cell.Reserved)
                {
                    reason = "lote ocupado ou reservado";
                    return false;
                }

                if (_context.ZonePlanner != null)
                {
                    IA_UrbanSectorType expectedSector = _context.ZonePlanner.ResolveSector(zone);
                    if (!IsSectorAccepted(expectedSector, cell.Sector))
                    {
                        reason = "setor urbano incompativel";
                        return false;
                    }
                }
            }

            return _context.Backend.BuildService.ValidatePlacement(
                itemKey,
                position,
                rotation,
                zone,
                _context.WorldState,
                _context.MapAnalyzer,
                _context.ThreatAnalyzer,
                out reason);
        }

        private static bool IsSectorAccepted(IA_UrbanSectorType expected, IA_UrbanSectorType actual)
        {
            if (actual == IA_UrbanSectorType.None || actual == IA_UrbanSectorType.Buffer)
            {
                return true;
            }

            if (expected == actual)
            {
                return true;
            }

            if (expected == IA_UrbanSectorType.Industrial && actual == IA_UrbanSectorType.Logistics)
            {
                return true;
            }

            if (expected == IA_UrbanSectorType.Military && actual == IA_UrbanSectorType.Logistics)
            {
                return true;
            }

            if (expected == IA_UrbanSectorType.Command && actual == IA_UrbanSectorType.Logistics)
            {
                return true;
            }

            return false;
        }
    }
}
