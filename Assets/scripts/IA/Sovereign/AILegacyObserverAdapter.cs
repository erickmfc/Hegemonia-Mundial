using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hegemonia.AI.Sovereign
{
    public sealed class AILegacyObserverAdapter
    {
        private readonly int _teamId;
        private readonly string _ownerKey;
        private readonly List<MonoBehaviour> _disabled = new List<MonoBehaviour>(16);

        public AILegacyObserverAdapter(int teamId, string ownerKey)
        {
            _teamId = teamId;
            _ownerKey = ownerKey;
        }

        public void Apply(bool claimActive)
        {
            if (!claimActive)
            {
                Restore();
                return;
            }

            if (_disabled.Count > 0)
            {
                return;
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                if (!BelongsToTeam(behaviour, _teamId))
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                if (typeName.Contains("Hegemonia.AI.Sovereign.AISovereignController"))
                {
                    continue;
                }

                if (!IsLegacyAiType(typeName))
                {
                    continue;
                }

                ApplyObserverIfSupported(behaviour);
                behaviour.enabled = false;
                _disabled.Add(behaviour);
            }
        }

        public void Restore()
        {
            for (int i = 0; i < _disabled.Count; i++)
            {
                MonoBehaviour behaviour = _disabled[i];
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }
            _disabled.Clear();
        }

        private void ApplyObserverIfSupported(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            System.Type type = behaviour.GetType();
            FieldInfo integrationMode = type.GetField("IntegrationMode", BindingFlags.Public | BindingFlags.Instance);
            if (integrationMode != null && integrationMode.FieldType.IsEnum)
            {
                if (TryResolveObserverMode(integrationMode.FieldType, out object shadow))
                {
                    integrationMode.SetValue(behaviour, shadow);
                }
            }

            PropertyInfo integrationProperty = type.GetProperty("IntegrationMode", BindingFlags.Public | BindingFlags.Instance);
            if (integrationProperty != null && integrationProperty.CanWrite && integrationProperty.PropertyType.IsEnum)
            {
                if (TryResolveObserverMode(integrationProperty.PropertyType, out object shadow))
                {
                    integrationProperty.SetValue(behaviour, shadow, null);
                }
            }
        }

        private static bool TryResolveObserverMode(System.Type enumType, out object shadow)
        {
            shadow = null;
            if (enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            string[] names = System.Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "ShadowReadOnly", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(names[i], "Observer", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(names[i], "ObserveOnly", System.StringComparison.OrdinalIgnoreCase))
                {
                    shadow = System.Enum.Parse(enumType, names[i]);
                    return true;
                }
            }

            return false;
        }

        private static bool IsLegacyAiType(string typeName)
        {
            return typeName.Contains("IA_Suprema")
                   || typeName.Contains("IA_Dominadora")
                   || typeName.Contains("IA_Comandante")
                   || typeName.Contains("Hegemonia.AI.BrainMaster.IA_BrainMaster")
                   || typeName.Contains("Hegemonia.AI.Master.IA_MasterController");
        }

        private static bool BelongsToTeam(Component behaviour, int teamId)
        {
            if (behaviour == null)
            {
                return false;
            }

            IdentidadeUnidade unidade = behaviour.GetComponent<IdentidadeUnidade>();
            if (unidade == null)
            {
                unidade = behaviour.GetComponentInParent<IdentidadeUnidade>();
            }
            if (unidade != null)
            {
                return unidade.teamID == teamId;
            }

            FieldInfo teamField = behaviour.GetType().GetField("teamID", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("TeamID", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("TeamId", BindingFlags.Public | BindingFlags.Instance);
            if (teamField != null && teamField.FieldType == typeof(int))
            {
                return (int)teamField.GetValue(behaviour) == teamId;
            }

            PropertyInfo teamProperty = behaviour.GetType().GetProperty("TeamId", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetProperty("TeamID", BindingFlags.Public | BindingFlags.Instance);
            if (teamProperty != null && teamProperty.PropertyType == typeof(int) && teamProperty.CanRead)
            {
                return (int)teamProperty.GetValue(behaviour, null) == teamId;
            }

            return false;
        }
    }
}
