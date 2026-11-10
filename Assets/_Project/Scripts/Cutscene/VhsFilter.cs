using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// Makes a camera look like 1990s home video: the camera draws into a small 4:3 picture, which is shown
    /// full-screen through the TSA/Vhs shader with a camcorder "PLAY" label and a date stamp on top.
    /// </summary>
    public class VhsFilter : MonoBehaviour
    {
        [SerializeField] Shader shader;
        [SerializeField] Vector2Int resolution = new(640, 480);

        Camera source;
        RenderTexture picture;
        GameObject overlay;
        Material material;

        public void Enable(Camera camera, string dateStamp)
        {
            if (overlay != null) return;
            source = camera;
            picture = new RenderTexture(resolution.x, resolution.y, 24) { name = "VHS Picture" };
            source.targetTexture = picture;

            overlay = new GameObject("VHS Overlay");
            overlay.transform.SetParent(transform, false);

            // Something still has to draw to the screen itself while the real camera draws to the tape picture.
            var blank = new GameObject("Blank Screen Camera").AddComponent<Camera>();
            blank.transform.SetParent(overlay.transform, false);
            blank.clearFlags = CameraClearFlags.SolidColor;
            blank.backgroundColor = Color.black;
            blank.cullingMask = 0;
            blank.depth = -100;

            var canvas = UIFactory.CreateCanvas("VHS Canvas", overlay.transform, 4);
            var black = UIFactory.CreateImage("Black", canvas.transform, Color.black);
            UIFactory.Stretch(black.rectTransform);

            var frame = UIFactory.CreateRect("Picture", canvas.transform);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = (float)resolution.x / resolution.y;
            frame.anchorMin = new Vector2(0.5f, 0f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.offsetMin = frame.offsetMax = Vector2.zero;

            var image = frame.gameObject.AddComponent<RawImage>();
            image.texture = picture;
            image.raycastTarget = false;
            material = new Material(shader);
            image.material = material;

            var ink = new Color(1f, 1f, 1f, 0.92f);
            var play = UIFactory.CreateText("Play", frame, "PLAY ▶", 44, TextAnchor.UpperLeft, ink);
            UIFactory.Place(play.rectTransform, new Vector2(0, 1), new Vector2(400, 60), new Vector2(60, -50));
            var speed = UIFactory.CreateText("Speed", frame, "SP", 44, TextAnchor.UpperRight, ink);
            UIFactory.Place(speed.rectTransform, new Vector2(1, 1), new Vector2(200, 60), new Vector2(-60, -50));
            var stamp = UIFactory.CreateText("Date", frame, dateStamp, 40, TextAnchor.LowerLeft, ink);
            UIFactory.Place(stamp.rectTransform, new Vector2(0, 0), new Vector2(900, 110), new Vector2(60, 60));
        }

        public void Disable()
        {
            if (overlay == null) return;
            source.targetTexture = null;
            Destroy(overlay);
            Destroy(material);
            picture.Release();
            Destroy(picture);
            overlay = null;
        }

        void OnDestroy()
        {
            if (overlay != null && source != null) source.targetTexture = null;
        }
    }
}
