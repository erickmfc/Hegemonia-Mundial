using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum RTSInputAction
    {
        Construction,
        Government,
        Pier,
        Barracks,
        StrategicMap,
        Follow,
        CommandMenu,
        CycleCameraPin
    }

    /// <summary>
    /// Mapa de atalhos comum. Mantem os atalhos legados como padrao, mas permite
    /// que a UI futura remapeie teclas sem alterar dezenas de scripts.
    /// </summary>
    public static class RTSInputBindings
    {
        private const string Prefix = "rts.input.";
        private static int textFocusCacheFrame = -1;
        private static bool textFocusCacheValue;
        private static readonly List<UIDocument> textInputDocuments = new List<UIDocument>();
        private static readonly Dictionary<RTSInputAction, KeyCode> Defaults = new Dictionary<RTSInputAction, KeyCode>
        {
            { RTSInputAction.Construction, KeyCode.C },
            { RTSInputAction.Government, KeyCode.X },
            { RTSInputAction.Pier, KeyCode.V },
            { RTSInputAction.Barracks, KeyCode.B },
            { RTSInputAction.StrategicMap, KeyCode.M },
            { RTSInputAction.Follow, KeyCode.F },
            { RTSInputAction.CommandMenu, KeyCode.Alpha1 },
            { RTSInputAction.CycleCameraPin, KeyCode.Alpha5 }
        };

        public static KeyCode GetKey(RTSInputAction action)
        {
            KeyCode fallback;
            if (!Defaults.TryGetValue(action, out fallback))
            {
                fallback = KeyCode.None;
            }

            string key = Prefix + action;
            if (!PlayerPrefs.HasKey(key))
            {
                return fallback;
            }

            int stored = PlayerPrefs.GetInt(key, (int)fallback);
            return Enum.IsDefined(typeof(KeyCode), stored) ? (KeyCode)stored : fallback;
        }

        public static bool GetKeyDown(RTSInputAction action)
        {
            if (IsTextEntryFocused()) return false;
            KeyCode key = GetKey(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool GetKey(RTSInputAction action, bool held)
        {
            if (IsTextEntryFocused()) return false;
            KeyCode key = GetKey(action);
            return key != KeyCode.None && (held ? Input.GetKey(key) : Input.GetKeyDown(key));
        }

        /// <summary>
        /// Prevents gameplay shortcuts from consuming characters typed into
        /// legacy uGUI/TMP fields or UI Toolkit text inputs.
        /// </summary>
        public static bool IsTextEntryFocused()
        {
            if (textFocusCacheFrame == Time.frameCount)
                return textFocusCacheValue;

            textFocusCacheFrame = Time.frameCount;
            textFocusCacheValue = IsLegacyTextInputFocused();
            if (!textFocusCacheValue)
                textFocusCacheValue = IsUiToolkitTextInputFocused();
            return textFocusCacheValue;
        }

        public static void RegisterTextInputDocument(UIDocument document)
        {
            if (document == null) return;
            textInputDocuments.Remove(document);
            textInputDocuments.Add(document);
            textFocusCacheFrame = -1;
        }

        public static void UnregisterTextInputDocument(UIDocument document)
        {
            if (document == null) return;
            textInputDocuments.Remove(document);
            textFocusCacheFrame = -1;
        }

        private static bool IsLegacyTextInputFocused()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            if (selected == null || !selected.activeInHierarchy)
                return false;

            if (selected.GetComponentInParent<InputField>() != null)
                return true;

            for (Transform current = selected.transform; current != null; current = current.parent)
            {
                if (current.GetComponent("TMP_InputField") != null)
                    return true;
            }

            return false;
        }

        private static bool IsUiToolkitTextInputFocused()
        {
            for (int i = textInputDocuments.Count - 1; i >= 0; i--)
            {
                UIDocument document = textInputDocuments[i];
                if (document == null)
                {
                    textInputDocuments.RemoveAt(i);
                    continue;
                }
                if (!document.isActiveAndEnabled || !document.gameObject.activeInHierarchy)
                    continue;

                VisualElement focused = document != null
                    && document.rootVisualElement != null
                    && document.rootVisualElement.panel != null
                    ? document.rootVisualElement.panel.focusController.focusedElement as VisualElement
                    : null;

                for (VisualElement element = focused; element != null; element = element.parent)
                {
                    if (IsTextInputElement(element))
                        return true;
                }
            }

            return false;
        }

        private static bool IsTextInputElement(VisualElement element)
        {
            if (element is TextField)
                return true;

            // Numeric and custom UI Toolkit fields inherit from the same
            // generic base class as TextField, so they receive the same guard.
            for (Type type = element.GetType(); type != null; type = type.BaseType)
            {
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition().Name == "TextInputBaseField`1")
                    return true;
            }

            return false;
        }

        public static void SetKey(RTSInputAction action, KeyCode key)
        {
            if (key == KeyCode.None) return;
            PlayerPrefs.SetInt(Prefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            foreach (RTSInputAction action in Enum.GetValues(typeof(RTSInputAction)))
            {
                PlayerPrefs.DeleteKey(Prefix + action);
            }
            PlayerPrefs.Save();
        }
    }
}
