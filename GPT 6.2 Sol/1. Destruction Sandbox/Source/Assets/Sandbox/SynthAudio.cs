using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal enum Sound
    {
        Cannon, Shell, OrbShot, Blast, Stone, Wood, Glass, Hum, Release, Click, Heavy
    }

    internal sealed class SynthAudio
    {
        readonly AudioClip[] clips = new AudioClip[11];
        readonly AudioSource[] voices = new AudioSource[VoiceCap];
        readonly float[] initialGain = new float[VoiceCap], pitch = new float[VoiceCap];
        readonly bool[] ui = new bool[VoiceCap];
        readonly Transform listener;
        bool slowed;
        float master = .75f;
        public int StartedMask { get; private set; }
        public int Playing
        {
            get
            {
                int count = 0;
                foreach (AudioSource voice in voices) if (voice.isPlaying) count++;
                return count;
            }
        }
        public float Master
        {
            get => master;
            set
            {
                master = Mathf.Clamp01(value);
                for (int i = 0; i < voices.Length; i++)
                    voices[i].volume = master * initialGain[i];
            }
        }

        public SynthAudio(Sandbox game)
        {
            listener = game.Player.Camera.transform;
            float[] times = { .58f, .37f, .60f, 1.65f, .27f, .25f, .94f, OrbTime, 1.18f, .105f, .59f };
            for (int i = 0; i < clips.Length; i++)
                clips[i] = Synthesize((Sound)i, times[i]);
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = game.Node("Voice " + (i + 1)).AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.dopplerLevel = 0;
                voice.minDistance = 8;
                voice.maxDistance = 90;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voices[i] = voice;
            }
        }

        public void Play(Sound sound, Vector3 p, float volume, float rate = 1)
        {
            if (master <= 0 || Application.isBatchMode) return;
            bool centered = sound == Sound.Click;
            int slot = 0;
            float quietest = float.MaxValue;
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource candidate = voices[i];
                if (!candidate.isPlaying) { slot = i; break; }
                float distance = ui[i] ? 0 : Vector3.Distance(listener.position, candidate.transform.position);
                float progress = candidate.time / candidate.clip.length;
                float gain = initialGain[i] / Mathf.Max(1, distance / candidate.minDistance);
                // Peak decay is a close proxy for the remaining audible level of these one-shots.
                if (candidate.clip != clips[(int)Sound.Hum]) gain *= Mathf.Max(.03f, 1 - progress);
                if (gain < quietest) { quietest = gain; slot = i; }
            }

            AudioSource voice = voices[slot];
            voice.Stop();
            voice.transform.position = p;
            voice.clip = clips[(int)sound];
            voice.loop = sound == Sound.Hum;
            voice.spatialBlend = centered ? 0 : .9f;
            initialGain[slot] = Mathf.Clamp01(volume);
            pitch[slot] = rate;
            ui[slot] = centered;
            voice.volume = master * initialGain[slot];
            voice.pitch = rate * (slowed && !centered ? .58f : 1);
            voice.Play();
            StartedMask |= 1 << (int)sound;
        }

        public void SetClock(bool pause, bool slow)
        {
            slowed = slow;
            for (int i = 0; i < voices.Length; i++)
            {
                if (ui[i]) continue;
                voices[i].pitch = pitch[i] * (slowed ? .58f : 1);
                if (pause) voices[i].Pause();
                else voices[i].UnPause();
            }
        }

        public void Reset()
        {
            foreach (AudioSource voice in voices) voice.Stop();
        }

        public void StopHum(Vector3 position)
        {
            foreach (AudioSource voice in voices)
                if (voice.clip == clips[(int)Sound.Hum] && (voice.transform.position - position).sqrMagnitude < .01f)
                    voice.Stop();
        }

        static AudioClip Synthesize(Sound sound, float duration)
        {
            const int sampleRate = 32000;
            int length = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[length];
            uint rng = (uint)sound * 41113u + 17431u;
            float low = 0, bass = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float white = (rng & 65535) / 32768f - 1f;
                low += .13f * (white - low);
                bass += .022f * (white - bass);
                float sample = sound switch
                {
                    Sound.Cannon => Wave(44 * t + 5 * (1 - Mathf.Exp(-25 * t))) * Decay(t, 9) * .72f
                        + low * Decay(t, 13) * 1.36f + white * Decay(t, 93) * .25f,
                    Sound.Shell => Wave(81 * t + 7 * (1 - Mathf.Exp(-21 * t))) * Decay(t, 12) * .48f
                        + (low * 1.4f + white * .27f) * Decay(t, 20),
                    Sound.OrbShot => Wave(155 * t + 11 * Mathf.Sin(t * 8)) * Decay(t, 7) * .44f +
                        Wave(301 * t + 4 * Mathf.Sin(t * 12)) * Decay(t, 9) * .35f,
                    Sound.Blast => Wave(33 * t + 4 * (1 - Mathf.Exp(-19 * t))) * Decay(t, 3.2f) * .83f
                        + low * Decay(t, 2.7f) * .88f + bass * Decay(t, 1.7f) * 1.4f + white * Decay(t, 71) * .32f,
                    Sound.Stone => (Wave(143 * t) * .32f + Wave(304 * t) * .16f + low * 1.0f) * Decay(t, 26),
                    Sound.Wood => (Wave(245 * t) * .34f + Wave(529 * t) * .24f + white * .48f) * Decay(t, 41),
                    Sound.Glass => Wave(1517 * t) * Decay(t, 14) * .24f + Wave(2281 * t) * Decay(t, 21) * .19f +
                        Wave(3113 * t) * Decay(t, 30) * .18f + (white - low) * Decay(t, 42) * .31f +
                        Wave(3941 * t) * Decay(t, 15) * (.11f + .09f * Wave(t * 13)),
                    Sound.Hum => (Wave(48 * t + .12f * Mathf.Sin(t * 9)) * .3f + Wave(97 * t) * .15f +
                        Wave(137 * t + .5f * Mathf.Sin(t * 3)) * .14f) * (.72f + .28f * Wave(2.8f * t)),
                    Sound.Release => (Wave(51 * t + 6 * (1 - Mathf.Exp(-13 * t))) * .65f + bass * 1.3f) * Decay(t, 5.7f) +
                        Wave(373 * t + 19 * (1 - Mathf.Exp(-8 * t))) * Decay(t, 11) * .29f,
                    Sound.Click => (Wave(860 * t) * .40f + Wave(1320 * t) * .22f + white * .22f) * Decay(t, 65),
                    _ => (Wave(63 * t + 2 * (1 - Mathf.Exp(-30 * t))) * .68f + low * .9f) * Decay(t, 11f)
                };
                float attack = Mathf.Min(1, t * (sound == Sound.Hum ? 6 : 430));
                float tail = Mathf.Clamp01((duration - t) * (sound == Sound.Hum ? 9 : 60));
                samples[i] = Mathf.Clamp(sample * attack * tail, -.94f, .94f);
            }
            var clip = AudioClip.Create(sound.ToString(), length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float Wave(float cycles) => Mathf.Sin(cycles * (2 * Mathf.PI));
        static float Decay(float time, float rate) => Mathf.Exp(-time * rate);
    }
}
