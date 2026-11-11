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

        [Tooltip("Optional: the camera turns to follow this object (a fly-by).")]
        public Transform lookAt;
        [Tooltip("Seconds into the shot before the camera starts turning toward Look At.")]
        public float lookAtDelay;
        [Tooltip("Seconds the camera takes to swing onto Look At.")]
        public float lookAtBlend = 1.5f;

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
        [Tooltip("Seconds the black bars take to slide in.")]
        [SerializeField] float letterboxDuration;
        [SerializeField] List<CutsceneShot> shots = new();

        [Header("Ending")]
        [SerializeField] string endTitle;
        [SerializeField] string endSubtitle;
        [SerializeField] float endTitleHold = 2.5f;
        [Tooltip("End on a hard cut to black with an impact sound, instead of a fade.")]
        [SerializeField] bool cutToBlackAtEnd;
        [SerializeField] float blackHold = 2f;
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
            hud.SetLetterbox(true, letterboxDuration);
            StartCoroutine(hud.FadeTo(0f, fadeInDuration, Color.black));

            foreach (var shot in shots)
                yield return PlayShot(shot);

            yield return Finish(skipped: false);
        }

        IEnumerator PlayShot(CutsceneShot shot)
        {
            // One broken hook-up (a deleted light, a renamed object) must never freeze the whole cutscene.
            try { shot.onStart?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e, this); }
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
                    var position = Vector3.Lerp(shot.from.position, target.position, k);
                    var rotation = Quaternion.Slerp(shot.from.rotation, target.rotation, k);
                    if (shot.lookAt != null)
                    {
                        float weight = Mathf.SmoothStep(0f, 1f, (t - shot.lookAtDelay) / Mathf.Max(0.01f, shot.lookAtBlend));
                        if (weight > 0f)
                            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(shot.lookAt.position - position), weight);
                    }
                    cutsceneCamera.transform.SetPositionAndRotation(position, rotation);
                }
                if (shot.shake > 0f)
                {
                    float time = Time.time * 18f;
                    float x = (Mathf.PerlinNoise(seed, time) - 0.5f) * 2f, y = (Mathf.PerlinNoise(seed + 10f, time) - 0.5f) * 2f;
                    cutsceneCamera.transform.position += cutsceneCamera.transform.rotation * new Vector3(x * shot.shake, y * shot.shake, 0f);
                    // A little rotation as well: it reads as much stronger than sliding the camera.
                    cutsceneCamera.transform.rotation *= Quaternion.Euler(y * shot.shake * 22f, x * shot.shake * 22f, x * shot.shake * 30f);
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

            if (!skipped && cutToBlackAtEnd)
            {
                hud.SetFade(1f, Color.black);
                Dialogue.Stop();
                AudioManager.Play(Sfx.Impact);
                foreach (var loop in new[] { "engine", "gate", "alarm" }) AudioManager.StopLoop(loop, 0.05f);
                yield return new WaitForSeconds(blackHold);
            }

            IsPlaying = false;
            hud.SetLetterbox(false);
            onFinished?.Invoke();
            Finished?.Invoke();

            if (!string.IsNullOrEmpty(nextScene))
                SceneFlow.Load(nextScene, skipped ? 0.5f : cutToBlackAtEnd ? 0.05f : 1f);
        }
    }
}
