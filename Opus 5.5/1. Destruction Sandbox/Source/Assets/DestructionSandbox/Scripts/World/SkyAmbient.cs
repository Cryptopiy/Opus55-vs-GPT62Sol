using UnityEngine;
using UnityEngine.Rendering;
using L = DestructionSandbox.GameConfig.Lighting;

namespace DestructionSandbox
{
    /// <summary>
    /// "Ambient light from sky" (D-02): projects the same gradient used by the sky shader into the L2 spherical
    /// harmonics ambient probe. Each sky direction is added as a tiny directional light weighted by its solid
    /// angle (4/N keeps a uniform sky of radiance L at an ambient of exactly L on a white surface).
    /// </summary>
    public static class SkyAmbient
    {
        public static SphericalHarmonicsL2 Project(int samples)
        {
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            float weight = 4f / samples;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < samples; i++)
            {
                // Fibonacci sphere: evenly spread directions.
                float y = 1f - (i + 0.5f) / samples * 2f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float phi = i * golden;
                var dir = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
                Color c = SkyColor(dir).linear;      // SH works in linear space; sky colours are authored in sRGB
                sh.AddDirectionalLight(dir, c, weight);
            }
            return sh;
        }

        /// <summary>C# twin of DSSkyColor in DS_Common.hlsl.</summary>
        public static Color SkyColor(Vector3 dir)
        {
            float y = dir.y;
            if (y >= 0f)
            {
                float t = Mathf.Pow(Mathf.Clamp01(y), 0.55f);
                return Color.Lerp(L.SkyHorizon, L.SkyZenith, t);
            }
            return Color.Lerp(L.SkyHorizon, L.SkyGround, Mathf.Clamp01(-y * 4f));
        }
    }
}
