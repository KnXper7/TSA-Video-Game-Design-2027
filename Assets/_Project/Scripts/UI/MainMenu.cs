using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// The main menu: Play, Options, Save and Load over the live space scene. Play fades the menu out and starts
    /// the opening cutscene in the same scene, so the black bars slide in and the rocket flies past without a cut.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] CutsceneDirector cutscene;
        [SerializeField] string title = "THE GATEWAY";
        [SerializeField] string tagline = "A deep space story";

        CanvasGroup group;
        GameObject optionsPanel;
        Text status;
        Button playButton;
        bool starting;

        void Start()
        {
            Build();
            StartCoroutine(HUD.Instance.FadeTo(0f, 2.5f, Color.black));
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
            var tag = UIFactory.CreateText("Tagline", canvas.transform, tagline.ToUpper(), 30, TextAnchor.UpperLeft, accent);
            UIFactory.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(900, 44), new Vector2(116, -262));

            playButton = MenuButton(canvas.transform, "PLAY", 0, Play);
            MenuButton(canvas.transform, "OPTIONS", 1, () => optionsPanel.SetActive(!optionsPanel.activeSelf));
            MenuButton(canvas.transform, "SAVE", 2, Save);
            MenuButton(canvas.transform, "LOAD", 3, Load);

            status = UIFactory.CreateText("Status", canvas.transform, "", 26, TextAnchor.UpperLeft, accent);
            UIFactory.Place(status.rectTransform, new Vector2(0, 1), new Vector2(620, 80), new Vector2(116, -770));
            string chapter = SaveSystem.SavedChapter;
            if (chapter != null) status.text = $"Saved game: {chapter}";

            BuildOptions(canvas.transform);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        Button MenuButton(Transform parent, string label, int row, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.CreateButton(label, parent, label, 34, action);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0, 1), new Vector2(420, 78), new Vector2(110, -380 - row * 94));
            var text = button.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(34, 4);
            return button;
        }

        void BuildOptions(Transform parent)
        {
            var panel = UIFactory.CreateImage("Options", parent, new Color(0.03f, 0.05f, 0.09f, 0.94f));
            panel.raycastTarget = true;
            UIFactory.Place(panel.rectTransform, new Vector2(0, 1), new Vector2(760, 520), new Vector2(580, -380));
            optionsPanel = panel.gameObject;

            var heading = UIFactory.CreateText("Heading", panel.transform, "OPTIONS", 34, TextAnchor.UpperLeft, new Color(0.55f, 0.85f, 1f));
            UIFactory.Place(heading.rectTransform, new Vector2(0, 1), new Vector2(600, 50), new Vector2(40, -30));

            var audio = AudioManager.Instance;
            Row(panel.transform, 0, "Master volume", audio.masterVolume, v => { audio.masterVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 1, "Music", audio.musicVolume, v => { audio.musicVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 2, "Sound effects", audio.sfxVolume, v => { audio.sfxVolume = v; audio.ApplyVolumes(); });
            Row(panel.transform, 3, "Voices", audio.voiceVolume, v => { audio.voiceVolume = v; audio.ApplyVolumes(); });

            var close = UIFactory.CreateButton("Close", panel.transform, "CLOSE", 28, () => optionsPanel.SetActive(false));
            UIFactory.Place((RectTransform)close.transform, new Vector2(1, 0), new Vector2(200, 60), new Vector2(-40, 34));
            optionsPanel.SetActive(false);
        }

        static void Row(Transform parent, int row, string label, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var slider = UIFactory.CreateSlider(label, parent, label, value, onChanged);
            var rt = (RectTransform)slider.transform.parent;
            UIFactory.Place(rt, new Vector2(0, 1), new Vector2(680, 60), new Vector2(40, -110 - row * 78));
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

        void Save()
        {
            string chapter = SaveSystem.Save();
            status.text = chapter != null ? $"Saved: {chapter}" : "Nothing to save yet. Press Play to start a game.";
        }

        void Load()
        {
            if (starting) return;
            if (!SaveSystem.HasSave)
            {
                status.text = "No saved game yet.";
                return;
            }
            starting = true;
            group.interactable = false;
            SaveSystem.Load();
        }
    }
}
