using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public sealed class IA_ManualPlacementTag : MonoBehaviour
    {
        [TextArea(1, 2)] public string SourceLabel = string.Empty;
    }
}
