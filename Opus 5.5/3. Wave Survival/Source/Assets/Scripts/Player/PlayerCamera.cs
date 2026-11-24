using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Mouse look, ADS field of view (S-11), aim recoil offset (rifle pattern), visual camera kick (S-12), damage
    /// flinch (S-06), screen shake, head bob (S-05) and the death camera.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class PlayerCamera : MonoBehaviour
    {
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float AdsBlend { get; private set; }     // 0 = hip, 1 = fully aimed (smoothed over 0.12 s)
        public Vector2 LookDelta { get; private set; }  // degrees this frame (used by weapon sway)

        PlayerRig rig;
        Transform camT;
        Vector2 recoil;            // x = pitch up, y = yaw right (degrees); part of the real aim direction
        float lastRecoilTime;
        SpringV3 kick;             // visual-only rotational punch (degrees)
        float shake;
        float deathT;
        float adsTarget;

        public void Init(PlayerRig owner, float yaw)
        {
            rig = owner;
            camT = owner.Camera.transform;
            Yaw = yaw;
            Pitch = GameConfig.Player.StartEuler.x;
        }

        public void SetAds(bool aiming) => adsTarget = aiming ? 1f : 0f;

        /// <summary>Recoil that moves the actual aim (crosshair) – recovers after the trigger is released.</summary>
        public void AddRecoil(float pitchUp, float yawRight)
        {
            recoil.x += pitchUp;
            recoil.y += yawRight;
            lastRecoilTime = Time.time;
        }

        /// <summary>Visual punch of the camera (degrees per second impulse on a spring).</summary>
        public void Kick(Vector3 eulerImpulse) => kick.Velocity += eulerImpulse;

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// <summary>S-06 flinch: the view jerks away from the attacker.</summary>
        public void Flinch(Vector3 sourcePos)
        {
            Vector3 local = transform.InverseTransformDirection(MathUtil.FlatDirection(transform.position, sourcePos));
            float f = GameConfig.Player.FlinchDegrees * 60f;
            kick.Velocity += new Vector3(-f * (0.6f + Random.value * 0.4f), -local.x * f * 0.8f, local.x * f * 0.9f + Random.Range(-0.3f, 0.3f) * f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (rig.Health.Alive && Game.IsPlaying)
            {
                InputState input = InputRouter.State;
                float sens = GameConfig.Player.MouseSensitivity * Mathf.Lerp(1f, GameConfig.Player.AdsSensitivityScale, AdsBlend);
                LookDelta = input.Look * sens;
                Yaw = Mathf.Repeat(Yaw + LookDelta.x, 360f);
                Pitch = Mathf.Clamp(Pitch - LookDelta.y, -GameConfig.Player.PitchLimit, GameConfig.Player.PitchLimit);
            }
            else LookDelta = Vector2.zero;

            // Recoil only recovers once the gun stops firing, so sustained fire really climbs (S-09 pattern).
            if (Time.time - lastRecoilTime > 0.12f && dt > 0f)
                recoil = Vector2.Lerp(recoil, Vector2.zero, 1f - Mathf.Exp(-GameConfig.View.RecoilRecovery * dt));

            float aimPitch = Mathf.Clamp(Pitch - recoil.x, -GameConfig.Player.PitchLimit, GameConfig.Player.PitchLimit);
            transform.rotation = Quaternion.Euler(0f, Yaw + recoil.y, 0f);

            // ADS (S-11): FOV 75 -> 45 over 0.12 s.
            if (dt > 0f) AdsBlend = Mathf.MoveTowards(AdsBlend, adsTarget, dt / GameConfig.View.AdsTime);
            rig.Camera.fieldOfView = Mathf.Lerp(GameConfig.View.HipFov, GameConfig.View.AdsFov, MathUtil.Smooth01(AdsBlend));

            // Visual layers: kick spring, shake noise, head bob, death fall.
            if (dt > 0f) kick.Step(Vector3.zero, GameConfig.View.KickStiffness, GameConfig.View.KickDamping, dt);
            shake = Mathf.Max(0f, shake - GameConfig.View.ShakeDecay * dt * Mathf.Max(0.3f, shake));
            float t = Time.time * 31f;
            Vector3 shakeRot = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * (shake * 6f);

            PlayerMotor m = rig.Motor;
            float bobScale = Mathf.Lerp(1f, 0.25f, AdsBlend) * Mathf.Min(1.6f, m.BobWeight);
            float phase = m.StepPhase * Mathf.PI;
            Vector3 bob = new Vector3(Mathf.Sin(phase) * GameConfig.Player.BobSideAmplitude,
                                      -Mathf.Abs(Mathf.Cos(phase)) * GameConfig.Player.BobAmplitude, 0f) * bobScale;
            if (m.LandImpact > 0f) kick.Velocity += new Vector3(Mathf.Min(m.LandImpact, 12f) * 6f, 0f, 0f);

            float eye = m.EyeHeight;
            float roll = 0f;
            if (!rig.Health.Alive)
            {
                // Death camera: drop to the floor and roll sideways.
                deathT = Mathf.Min(1f, deathT + dt / GameConfig.Player.DeathCameraTime);
                float e = MathUtil.Smooth01(deathT);
                eye = Mathf.Lerp(eye, 0.25f, e);
                roll = e * 70f;
            }
            rig.CameraPivot.localPosition = new Vector3(0f, eye, 0f);
            rig.CameraPivot.localRotation = Quaternion.Euler(aimPitch, 0f, 0f);
            camT.localPosition = bob;
            camT.localRotation = Quaternion.Euler(kick.Value + shakeRot + new Vector3(0f, 0f, roll));
        }
    }
}
