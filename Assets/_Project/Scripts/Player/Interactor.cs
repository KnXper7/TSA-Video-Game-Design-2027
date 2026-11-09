using UnityEngine;

namespace TSAGame
{
    /// <summary>Looks for an Interactable in front of the camera, shows its prompt and handles press or hold.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float range = 2.8f;
        [SerializeField] LayerMask mask = ~0;

        readonly RaycastHit[] hits = new RaycastHit[8];
        Interactable current;
        float held;

        void OnDisable()
        {
            current = null;
            held = 0f;
            if (HUD.Instance != null) HUD.Instance.SetPrompt(null);
        }

        void Update()
        {
            var hud = HUD.Instance;
            if (hud == null) return;

            if (InputLock.IsLocked)
            {
                hud.SetPrompt(null);
                held = 0f;
                return;
            }

            var target = FindTarget();
            if (target != current) held = 0f;
            current = target;

            if (current == null || current.IsUsedUp)
            {
                hud.SetPrompt(null);
                return;
            }

            // Say why E does nothing, instead of hiding the prompt.
            if (!current.isActiveAndEnabled)
            {
                hud.SetPrompt($"{current.Prompt}  (not yet)");
                return;
            }
            if (Dialogue.IsPlaying)
            {
                held = 0f;
                hud.SetPrompt($"{current.Prompt}  (after the conversation, [E] skips lines)");
                return;
            }

            if (current.HoldDuration <= 0f)
            {
                hud.SetPrompt($"[E] {current.Prompt}");
                if (GameInput.Interact.WasPressedThisFrame()) current.Interact();
                return;
            }

            held = GameInput.Interact.IsPressed() ? held + Time.deltaTime : 0f;
            hud.SetPrompt(held > 0f
                ? $"{current.Prompt}... {Mathf.RoundToInt(held / current.HoldDuration * 100f)}%"
                : $"Hold [E] {current.Prompt}");

            if (held >= current.HoldDuration)
            {
                held = 0f;
                current.Interact();
            }
        }

        Interactable FindTarget()
        {
            // The camera sits inside the top of the player's own capsule, so looking down used to hit the player's
            // body first and nothing could be selected. Take the nearest hit that isn't part of the player.
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, range, mask, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (nearest == null || hits[i].distance < nearest.Value.distance) nearest = hits[i];
            }
            return nearest?.collider.GetComponentInParent<Interactable>();
        }
    }
}
