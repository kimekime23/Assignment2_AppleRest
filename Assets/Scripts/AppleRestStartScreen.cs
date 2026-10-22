using UnityEngine;
using UnityEngine.UI;

namespace AppleRest
{
    [DefaultExecutionOrder(-200)]
    public sealed class AppleRestStartScreen : MonoBehaviour
    {
        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private GameObject screen;
        [SerializeField] private Button startButton;
        [SerializeField] private Canvas[] gameplayHUD;
        private bool[] previousHUDStates;
        private bool previousPlayerEnabled;
        private float previousTimeScale;
        private bool waiting;
        public bool HasStarted { get; private set; }

        private void Awake()
        {
            if (player == null || screen == null || startButton == null)
            {
                Debug.LogError("[AppleRest] Start screen references missing. Run Add Start Screen.", this);
                if (screen != null) screen.SetActive(false);
                enabled = false;
                return;
            }
            previousTimeScale = Time.timeScale;
            previousPlayerEnabled = player.enabled;
            waiting = true;
            Time.timeScale = 0f;
            player.enabled = false;
            previousHUDStates = new bool[gameplayHUD.Length];
            for (int i = 0; i < gameplayHUD.Length; i++)
            {
                if (gameplayHUD[i] == null) continue;
                previousHUDStates[i] = gameplayHUD[i].enabled;
                gameplayHUD[i].enabled = false; // Scripts can still initialize their labels.
            }
            screen.SetActive(true);
            startButton.onClick.AddListener(Begin);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (waiting) startButton.Select();
        }

        public void Begin()
        {
            if (!waiting || HasStarted) return;
            HasStarted = true;
            startButton.interactable = false;
            screen.SetActive(false);
            ReleasePause();
        }

        private void ReleasePause()
        {
            if (!waiting) return;
            waiting = false;
            Time.timeScale = previousTimeScale;
            if (player != null) player.enabled = previousPlayerEnabled;
            for (int i = 0; i < gameplayHUD.Length; i++)
                if (gameplayHUD[i] != null) gameplayHUD[i].enabled = previousHUDStates[i];
        }

        private void OnDisable()
        {
            if (waiting && screen != null) screen.SetActive(false);
            ReleasePause(); // Never leave the next scene or editor session paused.
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(Begin);
            ReleasePause();
        }
    }
}
