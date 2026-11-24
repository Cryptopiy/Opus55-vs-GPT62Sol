using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Procedural viewmodel animation (S-12, S-13): hip/ADS blend, mouse sway, movement bob, sprint pose, recoil
    /// springs, slide/pump/magazine/shell motion, and the lowered pose during reloads and weapon switches.
    /// </summary>
    public sealed class ViewmodelAnimator : MonoBehaviour
    {
        PlayerRig rig;
        WeaponController wc;
        Viewmodel[] models;
        int active;
        SpringV3 swayRot, swayPos, recoilPos, recoilRot;
        float slideKick, sprintW, reloadW;
        float pumpT = -1f, pumpDuration = 1f;

        public void Init(PlayerRig owner, WeaponController controller, Viewmodel[] vms)
        {
            rig = owner;
            wc = controller;
            models = vms;
        }

        public void SetActive(int index)
        {
            active = index;
            for (int i = 0; i < models.Length; i++) models[i].Root.gameObject.SetActive(i == index);
            pumpT = -1f;
            recoilPos.Reset();
            recoilRot.Reset();
        }

        public void OnFire()
        {
            Viewmodel v = models[active];
            recoilPos.Velocity += new Vector3(0f, v.RecoilBack * 4f, -v.RecoilBack * 32f);
            recoilRot.Velocity += new Vector3(-v.RecoilPitch * 26f, Random.Range(-1f, 1f) * v.RecoilPitch * 4f, Random.Range(-1f, 1f) * v.RecoilPitch * 6f);
            slideKick = 1f;
            if (wc.Current.Def.Id == WeaponId.Shotgun)
            {
                pumpT = 0f;
                pumpDuration = wc.Current.Def.Interval;
            }
        }

        /// <summary>Small kick of the whole gun when a shell seats (the shell itself follows the reload progress).</summary>
        public void OnShellInserted() => recoilRot.Velocity += new Vector3(-25f, 0f, 8f);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || models == null) return;
            Viewmodel v = models[active];
            PlayerMotor motor = rig.Motor;
            float ads = MathUtil.Smooth01(rig.View.AdsBlend);

            Vector3 pos = Vector3.Lerp(v.HipPos, v.AdsPos, ads);
            Quaternion rot = Quaternion.Slerp(Quaternion.Euler(v.HipEuler), Quaternion.identity, ads);

            // Mouse sway: the gun lags behind the view and settles on a spring (reduced in ADS).
            Vector2 look = rig.View.LookDelta;
            float swayScale = 1f - 0.75f * ads;
            Vector3 swayTarget = new Vector3(Mathf.Clamp(look.y * 1.4f, -6f, 6f), Mathf.Clamp(-look.x * 1.4f, -6f, 6f), Mathf.Clamp(-look.x * 2f, -8f, 8f)) * swayScale;
            swayRot.Step(swayTarget, 170f, 18f, dt);
            Vector3 swayPosTarget = new Vector3(Mathf.Clamp(-look.x * 0.0022f, -0.02f, 0.02f), Mathf.Clamp(-look.y * 0.0022f, -0.02f, 0.02f), 0f) * swayScale;
            swayPos.Step(swayPosTarget, 140f, 16f, dt);
            pos += swayPos.Value;
            rot *= Quaternion.Euler(swayRot.Value);

            // Movement bob in sync with the footsteps (S-13 bob proportional to movement).
            float phase = motor.StepPhase * Mathf.PI;
            float bw = Mathf.Min(1.6f, motor.BobWeight) * (1f - 0.85f * ads);
            pos += new Vector3(Mathf.Sin(phase) * 0.009f, -Mathf.Abs(Mathf.Cos(phase)) * 0.008f + 0.004f, 0f) * bw;
            rot *= Quaternion.Euler(Mathf.Abs(Mathf.Cos(phase)) * 1.2f * bw, 0f, Mathf.Sin(phase) * 1.4f * bw);
            pos.y += Mathf.Sin(Time.time * 1.6f) * 0.0015f * (1f - 0.7f * ads);

            sprintW = MathUtil.Damp(sprintW, motor.IsSprinting ? 1f : 0f, 10f, dt);
            pos += new Vector3(-0.03f, -0.035f, -0.02f) * sprintW;
            rot *= Quaternion.Euler(new Vector3(12f, -26f, 10f) * sprintW);

            // Lower/raise during reloads and switches (S-13).
            float reloadTarget = 0f;
            if (wc.IsReloading) reloadTarget = wc.Current.Def.ShellByShell ? 0.5f : Mathf.Sin(Mathf.Clamp01(wc.ReloadProgress) * Mathf.PI) * 0.9f + 0.1f;
            reloadW = MathUtil.Damp(reloadW, reloadTarget, 12f, dt);
            float lower = Mathf.Max(reloadW, MathUtil.Smooth01(wc.SwitchLower));
            pos += new Vector3(-0.02f, -0.11f, -0.03f) * lower;
            rot *= Quaternion.Euler(new Vector3(28f, -12f, 24f) * lower);

            recoilPos.Step(Vector3.zero, 420f, 30f, dt);
            recoilRot.Step(Vector3.zero, 300f, 26f, dt);
            pos += recoilPos.Value * (1f - 0.4f * ads);
            rot *= Quaternion.Euler(recoilRot.Value * (1f - 0.5f * ads));

            v.Root.localPosition = pos;
            v.Root.localRotation = rot;
            AnimateParts(v, dt);
        }

        void AnimateParts(Viewmodel v, float dt)
        {
            if (v.Magazine != null)
            {
                float drop = 0f;
                if (wc.IsReloading && !wc.Current.Def.ShellByShell)
                {
                    float p = wc.ReloadProgress;
                    // Out between 12% and 35%, hidden below until 50%, back in by 68% of the reload.
                    if (p < 0.12f) drop = 0f;
                    else if (p < 0.35f) drop = MathUtil.Smooth01((p - 0.12f) / 0.23f);
                    else if (p < 0.5f) drop = 1f;
                    else if (p < 0.68f) drop = 1f - MathUtil.Smooth01((p - 0.5f) / 0.18f);
                }
                v.Magazine.localPosition = v.MagBase + new Vector3(0f, -0.16f, -0.02f) * drop;
            }
            if (v.Slide != null)
            {
                slideKick = Mathf.MoveTowards(slideKick, 0f, dt * 14f);
                bool locked = wc.Current.Mag == 0 && !wc.IsReloading; // slide locks back on an empty magazine
                float back = locked ? 1f : slideKick;
                v.Slide.localPosition = v.SlideBase + new Vector3(0f, 0f, -0.03f * back);
            }
            if (v.Pump != null)
            {
                float off = 0f;
                if (pumpT >= 0f)
                {
                    pumpT += dt;
                    float u = (pumpT / pumpDuration - 0.25f) / 0.55f;
                    if (u >= 0f && u <= 1f) off = Mathf.Sin(u * Mathf.PI);
                    if (pumpT >= pumpDuration) pumpT = -1f;
                }
                v.Pump.localPosition = v.PumpBase + new Vector3(0f, 0f, -0.075f * off);
            }
            if (v.Shell != null)
            {
                // Each shell travels from below into the loading port during the first 70% of its 0.5 s insert.
                float u = wc.IsReloading ? wc.ReloadProgress / 0.7f : 2f;
                bool show = u <= 1f;
                if (v.Shell.gameObject.activeSelf != show) v.Shell.gameObject.SetActive(show);
                if (show) v.Shell.localPosition = Vector3.Lerp(v.ShellBase + new Vector3(0.03f, -0.06f, -0.05f), v.ShellBase, MathUtil.Smooth01(u));
            }
        }
    }
}
