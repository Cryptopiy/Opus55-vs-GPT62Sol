using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Pooled audio playback: 2D voices for player/UI sounds and 3D positional voices for enemies, shots, impacts
    /// (S-26). Voices are reused round-robin, so playing a sound never allocates.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        AudioSource[] voices3D;
        AudioSource[] voices2D;
        int next3D, next2D;
        AudioSource ambient;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            AudioManager am = go.AddComponent<AudioManager>();
            am.Build();
            return am;
        }

        void Build()
        {
            AudioListener.volume = GameConfig.Audio.MasterVolume;
            voices3D = new AudioSource[GameConfig.Audio.Pool3D];
            for (int i = 0; i < voices3D.Length; i++)
            {
                AudioSource s = NewSource("Voice3D");
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = GameConfig.Audio.MinDistance;
                s.maxDistance = GameConfig.Audio.MaxDistance;
                s.dopplerLevel = 0f;
                voices3D[i] = s;
            }
            voices2D = new AudioSource[GameConfig.Audio.Pool2D];
            for (int i = 0; i < voices2D.Length; i++)
            {
                AudioSource s = NewSource("Voice2D");
                s.spatialBlend = 0f;
                voices2D[i] = s;
            }
            ambient = NewSource("Ambient");
            ambient.spatialBlend = 0f;
            ambient.loop = true;
            ambient.clip = SoundLibrary.Get(SoundId.AmbientDrone);
            ambient.volume = GameConfig.Audio.AmbientVolume;
            ambient.Play();
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            return s;
        }

        public void Play2D(SoundId id, float volume = 1f, float pitch = 1f)
        {
            AudioSource s = voices2D[next2D];
            next2D = (next2D + 1) % voices2D.Length;
            s.Stop();
            s.clip = SoundLibrary.Get(id);
            s.volume = volume;
            s.pitch = pitch;
            s.Play();
        }

        public void PlayAt(SoundId id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            // Prefer a free voice; otherwise steal the oldest one in round-robin order.
            AudioSource s = null;
            for (int k = 0; k < voices3D.Length; k++)
            {
                AudioSource c = voices3D[(next3D + k) % voices3D.Length];
                if (!c.isPlaying) { s = c; next3D = (next3D + k + 1) % voices3D.Length; break; }
            }
            if (s == null)
            {
                s = voices3D[next3D];
                next3D = (next3D + 1) % voices3D.Length;
            }
            s.Stop();
            s.transform.position = position;
            s.clip = SoundLibrary.Get(id);
            s.volume = volume;
            s.pitch = pitch;
            s.Play();
        }

        /// <summary>The player's own gunshots: slight pitch variation keeps rapid fire from sounding robotic.</summary>
        public void PlayShot(SoundId id, Vector3 position) => Play2D(id, 0.9f, Random.Range(0.95f, 1.05f));

        public void PlayFootstep(Vector3 position, float volume) =>
            Play2D(SoundId.Footstep, GameConfig.Player.FootstepVolume * volume, Random.Range(0.9f, 1.1f));

        public void SetPaused(bool paused) => AudioListener.pause = paused;
    }
}
