using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The second pilot walking with the player. A simple follow with no pathfinding: it closes the distance in a
    /// straight line and stops a few steps away. Good enough for open placeholder levels.
    /// </summary>
    public class Companion : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float followDistance = 3.5f;
        [SerializeField] float speed = 5.5f;
        [Tooltip("Catch up instantly when left this far behind.")]
        [SerializeField] float teleportDistance = 40f;

        [field: SerializeField] public bool Following { get; set; } = true;

        void Update()
        {
            if (!Following || target == null) return;

            Vector3 offset = target.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance > teleportDistance)
                transform.position = target.position - target.forward * followDistance;
            else if (distance > followDistance)
                transform.position += offset.normalized * (Mathf.Min(speed * Time.deltaTime, distance - followDistance));

            if (distance > 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), 6f * Time.deltaTime);
        }
    }
}
