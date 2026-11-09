using UnityEngine;

namespace TSAGame
{
    /// <summary>Slowly rotates an object (planets, moons).</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new(0f, 2f, 0f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
