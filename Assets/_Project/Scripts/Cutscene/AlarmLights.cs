using UnityEngine;

namespace TSAGame
{
    /// <summary>Red alarm lights that pulse, while the room's normal lights dim. Optional looping alarm sound.</summary>
    public class AlarmLights : MonoBehaviour
    {
        [SerializeField] Light[] alarmLights;
        [SerializeField] Light[] normalLights;
        [SerializeField] float alarmIntensity = 6f;
        [SerializeField] float pulseSpeed = 4f;
        [SerializeField, Range(0, 1)] float dimNormalTo = 0.3f;
        [SerializeField] AudioSource alarmSound;
        [SerializeField] bool startOn;

        float[] normalIntensities;
        bool active;

        void Awake()
        {
            normalIntensities = new float[normalLights.Length];
            for (int i = 0; i < normalLights.Length; i++) normalIntensities[i] = normalLights[i].intensity;
            if (startOn) Trigger(); else Stop();
        }

        public void Trigger()
        {
            active = true;
            for (int i = 0; i < normalLights.Length; i++) normalLights[i].intensity = normalIntensities[i] * dimNormalTo;
            if (alarmSound != null && alarmSound.clip != null) alarmSound.Play();
            else AudioManager.StartLoop("alarm", Sfx.Alarm, 0.55f, 0.1f);
        }

        public void Stop()
        {
            active = false;
            foreach (var l in alarmLights) l.intensity = 0f;
            for (int i = 0; i < normalLights.Length; i++) normalLights[i].intensity = normalIntensities[i];
            if (alarmSound != null && alarmSound.clip != null) alarmSound.Stop();
            else AudioManager.StopLoop("alarm", 0.4f);
        }

        void Update()
        {
            if (!active) return;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            foreach (var l in alarmLights) l.intensity = alarmIntensity * pulse;
        }
    }
}
