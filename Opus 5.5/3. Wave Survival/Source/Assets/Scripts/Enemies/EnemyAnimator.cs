using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Joint angles (degrees) of the primitive humanoid; positive pitch = limb swings forward / knee bends.</summary>
    public struct Pose
    {
        public float HipsDrop, SpinePitch, SpineYaw, HeadPitch;
        public float ArmLPitch, ArmLRoll, ForeLPitch, ArmRPitch, ArmRRoll, ForeRPitch;
        public float LegLPitch, ShinLPitch, LegRPitch, ShinRPitch;

        public static Pose Lerp(in Pose a, in Pose b, float t, bool legs)
        {
            Pose p = a;
            p.HipsDrop = Mathf.Lerp(a.HipsDrop, b.HipsDrop, t);
            p.SpinePitch = Mathf.Lerp(a.SpinePitch, b.SpinePitch, t);
            p.SpineYaw = Mathf.Lerp(a.SpineYaw, b.SpineYaw, t);
            p.HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t);
            p.ArmLPitch = Mathf.Lerp(a.ArmLPitch, b.ArmLPitch, t);
            p.ArmLRoll = Mathf.Lerp(a.ArmLRoll, b.ArmLRoll, t);
            p.ForeLPitch = Mathf.Lerp(a.ForeLPitch, b.ForeLPitch, t);
            p.ArmRPitch = Mathf.Lerp(a.ArmRPitch, b.ArmRPitch, t);
            p.ArmRRoll = Mathf.Lerp(a.ArmRRoll, b.ArmRRoll, t);
            p.ForeRPitch = Mathf.Lerp(a.ForeRPitch, b.ForeRPitch, t);
            if (legs)
            {
                p.LegLPitch = Mathf.Lerp(a.LegLPitch, b.LegLPitch, t);
                p.ShinLPitch = Mathf.Lerp(a.ShinLPitch, b.ShinLPitch, t);
                p.LegRPitch = Mathf.Lerp(a.LegRPitch, b.LegRPitch, t);
                p.ShinRPitch = Mathf.Lerp(a.ShinRPitch, b.ShinRPitch, t);
            }
            return p;
        }

        public static readonly Pose Aim = new Pose { SpinePitch = 4f, ArmRPitch = 86f, ForeRPitch = 8f, ArmLPitch = 68f, ArmLRoll = -18f, ForeLPitch = 42f };
        public static readonly Pose Windup = new Pose { SpinePitch = -6f, SpineYaw = -16f, ArmRPitch = 165f, ArmRRoll = 10f, ForeRPitch = 55f, ArmLPitch = -20f, ForeLPitch = 30f };
        public static readonly Pose Strike = new Pose { SpinePitch = 32f, SpineYaw = 22f, ArmRPitch = 35f, ForeRPitch = 8f, ArmLPitch = 25f, ForeLPitch = 40f };
        public static readonly Pose Telegraph = new Pose { HipsDrop = 0.12f, SpinePitch = 26f, HeadPitch = -28f, ArmLPitch = 30f, ArmRPitch = 30f, ArmLRoll = 48f, ArmRRoll = 48f, ForeLPitch = 65f, ForeRPitch = 65f, LegLPitch = 22f, ShinLPitch = 38f, LegRPitch = 22f, ShinRPitch = 38f };
        public static readonly Pose Charge = new Pose { SpinePitch = 38f, HeadPitch = -12f, ArmLPitch = -35f, ArmRPitch = -35f, ArmLRoll = 15f, ArmRRoll = 15f, ForeLPitch = 70f, ForeRPitch = 70f };
        public static readonly Pose Stunned = new Pose { HipsDrop = 0.1f, SpinePitch = 42f, HeadPitch = 38f, ArmLPitch = -6f, ArmRPitch = -6f, ArmLRoll = 10f, ArmRRoll = 10f, ForeLPitch = 10f, ForeRPitch = 10f, LegLPitch = 15f, ShinLPitch = 30f, LegRPitch = 15f, ShinRPitch = 30f };
        public static readonly Pose SlamCrouch = new Pose { HipsDrop = 0.24f, SpinePitch = 32f, HeadPitch = -20f, ArmLPitch = -55f, ArmRPitch = -55f, ForeLPitch = 30f, ForeRPitch = 30f, LegLPitch = 38f, ShinLPitch = 72f, LegRPitch = 38f, ShinRPitch = 72f };
        public static readonly Pose Air = new Pose { SpinePitch = 8f, ArmLPitch = 160f, ArmRPitch = 160f, ArmLRoll = 22f, ArmRRoll = 22f, ForeLPitch = 20f, ForeRPitch = 20f, LegLPitch = 55f, ShinLPitch = 95f, LegRPitch = 30f, ShinRPitch = 80f };
        public static readonly Pose Summon = new Pose { SpinePitch = -10f, HeadPitch = -32f, ArmLPitch = 150f, ArmRPitch = 150f, ArmLRoll = 38f, ArmRRoll = 38f, ForeLPitch = 15f, ForeRPitch = 15f };
        public static readonly Pose Cannon = new Pose { SpinePitch = 6f, SpineYaw = 12f, ArmRPitch = 88f, ForeRPitch = 2f, ArmLPitch = 20f, ArmLRoll = 20f, ForeLPitch = 35f };
        public static readonly Pose Crouch = new Pose { HipsDrop = 0.3f, SpinePitch = 16f, HeadPitch = -6f, ArmRPitch = 35f, ForeRPitch = 45f, ArmLPitch = 30f, ArmLRoll = -10f, ForeLPitch = 55f, LegLPitch = 48f, ShinLPitch = 92f, LegRPitch = 40f, ShinRPitch = 85f };
        public static readonly Pose Limp = new Pose { HeadPitch = -35f, ArmLPitch = 75f, ArmRPitch = 60f, ArmLRoll = 45f, ArmRRoll = 40f, ForeLPitch = 30f, ForeRPitch = 35f, LegLPitch = 15f, ShinLPitch = 45f, LegRPitch = 5f, ShinRPitch = 25f };
    }

    /// <summary>
    /// Procedural animation of an <see cref="EnemyBody"/>: walk cycle from agent speed, blended action poses
    /// (wind-up, aim, telegraph, stun...) and the S-17 death topple (animated fall around the feet).
    /// </summary>
    public sealed class EnemyAnimator
    {
        readonly EnemyBody body;
        readonly float stride, lean;
        readonly Quaternion bSpine, bHead, bArmL, bArmR, bForeL, bForeR, bLegL, bLegR, bShinL, bShinR;
        readonly Vector3 bHips;
        float phase;
        Pose overlay;
        float overlayWeight, overlayTarget;
        bool overlayLegs;
        bool dead;
        float deathT;
        Vector3 fallAxis, pivot;

        public EnemyAnimator(EnemyBody b, float strideLength, float leanDegrees)
        {
            body = b;
            stride = strideLength;
            lean = leanDegrees;
            bHips = b.BindPosition(b.Hips);
            bSpine = b.Bind(b.Spine); bHead = b.Bind(b.Head);
            bArmL = b.Bind(b.ArmL); bArmR = b.Bind(b.ArmR); bForeL = b.Bind(b.ForeL); bForeR = b.Bind(b.ForeR);
            bLegL = b.Bind(b.LegL); bLegR = b.Bind(b.LegR); bShinL = b.Bind(b.ShinL); bShinR = b.Bind(b.ShinR);
        }

        public void Reset()
        {
            dead = false;
            deathT = 0f;
            overlayWeight = 0f;
            overlayTarget = 0f;
            phase = 0f;
            body.ResetPose();
        }

        /// <summary>Blend toward an action pose this frame (call every frame while the action lasts).</summary>
        public void SetPose(in Pose p, float weight = 1f, bool legs = false)
        {
            overlay = p;
            overlayTarget = weight;
            overlayLegs = legs;
        }

        public void ClearPose() => overlayTarget = 0f;

        public void BeginDeath(Vector3 fallDirLocal)
        {
            dead = true;
            deathT = 0f;
            fallDirLocal.y = 0f;
            if (fallDirLocal.sqrMagnitude < 0.01f) fallDirLocal = Vector3.back;
            fallDirLocal.Normalize();
            // Topple around a pivot at the feet on the falling side.
            fallAxis = Vector3.Cross(Vector3.up, fallDirLocal);
            pivot = fallDirLocal * 0.25f;
        }

        public void Update(float speed, float dt, float blendRate = 10f)
        {
            if (dead) { UpdateDeath(dt); return; }
            float s = Mathf.Clamp(speed / 3.5f, 0f, 1.6f);
            phase += Mathf.PI * speed / stride * dt;
            float sp = Mathf.Sin(phase), cp = Mathf.Cos(phase);
            Pose walk = new Pose
            {
                HipsDrop = Mathf.Abs(cp) * 0.035f * s,
                SpinePitch = lean * Mathf.Min(1f, s) + Mathf.Sin(Time.time * 1.7f) * 1.5f,
                SpineYaw = sp * 6f * s,
                ArmLPitch = -sp * 30f * s, ArmRPitch = sp * 30f * s,
                ArmLRoll = 6f, ArmRRoll = 6f,
                ForeLPitch = 18f + 18f * s, ForeRPitch = 18f + 18f * s,
                LegLPitch = sp * 32f * s, LegRPitch = -sp * 32f * s,
                ShinLPitch = 4f + Mathf.Max(0f, cp) * 58f * s, ShinRPitch = 4f + Mathf.Max(0f, -cp) * 58f * s,
            };
            overlayWeight = Mathf.MoveTowards(overlayWeight, overlayTarget, blendRate * dt);
            Pose p = overlayWeight > 0.001f ? Pose.Lerp(walk, overlay, overlayWeight, overlayLegs) : walk;
            Apply(p);
        }

        void Apply(in Pose p)
        {
            body.Hips.localPosition = bHips + new Vector3(0f, -p.HipsDrop, 0f);
            body.Spine.localRotation = bSpine * Quaternion.Euler(p.SpinePitch, p.SpineYaw, 0f);
            body.Head.localRotation = bHead * Quaternion.Euler(p.HeadPitch, 0f, 0f);
            body.ArmL.localRotation = bArmL * Quaternion.Euler(-p.ArmLPitch, 0f, -p.ArmLRoll);
            body.ArmR.localRotation = bArmR * Quaternion.Euler(-p.ArmRPitch, 0f, p.ArmRRoll);
            body.ForeL.localRotation = bForeL * Quaternion.Euler(-p.ForeLPitch, 0f, 0f);
            body.ForeR.localRotation = bForeR * Quaternion.Euler(-p.ForeRPitch, 0f, 0f);
            body.LegL.localRotation = bLegL * Quaternion.Euler(-p.LegLPitch, 0f, 0f);
            body.LegR.localRotation = bLegR * Quaternion.Euler(-p.LegRPitch, 0f, 0f);
            body.ShinL.localRotation = bShinL * Quaternion.Euler(p.ShinLPitch, 0f, 0f);
            body.ShinR.localRotation = bShinR * Quaternion.Euler(p.ShinRPitch, 0f, 0f);
        }

        void UpdateDeath(float dt)
        {
            deathT += dt;
            // Gravity-like acceleration to 90 degrees, then two damped bounces.
            const float accel = 380f;
            float angle = 0.5f * accel * deathT * deathT;
            float tHit = Mathf.Sqrt(2f * 90f / accel);
            if (deathT > tHit)
            {
                float u = deathT - tHit;
                angle = 90f - Mathf.Abs(Mathf.Sin(u * 14f)) * 9f * Mathf.Exp(-u * 6f);
            }
            angle = Mathf.Min(angle, 90f);
            Quaternion r = Quaternion.AngleAxis(angle, fallAxis);
            float s = body.Scale;
            body.Model.localRotation = r;
            body.Model.localPosition = pivot * s - r * (pivot * s);
            float limp = Mathf.Clamp01(deathT / 0.5f);
            Apply(Pose.Lerp(default, Pose.Limp, limp, true));
        }

        public float DeathTime => deathT;
    }
}
