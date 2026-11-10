using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    /// <summary>Scene names in play order, and a fade-out-then-load helper.</summary>
    public static class SceneFlow
    {
        /// <summary>Main menu and the opening cutscene (they share one scene so Play flows straight into it).</summary>
        public const string MissionControl = "01_MissionControl";
        public const string Memory = "02_Memory";
        public const string Adrift = "03_Adrift";
        public const string Cockpit = "04_Cockpit";
        public const string LandingSite = "05_LandingSite";
        public const string FuelMoon = "06_FuelMoon";
        public const string GateWorld = "07_GateWorld";
        public const string Harvester = "08_Harvester";
        public const string Control = "09_Control";

        /// <summary>Every scene in play order.</summary>
        public static readonly string[] All = { MissionControl, Memory, Adrift, Cockpit, LandingSite, FuelMoon, GateWorld, Harvester, Control };

        /// <summary>Chapter names shown on the menu's Save and Load buttons.</summary>
        public static string DisplayName(string scene) => scene switch
        {
            Memory => "Launch Day",
            Adrift => "Adrift",
            Cockpit => "The Cockpit",
            LandingSite => "The Landing Site",
            FuelMoon => "The Fuel Moon",
            GateWorld => "The Gate World",
            Harvester => "The Harvester",
            Control => "The Control Core",
            _ => "The Beginning",
        };

        static bool loading;

        // Statics survive between Play sessions when domain reload is off, so reset them on every start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => loading = false;

        /// <summary>Fades to black (or the given colour), then loads the scene. The next scene fades itself in.</summary>
        public static void Load(string sceneName, float fadeDuration = 1f, Color? fadeColor = null)
        {
            if (loading || GameState.Instance == null) return;
            GameState.Instance.StartCoroutine(LoadRoutine(sceneName, fadeDuration, fadeColor));
        }

        public static void Reload(float fadeDuration = 1f) => Load(SceneManager.GetActiveScene().name, fadeDuration);

        static IEnumerator LoadRoutine(string sceneName, float fadeDuration, Color? fadeColor)
        {
            loading = true;
            if (HUD.Instance != null)
                yield return HUD.Instance.FadeTo(1f, fadeDuration, fadeColor ?? Color.black);
            yield return SceneManager.LoadSceneAsync(sceneName);
            loading = false;
        }
    }
}
