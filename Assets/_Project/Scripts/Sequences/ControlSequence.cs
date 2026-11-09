using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Act V: the network's control centre. The player restores the console (power puzzle), then decides what
    /// happens to the network. Sharing it needs the other pilot's trust and an honest history between them;
    /// a high Diplomacy stat can make up for some missing trust.
    /// </summary>
    public class ControlSequence : MonoBehaviour
    {
        public const string SystemCoreLink = "CORE LINK";
        public const string SystemGateControl = "GATE CONTROL";
        public const string SystemOverride = "HARVEST OVERRIDE";

        [SerializeField] FirstPersonController player;
        [SerializeField] Interactable console;
        [SerializeField] PowerRoutingPuzzle puzzle;
        [SerializeField] Interactable core;
        [SerializeField] int diplomacyToPersuade = 4;
        [SerializeField] int trustToPersuade = 40;

        void Start()
        {
            Story.EnsureMidGameDefaults();
            Story.Lock(console, core);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            yield return Story.Arrive("THE CONTROL CENTRE", "The heart of the network.");
            yield return Dialogue.Say(Speakers.ShipAI, "Every gate and every harvester reports to this place. It has run for thousands of years with nobody here.");
            if (!state.HasFlag("truth.homeworld"))
            {
                state.SetFlag("truth.homeworld");
                yield return Dialogue.Play(
                    (Speakers.ShipAI, "Commander, the star charts here match the first planet. It was never a second Earth. It was the builders' homeworld."),
                    (Speakers.Commander, "They drained it, left it, and it healed without them."));
            }

            // Wake the console.
            console.Interacted += _ => puzzle.Open();
            Story.Unlock(console);
            puzzle.SetGoal($"Bring {SystemCoreLink} and {SystemOverride} online.");
            puzzle.SetHint("The gates can hold themselves open for a few minutes. Take their power.");
            hud.SetObjective("Restore power to the control console");
            yield return new WaitUntil(() => puzzle.Grid.AreOnline(SystemCoreLink, SystemOverride));
            puzzle.Close();
            state.AddXp(60, "Control console online");
            if (!puzzle.HintUsed) state.AddXp(25, "Solved without hints");

            yield return Dialogue.Play(
                (Speakers.ShipAI, "The console is live. From here you can shut the harvesters down, turn them on other worlds, or lock the gates to one world."),
                (Speakers.Commander, "The builders had this same choice. They chose to keep taking."));
            if (state.Trust >= Story.TrustForBestEnding)
                yield return Dialogue.Say(Speakers.Pilot, "We shut them down. Both our worlds, together. That's the only version where nobody becomes them.");
            else
                yield return Dialogue.Say(Speakers.Pilot, "I don't know what you'll choose. I know what my people would be afraid of.");

            hud.SetObjective("Decide the future of the network (the core)");
            yield return Story.Use(core);
            hud.SetObjective(null);
            yield return Decide();
        }

        IEnumerator Decide()
        {
            var state = GameState.Instance;
            var options = new[]
            {
                "Shut down the harvesters. Share the gates with both worlds.",
                "Take control of the network for Earth alone.",
                "Walk away. Stay on the green homeworld.",
            };

            while (true)
            {
                int choice = 0;
                yield return Dialogue.Choose(options, c => choice = c);

                if (choice == 1)
                {
                    yield return Dialogue.Say(Speakers.Pilot, state.Trust >= Story.TrustForBestEnding
                        ? "After everything? I trusted you."
                        : "I knew it. Earth first. It was always going to be Earth first.");
                    yield return Endings.Play(Ending.EarthAboveAll);
                    yield break;
                }
                if (choice == 2)
                {
                    yield return Dialogue.Say(Speakers.Pilot, "You're just going to leave them? All of them?");
                    yield return Endings.Play(Ending.ArchitectsMistake);
                    yield break;
                }

                // Sharing needs the other pilot to agree: the override takes two people.
                if (state.HasFlag("truth.withheld"))
                {
                    yield return Dialogue.Play(
                        (Speakers.Pilot, "I read the transmission logs while you were at the console. You knew, and you said nothing."),
                        (Speakers.Pilot, "Share the network with Earth? I can't even share the truth with you."));
                    options[0] = "[The pilot refuses] Shut down the harvesters. Share the gates.";
                    continue;
                }
                if (state.Trust >= Story.TrustForBestEnding)
                {
                    yield return Dialogue.Say(Speakers.Pilot, "Together, then. On three.");
                    yield return Endings.Play(Ending.NewBeginning);
                    yield break;
                }
                if (state.Trust >= trustToPersuade && state.Check(Stat.Diplomacy, diplomacyToPersuade))
                {
                    state.AddXp(50, "Diplomacy: the pilot persuaded");
                    yield return Dialogue.Play(
                        (Speakers.Commander, "You don't have to trust Earth. Trust what we both saw out there. Neither of our worlds survives this alone."),
                        (Speakers.Pilot, "...That's the first thing an Earther has said that my people would believe. On three."));
                    yield return Endings.Play(Ending.NewBeginning);
                    yield break;
                }

                yield return Dialogue.Say(Speakers.Pilot, "Hand the gates to both worlds? I barely trust you. I'm not turning that key.");
                options[0] = $"[Needs more trust, or DIPLOMACY {diplomacyToPersuade}] Shut down the harvesters. Share the gates.";
            }
        }
    }
}
