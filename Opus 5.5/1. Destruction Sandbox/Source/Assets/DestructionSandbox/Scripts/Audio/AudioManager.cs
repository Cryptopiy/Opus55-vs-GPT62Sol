using UnityEngine;
using A = DestructionSandbox.GameConfig.Audio;

namespace DestructionSandbox
{
    /// <summary>
    /// D-25 voice manager: exactly 16 AudioSources. A new sound takes a free voice, otherwise it steals the
    /// currently quietest voice (estimated from volume, distance attenuation and remaining decay); if even the
    /// quietest voice is louder than the new sound, the new sound is dropped. Impact sounds are collected per
    /// physics step and only the loudest few are played, so a collapse cannot flood the mixer.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public float BaseVolume;
            public float BasePitch;
            public float StartTime;
            public float Duration;
            public bool Loop;
            public int Id;
        }

        private struct ImpactRequest
        {
            public BlockMaterialType Material;
            public Vector3 Position;
            public float Impulse;
            public bool Break;
            public float Loudness;
        }

        private Voice[] voices;
        private readonly ImpactRequest[] pending = new ImpactRequest[A.MaxImpactSoundsPerStep];
        private int pendingCount;
        private int nextId = 1;
        private float pitchScale = 1f;
        private Transform listener;

        public AudioClip CannonShot, ShellShot, OrbShot, Explosion, StoneImpact, WoodImpact, GlassBreak, HeavyImpact, OrbHum, OrbImplosion, UiClick;

        public int VoiceCount => voices.Length;
        public int StolenVoices { get; private set; }
        public int DroppedSounds { get; private set; }
        public float MasterVolume
        {
            get => AudioListener.volume;
            set => AudioListener.volume = Mathf.Clamp01(value);
        }

        public void Init(Transform listenerTransform)
        {
            listener = listenerTransform;
            var synth = new AudioSynth(A.SampleRate, A.SynthSeed);
            CannonShot = synth.CannonShot();
            ShellShot = synth.ShellShot();
            OrbShot = synth.OrbShot();
            Explosion = synth.Explosion();
            StoneImpact = synth.StoneImpact();
            WoodImpact = synth.WoodImpact();
            GlassBreak = synth.GlassBreak();
            HeavyImpact = synth.HeavyImpact();
            OrbHum = synth.OrbHum();
            OrbImplosion = synth.OrbImplosion();
            UiClick = synth.UiClick();

            voices = new Voice[A.MaxVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = A.SpatialMinDistance;
                src.maxDistance = A.SpatialMaxDistance;
                src.dopplerLevel = 0f;
                src.spatialBlend = 1f;
                voices[i] = new Voice { Source = src };
            }
            MasterVolume = A.DefaultMasterVolume;
        }

        public int BusyVoices()
        {
            int n = 0;
            for (int i = 0; i < voices.Length; i++) if (voices[i].Source.isPlaying) n++;
            return n;
        }

        /// <summary>D-20: slow motion lowers the pitch of everything currently playing and of future sounds.</summary>
        public void SetPitchScale(float scale)
        {
            pitchScale = scale;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].Source.isPlaying && voices[i].Source.spatialBlend > 0f)
                    voices[i].Source.pitch = voices[i].BasePitch * pitchScale;
        }

        public void SetPaused(bool paused)
        {
            AudioListener.pause = paused;
        }

        private float Attenuation(Vector3 position, bool spatial)
        {
            if (!spatial || listener == null) return 1f;
            float d = Vector3.Distance(position, listener.position);
            return Mathf.Min(1f, A.SpatialMinDistance / Mathf.Max(d, A.SpatialMinDistance));
        }

        private float CurrentLoudness(Voice v, float now)
        {
            if (!v.Source.isPlaying) return 0f;
            float env = 1f;
            if (!v.Loop)
            {
                float k = Mathf.Clamp01((now - v.StartTime) / Mathf.Max(0.01f, v.Duration));
                env = (1f - k) * (1f - k);
            }
            return v.BaseVolume * env * Attenuation(v.Source.transform.position, v.Source.spatialBlend > 0f);
        }

        /// <summary>Plays a clip on a free or stolen voice. Returns the voice id (or -1 if dropped).</summary>
        public int Play(AudioClip clip, Vector3 position, float volume, float pitch, bool spatial, bool loop = false)
        {
            float now = Time.unscaledTime;
            Voice target = null;
            float quietest = float.MaxValue;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.Source.isPlaying) { target = v; quietest = -1f; break; }
                float l = CurrentLoudness(v, now);
                if (l < quietest) { quietest = l; target = v; }
            }
            float loudness = volume * Attenuation(position, spatial);
            if (quietest >= 0f)
            {
                if (quietest > loudness) { DroppedSounds++; return -1; }
                StolenVoices++;
                target.Source.Stop();
            }

            AudioSource src = target.Source;
            src.transform.position = position;
            src.spatialBlend = spatial ? 1f : 0f;
            src.ignoreListenerPause = !spatial;
            src.clip = clip;
            src.loop = loop;
            src.volume = Mathf.Clamp01(volume);
            float p = pitch * (spatial ? pitchScale : 1f);
            src.pitch = p;
            src.Play();
            target.BaseVolume = volume;
            target.BasePitch = pitch;
            target.StartTime = now;
            target.Duration = clip.length / Mathf.Max(0.05f, p);
            target.Loop = loop;
            target.Id = nextId++;
            return target.Id;
        }

        public void StopVoice(int id)
        {
            if (id < 0) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].Id == id && voices[i].Source.isPlaying) { voices[i].Source.Stop(); return; }
        }

        public void StopAll()
        {
            for (int i = 0; i < voices.Length; i++) voices[i].Source.Stop();
            pendingCount = 0;
        }

        // ---- Gameplay sounds -------------------------------------------------------------------------------

        public void PlayShot(WeaponType type, Vector3 position)
        {
            AudioClip clip = type == WeaponType.Cannonball ? CannonShot : type == WeaponType.Shell ? ShellShot : OrbShot;
            Play(clip, position, 1f, 1f + Random.Range(-A.PitchJitter, A.PitchJitter), true);
        }

        public void PlayExplosion(Vector3 position) => Play(Explosion, position, 1f, 1f + Random.Range(-A.PitchJitter, A.PitchJitter), true);
        public void PlayHeavyImpact(Vector3 position, float strength) => Play(HeavyImpact, position, Mathf.Lerp(0.5f, 1f, strength), 1f, true);
        public int PlayOrbHum(Vector3 position) => Play(OrbHum, position, 0.85f, 1f, true, true);
        public void PlayOrbImplosion(Vector3 position) => Play(OrbImplosion, position, 1f, 1f, true);
        public void PlayUiClick() => Play(UiClick, Vector3.zero, 0.7f, 1f, false);

        /// <summary>Collects an impact; only the loudest MaxImpactSoundsPerStep of a physics step are played.</summary>
        public void RequestImpact(BlockMaterialType material, Vector3 position, float impulse, bool isBreak)
        {
            float norm = Mathf.InverseLerp(Mathf.Log10(A.ImpactMinImpulse), Mathf.Log10(A.ImpactMaxImpulse), Mathf.Log10(Mathf.Max(impulse, 1e-3f)));
            float loudness = Mathf.Lerp(A.ImpactMinVolume, A.ImpactMaxVolume, norm) * (isBreak ? 1f : 0.7f) * Attenuation(position, true);
            var req = new ImpactRequest { Material = material, Position = position, Impulse = norm, Break = isBreak, Loudness = loudness };
            if (pendingCount < pending.Length) { pending[pendingCount++] = req; return; }
            int weakest = 0;
            for (int i = 1; i < pendingCount; i++) if (pending[i].Loudness < pending[weakest].Loudness) weakest = i;
            if (pending[weakest].Loudness < loudness) pending[weakest] = req;
        }

        public void FlushImpacts()
        {
            for (int i = 0; i < pendingCount; i++)
            {
                ImpactRequest r = pending[i];
                AudioClip clip;
                if (r.Material == BlockMaterialType.Glass) clip = r.Break ? GlassBreak : StoneImpact;
                else clip = r.Material == BlockMaterialType.Wood ? WoodImpact : StoneImpact;
                float volume = Mathf.Lerp(A.ImpactMinVolume, A.ImpactMaxVolume, r.Impulse) * (r.Break ? 1f : 0.7f);
                float pitch = Mathf.Lerp(A.ImpactPitchSoft, A.ImpactPitchHard, r.Impulse) + Random.Range(-A.PitchJitter, A.PitchJitter);
                if (r.Material == BlockMaterialType.Glass && !r.Break) pitch *= 1.6f;   // light glass clink
                Play(clip, r.Position, volume, pitch, true);
            }
            pendingCount = 0;
        }
    }
}
