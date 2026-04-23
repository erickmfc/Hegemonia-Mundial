using UnityEngine;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-1000)]
    public sealed class IA_ModeSwitch : MonoBehaviour
    {
        public enum AIStackMode
        {
            NovaIA = 0,
            BrainMaster = 1,
            Legacy = 2
        }

        [SerializeField] private AIStackMode _mode = AIStackMode.NovaIA;
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _logChanges = true;

        private static bool _appliedOnce;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDefaultModeSwitch()
        {
            IA_ModeSwitch existing = Object.FindFirstObjectByType<IA_ModeSwitch>();
            if (existing != null)
            {
                existing.ApplyMode();
                return;
            }

            GameObject root = new GameObject("IA_ModeSwitch_Auto");
            IA_ModeSwitch modeSwitch = root.AddComponent<IA_ModeSwitch>();
            modeSwitch.ApplyMode();
            Object.DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (_applyOnAwake)
            {
                ApplyMode();
            }
        }

        [ContextMenu("Apply Mode")]
        public void ApplyMode()
        {
            if (_appliedOnce && !Application.isEditor)
            {
                return;
            }

            _appliedOnce = true;
            switch (_mode)
            {
                case AIStackMode.NovaIA:
                    ApplyNovaIAMode();
                    break;
                case AIStackMode.BrainMaster:
                    ApplyBrainMasterMode();
                    break;
                case AIStackMode.Legacy:
                    ApplyLegacyMode();
                    break;
            }
        }

        private void ApplyNovaIAMode()
        {
            SetScriptsEnabledByTypeName("IA_MasterController", true);
            SetScriptsEnabledByNamespace("Hegemonia.AI.BrainMaster", false);
            SetScriptsEnabledByTypeName("IA_Dominadora", false);
            SetScriptsEnabledByTypeName("IA_Suprema", false);
            SetScriptsEnabledByTypeName("IA_Comandante", false);
            SetScriptsEnabledByTypeName("IA_General", false);
            SetScriptsEnabledByTypeName("CerebroIA", false);
        }

        private void ApplyBrainMasterMode()
        {
            SetScriptsEnabledByTypeName("IA_MasterController", false);
            SetScriptsEnabledByNamespace("Hegemonia.AI.BrainMaster", true);
        }

        private void ApplyLegacyMode()
        {
            SetScriptsEnabledByTypeName("IA_MasterController", false);
            SetScriptsEnabledByNamespace("Hegemonia.AI.BrainMaster", false);
            SetScriptsEnabledByTypeName("IA_Dominadora", true);
            SetScriptsEnabledByTypeName("IA_Suprema", true);
            SetScriptsEnabledByTypeName("IA_Comandante", true);
            SetScriptsEnabledByTypeName("IA_General", true);
            SetScriptsEnabledByTypeName("CerebroIA", true);
        }

        private void SetScriptsEnabledByNamespace(string ns, bool enabledValue)
        {
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                {
                    continue;
                }

                string typeNamespace = mb.GetType().Namespace ?? string.Empty;
                if (typeNamespace != ns)
                {
                    continue;
                }

                mb.enabled = enabledValue;
                if (_logChanges)
                {
                    Debug.Log("[IA_ModeSwitch] " + mb.GetType().Name + " => " + enabledValue, mb);
                }
            }
        }

        private void SetScriptsEnabledByTypeName(string typeNameContains, bool enabledValue)
        {
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                {
                    continue;
                }

                string typeName = mb.GetType().Name;
                if (typeName.IndexOf(typeNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                mb.enabled = enabledValue;
                if (_logChanges)
                {
                    Debug.Log("[IA_ModeSwitch] " + typeName + " => " + enabledValue, mb);
                }
            }
        }
    }
}
