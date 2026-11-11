using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// A station light on the practice range: amber while its target is still standing, green once it has been hit.
    /// The same idea as the indicator strips in any good puzzle room: the room itself shows what is done.
    /// </summary>
    public class RangeLamp : MonoBehaviour
    {
        [SerializeField] BallSurface watch;
        [SerializeField] Renderer lamp;
        [SerializeField] Light glow;
        [SerializeField] Color waiting = new Color(1f, 0.55f, 0.1f);
        [SerializeField] Color done = new Color(0.25f, 1f, 0.45f);

        MaterialPropertyBlock block;
        bool shownDone;

        void Start() => Show(waiting);

        void Update()
        {
            if (shownDone || watch == null || !watch.WasHit) return;
            shownDone = true;
            Show(done);
        }

        void Show(Color colour)
        {
            block ??= new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            block.SetColor("_EmissionColor", colour * 3f);
            block.SetColor("_Color", colour);
            if (lamp != null) lamp.SetPropertyBlock(block);
            if (glow != null) glow.color = colour;
        }
    }
}
