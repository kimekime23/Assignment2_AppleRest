using UnityEngine;

namespace AppleRest
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class ApplePickup : MonoBehaviour
    {
        [SerializeField] private AppleCollection collection;
        private bool collected;
        public bool IsAvailable => isActiveAndEnabled && !collected && collection != null && !collection.IsComplete;

        // Collection is requested by the nearest-apple E interaction, never by touching the trigger.
        public bool TryPickup(AppleRestPlayer player)
        {
            if (!IsAvailable || player == null || !player.isActiveAndEnabled) return false;
            collected = true;
            if (!collection.TryCollect(this, player))
            {
                collected = false;
                return false;
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
