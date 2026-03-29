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
            lot = null;
            reason = string.Empty;

            if (_context == null || _context.Backend == null || _context.LotPlanner == null)
            {
                reason = "construction planner sem dependencias";
                return false;
            }

            DadosConstrucao data;
            if (!_context.Backend.TryResolveItem(itemKey, out data) || data == null || data.prefabDaUnidade == null)
            {
                reason = "item nao encontrado";
                return false;
            }

            if (!_context.LotPlanner.TryFindBestLot(itemKey, zone, data.prefabDaUnidade, out lot))
            {
                reason = "nenhum lote valido";
                return false;
            }

            reason = lot.ValidationMessage;
            return lot.Valid;
        }
    }
}
