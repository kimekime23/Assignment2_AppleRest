using UnityEngine;

namespace AppleRest
{
    public sealed class AppleRestCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 3.4f, -5.5f);
        [SerializeField] private float lookHeight = 1.1f;
        [SerializeField] private float smoothTime = 0.12f;
        private Vector3 velocity;

        private void Start()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position,
                target.position + offset, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
