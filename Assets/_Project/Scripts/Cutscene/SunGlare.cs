using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The visible sun: a glow, a hot core and a wide lens streak, kept far away in a fixed direction and always
    /// turned to face the camera. Rocks and ships pass in front of it. Resize or recolour the child quads freely.
    /// </summary>
    public class SunGlare : MonoBehaviour
    {
        [Tooltip("The light this glare belongs to: the glare sits where that light comes from.")]
        [SerializeField] Light sun;
        [SerializeField] float distance = 3000f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || sun == null) return;
            transform.SetPositionAndRotation(cam.transform.position - sun.transform.forward * distance, cam.transform.rotation);
        }
    }
}
