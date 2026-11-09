using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>
    /// On-screen layer shared by cutscenes and gameplay: subtitles, objective, interact prompt, toasts,
    /// screen fades, letterbox bars, title cards, the oxygen meter and dialogue choices.
    /// The layout is built once (in the editor by the scene builder, or at runtime if missing) and can be restyled freely.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        [SerializeField] Image fader;
        [SerializeField] RectTransform letterboxTop;
        [SerializeField] RectTransform letterboxBottom;
        [SerializeField] GameObject subtitleRoot;
        [SerializeField] Text subtitleSpeaker;
        [SerializeField] Text subtitleBody;
        [SerializeField] Text objectiveText;
        [SerializeField] Text promptText;
        [SerializeField] Text toastText;
        [SerializeField] CanvasGroup titleGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text titleSubText;
        [SerializeField] GameObject oxygenRoot;
        [SerializeField] Image oxygenFill;
        [SerializeField] Text skipHint;
        [SerializeField] RectTransform choiceRoot;

        [Tooltip("Scene starts on a black screen. Scripts fade it in when they are ready.")]
        [SerializeField] bool startBlack = true;

        const float LetterboxHeight = 120f;

        readonly Queue<string> toasts = new();
        Coroutine toastRoutine;
        Coroutine letterboxRoutine;

        public bool IsChoosing { get; private set; }

        void Awake()
        {
            Instance = this;
            if (fader == null) BuildDefaultLayout();
            UIFactory.EnsureEventSystem();

            SetFade(startBlack ? 1f : 0f);
            HideSubtitle();
            SetObjective(null);
            SetPrompt(null);
            ShowOxygen(false);
            SetSkipHint(-1f);
            toastText.text = "";
            titleGroup.alpha = 0f;
            letterboxTop.sizeDelta = new Vector2(0, 0);
            letterboxBottom.sizeDelta = new Vector2(0, 0);
        }

        void OnEnable()
        {
            if (GameState.Instance == null) return;
            GameState.Instance.XpGained += OnXpGained;
            GameState.Instance.LeveledUp += OnLeveledUp;
        }

        void OnDisable()
        {
            if (GameState.Instance == null) return;
            GameState.Instance.XpGained -= OnXpGained;
            GameState.Instance.LeveledUp -= OnLeveledUp;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnXpGained(int amount, string reason) => ShowToast($"+{amount} XP  {reason}");
        void OnLeveledUp(int level) => ShowToast($"LEVEL {level}!  +{GameState.PointsPerLevel} stat points");

        // ---------- Subtitles, objective, prompt ----------

        public void ShowSubtitle(string speaker, string body)
        {
            subtitleRoot.SetActive(true);
            subtitleSpeaker.text = speaker;
            subtitleBody.text = body;
        }

        public void HideSubtitle() => subtitleRoot.SetActive(false);

        public void SetObjective(string objective)
        {
            objectiveText.text = string.IsNullOrEmpty(objective) ? "" : "OBJECTIVE\n" + objective;
        }

        public void SetPrompt(string prompt) => promptText.text = prompt ?? "";

        public void ShowToast(string message)
        {
            toasts.Enqueue(message);
            toastRoutine ??= StartCoroutine(PlayToasts());
        }

        IEnumerator PlayToasts()
        {
            while (toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                yield return new WaitForSecondsRealtime(2.2f);
            }
            toastText.text = "";
            toastRoutine = null;
        }

        // ---------- Fades, letterbox, title cards ----------

        public void SetFade(float alpha, Color? color = null)
        {
            var c = color ?? fader.color;
            c.a = alpha;
            fader.color = c;
            fader.raycastTarget = alpha > 0.99f;
        }

        public IEnumerator FadeTo(float alpha, float duration, Color? color = null)
        {
            var target = color ?? fader.color;
            var from = fader.color;
            if (color.HasValue) from = new Color(target.r, target.g, target.b, from.a);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from.a, alpha, t / duration), target);
                yield return null;
            }
            SetFade(alpha, target);
        }

        public void SetLetterbox(bool on, float duration = 0.6f)
        {
            if (letterboxRoutine != null) StopCoroutine(letterboxRoutine);
            letterboxRoutine = StartCoroutine(AnimateLetterbox(on ? LetterboxHeight : 0f, duration));
        }

        IEnumerator AnimateLetterbox(float height, float duration)
        {
            float start = letterboxTop.sizeDelta.y;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float h = Mathf.SmoothStep(start, height, t / duration);
                letterboxTop.sizeDelta = new Vector2(0, h);
                letterboxBottom.sizeDelta = new Vector2(0, h);
                yield return null;
            }
            letterboxTop.sizeDelta = new Vector2(0, height);
            letterboxBottom.sizeDelta = new Vector2(0, height);
        }

        public IEnumerator ShowTitle(string title, string subtitle, float hold, Color? color = null)
        {
            titleText.text = title;
            titleText.color = color ?? Color.white;
            titleSubText.text = subtitle ?? "";
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime) { titleGroup.alpha = t; yield return null; }
            titleGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 1f; t > 0; t -= Time.unscaledDeltaTime) { titleGroup.alpha = t; yield return null; }
            titleGroup.alpha = 0f;
        }

        // ---------- Oxygen, skip hint ----------

        public void ShowOxygen(bool visible) => oxygenRoot.SetActive(visible);

        public void SetOxygen(float normalized)
        {
            oxygenFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            oxygenFill.color = normalized > 0.25f ? new Color(0.45f, 0.85f, 1f) : new Color(1f, 0.35f, 0.3f);
        }

        /// <summary>Progress 0-1 shows "Hold to skip" with a fill; a negative value hides it.</summary>
        public void SetSkipHint(float progress)
        {
            skipHint.gameObject.SetActive(progress >= 0f);
            if (progress >= 0f)
                skipHint.text = progress > 0f ? $"Skipping... {Mathf.RoundToInt(progress * 100)}%" : "Hold ESC to skip";
        }

        // ---------- Dialogue choices ----------

        /// <summary>Shows up to three options as buttons (also keys 1-3) and reports the chosen index.</summary>
        public IEnumerator Choose(IList<string> options, Action<int> onChosen)
        {
            IsChoosing = true;
            int chosen = -1;
            var previousLock = Cursor.lockState;
            var previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            choiceRoot.gameObject.SetActive(true);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = UIFactory.CreateButton($"Choice {i + 1}", choiceRoot, $"{i + 1}.  {options[i]}", 28, () => chosen = index);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                var rt = (RectTransform)button.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 60);
                rt.anchoredPosition = new Vector2(0, -i * 70);
            }

            while (chosen < 0)
            {
                if (GameInput.Choice1.WasPressedThisFrame() && options.Count > 0) chosen = 0;
                else if (GameInput.Choice2.WasPressedThisFrame() && options.Count > 1) chosen = 1;
                else if (GameInput.Choice3.WasPressedThisFrame() && options.Count > 2) chosen = 2;
                yield return null;
            }

            foreach (Transform child in choiceRoot) Destroy(child.gameObject);
            choiceRoot.gameObject.SetActive(false);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            IsChoosing = false;
            onChosen?.Invoke(chosen);
        }

        // ---------- Layout ----------

        /// <summary>Builds the placeholder layout under this object. Safe to restyle afterwards.</summary>
        [ContextMenu("Build Default Layout")]
        public void BuildDefaultLayout()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            var canvas = UIFactory.CreateCanvas("HUD Canvas", transform, 10);
            var root = canvas.transform;
            var ink = new Color(0.92f, 0.95f, 1f);
            var accent = new Color(0.55f, 0.85f, 1f);

            // Objective, top left
            objectiveText = UIFactory.CreateText("Objective", root, "", 26, TextAnchor.UpperLeft, ink);
            UIFactory.Place(objectiveText.rectTransform, new Vector2(0, 1), new Vector2(700, 120), new Vector2(40, -40));

            // Toasts, top right
            toastText = UIFactory.CreateText("Toast", root, "", 26, TextAnchor.UpperRight, accent);
            UIFactory.Place(toastText.rectTransform, new Vector2(1, 1), new Vector2(700, 60), new Vector2(-40, -40));

            // Interact prompt, just below centre
            promptText = UIFactory.CreateText("Prompt", root, "", 28, TextAnchor.MiddleCenter, ink);
            UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 50), new Vector2(0, -90));

            // Crosshair dot
            var dot = UIFactory.CreateImage("Crosshair", root, new Color(1, 1, 1, 0.6f));
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(6, 6), Vector2.zero);

            // Oxygen meter, bottom left
            var oxygen = UIFactory.CreateImage("Oxygen", root, new Color(0, 0, 0, 0.5f));
            UIFactory.Place(oxygen.rectTransform, new Vector2(0, 0), new Vector2(360, 56), new Vector2(40, 40));
            oxygenRoot = oxygen.gameObject;
            var oxygenLabel = UIFactory.CreateText("Label", oxygen.transform, "O₂", 22, TextAnchor.MiddleLeft, ink);
            UIFactory.Stretch(oxygenLabel.rectTransform, 12, 0, 0, 0);
            var bar = UIFactory.CreateImage("Bar", oxygen.transform, new Color(1, 1, 1, 0.12f));
            UIFactory.Stretch(bar.rectTransform, 56, 16, 14, 16);
            oxygenFill = UIFactory.CreateImage("Fill", bar.transform, accent);
            UIFactory.Stretch(oxygenFill.rectTransform);

            // Letterbox bars
            letterboxTop = UIFactory.CreateImage("Letterbox Top", root, Color.black).rectTransform;
            letterboxTop.anchorMin = new Vector2(0, 1); letterboxTop.anchorMax = new Vector2(1, 1); letterboxTop.pivot = new Vector2(0.5f, 1);
            letterboxBottom = UIFactory.CreateImage("Letterbox Bottom", root, Color.black).rectTransform;
            letterboxBottom.anchorMin = new Vector2(0, 0); letterboxBottom.anchorMax = new Vector2(1, 0); letterboxBottom.pivot = new Vector2(0.5f, 0);

            // Subtitles, bottom centre (above the letterbox)
            var subtitle = UIFactory.CreateImage("Subtitle", root, new Color(0, 0, 0, 0.55f));
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0), new Vector2(1300, 150), new Vector2(0, 140));
            subtitleRoot = subtitle.gameObject;
            subtitleSpeaker = UIFactory.CreateText("Speaker", subtitle.transform, "", 24, TextAnchor.UpperLeft, accent);
            subtitleSpeaker.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(subtitleSpeaker.rectTransform, 24, 14, 24, 100);
            subtitleBody = UIFactory.CreateText("Body", subtitle.transform, "", 30, TextAnchor.UpperLeft, ink);
            UIFactory.Stretch(subtitleBody.rectTransform, 24, 50, 24, 14);

            // Dialogue choices, centre
            choiceRoot = UIFactory.CreateRect("Choices", root);
            UIFactory.Place(choiceRoot, new Vector2(0.5f, 0.5f), new Vector2(900, 220), new Vector2(0, 0));
            choiceRoot.gameObject.SetActive(false);

            // Title card, centre
            var title = UIFactory.CreateRect("Title Card", root);
            UIFactory.Stretch(title);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.blocksRaycasts = false;
            titleText = UIFactory.CreateText("Title", title, "", 84, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1600, 140), new Vector2(0, 30));
            titleSubText = UIFactory.CreateText("Subtitle", title, "", 30, TextAnchor.MiddleCenter, accent);
            UIFactory.Place(titleSubText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1600, 60), new Vector2(0, -60));

            // Skip hint, bottom right
            skipHint = UIFactory.CreateText("Skip Hint", root, "Hold ESC to skip", 22, TextAnchor.LowerRight, new Color(1, 1, 1, 0.6f));
            UIFactory.Place(skipHint.rectTransform, new Vector2(1, 0), new Vector2(500, 40), new Vector2(-30, 20));

            // Fader on top of everything
            fader = UIFactory.CreateImage("Fader", root, Color.black);
            UIFactory.Stretch(fader.rectTransform);
        }
    }
}
