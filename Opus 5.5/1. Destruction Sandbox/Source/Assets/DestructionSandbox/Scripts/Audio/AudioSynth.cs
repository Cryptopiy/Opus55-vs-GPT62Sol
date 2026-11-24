using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// D-25: every sound is synthesized here at start-up from sines, filtered noise and envelopes
    /// (no audio files). Deterministic thanks to a fixed seed.
    /// </summary>
    public sealed class AudioSynth
    {
        private readonly int rate;
        private System.Random rng;

        public AudioSynth(int sampleRate, int seed)
        {
            rate = sampleRate;
            rng = new System.Random(seed);
        }

        private float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        private static float Env(float t, float attack, float tau) => (t < attack ? t / attack : 1f) * Mathf.Exp(-Mathf.Max(0f, t - attack) / tau);
        private float Alpha(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);

        private AudioClip Finish(string name, float[] data, float peak = 0.95f)
        {
            float max = 1e-6f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            // Short fade-out so no clip ends with a click.
            int fade = Mathf.Min(data.Length, rate / 100);
            for (int i = 0; i < fade; i++) data[data.Length - 1 - i] *= (float)i / fade;
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float SoftClip(float x) => (float)System.Math.Tanh(x);

        /// <summary>Sine whose frequency glides exponentially from f0 to f1 with time constant 'glide'.</summary>
        private float SweepSine(float[] buffer, float f0, float f1, float glide, float attack, float tau, float amp)
        {
            float phase = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / rate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / glide);
                phase += 2f * Mathf.PI * f / rate;
                buffer[i] += Mathf.Sin(phase) * Env(t, attack, tau) * amp;
            }
            return phase;
        }

        /// <summary>White noise through a one-pole low-pass whose cutoff glides from c0 to c1.</summary>
        private void FilteredNoise(float[] buffer, float c0, float c1, float glide, float attack, float tau, float amp, float start = 0f)
        {
            float y = 0f;
            int s = Mathf.Clamp((int)(start * rate), 0, buffer.Length);
            for (int i = s; i < buffer.Length; i++)
            {
                float t = (float)(i - s) / rate;
                float cutoff = c1 + (c0 - c1) * Mathf.Exp(-t / glide);
                y += Alpha(cutoff) * (Noise() - y);
                buffer[i] += y * Env(t, attack, tau) * amp;
            }
        }

        private void HighNoise(float[] buffer, float cutoff, float attack, float tau, float amp, float start = 0f)
        {
            float low = 0f;
            float a = Alpha(cutoff);
            int s = Mathf.Clamp((int)(start * rate), 0, buffer.Length);
            for (int i = s; i < buffer.Length; i++)
            {
                float t = (float)(i - s) / rate;
                float n = Noise();
                low += a * (n - low);
                buffer[i] += (n - low) * Env(t, attack, tau) * amp;
            }
        }

        private void Partial(float[] buffer, float freq, float tau, float amp, float start = 0f)
        {
            int s = Mathf.Clamp((int)(start * rate), 0, buffer.Length);
            int end = Mathf.Min(buffer.Length, s + (int)(tau * 7f * rate));
            for (int i = s; i < end; i++)
            {
                float t = (float)(i - s) / rate;
                buffer[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t / tau) * amp * Mathf.Min(1f, t * 2000f);
            }
        }

        private float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * rate)];

        public AudioClip CannonShot()
        {
            float[] b = Buffer(1.4f);
            SweepSine(b, 115f, 36f, 0.12f, 0.003f, 0.24f, 1.0f);
            FilteredNoise(b, 2400f, 220f, 0.08f, 0.002f, 0.32f, 0.9f);
            HighNoise(b, 2500f, 0.0005f, 0.012f, 0.7f);
            FilteredNoise(b, 400f, 120f, 0.4f, 0.02f, 0.55f, 0.5f, 0.02f);
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.6f);
            return Finish("CannonShot", b);
        }

        public AudioClip ShellShot()
        {
            float[] b = Buffer(0.9f);
            SweepSine(b, 190f, 70f, 0.06f, 0.002f, 0.13f, 1.0f);
            FilteredNoise(b, 1800f, 300f, 0.05f, 0.002f, 0.14f, 0.7f);
            FilteredNoise(b, 5000f, 1500f, 0.25f, 0.02f, 0.3f, 0.25f, 0.03f);
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.4f);
            return Finish("ShellShot", b, 0.85f);
        }

        public AudioClip OrbShot()
        {
            float[] b = Buffer(1.0f);
            float phase = 0f, mod = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / rate;
                float f = Mathf.Lerp(220f, 700f, 1f - Mathf.Exp(-t / 0.18f));
                mod += 2f * Mathf.PI * (f * 1.5f) / rate;
                float index = 3f * Mathf.Exp(-t / 0.25f);
                phase += 2f * Mathf.PI * f / rate;
                float env = Env(t, 0.01f, 0.3f);
                b[i] = Mathf.Sin(phase + index * Mathf.Sin(mod)) * env * 0.7f
                     + Mathf.Sin(2f * Mathf.PI * 1320f * t + 2f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * env * 0.18f;
            }
            HighNoise(b, 3000f, 0.02f, 0.2f, 0.15f);
            return Finish("OrbShot", b, 0.75f);
        }

        public AudioClip Explosion()
        {
            float[] b = Buffer(3.2f);
            SweepSine(b, 72f, 27f, 0.35f, 0.004f, 0.65f, 1.0f);
            // Rumbling tail: low-passed noise whose cutoff falls from bright to dull.
            FilteredNoise(b, 3200f, 260f, 0.25f, 0.004f, 0.95f, 1.1f);
            FilteredNoise(b, 220f, 90f, 1.0f, 0.05f, 1.3f, 0.8f, 0.05f);
            // Debris crackle: sparse decaying clicks.
            for (int k = 0; k < 90; k++)
            {
                float at = 0.05f + Mathf.Pow(Rand(0f, 1f), 1.8f) * 1.6f;
                Partial(b, Rand(900f, 4200f), Rand(0.004f, 0.015f), Rand(0.05f, 0.25f) * Mathf.Exp(-at / 0.8f), at);
            }
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.8f);
            return Finish("Explosion", b);
        }

        public AudioClip StoneImpact()
        {
            float[] b = Buffer(0.6f);
            Partial(b, 185f, 0.08f, 0.9f);
            Partial(b, 437f, 0.05f, 0.6f);
            Partial(b, 791f, 0.035f, 0.45f);
            Partial(b, 1283f, 0.02f, 0.3f);
            FilteredNoise(b, 3500f, 900f, 0.03f, 0.0005f, 0.05f, 0.8f);
            FilteredNoise(b, 1200f, 500f, 0.1f, 0.01f, 0.16f, 0.35f, 0.01f);   // crumble
            return Finish("StoneImpact", b, 0.9f);
        }

        public AudioClip WoodImpact()
        {
            float[] b = Buffer(0.5f);
            Partial(b, 163f, 0.12f, 0.9f);
            Partial(b, 384f, 0.08f, 0.7f);
            Partial(b, 652f, 0.05f, 0.45f);
            Partial(b, 1049f, 0.03f, 0.25f);
            HighNoise(b, 1500f, 0.0005f, 0.012f, 0.6f);
            FilteredNoise(b, 900f, 300f, 0.05f, 0.002f, 0.07f, 0.4f);
            return Finish("WoodImpact", b, 0.9f);
        }

        public AudioClip GlassBreak()
        {
            float[] b = Buffer(1.6f);
            HighNoise(b, 3000f, 0.0005f, 0.07f, 0.9f);
            for (int k = 0; k < 60; k++)
            {
                float at = Mathf.Pow(Rand(0f, 1f), 1.6f) * 1.0f;
                Partial(b, Rand(2400f, 7200f), Rand(0.03f, 0.12f), Rand(0.15f, 0.45f) * Mathf.Exp(-at / 0.6f), at);
            }
            HighNoise(b, 6000f, 0.05f, 0.35f, 0.12f, 0.02f);
            return Finish("GlassBreak", b, 0.85f);
        }

        public AudioClip HeavyImpact()
        {
            float[] b = Buffer(1.0f);
            SweepSine(b, 80f, 38f, 0.08f, 0.002f, 0.2f, 1.0f);
            FilteredNoise(b, 1200f, 250f, 0.05f, 0.001f, 0.12f, 0.9f);
            FilteredNoise(b, 700f, 200f, 0.3f, 0.02f, 0.35f, 0.4f, 0.03f);
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.5f);
            return Finish("HeavyImpact", b);
        }

        /// <summary>2 s seamless loop: every frequency completes a whole number of cycles in the loop.</summary>
        public AudioClip OrbHum()
        {
            const float length = 2f;
            float[] b = Buffer(length);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / rate;
                float am = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                float whirr = Mathf.Sin(2f * Mathf.PI * 480f * t + 6f * Mathf.Sin(2f * Mathf.PI * 5f * t));
                b[i] = (Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.35f
                        + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.15f) * am + whirr * 0.08f * am;
            }
            var clip = AudioClip.Create("OrbHum", b.Length, 1, rate, false);
            float max = 1e-6f;
            for (int i = 0; i < b.Length; i++) max = Mathf.Max(max, Mathf.Abs(b[i]));
            for (int i = 0; i < b.Length; i++) b[i] *= 0.8f / max;
            clip.SetData(b, 0);
            return clip;
        }

        public AudioClip OrbImplosion()
        {
            float[] b = Buffer(1.6f);
            const float swell = 0.32f;
            float y = 0f;
            int swellEnd = (int)(swell * rate);
            for (int i = 0; i < swellEnd; i++)
            {
                float t = (float)i / rate;
                float k = t / swell;
                y += Alpha(300f + 4000f * k * k) * (Noise() - y);
                b[i] += y * k * k * 0.8f;   // reversed-sounding rising suck
            }
            float[] tail = Buffer(1.6f - swell);
            SweepSine(tail, 95f, 32f, 0.15f, 0.003f, 0.32f, 1.0f);
            FilteredNoise(tail, 2500f, 300f, 0.12f, 0.003f, 0.45f, 0.8f);
            for (int i = 0; i < tail.Length && swellEnd + i < b.Length; i++) b[swellEnd + i] += tail[i];
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.5f);
            return Finish("OrbImplosion", b);
        }

        public AudioClip UiClick()
        {
            float[] b = Buffer(0.08f);
            Partial(b, 1800f, 0.012f, 0.8f);
            Partial(b, 900f, 0.02f, 0.5f);
            return Finish("UiClick", b, 0.6f);
        }
    }
}
