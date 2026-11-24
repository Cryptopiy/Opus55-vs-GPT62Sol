using System;

namespace WaveSurvival
{
    /// <summary>Synthesized weapon and player sounds: three distinct gunshots, reload mechanics, footsteps, hit ticks (S-26).</summary>
    public static class WeaponSounds
    {
        /// <summary>Gunshot = transient crack + filtered noise body + pitch-dropping thump + room tail + reverb.</summary>
        static float[] Gunshot(float seconds, uint seed, float crackHz, float bodyTau, float cutStart, float cutEnd,
            float thumpStart, float thumpEnd, float thumpTau, float thumpAmp, float tailTau, float reverb, float drive)
        {
            float[] b = Dsp.Buffer(seconds);
            var rng = new Dsp.Rng(seed);
            var lp = new Dsp.OnePole();
            var lp2 = new Dsp.OnePole();
            var hp = new Dsp.OnePole();
            var bp = new Dsp.BandPass();
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float n = rng.Next();
                float crack = t < 0.004f ? hp.Hp(n, 1800f) * (1f - t / 0.004f) * 1.4f : 0f;
                float cut = cutEnd + (cutStart - cutEnd) * Dsp.Decay(t, bodyTau * 1.4f);
                float body = lp.Lp(n, cut) * Dsp.Decay(t, bodyTau) * 1.6f;
                float mid = bp.Process(n, crackHz, 0.9f) * Dsp.Decay(t, bodyTau * 0.5f) * 1.2f;
                float thump = Dsp.Sin(Dsp.Glide(ref ph, t, thumpStart, thumpEnd, thumpTau * 0.6f)) * Dsp.Decay(t, thumpTau) * thumpAmp;
                float tail = lp2.Lp(n, 700f) * Dsp.Decay(t, tailTau) * 0.5f;
                b[i] = Dsp.SoftClip(crack + body + mid + thump + tail, drive);
            }
            Dsp.Reverb(b, reverb);
            return Dsp.Finish(b);
        }

        public static float[] Pistol() => Gunshot(0.6f, 11, 2600f, 0.045f, 7000f, 900f, 150f, 55f, 0.06f, 0.9f, 0.2f, 0.28f, 1.6f);
        public static float[] Rifle() => Gunshot(0.55f, 23, 3200f, 0.032f, 9000f, 1100f, 120f, 45f, 0.05f, 1.0f, 0.17f, 0.24f, 2.2f);
        public static float[] Shotgun() => Gunshot(1.1f, 37, 1500f, 0.09f, 4500f, 450f, 95f, 32f, 0.13f, 1.4f, 0.35f, 0.34f, 2.6f);

        public static float[] MagOut()
        {
            float[] b = Dsp.Buffer(0.3f);
            Dsp.NoiseBurst(b, 0f, 0.01f, 0.8f, 3800f, 3f, 0.003f, 101);
            Dsp.Ping(b, 0f, 0.35f, 0.02f, 2200f, 3500f);
            Dsp.NoiseBurst(b, 0.04f, 0.09f, 0.25f, 1500f, 1.5f, 0.04f, 103);
            Dsp.NoiseBurst(b, 0.15f, 0.01f, 0.5f, 2600f, 2.5f, 0.004f, 107);
            return Dsp.Finish(b, 0.8f);
        }

        public static float[] MagIn()
        {
            float[] b = Dsp.Buffer(0.3f);
            Dsp.NoiseBurst(b, 0f, 0.06f, 0.3f, 1200f, 1.2f, 0.03f, 109);
            Dsp.NoiseBurst(b, 0.06f, 0.02f, 1f, 1800f, 2f, 0.006f, 113);
            Dsp.Ping(b, 0.06f, 0.5f, 0.025f, 320f, 1900f, 2700f);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] BoltRack()
        {
            float[] b = Dsp.Buffer(0.4f);
            Dsp.NoiseBurst(b, 0f, 0.07f, 0.35f, 1300f, 1.4f, 0.03f, 127);
            Dsp.NoiseBurst(b, 0.07f, 0.01f, 0.8f, 3000f, 3f, 0.004f, 131);
            Dsp.NoiseBurst(b, 0.17f, 0.012f, 1f, 2400f, 2.5f, 0.005f, 137);
            Dsp.Ping(b, 0.17f, 0.45f, 0.04f, 2800f, 4100f);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] ShellInsert()
        {
            float[] b = Dsp.Buffer(0.22f);
            Dsp.NoiseBurst(b, 0f, 0.05f, 0.3f, 2000f, 1.5f, 0.02f, 139);
            Dsp.NoiseBurst(b, 0.05f, 0.015f, 0.9f, 1400f, 2f, 0.006f, 149);
            Dsp.Ping(b, 0.05f, 0.3f, 0.03f, 900f, 2300f);
            return Dsp.Finish(b, 0.8f);
        }

