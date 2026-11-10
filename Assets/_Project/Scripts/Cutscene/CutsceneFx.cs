using UnityEngine;

namespace TSAGame
{
    /// <summary>Small effects a cutscene shot can trigger from its On Start event.</summary>
    public class CutsceneFx : MonoBehaviour
    {
        /// <summary>The screen goes white and clears over a second.</summary>
        public void FlashWhite()
        {
            var hud = HUD.Instance;
            if (hud == null) return;
            AudioManager.Play(Sfx.Explosion, 0.8f);
            hud.SetFade(1f, Color.white);
            hud.StartCoroutine(hud.FadeTo(0f, 1.1f, Color.white));
        }
    }
}
