using UnityEngine;
using Fx = DestructionSandbox.GameConfig.Effects;
using Wpn = DestructionSandbox.GameConfig.Weapons;

namespace DestructionSandbox
{
    /// <summary>
    /// Trauma-based camera shake on the rig between the eye pivot and the camera. Shake = trauma^2 so small
    /// hits stay subtle; Perlin noise keeps the motion smooth. Uses unscaled time (shakes even in slow motion).
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private float trauma;
        private float seed;

        public float Trauma => trauma;

        private void Awake()
        {
            seed = Random.value * 100f;
        }

        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>D-18: full shake at &lt;= 5 m, none at &gt;= 40 m, linear in between.</summary>
        public void ShakeFromExplosion(Vector3 position, float strength)
        {
            float distance = Vector3.Distance(position, transform.position);
            float t = 1f - Mathf.InverseLerp(Wpn.ShakeFullDistance, Wpn.ShakeNoneDistance, distance);
            if (t > 0f) AddTrauma(strength * t);
        }

        public void ResetShake()
        {
            trauma = 0f;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (trauma <= 0f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }
            float t = Time.unscaledTime * Fx.CameraShakeFrequency;
            float shake = trauma * trauma;
            float nx = Mathf.PerlinNoise(seed, t) * 2f - 1f;
            float ny = Mathf.PerlinNoise(seed + 13.1f, t) * 2f - 1f;
            float nz = Mathf.PerlinNoise(seed + 27.7f, t) * 2f - 1f;
            float nr = Mathf.PerlinNoise(seed + 41.3f, t) * 2f - 1f;
            transform.localPosition = new Vector3(nx, ny, nz * 0.5f) * (Fx.CameraShakeMaxOffset * shake);
            transform.localRotation = Quaternion.Euler(ny * Fx.CameraShakeMaxAngle * shake, nx * Fx.CameraShakeMaxAngle * shake, nr * Fx.CameraShakeMaxAngle * shake);
            trauma = Mathf.Max(0f, trauma - Fx.CameraShakeDecay * Time.unscaledDeltaTime);
        }
    }
}
