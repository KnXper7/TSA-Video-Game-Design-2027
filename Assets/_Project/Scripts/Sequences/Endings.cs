using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    public enum Ending { NewBeginning, EarthAboveAll, ArchitectsMistake, Lost }

    /// <summary>
    /// The four endings. Each plays as a series of title cards over black, then resets the game to the start.
    /// Replace these cards with full cutscenes later; the story logic only needs Endings.Play(ending).
    /// </summary>
    public static class Endings
    {
        public static bool Playing { get; private set; }

        // Statics survive between Play sessions when domain reload is off, so reset them on every start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Playing = false;

        static (string title, string[] cards, string meaning, Color color) Content(Ending ending) => ending switch
        {
            Ending.NewBeginning => ("A NEW BEGINNING", new[]
            {
                "The harvesters fell silent on every world at once.",
                "The gates stayed open: not to take, but to trade.",
                "Earth remained home. Planet X was no longer alone.",
                "Two pilots flew back through the last gate together.",
            }, "We didn't need a new home. We needed a better way to keep this one.", new Color(0.55f, 0.9f, 1f)),

            Ending.EarthAboveAll => ("EARTH ABOVE ALL", new[]
            {
                "The network answered to Earth, and Earth alone.",
                "The shortages ended. The cities lit up again.",
                "The gate to Planet X was locked from the inside.",
                "Somewhere, a harvester quietly added a new target.",
            }, "Every harvest has a cost. Someone else always pays it.", new Color(1f, 0.8f, 0.4f)),

            Ending.ArchitectsMistake => ("THE ARCHITECTS' MISTAKE", new[]
            {
                "The pilot stayed on the green world, under the wrong stars.",
                "Earth kept waiting for a signal that never came.",
                "Planet X ran dry, one season at a time.",
                "The harvesters kept working. Nobody told them to stop.",
            }, "Escaping a problem is not the same as solving it.", new Color(0.7f, 0.95f, 0.65f)),

            _ => ("LOST IN THE NETWORK", new[]
            {
                "The fuel ran out between one gate and the next.",
                "The ship drifted where no signal could reach it.",
                "\"Rocket, this is Flight. Do you read?\"",
                "Earth and Planet X never learned what was coming.",
            }, "The journey ended before it could change anything.", new Color(1f, 0.45f, 0.4f)),
        };

        public static IEnumerator Play(Ending ending)
        {
            if (Playing) yield break;
            Playing = true;

            var hud = HUD.Instance;
            var state = GameState.Instance;
            var (title, cards, meaning, color) = Content(ending);
            state.SetFlag("ending." + ending);

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player != null) player.CanMove = false;
            hud.SetObjective(null);
            hud.SetPrompt(null);

            yield return hud.FadeTo(1f, 2.5f, Color.black);
            yield return hud.ShowTitle(title, "", 2.5f, color);
            foreach (var card in cards)
                yield return hud.ShowTitle("", card, 3.2f);
            yield return hud.ShowTitle("", meaning, 4f);
            yield return hud.ShowTitle("THE GATEWAY", ending == Ending.Lost ? "Try again" : "The End", 3f);

            state.ResetAll();
            // Stay "playing" until the first scene is back, so chapter scripts waiting on us don't carry on.
            SceneManager.sceneLoaded += OnRestarted;
            SceneFlow.Load(SceneFlow.MissionControl, 0.1f);
        }

        static void OnRestarted(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnRestarted;
            Playing = false;
        }
    }
}
