using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Mixes the synthesised soundscape (I-31, I-27, I-19): ocean (volume by distance to the shore),
    /// wind (follows wind strength), day birds, night crickets, rain (follows rain intensity), delayed
    /// thunder, positional campfire crackle and surface-dependent footsteps. Underwater everything is muffled.
    /// </summary>
    public sealed class AudioSystem : MonoBehaviour
    {
        /// <summary>Footsteps played per surface (index = SurfaceType); read by the automated audio check.</summary>
        public readonly int[] StepCounts = new int[5];
        public int BirdCalls { get; private set; }
        public int ThunderPlayed { get; private set; }
        public float OceanVolume => ocean.volume;
        public float WindVolume => wind.volume;
        public float CricketVolume => crickets.volume;
        public float RainVolume => rain.volume;
        public float FireVolume => fire.volume;
        public float UnderwaterCutoff => underwaterFilter.cutoffFrequency;

        AudioSource ocean, wind, crickets, rain, fire, steps, birds, thunder;
        AudioLowPassFilter underwaterFilter;
        AudioClip[] birdClips, thunderClips;
        AudioClip[][] stepClips;
        DetRandom rng = new DetRandom(C.DefaultSeed, 271);
        float birdTimer = 2f, shoreTimer, shoreDistance = 50f;
        readonly float[] thunderAt = new float[8];
        readonly float[] thunderVolume = new float[8];
        int stepVariant;

        public void Init(Transform listener)
        {
            float t0 = Time.realtimeSinceStartup;
            ocean = Loop("Ocean", AudioSynth.Ocean(11));
            wind = Loop("Wind", AudioSynth.Wind(12));
            crickets = Loop("Crickets", AudioSynth.Crickets(13));
            rain = Loop("Rain", AudioSynth.Rain(14));
            fire = Loop("Campfire", AudioSynth.Fire(15));
            fire.spatialBlend = 1f;
            fire.rolloffMode = AudioRolloffMode.Linear;
            fire.minDistance = 1.5f;
            fire.maxDistance = 22f;
            fire.dopplerLevel = 0f;
            steps = OneShot("Footsteps");
            birds = OneShot("Birds");
            thunder = OneShot("Thunder");

            birdClips = new AudioClip[6];
            for (int i = 0; i < birdClips.Length; i++) birdClips[i] = AudioSynth.Bird(100 + i);
            thunderClips = new AudioClip[3];
            for (int i = 0; i < thunderClips.Length; i++) thunderClips[i] = AudioSynth.Thunder(200 + i);
            stepClips = new AudioClip[5][];
            for (int s = 0; s < 5; s++)
            {
                stepClips[s] = new AudioClip[3];
                for (int v = 0; v < 3; v++) stepClips[s][v] = AudioSynth.Footstep((SurfaceType)s, v);
            }
            for (int i = 0; i < thunderAt.Length; i++) thunderAt[i] = -1f;

            underwaterFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            underwaterFilter.cutoffFrequency = 22000f;
            if (Game.Lightning != null) Game.Lightning.OnThunder += ScheduleThunder;
            if (Game.Avatar != null) Game.Avatar.OnFootPlant += OnFootPlant;
            Debug.Log("[LI] Synthesised " + (5 + birdClips.Length + thunderClips.Length + 15) + " audio clips in " +
                      (Time.realtimeSinceStartup - t0).ToString("F2") + " s");
        }

        AudioSource Loop(string name, AudioClip clip)
        {
            var src = OneShot(name);
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.Play();
            return src;
        }

        AudioSource OneShot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            return src;
        }

        void ScheduleThunder(float delay, float distance)
        {
            for (int i = 0; i < thunderAt.Length; i++)
            {
                if (thunderAt[i] >= 0f) continue;
                thunderAt[i] = Time.time + delay;
                // Farther strikes are quieter: 1.0 at ~340 m down to ~0.45 at ~1370 m.
                thunderVolume[i] = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(C.ThunderMinDelay * C.SpeedOfSound, C.ThunderMaxDelay * C.SpeedOfSound, distance));
                return;
            }
        }

        void OnFootPlant(int foot)
        {
            var player = Game.Player;
            if (player == null || Game.World == null) return;
            Vector3 p = player.transform.position;
            SurfaceType s;
            if (player.WaterDepthHere > 0.06f) s = SurfaceType.Water;
            else if (Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, 0.8f, 1 << Layers.Props, QueryTriggerInteraction.Ignore))
                s = SurfaceType.Rock;   // standing on a rock, log or other prop
            else s = Game.World.Data.SurfaceAt(p);
            stepVariant = (stepVariant + 1 + rng.Range(0, 2)) % 3;
            steps.pitch = rng.Range(0.9f, 1.1f);
            steps.PlayOneShot(stepClips[(int)s][stepVariant], player.IsRunning ? 0.75f : 0.5f);
            StepCounts[(int)s]++;
        }

        void Update()
        {
            if (Game.Weather == null || Game.TimeOfDay == null) return;
            float dt = Time.deltaTime;
            var w = Game.Weather.Current;
            float night = Game.TimeOfDay.NightFactor;
            bool underwater = Game.Atmosphere != null && Game.Atmosphere.CameraUnderwater;

            shoreTimer -= dt;
            if (shoreTimer <= 0f && Game.MainCamera != null)
            {
                shoreTimer = 0.25f;
                shoreDistance = Game.World.Data.DistanceToShore(Game.MainCamera.transform.position);
            }
            float oceanVol = 1f - Mathf.InverseLerp(C.OceanFullVolumeDistance, C.OceanSilentDistance, shoreDistance);
            ocean.volume = Mathf.Lerp(ocean.volume, (0.08f + 0.62f * oceanVol * oceanVol) * Mathf.Lerp(1f, 1.3f, w.WaveScale - 1f), dt * 2f);
            wind.volume = Mathf.Lerp(wind.volume, 0.06f + 0.22f * Mathf.Clamp01(w.Wind / C.StormWindMultiplier), dt * 2f);
            wind.pitch = 0.9f + 0.15f * Mathf.Clamp01(w.Wind / C.StormWindMultiplier);
            crickets.volume = Mathf.Lerp(crickets.volume, 0.32f * night * (1f - w.Rain * 0.85f), dt * 1.5f);
            rain.volume = Mathf.Lerp(rain.volume, 0.75f * w.Rain, dt * 2f);

            var campfire = Game.World.Campfire;
            if (campfire != null)
            {
                fire.transform.position = campfire.transform.position + Vector3.up * 0.4f;
                fire.volume = 0.9f * (1f - w.Rain * 0.3f);
            }
            else fire.volume = 0f;

            float day = 1f - night;
            birdTimer -= dt;
            if (birdTimer <= 0f)
            {
                birdTimer = rng.Range(1.2f, 5f);
                if (day > 0.6f && w.Rain < 0.25f && !underwater)
                {
                    birds.panStereo = rng.Range(-0.8f, 0.8f);
                    birds.pitch = rng.Range(0.85f, 1.2f);
                    birds.PlayOneShot(birdClips[rng.Range(0, birdClips.Length)], 0.18f * day);
                    BirdCalls++;
                }
            }

            for (int i = 0; i < thunderAt.Length; i++)
            {
                if (thunderAt[i] < 0f || Time.time < thunderAt[i]) continue;
                thunder.pitch = rng.Range(0.85f, 1.05f);
                thunder.PlayOneShot(thunderClips[rng.Range(0, thunderClips.Length)], thunderVolume[i]);
                ThunderPlayed++;
                thunderAt[i] = -1f;
            }

            underwaterFilter.cutoffFrequency = Mathf.Lerp(underwaterFilter.cutoffFrequency, underwater ? 700f : 22000f, dt * 6f);
        }
    }
}
