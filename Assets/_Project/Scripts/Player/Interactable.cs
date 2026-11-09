using System;
using UnityEngine;
using UnityEngine.Events;

namespace TSAGame
{
    /// <summary>
    /// Anything the player can use by looking at it and pressing Interact. Set a hold duration for "hold to scan" style actions.
    /// Needs a collider on this object or a child.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [SerializeField] string prompt = "Use";
        [Tooltip("Seconds Interact must be held. 0 = a single press.")]
        [SerializeField, Min(0f)] float holdDuration;
        [SerializeField] bool singleUse = true;
        [Tooltip("Hide the object after it is used (pickups).")]
        [SerializeField] bool hideOnUse;
        [SerializeField] UnityEvent onInteract = new();

        public event Action<Interactable> Interacted;

        bool used;

        public string Prompt => prompt;
        public float HoldDuration => holdDuration;
        public bool IsAvailable => isActiveAndEnabled && !IsUsedUp;
        public bool IsUsedUp => singleUse && used;

        public void SetPrompt(string newPrompt) => prompt = newPrompt;

        /// <summary>Lets a single-use object be used again.</summary>
        public void ResetUse() => used = false;

        public void Interact()
        {
            if (!IsAvailable) return;
            used = true;
            AudioManager.Play(Sfx.Interact, 0.7f);
            onInteract?.Invoke();
            Interacted?.Invoke(this);
            if (hideOnUse) gameObject.SetActive(false);
        }
    }
}
