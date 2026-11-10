using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The player floating in space. There is no walking and no thrusters: once moving, you keep moving.
    /// The only way to change course is the grip ball (see GripBall). Looking around works as on the ground.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ZeroGPlayer : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSensitivity = 160f;
        [SerializeField] float pitchLimit = 88f;
        [Tooltip("Speed of a push off a nearby surface (Jump).")]
        [SerializeField] float pushOffSpeed = 3.5f;
        [SerializeField] float pushOffReach = 2.6f;

        public bool CanLook { get; set; } = true;
        public Rigidbody Body { get; private set; }
        public Transform View => cameraPivot;

        /// <summary>True when a rock or the hull is close enough to push off from.</summary>
        public bool NearSurface { get; private set; }

        /// <summary>Degrees per second the view rolls by itself (waking up dazed). Fades when set back to 0.</summary>
        public float Tumble { get; set; }

        float yaw, pitch, roll, tumbleNow;
        int settleFrames = 2;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
            Body.freezeRotation = true;
            yaw = transform.eulerAngles.y;
        }

        void OnEnable() => InputLock.ApplyCursor();

        void Update()
        {
            bool menuOpen = InputLock.IsLocked;
            if (!menuOpen && Cursor.lockState != CursorLockMode.Locked)
            {
                // In the editor Esc frees the mouse; a click captures it again.
                settleFrames = 2;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) InputLock.ApplyCursor();
            }

            tumbleNow = Mathf.MoveTowards(tumbleNow, Tumble, 12f * Time.deltaTime);
            roll += tumbleNow * Time.deltaTime;
            if (Mathf.Approximately(Tumble, 0f)) roll = Mathf.LerpAngle(roll, 0f, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
            yaw += tumbleNow * 0.4f * Time.deltaTime;

            if (CanLook && !menuOpen)
            {
                Vector2 mouse = Vector2.zero;
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    if (settleFrames > 0) settleFrames--;
                    else mouse = Vector2.ClampMagnitude(GameInput.LookMouse.ReadValue<Vector2>(), 120f);
                }
                Vector2 delta = mouse * mouseSensitivity + GameInput.LookStick.ReadValue<Vector2>() * (stickSensitivity * Time.deltaTime);
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            }
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);

            // With something solid within arm's reach you can kick off it in the direction you are looking.
            NearSurface = Physics.CheckSphere(transform.position, pushOffReach, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (CanLook && !menuOpen && NearSurface && GameInput.Jump.WasPressedThisFrame())
            {
                Body.linearVelocity = transform.forward * pushOffSpeed;
                AudioManager.Play(Sfx.Thud, 0.6f, 0.1f);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.5f) AudioManager.Play(Sfx.Thud, Mathf.Clamp01(speed / 6f), 0.15f);
        }
    }
}
