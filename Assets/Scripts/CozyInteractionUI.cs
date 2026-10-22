using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AppleRest
{
    public sealed class CozyInteractionUI : MonoBehaviour
    {
        [SerializeField] private AppleRestPlayer player;
        [SerializeField] private AppleCollection collection;
        [SerializeField] private StumpInteraction stump;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform pickupHint;
        [SerializeField] private RectTransform goalBubble;
        [SerializeField] private TMP_Text goalProgress;
        [SerializeField] private Texture2D cursorTexture;
        [Min(0.1f)] [SerializeField] private float pickupRadius = 1.35f;
        [Min(0.1f)] [SerializeField] private float maximumHeightDifference = 1.8f;
        private readonly List<ApplePickup> apples = new List<ApplePickup>();
        private Renderer[] characterRenderers;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private bool ownsCursor;

        private void Start()
        {
            if (player == null || collection == null || stump == null || canvasRect == null ||
                pickupHint == null || goalBubble == null || goalProgress == null)
            { Debug.LogError("[AppleRest] Run Add Cozy UI and E Apple Pickup to connect presentation references.", this); enabled = false; return; }
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                apples.AddRange(root.GetComponentsInChildren<ApplePickup>(true));
            characterRenderers = player.GetComponentsInChildren<Renderer>();
            if (viewCamera == null) viewCamera = Camera.main;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            ownsCursor = true;
            SetCursor();
        }

        private void SetCursor()
        {
            if (!Application.isFocused || !ownsCursor) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (cursorTexture != null) Cursor.SetCursor(cursorTexture, new Vector2(3, 3), CursorMode.Auto);
        }

        private void LateUpdate()
        {
            // StumpInteraction.Update has already processed E before collection changes this frame.
            // Thus the final apple cannot also trigger sitting with the same E press.
            bool active = player != null && player.isActiveAndEnabled && Application.isFocused && viewCamera != null;
            if (!active) { HidePanels(); return; }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            ApplePickup nearest = null;
            float best = pickupRadius * pickupRadius;
            if (!stump.CanInteract && !collection.IsComplete)
                foreach (ApplePickup apple in apples)
                {
                    if (apple == null || !apple.IsAvailable) continue;
                    Vector3 delta = apple.transform.position - player.transform.position;
                    if (Mathf.Abs(delta.y) > maximumHeightDifference) continue;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > best) continue;
                    best = delta.sqrMagnitude;
                    nearest = apple;
                }
            pickupHint.gameObject.SetActive(nearest != null);
            if (nearest != null)
            {
                FollowWorld(pickupHint, nearest.transform.position + Vector3.up * 0.6f);
                if (!overUI && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    nearest.TryPickup(player);
                    pickupHint.gameObject.SetActive(false);
                }
            }
            bool hover = false;
            if (!overUI && Mouse.current != null)
            {
                Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 150f, ~0, QueryTriggerInteraction.Ignore))
                    hover = hit.collider.GetComponentInParent<AppleRestPlayer>() == player;
            }
            goalBubble.gameObject.SetActive(hover);
            if (hover)
            {
                Bounds bounds = new Bounds(player.transform.position + Vector3.up, Vector3.one);
                bool found = false;
                foreach (Renderer renderer in characterRenderers)
                {
                    if (renderer == null || !renderer.enabled) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                goalProgress.text = "사과 " + collection.CollectedCount + " / " + collection.TotalCount;
                FollowWorld(goalBubble, new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z));
            }
        }

        private void FollowWorld(RectTransform panel, Vector3 world)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f) { panel.gameObject.SetActive(false); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            Rect area = canvasRect.rect;
            float half = panel.rect.width * 0.5f;
            local.x = Mathf.Clamp(local.x, area.xMin + half + 12, area.xMax - half - 12);
            local.y = Mathf.Clamp(local.y, area.yMin + 12, area.yMax - panel.rect.height - 12);
            panel.anchoredPosition = local;
        }

        private void HidePanels()
        {
            if (pickupHint != null) pickupHint.gameObject.SetActive(false);
            if (goalBubble != null) goalBubble.gameObject.SetActive(false);
        }
        private void OnApplicationFocus(bool focus) { if (focus) SetCursor(); }
        private void OnDisable()
        {
            HidePanels();
            if (!ownsCursor) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            ownsCursor = false;
        }
    }
}
