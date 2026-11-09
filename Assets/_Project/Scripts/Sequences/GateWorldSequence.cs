using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Act II, chapter 5: the gate sent the pilots to the wrong world, and they learn it is one of a whole network.
    /// Coordinate tablets in the ruins each reveal one ring of the gate address (Navigation 3+ reads the first ring
    /// straight off the gate). The player can also guess, but every wrong alignment burns a fuel cell.
    /// </summary>
    public class GateWorldSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] Interactable[] tablets;
        [SerializeField] Interactable gateConsole;
        [SerializeField] GlyphPuzzle puzzle;
        [SerializeField] int navigationForFreeClue = 3;

        static readonly (string speaker, string text)[] TabletLines =
        {
            (Speakers.Commander, "A carving of ships passing through rings. They built these to travel between worlds."),
            (Speakers.Commander, "The same three symbols repeat beside the picture of a gate. It's an address."),
            (Speakers.Commander, "This one shows a planet with pieces being lifted off it. That is... not travel."),
        };

        int found;
        int wrongAlignments;

        void Start()
        {
            Story.EnsureMidGameDefaults();
            Story.Lock(tablets);
            Story.Lock(gateConsole);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            yield return Story.Arrive("UNKNOWN WORLD", "This is not home.");
            yield return Dialogue.Play(
                (Speakers.Pilot, "That's not Planet X."),
                (Speakers.Commander, "It's not Earth either."),
                (Speakers.ShipAI, "Good news: the gate works. Bad news: it is one of many. I am detecting dozens of linked gates."),
                (Speakers.Commander, "A network."),
                (Speakers.ShipAI, "Each gate needs an address set in these symbols. Set it wrong and we burn fuel going nowhere."));
            state.SetFlag("gate.network_known");
            state.AddXp(40, "Discovery: the gate network");

            if (state.Check(Stat.Navigation, navigationForFreeClue))
            {
                puzzle.RevealHint(0);
                hud.ShowToast($"NAVIGATION {navigationForFreeClue}: you read the first ring off the gate");
            }

            gateConsole.Interacted += _ => puzzle.Open();
            puzzle.Failed += () => StartCoroutine(OnWrongAlignment());
            for (int i = 0; i < tablets.Length; i++)
            {
                int index = i;
                tablets[i].Interacted += _ => StartCoroutine(OnTablet(index));
            }
            Story.Unlock(tablets);
            Story.Unlock(gateConsole);
            UpdateObjective();

            yield return new WaitUntil(() => puzzle.IsSolved);
            if (Endings.Playing) yield break;

            hud.SetObjective(null);
            state.SetFlag("gate.first_alignment");
            state.AddXp(60, "Gate aligned");
            if (wrongAlignments == 0) state.AddXp(25, "Aligned on the first try");
            state.ChangeTrust(3);
            yield return Dialogue.Play(
                (Speakers.Pilot, "You're learning their machine faster than I am."),
                (Speakers.ShipAI, "The address leads to a world with heavy machine activity. Something there is still working."));
            yield return Story.Jump(SceneFlow.Harvester);
        }

        IEnumerator OnTablet(int index)
        {
            found++;
            puzzle.RevealHint(index);
            GameState.Instance.SetFlag("tablet." + index);
            GameState.Instance.AddXp(25, "Coordinate tablet");
            UpdateObjective();
            yield return Dialogue.Play(TabletLines[Mathf.Min(index, TabletLines.Length - 1)]);
        }

        IEnumerator OnWrongAlignment()
        {
            wrongAlignments++;
            puzzle.Close();
            yield return Story.BurnFuel();
            if (!Endings.Playing)
                yield return Dialogue.Say(Speakers.ShipAI, "That address goes nowhere. The tablets in the ruins may hold the right symbols.");
        }

        void UpdateObjective()
        {
            HUD.Instance.SetObjective(found < tablets.Length
                ? $"Find coordinate tablets in the ruins ({found}/{tablets.Length}), then align the gate"
                : "Align the gate at its console");
        }
    }
}
