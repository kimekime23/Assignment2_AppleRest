using UnityEngine;

namespace AppleRest
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class StorybookPaperEntrance : MonoBehaviour
    {
        private RectTransform paper;
        private CanvasGroup group;
        private Vector2 restPosition;
        private Quaternion restRotation;
        private float started;
        private void Awake()
        {
            paper = GetComponent<RectTransform>(); group = GetComponent<CanvasGroup>();
            restPosition = paper.anchoredPosition; restRotation = paper.localRotation;
        }
        private void OnEnable()
        {
            started = Time.unscaledTime;
            group.interactable = false;
            Animate(0f);
        }
        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / 0.25f);
            Animate(t);
            group.interactable = t >= 1f;
        }
        private void Animate(float t)
        {
            float remaining = Mathf.Pow(1f - t, 3f);
            paper.anchoredPosition = restPosition + Vector2.up * (42f * remaining);
            paper.localRotation = restRotation * Quaternion.Euler(0, 0, -4f * remaining);
            group.alpha = Mathf.Lerp(0.6f, 1f, t);
        }
        private void OnDisable()
        {
            if (paper == null) return;
            paper.anchoredPosition = restPosition; paper.localRotation = restRotation;
            group.alpha = 1f; group.interactable = true;
        }
    }
}
