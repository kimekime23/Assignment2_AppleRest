using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace AppleRest
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class StumpInteraction : MonoBehaviour
    {
        public const string LockedPrompt = "사과를 모두 모아야 쉴 수 있어요";
        public const string ReadyPrompt = "E - 앉기";

        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private AppleCollection collection;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private UnityEvent onSitRequested = new UnityEvent();
        private readonly HashSet<Collider> playerContacts = new HashSet<Collider>();

        public bool HasTriggered { get; private set; }
        public bool IsPlayerInRange => playerContacts.Count > 0;
        public bool CanInteract => isActiveAndEnabled && !HasTriggered && IsPlayerInRange &&
            player != null && player.isActiveAndEnabled && collection != null && collection.IsComplete;
        public event Action SitRequested;

        private void OnEnable()
        {
            playerContacts.Clear();
            SetPrompt(false, "");
        }

        private void OnTriggerEnter(Collider other) => TrackPlayer(other);
        private void OnTriggerStay(Collider other) => TrackPlayer(other);
        private void OnTriggerExit(Collider other) => playerContacts.Remove(other);

        private void TrackPlayer(Collider other)
        {
            if (isActiveAndEnabled && player != null && other.GetComponentInParent<AppleRestPlayer>() == player)
                playerContacts.Add(other);
        }

        private void Update()
        {
            // Exit callbacks are not guaranteed when a collider is disabled/destroyed.
            playerContacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            bool show = !HasTriggered && IsPlayerInRange && player != null && player.isActiveAndEnabled;
            SetPrompt(show, collection != null && collection.IsComplete ? ReadyPrompt : LockedPrompt);
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null || !keyboard.eKey.wasPressedThisFrame || !CanInteract) return;

            // Set the latch before notifying listeners so re-entry cannot trigger twice.
            HasTriggered = true;
            SetPrompt(false, "");
            Debug.Log("Sit interaction triggered", this);
            SitRequested?.Invoke();
            onSitRequested.Invoke();
        }

        private void OnDisable()
        {
            playerContacts.Clear();
            SetPrompt(false, "");
        }

        private void SetPrompt(bool visible, string message)
        {
            if (promptLabel != null && visible && promptLabel.text != message) promptLabel.text = message;
            if (promptPanel != null && promptPanel.activeSelf != visible) promptPanel.SetActive(visible);
        }
    }
}
