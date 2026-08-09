using System;
using System.Collections.Generic;
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
        CommandMenu
    }

    /// <summary>
    /// Mapa de atalhos comum. Mantem os atalhos legados como padrao, mas permite
    /// que a UI futura remapeie teclas sem alterar dezenas de scripts.
    /// </summary>
    public static class RTSInputBindings
    {
        private const string Prefix = "rts.input.";
        private static readonly Dictionary<RTSInputAction, KeyCode> Defaults = new Dictionary<RTSInputAction, KeyCode>
        {
            { RTSInputAction.Construction, KeyCode.C },
            { RTSInputAction.Government, KeyCode.X },
            { RTSInputAction.Pier, KeyCode.V },
            { RTSInputAction.Barracks, KeyCode.B },
            { RTSInputAction.StrategicMap, KeyCode.M },
            { RTSInputAction.Follow, KeyCode.F },
            { RTSInputAction.CommandMenu, KeyCode.Alpha1 }
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
            KeyCode key = GetKey(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool GetKey(RTSInputAction action, bool held)
        {
            KeyCode key = GetKey(action);
            return key != KeyCode.None && (held ? Input.GetKey(key) : Input.GetKeyDown(key));
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
