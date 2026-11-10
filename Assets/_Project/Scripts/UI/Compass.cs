using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The compass line across the top of the screen. Every active CompassMarker slides along it according to
    /// where it is relative to the way the player is facing; at the ends an arrow says "keep turning".
    /// The HUD adds this automatically in scenes that have a player.
    /// </summary>
    public class Compass : MonoBehaviour
    {
        const float HalfWidth = 430f;
        const float FieldOfView = 70f;       // degrees either side of straight ahead that the line covers

        class Pip
        {
            public RectTransform root;
            public Image diamond;
            public Text distance, arrow;
        }

        RectTransform bar;
        CanvasGroup group;
        readonly List<Pip> pips = new();
        static readonly Color Line = new(0.6f, 0.9f, 1f, 0.9f);

        void Awake()
        {
            var canvas = UIFactory.CreateCanvas("Compass", transform, 8);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;

            bar = UIFactory.CreateRect("Bar", canvas.transform);
            UIFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(HalfWidth * 2f, 60f), new Vector2(0f, -70f));
            var line = UIFactory.CreateImage("Line", bar, Line);
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            Diamond("End L", bar, new Vector2(-HalfWidth, 0f), 9f, Line, false);
            Diamond("End R", bar, new Vector2(HalfWidth, 0f), 9f, Line, false);
            Diamond("Centre", bar, Vector2.zero, 22f, Line, true);
            Diamond("Centre Dot", bar, Vector2.zero, 8f, Line, false);
            for (int tick = -3; tick <= 3; tick++)
            {
                if (tick == 0) continue;
                var mark = UIFactory.CreateImage("Tick", bar, new Color(Line.r, Line.g, Line.b, 0.5f));
                mark.rectTransform.sizeDelta = new Vector2(2f, 8f);
                mark.rectTransform.anchoredPosition = new Vector2(tick * HalfWidth / 4f, 0f);
            }
        }

        static Image Diamond(string name, Transform parent, Vector2 position, float size, Color color, bool hollow)
        {
            var image = UIFactory.CreateImage(name, parent, color);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            if (hollow)
            {
                var hole = UIFactory.CreateImage("Hole", image.transform, new Color(0.02f, 0.05f, 0.08f, 0.85f));
                UIFactory.Stretch(hole.rectTransform, 3f, 3f, 3f, 3f);
            }
            return image;
        }

        Pip NewPip()
        {
            var root = UIFactory.CreateRect("Marker", bar);
            root.sizeDelta = new Vector2(40f, 40f);
            var pip = new Pip { root = root };
            pip.diamond = Diamond("Diamond", root, Vector2.zero, 20f, Color.white, false);
            Diamond("Core", pip.diamond.transform, Vector2.zero, 8f, new Color(0.05f, 0.05f, 0.08f), false);
            pip.distance = UIFactory.CreateText("Distance", root, "", 24, TextAnchor.LowerCenter, Color.white);
            pip.distance.rectTransform.sizeDelta = new Vector2(200f, 30f);
            pip.distance.rectTransform.anchoredPosition = new Vector2(0f, 32f);
            pip.distance.horizontalOverflow = HorizontalWrapMode.Overflow;
            pip.arrow = UIFactory.CreateText("Arrow", root, "", 34, TextAnchor.MiddleCenter, Line);
            pip.arrow.rectTransform.sizeDelta = new Vector2(40f, 40f);
            pips.Add(pip);
            return pip;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var markers = CompassMarker.Active;
            bool show = cam != null && markers.Count > 0 && !InputLock.IsLocked;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (!show) return;

            for (int i = 0; i < Mathf.Max(markers.Count, pips.Count); i++)
            {
                var pip = i < pips.Count ? pips[i] : NewPip();
                bool used = i < markers.Count;
                pip.root.gameObject.SetActive(used);
                if (!used) continue;

                // Angle between where the camera faces and where the marker is, measured in the camera's own frame
                // so it also works floating in space.
                var to = cam.transform.InverseTransformPoint(markers[i].transform.position);
                float angle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float k = Mathf.Clamp(angle / FieldOfView, -1f, 1f);
                bool offEnd = Mathf.Abs(angle) > FieldOfView;
                pip.root.anchoredPosition = new Vector2(k * HalfWidth, 0f);
                pip.diamond.color = markers[i].Color;
                pip.distance.text = $"{to.magnitude:0} m";
                pip.distance.color = markers[i].Color;
                pip.arrow.text = offEnd ? (angle > 0f ? "›" : "‹") : "";
                pip.arrow.rectTransform.anchoredPosition = new Vector2(angle > 0f ? 30f : -30f, 2f);
            }
        }
    }
}
