using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The portal: a machined ring with two thin rings spinning around it and a swirling vortex in the middle.
    /// Appear() makes it grow out of nothing. Swap the ring model freely; the vortex is a quad using the TSA/PortalVortex shader.
    /// </summary>
    public class PortalGate : MonoBehaviour
    {
        [SerializeField] Transform ringMain;
        [SerializeField] Transform ringOuter;
        [SerializeField] Transform ringInner;
        [SerializeField] Renderer vortex;
        [SerializeField] Light glow;
        [SerializeField] float glowIntensity = 60f;
        [SerializeField] float appearDuration = 3.5f;
        [SerializeField] bool startOpen;

        static readonly int OpenId = Shader.PropertyToID("_Open");

        Vector3 fullScale;
        MaterialPropertyBlock block;
        float open;

        void Awake()
        {
            fullScale = transform.localScale;
            block = new MaterialPropertyBlock();
            Set(startOpen ? 1f : 0f);
        }

        public void Appear()
        {
            AudioManager.Play(Sfx.PortalOpen);
            AudioManager.StartLoop("gate", Sfx.GateHum, 0.7f, appearDuration);
            StartCoroutine(Animate());
        }

        IEnumerator Animate()
        {
            for (float t = 0; t < appearDuration; t += Time.deltaTime)
            {
                // Overshoots a little and settles, like something snapping into place.
                float k = t / appearDuration - 1f;
                Set(Mathf.Max(0f, 1f + 2.2f * k * k * k + 1.2f * k * k));
                yield return null;
            }
            Set(1f);
        }

        void Set(float amount)
        {
            open = amount;
            transform.localScale = fullScale * Mathf.Max(0.0001f, amount);
            if (glow != null) glow.intensity = glowIntensity * amount;
            if (vortex != null)
            {
                block.SetFloat(OpenId, Mathf.Clamp01(amount));
                vortex.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (open <= 0f) return;
            var axis = transform.forward;
            if (ringMain != null) ringMain.Rotate(axis, 2f * Time.deltaTime, Space.World);
            if (ringOuter != null) ringOuter.Rotate(axis, 14f * Time.deltaTime, Space.World);
            if (ringInner != null) ringInner.Rotate(axis, -30f * Time.deltaTime, Space.World);
        }
    }
}
