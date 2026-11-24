using System;

namespace WaveSurvival
{
    /// <summary>Synthesized creature, boss, pickup, ambience and UI sounds (S-26).</summary>
    public static class WorldSounds
    {
        /// <summary>Voice = oscillator through two formant band-passes, with jitter, rasp and distortion.</summary>
        static float[] Voice(float seconds, uint seed, float f0, float f1, float formA, float formB, float rasp, float drive,
            float attack, float release, bool square, float reverb)
        {
            float[] b = Dsp.Buffer(seconds);
            var rng = new Dsp.Rng(seed);
            var bpA = new Dsp.BandPass();
            var bpB = new Dsp.BandPass();
            var lp = new Dsp.OnePole();
            float ph = 0f, jitter = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float k = t / seconds;
                jitter = jitter * 0.999f + rng.Next() * 0.001f;
                float f = (f0 + (f1 - f0) * k) * (1f + Dsp.Sin(t * 37f) * 0.04f + jitter * 2f);
                float p = Dsp.Advance(ref ph, f);
                float osc = square ? Dsp.Square(p) * 0.6f : Dsp.Saw(p);
                float noise = lp.Lp(rng.Next(), 3000f) * rasp * (0.6f + 0.4f * Dsp.Sin(t * 190f));
                float src = osc + noise;
                float v = bpA.Process(src, formA * (1f + 0.15f * k), 2.5f) * 1.4f + bpB.Process(src, formB, 3f) + src * 0.15f;
                float env = Dsp.Asr(t, attack, seconds - release, release);
                b[i] = Dsp.SoftClip(v * env, drive);
            }
            if (reverb > 0f) Dsp.Reverb(b, reverb);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] RunnerGrowl() => Voice(0.75f, 211, 240f, 170f, 900f, 2500f, 0.9f, 2.5f, 0.04f, 0.3f, false, 0.12f);
        public static float[] ShooterGrowl()
        {
            // Radio-like robotic chatter: square voice, syllable gating and bit-crush.
            float[] b = Voice(0.85f, 223, 115f, 100f, 1000f, 2200f, 0.4f, 3f, 0.02f, 0.15f, true, 0f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float gate = Dsp.Sin(t * 2f * (float)Math.PI * 7f) > -0.2f ? 1f : 0.15f;
                b[i] = (float)Math.Round(b[i] * gate * 10f) / 10f;
            }
            return Dsp.Finish(b, 0.8f);
        }
        public static float[] TankGrowl() => Voice(1.2f, 227, 58f, 48f, 380f, 900f, 0.6f, 4f, 0.15f, 0.5f, false, 0.15f);
        public static float[] TankRoar() => Voice(1.1f, 229, 78f, 52f, 450f, 1200f, 1.2f, 5f, 0.03f, 0.4f, false, 0.25f);
        public static float[] BossRoar() => Voice(2.4f, 233, 46f, 36f, 320f, 760f, 1.4f, 6f, 0.12f, 0.9f, false, 0.4f);
        public static float[] RunnerDeath() => Voice(0.65f, 239, 420f, 120f, 1100f, 2800f, 1f, 3f, 0.01f, 0.35f, false, 0.15f);
        public static float[] ShooterDeath()
        {
            float[] b = Voice(0.75f, 241, 210f, 60f, 900f, 2000f, 0.8f, 3f, 0.01f, 0.4f, true, 0.1f);
            var rng = new Dsp.Rng(243);
            for (int i = 0; i < b.Length; i++) if (rng.Next01() > 0.985f) b[i] += rng.Next() * 0.8f; // radio crackle
            return Dsp.Finish(b, 0.8f);
        }
        public static float[] TankDeath()
        {
            float[] b = Voice(1.3f, 251, 72f, 34f, 400f, 1000f, 0.8f, 4f, 0.02f, 0.8f, false, 0.25f);
            Dsp.Ping(b, 0.25f, 0.5f, 0.25f, 340f, 523f, 871f, 1240f);
            return Dsp.Finish(b, 0.9f);
        }

