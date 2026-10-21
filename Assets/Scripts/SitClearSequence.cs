using System.Collections;
using UnityEngine;

namespace AppleRest
{
    // SitAnchor is the final Player root pose, never a bone target.
    [RequireComponent(typeof(Animator))]

    public sealed class SitClearSequence : MonoBehaviour
    {
        [SerializeField] private StumpInteraction interaction;
        [SerializeField] private AppleCollection collection;
        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private CharacterController characterController;
        [Tooltip("Final Player ROOT position and rotation. Adjust RestStump/SitAnchor in Edit Mode.")]
        [SerializeField] private Transform sitAnchor;
        [SerializeField] private GameObject clearPanel;
        [SerializeField] private GameObject counterCanvas;
        [SerializeField] private GameObject promptCanvas;
        [Min(0f)] [SerializeField] private float clearDelay = 0.6f;
        private Animator animator;
        private bool started;
        private bool seating;
        private static readonly int SitState = Animator.StringToHash("Base Layer.Sit");
        public bool IsSitting => seating;
        public bool IsClear { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            if (clearPanel != null) clearPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (interaction != null) interaction.SitRequested += Begin;
        }

        private void OnDisable()
        {
            if (interaction != null) interaction.SitRequested -= Begin;
            StopAllCoroutines();
        }

        private void Begin()
        {
            // Recheck collection and approved interaction independently of the event publisher.
            if (started || collection == null || !collection.IsComplete || interaction == null ||
                !interaction.HasTriggered || !interaction.IsPlayerInRange) return;
            if (player == null || sitAnchor == null || characterController == null ||
                clearPanel == null || !animator.isHuman || !animator.HasState(0, SitState))
            {
                Debug.LogError("[AppleRest] SitAnchor, sequence references, Humanoid Avatar or Sit state are missing. Run AppleRest/Use Manual SitAnchor.", this);
                return;
            }
            started = true;
            player.enabled = false;
            characterController.enabled = false;
            animator.SetFloat("Speed", 0f);
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            // One placement only. No hips offsets, approach offsets, ground snapping or IK corrections.
            player.transform.SetPositionAndRotation(sitAnchor.position, sitAnchor.rotation);
            seating = true;
            animator.CrossFadeInFixedTime(SitState, 0.15f, 0, 0f);
            float elapsed = 0f;
            bool finished = false;
            while (elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == SitState)
                {
                    if (state.normalizedTime >= 1f && !animator.IsInTransition(0)) { finished = true; break; }
                }
                yield return null;
            }
            if (!finished)
            {
                Debug.LogError("[AppleRest] Sit did not finish. Clear was not triggered; inspect Animator.", this);
                yield break;
            }
            yield return new WaitForSeconds(clearDelay);
            if (!collection.IsComplete) yield break;
            IsClear = true;
            if (counterCanvas != null) counterCanvas.SetActive(false);
            if (promptCanvas != null) promptCanvas.SetActive(false);
            clearPanel.SetActive(true);
        }

        private void OnDrawGizmos()
        {
            if (sitAnchor == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(sitAnchor.position, 0.12f);
            Vector3 tip = sitAnchor.position + sitAnchor.forward * 0.6f;
            Gizmos.DrawLine(sitAnchor.position, tip);
            Gizmos.DrawLine(tip, tip - sitAnchor.forward * 0.15f + sitAnchor.right * 0.1f);
            Gizmos.DrawLine(tip, tip - sitAnchor.forward * 0.15f - sitAnchor.right * 0.1f);
        }
    }
}