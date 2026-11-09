using System;
using UnityEngine;
using UnityEngine.Events;

namespace TSAGame
{
    /// <summary>Fires once when the player walks into this trigger collider.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        [SerializeField] bool singleUse = true;
        [SerializeField] UnityEvent onEntered = new();

        public event Action Entered;

        bool fired;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (fired && singleUse) return;
            if (other.GetComponentInParent<FirstPersonController>() == null) return;
            fired = true;
            onEntered?.Invoke();
            Entered?.Invoke();
        }
    }
}
