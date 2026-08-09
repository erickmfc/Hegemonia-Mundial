using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ConstructionPlanner : IIAUpdateModule
    {
        private readonly IA_Context _context;

        public IA_BaseBlueprintProfile ActiveBlueprint { get; private set; }
        public string LastSummary { get; private set; }

        public IA_ConstructionPlanner(IA_Context context)
        {
            _context = context;
            ActiveBlueprint = IA_BaseBlueprintProfile.CreateDefault();
        }

        public string Name
        {
            get { return "IA_ConstructionPlanner"; }
        }

        public float Interval
        {
            get { return 2.75f; }
        }

        public float BudgetMs
        {
            get { return 0.25f; }
        }

        public void Tick(float now, float deltaTime)
        {
            if (_context == null || _context.ZonePlanner == null)
            {
                LastSummary = "blueprint=" + ActiveBlueprint.Id + " status=sem zone planner";
                return;
            }

            Vector3 commandAnchor;
            Vector3 airAnchor;
            Vector3 navalAnchor;

            _context.ZonePlanner.TryGetAnchor(IA_UrbanSectorType.Command, out commandAnchor);
            _context.ZonePlanner.TryGetAnchor(IA_UrbanSectorType.Airfield, out airAnchor);
            _context.ZonePlanner.TryGetAnchor(IA_UrbanSectorType.Naval, out navalAnchor);

            LastSummary =
                "blueprint=" + ActiveBlueprint.Id
                + " command=" + commandAnchor
                + " air=" + airAnchor
                + " naval=" + navalAnchor;
        }

        public bool TryPlanBuild(string itemKey, IA_ZoneType zone, out IA_LotCandidate lot, out string reason)
        {
            return TryPlanBuild(itemKey, zone, Vector3.zero, IA_TerrainType.Unknown, out lot, out reason);
        }

        public bool TryPlanBuild(string itemKey, IA_ZoneType zone, Vector3 anchor, IA_TerrainType desiredTerrain, out IA_LotCandidate lot, out string reason)
        {
            lot = null;
            reason = string.Empty;

            if (_context == null || _context.Backend == null || _context.LotPlanner == null)
            {
                reason = "construction planner sem dependencias";
                LastSummary = "blueprint=" + ActiveBlueprint.Id
                              + " item=" + itemKey
                              + " terrain=" + desiredTerrain
                              + " status=sem dependencias";
                return false;
            }

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.PrefabDaUnidade == null)
            {
                reason = "item nao encontrado";
                LastSummary = "blueprint=" + ActiveBlueprint.Id
                              + " item=" + itemKey
                              + " terrain=" + desiredTerrain
                              + " status=item nao encontrado";
                return false;
            }

            IA_UrbanSectorType sector = _context.ZonePlanner != null
                ? _context.ZonePlanner.ResolveSector(zone)
                : IA_UrbanSectorType.Logistics;

            Vector3 resolvedAnchor = anchor;
            if (resolvedAnchor == Vector3.zero && _context.ZonePlanner != null)
            {
                _context.ZonePlanner.TryGetAnchor(zone, out resolvedAnchor);
            }

            if (resolvedAnchor == Vector3.zero && _context.Brain != null)
            {
                resolvedAnchor = _context.Brain.transform.position;
            }

            if (!_context.LotPlanner.TryFindBestLot(itemKey, zone, data.PrefabDaUnidade, out lot))
            {
                reason = "nenhum lote valido";
                LastSummary = "blueprint=" + ActiveBlueprint.Id
                              + " item=" + data.GetDisplayName()
                              + " sector=" + sector
                              + " anchor=" + resolvedAnchor
                              + " terrain=" + desiredTerrain
                              + " fallback=lot";
                return false;
            }

            reason = lot.ValidationMessage;
            LastSummary = "blueprint=" + ActiveBlueprint.Id
                          + " item=" + data.GetDisplayName()
                          + " sector=" + sector
                          + " anchor=" + resolvedAnchor
                          + " terrain=" + desiredTerrain
                          + " lot=" + lot.Position
                          + " score=" + lot.Score.ToString("0.0")
                          + (string.IsNullOrEmpty(reason) ? string.Empty : " validation=" + reason);
            return lot.Valid;
        }
    }
}
