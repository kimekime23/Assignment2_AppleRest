using UnityEngine;

namespace AppleRest
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class ApplePickup : MonoBehaviour
    {
        [SerializeField] private AppleCollection collection;
        private bool collected;

        private void OnTriggerEnter(Collider other)
        {
            if (collected || collection == null) return;
            AppleRestPlayer player = other.GetComponentInParent<AppleRestPlayer>();
            if (player == null) return;
            collected = true;
            if (!collection.TryCollect(this, player))
            {
                collected = false;
                return;
            }
            // Disable immediately; Destroy is deferred until the end of the frame.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
