using UnityEngine;

namespace TSAGame
{
    /// <summary>A light that flickers violently (the flash of the portal through a window, sparks from damage).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] float minIntensity = 0.5f;
        [SerializeField] float maxIntensity = 8f;
        [SerializeField] float speed = 14f;

        Light target;
        float seed;

        void Awake()
        {
            target = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update() => target.intensity = Mathf.Lerp(minIntensity, maxIntensity, Mathf.PerlinNoise(seed, Time.time * speed));
    }
}
