using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppleRest
{
    // Separate failure controller: existing gameplay scripts and alignment remain unchanged.
    public sealed class StageDeadline : MonoBehaviour
    {
        [Min(0.1f)] [SerializeField] private float timeLimit = 45f;
        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private StumpInteraction interaction;
        [SerializeField] private SitClearSequence sitSequence;
        [SerializeField] private Light sunlight;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private TMP_Text penaltyLabel;
        [SerializeField] private Color sunsetColor = new Color(1f, 0.45f, 0.16f);
        [Range(0f, 1f)] [SerializeField] private float sunsetIntensityMultiplier = 0.35f;
        private float duration;
        private float initialIntensity;
        private bool stopped;
        private bool restarting;
        private bool initialized;
        private float penaltyUntil;
        private float nextPenaltyTime;
        private Vector2 penaltyOrigin;
        public float RemainingSeconds { get; private set; }
        public bool HasFailed { get; private set; }

        private void Start()
        {
            if (player == null || interaction == null || sitSequence == null || sunlight == null ||
                timerLabel == null || failurePanel == null || retryButton == null)
            {
                Debug.LogError("[AppleRest] Deadline references are missing. Run Add Timer and Sunset Failure.", this);
                enabled = false;
                return;
            }
            duration = Mathf.Max(0.1f, timeLimit);
            RemainingSeconds = duration;
            initialIntensity = sunlight.intensity;
            failurePanel.SetActive(false);
            retryButton.onClick.AddListener(Restart);
            if (penaltyLabel != null)
            {
                penaltyOrigin = penaltyLabel.rectTransform.anchoredPosition;
                penaltyLabel.gameObject.SetActive(false);
            }
            initialized = true;
            RefreshDisplay();
        }

        private void LateUpdate()
        {
            UpdatePenaltyDisplay();
            if (stopped) return;
            // Clear already reached this frame wins. Sitting alone does not stop the deadline.
            if (sitSequence.IsClear) { stopped = true; return; }
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
            RefreshDisplay();
            if (RemainingSeconds <= 0f) Fail();
        }

        // One shared cooldown also prevents overlapping bushes from charging on the same contact.
        public bool TryApplyThornPenalty(AppleRestPlayer visitor, float seconds)
        {
            if (!initialized || !isActiveAndEnabled || stopped || sitSequence.IsClear ||
                visitor != player || !player.isActiveAndEnabled || seconds <= 0f ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || Time.time < nextPenaltyTime) return false;
            nextPenaltyTime = Time.time + 1f;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - seconds);
            if (penaltyLabel != null)
            {
                penaltyLabel.text = "-" + seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "초";
                penaltyLabel.rectTransform.anchoredPosition = penaltyOrigin;
                penaltyLabel.color = new Color(1f, 0.15f, 0.12f, 1f);
                penaltyLabel.gameObject.SetActive(true);
                penaltyUntil = Time.unscaledTime + 1f;
            }
            RefreshDisplay();
            if (RemainingSeconds <= 0f) Fail();
            return true;
        }

        private void UpdatePenaltyDisplay()
        {
            if (penaltyLabel == null || !penaltyLabel.gameObject.activeSelf) return;
            float progress = Mathf.Clamp01(1f - (penaltyUntil - Time.unscaledTime));
            penaltyLabel.rectTransform.anchoredPosition = penaltyOrigin + Vector2.up * (30f * progress);
            Color color = penaltyLabel.color;
            color.a = 1f - progress;
            penaltyLabel.color = color;
            if (progress >= 1f) penaltyLabel.gameObject.SetActive(false);
        }

        private void RefreshDisplay()
        {
            timerLabel.text = "남은 시간 " + Mathf.CeilToInt(RemainingSeconds) + "초";
            float sunset = 1f - RemainingSeconds / duration;
            sunlight.color = Color.Lerp(Color.white, sunsetColor, sunset);
            sunlight.intensity = Mathf.Lerp(initialIntensity,
                initialIntensity * sunsetIntensityMultiplier, sunset);
        }

        private void Fail()
        {
            stopped = true;
            HasFailed = true;
            player.enabled = false;
            CharacterController capsule = player.GetComponent<CharacterController>();
            if (capsule != null) capsule.enabled = false;
            interaction.enabled = false; // Its existing OnDisable hides the prompt.
            sitSequence.enabled = false; // Its existing OnDisable cancels the pending Clear coroutine.
            Animator animator = sitSequence.GetComponent<Animator>();
            if (animator != null) animator.speed = 0f;
            failurePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            retryButton.Select();
        }

        public void Restart()
        {
            if (!HasFailed || restarting) return;
            string path = gameObject.scene.path;
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("[AppleRest] Enable GameStage in the Build Profiles scene list to restart.", this);
                return;
            }
            restarting = true;
            retryButton.interactable = false;
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(Restart);
        }
    }
}
