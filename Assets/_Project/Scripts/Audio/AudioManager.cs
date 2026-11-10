using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    /// <summary>
    /// Plays all game audio: one-shot sound effects, named loops (alarms, engines), per-scene ambience and music,
    /// and dialogue voice. Created automatically before the first scene and kept across scenes.
    /// Everything is 2D for now (no positional sound), which is enough for an MVP.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0, 1)] public float masterVolume = 1f;
        [Range(0, 1)] public float musicVolume = 0.5f;
        [Range(0, 1)] public float sfxVolume = 0.8f;
        [Range(0, 1)] public float voiceVolume = 1f;

        // Ambience loop and music pad for each scene.
        static readonly Dictionary<string, (string ambience, string music)> SceneAudio = new()
        {
            [SceneFlow.MissionControl] = (Sfx.SpaceDrone, Sfx.PadMystery),
            [SceneFlow.Memory] = (Sfx.Wind, Sfx.PadCalm),
            [SceneFlow.Adrift] = (Sfx.Breath, Sfx.PadDark),
            [SceneFlow.Cockpit] = (Sfx.ShipHum, Sfx.PadMystery),
            [SceneFlow.LandingSite] = (Sfx.Wind, Sfx.PadCalm),
            [SceneFlow.FuelMoon] = (Sfx.SpaceDrone, Sfx.PadMystery),
            [SceneFlow.GateWorld] = (Sfx.Wind, Sfx.PadMystery),
            [SceneFlow.Harvester] = (Sfx.Machinery, Sfx.PadDark),
            [SceneFlow.Control] = (Sfx.SpaceDrone, Sfx.PadTense),
        };

        AudioSource sfx;
        AudioSource voice;
        AudioLowPassFilter radioLow;
        AudioHighPassFilter radioHigh;
        readonly Dictionary<string, (AudioSource source, float volume)> loops = new();
        readonly Dictionary<string, Coroutine> fades = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[AudioManager]").AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            // Voice gets its own object so radio filtering only affects voices.
            var voiceObject = new GameObject("Voice");
            voiceObject.transform.SetParent(transform, false);
            voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            // Filters on a source with no clip log a warning, so start with a tiny silent clip.
            voice.clip = AudioClip.Create("silence", 32, 1, 22050, false);
            radioLow = voiceObject.AddComponent<AudioLowPassFilter>();
            radioLow.cutoffFrequency = 3200f;
            radioHigh = voiceObject.AddComponent<AudioHighPassFilter>();
            radioHigh.cutoffFrequency = 500f;
            SetRadio(false);

            masterVolume = PlayerPrefs.GetFloat("tsa.volume.master", masterVolume);
            musicVolume = PlayerPrefs.GetFloat("tsa.volume.music", musicVolume);
            sfxVolume = PlayerPrefs.GetFloat("tsa.volume.sfx", sfxVolume);
            voiceVolume = PlayerPrefs.GetFloat("tsa.volume.voice", voiceVolume);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>Called by the options menu: stores the volumes and applies them to sounds that are already playing.</summary>
        public void ApplyVolumes()
        {
            PlayerPrefs.SetFloat("tsa.volume.master", masterVolume);
            PlayerPrefs.SetFloat("tsa.volume.music", musicVolume);
            PlayerPrefs.SetFloat("tsa.volume.sfx", sfxVolume);
            PlayerPrefs.SetFloat("tsa.volume.voice", voiceVolume);
            foreach (var pair in loops)
                if (!fades.ContainsKey(pair.Key)) pair.Value.source.volume = LoopVolume(pair.Key, pair.Value.volume);
            voice.volume = voiceVolume * masterVolume;
        }

        // The scene you press Play in doesn't always raise sceneLoaded in the editor, so check it here too.
        void Start() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        Scene audioScene;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == audioScene) return;
            audioScene = scene;
            StopVoice();
            foreach (var id in new List<string>(loops.Keys))
                if (id != "ambience" && id != "music") StopLoop(id, 0.5f);

            if (!SceneAudio.TryGetValue(scene.name, out var audio)) return;
            StartLoop("ambience", audio.ambience, 0.5f, 2f);
            PlayMusic(audio.music);
        }

        // ---------- One-shots ----------

        /// <summary>Plays a sound effect once. Pitch variation keeps repeated sounds (footsteps) from sounding robotic.</summary>
        public static void Play(string name, float volume = 1f, float pitchVariation = 0f)
        {
            if (Instance == null) return;
            var clip = SoundLibrary.Get(name);
            if (clip == null) return;
            if (pitchVariation > 0f)
            {
                // A separate short-lived source so the pitch change doesn't affect other one-shots.
                var source = Instance.gameObject.AddComponent<AudioSource>();
                source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                source.PlayOneShot(clip, volume * Instance.sfxVolume * Instance.masterVolume);
                Destroy(source, clip.length / source.pitch + 0.1f);
                return;
            }
            Instance.sfx.PlayOneShot(clip, volume * Instance.sfxVolume * Instance.masterVolume);
        }

        // ---------- Loops ----------

        /// <summary>Starts (or keeps) a looping sound under an id, fading it in. Calling again with the same id changes the clip or volume.</summary>
        public static void StartLoop(string id, string clipName, float volume = 1f, float fadeIn = 0.5f)
        {
            if (Instance == null) return;
            var clip = SoundLibrary.Get(clipName);
            if (clip == null) return;

            if (!Instance.loops.TryGetValue(id, out var loop))
            {
                var source = Instance.gameObject.AddComponent<AudioSource>();
                source.loop = true;
                source.playOnAwake = false;
                loop = (source, volume);
            }
            if (loop.source.clip != clip)
            {
                loop.source.clip = clip;
                loop.source.volume = 0f;
                loop.source.Play();
            }
            Instance.loops[id] = (loop.source, volume);
            Instance.Fade(id, Instance.LoopVolume(id, volume), fadeIn, stopAtEnd: false);
        }

        public static void StopLoop(string id, float fadeOut = 0.5f)
        {
            if (Instance == null || !Instance.loops.ContainsKey(id)) return;
            Instance.Fade(id, 0f, fadeOut, stopAtEnd: true);
        }

        /// <summary>Changes a running loop's volume (and pitch), for example to build up an engine roar.</summary>
        public static void SetLoop(string id, float volume, float pitch = 1f)
        {
            if (Instance == null || !Instance.loops.TryGetValue(id, out var loop)) return;
            Instance.loops[id] = (loop.source, volume);
            if (Instance.fades.TryGetValue(id, out var running) && running != null) Instance.StopCoroutine(running);
            loop.source.volume = Instance.LoopVolume(id, volume);
            loop.source.pitch = pitch;
        }

        public static void PlayMusic(string padName, float fade = 3f) => StartLoop("music", padName, 1f, fade);

        float LoopVolume(string id, float volume) => volume * masterVolume * (id == "music" ? musicVolume : sfxVolume);

        void Fade(string id, float target, float duration, bool stopAtEnd)
        {
            if (fades.TryGetValue(id, out var running) && running != null) StopCoroutine(running);
            fades[id] = StartCoroutine(FadeRoutine(id, target, duration, stopAtEnd));
        }

        IEnumerator FadeRoutine(string id, float target, float duration, bool stopAtEnd)
        {
            var source = loops[id].source;
            float start = source.volume;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            source.volume = target;
            if (stopAtEnd)
            {
                loops.Remove(id);
                Destroy(source);
            }
            fades.Remove(id);
        }

        // ---------- Voice ----------

        /// <summary>Plays a dialogue clip and returns its length in seconds. Radio adds a thin, filtered "comms" sound.</summary>
        public static float PlayVoice(AudioClip clip, bool radio)
        {
            if (Instance == null || clip == null) return 0f;
            Instance.SetRadio(radio);
            Instance.voice.Stop();
            Instance.voice.clip = clip;
            Instance.voice.volume = Instance.voiceVolume * Instance.masterVolume;
            Instance.voice.Play();
            return clip.length;
        }

        public static bool VoicePlaying => Instance != null && Instance.voice.isPlaying;

        static readonly float[] levelBuffer = new float[256];
        static int levelFrame = -1;
        static float level;

        /// <summary>How loud the voice is at this moment (0 to about 0.3). Used to move speakers' mouths.</summary>
        public static float VoiceLevel
        {
            get
            {
                if (!VoicePlaying) return 0f;
                if (levelFrame == Time.frameCount) return level;
                levelFrame = Time.frameCount;
                Instance.voice.GetOutputData(levelBuffer, 0);
                float sum = 0f;
                foreach (float sample in levelBuffer) sum += sample * sample;
                return level = Mathf.Sqrt(sum / levelBuffer.Length);
            }
        }

        public static void StopVoice()
        {
            if (Instance != null) Instance.voice.Stop();
        }

        void SetRadio(bool on)
        {
            radioLow.enabled = on;
            radioHigh.enabled = on;
        }
    }
}
