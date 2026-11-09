using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>A glowing ring that irises open. The ring is drawn with a LineRenderer; the core is any mesh (a disc placeholder).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GateRing : MonoBehaviour
    {
        [SerializeField] float radius = 14f;
        [SerializeField] int segments = 96;
        [SerializeField] float openDuration = 2.5f;
        [SerializeField] float ringWidth = 1.2f;
        [SerializeField] Transform core;
        [SerializeField] Light glow;
        [SerializeField] float glowIntensity = 40f;
        [SerializeField] float spinSpeed = 20f;

        LineRenderer line;
        float openAmount;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            SetOpen(0f);
        }

        public void Open()
        {
            AudioManager.Play(Sfx.Whoosh, 0.8f);
            AudioManager.StartLoop("gate", Sfx.GateHum, 0.7f, openDuration);
            StartCoroutine(Animate(1f));
        }
        public void Close() => StartCoroutine(Animate(0f));

        IEnumerator Animate(float target)
        {
            float start = openAmount;
            for (float t = 0; t < openDuration; t += Time.deltaTime)
            {
                SetOpen(Mathf.SmoothStep(start, target, t / openDuration));
                yield return null;
            }
            SetOpen(target);
        }

        void Update()
        {
            if (openAmount <= 0f) return;
            transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
            SetOpen(openAmount); // re-draw for the ripple
        }

        void SetOpen(float amount)
        {
            openAmount = amount;
            line.widthMultiplier = ringWidth * amount;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float ripple = 1f + 0.03f * Mathf.Sin(angle * 6f + Time.time * 3f);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * amount * ripple));
            }
            if (core != null) core.localScale = new Vector3(radius * 1.9f, radius * 1.9f, 0.1f) * amount;
            if (glow != null) glow.intensity = glowIntensity * amount;
        }
    }
}
