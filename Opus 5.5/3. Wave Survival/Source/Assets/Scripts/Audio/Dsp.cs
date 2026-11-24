using System;

namespace WaveSurvival
{
    /// <summary>Tiny DSP toolkit for synthesizing every sound of the game in code (S-26). Thread-safe (no shared state).</summary>
    public static class Dsp
    {
        public const int Rate = GameConfig.Audio.SampleRate;
        const float TwoPi = (float)(Math.PI * 2.0);

        /// <summary>Deterministic xorshift noise so every generated clip is identical between runs.</summary>
        public struct Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 8388607.5f - 1f; }
            public float Next01() => Next() * 0.5f + 0.5f;
        }

        public struct OnePole
        {
            float y;
            public float Lp(float x, float cutoff)
            {
                float a = 1f - (float)Math.Exp(-TwoPi * cutoff / Rate);
                y += a * (x - y);
                return y;
            }
            public float Hp(float x, float cutoff) => x - Lp(x, cutoff);
        }

        /// <summary>RBJ band-pass biquad (constant 0 dB peak gain); coefficients are recomputed when the centre moves.</summary>
        public struct BandPass
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2, lastF, lastQ;
            public float Process(float x, float freq, float q)
            {
                if (freq != lastF || q != lastQ)
                {
                    lastF = freq; lastQ = q;
                    float w = TwoPi * Math.Min(freq, Rate * 0.45f) / Rate;
                    float alpha = (float)Math.Sin(w) / (2f * q);
                    float a0 = 1f + alpha;
                    b0 = alpha / a0; b1 = 0f; b2 = -alpha / a0;
                    a1 = -2f * (float)Math.Cos(w) / a0; a2 = (1f - alpha) / a0;
                }
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        public static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];
        public static float T(int i) => (float)i / Rate;
        public static float Sin(float phase) => (float)Math.Sin(phase);
        public static float Exp(float x) => (float)Math.Exp(x);
        public static float Decay(float t, float tau) => (float)Math.Exp(-t / tau);
        public static float Saw(float phase) { float p = phase / TwoPi; return 2f * (p - (float)Math.Floor(p + 0.5f)); }
        public static float Square(float phase) => Sin(phase) >= 0f ? 1f : -1f;
        public static float SoftClip(float x, float drive) => (float)Math.Tanh(x * drive);

        /// <summary>Attack/sustain/release envelope (seconds).</summary>
        public static float Asr(float t, float attack, float sustainEnd, float release)
        {
            if (t < attack) return t / attack;
            if (t < sustainEnd) return 1f;
            return Math.Max(0f, 1f - (t - sustainEnd) / release);
        }

        /// <summary>Exponential pitch glide from f0 to f1 with time constant tau; returns the running phase.</summary>
        public static float Glide(ref float phase, float t, float f0, float f1, float tau)
        {
            float f = f1 + (f0 - f1) * Decay(t, tau);
            phase += TwoPi * f / Rate;
            return phase;
        }

        public static float Advance(ref float phase, float freq)
        {
            phase += TwoPi * freq / Rate;
            if (phase > 1000f) phase -= TwoPi * 159f; // keep precision on long clips
            return phase;
        }

        /// <summary>Adds a short metallic "ping" (inharmonic partials) at the given start time.</summary>
        public static void Ping(float[] buf, float start, float amp, float tau, params float[] freqs)
        {
            int s0 = (int)(start * Rate);
            int len = Math.Min(buf.Length - s0, (int)(tau * 7f * Rate));
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate, v = 0f;
                for (int k = 0; k < freqs.Length; k++) v += Sin(TwoPi * freqs[k] * t) / (k + 1);
                buf[s0 + i] += v * amp * Decay(t, tau);
            }
        }

        /// <summary>Adds a band-passed noise burst (clicks, scrapes, cloth) at the given start time.</summary>
        public static void NoiseBurst(float[] buf, float start, float length, float amp, float freq, float q, float tau, uint seed)
        {
            var rng = new Rng(seed);
            var bp = new BandPass();
            int s0 = (int)(start * Rate);
            int len = Math.Min(buf.Length - s0, (int)(length * Rate));
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Math.Min(1f, t / 0.0008f) * Decay(t, tau);
                buf[s0 + i] += bp.Process(rng.Next(), freq, q) * amp * env * 3f;
            }
        }

        /// <summary>Schroeder reverb (4 combs + 2 all-passes) mixed in place; gives the hall its industrial tail.</summary>
        public static void Reverb(float[] buf, float wet, float roomScale = 1f)
        {
            int[] combLen = { (int)(1557 * roomScale), (int)(1617 * roomScale), (int)(1491 * roomScale), (int)(1422 * roomScale) };
            float[] fb = { 0.84f, 0.83f, 0.85f, 0.82f };
            var outBuf = new float[buf.Length];
            for (int c = 0; c < 4; c++)
            {
                var line = new float[combLen[c]];
                int idx = 0;
                float damp = 0f;
                for (int i = 0; i < buf.Length; i++)
                {
                    float y = line[idx];
                    damp = y * 0.6f + damp * 0.4f; // damping low-passes the feedback
                    line[idx] = buf[i] + damp * fb[c];
                    idx = (idx + 1) % line.Length;
                    outBuf[i] += y * 0.25f;
                }
            }
            int[] apLen = { 556, 441 };
            for (int a = 0; a < 2; a++)
            {
                var line = new float[apLen[a]];
                int idx = 0;
                for (int i = 0; i < outBuf.Length; i++)
                {
                    float bufOut = line[idx];
                    float input = outBuf[i];
                    line[idx] = input + bufOut * 0.5f;
                    outBuf[i] = bufOut - input;
                    idx = (idx + 1) % line.Length;
                }
            }
            for (int i = 0; i < buf.Length; i++) buf[i] += outBuf[i] * wet;
        }

        /// <summary>Normalises to the given peak and applies 2 ms fades so clips never click.</summary>
        public static float[] Finish(float[] buf, float peak = 0.9f)
        {
            float max = 1e-6f;
            for (int i = 0; i < buf.Length; i++) max = Math.Max(max, Math.Abs(buf[i]));
            float g = peak / max;
            int fade = Math.Min(buf.Length / 4, (int)(0.002f * Rate));
            for (int i = 0; i < buf.Length; i++)
            {
                float e = 1f;
                if (i < fade) e = (float)i / fade;
                else if (i > buf.Length - fade) e = (float)(buf.Length - i) / fade;
                buf[i] *= g * e;
            }
            return buf;
        }
    }
}
