using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Act II, chapter 4: the burnt moon. No air, so oxygen drains the whole time (refill at the ship).
    /// The pilots drill ice, route coolant in the right order (a logic puzzle; Engineering 3+ reads the schematic),
    /// refine fuel together, then decide how to split it. That choice sets fuel for the rest of the game.
    /// </summary>
    public class FuelMoonSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] OxygenSystem oxygen;
        [SerializeField] Interactable oxygenStation;
        [SerializeField] Interactable drill;
        [Tooltip("In the correct order: coldest tank first.")]
        [SerializeField] Interactable[] valves;
        [SerializeField] string[] valveNames = { "BLUE", "WHITE", "RED" };
        [SerializeField] Interactable refinery;
        [SerializeField] Interactable ship;
        [SerializeField] int engineeringForSchematic = 3;

        int valveStep;
        bool failed;

        void Start()
        {
            oxygen.Depleted += () =>
            {
                if (failed) return;
                failed = true;
                StartCoroutine(Story.OxygenFailure(player));
            };
            oxygenStation.Interacted += _ =>
            {
                oxygen.Refill(1000f);
                HUD.Instance.SetOxygen(oxygen.Normalized);
                HUD.Instance.ShowToast("Oxygen refilled");
            };
            Story.Lock(valves);
            Story.Lock(drill, refinery, ship);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            hud.SetOxygen(oxygen.Normalized);
            hud.ShowOxygen(true);
            yield return Story.Arrive("THE BURNT MOON", "No air. Watch your oxygen.");
            oxygen.Draining = true;

            yield return Dialogue.Play(
                (Speakers.ShipAI, "No atmosphere. Your suit is all you have. You can refill at the ship."),
                (Speakers.Pilot, "Old machines everywhere. Someone mined this place hollow, long before us."));

            hud.SetObjective("Start the ice drill");
            yield return Story.Use(drill);
            state.AddXp(20, "Drill running");

            // Coolant valves: a short logic puzzle.
            yield return Dialogue.Say(Speakers.ShipAI, "The refinery needs coolant, run from the coldest tank to the hottest. Get it wrong and it dumps pressure.");
            if (state.Check(Stat.Engineering, engineeringForSchematic))
            {
                hud.ShowToast($"ENGINEERING {engineeringForSchematic}: you read the pipe schematic");
                hud.SetObjective($"Open the valves in order: {string.Join(", then ", valveNames)}");
            }
            else
            {
                hud.SetObjective("Open the three coolant valves, coldest tank first");
            }
            foreach (var valve in valves) valve.Interacted += OnValve;
            Story.Unlock(valves);
            yield return new WaitUntil(() => valveStep >= valves.Length);
            state.AddXp(50, "Coolant routed");

            hud.SetObjective("Start the refinery");
            yield return Story.Use(refinery);
            yield return Dialogue.Say(Speakers.Pilot, "I'll hold the pressure lever. Go!");
            yield return new WaitForSeconds(1.5f);
            yield return Dialogue.Say(Speakers.ShipAI, "Refining complete. There is enough for one full tank and one half tank.");

            yield return SplitTheFuel();

            hud.SetObjective("Return to the ship and fly to the gate");
            yield return Story.Use(ship);
            oxygen.Draining = false;
            hud.SetObjective(null);
            yield return Dialogue.Play(
                (Speakers.Commander, "That gate brought me here. It can take us back."),
                (Speakers.Pilot, "And if it doesn't?"));
            yield return Story.Jump(SceneFlow.GateWorld);
        }

        IEnumerator SplitTheFuel()
        {
            var state = GameState.Instance;
            if (state.HasFlag("moon.refueled")) yield break; // scene was restarted after the fuel was made

            int choice = 0;
            yield return Dialogue.Choose(new[] { "Split it evenly.", "You take the full tank.", "I'm keeping the full tank." }, c => choice = c);
            state.SetFlag("moon.refueled");
            switch (choice)
            {
                case 0:
                    state.ChangeTrust(8);
                    state.ChangeFuel(4);
                    yield return Dialogue.Say(Speakers.Pilot, "Even. Fair enough.");
                    break;
                case 1:
                    state.SetFlag("moon.gave_fuel");
                    state.ChangeTrust(12);
                    state.ChangeFuel(3);
                    yield return Dialogue.Say(Speakers.Pilot, "You'd give me the full tank? ...Nobody on Planet X would believe an Earther did that.");
                    break;
                default:
                    state.SetFlag("moon.kept_fuel");
                    state.ChangeTrust(-12);
                    state.ChangeFuel(6);
                    yield return Dialogue.Say(Speakers.Pilot, "Of course. Earth first. I should have guessed.");
                    break;
            }
        }

        void OnValve(Interactable valve)
        {
            if (valveStep >= valves.Length) return;
            if (valve == valves[valveStep])
            {
                valveStep++;
                HUD.Instance.ShowToast($"Valve open ({valveStep}/{valves.Length})");
            }
            else
            {
                valveStep = 0;
                HUD.Instance.ShowToast("Pressure dumped. All valves closed. Start again.");
            }
        }
    }
}
