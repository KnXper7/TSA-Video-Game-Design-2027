using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The cockpit power console. Opens a panel where the player moves power cells between systems.
    /// Engineering at or above the bonus threshold adds an extra cell (a stat-based shortcut).
    /// Goal checks live in the scene's sequence script, which listens to Grid.Changed.
    /// </summary>
    public class PowerRoutingPuzzle : MonoBehaviour
    {
        [SerializeField] List<PowerSystem> systems = new()
        {
            new PowerSystem { name = SystemLifeSupport, required = 2 },
            new PowerSystem { name = SystemSensors, required = 1 },
            new PowerSystem { name = SystemEngines, required = 2 },
            new PowerSystem { name = SystemComms, required = 1 },
        };
        [SerializeField] int baseCapacity = 4;
        [SerializeField] int engineeringBonusThreshold = 3;
        [SerializeField] int engineeringBonusCells = 1;
        [Tooltip("Hint the ship AI gives when the player asks. The sequence script can change it per phase.")]
        [SerializeField, TextArea] string hint = "";

        public const string SystemLifeSupport = "LIFE SUPPORT";
        public const string SystemSensors = "SENSORS";
        public const string SystemEngines = "ENGINES";
        public const string SystemComms = "COMMS";

        public PowerGrid Grid { get; private set; }
        public bool IsOpen { get; private set; }
        public bool HintUsed { get; private set; }
        public bool EngineeringBonus { get; private set; }

        public event Action Opened;
        public event Action Closed;

        FirstPersonController player;
        Interactor interactor;
        Canvas canvas;
        Text capacityText;
        Text goalText;
        readonly List<(Text pips, Text status, Button minus, Button plus)> rows = new();
        bool hintPlaying;
        int openedFrame;

        void Awake()
        {
            EngineeringBonus = GameState.Instance != null && GameState.Instance.Check(Stat.Engineering, engineeringBonusThreshold);
            Grid = new PowerGrid(systems, baseCapacity + (EngineeringBonus ? engineeringBonusCells : 0));
            Grid.Changed += Refresh;
            player = FindAnyObjectByType<FirstPersonController>();
            interactor = FindAnyObjectByType<Interactor>();
            BuildPanel();
            canvas.gameObject.SetActive(false);
        }

        public void SetGoal(string goal)
        {
            if (goalText != null) goalText.text = goal;
        }

        public void SetHint(string newHint) => hint = newHint;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openedFrame = Time.frameCount;
            canvas.gameObject.SetActive(true);
            if (player != null) player.CanLook = false;
            if (interactor != null) interactor.enabled = false;
            FirstPersonController.LockCursor(false);
            Refresh();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            canvas.gameObject.SetActive(false);
            if (player != null) player.CanLook = true;
            if (interactor != null) interactor.enabled = true;
            FirstPersonController.LockCursor(true);
            Closed?.Invoke();
        }

        void Update()
        {
            if (IsOpen && !hintPlaying && Time.frameCount > openedFrame && GameInput.Interact.WasPressedThisFrame()) Close();
        }

        void AskForHint()
        {
            if (hintPlaying || string.IsNullOrEmpty(hint)) return;
            HintUsed = true;
            StartCoroutine(PlayHint());
        }

        System.Collections.IEnumerator PlayHint()
        {
            hintPlaying = true;
            yield return Dialogue.Say(Speakers.ShipAI, hint);
            hintPlaying = false;
        }

        void Refresh()
        {
            capacityText.text = $"POWER CELLS  {Grid.Used} / {Grid.Capacity}" + (EngineeringBonus ? "   (ENGINEERING: BUS OVERCLOCKED +1)" : "");
            for (int i = 0; i < rows.Count; i++)
            {
                var system = Grid.Systems[i];
                var row = rows[i];
                row.pips.text = new string('■', system.allocated) + new string('□', Mathf.Max(0, system.required - system.allocated));
                row.status.text = system.Online ? "ONLINE" : $"NEEDS {system.required}";
                row.status.color = system.Online ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.55f, 0.4f);
                row.minus.interactable = Grid.CanRemove(i);
                row.plus.interactable = Grid.CanAdd(i);
            }
        }

        void BuildPanel()
        {
            canvas = UIFactory.CreateCanvas("Power Console UI", transform, 5);
            var panel = UIFactory.CreateImage("Panel", canvas.transform, new Color(0.03f, 0.06f, 0.1f, 0.98f));
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000, 640), Vector2.zero);
            var ink = new Color(0.85f, 0.95f, 1f);

            var title = UIFactory.CreateText("Title", panel.transform, "POWER DISTRIBUTION", 40, TextAnchor.UpperLeft, ink);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(900, 60), new Vector2(40, -30));

            capacityText = UIFactory.CreateText("Capacity", panel.transform, "", 24, TextAnchor.UpperLeft, new Color(0.55f, 0.85f, 1f));
            UIFactory.Place(capacityText.rectTransform, new Vector2(0, 1), new Vector2(920, 40), new Vector2(40, -90));

            for (int i = 0; i < Grid.Systems.Count; i++)
            {
                int index = i;
                float y = -150 - i * 80;
                var label = UIFactory.CreateText("Name", panel.transform, Grid.Systems[i].name, 28, TextAnchor.MiddleLeft, ink);
                UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(260, 60), new Vector2(40, y));

                var minus = UIFactory.CreateButton("Minus", panel.transform, "−", 34, () => Grid.Remove(index));
                UIFactory.Place((RectTransform)minus.transform, new Vector2(0, 1), new Vector2(60, 60), new Vector2(310, y));

                var pips = UIFactory.CreateText("Cells", panel.transform, "", 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
                UIFactory.Place(pips.rectTransform, new Vector2(0, 1), new Vector2(180, 60), new Vector2(380, y));

                var plus = UIFactory.CreateButton("Plus", panel.transform, "+", 34, () => Grid.Add(index));
                UIFactory.Place((RectTransform)plus.transform, new Vector2(0, 1), new Vector2(60, 60), new Vector2(570, y));

                var status = UIFactory.CreateText("Status", panel.transform, "", 24, TextAnchor.MiddleLeft, ink);
                UIFactory.Place(status.rectTransform, new Vector2(0, 1), new Vector2(300, 60), new Vector2(660, y));

                rows.Add((pips, status, minus, plus));
            }

            goalText = UIFactory.CreateText("Goal", panel.transform, "", 24, TextAnchor.UpperLeft, ink);
            UIFactory.Place(goalText.rectTransform, new Vector2(0, 0), new Vector2(620, 90), new Vector2(40, 30));

            var hintButton = UIFactory.CreateButton("Hint", panel.transform, "ASK SHIP AI", 22, AskForHint);
            UIFactory.Place((RectTransform)hintButton.transform, new Vector2(1, 0), new Vector2(170, 56), new Vector2(-210, 30));

            var closeButton = UIFactory.CreateButton("Close", panel.transform, "CLOSE [E]", 22, Close);
            UIFactory.Place((RectTransform)closeButton.transform, new Vector2(1, 0), new Vector2(150, 56), new Vector2(-40, 30));
        }
    }
}
