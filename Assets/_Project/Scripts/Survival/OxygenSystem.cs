using System;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// Suit oxygen. Drains while Draining is on; a higher Survival stat makes it last longer.
    /// Raises Depleted once when it runs out.
    /// </summary>
    public class OxygenSystem : MonoBehaviour
    {
        [SerializeField] float capacity = 100f;
        [Tooltip("Units per second at Survival 2.")]
        [SerializeField] float baseDrainPerSecond = 0.5f;
        [Tooltip("Each Survival point above 2 cuts the drain by this fraction.")]
        [SerializeField, Range(0f, 0.2f)] float reductionPerSurvivalPoint = 0.1f;

        public event Action Depleted;

        public float Current { get; private set; }
        public float Normalized => Current / capacity;
        public bool Draining { get; set; }

        bool depleted;

        void Awake() => Current = capacity;

        public float DrainRate
        {
            get
            {
                int survival = GameState.Instance != null ? GameState.Instance.Get(Stat.Survival) : 2;
                float multiplier = Mathf.Max(0.3f, 1f - reductionPerSurvivalPoint * (survival - 2));
                return baseDrainPerSecond * multiplier;
            }
        }

        public void Refill(float amount)
        {
            Current = Mathf.Min(capacity, Current + amount);
            depleted = false;
        }

        void Update()
        {
            if (!Draining || depleted) return;

            Current = Mathf.Max(0f, Current - DrainRate * Time.deltaTime);
            if (HUD.Instance != null) HUD.Instance.SetOxygen(Normalized);

            if (Current <= 0f)
            {
                depleted = true;
                Depleted?.Invoke();
            }
        }
    }
}
