using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Act I on the planet that looks like Earth. Objectives, in order: scan the air (oxygen stops draining once
    /// it's breathable), salvage parts, find the ruins and the ancient gate, follow a radio signal to a second
    /// rocket, and meet its pilot. How the player approaches the pilot sets the starting Trust.
    /// Navigation 3+ marks salvage with beacons; Survival slows oxygen loss.
    /// </summary>
    public class LandingSiteSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] OxygenSystem oxygen;
        [SerializeField] Interactable airScanner;
        [SerializeField] Interactable[] salvage;
        [SerializeField] GameObject[] salvageBeacons;
        [SerializeField] int navigationForBeacons = 3;
        [SerializeField] TriggerZone ruinsZone;
        [SerializeField] Interactable gateConsole;
        [SerializeField] TriggerZone signalZone;
        [SerializeField] GameObject signalMarker;
        [SerializeField] Interactable pilotShip;

        int salvaged;
        bool failed;

        void Start()
        {
            foreach (var beacon in salvageBeacons) beacon.SetActive(false);
            if (signalMarker != null) signalMarker.SetActive(false);
            signalZone.gameObject.SetActive(false);
            Story.Lock(salvage);
            Story.Lock(gateConsole, pilotShip);
            oxygen.Depleted += () =>
            {
                if (failed) return;
                failed = true;
                StartCoroutine(Story.OxygenFailure(player));
            };
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            player.CanMove = false;
            hud.SetOxygen(oxygen.Normalized);
            hud.ShowOxygen(true);
            yield return hud.FadeTo(0f, 3f, Color.black);

            yield return Dialogue.Play(
                (Speakers.ShipAI, "Hull integrity forty percent. We're on the ground. Mostly in one piece."),
                (Speakers.Commander, "Mostly?"),
                (Speakers.ShipAI, "Your suit has limited oxygen. I suggest finding out if this air is safe to breathe."));

            // 1. Air
            player.CanMove = true;
            oxygen.Draining = true;
            hud.SetObjective("Scan the atmosphere with the sensor kit");
            yield return Story.Use(airScanner);

            oxygen.Draining = false;
            hud.ShowOxygen(false);
            state.SetFlag("site.air_breathable");
            state.AddXp(30, "Atmosphere scanned");
            yield return Dialogue.Play(
                (Speakers.ShipAI, "Nitrogen seventy-eight percent. Oxygen twenty-one. Commander... it's the same air as Earth."),
                (Speakers.Commander, "That's not possible."),
                (Speakers.ShipAI, "And yet you may remove your helmet."));

            // 2. Salvage
            if (state.Check(Stat.Navigation, navigationForBeacons))
            {
                foreach (var beacon in salvageBeacons) beacon.SetActive(true);
                hud.ShowToast($"NAVIGATION {navigationForBeacons}: salvage marked on your scanner");
            }
            foreach (var part in salvage) part.Interacted += OnSalvaged;
            Story.Unlock(salvage);
            UpdateSalvageObjective();
            yield return new WaitUntil(() => salvaged >= salvage.Length);
            yield return Dialogue.Say(Speakers.ShipAI, "That's enough to patch the hull. Now, about those structures in the trees.");

            // 3. Ruins and the gate
            hud.SetObjective("Investigate the structures in the forest");
            yield return Story.Enter(ruinsZone);
            state.SetFlag("site.found_ruins");
            state.AddXp(40, "Discovery: alien ruins");
            yield return Dialogue.Play(
                (Speakers.Commander, "These aren't natural. Someone built these. A long time ago."),
                (Speakers.ShipAI, "The markings match nothing in my database. And the stars last night matched no chart of Earth's sky."));

            hud.SetObjective("Examine the ring structure behind the ruins");
            yield return Story.Use(gateConsole);
            state.SetFlag("site.found_gate");
            state.AddXp(40, "Discovery: the ancient gate");
            yield return Dialogue.Play(
                (Speakers.Commander, "A ring, big enough to fly a ship through. This is what pulled me in."),
                (Speakers.ShipAI, "It still has power. It is thousands of years old, and it still has power."));

            // 4. Radio signal
            yield return Dialogue.Say(Speakers.ShipAI, "Commander, I'm picking up a radio signal. It's human. An old frequency. Very old.");
            hud.SetObjective("Follow the radio signal");
            signalZone.gameObject.SetActive(true);
            if (signalMarker != null) signalMarker.SetActive(true);
            yield return Story.Enter(signalZone);

            hud.SetObjective(null);
            if (signalMarker != null) signalMarker.SetActive(false);
            player.CanMove = false;
            state.SetFlag("site.found_second_rocket");
            state.AddXp(50, "Discovery: a second rocket");
            yield return Dialogue.Play(
                (Speakers.Commander, "Another rocket. Old. Patched together by hand."),
                (Speakers.ShipAI, "Commander. There is someone inside. Human."));

            yield return MeetThePilot();

            player.CanMove = true;
            hud.SetObjective("Board the colony pilot's ship to reach the burnt moon");
            yield return Story.Use(pilotShip);
            SceneFlow.Load(SceneFlow.FuelMoon, 1.5f);
        }

        /// <summary>The first trust choice: talk right away, or watch for a few days first.</summary>
        IEnumerator MeetThePilot()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            int approach = 0;
            yield return Dialogue.Choose(new[] { "Walk out and say hello.", "Stay hidden. Watch them for a few days." }, c => approach = c);

            if (approach == 0)
            {
                state.SetFlag("pilot.approached");
                state.ChangeTrust(15);
                yield return Dialogue.Play(
                    (Speakers.Commander, "Hello? I'm not armed. My ship came down past the ridge."),
                    (Speakers.Pilot, "...You're human. Where are you from?"),
                    (Speakers.Commander, "Earth."),
                    (Speakers.Pilot, "Earth. The world that left my great-grandparents behind."));
            }
            else
            {
                state.SetFlag("pilot.observed");
                state.ChangeTrust(-10);
                yield return hud.FadeTo(1f, 1.5f, Color.black);
                yield return hud.ShowTitle("THREE DAYS LATER", "", 2f);
                yield return hud.FadeTo(0f, 1.5f, Color.black);
                state.AddXp(20, "Observation notes");
                yield return Dialogue.Play(
                    (Speakers.ShipAI, "Three days of notes. They come from a colony on Planet X. They were sent to find Earth."),
                    (Speakers.ShipAI, "Also, they found your footprints yesterday."),
                    (Speakers.Pilot, "You can come out. I know you've been watching me."));

                int answer = 0;
                yield return Dialogue.Choose(new[] { "You're right. I'm sorry. I was afraid.", "Watching? I only just got here." }, c => answer = c);
                if (answer == 0)
                {
                    state.ChangeTrust(8);
                    yield return Dialogue.Say(Speakers.Pilot, "...At least you admit it. I'd have done the same.");
                }
                else
                {
                    state.SetFlag("pilot.lied_about_watching");
                    state.ChangeTrust(-5);
                    yield return Dialogue.Say(Speakers.Pilot, "Sure you did.");
                }
            }

            yield return Dialogue.Play(
                (Speakers.Pilot, "I'm from the colony on Planet X. A hundred years cut off. We're running out of everything."),
                (Speakers.Pilot, "They sent me to find Earth and ask for help. Is it true Earth has oceans you can drink from?"));

            int honesty = 0;
            yield return Dialogue.Choose(new[] { "Not anymore. Earth is running out too.", "It's complicated.", "It's a paradise. You'll see." }, c => honesty = c);
            if (honesty == 0)
            {
                state.SetFlag("pilot.honest_about_earth");
                state.ChangeTrust(5);
                yield return Dialogue.Say(Speakers.Pilot, "Then we're both a long way from help.");
            }
            else if (honesty == 1)
            {
                yield return Dialogue.Say(Speakers.Pilot, "It always is.");
            }
            else
            {
                state.SetFlag("pilot.lied_about_earth");
                yield return Dialogue.Say(Speakers.Pilot, "A paradise. That's what the old stories say too.");
            }

            yield return Dialogue.Play(
                (Speakers.ShipAI, "Neither ship has the fuel to leave. The burnt moon has ice we could refine."),
                (Speakers.Pilot, "My ship can still fly that far. My pump, your refinery. Neither works alone."));
        }

        void OnSalvaged(Interactable part)
        {
            salvaged++;
            GameState.Instance.AddXp(25, "Salvage recovered");
            int index = System.Array.IndexOf(salvage, part);
            if (index >= 0 && index < salvageBeacons.Length) salvageBeacons[index].SetActive(false);
            UpdateSalvageObjective();
        }

        void UpdateSalvageObjective()
        {
            if (salvaged < salvage.Length)
                HUD.Instance.SetObjective($"Salvage parts from the wreckage ({salvaged}/{salvage.Length})");
        }
    }
}
