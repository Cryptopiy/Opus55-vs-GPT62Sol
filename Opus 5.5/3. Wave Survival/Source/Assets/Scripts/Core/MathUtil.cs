using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Small allocation-free math helpers shared by gameplay code.</summary>
    public static class MathUtil
    {
        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            float m = d.magnitude;
            return m > 1e-4f ? d / m : Vector3.forward;
        }

        public static Vector3 RotateY(Vector3 v, float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * v;

        /// <summary>Frame-rate independent exponential smoothing toward a target.</summary>
        public static float Damp(float current, float target, float rate, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));

        public static Vector3 Damp(Vector3 current, Vector3 target, float rate, float dt) =>
            Vector3.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>
        /// Random direction inside a cone of the given half-angle. Sampling sqrt(u) keeps the hits uniformly
        /// distributed over the disk the cone projects onto (no clumping in the centre).
        /// </summary>
        public static Vector3 RandomInCone(Vector3 dir, float halfAngleDeg)
        {
            if (halfAngleDeg <= 0f) return dir;
            Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, dir);
            float r = halfAngleDeg * Mathf.Deg2Rad * Mathf.Sqrt(Random.value);
            float theta = Random.value * Mathf.PI * 2f;
            Vector3 offset = right * Mathf.Cos(theta) + up * Mathf.Sin(theta);
            return (dir * Mathf.Cos(r) + offset * Mathf.Sin(r)).normalized;
        }

        /// <summary>Rotates a direction by exactly <paramref name="degrees"/> around a random perpendicular axis.</summary>
        public static Vector3 OffsetByAngle(Vector3 dir, float degrees)
        {
            Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            Vector3 axis = Quaternion.AngleAxis(Random.value * 360f, dir) * right;
            return Quaternion.AngleAxis(degrees, axis) * dir;
        }

        public static float SignedAngleY(Vector3 from, Vector3 to) =>
            Vector3.SignedAngle(Flat(from), Flat(to), Vector3.up);
    }

    /// <summary>Critically-damped style spring used for weapon/camera procedural animation.</summary>
    public struct SpringV3
    {
        public Vector3 Value;
        public Vector3 Velocity;

        public void Step(Vector3 target, float stiffness, float damping, float dt)
        {
            // Sub-step so large frame times never make the spring explode.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 accel = (target - Value) * stiffness - Velocity * damping;
                Velocity += accel * h;
                Value += Velocity * h;
            }
        }

        public void Reset() { Value = Vector3.zero; Velocity = Vector3.zero; }
    }
}
