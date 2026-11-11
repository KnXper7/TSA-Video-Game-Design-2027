using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// After the title: the rocket is hit by a rock as it leaves the portal (a short shot), and the pilot wakes
    /// up floating outside with no thrusters. The player takes out the grip ball and throws it from rock to rock
    /// to get back to the hatch before the suit's oxygen runs out.
    /// </summary>
    public class AdriftSequence : MonoBehaviour
    {
        [SerializeField] ZeroGPlayer player;
        [SerializeField] GripBall ball;
        [SerializeField] OxygenSystem oxygen;
        [SerializeField] CameraShake playerShake;
        [SerializeField] Transform hatch;
        [SerializeField] float hatchReach = 3.5f;
        [Tooltip("Loose debris wedged against the hatch. It has to be knocked clear with the ball before you can get in.")]
        [SerializeField] BallSurface hatchBlocker;
        [SerializeField] float blockerClearDistance = 6.5f;
        [Tooltip("The emergency release beside the hatch. The hatch stays locked until the ball hits it.")]
        [SerializeField] BallSurface hatchRelease;
        [Tooltip("Drifting further than this from the hatch counts as lost.")]
        [SerializeField] float maxDistance = 260f;

        [Header("Impact shot")]
        [SerializeField] Camera cutsceneCamera;
        [SerializeField] Transform impactShot;
        [SerializeField] Transform rocket;
        [SerializeField] Transform rocketFlyStart;
        [SerializeField] Transform rocketRest;
        [SerializeField] Transform rock;
        [SerializeField] Transform rockStart;
        [SerializeField] Transform rockRest;
        [SerializeField] ParticleSystem[] plumes;
        [SerializeField] ParticleSystem debrisBurst;
        [SerializeField] GameObject damageEffects;

        // After a failed attempt the scene restarts straight into the spacewalk.
        static bool retry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => retry = false;

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;
            player.CanLook = false;
            ball.enabled = false;
            hud.ShowOxygen(false);

            if (!retry)
            {
                yield return ImpactShot();
            }
            else
            {
                rocket.SetPositionAndRotation(rocketRest.position, rocketRest.rotation);
                rock.SetPositionAndRotation(rockRest.position, rockRest.rotation);
                damageEffects.SetActive(true);
                cutsceneCamera.gameObject.SetActive(false);
                player.gameObject.SetActive(true);
            }

            // Waking up
            if (!retry) yield return hud.ShowTitle("LEVEL 2", "Adrift", 2f);
            player.Tumble = 14f;
            player.Body.linearVelocity = new Vector3(-0.35f, 0.1f, -0.45f);
            StartCoroutine(hud.FadeTo(0f, retry ? 2f : 6f, Color.black));
            if (!retry)
            {
                yield return new WaitForSeconds(2f);
                yield return Dialogue.Play(
                    (Speakers.Commander, "...Stars. Why am I looking at stars?"),
                    (Speakers.Commander, "I'm outside. I'm outside the ship."));
                player.CanLook = true;
                yield return Dialogue.Play(
                    (Speakers.ShipAI, "Commander. A rock struck us as we left the ring. The hatch failed and you were pulled out."),
                    (Speakers.ShipAI, "Your suit has no thrusters. You have four minutes of air."),
                    (Speakers.Commander, "No thrusters. Nothing to push against."),
                    (Speakers.Engineer, "Stuck in zero gravity, that ball is the difference between drifting forever and getting home."));
                // What you told the engineer on launch day comes back to you now.
                if (state.HasFlag("memory.promise.0")) yield return Dialogue.Say(Speakers.Commander, "I promised I'd come back. So move.");
                else if (state.HasFlag("memory.promise.2")) yield return Dialogue.Say(Speakers.Commander, "Keep the lights on. I'm not done yet.");
                if (state.HasFlag("memory.spare_air"))
                {
                    oxygen.AddCapacity(0.3f);
                    hud.ShowToast("Spare canister from the quartermaster: +30% air");
                }
            }
            player.CanLook = true;
            player.Tumble = 0f;

            // The ball comes out of the pocket
            hud.SetObjective("Take the grip ball out of your pocket");
            hud.SetPrompt("[E] Take out the grip ball");
            yield return new WaitUntil(() => !Dialogue.IsPlaying && !InputLock.IsLocked && GameInput.Interact.WasPressedThisFrame());
            ball.enabled = true;
            ball.TakeOut();
            // The controls stay on screen for the whole spacewalk, and the air only starts running down once the
            // first throw has gripped, so nobody loses air while reading.
            const string controls = "LEFT MOUSE  throw at a rock   •   when it pulses, HOLD LEFT MOUSE to pull in\nR  call the ball back   •   SPACE  push off a rock   •   C  stats";
            hud.ShowOxygen(true);
            ball.Gripped += () => oxygen.Draining = true;
            bool failed = false, gripped = false, arrived = false, saidGrip = false, saidArrive = false;
            oxygen.Depleted += () => failed = true;
            ball.Gripped += () => gripped = true;
            ball.Arrived += () => arrived = true;
            bool bounced = false, saidFrost = false, saidBlocked = false, saidCleared = false, saidSealed = false, saidReleased = false;
            ball.Bounced += () => bounced = true;

            while (true)
            {
                float distance = Vector3.Distance(player.transform.position, hatch.position);
                bool blockedNow = hatchBlocker != null && Vector3.Distance(hatchBlocker.transform.position, hatch.position) < blockerClearDistance;
                bool locked = hatchRelease != null && !hatchRelease.WasHit;
                string goal = distance > 45f ? "Get back to the rocket's hatch"
                    : blockedNow ? "Knock the debris clear of the hatch"
                    : locked ? "Hit the yellow emergency release beside the hatch"
                    : "Get to the hatch";
                hud.SetObjective($"{goal}  ({distance:0} m)\n{controls}");
                // Everything is heard from inside the helmet: as the air runs low, the breathing gets faster and louder.
                float panic = 1f - Mathf.Clamp01(oxygen.Normalized * 1.6f);
                AudioManager.SetLoop("ambience", Mathf.Lerp(0.5f, 0.95f, panic), Mathf.Lerp(1f, 1.7f, panic));
                bool blocked = hatchBlocker != null && Vector3.Distance(hatchBlocker.transform.position, hatch.position) < blockerClearDistance;
                if (distance < hatchReach && !blocked && !locked) break;
                if (distance < hatchReach + 2f && locked && !blocked && !saidSealed && !Dialogue.IsPlaying)
                {
                    saidSealed = true;
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "The hatch is sealed from the strike. The emergency release is the yellow box beside it. Hit it."));
                }
                if (!locked && hatchRelease != null && !saidReleased && !Dialogue.IsPlaying)
                {
                    saidReleased = true;
                    AudioManager.Play(Sfx.PowerUp, 0.7f);
                    state.AddXp(25, "Hatch released");
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "Release fired. The hatch is open. Come home."));
                }
                if (bounced && !saidFrost && !Dialogue.IsPlaying)
                {
                    saidFrost = true;
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "That is glazed with frost. The ball cannot bite into it. It will only skip off. Look for bare rock or torn hull."));
                }
                if (blocked && distance < 45f && !saidBlocked && !Dialogue.IsPlaying)
                {
                    saidBlocked = true;
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "Debris is wedged against the hatch. It is loose. Knock it clear with the ball."));
                }
                if (!blocked && saidBlocked && !saidCleared && !Dialogue.IsPlaying)
                {
                    saidCleared = true;
                    state.AddXp(25, "Hatch cleared");
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, locked ? "Debris is clear. Now the emergency release. The yellow box beside the hatch." : "Hatch is clear. Come home."));
                }
                if (failed || distance > maxDistance)
                {
                    yield return Fail();
                    yield break;
                }
                if (gripped && !saidGrip && !Dialogue.IsPlaying)
                {
                    saidGrip = true;
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "It's holding. Now reel yourself in."));
                }
                if (arrived && !saidArrive && !Dialogue.IsPlaying)
                {
                    saidArrive = true;
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "Good. Now the next one. Work your way to the hull."));
                }
                yield return null;
            }

            // Back aboard
            retry = false;
            ball.enabled = false;
            oxygen.Draining = false;
            player.Body.linearVelocity = Vector3.zero;
            hud.SetPrompt(null);
            hud.SetObjective(null);
            AudioManager.Play(Sfx.Door, 0.8f);
            state.SetFlag("adrift.done");
            state.AddXp(60, "Made it back aboard");
            if (oxygen.Normalized > 0.5f) state.AddXp(20, "Air to spare");
            if (ball.Throws <= 6) state.AddXp(20, "Clean throws");
            yield return Dialogue.Play(
                (Speakers.Commander, "Got it. I've got the rail."),
                (Speakers.ShipAI, "Outer hatch cycling. Welcome back, Commander."));
            SceneFlow.Load(SceneFlow.Cockpit, 1.5f);
        }

        IEnumerator ImpactShot()
        {
            var hud = HUD.Instance;
            player.gameObject.SetActive(false);
            cutsceneCamera.gameObject.SetActive(true);
            cutsceneCamera.transform.SetPositionAndRotation(impactShot.position, impactShot.rotation);
            hud.SetLetterbox(true, 0f);
            damageEffects.SetActive(false);

            rocket.SetPositionAndRotation(rocketFlyStart.position, rocketFlyStart.rotation);
            rock.SetPositionAndRotation(rockStart.position, rockStart.rotation);
            foreach (var plume in plumes) plume.Play();
            AudioManager.StartLoop("engine", Sfx.Roar, 0.5f, 0.4f);
            AudioManager.Play(Sfx.Whoosh, 0.8f);
            StartCoroutine(hud.FadeTo(0f, 0.7f, Color.white));

            // The shot takes its time: the rocket crosses the frame, the warning comes, and only then the rock arrives.
            const float duration = 9f;
            var basePosition = impactShot.position;
            bool warned = false, braced = false;
            float skipHeld = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // Hold Enter to jump to the impact.
                skipHeld = GameInput.Skip.IsPressed() ? skipHeld + Time.deltaTime : 0f;
                hud.SetSkipHint(skipHeld / 0.8f);
                if (skipHeld >= 0.8f) break;
                float k = t / duration;
                if (!warned && t > 2.5f)
                {
                    warned = true;
                    AudioManager.Play(Sfx.Warning);
                    StartCoroutine(Dialogue.Say(Speakers.ShipAI, "Proximity alert. Object on a collision course."));
                }
                if (!braced && t > 6.3f && !Dialogue.IsPlaying)
                {
                    braced = true;
                    StartCoroutine(Dialogue.Say(Speakers.Commander, "I see it. Brace—"));
                }
                rocket.position = Vector3.Lerp(rocketFlyStart.position, rocketRest.position, k);
                rock.position = Vector3.Lerp(rockStart.position, rockRest.position, k * k);
                rock.Rotate(40f * Time.deltaTime, 25f * Time.deltaTime, 0f);
                cutsceneCamera.transform.position = basePosition + Random.insideUnitSphere * 0.05f;
                yield return null;
            }

            // The hit
            hud.SetSkipHint(-1f);
            Dialogue.Stop();
            AudioManager.Play(Sfx.Explosion);
            AudioManager.StopLoop("engine", 0.1f);
            debrisBurst.Play();
            foreach (var plume in plumes) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hud.SetFade(1f, Color.white);
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                cutsceneCamera.transform.position = basePosition + Random.insideUnitSphere * 0.6f;
                yield return null;
            }
            hud.SetFade(1f, Color.black);
            rocket.SetPositionAndRotation(rocketRest.position, rocketRest.rotation);
            rock.SetPositionAndRotation(rockRest.position, rockRest.rotation);
            damageEffects.SetActive(true);
            yield return new WaitForSeconds(2.5f);

            hud.SetLetterbox(false, 0f);
            cutsceneCamera.gameObject.SetActive(false);
            player.gameObject.SetActive(true);
        }

        IEnumerator Fail()
        {
            var hud = HUD.Instance;
            retry = true;
            ball.enabled = false;
            player.CanLook = false;
            hud.SetPrompt(null);
            yield return Dialogue.Say(Speakers.ShipAI, "Commander? Commander, respond.");
            yield return hud.FadeTo(1f, 2f, Color.black);
            yield return hud.ShowTitle("OUT OF AIR", "Trying again", 2f, new Color(1f, 0.4f, 0.35f));
            SceneFlow.Reload(0.1f);
        }
    }
}
