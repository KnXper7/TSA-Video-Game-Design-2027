using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>Helpers shared by the chapter scripts: waiting on the player, fuel and gate travel, and common story checks.</summary>
    public static class Story
    {
        public const int TrustForBestEnding = 60;
        public const int TrustForSharedFuel = 50;

        /// <summary>Architect records the player has found so far.</summary>
        public static int Records => GameState.Instance.CountFlags("record.");

        /// <summary>Switches objects off until their step of the chapter comes up, so they can't be used out of order.</summary>
        public static void Lock(params Interactable[] interactables)
        {
            foreach (var interactable in interactables) interactable.enabled = false;
        }

        public static void Unlock(params Interactable[] interactables)
        {
            foreach (var interactable in interactables) interactable.enabled = true;
        }

        /// <summary>Unlocks the object and waits until the player uses it.</summary>
        public static IEnumerator Use(Interactable interactable)
        {
            interactable.enabled = true;
            interactable.ResetUse();
            bool used = false;
            interactable.Interacted += _ => used = true;
            yield return new WaitUntil(() => used);
        }

        public static IEnumerator Enter(TriggerZone zone)
        {
            bool entered = false;
            zone.Entered += () => entered = true;
            yield return new WaitUntil(() => entered);
        }

        /// <summary>
        /// Lets a chapter scene be played on its own from the editor: if the game did not come through the
        /// fuel moon, start with a normal amount of fuel instead of an empty tank.
        /// </summary>
        public static void EnsureMidGameDefaults()
        {
            var state = GameState.Instance;
            if (state.HasFlag("moon.refueled")) return;
            state.SetFlag("moon.refueled");
            state.ChangeFuel(4);
        }

        /// <summary>
        /// Burns one fuel cell. On an empty tank the other pilot shares fuel once if they trust you;
        /// otherwise the journey ends in the Lost ending. Check Endings.Playing afterwards.
        /// </summary>
        public static IEnumerator BurnFuel()
        {
            var state = GameState.Instance;
            if (state.Fuel > 0) state.ChangeFuel(-1);
            if (state.Fuel > 0) yield break;

            if (state.Trust >= TrustForSharedFuel && !state.HasFlag("pilot.shared_fuel"))
            {
                state.SetFlag("pilot.shared_fuel");
                yield return Dialogue.Play(
                    (Speakers.ShipAI, "Commander, we are out of fuel cells."),
                    (Speakers.Pilot, "Take two of mine. We get home together, or not at all."));
                state.ChangeFuel(2);
                HUD.Instance.ShowToast("The colony pilot shared 2 fuel cells");
            }
            else
            {
                yield return Endings.Play(Ending.Lost);
            }
        }

        /// <summary>Flies through a gate to the next scene, burning a fuel cell.</summary>
        public static IEnumerator Jump(string nextScene)
        {
            yield return BurnFuel();
            if (Endings.Playing) yield break;
            SceneFlow.Load(nextScene, 1.5f, Color.white);
        }

        /// <summary>Oxygen ran out: fade, explain, restart the current scene.</summary>
        public static IEnumerator OxygenFailure(FirstPersonController player)
        {
            player.CanMove = false;
            yield return Dialogue.Say(Speakers.ShipAI, "Commander! Oxygen critical. Commander, do you read?");
            yield return HUD.Instance.FadeTo(1f, 2f, Color.black);
            yield return HUD.Instance.ShowTitle("OXYGEN DEPLETED", "Restarting this area", 2f, new Color(1f, 0.4f, 0.35f));
            SceneFlow.Reload(0.1f);
        }

        /// <summary>The black-to-scene fade every chapter opens with.</summary>
        public static IEnumerator Arrive(string placeName, string subtitle)
        {
            var hud = HUD.Instance;
            yield return hud.ShowTitle(placeName, subtitle, 2f);
            yield return hud.FadeTo(0f, 2.5f, Color.black);
        }
    }
}
