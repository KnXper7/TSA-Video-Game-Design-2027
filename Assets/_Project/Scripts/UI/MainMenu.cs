using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The main menu: the title, Play and Options over the live space scene. Play fades the menu out and starts
    /// the opening cutscene in the same scene, so the black bars slide in and the rocket flies past without a cut.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] CutsceneDirector cutscene;
        [SerializeField] string title = "THE GATEWAY";

        CanvasGroup group;
        static readonly Color Accent = new Color(0.55f, 0.85f, 1f);
        static readonly Color Glass = new Color(0.04f, 0.07f, 0.11f, 0.72f);
        const float OptionsX = 600f, OptionsSlide = 46f;

        RectTransform optionsPanel;
        CanvasGroup optionsGroup;
        Coroutine optionsRoutine;
        bool optionsOpen;
        Button playButton;
        bool starting;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
            StartCoroutine(HUD.Instance.FadeTo(0f, 2.5f, Color.black));
        }

        void Update()
        {
            // The menu always needs a visible, free mouse.
            if (!starting && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Build()
        {
            var canvas = UIFactory.CreateCanvas("Main Menu", transform, 5);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var accent = new Color(0.55f, 0.85f, 1f);

            var titleText = UIFactory.CreateText("Title", canvas.transform, title, 78, TextAnchor.UpperLeft, Color.white);
            titleText.font = UIFactory.TitleFont;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1200, 110), new Vector2(110, -150));

            playButton = MenuButton(canvas.transform, "PLAY", 0, Play);
            MenuButton(canvas.transform, "OPTIONS", 1, () => ShowOptions(!optionsOpen));

            BuildOptions(canvas.transform);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        Button MenuButton(Transform parent, string label, int row, UnityEngine.Events.UnityAction action)
        {
            var button = ThemedButton(parent, label, $"0{row + 1}", action);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0, 1), new Vector2(470, 76), new Vector2(110, -330 - row * 92));
            return button;
        }

        /// <summary>The menu's button look: dark glass, hairlines top and bottom, an accent bar that grows on hover.</summary>
        static Button ThemedButton(Transform parent, string label, string tag, UnityEngine.Events.UnityAction action)
        {
            var accentColor = Accent;
            var fill = UIFactory.CreateImage(label, parent, Glass);
            fill.raycastTarget = true;
            var button = fill.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
            button.onClick.AddListener(action);

            // Thin lines top and bottom, an accent bar on the left that grows when hovered.
            foreach (float edge in new[] { 0f, 1f })
            {
                var line = UIFactory.CreateImage("Line", fill.transform, new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f));
                line.rectTransform.anchorMin = new Vector2(0, edge);
                line.rectTransform.anchorMax = new Vector2(1, edge);
                line.rectTransform.sizeDelta = new Vector2(0, 1.5f);
            }
            var accent = UIFactory.CreateImage("Accent", fill.transform, accentColor);
            accent.rectTransform.anchorMin = new Vector2(0, 0);
            accent.rectTransform.anchorMax = new Vector2(0, 1);
            accent.rectTransform.pivot = new Vector2(0, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(4, 0);

            var number = UIFactory.CreateText("Number", fill.transform, tag, 20, TextAnchor.MiddleLeft, new Color(accentColor.r, accentColor.g, accentColor.b, 0.75f));
            UIFactory.Place(number.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 30), new Vector2(26, 0));
            var text = UIFactory.CreateText("Label", fill.transform, label, 26, TextAnchor.MiddleLeft, Color.white);
            text.font = UIFactory.TitleFont;
            UIFactory.Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(330, 40), new Vector2(72, 0));
            var chevron = UIFactory.CreateText("Chevron", fill.transform, "›", 40, TextAnchor.MiddleRight, accentColor);
            UIFactory.Place(chevron.rectTransform, new Vector2(1, 0.5f), new Vector2(40, 50), new Vector2(-22, 4));

            var fx = fill.gameObject.AddComponent<MenuButtonFx>();
            fx.accent = accent.rectTransform;
            fx.label = text.rectTransform;
            fx.fill = fill;
            fx.text = text;
            fx.chevron = chevron;
            return button;
        }

        void BuildOptions(Transform parent)
        {
            var panel = UIFactory.CreateImage("Options", parent, new Color(0.04f, 0.07f, 0.11f, 0.86f));
            panel.raycastTarget = true;
            UIFactory.Place(panel.rectTransform, new Vector2(0, 1), new Vector2(760, 470), new Vector2(OptionsX, -330));
            optionsPanel = panel.rectTransform;
            optionsGroup = panel.gameObject.AddComponent<CanvasGroup>();

            // Same frame as the buttons: hairlines top and bottom, accent bar on the left.
            foreach (float edge in new[] { 0f, 1f })
            {
                var line = UIFactory.CreateImage("Line", panel.transform, new Color(Accent.r, Accent.g, Accent.b, 0.35f));
                line.rectTransform.anchorMin = new Vector2(0, edge);
                line.rectTransform.anchorMax = new Vector2(1, edge);
                line.rectTransform.sizeDelta = new Vector2(0, 1.5f);
            }
            var bar = UIFactory.CreateImage("Accent", panel.transform, Accent);
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(0, 1);
            bar.rectTransform.pivot = new Vector2(0, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(4, 0);

            var number = UIFactory.CreateText("Number", panel.transform, "02", 20, TextAnchor.MiddleLeft, new Color(Accent.r, Accent.g, Accent.b, 0.75f));
            UIFactory.Place(number.rectTransform, new Vector2(0, 1), new Vector2(40, 30), new Vector2(40, -36));
            var heading = UIFactory.CreateText("Heading", panel.transform, "OPTIONS", 26, TextAnchor.MiddleLeft, Color.white);
            heading.font = UIFactory.TitleFont;
            UIFactory.Place(heading.rectTransform, new Vector2(0, 1), new Vector2(500, 40), new Vector2(86, -30));
            var rule = UIFactory.CreateImage("Rule", panel.transform, new Color(Accent.r, Accent.g, Accent.b, 0.2f));
            UIFactory.Place(rule.rectTransform, new Vector2(0, 1), new Vector2(680, 1.5f), new Vector2(40, -86));

            var audio = AudioManager.Instance;
            Row(panel.transform, 0, "MASTER", audio.masterVolume, v => { audio.masterVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 1, "MUSIC", audio.musicVolume, v => { audio.musicVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 2, "EFFECTS", audio.sfxVolume, v => { audio.sfxVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 3, "VOICES", audio.voiceVolume, v => { audio.voiceVolume = v; audio.ApplyVolumes(); });

            var close = ThemedButton(panel.transform, "CLOSE", "‹", () => ShowOptions(false));
            UIFactory.Place((RectTransform)close.transform, new Vector2(1, 0), new Vector2(250, 58), new Vector2(-40, 30));

            optionsGroup.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        /// <summary>One volume row: label, a thin track with an accent fill, and the value as a percentage.</summary>
        static void Row(Transform parent, int row, string label, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var slider = UIFactory.CreateSlider(label, parent, label, value, onChanged);
            var holder = (RectTransform)slider.transform.parent;
            UIFactory.Place(holder, new Vector2(0, 1), new Vector2(680, 50), new Vector2(40, -112 - row * 64));

            var name = holder.Find("Label").GetComponent<Text>();
            name.fontSize = 22;
            name.color = new Color(0.82f, 0.88f, 0.95f);
            var track = (RectTransform)slider.transform;
            track.anchorMin = new Vector2(0.3f, 0.5f);
            track.anchorMax = new Vector2(0.86f, 0.5f);
            track.offsetMin = new Vector2(0, -2);
            track.offsetMax = new Vector2(0, 2);
            track.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.18f);
            var handle = slider.handleRect;
            handle.sizeDelta = new Vector2(8, 26);
            handle.GetComponent<Image>().color = Accent;
            // A taller invisible strip so the thin track is still easy to grab.
            var grab = UIFactory.CreateImage("Grab", track, new Color(0, 0, 0, 0));
            grab.raycastTarget = true;
            UIFactory.Stretch(grab.rectTransform, -6, -16, -6, -16);
            grab.transform.SetAsFirstSibling();

            var percent = UIFactory.CreateText("Value", holder, "", 20, TextAnchor.MiddleRight, Accent);
            percent.rectTransform.anchorMin = new Vector2(0.88f, 0);
            percent.rectTransform.anchorMax = new Vector2(1, 1);
            percent.rectTransform.offsetMin = percent.rectTransform.offsetMax = Vector2.zero;
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
            slider.onValueChanged.AddListener(v => percent.text = Mathf.RoundToInt(v * 100f) + "%");
        }

        void ShowOptions(bool show)
        {
            if (show == optionsOpen) return;
            optionsOpen = show;
            if (optionsRoutine != null) StopCoroutine(optionsRoutine);
            optionsRoutine = StartCoroutine(AnimateOptions(show));
        }

        /// <summary>Slides and fades the panel in from the buttons, and back out the same way.</summary>
        IEnumerator AnimateOptions(bool show)
        {
            optionsPanel.gameObject.SetActive(true);
            optionsGroup.interactable = show;
            optionsGroup.blocksRaycasts = show;
            float from = optionsGroup.alpha, to = show ? 1f : 0f;
            float seconds = show ? 0.28f : 0.2f;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                optionsGroup.alpha = k;
                optionsPanel.anchoredPosition = new Vector2(OptionsX - OptionsSlide * (1f - k), optionsPanel.anchoredPosition.y);
                yield return null;
            }
            optionsGroup.alpha = to;
            optionsPanel.anchoredPosition = new Vector2(OptionsX, optionsPanel.anchoredPosition.y);
            if (!show) optionsPanel.gameObject.SetActive(false);
            optionsRoutine = null;
        }

        void Play()
        {
            if (starting) return;
            starting = true;
            GameState.Instance.ResetAll();
            StartCoroutine(Begin());
        }

        IEnumerator Begin()
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            AudioManager.Play(Sfx.Stinger, 0.7f);
            for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.9f;
                yield return null;
            }
            group.gameObject.SetActive(false);
            cutscene.Play();
        }
    }
}
