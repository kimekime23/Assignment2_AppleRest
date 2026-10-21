using TMPro;
using UnityEngine;

namespace AppleRest
{
    public sealed class AppleCounterUI : MonoBehaviour
    {
        [SerializeField] private AppleCollection collection;
        [SerializeField] private TMP_Text label;

        private void OnEnable()
        {
            if (collection == null || label == null) return;
            collection.CountChanged += Refresh;
            Refresh(collection.CollectedCount, collection.TotalCount);
        }

        private void OnDisable()
        {
            if (collection != null) collection.CountChanged -= Refresh;
        }

        private void Refresh(int collected, int total)
        {
            label.text = $"사과 {collected} / {total}";
        }
    }
}
