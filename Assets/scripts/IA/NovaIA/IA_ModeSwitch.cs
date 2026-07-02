using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-1000)]
    public sealed class IA_ModeSwitch : MonoBehaviour
    {
        public enum AIStackMode
        {
            BrainMaster = 0,
            NovaIA = 1,
            Legacy = 2
        }

        private static readonly Type[] LegacyStackTypes =
        {
            typeof(IA_Comandante),
            typeof(IA_General),
            typeof(IA_General_Pro),
            typeof(IA_Arquiteto_Pro),
            typeof(CerebroIA),
            typeof(IA_Dominadora),
            typeof(IA_Suprema)
        };

        [SerializeField] private AIStackMode _mode = AIStackMode.BrainMaster;
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _logChanges = false;

        private static IA_ModeSwitch _instance;
        private AIStackMode _lastAppliedMode = (AIStackMode)(-1);
        private bool _sceneLoadedHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDefaultModeSwitch()
        {
            IA_ModeSwitch existing = UnityEngine.Object.FindFirstObjectByType<IA_ModeSwitch>();
            if (existing != null)
            {
                return;
            }

            GameObject root = new GameObject("IA_ModeSwitch_Auto");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<IA_ModeSwitch>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            HookSceneLoaded();

            if (_applyOnAwake)
            {
                ApplyMode(true);
            }
        }

        private void OnEnable()
        {
            HookSceneLoaded();
            if (_applyOnAwake)
            {
                ApplyMode(true);
            }
        }

        private void OnDisable()
        {
            UnhookSceneLoaded();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            UnhookSceneLoaded();
        }

        [ContextMenu("Apply Mode")]
        public void ApplyMode()
        {
            ApplyMode(false);
        }

        public void ApplyMode(bool force)
        {
            if (!force && _lastAppliedMode == _mode)
            {
                return;
            }

            _lastAppliedMode = _mode;

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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastAppliedMode = (AIStackMode)(-1);
            if (_applyOnAwake)
            {
                ApplyMode(true);
            }
        }

        private void HookSceneLoaded()
        {
            if (_sceneLoadedHooked)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneLoadedHooked = true;
        }

        private void UnhookSceneLoaded()
        {
            if (!_sceneLoadedHooked)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneLoadedHooked = false;
        }

        private void ApplyNovaIAMode()
        {
            SetNamespaceEnabled("Hegemonia.AI.BrainMaster", false);
            SetNamespaceEnabled("Hegemonia.AI.DEUSA", false);
            SetExactTypesEnabled(false, LegacyStackTypes);
            SetNamespaceEnabled("Hegemonia.AI.Master", true, excludeSelf: true);
        }

        private void ApplyBrainMasterMode()
        {
            SetNamespaceEnabled("Hegemonia.AI.Master", false, excludeSelf: true);
            SetNamespaceEnabled("Hegemonia.AI.DEUSA", true);
            SetNamespaceEnabled("Hegemonia.AI.BrainMaster", true);
            SetExactTypesEnabled(false, LegacyStackTypes);
        }

        private void ApplyLegacyMode()
        {
            SetNamespaceEnabled("Hegemonia.AI.BrainMaster", false);
            SetNamespaceEnabled("Hegemonia.AI.DEUSA", false);
            SetNamespaceEnabled("Hegemonia.AI.Master", false, excludeSelf: true);
            SetExactTypesEnabled(true, LegacyStackTypes);
        }

        private void SetNamespaceEnabled(string ns, bool enabledValue, bool excludeSelf = false)
        {
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                {
                    continue;
                }

                if (excludeSelf && mb == this)
                {
                    continue;
                }

                string typeNamespace = mb.GetType().Namespace ?? string.Empty;
                if (typeNamespace != ns)
                {
                    continue;
                }

                SetEnabled(mb, enabledValue);
            }
        }

        private void SetExactTypesEnabled(bool enabledValue, params Type[] types)
        {
            if (types == null || types.Length == 0)
            {
                return;
            }

            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                {
                    continue;
                }

                Type behaviourType = mb.GetType();
                bool matches = false;
                for (int t = 0; t < types.Length; t++)
                {
                    if (behaviourType == types[t])
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                SetEnabled(mb, enabledValue);
            }
        }

        private void SetEnabled(MonoBehaviour behaviour, bool enabledValue)
        {
            if (behaviour.enabled == enabledValue)
            {
                return;
            }

            behaviour.enabled = enabledValue;
            if (_logChanges)
            {
                Debug.Log("[IA_ModeSwitch] " + behaviour.GetType().Name + " => " + enabledValue, behaviour);
            }
        }
    }
}
