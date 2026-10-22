using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppleRest
{
    // Presentation only: reads game state, never changes timer/collection/result conditions.
    [DefaultExecutionOrder(1000)]
    public sealed class StorybookUIEffects : MonoBehaviour
    {
        [SerializeField] private AppleCollection collection;
        [SerializeField] private StageDeadline deadline;
        [SerializeField] private SitClearSequence sitSequence;
        [SerializeField] private RectTransform counterPaper;
        [SerializeField] private RectTransform timerPaper;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text penaltyLabel;
        [SerializeField] private Button clearRetry;
        private Vector3 counterScale;
        private Vector2 timerPosition;
        private float pulseStarted = -100f;
        private int previousCount;
        private bool reloading;
        private static readonly Color Pencil = new Color32(0x42, 0x30, 0x24, 255);
        private static readonly Color RedInk = new Color32(0x96, 0x46, 0x3C, 255);
        private static readonly Color PenaltyInk = new Color32(0xBA, 0x3A, 0x30, 255);

        private void Awake()
        {
            if (counterPaper != null) counterScale = counterPaper.localScale;
            if (timerPaper != null) timerPosition = timerPaper.anchoredPosition;
        }
        private void OnEnable()
        {
            if (collection != null)
            {
                previousCount = collection.CollectedCount;
                collection.CountChanged += OnCountChanged;
            }
            if (clearRetry != null) clearRetry.onClick.AddListener(RetryClear);
        }
        private void OnCountChanged(int count, int total)
        {
            if (count > previousCount) pulseStarted = Time.unscaledTime;
            previousCount = count;
        }
        private void LateUpdate()
        {
            if (collection != null && counterLabel != null)
                counterLabel.text = collection.CollectedCount + " / " + collection.TotalCount;
            if (counterPaper != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - pulseStarted) / 0.2f);
                counterPaper.localScale = counterScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.075f);
            }
            if (deadline != null && timerLabel != null)
            {
                int seconds = Mathf.CeilToInt(deadline.RemainingSeconds);
                timerLabel.text = seconds + "초";
                bool low = deadline.RemainingSeconds <= 10f;
                timerLabel.color = low ? RedInk : Pencil;
                bool shake = low && !deadline.HasFailed && (sitSequence == null || !sitSequence.IsClear);
                if (timerPaper != null) timerPaper.anchoredPosition = timerPosition +
                    (shake ? new Vector2(Mathf.Sin(Time.unscaledTime * 19f) * 1.8f,
                        Mathf.Sin(Time.unscaledTime * 23f) * 0.8f) : Vector2.zero);
            }
            if (penaltyLabel != null && penaltyLabel.gameObject.activeInHierarchy)
            {
                // The existing deadline already owns its one-second upward movement and fading.
                string text = penaltyLabel.text.Replace('-', '\u2212');
                if (!text.EndsWith("!")) text += "!";
                if (penaltyLabel.text != text) penaltyLabel.text = text;
                Color ink = PenaltyInk; ink.a = penaltyLabel.color.a;
                penaltyLabel.color = ink;
            }
        }
        private void RetryClear()
        {
            if (reloading || sitSequence == null || !sitSequence.IsClear) return;
            string path = gameObject.scene.path;
            if (!Application.CanStreamedLevelBeLoaded(path))
            { Debug.LogError("Enable GameStage in the Build Profiles scene list for Retry.", this); return; }
            reloading = true;
            clearRetry.interactable = false;
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }
        private void OnDisable()
        {
            if (collection != null) collection.CountChanged -= OnCountChanged;
            if (clearRetry != null) clearRetry.onClick.RemoveListener(RetryClear);
            if (counterPaper != null) counterPaper.localScale = counterScale;
            if (timerPaper != null) timerPaper.anchoredPosition = timerPosition;
        }
    }
}
