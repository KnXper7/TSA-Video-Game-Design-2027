using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The gate alignment console. The player sets each dial to a glyph and presses ALIGN.
    /// Clues found in the world reveal dials one at a time (RevealHint). A wrong alignment raises Failed,
    /// which the chapter script uses to burn a fuel cell.
    /// </summary>
    public class GlyphPuzzle : MonoBehaviour
    {
        public static readonly string[] Glyphs = { "▲", "●", "■", "◆", "★" };

        [Tooltip("The correct glyph index for each dial.")]
        [SerializeField] int[] target = { 3, 0, 4 };
        [SerializeField] string title = "GATE ALIGNMENT";

        public event Action Solved;
        public event Action Failed;

        public bool IsOpen { get; private set; }
        public bool IsSolved { get; private set; }
        public int DialCount => target.Length;

        int[] current;
        bool[] revealed;
        FirstPersonController player;
        Interactor interactor;
        Canvas canvas;
        Text status;
        readonly List<(Text glyph, Text hint)> dials = new();
        int openedFrame;

        void Awake()
        {
            current = new int[target.Length];
            revealed = new bool[target.Length];
            player = FindAnyObjectByType<FirstPersonController>();
            interactor = FindAnyObjectByType<Interactor>();
            Build();
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Shows the correct glyph under a dial (a clue the player found).</summary>
        public void RevealHint(int dial)
        {
            if (dial < 0 || dial >= revealed.Length) return;
            revealed[dial] = true;
            Refresh();
        }

        public void RevealAll()
        {
            for (int i = 0; i < revealed.Length; i++) revealed[i] = true;
            Refresh();
        }

        public void Open()
        {
            if (IsOpen || IsSolved) return;
            IsOpen = true;
            openedFrame = Time.frameCount;
            canvas.gameObject.SetActive(true);
            if (player != null) player.CanLook = false;
            if (interactor != null) interactor.enabled = false;
            FirstPersonController.LockCursor(false);
            status.text = "Set each ring, then align the gate. A wrong alignment burns a fuel cell.";
            status.color = new Color(0.85f, 0.95f, 1f);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            canvas.gameObject.SetActive(false);
            if (player != null) player.CanLook = true;
            if (interactor != null) interactor.enabled = true;
            FirstPersonController.LockCursor(true);
        }

        void Update()
        {
            if (IsOpen && Time.frameCount > openedFrame && !Dialogue.IsPlaying && GameInput.Interact.WasPressedThisFrame()) Close();
        }

        void Turn(int dial, int step)
        {
            current[dial] = (current[dial] + step + Glyphs.Length) % Glyphs.Length;
            Refresh();
        }

        /// <summary>Sets a dial directly (used by tests and scripted solutions).</summary>
        public void SetDial(int dial, int glyph)
        {
            current[dial] = glyph;
            Refresh();
        }

        public void Align()
        {
            for (int i = 0; i < target.Length; i++)
            {
                if (current[i] == target[i]) continue;
                status.text = "MISALIGNED. The gate burned a fuel cell and went dark.";
                status.color = new Color(1f, 0.45f, 0.35f);
                Failed?.Invoke();
                return;
            }
            IsSolved = true;
            Close();
            Solved?.Invoke();
        }

        void Refresh()
        {
            for (int i = 0; i < dials.Count; i++)
            {
                dials[i].glyph.text = Glyphs[current[i]];
                dials[i].hint.text = revealed[i] ? $"clue: {Glyphs[target[i]]}" : "clue: ?";
            }
        }

        void Build()
        {
            canvas = UIFactory.CreateCanvas("Gate Console UI", transform, 5);
            var panel = UIFactory.CreateImage("Panel", canvas.transform, new Color(0.03f, 0.06f, 0.1f, 0.98f));
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000, 620), Vector2.zero);
            var ink = new Color(0.85f, 0.95f, 1f);

            var heading = UIFactory.CreateText("Title", panel.transform, title, 40, TextAnchor.UpperLeft, ink);
            heading.fontStyle = FontStyle.Bold;
            UIFactory.Place(heading.rectTransform, new Vector2(0, 1), new Vector2(900, 60), new Vector2(40, -30));

            float spacing = 240f;
            float startX = -(target.Length - 1) * spacing / 2f;
            for (int i = 0; i < target.Length; i++)
            {
                int dial = i;
                float x = startX + i * spacing;
                var up = UIFactory.CreateButton("Up", panel.transform, "▲", 26, () => Turn(dial, 1));
                UIFactory.Place((RectTransform)up.transform, new Vector2(0.5f, 0.5f), new Vector2(120, 50), new Vector2(x, 150));
                var glyph = UIFactory.CreateText("Glyph", panel.transform, "", 110, TextAnchor.MiddleCenter, new Color(0.45f, 0.9f, 1f));
                UIFactory.Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 160), new Vector2(x, 30));
                var down = UIFactory.CreateButton("Down", panel.transform, "▼", 26, () => Turn(dial, -1));
                UIFactory.Place((RectTransform)down.transform, new Vector2(0.5f, 0.5f), new Vector2(120, 50), new Vector2(x, -90));
                var hint = UIFactory.CreateText("Hint", panel.transform, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
                UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 40), new Vector2(x, -150));
                dials.Add((glyph, hint));
            }

            status = UIFactory.CreateText("Status", panel.transform, "", 22, TextAnchor.UpperLeft, ink);
            UIFactory.Place(status.rectTransform, new Vector2(0, 0), new Vector2(520, 70), new Vector2(40, 30));

            var align = UIFactory.CreateButton("Align", panel.transform, "ALIGN GATE", 24, Align);
            UIFactory.Place((RectTransform)align.transform, new Vector2(1, 0), new Vector2(190, 56), new Vector2(-210, 30));
            var close = UIFactory.CreateButton("Close", panel.transform, "CLOSE [E]", 22, Close);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1, 0), new Vector2(150, 56), new Vector2(-40, 30));
        }
    }
}
