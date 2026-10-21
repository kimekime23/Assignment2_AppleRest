using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppleRest
{
    public sealed class AppleCollection : MonoBehaviour
    {
        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private ApplePickup[] apples = new ApplePickup[0];
        private readonly HashSet<ApplePickup> collectedApples = new HashSet<ApplePickup>();

        public int CollectedCount => collectedApples.Count;
        public int TotalCount => apples.Length;
        public bool IsComplete => TotalCount > 0 && CollectedCount == TotalCount;
        public event Action<int, int> CountChanged;

        internal bool TryCollect(ApplePickup apple, AppleRestPlayer collector)
        {
            if (apple == null || player == null || collector != player || IsComplete) return false;
            if (Array.IndexOf(apples, apple) < 0 || !collectedApples.Add(apple)) return false;
            CountChanged?.Invoke(CollectedCount, TotalCount);
            return true;
        }
    }
}
