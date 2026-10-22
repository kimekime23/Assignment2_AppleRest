using System.Collections.Generic;
using UnityEngine;

namespace AppleRest
{
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public sealed class ThornBush : MonoBehaviour
    {
        [Min(0f)] [SerializeField] private float penaltySeconds = 3f;
        [SerializeField] private Material thornMaterial;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private StageDeadline deadline;
        private float nextContactTime;

        private void Awake()
        {
            // Also keeps the appearance correct if the prefab was placed before running the setup menu.
            if (thornMaterial != null)
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = thornMaterial;
                    renderer.sharedMaterials = materials;
                }
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                deadline = root.GetComponentInChildren<StageDeadline>();
                if (deadline != null) break;
            }
            if (deadline == null) Debug.LogWarning("[AppleRest] ThornBush needs a StageDeadline in this scene.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            AppleRestPlayer player = other.GetComponentInParent<AppleRestPlayer>();
            if (player == null || !player.isActiveAndEnabled) return;
            contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            bool firstContact = contacts.Count == 0;
            if (!contacts.Add(other) || !firstContact || Time.time < nextContactTime) return;
            if (deadline != null && deadline.TryApplyThornPenalty(player, penaltySeconds))
                nextContactTime = Time.time + 1f;
        }

        // No OnTriggerStay: remaining inside never repeatedly subtracts time.
        private void OnTriggerExit(Collider other) => contacts.Remove(other);
        private void OnDisable() => contacts.Clear();
    }
}
