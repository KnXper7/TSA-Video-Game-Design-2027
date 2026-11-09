using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The character sheet: level, XP and the four stats, with buttons to spend points earned by leveling up.
    /// Tab opens and closes it. The HUD adds this automatically in scenes that have a player.
    /// </summary>
    public class StatsPanel : MonoBehaviour
    {
        static readonly (Stat stat, string blurb)[] Rows =
        {
            (Stat.Engineering, "Machines and power. Opens technical shortcuts."),
            (Stat.Navigation, "Maps, glyphs and gates. Reveals hidden routes."),
            (Stat.Diplomacy, "Persuasion. Unlocks dialogue options."),
            (Stat.Survival, "Oxygen lasts longer. Endure hazards."),
        };

        Canvas canvas;
        Text header;
        Text reminder;
        readonly List<(Text value, Button plus)> rows = new();

        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

        void OnDisable() => InputLock.Release(this);

        void Awake()
        {
            Build();
            canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            var state = GameState.Instance;
            reminder.text = !IsOpen && state.UnspentPoints > 0 ? $"[TAB] {state.UnspentPoints} stat points to spend" : "";

            if (!GameInput.Stats.WasPressedThisFrame()) return;
            if (IsOpen) Close();
            // Only open during normal play: not while a puzzle panel or a dialogue choice has the cursor.
            else if (!InputLock.IsLocked && !Dialogue.IsPlaying) Open();
        }

        void Open()
        {
            canvas.gameObject.SetActive(true);
            AudioManager.Play(Sfx.Interact, 0.5f);
            InputLock.Push(this);
            Refresh();
        }

        void Close()
        {
            canvas.gameObject.SetActive(false);
            InputLock.Release(this);
        }

        void Spend(Stat stat)
        {
            if (GameState.Instance.SpendPoint(stat)) Refresh();
        }

        void Refresh()
        {
            var state = GameState.Instance;
            header.text = $"LEVEL {state.Level}     XP {state.Xp} / {state.XpToNextLevel}     POINTS TO SPEND: {state.UnspentPoints}";
            for (int i = 0; i < rows.Count; i++)
            {
                int value = state.Get(Rows[i].stat);
                rows[i].value.text = value.ToString();
                rows[i].plus.interactable = state.UnspentPoints > 0 && value < GameState.MaxStat;
            }
        }

        void Build()
        {
            var ink = new Color(0.85f, 0.95f, 1f);
            var accent = new Color(0.55f, 0.85f, 1f);

            // Reminder lives on its own canvas so it shows while the panel is closed.
            var reminderCanvas = UIFactory.CreateCanvas("Stats Reminder", transform, 4);
            reminder = UIFactory.CreateText("Reminder", reminderCanvas.transform, "", 22, TextAnchor.UpperRight, new Color(1f, 0.85f, 0.35f));
            UIFactory.Place(reminder.rectTransform, new Vector2(1, 1), new Vector2(600, 40), new Vector2(-40, -90));

            canvas = UIFactory.CreateCanvas("Stats Panel", transform, 6);
            var panel = UIFactory.CreateImage("Panel", canvas.transform, new Color(0.03f, 0.06f, 0.1f, 0.98f));
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000, 560), Vector2.zero);

            var title = UIFactory.CreateText("Title", panel.transform, "PILOT RECORD", 40, TextAnchor.UpperLeft, ink);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(900, 60), new Vector2(40, -30));
            header = UIFactory.CreateText("Header", panel.transform, "", 24, TextAnchor.UpperLeft, accent);
            UIFactory.Place(header.rectTransform, new Vector2(0, 1), new Vector2(920, 40), new Vector2(40, -90));

            for (int i = 0; i < Rows.Length; i++)
            {
                var (stat, blurb) = Rows[i];
                float y = -150 - i * 85;
                var label = UIFactory.CreateText("Name", panel.transform, stat.ToString().ToUpper(), 28, TextAnchor.MiddleLeft, ink);
                UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(240, 60), new Vector2(40, y));
                var value = UIFactory.CreateText("Value", panel.transform, "", 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
                UIFactory.Place(value.rectTransform, new Vector2(0, 1), new Vector2(60, 60), new Vector2(290, y));
                var plus = UIFactory.CreateButton("Plus", panel.transform, "+", 34, () => Spend(stat));
                UIFactory.Place((RectTransform)plus.transform, new Vector2(0, 1), new Vector2(60, 60), new Vector2(360, y));
                var note = UIFactory.CreateText("Blurb", panel.transform, blurb, 22, TextAnchor.MiddleLeft, new Color(0.7f, 0.8f, 0.9f));
                UIFactory.Place(note.rectTransform, new Vector2(0, 1), new Vector2(540, 60), new Vector2(440, y));
                rows.Add((value, plus));
            }

            var close = UIFactory.CreateText("Close", panel.transform, "[TAB] close", 22, TextAnchor.LowerRight, new Color(1, 1, 1, 0.6f));
            UIFactory.Place(close.rectTransform, new Vector2(1, 0), new Vector2(300, 40), new Vector2(-40, 24));
        }
    }
}
