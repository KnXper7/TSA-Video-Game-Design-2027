using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// Makes a camera look like 1990s home video being watched on an old television: the camera draws into a small
    /// 4:3 picture, and the TSA/Vhs shader shows it on a curved CRT screen in a dark room, with a camcorder
    /// "PLAY" label and a date stamp on the glass.
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

            // The whole frame is one image: the shader draws the television, its curved screen and the dark room.
            var image = UIFactory.CreateRect("Television", canvas.transform).gameObject.AddComponent<RawImage>();
            UIFactory.Stretch(image.rectTransform);
            image.texture = picture;
            image.raycastTarget = false;
            material = new Material(shader);
            material.SetFloat("_Aspect", (float)Screen.width / Screen.height);
            image.material = material;

            // Camcorder labels sit on the glass, so they live in a 4:3 box the size of the TV's picture.
            var frame = UIFactory.CreateRect("On-screen Labels", canvas.transform);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1080f * 0.78f * 4f / 3f, 1080f * 0.78f);
            frame.anchoredPosition = new Vector2(0f, 1080f * 0.03f);

            var ink = new Color(1f, 1f, 1f, 0.92f);
            var play = UIFactory.CreateText("Play", frame, "PLAY ▶", 44, TextAnchor.UpperLeft, ink);
            UIFactory.Place(play.rectTransform, new Vector2(0, 1), new Vector2(400, 60), new Vector2(80, -70));
            var speed = UIFactory.CreateText("Speed", frame, "SP", 44, TextAnchor.UpperRight, ink);
            UIFactory.Place(speed.rectTransform, new Vector2(1, 1), new Vector2(200, 60), new Vector2(-80, -70));
            var stamp = UIFactory.CreateText("Date", frame, dateStamp, 40, TextAnchor.LowerLeft, ink);
            UIFactory.Place(stamp.rectTransform, new Vector2(0, 0), new Vector2(900, 110), new Vector2(80, 80));
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
