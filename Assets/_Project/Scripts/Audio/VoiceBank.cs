using System.Text;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Finds the recorded clip for a line of dialogue. Clips live in Assets/_Project/Resources/Voice/ and are
    /// named by a short code made from the speaker and the exact text (see Tools/voice_lines.csv for the list).
    /// The placeholder clips come from Tools/generate_voices.py; to use a real recording, save it over the
    /// placeholder with the same file name. If a line's text changes, its code changes too: re-run the script.
    /// </summary>
    public static class VoiceBank
    {
        /// <summary>Speakers heard over a radio link get a filtered sound.</summary>
        public static bool IsRadio(string speaker) => speaker == Speakers.FlightDirector || speaker == Speakers.Controller;

        public static AudioClip Get(string speaker, string text) => Resources.Load<AudioClip>("Voice/" + Key(speaker, text));

        /// <summary>FNV-1a hash of "speaker|text" as 8 hex digits. Must match Tools/generate_voices.py.</summary>
        public static string Key(string speaker, string text)
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(speaker + "|" + text))
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }
}
