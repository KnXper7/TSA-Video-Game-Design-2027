using UnityEngine;

namespace TSAGame
{
    /// <summary>Keeps a camera aimed at a target from a fixed offset (used for the tracking feed).</summary>
    public class TrackTarget : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(8f, 3f, -14f);
        [SerializeField] float smoothing = 3f;

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(target.position - transform.position);
        }
    }
}
