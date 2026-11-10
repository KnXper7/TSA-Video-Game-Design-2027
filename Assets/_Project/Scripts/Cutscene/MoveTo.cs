using UnityEngine;

namespace TSAGame
{
    /// <summary>Jumps an object to a marker. Cutscene shots call Go() to put props where the next shot needs them.</summary>
    public class MoveTo : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Transform destination;

        public void Go() => target.SetPositionAndRotation(destination.position, destination.rotation);
    }
}
