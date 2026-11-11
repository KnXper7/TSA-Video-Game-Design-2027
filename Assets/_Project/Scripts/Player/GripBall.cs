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
        // Stats are read live, so points spent in the middle of a spacewalk count straight away.
        static bool Has(Stat stat) => GameState.Instance != null && GameState.Instance.Check(stat, 3);
        public float Range => range * (Has(Stat.Navigation) ? 1.3f : 1f);
        float ReelAcceleration => reelAcceleration * (Has(Stat.Engineering) ? 1.35f : 1f);
        public int Throws { get; private set; }

        /// <summary>The ball gripped a surface.</summary>
        public event Action Gripped;
        /// <summary>The player reeled all the way in and grabbed on.</summary>
        public event Action Arrived;
        /// <summary>The ball skipped off a frost surface.</summary>
        public event Action Bounced;

        int bounces;
        float stuckFor;

        Vector3 velocity;
        float travelled, swing, slack;

        // Key poses of the throw, relative to where the hand rests (right, up, forward) and as turns of the hand.
        const float ThrowSeconds = 1.6f, WindUpEnd = 0.26f, WhipEnd = 0.34f, FollowEnd = 0.72f;
        static readonly Vector3 WindUpReach = new(0.02f, -0.03f, -0.1f), ThrowReach = new(-0.04f, -0.03f, 0.13f);     // a short push: the sleeve end must stay out of view
        static readonly Quaternion HoldTurn = Quaternion.Euler(-18f, -12f, 8f), WindUpTurn = Quaternion.Euler(-22f, -12f, 8f),
            ThrowTurn = Quaternion.Euler(-11f, -13f, 8f);      // palm tips forward as the ball rolls off the fingers
        // Where the ball sits in the open palm (set by hand in the editor).
        static readonly Vector3 BallInHand = new(-0.079f, 0.099f, 0.077f);
        Vector3 handReach;
        Quaternion handTurn = Quaternion.Euler(-18f, -12f, 8f);
        Light pulse;
        Vector3 baseScale;
        bool pullArmed;
        bool throwing;
        bool reeling;

        void Awake()
        {
            baseScale = ball.localScale;
            pulse = new GameObject("Pulse").AddComponent<Light>();
            pulse.transform.SetParent(ball, false);
            pulse.type = LightType.Point;
            pulse.color = new Color(0.4f, 0.85f, 1f);
            pulse.range = 9f;
            pulse.intensity = 0f;
            ball.gameObject.SetActive(false);
            if (tether != null) tether.enabled = false;
            if (hand != null) hand.gameObject.SetActive(false);
        }

        void OnDisable() => StopReel();

        /// <summary>Puts the ball and the hand away again.</summary>
        public void PutAway()
        {
            StopReel();
            ball.SetParent(null, true);
            ball.gameObject.SetActive(false);
            if (hand != null) hand.gameObject.SetActive(false);
            Current = State.Pocket;
            HUD.Instance?.SetPrompt(null);
        }

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

            // The throw copies the Seedance reference video (Docs/Targets/glove_throw_seedance.mp4), frame by frame:
            //   wind-up  - the fist, wrapped over the ball, is drawn back toward the helmet and cocked upward;
            //   whip     - in about a tenth of a second the arm snaps out, up and across toward the middle of the view,
            //              and the ball leaves the fingers;
            //   follow   - the arm stays at full stretch, fingers pointing after the ball, for most of a second;
            //   recover  - it eases back down to where it started.
            swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * (throwing ? 1f / ThrowSeconds : 2.6f));
            var reach = Vector3.zero;
            var turn = HoldTurn;
            if (throwing)
            {
                float p = 1f - swing;
                if (p < WindUpEnd)
                {
                    float a = Mathf.SmoothStep(0f, 1f, p / WindUpEnd);
                    reach = WindUpReach * a;
                    turn = Quaternion.Slerp(HoldTurn, WindUpTurn, a);
                }
                else if (p < WhipEnd)
                {
                    float a = (p - WindUpEnd) / (WhipEnd - WindUpEnd);
                    a = 1f - (1f - a) * (1f - a);                       // fastest at the start of the snap
                    reach = Vector3.Lerp(WindUpReach, ThrowReach, a) + Vector3.up * (Mathf.Sin(a * Mathf.PI) * 0.015f);
                    turn = Quaternion.Slerp(WindUpTurn, ThrowTurn, a);
                    if (a > 0.55f && Current == State.Held) Release();
                }
                else if (p < FollowEnd)
                {
                    float settle = Mathf.Exp(-(p - WhipEnd) * 18f) * 0.03f;       // a tiny overshoot that dies away
                    reach = ThrowReach + new Vector3(0f, 0f, settle);
                    turn = ThrowTurn;
                    if (Current == State.Held) Release();
                }
                else
                {
                    float a = Mathf.SmoothStep(0f, 1f, (p - FollowEnd) / (1f - FollowEnd));
                    reach = Vector3.Lerp(ThrowReach, Vector3.zero, a);
                    turn = Quaternion.Slerp(ThrowTurn, HoldTurn, a);
                }
                if (swing <= 0f) throwing = false;
            }
            else
            {
                // Taking the ball out raises the hand from below. While it is held, the hand floats a little.
                reach = new Vector3(0f, -0.35f * Mathf.SmoothStep(0f, 1f, swing), 0f)
                      + new Vector3(Mathf.Sin(Time.time * 0.9f) * 0.004f, Mathf.Sin(Time.time * 1.3f) * 0.006f, 0f);
                // Pulling yourself in: the arm reaches toward the ball and hauls.
                if (reeling) reach += new Vector3(-0.03f, -0.02f, 0.08f + Mathf.Sin(Time.time * 9f) * 0.015f);
            }
            handReach = Vector3.Lerp(handReach, reach, 1f - Mathf.Exp(-(throwing ? 60f : 10f) * Time.deltaTime));
            handTurn = Quaternion.Slerp(handTurn, turn, 1f - Mathf.Exp(-(throwing ? 60f : 10f) * Time.deltaTime));
            if (hand != null)
                hand.SetPositionAndRotation(view.transform.TransformPoint(holdOffset + new Vector3(0f, -0.06f, 0f) + handReach),
                    view.transform.rotation * handTurn);

            switch (Current)
            {
                case State.Held:
                    if (hand != null) ball.SetPositionAndRotation(hand.TransformPoint(BallInHand), hand.rotation);
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
                    if (player == null)
                    {
                        // On the ground there is nothing to pull yourself along: the ball just comes back.
                        stuckFor += Time.deltaTime;
                        if (stuckFor > 0.9f || (pullArmed && GameInput.Pull.IsPressed())) Return();
                        break;
                    }
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
            if (tether != null) tether.enabled = false;
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
            ball.position = hand != null ? hand.TransformPoint(BallInHand) : view.transform.position + direction * 0.7f;
            var aim = view.transform.position + direction * Mathf.Min(Range, 40f);
            // A forgiving aim: a throw that passes within half a metre of a rock still goes to it.
            if (Physics.SphereCast(view.transform.position, 0.5f, direction, out var target, Range, surfaces, QueryTriggerInteraction.Ignore)) aim = target.point;
            direction = (aim - ball.position).normalized;
            velocity = direction * throwSpeed;
            if (player != null)
            {
                velocity += player.Body.linearVelocity;
                player.Body.linearVelocity -= direction * recoil;      // every throw pushes you back a little
            }
            bounces = 0;
            stuckFor = 0f;
            travelled = 0f;
            Throws++;
            Current = State.Flying;
        }

        void Fly()
        {
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(ball.position, 0.08f, step.normalized, out var hit, step.magnitude, surfaces, QueryTriggerInteraction.Ignore))
            {
                var surface = hit.collider.GetComponentInParent<BallSurface>();
                var kind = surface != null ? surface.Type : BallSurface.Kind.Grip;
                if (kind == BallSurface.Kind.Frost && bounces < 4)
                {
                    // Frost: no grip. The ball skips off at the mirror angle and keeps going.
                    velocity = Vector3.Reflect(velocity, hit.normal);
                    ball.position = hit.point + hit.normal * 0.12f;
                    bounces++;
                    travelled *= 0.5f;
                    AudioManager.Play(Sfx.Ricochet, 0.8f, 0.1f);
                    Bounced?.Invoke();
                    return;
                }
                if (kind == BallSurface.Kind.Loose || kind == BallSurface.Kind.Target)
                {
                    // Nothing to hold on to: the hit does its work and the ball comes back.
                    surface.Strike(velocity, hit.point);
                    AudioManager.Play(Sfx.Clank, 0.8f, 0.1f);
                    if (impactDust != null)
                    {
                        impactDust.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
                        impactDust.Play();
                    }
                    Return();
                    return;
                }
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
            if (travelled > Range)
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
            v = along + sideways * Mathf.Exp(-1.8f * Time.deltaTime) + direction * (ReelAcceleration * Time.deltaTime);
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
                    bool target = Physics.SphereCast(view.transform.position, 0.5f, view.transform.forward, out var hit, Range, surfaces, QueryTriggerInteraction.Ignore);
                    string push = player != null && player.NearSurface ? "\n[Space] Push off" : "";
                    string bonus = Has(Stat.Navigation) ? "  •  NAVIGATION 3: +30% range" : "";
                    hud.SetPrompt((target ? $"[Left mouse] Throw  •  {hit.distance:0} m{bonus}" : "Out of range: aim at something closer") + push);
                    break;
                case State.Stuck when player == null:
                    hud.SetPrompt(null);
                    break;
                case State.Stuck:
                    hud.SetPrompt("Hold [Left mouse] to pull yourself in   •   [R] recall" + (Has(Stat.Engineering) ? "\nENGINEERING 3: faster pull" : ""));
                    break;
                default:
                    hud.SetPrompt(null);
                    break;
            }
        }
    }
}
