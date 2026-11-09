using UnityEngine;

namespace TSAGame
{
    /// <summary>Looks for an Interactable in front of the camera, shows its prompt and handles press or hold.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float range = 2.8f;
        [SerializeField] LayerMask mask = ~0;

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

            if (Dialogue.IsPlaying || hud.IsChoosing)
            {
                hud.SetPrompt(null);
                held = 0f;
                return;
            }

            var target = FindTarget();
            if (target != current) held = 0f;
            current = target;

            if (current == null)
            {
                hud.SetPrompt(null);
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
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Ignore)) return null;
            var interactable = hit.collider.GetComponentInParent<Interactable>();
            return interactable != null && interactable.IsAvailable ? interactable : null;
        }
    }
}
