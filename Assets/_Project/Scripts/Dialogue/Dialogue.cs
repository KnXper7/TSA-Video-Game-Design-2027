using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TSAGame
{
    /// <summary>Speaker labels shown above subtitles. Change them here once character names are final.</summary>
    public static class Speakers
    {
        public const string Commander = "COMMANDER";
        public const string ShipAI = "SHIP AI";
        public const string Pilot = "COLONY PILOT";
        public const string Network = "NETWORK";
        public const string FlightDirector = "FLIGHT DIRECTOR";
        public const string Controller = "CONTROLLER";
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)] public string text;
        [Tooltip("Seconds the line stays up after it finishes typing. 0 = automatic, based on length.")]
        public float hold;

        public DialogueLine() { }

        public DialogueLine(string speaker, string text, float hold = 0f)
        {
            this.speaker = speaker;
            this.text = text;
            this.hold = hold;
        }

        public float HoldTime => hold > 0f ? hold : 1.2f + text.Length * 0.035f;
    }

    /// <summary>
    /// Plays subtitled lines through the HUD with a typewriter effect. Interact finishes a line early.
    /// Use from any coroutine: <c>yield return Dialogue.Play(lines);</c>
    /// </summary>
    public static class Dialogue
    {
        const float CharsPerSecond = 45f;

        public static bool IsPlaying { get; private set; }

        /// <summary>Text speed. 1 is normal; higher is faster (a settings menu or a test run can raise it).</summary>
        public static float SpeedMultiplier { get; set; } = 1f;

        // Statics survive between Play sessions when domain reload is off, so reset them on every start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPlaying = false;
            SpeedMultiplier = 1f;
        }

        public static IEnumerator Play(IEnumerable<DialogueLine> lines)
        {
            foreach (var line in lines)
                yield return Say(line);
        }

        /// <summary>Short form for scripts: <c>yield return Dialogue.Play((Speakers.ShipAI, "..."), ...);</c></summary>
        public static IEnumerator Play(params (string speaker, string text)[] lines)
        {
            foreach (var (speaker, text) in lines)
                yield return Say(speaker, text);
        }

        public static IEnumerator Say(string speaker, string text, float hold = 0f) => Say(new DialogueLine(speaker, text, hold));

        public static IEnumerator Say(DialogueLine line)
        {
            var hud = HUD.Instance;
            if (hud == null || line == null || string.IsNullOrEmpty(line.text)) yield break;

            IsPlaying = true;

            // Typewriter
            float shown = 0f;
            while (shown < line.text.Length)
            {
                shown += CharsPerSecond * SpeedMultiplier * Time.unscaledDeltaTime;
                hud.ShowSubtitle(line.speaker, line.text.Substring(0, Mathf.Min(line.text.Length, (int)shown)));
                yield return null;
                if (GameInput.Interact.WasPressedThisFrame()) break;
            }
            hud.ShowSubtitle(line.speaker, line.text);
            yield return null; // so the press that finished typing doesn't also skip the hold

            for (float t = 0; t < line.HoldTime / SpeedMultiplier; t += Time.unscaledDeltaTime)
            {
                if (GameInput.Interact.WasPressedThisFrame()) break;
                yield return null;
            }

            hud.HideSubtitle();
            IsPlaying = false;
        }

        /// <summary>Call after stopping a coroutine that was playing lines (for example when a cutscene is skipped).</summary>
        public static void Stop()
        {
            IsPlaying = false;
            if (HUD.Instance != null) HUD.Instance.HideSubtitle();
        }

        /// <summary>Shows a branching choice and reports the chosen index (0-based).</summary>
        public static IEnumerator Choose(IList<string> options, Action<int> onChosen)
        {
            var hud = HUD.Instance;
            if (hud == null) { onChosen?.Invoke(0); yield break; }
            IsPlaying = true;
            yield return hud.Choose(options, onChosen);
            IsPlaying = false;
        }
    }
}
