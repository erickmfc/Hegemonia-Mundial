using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Hegemonia.AI.Shared;

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

            MonoBehaviour[] behaviours = IA_UnitySearch.FindAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                if (!IA_SharedRuntimeSupport.BelongsToTeam(behaviour, _teamId))
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                if (typeName.Contains("Hegemonia.AI.Sovereign.AISovereignController"))
                {
                    continue;
                }

                if (!IA_SharedRuntimeSupport.IsLegacyAiType(typeName))
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
                if (IA_SharedRuntimeSupport.TryResolveObserverMode(integrationMode.FieldType, out object shadow))
                {
                    integrationMode.SetValue(behaviour, shadow);
                }
            }

            PropertyInfo integrationProperty = type.GetProperty("IntegrationMode", BindingFlags.Public | BindingFlags.Instance);
            if (integrationProperty != null && integrationProperty.CanWrite && integrationProperty.PropertyType.IsEnum)
            {
                if (IA_SharedRuntimeSupport.TryResolveObserverMode(integrationProperty.PropertyType, out object shadow))
                {
                    integrationProperty.SetValue(behaviour, shadow, null);
                }
            }
        }
    }
}
