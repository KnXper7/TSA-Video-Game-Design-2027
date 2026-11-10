using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Fills a box with hundreds of small tumbling rocks, drawn cheaply as mesh particles.
    /// Needs a ParticleSystem whose renderer is set to Mesh with a rock mesh and material.
    /// A straight lane can be kept clear (for a rocket's flight path).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class DebrisField : MonoBehaviour
    {
        [SerializeField] int count = 1200;
        [SerializeField] Vector3 size = new(600f, 250f, 900f);
        [SerializeField] Vector2 sizeRange = new(0.15f, 1.6f);
        [SerializeField] int seed = 77;
        [Tooltip("Rocks slowly drift this fast (metres per second), each in its own direction.")]
        [SerializeField] float drift = 0.15f;
        [Tooltip("Keep a tunnel along Z clear around this local X/Y point. 0 radius = no lane.")]
        [SerializeField] Vector2 clearLane;
        [SerializeField] float clearRadius;

        const float Lifetime = 1e6f;

        void Start()
        {
            var system = GetComponent<ParticleSystem>();
            var main = system.main;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Lifetime;
            main.startRotation3D = true;
            main.playOnAwake = false;
            var emission = system.emission;
            emission.enabled = false;
            system.Play();

            var random = new System.Random(seed);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var rocks = new ParticleSystem.Particle[count];
            int made = 0;
            for (int i = 0; i < count * 2 && made < count; i++)
            {
                var local = new Vector3(Next(-0.5f, 0.5f) * size.x, Next(-0.5f, 0.5f) * size.y, Next(-0.5f, 0.5f) * size.z);
                if (clearRadius > 0f && new Vector2(local.x - clearLane.x, local.y - clearLane.y).magnitude < clearRadius) continue;
                float small = Next(0f, 1f);
                rocks[made].position = transform.TransformPoint(local);
                rocks[made].startSize = Mathf.Lerp(sizeRange.x, sizeRange.y, small * small * small);      // mostly small ones
                rocks[made].rotation3D = new Vector3(Next(0, 360), Next(0, 360), Next(0, 360));
                rocks[made].angularVelocity3D = new Vector3(Next(-8, 8), Next(-8, 8), Next(-8, 8));
                rocks[made].velocity = new Vector3(Next(-1, 1), Next(-1, 1), Next(-1, 1)) * drift;
                rocks[made].startColor = Color.white;
                rocks[made].startLifetime = rocks[made].remainingLifetime = Lifetime;
                made++;
            }
            system.SetParticles(rocks, made);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.4f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
