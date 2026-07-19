using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaMapaMemoria
    {
        private float _lastRefreshTime = -999f;
        private int _lastStructureCount = -1;

        public bool TemAreaNavalValida { get; private set; }
        public bool TemAreaAereaValida { get; private set; }
        public bool TemTerraSegura { get; private set; }
        public bool TemCosta { get; private set; }
        public int TagsManuaisEncontradas { get; private set; } = -1;
        public Vector3 AncoraTerraSegura { get; private set; }
        public Vector3 AncoraCosta { get; private set; }
        public Vector3 AncoraAerea { get; private set; }
        public Vector3 AncoraNaval { get; private set; }
        public Vector3 AncoraExpansao { get; private set; }
        public string UltimoResumo { get; private set; } = "cache de mapa indisponivel";

        public void Atualizar(IA_Context context, float now, bool force = false)
        {
            if (context == null || context.MapAnalyzer == null || context.WorldState == null)
            {
                return;
            }

            IA_ForceSnapshot snapshot = context.ForceSnapshot ?? context.WorldState.ForceSnapshot;
            int structureCount = snapshot != null ? snapshot.TotalOwnStructures : 0;
            if (!force && structureCount == _lastStructureCount && now - _lastRefreshTime < 8f)
            {
                return;
            }

            _lastRefreshTime = now;
            _lastStructureCount = structureCount;

            Vector3 baseCenter = context.WorldState.BaseCenter;
            if (baseCenter == Vector3.zero && context.Brain != null)
            {
                baseCenter = context.Brain.transform.position;
            }

            AncoraTerraSegura = context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Land, 40f, 220f, 18);
            AncoraCosta = context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Coast, 100f, 420f, 24);
            AncoraAerea = context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Open, 90f, 260f, 22);
            AncoraNaval = context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.Water, 120f, 440f, 24);
            AncoraExpansao = context.MapAnalyzer.FindPointInTerrain(baseCenter, IA_TerrainType.City, 180f, 520f, 26);

            TemTerraSegura = AncoraTerraSegura != Vector3.zero;
            TemCosta = AncoraCosta != Vector3.zero;
            TemAreaAereaValida = AncoraAerea != Vector3.zero && TemTerraSegura;
            TemAreaNavalValida = AncoraNaval != Vector3.zero || TemCosta;

            if (TagsManuaisEncontradas < 0) {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
                TagsManuaisEncontradas = Object.FindObjectsByType<IA_ManualPlacementTag>(FindObjectsSortMode.None).Length;
#else
                TagsManuaisEncontradas = Object.FindObjectsOfType<IA_ManualPlacementTag>().Length;
#endif
            }

            UltimoResumo = "terra=" + TemTerraSegura
                           + " | costa=" + TemCosta
                           + " | naval=" + TemAreaNavalValida
                           + " | aereo=" + TemAreaAereaValida
                           + " | tags=" + TagsManuaisEncontradas;
        }
    }
}
