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

        public Kind Type => kind;
        public bool WasHit { get; private set; }
        public event Action Hit;

        public void Strike(Vector3 ballVelocity, Vector3 point)
        {
            if (kind == Kind.Grip || kind == Kind.Frost) return;
            if (kind == Kind.Loose)
            {
                var body = GetComponent<Rigidbody>();
                if (body != null)
                {
                    var push = ballVelocity.normalized;
                    if (awayFrom != null) push = (push * 0.4f + (transform.position - awayFrom.position).normalized).normalized;
                    body.linearVelocity = push * shove;
                    body.angularVelocity += UnityEngine.Random.insideUnitSphere * 1.5f;
                }
            }
            else if (!WasHit)
            {
                AudioManager.Play(Sfx.Success, 0.7f);
                foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;      // the target drops away
            }
            WasHit = true;
            Hit?.Invoke();
        }
    }
}
