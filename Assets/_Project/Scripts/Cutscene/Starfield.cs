using UnityEngine;

namespace TSAGame
{
    /// <summary>Fills a sphere around this object with static star particles. Needs a ParticleSystem with an unlit particle material.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class Starfield : MonoBehaviour
    {
        [SerializeField] int starCount = 1500;
        [SerializeField] float radius = 3000f;
        [SerializeField] Vector2 sizeRange = new(2f, 9f);
        [SerializeField] int seed = 2140;

        const float Lifetime = 1e6f;

        void Start()
        {
            var system = GetComponent<ParticleSystem>();
            var main = system.main;
            main.maxParticles = starCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            system.Play();
            var random = new System.Random(seed);
            var stars = new ParticleSystem.Particle[starCount];
            for (int i = 0; i < starCount; i++)
            {
                var direction = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f).normalized;
                float brightness = 0.5f + (float)random.NextDouble() * 0.5f;
                stars[i].position = transform.position + direction * radius;
                stars[i].startSize = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)random.NextDouble());
                stars[i].startColor = new Color(brightness, brightness, Mathf.Min(1f, brightness + 0.1f));
                stars[i].remainingLifetime = Lifetime;
                stars[i].startLifetime = Lifetime;
            }
            system.SetParticles(stars, starCount);
        }
    }
}
