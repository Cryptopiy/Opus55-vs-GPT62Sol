using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Test bot input (used only by AutoTest via the -autotest command line): aims with simulated mouse deltas at the
    /// nearest visible enemy, fires through the normal weapon code, reloads, strafes. Never active in normal play.
    /// </summary>
    public sealed class BotInput : IInputSource
    {
        public bool AllowFire = true;
        public bool AllowMove = true;
        public bool AimAtHead;
        public int Weapon = 1;
        public EnemyBase ForcedTarget;
        public Vector3? ForcedAimPoint;
        public Vector2 ForcedMove;
        public bool HasForcedMove;
        public bool Sprint, Crouch, Jump, Aim;
        public EnemyBase Target { get; private set; }

        float retarget, strafeTimer, strafeDir = 1f;
        bool semiToggle;

        public void Read(ref InputState s)
        {
            s = default;
            s.SelectWeapon = -1;
            PlayerRig p = Game.Player;
            if (p == null) return;
            WeaponController w = p.Weapons;
            if (w.SwitchTarget != Weapon) s.SelectWeapon = Weapon;

            retarget -= Time.deltaTime;
            if (retarget <= 0f || Target == null || !Target.IsAlive)
            {
                retarget = 0.3f;
                Target = ForcedTarget != null && ForcedTarget.IsAlive ? ForcedTarget : FindTarget(p);
            }

            Vector3? aim = ForcedAimPoint;
            if (aim == null && Target != null && Target.IsAlive)
                aim = AimAtHead ? Target.Body.Head.position : Target.CenterPosition;

            float error = 999f;
            if (aim.HasValue)
            {
                Transform cam = p.Camera.transform;
                Vector3 to = aim.Value - cam.position;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                Vector3 e = cam.rotation.eulerAngles;
                float dYaw = Mathf.DeltaAngle(e.y, wantYaw);
                float dPitch = Mathf.DeltaAngle(e.x, wantPitch);
                float sens = GameConfig.Player.MouseSensitivity * Mathf.Lerp(1f, GameConfig.Player.AdsSensitivityScale, p.View.AdsBlend);
                // Proportional controller on the mouse: converges in a few frames, also fights the rifle's recoil climb.
                s.Look = new Vector2(Mathf.Clamp(dYaw * 0.45f, -25f, 25f), Mathf.Clamp(-dPitch * 0.45f, -25f, 25f)) / sens;
                error = Vector3.Angle(cam.forward, to);
            }

            WeaponSlot slot = w.Current;
            if (AllowFire && aim.HasValue && error < 2.2f && !w.IsSwitching)
            {
                if (slot.Def.Automatic) s.FireHeld = true;
                else
                {
                    semiToggle = !semiToggle;
                    s.FirePressed = semiToggle;
                    s.FireHeld = semiToggle;
                }
            }
            if (slot.Mag == 0 && slot.CanReload) s.ReloadPressed = true;
            s.SprintHeld = Sprint;
            s.CrouchHeld = Crouch;
            s.JumpPressed = Jump;
            s.AimHeld = Aim;

            if (HasForcedMove) s.Move = ForcedMove;
            else if (AllowMove)
            {
                strafeTimer -= Time.deltaTime;
                if (strafeTimer <= 0f)
                {
                    strafeTimer = Random.Range(1.2f, 2.5f);
                    strafeDir = -strafeDir;
                }
                Vector2 move = new Vector2(strafeDir, 0f);
                if (Target != null && Target.IsAlive && Target.DistanceToPlayer < 5f) move.y = -1f;
                // Drift back toward the middle of the arena when close to a wall.
                Vector3 pos = p.Position;
                if (Mathf.Abs(pos.x) > 18f || Mathf.Abs(pos.z) > 18f)
                {
                    Vector3 toCenter = p.transform.InverseTransformDirection(-new Vector3(pos.x, 0f, pos.z).normalized);
                    move = new Vector2(toCenter.x, toCenter.z);
                }
                s.Move = Vector2.ClampMagnitude(move, 1f);
            }
        }

        static EnemyBase FindTarget(PlayerRig p)
        {
            EnemyBase best = null;
            float bd = float.MaxValue;
            Vector3 eye = p.Camera.transform.position;
            var list = Game.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                EnemyBase e = list[i];
                if (!e.IsAlive || e.Fsm.CurrentId == EnemyState.Idle) continue;
                float d = (e.CenterPosition - eye).sqrMagnitude;
                if (d >= bd) continue;
                if (Physics.Linecast(eye, e.CenterPosition, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                bd = d;
                best = e;
            }
            return best;
        }
    }
}
