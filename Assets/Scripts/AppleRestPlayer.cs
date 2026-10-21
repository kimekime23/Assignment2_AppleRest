using UnityEngine;
using UnityEngine.InputSystem;

namespace AppleRest
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class AppleRestPlayer : MonoBehaviour
    {
        [Min(0f)] public float moveSpeed = 3f;
        [SerializeField] private float rotationSpeed = 540f;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Transform viewCamera;
        [SerializeField] private float stageHalfExtent = 14f;
        private CharacterController controller;
        private float verticalSpeed;
        private static readonly int Speed = Animator.StringToHash("Speed");

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (characterAnimator != null) characterAnimator.applyRootMotion = false;
        }

        private void Update()
        {
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            }
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 forward = viewCamera != null ? viewCamera.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            Vector3 before = transform.position;
            Vector3 target = before + direction * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
            target.x = Mathf.Clamp(target.x, -stageHalfExtent, stageHalfExtent);
            target.z = Mathf.Clamp(target.z, -stageHalfExtent, stageHalfExtent);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed - 20f * Time.deltaTime, -30f);
            Vector3 delta = target - before;
            delta.y = verticalSpeed * Time.deltaTime;
            controller.Move(delta);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            if (moved.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(moved), rotationSpeed * Time.deltaTime);
            if (characterAnimator != null)
                characterAnimator.SetFloat(Speed, moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
            // Recover safely if the floor is accidentally removed while editing.
            if (transform.position.y < -3f)
            {
                controller.enabled = false;
                transform.position = new Vector3(0f, 0.05f, 0f);
                controller.enabled = true;
                verticalSpeed = 0f;
            }
        }

        private void OnDisable()
        {
            verticalSpeed = 0f;
            if (characterAnimator != null) characterAnimator.SetFloat(Speed, 0f);
        }
    }
}
