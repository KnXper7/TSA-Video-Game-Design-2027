using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// First-person walking and looking with keyboard/mouse or gamepad.
    /// Turn off CanMove for seated sections (the cockpit); turn off CanLook while a UI panel is open.
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

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = startYaw = transform.eulerAngles.y;
        }

        void OnEnable() => LockCursor(true);

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            bool uiBusy = HUD.Instance != null && HUD.Instance.IsChoosing;
            if (CanLook && !uiBusy) Look();
            Move(CanMove && !uiBusy);
        }

        void Look()
        {
            Vector2 delta = GameInput.LookMouse.ReadValue<Vector2>() * mouseSensitivity
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

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            if (controller.enabled)
                controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
