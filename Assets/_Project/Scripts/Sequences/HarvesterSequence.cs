using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Acts III and IV: a harvesting station that is still draining a world. Getting in has four solutions
    /// (Engineering, Navigation, the other pilot's trust, or searching outside). Inside, Architect records tell
    /// the builders' history, a terminal shows Earth and Planet X on the target list, and old transmissions reveal
    /// why the two worlds lost contact. What the player tells the pilot about that changes Trust and the endings.
    /// </summary>
    public class HarvesterSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] TriggerZone entranceZone;
        [SerializeField] GameObject door;
        [SerializeField] Interactable accessKey;
        [SerializeField] Interactable[] records;
        [SerializeField] Interactable targetTerminal;
        [SerializeField] Interactable commsTerminal;
        [SerializeField] Interactable gateConsole;
        [SerializeField] GlyphPuzzle puzzle;
        [SerializeField] int statForShortcut = 3;
        [SerializeField] int trustForHelp = 40;

        int wrongAlignments;

        void Start()
        {
            Story.EnsureMidGameDefaults();
            accessKey.gameObject.SetActive(false);
            Story.Lock(records);
            Story.Lock(targetTerminal, commsTerminal, gateConsole);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            yield return Story.Arrive("THE DRAINED SEA", "Something here is still running.");
            yield return Dialogue.Play(
                (Speakers.Pilot, "This was an ocean. Look at the old shoreline. It's a hundred meters above us."),
                (Speakers.ShipAI, "That tower is pumping what is left of the water through a gate. It is automated. Nobody is in charge."));

            hud.SetObjective("Reach the station entrance");
            yield return Story.Enter(entranceZone);
            yield return Dialogue.Say(Speakers.ShipAI, "Sealed, and the security system is live. We need a way in that doesn't set it off.");
            yield return GetInside();

            // Inside: records are optional, the two terminals are required.
            for (int i = 0; i < records.Length; i++)
            {
                int index = i;
                records[i].Interacted += _ => StartCoroutine(OnRecord(index));
            }
            Story.Unlock(records);
            hud.SetObjective("Find out what this station is doing (main terminal)");
            yield return Story.Use(targetTerminal);
            state.SetFlag("truth.targets");
            AudioManager.Play(Sfx.Stinger);
            state.AddXp(60, "Discovery: the target list");
            yield return Dialogue.Play(
                (Speakers.ShipAI, "This is a target list. Worlds ranked by their resources and by how well they can defend themselves."),
                (Speakers.ShipAI, "Entry 41: Planet X. Rich in minerals. Defenses: minimal. Status: next."),
                (Speakers.Pilot, "Next?"),
                (Speakers.ShipAI, "Entry 42: Earth. Large population. Water remaining. Status: queued."),
                (Speakers.Commander, "The gate that grabbed my ship wasn't an accident. It was scouting."),
                (Speakers.ShipAI, "The machines are not broken. They are doing exactly what they were built to do."));
            puzzle.RevealHint(0);
            puzzle.RevealHint(1);

            hud.SetObjective("Check the communications terminal");
            yield return Story.Use(commsTerminal);
            yield return TheMissingHistory();

            if (Story.Records >= records.Length)
                yield return Dialogue.Say(Speakers.ShipAI, "The records name the network's control centre. I have its full gate address.");
            else
                yield return Dialogue.Say(Speakers.ShipAI, "I have two of the three symbols for the network's control centre. More records might give us the third.");

            gateConsole.Interacted += _ => puzzle.Open();
            Story.Unlock(gateConsole);
            puzzle.Failed += () => StartCoroutine(OnWrongAlignment());
            hud.SetObjective("Set the gate for the control centre");
            yield return new WaitUntil(() => puzzle.IsSolved);
            if (Endings.Playing) yield break;

            hud.SetObjective(null);
            state.AddXp(60, "Gate aligned");
            yield return Dialogue.Play(
                (Speakers.Pilot, "If that list is real, my people have no time left."),
                (Speakers.Commander, "Then we go to the place that gives the orders."));
            yield return Story.Jump(SceneFlow.Control);
        }

        /// <summary>Four ways through the door. Three depend on stats or trust; searching always works.</summary>
        IEnumerator GetInside()
        {
            var state = GameState.Instance;
            var options = new[]
            {
                $"[ENGINEERING {statForShortcut}] Cut the door's power feed",
                $"[NAVIGATION {statForShortcut}] Find the maintenance duct on the station plans",
                "[TRUST] Ask the pilot to help force the breaker",
                "Search outside for another way in",
            };

            while (door.activeSelf)
            {
                int choice = 0;
                yield return Dialogue.Choose(options, c => choice = c);
                switch (choice)
                {
                    case 0 when state.Check(Stat.Engineering, statForShortcut):
                        state.SetFlag("station.entry_engineering");
                        state.AddXp(40, "Engineering: door power cut");
                        yield return Dialogue.Say(Speakers.ShipAI, "Power feed isolated. The door thinks it is closed. It is not.");
                        AudioManager.Play(Sfx.Door);
                        door.SetActive(false);
                        break;
                    case 1 when state.Check(Stat.Navigation, statForShortcut):
                        state.SetFlag("station.entry_navigation");
                        state.AddXp(40, "Navigation: maintenance duct found");
                        yield return Dialogue.Say(Speakers.Commander, "There. A service duct runs right past the lock. I can reach the release from inside.");
                        AudioManager.Play(Sfx.Door);
                        door.SetActive(false);
                        break;
                    case 2 when state.Trust >= trustForHelp:
                        state.SetFlag("station.entry_together");
                        state.ChangeTrust(5);
                        state.AddXp(40, "Opened together");
                        yield return Dialogue.Say(Speakers.Pilot, "On three. One, two... there. Don't tell anyone I helped an Earther break and enter.");
                        AudioManager.Play(Sfx.Door);
                        door.SetActive(false);
                        break;
                    case 2:
                        yield return Dialogue.Say(Speakers.Pilot, "Put my hands in that machine because you say so? No.");
                        break;
                    case 3:
                        yield return Dialogue.Say(Speakers.ShipAI, "There is a wrecked maintenance drone behind the station. It may still carry an access key.");
                        accessKey.gameObject.SetActive(true);
                        HUD.Instance.SetObjective("Find the access key behind the station");
                        yield return Story.Use(accessKey);
                        state.SetFlag("station.entry_search");
                        state.AddXp(20, "Access key found");
                        AudioManager.Play(Sfx.Door);
                        door.SetActive(false);
                        break;
                    default:
                        yield return Dialogue.Say(Speakers.ShipAI, "You don't have the training for that yet. Press Tab if you have stat points to spend.");
                        break;
                }
            }
            HUD.Instance.ShowToast("The station door is open");
        }

        IEnumerator OnRecord(int index)
        {
            var state = GameState.Instance;
            state.SetFlag("record." + index);
            state.AddXp(30, "Architect record");
            switch (index)
            {
                case 0:
                    yield return Dialogue.Play(
                        (Speakers.Commander, "A recording. Their homeworld was running dry: water, metal, clean air."),
                        (Speakers.ShipAI, "Instead of fixing it, they built machines to take from other worlds. This network."));
                    break;
                case 1:
                    yield return Dialogue.Play(
                        (Speakers.Commander, "Evacuation orders. They knew their world was failing, so they built escape sites across the network."),
                        (Speakers.ShipAI, "The logs simply stop. The evacuation failed. The machines never noticed their makers were gone."));
                    break;
                default:
                    state.SetFlag("truth.homeworld");
                    yield return Dialogue.Play(
                        (Speakers.ShipAI, "A star chart of the builders' homeworld. Blue oceans. One small burnt moon."),
                        (Speakers.Pilot, "That's where we met."),
                        (Speakers.Commander, "It was never a second Earth. It was their home. It healed after they were gone."));
                    break;
            }
            if (Story.Records >= records.Length) puzzle.RevealHint(2);
        }

        /// <summary>Why Earth and Planet X lost contact, and what the player tells the pilot about it.</summary>
        IEnumerator TheMissingHistory()
        {
            var state = GameState.Instance;
            state.SetFlag("truth.transmissions");
            state.AddXp(40, "Discovery: the lost transmissions");
            yield return Dialogue.Play(
                (Speakers.ShipAI, "Old human transmissions, trapped in the network's distortion. Earth called Planet X for nine years. The gates swallowed every signal."),
                (Speakers.ShipAI, "There is one more file. Earth's council closed the search early. The reason given: 'we cannot afford a rescue.'"),
                (Speakers.ShipAI, "The pilot is still at the door. They have not seen this."));

            int choice = 0;
            yield return Dialogue.Choose(new[] { "Tell them everything.", "Tell them only that Earth tried.", "Say nothing." }, c => choice = c);
            switch (choice)
            {
                case 0:
                    state.SetFlag("truth.shared");
                    state.ChangeTrust(12);
                    yield return Dialogue.Play(
                        (Speakers.Pilot, "So they tried. And then they gave up on us."),
                        (Speakers.Pilot, "...Thank you for not hiding the second part."));
                    break;
                case 1:
                    state.SetFlag("truth.partial");
                    state.ChangeTrust(4);
                    yield return Dialogue.Say(Speakers.Pilot, "They tried? All this time, we thought they just left.");
                    break;
                default:
                    state.SetFlag("truth.withheld");
                    state.ChangeTrust(-5);
                    yield return Dialogue.Say(Speakers.ShipAI, "Understood. I will not mention it.");
                    break;
            }

            if (state.HasFlag("pilot.lied_about_earth"))
            {
                state.ChangeTrust(-10);
                yield return Dialogue.Say(Speakers.Pilot, "One thing. That list says Earth is running dry. You told me it was a paradise.");
            }
        }

        IEnumerator OnWrongAlignment()
        {
            wrongAlignments++;
            puzzle.Close();
            yield return Story.BurnFuel();
            if (!Endings.Playing)
                yield return Dialogue.Say(Speakers.ShipAI, "Wrong address. The records in this station may hold the missing symbol.");
        }
    }
}
