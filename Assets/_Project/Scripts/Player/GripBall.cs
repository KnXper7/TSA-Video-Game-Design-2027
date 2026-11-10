using System;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The lumpy metal ball for moving in zero gravity. Throw it at a surface and its bumps grip; hold Pull to reel
    /// yourself along the line to it; Recall brings it back. Throwing also nudges you the opposite way.
    /// (Next to a rock you can also push off it with Jump: see ZeroGPlayer.)
    /// Navigation 3+ throws further; Engineering 3+ reels faster.
    /// </summary>
    public class GripBall : MonoBehaviour
    {
        public enum State { Pocket, Held, Flying, Stuck, Returning }

        [SerializeField] ZeroGPlayer player;
        [SerializeField] Camera view;
        [SerializeField] Transform ball;
        [SerializeField] LineRenderer tether;
        [SerializeField] Vector3 holdOffset = new(0.3f, -0.22f, 0.62f);
        [SerializeField] float throwSpeed = 34f;
        [SerializeField] float range = 46f;
        [SerializeField] float recoil = 0.7f;
        [SerializeField] float reelAcceleration = 7f;
        [SerializeField] float maxReelSpeed = 10f;
        [SerializeField] float grabDistance = 2.2f;
        [SerializeField] float returnSpeed = 50f;
        [SerializeField] LayerMask surfaces = ~(1 << 2);

        public State Current { get; private set; } = State.Pocket;
        public float Range => range;
        public int Throws { get; private set; }

        /// <summary>The ball gripped a surface.</summary>
        public event Action Gripped;
        /// <summary>The player reeled all the way in and grabbed on.</summary>
        public event Action Arrived;

        Vector3 velocity;
        float travelled;
        bool reeling;

        void Awake()
        {
            var state = GameState.Instance;
            if (state != null && state.Check(Stat.Navigation, 3)) range *= 1.3f;
            if (state != null && state.Check(Stat.Engineering, 3)) reelAcceleration *= 1.35f;
            ball.gameObject.SetActive(false);
            tether.enabled = false;
        }

        void OnDisable() => StopReel();

        public void TakeOut()
        {
            if (Current != State.Pocket) return;
            Current = State.Held;
            ball.gameObject.SetActive(true);
            AudioManager.Play(Sfx.Pickup, 0.7f);
        }

        Vector3 HoldPoint => view.transform.TransformPoint(holdOffset);

        void Update()
        {
            if (Current == State.Pocket) return;
            bool canAct = !InputLock.IsLocked && Cursor.lockState == CursorLockMode.Locked;

            switch (Current)
            {
                case State.Held:
                    ball.SetPositionAndRotation(HoldPoint, view.transform.rotation * Quaternion.Euler(Time.time * 25f, Time.time * 40f, 0f));
                    if (canAct && GameInput.Throw.WasPressedThisFrame()) Throw();
                    break;

                case State.Flying:
                    Fly();
                    if (canAct && GameInput.Recall.WasPressedThisFrame()) Return();
                    break;

                case State.Stuck:
                    Reel(canAct && GameInput.Pull.IsPressed());
                    if (canAct && GameInput.Recall.WasPressedThisFrame()) Return();
                    break;

                case State.Returning:
                    ball.position = Vector3.MoveTowards(ball.position, HoldPoint, returnSpeed * Time.deltaTime);
                    if ((ball.position - HoldPoint).sqrMagnitude < 0.04f) Current = State.Held;
                    break;
            }

            tether.enabled = Current != State.Held;
            if (tether.enabled)
            {
                tether.SetPosition(0, view.transform.TransformPoint(new Vector3(0.22f, -0.3f, 0.35f)));
                tether.SetPosition(1, ball.position);
            }
            ShowPrompt();
        }

        void Throw()
        {
            var direction = view.transform.forward;
            ball.SetParent(null, true);
            ball.position = view.transform.position + direction * 0.7f;
            velocity = direction * throwSpeed + player.Body.linearVelocity;
            player.Body.linearVelocity -= direction * recoil;      // every throw pushes you back a little
            travelled = 0f;
            Throws++;
            Current = State.Flying;
            AudioManager.Play(Sfx.Throw, 0.9f, 0.1f);
        }

        void Fly()
        {
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(ball.position, 0.08f, step.normalized, out var hit, step.magnitude, surfaces, QueryTriggerInteraction.Ignore))
            {
                ball.position = hit.point + hit.normal * 0.05f;
                ball.SetParent(hit.transform, true);
                Current = State.Stuck;
                AudioManager.Play(Sfx.Clank, 0.8f, 0.1f);
                Gripped?.Invoke();
                return;
            }
            ball.position += step;
            ball.Rotate(400f * Time.deltaTime, 250f * Time.deltaTime, 0f);
            travelled += step.magnitude;
            if (travelled > range)
            {
                HUD.Instance?.ShowToast("Nothing to grip. The ball comes back.");
                Return();
            }
        }

        void Reel(bool pulling)
        {
            if (!pulling)
            {
                StopReel();
                return;
            }
            if (!reeling)
            {
                reeling = true;
                AudioManager.StartLoop("reel", Sfx.Reel, 0.5f, 0.1f);
            }

            var body = player.Body;
            var toBall = ball.position - body.position;
            float distance = toBall.magnitude;
            var direction = toBall / Mathf.Max(distance, 0.001f);

            // Speed up along the line, and bleed off sideways drift so you home in on the ball.
            var v = body.linearVelocity;
            var along = Vector3.Dot(v, direction) * direction;
            var sideways = v - along;
            v = along + sideways * Mathf.Exp(-1.8f * Time.deltaTime) + direction * (reelAcceleration * Time.deltaTime);
            body.linearVelocity = Vector3.ClampMagnitude(v, maxReelSpeed);

            if (distance < grabDistance)
            {
                body.linearVelocity = Vector3.zero;            // you grab the surface and stop
                StopReel();
                AudioManager.Play(Sfx.Interact, 0.6f);
                Return();
                Arrived?.Invoke();
            }
        }

        void StopReel()
        {
            if (!reeling) return;
            reeling = false;
            AudioManager.StopLoop("reel", 0.15f);
        }

        void Return()
        {
            StopReel();
            ball.SetParent(null, true);
            Current = State.Returning;
        }

        void ShowPrompt()
        {
            var hud = HUD.Instance;
            if (hud == null || Dialogue.IsPlaying) return;
            switch (Current)
            {
                case State.Held:
                    bool target = Physics.SphereCast(view.transform.position, 0.08f, view.transform.forward, out var hit, range, surfaces, QueryTriggerInteraction.Ignore);
                    string push = player.NearSurface ? "\n[Space] Push off" : "";
                    hud.SetPrompt((target ? $"[Left mouse] Throw  •  {hit.distance:0} m" : "Out of range: aim at something closer") + push);
                    break;
                case State.Stuck:
                    hud.SetPrompt("Hold [Right mouse] to reel in   •   [R] recall");
                    break;
                default:
                    hud.SetPrompt(null);
                    break;
            }
        }
    }
}
