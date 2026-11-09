using UnityEngine;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>The big Mission Control screen: a live camera feed plus a status line. Cutting the feed shows SIGNAL LOST.</summary>
    public class MonitorScreen : MonoBehaviour
    {
        [SerializeField] Renderer screen;
        [SerializeField] Material lostSignalMaterial;
        [SerializeField] Text statusText;
        [SerializeField] string trackingStatus = "TRACKING  •  DISTANCE 40,000 KM";

        bool lost;

        void Start()
        {
            if (statusText != null) statusText.text = trackingStatus;
        }

        public void ShowAlert()
        {
            if (statusText == null) return;
            statusText.text = "WARNING  •  GRAVITY ANOMALY";
            statusText.color = new Color(1f, 0.75f, 0.2f);
        }

        public void CutFeed()
        {
            lost = true;
            if (screen != null && lostSignalMaterial != null) screen.sharedMaterial = lostSignalMaterial;
            if (statusText != null)
            {
                statusText.text = "SIGNAL LOST";
                statusText.color = new Color(1f, 0.25f, 0.2f);
            }
        }

        void Update()
        {
            if (lost && statusText != null) statusText.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
        }
    }
}
