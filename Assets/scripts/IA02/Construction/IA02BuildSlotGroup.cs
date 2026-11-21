using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [DisallowMultipleComponent]
    public sealed class IA02BuildSlotGroup : MonoBehaviour
    {
        [SerializeField] private string groupId;
        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId.Trim();
    }
}
