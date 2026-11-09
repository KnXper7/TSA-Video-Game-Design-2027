using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    /// <summary>Scene names in play order, and a fade-out-then-load helper.</summary>
    public static class SceneFlow
    {
        public const string MissionControl = "01_MissionControl";
        public const string Cockpit = "02_Cockpit";
        public const string LandingSite = "03_LandingSite";

        static bool loading;

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
