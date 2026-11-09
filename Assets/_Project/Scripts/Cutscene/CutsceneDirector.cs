using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TSAGame
{
    /// <summary>
    /// One camera shot: the camera moves from one marker to another while subtitle lines play and events fire.
    /// </summary>
    [Serializable]
    public class CutsceneShot
    {
        public string name = "Shot";

        [Tooltip("Camera pose at the start of the shot.")]
        public Transform from;
        [Tooltip("Optional pose the camera glides to over the shot. Leave empty to hold still.")]
        public Transform to;

        [Min(0.1f)] public float duration = 4f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Range(10f, 100f)] public float fieldOfView = 50f;
        [Tooltip("Camera shake strength in metres. 0 = steady.")]
        public float shake;

        [Tooltip("Keep the shot running until all its lines have finished, even past the duration.")]
        public bool waitForLines = true;
        [Min(0f)] public float linesDelay = 0.3f;
        public List<DialogueLine> lines = new();

        [Tooltip("Fires when the shot begins: start props, alarms, effects.")]
        public UnityEvent onStart = new();
    }

    /// <summary>
    /// Plays a list of shots in order with letterbox bars, subtitles and a hold-to-skip.
    /// Edit shots in the Inspector: move the camera markers in the scene to reframe them.
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        [SerializeField] Camera cutsceneCamera;
        [SerializeField] bool playOnStart = true;
        [SerializeField] float fadeInDuration = 2f;
        [SerializeField] float skipHoldSeconds = 1f;
        [SerializeField] List<CutsceneShot> shots = new();

        [Header("Ending")]
        [SerializeField] string endTitle;
        [SerializeField] string endSubtitle;
        [SerializeField] float endTitleHold = 2.5f;
        [Tooltip("Scene to load when the cutscene ends. Leave empty to stay in this scene.")]
        [SerializeField] string nextScene;
        [SerializeField] UnityEvent onFinished = new();

        public bool IsPlaying { get; private set; }
        public event Action Finished;

        /// <summary>The shot list, for editor tools that build or edit cutscenes.</summary>
        public List<CutsceneShot> Shots => shots;

        float skipHeld;

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (IsPlaying) return;
            StartCoroutine(Run());
        }

        void Update()
        {
            if (!IsPlaying || HUD.Instance == null) return;

            skipHeld = GameInput.Skip.IsPressed() ? skipHeld + Time.unscaledDeltaTime : 0f;
            HUD.Instance.SetSkipHint(skipHeld / skipHoldSeconds);
            if (skipHeld >= skipHoldSeconds) Skip();
        }

        public void Skip()
        {
            if (!IsPlaying) return;
            StopAllCoroutines();
            Dialogue.Stop();
            StartCoroutine(Finish(skipped: true));
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            var hud = HUD.Instance;
            hud.SetLetterbox(true, 0f);
            StartCoroutine(hud.FadeTo(0f, fadeInDuration, Color.black));

            foreach (var shot in shots)
                yield return PlayShot(shot);

            yield return Finish(skipped: false);
        }

        IEnumerator PlayShot(CutsceneShot shot)
        {
            shot.onStart?.Invoke();
            cutsceneCamera.fieldOfView = shot.fieldOfView;

            bool linesDone = shot.lines.Count == 0;
            if (!linesDone) StartCoroutine(PlayLines(shot, () => linesDone = true));

            float seed = UnityEngine.Random.value * 100f;
            for (float t = 0f; t < shot.duration || (shot.waitForLines && !linesDone); t += Time.deltaTime)
            {
                float k = shot.ease.Evaluate(Mathf.Clamp01(t / shot.duration));
                if (shot.from != null)
                {
                    var target = shot.to != null ? shot.to : shot.from;
                    cutsceneCamera.transform.SetPositionAndRotation(
                        Vector3.Lerp(shot.from.position, target.position, k),
                        Quaternion.Slerp(shot.from.rotation, target.rotation, k));
                }
                if (shot.shake > 0f)
                {
                    float time = Time.time * 18f;
                    cutsceneCamera.transform.position += cutsceneCamera.transform.rotation * new Vector3(
                        (Mathf.PerlinNoise(seed, time) - 0.5f) * 2f * shot.shake,
                        (Mathf.PerlinNoise(seed + 10f, time) - 0.5f) * 2f * shot.shake,
                        0f);
                }
                yield return null;
            }
        }

        IEnumerator PlayLines(CutsceneShot shot, Action done)
        {
            if (shot.linesDelay > 0f) yield return new WaitForSeconds(shot.linesDelay);
            yield return Dialogue.Play(shot.lines);
            done();
        }

        IEnumerator Finish(bool skipped)
        {
            var hud = HUD.Instance;
            hud.SetSkipHint(-1f);

            if (!skipped && !string.IsNullOrEmpty(endTitle))
            {
                yield return hud.FadeTo(1f, 1.5f, Color.black);
                yield return hud.ShowTitle(endTitle, endSubtitle, endTitleHold);
            }

            IsPlaying = false;
            hud.SetLetterbox(false);
            onFinished?.Invoke();
            Finished?.Invoke();

            if (!string.IsNullOrEmpty(nextScene))
                SceneFlow.Load(nextScene, skipped ? 0.5f : 1f);
        }
    }
}
