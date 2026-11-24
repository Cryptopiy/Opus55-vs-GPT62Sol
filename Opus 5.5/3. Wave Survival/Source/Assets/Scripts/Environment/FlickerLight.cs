using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Ceiling lamp. When <see cref="Flicker"/> is on (S-03: 2 of the 8 lamps) the light and the bulb's emission
    /// buzz with noise and occasionally drop out, like a failing industrial fixture.
    /// </summary>
    public sealed class FlickerLight : MonoBehaviour
    {
        public Light Light;
        public Renderer Bulb;
        public bool Flicker;
        public int Seed;

        float baseIntensity;
        float dropTimer;
        float stepTimer;
        float target = 1f, current = 1f;
        MaterialPropertyBlock mpb;
        static readonly Color BulbEmission = new Color(6f, 4.6f, 3.1f);

        void Start()
        {
            baseIntensity = Light.intensity;
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!Flicker) return;
            float dt = Time.deltaTime;
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = 1f / GameConfig.Lighting.FlickerSpeed * Random.Range(0.5f, 1.5f);
                target = Random.Range(0.55f, 1.05f);
                if (Random.value < GameConfig.Lighting.FlickerDropChance) dropTimer = Random.Range(0.05f, 0.22f);
            }
            if (dropTimer > 0f) { dropTimer -= dt; target = 0.04f; }
            current = MathUtil.Damp(current, target, 40f, dt);
            Light.intensity = baseIntensity * current;
            mpb.SetColor(MaterialFactory.EmissionColor, BulbEmission * current);
            Bulb.SetPropertyBlock(mpb);
        }
    }
}
