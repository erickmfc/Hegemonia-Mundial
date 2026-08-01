using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.Shared;

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

        private static readonly Type[] LegacyStackTypes = IA_SharedRuntimeSupport.LegacyStackTypes;

        [SerializeField] private AIStackMode _mode = AIStackMode.BrainMaster;
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _logChanges = false;

        private static IA_ModeSwitch _instance;
        private AIStackMode _lastAppliedMode = (AIStackMode)(-1);
        private bool _sceneLoadedHooked;

        public static AIStackMode CurrentMode
        {
            get { return _instance != null ? _instance._mode : AIStackMode.BrainMaster; }
        }

        public static bool IsBrainMasterMode
        {
            get { return CurrentMode == AIStackMode.BrainMaster; }
        }

        public static bool IsNovaIAMode
        {
            get { return CurrentMode == AIStackMode.NovaIA; }
        }

        public static bool IsLegacyMode
        {
            get { return CurrentMode == AIStackMode.Legacy; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDefaultModeSwitch()
        {
            IA_ModeSwitch existing = IA_UnitySearch.FindFirst<IA_ModeSwitch>();
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
            _instance = this;

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
            MonoBehaviour[] all = IA_UnitySearch.FindAll<MonoBehaviour>();
            SetNamespaceEnabled(all, "Hegemonia.AI.BrainMaster", false);
            SetNamespaceEnabled(all, "Hegemonia.AI.DEUSA", false);
            SetNamespaceEnabled(all, "Hegemonia.AI.Sovereign", false);
            SetExactTypesEnabled(all, false, LegacyStackTypes);
            SetNamespaceEnabled(all, "Hegemonia.AI.Master", true, excludeSelf: true);
        }

        private void ApplyBrainMasterMode()
        {
            MonoBehaviour[] all = IA_UnitySearch.FindAll<MonoBehaviour>();
            SetNamespaceEnabled(all, "Hegemonia.AI.Master", false, excludeSelf: true);

            // A cena 19 foi reconstruida a partir de dados recuperados. Nela,
            // o BrainMaster/DEUSA legado fica deliberadamente bloqueado durante
            // o bootstrap; reativá-lo automaticamente aqui fazia a cena travar
            // antes do primeiro frame jogável.
            if (SceneManager.GetActiveScene().name == ConfiguracaoCenasJogo.CenaCampanhaCanonica)
            {
                SetNamespaceEnabled(all, "Hegemonia.AI.DEUSA", false);
                SetNamespaceEnabled(all, "Hegemonia.AI.BrainMaster", false);
                SetNamespaceEnabled(all, "Hegemonia.AI.Sovereign", false);
                SetExactTypesEnabled(all, false, LegacyStackTypes);
                return;
            }

            SetNamespaceEnabled(all, "Hegemonia.AI.DEUSA", true);
            SetNamespaceEnabled(all, "Hegemonia.AI.BrainMaster", true);
            SetNamespaceEnabled(all, "Hegemonia.AI.Sovereign", true);
            SetExactTypesEnabled(all, false, LegacyStackTypes);
        }

        private void ApplyLegacyMode()
        {
            MonoBehaviour[] all = IA_UnitySearch.FindAll<MonoBehaviour>();
            SetNamespaceEnabled(all, "Hegemonia.AI.BrainMaster", false);
            SetNamespaceEnabled(all, "Hegemonia.AI.DEUSA", false);
            SetNamespaceEnabled(all, "Hegemonia.AI.Sovereign", false);
            SetNamespaceEnabled(all, "Hegemonia.AI.Master", false, excludeSelf: true);
            SetExactTypesEnabled(all, true, LegacyStackTypes);
        }

        private void SetNamespaceEnabled(MonoBehaviour[] all, string ns, bool enabledValue, bool excludeSelf = false)
        {
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

        private void SetExactTypesEnabled(MonoBehaviour[] all, bool enabledValue, params Type[] types)
        {
            if (types == null || types.Length == 0)
            {
                return;
            }

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