        static float[] Blaster(float seconds, uint seed, float f0, float f1, float tau, float reverb)
        {
            float[] b = Dsp.Buffer(seconds);
            var rng = new Dsp.Rng(seed);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float p = Dsp.Glide(ref ph, t, f0, f1, tau * 0.7f);
                b[i] = (Dsp.Saw(p) * 0.5f + Dsp.Sin(p) * 0.7f) * Dsp.Decay(t, tau) + (t < 0.005f ? rng.Next() * 0.8f : 0f);
            }
            Dsp.Reverb(b, reverb);
            return Dsp.Finish(b, 0.8f);
        }

        public static float[] EnemyShot() => Blaster(0.4f, 257, 1500f, 280f, 0.08f, 0.2f);
        public static float[] BossShot() => Blaster(0.5f, 263, 820f, 140f, 0.11f, 0.25f);

        public static float[] Slam()
        {
            float[] b = Dsp.Buffer(1.4f);
            var rng = new Dsp.Rng(269);
            var lp = new Dsp.OnePole();
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float boom = Dsp.Sin(Dsp.Glide(ref ph, t, 75f, 24f, 0.12f)) * Dsp.Decay(t, 0.3f) * 1.4f;
                float crash = lp.Lp(rng.Next(), 2200f - 1600f * Math.Min(1f, t)) * Dsp.Decay(t, 0.32f) * 1.5f;
                float rattle = (rng.Next01() > 0.97f ? rng.Next() : 0f) * Dsp.Decay(t, 0.5f) * 0.5f;
                b[i] = Dsp.SoftClip(boom + crash + rattle, 2.5f);
            }
            Dsp.Reverb(b, 0.35f);
            return Dsp.Finish(b, 0.95f);
        }

        public static float[] Whoosh()
        {
            float[] b = Dsp.Buffer(0.7f);
            var rng = new Dsp.Rng(271);
            var bp = new Dsp.BandPass();
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i), k = t / 0.7f;
                float f = 300f + 1000f * Dsp.Sin(k * (float)Math.PI);
                b[i] = bp.Process(rng.Next(), f, 1.2f) * Dsp.Sin(k * (float)Math.PI) * 2f;
            }
            return Dsp.Finish(b, 0.75f);
        }

        public static float[] Summon()
        {
            float[] b = Dsp.Buffer(1.1f);
            var rng = new Dsp.Rng(277);
            var lp = new Dsp.OnePole();
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i), k = t / 1.1f;
                float swell = k * k;
                float drone = Dsp.Saw(Dsp.Advance(ref ph, 70f + 50f * k)) * 0.5f;
                b[i] = (lp.Lp(rng.Next(), 300f + 2500f * k) * 1.5f + drone) * swell * (k < 0.92f ? 1f : (1f - k) / 0.08f);
            }
            Dsp.NoiseBurst(b, 0.98f, 0.12f, 1f, 1500f, 1f, 0.04f, 281);
            Dsp.Reverb(b, 0.3f);
            return Dsp.Finish(b, 0.85f);
        }

        static void Bell(float[] b, float start, float freq, float amp, float tau)
        {
            Dsp.Ping(b, start, amp, tau, freq, freq * 2.01f, freq * 3.02f);
        }

        public static float[] PickupHealth()
        {
            float[] b = Dsp.Buffer(0.7f);
            Bell(b, 0f, 660f, 0.6f, 0.2f);
            Bell(b, 0.08f, 880f, 0.6f, 0.22f);
            Bell(b, 0.16f, 1320f, 0.7f, 0.3f);
            Dsp.Reverb(b, 0.2f);
            return Dsp.Finish(b, 0.75f);
        }

        public static float[] PickupAmmo()
        {
            float[] b = Dsp.Buffer(0.5f);
            Dsp.NoiseBurst(b, 0f, 0.02f, 0.8f, 1600f, 2f, 0.008f, 283);
            Bell(b, 0.03f, 990f, 0.55f, 0.15f);
            Bell(b, 0.1f, 1480f, 0.6f, 0.2f);
            return Dsp.Finish(b, 0.75f);
        }

        /// <summary>Two-tone industrial horn announcing a wave (S-26).</summary>
        public static float[] WaveHorn()
        {
            float[] b = Dsp.Buffer(2.6f);
            var lp = new Dsp.OnePole();
            float p1 = 0f, p2 = 0f, p3 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float vib = 1f + Dsp.Sin(t * 2f * (float)Math.PI * 5f) * 0.006f;
                float second = t > 1.1f ? 1f : 0f;
                float f = (second > 0f ? 196f : 220f) * vib;
                float v = Dsp.Saw(Dsp.Advance(ref p1, f)) + Dsp.Saw(Dsp.Advance(ref p2, f * 1.26f)) * 0.7f + Dsp.Sin(Dsp.Advance(ref p3, f * 0.5f)) * 0.8f;
                float env = Dsp.Asr(t, 0.12f, 2.1f, 0.5f) * (Math.Abs(t - 1.1f) < 0.04f ? 0.5f : 1f);
                b[i] = Dsp.SoftClip(lp.Lp(v, 1700f) * env, 1.6f);
            }
            Dsp.Reverb(b, 0.45f, 1.3f);
            return Dsp.Finish(b, 0.9f);
        }

        public static float[] GateHum()
        {
            float[] b = Dsp.Buffer(1.05f);
            var rng = new Dsp.Rng(293);
            float p1 = 0f, p2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i), k = t / 1.05f;
                float f = 60f + 40f * k;
                float buzz = Dsp.Saw(Dsp.Advance(ref p1, f)) * 0.6f + Dsp.Square(Dsp.Advance(ref p2, f * 2f)) * 0.2f;
                float crackle = rng.Next01() > 0.995f ? rng.Next() : 0f;
                b[i] = (buzz + crackle) * (0.2f + 0.8f * k) * (k > 0.95f ? (1f - k) / 0.05f : 1f);
            }
            return Dsp.Finish(b, 0.7f);
        }

        public static float[] Victory()
        {
            float[] b = Dsp.Buffer(2.8f);
            float[] notes = { 261.6f, 329.6f, 392f, 523.3f };
            var lp = new Dsp.OnePole();
            float[] ph = new float[4];
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i), v = 0f;
                for (int n = 0; n < 4; n++)
                {
                    float start = n * 0.18f;
                    if (t < start) continue;
                    v += Dsp.Saw(Dsp.Advance(ref ph[n], notes[n])) * Dsp.Asr(t - start, 0.05f, 1.8f - start, 0.8f) * 0.5f;
                }
                b[i] = lp.Lp(v, 2200f);
            }
            Dsp.Reverb(b, 0.4f, 1.2f);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] UiClick()
        {
            float[] b = Dsp.Buffer(0.06f);
            Dsp.NoiseBurst(b, 0f, 0.01f, 0.8f, 3000f, 2f, 0.003f, 307);
            Dsp.Ping(b, 0f, 0.3f, 0.01f, 1500f);
            return Dsp.Finish(b, 0.6f);
        }

        /// <summary>
        /// Seamless 12 s industrial drone: hum partials complete whole cycles in the loop, the noise layer is
        /// cross-faded across the loop point, distant metal creaks sit away from the edges.
        /// </summary>
        public static float[] AmbientDrone()
        {
            const float loop = 12f, extra = 1f;
            float[] b = Dsp.Buffer(loop + extra);
            var rng = new Dsp.Rng(311);
            var lp = new Dsp.OnePole();
            var lp2 = new Dsp.OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float w = 2f * (float)Math.PI;
                float hum = Dsp.Sin(w * 50f * t) * 0.5f + Dsp.Sin(w * 100f * t) * 0.22f + Dsp.Sin(w * 150f * t) * 0.1f + Dsp.Sin(w * 50.25f * t) * 0.3f;
                float air = lp.Lp(rng.Next(), 380f) * (0.7f + 0.3f * Dsp.Sin(w * t / 6f)) * 1.8f;
                float rumble = lp2.Lp(rng.Next(), 90f) * 2.5f;
                b[i] = hum * 0.5f + air + rumble;
            }
            Dsp.Ping(b, 3.1f, 0.12f, 0.9f, 211f, 347f, 593f);
            Dsp.Ping(b, 7.4f, 0.1f, 1.1f, 163f, 281f, 449f, 702f);
            Dsp.Reverb(b, 0.3f, 1.5f);
            int n = (int)(loop * Dsp.Rate), x = (int)(extra * Dsp.Rate);
            float[] o = new float[n];
            Array.Copy(b, o, n);
            for (int i = 0; i < x; i++)
            {
                float k = (float)i / x; // equal-power crossfade of the overhang into the loop start
                o[i] = b[n + i] * (float)Math.Cos(k * Math.PI * 0.5) + b[i] * (float)Math.Sin(k * Math.PI * 0.5);
            }
            float max = 1e-6f;
            for (int i = 0; i < o.Length; i++) max = Math.Max(max, Math.Abs(o[i]));
            for (int i = 0; i < o.Length; i++) o[i] *= 0.8f / max;
            return o;
        }
    }
}
