using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The first playable scene. The pilot wakes up after the gate, restores power (phase 1: life support + sensors),
    /// sees a planet that is almost Earth, then reroutes power to the engines (phase 2) and crash-lands.
    /// Sensors only need power long enough to scan, so the intended solution is to move that cell to the engines afterwards.
    /// Engineering 3+ adds a cell, so both phases can be powered at once.
    /// </summary>
    public class CockpitSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] Interactor interactor;
        [SerializeField] Interactable console;
        [SerializeField] PowerRoutingPuzzle puzzle;
        [SerializeField] AlarmLights alarms;
        [SerializeField] CameraShake cameraShake;

        [Header("Window")]
        [SerializeField] Transform windowShutter;
        [SerializeField] Vector3 shutterOpenOffset = new(0f, 2.4f, 0f);
        [SerializeField] float shutterOpenTime = 3f;

        [Header("Landing")]
        [SerializeField] Transform planet;
        [SerializeField] float planetApproachDistance = 180f;
        [SerializeField] float landingDuration = 7f;

        void Start()
        {
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            player.CanMove = false;
            interactor.enabled = false;
            alarms.Trigger();
            console.Interacted += _ => puzzle.Open();

            // Wake up
            yield return new WaitForSeconds(1f);
            yield return Dialogue.Say(Speakers.ShipAI, "Commander. Commander, wake up.");
            StartCoroutine(hud.FadeTo(0f, 4f, Color.black));
            yield return Dialogue.Say(Speakers.ShipAI, "Commander, I need you conscious. Main power is down. Life support is running on reserve.");
            yield return Dialogue.Say(Speakers.Commander, "...What happened? Where's Mission Control?");
            yield return Dialogue.Say(Speakers.ShipAI, "Unknown. Unknown. And I would like to also say unknown.");

            // Phase 1: life support + sensors
            hud.SetObjective("Restore power at the main console");
            puzzle.SetGoal("Restore LIFE SUPPORT and SENSORS.");
            puzzle.SetHint("Sensors don't need power forever. Just long enough to see where we are.");
            interactor.enabled = true;

            if (puzzle.EngineeringBonus)
            {
                yield return new WaitUntil(() => puzzle.IsOpen);
                yield return Dialogue.Say(Speakers.ShipAI, "You're overclocking the power bus? That is not in the manual. ...It's working.");
            }

            yield return new WaitUntil(() => puzzle.Grid.AreOnline(PowerRoutingPuzzle.SystemLifeSupport, PowerRoutingPuzzle.SystemSensors));
            puzzle.Close();
            interactor.enabled = false;
            alarms.Stop();
            state.SetFlag("cockpit.power_restored");
            state.AddXp(50, "Power restored");

            // The view
            AudioManager.Play(Sfx.Door, 0.7f);
            yield return OpenShutter();
            AudioManager.PlayMusic(Sfx.PadCalm, 4f);
            yield return Dialogue.Say(Speakers.Commander, "Blue oceans. Clouds. That's... that's home. Somehow I'm home.");
            yield return Dialogue.Say(Speakers.ShipAI, "Overlaying Earth's continents now.");
            yield return new WaitForSeconds(1f);
            yield return Dialogue.Say(Speakers.ShipAI, "They don't match. There is no Moon, only that small burnt one. The stars are wrong.");
            yield return Dialogue.Say(Speakers.ShipAI, "Commander, I regret to inform you that this is not Earth. I also regret to inform you that I don't know where we are.");
            yield return Dialogue.Say(Speakers.ShipAI, "Our orbit is decaying. Would you like our survival odds, or a comforting lie?");

            int choice = 0;
            yield return Dialogue.Choose(new[] { "Give me the number.", "Lie to me.", "Just get us down." }, c => choice = c);
            switch (choice)
            {
                case 0:
                    state.SetFlag("cockpit.asked_odds");
                    yield return Dialogue.Say(Speakers.ShipAI, "Thirty-one percent. I rounded up.");
                    break;
                case 1:
                    state.SetFlag("cockpit.asked_lie");
                    yield return Dialogue.Say(Speakers.ShipAI, "Everything is fine. We will definitely be fine. This is a normal Tuesday.");
                    yield return Dialogue.Say(Speakers.Commander, "...Thanks.");
                    break;
                default:
                    state.SetFlag("cockpit.just_land");
                    yield return Dialogue.Say(Speakers.ShipAI, "Rerouting all priorities to 'down'.");
                    break;
            }

            // Phase 2: engines
            console.ResetUse();
            hud.SetObjective("Reroute power to the ENGINES and land");
            puzzle.SetGoal("Keep LIFE SUPPORT online and bring the ENGINES online.");
            puzzle.SetHint("We've already scanned the planet. That sensor cell could be doing something more useful.");
            interactor.enabled = true;

            yield return new WaitUntil(() => puzzle.Grid.AreOnline(PowerRoutingPuzzle.SystemLifeSupport, PowerRoutingPuzzle.SystemEngines));
            puzzle.Close();
            interactor.enabled = false;
            hud.SetObjective(null);
            state.SetFlag("cockpit.engines_online");
            state.AddXp(50, "Engines online");
            if (!puzzle.HintUsed) state.AddXp(25, "Solved without hints");

            yield return Land();
        }

        IEnumerator OpenShutter()
        {
            if (windowShutter == null) yield break;
            var closed = windowShutter.localPosition;
            var open = closed + shutterOpenOffset;
            for (float t = 0; t < shutterOpenTime; t += Time.deltaTime)
            {
                windowShutter.localPosition = Vector3.Lerp(closed, open, Mathf.SmoothStep(0f, 1f, t / shutterOpenTime));
                yield return null;
            }
            windowShutter.localPosition = open;
        }

        IEnumerator Land()
        {
            var hud = HUD.Instance;
            alarms.Trigger();
            yield return Dialogue.Say(Speakers.ShipAI, "Engines online. Beginning atmospheric entry. Please hold on to something. Anything.");

            AudioManager.Play(Sfx.PowerUp);
            AudioManager.StartLoop("rumble", Sfx.Rumble, 0.2f, 1f);
            Vector3 planetStart = planet != null ? planet.position : Vector3.zero;
            Vector3 planetEnd = planet != null ? planetStart - (planetStart - transform.position).normalized * planetApproachDistance : Vector3.zero;

            for (float t = 0; t < landingDuration; t += Time.deltaTime)
            {
                float k = t / landingDuration;
                if (planet != null) planet.position = Vector3.Lerp(planetStart, planetEnd, k * k);
                if (cameraShake != null) cameraShake.SetConstant(Mathf.Lerp(0.005f, 0.06f, k));
                AudioManager.SetLoop("rumble", Mathf.Lerp(0.2f, 1f, k), Mathf.Lerp(0.8f, 1.3f, k));
                yield return null;
            }

            AudioManager.Play(Sfx.Impact);
            AudioManager.StopLoop("rumble", 0.3f);
            AudioManager.StopLoop("alarm", 0.3f);
            yield return hud.FadeTo(1f, 0.25f, Color.white);
            if (cameraShake != null) cameraShake.SetConstant(0f);
            yield return new WaitForSeconds(0.4f);
            SceneFlow.Load(SceneFlow.LandingSite, 1.5f, Color.black);
        }
    }
}
