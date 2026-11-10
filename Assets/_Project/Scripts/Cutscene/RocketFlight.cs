using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Flies the rocket forward with its engines burning, and can pull it into a gate.
    /// The engine sound gets louder as the rocket nears the camera, so a fly-by sounds like one.
    /// </summary>
    public class RocketFlight : MonoBehaviour
    {
        [SerializeField] float speed = 34f;
        [SerializeField] float rollSpeed = 2.5f;
        [SerializeField] Transform gate;
        [SerializeField] float pullDelay = 1.5f;
        [SerializeField] float pullDuration = 5.5f;
        [SerializeField] ParticleSystem[] exhaust;
        [Tooltip("Distance at which the engines are at full volume.")]
        [SerializeField] float fullVolumeDistance = 45f;

        bool flying, pulling;

        public void BeginApproach()
        {
            flying = true;
            foreach (var plume in exhaust) plume.Play();
            AudioManager.StartLoop("engine", Sfx.Roar, 0.05f, 0.2f);
        }

        public void PullIntoGate() => StartCoroutine(Pull());

        void Update()
        {
            if (flying)
            {
                transform.position += transform.forward * (speed * Time.deltaTime);
                transform.Rotate(0f, 0f, rollSpeed * Time.deltaTime, Space.Self);
            }
            if (!flying && !pulling) return;

            var listener = Camera.main;
            if (listener == null) return;
            float nearness = Mathf.Clamp01(fullVolumeDistance / Vector3.Distance(listener.transform.position, transform.position));
            AudioManager.SetLoop("engine", Mathf.Lerp(0.03f, 1f, nearness * nearness), pulling ? 1.25f : 1f);
        }

        IEnumerator Pull()
        {
            yield return new WaitForSeconds(pullDelay);
            flying = false;
            pulling = true;
            AudioManager.Play(Sfx.Whoosh);
            var start = transform.position;
            for (float t = 0; t < pullDuration; t += Time.deltaTime)
            {
                float k = t / pullDuration;
                transform.position = Vector3.Lerp(start, gate.position, k * k * k);
                transform.Rotate(0f, 0f, 160f * k * Time.deltaTime, Space.Self);
                yield return null;
            }
            pulling = false;
            AudioManager.StopLoop("engine", 0.2f);
            gameObject.SetActive(false);
        }
    }
}
