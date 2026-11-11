using System;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// What happens when the grip ball hits this object. Anything without this component is plain rock or metal:
    /// the ball grips it. These are the pieces the ball puzzles are built from:
    ///   Frost  - a glassy frozen coating. Nothing grips it: the ball skips off at the mirror angle. Use it to
    ///            bank a throw round a corner.
    ///   Loose  - not fixed to anything. The ball can't hold it, but the hit shoves it away. Use it to clear a path.
    ///   Target - a switch. Hitting it fires its event (opens, powers or counts something).
    /// </summary>
    public class BallSurface : MonoBehaviour
    {
        public enum Kind { Grip, Frost, Loose, Target }

        [SerializeField] Kind kind = Kind.Frost;
        [Tooltip("Loose only: how fast a hit sends it away, in metres per second.")]
        [SerializeField] float shove = 5f;
        [Tooltip("Loose only, optional: the hit always carries it away from this point as well (so it can't be driven into the thing it is blocking).")]
        [SerializeField] Transform awayFrom;
        [Tooltip("Loose only: once knocked over it no longer stops the ball, wherever it ends up lying.")]
        [SerializeField] bool clearedOnceHit;

        [Tooltip("Target only, optional: this part is knocked flat when the target is hit. Leave empty to make the target vanish instead.")]
        [SerializeField] Transform tips;
        [Tooltip("Target only: stays where it is after being hit (a switch on a wall, not a knock-down target).")]
        [SerializeField] bool staysPut;

        public Kind Type => kind;
        public bool WasHit { get; private set; }
        public event Action Hit;

        /// <summary>Knocks the target over backwards, with a small bounce as it lands.</summary>
        System.Collections.IEnumerator Tip()
        {
            var from = tips.localRotation;
            var to = from * Quaternion.Euler(-84f, 0f, 0f);
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                float eased = k < 0.7f ? Mathf.Pow(k / 0.7f, 2f) : 1f - 0.08f * Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI);
                tips.localRotation = Quaternion.SlerpUnclamped(from, to, eased);
                yield return null;
            }
            tips.localRotation = to;
        }

        public void Strike(Vector3 ballVelocity, Vector3 point)
        {
            if (kind == Kind.Grip || kind == Kind.Frost) return;
            if (kind == Kind.Loose)
            {
                var body = GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = false;       // things standing on edge are held still until they are hit
                    var push = ballVelocity.normalized;
                    if (awayFrom != null) push = (push * 0.4f + (transform.position - awayFrom.position).normalized).normalized;
                    body.linearVelocity = push * shove;
                    body.angularVelocity += UnityEngine.Random.insideUnitSphere * 1.5f;
                    if (clearedOnceHit) gameObject.layer = 2;      // the ball ignores this layer; it still lands on the ground
                }
            }
            else if (!WasHit)
            {
                AudioManager.Play(Sfx.Success, 0.7f);
                if (staysPut) { }
                else if (tips != null) StartCoroutine(Tip());
                else foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;      // the target drops away
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            WasHit = true;
            Hit?.Invoke();
        }
    }
}
