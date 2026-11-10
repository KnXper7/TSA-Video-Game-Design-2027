using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Moves a rigged person without animation clips, and keeps the movement tied to what they are doing:
    ///  - standing: weight on one leg then the other, arms relaxed and still, eyes on whatever they are watching;
    ///  - seated at a desk: hands on the keyboard, typing in bursts, pausing to read the screen;
    ///  - talking: the hands come up on the stressed words of the actual voice recording and settle in the pauses,
    ///    the head nods on the same beats, and the jaw follows the loudness of the voice.
    /// Works on any rig that uses the usual bone names (Hips, Spine, Head, LeftArm, LeftForeArm...).
    /// For motion-captured animation instead, remove this component and add an Animator Controller.
    /// </summary>
    public class PersonAnimator : MonoBehaviour
    {
        public enum Pose { Standing, Seated }

        [SerializeField] Pose pose = Pose.Standing;
        [Tooltip("Speaker label (as shown above subtitles). While that speaker talks, this person gestures and moves their mouth.")]
        [SerializeField] string speaker;
        [Tooltip("What the head turns toward. Empty = look around idly.")]
        [SerializeField] Transform lookAt;
        [Tooltip("Look at the player's camera (overrides Look At).")]
        [SerializeField] bool lookAtPlayer;
        [Tooltip("Seated only: hands rest on a keyboard and type.")]
        [SerializeField] bool typing = true;
        [SerializeField] float seatHeight = 0.5f;
        [Tooltip("Seated and typing: where the keyboard is, relative to this person (right, up, forward).")]
        [SerializeField] Vector3 keyboard = new(0f, 0.8f, 0.36f);
        [Tooltip("How much idle movement. 0 = statue, 1 = normal.")]
        [SerializeField, Range(0f, 2f)] float liveliness = 1f;
        [Tooltip("Tint for the clothes, so copies of one character don't all look the same. White = unchanged.")]
        [SerializeField] Color clothesTint = Color.white;

        class Bone
        {
            public Transform t;
            public Quaternion bind;      // rotation in the character's own space, in the rest pose
            public Vector3 direction;    // which way the limb points in the rest pose (toward its child)
            public float length;
        }

        Bone hips, spine, spine1, spine2, neck, head, jaw;
        readonly Bone[] upperArm = new Bone[2], foreArm = new Bone[2], hand = new Bone[2];
        readonly Bone[] upperLeg = new Bone[2], lowerLeg = new Bone[2], foot = new Bone[2];
        readonly Vector3[] upperNow = new Vector3[2], foreNow = new Vector3[2], keyTarget = new Vector3[2];
        readonly float[] nextKey = new float[2];
        Vector3 hipsRest;
        float phase, talk, jawOpen, lookYaw, lookPitch;
        float stress, phrase;                    // fast and slow envelopes of the voice
        float typingUntil, restingUntil, weight; // typing bursts, and which leg carries the weight
        bool presenting;

        /// <summary>The right hand bone, for putting a prop in it.</summary>
        public Transform RightHand => hand[1]?.t;

        /// <summary>Holds the right hand out, palm up, as if offering something. Parent the prop to RightHand.</summary>
        public void Present(bool on) => presenting = on;
        int leadHand;
        System.Random random;
        bool ready;

        void Awake()
        {
            int seed = transform.position.GetHashCode();
            random = new System.Random(seed);
            phase = Mathf.Abs(seed % 1000) * 0.37f;
            leadHand = random.Next(2);
            hips = Find("Hips"); spine = Find("Spine"); spine1 = Find("Spine1"); spine2 = Find("Spine2");
            neck = Find("Neck") ?? Find("neck"); head = Find("Head"); jaw = Find("Jaw");
            if (Find("Spine02") != null)          // rigs that number the spine from the top down (Meshy)
            {
                spine2 = spine; spine = Find("Spine02"); spine1 = Find("Spine01");
            }
            string[] sides = { "Left", "Right" };
            for (int i = 0; i < 2; i++)
            {
                upperArm[i] = Find(sides[i] + "Arm"); foreArm[i] = Find(sides[i] + "ForeArm"); hand[i] = Find(sides[i] + "Hand");
                upperLeg[i] = Find(sides[i] + "UpLeg"); lowerLeg[i] = Find(sides[i] + "Leg"); foot[i] = Find(sides[i] + "Foot");
                Aim(upperArm[i], foreArm[i]); Aim(foreArm[i], hand[i]); Aim(upperLeg[i], lowerLeg[i]); Aim(lowerLeg[i], foot[i]);
            }
            ready = hips != null && head != null && upperArm[0] != null && upperArm[1] != null && upperLeg[0] != null && upperLeg[1] != null
                    && foreArm[0] != null && foreArm[1] != null;
            if (!ready)
            {
                Debug.LogWarning($"{name}: PersonAnimator could not find the usual bone names, so this person will stay in its rest pose.", this);
                return;
            }
            hipsRest = transform.InverseTransformPoint(hips.t.position);

            if (clothesTint != Color.white)
            {
                var block = new MaterialPropertyBlock();
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.name.StartsWith("Clothes")) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", clothesTint);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        Bone Find(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name;
                int colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                if (n == boneName) return new Bone { t = t, bind = Quaternion.Inverse(transform.rotation) * t.rotation };
            }
            return null;
        }

        void Aim(Bone bone, Bone child)
        {
            if (bone == null || child == null) return;
            var offset = transform.InverseTransformDirection(child.t.position - bone.t.position);
            bone.direction = offset.normalized;
            bone.length = offset.magnitude;
        }

        float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            float t = Time.time + phase;
            float live = liveliness;
            var root = transform.rotation;
            bool seated = pose == Pose.Seated;

            // ---- Voice: is this person speaking, and how hard is the current syllable? ----
            bool speaking = !string.IsNullOrEmpty(speaker) && Dialogue.IsPlaying && Dialogue.CurrentSpeaker == speaker;
            talk = Mathf.MoveTowards(talk, speaking ? 1f : 0f, dt * 2.5f);
            float voice = speaking ? Mathf.Clamp01(AudioManager.VoiceLevel * 10f) : 0f;
            if (speaking && !AudioManager.VoicePlaying)                       // a line with no recording: fake a speech rhythm
                voice = Mathf.Clamp01(0.45f + 0.5f * Mathf.Sin(t * 14f) * Mathf.Sin(t * 3.1f));
            stress = Mathf.Lerp(stress, voice, 1f - Mathf.Exp(-(voice > stress ? 30f : 9f) * dt));     // jumps up, falls slower
            phrase = Mathf.Lerp(phrase, speaking && voice > 0.08f ? 1f : 0f, 1f - Mathf.Exp(-(speaking ? 2.2f : 4f) * dt));
            jawOpen = Mathf.Lerp(jawOpen, voice, 1f - Mathf.Exp(-28f * dt));

            // ---- Hips and torso ----
            // Standing people rest their weight on one leg for a while, then change.
            float wantWeight = Mathf.PerlinNoise(t * 0.05f, 3f) > 0.5f ? 1f : -1f;
            weight = Mathf.Lerp(weight, seated ? 0f : wantWeight, 1f - Mathf.Exp(-0.8f * dt));
            float breath = Mathf.Sin(t * 1.4f) * 0.9f * live;
            var qHips = Quaternion.Euler(0f, weight * 2f * live, weight * 2.5f * live);
            Set(hips, root, qHips);
            hips.t.position = seated
                ? transform.TransformPoint(new Vector3(hipsRest.x, seatHeight + 0.1f, hipsRest.z - 0.08f))
                : transform.TransformPoint(hipsRest + new Vector3(weight * 0.025f * live, -Mathf.Abs(weight) * 0.006f * live, 0f));
            float lean = seated ? (typing ? 9f : 4f) : 0f;
            var qTorso = qHips * Quaternion.Euler(lean + breath - stress * talk * 1.5f, -weight * live, -weight * 2f * live);
            Set(spine, root, Quaternion.Slerp(qHips, qTorso, 0.4f));
            Set(spine1, root, Quaternion.Slerp(qHips, qTorso, 0.7f));
            Set(spine2, root, qTorso);

            // ---- Head: on its target, with small nods on stressed words ----
            float wantYaw = (Mathf.PerlinNoise(t * 0.08f, 9f) - 0.5f) * 30f * live;
            float wantPitch = (Mathf.PerlinNoise(t * 0.07f, 15f) - 0.5f) * 8f * live;
            var target = lookAtPlayer && Camera.main != null ? Camera.main.transform : lookAt;
            bool reading = seated && typing && Time.time < restingUntil;       // a pause from typing: look up at the big screen
            if (target != null && (!seated || !typing || reading || speaking))
            {
                var to = transform.InverseTransformDirection(target.position - head.t.position);
                wantYaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -65f, 65f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
            }
            else if (seated && typing)
            {
                wantYaw *= 0.25f;
                wantPitch = 6f;                                                 // eyes on their own monitor
            }
            float ease = 1f - Mathf.Exp(-2.5f * dt);
            lookYaw = Mathf.Lerp(lookYaw, wantYaw, ease);
            lookPitch = Mathf.Lerp(lookPitch, wantPitch, ease);
            var qHead = qTorso * Quaternion.Euler(lookPitch - lean * 0.8f + stress * talk * 4f, lookYaw, 0f);
            Set(neck, root, Quaternion.Slerp(qTorso, qHead, 0.45f));
            Set(head, root, qHead);
            Set(jaw, root, qHead * Quaternion.Euler(jawOpen * 17f, 0f, 0f));

            // ---- Typing rhythm: bursts of keys, then a rest ----
            if (seated && typing && Time.time > typingUntil && Time.time > restingUntil)
            {
                if (Time.time - typingUntil < 0.1f) restingUntil = Time.time + Next(1.5f, 5f);
                else typingUntil = Time.time + Next(3f, 9f);
            }
            bool keysDown = seated && typing && Time.time < typingUntil && !speaking;

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 upper, fore;
                var shoulder = transform.InverseTransformPoint(upperArm[i].t.position);

                if (seated && typing)
                {
                    // Hands sit on the keyboard. Each hand hops to a new key now and then while typing.
                    if (keysDown && Time.time > nextKey[i])
                    {
                        nextKey[i] = Time.time + Next(0.12f, 0.45f);
                        keyTarget[i] = new Vector3(side * Next(0.05f, 0.2f), 0f, Next(-0.05f, 0.05f));
                    }
                    float tap = keysDown ? Mathf.Max(0f, Mathf.Sin((Time.time - nextKey[i]) * 28f)) * 0.012f : 0f;
                    var palm = keyboard + new Vector3(side * 0.02f, 0.035f + tap, -0.07f) + keyTarget[i] * (keysDown ? 1f : 0.4f);
                    Reach(shoulder, palm, upperArm[i].length, foreArm[i].length, new Vector3(side * 0.9f, -1f, -0.3f), out upper, out fore);
                }
                else if (seated)
                {
                    // Hands resting on the thighs.
                    var palm = new Vector3(side * 0.14f, seatHeight + 0.14f, 0.3f);
                    Reach(shoulder, palm, upperArm[i].length, foreArm[i].length, new Vector3(side, -0.6f, -0.6f), out upper, out fore);
                }
                else
                {
                    upper = qTorso * new Vector3(side * 0.16f, -1f, 0.02f);
                    fore = qTorso * new Vector3(side * 0.1f, -1f, 0.2f);
                }

                // Talking with the hands: they rise while a phrase is being said and beat on its stressed syllables.
                // The leading hand does most of it; the other follows smaller and a moment later.
                if (talk > 0.01f && !(seated && typing && !speaking))
                {
                    bool lead = i == leadHand;
                    float lift = phrase * (lead ? 1f : 0.45f) * talk;
                    float beat = stress * (lead ? 0.16f : 0.07f);
                    var gestureUpper = new Vector3(side * 0.28f, -1f, 0.32f);
                    var gestureFore = new Vector3(-side * 0.18f, 0.05f + beat * 2.2f, 1f + beat);
                    upper = Vector3.Slerp(upper.normalized, gestureUpper.normalized, lift);
                    fore = Vector3.Slerp(fore.normalized, gestureFore.normalized, lift);
                }

                // Offering something: the right hand comes out in front, at chest height.
                if (presenting && i == 1)
                    Reach(shoulder, new Vector3(0.2f, shoulder.y - 0.28f, 0.42f), upperArm[i].length, foreArm[i].length, new Vector3(0.7f, -1f, -0.2f), out upper, out fore);

                // Ease toward the new pose so nothing snaps.
                float follow = 1f - Mathf.Exp(-(keysDown ? 16f : 6f) * dt);
                upperNow[i] = upperNow[i] == Vector3.zero ? upper.normalized : Vector3.Slerp(upperNow[i], upper.normalized, follow);
                foreNow[i] = foreNow[i] == Vector3.zero ? fore.normalized : Vector3.Slerp(foreNow[i], fore.normalized, follow);
                Point(upperArm[i], root, upperNow[i]);
                var qFore = Point(foreArm[i], root, foreNow[i]);
                Set(hand[i], root, qFore * Quaternion.Euler(seated && typing ? 12f : 0f, 0f, 0f));

                // ---- Legs ----
                if (seated)
                {
                    Point(upperLeg[i], root, new Vector3(side * 0.1f, -0.06f, 1f));
                    Point(lowerLeg[i], root, new Vector3(side * 0.02f, -1f, 0.1f));
                }
                else
                {
                    Set(upperLeg[i], root, Quaternion.identity);
                    Set(lowerLeg[i], root, Quaternion.identity);
                }
                Set(foot[i], root, Quaternion.identity);
            }
        }

        /// <summary>Works out where the elbow has to be for the hand to reach a point (all in the character's own space).</summary>
        static void Reach(Vector3 shoulder, Vector3 palm, float upperLength, float foreLength, Vector3 elbowSide, out Vector3 upper, out Vector3 fore)
        {
            var toPalm = palm - shoulder;
            float distance = Mathf.Clamp(toPalm.magnitude, 0.05f, (upperLength + foreLength) * 0.995f);
            var direction = toPalm.normalized;
            float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) / (2f * distance);
            float out_ = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            var sideways = (elbowSide - Vector3.Dot(elbowSide, direction) * direction).normalized;
            var elbow = shoulder + direction * along + sideways * out_;
            upper = elbow - shoulder;
            fore = shoulder + direction * distance - elbow;
        }

        /// <summary>Rotates a bone by an offset given in the character's own space.</summary>
        static void Set(Bone bone, Quaternion root, Quaternion offset)
        {
            if (bone != null) bone.t.rotation = root * offset * bone.bind;
        }

        /// <summary>Swings a limb so it points along a direction given in the character's own space.</summary>
        static Quaternion Point(Bone bone, Quaternion root, Vector3 direction)
        {
            if (bone == null) return Quaternion.identity;
            var offset = Quaternion.FromToRotation(bone.direction, direction.normalized);
            bone.t.rotation = root * offset * bone.bind;
            return offset;
        }
    }
}
