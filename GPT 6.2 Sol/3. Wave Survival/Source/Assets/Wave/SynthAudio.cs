using System;
using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal enum Sound
    {
        Pistol, Rifle, Shotgun, Open, Seat, Step, Runner, Shooter, Tank, Warden, Pickup, Hit, Kill, Horn, Death, Slam, Drone
    }

    internal sealed class SynthAudio
    {
        const int Rate = 32000;
        readonly AudioSource[] voices = new AudioSource[VoicePoolSize];
        readonly AudioClip[] clips = new AudioClip[(int)Sound.Drone + 1];
        int cursor;

        public SynthAudio(Transform parent)
        {
            var root = Geometry.Root("Synthesized audio", parent);
            float[] duration = { .29f, .24f, .53f, .19f, .19f, .24f, .61f, .68f, 1.09f, 1.26f,
                .41f, .079f, .27f, 1.41f, .44f, .57f, 8 };
            for (int i = 0; i < clips.Length; i++) clips[i] = Create((Sound)i, duration[i]);
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = Geometry.Root("Positional voice", root).gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 1;
                voice.maxDistance = 31;
                voice.dopplerLevel = 0;
                voices[i] = voice;
            }
            var ambience = root.gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.clip = clips[(int)Sound.Drone];
            ambience.volume = .26f;
            ambience.loop = true;
            ambience.Play();
        }

        public void Play(Sound sound, Vector3 at, float volume, bool positional = true, float pitch = 1)
        {
            int next = cursor;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (cursor + i) % voices.Length;
                if (voices[candidate].isPlaying) continue;
                next = candidate;
                break;
            }
            cursor = (next + 1) % voices.Length;
            AudioSource source = voices[next];
            source.Stop();
            source.transform.position = at;
            source.spatialBlend = positional ? 1 : 0;
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clips[(int)sound];
            source.Play();
        }

        static AudioClip Create(Sound sound, float duration)
        {
            int length = Mathf.CeilToInt(duration * Rate);
            var samples = new float[length];
            var random = new System.Random(1443 + (int)sound * 37);
            float low = 0, faster = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)random.NextDouble() * 2 - 1;
                low += (n - low) * .052f;
                faster += (n - faster) * .26f;
                float v = sound switch
                {
                    Sound.Pistol => .78f * n * Decay(t, 62) + .65f * Tone(192 * t - 155 * t * t) * Decay(t, 34) + .17f * faster * Decay(t, 13),
                    Sound.Rifle => .85f * n * Decay(t, 87) + .74f * Tone(123 * t - 90 * t * t) * Decay(t, 37) + .34f * faster * Decay(t, 19),
                    Sound.Shotgun => .91f * faster * Decay(t, 21) + .97f * Tone(77 * t - 31 * t * t) * Decay(t, 17) + .49f * n * Decay(t, 49) + .21f * low * Decay(t, 6),
                    Sound.Open => (.31f * n + .39f * Tone(1030 * t)) * Decay(t, 52) + .66f * faster * Offset(t, .08f, 53),
                    Sound.Seat => (.68f * faster + .28f * Tone(414 * t)) * Decay(t, 46) + .53f * n * Offset(t, .053f, 72),
                    Sound.Step => (.52f * low + .36f * Tone(89 * t - 25 * t * t) + .17f * n) * Decay(t, 17),
                    Sound.Runner => Growl(t, 124, 13, faster, duration),
                    Sound.Shooter => Growl(t, 181, 23, faster, duration) * .7f + .23f * Tone(311 * t + t * Mathf.Sin(t * 90)) * Decay(t, 7),
                    Sound.Tank => Growl(t, 56, 8, low, duration),
                    Sound.Warden => Growl(t, 44, 15, faster, duration) + .26f * Tone(60 * t - 9 * t * t) * Decay(t, 3),
                    Sound.Pickup => .31f * Tone(660 * t) * Decay(t, 17) + .38f * Tone(880 * t) * Offset(t, .072f, 11) +
                        .32f * Tone(1320 * t) * Offset(t, .15f, 11),
                    Sound.Hit => (.5f * Tone(1750 * t) + .35f * n) * Decay(t, 85),
                    Sound.Kill => (.51f * Tone(1160 * t) + .26f * Tone(580 * t)) * Decay(t, 26) + .38f * Tone(1550 * t) * Offset(t, .047f, 25),
                    Sound.Horn => (.53f * Tone(103 * t) + .26f * Tone(206 * t) + .18f * Tone(309 * t)) *
                        Mathf.Min(1, t * 21) * (.76f + .24f * Tone(3.5f * t)) * Decay(t, 2.7f),
                    Sound.Death => (.52f * Tone(77 * t - t * t * 55) + .55f * faster) * Decay(t, 11),
                    Sound.Slam => (.94f * Tone(48 * t - 18 * t * t) + .52f * low + .25f * n) * Decay(t, 8),
                    _ => (.18f * Tone(41 * t) + .15f * Tone(61 * t) + .09f * Tone(117 * t)) * (.66f + .34f * Tone(.25f * t)) +
                        .09f * Tone(177 * t + .12f * Tone(.5f * t))
                };
                float tail = sound == Sound.Drone ? 1 : Mathf.Clamp01((duration - t) * 35);
                samples[i] = .86f * v / (1 + Mathf.Abs(v) * .65f) * tail;
            }
            AudioClip clip = Geometry.Own(AudioClip.Create(sound.ToString(), length, 1, Rate, false));
            clip.SetData(samples, 0);
            return clip;
        }

        static float Tone(float cycles) => Mathf.Sin(cycles * 2 * Mathf.PI);
        static float Decay(float t, float rate) => Mathf.Exp(-t * rate);
        static float Offset(float t, float at, float rate) => t < at ? 0 : Decay(t - at, rate);
        static float Growl(float t, float frequency, float wobble, float noise, float duration)
        {
            float phase = frequency * t + .29f * Tone(wobble * t) - 9 * t * t;
            return (.55f * Tone(phase) + .24f * Tone(phase * 2) + .2f * Tone(phase * 3) + .38f * noise) *
                Mathf.Sin(Mathf.PI * t / duration) * (.81f + .19f * Tone(t * 7));
        }
    }
}

