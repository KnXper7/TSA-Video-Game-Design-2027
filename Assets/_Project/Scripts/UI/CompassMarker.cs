using System.Collections.Generic;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// A place the player should head for. While this object is active it shows as a diamond on the compass line at
    /// the top of the screen, with its distance. Turn the object on and off to show and hide it.
    /// </summary>
    public class CompassMarker : MonoBehaviour
    {
        [SerializeField] Color color = new(1f, 0.72f, 0.25f);

        public static readonly List<CompassMarker> Active = new();
        public Color Color => color;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
