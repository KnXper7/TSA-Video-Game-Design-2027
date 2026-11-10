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
        [Tooltip("The player's own hand. It holds the ball, and swings forward on a throw.")]
        [SerializeField] Transform hand;
        [Tooltip("Puff of dust and chips where the ball bites into a surface.")]
        [SerializeField] ParticleSystem impactDust;
        [SerializeField] Vector3 holdOffset = new(0.2f, -0.2f, 0.48f);
        [SerializeField] float throwSpeed = 48f;
        [SerializeField] float range = 85f;
        [SerializeField] float recoil = 0.7f;
        [SerializeField] float reelAcceleration = 7f;
        [SerializeField] float maxReelSpeed = 10f;
        [SerializeField] float grabDistance = 2.2f;
        [SerializeField] float returnSpeed = 70f;
        [SerializeField] LayerMask surfaces = ~(1 << 2);

        public State Current { get; private set; } = State.Pocket;
        public float Range => range;
        public int Throws { get; private set; }

        /// <summary>The ball gripped a surface.</summary>
        public event Action Gripped;
        /// <summary>The player reeled all the way in and grabbed on.</summary>
        public event Action Arrived;

        Vector3 velocity;
        float travelled, swing, slack;
        Light pulse;
        Vector3 baseScale;
        bool pullArmed;
        bool throwing;
        bool reeling;

        void Awake()
        {
            var state = GameState.Instance;
            if (state != null && state.Check(Stat.Navigation, 3)) range *= 1.3f;
            if (state != null && state.Check(Stat.Engineering, 3)) reelAcceleration *= 1.35f;
            baseScale = ball.localScale;
            pulse = new GameObject("Pulse").AddComponent<Light>();
            pulse.transform.SetParent(ball, false);
            pulse.type = LightType.Point;
            pulse.color = new Color(0.4f, 0.85f, 1f);
            pulse.range = 9f;
            pulse.intensity = 0f;
            ball.gameObject.SetActive(false);
            tether.enabled = false;
            if (hand != null) hand.gameObject.SetActive(false);
        }

        void OnDisable() => StopReel();

        public void TakeOut()
        {
            if (Current != State.Pocket) return;
            Current = State.Held;
            ball.gameObject.SetActive(true);
            if (hand != null) hand.gameObject.SetActive(true);
            swing = 1f;                                   // the hand comes up from the pocket
            AudioManager.Play(Sfx.Pickup, 0.7f);
        }

        Vector3 HoldPoint => view.transform.TransformPoint(holdOffset);

        void Update()
        {
            if (Current == State.Pocket) return;
            bool canAct = !InputLock.IsLocked && Cursor.lockState == CursorLockMode.Locked;

            // The hand rides at the hold point. Taking the ball out raises it from below; a throw draws the arm
            // back, drives it forward and out to full stretch (the ball leaves the glove on the way), then recovers.
            swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * (throwing ? 1.7f : 2.6f));
            var reach = Vector3.zero;
            float tilt = 0f;
            if (throwing)
            {
                float p = 1f - swing;
                // Timed from a reference video of a first-person throw: a short draw back, a fast push forward and
                // toward the middle of the view, the arm held out at full stretch for a beat, then it drops back.
                var back = new Vector3(0.05f, 0.06f, -0.16f);
                var out_ = new Vector3(-0.1f, 0.16f, 0.5f);
                if (p < 0.2f) { float a = Mathf.SmoothStep(0f, 1f, p / 0.2f); reach = back * a; tilt = 28f * a; }
                else if (p < 0.38f) { float a = (p - 0.2f) / 0.18f; reach = Vector3.Lerp(back, out_, a * a); tilt = Mathf.Lerp(28f, -40f, a); }
                else if (p < 0.62f) { reach = out_; tilt = -40f; }
                else { float a = Mathf.SmoothStep(0f, 1f, (p - 0.62f) / 0.38f); reach = Vector3.Lerp(out_, Vector3.zero, a); tilt = Mathf.Lerp(-40f, 0f, a); }
                if (p >= 0.34f && Current == State.Held) Release();
                if (swing <= 0f) throwing = false;
            }
            else reach = new Vector3(0f, -0.35f * Mathf.SmoothStep(0f, 1f, swing), 0f);
            if (hand != null)
                hand.SetPositionAndRotation(view.transform.TransformPoint(holdOffset + new Vector3(0f, -0.06f, 0f) + reach),
                    view.transform.rotation * Quaternion.Euler(-18f - tilt, -12f, 8f));

            switch (Current)
            {
                case State.Held:
                    if (hand != null) ball.SetPositionAndRotation(hand.TransformPoint(new Vector3(0.02f, -0.015f, 0.11f)), hand.rotation);
                    else ball.SetPositionAndRotation(HoldPoint, view.transform.rotation);
                    if (canAct && !throwing && GameInput.Throw.WasPressedThisFrame()) Throw();
                    break;

                case State.Flying:
                    Fly();
                    if (canAct && GameInput.Recall.WasPressedThisFrame()) Return();
                    break;

                case State.Stuck:
                    // The same button throws and pulls; it has to be let go once after the throw before it pulls.
                    if (!GameInput.Pull.IsPressed()) pullArmed = true;
                    Reel(canAct && pullArmed && GameInput.Pull.IsPressed());
                    if (canAct && GameInput.Recall.WasPressedThisFrame()) Return();
                    break;

                case State.Returning:
                    ball.position = Vector3.MoveTowards(ball.position, HoldPoint, returnSpeed * Time.deltaTime);
                    if ((ball.position - HoldPoint).sqrMagnitude < 0.04f)
                    {
                        Current = State.Held;
                        AudioManager.Play(Sfx.Thud, 0.35f, 0.2f);      // it smacks back into the glove
                    }
                    break;
            }

            // No visible line. Once the ball has a grip it pulses with light to say "pull me".
            tether.enabled = false;
            if (pulse != null)
                pulse.intensity = Current == State.Stuck && !reeling ? 2.5f + 2.5f * Mathf.Sin(Time.time * 7f) : Current == State.Stuck ? 5f : 0f;
            ball.localScale = baseScale * (Current == State.Stuck && !reeling ? 1f + 0.18f * Mathf.Max(0f, Mathf.Sin(Time.time * 7f)) : 1f);

            ShowPrompt();
        }

        /// <summary>Starts the throwing motion. The ball leaves the hand part-way through it (see Release).</summary>
        void Throw()
        {
            throwing = true;
            pullArmed = false;
            swing = 1f;
            AudioManager.Play(Sfx.Throw, 0.9f, 0.1f);
            if (hand == null) Release();
        }

        void Release()
        {
            var direction = view.transform.forward;
            ball.SetParent(null, true);
            // Leaves from the glove, but is aimed so it still flies to the middle of the screen.
            ball.position = hand != null ? hand.TransformPoint(new Vector3(0.02f, -0.015f, 0.11f)) : view.transform.position + direction * 0.7f;
            var aim = view.transform.position + direction * Mathf.Min(range, 40f);
            if (Physics.Raycast(view.transform.position, direction, out var target, range, surfaces, QueryTriggerInteraction.Ignore)) aim = target.point;
            direction = (aim - ball.position).normalized;
            velocity = direction * throwSpeed + player.Body.linearVelocity;
            player.Body.linearVelocity -= direction * recoil;      // every throw pushes you back a little
            travelled = 0f;
            Throws++;
            Current = State.Flying;
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
                if (impactDust != null)
                {
                    impactDust.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
                    impactDust.Play();
                }
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
                    hud.SetPrompt("Hold [Left mouse] to pull yourself in   •   [R] recall");
                    break;
                default:
                    hud.SetPrompt(null);
                    break;
            }
        }
    }
}
