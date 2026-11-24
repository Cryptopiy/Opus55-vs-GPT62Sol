using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Every sound of the game is synthesised here at startup (I-31): filtered noise, oscillators and
    /// envelopes rendered into AudioClips. Loops are made seamless by cross-fading their tail into the head.
    /// </summary>
    public static class AudioSynth
    {
        const int SR = GameConfig.AudioSampleRate;

        /// <summary>RBJ biquad filter (direct form I).</summary>
        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            static Biquad Make(float b0, float b1, float b2, float a0, float a1, float a2)
            {
                return new Biquad { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
            }

            public static Biquad LowPass(float f, float q)
            {
                float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q);
                return Make((1f - c) * 0.5f, 1f - c, (1f - c) * 0.5f, 1f + alpha, -2f * c, 1f - alpha);
            }

            public static Biquad HighPass(float f, float q)
            {
                float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q);
                return Make((1f + c) * 0.5f, -(1f + c), (1f + c) * 0.5f, 1f + alpha, -2f * c, 1f - alpha);
            }

            public static Biquad BandPass(float f, float q)
            {
                float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q);
                return Make(alpha, 0f, -alpha, 1f + alpha, -2f * c, 1f - alpha);
            }

            /// <summary>Retunes coefficients in place (keeps the filter state: no clicks while sweeping).</summary>
            public void SetBandPass(float f, float q)
            {
                var n = BandPass(f, q);
                b0 = n.b0; b1 = n.b1; b2 = n.b2; a1 = n.a1; a2 = n.a2;
            }

            public void SetLowPass(float f, float q)
            {
                var n = LowPass(f, q);
                b0 = n.b0; b1 = n.b1; b2 = n.b2; a1 = n.a1; a2 = n.a2;
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        struct Noise
        {
            DetRandom rng;
            float brown;
            public Noise(int seed) { rng = new DetRandom(seed, 3); brown = 0f; }
            public float White() => rng.Value * 2f - 1f;
            /// <summary>Leaky-integrated white noise (red/brown spectrum).</summary>
            public float Brown() { brown = brown * 0.995f + White() * 0.06f; return brown * 3f; }
            public float Range(float a, float b) => rng.Range(a, b);
            public float Value => rng.Value;
        }

        static AudioClip Clip(string name, float[] data, float peak = 0.85f)
        {
            float max = 1e-5f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float g = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Renders len + fade seconds, then folds the extra tail onto the head for a seamless loop.</summary>
        static float[] Loop(float[] raw, int fade)
        {
            int len = raw.Length - fade;
            var outData = new float[len];
            for (int i = 0; i < len; i++) outData[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                outData[i] = raw[i] * t + raw[len + i] * (1f - t);
            }
            return outData;
        }

        static float Env(float t, float attack, float decay) => t < 0f ? 0f : (t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay));

        public static AudioClip Ocean(int seed)
        {
            int fade = SR, n = SR * 9 + fade;
            var d = new float[n];
            var noise = new Noise(seed);
            var lp = Biquad.LowPass(500f, 0.7f);
            var hiss = Biquad.BandPass(2200f, 0.6f);
            // Wave swells: overlapping surges of low roar with a hissing wash at each crest.
            int waves = 5;
            float[] start = new float[waves], strength = new float[waves];
            float tPos = 0f;
            for (int w = 0; w < waves; w++) { start[w] = tPos; strength[w] = noise.Range(0.6f, 1f); tPos += noise.Range(1.6f, 2.2f); }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float swell = 0.25f;
                float wash = 0f;
                for (int w = 0; w < waves; w++)
                {
                    float e = Env(t - start[w], 1.1f, 1.6f) * strength[w];
                    swell += e;
                    wash += Env(t - start[w] - 0.9f, 0.25f, 0.9f) * strength[w];
                }
                lp.SetLowPass(300f + 500f * Mathf.Clamp01(swell * 0.6f), 0.7f);
                d[i] = lp.Process(noise.Brown()) * swell * 0.9f + hiss.Process(noise.White()) * wash * 0.35f;
            }
            return Clip("Ocean", Loop(d, fade), 0.8f);
        }

        public static AudioClip Wind(int seed)
        {
            int fade = SR, n = SR * 7 + fade;
            var d = new float[n];
            var noise = new Noise(seed);
            var bp = Biquad.BandPass(500f, 1.2f);
            var lp = Biquad.LowPass(180f, 0.7f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.9f) + 0.15f * Mathf.Sin(t * 2.3f + 1f);
                if ((i & 31) == 0) bp.SetBandPass(320f + 520f * gust, 1.6f);
                d[i] = bp.Process(noise.White()) * gust * 1.4f + lp.Process(noise.White()) * 0.6f;
            }
            return Clip("Wind", Loop(d, fade), 0.75f);
        }

        public static AudioClip Crickets(int seed)
        {
            int fade = SR / 4, n = SR * 4 + fade;
            var d = new float[n];
            var noise = new Noise(seed);
            AddCricket(d, 4650f, 0.33f, 0f, 1f, ref noise);
            AddCricket(d, 3950f, 0.41f, 0.13f, 0.55f, ref noise);
            AddCricket(d, 5200f, 0.29f, 0.07f, 0.35f, ref noise);
            return Clip("Crickets", Loop(d, fade), 0.6f);
        }

        static void AddCricket(float[] d, float freq, float period, float offset, float gain, ref Noise noise)
        {
            for (float t0 = offset; t0 < d.Length / (float)SR; t0 += period + noise.Range(-0.03f, 0.03f))
            {
                for (int p = 0; p < 3; p++)                       // three pulses per chirp
                {
                    int s0 = (int)((t0 + p * 0.03f) * SR), len = (int)(0.018f * SR);
                    for (int k = 0; k < len && s0 + k < d.Length; k++)
                    {
                        float env = Mathf.Sin(Mathf.PI * k / len);
                        d[s0 + k] += Mathf.Sin(2f * Mathf.PI * freq * (s0 + k) / SR) * env * gain;
                    }
                }
            }
        }

        public static AudioClip Rain(int seed)
        {
            int fade = SR / 2, n = SR * 5 + fade;
            var d = new float[n];
            var noise = new Noise(seed);
            var hp = Biquad.HighPass(700f, 0.7f);
            var lp = Biquad.LowPass(7000f, 0.7f);
            var drop = Biquad.BandPass(3000f, 4f);
            for (int i = 0; i < n; i++)
            {
                float impulse = noise.Value < 140f / SR ? noise.Range(0.4f, 1f) : 0f; // ~140 droplets/s
                if (impulse > 0f) drop.SetBandPass(noise.Range(1800f, 5200f), 5f);
                d[i] = lp.Process(hp.Process(noise.White())) * 0.5f + drop.Process(impulse) * 2.5f;
            }
            return Clip("Rain", Loop(d, fade), 0.7f);
        }

        public static AudioClip Thunder(int seed)
        {
            int n = SR * 7;
            var d = new float[n];
            var noise = new Noise(seed);
            var lp = Biquad.LowPass(160f, 0.8f);
            var lp2 = Biquad.LowPass(900f, 0.7f);
            int claps = 4;
            float[] at = new float[claps];
            for (int c = 0; c < claps; c++) at[c] = c == 0 ? 0f : noise.Range(0.2f, 2.5f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = 0f;
                for (int c = 0; c < claps; c++) env += Env(t - at[c], c == 0 ? 0.02f : 0.15f, c == 0 ? 2.2f : 1.4f) * (c == 0 ? 1f : 0.6f);
                float crack = Env(t, 0.005f, 0.12f);
                d[i] = lp.Process(noise.Brown()) * env * 1.6f + lp2.Process(noise.White()) * crack * 0.7f;
            }
            return Clip("Thunder", d, 0.95f);
        }

        public static AudioClip Fire(int seed)
        {
            int fade = SR / 2, n = SR * 6 + fade;
            var d = new float[n];
            var noise = new Noise(seed);
            var roar = Biquad.LowPass(260f, 0.7f);
            var crackle = Biquad.HighPass(1600f, 0.8f);
            float crackEnv = 0f, popEnv = 0f;
            var pop = Biquad.BandPass(500f, 2f);
            for (int i = 0; i < n; i++)
            {
                if (noise.Value < 28f / SR) crackEnv = noise.Range(0.3f, 1f);       // ~28 crackles/s
                if (noise.Value < 2f / SR) { popEnv = noise.Range(0.6f, 1f); pop.SetBandPass(noise.Range(300f, 900f), 3f); }
                crackEnv *= 0.9965f;
                popEnv *= 0.9992f;
                float w = noise.White();
                d[i] = roar.Process(noise.Brown()) * 0.35f + crackle.Process(w) * crackEnv * 0.9f + pop.Process(w) * popEnv * 1.2f;
            }
            return Clip("Campfire", Loop(d, fade), 0.75f);
        }

        public static AudioClip Bird(int seed)
        {
            var noise = new Noise(seed);
            int chirps = (int)noise.Range(2f, 6.99f);
            float chirpLen = noise.Range(0.06f, 0.14f), gap = noise.Range(0.03f, 0.09f);
            int n = (int)((chirpLen + gap) * chirps * SR) + SR / 10;
            var d = new float[n];
            float f0 = noise.Range(2400f, 4200f), f1 = f0 * noise.Range(0.7f, 1.5f);
            float phase = 0f;
            for (int c = 0; c < chirps; c++)
            {
                int s0 = (int)(c * (chirpLen + gap) * SR), len = (int)(chirpLen * SR);
                float vib = noise.Range(18f, 40f);
                for (int k = 0; k < len && s0 + k < n; k++)
                {
                    float t = k / (float)len;
                    float f = Mathf.Lerp(f0, f1, t) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * vib * k / SR));
                    phase += 2f * Mathf.PI * f / SR;
                    float env = Mathf.Sin(Mathf.PI * t) * Mathf.Sin(Mathf.PI * t);
                    d[s0 + k] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * env;
                }
            }
            return Clip("Bird" + seed, d, 0.7f);
        }

        public static AudioClip Footstep(SurfaceType surface, int variant)
        {
            var noise = new Noise(900 + (int)surface * 17 + variant);
            int n = (int)(0.32f * SR);
            var d = new float[n];
            switch (surface)
            {
                case SurfaceType.Sand:
                {
                    var lp = Biquad.LowPass(noise.Range(700f, 1000f), 0.7f);
                    var grain = Biquad.BandPass(noise.Range(2500f, 3500f), 1.5f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)SR, w = noise.White();
                        d[i] = lp.Process(w) * Env(t, 0.02f, 0.06f) + grain.Process(w) * Env(t, 0.01f, 0.04f) * 0.4f;
                    }
                    break;
                }
                case SurfaceType.Rock:
                {
                    var body = Biquad.BandPass(noise.Range(900f, 1400f), 6f);
                    var thump = Biquad.LowPass(160f, 0.9f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)SR, w = noise.White();
                        float click = Env(t, 0.001f, 0.008f);
                        d[i] = body.Process(w * click) * 6f + thump.Process(w) * Env(t, 0.003f, 0.03f) * 1.5f;
                    }
                    break;
                }
                case SurfaceType.Water:
                {
                    var sweep = Biquad.BandPass(400f, 2f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)SR;
                        if ((i & 15) == 0) sweep.SetBandPass(400f + 1600f * Mathf.Clamp01(t / 0.18f), 2.5f);
                        float bubble = Mathf.Sin(2f * Mathf.PI * (700f + 900f * t) * t) * Env(t - 0.08f, 0.01f, 0.05f) * 0.3f;
                        d[i] = sweep.Process(noise.White()) * Env(t, 0.015f, 0.09f) * 2f + bubble;
                    }
                    break;
                }
                default: // Grass and dirt: rustle of many tiny high-pass grains
                {
                    var hp = Biquad.HighPass(surface == SurfaceType.Dirt ? 900f : 2200f, 0.7f);
                    var lp = Biquad.LowPass(surface == SurfaceType.Dirt ? 3500f : 7500f, 0.7f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)SR;
                        float grains = noise.Value < 0.3f ? 1f : 0.35f;
                        d[i] = lp.Process(hp.Process(noise.White())) * grains * Env(t, 0.025f, 0.08f);
                    }
                    break;
                }
            }
            return Clip("Step_" + surface + variant, d, 0.8f);
        }
    }
}
