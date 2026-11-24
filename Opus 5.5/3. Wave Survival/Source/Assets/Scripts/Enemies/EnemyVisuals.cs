using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Per-enemy material effects through MaterialPropertyBlocks: 0.08 s emissive hit flash, coloured glow (tank
    /// charge telegraph, boss enrage) and the death fade. Blocks are cleared when idle so the SRP batcher applies again.
    /// </summary>
    public sealed class EnemyVisuals
    {
        static MaterialPropertyBlock mpb;
        readonly EnemyBody body;
        float flashTimer;
        Color tint;
        float tintWeight;
        float alpha = 1f;
        bool fading, wasActive;

        public EnemyVisuals(EnemyBody b)
        {
            body = b;
            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        public void Flash() => flashTimer = GameConfig.Fx.HitFlashTime;

        public void SetTint(Color color, float weight)
        {
            tint = color;
            tintWeight = weight;
        }

        public void BeginFade()
        {
            fading = true;
            for (int i = 0; i < body.Renderers.Count; i++)
            {
                Renderer r = body.Renderers[i];
                r.sharedMaterial = body.Mats.FadeOf(body.BaseMaterials[i]);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        public void SetAlpha(float a) => alpha = a;

        public void Reset()
        {
            flashTimer = 0f;
            tintWeight = 0f;
            alpha = 1f;
            fading = false;
            wasActive = false;
            body.RestoreMaterials();
        }

        public void Update(float dt)
        {
            if (flashTimer > 0f) flashTimer -= dt;
            bool active = flashTimer > 0f || tintWeight > 0.001f || fading;
            if (!active)
            {
                if (wasActive)
                {
                    for (int i = 0; i < body.Renderers.Count; i++) body.Renderers[i].SetPropertyBlock(null);
                    wasActive = false;
                }
                return;
            }
            wasActive = true;
            float flash = flashTimer > 0f ? GameConfig.Fx.HitFlashIntensity : 0f;
            for (int i = 0; i < body.Renderers.Count; i++)
            {
                Color e = body.BaseEmission[i];
                if (body.TintMask[i]) e += tint * tintWeight;
                e += new Color(flash, flash, flash, 0f);
                mpb.Clear();
                mpb.SetColor(MaterialFactory.EmissionColor, e);
                if (fading)
                {
                    Color c = body.BaseColors[i];
                    c.a = alpha;
                    mpb.SetColor(MaterialFactory.BaseColor, c);
                }
                body.Renderers[i].SetPropertyBlock(mpb);
            }
        }
    }
}