        public static float[] Pump()
        {
            float[] b = Dsp.Buffer(0.5f);
            Dsp.NoiseBurst(b, 0f, 0.08f, 0.45f, 900f, 1.2f, 0.04f, 151);
            Dsp.NoiseBurst(b, 0.08f, 0.015f, 1f, 2200f, 2.5f, 0.006f, 157);
            Dsp.NoiseBurst(b, 0.2f, 0.07f, 0.35f, 1100f, 1.2f, 0.035f, 163);
            Dsp.NoiseBurst(b, 0.27f, 0.015f, 1f, 1700f, 2.5f, 0.006f, 167);
            Dsp.Ping(b, 0.27f, 0.35f, 0.03f, 640f, 1900f);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] DryFire()
        {
            float[] b = Dsp.Buffer(0.12f);
            Dsp.NoiseBurst(b, 0f, 0.006f, 1f, 4200f, 3f, 0.002f, 173);
            Dsp.Ping(b, 0f, 0.25f, 0.015f, 3100f);
            return Dsp.Finish(b, 0.7f);
        }

        public static float[] Switch()
        {
            float[] b = Dsp.Buffer(0.28f);
            var rng = new Dsp.Rng(179);
            var lp = new Dsp.OnePole();
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                b[i] = lp.Lp(rng.Next(), 1800f) * Dsp.Asr(t, 0.05f, 0.1f, 0.12f) * 0.6f;
            }
            Dsp.NoiseBurst(b, 0.16f, 0.012f, 0.9f, 2600f, 2.5f, 0.005f, 181);
            return Dsp.Finish(b, 0.7f);
        }

        public static float[] Footstep(uint seed)
        {
            float[] b = Dsp.Buffer(0.2f);
            var rng = new Dsp.Rng(seed);
            var lp = new Dsp.OnePole();
            float ph = 0f;
            float f0 = 75f + rng.Next01() * 25f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float thump = Dsp.Sin(Dsp.Glide(ref ph, t, f0 * 1.6f, f0, 0.02f)) * Dsp.Decay(t, 0.028f);
                float scuff = lp.Lp(rng.Next(), 1400f + rng.Next01() * 600f) * Dsp.Decay(Math.Max(0f, t - 0.005f), 0.035f) * 1.4f;
                b[i] = thump + scuff;
            }
            return Dsp.Finish(b, 0.75f);
        }

        public static float[] HitTick()
        {
            float[] b = Dsp.Buffer(0.08f);
            Dsp.Ping(b, 0f, 0.8f, 0.012f, 1900f, 3800f);
            Dsp.NoiseBurst(b, 0f, 0.004f, 0.4f, 5000f, 2f, 0.0015f, 191);
            return Dsp.Finish(b, 0.75f);
        }

        public static float[] KillConfirm()
        {
            float[] b = Dsp.Buffer(0.32f);
            Dsp.Ping(b, 0f, 0.8f, 0.05f, 2200f, 4400f);
            Dsp.Ping(b, 0.065f, 0.9f, 0.08f, 2950f, 5900f);
            Dsp.Ping(b, 0f, 0.6f, 0.06f, 180f);
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] PlayerHurt()
        {
            float[] b = Dsp.Buffer(0.4f);
            var rng = new Dsp.Rng(193);
            var lp = new Dsp.OnePole();
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float thud = Dsp.Sin(Dsp.Glide(ref ph, t, 110f, 55f, 0.05f)) * Dsp.Decay(t, 0.09f);
                float muff = lp.Lp(rng.Next(), 500f) * Dsp.Decay(t, 0.12f) * 1.5f;
                b[i] = Dsp.SoftClip(thud + muff, 2f);
            }
            return Dsp.Finish(b, 0.85f);
        }

        public static float[] ShellClink()
        {
            float[] b = Dsp.Buffer(0.25f);
            Dsp.Ping(b, 0f, 0.6f, 0.035f, 4200f, 6100f, 7800f);
            Dsp.Ping(b, 0.07f, 0.3f, 0.03f, 4500f, 6600f);
            return Dsp.Finish(b, 0.6f);
        }

        public static float[] Ricochet()
        {
            float[] b = Dsp.Buffer(0.45f);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Dsp.T(i);
                float f = 3200f - 1500f * Math.Min(1f, t / 0.4f) + Dsp.Sin(t * 180f) * 60f;
                b[i] = Dsp.Sin(Dsp.Advance(ref ph, f)) * Dsp.Decay(t, 0.12f) * Math.Min(1f, t / 0.01f) * 0.5f;
            }
            Dsp.NoiseBurst(b, 0f, 0.01f, 0.6f, 4500f, 2f, 0.003f, 197);
            return Dsp.Finish(b, 0.6f);
        }
    }
}
