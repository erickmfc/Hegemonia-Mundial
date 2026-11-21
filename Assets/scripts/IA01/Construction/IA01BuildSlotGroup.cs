using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    [DisallowMultipleComponent]
    public sealed class IA01BuildSlotGroup : MonoBehaviour
    {
        [SerializeField] private string groupId;
        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId.Trim();
    }
}
