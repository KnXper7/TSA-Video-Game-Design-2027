using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// First-person walking and looking with keyboard/mouse or gamepad.
    /// Turn off CanMove for seated sections (the cockpit). Menus and panels block input through InputLock.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -18f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSensitivity = 160f;
        [SerializeField] float pitchLimit = 80f;
        [Tooltip("Limits how far the player can turn when seated (degrees either side). 0 = no limit.")]
        [SerializeField] float seatedYawLimit = 0f;

        [field: SerializeField] public bool CanMove { get; set; } = true;
        [field: SerializeField] public bool CanLook { get; set; } = true;

        CharacterController controller;
        float pitch;
        float yaw;
        float startYaw;
        float verticalVelocity;
        float stride, bob, pivotHeight = float.NaN;
        Vector3 smoothed;
        int settleFrames = 2;

        const float MaxMouseDeltaPerFrame = 120f;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = startYaw = transform.eulerAngles.y;
        }

        void OnEnable() => InputLock.ApplyCursor();

        void Update()
        {
            bool menuOpen = InputLock.IsLocked;
            RecoverCursor(menuOpen);
            if (CanLook && !menuOpen) Look();
            Move(CanMove && !menuOpen);
        }

        /// <summary>
        /// In the editor, Esc frees the mouse. Clicking back into the game locks it again. The first frames after
        /// locking are skipped because the mouse often reports one huge jump as it is captured.
        /// </summary>
        void RecoverCursor(bool menuOpen)
        {
            if (menuOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                settleFrames = 2;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) InputLock.ApplyCursor();
            }
        }

        void Look()
        {
            // Mouse look only while the cursor is captured, so moving a free cursor doesn't spin the camera.
            Vector2 mouse = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (settleFrames > 0) settleFrames--;
                else mouse = Vector2.ClampMagnitude(GameInput.LookMouse.ReadValue<Vector2>(), MaxMouseDeltaPerFrame);
            }
            Vector2 delta = mouse * mouseSensitivity
                          + GameInput.LookStick.ReadValue<Vector2>() * (stickSensitivity * Time.deltaTime);

            yaw += delta.x;
            if (seatedYawLimit > 0f) yaw = Mathf.Clamp(yaw, startYaw - seatedYawLimit, startYaw + seatedYawLimit);
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Move(bool allowed)
        {
            Vector3 horizontal = Vector3.zero;
            if (allowed)
            {
                Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
                float speed = GameInput.Sprint.IsPressed() ? sprintSpeed : walkSpeed;
                horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

                if (controller.isGrounded && GameInput.Jump.WasPressedThisFrame())
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Ease in and out of a walk, so starting and stopping don't feel like a switch.
            smoothed = Vector3.MoveTowards(smoothed, horizontal, (horizontal.sqrMagnitude > 0.01f ? 28f : 36f) * Time.deltaTime);
            horizontal = smoothed;
            // A small bob of the head in step with the stride.
            if (cameraPivot != null)
            {
                if (float.IsNaN(pivotHeight)) pivotHeight = cameraPivot.localPosition.y;
                float moving = Mathf.Clamp01(horizontal.magnitude / walkSpeed) * (controller.isGrounded ? 1f : 0f);
                bob = Mathf.Lerp(bob, moving, 1f - Mathf.Exp(-8f * Time.deltaTime));
                var local = cameraPivot.localPosition;
                local.y = pivotHeight + Mathf.Sin(stride / 2.2f * Mathf.PI * 2f) * 0.035f * bob;
                cameraPivot.localPosition = local;
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            if (controller.enabled)
                controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

            // Footsteps: one per stride while walking on the ground.
            if (controller.isGrounded && horizontal.sqrMagnitude > 1f)
            {
                stride += horizontal.magnitude * Time.deltaTime;
                if (stride > 2.2f)
                {
                    stride = 0f;
                    AudioManager.Play(Sfx.Footstep, 0.5f, 0.15f);
                }
            }
        }
    }
}
