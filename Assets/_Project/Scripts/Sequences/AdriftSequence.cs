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
            }
            player.CanLook = true;
            player.Tumble = 0f;

            // The ball comes out of the pocket
            hud.SetObjective("Take the grip ball out of your pocket");
            hud.SetPrompt("[E] Take out the grip ball");
            yield return new WaitUntil(() => !Dialogue.IsPlaying && !InputLock.IsLocked && GameInput.Interact.WasPressedThisFrame());
            ball.enabled = true;
            ball.TakeOut();
            hud.ShowToast("Left mouse: throw at a rock");
            hud.ShowToast("Hold right mouse: reel yourself in");
            hud.ShowToast("R: call the ball back");
            hud.ShowToast("Space: push off a rock you are touching");

            oxygen.Draining = true;
            hud.ShowOxygen(true);
            bool failed = false, gripped = false, arrived = false, saidGrip = false, saidArrive = false;
            oxygen.Depleted += () => failed = true;
            ball.Gripped += () => gripped = true;
            ball.Arrived += () => arrived = true;

            while (true)
            {
                float distance = Vector3.Distance(player.transform.position, hatch.position);
                hud.SetObjective($"Get back to the rocket's hatch  ({distance:0} m)\nFollow the green light");
                if (distance < hatchReach) break;
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

            const float duration = 2.6f;
            var basePosition = impactShot.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                rocket.position = Vector3.Lerp(rocketFlyStart.position, rocketRest.position, k);
                rock.position = Vector3.Lerp(rockStart.position, rockRest.position, k * k);
                rock.Rotate(40f * Time.deltaTime, 25f * Time.deltaTime, 0f);
                cutsceneCamera.transform.position = basePosition + Random.insideUnitSphere * 0.05f;
                yield return null;
            }

            // The hit
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
            yield return hud.ShowTitle("LOST TO THE DARK", "Trying again", 2f, new Color(1f, 0.4f, 0.35f));
            SceneFlow.Reload(0.1f);
        }
    }
}
