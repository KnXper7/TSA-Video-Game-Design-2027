using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>Flies the rocket forward, and can pull it into a gate (spinning and shrinking until it vanishes).</summary>
    public class RocketFlight : MonoBehaviour
    {
        [SerializeField] float speed = 12f;
        [SerializeField] Transform gate;
        [SerializeField] float pullDelay = 1.5f;
        [SerializeField] float pullDuration = 2.5f;

        bool flying;

        public void BeginApproach()
        {
            flying = true;
            AudioManager.StartLoop("engine", Sfx.Engine, 0.35f, 2f);
        }

        public void PullIntoGate() => StartCoroutine(Pull());

        void Update()
        {
            if (flying) transform.position += transform.forward * (speed * Time.deltaTime);
        }

        IEnumerator Pull()
        {
            yield return new WaitForSeconds(pullDelay);
            flying = false;
            AudioManager.Play(Sfx.Whoosh);
            AudioManager.SetLoop("engine", 0.6f, 1.4f);
            var startPosition = transform.position;
            var startScale = transform.localScale;
            for (float t = 0; t < pullDuration; t += Time.deltaTime)
            {
                float k = t / pullDuration;
                float eased = k * k;
                transform.position = Vector3.Lerp(startPosition, gate.position, eased);
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, eased);
                transform.Rotate(Vector3.forward, 540f * k * Time.deltaTime, Space.Self);
                yield return null;
            }
            AudioManager.StopLoop("engine", 0.2f);
            gameObject.SetActive(false);
        }
    }
}
