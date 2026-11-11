using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSAGame
{
    /// <summary>Names of every built-in sound. Use these with AudioManager.Sfx / StartLoop.</summary>
    public static class Sfx
    {
        // One-shots
        public const string Click = "click";
        public const string Blip = "blip";
        public const string Interact = "interact";
        public const string PowerUp = "power_up";
        public const string PowerDown = "power_down";
        public const string Success = "success";
        public const string Error = "error";
        public const string Pickup = "pickup";
        public const string LevelUp = "level_up";
        public const string Static = "static";
        public const string Whoosh = "whoosh";
        public const string Impact = "impact";
        public const string Footstep = "footstep";
        public const string Warning = "warning";
        public const string Tick = "tick";
        public const string Door = "door";
        public const string Stinger = "stinger";
        public const string Beep = "beep";
        public const string Throw = "throw";
        public const string Clank = "clank";
        public const string Thud = "thud";
        public const string PortalOpen = "portal_open";
        public const string Explosion = "explosion";
        public const string Ricochet = "ricochet";

        // Loops
        public const string Alarm = "alarm";
        public const string Engine = "engine";
        public const string Rumble = "rumble";
        public const string GateHum = "gate_hum";
        public const string Machinery = "machinery";
        public const string RoomHum = "room_hum";
        public const string ShipHum = "ship_hum";
        public const string Wind = "wind";
        public const string SpaceDrone = "space_drone";
        public const string Roar = "rocket_roar";
        public const string Reel = "reel";
        public const string Breath = "breath";
        public const string PadTense = "pad_tense";
        public const string PadCalm = "pad_calm";
        public const string PadMystery = "pad_mystery";
        public const string PadDark = "pad_dark";
    }

    /// <summary>
    /// Placeholder sound effects and music, synthesized in code the first time each is used, so the game has
    /// sound without any audio files. To use a real recording instead, put a clip with the same name
    /// (for example "alarm.wav") in Assets/_Project/Resources/Sfx/ and it takes over automatically.
    /// </summary>
    public static class SoundLibrary
    {
        const int Rate = 22050;

        static readonly Dictionary<string, AudioClip> cache = new();
        static System.Random random = new(2140);

        public static AudioClip Get(string name)
        {
            // A cached clip can be destroyed between Play sessions (scripts aren't reloaded), so check it's still alive.
            if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>("Sfx/" + name) ?? Build(name);
            if (clip != null) cache[name] = clip;
            return clip;
        }

        static AudioClip Build(string name)
        {
            float[] data = name switch
            {
                Sfx.Click => Tone(1800f, 0.04f, Wave.Sine, 0.001f, 0.035f, 0.35f),
                Sfx.Blip => Tone(950f, 0.03f, Wave.Square, 0.002f, 0.02f, 0.06f),
                Sfx.Tick => Tone(2400f, 0.025f, Wave.Square, 0.001f, 0.02f, 0.12f),
                Sfx.Interact => Concat(Tone(660f, 0.06f, Wave.Sine, 0.002f, 0.04f, 0.35f), Tone(990f, 0.08f, Wave.Sine, 0.002f, 0.07f, 0.35f)),
                Sfx.PowerUp => Sweep(300f, 900f, 0.18f, Wave.Triangle, 0.4f),
                Sfx.PowerDown => Sweep(700f, 250f, 0.18f, Wave.Triangle, 0.4f),
                Sfx.Success => Concat(Note(523.25f, 0.11f), Note(659.25f, 0.11f), Note(783.99f, 0.11f), Note(1046.5f, 0.35f)),
                Sfx.Error => Mul(Tone(140f, 0.4f, Wave.Saw, 0.01f, 0.1f, 0.35f), Lfo(0.4f, 18f, 0.6f)),
                Sfx.Pickup => Sweep(1100f, 1700f, 0.14f, Wave.Sine, 0.3f),
                Sfx.LevelUp => Concat(Note(523.25f, 0.1f), Note(659.25f, 0.1f), Note(783.99f, 0.1f), Note(1046.5f, 0.1f), Note(1318.5f, 0.45f)),
                Sfx.Warning => Concat(Tone(1000f, 0.09f, Wave.Square, 0.002f, 0.02f, 0.18f), Silence(0.08f), Tone(1000f, 0.09f, Wave.Square, 0.002f, 0.02f, 0.18f)),
                Sfx.Static => Envelope(Noise(0.9f, 1f, 0.5f), 0.005f, 0.5f),
                Sfx.Whoosh => Whoosh(2.2f),
                Sfx.Impact => Impact(1.6f),
                Sfx.Footstep => Envelope(Noise(0.09f, 0.25f, 0.5f), 0.003f, 0.08f),
                Sfx.Door => Mix(Sweep(120f, 60f, 0.7f, Wave.Saw, 0.25f), Envelope(Noise(0.7f, 0.08f, 0.3f), 0.01f, 0.4f)),
                Sfx.Stinger => Envelope(Chord(2.5f, 0.25f, 110f, 164.81f, 220f, 261.63f), 0.02f, 1.6f),

                Sfx.Beep => Tone(880f, 0.16f, Wave.Sine, 0.004f, 0.05f, 0.3f),
                Sfx.Ricochet => Tone(2600f, 0.25f, Wave.Sine, 0.001f, 0.22f, 0.3f),
                Sfx.Throw => Envelope(Mul(Noise(0.28f, 0.35f, 0.5f), Lfo(0.28f, 3.5f, 0.9f)), 0.01f, 0.2f),
                Sfx.Clank => Clank(0.7f),
                Sfx.Thud => Envelope(Mix(Tone(70f, 0.3f, Wave.Sine, 0f, 0f, 0.6f), Noise(0.3f, 0.05f, 0.4f)), 0.002f, 0.25f),
                Sfx.PortalOpen => Mix(Sweep(40f, 320f, 3.2f, Wave.Saw, 0.16f), Envelope(Chord(3.6f, 0.07f, 110f, 165f, 220.5f, 331f), 1.2f, 1.5f), Whoosh(3.2f)),
                Sfx.Explosion => Mix(Impact(2.6f), Envelope(Noise(2.6f, 0.02f, 0.9f), 0.005f, 2.3f), Envelope(Noise(0.5f, 0.6f, 0.5f), 0.001f, 0.45f)),

                // A rocket engine heard from the ground: deep rumble, a mid roar and fast crackle on top.
                Sfx.Roar => Loop(Mix(Noise(6.5f, 0.012f, 0.75f), Noise(6.5f, 0.07f, 0.4f), Mul(Noise(6.5f, 0.5f, 0.3f), Crackle(6.5f)),
                    Tone(31f, 6.5f, Wave.Saw, 0f, 0f, 0.12f)), 0.5f),
                Sfx.Reel => Loop(Mix(Tone(190f, 2.5f, Wave.Saw, 0f, 0f, 0.05f), Tone(285f, 2.5f, Wave.Triangle, 0f, 0f, 0.05f), Noise(2.5f, 0.4f, 0.05f)), 0.5f),
                Sfx.Breath => Mul(Noise(4f, 0.07f, 0.22f), Breath(4f)),

                Sfx.Alarm => Concat(Tone(720f, 0.8f, Wave.Square, 0.01f, 0.01f, 0.13f), Tone(540f, 0.8f, Wave.Square, 0.01f, 0.01f, 0.13f)),
                Sfx.Engine => Loop(Mix(Noise(4.5f, 0.03f, 0.5f), Tone(42f, 4.5f, Wave.Sine, 0f, 0f, 0.3f)), 0.5f),
                Sfx.Rumble => Loop(Mix(Noise(4.5f, 0.015f, 0.9f), Tone(33f, 4.5f, Wave.Saw, 0f, 0f, 0.25f)), 0.5f),
                Sfx.GateHum => Mul(Chord(4f, 0.18f, 55f, 110f, 165f), Lfo(4f, 0.5f, 0.35f)),
                Sfx.Machinery => Loop(Mix(Mul(Noise(4.5f, 0.06f, 0.35f), Lfo(4.5f, 2f, 0.6f)), Tone(66f, 4.5f, Wave.Saw, 0f, 0f, 0.12f)), 0.5f),
                Sfx.RoomHum => Loop(Mix(Chord(4.5f, 0.05f, 60f, 120f), Noise(4.5f, 0.05f, 0.04f)), 0.5f),
                Sfx.ShipHum => Loop(Mix(Chord(4.5f, 0.08f, 80f, 120f), Noise(4.5f, 0.04f, 0.08f)), 0.5f),
                Sfx.Wind => Loop(Mul(Noise(8.5f, 0.02f, 0.5f), Lfo(8.5f, 0.17f, 0.7f)), 0.5f),
                Sfx.SpaceDrone => Mul(Chord(8f, 0.1f, 55f, 82.5f, 110f), Lfo(8f, 0.25f, 0.4f)),

                // Music pads: 8-second loops whose frequencies repeat exactly every 8 seconds, so they loop seamlessly.
                Sfx.PadTense => Mul(Chord(8f, 0.09f, 110f, 130.75f, 164.75f, 220f, 233.125f), Lfo(8f, 0.125f, 0.5f)),
                Sfx.PadCalm => Mul(Chord(8f, 0.09f, 130.75f, 196f, 261.625f, 329.625f, 392f), Lfo(8f, 0.125f, 0.4f)),
                Sfx.PadMystery => Mul(Chord(8f, 0.09f, 110f, 164.75f, 246.875f, 293.625f, 349.25f), Lfo(8f, 0.125f, 0.45f)),
                Sfx.PadDark => Mul(Chord(8f, 0.1f, 73.375f, 110f, 146.875f, 174.625f), Lfo(8f, 0.25f, 0.5f)),
                _ => null,
            };
            if (data == null)
            {
                Debug.LogWarning($"No sound named '{name}'.");
                return null;
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------- Synthesis helpers ----------

        enum Wave { Sine, Square, Saw, Triangle }

        static float Osc(Wave wave, float phase) => wave switch
        {
            Wave.Square => phase % 1f < 0.5f ? 1f : -1f,
            Wave.Saw => 2f * (phase % 1f) - 1f,
            Wave.Triangle => 1f - 4f * Mathf.Abs((phase % 1f) - 0.5f),
            _ => Mathf.Sin(phase * 2f * Mathf.PI),
        };

        static float[] Tone(float freq, float seconds, Wave wave, float attack, float release, float volume)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
                data[i] = Osc(wave, freq * i / Rate) * volume;
            return attack <= 0f && release <= 0f ? data : Envelope(data, attack, release);
        }

        /// <summary>A soft bell-like note for chimes.</summary>
        static float[] Note(float freq, float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * 0.22f * Mathf.Exp(-t * 6f);
            }
            return Envelope(data, 0.003f, 0.02f);
        }

        static float[] Sweep(float from, float to, float seconds, Wave wave, float volume)
        {
            var data = new float[(int)(seconds * Rate)];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                phase += Mathf.Lerp(from, to, (float)i / data.Length) / Rate;
                data[i] = Osc(wave, phase) * volume;
            }
            return Envelope(data, 0.005f, seconds * 0.4f);
        }

        /// <summary>Several sine tones at once (frequencies should repeat within the length for clean loops).</summary>
        static float[] Chord(float seconds, float volume, params float[] freqs)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate, sum = 0f;
                foreach (var f in freqs) sum += Mathf.Sin(2f * Mathf.PI * f * t);
                data[i] = sum * volume;
            }
            return data;
        }

        /// <summary>Filtered noise. smoothing 1 = white (bright), small values = deep and rumbly.</summary>
        static float[] Noise(float seconds, float smoothing, float volume)
        {
            var data = new float[(int)(seconds * Rate)];
            float value = 0f, peak = 0.0001f;
            for (int i = 0; i < data.Length; i++)
            {
                value += ((float)random.NextDouble() * 2f - 1f - value) * smoothing;
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * volume;
            return data;
        }

        /// <summary>A slow volume wobble: 1 - depth .. 1, at rate Hz.</summary>
        static float[] Lfo(float seconds, float rate, float depth)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
                data[i] = 1f - depth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * rate * i / Rate));
            return data;
        }

        static float[] Whoosh(float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            float value = 0f, phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float k = (float)i / data.Length;
                value += ((float)random.NextDouble() * 2f - 1f - value) * Mathf.Lerp(0.01f, 0.4f, k * k);
                phase += Mathf.Lerp(60f, 420f, k * k) / Rate;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
                data[i] = (value * 1.6f + Mathf.Sin(phase * 2f * Mathf.PI) * 0.3f) * envelope * 0.5f;
            }
            return data;
        }

        static float[] Impact(float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            float value = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                value += ((float)random.NextDouble() * 2f - 1f - value) * 0.25f;
                data[i] = (value * 1.4f * Mathf.Exp(-t * 4f) + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 30f, t / seconds) * t) * 0.7f * Mathf.Exp(-t * 3f)) * 0.7f;
            }
            return data;
        }

        /// <summary>A struck piece of metal: several tones that don't line up, dying away quickly.</summary>
        static float[] Clank(float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            float[] freqs = { 523f, 1187f, 1733f, 2411f, 3290f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate, sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * Mathf.Exp(-t * (7f + f * 4f)) / (f + 1);
                data[i] = sum * 0.35f + ((float)random.NextDouble() * 2f - 1f) * 0.3f * Mathf.Exp(-t * 60f);
            }
            return data;
        }

        /// <summary>Random sharp pops, for the crackle of a rocket plume.</summary>
        static float[] Crackle(float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            float level = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                if (random.NextDouble() < 0.004) level = 0.4f + (float)random.NextDouble() * 0.6f;
                level *= 0.9985f;
                data[i] = level;
            }
            return data;
        }

        /// <summary>One breath in and out over the length: the loudness curve of slow breathing in a helmet.</summary>
        static float[] Breath(float seconds)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float k = (float)i / data.Length;
                float inhale = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / 0.38f)));
                float exhale = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01((k - 0.48f) / 0.45f)));
                data[i] = inhale * 0.8f + exhale;
            }
            return data;
        }

        static float[] Silence(float seconds) => new float[(int)(seconds * Rate)];

        static float[] Envelope(float[] data, float attack, float release)
        {
            int a = Mathf.Max(1, (int)(attack * Rate)), r = Mathf.Max(1, (int)(release * Rate));
            for (int i = 0; i < data.Length; i++)
            {
                float gain = Mathf.Min(1f, (float)i / a) * Mathf.Min(1f, (float)(data.Length - i) / r);
                data[i] *= gain;
            }
            return data;
        }

        static float[] Concat(params float[][] parts)
        {
            int total = 0;
            foreach (var p in parts) total += p.Length;
            var data = new float[total];
            int offset = 0;
            foreach (var p in parts) { Array.Copy(p, 0, data, offset, p.Length); offset += p.Length; }
            return data;
        }

        static float[] Mix(params float[][] parts)
        {
            int length = 0;
            foreach (var p in parts) length = Mathf.Max(length, p.Length);
            var data = new float[length];
            foreach (var p in parts)
                for (int i = 0; i < p.Length; i++) data[i] += p[i];
            return data;
        }

        static float[] Mul(float[] a, float[] b)
        {
            for (int i = 0; i < a.Length && i < b.Length; i++) a[i] *= b[i];
            return a;
        }

        /// <summary>Makes a seamless loop by crossfading the extra tail back over the start.</summary>
        static float[] Loop(float[] data, float fadeSeconds)
        {
            int fade = (int)(fadeSeconds * Rate);
            var loop = new float[data.Length - fade];
            Array.Copy(data, loop, loop.Length);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                loop[i] = loop[i] * k + data[loop.Length + i] * (1f - k);
            }
            return loop;
        }
    }
}
