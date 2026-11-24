using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// CPU mirror of the water vertex shader: the same 4 Gerstner waves (I-05), the same shallow-water
    /// damping and the same clock, so swimming, the boat and underwater detection match the rendered surface.
    /// </summary>
    public static class WaveModel
    {
        const int WaveCount = 4;
        static readonly Vector2[] dir = new Vector2[WaveCount];
        static readonly float[] amp = new float[WaveCount];
        static readonly float[] k = new float[WaveCount];
        static readonly float[] omega = new float[WaveCount];
        static readonly float[] q = new float[WaveCount];
        static readonly Vector4[] shaderWaves = new Vector4[WaveCount];

        public static float Time;                 // shared water clock (pushed as _IslandTime)
        public static float AmplitudeScale = 1f;  // weather multiplier (Storm = 1.8)
        public static IslandData Island;          // for shore damping

        static WaveModel()
        {
            for (int i = 0; i < WaveCount; i++)
            {
                Vector4 w = GameConfig.GerstnerWaves[i];
                dir[i] = new Vector2(w.x, w.y).normalized;
                amp[i] = w.z;
                k[i] = 2f * Mathf.PI / w.w;
                omega[i] = Mathf.Sqrt(9.81f * k[i]);            // deep-water dispersion
                // Steepness shared out so the summed horizontal compression never loops (sum Q*k*A <= 1 at x1.8).
                q[i] = GameConfig.WaveSteepness / (k[i] * amp[i] * WaveCount);
                shaderWaves[i] = new Vector4(dir[i].x, dir[i].y, amp[i], k[i]);
            }
        }

        /// <summary>Pushes the static wave set to the water shader.</summary>
        public static void PushToShaders()
        {
            Shader.SetGlobalVectorArray(ShaderIds.GerstnerWaves, shaderWaves);
            Shader.SetGlobalVector(ShaderIds.GerstnerQ, new Vector4(q[0], q[1], q[2], q[3]));
            Shader.SetGlobalVector(ShaderIds.GerstnerOmega, new Vector4(omega[0], omega[1], omega[2], omega[3]));
        }

        public static float ShoreDamping(float x, float z)
        {
            if (Island == null) return 1f;
            float depth = GameConfig.SeaLevel - Island.HeightAt(x, z);
            float t = Mathf.Clamp01(depth / GameConfig.ShoreWaveDampDepth);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Displacement of the surface point that starts at (x0, z0).</summary>
        public static Vector3 Displacement(float x0, float z0)
        {
            float damp = ShoreDamping(x0, z0) * AmplitudeScale;
            Vector3 d = Vector3.zero;
            for (int i = 0; i < WaveCount; i++)
            {
                float a = amp[i] * damp;
                float theta = k[i] * (dir[i].x * x0 + dir[i].y * z0) - omega[i] * Time;
                float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                d.x += q[i] * a * dir[i].x * c;
                d.z += q[i] * a * dir[i].y * c;
                d.y += a * s;
            }
            return d;
        }

        /// <summary>Water surface height at world XZ (inverts the horizontal Gerstner shift by fixed-point iteration).</summary>
        public static float HeightAt(float x, float z)
        {
            float x0 = x, z0 = z;
            Vector3 d = Vector3.zero;
            for (int it = 0; it < 4; it++)
            {
                d = Displacement(x0, z0);
                x0 = x - d.x;
                z0 = z - d.z;
            }
            return GameConfig.SeaLevel + d.y;
        }

        /// <summary>Approximate surface normal at world XZ (finite differences of HeightAt).</summary>
        public static Vector3 NormalAt(float x, float z, float eps = 0.4f)
        {
            float hl = HeightAt(x - eps, z), hr = HeightAt(x + eps, z);
            float hd = HeightAt(x, z - eps), hu = HeightAt(x, z + eps);
            return new Vector3(hl - hr, 2f * eps, hd - hu).normalized;
        }
    }
}
