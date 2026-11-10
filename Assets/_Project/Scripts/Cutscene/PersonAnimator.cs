using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Brings a rigged person to life without animation clips: standing or seated, breathing, shifting weight,
    /// looking around or at a target, typing, and gesturing, nodding and moving the jaw while their speaker has a
    /// line of dialogue. Works on any rig that uses the usual bone names (Hips, Spine, Head, LeftArm, LeftForeArm...).
    /// To use real animation clips instead, remove this component and add an Animator Controller.
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
        [Tooltip("Seated only: hands hover over a keyboard and tap.")]
        [SerializeField] bool typing = true;
        [SerializeField] float seatHeight = 0.5f;
        [Tooltip("How much idle movement. 0 = statue, 1 = normal, 2 = restless.")]
        [SerializeField, Range(0f, 2f)] float liveliness = 1f;
        [Tooltip("Tint for the clothes, so copies of one character don't all look the same. White = unchanged.")]
        [SerializeField] Color clothesTint = Color.white;

        class Bone
        {
            public Transform t;
            public Quaternion bind;      // rotation in the character's own space, in the rest pose
            public Vector3 direction;    // which way the limb points in the rest pose (toward its child)
        }

        Bone hips, spine, spine1, spine2, neck, head, jaw;
        readonly Bone[] upperArm = new Bone[2], foreArm = new Bone[2], hand = new Bone[2];
        readonly Bone[] upperLeg = new Bone[2], lowerLeg = new Bone[2], foot = new Bone[2];
        Vector3 hipsRest;
        float phase, talk, jawOpen, lookYaw, lookPitch;
        bool ready;

        void Awake()
        {
            phase = Mathf.Abs(transform.position.GetHashCode() % 1000) * 0.37f;
            hips = Find("Hips"); spine = Find("Spine"); spine1 = Find("Spine1"); spine2 = Find("Spine2");
            neck = Find("Neck"); head = Find("Head"); jaw = Find("Jaw");
            string[] sides = { "Left", "Right" };
            for (int i = 0; i < 2; i++)
            {
                upperArm[i] = Find(sides[i] + "Arm"); foreArm[i] = Find(sides[i] + "ForeArm"); hand[i] = Find(sides[i] + "Hand");
                upperLeg[i] = Find(sides[i] + "UpLeg"); lowerLeg[i] = Find(sides[i] + "Leg"); foot[i] = Find(sides[i] + "Foot");
                Aim(upperArm[i], foreArm[i]); Aim(foreArm[i], hand[i]); Aim(upperLeg[i], lowerLeg[i]); Aim(lowerLeg[i], foot[i]);
            }
            ready = hips != null && head != null && upperArm[0] != null && upperArm[1] != null && upperLeg[0] != null && upperLeg[1] != null;
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
            if (bone != null && child != null)
                bone.direction = transform.InverseTransformDirection(child.t.position - bone.t.position).normalized;
        }

        void LateUpdate()
        {
            if (!ready) return;
            float t = Time.time + phase;
            float live = liveliness;
            var root = transform.rotation;
            bool seated = pose == Pose.Seated;

            // Talking: ease in and out, and open the jaw with the loudness of the voice.
            bool speaking = !string.IsNullOrEmpty(speaker) && Dialogue.IsPlaying && Dialogue.CurrentSpeaker == speaker;
            talk = Mathf.MoveTowards(talk, speaking ? 1f : 0f, Time.deltaTime * 3f);
            float voice = speaking ? Mathf.Clamp01(AudioManager.VoiceLevel * 9f) : 0f;
            if (speaking && voice <= 0.001f) voice = 0.35f + 0.35f * Mathf.Sin(t * 17f);      // no recording: just flap
            jawOpen = Mathf.Lerp(jawOpen, voice, 1f - Mathf.Exp(-22f * Time.deltaTime));

            // Hips and torso: weight shift and breathing.
            float sway = (Mathf.PerlinNoise(t * 0.13f, 3f) - 0.5f) * (seated ? 2f : 5f) * live;
            float breath = Mathf.Sin(t * 1.5f) * 1.2f * live;
            var qHips = Quaternion.Euler(0f, sway * 0.6f, sway);
            Set(hips, root, qHips);
            hips.t.position = seated
                ? transform.TransformPoint(new Vector3(hipsRest.x, seatHeight + 0.1f, hipsRest.z - 0.08f))
                : transform.TransformPoint(hipsRest + new Vector3(sway * 0.004f, -Mathf.Abs(sway) * 0.0015f, 0f));
            float lean = (seated ? 7f : 0f) + talk * 2f * Mathf.Sin(t * 2.3f);
            var qTorso = qHips * Quaternion.Euler(lean + breath, -sway * 0.5f, -sway * 0.7f);
            Set(spine, root, Quaternion.Slerp(qHips, qTorso, 0.4f));
            Set(spine1, root, Quaternion.Slerp(qHips, qTorso, 0.7f));
            Set(spine2, root, qTorso);

            // Head: toward a target if there is one, otherwise drifting; nods while talking.
            float wantYaw = (Mathf.PerlinNoise(t * 0.21f, 9f) - 0.5f) * 40f * live;
            float wantPitch = (Mathf.PerlinNoise(t * 0.17f, 15f) - 0.5f) * 14f * live;
            var target = lookAtPlayer && Camera.main != null ? Camera.main.transform : lookAt;
            if (target != null)
            {
                var to = transform.InverseTransformDirection(target.position - head.t.position);
                wantYaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -65f, 65f) + wantYaw * 0.15f;
                wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -30f, 30f) + wantPitch * 0.2f;
            }
            float ease = 1f - Mathf.Exp(-3f * Time.deltaTime);
            lookYaw = Mathf.Lerp(lookYaw, wantYaw, ease);
            lookPitch = Mathf.Lerp(lookPitch, wantPitch, ease);
            float nod = talk * (Mathf.Sin(t * 5.1f) * 3f + Mathf.Sin(t * 2.7f) * 2f);
            var qHead = qTorso * Quaternion.Euler(lookPitch + nod - (seated ? 5f : 0f), lookYaw, talk * Mathf.Sin(t * 1.9f) * 3f);
            Set(neck, root, Quaternion.Slerp(qTorso, qHead, 0.45f));
            Set(head, root, qHead);
            Set(jaw, root, qHead * Quaternion.Euler(jawOpen * 13f, 0f, 0f));

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float wave = Mathf.PerlinNoise(t * 0.3f + i * 7f, 21f) - 0.5f;

                // Arms
                Vector3 upper, fore;
                if (seated && typing)
                {
                    float tap = (Mathf.PerlinNoise(t * 6f + i * 3f, 5f) - 0.5f) * 0.12f * live;
                    upper = new Vector3(side * 0.22f, -1f, 0.38f);
                    fore = new Vector3(-side * 0.12f, -0.1f + tap, 1f);
                }
                else if (seated)
                {
                    upper = new Vector3(side * 0.2f, -1f, 0.3f);
                    fore = new Vector3(-side * 0.25f, -0.45f, 1f);
                }
                else
                {
                    upper = new Vector3(side * 0.17f, -1f, 0.03f + wave * 0.08f * live);
                    fore = new Vector3(side * 0.1f, -1f, 0.24f + wave * 0.12f * live);
                }
                // Talking: one hand, then the other, comes up to make a point.
                float gesture = talk * Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(t * 0.9f + i * 11f, 33f) * 1.6f - 0.3f);
                if (seated && typing) gesture *= 0.5f;
                upper = Vector3.Slerp(upper.normalized, new Vector3(side * 0.3f, -1f, 0.45f).normalized, gesture);
                fore = Vector3.Slerp(fore.normalized, new Vector3(-side * 0.1f + Mathf.Sin(t * 3.3f + i) * 0.2f, 0.25f, 1f).normalized, gesture);
                var qUpper = Point(upperArm[i], root, qTorso, upper);
                var qFore = Point(foreArm[i], root, qTorso, fore);
                Set(hand[i], root, qFore);

                // Legs
                if (seated)
                {
                    Point(upperLeg[i], root, Quaternion.identity, new Vector3(side * 0.1f, -0.06f, 1f));
                    Point(lowerLeg[i], root, Quaternion.identity, new Vector3(side * 0.02f, -1f, 0.1f));
                    Set(foot[i], root, Quaternion.identity);
                }
                else
                {
                    Set(upperLeg[i], root, Quaternion.identity);
                    Set(lowerLeg[i], root, Quaternion.identity);
                    Set(foot[i], root, Quaternion.identity);
                }
            }
        }

        /// <summary>Rotates a bone by an offset given in the character's own space.</summary>
        static void Set(Bone bone, Quaternion root, Quaternion offset)
        {
            if (bone != null) bone.t.rotation = root * offset * bone.bind;
        }

        /// <summary>Swings a limb so it points along a direction given in the character's own space.</summary>
        static Quaternion Point(Bone bone, Quaternion root, Quaternion body, Vector3 direction)
        {
            if (bone == null) return Quaternion.identity;
            var offset = body * Quaternion.FromToRotation(bone.direction, direction.normalized);
            bone.t.rotation = root * offset * bone.bind;
            return offset;
        }
    }
}
