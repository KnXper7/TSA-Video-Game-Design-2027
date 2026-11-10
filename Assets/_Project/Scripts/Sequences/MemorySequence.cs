using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The memory of launch day on a dying Earth. The player talks to the people seeing them off, collects the
    /// last three pieces of cargo, is given the grip ball by their engineer and boards the rocket. Then the launch
    /// plays as old home video, and the game's title flies in.
    /// </summary>
    public class MemorySequence : MonoBehaviour
    {
        [Header("People and cargo")]
        [SerializeField] FirstPersonController player;
        [SerializeField] Interactable engineer;
        [SerializeField] Interactable pump;
        [SerializeField] Interactable botanist;
        [SerializeField] Interactable guidanceCore;
        [SerializeField] Interactable lift;
        [SerializeField] GameObject pumpBeacon;
        [SerializeField] GameObject botanistBeacon;
        [SerializeField] GameObject coreBeacon;
        [SerializeField] GameObject liftBeacon;
        [SerializeField] GameObject seedCase;
        [SerializeField] GameObject ballProp;

        [Header("Launch")]
        [SerializeField] Camera cutsceneCamera;
        [SerializeField] VhsFilter vhs;
        [SerializeField] string dateStamp = "AM 6:42\nMAR. 3 2139";
        [SerializeField] Transform rocket;
        [SerializeField] Transform rocketCentre;
        [SerializeField] ParticleSystem[] plumes;
        [SerializeField] ParticleSystem padSmoke;
        [SerializeField] Light launchLight;
        [SerializeField] Transform wideShot;
        [SerializeField] Transform lowShot;

        [Header("Title")]
        [SerializeField] Transform titleShot;
        [SerializeField] TitleLetters title;
        [Tooltip("Everything around the title (stars, lights). Hidden until the title plays.")]
        [SerializeField] GameObject titleStage;

        // Cutscene camera state
        Transform shot;
        bool tracking;
        float shake, dolly;

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            player.CanMove = false;
            Story.Lock(pump, botanist, guidanceCore, lift);
            foreach (var beacon in new[] { pumpBeacon, botanistBeacon, coreBeacon, liftBeacon }) beacon.SetActive(false);
            ballProp.SetActive(false);
            titleStage.SetActive(false);

            yield return new WaitForSeconds(1f);
            yield return hud.ShowTitle("FOURTEEN MONTHS EARLIER", "Earth. Launch day.", 2.5f);
            StartCoroutine(hud.FadeTo(0f, 3f, Color.black));
            yield return Dialogue.Say(Speakers.Commander, "I remember that morning. The last one I spent on Earth.");
            player.CanMove = true;

            // The briefing
            hud.SetObjective("Talk to your engineer at the launch pad");
            yield return Story.Use(engineer);
            yield return Dialogue.Play(
                (Speakers.Engineer, "There you are. She's fuelled and she's angry. Six hours to launch."),
                (Speakers.Engineer, "Three things still aren't aboard: the water ration, the seed vault and the guidance core."),
                (Speakers.Commander, "The three things I can't fly without."),
                (Speakers.Engineer, "Then stop standing here. Bring them to the cargo lift and I'll get them stowed."));
            state.AddXp(10, "Briefed");

            // The last three pieces of cargo, in any order
            Story.Unlock(pump, botanist, guidanceCore);
            pumpBeacon.SetActive(true);
            botanistBeacon.SetActive(true);
            coreBeacon.SetActive(true);
            Interactable used = null;
            void OnUsed(Interactable interactable) => used = interactable;
            pump.Interacted += OnUsed;
            botanist.Interacted += OnUsed;
            guidanceCore.Interacted += OnUsed;

            for (int collected = 0; collected < 3; collected++)
            {
                hud.SetObjective($"Collect the final cargo ({collected}/3)\nWater ration  •  Seed vault  •  Guidance core");
                used = null;
                yield return new WaitUntil(() => used != null);
                if (used == pump)
                {
                    pumpBeacon.SetActive(false);
                    yield return Dialogue.Play(
                        (Speakers.Quartermaster, "That's the last of Reservoir Three. Forty litres."),
                        (Speakers.Quartermaster, "Used to be you couldn't see the far shore of this lake."),
                        (Speakers.Commander, "I learned to swim here."),
                        (Speakers.Quartermaster, "Then bring some back with you, Commander."));
                    state.AddXp(15, "Water ration");
                }
                else if (used == botanist)
                {
                    botanistBeacon.SetActive(false);
                    yield return Seeds();
                    seedCase.SetActive(false);
                    state.AddXp(15, "Seed vault");
                }
                else
                {
                    coreBeacon.SetActive(false);
                    yield return Dialogue.Play(
                        (Speakers.Commander, "The guidance core. Every chart we have of a place no one has ever been."),
                        (Speakers.Commander, "Fourteen months of flying on somebody's best guess."));
                    state.AddXp(15, "Guidance core");
                }
            }

            // Load up, and the gift
            hud.SetObjective("Bring the cargo to the lift at the launch pad");
            liftBeacon.SetActive(true);
            yield return Story.Use(lift);
            liftBeacon.SetActive(false);
            hud.SetObjective(null);
            state.AddXp(30, "Cargo loaded");
            yield return GripBallGift();

            lift.SetPrompt("Board the rocket");
            hud.SetObjective("Board the rocket");
            liftBeacon.SetActive(true);
            yield return Story.Use(lift);
            state.SetFlag("memory.done");

            yield return Launch();
        }

        IEnumerator Seeds()
        {
            var state = GameState.Instance;
            yield return Dialogue.Play(
                (Speakers.Botanist, "Commander. I was afraid you'd leave without it."),
                (Speakers.Botanist, "Every crop we still have, in one case. Wheat, rice, beans. Forty thousand seeds."),
                (Speakers.Botanist, "Nothing grows here anymore. Tell me honestly. Do you think Planet X has soil?"));
            int choice = 0;
            yield return Dialogue.Choose(new[]
            {
                "I don't know. But I'll find out, and I'll tell you the truth.",
                "It does. I'm sure of it.",
                "If it doesn't, I'll find somewhere that does.",
            }, c => choice = c);
            state.SetFlag("memory.seeds." + choice);
            yield return Dialogue.Say(Speakers.Botanist, choice switch
            {
                0 => "The truth. That's all any of us can ask for now.",
                1 => "I hope you're right. I need you to be right.",
                _ => "Then don't come back until you do.",
            });
        }

        IEnumerator GripBallGift()
        {
            var state = GameState.Instance;
            yield return Dialogue.Say(Speakers.Engineer, "That's everything on the manifest. And one thing that isn't.");
            ballProp.SetActive(true);
            AudioManager.Play(Sfx.Pickup);
            HUD.Instance.ShowToast("Received: grip ball");
            yield return Dialogue.Play(
                (Speakers.Commander, "It's... a lumpy metal ball."),
                (Speakers.Engineer, "It's a grip anchor. See the bumps? They bite into anything. Rock, hull, ice."),
                (Speakers.Engineer, "If you're ever floating free with nothing to push against, throw it. It grips, the line goes tight, and you reel yourself in."),
                (Speakers.Engineer, "Stuck in zero gravity, that ball is the difference between drifting forever and getting home."),
                (Speakers.Commander, "You built this for me?"),
                (Speakers.Engineer, "I built it so you'd come back. Promise me you'll come back."));
            int choice = 0;
            yield return Dialogue.Choose(new[] { "I promise.", "I'll try.", "Keep the lights on for me." }, c => choice = c);
            state.SetFlag("memory.promise." + choice);
            yield return Dialogue.Say(Speakers.Engineer, choice switch
            {
                0 => "I'm holding you to that.",
                1 => "Try hard.",
                _ => "As long as there's power to run them.",
            });
            ballProp.SetActive(false);
            yield return Dialogue.Play(
                (Speakers.Engineer, "This is it, then."),
                (Speakers.Commander, "This is it."));
            state.AddXp(20, "Ready for launch");
        }

        // ---------- Launch, as old home video ----------

        IEnumerator Launch()
        {
            var hud = HUD.Instance;
            player.CanMove = false;
            hud.SetObjective(null);
            yield return hud.FadeTo(1f, 1.5f, Color.black);

            player.gameObject.SetActive(false);
            liftBeacon.SetActive(false);
            cutsceneCamera.gameObject.SetActive(true);
            vhs.Enable(cutsceneCamera, dateStamp);
            SetShot(wideShot, 30f);
            tracking = true;
            AudioManager.StopLoop("music", 2f);
            yield return hud.FadeTo(0f, 1.5f, Color.black);

            yield return Dialogue.Say(Speakers.Controller, "T minus ten. Nine. Eight. Seven.");

            // Ignition
            foreach (var plume in plumes) plume.Play();
            padSmoke.Play();
            AudioManager.StartLoop("launch", Sfx.Roar, 0.5f, 1.2f);
            shake = 0.04f;
            StartCoroutine(RampLight(60f, 2f));
            yield return Dialogue.Say(Speakers.Controller, "Main engine start. Five. Four. Three. Two. One.");

            // Liftoff
            AudioManager.StartLoop("launch", Sfx.Roar, 1f, 0.6f);
            AudioManager.Play(Sfx.Explosion, 0.6f);
            StartCoroutine(Dialogue.Say(Speakers.Controller, "Liftoff. We have liftoff."));
            shake = 0.1f;
            float speed = 0f;
            bool cut = false, farewell = false;
            for (float t = 0f; t < 14f; t += Time.deltaTime)
            {
                speed += (0.8f + t * 0.5f) * Time.deltaTime;
                rocket.position += Vector3.up * (speed * Time.deltaTime);
                if (!cut && t > 6f)
                {
                    cut = true;
                    SetShot(lowShot, 52f);
                    shake = 0.2f;
                }
                if (!farewell && t > 8f && !Dialogue.IsPlaying)
                {
                    farewell = true;
                    StartCoroutine(Dialogue.Say(Speakers.Engineer, "Go on. Find us a way."));
                }
                shake = Mathf.MoveTowards(shake, cut ? 0.05f : 0.1f, 0.02f * Time.deltaTime);
                yield return null;
            }

            yield return hud.FadeTo(1f, 2f, Color.white);
            AudioManager.StopLoop("launch", 1.5f);
            padSmoke.Stop();
            vhs.Disable();
            Dialogue.Stop();

            // The title, in deep space
            tracking = false;
            shake = 0f;
            RenderSettings.fog = false;
            cutsceneCamera.clearFlags = CameraClearFlags.SolidColor;
            cutsceneCamera.backgroundColor = Color.black;
            titleStage.SetActive(true);
            SetShot(titleShot, 50f);
            // A beat of complete silence after the roar, then the title lands on the first sound.
            AudioManager.StopLoop("ambience", 0.5f);
            AudioManager.StopLoop("music", 0.5f);
            yield return hud.FadeTo(1f, 0.6f, Color.black);
            yield return new WaitForSeconds(1.6f);
            StartCoroutine(hud.FadeTo(0f, 1.2f, Color.black));
            AudioManager.Play(Sfx.Stinger);
            AudioManager.PlayMusic(Sfx.PadMystery, 3f);
            yield return title.Play();
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                dolly += 0.3f * Time.deltaTime;
                yield return null;
            }
            SceneFlow.Load(SceneFlow.Adrift, 2f);
            while (true)
            {
                dolly += 0.3f * Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator RampLight(float intensity, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                launchLight.intensity = intensity * t / seconds;
                yield return null;
            }
            launchLight.intensity = intensity;
        }

        void SetShot(Transform marker, float fieldOfView)
        {
            shot = marker;
            dolly = 0f;
            cutsceneCamera.fieldOfView = fieldOfView;
            cutsceneCamera.transform.SetPositionAndRotation(marker.position, marker.rotation);
        }

        void LateUpdate()
        {
            if (shot == null || !cutsceneCamera.gameObject.activeSelf) return;
            var cam = cutsceneCamera.transform;
            var position = shot.position + shot.forward * dolly;
            var rotation = shot.rotation;
            if (tracking)
                rotation = Quaternion.LookRotation(rocketCentre.position - position);
            if (shake > 0f)
            {
                float time = Time.time * 16f;
                float x = (Mathf.PerlinNoise(3f, time) - 0.5f) * 2f, y = (Mathf.PerlinNoise(17f, time) - 0.5f) * 2f;
                position += rotation * new Vector3(x * shake, y * shake, 0f);
                rotation *= Quaternion.Euler(y * shake * 14f, x * shake * 14f, x * shake * 20f);
            }
            cam.SetPositionAndRotation(position, rotation);
        }
    }
}
