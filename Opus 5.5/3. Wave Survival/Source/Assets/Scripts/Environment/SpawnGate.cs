using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// S-02 / S-20 spawn gate: a doorway frame in the wall with an emissive door panel and a light that pulses for
    /// <see cref="GameConfig.Waves.GatePulse"/> seconds before a group spawns.
    /// </summary>
    public sealed class SpawnGate : MonoBehaviour
    {
        public int Index;
        public Light Light;
        public Renderer Door;
        public Vector3[] Slots = new Vector3[4];   // world spawn positions in front of the door (sampled on the NavMesh)

        float pulseTimer, pulseDuration;
        MaterialPropertyBlock mpb;

        public bool IsPulsing => pulseTimer > 0f;
        public Vector3 SpawnCenter => transform.position + transform.forward * GameConfig.Arena.GateSpawnOffset;

        void Awake() => mpb = new MaterialPropertyBlock();

        public void Pulse(float duration)
        {
            pulseDuration = Mathf.Max(0.01f, duration);
            pulseTimer = pulseDuration;
            if (Game.Audio != null) Game.Audio.PlayAt(SoundId.GateHum, SpawnCenter + Vector3.up * 2f, 0.8f);
        }

        void Update()
        {
            float k = 0f;
            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(pulseTimer / pulseDuration);
                // Pulsing that speeds up toward the spawn moment.
                float wave = 0.5f + 0.5f * Mathf.Sin(t * t * GameConfig.Lighting.GatePulseHz * 2f * Mathf.PI * 2f);
                k = Mathf.Lerp(0.35f, 1f, t) * (0.4f + 0.6f * wave);
            }
            float idle = GameConfig.Lighting.GateLightIdle;
            Light.intensity = Mathf.Lerp(idle, GameConfig.Lighting.GateLightPulse, k);
            mpb.SetColor(MaterialFactory.EmissionColor, GameConfig.Lighting.GateColor * Mathf.Lerp(0.25f, 6f, k));
            Door.SetPropertyBlock(mpb);
        }
    }
}
