using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    [DisallowMultipleComponent]
    public sealed class IA_IdentityRegistryHook : MonoBehaviour
    {
        private IdentidadeUnidade _identity;

        private void Awake()
        {
            _identity = GetComponent<IdentidadeUnidade>();
            if (_identity != null)
            {
                IA_WorldState.Register(_identity);
            }
        }

        private void OnEnable()
        {
            if (_identity == null)
            {
                _identity = GetComponent<IdentidadeUnidade>();
            }

            if (_identity != null)
            {
                IA_WorldState.Register(_identity);
            }
        }

        private void OnDisable()
        {
            if (_identity != null)
            {
                IA_WorldState.Unregister(_identity);
            }
        }

        private void OnDestroy()
        {
            if (_identity != null)
            {
                IA_WorldState.Unregister(_identity);
            }
        }
    }
}
