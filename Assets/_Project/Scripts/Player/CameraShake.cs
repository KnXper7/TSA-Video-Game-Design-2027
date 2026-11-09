using UnityEngine;

namespace TSAGame
{
    /// <summary>Shakes the camera around its resting local position. Put it on the camera itself, not the look pivot.</summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] float frequency = 22f;

        Vector3 restPosition;
        float constantStrength;
        float burstStrength;
        float burstTimeLeft;
        float burstDuration;

        void Awake() => restPosition = transform.localPosition;

        /// <summary>A shake that fades out over the duration.</summary>
        public void Shake(float strength, float duration)
        {
            burstStrength = strength;
            burstDuration = burstTimeLeft = duration;
        }

        /// <summary>A shake that keeps going until set back to 0 (for example, atmospheric entry).</summary>
        public void SetConstant(float strength) => constantStrength = strength;

        void LateUpdate()
        {
            float strength = constantStrength;
            if (burstTimeLeft > 0f)
            {
                burstTimeLeft -= Time.deltaTime;
                strength += burstStrength * (burstTimeLeft / burstDuration);
            }

            if (strength <= 0f)
            {
                transform.localPosition = restPosition;
                return;
            }

            float t = Time.time * frequency;
            transform.localPosition = restPosition + new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * strength,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * strength,
                0f);
        }
    }
}
